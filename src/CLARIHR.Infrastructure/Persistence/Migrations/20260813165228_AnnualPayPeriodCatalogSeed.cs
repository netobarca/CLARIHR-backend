using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnnualPayPeriodCatalogSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "pay_period_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9744L, "ANUAL", new Guid("8d8f161c-7c9b-ec89-672a-6f3c78103785"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anual", "ANUAL", "ANUAL", new Guid("47838a09-5700-2df9-9290-c59074f06045"), 50 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "pay_period_catalog_items",
                keyColumn: "id",
                keyValue: -9744L);
        }
    }
}
