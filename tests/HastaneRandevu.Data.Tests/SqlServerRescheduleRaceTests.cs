using System.Globalization;
using HastaneRandevu.Business;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Data.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// Moving appointments under concurrency on a real SQL Server instance (see
/// <see cref="SqlServerTestDatabase"/>). Every contender runs in its own application instance.
/// </summary>
public sealed class SqlServerRescheduleRaceTests
{
    private const int Contenders = 12;
    private const string TakenMessage = "Bu randevu saatinde doktor zaten dolu. Lütfen başka bir zaman seçin.";

    /// <summary>Many patients move their own appointments into the same free slot: exactly one wins.</summary>
    [Fact]
    public async Task Reschedule_ManyPatientsMoveIntoOneSlot_ExactlyOneWinsAndNoAppointmentIsLost()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var patients = await AddPatientsAsync(database, Contenders, ct);
        var service = provider.GetRequiredService<IAppointmentService>();

        for (var round = 0; round < 4; round++)
        {
            // Each patient holds a slot on Wednesday; all of them race for one slot on Thursday.
            var day = new DateTime(2026, 3, 4).AddDays(round * 7);
            var target = day.AddDays(1).AddHours(10);
            var owned = new List<(int Patient, int Appointment, DateTime Start)>();
            for (var i = 0; i < patients.Count; i++)
            {
                var start = day.AddHours(9).AddMinutes(30 * i);
                owned.Add((patients[i], await service.BookAsync(TestServices.Patient(patients[i]), 1, start, ct), start));
            }

            var results = await RaceAsync(owned.Select(o => (Func<Task>)(() => service.RescheduleAsync(TestServices.Patient(o.Patient), o.Appointment, target, ct))), ct);

            Assert.Equal(1, results.Count(r => r is null));
            Assert.All(results.Where(r => r is not null), r => Assert.Equal(TakenMessage, Assert.IsType<BusinessRuleException>(r).Message));
            Assert.Equal(1, await database.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Randevular WHERE DoktorID = 1 AND RandevuTarihi = @target AND Durum = 0", ct, ("@target", target)));

            // The losers keep their original slot; no appointment was cancelled or duplicated.
            for (var i = 0; i < owned.Count; i++)
            {
                var expected = results[i] is null ? target : owned[i].Start;
                Assert.Equal(1, await database.ScalarAsync(
                    "SELECT COUNT(*) FROM dbo.Randevular WHERE RandevuID = @id AND RandevuTarihi = @start AND Durum = 0",
                    ct,
                    ("@id", owned[i].Appointment),
                    ("@start", expected)));
            }

            Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular WHERE Durum <> 0", ct));
        }
    }

    /// <summary>Half of the contenders book the slot, the other half move an existing appointment into it.</summary>
    [Fact]
    public async Task RescheduleAndBooking_RaceForOneSlot_ExactlyOneWins()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var setup = database.BuildServices();
        await setup.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var patients = await AddPatientsAsync(database, Contenders, ct);

        for (var round = 0; round < 4; round++)
        {
            var day = new DateTime(2026, 3, 4).AddDays(round * 7);
            var target = day.AddDays(1).AddHours(14);
            var attempts = new List<Func<Task>>();
            var providers = new List<ServiceProvider>();
            try
            {
                for (var i = 0; i < patients.Count; i++)
                {
                    var instance = database.BuildServices();
                    providers.Add(instance);
                    var service = instance.GetRequiredService<IAppointmentService>();
                    var patient = TestServices.Patient(patients[i]);
                    if (i % 2 == 0)
                    {
                        var id = await service.BookAsync(patient, 1, day.AddHours(9).AddMinutes(30 * i), ct);
                        attempts.Add(() => service.RescheduleAsync(patient, id, target, ct));
                    }
                    else
                    {
                        attempts.Add(() => service.BookAsync(patient, 1, target, ct));
                    }
                }

                var results = await RaceAsync(attempts, ct);

                Assert.Equal(1, results.Count(r => r is null));
                Assert.All(results.Where(r => r is not null), r => Assert.Equal(TakenMessage, Assert.IsType<BusinessRuleException>(r).Message));
                Assert.Equal(1, await database.ScalarAsync(
                    "SELECT COUNT(*) FROM dbo.Randevular WHERE DoktorID = 1 AND RandevuTarihi = @target AND Durum = 0", ct, ("@target", target)));
                // Every moving contender that lost still has its Wednesday appointment.
                var moverWon = results.Where((r, i) => r is null && i % 2 == 0).Any();
                Assert.Equal(patients.Count / 2 - (moverWon ? 1 : 0), await database.ScalarAsync(
                    "SELECT COUNT(*) FROM dbo.Randevular WHERE DoktorID = 1 AND RandevuTarihi >= @from AND RandevuTarihi < @to AND Durum = 0",
                    ct,
                    ("@from", day),
                    ("@to", day.AddDays(1))));
                Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular WHERE Durum <> 0", ct));
            }
            finally
            {
                foreach (var instance in providers)
                {
                    await instance.DisposeAsync();
                }
            }
        }
    }

    /// <summary>
    /// Two sessions of the same patient read the same appointment and then move it to two different free
    /// slots. Both reads finish before either move starts, so the second move must notice that the
    /// appointment is no longer where it read it, instead of silently moving the first session's result.
    /// </summary>
    [Fact]
    public async Task Reschedule_SameSourceDifferentTargets_OnlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var setup = database.BuildServices();
        await setup.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var patient = TestServices.Patient(1);
        var source = new DateTime(2026, 3, 4, 10, 0, 0);
        var id = await setup.GetRequiredService<IAppointmentService>().BookAsync(patient, 1, source, ct);
        DateTime[] targets = [new(2026, 3, 5, 10, 0, 0), new(2026, 3, 5, 11, 0, 0)];

        var bothRead = RepositoryHooks.Barrier(2);
        var audit = new List<AuditEvent>();
        await using var first = database.BuildServices(s => s.AfterAppointmentRead(bothRead).RecordAudit(audit), demo: false);
        await using var second = database.BuildServices(s => s.AfterAppointmentRead(bothRead).RecordAudit(audit), demo: false);
        var sessions = new[] { first, second }.Select(p => p.GetRequiredService<IAppointmentService>()).ToList();

        var results = await RaceAsync(
            [() => sessions[0].RescheduleAsync(patient, id, targets[0], ct), () => sessions[1].RescheduleAsync(patient, id, targets[1], ct)],
            ct);

        Assert.Equal(1, results.Count(r => r is null));
        var loser = Assert.IsType<BusinessRuleException>(Assert.Single(results, r => r is not null));
        Assert.Equal(AppointmentService.RescheduleFailed, loser.Message);
        var winnerTarget = targets[Array.FindIndex(results, r => r is null)];
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Randevular WHERE RandevuID = @id AND RandevuTarihi = @start AND Durum = 0", ct, ("@id", id), ("@start", winnerTarget)));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular", ct));
        Assert.Equal([AuditEvent.AppointmentRescheduled], audit);
    }

    /// <summary>Starts all attempts at once; returns the exception of each attempt, or null for the winner.</summary>
    private static async Task<Exception?[]> RaceAsync(IEnumerable<Func<Task>> attempts, CancellationToken ct)
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = attempts
            .Select(attempt => Task.Run(
                async () =>
                {
                    start.Wait(ct);
                    try
                    {
                        await attempt();
                        return (Exception?)null;
                    }
#pragma warning disable CA1031 // Collected and asserted by the caller.
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        return ex;
                    }
                },
                ct))
            .ToList();
        start.Set();
        return await Task.WhenAll(tasks);
    }

    private static async Task<IReadOnlyList<int>> AddPatientsAsync(SqlServerTestDatabase database, int count, CancellationToken ct)
    {
        var ids = new List<int>();
        for (var i = 0; i < count; i++)
        {
            // Format-only placeholder ids; the database does not check the checksum.
            await database.ExecuteAsync(
                "INSERT dbo.Hastalar (Ad_Soyad, TC, Sifre, SifreBicimi, BasarisizGiris) VALUES (@name, @tc, N'-', 0, 0)",
                ct,
                ("@name", "Erteleme Hastası " + i),
                ("@tc", "3000000" + i.ToString("0000", CultureInfo.InvariantCulture)));
            ids.Add(await database.ScalarAsync("SELECT MAX(HastaID) FROM dbo.Hastalar", ct));
        }

        return ids;
    }
}
