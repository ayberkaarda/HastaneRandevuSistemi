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

    /// <summary>Environment variable that switches demo mode on when it is "1" or "true".</summary>
    public const string DemoEnvironmentVariable = "HASTANE_DEMO";

    /// <summary>Registers the data access layer on SQL Server.</summary>
    /// <param name="demo">Whether demo accounts are created; off when not given.</param>
    public static IServiceCollection AddSqlServerDataAccess(
        this IServiceCollection services,
        string connectionString,
        DemoDataOptions? demo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddDataAccess(options => options.UseSqlServer(connectionString), demo);
    }

    /// <summary>
    /// Registers the data access layer with a caller supplied provider configuration, so tests can
    /// plug in another provider.
    /// </summary>
    /// <param name="demo">Whether demo accounts are created; off when not given.</param>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider,
        DemoDataOptions? demo = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureProvider);

        services.AddDbContextFactory<HospitalDbContext>(configureProvider);
        services.AddSingleton(demo ?? new DemoDataOptions());
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<IPatientRepository, PatientRepository>();
        services.AddSingleton<IDoctorRepository, DoctorRepository>();
        services.AddSingleton<IAppointmentRepository, AppointmentRepository>();
        services.AddSingleton<IDoctorScheduleRepository, DoctorScheduleRepository>();
        return services;
    }
}
