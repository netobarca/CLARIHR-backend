using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetirementRequestStatusCatalogAndBajaActionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retirement_request_status_catalog_items",
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
                    table.PrimaryKey("pk_retirement_request_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_retirement_request_status_catalog_items_country_catalog_cou~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "action_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9483L, "REVERSION_BAJA", new Guid("ae16529c-8971-7336-0e42-b6ded34f13b5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Reversión de baja", "REVERSION_BAJA", "REVERSIÓN DE BAJA", new Guid("e6394b19-950f-55f0-90a3-d1809fc06366"), 140 },
                    { -9482L, "BAJA", new Guid("7e053686-2e0f-d25a-a7c1-db39708aa7bd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Baja / retiro definitivo", "BAJA", "BAJA / RETIRO DEFINITIVO", new Guid("894db1f6-8c86-9573-2765-d74a9563988a"), 130 }
                });

            migrationBuilder.InsertData(
                table: "retirement_request_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9815L, "REVERTIDA", new Guid("4e03bc01-2566-723e-4304-25bb53f7ca3d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Revertida", "REVERTIDA", "REVERTIDA", new Guid("1561f4a4-35d9-f8b1-5112-25ffe9db131f"), 60 },
                    { -9814L, "EJECUTADA", new Guid("363f65ba-7961-644f-81fe-6d4399854954"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ejecutada", "EJECUTADA", "EJECUTADA", new Guid("2bee661b-d1cd-6f9a-5ed6-758895dc21b8"), 50 },
                    { -9813L, "ANULADA", new Guid("2a41bfbd-16fb-ffbb-525a-c93e190a33f2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("130c4a73-840a-33a9-9481-ddc8f14d72e5"), 40 },
                    { -9812L, "RECHAZADA", new Guid("da0f071a-412b-8f30-e729-d930e3e1513f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazada", "RECHAZADA", "RECHAZADA", new Guid("1c0e3acd-a6ec-9fa9-1ee1-61ee8834b358"), 30 },
                    { -9811L, "AUTORIZADA", new Guid("6758a276-9938-b041-a007-f3c18b0756d8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Autorizada", "AUTORIZADA", "AUTORIZADA", new Guid("715de8b2-cfeb-c901-7220-9bcd95070e24"), 20 },
                    { -9810L, "SOLICITADA", new Guid("2c14b068-dd96-3b2e-80a8-835737bb1f7e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Solicitada", "SOLICITADA", "SOLICITADA", new Guid("ffadbda3-3e2c-6c7e-e157-a4ca456d478c"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_retirement_request_status_catalog_items__active_sort",
                table: "retirement_request_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_retirement_request_status_catalog_items__country_code",
                table: "retirement_request_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_retirement_request_status_catalog_items__public_id",
                table: "retirement_request_status_catalog_items",
                column: "public_id",
                unique: true);

            // D-15 data fix: rehire used to journal RECONTRATACION actions with the orphan status
            // "COMPLETADA" (never seeded in ActionStatus). Realign existing rows to the seeded APLICADA.
            migrationBuilder.Sql(
                "UPDATE personnel_file_personnel_actions SET action_status_code = 'APLICADA' WHERE action_status_code = 'COMPLETADA';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retirement_request_status_catalog_items");

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9483L);

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9482L);
        }
    }
}
