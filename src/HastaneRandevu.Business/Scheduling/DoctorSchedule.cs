using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Scheduling;

/// <summary>
/// When a doctor can be booked: weekly working hours split into slots of
/// <see cref="AppointmentRules.SlotLength"/>, minus leave days. A doctor who never saved a schedule
/// works the default hours (weekdays 09:00-17:00), which is the rule of the original application.
/// Times are local wall clock times.
/// </summary>
public sealed class DoctorSchedule
{
    private readonly Dictionary<DayOfWeek, WorkingDay> _days;
    private readonly HashSet<DateOnly> _leaveDays;

    /// <summary>Monday to Friday, <see cref="AppointmentRules.DayStart"/> to <see cref="AppointmentRules.DayEnd"/>.</summary>
    public static IReadOnlyList<WorkingDay> DefaultWorkingDays { get; } =
    [
        new(DayOfWeek.Monday, AppointmentRules.DayStart, AppointmentRules.DayEnd),
        new(DayOfWeek.Tuesday, AppointmentRules.DayStart, AppointmentRules.DayEnd),
        new(DayOfWeek.Wednesday, AppointmentRules.DayStart, AppointmentRules.DayEnd),
        new(DayOfWeek.Thursday, AppointmentRules.DayStart, AppointmentRules.DayEnd),
        new(DayOfWeek.Friday, AppointmentRules.DayStart, AppointmentRules.DayEnd),
    ];

    /// <summary>The default hours without leave days.</summary>
    public static DoctorSchedule Default { get; } = new([], []);

    /// <param name="workingDays">The saved weekly hours; none means the default hours.</param>
    /// <param name="leaveDays">Days without slots.</param>
    public DoctorSchedule(IEnumerable<WorkingDay> workingDays, IEnumerable<DateOnly> leaveDays)
    {
        ArgumentNullException.ThrowIfNull(workingDays);
        ArgumentNullException.ThrowIfNull(leaveDays);

        var saved = workingDays.ToList();
        UsesDefaultHours = saved.Count == 0;
        _days = (UsesDefaultHours ? DefaultWorkingDays : saved).ToDictionary(d => d.Day);
        _leaveDays = [.. leaveDays];
        WorkingDays = [.. _days.Values.OrderBy(d => ((int)d.Day + 6) % 7)]; // Monday first
    }

    /// <summary>Whether the doctor never saved own hours and works <see cref="DefaultWorkingDays"/>.</summary>
    public bool UsesDefaultHours { get; }

    /// <summary>The weekly hours in effect, Monday first.</summary>
    public IReadOnlyList<WorkingDay> WorkingDays { get; }

    public bool IsLeaveDay(DateOnly day) => _leaveDays.Contains(day);

    /// <summary>The hours on <paramref name="day"/>, or <see langword="null"/> when the doctor does not work that weekday.</summary>
    public WorkingDay? HoursOn(DayOfWeek day) => _days.GetValueOrDefault(day);

    /// <summary>Whether the doctor works on <paramref name="date"/>: a working weekday that is not a leave day.</summary>
    public bool IsWorkingDate(DateTime date) =>
        HoursOn(date.DayOfWeek) is not null && !IsLeaveDay(DateOnly.FromDateTime(date));

    /// <summary>All slot start times of <paramref name="date"/>; none on a day off or a leave day.</summary>
    public IReadOnlyList<DateTime> SlotsOf(DateTime date)
    {
        var day = date.Date;
        if (!IsWorkingDate(day))
        {
            return [];
        }

        var hours = HoursOn(day.DayOfWeek)!;
        var slots = new List<DateTime>();
        for (var time = hours.Start; time + AppointmentRules.SlotLength <= hours.End; time += AppointmentRules.SlotLength)
        {
            slots.Add(day + time);
        }

        return slots;
    }

    /// <summary>Whether <paramref name="start"/> is exactly the start of one of the doctor's slots.</summary>
    public bool IsSlotStart(DateTime start)
    {
        if (!IsWorkingDate(start))
        {
            return false;
        }

        var hours = HoursOn(start.DayOfWeek)!;
        var time = start.TimeOfDay;
        return time >= hours.Start
            && time + AppointmentRules.SlotLength <= hours.End
            && (time - hours.Start).Ticks % AppointmentRules.SlotLength.Ticks == 0;
    }
}
