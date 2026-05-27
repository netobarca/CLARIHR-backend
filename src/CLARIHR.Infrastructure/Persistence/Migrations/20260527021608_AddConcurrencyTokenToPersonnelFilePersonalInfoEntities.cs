using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokenToPersonnelFilePersonalInfoEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_identifications",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_family_members",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_emergency_contacts",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_addresses",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_identifications");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_family_members");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_emergency_contacts");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_addresses");
        }
    }
}
