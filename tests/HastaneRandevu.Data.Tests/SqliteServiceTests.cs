using HastaneRandevu.Business;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// Services and repositories together on SQLite in-memory: sign-in, lockout, password migration,
/// booking, cancelling and the ownership rules end to end. Each test gets a fresh database.
/// </summary>
public sealed class SqliteServiceTests : IAsyncLifetime
{
    private const string SeedPatientId = "10000000450";
    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAuthService Auth => _db.Get<IAuthService>();

    private IAppointmentService Appointments => _db.Get<IAppointmentService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Seed_ContainsOnlyFictionalAccountsThatCanSignIn()
    {
        var patient = await Auth.SignInPatientAsync(SeedPatientId, "Demo.Hasta1", Ct);
        var doctor = await Auth.SignInDoctorAsync("Dr. Ada Örnek", "Demo.Doktor1", Ct);
        var doctors = await Appointments.GetDoctorsAsync(patient, Ct);

        Assert.Equal("Deniz Örnek", patient.DisplayName);
        Assert.Equal("100******50", patient.MaskedNationalId);
        Assert.Equal(1, doctor.AccountId);
        Assert.Equal(["Dr. Ada Örnek - Kardiyoloji", "Dr. Can Deneme - Dahiliye", "Dr. Ece Taslak - Ortopedi"], doctors.Select(d => d.DisplayName));

        await using var context = _db.CreateContext();
        Assert.All(await context.Patients.Select(p => p.NationalId).ToListAsync(Ct), id => Assert.True(Business.Validation.NationalIdNumber.IsValid(id)));
        Assert.All(await context.Doctors.Select(d => d.PasswordHash).ToListAsync(Ct), h => Assert.StartsWith("PBKDF2-SHA256$600000$", h, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Register_StoresAHashAndAllowsSignIn()
    {
        await Auth.RegisterPatientAsync("Yeni Hasta", "10000000696", "Gizli.Sifre1", Ct);

        var session = await Auth.SignInPatientAsync("10000000696", "Gizli.Sifre1", Ct);
        await using var context = _db.CreateContext();
        var stored = await context.Patients.SingleAsync(p => p.Id == session.AccountId, Ct);

        Assert.StartsWith("PBKDF2-SHA256$", stored.PasswordHash, StringComparison.Ordinal);
        Assert.DoesNotContain("Gizli.Sifre1", stored.PasswordHash, StringComparison.Ordinal);
        var duplicate = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Auth.RegisterPatientAsync("Başka Hasta", "10000000696", "Gizli.Sifre2", Ct));
        Assert.Equal("Bu T.C. Kimlik No ile kayıtlı bir hasta zaten var.", duplicate.Message);
    }

    [Fact]
    public async Task Lockout_AfterFiveFailures_RefusesTheRightPasswordUntilTheLockEnds()
    {
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync(SeedPatientId, "yanlis", Ct));
        }

        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync(SeedPatientId, "Demo.Hasta1", Ct));
        Assert.Contains("15 dakika", locked.Message, StringComparison.Ordinal);

        _db.Clock.Advance(TimeSpan.FromMinutes(14));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync(SeedPatientId, "Demo.Hasta1", Ct));

        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var session = await Auth.SignInPatientAsync(SeedPatientId, "Demo.Hasta1", Ct);

        await using var context = _db.CreateContext();
        var patient = await context.Patients.SingleAsync(p => p.Id == session.AccountId, Ct);
        Assert.Equal(0, patient.FailedLoginCount);
        Assert.Null(patient.LockoutEnd);
    }

    [Fact]
    public async Task Lockout_SuccessfulSignInResetsTheCounter()
    {
        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInDoctorAsync("Dr. Can Deneme", "yanlis", Ct));
        }

        await Auth.SignInDoctorAsync("Dr. Can Deneme", "Demo.Doktor1", Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInDoctorAsync("Dr. Can Deneme", "yanlis", Ct));

        // One failure after the reset does not lock the account.
        await Auth.SignInDoctorAsync("Dr. Can Deneme", "Demo.Doktor1", Ct);
    }

    [Fact]
    public async Task LegacyPlainTextPassword_IsHashedOnTheFirstSuccessfulSignIn()
    {
        // As written by the original application: the password in plain text.
        await using (var context = _db.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE Hastalar SET Sifre = 'eski1' WHERE TC = '10000000528'", Ct);
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync("10000000528", "eski2", Ct));
        await Auth.SignInPatientAsync("10000000528", "eski1", Ct);

        await using (var context = _db.CreateContext())
        {
            var stored = await context.Patients.Where(p => p.NationalId == "10000000528").Select(p => p.PasswordHash).SingleAsync(Ct);
            Assert.StartsWith("PBKDF2-SHA256$1000$", stored, StringComparison.Ordinal);
        }

        await Auth.SignInPatientAsync("10000000528", "eski1", Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInPatientAsync("10000000528", "eski2", Ct));
    }

    [Fact]
    public async Task Lists_ShowOnlyTheSignedInAccountsAppointments()
    {
        var patientA = TestServices.Patient(1);
        var patientB = TestServices.Patient(2);
        await Appointments.BookAsync(patientA, 1, TestServices.Tomorrow10, Ct);
        await Appointments.BookAsync(patientB, 2, TestServices.Tomorrow10, Ct);
        await Appointments.BookAsync(patientB, 1, TestServices.Tomorrow10.AddHours(1), Ct);

        var listA = await Appointments.GetMyAppointmentsAsync(patientA, Ct);
        var listB = await Appointments.GetMyAppointmentsAsync(patientB, Ct);
        var doctor1 = await Appointments.GetMyAppointmentsAsync(TestServices.Doctor(1), Ct);
        var doctor3 = await Appointments.GetMyAppointmentsAsync(TestServices.Doctor(3), Ct);

        Assert.All(listA, a => Assert.Equal(1, a.PatientId));
        Assert.Single(listA);
        Assert.Equal(2, listB.Count);
        Assert.All(listB, a => Assert.Equal(2, a.PatientId));
        Assert.Equal(TestServices.Tomorrow10.AddHours(1), listB[0].StartsAt); // latest first for patients
        Assert.Equal([1, 2], doctor1.Select(a => a.PatientId)); // earliest first for doctors
        Assert.All(doctor1, a => Assert.Equal(1, a.DoctorId));
        Assert.Empty(doctor3);
        Assert.Equal("Deniz Örnek", doctor1[0].PatientName);
        Assert.Equal("Kardiyoloji", listA[0].Branch);
    }

    [Fact]
    public async Task Book_SameSlotTwice_IsRejected()
    {
        await Appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10, Ct);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Appointments.BookAsync(TestServices.Patient(2), 1, TestServices.Tomorrow10, Ct));

        Assert.Equal("Bu randevu saatinde doktor zaten dolu. Lütfen başka bir zaman seçin.", error.Message);
        var slots = await Appointments.GetAvailableSlotsAsync(TestServices.Patient(2), 1, TestServices.Tomorrow10, Ct);
        Assert.DoesNotContain(TestServices.Tomorrow10, slots);
        Assert.Equal(15, slots.Count);
    }

    [Fact]
    public async Task Database_UniqueFilteredIndex_RejectsASecondActiveAppointmentButNotACancelledOne()
    {
        await using var context = _db.CreateContext();
        context.Appointments.Add(new Appointment { PatientId = 1, DoctorId = 1, StartsAt = TestServices.Tomorrow10 });
        context.Appointments.Add(new Appointment { PatientId = 2, DoctorId = 1, StartsAt = TestServices.Tomorrow10, Status = AppointmentStatus.Cancelled });
        await context.SaveChangesAsync(Ct);

        // Inserted directly, past every check in the services and repositories.
        context.Appointments.Add(new Appointment { PatientId = 2, DoctorId = 1, StartsAt = TestServices.Tomorrow10 });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Repository_SecondInsertForTheSameSlot_IsReportedAsSlotTaken()
    {
        var repository = _db.Get<IAppointmentRepository>();
        await repository.AddAsync(new Appointment { PatientId = 1, DoctorId = 2, StartsAt = TestServices.Tomorrow10 }, Ct);

        await Assert.ThrowsAsync<SlotAlreadyBookedException>(
            () => repository.AddAsync(new Appointment { PatientId = 2, DoctorId = 2, StartsAt = TestServices.Tomorrow10 }, Ct));
    }

    [Fact]
    public async Task Cancel_KeepsTheRowAsHistoryAndFreesTheSlot()
    {
        var patient = TestServices.Patient(1);
        var id = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10, Ct);

        await Appointments.CancelAsync(patient, id, Ct);

        Assert.Empty(await Appointments.GetMyAppointmentsAsync(patient, Ct));
        await using (var context = _db.CreateContext())
        {
            var row = await context.Appointments.SingleAsync(a => a.Id == id, Ct);
            Assert.Equal(AppointmentStatus.Cancelled, row.Status);
            Assert.Equal(TestServices.Start.UtcDateTime, row.CancelledAt);
        }

        await Appointments.BookAsync(TestServices.Patient(2), 1, TestServices.Tomorrow10, Ct);
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments.CancelAsync(patient, id, Ct));
        Assert.Equal("Bu randevu zaten iptal edilmiş.", again.Message);
    }

    [Fact]
    public async Task Cancel_PastAppointment_IsRefused()
    {
        var patient = TestServices.Patient(1);
        var id = await Appointments.BookAsync(patient, 1, TestServices.Tomorrow10, Ct);
        _db.Clock.Advance(TimeSpan.FromDays(1));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Appointments.CancelAsync(patient, id, Ct));

        Assert.Equal("Geçmiş veya başlamış bir randevu iptal edilemez.", error.Message);
        Assert.Single(await Appointments.GetMyAppointmentsAsync(patient, Ct));
    }

    // IDOR: valid sessions must not reach other accounts' appointments by passing their ids.
    [Fact]
    public async Task Cancel_OtherPatientsOrDoctorsAppointment_IsDeniedAndTheRowStaysActive()
    {
        // Patient 2 books with doctor 1.
        var id = await Appointments.BookAsync(TestServices.Patient(2), 1, TestServices.Tomorrow10, Ct);

        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.CancelAsync(TestServices.Patient(1), id, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.CancelAsync(TestServices.Doctor(3), id, Ct));

        // Doctor 2 has the same numeric id as the patient, but ownership is checked per role.
        await Assert.ThrowsAsync<AccessDeniedException>(() => Appointments.CancelAsync(TestServices.Doctor(2), id, Ct));
        Assert.DoesNotContain(await Appointments.GetMyAppointmentsAsync(TestServices.Doctor(2), Ct), a => a.Id == id);
        Assert.DoesNotContain(await Appointments.GetMyAppointmentsAsync(TestServices.Patient(1), Ct), a => a.Id == id);

        await using var context = _db.CreateContext();
        Assert.Equal(AppointmentStatus.Active, (await context.Appointments.SingleAsync(a => a.Id == id, Ct)).Status);

        // The doctor who receives the appointment may cancel it.
        await Appointments.CancelAsync(TestServices.Doctor(1), id, Ct);
    }

    [Fact]
    public async Task Repository_Cancel_RepeatsTheOwnerConditionInTheUpdate()
    {
        var repository = _db.Get<IAppointmentRepository>();
        var id = await repository.AddAsync(new Appointment { PatientId = 1, DoctorId = 1, StartsAt = TestServices.Tomorrow10 }, Ct);
        var now = TestServices.Start.DateTime;

        Assert.False(await repository.CancelAsync(id, AccountRole.Patient, 2, now, now, Ct));
        Assert.False(await repository.CancelAsync(id, AccountRole.Doctor, 2, now, now, Ct));
        Assert.False(await repository.CancelAsync(id, AccountRole.Patient, 1, TestServices.Tomorrow10, now, Ct));
        Assert.True(await repository.CancelAsync(id, AccountRole.Patient, 1, now, now, Ct));
        Assert.False(await repository.CancelAsync(id, AccountRole.Patient, 1, now, now, Ct));
    }
}
