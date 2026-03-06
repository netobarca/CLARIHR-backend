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

\echo 'CLARIHR schema scripts completed.'
