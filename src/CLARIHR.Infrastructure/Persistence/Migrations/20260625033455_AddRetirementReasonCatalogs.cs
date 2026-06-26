using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetirementReasonCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-11: no legacy retirement data is migrated — any pre-existing free-text "motivo de baja"
            // (test data) is cleared so the now-validated codes start clean.
            migrationBuilder.Sql(
                "UPDATE personnel_file_employee_profiles SET retirement_category_code = NULL, retirement_reason_code = NULL " +
                "WHERE retirement_category_code IS NOT NULL OR retirement_reason_code IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "retirement_category_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    separation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("pk_retirement_category_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_retirement_category_catalog_items_country_catalog_country_c~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "retirement_reason_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    retirement_category_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_retirement_reason_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_retirement_reason_catalog_items_country_catalog_country_cat~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_retirement_reason_catalog_items__category",
                        column: x => x.retirement_category_catalog_item_id,
                        principalTable: "retirement_category_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "retirement_category_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "separation_type", "sort_order" },
                values: new object[,]
                {
                    { -9207L, "FALLECIMIENTO", new Guid("42276497-8086-570e-950f-cc26d2291cfb"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fallecimiento", "FALLECIMIENTO", "FALLECIMIENTO", new Guid("a7f2569d-22a7-6ef3-d9e8-e2800bee9f65"), "Otra", 80 },
                    { -9206L, "MUTUO_ACUERDO", new Guid("f092a152-d7c3-c5a3-dbe7-b8629849bdc7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mutuo acuerdo", "MUTUO_ACUERDO", "MUTUO ACUERDO", new Guid("0ae8dc61-a858-caa4-f231-d0ac0485e3bd"), "Otra", 70 },
                    { -9205L, "FIN_CONTRATO", new Guid("e01c2f39-ebf4-c21e-09b3-06eb3a3d613e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fin de contrato", "FIN_CONTRATO", "FIN DE CONTRATO", new Guid("664a49fd-6234-e264-3e59-d1a39b806701"), "Otra", 60 },
                    { -9204L, "NO_SUPERA_PERIODO_PRUEBA", new Guid("238f6f5e-ba5c-4a41-9454-a7ebf0a69fa2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "No supera período de prueba", "NO_SUPERA_PERIODO_PRUEBA", "NO SUPERA PERÍODO DE PRUEBA", new Guid("e59ca395-6d36-6d4f-b38e-9cba531b3f50"), "Involuntaria", 50 },
                    { -9203L, "ABANDONO", new Guid("44e58f01-08c0-2804-6efb-3dbbb668d3d8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Abandono de trabajo", "ABANDONO", "ABANDONO DE TRABAJO", new Guid("86a735f5-38ce-04c8-7d8e-e6f57ef29a63"), "Involuntaria", 40 },
                    { -9202L, "INVOLUNTARIA", new Guid("461166ba-cda0-add0-a17b-a36004199db2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Despido / involuntaria", "INVOLUNTARIA", "DESPIDO / INVOLUNTARIA", new Guid("45ccc19b-4537-07b3-d235-df3deeb701e7"), "Involuntaria", 30 },
                    { -9201L, "JUBILACION", new Guid("0286d7d7-4ece-142a-e99f-b929b3d0a0d8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jubilación", "JUBILACION", "JUBILACIÓN", new Guid("4ee17bee-1013-11c1-fd58-984cc73b6a05"), "Voluntaria", 20 },
                    { -9200L, "VOLUNTARIA", new Guid("e09e5552-6df8-f240-d11b-6d166fbbad01"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Renuncia voluntaria", "VOLUNTARIA", "RENUNCIA VOLUNTARIA", new Guid("be2d0a01-e392-1d51-92f8-7cd10f25e9ed"), "Voluntaria", 10 }
                });

            migrationBuilder.InsertData(
                table: "retirement_reason_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "retirement_category_catalog_item_id", "sort_order" },
                values: new object[,]
                {
                    { -9242L, "FALLECIMIENTO", new Guid("361b1df0-9d7a-ca72-8cdb-ef2566c928ef"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fallecimiento", "FALLECIMIENTO", "FALLECIMIENTO", new Guid("bdced7a5-08c9-ad22-a97c-38721400dd52"), -9207L, 10 },
                    { -9241L, "MUTUO_ACUERDO", new Guid("aa8a26ca-abe4-b7f4-e2ef-a7ce9c0844ce"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mutuo acuerdo", "MUTUO_ACUERDO", "MUTUO ACUERDO", new Guid("384fc4ff-89a7-d167-1895-7242b649b726"), -9206L, 10 },
                    { -9240L, "FIN_OBRA_PROYECTO", new Guid("1c039c9e-de78-b115-dd0f-7485a78bfa07"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fin de obra o proyecto", "FIN_OBRA_PROYECTO", "FIN DE OBRA O PROYECTO", new Guid("27333639-bf58-fa82-309e-e9cb79668eb4"), -9205L, 20 },
                    { -9239L, "FIN_CONTRATO_TEMPORAL", new Guid("70434914-440d-b47a-3c79-b44ab9080454"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fin de contrato temporal", "FIN_CONTRATO_TEMPORAL", "FIN DE CONTRATO TEMPORAL", new Guid("f35baa16-5a29-6513-7e40-59e9f7b12f08"), -9205L, 10 },
                    { -9238L, "NO_SUPERA_PRUEBA", new Guid("f82b5fc9-a587-9b34-c9f6-2bc79389a127"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "No superó el período de prueba", "NO_SUPERA_PRUEBA", "NO SUPERÓ EL PERÍODO DE PRUEBA", new Guid("b1da9b96-f2f5-68db-2f93-9fede21581cc"), -9204L, 10 },
                    { -9237L, "ABANDONO_TRABAJO", new Guid("74ef4198-586d-e960-4a1b-fefa1a4cd53f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Abandono de trabajo", "ABANDONO_TRABAJO", "ABANDONO DE TRABAJO", new Guid("f2959359-a832-fc3b-95f6-50d823df9b07"), -9203L, 10 },
                    { -9236L, "RECORTE_PRESUPUESTARIO", new Guid("c782ae26-d889-5bb5-aa1d-84efe018c138"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Recorte presupuestario", "RECORTE_PRESUPUESTARIO", "RECORTE PRESUPUESTARIO", new Guid("34b6a819-e658-82ac-bb9b-052651525d5b"), -9202L, 60 },
                    { -9235L, "INCUMPLIMIENTO_POLITICAS", new Guid("d054f820-a82f-b242-8235-961124272541"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Incumplimiento de políticas", "INCUMPLIMIENTO_POLITICAS", "INCUMPLIMIENTO DE POLÍTICAS", new Guid("89e51319-21cf-98e8-fa49-cf36d3b8e438"), -9202L, 50 },
                    { -9234L, "AUSENTISMO", new Guid("ba8bd617-5a33-e824-14b1-f7e9d06ed319"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ausentismo", "AUSENTISMO", "AUSENTISMO", new Guid("32422adb-954e-8fdb-4a92-af5755473134"), -9202L, 40 },
                    { -9233L, "FALTA_DISCIPLINARIA", new Guid("39a4c4b6-7129-6d75-8a78-f7e3b0a9a5e5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Falta disciplinaria", "FALTA_DISCIPLINARIA", "FALTA DISCIPLINARIA", new Guid("f07c2dc9-28fd-0c6f-6a21-665a79e9f715"), -9202L, 30 },
                    { -9232L, "REESTRUCTURACION", new Guid("8f4ae07c-dd2c-6ff0-1ec1-06a2e88bf521"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Reestructuración", "REESTRUCTURACION", "REESTRUCTURACIÓN", new Guid("ae057cf0-5dad-9bff-607e-4a2e5d9d4d42"), -9202L, 20 },
                    { -9231L, "BAJO_DESEMPENO", new Guid("0541bd31-4356-cb55-6e5a-305feba39b7a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bajo desempeño", "BAJO_DESEMPENO", "BAJO DESEMPEÑO", new Guid("7fcd37e5-9f79-2420-1f87-1b2c70530e9f"), -9202L, 10 },
                    { -9230L, "JUBILACION_EDAD", new Guid("1edffa0a-6476-a6f8-e26d-426a91aca36c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jubilación por edad", "JUBILACION_EDAD", "JUBILACIÓN POR EDAD", new Guid("0281449b-4dee-ac64-f2d2-3a413d216c31"), -9201L, 10 },
                    { -9229L, "INSATISFACCION_FUNCIONES", new Guid("fca3d9f0-743c-4b74-dc05-8e6b080338c9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Insatisfacción con las funciones", "INSATISFACCION_FUNCIONES", "INSATISFACCIÓN CON LAS FUNCIONES", new Guid("c9e5dd70-546d-cb01-3853-9914204e2649"), -9200L, 100 },
                    { -9228L, "DISTANCIA_TRANSPORTE", new Guid("3d728c79-5bb7-f445-f5b8-00e942bb0561"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Distancia / transporte", "DISTANCIA_TRANSPORTE", "DISTANCIA / TRANSPORTE", new Guid("2372d3c2-dbdf-0b94-9811-54dd9292018b"), -9200L, 90 },
                    { -9227L, "REUBICACION_GEOGRAFICA", new Guid("9e56230c-05f9-99d3-de15-ca9d6722bb0a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Reubicación geográfica", "REUBICACION_GEOGRAFICA", "REUBICACIÓN GEOGRÁFICA", new Guid("ac6ce315-7b36-002d-d97d-c98b37d0907c"), -9200L, 80 },
                    { -9226L, "ESTUDIOS", new Guid("5fefe98e-94e3-f962-1604-00273846afdc"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Estudios", "ESTUDIOS", "ESTUDIOS", new Guid("544151ed-a406-ffd5-7f7f-a2d3bf2d3c3a"), -9200L, 70 },
                    { -9225L, "SALUD", new Guid("5e2cc6d8-200d-3ce5-a8da-b49f2ca971b8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Salud", "SALUD", "SALUD", new Guid("4c57cab9-587c-c3a4-b7a1-c689686e2911"), -9200L, 60 },
                    { -9224L, "MOTIVOS_PERSONALES", new Guid("add47424-45cc-4835-244f-4e57508afc99"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Motivos personales", "MOTIVOS_PERSONALES", "MOTIVOS PERSONALES", new Guid("84db5674-af89-345f-0f84-e5790e1cce3d"), -9200L, 50 },
                    { -9223L, "RELACION_JEFATURA", new Guid("78e0dcae-37dd-775d-cac6-3f30b21c8e2e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Relación con la jefatura", "RELACION_JEFATURA", "RELACIÓN CON LA JEFATURA", new Guid("608c68ff-3f1e-b0e6-f968-8673994ea659"), -9200L, 40 },
                    { -9222L, "AMBIENTE_LABORAL", new Guid("b2eb7e7a-f2ba-90ed-cfa8-d3eb7fe66a5f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ambiente laboral", "AMBIENTE_LABORAL", "AMBIENTE LABORAL", new Guid("fea9b50a-8cfd-cc41-0317-4a37eb3308f8"), -9200L, 30 },
                    { -9221L, "CRECIMIENTO_PROFESIONAL", new Guid("d87d720d-a5b0-ee72-74c4-dc450ec3bff4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Crecimiento profesional", "CRECIMIENTO_PROFESIONAL", "CRECIMIENTO PROFESIONAL", new Guid("58812f9f-9c56-e8e9-7fbb-867d13581a1d"), -9200L, 20 },
                    { -9220L, "MEJOR_OFERTA_SALARIAL", new Guid("3bb4647b-29b2-aa6e-f592-fc74ccd9267e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mejor oferta salarial", "MEJOR_OFERTA_SALARIAL", "MEJOR OFERTA SALARIAL", new Guid("d50b83b2-81ee-f0be-f526-5728c08617ae"), -9200L, 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_retirement_category_catalog_items__country_active_sort",
                table: "retirement_category_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_retirement_category_catalog_items__country_code",
                table: "retirement_category_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_retirement_category_catalog_items__public_id",
                table: "retirement_category_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retirement_reason_catalog_items__category_active_sort",
                table: "retirement_reason_catalog_items",
                columns: new[] { "retirement_category_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_retirement_reason_catalog_items__country_active_sort",
                table: "retirement_reason_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_retirement_reason_catalog_items__country_code",
                table: "retirement_reason_catalog_items",
                columns: new[] { "country_catalog_item_id", "retirement_category_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_retirement_reason_catalog_items__public_id",
                table: "retirement_reason_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retirement_reason_catalog_items");

            migrationBuilder.DropTable(
                name: "retirement_category_catalog_items");
        }
    }
}
