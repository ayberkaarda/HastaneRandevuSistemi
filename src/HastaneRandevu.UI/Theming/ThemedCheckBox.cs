namespace HastaneRandevu.UI.Theming;

/// <summary>
/// A check box painted with the theme: a rounded box in the accent color when checked and a focus ring
/// around the box. Space, Tab, the access key and screen readers stay those of the native check box.
/// Under high contrast the native look is kept.
/// </summary>
internal sealed class ThemedCheckBox : CheckBox, IThemeAware
{
    private const int Box = 18;
    private const int Gap = 8;

    /// <summary>Room left of the box for its focus ring.</summary>
    private const int Inset = 3;

    private bool _hover;

    public ThemedCheckBox()
    {
        AutoSize = true;
        Font = Theme.Fonts.Body;
        Cursor = Cursors.Hand;
        ApplyStyles();
    }

    /// <summary>Native drawing under high contrast, custom drawing otherwise.</summary>
    void IThemeAware.ApplyTheme(ThemeRemap remap)
    {
        ApplyStyles();
        AutoSize = false;
        AutoSize = true;
        Invalidate();
    }

    private void ApplyStyles() =>
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, !Theme.Current.IsHighContrast);

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Theme.Current.IsHighContrast)
        {
            return base.GetPreferredSize(proposedSize);
        }

        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix);
        return new Size(Theme.Px(this, Inset + Box + Gap) + text.Width + Theme.Px(this, 2), Math.Max(text.Height, Theme.Px(this, Box + 4)));
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);
        if (Theme.Current.IsHighContrast)
        {
            base.OnPaint(pevent);
            return;
        }

        var p = Theme.Current;
        var g = pevent.Graphics;
        g.Clear(Painting.ParentBackground(this));

        var size = Theme.PxF(this, Box);
        var box = new RectangleF(Theme.PxF(this, Inset), (Height - size) / 2F, size, size);
        var radius = Theme.PxF(this, 4);
        var (fill, border) = !Enabled
            ? (p.SurfaceAlt, p.Border)
            : Checked ? (_hover ? p.AccentHover : p.Accent, (Color?)null) : (p.Surface, _hover ? p.Accent : p.BorderStrong);
        Painting.FillRounded(g, box, radius, fill, border, Math.Max(1F, Theme.PxF(this, 1.5F)));
        if (Checked)
        {
            Painting.DrawGlyph(g, Glyphs.CheckMark, Theme.Fonts.Glyph(9F), Rectangle.Round(box), Enabled ? p.OnAccent : p.TextDisabled);
        }

        if (Focused && ShowFocusCues)
        {
            Painting.FillRounded(g, RectangleF.Inflate(box, Theme.PxF(this, 2), Theme.PxF(this, 2)), radius + Theme.PxF(this, 2), Color.Transparent, p.Accent, Theme.PxF(this, 1.5F));
        }

        var left = Theme.Px(this, Inset + Box + Gap);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            new Rectangle(left, 0, Math.Max(0, Width - left), Height),
            Enabled ? p.Text : p.TextDisabled,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
    }
}
