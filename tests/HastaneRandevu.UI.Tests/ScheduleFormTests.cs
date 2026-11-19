using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using HastaneRandevu.UI.Theming;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>The doctor's schedule screen. The form is created but never shown.</summary>
public sealed class ScheduleFormTests
{
    [Fact]
    public void Load_ShowsHoursLeaveDaysAndConflictsWithoutWriting() => Run(() =>
    {
        var schedules = new ControlledScheduleService();
        schedules.Conflicts.Add(new ScheduleConflict(Sessions.Appointment(5), ScheduleConflictReason.LeaveDay));
        using var form = new CalismaProgramiForm(schedules, Sessions.Doctor) { Prompts = new RecordingPrompts() };

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);

        Assert.True(Find<CheckBox>(form, "checkBoxMonday").Checked);
        Assert.False(Find<CheckBox>(form, "checkBoxTuesday").Checked);
        Assert.False(Find<ComboBox>(form, "comboBoxBaslangicTuesday").Enabled);
        Assert.Equal("09:00", Find<ComboBox>(form, "comboBoxBaslangicMonday").Text);
        Assert.Equal("17:00", Find<ComboBox>(form, "comboBoxBitisMonday").Text);
        Assert.Equal(["03.03.2026 Salı"], Items(Find<ListBox>(form, "listBoxIzinler")));
        Assert.Equal(["03.03.2026 10:00 - Deniz Örnek - İzin günü"], Items(Find<ListBox>(form, "listBoxCakisanlar")));
        Assert.Empty(schedules.Saved);
        Assert.Empty(schedules.AddedLeave);
    });

    [Fact]
    public void Load_DefaultHours_ExplainsTheDefault() => Run(() =>
    {
        var schedules = new ControlledScheduleService
        {
            View = new DoctorScheduleView(
                [.. Enumerable.Range(1, 5).Select(d => new WorkingDay((DayOfWeek)d, TimeSpan.FromHours(9), TimeSpan.FromHours(17)))],
                UsesDefaultHours: true,
                []),
        };
        using var form = new CalismaProgramiForm(schedules, Sessions.Doctor);

        _ = form.LoadAsync();

        Assert.Contains("09:00-17:00", Find<Label>(form, "labelVarsayilan").Text, StringComparison.Ordinal);
        Assert.False(Find<CheckBox>(form, "checkBoxSaturday").Checked);
        Assert.True(Find<CheckBox>(form, "checkBoxFriday").Checked);
    });

    [Fact]
    public void Save_SendsTheCheckedDaysOnceWhilePending() => Run(() =>
    {
        var schedules = new ControlledScheduleService();
        var prompts = new RecordingPrompts();
        using var form = new CalismaProgramiForm(schedules, Sessions.Doctor) { Prompts = prompts };
        _ = form.LoadAsync();
        Find<CheckBox>(form, "checkBoxSaturday").Checked = true;
        Find<ComboBox>(form, "comboBoxBaslangicSaturday").SelectedIndex = 20; // 10:00
        Find<ComboBox>(form, "comboBoxBitisSaturday").SelectedIndex = 47;     // 24:00

        var first = form.SaveAsync();
        var second = form.SaveAsync();

        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "buttonKaydet").Enabled);
        var saved = Assert.Single(schedules.Saved);
        Assert.Equal(
            [
                new WorkingDay(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
                new WorkingDay(DayOfWeek.Saturday, TimeSpan.FromHours(10), TimeSpan.FromDays(1)),
            ],
            saved);

        schedules.Conflicts.Add(new ScheduleConflict(Sessions.Appointment(7), ScheduleConflictReason.OutsideWorkingHours));
        schedules.CompleteWrite();

        Assert.True(first.IsCompletedSuccessfully && first.Result);
        Assert.True(Find<Button>(form, "buttonKaydet").Enabled);
        Assert.Contains("1 randevu", Assert.Single(prompts.Messages), StringComparison.Ordinal);
        Assert.Equal(["03.03.2026 10:00 - Deniz Örnek - Çalışma saatleri dışında"], Items(Find<ListBox>(form, "listBoxCakisanlar")));
    });

    [Fact]
    public void LeaveChanges_AreExclusiveAcrossAddAndRemove() => Run(() =>
    {
        var schedules = new ControlledScheduleService();
        using var form = new CalismaProgramiForm(schedules, Sessions.Doctor) { Prompts = new RecordingPrompts() };
        _ = form.LoadAsync();
        Find<ListBox>(form, "listBoxIzinler").SelectedIndex = 0;

        var add = form.AddLeaveAsync();
        var remove = form.RemoveLeaveAsync();

        Assert.Single(schedules.AddedLeave);
        Assert.Empty(schedules.RemovedLeave);
        Assert.True(remove.IsCompletedSuccessfully && !remove.Result);
        Assert.False(Find<Button>(form, "buttonIzinEkle").Enabled);
        Assert.False(Find<Button>(form, "buttonIzinKaldir").Enabled);

        schedules.CompleteWrite();

        Assert.True(add.IsCompletedSuccessfully && add.Result);
        Assert.True(Find<Button>(form, "buttonIzinKaldir").Enabled);
    });

    [Fact]
    public void PendingRemoval_BlocksAddingLeave() => Run(() =>
    {
        var schedules = new ControlledScheduleService();
        using var form = new CalismaProgramiForm(schedules, Sessions.Doctor) { Prompts = new RecordingPrompts() };
        _ = form.LoadAsync();
        Find<ListBox>(form, "listBoxIzinler").SelectedIndex = 0;

        var remove = form.RemoveLeaveAsync();
        var add = form.AddLeaveAsync();

        Assert.Equal([new DateOnly(2026, 3, 3)], schedules.RemovedLeave);
        Assert.Empty(schedules.AddedLeave);
        Assert.True(add.IsCompletedSuccessfully && !add.Result);
        Assert.False(Find<Button>(form, "buttonIzinEkle").Enabled);
        Assert.False(remove.IsCompleted);
    });

    [Fact]
    public void RemoveLeave_WithoutASelection_AsksForOne() => Run(() =>
    {
        var schedules = new ControlledScheduleService();
        var prompts = new RecordingPrompts();
        using var form = new CalismaProgramiForm(schedules, Sessions.Doctor) { Prompts = prompts };

        Assert.True(form.RemoveLeaveAsync().IsCompletedSuccessfully);

        Assert.Empty(schedules.RemovedLeave);
        Assert.Equal(["Lütfen kaldırmak istediğiniz izin gününü seçin."], prompts.Messages);
    });

    [Fact]
    public void Layout_KeepsEveryControlInsideTheWindow() => Run(() =>
    {
        LayoutAssert.FitsInsideOnEveryScreen(() =>
        {
            var form = new CalismaProgramiForm(new ControlledScheduleService(), Sessions.Doctor);
            _ = form.LoadAsync();
            return form;
        });
    });

    private static readonly int[] ConflictHours = [10, 14];

    [Fact]
    public void ScheduleConflicts_LongPatientNameKeepsDateAndTimeVisible() => Run(() =>
    {
        var previous = Theme.Current;
        var previousScale = Theme.RenderScale;
        var previousArea = AppDialog.WorkingAreaOf;
        Theme.Use(previous, 2F);
        AppDialog.WorkingAreaOf = _ => new Rectangle(0, 0, 1024, 600);
        try
        {
            var schedules = new ControlledScheduleService();
            var name = new string('W', 80);
            foreach (var hour in ConflictHours)
            {
                var appointment = Sessions.Appointment(hour) with { PatientName = name, StartsAt = new DateTime(2026, 3, 3, hour, hour == 14 ? 30 : 0, 0) };
                schedules.Conflicts.Add(new ScheduleConflict(appointment, ScheduleConflictReason.LeaveDay));
            }

            using var form = new CalismaProgramiForm(schedules, Sessions.Doctor) { Prompts = new RecordingPrompts() };
            _ = form.Handle;
            Assert.True(form.LoadAsync().IsCompletedSuccessfully);

            var list = Find<ThemedListBox>(form, "listBoxCakisanlar");
            var rows = list.Items.Cast<object>().Select(item => list.Describe!(item).Primary).ToList();
            Assert.Equal(2, rows.Count);

            // What the row can paint: the list width minus the padding on both sides, as in the list's own drawing.
            var available = list.ClientSize.Width - Theme.Px(list, (Spacing.M + 2) + Spacing.M);
            string[] times = ["03 Mart 2026 Salı, 10:00", "03 Mart 2026 Salı, 14:30"];
            foreach (var (row, time) in rows.Zip(times))
            {
                var stamp = row[..row.IndexOf(" · ", StringComparison.Ordinal)];
                Assert.Equal(time.TrimStart('0'), stamp);
                var width = TextRenderer.MeasureText(stamp, Theme.Fonts.BodyStrong, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
                Assert.True(width <= available, $"'{stamp}' needs {width}px, the row has {available}px.");
            }
        }
        finally
        {
            AppDialog.WorkingAreaOf = previousArea;
            Theme.Use(previous, previousScale);
        }
    });

    private static string[] Items(ListBox list) => [.. list.Items.Cast<object>().Select(i => i.ToString()!)];
}
