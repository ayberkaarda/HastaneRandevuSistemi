namespace HastaneRandevu.Domain.Entities;

/// <summary>An appointment of a patient with a doctor (table: Randevular).</summary>
public sealed class Appointment
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    /// <summary>Local wall clock start time of the slot.</summary>
    public DateTime StartsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Active;

    /// <summary>UTC time of the cancellation, or <see langword="null"/> for an active appointment.</summary>
    public DateTime? CancelledAt { get; set; }
}

/// <summary>
/// Cancelled appointments are kept for the record and no longer occupy their slot.
/// </summary>
public enum AppointmentStatus
{
    Active = 0,
    Cancelled = 1,
}
