using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Scheduling;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>Limits of moving an appointment: started, cut-off, same slot and the booking rules.</summary>
public sealed class RescheduleValidatorTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0); // Monday
    private static readonly DateTime Free = new(2026, 3, 4, 11, 0, 0);  // Wednesday
    private static readonly TimeSpan TwoHours = TimeSpan.FromHours(2);

    private static string? FirstError(RescheduleInput input) =>
        new RescheduleInputValidator().Validate(input).Errors.FirstOrDefault()?.ErrorMessage;

    private static RescheduleInput Input(DateTime current, DateTime next, TimeSpan cutoff, AppointmentStatus status = AppointmentStatus.Active) =>
        new(status, current, next, Now, cutoff, DoctorSchedule.Default);

    [Fact]
    public void Cutoff_ExactlyNHoursBefore_IsAllowedAndOneTickLaterIsNot()
    {
        Assert.Null(FirstError(Input(Now + TwoHours, Free, TwoHours)));
        Assert.Equal(
            "Randevu en geç başlamadan 2 saat öncesine kadar ertelenebilir.",
            FirstError(Input(Now + TwoHours - TimeSpan.FromTicks(1), Free, TwoHours)));
    }

    [Fact]
    public void ZeroCutoff_AllowsUntilTheStartButNotAfter()
    {
        Assert.Null(FirstError(Input(Now.AddTicks(1), Free, TimeSpan.Zero)));
        Assert.Equal(ValidationMessages.StartedCannotReschedule, FirstError(Input(Now, Free, TimeSpan.Zero)));
        Assert.Equal(ValidationMessages.StartedCannotReschedule, FirstError(Input(Now.AddHours(-1), Free, TimeSpan.Zero)));
    }

    [Fact]
    public void StartedAppointment_ReportsStartedBeforeTheCutoff()
    {
        Assert.Equal(ValidationMessages.StartedCannotReschedule, FirstError(Input(Now.AddMinutes(-20), Free, TwoHours)));
    }

    [Fact]
    public void CancelledOrSameSlot_AreRefused()
    {
        var current = Now.AddDays(2).Date.AddHours(10);

        Assert.Equal(ValidationMessages.AlreadyCancelled, FirstError(Input(current, Free, TwoHours, AppointmentStatus.Cancelled)));
        Assert.Equal(ValidationMessages.SameSlot, FirstError(Input(current, current, TwoHours)));
    }

    [Fact]
    public void NewTime_FollowsTheBookingRules()
    {
        var current = Now.AddDays(2).Date.AddHours(10);
        var day90 = Now.Date.AddDays(AppointmentRules.MaxDaysAhead); // Sunday 31 May 2026
        var leave = new DoctorSchedule([], [DateOnly.FromDateTime(Free)]);

        Assert.Equal(ValidationMessages.InvalidSlot, FirstError(Input(current, Free.AddMinutes(15), TwoHours)));
        Assert.Equal(ValidationMessages.InvalidSlot, FirstError(Input(current, new DateTime(2026, 3, 7, 10, 0, 0), TwoHours)));
        Assert.Equal(ValidationMessages.PastSlot, FirstError(Input(current, Now.Date.AddHours(9), TwoHours)));
        Assert.Equal(ValidationMessages.InvalidSlot, FirstError(new RescheduleInput(AppointmentStatus.Active, current, Free, Now, TwoHours, leave)));
        Assert.Null(FirstError(Input(current, day90.AddDays(-2).AddHours(9), TwoHours)));            // Friday, day 88
        Assert.Equal(ValidationMessages.TooFarAhead, FirstError(Input(current, day90.AddDays(1).AddHours(9), TwoHours))); // Monday, day 91
    }
}

/// <summary>The reschedule service: ownership, cut-off, atomic move and audit.</summary>
public sealed class RescheduleServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0);
    private static readonly DateTime Current = new(2026, 3, 3, 10, 0, 0);
    private static readonly DateTime Target = new(2026, 3, 4, 11, 0, 0);

    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly Mock<IDoctorRepository> _doctors = new(MockBehavior.Strict);
    private readonly InMemoryScheduleRepository _schedules = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));
    private readonly List<(AuditEvent, AccountRole)> _audit = [];
    private readonly UserSession _owner = new(AccountRole.Patient, 1, "Hasta", "***");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RescheduleServiceTests() =>
        _appointments.Setup(a => a.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Appointment { Id = 5, PatientId = 1, DoctorId = 10, StartsAt = Current, Status = AppointmentStatus.Active });

    [Fact]
    public async Task Owner_MovesTheAppointmentInOneRepositoryCall()
    {
        _appointments.Setup(a => a.RescheduleAsync(5, 1, Current, Target, Now, Now.AddHours(3), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Service(new AppointmentPolicy { RescheduleCutoffHours = 3 }).RescheduleAsync(_owner, 5, Target, Ct);

        _appointments.Verify(a => a.RescheduleAsync(5, 1, Current, Target, Now, Now.AddHours(3), It.IsAny<CancellationToken>()), Times.Once);
        _appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
        _appointments.Verify(a => a.CancelAsync(It.IsAny<int>(), It.IsAny<AccountRole>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal([(AuditEvent.AppointmentRescheduled, AccountRole.Patient)], _audit);
    }

    [Fact]
    public async Task ForeignMissingOrDoctor_GetTheSameRefusalAndNothingMoves()
    {
        _appointments.Setup(a => a.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync((Appointment?)null);
        var other = new UserSession(AccountRole.Patient, 2, "Başka", "***");
        var doctor = new UserSession(AccountRole.Doctor, 10, "Dr.", "***");

        var foreign = await Assert.ThrowsAsync<AccessDeniedException>(() => Service().RescheduleAsync(other, 5, Target, Ct));
        var missing = await Assert.ThrowsAsync<AccessDeniedException>(() => Service().RescheduleAsync(_owner, 6, Target, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service().RescheduleAsync(doctor, 5, Target, Ct));

        Assert.Equal(foreign.Message, missing.Message);
        Assert.Equal(3, _audit.Count(a => a.Item1 == AuditEvent.AccessDenied));
        _appointments.Verify(
            a => a.RescheduleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TooLate_IsRefusedWithTheConfiguredHours()
    {
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(new AppointmentPolicy { RescheduleCutoffHours = 24 }).RescheduleAsync(_owner, 5, Target, Ct));

        Assert.Equal("Randevu en geç başlamadan 24 saat öncesine kadar ertelenebilir.", refused.Message);
    }

    [Fact]
    public async Task LostRace_IsReportedAsSlotTaken()
    {
        _appointments.Setup(a => a.RescheduleAsync(5, 1, Current, Target, Now, Now.AddHours(2), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SlotAlreadyBookedException());

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().RescheduleAsync(_owner, 5, Target, Ct));

        Assert.Equal(AppointmentService.SlotTaken, refused.Message);
        Assert.Equal([(AuditEvent.AppointmentSlotTaken, AccountRole.Patient)], _audit);
    }

    [Fact]
    public async Task ChangedMeanwhile_IsRefusedWithoutAudit()
    {
        _appointments.Setup(a => a.RescheduleAsync(5, 1, Current, Target, Now, Now.AddHours(2), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().RescheduleAsync(_owner, 5, Target, Ct));

        Assert.Equal(AppointmentService.RescheduleFailed, refused.Message);
        Assert.Empty(_audit);
    }

    [Fact]
    public async Task NewTime_UsesTheSchedulesOfTheAppointmentsDoctor()
    {
        _schedules.Leave.Add((10, DateOnly.FromDateTime(Target)));

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().RescheduleAsync(_owner, 5, Target, Ct));

        Assert.Equal(ValidationMessages.InvalidSlot, refused.Message);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(168, true)]
    [InlineData(169, false)]
    public void Policy_CutoffBetweenZeroAndAWeek(int hours, bool valid)
    {
        var policy = new AppointmentPolicy { RescheduleCutoffHours = hours };

        Assert.Equal(valid, policy.Validate().Count == 0);
        if (!valid)
        {
            Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBusinessServices(policy));
        }
    }

    private AppointmentService Service(AppointmentPolicy? policy = null) =>
        ServiceFactory.Appointments(_appointments.Object, _doctors.Object, _time, new Recorder(_audit), _schedules, policy);

    private sealed class Recorder(List<(AuditEvent, AccountRole)> events) : IAuditLog
    {
        public void Record(AuditEvent auditEvent, AccountRole role) => events.Add((auditEvent, role));
    }
}
