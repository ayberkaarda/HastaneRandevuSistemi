using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentValidation;
using HastaneRandevu.Business.Scheduling;
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

    public const string WorkingDayRequired = "En az bir çalışma günü seçiniz.";
    public const string WorkingDayRepeated = "Her gün yalnızca bir kez seçilebilir.";
    public const string WorkingHoursInvalid =
        "Çalışma saatleri yarım saatlik adımlarla seçilmeli ve başlangıç saati bitiş saatinden önce olmalıdır.";
public const string StartedCannotReschedule = "Geçmiş veya başlamış bir randevu ertelenemez.";
    public const string SameSlot = "Lütfen randevunuzun şu anki saatinden farklı bir saat seçiniz.";
    public const string LeaveDayInPast = "Geçmiş bir gün izin olarak eklenemez.";
    public const string LeaveDayTooFarAhead = "İzin günü en fazla 365 gün sonrası için eklenebilir.";

    /// <summary>The cut-off for moving an appointment; <paramref name="cutoff"/> comes from the settings.</summary>
    public static string RescheduleTooLate(TimeSpan cutoff) => string.Create(
        CultureInfo.InvariantCulture,
        $"Randevu en geç başlamadan {cutoff.TotalHours:0} saat öncesine kadar ertelenebilir.");

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
/// Booking: a doctor is chosen and the start is a slot of the doctor's <see cref="DoctorSchedule"/> (not
/// on a leave day), in the future and at most <see cref="AppointmentRules.MaxDaysAhead"/> days ahead.
/// </summary>
internal sealed class BookingInputValidator : AbstractValidator<BookingInput>
{
    public BookingInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(b => b.DoctorId)
            .GreaterThan(0).WithMessage(ValidationMessages.DoctorRequired);
        RuleFor(b => b.StartsAt).MustBeBookableSlot(b => b.Schedule, b => b.Now);
    }
}

internal static class SlotRuleExtensions
{
    /// <summary>
    /// The rules every new appointment time follows, in this order: a slot of the doctor's schedule, in
    /// the future, at most <see cref="AppointmentRules.MaxDaysAhead"/> days ahead (day 90 included).
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> MustBeBookableSlot<T>(
        this IRuleBuilder<T, DateTime> rule,
        Func<T, DoctorSchedule> schedule,
        Func<T, DateTime> now) =>
        rule
            .Must((input, start) => schedule(input).IsSlotStart(start)).WithMessage(ValidationMessages.InvalidSlot)
            .Must((input, start) => start > now(input)).WithMessage(ValidationMessages.PastSlot)
            .Must((input, start) => start.Date <= now(input).Date.AddDays(AppointmentRules.MaxDaysAhead))
            .WithMessage(ValidationMessages.TooFarAhead);
}

/// <summary>
/// Weekly hours: at least one day, each weekday at most once, and per day a start before the end on
/// 30 minute boundaries between 00:00 and 24:00.
/// </summary>
internal sealed class WeeklyHoursInputValidator : AbstractValidator<WeeklyHoursInput>
{
    private static readonly TimeSpan EndOfDay = TimeSpan.FromDays(1);

    public WeeklyHoursInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(w => w.Days)
            .NotNull().WithMessage(ValidationMessages.WorkingDayRequired)
            .Must(days => days.Count > 0).WithMessage(ValidationMessages.WorkingDayRequired)
            .Must(days => days.All(d => d is not null && Enum.IsDefined(d.Day))).WithMessage(ValidationMessages.WorkingHoursInvalid)
            .Must(days => days.Select(d => d.Day).Distinct().Count() == days.Count).WithMessage(ValidationMessages.WorkingDayRepeated)
            .Must(days => days.All(IsValid)).WithMessage(ValidationMessages.WorkingHoursInvalid);
    }

    public static bool IsValid(WorkingDay day) =>
        day.Start >= TimeSpan.Zero
        && day.End <= EndOfDay
        && day.Start < day.End
        && day.Start.Ticks % AppointmentRules.SlotLength.Ticks == 0
        && day.End.Ticks % AppointmentRules.SlotLength.Ticks == 0;
}

/// <summary>Leave day: from today up to <see cref="LeaveDayMaxDaysAhead"/> days ahead.</summary>
internal sealed class LeaveDayInputValidator : AbstractValidator<LeaveDayInput>
{
    public const int LeaveDayMaxDaysAhead = 365;

    public LeaveDayInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(l => l.Day)
            .Must((l, day) => day >= l.Today).WithMessage(ValidationMessages.LeaveDayInPast)
            .Must((l, day) => day <= l.Today.AddDays(LeaveDayMaxDaysAhead)).WithMessage(ValidationMessages.LeaveDayTooFarAhead);
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

/// <summary>
/// Rescheduling: the appointment is active, has not started, is still at least the configured cut-off
/// away, and the new time differs and follows the same rules as a new booking.
/// </summary>
internal sealed class RescheduleInputValidator : AbstractValidator<RescheduleInput>
{
    public RescheduleInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Status)
            .NotEqual(AppointmentStatus.Cancelled).WithMessage(ValidationMessages.AlreadyCancelled);
        RuleFor(r => r.CurrentStartsAt)
            .Must((r, start) => start > r.Now).WithMessage(ValidationMessages.StartedCannotReschedule)
            .Must((r, start) => start - r.Now >= r.Cutoff).WithMessage(r => ValidationMessages.RescheduleTooLate(r.Cutoff));
        RuleFor(r => r.NewStartsAt)
            .Must((r, start) => start != r.CurrentStartsAt).WithMessage(ValidationMessages.SameSlot)
            .MustBeBookableSlot(r => r.Schedule, r => r.Now);
    }
}
