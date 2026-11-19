using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>Error messages go through <see cref="Dialogs"/>, which the test assembly replaces, so no window opens.</summary>
public sealed class DialogTests
{
    [Fact]
    public void FailingLoad_ShowsTheFixedMessageThroughTheDialogHelper() => Run(() =>
    {
        var before = NoDialogs.Requested.Count;
        using var form = new MainForm(new FailingAppointmentService(), Sessions.Patient) { Prompts = new RecordingPrompts() };

        var load = form.LoadAsync();

        Assert.True(load.IsCompletedSuccessfully);
        var request = Assert.Single(NoDialogs.Requested.Skip(before));
        Assert.Equal(AppText.Title + ": " + ErrorReporter.UnexpectedErrorMessage, request);
        Assert.DoesNotContain("10000000450", request, StringComparison.Ordinal);
    });

    [Fact]
    public void BusinessRule_IsShownAsAWarningThroughTheDialogHelper()
    {
        var before = NoDialogs.Requested.Count;

        ErrorReporter.Show(new Business.BusinessRuleException("Lütfen bir doktor seçiniz."));

        Assert.Equal("Uyarı: Lütfen bir doktor seçiniz.", Assert.Single(NoDialogs.Requested.Skip(before)));
    }

    private sealed class FailingAppointmentService : IAppointmentService
    {
        // A message that a provider could produce, with personal data in it.
        private static InvalidOperationException Failure() => new("Hasta 10000000450 Deniz Örnek");

        public Task<IReadOnlyList<DoctorSummary>> GetDoctorsAsync(UserSession session, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<DoctorSummary>>(Failure());

        public Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(UserSession session, int doctorId, DateTime day, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<DateTime>>(Failure());

        public Task<IReadOnlyList<AppointmentDetails>> GetMyAppointmentsAsync(UserSession session, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<AppointmentDetails>>(Failure());

        public Task<int> BookAsync(UserSession session, int doctorId, DateTime startsAt, CancellationToken cancellationToken = default) =>
            Task.FromException<int>(Failure());

        public Task CancelAsync(UserSession session, int appointmentId, CancellationToken cancellationToken = default) =>
            Task.FromException(Failure());

        public Task RescheduleAsync(UserSession session, int appointmentId, DateTime newStartsAt, CancellationToken cancellationToken = default) =>
            Task.FromException(Failure());
    }
}
