using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>Sign-in and registration with strict mocks: rejected input must never reach a repository.</summary>
public sealed class AuthServiceTests
{
    private const string NationalId = "10000000450";
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0, DateTimeKind.Utc);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IPatientRepository> _patients = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("", "Gizli.Sifre1")]
    [InlineData("1000000045", "Gizli.Sifre1")]
    [InlineData("1000000045a", "Gizli.Sifre1")]
    [InlineData(NationalId, "")]
    [InlineData(NationalId, null)]
    public async Task SignInPatient_InvalidInput_IsRejectedWithoutTouchingTheRepository(string nationalId, string? password)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SignInPatientAsync(nationalId, password, Ct));

        Assert.Equal(AuthService.PatientInputRequired, error.Message);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignInPatient_UnknownAccount_GivesTheSameAnswerAsAWrongPassword()
    {
        _accounts.Setup(a => a.FindPatientByNationalIdAsync(NationalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountCredentials?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal(AuthService.PatientCredentialsInvalid, error.Message);
        _accounts.Verify(a => a.FindPatientByNationalIdAsync(NationalId, It.IsAny<CancellationToken>()), Times.Once);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignInPatient_WrongPassword_CountsTheFailureAndLocksForFifteenMinutesAtTheLimit()
    {
        Setup(Account(_hasher.Hash("Gizli.Sifre1")));
        _accounts.Setup(a => a.RecordFailedLoginAsync(
                AccountRole.Patient, 7, LoginPolicy.MaxFailedAttempts, Now.AddMinutes(15), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SignInPatientAsync(NationalId, "yanlis", Ct));

        Assert.Equal(AuthService.PatientCredentialsInvalid, error.Message);
        _accounts.Verify(
            a => a.RecordFailedLoginAsync(AccountRole.Patient, 7, 5, Now.AddMinutes(15), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SignInPatient_LockedAccount_IsRefusedEvenWithTheRightPassword()
    {
        Setup(Account(_hasher.Hash("Gizli.Sifre1"), lockoutEnd: Now.AddMinutes(14).AddSeconds(30)));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal(AuthService.LockedMessage(TimeSpan.FromMinutes(14.5)), error.Message);
        Assert.Contains("15 dakika", error.Message, StringComparison.Ordinal);

        // Neither a failure nor a success is recorded while the lock lasts.
        _accounts.Verify(a => a.FindPatientByNationalIdAsync(NationalId, It.IsAny<CancellationToken>()), Times.Once);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignInPatient_ExpiredLockout_AllowsSignIn()
    {
        Setup(Account(_hasher.Hash("Gizli.Sifre1"), lockoutEnd: Now.AddSeconds(-1)));
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, 7, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var session = await Service().SignInPatientAsync(NationalId, "Gizli.Sifre1", Ct);

        Assert.Equal(7, session.AccountId);
    }

    [Fact]
    public async Task SignInPatient_Success_ResetsFailuresAndReturnsAMaskedSession()
    {
        Setup(Account(_hasher.Hash("Gizli.Sifre1")));
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, 7, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var session = await Service().SignInPatientAsync($" {NationalId} ", "Gizli.Sifre1", Ct);

        Assert.Equal(AccountRole.Patient, session.Role);
        Assert.Equal(7, session.AccountId);
        Assert.Equal("Deniz Örnek", session.DisplayName);
        Assert.Equal("100******50", session.MaskedNationalId);
        _accounts.Verify(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, 7, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignInPatient_LegacyPlainTextPassword_IsReplacedWithAHash()
    {
        Setup(Account("sifre1"));
        string? stored = null;
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Patient, 7, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<AccountRole, int, string?, CancellationToken>((_, _, hash, _) => stored = hash)
            .Returns(Task.CompletedTask);

        await Service().SignInPatientAsync(NationalId, "sifre1", Ct);

        Assert.NotNull(stored);
        Assert.StartsWith("PBKDF2-SHA256$", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("sifre1", stored, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("sifre1", stored));
    }

    [Fact]
    public async Task SignInDoctor_UsesTheNameAndTheDoctorRole()
    {
        _accounts.Setup(a => a.FindDoctorByNameAsync("Dr. Ada Örnek", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountCredentials(3, "Dr. Ada Örnek", "10000000146", _hasher.Hash("Demo.Doktor1"), 0, null));
        _accounts.Setup(a => a.RecordFailedLoginAsync(AccountRole.Doctor, 3, 5, Now.AddMinutes(15), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Doctor, 3, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var wrong = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SignInDoctorAsync("Dr. Ada Örnek", "yanlis", Ct));
        var session = await Service().SignInDoctorAsync(" Dr. Ada Örnek ", "Demo.Doktor1", Ct);

        Assert.Equal(AuthService.DoctorCredentialsInvalid, wrong.Message);
        Assert.Equal(AccountRole.Doctor, session.Role);
        Assert.Equal(3, session.AccountId);
    }

    [Theory]
    [InlineData("", "Gizli.Sifre1")]
    [InlineData("   ", "Gizli.Sifre1")]
    [InlineData("Dr. Ada Örnek", "")]
    public async Task SignInDoctor_MissingInput_IsRejectedWithoutTouchingTheRepository(string name, string password)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SignInDoctorAsync(name, password, Ct));

        Assert.Equal(AuthService.DoctorInputRequired, error.Message);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Register_ValidInput_StoresTrimmedValuesAndAHashedPassword()
    {
        Patient? added = null;
        _patients.Setup(p => p.NationalIdExistsAsync(NationalId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _patients.Setup(p => p.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, CancellationToken>((patient, _) => added = patient)
            .ReturnsAsync(12);

        await Service().RegisterPatientAsync(" Deniz Örnek ", $" {NationalId} ", "Gizli.Sifre1", Ct);

        Assert.NotNull(added);
        Assert.Equal("Deniz Örnek", added.FullName);
        Assert.Equal(NationalId, added.NationalId);
        Assert.NotEqual("Gizli.Sifre1", added.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", added.PasswordHash));
    }

    [Theory]
    [InlineData("", NationalId, "Gizli.Sifre1", "name")]
    [InlineData("Deniz Örnek", "10000000451", "Gizli.Sifre1", "national id")]
    [InlineData("Deniz Örnek", "12345678901", "Gizli.Sifre1", "national id")]
    [InlineData("Deniz Örnek", NationalId, "1234567", "password")]
    public async Task Register_InvalidInput_IsRejectedWithoutTouchingTheRepository(
        string name, string nationalId, string password, string field)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().RegisterPatientAsync(name, nationalId, password, Ct));

        var expected = field switch
        {
            "name" => AuthService.FullNameInvalid,
            "national id" => AuthService.NationalIdInvalid,
            _ => AuthService.PasswordInvalid,
        };
        Assert.Equal(expected, error.Message);
        Assert.DoesNotContain(nationalId, error.Message, StringComparison.Ordinal);
        _patients.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Register_NameLongerThanTheColumn_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().RegisterPatientAsync(new string('a', 81), NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal(AuthService.FullNameInvalid, error.Message);
        _patients.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Register_TakenNationalId_IsRejected()
    {
        _patients.Setup(p => p.NationalIdExistsAsync(NationalId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().RegisterPatientAsync("Deniz Örnek", NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal(AuthService.NationalIdTaken, error.Message);
    }

    [Fact]
    public async Task Register_ConcurrentRegistrationOfTheSameNationalId_IsReportedAsTaken()
    {
        _patients.Setup(p => p.NationalIdExistsAsync(NationalId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _patients.Setup(p => p.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateNationalIdException());

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().RegisterPatientAsync("Deniz Örnek", NationalId, "Gizli.Sifre1", Ct));

        Assert.Equal(AuthService.NationalIdTaken, error.Message);
    }

    private static AccountCredentials Account(string passwordHash, DateTime? lockoutEnd = null) =>
        new(7, "Deniz Örnek", NationalId, passwordHash, 2, lockoutEnd);

    private void Setup(AccountCredentials account) =>
        _accounts.Setup(a => a.FindPatientByNationalIdAsync(NationalId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

    private AuthService Service() => new(_accounts.Object, _patients.Object, _hasher, _time);
}
