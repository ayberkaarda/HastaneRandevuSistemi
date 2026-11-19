using HastaneRandevu.Business.Scheduling;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Tests;

/// <summary>Slots generated from a doctor's weekly hours and leave days.</summary>
public sealed class DoctorScheduleTests
{
    private static readonly DateTime Monday = new(2026, 3, 2);
    private static readonly DateTime Saturday = new(2026, 3, 7);

    /// <summary>
    /// A doctor without a saved schedule must keep exactly the rule of the original application:
    /// weekdays, 09:00 to 17:00, 30 minute slots. Checked for every quarter hour of two weeks.
    /// </summary>
    [Fact]
    public void Default_IsTheOriginalWeekdayRule()
    {
        for (var day = Monday; day < Monday.AddDays(14); day = day.AddDays(1))
        {
            var weekday = day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            Assert.Equal(weekday ? 16 : 0, DoctorSchedule.Default.SlotsOf(day).Count);
            for (var time = day; time < day.AddDays(1); time = time.AddMinutes(15))
            {
                var original = weekday
                    && time.TimeOfDay >= TimeSpan.FromHours(9)
                    && time.TimeOfDay <= TimeSpan.FromHours(16.5)
                    && time.Minute % 30 == 0;
                Assert.Equal(original, DoctorSchedule.Default.IsSlotStart(time));
            }
        }

        Assert.True(DoctorSchedule.Default.UsesDefaultHours);
        Assert.Equal(DoctorSchedule.DefaultWorkingDays, new DoctorSchedule([], []).WorkingDays);
    }

    [Fact]
    public void CustomHours_ReplaceTheDefaultCompletely()
    {
        var schedule = new DoctorSchedule([new WorkingDay(DayOfWeek.Saturday, TimeSpan.FromHours(10), TimeSpan.FromHours(12))], []);

        Assert.False(schedule.UsesDefaultHours);
        Assert.Equal([Saturday.AddHours(10), Saturday.AddHours(10.5), Saturday.AddHours(11), Saturday.AddHours(11.5)], schedule.SlotsOf(Saturday.AddHours(15)));
        Assert.Empty(schedule.SlotsOf(Monday));
        Assert.False(schedule.IsSlotStart(Monday.AddHours(10)));
        Assert.True(schedule.IsSlotStart(Saturday.AddHours(11.5)));
        Assert.False(schedule.IsSlotStart(Saturday.AddHours(12))); // the end is not a start
        Assert.False(schedule.IsSlotStart(Saturday.AddHours(9.5)));
        Assert.False(schedule.IsSlotStart(Saturday.AddHours(10).AddMinutes(15)));
    }

    [Fact]
    public void WholeDay_FromMidnightToMidnight_HasFortyEightSlots()
    {
        var schedule = new DoctorSchedule([new WorkingDay(DayOfWeek.Monday, TimeSpan.Zero, TimeSpan.FromDays(1))], []);

        var slots = schedule.SlotsOf(Monday);

        Assert.Equal(48, slots.Count);
        Assert.Equal(Monday, slots[0]);
        Assert.Equal(Monday.AddHours(23.5), slots[^1]);
        Assert.True(schedule.IsSlotStart(Monday.AddHours(23.5)));
    }

    [Fact]
    public void LeaveDay_HasNoSlotsAndOnlyThatDayIsAffected()
    {
        var schedule = new DoctorSchedule([], [DateOnly.FromDateTime(Monday)]);

        Assert.True(schedule.IsLeaveDay(DateOnly.FromDateTime(Monday)));
        Assert.Empty(schedule.SlotsOf(Monday));
        Assert.False(schedule.IsSlotStart(Monday.AddHours(10)));
        Assert.False(schedule.IsWorkingDate(Monday));
        Assert.Equal(16, schedule.SlotsOf(Monday.AddDays(1)).Count);
        Assert.Equal(16, schedule.SlotsOf(Monday.AddDays(-7)).Count);
    }

    [Fact]
    public void WorkingDays_AreListedMondayFirst()
    {
        var schedule = new DoctorSchedule(
            [
                new WorkingDay(DayOfWeek.Sunday, TimeSpan.FromHours(9), TimeSpan.FromHours(10)),
                new WorkingDay(DayOfWeek.Wednesday, TimeSpan.FromHours(9), TimeSpan.FromHours(10)),
                new WorkingDay(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(10)),
            ],
            []);

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Sunday], schedule.WorkingDays.Select(d => d.Day));
    }

    [Fact]
    public void AppointmentRules_StillDescribeTheDefaultSchedule()
    {
        Assert.Equal(DoctorSchedule.Default.SlotsOf(Monday), AppointmentRules.SlotsOf(Monday));
        Assert.True(AppointmentRules.IsSlotStart(Monday.AddHours(16.5)));
        Assert.False(AppointmentRules.IsSlotStart(Saturday.AddHours(10)));
    }
}

public sealed class ScheduleValidatorTests
{
    private static readonly DateOnly Today = new(2026, 3, 2);

    private static WorkingDay Day(DayOfWeek day, double start, double end) =>
        new(day, TimeSpan.FromHours(start), TimeSpan.FromHours(end));

    public static TheoryData<WorkingDay, bool> Hours => new()
    {
        { Day(DayOfWeek.Monday, 9, 17), true },
        { Day(DayOfWeek.Monday, 0, 24), true },
        { Day(DayOfWeek.Monday, 9, 9.5), true },
        { Day(DayOfWeek.Monday, 9, 9), false },
        { Day(DayOfWeek.Monday, 10, 9), false },
        { Day(DayOfWeek.Monday, 9.25, 17), false },
        { Day(DayOfWeek.Monday, 9, 16.75), false },
        { Day(DayOfWeek.Monday, 0, 24.5), false },
        { Day(DayOfWeek.Monday, -0.5, 10), false },
        { Day((DayOfWeek)7, 9, 17), false },
    };

    [Theory]
    [MemberData(nameof(Hours))]
    public void WeeklyHours_DayLimits(WorkingDay day, bool valid)
    {
        var result = new WeeklyHoursInputValidator().Validate(new WeeklyHoursInput([day]));

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(ValidationMessages.WorkingHoursInvalid, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Fact]
    public void WeeklyHours_NoDayOrARepeatedDay_AreRefused()
    {
        var validator = new WeeklyHoursInputValidator();

        Assert.Equal(ValidationMessages.WorkingDayRequired, validator.Validate(new WeeklyHoursInput([])).Errors.Single().ErrorMessage);
        Assert.Equal(
            ValidationMessages.WorkingDayRepeated,
            validator.Validate(new WeeklyHoursInput([Day(DayOfWeek.Monday, 9, 12), Day(DayOfWeek.Monday, 13, 17)])).Errors.Single().ErrorMessage);
        Assert.True(validator.Validate(new WeeklyHoursInput([Day(DayOfWeek.Monday, 9, 12), Day(DayOfWeek.Sunday, 13, 17)])).IsValid);
    }

    [Theory]
    [InlineData(-1, ValidationMessages.LeaveDayInPast)]
    [InlineData(0, null)]
    [InlineData(365, null)]
    [InlineData(366, ValidationMessages.LeaveDayTooFarAhead)]
    public void LeaveDay_FromTodayUpTo365DaysAhead(int offset, string? message)
    {
        var result = new LeaveDayInputValidator().Validate(new LeaveDayInput(Today.AddDays(offset), Today));

        Assert.Equal(message is null, result.IsValid);
        Assert.Equal(message, result.Errors.SingleOrDefault()?.ErrorMessage);
    }
}
