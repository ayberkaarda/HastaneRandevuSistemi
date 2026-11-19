namespace HastaneRandevu.Business.Security;

/// <summary>Brute force protection: an account is locked for a while after repeated failures.</summary>
public static class LoginPolicy
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}

/// <summary>Rules for passwords chosen at registration.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 64;
}
