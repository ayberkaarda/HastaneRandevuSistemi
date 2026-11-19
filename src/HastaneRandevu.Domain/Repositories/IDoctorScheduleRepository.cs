using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Domain.Repositories;

/// <summary>Weekly working hours and leave days of doctors.</summary>
public interface IDoctorScheduleRepository
{
    /// <summary>The doctor's weekly hours ordered by day; empty when the doctor uses the default hours.</summary>
    Task<IReadOnlyList<WorkingDay>> GetWorkingDaysAsync(int doctorId, CancellationToken cancellationToken = default);

    /// <summary>The doctor's leave days in [<paramref name="firstDay"/>, <paramref name="lastDay"/>], ordered.</summary>
    Task<IReadOnlyList<DateOnly>> GetLeaveDaysAsync(int doctorId, DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all weekly hours of the doctor with <paramref name="days"/> in one transaction, serialized
    /// with every other replacement for the same doctor (also when the doctor has no rows yet).
    /// </summary>
    Task ReplaceWorkingDaysAsync(int doctorId, IReadOnlyList<WorkingDay> days, CancellationToken cancellationToken = default);

    /// <summary>Adds a leave day; returns <see langword="false"/> when the doctor already has that day off.</summary>
    Task<bool> AddLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>Removes a leave day; returns <see langword="false"/> when the doctor had no leave that day.</summary>
    Task<bool> RemoveLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default);
}
