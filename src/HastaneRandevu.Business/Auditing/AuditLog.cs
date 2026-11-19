using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Auditing;

/// <summary>
/// Security relevant events. An event carries only its kind and the role of the account involved: no
/// account id, national id, name, password, appointment time or doctor, so an audit entry cannot
/// identify a person or reveal a health related detail (KVKK). The entries can only be counted.
/// </summary>
public enum AuditEvent
{
    /// <summary>A sign-in succeeded.</summary>
    SignInSucceeded = 1,

    /// <summary>A sign-in failed: unknown account or wrong password.</summary>
    SignInFailed = 2,

    /// <summary>A sign-in was refused because the account is locked.</summary>
    SignInLocked = 3,

    /// <summary>A plain text or weaker stored password was replaced with a current hash.</summary>
    PasswordUpgraded = 4,

    /// <summary>A new patient account was created.</summary>
    PatientRegistered = 5,

    /// <summary>An appointment was booked.</summary>
    AppointmentBooked = 6,

    /// <summary>A booking lost the race for a slot.</summary>
    AppointmentSlotTaken = 7,

    /// <summary>An appointment was cancelled.</summary>
    AppointmentCancelled = 8,

    /// <summary>An account asked for data or an operation it is not allowed to use.</summary>
    AccessDenied = 9,
}

/// <summary>Receives <see cref="AuditEvent"/>s; the application writes them to its log file.</summary>
public interface IAuditLog
{
    void Record(AuditEvent auditEvent, AccountRole role);
}

public static class AuditCodes
{
    /// <summary>Stable, lower case code of <paramref name="auditEvent"/> as it is written to the log.</summary>
    public static string Of(AuditEvent auditEvent) => auditEvent switch
    {
        AuditEvent.SignInSucceeded => "auth.sign_in.succeeded",
        AuditEvent.SignInFailed => "auth.sign_in.failed",
        AuditEvent.SignInLocked => "auth.account.locked",
        AuditEvent.PasswordUpgraded => "auth.password.upgraded",
        AuditEvent.PatientRegistered => "auth.patient.registered",
        AuditEvent.AppointmentBooked => "appointment.booked",
        AuditEvent.AppointmentSlotTaken => "appointment.slot_taken",
        AuditEvent.AppointmentCancelled => "appointment.cancelled",
        AuditEvent.AccessDenied => "access.denied",
        _ => throw new ArgumentOutOfRangeException(nameof(auditEvent), auditEvent, "Unknown audit event."),
    };

    /// <summary>Lower case role name as it is written to the log.</summary>
    public static string Of(AccountRole role) => role switch
    {
        AccountRole.Patient => "patient",
        AccountRole.Doctor => "doctor",
        _ => "unknown",
    };
}

/// <summary>Discards every event; used when no audit sink is registered, for example in tests.</summary>
internal sealed class NullAuditLog : IAuditLog
{
    public static readonly NullAuditLog Instance = new();

    public void Record(AuditEvent auditEvent, AccountRole role)
    {
    }
}
