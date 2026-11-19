using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>Doctor search, first free slot and calendar on the patient panel. No window is shown.</summary>
public sealed class SearchAndCalendarTests
{
    private static readonly DoctorSummary Ada = new(1, "Dr. Ada Örnek", "Kardiyoloji");
    private static readonly DoctorSummary Can = new(2, "Dr. Can Deneme", "Dahiliye");
    private static readonly DateTime Slot = DateTime.Today.AddDays(3).AddHours(11);

    [Fact]
    public void Search_AnswersOutOfOrder_ShowOnlyTheLatest() => Run(() =>
    {
        var availability = new ControlledAvailabilityService { HoldSearches = true };
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), availability: availability);
        var search = Find<TextBox>(form, "textBoxDoktorAra");
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");

        search.Text = "ada";
        search.Text = "can";
        availability.Searches[1].Answer.SetResult([Can]);
        availability.Searches[0].Answer.SetResult([Ada]);

        Assert.Equal([("ada", (string?)null), ("can", null)], availability.Searches.Select(s => (s.Name, s.Branch)));
        Assert.Equal([Can], doctors.Items.Cast<DoctorSummary>());
    });

    [Fact]
    public void Branch_IsPassedToTheSearchExceptForAllBranches() => Run(() =>
    {
        var availability = new ControlledAvailabilityService();
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), availability: availability);
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        var branches = Find<ComboBox>(form, "comboBoxBrans");

        branches.SelectedItem = "Kardiyoloji";
        branches.SelectedIndex = 0;

        Assert.Equal(["Tüm branşlar", "Dahiliye", "Kardiyoloji"], branches.Items.Cast<string>());
        Assert.Equal([null, "Kardiyoloji", null], availability.Searches.Select(s => s.Branch));
    });

    /// <summary>Refreshing the doctor list is data binding, not a user choice: it never starts a slot load.</summary>
    [Fact]
    public void SearchResults_KeepOrClearTheSelectionWithoutLoadingSlots() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        var availability = new ControlledAvailabilityService();
        using var form = TestForms.PatientPanel(appointments, availability: availability);
        _ = form.LoadAsync();
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        var slots = Find<ComboBox>(form, "comboBoxSaat");
        Assert.Null(doctors.SelectedItem);
        Assert.Empty(appointments.SlotRequests);

        doctors.SelectedIndex = 0; // the user picks Dr. Ada
        appointments.SlotRequests[0].Answer.SetResult([Slot]);
        Assert.Single(slots.Items);

        availability.Doctors.Reverse();
        _ = form.SearchDoctorsAsync();
        Assert.Equal(1, ((DoctorSummary)doctors.SelectedItem!).Id);
        Assert.Single(appointments.SlotRequests);

        availability.Doctors.RemoveAt(1); // Dr. Ada leaves the list
        _ = form.SearchDoctorsAsync();
        Assert.Null(doctors.SelectedItem);
        Assert.Empty(slots.Items);
        Assert.Single(appointments.SlotRequests);
    });

    [Fact]
    public void FirstFree_ShowsTheDayAndSelectsTheSlotWithOneLoad() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        var availability = new ControlledAvailabilityService();
        using var form = TestForms.PatientPanel(appointments, availability: availability);
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        doctors.Items.Add(Ada);
        doctors.SelectedIndex = 0;
        appointments.SlotRequests[0].Answer.SetResult([]);

        _ = form.FindFirstFreeAsync();
        availability.FirstFree[0].Answer.SetResult(Slot);

        Assert.Equal(Slot.Date, Find<DateTimePicker>(form, "dateTimePickerRandevu").Value.Date);
        Assert.Equal(2, appointments.SlotRequests.Count); // the date change itself loaded nothing
        appointments.SlotRequests[1].Answer.SetResult([Slot.AddMinutes(-30), Slot]);
        Assert.Equal("11:00", Find<ComboBox>(form, "comboBoxSaat").SelectedItem?.ToString());
        Assert.Contains("İlk boş randevu:", Find<Label>(form, "labelIlkBos").Text, StringComparison.Ordinal);
    });

    [Fact]
    public void FirstFree_NoneOrStale_IsHandled() => Run(() =>
    {
        var prompts = new RecordingPrompts();
        var availability = new ControlledAvailabilityService();
        using var form = TestForms.PatientPanel(new ControlledAppointmentService(), prompts, availability: availability);

        _ = form.FindFirstFreeAsync();
        Assert.Equal(["Lütfen bir doktor seçiniz."], prompts.Messages);

        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        doctors.Items.Add(Ada);
        doctors.SelectedIndex = 0;
        _ = form.FindFirstFreeAsync();
        _ = form.FindFirstFreeAsync();
        availability.FirstFree[1].Answer.SetResult(null);
        availability.FirstFree[0].Answer.SetResult(Slot); // stale, ignored

        Assert.Equal("90 gün içinde boş randevu bulunamadı.", Find<Label>(form, "labelIlkBos").Text);
        Assert.Equal(DateTime.Today, Find<DateTimePicker>(form, "dateTimePickerRandevu").Value.Date);
    });

    [Fact]
    public void Calendar_MonthAndWeek_RequestFromMondayAndShowEachDay() => Run(() =>
    {
        var availability = new ControlledAvailabilityService();
        using var form = new TakvimForm(availability, Sessions.Patient, Ada);

        _ = form.ShowPeriodAsync();
        _ = form.SetMonthViewAsync(month: false);

        var month = availability.Calendars[0];
        var week = availability.Calendars[1];
        Assert.Equal(2, availability.Calendars.Count);
        Assert.Equal((1, 42, DayOfWeek.Monday), (month.DoctorId, month.Days, month.FirstDay.DayOfWeek));
        Assert.True(month.FirstDay <= new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1));
        Assert.Equal((7, DayOfWeek.Monday), (week.Days, week.FirstDay.DayOfWeek));

        var first = week.FirstDay;
        week.Answer.SetResult(
        [
            new CalendarDay(first, CalendarDayStatus.OutsideWindow, 0, 0),
            new CalendarDay(first.AddDays(1), CalendarDayStatus.Available, 5, 16),
            new CalendarDay(first.AddDays(2), CalendarDayStatus.Full, 0, 16),
            new CalendarDay(first.AddDays(3), CalendarDayStatus.Leave, 0, 0),
            new CalendarDay(first.AddDays(4), CalendarDayStatus.Available, 1, 16),
            new CalendarDay(first.AddDays(5), CalendarDayStatus.NotWorking, 0, 0),
            new CalendarDay(first.AddDays(6), CalendarDayStatus.NotWorking, 0, 0),
        ]);
        month.Answer.SetResult([]); // stale answer of the month view is ignored

        var buttons = Enumerable.Range(0, 7).Select(i => Find<Button>(form, "buttonGun" + i)).ToList();
        Assert.Equal([false, true, false, false, true, false, false], buttons.Select(b => b.Enabled));
        Assert.EndsWith("Boş: 5/16", buttons[1].Text, StringComparison.Ordinal);
        Assert.EndsWith("Dolu", buttons[2].Text, StringComparison.Ordinal);
        Assert.EndsWith("İzinli", buttons[3].Text, StringComparison.Ordinal);
        Assert.False(Find<Button>(form, "buttonGun7").Visible);

        form.ChooseDayAt(2); // full: nothing happens
        Assert.Null(form.SelectedDay);
        form.ChooseDayAt(1);
        Assert.Equal(first.AddDays(1), form.SelectedDay);
        Assert.Equal(DialogResult.OK, form.DialogResult);
    });

    [Fact]
    public void Calendar_Navigation_MovesByMonthOrWeek() => Run(() =>
    {
        var availability = new ControlledAvailabilityService();
        using var form = new TakvimForm(availability, Sessions.Patient, Ada);
        var today = DateOnly.FromDateTime(DateTime.Today);

        _ = form.MoveAsync(1);
        Assert.Equal(today.AddMonths(1), form.PeriodDay);
        _ = form.SetMonthViewAsync(month: false);
        _ = form.MoveAsync(-1);

        Assert.Equal(today.AddMonths(1).AddDays(-7), form.PeriodDay);
        Assert.Equal(3, availability.Calendars.Count);
    });

    [Fact]
    public void Calendar_Layout_FitsTheWindow() => Run(() =>
    {
        using var form = new TakvimForm(new ControlledAvailabilityService(), Sessions.Patient, Ada);

        LayoutAssert.FitsInside(form);
    });
}
