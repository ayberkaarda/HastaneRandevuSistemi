namespace HastaneRandevu.UI.Theming;

/// <summary>
/// A native date box that is always wide enough for its longest date: once its window exists (fonts and
/// DPI are final) it grows to fit, for example, "28 Ağustos 2026 Çarşamba" and the drop-down button.
/// </summary>
internal sealed class DateField : DateTimePicker
{
    /// <summary>A date with the longest month and day names in Turkish.</summary>
    private const string LongestDate = "28 Ağustos 2026 Çarşamba";

    public DateField()
    {
        Format = DateTimePickerFormat.Long;
        Font = Theme.Fonts.Body;
        Width = 200;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        FitText();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (IsHandleCreated)
        {
            FitText();
        }
    }

    private void FitText()
    {
        var needed = TextRenderer.MeasureText(LongestDate, Font).Width + SystemInformation.VerticalScrollBarWidth + Theme.Px(this, Spacing.L);
        if (Width < needed)
        {
            Width = needed;
        }
    }
}
