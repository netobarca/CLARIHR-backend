using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberFormatToIdentificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "number_format",
                table: "identification_type_catalog_items",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // RF-003 / §20.3: anchored default patterns for the SV identification types (editable).
            // Identification types are seeded via the legacy reference-catalog copy (not HasData), so the
            // patterns are applied by direct UPDATE keyed on the normalized code.
            migrationBuilder.Sql(
                """
                UPDATE identification_type_catalog_items SET number_format = '^\d{8}-\d$' WHERE normalized_code = 'DUI';
                UPDATE identification_type_catalog_items SET number_format = '^\d{4}-\d{6}-\d{3}-\d$' WHERE normalized_code = 'NIT';
                UPDATE identification_type_catalog_items SET number_format = '^[A-Z0-9]{6,12}$' WHERE normalized_code = 'PASSPORT';
                UPDATE identification_type_catalog_items SET number_format = '^[A-Za-z0-9-]{5,20}$' WHERE normalized_code = 'RESIDENT_CARD';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "number_format",
                table: "identification_type_catalog_items");
        }
    }
}
