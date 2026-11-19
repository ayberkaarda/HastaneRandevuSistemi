using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

public sealed class MainFormTests
{
    private static readonly DateTime Ten = new(2026, 3, 3, 10, 0, 0);

    [Fact]
    public void Header_ShowsTheNameAndOnlyTheMaskedNationalId() => Run(() =>
    {
        using var form = new MainForm(new ControlledAppointmentService(), Sessions.Patient);

        var header = Find<Label>(form, "labelHastaBilgi").Text;

        Assert.Contains("Deniz Örnek", header, StringComparison.Ordinal);
        Assert.Contains("100******50", header, StringComparison.Ordinal);
        Assert.DoesNotContain("10000000450", header, StringComparison.Ordinal);
    });

    [Fact]
    public void LoadSlots_AnswersOutOfOrder_ShowOnlyTheLatestSelection() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = new MainForm(appointments, Sessions.Patient);
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        var slots = Find<ComboBox>(form, "comboBoxSaat");
        doctors.Items.Add(appointments.Doctors[0]);
        doctors.Items.Add(appointments.Doctors[1]);

        doctors.SelectedIndex = 0; // request for doctor 1
        doctors.SelectedIndex = 1; // request for doctor 2
        Assert.Equal([1, 2], appointments.SlotRequests.Select(r => r.DoctorId));

        // The newer answer arrives first, then the stale one.
        appointments.SlotRequests[1].Answer.SetResult([Ten, Ten.AddMinutes(30)]);
        appointments.SlotRequests[0].Answer.SetResult([Ten.AddHours(5)]);

        Assert.Equal(["10:00", "10:30"], slots.Items.Cast<object>().Select(i => i.ToString()));
    });

    [Fact]
    public void LoadSlots_NewSelection_ClearsTheOldSlotsImmediately() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = new MainForm(appointments, Sessions.Patient);
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        var slots = Find<ComboBox>(form, "comboBoxSaat");
        doctors.Items.Add(appointments.Doctors[0]);
        doctors.Items.Add(appointments.Doctors[1]);
        doctors.SelectedIndex = 0;
        appointments.SlotRequests[0].Answer.SetResult([Ten]);
        Assert.Single(slots.Items.Cast<object>());

        doctors.SelectedIndex = 1;

        Assert.Empty(slots.Items.Cast<object>());
    });

    [Fact]
    public void Book_WithoutASlot_AsksForOneAndCallsNothing() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        var prompts = new RecordingPrompts();
        using var form = new MainForm(appointments, Sessions.Patient) { Prompts = prompts };
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        doctors.Items.Add(appointments.Doctors[0]);
        doctors.SelectedIndex = 0;

        var run = form.BookAsync();

        Assert.True(run.IsCompletedSuccessfully);
        Assert.Equal(0, appointments.BookCalls);
        Assert.Equal(["Lütfen bir randevu saati seçiniz."], prompts.Messages);
    });

    [Fact]
    public void AppointmentItems_ShowDoctorBranchAndTimeOrPatientAndTime()
    {
        var appointment = Sessions.Appointment(5);

        Assert.Equal("Dr. Ada Örnek - Kardiyoloji - 03.03.2026 10:00", new PatientAppointmentItem(appointment).ToString());
        Assert.Equal("Deniz Örnek - 03.03.2026 10:00", new DoctorAppointmentItem(appointment).ToString());
    }
}
