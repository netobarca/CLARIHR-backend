# Guía de integración Frontend — Retiro Definitivo de Empleado

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-04 |
| **Rama backend** | `feature/retiro-definitivo` (11 PRs, Olas 1 y 2 completas) |
| **Documentos** | `docs/business/analisis-retiro-definitivo-empleado.md` (D-01…D-19 ratificadas) · `docs/technical/plan-tecnico-retiro-definitivo.md` |
| **⚠ BREAKING** | El `PUT …/employment-information` **pierde los 4 campos de retiro** y rechaza `RETIRADO` (§2). Debe desplegarse FE+BE en el mismo release. |

El módulo agrega las tres pantallas del requerimiento: **Retiros** (solicitud con ciclo de autorización y ejecución), **Entrevista de retiro** (bandeja de empleados autorizados) y **Reversión de retiro**. Todas las convenciones de la casa aplican: prefijo `api/v1`, error de negocio en `problemDetails.extensions.code` (mensajes bilingües EN/ES), concurrencia optimista con `If-Match` (sin header → 400; token obsoleto → 409; el nuevo token viaja en el header `ETag` y en el campo `concurrencyToken` del body), enums/códigos como strings, y todo `Guid XxxId` serializado como `xxxPublicId`.

---

## 1. Permisos (RBAC)

| Permiso | Habilita | Notas |
|---|---|---|
| `PersonnelFiles.ViewRetirements` | Ver solicitudes (per-file y bandeja), detalle, export, interview-tray | `PersonnelFiles.Admin` lo implica. Lectura **solo RRHH** (sin autoservicio). |
| `PersonnelFiles.ManageRetirements` | Registrar, editar, anular una `SOLICITADA`, **ejecutar** | `Admin` lo implica. |
| `PersonnelFiles.AuthorizeRetirement` | Autorizar/rechazar, anular una `AUTORIZADA` | **`Admin` NO lo implica** (separación de funciones). Botones de autorización: mostrar solo con este permiso. |
| `PersonnelFiles.RevertRetirement` | Revertir una `EJECUTADA` | **`Admin` NO lo implica.** |

El **interview-tray** acepta `ViewExitInterviews` **o** `ViewRetirements`. Leer/capturar las **respuestas** de la entrevista sigue gobernado por los permisos existentes del módulo de entrevista.

---

## 2. ⚠ BREAKING — `PUT /api/v1/personnel-files/{id}/employment-information`

La baja **ya no se registra por este PUT** (puerta única, sin fallbacks):

| Cambio | Detalle |
|---|---|
| Campos **eliminados del request** | `retirementCategoryCode`, `retirementReasonCode`, `retirementNotes`, `retirementDate` — enviarlos ahora es ignorado/rechazado por contrato (`additionalProperties: false`). |
| `employmentStatusCode: "RETIRADO"` | **Rechazado** con `422 EMPLOYMENT_STATUS_RETIRADO_RESERVED`. El estado RETIRADO solo lo escribe la ejecución del módulo. |
| Perfil ya retirado (`retirementDate != null`) | El PUT completo se rechaza con `422 EMPLOYMENT_STATUS_RETIRADO_RESERVED`: un retirado solo se toca vía **reversión** o **recontratación**. La pantalla de información de empleo debe mostrarse **solo-lectura** cuando `retirementDate != null`. |
| **GET / response** | **Sin cambios**: sigue devolviendo `retirementCategoryCode/retirementReasonCode/retirementNotes/retirementDate` (ahora escritos únicamente por el módulo). |

Acción FE: quitar los 4 campos del formulario de "Información de empleo", quitar `RETIRADO` del combo de estado laboral, y deshabilitar la edición cuando el perfil esté retirado (mostrar un aviso "Gestionado por el módulo de retiros").

**Datos legados:** al desplegar, una migración **elimina los retiros de prueba** registrados por la vía antigua (los perfiles vuelven a `ACTIVO`); no hay que migrar nada en FE.

---

## 3. Catálogo nuevo y máquina de estados

`GET /api/v1/general-catalogs/retirement-request-statuses` → 6 estados país-scoped (SV sembrado): `SOLICITADA`, `AUTORIZADA`, `RECHAZADA`, `ANULADA`, `EJECUTADA`, `REVERTIDA`. Los **códigos son estructurales** (el nombre es editable/i18n; el código nunca).

Los catálogos de categoría/motivo son los existentes: `GET /api/v1/reference-catalogs/retirement-categories` y `GET /api/v1/reference-catalogs/retirement-reasons?parentCode={categoria}` (el motivo debe pertenecer a la categoría).

```
SOLICITADA ──resolution──► AUTORIZADA ──execution──► EJECUTADA ──reversal──► REVERTIDA (terminal)
    │  │                      │                          (≤ 30 días desde la ejecución)
    │  └─resolution─► RECHAZADA (terminal, nota obligatoria)
    ├─cancel────────► ANULADA (terminal)                 EJECUTADA nunca se anula: se revierte.
    └─(editable con PUT solo aquí)
AUTORIZADA ─annulment─► ANULADA (terminal, por el AUTORIZADOR; archiva la entrevista)
```

Acciones visibles por estado (y permiso):

| Estado | Acciones (permiso) |
|---|---|
| `SOLICITADA` | Editar (`Manage`) · Anular `PATCH /cancel` (`Manage`) · Autorizar/Rechazar `PATCH /resolution` (`Authorize`) |
| `AUTORIZADA` | Ejecutar `PATCH /execution` (`Manage`, solo si `retirementDate ≤ hoy` UTC) · Anular `PATCH /annulment` (`Authorize`) · Entrevista disponible |
| `EJECUTADA` | Revertir `PATCH /reversal` (`Revert`, solo dentro de los 30 días) · Entrevista sigue disponible |
| `RECHAZADA` / `ANULADA` / `REVERTIDA` | Solo lectura (histórico). El empleado queda libre para una **nueva** solicitud. |

---

## 4. Endpoints — solicitud (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/retirement-requests`

### 4.1 Registrar — `POST …/retirement-requests` → `201`

```json
{
  "requesterFilePublicId": "6f9d…",     // expediente del SOLICITANTE (picker de expedientes; puede ser el mismo empleado)
  "requestDate": "2026-07-04",
  "retirementDate": "2026-07-15",        // pasada (retroactiva) o futura (programada); nunca < hireDate
  "retirementCategoryCode": "VOLUNTARIA",
  "retirementReasonCode": "MOTIVOS_PERSONALES",
  "notes": "Presenta carta de renuncia."  // opcional, ≤ 2000
}
```

Reglas: empleado `Employee`+completado, activo y sin retiro vigente; **a lo sumo UNA solicitud abierta** (`SOLICITADA`/`AUTORIZADA`) por empleado; `requestDate ≤ hoy` (UTC).

### 4.2 Respuesta (shape de todas las operaciones)

```json
{
  "retirementRequestPublicId": "0b3c…",
  "requesterFilePublicId": "6f9d…",
  "requesterName": "Rafael Solicitante",          // snapshot al registrar
  "requestDate": "2026-07-04T00:00:00Z",
  "retirementDate": "2026-07-15T00:00:00Z",
  "retirementCategoryCode": "VOLUNTARIA",
  "retirementCategoryName": "Renuncia voluntaria", // snapshot
  "retirementReasonCode": "MOTIVOS_PERSONALES",
  "retirementReasonName": "Motivos personales",    // snapshot
  "notes": "Presenta carta de renuncia.",
  "requestStatusCode": "SOLICITADA",
  "requestedByUserPublicId": "…",                  // quién lo registró en el sistema (auditoría)
  "resolvedByUserPublicId": null, "resolutionDateUtc": null, "resolutionNotes": null,
  "canceledByUserPublicId": null, "cancellationDateUtc": null, "cancellationNotes": null,
  "executedByUserPublicId": null, "executionDateUtc": null,
  "revertedByUserPublicId": null, "reversalDateUtc": null, "reversalReason": null,
  "isActive": true,
  "concurrencyToken": "e7a1…"          // ⇒ header If-Match de la siguiente mutación
}
```

La **línea de tiempo** del detalle se arma con los tríos `(actor, fecha, nota)` de cada transición: solicitada (`requestedBy…`), resolución, anulación, ejecución y reversión.

### 4.3 Resto del CRUD

| Operación | Endpoint | Notas |
|---|---|---|
| Listar del expediente | `GET …/retirement-requests` | Orden: más reciente primero. |
| Detalle | `GET …/retirement-requests/{id}` | |
| Editar | `PUT …/retirement-requests/{id}` + `If-Match` | **Solo `SOLICITADA`** (422 en otro estado). Mismo body que el POST. |
| Anular `SOLICITADA` | `PATCH …/{id}/cancel` + `If-Match` — body `{ "notes": "…" }` (nota opcional) | Permiso `Manage`. Si está `AUTORIZADA` → usar `/annulment`; si `EJECUTADA` → 422 (corresponde reversión). |

No existe DELETE: el registro nunca se borra (histórico); la salida es `ANULADA`.

---

## 5. Acciones del ciclo

### 5.1 Autorizar / rechazar — `PATCH …/{id}/resolution` + `If-Match` (permiso `AuthorizeRetirement`)

```json
{ "targetStatusCode": "AUTORIZADA", "notes": null }          // nota opcional al autorizar
{ "targetStatusCode": "RECHAZADA", "notes": "No procede." }  // nota OBLIGATORIA al rechazar
```

403 esperables (mostrar el mensaje del backend): el **empleado sujeto** no puede autorizar su propio retiro (`RETIREMENT_SELF_ACTION_FORBIDDEN`) y el **solicitante** no puede autorizar lo que él pidió (`RETIREMENT_REQUESTER_CANNOT_AUTHORIZE`) — si el autorizador habitual es quien renuncia, autoriza su superior.

### 5.2 Anular una `AUTORIZADA` — `PATCH …/{id}/annulment` + `If-Match` (permiso `AuthorizeRetirement`)

Body `{ "notes": "…" }` (opcional). Efecto adicional: el empleado sale del interview-tray y **todas** sus submissions de entrevista no archivadas se archivan (la baja "no ocurrió").

### 5.3 Ejecutar la baja — `PATCH …/{id}/execution` + `If-Match` (permiso `ManageRetirements`)

```json
{ "blockRehire": false, "rehireBlockReason": null }   // D-18: opcionalmente marcar "no recontratable"
```

Solo desde `AUTORIZADA` y **solo cuando `retirementDate ≤ hoy`** (fecha UTC — una baja programada se ejecuta al llegar el día; las retroactivas se ejecutan de inmediato). En **una transacción** el backend: estampa el perfil (`RETIRADO` + categoría/motivo/nota/fecha), desactiva el expediente, **cierra todas las plazas y contratos activos** a la fecha de retiro, **desactiva el login** del empleado (revoca sus sesiones — si el empleado está usando la app, quedará deslogueado), journalea la acción `BAJA` y guarda el snapshot para la reversión.

422 esperables: `RETIREMENT_EXECUTION_DATE_NOT_REACHED` (fecha futura), `RETIREMENT_EXECUTION_STATE_CONFLICT` (el perfil cambió), `RETIREMENT_REQUEST_DATE_INCOHERENT` (hay una plaza/contrato activo que inicia después de la fecha de retiro), `RETIREMENT_LAST_ADMIN_CONFLICT` (es el último administrador activo — transferir la administración primero).

### 5.4 Revertir — `PATCH …/{id}/reversal` + `If-Match` (permiso `RevertRetirement`)

```json
{ "reason": "Baja registrada por error administrativo." }   // OBLIGATORIO
```

Solo desde `EJECUTADA` y **dentro de los 30 días calendario** posteriores a la ejecución (`executionDateUtc`). Restaura exactamente lo que la ejecución cambió: perfil limpio con su estado laboral **previo** (no asume ACTIVO), expediente activo con su bloqueo de rehire previo, plazas/contratos **reabiertos con su fecha de fin anterior**, login reactivado **solo si estaba activo antes**, entrevista archivada, acción `REVERSION_BAJA`. La antigüedad continúa (el `hireDate` nunca se movió) — a diferencia de la **recontratación**, que abre un período nuevo.

422 esperables: `RETIREMENT_REVERSAL_WINDOW_EXPIRED` (＞30 días — ofrecer "Recontratar" como alternativa), `RETIREMENT_REVERSAL_BLOCKED_BY_REHIRE`, `RETIREMENT_REVERSAL_STATE_DIVERGED`, `RETIREMENT_REVERSAL_NOT_MOST_RECENT`, `RETIREMENT_REVERSAL_REASON_REQUIRED`.

Sugerencia UI: mostrar el botón "Revertir" con un contador de días restantes (`executionDateUtc + 30d − ahora`); pasado el plazo, deshabilitarlo con tooltip "Ventana vencida — use Recontratación".

---

## 6. Bandeja de la empresa (pantalla "Retiros")

### 6.1 Consulta — `POST /api/v1/companies/{companyId}/retirement-requests/query`

```json
{
  "statusCode": "SOLICITADA",          // opcional (canónico)
  "categoryCode": null, "reasonCode": null,
  "employeeId": null,                   // publicId del expediente
  "requestFromUtc": null, "requestToUtc": null,
  "retirementFromUtc": null, "retirementToUtc": null,
  "search": "ana",                      // texto libre: nombre del empleado o del solicitante
  "pageNumber": 1, "pageSize": 25
}
```

Respuesta: `{ items: […], pageNumber, pageSize, totalCount, statusCounts: { "SOLICITADA": 3, "EJECUTADA": 1, … } }` — usar `statusCounts` para los contadores/chips por estado. Cada item trae empleado (`personnelFilePublicId`, `employeeFullName`), solicitante, fechas, categoría/motivo (código + nombre snapshot), estado y las fechas de resolución/ejecución/reversión.

### 6.2 Export — `GET /api/v1/companies/{companyId}/retirement-requests/export?format=xlsx&…`

Mismos filtros como query-params (`statusCode, categoryCode, reasonCode, employeeId, requestFromUtc, requestToUtc, retirementFromUtc, retirementToUtc, q`). Formatos `xlsx | csv | json`. Descarga síncrona con tope configurado (`413 REPORT_EXPORT_TOO_LARGE` si se excede — sugerir filtrar).

---

## 7. Pantalla "Entrevista de retiro" (bandeja de autorizados)

`GET /api/v1/companies/{companyId}/retirement-requests/interview-tray?interviewStatus=&categoryCode=&reasonCode=&retirementFromUtc=&retirementToUtc=`

Lista a los empleados con retiro `AUTORIZADA` o `EJECUTADA` (los `REVERTIDA/ANULADA/RECHAZADA` desaparecen). Cada fila:

```json
{
  "retirementRequestPublicId": "…",
  "personnelFilePublicId": "…",
  "employeeFullName": "Elena Egresada",
  "retirementCategoryCode": "VOLUNTARIA", "retirementCategoryName": "Renuncia voluntaria",
  "retirementReasonCode": "MOTIVOS_PERSONALES", "retirementReasonName": "Motivos personales",
  "retirementDate": "2026-07-15T00:00:00Z",
  "requestStatusCode": "AUTORIZADA",
  "interviewStatus": "PENDIENTE",      // SIN_FORMULARIO | PENDIENTE | BORRADOR | ENVIADA
  "submissionPublicId": null            // navegar a la entrevista existente cuando no es null
}
```

Desde la fila se navega a la entrevista del empleado usando los **endpoints existentes** del módulo de entrevista (visualizar formulario / capturar respuestas — sin cambios de contrato). Cambio de comportamiento del gate: la entrevista se habilita **desde la autorización** (ya no hace falta que el perfil tenga la baja consumada); si no hay solicitud vigente, el guardado devuelve el 400 de precondición con el mensaje "no retirement request in force". `SIN_FORMULARIO` = no hay formulario publicado activo para el motivo (la entrevista sigue siendo **opcional** y nunca bloquea la ejecución).

---

## 8. Tabla de errores del módulo

| `extensions.code` | HTTP | Cuándo |
|---|---|---|
| `RETIREMENT_REQUEST_EMPLOYEE_NOT_ELIGIBLE` | 422 | Alta/autorización: no es empleado completado activo, o ya está retirado. |
| `RETIREMENT_REQUEST_ALREADY_OPEN` | 422 | Ya hay una `SOLICITADA`/`AUTORIZADA` del empleado. |
| `RETIREMENT_REQUEST_REQUESTER_INVALID` | 422 | El solicitante no es un expediente válido/activo de la empresa. |
| `RETIREMENT_REQUEST_DATE_INCOHERENT` | 422 | `requestDate` futura, `retirementDate < hireDate`, o una plaza/contrato activo inicia después de la fecha de retiro. |
| `RETIREMENT_REQUEST_STATE_RULE_VIOLATION` | 422 | Transición/edición inválida para el estado actual. |
| `RETIREMENT_RESOLUTION_TARGET_INVALID` | 422 | `targetStatusCode` ∉ {AUTORIZADA, RECHAZADA}. |
| `RETIREMENT_RESOLUTION_NOTES_REQUIRED` | 422 | Rechazo sin nota. |
| `RETIREMENT_SELF_ACTION_FORBIDDEN` | **403** | El empleado sujeto intenta autorizar/ejecutar/revertir su propio retiro. |
| `RETIREMENT_REQUESTER_CANNOT_AUTHORIZE` | **403** | El solicitante intenta autorizar/anular la solicitud que él pidió. |
| `RETIREMENT_EXECUTION_DATE_NOT_REACHED` | 422 | Ejecutar antes de `retirementDate`. |
| `RETIREMENT_EXECUTION_STATE_CONFLICT` | 422 | El perfil divergió (p. ej. ya retirado). |
| `RETIREMENT_LAST_ADMIN_CONFLICT` | 422 | El sujeto es el último admin activo de la empresa. |
| `RETIREMENT_REVERSAL_REASON_REQUIRED` | 422 | Revertir sin motivo. |
| `RETIREMENT_REVERSAL_WINDOW_EXPIRED` | 422 | ＞30 días desde la ejecución. |
| `RETIREMENT_REVERSAL_BLOCKED_BY_REHIRE` | 422 | Hubo recontratación después de la ejecución. |
| `RETIREMENT_REVERSAL_STATE_DIVERGED` | 422 | El estado actual no coincide con lo que dejó la ejecución. |
| `RETIREMENT_REVERSAL_NOT_MOST_RECENT` | 422 | No es el retiro ejecutado más reciente. |
| `RETIREMENT_REQUEST_NOT_FOUND` | 404 | Solicitud inexistente. |
| `EMPLOYMENT_STATUS_RETIRADO_RESERVED` | 422 | PUT legado con `RETIRADO` o sobre un perfil retirado (§2). |
| `PERSONNEL_FILE_ITEM_NOT_FOUND` / `CONCURRENCY_CONFLICT` | 404 / 409 | Convenciones existentes (ítem/If-Match). |
| `REPORT_EXPORT_TOO_LARGE` / `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` | 413 / 400 | Export. |

Todos los mensajes llegan localizados (EN/ES) en `problemDetails.detail`; mostrar tal cual.

---

## 9. Flujo recomendado de pantallas

1. **Retiros → Nueva solicitud**: picker de empleado (expedientes empleado-completado activos), picker de solicitante (cualquier expediente activo; por defecto el mismo empleado si es renuncia), fecha de solicitud (default hoy), fecha de retiro, categoría → motivo (cascada por `parentCode`), observación. POST → toast + fila en bandeja.
2. **Bandeja**: chips por estado con `statusCounts`; filas → detalle con línea de tiempo + botones por estado/permiso (§3). Export arriba a la derecha.
3. **Autorización**: modal con nota opcional (autorizar) / obligatoria (rechazar). Tras autorizar, sugerir "Ir a entrevista" (tray).
4. **Ejecución**: modal de confirmación resumiendo los efectos (cerrar plazas/contratos, desactivar login, opción "bloquear recontratación" + razón). Deshabilitado si `retirementDate > hoy` (mostrar "programada para {fecha}").
5. **Entrevista**: tray (§7) → pantalla de captura existente.
6. **Reversión**: desde el detalle de una `EJECUTADA`, modal con motivo obligatorio + contador de la ventana. Tras revertir, refrescar el expediente completo (perfil, plazas, contratos vuelven a activo).

---

## 10. Notas de despliegue

- **Mismo release FE+BE** (breaking del §2, ratificado sin fallbacks).
- Migraciones incluidas: catálogo+acciones (`…AddRetirementRequestStatusCatalogAndBajaActionTypes`), tablas (`…AddRetirementRequests`) y **limpieza destructiva de retiros de prueba** (`…CloseLegacyRetirementPathAndCleanupTestData`).
- Los 4 permisos se agregan al catálogo de aprovisionamiento; hay que **asignarlos a los roles** correspondientes (recordar que `Admin` NO incluye autorizar ni revertir).
- La acción de recontratación ahora journalea con estado `APLICADA` (antes `COMPLETADA`, código huérfano — corregido con data-fix).
