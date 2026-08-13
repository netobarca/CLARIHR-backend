using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H22DocumentTypeCatalogBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "document_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9989L, "OTRO", new Guid("57545f1c-5056-f299-78c0-587bd8c306aa"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("1ea14b4c-24cd-533c-629d-e61fa748666f"), 120 },
                    { -9988L, "RESPALDO", new Guid("0390f696-f45d-55f6-e65f-2dbd3bd01e63"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Respaldo", "RESPALDO", "RESPALDO", new Guid("ad1359db-9dfd-1b99-475c-5c923860fa90"), 110 },
                    { -9987L, "IDENTIFICACION", new Guid("bc32c1e7-fdbc-f198-1892-9499e2d1e7da"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Copia de documento de identidad", "IDENTIFICACION", "COPIA DE DOCUMENTO DE IDENTIDAD", new Guid("249350fa-24db-9545-4ab3-c98e279b8d3c"), 100 },
                    { -9986L, "CURRICULUM", new Guid("3a814008-c1fc-7e95-9a6e-911ae37a2551"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Currículum", "CURRICULUM", "CURRÍCULUM", new Guid("51377591-fd3d-62a1-2e48-727ab919c715"), 90 },
                    { -9985L, "TITULO", new Guid("3772f170-95f1-98c7-3e1a-42ef56f1249e"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Título o diploma", "TITULO", "TÍTULO O DIPLOMA", new Guid("e789df3a-4e67-60a7-561a-48ab9f9fd259"), 80 },
                    { -9984L, "CARTA", new Guid("f6f1c64d-212e-4ebe-1ba6-c5fbe886a437"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Carta o nota", "CARTA", "CARTA O NOTA", new Guid("e2edfc08-a09a-e1bb-d2ab-9c44bae2fb48"), 70 },
                    { -9983L, "CONTRATO", new Guid("34a4ccce-a10b-4bf3-d5f7-f3044f45aa54"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato", "CONTRATO", "CONTRATO", new Guid("9ebb23e9-8189-43e9-2f1b-626dad2b8638"), 60 },
                    { -9982L, "RECIBO", new Guid("0d268701-b2bd-07a0-56fe-4dd366594394"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Recibo", "RECIBO", "RECIBO", new Guid("f700a1c4-70d7-96ac-e753-f88f60711bf8"), 50 },
                    { -9981L, "FACTURA", new Guid("9a8a28b9-2bb2-fdfb-897d-debea2cf4290"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Factura", "FACTURA", "FACTURA", new Guid("9a932f58-f05a-b9a1-8040-16abb58611d5"), 40 },
                    { -9980L, "RECETA", new Guid("5a57686a-b4bc-1b42-7037-b6b19825d6ba"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Receta o indicación médica", "RECETA", "RECETA O INDICACIÓN MÉDICA", new Guid("44f11e25-887a-a349-bd32-971dba61b637"), 30 },
                    { -9979L, "INCAPACIDAD", new Guid("6829046d-f1f7-38df-d9f2-1eef29dc38fc"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Incapacidad ISSS", "INCAPACIDAD", "INCAPACIDAD ISSS", new Guid("b6fc2b45-601c-473b-16cb-411200b4951d"), 20 },
                    { -9978L, "CONSTANCIA_MEDICA", new Guid("2b49d8b6-7796-c15e-c015-75296eef3bae"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Constancia médica", "CONSTANCIA_MEDICA", "CONSTANCIA MÉDICA", new Guid("04bb4db5-37b5-d501-4e15-4319d0a6df43"), 10 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9989L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9988L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9987L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9986L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9985L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9984L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9983L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9982L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9981L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9980L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9979L);

            migrationBuilder.DeleteData(
                table: "document_type_catalog_items",
                keyColumn: "id",
                keyValue: -9978L);
        }
    }
}
