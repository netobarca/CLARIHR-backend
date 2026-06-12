# Job Profiles — Competencies

Las **competencias** esperadas para el puesto. Sub‑recurso CRUD canónico bajo
`/job-profiles/{jobProfilePublicId}/competencies`.

> Leé las [Convenciones](./_conventions.md) (patrón de sub‑recurso §8). Acá solo lo específico.
> **Path param del ítem:** `competencyPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.
Lista **paginada**.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `catalogItemPublicId` | uuid | No\* | ítem de Job Catalog (categoría `Competency`); \*si no se manda, `name` es obligatorio |
| `name` | string | No\* | máx 300; requerido si no hay `catalogItemPublicId` |
| `expectedLevel` | string | No | máx 150 (nivel esperado, ej. "Avanzado") |
| `notes` | string | No | máx 1000 |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{ "catalogItemPublicId": "…", "expectedLevel": "Avanzado", "sortOrder": 1 }
```

**Patch** patchables: `/catalogItemPublicId`, `/name`, `/expectedLevel`, `/notes`, `/sortOrder`.

## Responses

`JobProfileCompetencyResponse`: `competencyPublicId`, `catalogItemPublicId`, `name`, `expectedLevel`,
`notes`, `sortOrder`, `concurrencyToken`.

## Errores / reglas

- `JOB_PROFILE_COMPETENCY_NOT_FOUND` (404), `JOB_PROFILE_COMPETENCY_NAME_REQUIRED` (400 si falta
  `name` sin catálogo), `JOB_CATALOG_ITEM_NOT_FOUND` (404), + comunes.
- Estas son competencias "legacy" simples (referencian el Job Catalog). La **matriz de competencias**
  avanzada (occupational pyramid, niveles conductuales) es del módulo **Competency Framework**, no de
  este sub‑recurso.
