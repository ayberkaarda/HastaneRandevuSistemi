using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
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
    private static readonly DateTime Tuesday10 = TestServices.Tomorrow10;

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

    /// <summary>
    /// A patient has read the doctor's schedule and stops before writing; the doctor takes the day off and
    /// gets an empty conflict list. The booking that goes on afterwards must not land on the leave day.
    /// </summary>
    [Fact]
    public async Task Booking_AfterLeaveCommit_RejectsStaleSchedule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var race = await StaleScheduleRace.StartAsync(ct);
        var patientRead = race.HoldPatientAfterScheduleRead();
        await using var patient = race.Patient();
        var booking = Task.Run(() => patient.GetRequiredService<IAppointmentService>().BookAsync(TestServices.Patient(1), 1, Tuesday10, ct), ct);
        await patientRead.Reached.WaitAsync(Timeout, ct);

        var conflicts = await race.DoctorSchedule().AddLeaveDayAsync(TestServices.Doctor(1), DateOnly.FromDateTime(Tuesday10), ct);
        patientRead.Release();

        Assert.Empty(conflicts);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => booking.WaitAsync(Timeout, ct));
        Assert.Equal(ValidationMessages.InvalidSlot, refused.Message);
        Assert.Equal(0, await race.Database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular", ct));
    }

    /// <summary>The same as <see cref="Booking_AfterLeaveCommit_RejectsStaleSchedule"/> for narrowed weekly hours.</summary>
    [Fact]
    public async Task Booking_AfterWorkingHoursNarrowed_RejectsStaleSchedule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var race = await StaleScheduleRace.StartAsync(ct);
        var patientRead = race.HoldPatientAfterScheduleRead();
        await using var patient = race.Patient();
        var booking = Task.Run(() => patient.GetRequiredService<IAppointmentService>().BookAsync(TestServices.Patient(1), 1, Tuesday10, ct), ct);
        await patientRead.Reached.WaitAsync(Timeout, ct);

        var afternoons = Enumerable.Range(1, 5)
            .Select(d => new WorkingDay((DayOfWeek)d, TimeSpan.FromHours(13), TimeSpan.FromHours(17)))
            .ToList();
        var conflicts = await race.DoctorSchedule().SaveWorkingDaysAsync(TestServices.Doctor(1), afternoons, ct);
        patientRead.Release();

        Assert.Empty(conflicts);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => booking.WaitAsync(Timeout, ct));
        Assert.Equal(ValidationMessages.InvalidSlot, refused.Message);
        Assert.Equal(0, await race.Database.ScalarAsync("SELECT COUNT(*) FROM dbo.Randevular", ct));
    }

    /// <summary>A move that read the old schedule must not land on a leave day taken in the meantime.</summary>
    [Fact]
    public async Task Reschedule_AfterLeaveCommit_RejectsStaleSchedule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var race = await StaleScheduleRace.StartAsync(ct);
        var source = Tuesday10.AddDays(1);
        int id;
        await using (var setup = race.Patient())
        {
            id = await setup.GetRequiredService<IAppointmentService>().BookAsync(TestServices.Patient(1), 1, source, ct);
        }

        var patientRead = race.HoldPatientAfterScheduleRead();
        await using var patient = race.Patient();
        var target = Tuesday10.AddDays(2);
        var move = Task.Run(() => patient.GetRequiredService<IAppointmentService>().RescheduleAsync(TestServices.Patient(1), id, target, ct), ct);
        await patientRead.Reached.WaitAsync(Timeout, ct);

        var conflicts = await race.DoctorSchedule().AddLeaveDayAsync(TestServices.Doctor(1), DateOnly.FromDateTime(target), ct);
        patientRead.Release();

        Assert.Empty(conflicts);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => move.WaitAsync(Timeout, ct));
        Assert.Equal(ValidationMessages.InvalidSlot, refused.Message);
        Assert.Equal(1, await race.Database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Randevular WHERE RandevuID = @id AND RandevuTarihi = @start AND Durum = 0", ct, ("@id", id), ("@start", source)));
    }

    /// <summary>
    /// The other order: the booking has passed its schedule check and stops right before its insert while
    /// the doctor takes that day off. The doctor waits for the booking, so the booking is written and the
    /// doctor's conflict list reports it.
    /// </summary>
    [Fact]
    public async Task LeaveDay_WhileBookingWrites_ReportsTheBookingAsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var race = await StaleScheduleRace.StartAsync(ct);
        var insert = CommandPoint.Before(sql => CommandPoint.IsInsertInto(sql, "Randevular"));
        await using var patient = race.Database.BuildServices(demo: false, interceptors: insert);
        var doctorLocking = CommandPoint.Observe(CommandPoint.IsAppLock);
        await using var doctor = race.Database.BuildServices(demo: false, interceptors: doctorLocking);

        var booking = Task.Run(() => patient.GetRequiredService<IAppointmentService>().BookAsync(TestServices.Patient(1), 1, Tuesday10, ct), ct);
        await insert.Reached.WaitAsync(Timeout, ct);
        var leave = Task.Run(
            () => doctor.GetRequiredService<IScheduleService>().AddLeaveDayAsync(TestServices.Doctor(1), DateOnly.FromDateTime(Tuesday10), ct),
            ct);
        await Task.WhenAny(doctorLocking.Reached, leave).WaitAsync(Timeout, ct);
        insert.Release();

        var id = await booking.WaitAsync(Timeout, ct);
        var conflict = Assert.Single(await leave.WaitAsync(Timeout, ct));
        Assert.Equal((id, ScheduleConflictReason.LeaveDay), (conflict.Appointment.Id, conflict.Reason));
    }

    /// <summary>
    /// Bookings, moves, leave changes and weekly hour changes of two doctors all at once, several rounds.
    /// Every transaction takes one doctor lock as its first statement, so none may fail with a deadlock or
    /// a lock timeout; the only allowed refusals are business rules (slot taken, not a working slot, ...).
    /// </summary>
    [Fact]
    public async Task MixedScheduleAndAppointmentWrites_NeverDeadlock()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using (var setup = database.BuildServices())
        {
            await setup.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        }

        var instances = Enumerable.Range(0, 6).Select(_ => database.BuildServices(demo: false)).ToList();
        try
        {
            var appointmentsOf = instances.Select(i => i.GetRequiredService<IAppointmentService>()).ToList();
            var schedulesOf = instances.Select(i => i.GetRequiredService<IScheduleService>()).ToList();
            var booked = new List<(int Patient, int Id)>();
            for (var round = 0; round < 3; round++)
            {
                var week = Tuesday10.AddDays(7 * round);
                var attempts = new List<Func<Task>>();
                foreach (var doctorId in new[] { 1, 2 })
                {
                    var doctor = TestServices.Doctor(doctorId);
                    for (var i = 0; i < 6; i++)
                    {
                        var service = appointmentsOf[i];
                        var patient = TestServices.Patient(1 + (i % 2));
                        var slot = week.AddHours(i % 3).AddDays(i % 2);
                        attempts.Add(async () =>
                        {
                            var id = await service.BookAsync(patient, doctorId, slot, ct);
                            lock (booked)
                            {
                                booked.Add((patient.AccountId, id));
                            }
                        });
                    }

                    foreach (var (patientId, id) in booked.ToList())
                    {
                        attempts.Add(() => appointmentsOf[id % 6].RescheduleAsync(TestServices.Patient(patientId), id, week.AddDays(2).AddHours(id % 4), ct));
                    }

                    attempts.Add(() => schedulesOf[0].AddLeaveDayAsync(doctor, DateOnly.FromDateTime(week.AddDays(1)), ct));
                    attempts.Add(() => schedulesOf[1].RemoveLeaveDayAsync(doctor, DateOnly.FromDateTime(week.AddDays(1)), ct));
                    attempts.Add(() => schedulesOf[2].SaveWorkingDaysAsync(
                        doctor,
                        [new WorkingDay(DayOfWeek.Tuesday, TimeSpan.FromHours(9), TimeSpan.FromHours(11)), new WorkingDay(DayOfWeek.Thursday, TimeSpan.FromHours(9), TimeSpan.FromHours(17))],
                        ct));
                    attempts.Add(() => schedulesOf[3].SaveWorkingDaysAsync(
                        doctor,
                        [.. Enumerable.Range(1, 5).Select(d => new WorkingDay((DayOfWeek)d, TimeSpan.FromHours(9), TimeSpan.FromHours(17)))],
                        ct));
                    attempts.Add(() => schedulesOf[4].GetConflictsAsync(doctor, ct));
                }

                using var start = new ManualResetEventSlim(false);
                var tasks = attempts.Select(attempt => Task.Run(
                    async () =>
                    {
                        start.Wait(ct);
                        try
                        {
                            await attempt();
                            return (Exception?)null;
                        }
#pragma warning disable CA1031 // Collected and asserted below.
                        catch (Exception ex)
#pragma warning restore CA1031
                        {
                            return ex;
                        }
                    },
                    ct)).ToList();
                start.Set();
                var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromMinutes(2), ct);

                Assert.All(results.Where(r => r is not null), r => Assert.IsType<BusinessRuleException>(r));
            }
        }
        finally
        {
            foreach (var instance in instances)
            {
                await instance.DisposeAsync();
            }
        }
    }

    /// <summary>A database with the demo accounts, a patient side that can be held, and a doctor side.</summary>
    private sealed class StaleScheduleRace : IAsyncDisposable
    {
        private CommandPoint? _patientRead;

        private StaleScheduleRace(SqlServerTestDatabase database, ServiceProvider doctor)
        {
            Database = database;
            DoctorInstance = doctor;
        }

        public SqlServerTestDatabase Database { get; }

        private ServiceProvider DoctorInstance { get; }

        public static async Task<StaleScheduleRace> StartAsync(CancellationToken ct)
        {
            var database = await SqlServerTestDatabase.ReserveAsync(ct);
            await using (var setup = database.BuildServices())
            {
                await setup.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
            }

            return new StaleScheduleRace(database, database.BuildServices(demo: false));
        }

        /// <summary>The next patient instance stops right after the service read the doctor's leave days.</summary>
        public CommandPoint HoldPatientAfterScheduleRead() =>
            _patientRead = CommandPoint.After(sql => sql.Contains("[DoktorIzinGunleri]", StringComparison.Ordinal));

        public ServiceProvider Patient()
        {
            var point = _patientRead;
            _patientRead = null;
            return point is null ? Database.BuildServices(demo: false) : Database.BuildServices(demo: false, interceptors: point);
        }

        public IScheduleService DoctorSchedule() => DoctorInstance.GetRequiredService<IScheduleService>();

        public async ValueTask DisposeAsync()
        {
            await DoctorInstance.DisposeAsync();
            await Database.DisposeAsync();
        }
    }
}
