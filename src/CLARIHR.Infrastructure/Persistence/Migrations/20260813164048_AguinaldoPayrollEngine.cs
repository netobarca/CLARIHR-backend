using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AguinaldoPayrollEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "exempt_amount",
                table: "payroll_run_lines",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "purpose_code",
                table: "payroll_definitions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ORDINARIA");

            migrationBuilder.AddColumn<int>(
                name: "aguinaldo_payment_day",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "aguinaldo_payment_month",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aguinaldo_exemptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year = table.Column<int>(type: "integer", nullable: false),
                    exempt_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aguinaldo_exemptions", x => x.id);
                    table.CheckConstraint("ck_aguinaldo_exemptions__amount", "exempt_amount >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "uq_aguinaldo_exemptions__public_id",
                table: "aguinaldo_exemptions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_aguinaldo_exemptions__tenant_year",
                table: "aguinaldo_exemptions",
                columns: new[] { "tenant_id", "year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aguinaldo_exemptions");

            migrationBuilder.DropColumn(
                name: "exempt_amount",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "purpose_code",
                table: "payroll_definitions");

            migrationBuilder.DropColumn(
                name: "aguinaldo_payment_day",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "aguinaldo_payment_month",
                table: "company_preferences");
        }
    }
}
