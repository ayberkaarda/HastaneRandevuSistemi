using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>
/// The focus is moved only after the guarded controls are enabled again; a disabled control cannot
/// take it. Forms get window handles but are never shown.
/// </summary>
public sealed class FocusTests
{
    [Fact]
    public void FocusAfterUnlock_SelectsTheTargetOnlyOnceItIsEnabledAgain() => Run(() =>
    {
        using var form = new Form();
        using var button = new Button();
        using var field = new TextBox();
        form.Controls.AddRange([button, field]);
        CreateHandles(form);
        var operation = new ExclusiveOperation();
        var pending = new TaskCompletionSource();

        var run = operation.RunAsync(
            async () =>
            {
                await pending.Task;
                Assert.False(field.Enabled);
                operation.FocusAfterUnlock(field);
                Assert.NotSame(field, form.ActiveControl);
            },
            button,
            field);
        pending.SetResult();

        Assert.True(run.IsCompletedSuccessfully);
        Assert.True(field.Enabled);
        Assert.Same(field, form.ActiveControl);
    });

    [Fact]
    public void FocusAfterUnlock_TargetOfAnEarlierRun_IsNotReused() => Run(() =>
    {
        using var form = new Form();
        using var first = new TextBox();
        using var second = new TextBox();
        form.Controls.AddRange([first, second]);
        CreateHandles(form);
        var operation = new ExclusiveOperation();

        _ = operation.RunAsync(() =>
        {
            operation.FocusAfterUnlock(first);
            return Task.CompletedTask;
        });
        second.Select();
        _ = operation.RunAsync(() => Task.CompletedTask);

        Assert.Same(second, form.ActiveControl);
    });

    [Fact]
    public void RefusedPatientSignIn_SelectsThePasswordAfterUnlock() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new LoginForm(auth, new NoFormsFactory());
        CreateHandles(form);
        var password = Find<TextBox>(form, "textBoxSifre");
        Find<TextBox>(form, "textBoxTC").Text = "10000000450";
        password.Text = "yanlis";
        Find<Button>(form, "buttonGiris").Select();

        var signIn = form.SignInAsync();
        auth.RefuseSignIn();

        Assert.True(signIn.IsCompletedSuccessfully);
        Assert.Null(signIn.Result);
        Assert.True(Find<Button>(form, "buttonGiris").Enabled);
        Assert.Empty(password.Text);
        Assert.Same(password, form.ActiveControl);
    });

    [Fact]
    public void RefusedDoctorSignIn_SelectsThePasswordAfterUnlock() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new DoktorGirisForm(auth, new NoFormsFactory());
        CreateHandles(form);
        var password = Find<TextBox>(form, "textBoxSifre");
        Find<TextBox>(form, "textBoxDoktorIsim").Text = "Dr. Ada Örnek";
        password.Text = "yanlis";

        var signIn = form.SignInAsync();
        auth.RefuseSignIn();

        Assert.True(signIn.IsCompletedSuccessfully);
        Assert.Same(password, form.ActiveControl);
    });

    [Fact]
    public void SuccessfulRegistration_SelectsTheNameAfterUnlock() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new UyeOlForm(auth) { Prompts = new RecordingPrompts() };
        CreateHandles(form);
        var name = Find<TextBox>(form, "textBoxIsim");
        name.Text = "Yeni Hasta";
        Find<TextBox>(form, "textBoxTC").Text = "10000000696";
        Find<TextBox>(form, "textBoxSifre").Text = "Gizli.Sifre1";
        Find<Button>(form, "buttonKaydet").Select();

        var save = form.SaveAsync();
        Assert.False(Find<Button>(form, "buttonKaydet").Enabled);
        auth.CompleteRegister();

        Assert.True(save.IsCompletedSuccessfully && save.Result);
        Assert.True(Find<Button>(form, "buttonKaydet").Enabled);
        Assert.Same(name, form.ActiveControl);
    });

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }
}
