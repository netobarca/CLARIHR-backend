-- HU-009 - Account-level multi-company management
-- This iteration does not require new tables.
-- It adds ownership-focused indexes to support account company list/count flows.

CREATE INDEX IF NOT EXISTS ix_companies_created_by_user_status
    ON companies (created_by_user_public_id, status);

CREATE INDEX IF NOT EXISTS ix_companies_created_by_user_created_utc_desc
    ON companies (created_by_user_public_id, created_utc DESC);
