using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendPayrollPeriodDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_payroll_period_definitions__tenant_type_year_number",
                table: "payroll_period_definitions");

            migrationBuilder.AddColumn<bool>(
                name: "allows_attendance",
                table: "payroll_period_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_overtime_entry",
                table: "payroll_period_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "attendance_entry_end",
                table: "payroll_period_definitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "attendance_entry_start",
                table: "payroll_period_definitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "payroll_period_definitions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "cutoff_date",
                table: "payroll_period_definitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "month",
                table: "payroll_period_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "overtime_entry_end",
                table: "payroll_period_definitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "overtime_entry_start",
                table: "payroll_period_definitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "payment_date",
                table: "payroll_period_definitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "payroll_definition_id",
                table: "payroll_period_definitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_code",
                table: "payroll_period_definitions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "GENERADO");

            // REQ-012 №3 backfill (soft default): legacy rows keep no Nómina (payroll_definition_id NULL)
            // and inherit code = normalized label, cutoff = end, payment = end, month = end's month
            // (status_code already defaulted to GENERADO by the column definition). Down() does not
            // restore — molde 20260709051104_AddPayrollTypeCatalogAndJournalIndex.
            migrationBuilder.Sql(
                """
                UPDATE payroll_period_definitions
                SET code = UPPER(BTRIM(label)),
                    cutoff_date = end_date,
                    payment_date = end_date,
                    month = EXTRACT(MONTH FROM end_date)::int
                WHERE code IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_payroll_period_definitions_payroll_definition_id",
                table: "payroll_period_definitions",
                column: "payroll_definition_id");

            migrationBuilder.CreateIndex(
                name: "uq_payroll_period_definitions__tenant_definition_year_number",
                table: "payroll_period_definitions",
                columns: new[] { "tenant_id", "payroll_definition_id", "year", "number" },
                unique: true,
                filter: "payroll_definition_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_payroll_period_definitions__tenant_type_year_number",
                table: "payroll_period_definitions",
                columns: new[] { "tenant_id", "pay_period_type_code", "year", "number" },
                unique: true,
                filter: "payroll_definition_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payroll_period_definitions__attendance_window",
                table: "payroll_period_definitions",
                sql: "attendance_entry_start IS NULL OR attendance_entry_end IS NULL OR attendance_entry_end >= attendance_entry_start");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payroll_period_definitions__cutoff_in_range",
                table: "payroll_period_definitions",
                sql: "cutoff_date IS NULL OR (cutoff_date >= start_date AND cutoff_date <= end_date)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payroll_period_definitions__month",
                table: "payroll_period_definitions",
                sql: "month IS NULL OR (month >= 1 AND month <= 12)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payroll_period_definitions__overtime_window",
                table: "payroll_period_definitions",
                sql: "overtime_entry_start IS NULL OR overtime_entry_end IS NULL OR overtime_entry_end >= overtime_entry_start");

            migrationBuilder.AddForeignKey(
                name: "fk_payroll_period_definitions__payroll_definition",
                table: "payroll_period_definitions",
                column: "payroll_definition_id",
                principalTable: "payroll_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payroll_period_definitions__payroll_definition",
                table: "payroll_period_definitions");

            migrationBuilder.DropIndex(
                name: "IX_payroll_period_definitions_payroll_definition_id",
                table: "payroll_period_definitions");

            migrationBuilder.DropIndex(
                name: "uq_payroll_period_definitions__tenant_definition_year_number",
                table: "payroll_period_definitions");

            migrationBuilder.DropIndex(
                name: "uq_payroll_period_definitions__tenant_type_year_number",
                table: "payroll_period_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payroll_period_definitions__attendance_window",
                table: "payroll_period_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payroll_period_definitions__cutoff_in_range",
                table: "payroll_period_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payroll_period_definitions__month",
                table: "payroll_period_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payroll_period_definitions__overtime_window",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "allows_attendance",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "allows_overtime_entry",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "attendance_entry_end",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "attendance_entry_start",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "code",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "cutoff_date",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "month",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "overtime_entry_end",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "overtime_entry_start",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "payment_date",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "payroll_definition_id",
                table: "payroll_period_definitions");

            migrationBuilder.DropColumn(
                name: "status_code",
                table: "payroll_period_definitions");

            migrationBuilder.CreateIndex(
                name: "uq_payroll_period_definitions__tenant_type_year_number",
                table: "payroll_period_definitions",
                columns: new[] { "tenant_id", "pay_period_type_code", "year", "number" },
                unique: true);
        }
    }
}
