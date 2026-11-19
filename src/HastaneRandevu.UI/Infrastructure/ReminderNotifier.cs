using System.Globalization;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.UI.Infrastructure;

/// <summary>Shows reminders outside the window, without taking the focus.</summary>
public interface IReminderNotifier
{
    void Notify(string title, string text);
}

/// <summary>Used when <c>Appointments:ShowReminderBalloon</c> is false: reminders appear only inside the window.</summary>
internal sealed class NullReminderNotifier : IReminderNotifier
{
    public void Notify(string title, string text)
    {
    }
}

/// <summary>
/// Shows reminders as a balloon from a notification area icon. A balloon never takes the focus or brings
/// a window to the front. The icon is created on the first reminder and removed when disposed.
/// </summary>
internal sealed class NotifyIconReminderNotifier : IReminderNotifier, IDisposable
{
    private NotifyIcon? _icon;

    public void Notify(string title, string text)
    {
        _icon ??= new NotifyIcon { Icon = AppAssets.Icon, Text = AppText.Title, Visible = true };
        _icon.ShowBalloonTip(10_000, title, text, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}

internal static class ReminderText
{
    public const string Title = "Hastane Randevu | Yaklaşan randevu";

    /// <summary>
    /// At most three appointments, one per line, for example "Dr. Ada Örnek - Kardiyoloji - 03.03.2026 10:00",
    /// and the number of the others.
    /// </summary>
    public static string Describe(IReadOnlyList<AppointmentDetails> appointments)
    {
        ArgumentNullException.ThrowIfNull(appointments);

        const int MaxLines = 3;
        var lines = appointments.Take(MaxLines).Select(a => new PatientAppointmentItem(a).ToString());
        var text = string.Join(Environment.NewLine, lines);
        return appointments.Count > MaxLines
            ? text + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, "ve {0} randevu daha", appointments.Count - MaxLines)
            : text;
    }
}
