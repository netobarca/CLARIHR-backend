using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalJobProfileCompensationFromSalaryTabulator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_salary_scale_code",
                table: "job_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "salary_class_catalog_item_id",
                table: "job_profiles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "salary_scale_code",
                table: "job_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH ranked_compensations AS (
                    SELECT
                        compensation.job_profile_id,
                        compensation.salary_class_catalog_item_id,
                        compensation.work_schedule,
                        ROW_NUMBER() OVER (
                            PARTITION BY compensation.job_profile_id
                            ORDER BY
                                compensation.is_primary DESC,
                                compensation.modified_utc DESC NULLS LAST,
                                compensation.created_utc DESC,
                                compensation.id DESC
                        ) AS row_number
                    FROM job_profile_compensations AS compensation
                ),
                mapped_compensations AS (
                    SELECT
                        ranked.job_profile_id,
                        position_description_salary_class.id AS salary_class_catalog_item_id,
                        NULLIF(LEFT(BTRIM(ranked.work_schedule), 50), '') AS salary_scale_code
                    FROM ranked_compensations AS ranked
                    INNER JOIN job_catalog_items AS legacy_salary_class
                        ON legacy_salary_class.id = ranked.salary_class_catalog_item_id
                       AND legacy_salary_class.category = 'SalaryClass'
                    LEFT JOIN position_description_catalog_items AS position_description_salary_class
                        ON position_description_salary_class.tenant_id = legacy_salary_class.tenant_id
                       AND position_description_salary_class.catalog_type = 'SalaryClass'
                       AND position_description_salary_class.normalized_code = legacy_salary_class.normalized_code
                    WHERE ranked.row_number = 1
                )
                UPDATE job_profiles AS profile
                SET
                    salary_class_catalog_item_id = mapped.salary_class_catalog_item_id,
                    salary_scale_code = mapped.salary_scale_code,
                    normalized_salary_scale_code = UPPER(mapped.salary_scale_code)
                FROM mapped_compensations AS mapped
                WHERE profile.id = mapped.job_profile_id
                  AND mapped.salary_class_catalog_item_id IS NOT NULL
                  AND mapped.salary_scale_code IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "job_profile_compensations");

            migrationBuilder.CreateIndex(
                name: "ix_job_profiles__tenant_salary_class",
                table: "job_profiles",
                columns: new[] { "tenant_id", "salary_class_catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_job_profiles__tenant_salary_scale",
                table: "job_profiles",
                columns: new[] { "tenant_id", "normalized_salary_scale_code" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_salary_class_catalog_item_id",
                table: "job_profiles",
                column: "salary_class_catalog_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_job_profiles__salary_class_catalog_item",
                table: "job_profiles",
                column: "salary_class_catalog_item_id",
                principalTable: "position_description_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_job_profiles__salary_class_catalog_item",
                table: "job_profiles");

            migrationBuilder.DropIndex(
                name: "ix_job_profiles__tenant_salary_class",
                table: "job_profiles");

            migrationBuilder.DropIndex(
                name: "ix_job_profiles__tenant_salary_scale",
                table: "job_profiles");

            migrationBuilder.DropIndex(
                name: "IX_job_profiles_salary_class_catalog_item_id",
                table: "job_profiles");

            migrationBuilder.DropColumn(
                name: "normalized_salary_scale_code",
                table: "job_profiles");

            migrationBuilder.DropColumn(
                name: "salary_class_catalog_item_id",
                table: "job_profiles");

            migrationBuilder.DropColumn(
                name: "salary_scale_code",
                table: "job_profiles");

            migrationBuilder.CreateTable(
                name: "job_profile_compensations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    salary_class_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    max_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    min_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_class_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_schedule = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_compensations", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_compensations__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_profile_compensations__salary_class",
                        column: x => x.salary_class_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_compensations__tenant_profile_primary",
                table: "job_profile_compensations",
                columns: new[] { "tenant_id", "job_profile_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_compensations_job_profile_id",
                table: "job_profile_compensations",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_compensations_salary_class_catalog_item_id",
                table: "job_profile_compensations",
                column: "salary_class_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_compensations__public_id",
                table: "job_profile_compensations",
                column: "public_id",
                unique: true);
        }
    }
}
