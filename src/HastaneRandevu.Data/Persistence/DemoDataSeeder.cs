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

internal static class DemoDataSeeder
{
    /// <summary>
    /// Adds the demo doctors and patients that are missing, matched by national id, so it can run on every
    /// start. Existing rows are never changed. Rows are inserted in a fixed order, so a new database gets
    /// the same ids every time (doctors 1-3, patients 1-2).
    /// </summary>
    public static async Task SeedAsync(HospitalDbContext db, CancellationToken cancellationToken)
    {
        var doctorIds = DemoData.Doctors.Select(d => d.NationalId).ToList();
        var doctorNames = DemoData.Doctors.Select(d => d.FullName).ToList();
        var patientIds = DemoData.Patients.Select(p => p.NationalId).ToList();

        // Doctors are unique by national id and by name; a clash on either means the doctor exists.
        var clashing = await db.Doctors
            .Where(d => doctorIds.Contains(d.NationalId) || doctorNames.Contains(d.FullName))
            .Select(d => new { d.NationalId, d.FullName })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var existingDoctors = DemoData.Doctors
            .Where(d => clashing.Any(c => c.NationalId == d.NationalId || c.FullName == d.FullName))
            .Select(d => d.NationalId)
            .ToList();
        var existingPatients = await db.Patients
            .Where(p => patientIds.Contains(p.NationalId))
            .Select(p => p.NationalId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var doctor in DemoData.Doctors.Where(d => !existingDoctors.Contains(d.NationalId)))
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

        foreach (var patient in DemoData.Patients.Where(p => !existingPatients.Contains(p.NationalId)))
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
    }
}
