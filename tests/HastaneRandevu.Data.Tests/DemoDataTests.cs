using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

        await database.Get<IDatabaseInitializer>().InitializeAsync(ct); // a second start adds nothing

        await using var db = database.CreateContext();
        Assert.Equal([1, 2, 3], await db.Doctors.OrderBy(d => d.Id).Select(d => d.Id).ToListAsync(ct));
        Assert.Equal([1, 2], await db.Patients.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct));
        var doctor = await database.Get<IAuthService>().SignInDoctorAsync("Dr. Ada Örnek", "Demo.Doktor1", ct);
        Assert.Equal(1, doctor.AccountId);
    }

    [Fact]
    public async Task DemoMode_KeepsAnExistingDoctorWithTheSameName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        var services = new ServiceCollection();
        services.AddDataAccess(options => options.UseSqlite(connection));
        await using (var provider = services.BuildServiceProvider())
        {
            await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
            await using var db = await provider.GetRequiredService<IDbContextFactory<HospitalDbContext>>().CreateDbContextAsync(ct);
            db.Doctors.Add(new() { FullName = "Dr. Ada Örnek", NationalId = "20000000003", PasswordHash = "-" });
            await db.SaveChangesAsync(ct);
        }

        var demoServices = new ServiceCollection();
        demoServices.AddDataAccess(options => options.UseSqlite(connection), new DemoDataOptions { Enabled = true });
        await using var demoProvider = demoServices.BuildServiceProvider();
        await demoProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        await using var check = await demoProvider.GetRequiredService<IDbContextFactory<HospitalDbContext>>().CreateDbContextAsync(ct);
        var ada = await check.Doctors.SingleAsync(d => d.FullName == "Dr. Ada Örnek", ct);
        Assert.Equal("20000000003", ada.NationalId);
        Assert.Equal("-", ada.PasswordHash);
        Assert.Equal(3, await check.Doctors.CountAsync(ct));
    }
}
