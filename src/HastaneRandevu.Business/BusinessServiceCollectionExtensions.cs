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
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPasswordHasher>(_ => new PasswordHasher());
        services.TryAddSingleton<IAuditLog>(NullAuditLog.Instance);
        services.AddSingleton<IValidator<RegistrationInput>, RegistrationInputValidator>();
        services.AddSingleton<IValidator<PatientSignInInput>, PatientSignInInputValidator>();
        services.AddSingleton<IValidator<DoctorSignInInput>, DoctorSignInInputValidator>();
        services.AddSingleton<IValidator<BookingInput>, BookingInputValidator>();
        services.AddSingleton<IValidator<CancellationInput>, CancellationInputValidator>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IAppointmentService, AppointmentService>();
        return services;
    }
}
