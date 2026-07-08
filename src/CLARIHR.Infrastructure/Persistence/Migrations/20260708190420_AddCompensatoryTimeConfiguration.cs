using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensatoryTimeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "compensatory_time_credit_requires_document",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "compensatory_time_max_balance_hours",
                table: "company_preferences",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "compensatory_time_settlement_rate_factor",
                table: "company_preferences",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "compensatory_time_standard_daily_hours",
                table: "company_preferences",
                type: "numeric(4,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "compensatory_time_operation_catalog_items",
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
                    table.PrimaryKey("pk_compensatory_time_operation_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_compensatory_time_operation_catalog_items_country_catalog_c~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compensatory_time_status_catalog_items",
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
                    table.PrimaryKey("pk_compensatory_time_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_compensatory_time_status_catalog_items_country_catalog_coun~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compensatory_time_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    operation_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    credit_factor = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compensatory_time_types", x => x.id);
                    table.CheckConstraint("ck_compensatory_time_types__credit_factor_positive", "credit_factor > 0");
                });

            migrationBuilder.InsertData(
                table: "action_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9871L, "GOCE_TIEMPO_COMPENSATORIO", new Guid("76d0771a-3fec-95c8-c86b-e7ab38466a57"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Goce de tiempo compensatorio", "GOCE_TIEMPO_COMPENSATORIO", "GOCE DE TIEMPO COMPENSATORIO", new Guid("00b85f20-b2cd-bb4d-17df-08c9634f6e66"), 220 },
                    { -9870L, "ACREDITACION_TIEMPO_COMPENSATORIO", new Guid("1a274fcc-2755-343a-9e39-6fdd985dcbc2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Acreditación de tiempo compensatorio", "ACREDITACION_TIEMPO_COMPENSATORIO", "ACREDITACIÓN DE TIEMPO COMPENSATORIO", new Guid("24560350-f9bc-8727-997f-a81f1d0a39b9"), 210 }
                });

            migrationBuilder.InsertData(
                table: "compensatory_time_operation_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9869L, "AMBAS", new Guid("e2b5fbbd-b01d-5fa7-f444-3b8bccd908a4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ambas", "AMBAS", "AMBAS", new Guid("37cbdb8d-deca-c663-5322-093e3d077675"), 30 },
                    { -9868L, "DEBITA", new Guid("03a342a6-f8c8-6782-31ee-c8e6606e6e98"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Debita", "DEBITA", "DEBITA", new Guid("b982632c-475c-5472-1197-62f718808ce0"), 20 },
                    { -9867L, "ACREDITA", new Guid("cc737fe7-5807-33b3-30ef-906770ee80c7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Acredita", "ACREDITA", "ACREDITA", new Guid("76d60499-b184-7506-5c23-ef0788031ab5"), 10 }
                });

            migrationBuilder.InsertData(
                table: "compensatory_time_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9866L, "ANULADA", new Guid("64ee13bc-277a-00e5-8276-e2b9c1ff2283"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("384503f9-100f-c53a-2ac8-b6ee157d7f60"), 20 },
                    { -9865L, "REGISTRADA", new Guid("10acc706-29a3-b3c6-38db-da3402acb745"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Registrada", "REGISTRADA", "REGISTRADA", new Guid("057e2104-0607-c8d3-fcf6-40778638e290"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_compensatory_time_operation_catalog_items__active_sort",
                table: "compensatory_time_operation_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_compensatory_time_operation_catalog_items__country_code",
                table: "compensatory_time_operation_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_compensatory_time_operation_catalog_items__public_id",
                table: "compensatory_time_operation_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_compensatory_time_status_catalog_items__country_active_sort",
                table: "compensatory_time_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_compensatory_time_status_catalog_items__country_code",
                table: "compensatory_time_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_compensatory_time_status_catalog_items__public_id",
                table: "compensatory_time_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_compensatory_time_types__tenant_active",
                table: "compensatory_time_types",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_compensatory_time_types__public_id",
                table: "compensatory_time_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_compensatory_time_types__tenant_code_active",
                table: "compensatory_time_types",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compensatory_time_operation_catalog_items");

            migrationBuilder.DropTable(
                name: "compensatory_time_status_catalog_items");

            migrationBuilder.DropTable(
                name: "compensatory_time_types");

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9871L);

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9870L);

            migrationBuilder.DropColumn(
                name: "compensatory_time_credit_requires_document",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "compensatory_time_max_balance_hours",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "compensatory_time_settlement_rate_factor",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "compensatory_time_standard_daily_hours",
                table: "company_preferences");
        }
    }
}
