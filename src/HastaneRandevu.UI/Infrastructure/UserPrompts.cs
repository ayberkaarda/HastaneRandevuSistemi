using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI.Infrastructure;

/// <summary>Dialogs a form shows during an operation. Tests replace them so no window opens.</summary>
internal interface IUserPrompts
{
    bool Confirm(string question);

    void Inform(string message);

    void Warn(string message);

    /// <summary>Asks where to save a file; <see langword="null"/> when the user cancelled.</summary>
    string? AskSavePath(string fileName, string filter);
}

/// <summary>Something that can show an inline result or validation message.</summary>
internal interface IStatusSurface
{
    void ShowStatus(StatusKind kind, string message);

    void ClearStatus();
}

/// <summary>Every prompt in a message window (the themed <see cref="MessageDialog"/>, through <see cref="Dialogs"/>).</summary>
internal sealed class MessageBoxPrompts(IWin32Window owner) : IUserPrompts
{
    public bool Confirm(string question) =>
        Dialogs.Show(owner, question, "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public void Inform(string message) =>
        Dialogs.Show(owner, message, AppText.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public void Warn(string message) =>
        Dialogs.Show(owner, message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public string? AskSavePath(string fileName, string filter) => Dialogs.AskSavePath(owner, fileName, filter);
}

/// <summary>
/// Prompts of a screen: results and validation messages appear inline in its message strip, so nothing
/// takes the focus; questions and the save location are asked in windows owned by the screen.
/// </summary>
internal sealed class ScreenPrompts(AppDialog screen) : IUserPrompts
{
    /// <summary>
    /// Where results are shown; the screen itself when not set. A dialog that closes after its work is done
    /// reports the result in the window that opened it.
    /// </summary>
    public IStatusSurface? Results { get; init; }

    public bool Confirm(string question) =>
        Dialogs.Show(screen, question, "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public void Inform(string message) => (Results ?? screen).ShowStatus(StatusKind.Success, message);

    public void Warn(string message) => screen.ShowStatus(StatusKind.Warning, message);

    public string? AskSavePath(string fileName, string filter) => Dialogs.AskSavePath(screen, fileName, filter);
}
