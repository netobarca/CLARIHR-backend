# Job Profiles — Working Conditions

Las **condiciones de trabajo** del puesto (ej. trabajo de campo, turnos rotativos, exposición a
riesgos). Sub‑recurso CRUD canónico bajo `/job-profiles/{jobProfilePublicId}/working-conditions`.

> Leé las [Convenciones](./_conventions.md) (patrón de sub‑recurso §8). Acá solo lo específico.
> **Path param del ítem:** `workingConditionPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.
Lista **paginada**.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `workConditionTypeCatalogItemPublicId` | uuid | No | catálogo del tipo de condición |
| `catalogItemPublicId` | uuid | No\* | catálogo de la condición (valor) |
| `name` | string | No\* | máx 300; \*si no se manda `catalogItemPublicId`, `name` es obligatorio (se autocompleta del catálogo si se usa) |
| `notes` | string | No | máx 1000 |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{ "workConditionTypeCatalogItemPublicId": "…", "catalogItemPublicId": "…", "notes": "8h de campo", "sortOrder": 1 }
```

**Patch** patchables: `/workConditionTypeCatalogItemPublicId`, `/catalogItemPublicId`, `/name`,
`/notes`, `/sortOrder`.

## Responses

`JobProfileWorkingConditionResponse`: `workingConditionPublicId`, `workConditionTypeCatalogItemPublicId`,
`catalogItemPublicId`, `name` (auto desde catálogo si no se dio), `notes`, `sortOrder`,
`concurrencyToken`.

## Errores / reglas

- `JOB_PROFILE_WORKING_CONDITION_NOT_FOUND` (404),
  `JOB_PROFILE_WORKING_CONDITION_NAME_REQUIRED` (422 si faltan tanto `catalogItemPublicId` como `name`),
  `JOB_CATALOG_ITEM_NOT_FOUND` (404), + comunes.
