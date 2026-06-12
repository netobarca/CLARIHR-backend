# Audit Logs — Guía de consumo (frontend) · Fase 15

> **Prerequisitos:** [onboarding 1–6](../README.md). Convenciones globales en el
> [índice maestro](../README.md).

---

## Overview

El **audit trail** de la compañía: un registro **append-only y read-only** de todas las acciones
relevantes (quién hizo qué, cuándo, sobre qué entidad, con el antes/después). Los registros los
escribe el backend internamente — **el cliente nunca crea/edita/borra** entradas.

Un controlador, **2 endpoints `GET`** (base `/api/v1/audit/logs`):

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/api/v1/audit/logs` | listar (paginado, newest first, filtros) |
| `GET` | `/api/v1/audit/logs/{auditLogId}` | detalle con before/after/diff completos |

**No hay `POST`/`PUT`/`PATCH`/`DELETE`** por diseño (integridad probatoria del trail).

### Conceptos clave

- **Permiso**: ambos endpoints requieren `AUDIT_LOGS` acción **Read** (handler-gated). Sin permiso →
  `403`.
- **Tenant-scoped**: solo ves el trail de tu compañía activa. Un id de otra compañía → `403
  TENANT_MISMATCH`; uno inexistente → `404`.
- **PII redactada al escribir**: los campos sensibles (secretos, datos personales) ya vienen
  redactados/saneados en `before`/`after`/`diff` — no expone secretos.
- **Read-only**: no hay concurrencia ni `If-Match` (las entradas son inmutables).

---

## 1. Listar (`GET /api/v1/audit/logs`)

Lista paginada, ordenada por fecha **descendente** (lo más nuevo primero).

### Query Parameters

| Param (wire) | Tipo | Notas |
|--------------|------|-------|
| `fromUtc` | date-time | desde (inclusive) |
| `toUtc` | date-time | hasta (inclusive) |
| `actorUserPublicId` | uuid | filtrar por quién ejecutó la acción |
| `entityPublicId` | uuid | filtrar por la entidad afectada |
| `entityType` | string | tipo de entidad (ver abajo) |
| `eventType` | string | tipo de evento (ver abajo) |
| `search` | string | texto libre (match en email del actor y resumen); **mínimo 2 caracteres** |
| `page` / `pageSize` | int | 1 / 20; máx 100 |

**Rate limit:** 120/min por usuario+tenant.

### Respuesta — `AuditLogSummaryResponse[]` (paginado)

```json
{
  "items": [{
    "publicId": "…",
    "createdAtUtc": "2026-06-10T15:30:00Z",
    "actorUserPublicId": "…",
    "actorEmail": "ana@empresa.com",
    "eventType": "OrgUnitUpdated",
    "entityType": "OrgUnit",
    "entityPublicId": "…",
    "entityKey": "FIN",
    "action": "Update",
    "summary": "Updated organization unit Finanzas.",
    "diff": "{ … cambios resumidos, saneados … }"
  }],
  "pageNumber": 1, "pageSize": 20, "totalCount": 482
}
```

Cada ítem trae el **`diff` saneado** (resumen del cambio); el `before`/`after` completos están en el
detalle.

`400` (validación / `search` < 2) · `401` · `403` · `429`.

---

## 2. Detalle (`GET /api/v1/audit/logs/{auditLogId}`)

### Respuesta — `AuditLogDetailResponse`

Los mismos campos del resumen **más**: `companyPublicId`, `before` (payload previo completo, saneado),
`after` (payload posterior completo), `ipAddress` y `userAgent` del actor.

```json
{
  "publicId": "…", "companyPublicId": "…",
  "createdAtUtc": "…", "actorUserPublicId": "…", "actorEmail": "ana@empresa.com",
  "eventType": "OrgUnitUpdated", "entityType": "OrgUnit", "entityPublicId": "…", "entityKey": "FIN",
  "action": "Update", "summary": "…",
  "before": { … }, "after": { … }, "diff": { … },
  "ipAddress": "190.0.0.1", "userAgent": "Mozilla/5.0 …"
}
```

| Status | Cuándo |
|--------|--------|
| `200` | entrada encontrada en tu compañía |
| `401` / `403` | sin permiso `AUDIT_LOGS:Read` |
| `403 TENANT_MISMATCH` | la entrada pertenece a otra compañía |
| `404` | id inexistente |

---

## `entityType`, `eventType`, `action` (strings estables)

No son enums cerrados en el wire: son **strings estables** que el FE usa para filtrar y mostrar.

- **`entityType`** — la entidad afectada. Ejemplos: `User`, `Role`, `Permission`, `Company`,
  `OrgUnit`, `JobProfile`, `JobProfileCompensation`, `PositionSlot`, `CommercialPlan`,
  `CompanySubscription`, etc.
- **`eventType`** — el evento, típicamente `<Entidad><Acción>`. Ejemplos: `UserLoggedIn`,
  `UserLoginFailed`, `OrgUnitCreated`, `OrgUnitMoved`, `JobProfileUpdated`, `RoleDeleted`,
  `ActiveCompanySwitched`.
- **`action`** — la categoría de operación. Ejemplos: `Create`, `Update`, `Delete`, `Login`,
  `Activate`, `Deactivate`, `Switch`, `Invite`, `Approve`.

> Para los dropdowns de filtro, lo más robusto es **derivar las opciones de los datos** (los valores
> presentes en el trail) en vez de hardcodear la lista completa, ya que crece con cada módulo.

## Guía FE

1. **Pantalla de auditoría**: tabla paginada con los filtros de arriba (rango de fechas + actor +
   entidad + tipos + búsqueda ≥2). Mostrá `createdAtUtc`, `actorEmail`, `summary` y `action`.
2. **Detalle/drill-down**: al abrir una fila, `GET {id}` para el `before`/`after` completos —
   renderizá un diff visual (los payloads ya vienen saneados).
3. **Trail por entidad**: para "historial de este recurso", filtrá por `entityPublicId` (y opcional
   `entityType`). Útil como pestaña "Historial" en las pantallas de cada módulo.
4. **Solo lectura**: no hay acciones de escritura — es una vista de consulta.

## Estado de la documentación

Audit es un módulo de soporte transversal. Ver el [índice maestro](../README.md) para todas las áreas
documentadas.
