namespace HastaneRandevu.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label labelHastaBilgi;
        private System.Windows.Forms.ComboBox comboBoxDoktorlar;
        private System.Windows.Forms.ListBox listBoxRandevuGecmisi;
        private System.Windows.Forms.DateTimePicker dateTimePickerRandevu;
        private System.Windows.Forms.ComboBox comboBoxSaat;
        private System.Windows.Forms.Button buttonRandevuAl;
        private System.Windows.Forms.Button buttonRandevuIptal;
        private System.Windows.Forms.Button buttonCikis;
        private System.Windows.Forms.Button buttonErtele;
        private System.Windows.Forms.TextBox textBoxDoktorAra;
        private System.Windows.Forms.ComboBox comboBoxBrans;
        private System.Windows.Forms.Button buttonIlkBos;
        private System.Windows.Forms.Button buttonTakvim;
        private System.Windows.Forms.Label labelIlkBos;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelHastaBilgi = new System.Windows.Forms.Label();
            this.comboBoxDoktorlar = new System.Windows.Forms.ComboBox();
            this.listBoxRandevuGecmisi = new System.Windows.Forms.ListBox();
            this.dateTimePickerRandevu = new System.Windows.Forms.DateTimePicker();
            this.comboBoxSaat = new System.Windows.Forms.ComboBox();
            this.buttonRandevuAl = new System.Windows.Forms.Button();
            this.buttonRandevuIptal = new System.Windows.Forms.Button();
            this.buttonCikis = new System.Windows.Forms.Button();
            this.buttonErtele = new System.Windows.Forms.Button();
            this.textBoxDoktorAra = new System.Windows.Forms.TextBox();
            this.comboBoxBrans = new System.Windows.Forms.ComboBox();
            this.buttonIlkBos = new System.Windows.Forms.Button();
            this.buttonTakvim = new System.Windows.Forms.Button();
            this.labelIlkBos = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.labelSaat = new System.Windows.Forms.Label();
            this.pictureBoxLogo = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
            this.SuspendLayout();
            //
            // pictureBoxLogo
            //
            this.pictureBoxLogo.Location = new System.Drawing.Point(12, 12);
            this.pictureBoxLogo.Name = "pictureBoxLogo";
            this.pictureBoxLogo.Size = new System.Drawing.Size(64, 64);
            this.pictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxLogo.TabStop = false;
            //
            // labelHastaBilgi
            //
            this.labelHastaBilgi.AutoSize = true;
            this.labelHastaBilgi.Font = new System.Drawing.Font("Segoe UI", 10.2F);
            this.labelHastaBilgi.Location = new System.Drawing.Point(90, 32);
            this.labelHastaBilgi.Name = "labelHastaBilgi";
            this.labelHastaBilgi.Size = new System.Drawing.Size(98, 23);
            this.labelHastaBilgi.TabIndex = 0;
            this.labelHastaBilgi.Text = "Hoşgeldiniz";
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(24, 120);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(72, 16);
            this.label4.Text = "Doktor Ara";
            //
            // textBoxDoktorAra
            //
            this.textBoxDoktorAra.Location = new System.Drawing.Point(24, 142);
            this.textBoxDoktorAra.MaxLength = 100;
            this.textBoxDoktorAra.Name = "textBoxDoktorAra";
            this.textBoxDoktorAra.PlaceholderText = "Ad soyad";
            this.textBoxDoktorAra.Size = new System.Drawing.Size(195, 22);
            this.textBoxDoktorAra.TabIndex = 1;
            this.textBoxDoktorAra.TextChanged += new System.EventHandler(this.textBoxDoktorAra_TextChanged);
            //
            // comboBoxBrans
            //
            this.comboBoxBrans.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxBrans.Location = new System.Drawing.Point(229, 142);
            this.comboBoxBrans.Name = "comboBoxBrans";
            this.comboBoxBrans.Size = new System.Drawing.Size(195, 24);
            this.comboBoxBrans.TabIndex = 2;
            this.comboBoxBrans.SelectedIndexChanged += new System.EventHandler(this.comboBoxBrans_SelectedIndexChanged);
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(24, 178);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(93, 16);
            this.label2.Text = "Doktor Seçiniz";
            //
            // comboBoxDoktorlar
            //
            this.comboBoxDoktorlar.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDoktorlar.FormattingEnabled = true;
            this.comboBoxDoktorlar.Location = new System.Drawing.Point(24, 200);
            this.comboBoxDoktorlar.Name = "comboBoxDoktorlar";
            this.comboBoxDoktorlar.Size = new System.Drawing.Size(400, 24);
            this.comboBoxDoktorlar.TabIndex = 3;
            this.comboBoxDoktorlar.SelectedIndexChanged += new System.EventHandler(this.comboBoxDoktorlar_SelectedIndexChanged);
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(24, 236);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(84, 16);
            this.label3.Text = "Tarih Seçiniz";
            //
            // labelSaat
            //
            this.labelSaat.AutoSize = true;
            this.labelSaat.Location = new System.Drawing.Point(234, 236);
            this.labelSaat.Name = "labelSaat";
            this.labelSaat.Size = new System.Drawing.Size(83, 16);
            this.labelSaat.Text = "Saat Seçiniz";
            //
            // dateTimePickerRandevu
            //
            this.dateTimePickerRandevu.Location = new System.Drawing.Point(24, 258);
            this.dateTimePickerRandevu.Name = "dateTimePickerRandevu";
            this.dateTimePickerRandevu.Size = new System.Drawing.Size(200, 22);
            this.dateTimePickerRandevu.TabIndex = 4;
            this.dateTimePickerRandevu.ValueChanged += new System.EventHandler(this.dateTimePickerRandevu_ValueChanged);
            //
            // comboBoxSaat
            //
            this.comboBoxSaat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSaat.FormattingEnabled = true;
            this.comboBoxSaat.Location = new System.Drawing.Point(234, 258);
            this.comboBoxSaat.Name = "comboBoxSaat";
            this.comboBoxSaat.Size = new System.Drawing.Size(190, 24);
            this.comboBoxSaat.TabIndex = 5;
            //
            // buttonIlkBos
            //
            this.buttonIlkBos.Location = new System.Drawing.Point(24, 296);
            this.buttonIlkBos.Name = "buttonIlkBos";
            this.buttonIlkBos.Size = new System.Drawing.Size(195, 34);
            this.buttonIlkBos.TabIndex = 6;
            this.buttonIlkBos.Text = "İlk Boş Randevu";
            this.buttonIlkBos.UseVisualStyleBackColor = true;
            this.buttonIlkBos.Click += new System.EventHandler(this.buttonIlkBos_Click);
            //
            // buttonTakvim
            //
            this.buttonTakvim.Location = new System.Drawing.Point(229, 296);
            this.buttonTakvim.Name = "buttonTakvim";
            this.buttonTakvim.Size = new System.Drawing.Size(195, 34);
            this.buttonTakvim.TabIndex = 7;
            this.buttonTakvim.Text = "Takvim";
            this.buttonTakvim.UseVisualStyleBackColor = true;
            this.buttonTakvim.Click += new System.EventHandler(this.buttonTakvim_Click);
            //
            // labelIlkBos
            //
            this.labelIlkBos.Location = new System.Drawing.Point(24, 336);
            this.labelIlkBos.Name = "labelIlkBos";
            this.labelIlkBos.Size = new System.Drawing.Size(400, 20);
            //
            // buttonRandevuAl
            //
            this.buttonRandevuAl.Location = new System.Drawing.Point(24, 362);
            this.buttonRandevuAl.Name = "buttonRandevuAl";
            this.buttonRandevuAl.Size = new System.Drawing.Size(400, 40);
            this.buttonRandevuAl.TabIndex = 8;
            this.buttonRandevuAl.Text = "Randevu Al";
            this.buttonRandevuAl.UseVisualStyleBackColor = true;
            this.buttonRandevuAl.Click += new System.EventHandler(this.buttonRandevuAl_Click);
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(460, 120);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(119, 16);
            this.label1.Text = "Aktif Randevularım";
            //
            // listBoxRandevuGecmisi
            //
            this.listBoxRandevuGecmisi.FormattingEnabled = true;
            this.listBoxRandevuGecmisi.IntegralHeight = false;
            this.listBoxRandevuGecmisi.ItemHeight = 16;
            this.listBoxRandevuGecmisi.Location = new System.Drawing.Point(460, 142);
            this.listBoxRandevuGecmisi.Name = "listBoxRandevuGecmisi";
            this.listBoxRandevuGecmisi.Size = new System.Drawing.Size(416, 214);
            this.listBoxRandevuGecmisi.TabIndex = 9;
            //
            // buttonRandevuIptal
            //
            this.buttonRandevuIptal.Location = new System.Drawing.Point(460, 366);
            this.buttonRandevuIptal.Name = "buttonRandevuIptal";
            this.buttonRandevuIptal.Size = new System.Drawing.Size(132, 36);
            this.buttonRandevuIptal.TabIndex = 10;
            this.buttonRandevuIptal.Text = "İptal Et";
            this.buttonRandevuIptal.UseVisualStyleBackColor = true;
            this.buttonRandevuIptal.Click += new System.EventHandler(this.buttonRandevuIptal_Click);
            //
            // buttonErtele
            //
            this.buttonErtele.Location = new System.Drawing.Point(602, 366);
            this.buttonErtele.Name = "buttonErtele";
            this.buttonErtele.Size = new System.Drawing.Size(132, 36);
            this.buttonErtele.TabIndex = 11;
            this.buttonErtele.Text = "Ertele";
            this.buttonErtele.UseVisualStyleBackColor = true;
            this.buttonErtele.Click += new System.EventHandler(this.buttonErtele_Click);
            //
            // buttonCikis
            //
            this.buttonCikis.Location = new System.Drawing.Point(761, 424);
            this.buttonCikis.Name = "buttonCikis";
            this.buttonCikis.Size = new System.Drawing.Size(115, 36);
            this.buttonCikis.TabIndex = 13;
            this.buttonCikis.Text = "Çıkış";
            this.buttonCikis.UseVisualStyleBackColor = true;
            this.buttonCikis.Click += new System.EventHandler(this.buttonCikis_Click);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 480);
            this.Controls.Add(this.pictureBoxLogo);
            this.Controls.Add(this.labelHastaBilgi);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textBoxDoktorAra);
            this.Controls.Add(this.comboBoxBrans);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.comboBoxDoktorlar);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.labelSaat);
            this.Controls.Add(this.dateTimePickerRandevu);
            this.Controls.Add(this.comboBoxSaat);
            this.Controls.Add(this.buttonIlkBos);
            this.Controls.Add(this.buttonTakvim);
            this.Controls.Add(this.labelIlkBos);
            this.Controls.Add(this.buttonRandevuAl);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.listBoxRandevuGecmisi);
            this.Controls.Add(this.buttonRandevuIptal);
            this.Controls.Add(this.buttonErtele);
            this.Controls.Add(this.buttonCikis);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hastane Randevu - Randevu Ekranı";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelSaat;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
    }
}
