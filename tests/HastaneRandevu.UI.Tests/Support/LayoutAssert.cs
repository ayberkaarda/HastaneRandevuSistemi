namespace HastaneRandevu.UI.Tests.Support;

/// <summary>
/// Layout checks on a form that is created but not shown: handles are created so the layout engine
/// runs, then every visible control must lie inside the client area and must not overlap a sibling.
/// </summary>
internal static class LayoutAssert
{
    public static void FitsInside(Form form)
    {
        CreateHandles(form);
        form.PerformLayout();

        // Every control lies inside its parent's client area, so nothing is clipped at any level.
        foreach (var control in Descendants(form))
        {
            var parent = control.Parent!;
            Assert.True(
                parent.ClientRectangle.Contains(control.Bounds),
                $"{Describe(control)} at {control.Bounds} lies outside {Describe(parent)} ({parent.ClientRectangle}).");
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
