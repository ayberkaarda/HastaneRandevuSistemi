using FluentValidation;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Scheduling;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Services;

/// <summary>
/// Booking, listing and cancelling appointments. Every call takes the signed-in
/// <see cref="UserSession"/>: patients only see and cancel their own appointments, doctors only the
/// appointments made with them, and only patients can book.
/// </summary>
public interface IAppointmentService
{
    Task<IReadOnlyList<DoctorSummary>> GetDoctorsAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>Free future slots of the doctor on <paramref name="day"/>, from the doctor's schedule.</summary>
    Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(
        UserSession session,
        int doctorId,
        DateTime day,
        CancellationToken cancellationToken = default);

    /// <summary>Active appointments of the signed-in patient or doctor.</summary>
    Task<IReadOnlyList<AppointmentDetails>> GetMyAppointmentsAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>Books a slot for the signed-in patient and returns the new appointment id.</summary>
    Task<int> BookAsync(UserSession session, int doctorId, DateTime startsAt, CancellationToken cancellationToken = default);

    /// <summary>Cancels an appointment of the signed-in patient or doctor.</summary>
    Task CancelAsync(UserSession session, int appointmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an appointment of the signed-in patient to a free slot of the same doctor in one atomic
    /// step: no cancellation and no new appointment. The appointment must not have started and must be
    /// at least <see cref="AppointmentPolicy.RescheduleCutoffHours"/> away; the new time follows the
    /// booking rules.
    /// </summary>
    Task RescheduleAsync(UserSession session, int appointmentId, DateTime newStartsAt, CancellationToken cancellationToken = default);
}

internal sealed class AppointmentService(
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    IDoctorScheduleRepository schedules,
    TimeProvider timeProvider,
    IValidator<BookingInput> bookingValidator,
    IValidator<CancellationInput> cancellationValidator,
    IValidator<RescheduleInput> rescheduleValidator,
    AppointmentPolicy policy,
    IAuditLog audit) : IAppointmentService
{
    internal const string SlotTaken = "Bu randevu saatinde doktor zaten dolu. Lütfen başka bir zaman seçin.";
    internal const string CancelFailed = "Randevu iptal edilemedi.";
    internal const string RescheduleFailed =
        "Randevu ertelenemedi; randevu bu arada değişmiş olabilir. Lütfen randevu listesini yenileyip tekrar deneyin.";

    public Task<IReadOnlyList<DoctorSummary>> GetDoctorsAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return doctors.GetAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(
        UserSession session,
        int doctorId,
        DateTime day,
        CancellationToken cancellationToken = default)
    {
        RequirePatient(session);

        day = day.Date;
        var now = LocalNow();
        if (day > now.Date.AddDays(AppointmentRules.MaxDaysAhead))
        {
            return [];
        }

        var date = DateOnly.FromDateTime(day);
        var schedule = await schedules.LoadScheduleAsync(doctorId, date, date, cancellationToken).ConfigureAwait(false);
        var slots = schedule.SlotsOf(day);
        if (slots.Count == 0)
        {
            return [];
        }

        var booked = await appointments.GetBookedStartsAsync(doctorId, day, day.AddDays(1), cancellationToken)
            .ConfigureAwait(false);
        var taken = booked.ToHashSet();
        return slots.Where(s => s > now && !taken.Contains(s)).ToList();
    }

    public Task<IReadOnlyList<AppointmentDetails>> GetMyAppointmentsAsync(
        UserSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.Role switch
        {
            AccountRole.Patient => appointments.GetActiveForPatientAsync(session.AccountId, cancellationToken),
            AccountRole.Doctor => appointments.GetActiveForDoctorAsync(session.AccountId, cancellationToken),
            _ => throw new AccessDeniedException(),
        };
    }

    public async Task<int> BookAsync(
        UserSession session,
        int doctorId,
        DateTime startsAt,
        CancellationToken cancellationToken = default)
    {
        RequirePatient(session);
        var schedule = await LoadScheduleForAsync(doctorId, startsAt, cancellationToken).ConfigureAwait(false);
        bookingValidator.ValidateOrThrow(new BookingInput(doctorId, startsAt, LocalNow(), schedule));

        if (!await doctors.ExistsAsync(doctorId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(ValidationMessages.DoctorRequired);
        }

        var appointment = new Appointment
        {
            PatientId = session.AccountId,
            DoctorId = doctorId,
            StartsAt = startsAt,
            Status = AppointmentStatus.Active,
        };

        int id;
        try
        {
            id = await appointments.AddAsync(appointment, cancellationToken).ConfigureAwait(false);
        }
        catch (SlotAlreadyBookedException ex)
        {
            audit.Record(AuditEvent.AppointmentSlotTaken, session.Role);
            throw new BusinessRuleException(SlotTaken, ex);
        }

        audit.Record(AuditEvent.AppointmentBooked, session.Role);
        return id;
    }

    public async Task CancelAsync(UserSession session, int appointmentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken).ConfigureAwait(false);

        // A missing appointment and somebody else's appointment get the same answer, so ids cannot be probed.
        if (appointment is null || !IsOwnedBy(appointment, session))
        {
            audit.Record(AuditEvent.AccessDenied, session.Role);
            throw new AccessDeniedException();
        }

        var now = LocalNow();
        cancellationValidator.ValidateOrThrow(new CancellationInput(appointment.Status, appointment.StartsAt, now));

        // The repository repeats the ownership, status and time conditions in the same statement, so a
        // concurrent change between the read above and this update cannot slip through.
        var cancelled = await appointments.CancelAsync(
            appointmentId,
            session.Role,
            session.AccountId,
            now,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken).ConfigureAwait(false);
        if (!cancelled)
        {
            throw new BusinessRuleException(CancelFailed);
        }

        audit.Record(AuditEvent.AppointmentCancelled, session.Role);
    }

    public async Task RescheduleAsync(
        UserSession session,
        int appointmentId,
        DateTime newStartsAt,
        CancellationToken cancellationToken = default)
    {
        RequirePatient(session);

        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken).ConfigureAwait(false);

        // A missing appointment and somebody else's appointment get the same answer, so ids cannot be probed.
        if (appointment is null || !IsOwnedBy(appointment, session))
        {
            audit.Record(AuditEvent.AccessDenied, session.Role);
            throw new AccessDeniedException();
        }

        var now = LocalNow();
        var schedule = await LoadScheduleForAsync(appointment.DoctorId, newStartsAt, cancellationToken).ConfigureAwait(false);
        rescheduleValidator.ValidateOrThrow(new RescheduleInput(
            appointment.Status,
            appointment.StartsAt,
            newStartsAt,
            now,
            policy.RescheduleCutoff,
            schedule));

        bool moved;
        try
        {
            // The repository repeats ownership, status, the time read above and both time limits in the
            // moving statement, so a concurrent move of the same appointment is not overwritten.
            moved = await appointments.RescheduleAsync(
                appointmentId,
                session.AccountId,
                appointment.StartsAt,
                newStartsAt,
                now,
                now + policy.RescheduleCutoff,
                cancellationToken).ConfigureAwait(false);
        }
        catch (SlotAlreadyBookedException ex)
        {
            audit.Record(AuditEvent.AppointmentSlotTaken, session.Role);
            throw new BusinessRuleException(SlotTaken, ex);
        }

        if (!moved)
        {
            throw new BusinessRuleException(RescheduleFailed);
        }

        audit.Record(AuditEvent.AppointmentRescheduled, session.Role);
    }

    private static bool IsOwnedBy(Appointment appointment, UserSession session) => session.Role switch
    {
        AccountRole.Patient => appointment.PatientId == session.AccountId,
        AccountRole.Doctor => appointment.DoctorId == session.AccountId,
        _ => false,
    };

    private void RequirePatient(UserSession session) => Access.RequireRole(session, AccountRole.Patient, audit);

    /// <summary>The doctor's schedule for the day of <paramref name="start"/>; the default one for no doctor.</summary>
    private Task<DoctorSchedule> LoadScheduleForAsync(int doctorId, DateTime start, CancellationToken cancellationToken)
    {
        if (doctorId <= 0)
        {
            return Task.FromResult(DoctorSchedule.Default);
        }

        var day = DateOnly.FromDateTime(start);
        return schedules.LoadScheduleAsync(doctorId, day, day, cancellationToken);
    }

    private DateTime LocalNow() => timeProvider.GetLocalNow().DateTime;
}
