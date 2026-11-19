using System.Globalization;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.UI.Infrastructure;

/// <summary>Formats dates the way the screens show them.</summary>
internal static class DisplayFormat
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string DateTime(DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Turkish);

    public static string Time(DateTime value) => value.ToString("HH:mm", Turkish);

    public static string Day(DateOnly value) => value.ToString("dd.MM.yyyy", Turkish);
}

/// <summary>A time of day in the working hours pickers; 24:00 is the end of the day.</summary>
internal sealed record TimeItem(TimeSpan Time)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)Time.TotalHours:00}:{Time.Minutes:00}");
}

/// <summary>A leave day in the doctor's list, for example "03.03.2026 Salı".</summary>
internal sealed record LeaveDayItem(DateOnly Day)
{
    public override string ToString() => Day.ToString("dd.MM.yyyy dddd", DisplayFormat.Turkish);
}

/// <summary>An appointment that no longer fits the doctor's schedule, with the reason.</summary>
internal sealed record ConflictItem(Business.Services.ScheduleConflict Conflict)
{
    public override string ToString() =>
        $"{DisplayFormat.DateTime(Conflict.Appointment.StartsAt)} - {Conflict.Appointment.PatientName} - "
        + (Conflict.Reason == Business.Services.ScheduleConflictReason.LeaveDay ? "İzin günü" : "Çalışma saatleri dışında");
}

/// <summary>A free slot in the time picker.</summary>
internal sealed record SlotItem(DateTime StartsAt)
{
    public override string ToString() => DisplayFormat.Time(StartsAt);
}

/// <summary>An appointment in the patient's list: doctor, branch and time.</summary>
internal sealed record PatientAppointmentItem(AppointmentDetails Appointment)
{
    public override string ToString() =>
        string.IsNullOrWhiteSpace(Appointment.Branch)
            ? $"{Appointment.DoctorName} - {DisplayFormat.DateTime(Appointment.StartsAt)}"
            : $"{Appointment.DoctorName} - {Appointment.Branch} - {DisplayFormat.DateTime(Appointment.StartsAt)}";
}

/// <summary>An appointment in the doctor's list: patient and time.</summary>
internal sealed record DoctorAppointmentItem(AppointmentDetails Appointment)
{
    public override string ToString() => $"{Appointment.PatientName} - {DisplayFormat.DateTime(Appointment.StartsAt)}";
}
