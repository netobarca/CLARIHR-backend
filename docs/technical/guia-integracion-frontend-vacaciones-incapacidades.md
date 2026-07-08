# Guía de integración Frontend — Vacaciones e Incapacidades

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-08 |
| **Rama backend** | `feature/vacaciones-incapacidades` (PR-1…PR-10, Olas 1 y 2 completas) |
| **Documentos** | `docs/business/analisis-vacaciones-incapacidades-empleado.md` (D-01…D-27) · `docs/technical/plan-tecnico-vacaciones-incapacidades.md` |
| **Alcance** | Incapacidades (motor de días + montos ISSS, prórrogas, autoservicio, constancia) · Lactancia · Fondo anual de vacaciones por empleado · Plan anual + calendario · Solicitudes con decisión y devolución · Maestros por empresa · Bandejas/exports · Saldos en el perfil · Integración con la liquidación |

El módulo agrega la configuración por empresa (clínicas, riesgos ISSS, tipos, asuetos, periodos de planilla), el registro de **incapacidades** con su motor de cálculo, **lactancia**, el **fondo de vacaciones** (periodos + solicitudes + plan), y las bandejas/exports. Todas las convenciones de la casa aplican:

- Prefijo `api/v1`.
- Error de negocio en `problemDetails.extensions.code` (mensajes bilingües EN/ES en `problemDetails.detail` — mostrar tal cual).
- Concurrencia optimista con `If-Match` en **todo write** de sub-recurso (sin header → `400`; token obsoleto → `409`; el token nuevo viaja en el header `ETag` y en `concurrencyToken` del body). Los `DELETE` de periodos llevan el token en `If-Match`.
- Enums/códigos como **strings**; todo `Guid XxxId` se serializa como `xxxPublicId`.
- Fechas de negocio (inicio/fin de incapacidad, periodo, horario) viajan como `date` (`"2026-07-15"`, sin hora); las fechas de auditoría/decisión son `date-time` UTC.

> **Montos referenciales (convención salario/30).** Todos los importes del módulo (subsidio, descuento, patrono, provisión) usan **salario mensual ÷ 30** como salario diario. Son **referenciales** para insumo de planilla y provisión financiera; la planilla real reconcilia. Documentarlo en la UI donde se muestren montos (tooltip "cálculo referencial /30").

---

## 1. Permisos (RBAC)

| Permiso | Habilita | Notas |
|---|---|---|
| `PersonnelFiles.LeaveConfiguration.Read` | Ver los 5 maestros de configuración | `Admin` lo implica. |
| `PersonnelFiles.LeaveConfiguration.Admin` | Crear/editar/activar/inactivar maestros + `load-template` | `Admin` lo implica. |
| `PersonnelFiles.ViewIncapacities` | Leer incapacidades (per-file y bandeja), balance, export, lactancia | `Admin` lo implica. Lectura de un expediente ajeno: **View OR es el propio empleado** (dato de salud — 403 sin enmascarar). |
| `PersonnelFiles.ManageIncapacities` | Crear/editar/confirmar/cerrar/anular incapacidad, prórrogas, adjuntos, **toda** la lactancia | `Admin` lo implica. Cubre lactancia (solo RRHH, D-18). |
| `PersonnelFiles.ViewVacations` | Leer fondo, saldos, solicitudes, plan, calendario, bandeja/export | `Admin` lo implica. Lectura propia sin permiso: **View OR es el propio empleado**. |
| `PersonnelFiles.ManageVacations` | Periodos, generación masiva, decisión, devolución, plan anual | `Admin` lo implica. |

**Autoservicio** (el empleado logueado, vinculado a su expediente): puede **crear su propia incapacidad** (queda `EN_REVISION`) y **crear/anular su propia solicitud** de vacaciones (`SOLICITADA`) sin permiso de gestión. Confirmar/decidir/devolver es **solo RRHH** y con **anti-autoacción** (no puede confirmar su propia incapacidad ni decidir/devolver su propia solicitud → `403`).

---

## 2. Catálogos y máquinas de estado

Catálogos país-scoped (SV sembrado) vía `GET /api/v1/general-catalogs/{key}` — los **códigos son estructurales** (el nombre es i18n/editable; el código nunca):

| Wire key | Códigos |
|---|---|
| `incapacity-statuses` | `EN_REVISION`, `REGISTRADA`, `ANULADA` |
| `vacation-request-statuses` | `SOLICITADA`, `APROBADA`, `RECHAZADA`, `ANULADA`, `DEVUELTA_PARCIAL`, `DEVUELTA` |
| `clinic-sectors` | `ISSS`, `PUBLICA`, `PRIVADA` |

```
Incapacidad:   EN_REVISION ──confirmation──► REGISTRADA ──annulment──► ANULADA (terminal)
(autoservicio) EN_REVISION ──annulment──────────────────────────────► ANULADA
(RRHH crea)    ────────────────────────────► REGISTRADA (directo)
   Solo REGISTRADA consume el tope patronal y entra al insumo de planilla.

Solicitud vac.: SOLICITADA ─decision(approve)─► APROBADA ─returns─► DEVUELTA_PARCIAL ─returns─► DEVUELTA
                    │                              (LIFO; auto-DEVUELTA al agotar)
                    ├─decision(reject)─► RECHAZADA (terminal)
                    └─cancellation────► ANULADA (terminal; self mientras SOLICITADA)
```

---

## 3. Configuración por empresa (pantalla "Configuración de ausencias")

5 maestros **governed** (familia `[ResourceActions]`): cada respuesta incluye `allowedActions[]` (usar para habilitar botones), `concurrencyToken` e `isActive`. Baja **lógica** (`activation`/`inactivation`, sin DELETE físico); un maestro referenciado por un registro activo no se puede inactivar (`LEAVE_MASTER_IN_USE`). Permiso: `LeaveConfiguration.Read` (GET) / `LeaveConfiguration.Admin` (write). If-Match en todo write.

| Maestro | Base (company-scoped) | Campos clave |
|---|---|---|
| Clínicas | `…/companies/{companyId}/medical-clinics` | `description` (req.), `specialty`, `sectorCode` (`clinic-sectors`). **Inicia vacío**; la clínica es **opcional** en la incapacidad. |
| Riesgos ISSS | `…/companies/{companyId}/incapacity-risks` | `code`, `name`, flags (`countsSeventhDay/Saturday/Holiday`, `usesWorkSchedule`, `allowsIndefinite`, `allowsExtension`, `usesFund`, `hasSubsidy`). **Tramos**: `PUT …/incapacity-risks/{id}/parameters` reemplaza el set completo (`dayFrom`≥1, `dayTo` null=∞, `subsidyPercent` 0–100, `payerCode` `ISSS`/`EMPRESA`/`SIN_PAGO`, `sortOrder`) — **contiguos desde 1 sin solapes** o `RISK_PARAMETERS_INVALID`. |
| Tipos de incapacidad | `…/companies/{companyId}/incapacity-types` | `code`, `name`, textos informativos, `appliesToWorkAccident`. La plantilla incluye `LACTANCIA` (lo consume el módulo de lactancia). |
| Asuetos | `…/companies/{companyId}/company-holidays` | `date` (única por año), `description`, `scopeCode` (`NACIONAL`/`LOCAL`/`INSTITUCIONAL`). |
| Periodos de planilla | `…/companies/{companyId}/payroll-periods` | `payPeriodTypeCode` (`PAY_PERIOD_CATALOG`), `year`, `number`, `label`, `startDate`, `endDate`. **Sin plantilla** — cada empresa carga sus quincenas (sin solape por tipo/año). |

**Carga inicial de la plantilla SV** — `POST /api/v1/companies/{companyId}/leave-configuration/load-template?year=YYYY` (permiso `LeaveConfiguration.Admin`). Idempotente por código/fecha: crea faltantes de riesgos (A.2), tipos (incl. `LACTANCIA`) y asuetos del año, **sin pisar ediciones**. Se ejecuta una vez al adoptar y una vez por año para los asuetos. Clínicas y periodos de planilla **no** tienen plantilla.

---

## 4. Preferencias de la empresa + descanso semanal de la plaza

- **Preferencias** — `GET /api/v1/companies/{companyId}/preferences` y `PUT …/preferences` (reemplazo; el `PATCH` sigue siendo scalar-only para moneda/zona — los policies de ausencias van por el **PUT**). Campos nuevos (todos anulables; `null` = default legal):
  `annualVacationDaysDefault` (15), `additionalVacationBenefitDaysDefault` (0), `allowVacationStartOnHoliday` (false), `allowVacationEndOnHoliday` (false), `allowVacationStartOnRestDay` (false), `defaultUseAnniversary` (true), `companyRestDayOfWeek` (domingo), `employerCoveredIncapacityDaysPerYear` (9), `additionalIncapacityBenefitDaysPerYear` (0), `incapacityRequiresDocument` (true).
- **Descanso semanal por plaza (séptimo día)** — el contrato de assignments gana `restDayOfWeek` (`int` 0–6, 0 = domingo; nullable). Viaja en `Create/Update` y en el **JSON Patch** del assignment y en la respuesta. Es la fuente del "séptimo día" del motor de incapacidades y del Art. 178 de vacaciones (plaza → preferencia `companyRestDayOfWeek` → domingo).

---

## 5. Incapacidades (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/incapacities`. Gate de creación: **Manage OR es el propio empleado** (autoservicio ⇒ `origin=AUTOSERVICIO` ⇒ `EN_REVISION`). Lecturas: **View OR propio**.

### 5.1 Registrar — `POST …/incapacities` → `201`

```json
{
  "riskPublicId": "…",                        // FK dura al maestro de riesgos
  "incapacityTypePublicId": "…",
  "medicalClinicPublicId": null,              // opcional (maestro puede estar vacío)
  "assignedPositionPublicId": null,           // opcional; null ⇒ plaza principal (salario + restDay)
  "payrollTypeCode": null,
  "payrollPeriodDefinitionPublicId": null,    // opcional (imputación; sin validación de contención)
  "startDate": "2026-07-01",
  "endDate": "2026-07-05",                    // null ⇒ indefinida (solo si el riesgo lo permite)
  "notes": null,
  "documentFilePublicId": "…",                // CONSTANCIA — file ya subido con purpose=IncapacityDocument
  "documentTypeCatalogItemPublicId": null,
  "documentObservations": null
}
```

- **Constancia obligatoria** si `incapacityRequiresDocument` (default sí): sin `documentFilePublicId` → `422 INCAPACITY_DOCUMENT_REQUIRED`. Subir el archivo primero (flujo de archivos con `purpose=IncapacityDocument`), luego enviar su `publicId` aquí; se asocia en la misma transacción.
- La respuesta trae el **desglose calculado**: `calendarDays`, `computableDays`, `subsidizedDays/discountDays/employerDays` (días **y montos** `subsidyAmount/discountAmount/employerAmount`), `monthlyBaseSalary`, `dailySalary`, y `trancheDetail[]` (por tramo: rango absoluto de la cadena, `%`, pagador, días, monto — para la tabla auditable). `statusCode`, `originCode`, `isActive`, `concurrencyToken`.
- El motor recalcula el desglose en **cada** escritura de campos de cálculo (crear/editar/cerrar); no hay `regenerate`.

### 5.2 Ciclo

| Operación | Endpoint (+ `If-Match`) | Notas |
|---|---|---|
| Editar | `PUT …/{id}` | Recalcula. Editar fechas con prórrogas posteriores vigentes → `422 INCAPACITY_CHAIN_LOCKED` (anular las posteriores primero). |
| Confirmar (RRHH) | `PATCH …/{id}/confirmation` (sin body) | `EN_REVISION → REGISTRADA`; recalcula contra el tope **disponible al confirmar**; journalea `INCAPACIDAD`. Anti-self: confirmar la propia → `403 INCAPACITY_CONFIRM_SELF_FORBIDDEN`. |
| Cerrar indefinida | `PATCH …/{id}/closure` — `{ "endDate": "2026-08-01" }` | Solo si `endDate == null`. Fija la fecha y calcula el desglose. |
| Anular | `PATCH …/{id}/annulment` — `{ "reason": "…" }` (obligatorio) | Desde `EN_REVISION`/`REGISTRADA`. **Revierte el consumo del tope.** |
| Prórroga | `POST …/{id}/extensions` — body como el POST pero **sin `startDate`** (derivado = fin del origen + 1), con `endDate` | Solo si el riesgo permite extensión y el origen es `REGISTRADA` no anulada; la cadena **continúa la numeración** de tramos (no reinicia). Journalea `PRORROGA_INCAPACIDAD`. |

### 5.3 Balance y documentos

- `GET …/personnel-files/{id}/incapacity-balance?year=YYYY` → acumulado del año, tope ley/política, beneficio, **restante** (misma fórmula que el saldo del perfil — cuadran por construcción).
- Documentos: `GET/POST …/incapacities/{id}/documents`, `DELETE …/documents/{docId}`, `GET …/documents/{docId}/read-url` (URL temporal de descarga). POST body: `{ filePublicId, documentTypeCatalogItemPublicId?, observations? }`.

### 5.4 Autoservicio

El empleado logueado crea la suya (`EN_REVISION`, no consume tope, **no aparece** en el export default de planilla). RRHH la confirma (`REGISTRADA`, consume). Un intento de crear sobre otro expediente → `403`; sin constancia (si obligatoria) → `422`.

---

## 6. Lactancia (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/lactation-periods`. **Solo RRHH** (`ManageIncapacities`; lecturas View OR propio). `IncapacityTypePublicId` debe referir el tipo activo `LACTANCIA`.

| Operación | Endpoint (+ `If-Match`) |
|---|---|
| Listar / crear | `GET/POST …/lactation-periods` |
| Editar (reemplaza datos + horarios) | `PUT …/{id}` — el set completo de `schedules` viaja siempre y se reemplaza atómicamente |
| Anular | `PATCH …/{id}/annulment` — `{ "reason": "…" }` |

Body: `{ incapacityTypePublicId, startDate, endDate, notes?, schedules: [{ startDate, endDate, dailyPermitsCount, minutesPerPermit }] }`. Cada horario debe estar **contenido** en el periodo (`LACTATION_SCHEDULE_OUT_OF_RANGE`) y **no solaparse** con otro (`LACTATION_SCHEDULE_OVERLAP`). Al crear journalea `LACTANCIA`.

---

## 7. Fondo de vacaciones (sub-recurso + acción de empresa)

### 7.1 Periodos del fondo — `…/personnel-files/{id}/vacation-periods`

| Operación | Endpoint (+ `If-Match`) | Notas |
|---|---|---|
| Listar / crear | `GET/POST …/vacation-periods` | Body: `{ periodYear, useAnniversary?, legalDaysGranted?, benefitDaysGranted?, generatesEnjoymentDays? }` (grants default = preferencia). Un periodo activo duplicado por año → `422 VACATION_PERIOD_DUPLICATE`. |
| Editar grants | `PUT …/{id}` — `{ legalDaysGranted, benefitDaysGranted }` | **Solo sin consumo** → `422 VACATION_PERIOD_HAS_CONSUMPTION`. |
| Borrar (soft) | `DELETE …/{id}` (`If-Match` = token) | Solo sin consumo. |
| Detalle del fondo | `GET …/vacation-fund` | Por periodo: `totalDaysGranted`, `enjoyedDays`, `pendingDays`, `provisionAmount` (`pendientes × diario × 1.30`); totales `dailySalary`, `totalPendingDays`, `totalProvisionAmount`. |

### 7.2 Generación masiva — `POST /api/v1/companies/{companyId}/vacation-periods/generate`

Body: `{ year, useAnniversary?, legalDaysGranted?, benefitDaysGranted?, generatesEnjoymentDays?, employeeIds? }`. **Idempotente** (2.ª corrida = 0 creados). Resumen: `{ totalEmployees, created, skipped, errors: [{ personnelFilePublicId, code }] }` — inelegibles (Art. 177, < 1 año) llegan como `VACATION_ELIGIBILITY_NOT_MET` por fila, no rompen la corrida.

### 7.3 Export de provisión (Finanzas) — `GET /api/v1/companies/{companyId}/vacation-fund/export?format=xlsx|csv|json&year=YYYY`

Fila por empleado con otorgados ley/beneficio, gozados, pendientes, salario diario y provisión.

---

## 8. Solicitudes de vacaciones (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/vacation-requests`. Crear/anular: **Manage OR propio**; decidir/devolver: **Manage + anti-self**.

| Operación | Endpoint (+ `If-Match` en los write) | Body / notas |
|---|---|---|
| Crear | `POST …/vacation-requests` → `201` | `{ startDate, endDate, requestedDays, planLinePublicId?, notes? }`. Valida **Art. 178** (no iniciar en asueto/descanso ni terminar en asueto, salvo que la preferencia lo permita), solape con solicitud viva/incapacidad, y **disponibilidad de fondo**. |
| Listar / detalle | `GET …/vacation-requests` · `GET …/{id}` | |
| Decidir | `PATCH …/{id}/decision` | `{ approve: true, allocations?: [{ vacationPeriodPublicId, days }], notes? }` — aprobar con asignaciones editables (default **FIFO**; Σ = `requestedDays` o `VACATION_ALLOCATION_MISMATCH`); re-verifica el fondo dentro de la tx; journalea `GOCE_VACACIONES`. `{ approve: false, notes? }` = rechazar. |
| Anular | `PATCH …/{id}/cancellation` | Solo `SOLICITADA` (self permitido). |
| Devolver | `POST …/{id}/returns` | `{ days, reason?, distribution?: [{ vacationPeriodPublicId, days }] }` — total/parcial, reversa **LIFO** editable; tope = consumido restante (`VACATION_RETURN_EXCEEDS_CONSUMED`); auto `DEVUELTA_PARCIAL → DEVUELTA` al agotar; journalea `DEVOLUCION_VACACIONES`. |

El **saldo del perfil** (`vacationDaysAvailable`, ver §11) cuadra por construcción con las asignaciones − devoluciones.

---

## 9. Plan anual + calendario (nivel empresa)

- **Plan anual** — `GET/POST /api/v1/companies/{companyId}/vacation-plans`, `PUT …/{id}` (reemplaza líneas), `PATCH …/{id}/annulment`. Body: `{ planYear, lines: [{ personnelFilePublicId, startDate, endDate, days }] }`. Escritura `ManageVacations`, lectura `ViewVacations`.
  - El plan es **indicativo**: POST/PUT devuelven `warnings[]` **por línea** (`VACATION_PLAN_WARNING_INSUFFICIENT_FUND`, `VACATION_PLAN_WARNING_DATE_RULE`) sin bloquear. Mostrarlos como avisos, no errores.
  - Sí **bloquea** el solape de líneas del mismo empleado (`422 VACATION_PLAN_LINE_OVERLAP`), empleado ajeno (`VACATION_PLAN_EMPLOYEE_INVALID`) y plan no vigente (`VACATION_PLAN_STATE_RULE_VIOLATION`).
- **Calendario** — `GET /api/v1/companies/{companyId}/vacations/calendar?year=YYYY` → por empleado: goces de solicitudes `APROBADA/DEVUELTA_PARCIAL/DEVUELTA` que solapan el año + líneas de planes `VIGENTE`.

---

## 10. Bandejas y exportaciones

| Pantalla | Query (`POST`) | Export (`GET`) |
|---|---|---|
| Incapacidades | `POST /api/v1/companies/{companyId}/incapacities/query` — `{ employeeId?, statusCode?, …, pageNumber, pageSize }`; `statusCounts` sobre todos los estados. **Default `statusCode=REGISTRADA`** (excluye `EN_REVISION` del insumo de planilla). | `GET …/incapacities/export?format=xlsx\|csv\|json` — filas con días **y montos**, % por tramo, base mensual/diaria, tipo de planilla + periodo. |
| Solicitudes de vacaciones | `POST /api/v1/companies/{companyId}/vacation-requests/query` — `{ employeeId?, statusCode?, startFromUtc?, startToUtc?, pageNumber?, pageSize? }`; `statusCounts`. | `GET …/vacation-requests/export?format=…` — goces `APROBADA/DEVUELTA_PARCIAL/DEVUELTA` con periodos de origen. |
| Provisión del fondo | — | `GET …/vacation-fund/export?format=…&year=YYYY` (§7.3). |

Exports **síncronos**, rate-limited y con tope (`413 REPORT_EXPORT_TOO_LARGE` → sugerir filtrar); formato inválido → `400 PERSONNEL_FILE_EXPORT_FORMAT_INVALID`.

---

## 11. Saldos en el perfil e integración con la liquidación

- `GET /api/v1/personnel-files/{id}/employment-information` ahora **puebla** dos campos ya existentes en el contrato:
  - `vacationDaysAvailable` = Σ pendientes de periodos activos con goce (otorgados − aprobado-no-devuelto). **`null`** si el módulo aún no tiene fondo para el empleado (mostrar "—", no 0).
  - `disabilityDaysAvailable` = `(tope + beneficio) − Σ días patrono` de incapacidades `REGISTRADA` del año en curso.
- **Liquidación (RF-019).** Al generar la liquidación de un empleado con retiro, la línea **`VACACION_PROPORCIONAL`** ahora **sugiere los días pendientes del fondo** en lugar del cálculo por aniversario (`DaysSinceAnniversary`). Sin fondo → mantiene el comportamiento anterior (retrocompatible). El liquidador puede **sobreescribir** las unidades de la línea (edición manual auditada) y ese override **sobrevive** los recálculos; una **regeneración** completa vuelve a leer el fondo. Cambio **interno del motor**: no altera el contrato de la API de liquidación (el FE de liquidación no requiere cambios).

---

## 12. Tabla de errores del módulo

| `extensions.code` | HTTP | Cuándo |
|---|---|---|
| `INCAPACITY_RISK_INVALID` / `INCAPACITY_TYPE_INVALID` / `INCAPACITY_CLINIC_INVALID` / `INCAPACITY_PAYROLL_PERIOD_INVALID` | 422 | Referencia inexistente/inactiva. |
| `INCAPACITY_OVERLAP` | 422 | Solape con incapacidad activa del empleado. |
| `INCAPACITY_END_DATE_REQUIRED` | 422 | `endDate` nula con riesgo sin `allowsIndefinite`. |
| `INCAPACITY_DOCUMENT_REQUIRED` | 422 | Alta sin constancia con la preferencia activa. |
| `INCAPACITY_BASE_SALARY_MISSING` | 422 | Sin `SALARIO_BASE` resoluble en la plaza. |
| `INCAPACITY_EXTENSION_NOT_ALLOWED` / `…_NOT_CONTIGUOUS` / `…_SOURCE_INVALID` | 422 | Prórroga inválida. |
| `INCAPACITY_CHAIN_LOCKED` | 422 | Editar fechas con prórrogas posteriores vigentes. |
| `INCAPACITY_STATE_RULE_VIOLATION` | 422/409 | Transición inválida (confirmar no-`EN_REVISION`, cerrar no-indefinida, editar/anular anulada). |
| `INCAPACITY_CONFIRM_SELF_FORBIDDEN` | **403** | El empleado confirma su propia incapacidad. |
| `INCAPACITY_OVERRIDE_NOTE_REQUIRED` | 422 | Ajuste de días computables sin nota. |
| `LACTATION_SCHEDULE_OUT_OF_RANGE` / `LACTATION_SCHEDULE_OVERLAP` / `LACTATION_TYPE_INVALID` / `LACTATION_STATE_RULE_VIOLATION` | 422 | Horario fuera de rango / solapado / tipo ≠ LACTANCIA / estado. |
| `VACATION_PERIOD_DUPLICATE` | 422 | `(expediente, año)` ya existe. |
| `VACATION_PERIOD_HAS_CONSUMPTION` | 422 | Editar/borrar periodo con consumo. |
| `VACATION_ELIGIBILITY_NOT_MET` | 422 / fila | < 1 año de servicio (Art. 177). |
| `VACATION_FUND_INSUFFICIENT` | 422 | Días solicitados > disponibles (al crear y al aprobar). |
| `VACATION_REQUEST_OVERLAP` / `VACATION_INCAPACITY_OVERLAP` | 422 | Solape con solicitud viva / incapacidad activa. |
| `VACATION_START_ON_HOLIDAY_FORBIDDEN` / `VACATION_START_ON_REST_DAY_FORBIDDEN` / `VACATION_END_ON_HOLIDAY_FORBIDDEN` | 422 | Art. 178 (en el plan anual = warning). |
| `VACATION_ALLOCATION_MISMATCH` | 422 | Σ asignaciones ≠ días solicitados. |
| `VACATION_DECISION_SELF_FORBIDDEN` | **403** | Decidir/devolver la solicitud del propio expediente. |
| `VACATION_STATE_RULE_VIOLATION` / `VACATION_RETURN_EXCEEDS_CONSUMED` | 422/409 | Estado inválido / devolución > consumido. |
| `VACATION_PLAN_LINE_OVERLAP` / `VACATION_PLAN_EMPLOYEE_INVALID` / `VACATION_PLAN_STATE_RULE_VIOLATION` / `VACATION_PLAN_NOT_FOUND` | 422/404 | Plan anual. |
| `LEAVE_MASTER_IN_USE` / `RISK_PARAMETERS_INVALID` / `PAYROLL_PERIOD_OVERLAP` / `HOLIDAY_DUPLICATE` | 422 | Maestros. |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` | 422 | Cualquier alta sobre un perfil `RETIRADO`. |
| `REPORT_EXPORT_TOO_LARGE` / `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` | 413 / 400 | Export. |
| `CONCURRENCY_CONFLICT` / (sin If-Match) | 409 / 400 | Convenciones de concurrencia. |
| **Warnings** (no bloquean): `INCAPACITY_WARNING_CAP_EXHAUSTED` · `VACATION_PLAN_WARNING_INSUFFICIENT_FUND` · `VACATION_PLAN_WARNING_DATE_RULE` | — | En la respuesta de cálculo / del plan. |

---

## 13. Flujo recomendado + pasos de adopción por empresa

**Adopción (una vez por empresa, roles `LeaveConfiguration.Admin` / `Admin`):**
1. `POST …/leave-configuration/load-template?year=YYYY` → siembra riesgos (con tramos), tipos (incl. `LACTANCIA`) y asuetos del año.
2. Cargar las **clínicas** (maestro inicia vacío — opcional).
3. Cargar los **periodos de planilla** (quincenas) del año — no hay plantilla.
4. Configurar las **preferencias** de ausencias (§4) y el `restDayOfWeek` de las plazas.
5. (Anual) volver a correr `load-template` para los asuetos del nuevo año, y `vacation-periods/generate?year=` para abrir el fondo.

**Pantallas:**
1. **Configuración de ausencias**: tabs de los 5 maestros (`allowedActions[]` gobierna botones) + botón "Cargar plantilla SV".
2. **Incapacidades** (per-file + autoservicio): alta con **constancia obligatoria** (subir archivo → enviar `documentFilePublicId`), tabla del desglose por tramo, acciones por estado (§5), balance del año. Autoservicio: alta que queda `EN_REVISION` a la espera de confirmación RRHH.
3. **Lactancia**: periodo + editor de horarios (reemplazo atómico).
4. **Fondo de vacaciones**: generación masiva (resumen creados/omitidos/errores), detalle con provisión, export Finanzas.
5. **Solicitudes**: alta (autoservicio/RRHH, valida Art. 178 + fondo) → decisión (asignaciones FIFO editables) → devolución total/parcial (LIFO). Anti-self en decisión/devolución.
6. **Plan anual + calendario**: editor de líneas con `warnings[]` por línea; vista de calendario del año.
7. **Bandejas** (incapacidades / solicitudes): chips por `statusCounts`, filtros, export.

---

## 14. Notas de despliegue

- Migraciones incluidas (aplicar en orden): **M1** `AddLeaveConfigurationMasters`, **M2** `AddLeaveStatusCatalogsPermissionsAndPreferences`, **M3** `AddPersonnelFileIncapacitiesAndLactation`, **M4** `AddVacationFundAndRequests`. PR-10 **no** agrega migración (la integración con liquidación es solo lógica).
- `Storage:Purposes:IncapacityDocument` debe estar en **appsettings base** (tamaño + content-types pdf/jpg/png) y el contenedor de blobs `clarihr-incapacity-documents` **pre-aprovisionado** (config faltante → `422` en el alta con constancia).
- Ejecutar `load-template` en el tenant productivo; la empresa carga sus **periodos de planilla** (quincenas) y **clínicas**, y configura las **preferencias**.
- Los 6 permisos se agregan al catálogo de aprovisionamiento; **asignarlos a los roles** correspondientes.
- Todos los mensajes llegan localizados (EN/ES/es-SV) en `problemDetails.detail`.
