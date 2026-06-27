using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAssignmentTypeCatalogForElSalvador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "assignment_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9148L, "RECARGO_FUNCIONES", new Guid("ae59204d-f5d5-1262-e890-af9c8d4055ae"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Recargo de funciones", "RECARGO_FUNCIONES", "RECARGO DE FUNCIONES", new Guid("43838bcc-f194-b51f-d4d8-86847325a3f1"), 90 },
                    { -9147L, "SERVICIOS_PROFESIONALES", new Guid("d84f9074-1955-5000-0e83-5978d30228f2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Servicios profesionales", "SERVICIOS_PROFESIONALES", "SERVICIOS PROFESIONALES", new Guid("b862d3f9-d6ca-5ba7-9f61-b2691fc11a31"), 80 },
                    { -9146L, "AD_HONOREM", new Guid("da12556d-8cfc-9291-bcd3-b0014536b5db"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ad honorem", "AD_HONOREM", "AD HONOREM", new Guid("d7a45963-017b-c64e-1e91-0076453076bb"), 70 },
                    { -9145L, "POR_OBRA", new Guid("7b6f6095-aea9-4a99-70d9-18ae8129a57c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Por obra o servicio", "POR_OBRA", "POR OBRA O SERVICIO", new Guid("2ebfb001-8bbb-086d-0876-1cca9d97058e"), 60 },
                    { -9144L, "INTERINO", new Guid("92087d0b-f28b-5be1-1cca-1dcffd6064cb"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Interinato", "INTERINO", "INTERINATO", new Guid("fc075bde-b051-3b9e-919f-d7729bf3d5a7"), 50 },
                    { -9143L, "PLAZO_FIJO", new Guid("458b8ec7-f514-f6c8-5d28-eac05db3ed6e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Plazo fijo", "PLAZO_FIJO", "PLAZO FIJO", new Guid("d810b4f4-01ec-48ae-27f1-eadc9c44f608"), 40 },
                    { -9142L, "INDEFINIDO", new Guid("b227c823-96d4-cd05-9986-1782629d2e0b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tiempo indefinido", "INDEFINIDO", "TIEMPO INDEFINIDO", new Guid("63bbcafe-9c41-beee-0cde-8d4da17cf671"), 30 },
                    { -9141L, "CONTRATO", new Guid("95bfda59-2e40-5e82-1ed9-d4d8e963f257"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato", "CONTRATO", "CONTRATO", new Guid("31fc1630-4656-edee-99d5-aa0303b73256"), 20 },
                    { -9140L, "LEY_SALARIOS", new Guid("10c15bde-e3a2-3cf3-6c37-9cda3d2e5415"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ley de Salarios", "LEY_SALARIOS", "LEY DE SALARIOS", new Guid("22165e5a-a15e-3a37-b995-8c65bf6807c3"), 10 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9148L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9147L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9146L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9145L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9144L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9143L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9142L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9141L);

            migrationBuilder.DeleteData(
                table: "assignment_type_catalog_items",
                keyColumn: "id",
                keyValue: -9140L);
        }
    }
}
