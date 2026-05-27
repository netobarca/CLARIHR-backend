using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokenToPersonnelFileEmploymentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_personnel_actions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_employment_assignments",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_contract_histories",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_authorization_substitutions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_assets_accesses",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_personnel_actions");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_employment_assignments");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_contract_histories");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_authorization_substitutions");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_assets_accesses");
        }
    }
}
