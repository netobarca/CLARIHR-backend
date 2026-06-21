using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRehireBlockToPersonnelFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_rehire_blocked",
                table: "personnel_files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "rehire_blocked_reason",
                table: "personnel_files",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_rehire_blocked",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "rehire_blocked_reason",
                table: "personnel_files");
        }
    }
}
