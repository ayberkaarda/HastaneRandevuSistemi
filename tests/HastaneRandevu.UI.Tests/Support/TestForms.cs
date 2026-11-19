using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI.Tests.Support;

/// <summary>Creates the screens with test doubles for every dependency, so no window or icon appears.</summary>
internal static class TestForms
{
    public static MainForm PatientPanel(IAppointmentService appointments, IUserPrompts? prompts = null, IFormFactory? forms = null)
    {
        var form = new MainForm(appointments, forms ?? new NoFormsFactory(), Sessions.Patient);
        if (prompts is not null)
        {
            form.Prompts = prompts;
        }

        return form;
    }
}
