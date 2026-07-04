using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetirementRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_retirement_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    requester_file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    request_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retirement_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retirement_category_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    retirement_category_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    retirement_reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    retirement_reason_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    request_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    canceled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    executed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    execution_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    prior_employment_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    prior_login_was_active = table.Column<bool>(type: "boolean", nullable: true),
                    prior_rehire_blocked = table.Column<bool>(type: "boolean", nullable: true),
                    prior_rehire_block_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reverted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_retirement_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_retirement_requests__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_retirement_closed_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    retirement_request_id = table.Column<long>(type: "bigint", nullable: false),
                    entity_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entity_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_retirement_closed_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_retirement_closed_records__request",
                        column: x => x.retirement_request_id,
                        principalTable: "personnel_file_retirement_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_retirement_closed_records__tenant_request",
                table: "personnel_file_retirement_closed_records",
                columns: new[] { "tenant_id", "retirement_request_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_retirement_closed_records_retirement_request~",
                table: "personnel_file_retirement_closed_records",
                column: "retirement_request_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_retirement_closed_records__public_id",
                table: "personnel_file_retirement_closed_records",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_retirement_requests__tenant_file_date",
                table: "personnel_file_retirement_requests",
                columns: new[] { "tenant_id", "personnel_file_id", "request_date" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_retirement_requests__tenant_file_status",
                table: "personnel_file_retirement_requests",
                columns: new[] { "tenant_id", "personnel_file_id", "request_status_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_retirement_requests_personnel_file_id",
                table: "personnel_file_retirement_requests",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_retirement_requests__public_id",
                table: "personnel_file_retirement_requests",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_retirement_requests__tenant_file_open",
                table: "personnel_file_retirement_requests",
                columns: new[] { "tenant_id", "personnel_file_id" },
                unique: true,
                filter: "request_status_code in ('SOLICITADA','AUTORIZADA') and is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_retirement_closed_records");

            migrationBuilder.DropTable(
                name: "personnel_file_retirement_requests");
        }
    }
}
