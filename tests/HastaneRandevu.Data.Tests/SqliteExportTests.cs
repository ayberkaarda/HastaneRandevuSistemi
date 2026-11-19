using System.Text;
using HastaneRandevu.Business.Services;

namespace HastaneRandevu.Data.Tests;

/// <summary>History and agenda exports with the real repositories on SQLite in-memory.</summary>
public sealed class SqliteExportTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hastanerandevu-export-data-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return _db.InitializeAsync();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task History_ContainsOnlyTheOwnAppointmentsCancelledOnesIncluded()
    {
        var appointments = _db.Get<IAppointmentService>();
        var kept = await appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10, Ct);
        var cancelled = await appointments.BookAsync(TestServices.Patient(1), 2, TestServices.Tomorrow10, Ct);
        await appointments.CancelAsync(TestServices.Patient(1), cancelled, Ct);
        await appointments.BookAsync(TestServices.Patient(2), 3, TestServices.Tomorrow10, Ct);
        var path = Path.Combine(_directory, "gecmis.csv");

        var count = await _db.Get<IExportService>().ExportHistoryCsvAsync(TestServices.Patient(1), path, Ct);

        var text = await File.ReadAllTextAsync(path, Encoding.UTF8, Ct);
        Assert.Equal(2, count);
        Assert.Contains("Dr. Ada Örnek;Kardiyoloji;Aktif", text, StringComparison.Ordinal);
        Assert.Contains("Dr. Can Deneme;Dahiliye;İptal edildi;2026-03-02 10:10", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ece Taslak", text, StringComparison.Ordinal);
        Assert.DoesNotContain("10000000450", text, StringComparison.Ordinal);
        Assert.True(kept > 0);
    }

    [Fact]
    public async Task Agenda_ListsTheDoctorsActiveAppointmentsOfThatDayWithMaskedIds()
    {
        var appointments = _db.Get<IAppointmentService>();
        await appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10, Ct);
        await appointments.BookAsync(TestServices.Patient(2), 1, TestServices.Tomorrow10.AddHours(1), Ct);
        var cancelled = await appointments.BookAsync(TestServices.Patient(2), 1, TestServices.Tomorrow10.AddHours(2), Ct);
        await appointments.CancelAsync(TestServices.Patient(2), cancelled, Ct);
        await appointments.BookAsync(TestServices.Patient(1), 1, TestServices.Tomorrow10.AddDays(1), Ct); // another day
        await appointments.BookAsync(TestServices.Patient(1), 2, TestServices.Tomorrow10.AddHours(3), Ct); // another doctor
        var path = Path.Combine(_directory, "ajanda.html");

        var count = await _db.Get<IExportService>().ExportAgendaHtmlAsync(
            TestServices.Doctor(1), DateOnly.FromDateTime(TestServices.Tomorrow10), path, Ct);

        var html = await File.ReadAllTextAsync(path, Ct);
        Assert.Equal(2, count);
        Assert.Contains("<td>10:00</td><td>Deniz Örnek</td><td>100******50</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>11:00</td><td>Mert Deneme</td><td>100******28</td>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("12:00", html, StringComparison.Ordinal);
        Assert.DoesNotContain("13:00", html, StringComparison.Ordinal);
    }
}
