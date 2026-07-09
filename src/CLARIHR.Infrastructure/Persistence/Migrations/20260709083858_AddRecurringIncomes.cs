using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringIncomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_recurring_incomes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    registration_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recurring_income_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concept_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concept_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    observations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_center_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_center_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    installment_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    installment_frequency_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_indefinite = table.Column<bool>(type: "boolean", nullable: false),
                    installment_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    installment_count = table.Column<int>(type: "integer", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    settlement_action_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
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
                    table.PrimaryKey("pk_personnel_file_recurring_incomes", x => x.id);
                    table.CheckConstraint("ck_pf_recurring_incomes__indefinite_no_limits", "is_indefinite = false OR (installment_count IS NULL AND total_amount IS NULL)");
                    table.CheckConstraint("ck_pf_recurring_incomes__installment_count", "installment_count IS NULL OR installment_count >= 1");
                    table.CheckConstraint("ck_pf_recurring_incomes__installment_value", "installment_value > 0");
                    table.CheckConstraint("ck_pf_recurring_incomes__total_amount", "total_amount IS NULL OR total_amount > 0");
                    table.ForeignKey(
                        name: "fk_pf_recurring_incomes__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_recurring_income_installments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recurring_income_id = table.Column<long>(type: "bigint", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    applied_date = table.Column<DateOnly>(type: "date", nullable: false),
                    theoretical_due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("pk_personnel_file_recurring_income_installments", x => x.id);
                    table.CheckConstraint("ck_pf_ri_installments__amount_positive", "amount > 0");
                    table.CheckConstraint("ck_pf_ri_installments__number_positive", "installment_number >= 1");
                    table.ForeignKey(
                        name: "fk_pf_ri_installments__payroll_period",
                        column: x => x.payroll_period_id,
                        principalTable: "payroll_period_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pf_ri_installments__recurring_income",
                        column: x => x.recurring_income_id,
                        principalTable: "personnel_file_recurring_incomes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recurring_income_installments_payroll_period~",
                table: "personnel_file_recurring_income_installments",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recurring_income_installments_recurring_inco~",
                table: "personnel_file_recurring_income_installments",
                column: "recurring_income_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_ri_installments__income_number",
                table: "personnel_file_recurring_income_installments",
                columns: new[] { "tenant_id", "recurring_income_id", "installment_number" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_ri_installments__payroll_applied_date",
                table: "personnel_file_recurring_income_installments",
                columns: new[] { "tenant_id", "payroll_type_code", "applied_date" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_ri_installments__income_number_active",
                table: "personnel_file_recurring_income_installments",
                columns: new[] { "tenant_id", "recurring_income_id", "installment_number" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "uq_pf_ri_installments__public_id",
                table: "personnel_file_recurring_income_installments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recurring_incomes_personnel_file_id",
                table: "personnel_file_recurring_incomes",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_recurring_incomes__tenant_file_status",
                table: "personnel_file_recurring_incomes",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_recurring_incomes__tenant_status_payroll",
                table: "personnel_file_recurring_incomes",
                columns: new[] { "tenant_id", "status_code", "payroll_type_code" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_recurring_incomes__public_id",
                table: "personnel_file_recurring_incomes",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_recurring_income_installments");

            migrationBuilder.DropTable(
                name: "personnel_file_recurring_incomes");
        }
    }
}
