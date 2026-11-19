using HastaneRandevu.Business;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// Edges of the lockout, the password format upgrade, the slot grid, the booking window, the
/// cancellation cut-off and ownership with the real repositories on SQLite in-memory.
/// </summary>
public sealed class SqliteBoundaryTests : IAsyncLifetime
{
    private const string SeedPatientId = "10000000450";
    private const string SeedPatientPassword = "Demo.Hasta1";
    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAuthService Auth => _db.Get<IAuthService>();

    private IAppointmentService Appointments => _db.Get<IAppointmentService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Lockout_FourFailuresAreCountedWithoutALock()
    {
        await FailAsync(LoginPolicy.MaxFailedAttempts - 1);

        var (count, lockoutEnd) = await LockStateAsync();
        Assert.Equal(4, count);
        Assert.Null(lockoutEnd);

        await Auth.SignInPatientAsync(SeedPatientId, SeedPatientPassword, Ct);
        Assert.Equal((0, null), await LockStateAsync());
    }

    [Fact]
    public async Task Lockout_EndsExactlyFifteenMinutesAfterTheFifthFailure()
    {
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++)
        {
            await FailAsync(1);
            _db.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        // The fifth failure happened one minute ago.
        var fifth = _db.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1);
        var (_, lockoutEnd) = await LockStateAsync();
        Assert.Equal(fifth + LoginPolicy.LockoutDuration, lockoutEnd);

        _db.Clock.Advance(LoginPolicy.LockoutDuration - TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync(SeedPatientId, SeedPatientPassword, Ct));

        _db.Clock.Advance(TimeSpan.FromSeconds(1));
        await Auth.SignInPatientAsync(SeedPatientId, SeedPatientPassword, Ct);
        Assert.Equal((0, null), await LockStateAsync());
    }

    [Fact]
    public async Task Lockout_AttemptsDuringTheLockNeitherExtendNorCountAgainstIt()
    {
        await FailAsync(LoginPolicy.MaxFailedAttempts);
        var locked = await LockStateAsync();

        _db.Clock.Advance(TimeSpan.FromMinutes(10));
        await FailAsync(3);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync(SeedPatientId, SeedPatientPassword, Ct));

        Assert.Equal(locked, await LockStateAsync());
        _db.Clock.Advance(TimeSpan.FromMinutes(5));
        await Auth.SignInPatientAsync(SeedPatientId, SeedPatientPassword, Ct);
    }

    [Fact]
    public async Task PasswordUpgrade_HashWithFewerIterationsIsReplacedOnSignIn()
    {
        var weaker = new PasswordHasher(iterations: 1_000).Hash(SeedPatientPassword);
        await SetStoredPasswordAsync(weaker, PasswordFormat.Pbkdf2);

        await AuthWithHasher(new PasswordHasher(iterations: 2_000)).SignInPatientAsync(SeedPatientId, SeedPatientPassword, Ct);

        var (hash, format) = await StoredPasswordAsync();
        Assert.StartsWith("PBKDF2-SHA256$2000$", hash, StringComparison.Ordinal);
        Assert.NotEqual(weaker, hash);
        Assert.Equal(PasswordFormat.Pbkdf2, format);
    }

    [Fact]
    public async Task PasswordUpgrade_PlainTextInTheHashFormatIsRefusedAndCounted()
    {
        await SetStoredPasswordAsync(SeedPatientPassword, PasswordFormat.Pbkdf2);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync(SeedPatientId, SeedPatientPassword, Ct));

        Assert.Equal((SeedPatientPassword, PasswordFormat.Pbkdf2), await StoredPasswordAsync());
        Assert.Equal(1, (await LockStateAsync()).Count);
    }

    [Fact]
    public async Task Slots_LastSlotCanBeBookedAndDisappearsFromTheList()
    {
        var day = TestServices.Tomorrow10.Date;
        var last = day.AddHours(16.5);

        await Appointments.BookAsync(TestServices.Patient(1), 1, last, Ct);
        var free = await Appointments.GetAvailableSlotsAsync(TestServices.Patient(2), 1, day, Ct);
        var closing = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Appointments.BookAsync(TestServices.Patient(2), 1, day.AddHours(17), Ct));

        Assert.Equal(15, free.Count);
        Assert.DoesNotContain(last, free);
        Assert.Equal(ValidationMessages.InvalidSlot, closing.Message);
    }

    [Fact]
    public async Task Window_EndsOnDayNinety()
    {
        // Start is Monday 2 March 2026: day 88 is a Friday, day 90 a Sunday and day 91 a Monday.
        var today = TestServices.Start.Date;

        Assert.Equal(16, (await Appointments.GetAvailableSlotsAsync(TestServices.Patient(1), 1, today.AddDays(88), Ct)).Count);
        Assert.Empty(await Appointments.GetAvailableSlotsAsync(TestServices.Patient(1), 1, today.AddDays(90), Ct));
        Assert.Empty(await Appointments.GetAvailableSlotsAsync(TestServices.Patient(1), 1, today.AddDays(91), Ct));

        await Appointments.BookAsync(TestServices.Patient(1), 1, today.AddDays(88).AddHours(16.5), Ct);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Appointments.BookAsync(TestServices.Patient(1), 1, today.AddDays(91).AddHours(9), Ct));
        Assert.Equal(ValidationMessages.TooFarAhead, error.Message);
    }

    [Fact]
    public async Task Cancel_AtTheStartTimeIsRefusedAndOneSecondBeforeIsAllowed()
    {
        var patient = TestServices.Patient(1);
        var early = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10, Ct);
        var late = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10.AddHours(1), Ct);

        _db.Clock.SetUtcNow(new DateTimeOffset(TestServices.Tomorrow10, TimeSpan.Zero));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments.CancelAsync(patient, early, Ct));
        Assert.Equal(ValidationMessages.StartedCannotCancel, error.Message);

        _db.Clock.SetUtcNow(new DateTimeOffset(TestServices.Tomorrow10.AddHours(1).AddSeconds(-1), TimeSpan.Zero));
        await Appointments.CancelAsync(patient, late, Ct);

        await using var context = _db.CreateContext();
        Assert.Equal(AppointmentStatus.Active, (await context.Appointments.SingleAsync(a => a.Id == early, Ct)).Status);
        Assert.Equal(AppointmentStatus.Cancelled, (await context.Appointments.SingleAsync(a => a.Id == late, Ct)).Status);
    }

    // IDOR: probing ids gives one answer for foreign, cancelled and missing appointments alike.
    [Fact]
    public async Task Cancel_ProbedIdsAllLookTheSameAndChangeNothing()
    {
        var foreign = await Appointments.BookAsync(TestServices.Patient(2), 2, TestServices.Tomorrow10, Ct);
        var foreignCancelled = await Appointments.BookAsync(TestServices.Patient(2), 3, TestServices.Tomorrow10, Ct);
        await Appointments.CancelAsync(TestServices.Patient(2), foreignCancelled, Ct);

        var answers = new List<string>();
        foreach (var id in new[] { foreign, foreignCancelled, foreignCancelled + 1, 0, -1, int.MaxValue })
        {
            answers.Add((await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.CancelAsync(TestServices.Patient(1), id, Ct))).Message);
        }

        Assert.All(answers, message => Assert.Equal("Bu işlem için yetkiniz yok.", message));
        await using var context = _db.CreateContext();
        Assert.Equal(AppointmentStatus.Active, (await context.Appointments.SingleAsync(a => a.Id == foreign, Ct)).Status);
    }

    [Fact]
    public async Task Doctor_CannotBookOrListSlotsAndSeesOnlyOwnAppointments()
    {
        await Appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10, Ct);
        await Appointments.BookAsync(TestServices.Patient(1), 2, TestServices.Tomorrow10.AddHours(1), Ct);

        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.BookAsync(TestServices.Doctor(1), 1, TestServices.Tomorrow10.AddHours(2), Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.GetAvailableSlotsAsync(TestServices.Doctor(1), 1, TestServices.Tomorrow10, Ct));

        var own = await Appointments.GetMyAppointmentsAsync(TestServices.Doctor(1), Ct);
        Assert.Equal(1, Assert.Single(own).DoctorId);
    }

    private async Task FailAsync(int times)
    {
        for (var i = 0; i < times; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync(SeedPatientId, "yanlis", Ct));
        }
    }

    private async Task<(int Count, DateTime? LockoutEnd)> LockStateAsync()
    {
        await using var context = _db.CreateContext();
        var patient = await context.Patients.AsNoTracking().SingleAsync(p => p.NationalId == SeedPatientId, Ct);
        return (patient.FailedLoginCount, patient.LockoutEnd);
    }

    private async Task<(string Hash, PasswordFormat Format)> StoredPasswordAsync()
    {
        await using var context = _db.CreateContext();
        var patient = await context.Patients.AsNoTracking().SingleAsync(p => p.NationalId == SeedPatientId, Ct);
        return (patient.PasswordHash, patient.PasswordFormat);
    }

    private async Task SetStoredPasswordAsync(string stored, PasswordFormat format)
    {
        await using var context = _db.CreateContext();
        await context.Patients
            .Where(p => p.NationalId == SeedPatientId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PasswordHash, stored).SetProperty(p => p.PasswordFormat, format), Ct);
    }

    private AuthService AuthWithHasher(IPasswordHasher hasher) =>
        new(
            _db.Get<IAccountRepository>(),
            _db.Get<IPatientRepository>(),
            hasher,
            _db.Clock,
            new RegistrationInputValidator(),
            new PatientSignInInputValidator(),
            new DoctorSignInInputValidator(),
            NullAuditLog.Instance);
}
