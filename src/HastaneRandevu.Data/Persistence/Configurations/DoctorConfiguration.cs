using HastaneRandevu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HastaneRandevu.Data.Persistence.Configurations;

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doktorlar", table =>
        {
            table.HasCheckConstraint("CK_Doktorlar_BasarisizGiris", SchemaLimits.FailedLoginCheck);
            table.HasCheckConstraint("CK_Doktorlar_SifreBicimi", SchemaLimits.PasswordFormatCheck);
        });

        builder.HasKey(d => d.Id).HasName("PK_Doktorlar");
        builder.Property(d => d.Id).HasColumnName("DoktorID").ValueGeneratedOnAdd();
        builder.Property(d => d.FullName).HasColumnName("Ad_Soyad").HasMaxLength(SchemaLimits.DoctorNameLength).IsRequired();
        builder.Property(d => d.Branch).HasColumnName("Brans").HasMaxLength(SchemaLimits.BranchLength);
        builder.Property(d => d.NationalId).HasColumnName("TC").HasMaxLength(SchemaLimits.NationalIdLength).IsRequired();
        builder.Property(d => d.PasswordHash).HasColumnName("Sifre").HasMaxLength(SchemaLimits.PasswordLength).IsRequired();
        builder.Property(d => d.PasswordFormat).HasColumnName("SifreBicimi");
        builder.Property(d => d.FailedLoginCount).HasColumnName("BasarisizGiris");
        builder.Property(d => d.LockoutEnd).HasColumnName("KilitBitis");

        builder.HasIndex(d => d.NationalId, "IX_Doktorlar_TC").IsUnique();

        // Doctors sign in with their name, so it has to identify one doctor.
        builder.HasIndex(d => d.FullName, "IX_Doktorlar_Ad_Soyad").IsUnique();

        builder.HasData(SeedData.Doctors);
    }
}
