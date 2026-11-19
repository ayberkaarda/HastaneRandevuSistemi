namespace HastaneRandevu.UI.Infrastructure;

/// <summary>The single place that opens message boxes.</summary>
internal static class Dialogs
{
    /// <summary>
    /// Replaces the message box; the test assembly sets it so that no window can open. Always
    /// <see langword="null"/> in the application.
    /// </summary>
    internal static Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult>? Override { get; set; }

    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None) =>
        Override is { } replacement
            ? replacement(text, caption, buttons, icon)
            : MessageBox.Show(owner, text, caption, buttons, icon);

    /// <summary>
    /// Replaces the save file dialog (file name, filter) and returns the chosen path or
    /// <see langword="null"/>; the test assembly sets it so that no window can open.
    /// </summary>
    internal static Func<string, string, string?>? SaveFileOverride { get; set; }

    /// <summary>Asks where to save a file; <see langword="null"/> when the user cancelled.</summary>
    public static string? AskSavePath(IWin32Window? owner, string fileName, string filter)
    {
        if (SaveFileOverride is { } replacement)
        {
            return replacement(fileName, filter);
        }

        using var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            OverwritePrompt = true,
            AddExtension = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }
}