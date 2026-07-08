using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVacationFundAndRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_file_vacation_periods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    period_year = table.Column<int>(type: "integer", nullable: false),
                    period_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    legal_days_granted = table.Column<int>(type: "integer", nullable: false),
                    benefit_days_granted = table.Column<int>(type: "integer", nullable: false),
                    generates_enjoyment_days = table.Column<bool>(type: "boolean", nullable: false),
                    used_anniversary = table.Column<bool>(type: "boolean", nullable: false),
                    source_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_vacation_periods", x => x.id);
                    table.CheckConstraint("ck_personnel_file_vacation_periods__benefit_days", "benefit_days_granted >= 0");
                    table.CheckConstraint("ck_personnel_file_vacation_periods__dates", "period_end_date >= period_start_date");
                    table.CheckConstraint("ck_personnel_file_vacation_periods__legal_days", "legal_days_granted > 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_vacation_periods__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_vacation_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    requester_file_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requester_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    requested_days = table.Column<int>(type: "integer", nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    plan_line_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    decision_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_personnel_file_vacation_requests", x => x.id);
                    table.CheckConstraint("ck_personnel_file_vacation_requests__dates", "end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_vacation_requests__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacation_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_year = table.Column<int>(type: "integer", nullable: false),
                    request_date = table.Column<DateOnly>(type: "date", nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requester_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacation_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vacation_request_allocations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vacation_request_id = table.Column<long>(type: "bigint", nullable: false),
                    vacation_period_id = table.Column<long>(type: "bigint", nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacation_request_allocations", x => x.id);
                    table.CheckConstraint("ck_vacation_request_allocations__days", "days > 0");
                    table.ForeignKey(
                        name: "fk_vacation_request_allocations__request",
                        column: x => x.vacation_request_id,
                        principalTable: "personnel_file_vacation_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vacation_request_allocations__vacation_period",
                        column: x => x.vacation_period_id,
                        principalTable: "personnel_file_vacation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacation_returns",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vacation_request_id = table.Column<long>(type: "bigint", nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    return_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    distribution_json = table.Column<string>(type: "jsonb", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacation_returns", x => x.id);
                    table.CheckConstraint("ck_vacation_returns__days", "days > 0");
                    table.ForeignKey(
                        name: "fk_vacation_returns__request",
                        column: x => x.vacation_request_id,
                        principalTable: "personnel_file_vacation_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacation_plan_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vacation_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    personnel_file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacation_plan_lines", x => x.id);
                    table.CheckConstraint("ck_vacation_plan_lines__dates", "end_date >= start_date");
                    table.CheckConstraint("ck_vacation_plan_lines__days", "days > 0");
                    table.ForeignKey(
                        name: "fk_vacation_plan_lines__vacation_plan",
                        column: x => x.vacation_plan_id,
                        principalTable: "vacation_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_vacation_periods_personnel_file_id",
                table: "personnel_file_vacation_periods",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_vacation_periods__public_id",
                table: "personnel_file_vacation_periods",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_pf_vacation_periods__tenant_file_year_active",
                table: "personnel_file_vacation_periods",
                columns: new[] { "tenant_id", "personnel_file_id", "period_year" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_vacation_requests__tenant_file_status",
                table: "personnel_file_vacation_requests",
                columns: new[] { "tenant_id", "personnel_file_id", "status_code" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_vacation_requests__tenant_start",
                table: "personnel_file_vacation_requests",
                columns: new[] { "tenant_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_vacation_requests_personnel_file_id",
                table: "personnel_file_vacation_requests",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_vacation_requests__public_id",
                table: "personnel_file_vacation_requests",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacation_plan_lines__plan_employee",
                table: "vacation_plan_lines",
                columns: new[] { "tenant_id", "vacation_plan_id", "personnel_file_public_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vacation_plan_lines_vacation_plan_id",
                table: "vacation_plan_lines",
                column: "vacation_plan_id");

            migrationBuilder.CreateIndex(
                name: "uq_vacation_plan_lines__public_id",
                table: "vacation_plan_lines",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacation_plans__tenant_year_status",
                table: "vacation_plans",
                columns: new[] { "tenant_id", "plan_year", "status_code" });

            migrationBuilder.CreateIndex(
                name: "uq_vacation_plans__public_id",
                table: "vacation_plans",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacation_request_allocations__tenant_request",
                table: "vacation_request_allocations",
                columns: new[] { "tenant_id", "vacation_request_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vacation_request_allocations__vacation_period",
                table: "vacation_request_allocations",
                column: "vacation_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_allocations_vacation_request_id",
                table: "vacation_request_allocations",
                column: "vacation_request_id");

            migrationBuilder.CreateIndex(
                name: "uq_vacation_request_allocations__public_id",
                table: "vacation_request_allocations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacation_returns__tenant_request",
                table: "vacation_returns",
                columns: new[] { "tenant_id", "vacation_request_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vacation_returns_vacation_request_id",
                table: "vacation_returns",
                column: "vacation_request_id");

            migrationBuilder.CreateIndex(
                name: "uq_vacation_returns__public_id",
                table: "vacation_returns",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vacation_plan_lines");

            migrationBuilder.DropTable(
                name: "vacation_request_allocations");

            migrationBuilder.DropTable(
                name: "vacation_returns");

            migrationBuilder.DropTable(
                name: "vacation_plans");

            migrationBuilder.DropTable(
                name: "personnel_file_vacation_periods");

            migrationBuilder.DropTable(
                name: "personnel_file_vacation_requests");
        }
    }
}
