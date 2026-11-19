using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// Moves one of the patient's appointments to a free slot of the same doctor. The move runs through an
    /// <see cref="ExclusiveOperation"/> and slot loads are latest-wins.
    /// </summary>
    public sealed class ErteleForm : AppDialog
    {
        private readonly IAppointmentService _appointments;
        private readonly UserSession _session;
        private readonly AppointmentDetails _appointment;
        private readonly ExclusiveOperation _move = new();
        private readonly RequestSequence _slotRequests = new();
        private readonly Label labelRandevu = FormLayout.Text(nameof(labelRandevu));
        private readonly DateTimePicker dateTimePickerYeni = FormLayout.DatePicker(nameof(dateTimePickerYeni));
        private readonly ThemedComboBox comboBoxYeniSaat = FormLayout.Picker(nameof(comboBoxYeniSaat), 120, "Saat seçin");
        private readonly ThemedButton buttonErtele = FormLayout.Button(nameof(buttonErtele), "Ertele", ButtonKind.Primary);
        private readonly ThemedButton buttonVazgec = FormLayout.Button(nameof(buttonVazgec), "Vazgeç");

        public ErteleForm(IAppointmentService appointments, UserSession session, AppointmentDetails appointment)
            : base("Randevuyu ertele", "Randevunuzu aynı doktorun boş bir saatine taşıyın.", Glyphs.Reschedule, new Size(580, 360), new Size(520, 340), modal: true)
        {
            ArgumentNullException.ThrowIfNull(appointments);
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(appointment);

            _appointments = appointments;
            _session = session;
            _appointment = appointment;

            BuildLayout();
            labelRandevu.Text = string.IsNullOrWhiteSpace(appointment.Branch)
                ? appointment.DoctorName
                : $"{appointment.DoctorName} · {appointment.Branch}";
            labelRandevu.Text += Environment.NewLine + appointment.StartsAt.ToString("d MMMM yyyy dddd, HH:mm", DisplayFormat.Turkish);
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

        /// <summary>Loads the doctor's free slots of the chosen day; only the latest answer is shown.</summary>
        internal async Task LoadSlotsAsync()
        {
            var request = _slotRequests.Next();
            comboBoxYeniSaat.Items.Clear();
            comboBoxYeniSaat.Placeholder = "Yükleniyor…";
            var day = dateTimePickerYeni.Value.Date;
            await this.RunAsync(async () =>
            {
                var slots = await _appointments.GetAvailableSlotsAsync(_session, _appointment.DoctorId, day);
                if (_slotRequests.IsLatest(request) && !IsDisposed)
                {
                    comboBoxYeniSaat.ReplaceItems(slots.Select(s => new SlotItem(s)));
                    comboBoxYeniSaat.Placeholder = slots.Count == 0 ? "Bu gün boş saat yok" : "Saat seçin";
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
            Name = nameof(ErteleForm);

            var current = FormLayout.Hint("Şu anki randevu");
            current.Name = "currentCaption";
            current.Margin = new Padding(0, 0, 0, Spacing.XS);
            labelRandevu.Font = Theme.Fonts.BodyStrong;
            var summary = FormLayout.Card("currentCard", null, FormLayout.Stack("currentStack", current, labelRandevu), fill: false);
            summary.Fill = Theme.Current.AccentSubtle;
            summary.Padding = new Padding(Spacing.L, Spacing.M, Spacing.L, Spacing.M);

            var fields = FormLayout.FieldRow(
                "newSlotRow",
                (FormLayout.Field("Yeni &tarih", dateTimePickerYeni), 0),
                (FormLayout.Field("Yeni &saat", comboBoxYeniSaat), 100));
            var hint = FormLayout.Hint("Yalnızca aynı doktorun o gündeki boş saatleri listelenir.");
            hint.Name = "slotHint";
            hint.Margin = Padding.Empty;

            Content.Controls.Add(FormLayout.Page("page", (summary, false), (fields, false), (hint, false)));
            AcceptButton = buttonErtele;
            CancelButton = buttonVazgec;
            buttonVazgec.DialogResult = DialogResult.Cancel;
            SetFooterNote("Esc ile vazgeçebilirsiniz.");
            SetFooterButtons(buttonErtele, buttonVazgec);
            FinishLayout();
        }
    }
}
