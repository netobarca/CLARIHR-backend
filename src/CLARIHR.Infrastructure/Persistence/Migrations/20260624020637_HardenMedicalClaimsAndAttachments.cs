using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenMedicalClaimsAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "insurance_public_id",
                table: "personnel_file_medical_claims",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "beneficiary_public_id",
                table: "personnel_file_medical_claims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "claim_status_code",
                table: "personnel_file_medical_claims",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "claimant_type",
                table: "personnel_file_medical_claims",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "TITULAR");

            migrationBuilder.AddColumn<string>(
                name: "insurance_name_snapshot",
                table: "personnel_file_medical_claims",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kinship_code_snapshot",
                table: "personnel_file_medical_claims",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_name_snapshot",
                table: "personnel_file_medical_claims",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolution_date_utc",
                table: "personnel_file_medical_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "medical_claim_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    medical_claim_id = table.Column<long>(type: "bigint", nullable: false),
                    document_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_medical_claim_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_medical_claim_documents__document_type_catalog_item",
                        column: x => x.document_type_catalog_item_id,
                        principalTable: "document_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_claim_documents__medical_claim",
                        column: x => x.medical_claim_id,
                        principalTable: "personnel_file_medical_claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medical_claim_status_catalog_items",
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
                    table.PrimaryKey("pk_medical_claim_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_medical_claim_status_catalog_items_country_catalog_country_~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medical_claim_type_catalog_items",
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
                    table.PrimaryKey("pk_medical_claim_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_medical_claim_type_catalog_items_country_catalog_country_ca~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_medical_claims__insurance_public_id",
                table: "personnel_file_medical_claims",
                column: "insurance_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_medical_claim_documents__document_type_catalog_item_id",
                table: "medical_claim_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_medical_claim_documents__file_public_id",
                table: "medical_claim_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_medical_claim_documents__tenant_claim_active",
                table: "medical_claim_documents",
                columns: new[] { "tenant_id", "medical_claim_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_medical_claim_documents_medical_claim_id",
                table: "medical_claim_documents",
                column: "medical_claim_id");

            migrationBuilder.CreateIndex(
                name: "uq_medical_claim_documents__public_id",
                table: "medical_claim_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_medical_claim_status_catalog_items__country_active_sort",
                table: "medical_claim_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_medical_claim_status_catalog_items__country_code",
                table: "medical_claim_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_medical_claim_status_catalog_items__public_id",
                table: "medical_claim_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_medical_claim_type_catalog_items__country_active_sort",
                table: "medical_claim_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_medical_claim_type_catalog_items__country_code",
                table: "medical_claim_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_medical_claim_type_catalog_items__public_id",
                table: "medical_claim_type_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_claim_documents");

            migrationBuilder.DropTable(
                name: "medical_claim_status_catalog_items");

            migrationBuilder.DropTable(
                name: "medical_claim_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_personnel_file_medical_claims__insurance_public_id",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "beneficiary_public_id",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "claim_status_code",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "claimant_type",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "insurance_name_snapshot",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "kinship_code_snapshot",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "patient_name_snapshot",
                table: "personnel_file_medical_claims");

            migrationBuilder.DropColumn(
                name: "resolution_date_utc",
                table: "personnel_file_medical_claims");

            migrationBuilder.AlterColumn<Guid>(
                name: "insurance_public_id",
                table: "personnel_file_medical_claims",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
