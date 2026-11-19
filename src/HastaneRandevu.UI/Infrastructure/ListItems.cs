using System.Globalization;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.UI.Infrastructure;

/// <summary>Formats dates the way the screens show them.</summary>
internal static class DisplayFormat
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string DateTime(DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Turkish);

    public static string Time(DateTime value) => value.ToString("HH:mm", Turkish);
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
