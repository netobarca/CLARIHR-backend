# Job Profiles — Requirements

Los **requisitos** del puesto: educación, experiencia, conocimiento, certificación u otros. Sub‑recurso
CRUD canónico bajo `/job-profiles/{jobProfilePublicId}/requirements`.

> Leé las [Convenciones](./_conventions.md) (patrón de sub‑recurso §8). Acá solo lo específico.
> **Path param del ítem:** `requirementPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.
Lista **paginada**.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `requirementType` | enum string | Sí | `Education` \| `Experience` \| `Knowledge` \| `Certification` \| `Other` |
| `requirementTypeCatalogItemPublicId` | uuid | No | catálogo del tipo de requisito (ver `catalog-manifest`) |
| `catalogItemPublicId` | uuid | No | ítem de Job Catalog (ej. nivel educativo, área de conocimiento) |
| `description` | string | Sí | no vacío, máx 1000 |
| `sortOrder` | int | Sí | ≥ 0 |

> `catalogCode`/`catalogName` aparecen en la respuesta (resueltos) pero **no** se envían al crear.

```json
{ "requirementType": "Education", "catalogItemPublicId": "…", "description": "Ingeniería Industrial", "sortOrder": 1 }
```

**Patch** patchables: `/requirementType`, `/requirementTypeCatalogItemPublicId`, `/catalogItemPublicId`,
`/description`, `/sortOrder`.

## Autocomplete de `description` (Internal Catalogs)

Para `Education`/`Knowledge`/`Certification`, la `description` se respalda en el **diccionario global**
de [Internal Catalogs](./job-catalogs.md#b-internal-catalogs-diccionario-global-de-valores-freetext):
mostrá un autocomplete (`GET .../internal-catalogs/{catalogKey}/values?q=`) y permití crear valores
nuevos (`POST`, con dedup por similitud). Para `Experience`/`Other` es texto libre sin catálogo. El
mapeo tipo→`catalogKey`/`renderType` lo da `GET /internal-catalogs?context=job-profile.requirements`.

## Responses

`JobProfileRequirementResponse`: `requirementPublicId`, `requirementType`,
`requirementTypeCatalogItemPublicId`, `catalogItemPublicId`, `catalogCode`/`catalogName` (resueltos),
`description`, `sortOrder`, `concurrencyToken`.

## Errores / reglas

- `JOB_PROFILE_REQUIREMENT_NOT_FOUND` (404), `JOB_CATALOG_ITEM_NOT_FOUND`/`RELATED_CATALOG_ITEM_NOT_FOUND`
  (404), `JOB_CATALOG_ITEM_INACTIVE` (409), + comunes.
- **≥1 requirement es prerrequisito para publicar** el perfil.
