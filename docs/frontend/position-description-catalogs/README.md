# Position Description Catalogs — Guía de consumo (frontend) · Fase 13

> **Prerequisitos:** [onboarding 1–6](../README.md). Estos catálogos **alimentan** los
> [Job Profiles](../job-profiles/README.md) (Fase 10) y la categorización de las
> [Position Slots](../position-slots/position-slots.md) (Fase 12). Convenciones globales en el
> [índice maestro](../README.md).

---

## Overview

Los **Position Description Catalogs** son los catálogos administrables que tipifican los descriptores
de puesto: los **ítems de catálogo** (13 tipos: frecuencias, tipos de requisito, objetivos
estratégicos, clases salariales, etc.) y la **categorización de posiciones** (clasificaciones →
categorías). Son **3 controladores** (tag "Position Description Catalogs"):

| Recurso | Doc | Rol |
|---------|-----|-----|
| Position Description Catalog Items | [catalog-items.md](./catalog-items.md) | 13 catálogos de ítems (un endpoint genérico por `{catalogType}`) que alimentan los campos de los Job Profiles |
| Position Categories & Classifications | [categories-and-classifications.md](./categories-and-classifications.md) | la jerarquía de categorización de posiciones |

## El patrón común (los 3 recursos)

Todos siguen el **mismo CRUD parcial** (importante — no es el canónico completo):

```
GET    .../<recurso>                     listar (paginado, isActive, q≥2, includeAllowedActions)
GET    .../<recurso>/{id}                detalle (+ ETag)
POST   .../<recurso>                     crear (201 + Location)
PATCH  .../<recurso>/{id}    If-Match    JSON Patch (único verbo de mutación)
```

- **NO hay `PUT`** → un reemplazo total se expresa como JSON Patch con `replace` por campo. `PUT` →
  `405 Method Not Allowed` por diseño.
- **NO hay `DELETE`** → la baja es **soft-delete vía PATCH**: `[{ "op": "replace", "path":
  "/isActive", "value": false }]` (y reactivar con `true`). `DELETE` → `405` por diseño.
- **`PATCH` requiere `If-Match`** con el `concurrencyToken` (token fuerte GUID; faltante → `400`,
  stale → `409 CONCURRENCY_CONFLICT`). El `GET {id}` emite `ETag`.
- **Soft-delete bloqueado si está en uso**: desactivar un ítem/categoría referenciado por perfiles u
  otros catálogos → `409 *_IN_USE`.
- **Permisos**: `GET` → `PositionDescriptionCatalogs.Read`; escrituras →
  `PositionDescriptionCatalogs.Manage`. Sin permiso → `403 POSITION_DESCRIPTION_CATALOG_FORBIDDEN`.
- **Compañía activa**: listados/creación scoped por `companyPublicId` (= tenant activo).
- **Campos comunes**: `code` (máx 50, formato código, único), `name` (máx 150), `description` (máx
  500, opcional), `sortOrder` (orden), `isActive`.
- Sin rate limits específicos.

## El modelo de datos

```
Position Category Classifications        (clasificación de posiciones)
  ├─ positionFunctionType  ─┐
  ├─ positionContractType  ─┼─► catalog-items (tipos)
  └─ orgUnitType ───────────┼─► Organization Structure Catalogs (Fase 8)
        ▲                    │
        │ classificationPublicId
  Position Categories        (categorías; el Job Profile referencia positionCategoryPublicId)
        ▲
        │  (Fase 10)
  Job Profiles / Position Slots

Position Description Catalog Items  (13 tipos)  ─► alimentan campos de Job Profiles:
  frequencies→functions · requirement-types→requirements · strategic-objectives/
  work-equipments/responsibilities→shell · salary-classes→filtro · work-conditions→…
```

- Los **catalog items** (13 tipos) son las opciones de los dropdowns de los Job Profiles (Fase 10).
- Las **classifications** combinan un tipo de función + tipo de contrato + tipo de org unit (única por
  esa tupla); las **categories** cuelgan de una clasificación; el Job Profile referencia una categoría.

## Orden de integración

1. **Catalog Items** (los 13 tipos que necesites) — antes de los forms de Job Profile.
2. **Classifications** → **Categories** — la categorización, antes de asignar categoría a un perfil.

## Próximas fases

Módulos vecinos aún sin documentar: **Salary Tabulator** (las `salary-classes` de acá se conectan con
el tabulador) y el resto de los módulos de negocio.
