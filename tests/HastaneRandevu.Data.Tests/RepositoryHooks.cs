using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// Deterministic interleaving points between the business services and the real repositories: the
/// decorators run a test supplied hook right after a read returned, before the service goes on.
/// </summary>
internal static class RepositoryHooks
{
    /// <summary>Runs <paramref name="afterRead"/> each time an appointment was read by id.</summary>
    public static IServiceCollection AfterAppointmentRead(this IServiceCollection services, Func<CancellationToken, Task> afterRead) =>
        services.Decorate<IAppointmentRepository>(inner => new HookedAppointmentRepository(inner, afterRead));

    /// <summary>Records every audit event of this application instance in <paramref name="events"/>.</summary>
    public static IServiceCollection RecordAudit(this IServiceCollection services, List<AuditEvent> events) =>
        services.AddSingleton<IAuditLog>(new RecordingAuditLog(events));

    /// <summary>A hook that lets each caller pass only after <paramref name="count"/> callers arrived.</summary>
    public static Func<CancellationToken, Task> Barrier(int count)
    {
        var arrived = 0;
        var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return async ct =>
        {
            if (Interlocked.Increment(ref arrived) == count)
            {
                all.TrySetResult();
            }

            await all.Task.WaitAsync(ct);
        };
    }

    private static IServiceCollection Decorate<TService>(this IServiceCollection services, Func<TService, TService> decorate)
        where TService : class
    {
        var registration = services.Single(d => d.ServiceType == typeof(TService));
        services.Remove(registration);
        services.AddSingleton(provider => decorate((TService)ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!)));
        return services;
    }

    private sealed class RecordingAuditLog(List<AuditEvent> events) : IAuditLog
    {
        public void Record(AuditEvent auditEvent, AccountRole role)
        {
            lock (events)
            {
                events.Add(auditEvent);
            }
        }
    }

    private sealed class HookedAppointmentRepository(IAppointmentRepository inner, Func<CancellationToken, Task> afterRead) : IAppointmentRepository
    {
        public Task<IReadOnlyList<AppointmentDetails>> GetActiveForPatientAsync(int patientId, CancellationToken cancellationToken = default) =>
            inner.GetActiveForPatientAsync(patientId, cancellationToken);

        public Task<IReadOnlyList<AppointmentDetails>> GetActiveForDoctorAsync(int doctorId, CancellationToken cancellationToken = default) =>
            inner.GetActiveForDoctorAsync(doctorId, cancellationToken);

        public Task<IReadOnlyList<AppointmentHistoryEntry>> GetHistoryForPatientAsync(int patientId, CancellationToken cancellationToken = default) =>
            inner.GetHistoryForPatientAsync(patientId, cancellationToken);

        public Task<IReadOnlyList<AgendaEntry>> GetAgendaForDoctorAsync(int doctorId, DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken = default) =>
            inner.GetAgendaForDoctorAsync(doctorId, rangeStart, rangeEnd, cancellationToken);

        public Task<IReadOnlyList<DateTime>> GetBookedStartsAsync(int doctorId, DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken = default) =>
            inner.GetBookedStartsAsync(doctorId, rangeStart, rangeEnd, cancellationToken);

        public async Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var appointment = await inner.GetByIdAsync(id, cancellationToken);
            await afterRead(cancellationToken);
            return appointment;
        }

        public Task<int> AddAsync(Appointment appointment, ScheduleCheck checkSchedule, CancellationToken cancellationToken = default) =>
            inner.AddAsync(appointment, checkSchedule, cancellationToken);

        public Task<bool> CancelAsync(int id, AccountRole ownerRole, int ownerId, DateTime startsAfter, DateTime cancelledAtUtc, CancellationToken cancellationToken = default) =>
            inner.CancelAsync(id, ownerRole, ownerId, startsAfter, cancelledAtUtc, cancellationToken);

        public Task<bool> RescheduleAsync(
            int id,
            int patientId,
            DateTime currentStartsAt,
            DateTime newStartsAt,
            DateTime startsAfter,
            DateTime startsNoEarlierThan,
            ScheduleCheck checkSchedule,
            CancellationToken cancellationToken = default) =>
            inner.RescheduleAsync(id, patientId, currentStartsAt, newStartsAt, startsAfter, startsNoEarlierThan, checkSchedule, cancellationToken);
    }
}
