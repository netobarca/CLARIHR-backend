using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedGeneralCatalogsAndAddContractActionCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotency guard (matches the employment-statuses / assignment-types seed migrations): these 13
            // catalogs were previously seeded per-country at runtime by DevSeedService on dev databases. Clear any
            // such pre-existing SV rows so the canonical fixed-id HasData seed below inserts cleanly (no unique
            // clash on country + normalized code). These tables hold only reference data — nothing references them
            // by foreign key — so on a fresh or production database (empty tables) this is a harmless no-op.
            migrationBuilder.Sql("DELETE FROM currency_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM payment_method_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM substitution_type_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM asset_access_type_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM delivery_status_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM medical_claim_type_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM medical_claim_status_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM off_payroll_transaction_type_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM language_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM language_level_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM training_type_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM duration_unit_catalog_items WHERE country_code = 'SV';");
            migrationBuilder.Sql("DELETE FROM reference_type_catalog_items WHERE country_code = 'SV';");

            migrationBuilder.CreateTable(
                name: "action_status_catalog_items",
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
                    table.PrimaryKey("pk_action_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_action_status_catalog_items_country_catalog_country_catalog~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "action_type_catalog_items",
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
                    table.PrimaryKey("pk_action_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_action_type_catalog_items_country_catalog_country_catalog_i~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_type_catalog_items",
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
                    table.PrimaryKey("pk_contract_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_type_catalog_items_country_catalog_country_catalog~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "action_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9496L, "ANULADA", new Guid("495a2355-1751-66da-1cf6-f3083c84a17e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("472699e9-56d5-fd00-0160-43cd8f1edfe0"), 70 },
                    { -9495L, "APLICADA", new Guid("9686c7c7-bacc-79b4-07e0-1ec167656cf8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aplicada", "APLICADA", "APLICADA", new Guid("5dde1631-135f-a52b-9fad-3d76d3e0b077"), 60 },
                    { -9494L, "RECHAZADA", new Guid("17c3b370-9c8d-91dd-51a7-c3d9bca0b2ed"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazada", "RECHAZADA", "RECHAZADA", new Guid("fe1cc7a6-4493-0baf-6a44-1f16d86e5c92"), 50 },
                    { -9493L, "APROBADA", new Guid("6cd65a8e-36f2-9952-e6e5-981cbf7516da"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aprobada", "APROBADA", "APROBADA", new Guid("847f637d-5611-7c46-f287-426002137092"), 40 },
                    { -9492L, "EN_TRAMITE", new Guid("cc261a82-14e6-8d41-938f-07a1799946d9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En trámite", "EN_TRAMITE", "EN TRÁMITE", new Guid("3d2004c1-17b9-e08f-9e90-28371a83459c"), 30 },
                    { -9491L, "PENDIENTE", new Guid("88931ced-7030-f0b5-8652-4fc0f372f090"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pendiente", "PENDIENTE", "PENDIENTE", new Guid("f835464f-24d3-b770-f126-57c32e73470a"), 20 },
                    { -9490L, "BORRADOR", new Guid("d970316c-a54c-cc45-402f-9c1d1069e3b3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Borrador", "BORRADOR", "BORRADOR", new Guid("50703028-2cf2-b71f-8703-01cabd70ea21"), 10 }
                });

            migrationBuilder.InsertData(
                table: "action_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9481L, "OTRO", new Guid("9d0cb2c5-6da9-9d5e-2a5f-60da3d916902"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("b5efa4bd-f928-9487-68de-33c7de98dca5"), 120 },
                    { -9480L, "REINTEGRO", new Guid("2356699f-8f8e-00d0-62e3-ff93f89bc9c3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Reintegro", "REINTEGRO", "REINTEGRO", new Guid("ad110861-229f-ee36-2f58-8a3cf8fa3725"), 110 },
                    { -9479L, "PERMISO", new Guid("9fe54171-bd20-16fe-f525-f0d331a3f4c9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Permiso", "PERMISO", "PERMISO", new Guid("5ed01639-f81a-777a-a86b-faaae054ed26"), 100 },
                    { -9478L, "SUSPENSION", new Guid("680fca02-ec55-8e93-8633-e085aad9f850"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Suspensión", "SUSPENSION", "SUSPENSIÓN", new Guid("c0d5f5b3-d8b6-56af-7863-b32bc1151d51"), 90 },
                    { -9477L, "AMONESTACION", new Guid("0e338cd2-b23e-49bb-166b-28af405941e1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Amonestación", "AMONESTACION", "AMONESTACIÓN", new Guid("5f14f68c-60de-5933-666a-39c75bad78de"), 80 },
                    { -9476L, "AUMENTO_SALARIAL", new Guid("26c9d776-82cd-8c4b-940b-847c9760fc85"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aumento salarial", "AUMENTO_SALARIAL", "AUMENTO SALARIAL", new Guid("76713b08-0c88-403b-5ef5-30f263a9248b"), 70 },
                    { -9475L, "CAMBIO_PUESTO", new Guid("63ae5284-8672-a530-b18c-1f4545b86ae6"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cambio de puesto", "CAMBIO_PUESTO", "CAMBIO DE PUESTO", new Guid("8c166060-dad3-2242-b73f-1ad92e38aeee"), 60 },
                    { -9474L, "TRASLADO", new Guid("cfad6346-1213-ebe6-62eb-6c7113b24e47"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Traslado", "TRASLADO", "TRASLADO", new Guid("3856109f-cdf4-3fb8-9779-23159374e98a"), 50 },
                    { -9473L, "ASCENSO", new Guid("c007bdf6-935b-bab5-2a6a-3dd4bf9cabd7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ascenso", "ASCENSO", "ASCENSO", new Guid("ae8c1bd1-3b8e-8136-be12-be83eb1515ed"), 40 },
                    { -9472L, "RECONTRATACION", new Guid("c9c4c128-387b-c198-c5b2-e48e6916f949"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Recontratación", "RECONTRATACION", "RECONTRATACIÓN", new Guid("b761aa72-9118-861d-ae7e-edf54b4d610a"), 30 },
                    { -9471L, "CONTRATACION", new Guid("5bb0f4ea-0231-3431-d920-1788622f7d87"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contratación", "CONTRATACION", "CONTRATACIÓN", new Guid("0f600c9c-bfab-b238-0da8-7466f2c2539a"), 20 },
                    { -9470L, "NOMBRAMIENTO", new Guid("370be43b-60bb-654d-17f5-92c3b48f4bed"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nombramiento", "NOMBRAMIENTO", "NOMBRAMIENTO", new Guid("0a8e531b-ea96-b3d2-1a46-85a1054ce594"), 10 }
                });

            migrationBuilder.InsertData(
                table: "asset_access_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9307L, "OTRO", new Guid("6bbddb7c-2e0e-8775-53ae-2592d2385feb"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("29515eb5-10c5-dd5e-14b9-01c45f48410d"), 80 },
                    { -9306L, "HERRAMIENTA", new Guid("d86fc9a1-afa7-76b1-1eea-51a95a97a27f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Herramienta", "HERRAMIENTA", "HERRAMIENTA", new Guid("8315e712-ef90-aadd-29b0-62750febc8d7"), 70 },
                    { -9305L, "MOBILIARIO", new Guid("8734fb6c-0afa-8208-d645-3651f5c1bef1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mobiliario", "MOBILIARIO", "MOBILIARIO", new Guid("f6dd73dd-e6c8-5ec4-0bb9-9f58dc6dd342"), 60 },
                    { -9304L, "ACCESO_SISTEMA", new Guid("3a984369-1a08-b98e-0ba6-06cbb4000b4b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Acceso a sistema", "ACCESO_SISTEMA", "ACCESO A SISTEMA", new Guid("69ae6fbc-d706-e515-c688-244c30e66590"), 50 },
                    { -9303L, "LICENCIA_SOFTWARE", new Guid("1478e73f-0604-35db-cb3e-68a6278ac84b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Licencia de software", "LICENCIA_SOFTWARE", "LICENCIA DE SOFTWARE", new Guid("9acfbf68-7364-295d-09c3-f9cd851a1e1e"), 40 },
                    { -9302L, "UNIFORME", new Guid("d5c93b8b-07c8-f93f-6eef-23b6be1428bd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uniforme", "UNIFORME", "UNIFORME", new Guid("7168ccfc-effc-ae3e-ddd4-c64a8da6c7e3"), 30 },
                    { -9301L, "TELEFONO_MOVIL", new Guid("1251c4a6-8696-b3e4-a8b8-f0b58f7f9af2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Teléfono móvil", "TELEFONO_MOVIL", "TELÉFONO MÓVIL", new Guid("4e0f2c30-38dc-2879-44a6-eb2388d372df"), 20 },
                    { -9300L, "EQUIPO_COMPUTO", new Guid("342ad126-2c36-6d56-1fa9-8af131bfab2f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Equipo de cómputo", "EQUIPO_COMPUTO", "EQUIPO DE CÓMPUTO", new Guid("8fb09ca4-6df9-ba89-a2a5-5c64e07422be"), 10 }
                });

            migrationBuilder.InsertData(
                table: "contract_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9467L, "OTRO", new Guid("96fced9c-8f86-ea7a-7c33-6eda436dfdb2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("7fc887d5-5abd-0885-a1e3-a250b64da9a3"), 80 },
                    { -9466L, "TEMPORAL", new Guid("ad795c44-19f3-2413-8d73-6c0c5c910224"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato temporal", "TEMPORAL", "CONTRATO TEMPORAL", new Guid("52160e46-ead9-f877-6a3d-ad9f0f297a1e"), 70 },
                    { -9465L, "SERVICIOS_PROFESIONALES", new Guid("dbd9c16f-18ba-0ffc-419f-c7da125517c2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Servicios profesionales", "SERVICIOS_PROFESIONALES", "SERVICIOS PROFESIONALES", new Guid("19b43d5a-8821-3b2c-7289-1a8c661dcdae"), 60 },
                    { -9464L, "APRENDIZAJE", new Guid("79cbadbd-7173-6a1e-5d23-d570d5b9756d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato de aprendizaje", "APRENDIZAJE", "CONTRATO DE APRENDIZAJE", new Guid("6c821234-749b-cbb2-9699-4154011cfd35"), 50 },
                    { -9463L, "EVENTUAL", new Guid("6b3ed6c4-7448-9279-ab22-deee304190b2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato eventual", "EVENTUAL", "CONTRATO EVENTUAL", new Guid("8b7f1baa-b52d-b54f-118b-53bd746b3603"), 40 },
                    { -9462L, "POR_OBRA", new Guid("2abadeb3-1e68-5b6a-1c33-dcaed412a638"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato por obra o labor", "POR_OBRA", "CONTRATO POR OBRA O LABOR", new Guid("4961ad29-20db-5df3-e4e6-051bfb269d72"), 30 },
                    { -9461L, "PLAZO_FIJO", new Guid("4de7ec14-541a-1ac2-a767-a5b99c2692ef"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato a plazo fijo", "PLAZO_FIJO", "CONTRATO A PLAZO FIJO", new Guid("76005019-7602-8415-d97c-589aeb3e58e1"), 20 },
                    { -9460L, "INDEFINIDO", new Guid("fd78dd59-2c62-c10e-a26d-fec47f3f873c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contrato por tiempo indefinido", "INDEFINIDO", "CONTRATO POR TIEMPO INDEFINIDO", new Guid("abd24623-4603-ba3e-d649-d3924a12ff82"), 10 }
                });

            migrationBuilder.InsertData(
                table: "currency_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9370L, "USD", new Guid("a9dca1a5-d320-5c88-6195-18fe8c8c8528"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dolar estadounidense", "USD", "DOLAR ESTADOUNIDENSE", new Guid("e28875cc-cf00-49e5-5d19-3fe706a5a33f"), 10 });

            migrationBuilder.InsertData(
                table: "delivery_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9316L, "NO_APLICA", new Guid("30b39e52-3c3c-0334-78ad-6f38643e91c3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "No aplica", "NO_APLICA", "NO APLICA", new Guid("a0ee137e-d06a-0b7b-e629-3a10235bf2d3"), 70 },
                    { -9315L, "DANADO", new Guid("a3f9068e-b5a1-55cf-b2a7-c80e4eb40d39"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dañado", "DANADO", "DAÑADO", new Guid("29917f61-b0f5-e237-19a7-fd5898429470"), 60 },
                    { -9314L, "EXTRAVIADO", new Guid("4a7c5144-e797-e623-a11e-a4e2493b4a9d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Extraviado", "EXTRAVIADO", "EXTRAVIADO", new Guid("ef653ad5-b2c3-1147-06d6-40340b376edb"), 50 },
                    { -9313L, "DEVUELTO", new Guid("f89d5a12-aaff-9f32-d7d9-68e21b53c287"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Devuelto", "DEVUELTO", "DEVUELTO", new Guid("96aa0643-33d7-56ec-a9f4-b28931617173"), 40 },
                    { -9312L, "EN_USO", new Guid("7f6d7ccf-0971-1cbc-d066-6628275f9345"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En uso", "EN_USO", "EN USO", new Guid("1d69f4c2-bff9-055a-d443-04aa8fe5b397"), 30 },
                    { -9311L, "ENTREGADO", new Guid("2f0654f5-ed66-8c5b-9e30-04fd26f0b0fa"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Entregado", "ENTREGADO", "ENTREGADO", new Guid("c18e3849-6f14-f3af-3065-e3a3e9675323"), 20 },
                    { -9310L, "PENDIENTE", new Guid("3d038349-3765-7217-433b-3e386114d936"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pendiente", "PENDIENTE", "PENDIENTE", new Guid("3498b45d-abbe-a53f-03eb-3b4b08fdfe1a"), 10 }
                });

            migrationBuilder.InsertData(
                table: "duration_unit_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9441L, "DAY", new Guid("575089c0-3aa6-a95a-a110-6b27200d10c8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dia", "DAY", "DIA", new Guid("8033d7d6-d068-2842-95f8-353a2c80e415"), 20 },
                    { -9440L, "HOUR", new Guid("4f354ca3-5136-7ab5-3ffd-919455d3ef8b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hora", "HOUR", "HORA", new Guid("92875eed-2396-2098-0d54-2973ed47c1b0"), 10 }
                });

            migrationBuilder.InsertData(
                table: "language_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9411L, "SPANISH", new Guid("f3b96bf0-b8c7-9559-6929-7459788b46cd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Espanol", "SPANISH", "ESPANOL", new Guid("cf70307b-242b-2509-429d-dea44398eb47"), 20 },
                    { -9410L, "ENGLISH", new Guid("96ab34d5-fcb8-fa8d-1511-cd4fd4ceefcd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingles", "ENGLISH", "INGLES", new Guid("5fcab141-6fb9-d5d8-2ac4-1665345f6f02"), 10 }
                });

            migrationBuilder.InsertData(
                table: "language_level_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9422L, "BASIC", new Guid("4c851e57-0250-d6fd-cf77-723ce49e299b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Basico", "BASIC", "BASICO", new Guid("a49e08ba-12d1-3eb2-2692-06c94dccf5fa"), 30 },
                    { -9421L, "INTERMEDIATE", new Guid("ac4584bd-c669-4274-8a24-904c6b59a535"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Intermedio", "INTERMEDIATE", "INTERMEDIO", new Guid("d085e59c-1422-8600-7405-2f6880dc3356"), 20 },
                    { -9420L, "ADVANCED", new Guid("3ec88c6f-e5ef-07b5-c41f-71dea5068c8a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Avanzado", "ADVANCED", "AVANZADO", new Guid("0a2b671b-9980-4e0f-ea9f-29af315b4a77"), 10 }
                });

            migrationBuilder.InsertData(
                table: "medical_claim_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9357L, "ANULADO", new Guid("a48ceb78-d030-0d7e-27c5-ec314bb253f3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulado", "ANULADO", "ANULADO", new Guid("175fbd00-20e8-7eb0-ee3a-5b74e88a630d"), 80 },
                    { -9356L, "PAGO_PARCIAL", new Guid("a2285b2e-5bbe-2efd-161c-8af5d02ea9f4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pago parcial", "PAGO_PARCIAL", "PAGO PARCIAL", new Guid("dfb1ce5c-ee6b-46d6-0642-738cc15cd145"), 70 },
                    { -9355L, "PAGADO", new Guid("d81569ec-f540-cd7c-ef66-cfbfa248683c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pagado", "PAGADO", "PAGADO", new Guid("38c7e435-664c-7a91-0e7c-cb11b3fb43ab"), 60 },
                    { -9354L, "RECHAZADO", new Guid("ab8529da-0eee-0a19-0d82-f144dc15d7b3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazado", "RECHAZADO", "RECHAZADO", new Guid("b183ac10-86dd-8c3a-dcd7-41eab79976b8"), 50 },
                    { -9353L, "APROBADO", new Guid("99fab33c-451b-e26e-bf9a-11e4ccf8771e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aprobado", "APROBADO", "APROBADO", new Guid("e6a8fa47-4c1a-60b9-7afe-416d012587cf"), 40 },
                    { -9352L, "PENDIENTE_DOCUMENTACION", new Guid("3124391b-e26d-3cf5-3f02-eb1769811563"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pendiente de documentación", "PENDIENTE_DOCUMENTACION", "PENDIENTE DE DOCUMENTACIÓN", new Guid("2f7e2ccc-dbf1-0306-cf53-a6d9425d16ad"), 30 },
                    { -9351L, "EN_REVISION", new Guid("4d678eb1-4919-050f-51a1-6eb582d14e5f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En revisión", "EN_REVISION", "EN REVISIÓN", new Guid("fd84af05-eb48-ba6e-e25e-239d4fdcd9b8"), 20 },
                    { -9350L, "PRESENTADO", new Guid("667d9dec-5f55-dcc7-605c-d0f0b452b792"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Presentado", "PRESENTADO", "PRESENTADO", new Guid("4526d3d8-7c6a-f8ec-69b7-13fe0737dae7"), 10 }
                });

            migrationBuilder.InsertData(
                table: "medical_claim_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9348L, "OTRO", new Guid("cf6d3294-3595-e924-f9fb-12264c8869ee"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("d37747ff-3831-96c0-ad08-9d8f57e12664"), 90 },
                    { -9347L, "MATERNIDAD", new Guid("a715ae09-2c86-527a-2e80-6b61f8e5e415"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Maternidad", "MATERNIDAD", "MATERNIDAD", new Guid("8d395047-1463-9c53-fa16-b7ebb2231f60"), 80 },
                    { -9346L, "OFTALMOLOGICO", new Guid("19f86b0a-00f6-5c7c-5516-5b2314c703c3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Oftalmológico", "OFTALMOLOGICO", "OFTALMOLÓGICO", new Guid("4d946806-a4a4-d055-d9c4-190e34bdc1c7"), 70 },
                    { -9345L, "DENTAL", new Guid("d27b276e-a5fe-831c-d5f2-43ec4b852179"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dental", "DENTAL", "DENTAL", new Guid("1faf66f0-8051-544c-c46e-d81c5b6f35a5"), 60 },
                    { -9344L, "LABORATORIO", new Guid("b4f5b96c-27c4-d16a-6e07-a30edc825245"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Laboratorio", "LABORATORIO", "LABORATORIO", new Guid("51844c3e-2c8c-3d4f-6272-942a653ba192"), 50 },
                    { -9343L, "FARMACIA", new Guid("863d15f5-d289-e48b-cf9e-6936291f0f95"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Farmacia", "FARMACIA", "FARMACIA", new Guid("24fb23e7-3571-f662-66d6-916e45e5a19a"), 40 },
                    { -9342L, "EMERGENCIA", new Guid("9867cac4-5e9b-f389-617b-d9c32db352e9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Emergencia", "EMERGENCIA", "EMERGENCIA", new Guid("f837527c-0730-f90f-ad50-f04218dfce33"), 30 },
                    { -9341L, "HOSPITALARIO", new Guid("b3673c74-ebd8-27b8-1ffe-7fcf9adf3293"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hospitalario", "HOSPITALARIO", "HOSPITALARIO", new Guid("03882d1a-e77e-67ee-6580-4eca17279da1"), 20 },
                    { -9340L, "AMBULATORIO", new Guid("bce06a87-d957-5992-aae5-8064c4282c77"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ambulatorio", "AMBULATORIO", "AMBULATORIO", new Guid("70e5e364-876e-2083-0285-afbb6622477c"), 10 }
                });

            migrationBuilder.InsertData(
                table: "off_payroll_transaction_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9365L, "REGALOS", new Guid("8444fefe-a8f7-33ed-1d68-17f45e2c7779"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Regalos", "REGALOS", "REGALOS", new Guid("f7abaab0-d6aa-76ed-d532-90e278693859"), 60 },
                    { -9364L, "RECONOCIMIENTOS", new Guid("5a08f907-6867-9295-6a7e-cddd3f0b69fd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Reconocimientos", "RECONOCIMIENTOS", "RECONOCIMIENTOS", new Guid("43a5241d-662b-0586-4714-0109ea7c8f5e"), 50 },
                    { -9363L, "PROMOCIONALES", new Guid("37b4c765-6d33-2444-5df7-aaec694920ea"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Artículos promocionales", "PROMOCIONALES", "ARTÍCULOS PROMOCIONALES", new Guid("fb3d2297-bd5b-5aac-6c6c-869952e30d2a"), 40 },
                    { -9362L, "UNIFORMES", new Guid("ea65a30c-499d-a5d2-bc5b-dd58ed6983cd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uniformes", "UNIFORMES", "UNIFORMES", new Guid("3b106f4e-7b7b-1441-7e65-f7b47706e0b7"), 30 },
                    { -9361L, "EPP", new Guid("69406ddc-871a-cfb4-328c-5382234ae6fa"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Equipo de protección personal", "EPP", "EQUIPO DE PROTECCIÓN PERSONAL", new Guid("ed919a2d-a0e9-b689-4451-7e2b104bae1b"), 20 },
                    { -9360L, "HERRAMIENTAS", new Guid("5ddedbf4-9a38-c364-5ef4-7130d5124fe7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Herramientas de trabajo", "HERRAMIENTAS", "HERRAMIENTAS DE TRABAJO", new Guid("f1a5791e-176a-ed33-eab0-a9275594347c"), 10 }
                });

            migrationBuilder.InsertData(
                table: "payment_method_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9322L, "EFECTIVO", new Guid("a2e7bad9-530b-7be3-e0b5-11a2223ab805"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Efectivo", "EFECTIVO", "EFECTIVO", new Guid("7918b3d3-395e-0205-ca7a-63b0d6fc3e85"), 30 },
                    { -9321L, "CHEQUE", new Guid("6988801c-c4a9-edb2-c2e4-fa2c1e7d3767"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cheque", "CHEQUE", "CHEQUE", new Guid("671d3630-afb4-e6b5-6e59-f65a5fd5b80d"), 20 },
                    { -9320L, "TRANSFERENCIA", new Guid("c4c67543-475a-593e-1cf7-7b038e34250d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Transferencia bancaria", "TRANSFERENCIA", "TRANSFERENCIA BANCARIA", new Guid("d9ad9798-f964-5194-5ccd-a901a73ca9f9"), 10 }
                });

            migrationBuilder.InsertData(
                table: "reference_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9451L, "PROFESSIONAL", new Guid("1ca96cd1-1934-6f24-8fb5-76c52e070121"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Profesional", "PROFESSIONAL", "PROFESIONAL", new Guid("0a389591-968e-59cf-9325-1aae644e36f6"), 20 },
                    { -9450L, "PERSONAL", new Guid("3a1f1059-9e68-6616-01bd-2073ef9426b9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Personal", "PERSONAL", "PERSONAL", new Guid("591ebc0d-1883-83d0-4870-bad4a06233dd"), 10 }
                });

            migrationBuilder.InsertData(
                table: "substitution_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9335L, "OTRO", new Guid("7ac975b0-e03c-8ec4-9004-f74b86b635e5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("07b62aa9-fb35-863b-dbd9-3f7c5bd23cfc"), 60 },
                    { -9334L, "LICENCIA", new Guid("ee1045fa-2451-281d-8556-9b3d0fd11265"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Licencia", "LICENCIA", "LICENCIA", new Guid("468db47b-541d-7a02-3cfa-db54457dd194"), 50 },
                    { -9333L, "MISION_OFICIAL", new Guid("63214ec1-68be-c156-3672-31a2b4f7dcf5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Misión oficial", "MISION_OFICIAL", "MISIÓN OFICIAL", new Guid("d8af0122-8d8c-1b8a-764f-c6fdff3524bf"), 40 },
                    { -9332L, "PERMISO", new Guid("9a1bf89c-cb4e-675a-62cf-2f9babd00126"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Permiso", "PERMISO", "PERMISO", new Guid("c5c64c50-56ff-ad68-e67e-286baa390b31"), 30 },
                    { -9331L, "INCAPACIDAD", new Guid("58b57285-b240-6047-1004-6e972d3ee358"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Incapacidad", "INCAPACIDAD", "INCAPACIDAD", new Guid("441eaa82-03c3-f8c5-35ca-2e299d2ccaa3"), 20 },
                    { -9330L, "VACACIONES", new Guid("36fbeebc-99ea-7863-4b96-d02bd29cbf4e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vacaciones", "VACACIONES", "VACACIONES", new Guid("4f4eaf7a-9d20-9de8-16c4-e3ca9ce17a4a"), 10 }
                });

            migrationBuilder.InsertData(
                table: "training_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9432L, "CERTIFICATION", new Guid("6866fc2f-3969-fe45-36ec-56992c51a7a3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Certificacion", "CERTIFICATION", "CERTIFICACION", new Guid("8299c70a-64fe-60e9-9d08-96506de5c191"), 30 },
                    { -9431L, "WORKSHOP", new Guid("6c70c195-0697-741b-4961-92d88f685256"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Taller", "WORKSHOP", "TALLER", new Guid("95303da2-19c6-5066-2e48-e229517fef30"), 20 },
                    { -9430L, "COURSE", new Guid("7276a8c3-06b2-567a-214f-86475b05a7e6"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Curso", "COURSE", "CURSO", new Guid("886f8cf7-1951-47a3-0fbe-812d378ce11f"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_action_status_catalog_items__country_active_sort",
                table: "action_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_action_status_catalog_items__country_code",
                table: "action_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_action_status_catalog_items__public_id",
                table: "action_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_action_type_catalog_items__country_active_sort",
                table: "action_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_action_type_catalog_items__country_code",
                table: "action_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_action_type_catalog_items__public_id",
                table: "action_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contract_type_catalog_items__country_active_sort",
                table: "contract_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_contract_type_catalog_items__country_code",
                table: "contract_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_contract_type_catalog_items__public_id",
                table: "contract_type_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_status_catalog_items");

            migrationBuilder.DropTable(
                name: "action_type_catalog_items");

            migrationBuilder.DropTable(
                name: "contract_type_catalog_items");

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9307L);

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9306L);

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9305L);

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9304L);

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9303L);

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9302L);

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9301L);

            migrationBuilder.DeleteData(
                table: "asset_access_type_catalog_items",
                keyColumn: "id",
                keyValue: -9300L);

            migrationBuilder.DeleteData(
                table: "currency_catalog_items",
                keyColumn: "id",
                keyValue: -9370L);

            migrationBuilder.DeleteData(
                table: "delivery_status_catalog_items",
                keyColumn: "id",
                keyValue: -9316L);

            migrationBuilder.DeleteData(
                table: "delivery_status_catalog_items",
                keyColumn: "id",
                keyValue: -9315L);

            migrationBuilder.DeleteData(
                table: "delivery_status_catalog_items",
                keyColumn: "id",
                keyValue: -9314L);

            migrationBuilder.DeleteData(
                table: "delivery_status_catalog_items",
                keyColumn: "id",
                keyValue: -9313L);

            migrationBuilder.DeleteData(
                table: "delivery_status_catalog_items",
                keyColumn: "id",
                keyValue: -9312L);

            migrationBuilder.DeleteData(
                table: "delivery_status_catalog_items",
                keyColumn: "id",
                keyValue: -9311L);

            migrationBuilder.DeleteData(
                table: "delivery_status_catalog_items",
                keyColumn: "id",
                keyValue: -9310L);

            migrationBuilder.DeleteData(
                table: "duration_unit_catalog_items",
                keyColumn: "id",
                keyValue: -9441L);

            migrationBuilder.DeleteData(
                table: "duration_unit_catalog_items",
                keyColumn: "id",
                keyValue: -9440L);

            migrationBuilder.DeleteData(
                table: "language_catalog_items",
                keyColumn: "id",
                keyValue: -9411L);

            migrationBuilder.DeleteData(
                table: "language_catalog_items",
                keyColumn: "id",
                keyValue: -9410L);

            migrationBuilder.DeleteData(
                table: "language_level_catalog_items",
                keyColumn: "id",
                keyValue: -9422L);

            migrationBuilder.DeleteData(
                table: "language_level_catalog_items",
                keyColumn: "id",
                keyValue: -9421L);

            migrationBuilder.DeleteData(
                table: "language_level_catalog_items",
                keyColumn: "id",
                keyValue: -9420L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9357L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9356L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9355L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9354L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9353L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9352L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9351L);

            migrationBuilder.DeleteData(
                table: "medical_claim_status_catalog_items",
                keyColumn: "id",
                keyValue: -9350L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9348L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9347L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9346L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9345L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9344L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9343L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9342L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9341L);

            migrationBuilder.DeleteData(
                table: "medical_claim_type_catalog_items",
                keyColumn: "id",
                keyValue: -9340L);

            migrationBuilder.DeleteData(
                table: "off_payroll_transaction_type_catalog_items",
                keyColumn: "id",
                keyValue: -9365L);

            migrationBuilder.DeleteData(
                table: "off_payroll_transaction_type_catalog_items",
                keyColumn: "id",
                keyValue: -9364L);

            migrationBuilder.DeleteData(
                table: "off_payroll_transaction_type_catalog_items",
                keyColumn: "id",
                keyValue: -9363L);

            migrationBuilder.DeleteData(
                table: "off_payroll_transaction_type_catalog_items",
                keyColumn: "id",
                keyValue: -9362L);

            migrationBuilder.DeleteData(
                table: "off_payroll_transaction_type_catalog_items",
                keyColumn: "id",
                keyValue: -9361L);

            migrationBuilder.DeleteData(
                table: "off_payroll_transaction_type_catalog_items",
                keyColumn: "id",
                keyValue: -9360L);

            migrationBuilder.DeleteData(
                table: "payment_method_catalog_items",
                keyColumn: "id",
                keyValue: -9322L);

            migrationBuilder.DeleteData(
                table: "payment_method_catalog_items",
                keyColumn: "id",
                keyValue: -9321L);

            migrationBuilder.DeleteData(
                table: "payment_method_catalog_items",
                keyColumn: "id",
                keyValue: -9320L);

            migrationBuilder.DeleteData(
                table: "reference_type_catalog_items",
                keyColumn: "id",
                keyValue: -9451L);

            migrationBuilder.DeleteData(
                table: "reference_type_catalog_items",
                keyColumn: "id",
                keyValue: -9450L);

            migrationBuilder.DeleteData(
                table: "substitution_type_catalog_items",
                keyColumn: "id",
                keyValue: -9335L);

            migrationBuilder.DeleteData(
                table: "substitution_type_catalog_items",
                keyColumn: "id",
                keyValue: -9334L);

            migrationBuilder.DeleteData(
                table: "substitution_type_catalog_items",
                keyColumn: "id",
                keyValue: -9333L);

            migrationBuilder.DeleteData(
                table: "substitution_type_catalog_items",
                keyColumn: "id",
                keyValue: -9332L);

            migrationBuilder.DeleteData(
                table: "substitution_type_catalog_items",
                keyColumn: "id",
                keyValue: -9331L);

            migrationBuilder.DeleteData(
                table: "substitution_type_catalog_items",
                keyColumn: "id",
                keyValue: -9330L);

            migrationBuilder.DeleteData(
                table: "training_type_catalog_items",
                keyColumn: "id",
                keyValue: -9432L);

            migrationBuilder.DeleteData(
                table: "training_type_catalog_items",
                keyColumn: "id",
                keyValue: -9431L);

            migrationBuilder.DeleteData(
                table: "training_type_catalog_items",
                keyColumn: "id",
                keyValue: -9430L);
        }
    }
}
