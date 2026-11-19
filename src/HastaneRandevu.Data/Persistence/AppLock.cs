using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// Exclusive SQL Server application locks owned by the current transaction and released when it ends.
/// SQLite (used by the tests) needs none: its writing transactions start with BEGIN IMMEDIATE and are
/// already serialized.
/// </summary>
internal static class AppLock
{
    /// <summary>
    /// Takes the lock on <paramref name="resource"/> inside the current transaction. sp_getapplock reports
    /// failures through its return code instead of raising an error, so the code is checked and nothing
    /// else runs in the transaction when the lock was not granted.
    /// </summary>
    /// <exception cref="InvalidOperationException">No transaction is open, or the lock was not granted.</exception>
    public static async Task AcquireAsync(HospitalDbContext db, string resource, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            return;
        }

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An application lock is owned by a transaction; begin the transaction first.");
        }

        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var name = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
        var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = timeoutMilliseconds };
#pragma warning disable EF1002 // Constant statement; every value is a parameter.
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout;",
            [result, name, timeout],
            cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002

        if (result.Value is int code && code >= 0)
        {
            return;
        }

        var returned = result.Value is int value ? value.ToString(CultureInfo.InvariantCulture) : "no result";
        throw new InvalidOperationException(
            $"The lock '{resource}' could not be acquired (sp_getapplock returned {returned}). No changes were made; try again.");
    }
}

/// <summary>
/// One lock per doctor that serializes every write depending on the doctor's schedule: changing the weekly
/// hours or the leave days, and booking or moving an appointment with the doctor. Each of these
/// transactions takes exactly one doctor lock, as its first statement, and no other application lock
/// afterwards, so two of them can never wait on each other in opposite order.
/// </summary>
internal static class DoctorLock
{
    private const int TimeoutMilliseconds = 30_000;

    public static Task AcquireAsync(HospitalDbContext db, int doctorId, CancellationToken cancellationToken) =>
        AppLock.AcquireAsync(
            db,
            "HastaneRandevu.Doctor." + doctorId.ToString(CultureInfo.InvariantCulture),
            TimeoutMilliseconds,
            cancellationToken);
}
