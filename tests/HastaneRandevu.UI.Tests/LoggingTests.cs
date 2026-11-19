using System.Text.RegularExpressions;
using HastaneRandevu.Business;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using HastaneRandevu.UI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.UI.Tests;

/// <summary>What reaches the log file (KVKK). No window is created by these tests.</summary>
public sealed partial class LoggingTests : IDisposable
{
    // Personal data as it can appear in a sign-in, a registration, an appointment or an exception message.
    private const string NationalId = "10000000450";
    private const string PatientName = "Deniz Örnek";
    private const string Password = "Gizli.Sifre1";
    private const string DoctorName = "Dr. Ada Örnek";
    private const string Branch = "Kardiyoloji";

    // Wednesday 5 March 2031, 10:10 UTC: the year cannot appear in a log timestamp written today.
    private static readonly DateTimeOffset Now = new(2031, 3, 5, 10, 10, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hastanerandevu-log-test-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void CreateLogger_WritesADailyFileInTheGivenDirectory()
    {
        using (var logger = AppLogging.CreateLogger(_directory))
        {
            logger.Information("probe");
        }

        var file = Assert.Single(Directory.GetFiles(_directory));
        Assert.Matches(@"hastanerandevu-\d{8}\.log$", file);
        Assert.Contains("probe", File.ReadAllText(file), StringComparison.Ordinal);
    }

    /// <summary>Files dated before the last 30 days are deleted when the logger opens today's file.</summary>
    [Fact]
    public void Retention_DeletesFilesOlderThanThirtyDaysAndKeepsRecentOnes()
    {
        int[] ages = [1, 2, 15, 29, 30, 31, 45, 60, 400];
        Directory.CreateDirectory(_directory);
        foreach (var age in ages)
        {
            File.WriteAllText(DatedFile(age), "old");
        }

        var unrelated = Path.Combine(_directory, "notes.txt");
        File.WriteAllText(unrelated, "not a log file");

        using (var logger = AppLogging.CreateLogger(_directory))
        {
            logger.Information("probe");
        }

        Assert.Equal(30, AppLogging.RetainedDays);
        Assert.All(ages.Where(a => a < 30), age => Assert.True(File.Exists(DatedFile(age)), $"The file of {age} days ago was deleted."));
        Assert.All(ages.Where(a => a >= 30), age => Assert.False(File.Exists(DatedFile(age)), $"The file of {age} days ago was kept."));
        Assert.True(File.Exists(DatedFile(0)));
        Assert.True(File.Exists(unrelated));
    }

    /// <summary>
    /// More than 30 recent files (several per day, as size or restart sequences produce) are cut to the
    /// newest 30, today's file included.
    /// </summary>
    [Fact]
    public void Retention_KeepsAtMostThirtyFilesNewestFirst()
    {
        Directory.CreateDirectory(_directory);
        var created = new List<string>();
        for (var age = 1; age <= 10; age++)
        {
            created.Add(DatedFile(age));
            for (var sequence = 1; sequence <= 3; sequence++)
            {
                created.Add(DatedFile(age, sequence));
            }
        }

        foreach (var file in created)
        {
            File.WriteAllText(file, "old");
        }

        using (var logger = AppLogging.CreateLogger(_directory))
        {
            logger.Information("probe");
        }

        // Newest first: day 1 sequence 3, 2, 1, base, then day 2 and so on.
        var newestFirst = created
            .OrderBy(AgeOf)
            .ThenByDescending(SequenceOf)
            .ToList();
        var remaining = Directory.GetFiles(_directory, "hastanerandevu-*.log");
        Assert.Equal(AppLogging.RetainedFileCount, remaining.Length);
        Assert.Equal(30, AppLogging.RetainedFileCount);
        Assert.All(newestFirst.Take(AppLogging.RetainedFileCount - 1), f => Assert.True(File.Exists(f), Path.GetFileName(f) + " was deleted."));
        Assert.All(newestFirst.Skip(AppLogging.RetainedFileCount - 1), f => Assert.False(File.Exists(f), Path.GetFileName(f) + " was kept."));
    }

    private string DatedFile(int age, int sequence = 0)
    {
        var day = DateTime.Today.AddDays(-age).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var suffix = sequence == 0 ? string.Empty : "_" + sequence.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(_directory, $"hastanerandevu-{day}{suffix}.log");
    }

    private static int AgeOf(string file)
    {
        var day = DateTime.ParseExact(Path.GetFileName(file).Substring(15, 8), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        return (DateTime.Today - day).Days;
    }

    private static int SequenceOf(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var underscore = name.LastIndexOf('_');
        return underscore < 0 ? 0 : int.Parse(name[(underscore + 1)..], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void DefaultDirectory_IsUnderLocalApplicationData()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(root, AppLogging.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("HastaneRandevu", "logs"), AppLogging.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Record_UnexpectedError_KeepsTypesCodesAndMethodsButNoMessageOrPersonalData()
    {
        var exception = Throw(new InvalidOperationException(
            $"Randevu kaydedilemedi: {PatientName} T.C. {NationalId} şifre {Password}",
            new TimeoutException($"{DoctorName} {Branch} 05.03.2031 10:30")));

        var text = WriteAndRead(logger => ErrorReporter.Record(logger, exception));

        Assert.Contains("[ERR] Operation failed", text, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains(typeof(TimeoutException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains("HResult 0x", text, StringComparison.Ordinal);
        Assert.Contains(nameof(LoggingTests) + "." + nameof(Throw), text, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Randevu kaydedilemedi", text, StringComparison.Ordinal);
        AssertNoPersonalData(text);
    }

    [Fact]
    public void Record_RefusedOperation_LogsTheTypeButNotTheMessage()
    {
        var text = WriteAndRead(logger =>
        {
            ErrorReporter.Record(logger, new BusinessRuleException($"{NationalId} {PatientName}"));
            ErrorReporter.Record(logger, new AccessDeniedException($"{DoctorName} {Branch}"));
        });

        Assert.Contains("[WRN] Operation refused: BusinessRuleException", text, StringComparison.Ordinal);
        Assert.Contains("[WRN] Operation refused: AccessDeniedException", text, StringComparison.Ordinal);
        AssertNoPersonalData(text);
    }

    [Fact]
    public void AuditLog_WritesOnlyTheCodeAndTheRole()
    {
        var text = WriteAndRead(logger =>
        {
            var audit = new SerilogAuditLog(logger);
            foreach (var auditEvent in Enum.GetValues<AuditEvent>())
            {
                audit.Record(auditEvent, AccountRole.Patient);
                audit.Record(auditEvent, AccountRole.Doctor);
            }
        });

        Assert.Contains("[INF] Audit auth.sign_in.failed patient", text, StringComparison.Ordinal);
        Assert.DoesNotContain("locked", text, StringComparison.Ordinal);
        Assert.Contains("[INF] Audit appointment.cancelled doctor", text, StringComparison.Ordinal);
        Assert.All(Lines(text), line => Assert.Matches(AuditLine(), line));
    }

    [Fact]
    public async Task FullSession_LeavesOnlyCodesAndTypesInTheLog()
    {
        using var logger = AppLogging.CreateLogger(_directory);
        var accounts = new InMemoryAccounts();
        var appointments = new InMemoryAppointments();
        var services = new ServiceCollection()
            .AddSingleton<IAuditLog>(new SerilogAuditLog(logger))
            .AddSingleton<TimeProvider>(new FixedTime(Now))
            .AddSingleton<IPasswordHasher>(new PasswordHasher(iterations: 1_000))
            .AddSingleton<IAccountRepository>(accounts)
            .AddSingleton<IPatientRepository>(accounts)
            .AddSingleton<IDoctorRepository>(appointments)
            .AddSingleton<IAppointmentRepository>(appointments)
            .AddSingleton<IDoctorScheduleRepository>(new EmptySchedules())
            .AddBusinessServices();
        await using var provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthService>();
        var booking = provider.GetRequiredService<IAppointmentService>();

        async Task Attempt(Func<Task> action)
        {
            try
            {
                await action();
            }
#pragma warning disable CA1031 // Every failure goes to the log, as the forms do.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Record(logger, ex);
            }
        }

        await auth.RegisterPatientAsync(PatientName, NationalId, Password, Ct);
        await Attempt(() => auth.RegisterPatientAsync(PatientName, NationalId, Password, Ct)); // taken
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++)
        {
            await Attempt(() => auth.SignInPatientAsync(NationalId, "yanlis-" + Password, Ct));
        }

        await Attempt(() => auth.SignInPatientAsync(NationalId, Password, Ct)); // locked
        accounts.Unlock();
        var patient = await auth.SignInPatientAsync(NationalId, Password, Ct);
        var start = new DateTime(2031, 3, 6, 10, 30, 0);
        var id = await booking.BookAsync(patient, 1, start, Ct);
        await Attempt(() => booking.BookAsync(patient, 1, start, Ct)); // slot taken
        await Attempt(() => booking.CancelAsync(new UserSession(AccountRole.Patient, 99, "Başka", "***"), id, Ct)); // not owner
        await booking.CancelAsync(patient, id, Ct);
        logger.Dispose();

        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(_directory)));
        Assert.Contains("Audit auth.patient.registered patient", text, StringComparison.Ordinal);
        // The refusal of the locked account is counted as one more failure, not as a lock.
        Assert.Equal(LoginPolicy.MaxFailedAttempts + 1, Regex.Count(text, "Audit auth.sign_in.failed patient"));
        Assert.DoesNotContain("locked", text, StringComparison.Ordinal);
        Assert.Contains("Audit auth.sign_in.succeeded patient", text, StringComparison.Ordinal);
        Assert.Contains("Audit appointment.booked patient", text, StringComparison.Ordinal);
        Assert.Contains("Audit appointment.slot_taken patient", text, StringComparison.Ordinal);
        Assert.Contains("Audit access.denied patient", text, StringComparison.Ordinal);
        Assert.Contains("Audit appointment.cancelled patient", text, StringComparison.Ordinal);
        Assert.All(Lines(text), line => Assert.Matches(AllowedLine(), line));
        AssertNoPersonalData(text);
    }

    /// <summary>
    /// The log file is readable by the local Windows user, who knows when they made an attempt. A wrong
    /// password for an unknown, an unlocked and a locked account must therefore leave identical entries
    /// (and show identical messages), or the file would reveal which accounts exist or are locked.
    /// </summary>
    [Fact]
    public async Task SignInFailures_DoNotDiscloseLockStateInLocalLog()
    {
        const string Unlocked = "10000000450";
        const string Locked = "10000000528";
        const string Unknown = "10000000696";
        var hasher = new PasswordHasher(iterations: 1_000);
        var accounts = new FixedAccounts(
            new AccountCredentials(1, PatientName, Unlocked, hasher.Hash(Password), PasswordFormat.Pbkdf2, 0, null),
            new AccountCredentials(2, "Mert Deneme", Locked, hasher.Hash(Password), PasswordFormat.Pbkdf2, 0, Now.UtcDateTime.AddMinutes(10)));

        var outcomes = new List<(string Message, string[] Entries)>();
        foreach (var nationalId in new[] { Unknown, Unlocked, Locked })
        {
            var directory = Path.Combine(_directory, nationalId);
            string message;
            using (var logger = AppLogging.CreateLogger(directory))
            {
                var services = new ServiceCollection()
                    .AddSingleton<IAuditLog>(new SerilogAuditLog(logger))
                    .AddSingleton<TimeProvider>(new FixedTime(Now))
                    .AddSingleton<IPasswordHasher>(hasher)
                    .AddSingleton<IAccountRepository>(accounts)
                    .AddSingleton<IPatientRepository>(new InMemoryAccounts())
                    .AddSingleton<IDoctorRepository>(new InMemoryAppointments())
                    .AddSingleton<IAppointmentRepository>(new InMemoryAppointments())
                    .AddSingleton<IDoctorScheduleRepository>(new EmptySchedules())
            .AddBusinessServices();
                await using var provider = services.BuildServiceProvider();

                var refused = await Assert.ThrowsAsync<BusinessRuleException>(
                    () => provider.GetRequiredService<IAuthService>().SignInPatientAsync(nationalId, "yanlis-" + Password, Ct));
                ErrorReporter.Record(logger, refused); // as the sign-in form does
                message = refused.Message;
            }

            var entries = Lines(File.ReadAllText(Assert.Single(Directory.GetFiles(directory))))
                .Select(line => StampPattern().Replace(line, string.Empty))
                .ToArray();
            outcomes.Add((message, entries));
        }

        Assert.Equal(["[INF] Audit auth.sign_in.failed patient", "[WRN] Operation refused: BusinessRuleException"], outcomes[0].Entries);
        Assert.All(outcomes, o => Assert.Equal(outcomes[0].Entries, o.Entries));
        Assert.All(outcomes, o => Assert.Equal(outcomes[0].Message, o.Message));
        Assert.All(outcomes, o => Assert.DoesNotContain(o.Entries, e => e.Contains("locked", StringComparison.OrdinalIgnoreCase)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static void AssertNoPersonalData(string text)
    {
        Assert.DoesNotContain(NationalId, text, StringComparison.Ordinal);
        Assert.DoesNotContain(NationalId[..5], text, StringComparison.Ordinal); // not even a masked prefix
        Assert.DoesNotContain("Deniz", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Örnek", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sifre", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("şifre", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Branch, text, StringComparison.Ordinal);
        Assert.DoesNotContain("2031", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PBKDF2", text, StringComparison.Ordinal);
    }

    private static string[] Lines(string text) =>
        text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private const string Stamp = @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} ";

    [GeneratedRegex(Stamp)]
    private static partial Regex StampPattern();

    [GeneratedRegex(Stamp + @"\[INF\] Audit [a-z_]+(\.[a-z_]+)+ (patient|doctor)$")]
    private static partial Regex AuditLine();

    [GeneratedRegex(Stamp + @"(\[INF\] Audit [a-z_]+(\.[a-z_]+)+ (patient|doctor)|\[WRN\] Operation refused: (BusinessRuleException|AccessDeniedException))$")]
    private static partial Regex AllowedLine();

    private string WriteAndRead(Action<Serilog.ILogger> write)
    {
        using (var logger = AppLogging.CreateLogger(_directory))
        {
            write(logger);
        }

        return File.ReadAllText(Assert.Single(Directory.GetFiles(_directory)));
    }

    private static Exception Throw(Exception exception)
    {
        try
        {
            throw exception;
        }
#pragma warning disable CA1031 // Test helper captures the stack trace.
        catch (Exception caught)
#pragma warning restore CA1031
        {
            return caught;
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    /// <summary>One patient account with the lockout counter of the real repository.</summary>
    private sealed class InMemoryAccounts : IAccountRepository, IPatientRepository
    {
        private Patient? _patient;

        public void Unlock() => _patient!.LockoutEnd = null;

        public Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_patient?.NationalId == nationalId);

        public Task<int> AddAsync(Patient patient, CancellationToken cancellationToken = default)
        {
            _patient = patient;
            patient.Id = 1;
            return Task.FromResult(1);
        }

        public Task<AccountCredentials?> FindPatientByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_patient is { } p && p.NationalId == nationalId
                ? new AccountCredentials(p.Id, p.FullName, p.NationalId, p.PasswordHash, p.PasswordFormat, p.FailedLoginCount, p.LockoutEnd)
                : null);

        public Task<AccountCredentials?> FindDoctorByNameAsync(string fullName, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountCredentials?>(null);

        public Task RecordFailedLoginAsync(AccountRole role, int accountId, int maxAttempts, DateTime lockoutEnd, CancellationToken cancellationToken = default)
        {
            var p = _patient!;
            if (p.FailedLoginCount + 1 >= maxAttempts)
            {
                p.LockoutEnd = lockoutEnd;
                p.FailedLoginCount = 0;
            }
            else
            {
                p.FailedLoginCount++;
            }

            return Task.CompletedTask;
        }

        public Task<bool> RecordSuccessfulLoginAsync(AccountRole role, AccountCredentials verified, DateTime now, string? newPasswordHash, CancellationToken cancellationToken = default)
        {
            _patient!.FailedLoginCount = 0;
            return Task.FromResult(true);
        }
    }

    /// <summary>Fixed patient accounts; failures and successes are accepted and not stored.</summary>
    private sealed class FixedAccounts(params AccountCredentials[] patients) : IAccountRepository
    {
        public Task<AccountCredentials?> FindPatientByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(patients.SingleOrDefault(p => p.NationalId == nationalId));

        public Task<AccountCredentials?> FindDoctorByNameAsync(string fullName, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountCredentials?>(null);

        public Task RecordFailedLoginAsync(AccountRole role, int accountId, int maxAttempts, DateTime lockoutEnd, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> RecordSuccessfulLoginAsync(AccountRole role, AccountCredentials verified, DateTime now, string? newPasswordHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    /// <summary>No saved schedules: every doctor works the default hours.</summary>
    private sealed class EmptySchedules : IDoctorScheduleRepository
    {
        public Task<IReadOnlyList<WorkingDay>> GetWorkingDaysAsync(int doctorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkingDay>>([]);

        public Task<IReadOnlyList<DateOnly>> GetLeaveDaysAsync(int doctorId, DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DateOnly>>([]);

        public Task ReplaceWorkingDaysAsync(int doctorId, IReadOnlyList<WorkingDay> days, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> AddLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>One doctor and a list of appointments with the unique active slot rule.</summary>
    private sealed class InMemoryAppointments : IAppointmentRepository, IDoctorRepository
    {
        private readonly List<Appointment> _rows = [];

        public Task<IReadOnlyList<DoctorSummary>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DoctorSummary>>([new DoctorSummary(1, DoctorName, Branch)]);

        public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(id == 1);

        public Task<IReadOnlyList<AppointmentDetails>> GetActiveForPatientAsync(int patientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentDetails>>([]);

        public Task<IReadOnlyList<AppointmentDetails>> GetActiveForDoctorAsync(int doctorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentDetails>>([]);

        public Task<IReadOnlyList<DateTime>> GetBookedStartsAsync(int doctorId, DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DateTime>>([]);

        public Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rows.SingleOrDefault(a => a.Id == id));

        public Task<int> AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
        {
            if (_rows.Any(a => a.DoctorId == appointment.DoctorId && a.StartsAt == appointment.StartsAt && a.Status == AppointmentStatus.Active))
            {
                return Task.FromException<int>(new SlotAlreadyBookedException($"{DoctorName} {appointment.StartsAt:O}"));
            }

            appointment.Id = _rows.Count + 1;
            _rows.Add(appointment);
            return Task.FromResult(appointment.Id);
        }

        public Task<bool> CancelAsync(int id, AccountRole ownerRole, int ownerId, DateTime startsAfter, DateTime cancelledAtUtc, CancellationToken cancellationToken = default)
        {
            var row = _rows.Single(a => a.Id == id);
            row.Status = AppointmentStatus.Cancelled;
            return Task.FromResult(true);
        }
    }
}
