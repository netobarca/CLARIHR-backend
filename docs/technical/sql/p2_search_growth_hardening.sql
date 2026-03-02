-- P2.2 - Search growth hardening
-- PostgreSQL strategy for substring search acceleration with pg_trgm.
-- Apply only when the documented thresholds are met.

create extension if not exists pg_trgm;

-- Company users / auth users
create index if not exists ix_auth_users__normalized_email_trgm
    on auth_users using gin (normalized_email gin_trgm_ops);

create index if not exists ix_auth_users__upper_first_name_trgm
    on auth_users using gin (upper(first_name) gin_trgm_ops);

create index if not exists ix_auth_users__upper_last_name_trgm
    on auth_users using gin (upper(last_name) gin_trgm_ops);

-- IAM users
create index if not exists ix_iam_users__normalized_email_trgm
    on iam_users using gin (normalized_email gin_trgm_ops);

create index if not exists ix_iam_users__upper_first_name_trgm
    on iam_users using gin (upper(first_name) gin_trgm_ops);

create index if not exists ix_iam_users__upper_last_name_trgm
    on iam_users using gin (upper(last_name) gin_trgm_ops);

-- IAM roles
create index if not exists ix_iam_roles__normalized_name_trgm
    on iam_roles using gin (normalized_name gin_trgm_ops);

create index if not exists ix_iam_roles__upper_description_trgm
    on iam_roles using gin (upper(description) gin_trgm_ops);

-- IAM permissions
create index if not exists ix_iam_permissions__normalized_code_trgm
    on iam_permissions using gin (normalized_code gin_trgm_ops);

create index if not exists ix_iam_permissions__normalized_module_trgm
    on iam_permissions using gin (normalized_module gin_trgm_ops);

create index if not exists ix_iam_permissions__normalized_screen_trgm
    on iam_permissions using gin (normalized_screen gin_trgm_ops);

create index if not exists ix_iam_permissions__upper_name_trgm
    on iam_permissions using gin (upper(name) gin_trgm_ops);

-- Audit logs
create index if not exists ix_audit_logs__upper_actor_email_trgm
    on audit_logs using gin (upper(actor_email) gin_trgm_ops);

create index if not exists ix_audit_logs__upper_summary_trgm
    on audit_logs using gin (upper(summary) gin_trgm_ops);
