using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedEmploymentStatusCatalogForElSalvador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotency guard: this catalog was previously seeded per-country at runtime by DevSeedService
            // on fresh dev databases. Clear any such pre-existing SV rows so the canonical fixed-id seed below
            // inserts cleanly (no unique clash on country + normalized code). The table holds only reference
            // data — nothing references it by foreign key — so on a fresh or production database this is a
            // harmless no-op.
            migrationBuilder.Sql("DELETE FROM employment_status_catalog_items WHERE country_code = 'SV';");

            migrationBuilder.InsertData(
                table: "employment_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9104L, "RETIRADO", new Guid("b3e25e1c-5aaf-28f4-3489-fbddaf9aa982"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Retirado", "RETIRADO", "RETIRADO", new Guid("3bb07083-2fc1-9309-26ba-1ec4fae5e4c6"), 50 },
                    { -9103L, "INCAPACIDAD", new Guid("29f5810e-0e56-3fed-28aa-a4e96d8f7802"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Incapacidad", "INCAPACIDAD", "INCAPACIDAD", new Guid("cd8c2dc0-c49c-fa04-007e-f4c9efce6b81"), 40 },
                    { -9102L, "LICENCIA", new Guid("11447e54-0e54-c672-7cec-80d12845bf6f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Licencia", "LICENCIA", "LICENCIA", new Guid("b2bf570f-8494-c823-6fdc-7ec70f55e224"), 30 },
                    { -9101L, "SUSPENDIDO", new Guid("2104a7a7-9b59-4d77-1740-c4b7ba9f237b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Suspendido", "SUSPENDIDO", "SUSPENDIDO", new Guid("0972caa2-abf8-50a7-6a2f-412ffc98f22e"), 20 },
                    { -9100L, "ACTIVO", new Guid("c4d7577e-331a-5533-8b0b-b9a7cdd392dc"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Activo", "ACTIVO", "ACTIVO", new Guid("de6c6a77-a1d2-3e50-7606-712700c0198f"), 10 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "employment_status_catalog_items",
                keyColumn: "id",
                keyValue: -9104L);

            migrationBuilder.DeleteData(
                table: "employment_status_catalog_items",
                keyColumn: "id",
                keyValue: -9103L);

            migrationBuilder.DeleteData(
                table: "employment_status_catalog_items",
                keyColumn: "id",
                keyValue: -9102L);

            migrationBuilder.DeleteData(
                table: "employment_status_catalog_items",
                keyColumn: "id",
                keyValue: -9101L);

            migrationBuilder.DeleteData(
                table: "employment_status_catalog_items",
                keyColumn: "id",
                keyValue: -9100L);
        }
    }
}
