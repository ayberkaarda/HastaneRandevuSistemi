using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>
/// Edges of the critical rules with strict mocks: lockout end, password format upgrade, the booking
/// window, the cancellation cut-off and ownership.
/// </summary>
public sealed class BoundaryTests
{
    private const string NationalId = "10000000450";

    // Wednesday 4 March 2026, 10:10; day 90 (2 June) is a Tuesday. The fake clock's local zone is UTC.
    private static readonly DateTime Now = new(2026, 3, 4, 10, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime LocalNow = new(2026, 3, 4, 10, 10, 0);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IPatientRepository> _patients = new(MockBehavior.Strict);
    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly Mock<IDoctorRepository> _doctors = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 2_000);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignIn_LockEndingExactlyNow_IsOver()
    {
        SetupAccount(Account(_hasher.Hash("Gizli.Sifre1"), lockoutEnd: Now));
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, It.IsAny<AccountCredentials>(), Now, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var session = await Auth().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct);

        Assert.Equal(7, session.AccountId);
    }

    [Fact]
    public async Task SignIn_LockEndingOneTickLater_StillRefusesWithoutWriting()
    {
        SetupAccount(Account(_hasher.Hash("Gizli.Sifre1"), lockoutEnd: Now.AddTicks(1)));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal(AuthService.PatientCredentialsInvalid, error.Message);
        _accounts.Verify(a => a.FindPatientByNationalIdAsync(NationalId, It.IsAny<CancellationToken>()), Times.Once);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignIn_FailureLocksForExactlyTheLockoutDuration()
    {
        SetupAccount(Account(_hasher.Hash("Gizli.Sifre1"), failedLogins: LoginPolicy.MaxFailedAttempts - 1));
        _accounts.Setup(a => a.RecordFailedLoginAsync(AccountRole.Patient, 7, 5, Now + TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInPatientAsync(NationalId, "yanlis", Ct));

        Assert.Equal(TimeSpan.FromMinutes(15), LoginPolicy.LockoutDuration);
        Assert.Equal(5, LoginPolicy.MaxFailedAttempts);
        _accounts.Verify(a => a.RecordFailedLoginAsync(AccountRole.Patient, 7, 5, Now.AddMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignIn_HashWithFewerIterations_IsReplacedWithACurrentHash()
    {
        var weaker = new PasswordHasher(iterations: 1_000).Hash("Gizli.Sifre1");
        SetupAccount(Account(weaker));
        string? replacement = null;
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, It.IsAny<AccountCredentials>(), Now, It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
            .Callback<AccountRole, AccountCredentials, DateTime, string?, CancellationToken>((_, _, _, hash, _) => replacement = hash)
            .ReturnsAsync(true);

        await Auth().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct);

        Assert.NotNull(replacement);
        Assert.StartsWith("PBKDF2-SHA256$2000$", replacement, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", replacement));
    }

    [Fact]
    public async Task SignIn_CurrentHash_IsNotRewritten()
    {
        SetupAccount(Account(_hasher.Hash("Gizli.Sifre1")));
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, It.IsAny<AccountCredentials>(), Now, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Auth().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct);

        _accounts.Verify(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, It.IsAny<AccountCredentials>(), Now, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignIn_HashFormatHoldingPlainText_IsNotReadAsPlainText()
    {
        // The format column decides; a hash account whose value is the typed password is a failure.
        SetupAccount(Account("Gizli.Sifre1", format: PasswordFormat.Pbkdf2));
        _accounts.Setup(a => a.RecordFailedLoginAsync(AccountRole.Patient, 7, LoginPolicy.MaxFailedAttempts, Now.AddMinutes(15), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct));

        _accounts.Verify(a => a.RecordFailedLoginAsync(AccountRole.Patient, 7, LoginPolicy.MaxFailedAttempts, Now.AddMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignIn_LegacyPlainText_IsStoredTrimmedAsAHash()
    {
        SetupAccount(Account("eski1", format: PasswordFormat.LegacyPlainText));
        string? replacement = null;
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, It.IsAny<AccountCredentials>(), Now, It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
            .Callback<AccountRole, AccountCredentials, DateTime, string?, CancellationToken>((_, _, _, hash, _) => replacement = hash)
            .ReturnsAsync(true);

        await Auth().SignInPatientAsync(NationalId, " eski1 ", Ct);

        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("eski1", replacement!));
    }

    [Fact]
    public async Task Slots_LastDayOfTheWindowIsOfferedAndTheNextIsNot()
    {
        var day90 = LocalNow.Date.AddDays(AppointmentRules.MaxDaysAhead);
        _appointments.Setup(a => a.GetBookedStartsAsync(10, day90, day90.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var lastDay = await Appointments().GetAvailableSlotsAsync(Patient(1), 10, day90.AddHours(13), Ct);
        var nextDay = await Appointments().GetAvailableSlotsAsync(Patient(1), 10, day90.AddDays(1), Ct);

        Assert.Equal(16, lastDay.Count);
        Assert.Equal(day90.AddHours(9), lastDay[0]);
        Assert.Equal(day90.AddHours(16.5), lastDay[^1]);
        Assert.Empty(nextDay);
        _appointments.Verify(a => a.GetBookedStartsAsync(10, day90, day90.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Book_LastSlotOfTheLastDay_IsAcceptedAndTheNextDayIsNot()
    {
        var day90 = LocalNow.Date.AddDays(AppointmentRules.MaxDaysAhead);
        _doctors.Setup(d => d.ExistsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appointments.Setup(a => a.AddAsync(It.Is<Appointment>(x => x.StartsAt == day90.AddHours(16.5)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await Appointments().BookAsync(Patient(1), 10, day90.AddHours(16.5), Ct);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments().BookAsync(Patient(1), 10, day90.AddDays(1).AddHours(9), Ct));

        Assert.Equal(ValidationMessages.TooFarAhead, error.Message);
    }

    [Fact]
    public async Task Book_SlotThatStartedASecondAgo_IsInThePast()
    {
        _time.SetUtcNow(new DateTimeOffset(2026, 3, 4, 11, 0, 1, TimeSpan.Zero));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Appointments().BookAsync(Patient(1), 10, new DateTime(2026, 3, 4, 11, 0, 0), Ct));

        Assert.Equal(ValidationMessages.PastSlot, error.Message);
        _doctors.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_AtTheStartTime_IsRefusedWithoutWriting()
    {
        var start = new DateTime(2026, 3, 4, 11, 0, 0);
        SetupAppointment(start);
        _time.SetUtcNow(new DateTimeOffset(start, TimeSpan.Zero));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments().CancelAsync(Patient(1), 5, Ct));

        Assert.Equal(ValidationMessages.StartedCannotCancel, error.Message);
        _appointments.Verify(a => a.GetByIdAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_OneSecondBeforeTheStart_PassesTheCutOffToTheRepository()
    {
        var start = new DateTime(2026, 3, 4, 11, 0, 0);
        var oneSecondBefore = start.AddSeconds(-1);
        SetupAppointment(start);
        _time.SetUtcNow(new DateTimeOffset(oneSecondBefore, TimeSpan.Zero));
        _appointments.Setup(a => a.CancelAsync(5, AccountRole.Patient, 1, oneSecondBefore, oneSecondBefore, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Appointments().CancelAsync(Patient(1), 5, Ct);

        _appointments.Verify(a => a.CancelAsync(5, AccountRole.Patient, 1, oneSecondBefore, oneSecondBefore, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IDOR: a doctor whose id equals the patient's id is still another account.
    [Fact]
    public async Task Cancel_DoctorWithThePatientsIdNumber_IsDenied()
    {
        SetupAppointment(LocalNow.AddDays(1), patientId: 1, doctorId: 10);

        await Assert.ThrowsAsync<AccessDeniedException>(
            () => Appointments().CancelAsync(new UserSession(AccountRole.Doctor, 1, "Dr. Bir", "***"), 5, Ct));

        _appointments.Verify(a => a.GetByIdAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        _appointments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_ForeignAppointment_IsDeniedBeforeItsStateIsRevealed()
    {
        // An already cancelled, past appointment of someone else gets the access answer, not a rule message.
        SetupAppointment(LocalNow.AddDays(-1), patientId: 2, status: AppointmentStatus.Cancelled);

        var error = await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments().CancelAsync(Patient(1), 5, Ct));

        Assert.Equal(AccessDeniedException.DefaultMessage, error.Message);
    }

    [Fact]
    public async Task GetMyAppointments_UnknownRole_IsDenied()
    {
        await Assert.ThrowsAsync<AccessDeniedException>(
            () => Appointments().GetMyAppointmentsAsync(new UserSession((AccountRole)7, 1, "?", "***"), Ct));

        _appointments.VerifyNoOtherCalls();
    }

    private static UserSession Patient(int id) => new(AccountRole.Patient, id, "Hasta " + id, "***");

    private static AccountCredentials Account(
        string stored,
        DateTime? lockoutEnd = null,
        PasswordFormat format = PasswordFormat.Pbkdf2,
        int failedLogins = 0) =>
        new(7, "Deniz Örnek", NationalId, stored, format, failedLogins, lockoutEnd);

    private void SetupAccount(AccountCredentials account) =>
        _accounts.Setup(a => a.FindPatientByNationalIdAsync(NationalId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

    private void SetupAppointment(
        DateTime startsAt,
        int patientId = 1,
        int doctorId = 10,
        AppointmentStatus status = AppointmentStatus.Active) =>
        _appointments.Setup(a => a.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new Appointment
        {
            Id = 5,
            PatientId = patientId,
            DoctorId = doctorId,
            StartsAt = startsAt,
            Status = status,
        });

    private AuthService Auth() => ServiceFactory.Auth(_accounts.Object, _patients.Object, _hasher, _time);

    private AppointmentService Appointments() => ServiceFactory.Appointments(_appointments.Object, _doctors.Object, _time);
}
