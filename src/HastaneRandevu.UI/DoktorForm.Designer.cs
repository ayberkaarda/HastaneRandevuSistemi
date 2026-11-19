namespace HastaneRandevu.UI
{
    partial class DoktorForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label labelDoktor;
        private System.Windows.Forms.ListBox listBoxRandevular;
        private System.Windows.Forms.Button buttonCikis;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonRandevuIptal;
        private System.Windows.Forms.Button buttonCalismaProgrami;
        private System.Windows.Forms.DateTimePicker dateTimePickerAjanda;
        private System.Windows.Forms.Button buttonAjanda;

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
            this.labelDoktor = new System.Windows.Forms.Label();
            this.listBoxRandevular = new System.Windows.Forms.ListBox();
            this.buttonCikis = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonRandevuIptal = new System.Windows.Forms.Button();
            this.buttonCalismaProgrami = new System.Windows.Forms.Button();
            this.dateTimePickerAjanda = new System.Windows.Forms.DateTimePicker();
            this.buttonAjanda = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelDoktor
            //
            this.labelDoktor.AutoSize = true;
            this.labelDoktor.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.labelDoktor.Location = new System.Drawing.Point(12, 9);
            this.labelDoktor.Name = "labelDoktor";
            this.labelDoktor.Size = new System.Drawing.Size(132, 28);
            this.labelDoktor.TabIndex = 0;
            this.labelDoktor.Text = "Hoş geldiniz...";
            //
            // listBoxRandevular
            //
            this.listBoxRandevular.FormattingEnabled = true;
            this.listBoxRandevular.ItemHeight = 16;
            this.listBoxRandevular.Location = new System.Drawing.Point(23, 94);
            this.listBoxRandevular.Name = "listBoxRandevular";
            this.listBoxRandevular.Size = new System.Drawing.Size(361, 180);
            this.listBoxRandevular.TabIndex = 2;
            //
            // buttonCikis
            //
            this.buttonCikis.Location = new System.Drawing.Point(23, 380);
            this.buttonCikis.Name = "buttonCikis";
            this.buttonCikis.Size = new System.Drawing.Size(100, 30);
            this.buttonCikis.TabIndex = 7;
            this.buttonCikis.Text = "Çıkış";
            this.buttonCikis.UseVisualStyleBackColor = true;
            this.buttonCikis.Click += new System.EventHandler(this.buttonCikis_Click);
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 10.2F);
            this.label1.Location = new System.Drawing.Point(24, 65);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(209, 23);
            this.label1.TabIndex = 4;
            this.label1.Text = "Sisteme Kayıtlı Randevular";
            //
            // buttonRandevuIptal
            //
            this.buttonRandevuIptal.Location = new System.Drawing.Point(23, 290);
            this.buttonRandevuIptal.Name = "buttonRandevuIptal";
            this.buttonRandevuIptal.Size = new System.Drawing.Size(170, 30);
            this.buttonRandevuIptal.TabIndex = 3;
            this.buttonRandevuIptal.Text = "Randevu İptal";
            this.buttonRandevuIptal.UseVisualStyleBackColor = true;
            this.buttonRandevuIptal.Click += new System.EventHandler(this.buttonRandevuIptal_Click);
            //
            // buttonCalismaProgrami
            //
            this.buttonCalismaProgrami.Location = new System.Drawing.Point(214, 290);
            this.buttonCalismaProgrami.Name = "buttonCalismaProgrami";
            this.buttonCalismaProgrami.Size = new System.Drawing.Size(170, 30);
            this.buttonCalismaProgrami.TabIndex = 4;
            this.buttonCalismaProgrami.Text = "Çalışma Programım";
            this.buttonCalismaProgrami.UseVisualStyleBackColor = true;
            this.buttonCalismaProgrami.Click += new System.EventHandler(this.buttonCalismaProgrami_Click);
            //
            // dateTimePickerAjanda
            //
            this.dateTimePickerAjanda.Location = new System.Drawing.Point(23, 334);
            this.dateTimePickerAjanda.Name = "dateTimePickerAjanda";
            this.dateTimePickerAjanda.Size = new System.Drawing.Size(170, 22);
            this.dateTimePickerAjanda.TabIndex = 5;
            //
            // buttonAjanda
            //
            this.buttonAjanda.Location = new System.Drawing.Point(214, 330);
            this.buttonAjanda.Name = "buttonAjanda";
            this.buttonAjanda.Size = new System.Drawing.Size(170, 30);
            this.buttonAjanda.TabIndex = 6;
            this.buttonAjanda.Text = "Ajandayı Dışa Aktar";
            this.buttonAjanda.UseVisualStyleBackColor = true;
            this.buttonAjanda.Click += new System.EventHandler(this.buttonAjanda_Click);
            //
            // DoktorForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 430);
            this.Controls.Add(this.buttonAjanda);
            this.Controls.Add(this.dateTimePickerAjanda);
            this.Controls.Add(this.buttonCalismaProgrami);
            this.Controls.Add(this.buttonRandevuIptal);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buttonCikis);
            this.Controls.Add(this.listBoxRandevular);
            this.Controls.Add(this.labelDoktor);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "DoktorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hastane Randevu - Doktor Paneli";
            this.Load += new System.EventHandler(this.DoktorForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }
}
