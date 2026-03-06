# Org Units

## Scope

HU-011 introduce administracion tenant-scoped de unidades organizativas para organigrama:

- CRUD de unidades por empresa
- movimiento de nodos (cambio de dependencia)
- consultas en modo lista, arbol y grafo
- proteccion anti-ciclos y concurrencia optimista

## Endpoints

- `POST /api/v1/companies/{companyId}/org-units`
- `GET /api/v1/companies/{companyId}/org-units`
- `GET /api/v1/org-units/{id}`
- `PUT /api/v1/org-units/{id}`
- `PATCH /api/v1/org-units/{id}/move`
- `PATCH /api/v1/org-units/{id}/activate`
- `PATCH /api/v1/org-units/{id}/inactivate`
- `GET /api/v1/companies/{companyId}/org-units/tree`
- `GET /api/v1/companies/{companyId}/org-units/graph`

## Security

- Requiere JWT.
- `companyId` debe coincidir con el `tid` actual del token.
- Lectura:
  - `OrgUnits.Read`
  - `OrgUnits.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura:
  - `OrgUnits.Admin`
  - `iam.administration.manage`
  - `platform_admin`

## Reglas importantes

- `code` es unico por tenant.
- no se permiten ciclos en la jerarquia.
- profundidad maxima: `15` niveles.
- no se puede inactivar una unidad con hijos activos.
- la concurrencia se valida con `concurrencyToken` y devuelve `409 CONCURRENCY_CONFLICT`.

## Contratos principales

Busqueda `GET /api/v1/companies/{companyId}/org-units`:

- `isActive?`
- `type?`
- `parentId?`
- `q?`
- `page` (default `1`)
- `pageSize` (default `20`)

Escrituras:

- `CreateOrgUnitRequest`
  - `code`, `name`, `unitType`, `parentId?`, `sortOrder?`, `description?`, `costCenterCode?`, `managerEmployeeId?`
- `UpdateOrgUnitRequest`
  - `code`, `name`, `unitType`, `sortOrder?`, `description?`, `costCenterCode?`, `managerEmployeeId?`, `concurrencyToken`
- `MoveOrgUnitRequest`
  - `newParentId?`, `sortOrder?`, `concurrencyToken`
- `ConcurrencyRequest`
  - `concurrencyToken`

Respuesta base:

- `OrgUnitResponse`
  - `id`, `code`, `name`, `unitType`, `parentId`, `sortOrder`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`

## Errores esperados

- `400` validacion de input
- `403` forbidden o tenant mismatch
- `404` recurso inexistente
- `409` conflictos de codigo, ciclo, profundidad, hijos activos o concurrencia

## Auditoria

Eventos auditados:

- `ORG_UNIT_CREATED`
- `ORG_UNIT_UPDATED`
- `ORG_UNIT_MOVED`
- `ORG_UNIT_ACTIVATED`
- `ORG_UNIT_INACTIVATED`
