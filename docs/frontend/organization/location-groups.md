# Organización — Location Groups

Los **nodos del árbol de ubicaciones**: cada grupo vive en un nivel (`levelOrder`) y cuelga de un
padre del nivel inmediatamente superior (ej. "Honduras" → "Región Norte" → "Planta SPS"). Es donde
luego se ubican los [Work Centers](./work-centers.md).

> Antes de consumir, leé las [Convenciones](./_conventions.md) y
> [Location Hierarchy & Levels](./location-hierarchy-and-levels.md). Acá solo lo específico.

**Permisos:** `GET` → `Locations.Read` · `POST/PUT/PATCH` → `Locations.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/location-groups/tree` | árbol completo anidado |
| `GET` | `/companies/{companyPublicId}/location-groups` | listar plano (paginado, filtros) |
| `GET` | `/location-groups/{publicId}` | detalle (+ `ETag`) |
| `GET` | `/location-groups/{publicId}/children` | hijos directos (lazy-load del árbol) |
| `GET` | `/location-groups/{publicId}/path` | breadcrumb raíz→nodo |
| `GET` | `/location-groups/{publicId}/usage` | conteos + `canInactivate` |
| `POST` | `/companies/{companyPublicId}/location-groups` | crear |
| `PUT` | `/location-groups/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/location-groups/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/location-groups/{publicId}/move` | reparentar (`If-Match`) |
| `PATCH` | `/location-groups/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/location-groups/{publicId}/inactivate` | inactivar (`If-Match`) |

**Filtros del listado:** `levelOrder`, `isActive`, `q` (≥2), `page`, `pageSize`,
`includeAllowedActions`.

## Request body — Create

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `levelOrder` | int | Sí | > 0; el nivel debe existir y estar activo |
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `name` | string | Sí | máx 150 |
| `parentPublicId` | uuid | No\* | \*requerido si `levelOrder > 1`; el padre debe ser activo y de `levelOrder - 1`. Para raíz (`levelOrder = 1`): `null` |
| `description` | string | No | máx 500 |

```json
{ "levelOrder": 3, "code": "SPS-01", "name": "Planta San Pedro Sula", "parentPublicId": "…" }
```

**Update (`PUT`)**: `code`, `name`, `description` (el **nivel y el padre son inmutables** — el padre
se cambia por `/move`).
**Patch (`PATCH`)**: `/code`, `/name`, `/description`.

## Responses

`LocationGroupResponse`: `publicId`, `levelOrder`, `code`, `name`, `parentPublicId|null`,
`description`, `isActive`, `isDefault`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`,
`allowedActions?`.

## Endpoints especiales

### `GET /tree`
Árbol anidado (`LocationGroupTreeNodeResponse` con `children[]`). Rate limit de árbol.

### `GET /{id}/children?isActive=`
Hijos **directos** (un nivel abajo), filtrable por `isActive`. Para construir el árbol con lazy-load.

### `GET /{id}/path`
Breadcrumb raíz→nodo (`[{publicId, levelOrder, code, name}]`).

### `GET /{id}/usage`
```json
{
  "publicId": "…", "code": "…", "name": "…",
  "activeChildGroupCount": 2, "inactiveChildGroupCount": 0,
  "activeWorkCenterCount": 5, "inactiveWorkCenterCount": 1,
  "isDefault": false, "canInactivate": false
}
```
`canInactivate: false` si es el grupo default, tiene hijos activos, o tiene work centers activos.

### `PATCH /move`
| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `parentPublicId` | uuid | No | nuevo padre (`null` solo para nivel 1); debe ser activo y de `levelOrder - 1` |

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `LOCATION_GROUP_CODE_CONFLICT` | 409 | código duplicado |
| `LOCATION_GROUP_NOT_FOUND` | 404 | inexistente / otro tenant |
| `LOCATION_GROUP_INVALID_PARENT` | 409 | padre de nivel incorrecto, o raíz con padre |
| `LOCATION_GROUP_PARENT_REQUIRED` | 409 | nivel > 1 sin padre |
| `LOCATION_GROUP_PARENT_INACTIVE` | 409 | padre inactivo |
| `LOCATION_GROUP_CYCLE_DETECTED` | 409 | el move crearía un ciclo |
| `LOCATION_GROUP_HAS_ACTIVE_CHILDREN` | 409 | inactivar con hijos activos |
| `LOCATION_GROUP_HAS_ACTIVE_WORK_CENTERS` | 409 | inactivar con work centers activos |
| `DEFAULT_GROUP_PROTECTED` | 409 | editar code/name o inactivar el grupo default |
| `LOCATION_LEVEL_NOT_FOUND` | 404 | el `levelOrder` no existe o está inactivo |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **Colocación por nivel**: un grupo de `levelOrder = N` cuelga de un padre de `levelOrder = N-1`
  (activo). Los de nivel 1 son raíces (`parentPublicId: null`).
- **Grupo default protegido**: hay un grupo "General" que no se puede renombrar, recodificar ni
  inactivar (`409 DEFAULT_GROUP_PROTECTED`); su `description` sí se puede editar.
- **Inactivar**: bloqueado con hijos activos o work centers activos — usá `/usage` para anticiparlo.
- **Move**: valida nivel del padre y ciclos.

## Guía FE

- Árbol de ubicaciones: `GET /tree` para la vista completa, o `/children` (lazy) + `/path`
  (breadcrumb) para árboles grandes.
- Al crear: el `levelOrder` y el `parentPublicId` válido salen de la config de niveles
  ([Location Levels](./location-hierarchy-and-levels.md)). Solo los grupos del **último nivel**
  (`allowsWorkCenters`) podrán alojar work centers.
- Antes de inactivar, `GET /usage` → mostrá los conteos y respetá `canInactivate`.
