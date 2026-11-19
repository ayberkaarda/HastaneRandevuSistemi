using System.ComponentModel;
using System.Globalization;
using HastaneRandevu.Business.Services;

namespace HastaneRandevu.UI.Theming;

/// <summary>
/// A day of the doctor's calendar. It stays a real <see cref="Button"/> (Tab, Enter, Space, the accessible
/// name and the text that describes the day are unchanged) and paints the day as a small tile: the day
/// number, the free slots or the reason the day is closed, and a bar of the free share of the day.
/// </summary>
internal sealed class DayButton : Button
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private bool _hover;

    public DayButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        UseMnemonic = false;
        Margin = new Padding(3);
        MinimumSize = new Size(0, Sizes.CalendarDayHeight);
        Font = Theme.Fonts.Body;
    }

    /// <summary>Whether the day belongs to the month on screen; days of the neighbouring months are dimmed.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool InPeriod { get; set; } = true;

    private CalendarDay? Day => Tag as CalendarDay;

    /// <summary>Fill, border, number and detail colors of a day tile.</summary>
    internal static (Color Fill, Color? Border, Color Number, Color Detail) Colors(ThemePalette palette, CalendarDayStatus? status, bool enabled, bool hover)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var p = palette;

        if (p.IsHighContrast)
        {
            // The system pair for the hovered or focused choice; everything else in the window colors.
            return hover && enabled
                ? (p.Accent, p.Border, p.OnAccent, p.OnAccent)
                : (p.Surface, enabled ? p.Border : p.TextDisabled, enabled ? p.Text : p.TextDisabled, enabled ? p.Text : p.TextDisabled);
        }

        return status switch
        {
            CalendarDayStatus.Available => (hover ? p.AccentSubtle : p.Surface, hover ? p.Accent : p.Border, p.Text, p.AccentText),
            CalendarDayStatus.Full => (p.SurfaceAlt, null, p.TextSecondary, p.TextMuted),
            CalendarDayStatus.Leave => (p.WarmSubtle, null, p.WarmText, p.WarmText),
            CalendarDayStatus.NotWorking => (p.SurfaceAlt, null, p.TextMuted, p.TextMuted),
            _ => (Color.Transparent, null, p.TextDisabled, p.TextDisabled),
        };
    }

    /// <summary>The short line under the day number.</summary>
    internal static string Detail(CalendarDay day)
    {
        ArgumentNullException.ThrowIfNull(day);

        return day.Status switch
        {
            CalendarDayStatus.Available => string.Format(Turkish, "{0}/{1} boş", day.FreeSlots, day.TotalSlots),
            CalendarDayStatus.Full => "Dolu",
            CalendarDayStatus.Leave => "İzinli",
            CalendarDayStatus.NotWorking => "Çalışmıyor",
            _ => string.Empty,
        };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _hover = false;
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);

        var p = Theme.Current;
        var g = pevent.Graphics;
        g.Clear(Painting.ParentBackground(this));
        var day = Day;
        var focused = Focused && ShowFocusCues;
        var (fill, border, number, detail) = Colors(p, day?.Status, Enabled, _hover || focused);
        var bounds = new RectangleF(0, 0, Width, Height);
        var radius = Theme.PxF(this, Radius.Control + 2);
        Painting.FillRounded(g, bounds, radius, fill, border, Math.Max(1F, Theme.PxF(this, 1)));
        if (focused && !p.IsHighContrast)
        {
            Painting.FillRounded(g, bounds, radius, Color.Transparent, p.Accent, Theme.PxF(this, Sizes.FocusRing));
        }

        if (day is null)
        {
            return;
        }

        if (!InPeriod && !p.IsHighContrast)
        {
            number = p.TextDisabled;
        }

        var pad = Theme.Px(this, Spacing.S);
        var numberFont = Theme.Fonts.Value;
        var numberText = day.Date.Day.ToString(Turkish);
        var numberBounds = new Rectangle(pad, pad - Theme.Px(this, 2), Math.Max(0, Width - (2 * pad)), numberFont.Height);
        TextRenderer.DrawText(g, numberText, numberFont, numberBounds, number, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        // Today: a small warm dot after the number.
        if (day.Date == DateOnly.FromDateTime(DateTime.Today))
        {
            var width = TextRenderer.MeasureText(g, numberText, numberFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            var size = Theme.PxF(this, 6);
            var dot = new RectangleF(pad + width + Theme.PxF(this, 6), numberBounds.Top + ((numberFont.Height - size) / 2F), size, size);
            Painting.FillDot(g, dot, p.IsHighContrast ? number : p.Warm);
        }

        var caption = Theme.Fonts.Caption;
        var text = Detail(day);
        var barHeight = Theme.Px(this, 4);
        var available = day.Status == CalendarDayStatus.Available;

        // A small tile keeps its detail at the bottom; a tall one (the week view) under the number.
        var tall = Height > Theme.Px(this, Sizes.CalendarDayHeight * 2);
        var detailTop = tall
            ? numberBounds.Bottom + Theme.Px(this, 4)
            : Math.Max(numberBounds.Bottom, Height - pad - caption.Height - (available ? barHeight + Theme.Px(this, 4) : 0));
        TextRenderer.DrawText(g, text, caption, new Rectangle(pad, detailTop, Math.Max(0, Width - (2 * pad)), caption.Height), detail, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        // The free share of the day as a thin bar, so busy and quiet days read at a glance.
        if (available && day.TotalSlots > 0 && !p.IsHighContrast)
        {
            var barTop = tall ? detailTop + caption.Height + Theme.Px(this, 6) : Height - pad - barHeight;
            var track = new RectangleF(pad, barTop, Math.Max(0, Width - (2 * pad)), barHeight);
            Painting.FillRounded(g, track, barHeight / 2F, p.AccentSubtle);
            var share = Math.Clamp(day.FreeSlots / (float)day.TotalSlots, 0.08F, 1F);
            Painting.FillRounded(g, new RectangleF(track.X, track.Y, track.Width * share, track.Height), barHeight / 2F, p.Accent);
        }
    }
}

/// <summary>
/// One option of a small segmented choice (for example month or week). It stays a real
/// <see cref="RadioButton"/>, so the arrow keys and screen readers treat the options as one group.
/// Under high contrast the native button look is used.
/// </summary>
internal sealed class SegmentButton : RadioButton
{
    private bool _hover;

    public SegmentButton()
    {
        Appearance = Appearance.Button;
        AutoSize = false;
        TextAlign = ContentAlignment.MiddleCenter;
        Font = Theme.Fonts.BodyStrong;
        Margin = new Padding(0, 0, Spacing.XS, 0);
        Size = new Size(Sizes.ButtonMinWidth - 16, Sizes.ControlHeight);
        Cursor = Cursors.Hand;
        if (Theme.Current.IsHighContrast)
        {
            FlatStyle = FlatStyle.System;
            return;
        }

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
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

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
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
        var (fill, border, text) = Checked
            ? (p.AccentSubtle, p.Accent, p.AccentText)
            : (_hover ? p.SurfaceHover : p.Surface, p.BorderStrong, p.Text);
        var radius = Theme.PxF(this, Radius.Control);
        var bounds = new RectangleF(0, 0, Width, Height);
        Painting.FillRounded(g, bounds, radius, fill, border, Math.Max(1F, Theme.PxF(this, Checked ? 1.5F : 1F)));
        if (Focused && ShowFocusCues)
        {
            Painting.FillRounded(g, RectangleF.Inflate(bounds, -Theme.PxF(this, 3), -Theme.PxF(this, 3)), radius, Color.Transparent, p.Accent, Theme.PxF(this, 1.5F));
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
    }
}

/// <summary>A small rounded color sample for a legend, for example the color of free days.</summary>
internal sealed class Swatch : Control
{
    public Swatch(CalendarDayStatus status)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        Status = status;
        TabStop = false;
        Size = new Size(14, 14);
        Margin = new Padding(0, 0, Spacing.XS + 2, 0);
        AccessibleRole = AccessibleRole.Graphic;
    }

    public CalendarDayStatus Status { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var p = Theme.Current;
        e.Graphics.Clear(Painting.ParentBackground(this));
        var (fill, border, _, detail) = DayButton.Colors(p, Status, enabled: true, hover: false);
        var bounds = new RectangleF(0, 0, Width, Height);
        Painting.FillRounded(e.Graphics, bounds, Theme.PxF(this, 3), fill, Status == CalendarDayStatus.Available ? p.BorderStrong : border ?? detail, Math.Max(1F, Theme.PxF(this, 1)));

        // A free day is a white tile with the accent bar of its free share; the sample shows the bar too.
        if (Status == CalendarDayStatus.Available && !p.IsHighContrast)
        {
            var bar = Theme.PxF(this, 3);
            using var brush = new SolidBrush(p.Accent);
            e.Graphics.FillRectangle(brush, Theme.PxF(this, 3), Height - bar - Theme.PxF(this, 3), Width - Theme.PxF(this, 6), bar);
        }
    }
}
