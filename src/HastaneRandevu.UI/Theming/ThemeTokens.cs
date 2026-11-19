namespace HastaneRandevu.UI.Theming;

/// <summary>Spacing scale in logical pixels (96 DPI); layouts are scaled by Windows Forms for the monitor.</summary>
internal static class Spacing
{
    public const int XS = 4;
    public const int S = 8;
    public const int M = 12;
    public const int L = 16;
    public const int XL = 24;
    public const int XXL = 32;

    /// <summary>All steps in ascending order.</summary>
    public static IReadOnlyList<int> Scale { get; } = [XS, S, M, L, XL, XXL];
}

/// <summary>Corner radii in logical pixels.</summary>
internal static class Radius
{
    /// <summary>Buttons and inputs.</summary>
    public const int Control = 6;

    /// <summary>Cards, banners and calendar days.</summary>
    public const int Card = 10;
}

/// <summary>Fixed control sizes in logical pixels.</summary>
internal static class Sizes
{
    /// <summary>Height of buttons, text fields and drop-down lists, so a row of them lines up.</summary>
    public const int ControlHeight = 34;

    public const int ButtonMinWidth = 96;

    /// <summary>Glyph badge in dialog headers.</summary>
    public const int HeaderBadge = 40;

    /// <summary>Logo badge of the sign-in screens and the panels.</summary>
    public const int LogoBadge = 56;

    /// <summary>Width of the brand panel on the sign-in and registration screens.</summary>
    public const int BrandPanelWidth = 300;

    /// <summary>Smallest height of a day in the calendar.</summary>
    public const int CalendarDayHeight = 58;

    public const int FocusRing = 2;
}

/// <summary>One step of the type ramp; sizes are in points.</summary>
internal readonly record struct FontToken(string Family, float Size, FontStyle Style = FontStyle.Regular);

/// <summary>
/// Type ramp. Segoe UI (the Windows UI face) carries all text; the hierarchy comes from size and the
/// Semibold weight, never from color alone. Clinical screens are read quickly, so there is no display
/// face: titles are Semibold Segoe UI, body text stays at 9.75 pt.
/// Display &gt; Title &gt; Subtitle &gt; Body &gt; Caption.
/// </summary>
internal static class Typography
{
    public const string Family = "Segoe UI";
    public const string SemiboldFamily = "Segoe UI Semibold";

    public static readonly FontToken Display = new(SemiboldFamily, 18F);
    public static readonly FontToken Title = new(SemiboldFamily, 13.5F);
    public static readonly FontToken Subtitle = new(SemiboldFamily, 11F);
    public static readonly FontToken Body = new(Family, 9.75F);
    public static readonly FontToken BodyStrong = new(SemiboldFamily, 9.75F);
    public static readonly FontToken Caption = new(Family, 9F);

    /// <summary>The day number in the calendar.</summary>
    public static readonly FontToken Value = new(SemiboldFamily, 12F);

    /// <summary>Text sizes from the smallest to the largest; used to check the ramp is ordered.</summary>
    public static IReadOnlyList<FontToken> Ramp { get; } = [Caption, Body, Subtitle, Title, Display];
}

/// <summary>Code points of Segoe Fluent Icons / Segoe MDL2 Assets used by the application.</summary>
internal static class Glyphs
{
    public const string Calendar = "\uE787";
    public const string Clock = "\uE823";
    public const string Search = "\uE721";
    public const string Person = "\uE77B";
    public const string AddPerson = "\uE8FA";
    public const string Lock = "\uE72E";
    public const string Download = "\uE896";
    public const string Reminder = "\uEA8F";
    public const string Schedule = "\uEC92";
    public const string Agenda = "\uE7EE";
    public const string Reschedule = "\uE81C";
    public const string Leave = "\uE709";
    public const string Info = "\uE946";
    public const string Success = "\uE930";
    public const string Warning = "\uE7BA";
    public const string Error = "\uE783";
    public const string Question = "\uE9CE";
    public const string CheckMark = "\uE73E";
    public const string ChevronDown = "\uE70D";
    public const string ChevronLeft = "\uE76B";
    public const string ChevronRight = "\uE76C";
}
