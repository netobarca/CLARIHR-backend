using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomicAidRequestsAndCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "minimum_seniority_months_for_economic_aid",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "economic_aid_status_catalog_items",
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
                    table.PrimaryKey("pk_economic_aid_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_economic_aid_status_catalog_items_country_catalog_country_c~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "economic_aid_type_catalog_items",
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
                    table.PrimaryKey("pk_economic_aid_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_economic_aid_type_catalog_items_country_catalog_country_cat~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_economic_aid_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    economic_aid_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    type_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    request_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    request_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    response_time_days = table.Column<int>(type: "integer", nullable: true),
                    disbursed_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    disbursement_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_method_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_economic_aid_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_economic_aid_requests__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "economic_aid_request_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    economic_aid_request_id = table.Column<long>(type: "bigint", nullable: false),
                    document_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_economic_aid_request_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_economic_aid_request_documents__document_type",
                        column: x => x.document_type_catalog_item_id,
                        principalTable: "document_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_economic_aid_request_documents__request",
                        column: x => x.economic_aid_request_id,
                        principalTable: "personnel_file_economic_aid_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "economic_aid_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9546L, "ANULADA", new Guid("4ab3745c-2a50-91cd-ff71-de7671bfc893"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("25f3ad8f-ac21-5443-138b-c41621db5d7f"), 70 },
                    { -9545L, "DESEMBOLSADA", new Guid("8f3e628e-2a9e-c8fe-a415-f527737b8262"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Desembolsada", "DESEMBOLSADA", "DESEMBOLSADA", new Guid("60d866e9-a5e3-7c51-de30-aed6367cf040"), 60 },
                    { -9544L, "RECHAZADA", new Guid("d29aa18d-5977-5e48-f42e-8c65309f019f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazada", "RECHAZADA", "RECHAZADA", new Guid("69ab943f-4e63-a716-9166-cc73735c540a"), 50 },
                    { -9543L, "APROBADA", new Guid("94f53a5b-a9c4-ca14-2759-cbfc3f2be39f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aprobada", "APROBADA", "APROBADA", new Guid("f5500e6f-49d3-902f-cb8c-1035975e3027"), 40 },
                    { -9542L, "PENDIENTE_DOCUMENTACION", new Guid("31cc9cab-77c4-2bb5-1047-d3b2a45930da"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pendiente de documentación", "PENDIENTE_DOCUMENTACION", "PENDIENTE DE DOCUMENTACIÓN", new Guid("47c5eb8f-bdca-f058-5c68-b87eb9412c4e"), 30 },
                    { -9541L, "EN_REVISION", new Guid("c207d36c-5936-4010-a63d-32b3d10c6731"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En revisión", "EN_REVISION", "EN REVISIÓN", new Guid("832c94d9-87b1-ca24-b73b-8f1afa2c3444"), 20 },
                    { -9540L, "SOLICITADA", new Guid("77526749-2c88-e70c-f81d-268486ff25a4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Solicitada", "SOLICITADA", "SOLICITADA", new Guid("f738ee6a-fd0c-bc44-337d-afaeaac4d31f"), 10 }
                });

            migrationBuilder.InsertData(
                table: "economic_aid_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9526L, "OTRA", new Guid("71bbd613-774b-d5b4-44d1-b47c42b0cdb3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otra emergencia", "OTRA", "OTRA EMERGENCIA", new Guid("43814374-4203-93f8-ba7a-75a59bdbdf31"), 70 },
                    { -9525L, "ACCIDENTE", new Guid("4d465fbc-9cbc-e864-026d-8b3032c427ad"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Accidente", "ACCIDENTE", "ACCIDENTE", new Guid("01886ebd-8308-accf-51da-1711cb084a30"), 60 },
                    { -9524L, "CALAMIDAD_DOMESTICA", new Guid("f417634c-5e2b-8bab-c0b7-1e0ae3c6501d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Calamidad doméstica", "CALAMIDAD_DOMESTICA", "CALAMIDAD DOMÉSTICA", new Guid("221b4af5-f56c-e56b-d1df-3b1633ad12de"), 50 },
                    { -9523L, "INCENDIO_VIVIENDA", new Guid("8941c49a-4c47-921f-ee74-72d38b99efe1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Incendio o daño en vivienda", "INCENDIO_VIVIENDA", "INCENDIO O DAÑO EN VIVIENDA", new Guid("3d0e60f3-d74e-f162-a460-49157327d36f"), 40 },
                    { -9522L, "DESASTRE_NATURAL", new Guid("64ffc8e2-6d8f-08ee-76a6-a7c73dd31103"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Desastre natural", "DESASTRE_NATURAL", "DESASTRE NATURAL", new Guid("a3f69019-aa26-caaf-8037-976961416f99"), 30 },
                    { -9521L, "GASTOS_FUNEBRES", new Guid("dc51f0f6-36e8-1d14-3f17-0f22d61563b7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gastos fúnebres / fallecimiento de familiar", "GASTOS_FUNEBRES", "GASTOS FÚNEBRES / FALLECIMIENTO DE FAMILIAR", new Guid("ed843831-1fc1-c390-0ee6-51ad55733d09"), 20 },
                    { -9520L, "EMERGENCIA_MEDICA", new Guid("8e7211fb-cf0d-8a07-abda-2f8c37f4a7cc"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Emergencia médica", "EMERGENCIA_MEDICA", "EMERGENCIA MÉDICA", new Guid("11a91f7c-b7d8-0623-4ec1-5d456bea85ff"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_economic_aid_request_documents__document_type",
                table: "economic_aid_request_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_economic_aid_request_documents__file_public_id",
                table: "economic_aid_request_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_economic_aid_request_documents__tenant_req_active",
                table: "economic_aid_request_documents",
                columns: new[] { "tenant_id", "economic_aid_request_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_economic_aid_request_documents_economic_aid_request_id",
                table: "economic_aid_request_documents",
                column: "economic_aid_request_id");

            migrationBuilder.CreateIndex(
                name: "uq_economic_aid_request_documents__public_id",
                table: "economic_aid_request_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_economic_aid_status_catalog_items__country_active_sort",
                table: "economic_aid_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_economic_aid_status_catalog_items__country_code",
                table: "economic_aid_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_economic_aid_status_catalog_items__public_id",
                table: "economic_aid_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_economic_aid_type_catalog_items__country_active_sort",
                table: "economic_aid_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_economic_aid_type_catalog_items__country_code",
                table: "economic_aid_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_economic_aid_type_catalog_items__public_id",
                table: "economic_aid_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_economic_aid_requests__tenant_file_date",
                table: "personnel_file_economic_aid_requests",
                columns: new[] { "tenant_id", "personnel_file_id", "request_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_economic_aid_requests__tenant_file_status",
                table: "personnel_file_economic_aid_requests",
                columns: new[] { "tenant_id", "personnel_file_id", "request_status_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_economic_aid_requests_personnel_file_id",
                table: "personnel_file_economic_aid_requests",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_economic_aid_requests__public_id",
                table: "personnel_file_economic_aid_requests",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "economic_aid_request_documents");

            migrationBuilder.DropTable(
                name: "economic_aid_status_catalog_items");

            migrationBuilder.DropTable(
                name: "economic_aid_type_catalog_items");

            migrationBuilder.DropTable(
                name: "personnel_file_economic_aid_requests");

            migrationBuilder.DropColumn(
                name: "minimum_seniority_months_for_economic_aid",
                table: "company_preferences");
        }
    }
}
