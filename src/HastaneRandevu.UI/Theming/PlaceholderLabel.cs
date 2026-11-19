using System.ComponentModel;

namespace HastaneRandevu.UI.Theming;

/// <summary>
/// A label that shows a muted placeholder while its text is empty, for example "no upcoming appointment"
/// in place of a reminder. The text stays exactly what the screen set (empty when there is nothing to
/// show); the placeholder is only painted and given to screen readers as the description.
/// </summary>
internal sealed class PlaceholderLabel : Label
{
    private string _placeholder = string.Empty;

    public PlaceholderLabel()
    {
        AutoSize = true;
        UseMnemonic = false;
        Font = Theme.Fonts.Body;
        ForeColor = Theme.Current.Text;
        Margin = Padding.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            AccessibleDescription = value;
            Invalidate();
        }
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        Text.Length == 0 && _placeholder.Length > 0
            ? TextRenderer.MeasureText(_placeholder, Font, new Size(proposedSize.Width > 1 ? proposedSize.Width : int.MaxValue, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix)
            : base.GetPreferredSize(proposedSize);

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (Text.Length > 0 || _placeholder.Length == 0)
        {
            base.OnPaint(e);
            return;
        }

        TextRenderer.DrawText(e.Graphics, _placeholder, Font, ClientRectangle, Theme.Current.TextMuted, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
