using System.Globalization;
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
    TimeProvider timeProvider) : IAuthService
{
    internal const int FullNameMaxLength = 80;

    internal const string PatientInputRequired = "Lütfen geçerli TC ve şifre giriniz.";
    internal const string PatientCredentialsInvalid = "TC veya şifre hatalı. Lütfen tekrar deneyin.";
    internal const string DoctorInputRequired = "Lütfen tüm alanları doldurun.";
    internal const string DoctorCredentialsInvalid = "Doktor adı veya şifre hatalı.";
    internal const string FullNameInvalid = "Ad soyad boş bırakılamaz ve en fazla 80 karakter olabilir.";
    internal const string NationalIdInvalid = "Lütfen geçerli bir T.C. Kimlik No giriniz.";
    internal const string NationalIdTaken = "Bu T.C. Kimlik No ile kayıtlı bir hasta zaten var.";
    internal static readonly string PasswordInvalid = string.Create(
        CultureInfo.InvariantCulture,
        $"Şifre en az {PasswordPolicy.MinLength}, en fazla {PasswordPolicy.MaxLength} karakter olmalıdır.");

    public async Task<UserSession> SignInPatientAsync(
        string? nationalId,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var id = nationalId?.Trim();
        if (!NationalIdNumber.HasValidFormat(id) || string.IsNullOrEmpty(password) || password.Length > PasswordPolicy.MaxLength)
        {
            throw new BusinessRuleException(PatientInputRequired);
        }

        var account = await accounts.FindPatientByNationalIdAsync(id!, cancellationToken).ConfigureAwait(false);
        return await SignInAsync(AccountRole.Patient, account, password, PatientCredentialsInvalid, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserSession> SignInDoctorAsync(
        string? fullName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var name = fullName?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password) || password.Length > PasswordPolicy.MaxLength)
        {
            throw new BusinessRuleException(DoctorInputRequired);
        }

        var account = await accounts.FindDoctorByNameAsync(name, cancellationToken).ConfigureAwait(false);
        return await SignInAsync(AccountRole.Doctor, account, password, DoctorCredentialsInvalid, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RegisterPatientAsync(
        string? fullName,
        string? nationalId,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var name = fullName?.Trim();
        var id = nationalId?.Trim();

        if (string.IsNullOrEmpty(name) || name.Length > FullNameMaxLength)
        {
            throw new BusinessRuleException(FullNameInvalid);
        }

        if (!NationalIdNumber.IsValid(id))
        {
            throw new BusinessRuleException(NationalIdInvalid);
        }

        if (password is null || password.Length < PasswordPolicy.MinLength || password.Length > PasswordPolicy.MaxLength)
        {
            throw new BusinessRuleException(PasswordInvalid);
        }

        if (await patients.NationalIdExistsAsync(id!, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(NationalIdTaken);
        }

        var patient = new Patient
        {
            FullName = name,
            NationalId = id!,
            PasswordHash = passwordHasher.Hash(password),
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
            throw new BusinessRuleException(invalidMessage);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (account.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            throw new BusinessRuleException(LockedMessage(lockoutEnd - now));
        }

        var result = passwordHasher.Verify(password, account.PasswordHash);
        if (result == PasswordVerificationResult.Failed)
        {
            await accounts.RecordFailedLoginAsync(
                role,
                account.Id,
                LoginPolicy.MaxFailedAttempts,
                now + LoginPolicy.LockoutDuration,
                cancellationToken).ConfigureAwait(false);
            throw new BusinessRuleException(invalidMessage);
        }

        string? newHash = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Plain text passwords were trimmed by the original application before they were stored.
            var legacy = !account.PasswordHash.StartsWith(PasswordHasher.Prefix, StringComparison.Ordinal);
            newHash = passwordHasher.Hash(legacy ? password.Trim() : password);
        }

        await accounts.RecordSuccessfulLoginAsync(role, account.Id, newHash, cancellationToken).ConfigureAwait(false);

        return new UserSession(role, account.Id, account.FullName, NationalIdNumber.Mask(account.NationalId));
    }

    internal static string LockedMessage(TimeSpan remaining)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Çok sayıda hatalı giriş denemesi yapıldı. Hesap {minutes} dakika sonra yeniden denenebilir.");
    }
}
