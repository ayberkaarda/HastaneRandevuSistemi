using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Repositories;

internal sealed class PatientRepository(IDbContextFactory<HospitalDbContext> contextFactory) : IPatientRepository
{
    public async Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nationalId);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Patients.AnyAsync(p => p.NationalId == nationalId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Patient
        {
            FullName = patient.FullName,
            NationalId = patient.NationalId,
            PasswordHash = patient.PasswordHash,
            PasswordFormat = patient.PasswordFormat,
        };
        db.Patients.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // The unique index rejected a concurrent registration of the same national id. Checked
            // with a new query so the provider specific error code does not matter.
            if (await NationalIdExistsAsync(patient.NationalId, CancellationToken.None).ConfigureAwait(false))
            {
                throw new DuplicateNationalIdException("The national id is already registered.", ex);
            }

            throw;
        }

        return entity.Id;
    }
}
