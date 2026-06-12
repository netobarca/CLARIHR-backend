# Job Profiles — Dependent Positions

Los **puestos que dependen** de este (a cuántas posiciones de otro perfil supervisa/incluye). Sub‑recurso
CRUD canónico bajo `/job-profiles/{jobProfilePublicId}/dependent-positions`.

> Leé las [Convenciones](./_conventions.md) (patrón de sub‑recurso §8). Acá solo lo específico.
> **Path param del ítem:** `dependentPositionPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.
Lista **paginada**.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `dependentJobProfilePublicId` | uuid | Sí | **otro** Job Profile (no puede ser el mismo perfil) |
| `quantity` | int | Sí | ≥ 0 (cuántas posiciones de ese perfil dependen) |
| `notes` | string | No | máx 1000 |

```json
{ "dependentJobProfilePublicId": "…", "quantity": 3, "notes": "Analistas a cargo" }
```

**Patch** patchables: `/dependentJobProfilePublicId`, `/quantity`, `/notes`.

## Responses

`JobProfileDependentPositionResponse`: `dependentPositionPublicId`, `dependentJobProfilePublicId`,
`dependentJobProfileCode`/`dependentJobProfileTitle` (resueltos), `quantity`, `notes`,
`concurrencyToken`.

## Errores / reglas

- `JOB_PROFILE_DEPENDENT_POSITION_NOT_FOUND` (404), `JOB_PROFILE_NOT_FOUND` (404 si el perfil
  dependiente no existe), + comunes.
- **Detección de ciclos** (`409 JOB_PROFILE_DEPENDENCY_CYCLE`): no se puede (a) apuntar al mismo
  perfil, (b) apuntar al perfil al que este reporta, ni (c) crear un ciclo transitivo en el grafo de
  dependencias. El backend lo valida — manejá el `409` con un mensaje claro.
