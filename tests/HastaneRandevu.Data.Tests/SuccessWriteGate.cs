using HastaneRandevu.Data.Repositories;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// A deterministic interleaving point: a sign-in that has verified the password stops right before
/// it records the success, until the test releases it.
/// </summary>
internal sealed class SuccessWriteGate
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when a sign-in waits at the gate.</summary>
    public Task Reached => _reached.Task;

    public void Release() => _released.TrySetResult();

    internal async Task PassAsync(CancellationToken cancellationToken)
    {
        _reached.TrySetResult();
        await _released.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static class SuccessWriteGateExtensions
{
    /// <summary>Replaces the account repository with one that waits at <paramref name="gate"/>.</summary>
    public static IServiceCollection HoldSuccessfulSignInWrites(this IServiceCollection services, SuccessWriteGate gate)
    {
        var registration = services.Single(d => d.ServiceType == typeof(IAccountRepository));
        services.Remove(registration);
        services.AddSingleton<IAccountRepository>(provider =>
            new GatedAccountRepository(ActivatorUtilities.CreateInstance<AccountRepository>(provider), gate));
        return services;
    }

    private sealed class GatedAccountRepository(IAccountRepository inner, SuccessWriteGate gate) : IAccountRepository
    {
        public Task<AccountCredentials?> FindPatientByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default) =>
            inner.FindPatientByNationalIdAsync(nationalId, cancellationToken);

        public Task<AccountCredentials?> FindDoctorByNameAsync(string fullName, CancellationToken cancellationToken = default) =>
            inner.FindDoctorByNameAsync(fullName, cancellationToken);

        public Task RecordFailedLoginAsync(
            AccountRole role,
            int accountId,
            int maxAttempts,
            DateTime lockoutEnd,
            CancellationToken cancellationToken = default) =>
            inner.RecordFailedLoginAsync(role, accountId, maxAttempts, lockoutEnd, cancellationToken);

        public async Task<bool> RecordSuccessfulLoginAsync(
            AccountRole role,
            AccountCredentials verified,
            DateTime now,
            string? newPasswordHash,
            CancellationToken cancellationToken = default)
        {
            await gate.PassAsync(cancellationToken);
            return await inner.RecordSuccessfulLoginAsync(role, verified, now, newPasswordHash, cancellationToken);
        }
    }
}
