using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HastaneRandevu.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoktorCalismaSaatleri",
                columns: table => new
                {
                    CalismaSaatiID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoktorID = table.Column<int>(type: "int", nullable: false),
                    Gun = table.Column<int>(type: "int", nullable: false),
                    BaslangicDakika = table.Column<int>(type: "int", nullable: false),
                    BitisDakika = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoktorCalismaSaatleri", x => x.CalismaSaatiID);
                    table.CheckConstraint("CK_DoktorCalismaSaatleri_Gun", "[Gun] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_DoktorCalismaSaatleri_Saat", "[BaslangicDakika] >= 0 AND [BitisDakika] <= 1440 AND [BaslangicDakika] < [BitisDakika] AND [BaslangicDakika] % 30 = 0 AND [BitisDakika] % 30 = 0");
                    table.ForeignKey(
                        name: "FK_DoktorCalismaSaatleri_Doktorlar",
                        column: x => x.DoktorID,
                        principalTable: "Doktorlar",
                        principalColumn: "DoktorID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoktorIzinGunleri",
                columns: table => new
                {
                    IzinID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoktorID = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoktorIzinGunleri", x => x.IzinID);
                    table.ForeignKey(
                        name: "FK_DoktorIzinGunleri_Doktorlar",
                        column: x => x.DoktorID,
                        principalTable: "Doktorlar",
                        principalColumn: "DoktorID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoktorCalismaSaatleri_DoktorID_Gun",
                table: "DoktorCalismaSaatleri",
                columns: new[] { "DoktorID", "Gun" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoktorIzinGunleri_DoktorID_Tarih",
                table: "DoktorIzinGunleri",
                columns: new[] { "DoktorID", "Tarih" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoktorCalismaSaatleri");

            migrationBuilder.DropTable(
                name: "DoktorIzinGunleri");
        }
    }
}
