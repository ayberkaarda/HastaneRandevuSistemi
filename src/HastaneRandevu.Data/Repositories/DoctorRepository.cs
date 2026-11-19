using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Repositories;

internal sealed class DoctorRepository(IDbContextFactory<HospitalDbContext> contextFactory) : IDoctorRepository
{
    public async Task<IReadOnlyList<DoctorSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Doctors
            .AsNoTracking()
            .OrderBy(d => d.Id)
            .Select(d => new DoctorSummary(d.Id, d.FullName, d.Branch))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Doctors.AnyAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);
    }
}
