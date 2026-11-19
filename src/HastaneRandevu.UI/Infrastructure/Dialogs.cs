namespace HastaneRandevu.UI.Infrastructure;

/// <summary>
/// The single place that opens message windows. Questions, refusals and errors use the themed
/// <see cref="MessageDialog"/>; results and validation messages of a screen are shown inline instead
/// (see <see cref="IStatusSurface"/>).
/// </summary>
internal static class Dialogs
{
    /// <summary>
    /// Replaces the message window; the test assembly and the offscreen screenshot tool set it so that no
    /// window can open. Always <see langword="null"/> in the application.
    /// </summary>
    internal static Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult>? Override { get; set; }

    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None)
    {
        if (Override is { } replacement)
        {
            return replacement(text, caption, buttons, icon);
        }

        // A window needs an STA thread; errors reported from a worker thread fall back to the system box.
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return MessageBox.Show(owner, text, caption, buttons, icon);
        }

        using var dialog = new MessageDialog(text, caption, buttons, icon);
        return dialog.ShowDialog(owner);
    }

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
