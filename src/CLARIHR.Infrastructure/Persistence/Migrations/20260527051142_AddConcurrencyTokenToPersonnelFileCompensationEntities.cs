using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokenToPersonnelFileCompensationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_salary_items",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_payroll_transactions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_payment_methods",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_medical_claims",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_insurances",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_insurance_beneficiaries",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_bank_accounts",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "personnel_file_additional_benefits",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_salary_items");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_payroll_transactions");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_payment_methods");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_insurances");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_insurance_beneficiaries");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_bank_accounts");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "personnel_file_additional_benefits");
        }
    }
}
