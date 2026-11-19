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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.labelSaat = new System.Windows.Forms.Label();
            this.pictureBoxLogo = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
            this.SuspendLayout();
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
            // comboBoxDoktorlar
            //
            this.comboBoxDoktorlar.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDoktorlar.FormattingEnabled = true;
            this.comboBoxDoktorlar.Location = new System.Drawing.Point(57, 189);
            this.comboBoxDoktorlar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.comboBoxDoktorlar.Name = "comboBoxDoktorlar";
            this.comboBoxDoktorlar.Size = new System.Drawing.Size(317, 24);
            this.comboBoxDoktorlar.TabIndex = 1;
            this.comboBoxDoktorlar.SelectedIndexChanged += new System.EventHandler(this.comboBoxDoktorlar_SelectedIndexChanged);
            //
            // listBoxRandevuGecmisi
            //
            this.listBoxRandevuGecmisi.Font = new System.Drawing.Font("Segoe UI", 7.8F);
            this.listBoxRandevuGecmisi.FormattingEnabled = true;
            this.listBoxRandevuGecmisi.ItemHeight = 17;
            this.listBoxRandevuGecmisi.Location = new System.Drawing.Point(404, 118);
            this.listBoxRandevuGecmisi.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.listBoxRandevuGecmisi.Name = "listBoxRandevuGecmisi";
            this.listBoxRandevuGecmisi.Size = new System.Drawing.Size(357, 174);
            this.listBoxRandevuGecmisi.TabIndex = 5;
            //
            // dateTimePickerRandevu
            //
            this.dateTimePickerRandevu.CalendarFont = new System.Drawing.Font("Segoe UI", 7.8F);
            this.dateTimePickerRandevu.Location = new System.Drawing.Point(57, 259);
            this.dateTimePickerRandevu.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dateTimePickerRandevu.Name = "dateTimePickerRandevu";
            this.dateTimePickerRandevu.Size = new System.Drawing.Size(200, 22);
            this.dateTimePickerRandevu.TabIndex = 2;
            this.dateTimePickerRandevu.ValueChanged += new System.EventHandler(this.dateTimePickerRandevu_ValueChanged);
            //
            // comboBoxSaat
            //
            this.comboBoxSaat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSaat.FormattingEnabled = true;
            this.comboBoxSaat.Location = new System.Drawing.Point(270, 259);
            this.comboBoxSaat.Name = "comboBoxSaat";
            this.comboBoxSaat.Size = new System.Drawing.Size(104, 24);
            this.comboBoxSaat.TabIndex = 3;
            //
            // buttonRandevuAl
            //
            this.buttonRandevuAl.Font = new System.Drawing.Font("Segoe UI", 7.8F);
            this.buttonRandevuAl.Location = new System.Drawing.Point(156, 324);
            this.buttonRandevuAl.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonRandevuAl.Name = "buttonRandevuAl";
            this.buttonRandevuAl.Size = new System.Drawing.Size(115, 39);
            this.buttonRandevuAl.TabIndex = 4;
            this.buttonRandevuAl.Text = "Randevu Al";
            this.buttonRandevuAl.UseVisualStyleBackColor = true;
            this.buttonRandevuAl.Click += new System.EventHandler(this.buttonRandevuAl_Click);
            //
            // buttonRandevuIptal
            //
            this.buttonRandevuIptal.Font = new System.Drawing.Font("Segoe UI", 7.8F);
            this.buttonRandevuIptal.Location = new System.Drawing.Point(473, 324);
            this.buttonRandevuIptal.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonRandevuIptal.Name = "buttonRandevuIptal";
            this.buttonRandevuIptal.Size = new System.Drawing.Size(115, 39);
            this.buttonRandevuIptal.TabIndex = 6;
            this.buttonRandevuIptal.Text = "İptal Et";
            this.buttonRandevuIptal.UseVisualStyleBackColor = true;
            this.buttonRandevuIptal.Click += new System.EventHandler(this.buttonRandevuIptal_Click);
            //
            // buttonCikis
            //
            this.buttonCikis.Font = new System.Drawing.Font("Segoe UI", 7.8F);
            this.buttonCikis.Location = new System.Drawing.Point(646, 324);
            this.buttonCikis.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonCikis.Name = "buttonCikis";
            this.buttonCikis.Size = new System.Drawing.Size(115, 39);
            this.buttonCikis.TabIndex = 7;
            this.buttonCikis.Text = "Çıkış";
            this.buttonCikis.UseVisualStyleBackColor = true;
            this.buttonCikis.Click += new System.EventHandler(this.buttonCikis_Click);
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(402, 94);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(119, 16);
            this.label1.TabIndex = 8;
            this.label1.Text = "Aktif Randevularım";
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(58, 164);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(93, 16);
            this.label2.TabIndex = 9;
            this.label2.Text = "Doktor Seçiniz";
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(58, 237);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(84, 16);
            this.label3.TabIndex = 10;
            this.label3.Text = "Tarih Seçiniz";
            //
            // labelSaat
            //
            this.labelSaat.AutoSize = true;
            this.labelSaat.Location = new System.Drawing.Point(270, 237);
            this.labelSaat.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelSaat.Name = "labelSaat";
            this.labelSaat.Size = new System.Drawing.Size(83, 16);
            this.labelSaat.TabIndex = 11;
            this.labelSaat.Text = "Saat Seçiniz";
            //
            // pictureBoxLogo
            //
            this.pictureBoxLogo.Location = new System.Drawing.Point(12, 12);
            this.pictureBoxLogo.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pictureBoxLogo.Name = "pictureBoxLogo";
            this.pictureBoxLogo.Size = new System.Drawing.Size(64, 64);
            this.pictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxLogo.TabIndex = 14;
            this.pictureBoxLogo.TabStop = false;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 400);
            this.Controls.Add(this.pictureBoxLogo);
            this.Controls.Add(this.labelSaat);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buttonCikis);
            this.Controls.Add(this.buttonRandevuIptal);
            this.Controls.Add(this.buttonRandevuAl);
            this.Controls.Add(this.comboBoxSaat);
            this.Controls.Add(this.dateTimePickerRandevu);
            this.Controls.Add(this.listBoxRandevuGecmisi);
            this.Controls.Add(this.comboBoxDoktorlar);
            this.Controls.Add(this.labelHastaBilgi);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
        private System.Windows.Forms.Label labelSaat;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
    }
}
