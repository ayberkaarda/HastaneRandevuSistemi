using System.Data;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// Whether the demo accounts of <see cref="DemoData"/> are created. Off by default: their passwords are
/// published in the README, so they must never appear in a database nobody asked for them in.
/// </summary>
public sealed class DemoDataOptions
{
    /// <summary>Demo mode; switched on with HASTANE_DEMO=1 or the <c>Seed:Demo</c> setting.</summary>
    public bool Enabled { get; init; }
}

/// <summary>What demo mode did when the database was initialized.</summary>
public enum DemoSeedOutcome
{
    /// <summary>Demo mode is off; nothing was added.</summary>
    Disabled,

    /// <summary>The database was empty and all demo accounts were added.</summary>
    Seeded,

    /// <summary>The database already had patients, doctors or appointments; nothing was added.</summary>
    SkippedExistingData,
}

internal static class DemoDataSeeder
{
    private const string LockResource = "HastaneRandevu.DemoSeed";
    private const int LockTimeoutMilliseconds = 60_000;

    /// <summary>
    /// Adds every demo doctor and patient, but only to a database without any patient, doctor or
    /// appointment; otherwise nothing is added, so demo accounts with published passwords never end up
    /// next to existing accounts. The emptiness check and all inserts run in one serializable
    /// transaction under an application lock: concurrent starts add the accounts once, and a failure or
    /// cancellation part way leaves no account behind. Rows are inserted one by one in a fixed order,
    /// so a new database gets the same ids every time (doctors 1-3, patients 1-2).
    /// </summary>
    public static async Task<DemoSeedOutcome> SeedAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AppLock.AcquireAsync(db, LockResource, LockTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);

        if (await db.Patients.AnyAsync(cancellationToken).ConfigureAwait(false)
            || await db.Doctors.AnyAsync(cancellationToken).ConfigureAwait(false)
            || await db.Appointments.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return DemoSeedOutcome.SkippedExistingData;
        }

        foreach (var doctor in DemoData.Doctors)
        {
            db.Doctors.Add(new()
            {
                FullName = doctor.FullName,
                Branch = doctor.Branch,
                NationalId = doctor.NationalId,
                PasswordHash = doctor.PasswordHash,
                PasswordFormat = doctor.PasswordFormat,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var patient in DemoData.Patients)
        {
            db.Patients.Add(new()
            {
                FullName = patient.FullName,
                NationalId = patient.NationalId,
                PasswordHash = patient.PasswordHash,
                PasswordFormat = patient.PasswordFormat,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return DemoSeedOutcome.Seeded;
    }
}
