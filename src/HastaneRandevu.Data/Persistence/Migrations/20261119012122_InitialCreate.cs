using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HastaneRandevu.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Doktorlar",
                columns: table => new
                {
                    DoktorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad_Soyad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Brans = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TC = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Sifre = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BasarisizGiris = table.Column<int>(type: "int", nullable: false),
                    KilitBitis = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doktorlar", x => x.DoktorID);
                    table.CheckConstraint("CK_Doktorlar_BasarisizGiris", "[BasarisizGiris] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Hastalar",
                columns: table => new
                {
                    HastaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad_Soyad = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TC = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Sifre = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BasarisizGiris = table.Column<int>(type: "int", nullable: false),
                    KilitBitis = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hastalar", x => x.HastaID);
                    table.CheckConstraint("CK_Hastalar_BasarisizGiris", "[BasarisizGiris] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Randevular",
                columns: table => new
                {
                    RandevuID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HastaID = table.Column<int>(type: "int", nullable: false),
                    DoktorID = table.Column<int>(type: "int", nullable: false),
                    RandevuTarihi = table.Column<DateTime>(type: "datetime", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    IptalZamani = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Randevular", x => x.RandevuID);
                    table.CheckConstraint("CK_Randevular_Durum", "[Durum] IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_Randevular_Doktorlar",
                        column: x => x.DoktorID,
                        principalTable: "Doktorlar",
                        principalColumn: "DoktorID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Randevular_Hastalar",
                        column: x => x.HastaID,
                        principalTable: "Hastalar",
                        principalColumn: "HastaID",
                        onDelete: ReferentialAction.Restrict);
                });



            migrationBuilder.CreateIndex(
                name: "IX_Doktorlar_Ad_Soyad",
                table: "Doktorlar",
                column: "Ad_Soyad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doktorlar_TC",
                table: "Doktorlar",
                column: "TC",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hastalar_TC",
                table: "Hastalar",
                column: "TC",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Randevular_DoktorID_RandevuTarihi",
                table: "Randevular",
                columns: new[] { "DoktorID", "RandevuTarihi" },
                unique: true,
                filter: "[Durum] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Randevular_HastaID_RandevuTarihi",
                table: "Randevular",
                columns: new[] { "HastaID", "RandevuTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Randevular");

            migrationBuilder.DropTable(
                name: "Doktorlar");

            migrationBuilder.DropTable(
                name: "Hastalar");
        }
    }
}
