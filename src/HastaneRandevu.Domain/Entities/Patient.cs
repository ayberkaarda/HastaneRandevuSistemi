namespace HastaneRandevu.Domain.Entities;

/// <summary>A patient who books appointments (table: Hastalar).</summary>
public sealed class Patient
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Turkish national identity number (T.C. Kimlik No). Personal data: never log it.</summary>
    public string NationalId { get; set; } = string.Empty;

    /// <summary>
    /// PBKDF2 hash of the password. Rows created by the original application hold the plain text
    /// password until the first successful sign-in replaces it with a hash.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Failed sign-in attempts since the last success or lockout.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>UTC time until which sign-in is refused, or <see langword="null"/>.</summary>
    public DateTime? LockoutEnd { get; set; }
}
