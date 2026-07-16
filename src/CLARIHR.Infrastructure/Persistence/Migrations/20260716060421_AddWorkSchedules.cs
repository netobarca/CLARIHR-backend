using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_schedules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schedule_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attendance_date_anchor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    schedule_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_weekly_hours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedules", x => x.id);
                    table.CheckConstraint("ck_work_schedules__total_weekly_hours", "total_weekly_hours > 0 AND total_weekly_hours <= 168");
                });

            migrationBuilder.CreateTable(
                name: "work_schedule_days",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_schedule_id = table.Column<long>(type: "bigint", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    meal_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    meal_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    net_hours = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedule_days", x => x.id);
                    table.CheckConstraint("ck_work_schedule_days__day_of_week", "day_of_week >= 0 AND day_of_week <= 6");
                    table.ForeignKey(
                        name: "fk_work_schedule_days__work_schedule",
                        column: x => x.work_schedule_id,
                        principalTable: "work_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_work_schedule_days__public_id",
                table: "work_schedule_days",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_work_schedule_days__schedule_day",
                table: "work_schedule_days",
                columns: new[] { "work_schedule_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_schedules__tenant_active",
                table: "work_schedules",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_work_schedules__public_id",
                table: "work_schedules",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_work_schedules__tenant_code_active",
                table: "work_schedules",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true,
                filter: "is_active");

            // REQ-012 P-17 (ratified): destructive one-way cleanup of the legacy free-text workday_code,
            // NO backfill — mirrors the payroll_type_code cleanup of 20260709051104. Step 1 normalizes
            // case/whitespace on values that already match a work schedule of the SAME tenant; step 2 nulls
            // out everything else so the strict validate-by-code (422 WORK_SCHEDULE_INVALID) governs from
            // this deployment onward. Since the master is born empty here (the 44-h template loads at
            // provisioning/load-template, after this migration), in practice every legacy test value goes
            // NULL. Idempotent; Down() does NOT restore the erased values — irreversible by design.
            migrationBuilder.Sql(
                """
                UPDATE personnel_file_employment_assignments a SET workday_code = UPPER(BTRIM(a.workday_code))
                WHERE a.workday_code IS NOT NULL AND EXISTS (
                    SELECT 1 FROM work_schedules w
                    WHERE w.tenant_id = a.tenant_id AND w.normalized_code = UPPER(BTRIM(a.workday_code)));
                """);
            migrationBuilder.Sql(
                """
                UPDATE personnel_file_employment_assignments a SET workday_code = NULL
                WHERE a.workday_code IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM work_schedules w
                    WHERE w.tenant_id = a.tenant_id AND w.normalized_code = a.workday_code);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_schedule_days");

            migrationBuilder.DropTable(
                name: "work_schedules");
        }
    }
}
