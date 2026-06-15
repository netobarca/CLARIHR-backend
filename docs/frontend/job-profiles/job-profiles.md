# Job Profiles — Shell (perfil + ciclo de vida)

El recurso raíz: crear, buscar y editar el perfil de puesto, gestionar su ciclo de vida
(Draft/Published/Archived) y descubrir los catálogos de los forms (`catalog-manifest`).

> Antes de consumir, leé las [Convenciones](./_conventions.md) y el
> [modelo de datos](./README.md#el-modelo-de-datos-leer-antes-de-integrar). Acá solo lo específico.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH` → `JobProfiles.Admin`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/job-profiles/catalog-manifest` | descubrir qué catálogo alimenta cada campo (para armar los forms) |
| `GET` | `/companies/{companyPublicId}/job-profiles` | buscar (paginado, filtros) |
| `GET` | `/job-profiles/{publicId}` | detalle del perfil (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/job-profiles` | crear (nace en `Draft`) |
| `PUT` | `/job-profiles/{publicId}` | reemplazar campos núcleo (`If-Match`) |
| `PATCH` | `/job-profiles/{publicId}` | JSON Patch — **incluye cambiar `/status`** (`If-Match`) |

**Filtros del buscador:** `status` (`Draft`/`Published`/`Archived`), `orgUnitPublicId`,
`salaryClassPublicId`, `q` (máx 150), `page`, `pageSize`, `includeAllowedActions`.

## Request body — Create / Update (campos núcleo)

| Campo | Tipo | Req. | Validación / Notas |
|-------|------|------|--------------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `title` | string | Sí | máx 180 |
| `objective` | string | No\* | máx 4000; \*requerido para publicar |
| `orgUnitPublicId` | uuid | Sí | Organization Unit ([Fase 8](../organization/organization-units.md)) |
| `reportsToJobProfilePublicId` | uuid | No | perfil al que reporta (detección de ciclos) |
| `positionCategoryPublicId` | uuid | No | categoría de posición |
| `strategicObjectiveCatalogItemPublicId` | uuid | No | catálogo (objetivo estratégico) |
| `assignedWorkEquipmentCatalogItemPublicId` | uuid | No | catálogo (equipamiento asignado) |
| `responsibilityCatalogItemPublicId` | uuid | No | catálogo (responsabilidad) |
| `responsibilities` | string | No\* | máx 4000; \*requerido para publicar |
| `decisionScope`, `assignedResources`, `marketSalaryReference`, `valuationNotes` | string | No | máx 4000 c/u |
| `effectiveFromUtc` / `effectiveToUtc` | date-time | No | `from ≤ to` |
| `allowInlineCatalogCreate` | bool | No | crea catálogos faltantes inline; **requiere también `JobCatalogs.Admin`** (si no → `403 JOB_CATALOG_INLINE_CREATE_FORBIDDEN`) |

> El `PUT` **no** cambia el estado (status se excluye a propósito) — usá `PATCH /status`.

**Patch**: todos los campos núcleo de arriba **más `/status`** (`Draft`/`Published`/`Archived`).

## Responses

`GET {id}` devuelve el perfil completo (`JobProfileEntityResponse`: campos núcleo + `status` +
`version` + `concurrencyToken` + referencias resueltas como `orgUnitName`, `reportsToJobProfileCode`/
`Title`). El buscador devuelve `JobProfileListItemResponse` paginado (resumen: `publicId`, `code`,
`title`, `status`, `version`, `orgUnitName`, `isActive`, `allowedActions?`).

## Ciclo de vida (Draft / Published / Archived)

```
POST  ──► Draft  ──PATCH /status=Published──►  Published  ──PATCH /status=Archived──►  Archived
                  (valida prerrequisitos)                                              (read-only)
```

- **Crear** → siempre `Draft`. Editá el perfil y agregá sub‑recursos en `Draft`.
- **Publicar** (`PATCH [{op:replace, path:/status, value:Published}]`): exige `objective`,
  `responsibilities`, **≥1 requirement** y **≥1 function**; si falta algo →
  `422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`. **Avisá en la UI antes de intentar.**
- **Archivar** (`value: Archived`): deja el perfil read-only (toda escritura, incluida la de
  sub‑recursos, falla). Cambiar el estado bumpea `version` y rota el `concurrencyToken`.

## `GET /job-profiles/catalog-manifest`

Devuelve, por sub‑recurso y campo, qué catálogo lo alimenta y desde qué endpoint cargarlo — así el
FE **no hardcodea** las categorías de catálogo:

```json
{
  "subResources": [
    {
      "subResource": "requirements",
      "fields": [
        {
          "fieldName": "requirementTypeCatalogItemPublicId",
          "slug": "requirement-types",
          "family": "REQUIREMENT_TYPE",
          "apiEndpointTemplate": "/api/v1/.../job-catalogs/REQUIREMENT_TYPE...",
          "displayName": "Requirement Type",
          "isActive": true
        }
      ]
    }
  ]
}
```

Cargalo una vez al iniciar; usá `apiEndpointTemplate`/`family` para poblar cada dropdown, y `isActive`
para ocultar catálogos deshabilitados sin redeploy. La respuesta trae `Cache-Control: no-store`
(no la caches del lado HTTP; cacheala en memoria de la sesión si querés).

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `JOB_PROFILE_CODE_CONFLICT` | 409 | código duplicado en la compañía |
| `JOB_PROFILE_NOT_FOUND` | 404 | inexistente |
| `JOB_PROFILE_ORG_UNIT_NOT_FOUND` | 404 | `orgUnitPublicId` inexistente |
| `JOB_PROFILE_REPORTS_TO_NOT_FOUND` | 404 | `reportsToJobProfilePublicId` inexistente |
| `JOB_PROFILE_DEPENDENCY_CYCLE` | 409 | el `reportsTo` crearía un ciclo |
| `JOB_CATALOG_ITEM_NOT_FOUND` / `JOB_CATALOG_ITEM_INACTIVE` | 404/409 | catálogo referenciado inexistente/inactivo |
| `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` | 422 | publicar sin objective/responsibilities/≥1 requirement/≥1 function |
| `JOB_CATALOG_INLINE_CREATE_FORBIDDEN` | 403 | `allowInlineCatalogCreate=true` sin `JobCatalogs.Admin` |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Guía FE

1. Al entrar al módulo, `GET /catalog-manifest` → arma los dropdowns de los forms de perfil y
   sub‑recursos.
2. Crear → `POST` (queda en `Draft`) → editar campos + agregar sub‑recursos.
3. Antes de "Publicar", validá localmente que estén objective, responsibilities, ≥1 requirement y ≥1
   function; deshabilitá el botón si falta algo (el backend igual lo re-valida con `422`).
4. Detección de ciclos (`reportsTo`) la hace el backend — manejá el `409` con mensaje claro.
