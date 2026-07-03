using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichContractTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "abbreviation",
                table: "contract_type_catalog_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_temporary",
                table: "contract_type_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9467L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "OTRO", false });

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9466L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "TEMP", true });

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9465L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "SP", false });

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9464L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "APREN", true });

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9463L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "EVEN", true });

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9462L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "OBRA", true });

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9461L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "PF", true });

            migrationBuilder.UpdateData(
                table: "contract_type_catalog_items",
                keyColumn: "id",
                keyValue: -9460L,
                columns: new[] { "abbreviation", "is_temporary" },
                values: new object[] { "INDEF", false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "abbreviation",
                table: "contract_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "is_temporary",
                table: "contract_type_catalog_items");
        }
    }
}
