using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensatoryTimeSettlementConcept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "settlement_concept_catalog_items",
                keyColumn: "id",
                keyValue: -9837L,
                column: "is_system_calculated",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "settlement_concept_catalog_items",
                keyColumn: "id",
                keyValue: -9837L,
                column: "is_system_calculated",
                value: false);
        }
    }
}
