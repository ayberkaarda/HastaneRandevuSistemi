using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>Patient panel: book a slot with a doctor and list or cancel own appointments.</summary>
    public partial class MainForm : Form
    {
        private readonly IAppointmentService _appointments;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _book = new();
        private readonly ExclusiveOperation _cancel = new();
        private readonly RequestSequence _slotRequests = new();
        private readonly RequestSequence _listRequests = new();

        public MainForm(IAppointmentService appointments, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(appointments);
            ArgumentNullException.ThrowIfNull(session);

            _appointments = appointments;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            Icon = AppAssets.Icon;
            pictureBoxLogo.Image = AppAssets.Logo;
            labelHastaBilgi.Text = $"Hoşgeldiniz, {session.DisplayName} (T.C. {session.MaskedNationalId})";

            var today = DateTime.Today;
            dateTimePickerRandevu.MinDate = today;
            dateTimePickerRandevu.MaxDate = today.AddDays(AppointmentRules.MaxDaysAhead);
        }

        /// <summary>Confirmation and information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Loads the doctors and the patient's appointments.</summary>
        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var doctors = await _appointments.GetDoctorsAsync(_session);
                comboBoxDoktorlar.ReplaceItems(doctors);
                await RefreshAppointmentsAsync();
            });

        /// <summary>
        /// Loads the free slots of the selected doctor and day. Selections can change faster than the
        /// answers arrive, so only the answer to the latest request is shown.
        /// </summary>
        internal async Task LoadSlotsAsync()
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
                    comboBoxSaat.ReplaceItems(slots.Select(s => new SlotItem(s)));
                }
            });
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

        private async Task RefreshAppointmentsAsync()
        {
            var request = _listRequests.Next();
            var appointments = await _appointments.GetMyAppointmentsAsync(_session);
            if (_listRequests.IsLatest(request) && !IsDisposed)
            {
                listBoxRandevuGecmisi.ReplaceItems(appointments.Select(a => new PatientAppointmentItem(a)));
            }
        }

        private async void MainForm_Load(object? sender, EventArgs e) => await LoadAsync();

        private async void comboBoxDoktorlar_SelectedIndexChanged(object? sender, EventArgs e) => await LoadSlotsAsync();

        private async void dateTimePickerRandevu_ValueChanged(object? sender, EventArgs e) => await LoadSlotsAsync();

        private async void buttonRandevuAl_Click(object? sender, EventArgs e) => await BookAsync();

        private async void buttonRandevuIptal_Click(object? sender, EventArgs e) => await CancelAsync();

        private void buttonCikis_Click(object? sender, EventArgs e) => Close();
    }
}
