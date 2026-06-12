# Organización — Location Hierarchy & Levels

La **configuración de la jerarquía de ubicaciones**: cuántas capas tiene y cómo se llaman. Es la base
de [Location Groups](./location-groups.md) y [Work Centers](./work-centers.md). Dos controladores que
comparten un invariante config↔niveles.

> Antes de consumir, leé las [Convenciones](./_conventions.md) y el
> [modelo de datos](./README.md#estructura-de-ubicaciones-4-capas-en-orden-de-dependencia). Acá solo
> lo específico.

**Permisos:** `GET` → `Locations.Read` · `PUT/POST/PATCH` → `Locations.Manage` (= `Locations.Admin`
o `iam.administration.manage`).
**Sin rate limits** en esta familia (listas chicas).

---

## A. Location Hierarchy (config singleton)

Una config por compañía, **auto-creada en el onboarding** (no se crea ni se borra desde el FE). Solo
alterna si la jerarquía es de uno o varios niveles.

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/location-hierarchy` | leer la config |
| `PUT` | `/companies/{companyPublicId}/location-hierarchy` | cambiar `isMultiLevel` (`If-Match`) |

### Request body (`PUT`)

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `isMultiLevel` | bool | Sí | `true` = varios niveles; `false` = un solo nivel |

### Response (`LocationHierarchyConfigResponse`)

```json
{
  "publicId": "…", "isMultiLevel": true,
  "defaultGroupCode": "GENERAL", "defaultGroupName": "General",
  "concurrencyToken": "8f3a1c2e-…"
}
```

### Reglas

- Cambiar a **single-level** (`isMultiLevel: false`) exige que haya **exactamente un nivel activo** →
  si no, `409 LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL`.
- No hay `DELETE` ni `POST` (la config ya existe). Solo `GET`/`PUT`.

### Errores

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `LOCATION_HIERARCHY_NOT_FOUND` | 404 | config inexistente (anómalo) |
| `LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL` | 409 | pasar a single-level sin exactamente 1 nivel activo |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

---

## B. Location Levels (las capas)

Las capas de la jerarquía (nivel 1 = "País", nivel 2 = "Región", nivel 3 = "Sede"…). **Lista no
paginada** (son pocas, ordenadas por `levelOrder`).

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/location-levels` | listar (array completo, ordenado) |
| `GET` | `/location-levels/{publicId}` | detalle |
| `POST` | `/companies/{companyPublicId}/location-levels` | crear nivel |
| `PUT` | `/location-levels/{publicId}` | actualizar (`If-Match`) |
| `PATCH` | `/location-levels/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/location-levels/{publicId}/inactivate` | inactivar (`If-Match`) |

> No hay `PATCH` de JSON Patch (se removió por degenerado) ni `DELETE`.

### Request body — Create

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `levelOrder` | int | Sí | > 0; único por compañía |
| `displayName` | string | Sí | máx 100 |
| `isActive` | bool | Sí | |
| `isRequired` | bool | Sí | si `true`, `isActive` debe ser `true` |
| `allowsWorkCenters` | bool | Sí | si `true`: `isActive` debe ser `true` **y** solo el **último** nivel activo puede tenerlo |

**Update (`PUT`)**: `displayName`, `isActive`, `isRequired`, `allowsWorkCenters` (el `levelOrder` es
inmutable).

### Response (`LocationLevelResponse`)

```json
{
  "publicId": "…", "levelOrder": 2, "displayName": "Región",
  "isActive": true, "isRequired": false, "allowsWorkCenters": false,
  "concurrencyToken": "8f3a1c2e-…"
}
```

### Errores

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `LOCATION_LEVEL_ORDER_CONFLICT` | 409 | `levelOrder` duplicado |
| `LOCATION_LEVEL_NOT_FOUND` | 404 | inexistente / otro tenant |
| `LOCATION_HIERARCHY_NOT_FOUND` | 404 | falta la config (prerequisito) |
| `WORK_CENTERS_ALLOWED_ONLY_ON_LAST_LEVEL` | 409 | `allowsWorkCenters=true` en un nivel que no es el último activo, o ya hay otro nivel que lo permite |
| `LAST_ACTIVE_LEVEL_REQUIRED` | 409 | inactivar/desactivar el último nivel activo |
| `LOCATION_LEVEL_REQUIRED_ACTIVE` | 409 | inactivar un nivel con `isRequired=true` |
| `LOCATION_LEVEL_HAS_ACTIVE_GROUPS` | 409 | inactivar un nivel con grupos activos |
| `LOCATION_LEVEL_ALLOWS_WORK_CENTERS_IN_USE` | 409 | quitar `allowsWorkCenters` a un nivel con grupos que alojan work centers |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

### Reglas de negocio (invariantes)

- **Siempre ≥1 nivel activo** (no se puede inactivar el último).
- **`allowsWorkCenters` solo en el último nivel activo**: es el nivel "hoja" donde cuelgan los work
  centers. Solo uno puede tenerlo.
- No se inactiva un nivel **requerido** (`isRequired`) ni uno con **grupos activos** (inactivá los
  grupos primero).
- Validaciones de coherencia (`isRequired`/`allowsWorkCenters` exigen `isActive`) → `400` en el form.

### Relación con Location Groups

Los **Location Groups** se ubican en un `levelOrder`. Un grupo no puede existir en un nivel inactivo,
y un nivel con grupos activos no se puede inactivar. Los work centers solo cuelgan de grupos cuyo
nivel tenga `allowsWorkCenters=true`.

## Guía FE

- Pantalla de configuración de ubicaciones: leé la config (`isMultiLevel`) + los niveles (lista
  completa). Para modo single-level, asegurá que quede exactamente 1 nivel activo antes de cambiar
  el flag.
- Editar niveles es una operación de administración poco frecuente; el flujo normal del usuario es
  trabajar sobre [Location Groups](./location-groups.md).
