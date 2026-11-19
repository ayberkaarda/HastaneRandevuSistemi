using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>Patient registration.</summary>
    public partial class UyeOlForm : Form
    {
        private readonly IAuthService _auth;
        private readonly ExclusiveOperation _save = new();

        public UyeOlForm(IAuthService auth)
        {
            ArgumentNullException.ThrowIfNull(auth);
            _auth = auth;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            Icon = AppAssets.Icon;
            pictureBoxLogo.Image = AppAssets.Logo;
            pictureBoxArkaplan.Image = AppAssets.Background;
            textBoxTC.AcceptDigitsOnly();
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Registers the patient; ignored while a previous save is still running.</summary>
        internal Task<bool> SaveAsync() =>
            this.RunExclusiveAsync(
                _save,
                async () =>
                {
                    await _auth.RegisterPatientAsync(textBoxIsim.Text, textBoxTC.Text, textBoxSifre.Text);

                    textBoxIsim.Clear();
                    textBoxTC.Clear();
                    textBoxSifre.Clear();
                    Prompts.Inform("Kayıt başarıyla eklendi!");
                },
                buttonKaydet);

        private async void buttonKaydet_Click(object? sender, EventArgs e) => await SaveAsync();
    }
}
