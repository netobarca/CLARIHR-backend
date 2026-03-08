# Position Slots

## Scope

HU-013 introduce el modulo tenant-scoped de plazas y dependencias organizativas:

- CRUD de plazas (`PositionSlot`)
- estado `Vacant | Occupied | Suspended`
- dependencias `Direct` y `Functional`
- actualizacion de ocupacion por conteo (`occupiedEmployees`)
- grafo de dependencias y export de diagrama (`graphml|json|dot`)
- export tabular (`xlsx|csv`)

## Endpoints

- `POST /api/v1/companies/{companyId}/position-slots`
- `GET /api/v1/companies/{companyId}/position-slots`
- `GET /api/v1/position-slots/{id}`
- `PUT /api/v1/position-slots/{id}`
- `PATCH /api/v1/position-slots/{id}/status`
- `PATCH /api/v1/position-slots/{id}/dependencies`
- `PATCH /api/v1/position-slots/{id}/occupancy`
- `GET /api/v1/companies/{companyId}/position-slots/graph`
- `GET /api/v1/companies/{companyId}/position-slots/diagram-export?format=graphml|json|dot`
- `GET /api/v1/companies/{companyId}/position-slots/export?format=xlsx|csv`

## Security

- Requiere JWT.
- El `companyId` de la ruta debe coincidir con el claim `tid`.
- Lectura:
  - `PositionSlots.Read`
  - `PositionSlots.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura:
  - `PositionSlots.Admin`
  - `iam.administration.manage`
  - `platform_admin`

## Reglas importantes

- `code` es unico por tenant.
- `concurrencyToken` es obligatorio para updates por id.
- `maxEmployees >= 1`.
- `0 <= occupiedEmployees <= maxEmployees`.
- `effectiveFromUtc <= effectiveToUtc` cuando exista fecha fin.
- si `costCenterCode` se provee, debe existir y estar activo en `CostCenters`.
- No se permite autoreferencia en dependencias.
- No se permiten ciclos en dependencia directa.
- Plaza en `Suspended` no permite cambios de ocupacion.

## Contratos principales

Escrituras:

- `CreatePositionSlotRequest`
  - `code`, `title?`, `jobProfileId`, `orgUnitId`, `workCenterId?`
  - `costCenterCode?`, `directDependencyPositionSlotId?`, `functionalDependencyPositionSlotId?`
  - `status`, `maxEmployees`, `occupiedEmployees`, `isFixedTerm`
  - `effectiveFromUtc`, `effectiveToUtc?`, `notes?`
- `UpdatePositionSlotRequest`
  - mismo contrato base (sin status/occupied) + `concurrencyToken`
- `UpdatePositionSlotStatusRequest`
  - `status`, `concurrencyToken`
- `UpdatePositionSlotDependenciesRequest`
  - `directDependencyPositionSlotId?`, `functionalDependencyPositionSlotId?`, `concurrencyToken`
- `UpdatePositionSlotOccupancyRequest`
  - `occupiedEmployees`, `concurrencyToken`

Lecturas:

- `PositionSlotListItemResponse`
- `PositionSlotResponse`
- `PositionSlotGraphResponse`

`PositionSlotListItemResponse` y `PositionSlotResponse` incluyen `allowedActions` cuando se consulta con `includeAllowedActions=true`.

## Filtros de busqueda

`GET /api/v1/companies/{companyId}/position-slots`:

- `status?`
- `jobProfileId?`
- `orgUnitId?`
- `workCenterId?`
- `isFixedTerm?`
- `q?`
- `page`
- `pageSize`
- `includeAllowedActions` (default `false`)

`GET /api/v1/companies/{companyId}/position-slots/graph`:

- `rootId?`
- `depth?` (1..15)
- `includeFunctional` (default `true`)

## Errores esperados

- `400` validacion
- `403` forbidden o tenant mismatch
- `404` plaza o referencia no encontrada
- `409` conflictos (codigo, ciclo, estado, concurrencia)
- `422` reglas semanticas de capacidad/fechas y validacion de centro de costo (`POSITION_SLOT_COST_CENTER_INVALID`)

## Auditoria

Eventos auditados:

- `POSITION_SLOT_CREATED`
- `POSITION_SLOT_UPDATED`
- `POSITION_SLOT_STATUS_CHANGED`
- `POSITION_SLOT_DEPENDENCY_UPDATED`
- `POSITION_SLOT_OCCUPANCY_CHANGED`
