using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H29AguinaldoTaxability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9725L,
                columns: new[] { "affects_afp", "affects_isss" },
                values: new object[] { false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9725L,
                columns: new[] { "affects_afp", "affects_isss" },
                values: new object[] { true, true });
        }
    }
}
