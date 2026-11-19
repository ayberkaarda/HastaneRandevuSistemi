using HastaneRandevu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HastaneRandevu.Data.Persistence.Configurations;

internal sealed class DoctorWorkingHoursConfiguration : IEntityTypeConfiguration<DoctorWorkingHours>
{
    public void Configure(EntityTypeBuilder<DoctorWorkingHours> builder)
    {
        builder.ToTable("DoktorCalismaSaatleri", table =>
        {
            table.HasCheckConstraint("CK_DoktorCalismaSaatleri_Gun", SchemaLimits.DayOfWeekCheck);
            table.HasCheckConstraint("CK_DoktorCalismaSaatleri_Saat", SchemaLimits.WorkingHoursCheck);
        });

        builder.HasKey(h => h.Id).HasName("PK_DoktorCalismaSaatleri");
        builder.Property(h => h.Id).HasColumnName("CalismaSaatiID").ValueGeneratedOnAdd();
        builder.Property(h => h.DoctorId).HasColumnName("DoktorID");
        builder.Property(h => h.DayOfWeek).HasColumnName("Gun").HasConversion<int>();
        builder.Property(h => h.StartMinute).HasColumnName("BaslangicDakika");
        builder.Property(h => h.EndMinute).HasColumnName("BitisDakika");

        // A doctor is not deleted while schedule rows exist, like appointments.
        builder.HasOne<Doctor>()
            .WithMany()
            .HasForeignKey(h => h.DoctorId)
            .HasConstraintName("FK_DoktorCalismaSaatleri_Doktorlar")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.DoctorId, h.DayOfWeek }, "IX_DoktorCalismaSaatleri_DoktorID_Gun").IsUnique();
    }
}

internal sealed class DoctorLeaveDayConfiguration : IEntityTypeConfiguration<DoctorLeaveDay>
{
    public void Configure(EntityTypeBuilder<DoctorLeaveDay> builder)
    {
        builder.ToTable("DoktorIzinGunleri");

        builder.HasKey(l => l.Id).HasName("PK_DoktorIzinGunleri");
        builder.Property(l => l.Id).HasColumnName("IzinID").ValueGeneratedOnAdd();
        builder.Property(l => l.DoctorId).HasColumnName("DoktorID");
        builder.Property(l => l.Date).HasColumnName("Tarih").HasColumnType("date");

        builder.HasOne<Doctor>()
            .WithMany()
            .HasForeignKey(l => l.DoctorId)
            .HasConstraintName("FK_DoktorIzinGunleri_Doktorlar")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.DoctorId, l.Date }, "IX_DoktorIzinGunleri_DoktorID_Tarih").IsUnique();
    }
}
