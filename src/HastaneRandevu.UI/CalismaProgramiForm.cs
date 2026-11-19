using System.Globalization;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// Doctor's own schedule: weekly working hours, leave days and the appointments that no longer fit.
    /// Every write goes through an <see cref="ExclusiveOperation"/>.
    /// </summary>
    public sealed class CalismaProgramiForm : AppDialog
    {
        private static readonly DayOfWeek[] Week =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        ];

        private readonly IScheduleService _schedules;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _save = new();
        private readonly ExclusiveOperation _leave = new();
        private readonly RequestSequence _loads = new();
        private readonly Dictionary<DayOfWeek, (CheckBox Works, ComboBox Start, ComboBox End)> _rows = [];
        private readonly Label labelVarsayilan = new() { AutoSize = true, Name = "labelVarsayilan", UseMnemonic = false };
        private readonly ThemedButton buttonKaydet = FormLayout.Button("buttonKaydet", "Programı kaydet", ButtonKind.Primary);
        private readonly DateTimePicker dateTimePickerIzin = FormLayout.DatePicker("dateTimePickerIzin");
        private readonly ThemedButton buttonIzinEkle = FormLayout.Button("buttonIzinEkle", "İzin ekle", ButtonKind.Secondary, Glyphs.Leave);
        private readonly ThemedListBox listBoxIzinler = new() { Name = "listBoxIzinler", Dock = DockStyle.Fill, AccessibleName = "İzin günleri" };
        private readonly ThemedButton buttonIzinKaldir = FormLayout.Button("buttonIzinKaldir", "Seçili izni kaldır");
        private readonly ThemedListBox listBoxCakisanlar = new() { Name = "listBoxCakisanlar", Dock = DockStyle.Fill, AccessibleName = "Programa uymayan randevular" };

        public CalismaProgramiForm(IScheduleService schedules, UserSession session)
            : base(
                "Çalışma programım",
                "Haftalık çalışma saatleriniz ve izin günleriniz; yeni randevular bu programa göre verilir.",
                Glyphs.Schedule,
                new Size(940, 800),
                new Size(800, 600),
                modal: true)
        {
            ArgumentNullException.ThrowIfNull(schedules);
            ArgumentNullException.ThrowIfNull(session);

            _schedules = schedules;
            _session = session;

            BuildLayout();
            var today = DateTime.Today;
            dateTimePickerIzin.MinDate = today;
            dateTimePickerIzin.MaxDate = today.AddDays(365);

            Load += async (_, _) => await LoadAsync();
            buttonKaydet.Click += async (_, _) => await SaveAsync();
            buttonIzinEkle.Click += async (_, _) => await AddLeaveAsync();
            buttonIzinKaldir.Click += async (_, _) => await RemoveLeaveAsync();
        }

        /// <summary>Loads the schedule and the conflicts; only the latest of overlapping loads is shown.</summary>
        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var request = _loads.Next();
                var view = await _schedules.GetMyScheduleAsync(_session);
                var conflicts = await _schedules.GetConflictsAsync(_session);
                if (_loads.IsLatest(request) && !IsDisposed)
                {
                    ShowSchedule(view);
                    ShowConflicts(conflicts);
                }
            });

        /// <summary>Saves the weekly hours; ignored while a previous save is still running.</summary>
        internal Task<bool> SaveAsync() =>
            this.RunExclusiveAsync(
                _save,
                async () =>
                {
                    var days = ReadDays();
                    var conflicts = await _schedules.SaveWorkingDaysAsync(_session, days);
                    ShowConflicts(conflicts);
                    labelVarsayilan.Text = string.Empty;
                    Prompts.Inform(conflicts.Count == 0
                        ? "Çalışma programı kaydedildi."
                        : $"Çalışma programı kaydedildi. Programa uymayan {conflicts.Count} randevu listede gösteriliyor; randevular değiştirilmedi.");
                    _save.FocusAfterUnlock(buttonKaydet);
                },
                buttonKaydet);

        /// <summary>Adds the chosen day as leave; ignored while a leave change is still running.</summary>
        internal Task<bool> AddLeaveAsync() =>
            this.RunExclusiveAsync(
                _leave,
                async () =>
                {
                    var day = DateOnly.FromDateTime(dateTimePickerIzin.Value);
                    var conflicts = await _schedules.AddLeaveDayAsync(_session, day);
                    ShowConflicts(conflicts);
                    await ReloadLeaveDaysAsync();
                    if (conflicts.Any(c => c.Reason == ScheduleConflictReason.LeaveDay && DateOnly.FromDateTime(c.Appointment.StartsAt) == day))
                    {
                        Prompts.Warn("İzin günü eklendi. Bu güne alınmış randevular iptal edilmedi; listede gösteriliyor.");
                    }

                    _leave.FocusAfterUnlock(dateTimePickerIzin);
                },
                buttonIzinEkle,
                buttonIzinKaldir);

        /// <summary>Removes the selected leave day; ignored while a leave change is still running.</summary>
        internal Task<bool> RemoveLeaveAsync() =>
            this.RunExclusiveAsync(
                _leave,
                async () =>
                {
                    if (listBoxIzinler.SelectedItem is not LeaveDayItem item)
                    {
                        Prompts.Warn("Lütfen kaldırmak istediğiniz izin gününü seçin.");
                        return;
                    }

                    await _schedules.RemoveLeaveDayAsync(_session, item.Day);
                    await ReloadLeaveDaysAsync();
                    ShowConflicts(await _schedules.GetConflictsAsync(_session));
                    _leave.FocusAfterUnlock(listBoxIzinler);
                },
                buttonIzinEkle,
                buttonIzinKaldir);

        /// <summary>The checked days with their hours, as entered.</summary>
        internal IReadOnlyList<WorkingDay> ReadDays() =>
            Week.Where(d => _rows[d].Works.Checked)
                .Select(d => new WorkingDay(
                    d,
                    ((TimeItem)_rows[d].Start.SelectedItem!).Time,
                    ((TimeItem)_rows[d].End.SelectedItem!).Time))
                .ToList();

        private async Task ReloadLeaveDaysAsync()
        {
            var view = await _schedules.GetMyScheduleAsync(_session);
            if (!IsDisposed)
            {
                listBoxIzinler.ReplaceItems(view.LeaveDays.Select(d => new LeaveDayItem(d)));
            }
        }

        private void ShowSchedule(DoctorScheduleView view)
        {
            foreach (var day in Week)
            {
                var (works, start, end) = _rows[day];
                var hours = view.WorkingDays.FirstOrDefault(d => d.Day == day);
                works.Checked = hours is not null;
                start.SelectedItem = start.Items.Cast<TimeItem>().Single(i => i.Time == (hours?.Start ?? TimeSpan.FromHours(9)));
                end.SelectedItem = end.Items.Cast<TimeItem>().Single(i => i.Time == (hours?.End ?? TimeSpan.FromHours(17)));
            }

            labelVarsayilan.Text = view.UsesDefaultHours
                ? "Henüz program kaydetmediniz; hafta içi 09:00-17:00 uygulanıyor."
                : string.Empty;
            listBoxIzinler.ReplaceItems(view.LeaveDays.Select(d => new LeaveDayItem(d)));
        }

        private void ShowConflicts(IReadOnlyList<ScheduleConflict> conflicts) =>
            listBoxCakisanlar.ReplaceItems(conflicts.Select(c => new ConflictItem(c)));

        private void BuildLayout()
        {
            Name = nameof(CalismaProgramiForm);

            var weekCard = FormLayout.Card("weekCard", "Haftalık çalışma saatleri", BuildWeek(), fill: false);
            weekCard.Dock = DockStyle.Fill;
            weekCard.Margin = new Padding(0, 0, Spacing.S, Spacing.L);
            var leaveCard = FormLayout.Card("leaveCard", "İzin ve tatil günleri", BuildLeave());
            leaveCard.Margin = new Padding(Spacing.S, 0, 0, Spacing.L);

            var top = new TableLayoutPanel
            {
                Name = "topRow",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.Controls.Add(weekCard, 0, 0);
            top.Controls.Add(leaveCard, 1, 0);

            listBoxCakisanlar.Describe = item => item is ConflictItem conflict ? ConflictRow(conflict.Conflict) : new ListRow(item.ToString() ?? string.Empty);
            listBoxCakisanlar.EmptyText = "Programınıza uymayan randevu yok.";
            var conflictHint = FormLayout.Hint("Bu randevular değiştirilmedi; gerekirse hastayla görüşüp doktor panelinden iptal edin.");
            conflictHint.Name = "conflictHint";
            conflictHint.Margin = new Padding(0, Spacing.S, 0, 0);
            var conflictsCard = FormLayout.Card("conflictsCard", "Programa uymayan randevular", FormLayout.Page("conflictsPage", (listBoxCakisanlar, true), (conflictHint, false)));
            conflictsCard.Margin = Padding.Empty;

            Content.Controls.Add(FormLayout.Page("page", (top, false), (conflictsCard, true)));
            SetFooterNote("Kaydettiğiniz program yeni randevulara uygulanır; mevcut randevular değişmez.");
            SetFooterButtons(CreateCloseButton("buttonKapat"));
            FinishLayout();
        }

        private TableLayoutPanel BuildWeek()
        {
            var turkish = CultureInfo.GetCultureInfo("tr-TR");
            var intro = FormLayout.Hint("Çalıştığınız günleri işaretleyin, başlangıç ve bitiş saatlerini seçin.");
            intro.Name = "weekIntro";
            intro.Margin = new Padding(0, 0, 0, Spacing.S);
            var rows = new List<Control> { intro };
            foreach (var day in Week)
            {
                var works = new ThemedCheckBox
                {
                    Name = "checkBox" + day,
                    Text = turkish.DateTimeFormat.GetDayName(day),
                    Margin = new Padding(0, 0, Spacing.M, 0),
                };
                var start = TimePicker("comboBoxBaslangic" + day, 0, 47);
                var end = TimePicker("comboBoxBitis" + day, 1, 48);
                start.AccessibleName = works.Text + " başlangıç saati";
                end.AccessibleName = works.Text + " bitiş saati";
                works.CheckedChanged += (_, _) => start.Enabled = end.Enabled = works.Checked;
                start.Enabled = end.Enabled = false;
                _rows[day] = (works, start, end);
                var dash = new Label { Text = "–", AutoSize = true, ForeColor = Theme.Current.TextMuted, Margin = new Padding(Spacing.XS, 0, Spacing.XS, 0) };

                // One bar per day: the hours keep the same width, so they line up from day to day.
                var row = FormLayout.Bar("weekRow" + day, (works, true), (start, false), (dash, false), (end, false));
                works.Anchor = AnchorStyles.Left;
                row.Margin = Padding.Empty;
                rows.Add(row);
            }

            labelVarsayilan.Font = Theme.Fonts.Body;
            labelVarsayilan.ForeColor = Theme.Current.AccentText;
            labelVarsayilan.Margin = new Padding(0, Spacing.S, 0, 0);
            rows.Add(labelVarsayilan);
            buttonKaydet.Margin = new Padding(0, Spacing.M, 0, 0);
            rows.Add(buttonKaydet);
            var stack = FormLayout.Stack("weekTable", [.. rows]);
            buttonKaydet.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            return stack;
        }

        private TableLayoutPanel BuildLeave()
        {
            listBoxIzinler.Describe = item => item is LeaveDayItem leave
                ? new ListRow(leave.Day.ToString("d MMMM yyyy", DisplayFormat.Turkish), leave.Day.ToString("dddd", DisplayFormat.Turkish))
                : new ListRow(item.ToString() ?? string.Empty);
            listBoxIzinler.EmptyText = "Kayıtlı izin gününüz yok.";
            dateTimePickerIzin.AccessibleName = "İzin günü";
            dateTimePickerIzin.Margin = new Padding(0, 5, Spacing.S, 5);
            buttonIzinEkle.Margin = Padding.Empty;
            var add = FormLayout.Bar("leaveAddBar", (dateTimePickerIzin, true), (buttonIzinEkle, false));
            add.Margin = new Padding(0, 0, 0, Spacing.M);
            buttonIzinKaldir.Margin = new Padding(0, Spacing.M, 0, 0);
            var remove = FormLayout.Bar("leaveRemoveBar", (buttonIzinKaldir, false), (FormLayout.Spacer(), true));
            return FormLayout.Page("leavePage", (add, false), (listBoxIzinler, true), (remove, false));
        }

        /// <summary>The patient and time on the first line, the reason on the second.</summary>
        private static ListRow ConflictRow(ScheduleConflict conflict) => new(
            $"{conflict.Appointment.PatientName} · {conflict.Appointment.StartsAt.ToString("d MMMM yyyy dddd, HH:mm", DisplayFormat.Turkish)}",
            conflict.Reason == ScheduleConflictReason.LeaveDay ? "İzin gününe denk geliyor" : "Çalışma saatlerinin dışında kalıyor",
            RowTone.Warning);

        private static ThemedComboBox TimePicker(string name, int first, int last)
        {
            var combo = FormLayout.Picker(name, 96);
            combo.Margin = new Padding(0, 1, 0, 1);
            for (var i = first; i <= last; i++)
            {
                combo.Items.Add(new TimeItem(TimeSpan.FromMinutes(30 * i)));
            }

            return combo;
        }
    }
}
