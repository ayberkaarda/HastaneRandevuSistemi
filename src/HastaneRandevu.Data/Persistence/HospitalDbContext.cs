using HastaneRandevu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// EF Core context for the appointment database. Table and column names follow the original schema
/// (Hastalar, Doktorlar, Randevular).
/// </summary>
public sealed class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);
    }
}
