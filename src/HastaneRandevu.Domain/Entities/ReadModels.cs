namespace HastaneRandevu.Domain.Entities;

/// <summary>An appointment joined with the names of its patient and doctor, for listings.</summary>
public sealed record AppointmentDetails(
    int Id,
    int PatientId,
    string PatientName,
    int DoctorId,
    string DoctorName,
    string? Branch,
    DateTime StartsAt);

/// <summary>A doctor as offered to patients when booking.</summary>
public sealed record DoctorSummary(int Id, string FullName, string? Branch)
{
    /// <summary>Text shown in the doctor picker, for example "Dr. Ada Örnek - Kardiyoloji".</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Branch) ? FullName : $"{FullName} - {Branch}";

    public override string ToString() => DisplayName;
}

/// <summary>An appointment of a patient's history, cancelled ones included.</summary>
public sealed record AppointmentHistoryEntry(
    int Id,
    string DoctorName,
    string? Branch,
    DateTime StartsAt,
    AppointmentStatus Status,
    DateTime? CancelledAt);

/// <summary>An active appointment on a doctor's agenda. The national id is personal data: mask it before showing it.</summary>
public sealed record AgendaEntry(int Id, DateTime StartsAt, string PatientName, string PatientNationalId);

/// <summary>The working hours of a doctor on one day of the week, as local wall clock times.</summary>
public sealed record WorkingDay(DayOfWeek Day, TimeSpan Start, TimeSpan End);

/// <summary>Who can sign in.</summary>
public enum AccountRole
{
    Patient = 0,
    Doctor = 1,
}

/// <summary>What a sign-in needs to know about an account.</summary>
public sealed record AccountCredentials(
    int Id,
    string FullName,
    string NationalId,
    string PasswordHash,
    PasswordFormat PasswordFormat,
    int FailedLoginCount,
    DateTime? LockoutEnd);
