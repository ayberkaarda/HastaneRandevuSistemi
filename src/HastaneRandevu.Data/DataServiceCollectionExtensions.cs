using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Data.Repositories;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Environment variable that overrides the configured connection string.</summary>
    public const string ConnectionEnvironmentVariable = "HASTANE_CONNECTION";

    /// <summary>Registers the data access layer on SQL Server.</summary>
    public static IServiceCollection AddSqlServerDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddDataAccess(options => options.UseSqlServer(connectionString));
    }

    /// <summary>
    /// Registers the data access layer with a caller supplied provider configuration, so tests can
    /// plug in another provider.
    /// </summary>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureProvider);

        services.AddDbContextFactory<HospitalDbContext>(configureProvider);
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<IPatientRepository, PatientRepository>();
        services.AddSingleton<IDoctorRepository, DoctorRepository>();
        services.AddSingleton<IAppointmentRepository, AppointmentRepository>();
        services.AddSingleton<IDoctorScheduleRepository, DoctorScheduleRepository>();
        return services;
    }
}
