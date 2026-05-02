using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePersonnelDocumentDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_personnel_file_documents__loan_return",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "delivery_date",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "loan_date",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "return_date",
                table: "personnel_file_documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "delivery_date",
                table: "personnel_file_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "loan_date",
                table: "personnel_file_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "return_date",
                table: "personnel_file_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_personnel_file_documents__loan_return",
                table: "personnel_file_documents",
                sql: "return_date is null or loan_date is null or return_date >= loan_date");
        }
    }
}
