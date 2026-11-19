using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Repositories;

internal sealed class AccountRepository(IDbContextFactory<HospitalDbContext> contextFactory) : IAccountRepository
{
    public async Task<AccountCredentials?> FindPatientByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nationalId);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Patients
            .AsNoTracking()
            .Where(p => p.NationalId == nationalId)
            .Select(p => new AccountCredentials(p.Id, p.FullName, p.NationalId, p.PasswordHash, p.PasswordFormat, p.FailedLoginCount, p.LockoutEnd))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AccountCredentials?> FindDoctorByNameAsync(string fullName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Doctors
            .AsNoTracking()
            .Where(d => d.FullName == fullName)
            .Select(d => new AccountCredentials(d.Id, d.FullName, d.NationalId, d.PasswordHash, d.PasswordFormat, d.FailedLoginCount, d.LockoutEnd))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RecordFailedLoginAsync(
        AccountRole role,
        int accountId,
        int maxAttempts,
        DateTime lockoutEnd,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // One UPDATE statement, so concurrent failures cannot lose increments.
        if (role == AccountRole.Patient)
        {
            await db.Patients
                .Where(p => p.Id == accountId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(p => p.LockoutEnd, p => p.FailedLoginCount + 1 >= maxAttempts ? lockoutEnd : p.LockoutEnd)
                        .SetProperty(p => p.FailedLoginCount, p => p.FailedLoginCount + 1 >= maxAttempts ? 0 : p.FailedLoginCount + 1),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await db.Doctors
                .Where(d => d.Id == accountId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(d => d.LockoutEnd, d => d.FailedLoginCount + 1 >= maxAttempts ? lockoutEnd : d.LockoutEnd)
                        .SetProperty(d => d.FailedLoginCount, d => d.FailedLoginCount + 1 >= maxAttempts ? 0 : d.FailedLoginCount + 1),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> RecordSuccessfulLoginAsync(
        AccountRole role,
        AccountCredentials verified,
        DateTime now,
        string? newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verified);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // The WHERE clause is the final decision: a lock set by a concurrent failure, or a password
        // changed since it was verified, makes the statement match no row and the sign-in fails.
        var id = verified.Id;
        var verifiedHash = verified.PasswordHash;
        var verifiedFormat = verified.PasswordFormat;
        var hash = newPasswordHash ?? verifiedHash;
        var format = newPasswordHash is null ? verifiedFormat : PasswordFormat.Pbkdf2;

        var updated = role == AccountRole.Patient
            ? await db.Patients
                .Where(p => p.Id == id
                    && (p.LockoutEnd == null || p.LockoutEnd <= now)
                    && p.PasswordHash == verifiedHash
                    && p.PasswordFormat == verifiedFormat)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(p => p.FailedLoginCount, 0)
                        .SetProperty(p => p.LockoutEnd, (DateTime?)null)
                        .SetProperty(p => p.PasswordHash, hash)
                        .SetProperty(p => p.PasswordFormat, format),
                    cancellationToken)
                .ConfigureAwait(false)
            : await db.Doctors
                .Where(d => d.Id == id
                    && (d.LockoutEnd == null || d.LockoutEnd <= now)
                    && d.PasswordHash == verifiedHash
                    && d.PasswordFormat == verifiedFormat)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(d => d.FailedLoginCount, 0)
                        .SetProperty(d => d.LockoutEnd, (DateTime?)null)
                        .SetProperty(d => d.PasswordHash, hash)
                        .SetProperty(d => d.PasswordFormat, format),
                    cancellationToken)
                .ConfigureAwait(false);

        return updated == 1;
    }
}
