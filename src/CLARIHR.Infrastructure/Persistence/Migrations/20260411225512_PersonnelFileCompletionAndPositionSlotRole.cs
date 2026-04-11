using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonnelFileCompletionAndPositionSlotRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "role_id",
                table: "position_slots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_position_slot_public_id",
                table: "personnel_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_status",
                table: "personnel_files",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "linked_user_public_id",
                table: "personnel_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE personnel_files
                SET lifecycle_status = 'Draft'
                WHERE lifecycle_status IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "lifecycle_status",
                table: "personnel_files",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_role",
                table: "position_slots",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "IX_position_slots_role_id",
                table: "position_slots",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_assigned_position_slot",
                table: "personnel_files",
                columns: new[] { "tenant_id", "assigned_position_slot_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_lifecycle_type",
                table: "personnel_files",
                columns: new[] { "tenant_id", "lifecycle_status", "record_type" });

            migrationBuilder.CreateIndex(
                name: "uq_personnel_files__tenant_linked_user",
                table: "personnel_files",
                columns: new[] { "tenant_id", "linked_user_public_id" },
                unique: true,
                filter: "linked_user_public_id is not null");

            migrationBuilder.AddForeignKey(
                name: "fk_position_slots__role",
                table: "position_slots",
                column: "role_id",
                principalTable: "iam_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_position_slots__role",
                table: "position_slots");

            migrationBuilder.DropIndex(
                name: "ix_position_slots__tenant_role",
                table: "position_slots");

            migrationBuilder.DropIndex(
                name: "IX_position_slots_role_id",
                table: "position_slots");

            migrationBuilder.DropIndex(
                name: "ix_personnel_files__tenant_assigned_position_slot",
                table: "personnel_files");

            migrationBuilder.DropIndex(
                name: "ix_personnel_files__tenant_lifecycle_type",
                table: "personnel_files");

            migrationBuilder.DropIndex(
                name: "uq_personnel_files__tenant_linked_user",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "position_slots");

            migrationBuilder.DropColumn(
                name: "assigned_position_slot_public_id",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "lifecycle_status",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "linked_user_public_id",
                table: "personnel_files");
        }
    }
}
