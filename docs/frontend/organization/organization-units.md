# Organización — Organization Units

El **árbol organizativo** de la compañía (direcciones, gerencias, departamentos…). Cada unidad es un
nodo con un tipo, opcionalmente un área funcional, un padre, un centro de costo y un manager.

> Antes de consumir, leé las [Convenciones](./_conventions.md) y el
> [modelo de datos](./README.md#el-modelo-de-datos-leer-antes-de-integrar). Acá solo lo específico.

**Permisos:** `GET` → `OrgUnits.Read` · `POST/PUT/PATCH` → `OrgUnits.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/organization-units` | listar (paginado, filtros) |
| `GET` | `/organization-units/{publicId}` | detalle (+ `ETag`) |
| `GET` | `/companies/{companyPublicId}/organization-units/tree` | árbol jerárquico anidado |
| `GET` | `/companies/{companyPublicId}/organization-units/graph` | grafo (nodos + aristas) para diagramas |
| `GET` | `/companies/{companyPublicId}/organization-units/export` | exportar tabla (xlsx/csv/json) |
| `GET` | `/companies/{companyPublicId}/organization-units/diagram-export` | exportar diagrama (graphml/json/dot) |
| `POST` | `/companies/{companyPublicId}/organization-units` | crear |
| `PUT` | `/organization-units/{publicId}` | reemplazar campos editables (`If-Match`) |
| `PATCH` | `/organization-units/{publicId}` | JSON Patch parcial (`If-Match`) |
| `PATCH` | `/organization-units/{publicId}/move` | reparentar / reordenar (`If-Match`) |
| `PATCH` | `/organization-units/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/organization-units/{publicId}/inactivate` | inactivar (`If-Match`) |

**Filtros del listado:** `isActive`, `orgUnitTypeId`, `functionalAreaId`, `parentId`, `q` (≥2),
`page`, `pageSize`, `includeAllowedActions`.

## Request body — Create

| Campo | Tipo | Req. | Validación / Notas |
|-------|------|------|--------------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `name` | string | Sí | máx 150 |
| `orgUnitTypePublicId` | uuid | Sí | tipo de unidad **activo** (de [Org Structure Catalogs](./organization-structure-catalogs.md)) |
| `functionalAreaPublicId` | uuid | No | área funcional activa, si se usa |
| `parentPublicId` | uuid | No | unidad padre (define el árbol; omitir = raíz) |
| `sortOrder` | int | No | ≥ 0 |
| `description` | string | No | máx 500 |
| `costCenterCode` | string | No | **código** (no id) de un [Cost Center](./cost-centers.md) activo |
| `managerEmployeePublicId` | uuid | No | empleado responsable (Personnel Files) |

```json
{
  "code": "FIN", "name": "Finanzas",
  "orgUnitTypePublicId": "…", "functionalAreaPublicId": "…",
  "parentPublicId": "…", "costCenterCode": "CC-001", "sortOrder": 1
}
```

**Update (`PUT`)**: mismos campos editables (`code`, `name`, `orgUnitTypePublicId`,
`functionalAreaPublicId`, `sortOrder`, `description`, `costCenterCode`, `managerEmployeePublicId`).
El **padre NO se cambia por PUT** → usá `/move`.

**Patch (`PATCH`)**: solo `/name`, `/sortOrder`, `/description`. El resto (`code`, tipo, área, cost
center, manager) va por `PUT`; el padre por `/move`; el estado por `/activate`–`/inactivate`.

## Responses

`OrgUnitResponse` (detalle y escrituras): `publicId`, `code`, `name`,
`orgUnitType{publicId,code,name}`, `functionalArea{…}|null`, `parent{…}|null`, `sortOrder`,
`description`, `costCenterCode`, `managerEmployeePublicId`, `isActive`, `concurrencyToken`,
`createdAtUtc`, `modifiedAtUtc`.

Los items del **listado paginado** tienen el mismo shape **sin `description`** (solo detalle) —
para mostrarla o precargar el form de edición, pedí la unidad por id.

## Endpoints especiales

### `GET /tree`
Árbol anidado (`OrgUnitTreeNodeResponse` con `children[]` recursivo). Query opcional: `rootId`
(subárbol), `depth` (1–15). Rate limit de árbol.

### `GET /graph`
`{ nodes: [{publicId, label, orgUnitType…, isActive}], edges: [{fromPublicId, toPublicId}] }` — para
renderers de grafo. Mismo `rootId`/`depth`. Rate limit de árbol.

### `GET /export`
Descarga tabular. Query: `format` (`xlsx` default / `csv` / `json`; desconocido → `400`) + los mismos
filtros del listado. Excede el límite síncrono → `413`. Rate limit de export.

### `GET /diagram-export`
Descarga del diagrama. Query: `format` (`graphml` default / `json` / `dot`) + `rootId`/`depth`.
`413` si supera el máximo de nodos. Rate limit de export.

### `PATCH /move`
Reparenta y/o reordena.

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `newParentPublicId` | uuid | No | nuevo padre (`null`/omitir = mover a raíz) |
| `sortOrder` | int | No | nuevo orden (omitir = preservar) |

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `ORG_UNIT_CODE_CONFLICT` | 409 | código duplicado |
| `ORG_UNIT_NOT_FOUND` | 404 | inexistente / otro tenant |
| `ORG_UNIT_PARENT_NOT_FOUND` | 404 | padre (create/move) inexistente |
| `ORG_UNIT_CYCLE_DETECTED` | 409 | el move crearía un ciclo (mover bajo un descendiente) |
| `ORG_UNIT_DEPTH_LIMIT_EXCEEDED` | 409 | excede la profundidad máxima del árbol |
| `ORG_UNIT_HAS_ACTIVE_CHILDREN` | 409 | inactivar una unidad con hijos activos |
| `ORG_UNIT_COST_CENTER_INVALID` | 422 | `costCenterCode` inexistente o inactivo |
| `ORG_UNITS_FORBIDDEN` | 403 | sin permiso |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **Árbol**: `parentPublicId` define la jerarquía; el `move` valida ciclos (no mover una unidad bajo
  sí misma o un descendiente) y un **límite de profundidad**.
- **Inactivar**: bloqueado si tiene hijos activos (`409 ORG_UNIT_HAS_ACTIVE_CHILDREN`) — inactivá las
  hojas primero. En el listado, `hasActiveChildren` viene optimista (`false`) para no hacer N+1;
  el `GET` por id trae el flag real, y el `inactivate` aplica el guard de verdad.
- **Cost center por código**: `costCenterCode` referencia un Cost Center por su `code` (no por id);
  inválido/inactivo → `422`.
- **Tipo/área**: deben estar activos al momento del alta/edición.

## Guía FE

- Pantalla de organigrama: `GET /tree` (lazy con `rootId`/`depth` para árboles grandes) o `/graph`
  para diagrama. Para edición masiva, `/export`.
- Form de unidad: poblá los dropdowns con unit-types/functional-areas activos
  ([Org Structure Catalogs](./organization-structure-catalogs.md)) y cost centers activos
  ([Cost Centers](./cost-centers.md)).
- Mover nodos en el árbol → `PATCH /move` (no `PUT`); manejá `409` de ciclo/profundidad con un
  mensaje claro.
