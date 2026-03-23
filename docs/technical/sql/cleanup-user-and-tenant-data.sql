-- =============================================================================
-- CLEANUP: Remove a user and ALL their provisioned company/tenant data
-- =============================================================================
-- Target: PostgreSQL
-- Usage:  Replace 'TU_EMAIL@EJEMPLO.COM' with the actual user email.
--         Run inside a transaction. Review output before committing.
-- WARNING: This is IRREVERSIBLE. Always run inside BEGIN/ROLLBACK first.
-- =============================================================================

BEGIN;

DO $$
DECLARE
    v_user_email     TEXT := 'TU_EMAIL@EJEMPLO.COM';  -- <-- CAMBIAR AQUI
    v_user_id        BIGINT;
    v_user_public_id UUID;
    v_company_id     BIGINT;
    v_tenant_id      UUID;
    v_company_name   TEXT;
BEGIN
    -- =========================================================================
    -- 1. LOCATE USER
    -- =========================================================================
    SELECT id, public_id
      INTO v_user_id, v_user_public_id
      FROM auth_users
     WHERE LOWER(email) = LOWER(v_user_email)
        OR LOWER(normalized_email) = LOWER(v_user_email);

    IF v_user_id IS NULL THEN
        RAISE NOTICE 'Existing emails in auth_users:';
        PERFORM email FROM auth_users LIMIT 10;
        RAISE EXCEPTION 'User not found with email: %. Run: SELECT id, email, normalized_email FROM auth_users;', v_user_email;
    END IF;

    RAISE NOTICE '[1/4] User found: id=%, public_id=%', v_user_id, v_user_public_id;

    -- =========================================================================
    -- 2. LOCATE PRIMARY COMPANY
    -- =========================================================================
    SELECT c.id, c.public_id, c.name
      INTO v_company_id, v_tenant_id, v_company_name
      FROM companies c
     INNER JOIN user_companies uc ON uc.company_id = c.id
     WHERE uc.user_id = v_user_id
     LIMIT 1;

    IF v_company_id IS NOT NULL THEN
        RAISE NOTICE '[2/4] Company found: id=%, tenant_id=%, name=%',
            v_company_id, v_tenant_id, v_company_name;

        -- =====================================================================
        -- 3. DELETE ALL TENANT-SCOPED DATA (order respects FK constraints)
        -- =====================================================================

        -- 3.1 Position Slots (RESTRICT on job_profiles, org_units, work_centers)
        --     Clear self-referencing FKs first
        UPDATE position_slots
           SET direct_dependency_position_slot_id = NULL,
               functional_dependency_position_slot_id = NULL
         WHERE tenant_id = v_tenant_id;
        DELETE FROM position_slots WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - position_slots deleted';

        -- 3.2 Competency Framework (FK to job_profiles and conducts)
        DELETE FROM job_profile_competency_expectation_conducts WHERE tenant_id = v_tenant_id;
        DELETE FROM job_profile_competency_expectations WHERE tenant_id = v_tenant_id;
        DELETE FROM competency_conduct_behaviors WHERE tenant_id = v_tenant_id;
        DELETE FROM competency_conducts WHERE tenant_id = v_tenant_id;
        DELETE FROM occupational_pyramid_levels WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - competency framework deleted';

        -- 3.3 Job Profiles (CASCADE handles 11 child tables)
        DELETE FROM job_profiles WHERE tenant_id = v_tenant_id;
        DELETE FROM job_catalog_items WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - job_profiles deleted';

        -- 3.4 Position Description Catalogs
        DELETE FROM position_category_classifications WHERE tenant_id = v_tenant_id;
        DELETE FROM position_categories WHERE tenant_id = v_tenant_id;
        DELETE FROM position_description_catalog_items WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - position catalogs deleted';

        -- 3.5 Personnel Files (CASCADE handles 35+ child tables)
        DELETE FROM personnel_files WHERE tenant_id = v_tenant_id;
        DELETE FROM personnel_catalog_items WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - personnel_files deleted';

        -- 3.6 Org Units (clear self-referencing FK first)
        UPDATE org_units SET parent_id = NULL WHERE tenant_id = v_tenant_id;
        DELETE FROM org_units WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - org_units deleted';

        -- 3.7 Org Structure Catalogs
        DELETE FROM org_unit_type_catalog_items WHERE tenant_id = v_tenant_id;
        DELETE FROM functional_area_catalog_items WHERE tenant_id = v_tenant_id;
        -- company_type_catalog_items uses owner_user_public_id, not tenant_id
        DELETE FROM company_type_catalog_items WHERE owner_user_public_id = v_user_public_id;
        RAISE NOTICE '  - org structure catalogs deleted';

        -- 3.8 Cost Centers
        DELETE FROM cost_centers WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - cost_centers deleted';

        -- 3.9 Salary Tabulator
        DELETE FROM salary_tabulator_change_requests WHERE tenant_id = v_tenant_id;
        DELETE FROM salary_tabulator_lines WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - salary tabulator deleted';

        -- 3.10 Legal Representatives (catalogs are global/seeded, only delete tenant data)
        DELETE FROM legal_representatives WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - legal representatives deleted';

        -- 3.11 Locations
        DELETE FROM work_centers WHERE tenant_id = v_tenant_id;
        DELETE FROM work_center_types WHERE tenant_id = v_tenant_id;
        DELETE FROM location_groups WHERE tenant_id = v_tenant_id;
        DELETE FROM location_levels WHERE tenant_id = v_tenant_id;
        DELETE FROM location_hierarchy_configs WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - locations deleted';

        -- 3.12 RBAC Audit & Field Permissions
        DELETE FROM rbac_permission_audit_logs WHERE tenant_id = v_tenant_id;
        DELETE FROM field_permission_audit_logs WHERE tenant_id = v_tenant_id;
        DELETE FROM role_field_permissions WHERE tenant_id = v_tenant_id;
        DELETE FROM audit_logs WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - audit logs & field permissions deleted';

        -- 3.13 user_companies (RESTRICT FK to iam_roles, must go first)
        DELETE FROM user_companies WHERE company_id = v_company_id;
        RAISE NOTICE '  - user_companies deleted';

        -- 3.14 IAM (order: assignments -> users -> roles -> permissions)
        DELETE FROM iam_user_role_assignments WHERE tenant_id = v_tenant_id;
        DELETE FROM iam_role_permission_assignments WHERE tenant_id = v_tenant_id;
        DELETE FROM iam_users WHERE tenant_id = v_tenant_id;
        DELETE FROM iam_roles WHERE tenant_id = v_tenant_id;
        DELETE FROM iam_permissions WHERE tenant_id = v_tenant_id;
        RAISE NOTICE '  - IAM data deleted';

        -- =====================================================================
        -- 3.15 GLOBAL RECORDS LINKED TO THIS COMPANY
        -- =====================================================================
        DELETE FROM company_invitation_tokens WHERE company_id = v_company_id;
        DELETE FROM company_subscriptions WHERE company_id = v_company_id;
        DELETE FROM companies WHERE id = v_company_id;
        RAISE NOTICE '  - company & memberships deleted';

    ELSE
        RAISE NOTICE '[2/4] No company found for this user (skipping tenant cleanup)';
    END IF;

    -- =========================================================================
    -- 4. DELETE GLOBAL USER DATA
    -- =========================================================================
    DELETE FROM auth_refresh_tokens WHERE user_id = v_user_id;
    DELETE FROM auth_users WHERE id = v_user_id;
    RAISE NOTICE '[4/4] User deleted: % (%)', v_user_public_id, v_user_email;

    RAISE NOTICE '';
    RAISE NOTICE '=== CLEANUP COMPLETE ===';
    RAISE NOTICE 'User:    % (%)', v_user_email, v_user_public_id;
    RAISE NOTICE 'Company: % (%)', v_company_name, v_tenant_id;
    RAISE NOTICE '';
    RAISE NOTICE 'Review the output above, then:';
    RAISE NOTICE '  COMMIT;   -- to confirm deletion';
    RAISE NOTICE '  ROLLBACK; -- to cancel';
END $$;

-- IMPORTANT: Review the NOTICE output above before deciding:
-- COMMIT;
-- ROLLBACK;
