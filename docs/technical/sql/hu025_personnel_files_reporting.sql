-- HU-025 - Personnel files reporting and analytics indexing hardening

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_active_birth_date
    ON personnel_files (tenant_id, is_active, birth_date);

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_type_birth_date
    ON personnel_files (tenant_id, record_type, birth_date);

CREATE INDEX IF NOT EXISTS ix_personnel_files__tenant_org_unit_active
    ON personnel_files (tenant_id, org_unit_public_id, is_active);
