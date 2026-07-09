using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollTypeCatalogAndJournalIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payroll_type_catalog_items",
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
                    table.PrimaryKey("pk_payroll_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_type_catalog_items_country_catalog_country_catalog_~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "payroll_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9895L, "OTRO", new Guid("644037f4-7db7-c18f-1b4f-a4259b514ab5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("f6cc5317-378c-c7f0-c8cf-af271d8df2e7"), 60 },
                    { -9894L, "POR_OBRA", new Guid("040a0fa6-6667-3687-e38f-642851dd2d0d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Por obra", "POR_OBRA", "POR OBRA", new Guid("d83387b0-0279-55bc-a1aa-dc63f96070f2"), 50 },
                    { -9893L, "POR_DIA", new Guid("1209eba4-3fc5-5c84-f95a-d44798b8a099"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Por día", "POR_DIA", "POR DÍA", new Guid("dee2a8cf-79e0-77d4-b392-e465cb464d86"), 40 },
                    { -9892L, "SEMANAL", new Guid("dc4c86df-0a1a-f526-c8de-2ff942fe75a9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Semanal", "SEMANAL", "SEMANAL", new Guid("b60ce474-03e6-e8ed-0b7f-111019624000"), 30 },
                    { -9891L, "QUINCENAL", new Guid("69197eb1-6163-2c87-cf4b-ebf70c20760b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Quincenal", "QUINCENAL", "QUINCENAL", new Guid("706ebf7b-cf32-6af3-a49f-56c8afbcb2ab"), 20 },
                    { -9890L, "MENSUAL", new Guid("058184dd-0585-d9ac-cf12-a32423517299"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mensual", "MENSUAL", "MENSUAL", new Guid("5396350e-a52c-6fd8-0736-13e25582860a"), 10 }
                });

            // REQ-004 (tablero de acciones de personal · aclaración №2): destructive one-way cleanup of the legacy
            // free-text payroll_type_code, NO backfill. Step 1 normalizes case/whitespace on values that already
            // match a PAYROLL_TYPE_CATALOG code; step 2 nulls out everything else so the strict validate-by-code
            // (422 PAYROLL_TYPE_INVALID) governs from this deployment onward. Idempotent; Down() does NOT restore
            // the erased values — the cleanup is irreversible by design.
            migrationBuilder.Sql(
                @"UPDATE personnel_file_employment_assignments SET payroll_type_code = UPPER(TRIM(payroll_type_code))
WHERE payroll_type_code IS NOT NULL AND UPPER(TRIM(payroll_type_code)) IN ('MENSUAL','QUINCENAL','SEMANAL','POR_DIA','POR_OBRA','OTRO');");
            migrationBuilder.Sql(
                @"UPDATE personnel_file_employment_assignments SET payroll_type_code = NULL
WHERE payroll_type_code IS NOT NULL AND payroll_type_code NOT IN ('MENSUAL','QUINCENAL','SEMANAL','POR_DIA','POR_OBRA','OTRO');");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_personnel_actions_tenant_action_date",
                table: "personnel_file_personnel_actions",
                columns: new[] { "tenant_id", "action_date_utc" })
                .Annotation("Npgsql:IndexInclude", new[] { "action_type_code", "action_status_code", "is_system_generated", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_type_catalog_items__country_active_sort",
                table: "payroll_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_payroll_type_catalog_items__country_code",
                table: "payroll_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payroll_type_catalog_items__public_id",
                table: "payroll_type_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payroll_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_personnel_file_personnel_actions_tenant_action_date",
                table: "personnel_file_personnel_actions");
        }
    }
}
