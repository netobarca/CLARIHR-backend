using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "overtime_max_daily_minutes",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "overtime_self_service_enabled",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "overtime_justification_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_overtime_justification_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "overtime_record_status_catalog_items",
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
                    table.PrimaryKey("pk_overtime_record_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_overtime_record_status_catalog_items_country_catalog_countr~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "overtime_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    default_factor = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    payroll_effect_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_overtime_types", x => x.id);
                    table.CheckConstraint("ck_overtime_types__default_factor", "default_factor > 0");
                });

            migrationBuilder.InsertData(
                table: "overtime_record_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9914L, "ANULADA", new Guid("e8c826bd-78a4-eac0-11d5-4ee9a9d669cf"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("241935eb-57fb-df10-a41d-a1879542d18c"), 50 },
                    { -9913L, "APLICADA", new Guid("e6f5038d-e459-cc2b-0eb6-bef550d8662e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aplicada", "APLICADA", "APLICADA", new Guid("44578b12-1dea-7e93-9ea2-0b1b36642d03"), 40 },
                    { -9912L, "RECHAZADA", new Guid("126a330d-499e-3493-238c-6f3b0c07fb02"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazada", "RECHAZADA", "RECHAZADA", new Guid("e52539f9-ccd5-1bd4-1878-9ab8cbb51052"), 30 },
                    { -9911L, "AUTORIZADA", new Guid("5b0f50dd-d012-27d0-b58c-a8f90718b1ff"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Autorizada", "AUTORIZADA", "AUTORIZADA", new Guid("be16e042-07db-904f-0c9d-57b1e1095fca"), 20 },
                    { -9910L, "EN_REVISION", new Guid("1441fd97-d84f-65ee-aa2b-eef1f67be305"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En revisión", "EN_REVISION", "EN REVISIÓN", new Guid("32afefc7-2107-1460-9c11-820df2a1a118"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_overtime_justification_types__tenant_active",
                table: "overtime_justification_types",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_overtime_justification_types__public_id",
                table: "overtime_justification_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_overtime_justification_types__tenant_code_active",
                table: "overtime_justification_types",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_overtime_record_status_catalog_items__country_active_sort",
                table: "overtime_record_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_overtime_record_status_catalog_items__country_code",
                table: "overtime_record_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_overtime_record_status_catalog_items__public_id",
                table: "overtime_record_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_overtime_types__tenant_active",
                table: "overtime_types",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_overtime_types__public_id",
                table: "overtime_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_overtime_types__tenant_code_active",
                table: "overtime_types",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "overtime_justification_types");

            migrationBuilder.DropTable(
                name: "overtime_record_status_catalog_items");

            migrationBuilder.DropTable(
                name: "overtime_types");

            migrationBuilder.DropColumn(
                name: "overtime_max_daily_minutes",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "overtime_self_service_enabled",
                table: "company_preferences");
        }
    }
}
