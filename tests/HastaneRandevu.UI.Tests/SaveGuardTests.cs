using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>Every write button is exclusive: a second click while the first call is pending is ignored.</summary>
public sealed class SaveGuardTests
{
    [Fact]
    public void PatientSignIn_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new LoginForm(auth, new NoFormsFactory());
        Find<TextBox>(form, "textBoxTC").Text = "10000000450";
        Find<TextBox>(form, "textBoxSifre").Text = "Demo.Hasta1";

        var first = form.SignInAsync();
        var second = form.SignInAsync();

        Assert.Equal(1, auth.PatientSignInCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully);
        Assert.Null(second.Result);
        Assert.False(Find<Button>(form, "buttonGiris").Enabled);
        Assert.False(Find<Button>(form, "buttonDoktorGiris").Enabled);
    });

    [Fact]
    public void DoctorSignIn_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new DoktorGirisForm(auth, new NoFormsFactory());
        Find<TextBox>(form, "textBoxDoktorIsim").Text = "Dr. Ada Örnek";
        Find<TextBox>(form, "textBoxSifre").Text = "Demo.Doktor1";

        var first = form.SignInAsync();
        var second = form.SignInAsync();

        Assert.Equal(1, auth.DoctorSignInCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully);
        Assert.False(Find<Button>(form, "buttonGiris").Enabled);
    });

    [Fact]
    public void Register_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new UyeOlForm(auth) { Prompts = new RecordingPrompts() };
        Find<TextBox>(form, "textBoxIsim").Text = "Yeni Hasta";
        Find<TextBox>(form, "textBoxTC").Text = "10000000696";
        Find<TextBox>(form, "textBoxSifre").Text = "Gizli.Sifre1";

        var first = form.SaveAsync();
        var second = form.SaveAsync();

        Assert.Equal(1, auth.RegisterCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonKaydet").Enabled);
    });

    [Fact]
    public void Book_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = new MainForm(appointments, Sessions.Patient) { Prompts = new RecordingPrompts() };
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        doctors.Items.Add(appointments.Doctors[0]);
        doctors.SelectedIndex = 0;
        var slots = Find<ComboBox>(form, "comboBoxSaat");
        slots.Items.Add(new SlotItem(new DateTime(2026, 3, 3, 10, 0, 0)));
        slots.SelectedIndex = 0;

        var first = form.BookAsync();
        var second = form.BookAsync();

        Assert.Equal(1, appointments.BookCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonRandevuAl").Enabled);
    });

    [Fact]
    public void PatientCancel_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        var prompts = new RecordingPrompts();
        using var form = new MainForm(appointments, Sessions.Patient) { Prompts = prompts };
        var list = Find<ListBox>(form, "listBoxRandevuGecmisi");
        list.Items.Add(new PatientAppointmentItem(Sessions.Appointment(5)));
        list.SelectedIndex = 0;

        var first = form.CancelAsync();
        var second = form.CancelAsync();

        Assert.Equal(1, appointments.CancelCalls);
        Assert.Single(prompts.Messages); // one confirmation only
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonRandevuIptal").Enabled);
    });

    [Fact]
    public void DoctorCancel_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = new DoktorForm(appointments, new NoFormsFactory(), Sessions.Doctor) { Prompts = new RecordingPrompts() };
        var list = Find<ListBox>(form, "listBoxRandevular");
        list.Items.Add(new DoctorAppointmentItem(Sessions.Appointment(5)));
        list.SelectedIndex = 0;

        var first = form.CancelAsync();
        var second = form.CancelAsync();

        Assert.Equal(1, appointments.CancelCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonRandevuIptal").Enabled);
    });
}
