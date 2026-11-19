using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>Doctor panel: the appointments made with the signed-in doctor and the way to the own schedule.</summary>
    public sealed class DoktorForm : AppDialog
    {
        private readonly IAppointmentService _appointments;
        private readonly IExportService _exports;
        private readonly IFormFactory _forms;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _cancel = new();
        private readonly ExclusiveOperation _export = new();
        private readonly RequestSequence _listRequests = new();

        private readonly Label labelDoktor = new() { Name = nameof(labelDoktor), AutoSize = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly ThemedListBox listBoxRandevular = new() { Name = nameof(listBoxRandevular), AccessibleName = "Randevular" };
        private readonly ThemedButton buttonRandevuIptal = FormLayout.Button(nameof(buttonRandevuIptal), "Randevuyu iptal et", ButtonKind.Danger);
        private readonly Label labelOzet = FormLayout.Hint(string.Empty);
        private readonly DateTimePicker dateTimePickerAjanda = FormLayout.DatePicker(nameof(dateTimePickerAjanda));
        private readonly ThemedButton buttonAjanda = FormLayout.Button(nameof(buttonAjanda), "Ajandayı dışa aktar", ButtonKind.Secondary, Glyphs.Download);
        private readonly ThemedButton buttonCalismaProgrami = FormLayout.Button(nameof(buttonCalismaProgrami), "Çalışma programım", ButtonKind.Secondary, Glyphs.Schedule);

        public DoktorForm(IAppointmentService appointments, IExportService exports, IFormFactory forms, UserSession session)
            : base(
                "Doktor paneli",
                "Randevularınız, günlük ajandanız ve çalışma programınız.",
                Glyphs.Agenda,
                new Size(1000, 620),
                new Size(860, 540))
        {
            ArgumentNullException.ThrowIfNull(appointments);
            ArgumentNullException.ThrowIfNull(exports);
            ArgumentNullException.ThrowIfNull(forms);
            ArgumentNullException.ThrowIfNull(session);

            _appointments = appointments;
            _exports = exports;
            _forms = forms;
            _session = session;

            BuildLayout();
            labelDoktor.Text = session.DisplayName + Environment.NewLine + "Doktor hesabı";

            Load += async (_, _) => await LoadAsync();
            buttonRandevuIptal.Click += async (_, _) => await CancelAsync();
            buttonCalismaProgrami.Click += async (_, _) => await OpenScheduleAsync();
            buttonAjanda.Click += async (_, _) => await ExportAgendaAsync();
        }

        /// <summary>Loads the doctor's appointments; only the latest of overlapping loads is shown.</summary>
        internal Task LoadAsync() => this.RunAsync(RefreshAppointmentsAsync);

        /// <summary>Cancels the selected appointment; ignored while a previous cancellation is still running.</summary>
        internal Task<bool> CancelAsync() =>
            this.RunExclusiveAsync(
                _cancel,
                async () =>
                {
                    if (listBoxRandevular.SelectedItem is not DoctorAppointmentItem item)
                    {
                        Prompts.Warn("Lütfen iptal etmek için bir randevu seçin.");
                        return;
                    }

                    if (!Prompts.Confirm("Seçilen randevuyu iptal etmek istediğinize emin misiniz?"))
                    {
                        return;
                    }

                    await _appointments.CancelAsync(_session, item.Appointment.Id);
                    Prompts.Inform("Randevu başarıyla iptal edildi.");
                    await RefreshAppointmentsAsync();
                },
                buttonRandevuIptal);

        /// <summary>
        /// Saves the agenda of the chosen day as a printable HTML page; ignored while a previous export is
        /// still running.
        /// </summary>
        internal Task<bool> ExportAgendaAsync() =>
            this.RunExclusiveAsync(
                _export,
                async () =>
                {
                    var day = DateOnly.FromDateTime(dateTimePickerAjanda.Value);
                    var path = Prompts.AskSavePath(
                        $"ajanda-{day:yyyyMMdd}.html",
                        "HTML dosyası (*.html)|*.html");
                    if (path is null)
                    {
                        return;
                    }

                    var count = await _exports.ExportAgendaHtmlAsync(_session, day, path);
                    Prompts.Inform($"{DisplayFormat.Day(day)} ajandası ({count} randevu) kaydedildi. Dosyayı tarayıcıda açıp yazdırabilirsiniz.");
                },
                buttonAjanda,
                dateTimePickerAjanda);

        private async Task RefreshAppointmentsAsync()
        {
            var request = _listRequests.Next();
            listBoxRandevular.IsLoading = true;
            try
            {
                var appointments = await _appointments.GetMyAppointmentsAsync(_session);
                if (_listRequests.IsLatest(request) && !IsDisposed)
                {
                    listBoxRandevular.ReplaceItems(appointments.Select(a => new DoctorAppointmentItem(a)));
                    labelOzet.Text = appointments.Count == 0 ? string.Empty : $"{appointments.Count} aktif randevu";
                }
            }
            finally
            {
                if (_listRequests.IsLatest(request))
                {
                    listBoxRandevular.IsLoading = false;
                }
            }
        }

        private async Task OpenScheduleAsync()
        {
            using (var schedule = _forms.Create<CalismaProgramiForm>(_session))
            {
                schedule.ShowDialog(this);
            }

            // Leave days and hours do not change the list, but another window may have cancelled meanwhile.
            await LoadAsync();
        }

        private void BuildLayout()
        {
            Name = nameof(DoktorForm);
            var p = Theme.Current;
            labelDoktor.Font = Theme.Fonts.Body;
            labelDoktor.ForeColor = p.TextSecondary;
            SetHeaderAside(labelDoktor);

            // The appointment list with its action.
            listBoxRandevular.Describe = item => item is DoctorAppointmentItem appointment
                ? new ListRow(appointment.Appointment.PatientName, appointment.Appointment.StartsAt.ToString("d MMMM yyyy dddd, HH:mm", DisplayFormat.Turkish), RowTone.Accent)
                : new ListRow(item.ToString() ?? string.Empty);
            listBoxRandevular.EmptyText = "Sizinle alınmış aktif bir randevu yok.";
            buttonRandevuIptal.Margin = Padding.Empty;
            labelOzet.Name = nameof(labelOzet);
            labelOzet.Margin = new Padding(Spacing.M, 0, 0, 0);
            var actions = FormLayout.Bar("listActions", (buttonRandevuIptal, false), (labelOzet, true));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);
            var listCard = FormLayout.Card("appointmentsCard", "Randevular", FormLayout.Page("appointmentsPage", (listBoxRandevular, true), (actions, false)));
            listCard.Margin = Padding.Empty;

            // Agenda export of one day.
            var dayField = FormLayout.Field("&Gün", dateTimePickerAjanda);
            dayField.Margin = new Padding(0, 0, 0, Spacing.S);
            buttonAjanda.Margin = new Padding(0, 0, 0, 0);
            var agendaHint = FormLayout.Hint("Seçilen günün randevuları yazdırılabilir bir HTML sayfası olarak kaydedilir; kimlik numaraları maskelenir.");
            agendaHint.Name = "agendaHint";
            agendaHint.Margin = new Padding(0, Spacing.S, 0, 0);
            var agendaCard = FormLayout.Card("agendaCard", "Günlük ajanda", FormLayout.Stack("agendaStack", dayField, buttonAjanda, agendaHint), fill: false);

            // The way to the working schedule.
            var scheduleHint = FormLayout.Hint("Çalışma günlerinizi, saatlerinizi ve izin günlerinizi düzenleyin. Mevcut randevular değişmez.");
            scheduleHint.Name = "scheduleHint";
            scheduleHint.Margin = new Padding(0, 0, 0, Spacing.M);
            buttonCalismaProgrami.Margin = Padding.Empty;
            var scheduleCard = FormLayout.Card("scheduleCard", "Çalışma programı", FormLayout.Stack("scheduleStack", scheduleHint, buttonCalismaProgrami), fill: false);
            scheduleCard.Margin = Padding.Empty;

            var right = FormLayout.Page("rightColumn", (agendaCard, false), (scheduleCard, false));
            Content.Controls.Add(FormLayout.Columns("layout", listCard, 60, right));

            SetFooterNote("İptal etmek için listeden bir randevu seçin.");
            SetFooterButtons(CreateCloseButton("buttonCikis", "Çıkış"));
            FinishLayout();
        }
    }
}
