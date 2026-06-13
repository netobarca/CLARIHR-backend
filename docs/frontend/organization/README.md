# Organización — Guía de consumo (frontend) · Fase 8

> **Prerequisitos:** [onboarding fases 1–6](../) (compañía activa, permisos) y
> [General Catalogs](../general-catalogs/general-catalogs.md) (Fase 7).
>
> Empezá por las [Convenciones](./_conventions.md) — reglas transversales (auth, compañía activa,
> `If-Match` token fuerte, JSON Patch, paginación, activate/inactivate, errores, rate limits). Cada
> doc de recurso solo documenta lo específico.

---

## Overview

La fase de Organización son **9 controladores** que modelan **dos estructuras independientes** de la
compañía, más un recurso transversal:

- **Estructura organizativa** (jerarquía de mando: direcciones, departamentos, áreas).
- **Estructura de ubicaciones** (jerarquía física: regiones, sedes, centros de trabajo).
- **Centros de costo** (transversal, para imputación contable).

| Recurso (tag) | Doc | Rol |
|---------------|-----|-----|
| Organization Structure Catalogs | [organization-structure-catalogs.md](./organization-structure-catalogs.md) | catálogos `unit-types` + `functional-areas` que tipifican las unidades |
| Organization Units | [organization-units.md](./organization-units.md) | el **árbol organizativo** (CRUD + tree/graph/export/move) |
| Cost Center Types | [cost-center-types.md](./cost-center-types.md) | catálogo de tipos que clasifican los centros de costo |
| Cost Centers | [cost-centers.md](./cost-centers.md) | centros de costo (CRUD + usage/export) |
| Location Hierarchy & Levels | [location-hierarchy-and-levels.md](./location-hierarchy-and-levels.md) | config de niveles de ubicación (cuántos y cuáles) |
| Location Groups | [location-groups.md](./location-groups.md) | los **nodos del árbol de ubicaciones** (CRUD + tree/children/path/usage/move) |
| Work Center Types | [work-center-types.md](./work-center-types.md) | catálogo de tipos de centro de trabajo |
| Work Centers | [work-centers.md](./work-centers.md) | centros de trabajo físicos (CRUD + reassign-group) |

---

## El modelo de datos (leer antes de integrar)

### Estructura organizativa

```
Organization Structure Catalogs            Organization Units (árbol)
┌─────────────────────────┐                ┌───────────────────────────┐
│ unit-types              │◄──obligatorio──│ orgUnitTypePublicId        │
│ functional-areas        │◄──opcional─────│ functionalAreaPublicId     │
└─────────────────────────┘                │ parentPublicId (self) ─────┼─► árbol
                                           │ costCenterCode ────────────┼─► Cost Center (por código)
                                           │ managerEmployeePublicId ───┼─► empleado (Personnel Files)
                                           └───────────────────────────┘
```

- Una **Organization Unit** es un nodo del organigrama. Tiene un **Unit Type** (obligatorio) y
  opcionalmente una **Functional Area** — ambos del catálogo de estructura. Forma árbol con
  `parentPublicId`. Puede imputar a un **Cost Center** (por `costCenterCode`) y tener un manager.
- Para crear unidades **primero deben existir** los unit-types (y functional-areas si se usan).

### Estructura de ubicaciones (4 capas, en orden de dependencia)

```
1. Location Hierarchy (config singleton)   isMultiLevel: ¿1 nivel o varios?
        │  auto-creada con la compañía
        ▼
2. Location Levels      nivel 1 (ej. "País"), nivel 2 ("Región"), nivel 3 ("Sede")…
        │               solo el ÚLTIMO nivel activo puede allowsWorkCenters=true
        ▼
3. Location Groups      nodos concretos en un levelOrder, en árbol (parentPublicId)
        │               ej. "Honduras" (nivel 1) → "Norte" (nivel 2) → "Planta SPS" (nivel 3)
        ▼
4. Work Centers         centro de trabajo físico, en un Group de un nivel que permite work centers
                        + un Work Center Type (define si requiere dirección/geo)
```

- **Location Hierarchy** es una config singleton por compañía (se auto-crea en el onboarding); solo
  alterna `isMultiLevel`.
- **Location Levels** definen las capas (`levelOrder`, `displayName`, flags). Hay un invariante: al
  menos un nivel activo, y solo el último activo puede alojar work centers (`allowsWorkCenters`).
- **Location Groups** son los nodos concretos, cada uno en un `levelOrder`, colgando de un padre del
  nivel inmediatamente superior. Hay un grupo "default" protegido.
- **Work Centers** cuelgan de un Location Group (cuyo nivel permita work centers) y tienen un **Work
  Center Type** que define si exigen `address`/`geoLat`/`geoLong`.

### Cost Centers (transversal)

Independiente de las dos estructuras; lo referencian las Organization Units (por código) y los
Position Slots. Cada centro de costo referencia un **Cost Center Type** (catálogo propio por
compañía, por `costCenterTypePublicId`). Tiene su propio `/usage` para ver dónde se usa antes de
inactivar.

---

## Orden de integración recomendado

El orden importa por las dependencias de FK:

1. **Cost Center Types** — catálogo que clasifica los centros de costo; sin un tipo activo no se
   puede crear un cost center.
2. **Cost Centers** — lo van a referenciar las unidades.
3. **Org Structure Catalogs** (`unit-types`, `functional-areas`) — antes de las unidades.
4. **Organization Units** — el organigrama (usa los catálogos + cost centers).
5. **Location Hierarchy** (ya existe) → **Location Levels** → **Location Groups**.
6. **Work Center Types** → **Work Centers** (cuelgan de groups + types).

Para las pantallas de árbol, `Organization Units` y `Location Groups` exponen `/tree` (y org-units
además `/graph` y exportaciones de diagrama).

---

## Patrón común de cada recurso

Casi todos siguen el mismo CRUD canónico (detallado en [Convenciones](./_conventions.md)):

```
GET    /companies/{companyPublicId}/<recurso>        listar (paginado, q, filtros, includeAllowedActions)
GET    /<recurso>/{publicId}                         detalle (+ ETag para mutar)
POST   /companies/{companyPublicId}/<recurso>        crear (201 + Location + ETag)
PUT    /<recurso>/{publicId}             If-Match    reemplazar campos editables
PATCH  /<recurso>/{publicId}             If-Match    JSON Patch parcial
PATCH  /<recurso>/{publicId}/activate    If-Match    reactivar
PATCH  /<recurso>/{publicId}/inactivate  If-Match    soft-delete (guardado por uso)
```

Más los endpoints especiales por recurso (`/tree`, `/graph`, `/export`, `/move`,
`/reassign-group`, `/usage`, `/children`, `/path`). Cada doc los detalla.

## Próximas fases

Con la estructura organizativa lista, lo que cuelga de ella:
**Personnel Files** (expedientes de empleado — docs en `docs/frontend/personnel-files/`),
**Job Profiles**, y el resto de los módulos de negocio.
