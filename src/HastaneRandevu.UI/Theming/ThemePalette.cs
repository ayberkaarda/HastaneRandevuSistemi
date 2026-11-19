namespace HastaneRandevu.UI.Theming;

/// <summary>
/// Named colors of one theme. Controls never use literal colors; they read a role from the current
/// palette, so the light, dark and high contrast themes stay consistent.
/// </summary>
internal sealed record ThemePalette
{
    public required string Name { get; init; }

    public bool IsDark { get; init; }

    /// <summary>Built from the Windows high contrast colors; custom painting steps back for native drawing.</summary>
    public bool IsHighContrast { get; init; }

    /// <summary>Window background behind cards: a cool, slightly teal tinted grey.</summary>
    public required Color Page { get; init; }

    /// <summary>Cards, inputs, header and footer.</summary>
    public required Color Surface { get; init; }

    /// <summary>Quiet areas: disabled inputs, days off and the footer of the message dialog.</summary>
    public required Color SurfaceAlt { get; init; }

    public required Color SurfaceHover { get; init; }

    /// <summary>Decorative hairlines between areas.</summary>
    public required Color Border { get; init; }

    /// <summary>Borders of inputs; at least 3:1 against the surface.</summary>
    public required Color BorderStrong { get; init; }

    public required Color Text { get; init; }

    public required Color TextSecondary { get; init; }

    public required Color TextMuted { get; init; }

    public required Color TextDisabled { get; init; }

    /// <summary>The deep clinical teal of the logo: primary actions and the brand panel.</summary>
    public required Color Accent { get; init; }

    public required Color AccentHover { get; init; }

    public required Color AccentPressed { get; init; }

    /// <summary>Text and glyphs on <see cref="Accent"/>.</summary>
    public required Color OnAccent { get; init; }

    /// <summary>Secondary text on <see cref="Accent"/>, for example the tagline of the brand panel.</summary>
    public required Color OnAccentMuted { get; init; }

    /// <summary>Tinted background for selected rows, free calendar days and glyph badges.</summary>
    public required Color AccentSubtle { get; init; }

    /// <summary>Accent used as text or a glyph on <see cref="Surface"/>.</summary>
    public required Color AccentText { get; init; }

    /// <summary>
    /// The warm apricot of the logo's clock: a small mark for what is coming up (reminders, leave days, the
    /// brand line). Decorative only; text in this family uses <see cref="WarmText"/>.
    /// </summary>
    public required Color Warm { get; init; }

    public required Color WarmSubtle { get; init; }

    public required Color WarmText { get; init; }

    public required Color Success { get; init; }

    public required Color SuccessSubtle { get; init; }

    public required Color Warning { get; init; }

    public required Color WarningSubtle { get; init; }

    public required Color Danger { get; init; }

    public required Color DangerSubtle { get; init; }

    public required Color Info { get; init; }

    public required Color InfoSubtle { get; init; }

    /// <summary>
    /// Light theme: calm, cool neutrals with a deep teal accent and a thin warm apricot mark. The accent sits
    /// far from the red danger role, so cancelling never looks like the primary action, and it avoids the
    /// red and blue of public health services.
    /// </summary>
    public static ThemePalette Light { get; } = new()
    {
        Name = "Light",
        Page = Hex(0xF1F5F5),
        Surface = Hex(0xFFFFFF),
        SurfaceAlt = Hex(0xF5F8F8),
        SurfaceHover = Hex(0xEAF1F1),
        Border = Hex(0xD9E2E2),
        BorderStrong = Hex(0x7E8C8D),
        Text = Hex(0x132225),
        TextSecondary = Hex(0x3F4F53),
        TextMuted = Hex(0x566669),
        TextDisabled = Hex(0x9AA7A9),
        Accent = Hex(0x0B5F6E),
        AccentHover = Hex(0x094F5C),
        AccentPressed = Hex(0x07424D),
        OnAccent = Hex(0xFFFFFF),
        OnAccentMuted = Hex(0xCDE6E9),
        AccentSubtle = Hex(0xE1EFF0),
        AccentText = Hex(0x0B5F6E),
        Warm = Hex(0xE8924A),
        WarmSubtle = Hex(0xFCEEDF),
        WarmText = Hex(0x8A4A12),
        Success = Hex(0x1B6B47),
        SuccessSubtle = Hex(0xE4F2EA),
        Warning = Hex(0x7A4B00),
        WarningSubtle = Hex(0xFBF0D9),
        Danger = Hex(0xB3261E),
        DangerSubtle = Hex(0xFBE9E7),
        Info = Hex(0x1F5A8C),
        InfoSubtle = Hex(0xE7EFF7),
    };

    /// <summary>Dark theme: the same roles stepped for a dark teal-grey surface, not an inverted light theme.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Name = "Dark",
        IsDark = true,
        Page = Hex(0x0D1618),
        Surface = Hex(0x152023),
        SurfaceAlt = Hex(0x1A272A),
        SurfaceHover = Hex(0x203034),
        Border = Hex(0x2C3C40),
        BorderStrong = Hex(0x6E8084),
        Text = Hex(0xE6EEEF),
        TextSecondary = Hex(0xB6C4C7),
        TextMuted = Hex(0x94A5A9),
        TextDisabled = Hex(0x56676B),
        Accent = Hex(0x5FC4D1),
        AccentHover = Hex(0x77CFDA),
        AccentPressed = Hex(0x4AB3C1),
        OnAccent = Hex(0x06272D),
        OnAccentMuted = Hex(0x0E3C44),
        AccentSubtle = Hex(0x183A40),
        AccentText = Hex(0x7FD3DE),
        Warm = Hex(0xF2AE72),
        WarmSubtle = Hex(0x3A2716),
        WarmText = Hex(0xF5BD8A),
        Success = Hex(0x7CCF9C),
        SuccessSubtle = Hex(0x14302A),
        Warning = Hex(0xF0BF5C),
        WarningSubtle = Hex(0x33290F),
        Danger = Hex(0xFF9C8C),
        DangerSubtle = Hex(0x3C1B16),
        Info = Hex(0x92C0F0),
        InfoSubtle = Hex(0x172A3E),
    };

    /// <summary>
    /// High contrast theme built from the active Windows system colors, so the user's contrast theme is
    /// respected instead of being replaced by the application's colors. Selected and hovered content is
    /// written in HighlightText on Highlight.
    /// </summary>
    public static ThemePalette HighContrast() => new()
    {
        Name = "HighContrast",
        IsHighContrast = true,
        IsDark = SystemColors.Window.GetBrightness() < 0.5f,
        Page = SystemColors.Window,
        Surface = SystemColors.Window,
        SurfaceAlt = SystemColors.Window,
        SurfaceHover = SystemColors.Highlight,
        Border = SystemColors.WindowText,
        BorderStrong = SystemColors.WindowText,
        Text = SystemColors.WindowText,
        TextSecondary = SystemColors.WindowText,
        TextMuted = SystemColors.WindowText,
        TextDisabled = SystemColors.GrayText,
        Accent = SystemColors.Highlight,
        AccentHover = SystemColors.Highlight,
        AccentPressed = SystemColors.Highlight,
        OnAccent = SystemColors.HighlightText,
        OnAccentMuted = SystemColors.HighlightText,
        AccentSubtle = SystemColors.Window,
        AccentText = SystemColors.HotTrack,
        Warm = SystemColors.WindowText,
        WarmSubtle = SystemColors.Window,
        WarmText = SystemColors.WindowText,
        Success = SystemColors.WindowText,
        SuccessSubtle = SystemColors.Window,
        Warning = SystemColors.WindowText,
        WarningSubtle = SystemColors.Window,
        Danger = SystemColors.WindowText,
        DangerSubtle = SystemColors.Window,
        Info = SystemColors.WindowText,
        InfoSubtle = SystemColors.Window,
    };

    private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
