using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestructureEducationCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RT-02 (drop & recreate ratificado) + gotcha DevSeed→HasData: positive-id career/study-type
            // rows (runtime-seeded in dev DBs) would be left with country/FK columns = 0 and break the new
            // NOT NULL FKs. They are removed together with any personnel-file educations referencing them
            // (FK RESTRICT). The HasData rows (negative ids) are renamed in place below, so seed-backed
            // educations keep their FKs. No-op on fresh/server databases.
            migrationBuilder.Sql(
                """
                DELETE FROM personnel_file_educations
                WHERE education_career_catalog_item_id IN (SELECT id FROM education_career_catalog_items WHERE id > 0)
                   OR education_study_type_catalog_item_id IN (SELECT id FROM education_study_type_catalog_items WHERE id > 0);
                DELETE FROM education_career_catalog_items WHERE id > 0;
                DELETE FROM education_study_type_catalog_items WHERE id > 0;
                """);

            migrationBuilder.DropIndex(
                name: "ix_education_career_catalog_items__active_sort",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_career_catalog_items__code",
                table: "education_career_catalog_items");

            migrationBuilder.AddColumn<string>(
                name: "abbreviation",
                table: "education_study_type_catalog_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "education_level_catalog_item_id",
                table: "education_study_type_catalog_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "abbreviation",
                table: "education_career_catalog_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "education_career_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "education_career_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "education_study_type_catalog_item_id",
                table: "education_career_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "increment",
                table: "education_career_catalog_items",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_recognized",
                table: "education_career_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "education_level_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_education_level_catalog_items", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9785L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "country_catalog_item_id", "country_code", "education_study_type_catalog_item_id", "increment", "is_recognized", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "LCP", "LIC_CONTADURIA", new Guid("914769fb-fab8-2ca3-4c25-dac393c9c35c"), -7068L, "SV", -9765L, 0m, true, "Lic. Contaduría Pública", "LIC_CONTADURIA", "LIC. CONTADURÍA PÚBLICA", new Guid("15971151-d673-8b54-98cb-0929fb549ece"), 40 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9784L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "country_catalog_item_id", "country_code", "education_study_type_catalog_item_id", "increment", "is_recognized", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "IS", "ING_SISTEMAS", new Guid("bae5c4d8-4c94-b3cd-8df6-c72520cd7ed0"), -7068L, "SV", -9765L, 0m, true, "Ingeniería en Sistemas/Computación", "ING_SISTEMAS", "INGENIERÍA EN SISTEMAS/COMPUTACIÓN", new Guid("172cd5f5-d2ef-005e-aefb-a4cc45414e01"), 20 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9783L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "country_catalog_item_id", "country_code", "education_study_type_catalog_item_id", "increment", "is_recognized", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "LP", "LIC_PSICOLOGIA", new Guid("1bd28f5f-9d23-1a7f-d148-fc8d901c62f2"), -7068L, "SV", -9765L, 0m, true, "Lic. Psicología", "LIC_PSICOLOGIA", "LIC. PSICOLOGÍA", new Guid("1ccb5620-a23c-88fe-98a0-22e4a78d5fd7"), 50 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9782L,
                columns: new[] { "abbreviation", "concurrency_token", "country_catalog_item_id", "country_code", "education_study_type_catalog_item_id", "increment", "is_recognized", "name", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "MBA", new Guid("36fb82c4-4147-82ed-8d88-8089d31a8cc5"), -7068L, "SV", -9766L, 0m, true, "Maestría en Administración (MBA)", "MAESTRÍA EN ADMINISTRACIÓN (MBA)", new Guid("9c3e3d65-719c-2631-74e1-7105183a1c25"), 80 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9781L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "country_catalog_item_id", "country_code", "education_study_type_catalog_item_id", "increment", "is_recognized", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "LAE", "LIC_ADMIN", new Guid("4780f727-d0d6-bfe8-00d7-f8b36609983a"), -7068L, "SV", -9765L, 0m, true, "Lic. Administración de Empresas", "LIC_ADMIN", "LIC. ADMINISTRACIÓN DE EMPRESAS", new Guid("e6bcea2c-7f23-8b4d-2e4f-a4209a51b572"), 30 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9780L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "country_catalog_item_id", "country_code", "education_study_type_catalog_item_id", "increment", "is_recognized", "name", "normalized_code", "normalized_name", "public_id" },
                values: new object[] { "II", "ING_INDUSTRIAL", new Guid("7ce40fe1-d2f8-f290-5672-8fcb37038f34"), -7068L, "SV", -9765L, 0m, true, "Ingeniería Industrial", "ING_INDUSTRIAL", "INGENIERÍA INDUSTRIAL", new Guid("733f504c-1093-a8f7-4d4d-938442bad597") });

            migrationBuilder.InsertData(
                table: "education_career_catalog_items",
                columns: new[] { "id", "abbreviation", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "education_study_type_catalog_item_id", "increment", "is_active", "is_recognized", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9788L, "OTRA", "OTRA", new Guid("8d09e1e1-71ff-9e23-1e1e-6524864dbe7c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9765L, 0m, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otra carrera", "OTRA", "OTRA CARRERA", new Guid("555b7eab-70ad-8c46-ac1a-2665f4243c7f"), 90 },
                    { -9787L, "TC", "TEC_COMPUTACION", new Guid("3e1cac0f-b740-95b6-42d8-64df447e08f8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9767L, 0m, true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Técnico en Computación", "TEC_COMPUTACION", "TÉCNICO EN COMPUTACIÓN", new Guid("96ada74e-49f3-3efb-cdeb-7fa32358ddeb"), 70 },
                    { -9786L, "LCJ", "LIC_DERECHO", new Guid("a52a5732-4ba2-4102-ff26-d03a0a2b0eb3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9765L, 0m, true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lic. Ciencias Jurídicas", "LIC_DERECHO", "LIC. CIENCIAS JURÍDICAS", new Guid("ba7c835d-8954-718b-2b8e-fbee25384d6d"), 60 }
                });

            migrationBuilder.InsertData(
                table: "education_level_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9804L, "POSGRADO", new Guid("51b5adb0-9ea6-f652-870d-dd737c02d8a1"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Posgrado", "POSGRADO", "POSGRADO", new Guid("19f1c3c5-31de-4edc-e501-1b18da26429d"), 50 },
                    { -9803L, "SUPERIOR", new Guid("c4279da5-a0f4-6acf-de55-0b259df34785"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Superior / Universitario", "SUPERIOR", "SUPERIOR / UNIVERSITARIO", new Guid("93eea75a-eee5-5071-bfeb-e65199eadd6d"), 40 },
                    { -9802L, "TECNICO", new Guid("f3c0561e-e93b-4b30-a0e5-5e39a17f0f40"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Técnico", "TECNICO", "TÉCNICO", new Guid("8b58115f-1fb3-d737-0723-c61056fa34db"), 30 },
                    { -9801L, "MEDIO", new Guid("bea0f175-3578-e2d8-de15-ec24d4caad99"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Medio", "MEDIO", "MEDIO", new Guid("72c6cd86-987b-8f0f-e256-b93d2c59d007"), 20 },
                    { -9800L, "BASICO", new Guid("320fb3df-51ef-0d14-3d67-f4cca66ef701"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Básico", "BASICO", "BÁSICO", new Guid("1f2540b5-f7e8-5417-f470-33686cd30a2a"), 10 }
                });

            migrationBuilder.UpdateData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9767L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "education_level_catalog_item_id", "name", "normalized_code", "normalized_name", "public_id" },
                values: new object[] { "TEC", "TECNICO", new Guid("2fb6997c-ca2b-8153-aa9d-8157854fadb4"), -9802L, "Técnico / Tecnólogo", "TECNICO", "TÉCNICO / TECNÓLOGO", new Guid("890644df-f7b1-f0d2-c72b-33d4bd8f71ee") });

            migrationBuilder.UpdateData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9766L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "education_level_catalog_item_id", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "POSG", "POSGRADO", new Guid("1130a7cb-9858-e441-19f2-4c40046ab2fb"), -9804L, "Posgrado", "POSGRADO", "POSGRADO", new Guid("559e572f-b503-902f-c787-106bb7b39104"), 50 });

            migrationBuilder.UpdateData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9765L,
                columns: new[] { "abbreviation", "code", "concurrency_token", "education_level_catalog_item_id", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "UNIV", "UNIVERSITARIA", new Guid("80f8b0bf-eb9a-74ff-8077-23eed89af25e"), -9803L, "Universitaria", "UNIVERSITARIA", "UNIVERSITARIA", new Guid("5a7c2efc-5774-8905-3aad-521a6475220e"), 40 });

            migrationBuilder.InsertData(
                table: "education_study_type_catalog_items",
                columns: new[] { "id", "abbreviation", "code", "concurrency_token", "created_utc", "education_level_catalog_item_id", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9769L, "BACH", "BACHILLERATO", new Guid("b24f39c3-a8b5-203f-31ea-41a1e5d9cace"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9801L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bachillerato", "BACHILLERATO", "BACHILLERATO", new Guid("ea2e2a99-e14a-ddcd-1252-01ae99bb86ac"), 20 },
                    { -9768L, "BAS", "BASICA", new Guid("753cafb4-ca6f-2bea-c43a-777facd81e05"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9800L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Educación Básica", "BASICA", "EDUCACIÓN BÁSICA", new Guid("995417fc-a4c4-f6a6-ac89-5c7764388ca7"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_education_study_type_catalog_items_education_level_catalog_~",
                table: "education_study_type_catalog_items",
                column: "education_level_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_education_career_catalog_items__active_sort",
                table: "education_career_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_education_career_catalog_items_education_study_type_catalog~",
                table: "education_career_catalog_items",
                column: "education_study_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_education_career_catalog_items__code",
                table: "education_career_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_level_catalog_items__active_sort",
                table: "education_level_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_level_catalog_items__code",
                table: "education_level_catalog_items",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_education_level_catalog_items__public_id",
                table: "education_level_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_education_career_catalog_items_country_catalog_country_cata~",
                table: "education_career_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_education_career_catalog_items__education_study_type",
                table: "education_career_catalog_items",
                column: "education_study_type_catalog_item_id",
                principalTable: "education_study_type_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_education_study_type_catalog_items__education_level",
                table: "education_study_type_catalog_items",
                column: "education_level_catalog_item_id",
                principalTable: "education_level_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_education_career_catalog_items_country_catalog_country_cata~",
                table: "education_career_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "fk_education_career_catalog_items__education_study_type",
                table: "education_career_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "fk_education_study_type_catalog_items__education_level",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropTable(
                name: "education_level_catalog_items");

            migrationBuilder.DropIndex(
                name: "IX_education_study_type_catalog_items_education_level_catalog_~",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_career_catalog_items__active_sort",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "IX_education_career_catalog_items_education_study_type_catalog~",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_career_catalog_items__code",
                table: "education_career_catalog_items");

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9788L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9787L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9786L);

            migrationBuilder.DeleteData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9769L);

            migrationBuilder.DeleteData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9768L);

            migrationBuilder.DropColumn(
                name: "abbreviation",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "education_level_catalog_item_id",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "abbreviation",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "education_study_type_catalog_item_id",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "increment",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "is_recognized",
                table: "education_career_catalog_items");

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9785L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "ACCOUNTING_AUDITING", new Guid("b5103c21-65fb-d8d3-179e-f79b185ed44d"), "Contaduria Publica y Auditoria", "ACCOUNTING_AUDITING", "CONTADURIA PUBLICA Y AUDITORIA", new Guid("f79ebd75-9240-0d8e-e4f5-3d73e10cb0d7"), 60 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9784L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "SYSTEMS_ENGINEERING", new Guid("782fa29f-6fd2-087e-64e8-4c4ef2c3468d"), "Ingenieria en Sistemas Informaticos", "SYSTEMS_ENGINEERING", "INGENIERIA EN SISTEMAS INFORMATICOS", new Guid("a923e36d-c203-b34c-1dc3-acee89173b40"), 50 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9783L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "PSYCHOLOGY", new Guid("fa96fe26-112b-ebbf-fd7a-fc316063633c"), "Psicologia", "PSYCHOLOGY", "PSICOLOGIA", new Guid("9b5502a1-bce5-aadd-fbcd-e863bdcbcf1f"), 40 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9782L,
                columns: new[] { "concurrency_token", "name", "normalized_name", "public_id", "sort_order" },
                values: new object[] { new Guid("80b373e8-5d54-d636-fd88-88dce6c42232"), "Maestria en Administracion de Negocios", "MAESTRIA EN ADMINISTRACION DE NEGOCIOS", new Guid("3354f6c2-3352-1491-52f6-be24d9eb394c"), 30 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9781L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "BUSINESS_ADMINISTRATION", new Guid("787f07c6-8397-f725-d811-e458ec01865f"), "Administracion de Empresas", "BUSINESS_ADMINISTRATION", "ADMINISTRACION DE EMPRESAS", new Guid("b0129fc7-71c9-63cb-4d2c-b985e4ff0d55"), 20 });

            migrationBuilder.UpdateData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9780L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id" },
                values: new object[] { "INDUSTRIAL_ENGINEERING", new Guid("62755550-1ec4-7e9c-edff-bb6e550df223"), "Ingenieria Industrial", "INDUSTRIAL_ENGINEERING", "INGENIERIA INDUSTRIAL", new Guid("be53dc66-851f-9cf6-abf3-5aa2e5cf1efa") });

            migrationBuilder.UpdateData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9767L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id" },
                values: new object[] { "TECHNICAL", new Guid("9aff6f4c-7538-8ec2-5928-137af7642871"), "Tecnico", "TECHNICAL", "TECNICO", new Guid("95b4d3b3-e4f5-822d-5233-6c225402dc1f") });

            migrationBuilder.UpdateData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9766L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "MASTER", new Guid("e43200e0-ed00-158f-6c54-5e354b9dbe5d"), "Maestria", "MASTER", "MAESTRIA", new Guid("2e8468ba-c9cf-7a25-a0cf-67a55d16d0a5"), 20 });

            migrationBuilder.UpdateData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9765L,
                columns: new[] { "code", "concurrency_token", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { "BACHELOR", new Guid("77e9f2d7-c902-2ec4-ad82-5303c1606e63"), "Licenciatura", "BACHELOR", "LICENCIATURA", new Guid("4fe0f4aa-a1b6-8289-3a20-7f0dcd74df47"), 10 });

            migrationBuilder.CreateIndex(
                name: "ix_education_career_catalog_items__active_sort",
                table: "education_career_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_career_catalog_items__code",
                table: "education_career_catalog_items",
                column: "normalized_code",
                unique: true);
        }
    }
}
