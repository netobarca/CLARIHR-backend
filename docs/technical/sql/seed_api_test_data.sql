-- Development seed for API manual/integration testing.
-- This script is idempotent and tenant-scoped around 2 demo companies.
--
-- Demo tenants:
--   - aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa (Seed Acme A)
--   - bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb (Seed Acme B)
--
-- Refresh tokens (plain value -> call POST /api/auth/refresh):
--   - seed-main-refresh-token-2026
--   - seed-secondary-refresh-token-2026

BEGIN;

-- Free plan modules enabled for seeded tenants.
INSERT INTO plan_entitlements (
    plan_code,
    module_key,
    is_enabled,
    created_utc,
    modified_utc
)
VALUES
    ('FREE', 'RBAC', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'USERS', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'LOCATIONS', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'ORG_UNITS', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'JOB_PROFILES', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'POSITION_SLOTS', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'SALARY_TABULATOR', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'COST_CENTERS', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('FREE', 'LEGAL_REPRESENTATIVES', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z')
ON CONFLICT (plan_code, module_key) DO UPDATE
SET is_enabled = EXCLUDED.is_enabled,
    modified_utc = EXCLUDED.modified_utc;

-- Expand RBAC resource catalog with module keys used by current functional endpoints.
INSERT INTO rbac_resource_catalog (
    resource_key,
    normalized_resource_key,
    display_name,
    is_active,
    created_utc,
    modified_utc
)
VALUES
    ('LOCATIONS', 'LOCATIONS', 'Locations', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('ORG_UNITS', 'ORG_UNITS', 'Org Units', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('JOB_PROFILES', 'JOB_PROFILES', 'Job Profiles', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('POSITION_SLOTS', 'POSITION_SLOTS', 'Position Slots', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('SALARY_TABULATOR', 'SALARY_TABULATOR', 'Salary Tabulator', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('COST_CENTERS', 'COST_CENTERS', 'Cost Centers', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z'),
    ('LEGAL_REPRESENTATIVES', 'LEGAL_REPRESENTATIVES', 'Legal Representatives', true, '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z')
ON CONFLICT (resource_key) DO UPDATE
SET normalized_resource_key = EXCLUDED.normalized_resource_key,
    display_name = EXCLUDED.display_name,
    is_active = EXCLUDED.is_active,
    modified_utc = EXCLUDED.modified_utc;

-- Auth users (login-less flow uses refresh token exchange).
INSERT INTO auth_users (
    public_id,
    first_name,
    last_name,
    email,
    normalized_email,
    password_hash,
    auth_provider,
    provider_user_id,
    country,
    source,
    status,
    created_utc,
    modified_utc
)
VALUES
    (
        '11111111-1111-1111-1111-111111111111',
        'Seed',
        'AdminA',
        'seed.admin@clarihr.test',
        'seed.admin@clarihr.test',
        'seed-local-password-hash',
        'Local',
        NULL,
        'SV',
        'seed-api',
        'Active',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    ),
    (
        '22222222-2222-2222-2222-222222222222',
        'Seed',
        'HrA',
        'seed.hr@clarihr.test',
        'seed.hr@clarihr.test',
        'seed-local-password-hash',
        'Local',
        NULL,
        'SV',
        'seed-api',
        'Active',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    ),
    (
        '33333333-3333-3333-3333-333333333333',
        'Seed',
        'AdminB',
        'seed.audit@clarihr.test',
        'seed.audit@clarihr.test',
        'seed-local-password-hash',
        'Local',
        NULL,
        'SV',
        'seed-api',
        'Active',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    )
ON CONFLICT (normalized_email) DO UPDATE
SET public_id = EXCLUDED.public_id,
    first_name = EXCLUDED.first_name,
    last_name = EXCLUDED.last_name,
    email = EXCLUDED.email,
    password_hash = EXCLUDED.password_hash,
    auth_provider = EXCLUDED.auth_provider,
    provider_user_id = EXCLUDED.provider_user_id,
    country = EXCLUDED.country,
    source = EXCLUDED.source,
    status = EXCLUDED.status,
    modified_utc = EXCLUDED.modified_utc;

INSERT INTO companies (
    public_id,
    name,
    slug,
    status,
    created_by_user_public_id,
    created_utc,
    modified_utc
)
VALUES
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Seed Acme A',
        'seed-acme-a',
        'Active',
        '11111111-1111-1111-1111-111111111111',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    ),
    (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'Seed Acme B',
        'seed-acme-b',
        'Active',
        '33333333-3333-3333-3333-333333333333',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    )
ON CONFLICT (slug) DO UPDATE
SET public_id = EXCLUDED.public_id,
    name = EXCLUDED.name,
    status = EXCLUDED.status,
    created_by_user_public_id = EXCLUDED.created_by_user_public_id,
    modified_utc = EXCLUDED.modified_utc;

INSERT INTO company_subscriptions (
    company_id,
    plan_code,
    status,
    start_date_utc,
    end_date_utc,
    created_utc,
    modified_utc
)
SELECT
    company.id,
    'FREE',
    'Active',
    '2026-03-01T00:00:00Z',
    NULL,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM companies company
WHERE company.public_id IN (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
)
AND NOT EXISTS (
    SELECT 1
    FROM company_subscriptions existing
    WHERE existing.company_id = company.id
      AND existing.status = 'Active'
);

-- Tenant roles.
INSERT INTO iam_roles (
    public_id,
    tenant_id,
    name,
    normalized_name,
    description,
    is_system_role,
    created_utc,
    modified_utc
)
VALUES
    (
        'aaaaaaaa-0000-0000-0000-000000000001',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Seed Company Admin',
        'SEED COMPANY ADMIN',
        'Seed full-access company role.',
        true,
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    ),
    (
        'aaaaaaaa-0000-0000-0000-000000000002',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Seed HR Analyst',
        'SEED HR ANALYST',
        'Seed read-focused role.',
        false,
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    ),
    (
        'bbbbbbbb-0000-0000-0000-000000000001',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'Seed Company Admin',
        'SEED COMPANY ADMIN',
        'Seed full-access company role.',
        true,
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    )
ON CONFLICT (tenant_id, normalized_name) DO UPDATE
SET public_id = EXCLUDED.public_id,
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    is_system_role = EXCLUDED.is_system_role,
    modified_utc = EXCLUDED.modified_utc;

-- Tenant permissions used by current module authorization services.
WITH permission_source AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'iam.administration.manage', 'Manage IAM', 'Full IAM administration.', 'IAM', 'Administration', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'RBAC.USERS.MANAGE', 'Manage Users', 'Company users administration.', 'RBAC', 'Users', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'RBAC.ROLES.MANAGE', 'Manage Roles', 'Roles administration.', 'RBAC', 'Roles', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'RBAC.PERMISSIONS.MANAGE', 'Manage Permissions', 'Permissions administration.', 'RBAC', 'Permissions', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'Locations.Read', 'Read Locations', 'Read location hierarchy.', 'LOCATIONS', 'Locations', 'Read'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'Locations.Admin', 'Admin Locations', 'Manage location hierarchy.', 'LOCATIONS', 'Locations', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'OrgUnits.Read', 'Read Org Units', 'Read org units.', 'ORG_UNITS', 'OrgUnits', 'Read'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'OrgUnits.Admin', 'Admin Org Units', 'Manage org units.', 'ORG_UNITS', 'OrgUnits', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JobProfiles.Read', 'Read Job Profiles', 'Read job profiles.', 'JOB_PROFILES', 'JobProfiles', 'Read'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JobProfiles.Admin', 'Admin Job Profiles', 'Manage job profiles.', 'JOB_PROFILES', 'JobProfiles', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JobCatalogs.Admin', 'Admin Job Catalogs', 'Manage job catalogs.', 'JOB_PROFILES', 'JobCatalogs', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'PositionSlots.Read', 'Read Position Slots', 'Read position slots.', 'POSITION_SLOTS', 'PositionSlots', 'Read'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'PositionSlots.Admin', 'Admin Position Slots', 'Manage position slots.', 'POSITION_SLOTS', 'PositionSlots', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'SalaryTabulator.Read', 'Read Salary Tabulator', 'Read salary tabulator lines and requests.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Read'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'SalaryTabulator.Request', 'Request Salary Changes', 'Submit salary tabulator requests.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Request'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'SalaryTabulator.Approve', 'Approve Salary Changes', 'Approve/reject salary tabulator requests.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Approve'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'SalaryTabulator.Admin', 'Admin Salary Tabulator', 'Full salary tabulator administration.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'CostCenters.Read', 'Read Cost Centers', 'Read cost centers.', 'COST_CENTERS', 'CostCenters', 'Read'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'CostCenters.Admin', 'Admin Cost Centers', 'Manage cost centers.', 'COST_CENTERS', 'CostCenters', 'Manage'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'LegalRepresentatives.Read', 'Read Legal Representatives', 'Read legal representatives.', 'LEGAL_REPRESENTATIVES', 'LegalRepresentatives', 'Read'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'LegalRepresentatives.Admin', 'Admin Legal Representatives', 'Manage legal representatives.', 'LEGAL_REPRESENTATIVES', 'LegalRepresentatives', 'Manage'),

        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'iam.administration.manage', 'Manage IAM', 'Full IAM administration.', 'IAM', 'Administration', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'RBAC.USERS.MANAGE', 'Manage Users', 'Company users administration.', 'RBAC', 'Users', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'RBAC.ROLES.MANAGE', 'Manage Roles', 'Roles administration.', 'RBAC', 'Roles', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'RBAC.PERMISSIONS.MANAGE', 'Manage Permissions', 'Permissions administration.', 'RBAC', 'Permissions', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'Locations.Read', 'Read Locations', 'Read location hierarchy.', 'LOCATIONS', 'Locations', 'Read'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'Locations.Admin', 'Admin Locations', 'Manage location hierarchy.', 'LOCATIONS', 'Locations', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'OrgUnits.Read', 'Read Org Units', 'Read org units.', 'ORG_UNITS', 'OrgUnits', 'Read'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'OrgUnits.Admin', 'Admin Org Units', 'Manage org units.', 'ORG_UNITS', 'OrgUnits', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'JobProfiles.Read', 'Read Job Profiles', 'Read job profiles.', 'JOB_PROFILES', 'JobProfiles', 'Read'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'JobProfiles.Admin', 'Admin Job Profiles', 'Manage job profiles.', 'JOB_PROFILES', 'JobProfiles', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'JobCatalogs.Admin', 'Admin Job Catalogs', 'Manage job catalogs.', 'JOB_PROFILES', 'JobCatalogs', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'PositionSlots.Read', 'Read Position Slots', 'Read position slots.', 'POSITION_SLOTS', 'PositionSlots', 'Read'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'PositionSlots.Admin', 'Admin Position Slots', 'Manage position slots.', 'POSITION_SLOTS', 'PositionSlots', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'SalaryTabulator.Read', 'Read Salary Tabulator', 'Read salary tabulator lines and requests.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Read'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'SalaryTabulator.Request', 'Request Salary Changes', 'Submit salary tabulator requests.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Request'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'SalaryTabulator.Approve', 'Approve Salary Changes', 'Approve/reject salary tabulator requests.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Approve'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'SalaryTabulator.Admin', 'Admin Salary Tabulator', 'Full salary tabulator administration.', 'SALARY_TABULATOR', 'SalaryTabulator', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'CostCenters.Read', 'Read Cost Centers', 'Read cost centers.', 'COST_CENTERS', 'CostCenters', 'Read'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'CostCenters.Admin', 'Admin Cost Centers', 'Manage cost centers.', 'COST_CENTERS', 'CostCenters', 'Manage'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'LegalRepresentatives.Read', 'Read Legal Representatives', 'Read legal representatives.', 'LEGAL_REPRESENTATIVES', 'LegalRepresentatives', 'Read'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'LegalRepresentatives.Admin', 'Admin Legal Representatives', 'Manage legal representatives.', 'LEGAL_REPRESENTATIVES', 'LegalRepresentatives', 'Manage')
    ) AS source(tenant_id, code, name, description, module_name, screen_name, action_name)
)
INSERT INTO iam_permissions (
    public_id,
    tenant_id,
    code,
    normalized_code,
    name,
    description,
    module,
    normalized_module,
    screen,
    normalized_screen,
    kind,
    action,
    normalized_action,
    field_name,
    normalized_field_name,
    field_access,
    created_utc,
    modified_utc
)
SELECT
    (
        SUBSTRING(MD5(source.tenant_id::text || ':' || UPPER(TRIM(source.code)) || ':public'), 1, 8) || '-' ||
        SUBSTRING(MD5(source.tenant_id::text || ':' || UPPER(TRIM(source.code)) || ':public'), 9, 4) || '-' ||
        SUBSTRING(MD5(source.tenant_id::text || ':' || UPPER(TRIM(source.code)) || ':public'), 13, 4) || '-' ||
        SUBSTRING(MD5(source.tenant_id::text || ':' || UPPER(TRIM(source.code)) || ':public'), 17, 4) || '-' ||
        SUBSTRING(MD5(source.tenant_id::text || ':' || UPPER(TRIM(source.code)) || ':public'), 21, 12)
    )::uuid,
    source.tenant_id,
    source.code,
    UPPER(TRIM(source.code)),
    source.name,
    source.description,
    source.module_name,
    UPPER(TRIM(source.module_name)),
    source.screen_name,
    UPPER(TRIM(source.screen_name)),
    'ScreenAction',
    source.action_name,
    UPPER(TRIM(source.action_name)),
    NULL,
    NULL,
    NULL,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM permission_source source
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET code = EXCLUDED.code,
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    module = EXCLUDED.module,
    normalized_module = EXCLUDED.normalized_module,
    screen = EXCLUDED.screen,
    normalized_screen = EXCLUDED.normalized_screen,
    kind = EXCLUDED.kind,
    action = EXCLUDED.action,
    normalized_action = EXCLUDED.normalized_action,
    modified_utc = EXCLUDED.modified_utc;

-- Company admin role gets all seeded tenant permissions.
INSERT INTO iam_role_permission_assignments (
    tenant_id,
    role_id,
    permission_id,
    created_utc,
    modified_utc
)
SELECT
    role.tenant_id,
    role.id,
    permission.id,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM iam_roles role
JOIN iam_permissions permission
  ON permission.tenant_id = role.tenant_id
WHERE role.normalized_name = 'SEED COMPANY ADMIN'
ON CONFLICT (tenant_id, role_id, permission_id) DO NOTHING;

-- Read-focused role for tenant A.
WITH analyst_permission_codes AS (
    SELECT *
    FROM (VALUES
        ('LOCATIONS.READ'),
        ('ORGUNITS.READ'),
        ('JOBPROFILES.READ'),
        ('POSITIONSLOTS.READ'),
        ('SALARYTABULATOR.READ'),
        ('COSTCENTERS.READ'),
        ('LEGALREPRESENTATIVES.READ')
    ) AS source(normalized_code_compact)
)
INSERT INTO iam_role_permission_assignments (
    tenant_id,
    role_id,
    permission_id,
    created_utc,
    modified_utc
)
SELECT
    role.tenant_id,
    role.id,
    permission.id,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM iam_roles role
JOIN iam_permissions permission
  ON permission.tenant_id = role.tenant_id
JOIN analyst_permission_codes catalog
  ON REPLACE(permission.normalized_code, '.', '') = REPLACE(catalog.normalized_code_compact, '.', '')
WHERE role.tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
  AND role.normalized_name = 'SEED HR ANALYST'
ON CONFLICT (tenant_id, role_id, permission_id) DO NOTHING;

-- IAM users linked to auth users.
INSERT INTO iam_users (
    public_id,
    tenant_id,
    first_name,
    last_name,
    email,
    normalized_email,
    is_active,
    created_utc,
    modified_utc
)
VALUES
    (
        '11111111-1111-1111-1111-111111111111',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Seed',
        'AdminA',
        'seed.admin@clarihr.test',
        'seed.admin@clarihr.test',
        true,
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    ),
    (
        '22222222-2222-2222-2222-222222222222',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Seed',
        'HrA',
        'seed.hr@clarihr.test',
        'seed.hr@clarihr.test',
        true,
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    ),
    (
        '33333333-3333-3333-3333-333333333333',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'Seed',
        'AdminB',
        'seed.audit@clarihr.test',
        'seed.audit@clarihr.test',
        true,
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z'
    )
ON CONFLICT (tenant_id, normalized_email) DO UPDATE
SET public_id = EXCLUDED.public_id,
    first_name = EXCLUDED.first_name,
    last_name = EXCLUDED.last_name,
    email = EXCLUDED.email,
    is_active = EXCLUDED.is_active,
    modified_utc = EXCLUDED.modified_utc;

INSERT INTO iam_user_role_assignments (
    tenant_id,
    user_id,
    role_id,
    created_utc,
    modified_utc
)
SELECT
    iam_user.tenant_id,
    iam_user.id,
    role.id,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM iam_users iam_user
JOIN iam_roles role
  ON role.tenant_id = iam_user.tenant_id
WHERE (iam_user.tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
       AND iam_user.normalized_email = 'seed.admin@clarihr.test'
       AND role.normalized_name = 'SEED COMPANY ADMIN')
   OR (iam_user.tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
       AND iam_user.normalized_email = 'seed.hr@clarihr.test'
       AND role.normalized_name = 'SEED HR ANALYST')
   OR (iam_user.tenant_id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
       AND iam_user.normalized_email = 'seed.audit@clarihr.test'
       AND role.normalized_name = 'SEED COMPANY ADMIN')
ON CONFLICT (tenant_id, user_id, role_id) DO NOTHING;

-- Company membership (tenant context + role + status).
INSERT INTO user_companies (
    user_id,
    company_id,
    role_id,
    is_primary,
    status,
    created_utc,
    modified_utc
)
SELECT
    auth_user.id,
    company.id,
    role.id,
    true,
    'Active',
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM auth_users auth_user
JOIN companies company
  ON (
        auth_user.public_id IN ('11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222')
    AND company.public_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
  )
  OR (
        auth_user.public_id = '33333333-3333-3333-3333-333333333333'
    AND company.public_id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
  )
JOIN iam_roles role
  ON role.tenant_id = company.public_id
 AND (
        (auth_user.public_id = '11111111-1111-1111-1111-111111111111' AND role.normalized_name = 'SEED COMPANY ADMIN')
     OR (auth_user.public_id = '22222222-2222-2222-2222-222222222222' AND role.normalized_name = 'SEED HR ANALYST')
     OR (auth_user.public_id = '33333333-3333-3333-3333-333333333333' AND role.normalized_name = 'SEED COMPANY ADMIN')
 )
ON CONFLICT (user_id, company_id) DO UPDATE
SET role_id = EXCLUDED.role_id,
    is_primary = EXCLUDED.is_primary,
    status = EXCLUDED.status,
    modified_utc = EXCLUDED.modified_utc;

-- Refresh tokens for auth bootstrap.
INSERT INTO auth_refresh_tokens (
    family_id,
    user_id,
    token_hash,
    expires_utc,
    revoked_utc,
    replaced_by_token_hash,
    revocation_reason,
    created_utc,
    modified_utc
)
SELECT
    source.family_id,
    auth_user.id,
    source.token_hash,
    '2099-12-31T23:59:59Z',
    NULL,
    NULL,
    NULL,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM (
    VALUES
        ('11111111-1111-1111-1111-111111111111'::uuid, 'aaaaaaaa-ffff-ffff-ffff-fffffffffff1'::uuid, '02E4D768EE1398A515D832B8921C0FEB0C833763C13B41D525EAA2BF9D44FF8B'),
        ('33333333-3333-3333-3333-333333333333'::uuid, 'bbbbbbbb-ffff-ffff-ffff-fffffffffff1'::uuid, '583964B3523D9474D97F36E064FAD763A00C3638E7E21705B1BF6651A091A37B')
) AS source(user_public_id, family_id, token_hash)
JOIN auth_users auth_user
  ON auth_user.public_id = source.user_public_id
ON CONFLICT (token_hash) DO UPDATE
SET user_id = EXCLUDED.user_id,
    family_id = EXCLUDED.family_id,
    expires_utc = EXCLUDED.expires_utc,
    revoked_utc = NULL,
    replaced_by_token_hash = NULL,
    revocation_reason = NULL,
    modified_utc = EXCLUDED.modified_utc;

-- Invitation token sample for company user workflows.
INSERT INTO company_invitation_tokens (
    user_id,
    company_id,
    token_hash,
    expiration_utc,
    is_used,
    revoked_utc,
    created_utc,
    modified_utc
)
SELECT
    auth_user.id,
    company.id,
    '6A79624E2C37734742C1556A80EA77D9AE2103FFEE5A92AC7AF85403834C1899',
    '2099-12-31T23:59:59Z',
    false,
    NULL,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM auth_users auth_user
JOIN companies company
  ON company.public_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
WHERE auth_user.public_id = '22222222-2222-2222-2222-222222222222'
ON CONFLICT (token_hash) DO UPDATE
SET expiration_utc = EXCLUDED.expiration_utc,
    is_used = EXCLUDED.is_used,
    revoked_utc = EXCLUDED.revoked_utc,
    modified_utc = EXCLUDED.modified_utc;

-- Locations seed.
INSERT INTO location_hierarchy_configs (
    public_id,
    is_multi_level,
    default_group_code,
    default_group_name,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    (
        'aaaaaaaa-1000-0000-0000-000000000001',
        false,
        'GENERAL',
        'General',
        'aaaaaaaa-1000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'bbbbbbbb-1000-0000-0000-000000000001',
        false,
        'GENERAL',
        'General',
        'bbbbbbbb-1000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    )
ON CONFLICT (tenant_id) DO UPDATE
SET public_id = EXCLUDED.public_id,
    is_multi_level = EXCLUDED.is_multi_level,
    default_group_code = EXCLUDED.default_group_code,
    default_group_name = EXCLUDED.default_group_name,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

INSERT INTO location_levels (
    public_id,
    level_order,
    display_name,
    is_active,
    is_required,
    allows_work_centers,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    (
        'aaaaaaaa-1000-0000-0000-000000000010',
        1,
        'General',
        true,
        true,
        true,
        'aaaaaaaa-1000-0000-0000-000000000110',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'bbbbbbbb-1000-0000-0000-000000000010',
        1,
        'General',
        true,
        true,
        true,
        'bbbbbbbb-1000-0000-0000-000000000110',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    )
ON CONFLICT (tenant_id, level_order) DO UPDATE
SET public_id = EXCLUDED.public_id,
    display_name = EXCLUDED.display_name,
    is_active = EXCLUDED.is_active,
    is_required = EXCLUDED.is_required,
    allows_work_centers = EXCLUDED.allows_work_centers,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

INSERT INTO location_groups (
    public_id,
    level_order,
    code,
    normalized_code,
    name,
    normalized_name,
    parent_id,
    description,
    is_active,
    is_default,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    (
        'aaaaaaaa-1000-0000-0000-000000000020',
        1,
        'GENERAL',
        'GENERAL',
        'General',
        'GENERAL',
        NULL,
        'Default location group.',
        true,
        true,
        'aaaaaaaa-1000-0000-0000-000000000120',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'aaaaaaaa-1000-0000-0000-000000000021',
        1,
        'HQ',
        'HQ',
        'Headquarters',
        'HEADQUARTERS',
        NULL,
        'Main office group.',
        true,
        false,
        'aaaaaaaa-1000-0000-0000-000000000121',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'aaaaaaaa-1000-0000-0000-000000000022',
        1,
        'PLANT',
        'PLANT',
        'Production Plant',
        'PRODUCTION PLANT',
        NULL,
        'Plant group.',
        true,
        false,
        'aaaaaaaa-1000-0000-0000-000000000122',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'bbbbbbbb-1000-0000-0000-000000000020',
        1,
        'GENERAL',
        'GENERAL',
        'General',
        'GENERAL',
        NULL,
        'Default location group.',
        true,
        true,
        'bbbbbbbb-1000-0000-0000-000000000120',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    ),
    (
        'bbbbbbbb-1000-0000-0000-000000000021',
        1,
        'HQ',
        'HQ',
        'Headquarters',
        'HEADQUARTERS',
        NULL,
        'Main office group.',
        true,
        false,
        'bbbbbbbb-1000-0000-0000-000000000121',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    )
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    level_order = EXCLUDED.level_order,
    name = EXCLUDED.name,
    normalized_name = EXCLUDED.normalized_name,
    parent_id = EXCLUDED.parent_id,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active,
    is_default = EXCLUDED.is_default,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

INSERT INTO work_center_types (
    public_id,
    code,
    normalized_code,
    name,
    normalized_name,
    requires_address,
    requires_geo,
    allows_biometric,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    (
        'aaaaaaaa-1000-0000-0000-000000000030',
        'OFFICE',
        'OFFICE',
        'Office',
        'OFFICE',
        true,
        false,
        true,
        true,
        'aaaaaaaa-1000-0000-0000-000000000130',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'aaaaaaaa-1000-0000-0000-000000000031',
        'PLANT',
        'PLANT',
        'Plant',
        'PLANT',
        true,
        true,
        true,
        true,
        'aaaaaaaa-1000-0000-0000-000000000131',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'bbbbbbbb-1000-0000-0000-000000000030',
        'OFFICE',
        'OFFICE',
        'Office',
        'OFFICE',
        true,
        false,
        true,
        true,
        'bbbbbbbb-1000-0000-0000-000000000130',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    )
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    name = EXCLUDED.name,
    normalized_name = EXCLUDED.normalized_name,
    requires_address = EXCLUDED.requires_address,
    requires_geo = EXCLUDED.requires_geo,
    allows_biometric = EXCLUDED.allows_biometric,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

WITH work_center_source AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-1000-0000-0000-000000000040'::uuid, 'HQ-CAMPUS', 'HQ-CAMPUS', 'Headquarters Campus', 'HEADQUARTERS CAMPUS', 'OFFICE', 'HQ', 'Avenida Central 100, San Salvador', 13.692940::numeric, -89.218191::numeric, '+50322001111', 'hq@seed-acme-a.test', 'Main headquarters', 'aaaaaaaa-1000-0000-0000-000000000140'::uuid),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-1000-0000-0000-000000000041'::uuid, 'PLANT-SITE', 'PLANT-SITE', 'Plant Site', 'PLANT SITE', 'PLANT', 'PLANT', 'KM 30 Carretera Litoral', 13.512340::numeric, -88.971230::numeric, '+50322002222', 'plant@seed-acme-a.test', 'Production plant', 'aaaaaaaa-1000-0000-0000-000000000141'::uuid),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'bbbbbbbb-1000-0000-0000-000000000040'::uuid, 'HQ-CAMPUS', 'HQ-CAMPUS', 'Headquarters Campus', 'HEADQUARTERS CAMPUS', 'OFFICE', 'HQ', 'Boulevard Los Heroes 55, San Salvador', 13.705678::numeric, -89.210111::numeric, '+50322003333', 'hq@seed-acme-b.test', 'Main headquarters', 'bbbbbbbb-1000-0000-0000-000000000140'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        code,
        normalized_code,
        name,
        normalized_name,
        type_code,
        group_code,
        address,
        geo_lat,
        geo_long,
        phone,
        email,
        notes,
        concurrency_token
    )
)
INSERT INTO work_centers (
    public_id,
    code,
    normalized_code,
    name,
    normalized_name,
    work_center_type_id,
    location_group_id,
    address,
    geo_lat,
    geo_long,
    phone,
    email,
    notes,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.code,
    source.normalized_code,
    source.name,
    source.normalized_name,
    type_row.id,
    group_row.id,
    source.address,
    source.geo_lat,
    source.geo_long,
    source.phone,
    source.email,
    source.notes,
    true,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM work_center_source source
JOIN work_center_types type_row
  ON type_row.tenant_id = source.tenant_id
 AND type_row.normalized_code = source.type_code
JOIN location_groups group_row
  ON group_row.tenant_id = source.tenant_id
 AND group_row.normalized_code = source.group_code
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    name = EXCLUDED.name,
    normalized_name = EXCLUDED.normalized_name,
    work_center_type_id = EXCLUDED.work_center_type_id,
    location_group_id = EXCLUDED.location_group_id,
    address = EXCLUDED.address,
    geo_lat = EXCLUDED.geo_lat,
    geo_long = EXCLUDED.geo_long,
    phone = EXCLUDED.phone,
    email = EXCLUDED.email,
    notes = EXCLUDED.notes,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Cost centers.
INSERT INTO cost_centers (
    public_id,
    code,
    normalized_code,
    name,
    normalized_name,
    type,
    payroll_expense_account_code,
    employer_contribution_account_code,
    provision_account_code,
    description,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    (
        'aaaaaaaa-2000-0000-0000-000000000001',
        'CC-HR-001',
        'CC-HR-001',
        'Human Resources',
        'HUMAN RESOURCES',
        'Mixed',
        '5100-HR',
        '5200-HR',
        '5300-HR',
        'Human resources cost center.',
        true,
        'aaaaaaaa-2000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'aaaaaaaa-2000-0000-0000-000000000002',
        'CC-FIN-001',
        'CC-FIN-001',
        'Finance',
        'FINANCE',
        'SalaryExpense',
        '5100-FIN',
        '5200-FIN',
        '5300-FIN',
        'Finance cost center.',
        true,
        'aaaaaaaa-2000-0000-0000-000000000102',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'bbbbbbbb-2000-0000-0000-000000000001',
        'CC-AUD-001',
        'CC-AUD-001',
        'Audit',
        'AUDIT',
        'Mixed',
        '5100-AUD',
        '5200-AUD',
        '5300-AUD',
        'Audit cost center.',
        true,
        'bbbbbbbb-2000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    )
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    name = EXCLUDED.name,
    normalized_name = EXCLUDED.normalized_name,
    type = EXCLUDED.type,
    payroll_expense_account_code = EXCLUDED.payroll_expense_account_code,
    employer_contribution_account_code = EXCLUDED.employer_contribution_account_code,
    provision_account_code = EXCLUDED.provision_account_code,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Org units (roots first, then child nodes).
WITH root_source AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-3000-0000-0000-000000000001'::uuid, 'OU-DIR', 'OU-DIR', 'Direccion General', 'DIRECCION GENERAL', 'Direccion', 1::integer, 'Executive management', 'CC-FIN-001', '11111111-1111-1111-1111-111111111111'::uuid, 'aaaaaaaa-3000-0000-0000-000000000101'::uuid),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'bbbbbbbb-3000-0000-0000-000000000001'::uuid, 'OU-DIR', 'OU-DIR', 'Direccion General', 'DIRECCION GENERAL', 'Direccion', 1::integer, 'Executive management', 'CC-AUD-001', '33333333-3333-3333-3333-333333333333'::uuid, 'bbbbbbbb-3000-0000-0000-000000000101'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        code,
        normalized_code,
        name,
        normalized_name,
        unit_type,
        sort_order,
        description,
        cost_center_code,
        manager_employee_id,
        concurrency_token
    )
)
INSERT INTO org_units (
    public_id,
    code,
    normalized_code,
    name,
    normalized_name,
    unit_type,
    parent_id,
    sort_order,
    description,
    cost_center_code,
    manager_employee_id,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.code,
    source.normalized_code,
    source.name,
    source.normalized_name,
    source.unit_type,
    NULL,
    source.sort_order,
    source.description,
    source.cost_center_code,
    source.manager_employee_id,
    true,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM root_source source
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    name = EXCLUDED.name,
    normalized_name = EXCLUDED.normalized_name,
    unit_type = EXCLUDED.unit_type,
    parent_id = EXCLUDED.parent_id,
    sort_order = EXCLUDED.sort_order,
    description = EXCLUDED.description,
    cost_center_code = EXCLUDED.cost_center_code,
    manager_employee_id = EXCLUDED.manager_employee_id,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

WITH child_source AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-3000-0000-0000-000000000002'::uuid, 'OU-HR', 'OU-HR', 'Human Resources', 'HUMAN RESOURCES', 'Gerencia', 'OU-DIR', 1::integer, 'HR management', 'CC-HR-001', '11111111-1111-1111-1111-111111111111'::uuid, 'aaaaaaaa-3000-0000-0000-000000000102'::uuid),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-3000-0000-0000-000000000003'::uuid, 'OU-FIN', 'OU-FIN', 'Finance', 'FINANCE', 'Gerencia', 'OU-DIR', 2::integer, 'Finance management', 'CC-FIN-001', '11111111-1111-1111-1111-111111111111'::uuid, 'aaaaaaaa-3000-0000-0000-000000000103'::uuid),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-3000-0000-0000-000000000004'::uuid, 'OU-HR-OPS', 'OU-HR-OPS', 'HR Operations', 'HR OPERATIONS', 'Departamento', 'OU-HR', 1::integer, 'HR operations team', 'CC-HR-001', '22222222-2222-2222-2222-222222222222'::uuid, 'aaaaaaaa-3000-0000-0000-000000000104'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        code,
        normalized_code,
        name,
        normalized_name,
        unit_type,
        parent_code,
        sort_order,
        description,
        cost_center_code,
        manager_employee_id,
        concurrency_token
    )
)
INSERT INTO org_units (
    public_id,
    code,
    normalized_code,
    name,
    normalized_name,
    unit_type,
    parent_id,
    sort_order,
    description,
    cost_center_code,
    manager_employee_id,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.code,
    source.normalized_code,
    source.name,
    source.normalized_name,
    source.unit_type,
    parent_unit.id,
    source.sort_order,
    source.description,
    source.cost_center_code,
    source.manager_employee_id,
    true,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM child_source source
JOIN org_units parent_unit
  ON parent_unit.tenant_id = source.tenant_id
 AND parent_unit.normalized_code = source.parent_code
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    name = EXCLUDED.name,
    normalized_name = EXCLUDED.normalized_name,
    unit_type = EXCLUDED.unit_type,
    parent_id = EXCLUDED.parent_id,
    sort_order = EXCLUDED.sort_order,
    description = EXCLUDED.description,
    cost_center_code = EXCLUDED.cost_center_code,
    manager_employee_id = EXCLUDED.manager_employee_id,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Job catalogs.
INSERT INTO job_catalog_items (
    public_id,
    category,
    code,
    normalized_code,
    name,
    normalized_name,
    is_system,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    ('aaaaaaaa-4000-0000-0000-000000000001', 'EducationLevel', 'EDU-BS', 'EDU-BS', 'Bachelor Degree', 'BACHELOR DEGREE', false, true, 'aaaaaaaa-4000-0000-0000-000000000101', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('aaaaaaaa-4000-0000-0000-000000000002', 'KnowledgeArea', 'KNOW-HR', 'KNOW-HR', 'Human Resources', 'HUMAN RESOURCES', false, true, 'aaaaaaaa-4000-0000-0000-000000000102', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('aaaaaaaa-4000-0000-0000-000000000003', 'Competency', 'COMP-LEAD', 'COMP-LEAD', 'Leadership', 'LEADERSHIP', false, true, 'aaaaaaaa-4000-0000-0000-000000000103', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('aaaaaaaa-4000-0000-0000-000000000004', 'Training', 'TRN-LAB', 'TRN-LAB', 'Labor Law', 'LABOR LAW', false, true, 'aaaaaaaa-4000-0000-0000-000000000104', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('aaaaaaaa-4000-0000-0000-000000000005', 'SalaryClass', 'SAL-A1', 'SAL-A1', 'Administrative A1', 'ADMINISTRATIVE A1', false, true, 'aaaaaaaa-4000-0000-0000-000000000105', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('aaaaaaaa-4000-0000-0000-000000000006', 'BenefitType', 'BEN-HEALTH', 'BEN-HEALTH', 'Health Insurance', 'HEALTH INSURANCE', false, true, 'aaaaaaaa-4000-0000-0000-000000000106', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('aaaaaaaa-4000-0000-0000-000000000007', 'WorkingCondition', 'WC-HYBRID', 'WC-HYBRID', 'Hybrid Work', 'HYBRID WORK', false, true, 'aaaaaaaa-4000-0000-0000-000000000107', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('aaaaaaaa-4000-0000-0000-000000000008', 'RelationType', 'REL-INTERNAL', 'REL-INTERNAL', 'Internal Areas', 'INTERNAL AREAS', false, true, 'aaaaaaaa-4000-0000-0000-000000000108', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    ('bbbbbbbb-4000-0000-0000-000000000001', 'SalaryClass', 'SAL-B1', 'SAL-B1', 'Audit B1', 'AUDIT B1', false, true, 'bbbbbbbb-4000-0000-0000-000000000101', '2026-03-01T00:00:00Z', '2026-03-01T00:00:00Z', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb')
ON CONFLICT (tenant_id, category, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    code = EXCLUDED.code,
    name = EXCLUDED.name,
    normalized_name = EXCLUDED.normalized_name,
    is_system = EXCLUDED.is_system,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Job profiles (no report-to dependencies first).
WITH profile_source AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-5000-0000-0000-000000000001'::uuid, 'JP-HR-MANAGER', 'JP-HR-MANAGER', 'HR Manager', 'HR MANAGER', 'Lead HR area and policy execution.', 'OU-HR', NULL::text, 'Approved decisions within HR scope.', 'HRIS, ATS, Policy Repository', 'Manage HR operations and compliance.', 'Medical and life insurance.', 'Hybrid schedule.', 'Market benchmark 2026.', 'Valuated as strategic role.', 'Published', 2::integer, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, true, 'aaaaaaaa-5000-0000-0000-000000000101'::uuid),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-5000-0000-0000-000000000002'::uuid, 'JP-FIN-MANAGER', 'JP-FIN-MANAGER', 'Finance Manager', 'FINANCE MANAGER', 'Lead finance planning and control.', 'OU-FIN', NULL::text, 'Approve and control budget execution.', 'ERP and accounting tools.', 'Manage accounting and financial reporting.', 'Performance bonus.', 'Office schedule.', 'Market benchmark 2026.', 'Valuated as core financial role.', 'Published', 2::integer, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, true, 'aaaaaaaa-5000-0000-0000-000000000102'::uuid),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'bbbbbbbb-5000-0000-0000-000000000001'::uuid, 'JP-AUDITOR', 'JP-AUDITOR', 'Auditor', 'AUDITOR', 'Run internal audits and controls.', 'OU-DIR', NULL::text, 'Control and report audit findings.', 'Audit software.', 'Execute internal audit plan.', 'Transport allowance.', 'Office schedule.', 'Market benchmark 2026.', 'Valuated as control role.', 'Published', 1::integer, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, true, 'bbbbbbbb-5000-0000-0000-000000000101'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        code,
        normalized_code,
        title,
        normalized_title,
        objective,
        org_unit_code,
        reports_to_code,
        decision_scope,
        assigned_resources,
        responsibilities,
        benefits_summary,
        working_condition_summary,
        market_salary_reference,
        valuation_notes,
        status,
        version,
        effective_from_utc,
        effective_to_utc,
        is_active,
        concurrency_token
    )
)
INSERT INTO job_profiles (
    public_id,
    code,
    normalized_code,
    title,
    normalized_title,
    objective,
    org_unit_id,
    reports_to_job_profile_id,
    decision_scope,
    assigned_resources,
    responsibilities,
    benefits_summary,
    working_condition_summary,
    market_salary_reference,
    valuation_notes,
    status,
    version,
    effective_from_utc,
    effective_to_utc,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.code,
    source.normalized_code,
    source.title,
    source.normalized_title,
    source.objective,
    org_unit.id,
    NULL,
    source.decision_scope,
    source.assigned_resources,
    source.responsibilities,
    source.benefits_summary,
    source.working_condition_summary,
    source.market_salary_reference,
    source.valuation_notes,
    source.status,
    source.version,
    source.effective_from_utc,
    source.effective_to_utc,
    source.is_active,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM profile_source source
JOIN org_units org_unit
  ON org_unit.tenant_id = source.tenant_id
 AND org_unit.normalized_code = source.org_unit_code
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    title = EXCLUDED.title,
    normalized_title = EXCLUDED.normalized_title,
    objective = EXCLUDED.objective,
    org_unit_id = EXCLUDED.org_unit_id,
    reports_to_job_profile_id = EXCLUDED.reports_to_job_profile_id,
    decision_scope = EXCLUDED.decision_scope,
    assigned_resources = EXCLUDED.assigned_resources,
    responsibilities = EXCLUDED.responsibilities,
    benefits_summary = EXCLUDED.benefits_summary,
    working_condition_summary = EXCLUDED.working_condition_summary,
    market_salary_reference = EXCLUDED.market_salary_reference,
    valuation_notes = EXCLUDED.valuation_notes,
    status = EXCLUDED.status,
    version = EXCLUDED.version,
    effective_from_utc = EXCLUDED.effective_from_utc,
    effective_to_utc = EXCLUDED.effective_to_utc,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Job profiles that reference another profile.
WITH profile_source AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-5000-0000-0000-000000000003'::uuid, 'JP-HR-ANALYST', 'JP-HR-ANALYST', 'HR Analyst', 'HR ANALYST', 'Support HR processes and analytics.', 'OU-HR-OPS', 'JP-HR-MANAGER', 'Execute approved HR tasks.', 'HRIS and spreadsheet models.', 'Recruitment, onboarding and KPIs.', 'Health insurance.', 'Hybrid schedule.', 'Market benchmark 2026.', 'Valuated as tactical role.', 'Draft', 1::integer, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, true, 'aaaaaaaa-5000-0000-0000-000000000103'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        code,
        normalized_code,
        title,
        normalized_title,
        objective,
        org_unit_code,
        reports_to_code,
        decision_scope,
        assigned_resources,
        responsibilities,
        benefits_summary,
        working_condition_summary,
        market_salary_reference,
        valuation_notes,
        status,
        version,
        effective_from_utc,
        effective_to_utc,
        is_active,
        concurrency_token
    )
)
INSERT INTO job_profiles (
    public_id,
    code,
    normalized_code,
    title,
    normalized_title,
    objective,
    org_unit_id,
    reports_to_job_profile_id,
    decision_scope,
    assigned_resources,
    responsibilities,
    benefits_summary,
    working_condition_summary,
    market_salary_reference,
    valuation_notes,
    status,
    version,
    effective_from_utc,
    effective_to_utc,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.code,
    source.normalized_code,
    source.title,
    source.normalized_title,
    source.objective,
    org_unit.id,
    reports_to.id,
    source.decision_scope,
    source.assigned_resources,
    source.responsibilities,
    source.benefits_summary,
    source.working_condition_summary,
    source.market_salary_reference,
    source.valuation_notes,
    source.status,
    source.version,
    source.effective_from_utc,
    source.effective_to_utc,
    source.is_active,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM profile_source source
JOIN org_units org_unit
  ON org_unit.tenant_id = source.tenant_id
 AND org_unit.normalized_code = source.org_unit_code
JOIN job_profiles reports_to
  ON reports_to.tenant_id = source.tenant_id
 AND reports_to.normalized_code = source.reports_to_code
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    title = EXCLUDED.title,
    normalized_title = EXCLUDED.normalized_title,
    objective = EXCLUDED.objective,
    org_unit_id = EXCLUDED.org_unit_id,
    reports_to_job_profile_id = EXCLUDED.reports_to_job_profile_id,
    decision_scope = EXCLUDED.decision_scope,
    assigned_resources = EXCLUDED.assigned_resources,
    responsibilities = EXCLUDED.responsibilities,
    benefits_summary = EXCLUDED.benefits_summary,
    working_condition_summary = EXCLUDED.working_condition_summary,
    market_salary_reference = EXCLUDED.market_salary_reference,
    valuation_notes = EXCLUDED.valuation_notes,
    status = EXCLUDED.status,
    version = EXCLUDED.version,
    effective_from_utc = EXCLUDED.effective_from_utc,
    effective_to_utc = EXCLUDED.effective_to_utc,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Job profile requirements.
INSERT INTO job_profile_requirements (
    job_profile_id,
    requirement_type,
    catalog_item_id,
    description,
    sort_order,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    source.requirement_type,
    catalog.id,
    source.description,
    source.sort_order,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'Education', 'EDU-BS', 'Bachelor degree in HR or related field.', 1),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'Knowledge', 'KNOW-HR', 'Strong HR legal and policy knowledge.', 2)
) AS source(tenant_id, profile_code, requirement_type, catalog_code, description, sort_order)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
JOIN job_catalog_items catalog
  ON catalog.tenant_id = source.tenant_id
 AND catalog.normalized_code = source.catalog_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_requirements existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.requirement_type = source.requirement_type
      AND existing.sort_order = source.sort_order
);

-- Job profile functions.
INSERT INTO job_profile_functions (
    job_profile_id,
    function_type,
    description,
    sort_order,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    source.function_type,
    source.description,
    source.sort_order,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'General', 'Lead the HR annual roadmap execution.', 1),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'Specific', 'Approve headcount and onboarding plans.', 2),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-ANALYST', 'Specific', 'Maintain HR KPIs and reports.', 1)
) AS source(tenant_id, profile_code, function_type, description, sort_order)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_functions existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.function_type = source.function_type
      AND existing.sort_order = source.sort_order
);

-- Job profile relations.
INSERT INTO job_profile_relations (
    job_profile_id,
    relation_type,
    catalog_item_id,
    counterpart,
    notes,
    sort_order,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    source.relation_type,
    catalog.id,
    source.counterpart,
    source.notes,
    source.sort_order,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'Internal', 'REL-INTERNAL', 'Finance and Operations', 'Coordinate payroll and onboarding.', 1)
) AS source(tenant_id, profile_code, relation_type, catalog_code, counterpart, notes, sort_order)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
LEFT JOIN job_catalog_items catalog
  ON catalog.tenant_id = source.tenant_id
 AND catalog.normalized_code = source.catalog_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_relations existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.relation_type = source.relation_type
      AND existing.sort_order = source.sort_order
);

-- Job profile competencies.
INSERT INTO job_profile_competencies (
    job_profile_id,
    catalog_item_id,
    name,
    expected_level,
    notes,
    sort_order,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    catalog.id,
    source.name,
    source.expected_level,
    source.notes,
    source.sort_order,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'COMP-LEAD', 'Leadership', 'Advanced', 'Leads strategic initiatives.', 1)
) AS source(tenant_id, profile_code, catalog_code, name, expected_level, notes, sort_order)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
LEFT JOIN job_catalog_items catalog
  ON catalog.tenant_id = source.tenant_id
 AND catalog.normalized_code = source.catalog_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_competencies existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.sort_order = source.sort_order
);

-- Job profile trainings.
INSERT INTO job_profile_trainings (
    job_profile_id,
    catalog_item_id,
    name,
    notes,
    sort_order,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    catalog.id,
    source.name,
    source.notes,
    source.sort_order,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'TRN-LAB', 'Labor Law Update', 'Annual labor law refresher.', 1)
) AS source(tenant_id, profile_code, catalog_code, name, notes, sort_order)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
LEFT JOIN job_catalog_items catalog
  ON catalog.tenant_id = source.tenant_id
 AND catalog.normalized_code = source.catalog_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_trainings existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.sort_order = source.sort_order
);

-- Job profile compensation.
INSERT INTO job_profile_compensations (
    job_profile_id,
    salary_class_catalog_item_id,
    salary_class_name,
    min_salary,
    max_salary,
    currency_code,
    work_schedule,
    is_primary,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    salary_class.id,
    salary_class.name,
    source.min_salary,
    source.max_salary,
    source.currency_code,
    source.work_schedule,
    true,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'SAL-A1', 1200.00::numeric, 1800.00::numeric, 'USD', 'Mon-Fri 8:00-17:00')
) AS source(tenant_id, profile_code, salary_class_code, min_salary, max_salary, currency_code, work_schedule)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
LEFT JOIN job_catalog_items salary_class
  ON salary_class.tenant_id = source.tenant_id
 AND salary_class.normalized_code = source.salary_class_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_compensations existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.is_primary = true
);

-- Job profile benefits.
INSERT INTO job_profile_benefits (
    job_profile_id,
    catalog_item_id,
    name,
    notes,
    sort_order,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    catalog.id,
    source.name,
    source.notes,
    source.sort_order,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'BEN-HEALTH', 'Health Insurance', 'Private health policy.', 1)
) AS source(tenant_id, profile_code, catalog_code, name, notes, sort_order)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
LEFT JOIN job_catalog_items catalog
  ON catalog.tenant_id = source.tenant_id
 AND catalog.normalized_code = source.catalog_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_benefits existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.sort_order = source.sort_order
);

-- Job profile working conditions.
INSERT INTO job_profile_working_conditions (
    job_profile_id,
    catalog_item_id,
    name,
    notes,
    sort_order,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    profile.id,
    catalog.id,
    source.name,
    source.notes,
    source.sort_order,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'JP-HR-MANAGER', 'WC-HYBRID', 'Hybrid Work', '3 days office / 2 remote.', 1)
) AS source(tenant_id, profile_code, catalog_code, name, notes, sort_order)
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.profile_code
LEFT JOIN job_catalog_items catalog
  ON catalog.tenant_id = source.tenant_id
 AND catalog.normalized_code = source.catalog_code
WHERE NOT EXISTS (
    SELECT 1
    FROM job_profile_working_conditions existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.job_profile_id = profile.id
      AND existing.sort_order = source.sort_order
);

-- Job profile dependencies.
INSERT INTO job_profile_dependent_positions (
    job_profile_id,
    dependent_job_profile_id,
    quantity,
    notes,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    manager_profile.id,
    analyst_profile.id,
    2,
    'Manager supervises 2 analyst positions.',
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    manager_profile.tenant_id
FROM job_profiles manager_profile
JOIN job_profiles analyst_profile
  ON analyst_profile.tenant_id = manager_profile.tenant_id
 AND analyst_profile.normalized_code = 'JP-HR-ANALYST'
WHERE manager_profile.tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
  AND manager_profile.normalized_code = 'JP-HR-MANAGER'
  AND NOT EXISTS (
      SELECT 1
      FROM job_profile_dependent_positions existing
      WHERE existing.tenant_id = manager_profile.tenant_id
        AND existing.job_profile_id = manager_profile.id
        AND existing.dependent_job_profile_id = analyst_profile.id
  );

-- Position slots.
WITH root_slots AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-6000-0000-0000-000000000001'::uuid, 'PS-HR-MGR', 'PS-HR-MGR', 'HR Manager Slot', 'JP-HR-MANAGER', 'OU-HR', 'HQ-CAMPUS', 'CC-HR-001', NULL::text, NULL::text, 'Occupied', 1::integer, 1::integer, false, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, 'Seeded manager slot.', true, 'aaaaaaaa-6000-0000-0000-000000000101'::uuid),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-6000-0000-0000-000000000003'::uuid, 'PS-FIN-MGR', 'PS-FIN-MGR', 'Finance Manager Slot', 'JP-FIN-MANAGER', 'OU-FIN', 'HQ-CAMPUS', 'CC-FIN-001', NULL::text, NULL::text, 'Occupied', 1::integer, 1::integer, false, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, 'Seeded finance manager slot.', true, 'aaaaaaaa-6000-0000-0000-000000000103'::uuid),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'bbbbbbbb-6000-0000-0000-000000000001'::uuid, 'PS-AUD-LEAD', 'PS-AUD-LEAD', 'Audit Lead Slot', 'JP-AUDITOR', 'OU-DIR', 'HQ-CAMPUS', 'CC-AUD-001', NULL::text, NULL::text, 'Occupied', 1::integer, 1::integer, false, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, 'Seeded audit lead slot.', true, 'bbbbbbbb-6000-0000-0000-000000000101'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        code,
        normalized_code,
        title,
        job_profile_code,
        org_unit_code,
        work_center_code,
        cost_center_code,
        direct_dependency_code,
        functional_dependency_code,
        status,
        max_employees,
        occupied_employees,
        is_fixed_term,
        effective_from_utc,
        effective_to_utc,
        notes,
        is_active,
        concurrency_token
    )
)
INSERT INTO position_slots (
    public_id,
    code,
    normalized_code,
    title,
    job_profile_id,
    org_unit_id,
    work_center_id,
    cost_center_code,
    direct_dependency_position_slot_id,
    functional_dependency_position_slot_id,
    status,
    max_employees,
    occupied_employees,
    is_fixed_term,
    effective_from_utc,
    effective_to_utc,
    notes,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.code,
    source.normalized_code,
    source.title,
    profile.id,
    org_unit.id,
    work_center.id,
    source.cost_center_code,
    NULL,
    NULL,
    source.status,
    source.max_employees,
    source.occupied_employees,
    source.is_fixed_term,
    source.effective_from_utc,
    source.effective_to_utc,
    source.notes,
    source.is_active,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM root_slots source
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.job_profile_code
JOIN org_units org_unit
  ON org_unit.tenant_id = source.tenant_id
 AND org_unit.normalized_code = source.org_unit_code
LEFT JOIN work_centers work_center
  ON work_center.tenant_id = source.tenant_id
 AND work_center.normalized_code = source.work_center_code
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    title = EXCLUDED.title,
    job_profile_id = EXCLUDED.job_profile_id,
    org_unit_id = EXCLUDED.org_unit_id,
    work_center_id = EXCLUDED.work_center_id,
    cost_center_code = EXCLUDED.cost_center_code,
    direct_dependency_position_slot_id = EXCLUDED.direct_dependency_position_slot_id,
    functional_dependency_position_slot_id = EXCLUDED.functional_dependency_position_slot_id,
    status = EXCLUDED.status,
    max_employees = EXCLUDED.max_employees,
    occupied_employees = EXCLUDED.occupied_employees,
    is_fixed_term = EXCLUDED.is_fixed_term,
    effective_from_utc = EXCLUDED.effective_from_utc,
    effective_to_utc = EXCLUDED.effective_to_utc,
    notes = EXCLUDED.notes,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

WITH dependent_slots AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-6000-0000-0000-000000000002'::uuid, 'PS-HR-ANL', 'PS-HR-ANL', 'HR Analyst Slot', 'JP-HR-ANALYST', 'OU-HR-OPS', 'HQ-CAMPUS', 'CC-HR-001', 'PS-HR-MGR', NULL::text, 'Vacant', 2::integer, 0::integer, false, '2026-01-01T00:00:00Z'::timestamptz, NULL::timestamptz, 'Seeded analyst slot.', true, 'aaaaaaaa-6000-0000-0000-000000000102'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        code,
        normalized_code,
        title,
        job_profile_code,
        org_unit_code,
        work_center_code,
        cost_center_code,
        direct_dependency_code,
        functional_dependency_code,
        status,
        max_employees,
        occupied_employees,
        is_fixed_term,
        effective_from_utc,
        effective_to_utc,
        notes,
        is_active,
        concurrency_token
    )
)
INSERT INTO position_slots (
    public_id,
    code,
    normalized_code,
    title,
    job_profile_id,
    org_unit_id,
    work_center_id,
    cost_center_code,
    direct_dependency_position_slot_id,
    functional_dependency_position_slot_id,
    status,
    max_employees,
    occupied_employees,
    is_fixed_term,
    effective_from_utc,
    effective_to_utc,
    notes,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.code,
    source.normalized_code,
    source.title,
    profile.id,
    org_unit.id,
    work_center.id,
    source.cost_center_code,
    direct_slot.id,
    NULL,
    source.status,
    source.max_employees,
    source.occupied_employees,
    source.is_fixed_term,
    source.effective_from_utc,
    source.effective_to_utc,
    source.notes,
    source.is_active,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM dependent_slots source
JOIN job_profiles profile
  ON profile.tenant_id = source.tenant_id
 AND profile.normalized_code = source.job_profile_code
JOIN org_units org_unit
  ON org_unit.tenant_id = source.tenant_id
 AND org_unit.normalized_code = source.org_unit_code
LEFT JOIN work_centers work_center
  ON work_center.tenant_id = source.tenant_id
 AND work_center.normalized_code = source.work_center_code
LEFT JOIN position_slots direct_slot
  ON direct_slot.tenant_id = source.tenant_id
 AND direct_slot.normalized_code = source.direct_dependency_code
ON CONFLICT (tenant_id, normalized_code) DO UPDATE
SET public_id = EXCLUDED.public_id,
    title = EXCLUDED.title,
    job_profile_id = EXCLUDED.job_profile_id,
    org_unit_id = EXCLUDED.org_unit_id,
    work_center_id = EXCLUDED.work_center_id,
    cost_center_code = EXCLUDED.cost_center_code,
    direct_dependency_position_slot_id = EXCLUDED.direct_dependency_position_slot_id,
    functional_dependency_position_slot_id = EXCLUDED.functional_dependency_position_slot_id,
    status = EXCLUDED.status,
    max_employees = EXCLUDED.max_employees,
    occupied_employees = EXCLUDED.occupied_employees,
    is_fixed_term = EXCLUDED.is_fixed_term,
    effective_from_utc = EXCLUDED.effective_from_utc,
    effective_to_utc = EXCLUDED.effective_to_utc,
    notes = EXCLUDED.notes,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Salary tabulator lines.
INSERT INTO salary_tabulator_lines (
    public_id,
    salary_class_code,
    normalized_salary_class_code,
    salary_scale_code,
    normalized_salary_scale_code,
    currency_code,
    base_amount,
    min_amount,
    max_amount,
    effective_from_utc,
    effective_to_utc,
    is_active,
    version,
    notes,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    (
        'aaaaaaaa-7000-0000-0000-000000000001',
        'SAL-A1',
        'SAL-A1',
        'SCALE-01',
        'SCALE-01',
        'USD',
        1250.00,
        1100.00,
        1500.00,
        '2026-01-01T00:00:00Z',
        NULL,
        true,
        1,
        'Initial salary line.',
        'aaaaaaaa-7000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'aaaaaaaa-7000-0000-0000-000000000002',
        'SAL-A1',
        'SAL-A1',
        'SCALE-02',
        'SCALE-02',
        'USD',
        1500.00,
        1300.00,
        1800.00,
        '2026-01-01T00:00:00Z',
        NULL,
        true,
        1,
        'Second salary line.',
        'aaaaaaaa-7000-0000-0000-000000000102',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'bbbbbbbb-7000-0000-0000-000000000001',
        'SAL-B1',
        'SAL-B1',
        'SCALE-01',
        'SCALE-01',
        'USD',
        1400.00,
        1200.00,
        1700.00,
        '2026-01-01T00:00:00Z',
        NULL,
        true,
        1,
        'Initial salary line.',
        'bbbbbbbb-7000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    )
ON CONFLICT (tenant_id, normalized_salary_class_code, normalized_salary_scale_code, effective_from_utc) DO UPDATE
SET public_id = EXCLUDED.public_id,
    salary_class_code = EXCLUDED.salary_class_code,
    salary_scale_code = EXCLUDED.salary_scale_code,
    currency_code = EXCLUDED.currency_code,
    base_amount = EXCLUDED.base_amount,
    min_amount = EXCLUDED.min_amount,
    max_amount = EXCLUDED.max_amount,
    effective_to_utc = EXCLUDED.effective_to_utc,
    is_active = EXCLUDED.is_active,
    version = EXCLUDED.version,
    notes = EXCLUDED.notes,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Salary tabulator change requests.
WITH request_source AS (
    SELECT *
    FROM (VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-7100-0000-0000-000000000001'::uuid, 'ST-REQ-0001', 'Annual salary review cycle', 'Submitted', '2026-07-01T00:00:00Z'::timestamptz, '11111111-1111-1111-1111-111111111111'::uuid, '2026-03-02T10:00:00Z'::timestamptz, NULL::uuid, NULL::timestamptz, NULL::text, 'aaaaaaaa-7100-0000-0000-000000000101'::uuid),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'aaaaaaaa-7100-0000-0000-000000000002'::uuid, 'ST-REQ-0002', 'Adjustment approved by management', 'Approved', '2026-08-01T00:00:00Z'::timestamptz, '22222222-2222-2222-2222-222222222222'::uuid, '2026-03-03T08:00:00Z'::timestamptz, '11111111-1111-1111-1111-111111111111'::uuid, '2026-03-03T12:00:00Z'::timestamptz, 'Approved after budget validation.', 'aaaaaaaa-7100-0000-0000-000000000102'::uuid),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'bbbbbbbb-7100-0000-0000-000000000001'::uuid, 'ST-REQ-0001', 'Audit compensation update', 'Draft', '2026-09-01T00:00:00Z'::timestamptz, '33333333-3333-3333-3333-333333333333'::uuid, NULL::timestamptz, NULL::uuid, NULL::timestamptz, NULL::text, 'bbbbbbbb-7100-0000-0000-000000000101'::uuid)
    ) AS source(
        tenant_id,
        public_id,
        request_number,
        reason,
        status,
        effective_from_utc,
        requested_by_user_id,
        submitted_at_utc,
        decided_by_user_id,
        decided_at_utc,
        decision_comment,
        concurrency_token
    )
)
INSERT INTO salary_tabulator_change_requests (
    public_id,
    request_number,
    reason,
    status,
    effective_from_utc,
    requested_by_user_id,
    submitted_at_utc,
    decided_by_user_id,
    decided_at_utc,
    decision_comment,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    source.public_id,
    source.request_number,
    source.reason,
    source.status,
    source.effective_from_utc,
    source.requested_by_user_id,
    source.submitted_at_utc,
    source.decided_by_user_id,
    source.decided_at_utc,
    source.decision_comment,
    source.concurrency_token,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM request_source source
ON CONFLICT (tenant_id, request_number) DO UPDATE
SET public_id = EXCLUDED.public_id,
    reason = EXCLUDED.reason,
    status = EXCLUDED.status,
    effective_from_utc = EXCLUDED.effective_from_utc,
    requested_by_user_id = EXCLUDED.requested_by_user_id,
    submitted_at_utc = EXCLUDED.submitted_at_utc,
    decided_by_user_id = EXCLUDED.decided_by_user_id,
    decided_at_utc = EXCLUDED.decided_at_utc,
    decision_comment = EXCLUDED.decision_comment,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Salary tabulator request items.
INSERT INTO salary_tabulator_change_request_items (
    salary_tabulator_change_request_id,
    salary_class_code,
    normalized_salary_class_code,
    salary_scale_code,
    normalized_salary_scale_code,
    currency_code,
    change_type,
    current_base_amount,
    proposed_base_amount,
    current_min_amount,
    proposed_min_amount,
    current_max_amount,
    proposed_max_amount,
    notes,
    created_utc,
    modified_utc,
    tenant_id
)
SELECT
    request.id,
    source.salary_class_code,
    source.normalized_salary_class_code,
    source.salary_scale_code,
    source.normalized_salary_scale_code,
    source.currency_code,
    source.change_type,
    source.current_base_amount,
    source.proposed_base_amount,
    source.current_min_amount,
    source.proposed_min_amount,
    source.current_max_amount,
    source.proposed_max_amount,
    source.notes,
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    source.tenant_id
FROM (
    VALUES
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'ST-REQ-0001', 'SAL-A1', 'SAL-A1', 'SCALE-01', 'SCALE-01', 'USD', 'Update', 1250.00::numeric, 1300.00::numeric, 1100.00::numeric, 1150.00::numeric, 1500.00::numeric, 1550.00::numeric, 'Pending committee approval'),
        ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'ST-REQ-0002', 'SAL-A1', 'SAL-A1', 'SCALE-03', 'SCALE-03', 'USD', 'Create', NULL::numeric, 1850.00::numeric, NULL::numeric, 1600.00::numeric, NULL::numeric, 2100.00::numeric, 'Approved new scale creation'),
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'ST-REQ-0001', 'SAL-B1', 'SAL-B1', 'SCALE-02', 'SCALE-02', 'USD', 'Create', NULL::numeric, 1650.00::numeric, NULL::numeric, 1450.00::numeric, NULL::numeric, 1900.00::numeric, 'Draft request item')
) AS source(
    tenant_id,
    request_number,
    salary_class_code,
    normalized_salary_class_code,
    salary_scale_code,
    normalized_salary_scale_code,
    currency_code,
    change_type,
    current_base_amount,
    proposed_base_amount,
    current_min_amount,
    proposed_min_amount,
    current_max_amount,
    proposed_max_amount,
    notes
)
JOIN salary_tabulator_change_requests request
  ON request.tenant_id = source.tenant_id
 AND request.request_number = source.request_number
WHERE NOT EXISTS (
    SELECT 1
    FROM salary_tabulator_change_request_items existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.salary_tabulator_change_request_id = request.id
      AND existing.normalized_salary_class_code = source.normalized_salary_class_code
      AND existing.normalized_salary_scale_code = source.normalized_salary_scale_code
      AND existing.change_type = source.change_type
);

-- Legal representatives: every company keeps at least 1 active representative.
INSERT INTO legal_representatives (
    public_id,
    first_name,
    last_name,
    full_name,
    normalized_full_name,
    document_type,
    document_number,
    normalized_document_number,
    position_title,
    representation_type,
    authority_description,
    appointment_instrument,
    appointment_date_utc,
    effective_from_utc,
    effective_to_utc,
    email,
    phone,
    is_primary,
    is_active,
    concurrency_token,
    created_utc,
    modified_utc,
    tenant_id
)
VALUES
    (
        'aaaaaaaa-8000-0000-0000-000000000001',
        'Ana',
        'Mendoza',
        'Ana Mendoza',
        'ANA MENDOZA',
        'TaxId',
        '0614-290190-102-3',
        '0614-290190-102-3',
        'Representante Legal',
        'PrimaryLegalRepresentative',
        'General legal representation.',
        'Board appointment act.',
        '2025-12-20T00:00:00Z',
        '2026-01-01T00:00:00Z',
        NULL,
        'ana.mendoza@seed-acme-a.test',
        '+50370000001',
        true,
        true,
        'aaaaaaaa-8000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'aaaaaaaa-8000-0000-0000-000000000002',
        'Carlos',
        'Lopez',
        'Carlos Lopez',
        'CARLOS LOPEZ',
        'Passport',
        'P-ACMEA-9901',
        'P-ACMEA-9901',
        'Representante Suplente',
        'AlternateLegalRepresentative',
        'Acts as alternate legal representative.',
        'Alternate appointment letter.',
        '2026-01-10T00:00:00Z',
        '2026-01-10T00:00:00Z',
        NULL,
        'carlos.lopez@seed-acme-a.test',
        '+50370000002',
        false,
        true,
        'aaaaaaaa-8000-0000-0000-000000000102',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ),
    (
        'bbbbbbbb-8000-0000-0000-000000000001',
        'Rosa',
        'Perez',
        'Rosa Perez',
        'ROSA PEREZ',
        'TaxId',
        '0625-120585-101-9',
        '0625-120585-101-9',
        'Representante Legal',
        'PrimaryLegalRepresentative',
        'General legal representation.',
        'Board appointment act.',
        '2025-12-20T00:00:00Z',
        '2026-01-01T00:00:00Z',
        NULL,
        'rosa.perez@seed-acme-b.test',
        '+50370000003',
        true,
        true,
        'bbbbbbbb-8000-0000-0000-000000000101',
        '2026-03-01T00:00:00Z',
        '2026-03-01T00:00:00Z',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    )
ON CONFLICT (tenant_id, document_type, normalized_document_number) DO UPDATE
SET public_id = EXCLUDED.public_id,
    first_name = EXCLUDED.first_name,
    last_name = EXCLUDED.last_name,
    full_name = EXCLUDED.full_name,
    normalized_full_name = EXCLUDED.normalized_full_name,
    position_title = EXCLUDED.position_title,
    representation_type = EXCLUDED.representation_type,
    authority_description = EXCLUDED.authority_description,
    appointment_instrument = EXCLUDED.appointment_instrument,
    appointment_date_utc = EXCLUDED.appointment_date_utc,
    effective_from_utc = EXCLUDED.effective_from_utc,
    effective_to_utc = EXCLUDED.effective_to_utc,
    email = EXCLUDED.email,
    phone = EXCLUDED.phone,
    is_primary = EXCLUDED.is_primary,
    is_active = EXCLUDED.is_active,
    concurrency_token = EXCLUDED.concurrency_token,
    modified_utc = EXCLUDED.modified_utc;

-- Field permission sample for RBAC users screen.
INSERT INTO role_field_permissions (
    tenant_id,
    role_id,
    field_key,
    normalized_field_key,
    is_visible,
    is_editable,
    is_required,
    is_masked,
    updated_by_user_id,
    updated_at_utc,
    created_utc,
    modified_utc
)
SELECT
    role.tenant_id,
    role.id,
    'RBAC_USERS.EMAIL',
    'RBAC_USERS.EMAIL',
    false,
    false,
    false,
    false,
    '11111111-1111-1111-1111-111111111111',
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z',
    '2026-03-01T00:00:00Z'
FROM iam_roles role
WHERE role.tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
  AND role.normalized_name = 'SEED HR ANALYST'
ON CONFLICT (tenant_id, role_id, normalized_field_key) DO UPDATE
SET is_visible = EXCLUDED.is_visible,
    is_editable = EXCLUDED.is_editable,
    is_required = EXCLUDED.is_required,
    is_masked = EXCLUDED.is_masked,
    updated_by_user_id = EXCLUDED.updated_by_user_id,
    updated_at_utc = EXCLUDED.updated_at_utc,
    modified_utc = EXCLUDED.modified_utc;

INSERT INTO field_permission_audit_logs (
    tenant_id,
    role_public_id,
    field_key,
    normalized_field_key,
    changed_by_user_id,
    before_json,
    after_json,
    changed_at_utc,
    created_utc,
    modified_utc
)
SELECT
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'aaaaaaaa-0000-0000-0000-000000000002',
    'RBAC_USERS.EMAIL',
    'RBAC_USERS.EMAIL',
    '11111111-1111-1111-1111-111111111111',
    '{"isVisible":true,"isEditable":true}'::jsonb,
    '{"isVisible":false,"isEditable":false}'::jsonb,
    '2026-03-02T15:00:00Z',
    '2026-03-02T15:00:00Z',
    '2026-03-02T15:00:00Z'
WHERE NOT EXISTS (
    SELECT 1
    FROM field_permission_audit_logs existing
    WHERE existing.tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
      AND existing.role_public_id = 'aaaaaaaa-0000-0000-0000-000000000002'
      AND existing.normalized_field_key = 'RBAC_USERS.EMAIL'
      AND existing.changed_at_utc = '2026-03-02T15:00:00Z'
);

INSERT INTO rbac_permission_audit_logs (
    tenant_id,
    role_public_id,
    resource_key,
    normalized_resource_key,
    changed_by_user_id,
    change_type,
    before_json,
    after_json,
    changed_at_utc,
    created_utc,
    modified_utc
)
SELECT
    source.tenant_id,
    source.role_public_id,
    source.resource_key,
    source.normalized_resource_key,
    source.changed_by_user_id,
    source.change_type,
    source.before_json::jsonb,
    source.after_json::jsonb,
    source.changed_at_utc,
    source.created_utc,
    source.modified_utc
FROM (
    VALUES
        (
            'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid,
            'aaaaaaaa-0000-0000-0000-000000000001'::uuid,
            'ORG_UNITS',
            'ORG_UNITS',
            '11111111-1111-1111-1111-111111111111'::uuid,
            'Upsert',
            '{"hasAccess":true,"canRead":false,"canCreate":false,"canUpdate":false,"canDelete":false}',
            '{"hasAccess":true,"canRead":true,"canCreate":true,"canUpdate":true,"canDelete":false}',
            '2026-03-02T11:00:00Z'::timestamptz,
            '2026-03-02T11:00:00Z'::timestamptz,
            '2026-03-02T11:00:00Z'::timestamptz
        ),
        (
            'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid,
            'bbbbbbbb-0000-0000-0000-000000000001'::uuid,
            'LEGAL_REPRESENTATIVES',
            'LEGAL_REPRESENTATIVES',
            '33333333-3333-3333-3333-333333333333'::uuid,
            'Upsert',
            '{"hasAccess":true,"canRead":false,"canCreate":false,"canUpdate":false,"canDelete":false}',
            '{"hasAccess":true,"canRead":true,"canCreate":false,"canUpdate":true,"canDelete":false}',
            '2026-03-02T11:30:00Z'::timestamptz,
            '2026-03-02T11:30:00Z'::timestamptz,
            '2026-03-02T11:30:00Z'::timestamptz
        )
) AS source(
    tenant_id,
    role_public_id,
    resource_key,
    normalized_resource_key,
    changed_by_user_id,
    change_type,
    before_json,
    after_json,
    changed_at_utc,
    created_utc,
    modified_utc
)
WHERE NOT EXISTS (
    SELECT 1
    FROM rbac_permission_audit_logs existing
    WHERE existing.tenant_id = source.tenant_id
      AND existing.role_public_id = source.role_public_id
      AND existing.normalized_resource_key = source.normalized_resource_key
      AND existing.change_type = source.change_type
      AND existing.changed_at_utc = source.changed_at_utc
);

-- Audit logs sample.
INSERT INTO audit_logs (
    public_id,
    tenant_id,
    actor_user_id,
    actor_email,
    event_type,
    entity_type,
    entity_id,
    entity_key,
    action,
    summary,
    before_json,
    after_json,
    diff_json,
    ip_address,
    user_agent,
    created_utc,
    modified_utc
)
VALUES
    (
        'aaaaaaaa-9000-0000-0000-000000000001',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        '11111111-1111-1111-1111-111111111111',
        'seed.admin@clarihr.test',
        'LEGAL_REPRESENTATIVE_UPDATED',
        'LegalRepresentative',
        'aaaaaaaa-8000-0000-0000-000000000002',
        'P-ACMEA-9901',
        'Update',
        'Updated alternate legal representative contact details.',
        '{"phone":"+50370000000"}',
        '{"phone":"+50370000002"}',
        '{"phone":{"before":"+50370000000","after":"+50370000002"}}',
        '127.0.0.1',
        'seed-script',
        '2026-03-03T10:00:00Z',
        '2026-03-03T10:00:00Z'
    ),
    (
        'bbbbbbbb-9000-0000-0000-000000000001',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        '33333333-3333-3333-3333-333333333333',
        'seed.audit@clarihr.test',
        'REPORT_EXPORTED',
        'Report',
        NULL,
        'POSITION_SLOTS',
        'Export',
        'Exported position slots report in CSV.',
        NULL,
        '{"format":"csv","rows":1}',
        NULL,
        '127.0.0.1',
        'seed-script',
        '2026-03-03T10:15:00Z',
        '2026-03-03T10:15:00Z'
    )
ON CONFLICT (public_id) DO UPDATE
SET actor_user_id = EXCLUDED.actor_user_id,
    actor_email = EXCLUDED.actor_email,
    event_type = EXCLUDED.event_type,
    entity_type = EXCLUDED.entity_type,
    entity_id = EXCLUDED.entity_id,
    entity_key = EXCLUDED.entity_key,
    action = EXCLUDED.action,
    summary = EXCLUDED.summary,
    before_json = EXCLUDED.before_json,
    after_json = EXCLUDED.after_json,
    diff_json = EXCLUDED.diff_json,
    ip_address = EXCLUDED.ip_address,
    user_agent = EXCLUDED.user_agent,
    modified_utc = EXCLUDED.modified_utc;

COMMIT;
