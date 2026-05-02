using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilePublicIdColumnAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blob_name",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "blob_url",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "sha256",
                table: "personnel_file_documents");

            migrationBuilder.AddColumn<long>(
                name: "document_type_catalog_item_id",
                table: "personnel_file_documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "file_public_id",
                table: "personnel_file_documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "document_type_catalog_items",
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
                    table.PrimaryKey("pk_document_type_catalog_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_documents__document_type_catalog_item_id",
                table: "personnel_file_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_documents__file_public_id",
                table: "personnel_file_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_type_catalog_items__active_sort",
                table: "document_type_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_document_type_catalog_items__code",
                table: "document_type_catalog_items",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_document_type_catalog_items__public_id",
                table: "document_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_documents__document_type_catalog_item",
                table: "personnel_file_documents",
                column: "document_type_catalog_item_id",
                principalTable: "document_type_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_documents__document_type_catalog_item",
                table: "personnel_file_documents");

            migrationBuilder.DropTable(
                name: "document_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_personnel_file_documents__document_type_catalog_item_id",
                table: "personnel_file_documents");

            migrationBuilder.DropIndex(
                name: "ix_personnel_file_documents__file_public_id",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "document_type_catalog_item_id",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "file_public_id",
                table: "personnel_file_documents");

            migrationBuilder.AddColumn<string>(
                name: "blob_name",
                table: "personnel_file_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "blob_url",
                table: "personnel_file_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sha256",
                table: "personnel_file_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}
