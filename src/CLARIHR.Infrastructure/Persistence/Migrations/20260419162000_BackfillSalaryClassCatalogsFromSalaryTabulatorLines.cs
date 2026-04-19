using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSalaryClassCatalogsFromSalaryTabulatorLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH missing_salary_classes AS (
                    SELECT DISTINCT
                        line.tenant_id,
                        line.salary_class_code AS code,
                        line.normalized_salary_class_code AS normalized_code
                    FROM salary_tabulator_lines line
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM position_description_catalog_items item
                        WHERE item.tenant_id = line.tenant_id
                          AND item.catalog_type = 'SalaryClass'
                          AND item.normalized_code = line.normalized_salary_class_code
                    )
                )
                INSERT INTO position_description_catalog_items (
                    id,
                    catalog_type,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    description,
                    sort_order,
                    is_active,
                    concurrency_token,
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id
                )
                SELECT
                    nextval('position_description_catalog_items_id_seq'),
                    'SalaryClass',
                    code,
                    normalized_code,
                    code,
                    normalized_code,
                    'Backfilled from salary tabulator lines.',
                    row_number() OVER (PARTITION BY tenant_id ORDER BY normalized_code),
                    TRUE,
                    gen_random_uuid(),
                    gen_random_uuid(),
                    now(),
                    NULL,
                    tenant_id
                FROM missing_salary_classes;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM position_description_catalog_items item
                WHERE item.catalog_type = 'SalaryClass'
                  AND item.description = 'Backfilled from salary tabulator lines.'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM job_profiles profile
                      WHERE profile.salary_class_catalog_item_id = item.id
                  );
                """);
        }
    }
}
