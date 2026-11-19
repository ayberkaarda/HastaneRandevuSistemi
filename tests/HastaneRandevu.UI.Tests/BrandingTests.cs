using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>
/// The screens use the neutral product name and the application's own artwork; nothing suggests an
/// official government service.
/// </summary>
public sealed class BrandingTests
{
    private static readonly string[] ForbiddenTexts = ["MHRS", "Merkezi Hekim", "Bakanlığı", "T.C. Sağlık"];

    [Fact]
    public void Forms_UseTheNeutralTitleAndOwnIcon() => Run(() =>
    {
        var auth = new PendingAuthService();
        var appointments = new ControlledAppointmentService();
        using var login = new LoginForm(auth, new NoFormsFactory());
        using var register = new UyeOlForm(auth);
        using var doctorSignIn = new DoktorGirisForm(auth, new NoFormsFactory());
        using var patientPanel = new MainForm(appointments, Sessions.Patient);
        using var doctorPanel = new DoktorForm(appointments, Sessions.Doctor);

        foreach (Form form in new Form[] { login, register, doctorSignIn, patientPanel, doctorPanel })
        {
            Assert.StartsWith(AppText.Title + " - ", form.Text, StringComparison.Ordinal);
            Assert.Same(AppAssets.Icon, form.Icon);
            foreach (var text in AllTexts(form))
            {
                Assert.All(ForbiddenTexts, forbidden => Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase));
            }
        }

        Assert.Same(AppAssets.Logo, Find<PictureBox>(login, "pictureBoxLogo").Image);
        Assert.Same(AppAssets.Background, Find<PictureBox>(login, "pictureBoxArkaplan").Image);
        Assert.Same(AppAssets.Background, Find<PictureBox>(register, "pictureBoxArkaplan").Image);
    });

    [Fact]
    public void EmbeddedResources_AreOnlyTheApplicationsOwnArtwork()
    {
        var images = typeof(AppAssets).Assembly.GetManifestResourceNames()
            .Where(n => !n.EndsWith(".resources", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            ["HastaneRandevu.UI.Assets.app.ico", "HastaneRandevu.UI.Assets.background.png", "HastaneRandevu.UI.Assets.logo.png"],
            images);
        Assert.Equal(new Size(256, 256), AppAssets.Logo.Size);
    }

    private static IEnumerable<string> AllTexts(Control root)
    {
        yield return root.Text;
        foreach (Control child in root.Controls)
        {
            foreach (var text in AllTexts(child))
            {
                yield return text;
            }
        }
    }
}
