using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokenToPersonnelFileInterestsEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_hobbies",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_employee_relations",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_associations",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_hobbies");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_employee_relations");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_associations");
        }
    }
}
