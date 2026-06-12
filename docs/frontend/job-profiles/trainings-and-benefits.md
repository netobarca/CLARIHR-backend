# Job Profiles — Trainings & Benefits

Dos sub‑recursos con **estructura idéntica**: las **capacitaciones** (trainings) y los **beneficios**
(benefits) del puesto. Solo cambian la ruta y la categoría de catálogo que referencian.

> Leé las [Convenciones](./_conventions.md) (patrón de sub‑recurso §8). Acá solo lo específico.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.
Listas **paginadas**.

## Rutas

| Sub‑recurso | Base | Path param del ítem | Categoría de catálogo |
|-------------|------|---------------------|------------------------|
| Trainings | `/job-profiles/{jobProfilePublicId}/trainings` | `trainingPublicId` | `Training` |
| Benefits | `/job-profiles/{jobProfilePublicId}/benefits` | `benefitPublicId` | `BenefitType` |

Ambas con los 6 verbos canónicos (GET list/by-id, POST, PUT, PATCH, DELETE).

## Request body — Create / Update (idéntico en ambos)

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `catalogItemPublicId` | uuid | No | ítem de Job Catalog (categoría `Training` o `BenefitType`) |
| `name` | string | No | máx 300 (texto libre si no se usa catálogo) |
| `notes` | string | No | máx 1000 |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{ "catalogItemPublicId": "…", "notes": "Renovación anual", "sortOrder": 1 }
```

**Patch** patchables: `/catalogItemPublicId`, `/name`, `/notes`, `/sortOrder`.

## Responses

`JobProfileTrainingResponse` / `JobProfileBenefitResponse`: `<recurso>PublicId`, `catalogItemPublicId`,
`name`, `notes`, `sortOrder`, `concurrencyToken`.

## Errores / reglas

- `JOB_PROFILE_TRAINING_NOT_FOUND` / `JOB_PROFILE_BENEFIT_NOT_FOUND` (404),
  `JOB_CATALOG_ITEM_NOT_FOUND` (404), `JOB_CATALOG_ITEM_INACTIVE` (409), + comunes.
- `name` o `catalogItemPublicId`: usá el catálogo para estandarizar, o `name` libre para casos
  puntuales.
