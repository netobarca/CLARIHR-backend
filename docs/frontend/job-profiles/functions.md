# Job Profiles — Functions

Las **funciones** del puesto (responsabilidades concretas), clasificadas como generales o
específicas. Sub‑recurso CRUD canónico bajo `/job-profiles/{jobProfilePublicId}/functions`.

> Leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, el patrón de
> sub‑recurso §8, errores). Acá solo lo específico. **Path param del ítem:** `functionPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.
Lista **paginada**.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `functionType` | enum string | Sí | `General` \| `Specific` |
| `frequencyCatalogItemPublicId` | uuid | No | catálogo de frecuencia (ver `catalog-manifest`) |
| `description` | string | Sí | no vacío, máx 2000 |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{ "functionType": "Specific", "description": "Elaborar el reporte mensual de ventas", "sortOrder": 1 }
```

**Patch** patchables: `/functionType`, `/frequencyCatalogItemPublicId`, `/description`, `/sortOrder`.

## Responses

`JobProfileFunctionResponse`: `functionPublicId`, `functionType`, `frequencyCatalogItemPublicId`,
`description`, `sortOrder`, `concurrencyToken`.

## Errores / reglas

- `JOB_PROFILE_FUNCTION_NOT_FOUND` (404), `JOB_CATALOG_ITEM_NOT_FOUND` (404) si la frecuencia no
  existe, + los comunes (`CONCURRENCY_CONFLICT`, `JOB_PROFILE_NOT_FOUND`).
- **≥1 function es prerrequisito para publicar** el perfil (ver [shell](./job-profiles.md)).
