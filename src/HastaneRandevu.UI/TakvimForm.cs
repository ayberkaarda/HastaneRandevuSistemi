using System.Globalization;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// A doctor's calendar by month or week: every day shows whether it is free (with the number of free
    /// slots), full, a leave day, a day off or outside the booking window. Choosing a free day closes the
    /// dialog with <see cref="SelectedDay"/>. The days come from the service; navigation is latest-wins.
    /// </summary>
    public sealed class TakvimForm : Form
    {
        private const int Columns = 7;
        private const int MonthRows = 6;
        private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

        private readonly IAvailabilityService _availability;
        private readonly UserSession _session;
        private readonly DoctorSummary _doctor;
        private readonly RequestSequence _loads = new();
        private readonly RadioButton radioButtonAy = new() { Name = "radioButtonAy", Text = "Ay", AutoSize = true, Checked = true };
        private readonly RadioButton radioButtonHafta = new() { Name = "radioButtonHafta", Text = "Hafta", AutoSize = true };
        private readonly Button buttonOnceki = new() { Name = "buttonOnceki", Text = "< Önceki", AutoSize = true };
        private readonly Button buttonSonraki = new() { Name = "buttonSonraki", Text = "Sonraki >", AutoSize = true };
        private readonly Label labelDonem = new() { Name = "labelDonem", AutoSize = true, Anchor = AnchorStyles.Left };
        private readonly TableLayoutPanel tableGunler = new() { Name = "tableGunler", Dock = DockStyle.Fill, ColumnCount = Columns, RowCount = MonthRows + 1 };
        private readonly Button[] _dayButtons = new Button[Columns * MonthRows];
        private bool _binding;

        public TakvimForm(IAvailabilityService availability, UserSession session, DoctorSummary doctor)
        {
            ArgumentNullException.ThrowIfNull(availability);
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(doctor);

            _availability = availability;
            _session = session;
            _doctor = doctor;
            PeriodDay = DateOnly.FromDateTime(DateTime.Today);

            BuildLayout();
            Icon = AppAssets.Icon;
            Text = "Hastane Randevu - Takvim: " + doctor.DisplayName;

            Load += async (_, _) => await ShowPeriodAsync();
            radioButtonAy.CheckedChanged += async (_, _) =>
            {
                if (!_binding && radioButtonAy.Checked)
                {
                    await ShowPeriodAsync();
                }
            };
            radioButtonHafta.CheckedChanged += async (_, _) =>
            {
                if (!_binding && radioButtonHafta.Checked)
                {
                    await ShowPeriodAsync();
                }
            };
            buttonOnceki.Click += async (_, _) => await MoveAsync(-1);
            buttonSonraki.Click += async (_, _) => await MoveAsync(1);
        }

        /// <summary>The free day the patient chose, when the dialog was closed with OK.</summary>
        public DateOnly? SelectedDay { get; private set; }

        /// <summary>A day inside the period on screen.</summary>
        internal DateOnly PeriodDay { get; private set; }

        internal bool MonthView => radioButtonAy.Checked;

        /// <summary>The first day on screen: the Monday on or before the month's first day, or of the week.</summary>
        internal DateOnly FirstDay
        {
            get
            {
                var start = MonthView ? new DateOnly(PeriodDay.Year, PeriodDay.Month, 1) : PeriodDay;
                return start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
            }
        }

        internal int DayCount => MonthView ? Columns * MonthRows : Columns;

        /// <summary>Loads and shows the days of the current period; only the latest load is shown.</summary>
        internal Task ShowPeriodAsync() =>
            this.RunAsync(async () =>
            {
                var request = _loads.Next();
                var first = FirstDay;
                var count = DayCount;
                labelDonem.Text = MonthView
                    ? PeriodDay.ToString("MMMM yyyy", Turkish)
                    : $"{first.ToString("dd.MM.yyyy", Turkish)} - {first.AddDays(6).ToString("dd.MM.yyyy", Turkish)}";
                foreach (var button in _dayButtons)
                {
                    button.Enabled = false;
                }

                var days = await _availability.GetCalendarAsync(_session, _doctor.Id, first, count);
                if (_loads.IsLatest(request) && !IsDisposed)
                {
                    ShowDays(days, count);
                }
            });

        /// <summary>Switches between the month and the week view without starting a second load.</summary>
        internal Task SetMonthViewAsync(bool month)
        {
            _binding = true;
            try
            {
                radioButtonAy.Checked = month;
                radioButtonHafta.Checked = !month;
            }
            finally
            {
                _binding = false;
            }

            return ShowPeriodAsync();
        }

        internal Task MoveAsync(int step)
        {
            PeriodDay = MonthView ? PeriodDay.AddMonths(step) : PeriodDay.AddDays(7 * step);
            return ShowPeriodAsync();
        }

        /// <summary>The text of a day, for example "05\nBoş: 12/16".</summary>
        internal static string Describe(CalendarDay day) =>
            day.Date.Day.ToString("00", Turkish) + Environment.NewLine + day.Status switch
            {
                CalendarDayStatus.Available => $"Boş: {day.FreeSlots}/{day.TotalSlots}",
                CalendarDayStatus.Full => "Dolu",
                CalendarDayStatus.Leave => "İzinli",
                CalendarDayStatus.NotWorking => "Çalışmıyor",
                _ => "-",
            };

        private void ShowDays(IReadOnlyList<CalendarDay> days, int count)
        {
            for (var i = 0; i < _dayButtons.Length; i++)
            {
                var button = _dayButtons[i];
                button.Visible = i < count;
                if (i < count && i < days.Count)
                {
                    var day = days[i];
                    button.Text = Describe(day);
                    button.Tag = day;
                    button.Enabled = day.Status == CalendarDayStatus.Available;
                    button.AccessibleName = $"{day.Date.ToString("d MMMM dddd", Turkish)}, {Describe(day).Replace(Environment.NewLine, " ", StringComparison.Ordinal)}";
                }
            }
        }

        /// <summary>Chooses the day shown at <paramref name="index"/>, as a click on it does.</summary>
        internal void ChooseDayAt(int index) => Choose(_dayButtons[index]);

        private void Choose(Button button)
        {
            if (button.Tag is CalendarDay { Status: CalendarDayStatus.Available } day)
            {
                SelectedDay = day.Date;
                DialogResult = DialogResult.OK;
            }
        }

        private void BuildLayout()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(760, 520);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.AddRange([radioButtonAy, radioButtonHafta, buttonOnceki, buttonSonraki, labelDonem]);
            root.Controls.Add(bar, 0, 0);

            for (var c = 0; c < Columns; c++)
            {
                tableGunler.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / Columns));
            }

            tableGunler.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var r = 0; r < MonthRows; r++)
            {
                tableGunler.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / MonthRows));
            }

            var monday = new DateOnly(2024, 1, 1); // a Monday, only for the weekday names
            for (var c = 0; c < Columns; c++)
            {
                tableGunler.Controls.Add(
                    new Label { Text = monday.AddDays(c).ToString("ddd", Turkish), AutoSize = true, Anchor = AnchorStyles.None },
                    c,
                    0);
            }

            for (var i = 0; i < _dayButtons.Length; i++)
            {
                var button = new Button { Name = "buttonGun" + i.ToString(CultureInfo.InvariantCulture), Dock = DockStyle.Fill, Enabled = false, UseVisualStyleBackColor = true };
                button.Click += (_, _) => Choose(button);
                _dayButtons[i] = button;
                tableGunler.Controls.Add(button, i % Columns, (i / Columns) + 1);
            }

            root.Controls.Add(tableGunler, 0, 1);
            Controls.Add(root);
            ResumeLayout(true);
        }
    }
}
