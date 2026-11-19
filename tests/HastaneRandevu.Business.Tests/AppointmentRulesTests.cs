using HastaneRandevu.Business.Services;

namespace HastaneRandevu.Business.Tests;

public sealed class AppointmentRulesTests
{
    [Fact]
    public void SlotsOf_WorkingDay_HasHalfHourSlotsFromNineToFive()
    {
        var slots = AppointmentRules.SlotsOf(new DateTime(2026, 3, 2, 13, 45, 0));

        Assert.Equal(16, slots.Count);
        Assert.Equal(new DateTime(2026, 3, 2, 9, 0, 0), slots[0]);
        Assert.Equal(new DateTime(2026, 3, 2, 16, 30, 0), slots[^1]);
        Assert.All(slots, s => Assert.True(AppointmentRules.IsSlotStart(s)));
    }

    [Theory]
    [InlineData(2026, 3, 7)]
    [InlineData(2026, 3, 8)]
    public void SlotsOf_Weekend_IsEmpty(int year, int month, int day)
    {
        Assert.Empty(AppointmentRules.SlotsOf(new DateTime(year, month, day)));
    }

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(16, 30, true)]
    [InlineData(8, 30, false)]
    [InlineData(9, 10, false)]
    [InlineData(17, 0, false)]
    public void IsSlotStart_OnlyAcceptsSlotBoundariesWithinTheDay(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, AppointmentRules.IsSlotStart(new DateTime(2026, 3, 3, hour, minute, 0)));
    }
}
