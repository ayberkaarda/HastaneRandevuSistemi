using HastaneRandevu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HastaneRandevu.Data.Persistence.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Hastalar", table =>
        {
            table.HasCheckConstraint("CK_Hastalar_BasarisizGiris", SchemaLimits.FailedLoginCheck);
            table.HasCheckConstraint("CK_Hastalar_SifreBicimi", SchemaLimits.PasswordFormatCheck);
        });

        builder.HasKey(p => p.Id).HasName("PK_Hastalar");
        builder.Property(p => p.Id).HasColumnName("HastaID").ValueGeneratedOnAdd();
        builder.Property(p => p.FullName).HasColumnName("Ad_Soyad").HasMaxLength(SchemaLimits.PatientNameLength).IsRequired();
        builder.Property(p => p.NationalId).HasColumnName("TC").HasMaxLength(SchemaLimits.NationalIdLength).IsRequired();
        builder.Property(p => p.PasswordHash).HasColumnName("Sifre").HasMaxLength(SchemaLimits.PasswordLength).IsRequired();
        builder.Property(p => p.PasswordFormat).HasColumnName("SifreBicimi");
        builder.Property(p => p.FailedLoginCount).HasColumnName("BasarisizGiris");
        builder.Property(p => p.LockoutEnd).HasColumnName("KilitBitis");

        builder.HasIndex(p => p.NationalId, "IX_Hastalar_TC").IsUnique();
    }
}
