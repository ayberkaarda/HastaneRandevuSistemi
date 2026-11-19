using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Tests;

/// <summary>Doctor schedules with the real repositories and services on SQLite in-memory.</summary>
public sealed class SqliteScheduleTests : IAsyncLifetime
{
    // The fake clock starts on Monday 2 March 2026, 10:10.
    private static readonly DateTime Tuesday = new(2026, 3, 3);
    private static readonly DateTime Saturday = new(2026, 3, 7);

    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IScheduleService Schedules => _db.Get<IScheduleService>();

    private IAppointmentService Appointments => _db.Get<IAppointmentService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task NewDatabase_DoctorsWorkTheDefaultHours()
    {
        var view = await Schedules.GetMyScheduleAsync(TestServices.Doctor(1), Ct);
        var slots = await Appointments.GetAvailableSlotsAsync(TestServices.Patient(1), 1, Tuesday, Ct);

        Assert.True(view.UsesDefaultHours);
        Assert.Equal(16, slots.Count);
        Assert.Equal(Tuesday.AddHours(9), slots[0]);
        Assert.Equal(Tuesday.AddHours(16.5), slots[^1]);
    }

    [Fact]
    public async Task SavedHoursAndLeaveDays_RoundTripAndDriveTheSlots()
    {
        var doctor = TestServices.Doctor(1);
        await Schedules.SaveWorkingDaysAsync(
            doctor,
            [
                new WorkingDay(DayOfWeek.Saturday, TimeSpan.FromHours(10), TimeSpan.FromHours(12)),
                new WorkingDay(DayOfWeek.Tuesday, TimeSpan.Zero, TimeSpan.FromDays(1)),
            ],
            Ct);
        await Schedules.AddLeaveDayAsync(doctor, DateOnly.FromDateTime(Saturday), Ct);

        var view = await Schedules.GetMyScheduleAsync(doctor, Ct);
        Assert.False(view.UsesDefaultHours);
        Assert.Equal([DayOfWeek.Tuesday, DayOfWeek.Saturday], view.WorkingDays.Select(d => d.Day));
        Assert.Equal(TimeSpan.FromDays(1), view.WorkingDays[0].End);
        Assert.Equal([DateOnly.FromDateTime(Saturday)], view.LeaveDays);

        var patient = TestServices.Patient(1);
        Assert.Equal(48, (await Appointments.GetAvailableSlotsAsync(patient, 1, Tuesday, Ct)).Count);
        Assert.Empty(await Appointments.GetAvailableSlotsAsync(patient, 1, Saturday, Ct));
        Assert.Equal(4, (await Appointments.GetAvailableSlotsAsync(patient, 1, Saturday.AddDays(7), Ct)).Count);
        Assert.Empty(await Appointments.GetAvailableSlotsAsync(patient, 1, Tuesday.AddDays(1), Ct));

        // Saving again replaces the hours instead of adding to them.
        await Schedules.SaveWorkingDaysAsync(doctor, [new WorkingDay(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(9))], Ct);
        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.WorkingHours.CountAsync(h => h.DoctorId == 1, Ct));
    }

    /// <summary>IDOR on the real database: a doctor's changes never touch another doctor's rows.</summary>
    [Fact]
    public async Task Doctor_CannotChangeAnotherDoctorsSchedule()
    {
        await Schedules.SaveWorkingDaysAsync(TestServices.Doctor(2), [new WorkingDay(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(9))], Ct);
        await Schedules.AddLeaveDayAsync(TestServices.Doctor(2), DateOnly.FromDateTime(Tuesday), Ct);

        await Assert.ThrowsAsync<AccessDeniedException>(() => Schedules.AddLeaveDayAsync(TestServices.Patient(1), DateOnly.FromDateTime(Tuesday), Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Schedules.RemoveLeaveDayAsync(TestServices.Doctor(1), DateOnly.FromDateTime(Tuesday), Ct));

        await using var context = _db.CreateContext();
        Assert.Equal([2], await context.WorkingHours.Select(h => h.DoctorId).Distinct().ToListAsync(Ct));
        Assert.Equal([2], await context.LeaveDays.Select(l => l.DoctorId).ToListAsync(Ct));
        Assert.True((await Schedules.GetMyScheduleAsync(TestServices.Doctor(1), Ct)).UsesDefaultHours);
        Assert.Equal(16, (await Appointments.GetAvailableSlotsAsync(TestServices.Patient(1), 1, Tuesday, Ct)).Count);
    }

    [Fact]
    public async Task LeaveDay_KeepsExistingAppointmentsAndListsThem()
    {
        var patient = TestServices.Patient(1);
        var id = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10, Ct);

        var conflicts = await Schedules.AddLeaveDayAsync(TestServices.Doctor(1), DateOnly.FromDateTime(Tuesday), Ct);

        var conflict = Assert.Single(conflicts);
        Assert.Equal((id, ScheduleConflictReason.LeaveDay), (conflict.Appointment.Id, conflict.Reason));
        Assert.Equal([id], (await Appointments.GetMyAppointmentsAsync(patient, Ct)).Select(a => a.Id));
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments.BookAsync(patient, 1, TestServices.Tomorrow10.AddHours(1), Ct));
        Assert.Equal("Lütfen çalışma saatleri içinde geçerli bir randevu saati seçiniz.", refused.Message);
    }

    [Fact]
    public async Task Database_RejectsInvalidHoursAndDuplicateLeaveInsertedPastTheServices()
    {
        var repository = _db.Get<IDoctorScheduleRepository>();
        await using var context = _db.CreateContext();

        context.WorkingHours.Add(new DoctorWorkingHours { DoctorId = 1, DayOfWeek = DayOfWeek.Monday, StartMinute = 600, EndMinute = 540 });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        context.ChangeTracker.Clear();
        context.WorkingHours.Add(new DoctorWorkingHours { DoctorId = 1, DayOfWeek = DayOfWeek.Monday, StartMinute = 545, EndMinute = 600 });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        context.ChangeTracker.Clear();

        Assert.True(await repository.AddLeaveDayAsync(1, DateOnly.FromDateTime(Tuesday), Ct));
        Assert.False(await repository.AddLeaveDayAsync(1, DateOnly.FromDateTime(Tuesday), Ct));
        context.LeaveDays.Add(new DoctorLeaveDay { DoctorId = 1, Date = DateOnly.FromDateTime(Tuesday) });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
    }
}
