using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Domain.Repositories;

public interface IAppointmentRepository
{
    /// <summary>Active appointments of one patient, latest first.</summary>
    Task<IReadOnlyList<AppointmentDetails>> GetActiveForPatientAsync(int patientId, CancellationToken cancellationToken = default);

    /// <summary>Active appointments of one doctor, earliest first.</summary>
    Task<IReadOnlyList<AppointmentDetails>> GetActiveForDoctorAsync(int doctorId, CancellationToken cancellationToken = default);

    /// <summary>All appointments of one patient, cancelled ones included, latest first.</summary>
    Task<IReadOnlyList<AppointmentHistoryEntry>> GetHistoryForPatientAsync(int patientId, CancellationToken cancellationToken = default);

    /// <summary>The doctor's active appointments in [<paramref name="rangeStart"/>, <paramref name="rangeEnd"/>), earliest first.</summary>
    Task<IReadOnlyList<AgendaEntry>> GetAgendaForDoctorAsync(
        int doctorId,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default);

    /// <summary>Start times of the doctor's active appointments in [<paramref name="rangeStart"/>, <paramref name="rangeEnd"/>).</summary>
    Task<IReadOnlyList<DateTime>> GetBookedStartsAsync(
        int doctorId,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default);

    Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Stores a new active appointment and returns its id.</summary>
    /// <exception cref="SlotAlreadyBookedException">The doctor already has an active appointment at that time.</exception>
    Task<int> AddAsync(Appointment appointment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the appointment atomically, only if it is still active, starts after
    /// <paramref name="startsAfter"/> and belongs to <paramref name="ownerId"/> in the given role
    /// (the patient who booked it or the doctor who receives it).
    /// </summary>
    /// <returns><see langword="false"/> when no appointment matched.</returns>
    Task<bool> CancelAsync(
        int id,
        AccountRole ownerRole,
        int ownerId,
        DateTime startsAfter,
        DateTime cancelledAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the appointment to <paramref name="newStartsAt"/> with the same doctor in one statement,
    /// only if it is still active, belongs to <paramref name="patientId"/>, still starts at
    /// <paramref name="currentStartsAt"/> (the time the caller read and checked), starts after
    /// <paramref name="startsAfter"/> and not before <paramref name="startsNoEarlierThan"/>. The old slot
    /// becomes free in the same moment; nothing is cancelled or created.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when no appointment matched, for example because a concurrent move already
    /// changed its time; nothing is changed then.
    /// </returns>
    /// <exception cref="SlotAlreadyBookedException">The doctor already has an active appointment at the new time.</exception>
    Task<bool> RescheduleAsync(
        int id,
        int patientId,
        DateTime currentStartsAt,
        DateTime newStartsAt,
        DateTime startsAfter,
        DateTime startsNoEarlierThan,
        CancellationToken cancellationToken = default);
}

/// <summary>Raised when a doctor's slot is already taken by an active appointment.</summary>
public sealed class SlotAlreadyBookedException : Exception
{
    public SlotAlreadyBookedException()
        : base("The slot is already booked.")
    {
    }

    public SlotAlreadyBookedException(string message)
        : base(message)
    {
    }

    public SlotAlreadyBookedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
