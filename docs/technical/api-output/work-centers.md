# Work Centers

## Scope

HU-010 agrega dos bloques funcionales relacionados:

- tipos de centro de trabajo
- centros de trabajo

## Work Center Types

### Endpoints

- `GET /api/v1/companies/{companyId}/work-center-types`
- `POST /api/v1/companies/{companyId}/work-center-types`
- `PUT /api/v1/work-center-types/{id}`
- `PATCH /api/v1/work-center-types/{id}/activate`
- `PATCH /api/v1/work-center-types/{id}/inactivate`

### Reglas

- `code` unico por tenant
- no hay hard delete
- la inactivacion falla si existen centros activos usando el tipo

## Work Centers

### Endpoints

- `GET /api/v1/companies/{companyId}/work-centers`
- `GET /api/v1/work-centers/{id}`
- `POST /api/v1/companies/{companyId}/work-centers`
- `PUT /api/v1/work-centers/{id}`
- `PATCH /api/v1/work-centers/{id}/reassign-group`
- `PATCH /api/v1/work-centers/{id}/activate`
- `PATCH /api/v1/work-centers/{id}/inactivate`

### Reglas

- `code`, `name`, `workCenterTypeId` y `locationGroupId` son obligatorios.
- El grupo debe pertenecer al tenant actual.
- El grupo debe estar en un nivel con `allowsWorkCenters = true`.
- Si el tipo requiere direccion, `address` es obligatorio.
- Si el tipo requiere geo, `geoLat` y `geoLong` son obligatorios.
- Rangos validos:
  - `geoLat`: `-90..90`
  - `geoLong`: `-180..180`
- La inactivacion usa `ILocationDependencyPolicy` y ya deja preparado el hook para dependencias futuras.

## Concurrency

- Todos los updates operan con `concurrencyToken`.
- Conflictos concurrentes responden `409 CONCURRENCY_CONFLICT`.
