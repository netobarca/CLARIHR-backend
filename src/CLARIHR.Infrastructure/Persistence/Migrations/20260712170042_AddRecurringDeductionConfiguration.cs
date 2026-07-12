using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringDeductionConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "recurring_deduction_default_interest_rate_percent",
                table: "company_preferences",
                type: "numeric(9,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recurring_deduct_settle_action_catalog_items",
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
                    table.PrimaryKey("pk_recurring_deduct_settle_action_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_deduct_settle_action_catalog_items_country_catalo~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_deduction_status_catalog_items",
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
                    table.PrimaryKey("pk_recurring_deduction_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_deduction_status_catalog_items_country_catalog_co~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_deduction_type_catalog_items",
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
                    table.PrimaryKey("pk_recurring_deduction_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_deduction_type_catalog_items_country_catalog_coun~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "compensation_concept_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "contribution_cap", "country_catalog_item_id", "country_code", "created_utc", "default_calculation_base_code", "default_calculation_type", "default_deduction_class", "default_employee_rate", "default_employer_rate", "default_pensioned_employer_rate", "is_active", "is_base_salary", "is_statutory", "min_contribution_base", "modified_utc", "name", "nature", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9738L, "PROCURADURIA", new Guid("caafcbc1-06c2-2767-a045-ff6c8e575bd4"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Externo", null, null, null, true, false, false, null, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Procuraduria", "Egreso", "PROCURADURIA", "PROCURADURIA", new Guid("6bc8f11f-13f8-4730-154c-a83882b91da1"), 350 },
                    { -9737L, "COOPERATIVA", new Guid("ac1fda70-62e9-2c80-32c5-c580dd147a00"), null, -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fixed", "Externo", null, null, null, true, false, false, null, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cooperativa", "Egreso", "COOPERATIVA", "COOPERATIVA", new Guid("65e84a62-3064-7dd3-5ef2-71893eafce80"), 340 }
                });

            migrationBuilder.InsertData(
                table: "recurring_deduct_settle_action_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9927L, "CANCELAR", new Guid("dac941ec-7653-ce6f-b135-f9303aee5724"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cancelar al liquidar", "CANCELAR", "CANCELAR AL LIQUIDAR", new Guid("4f8dac92-4baf-a4a2-b2f2-fa20551d0b1e"), 20 },
                    { -9926L, "DESCONTAR_SALDO", new Guid("177c5d50-533b-e299-b42f-4932b40db881"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Descontar saldo al liquidar", "DESCONTAR_SALDO", "DESCONTAR SALDO AL LIQUIDAR", new Guid("279aaf34-1a3b-a189-e7e1-c9b783ed5c38"), 10 }
                });

            migrationBuilder.InsertData(
                table: "recurring_deduction_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9925L, "ANULADO", new Guid("29bcf9f4-37dc-f32e-7ae7-093a6af089a5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulado", "ANULADO", "ANULADO", new Guid("51b19e05-f76f-1567-f5cc-903a7078d8a8"), 60 },
                    { -9924L, "FINALIZADO", new Guid("baccf0d9-dcae-5673-f276-7ab969c334fd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Finalizado", "FINALIZADO", "FINALIZADO", new Guid("44c0f224-5bce-dfa3-bd2a-0dde6533a3d5"), 50 },
                    { -9923L, "SUSPENDIDO", new Guid("6cfd876f-c28b-d518-4dda-f3092ab7eaa1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Suspendido", "SUSPENDIDO", "SUSPENDIDO", new Guid("5eeb6288-e680-ed7c-98e7-8407842d6776"), 40 },
                    { -9922L, "RECHAZADO", new Guid("5c1cda09-2dfb-2d8e-8e74-18e0812e9fa5"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazado", "RECHAZADO", "RECHAZADO", new Guid("86151938-834f-9eb4-71da-06721c274ca8"), 30 },
                    { -9921L, "VIGENTE", new Guid("86cd6d99-f8fc-1df4-2561-3b8b68b36b6e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vigente", "VIGENTE", "VIGENTE", new Guid("57698575-1777-2122-3002-dc03170b24d4"), 20 },
                    { -9920L, "EN_REVISION", new Guid("8cfa89c3-69d0-9433-a1f4-9286ee2e7533"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En revisión", "EN_REVISION", "EN REVISIÓN", new Guid("b0adf496-ddb8-3c8b-af0c-b7a3c0155809"), 10 }
                });

            migrationBuilder.InsertData(
                table: "recurring_deduction_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9934L, "OTRO", new Guid("10c23baa-4dec-3f9f-067a-f141a6f7e6fc"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("c12101b9-0b95-e657-9ec2-37de6550f337"), 50 },
                    { -9933L, "ASOCIACION", new Guid("0c3bd634-1592-8620-4548-ac1995f5dfbb"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Asociación", "ASOCIACION", "ASOCIACIÓN", new Guid("b9b33e47-d136-6f13-6fa5-589b61c8e698"), 40 },
                    { -9932L, "COOPERATIVA", new Guid("51f4dcc3-af4f-829c-13f8-dadb4df1bef0"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cooperativa", "COOPERATIVA", "COOPERATIVA", new Guid("224a0d28-ab68-8103-38c3-76ed3a9ea9cf"), 30 },
                    { -9931L, "PROCURADURIA", new Guid("8e7f04fa-942c-58c1-9730-f9068bfb69ef"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Procuraduría", "PROCURADURIA", "PROCURADURÍA", new Guid("c1baeebf-b7d7-82b9-058e-5023cdea53a2"), 20 },
                    { -9930L, "PRESTAMO_BANCARIO", new Guid("2f7ab5bd-6d6f-df98-2653-a61e7aa083c2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Préstamo bancario", "PRESTAMO_BANCARIO", "PRÉSTAMO BANCARIO", new Guid("51a191f6-7431-75fa-b71b-104e949b9c26"), 10 }
                });

            migrationBuilder.InsertData(
                table: "settlement_concept_catalog_items",
                columns: new[] { "id", "affects_afp", "affects_isss", "affects_renta", "code", "concept_class", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "default_rate_percent", "exemption_multiplier", "exemption_rule", "is_active", "is_system_calculated", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[] { -9928L, false, false, false, "DESCUENTO_CICLICO_PENDIENTE", "Descuento", new Guid("daa00043-5061-dc71-4ae4-55e0c5a02bd0"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Ninguna", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Descuento cíclico pendiente", "DESCUENTO_CICLICO_PENDIENTE", "DESCUENTO CÍCLICO PENDIENTE", new Guid("18acfab4-3bdc-9eea-6c37-0e0b6eac572b"), 135 });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_deduct_settle_action_catalog_items__active_sort",
                table: "recurring_deduct_settle_action_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_recurring_deduct_settle_action_catalog_items__country_code",
                table: "recurring_deduct_settle_action_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_recurring_deduct_settle_action_catalog_items__public_id",
                table: "recurring_deduct_settle_action_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_deduction_status_catalog_items__active_sort",
                table: "recurring_deduction_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_recurring_deduction_status_catalog_items__country_code",
                table: "recurring_deduction_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_recurring_deduction_status_catalog_items__public_id",
                table: "recurring_deduction_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_deduction_type_catalog_items__country_active_sort",
                table: "recurring_deduction_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_recurring_deduction_type_catalog_items__country_code",
                table: "recurring_deduction_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_recurring_deduction_type_catalog_items__public_id",
                table: "recurring_deduction_type_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurring_deduct_settle_action_catalog_items");

            migrationBuilder.DropTable(
                name: "recurring_deduction_status_catalog_items");

            migrationBuilder.DropTable(
                name: "recurring_deduction_type_catalog_items");

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9738L);

            migrationBuilder.DeleteData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9737L);

            migrationBuilder.DeleteData(
                table: "settlement_concept_catalog_items",
                keyColumn: "id",
                keyValue: -9928L);

            migrationBuilder.DropColumn(
                name: "recurring_deduction_default_interest_rate_percent",
                table: "company_preferences");
        }
    }
}
