using FluentValidation;

namespace HastaneRandevu.Business;

/// <summary>
/// Configurable appointment rules, bound from the <c>Appointments</c> section of the application
/// settings. The defaults apply when the section or a value is missing; <see cref="Validate"/> lists the
/// problems of configured values, and the application refuses to start with an invalid policy.
/// </summary>
public sealed class AppointmentPolicy
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Appointments";

    public const int MaxRescheduleCutoffHours = 168;

    /// <summary>
    /// A patient can move an appointment to another slot until this many hours before it starts
    /// (0: until it starts).
    /// </summary>
    public int RescheduleCutoffHours { get; set; } = 2;

    internal TimeSpan RescheduleCutoff => TimeSpan.FromHours(RescheduleCutoffHours);

    public const int MaxReminderLeadHours = 168;

    /// <summary>Appointments starting within this many hours are announced when the patient signs in.</summary>
    public int ReminderLeadHours { get; set; } = 24;

    internal TimeSpan ReminderLead => TimeSpan.FromHours(ReminderLeadHours);

    /// <summary>
    /// Whether reminders are also shown as a notification balloon, which never takes the focus; the
    /// in-window notice is always shown.
    /// </summary>
    public bool ShowReminderBalloon { get; set; } = true;

    /// <summary>Returns the (Turkish) problems with the configured values; empty when the policy is valid.</summary>
    public IReadOnlyList<string> Validate() =>
        new AppointmentPolicyValidator().Validate(this).Errors.Select(e => e.ErrorMessage).ToList();
}

internal sealed class AppointmentPolicyValidator : AbstractValidator<AppointmentPolicy>
{
    public AppointmentPolicyValidator()
    {
        RuleFor(p => p.RescheduleCutoffHours)
            .InclusiveBetween(0, AppointmentPolicy.MaxRescheduleCutoffHours)
            .WithMessage($"RescheduleCutoffHours 0 ile {AppointmentPolicy.MaxRescheduleCutoffHours} arasında olmalıdır.");
        RuleFor(p => p.ReminderLeadHours)
            .InclusiveBetween(1, AppointmentPolicy.MaxReminderLeadHours)
            .WithMessage($"ReminderLeadHours 1 ile {AppointmentPolicy.MaxReminderLeadHours} arasında olmalıdır.");
    }
}
