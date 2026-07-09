using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOneTimeIncomeSettlementConcept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "settlement_concept_catalog_items",
                columns: new[] { "id", "affects_afp", "affects_isss", "affects_renta", "code", "concept_class", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "default_rate_percent", "exemption_multiplier", "exemption_rule", "is_active", "is_system_calculated", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9905L, true, true, true, "INGRESO_EVENTUAL_PENDIENTE", "Ingreso", new Guid("cf993e50-1006-175f-d9e7-1c0c9469e029"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso eventual pendiente", "INGRESO_EVENTUAL_PENDIENTE", "INGRESO EVENTUAL PENDIENTE", new Guid("236e0226-6053-2657-7c5b-af53b2395800"), 96 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "settlement_concept_catalog_items",
                keyColumn: "id",
                keyValue: -9905L);
        }
    }
}
