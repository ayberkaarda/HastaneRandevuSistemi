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

/// <summary>A booking request checked against the local wall clock time <paramref name="Now"/>.</summary>
internal sealed record BookingInput(int DoctorId, DateTime StartsAt, DateTime Now);

/// <summary>
/// A cancellation of an appointment the caller owns, checked against the local wall clock time
/// <paramref name="Now"/>. Ownership is decided before this input is built.
/// </summary>
internal sealed record CancellationInput(AppointmentStatus Status, DateTime StartsAt, DateTime Now);
