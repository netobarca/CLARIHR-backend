-- HU-022 - SalaryTabulator direct-cut support via Position Description salary classes

WITH salary_sources AS (
    SELECT DISTINCT
        line.tenant_id,
        btrim(line.salary_class_code) AS code,
        btrim(line.salary_class_code) AS name
    FROM salary_tabulator_lines line
    WHERE line.salary_class_code IS NOT NULL
      AND btrim(line.salary_class_code) <> ''

    UNION

    SELECT DISTINCT
        item.tenant_id,
        btrim(item.salary_class_code) AS code,
        btrim(item.salary_class_code) AS name
    FROM salary_tabulator_change_request_items item
    WHERE item.salary_class_code IS NOT NULL
      AND btrim(item.salary_class_code) <> ''

    UNION

    SELECT DISTINCT
        catalog.tenant_id,
        btrim(catalog.code) AS code,
        COALESCE(NULLIF(btrim(catalog.name), ''), btrim(catalog.code)) AS name
    FROM job_catalog_items catalog
    WHERE catalog.category = 'SalaryClass'
      AND catalog.code IS NOT NULL
      AND btrim(catalog.code) <> ''
)
INSERT INTO position_description_catalog_items (
    public_id,
    tenant_id,
    catalog_type,
    code,
    normalized_code,
    name,
    normalized_name,
    description,
    sort_order,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc
)
SELECT
    gen_random_uuid(),
    source.tenant_id,
    'SalaryClass',
    source.code,
    upper(source.code),
    source.name,
    upper(source.name),
    'Backfilled from salary tabulator and legacy job catalog salary classes.',
    0,
    true,
    gen_random_uuid(),
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM salary_sources source
WHERE NOT EXISTS (
    SELECT 1
    FROM position_description_catalog_items existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.catalog_type = 'SalaryClass'
      AND existing.normalized_code = upper(source.code)
);
