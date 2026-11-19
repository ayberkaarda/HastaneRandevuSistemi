using HastaneRandevu.Business.Services;
using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// Schedule changes racing each other and racing bookings on a real SQL Server instance (see
/// <see cref="SqlServerTestDatabase"/>). Each side runs in its own application instance; the
/// interleavings are forced with <see cref="CommandPoint"/>s, not with timing.
/// </summary>
public sealed class SqlServerScheduleRaceTests
{
    private const string WorkingHoursTable = "DoktorCalismaSaatleri";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Two first saves of a doctor without own hours. The first one stops right after its delete; the
    /// second one is let run until it has deleted as well or is waiting for the doctor's lock. Without
    /// serialization both deletes find nothing and the two inserts leave the union of both schedules.
    /// </summary>
    [Fact]
    public async Task ReplaceWorkingDays_ConcurrentFirstSave_DoesNotUnionSchedules()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using (var setup = database.BuildServices())
        {
            await setup.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        }

        var firstDeleted = CommandPoint.After(sql => CommandPoint.IsDeleteFrom(sql, WorkingHoursTable));
        var secondDeleted = CommandPoint.After(sql => CommandPoint.IsDeleteFrom(sql, WorkingHoursTable));
        var secondLocking = CommandPoint.Observe(CommandPoint.IsAppLock);
        await using var first = database.BuildServices(demo: false, interceptors: firstDeleted);
        await using var second = database.BuildServices(demo: false, interceptors: [secondDeleted, secondLocking]);
        var doctor = TestServices.Doctor(1);
        WorkingDay[] monday = [new(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(12))];
        WorkingDay[] tuesday = [new(DayOfWeek.Tuesday, TimeSpan.FromHours(13), TimeSpan.FromHours(17))];

        var saveFirst = Task.Run(() => first.GetRequiredService<IScheduleService>().SaveWorkingDaysAsync(doctor, monday, ct), ct);
        await firstDeleted.Reached.WaitAsync(Timeout, ct);
        var saveSecond = Task.Run(() => second.GetRequiredService<IScheduleService>().SaveWorkingDaysAsync(doctor, tuesday, ct), ct);
        await Task.WhenAny(secondDeleted.Reached, secondLocking.Reached).WaitAsync(Timeout, ct);
        firstDeleted.Release();
        secondDeleted.Release();
        await Task.WhenAll(saveFirst, saveSecond).WaitAsync(Timeout, ct);

        var days = await first.GetRequiredService<IScheduleService>().GetMyScheduleAsync(doctor, ct);
        Assert.Equal([DayOfWeek.Tuesday], days.WorkingDays.Select(d => d.Day));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.DoktorCalismaSaatleri WHERE DoktorID = 1", ct));
    }
}
