using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H23DeriveSlotOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_position_slots__tenant_status",
                table: "position_slots");

            migrationBuilder.DropColumn(
                name: "occupied_employees",
                table: "position_slots");

            migrationBuilder.DropColumn(
                name: "status",
                table: "position_slots");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_employment_assignments__tenant_slot_active",
                table: "personnel_file_employment_assignments",
                columns: new[] { "tenant_id", "position_slot_public_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_personnel_file_employment_assignments__tenant_slot_active",
                table: "personnel_file_employment_assignments");

            migrationBuilder.AddColumn<int>(
                name: "occupied_employees",
                table: "position_slots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "position_slots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_status",
                table: "position_slots",
                columns: new[] { "tenant_id", "status" });
        }
    }
}
