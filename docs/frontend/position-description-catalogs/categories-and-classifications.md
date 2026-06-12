# Position Description Catalogs — Categories & Classifications

La **categorización de posiciones** en dos niveles: las **clasificaciones** (combinación de tipo de
función + contrato + org unit) y las **categorías** (que cuelgan de una clasificación). El
[Job Profile](../job-profiles/job-profiles.md) (Fase 10) referencia una **categoría**
(`positionCategoryPublicId`).

> Leé el [README](./README.md) (patrón común: GET/POST/PATCH, sin PUT/DELETE→405, soft-delete vía
> `/isActive`, `If-Match` en PATCH). Acá solo lo específico.

**Permisos:** `GET` → `PositionDescriptionCatalogs.Read` · `POST/PATCH` →
`PositionDescriptionCatalogs.Manage`. Listas **paginadas**.

---

## A. Position Category Classifications

El nivel superior: una clasificación combina un **tipo de función**, un **tipo de contrato** y un
**tipo de org unit**. **Path param del ítem:** `positionCategoryClassificationPublicId`.

### Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/position-category-classifications` | listar (paginado, filtros) |
| `GET` | `/position-category-classifications/{publicId}` | detalle (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/position-category-classifications` | crear |
| `PATCH` | `/position-category-classifications/{publicId}` | JSON Patch (`If-Match`) |

**Filtros del listado:** `positionFunctionTypePublicId`, `positionContractTypePublicId`,
`orgUnitTypePublicId`, `isActive`, `q`, `page`, `pageSize`, `includeAllowedActions`.

### Request body — Create / Patch

| Campo | Tipo | Req. | Validación / FK |
|-------|------|------|-----------------|
| `code` | string | Sí | máx 50, formato código, único |
| `name` | string | Sí | máx 150 |
| `description` | string | No | máx 500 |
| `positionFunctionTypePublicId` | uuid | Sí | catalog item `position-function-types` |
| `positionContractTypePublicId` | uuid | Sí | catalog item `position-contract-types` |
| `orgUnitTypePublicId` | uuid | Sí | unit-type de [Org Structure Catalogs](../organization/organization-structure-catalogs.md) (Fase 8) |
| `sortOrder` | int | Sí | orden |

> `PATCH` además permite `/isActive`.

### Response

`PositionCategoryClassificationResponse`: `publicId`, `code`, `name`, `description`,
`positionFunctionType{publicId,code,name}`, `positionContractType{…}`, `orgUnitType{…}` (resueltos),
`sortOrder`, `isActive`, `concurrencyToken`, timestamps, `allowedActions?`.

### Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `POSITION_CATEGORY_CLASSIFICATION_CODE_CONFLICT` | 409 | código duplicado |
| `POSITION_CATEGORY_CLASSIFICATION_DUPLICATE_AXES` | 409 | ya existe una clasificación con esa **tupla** (función × contrato × org-unit) |
| `POSITION_CATEGORY_CLASSIFICATION_NOT_FOUND` | 404 | inexistente |
| `POSITION_CATEGORY_CLASSIFICATION_IN_USE` | 409 | desactivar una clasificación con categorías activas |
| `POSITION_DESCRIPTION_CATALOG_RELATED_ITEM_NOT_FOUND` / `ORG_UNIT_TYPE_NOT_FOUND` | 404 | FK (función/contrato/org-unit) inexistente |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

> La **tupla (función, contrato, org-unit) es única** (`DUPLICATE_AXES`): cada combinación se clasifica
> una sola vez.

---

## B. Position Categories

Las categorías concretas, cada una bajo una clasificación. Es lo que el Job Profile referencia.
**Path param del ítem:** `positionCategoryPublicId`.

### Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/position-categories` | listar (paginado, filtros) |
| `GET` | `/position-categories/{publicId}` | detalle (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/position-categories` | crear |
| `PATCH` | `/position-categories/{publicId}` | JSON Patch (`If-Match`) |

**Filtros del listado:** `classificationPublicId`, `isActive`, `q`, `page`, `pageSize`,
`includeAllowedActions`.

### Request body — Create / Patch

| Campo | Tipo | Req. | Validación / FK |
|-------|------|------|-----------------|
| `code` | string | Sí | máx 50, formato código, único |
| `name` | string | Sí | máx 150 |
| `description` | string | No | máx 500 |
| `classificationPublicId` | uuid | Sí | la clasificación padre (§A) |
| `sortOrder` | int | Sí | orden |

> `PATCH` además permite `/isActive`.

### Response

`PositionCategoryResponse`: `publicId`, `code`, `name`, `description`,
`classification{publicId,code,name}` (resuelta), `sortOrder`, `isActive`, `concurrencyToken`,
timestamps, `allowedActions?`.

### Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `POSITION_CATEGORY_CODE_CONFLICT` | 409 | código duplicado |
| `POSITION_CATEGORY_NOT_FOUND` | 404 | inexistente |
| `POSITION_CATEGORY_CLASSIFICATION_NOT_FOUND` | 404 | la `classificationPublicId` no existe |
| `POSITION_CATEGORY_IN_USE` | 409 | desactivar una categoría usada por job profiles |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Guía FE

- Pantalla de configuración: primero las clasificaciones (combinando función/contrato/org-unit), luego
  las categorías bajo cada clasificación.
- En el form de Job Profile (Fase 10), el dropdown de "categoría" se llena con `position-categories`
  activas; el `publicId` elegido va como `positionCategoryPublicId` del perfil.
- Para dar de baja, `PATCH /isActive false`; está bloqueado (`409 *_IN_USE`) si hay dependientes
  activos (categorías bajo una clasificación, o perfiles bajo una categoría).
