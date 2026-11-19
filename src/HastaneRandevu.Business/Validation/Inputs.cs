using HastaneRandevu.Business.Scheduling;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Validation;

/// <summary>
/// Data entered on the registration screen. The name and the national id are trimmed on creation; the
/// password is kept exactly as typed.
/// </summary>
internal sealed record RegistrationInput(string FullName, string NationalId, string? Password)
{
    public static RegistrationInput Create(string? fullName, string? nationalId, string? password) =>
        new(fullName?.Trim() ?? string.Empty, nationalId?.Trim() ?? string.Empty, password);
}

/// <summary>A patient sign-in attempt. The national id is trimmed; the password is kept as typed.</summary>
internal sealed record PatientSignInInput(string NationalId, string? Password)
{
    public static PatientSignInInput Create(string? nationalId, string? password) =>
        new(nationalId?.Trim() ?? string.Empty, password);
}

/// <summary>A doctor sign-in attempt. The name is trimmed; the password is kept as typed.</summary>
internal sealed record DoctorSignInInput(string FullName, string? Password)
{
    public static DoctorSignInInput Create(string? fullName, string? password) =>
        new(fullName?.Trim() ?? string.Empty, password);
}

/// <summary>
/// A booking request checked against the local wall clock time <paramref name="Now"/> and the doctor's
/// <paramref name="Schedule"/> (working hours and leave days).
/// </summary>
internal sealed record BookingInput(int DoctorId, DateTime StartsAt, DateTime Now, DoctorSchedule Schedule);

/// <summary>A doctor's new weekly working hours.</summary>
internal sealed record WeeklyHoursInput(IReadOnlyList<WorkingDay> Days);

/// <summary>A leave day to add, checked against the local date <paramref name="Today"/>.</summary>
internal sealed record LeaveDayInput(DateOnly Day, DateOnly Today);

/// <summary>
/// A cancellation of an appointment the caller owns, checked against the local wall clock time
/// <paramref name="Now"/>. Ownership is decided before this input is built.
/// </summary>
internal sealed record CancellationInput(AppointmentStatus Status, DateTime StartsAt, DateTime Now);

/// <summary>
/// Moving an appointment the caller owns from <paramref name="CurrentStartsAt"/> to
/// <paramref name="NewStartsAt"/>, checked against the local time <paramref name="Now"/>, the latest
/// moment before the start at which a move is allowed (<paramref name="Cutoff"/>) and the doctor's
/// <paramref name="Schedule"/>. Ownership is decided before this input is built.
/// </summary>
internal sealed record RescheduleInput(
    AppointmentStatus Status,
    DateTime CurrentStartsAt,
    DateTime NewStartsAt,
    DateTime Now,
    TimeSpan Cutoff,
    DoctorSchedule Schedule);
