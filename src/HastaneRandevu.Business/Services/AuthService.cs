using System.Globalization;
using FluentValidation;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Services;

public interface IAuthService
{
    /// <summary>Signs a patient in with the national id and password.</summary>
    /// <exception cref="BusinessRuleException">Wrong credentials, invalid input or a locked account.</exception>
    Task<UserSession> SignInPatientAsync(string? nationalId, string? password, CancellationToken cancellationToken = default);

    /// <summary>Signs a doctor in with the full name and password.</summary>
    /// <exception cref="BusinessRuleException">Wrong credentials, invalid input or a locked account.</exception>
    Task<UserSession> SignInDoctorAsync(string? fullName, string? password, CancellationToken cancellationToken = default);

    /// <summary>Registers a new patient. The password is stored as a hash.</summary>
    Task RegisterPatientAsync(string? fullName, string? nationalId, string? password, CancellationToken cancellationToken = default);
}

internal sealed class AuthService(
    IAccountRepository accounts,
    IPatientRepository patients,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IValidator<RegistrationInput> registrationValidator,
    IValidator<PatientSignInInput> patientSignInValidator,
    IValidator<DoctorSignInInput> doctorSignInValidator,
    IAuditLog audit) : IAuthService
{
    // A locked account gets the same answer as wrong credentials, so the lock state is not disclosed
    // before the user has proven who they are; the lockout rule itself is stated for everyone.
    internal static readonly string PatientCredentialsInvalid = "TC veya şifre hatalı. " + LockoutNotice;
    internal static readonly string DoctorCredentialsInvalid = "Doktor adı veya şifre hatalı. " + LockoutNotice;
    internal const string NationalIdTaken = "Bu T.C. Kimlik No ile kayıtlı bir hasta zaten var.";

    private static string LockoutNotice => string.Create(
        CultureInfo.InvariantCulture,
        $"{LoginPolicy.MaxFailedAttempts} hatalı denemeden sonra giriş {LoginPolicy.LockoutDuration.TotalMinutes:0} dakika engellenir.");

    public async Task<UserSession> SignInPatientAsync(
        string? nationalId,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var input = PatientSignInInput.Create(nationalId, password);
        patientSignInValidator.ValidateOrThrow(input);

        var account = await accounts.FindPatientByNationalIdAsync(input.NationalId, cancellationToken).ConfigureAwait(false);
        return await SignInAsync(AccountRole.Patient, account, input.Password!, PatientCredentialsInvalid, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserSession> SignInDoctorAsync(
        string? fullName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var input = DoctorSignInInput.Create(fullName, password);
        doctorSignInValidator.ValidateOrThrow(input);

        var account = await accounts.FindDoctorByNameAsync(input.FullName, cancellationToken).ConfigureAwait(false);
        return await SignInAsync(AccountRole.Doctor, account, input.Password!, DoctorCredentialsInvalid, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RegisterPatientAsync(
        string? fullName,
        string? nationalId,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var input = RegistrationInput.Create(fullName, nationalId, password);
        registrationValidator.ValidateOrThrow(input);

        if (await patients.NationalIdExistsAsync(input.NationalId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(NationalIdTaken);
        }

        var patient = new Patient
        {
            FullName = input.FullName,
            NationalId = input.NationalId,
            PasswordHash = passwordHasher.Hash(input.Password!),
            PasswordFormat = PasswordFormat.Pbkdf2,
        };

        try
        {
            await patients.AddAsync(patient, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateNationalIdException ex)
        {
            // Registered concurrently between the check and the insert.
            throw new BusinessRuleException(NationalIdTaken, ex);
        }

        audit.Record(AuditEvent.PatientRegistered, AccountRole.Patient);
    }

    private async Task<UserSession> SignInAsync(
        AccountRole role,
        AccountCredentials? account,
        string password,
        string invalidMessage,
        CancellationToken cancellationToken)
    {
        if (account is null)
        {
            passwordHasher.SimulateVerification(password);
            audit.Record(AuditEvent.SignInFailed, role);
            throw new BusinessRuleException(invalidMessage);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (account.LockoutEnd > now)
        {
            // Same answer and about the same cost as a wrong password; nothing is written, so the
            // lock neither grows nor ends early.
            passwordHasher.SimulateVerification(password);
            audit.Record(AuditEvent.SignInLocked, role);
            throw new BusinessRuleException(invalidMessage);
        }

        var result = passwordHasher.Verify(password, account.PasswordHash, account.PasswordFormat);
        if (result == PasswordVerificationResult.Failed)
        {
            await accounts.RecordFailedLoginAsync(
                role,
                account.Id,
                LoginPolicy.MaxFailedAttempts,
                now + LoginPolicy.LockoutDuration,
                cancellationToken).ConfigureAwait(false);
            audit.Record(AuditEvent.SignInFailed, role);
            throw new BusinessRuleException(invalidMessage);
        }

        string? newHash = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Plain text passwords were trimmed by the original application before they were stored.
            var legacy = account.PasswordFormat == PasswordFormat.LegacyPlainText;
            newHash = passwordHasher.Hash(legacy ? password.Trim() : password);
        }

        // The session is issued only if the account is still unlocked and unchanged when the success
        // is written; a lock placed by a concurrent failure since the read wins.
        var recorded = await accounts.RecordSuccessfulLoginAsync(role, account, now, newHash, cancellationToken)
            .ConfigureAwait(false);
        if (!recorded)
        {
            // A concurrent failure locked the account (or its password changed) after it was read.
            audit.Record(AuditEvent.SignInLocked, role);
            throw new BusinessRuleException(invalidMessage);
        }

        if (newHash is not null)
        {
            audit.Record(AuditEvent.PasswordUpgraded, role);
        }

        audit.Record(AuditEvent.SignInSucceeded, role);

        return new UserSession(role, account.Id, account.FullName, NationalIdNumber.Mask(account.NationalId));
    }
}
