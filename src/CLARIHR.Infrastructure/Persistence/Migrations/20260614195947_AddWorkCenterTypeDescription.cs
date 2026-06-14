using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the optional <c>description</c> column (varchar(500), nullable) to the
    /// <c>work_center_types</c> catalog so the type's stored description round-trips through the API
    /// (mirror of <c>cost_center_types.description</c>). Nullable, so existing rows need no backfill.
    /// </summary>
    public partial class AddWorkCenterTypeDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "work_center_types",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "work_center_types");
        }
    }
}
