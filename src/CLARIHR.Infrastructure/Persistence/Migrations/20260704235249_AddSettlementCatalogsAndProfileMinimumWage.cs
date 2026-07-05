using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementCatalogsAndProfileMinimumWage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "minimum_monthly_wage",
                table: "personnel_file_employee_profiles",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "settlement_concept_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    concept_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    affects_isss = table.Column<bool>(type: "boolean", nullable: false),
                    affects_afp = table.Column<bool>(type: "boolean", nullable: false),
                    affects_renta = table.Column<bool>(type: "boolean", nullable: false),
                    exemption_rule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    exemption_multiplier = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
                    is_system_calculated = table.Column<bool>(type: "boolean", nullable: false),
                    default_rate_percent = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
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
                    table.PrimaryKey("pk_settlement_concept_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_settlement_concept_catalog_items_country_catalog_country_ca~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "settlement_status_catalog_items",
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
                    table.PrimaryKey("pk_settlement_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_settlement_status_catalog_items_country_catalog_country_cat~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "action_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9484L, "LIQUIDACION", new Guid("9a96edb4-e766-44c0-fa59-9dc62b59f335"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Liquidación de personal", "LIQUIDACION", "LIQUIDACIÓN DE PERSONAL", new Guid("e85f50ea-062f-8c17-085b-870a8af6b4de"), 150 });

            migrationBuilder.InsertData(
                table: "settlement_concept_catalog_items",
                columns: new[] { "id", "affects_afp", "affects_isss", "affects_renta", "code", "concept_class", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "default_rate_percent", "exemption_multiplier", "exemption_rule", "is_active", "is_system_calculated", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9846L, false, false, false, "INCAF", "PagoPatronal", new Guid("9075206e-0ea3-1ed3-6347-dc6a2dea2833"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), 1.00m, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "INCAF (ex-INSAFORP)", "INCAF", "INCAF (EX-INSAFORP)", new Guid("5275add1-685d-74ab-c3b7-23bc695ef5fb"), 220 },
                    { -9845L, false, false, false, "AFP_PATRONAL", "PagoPatronal", new Guid("8ac7487e-ba15-fbdd-bf74-d3e2488df32e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), 8.75m, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AFP patronal", "AFP_PATRONAL", "AFP PATRONAL", new Guid("7e082dcb-1860-ebcb-3bd9-75c7740330f8"), 210 },
                    { -9844L, false, false, false, "ISSS_PATRONAL", "PagoPatronal", new Guid("061003a9-8945-181d-eee3-56ebb8afc8f8"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), 7.50m, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ISSS patronal", "ISSS_PATRONAL", "ISSS PATRONAL", new Guid("efbbfb19-46cc-4477-3fdd-82a525cfddc5"), 200 },
                    { -9843L, false, false, false, "OTRO_DESCUENTO", "Descuento", new Guid("5741d7a3-258f-1e00-3df7-d7cc9b40492d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro descuento", "OTRO_DESCUENTO", "OTRO DESCUENTO", new Guid("c5ca97e5-640f-ac8e-32b5-75a1b88732a3"), 140 },
                    { -9842L, false, false, false, "DESCUENTO_EXTERNO", "Descuento", new Guid("10940f9e-3537-fba1-e237-7ea917533936"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Descuento externo (última cuota)", "DESCUENTO_EXTERNO", "DESCUENTO EXTERNO (ÚLTIMA CUOTA)", new Guid("a6604390-c5e8-cdd3-2883-287bfac8bdd5"), 130 },
                    { -9841L, false, false, false, "RENTA", "Descuento", new Guid("a9995ae2-294d-4548-01c2-3dfdf2d1ba16"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Renta (retención ISR)", "RENTA", "RENTA (RETENCIÓN ISR)", new Guid("052aabe5-5d92-7c7a-2f70-c40d7c6b23a0"), 120 },
                    { -9840L, false, false, false, "AFP", "Descuento", new Guid("0e151374-83c0-a90b-09fb-db777ec0669f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AFP (cotización del empleado)", "AFP", "AFP (COTIZACIÓN DEL EMPLEADO)", new Guid("40bf5d03-4691-44b4-efbf-d2827fcf4305"), 110 },
                    { -9839L, false, false, false, "ISSS", "Descuento", new Guid("2673296a-077e-2b7f-0172-2798c005ec1d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ISSS (cotización del empleado)", "ISSS", "ISSS (COTIZACIÓN DEL EMPLEADO)", new Guid("e0789a1e-e430-f56b-04d7-2d8c7ec10f59"), 100 },
                    { -9838L, true, true, true, "OTRO_INGRESO", "Ingreso", new Guid("d34fc437-8809-0d8c-3df3-00a71845a23e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro ingreso", "OTRO_INGRESO", "OTRO INGRESO", new Guid("681ace21-559f-4ccd-a6d8-eed084eaea76"), 90 },
                    { -9837L, true, true, true, "HORAS_EXTRAS_PENDIENTES", "Ingreso", new Guid("96678f2b-56d0-646f-d6d3-2b67e0f50939"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Horas extras pendientes", "HORAS_EXTRAS_PENDIENTES", "HORAS EXTRAS PENDIENTES", new Guid("187b2e65-a07c-7105-67ee-cc6e07a1c8f2"), 80 },
                    { -9836L, true, true, true, "COMISION_PENDIENTE", "Ingreso", new Guid("8e279ee3-69b7-dd96-9bf6-d92b5af0c1a2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Comisión pendiente", "COMISION_PENDIENTE", "COMISIÓN PENDIENTE", new Guid("3c3be5b5-13a5-ad53-b3e8-0a4748e26c37"), 70 },
                    { -9835L, true, true, true, "BONO_PENDIENTE", "Ingreso", new Guid("074cee65-def5-3be6-87d9-36994dffcce9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bono pendiente", "BONO_PENDIENTE", "BONO PENDIENTE", new Guid("be0ea74c-8640-2695-06d8-c4253c228975"), 60 },
                    { -9834L, false, false, true, "RENUNCIA_VOLUNTARIA", "Ingreso", new Guid("503c9398-50af-cde9-9513-42be8be0d4cf"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "HastaMontoLegal", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Compensación económica por renuncia voluntaria", "RENUNCIA_VOLUNTARIA", "COMPENSACIÓN ECONÓMICA POR RENUNCIA VOLUNTARIA", new Guid("0fa2e40e-10c9-8106-d53a-b94b7d7ae11b"), 50 },
                    { -9833L, false, false, true, "INDEMNIZACION", "Ingreso", new Guid("fdd9bcc3-b388-9422-1540-1d18a21fe049"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "HastaMontoLegal", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Indemnización", "INDEMNIZACION", "INDEMNIZACIÓN", new Guid("04b32d3e-0393-781f-e06f-846a9c203dab"), 40 },
                    { -9832L, false, false, true, "AGUINALDO_PROPORCIONAL", "Ingreso", new Guid("3e03670d-e103-3196-cea5-4feeac0b8182"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, 2.00m, "HastaLimitePorMinimo", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aguinaldo proporcional", "AGUINALDO_PROPORCIONAL", "AGUINALDO PROPORCIONAL", new Guid("422cb0ad-340c-6998-d5a0-9cf427062d1a"), 30 },
                    { -9831L, true, true, true, "VACACION_PROPORCIONAL", "Ingreso", new Guid("2f3a7637-672c-c35a-081e-e48194762964"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vacación proporcional", "VACACION_PROPORCIONAL", "VACACIÓN PROPORCIONAL", new Guid("40909c4f-82ef-1d30-1be0-aa5aad4dd7a5"), 20 },
                    { -9830L, true, true, true, "SALARIO", "Ingreso", new Guid("81c21853-3ce1-66b0-adc8-1d807c73655e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Salario pendiente", "SALARIO", "SALARIO PENDIENTE", new Guid("dec5595d-f4eb-b509-8fbf-8827ce57c820"), 10 }
                });

            migrationBuilder.InsertData(
                table: "settlement_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9822L, "ANULADA", new Guid("d78b962f-3956-dfce-8b29-b313b05add5d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("12680930-e922-d6de-5eb6-aae627fad0b0"), 30 },
                    { -9821L, "EMITIDA", new Guid("317085f5-7ab8-ffb6-9d07-36ade1d1d2d1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Emitida", "EMITIDA", "EMITIDA", new Guid("89e5bf45-21a0-446d-507f-aa196add53f2"), 20 },
                    { -9820L, "BORRADOR", new Guid("d948c901-d36e-0875-9c00-0da885f640fe"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Borrador", "BORRADOR", "BORRADOR", new Guid("6c195ba4-9d52-cefd-a882-645158016a5d"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_settlement_concept_catalog_items__country_active_sort",
                table: "settlement_concept_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_settlement_concept_catalog_items__country_class_active",
                table: "settlement_concept_catalog_items",
                columns: new[] { "country_catalog_item_id", "concept_class", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_settlement_concept_catalog_items__country_code",
                table: "settlement_concept_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_settlement_concept_catalog_items__public_id",
                table: "settlement_concept_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_settlement_status_catalog_items__country_active_sort",
                table: "settlement_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_settlement_status_catalog_items__country_code",
                table: "settlement_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_settlement_status_catalog_items__public_id",
                table: "settlement_status_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settlement_concept_catalog_items");

            migrationBuilder.DropTable(
                name: "settlement_status_catalog_items");

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9484L);

            migrationBuilder.DropColumn(
                name: "minimum_monthly_wage",
                table: "personnel_file_employee_profiles");
        }
    }
}
