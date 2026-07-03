using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalTitleAndAddressTypeCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "personal_title_code",
                table: "personnel_files",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_type_code",
                table: "personnel_file_addresses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "address_type_catalog_items",
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
                    table.PrimaryKey("pk_address_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_address_type_catalog_items_country_catalog_country_catalog_~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personal_title_catalog_items",
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
                    table.PrimaryKey("pk_personal_title_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_personal_title_catalog_items_country_catalog_country_catalo~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "address_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9624L, "OTRA", new Guid("92b686b3-0adf-88d0-4460-a36673fe6aea"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otra", "OTRA", "OTRA", new Guid("7f50ef28-8221-aae0-fdb5-5e2350165cd5"), 50 },
                    { -9623L, "TEMPORAL", new Guid("5917e21d-0033-6a95-7593-f8173ef2bdf7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Temporal", "TEMPORAL", "TEMPORAL", new Guid("585c0364-1688-a2c1-0daf-98e8481e7a3c"), 40 },
                    { -9622L, "FACTURACION", new Guid("1f565487-062b-0728-838e-f50b602f27f3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Facturación", "FACTURACION", "FACTURACIÓN", new Guid("ff8f9c9d-f42e-57e0-c903-d8a45441b038"), 30 },
                    { -9621L, "TRABAJO", new Guid("c29d5501-1b04-7a66-e512-4e4ac7e961ad"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Trabajo", "TRABAJO", "TRABAJO", new Guid("79e07032-8e23-b57b-0b5d-717f782a1810"), 20 },
                    { -9620L, "CASA", new Guid("e6dbba53-38be-9d00-079f-4fbe5683d2f0"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Casa / Habitación", "CASA", "CASA / HABITACIÓN", new Guid("2fa34acc-adf8-a342-8162-4b40943f0b3f"), 10 }
                });

            migrationBuilder.InsertData(
                table: "personal_title_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9611L, "OTRO", new Guid("7f752f3e-5807-9279-f70a-e9eae2dace20"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("d7ea6c25-2d95-5673-e62a-50f0ca1828ef"), 120 },
                    { -9610L, "SRTA", new Guid("9a21116a-7182-2abd-a1c6-177800d60925"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Señorita", "SRTA", "SEÑORITA", new Guid("96bb078d-5047-c39e-94bc-135a00bcae59"), 110 },
                    { -9609L, "SRA", new Guid("2d43f3dd-6b54-783c-d505-e4ad71fedc8b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Señora", "SRA", "SEÑORA", new Guid("d9e89ada-a9a9-f0fe-5ecb-2acf6fb08c03"), 100 },
                    { -9608L, "SR", new Guid("06ee8e2c-d02a-f63b-42f1-011a123e38b7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Señor", "SR", "SEÑOR", new Guid("132d0723-1a3f-9447-deb2-ee7d9540b787"), 90 },
                    { -9607L, "PROF", new Guid("bc322b76-8d40-e3ec-ebdb-28ba5b0c08ac"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Profesor/a", "PROF", "PROFESOR/A", new Guid("2d247c07-23f8-e0ee-a96e-908c11b74b5d"), 80 },
                    { -9606L, "TEC", new Guid("569f13f0-d6da-5846-5bce-5b32d5443a72"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Técnico/a", "TEC", "TÉCNICO/A", new Guid("57f1a17a-3bc6-afb5-e31c-4e8451e314e4"), 70 },
                    { -9605L, "MSC", new Guid("ee561806-b95e-0c06-e14c-a39e2cf7a596"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Máster", "MSC", "MÁSTER", new Guid("0495e165-077e-6e6f-4829-0c56116902ae"), 60 },
                    { -9604L, "DRA", new Guid("a985678a-f488-c7c8-9015-3c874614871c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Doctora", "DRA", "DOCTORA", new Guid("a4362367-8d71-28c5-15d0-5e26c64803e0"), 50 },
                    { -9603L, "DR", new Guid("cadf579d-5642-6a8a-10df-9eda310c0e1c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Doctor", "DR", "DOCTOR", new Guid("d066db5e-6307-7502-64e5-83f855675eb0"), 40 },
                    { -9602L, "ARQ", new Guid("15ee8b23-f1ed-bf13-d715-1acc5b3faa5f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Arquitecto/a", "ARQ", "ARQUITECTO/A", new Guid("22c5f416-af51-69a8-0322-b857c8885005"), 30 },
                    { -9601L, "LIC", new Guid("0d678cef-f100-e0cc-fee1-f980ed7b4428"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Licenciado/a", "LIC", "LICENCIADO/A", new Guid("eebc1c01-59ec-d3c5-f38c-6c87358e8038"), 20 },
                    { -9600L, "ING", new Guid("dbbe1114-a031-370b-578c-18aa6746c4cc"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingeniero/a", "ING", "INGENIERO/A", new Guid("94864ec0-a6ed-c168-7ebc-f53cd53d956f"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_address_type_catalog_items__country_active_sort",
                table: "address_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_address_type_catalog_items__country_code",
                table: "address_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_address_type_catalog_items__public_id",
                table: "address_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personal_title_catalog_items__country_active_sort",
                table: "personal_title_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_personal_title_catalog_items__country_code",
                table: "personal_title_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personal_title_catalog_items__public_id",
                table: "personal_title_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "address_type_catalog_items");

            migrationBuilder.DropTable(
                name: "personal_title_catalog_items");

            migrationBuilder.DropColumn(
                name: "personal_title_code",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "address_type_code",
                table: "personnel_file_addresses");
        }
    }
}
