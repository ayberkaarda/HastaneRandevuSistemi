using System.Data;
using System.Globalization;
using System.Reflection;
using HastaneRandevu.Data.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// Adopts a database created by the original application (Hastalar, Doktorlar and Randevular without
/// an EF Core migrations history). The existing rows, identity values and plain text passwords are
/// kept and marked as legacy plain text (they are hashed on each account's next successful sign-in);
/// the missing columns, constraints and indexes of <see cref="InitialCreate"/> and
/// <see cref="AddPasswordFormat"/> are added and both migrations are recorded as applied, so the
/// sample data is never inserted into such a database.
/// </summary>
/// <remarks>
/// Every step checks the current schema first, and the whole upgrade runs in one transaction under
/// an application lock, so it is safe to run repeatedly and from concurrent processes.
/// </remarks>
internal static class LegacySchemaUpgrader
{
    private static readonly string[] LegacyTables = ["Hastalar", "Doktorlar", "Randevular"];

    /// <summary>Identifier of the first migration; a database without it in its history may be legacy.</summary>
    internal static string BaselineMigrationId { get; } = MigrationId<InitialCreate>();

    /// <summary>
    /// The migrations whose schema the upgrade creates itself and records as applied. Besides the
    /// baseline this includes <see cref="AddPasswordFormat"/>, because only the upgrade knows that
    /// every existing password is plain text and has to be marked as such.
    /// </summary>
    internal static IReadOnlyList<string> AdoptedMigrationIds { get; } =
        [BaselineMigrationId, MigrationId<AddPasswordFormat>()];

    private static string MigrationId<TMigration>()
        where TMigration : Migration =>
        typeof(TMigration).GetCustomAttribute<MigrationAttribute>()!.Id;

    /// <summary>
    /// Upgrades a legacy database in place. Returns <see langword="false"/> when there is nothing to
    /// adopt (no database yet, an empty database, or the baseline migration is already applied).
    /// </summary>
    public static async Task<bool> UpgradeIfLegacyAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!await IsLegacyAsync(db, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            db,
            static async (context, token) =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);

                await AcquireUpgradeLockAsync(context, token).ConfigureAwait(false);

                // Another process may have finished the upgrade while this one waited for the lock.
                if (!await IsLegacyAsync(context, token).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return false;
                }

                await EnsureAllTablesExistAsync(context, token).ConfigureAwait(false);
                await ValidateDataAsync(context, token).ConfigureAwait(false);

                foreach (var statement in UpgradeStatements)
                {
                    await ExecuteAsync(context, statement, token).ConfigureAwait(false);
                }

                await MarkAdoptedMigrationsAppliedAsync(context, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return true;
            },
            verifySucceeded: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the transaction scoped application lock that serializes upgrades. sp_getapplock reports
    /// failures through its return code instead of raising an error, so the code is checked and the
    /// upgrade stops before any schema statement runs when the lock was not granted.
    /// </summary>
    private static async Task AcquireUpgradeLockAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
#pragma warning disable EF1002 // Constant statement; the only parameter is the output value.
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = N'HastaneRandevu.LegacySchemaUpgrade', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;",
            [result],
            cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002

        EnsureLockGranted(result.Value is int code ? code : null);
    }

    /// <summary>Throws when an sp_getapplock return code means the lock was not granted.</summary>
    internal static void EnsureLockGranted(int? returnCode)
    {
        if (returnCode is >= 0)
        {
            return;
        }

        var reason = returnCode switch
        {
            -1 => "the request timed out because another upgrade is still running",
            -2 => "the request was canceled",
            -3 => "the request was chosen as a deadlock victim",
            -999 => "the lock request was invalid",
            null => "the lock procedure returned no result",
            _ => "the lock procedure failed",
        };

        throw new InvalidOperationException(
            $"The legacy database upgrade lock could not be acquired ({reason}, sp_getapplock returned "
            + $"{(returnCode?.ToString(CultureInfo.InvariantCulture) ?? "null")}). No changes were made; try again later.");
    }

    private static async Task<bool> IsLegacyAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (applied.Contains(BaselineMigrationId, StringComparer.Ordinal))
        {
            return false;
        }

        return await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false) > 0;
    }

    private static Task<int> CountExistingLegacyTablesAsync(HospitalDbContext db, CancellationToken cancellationToken) =>
        ScalarAsync(
            db,
            "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name IN (N'Hastalar', N'Doktorlar', N'Randevular')",
            cancellationToken);

    private static async Task EnsureAllTablesExistAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        var count = await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false);
        if (count != LegacyTables.Length)
        {
            throw new InvalidOperationException(
                "The database contains only part of the appointment schema (Hastalar, Doktorlar, Randevular) "
                + "and no migration history, so it cannot be upgraded automatically.");
        }
    }

    /// <summary>
    /// Refuses the upgrade, before anything is changed, when existing rows cannot satisfy the new
    /// constraints. Fixing such rows needs a human decision, so no data is altered or deleted here.
    /// Only counts are reported; row contents (names, national ids, passwords) never appear in the message.
    /// </summary>
    private static async Task ValidateDataAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        (string Sql, string Problem)[] checks =
        [
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Hastalar
             WHERE Ad_Soyad IS NULL OR LEN(Ad_Soyad) > 80 OR TC IS NULL OR LEN(TC) > 11
                OR Sifre IS NULL OR LEN(Sifre) > 256
             """,
                "patients with a missing or too long name, national id or password"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Doktorlar
             WHERE Ad_Soyad IS NULL OR LEN(Ad_Soyad) > 100 OR LEN(Brans) > 100 OR TC IS NULL OR LEN(TC) > 11
                OR Sifre IS NULL OR LEN(Sifre) > 256
             """,
                "doctors with a missing or too long name, branch, national id or password"),
            ("""
             SELECT COUNT(*) AS [Value] FROM (
                 SELECT TC FROM dbo.Hastalar GROUP BY TC HAVING COUNT(*) > 1) d
             """,
                "national ids shared by more than one patient"),
            ("""
             SELECT COUNT(*) AS [Value] FROM (
                 SELECT TC FROM dbo.Doktorlar GROUP BY TC HAVING COUNT(*) > 1) d
             """,
                "national ids shared by more than one doctor"),
            ("""
             SELECT COUNT(*) AS [Value] FROM (
                 SELECT Ad_Soyad FROM dbo.Doktorlar GROUP BY Ad_Soyad HAVING COUNT(*) > 1) d
             """,
                "doctor names shared by more than one doctor (doctors sign in with their name)"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Randevular r
             WHERE r.HastaID IS NULL OR r.DoktorID IS NULL OR r.RandevuTarihi IS NULL
                OR NOT EXISTS (SELECT 1 FROM dbo.Hastalar h WHERE h.HastaID = r.HastaID)
                OR NOT EXISTS (SELECT 1 FROM dbo.Doktorlar d WHERE d.DoktorID = r.DoktorID)
             """,
                "appointments without an existing patient, doctor or date"),
            ("""
             SELECT COUNT(*) AS [Value] FROM (
                 SELECT DoktorID, RandevuTarihi FROM dbo.Randevular
                 GROUP BY DoktorID, RandevuTarihi HAVING COUNT(*) > 1) d
             """,
                "doctor time slots booked more than once"),
        ];

        var problems = new List<string>();
        foreach (var (sql, problem) in checks)
        {
            var count = await ScalarAsync(db, sql, cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                problems.Add($"{count} {problem}");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The existing appointment database cannot be upgraded until these rows are corrected: "
                + string.Join("; ", problems) + ". No changes were made.");
        }
    }

    /// <summary>
    /// Brings the legacy tables to the shape created by <see cref="InitialCreate"/> and
    /// <see cref="AddPasswordFormat"/>. Each statement
    /// is guarded so it only runs when the corresponding part of the schema is missing.
    /// </summary>
    private static readonly string[] UpgradeStatements =
    [
        // System-named keys are dropped and recreated under the model's names; the unique national id
        // constraints become unique indexes.
        DropForeignKeysExcept("Randevular", "FK_Randevular_Hastalar", "FK_Randevular_Doktorlar"),
        DropUniqueConstraints("Hastalar"),
        DropUniqueConstraints("Doktorlar"),

        AlterColumn("Hastalar", "Ad_Soyad", "nvarchar", 160, "NVARCHAR(80) NOT NULL"),
        AlterColumn("Hastalar", "TC", "nvarchar", 22, "NVARCHAR(11) NOT NULL"),
        AlterColumn("Hastalar", "Sifre", "nvarchar", 512, "NVARCHAR(256) NOT NULL"),
        AlterColumn("Doktorlar", "Ad_Soyad", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Doktorlar", "Brans", "nvarchar", 200, "NVARCHAR(100) NULL", nullable: true),
        AlterColumn("Doktorlar", "TC", "nvarchar", 22, "NVARCHAR(11) NOT NULL"),
        AlterColumn("Doktorlar", "Sifre", "nvarchar", 512, "NVARCHAR(256) NOT NULL"),
        AlterColumn("Randevular", "HastaID", "int", 4, "INT NOT NULL"),
        AlterColumn("Randevular", "DoktorID", "int", 4, "INT NOT NULL"),
        AlterColumn("Randevular", "RandevuTarihi", "datetime", 8, "DATETIME NOT NULL"),

        // Existing rows get the values of a fresh account and of an active appointment.
        AddColumnWithInitialValue("Hastalar", "BasarisizGiris", "INT NOT NULL", "0"),
        AddColumn("Hastalar", "KilitBitis", "DATETIME2 NULL"),
        AddColumnWithInitialValue("Doktorlar", "BasarisizGiris", "INT NOT NULL", "0"),
        AddColumn("Doktorlar", "KilitBitis", "DATETIME2 NULL"),
        AddColumnWithInitialValue("Randevular", "Durum", "INT NOT NULL", "0"),
        AddColumn("Randevular", "IptalZamani", "DATETIME2 NULL"),

        // AddPasswordFormat: every password of the original application is plain text (format 1),
        // whatever it looks like. A successful sign-in replaces value and format together.
        AddColumnWithInitialValue("Hastalar", "SifreBicimi", "INT NOT NULL", "1"),
        AddColumnWithInitialValue("Doktorlar", "SifreBicimi", "INT NOT NULL", "1"),

        RenamePrimaryKey("Hastalar", "PK_Hastalar"),
        RenamePrimaryKey("Doktorlar", "PK_Doktorlar"),
        RenamePrimaryKey("Randevular", "PK_Randevular"),

        AddForeignKey("Randevular", "FK_Randevular_Hastalar", "HastaID", "Hastalar", "HastaID"),
        AddForeignKey("Randevular", "FK_Randevular_Doktorlar", "DoktorID", "Doktorlar", "DoktorID"),

        AddCheck("Hastalar", "CK_Hastalar_BasarisizGiris", SchemaLimits.FailedLoginCheck),
        AddCheck("Doktorlar", "CK_Doktorlar_BasarisizGiris", SchemaLimits.FailedLoginCheck),
        AddCheck("Randevular", "CK_Randevular_Durum", SchemaLimits.StatusCheck),
        AddCheck("Hastalar", "CK_Hastalar_SifreBicimi", SchemaLimits.PasswordFormatCheck),
        AddCheck("Doktorlar", "CK_Doktorlar_SifreBicimi", SchemaLimits.PasswordFormatCheck),

        CreateIndex("Hastalar", "IX_Hastalar_TC", "CREATE UNIQUE INDEX IX_Hastalar_TC ON dbo.Hastalar (TC)"),
        CreateIndex("Doktorlar", "IX_Doktorlar_TC", "CREATE UNIQUE INDEX IX_Doktorlar_TC ON dbo.Doktorlar (TC)"),
        CreateIndex("Doktorlar", "IX_Doktorlar_Ad_Soyad", "CREATE UNIQUE INDEX IX_Doktorlar_Ad_Soyad ON dbo.Doktorlar (Ad_Soyad)"),
        CreateIndex(
            "Randevular",
            "IX_Randevular_DoktorID_RandevuTarihi",
            "CREATE UNIQUE INDEX IX_Randevular_DoktorID_RandevuTarihi ON dbo.Randevular (DoktorID, RandevuTarihi) WHERE "
                + SchemaLimits.ActiveAppointmentFilter),
        CreateIndex(
            "Randevular",
            "IX_Randevular_HastaID_RandevuTarihi",
            "CREATE INDEX IX_Randevular_HastaID_RandevuTarihi ON dbo.Randevular (HastaID, RandevuTarihi)"),
    ];

    private static string AlterColumn(string table, string column, string typeName, int maxLength, string definition, bool nullable = false) =>
        $"""
        IF EXISTS (
            SELECT 1 FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.{table}') AND c.name = N'{column}'
              AND (c.is_nullable <> {(nullable ? 1 : 0)} OR t.name <> N'{typeName}' OR c.max_length <> {maxLength}))
            EXEC(N'ALTER TABLE dbo.{table} ALTER COLUMN {column} {definition}');
        """;

    private static string AddColumn(string table, string column, string definition) =>
        $"""
        IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} ADD {column} {definition}');
        """;

    /// <summary>
    /// Adds a NOT NULL column filled with <paramref name="value"/> for the existing rows. The default
    /// constraint only exists for the duration of the statement, as the migration creates none.
    /// </summary>
    private static string AddColumnWithInitialValue(string table, string column, string definition, string value) =>
        $"""
        IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL
        BEGIN
            EXEC(N'ALTER TABLE dbo.{table} ADD {column} {definition} CONSTRAINT DF_Upgrade_{table}_{column} DEFAULT {value} WITH VALUES');
            EXEC(N'ALTER TABLE dbo.{table} DROP CONSTRAINT DF_Upgrade_{table}_{column}');
        END
        """;

    private static string RenamePrimaryKey(string table, string name) =>
        $"""
        DECLARE @current sysname = (
            SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.{table}') AND type = 'PK');
        IF @current IS NOT NULL AND @current <> N'{name}'
        BEGIN
            DECLARE @qualified nvarchar(300) = N'dbo.' + QUOTENAME(@current);
            EXEC sp_rename @objname = @qualified, @newname = N'{name}', @objtype = N'OBJECT';
        END
        """;

    /// <summary>Drops the foreign keys of <paramref name="table"/> that do not carry one of the model's names.</summary>
    private static string DropForeignKeysExcept(string table, params string[] keep) =>
        $"""
        DECLARE @drop nvarchar(max) = N'';
        SELECT @drop += N'ALTER TABLE dbo.{table} DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
        FROM sys.foreign_keys fk
        WHERE fk.parent_object_id = OBJECT_ID(N'dbo.{table}')
          AND fk.name NOT IN ({string.Join(", ", keep.Select(k => $"N'{k}'"))});
        IF @drop <> N'' EXEC(@drop);
        """;

    /// <summary>Drops the unique constraints of <paramref name="table"/>; the model uses unique indexes instead.</summary>
    private static string DropUniqueConstraints(string table) =>
        $"""
        DECLARE @dropUnique nvarchar(max) = N'';
        SELECT @dropUnique += N'ALTER TABLE dbo.{table} DROP CONSTRAINT ' + QUOTENAME(kc.name) + N';'
        FROM sys.key_constraints kc
        WHERE kc.parent_object_id = OBJECT_ID(N'dbo.{table}') AND kc.type = 'UQ';
        IF @dropUnique <> N'' EXEC(@dropUnique);
        """;

    /// <summary>Adds the named, non-cascading foreign key when it is missing.</summary>
    private static string AddForeignKey(string table, string name, string column, string principal, string principalColumn) =>
        $"""
        IF OBJECT_ID(N'dbo.{name}', N'F') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} WITH CHECK ADD CONSTRAINT {name} FOREIGN KEY ({column}) REFERENCES dbo.{principal} ({principalColumn})');
        """;

    private static string AddCheck(string table, string name, string condition) =>
        $"""
        IF OBJECT_ID(N'dbo.{name}', N'C') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} WITH CHECK ADD CONSTRAINT {name} CHECK ({condition.Replace("'", "''", StringComparison.Ordinal)})');
        """;

    private static string CreateIndex(string table, string name, string createStatement) =>
        $"""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'{name}')
            EXEC(N'{createStatement.Replace("'", "''", StringComparison.Ordinal)}');
        """;

    private static async Task MarkAdoptedMigrationsAppliedAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        var history = db.GetService<IHistoryRepository>();
        await ExecuteAsync(db, history.GetCreateIfNotExistsScript(), cancellationToken).ConfigureAwait(false);
        foreach (var migrationId in AdoptedMigrationIds)
        {
            await ExecuteAsync(
                db,
                history.GetInsertScript(new HistoryRow(migrationId, ProductInfo.GetVersion())),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task<int> ScalarAsync(HospitalDbContext db, string sql, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // The statements are built from constants in this class, never from user input.
        db.Database.SqlQueryRaw<int>(sql).SingleAsync(cancellationToken);
#pragma warning restore EF1002

    private static Task<int> ExecuteAsync(HospitalDbContext db, string sql, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // The statements are built from constants in this class, never from user input.
        db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
#pragma warning restore EF1002
}
