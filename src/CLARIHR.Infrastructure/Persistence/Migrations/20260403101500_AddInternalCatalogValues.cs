using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260403101500_AddInternalCatalogValues")]
    public partial class AddInternalCatalogValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "internal_catalog_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    catalog_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    last_used_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_internal_catalog_values", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_internal_catalog_values__catalog_key_active",
                table: "internal_catalog_values",
                columns: new[] { "catalog_key", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_internal_catalog_values__catalog_key_normalized_value",
                table: "internal_catalog_values",
                columns: new[] { "catalog_key", "normalized_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_internal_catalog_values__public_id",
                table: "internal_catalog_values",
                column: "public_id",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX ix_internal_catalog_values__normalized_value_trgm
                ON internal_catalog_values
                USING gin (normalized_value gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_internal_catalog_values__normalized_value_trgm;");

            migrationBuilder.DropTable(
                name: "internal_catalog_values");
        }
    }
}
