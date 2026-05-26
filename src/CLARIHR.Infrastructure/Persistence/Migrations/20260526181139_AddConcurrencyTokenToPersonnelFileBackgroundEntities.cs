using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokenToPersonnelFileBackgroundEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_trainings",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_references",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_previous_employments",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_languages",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_educations",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_trainings");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_references");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_previous_employments");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_languages");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_educations");
        }
    }
}
