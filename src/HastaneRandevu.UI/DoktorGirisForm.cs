using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>Doctor sign-in with name and password.</summary>
    public sealed class DoktorGirisForm : AppDialog
    {
        private readonly IAuthService _auth;
        private readonly IFormFactory _forms;
        private readonly ExclusiveOperation _signIn = new();

        private readonly TextBox textBoxDoktorIsim = new() { Name = nameof(textBoxDoktorIsim), MaxLength = 100 };
        private readonly TextBox textBoxSifre = new() { Name = nameof(textBoxSifre), MaxLength = PasswordPolicy.SignInMaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton buttonGiris = FormLayout.Button(nameof(buttonGiris), "Giriş", ButtonKind.Primary);
        private readonly ThemedButton buttonIptal = FormLayout.Button(nameof(buttonIptal), "İptal");

        public DoktorGirisForm(IAuthService auth, IFormFactory forms)
            : base("Doktor girişi", string.Empty, string.Empty, new Size(760, 420), new Size(680, 400), modal: true, header: false)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(forms);

            _auth = auth;
            _forms = forms;
            BuildLayout();
            buttonGiris.Click += async (_, _) => await SignInAndOpenAsync();
            buttonIptal.Click += (_, _) => Close();
        }

        /// <summary>
        /// Signs the doctor in; ignored while a previous attempt is still running. Returns the session,
        /// or <see langword="null"/> when the attempt failed (the reason has been shown) or was ignored.
        /// </summary>
        internal async Task<UserSession?> SignInAsync()
        {
            UserSession? session = null;
            await this.RunExclusiveAsync(
                _signIn,
                async () =>
                {
                    try
                    {
                        session = await _auth.SignInDoctorAsync(textBoxDoktorIsim.Text, textBoxSifre.Text);
                    }
                    finally
                    {
                        textBoxSifre.Clear();
                        if (session is null)
                        {
                            _signIn.FocusAfterUnlock(textBoxSifre);
                        }
                    }
                },
                buttonGiris,
                buttonIptal);
            return session;
        }

        private async Task SignInAndOpenAsync()
        {
            var session = await SignInAsync();
            if (session is null)
            {
                return;
            }

            Hide();
            using (var panel = _forms.Create<DoktorForm>(session))
            {
                panel.ShowDialog();
            }

            Close();
        }

        private void BuildLayout()
        {
            Name = nameof(DoktorGirisForm);
            SetSidePanel(
                new BrandPanel(
                    "Doktor paneli: ajandanız, çalışma saatleriniz ve izin günleriniz.",
                    (Glyphs.Agenda, "Randevularınızı görün, gerektiğinde iptal edin."),
                    (Glyphs.Schedule, "Haftalık çalışma saatlerinizi kendiniz belirleyin.")),
                Sizes.BrandPanelWidth - 30);

            var title = FormLayout.SectionTitle("Doktor girişi");
            title.Name = "formTitle";
            title.Font = Theme.Fonts.Title;
            var intro = FormLayout.Hint("Ad soyadınızı ve şifrenizi girin.");
            intro.Name = "formIntro";
            intro.Margin = new Padding(0, 0, 0, Spacing.XL);

            var name = FormLayout.Field("&Ad soyad", FormLayout.Input(textBoxDoktorIsim));
            var password = FormLayout.Field("Ş&ifre", FormLayout.Input(textBoxSifre));
            name.Margin = password.Margin = new Padding(0, 0, 0, Spacing.M);

            var stack = FormLayout.Stack("doctorSignInForm", title, intro, name, password);
            stack.Padding = new Padding(Spacing.S, Spacing.S, Spacing.S, 0);
            Content.Controls.Add(stack);

            AcceptButton = buttonGiris;
            CancelButton = buttonIptal;
            buttonIptal.DialogResult = DialogResult.Cancel;
            SetFooterNote("Hasta girişi için bu pencereyi kapatın.");
            SetFooterButtons(buttonGiris, buttonIptal);
            FinishLayout();
        }
    }
}
