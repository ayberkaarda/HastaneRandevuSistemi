namespace HastaneRandevu.Domain.Entities;

/// <summary>A doctor who receives appointments (table: Doktorlar).</summary>
public sealed class Doctor
{
    public int Id { get; set; }

    /// <summary>Full name; doctors sign in with it, so it is unique.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Medical branch, for example "Kardiyoloji".</summary>
    public string? Branch { get; set; }

    /// <summary>Turkish national identity number (T.C. Kimlik No). Personal data: never log it.</summary>
    public string NationalId { get; set; } = string.Empty;

    /// <inheritdoc cref="Patient.PasswordHash"/>
    public string PasswordHash { get; set; } = string.Empty;

    /// <inheritdoc cref="Patient.PasswordFormat"/>
    public PasswordFormat PasswordFormat { get; set; }

    /// <inheritdoc cref="Patient.FailedLoginCount"/>
    public int FailedLoginCount { get; set; }

    /// <inheritdoc cref="Patient.LockoutEnd"/>
    public DateTime? LockoutEnd { get; set; }
}
