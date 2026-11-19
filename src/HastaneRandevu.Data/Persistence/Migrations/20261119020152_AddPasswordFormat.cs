using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HastaneRandevu.Data.Persistence.Migrations
{
    /// <summary>
    /// Records how each stored password has to be checked (SifreBicimi: 0 PBKDF2 hash, 1 legacy plain
    /// text). Databases that reach this migration were created by <see cref="InitialCreate"/>, which
    /// only ever stores hashes, so existing rows get 0. Databases of the original application are
    /// adopted by LegacySchemaUpgrader instead, which adds the column with 1 for every existing row.
    /// </summary>
    public partial class AddPasswordFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Hastalar", "Doktorlar" })
            {
                // The default only fills the existing rows; new rows always state their format.
                migrationBuilder.AddColumn<int>(
                    name: "SifreBicimi",
                    table: table,
                    type: "int",
                    nullable: false,
                    defaultValue: 0);

                migrationBuilder.AlterColumn<int>(
                    name: "SifreBicimi",
                    table: table,
                    type: "int",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "int",
                    oldDefaultValue: 0);

                migrationBuilder.AddCheckConstraint(
                    name: $"CK_{table}_SifreBicimi",
                    table: table,
                    sql: "[SifreBicimi] IN (0, 1)");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Hastalar", "Doktorlar" })
            {
                migrationBuilder.DropCheckConstraint(
                    name: $"CK_{table}_SifreBicimi",
                    table: table);

                migrationBuilder.DropColumn(
                    name: "SifreBicimi",
                    table: table);
            }
        }
    }
}
