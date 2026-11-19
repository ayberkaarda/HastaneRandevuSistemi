using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>Booking rules and ownership checks with strict mocks.</summary>
public sealed class AppointmentServiceTests
{
    // Monday 2 March 2026, 10:10. The fake clock's local time zone is UTC.
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0);
    private static readonly DateTime NextSlot = new(2026, 3, 2, 10, 30, 0);

    private static readonly UserSession PatientA = new(AccountRole.Patient, 1, "Hasta A", "100******46");
    private static readonly UserSession PatientB = new(AccountRole.Patient, 2, "Hasta B", "100******14");
    private static readonly UserSession DoctorX = new(AccountRole.Doctor, 10, "Dr. X", "100******82");
    private static readonly UserSession DoctorY = new(AccountRole.Doctor, 11, "Dr. Y", "100******50");

    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly Mock<IDoctorRepository> _doctors = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Book_FreeFutureSlot_StoresAnActiveAppointmentForTheSignedInPatient()
    {
        Appointment? stored = null;
        _doctors.Setup(d => d.ExistsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appointments.Setup(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Callback<Appointment, CancellationToken>((appointment, _) => stored = appointment)
            .ReturnsAsync(99);

        var id = await Service().BookAsync(PatientA, 10, NextSlot, Ct);

        Assert.Equal(99, id);
        Assert.NotNull(stored);
        Assert.Equal(PatientA.AccountId, stored.PatientId);
        Assert.Equal(10, stored.DoctorId);
        Assert.Equal(NextSlot, stored.StartsAt);
        Assert.Equal(AppointmentStatus.Active, stored.Status);
    }

    [Theory]
    [InlineData(2026, 3, 2, 10, 0, "past")] // already started today
    [InlineData(2026, 2, 27, 11, 0, "past")] // earlier day
    [InlineData(2026, 3, 2, 10, 15, "slot")] // not on a slot boundary
    [InlineData(2026, 3, 2, 8, 30, "slot")] // before opening hours
    [InlineData(2026, 3, 2, 17, 0, "slot")] // the day ends at 17:00
    [InlineData(2026, 3, 7, 10, 0, "slot")] // Saturday
    [InlineData(2026, 6, 1, 10, 0, "far")] // 91 days ahead
    public async Task Book_InvalidTime_IsRejectedWithoutTouchingTheRepositories(
        int year, int month, int day, int hour, int minute, string rule)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().BookAsync(PatientA, 10, new DateTime(year, month, day, hour, minute, 0), Ct));

        var expected = rule switch
        {
            "past" => ValidationMessages.PastSlot,
            "slot" => ValidationMessages.InvalidSlot,
            _ => ValidationMessages.TooFarAhead,
        };
        Assert.Equal(expected, error.Message);
        _appointments.VerifyNoOtherCalls();
        _doctors.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Book_SecondsWithinASlot_AreRejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service().BookAsync(PatientA, 10, NextSlot.AddSeconds(5), Ct));
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Book_LastDayOfTheWindow_IsAccepted()
    {
        _doctors.Setup(d => d.ExistsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appointments.Setup(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // 29 May 2026 is a Friday, 88 days ahead; 31 May (day 90) is a Sunday.
        await Service().BookAsync(PatientA, 10, new DateTime(2026, 5, 29, 16, 30, 0), Ct);
    }

    [Fact]
    public async Task Book_UnknownDoctor_IsRejected()
    {
        _doctors.Setup(d => d.ExistsAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().BookAsync(PatientA, 42, NextSlot, Ct));

        Assert.Equal(ValidationMessages.DoctorRequired, error.Message);
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Book_TakenSlot_IsReportedWithAFixedMessage()
    {
        _doctors.Setup(d => d.ExistsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appointments.Setup(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SlotAlreadyBookedException());

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().BookAsync(PatientA, 10, NextSlot, Ct));

        Assert.Equal(AppointmentService.SlotTaken, error.Message);
    }

    [Fact]
    public async Task Book_AsDoctor_IsDenied()
    {
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service().BookAsync(DoctorX, 10, NextSlot, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service().GetAvailableSlotsAsync(DoctorX, 10, NextSlot, Ct));
        _appointments.VerifyNoOtherCalls();
        _doctors.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetMyAppointments_Patient_QueriesOnlyTheirOwnAppointments()
    {
        _appointments.Setup(a => a.GetActiveForPatientAsync(PatientA.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Service().GetMyAppointmentsAsync(PatientA, Ct);

        _appointments.Verify(a => a.GetActiveForPatientAsync(PatientA.AccountId, It.IsAny<CancellationToken>()), Times.Once);
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetMyAppointments_Doctor_QueriesOnlyTheirOwnAppointments()
    {
        _appointments.Setup(a => a.GetActiveForDoctorAsync(DoctorX.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Service().GetMyAppointmentsAsync(DoctorX, Ct);

        _appointments.Verify(a => a.GetActiveForDoctorAsync(DoctorX.AccountId, It.IsAny<CancellationToken>()), Times.Once);
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAvailableSlots_SkipsBookedAndPastSlots()
    {
        var day = Now.Date;
        _appointments.Setup(a => a.GetBookedStartsAsync(10, day, day.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([day.AddHours(11), day.AddHours(16.5)]);

        var slots = await Service().GetAvailableSlotsAsync(PatientA, 10, Now, Ct);

        Assert.Equal(day.AddHours(10.5), slots[0]);
        Assert.DoesNotContain(day.AddHours(11), slots);
        Assert.DoesNotContain(day.AddHours(16.5), slots);
        Assert.Equal(16 - 3 - 2, slots.Count); // 16 slots, 3 already started (9:00, 9:30, 10:00), 2 booked
    }

    [Fact]
    public async Task GetAvailableSlots_WeekendOrBeyondTheWindow_IsEmptyWithoutAQuery()
    {
        Assert.Empty(await Service().GetAvailableSlotsAsync(PatientA, 10, new DateTime(2026, 3, 7), Ct));
        Assert.Empty(await Service().GetAvailableSlotsAsync(PatientA, 10, new DateTime(2026, 6, 1), Ct));
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_OwnFutureAppointment_CancelsWithOwnerConditions()
    {
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId));
        _appointments.Setup(a => a.CancelAsync(5, AccountRole.Patient, PatientA.AccountId, Now, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Service().CancelAsync(PatientA, 5, Ct);

        _appointments.Verify(
            a => a.CancelAsync(5, AccountRole.Patient, PatientA.AccountId, Now, Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cancel_DoctorOwnAppointment_CancelsWithDoctorConditions()
    {
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId));
        _appointments.Setup(a => a.CancelAsync(5, AccountRole.Doctor, DoctorX.AccountId, Now, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Service().CancelAsync(DoctorX, 5, Ct);
    }

    // IDOR: a valid session must not reach other accounts' appointments by passing their ids.
    [Fact]
    public async Task Cancel_AnotherPatientsAppointment_IsDeniedAndNothingIsChanged()
    {
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId));

        var error = await Assert.ThrowsAsync<AccessDeniedException>(() => Service().CancelAsync(PatientB, 5, Ct));

        Assert.Equal(AccessDeniedException.DefaultMessage, error.Message);
        _appointments.Verify(
            a => a.CancelAsync(It.IsAny<int>(), It.IsAny<AccountRole>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_AnotherDoctorsAppointment_IsDeniedAndNothingIsChanged()
    {
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId));

        await Assert.ThrowsAsync<AccessDeniedException>(() => Service().CancelAsync(DoctorY, 5, Ct));

        _appointments.Verify(
            a => a.CancelAsync(It.IsAny<int>(), It.IsAny<AccountRole>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_PatientUsingTheDoctorsIdAsTheirOwn_IsDenied()
    {
        // Patient 10 and doctor 10 are different accounts; the role decides which column is compared.
        var patientTen = new UserSession(AccountRole.Patient, DoctorX.AccountId, "Hasta 10", "***");
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId));

        await Assert.ThrowsAsync<AccessDeniedException>(() => Service().CancelAsync(patientTen, 5, Ct));
    }

    [Fact]
    public async Task Cancel_MissingAppointment_LooksTheSameAsAForeignOne()
    {
        _appointments.Setup(a => a.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Appointment?)null);

        var error = await Assert.ThrowsAsync<AccessDeniedException>(() => Service().CancelAsync(PatientA, 404, Ct));

        Assert.Equal(AccessDeniedException.DefaultMessage, error.Message);
    }

    [Fact]
    public async Task Cancel_StartedAppointment_IsRefused()
    {
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId, startsAt: Now.AddMinutes(-10)));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().CancelAsync(PatientA, 5, Ct));

        Assert.Equal(ValidationMessages.StartedCannotCancel, error.Message);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledAppointment_IsRefused()
    {
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId, status: AppointmentStatus.Cancelled));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().CancelAsync(PatientA, 5, Ct));

        Assert.Equal(ValidationMessages.AlreadyCancelled, error.Message);
    }

    [Fact]
    public async Task Cancel_ChangedConcurrently_IsReportedAsFailed()
    {
        SetupAppointment(Appointment(patientId: PatientA.AccountId, doctorId: DoctorX.AccountId));
        _appointments.Setup(a => a.CancelAsync(5, AccountRole.Patient, PatientA.AccountId, Now, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().CancelAsync(PatientA, 5, Ct));

        Assert.Equal(AppointmentService.CancelFailed, error.Message);
    }

    private static Appointment Appointment(
        int patientId,
        int doctorId,
        DateTime? startsAt = null,
        AppointmentStatus status = AppointmentStatus.Active) =>
        new()
        {
            Id = 5,
            PatientId = patientId,
            DoctorId = doctorId,
            StartsAt = startsAt ?? NextSlot,
            Status = status,
        };

    private void SetupAppointment(Appointment appointment) =>
        _appointments.Setup(a => a.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

    private AppointmentService Service() => ServiceFactory.Appointments(_appointments.Object, _doctors.Object, _time);
}
