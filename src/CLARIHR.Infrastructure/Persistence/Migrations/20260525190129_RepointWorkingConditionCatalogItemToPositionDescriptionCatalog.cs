using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepointWorkingConditionCatalogItemToPositionDescriptionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_job_profile_working_conditions__catalog_item",
                table: "job_profile_working_conditions");

            // The working-condition catalog item now references Position Description Catalog
            // "WorkCondition" items (canonical JobProfileCatalogBindingMap), not job_catalog_items.
            // Null out any pre-existing value that cannot satisfy the new FK so it can be added
            // cleanly. Expected to affect 0 rows: the old job-catalog path never resolved for
            // clients (it 404'd), so no working condition was ever persisted with a valid one.
            migrationBuilder.Sql(@"
                UPDATE job_profile_working_conditions
                SET catalog_item_id = NULL
                WHERE catalog_item_id IS NOT NULL
                  AND catalog_item_id NOT IN (SELECT id FROM position_description_catalog_items);");

            migrationBuilder.AddForeignKey(
                name: "fk_job_profile_working_conditions__catalog_item",
                table: "job_profile_working_conditions",
                column: "catalog_item_id",
                principalTable: "position_description_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_job_profile_working_conditions__catalog_item",
                table: "job_profile_working_conditions");

            migrationBuilder.AddForeignKey(
                name: "fk_job_profile_working_conditions__catalog_item",
                table: "job_profile_working_conditions",
                column: "catalog_item_id",
                principalTable: "job_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
