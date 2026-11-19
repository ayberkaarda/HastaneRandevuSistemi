using System.Globalization;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Scheduling;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Services;

/// <summary>What a day of a doctor's calendar offers.</summary>
public enum CalendarDayStatus
{
    /// <summary>Before today or after the booking window of <see cref="AppointmentRules.MaxDaysAhead"/> days.</summary>
    OutsideWindow,

    /// <summary>The doctor does not work on this weekday.</summary>
    NotWorking,

    /// <summary>A leave or holiday day of the doctor.</summary>
    Leave,

    /// <summary>A working day without a free future slot.</summary>
    Full,

    /// <summary>At least one free slot.</summary>
    Available,
}

/// <summary>One day of a doctor's calendar: its status, free slots and all slots of the day.</summary>
public sealed record CalendarDay(DateOnly Date, CalendarDayStatus Status, int FreeSlots, int TotalSlots);

/// <summary>
/// Finding a doctor and a time: search by name and branch, a calendar of free and full days, and the
/// earliest free slot. Free slots are computed here from the schedule and the bookings, never in the UI.
/// </summary>
public interface IAvailabilityService
{
    /// <summary>
    /// Doctors whose name contains <paramref name="name"/> and whose branch is <paramref name="branch"/>
    /// (both ignore case by Turkish rules; empty means any), ordered as listed for booking.
    /// </summary>
    Task<IReadOnlyList<DoctorSummary>> SearchDoctorsAsync(
        UserSession session,
        string? name,
        string? branch,
        CancellationToken cancellationToken = default);

    /// <summary>The distinct branches of all doctors, in Turkish alphabetical order.</summary>
    Task<IReadOnlyList<string>> GetBranchesAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>The doctor's calendar for <paramref name="days"/> days (1-42) from <paramref name="firstDay"/>.</summary>
    Task<IReadOnlyList<CalendarDay>> GetCalendarAsync(
        UserSession session,
        int doctorId,
        DateOnly firstDay,
        int days,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The doctor's earliest free future slot within the booking window (today up to and including day
    /// <see cref="AppointmentRules.MaxDaysAhead"/>), or <see langword="null"/> when there is none.
    /// </summary>
    Task<DateTime?> FindFirstFreeSlotAsync(UserSession session, int doctorId, CancellationToken cancellationToken = default);
}

internal sealed class AvailabilityService(
    IDoctorRepository doctors,
    IAppointmentRepository appointments,
    IDoctorScheduleRepository schedules,
    TimeProvider timeProvider,
    IAuditLog audit) : IAvailabilityService
{
    public const int MaxCalendarDays = 42;
    private const int MaxSearchLength = 100;
    private static readonly CompareInfo Turkish = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public async Task<IReadOnlyList<DoctorSummary>> SearchDoctorsAsync(
        UserSession session,
        string? name,
        string? branch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var query = Normalize(name);
        var wanted = Normalize(branch);
        var all = await doctors.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all
            .Where(d => query.Length == 0 || Turkish.IndexOf(d.FullName, query, CompareOptions.IgnoreCase) >= 0)
            .Where(d => wanted.Length == 0 || Turkish.Compare(d.Branch?.Trim() ?? string.Empty, wanted, CompareOptions.IgnoreCase) == 0)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var all = await doctors.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);
        return all
            .Select(d => d.Branch?.Trim())
            .Where(b => !string.IsNullOrEmpty(b))
            .Select(b => b!)
            .Distinct(comparer)
            .Order(comparer)
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarDay>> GetCalendarAsync(
        UserSession session,
        int doctorId,
        DateOnly firstDay,
        int days,
        CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Patient, audit);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, MaxCalendarDays);

        var lastDay = firstDay.AddDays(days - 1);
        var plan = await PlanAsync(doctorId, firstDay, lastDay, cancellationToken).ConfigureAwait(false);
        return Enumerable.Range(0, days).Select(i => plan.Describe(firstDay.AddDays(i))).ToList();
    }

    public async Task<DateTime?> FindFirstFreeSlotAsync(UserSession session, int doctorId, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Patient, audit);

        var today = DateOnly.FromDateTime(LocalNow());
        var lastDay = today.AddDays(AppointmentRules.MaxDaysAhead);
        var plan = await PlanAsync(doctorId, today, lastDay, cancellationToken).ConfigureAwait(false);
        for (var day = today; day <= lastDay; day = day.AddDays(1))
        {
            foreach (var slot in plan.FreeSlotsOf(day))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>Loads the schedule and the bookings of [<paramref name="firstDay"/>, <paramref name="lastDay"/>] once.</summary>
    private async Task<SlotPlan> PlanAsync(int doctorId, DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken)
    {
        var now = LocalNow();
        var today = DateOnly.FromDateTime(now);
        var windowEnd = today.AddDays(AppointmentRules.MaxDaysAhead);

        // Only the part inside the booking window can offer slots; nothing else is queried.
        var from = firstDay < today ? today : firstDay;
        var to = lastDay > windowEnd ? windowEnd : lastDay;
        if (from > to)
        {
            return new SlotPlan(DoctorSchedule.Default, [], now, today, windowEnd, empty: true);
        }

        var schedule = await schedules.LoadScheduleAsync(doctorId, from, to, cancellationToken).ConfigureAwait(false);
        var booked = await appointments.GetBookedStartsAsync(
            doctorId,
            from.ToDateTime(TimeOnly.MinValue),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue),
            cancellationToken).ConfigureAwait(false);
        return new SlotPlan(schedule, [.. booked], now, today, windowEnd, empty: false);
    }

    private static string Normalize(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > MaxSearchLength ? text[..MaxSearchLength] : text;
    }

    private DateTime LocalNow() => timeProvider.GetLocalNow().DateTime;

    private sealed class SlotPlan(DoctorSchedule schedule, HashSet<DateTime> booked, DateTime now, DateOnly today, DateOnly windowEnd, bool empty)
    {
        public IEnumerable<DateTime> FreeSlotsOf(DateOnly day) =>
            empty || day < today || day > windowEnd
                ? []
                : schedule.SlotsOf(day.ToDateTime(TimeOnly.MinValue)).Where(s => s > now && !booked.Contains(s));

        public CalendarDay Describe(DateOnly day)
        {
            if (day < today || day > windowEnd)
            {
                return new CalendarDay(day, CalendarDayStatus.OutsideWindow, 0, 0);
            }

            var date = day.ToDateTime(TimeOnly.MinValue);
            if (schedule.IsLeaveDay(day))
            {
                return new CalendarDay(day, CalendarDayStatus.Leave, 0, 0);
            }

            var total = schedule.SlotsOf(date).Count;
            if (total == 0)
            {
                return new CalendarDay(day, CalendarDayStatus.NotWorking, 0, 0);
            }

            var free = FreeSlotsOf(day).Count();
            return new CalendarDay(day, free == 0 ? CalendarDayStatus.Full : CalendarDayStatus.Available, free, total);
        }
    }
}
