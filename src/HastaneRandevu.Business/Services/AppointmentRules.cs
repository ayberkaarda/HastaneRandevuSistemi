namespace HastaneRandevu.Business.Services;

/// <summary>
/// When appointments can be booked: 30 minute slots on weekdays between 09:00 and 17:00, in the
/// future and at most 90 days ahead. Times are local wall clock times, as stored by the original
/// application.
/// </summary>
public static class AppointmentRules
{
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DayStart = TimeSpan.FromHours(9);
    public static readonly TimeSpan DayEnd = TimeSpan.FromHours(17);
    public const int MaxDaysAhead = 90;

    public static bool IsWorkingDay(DateTime date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>All slot start times of <paramref name="date"/>, or none on a weekend.</summary>
    public static IReadOnlyList<DateTime> SlotsOf(DateTime date)
    {
        var day = date.Date;
        if (!IsWorkingDay(day))
        {
            return [];
        }

        var slots = new List<DateTime>();
        for (var time = DayStart; time + SlotLength <= DayEnd; time += SlotLength)
        {
            slots.Add(day + time);
        }

        return slots;
    }

    /// <summary>Whether <paramref name="start"/> is exactly the start of a slot on a working day.</summary>
    public static bool IsSlotStart(DateTime start)
    {
        var time = start.TimeOfDay;
        return IsWorkingDay(start)
            && time >= DayStart
            && time + SlotLength <= DayEnd
            && (time - DayStart).Ticks % SlotLength.Ticks == 0;
    }
}
