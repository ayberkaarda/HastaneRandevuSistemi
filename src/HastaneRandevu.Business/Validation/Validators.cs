using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentValidation;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Validation;

/// <summary>
/// User facing (Turkish) validation messages. They name fields and limits only, never the entered
/// values, so they can be shown and logged as they are.
/// </summary>
internal static class ValidationMessages
{
    public const string PatientInputRequired = "Lütfen geçerli TC ve şifre giriniz.";
    public const string DoctorInputRequired = "Lütfen tüm alanları doldurun.";
    public const string FullNameInvalid = "Ad soyad boş bırakılamaz ve en fazla 80 karakter olabilir.";
    public const string NationalIdInvalid = "Lütfen geçerli bir T.C. Kimlik No giriniz.";

    public const string DoctorRequired = "Lütfen bir doktor seçiniz.";
    public const string InvalidSlot = "Lütfen çalışma saatleri içinde geçerli bir randevu saati seçiniz.";
    public const string PastSlot = "Geçmiş bir tarih veya saat için randevu alınamaz.";
    public const string TooFarAhead = "Randevu en fazla 90 gün sonrası için alınabilir.";
    public const string AlreadyCancelled = "Bu randevu zaten iptal edilmiş.";
    public const string StartedCannotCancel = "Geçmiş veya başlamış bir randevu iptal edilemez.";

    public static readonly string PasswordInvalid = string.Create(
        CultureInfo.InvariantCulture,
        $"Şifre en az {PasswordPolicy.MinLength}, en fazla {PasswordPolicy.MaxLength} karakter olmalıdır.");
}

internal static class ValidationRuleExtensions
{
    /// <summary>
    /// Validates <paramref name="input"/> and throws a <see cref="BusinessRuleException"/> carrying the
    /// first failure message, so the user sees one clear message at a time.
    /// </summary>
    public static void ValidateOrThrow<T>(this IValidator<T> validator, T input)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var result = validator.Validate(input);
        if (!result.IsValid)
        {
            throw new BusinessRuleException(result.Errors[0].ErrorMessage);
        }
    }

    /// <summary>
    /// Sign-in only bounds the password by what an existing account can hold; the registration rules of
    /// <see cref="PasswordPolicy"/> apply to new passwords and would lock out legacy accounts here.
    /// </summary>
    public static bool IsAcceptableSignInPassword([NotNullWhen(true)] string? password) =>
        !string.IsNullOrEmpty(password) && password.Length <= PasswordPolicy.SignInMaxLength;
}

/// <summary>Registration: name 1-80 characters, a valid national id, a password of 8-64 characters.</summary>
internal sealed class RegistrationInputValidator : AbstractValidator<RegistrationInput>
{
    public const int FullNameMaxLength = 80;

    public RegistrationInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.FullName)
            .NotEmpty().WithMessage(ValidationMessages.FullNameInvalid)
            .MaximumLength(FullNameMaxLength).WithMessage(ValidationMessages.FullNameInvalid);
        RuleFor(r => r.NationalId)
            .Must(NationalIdNumber.IsValid).WithMessage(ValidationMessages.NationalIdInvalid);
        RuleFor(r => r.Password)
            .Must(p => p is { Length: >= PasswordPolicy.MinLength and <= PasswordPolicy.MaxLength })
            .WithMessage(ValidationMessages.PasswordInvalid);
    }
}

/// <summary>
/// Patient sign-in: 11 digits and a password an existing account can have. Only the format is checked,
/// not the checksum, so the answer does not reveal which national ids are well formed.
/// </summary>
internal sealed class PatientSignInInputValidator : AbstractValidator<PatientSignInInput>
{
    public PatientSignInInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(s => s.NationalId)
            .Must(NationalIdNumber.HasValidFormat).WithMessage(ValidationMessages.PatientInputRequired);
        RuleFor(s => s.Password)
            .Must(ValidationRuleExtensions.IsAcceptableSignInPassword).WithMessage(ValidationMessages.PatientInputRequired);
    }
}

/// <summary>Doctor sign-in: a name and a password an existing account can have.</summary>
internal sealed class DoctorSignInInputValidator : AbstractValidator<DoctorSignInInput>
{
    public DoctorSignInInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(s => s.FullName)
            .NotEmpty().WithMessage(ValidationMessages.DoctorInputRequired);
        RuleFor(s => s.Password)
            .Must(ValidationRuleExtensions.IsAcceptableSignInPassword).WithMessage(ValidationMessages.DoctorInputRequired);
    }
}

/// <summary>
/// Booking: a doctor is chosen and the start is a slot of <see cref="AppointmentRules"/>, in the future
/// and at most <see cref="AppointmentRules.MaxDaysAhead"/> days ahead.
/// </summary>
internal sealed class BookingInputValidator : AbstractValidator<BookingInput>
{
    public BookingInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(b => b.DoctorId)
            .GreaterThan(0).WithMessage(ValidationMessages.DoctorRequired);
        RuleFor(b => b.StartsAt)
            .Must(AppointmentRules.IsSlotStart).WithMessage(ValidationMessages.InvalidSlot)
            .Must((b, start) => start > b.Now).WithMessage(ValidationMessages.PastSlot)
            .Must((b, start) => start.Date <= b.Now.Date.AddDays(AppointmentRules.MaxDaysAhead))
            .WithMessage(ValidationMessages.TooFarAhead);
    }
}

/// <summary>Cancellation: the appointment is still active and has not started yet.</summary>
internal sealed class CancellationInputValidator : AbstractValidator<CancellationInput>
{
    public CancellationInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Status)
            .NotEqual(AppointmentStatus.Cancelled).WithMessage(ValidationMessages.AlreadyCancelled);
        RuleFor(c => c.StartsAt)
            .Must((c, start) => start > c.Now).WithMessage(ValidationMessages.StartedCannotCancel);
    }
}
