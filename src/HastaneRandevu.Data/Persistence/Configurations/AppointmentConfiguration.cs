using HastaneRandevu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HastaneRandevu.Data.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Randevular", table =>
            table.HasCheckConstraint("CK_Randevular_Durum", SchemaLimits.StatusCheck));

        builder.HasKey(a => a.Id).HasName("PK_Randevular");
        builder.Property(a => a.Id).HasColumnName("RandevuID").ValueGeneratedOnAdd();
        builder.Property(a => a.PatientId).HasColumnName("HastaID");
        builder.Property(a => a.DoctorId).HasColumnName("DoktorID");

        // The original column type is kept; slots start on whole minutes.
        builder.Property(a => a.StartsAt).HasColumnName("RandevuTarihi").HasColumnType("datetime");
        builder.Property(a => a.Status).HasColumnName("Durum").HasConversion<int>();
        builder.Property(a => a.CancelledAt).HasColumnName("IptalZamani");

        // Appointments are history: deleting a patient or doctor that has appointments is refused.
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .HasConstraintName("FK_Randevular_Hastalar")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>()
            .WithMany()
            .HasForeignKey(a => a.DoctorId)
            .HasConstraintName("FK_Randevular_Doktorlar")
            .OnDelete(DeleteBehavior.Restrict);

        // One active appointment per doctor and start time. This index is what prevents double
        // bookings under concurrency; the service's own check only produces a friendlier message.
        builder.HasIndex(a => new { a.DoctorId, a.StartsAt }, "IX_Randevular_DoktorID_RandevuTarihi")
            .IsUnique()
            .HasFilter(SchemaLimits.ActiveAppointmentFilter);
        builder.HasIndex(a => new { a.PatientId, a.StartsAt }, "IX_Randevular_HastaID_RandevuTarihi");
    }
}
