using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_account_type_catalog_items",
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
                    table.PrimaryKey("pk_bank_account_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_account_type_catalog_items_country_catalog_country_cat~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "bank_account_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9794L, "OTRO", new Guid("c556c32d-e271-6ba0-980a-5b890e090064"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("bc243323-758f-9f00-8922-6703d9400b62"), 50 },
                    { -9793L, "A_LA_VISTA", new Guid("153c13c1-a323-4e05-4813-7cdd29d7f684"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuenta a la vista", "A_LA_VISTA", "CUENTA A LA VISTA", new Guid("e08c5a95-a29f-9aa4-b760-7accd997afd5"), 40 },
                    { -9792L, "PLANILLA", new Guid("a09b3460-76c1-63e0-bf78-2bea77e06439"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuenta de planilla", "PLANILLA", "CUENTA DE PLANILLA", new Guid("30a98fdf-9350-4afa-615f-f3c2f619f087"), 30 },
                    { -9791L, "CORRIENTE", new Guid("c0e383be-dfd5-cfe8-efad-db1313409e10"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuenta corriente", "CORRIENTE", "CUENTA CORRIENTE", new Guid("566fd93e-3930-c8a7-33d4-b66dc83ee7d2"), 20 },
                    { -9790L, "AHORRO", new Guid("4cf629be-ce00-51de-6e4c-de1477d3c1e6"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuenta de ahorro", "AHORRO", "CUENTA DE AHORRO", new Guid("76a1fc1f-9f63-3f0b-29d9-91daefc4b6d7"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_account_type_catalog_items__country_active_sort",
                table: "bank_account_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_bank_account_type_catalog_items__country_code",
                table: "bank_account_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_bank_account_type_catalog_items__public_id",
                table: "bank_account_type_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_account_type_catalog_items");
        }
    }
}
