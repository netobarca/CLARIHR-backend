using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H29ConceptIncomeClassAndTaxability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "affects_afp",
                table: "compensation_concept_type_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "affects_isss",
                table: "compensation_concept_type_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "affects_renta",
                table: "compensation_concept_type_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "default_income_class",
                table: "compensation_concept_type_catalog_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9738L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9737L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9736L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9735L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9734L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9733L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9732L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9731L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9730L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9729L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9728L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9727L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9726L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, "NoDeducible" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9725L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { true, true, true, "Aguinaldo" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9724L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { false, false, false, "NoDeducible" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9723L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { true, true, true, "Bono" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9722L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { true, true, true, "Comision" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9721L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { true, true, true, "HorasExtra" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9720L,
                columns: new[] { "affects_afp", "affects_isss", "affects_renta", "default_income_class" },
                values: new object[] { true, true, true, "Salario" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "affects_afp",
                table: "compensation_concept_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "affects_isss",
                table: "compensation_concept_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "affects_renta",
                table: "compensation_concept_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "default_income_class",
                table: "compensation_concept_type_catalog_items");
        }
    }
}
