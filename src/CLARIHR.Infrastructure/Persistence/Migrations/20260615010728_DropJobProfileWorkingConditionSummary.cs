using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hard-drops the redundant <c>working_condition_summary</c> free-text column from <c>job_profiles</c>.
    /// The structured working-conditions list is the single source of truth; any narrative summary is
    /// derived from it on demand. Down re-adds the nullable column for reversibility.
    /// </summary>
    public partial class DropJobProfileWorkingConditionSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "working_condition_summary",
                table: "job_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "working_condition_summary",
                table: "job_profiles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }
    }
}
