using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Repositories;

internal sealed class DoctorScheduleRepository(IDbContextFactory<HospitalDbContext> contextFactory) : IDoctorScheduleRepository
{
    public async Task<IReadOnlyList<WorkingDay>> GetWorkingDaysAsync(int doctorId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ReadWorkingDaysAsync(db, doctorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DateOnly>> GetLeaveDaysAsync(
        int doctorId,
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ReadLeaveDaysAsync(db, doctorId, firstDay, lastDay, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceWorkingDaysAsync(int doctorId, IReadOnlyList<WorkingDay> days, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(days);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Without the lock two first saves of a doctor without rows both delete nothing and then both
        // insert, leaving the union of the two schedules.
        await DoctorLock.AcquireAsync(db, doctorId, cancellationToken).ConfigureAwait(false);
        await db.WorkingHours
            .Where(h => h.DoctorId == doctorId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        db.WorkingHours.AddRange(days.Select(d => new DoctorWorkingHours
        {
            DoctorId = doctorId,
            DayOfWeek = d.Day,
            StartMinute = (int)d.Start.TotalMinutes,
            EndMinute = (int)d.End.TotalMinutes,
        }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> AddLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Under the doctor's lock no booking or move can be between its schedule check and its write.
        await DoctorLock.AcquireAsync(db, doctorId, cancellationToken).ConfigureAwait(false);
        if (await HasLeaveAsync(db, doctorId, day, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        db.LeaveDays.Add(new DoctorLeaveDay { DoctorId = doctorId, Date = day });
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique index rejected a concurrent insert of the same day; checked with a new query so
            // the provider specific error code does not matter.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
            if (await HasLeaveAsync(check, doctorId, day, CancellationToken.None).ConfigureAwait(false))
            {
                return false;
            }

            throw;
        }
    }

    public async Task<bool> RemoveLeaveDayAsync(int doctorId, DateOnly day, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await DoctorLock.AcquireAsync(db, doctorId, cancellationToken).ConfigureAwait(false);
        var removed = await db.LeaveDays
            .Where(l => l.DoctorId == doctorId && l.Date == day)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return removed > 0;
    }

    /// <summary>The doctor's weekly hours ordered by day, read on <paramref name="db"/> (and its transaction).</summary>
    internal static async Task<IReadOnlyList<WorkingDay>> ReadWorkingDaysAsync(HospitalDbContext db, int doctorId, CancellationToken cancellationToken)
    {
        var rows = await db.WorkingHours
            .AsNoTracking()
            .Where(h => h.DoctorId == doctorId)
            .OrderBy(h => h.DayOfWeek)
            .Select(h => new { h.DayOfWeek, h.StartMinute, h.EndMinute })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Select(r => new WorkingDay(r.DayOfWeek, TimeSpan.FromMinutes(r.StartMinute), TimeSpan.FromMinutes(r.EndMinute)))
            .ToList();
    }

    /// <summary>The doctor's leave days in the range, ordered, read on <paramref name="db"/> (and its transaction).</summary>
    internal static async Task<IReadOnlyList<DateOnly>> ReadLeaveDaysAsync(
        HospitalDbContext db,
        int doctorId,
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken) =>
        await db.LeaveDays
            .AsNoTracking()
            .Where(l => l.DoctorId == doctorId && l.Date >= firstDay && l.Date <= lastDay)
            .OrderBy(l => l.Date)
            .Select(l => l.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static Task<bool> HasLeaveAsync(HospitalDbContext db, int doctorId, DateOnly day, CancellationToken cancellationToken) =>
        db.LeaveDays.AnyAsync(l => l.DoctorId == doctorId && l.Date == day, cancellationToken);
}
