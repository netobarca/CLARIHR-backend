using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRedundantPersonnelFilePositionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_personnel_files__tenant_assigned_position_slot",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "assigned_position_slot_public_id",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "job_profile_public_id",
                table: "personnel_file_employee_profiles");

            migrationBuilder.DropColumn(
                name: "position_slot_public_id",
                table: "personnel_file_employee_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_position_slot_public_id",
                table: "personnel_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "job_profile_public_id",
                table: "personnel_file_employee_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "position_slot_public_id",
                table: "personnel_file_employee_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_assigned_position_slot",
                table: "personnel_files",
                columns: new[] { "tenant_id", "assigned_position_slot_public_id" });
        }
    }
}
