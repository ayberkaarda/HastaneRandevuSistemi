using HastaneRandevu.Business.Services;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>Doctor panel: the appointments made with the signed-in doctor.</summary>
    public partial class DoktorForm : Form
    {
        private readonly IAppointmentService _appointments;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _cancel = new();
        private readonly RequestSequence _listRequests = new();

        public DoktorForm(IAppointmentService appointments, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(appointments);
            ArgumentNullException.ThrowIfNull(session);

            _appointments = appointments;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            Icon = AppAssets.Icon;
            labelDoktor.Text = session.DisplayName + " Hoş geldiniz!";
        }

        /// <summary>Confirmation and information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

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

        private async Task RefreshAppointmentsAsync()
        {
            var request = _listRequests.Next();
            var appointments = await _appointments.GetMyAppointmentsAsync(_session);
            if (_listRequests.IsLatest(request) && !IsDisposed)
            {
                listBoxRandevular.ReplaceItems(appointments.Select(a => new DoctorAppointmentItem(a)));
            }
        }

        private async void DoktorForm_Load(object? sender, EventArgs e) => await LoadAsync();

        private async void buttonRandevuIptal_Click(object? sender, EventArgs e) => await CancelAsync();

        private void buttonCikis_Click(object? sender, EventArgs e) => Close();
    }
}
