using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelateCompanyTypesToCountryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_company_type_catalog_items__owner_active",
                table: "company_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_company_type_catalog_items__owner_name",
                table: "company_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_company_type_catalog_items__owner_code",
                table: "company_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "owner_user_public_id",
                table: "company_type_catalog_items");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "company_type_catalog_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.InsertData(
                table: "company_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "created_utc", "description", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -8304L, "NONPROFIT", new Guid("11d194d5-88b0-f5db-2da7-f9e72c7adb74"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Corporation organized for charitable, educational or public benefit purposes.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nonprofit Corporation", "NONPROFIT", "NONPROFIT CORPORATION", new Guid("350cd327-f33f-af8b-e729-383190f047b1"), 50 },
                    { -8303L, "LLP", new Guid("bad183d7-1cf7-e216-8220-b00e198d670b"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Partnership structure with liability protections for partners.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Limited Liability Partnership", "LLP", "LIMITED LIABILITY PARTNERSHIP", new Guid("0de7547f-7824-3f09-6821-7fdfcc22477b"), 40 },
                    { -8302L, "S_CORP", new Guid("4bdefd1e-0249-a89c-023f-b956c863cdb9"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Corporation with pass-through taxation under eligible IRS election.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "S Corporation", "S_CORP", "S CORPORATION", new Guid("0e5fc91d-4c0e-7a38-d572-8516a155f03a"), 30 },
                    { -8301L, "C_CORP", new Guid("da04091d-e96d-caf0-b76e-73c725a497c9"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Corporation taxed separately from its owners.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "C Corporation", "C_CORP", "C CORPORATION", new Guid("62b93007-f025-adc5-72f4-69b799c2203c"), 20 },
                    { -8300L, "LLC", new Guid("03e6f071-4107-7745-f627-1a0f90c33c65"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Business entity with limited liability and flexible tax treatment.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Limited Liability Company", "LLC", "LIMITED LIABILITY COMPANY", new Guid("513c0578-fbc7-efab-e7ff-20b5d01ce120"), 10 },
                    { -8204L, "AC", new Guid("c59684b9-4cf3-e0b9-84dc-38ae1f338a16"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Persona moral de naturaleza civil sin fines preponderantemente mercantiles.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Asociacion Civil", "AC", "ASOCIACION CIVIL", new Guid("9f23ad01-a794-6393-8bc9-1bd6559027f8"), 50 },
                    { -8203L, "BRANCH_OFFICE", new Guid("dfa6068a-714b-9622-4fb8-8ebd241cc116"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Establecimiento mexicano dependiente de una sociedad matriz.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sucursal", "BRANCH_OFFICE", "SUCURSAL", new Guid("f6c55351-b07a-b645-8b99-ba58cdc405c8"), 40 },
                    { -8202L, "SAS", new Guid("1ff2a0b0-b0ed-fc81-38aa-8b5a040b5a5e"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil simplificada constituida por accionistas.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad por Acciones Simplificada", "SAS", "SOCIEDAD POR ACCIONES SIMPLIFICADA", new Guid("21361a52-175f-7753-0ae3-003a84fd8d76"), 30 },
                    { -8201L, "S_DE_RL_DE_CV", new Guid("53591402-f147-48e3-4505-d023a0d0dda2"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil mexicana de partes sociales con responsabilidad limitada.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad de Responsabilidad Limitada de Capital Variable", "S_DE_RL_DE_CV", "SOCIEDAD DE RESPONSABILIDAD LIMITADA DE CAPITAL VARIABLE", new Guid("f5a185f4-a5ae-b48e-2940-b57eb0822911"), 20 },
                    { -8200L, "SA_DE_CV", new Guid("865c1575-f03a-c4d4-47a8-a532978fccfc"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil mexicana con capital representado en acciones.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad Anonima de Capital Variable", "SA_DE_CV", "SOCIEDAD ANONIMA DE CAPITAL VARIABLE", new Guid("8f6b5423-1e9d-8eeb-0578-4c3597d2b3a5"), 10 },
                    { -8104L, "ASSOCIATION", new Guid("db0f00d6-3720-c452-56c9-af138d276891"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Entidad asociativa sin fines de lucro reconocida legalmente.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Asociacion", "ASSOCIATION", "ASOCIACION", new Guid("a36cd3b2-0ebe-a377-7d58-470a6ec059ae"), 50 },
                    { -8103L, "COOPERATIVE", new Guid("b61c2e4a-d3ce-c341-c7bb-170f378c5828"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Entidad asociativa organizada bajo el regimen cooperativo.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cooperativa", "COOPERATIVE", "COOPERATIVA", new Guid("ea00afce-0662-cf80-bc97-9da08d4aed5c"), 40 },
                    { -8102L, "INDIVIDUAL_ENTERPRISE", new Guid("34430f95-ab20-0377-0154-824491183943"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Operacion empresarial inscrita a nombre de una sola persona.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Empresa Individual", "INDIVIDUAL_ENTERPRISE", "EMPRESA INDIVIDUAL", new Guid("ddbd3d51-421d-d96a-bc34-f39d8f3d4159"), 30 },
                    { -8101L, "S_DE_RL", new Guid("92cb6ee1-f3d5-0022-336a-d23f73465588"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil de cuotas con responsabilidad limitada al aporte de los socios.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad de Responsabilidad Limitada", "S_DE_RL", "SOCIEDAD DE RESPONSABILIDAD LIMITADA", new Guid("9698c622-97f0-f976-7a59-ede072ffae46"), 20 },
                    { -8100L, "SA_DE_CV", new Guid("4f1c9778-dcce-91b8-9019-726b201a6014"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil con capital representado en acciones y posibilidad de variacion de capital.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad Anonima de Capital Variable", "SA_DE_CV", "SOCIEDAD ANONIMA DE CAPITAL VARIABLE", new Guid("44fce38b-0973-102e-4294-c4d4668201f4"), 10 }
                });

            migrationBuilder.Sql(
                """
                WITH mapped_company_types AS
                (
                    SELECT
                        company.id AS company_id,
                        target.id AS target_company_type_id
                    FROM companies AS company
                    JOIN company_type_catalog_items AS legacy
                      ON company.company_type_catalog_item_id = legacy.id
                    JOIN company_type_catalog_items AS target
                      ON target.country_catalog_item_id = company.country_catalog_item_id
                     AND target.normalized_code = CASE
                            WHEN company.country_catalog_item_id = -7068 AND legacy.normalized_code = 'LIMITED_LIABILITY' THEN 'S_DE_RL'
                            WHEN company.country_catalog_item_id = -7141 AND legacy.normalized_code = 'LIMITED_LIABILITY' THEN 'S_DE_RL_DE_CV'
                            WHEN company.country_catalog_item_id = -7236 AND legacy.normalized_code = 'LIMITED_LIABILITY' THEN 'LLC'
                            WHEN company.country_catalog_item_id = -7141 AND legacy.normalized_code = 'ASSOCIATION' THEN 'AC'
                            WHEN company.country_catalog_item_id = -7236 AND legacy.normalized_code IN ('ASSOCIATION', 'FOUNDATION') THEN 'NONPROFIT'
                            ELSE legacy.normalized_code
                         END
                    WHERE legacy.id > 0
                )
                UPDATE companies AS company
                SET company_type_catalog_item_id = mapped_company_types.target_company_type_id
                FROM mapped_company_types
                WHERE company.id = mapped_company_types.company_id;

                UPDATE companies
                SET company_type_catalog_item_id = NULL
                WHERE company_type_catalog_item_id IN
                (
                    SELECT legacy.id
                    FROM company_type_catalog_items AS legacy
                    WHERE legacy.id > 0
                );

                DELETE FROM company_type_catalog_items
                WHERE id > 0;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "country_catalog_item_id",
                table: "company_type_catalog_items",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__country_active",
                table: "company_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__country_name",
                table: "company_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_company_type_catalog_items__country_code",
                table: "company_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_company_type_catalog_items_country_catalog_country_catalog_~",
                table: "company_type_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_type_catalog_items_country_catalog_country_catalog_~",
                table: "company_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_company_type_catalog_items__country_active",
                table: "company_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_company_type_catalog_items__country_name",
                table: "company_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_company_type_catalog_items__country_code",
                table: "company_type_catalog_items");

            migrationBuilder.Sql(
                """
                UPDATE companies
                SET company_type_catalog_item_id = NULL
                WHERE company_type_catalog_item_id IN (-8304, -8303, -8302, -8301, -8300, -8204, -8203, -8202, -8201, -8200, -8104, -8103, -8102, -8101, -8100);
                """);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8304L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8303L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8302L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8301L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8300L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8204L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8203L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8202L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8201L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8200L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8104L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8103L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8102L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8101L);

            migrationBuilder.DeleteData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8100L);

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "company_type_catalog_items");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_public_id",
                table: "company_type_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__owner_active",
                table: "company_type_catalog_items",
                columns: new[] { "owner_user_public_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__owner_name",
                table: "company_type_catalog_items",
                columns: new[] { "owner_user_public_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_company_type_catalog_items__owner_code",
                table: "company_type_catalog_items",
                columns: new[] { "owner_user_public_id", "normalized_code" },
                unique: true);
        }
    }
}
