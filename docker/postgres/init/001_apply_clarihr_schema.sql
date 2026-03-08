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

\echo 'CLARIHR schema scripts completed.'
