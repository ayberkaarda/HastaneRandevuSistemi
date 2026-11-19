using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// Sample data applied by the InitialCreate migration through HasData, only to a new database. The
/// people are fictional and the national ids are synthetic numbers that merely pass the checksum.
/// Values are fixed so the migration is deterministic, which is why the password hashes are
/// precomputed (doctors: "Demo.Doktor1", patients: "Demo.Hasta1").
/// </summary>
internal static class SeedData
{
    public static readonly Doctor[] Doctors =
    [
        new()
        {
            Id = 1,
            FullName = "Dr. Ada Örnek",
            Branch = "Kardiyoloji",
            NationalId = "10000000146",
            PasswordHash = "PBKDF2-SHA256$600000$POBLGXwub/9Fp+d1fn82Hw==$pFvbcSUAJcXvZGOE/vQl28HWttZWt3J9fs/HvKZKhYU=",
        },
        new()
        {
            Id = 2,
            FullName = "Dr. Can Deneme",
            Branch = "Dahiliye",
            NationalId = "10000000214",
            PasswordHash = "PBKDF2-SHA256$600000$qe32m/2niYriZlrLXX/Txw==$tqXg28OLoLB4cDDVhFKks7o1YeJwdfA3vV9bvJeLoN0=",
        },
        new()
        {
            Id = 3,
            FullName = "Dr. Ece Taslak",
            Branch = "Ortopedi",
            NationalId = "10000000382",
            PasswordHash = "PBKDF2-SHA256$600000$Vd7YUlXgvduthbLitDC7ow==$Qzi+CYoFWFqRuu/gjbS+yZK99xJi3DRbOHHj1AcuzAY=",
        },
    ];

    public static readonly Patient[] Patients =
    [
        new()
        {
            Id = 1,
            FullName = "Deniz Örnek",
            NationalId = "10000000450",
            PasswordHash = "PBKDF2-SHA256$600000$E8lC16zcX8bS+fT0Jrf0cw==$TMREiFHa9ZGNrjxcCavxj3DIHdezh58JIYtkwR8Suws=",
        },
        new()
        {
            Id = 2,
            FullName = "Mert Deneme",
            NationalId = "10000000528",
            PasswordHash = "PBKDF2-SHA256$600000$KfV1QUPGviI9YNrUbnuCSQ==$HwqKONeM/WdlrtBIqDdYiM3AAtFxv2jFG/NqVnmgDIE=",
        },
    ];
}
