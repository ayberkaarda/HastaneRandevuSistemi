namespace HastaneRandevu.UI
{
    partial class LoginForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.buttonUyeOl = new System.Windows.Forms.Button();
            this.buttonGiris = new System.Windows.Forms.Button();
            this.textBoxSifre = new System.Windows.Forms.TextBox();
            this.textBoxTC = new System.Windows.Forms.TextBox();
            this.labelTC = new System.Windows.Forms.Label();
            this.labelSifre = new System.Windows.Forms.Label();
            this.buttonDoktorGiris = new System.Windows.Forms.Button();
            this.pictureBoxArkaplan = new System.Windows.Forms.PictureBox();
            this.pictureBoxLogo = new System.Windows.Forms.PictureBox();
            this.labelBaslik = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxArkaplan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
            this.SuspendLayout();
            //
            // buttonUyeOl
            //
            this.buttonUyeOl.ForeColor = System.Drawing.SystemColors.ControlText;
            this.buttonUyeOl.Location = new System.Drawing.Point(55, 359);
            this.buttonUyeOl.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonUyeOl.Name = "buttonUyeOl";
            this.buttonUyeOl.Size = new System.Drawing.Size(225, 39);
            this.buttonUyeOl.TabIndex = 3;
            this.buttonUyeOl.Text = "Üye Ol";
            this.buttonUyeOl.UseVisualStyleBackColor = true;
            this.buttonUyeOl.Click += new System.EventHandler(this.buttonUyeOl_Click);
            //
            // buttonGiris
            //
            this.buttonGiris.ForeColor = System.Drawing.SystemColors.ControlText;
            this.buttonGiris.Location = new System.Drawing.Point(55, 315);
            this.buttonGiris.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonGiris.Name = "buttonGiris";
            this.buttonGiris.Size = new System.Drawing.Size(225, 39);
            this.buttonGiris.TabIndex = 2;
            this.buttonGiris.Text = "Giriş Yap";
            this.buttonGiris.UseVisualStyleBackColor = true;
            this.buttonGiris.Click += new System.EventHandler(this.buttonGiris_Click);
            //
            // textBoxSifre
            //
            this.textBoxSifre.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.textBoxSifre.Location = new System.Drawing.Point(27, 258);
            this.textBoxSifre.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.textBoxSifre.MaxLength = 100;
            this.textBoxSifre.Name = "textBoxSifre";
            this.textBoxSifre.Size = new System.Drawing.Size(289, 34);
            this.textBoxSifre.TabIndex = 1;
            this.textBoxSifre.UseSystemPasswordChar = true;
            //
            // textBoxTC
            //
            this.textBoxTC.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.textBoxTC.Location = new System.Drawing.Point(27, 186);
            this.textBoxTC.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.textBoxTC.MaxLength = 11;
            this.textBoxTC.Name = "textBoxTC";
            this.textBoxTC.Size = new System.Drawing.Size(289, 34);
            this.textBoxTC.TabIndex = 0;
            //
            // labelTC
            //
            this.labelTC.AutoSize = true;
            this.labelTC.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.labelTC.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelTC.Location = new System.Drawing.Point(23, 155);
            this.labelTC.Name = "labelTC";
            this.labelTC.Size = new System.Drawing.Size(133, 28);
            this.labelTC.TabIndex = 9;
            this.labelTC.Text = "T.C. Kimlik No";
            //
            // labelSifre
            //
            this.labelSifre.AutoSize = true;
            this.labelSifre.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.labelSifre.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelSifre.Location = new System.Drawing.Point(23, 228);
            this.labelSifre.Name = "labelSifre";
            this.labelSifre.Size = new System.Drawing.Size(51, 28);
            this.labelSifre.TabIndex = 10;
            this.labelSifre.Text = "Şifre";
            //
            // buttonDoktorGiris
            //
            this.buttonDoktorGiris.ForeColor = System.Drawing.SystemColors.ControlText;
            this.buttonDoktorGiris.Location = new System.Drawing.Point(55, 405);
            this.buttonDoktorGiris.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonDoktorGiris.Name = "buttonDoktorGiris";
            this.buttonDoktorGiris.Size = new System.Drawing.Size(225, 39);
            this.buttonDoktorGiris.TabIndex = 4;
            this.buttonDoktorGiris.Text = "Doktor Giriş";
            this.buttonDoktorGiris.UseVisualStyleBackColor = true;
            this.buttonDoktorGiris.Click += new System.EventHandler(this.buttonDoktorGiris_Click);
            //
            // pictureBoxArkaplan
            //
            this.pictureBoxArkaplan.Location = new System.Drawing.Point(335, 0);
            this.pictureBoxArkaplan.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pictureBoxArkaplan.Name = "pictureBoxArkaplan";
            this.pictureBoxArkaplan.Size = new System.Drawing.Size(661, 460);
            this.pictureBoxArkaplan.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxArkaplan.TabIndex = 14;
            this.pictureBoxArkaplan.TabStop = false;
            //
            // pictureBoxLogo
            //
            this.pictureBoxLogo.Location = new System.Drawing.Point(12, 12);
            this.pictureBoxLogo.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pictureBoxLogo.Name = "pictureBoxLogo";
            this.pictureBoxLogo.Size = new System.Drawing.Size(64, 64);
            this.pictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxLogo.TabIndex = 13;
            this.pictureBoxLogo.TabStop = false;
            //
            // labelBaslik
            //
            this.labelBaslik.AutoSize = true;
            this.labelBaslik.Font = new System.Drawing.Font("Segoe UI Semibold", 14F, System.Drawing.FontStyle.Bold);
            this.labelBaslik.Location = new System.Drawing.Point(84, 28);
            this.labelBaslik.Name = "labelBaslik";
            this.labelBaslik.Size = new System.Drawing.Size(170, 32);
            this.labelBaslik.TabIndex = 12;
            this.labelBaslik.Text = "Hastane Randevu";
            //
            // LoginForm
            //
            this.AcceptButton = this.buttonGiris;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(777, 454);
            this.Controls.Add(this.pictureBoxArkaplan);
            this.Controls.Add(this.labelBaslik);
            this.Controls.Add(this.pictureBoxLogo);
            this.Controls.Add(this.buttonDoktorGiris);
            this.Controls.Add(this.labelSifre);
            this.Controls.Add(this.labelTC);
            this.Controls.Add(this.buttonUyeOl);
            this.Controls.Add(this.buttonGiris);
            this.Controls.Add(this.textBoxSifre);
            this.Controls.Add(this.textBoxTC);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hastane Randevu - Giriş";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxArkaplan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBoxTC;
        private System.Windows.Forms.TextBox textBoxSifre;
        private System.Windows.Forms.Button buttonGiris;
        private System.Windows.Forms.Button buttonUyeOl;
        private System.Windows.Forms.Label labelTC;
        private System.Windows.Forms.Label labelSifre;
        private System.Windows.Forms.Button buttonDoktorGiris;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
        private System.Windows.Forms.PictureBox pictureBoxArkaplan;
        private System.Windows.Forms.Label labelBaslik;
    }
}
