using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExitInterviewSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exit_interview_submissions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exit_interview_form_id = table.Column<long>(type: "bigint", nullable: false),
                    form_version = table.Column<int>(type: "integer", nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: true),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retirement_reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    retirement_category_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    separation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    position_slot_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plaza_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_score = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exit_interview_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_exit_interview_submissions__form",
                        column: x => x.exit_interview_form_id,
                        principalTable: "exit_interview_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exit_interview_submissions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exit_interview_answers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exit_interview_submission_id = table.Column<long>(type: "bigint", nullable: false),
                    field_key_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    control_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    value_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    value_number = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    value_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    selected_option_codes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    weight_snapshot = table.Column<decimal>(type: "numeric(9,2)", nullable: true),
                    normalized_score = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exit_interview_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_exit_interview_answers__submission",
                        column: x => x.exit_interview_submission_id,
                        principalTable: "exit_interview_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_answers__submission",
                table: "exit_interview_answers",
                columns: new[] { "tenant_id", "exit_interview_submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_exit_interview_answers_exit_interview_submission_id",
                table: "exit_interview_answers",
                column: "exit_interview_submission_id");

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_answers__public_id",
                table: "exit_interview_answers",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_submissions__file_status",
                table: "exit_interview_submissions",
                columns: new[] { "tenant_id", "personnel_file_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_submissions__form",
                table: "exit_interview_submissions",
                columns: new[] { "tenant_id", "exit_interview_form_id" });

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_submissions__period_category",
                table: "exit_interview_submissions",
                columns: new[] { "tenant_id", "period", "retirement_category_code" });

            migrationBuilder.CreateIndex(
                name: "IX_exit_interview_submissions_exit_interview_form_id",
                table: "exit_interview_submissions",
                column: "exit_interview_form_id");

            migrationBuilder.CreateIndex(
                name: "IX_exit_interview_submissions_personnel_file_id",
                table: "exit_interview_submissions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_submissions__public_id",
                table: "exit_interview_submissions",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exit_interview_answers");

            migrationBuilder.DropTable(
                name: "exit_interview_submissions");
        }
    }
}
