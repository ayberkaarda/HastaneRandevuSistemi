namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// Column sizes and value rules enforced by the database. The business layer applies the same or
/// stricter rules; the legacy upgrade checks existing rows against these values.
/// </summary>
internal static class SchemaLimits
{
    public const int PatientNameLength = 80;
    public const int DoctorNameLength = 100;
    public const int BranchLength = 100;
    public const int NationalIdLength = 11;

    /// <summary>Room for a PBKDF2 hash; the original column held plain text in 100 characters.</summary>
    public const int PasswordLength = 256;

    public const string FailedLoginCheck = "[BasarisizGiris] >= 0";
    public const string StatusCheck = "[Durum] IN (0, 1)";

    /// <summary>Only active appointments occupy a doctor's slot; cancelled ones are kept as history.</summary>
    public const string ActiveAppointmentFilter = "[Durum] = 0";
}
