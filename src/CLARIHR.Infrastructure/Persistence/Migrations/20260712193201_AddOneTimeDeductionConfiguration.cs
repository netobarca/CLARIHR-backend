using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOneTimeDeductionConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "one_time_deduction_status_catalog_items",
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
                    table.PrimaryKey("pk_one_time_deduction_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_one_time_deduction_status_catalog_items_country_catalog_cou~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "one_time_deduction_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9944L, "ANULADO", new Guid("9c4865c4-25f1-9697-5e3b-7a3155d6e5a1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulado", "ANULADO", "ANULADO", new Guid("30747931-fe03-7dba-f42f-1be3c317b3d0"), 50 },
                    { -9943L, "APLICADO", new Guid("15598081-a784-f98f-d7c9-32e836369c08"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aplicado", "APLICADO", "APLICADO", new Guid("6a1662cd-e5e8-4d02-0240-413c2c19307b"), 40 },
                    { -9942L, "RECHAZADO", new Guid("db08345b-bfd7-7274-aa83-0746e71def7b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazado", "RECHAZADO", "RECHAZADO", new Guid("a473cb82-d1b3-2770-8a77-de1d2809992d"), 30 },
                    { -9941L, "AUTORIZADO", new Guid("dc16c0c2-c641-961b-b956-34adf08a4cd2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Autorizado", "AUTORIZADO", "AUTORIZADO", new Guid("0ce778c4-3253-56a8-ea43-596843118236"), 20 },
                    { -9940L, "EN_REVISION", new Guid("fb0f0f94-6020-5adc-7eda-6725e25fde28"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En revisión", "EN_REVISION", "EN REVISIÓN", new Guid("6ccd65a4-becc-8cf9-216c-fbb0ccf4a2cd"), 10 }
                });

            migrationBuilder.InsertData(
                table: "settlement_concept_catalog_items",
                columns: new[] { "id", "affects_afp", "affects_isss", "affects_renta", "code", "concept_class", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "default_rate_percent", "exemption_multiplier", "exemption_rule", "is_active", "is_system_calculated", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9945L, false, false, false, "DESCUENTO_EVENTUAL_PENDIENTE", "Descuento", new Guid("ea715750-be4c-79fd-c12c-c0c410adace8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Descuento eventual pendiente", "DESCUENTO_EVENTUAL_PENDIENTE", "DESCUENTO EVENTUAL PENDIENTE", new Guid("cf15050f-ddf3-f449-4143-e0d2ffcd6b42"), 136 });

            migrationBuilder.CreateIndex(
                name: "ix_one_time_deduction_status_catalog_items__country_active_sort",
                table: "one_time_deduction_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_one_time_deduction_status_catalog_items__country_code",
                table: "one_time_deduction_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_one_time_deduction_status_catalog_items__public_id",
                table: "one_time_deduction_status_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "one_time_deduction_status_catalog_items");

            migrationBuilder.DeleteData(
                table: "settlement_concept_catalog_items",
                keyColumn: "id",
                keyValue: -9945L);
        }
    }
}
