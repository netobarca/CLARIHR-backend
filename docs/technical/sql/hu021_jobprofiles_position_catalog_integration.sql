-- HU-021 - JobProfiles integration with Position Description Catalogs

ALTER TABLE job_profiles
    ADD COLUMN IF NOT EXISTS position_category_id bigint,
    ADD COLUMN IF NOT EXISTS strategic_objective_catalog_item_id bigint,
    ADD COLUMN IF NOT EXISTS assigned_work_equipment_catalog_item_id bigint,
    ADD COLUMN IF NOT EXISTS responsibility_catalog_item_id bigint;

ALTER TABLE job_profile_requirements
    ADD COLUMN IF NOT EXISTS requirement_type_catalog_item_id bigint;

ALTER TABLE job_profile_functions
    ADD COLUMN IF NOT EXISTS frequency_catalog_item_id bigint;

ALTER TABLE job_profile_working_conditions
    ADD COLUMN IF NOT EXISTS work_condition_type_catalog_item_id bigint;

CREATE INDEX IF NOT EXISTS ix_job_profiles__tenant_position_category
    ON job_profiles (tenant_id, position_category_id);

CREATE INDEX IF NOT EXISTS ix_job_profile_requirements__tenant_requirement_type
    ON job_profile_requirements (tenant_id, requirement_type_catalog_item_id);

CREATE INDEX IF NOT EXISTS ix_job_profile_functions__tenant_frequency
    ON job_profile_functions (tenant_id, frequency_catalog_item_id);

CREATE INDEX IF NOT EXISTS ix_job_profile_working_conditions__tenant_work_condition_type
    ON job_profile_working_conditions (tenant_id, work_condition_type_catalog_item_id);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_profiles__position_category') THEN
        ALTER TABLE job_profiles
            ADD CONSTRAINT fk_job_profiles__position_category
                FOREIGN KEY (position_category_id)
                REFERENCES position_categories (id)
                ON DELETE RESTRICT;
    END IF;
END$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_profiles__strategic_objective_catalog_item') THEN
        ALTER TABLE job_profiles
            ADD CONSTRAINT fk_job_profiles__strategic_objective_catalog_item
                FOREIGN KEY (strategic_objective_catalog_item_id)
                REFERENCES position_description_catalog_items (id)
                ON DELETE RESTRICT;
    END IF;
END$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_profiles__assigned_work_equipment_catalog_item') THEN
        ALTER TABLE job_profiles
            ADD CONSTRAINT fk_job_profiles__assigned_work_equipment_catalog_item
                FOREIGN KEY (assigned_work_equipment_catalog_item_id)
                REFERENCES position_description_catalog_items (id)
                ON DELETE RESTRICT;
    END IF;
END$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_profiles__responsibility_catalog_item') THEN
        ALTER TABLE job_profiles
            ADD CONSTRAINT fk_job_profiles__responsibility_catalog_item
                FOREIGN KEY (responsibility_catalog_item_id)
                REFERENCES position_description_catalog_items (id)
                ON DELETE RESTRICT;
    END IF;
END$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_profile_requirements__requirement_type_catalog_item') THEN
        ALTER TABLE job_profile_requirements
            ADD CONSTRAINT fk_job_profile_requirements__requirement_type_catalog_item
                FOREIGN KEY (requirement_type_catalog_item_id)
                REFERENCES position_description_catalog_items (id)
                ON DELETE RESTRICT;
    END IF;
END$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_profile_functions__frequency_catalog_item') THEN
        ALTER TABLE job_profile_functions
            ADD CONSTRAINT fk_job_profile_functions__frequency_catalog_item
                FOREIGN KEY (frequency_catalog_item_id)
                REFERENCES position_description_catalog_items (id)
                ON DELETE RESTRICT;
    END IF;
END$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_profile_working_conditions__work_condition_type_catalog_item') THEN
        ALTER TABLE job_profile_working_conditions
            ADD CONSTRAINT fk_job_profile_working_conditions__work_condition_type_catalog_item
                FOREIGN KEY (work_condition_type_catalog_item_id)
                REFERENCES position_description_catalog_items (id)
                ON DELETE RESTRICT;
    END IF;
END$$;

-- Backfill defaults for tenants that already have job profiles.
WITH profile_tenants AS (
    SELECT DISTINCT tenant_id
    FROM job_profiles
),
missing_org_unit_types AS (
    SELECT tenant_id
    FROM profile_tenants tenant
    WHERE NOT EXISTS (
        SELECT 1
        FROM org_unit_type_catalog_items item
        WHERE item.tenant_id = tenant.tenant_id)
)
INSERT INTO org_unit_type_catalog_items (
    public_id,
    tenant_id,
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
    'GENERAL',
    'GENERAL',
    'General',
    'GENERAL',
    'Fallback org unit type created during HU-021 migration.',
    0,
    true,
    gen_random_uuid(),
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM missing_org_unit_types source;

WITH profile_tenants AS (
    SELECT DISTINCT tenant_id
    FROM job_profiles
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
    source.catalog_type,
    source.code,
    source.normalized_code,
    source.name,
    source.normalized_name,
    source.description,
    source.sort_order,
    true,
    gen_random_uuid(),
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM (
    SELECT
        tenant.tenant_id,
        'PositionFunctionType'::varchar AS catalog_type,
        'FUNC-DEFAULT'::varchar AS code,
        'FUNC-DEFAULT'::varchar AS normalized_code,
        'Default Function Type'::varchar AS name,
        'DEFAULT FUNCTION TYPE'::varchar AS normalized_name,
        'Fallback function type created during HU-021 migration.'::varchar AS description,
        0 AS sort_order
    FROM profile_tenants tenant

    UNION ALL

    SELECT
        tenant.tenant_id,
        'PositionContractType'::varchar,
        'CONTRACT-DEFAULT'::varchar,
        'CONTRACT-DEFAULT'::varchar,
        'Default Contract Type'::varchar,
        'DEFAULT CONTRACT TYPE'::varchar,
        'Fallback contract type created during HU-021 migration.'::varchar,
        0
    FROM profile_tenants tenant

    UNION ALL

    SELECT
        tenant.tenant_id,
        'Frequency'::varchar,
        'FREQ-DEFAULT'::varchar,
        'FREQ-DEFAULT'::varchar,
        'Default Frequency'::varchar,
        'DEFAULT FREQUENCY'::varchar,
        'Fallback frequency created during HU-021 migration.'::varchar,
        0
    FROM profile_tenants tenant

    UNION ALL

    SELECT
        tenant.tenant_id,
        'WorkConditionType'::varchar,
        'WCT-DEFAULT'::varchar,
        'WCT-DEFAULT'::varchar,
        'Default Work Condition Type'::varchar,
        'DEFAULT WORK CONDITION TYPE'::varchar,
        'Fallback work condition type created during HU-021 migration.'::varchar,
        0
    FROM profile_tenants tenant
) source
WHERE NOT EXISTS (
    SELECT 1
    FROM position_description_catalog_items existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.catalog_type = source.catalog_type
      AND existing.normalized_code = source.normalized_code
);

WITH requirement_sources AS (
    SELECT DISTINCT
        item.tenant_id,
        upper(btrim(item.requirement_type)) AS normalized_requirement_type
    FROM job_profile_requirements item
    WHERE item.requirement_type IS NOT NULL
      AND btrim(item.requirement_type) <> ''
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
    'RequirementType',
    source.normalized_requirement_type,
    source.normalized_requirement_type,
    initcap(lower(source.normalized_requirement_type)),
    source.normalized_requirement_type,
    'Backfilled from existing JobProfile requirement_type values.',
    0,
    true,
    gen_random_uuid(),
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM requirement_sources source
WHERE source.normalized_requirement_type IN ('EDUCATION', 'EXPERIENCE', 'KNOWLEDGE', 'CERTIFICATION', 'OTHER')
  AND NOT EXISTS (
      SELECT 1
      FROM position_description_catalog_items existing
      WHERE existing.tenant_id = source.tenant_id
        AND existing.catalog_type = 'RequirementType'
        AND existing.normalized_code = source.normalized_requirement_type
  );

WITH profile_tenants AS (
    SELECT DISTINCT tenant_id
    FROM job_profiles
),
function_type AS (
    SELECT tenant_id, id
    FROM position_description_catalog_items
    WHERE catalog_type = 'PositionFunctionType'
      AND normalized_code = 'FUNC-DEFAULT'
),
contract_type AS (
    SELECT tenant_id, id
    FROM position_description_catalog_items
    WHERE catalog_type = 'PositionContractType'
      AND normalized_code = 'CONTRACT-DEFAULT'
),
org_unit_type AS (
    SELECT
        tenant.tenant_id,
        selected.id
    FROM profile_tenants tenant
    JOIN LATERAL (
        SELECT id
        FROM org_unit_type_catalog_items item
        WHERE item.tenant_id = tenant.tenant_id
        ORDER BY item.sort_order, item.id
        LIMIT 1
    ) AS selected ON true
)
INSERT INTO position_category_classifications (
    public_id,
    tenant_id,
    code,
    normalized_code,
    name,
    normalized_name,
    description,
    position_function_catalog_item_id,
    position_contract_catalog_item_id,
    org_unit_type_catalog_item_id,
    sort_order,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc
)
SELECT
    gen_random_uuid(),
    tenant.tenant_id,
    'CLASS-DEFAULT',
    'CLASS-DEFAULT',
    'Default Classification',
    'DEFAULT CLASSIFICATION',
    'Fallback classification created during HU-021 migration.',
    function_item.id,
    contract_item.id,
    org_unit_item.id,
    0,
    true,
    gen_random_uuid(),
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM profile_tenants tenant
JOIN function_type function_item ON function_item.tenant_id = tenant.tenant_id
JOIN contract_type contract_item ON contract_item.tenant_id = tenant.tenant_id
JOIN org_unit_type org_unit_item ON org_unit_item.tenant_id = tenant.tenant_id
WHERE NOT EXISTS (
    SELECT 1
    FROM position_category_classifications existing
    WHERE existing.tenant_id = tenant.tenant_id
      AND existing.normalized_code = 'CLASS-DEFAULT'
);

WITH profile_tenants AS (
    SELECT DISTINCT tenant_id
    FROM job_profiles
),
default_classification AS (
    SELECT tenant_id, id
    FROM position_category_classifications
    WHERE normalized_code = 'CLASS-DEFAULT'
)
INSERT INTO position_categories (
    public_id,
    tenant_id,
    code,
    normalized_code,
    name,
    normalized_name,
    description,
    position_category_classification_id,
    sort_order,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc
)
SELECT
    gen_random_uuid(),
    tenant.tenant_id,
    'CAT-DEFAULT',
    'CAT-DEFAULT',
    'Default Category',
    'DEFAULT CATEGORY',
    'Fallback category created during HU-021 migration.',
    classification.id,
    0,
    true,
    gen_random_uuid(),
    now() AT TIME ZONE 'utc',
    now() AT TIME ZONE 'utc'
FROM profile_tenants tenant
JOIN default_classification classification ON classification.tenant_id = tenant.tenant_id
WHERE NOT EXISTS (
    SELECT 1
    FROM position_categories existing
    WHERE existing.tenant_id = tenant.tenant_id
      AND existing.normalized_code = 'CAT-DEFAULT'
);

UPDATE job_profiles profile
SET position_category_id = category.id
FROM position_categories category
WHERE profile.position_category_id IS NULL
  AND category.tenant_id = profile.tenant_id
  AND category.normalized_code = 'CAT-DEFAULT';

UPDATE job_profile_requirements requirement
SET requirement_type_catalog_item_id = catalog.id
FROM position_description_catalog_items catalog
WHERE requirement.requirement_type_catalog_item_id IS NULL
  AND catalog.tenant_id = requirement.tenant_id
  AND catalog.catalog_type = 'RequirementType'
  AND catalog.normalized_code = upper(requirement.requirement_type);

UPDATE job_profile_functions function
SET frequency_catalog_item_id = catalog.id
FROM position_description_catalog_items catalog
WHERE function.frequency_catalog_item_id IS NULL
  AND catalog.tenant_id = function.tenant_id
  AND catalog.catalog_type = 'Frequency'
  AND catalog.normalized_code = 'FREQ-DEFAULT';

UPDATE job_profile_working_conditions item
SET work_condition_type_catalog_item_id = catalog.id
FROM position_description_catalog_items catalog
WHERE item.work_condition_type_catalog_item_id IS NULL
  AND catalog.tenant_id = item.tenant_id
  AND catalog.catalog_type = 'WorkConditionType'
  AND catalog.normalized_code = 'WCT-DEFAULT';
