using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportExportJobsAndPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_export_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resource_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    queued_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    worker_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: true),
                    artifact_blob_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    artifact_file_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    artifact_content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    artifact_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_export_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_birth_public",
                table: "personnel_files",
                columns: new[] { "tenant_id", "birth_date", "public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_created_public",
                table: "personnel_files",
                columns: new[] { "tenant_id", "created_utc", "public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_lifecycle_type_org_unit",
                table: "personnel_files",
                columns: new[] { "tenant_id", "lifecycle_status", "record_type", "org_unit_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_report_export_jobs__expiration",
                table: "report_export_jobs",
                columns: new[] { "status", "expires_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_report_export_jobs__tenant_queued_public",
                table: "report_export_jobs",
                columns: new[] { "tenant_id", "queued_utc", "public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_report_export_jobs__tenant_status_queued",
                table: "report_export_jobs",
                columns: new[] { "tenant_id", "status", "queued_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_report_export_jobs__worker_claim",
                table: "report_export_jobs",
                columns: new[] { "status", "lease_until_utc", "queued_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_report_export_jobs__public_id",
                table: "report_export_jobs",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_export_jobs");

            migrationBuilder.DropIndex(
                name: "ix_personnel_files__tenant_birth_public",
                table: "personnel_files");

            migrationBuilder.DropIndex(
                name: "ix_personnel_files__tenant_created_public",
                table: "personnel_files");

            migrationBuilder.DropIndex(
                name: "ix_personnel_files__tenant_lifecycle_type_org_unit",
                table: "personnel_files");
        }
    }
}
