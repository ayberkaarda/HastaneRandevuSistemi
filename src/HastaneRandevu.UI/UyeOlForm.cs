using System.Globalization;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>Patient registration.</summary>
    public sealed class UyeOlForm : AppDialog
    {
        private readonly IAuthService _auth;
        private readonly ExclusiveOperation _save = new();

        private readonly TextBox textBoxIsim = new() { Name = nameof(textBoxIsim), MaxLength = 80 };
        private readonly TextBox textBoxTC = new() { Name = nameof(textBoxTC), MaxLength = 11, PlaceholderText = "11 haneli numaranız" };
        private readonly TextBox textBoxSifre = new() { Name = nameof(textBoxSifre), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton buttonKaydet = FormLayout.Button(nameof(buttonKaydet), "Kayıt Ol", ButtonKind.Primary);

        public UyeOlForm(IAuthService auth)
            : base("Hasta kaydı", string.Empty, string.Empty, new Size(820, 540), new Size(720, 500), modal: true, header: false)
        {
            ArgumentNullException.ThrowIfNull(auth);
            _auth = auth;
            BuildLayout();
            textBoxTC.AcceptDigitsOnly();
            buttonKaydet.Click += async (_, _) => await SaveAsync();
        }

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
                    _save.FocusAfterUnlock(textBoxIsim);
                },
                buttonKaydet);

        private void BuildLayout()
        {
            Name = nameof(UyeOlForm);
            SetSidePanel(
                new BrandPanel(
                    "Hesabınızı oluşturun; randevularınızı tek yerden yönetin.",
                    (Glyphs.Lock, "Şifreniz güvenli biçimde saklanır, kimseyle paylaşılmaz."),
                    (Glyphs.Person, "Kimlik numaranız ekranlarda maskelenerek gösterilir."),
                    (Glyphs.Calendar, "Kayıttan sonra hasta girişinden hemen randevu alabilirsiniz.")),
                Sizes.BrandPanelWidth);

            var title = FormLayout.SectionTitle("Hasta kaydı");
            title.Name = "formTitle";
            title.Font = Theme.Fonts.Title;
            var intro = FormLayout.Hint("Ad soyadınızı, T.C. kimlik numaranızı ve bir şifre girin.");
            intro.Name = "formIntro";
            intro.Margin = new Padding(0, 0, 0, Spacing.XL);

            var name = FormLayout.Field("&Ad soyad", FormLayout.Input(textBoxIsim));
            var tc = FormLayout.Field("&T.C. Kimlik No", FormLayout.Input(textBoxTC), "11 hane; ekranlarda yalnızca ilk ve son haneleri görünür.");
            var password = FormLayout.Field(
                "Ş&ifre",
                FormLayout.Input(textBoxSifre),
                string.Format(CultureInfo.CurrentCulture, "En az {0}, en fazla {1} karakter", PasswordPolicy.MinLength, PasswordPolicy.MaxLength));
            name.Margin = tc.Margin = password.Margin = new Padding(0, 0, 0, Spacing.M);

            var stack = FormLayout.Stack("registerForm", title, intro, name, tc, password);
            stack.Padding = new Padding(Spacing.S, Spacing.S, Spacing.S, 0);
            Content.Controls.Add(stack);

            AcceptButton = buttonKaydet;
            SetFooterNote("Kayıt olduktan sonra bu pencereyi kapatıp giriş yapın.");
            SetFooterButtons(buttonKaydet, CreateCloseButton("buttonKapat"));
            FinishLayout();
        }
    }
}
