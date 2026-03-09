-- HU-027 - Personnel files options (filters, sorting and dynamic query) indexing hardening

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_marital_status
    ON personnel_files (tenant_id, marital_status);

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_nationality
    ON personnel_files (tenant_id, nationality);

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_profession
    ON personnel_files (tenant_id, profession);

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_created_utc
    ON personnel_files (tenant_id, created_utc);

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_modified_utc
    ON personnel_files (tenant_id, modified_utc);
