using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBaseSalaryToCompensationConceptType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_base_salary",
                table: "compensation_concept_type_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9736L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9735L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9734L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9733L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9732L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9731L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9730L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9729L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9728L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9727L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9726L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9725L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9724L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9723L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9722L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9721L,
                column: "is_base_salary",
                value: false);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9720L,
                column: "is_base_salary",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_base_salary",
                table: "compensation_concept_type_catalog_items");
        }
    }
}
