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
