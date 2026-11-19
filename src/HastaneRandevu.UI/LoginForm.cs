using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>Patient sign-in; also the entry point to registration and the doctor sign-in.</summary>
    public partial class LoginForm : Form
    {
        private readonly IAuthService _auth;
        private readonly IFormFactory _forms;
        private readonly ExclusiveOperation _signIn = new();

        public LoginForm(IAuthService auth, IFormFactory forms)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(forms);

            _auth = auth;
            _forms = forms;

            InitializeComponent();
            Icon = AppAssets.Icon;
            pictureBoxLogo.Image = AppAssets.Logo;
            pictureBoxArkaplan.Image = AppAssets.Background;
            textBoxTC.AcceptDigitsOnly();
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

        private async void buttonGiris_Click(object? sender, EventArgs e)
        {
            var session = await SignInAsync();
            if (session is not null)
            {
                Dialogs.Show(this, "Giriş başarılı! Hoş geldiniz, " + session.DisplayName, AppText.Title);
                OpenPatientPanel(session);
            }
        }

        private void buttonUyeOl_Click(object? sender, EventArgs e)
        {
            using var register = _forms.Create<UyeOlForm>();
            register.ShowDialog(this);
        }

        private void buttonDoktorGiris_Click(object? sender, EventArgs e)
        {
            using var doctorSignIn = _forms.Create<DoktorGirisForm>();
            doctorSignIn.ShowDialog(this);
        }
    }
}
