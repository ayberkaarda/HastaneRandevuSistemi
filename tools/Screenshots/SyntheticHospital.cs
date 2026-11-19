using System.Globalization;
using System.Reflection;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Screenshots;

/// <summary>
/// A small invented hospital for the README images: made-up doctors and patients, a schedule and
/// appointments around today. Every service answers synchronously from memory; nothing is written to a
/// database or a file, and no national id belongs to a person (they are generated to pass the checksum
/// and are only ever shown masked).
/// </summary>
internal sealed class SyntheticHospital :
    IAuthService, IAppointmentService, IAvailabilityService, IReminderService, IExportService, IScheduleService
{
    private static readonly TimeSpan[] SlotTimes = Enumerable.Range(0, 16).Select(i => TimeSpan.FromHours(9) + TimeSpan.FromMinutes(30 * i)).ToArray();

    private readonly DateTime _today = DateTime.Today;
    private readonly List<AppointmentDetails> _appointments = [];

    public SyntheticHospital()
    {
        Doctors =
        [
            new(1, "Dr. Defne Akın", "Kardiyoloji"),
            new(2, "Dr. Emre Sarıgül", "Dahiliye"),
            new(3, "Dr. Nazlı Erdem", "Göz Hastalıkları"),
            new(4, "Dr. Kaan Özkaya", "Ortopedi"),
            new(5, "Dr. Mira Tunalı", "Dermatoloji"),
            new(6, "Dr. Ozan Bilgin", "Kulak Burun Boğaz"),
            new(7, "Dr. Ece Yalın", "Nöroloji"),
        ];

        Patient = CreateSession(AccountRole.Patient, 11, "Elif Kaya", SyntheticNationalId(3, 4, 7, 1, 9, 2, 5, 8, 6));
        Doctor = CreateSession(AccountRole.Doctor, 1, "Dr. Defne Akın", SyntheticNationalId(2, 8, 4, 6, 1, 3, 7, 5, 9));

        // The patient's own appointments: one tomorrow morning (the reminder) and a few later ones.
        AddForPatient(2, 1, 10, 30);
        AddForPatient(1, 6, 14, 0);
        AddForPatient(3, 13, 9, 30);
        AddForPatient(5, 27, 11, 0);

        // The doctor's agenda.
        string[] patients = ["Ali Demirtaş", "Zeynep Arslan", "Mert Çınar", "Elif Kaya", "Selin Uçar", "Burak Tan", "Aylin Koç", "Can Er"];
        var day = 0;
        for (var i = 0; i < patients.Length; i++)
        {
            day += i % 3 == 0 ? 1 : 0;
            var start = WorkingDay(day).AddHours(9).AddMinutes(30 * ((i * 3) % 14));
            _appointments.Add(new AppointmentDetails(200 + i, 20 + i, patients[i], 1, Doctors[0].FullName, Doctors[0].Branch, start));
        }
    }

    public IReadOnlyList<DoctorSummary> Doctors { get; }

    public UserSession Patient { get; }

    public UserSession Doctor { get; }

    /// <summary>The leave days of the doctor, in the coming weeks.</summary>
    public IReadOnlyList<DateOnly> LeaveDays => [DateOnly.FromDateTime(WorkingDay(9)), DateOnly.FromDateTime(WorkingDay(10)), DateOnly.FromDateTime(WorkingDay(24))];

    // Sign-in and registration are not used while capturing.
    public Task<UserSession> SignInPatientAsync(string? nationalId, string? password, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<UserSession> SignInDoctorAsync(string? fullName, string? password, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task RegisterPatientAsync(string? fullName, string? nationalId, string? password, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<DoctorSummary>> GetDoctorsAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult(Doctors);

    public Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(UserSession session, int doctorId, DateTime day, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DateTime>>(FreeSlots(doctorId, day.Date).ToList());

    public Task<IReadOnlyList<AppointmentDetails>> GetMyAppointmentsAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AppointmentDetails>>(
            [.. _appointments.Where(a => session.Role == AccountRole.Patient ? a.PatientId == session.AccountId : a.DoctorId == session.AccountId).OrderBy(a => a.StartsAt)]);

    public Task<int> BookAsync(UserSession session, int doctorId, DateTime startsAt, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task CancelAsync(UserSession session, int appointmentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RescheduleAsync(UserSession session, int appointmentId, DateTime newStartsAt, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<DoctorSummary>> SearchDoctorsAsync(UserSession session, string? name, string? branch, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DoctorSummary>>(
            [.. Doctors.Where(d => (string.IsNullOrWhiteSpace(name) || d.FullName.Contains(name, StringComparison.CurrentCultureIgnoreCase))
                && (branch is null || d.Branch == branch))]);

    public Task<IReadOnlyList<string>> GetBranchesAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. Doctors.Select(d => d.Branch!).Distinct().Order(StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), false))]);

    public Task<IReadOnlyList<CalendarDay>> GetCalendarAsync(UserSession session, int doctorId, DateOnly firstDay, int days, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CalendarDay>>([.. Enumerable.Range(0, days).Select(i => Day(doctorId, firstDay.AddDays(i)))]);

    public Task<DateTime?> FindFirstFreeSlotAsync(UserSession session, int doctorId, CancellationToken cancellationToken = default)
    {
        for (var day = 0; day <= AppointmentRules.MaxDaysAhead; day++)
        {
            var first = FreeSlots(doctorId, _today.AddDays(day)).FirstOrDefault();
            if (first != default)
            {
                return Task.FromResult<DateTime?>(first);
            }
        }

        return Task.FromResult<DateTime?>(null);
    }

    public Task<IReadOnlyList<AppointmentDetails>> GetUpcomingAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AppointmentDetails>>(
            [.. _appointments.Where(a => a.PatientId == session.AccountId && a.StartsAt > DateTime.Now && a.StartsAt <= DateTime.Now.AddHours(48)).OrderBy(a => a.StartsAt)]);

    public Task<int> ExportHistoryCsvAsync(UserSession session, string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(_appointments.Count(a => a.PatientId == session.AccountId) + 3);

    public Task<int> ExportAgendaHtmlAsync(UserSession session, DateOnly day, string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(_appointments.Count(a => a.DoctorId == session.AccountId && DateOnly.FromDateTime(a.StartsAt) == day));

    public Task<DoctorScheduleView> GetMyScheduleAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DoctorScheduleView(
            [
                new WorkingDay(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
                new WorkingDay(DayOfWeek.Tuesday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
                new WorkingDay(DayOfWeek.Wednesday, TimeSpan.FromHours(13), TimeSpan.FromHours(19)),
                new WorkingDay(DayOfWeek.Thursday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
                new WorkingDay(DayOfWeek.Friday, TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            ],
            UsesDefaultHours: false,
            LeaveDays));

    public Task<IReadOnlyList<ScheduleConflict>> SaveWorkingDaysAsync(UserSession session, IReadOnlyList<WorkingDay> days, CancellationToken cancellationToken = default) =>
        GetConflictsAsync(session, cancellationToken);

    public Task<IReadOnlyList<ScheduleConflict>> AddLeaveDayAsync(UserSession session, DateOnly day, CancellationToken cancellationToken = default) =>
        GetConflictsAsync(session, cancellationToken);

    public Task RemoveLeaveDayAsync(UserSession session, DateOnly day, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<ScheduleConflict>> GetConflictsAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        var leave = LeaveDays[0].ToDateTime(TimeOnly.MinValue);
        return Task.FromResult<IReadOnlyList<ScheduleConflict>>(
        [
            new(new AppointmentDetails(301, 31, "Deniz Aksoy", 1, Doctors[0].FullName, Doctors[0].Branch, leave.AddHours(10)), ScheduleConflictReason.LeaveDay),
            new(new AppointmentDetails(302, 32, "Umut Kara", 1, Doctors[0].FullName, Doctors[0].Branch, leave.AddHours(14).AddMinutes(30)), ScheduleConflictReason.LeaveDay),
            new(new AppointmentDetails(303, 33, "Sena Yurt", 1, Doctors[0].FullName, Doctors[0].Branch, NextWeekday(DayOfWeek.Wednesday).AddHours(10)), ScheduleConflictReason.OutsideWorkingHours),
        ]);
    }

    /// <summary>The <paramref name="offset"/>-th working day (Monday to Friday) from today.</summary>
    private DateTime WorkingDay(int offset)
    {
        var day = _today;
        var found = 0;
        while (true)
        {
            day = day.AddDays(1);
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && ++found >= offset)
            {
                return day;
            }
        }
    }

    private DateTime NextWeekday(DayOfWeek weekday)
    {
        var day = _today.AddDays(1);
        while (day.DayOfWeek != weekday)
        {
            day = day.AddDays(1);
        }

        return day;
    }

    private void AddForPatient(int doctorId, int days, int hour, int minute)
    {
        var doctor = Doctors.Single(d => d.Id == doctorId);
        var start = days == 1 ? _today.AddDays(1).AddHours(hour).AddMinutes(minute) : WorkingDay(days).AddHours(hour).AddMinutes(minute);
        _appointments.Add(new AppointmentDetails(100 + _appointments.Count, Patient.AccountId, Patient.DisplayName, doctor.Id, doctor.FullName, doctor.Branch, start));
    }

    /// <summary>A believable spread of free and taken slots that depends only on the doctor and the day.</summary>
    private IEnumerable<DateTime> FreeSlots(int doctorId, DateTime day)
    {
        var status = Day(doctorId, DateOnly.FromDateTime(day));
        if (status.Status != CalendarDayStatus.Available)
        {
            return [];
        }

        var taken = SlotTimes.Length - status.FreeSlots;
        return SlotTimes
            .Select((time, index) => (Time: time, Index: index))
            .Where(slot => (slot.Index * 7 + day.Day + doctorId) % SlotTimes.Length >= taken)
            .Select(slot => day.Date + slot.Time)
            .Where(start => start > DateTime.Now);
    }

    private CalendarDay Day(int doctorId, DateOnly date)
    {
        var today = DateOnly.FromDateTime(_today);
        if (date < today || date > today.AddDays(AppointmentRules.MaxDaysAhead))
        {
            return new CalendarDay(date, CalendarDayStatus.OutsideWindow, 0, 0);
        }

        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return new CalendarDay(date, CalendarDayStatus.NotWorking, 0, 0);
        }

        if (doctorId == 1 && LeaveDays.Contains(date))
        {
            return new CalendarDay(date, CalendarDayStatus.Leave, 0, 0);
        }

        var total = SlotTimes.Length;
        var seed = (date.DayNumber * 31) + (doctorId * 17);
        var free = date == today ? 0 : date.DayNumber - today.DayNumber < 4 ? seed % 3 : 2 + (seed % (total - 2));
        return free == 0
            ? new CalendarDay(date, CalendarDayStatus.Full, 0, total)
            : new CalendarDay(date, CalendarDayStatus.Available, free, total);
    }

    /// <summary>An 11 digit number that passes the national id checksum, built from nine invented digits.</summary>
    private static string SyntheticNationalId(params int[] digits)
    {
        var odd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var even = digits[1] + digits[3] + digits[5] + digits[7];
        var tenth = (((odd * 7) - even) % 10 + 10) % 10;
        var eleventh = (digits.Sum() + tenth) % 10;
        var id = string.Concat(digits.Select(d => d.ToString(CultureInfo.InvariantCulture))) + tenth.ToString(CultureInfo.InvariantCulture) + eleventh.ToString(CultureInfo.InvariantCulture);
        return NationalIdNumber.IsValid(id) ? id : throw new InvalidOperationException("The synthetic national id does not pass the checksum.");
    }

    /// <summary>
    /// Sessions are issued only by the sign-in service; the tool builds one through the internal constructor,
    /// with the national id already masked as a real sign-in would do.
    /// </summary>
    private static UserSession CreateSession(AccountRole role, int id, string name, string nationalId) =>
        (UserSession)Activator.CreateInstance(
            typeof(UserSession),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [role, id, name, NationalIdNumber.Mask(nationalId)],
            culture: CultureInfo.InvariantCulture)!;
}
