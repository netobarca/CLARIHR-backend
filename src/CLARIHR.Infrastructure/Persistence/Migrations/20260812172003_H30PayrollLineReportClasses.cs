using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H30PayrollLineReportClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deduction_class",
                table: "payroll_run_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "income_class",
                table: "payroll_run_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_payroll_run_lines__report_class_matches_side",
                table: "payroll_run_lines",
                sql: "(line_class = 'Ingreso'      AND deduction_class IS NULL)\nOR (line_class = 'Descuento' AND income_class    IS NULL)\nOR (line_class = 'PagoPatronal' AND income_class IS NULL AND deduction_class IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payroll_run_lines__report_class_matches_side",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "deduction_class",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "income_class",
                table: "payroll_run_lines");
        }
    }
}
