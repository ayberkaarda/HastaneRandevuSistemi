using System.Text;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Business.Reporting;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HastaneRandevu.Business.Tests;

/// <summary>CSV and HTML exports: masking, encoding, formula and markup injection and atomic writes.</summary>
public sealed class ExportTests : IDisposable
{
    private const string NationalId = "10000000450";
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hastanerandevu-export-test-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));
    private readonly UserSession _patient = new(AccountRole.Patient, 1, "Deniz Örnek", "100******50");
    private readonly UserSession _doctor = new(AccountRole.Doctor, 10, "Dr. Ada Örnek", "100******46");

    public ExportTests() => Directory.CreateDirectory(_directory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("\rx", "\"'\rx\"")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("a\nb", "\"a\nb\"")]
    [InlineData("Dr. Ada", "Dr. Ada")]
    [InlineData(null, "")]
    public void Cell_NeutralisesFormulasAndQuotesSeparators(string? value, string expected) =>
        Assert.Equal(expected, CsvHistoryWriter.Cell(value));

    [Fact]
    public async Task History_IsUtf8WithBomSemicolonsMaskedAndStatusPerRow()
    {
        _appointments.Setup(a => a.GetHistoryForPatientAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AppointmentHistoryEntry(3, "=cmd|' /C calc'!A0", "Kardiyoloji", Now.AddDays(2).Date.AddHours(10), AppointmentStatus.Active, null),
            new AppointmentHistoryEntry(2, "Dr. Can Deneme", null, Now.Date.AddDays(-1).AddHours(9), AppointmentStatus.Active, null),
            new AppointmentHistoryEntry(1, "Dr. Ece Taslak", "Ortopedi", Now.Date.AddDays(1).AddHours(9), AppointmentStatus.Cancelled, Now.AddHours(-3)),
        ]);
        var path = Path.Combine(_directory, "gecmis.csv");

        var count = await Exports().ExportHistoryCsvAsync(_patient, path, Ct);

        var bytes = await File.ReadAllBytesAsync(path, Ct);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split(Environment.NewLine);
        Assert.Equal(3, count);
        Assert.Equal("Hasta;Deniz Örnek", lines[1]);
        Assert.Equal("T.C. Kimlik No;100******50", lines[2]);
        Assert.Equal("Tarih;Saat;Doktor;Branş;Durum;İptal zamanı", lines[5]);
        Assert.Equal("2026-03-04;10:00;'=cmd|' /C calc'!A0;Kardiyoloji;Aktif;", lines[6]);
        Assert.Equal("2026-03-01;09:00;Dr. Can Deneme;;Geçmiş;", lines[7]);
        Assert.Equal("2026-03-03;09:00;Dr. Ece Taslak;Ortopedi;İptal edildi;2026-03-02 07:10", lines[8]);
        Assert.DoesNotContain(NationalId, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(["gecmis.csv"], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Agenda_IsStaticEncodedHtmlWithMaskedIds()
    {
        var day = DateOnly.FromDateTime(Now);
        _appointments.Setup(a => a.GetAgendaForDoctorAsync(10, Now.Date, Now.Date.AddDays(1), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AgendaEntry(1, Now.Date.AddHours(9), "<script>alert('x')</script>", NationalId),
            new AgendaEntry(2, Now.Date.AddHours(9.5), "Mert \"Deneme\" & Oğul", "10000000528"),
        ]);
        var path = Path.Combine(_directory, "ajanda.html");

        Assert.Equal(2, await Exports().ExportAgendaHtmlAsync(_doctor, day, path, Ct));

        var html = await File.ReadAllTextAsync(path, Ct);
        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("Mert &quot;Deneme&quot; &amp; Oğul", html, StringComparison.Ordinal);
        Assert.Contains("<td>09:00</td>", html, StringComparison.Ordinal);
        Assert.Contains("100******50", html, StringComparison.Ordinal);
        Assert.DoesNotContain(NationalId, html, StringComparison.Ordinal);
        Assert.DoesNotContain("10000000528", html, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?i)\son[a-z]+\s*=", html); // no event handler attributes
    }

    [Fact]
    public async Task Agenda_WithoutAppointments_SaysSo()
    {
        _appointments.Setup(a => a.GetAgendaForDoctorAsync(10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var path = Path.Combine(_directory, "bos.html");

        Assert.Equal(0, await Exports().ExportAgendaHtmlAsync(_doctor, DateOnly.FromDateTime(Now), path, Ct));

        Assert.Contains("Bu gün için randevu yok.", await File.ReadAllTextAsync(path, Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedWrite_KeepsTheEarlierFileAndLeavesNoTemporaryFile()
    {
        var path = Path.Combine(_directory, "gecmis.csv");
        await File.WriteAllTextAsync(path, "earlier", Ct);

        await Assert.ThrowsAsync<IOException>(() => ReportFile.WriteAtomicallyAsync(
            path,
            CsvHistoryWriter.Encoding,
            w =>
            {
                w.Write("half");
                throw new IOException("disk full");
            },
            Ct));

        Assert.Equal("earlier", await File.ReadAllTextAsync(path, Ct));
        Assert.Equal(["gecmis.csv"], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Exports_AreOnlyForTheOwnRole()
    {
        var audit = new List<AuditEvent>();
        var exports = Exports(audit);

        await Assert.ThrowsAsync<AccessDeniedException>(() => exports.ExportHistoryCsvAsync(_doctor, Path.Combine(_directory, "a.csv"), Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => exports.ExportAgendaHtmlAsync(_patient, DateOnly.FromDateTime(Now), Path.Combine(_directory, "a.html"), Ct));

        Assert.Equal([AuditEvent.AccessDenied, AuditEvent.AccessDenied], audit);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ExportService Exports(List<AuditEvent>? audit = null) => new(_appointments.Object, _time, new Recorder(audit ?? []));

    private sealed class Recorder(List<AuditEvent> events) : IAuditLog
    {
        public void Record(AuditEvent auditEvent, AccountRole role) => events.Add(auditEvent);
    }
}

/// <summary>Which appointments are announced after sign-in.</summary>
public sealed class ReminderTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0);

    private readonly Mock<IAppointmentRepository> _appointments = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upcoming_AreAfterNowAndWithinTheLeadTimeEarliestFirst()
    {
        var lead = TimeSpan.FromHours(24);
        _appointments.Setup(a => a.GetActiveForPatientAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Details(1, Now + lead + TimeSpan.FromTicks(1)), // just too late
            Details(2, Now + lead),                          // last moment included
            Details(3, Now),                                 // starting now: not upcoming
            Details(4, Now.AddMinutes(20)),
            Details(5, Now.AddDays(-1)),
        ]);

        var upcoming = await Service(new AppointmentPolicy { ReminderLeadHours = 24 }).GetUpcomingAsync(new UserSession(AccountRole.Patient, 1, "H", "***"), Ct);

        Assert.Equal([4, 2], upcoming.Select(a => a.Id));
    }

    [Fact]
    public async Task Doctor_GetsNoPatientReminders()
    {
        await Assert.ThrowsAsync<AccessDeniedException>(
            () => Service(new AppointmentPolicy()).GetUpcomingAsync(new UserSession(AccountRole.Doctor, 1, "Dr.", "***"), Ct));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(168, true)]
    [InlineData(169, false)]
    public void Policy_LeadBetweenOneHourAndAWeek(int hours, bool valid) =>
        Assert.Equal(valid, new AppointmentPolicy { ReminderLeadHours = hours }.Validate().Count == 0);

    private static AppointmentDetails Details(int id, DateTime start) => new(id, 1, "H", 10, "Dr. Ada Örnek", "Kardiyoloji", start);

    private ReminderService Service(AppointmentPolicy policy) => new(_appointments.Object, _time, policy, NullAuditLog.Instance);
}
