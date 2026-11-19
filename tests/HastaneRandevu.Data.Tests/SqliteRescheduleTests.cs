using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Tests;

/// <summary>Moving appointments with the real repository on SQLite in-memory.</summary>
public sealed class SqliteRescheduleTests : IAsyncLifetime
{
    // The fake clock starts on Monday 2 March 2026, 10:10; the default cut-off is 2 hours.
    private static readonly DateTime Target = new(2026, 3, 4, 11, 0, 0);

    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAppointmentService Appointments => _db.Get<IAppointmentService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Reschedule_MovesTheSameRowAndFreesTheOldSlot()
    {
        var patient = TestServices.Patient(1);
        var id = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10, Ct);

        await Appointments.RescheduleAsync(patient, id, Target, Ct);

        await using var context = _db.CreateContext();
        var row = await context.Appointments.SingleAsync(Ct);
        Assert.Equal((id, Target, AppointmentStatus.Active), (row.Id, row.StartsAt, row.Status));
        Assert.Contains(TestServices.Tomorrow10, await Appointments.GetAvailableSlotsAsync(patient, 1, TestServices.Tomorrow10.Date, Ct));
        Assert.DoesNotContain(Target, await Appointments.GetAvailableSlotsAsync(patient, 1, Target.Date, Ct));

        // Another patient can now book the old slot.
        await Appointments.BookAsync(TestServices.Patient(2), 1, TestServices.Tomorrow10, Ct);
    }

    [Fact]
    public async Task Reschedule_IntoATakenSlot_LeavesTheAppointmentWhereItWas()
    {
        var mine = await Appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10, Ct);
        await Appointments.BookAsync(TestServices.Patient(2), 1, Target, Ct);

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments.RescheduleAsync(TestServices.Patient(1), mine, Target, Ct));

        Assert.Equal("Bu randevu saatinde doktor zaten dolu. Lütfen başka bir zaman seçin.", refused.Message);
        await using var context = _db.CreateContext();
        Assert.Equal(TestServices.Tomorrow10, (await context.Appointments.SingleAsync(a => a.Id == mine, Ct)).StartsAt);
    }

    [Fact]
    public async Task Reschedule_ByAnotherPatient_IsRefusedAndChangesNothing()
    {
        var id = await Appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10, Ct);

        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.RescheduleAsync(TestServices.Patient(2), id, Target, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.RescheduleAsync(TestServices.Patient(1), id + 100, Target, Ct));

        await using var context = _db.CreateContext();
        Assert.Equal(TestServices.Tomorrow10, (await context.Appointments.SingleAsync(Ct)).StartsAt);
    }

    [Fact]
    public async Task Reschedule_AfterTheCutoff_IsRefusedAndTheClockDecides()
    {
        var patient = TestServices.Patient(1);
        var first = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10, Ct);
        var second = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10.AddMinutes(30), Ct);

        // Exactly 2 hours before the start the move is still allowed ...
        _db.Clock.SetUtcNow(new DateTimeOffset(TestServices.Tomorrow10.AddHours(-2), TimeSpan.Zero));
        await Appointments.RescheduleAsync(patient, first, Target, Ct);

        // ... one minute less than 2 hours before it is not.
        _db.Clock.SetUtcNow(new DateTimeOffset(TestServices.Tomorrow10.AddMinutes(30).AddHours(-2).AddMinutes(1), TimeSpan.Zero));
        var late = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments.RescheduleAsync(patient, second, Target.AddMinutes(30), Ct));
        Assert.Equal("Randevu en geç başlamadan 2 saat öncesine kadar ertelenebilir.", late.Message);
    }

    /// <summary>The repository repeats every condition, so a caller that skipped the service checks gets nothing.</summary>
    [Fact]
    public async Task Repository_RepeatsOwnerStatusAndTimeConditions()
    {
        var id = await Appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10, Ct);
        var repository = _db.Get<IAppointmentRepository>();
        var now = TestServices.Start.DateTime;

        Assert.False(await repository.RescheduleAsync(id, 2, TestServices.Tomorrow10, Target, now, now, Ct));                                  // not the owner
        Assert.False(await repository.RescheduleAsync(id, 1, TestServices.Tomorrow10, Target, TestServices.Tomorrow10, now, Ct));              // started
        Assert.False(await repository.RescheduleAsync(id, 1, TestServices.Tomorrow10, Target, now, TestServices.Tomorrow10.AddTicks(1), Ct));  // past the cut-off
        Assert.False(await repository.RescheduleAsync(id + 1, 1, TestServices.Tomorrow10, Target, now, now, Ct));
        Assert.False(await repository.RescheduleAsync(id, 1, Target.AddHours(1), Target, now, now, Ct));                         // moved meanwhile                              // missing
        await Appointments.CancelAsync(TestServices.Patient(1), id, Ct);
        Assert.False(await repository.RescheduleAsync(id, 1, TestServices.Tomorrow10, Target, now, now, Ct));                                  // cancelled

        await using var context = _db.CreateContext();
        Assert.Equal(TestServices.Tomorrow10, (await context.Appointments.SingleAsync(Ct)).StartsAt);
    }
}
