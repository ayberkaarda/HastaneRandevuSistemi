using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>Patient sign-in; also the entry point to registration and the doctor sign-in.</summary>
    public sealed class LoginForm : AppDialog
    {
        private readonly IAuthService _auth;
        private readonly IFormFactory _forms;
        private readonly ExclusiveOperation _signIn = new();

        private readonly TextBox textBoxTC = new() { Name = nameof(textBoxTC), MaxLength = 11, PlaceholderText = "11 haneli numaranız" };
        private readonly TextBox textBoxSifre = new() { Name = nameof(textBoxSifre), MaxLength = PasswordPolicy.SignInMaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton buttonGiris = FormLayout.Button(nameof(buttonGiris), "Giriş Yap", ButtonKind.Primary);
        private readonly ThemedButton buttonUyeOl = FormLayout.Button(nameof(buttonUyeOl), "Üye Ol", ButtonKind.Secondary, Glyphs.AddPerson);
        private readonly ThemedButton buttonDoktorGiris = FormLayout.Button(nameof(buttonDoktorGiris), "Doktor girişi", ButtonKind.Subtle, Glyphs.Person);

        public LoginForm(IAuthService auth, IFormFactory forms)
            : base("Giriş", string.Empty, string.Empty, new Size(820, 500), new Size(720, 460), header: false)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(forms);

            _auth = auth;
            _forms = forms;
            BuildLayout();
            textBoxTC.AcceptDigitsOnly();

            buttonGiris.Click += async (_, _) => await SignInAndOpenAsync();
            buttonUyeOl.Click += (_, _) => OpenRegistration();
            buttonDoktorGiris.Click += (_, _) => OpenDoctorSignIn();
        }

        /// <summary>
        /// Signs the patient in; ignored while a previous attempt is still running. Returns the session,
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
                        session = await _auth.SignInPatientAsync(textBoxTC.Text, textBoxSifre.Text);
                    }
                    finally
                    {
                        textBoxSifre.Clear();
                        if (session is null)
                        {
                            // Retyping the password is the next step after a refused attempt.
                            _signIn.FocusAfterUnlock(textBoxSifre);
                        }
                    }
                },
                buttonGiris,
                buttonUyeOl,
                buttonDoktorGiris);
            return session;
        }

        private async Task SignInAndOpenAsync()
        {
            var session = await SignInAsync();
            if (session is not null)
            {
                OpenPatientPanel(session);
            }
        }

        private void OpenPatientPanel(UserSession session)
        {
            textBoxTC.Clear();
            Hide();
            try
            {
                using var panel = _forms.Create<MainForm>(session);
                panel.ShowDialog();
            }
            finally
            {
                // Signing out (closing the panel) returns to this screen.
                Show();
                textBoxTC.Focus();
            }
        }

        private void OpenRegistration()
        {
            using var register = _forms.Create<UyeOlForm>();
            register.ShowDialog(this);
        }

        private void OpenDoctorSignIn()
        {
            using var doctorSignIn = _forms.Create<DoktorGirisForm>();
            doctorSignIn.ShowDialog(this);
        }

        private void BuildLayout()
        {
            Name = nameof(LoginForm);
            SetSidePanel(
                new BrandPanel(
                    "Randevunuzu birkaç adımda alın, değiştirin ve takip edin.",
                    (Glyphs.Search, "Doktoru adına ya da branşına göre bulun."),
                    (Glyphs.Calendar, "Takvimde boş günleri görün, ilk boş saati seçin."),
                    (Glyphs.Reminder, "Yaklaşan randevularınız için hatırlatma alın.")),
                Sizes.BrandPanelWidth);

            var title = FormLayout.SectionTitle("Hasta girişi");
            title.Name = "formTitle";
            title.Font = Theme.Fonts.Title;
            var intro = FormLayout.Hint("T.C. kimlik numaranız ve şifrenizle giriş yapın.");
            intro.Name = "formIntro";
            intro.Margin = new Padding(0, 0, 0, Spacing.XL);

            var tc = FormLayout.Field("&T.C. Kimlik No", FormLayout.Input(textBoxTC));
            var password = FormLayout.Field("Ş&ifre", FormLayout.Input(textBoxSifre));
            tc.Margin = password.Margin = new Padding(0, 0, 0, Spacing.M);
            buttonGiris.AutoSize = false;
            buttonGiris.Dock = DockStyle.Top;
            buttonGiris.Margin = new Padding(0, Spacing.S, 0, Spacing.XL);

            var newHere = FormLayout.Hint("Hesabınız yok mu? Birkaç bilgiyle hemen kayıt olun.");
            newHere.Name = "registerHint";
            newHere.Margin = new Padding(0, 0, 0, Spacing.S);
            buttonUyeOl.Margin = Padding.Empty;

            var stack = FormLayout.Stack("signInForm", title, intro, tc, password, buttonGiris, newHere, buttonUyeOl);
            stack.Padding = new Padding(Spacing.S, Spacing.S, Spacing.S, 0);
            buttonUyeOl.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            Content.Controls.Add(stack);

            AcceptButton = buttonGiris;
            SetFooterNote("Doktorlar ad soyad ve şifreleriyle doktor girişinden girer.");
            SetFooterButtons(buttonDoktorGiris, CreateCloseButton("buttonCikis", "Çıkış"));
            FinishLayout();
        }
    }
}
