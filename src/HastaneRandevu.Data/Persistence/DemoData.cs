using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// Demo accounts with published passwords (doctors: "Demo.Doktor1", patients: "Demo.Hasta1"). They are
/// created only when demo mode is switched on explicitly (<see cref="DemoDataOptions"/>); the default
/// installation and the migrations create no account. The people are fictional and the national ids
/// are synthetic numbers that merely pass the checksum. The hashes are precomputed so the data is fixed.
/// </summary>
internal static class DemoData
{
    public static readonly Doctor[] Doctors =
    [
        new()
        {
            FullName = "Dr. Ada Örnek",
            Branch = "Kardiyoloji",
            NationalId = "10000000146",
            PasswordHash = "PBKDF2-SHA256$600000$POBLGXwub/9Fp+d1fn82Hw==$pFvbcSUAJcXvZGOE/vQl28HWttZWt3J9fs/HvKZKhYU=",
        },
        new()
        {
            FullName = "Dr. Can Deneme",
            Branch = "Dahiliye",
            NationalId = "10000000214",
            PasswordHash = "PBKDF2-SHA256$600000$qe32m/2niYriZlrLXX/Txw==$tqXg28OLoLB4cDDVhFKks7o1YeJwdfA3vV9bvJeLoN0=",
        },
        new()
        {
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
            FullName = "Deniz Örnek",
            NationalId = "10000000450",
            PasswordHash = "PBKDF2-SHA256$600000$E8lC16zcX8bS+fT0Jrf0cw==$TMREiFHa9ZGNrjxcCavxj3DIHdezh58JIYtkwR8Suws=",
        },
        new()
        {
            FullName = "Mert Deneme",
            NationalId = "10000000528",
            PasswordHash = "PBKDF2-SHA256$600000$KfV1QUPGviI9YNrUbnuCSQ==$HwqKONeM/WdlrtBIqDdYiM3AAtFxv2jFG/NqVnmgDIE=",
        },
    ];
}
