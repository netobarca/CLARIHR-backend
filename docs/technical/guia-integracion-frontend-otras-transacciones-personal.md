# Guía de integración Frontend — Otras transacciones de personal (Reconocimientos · Amonestaciones · Disponibilidad de tiempos)

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-08 |
| **Rama backend** | `feature/vacaciones-incapacidades` (REQ-003 PR-1…PR-6 completos) |
| **Documentos** | `docs/business/analisis-otras-transacciones-personal.md` (D-01…D-18) · `docs/technical/plan-tecnico-otras-transacciones-personal.md` |
| **Alcance** | 3 maestros por empresa (con plantilla sembrada + `load-template`) · **reconocimientos** y **amonestaciones** como registros del expediente con **flujo de una decisión** y anti-autoaprobación **doble** · amonestación con **suspensión sin goce** y **descuento documental** · asientos automáticos (`RECONOCIMIENTO`/`AMONESTACION`/`SUSPENSION`) + revocación · adjuntos (2 purposes) · bandejas/exports + **insumo de planilla** · **consulta de disponibilidad de tiempos** por fuentes conectables (`activeSources[]`) |

El módulo modela dos familias de transacciones del expediente. Ambas nacen `EN_REVISION`, se **aprueban o rechazan en una sola decisión** (no hay flujo multi-nivel en F1) y, al **APLICAR**, generan asientos automáticos en el historial de acciones de personal. Convenciones de la casa:

- Prefijo `api/v1`.
- Error de negocio en `problemDetails.extensions.code` (mensajes bilingües EN/ES/es-SV en `problemDetails.detail` — mostrar tal cual).
- Concurrencia optimista con `If-Match` en **todo write** (sin header → `400`; token obsoleto → `409`; el token nuevo viaja en `ETag` y en `concurrencyToken` del body). El `DELETE` de documento lleva el token del padre en `parentConcurrencyToken`.
- Enums/códigos como **strings**; todo `Guid XxxId` se serializa como `xxxPublicId`.
- Fechas del hecho/falta/suspensión viajan como `date` (`"2026-05-10"`, sin hora); las fechas de auditoría/decisión son `date-time` UTC.

> **Sin dinero real.** El monto del reconocimiento es **informativo** y el descuento de la amonestación es un **insumo documental** para la planilla: el módulo **no** escribe ledgers de nómina (frontera «nómina externa»). La coexistencia con el asiento manual del historial se explica en §7.

---

## 1. Permisos (RBAC)

Siete permisos nuevos, dos familias simétricas + uno de consulta:

| Permiso | Habilita | Notas |
|---|---|---|
| `PersonnelFiles.EmployeeRelationsConfiguration.Read` / `.Admin` | Ver / administrar los **3 maestros** + `load-template` | `Admin` los implica. |
| `PersonnelFiles.ViewRecognitions` | Leer reconocimientos, bandeja y export de reconocimientos | `Admin` lo implica. Lectura de expediente ajeno: **View OR es el propio empleado** (self ve **solo `APLICADA`** — §4.5). |
| `PersonnelFiles.ManageRecognitions` | Crear/editar/anular (trámite) reconocimientos + adjuntos | `Admin` lo implica. |
| `PersonnelFiles.AuthorizeRecognitions` | **Decidir** (APLICAR/RECHAZAR) y **revocar** un reconocimiento aplicado | **NO lo implica `Admin`** (separación de funciones, espejo de `AuthorizeRetirement`). Solo el grant dedicado o el super-admin IAM (`ManageAdministration`). |
| `PersonnelFiles.ViewDisciplinaryActions` | Leer amonestaciones, bandeja/export + **insumo de planilla** | Igual corte que reconocimientos. |
| `PersonnelFiles.ManageDisciplinaryActions` | Crear/editar/anular (trámite) amonestaciones + adjuntos | `Admin` lo implica. |
| `PersonnelFiles.AuthorizeDisciplinaryActions` | **Decidir** y **revocar** una amonestación aplicada | **NO lo implica `Admin`** (igual que arriba). |
| `PersonnelFiles.ViewTimeAvailability` | **Consulta de disponibilidad de tiempos** (query + export) | `Admin` lo implica. Lectura corporativa **sin rama self**. |

> **Consecuencia UI:** el botón «Aprobar/Rechazar/Revocar» exige `Authorize{Recognitions|DisciplinaryActions}`. Un usuario con `Admin` (RRHH gestor) **puede registrar y anular trámites en revisión** pero **no puede decidir ni revocar aplicados** — necesita el grant de autorización. Separá los roles «registra» y «autoriza».

---

## 2. Catálogos y máquinas de estado

Catálogos país-scoped (SV sembrado) vía `GET /api/v1/general-catalogs/{key}?countryCode=SV` — los **códigos son estructurales** (el nombre es i18n/editable):

| Wire key | Códigos |
|---|---|
| `personnel-transaction-statuses` | `EN_REVISION`, `APLICADA`, `RECHAZADA`, `ANULADA` |
| `action-types` (existente, +1) | …, `RECONOCIMIENTO`, `AMONESTACION`, `SUSPENSION` (los dos últimos ya existían) |

```
Reconocimiento / Amonestación:
   EN_REVISION ──decision:APLICAR──► APLICADA ──annulment (revocación)──► ANULADA (terminal)
   EN_REVISION ──decision:RECHAZAR─► RECHAZADA (terminal)
   EN_REVISION ──annulment (retiro de trámite)──► ANULADA (terminal)
```

- **Decisión** (`PATCH …/decision`) solo desde `EN_REVISION` → `APLICADA` (con asientos) o `RECHAZADA` (nota obligatoria). Requiere `Authorize*`.
- **Anulación** (`PATCH …/annulment`, motivo obligatorio): desde `EN_REVISION` = **retiro de trámite** (permiso `Manage*`); desde `APLICADA` = **revocación** (permiso `Authorize*`, anula también los asientos).
- Una segunda decisión concurrente sobre el mismo registro → `409` (token) o `422 PERSONNEL_TRANSACTION_STATE_RULE_VIOLATION` (estado).

---

## 3. Maestros por empresa + adopción (`load-template`)

Tres maestros **governed** (familia `[ResourceActions]`): cada respuesta incluye `allowedActions[]` (habilitar botones), `concurrencyToken` e `isActive`. Permiso `EmployeeRelationsConfiguration.Read` (GET) / `.Admin` (write). If-Match en todo write; sin borrado físico (activar/inactivar).

| Maestro | Rutas | Campos de negocio |
|---|---|---|
| Tipos de reconocimiento | `GET/POST /companies/{companyId}/recognition-types` · `GET/PUT /recognition-types/{id}` · `PATCH /recognition-types/{id}/activate\|inactivate` | `{ code, name, sortOrder? }` |
| Tipos de amonestación | `…/disciplinary-action-types` (mismas operaciones) | `{ code, name, appliesSuspension, sortOrder? }` |
| Causas de amonestación | `…/disciplinary-action-causes` (mismas operaciones) | `{ code, name, deductionConceptTypeCode?, sortOrder? }` |

- **`appliesSuspension`** (tipo): habilita el bloque de suspensión al registrar una amonestación de ese tipo. Cambiarlo **no** altera registros existentes (cada amonestación guarda el flag como snapshot).
- **`deductionConceptTypeCode`** (causa, opcional): concepto de egreso **por defecto** del descuento. Se valida contra el catálogo país (`GET /api/v1/compensation-concept-types?countryCode=SV`, `Nature=Egreso`, activo) → si no cumple, `422 DEDUCTION_CONCEPT_INVALID`. **La plantilla sembrada trae las causas SIN concepto** (P-14 «no hay multas»); cada empresa asocia conceptos si su marco lo permite.
- Inactivar un maestro referenciado por un registro activo → `422 …_IN_USE`.

**Plantilla + adopción.** Al provisionar una empresa nueva, el backend **siembra** los 3 maestros (Anexo A.2). Para tenants **existentes**: `POST /api/v1/companies/{companyId}/employee-relations/load-template` (idempotente — 2.ª corrida = 0 cambios, nunca pisa ediciones). Ofrecé un botón «Cargar plantilla» en la pantalla de configuración.

---

## 4. Reconocimientos (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/recognitions`.

### 4.1 Registrar — `POST …/recognitions` → `201`

```json
{
  "recognitionTypePublicId": "…",
  "eventDate": "2026-03-01",
  "detail": "Reconocimiento por desempeño sobresaliente.",
  "amount": null,
  "currencyCode": null,
  "assignedPositionPublicId": null,
  "notes": null
}
```
Permiso `ManageRecognitions`. Nace `EN_REVISION` **sin asiento**. Validaciones: tipo activo del tenant (`RECOGNITION_TYPE_INVALID`); `eventDate ≤ hoy` (`RECOGNITION_EVENT_DATE_IN_FUTURE`); si viaja `amount` debe ser `> 0` con `currencyCode` (`RECOGNITION_AMOUNT_INVALID`). Perfil `RETIRADO` → `422 EMPLOYEE_PROFILE_RETIRED_LOCKED`.

### 4.2 Editar — `PUT …/recognitions/{id}` (If-Match)

Solo en `EN_REVISION`. Fuera de ese estado → `422 PERSONNEL_TRANSACTION_STATE_RULE_VIOLATION`.

### 4.3 Decidir — `PATCH …/recognitions/{id}/decision` (If-Match)

```json
{ "decision": "APLICAR", "note": null }      // o { "decision": "RECHAZAR", "note": "motivo obligatorio" }
```
Permiso **`AuthorizeRecognitions`**. **Anti-autoaprobación DOBLE** (RN-02): si el usuario actual es **el empleado sujeto** (`linkedUser`) **o** **quien registró** el trámite → `403 RECOGNITION_SELF_APPROVAL_FORBIDDEN`. Rechazar sin nota → `422 DECISION_NOTE_REQUIRED`.

Al **APLICAR** se crea un asiento `RECONOCIMIENTO` (`isSystemGenerated: true`, `APLICADA`) en la misma transacción; su publicId queda en `personnelActionPublicId` de la respuesta.

### 4.4 Anular / revocar — `PATCH …/recognitions/{id}/annulment` (If-Match)

```json
{ "reason": "Registrado por error." }
```
`reason` obligatorio (`ANNULMENT_REASON_REQUIRED`). Desde `EN_REVISION` → retiro de trámite (`ManageRecognitions`). Desde `APLICADA` → **revocación** (`AuthorizeRecognitions` + anti-self doble): anula el registro **y** el asiento `RECONOCIMIENTO` vinculado (→ `ANULADA`).

### 4.5 Lecturas — visibilidad self solo-`APLICADA`

- `GET …/recognitions` (lista) · `GET …/recognitions/{id}` — `View OR es el propio empleado`.
- El empleado logueado que lee **su propio** expediente sin `ViewRecognitions` ve **solo los `APLICADA`** (D-13): los `EN_REVISION`/`RECHAZADA`/`ANULADA` **no viajan**. RRHH (con `View`/`Admin`) ve todos los estados.

### 4.6 Adjuntos — `…/recognitions/{id}/documents`

Sub-recurso opcional, **solo RRHH** (`ManageRecognitions`). Flujo de 3 patas (upload-session → complete → asociar). `POST` asocia un `filePublicId` ya subido con `purpose = RecognitionDocument`; purpose ajeno → `400 files.invalid_purpose`. `GET …/documents/{docId}/read-url` da una URL temporal; `DELETE …/documents/{docId}` lleva `parentConcurrencyToken`.

---

## 5. Amonestaciones (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/disciplinary-actions`. **Espejo exacto** de reconocimientos (registrar/editar/decidir/anular/lecturas/documentos, con los permisos `*DisciplinaryActions` y purpose `DisciplinaryActionDocument`) **más** los bloques de suspensión y descuento.

### 5.1 Registrar — `POST …/disciplinary-actions` → `201`

```json
{
  "disciplinaryActionTypePublicId": "…",
  "disciplinaryActionCausePublicId": "…",
  "incidentDate": "2026-05-28",
  "factsDetail": "Faltó tres días sin aviso.",
  "hasPayrollDeduction": true,
  "deductionAmount": 25.00,
  "currencyCode": "USD",
  "deductionConceptTypeCode": null,
  "suspensionStartDate": "2026-05-28",
  "suspensionEndDate": "2026-06-03",
  "assignedPositionPublicId": null,
  "notes": null
}
```

- **Bloque suspensión** (solo si el tipo tiene `appliesSuspension`): `suspensionStartDate`/`EndDate` requeridas juntas; `inicio ≤ fin`; **fechas futuras permitidas**. Los **días** (`suspensionDays`) los **deriva** el backend (calendario-inclusivo, `fin − inicio + 1`). Errores: `SUSPENSION_NOT_ALLOWED_FOR_TYPE` (fechas sobre tipo sin flag), `SUSPENSION_DATES_REQUIRED`, `SUSPENSION_RANGE_INVALID`.
- **Bloque descuento** (`hasPayrollDeduction`): exige `deductionAmount > 0` (`DEDUCTION_AMOUNT_REQUIRED`). El `deductionConceptTypeCode` que viaja se valida `Nature=Egreso` activo (`DEDUCTION_CONCEPT_INVALID`); si se omite, se toma el **default de la causa** al aplicar.
- Sin cambio automático de estado del empleado: `SUSPENDIDO` es **gestión manual** de RRHH (no lo produce este módulo).

### 5.2 Decidir — `PATCH …/disciplinary-actions/{id}/decision`

Permiso `AuthorizeDisciplinaryActions` + anti-self doble → `403 DISCIPLINARY_ACTION_SELF_APPROVAL_FORBIDDEN`. Al **APLICAR**:
- **Congela el concepto de descuento** (default de la causa o el del registro), re-validado `Nature=Egreso` activo **en el momento de aplicar** (→ `422 DEDUCTION_CONCEPT_INVALID` si quedó inactivo). El snapshot viaja en `deductionConceptTypeCode` + `deductionConceptNameSnapshot`.
- **Solape de suspensiones** (RN-18): si el empleado ya tiene una amonestación `APLICADA` con suspensión que solapa el rango → `422 SUSPENSION_OVERLAP` (verificado bajo lock transaccional).
- **Doble asiento** en la misma transacción: `AMONESTACION` (`amount` = descuento si existe) **+** `SUSPENSION` (con `effectiveFrom/ToUtc` = rango) cuando aplica. Sus publicIds quedan en `personnelActionPublicId` y `suspensionActionPublicId`.

### 5.3 Revocar — `PATCH …/disciplinary-actions/{id}/annulment` desde `APLICADA`

`AuthorizeDisciplinaryActions` + anti-self doble + `reason`. Anula el registro **y ambos** asientos vinculados (`AMONESTACION` + `SUSPENSION` → `ANULADA`). Una amonestación revocada **sale del insumo de planilla y de la consulta de disponibilidad** (RN-14/RN-15).

---

## 6. Bandejas, exportaciones e insumo de planilla (nivel empresa)

Permiso `View{Recognitions|DisciplinaryActions}` (gate por handler; el POST de query es una **lectura**). Rate-limit de búsqueda/exportación; descarga síncrona con tope de filas (`413` si se excede).

| Operación | Endpoint |
|---|---|
| Bandeja reconocimientos | `POST /companies/{companyId}/recognitions/query` |
| Export reconocimientos | `GET /companies/{companyId}/recognitions/export?format=xlsx\|csv\|json&…` |
| Bandeja amonestaciones | `POST /companies/{companyId}/disciplinary-actions/query` |
| Export amonestaciones | `GET /companies/{companyId}/disciplinary-actions/export?…` |
| **Insumo de planilla** | `GET /companies/{companyId}/disciplinary-actions/payroll-input/export?format=…&startDate=…&endDate=…` |

- **Query (body):** filtros opcionales (`employeeId`, código de tipo/causa, `statusCode`, rango `fromDate`/`toDate` del hecho/falta, `pageNumber`/`pageSize`). Respuesta: `items[] + pageNumber/pageSize/totalCount + statusCounts`. **`statusCounts` cubre TODOS los estados**, pero los `items` **excluyen `ANULADA` por defecto** — para incluirlos, `includeAnnulled: true` o un `statusCode` explícito.
- **Insumo de planilla (RF-012):** rango **obligatorio** (filtra por `incidentDate`) — faltante/incoherente → `422 PERSONNEL_TRANSACTION_RANGE_REQUIRED`. Solo amonestaciones `APLICADA` con efecto, **una fila por efecto**: `Efecto = DESCUENTO` (con `ConceptoDescuento`/`Monto`/`Moneda`) o `SUSPENSION_SIN_GOCE` (con `FechaInicio`/`FechaFin`/`Dias`). Una amonestación con descuento **y** suspensión emite **2 filas**. Las revocadas (`ANULADA`) **nunca viajan**.

---

## 7. Coexistencia con el asiento manual del historial (RN-16)

El historial de acciones de personal (`GET /api/v1/personnel-files/{id}/personnel-actions`) admite **altas manuales** de cualquier tipo/estado (incluidos `AMONESTACION`/`SUSPENSION`) y **sigue existiendo** — este módulo no lo reemplaza. Diferencias que el FE debe respetar:

- Los asientos de **este módulo** llevan `isSystemGenerated: true` y están **referenciados** por el registro de reconocimiento/amonestación (`personnelActionPublicId`/`suspensionActionPublicId`). **No** los edites/anules por el endpoint manual: usá el `annulment` del registro (revocación), que los anula de forma consistente.
- Las **bandejas, el insumo de planilla y la consulta de disponibilidad leen SOLO las entidades del módulo** (no el journal manual). Un asiento manual histórico `AMONESTACION` **no** aparece en el insumo ni en la consulta — evitá el doble conteo mostrando esas vistas como «lo gestionado por el módulo».

---

## 8. Consulta de disponibilidad de tiempos (planificación)

Vista corporativa que unifica «quién no está disponible, cuándo y por qué» bajo **un contrato estable**. Permiso `ViewTimeAvailability` (sin rama self). **Payload mínimo**: no viaja causa, hechos ni montos (P-10).

### 8.1 Query — `POST /companies/{companyId}/time-availability/query`

```json
{
  "startDate": "2026-06-01",
  "endDate": "2026-06-15",
  "personnelFilePublicId": null,
  "categoryCodes": null,
  "orgUnitPublicId": null,
  "pageNumber": 1,
  "pageSize": 50
}
```
Rango **obligatorio**: faltante → `422 TIME_AVAILABILITY_RANGE_REQUIRED`; incoherente (inicio > fin) → `422 TIME_AVAILABILITY_RANGE_INVALID`. Filtros opcionales: un empleado (`personnelFilePublicId`), categorías (`categoryCodes`, omitir = todas) y unidad organizativa. Orden: `startDate` asc, empleado como desempate; paginado 1-100.

**Respuesta:**
```json
{
  "rows": [
    { "personnelFilePublicId": "…", "employeeName": "Ana Suspendida", "employeeCode": "EMP-TA-A",
      "positionPublicId": null, "positionName": null,
      "categoryCode": "SUSPENSION", "startDate": "2026-05-28", "endDate": "2026-06-03", "days": 7,
      "statusCode": "APLICADA", "sourceModule": "EMPLOYEE_RELATIONS", "referencePublicId": "<amonestación>" },
    { "categoryCode": "FIN_CONTRATO_TEMPORAL", "startDate": "2026-06-10", "endDate": "2026-06-10", "days": 1,
      "statusCode": "VIGENTE", "sourceModule": "EMPLOYMENT", "referencePublicId": "<plaza>", "…": "…" }
  ],
  "pageNumber": 1, "pageSize": 50, "totalCount": 2,
  "categoryCounts": { "SUSPENSION": 1, "FIN_CONTRATO_TEMPORAL": 1 },
  "activeSources": ["SUSPENSION", "FIN_CONTRATO_TEMPORAL"]
}
```

### 8.2 Fuentes de F1 y `activeSources[]` (degradación visible)

| `categoryCode` | `sourceModule` | Qué es | `referencePublicId` | `days` / `statusCode` |
|---|---|---|---|---|
| `SUSPENSION` | `EMPLOYEE_RELATIONS` | Amonestación `APLICADA` cuyo bloque de suspensión **intersecta** el rango (fechas reales) | la amonestación | `SuspensionDays` / `APLICADA` |
| `FIN_CONTRATO_TEMPORAL` | `EMPLOYMENT` | Plaza activa con contrato **temporal** (`IsTemporary`) cuyo `EndDate` cae en el rango | la plaza/asignación | `1` / `VIGENTE` |

- **`activeSources[]` es la lista de familias conectadas** (F1: las 2 de arriba). Es **estable**: usalo para pintar la leyenda/filtros de categoría **sin heurísticas** — si una familia no está en `activeSources`, todavía no hay datos de esa fuente.
- **`categoryCounts`** lista las categorías **incluidas** en la consulta (0 si no produjeron filas); si filtrás con `categoryCodes`, las excluidas no aparecen.
- **Fuentes futuras (costura, sin código en F1):** al construirse REQ-001/REQ-002 se agregarán categorías `VACACION`, `INCAPACIDAD`, `PERMISO` (y un futuro módulo de permisos generales). El backend las conecta agregando un método de fuente + la categoría a `activeSources` — **el contrato de la fila NO cambia**. El FE debe iterar `rows[]`/`categoryCounts`/`activeSources` **genéricamente** (no hardcodear las 2 categorías de F1) para absorberlas sin cambios.

### 8.3 Export — `GET /companies/{companyId}/time-availability/export?format=xlsx|csv|json&startDate=…&endDate=…&…`

Mismas fuentes/filtros; filas con encabezados en español: `Empleado, CodigoEmpleado, Plaza, Categoria, FechaInicio, FechaFin, Dias, Estado, Fuente`. Rango obligatorio (422 si falta), tope de filas (`413`).

---

## 9. Tabla de errores del módulo

| `code` | HTTP | Cuándo |
|---|---|---|
| `RECOGNITION_TYPE_INVALID` / `DISCIPLINARY_ACTION_TYPE_INVALID` / `DISCIPLINARY_ACTION_CAUSE_INVALID` | 422 | Maestro inexistente/inactivo/de otro tenant |
| `RECOGNITION_TYPE_IN_USE` / `DISCIPLINARY_ACTION_TYPE_IN_USE` / `DISCIPLINARY_ACTION_CAUSE_IN_USE` | 422 | Inactivar un maestro referenciado por un registro activo |
| `DEDUCTION_CONCEPT_INVALID` | 422 | Concepto inexistente/inactivo/no-egreso (en la causa, en el registro o al aplicar) |
| `RECOGNITION_EVENT_DATE_IN_FUTURE` / `DISCIPLINARY_ACTION_INCIDENT_DATE_IN_FUTURE` | 422 | Fecha del hecho/falta > hoy |
| `RECOGNITION_AMOUNT_INVALID` | 422 | Monto ≤ 0 o sin moneda |
| `SUSPENSION_NOT_ALLOWED_FOR_TYPE` / `SUSPENSION_DATES_REQUIRED` / `SUSPENSION_RANGE_INVALID` | 422 | Bloque de suspensión incoherente con el flag del tipo o con el rango |
| `SUSPENSION_OVERLAP` | 422 | Solape con una suspensión vigente del empleado (al aplicar) |
| `DEDUCTION_AMOUNT_REQUIRED` | 422 | `hasPayrollDeduction` sin monto > 0 |
| `RECOGNITION_SELF_APPROVAL_FORBIDDEN` / `DISCIPLINARY_ACTION_SELF_APPROVAL_FORBIDDEN` | 403 | Decide/revoca el sujeto o quien registró |
| `PERSONNEL_TRANSACTION_STATE_RULE_VIOLATION` | 422/409 | Editar/decidir/anular fuera del ciclo; segunda decisión concurrente |
| `DECISION_NOTE_REQUIRED` / `ANNULMENT_REASON_REQUIRED` | 422 | Rechazo/anulación/revocación sin motivo |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` | 422 | Alta/edición/aplicación sobre perfil `RETIRADO` |
| `PERSONNEL_TRANSACTION_RANGE_REQUIRED` | 422 | Insumo de planilla sin rango / rango incoherente |
| `TIME_AVAILABILITY_RANGE_REQUIRED` / `TIME_AVAILABILITY_RANGE_INVALID` | 422 | Consulta de disponibilidad sin rango / rango incoherente |
| `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` / `REPORT_EXPORT_TOO_LARGE` | 400 / 413 | Formato de export inválido / demasiadas filas |

Reusados: `400`/`409` de If-Match, `403` de los gates de permiso, errores de purpose/tamaño/tipo de los adjuntos.

---

## 10. Flujo recomendado + pasos de adopción por empresa

1. **Configuración (una vez):** cargá/ajustá los 3 maestros (botón «Cargar plantilla» → `load-template`). Asociá conceptos de egreso a las causas que lo requieran (opcional).
2. **Asigná los roles:** separá «registra» (`Manage*`) de «autoriza» (`Authorize*`); asigná `ViewTimeAvailability` a planificación/jefaturas.
3. **Registrar → Decidir:** RRHH registra (`EN_REVISION`), un autorizador **distinto** aprueba/rechaza. Al aprobar, mostrá los asientos generados (`personnelActionPublicId`/`suspensionActionPublicId`).
4. **Planilla:** exportá el **insumo** por rango de quincena/mes para pasar descuentos y suspensiones a la nómina.
5. **Planificación:** usá la **consulta de disponibilidad** para ver suspensiones y fines de contrato temporal del periodo; iterá `activeSources[]` genéricamente para absorber las fuentes futuras.

---

## 11. Notas de despliegue

- **Migraciones** M1 (`AddEmployeeRelationsConfiguration`) + M2 (`AddPersonnelFileRecognitionsAndDisciplinaryActions`) aplicadas. PR-6 **no agrega migración** (la consulta es solo lectura derivada).
- **Storage:** `Storage:Purposes:RecognitionDocument` y `Storage:Purposes:DisciplinaryActionDocument` en appsettings **base** + contenedores `clarihr-recognition-documents` / `clarihr-disciplinary-action-documents` aprovisionados (config faltante → `422` en el alta de adjunto).
- **Plantilla:** `load-template` ejecutado en el tenant productivo (causas **sin** concepto por defecto — P-14) + los 7 permisos asignados a los roles.
- **openapi.yaml** regenerado (paths de maestros, reconocimientos/amonestaciones + documentos, bandejas/exports, insumo y `time-availability`).
