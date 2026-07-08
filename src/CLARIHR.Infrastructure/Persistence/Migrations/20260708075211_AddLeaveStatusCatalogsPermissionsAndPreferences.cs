using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveStatusCatalogsPermissionsAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "rest_day_of_week",
                table: "personnel_file_employment_assignments",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "additional_incapacity_benefit_days_per_year",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "additional_vacation_benefit_days_default",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_vacation_end_on_holiday",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_vacation_start_on_holiday",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_vacation_start_on_rest_day",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "annual_vacation_days_default",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "company_rest_day_of_week",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "default_use_anniversary",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "employer_covered_incapacity_days_per_year",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "incapacity_requires_document",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "incapacity_status_catalog_items",
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
                    table.PrimaryKey("pk_incapacity_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_incapacity_status_catalog_items_country_catalog_country_cat~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacation_request_status_catalog_items",
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
                    table.PrimaryKey("pk_vacation_request_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_vacation_request_status_catalog_items_country_catalog_count~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "action_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9489L, "DEVOLUCION_VACACIONES", new Guid("d06d3692-8c3c-6b4f-b853-acad8f3abd97"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Devolución de vacaciones", "DEVOLUCION_VACACIONES", "DEVOLUCIÓN DE VACACIONES", new Guid("d8319eaf-e51b-5205-9aec-3abfd36bf632"), 200 },
                    { -9488L, "GOCE_VACACIONES", new Guid("7d7adc07-5fa4-86aa-7ea0-908d20137f92"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Goce de vacaciones", "GOCE_VACACIONES", "GOCE DE VACACIONES", new Guid("763a84ad-29ef-895e-9213-453e899397b2"), 190 },
                    { -9487L, "LACTANCIA", new Guid("77bce716-2558-202d-6400-5a0efe6dd6c3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lactancia", "LACTANCIA", "LACTANCIA", new Guid("edb9b7a3-6f19-e52e-ae7b-f5f11024caa2"), 180 },
                    { -9486L, "PRORROGA_INCAPACIDAD", new Guid("23e053e2-471e-001f-1002-bd69eb531fc3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Prórroga de incapacidad", "PRORROGA_INCAPACIDAD", "PRÓRROGA DE INCAPACIDAD", new Guid("41e50da7-1aeb-d8af-8ff1-62c61b799c77"), 170 },
                    { -9485L, "INCAPACIDAD", new Guid("d9dd1818-c674-c8c0-5a73-88c3931a2a03"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Incapacidad", "INCAPACIDAD", "INCAPACIDAD", new Guid("22826c98-048b-432e-b439-7cb1c3bb1323"), 160 }
                });

            migrationBuilder.InsertData(
                table: "incapacity_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9852L, "ANULADA", new Guid("75653f9c-2c71-6c0c-2156-07984c2ea818"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("04506868-51f4-6adc-689a-9e72f8983dfe"), 30 },
                    { -9851L, "REGISTRADA", new Guid("4e0f61b8-8f9d-5083-9061-ed6fd87d5d0a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Registrada", "REGISTRADA", "REGISTRADA", new Guid("3fbba33e-2c91-31ed-7736-727353757557"), 20 },
                    { -9850L, "EN_REVISION", new Guid("93684612-52fa-b8fe-388d-de2b43aeff1a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "En revisión", "EN_REVISION", "EN REVISIÓN", new Guid("8dd45b28-6124-7700-8459-1bc420b37327"), 10 }
                });

            migrationBuilder.InsertData(
                table: "vacation_request_status_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9858L, "DEVUELTA", new Guid("6e973d8f-9b97-98fa-94b5-5727e6df1e61"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Devuelta", "DEVUELTA", "DEVUELTA", new Guid("114fc3ac-592a-10fc-8ebd-f214b9e208a9"), 60 },
                    { -9857L, "DEVUELTA_PARCIAL", new Guid("b8141870-d5e9-45ad-59d7-8c4141226c9b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Devuelta parcial", "DEVUELTA_PARCIAL", "DEVUELTA PARCIAL", new Guid("9428530c-83c3-2a1c-2b3c-e4bfa2386573"), 50 },
                    { -9856L, "ANULADA", new Guid("f2981fe4-3d44-b0f4-f977-5d7502524978"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", "ANULADA", "ANULADA", new Guid("cebfb4b9-207f-2018-d8fd-167792103e9f"), 40 },
                    { -9855L, "RECHAZADA", new Guid("2c261db0-2c26-f3c3-122e-151846129f7e"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rechazada", "RECHAZADA", "RECHAZADA", new Guid("d817f153-e4eb-a778-a5cd-1b77884f0888"), 30 },
                    { -9854L, "APROBADA", new Guid("40a4ed9d-9b3d-74fb-e966-1fb745525e68"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aprobada", "APROBADA", "APROBADA", new Guid("cd77ee3d-2d84-940c-1033-a4919a7ef176"), 20 },
                    { -9853L, "SOLICITADA", new Guid("a32abc4e-98f0-e969-6110-1024a99a462f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Solicitada", "SOLICITADA", "SOLICITADA", new Guid("cd555398-4f2d-a13a-729d-67bc5f5e1f8f"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_incapacity_status_catalog_items__country_active_sort",
                table: "incapacity_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_incapacity_status_catalog_items__country_code",
                table: "incapacity_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_incapacity_status_catalog_items__public_id",
                table: "incapacity_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacation_request_status_catalog_items__country_active_sort",
                table: "vacation_request_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_vacation_request_status_catalog_items__country_code",
                table: "vacation_request_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_vacation_request_status_catalog_items__public_id",
                table: "vacation_request_status_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incapacity_status_catalog_items");

            migrationBuilder.DropTable(
                name: "vacation_request_status_catalog_items");

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9489L);

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9488L);

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9487L);

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9486L);

            migrationBuilder.DeleteData(
                table: "action_type_catalog_items",
                keyColumn: "id",
                keyValue: -9485L);

            migrationBuilder.DropColumn(
                name: "rest_day_of_week",
                table: "personnel_file_employment_assignments");

            migrationBuilder.DropColumn(
                name: "additional_incapacity_benefit_days_per_year",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "additional_vacation_benefit_days_default",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "allow_vacation_end_on_holiday",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "allow_vacation_start_on_holiday",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "allow_vacation_start_on_rest_day",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "annual_vacation_days_default",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "company_rest_day_of_week",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "default_use_anniversary",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "employer_covered_incapacity_days_per_year",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "incapacity_requires_document",
                table: "company_preferences");
        }
    }
}
