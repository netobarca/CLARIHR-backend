using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAfpCatalogAffiliationAndPensionParams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "afp_code",
                table: "personnel_files",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "default_pensioned_employer_rate",
                table: "compensation_concept_type_catalog_items",
                type: "numeric(11,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_contribution_base",
                table: "compensation_concept_type_catalog_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "afp_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    fax = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("pk_afp_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_afp_catalog_items_country_catalog_country_catalog_item_id",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "afp_catalog_items",
                columns: new[] { "id", "abbreviation", "address", "code", "concurrency_token", "contact_name", "country_catalog_item_id", "country_code", "created_utc", "fax", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "phone", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9692L, "OTRA", null, "OTRA", new Guid("61ac7813-3e76-6d47-05dd-7a4ce1ba87dc"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otra AFP", "OTRA", "OTRA AFP", null, new Guid("dab6246f-1e7d-60d1-ea79-1628bdfacfad"), 30 },
                    { -9691L, "CRECER", null, "CRECER", new Guid("2dfdac36-102a-1d6a-7716-bf80c75641d8"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AFP Crecer", "CRECER", "AFP CRECER", null, new Guid("a6c14192-2481-21cb-9087-fabb0160c2b9"), 20 },
                    { -9690L, "CONFIA", null, "CONFIA", new Guid("89b556d1-9c8a-250e-510e-1999c6b41026"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AFP Confía", "CONFIA", "AFP CONFÍA", null, new Guid("dc93ff0a-e139-c2b4-f708-e9d7743dcb7c"), 10 }
                });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9736L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9735L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9734L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9733L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9732L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9731L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9730L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9729L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9728L,
                columns: new[] { "contribution_cap", "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { 7045.06m, 8.75m, 365.00m });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9727L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, 365.00m });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9726L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9725L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9724L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9723L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9722L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9721L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9720L,
                columns: new[] { "default_pensioned_employer_rate", "min_contribution_base" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "ix_afp_catalog_items__country_active_sort",
                table: "afp_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_afp_catalog_items__country_code",
                table: "afp_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_afp_catalog_items__public_id",
                table: "afp_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "afp_catalog_items");

            migrationBuilder.DropColumn(
                name: "afp_code",
                table: "personnel_files");

            migrationBuilder.DropColumn(
                name: "default_pensioned_employer_rate",
                table: "compensation_concept_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "min_contribution_base",
                table: "compensation_concept_type_catalog_items");

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9728L,
                column: "contribution_cap",
                value: null);
        }
    }
}
