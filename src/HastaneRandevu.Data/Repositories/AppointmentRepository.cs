using System.Data.Common;
using HastaneRandevu.Data.Persistence;
using HastaneRandevu.Domain.Entities;
using HastaneRandevu.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Repositories;

internal sealed class AppointmentRepository(IDbContextFactory<HospitalDbContext> contextFactory) : IAppointmentRepository
{
    public async Task<IReadOnlyList<AppointmentDetails>> GetActiveForPatientAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await Details(db, db.Appointments.Where(a => a.PatientId == patientId && a.Status == AppointmentStatus.Active), latestFirst: true)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AppointmentDetails>> GetActiveForDoctorAsync(
        int doctorId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await Details(db, db.Appointments.Where(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Active), latestFirst: false)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AppointmentHistoryEntry>> GetHistoryForPatientAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await (
                from a in db.Appointments.AsNoTracking()
                join d in db.Doctors on a.DoctorId equals d.Id
                where a.PatientId == patientId
                orderby a.StartsAt descending, a.Id descending
                select new AppointmentHistoryEntry(a.Id, d.FullName, d.Branch, a.StartsAt, a.Status, a.CancelledAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgendaEntry>> GetAgendaForDoctorAsync(
        int doctorId,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await (
                from a in db.Appointments.AsNoTracking()
                join p in db.Patients on a.PatientId equals p.Id
                where a.DoctorId == doctorId
                    && a.Status == AppointmentStatus.Active
                    && a.StartsAt >= rangeStart
                    && a.StartsAt < rangeEnd
                orderby a.StartsAt, a.Id
                select new AgendaEntry(a.Id, a.StartsAt, p.FullName, p.NationalId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DateTime>> GetBookedStartsAsync(
        int doctorId,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Appointments
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                && a.Status == AppointmentStatus.Active
                && a.StartsAt >= rangeStart
                && a.StartsAt < rangeEnd)
            .Select(a => a.StartsAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Appointments
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Appointment appointment, ScheduleCheck checkSchedule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(checkSchedule);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await DoctorLock.AcquireAsync(db, appointment.DoctorId, cancellationToken).ConfigureAwait(false);
        await CheckScheduleAsync(db, appointment.DoctorId, appointment.StartsAt, checkSchedule, cancellationToken).ConfigureAwait(false);
        if (await IsSlotTakenAsync(db, appointment, cancellationToken).ConfigureAwait(false))
        {
            throw new SlotAlreadyBookedException();
        }

        var entity = new Appointment
        {
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            StartsAt = appointment.StartsAt,
            Status = AppointmentStatus.Active,
        };
        db.Appointments.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Another booking of the same slot committed between the check and the insert and the
            // filtered unique index rejected this one. Checked on a fresh context, outside this
            // transaction, so the provider specific error code does not matter.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
            if (await IsSlotTakenAsync(check, appointment, CancellationToken.None).ConfigureAwait(false))
            {
                throw new SlotAlreadyBookedException("The slot is already booked.", ex);
            }

            throw;
        }

        return entity.Id;
    }

    public async Task<bool> CancelAsync(
        int id,
        AccountRole ownerRole,
        int ownerId,
        DateTime startsAfter,
        DateTime cancelledAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = db.Appointments.Where(a => a.Id == id
            && a.Status == AppointmentStatus.Active
            && a.StartsAt > startsAfter);
        query = ownerRole switch
        {
            AccountRole.Patient => query.Where(a => a.PatientId == ownerId),
            AccountRole.Doctor => query.Where(a => a.DoctorId == ownerId),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerRole)),
        };

        var updated = await query
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.Status, AppointmentStatus.Cancelled)
                    .SetProperty(a => a.CancelledAt, cancelledAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        return updated == 1;
    }

    public async Task<bool> RescheduleAsync(
        int id,
        int patientId,
        DateTime currentStartsAt,
        DateTime newStartsAt,
        DateTime startsAfter,
        DateTime startsNoEarlierThan,
        ScheduleCheck checkSchedule,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkSchedule);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // The doctor of an appointment never changes, so it can be read before the lock is taken.
        var doctorId = await db.Appointments
            .Where(a => a.Id == id)
            .Select(a => (int?)a.DoctorId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (doctorId is not int doctor)
        {
            return false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await DoctorLock.AcquireAsync(db, doctor, cancellationToken).ConfigureAwait(false);
        await CheckScheduleAsync(db, doctor, newStartsAt, checkSchedule, cancellationToken).ConfigureAwait(false);
        if (await IsSlotTakenAsync(db, doctor, newStartsAt, cancellationToken).ConfigureAwait(false))
        {
            throw new SlotAlreadyBookedException();
        }

        try
        {
            // One conditional UPDATE: ownership, status, the time the caller read and the time limits are
            // decided by the database in the same statement that moves the appointment, so a concurrent
            // move of the same appointment makes this one match nothing instead of moving it again, and
            // the filtered unique index rejects the move when another booking or move took the new slot.
            var updated = await db.Appointments
                .Where(a => a.Id == id
                    && a.PatientId == patientId
                    && a.Status == AppointmentStatus.Active
                    && a.StartsAt == currentStartsAt
                    && a.StartsAt > startsAfter
                    && a.StartsAt >= startsNoEarlierThan)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.StartsAt, newStartsAt), cancellationToken)
                .ConfigureAwait(false);
            if (updated != 1)
            {
                return false;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            // Checked on a fresh context so the provider specific error code does not matter.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
            if (await IsSlotTakenAsync(check, doctor, newStartsAt, CancellationToken.None).ConfigureAwait(false))
            {
                throw new SlotAlreadyBookedException("The slot is already booked.", ex);
            }

            throw;
        }
    }

    /// <summary>
    /// Hands the doctor's committed schedule for the day of <paramref name="startsAt"/> to the caller's
    /// check. Runs under the doctor's lock, so no schedule change can commit until this write has ended.
    /// </summary>
    private static async Task CheckScheduleAsync(
        HospitalDbContext db,
        int doctorId,
        DateTime startsAt,
        ScheduleCheck checkSchedule,
        CancellationToken cancellationToken)
    {
        var day = DateOnly.FromDateTime(startsAt);
        var workingDays = await DoctorScheduleRepository.ReadWorkingDaysAsync(db, doctorId, cancellationToken).ConfigureAwait(false);
        var leaveDays = await DoctorScheduleRepository.ReadLeaveDaysAsync(db, doctorId, day, day, cancellationToken).ConfigureAwait(false);
        checkSchedule(workingDays, leaveDays);
    }

    private static Task<bool> IsSlotTakenAsync(HospitalDbContext db, Appointment appointment, CancellationToken cancellationToken) =>
        IsSlotTakenAsync(db, appointment.DoctorId, appointment.StartsAt, cancellationToken);

    private static Task<bool> IsSlotTakenAsync(HospitalDbContext db, int doctorId, DateTime startsAt, CancellationToken cancellationToken) =>
        db.Appointments.AnyAsync(
            a => a.DoctorId == doctorId
                && a.StartsAt == startsAt
                && a.Status == AppointmentStatus.Active,
            cancellationToken);

    private static IQueryable<AppointmentDetails> Details(HospitalDbContext db, IQueryable<Appointment> appointments, bool latestFirst)
    {
        var joined =
            from a in appointments.AsNoTracking()
            join p in db.Patients on a.PatientId equals p.Id
            join d in db.Doctors on a.DoctorId equals d.Id
            select new { Appointment = a, PatientName = p.FullName, DoctorName = d.FullName, d.Branch };

        var ordered = latestFirst
            ? joined.OrderByDescending(x => x.Appointment.StartsAt).ThenByDescending(x => x.Appointment.Id)
            : joined.OrderBy(x => x.Appointment.StartsAt).ThenBy(x => x.Appointment.Id);

        return ordered.Select(x => new AppointmentDetails(
            x.Appointment.Id,
            x.Appointment.PatientId,
            x.PatientName,
            x.Appointment.DoctorId,
            x.DoctorName,
            x.Branch,
            x.Appointment.StartsAt));
    }
}
