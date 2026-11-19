using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI.Tests.Support;

/// <summary>
/// Layout checks on a form that is created but not shown: handles are created so the layout engine
/// runs, then every visible control must lie inside the area its parent really provides and must not overlap
/// a sibling. A scrolling parent provides its whole scrollable area (the body of a window that is smaller than
/// its content); everything outside it (header, footer, actions) must lie inside the window itself.
/// </summary>
internal static class LayoutAssert
{
    /// <summary>Working areas of a small laptop, a common laptop and a large screen; sizes do not depend on the machine running the test.</summary>
    private static readonly Size[] Areas = [new(1024, 600), new(1366, 768), new(1920, 1040)];

    private static readonly float[] FontScales = [1F, 1.25F, 1.5F, 2F];

    /// <summary>
    /// Builds the screen for every combination of a stand-in working area and font scale (replacing the real
    /// screen and font metrics), requires it to open inside that area, and checks its layout.
    /// </summary>
    public static void FitsInsideOnEveryScreen(Func<Form> create)
    {
        var previous = Theme.Current;
        var previousScale = Theme.RenderScale;
        var previousArea = AppDialog.WorkingAreaOf;
        try
        {
            foreach (var size in Areas)
            {
                foreach (var scale in FontScales)
                {
                    Theme.Use(previous, scale);
                    var area = new Rectangle(Point.Empty, size);
                    AppDialog.WorkingAreaOf = _ => area;
                    using var form = create();
                    try
                    {
                        FitsInside(form);
                    }
                    catch (Xunit.Sdk.XunitException ex)
                    {
                        throw new Xunit.Sdk.XunitException($"[{area.Size}, font scale {scale}] {ex.Message}");
                    }

                    Assert.True(
                        form.Width <= area.Width && form.Height <= area.Height,
                        $"{form.GetType().Name} opens at {form.Size} in a {area.Size} working area at font scale {scale}.");
                }
            }
        }
        finally
        {
            AppDialog.WorkingAreaOf = previousArea;
            Theme.Use(previous, previousScale);
        }
    }

    public static void FitsInside(Form form)
    {
        CreateHandles(form);
        form.PerformLayout();

        // Every control lies inside what its parent provides, so nothing is clipped at any level. A scrolling
        // parent provides its whole scrollable area, which the user reaches by scrolling.
        foreach (var control in Descendants(form))
        {
            var parent = control.Parent!;
            var available = parent is ScrollableControl { AutoScroll: true }
                ? new Rectangle(parent.DisplayRectangle.Location, Size.Add(parent.DisplayRectangle.Size, Size.Empty))
                : parent.ClientRectangle;
            Assert.True(
                available.Contains(control.Bounds),
                $"{Describe(control)} at {control.Bounds} lies outside {Describe(parent)} ({available}).");
        }

        // The scrollable frame itself must be reachable: the body scrolls over all of it.
        foreach (var scroller in Descendants(form).OfType<ScrollableControl>().Where(c => c.AutoScroll))
        {
            foreach (Control child in scroller.Controls)
            {
                var needed = new Size(child.Right - scroller.DisplayRectangle.Left, child.Bottom - scroller.DisplayRectangle.Top);
                Assert.True(
                    scroller.DisplayRectangle.Width >= needed.Width - 1 && scroller.DisplayRectangle.Height >= needed.Height - 1,
                    $"{Describe(scroller)} does not scroll over {Describe(child)} ({needed}).");
            }
        }

        foreach (var parent in Descendants(form).Prepend(form))
        {
            var children = parent.Controls.Cast<Control>().Where(c => c.Width > 0 && c.Height > 0).ToList();
            for (var i = 0; i < children.Count; i++)
            {
                for (var j = i + 1; j < children.Count; j++)
                {
                    Assert.False(
                        children[i].Bounds.IntersectsWith(children[j].Bounds),
                        $"{Describe(children[i])} overlaps {Describe(children[j])} in {Describe(parent)}.");
                }
            }
        }
    }

    private static string Describe(Control control) =>
        string.IsNullOrEmpty(control.Name) ? $"{control.GetType().Name} '{control.Text}'" : control.Name;

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }
}
