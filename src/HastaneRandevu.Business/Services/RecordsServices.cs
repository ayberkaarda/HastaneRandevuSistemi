using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Reporting;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;

namespace HastaneRandevu.Business.Services;

/// <summary>Upcoming appointments the signed-in patient should be reminded of.</summary>
public interface IReminderService
{
    /// <summary>
    /// The patient's active appointments that start after now and within
    /// <see cref="AppointmentPolicy.ReminderLeadHours"/>, earliest first.
    /// </summary>
    Task<IReadOnlyList<AppointmentDetails>> GetUpcomingAsync(UserSession session, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exports of the signed-in account's own data: the patient's appointment history as CSV and the doctor's
/// agenda of one day as printable HTML. National ids are masked in both; nothing of the content is logged.
/// </summary>
public interface IExportService
{
    /// <summary>Writes the patient's whole history to <paramref name="path"/>; returns the number of appointments.</summary>
    Task<int> ExportHistoryCsvAsync(UserSession session, string path, CancellationToken cancellationToken = default);

    /// <summary>Writes the doctor's active appointments of <paramref name="day"/> to <paramref name="path"/>; returns their number.</summary>
    Task<int> ExportAgendaHtmlAsync(UserSession session, DateOnly day, string path, CancellationToken cancellationToken = default);
}

internal sealed class ReminderService(
    IAppointmentRepository appointments,
    TimeProvider timeProvider,
    AppointmentPolicy policy,
    IAuditLog audit) : IReminderService
{
    public async Task<IReadOnlyList<AppointmentDetails>> GetUpcomingAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Patient, audit);

        var now = timeProvider.GetLocalNow().DateTime;
        var until = now + policy.ReminderLead;
        var active = await appointments.GetActiveForPatientAsync(session.AccountId, cancellationToken).ConfigureAwait(false);
        return active
            .Where(a => a.StartsAt > now && a.StartsAt <= until)
            .OrderBy(a => a.StartsAt)
            .ThenBy(a => a.Id)
            .ToList();
    }
}

internal sealed class ExportService(
    IAppointmentRepository appointments,
    TimeProvider timeProvider,
    IAuditLog audit) : IExportService
{
    internal const string Active = "Aktif";
    internal const string Past = "Geçmiş";
    internal const string Cancelled = "İptal edildi";

    public async Task<int> ExportHistoryCsvAsync(UserSession session, string path, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Patient, audit);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var now = timeProvider.GetLocalNow().DateTime;
        var history = await appointments.GetHistoryForPatientAsync(session.AccountId, cancellationToken).ConfigureAwait(false);
        var report = new HistoryReport(
            session.DisplayName,
            session.MaskedNationalId,
            now,
            history.Select(h => new HistoryRow(
                h.StartsAt,
                h.DoctorName,
                h.Branch,
                h.Status == AppointmentStatus.Cancelled ? Cancelled : h.StartsAt > now ? Active : Past,
                h.CancelledAt is { } utc ? ToLocal(utc) : null)).ToList());

        await ReportFile.WriteAtomicallyAsync(path, CsvHistoryWriter.Encoding, w => CsvHistoryWriter.Write(report, w), cancellationToken)
            .ConfigureAwait(false);
        return report.Rows.Count;
    }

    public async Task<int> ExportAgendaHtmlAsync(UserSession session, DateOnly day, string path, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Doctor, audit);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var start = day.ToDateTime(TimeOnly.MinValue);
        var entries = await appointments.GetAgendaForDoctorAsync(session.AccountId, start, start.AddDays(1), cancellationToken)
            .ConfigureAwait(false);
        var report = new AgendaReport(
            session.DisplayName,
            day,
            timeProvider.GetLocalNow().DateTime,
            entries.Select(e => new AgendaRow(e.StartsAt, e.PatientName, NationalIdNumber.Mask(e.PatientNationalId))).ToList());

        await ReportFile.WriteAtomicallyAsync(path, HtmlAgendaWriter.Encoding, w => HtmlAgendaWriter.Write(report, w), cancellationToken)
            .ConfigureAwait(false);
        return report.Rows.Count;
    }

    private DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeProvider.LocalTimeZone);
}
