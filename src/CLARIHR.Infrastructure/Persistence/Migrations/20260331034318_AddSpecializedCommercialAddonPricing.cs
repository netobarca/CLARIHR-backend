using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecializedCommercialAddonPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "price_per_active_employee",
                table: "commercial_addons",
                newName: "unit_price");

            migrationBuilder.AddColumn<string>(
                name: "billing_model",
                table: "commercial_addons",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "measurement_unit",
                table: "commercial_addons",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "minimum_quantity",
                table: "commercial_addons",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE commercial_addons
                SET billing_model = 'PerActiveEmployee',
                    measurement_unit = 'active employee',
                    minimum_quantity = NULL
                WHERE billing_model IS NULL
                  AND measurement_unit IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "measurement_unit",
                table: "commercial_addons",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "billing_model",
                table: "commercial_addons",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_commercial_addons__billing_model",
                table: "commercial_addons",
                column: "billing_model");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_commercial_addons__billing_model",
                table: "commercial_addons");

            migrationBuilder.DropColumn(
                name: "billing_model",
                table: "commercial_addons");

            migrationBuilder.DropColumn(
                name: "measurement_unit",
                table: "commercial_addons");

            migrationBuilder.DropColumn(
                name: "minimum_quantity",
                table: "commercial_addons");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "commercial_addons",
                newName: "price_per_active_employee");
        }
    }
}
