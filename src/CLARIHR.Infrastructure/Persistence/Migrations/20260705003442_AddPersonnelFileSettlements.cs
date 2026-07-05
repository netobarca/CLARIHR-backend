using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonnelFileSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_settlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retirement_request_id = table.Column<long>(type: "bigint", nullable: true),
                    retirement_request_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    plaza_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cost_center_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_center_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    retirement_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retirement_category_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    retirement_category_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    retirement_reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    retirement_reason_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requester_file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    request_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    minimum_monthly_wage = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    indemnity_cap_multiplier = table.Column<decimal>(type: "numeric(11,8)", nullable: false),
                    resignation_cap_multiplier = table.Column<decimal>(type: "numeric(11,8)", nullable: false),
                    vacation_days = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    vacation_premium_percent = table.Column<decimal>(type: "numeric(11,8)", nullable: false),
                    aguinaldo_days = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    resignation_benefit_days = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    resignation_minimum_service_years = table.Column<int>(type: "integer", nullable: false),
                    aguinaldo_exemption_multiplier = table.Column<decimal>(type: "numeric(11,8)", nullable: false),
                    month_divisor_days = table.Column<int>(type: "integer", nullable: false),
                    year_divisor_days = table.Column<int>(type: "integer", nullable: false),
                    monthly_base_salary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    seniority_years = table.Column<int>(type: "integer", nullable: false),
                    seniority_days = table.Column<int>(type: "integer", nullable: false),
                    capped_monthly_salary_indemnity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    capped_monthly_salary_resignation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_incomes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_deductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    net_pay = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_employer_charges = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    provision_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    issued_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    annulled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annulled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_settlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_settlements__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_settlement_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    settlement_id = table.Column<long>(type: "bigint", nullable: false),
                    concept_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    concept_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concept_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_system_calculated = table.Column<bool>(type: "boolean", nullable: false),
                    calculation_base = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    units_or_days = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    calculated_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    exempt_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    taxable_excess_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    override_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    final_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_included = table.Column<bool>(type: "boolean", nullable: false),
                    is_zero_by_law = table.Column<bool>(type: "boolean", nullable: false),
                    zero_reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    calculation_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    counterparty_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_settlement_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_settlement_lines__settlement",
                        column: x => x.settlement_id,
                        principalTable: "personnel_file_settlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_settlement_lines__tenant_settlement_sort",
                table: "personnel_file_settlement_lines",
                columns: new[] { "tenant_id", "settlement_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_settlement_lines_settlement_id",
                table: "personnel_file_settlement_lines",
                column: "settlement_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_settlement_lines__public_id",
                table: "personnel_file_settlement_lines",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_settlements__tenant_file_kind",
                table: "personnel_file_settlements",
                columns: new[] { "tenant_id", "personnel_file_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_settlements__tenant_kind_status_date",
                table: "personnel_file_settlements",
                columns: new[] { "tenant_id", "kind", "status_code", "request_date" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_settlements_personnel_file_id",
                table: "personnel_file_settlements",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_settlements__public_id",
                table: "personnel_file_settlements",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_settlements__tenant_retirement_position",
                table: "personnel_file_settlements",
                columns: new[] { "tenant_id", "retirement_request_id", "assigned_position_public_id" },
                unique: true,
                filter: "kind = 'Liquidacion' and status_code <> 'ANULADA' and is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_settlement_lines");

            migrationBuilder.DropTable(
                name: "personnel_file_settlements");
        }
    }
}
