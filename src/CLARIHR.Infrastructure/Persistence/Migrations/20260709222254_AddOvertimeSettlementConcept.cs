using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeSettlementConcept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "settlement_concept_catalog_items",
                columns: new[] { "id", "affects_afp", "affects_isss", "affects_renta", "code", "concept_class", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "default_rate_percent", "exemption_multiplier", "exemption_rule", "is_active", "is_system_calculated", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9915L, true, true, true, "HORAS_EXTRAS_PENDIENTES_PAGO", "Ingreso", new Guid("4201fc96-1040-7adc-92e9-84ee43f8efde"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Horas extras pendientes de pago", "HORAS_EXTRAS_PENDIENTES_PAGO", "HORAS EXTRAS PENDIENTES DE PAGO", new Guid("445c862f-edf2-e9d5-05c7-58f35c2118bd"), 81 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "settlement_concept_catalog_items",
                keyColumn: "id",
                keyValue: -9915L);
        }
    }
}
