using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonnelKinshipCatalogAndFamilyMemberKinshipCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "relationship",
                table: "personnel_file_family_members",
                newName: "kinship_code");

            migrationBuilder.InsertData(
                table: "personnel_reference_catalog_items",
                columns: new[]
                {
                    "id", "category", "code", "country_code", "created_utc", "is_active", "modified_utc", "name",
                    "normalized_code", "normalized_name", "parent_id", "public_id", "sort_order"
                },
                values: new object[,]
                {
                    { -9603L, "Kinship", "CONYUGE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Conyuge", "CONYUGE", "CONYUGE", null, new Guid("e40d1b19-815a-eccc-7e79-f0334f41f832"), 10 },
                    { -9604L, "Kinship", "PAREJA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pareja", "PAREJA", "PAREJA", null, new Guid("61b6bf8d-674a-dd8e-1765-1664dc818329"), 20 },
                    { -9605L, "Kinship", "PADRE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Padre", "PADRE", "PADRE", null, new Guid("acb3b1cb-4384-9a9a-46d6-e81ee37d289a"), 30 },
                    { -9606L, "Kinship", "MADRE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Madre", "MADRE", "MADRE", null, new Guid("a6777218-fbfa-ec8a-32e7-6ff044a6245d"), 40 },
                    { -9607L, "Kinship", "HIJO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hijo/a", "HIJO_A", "HIJO/A", null, new Guid("742bce7c-992a-f4ed-a454-d36cf9dbe362"), 50 },
                    { -9608L, "Kinship", "HERMANO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hermano/a", "HERMANO_A", "HERMANO/A", null, new Guid("a2a08702-b2de-293f-ee57-b0c3b01a45a9"), 60 },
                    { -9609L, "Kinship", "ABUELO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Abuelo/a", "ABUELO_A", "ABUELO/A", null, new Guid("88b3c1c4-44de-f9fc-c681-449829ae8b7e"), 70 },
                    { -9610L, "Kinship", "NIETO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nieto/a", "NIETO_A", "NIETO/A", null, new Guid("3b3b848c-c640-6aac-9f92-a143bd8f0afe"), 80 },
                    { -9611L, "Kinship", "TIO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tio/a", "TIO_A", "TIO/A", null, new Guid("a5cad122-7a87-e36c-7b6e-24530e947161"), 90 },
                    { -9612L, "Kinship", "OTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", null, new Guid("f488c015-109f-d1fc-5d91-2270ae305efe"), 100 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9603L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9604L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9605L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9606L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9607L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9608L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9609L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9610L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9611L);

            migrationBuilder.DeleteData(
                table: "personnel_reference_catalog_items",
                keyColumn: "id",
                keyValue: -9612L);

            migrationBuilder.RenameColumn(
                name: "kinship_code",
                table: "personnel_file_family_members",
                newName: "relationship");
        }
    }
}
