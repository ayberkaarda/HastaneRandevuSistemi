using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// Concurrency on a real SQL Server instance (see <see cref="SqlServerTestDatabase"/>): many patients
/// race for one slot, and failed sign-ins arrive at the same time.
/// </summary>
public sealed class SqlServerConcurrencyTests
{
    private const int Contenders = 16;

    [Fact]
    public async Task Book_ManyPatientsRaceForOneSlot_ExactlyOneWins()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var patientIds = await AddPatientsAsync(database, Contenders, ct);
        var service = provider.GetRequiredService<IAppointmentService>();

        // Several rounds make it likely that inserts really overlap between the check and the commit.
        for (var round = 0; round < 5; round++)
        {
            var slot = TestServices.Tomorrow10.AddMinutes(30 * round);
            using var start = new ManualResetEventSlim(false);
            var attempts = patientIds
                .Select(id => Task.Run(
                    async () =>
                    {
                        start.Wait(ct);
                        try
                        {
                            await service.BookAsync(TestServices.Patient(id), 1, slot, ct);
                            return (Won: true, Error: (Exception?)null);
                        }
#pragma warning disable CA1031 // Collected and asserted below.
                        catch (Exception ex)
#pragma warning restore CA1031
                        {
                            return (Won: false, Error: ex);
                        }
                    },
                    ct))
                .ToList();
            start.Set();
            var results = await Task.WhenAll(attempts);

            Assert.Equal(1, results.Count(r => r.Won));
            Assert.All(
                results.Where(r => !r.Won),
                r => Assert.Equal(
                    "Bu randevu saatinde doktor zaten dolu. Lütfen başka bir zaman seçin.",
                    Assert.IsType<BusinessRuleException>(r.Error).Message));
            Assert.Equal(1, await database.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Randevular WHERE DoktorID = 1 AND RandevuTarihi = @slot AND Durum = 0",
                ct,
                ("@slot", slot)));
        }
    }

    [Fact]
    public async Task Database_RejectsADuplicateActiveSlotInsertedPastTheServices()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        const string Insert = "INSERT dbo.Randevular (HastaID, DoktorID, RandevuTarihi, Durum) VALUES (@patient, 1, '2026-03-03T10:00:00', @status)";
        await database.ExecuteAsync(Insert, ct, ("@patient", 1), ("@status", 0));
        await database.ExecuteAsync(Insert, ct, ("@patient", 2), ("@status", 1)); // cancelled rows do not count

        var duplicate = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(Insert, ct, ("@patient", 2), ("@status", 0)));
        Assert.Contains("IX_Randevular_DoktorID_RandevuTarihi", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ConcurrentFailures_AreAllCountedAndLockTheAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var auth = provider.GetRequiredService<IAuthService>();

        using var start = new ManualResetEventSlim(false);
        var attempts = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(
                async () =>
                {
                    start.Wait(ct);
                    await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInPatientAsync("10000000450", "yanlis", ct));
                },
                ct))
            .ToList();
        start.Set();
        await Task.WhenAll(attempts);

        // A read-modify-write counter would lose increments here and leave the account open.
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Hastalar WHERE TC = N'10000000450' AND KilitBitis IS NOT NULL AND BasarisizGiris = 0",
            ct));
        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInPatientAsync("10000000450", "Demo.Hasta1", ct));
        Assert.Equal(AuthService.PatientCredentialsInvalid, locked.Message);
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Hastalar WHERE TC = N'10000000450' AND KilitBitis IS NOT NULL AND BasarisizGiris = 0",
            ct));
    }

    /// <summary>
    /// Two application instances sign in to an account with four failures: B with the right password,
    /// A with a wrong one. B is held after its password check, A's fifth failure locks the account,
    /// then B continues. B must be refused and the lock must stay.
    /// </summary>
    [Theory]
    [InlineData(AccountRole.Patient)]
    [InlineData(AccountRole.Doctor)]
    public async Task SignIn_SuccessRacingWithFifthFailure_DoesNotClearLock(AccountRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        var (table, rightPassword) = role == AccountRole.Patient ? ("Hastalar", "Demo.Hasta1") : ("Doktorlar", "Demo.Doktor1");
        var nationalId = role == AccountRole.Patient ? "10000000450" : "10000000146";
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var instanceA = database.BuildServices();
        await instanceA.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var gate = new SuccessWriteGate();
        await using var instanceB = database.BuildServices(services => services.HoldSuccessfulSignInWrites(gate));
        await database.ExecuteAsync($"UPDATE dbo.{table} SET BasarisizGiris = 4 WHERE TC = @tc", ct, ("@tc", nationalId));
        var lockState = $"SELECT CONVERT(nvarchar(40), KilitBitis, 126) + N'/' + CONVERT(nvarchar(10), BasarisizGiris) FROM dbo.{table} WHERE TC = @tc";

        var authB = instanceB.GetRequiredService<IAuthService>();
        var signInB = Task.Run(() => SignInAsync(authB, role, rightPassword, ct), ct);
        await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), ct);

        var failureA = await Assert.ThrowsAsync<BusinessRuleException>(
            () => SignInAsync(instanceA.GetRequiredService<IAuthService>(), role, "yanlis", ct));
        var lockedByA = await database.TextAsync(lockState, ct, ("@tc", nationalId));
        Assert.NotNull(lockedByA);
        Assert.EndsWith("/0", lockedByA, StringComparison.Ordinal);

        gate.Release();
        var failureB = await Assert.ThrowsAsync<BusinessRuleException>(() => signInB);

        Assert.Equal(failureA.Message, failureB.Message);
        Assert.Equal(lockedByA, await database.TextAsync(lockState, ct, ("@tc", nationalId)));
    }

    private static Task<UserSession> SignInAsync(IAuthService auth, AccountRole role, string password, CancellationToken ct) =>
        role == AccountRole.Patient
            ? auth.SignInPatientAsync("10000000450", password, ct)
            : auth.SignInDoctorAsync("Dr. Ada Örnek", password, ct);

    private static async Task<IReadOnlyList<int>> AddPatientsAsync(SqlServerTestDatabase database, int count, CancellationToken ct)
    {
        var ids = new List<int>();
        for (var i = 0; i < count; i++)
        {
            // Format-only placeholder ids; the database does not check the checksum.
            await database.ExecuteAsync(
                "INSERT dbo.Hastalar (Ad_Soyad, TC, Sifre, SifreBicimi, BasarisizGiris) VALUES (@name, @tc, N'-', 0, 0)",
                ct,
                ("@name", "Yarış Hastası " + i),
                ("@tc", "2000000" + i.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)));
            ids.Add(await database.ScalarAsync("SELECT MAX(HastaID) FROM dbo.Hastalar", ct));
        }

        return ids;
    }
}
