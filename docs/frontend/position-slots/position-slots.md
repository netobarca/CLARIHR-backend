# Position Slots — Guía de consumo (frontend) · Fase 12

> **Prerequisitos:** [onboarding 1–6](../README.md), [Organización](../organization/README.md)
> (Fase 8 — work centers) y [Job Profiles](../job-profiles/README.md) (Fase 10 — cada slot instancia
> un perfil). Convenciones globales en el [índice maestro](../README.md).

---

## Overview

Una **Position Slot** es una **posición concreta y ocupable** (una "plaza"/"vacante") que instancia un
[Job Profile](../job-profiles/job-profiles.md): le pone un código, una capacidad de empleados, un
estado, fechas de vigencia, y opcionalmente un work center, un rol y dependencias jerárquicas con
otras posiciones. Del job profile **hereda** la org unit, la categoría, el tipo de contrato y el
centro de costo (no se repiten en el body).

Un controlador, **10 endpoints** (todos bajo `/api/v1`):

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/position-slots` | buscar (paginado, filtros) |
| `GET` | `/position-slots/{publicId}` | detalle (+ `ETag`) |
| `GET` | `/companies/{companyPublicId}/position-slots/graph` | grafo de dependencias (JSON) |
| `GET` | `/companies/{companyPublicId}/position-slots/diagram-export` | exportar diagrama (graphml/json/dot) |
| `GET` | `/companies/{companyPublicId}/position-slots/export` | exportar tabla (xlsx/csv) |
| `POST` | `/companies/{companyPublicId}/position-slots` | crear |
| `PUT` | `/position-slots/{publicId}` | reemplazar campos editables (`If-Match`) |
| `PATCH` | `/position-slots/{publicId}/status` | cambiar estado (`If-Match`) |
| `PATCH` | `/position-slots/{publicId}/dependencies` | cambiar dependencias (`If-Match`) |
| `PATCH` | `/position-slots/{publicId}/occupancy` | cambiar ocupación (`If-Match`) |

**Permisos:** `GET` → `PositionSlots.Read` · escrituras → `PositionSlots.Manage`.

### Conceptos clave (leer primero)

- **Compañía activa**: los endpoints `/companies/{companyPublicId}/...` exigen que el
  `companyPublicId` sea el tenant activo del JWT; cross-tenant → `404`/`403`.
- **Concurrencia fuerte (`If-Match`)**: `GET {id}` emite `ETag`; las **4 mutaciones** (PUT + los 3
  PATCH) requieren `If-Match: "<concurrencyToken>"` (faltante → `400`, stale → `409
  CONCURRENCY_CONFLICT`). El token va en el **header**, no en el body. Cada mutación devuelve el token
  nuevo (body + `ETag`). `POST` no lleva `If-Match` → `201` + `Location` + `ETag`.
- **Mutaciones segmentadas**: el `PUT` cambia los campos "estructurales"; **estado, dependencias y
  ocupación tienen su propio `PATCH`** (no se tocan por PUT) — cada uno con su regla de negocio.
- **Hereda del job profile**: org unit, categoría, contract type y cost center salen del perfil; si
  el perfil no los tiene configurados, ciertas operaciones fallan (ver errores).

---

## Request body — Create

| Campo | Tipo | Req. | Validación / Notas |
|-------|------|------|--------------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `title` | string | No | máx 180 |
| `jobProfilePublicId` | uuid | Sí | el [Job Profile](../job-profiles/job-profiles.md) que instancia |
| `rolePublicId` | uuid | No | rol IAM ([Fase 5](../iam-authorization/iam-authorization.md)) |
| `workCenterPublicId` | uuid | No | [Work Center](../organization/work-centers.md) (Fase 8) |
| `directDependencyPositionSlotPublicId` | uuid | No | posición de la que depende (línea directa) |
| `functionalDependencyPositionSlotPublicId` | uuid | No | dependencia funcional |
| `status` | enum string | Sí | `Vacant` \| `Occupied` \| `Suspended` |
| `maxEmployees` | int | Sí | ≥ 1 (capacidad) |
| `occupiedEmployees` | int | Sí | ≥ 0, ≤ `maxEmployees` |
| `effectiveFromUtc` | date-time | Sí | vigencia desde |
| `effectiveToUtc` | date-time | No | vigencia hasta (≥ `from`) |
| `notes` | string | No | máx 2000 |

```json
{
  "code": "POS-DEV-001", "title": "Desarrollador Senior",
  "jobProfilePublicId": "…", "workCenterPublicId": "…",
  "status": "Vacant", "maxEmployees": 2, "occupiedEmployees": 0,
  "effectiveFromUtc": "2026-06-10T00:00:00Z"
}
```

## Request body — Update (`PUT`)

Reemplaza solo los campos estructurales: `code`, `title`, `jobProfilePublicId`, `rolePublicId`,
`workCenterPublicId`, `maxEmployees`, `effectiveFromUtc`, `effectiveToUtc`, `notes`.

> **No** incluye `status`, `occupiedEmployees` ni las dependencias — esos van por sus PATCH
> dedicados (`/status`, `/occupancy`, `/dependencies`).

## Las 3 acciones segmentadas (`PATCH`)

| Endpoint | Body | Regla |
|----------|------|-------|
| `PATCH /status` | `{ "status": "Vacant"\|"Occupied"\|"Suspended" }` | el dominio reconcilia la ocupación con el estado destino; transición inválida → `409 POSITION_SLOT_STATUS_CONFLICT` |
| `PATCH /dependencies` | `{ directDependencyPositionSlotPublicId?, functionalDependencyPositionSlotPublicId? }` | un cambio que crearía un **ciclo** (directo o funcional) → `409 POSITION_SLOT_DEPENDENCY_CYCLE`; apuntarse a sí mismo → `409 ..._SELF_REFERENCE` |
| `PATCH /occupancy` | `{ occupiedEmployees: int }` | exceder la capacidad → `422 POSITION_SLOT_CAPACITY_RULE_VIOLATION` |

## Buscar (`GET /companies/{companyPublicId}/position-slots`)

**Query params** (los `*Id` viajan como `*PublicId` en el wire):

| Param (wire) | Tipo | Notas |
|--------------|------|-------|
| `status` | enum | `Vacant`/`Occupied`/`Suspended` |
| `jobProfilePublicId` | uuid | filtra por perfil |
| `orgUnitPublicId` | uuid | filtra por org unit (heredada del perfil) |
| `workCenterPublicId` | uuid | filtra por work center |
| `contractTypePublicId` | uuid | filtra por tipo de contrato (heredado) |
| `q` | string | búsqueda libre, **mínimo 2 caracteres**, máx (límite) |
| `page` / `pageSize` | int | 1 / 20; máx 100 |
| `includeAllowedActions` | bool | flags read/manage por ítem |

Respuesta: `PagedResponse<PositionSlotListItemResponse>`.

## Grafo y exportaciones

- **`GET /graph`** — nodos + aristas de dependencias (JSON) para renderizar el organigrama de
  posiciones. Query: `rootId` (subgrafo), `depth`, `includeFunctional` (default `true`). Capado por el
  máximo de nodos → `413` si lo excede. Rate limit de export.
- **`GET /diagram-export`** — descarga el diagrama: `format` (`graphml` default / `json` / `dot`;
  desconocido → `400 POSITION_SLOT_DIAGRAM_FORMAT_INVALID`) + `rootId`/`depth`/`includeFunctional`.
  `413` si es muy grande.
- **`GET /export`** — descarga tabular con los mismos filtros del search; `format` (`xlsx` default /
  `csv`; desconocido → `400 POSITION_SLOT_EXPORT_FORMAT_INVALID`). Excede el límite síncrono → `413`.

**Rate limits:** búsqueda 120/min, las 3 lecturas costosas (graph/diagram-export/export) 10/min, por
usuario+tenant.

## Responses

`PositionSlotResponse` (detalle): `publicId`, `code`, `title`, `jobProfilePublicId` + datos heredados
resueltos (org unit, categoría, contract type, cost center), `rolePublicId`, `workCenterPublicId`,
dependencias, `status`, `maxEmployees`, `occupiedEmployees`, `effectiveFromUtc`/`effectiveToUtc`,
`notes`, `concurrencyToken`, timestamps.

## Enum `PositionSlotStatus` (wire, string)

`Vacant` (vacante) · `Occupied` (ocupada) · `Suspended` (suspendida).

## Catálogo de errores

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `POSITION_SLOT_CODE_CONFLICT` | 409 | código duplicado en la compañía |
| `POSITION_SLOT_NOT_FOUND` | 404 | inexistente / otro tenant |
| `POSITION_SLOT_JOB_PROFILE_NOT_FOUND` | 404 | `jobProfilePublicId` inexistente |
| `POSITION_SLOT_WORK_CENTER_NOT_FOUND` / `POSITION_SLOT_ROLE_NOT_FOUND` | 404 | work center / rol inexistente |
| `POSITION_SLOT_DEPENDENCY_NOT_FOUND` | 404 | la posición dependiente no existe |
| `POSITION_SLOT_JOB_PROFILE_ORG_UNIT_NOT_CONFIGURED` | 422 | el perfil no tiene org unit configurada |
| `POSITION_SLOT_CONTRACT_TYPE_NOT_RESOLVED` | 422 | no se pudo resolver el tipo de contrato del perfil |
| `POSITION_SLOT_COST_CENTER_INVALID` | 422 | centro de costo heredado inválido |
| `POSITION_SLOT_DEPENDENCY_CYCLE` | 409 | la dependencia (directa o funcional) crearía un ciclo |
| `POSITION_SLOT_DEPENDENCY_SELF_REFERENCE` | 409 | una posición no puede depender de sí misma |
| `POSITION_SLOT_STATUS_CONFLICT` | 409 | transición de estado inválida |
| `POSITION_SLOT_STATUS_OCCUPANCY_MISMATCH` / `POSITION_SLOT_SUSPENDED_OCCUPANCY_CONFLICT` | 409 | estado y ocupación incompatibles |
| `POSITION_SLOT_CAPACITY_RULE_VIOLATION` | 422 | `occupiedEmployees` excede `maxEmployees` |
| `POSITION_SLOT_EFFECTIVE_DATES_INVALID` | 422 | `effectiveToUtc` < `effectiveFromUtc` |
| `POSITION_SLOT_EXPORT_FORMAT_INVALID` / `POSITION_SLOT_DIAGRAM_FORMAT_INVALID` | 400 | formato de export desconocido |
| `POSITION_SLOTS_FORBIDDEN` | 403 | sin permiso |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Guía de implementación del cliente

1. **Crear**: elegí el job profile (debe tener org unit configurada), poné code/título/capacidad/estado
   y fechas; el work center, rol y dependencias son opcionales. El response trae los datos heredados
   resueltos para mostrar.
2. **Editar segmentado**: usá `PUT` para code/título/perfil/work-center/capacidad/fechas; y los PATCH
   dedicados para **estado** (`/status`), **ocupación** (`/occupancy`) y **dependencias**
   (`/dependencies`). No mezcles — cada uno tiene su regla de negocio y su `409`/`422`.
3. **Concurrencia**: guardá el `concurrencyToken` del `GET {id}` (header `ETag`) y mandalo en
   `If-Match` de cada mutación; tras cada una, reemplazalo por el nuevo. Ante `409
   CONCURRENCY_CONFLICT`, recargá y reintentá.
4. **Organigrama de posiciones**: `GET /graph` para la vista interactiva (lazy con `rootId`/`depth`),
   o `/diagram-export` para descargar. Manejá `413` ofreciendo acotar el alcance.
5. **Dependencias**: la detección de ciclos (directa y funcional) la hace el backend — manejá el
   `409` con un mensaje claro; no intentes validarla en el FE.

## Próximas fases

Módulos vecinos aún sin documentar: **Position Description Catalogs** (categorías/clasificaciones de
posición), **Salary Tabulator** (tabulador salarial que referencia la compensación de los perfiles) y
el resto de los módulos de negocio.
