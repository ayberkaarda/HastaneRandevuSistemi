using System.ComponentModel;

namespace HastaneRandevu.UI.Theming;

/// <summary>How the second line of a list row is colored.</summary>
internal enum RowTone
{
    Neutral,

    /// <summary>A date and time to notice, in the accent text color.</summary>
    Accent,

    /// <summary>Something that needs attention, such as an appointment outside the schedule.</summary>
    Warning,
}

/// <summary>What a list row shows: a primary line and an optional secondary line.</summary>
internal readonly record struct ListRow(string Primary, string? Secondary = null, RowTone Tone = RowTone.Neutral);

/// <summary>
/// A list painted with the theme: one or two lines per row, hairline dividers, an accent edge on the
/// selected row and a message in place of the rows while the list loads or when it is empty. The items,
/// selection, keyboard and accessibility stay those of the native list box (an item's text is its
/// <see cref="object.ToString"/>). Under high contrast selected rows use HighlightText on Highlight.
/// </summary>
internal sealed class ThemedListBox : ListBox
{
    private const int WmPaint = 0x000F;
    private const TextFormatFlags LineFlags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

    private string _emptyText = string.Empty;
    private bool _loading;
    private bool _twoLines = true;

    public ThemedListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        IntegralHeight = false;
        BorderStyle = BorderStyle.None;
        Font = Theme.Fonts.Body;
        Margin = Padding.Empty;
        if (!Theme.Current.IsHighContrast)
        {
            BackColor = Theme.Current.Surface;
            ForeColor = Theme.Current.Text;
        }

        ItemHeight = RowHeight();
    }

    /// <summary>Turns an item into the lines of its row; the item's text is used as a single line when not set.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, ListRow>? Describe { get; set; }

    /// <summary>Whether the rows have a second line; decides the row height.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TwoLines
    {
        get => _twoLines;
        set
        {
            _twoLines = value;
            ItemHeight = RowHeight();
        }
    }

    /// <summary>Shown in the middle of the list while it has no rows.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText
    {
        get => _emptyText;
        set
        {
            _emptyText = value;
            Invalidate();
        }
    }

    /// <summary>Shown instead of <see cref="EmptyText"/> while <see cref="IsLoading"/> is set.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string LoadingText { get; set; } = "Yükleniyor…";

    /// <summary>A list request is on its way; an empty list says so instead of "nothing here".</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsLoading
    {
        get => _loading;
        set
        {
            if (_loading != value)
            {
                _loading = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Background, first line and second line colors of a row. Under high contrast a selected row is
    /// Highlight with HighlightText for both lines, never a palette color that could vanish on it.
    /// </summary>
    internal static (Color Back, Color Primary, Color Secondary) RowColors(ThemePalette palette, bool selected, RowTone tone)
    {
        ArgumentNullException.ThrowIfNull(palette);

        if (palette.IsHighContrast)
        {
            return selected ? (palette.Accent, palette.OnAccent, palette.OnAccent) : (palette.Surface, palette.Text, palette.Text);
        }

        var secondary = tone switch
        {
            RowTone.Accent => palette.AccentText,
            RowTone.Warning => palette.Warning,
            _ => palette.TextSecondary,
        };
        return (selected ? palette.AccentSubtle : palette.Surface, palette.Text, secondary);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ItemHeight = RowHeight();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ItemHeight = RowHeight();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        var p = Theme.Current;
        var g = e.Graphics;
        var item = Items[e.Index];
        var row = Describe?.Invoke(item) ?? new ListRow(GetItemText(item) ?? string.Empty);
        var selected = (e.State & DrawItemState.Selected) != 0;
        var (back, primary, secondary) = RowColors(p, selected, row.Tone);
        using (var brush = new SolidBrush(back))
        {
            g.FillRectangle(brush, e.Bounds);
        }

        if (selected && !p.IsHighContrast)
        {
            using var edge = new SolidBrush(p.Accent);
            g.FillRectangle(edge, e.Bounds.X, e.Bounds.Y, Theme.Px(this, 3), e.Bounds.Height);
        }

        if (!p.IsHighContrast && e.Index < Items.Count - 1)
        {
            using var pen = new Pen(p.Border, Math.Max(1, Theme.Px(this, 1)));
            g.DrawLine(pen, e.Bounds.Left + Theme.Px(this, Spacing.M), e.Bounds.Bottom - 1, e.Bounds.Right - Theme.Px(this, Spacing.M), e.Bounds.Bottom - 1);
        }

        var left = e.Bounds.X + Theme.Px(this, Spacing.M + 2);
        var width = Math.Max(0, e.Bounds.Width - (left - e.Bounds.X) - Theme.Px(this, Spacing.M));
        var strong = Theme.Fonts.BodyStrong;
        if (row.Secondary is { } second && TwoLines)
        {
            var pad = Theme.Px(this, Spacing.S);
            var firstHeight = strong.Height;
            TextRenderer.DrawText(g, row.Primary, strong, new Rectangle(left, e.Bounds.Y + pad, width, firstHeight), primary, back, LineFlags);
            TextRenderer.DrawText(g, second, Theme.Fonts.Caption, new Rectangle(left, e.Bounds.Y + pad + firstHeight, width, Math.Max(0, e.Bounds.Height - firstHeight - (2 * pad))), secondary, back, LineFlags);
        }
        else
        {
            TextRenderer.DrawText(g, row.Primary, Font, new Rectangle(left, e.Bounds.Y, width, e.Bounds.Height), primary, back, LineFlags);
        }

        if ((e.State & DrawItemState.Focus) != 0 && (e.State & DrawItemState.NoFocusRect) == 0 && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(g, e.Bounds, primary, back);
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        // The native list paints nothing without items; the empty or loading message is drawn on top.
        if (m.Msg == WmPaint && Items.Count == 0 && IsHandleCreated)
        {
            using var graphics = CreateGraphics();
            PaintTo(graphics);
        }
    }

    /// <summary>
    /// Paints the empty or loading message into <paramref name="graphics"/> while the list has no items.
    /// DrawToBitmap (used by the offscreen screenshot tool) only prints the native list, so it needs this entry point.
    /// </summary>
    internal void PaintTo(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        var message = _loading ? LoadingText : _emptyText;
        if (Items.Count > 0 || string.IsNullOrEmpty(message))
        {
            return;
        }

        var pad = Theme.Px(this, Spacing.L);
        var area = new Rectangle(pad, pad, Math.Max(0, ClientSize.Width - (2 * pad)), Math.Max(0, ClientSize.Height - (2 * pad)));
        TextRenderer.DrawText(
            graphics,
            message,
            Theme.Fonts.Body,
            area,
            Theme.Current.TextMuted,
            BackColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    private int RowHeight()
    {
        var lines = TwoLines ? Theme.Fonts.BodyStrong.Height + Theme.Fonts.Caption.Height : Font.Height;
        return Math.Min(255, lines + Theme.Px(this, TwoLines ? 2 * Spacing.S : Spacing.M + 2));
    }
}
