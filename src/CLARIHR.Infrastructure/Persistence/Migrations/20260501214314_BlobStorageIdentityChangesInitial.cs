using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlobStorageIdentityChangesInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_url",
                table: "personnel_files");

            migrationBuilder.AddColumn<Guid>(
                name: "photo_file_public_id",
                table: "personnel_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "files",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    container_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    object_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    visibility = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    upload_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    upload_confirmed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_files", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_files__owner_purpose",
                table: "files",
                columns: new[] { "created_by_user_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "ix_files__status_created_at",
                table: "files",
                columns: new[] { "status", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_files__tenant_public",
                table: "files",
                columns: new[] { "tenant_id", "public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_files__public_id",
                table: "files",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_files__provider_object_key",
                table: "files",
                columns: new[] { "provider", "object_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "files");

            migrationBuilder.DropColumn(
                name: "photo_file_public_id",
                table: "personnel_files");

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                table: "personnel_files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
