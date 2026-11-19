using HastaneRandevu.Business.Scheduling;

namespace HastaneRandevu.Business.Services;

/// <summary>
/// When appointments can be booked: 30 minute slots, in the future and at most 90 days ahead. A doctor
/// works the hours of their <see cref="DoctorSchedule"/>; without a saved schedule these are weekdays
/// between 09:00 and 17:00, the rule of the original application. Times are local wall clock times, as
/// stored by the original application.
/// </summary>
public static class AppointmentRules
{
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DayStart = TimeSpan.FromHours(9);
    public static readonly TimeSpan DayEnd = TimeSpan.FromHours(17);
    public const int MaxDaysAhead = 90;

    /// <summary>Whether <paramref name="date"/> is a working day of the default schedule.</summary>
    public static bool IsWorkingDay(DateTime date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>All slot start times of <paramref name="date"/> in the default schedule, or none on a weekend.</summary>
    public static IReadOnlyList<DateTime> SlotsOf(DateTime date) => DoctorSchedule.Default.SlotsOf(date);

    /// <summary>Whether <paramref name="start"/> is exactly the start of a slot of the default schedule.</summary>
    public static bool IsSlotStart(DateTime start) => DoctorSchedule.Default.IsSlotStart(start);
}
