using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// The demo accounts have published passwords, so they exist only when demo mode is switched on.
/// Runs on SQLite, without SQL Server.
/// </summary>
public sealed class DemoDataTests
{
    [Fact]
    public async Task Initialize_DefaultConfiguration_DoesNotCreateDemoCredentials()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        var services = new ServiceCollection();
        services.AddDataAccess(options => options.UseSqlite(connection)); // no demo options: the default
        services.AddTestBusinessServices(new FakeTimeProvider(TestServices.Start));
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.False(provider.GetRequiredService<DemoDataOptions>().Enabled);
        await using (var db = await provider.GetRequiredService<IDbContextFactory<HospitalDbContext>>().CreateDbContextAsync(ct))
        {
            Assert.Equal(0, await db.Doctors.CountAsync(ct));
            Assert.Equal(0, await db.Patients.CountAsync(ct));
        }

        var auth = provider.GetRequiredService<IAuthService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInDoctorAsync("Dr. Ada Örnek", "Demo.Doktor1", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInPatientAsync("10000000450", "Demo.Hasta1", ct));
    }

    [Fact]
    public async Task Initialize_DemoMode_CreatesTheDemoAccountsOnceWithFixedIds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = new SqliteDatabase();
        await database.InitializeAsync();

        // A second start finds the demo accounts and adds nothing.
        Assert.Equal(DemoSeedOutcome.SkippedExistingData, await database.Get<IDatabaseInitializer>().InitializeAsync(ct));

        await using var db = database.CreateContext();
        Assert.Equal([1, 2, 3], await db.Doctors.OrderBy(d => d.Id).Select(d => d.Id).ToListAsync(ct));
        Assert.Equal([1, 2], await db.Patients.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct));
        var doctor = await database.Get<IAuthService>().SignInDoctorAsync("Dr. Ada Örnek", "Demo.Doktor1", ct);
        Assert.Equal(1, doctor.AccountId);
    }

    [Fact]
    public async Task DemoMode_KeepsAnExistingDoctorWithTheSameNameAndAddsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await AddExistingAccountAsync(connection, db => db.Doctors.Add(new() { FullName = "Dr. Ada Örnek", NationalId = "20000000003", PasswordHash = "-" }), ct);

        await using var demoProvider = DemoServices(connection);
        var outcome = await demoProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.Equal(DemoSeedOutcome.SkippedExistingData, outcome);
        await using var check = await demoProvider.GetRequiredService<IDbContextFactory<HospitalDbContext>>().CreateDbContextAsync(ct);
        var ada = await check.Doctors.SingleAsync(ct);
        Assert.Equal(("Dr. Ada Örnek", "20000000003", "-"), (ada.FullName, ada.NationalId, ada.PasswordHash));
        Assert.Equal(0, await check.Patients.CountAsync(ct));
    }

    /// <summary>
    /// One existing account that clashes with no demo account is enough: demo mode adds nothing next to
    /// it, and the published demo passwords do not work.
    /// </summary>
    [Fact]
    public async Task Initialize_DemoEnabled_NonEmptyDatabaseAddsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await AddExistingAccountAsync(connection, db => db.Patients.Add(new() { FullName = "Var Olan Hasta", NationalId = "30000000001", PasswordHash = "-" }), ct);

        await using var demoProvider = DemoServices(connection);
        var outcome = await demoProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.Equal(DemoSeedOutcome.SkippedExistingData, outcome);
        await using (var check = await demoProvider.GetRequiredService<IDbContextFactory<HospitalDbContext>>().CreateDbContextAsync(ct))
        {
            Assert.Equal(0, await check.Doctors.CountAsync(ct));
            var patient = await check.Patients.SingleAsync(ct);
            Assert.Equal(("Var Olan Hasta", "30000000001", "-"), (patient.FullName, patient.NationalId, patient.PasswordHash));
        }

        var auth = demoProvider.GetRequiredService<IAuthService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInDoctorAsync("Dr. Ada Örnek", "Demo.Doktor1", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInPatientAsync("10000000450", "Demo.Hasta1", ct));
    }

    /// <summary>Creates the schema without demo mode and adds one account, as an existing installation would have.</summary>
    private static async Task AddExistingAccountAsync(SqliteConnection connection, Action<HospitalDbContext> add, CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddDataAccess(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        await using var db = await provider.GetRequiredService<IDbContextFactory<HospitalDbContext>>().CreateDbContextAsync(ct);
        add(db);
        await db.SaveChangesAsync(ct);
    }

    private static ServiceProvider DemoServices(SqliteConnection connection, params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddDataAccess(options => options.UseSqlite(connection).AddInterceptors(interceptors), new DemoDataOptions { Enabled = true });
        services.AddTestBusinessServices(new FakeTimeProvider(TestServices.Start));
        return services.BuildServiceProvider();
    }
}
