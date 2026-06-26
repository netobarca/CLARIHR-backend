using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormControlTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "form_control_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    value_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    supports_options = table.Column<bool>(type: "boolean", nullable: false),
                    supports_range = table.Column<bool>(type: "boolean", nullable: false),
                    supports_multiple = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_form_control_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_form_control_type_catalog_items_country_catalog_country_cat~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "form_control_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order", "supports_multiple", "supports_options", "supports_range", "value_kind" },
                values: new object[,]
                {
                    { -9268L, "ESCALA", new Guid("4a7daff8-993b-2c38-32ac-1230c772ae1d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Escala", "ESCALA", "ESCALA", new Guid("8b14226c-1478-fbac-0253-aba39be665de"), 90, false, false, true, "Number" },
                    { -9267L, "CASILLA", new Guid("798062f7-e818-dbf8-3fbf-643e8d795307"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Casilla (Sí/No)", "CASILLA", "CASILLA (SÍ/NO)", new Guid("44c31754-fc3f-eed9-2338-36ab6f3a3b37"), 80, false, false, false, "Boolean" },
                    { -9266L, "SELECCION_MULTIPLE", new Guid("46d65b02-c934-02af-4cd8-c668d82aab5c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Selección múltiple", "SELECCION_MULTIPLE", "SELECCIÓN MÚLTIPLE", new Guid("f7313862-11e5-d1a2-a242-8da00acbb2e9"), 70, true, true, false, "Options" },
                    { -9265L, "OPCION_UNICA", new Guid("c24e5470-87ef-e48f-e55f-9256ba33298f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Opción única", "OPCION_UNICA", "OPCIÓN ÚNICA", new Guid("30f98686-ff71-4cc0-1ab1-711ff8b70793"), 60, false, true, false, "Options" },
                    { -9264L, "LISTA_DESPLEGABLE", new Guid("c067e207-2180-a37e-26f9-0f2fe8d49a28"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lista desplegable", "LISTA_DESPLEGABLE", "LISTA DESPLEGABLE", new Guid("f1c7a038-5673-c411-f382-68252bdd073d"), 50, false, true, false, "Options" },
                    { -9263L, "FECHA", new Guid("ab9d910e-0708-8103-a997-d4b26d681e51"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fecha", "FECHA", "FECHA", new Guid("a135837e-9e9a-dc2e-26f2-daafc400340f"), 40, false, false, false, "Date" },
                    { -9262L, "NUMERO", new Guid("bfead33d-8453-eb7d-b6bf-ab8f43f5f58e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Número", "NUMERO", "NÚMERO", new Guid("c6ca6a89-504e-0c43-3889-724af2607f51"), 30, false, false, true, "Number" },
                    { -9261L, "TEXTO_LARGO", new Guid("0d1aa490-1d50-71b3-4512-3ab8a1bbe356"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Texto largo", "TEXTO_LARGO", "TEXTO LARGO", new Guid("ee874286-80b1-64ca-01c8-1d4753129947"), 20, false, false, false, "Text" },
                    { -9260L, "TEXTO_CORTO", new Guid("307dfbc7-fd50-93e0-68f3-12ca8946e990"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Texto corto", "TEXTO_CORTO", "TEXTO CORTO", new Guid("b1d1a832-0ea5-503a-35a5-fe95db31445f"), 10, false, false, false, "Text" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_form_control_type_catalog_items__country_active_sort",
                table: "form_control_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_form_control_type_catalog_items__country_code",
                table: "form_control_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_form_control_type_catalog_items__public_id",
                table: "form_control_type_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_control_type_catalog_items");
        }
    }
}
