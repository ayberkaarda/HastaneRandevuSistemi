using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Domain.Repositories;

public interface IDoctorRepository
{
    Task<IReadOnlyList<DoctorSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
