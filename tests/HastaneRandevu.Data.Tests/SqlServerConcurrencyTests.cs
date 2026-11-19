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
        Assert.Contains("dakika", locked.Message, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<int>> AddPatientsAsync(SqlServerTestDatabase database, int count, CancellationToken ct)
    {
        var ids = new List<int>();
        for (var i = 0; i < count; i++)
        {
            // Format-only placeholder ids; the database does not check the checksum.
            await database.ExecuteAsync(
                "INSERT dbo.Hastalar (Ad_Soyad, TC, Sifre, BasarisizGiris) VALUES (@name, @tc, N'-', 0)",
                ct,
                ("@name", "Yarış Hastası " + i),
                ("@tc", "2000000" + i.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)));
            ids.Add(await database.ScalarAsync("SELECT MAX(HastaID) FROM dbo.Hastalar", ct));
        }

        return ids;
    }
}
