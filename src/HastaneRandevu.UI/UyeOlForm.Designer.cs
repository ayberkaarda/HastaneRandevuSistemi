namespace HastaneRandevu.UI
{
    partial class UyeOlForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.buttonKaydet = new System.Windows.Forms.Button();
            this.textBoxSifre = new System.Windows.Forms.TextBox();
            this.textBoxTC = new System.Windows.Forms.TextBox();
            this.textBoxIsim = new System.Windows.Forms.TextBox();
            this.labelTC = new System.Windows.Forms.Label();
            this.labelSifre = new System.Windows.Forms.Label();
            this.labelIsim = new System.Windows.Forms.Label();
            this.labelSifreKurali = new System.Windows.Forms.Label();
            this.pictureBoxArkaplan = new System.Windows.Forms.PictureBox();
            this.pictureBoxLogo = new System.Windows.Forms.PictureBox();
            this.labelBaslik = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxArkaplan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
            this.SuspendLayout();
            //
            // buttonKaydet
            //
            this.buttonKaydet.ForeColor = System.Drawing.SystemColors.ControlText;
            this.buttonKaydet.Location = new System.Drawing.Point(555, 384);
            this.buttonKaydet.Name = "buttonKaydet";
            this.buttonKaydet.Size = new System.Drawing.Size(121, 39);
            this.buttonKaydet.TabIndex = 3;
            this.buttonKaydet.Text = "Kayıt Ol";
            this.buttonKaydet.UseVisualStyleBackColor = true;
            this.buttonKaydet.Click += new System.EventHandler(this.buttonKaydet_Click);
            //
            // textBoxSifre
            //
            this.textBoxSifre.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.textBoxSifre.Location = new System.Drawing.Point(483, 309);
            this.textBoxSifre.MaxLength = 64;
            this.textBoxSifre.Name = "textBoxSifre";
            this.textBoxSifre.Size = new System.Drawing.Size(289, 34);
            this.textBoxSifre.TabIndex = 2;
            this.textBoxSifre.UseSystemPasswordChar = true;
            //
            // textBoxTC
            //
            this.textBoxTC.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.textBoxTC.Location = new System.Drawing.Point(483, 236);
            this.textBoxTC.MaxLength = 11;
            this.textBoxTC.Name = "textBoxTC";
            this.textBoxTC.Size = new System.Drawing.Size(289, 34);
            this.textBoxTC.TabIndex = 1;
            //
            // textBoxIsim
            //
            this.textBoxIsim.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.textBoxIsim.Location = new System.Drawing.Point(483, 162);
            this.textBoxIsim.MaxLength = 80;
            this.textBoxIsim.Name = "textBoxIsim";
            this.textBoxIsim.Size = new System.Drawing.Size(289, 34);
            this.textBoxIsim.TabIndex = 0;
            //
            // labelTC
            //
            this.labelTC.AutoSize = true;
            this.labelTC.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.labelTC.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelTC.Location = new System.Drawing.Point(479, 206);
            this.labelTC.Name = "labelTC";
            this.labelTC.Size = new System.Drawing.Size(133, 28);
            this.labelTC.TabIndex = 13;
            this.labelTC.Text = "T.C. Kimlik No";
            //
            // labelSifre
            //
            this.labelSifre.AutoSize = true;
            this.labelSifre.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.labelSifre.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelSifre.Location = new System.Drawing.Point(479, 278);
            this.labelSifre.Name = "labelSifre";
            this.labelSifre.Size = new System.Drawing.Size(51, 28);
            this.labelSifre.TabIndex = 14;
            this.labelSifre.Text = "Şifre";
            //
            // labelIsim
            //
            this.labelIsim.AutoSize = true;
            this.labelIsim.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.labelIsim.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelIsim.Location = new System.Drawing.Point(479, 132);
            this.labelIsim.Name = "labelIsim";
            this.labelIsim.Size = new System.Drawing.Size(120, 28);
            this.labelIsim.TabIndex = 15;
            this.labelIsim.Text = "İsim Soyisim";
            //
            // labelSifreKurali
            //
            this.labelSifreKurali.AutoSize = true;
            this.labelSifreKurali.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelSifreKurali.Location = new System.Drawing.Point(481, 348);
            this.labelSifreKurali.Name = "labelSifreKurali";
            this.labelSifreKurali.Size = new System.Drawing.Size(200, 16);
            this.labelSifreKurali.TabIndex = 16;
            this.labelSifreKurali.Text = "En az 8, en fazla 64 karakter";
            //
            // pictureBoxArkaplan
            //
            this.pictureBoxArkaplan.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxArkaplan.Name = "pictureBoxArkaplan";
            this.pictureBoxArkaplan.Size = new System.Drawing.Size(450, 450);
            this.pictureBoxArkaplan.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxArkaplan.TabIndex = 17;
            this.pictureBoxArkaplan.TabStop = false;
            //
            // pictureBoxLogo
            //
            this.pictureBoxLogo.Location = new System.Drawing.Point(483, 24);
            this.pictureBoxLogo.Name = "pictureBoxLogo";
            this.pictureBoxLogo.Size = new System.Drawing.Size(64, 64);
            this.pictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxLogo.TabIndex = 18;
            this.pictureBoxLogo.TabStop = false;
            //
            // labelBaslik
            //
            this.labelBaslik.AutoSize = true;
            this.labelBaslik.Font = new System.Drawing.Font("Segoe UI Semibold", 14F, System.Drawing.FontStyle.Bold);
            this.labelBaslik.Location = new System.Drawing.Point(555, 40);
            this.labelBaslik.Name = "labelBaslik";
            this.labelBaslik.Size = new System.Drawing.Size(170, 32);
            this.labelBaslik.TabIndex = 19;
            this.labelBaslik.Text = "Hastane Randevu";
            //
            // UyeOlForm
            //
            this.AcceptButton = this.buttonKaydet;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.labelBaslik);
            this.Controls.Add(this.pictureBoxLogo);
            this.Controls.Add(this.pictureBoxArkaplan);
            this.Controls.Add(this.labelSifreKurali);
            this.Controls.Add(this.labelIsim);
            this.Controls.Add(this.labelSifre);
            this.Controls.Add(this.labelTC);
            this.Controls.Add(this.textBoxIsim);
            this.Controls.Add(this.textBoxSifre);
            this.Controls.Add(this.textBoxTC);
            this.Controls.Add(this.buttonKaydet);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "UyeOlForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Hastane Randevu - Üye Kayıt";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxArkaplan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonKaydet;
        private System.Windows.Forms.TextBox textBoxSifre;
        private System.Windows.Forms.TextBox textBoxTC;
        private System.Windows.Forms.TextBox textBoxIsim;
        private System.Windows.Forms.Label labelTC;
        private System.Windows.Forms.Label labelSifre;
        private System.Windows.Forms.Label labelIsim;
        private System.Windows.Forms.Label labelSifreKurali;
        private System.Windows.Forms.PictureBox pictureBoxArkaplan;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
        private System.Windows.Forms.Label labelBaslik;
    }
}
