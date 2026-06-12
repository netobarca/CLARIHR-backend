# Competency Framework — Competency Conducts

Las **conductas observables**: cada una combina una competencia, un tipo de competencia y un nivel de
comportamiento, con una descripción y una colección de **behaviors** anidados. Son los bloques
reutilizables de la [matriz de competencias](./competency-matrix.md).

> Leé el [README](./README.md) (convenciones de la familia). Acá solo lo específico.

**Permisos:** `GET` → `CompetencyFramework.Read` · `POST/PUT/PATCH` → `CompetencyFramework.Admin`.
Lista **paginada**.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/competency-conducts` | listar (paginado, filtros) |
| `GET` | `/competency-conducts/{publicId}` | detalle (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/competency-conducts` | crear |
| `PUT` | `/competency-conducts/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/competency-conducts/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/competency-conducts/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/competency-conducts/{publicId}/inactivate` | inactivar (`If-Match`) |
| `PUT` | `/competency-conducts/{publicId}/behaviors` | reemplazar los behaviors (`If-Match`) |

**Filtros del listado:** `competencyId`, `competencyTypeId`, `behaviorLevelId`, `isActive`, `q`,
`page`, `pageSize`, `includeAllowedActions`.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación / FK |
|-------|------|------|-----------------|
| `competencyPublicId` | uuid | Sí | Job Catalog categoría `Competency` |
| `competencyTypePublicId` | uuid | Sí | Job Catalog categoría `CompetencyType` |
| `behaviorLevelPublicId` | uuid | Sí | Job Catalog categoría `BehaviorLevel` |
| `description` | string | Sí | máx 1000 |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{ "competencyPublicId": "…", "competencyTypePublicId": "…", "behaviorLevelPublicId": "…",
  "description": "Comunica con claridad a su equipo", "sortOrder": 1 }
```

**Patch** patchables: `/competencyPublicId`, `/competencyTypePublicId`, `/behaviorLevelPublicId`,
`/description`, `/sortOrder` (estado por activate/inactivate; behaviors por su endpoint).

> Los 3 catálogos (`Competency`/`CompetencyType`/`BehaviorLevel`) se administran en
> [Job Catalogs](../job-profiles/job-catalogs.md) (Fase 10).

## `PUT /behaviors` — reemplazar behaviors

Reemplaza el **set completo** de behaviors de la conducta (`If-Match` requerido). Máx **50** behaviors.

| Campo (cada ítem de `behaviors[]`) | Tipo | Req. | Validación |
|-------|------|------|------------|
| `behaviorPublicId` | uuid | Sí | Job Catalog categoría `Behavior` |
| `notes` | string | No | nota del comportamiento |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{ "behaviors": [ { "behaviorPublicId": "…", "notes": "Ejemplo observable", "sortOrder": 1 } ] }
```

Behaviors duplicados → `409 COMPETENCY_CONDUCT_BEHAVIOR_DUPLICATE`.

## Responses

`CompetencyConductResponse`: `publicId`, `companyPublicId`, `competencyPublicId` +
`competencyCode`/`competencyName`, `competencyTypePublicId` + code/name, `behaviorLevelPublicId` +
code/name (todos resueltos), `description`, `sortOrder`, `isActive`, `behaviors[]`
(`{ behaviorPublicId, behaviorCode, behaviorName, notes, sortOrder }`), `concurrencyToken`.

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `COMPETENCY_CONDUCT_DUPLICATE` | 409 | tupla competencia/tipo/nivel/descripción duplicada |
| `COMPETENCY_CONDUCT_NOT_FOUND` | 404 | inexistente / otro tenant |
| `COMPETENCY_CONDUCT_IN_USE` | 409 | inactivar una conducta usada por matrices activas |
| `COMPETENCY_CONDUCT_BEHAVIOR_DUPLICATE` | 409 | behavior duplicado en el replace |
| `COMPETENCY_NOT_FOUND` / `COMPETENCY_TYPE_NOT_FOUND` / `BEHAVIOR_LEVEL_NOT_FOUND` / `BEHAVIOR_NOT_FOUND` | 404 | FK de catálogo inexistente |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- Una conducta es **única por la tupla** (competencia, tipo, nivel, descripción).
- **Inactivar** está bloqueado si la conducta está en uso por una matriz activa.
- Los behaviors se reemplazan en bloque (máx 50), no individualmente.

## Guía FE

- Poblá los 3 dropdowns (competencia/tipo/nivel) desde Job Catalogs; tras crear la conducta, gestioná
  sus behaviors con el `PUT /behaviors` (replace-all).
