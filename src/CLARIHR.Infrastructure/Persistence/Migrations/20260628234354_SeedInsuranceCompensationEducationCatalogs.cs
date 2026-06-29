using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedInsuranceCompensationEducationCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotency guard. This migration moves catalogs that were previously seeded by DevSeedService
            // (dev only) to canonical HasData rows with stable NEGATIVE ids. On a fresh database / the server
            // these tables are empty, so every statement below is a no-op. On a dev database that already ran
            // the old DevSeed, it removes ONLY the stale rows (positive identity ids) so the HasData inserts
            // below do not collide on the unique code index; HasData rows (id < 0) and any records referencing
            // them are left untouched. The education catalogs are FK-referenced (RESTRICT) by
            // personnel_file_educations, so the dev sample records pointing at stale (positive-id) rows are
            // cleared first — on the server that table is empty (those catalogs were never seeded there).
            migrationBuilder.Sql(@"
                DELETE FROM personnel_file_educations WHERE education_status_catalog_item_id > 0;
                DELETE FROM education_career_catalog_items WHERE id > 0;
                DELETE FROM education_modality_catalog_items WHERE id > 0;
                DELETE FROM education_shift_catalog_items WHERE id > 0;
                DELETE FROM education_status_catalog_items WHERE id > 0;
                DELETE FROM education_study_type_catalog_items WHERE id > 0;
                DELETE FROM insurance_range_catalog_items WHERE id > 0;
                DELETE FROM insurance_type_catalog_items WHERE id > 0;
                DELETE FROM compensation_concept_type_catalog_items WHERE id > 0;
                DELETE FROM pay_period_catalog_items WHERE id > 0;
                DELETE FROM calculation_base_catalog_items WHERE id > 0;
            ");

            migrationBuilder.InsertData(
                table: "calculation_base_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9753L, "RUBRO_ESPECIFICO", new Guid("2be07a28-f25a-17f3-4aa2-87adb923290b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rubro especifico", "RUBRO_ESPECIFICO", "RUBRO ESPECIFICO", new Guid("519e9347-362b-ff78-4283-08b76cfc73f4"), 40 },
                    { -9752L, "IBC", new Guid("e821ae33-fcf3-4b71-6f5f-e13389d4f083"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso base de cotizacion", "IBC", "INGRESO BASE DE COTIZACION", new Guid("b3617314-6dad-3891-e93a-f855f25c6e5e"), 30 },
                    { -9751L, "SALARIO_BRUTO", new Guid("7be8e213-b987-5c36-2679-d9b2a82a978b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Salario bruto", "SALARIO_BRUTO", "SALARIO BRUTO", new Guid("0eb72d2e-f46d-84bc-f500-1cdf99eb6b2f"), 20 },
                    { -9750L, "SALARIO_BASE", new Guid("f09988b7-5f9c-6dc1-0802-257e9476ce68"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Salario base", "SALARIO_BASE", "SALARIO BASE", new Guid("6748326c-9d57-ed1b-52b7-5c780ce5d6ba"), 10 }
                });

            migrationBuilder.InsertData(
                table: "compensation_concept_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "contribution_cap", "country_catalog_item_id", "country_code", "created_utc", "default_calculation_base_code", "default_calculation_type", "default_deduction_class", "default_employee_rate", "default_employer_rate", "is_active", "is_statutory", "modified_utc", "name", "nature", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9736L, "OTRO_EXTERNO", new Guid("285703c8-4044-3d38-70e2-42df925aa0a3"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Externo", null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro externo", "Egreso", "OTRO_EXTERNO", "OTRO EXTERNO", new Guid("aaff3cfd-e2dc-c0ad-da58-66331dd8a357"), 330 },
                    { -9735L, "CUOTA_ALIMENTICIA", new Guid("d4e99e06-c964-a401-6e04-c36eb5050515"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Externo", null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuota alimenticia", "Egreso", "CUOTA_ALIMENTICIA", "CUOTA ALIMENTICIA", new Guid("98ffc1cc-b25f-2620-f196-3f6471e521d3"), 320 },
                    { -9734L, "EMBARGO", new Guid("dc604c82-e5b3-d686-0715-9597e5628460"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Externo", null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Embargo", "Egreso", "EMBARGO", "EMBARGO", new Guid("a28a756e-cc16-bb6a-892d-046edc21e2a2"), 310 },
                    { -9733L, "PRESTAMO_BANCARIO", new Guid("935d8fc4-8358-2584-d25a-204be69226a2"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Externo", null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Prestamo bancario", "Egreso", "PRESTAMO_BANCARIO", "PRESTAMO BANCARIO", new Guid("552dcb33-62e3-14cf-6177-2396163e5602"), 300 },
                    { -9732L, "PRESTAMO_INTERNO", new Guid("9a393021-744c-f6e0-095c-3be6530425d8"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Interno", null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Prestamo interno", "Egreso", "PRESTAMO_INTERNO", "PRESTAMO INTERNO", new Guid("1eb42eef-ecee-1768-0e2a-017c951f6611"), 220 },
                    { -9731L, "ANTICIPO", new Guid("eee06601-4c45-b5ee-8ac6-c8507f4acca6"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Interno", null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anticipo", "Egreso", "ANTICIPO", "ANTICIPO", new Guid("3d56e7d2-1256-9263-e0d2-4c50f503153b"), 210 },
                    { -9730L, "DANO_EQUIPO", new Guid("7c6aa173-4806-25d4-d2e6-58f6b3e12d0f"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Interno", null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dano de equipo", "Egreso", "DANO_EQUIPO", "DANO DE EQUIPO", new Guid("983cf927-e2cf-262e-17b8-f112f16768f3"), 200 },
                    { -9729L, "RENTA", new Guid("c1c8d16c-d679-9c22-7095-f739794ee899"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "SALARIO_BRUTO", "Percentage", "Ley", null, null, true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Renta (ISR)", "Egreso", "RENTA", "RENTA (ISR)", new Guid("dfc57011-7b2a-e780-4eef-2a1f32814d52"), 120 },
                    { -9728L, "AFP", new Guid("cebacf52-a154-3da6-08b4-498787259ed4"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "IBC", "Percentage", "Ley", 7.25m, 8.75m, true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AFP", "Egreso", "AFP", "AFP", new Guid("49ea4a64-fbda-8fb9-7ea6-cfa9c9a48b7e"), 110 },
                    { -9727L, "ISSS", new Guid("91766b06-6129-51a2-ae85-76387cd8b424"), 1000.00m, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "IBC", "Percentage", "Ley", 3.00m, 7.50m, true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ISSS", "Egreso", "ISSS", "ISSS", new Guid("df803079-7311-f4c0-bbad-2b2bdad388b2"), 100 },
                    { -9726L, "OTRO_INGRESO", new Guid("b191dcab-142e-9fa9-3430-17969f10a6cd"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", null, null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro ingreso", "Ingreso", "OTRO_INGRESO", "OTRO INGRESO", new Guid("97c79f99-24f4-a840-f0e3-28494ac95861"), 70 },
                    { -9725L, "AGUINALDO", new Guid("c3706115-bc78-ea7a-90b3-8d064dc0df11"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", null, null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aguinaldo", "Ingreso", "AGUINALDO", "AGUINALDO", new Guid("f31df736-18ab-2ec7-dd33-5bc533db18b5"), 60 },
                    { -9724L, "VIATICOS", new Guid("f1ae99ef-b9d9-a269-022d-960031e8873b"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", null, null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Viaticos", "Ingreso", "VIATICOS", "VIATICOS", new Guid("f8099f09-b23d-ccd6-8e6d-923ec76999b7"), 50 },
                    { -9723L, "BONO", new Guid("8aaa651b-9b6e-d038-f364-5a8173367765"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", null, null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bono", "Ingreso", "BONO", "BONO", new Guid("d4f24d3a-8b14-39a2-acd8-4504e5b58e7c"), 40 },
                    { -9722L, "COMISION", new Guid("9bfee7da-dd61-7e86-d49b-3e7890373e3f"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "SALARIO_BASE", "Percentage", null, null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Comision", "Ingreso", "COMISION", "COMISION", new Guid("00e8df3d-b932-9a6a-83a3-0b0375d48027"), 30 },
                    { -9721L, "HORAS_EXTRA", new Guid("a38fd1ab-009d-1447-8fc6-7a4848af0c3a"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", null, null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Horas extra", "Ingreso", "HORAS_EXTRA", "HORAS EXTRA", new Guid("06cdde19-49cf-d5e2-0dbd-373559a8f004"), 20 },
                    { -9720L, "SALARIO_BASE", new Guid("1517262a-a255-e960-6292-c9a56f5250a9"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", null, null, null, true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Salario base", "Ingreso", "SALARIO_BASE", "SALARIO BASE", new Guid("4c2842d4-d995-2ed0-e8c9-b298bb96f7d9"), 10 }
                });

            migrationBuilder.InsertData(
                table: "education_career_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9785L, "ACCOUNTING_AUDITING", new Guid("b5103c21-65fb-d8d3-179e-f79b185ed44d"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contaduria Publica y Auditoria", "ACCOUNTING_AUDITING", "CONTADURIA PUBLICA Y AUDITORIA", new Guid("f79ebd75-9240-0d8e-e4f5-3d73e10cb0d7"), 60 },
                    { -9784L, "SYSTEMS_ENGINEERING", new Guid("782fa29f-6fd2-087e-64e8-4c4ef2c3468d"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingenieria en Sistemas Informaticos", "SYSTEMS_ENGINEERING", "INGENIERIA EN SISTEMAS INFORMATICOS", new Guid("a923e36d-c203-b34c-1dc3-acee89173b40"), 50 },
                    { -9783L, "PSYCHOLOGY", new Guid("fa96fe26-112b-ebbf-fd7a-fc316063633c"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Psicologia", "PSYCHOLOGY", "PSICOLOGIA", new Guid("9b5502a1-bce5-aadd-fbcd-e863bdcbcf1f"), 40 },
                    { -9782L, "MBA", new Guid("80b373e8-5d54-d636-fd88-88dce6c42232"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Maestria en Administracion de Negocios", "MBA", "MAESTRIA EN ADMINISTRACION DE NEGOCIOS", new Guid("3354f6c2-3352-1491-52f6-be24d9eb394c"), 30 },
                    { -9781L, "BUSINESS_ADMINISTRATION", new Guid("787f07c6-8397-f725-d811-e458ec01865f"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Administracion de Empresas", "BUSINESS_ADMINISTRATION", "ADMINISTRACION DE EMPRESAS", new Guid("b0129fc7-71c9-63cb-4d2c-b985e4ff0d55"), 20 },
                    { -9780L, "INDUSTRIAL_ENGINEERING", new Guid("62755550-1ec4-7e9c-edff-bb6e550df223"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingenieria Industrial", "INDUSTRIAL_ENGINEERING", "INGENIERIA INDUSTRIAL", new Guid("be53dc66-851f-9cf6-abf3-5aa2e5cf1efa"), 10 }
                });

            migrationBuilder.InsertData(
                table: "education_modality_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9776L, "REMOTE", new Guid("6f2c5b2e-f12b-1d62-14d0-dbd5407d69fa"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Virtual", "REMOTE", "VIRTUAL", new Guid("8fd48e29-69f5-9825-4ea9-a7e4888ef9df"), 20 },
                    { -9775L, "ONSITE", new Guid("72c3c3c9-1bbb-61ff-8e9f-f17a472afc28"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Presencial", "ONSITE", "PRESENCIAL", new Guid("64c5edbe-98fd-dac7-bb27-5e741fc63683"), 10 }
                });

            migrationBuilder.InsertData(
                table: "education_shift_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9771L, "AFTERNOON", new Guid("3ab3e20a-c1dd-cad5-770a-718870ae6c51"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vespertino", "AFTERNOON", "VESPERTINO", new Guid("f2e04061-e959-cc1d-8bea-9a05a8dc3eec"), 20 },
                    { -9770L, "MORNING", new Guid("fc575b74-4abc-a194-7010-c953f95e4252"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Matutino", "MORNING", "MATUTINO", new Guid("cbc80185-10e7-39f6-dd50-08c32a51c896"), 10 }
                });

            migrationBuilder.InsertData(
                table: "education_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9761L, "IN_PROGRESS", new Guid("76367958-f74c-6a0e-e511-1085d252de4d"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En curso", "IN_PROGRESS", "EN CURSO", new Guid("23c1e2d0-ab6f-3ef7-81de-032b95db8529"), 20 },
                    { -9760L, "GRADUATED", new Guid("4a915593-68cb-a00f-aec5-5c2aff38fc50"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Graduado", "GRADUATED", "GRADUADO", new Guid("2c708f9d-aadd-45da-7a56-1486cb0f6a5f"), 10 }
                });

            migrationBuilder.InsertData(
                table: "education_study_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9767L, "TECHNICAL", new Guid("9aff6f4c-7538-8ec2-5928-137af7642871"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tecnico", "TECHNICAL", "TECNICO", new Guid("95b4d3b3-e4f5-822d-5233-6c225402dc1f"), 30 },
                    { -9766L, "MASTER", new Guid("e43200e0-ed00-158f-6c54-5e354b9dbe5d"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Maestria", "MASTER", "MAESTRIA", new Guid("2e8468ba-c9cf-7a25-a0cf-67a55d16d0a5"), 20 },
                    { -9765L, "BACHELOR", new Guid("77e9f2d7-c902-2ec4-ad82-5303c1606e63"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Licenciatura", "BACHELOR", "LICENCIATURA", new Guid("4fe0f4aa-a1b6-8289-3a20-7f0dcd74df47"), 10 }
                });

            migrationBuilder.InsertData(
                table: "insurance_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9706L, "OTRO", new Guid("baaa76fb-c8b9-ee0c-410c-ea4a88690a61"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("1ea9d35e-5988-936b-855a-e217efadf3cf"), 70 },
                    { -9705L, "ACCIDENTES", new Guid("185a7590-3061-4dcb-dc6c-60ba653acaee"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Accidentes personales", "ACCIDENTES", "ACCIDENTES PERSONALES", new Guid("247e2693-2cdb-4df2-8c3e-bfb5a9bdb4cb"), 60 },
                    { -9704L, "VISION", new Guid("9924b237-08c9-8931-3748-725de5ce16e0"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Visión", "VISION", "VISIÓN", new Guid("ca4be6b6-e099-c34e-af3a-5ec753c8f296"), 50 },
                    { -9703L, "DENTAL", new Guid("3113bc3d-f0b5-ea58-2f5d-e844efec267d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dental", "DENTAL", "DENTAL", new Guid("38aa4529-da19-5927-60db-ced3524eac92"), 40 },
                    { -9702L, "GASTOS_MEDICOS", new Guid("9116d908-206d-1d89-82ec-44ab1d3fb72c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gastos médicos", "GASTOS_MEDICOS", "GASTOS MÉDICOS", new Guid("af92a9cd-8b6b-3620-cb59-52645bbb9a81"), 30 },
                    { -9701L, "MEDICO_HOSPITALARIO", new Guid("df476739-728c-11e6-776d-5d97eb3aef9d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Médico hospitalario", "MEDICO_HOSPITALARIO", "MÉDICO HOSPITALARIO", new Guid("311ae807-a311-e1d9-ef44-62bbf03fe7b7"), 20 },
                    { -9700L, "VIDA", new Guid("6dad7ed0-1e2f-d550-8064-cddd95a26035"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vida", "VIDA", "VIDA", new Guid("c0432d8c-dea0-5c33-b475-251a46ddae35"), 10 }
                });

            migrationBuilder.InsertData(
                table: "pay_period_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9743L, "UNICA", new Guid("1fdce3c0-91f1-cf7a-b975-d343f613ab5f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Unica", "UNICA", "UNICA", new Guid("7345b4b9-9347-9fce-6b2c-4cd6000b26ce"), 40 },
                    { -9742L, "SEMANAL", new Guid("4715ae26-e336-6b69-ab5c-64aa150b181c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Semanal", "SEMANAL", "SEMANAL", new Guid("74338605-6540-b0d7-bb23-d63cab96ad21"), 30 },
                    { -9741L, "QUINCENAL", new Guid("227d1c88-87e3-9b2c-02dd-ced99c3b74f4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Quincenal", "QUINCENAL", "QUINCENAL", new Guid("c687449e-8907-05e9-c35c-bf44fbc88c95"), 20 },
                    { -9740L, "MENSUAL", new Guid("e3535c31-09c5-e781-b016-e48f74069e48"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mensual", "MENSUAL", "MENSUAL", new Guid("17146915-2801-bc07-d0f1-5c6c53bf5c36"), 10 }
                });

            migrationBuilder.InsertData(
                table: "insurance_range_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "insurance_type_catalog_item_id", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9715L, "PREMIUM", new Guid("4f97e43e-6f86-811a-cf56-33b025b21741"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9701L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Premium", "PREMIUM", "PREMIUM", new Guid("fa790ad8-0847-9ddb-d0de-ee9ce3345ba7"), 30 },
                    { -9714L, "INTERMEDIO", new Guid("884eec26-4b52-bf9e-9384-298628b58d20"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9701L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Intermedio", "INTERMEDIO", "INTERMEDIO", new Guid("8953caa5-d60a-3228-7899-daa7a2f2dc7b"), 20 },
                    { -9713L, "BASICO", new Guid("07b060fe-65a5-d728-d27f-1e4fe7542bdf"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9701L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Básico", "BASICO", "BÁSICO", new Guid("1ad1fc48-7fff-5af9-4a16-6824cf8dcbeb"), 10 },
                    { -9712L, "PREMIUM", new Guid("90ba36e9-8aa1-c9f6-2ab4-6a318614a72b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9700L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Premium", "PREMIUM", "PREMIUM", new Guid("cafaf7e9-e934-16a2-0aaf-63432a0341f2"), 30 },
                    { -9711L, "INTERMEDIO", new Guid("231834df-83fc-f2e8-ad59-779a11beb648"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9700L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Intermedio", "INTERMEDIO", "INTERMEDIO", new Guid("796e2e74-4703-f718-29f3-ce76b4e5e098"), 20 },
                    { -9710L, "BASICO", new Guid("a5e7b82d-f304-e374-6f5f-f184a60d3961"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), -9700L, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Básico", "BASICO", "BÁSICO", new Guid("32d254e9-ac9a-69cd-e467-93f460b1e3ef"), 10 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9753L);

            migrationBuilder.DeleteData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9752L);

            migrationBuilder.DeleteData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9751L);

            migrationBuilder.DeleteData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9750L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9736L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9735L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9734L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9733L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9732L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9731L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9730L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9729L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9728L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9727L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9726L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9725L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9724L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9723L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9722L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9721L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9720L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9785L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9784L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9783L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9782L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9781L);

            migrationBuilder.DeleteData(
                table: "education_career_catalog_items",
                keyColumn: "id",
                keyValue: -9780L);

            migrationBuilder.DeleteData(
                table: "education_modality_catalog_items",
                keyColumn: "id",
                keyValue: -9776L);

            migrationBuilder.DeleteData(
                table: "education_modality_catalog_items",
                keyColumn: "id",
                keyValue: -9775L);

            migrationBuilder.DeleteData(
                table: "education_shift_catalog_items",
                keyColumn: "id",
                keyValue: -9771L);

            migrationBuilder.DeleteData(
                table: "education_shift_catalog_items",
                keyColumn: "id",
                keyValue: -9770L);

            migrationBuilder.DeleteData(
                table: "education_status_catalog_items",
                keyColumn: "id",
                keyValue: -9761L);

            migrationBuilder.DeleteData(
                table: "education_status_catalog_items",
                keyColumn: "id",
                keyValue: -9760L);

            migrationBuilder.DeleteData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9767L);

            migrationBuilder.DeleteData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9766L);

            migrationBuilder.DeleteData(
                table: "education_study_type_catalog_items",
                keyColumn: "id",
                keyValue: -9765L);

            migrationBuilder.DeleteData(
                table: "insurance_range_catalog_items",
                keyColumn: "id",
                keyValue: -9715L);

            migrationBuilder.DeleteData(
                table: "insurance_range_catalog_items",
                keyColumn: "id",
                keyValue: -9714L);

            migrationBuilder.DeleteData(
                table: "insurance_range_catalog_items",
                keyColumn: "id",
                keyValue: -9713L);

            migrationBuilder.DeleteData(
                table: "insurance_range_catalog_items",
                keyColumn: "id",
                keyValue: -9712L);

            migrationBuilder.DeleteData(
                table: "insurance_range_catalog_items",
                keyColumn: "id",
                keyValue: -9711L);

            migrationBuilder.DeleteData(
                table: "insurance_range_catalog_items",
                keyColumn: "id",
                keyValue: -9710L);

            migrationBuilder.DeleteData(
                table: "insurance_type_catalog_items",
                keyColumn: "id",
                keyValue: -9706L);

            migrationBuilder.DeleteData(
                table: "insurance_type_catalog_items",
                keyColumn: "id",
                keyValue: -9705L);

            migrationBuilder.DeleteData(
                table: "insurance_type_catalog_items",
                keyColumn: "id",
                keyValue: -9704L);

            migrationBuilder.DeleteData(
                table: "insurance_type_catalog_items",
                keyColumn: "id",
                keyValue: -9703L);

            migrationBuilder.DeleteData(
                table: "insurance_type_catalog_items",
                keyColumn: "id",
                keyValue: -9702L);

            migrationBuilder.DeleteData(
                table: "pay_period_catalog_items",
                keyColumn: "id",
                keyValue: -9743L);

            migrationBuilder.DeleteData(
                table: "pay_period_catalog_items",
                keyColumn: "id",
                keyValue: -9742L);

            migrationBuilder.DeleteData(
                table: "pay_period_catalog_items",
                keyColumn: "id",
                keyValue: -9741L);

            migrationBuilder.DeleteData(
                table: "pay_period_catalog_items",
                keyColumn: "id",
                keyValue: -9740L);

            migrationBuilder.DeleteData(
                table: "insurance_type_catalog_items",
                keyColumn: "id",
                keyValue: -9701L);

            migrationBuilder.DeleteData(
                table: "insurance_type_catalog_items",
                keyColumn: "id",
                keyValue: -9700L);
        }
    }
}
