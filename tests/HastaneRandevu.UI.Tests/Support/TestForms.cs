using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI.Tests.Support;

/// <summary>Creates the screens with test doubles for every dependency, so no window or icon appears.</summary>
internal static class TestForms
{
    public static MainForm PatientPanel(
        IAppointmentService appointments,
        IUserPrompts? prompts = null,
        IFormFactory? forms = null,
        IAvailabilityService? availability = null,
        IReminderService? reminders = null,
        IExportService? exports = null,
        IReminderNotifier? notifier = null)
    {
        var form = new MainForm(
            appointments,
            availability ?? new ControlledAvailabilityService(),
            reminders ?? new ControlledReminderService(),
            exports ?? new ControlledExportService(),
            notifier ?? new RecordingNotifier(),
            forms ?? new NoFormsFactory(),
            Sessions.Patient);
        if (prompts is not null)
        {
            form.Prompts = prompts;
        }

        return form;
    }

    public static DoktorForm DoctorPanel(IAppointmentService appointments, IUserPrompts? prompts = null, IExportService? exports = null)
    {
        var form = new DoktorForm(appointments, exports ?? new ControlledExportService(), new NoFormsFactory(), Sessions.Doctor);
        if (prompts is not null)
        {
            form.Prompts = prompts;
        }

        return form;
    }
}