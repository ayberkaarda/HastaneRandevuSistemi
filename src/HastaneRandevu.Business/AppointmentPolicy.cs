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
    }
}
