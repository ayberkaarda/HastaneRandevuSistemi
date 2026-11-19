using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>Doctor sign-in with name and password.</summary>
    public partial class DoktorGirisForm : Form
    {
        private readonly IAuthService _auth;
        private readonly IFormFactory _forms;
        private readonly ExclusiveOperation _signIn = new();

        public DoktorGirisForm(IAuthService auth, IFormFactory forms)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(forms);

            _auth = auth;
            _forms = forms;

            InitializeComponent();
            Icon = AppAssets.Icon;
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

        private async void buttonGiris_Click(object? sender, EventArgs e)
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

        private void buttonIptal_Click(object? sender, EventArgs e) => Close();
    }
}
