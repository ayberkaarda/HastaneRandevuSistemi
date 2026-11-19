using System.Globalization;
using System.Text;

namespace HastaneRandevu.Business.Reporting;

/// <summary>A patient's appointment history ready to export; the national id is already masked.</summary>
public sealed record HistoryReport(string PatientName, string MaskedNationalId, DateTime GeneratedAt, IReadOnlyList<HistoryRow> Rows);

/// <summary>One appointment of the history; times are local.</summary>
public sealed record HistoryRow(DateTime StartsAt, string DoctorName, string? Branch, string Status, DateTime? CancelledAt);

/// <summary>A doctor's appointments of one day ready to print; national ids are already masked.</summary>
public sealed record AgendaReport(string DoctorName, DateOnly Day, DateTime GeneratedAt, IReadOnlyList<AgendaRow> Rows);

/// <summary>One appointment of the agenda.</summary>
public sealed record AgendaRow(DateTime StartsAt, string PatientName, string MaskedNationalId);

/// <summary>
/// Writes the history as CSV for spreadsheet programs: UTF-8 with a byte order mark and ";" as separator,
/// which Excel in Turkish opens directly. Every cell goes through <see cref="Cell"/>, which neutralises
/// formulas.
/// </summary>
public static class CsvHistoryWriter
{
    public const char Separator = ';';

    /// <summary>UTF-8 with a byte order mark, so spreadsheet programs detect the encoding.</summary>
    public static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// Makes one CSV cell safe. A value starting with "=", "+", "-", "@", a tab or a carriage return
    /// would be run as a formula by spreadsheet programs (CSV/formula injection), so it gets a leading
    /// apostrophe. Values containing the separator, quotes or line breaks are quoted, quotes doubled.
    /// </summary>
    public static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }

        if (text.IndexOfAny([Separator, '"', '\r', '\n']) >= 0)
        {
            text = "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }

    public static void Write(HistoryReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        Line(writer, "Hastane Randevu - randevu geçmişi");
        Line(writer, "Hasta", report.PatientName);
        Line(writer, "T.C. Kimlik No", report.MaskedNationalId);
        Line(writer, "Oluşturulma", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        writer.WriteLine();
        Line(writer, "Tarih", "Saat", "Doktor", "Branş", "Durum", "İptal zamanı");
        foreach (var row in report.Rows)
        {
            Line(
                writer,
                row.StartsAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                row.DoctorName,
                row.Branch,
                row.Status,
                row.CancelledAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        }
    }

    private static void Line(TextWriter writer, params string?[] cells) =>
        writer.WriteLine(string.Join(Separator, cells.Select(Cell)));
}

/// <summary>
/// Writes the agenda as one self-contained HTML page for printing. It has no scripts, a content security
/// policy that allows nothing but the inline style, and every value is HTML encoded.
/// </summary>
public static class HtmlAgendaWriter
{
    public const string ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static void Write(AgendaReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        var day = report.Day.ToString("dd.MM.yyyy dddd", Turkish);
        writer.WriteLine("<!DOCTYPE html>");
        writer.WriteLine("<html lang=\"tr\">");
        writer.WriteLine("<head>");
        writer.WriteLine("<meta charset=\"utf-8\">");
        writer.WriteLine($"<meta http-equiv=\"Content-Security-Policy\" content=\"{ContentSecurityPolicy}\">");
        writer.WriteLine($"<title>{E($"Günlük ajanda - {report.DoctorName} - {day}")}</title>");
        writer.WriteLine("<style>");
        writer.WriteLine("body{font-family:'Segoe UI',Arial,sans-serif;margin:24px;color:#000;background:#fff}");
        writer.WriteLine("h1{font-size:20px;margin:0 0 4px}p{margin:0 0 16px}");
        writer.WriteLine("table{border-collapse:collapse;width:100%}th,td{border:1px solid #666;padding:6px 8px;text-align:left}");
        writer.WriteLine("th{background:#eee}@media print{body{margin:0}}");
        writer.WriteLine("</style>");
        writer.WriteLine("</head>");
        writer.WriteLine("<body>");
        writer.WriteLine($"<h1>{E("Günlük ajanda")}</h1>");
        writer.WriteLine($"<p>{E(report.DoctorName)} &middot; {E(day)} &middot; {E("Oluşturulma: " + report.GeneratedAt.ToString("dd.MM.yyyy HH:mm", Turkish))}</p>");
        if (report.Rows.Count == 0)
        {
            writer.WriteLine($"<p>{E("Bu gün için randevu yok.")}</p>");
        }
        else
        {
            writer.WriteLine($"<table><thead><tr><th>{E("Saat")}</th><th>{E("Hasta")}</th><th>{E("T.C. Kimlik No")}</th></tr></thead><tbody>");
            foreach (var row in report.Rows)
            {
                writer.WriteLine(
                    $"<tr><td>{E(row.StartsAt.ToString("HH:mm", Turkish))}</td><td>{E(row.PatientName)}</td><td>{E(row.MaskedNationalId)}</td></tr>");
            }

            writer.WriteLine("</tbody></table>");
        }

        writer.WriteLine("</body>");
        writer.WriteLine("</html>");
    }

/// <summary>
    /// Encodes the five characters that are markup in text and attributes. Turkish letters stay readable;
    /// the page is UTF-8.
    /// </summary>
    internal static string E(string? value) =>
        (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
}

/// <summary>
/// Saves an export through a temporary file in the target folder that is moved over the target only
/// when it is complete, so a failure never leaves a half-written file or destroys an earlier one.
/// </summary>
public static class ReportFile
{
    public static async Task WriteAtomicallyAsync(
        string path,
        Encoding encoding,
        Action<TextWriter> write,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentNullException.ThrowIfNull(write);

        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target) ?? throw new ArgumentException("The path has no folder.", nameof(path));

        // Same folder, so the final move is a rename on the same volume.
        var temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                var writer = new StreamWriter(stream, encoding);
                await using (writer.ConfigureAwait(false))
                {
                    write(writer);
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // Best effort; the original error is more useful to the caller.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
