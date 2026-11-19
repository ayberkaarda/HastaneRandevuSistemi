using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Data.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.Data.Tests;

/// <summary>Runs against a real SQL Server instance; see <see cref="SqlServerTestDatabase"/>.</summary>
public sealed class LegacyUpgradeTests
{
    private const string BackupEnvironmentVariable = "HASTANE_LEGACY_BACKUP";

    // Schema of the original application's database (read from its backup): system-named primary,
    // unique and foreign keys, plain text passwords in NVARCHAR(100), no migration history.
    internal const string LegacySchema = """
        CREATE TABLE dbo.Hastalar (
            HastaID INT IDENTITY(1,1) PRIMARY KEY,
            Ad_Soyad NVARCHAR(80) NOT NULL,
            TC NVARCHAR(11) NOT NULL UNIQUE,
            Sifre NVARCHAR(100) NOT NULL);
        CREATE TABLE dbo.Doktorlar (
            DoktorID INT IDENTITY(1,1) PRIMARY KEY,
            Ad_Soyad NVARCHAR(100) NOT NULL,
            Brans NVARCHAR(100) NULL,
            TC NVARCHAR(11) NOT NULL UNIQUE,
            Sifre NVARCHAR(100) NOT NULL);
        CREATE TABLE dbo.Randevular (
            RandevuID INT IDENTITY(1,1) PRIMARY KEY,
            HastaID INT NOT NULL FOREIGN KEY REFERENCES dbo.Hastalar (HastaID),
            DoktorID INT NOT NULL FOREIGN KEY REFERENCES dbo.Doktorlar (DoktorID),
            RandevuTarihi DATETIME NOT NULL);
        """;

    // Fictional rows. Identity values deliberately do not start at 1 so overwritten or re-seeded rows
    // would show; appointment times carry seconds, as the original date picker stored them.
    private const string LegacyData = """
        SET IDENTITY_INSERT dbo.Hastalar ON;
        INSERT dbo.Hastalar (HastaID, Ad_Soyad, TC, Sifre) VALUES
            (3, N'Eski Hasta', N'20000000001', N'sifre1'),
            (6, N'Diğer Hasta', N'20000000002', N'1234');
        SET IDENTITY_INSERT dbo.Hastalar OFF;
        SET IDENTITY_INSERT dbo.Doktorlar ON;
        INSERT dbo.Doktorlar (DoktorID, Ad_Soyad, Brans, TC, Sifre) VALUES
            (2, N'Dr. Eski Doktor', N'Dahiliye', N'20000000003', N'5678'),
            (5, N'Dr. Diğer Doktor', N'Ortopedi', N'20000000004', N'91011');
        SET IDENTITY_INSERT dbo.Doktorlar OFF;
        SET IDENTITY_INSERT dbo.Randevular ON;
        INSERT dbo.Randevular (RandevuID, HastaID, DoktorID, RandevuTarihi) VALUES
            (4, 3, 2, '2025-05-24T03:01:14'),
            (7, 6, 5, '2025-05-24T03:01:14'),
            (9, 3, 5, '2026-03-04T11:00:00');
        SET IDENTITY_INSERT dbo.Randevular OFF;
        """;

    [Fact]
    public async Task Initialize_LegacyDatabase_PreservesDataAndEnablesOperations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(LegacyData, ct);

        await using var provider = database.BuildServices(demo: false);
        var initializer = provider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync(ct);
        await initializer.InitializeAsync(ct); // Second start must be a no-op.

        // Existing rows and identities are kept; no sample data was added.
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Hastalar WHERE HastaID IN (3, 6)", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Hastalar", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Doktorlar", ct));
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular WHERE Durum = 0 AND IptalZamani IS NULL", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Hastalar WHERE BasarisizGiris = 0 AND KilitBitis IS NULL", ct));
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%_InitialCreate'",
            ct));
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%_AddPasswordFormat'",
            ct));
        Assert.Equal(4, await database.ScalarAsync(
            "SELECT (SELECT COUNT(*) FROM dbo.Hastalar WHERE SifreBicimi = 1) + (SELECT COUNT(*) FROM dbo.Doktorlar WHERE SifreBicimi = 1)",
            ct));
        await AssertUpgradedSchemaAsync(database, ct);

        // Plain text passwords still work once and are replaced with hashes.
        var auth = provider.GetRequiredService<IAuthService>();
        var patient = await auth.SignInPatientAsync("20000000001", "sifre1", ct);
        var doctor = await auth.SignInDoctorAsync("Dr. Diğer Doktor", "91011", ct);
        Assert.Equal(3, patient.AccountId);
        Assert.Equal(5, doctor.AccountId);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Hastalar WHERE HastaID = 3 AND Sifre LIKE N'PBKDF2-SHA256$%' AND SifreBicimi = 0", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Hastalar WHERE HastaID = 6 AND Sifre = N'1234' AND SifreBicimi = 1", ct));
        await auth.SignInPatientAsync("20000000001", "sifre1", ct);

        // Existing appointments are listed for their owners, past ones included.
        var appointments = provider.GetRequiredService<IAppointmentService>();
        Assert.Equal([9, 4], (await appointments.GetMyAppointmentsAsync(patient, ct)).Select(a => a.Id));
        Assert.Equal([7, 9], (await appointments.GetMyAppointmentsAsync(doctor, ct)).Select(a => a.Id));

        // New rows work on the upgraded schema and continue after the existing identities.
        await auth.RegisterPatientAsync("Yeni Hasta", "10000000696", "Gizli.Sifre1", ct);
        Assert.True(await database.ScalarAsync("SELECT HastaID FROM dbo.Hastalar WHERE TC = N'10000000696'", ct) > 6);
        var booked = await appointments.BookAsync(patient, 5, TestServices.Tomorrow10, ct);
        Assert.True(booked > 9);
        await appointments.CancelAsync(patient, 9, ct);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular WHERE RandevuID = 9 AND Durum = 1", ct));

        // The database itself refuses rows that break the new constraints.
        var duplicate = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.Randevular (HastaID, DoktorID, RandevuTarihi, Durum) VALUES (6, 5, '2026-03-03T10:00:00', 0)",
            ct));
        Assert.Contains("IX_Randevular_DoktorID_RandevuTarihi", duplicate.Message, StringComparison.Ordinal);
        var status = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.Randevular (HastaID, DoktorID, RandevuTarihi, Durum) VALUES (6, 5, '2026-03-03T11:00:00', 5)",
            ct));
        Assert.Contains("CK_Randevular_Durum", status.Message, StringComparison.Ordinal);
        var nationalId = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.Hastalar (Ad_Soyad, TC, Sifre, SifreBicimi, BasarisizGiris) VALUES (N'X', N'20000000002', N'-', 0, 0)",
            ct));
        Assert.Contains("IX_Hastalar_TC", nationalId.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The original registration form had no upper limit, so the NVARCHAR(100) column may hold
    /// passwords longer than the limit for new passwords. They must keep working, also once hashed.
    /// </summary>
    [Theory]
    [InlineData(65)]
    [InlineData(80)]
    [InlineData(100)]
    public async Task Initialize_LegacyLongPassword_RemainsUsableAfterRehash(int length)
    {
        var ct = TestContext.Current.CancellationToken;
        var password = SyntheticPassword(length);
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(
            """
            INSERT dbo.Hastalar (Ad_Soyad, TC, Sifre) VALUES (N'Uzun Şifreli Hasta', N'20000000001', @password);
            INSERT dbo.Doktorlar (Ad_Soyad, Brans, TC, Sifre) VALUES (N'Dr. Uzun Şifreli', N'Dahiliye', N'20000000003', @password);
            """,
            ct,
            ("@password", password));

        await using var provider = database.BuildServices(demo: false);
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var auth = provider.GetRequiredService<IAuthService>();

        // First sign-in verifies the plain text and stores a hash, the second one verifies that hash.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await auth.SignInPatientAsync("20000000001", password, ct);
            await auth.SignInDoctorAsync("Dr. Uzun Şifreli", password, ct);
        }

        Assert.Equal(2, await database.ScalarAsync(
            """
            SELECT (SELECT COUNT(*) FROM dbo.Hastalar WHERE Sifre LIKE N'PBKDF2-SHA256$%' AND BasarisizGiris = 0)
                 + (SELECT COUNT(*) FROM dbo.Doktorlar WHERE Sifre LIKE N'PBKDF2-SHA256$%' AND BasarisizGiris = 0)
            """,
            ct));
    }

    /// <summary>
    /// The original application let users pick any text, including text that starts like a stored
    /// hash. The stored format column, not the value, decides how a password is checked.
    /// </summary>
    [Fact]
    public async Task Initialize_LegacyPasswordWithHashPrefix_CanSignInAndUpgrade()
    {
        var ct = TestContext.Current.CancellationToken;
        const string PatientPassword = "PBKDF2-SHA256$eski01"; // 20 characters, not a valid hash
        const string DoctorPassword = "PBKDF2-SHA256$1000$a$b";
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(
            """
            INSERT dbo.Hastalar (Ad_Soyad, TC, Sifre) VALUES (N'Önekli Hasta', N'20000000001', @patient);
            INSERT dbo.Doktorlar (Ad_Soyad, Brans, TC, Sifre) VALUES (N'Dr. Önekli', N'Dahiliye', N'20000000003', @doctor);
            """,
            ct,
            ("@patient", PatientPassword),
            ("@doctor", DoctorPassword));

        await using var provider = database.BuildServices(demo: false);
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var auth = provider.GetRequiredService<IAuthService>();

        // The upgrade marks every existing password as legacy plain text.
        Assert.Equal(2, await database.ScalarAsync(
            "SELECT (SELECT COUNT(*) FROM dbo.Hastalar WHERE SifreBicimi = 1) + (SELECT COUNT(*) FROM dbo.Doktorlar WHERE SifreBicimi = 1)",
            ct));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await auth.SignInPatientAsync("20000000001", PatientPassword, ct);
            await auth.SignInDoctorAsync("Dr. Önekli", DoctorPassword, ct);
        }

        // The first sign-in replaced the value and its format together.
        Assert.Equal(2, await database.ScalarAsync(
            """
            SELECT (SELECT COUNT(*) FROM dbo.Hastalar WHERE SifreBicimi = 0 AND Sifre LIKE N'PBKDF2-SHA256$%' AND Sifre <> @patient)
                 + (SELECT COUNT(*) FROM dbo.Doktorlar WHERE SifreBicimi = 0 AND Sifre LIKE N'PBKDF2-SHA256$%' AND Sifre <> @doctor)
            """,
            ct,
            ("@patient", PatientPassword),
            ("@doctor", DoctorPassword)));

        // Knowing the stored hash is not knowing the password: there is no plain text fallback.
        var patientHash = await database.TextAsync("SELECT Sifre FROM dbo.Hastalar WHERE TC = N'20000000001'", ct);
        var doctorHash = await database.TextAsync("SELECT Sifre FROM dbo.Doktorlar WHERE TC = N'20000000003'", ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInPatientAsync("20000000001", patientHash, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInDoctorAsync("Dr. Önekli", doctorHash, ct));
        Assert.Equal(2, await database.ScalarAsync(
            "SELECT (SELECT SUM(BasarisizGiris) FROM dbo.Hastalar) + (SELECT SUM(BasarisizGiris) FROM dbo.Doktorlar)",
            ct));
    }

    [Fact]
    public async Task Initialize_LegacyDatabaseWithConflictingRows_RefusesWithoutChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(
            """
            INSERT dbo.Hastalar (Ad_Soyad, TC, Sifre) VALUES (N'Gizli Ad', N'20000000001', N'a');
            INSERT dbo.Doktorlar (Ad_Soyad, Brans, TC, Sifre) VALUES
                (N'Dr. Aynı İsim', N'Dahiliye', N'20000000002', N'b'),
                (N'Dr. Aynı İsim', N'Ortopedi', N'20000000003', N'c');
            DECLARE @doctor INT = (SELECT MIN(DoktorID) FROM dbo.Doktorlar);
            INSERT dbo.Randevular (HastaID, DoktorID, RandevuTarihi) VALUES
                (1, @doctor, '2025-05-24T03:01:14'),
                (1, @doctor, '2025-05-24T03:01:14');
            """,
            ct);

        await using var provider = database.BuildServices(demo: false);
        var error = await Assert.ThrowsAsync<DatabaseInitializationException>(
            () => provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct));

        var message = error.InnerException?.Message;
        Assert.Contains("doctor names shared by more than one doctor", message, StringComparison.Ordinal);
        Assert.Contains("doctor time slots booked more than once", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Aynı", message, StringComparison.Ordinal);
        Assert.DoesNotContain("2000000000", message, StringComparison.Ordinal);
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.tables WHERE name = N'__EFMigrationsHistory'", ct));
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Hastalar') AND name = N'Sifre' AND max_length = 200",
            ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.columns WHERE name = N'Durum'", ct));
    }

    [Fact]
    public async Task Initialize_DefaultConfiguration_DoesNotCreateDemoCredentials()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);

        await using var provider = database.BuildServices(demo: false);
        var initializer = provider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync(ct);
        await initializer.InitializeAsync(ct);

        // The migrations create the schema only: no account, so no published password works.
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Doktorlar", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Hastalar", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular", ct));
        var auth = provider.GetRequiredService<IAuthService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInDoctorAsync("Dr. Ada Örnek", "Demo.Doktor1", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInPatientAsync("10000000450", "Demo.Hasta1", ct));
        await AssertUpgradedSchemaAsync(database, ct);
    }

    [Fact]
    public async Task Initialize_EmptyDatabaseInDemoMode_CreatesTheFictionalDemoAccountsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);

        await using var provider = database.BuildServices(demo: true);
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct); // no duplicates

        var session = await provider.GetRequiredService<IAuthService>().SignInDoctorAsync("Dr. Ece Taslak", "Demo.Doktor1", ct);
        Assert.Equal(3, session.AccountId);
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Doktorlar", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Hastalar", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular", ct));
        Assert.Equal(5, await database.ScalarAsync(
            "SELECT (SELECT COUNT(*) FROM dbo.Hastalar WHERE SifreBicimi = 0) + (SELECT COUNT(*) FROM dbo.Doktorlar WHERE SifreBicimi = 0)",
            ct));
        await AssertUpgradedSchemaAsync(database, ct);
    }

    /// <summary>
    /// Restores a backup of the original application's database (path in HASTANE_LEGACY_BACKUP,
    /// readable by the SQL Server service) and upgrades it. Only counts, identities and checksums are
    /// compared, so no personal data from the backup ends up in test output.
    /// </summary>
    [Fact]
    public async Task Initialize_RestoredLegacyBackup_KeepsAllRows()
    {
        var backup = Environment.GetEnvironmentVariable(BackupEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(backup))
        {
            Assert.Skip($"{BackupEnvironmentVariable} is not set.");
        }

        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await RestoreAsync(database, backup, ct);

        const string Counts = """
            SELECT CHECKSUM_AGG(CHECKSUM(*)) FROM (
                SELECT 'H' AS T, COUNT(*) AS C, MAX(HastaID) AS M FROM dbo.Hastalar
                UNION ALL SELECT 'D', COUNT(*), MAX(DoktorID) FROM dbo.Doktorlar
                UNION ALL SELECT 'R', COUNT(*), MAX(RandevuID) FROM dbo.Randevular) s
            """;
        const string Appointments = """
            SELECT CHECKSUM_AGG(CHECKSUM(RandevuID, HastaID, DoktorID, RandevuTarihi)) FROM dbo.Randevular
            """;
        const string People = """
            SELECT CHECKSUM_AGG(c) FROM (
                SELECT CHECKSUM(HastaID, Ad_Soyad, TC, Sifre) AS c FROM dbo.Hastalar
                UNION ALL SELECT CHECKSUM(DoktorID, Ad_Soyad, Brans, TC, Sifre) FROM dbo.Doktorlar) s
            """;
        var countsBefore = await database.ScalarAsync(Counts, ct);
        var appointmentsBefore = await database.ScalarAsync(Appointments, ct);
        var peopleBefore = await database.ScalarAsync(People, ct);
        var rowsBefore = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular", ct);

        await using var provider = database.BuildServices(demo: false);
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.True(rowsBefore > 0, "The backup has no appointments.");
        Assert.Equal(countsBefore, await database.ScalarAsync(Counts, ct));
        Assert.Equal(appointmentsBefore, await database.ScalarAsync(Appointments, ct));
        Assert.Equal(peopleBefore, await database.ScalarAsync(People, ct));
        Assert.Equal(rowsBefore, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular WHERE Durum = 0", ct));
        Assert.Equal(0, await database.ScalarAsync(
            "SELECT (SELECT COUNT(*) FROM dbo.Hastalar WHERE SifreBicimi <> 1) + (SELECT COUNT(*) FROM dbo.Doktorlar WHERE SifreBicimi <> 1)",
            ct));
        await AssertUpgradedSchemaAsync(database, ct);
    }

    /// <summary>A fictional password without white space of exactly <paramref name="length"/> characters.</summary>
    private static string SyntheticPassword(int length) =>
        string.Concat(Enumerable.Repeat("Eski.Parola-42/", 10))[..length];

    private static async Task AssertUpgradedSchemaAsync(SqlServerTestDatabase database, CancellationToken ct)
    {
        string[] expected =
        [
            "PK_Hastalar", "PK_Doktorlar", "PK_Randevular",
            "FK_Randevular_Hastalar", "FK_Randevular_Doktorlar",
            "CK_Hastalar_BasarisizGiris", "CK_Doktorlar_BasarisizGiris", "CK_Randevular_Durum",
            "CK_Hastalar_SifreBicimi", "CK_Doktorlar_SifreBicimi",
        ];
        foreach (var name in expected)
        {
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.objects WHERE name = @name", ct, ("@name", name)));
        }

        // The system-named unique constraints are replaced by named unique indexes.
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.key_constraints WHERE type = 'UQ'", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.default_constraints", ct));
        Assert.Equal(3, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 1 AND has_filter = 0 AND name IN (N'IX_Hastalar_TC', N'IX_Doktorlar_TC', N'IX_Doktorlar_Ad_Soyad')",
            ct));
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE name = N'IX_Randevular_DoktorID_RandevuTarihi' AND is_unique = 1 AND has_filter = 1 AND filter_definition = N'([Durum]=(0))'",
            ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.indexes WHERE name = N'IX_Randevular_HastaID_RandevuTarihi'", ct));
        Assert.Equal(2, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.columns WHERE name = N'Sifre' AND max_length = 512 AND object_id IN (OBJECT_ID(N'dbo.Hastalar'), OBJECT_ID(N'dbo.Doktorlar'))",
            ct));
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID(N'dbo.Randevular') AND c.name = N'RandevuTarihi' AND t.name = N'datetime'",
            ct));
    }

    private static async Task RestoreAsync(SqlServerTestDatabase database, string backup, CancellationToken ct)
    {
        await SqlServerTestDatabase.ExecuteAsync(
            database.Server,
            """
            DECLARE @files TABLE (
                LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128), Size numeric(20,0),
                MaxSize numeric(20,0), FileId bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0), UniqueId uniqueidentifier,
                ReadOnlyLSN numeric(25,0), ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int,
                LogGroupGUID uniqueidentifier, DifferentialBaseLSN numeric(25,0), DifferentialBaseGUID uniqueidentifier, IsReadOnly bit,
                IsPresent bit, TDEThumbprint varbinary(32), SnapshotUrl nvarchar(360));
            INSERT @files EXEC sp_executesql N'RESTORE FILELISTONLY FROM DISK = @p', N'@p nvarchar(4000)', @p = @backup;
            DECLARE @data nvarchar(128) = (SELECT LogicalName FROM @files WHERE Type = 'D');
            DECLARE @log nvarchar(128) = (SELECT LogicalName FROM @files WHERE Type = 'L');
            DECLARE @path nvarchar(400) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400));
            DECLARE @mdf nvarchar(500) = @path + @name + N'.mdf';
            DECLARE @ldf nvarchar(500) = @path + @name + N'_log.ldf';
            RESTORE DATABASE @name FROM DISK = @backup WITH MOVE @data TO @mdf, MOVE @log TO @ldf, RECOVERY;
            """,
            ct,
            ("@name", database.Name),
            ("@backup", backup));
    }
}
