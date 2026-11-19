using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>Reminders after sign-in and the export buttons. No window, icon or file dialog appears.</summary>
public sealed class RemindersAndExportTests
{
    [Fact]
    public void Reminders_AreShownInTheWindowAndAsOneBalloon() => Run(() =>
    {
        var reminders = new ControlledReminderService();
        reminders.Upcoming.Add(Sessions.Appointment(5));
        var notifier = new RecordingNotifier();
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), reminders: reminders, notifier: notifier);

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        _ = form.ShowRemindersAsync(); // a later refresh does not repeat the balloon

        var label = Find<Label>(form, "labelHatirlatma").Text;
        Assert.Contains("Yaklaşan randevunuz:", label, StringComparison.Ordinal);
        Assert.Contains("Dr. Ada Örnek - Kardiyoloji - 03.03.2026 10:00", label, StringComparison.Ordinal);
        var balloon = Assert.Single(notifier.Shown);
        Assert.Equal("Dr. Ada Örnek - Kardiyoloji - 03.03.2026 10:00", balloon.Text);
        Assert.DoesNotContain("100******50", balloon.Text, StringComparison.Ordinal);
    });

    [Fact]
    public void NoUpcomingAppointment_ShowsNothing() => Run(() =>
    {
        var notifier = new RecordingNotifier();
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), notifier: notifier);

        _ = form.LoadAsync();

        Assert.Equal(string.Empty, Find<Label>(form, "labelHatirlatma").Text);
        Assert.Empty(notifier.Shown);
    });

    [Fact]
    public void ReminderText_ListsThreeAndCountsTheRest()
    {
        AppointmentDetails[] four = [Sessions.Appointment(1), Sessions.Appointment(2), Sessions.Appointment(3), Sessions.Appointment(4)];

        var lines = ReminderText.Describe(four).Split(Environment.NewLine);

        Assert.Equal(4, lines.Length);
        Assert.Equal("ve 1 randevu daha", lines[^1]);
    }

    [Fact]
    public void HistoryExport_CancelledDialog_WritesNothing() => Run(() =>
    {
        var exports = new ControlledExportService();
        var prompts = new RecordingPrompts { SavePath = null };
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), prompts, exports: exports);

        Assert.True(form.ExportHistoryAsync().IsCompletedSuccessfully);

        Assert.Empty(exports.HistoryPaths);
        Assert.Matches(@"^randevu-gecmisi-\d{8}\.csv$", Assert.Single(prompts.SaveRequests));
    });

    [Fact]
    public void HistoryExport_WhilePending_IsNotStartedTwice() => Run(() =>
    {
        var exports = new ControlledExportService();
        var prompts = new RecordingPrompts { SavePath = @"C:\temp\gecmis.csv" };
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), prompts, exports: exports);

        var first = form.ExportHistoryAsync();
        var second = form.ExportHistoryAsync();

        Assert.Equal([@"C:\temp\gecmis.csv"], exports.HistoryPaths);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonGecmisCsv").Enabled);

        exports.Pending.SetResult(4);

        Assert.True(first.IsCompletedSuccessfully && first.Result);
        Assert.True(Find<Button>(form, "buttonGecmisCsv").Enabled);
        Assert.Equal("Randevu geçmişiniz (4 randevu) dışa aktarıldı.", prompts.Messages.Single());
    });

    [Fact]
    public void AgendaExport_UsesTheChosenDayAndIsExclusive() => Run(() =>
    {
        var exports = new ControlledExportService();
        var prompts = new RecordingPrompts { SavePath = @"C:\temp\ajanda.html" };
        using var form = TestForms.DoctorPanel(new ControlledAppointmentService(), prompts, exports);
        var day = DateTime.Today.AddDays(5);
        Find<DateTimePicker>(form, "dateTimePickerAjanda").Value = day;

        var first = form.ExportAgendaAsync();
        var second = form.ExportAgendaAsync();

        Assert.Equal([(DateOnly.FromDateTime(day), @"C:\temp\ajanda.html")], exports.AgendaRequests);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonAjanda").Enabled);
        Assert.Equal($"ajanda-{day:yyyyMMdd}.html", Assert.Single(prompts.SaveRequests));
        exports.Pending.SetResult(2);
        Assert.True(first.Result);
        Assert.Contains("(2 randevu)", prompts.Messages.Single(), StringComparison.Ordinal);
    });

    [Fact]
    public void SaveDialog_GoesThroughTheDialogHelperAndOpensNoWindow() => Run(() =>
    {
        using var form = new Form();
        var before = NoDialogs.Requested.Count;

        var path = new MessageBoxPrompts(form).AskSavePath("gecmis.csv", "CSV dosyası (*.csv)|*.csv");

        Assert.Null(path);
        Assert.Equal("Save: gecmis.csv", NoDialogs.Requested.Skip(before).Single());
    });
}
