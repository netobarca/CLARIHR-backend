using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringIncomeSettlementConcept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "settlement_concept_catalog_items",
                columns: new[] { "id", "affects_afp", "affects_isss", "affects_renta", "code", "concept_class", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "default_rate_percent", "exemption_multiplier", "exemption_rule", "is_active", "is_system_calculated", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9888L, true, true, true, "INGRESO_CICLICO_PENDIENTE", "Ingreso", new Guid("60286c87-e587-69db-6a6d-2d753b08f665"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso cíclico pendiente", "INGRESO_CICLICO_PENDIENTE", "INGRESO CÍCLICO PENDIENTE", new Guid("91f16c81-27b2-7290-f475-f8dbc7669db5"), 95 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "settlement_concept_catalog_items",
                keyColumn: "id",
                keyValue: -9888L);
        }
    }
}
