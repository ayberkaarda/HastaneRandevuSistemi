using System.Globalization;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Domain.Entities;
using Serilog;
using Serilog.Core;

namespace HastaneRandevu.UI.Infrastructure;

/// <summary>
/// File logging for diagnostics. Logs stay on the user's machine, outside the repository. National
/// ids, names, passwords, appointment details and exception messages are never written: error entries
/// come from <see cref="ErrorReporter.FormatForLog"/> and audit entries are fixed codes from
/// <see cref="AuditCodes"/>.
/// </summary>
internal static class AppLogging
{
    internal const int RetainedDays = 30;

    /// <summary>%LOCALAPPDATA%\HastaneRandevu\logs</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HastaneRandevu",
        "logs");

    /// <summary>Creates a logger that writes one file per day and keeps the last 30 files.</summary>
    public static Logger CreateLogger(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(directory, "hastanerandevu-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}")
            .CreateLogger();
    }
}

/// <summary>
/// Writes audit events as a fixed code and a role, for example <c>Audit auth.account.locked patient</c>.
/// Nothing that identifies an account or an appointment is available here, so nothing can leak.
/// </summary>
internal sealed class SerilogAuditLog(ILogger logger) : IAuditLog
{
    public void Record(AuditEvent auditEvent, AccountRole role) =>
        logger.Information("Audit {Code} {Role}", AuditCodes.Of(auditEvent), AuditCodes.Of(role));
}
