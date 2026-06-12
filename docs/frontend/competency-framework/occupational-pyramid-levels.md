# Competency Framework — Occupational Pyramid Levels

Catálogo de **niveles de la pirámide ocupacional** (ej. Operativo, Táctico, Estratégico). Es la
dimensión jerárquica de la [matriz de competencias](./competency-matrix.md).

> Leé el [README](./README.md) (convenciones de la familia). Acá solo lo específico.

**Permisos:** `GET` → `CompetencyFramework.Read` · `POST/PUT/PATCH` → `CompetencyFramework.Admin`.
Lista **paginada**.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/occupational-pyramid-levels` | listar (paginado, `isActive`, `q`) |
| `GET` | `/occupational-pyramid-levels/{publicId}` | detalle (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/occupational-pyramid-levels` | crear |
| `PUT` | `/occupational-pyramid-levels/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/occupational-pyramid-levels/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/occupational-pyramid-levels/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/occupational-pyramid-levels/{publicId}/inactivate` | inactivar (`If-Match`) |

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, único por compañía |
| `name` | string | Sí | máx 120 |
| `levelOrder` | int | Sí | orden en la pirámide (único por compañía) |
| `description` | string | No | máx 500 |

```json
{ "code": "OPER", "name": "Operativo", "levelOrder": 1, "description": "Ejecución de tareas" }
```

**Patch** patchables: `/code`, `/name`, `/levelOrder`, `/description` (estado por activate/inactivate).

## Responses

`OccupationalPyramidLevelResponse`: `publicId`, `companyPublicId`, `code`, `name`, `levelOrder`,
`description`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`.

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `OCCUPATIONAL_PYRAMID_LEVEL_CODE_CONFLICT` | 409 | código duplicado |
| `OCCUPATIONAL_PYRAMID_LEVEL_ORDER_CONFLICT` | 409 | `levelOrder` duplicado |
| `OCCUPATIONAL_PYRAMID_LEVEL_NOT_FOUND` | 404 | inexistente / otro tenant |
| `OCCUPATIONAL_PYRAMID_LEVEL_IN_USE` | 409 | inactivar un nivel usado por conductas/matrices activas |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- `code` y `levelOrder` son únicos por compañía.
- **Inactivar** está bloqueado si el nivel está en uso (`409 *_IN_USE`).
- Los niveles son la columna "nivel" que usa la matriz; definílos antes de armar matrices.
