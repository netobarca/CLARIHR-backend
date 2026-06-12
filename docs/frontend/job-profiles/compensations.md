# Job Profiles — Compensations

La **compensación** del puesto, ligada a una línea de **tabulador salarial**. Sub‑recurso especial:
**uno por perfil** y **lista no paginada**. Bajo `/job-profiles/{jobProfilePublicId}/compensations`.

> Leé las [Convenciones](./_conventions.md) (patrón de sub‑recurso §8). Acá solo lo específico.
> **Path param del ítem:** `compensationPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.

## Endpoints

Los 6 verbos canónicos, con dos particularidades:

- **`GET` lista** devuelve el **array completo** (NO paginado) — un perfil tiene a lo sumo una
  compensación.
- **`POST`** falla con `409 JOB_PROFILE_COMPENSATION_ALREADY_EXISTS` si el perfil ya tiene una
  (uno‑por‑perfil). Para cambiarla, usá `PUT`/`PATCH` sobre la existente.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `salaryTabulatorLinePublicId` | uuid | Sí | línea de tabulador salarial **activa** |
| `notes` | string | No | máx 1000 |

```json
{ "salaryTabulatorLinePublicId": "…", "notes": "Banda salarial nivel 4" }
```

**Patch** patchables: `/salaryTabulatorLinePublicId`, `/notes`.

## Responses

`JobProfileCompensationItemResponse`: `compensationPublicId`, `salaryTabulatorLinePublicId`, `notes`,
`concurrencyToken`, **+ datos resueltos de la línea de tabulador**: `salaryClassCode`,
`salaryScaleCode`, `currencyCode`, `baseAmount`, `minAmount`, `maxAmount`, `effectiveFromUtc`,
`effectiveToUtc`. (Así el FE muestra el rango salarial sin consultar el tabulador aparte.)

## Errores / reglas

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `JOB_PROFILE_COMPENSATION_ALREADY_EXISTS` | 409 | crear una segunda compensación |
| `JOB_PROFILE_COMPENSATION_SALARY_TABULATOR_LINE_NOT_FOUND` | 404 | la línea no existe |
| `JOB_PROFILE_COMPENSATION_SALARY_TABULATOR_LINE_INACTIVE` | 409 | la línea está inactiva |
| `JOB_PROFILE_COMPENSATION_NOT_FOUND` | 404 | inexistente |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **Uno por perfil**: si ya existe, editá (no crees otra). El FE debería mostrar "editar" en vez de
  "agregar" cuando `GET` devuelve un ítem.
- La **línea de tabulador** debe estar activa; su rango (`min`/`max`/`base` + fechas de vigencia)
  define la banda salarial del puesto. Pertenece al módulo de **Salary Tabulator** (compensación).

## Guía FE

- Cargá el tabulador salarial para que el usuario elija la línea (`salaryTabulatorLinePublicId`); al
  guardar, el response trae los montos/moneda resueltos para mostrar la banda directamente.
- Tratá la pantalla como "0 o 1": si `GET` trae un ítem, mostrá editar/quitar; si está vacío,
  mostrá agregar.
