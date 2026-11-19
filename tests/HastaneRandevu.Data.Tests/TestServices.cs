using HastaneRandevu.Business;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace HastaneRandevu.Data.Tests;

/// <summary>Shared registrations for tests that run the real services on a real database.</summary>
internal static class TestServices
{
    /// <summary>Monday 2 March 2026, 10:10; the fake clock's local time zone is UTC.</summary>
    public static readonly DateTimeOffset Start = new(2026, 3, 2, 10, 10, 0, TimeSpan.Zero);

    /// <summary>A free weekday slot after <see cref="Start"/>.</summary>
    public static readonly DateTime Tomorrow10 = new(2026, 3, 3, 10, 0, 0);

    /// <summary>
    /// Adds the business services with a fake clock and a cheaper password hash (new hashes only; the
    /// seeded hashes keep their own iteration count).
    /// </summary>
    public static IServiceCollection AddTestBusinessServices(this IServiceCollection services, FakeTimeProvider clock)
    {
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IPasswordHasher>(new PasswordHasher(iterations: 1_000));
        services.AddBusinessServices();
        return services;
    }

    public static UserSession Patient(int id) => new(AccountRole.Patient, id, "Hasta " + id, "***");

    public static UserSession Doctor(int id) => new(AccountRole.Doctor, id, "Doktor " + id, "***");
}
