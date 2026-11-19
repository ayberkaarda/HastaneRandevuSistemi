using FluentValidation;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HastaneRandevu.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>
    /// Registers the business services. Repositories must be registered separately; an
    /// <see cref="IAuditLog"/> registered before this call receives the audit events.
    /// </summary>
    /// <param name="policy">The configured appointment rules; the defaults when not given.</param>
    /// <exception cref="ArgumentException"><paramref name="policy"/> is invalid.</exception>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, AppointmentPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        policy ??= new AppointmentPolicy();
        if (policy.Validate() is { Count: > 0 } problems)
        {
            throw new ArgumentException(string.Join(" ", problems), nameof(policy));
        }

        services.AddSingleton(policy);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPasswordHasher>(_ => new PasswordHasher());
        services.TryAddSingleton<IAuditLog>(NullAuditLog.Instance);
        services.AddSingleton<IValidator<RegistrationInput>, RegistrationInputValidator>();
        services.AddSingleton<IValidator<PatientSignInInput>, PatientSignInInputValidator>();
        services.AddSingleton<IValidator<DoctorSignInInput>, DoctorSignInInputValidator>();
        services.AddSingleton<IValidator<BookingInput>, BookingInputValidator>();
        services.AddSingleton<IValidator<CancellationInput>, CancellationInputValidator>();
        services.AddSingleton<IValidator<WeeklyHoursInput>, WeeklyHoursInputValidator>();
        services.AddSingleton<IValidator<LeaveDayInput>, LeaveDayInputValidator>();
        services.AddSingleton<IValidator<RescheduleInput>, RescheduleInputValidator>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IAppointmentService, AppointmentService>();
        services.AddSingleton<IScheduleService, ScheduleService>();
        return services;
    }
}
