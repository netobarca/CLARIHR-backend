# Organización — Cost Centers

Centros de costo para imputación contable. Transversales: los referencian las Organization Units
(por código) y los Position Slots. El **tipo** de centro de costo ya no es un enum: es un
**catálogo por compañía** (`cost-center-types`) que se administra con su propio CRUD.

> Antes de consumir, leé las [Convenciones](./_conventions.md). Acá solo lo específico.

**Permisos:** `GET` → `CostCenters.Read` · `POST/PUT/PATCH` → `CostCenters.Manage` (aplican a ambos
recursos: cost centers y cost center types).

## Endpoints — Cost Centers

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/cost-centers` | listar (paginado, filtros) |
| `GET` | `/cost-centers/{publicId}` | detalle (+ `ETag`) |
| `GET` | `/cost-centers/{publicId}/usage` | conteo de referencias (org units + position slots) |
| `GET` | `/companies/{companyPublicId}/cost-centers/export` | exportar (xlsx/…) |
| `POST` | `/companies/{companyPublicId}/cost-centers` | crear |
| `PUT` | `/cost-centers/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/cost-centers/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/cost-centers/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/cost-centers/{publicId}/inactivate` | inactivar (`If-Match`) |

**Filtros del listado:** `typeId` (public id del tipo), `isActive`, `q` (≥2), `page`, `pageSize`,
`includeAllowedActions`.

## El catálogo de tipos

El CRUD del catálogo (`/cost-center-types`) está documentado en
[cost-center-types.md](./cost-center-types.md) — integralo **antes** que este recurso: las
compañías nuevas inician con el catálogo vacío y el create de un cost center exige un tipo activo
(`costCenterTypePublicId`).

## Request body — Create / Update (cost center)

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `name` | string | Sí | máx 150 |
| `costCenterTypePublicId` | uuid | Sí | public id de un tipo **activo** de la compañía |
| `payrollExpenseAccountCode` | string | No | máx 100, formato `^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$` |
| `employerContributionAccountCode` | string | No | ídem |
| `provisionAccountCode` | string | No | ídem |
| `description` | string | No | máx 500 |

```json
{
  "code": "CC-001", "name": "Nómina Administración",
  "costCenterTypePublicId": "0a1b2c3d-…",
  "payrollExpenseAccountCode": "5101.01", "description": "…"
}
```

**Patch** patchables: `/code`, `/name`, `/costCenterTypeId` (uuid de un tipo activo), los tres
`*AccountCode`, `/description` (el estado va por activate/inactivate).

Regla del tipo: asignar un tipo **distinto** exige que esté activo; conservar el tipo actual (aunque
haya sido inactivado después) sigue siendo válido en update/patch.

## Responses

`CostCenterResponse`: `publicId`, `companyPublicId`, `code`, `name`, `costCenterTypeId`,
`costCenterTypeCode`, `costCenterTypeName`, los tres `*AccountCode|null`, `description`, `isActive`,
`concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`, `allowedActions{read,manage,canInactivate}?`.

El list item incluye los mismos campos de tipo (`costCenterTypeId/Code/Name`), así el selector y la
columna de tipo no necesitan un segundo fetch.

### `GET /usage`

```json
{
  "publicId": "…", "code": "CC-001", "name": "…",
  "orgUnitActiveReferences": 3, "orgUnitInactiveReferences": 1,
  "positionSlotActiveReferences": 12, "positionSlotInactiveReferences": 0,
  "hasActiveReferences": true
}
```

Llamalo **antes** de ofrecer inactivar — si `hasActiveReferences: true`, el inactivate dará `409`.

### `GET /export`
Descarga con los mismos filtros (`typeId`, `isActive`, `q`). Query `format` (default `xlsx`;
desconocido → `400 COST_CENTER_EXPORT_FORMAT_INVALID`). Excede el límite → `413`. **Rate limit
10/min** (search 120/min; el listado de tipos comparte el rate limit de search).

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `COST_CENTER_CODE_CONFLICT` | 409 | código de cost center duplicado |
| `COST_CENTER_NOT_FOUND` | 404 | cost center inexistente |
| `COST_CENTER_IN_USE` | 409 | inactivar uno aún referenciado por org units / position slots activos |
| `COST_CENTER_TYPE_NOT_FOUND` | 404 | el `costCenterTypePublicId` no existe en la compañía |
| `COST_CENTER_TYPE_CODE_CONFLICT` | 409 | código de tipo duplicado |
| `COST_CENTER_TYPE_INACTIVE` | 409 | se intentó asignar un tipo inactivo |
| `COST_CENTER_TYPE_IN_USE` | 409 | inactivar un tipo aún usado por cost centers activos |
| `COST_CENTER_EXPORT_FORMAT_INVALID` | 400 | formato de export desconocido |
| `TENANT_MISMATCH` | 403 | recurso de otra compañía |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **Inactivar un cost center** está bloqueado mientras tenga referencias activas
  (`409 COST_CENTER_IN_USE`); usá `/usage` para anticiparlo y mostrar dónde se usa.
- **Inactivar un tipo** está bloqueado mientras cost centers activos lo usen
  (`409 COST_CENTER_TYPE_IN_USE`).
- Las Organization Units referencian el cost center **por `code`** (no por id) — el `code` es la
  clave estable de imputación.

## Guía FE

- Selector de tipo en el form de cost center: listá `/cost-center-types?isActive=true` y mandá el
  `publicId` elegido como `costCenterTypePublicId`. Si la compañía no tiene tipos, ofrecé crear uno
  primero (pantalla del catálogo).
- Selector de cost center en el form de Organization Unit: listá activos y mandá el `code` elegido
  como `costCenterCode`.
- Antes de inactivar (centro o tipo), anticipá el `409`: para centros llamá `/usage`; para tipos,
  filtrá `/cost-centers?typeId=…&isActive=true` y mostrá cuántos lo usan.
