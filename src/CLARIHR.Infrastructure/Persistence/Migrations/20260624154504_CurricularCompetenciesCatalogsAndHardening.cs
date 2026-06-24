using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CurricularCompetenciesCatalogsAndHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_personnel_file_curricular_competencies__tenant_file_requirement_type",
                table: "personnel_file_curricular_competencies");

            migrationBuilder.AddColumn<string>(
                name: "normalized_requirement_name",
                table: "personnel_file_curricular_competencies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Backfill the new normalized column for any pre-existing rows BEFORE the unique anti-duplicate index
            // is created below — otherwise multiple legacy rows sharing a (tenant, file, requirement_type_code)
            // with an empty normalized name would violate the new unique index. The application re-normalizes on
            // the next write; this matches the entity's NormalizeName (trim + upper-case).
            migrationBuilder.Sql(
                "UPDATE personnel_file_curricular_competencies " +
                "SET normalized_requirement_name = upper(trim(requirement_name)) " +
                "WHERE normalized_requirement_name = '';");

            migrationBuilder.CreateTable(
                name: "experience_metric_catalog_items",
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
                    table.PrimaryKey("pk_experience_metric_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_experience_metric_catalog_items_country_catalog_country_cat~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "experience_metric_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9123L, "HORAS", new Guid("6d13901b-b143-daae-232e-8a4ae6197b06"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Horas", "HORAS", "HORAS", new Guid("98e2db31-f38e-2fa0-d89b-566d407fde49"), 40 },
                    { -9122L, "DIAS", new Guid("12144c4f-0c9e-41a4-bf90-027507a40d41"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Días", "DIAS", "DÍAS", new Guid("8d045c1a-c024-49e4-3431-56609eb46a20"), 30 },
                    { -9121L, "MESES", new Guid("d229d765-8f1c-4f0f-2915-116272136a97"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Meses", "MESES", "MESES", new Guid("f3940667-a1b7-991b-ca7f-0d4000324746"), 20 },
                    { -9120L, "ANOS", new Guid("a0e570a2-9003-4385-a6cb-beb0cdcc019a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Años", "ANOS", "AÑOS", new Guid("5d566769-601d-e758-9286-bd6e7d5a3f98"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_curricular_competencies__tenant_file_type_name",
                table: "personnel_file_curricular_competencies",
                columns: new[] { "tenant_id", "personnel_file_id", "requirement_type_code", "normalized_requirement_name" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_personnel_file_curricular_competencies__experience_nonneg",
                table: "personnel_file_curricular_competencies",
                sql: "experience_time_value is null or experience_time_value >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_experience_metric_catalog_items__country_active_sort",
                table: "experience_metric_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_experience_metric_catalog_items__country_code",
                table: "experience_metric_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_experience_metric_catalog_items__public_id",
                table: "experience_metric_catalog_items",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experience_metric_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_personnel_file_curricular_competencies__tenant_file_type_name",
                table: "personnel_file_curricular_competencies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personnel_file_curricular_competencies__experience_nonneg",
                table: "personnel_file_curricular_competencies");

            migrationBuilder.DropColumn(
                name: "normalized_requirement_name",
                table: "personnel_file_curricular_competencies");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_curricular_competencies__tenant_file_requirement_type",
                table: "personnel_file_curricular_competencies",
                columns: new[] { "tenant_id", "personnel_file_id", "requirement_type_code" });
        }
    }
}
