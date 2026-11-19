using HastaneRandevu.Business.Services;

namespace HastaneRandevu.Data.Tests;

/// <summary>Search, calendar and first free slot with the real repositories on SQLite in-memory.</summary>
public sealed class SqliteAvailabilityTests : IAsyncLifetime
{
    // The fake clock starts on Monday 2 March 2026, 10:10.
    private static readonly DateOnly Today = new(2026, 3, 2);

    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAvailabilityService Availability => _db.Get<IAvailabilityService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Search_FindsTheDemoDoctorsByNameAndBranch()
    {
        var patient = TestServices.Patient(1);

        Assert.Equal([1], (await Availability.SearchDoctorsAsync(patient, "örnek", null, Ct)).Select(d => d.Id));
        Assert.Equal([3], (await Availability.SearchDoctorsAsync(patient, null, "ORTOPEDI", Ct)).Select(d => d.Id));
        Assert.Equal(["Dahiliye", "Kardiyoloji", "Ortopedi"], await Availability.GetBranchesAsync(patient, Ct));
    }

    [Fact]
    public async Task FirstFreeAndCalendar_FollowBookingsAndLeaveDays()
    {
        var patient = TestServices.Patient(1);
        var appointments = _db.Get<IAppointmentService>();
        for (var slot = Today.ToDateTime(new TimeOnly(10, 30)); slot.Hour < 17; slot = slot.AddMinutes(30))
        {
            await appointments.BookAsync(patient, 1, slot, Ct); // the rest of today is full
        }

        await _db.Get<IScheduleService>().AddLeaveDayAsync(TestServices.Doctor(1), Today.AddDays(1), Ct);

        Assert.Equal(Today.AddDays(2).ToDateTime(new TimeOnly(9, 0)), await Availability.FindFirstFreeSlotAsync(patient, 1, Ct));
        Assert.Equal(Today.ToDateTime(new TimeOnly(10, 30)), await Availability.FindFirstFreeSlotAsync(patient, 2, Ct));
        var week = await Availability.GetCalendarAsync(patient, 1, Today, 3, Ct);
        Assert.Equal([CalendarDayStatus.Full, CalendarDayStatus.Leave, CalendarDayStatus.Available], week.Select(d => d.Status));
    }
}
