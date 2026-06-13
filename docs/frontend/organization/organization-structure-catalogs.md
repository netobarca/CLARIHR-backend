# Organización — Organization Structure Catalogs

Catálogos que **tipifican las unidades organizativas**: `unit-types` (tipo de unidad: Dirección,
Gerencia, Departamento…) y `functional-areas` (área funcional: Finanzas, RRHH, Operaciones…). Las
Organization Units los referencian por FK.

> Antes de consumir, leé las [Convenciones](./_conventions.md). Acá solo lo específico.

**Permisos:** `GET` → `OrgStructureCatalogs.Read` · `POST/PUT/PATCH` → `OrgStructureCatalogs.Manage`.

## Endpoints

Un controlador, **dos sub-catálogos** con el mismo CRUD (base `organization-structure-catalogs`):

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/organization-structure-catalogs/unit-types` | listar tipos de unidad (paginado) |
| `GET` | `/organization-structure-catalogs/unit-types/{publicId}` | detalle |
| `POST` | `/companies/{companyPublicId}/organization-structure-catalogs/unit-types` | crear |
| `PUT` | `/organization-structure-catalogs/unit-types/{publicId}` | actualizar (`If-Match`) |
| `PATCH` | `/organization-structure-catalogs/unit-types/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/organization-structure-catalogs/unit-types/{publicId}/inactivate` | inactivar (`If-Match`) |

Las mismas 6 rutas existen para **`functional-areas`** (reemplazá el segmento). Filtros del listado:
`isActive`, `q` (≥2), `page`, `pageSize`, `includeAllowedActions`.

> Nota: **no hay `PATCH` de JSON Patch** acá (solo `PUT` para editar + activate/inactivate). El
> `If-Match` en `PUT`/`activate`/`inactivate` es relativamente nuevo (alineación canónica) — el FE
> debe mandarlo siempre.

## Request body (Create / Update) — `unit-types` y `functional-areas`

Mismo body para ambos sub-catálogos (`UpsertCatalogItemRequest`):

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, formato código, único por compañía (ver [Convenciones §6](./_conventions.md#6-code-y-name-campos-comunes)) |
| `name` | string | Sí | máx 150 |
| `description` | string | No | máx 500 |
| `sortOrder` | int | Sí | ≥ 0 (orden de presentación en los dropdowns) |

```json
{ "code": "DEPT", "name": "Departamento", "description": "Unidad operativa", "sortOrder": 3 }
```

## Responses

`200`/`201` (detalle y escrituras) — `OrgUnitTypeCatalogItemResponse` / `FunctionalAreaCatalogItemResponse`:

```json
{
  "publicId": "…", "code": "DEPT", "name": "Departamento", "description": "…",
  "sortOrder": 3, "isActive": true,
  "concurrencyToken": "8f3a1c2e-…", "createdAtUtc": "…", "modifiedAtUtc": null
}
```

Create → `201` + `Location` + `ETag`. Listado → paginado (ver Convenciones §8); los items del
listado **no incluyen `description`** (solo detalle) — para mostrarla u editarla, pedí el item por
id.

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `ORG_STRUCTURE_CATALOG_CODE_CONFLICT` | 409 | código duplicado en la compañía |
| `ORG_STRUCTURE_CATALOG_NOT_FOUND` | 404 | inexistente / otro tenant |
| `ORG_STRUCTURE_CATALOG_IN_USE` | 409 | inactivar un ítem referenciado por org-units (los unit-types además por clasificaciones de posición) |
| `ORG_STRUCTURE_CATALOG_FORBIDDEN` | 403 | sin permiso |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **Consumo por Organization Units**: el `publicId` de un unit-type va como `orgUnitTypePublicId`
  (obligatorio) y el de una functional-area como `functionalAreaPublicId` (opcional) al crear/editar
  una unidad. Por eso estos catálogos se cargan **antes** del organigrama.
- **In-use guard**: no se puede inactivar un tipo/área que esté referenciado por unidades activas
  (`409 *_IN_USE`). Con `includeAllowedActions=true`, `allowedActions.canInactivate` lo anticipa
  (cálculo batch, sin N+1).
- `activate` no tiene guardas.

## Guía FE

Cargá ambos catálogos (filtrando `isActive=true`) para poblar los dropdowns del form de
Organization Unit; cacheálos por compañía activa y recargá tras un alta/edición.
