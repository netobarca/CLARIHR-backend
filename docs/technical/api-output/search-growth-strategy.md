# Search Growth Strategy

## Objective

Definir como debe evolucionar la búsqueda textual del sistema cuando el volumen de datos crezca, sin introducir regresiones funcionales ni complejidad prematura.

## Current Search Surface

Los endpoints que hoy dependen de búsqueda textual son:

- `GET /api/company/users`
- `GET /api/iam/users`
- `GET /api/iam/roles`
- `GET /api/iam/permissions`
- `GET /api/audit/logs`

La implementación actual usa substring match con `Contains` sobre columnas normalizadas o expresiones `ToUpper()`.

## Decision

La semántica actual de búsqueda se mantiene:

- búsqueda parcial
- case-insensitive
- sin cambiar a prefix-only

No se cambia hoy a `StartsWith`, porque eso sí alteraría el comportamiento funcional esperado por usuarios y QA.

La estrategia elegida para crecimiento es:

1. Mantener la implementación actual mientras el volumen siga siendo moderado.
2. Activar `pg_trgm` e índices GIN sobre los campos realmente buscados cuando se alcance un umbral operativo.
3. Medir antes de introducir un motor más complejo o full text search.

## Why This Strategy

Razones:

- `Contains` con substring es correcto funcionalmente hoy.
- Cambiar la semántica a prefijo reduciría recall y podría romper expectativas del negocio.
- En PostgreSQL, `pg_trgm` es la opción natural para acelerar búsquedas con `%term%`.
- Es una mejora incremental, reversible y de bajo impacto sobre el código.

## Strategy By Endpoint

### 1. Company Users

Campos:

- `auth_users.normalized_email`
- `upper(auth_users.first_name)`
- `upper(auth_users.last_name)`

Estrategia:

- mantener query actual
- agregar índices trigram sobre email y expresiones `upper(first_name)` / `upper(last_name)` cuando el volumen lo requiera

### 2. IAM Users

Campos:

- `iam_users.normalized_email`
- `upper(iam_users.first_name)`
- `upper(iam_users.last_name)`

Estrategia:

- mantener query actual
- agregar índices trigram equivalentes por tenant-aware dataset

### 3. IAM Roles

Campos:

- `iam_roles.normalized_name`
- `upper(iam_roles.description)`

Estrategia:

- trigram sobre `normalized_name`
- trigram opcional sobre `upper(description)` si realmente se vuelve campo de búsqueda frecuente

### 4. IAM Permissions

Campos:

- `iam_permissions.normalized_code`
- `iam_permissions.normalized_module`
- `iam_permissions.normalized_screen`
- `upper(iam_permissions.name)`

Estrategia:

- trigram sobre los tres campos normalizados de catálogo
- trigram sobre `upper(name)` si el catálogo crece lo suficiente para hacerlo costoso

### 5. Audit Logs

Campos:

- `upper(audit_logs.actor_email)`
- `upper(audit_logs.summary)`

Estrategia:

- mantener primero el filtrado por tenant, fechas y actor
- agregar trigram sobre `upper(actor_email)` y `upper(summary)` cuando el historial crezca

## Activation Thresholds

Aplicar el hardening SQL cuando ocurra cualquiera de estas condiciones:

1. `p95` de búsqueda > `200ms` sostenido en ambiente similar a producción.
2. Más de `50k` usuarios IAM por tenant o `50k` usuarios auth relevantes para company users.
3. Más de `100k` audit logs consultables por tenant.
4. Soporte/QA reporta degradación perceptible en búsquedas administrativas.

## Operational Steps

### Phase 0

Estado actual:

- mantener queries actuales
- mantener semántica actual
- sin costo operativo adicional

### Phase 1

Cuando se alcance un umbral:

- aplicar `docs/technical/sql/p2_search_growth_hardening.sql`
- validar planes con `EXPLAIN ANALYZE`
- medir `p95` antes y después

### Phase 2

Solo si `pg_trgm` no fuera suficiente:

- evaluar búsqueda especializada por módulo
- considerar full text search solo para campos narrativos como `summary`

## Non-Goals

Esta estrategia no incluye por ahora:

- cambiar semántica de búsqueda del producto
- introducir Elastic/OpenSearch
- hacer full text search generalizado

## Code References

- `src/CLARIHR.Infrastructure/Companies/UserCompanyRepository.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/IamAdministrationRepository.cs`
- `src/CLARIHR.Infrastructure/Auditing/AuditLogRepository.cs`

## SQL Artifact

El artefacto opcional para endurecimiento de búsquedas está en:

- `docs/technical/sql/p2_search_growth_hardening.sql`
