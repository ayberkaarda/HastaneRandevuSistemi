using HastaneRandevu.Business;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>The reschedule dialog. It is created but never shown.</summary>
public sealed class RescheduleFormTests
{
    private static readonly DateTime Ten = DateTime.Today.AddDays(2).AddHours(10);

    private static AppointmentDetails Appointment() =>
        new(5, 1, "Deniz Örnek", 2, "Dr. Can Deneme", "Dahiliye", DateTime.Today.AddDays(1).AddHours(9));

    [Fact]
    public void Slots_AreLoadedForTheAppointmentsDoctorAndOnlyTheLatestAnswerIsShown() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = new ErteleForm(appointments, Sessions.Patient, Appointment());
        var picker = Find<DateTimePicker>(form, "dateTimePickerYeni");
        var slots = Find<ComboBox>(form, "comboBoxYeniSaat");

        _ = form.LoadSlotsAsync();
        picker.Value = Ten.Date; // a user choice starts a newer request

        Assert.Equal([2, 2], appointments.SlotRequests.Select(r => r.DoctorId));
        appointments.SlotRequests[^1].Answer.SetResult([Ten]);
        appointments.SlotRequests[0].Answer.SetResult([Ten.AddHours(3)]);

        Assert.Equal(["10:00"], slots.Items.Cast<object>().Select(i => i.ToString()));
        Assert.Null(slots.SelectedItem); // loading never chooses a slot for the user
    });

    [Fact]
    public void Reschedule_WithoutASlot_AsksForOne() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        var prompts = new RecordingPrompts();
        using var form = new ErteleForm(appointments, Sessions.Patient, Appointment()) { Prompts = prompts };

        Assert.True(form.RescheduleAsync().IsCompletedSuccessfully);

        Assert.Empty(appointments.Reschedules);
        Assert.Equal(["Lütfen yeni bir randevu saati seçiniz."], prompts.Messages);
    });

    [Fact]
    public void Reschedule_WhilePending_IsNotSentTwiceAndClosesWithOk() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        var prompts = new RecordingPrompts();
        using var form = new ErteleForm(appointments, Sessions.Patient, Appointment()) { Prompts = prompts };
        var slots = Find<ComboBox>(form, "comboBoxYeniSaat");
        slots.Items.Add(new SlotItem(Ten));
        slots.SelectedIndex = 0;

        var first = form.RescheduleAsync();
        var second = form.RescheduleAsync();

        Assert.Equal([(5, Ten)], appointments.Reschedules);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonErtele").Enabled);
        Assert.False(slots.Enabled);

        appointments.PendingReschedule.SetResult();

        Assert.True(first.IsCompletedSuccessfully && first.Result);
        Assert.Equal(DialogResult.OK, form.DialogResult);
        Assert.Equal([$"Randevunuz {DisplayFormat.DateTime(Ten)} saatine ertelendi."], prompts.Messages);
    });

    [Fact]
    public void Reschedule_Refused_ShowsTheReasonKeepsTheDialogAndReloadsTheSlots() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = new ErteleForm(appointments, Sessions.Patient, Appointment()) { Prompts = new RecordingPrompts() };
        var slots = Find<ComboBox>(form, "comboBoxYeniSaat");
        slots.Items.Add(new SlotItem(Ten));
        slots.SelectedIndex = 0;
        var shownBefore = NoDialogs.Requested.Count;

        var run = form.RescheduleAsync();
        appointments.PendingReschedule.SetException(new BusinessRuleException("Bu randevu saatinde doktor zaten dolu. Lütfen başka bir zaman seçin."));

        Assert.True(run.IsCompletedSuccessfully);
        Assert.NotEqual(DialogResult.OK, form.DialogResult);
        Assert.Single(appointments.SlotRequests);
        Assert.Contains("zaten dolu", NoDialogs.Requested.Skip(shownBefore).Single(), StringComparison.Ordinal);
        Assert.True(Find<Button>(form, "buttonErtele").Enabled);
    });

    [Fact]
    public void PatientPanel_RescheduleWithoutASelection_AsksForOne() => Run(() =>
    {
        var prompts = new RecordingPrompts();
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), prompts);

        Assert.True(form.RescheduleSelectedAsync().IsCompletedSuccessfully);

        Assert.Equal(["Lütfen ertelemek istediğiniz randevuyu seçin."], prompts.Messages);
    });

    [Fact]
    public void Layout_KeepsEveryControlInsideTheWindow() => Run(() =>
    {
        using var form = new ErteleForm(new ControlledAppointmentService(), Sessions.Patient, Appointment());

        LayoutAssert.FitsInside(form);
    });
}
