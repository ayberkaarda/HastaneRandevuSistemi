using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Services;

/// <summary>
/// The signed-in account. Only <see cref="AuthService"/> creates sessions (the constructor is internal),
/// and every service that reads or changes appointments takes one and checks ownership against it, so
/// the user interface cannot widen access by passing other ids.
/// </summary>
public sealed class UserSession
{
    internal UserSession(AccountRole role, int accountId, string displayName, string maskedNationalId)
    {
        Role = role;
        AccountId = accountId;
        DisplayName = displayName;
        MaskedNationalId = maskedNationalId;
    }

    public AccountRole Role { get; }

    public int AccountId { get; }

    public string DisplayName { get; }

    /// <summary>The national id with most digits hidden; the full number is not kept after sign-in.</summary>
    public string MaskedNationalId { get; }
}
