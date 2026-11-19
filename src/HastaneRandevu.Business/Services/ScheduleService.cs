using FluentValidation;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Scheduling;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Services;

/// <summary>Why an existing appointment no longer fits the doctor's schedule.</summary>
public enum ScheduleConflictReason
{
    /// <summary>The appointment falls on one of the doctor's leave days.</summary>
    LeaveDay,

    /// <summary>The appointment lies outside the doctor's weekly working hours.</summary>
    OutsideWorkingHours,
}

/// <summary>An active future appointment that the current schedule would not offer any more.</summary>
public sealed record ScheduleConflict(AppointmentDetails Appointment, ScheduleConflictReason Reason);

/// <summary>A doctor's schedule as shown on the schedule screen.</summary>
/// <param name="WorkingDays">The weekly hours in effect, Monday first.</param>
/// <param name="UsesDefaultHours">Whether the doctor never saved own hours (weekdays 09:00-17:00).</param>
/// <param name="LeaveDays">Leave days from today on, ordered.</param>
public sealed record DoctorScheduleView(IReadOnlyList<WorkingDay> WorkingDays, bool UsesDefaultHours, IReadOnlyList<DateOnly> LeaveDays);

/// <summary>
/// The signed-in doctor's own working hours and leave days. Every call works on the session's own
/// account; there is no doctor id parameter, so one doctor cannot read or change another doctor's
/// schedule, and patients are refused. Existing appointments are never changed: appointments that no
/// longer fit are returned as conflicts so the doctor can decide what to do with them.
/// </summary>
public interface IScheduleService
{
    Task<DoctorScheduleView> GetMyScheduleAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>Replaces the weekly hours and returns the appointments that no longer fit.</summary>
    Task<IReadOnlyList<ScheduleConflict>> SaveWorkingDaysAsync(
        UserSession session,
        IReadOnlyList<WorkingDay> days,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a leave day and returns the appointments that no longer fit.</summary>
    Task<IReadOnlyList<ScheduleConflict>> AddLeaveDayAsync(UserSession session, DateOnly day, CancellationToken cancellationToken = default);

    Task RemoveLeaveDayAsync(UserSession session, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>Active future appointments that fall on a leave day or outside the working hours.</summary>
    Task<IReadOnlyList<ScheduleConflict>> GetConflictsAsync(UserSession session, CancellationToken cancellationToken = default);
}

internal sealed class ScheduleService(
    IDoctorScheduleRepository schedules,
    IAppointmentRepository appointments,
    TimeProvider timeProvider,
    IValidator<WeeklyHoursInput> weeklyHoursValidator,
    IValidator<LeaveDayInput> leaveDayValidator,
    IAuditLog audit) : IScheduleService
{
    internal const string LeaveDayExists = "Bu gün zaten izin günü olarak kayıtlı.";
    internal const string LeaveDayMissing = "Bu gün için kayıtlı bir izin yok.";

    public async Task<DoctorScheduleView> GetMyScheduleAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Doctor, audit);

        var today = Today();
        var schedule = await schedules.LoadScheduleAsync(
            session.AccountId,
            today,
            today.AddDays(LeaveDayInputValidator.LeaveDayMaxDaysAhead),
            cancellationToken).ConfigureAwait(false);
        var leave = await schedules.GetLeaveDaysAsync(
            session.AccountId,
            today,
            today.AddDays(LeaveDayInputValidator.LeaveDayMaxDaysAhead),
            cancellationToken).ConfigureAwait(false);
        return new DoctorScheduleView(schedule.WorkingDays, schedule.UsesDefaultHours, leave);
    }

    public async Task<IReadOnlyList<ScheduleConflict>> SaveWorkingDaysAsync(
        UserSession session,
        IReadOnlyList<WorkingDay> days,
        CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Doctor, audit);
        weeklyHoursValidator.ValidateOrThrow(new WeeklyHoursInput(days));

        await schedules.ReplaceWorkingDaysAsync(session.AccountId, days, cancellationToken).ConfigureAwait(false);
        audit.Record(AuditEvent.ScheduleChanged, session.Role);
        return await FindConflictsAsync(session.AccountId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScheduleConflict>> AddLeaveDayAsync(
        UserSession session,
        DateOnly day,
        CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Doctor, audit);
        leaveDayValidator.ValidateOrThrow(new LeaveDayInput(day, Today()));

        if (!await schedules.AddLeaveDayAsync(session.AccountId, day, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(LeaveDayExists);
        }

        audit.Record(AuditEvent.ScheduleChanged, session.Role);
        return await FindConflictsAsync(session.AccountId, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveLeaveDayAsync(UserSession session, DateOnly day, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Doctor, audit);

        if (!await schedules.RemoveLeaveDayAsync(session.AccountId, day, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(LeaveDayMissing);
        }

        audit.Record(AuditEvent.ScheduleChanged, session.Role);
    }

    public Task<IReadOnlyList<ScheduleConflict>> GetConflictsAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Doctor, audit);
        return FindConflictsAsync(session.AccountId, cancellationToken);
    }

    private async Task<IReadOnlyList<ScheduleConflict>> FindConflictsAsync(int doctorId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow().DateTime;
        var upcoming = (await appointments.GetActiveForDoctorAsync(doctorId, cancellationToken).ConfigureAwait(false))
            .Where(a => a.StartsAt > now)
            .ToList();
        if (upcoming.Count == 0)
        {
            return [];
        }

        var schedule = await schedules.LoadScheduleAsync(
            doctorId,
            DateOnly.FromDateTime(now),
            DateOnly.FromDateTime(upcoming.Max(a => a.StartsAt)),
            cancellationToken).ConfigureAwait(false);

        var conflicts = new List<ScheduleConflict>();
        foreach (var appointment in upcoming)
        {
            if (schedule.IsLeaveDay(DateOnly.FromDateTime(appointment.StartsAt)))
            {
                conflicts.Add(new ScheduleConflict(appointment, ScheduleConflictReason.LeaveDay));
            }
            else if (!schedule.IsSlotStart(appointment.StartsAt))
            {
                conflicts.Add(new ScheduleConflict(appointment, ScheduleConflictReason.OutsideWorkingHours));
            }
        }

        return conflicts;
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
