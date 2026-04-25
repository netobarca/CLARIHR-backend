using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "bank_catalog_item_id",
                table: "personnel_file_bank_accounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bank_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    alias = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    normalized_alias = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    swift_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    normalized_swift_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    routing_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    normalized_routing_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("pk_bank_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_catalog_items_country_catalog_country_catalog_item_id",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "bank_catalog_items",
                columns: new[] { "id", "alias", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_alias", "normalized_code", "normalized_name", "normalized_routing_code", "normalized_swift_code", "public_id", "routing_code", "sort_order", "swift_code" },
                values: new object[,]
                {
                    { -9013L, "JPMorgan Chase", "CHASE", new Guid("4f899240-065d-4c4a-e707-a16b3d0f2019"), -7236L, "US", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chase", "JPMORGAN CHASE", "CHASE", "CHASE", null, null, new Guid("8d01001e-aa15-8db9-030e-291eff3371c6"), null, 113, null },
                    { -9012L, "Wells", "WELLS_FARGO", new Guid("13a0dfeb-d819-4a0f-fe4d-19c1810f9026"), -7236L, "US", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Wells Fargo", "WELLS", "WELLS_FARGO", "WELLS FARGO", null, null, new Guid("5593cf15-9751-fc6f-5da2-f72d69ab96ab"), null, 112, null },
                    { -9011L, "Citi", "CITIBANK", new Guid("f8bfab4f-1615-85e4-2dc9-3ffce9cab61c"), -7236L, "US", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Citibank", "CITI", "CITIBANK", "CITIBANK", null, null, new Guid("449115f8-73f8-4fa8-3aec-b1be908cbb0d"), null, 111, null },
                    { -9010L, "BofA", "BANK_OF_AMERICA", new Guid("5f2b6709-37e6-81af-9a13-6e64dacdc5d3"), -7236L, "US", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bank of America", "BOFA", "BANK_OF_AMERICA", "BANK OF AMERICA", null, null, new Guid("d8348c78-80e3-8a20-7320-559b8283620a"), null, 110, null },
                    { -9003L, "BAC", "BAC", new Guid("3029e498-aa25-eee2-f2ed-8d9451f6cc46"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "BAC Credomatic", "BAC", "BAC", "BAC CREDOMATIC", null, null, new Guid("9ed2bcd0-9145-7de9-1d28-ad10d67a68e1"), null, 103, null },
                    { -9002L, "Cuscatlan", "CUSCATLAN", new Guid("33552f9e-df6e-f9ef-6c8f-004fd4a8495e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuscatlan", "CUSCATLAN", "CUSCATLAN", "CUSCATLAN", null, null, new Guid("5c08e418-a564-818e-628e-793c9c769e6d"), null, 102, null },
                    { -9001L, "Davivienda", "DAVIVIENDA", new Guid("c7e88730-fa31-41ab-b842-d0eb5d3587d5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Davivienda", "DAVIVIENDA", "DAVIVIENDA", "DAVIVIENDA", null, null, new Guid("ccef7f91-db96-6be7-3ae1-e3328495d536"), null, 101, null },
                    { -9000L, "Agricola", "BANCO_AGRICOLA", new Guid("0d78d96e-f3c3-6cc3-df75-7aa0d20d5743"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Banco Agricola", "AGRICOLA", "BANCO_AGRICOLA", "AGRICOLA", null, null, new Guid("c242c10d-3457-f77a-77cb-055689206e26"), null, 100, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_bank_accounts__bank_catalog_item_id",
                table: "personnel_file_bank_accounts",
                column: "bank_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_catalog_items__country_active_sort",
                table: "bank_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_catalog_items__country_alias",
                table: "bank_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_alias" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_catalog_items__country_name",
                table: "bank_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_catalog_items__country_routing",
                table: "bank_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_routing_code" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_catalog_items__country_swift",
                table: "bank_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_swift_code" });

            migrationBuilder.CreateIndex(
                name: "uq_bank_catalog_items__country_code",
                table: "bank_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_bank_catalog_items__public_id",
                table: "bank_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_bank_accounts__bank_catalog_item",
                table: "personnel_file_bank_accounts",
                column: "bank_catalog_item_id",
                principalTable: "bank_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_bank_accounts__bank_catalog_item",
                table: "personnel_file_bank_accounts");

            migrationBuilder.DropTable(
                name: "bank_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_personnel_file_bank_accounts__bank_catalog_item_id",
                table: "personnel_file_bank_accounts");

            migrationBuilder.DropColumn(
                name: "bank_catalog_item_id",
                table: "personnel_file_bank_accounts");
        }
    }
}
