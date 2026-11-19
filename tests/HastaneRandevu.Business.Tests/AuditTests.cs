using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>Which anonymous audit events the services raise; an event carries only its kind and a role.</summary>
public sealed class AuditTests
{
    private const string NationalId = "10000000450";
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime NextSlot = new(2026, 3, 2, 10, 30, 0);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IPatientRepository> _patients = new(MockBehavior.Strict);
    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly Mock<IDoctorRepository> _doctors = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// The written codes are an interface of the log file: every event is pinned to its exact code and
    /// numeric value, so renaming, renumbering or adding an event without a code fails here.
    /// </summary>
    public static TheoryData<AuditEvent, int, string> ExpectedCodes => new()
    {
        { AuditEvent.SignInSucceeded, 1, "auth.sign_in.succeeded" },
        { AuditEvent.SignInFailed, 2, "auth.sign_in.failed" },
        { AuditEvent.PasswordUpgraded, 4, "auth.password.upgraded" },
        { AuditEvent.PatientRegistered, 5, "auth.patient.registered" },
        { AuditEvent.AppointmentBooked, 6, "appointment.booked" },
        { AuditEvent.AppointmentSlotTaken, 7, "appointment.slot_taken" },
        { AuditEvent.AppointmentCancelled, 8, "appointment.cancelled" },
        { AuditEvent.AccessDenied, 9, "access.denied" },
        { AuditEvent.ScheduleChanged, 10, "schedule.changed" },
    };

    [Theory]
    [MemberData(nameof(ExpectedCodes))]
    public void Code_OfEveryEvent_IsTheFixedValue(AuditEvent auditEvent, int value, string code)
    {
        Assert.Equal(value, (int)auditEvent);
        Assert.Equal(code, AuditCodes.Of(auditEvent));
    }

    [Fact]
    public void Codes_CoverEveryEventExactlyOnceAndCarryNoData()
    {
        var expected = ExpectedCodes.Select(row => (AuditEvent)row.Data.Item1).ToList();

        Assert.Equal(expected.Order(), Enum.GetValues<AuditEvent>().Order());
        Assert.Equal(expected.Count, expected.Select(AuditCodes.Of).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Enum.GetValues<AuditEvent>(), e => Assert.Matches("^[a-z_]+(\\.[a-z_]+)+$", AuditCodes.Of(e)));
        Assert.DoesNotContain("auth.account.locked", Enum.GetValues<AuditEvent>().Select(AuditCodes.Of));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditCodes.Of((AuditEvent)3));
    }

    [Theory]
    [InlineData(AccountRole.Patient, "patient")]
    [InlineData(AccountRole.Doctor, "doctor")]
    [InlineData((AccountRole)7, "unknown")]
    public void Code_OfRole_IsTheFixedValue(AccountRole role, string code) => Assert.Equal(code, AuditCodes.Of(role));

    [Fact]
    public async Task SignIn_LockedAccount_RecordsTheSameFailureCodeAsAWrongPassword()
    {
        SetupAccount(new AccountCredentials(7, "Deniz Örnek", NationalId, _hasher.Hash("Gizli.Sifre1"), PasswordFormat.Pbkdf2, 0, Now.AddMinutes(5)));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal([(AuditEvent.SignInFailed, AccountRole.Patient)], _audit.Events);
    }

    [Fact]
    public async Task SignIn_UnknownAccountAndWrongPassword_RecordTheSameFailureCode()
    {
        _accounts.Setup(a => a.FindDoctorByNameAsync("Dr. Yok", It.IsAny<CancellationToken>())).ReturnsAsync((AccountCredentials?)null);
        _accounts.Setup(a => a.FindDoctorByNameAsync("Dr. Ada Örnek", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountCredentials(3, "Dr. Ada Örnek", "10000000146", _hasher.Hash("Demo.Doktor1"), PasswordFormat.Pbkdf2, 0, null));
        _accounts.Setup(a => a.RecordFailedLoginAsync(AccountRole.Doctor, 3, LoginPolicy.MaxFailedAttempts, Now + LoginPolicy.LockoutDuration, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInDoctorAsync("Dr. Yok", "yanlis", Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInDoctorAsync("Dr. Ada Örnek", "yanlis", Ct));

        Assert.Equal([(AuditEvent.SignInFailed, AccountRole.Doctor), (AuditEvent.SignInFailed, AccountRole.Doctor)], _audit.Events);
    }

    [Fact]
    public async Task SignIn_LegacyPassword_RecordsTheUpgradeAndTheSuccess()
    {
        SetupAccount(new AccountCredentials(7, "Deniz Örnek", NationalId, "sifre1", PasswordFormat.LegacyPlainText, 0, null));
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, It.IsAny<AccountCredentials>(), Now, It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Auth().SignInPatientAsync(NationalId, "sifre1", Ct);

        Assert.Equal([(AuditEvent.PasswordUpgraded, AccountRole.Patient), (AuditEvent.SignInSucceeded, AccountRole.Patient)], _audit.Events);
    }

    [Fact]
    public async Task SignIn_LockedByAConcurrentFailure_RecordsTheFailureCode()
    {
        SetupAccount(new AccountCredentials(7, "Deniz Örnek", NationalId, _hasher.Hash("Gizli.Sifre1"), PasswordFormat.Pbkdf2, 4, null));
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, It.IsAny<AccountCredentials>(), Now, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal([(AuditEvent.SignInFailed, AccountRole.Patient)], _audit.Events);
    }

    [Fact]
    public async Task InvalidInput_IsNotAnAuditEvent()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().SignInPatientAsync("123", "x", Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth().RegisterPatientAsync("", NationalId, "x", Ct));

        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task Register_RecordsTheNewPatient()
    {
        _patients.Setup(p => p.NationalIdExistsAsync(NationalId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _patients.Setup(p => p.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await Auth().RegisterPatientAsync("Deniz Örnek", NationalId, "Gizli.Sifre1", Ct);

        Assert.Equal([(AuditEvent.PatientRegistered, AccountRole.Patient)], _audit.Events);
    }

    [Fact]
    public async Task Book_RecordsBookedOrSlotTaken()
    {
        _doctors.Setup(d => d.ExistsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appointments.SetupSequence(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ThrowsAsync(new SlotAlreadyBookedException());
        var patient = new UserSession(AccountRole.Patient, 1, "Hasta", "***");

        await Appointments().BookAsync(patient, 10, NextSlot, Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments().BookAsync(patient, 10, NextSlot, Ct));

        Assert.Equal([(AuditEvent.AppointmentBooked, AccountRole.Patient), (AuditEvent.AppointmentSlotTaken, AccountRole.Patient)], _audit.Events);
    }

    [Fact]
    public async Task ForeignAppointmentOrDoctorBooking_RecordsAccessDenied()
    {
        _appointments.Setup(a => a.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Appointment { Id = 5, PatientId = 1, DoctorId = 10, StartsAt = NextSlot, Status = AppointmentStatus.Active });
        var otherPatient = new UserSession(AccountRole.Patient, 2, "Hasta B", "***");
        var doctor = new UserSession(AccountRole.Doctor, 10, "Dr. X", "***");

        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments().CancelAsync(otherPatient, 5, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments().BookAsync(doctor, 10, NextSlot, Ct));

        Assert.Equal([(AuditEvent.AccessDenied, AccountRole.Patient), (AuditEvent.AccessDenied, AccountRole.Doctor)], _audit.Events);
    }

    [Fact]
    public async Task Cancel_RecordsTheCancellation()
    {
        _appointments.Setup(a => a.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Appointment { Id = 5, PatientId = 1, DoctorId = 10, StartsAt = NextSlot, Status = AppointmentStatus.Active });
        _appointments.Setup(a => a.CancelAsync(5, AccountRole.Doctor, 10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Appointments().CancelAsync(new UserSession(AccountRole.Doctor, 10, "Dr. X", "***"), 5, Ct);

        Assert.Equal([(AuditEvent.AppointmentCancelled, AccountRole.Doctor)], _audit.Events);
    }

    private void SetupAccount(AccountCredentials account) =>
        _accounts.Setup(a => a.FindPatientByNationalIdAsync(NationalId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

    private AuthService Auth() => ServiceFactory.Auth(_accounts.Object, _patients.Object, _hasher, _time, _audit);

    private AppointmentService Appointments() => ServiceFactory.Appointments(_appointments.Object, _doctors.Object, _time, _audit);

    private sealed class RecordingAudit : IAuditLog
    {
        public List<(AuditEvent Event, AccountRole Role)> Events { get; } = [];

        public void Record(AuditEvent auditEvent, AccountRole role) => Events.Add((auditEvent, role));
    }
}
