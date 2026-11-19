using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>Doctors edit only their own schedule; existing appointments are kept and reported.</summary>
public sealed class ScheduleServiceTests
{
    // Monday 2 March 2026, 10:10; the fake clock's local time zone is UTC.
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0);
    private static readonly DateOnly Tomorrow = new(2026, 3, 3);

    private readonly InMemoryScheduleRepository _schedules = new();
    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));
    private readonly List<(AuditEvent Event, AccountRole Role)> _audit = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static UserSession Doctor(int id) => new(AccountRole.Doctor, id, "Dr. " + id, "***");

    private static UserSession Patient(int id) => new(AccountRole.Patient, id, "Hasta " + id, "***");

    private static IReadOnlyList<WorkingDay> Mornings =>
        [new WorkingDay(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(12)), new WorkingDay(DayOfWeek.Tuesday, TimeSpan.FromHours(8), TimeSpan.FromHours(12))];

    [Fact]
    public async Task Patient_CannotReadOrChangeAnySchedule()
    {
        // The patient's id equals the doctor's id: the role decides, not the number.
        var patient = Patient(10);
        var service = Service();

        await Assert.ThrowsAsync<AccessDeniedException>(() => service.GetMyScheduleAsync(patient, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.SaveWorkingDaysAsync(patient, Mornings, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.AddLeaveDayAsync(patient, Tomorrow, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.RemoveLeaveDayAsync(patient, Tomorrow, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.GetConflictsAsync(patient, Ct));

        Assert.Empty(_schedules.ReplacedFor);
        Assert.Empty(_schedules.Leave);
        Assert.Equal(5, _audit.Count(a => a == (AuditEvent.AccessDenied, AccountRole.Patient)));
    }

    /// <summary>IDOR: the service has no doctor id parameter; every write goes to the session's own account.</summary>
    [Fact]
    public async Task Doctor_ChangesOnlyTheirOwnSchedule()
    {
        _schedules.Hours[1] = [new WorkingDay(DayOfWeek.Friday, TimeSpan.FromHours(9), TimeSpan.FromHours(10))];
        NoAppointments(2);

        await Service().SaveWorkingDaysAsync(Doctor(2), Mornings, Ct);
        await Service().AddLeaveDayAsync(Doctor(2), Tomorrow, Ct);

        Assert.Equal([2], _schedules.ReplacedFor);
        Assert.Equal([(2, Tomorrow)], _schedules.Leave);
        Assert.Equal(DayOfWeek.Friday, Assert.Single(_schedules.Hours[1]).Day);

        var own = await Service().GetMyScheduleAsync(Doctor(2), Ct);
        var other = await Service().GetMyScheduleAsync(Doctor(1), Ct);
        Assert.Equal(Mornings, own.WorkingDays);
        Assert.Equal([Tomorrow], own.LeaveDays);
        Assert.Equal(DayOfWeek.Friday, Assert.Single(other.WorkingDays).Day);
        Assert.Empty(other.LeaveDays);
        Assert.Equal([(AuditEvent.ScheduleChanged, AccountRole.Doctor), (AuditEvent.ScheduleChanged, AccountRole.Doctor)], _audit);
    }

    [Fact]
    public async Task GetMySchedule_WithoutSavedHours_ShowsTheDefaultHours()
    {
        var view = await Service().GetMyScheduleAsync(Doctor(3), Ct);

        Assert.True(view.UsesDefaultHours);
        Assert.Equal(5, view.WorkingDays.Count);
        Assert.All(view.WorkingDays, d => Assert.Equal((TimeSpan.FromHours(9), TimeSpan.FromHours(17)), (d.Start, d.End)));
    }

    [Fact]
    public async Task Changes_KeepAppointmentsAndReportThoseThatNoLongerFit()
    {
        var past = Details(1, Now.Date.AddHours(9));                  // today 09:00, already started: ignored
        var onLeave = Details(2, Tomorrow.ToDateTime(TimeOnly.MinValue).AddHours(10));
        var afternoon = Details(3, Now.Date.AddDays(7).AddHours(14)); // next Monday 14:00, outside 08:00-12:00
        var morning = Details(4, Now.Date.AddDays(7).AddHours(9));    // next Monday 09:00, still fine
        _appointments.Setup(a => a.GetActiveForDoctorAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([past, onLeave, morning, afternoon]);

        var afterHours = await Service().SaveWorkingDaysAsync(Doctor(10), Mornings, Ct);
        var afterLeave = await Service().AddLeaveDayAsync(Doctor(10), Tomorrow, Ct);

        Assert.Equal([(3, ScheduleConflictReason.OutsideWorkingHours)], afterHours.Select(c => (c.Appointment.Id, c.Reason)));
        Assert.Equal(
            [(2, ScheduleConflictReason.LeaveDay), (3, ScheduleConflictReason.OutsideWorkingHours)],
            afterLeave.Select(c => (c.Appointment.Id, c.Reason)));
        Assert.Equal(afterLeave, await Service().GetConflictsAsync(Doctor(10), Ct));
        _appointments.Verify(a => a.GetActiveForDoctorAsync(It.Is<int>(id => id != 10), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidHours_AreRefusedBeforeAnythingIsWritten()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SaveWorkingDaysAsync(Doctor(1), [], Ct));

        Assert.Equal(ValidationMessages.WorkingDayRequired, error.Message);
        Assert.Empty(_schedules.ReplacedFor);
        Assert.Empty(_audit);
    }

    [Fact]
    public async Task LeaveDay_TwiceOrInThePastOrMissing_IsRefused()
    {
        NoAppointments(1);
        await Service().AddLeaveDayAsync(Doctor(1), Tomorrow, Ct);

        var twice = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().AddLeaveDayAsync(Doctor(1), Tomorrow, Ct));
        var past = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().AddLeaveDayAsync(Doctor(1), Tomorrow.AddDays(-2), Ct));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().RemoveLeaveDayAsync(Doctor(1), Tomorrow.AddDays(1), Ct));
        await Service().RemoveLeaveDayAsync(Doctor(1), Tomorrow, Ct);

        Assert.Equal(ScheduleService.LeaveDayExists, twice.Message);
        Assert.Equal(ValidationMessages.LeaveDayInPast, past.Message);
        Assert.Equal(ScheduleService.LeaveDayMissing, missing.Message);
        Assert.Empty(_schedules.Leave);
    }

    private void NoAppointments(int doctorId) =>
        _appointments.Setup(a => a.GetActiveForDoctorAsync(doctorId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

    private static AppointmentDetails Details(int id, DateTime start) =>
        new(id, 1, "Hasta", 10, "Dr. 10", "Kardiyoloji", start);

    private ScheduleService Service() =>
        ServiceFactory.Schedules(_schedules, _appointments.Object, _time, new Recorder(_audit));

    private sealed class Recorder(List<(AuditEvent, AccountRole)> events) : IAuditLog
    {
        public void Record(AuditEvent auditEvent, AccountRole role) => events.Add((auditEvent, role));
    }
}
