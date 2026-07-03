using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CleanEmergencyContactRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RF-004 / RT-06 (drop & recreate ratificado): emergency-contact relationship is now validated
            // against the Kinship catalog. Existing free-text rows that do not match an active kinship code
            // are removed — no backfill/mapping to OTRO by design ("no importa que haya datos, se deben
            // eliminar"). New writes are rejected with 422 when the code is not an active kinship code.
            migrationBuilder.Sql(
                """
                DELETE FROM personnel_file_emergency_contacts contact
                WHERE UPPER(TRIM(contact.relationship)) NOT IN (
                    SELECT kinship.normalized_code
                    FROM kinship_catalog_items kinship
                    WHERE kinship.is_active
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data cleanup is irreversible by design (RT-06): deleted free-text rows are not restored.
        }
    }
}
