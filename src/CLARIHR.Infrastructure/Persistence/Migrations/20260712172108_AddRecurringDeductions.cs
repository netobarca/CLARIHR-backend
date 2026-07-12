using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_recurring_deductions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recurring_deduction_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concept_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concept_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    financial_institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    exception_months = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    installment_frequency_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    application_frequency_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_indefinite = table.Column<bool>(type: "boolean", nullable: false),
                    settlement_action_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    uses_compound_interest = table.Column<bool>(type: "boolean", nullable: false),
                    principal_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    interest_rate_percent = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    planned_installments = table.Column<int>(type: "integer", nullable: true),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    suspended_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspension_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_by_settlement_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_recurring_deductions", x => x.id);
                    table.CheckConstraint("ck_pf_recurring_deductions__interest_fields", "uses_compound_interest = false OR (principal_amount > 0 AND interest_rate_percent > 0 AND planned_installments >= 1)");
                    table.CheckConstraint("ck_pf_recurring_deductions__interest_finite", "NOT (is_indefinite AND uses_compound_interest)");
                    table.CheckConstraint("ck_pf_recurring_deductions__principal_positive", "principal_amount IS NULL OR principal_amount > 0");
                    table.ForeignKey(
                        name: "fk_pf_recurring_deductions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_recurring_deduction_installments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recurring_deduction_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: true),
                    extraordinary_number = table.Column<int>(type: "integer", nullable: true),
                    applied_date = table.Column<DateOnly>(type: "date", nullable: false),
                    theoretical_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    capital_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    interest_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: true),
                    payroll_period_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    origin_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    applied_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    annulled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pf_recurring_deduction_installments", x => x.id);
                    table.CheckConstraint("ck_pf_rd_installments__amount_positive", "amount > 0");
                    table.CheckConstraint("ck_pf_rd_installments__capital_not_negative", "capital_amount IS NULL OR capital_amount >= 0");
                    table.CheckConstraint("ck_pf_rd_installments__extraordinary_number", "(kind = 'EXTRAORDINARIA') = (extraordinary_number IS NOT NULL)");
                    table.CheckConstraint("ck_pf_rd_installments__interest_not_negative", "interest_amount IS NULL OR interest_amount >= 0");
                    table.CheckConstraint("ck_pf_rd_installments__regular_number", "(kind = 'REGULAR') = (installment_number IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_pf_rd_installments__payroll_period",
                        column: x => x.payroll_period_id,
                        principalTable: "payroll_period_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pf_rd_installments__recurring_deduction",
                        column: x => x.recurring_deduction_id,
                        principalTable: "personnel_file_recurring_deductions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_recurring_deduction_plan_segments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recurring_deduction_id = table.Column<long>(type: "bigint", nullable: false),
                    from_installment = table.Column<int>(type: "integer", nullable: false),
                    to_installment = table.Column<int>(type: "integer", nullable: true),
                    installment_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pf_recurring_deduction_plan_segments", x => x.id);
                    table.CheckConstraint("ck_pf_rd_segments__from_positive", "from_installment >= 1");
                    table.CheckConstraint("ck_pf_rd_segments__range_ordered", "to_installment IS NULL OR to_installment >= from_installment");
                    table.CheckConstraint("ck_pf_rd_segments__value_positive", "installment_value > 0");
                    table.ForeignKey(
                        name: "fk_pf_rd_segments__recurring_deduction",
                        column: x => x.recurring_deduction_id,
                        principalTable: "personnel_file_recurring_deductions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recurring_deduction_installments_payroll_per~",
                table: "personnel_file_recurring_deduction_installments",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recurring_deduction_installments_recurring_d~",
                table: "personnel_file_recurring_deduction_installments",
                column: "recurring_deduction_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_rd_installments__deduction_number",
                table: "personnel_file_recurring_deduction_installments",
                columns: new[] { "tenant_id", "recurring_deduction_id", "installment_number" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_rd_installments__payroll_applied_date",
                table: "personnel_file_recurring_deduction_installments",
                columns: new[] { "tenant_id", "payroll_type_code", "applied_date" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_rd_installments__deduction_extra_active",
                table: "personnel_file_recurring_deduction_installments",
                columns: new[] { "tenant_id", "recurring_deduction_id", "extraordinary_number" },
                unique: true,
                filter: "is_active AND kind = 'EXTRAORDINARIA'");

            migrationBuilder.CreateIndex(
                name: "uq_pf_rd_installments__deduction_number_active",
                table: "personnel_file_recurring_deduction_installments",
                columns: new[] { "tenant_id", "recurring_deduction_id", "installment_number" },
                unique: true,
                filter: "is_active AND kind = 'REGULAR'");

            migrationBuilder.CreateIndex(
                name: "uq_pf_rd_installments__public_id",
                table: "personnel_file_recurring_deduction_installments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recurring_deduction_plan_segments_recurring_~",
                table: "personnel_file_recurring_deduction_plan_segments",
                column: "recurring_deduction_id");

            migrationBuilder.CreateIndex(
                name: "uq_pf_rd_segments__deduction_from_active",
                table: "personnel_file_recurring_deduction_plan_segments",
                columns: new[] { "tenant_id", "recurring_deduction_id", "from_installment" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "uq_pf_rd_segments__public_id",
                table: "personnel_file_recurring_deduction_plan_segments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recurring_deductions_personnel_file_id",
                table: "personnel_file_recurring_deductions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_recurring_deductions__tenant_file_status",
                table: "personnel_file_recurring_deductions",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_recurring_deductions__tenant_status_payroll",
                table: "personnel_file_recurring_deductions",
                columns: new[] { "tenant_id", "status_code", "payroll_type_code" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_recurring_deductions__public_id",
                table: "personnel_file_recurring_deductions",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_recurring_deduction_installments");

            migrationBuilder.DropTable(
                name: "personnel_file_recurring_deduction_plan_segments");

            migrationBuilder.DropTable(
                name: "personnel_file_recurring_deductions");
        }
    }
}
