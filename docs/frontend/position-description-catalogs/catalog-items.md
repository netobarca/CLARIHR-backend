# Position Description Catalogs — Catalog Items

Los **ítems de catálogo** que alimentan los campos de los [Job Profiles](../job-profiles/README.md)
(Fase 10). Un **único endpoint genérico** discrimina entre **13 catálogos** por el slug `{catalogType}`.

> Leé el [README](./README.md) (patrón común: GET/POST/PATCH, sin PUT/DELETE→405, soft-delete vía
> `/isActive`, `If-Match` en PATCH). Acá solo lo específico. **Path param del ítem:**
> `positionDescriptionCatalogItemPublicId`.

**Permisos:** `GET` → `PositionDescriptionCatalogs.Read` · `POST/PATCH` →
`PositionDescriptionCatalogs.Manage`. Lista **paginada**.

## Endpoints

Base: `/api/v1/companies/{companyPublicId}/position-description-catalogs/{catalogType}/items`

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/.../{catalogType}/items` | listar ítems del catálogo (paginado, `isActive`, `q`≥2) |
| `GET` | `/position-description-catalogs/{catalogType}/items/{itemPublicId}` | detalle (+ `ETag`) |
| `POST` | `/.../{catalogType}/items` | crear ítem |
| `PATCH` | `/position-description-catalogs/{catalogType}/items/{itemPublicId}` | JSON Patch (`If-Match`) |

## `{catalogType}` — whitelist (13 slugs)

Un slug desconocido → `400` (en routing). Cada slug es un catálogo independiente:

| Slug | Alimenta (en Job Profiles, Fase 10) |
|------|--------------------------------------|
| `position-function-types` | clasificaciones (tipo de función) |
| `position-contract-types` | clasificaciones (tipo de contrato) · filtro de Position Slots |
| `strategic-objectives` | shell del perfil (`strategicObjectiveCatalogItemPublicId`) |
| `frequencies` | functions (`frequencyCatalogItemPublicId`) |
| `requirement-types` | requirements (`requirementTypeCatalogItemPublicId`) |
| `requirements` | requirements (ítem de requisito) |
| `general-functions` | funciones generales del perfil |
| `salary-classes` | clase salarial (se conecta con el tabulador salarial) |
| `work-equipments` | shell del perfil (`assignedWorkEquipmentCatalogItemPublicId`) |
| `responsibilities-catalog` | shell del perfil (`responsibilityCatalogItemPublicId`) |
| `benefits-catalog` | benefits del perfil |
| `work-condition-types` | working-conditions (`workConditionTypeCatalogItemPublicId`) |
| `work-conditions` | working-conditions (`catalogItemPublicId`) |

> ⚠️ No confundir con los **Job Catalogs** de la Fase 10 (`job-catalogs/{category}`): son otra familia
> de catálogos. Cada campo del Job Profile usa uno u otro — el
> [`catalog-manifest`](../job-profiles/job-profiles.md) del perfil te dice cuál.

## Request body — Create / Patch

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, formato código, único en el catálogo |
| `name` | string | Sí | máx 150 |
| `description` | string | No | máx 500 |
| `sortOrder` | int | Sí | orden de presentación |

> El `PATCH` además permite `/isActive` (soft-delete: `replace /isActive false`).

```json
{ "code": "DAILY", "name": "Diaria", "sortOrder": 1 }
```

## Responses

`PositionDescriptionCatalogItemResponse`: `publicId`, `catalogType` (enum), `code`, `name`,
`description`, `sortOrder`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`,
`allowedActions?`.

> Pedir un ítem por id con un `{catalogType}` que no coincide con su tipo real → `404`.

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `POSITION_DESCRIPTION_CATALOG_CODE_CONFLICT` | 409 | código duplicado en el catálogo |
| `POSITION_DESCRIPTION_CATALOG_ITEM_NOT_FOUND` | 404 | inexistente / tipo no coincide |
| `POSITION_DESCRIPTION_CATALOG_INVALID_TYPE` | 400 | slug/tipo inválido |
| `POSITION_DESCRIPTION_CATALOG_IN_USE` | 409 | desactivar un ítem referenciado por perfiles |
| `POSITION_DESCRIPTION_CATALOG_RELATED_ITEM_NOT_FOUND` / `SALARY_CLASS_NOT_FOUND` / `REQUIREMENT_TYPE_NOT_FOUND` / `FREQUENCY_NOT_FOUND` / `WORK_CONDITION_TYPE_NOT_FOUND` / `WORK_CONDITION_NOT_FOUND` | 404 | FK interna entre catálogos inexistente |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Guía FE

- Para poblar un dropdown del form de Job Profile, resolvé el `{catalogType}` correcto vía el
  `catalog-manifest` del perfil y listá `?isActive=true`. Guardá el `publicId` del ítem elegido.
- La administración de estos catálogos (alta/edición/baja lógica) es una pantalla de configuración
  aparte; usá el `PATCH` con `/isActive` para activar/desactivar (no hay borrado).
