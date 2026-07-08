using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonnelFileIncapacitiesAndLactation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_incapacities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    requester_file_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requester_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origin_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    incapacity_risk_id = table.Column<long>(type: "bigint", nullable: false),
                    risk_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    risk_counts_seventh_day_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    risk_counts_saturday_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    risk_counts_holiday_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    risk_uses_fund_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    risk_has_subsidy_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    medical_clinic_id = table.Column<long>(type: "bigint", nullable: true),
                    incapacity_type_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    payroll_period_definition_id = table.Column<long>(type: "bigint", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    calendar_days = table.Column<int>(type: "integer", nullable: false),
                    computable_days = table.Column<int>(type: "integer", nullable: false),
                    computable_days_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    override_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subsidized_days = table.Column<int>(type: "integer", nullable: false),
                    discount_days = table.Column<int>(type: "integer", nullable: false),
                    employer_days = table.Column<int>(type: "integer", nullable: false),
                    monthly_base_salary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    daily_salary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    subsidy_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    employer_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tranche_detail_json = table.Column<string>(type: "jsonb", nullable: true),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    extends_incapacity_id = table.Column<long>(type: "bigint", nullable: true),
                    confirmed_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    annulled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_incapacities", x => x.id);
                    table.CheckConstraint("ck_personnel_file_incapacities__dates", "end_date is null or end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacities__extends_incapacity",
                        column: x => x.extends_incapacity_id,
                        principalTable: "personnel_file_incapacities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacities__incapacity_risk",
                        column: x => x.incapacity_risk_id,
                        principalTable: "incapacity_risks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacities__incapacity_type",
                        column: x => x.incapacity_type_id,
                        principalTable: "incapacity_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacities__medical_clinic",
                        column: x => x.medical_clinic_id,
                        principalTable: "medical_clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacities__payroll_period_definition",
                        column: x => x.payroll_period_definition_id,
                        principalTable: "payroll_period_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacities__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_lactation_periods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    requester_file_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requester_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    incapacity_type_id = table.Column<long>(type: "bigint", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    annulled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_lactation_periods", x => x.id);
                    table.CheckConstraint("ck_personnel_file_lactation_periods__dates", "end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_lactation_periods__incapacity_type",
                        column: x => x.incapacity_type_id,
                        principalTable: "incapacity_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_personnel_file_lactation_periods__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_incapacity_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_incapacity_id = table.Column<long>(type: "bigint", nullable: false),
                    document_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_incapacity_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacity_documents__document_type",
                        column: x => x.document_type_catalog_item_id,
                        principalTable: "document_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_personnel_file_incapacity_documents__incapacity",
                        column: x => x.personnel_file_incapacity_id,
                        principalTable: "personnel_file_incapacities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lactation_schedules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lactation_period_id = table.Column<long>(type: "bigint", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    daily_permits_count = table.Column<int>(type: "integer", nullable: false),
                    minutes_per_permit = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lactation_schedules", x => x.id);
                    table.CheckConstraint("ck_lactation_schedules__daily_permits_count", "daily_permits_count >= 1");
                    table.CheckConstraint("ck_lactation_schedules__dates", "end_date >= start_date");
                    table.CheckConstraint("ck_lactation_schedules__minutes_per_permit", "minutes_per_permit >= 1");
                    table.ForeignKey(
                        name: "fk_lactation_schedules__lactation_period",
                        column: x => x.lactation_period_id,
                        principalTable: "personnel_file_lactation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lactation_schedules__period_sort",
                table: "lactation_schedules",
                columns: new[] { "tenant_id", "lactation_period_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_lactation_schedules_lactation_period_id",
                table: "lactation_schedules",
                column: "lactation_period_id");

            migrationBuilder.CreateIndex(
                name: "uq_lactation_schedules__public_id",
                table: "lactation_schedules",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_incapacities__tenant_file_status",
                table: "personnel_file_incapacities",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_incapacities__tenant_start",
                table: "personnel_file_incapacities",
                columns: new[] { "tenant_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_incapacities_extends_incapacity_id",
                table: "personnel_file_incapacities",
                column: "extends_incapacity_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_incapacities_incapacity_risk_id",
                table: "personnel_file_incapacities",
                column: "incapacity_risk_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_incapacities_incapacity_type_id",
                table: "personnel_file_incapacities",
                column: "incapacity_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_incapacities_medical_clinic_id",
                table: "personnel_file_incapacities",
                column: "medical_clinic_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_incapacities_payroll_period_definition_id",
                table: "personnel_file_incapacities",
                column: "payroll_period_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_incapacities_personnel_file_id",
                table: "personnel_file_incapacities",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_incapacities__public_id",
                table: "personnel_file_incapacities",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_incapacity_documents__document_type",
                table: "personnel_file_incapacity_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_incapacity_documents__file_public_id",
                table: "personnel_file_incapacity_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_incapacity_documents__tenant_incapacity_active",
                table: "personnel_file_incapacity_documents",
                columns: new[] { "tenant_id", "personnel_file_incapacity_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_incapacity_documents_personnel_file_incapaci~",
                table: "personnel_file_incapacity_documents",
                column: "personnel_file_incapacity_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_incapacity_documents__public_id",
                table: "personnel_file_incapacity_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_lactation_periods__tenant_file_status",
                table: "personnel_file_lactation_periods",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_lactation_periods_incapacity_type_id",
                table: "personnel_file_lactation_periods",
                column: "incapacity_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_lactation_periods_personnel_file_id",
                table: "personnel_file_lactation_periods",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_lactation_periods__public_id",
                table: "personnel_file_lactation_periods",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lactation_schedules");

            migrationBuilder.DropTable(
                name: "personnel_file_incapacity_documents");

            migrationBuilder.DropTable(
                name: "personnel_file_lactation_periods");

            migrationBuilder.DropTable(
                name: "personnel_file_incapacities");
        }
    }
}
