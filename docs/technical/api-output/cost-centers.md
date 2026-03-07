# Cost Centers

## Scope

HU-015 introduce el modulo tenant-scoped de centros de costo contable:

- CRUD de centros de costo (`CostCenter`)
- activacion/inactivacion con bloqueo por uso activo
- consulta de uso (`usage`) sobre `OrgUnits` y `PositionSlots`
- export de listado (`csv|xlsx`)
- validacion semantica de `costCenterCode` en escrituras de `OrgUnits` y `PositionSlots`

## Endpoints

- `POST /api/v1/companies/{companyId}/cost-centers`
- `GET /api/v1/companies/{companyId}/cost-centers`
- `GET /api/v1/cost-centers/{id}`
- `PUT /api/v1/cost-centers/{id}`
- `PATCH /api/v1/cost-centers/{id}/activate`
- `PATCH /api/v1/cost-centers/{id}/inactivate`
- `GET /api/v1/cost-centers/{id}/usage`
- `GET /api/v1/companies/{companyId}/cost-centers/export?format=csv|xlsx`

## Security

- Requiere JWT.
- El `companyId` de la ruta debe coincidir con el claim `tid`.
- Lectura:
  - `CostCenters.Read`
  - `CostCenters.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura:
  - `CostCenters.Admin`
  - `iam.administration.manage`
  - `platform_admin`

## Reglas importantes

- `code` es unico por tenant.
- `concurrencyToken` es obligatorio en operaciones por id.
- inactivacion bloqueada cuando el centro de costo tiene uso activo en `OrgUnits` o `PositionSlots`.
- validaciones de referencia en escrituras:
  - `OrgUnits.costCenterCode` debe existir y estar activo.
  - `PositionSlots.costCenterCode` debe existir y estar activo.

## Contratos principales

Escrituras:

- `CreateCostCenterRequest`
  - `code`, `name`, `type`
  - `payrollExpenseAccountCode?`, `employerContributionAccountCode?`, `provisionAccountCode?`
  - `description?`
- `UpdateCostCenterRequest`
  - mismo contrato + `concurrencyToken`
- `ConcurrencyRequest`
  - `concurrencyToken`

Lecturas:

- `CostCenterListItemResponse`
- `CostCenterResponse`
- `CostCenterUsageResponse`

## Filtros de busqueda

`GET /api/v1/companies/{companyId}/cost-centers`:

- `type?`
- `isActive?`
- `q?`
- `page`
- `pageSize`

`GET /api/v1/companies/{companyId}/cost-centers/export`:

- `format=csv|xlsx`
- `type?`
- `isActive?`
- `q?`

## Errores esperados

- `400` validacion
- `403` forbidden o tenant mismatch
- `404` centro de costo inexistente
- `409` conflictos (codigo, concurrencia, centro en uso)
- `422` reglas semanticas de referencia para `costCenterCode` en otros modulos

## Auditoria

Eventos auditados:

- `COST_CENTER_CREATED`
- `COST_CENTER_UPDATED`
- `COST_CENTER_ACTIVATED`
- `COST_CENTER_INACTIVATED`
