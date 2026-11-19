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
        TimeProvider time) =>
        new(
            accounts,
            patients,
            hasher,
            time,
            new RegistrationInputValidator(),
            new PatientSignInInputValidator(),
            new DoctorSignInInputValidator());

    public static AppointmentService Appointments(
        IAppointmentRepository appointments,
        IDoctorRepository doctors,
        TimeProvider time) =>
        new(appointments, doctors, time, new BookingInputValidator(), new CancellationInputValidator());
}
