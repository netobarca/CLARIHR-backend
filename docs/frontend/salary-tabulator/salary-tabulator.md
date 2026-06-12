# Salary Tabulator — Guía de consumo (frontend) · Fase 14

> **Prerequisitos:** [onboarding 1–6](../README.md). El tabulador define las **bandas salariales** que
> referencian la compensación de los [Job Profiles](../job-profiles/compensations.md) (Fase 10,
> `salaryTabulatorLinePublicId`); sus **clases salariales** son las `salary-classes` de
> [Position Description Catalogs](../position-description-catalogs/catalog-items.md) (Fase 13).
> Convenciones globales en el [índice maestro](../README.md).

---

## Overview

El **Salary Tabulator** es la tabla de **bandas salariales** de la compañía (clase × escala →
base/mín/máx por moneda y vigencia). Son **datos sensibles (PII de salario)** y por eso el módulo usa
un **flujo de aprobación maker-checker**: las líneas **no se editan directamente** — se cambian
proponiendo un *change request* que otra persona aprueba.

Un controlador, **12 endpoints** (dos grupos):

| Grupo | Endpoints |
|-------|-----------|
| **Lines** (read-only) | `GET` lista · `GET {id}` · `GET export` |
| **Change Requests** (workflow) | `GET` lista · `GET {id}` · `GET {id}/impact` · `POST` · `PUT {id}` · `PATCH {id}/submit` · `PATCH {id}/approve` · `PATCH {id}/reject` · `PATCH {id}/cancel` |

### Conceptos clave (leer primero)

- **Maker-checker (separación de funciones)**: tres permisos distintos —
  - `SalaryTabulator.Read` → leer líneas y requests (datos sensibles).
  - `SalaryTabulator.Request` → crear/editar/`submit`/`cancel` requests (el "maker").
  - `SalaryTabulator.Approve` → `approve`/`reject` (el "checker").
- **No se puede auto-aprobar**: quien creó un request **no** puede aprobarlo (ni siquiera un admin).
  La identidad del aprobador sale del JWT, no del body → `403/409
  SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION`.
- **Las líneas son read-only**: el `GET lines` muestra el estado vigente; para cambiarlo, un change
  request aprobado **aplica** las líneas atómicamente.
- **`If-Match` obligatorio** en las 5 mutaciones de request (PUT + submit/approve/reject/cancel):
  token fuerte GUID en el header (faltante → `400`, stale → `409 CONCURRENCY_CONFLICT`).
- **Compañía activa**: los listados/creación scoped por `companyPublicId` (= tenant activo);
  cross-tenant → `404`.

---

## El ciclo de vida de un Change Request

```
POST ─► Draft ──PUT (editar)──┐
          │                   │
          ├─ PATCH /submit ─► Submitted ─┬─ PATCH /approve ─► Approved  (APLICA las líneas)
          │                              ├─ PATCH /reject  ─► Rejected  (no aplica)
          └─ PATCH /cancel ─► Canceled   └─ PATCH /cancel  ─► Canceled
```

| Acción | Desde → Hacia | Permiso | Body |
|--------|---------------|---------|------|
| `POST` crear | → `Draft` | Request | items + vigencia |
| `PUT` editar | `Draft` (solo) | Request | reason + vigencia + items |
| `PATCH /submit` | `Draft` → `Submitted` | Request | — |
| `PATCH /approve` | `Submitted` → `Approved` (+ aplica) | **Approve** | `{ decisionComment }` |
| `PATCH /reject` | `Submitted` → `Rejected` | **Approve** | `{ decisionComment }` |
| `PATCH /cancel` | `Draft` o `Submitted` → `Canceled` | Request | — |

Una acción inválida para el estado actual → `409 SALARY_TABULATOR_REQUEST_STATE_CONFLICT` (ej.
aprobar un `Draft`, o cancelar un `Approved`).

---

## Lines (read-only)

### Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/salary-tabulator/lines` | listar líneas (paginado) |
| `GET` | `/salary-tabulator/lines/{publicId}` | detalle de una línea |
| `GET` | `/companies/{companyPublicId}/salary-tabulator/export` | exportar (xlsx/csv) |

**Permiso:** `SalaryTabulator.Read`. **Filtros:** `salaryClassPublicId`, `salaryScale`, `isActive`,
`q` (≥2), `page`, `pageSize`. **Rate limits:** búsqueda 120/min, export 10/min (PII). Export con
`format` (default `xlsx`; desconocido → `400`), `413` si excede el límite síncrono.

### `SalaryTabulatorLineResponse`

```json
{
  "publicId": "…", "companyPublicId": "…",
  "salaryClassPublicId": "…", "salaryScaleCode": "E1", "currencyCode": "HNL",
  "baseAmount": 25000.00, "minAmount": 22000.00, "maxAmount": 30000.00,
  "effectiveFromUtc": "2026-01-01T00:00:00Z", "effectiveToUtc": null,
  "isActive": true, "version": 3
}
```

> El `publicId` de una línea es lo que la compensación de un Job Profile referencia como
> `salaryTabulatorLinePublicId` (Fase 10).

---

## Change Requests

### Endpoints de lectura

- `GET /companies/{companyPublicId}/salary-tabulator/change-requests` — listar (paginado). Filtros:
  `status`, `requestedBy`, `effectiveFrom`/`effectiveTo`. Rate limit búsqueda.
- `GET /salary-tabulator/change-requests/{publicId}` — detalle con sus items.
- `GET /salary-tabulator/change-requests/{publicId}/impact` — **análisis de impacto** sin aplicar
  nada: qué líneas se crearían/actualizarían/inactivarían y los deltas
  (`totalMonthlyDelta`/`estimatedAnnualDelta`), más alertas de cobertura. **Mostralo antes de
  aprobar.**

### Crear (`POST`) / Editar (`PUT`)

**Create body:**

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `effectiveFromUtc` | date-time | Sí | inicio de vigencia |
| `effectiveToUtc` | date-time | No | fin (≥ from) |
| `items` | array | Sí | ≥1 (`SALARY_TABULATOR_REQUEST_ITEM_REQUIRED` si vacío) |

**Update body** (solo en `Draft`): `reason`, `effectiveFromUtc`, `effectiveToUtc`, `items[]`.

**Cada item (`items[]`):**

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `salaryClassPublicId` | uuid | Sí | clase salarial (`salary-classes` de [Fase 13](../position-description-catalogs/catalog-items.md)) |
| `salaryScaleCode` | string | Sí | escala (ej. `E1`) |
| `currencyCode` | string | Sí | ISO 4217 |
| `changeType` | enum string | Sí | `Create` \| `Update` \| `Inactivate` |
| `proposedBaseAmount` | decimal | cond. | requerido para `Create`/`Update` |
| `proposedMinAmount` | decimal | cond. | mín de la banda |
| `proposedMaxAmount` | decimal | cond. | máx de la banda (mín ≤ base ≤ máx) |
| `notes` | string | No | |

```json
{
  "effectiveFromUtc": "2026-07-01T00:00:00Z",
  "items": [
    { "salaryClassPublicId": "…", "salaryScaleCode": "E1", "currencyCode": "HNL",
      "changeType": "Update", "proposedBaseAmount": 26000, "proposedMinAmount": 23000, "proposedMaxAmount": 31000 }
  ]
}
```

### Acciones de ciclo de vida

`PATCH /submit` y `PATCH /cancel` no llevan body. `PATCH /approve` y `PATCH /reject` llevan
`{ "decisionComment": "…" }`. Todas requieren `If-Match`.

### Responses

`SalaryTabulatorChangeRequestResponse`: `publicId`, `companyPublicId`, `requestNumber`, `reason`,
`status`, `effectiveFromUtc`/`effectiveToUtc`, `requestedByUserPublicId`, `submittedAtUtc`,
`decidedByUserPublicId`, `decidedAtUtc`, `decisionComment`, `items[]`, `concurrencyToken`. Cada item
trae los montos **actuales vs propuestos** (`currentBaseAmount`/`proposedBaseAmount`, etc.) para
mostrar el diff.

---

## Enums (wire, string)

| Enum | Valores |
|------|---------|
| `SalaryTabulatorChangeRequestStatus` | `Draft` · `Submitted` · `Approved` · `Rejected` · `Canceled` |
| `SalaryTabulatorChangeType` | `Create` · `Update` · `Inactivate` |

## Catálogo de errores

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `SALARY_TABULATOR_FORBIDDEN` | 403 | sin el permiso correspondiente (Read/Request/Approve) |
| `SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION` | 403/409 | **auto-aprobación** (el solicitante no puede aprobar/rechazar) |
| `SALARY_TABULATOR_REQUEST_STATE_CONFLICT` | 409 | acción inválida para el estado (ej. aprobar un Draft) |
| `SALARY_TABULATOR_REQUEST_ITEM_REQUIRED` | 422 | request sin items |
| `SALARY_TABULATOR_AMOUNT_RULE_VIOLATION` | 422 | montos inconsistentes (mín/base/máx) |
| `SALARY_TABULATOR_EFFECTIVE_DATES_INVALID` | 422 | `effectiveTo` < `effectiveFrom` |
| `SALARY_TABULATOR_EFFECTIVE_DATE_OVERLAP` | 409 | solapa la vigencia de una línea existente (clase/escala/fecha) |
| `SALARY_TABULATOR_JOB_PROFILE_COVERAGE_CONFLICT` | 409 | aprobar dejaría a un job profile **sin cobertura** salarial |
| `SALARY_TABULATOR_SALARY_CLASS_NOT_FOUND` | 404 | la clase salarial no existe |
| `SALARY_TABULATOR_LINE_NOT_FOUND` / `SALARY_TABULATOR_REQUEST_NOT_FOUND` | 404 | inexistente / otro tenant |
| `SALARY_TABULATOR_EXPORT_FORMAT_INVALID` | 400 | formato de export desconocido |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Guía de implementación del cliente

1. **Tabla de bandas**: `GET lines` (read-only) para mostrar el tabulador vigente; `/export` para
   descargar. Recordá que es PII — gateá la pantalla por `SalaryTabulator.Read`.
2. **Proponer un cambio**: `POST` (Draft) → editar con `PUT` → `PATCH /submit`. Mostrá los botones de
   maker (crear/editar/enviar/cancelar) solo con `SalaryTabulator.Request`.
3. **Aprobar/rechazar**: pantalla del checker (`SalaryTabulator.Approve`); **mostrá el
   `GET /impact`** (qué líneas cambian, deltas, alertas de cobertura) antes de decidir. El sistema
   bloquea al solicitante: no muestres aprobar/rechazar si el usuario es el `requestedByUserPublicId`.
4. **Concurrencia**: guardá el `concurrencyToken` y mandalo en `If-Match` de cada mutación; ante
   `409 CONCURRENCY_CONFLICT` recargá y reintentá.
5. **Diff visual**: usá `current*` vs `proposed*` de cada item para mostrar el antes/después.
6. **Cobertura**: si aprobar daría `SALARY_TABULATOR_JOB_PROFILE_COVERAGE_CONFLICT`, explicá que el
   cambio dejaría perfiles sin banda salarial — hay que ajustar el request.

## Estado de la documentación

Con el Salary Tabulator, la cadena de **descriptores de puesto y compensación** queda cubierta. Ver el
[índice maestro](../README.md) para todas las áreas documentadas.
