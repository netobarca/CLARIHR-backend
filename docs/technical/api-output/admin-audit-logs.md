# Administrative Audit Logs

HU-008 introduces a tenant-scoped audit log for administrative actions over users, roles, and RBAC permissions.

## Scope

Audited events:

- `USER_CREATED`
- `USER_UPDATED`
- `USER_DEACTIVATED`
- `USER_REACTIVATED`
- `USER_INVITED`
- `USER_INVITATION_RESET`
- `ROLE_CREATED`
- `ROLE_UPDATED`
- `ROLE_CLONED`
- `ROLE_RESOURCE_PERMISSIONS_UPDATED`
- `ROLE_FIELD_PERMISSIONS_UPDATED`

## Architecture

Application:

- `IAuditService`
- `IAuditSanitizer`
- `IAuditLogRepository`
- `GetAuditLogsQuery`
- `GetAuditLogDetailQuery`

Infrastructure:

- `AuditService`
- `AuditSanitizer`
- `AuditLogRepository`
- `AuditLog` persistence configuration

API:

- `GET /api/audit/logs`
- `GET /api/audit/logs/{auditLogId}`

## Authorization

Audit queries are protected with `AuthorizeResource("AUDIT_LOGS", Read)`.

The `AUDIT_LOGS` resource is part of `PermissionMatrixCatalog` and is evaluated by the same RBAC enforcement introduced in HU-007.

`RBAC.PERMISSIONS.MANAGE` remains the manage override for this screen, so existing security administrators keep read access even before explicit matrix assignment.

## Data Shape

Each audit log stores:

- tenant/company
- actor user id and email snapshot
- event type
- entity type and entity id
- entity key when applicable
- action
- summary
- sanitized `before`, `after`, and `diff`
- ip address
- user agent
- UTC creation timestamp

## Sanitization

Audit payloads are serialized through `AuditSanitizer`, which removes sensitive properties such as:

- password / passwordHash
- refresh tokens
- activation tokens
- raw tokens
- secrets / api keys / private keys

Handlers pass explicit whitelisted snapshots whenever possible to avoid leaking unrelated state.

## Notes

- Audit logs are immutable from the application layer.
- Queries are tenant-scoped by the global EF tenant filter.
- Detail requests return `TENANT_MISMATCH` when the log exists but belongs to another tenant.
