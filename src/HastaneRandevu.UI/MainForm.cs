using System.Globalization;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// Patient panel: find a doctor by name or branch, see the doctor's calendar or the first free slot,
    /// book a slot and list, move or cancel own appointments.
    /// </summary>
    public sealed class MainForm : AppDialog
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

        private readonly Label labelHastaBilgi = new() { Name = nameof(labelHastaBilgi), AutoSize = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly PlaceholderLabel labelHatirlatma = new() { Name = nameof(labelHatirlatma), Placeholder = "Yakında başlayacak bir randevunuz yok." };
        private readonly TextBox textBoxDoktorAra = new() { Name = nameof(textBoxDoktorAra), MaxLength = 100, PlaceholderText = "Ad soyad" };
        private readonly ThemedComboBox comboBoxBrans = FormLayout.Picker(nameof(comboBoxBrans), 160);
        private readonly ThemedComboBox comboBoxDoktorlar = FormLayout.Picker(nameof(comboBoxDoktorlar), 300, "Doktor seçin");
        private readonly DateTimePicker dateTimePickerRandevu = FormLayout.DatePicker(nameof(dateTimePickerRandevu));
        private readonly ThemedComboBox comboBoxSaat = FormLayout.Picker(nameof(comboBoxSaat), 110, "Saat seçin");
        private readonly ThemedButton buttonIlkBos = FormLayout.Button(nameof(buttonIlkBos), "İlk boş saat", ButtonKind.Secondary, Glyphs.Clock);
        private readonly ThemedButton buttonTakvim = FormLayout.Button(nameof(buttonTakvim), "Takvimde gör", ButtonKind.Secondary, Glyphs.Calendar);
        private readonly Label labelIlkBos = new() { Name = nameof(labelIlkBos), AutoSize = true, UseMnemonic = false };
        private readonly ThemedButton buttonRandevuAl = FormLayout.Button(nameof(buttonRandevuAl), "Randevu Al", ButtonKind.Primary);
        private readonly ThemedListBox listBoxRandevuGecmisi = new() { Name = nameof(listBoxRandevuGecmisi), AccessibleName = "Aktif randevularım" };
        private readonly ThemedButton buttonErtele = FormLayout.Button(nameof(buttonErtele), "Ertele", ButtonKind.Secondary, Glyphs.Reschedule);
        private readonly ThemedButton buttonRandevuIptal = FormLayout.Button(nameof(buttonRandevuIptal), "İptal Et", ButtonKind.Danger);
        private readonly ThemedButton buttonGecmisCsv = FormLayout.Button(nameof(buttonGecmisCsv), "Geçmişi dışa aktar", ButtonKind.Subtle, Glyphs.Download);
        private readonly CardPanel _reminderCard = new() { Name = "reminderCard" };
        private readonly GlyphBadge _reminderBadge = new(Glyphs.Reminder, 36, 13F) { Name = "reminderBadge" };

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
            : base(
                "Hasta paneli",
                "Doktor arayın, boş saati seçin; randevularınızı erteleyin ya da iptal edin.",
                Glyphs.Calendar,
                new Size(1040, 660),
                new Size(900, 580))
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

            BuildLayout();
            labelHastaBilgi.Text = $"{session.DisplayName}{Environment.NewLine}T.C. {session.MaskedNationalId}";

            var today = DateTime.Today;
            Bind(() =>
            {
                dateTimePickerRandevu.MinDate = today;
                dateTimePickerRandevu.MaxDate = today.AddDays(AppointmentRules.MaxDaysAhead);
                comboBoxBrans.Items.Add(AllBranches);
                comboBoxBrans.SelectedIndex = 0;
            });

            Load += async (_, _) => await LoadAsync();
            textBoxDoktorAra.TextChanged += async (_, _) =>
            {
                if (_binding == 0)
                {
                    await SearchDoctorsAsync();
                }
            };
            comboBoxBrans.SelectedIndexChanged += async (_, _) =>
            {
                if (_binding == 0)
                {
                    await SearchDoctorsAsync();
                }
            };
            comboBoxDoktorlar.SelectedIndexChanged += async (_, _) =>
            {
                if (_binding == 0)
                {
                    labelIlkBos.Text = string.Empty;
                    await LoadSlotsAsync();
                }
            };
            dateTimePickerRandevu.ValueChanged += async (_, _) =>
            {
                if (_binding == 0)
                {
                    await LoadSlotsAsync();
                }
            };
            labelHatirlatma.TextChanged += (_, _) => ShowReminderStyle();
            buttonIlkBos.Click += async (_, _) => await FindFirstFreeAsync();
            buttonTakvim.Click += async (_, _) => await ShowCalendarAsync();
            buttonRandevuAl.Click += async (_, _) => await BookAsync();
            buttonRandevuIptal.Click += async (_, _) => await CancelAsync();
            buttonErtele.Click += async (_, _) => await RescheduleSelectedAsync();
            buttonGecmisCsv.Click += async (_, _) => await ExportHistoryAsync();
        }

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
                comboBoxDoktorlar.Placeholder = doctors.Count == 0 ? "Aramaya uyan doktor yok" : "Doktor seçin";
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
                comboBoxSaat.Placeholder = "Saat seçin";
                return;
            }

            comboBoxSaat.Placeholder = "Yükleniyor…";
            var day = dateTimePickerRandevu.Value.Date;
            await this.RunAsync(async () =>
            {
                var slots = await _appointments.GetAvailableSlotsAsync(_session, doctor.Id, day);
                if (_slotRequests.IsLatest(request) && !IsDisposed)
                {
                    var items = slots.Select(s => new SlotItem(s)).ToList();
                    comboBoxSaat.ReplaceItems(items);
                    comboBoxSaat.Placeholder = items.Count == 0 ? "Bu gün boş saat yok" : "Saat seçin";
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

            // The dialog closes once the appointment has moved; its result is reported in this window.
            if (Prompts is ScreenPrompts)
            {
                dialog.Prompts = new ScreenPrompts(dialog) { Results = this };
            }

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
            listBoxRandevuGecmisi.IsLoading = true;
            try
            {
                var appointments = await _appointments.GetMyAppointmentsAsync(_session);
                if (_listRequests.IsLatest(request) && !IsDisposed)
                {
                    listBoxRandevuGecmisi.ReplaceItems(appointments.Select(a => new PatientAppointmentItem(a)));
                }
            }
            finally
            {
                if (_listRequests.IsLatest(request))
                {
                    listBoxRandevuGecmisi.IsLoading = false;
                }
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

        /// <summary>An upcoming appointment turns the reminder card warm; without one it stays quiet.</summary>
        private void ShowReminderStyle()
        {
            var p = Theme.Current;
            var due = labelHatirlatma.Text.Length > 0;
            _reminderCard.Fill = due ? p.WarmSubtle : null;
            _reminderCard.Edge = due ? p.Warm : null;
            _reminderBadge.Fill = due ? p.Surface : p.SurfaceAlt;
            _reminderBadge.GlyphColor = due ? p.WarmText : p.TextMuted;
            _reminderCard.Invalidate(invalidateChildren: true);
        }

        private void BuildLayout()
        {
            Name = nameof(MainForm);
            var p = Theme.Current;
            labelHastaBilgi.Font = Theme.Fonts.Body;
            labelHastaBilgi.ForeColor = p.TextSecondary;
            SetHeaderAside(labelHastaBilgi);

            // Booking card: search, doctor, day and time, the shortcuts and the primary action.
            var search = FormLayout.FieldRow(
                "searchRow",
                (FormLayout.Field("Doktor &ara", FormLayout.Input(textBoxDoktorAra)), 55),
                (FormLayout.Field("&Branş", comboBoxBrans), 45));
            var doctor = FormLayout.Field("&Doktor", comboBoxDoktorlar);
            doctor.Margin = new Padding(0, 0, 0, Spacing.M);
            var slot = FormLayout.FieldRow(
                "slotRow",
                (FormLayout.Field("&Tarih", dateTimePickerRandevu), 0),
                (FormLayout.Field("&Saat", comboBoxSaat), 100));
            buttonIlkBos.Margin = new Padding(0, 0, Spacing.S, 0);
            buttonTakvim.Margin = Padding.Empty;
            var shortcuts = FormLayout.Bar("shortcutBar", (buttonIlkBos, false), (buttonTakvim, false), (FormLayout.Spacer(), true));
            labelIlkBos.Font = Theme.Fonts.BodyStrong;
            labelIlkBos.ForeColor = p.AccentText;
            labelIlkBos.Margin = new Padding(0, Spacing.S, 0, 0);
            buttonRandevuAl.AutoSize = false;
            buttonRandevuAl.Dock = DockStyle.Top;
            buttonRandevuAl.Margin = new Padding(0, Spacing.L, 0, 0);
            var bookingNote = FormLayout.Hint("Randevular 30 dakikalıktır ve en fazla 90 gün sonrasına alınabilir.");
            bookingNote.Name = "bookingHint";
            bookingNote.Margin = new Padding(0, Spacing.M, 0, 0);
            var booking = FormLayout.Stack("bookingForm", search, doctor, slot, shortcuts, labelIlkBos, buttonRandevuAl, bookingNote);
            buttonRandevuAl.Dock = DockStyle.Top;
            var bookingCard = FormLayout.Card("bookingCard", "Randevu al", booking);
            booking.Dock = DockStyle.Top;
            bookingCard.Margin = new Padding(0, 0, Spacing.S, Spacing.L);

            // Reminder card: a bell and the upcoming appointment, or a quiet line when there is none.
            _reminderBadge.Margin = new Padding(0, 0, Spacing.M, 0);
            _reminderBadge.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            labelHatirlatma.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            var reminder = new TableLayoutPanel
            {
                Name = "reminderLayout",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
            };
            reminder.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            reminder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            reminder.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            reminder.Controls.Add(_reminderBadge, 0, 0);
            reminder.Controls.Add(labelHatirlatma, 1, 0);
            _reminderCard.Controls.Add(reminder);
            _reminderCard.Padding = new Padding(Spacing.L, Spacing.M, Spacing.L, Spacing.M);
            _reminderCard.AutoSize = true;
            _reminderCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ShowReminderStyle();

            // Appointment list with its actions.
            listBoxRandevuGecmisi.Describe = item => item is PatientAppointmentItem appointment ? Row(appointment.Appointment) : new ListRow(item.ToString() ?? string.Empty);
            listBoxRandevuGecmisi.EmptyText = "Aktif randevunuz yok. Soldan doktor ve saat seçerek randevu alabilirsiniz.";
            buttonErtele.Margin = new Padding(0, 0, Spacing.S, 0);
            buttonRandevuIptal.Margin = Padding.Empty;
            buttonGecmisCsv.Margin = Padding.Empty;
            var actions = FormLayout.Bar("listActions", (buttonErtele, false), (buttonRandevuIptal, false), (FormLayout.Spacer(), true), (buttonGecmisCsv, false));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);
            var listCard = FormLayout.Card("appointmentsCard", "Randevularım", FormLayout.Page("appointmentsPage", (listBoxRandevuGecmisi, true), (actions, false)));
            listCard.Margin = Padding.Empty;

            var right = FormLayout.Page("rightColumn", (_reminderCard, false), (listCard, true));
            Content.Controls.Add(FormLayout.Columns("layout", bookingCard, 42, right));

            SetFooterNote("Ertelemek ya da iptal etmek için listeden bir randevu seçin.");
            SetFooterButtons(CreateCloseButton("buttonCikis", "Çıkış"));
            FinishLayout();
        }

        /// <summary>Doctor and branch on the first line, the day and time on the second.</summary>
        private static ListRow Row(AppointmentDetails appointment) => new(
            string.IsNullOrWhiteSpace(appointment.Branch) ? appointment.DoctorName : $"{appointment.DoctorName} · {appointment.Branch}",
            appointment.StartsAt.ToString("d MMMM yyyy dddd, HH:mm", DisplayFormat.Turkish),
            RowTone.Accent);
    }
}
