using HastaneRandevu.Business.Security;

namespace HastaneRandevu.Business.Tests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Hash_ProducesSaltedPbkdf2HashWithoutThePassword()
    {
        var first = _hasher.Hash("Gizli.Sifre1");
        var second = _hasher.Hash("Gizli.Sifre1");

        Assert.StartsWith("PBKDF2-SHA256$1000$", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Gizli", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second); // random salt
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", first));
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", second));
    }

    [Theory]
    [InlineData("gizli.sifre1")]
    [InlineData("Gizli.Sifre1 ")]
    [InlineData("")]
    public void Verify_WrongPassword_Fails(string attempt)
    {
        var stored = _hasher.Hash("Gizli.Sifre1");

        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify(attempt, stored));
    }

    [Fact]
    public void Verify_LegacyPlainText_SucceedsOnlyWithRehashRequest()
    {
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify("1234", "1234"));

        // The original application trimmed the password before storing and comparing it.
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify(" 1234 ", "1234"));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("12345", "1234"));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("", ""));
    }

    [Fact]
    public void Verify_FewerIterationsThanConfigured_RequestsRehash()
    {
        var weak = new PasswordHasher(iterations: 1_000).Hash("Gizli.Sifre1");
        var strong = new PasswordHasher(iterations: 2_000);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, strong.Verify("Gizli.Sifre1", weak));
        Assert.Equal(PasswordVerificationResult.Failed, strong.Verify("yanlis", weak));
    }

    [Theory]
    [InlineData("PBKDF2-SHA256$")]
    [InlineData("PBKDF2-SHA256$abc$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$10$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$1000$not-base64$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$1000$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$1000$c2FsdA==$aGFzaA==$extra")]
    public void Verify_MalformedHash_FailsInsteadOfThrowing(string stored)
    {
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("PBKDF2-SHA256", stored));
    }

    [Fact]
    public void Constructor_RejectsTooFewIterations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordHasher(iterations: 999));
        Assert.Equal(600_000, PasswordHasher.DefaultIterations);
    }
}
