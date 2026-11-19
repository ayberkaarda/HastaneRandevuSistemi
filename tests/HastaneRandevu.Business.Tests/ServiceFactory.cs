using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Tests;

/// <summary>Builds the services with the real validators, as the application registers them.</summary>
internal static class ServiceFactory
{
    public static AuthService Auth(
        IAccountRepository accounts,
        IPatientRepository patients,
        IPasswordHasher hasher,
        TimeProvider time,
        IAuditLog? audit = null) =>
        new(
            accounts,
            patients,
            hasher,
            time,
            new RegistrationInputValidator(),
            new PatientSignInInputValidator(),
            new DoctorSignInInputValidator(),
            audit ?? NullAuditLog.Instance);

    /// <param name="schedules">Doctor schedules; when not given every doctor works the default hours.</param>
    public static AppointmentService Appointments(
        IAppointmentRepository appointments,
        IDoctorRepository doctors,
        TimeProvider time,
        IAuditLog? audit = null,
        IDoctorScheduleRepository? schedules = null,
        AppointmentPolicy? policy = null) =>
        new(
            appointments,
            doctors,
            schedules ?? new InMemoryScheduleRepository(),
            time,
            new BookingInputValidator(),
            new CancellationInputValidator(),
            new RescheduleInputValidator(),
            policy ?? new AppointmentPolicy(),
            audit ?? NullAuditLog.Instance);

    public static ScheduleService Schedules(
        IDoctorScheduleRepository schedules,
        IAppointmentRepository appointments,
        TimeProvider time,
        IAuditLog? audit = null) =>
        new(
            schedules,
            appointments,
            time,
            new WeeklyHoursInputValidator(),
            new LeaveDayInputValidator(),
            audit ?? NullAuditLog.Instance);
}
