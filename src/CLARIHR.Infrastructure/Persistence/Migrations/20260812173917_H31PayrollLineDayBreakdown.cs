using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H31PayrollLineDayBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "employer_paid_days",
                table: "payroll_run_lines",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "subsidized_days",
                table: "payroll_run_lines",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unpaid_days",
                table: "payroll_run_lines",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "employer_paid_days",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "subsidized_days",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "unpaid_days",
                table: "payroll_run_lines");
        }
    }
}
