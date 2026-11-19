using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Tests;

/// <summary>
/// Schedules kept in memory, per doctor. A doctor without saved hours has none, so the services use the
/// default weekday 09:00-17:00 schedule, as for every doctor of an existing database.
/// </summary>
internal sealed class InMemoryScheduleRepository : IDoctorScheduleRepository
{
    public Dictionary<int, List<WorkingDay>> Hours { get; } = [];

    public HashSet<(int DoctorId, DateOnly Day)> Leave { get; } = [];

    public List<int> ReplacedFor { get; } = [];

    public Task<IReadOnlyList<WorkingDay>> GetWorkingDaysAsync(int doctorId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkingDay>>(Hours.TryGetValue(doctorId, out var days) ? [.. days] : []);

    public Task<IReadOnlyList<DateOnly>> GetLeaveDaysAsync(int doctorId, DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DateOnly>>(
            [.. Leave.Where(l => l.DoctorId == doctorId && l.Day >= firstDay && l.Day <= lastDay).Select(l => l.Day).Order()]);

    public Task ReplaceWorkingDaysAsync(int doctorId, IReadOnlyList<WorkingDay> days, CancellationToken cancellationToken = default)
    {
        ReplacedFor.Add(doctorId);
        Hours[doctorId] = [.. days];
        return Task.CompletedTask;
    }

    public Task<bool> AddLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default) =>
        Task.FromResult(Leave.Add((doctorId, day)));

    public Task<bool> RemoveLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default) =>
        Task.FromResult(Leave.Remove((doctorId, day)));
}
