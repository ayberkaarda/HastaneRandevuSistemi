using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>Doctor search, the calendar and the earliest free slot, computed in the service.</summary>
public sealed class AvailabilityTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0); // Monday
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Mock<IDoctorRepository> _doctors = new(MockBehavior.Strict);
    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly InMemoryScheduleRepository _schedules = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));
    private readonly List<DateTime> _booked = [];
    private readonly UserSession _patient = new(AccountRole.Patient, 1, "Hasta", "***");

    public AvailabilityTests()
    {
        _doctors.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new DoctorSummary(1, "Dr. Ada Örnek", "Kardiyoloji"),
            new DoctorSummary(2, "Dr. Can Deneme", "Dahiliye"),
            new DoctorSummary(3, "Dr. Ece Taslak", " ortopedi "),
            new DoctorSummary(4, "Dr. İlker Işık", "Kardiyoloji"),
            new DoctorSummary(5, "Dr. Branşsız", null),
        ]);
        _appointments.Setup(a => a.GetBookedStartsAsync(10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, DateTime from, DateTime to, CancellationToken _) =>
                (IReadOnlyList<DateTime>)_booked.Where(b => b >= from && b < to).ToList());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null, null, new[] { 1, 2, 3, 4, 5 })]
    [InlineData("  ", "", new[] { 1, 2, 3, 4, 5 })]
    [InlineData("ada", null, new[] { 1 })]
    [InlineData("ÖRNEK", null, new[] { 1 })]
    [InlineData("ilker", null, new[] { 4 })]
    [InlineData("IŞIK", null, new[] { 4 })]
    [InlineData(null, "kardiyoloji", new[] { 1, 4 })]
    [InlineData(null, "Ortopedi", new[] { 3 })]
    [InlineData(null, "ORTOPEDI", new[] { 3 })]
    [InlineData("ERNEK", null, new int[0])]
    [InlineData("can", "Kardiyoloji", new int[0])]
    [InlineData("dr.", "Dahiliye", new[] { 2 })]
    public async Task Search_FiltersByNameAndBranchWithTurkishCaseRules(string? name, string? branch, int[] expected)
    {
        var found = await Service().SearchDoctorsAsync(_patient, name, branch, Ct);

        Assert.Equal(expected, found.Select(d => d.Id));
    }

    [Fact]
    public async Task Branches_AreDistinctTrimmedAndSorted()
    {
        Assert.Equal(["Dahiliye", "Kardiyoloji", "ortopedi"], await Service().GetBranchesAsync(_patient, Ct));
    }

    [Fact]
    public async Task FirstFree_IsTheNextFreeSlotAfterNow()
    {
        _booked.Add(Now.Date.AddHours(10.5));

        Assert.Equal(Now.Date.AddHours(11), await Service().FindFirstFreeSlotAsync(_patient, 10, Ct));
    }

    [Fact]
    public async Task FirstFree_SkipsLeaveDaysAndFullDays()
    {
        // Today after 16:30 has no slot left; Tuesday is leave; Wednesday is fully booked.
        _time.SetUtcNow(new DateTimeOffset(Now.Date.AddHours(16).AddMinutes(31), TimeSpan.Zero));
        _schedules.Leave.Add((10, Today.AddDays(1)));
        _booked.AddRange(DayOf(Today.AddDays(2)).Select(h => h));

        Assert.Equal(Today.AddDays(3).ToDateTime(new TimeOnly(9, 0)), await Service().FindFirstFreeSlotAsync(_patient, 10, Ct));
    }

    [Fact]
    public async Task FirstFree_FindsDay90ButNeverDay91()
    {
        EveryDayNineToTen();
        for (var i = 0; i < 90; i++)
        {
            _booked.AddRange(DayOf(Today.AddDays(i), 9, 10));
        }

        var day90 = await Service().FindFirstFreeSlotAsync(_patient, 10, Ct);
        _schedules.Leave.Add((10, Today.AddDays(90)));
        var none = await Service().FindFirstFreeSlotAsync(_patient, 10, Ct);

        Assert.Equal(Today.AddDays(90).ToDateTime(new TimeOnly(9, 0)), day90);
        Assert.Null(none); // day 91 is free but outside the window
    }

    [Fact]
    public async Task Calendar_DescribesEveryKindOfDay()
    {
        _schedules.Leave.Add((10, Today.AddDays(2)));        // Wednesday
        _booked.AddRange(DayOf(Today.AddDays(3)));          // Thursday full
        _booked.Add(Today.AddDays(1).ToDateTime(new TimeOnly(9, 0)));

        var days = await Service().GetCalendarAsync(_patient, 10, Today.AddDays(-1), 7, Ct);

        Assert.Equal(
            [
                (Today.AddDays(-1), CalendarDayStatus.OutsideWindow, 0, 0),
                (Today, CalendarDayStatus.Available, 13, 16),        // 10:30 to 16:30 left
                (Today.AddDays(1), CalendarDayStatus.Available, 15, 16),
                (Today.AddDays(2), CalendarDayStatus.Leave, 0, 0),
                (Today.AddDays(3), CalendarDayStatus.Full, 0, 16),
                (Today.AddDays(4), CalendarDayStatus.Available, 16, 16),
                (Today.AddDays(5), CalendarDayStatus.NotWorking, 0, 0), // Saturday
            ],
            days.Select(d => (d.Date, d.Status, d.FreeSlots, d.TotalSlots)));
    }

    [Fact]
    public async Task Calendar_EndsAtTheWindowAndLimitsItsLength()
    {
        var days = await Service().GetCalendarAsync(_patient, 10, Today.AddDays(85), 10, Ct);

        Assert.Equal(CalendarDayStatus.NotWorking, days.Single(d => d.Date == Today.AddDays(90)).Status); // Sunday, day 90
        Assert.Equal(CalendarDayStatus.Available, days.Single(d => d.Date == Today.AddDays(88)).Status);
        Assert.All(days.Where(d => d.Date > Today.AddDays(90)), d => Assert.Equal(CalendarDayStatus.OutsideWindow, d.Status));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Service().GetCalendarAsync(_patient, 10, Today, 0, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Service().GetCalendarAsync(_patient, 10, Today, 43, Ct));
    }

    [Fact]
    public async Task CalendarAndFirstFree_RequireAPatient()
    {
        var doctor = new UserSession(AccountRole.Doctor, 10, "Dr.", "***");
        var audit = new List<AuditEvent>();
        var service = Service(audit);

        await Assert.ThrowsAsync<AccessDeniedException>(() => service.GetCalendarAsync(doctor, 10, Today, 7, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.FindFirstFreeSlotAsync(doctor, 10, Ct));

        Assert.Equal([AuditEvent.AccessDenied, AuditEvent.AccessDenied], audit);
    }

    private void EveryDayNineToTen() =>
        _schedules.Hours[10] = [.. Enum.GetValues<DayOfWeek>().Select(d => new WorkingDay(d, TimeSpan.FromHours(9), TimeSpan.FromHours(10)))];

    private static IEnumerable<DateTime> DayOf(DateOnly day, double from = 9, double to = 17)
    {
        for (var hour = from; hour < to; hour += 0.5)
        {
            yield return day.ToDateTime(TimeOnly.MinValue).AddHours(hour);
        }
    }

    private AvailabilityService Service(List<AuditEvent>? audit = null) =>
        new(_doctors.Object, _appointments.Object, _schedules, _time, new Recorder(audit ?? []));

    private sealed class Recorder(List<AuditEvent> events) : IAuditLog
    {
        public void Record(AuditEvent auditEvent, AccountRole role) => events.Add(auditEvent);
    }
}
