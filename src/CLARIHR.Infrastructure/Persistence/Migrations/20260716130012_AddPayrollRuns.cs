using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payroll_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payroll_definition_id = table.Column<long>(type: "bigint", nullable: false),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: false),
                    payroll_definition_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payroll_definition_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    period_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    period_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    generated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    regenerated_count = table.Column<int>(type: "integer", nullable: false),
                    authorized_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorized_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    return_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    annulled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    employee_count = table.Column<int>(type: "integer", nullable: false),
                    total_income = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total_deductions = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total_employer_cost = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total_net = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_payroll_runs__payroll_definition",
                        column: x => x.payroll_definition_id,
                        principalTable: "payroll_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payroll_runs__payroll_period",
                        column: x => x.payroll_period_id,
                        principalTable: "payroll_period_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_run_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payroll_run_id = table.Column<long>(type: "bigint", nullable: false),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    employee_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_center_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    concept_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concept_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    line_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    units = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    base_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    calculated_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    override_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    override_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    adjusted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_included = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    source_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    source_reference_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    warning_codes_json = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_run_lines", x => x.id);
                    table.CheckConstraint("ck_payroll_run_lines__line_class", "line_class IN ('Ingreso','Descuento','PagoPatronal')");
                    table.ForeignKey(
                        name: "fk_payroll_run_lines__payroll_run",
                        column: x => x.payroll_run_id,
                        principalTable: "payroll_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_run_lines__tenant_file_run",
                table: "payroll_run_lines",
                columns: new[] { "tenant_id", "personnel_file_id", "payroll_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_run_lines__tenant_run_concept",
                table: "payroll_run_lines",
                columns: new[] { "tenant_id", "payroll_run_id", "concept_code" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_run_lines__tenant_run_file",
                table: "payroll_run_lines",
                columns: new[] { "tenant_id", "payroll_run_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_run_lines__tenant_source",
                table: "payroll_run_lines",
                columns: new[] { "tenant_id", "source_module", "source_reference_public_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payroll_run_lines_payroll_run_id",
                table: "payroll_run_lines",
                column: "payroll_run_id");

            migrationBuilder.CreateIndex(
                name: "uq_payroll_run_lines__public_id",
                table: "payroll_run_lines",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payroll_runs__tenant_status",
                table: "payroll_runs",
                columns: new[] { "tenant_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_payroll_definition_id",
                table: "payroll_runs",
                column: "payroll_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_payroll_period_id",
                table: "payroll_runs",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "uq_payroll_runs__public_id",
                table: "payroll_runs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payroll_runs__tenant_definition_period_active",
                table: "payroll_runs",
                columns: new[] { "tenant_id", "payroll_definition_id", "payroll_period_id" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payroll_run_lines");

            migrationBuilder.DropTable(
                name: "payroll_runs");
        }
    }
}
