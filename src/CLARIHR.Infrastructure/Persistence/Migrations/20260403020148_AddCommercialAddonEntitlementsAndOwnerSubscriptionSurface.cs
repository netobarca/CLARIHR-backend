using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialAddonEntitlementsAndOwnerSubscriptionSurface : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commercial_addon_entitlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    commercial_addon_id = table.Column<long>(type: "bigint", nullable: false),
                    addon_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    module_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_addon_entitlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_commercial_addon_entitlements__commercial_addons",
                        column: x => x.commercial_addon_id,
                        principalTable: "commercial_addons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_commercial_addon_entitlements__addon_module",
                table: "commercial_addon_entitlements",
                columns: new[] { "commercial_addon_id", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_addon_entitlements__public_id",
                table: "commercial_addon_entitlements",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commercial_addon_entitlements");
        }
    }
}
