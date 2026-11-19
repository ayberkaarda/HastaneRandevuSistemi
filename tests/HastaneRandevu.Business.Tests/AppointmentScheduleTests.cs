using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>Free slots and bookings follow the doctor's own hours and leave days.</summary>
public sealed class AppointmentScheduleTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0); // Monday
    private static readonly DateTime Saturday = new(2026, 3, 7);
    private static readonly DateTime Tuesday = new(2026, 3, 3);

    private readonly InMemoryScheduleRepository _schedules = new();
    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly Mock<IDoctorRepository> _doctors = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));
    private readonly UserSession _patient = new(AccountRole.Patient, 1, "Hasta", "***");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Slots_ComeFromTheDoctorsHours()
    {
        _schedules.Hours[10] = [new WorkingDay(DayOfWeek.Saturday, TimeSpan.FromHours(8), TimeSpan.FromHours(10))];
        _appointments.Setup(a => a.GetBookedStartsAsync(10, Saturday, Saturday.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Saturday.AddHours(8.5)]);

        var saturday = await Service().GetAvailableSlotsAsync(_patient, 10, Saturday, Ct);
        var tuesday = await Service().GetAvailableSlotsAsync(_patient, 10, Tuesday, Ct); // not a working day any more

        Assert.Equal([Saturday.AddHours(8), Saturday.AddHours(9), Saturday.AddHours(9.5)], saturday);
        Assert.Empty(tuesday);
    }

    [Fact]
    public async Task LeaveDay_HasNoSlotsAndCannotBeBooked()
    {
        _schedules.Leave.Add((10, DateOnly.FromDateTime(Tuesday)));
        _doctors.Setup(d => d.ExistsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var slots = await Service().GetAvailableSlotsAsync(_patient, 10, Tuesday, Ct);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().BookAsync(_patient, 10, Tuesday.AddHours(10), Ct));

        Assert.Empty(slots);
        Assert.Equal(ValidationMessages.InvalidSlot, refused.Message);
        _appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Booking_FollowsCustomHoursOnBothEnds()
    {
        _schedules.Hours[10] = [new WorkingDay(DayOfWeek.Saturday, TimeSpan.FromHours(8), TimeSpan.FromHours(10))];
        _doctors.Setup(d => d.ExistsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appointments.Setup(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);

        Assert.Equal(5, await Service().BookAsync(_patient, 10, Saturday.AddHours(8), Ct));
        Assert.Equal(5, await Service().BookAsync(_patient, 10, Saturday.AddHours(9.5), Ct));
        var afterEnd = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().BookAsync(_patient, 10, Saturday.AddHours(10), Ct));
        var oldHours = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().BookAsync(_patient, 10, Tuesday.AddHours(10), Ct));

        Assert.Equal(ValidationMessages.InvalidSlot, afterEnd.Message);
        Assert.Equal(ValidationMessages.InvalidSlot, oldHours.Message);
    }

    [Fact]
    public async Task LeaveOfAnotherDoctor_DoesNotMatter()
    {
        _schedules.Leave.Add((11, DateOnly.FromDateTime(Tuesday)));
        _appointments.Setup(a => a.GetBookedStartsAsync(10, Tuesday, Tuesday.AddDays(1), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        Assert.Equal(16, (await Service().GetAvailableSlotsAsync(_patient, 10, Tuesday, Ct)).Count);
    }

    private AppointmentService Service() =>
        ServiceFactory.Appointments(_appointments.Object, _doctors.Object, _time, schedules: _schedules);
}
