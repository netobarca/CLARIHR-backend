using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonnelFileRecognitionsAndDisciplinaryActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_disciplinary_actions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    disciplinary_action_type_id = table.Column<long>(type: "bigint", nullable: false),
                    type_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type_applied_suspension = table.Column<bool>(type: "boolean", nullable: false),
                    disciplinary_action_cause_id = table.Column<long>(type: "bigint", nullable: false),
                    cause_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    incident_date = table.Column<DateOnly>(type: "date", nullable: false),
                    facts_detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    has_payroll_deduction = table.Column<bool>(type: "boolean", nullable: false),
                    deduction_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    deduction_concept_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    deduction_concept_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    suspension_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    suspension_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    suspension_days = table.Column<int>(type: "integer", nullable: true),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    decided_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    decided_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    annulled_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    personnel_action_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suspension_action_public_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_personnel_file_disciplinary_actions", x => x.id);
                    table.CheckConstraint("ck_pf_disc_actions__deduction_amount_positive", "deduction_amount IS NULL OR deduction_amount > 0");
                    table.CheckConstraint("ck_pf_disc_actions__suspension_dates", "suspension_start_date IS NULL OR suspension_end_date IS NULL OR suspension_start_date <= suspension_end_date");
                    table.ForeignKey(
                        name: "fk_pf_disc_actions__cause",
                        column: x => x.disciplinary_action_cause_id,
                        principalTable: "disciplinary_action_causes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pf_disc_actions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pf_disc_actions__type",
                        column: x => x.disciplinary_action_type_id,
                        principalTable: "disciplinary_action_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_recognitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    recognition_type_id = table.Column<long>(type: "bigint", nullable: false),
                    type_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    decided_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    decided_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    annulled_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    personnel_action_public_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_personnel_file_recognitions", x => x.id);
                    table.CheckConstraint("ck_pf_recognitions__amount_positive", "amount IS NULL OR amount > 0");
                    table.ForeignKey(
                        name: "fk_pf_recognitions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pf_recognitions__recognition_type",
                        column: x => x.recognition_type_id,
                        principalTable: "recognition_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_disciplinary_action_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    disciplinary_action_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_personnel_file_disciplinary_action_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_pf_disc_action_docs__disciplinary_action",
                        column: x => x.disciplinary_action_id,
                        principalTable: "personnel_file_disciplinary_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pf_disc_action_docs__document_type",
                        column: x => x.document_type_catalog_item_id,
                        principalTable: "document_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_recognition_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recognition_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_personnel_file_recognition_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_pf_recognition_docs__document_type",
                        column: x => x.document_type_catalog_item_id,
                        principalTable: "document_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pf_recognition_docs__recognition",
                        column: x => x.recognition_id,
                        principalTable: "personnel_file_recognitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_disciplinary_action_documents_disciplinary_a~",
                table: "personnel_file_disciplinary_action_documents",
                column: "disciplinary_action_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_disc_action_docs__document_type",
                table: "personnel_file_disciplinary_action_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_disc_action_docs__file_public_id",
                table: "personnel_file_disciplinary_action_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_disc_action_docs__tenant_action_active",
                table: "personnel_file_disciplinary_action_documents",
                columns: new[] { "tenant_id", "disciplinary_action_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_disc_action_docs__public_id",
                table: "personnel_file_disciplinary_action_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_disciplinary_actions_disciplinary_action_cau~",
                table: "personnel_file_disciplinary_actions",
                column: "disciplinary_action_cause_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_disciplinary_actions_disciplinary_action_typ~",
                table: "personnel_file_disciplinary_actions",
                column: "disciplinary_action_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_disciplinary_actions_personnel_file_id",
                table: "personnel_file_disciplinary_actions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_disc_actions__tenant_file_status",
                table: "personnel_file_disciplinary_actions",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_disc_actions__tenant_status_incident",
                table: "personnel_file_disciplinary_actions",
                columns: new[] { "tenant_id", "status_code", "incident_date" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_disc_actions__tenant_suspension_dates",
                table: "personnel_file_disciplinary_actions",
                columns: new[] { "tenant_id", "suspension_start_date", "suspension_end_date" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_disc_actions__public_id",
                table: "personnel_file_disciplinary_actions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recognition_documents_recognition_id",
                table: "personnel_file_recognition_documents",
                column: "recognition_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_recognition_docs__document_type",
                table: "personnel_file_recognition_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_recognition_docs__file_public_id",
                table: "personnel_file_recognition_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_recognition_docs__tenant_recog_active",
                table: "personnel_file_recognition_documents",
                columns: new[] { "tenant_id", "recognition_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_recognition_docs__public_id",
                table: "personnel_file_recognition_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recognitions_personnel_file_id",
                table: "personnel_file_recognitions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_recognitions_recognition_type_id",
                table: "personnel_file_recognitions",
                column: "recognition_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_pf_recognitions__tenant_file_status",
                table: "personnel_file_recognitions",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_recognitions__public_id",
                table: "personnel_file_recognitions",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_disciplinary_action_documents");

            migrationBuilder.DropTable(
                name: "personnel_file_recognition_documents");

            migrationBuilder.DropTable(
                name: "personnel_file_disciplinary_actions");

            migrationBuilder.DropTable(
                name: "personnel_file_recognitions");
        }
    }
}
