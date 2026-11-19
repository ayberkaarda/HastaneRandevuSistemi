using System.Globalization;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// Doctor's own schedule: weekly working hours, leave days and the appointments that no longer fit.
    /// The layout is built in code; every write goes through an <see cref="ExclusiveOperation"/>.
    /// </summary>
    public sealed class CalismaProgramiForm : Form
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
        private readonly Label labelVarsayilan = new() { AutoSize = true, Name = "labelVarsayilan" };
        private readonly Button buttonKaydet = new() { Name = "buttonKaydet", Text = "Programı Kaydet", AutoSize = true };
        private readonly DateTimePicker dateTimePickerIzin = new() { Name = "dateTimePickerIzin", Width = 220 };
        private readonly Button buttonIzinEkle = new() { Name = "buttonIzinEkle", Text = "İzin Ekle", AutoSize = true };
        private readonly ListBox listBoxIzinler = new() { Name = "listBoxIzinler", Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Button buttonIzinKaldir = new() { Name = "buttonIzinKaldir", Text = "Seçili İzni Kaldır", AutoSize = true };
        private readonly ListBox listBoxCakisanlar = new() { Name = "listBoxCakisanlar", Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        private readonly Button buttonKapat = new() { Name = "buttonKapat", Text = "Kapat", AutoSize = true };

        public CalismaProgramiForm(IScheduleService schedules, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(schedules);
            ArgumentNullException.ThrowIfNull(session);

            _schedules = schedules;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            BuildLayout();
            Icon = AppAssets.Icon;
            var today = DateTime.Today;
            dateTimePickerIzin.MinDate = today;
            dateTimePickerIzin.MaxDate = today.AddDays(365);

            Load += async (_, _) => await LoadAsync();
            buttonKaydet.Click += async (_, _) => await SaveAsync();
            buttonIzinEkle.Click += async (_, _) => await AddLeaveAsync();
            buttonIzinKaldir.Click += async (_, _) => await RemoveLeaveAsync();
            buttonKapat.Click += (_, _) => Close();
        }

        /// <summary>Confirmation and information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

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
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9F);
            Text = "Hastane Randevu - Çalışma Programım";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(820, 640);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 340));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BuildWeekGroup(), 0, 0);
            root.Controls.Add(BuildLeaveGroup(), 1, 0);
            var conflicts = new GroupBox { Text = "Programa uymayan randevular", Dock = DockStyle.Fill, Padding = new Padding(8) };
            conflicts.Controls.Add(listBoxCakisanlar);
            root.Controls.Add(conflicts, 0, 1);
            root.SetColumnSpan(conflicts, 2);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            bottom.Controls.Add(buttonKapat);
            root.Controls.Add(bottom, 0, 2);
            root.SetColumnSpan(bottom, 2);

            Controls.Add(root);
            ResumeLayout(true);
        }

        private GroupBox BuildWeekGroup()
        {
            var group = new GroupBox { Text = "Haftalık çalışma saatleri", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            var turkish = CultureInfo.GetCultureInfo("tr-TR");
            foreach (var day in Week)
            {
                var works = new CheckBox { Name = "checkBox" + day, Text = turkish.DateTimeFormat.GetDayName(day), AutoSize = true, Anchor = AnchorStyles.Left };
                var start = TimePicker("comboBoxBaslangic" + day, 0, 47);
                var end = TimePicker("comboBoxBitis" + day, 1, 48);
                works.CheckedChanged += (_, _) => start.Enabled = end.Enabled = works.Checked;
                start.Enabled = end.Enabled = false;
                _rows[day] = (works, start, end);
                table.Controls.Add(works);
                table.Controls.Add(start);
                table.Controls.Add(end);
            }

            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            actions.Controls.Add(labelVarsayilan);
            actions.Controls.Add(buttonKaydet);
            table.Controls.Add(actions);
            table.SetColumnSpan(actions, 3);
            group.Controls.Add(table);
            return group;
        }

        private GroupBox BuildLeaveGroup()
        {
            var group = new GroupBox { Text = "İzin ve tatil günleri", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(dateTimePickerIzin, 0, 0);
            table.Controls.Add(buttonIzinEkle, 1, 0);
            table.Controls.Add(listBoxIzinler, 0, 1);
            table.SetColumnSpan(listBoxIzinler, 2);
            table.Controls.Add(buttonIzinKaldir, 0, 2);
            group.Controls.Add(table);
            return group;
        }

        private static ComboBox TimePicker(string name, int first, int last)
        {
            var combo = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            for (var i = first; i <= last; i++)
            {
                combo.Items.Add(new TimeItem(TimeSpan.FromMinutes(30 * i)));
            }

            return combo;
        }
    }
}
