using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI.Tests.Support;

/// <summary>Sign-in and registration calls that stay pending until the test completes them.</summary>
internal sealed class PendingAuthService : IAuthService
{
    public int PatientSignInCalls { get; private set; }

    public int DoctorSignInCalls { get; private set; }

    public int RegisterCalls { get; private set; }

    public Task<UserSession> SignInPatientAsync(string? nationalId, string? password, CancellationToken cancellationToken = default)
    {
        PatientSignInCalls++;
        return new TaskCompletionSource<UserSession>().Task;
    }

    public Task<UserSession> SignInDoctorAsync(string? fullName, string? password, CancellationToken cancellationToken = default)
    {
        DoctorSignInCalls++;
        return new TaskCompletionSource<UserSession>().Task;
    }

    public Task RegisterPatientAsync(string? fullName, string? nationalId, string? password, CancellationToken cancellationToken = default)
    {
        RegisterCalls++;
        return new TaskCompletionSource().Task;
    }
}

/// <summary>
/// Appointment service whose writes stay pending and whose slot queries are answered by the test, in
/// any order, through <see cref="SlotRequests"/>.
/// </summary>
internal sealed class ControlledAppointmentService : IAppointmentService
{
    public List<DoctorSummary> Doctors { get; } = [new(1, "Dr. Ada Örnek", "Kardiyoloji"), new(2, "Dr. Can Deneme", "Dahiliye")];

    public List<AppointmentDetails> Appointments { get; } = [];

    public List<(int DoctorId, DateTime Day, TaskCompletionSource<IReadOnlyList<DateTime>> Answer)> SlotRequests { get; } = [];

    public int BookCalls { get; private set; }

    public int CancelCalls { get; private set; }

    public Task<IReadOnlyList<DoctorSummary>> GetDoctorsAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DoctorSummary>>(Doctors);

    public Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(
        UserSession session,
        int doctorId,
        DateTime day,
        CancellationToken cancellationToken = default)
    {
        var answer = new TaskCompletionSource<IReadOnlyList<DateTime>>();
        SlotRequests.Add((doctorId, day, answer));
        return answer.Task;
    }

    public Task<IReadOnlyList<AppointmentDetails>> GetMyAppointmentsAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AppointmentDetails>>(Appointments);

    public Task<int> BookAsync(UserSession session, int doctorId, DateTime startsAt, CancellationToken cancellationToken = default)
    {
        BookCalls++;
        return new TaskCompletionSource<int>().Task;
    }

    public Task CancelAsync(UserSession session, int appointmentId, CancellationToken cancellationToken = default)
    {
        CancelCalls++;
        return new TaskCompletionSource().Task;
    }
}

/// <summary>Answers confirmations with yes and records every message instead of showing a dialog.</summary>
internal sealed class RecordingPrompts : IUserPrompts
{
    public List<string> Messages { get; } = [];

    public bool Confirm(string question)
    {
        Messages.Add(question);
        return true;
    }

    public void Inform(string message) => Messages.Add(message);

    public void Warn(string message) => Messages.Add(message);
}

internal sealed class NoFormsFactory : IFormFactory
{
    public T Create<T>(params object[] arguments)
        where T : Form => throw new InvalidOperationException("Tests do not open other forms.");
}

internal static class Sessions
{
    public static UserSession Patient { get; } = new(AccountRole.Patient, 1, "Deniz Örnek", "100******50");

    public static UserSession Doctor { get; } = new(AccountRole.Doctor, 1, "Dr. Ada Örnek", "100******46");

    public static AppointmentDetails Appointment(int id) =>
        new(id, 1, "Deniz Örnek", 1, "Dr. Ada Örnek", "Kardiyoloji", new DateTime(2026, 3, 3, 10, 0, 0));
}
