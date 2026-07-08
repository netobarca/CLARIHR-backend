using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonnelFileCompensatoryTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_compensatory_time_absences",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    compensatory_time_type_id = table.Column<long>(type: "bigint", nullable: false),
                    type_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hours_debited = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payroll_period_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    annulled_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_personnel_file_compensatory_time_absences", x => x.id);
                    table.CheckConstraint("ck_pf_comp_time_absences__dates", "start_date <= end_date");
                    table.CheckConstraint("ck_pf_comp_time_absences__hours_debited_positive", "hours_debited > 0");
                    table.ForeignKey(
                        name: "fk_pf_comp_time_absences__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pf_comp_time_absences__type",
                        column: x => x.compensatory_time_type_id,
                        principalTable: "compensatory_time_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_compensatory_time_credits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    compensatory_time_type_id = table.Column<long>(type: "bigint", nullable: false),
                    type_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hours_worked = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    factor_applied = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    hours_credited = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    is_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    override_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    work_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    authorized_by_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    authorizer_file_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    overtime_record_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    annulled_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_personnel_file_compensatory_time_credits", x => x.id);
                    table.CheckConstraint("ck_pf_comp_time_credits__factor_applied_positive", "factor_applied > 0");
                    table.CheckConstraint("ck_pf_comp_time_credits__hours_credited_positive", "hours_credited > 0");
                    table.CheckConstraint("ck_pf_comp_time_credits__hours_worked_positive", "hours_worked > 0");
                    table.ForeignKey(
                        name: "fk_pf_comp_time_credits__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pf_comp_time_credits__type",
                        column: x => x.compensatory_time_type_id,
                        principalTable: "compensatory_time_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_compensatory_time_credit_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    credit_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_personnel_file_compensatory_time_credit_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_pf_comp_time_credit_docs__credit",
                        column: x => x.credit_id,
                        principalTable: "personnel_file_compensatory_time_credits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pf_comp_time_credit_docs__document_type",
                        column: x => x.document_type_catalog_item_id,
                        principalTable: "document_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_compensatory_time_absences_compensatory_time~",
                table: "personnel_file_compensatory_time_absences",
                column: "compensatory_time_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_compensatory_time_absences_personnel_file_id",
                table: "personnel_file_compensatory_time_absences",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_comp_time_absences__tenant_file_dates",
                table: "personnel_file_compensatory_time_absences",
                columns: new[] { "tenant_id", "personnel_file_id", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_comp_time_absences__tenant_file_status",
                table: "personnel_file_compensatory_time_absences",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_comp_time_absences__public_id",
                table: "personnel_file_compensatory_time_absences",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_compensatory_time_credit_documents_credit_id",
                table: "personnel_file_compensatory_time_credit_documents",
                column: "credit_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_comp_time_credit_docs__document_type",
                table: "personnel_file_compensatory_time_credit_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_comp_time_credit_docs__file_public_id",
                table: "personnel_file_compensatory_time_credit_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_comp_time_credit_docs__tenant_credit_active",
                table: "personnel_file_compensatory_time_credit_documents",
                columns: new[] { "tenant_id", "credit_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_comp_time_credit_docs__public_id",
                table: "personnel_file_compensatory_time_credit_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_compensatory_time_credits_compensatory_time_~",
                table: "personnel_file_compensatory_time_credits",
                column: "compensatory_time_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_compensatory_time_credits_personnel_file_id",
                table: "personnel_file_compensatory_time_credits",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_comp_time_credits__tenant_file_status",
                table: "personnel_file_compensatory_time_credits",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_comp_time_credits__tenant_work_date",
                table: "personnel_file_compensatory_time_credits",
                columns: new[] { "tenant_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_comp_time_credits__public_id",
                table: "personnel_file_compensatory_time_credits",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_compensatory_time_absences");

            migrationBuilder.DropTable(
                name: "personnel_file_compensatory_time_credit_documents");

            migrationBuilder.DropTable(
                name: "personnel_file_compensatory_time_credits");
        }
    }
}
