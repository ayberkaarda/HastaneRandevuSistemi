using HastaneRandevu.Business.Security;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>
/// Sign-in password fields accept every password an existing account can have, which includes the
/// longer ones the original application stored; only registration applies the new password limit.
/// </summary>
public sealed class SignInFieldTests
{
    private static readonly string LongestLegacyPassword = new('x', PasswordPolicy.SignInMaxLength);

    [Fact]
    public void PatientSignIn_PasswordField_AcceptsTheLongestLegacyPassword() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new LoginForm(auth, new NoFormsFactory());
        var field = Find<TextBox>(form, "textBoxSifre");
        Find<TextBox>(form, "textBoxTC").Text = "10000000450";
        field.Text = LongestLegacyPassword;

        _ = form.SignInAsync();

        Assert.Equal(PasswordPolicy.SignInMaxLength, field.MaxLength);
        Assert.Equal(LongestLegacyPassword, auth.LastSignInPassword);
    });

    [Fact]
    public void DoctorSignIn_PasswordField_AcceptsTheLongestLegacyPassword() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new DoktorGirisForm(auth, new NoFormsFactory());
        var field = Find<TextBox>(form, "textBoxSifre");
        Find<TextBox>(form, "textBoxDoktorIsim").Text = "Dr. Ada Örnek";
        field.Text = LongestLegacyPassword;

        _ = form.SignInAsync();

        Assert.Equal(PasswordPolicy.SignInMaxLength, field.MaxLength);
        Assert.Equal(LongestLegacyPassword, auth.LastSignInPassword);
    });

    [Fact]
    public void Register_PasswordField_KeepsTheLimitForNewPasswords() => Run(() =>
    {
        using var form = new UyeOlForm(new PendingAuthService());

        Assert.Equal(PasswordPolicy.MaxLength, Find<TextBox>(form, "textBoxSifre").MaxLength);
    });
}
