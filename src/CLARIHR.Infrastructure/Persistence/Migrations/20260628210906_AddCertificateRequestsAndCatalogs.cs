using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateRequestsAndCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "certificate_delivery_method_catalog_items",
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
                    table.PrimaryKey("pk_certificate_delivery_method_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_certificate_delivery_method_catalog_items_country_catalog_c~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificate_purpose_catalog_items",
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
                    table.PrimaryKey("pk_certificate_purpose_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_certificate_purpose_catalog_items_country_catalog_country_c~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificate_request_status_catalog_items",
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
                    table.PrimaryKey("pk_certificate_request_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_certificate_request_status_catalog_items_country_catalog_co~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificate_type_catalog_items",
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
                    table.PrimaryKey("pk_certificate_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_certificate_type_catalog_items_country_catalog_country_cata~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_certificate_settings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    logo_file_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issuing_city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    signatory_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    signatory_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    footer_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_certificate_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_certificate_settings__companies",
                        column: x => x.tenant_id,
                        principalTable: "companies",
                        principalColumn: "public_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_certificate_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    certificate_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    type_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    request_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    purpose_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    addressed_to = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delivery_method_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    copies = table.Column<int>(type: "integer", nullable: false),
                    request_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    needed_by_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    response_time_days = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_certificate_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_certificate_requests__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificate_request_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    certificate_request_id = table.Column<long>(type: "bigint", nullable: false),
                    is_system_generated = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_certificate_request_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_certificate_request_documents__request",
                        column: x => x.certificate_request_id,
                        principalTable: "personnel_file_certificate_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "certificate_delivery_method_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9582L, "PORTAL", new Guid("e53826ff-bac3-64de-1101-99cf1693229f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Descarga desde el portal", "PORTAL", "DESCARGA DESDE EL PORTAL", new Guid("d2049f0e-a2b1-e7d0-a0d7-65c024a30180"), 30 },
                    { -9581L, "CORREO_ELECTRONICO", new Guid("965f2e8f-5a25-88ed-60d7-41ca0a7416ec"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Correo electrónico", "CORREO_ELECTRONICO", "CORREO ELECTRÓNICO", new Guid("668e0203-820c-8690-0d65-0ac69016820c"), 20 },
                    { -9580L, "PRESENCIAL", new Guid("ff1804ba-c9be-9dfd-d2f6-d7720a94cbff"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Entrega presencial", "PRESENCIAL", "ENTREGA PRESENCIAL", new Guid("233fa82a-ead0-38c1-a02a-51d5ad509735"), 10 }
                });

            migrationBuilder.InsertData(
                table: "certificate_purpose_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9595L, "OTRO", new Guid("55f4383f-bd4d-1385-cb04-ea349fe710cc"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("8edd2ebd-68aa-b0b1-3d2a-5f9acf62ff16"), 60 },
                    { -9594L, "USO_PERSONAL", new Guid("54d03635-03ed-aa19-f91d-6366c22478f7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uso personal", "USO_PERSONAL", "USO PERSONAL", new Guid("162269e1-c79b-4155-1d24-c0213098a433"), 50 },
                    { -9593L, "TRAMITE_MIGRATORIO", new Guid("f5f59966-8137-f073-a368-1770a280af00"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Trámite migratorio", "TRAMITE_MIGRATORIO", "TRÁMITE MIGRATORIO", new Guid("22af7861-86b4-fbd4-369a-22323ee81aa0"), 40 },
                    { -9592L, "VISA_EMBAJADA", new Guid("f704463a-f72d-3f4f-669b-2ab8b43c5257"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Visa / trámite ante embajada", "VISA_EMBAJADA", "VISA / TRÁMITE ANTE EMBAJADA", new Guid("5e99d45c-e457-6dde-7b11-0ca0f7d888b2"), 30 },
                    { -9591L, "CREDITO", new Guid("8aeae070-6caf-da3c-e14b-0434c049b896"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Solicitud de crédito", "CREDITO", "SOLICITUD DE CRÉDITO", new Guid("5aa88513-151a-ce65-9983-92ba190428db"), 20 },
                    { -9590L, "TRAMITE_BANCARIO", new Guid("ed9bb90b-77fb-e315-1d90-3c4a33be5302"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Trámite bancario", "TRAMITE_BANCARIO", "TRÁMITE BANCARIO", new Guid("6bcfa781-b125-eb76-ff00-4b9ad466572a"), 10 }
                });

            migrationBuilder.InsertData(
                table: "certificate_request_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9575L, "ANULADA", new Guid("109e2a18-ccc8-ba4c-af45-c07d3cb88081"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("316f7895-bc3f-7ce4-f45c-340552828d4f"), 60 },
                    { -9574L, "RECHAZADA", new Guid("29d5cea2-514f-48ff-071e-c31a40c79977"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazada", "RECHAZADA", "RECHAZADA", new Guid("39f91b5f-2d6e-c770-d102-b1d56af8ffbb"), 50 },
                    { -9573L, "ENTREGADA", new Guid("9bf793ec-774c-2103-2037-5ff5b76d595c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Entregada", "ENTREGADA", "ENTREGADA", new Guid("700cdd20-ea3b-f317-61cb-7ad111c26ca4"), 40 },
                    { -9572L, "EMITIDA", new Guid("5d55c49c-a744-0704-09b9-a85125dc8aab"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Emitida", "EMITIDA", "EMITIDA", new Guid("023de009-d9dd-6877-e75b-df12ab12ce6b"), 30 },
                    { -9571L, "EN_PROCESO", new Guid("f717ac4f-423d-5c20-1309-99e9373c9650"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En proceso", "EN_PROCESO", "EN PROCESO", new Guid("0629c185-f9f9-8ad4-1e3b-fc5f8acba304"), 20 },
                    { -9570L, "SOLICITADA", new Guid("679776c0-1ea6-db60-d3fc-a4ab5f3dc944"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Solicitada", "SOLICITADA", "SOLICITADA", new Guid("e19e9698-c905-0400-e03d-30d19ff8f138"), 10 }
                });

            migrationBuilder.InsertData(
                table: "certificate_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9565L, "CARTA_RECOMENDACION", new Guid("f5a9223c-b226-7b13-a794-b0548c46c37f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Carta de recomendación laboral", "CARTA_RECOMENDACION", "CARTA DE RECOMENDACIÓN LABORAL", new Guid("1d3b228f-993e-5e71-9477-9128e98b6ded"), 60 },
                    { -9564L, "CONSTANCIA_NO_DESCUENTO", new Guid("2754557c-ee58-c488-9657-59f4e9170f2a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Constancia de no descuento", "CONSTANCIA_NO_DESCUENTO", "CONSTANCIA DE NO DESCUENTO", new Guid("553bf7f9-632e-aeeb-1bfb-cfe15629d681"), 50 },
                    { -9563L, "CONSTANCIA_TIEMPO_LABORADO", new Guid("c5c13e66-edd3-38f6-4b44-a8a8c1a6e4b4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Constancia de tiempo laborado", "CONSTANCIA_TIEMPO_LABORADO", "CONSTANCIA DE TIEMPO LABORADO", new Guid("ad93568e-a27d-c72f-125c-071a08f0a5ab"), 40 },
                    { -9562L, "CONSTANCIA_EMBAJADA", new Guid("bba6ecbe-0333-e1a5-9760-76d9513ddbd0"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Constancia para embajada", "CONSTANCIA_EMBAJADA", "CONSTANCIA PARA EMBAJADA", new Guid("b8b4d286-5df2-de9f-7c5c-d0b339fa61b9"), 30 },
                    { -9561L, "CONSTANCIA_LABORAL", new Guid("d3eddeae-81f3-7ce4-312c-7ed36b29623a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Constancia de trabajo (laboral)", "CONSTANCIA_LABORAL", "CONSTANCIA DE TRABAJO (LABORAL)", new Guid("65a40f07-4648-1464-1021-cbb9b823c3f5"), 20 },
                    { -9560L, "CONSTANCIA_SALARIO", new Guid("14f134dd-ada4-70b8-c264-4870a52f43c5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Constancia de salario", "CONSTANCIA_SALARIO", "CONSTANCIA DE SALARIO", new Guid("3d5c7ba1-1597-5dcd-66ed-735dc3af241b"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_certificate_delivery_method_catalog_items__active_sort",
                table: "certificate_delivery_method_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_certificate_delivery_method_catalog_items__country_code",
                table: "certificate_delivery_method_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_certificate_delivery_method_catalog_items__public_id",
                table: "certificate_delivery_method_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificate_purpose_catalog_items__country_active_sort",
                table: "certificate_purpose_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_certificate_purpose_catalog_items__country_code",
                table: "certificate_purpose_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_certificate_purpose_catalog_items__public_id",
                table: "certificate_purpose_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificate_request_documents__file_public_id",
                table: "certificate_request_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificate_request_documents__tenant_req_active",
                table: "certificate_request_documents",
                columns: new[] { "tenant_id", "certificate_request_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_request_documents_certificate_request_id",
                table: "certificate_request_documents",
                column: "certificate_request_id");

            migrationBuilder.CreateIndex(
                name: "uq_certificate_request_documents__public_id",
                table: "certificate_request_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificate_request_status_catalog_items__active_sort",
                table: "certificate_request_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_certificate_request_status_catalog_items__country_code",
                table: "certificate_request_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_certificate_request_status_catalog_items__public_id",
                table: "certificate_request_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificate_type_catalog_items__country_active_sort",
                table: "certificate_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_certificate_type_catalog_items__country_code",
                table: "certificate_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_certificate_type_catalog_items__public_id",
                table: "certificate_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_certificate_settings__public_id",
                table: "company_certificate_settings",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_certificate_settings__tenant_id",
                table: "company_certificate_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_certificate_requests__tenant_date",
                table: "personnel_file_certificate_requests",
                columns: new[] { "tenant_id", "request_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_certificate_requests__tenant_file_status",
                table: "personnel_file_certificate_requests",
                columns: new[] { "tenant_id", "personnel_file_id", "request_status_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_certificate_requests_personnel_file_id",
                table: "personnel_file_certificate_requests",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_certificate_requests__public_id",
                table: "personnel_file_certificate_requests",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certificate_delivery_method_catalog_items");

            migrationBuilder.DropTable(
                name: "certificate_purpose_catalog_items");

            migrationBuilder.DropTable(
                name: "certificate_request_documents");

            migrationBuilder.DropTable(
                name: "certificate_request_status_catalog_items");

            migrationBuilder.DropTable(
                name: "certificate_type_catalog_items");

            migrationBuilder.DropTable(
                name: "company_certificate_settings");

            migrationBuilder.DropTable(
                name: "personnel_file_certificate_requests");
        }
    }
}
