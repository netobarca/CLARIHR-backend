# Organización — Work Centers

Los **centros de trabajo físicos** (oficinas, plantas, sucursales). Cada uno cuelga de un
[Location Group](./location-groups.md) (del último nivel, que permite work centers) y tiene un
[Work Center Type](./work-center-types.md) que define qué datos exige.

> Antes de consumir, leé las [Convenciones](./_conventions.md) y el
> [modelo de datos](./README.md#estructura-de-ubicaciones-4-capas-en-orden-de-dependencia). Acá solo
> lo específico.

**Permisos:** `GET` → `Locations.Read` · `POST/PUT/PATCH` → `Locations.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/work-centers` | listar (paginado, filtros) |
| `GET` | `/work-centers/{publicId}` | detalle (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/work-centers` | crear |
| `PUT` | `/work-centers/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/work-centers/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/work-centers/{publicId}/reassign-group` | mover a otro Location Group (`If-Match`) |
| `PATCH` | `/work-centers/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/work-centers/{publicId}/inactivate` | inactivar (`If-Match`) |

**Filtros del listado:** `groupPublicId`, `typePublicId`, `isActive`, `q` (≥2), `page`, `pageSize`,
`includeAllowedActions`.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `name` | string | Sí | máx 150 |
| `workCenterTypePublicId` | uuid | Sí | tipo activo ([Work Center Types](./work-center-types.md)) |
| `locationGroupPublicId` | uuid | Sí | grupo activo de un nivel que **permita work centers** |
| `address` | string | cond. | máx 300; **requerido si** el tipo tiene `requiresAddress` |
| `geoLat` | number | cond. | -90..90; **requerido si** el tipo tiene `requiresGeo` |
| `geoLong` | number | cond. | -180..180; requerido con `requiresGeo` |
| `phone` | string | No | máx 50 |
| `email` | string | No | formato email |
| `notes` | string | No | máx 1000 |

```json
{
  "code": "WC-SPS-01", "name": "Planta SPS",
  "workCenterTypePublicId": "…", "locationGroupPublicId": "…",
  "address": "Blvd. del Norte km 4", "geoLat": 15.50, "geoLong": -88.03
}
```

**Patch** patchables: `/code`, `/name`, `/address`, `/geoLat`, `/geoLong`, `/phone`, `/email`,
`/notes` (el **tipo y el grupo NO son patchables** — tipo/varios campos por `PUT`, grupo por
`/reassign-group`; estado por activate/inactivate).

## Responses

`WorkCenterResponse`: `publicId`, `code`, `name`, `workCenterTypePublicId` +
`workCenterTypeCode`/`workCenterTypeName`, `locationGroupPublicId` +
`locationGroupCode`/`locationGroupName`/`locationGroupLevelOrder`, `address`, `geoLat`, `geoLong`,
`phone`, `email`, `notes`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`,
`allowedActions?`. (Trae el code/name del tipo y del grupo denormalizados para mostrar sin joins.)

## `PATCH /reassign-group`

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `locationGroupPublicId` | uuid | Sí | grupo destino: activo y de un nivel que permita work centers |

Cambia solo el grupo (preserva el tipo). El nivel del grupo destino debe permitir work centers, si no
`409 LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`.

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `WORK_CENTER_CODE_CONFLICT` | 409 | código duplicado |
| `WORK_CENTER_NOT_FOUND` | 404 | inexistente / otro tenant |
| `WORK_CENTER_TYPE_NOT_FOUND` / `LOCATION_GROUP_NOT_FOUND` | 404 | tipo o grupo referenciado inexistente |
| `WORK_CENTER_TYPE_INACTIVE` / `LOCATION_GROUP_INACTIVE` | 409 | tipo o grupo inactivo |
| `LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER` | 409 | el nivel del grupo no aloja work centers |
| `WORK_CENTER_ADDRESS_REQUIRED` | 422 | el tipo exige `address` y falta |
| `WORK_CENTER_GEO_REQUIRED` | 422 | el tipo exige geo y falta `geoLat`/`geoLong` |
| `WORK_CENTER_INVALID_COORDINATES` | 422 | lat/long fuera de rango |
| `WORK_CENTER_HAS_ACTIVE_DEPENDENCIES` | 409 | inactivar con dependencias activas |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **El tipo gobierna los campos requeridos**: `requiresAddress`/`requiresGeo` del
  [Work Center Type](./work-center-types.md) determinan si `address` y `geoLat`/`geoLong` son
  obligatorios (`422` si faltan). Leé los flags del tipo elegido para condicionar el form.
- **El grupo debe permitir work centers**: solo los grupos del **último nivel activo**
  (`allowsWorkCenters`) pueden alojarlos. Para cambiar de grupo, `/reassign-group` (no `PUT`).
- **Inactivar** está bloqueado con dependencias activas (`409 WORK_CENTER_HAS_ACTIVE_DEPENDENCIES`).

## Guía FE

- Form de work center: 1) elegí el **tipo** → según sus flags mostrá/requerí dirección y/o
  coordenadas; 2) elegí el **grupo** entre los del último nivel (filtrá `location-groups` por el
  `levelOrder` que permite work centers). El response trae el code/name de tipo y grupo
  denormalizados para listar sin llamadas extra.
- Mover un centro de ubicación → `/reassign-group`.
