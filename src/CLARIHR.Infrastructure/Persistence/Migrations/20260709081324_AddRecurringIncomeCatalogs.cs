using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringIncomeCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_income_settle_action_catalog_items",
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
                    table.PrimaryKey("pk_recurring_income_settle_action_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_income_settle_action_catalog_items_country_catalo~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_income_status_catalog_items",
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
                    table.PrimaryKey("pk_recurring_income_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_income_status_catalog_items_country_catalog_count~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_income_type_catalog_items",
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
                    table.PrimaryKey("pk_recurring_income_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_income_type_catalog_items_country_catalog_country~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "recurring_income_settle_action_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9887L, "CANCELAR", new Guid("8d5199a9-392a-2111-8168-49e5505bd42f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cancelar al liquidar", "CANCELAR", "CANCELAR AL LIQUIDAR", new Guid("6deaa317-47ea-ffb9-01b4-665a24605a0e"), 20 },
                    { -9886L, "PAGAR_SALDO", new Guid("65ebe24d-75df-afa1-c9cb-d80193902558"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pagar saldo al liquidar", "PAGAR_SALDO", "PAGAR SALDO AL LIQUIDAR", new Guid("4aeb72a7-5cde-e56e-b243-817fbff65278"), 10 }
                });

            migrationBuilder.InsertData(
                table: "recurring_income_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9885L, "ANULADO", new Guid("f3362fb3-536f-7e97-f0e8-0379c32a6bf0"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulado", "ANULADO", "ANULADO", new Guid("47d411d5-cb13-22f8-3a2d-e35d279e76ee"), 60 },
                    { -9884L, "FINALIZADO", new Guid("0fc73a9f-5e2d-ccfb-f5b3-fc7d8de284c4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Finalizado", "FINALIZADO", "FINALIZADO", new Guid("fd2f6585-3e22-eea6-1ec1-14540e12d16b"), 50 },
                    { -9883L, "SUSPENDIDO", new Guid("b1017462-d5f9-0831-dc79-90bb2ea863cf"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Suspendido", "SUSPENDIDO", "SUSPENDIDO", new Guid("9180e3a6-1bb5-b5c9-3616-fe34d063b2b2"), 40 },
                    { -9882L, "RECHAZADO", new Guid("eaa93bb5-7bea-8243-d54d-cce2b91e4a8e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazado", "RECHAZADO", "RECHAZADO", new Guid("ab21fb54-5a28-a339-d05c-544774ec78fc"), 30 },
                    { -9881L, "VIGENTE", new Guid("60609f78-b8e3-4f61-6ab6-66fd0060491b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vigente", "VIGENTE", "VIGENTE", new Guid("f4f39b38-a182-8ae1-eed4-470ce16e345e"), 20 },
                    { -9880L, "EN_REVISION", new Guid("b2b3ae92-ec1a-1ac6-0ae1-1a3ed70ddbd0"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En revisión", "EN_REVISION", "EN REVISIÓN", new Guid("9950b695-d0e1-0c80-84fe-abccc758fe6e"), 10 }
                });

            migrationBuilder.InsertData(
                table: "recurring_income_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9899L, "OTRO", new Guid("9d68997d-de67-c33b-5e0a-2169bfdcacb8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("31c03516-49a8-abea-08fd-a85081f34885"), 40 },
                    { -9898L, "COMBUSTIBLE", new Guid("e0c5d65c-f07f-144a-5b7c-3c39fef98a9f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Combustible", "COMBUSTIBLE", "COMBUSTIBLE", new Guid("fd539af5-f1b8-dc30-49aa-9c1fdaea62cf"), 30 },
                    { -9897L, "GASTOS_REPRESENTACION", new Guid("a0026ce5-713f-01d1-1a6b-2af912c272e3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gastos de representación", "GASTOS_REPRESENTACION", "GASTOS DE REPRESENTACIÓN", new Guid("06d1816b-13df-b800-e576-4c973ca865c3"), 20 },
                    { -9896L, "AYUDA_ALIMENTACION", new Guid("6aa34294-3a13-5a21-9667-340ad7bf3e0c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ayuda para alimentación", "AYUDA_ALIMENTACION", "AYUDA PARA ALIMENTACIÓN", new Guid("8968e31d-08ef-23dc-222e-4283c7909a70"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_income_settle_action_catalog_items__active_sort",
                table: "recurring_income_settle_action_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_recurring_income_settle_action_catalog_items__country_code",
                table: "recurring_income_settle_action_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_recurring_income_settle_action_catalog_items__public_id",
                table: "recurring_income_settle_action_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_income_status_catalog_items__country_active_sort",
                table: "recurring_income_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_recurring_income_status_catalog_items__country_code",
                table: "recurring_income_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_recurring_income_status_catalog_items__public_id",
                table: "recurring_income_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_income_type_catalog_items__country_active_sort",
                table: "recurring_income_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_recurring_income_type_catalog_items__country_code",
                table: "recurring_income_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_recurring_income_type_catalog_items__public_id",
                table: "recurring_income_type_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurring_income_settle_action_catalog_items");

            migrationBuilder.DropTable(
                name: "recurring_income_status_catalog_items");

            migrationBuilder.DropTable(
                name: "recurring_income_type_catalog_items");
        }
    }
}
