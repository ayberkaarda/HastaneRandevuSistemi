namespace HastaneRandevu.Domain.Entities;

/// <summary>
/// How an account's stored password value has to be checked (column SifreBicimi). It is recorded
/// explicitly instead of being read from the value, because the original application let users choose
/// any text as a password, including text that looks like a hash.
/// </summary>
public enum PasswordFormat
{
    /// <summary>A PBKDF2 hash written by this application. Also the default for new accounts.</summary>
    Pbkdf2 = 0,

    /// <summary>
    /// The plain text password kept from a database of the original application. Replaced, together
    /// with the format, by a hash on the account's next successful sign-in.
    /// </summary>
    LegacyPlainText = 1,
}
