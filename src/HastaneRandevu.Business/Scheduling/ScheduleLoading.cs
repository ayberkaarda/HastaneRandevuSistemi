using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Scheduling;

internal static class ScheduleLoading
{
    /// <summary>
    /// The doctor's schedule with the leave days in [<paramref name="firstDay"/>, <paramref name="lastDay"/>];
    /// leave days outside that range are not loaded, so the schedule only answers for those days.
    /// </summary>
    public static async Task<DoctorSchedule> LoadScheduleAsync(
        this IDoctorScheduleRepository schedules,
        int doctorId,
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken)
    {
        var days = await schedules.GetWorkingDaysAsync(doctorId, cancellationToken).ConfigureAwait(false);
        var leave = lastDay < firstDay
            ? []
            : await schedules.GetLeaveDaysAsync(doctorId, firstDay, lastDay, cancellationToken).ConfigureAwait(false);
        return new DoctorSchedule(days, leave);
    }
}
