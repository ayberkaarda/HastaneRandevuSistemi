using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// Patient panel: find a doctor by name or branch, see the doctor's calendar or the first free slot,
    /// book a slot and list, move or cancel own appointments.
    /// </summary>
    public partial class MainForm : Form
    {
        internal const string AllBranches = "Tüm branşlar";

        private readonly IAppointmentService _appointments;
        private readonly IAvailabilityService _availability;
        private readonly IReminderService _reminders;
        private readonly IExportService _exports;
        private readonly IReminderNotifier _notifier;
        private readonly IFormFactory _forms;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _book = new();
        private readonly ExclusiveOperation _cancel = new();
        private readonly ExclusiveOperation _export = new();
        private readonly RequestSequence _slotRequests = new();
        private readonly RequestSequence _listRequests = new();
        private readonly RequestSequence _searchRequests = new();
        private readonly RequestSequence _firstFreeRequests = new();

        // While above zero, list and picker changes are made by the code, not by the user, and must not
        // start loads as a user selection would.
        private int _binding;

        private bool _remindersShown;

        public MainForm(
            IAppointmentService appointments,
            IAvailabilityService availability,
            IReminderService reminders,
            IExportService exports,
            IReminderNotifier notifier,
            IFormFactory forms,
            UserSession session)
        {
            ArgumentNullException.ThrowIfNull(appointments);
            ArgumentNullException.ThrowIfNull(availability);
            ArgumentNullException.ThrowIfNull(reminders);
            ArgumentNullException.ThrowIfNull(exports);
            ArgumentNullException.ThrowIfNull(notifier);
            ArgumentNullException.ThrowIfNull(forms);
            ArgumentNullException.ThrowIfNull(session);

            _appointments = appointments;
            _availability = availability;
            _reminders = reminders;
            _exports = exports;
            _notifier = notifier;
            _forms = forms;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            Icon = AppAssets.Icon;
            pictureBoxLogo.Image = AppAssets.Logo;
            labelHastaBilgi.Text = $"Hoşgeldiniz, {session.DisplayName} (T.C. {session.MaskedNationalId})";

            var today = DateTime.Today;
            Bind(() =>
            {
                dateTimePickerRandevu.MinDate = today;
                dateTimePickerRandevu.MaxDate = today.AddDays(AppointmentRules.MaxDaysAhead);
                comboBoxBrans.Items.Add(AllBranches);
                comboBoxBrans.SelectedIndex = 0;
            });
        }

        /// <summary>Confirmation and information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Loads the branches, the doctors, the patient's appointments and the reminders.</summary>
        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var branches = await _availability.GetBranchesAsync(_session);
                if (IsDisposed)
                {
                    return;
                }

                Bind(() =>
                {
                    comboBoxBrans.ReplaceItems(branches.Prepend(AllBranches));
                    comboBoxBrans.SelectedIndex = 0;
                });
                await SearchDoctorsAsync();
                await RefreshAppointmentsAsync();
                await ShowRemindersAsync();
            });

        /// <summary>
        /// Shows the appointments starting soon inside the window and, once per panel, as a balloon that
        /// does not take the focus (when switched on in the settings).
        /// </summary>
        internal async Task ShowRemindersAsync()
        {
            var upcoming = await _reminders.GetUpcomingAsync(_session);
            if (IsDisposed)
            {
                return;
            }

            if (upcoming.Count == 0)
            {
                labelHatirlatma.Text = string.Empty;
                return;
            }

            var text = ReminderText.Describe(upcoming);
            labelHatirlatma.Text = "Yaklaşan randevunuz:" + Environment.NewLine + text;
            if (!_remindersShown)
            {
                _remindersShown = true;
                _notifier.Notify(ReminderText.Title, text);
            }
        }

        /// <summary>Saves the patient's own history as CSV; ignored while a previous export is still running.</summary>
        internal Task<bool> ExportHistoryAsync() =>
            this.RunExclusiveAsync(
                _export,
                async () =>
                {
                    var path = Prompts.AskSavePath(
                        $"randevu-gecmisi-{DateTime.Today:yyyyMMdd}.csv",
                        "CSV dosyası (*.csv)|*.csv");
                    if (path is null)
                    {
                        return;
                    }

                    var count = await _exports.ExportHistoryCsvAsync(_session, path);
                    Prompts.Inform($"Randevu geçmişiniz ({count} randevu) dışa aktarıldı.");
                },
                buttonGecmisCsv);

        /// <summary>
        /// Filters the doctor list by the name and branch entered; only the answer to the latest request
        /// is shown. The selected doctor stays selected when it is still in the list; otherwise nothing is
        /// selected and no slots are shown, without a load being started.
        /// </summary>
        internal async Task SearchDoctorsAsync()
        {
            var request = _searchRequests.Next();
            var name = textBoxDoktorAra.Text;
            var branch = comboBoxBrans.SelectedItem as string is { } chosen && chosen != AllBranches ? chosen : null;
            await this.RunAsync(async () =>
            {
                var doctors = await _availability.SearchDoctorsAsync(_session, name, branch);
                if (!_searchRequests.IsLatest(request) || IsDisposed)
                {
                    return;
                }

                var selected = (comboBoxDoktorlar.SelectedItem as DoctorSummary)?.Id;
                Bind(() =>
                {
                    comboBoxDoktorlar.ReplaceItems(doctors);
                    comboBoxDoktorlar.SelectedItem = doctors.FirstOrDefault(d => d.Id == selected);
                });
                if (comboBoxDoktorlar.SelectedItem is null)
                {
                    _slotRequests.Next(); // answers for the doctor that left the list are stale
                    comboBoxSaat.Items.Clear();
                }
            });
        }

        /// <summary>
        /// Loads the free slots of the selected doctor and day. Selections can change faster than the
        /// answers arrive, so only the answer to the latest request is shown.
        /// </summary>
        internal async Task LoadSlotsAsync(DateTime? select = null)
        {
            var request = _slotRequests.Next();
            comboBoxSaat.Items.Clear();
            if (comboBoxDoktorlar.SelectedItem is not DoctorSummary doctor)
            {
                return;
            }

            var day = dateTimePickerRandevu.Value.Date;
            await this.RunAsync(async () =>
            {
                var slots = await _appointments.GetAvailableSlotsAsync(_session, doctor.Id, day);
                if (_slotRequests.IsLatest(request) && !IsDisposed)
                {
                    var items = slots.Select(s => new SlotItem(s)).ToList();
                    comboBoxSaat.ReplaceItems(items);
                    if (select is { } wanted)
                    {
                        comboBoxSaat.SelectedItem = items.FirstOrDefault(i => i.StartsAt == wanted);
                    }
                }
            });
        }

        /// <summary>
        /// Asks the service for the selected doctor's earliest free slot and shows it in the pickers, so
        /// the patient only has to confirm it with "Randevu Al". Only the latest request is applied.
        /// </summary>
        internal async Task FindFirstFreeAsync()
        {
            var request = _firstFreeRequests.Next();
            if (comboBoxDoktorlar.SelectedItem is not DoctorSummary doctor)
            {
                Prompts.Warn("Lütfen bir doktor seçiniz.");
                return;
            }

            await this.RunAsync(async () =>
            {
                var first = await _availability.FindFirstFreeSlotAsync(_session, doctor.Id);
                if (!_firstFreeRequests.IsLatest(request) || IsDisposed || !ReferenceEquals(comboBoxDoktorlar.SelectedItem, doctor))
                {
                    return;
                }

                if (first is not { } slot)
                {
                    labelIlkBos.Text = $"{AppointmentRules.MaxDaysAhead} gün içinde boş randevu bulunamadı.";
                    return;
                }

                labelIlkBos.Text = $"İlk boş randevu: {DisplayFormat.DateTime(slot)}";
                Bind(() => dateTimePickerRandevu.Value = slot.Date);
                await LoadSlotsAsync(select: slot);
            });
        }

        /// <summary>Opens the selected doctor's calendar; a chosen day is shown in the date picker.</summary>
        internal async Task<bool> ShowCalendarAsync()
        {
            if (comboBoxDoktorlar.SelectedItem is not DoctorSummary doctor)
            {
                Prompts.Warn("Lütfen bir doktor seçiniz.");
                return false;
            }

            DateOnly? chosen;
            using (var calendar = _forms.Create<TakvimForm>(_session, doctor))
            {
                if (calendar.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                chosen = calendar.SelectedDay;
            }

            if (chosen is not { } day)
            {
                return false;
            }

            Bind(() => dateTimePickerRandevu.Value = day.ToDateTime(TimeOnly.MinValue));
            await LoadSlotsAsync();
            return true;
        }

        /// <summary>Books the selected slot; ignored while a previous booking is still running.</summary>
        internal Task<bool> BookAsync() =>
            this.RunExclusiveAsync(
                _book,
                async () =>
                {
                    if (comboBoxDoktorlar.SelectedItem is not DoctorSummary doctor)
                    {
                        Prompts.Warn("Lütfen bir doktor seçiniz.");
                        return;
                    }

                    if (comboBoxSaat.SelectedItem is not SlotItem slot)
                    {
                        Prompts.Warn("Lütfen bir randevu saati seçiniz.");
                        return;
                    }

                    await _appointments.BookAsync(_session, doctor.Id, slot.StartsAt);
                    Prompts.Inform("Randevu başarıyla alındı.");
                    labelIlkBos.Text = string.Empty;
                    await RefreshAppointmentsAsync();
                    await LoadSlotsAsync();
                },
                buttonRandevuAl);

        /// <summary>Cancels the selected appointment; ignored while a previous cancellation is still running.</summary>
        internal Task<bool> CancelAsync() =>
            this.RunExclusiveAsync(
                _cancel,
                async () =>
                {
                    if (listBoxRandevuGecmisi.SelectedItem is not PatientAppointmentItem item)
                    {
                        Prompts.Warn("Lütfen iptal etmek istediğiniz randevuyu seçin.");
                        return;
                    }

                    if (!Prompts.Confirm("Seçilen randevuyu iptal etmek istediğinize emin misiniz?"))
                    {
                        return;
                    }

                    await _appointments.CancelAsync(_session, item.Appointment.Id);
                    Prompts.Inform("Randevu iptal edildi.");
                    await RefreshAppointmentsAsync();
                    await LoadSlotsAsync();
                },
                buttonRandevuIptal);

        /// <summary>
        /// Opens the reschedule dialog for the selected appointment; the lists are reloaded when it was
        /// moved. Returns whether the appointment was moved.
        /// </summary>
        internal async Task<bool> RescheduleSelectedAsync()
        {
            if (listBoxRandevuGecmisi.SelectedItem is not PatientAppointmentItem item)
            {
                Prompts.Warn("Lütfen ertelemek istediğiniz randevuyu seçin.");
                return false;
            }

            using var dialog = _forms.Create<ErteleForm>(_session, item.Appointment);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            await this.RunAsync(async () =>
            {
                await RefreshAppointmentsAsync();
                await LoadSlotsAsync();
            });
            return true;
        }

        private async Task RefreshAppointmentsAsync()
        {
            var request = _listRequests.Next();
            var appointments = await _appointments.GetMyAppointmentsAsync(_session);
            if (_listRequests.IsLatest(request) && !IsDisposed)
            {
                listBoxRandevuGecmisi.ReplaceItems(appointments.Select(a => new PatientAppointmentItem(a)));
            }
        }

        /// <summary>Runs a change made by the code; the change events it raises are not user selections.</summary>
        private void Bind(Action change)
        {
            _binding++;
            try
            {
                change();
            }
            finally
            {
                _binding--;
            }
        }

        private async void MainForm_Load(object? sender, EventArgs e) => await LoadAsync();

        private async void textBoxDoktorAra_TextChanged(object? sender, EventArgs e)
        {
            if (_binding == 0)
            {
                await SearchDoctorsAsync();
            }
        }

        private async void comboBoxBrans_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_binding == 0)
            {
                await SearchDoctorsAsync();
            }
        }

        private async void comboBoxDoktorlar_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_binding == 0)
            {
                labelIlkBos.Text = string.Empty;
                await LoadSlotsAsync();
            }
        }

        private async void dateTimePickerRandevu_ValueChanged(object? sender, EventArgs e)
        {
            if (_binding == 0)
            {
                await LoadSlotsAsync();
            }
        }

        private async void buttonIlkBos_Click(object? sender, EventArgs e) => await FindFirstFreeAsync();

        private async void buttonTakvim_Click(object? sender, EventArgs e) => await ShowCalendarAsync();

        private async void buttonRandevuAl_Click(object? sender, EventArgs e) => await BookAsync();

        private async void buttonRandevuIptal_Click(object? sender, EventArgs e) => await CancelAsync();

        private async void buttonErtele_Click(object? sender, EventArgs e) => await RescheduleSelectedAsync();

        private async void buttonGecmisCsv_Click(object? sender, EventArgs e) => await ExportHistoryAsync();

        private void buttonCikis_Click(object? sender, EventArgs e) => Close();
    }
}
