using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotWorkedTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "not_worked_time_status_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_not_worked_time_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_not_worked_time_status_catalog_items_country_catalog_countr~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "not_worked_time_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    applies_to_permission = table.Column<bool>(type: "boolean", nullable: false),
                    uses_work_schedule = table.Column<bool>(type: "boolean", nullable: false),
                    counts_holiday = table.Column<bool>(type: "boolean", nullable: false),
                    counts_saturday = table.Column<bool>(type: "boolean", nullable: false),
                    counts_rest_day = table.Column<bool>(type: "boolean", nullable: false),
                    counts_seventh_day_penalty = table.Column<bool>(type: "boolean", nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    deduction_concept_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    income_concept_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_not_worked_time_types", x => x.id);
                    table.CheckConstraint("ck_not_worked_time_types__deduction_concept", "discount_percent = 0 or deduction_concept_type_code is not null");
                    table.CheckConstraint("ck_not_worked_time_types__discount_percent", "discount_percent >= 0 and discount_percent <= 100");
                });

            migrationBuilder.CreateTable(
                name: "pf_not_worked_times",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type_name_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    uses_work_schedule = table.Column<bool>(type: "boolean", nullable: false),
                    counts_holiday = table.Column<bool>(type: "boolean", nullable: false),
                    counts_saturday = table.Column<bool>(type: "boolean", nullable: false),
                    counts_rest_day = table.Column<bool>(type: "boolean", nullable: false),
                    counts_seventh_day_penalty = table.Column<bool>(type: "boolean", nullable: false),
                    discount_percent_snapshot = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    deduction_concept_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    income_concept_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(9,2)", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    origin_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    calendar_days = table.Column<int>(type: "integer", nullable: false),
                    computable_days = table.Column<int>(type: "integer", nullable: false),
                    seventh_day_penalty_days = table.Column<int>(type: "integer", nullable: false),
                    discounted_days = table.Column<decimal>(type: "numeric(9,2)", nullable: false),
                    daily_salary_snapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    detail_json = table.Column<string>(type: "jsonb", nullable: true),
                    status_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registered_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    annulled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annulled_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pf_not_worked_times", x => x.id);
                    table.CheckConstraint("ck_pf_not_worked_times__discount_percent", "discount_percent_snapshot >= 0 and discount_percent_snapshot <= 100");
                    table.CheckConstraint("ck_pf_not_worked_times__range", "end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_pf_not_worked_times__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "action_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9965L, "TIEMPO_NO_TRABAJADO", new Guid("dd3e4826-89f4-d9c3-1f17-1dec8e2210a8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tiempo no trabajado", "TIEMPO_NO_TRABAJADO", "TIEMPO NO TRABAJADO", new Guid("0ea4b0ab-3303-2e0f-3267-e5f331ccaf66"), 240 });

            migrationBuilder.InsertData(
                table: "not_worked_time_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9961L, "ANULADO", new Guid("0dfc9874-1453-0839-abb0-5198da1ab579"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulado", "ANULADO", "ANULADO", new Guid("78427e38-721a-4e26-e803-a2269f47a910"), 20 },
                    { -9960L, "REGISTRADO", new Guid("e59e4e17-6ea9-3d57-2b32-7e507b02b0f6"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Registrado", "REGISTRADO", "REGISTRADO", new Guid("d8740980-18c4-ecd5-7163-069812179360"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_not_worked_time_status_catalog_items__country_active_sort",
                table: "not_worked_time_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_not_worked_time_status_catalog_items__country_code",
                table: "not_worked_time_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_not_worked_time_status_catalog_items__public_id",
                table: "not_worked_time_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_not_worked_time_types__tenant_active",
                table: "not_worked_time_types",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_not_worked_time_types__public_id",
                table: "not_worked_time_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_not_worked_time_types__tenant_code",
                table: "not_worked_time_types",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pf_not_worked_times__file_start",
                table: "pf_not_worked_times",
                columns: new[] { "personnel_file_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_pf_not_worked_times__tenant_start",
                table: "pf_not_worked_times",
                columns: new[] { "tenant_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_not_worked_times__public_id",
                table: "pf_not_worked_times",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "not_worked_time_status_catalog_items");

            migrationBuilder.DropTable(
                name: "not_worked_time_types");

            migrationBuilder.DropTable(
                name: "pf_not_worked_times");

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9965L);
        }
    }
}
