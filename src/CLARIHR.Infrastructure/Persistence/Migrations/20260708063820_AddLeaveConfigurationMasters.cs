using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveConfigurationMasters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinic_sector_catalog_items",
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
                    table.PrimaryKey("pk_clinic_sector_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_clinic_sector_catalog_items_country_catalog_country_catalog~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_holidays",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scope_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_holidays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incapacity_risks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    counts_seventh_day = table.Column<bool>(type: "boolean", nullable: false),
                    counts_saturday = table.Column<bool>(type: "boolean", nullable: false),
                    counts_holiday = table.Column<bool>(type: "boolean", nullable: false),
                    uses_work_schedule = table.Column<bool>(type: "boolean", nullable: false),
                    allows_indefinite = table.Column<bool>(type: "boolean", nullable: false),
                    allows_extension = table.Column<bool>(type: "boolean", nullable: false),
                    uses_fund = table.Column<bool>(type: "boolean", nullable: false),
                    has_subsidy = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incapacity_risks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incapacity_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    deduction_type_text = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    income_type_text = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    applies_to_work_accident = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incapacity_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "medical_clinics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    specialty = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    sector_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medical_clinics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_period_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pay_period_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_period_definitions", x => x.id);
                    table.CheckConstraint("ck_payroll_period_definitions__dates", "end_date >= start_date");
                });

            migrationBuilder.CreateTable(
                name: "incapacity_risk_parameters",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incapacity_risk_id = table.Column<long>(type: "bigint", nullable: false),
                    day_from = table.Column<int>(type: "integer", nullable: false),
                    day_to = table.Column<int>(type: "integer", nullable: true),
                    subsidy_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    payer_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incapacity_risk_parameters", x => x.id);
                    table.CheckConstraint("ck_incapacity_risk_parameters__bounds", "day_to is null or day_to >= day_from");
                    table.ForeignKey(
                        name: "fk_incapacity_risk_parameters__incapacity_risks",
                        column: x => x.incapacity_risk_id,
                        principalTable: "incapacity_risks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "clinic_sector_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9862L, "PRIVADA", new Guid("211ceccb-9496-678a-a83c-2b4bab7da166"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Privada", "PRIVADA", "PRIVADA", new Guid("f7de1fef-d3d0-0966-f987-1af4944142fd"), 30 },
                    { -9861L, "PUBLICA", new Guid("e18e5f3b-a2aa-a414-76b2-9e583dc3e9fb"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pública", "PUBLICA", "PÚBLICA", new Guid("706f0a7f-e5ac-41fe-ccc9-38f3af1729b8"), 20 },
                    { -9860L, "ISSS", new Guid("1f22bfe4-a008-642a-270b-9a09d864fb2d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ISSS", "ISSS", "ISSS", new Guid("ea3e46ad-9afa-5504-bc49-3db450ecbac8"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinic_sector_catalog_items__country_active_sort",
                table: "clinic_sector_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_clinic_sector_catalog_items__country_code",
                table: "clinic_sector_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_clinic_sector_catalog_items__public_id",
                table: "clinic_sector_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_holidays__public_id",
                table: "company_holidays",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_holidays__tenant_date",
                table: "company_holidays",
                columns: new[] { "tenant_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_incapacity_risk_parameters__risk_sort",
                table: "incapacity_risk_parameters",
                columns: new[] { "tenant_id", "incapacity_risk_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_incapacity_risk_parameters_incapacity_risk_id",
                table: "incapacity_risk_parameters",
                column: "incapacity_risk_id");

            migrationBuilder.CreateIndex(
                name: "uq_incapacity_risk_parameters__public_id",
                table: "incapacity_risk_parameters",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_incapacity_risks__tenant_active",
                table: "incapacity_risks",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_incapacity_risks__public_id",
                table: "incapacity_risks",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_incapacity_risks__tenant_code",
                table: "incapacity_risks",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_incapacity_types__public_id",
                table: "incapacity_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_incapacity_types__tenant_code",
                table: "incapacity_types",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_medical_clinics__tenant_active",
                table: "medical_clinics",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_medical_clinics__public_id",
                table: "medical_clinics",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_medical_clinics__tenant_description",
                table: "medical_clinics",
                columns: new[] { "tenant_id", "normalized_description" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payroll_period_definitions__tenant_start",
                table: "payroll_period_definitions",
                columns: new[] { "tenant_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "uq_payroll_period_definitions__public_id",
                table: "payroll_period_definitions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payroll_period_definitions__tenant_type_year_number",
                table: "payroll_period_definitions",
                columns: new[] { "tenant_id", "pay_period_type_code", "year", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinic_sector_catalog_items");

            migrationBuilder.DropTable(
                name: "company_holidays");

            migrationBuilder.DropTable(
                name: "incapacity_risk_parameters");

            migrationBuilder.DropTable(
                name: "incapacity_types");

            migrationBuilder.DropTable(
                name: "medical_clinics");

            migrationBuilder.DropTable(
                name: "payroll_period_definitions");

            migrationBuilder.DropTable(
                name: "incapacity_risks");
        }
    }
}
