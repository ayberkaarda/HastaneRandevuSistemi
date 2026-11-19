namespace HastaneRandevu.Domain.Entities;

/// <summary>
/// The hours a doctor works on one day of the week (table: DoktorCalismaSaatleri). Times are minutes
/// after local midnight on 30 minute boundaries. A doctor without any row works the default hours
/// (weekdays 09:00-17:00), so doctors of existing databases keep the original rule.
/// </summary>
public sealed class DoctorWorkingHours
{
    public int Id { get; set; }

    public int DoctorId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Start of the first slot, in minutes after midnight.</summary>
    public int StartMinute { get; set; }

    /// <summary>End of the last slot, in minutes after midnight (at most 1440).</summary>
    public int EndMinute { get; set; }
}

/// <summary>A day on which a doctor does not work, such as leave or a holiday (table: DoktorIzinGunleri).</summary>
public sealed class DoctorLeaveDay
{
    public int Id { get; set; }

    public int DoctorId { get; set; }

    public DateOnly Date { get; set; }
}
