\echo 'Applying CLARIHR schema scripts...'

\i /sql/initial_schema.sql
\i /sql/hu002_provisioning.sql
\i /sql/hu003_company_users.sql
\i /sql/hu005_rbac_hardening.sql
\i /sql/hu006_field_permissions.sql
\i /sql/iteration_3_hardening.sql
\i /sql/hu008_admin_audit_logs.sql
\i /sql/hu009_account_companies.sql
\i /sql/hu010_locations.sql
\i /sql/hu011_org_units.sql
\i /sql/hu012_job_profiles.sql
\i /sql/hu013_position_slots.sql
\i /sql/hu014_salary_tabulator.sql
\i /sql/hu015_cost_centers.sql
\i /sql/hu016_legal_representatives.sql
\i /sql/hu018_competency_framework.sql
\i /sql/hu019_org_structure_catalogs.sql
\i /sql/hu020_position_description_catalogs.sql
\i /sql/hu021_jobprofiles_position_catalog_integration.sql
\i /sql/hu022_salary_tabulator_catalog_backfill.sql
\i /sql/hu023_personnel_files.sql
\i /sql/hu024_personnel_files_documents_custom_fields.sql
\i /sql/hu025_personnel_files_reporting.sql
\i /sql/hu026_personnel_files_curriculum.sql
\i /sql/hu027_personnel_files_options_hardening.sql
\i /sql/hu028_personnel_files_employee_core.sql
\i /sql/hu029_personnel_files_employee_operations.sql
\i /sql/hu030_personnel_files_employee_integrations.sql

\echo 'CLARIHR schema scripts completed.'
