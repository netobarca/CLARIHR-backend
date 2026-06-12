# Report Export Jobs — Guía de consumo (frontend) · Fase 17

> **Prerequisitos:** [onboarding 1–6](../README.md). Es el camino **asíncrono** de exportación, en
> contraste con los `/export` **síncronos** de cada módulo (Cost Centers, Org Units, Position Slots,
> Salary Tabulator, Personnel Files…). Convenciones globales en el [índice maestro](../README.md).

---

## Overview

Una **cola de exportaciones asíncronas**: para reportes grandes (que excederían el límite síncrono
`413` de los `/export` directos), se **encola un job** que un worker procesa en segundo plano y deja
un artefacto descargable. El FE crea el job, **consulta su estado** y descarga cuando está listo.

Un controlador, **5 endpoints** (tag "Reports"):

| Método | Ruta | Para qué |
|--------|------|----------|
| `POST` | `/api/v1/companies/{companyPublicId}/report-export-jobs` | encolar un job (`202`) |
| `GET` | `/api/v1/companies/{companyPublicId}/report-export-jobs` | listar jobs (paginado, filtro `status`) |
| `GET` | `/api/v1/report-export-jobs/{jobId}` | estado de un job (poll) |
| `GET` | `/api/v1/report-export-jobs/{jobId}/download` | descargar el artefacto |
| `PATCH` | `/api/v1/report-export-jobs/{jobId}/cancel` | cancelar (`If-Match`) |

### Conceptos clave (leer primero)

- **Autorización por recurso**: cada job referencia un `resourceKey` (qué se exporta) y exige el
  **permiso de lectura de ESE recurso**. El `Search` solo lista jobs de recursos que el usuario puede
  leer (no filtra metadata de recursos que no podés ver); `GetById`/`download`/`cancel` están gateados
  por el mismo permiso → `403`/`404`.
- **PII protegida**: campos sensibles (ej. compensación) los **estampa el servidor según permisos**;
  no se pueden forzar por `parameters` — el cliente no puede pedir más de lo que su rol permite.
- **Sincrónico vs asíncrono**: si esperás pocos registros, el `/export` directo del módulo es más
  simple (descarga inmediata). Para volúmenes grandes, usá esta cola.

---

## El flujo (crear → consultar → descargar)

```
1. POST  /companies/{id}/report-export-jobs {resourceKey, format, parameters}  → 202 { jobId, status: "Queued" }
2. (poll) GET /report-export-jobs/{jobId}  hasta status == "Succeeded"  (o "Failed")
3. GET   /report-export-jobs/{jobId}/download  → stream del archivo
   (cancelable mientras Queued/Running vía PATCH /cancel)
```

### 1. Encolar (`POST`)

**Request body** (`CreateReportExportJobRequest`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `resourceKey` | string | Sí | uno de la whitelist (ver abajo); requiere el permiso de lectura de ese recurso |
| `format` | string | Sí | formato del archivo (`xlsx`/`csv`; `pdf` para ciertos recursos); no soportado → `400 REPORT_FORMAT_NOT_SUPPORTED` |
| `parameters` | object (JSON) | No | filtros del export, **específicos del recurso** (los mismos del `/export` síncrono de ese módulo); omitir = `{}` |

```json
{
  "resourceKey": "POSITION_SLOTS",
  "format": "xlsx",
  "parameters": { "status": "Vacant", "jobProfilePublicId": "…" }
}
```

**Response `202 Accepted`** + header `Location` → `ReportExportJobResponse` con `status: "Queued"`.
`413 REPORT_EXPORT_TOO_LARGE`/`REPORT_EXPORT_LIMIT_EXCEEDED` si excede límites; `503
REPORT_EXPORT_STORAGE_NOT_CONFIGURED` si falta config de storage.

### 2. Consultar estado (`GET {jobId}`) — poll

Devuelve `ReportExportJobResponse`. Hacé polling (con backoff) hasta `Succeeded` o un estado
terminal. Trae también el `concurrencyToken` para cancelar.

### 3. Descargar (`GET {jobId}/download`)

Hace stream del artefacto generado (`200` + el archivo). `409 REPORT_EXPORT_JOB_NOT_READY` si el job
no está `Succeeded`; `410 REPORT_EXPORT_JOB_EXPIRED` si el artefacto ya expiró (los artefactos tienen
vida limitada — ver `expiresUtc`). Rate limit 10/min.

### Cancelar (`PATCH {jobId}/cancel`)

Cancela un job `Queued` o `Running`. Requiere `If-Match: "<concurrencyToken>"` (faltante → `400`,
stale → `409 REPORT_CONCURRENCY_CONFLICT`). Un job en estado terminal → `409
REPORT_EXPORT_JOB_NOT_CANCELLABLE`.

---

## `resourceKey` — whitelist

| `resourceKey` | Qué exporta |
|---------------|-------------|
| `PERSONNEL_FILES` | expedientes de personal |
| `PERSONNEL_FILE_PERSONNEL_ACTIONS` | acciones de personal |
| `PERSONNEL_FILE_PAYROLL_TRANSACTIONS` | transacciones de nómina |
| `ORG_UNITS` | unidades organizativas |
| `POSITION_SLOTS` | posiciones |
| `SALARY_TABULATOR` | tabulador salarial (PII) |
| `COST_CENTERS` | centros de costo |
| `LEGAL_REPRESENTATIVES` | representantes legales |
| `JOB_PROFILE_COMPETENCY_MATRIX` | matriz de competencias de un perfil |
| `JOB_PROFILE_PDF` | descriptor de puesto en PDF |

Cada uno exige su permiso de lectura; los `parameters` son los mismos filtros que el `/export`
síncrono del módulo correspondiente.

## Responses — `ReportExportJobResponse`

```json
{
  "publicId": "…",
  "resourceKey": "POSITION_SLOTS",
  "format": "xlsx",
  "status": "Succeeded",
  "queuedUtc": "…", "startedUtc": "…", "completedUtc": "…", "expiresUtc": "…",
  "attempts": 1,
  "rowCount": 1240,
  "fileName": "position-slots-2026-06-10.xlsx",
  "sizeBytes": 84211,
  "lastErrorCode": null,
  "concurrencyToken": "…"
}
```

`status` (`ReportExportJobStatus`): `Queued` · `Running` · `Succeeded` · `Failed` · `Cancelled` ·
`Expired`. `lastErrorCode` explica un `Failed`. `expiresUtc` = cuándo deja de poder descargarse.

## Catálogo de errores

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `REPORT_FORBIDDEN` | 403 | sin permiso de lectura del `resourceKey` |
| `REPORT_FORMAT_NOT_SUPPORTED` | 400 | `format` no válido para el recurso |
| `REPORT_EXPORT_TOO_LARGE` / `REPORT_EXPORT_LIMIT_EXCEEDED` | 413 | excede límites de exportación |
| `REPORT_EXPORT_STORAGE_NOT_CONFIGURED` | 503 | storage no configurado |
| `REPORT_EXPORT_JOB_NOT_FOUND` | 404 | job inexistente / recurso no legible |
| `REPORT_EXPORT_JOB_NOT_READY` | 409 | descargar un job no `Succeeded` |
| `REPORT_EXPORT_JOB_EXPIRED` | 410 | el artefacto ya expiró |
| `REPORT_EXPORT_JOB_NOT_CANCELLABLE` | 409 | cancelar un job terminal |
| `REPORT_CONCURRENCY_CONFLICT` | 409 | cancel con `If-Match` stale |

## Guía de implementación del cliente

1. **Elegí síncrono o asíncrono**: para descargas chicas usá el `/export` directo del módulo
   (inmediato); para grandes, encolá un job acá.
2. **Crear → poll → descargar**: tras el `202`, hacé polling de `GET {jobId}` con backoff (ej. cada
   2–5 s) hasta `Succeeded`; mostrá progreso/estado. Al completar, `GET {jobId}/download`.
3. **Parámetros**: reutilizá exactamente los filtros del `/export` síncrono del recurso (mismo
   contrato). No metas flags de PII (compensación) — los ignora/estampa el servidor.
4. **Expiración**: respetá `expiresUtc`; si el usuario tarda, el artefacto puede dar `410` →
   re-encolá el job.
5. **Cancelar**: ofrecé cancelar mientras `Queued`/`Running` (con el `concurrencyToken` en `If-Match`).
6. **Historial**: el `Search` muestra los jobs del usuario por recurso legible — útil como bandeja de
   "Mis exportaciones" con estado y enlace de descarga.

## Estado de la documentación

Report Export Jobs es un módulo de soporte transversal. Ver el [índice maestro](../README.md).
