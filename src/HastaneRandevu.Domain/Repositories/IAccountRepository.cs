using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Domain.Repositories;

/// <summary>Sign-in data of patients and doctors.</summary>
public interface IAccountRepository
{
    Task<AccountCredentials?> FindPatientByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default);

    Task<AccountCredentials?> FindDoctorByNameAsync(string fullName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a failed sign-in atomically. When the count reaches <paramref name="maxAttempts"/> the
    /// account is locked until <paramref name="lockoutEnd"/> and the count starts again from zero.
    /// </summary>
    Task RecordFailedLoginAsync(
        AccountRole role,
        int accountId,
        int maxAttempts,
        DateTime lockoutEnd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the failed attempts and the lockout. When <paramref name="newPasswordHash"/> is given the
    /// stored password is replaced with it and its format becomes <see cref="PasswordFormat.Pbkdf2"/>
    /// in the same statement (upgrade of a plain text or weaker hash).
    /// </summary>
    Task RecordSuccessfulLoginAsync(
        AccountRole role,
        int accountId,
        string? newPasswordHash,
        CancellationToken cancellationToken = default);
}
