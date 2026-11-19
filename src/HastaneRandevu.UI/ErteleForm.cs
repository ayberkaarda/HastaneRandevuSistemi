using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// Moves one of the patient's appointments to a free slot of the same doctor. The layout is built in
    /// code; the move runs through an <see cref="ExclusiveOperation"/> and slot loads are latest-wins.
    /// </summary>
    public sealed class ErteleForm : Form
    {
        private readonly IAppointmentService _appointments;
        private readonly UserSession _session;
        private readonly AppointmentDetails _appointment;
        private readonly ExclusiveOperation _move = new();
        private readonly RequestSequence _slotRequests = new();
        private readonly Label labelRandevu = new() { Name = "labelRandevu", AutoSize = true, MaximumSize = new Size(440, 0) };
        private readonly DateTimePicker dateTimePickerYeni = new() { Name = "dateTimePickerYeni", Width = 220 };
        private readonly ComboBox comboBoxYeniSaat = new() { Name = "comboBoxYeniSaat", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly Button buttonErtele = new() { Name = "buttonErtele", Text = "Ertele", AutoSize = true };
        private readonly Button buttonVazgec = new() { Name = "buttonVazgec", Text = "Vazgeç", AutoSize = true, DialogResult = DialogResult.Cancel };

        public ErteleForm(IAppointmentService appointments, UserSession session, AppointmentDetails appointment)
        {
            ArgumentNullException.ThrowIfNull(appointments);
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(appointment);

            _appointments = appointments;
            _session = session;
            _appointment = appointment;
            Prompts = new MessageBoxPrompts(this);

            BuildLayout();
            Icon = AppAssets.Icon;
            labelRandevu.Text = $"Ertelenecek randevu: {new PatientAppointmentItem(appointment)}";
            var today = DateTime.Today;
            dateTimePickerYeni.MinDate = today;
            dateTimePickerYeni.MaxDate = today.AddDays(AppointmentRules.MaxDaysAhead);
            dateTimePickerYeni.Value = appointment.StartsAt.Date < today || appointment.StartsAt.Date > dateTimePickerYeni.MaxDate
                ? today
                : appointment.StartsAt.Date;

            Load += async (_, _) => await LoadSlotsAsync();
            dateTimePickerYeni.ValueChanged += async (_, _) => await LoadSlotsAsync();
            buttonErtele.Click += async (_, _) => await RescheduleAsync();
        }

        /// <summary>Confirmation and information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Loads the doctor's free slots of the chosen day; only the latest answer is shown.</summary>
        internal async Task LoadSlotsAsync()
        {
            var request = _slotRequests.Next();
            comboBoxYeniSaat.Items.Clear();
            var day = dateTimePickerYeni.Value.Date;
            await this.RunAsync(async () =>
            {
                var slots = await _appointments.GetAvailableSlotsAsync(_session, _appointment.DoctorId, day);
                if (_slotRequests.IsLatest(request) && !IsDisposed)
                {
                    comboBoxYeniSaat.ReplaceItems(slots.Select(s => new SlotItem(s)));
                }
            });
        }

        /// <summary>Moves the appointment to the selected slot; ignored while a move is still running.</summary>
        internal Task<bool> RescheduleAsync() =>
            this.RunExclusiveAsync(
                _move,
                async () =>
                {
                    if (comboBoxYeniSaat.SelectedItem is not SlotItem slot)
                    {
                        Prompts.Warn("Lütfen yeni bir randevu saati seçiniz.");
                        _move.FocusAfterUnlock(comboBoxYeniSaat);
                        return;
                    }

                    try
                    {
                        await _appointments.RescheduleAsync(_session, _appointment.Id, slot.StartsAt);
                    }
                    catch
                    {
                        // The slot may have been taken meanwhile; offer the current free slots again.
                        _move.FocusAfterUnlock(comboBoxYeniSaat);
                        _ = LoadSlotsAsync();
                        throw;
                    }

                    Prompts.Inform($"Randevunuz {DisplayFormat.DateTime(slot.StartsAt)} saatine ertelendi.");
                    DialogResult = DialogResult.OK;
                },
                buttonErtele,
                comboBoxYeniSaat,
                dateTimePickerYeni);

        private void BuildLayout()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9F);
            Text = "Hastane Randevu - Randevu Erteleme";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(480, 220);
            CancelButton = buttonVazgec;

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(12) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.Controls.Add(labelRandevu, 0, 0);
            table.SetColumnSpan(labelRandevu, 2);
            table.Controls.Add(new Label { Text = "Yeni tarih", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            table.Controls.Add(dateTimePickerYeni, 1, 1);
            table.Controls.Add(new Label { Text = "Yeni saat", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            table.Controls.Add(comboBoxYeniSaat, 1, 2);
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.Add(buttonVazgec);
            buttons.Controls.Add(buttonErtele);
            table.Controls.Add(buttons, 0, 3);
            table.SetColumnSpan(buttons, 2);
            Controls.Add(table);
            ResumeLayout(true);
        }
    }
}
