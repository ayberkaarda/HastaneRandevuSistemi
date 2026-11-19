using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>Screens with new controls keep every control inside its parent, without overlaps. Nothing is shown.</summary>
public sealed class LayoutTests
{
    [Fact]
    public void DoctorPanel_FitsItsWindow() => Run(() =>
    {
        LayoutAssert.FitsInsideOnEveryScreen(() => TestForms.DoctorPanel(new ControlledAppointmentService()));
    });

    [Fact]
    public void PatientPanel_FitsItsWindow() => Run(() =>
    {
        LayoutAssert.FitsInsideOnEveryScreen(() => TestForms.PatientPanel(new ControlledAppointmentService()));
    });
}
