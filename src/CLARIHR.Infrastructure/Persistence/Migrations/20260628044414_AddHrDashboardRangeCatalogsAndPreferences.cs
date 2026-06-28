using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHrDashboardRangeCatalogsAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "file_up_to_date_threshold_months",
                table: "company_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hr_functional_area_code",
                table: "company_preferences",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "age_range_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lower_bound_years = table.Column<int>(type: "integer", nullable: false),
                    upper_bound_years = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_age_range_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_age_range_catalog_items_country_catalog_country_catalog_ite~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seniority_range_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lower_bound_months = table.Column<int>(type: "integer", nullable: false),
                    upper_bound_months = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_seniority_range_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_seniority_range_catalog_items_country_catalog_country_catal~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "age_range_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "lower_bound_years", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order", "upper_bound_years" },
                values: new object[,]
                {
                    { -9504L, "EDAD_56_MAS", new Guid("4463c3e1-f51b-d8a4-148f-9d669ae4048b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 56, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "56 años o más", "EDAD_56_MAS", "56 AÑOS O MÁS", new Guid("26ee6e4c-d5fd-bd25-9637-2e568b7afda3"), 50, null },
                    { -9503L, "EDAD_46_55", new Guid("865e7943-3674-c76e-78ff-1c748a946ce4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 46, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "46 a 55 años", "EDAD_46_55", "46 A 55 AÑOS", new Guid("4f5544ed-7d45-5f48-c3ce-1879816211ef"), 40, 55 },
                    { -9502L, "EDAD_36_45", new Guid("6abb3389-4854-b6c1-0a80-26bdfcea6ae7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 36, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "36 a 45 años", "EDAD_36_45", "36 A 45 AÑOS", new Guid("e6a52ab7-6107-1d33-5fb7-fe311a651e0e"), 30, 45 },
                    { -9501L, "EDAD_26_35", new Guid("72f1a789-171a-3a1d-216b-302aa3a8ec91"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 26, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "26 a 35 años", "EDAD_26_35", "26 A 35 AÑOS", new Guid("cb48bf3d-ad43-7af0-dec6-e41c8b647d45"), 20, 35 },
                    { -9500L, "EDAD_18_25", new Guid("0ace8bdb-12a4-5102-4947-7ce905de6f0b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 18, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "18 a 25 años", "EDAD_18_25", "18 A 25 AÑOS", new Guid("606de5db-a7b3-717b-a24f-437f7e39a423"), 10, 25 }
                });

            migrationBuilder.InsertData(
                table: "seniority_range_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "lower_bound_months", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order", "upper_bound_months" },
                values: new object[,]
                {
                    { -9514L, "ANT_10_MAS", new Guid("e21324d0-9550-1112-4475-a2d9ad67f925"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 120, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "10 años o más", "ANT_10_MAS", "10 AÑOS O MÁS", new Guid("db3b4feb-bf15-69d9-6d0e-805b15c94be5"), 50, null },
                    { -9513L, "ANT_5_10", new Guid("0d1a2ec4-32f3-90b9-91b4-718d497c6e16"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 60, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "5 a 10 años", "ANT_5_10", "5 A 10 AÑOS", new Guid("b40b5732-aa32-0c08-7975-caf9b92dbfbc"), 40, 119 },
                    { -9512L, "ANT_3_5", new Guid("5f6c4460-b59d-4a44-4283-93b2db5487eb"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 36, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "3 a 5 años", "ANT_3_5", "3 A 5 AÑOS", new Guid("73a4b890-4dae-6387-4861-08a49f1aa127"), 30, 59 },
                    { -9511L, "ANT_1_3", new Guid("fc283eaf-9613-378b-8bec-defecdcdf1d4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 12, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "1 a 3 años", "ANT_1_3", "1 A 3 AÑOS", new Guid("62cdcf09-9f7b-3202-7ce9-234c0afb6a1c"), 20, 35 },
                    { -9510L, "ANT_0_1", new Guid("60094344-8b21-d930-5b0e-2667cc8b21ab"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Menos de 1 año", "ANT_0_1", "MENOS DE 1 AÑO", new Guid("e9359f2a-4818-9786-7e48-6320e35150c6"), 10, 11 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_age_range_catalog_items__country_active_sort",
                table: "age_range_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_age_range_catalog_items__country_code",
                table: "age_range_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_age_range_catalog_items__public_id",
                table: "age_range_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seniority_range_catalog_items__country_active_sort",
                table: "seniority_range_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_seniority_range_catalog_items__country_code",
                table: "seniority_range_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_seniority_range_catalog_items__public_id",
                table: "seniority_range_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "age_range_catalog_items");

            migrationBuilder.DropTable(
                name: "seniority_range_catalog_items");

            migrationBuilder.DropColumn(
                name: "file_up_to_date_threshold_months",
                table: "company_preferences");

            migrationBuilder.DropColumn(
                name: "hr_functional_area_code",
                table: "company_preferences");
        }
    }
}
