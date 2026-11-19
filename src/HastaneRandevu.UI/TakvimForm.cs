using System.Globalization;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;

namespace HastaneRandevu.UI
{
    /// <summary>
    /// A doctor's calendar by month or week: every day shows whether it is free (with the number of free
    /// slots), full, a leave day, a day off or outside the booking window. Choosing a free day closes the
    /// dialog with <see cref="SelectedDay"/>. The days come from the service; navigation is latest-wins.
    /// </summary>
    public sealed class TakvimForm : AppDialog
    {
        private const int Columns = 7;
        private const int MonthRows = 6;
        private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

        private readonly IAvailabilityService _availability;
        private readonly UserSession _session;
        private readonly DoctorSummary _doctor;
        private readonly RequestSequence _loads = new();
        private readonly SegmentButton radioButtonAy = new() { Name = "radioButtonAy", Text = "Ay", Checked = true };
        private readonly SegmentButton radioButtonHafta = new() { Name = "radioButtonHafta", Text = "Hafta" };
        private readonly ThemedButton buttonOnceki = FormLayout.Button("buttonOnceki", "‹  Önceki", ButtonKind.Subtle);
        private readonly ThemedButton buttonSonraki = FormLayout.Button("buttonSonraki", "Sonraki  ›", ButtonKind.Subtle);
        private readonly Label labelDonem = new() { Name = "labelDonem", AutoSize = true, Anchor = AnchorStyles.Left, UseMnemonic = false };
        private readonly TableLayoutPanel tableGunler = new() { Name = "tableGunler", Dock = DockStyle.Fill, ColumnCount = Columns, RowCount = MonthRows + 1, Margin = Padding.Empty };
        private readonly DayButton[] _dayButtons = new DayButton[Columns * MonthRows];
        private bool _binding;

        public TakvimForm(IAvailabilityService availability, UserSession session, DoctorSummary doctor)
            : base(
                "Takvim",
                (doctor ?? throw new ArgumentNullException(nameof(doctor))).DisplayName + " · boş bir güne tıklayın; gün, randevu tarihine yazılır.",
                Glyphs.Calendar,
                new Size(880, 660),
                new Size(720, 560),
                modal: true)
        {
            ArgumentNullException.ThrowIfNull(availability);
            ArgumentNullException.ThrowIfNull(session);

            _availability = availability;
            _session = session;
            _doctor = doctor;
            PeriodDay = DateOnly.FromDateTime(DateTime.Today);

            BuildLayout();
            Text = AppText.Title + " - Takvim: " + doctor.DisplayName;

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
            // The week view uses the first row of days only; it takes the whole height.
            tableGunler.SuspendLayout();
            for (var r = 0; r < MonthRows; r++)
            {
                tableGunler.RowStyles[r + 1] = count > Columns || r == 0
                    ? new RowStyle(SizeType.Percent, 100f / (count / Columns))
                    : new RowStyle(SizeType.Absolute, 0);
            }

            for (var i = 0; i < _dayButtons.Length; i++)
            {
                var button = _dayButtons[i];
                button.Visible = i < count;

                // Days outside the week take no room, so the rows of the week view can shrink to nothing.
                button.MinimumSize = i < count ? new Size(0, Sizes.CalendarDayHeight) : Size.Empty;
                if (i >= count)
                {
                    button.Height = 0; // A hidden control is not laid out and would keep its old height.
                }
                if (i < count && i < days.Count)
                {
                    var day = days[i];
                    button.Text = Describe(day);
                    button.Tag = day;
                    button.InPeriod = !MonthView || (day.Date.Month == PeriodDay.Month && day.Date.Year == PeriodDay.Year);
                    button.Enabled = day.Status == CalendarDayStatus.Available;
                    button.AccessibleName = $"{day.Date.ToString("d MMMM dddd", Turkish)}, {Describe(day).Replace(Environment.NewLine, " ", StringComparison.Ordinal)}";
                    button.Invalidate();
                }
            }

            SetGridMinimum(count / Columns);
            tableGunler.ResumeLayout(true);
        }

        /// <summary>
        /// The grid never shrinks below the height its rows of days need; a smaller window scrolls instead of
        /// letting the days overlap (rows that share the height by percentage would shrink below their tiles).
        /// </summary>
        private void SetGridMinimum(int weeks) =>
            tableGunler.MinimumSize = new Size(0, Theme.Fonts.Caption.Height + Spacing.XS + (weeks * (Sizes.CalendarDayHeight + 6)));

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
            Name = nameof(TakvimForm);

            // Toolbar: view, navigation and the period on screen.
            labelDonem.Font = Theme.Fonts.Subtitle;
            labelDonem.ForeColor = Theme.Current.Text;
            labelDonem.Margin = new Padding(Spacing.S, 0, Spacing.S, 0);
            labelDonem.MinimumSize = new Size(190, 0);
            labelDonem.TextAlign = ContentAlignment.MiddleCenter;
            radioButtonAy.AccessibleName = "Ay görünümü";
            radioButtonHafta.AccessibleName = "Hafta görünümü";
            radioButtonHafta.Margin = new Padding(0, 0, Spacing.L, 0);
            buttonOnceki.Margin = buttonSonraki.Margin = Padding.Empty;
            var toolbar = FormLayout.Bar(
                "calendarToolbar",
                (radioButtonAy, false),
                (radioButtonHafta, false),
                (buttonOnceki, false),
                (labelDonem, false),
                (buttonSonraki, false),
                (FormLayout.Spacer(), true));
            toolbar.Margin = new Padding(0, 0, 0, Spacing.M);

            // Weekday names and the day tiles.
            tableGunler.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var c = 0; c < Columns; c++)
            {
                tableGunler.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / Columns));
            }

            for (var r = 0; r < MonthRows; r++)
            {
                tableGunler.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / MonthRows));
            }

            var monday = new DateOnly(2024, 1, 1); // a Monday, only for the weekday names
            for (var c = 0; c < Columns; c++)
            {
                tableGunler.Controls.Add(
                    new Label
                    {
                        Name = "labelGun" + c.ToString(CultureInfo.InvariantCulture),
                        Text = monday.AddDays(c).ToString("dddd", Turkish),
                        AutoSize = true,
                        Font = Theme.Fonts.Caption,
                        ForeColor = Theme.Current.TextMuted,
                        Anchor = AnchorStyles.Left,
                        Margin = new Padding(Spacing.XS + 2, 0, 0, Spacing.XS),
                    },
                    c,
                    0);
            }

            for (var i = 0; i < _dayButtons.Length; i++)
            {
                var button = new DayButton { Name = "buttonGun" + i.ToString(CultureInfo.InvariantCulture), Dock = DockStyle.Fill, Enabled = false };
                button.Click += (_, _) => Choose(button);
                _dayButtons[i] = button;
                tableGunler.Controls.Add(button, i % Columns, (i / Columns) + 1);
            }

            SetGridMinimum(MonthRows);
            var calendarCard = FormLayout.Card("calendarCard", null, tableGunler);
            calendarCard.Padding = new Padding(Spacing.M);
            calendarCard.Margin = new Padding(0, 0, 0, Spacing.M);

            Content.Controls.Add(FormLayout.Page("page", (toolbar, false), (calendarCard, true), (Legend(), false)));
            SetFooterNote("Yalnızca boş günler seçilebilir; seçtiğiniz gün randevu tarihine yazılır.");
            var close = CreateCloseButton("buttonKapat");
            close.DialogResult = DialogResult.Cancel;
            SetFooterButtons(close);
            FinishLayout();
        }

        /// <summary>What the colors of the days mean.</summary>
        private static FlowLayoutPanel Legend()
        {
            var legend = new FlowLayoutPanel
            {
                Name = "legend",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, Spacing.S),
            };
            foreach (var (status, text) in new[]
            {
                (CalendarDayStatus.Available, "Boş saati olan gün"),
                (CalendarDayStatus.Full, "Dolu"),
                (CalendarDayStatus.Leave, "İzinli"),
                (CalendarDayStatus.NotWorking, "Çalışılmayan gün"),
            })
            {
                var swatch = new Swatch(status) { Anchor = AnchorStyles.Left, Margin = new Padding(0, Spacing.XS, Spacing.XS + 2, 0) };
                var label = FormLayout.Hint(text);
                label.Margin = new Padding(0, 0, Spacing.L, 0);
                label.Anchor = AnchorStyles.Left;
                legend.Controls.Add(swatch);
                legend.Controls.Add(label);
            }

            return legend;
        }
    }
}
