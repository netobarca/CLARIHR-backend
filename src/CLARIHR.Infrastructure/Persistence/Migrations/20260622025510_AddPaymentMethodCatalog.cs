using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_method_catalog_items",
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
                    table.PrimaryKey("pk_payment_method_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_method_catalog_items_country_catalog_country_catalo~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_method_catalog_items__country_active_sort",
                table: "payment_method_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_payment_method_catalog_items__country_code",
                table: "payment_method_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payment_method_catalog_items__public_id",
                table: "payment_method_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_method_catalog_items");
        }
    }
}
