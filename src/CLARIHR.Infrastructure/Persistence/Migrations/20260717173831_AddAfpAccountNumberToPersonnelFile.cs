using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // REQ-016 RF-007 (Gate B, P-11) — the NUP_ISSS identification type is NOT seeded here: unlike the old
    // (now-obsolete) unified personnel_reference_catalog_items table, identification_type_catalog_items has
    // no static HasData for any code (not even DUI/NIT) — every row is created at runtime, per tenant/country,
    // through the System Catalog Administration API (SystemCatalogAdministration.cs /
    // SystemCatalogRepository.cs). Creating a NUP_ISSS row (country SV) is an operational step for whoever
    // provisions each tenant, the same as it already is for DUI/NIT — not a migration seed.
    public partial class AddAfpAccountNumberToPersonnelFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "afp_account_number",
                table: "personnel_files",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "afp_account_number",
                table: "personnel_files");
        }
    }
}
