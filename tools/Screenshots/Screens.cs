using System.Globalization;
using HastaneRandevu.UI;
using HastaneRandevu.UI.Infrastructure;

namespace HastaneRandevu.Screenshots;

/// <summary>The screens shown in the README, each prepared with synthetic data as a patient or doctor would see it.</summary>
internal static class Screens
{
    /// <summary>File name (kebab-case, without extension) and how to build the screen.</summary>
    public static IReadOnlyList<(string Name, Func<SyntheticHospital, Form> Build)> All { get; } =
    [
        ("hasta-girisi", SignIn),
        ("hasta-kaydi", Registration),
        ("hasta-paneli", PatientPanel),
        ("randevu-takvimi", MonthCalendar),
        ("randevu-takvimi-hafta", WeekCalendar),
        ("randevu-erteleme", Reschedule),
        ("doktor-girisi", DoctorSignIn),
        ("doktor-paneli", DoctorPanel),
        ("calisma-programi", Schedule),
        ("onay-penceresi", Confirmation),
    ];

    private static LoginForm SignIn(SyntheticHospital hospital) => new(hospital, new UnusedFormFactory());

    private static UyeOlForm Registration(SyntheticHospital hospital) => new(hospital);

    private static DoktorGirisForm DoctorSignIn(SyntheticHospital hospital) => new(hospital, new UnusedFormFactory());

    /// <summary>
    /// The patient panel after sign-in: the reminder of tomorrow's appointment, a cardiologist chosen and
    /// the first free slot found, and the next appointment selected in the list.
    /// </summary>
    private static MainForm PatientPanel(SyntheticHospital hospital)
    {
        var form = new MainForm(hospital, hospital, hospital, hospital, new NullReminderNotifier(), new UnusedFormFactory(), hospital.Patient);
        Run(form.LoadAsync());
        Find<ComboBox>(form, "comboBoxDoktorlar").SelectedIndex = 0;
        Run(form.FindFirstFreeAsync());
        Find<ListBox>(form, "listBoxRandevuGecmisi").SelectedIndex = 0;
        return form;
    }

    private static TakvimForm MonthCalendar(SyntheticHospital hospital)
    {
        var form = new TakvimForm(hospital, hospital.Patient, hospital.Doctors[0]);
        Run(form.ShowPeriodAsync());
        return form;
    }

    private static TakvimForm WeekCalendar(SyntheticHospital hospital)
    {
        var form = new TakvimForm(hospital, hospital.Patient, hospital.Doctors[0]);
        Run(form.SetMonthViewAsync(month: false));
        Run(form.MoveAsync(1));
        return form;
    }

    /// <summary>Moving tomorrow's appointment: the free slots of a later day are loaded and one is chosen.</summary>
    private static ErteleForm Reschedule(SyntheticHospital hospital)
    {
        var appointment = hospital.GetMyAppointmentsAsync(hospital.Patient).Result[0];
        var form = new ErteleForm(hospital, hospital.Patient, appointment);
        var picker = Find<DateTimePicker>(form, "dateTimePickerYeni");
        picker.Value = NextWeekday(appointment.StartsAt.Date.AddDays(2));
        Run(form.LoadSlotsAsync());
        Find<ComboBox>(form, "comboBoxYeniSaat").SelectedIndex = 2;
        return form;
    }

    /// <summary>The doctor panel after the agenda of the next working day was saved: the result is shown inline.</summary>
    private static DoktorForm DoctorPanel(SyntheticHospital hospital)
    {
        var form = new DoktorForm(hospital, hospital, new UnusedFormFactory(), hospital.Doctor);
        Run(form.LoadAsync());
        var first = hospital.GetMyAppointmentsAsync(hospital.Doctor).Result[0];
        Find<DateTimePicker>(form, "dateTimePickerAjanda").Value = first.StartsAt.Date;
        Run(form.ExportAgendaAsync());
        Find<ListBox>(form, "listBoxRandevular").SelectedIndex = 1;
        return form;
    }

    private static CalismaProgramiForm Schedule(SyntheticHospital hospital)
    {
        var form = new CalismaProgramiForm(hospital, hospital.Doctor);
        Run(form.LoadAsync());
        return form;
    }

    /// <summary>The question asked before an appointment is cancelled.</summary>
    private static MessageDialog Confirmation(SyntheticHospital hospital) => new(
        "Seçilen randevuyu iptal etmek istediğinize emin misiniz?",
        "Onay",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question);

    private static DateTime NextWeekday(DateTime day)
    {
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            day = day.AddDays(1);
        }

        return day;
    }

    /// <summary>The in-memory services complete synchronously; anything else is a bug in the tool.</summary>
    internal static void Run(Task task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException("A screen did not finish loading synchronously.");
        }

        task.GetAwaiter().GetResult();
    }

    internal static T Find<T>(Control root, string name)
        where T : Control =>
        root.Controls.Find(name, searchAllChildren: true).OfType<T>().SingleOrDefault()
        ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Control '{0}' was not found.", name));

    /// <summary>No other window is opened while a screen is drawn.</summary>
    private sealed class UnusedFormFactory : IFormFactory
    {
        public T Create<T>(params object[] arguments)
            where T : Form => throw new InvalidOperationException("Screens are not opened while capturing.");
    }
}
