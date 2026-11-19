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

    /// <summary>Free future slots of the doctor on <paramref name="day"/>.</summary>
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
}

internal sealed class AppointmentService(
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    TimeProvider timeProvider) : IAppointmentService
{
    internal const string DoctorRequired = "Lütfen bir doktor seçiniz.";
    internal const string InvalidSlot = "Lütfen çalışma saatleri içinde geçerli bir randevu saati seçiniz.";
    internal const string PastSlot = "Geçmiş bir tarih veya saat için randevu alınamaz.";
    internal const string TooFarAhead = "Randevu en fazla 90 gün sonrası için alınabilir.";
    internal const string SlotTaken = "Bu randevu saatinde doktor zaten dolu. Lütfen başka bir zaman seçin.";
    internal const string AlreadyCancelled = "Bu randevu zaten iptal edilmiş.";
    internal const string StartedCannotCancel = "Geçmiş veya başlamış bir randevu iptal edilemez.";
    internal const string CancelFailed = "Randevu iptal edilemedi.";

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
        var slots = AppointmentRules.SlotsOf(day);
        var now = LocalNow();
        if (slots.Count == 0 || day > now.Date.AddDays(AppointmentRules.MaxDaysAhead))
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

        if (doctorId <= 0)
        {
            throw new BusinessRuleException(DoctorRequired);
        }

        if (!AppointmentRules.IsSlotStart(startsAt))
        {
            throw new BusinessRuleException(InvalidSlot);
        }

        var now = LocalNow();
        if (startsAt <= now)
        {
            throw new BusinessRuleException(PastSlot);
        }

        if (startsAt.Date > now.Date.AddDays(AppointmentRules.MaxDaysAhead))
        {
            throw new BusinessRuleException(TooFarAhead);
        }

        if (!await doctors.ExistsAsync(doctorId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(DoctorRequired);
        }

        var appointment = new Appointment
        {
            PatientId = session.AccountId,
            DoctorId = doctorId,
            StartsAt = startsAt,
            Status = AppointmentStatus.Active,
        };

        try
        {
            return await appointments.AddAsync(appointment, cancellationToken).ConfigureAwait(false);
        }
        catch (SlotAlreadyBookedException ex)
        {
            throw new BusinessRuleException(SlotTaken, ex);
        }
    }

    public async Task CancelAsync(UserSession session, int appointmentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken).ConfigureAwait(false);

        // A missing appointment and somebody else's appointment get the same answer, so ids cannot be probed.
        if (appointment is null || !IsOwnedBy(appointment, session))
        {
            throw new AccessDeniedException();
        }

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            throw new BusinessRuleException(AlreadyCancelled);
        }

        var now = LocalNow();
        if (appointment.StartsAt <= now)
        {
            throw new BusinessRuleException(StartedCannotCancel);
        }

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
    }

    private static bool IsOwnedBy(Appointment appointment, UserSession session) => session.Role switch
    {
        AccountRole.Patient => appointment.PatientId == session.AccountId,
        AccountRole.Doctor => appointment.DoctorId == session.AccountId,
        _ => false,
    };

    private static void RequirePatient(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Role != AccountRole.Patient)
        {
            throw new AccessDeniedException();
        }
    }

    private DateTime LocalNow() => timeProvider.GetLocalNow().DateTime;
}
