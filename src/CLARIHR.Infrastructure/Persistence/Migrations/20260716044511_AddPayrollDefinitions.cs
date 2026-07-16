using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payroll_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    pay_period_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    total_periods = table.Column<int>(type: "integer", nullable: false),
                    guarantees_minimum_income = table.Column<bool>(type: "boolean", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    overtime_window_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    overtime_window_offset_days = table.Column<int>(type: "integer", nullable: true),
                    attendance_window_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    attendance_window_offset_days = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_definitions", x => x.id);
                    table.CheckConstraint("ck_payroll_definitions__total_periods", "total_periods >= 1");
                });

            migrationBuilder.CreateTable(
                name: "payroll_period_status_catalog_items",
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
                    table.PrimaryKey("pk_payroll_period_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_period_status_catalog_items_country_catalog_country~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_run_status_catalog_items",
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
                    table.PrimaryKey("pk_payroll_run_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_run_status_catalog_items_country_catalog_country_ca~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "payroll_period_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9977L, "ANULADO", new Guid("47eb48c6-bc15-7a61-df9a-b0869fb49e7c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulado", "ANULADO", "ANULADO", new Guid("1ca5bdc9-eb94-a116-826c-b496d16eb588"), 30 },
                    { -9976L, "CERRADO", new Guid("79d2ed2d-52a6-7787-8b02-3d1f80c077d9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cerrado", "CERRADO", "CERRADO", new Guid("c03b3e64-ce12-df53-ee5d-2928abfbbb70"), 20 },
                    { -9975L, "GENERADO", new Guid("d65ffca3-8a7e-edde-67c7-4d6bebaca9d1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Generado", "GENERADO", "GENERADO", new Guid("0a8a4d88-b233-6fb9-883e-31dcde70ebc3"), 10 }
                });

            migrationBuilder.InsertData(
                table: "payroll_run_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9973L, "ANULADA", new Guid("bd3b23f9-a480-d50b-d3c7-8eebd0aa65ce"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("27789610-4d71-dd66-f1b4-9663fdb9cb97"), 40 },
                    { -9972L, "CERRADA", new Guid("4bf5db80-6593-22a5-5fdd-12f06eafeacf"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cerrada", "CERRADA", "CERRADA", new Guid("0ad97085-b357-ddd4-521a-3122b01b5bca"), 30 },
                    { -9971L, "AUTORIZADA", new Guid("018e12dc-4b15-4293-0e8d-245bbdb3900b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Autorizada", "AUTORIZADA", "AUTORIZADA", new Guid("580a0013-dca6-bd74-d7ae-ac4841930c95"), 20 },
                    { -9970L, "GENERADA", new Guid("88ad3978-33a8-963a-ade0-1e65c3aadd87"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Generada", "GENERADA", "GENERADA", new Guid("a2a69009-d9cd-4003-51cc-07f3c648936f"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_definitions__tenant_active",
                table: "payroll_definitions",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_payroll_definitions__public_id",
                table: "payroll_definitions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payroll_definitions__tenant_code_active",
                table: "payroll_definitions",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_payroll_period_status_catalog_items__country_active_sort",
                table: "payroll_period_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_payroll_period_status_catalog_items__country_code",
                table: "payroll_period_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payroll_period_status_catalog_items__public_id",
                table: "payroll_period_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payroll_run_status_catalog_items__country_active_sort",
                table: "payroll_run_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_payroll_run_status_catalog_items__country_code",
                table: "payroll_run_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payroll_run_status_catalog_items__public_id",
                table: "payroll_run_status_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payroll_definitions");

            migrationBuilder.DropTable(
                name: "payroll_period_status_catalog_items");

            migrationBuilder.DropTable(
                name: "payroll_run_status_catalog_items");
        }
    }
}
