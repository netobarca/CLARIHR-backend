using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOneTimeDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_one_time_deductions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    deduction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    concept_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concept_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    observations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_fixed_value = table.Column<bool>(type: "boolean", nullable: false),
                    calculation_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    unit_value = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    multiplier = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    percentage = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    base_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: true),
                    payroll_period_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payroll_period_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payroll_period_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    annulled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    applied_by_settlement_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_one_time_deductions", x => x.id);
                    table.CheckConstraint("ck_pf_one_time_deductions__amount_positive", "amount > 0");
                    table.CheckConstraint("ck_pf_one_time_deductions__base_amount", "base_amount IS NULL OR base_amount > 0");
                    table.CheckConstraint("ck_pf_one_time_deductions__fixed_or_method", "is_fixed_value = true OR calculation_method IS NOT NULL");
                    table.CheckConstraint("ck_pf_one_time_deductions__multiplier", "multiplier IS NULL OR multiplier > 0");
                    table.CheckConstraint("ck_pf_one_time_deductions__percentage", "percentage IS NULL OR percentage > 0");
                    table.CheckConstraint("ck_pf_one_time_deductions__quantity", "quantity IS NULL OR quantity > 0");
                    table.CheckConstraint("ck_pf_one_time_deductions__unit_value", "unit_value IS NULL OR unit_value > 0");
                    table.ForeignKey(
                        name: "fk_pf_one_time_deductions__payroll_period",
                        column: x => x.payroll_period_id,
                        principalTable: "payroll_period_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pf_one_time_deductions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_one_time_deduction_applications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    one_time_deduction_id = table.Column<long>(type: "bigint", nullable: false),
                    applied_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: true),
                    payroll_period_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payroll_period_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    origin_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    applied_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_public_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_pf_one_time_deduction_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_pf_otd_applications__one_time_deduction",
                        column: x => x.one_time_deduction_id,
                        principalTable: "personnel_file_one_time_deductions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pf_otd_applications__payroll_period",
                        column: x => x.payroll_period_id,
                        principalTable: "payroll_period_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_one_time_deduction_applications_one_time_ded~",
                table: "personnel_file_one_time_deduction_applications",
                column: "one_time_deduction_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_one_time_deduction_applications_payroll_peri~",
                table: "personnel_file_one_time_deduction_applications",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_otd_applications__deduction",
                table: "personnel_file_one_time_deduction_applications",
                columns: new[] { "tenant_id", "one_time_deduction_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_otd_applications__payroll_applied_date",
                table: "personnel_file_one_time_deduction_applications",
                columns: new[] { "tenant_id", "payroll_type_code", "applied_date" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_otd_applications__deduction_active",
                table: "personnel_file_one_time_deduction_applications",
                columns: new[] { "tenant_id", "one_time_deduction_id" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "uq_pf_otd_applications__public_id",
                table: "personnel_file_one_time_deduction_applications",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_one_time_deductions_payroll_period_id",
                table: "personnel_file_one_time_deductions",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_one_time_deductions_personnel_file_id",
                table: "personnel_file_one_time_deductions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_one_time_deductions__tenant_file_status",
                table: "personnel_file_one_time_deductions",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_one_time_deductions__tenant_status_date",
                table: "personnel_file_one_time_deductions",
                columns: new[] { "tenant_id", "status_code", "deduction_date" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_one_time_deductions__tenant_status_payroll",
                table: "personnel_file_one_time_deductions",
                columns: new[] { "tenant_id", "status_code", "payroll_type_code" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_one_time_deductions__public_id",
                table: "personnel_file_one_time_deductions",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_one_time_deduction_applications");

            migrationBuilder.DropTable(
                name: "personnel_file_one_time_deductions");
        }
    }
}
