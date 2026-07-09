# Guía de integración Frontend — Horas extras del empleado

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-09 |
| **Rama backend** | `feature/vacaciones-incapacidades` (REQ-007 PR-1…PR-6 completos) |
| **Documentos** | `docs/business/analisis-horas-extras-empleado.md` (D-01…D-21 · P-01…P-14) · `docs/technical/plan-tecnico-horas-extras.md` |
| **Alcance** | 2 **maestros por empresa** (tipos de hora extra + tipos de justificación) con plantilla · **registro** de jornadas de horas extras (duración h:m + factor) por **canal dual** RRHH/**portal** del empleado · **autorización** con **anti-self triple** · **aplicación** unitaria + lote por periodo con posposición · reversión · **consulta** + totales EN HORAS + 3 exports + insumo de planilla · **integración con la liquidación** (línea **CALCULADA** `HORAS_EXTRAS_PENDIENTES_PAGO`) · costura con tiempo compensatorio (REQ-002) |

El módulo registra, por empleado, **jornadas de horas extras** (fecha, tipo, duración h:m, factor, justificación). En Fase 1 **no hay motor de nómina** (P-01/P-09 — «este no es el módulo de horas extras operativo; el cálculo de valor-hora operativo será otro módulo»): cubre **registro + autorización + aplicación** y expone un **insumo exportable** que consume la nómina externa. La **única monetización** es la línea **calculada** de la liquidación (§9). Convenciones de la casa:

- Prefijo `api/v1`.
- Error de negocio en `problemDetails.extensions.code` (mensajes bilingües EN/ES/es-SV en `problemDetails.detail` — mostrar tal cual).
- Concurrencia optimista con `If-Match` en **todo write** (sin header → `400`; token obsoleto → `409`; el token nuevo viaja en `ETag` y en `concurrencyToken`/`overtimeRecordConcurrencyToken` del body). El `DELETE` de borrador devuelve el token del **expediente padre** en `parentConcurrencyToken`.
- Enums/códigos como **strings**; todo `Guid XxxId` se serializa como `xxxPublicId` (p.ej. `overtimeRecordPublicId`, `overtimeTypePublicId`, `justificationTypePublicId`, `requesterFilePublicId`, `assignedPositionPublicId`, `applicationPublicId`).
- `workDate`, `appliedDate`, `payrollPeriodEndDate`, `fromWorkDate`/`toWorkDate` viajan como `date` (`"2026-07-15"`, sin hora); `startTime`/`endTime` como `time` (`"18:30:00"`, opcionales); las fechas de auditoría (`decidedUtc`, `annulledUtc`) son `date-time` UTC.
- **Duración**: se envía `durationHours` (≥ 0) + `durationMinutes` (0–59, total > 0); el backend deriva y persiste `durationDecimalHours` (2 h 30 m = `2.50`) — mostrar la decimal en tablas/sumas.

> **Sin montos en operación regular.** La jornada lleva horas × factor, **cero montos, monedas ni centros de costo persistidos** (el centro de costo del insumo se **deriva** de la plaza al exportar). El módulo **no** genera asientos de nómina ni escribe en la contabilidad (RN-20): no toca `PersonnelFilePayrollTransaction` ni `PersonnelFileCompensationConcept`. El único punto donde entra un monto es la **liquidación** (§9), y ahí es una **línea CALCULADA por el motor** (a diferencia de REQ-005/006 que son líneas sugeridas manuales).

---

## 1. Permisos (RBAC)

| Permiso (wire) | Habilita | Notas |
|---|---|---|
| `PersonnelFiles.ViewOvertimeRecords` | Leer registros, historial de aplicaciones, bandejas/exports/insumo, **leer los maestros** | `Admin` lo implica. |
| `PersonnelFiles.ManageOvertimeRecords` | Crear/editar/descartar borrador, re-imputar periodo, anular, aplicar/anular aplicación, lote por periodo, **administrar los maestros** (crear/editar/activar/inactivar + plantilla) | `Admin` lo implica. |
| `PersonnelFiles.AuthorizeOvertimeRecords` | **Resolver** (autorizar/rechazar) y **revocar** un registro | **`Admin` NO lo implica** (separación de funciones, molde `AuthorizeRetirement`). Va por su **controlador dedicado**. |

- **5 policies sobre 3 codes**: los registros (`OvertimeRecordsController`) y los maestros (`OvertimeTypes`/`OvertimeJustificationTypes`/`Configuration`) usan **View/Manage**; la **resolución** usa **View + AuthorizeOvertimeRecords**. Los maestros aplican una policy **estricta** (assertion + fallback `Admin`); los registros son **authn-only + gate por handler** (habilita el canal portal).
- **`Authorize` excluye `Admin`** (`Program.cs`): un usuario con solo `Admin` recibe `403` al resolver/revocar; se requiere el grant dedicado. Modelar un rol "autorizador" separado del "gestor".
- **Canal portal (autoservicio, P-01)**: el **empleado sujeto** (usuario vinculado a su expediente) puede **crear su propia** hora extra **si** la empresa activó `OvertimeSelfServiceEnabled` (preferencia, default **off**). Con la preferencia off → `403 OVERTIME_SELF_SERVICE_DISABLED`. Sin permiso RRHH y no-sujeto → `403`. Ver §7.
- **Anti-self TRIPLE** (resolución/revocación): ni el **empleado sujeto**, ni el **registrador** del registro, ni el **solicitante** (`requesterFilePublicId`, cuando su expediente tiene login vinculado) pueden resolver/revocar → `403 OVERTIME_SELF_APPROVAL_FORBIDDEN`. Ocultar/deshabilitar el botón de autorizar para esos tres usuarios.

---

## 2. Catálogos, maestros y máquina de estados

### 2.1 Catálogo país-scoped

`GET /api/v1/general-catalogs/{key}?countryCode=SV` — los **códigos son estructurales**:

| Wire key | Códigos | Uso |
|---|---|---|
| `overtime-record-statuses` | `EN_REVISION`, `AUTORIZADA`, `RECHAZADA`, `APLICADA`, `ANULADA` | Estado del registro. |
| `payroll-types` | `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO` | `payrollTypeCode` (catálogo compartido REQ-004/005/006; ya sembrado). |

### 2.2 Maestros por empresa (§3) — 2 catálogos administrables

- **Tipos de hora extra** (`overtime-types`): `code`, `name`, **`defaultFactor` (> 0)** (factor de referencia: HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 según el contador), `payrollEffectDescription`, `sortOrder`, `isActive`.
- **Tipos de justificación** (`overtime-justification-types`): `code`, `name`, `description`, `sortOrder`, `isActive`.
- Ambos con **plantilla** cargable (`load-template`) que siembra los 4 tipos + 6 justificaciones de referencia (idempotente por código — no pisa ediciones).

### 2.3 Estados del registro

```
Hora extra:

  EN_REVISION ──resolution: AUTORIZADA──► AUTORIZADA ──application (unitaria / lote / al emitir liquidación)──► APLICADA
       │                                     │  ▲                                                                  │
       │ resolution: RECHAZADA               │  └──────────── application annulment / settlement annul ────────────┘
       │        └─► RECHAZADA (terminal)     │        (APLICADA es REVERSIBLE → vuelve a AUTORIZADA)
       │                                     └── revocation ──► ANULADA (terminal)
       └── annulment (borrador) ──► ANULADA  └── (al emitir liquidación, las FUTURAS de la plaza → ANULADA)
       └── DELETE (borrador) ──► soft-delete (isActive=false)
```

- **Solo `EN_REVISION` es editable** (PUT) y descartable (DELETE). Una hora extra autorizada se **revoca** o se **aplica**, no se edita ni se borra.
- **Solo `AUTORIZADA`** admite aplicación (unitaria, lote y línea calculada de liquidación), re-imputación de periodo y revocación.
- **`APLICADA` NO es terminal**: anular la aplicación (o anular la liquidación que la aplicó) la **reabre** a `AUTORIZADA`. Solo `RECHAZADA` y `ANULADA` son terminales.

---

## 3. Maestros por empresa (tipos + justificaciones)

Policy **estricta** (`View`/`Manage` con fallback `Admin`). **Sin DELETE físico** — baja lógica por `activation`/`inactivation`.

| Operación | Endpoint (+ `If-Match` en write) |
|---|---|
| Listar tipos | `GET /api/v1/companies/{companyId}/overtime-types` |
| Detalle tipo | `GET /api/v1/overtime-types/{id}` |
| Crear tipo | `POST /api/v1/companies/{companyId}/overtime-types` — `{ code, name, defaultFactor, payrollEffectDescription?, sortOrder }` |
| Editar tipo | `PUT /api/v1/overtime-types/{id}` — mismo body |
| Activar / Inactivar tipo | `PATCH /api/v1/overtime-types/{id}/activation` · `PATCH …/{id}/inactivation` |
| (Justificaciones) | idem con `overtime-justification-types` — `{ code, name, description?, sortOrder }` |
| Cargar plantilla | `POST /api/v1/companies/{companyId}/overtime-configuration/load-template` |

Errores: código duplicado → `422 OVERTIME_TYPE_CODE_TAKEN` / `OVERTIME_JUSTIFICATION_TYPE_CODE_TAKEN`; no encontrado → `404 OVERTIME_TYPE_NOT_FOUND` / `OVERTIME_JUSTIFICATION_TYPE_NOT_FOUND`. Inactivar siempre se permite (los registros históricos conservan snapshot; los nuevos quedan bloqueados por el tipo inactivo → `OVERTIME_TYPE_INVALID`).

---

## 4. Registrar y editar una hora extra (sub-recurso del expediente)

Base: `api/v1/personnel-files/{publicId}/overtime-records`.

### 4.1 Registrar — `POST …/overtime-records` → `201`

```json
{
  "workDate": "2026-07-08",                 // pasada O futura (jornada organizada); tope de sanidad ≤ +366 días
  "overtimeTypePublicId": "…",              // tipo activo de la empresa → snapshot code/name/factor
  "factorApplied": null,                    // OPCIONAL: null ⇒ usa el factor del tipo; si difiere → nota obligatoria
  "factorOverrideNote": null,               // OBLIGATORIA si factorApplied ≠ factor del tipo (P-06)
  "durationHours": 2,                       // ≥ 0
  "durationMinutes": 30,                    // 0–59, total > 0 → durationDecimalHours = 2.50
  "startTime": null, "endTime": null,       // opcionales informativas ("18:30:00")
  "justificationTypePublicId": "…",         // tipo de justificación activo → snapshot
  "observations": null,
  "assignedPositionPublicId": null,         // OPCIONAL: null ⇒ plaza principal (D-12)
  "requesterFilePublicId": "…",             // OBLIGATORIO en canal RRHH (el trío); ignorado en portal
  "payrollTypeCode": "QUINCENAL",           // catálogo payroll-types
  "payrollPeriodPublicId": null,            // opcional: imputa a una instancia de periodo de planilla
  "payrollPeriodLabel": "Quincena 13/2026", // OBLIGATORIA
  "payrollPeriodEndDate": null              // opcional: habilita el marcado "Vencida"
}
```

- Nace en **`EN_REVISION`**, canal `RRHH` (o `PORTAL` en autoservicio, §7). Respuesta = `OvertimeRecordResponse` (snapshots + `durationDecimalHours` + `statusCode` + `originChannel` + `concurrencyToken`).
- **Tope diario (P-05)**: si la empresa configuró `OvertimeMaxDailyMinutes`, la suma de minutos activos (`EN_REVISION`+`AUTORIZADA`+`APLICADA`) del `(empleado, workDate)` + los nuevos no puede excederlo → `422 OVERTIME_DAILY_CAP_EXCEEDED` (el detalle trae el tope y lo ya registrado).
- Perfil `RETIRADO` → el alta se bloquea (un expediente retirado está congelado para registros nuevos).

### 4.2 Listar / obtener / editar / descartar / anular / re-imputar

| Operación | Endpoint (+ `If-Match` en write) | Notas |
|---|---|---|
| Listar | `GET …/overtime-records` | Todos los del expediente (cada ítem con su `concurrencyToken`). **Lectura self**: el sujeto lee los propios sin permiso View (P-12). |
| Detalle | `GET …/overtime-records/{overtimeRecordPublicId}` | |
| Editar | `PUT …/{overtimeRecordPublicId}` | **Solo `EN_REVISION`** — mismo body que el POST. |
| Descartar borrador | `DELETE …/{overtimeRecordPublicId}` | **Solo `EN_REVISION`** (soft-delete); devuelve `parentConcurrencyToken`. |
| Anular (retiro del trámite) | `PATCH …/{overtimeRecordPublicId}/annulment` | `EN_REVISION` → `ANULADA`; **`reason` obligatorio**. |
| Re-imputar periodo | `PATCH …/{overtimeRecordPublicId}/period` | **Solo `AUTORIZADA`** (`ManageOvertimeRecords`): re-apunta `payrollTypeCode` + periodo + etiqueta + fecha fin. No-`AUTORIZADA` → `422 OVERTIME_NOT_RETARGETABLE`. |

---

## 5. Resolución y revocación del autorizador (controlador dedicado)

Requieren el grant **`AuthorizeOvertimeRecords`** (que `Admin` **no** implica) y respetan el **anti-self triple** (§1). `If-Match` con el token del registro.

| Operación | Endpoint | Body | Efecto |
|---|---|---|---|
| Resolver | `PATCH …/{overtimeRecordPublicId}/resolution` | `{ "targetStatusCode": "AUTORIZADA" \| "RECHAZADA", "note": "…" }` | `EN_REVISION` → `AUTORIZADA` o `RECHAZADA` (**`note` obligatoria al rechazar** → `422 OVERTIME_DECISION_NOTE_REQUIRED`). |
| Revocar | `PATCH …/{overtimeRecordPublicId}/revocation` | `{ "reason": "…" }` | `AUTORIZADA` → `ANULADA` (terminal); **`reason` obligatorio**. Un `APLICADA` exige revertir la aplicación primero. |

Sujeto, registrador o solicitante-con-login (aun con el grant) → `403 OVERTIME_SELF_APPROVAL_FORBIDDEN`. Admin sin el grant → `403` (policy). `targetStatusCode` inválido → `422 OVERTIME_STATUS_INVALID`.

---

## 6. Aplicación: unitaria, lote por periodo y reversión

### 6.1 Aplicar (unitaria) — `POST …/{overtimeRecordPublicId}/applications` → `201`

```json
{ "appliedDate": null, "payrollPeriodPublicId": null, "notes": null }
```

- Registra **la única** aplicación de un registro `AUTORIZADA` **cuya `workDate` ya transcurrió** (una jornada futura no es pagable). Las horas **no viajan** (son la duración×factor del registro). `appliedDate` default hoy; `payrollPeriodPublicId` (opcional) imputa a una instancia de periodo (valida activa, FK real) y la snapshotea. Origen = `MANUAL`.
- El registro pasa a **`APLICADA`**. Respuesta = `{ application{…}, overtimeRecordStatusCode, overtimeRecordConcurrencyToken }`. **Usar `overtimeRecordConcurrencyToken` como el nuevo `If-Match`.**
- `If-Match` = token del **registro**. Errores: no `AUTORIZADA` → `422 OVERTIME_NOT_APPLICABLE`; ya con aplicación activa → `422 OVERTIME_ALREADY_APPLIED`; **fecha futura no transcurrida → `422 OVERTIME_WORK_DATE_NOT_ELAPSED`**.

### 6.2 Lote por periodo (empresa) — `POST /api/v1/companies/{companyId}/overtime-records/apply-period` → `200`

```json
{
  "payrollTypeCode": "QUINCENAL",
  "payrollPeriodPublicId": null,               // su id/etiqueta se snapshotean…
  "payrollPeriodLabel": "Quincena 13/2026",    // …o una etiqueta de override
  "excludedRecordPublicIds": ["…"]             // POSPOSICIÓN
}
```

- Aplica **atómicamente** los registros `AUTORIZADA` **con fecha transcurrida** y sin aplicación activa del `payrollTypeCode` (incluye "atrasados"); las **futuras se omiten** (informativo). Conflicto → revierte todo el lote (`422 OVERTIME_APPLY_PERIOD_CONFLICT`). Protegido por lock; dos corridas concurrentes **no duplican**. `ManageOvertimeRecords`.

### 6.3 Anular la aplicación (reversión) — `PATCH …/{overtimeRecordPublicId}/applications/{applicationPublicId}/annulment` → `200`

- `{ "reason": "…" }` (obligatorio). Aplicación `APLICADA` → `ANULADA` y el registro **reabre** a `AUTORIZADA` (puede re-aplicarse). Aplicación ya anulada → `422 OVERTIME_APPLICATION_NOT_REVERTIBLE`.

### 6.4 Historial de aplicaciones

`GET …/{overtimeRecordPublicId}/applications` → `OvertimeRecordApplicationResponse[]` (`APLICADA` + `ANULADA`, más reciente primero): `appliedDate`, `payrollTypeCode`, `payrollPeriodLabel`, `originCode` (`MANUAL`/`MOTOR`/`LIQUIDACION`), `statusCode`, `annulmentReason`, …

---

## 7. Canal portal (autoservicio del empleado, P-01)

- La empresa lo activa con `OvertimeSelfServiceEnabled = true` (preferencia, default **off**) vía **PUT** de preferencias (`PUT /api/v1/companies/{companyId}/preferences`; el PATCH no cubre estos campos escalares ricos). También `OvertimeMaxDailyMinutes` (tope diario, null = sin tope).
- Con la preferencia **on**, el **empleado sujeto** (usuario vinculado a su expediente) puede `POST …/overtime-records` sobre **su propio** expediente: se crea con `originChannel = "PORTAL"`, el **solicitante es el propio expediente** (`requesterFilePublicId` del body se ignora). Con la preferencia **off** → `403 OVERTIME_SELF_SERVICE_DISABLED`.
- El sujeto solo puede **editar/descartar** su propio borrador `EN_REVISION` de origen `PORTAL`, y **leer** los propios (P-12). Sobre expediente ajeno → `403`.
- La **decisión (resolución/revocación) siempre la toma un tercero facultado** (anti-self triple): en el canal portal, sujeto=registrador=solicitante, así que ninguno puede autorizar su propia hora extra.

---

## 8. Consulta, exportaciones e insumo de planilla (nivel empresa)

| Pantalla | Endpoint | Notas |
|---|---|---|
| Bandeja / consulta | `POST /api/v1/companies/{companyId}/overtime-records/query` | Filtros: `statusCodes[]`, `employeeId`, `overtimeTypePublicId`, `justificationTypePublicId`, `fromWorkDate`/`toWorkDate`, `payrollTypeCode`, `payrollPeriod`, `requesterFilePublicId`, **`originChannel`** (`RRHH`/`PORTAL`), `assignedPositionPublicId`, `search`, `pageNumber`/`pageSize` (1–100). Respuesta: `items[]`, `totalCount`, `statusCounts` (cubren **todos** los estados), **`totalHours`** global y **`totalsByType[]`** (`{overtimeTypeCode, overtimeTypeName, count, totalHours}`). **Los totales son EN HORAS** — el módulo no lleva dinero; **no hay groupBy dimensional**. `View`, rate-limited. |
| Export de registros | `GET …/overtime-records/export?format=xlsx\|csv\|json` + los mismos filtros | Filas con header, jornada, factor, justificación, destino y estado. |
| Bandeja de pendientes/atrasadas | `POST …/overtime-records/pending/query` — `{ payrollTypeCode?, onlyOverdue? }` | Los `AUTORIZADA` sin aplicación activa (RF-012); marca **`Vencida`** cuando el `payrollPeriodEndDate` declarado ya pasó. |
| Export de pendientes | `GET …/overtime-records/pending/export?format=…&payrollTypeCode=&onlyOverdue=` | Mismos filtros. |
| **Insumo de planilla** | `GET …/overtime-records/payroll-input/export?format=…&payrollTypeCode=&payrollPeriod=` | Registros `AUTORIZADA` **no aplicados, con fecha transcurrida y NO compensados** del **`payrollTypeCode` + `payrollPeriod` OBLIGATORIOS** — una fila por registro (empleado, tipo, horas×factor, tipo de planilla, periodo, **centro de costo derivado de la plaza**). **Puente con la nómina externa**; cuadra contra las pendientes del mismo filtro. **Falta tipo/periodo → `400 OVERTIME_PAYROLL_INPUT_FILTER_REQUIRED`.** |

Exports **síncronos**, rate-limited y con tope (`413` → sugerir filtrar); formato inválido → `400`.

---

## 9. Integración con la liquidación — **línea CALCULADA por el motor**

⚠️ **Diferencia clave con REQ-005/006**: aquí la línea la **calcula el motor** (contexto + spec + `case`), **no** es una sugerencia manual editable de monto conocido. El molde es el **tiempo compensatorio** (REQ-002).

Cuando se **genera la liquidación** de un empleado con retiro, el motor lee — **por LA PLAZA que se liquida** (cada hora extra tiene su propia plaza → sin doble pago entre plazas; a diferencia del fondo compensatorio, que es por empleado y solo plaza principal) — las horas extras **`AUTORIZADA`** de esa plaza, **no aplicadas, no compensadas** (§10) y con **`workDate ≤` la fecha de corte**, y agrega **una** línea:

- Concepto **`HORAS_EXTRAS_PENDIENTES_PAGO`** (seed `-9915`, **`isSystemCalculated = true`** ⇒ **línea CALCULADA**, no manual). `unitsOrDays` = Σ(horas × factor) (**horas-factor**), `calculationBase` = valor-hora = `Round2(salarioDiario ÷ horasEstándarDía)` (default 8 h/día; `salarioDiario = salarioMensual ÷ 30`), `calculatedAmount` = `Round2(unitsOrDays × calculationBase)`.
  - **Ejemplo (golden A.4-16):** 2.50 h × 2.00 + 1.50 h × 2.50 = **8.75** h-factor; salario diario $10 (mensual $300), 8 h/día → valor-hora **$1.25** → línea = 8.75 × 1.25 = **$10.94**. Afecta ISSS/AFP/Renta automáticamente por configuración del concepto.
- La línea llega **incluida y editable/excluible**: el liquidador puede **editar las horas-factor** (`PUT …/lines/{lineId}` con `unitsOrDays` — sobrevive recálculos), **excluirla** (`isIncluded=false`) o **regenerar** (`POST …/lines/regenerate`, re-lee las horas extras). **No** puede editar el monto directamente vía `manualAmount` de este concepto.
- **Guard anti-duplicado**: como `isSystemCalculated=true`, **agregar el concepto como línea MANUAL se rechaza** → `422 SETTLEMENT_CONCEPT_INVALID`. (Ojo: `HORAS_EXTRAS_PENDIENTES_PAGO` es de REQ-007; **no confundir** con `HORAS_EXTRAS_PENDIENTES` de REQ-002/tiempo compensatorio, también calculado.)
- **Sin horas extras `AUTORIZADA` en la plaza → sin línea** (retrocompatible).
- **Al EMITIR** la liquidación **con la línea INCLUIDA**: las horas extras `AUTORIZADA` **transcurridas** de la plaza pasan a **`APLICADA`** (origen `LIQUIDACION`) y las **FUTURAS** de la plaza (`workDate > hoy`) se **ANULAN**. **Línea EXCLUIDA → NINGÚN registro se cierra** (todos siguen `AUTORIZADA`). **Al ANULAR** la liquidación → se **reabren exactamente** los que esa liquidación cerró (aplicados **y** futuros anulados) a `AUTORIZADA`.
- **Granularidad**: la línea es **una (agregada)**; para excluir un registro puntual del finiquito, operarlo antes de liquidar (revocar/aplicar). El FE de liquidación **no requiere cambios de contrato** (la línea viaja por el canal de líneas existente).

---

## 10. Costura con tiempo compensatorio (REQ-002, RF-013)

Una hora extra puede acreditarse como **tiempo compensatorio** en vez de pagarse. El registro lleva `compensatedByCreditPublicId`: cuando una acreditación compensatoria (REQ-002) referencia una hora extra `AUTORIZADA`, ésta queda **compensada** y — por construcción — **excluida** del insumo de pago y del contexto de la liquidación (§9) (no se paga dos veces). En el FE: mostrar la insignia "compensada" en el detalle/consulta y **no** ofrecer aplicarla al pago. *(La validación del vínculo acreditación↔registro se refuerza en el handler de REQ-002; ver §12.)*

---

## 11. Tabla de errores del módulo

| `extensions.code` | HTTP | Cuándo |
|---|---|---|
| `OVERTIME_DURATION_HOURS_INVALID` / `_MINUTES_INVALID` / `_EMPTY` | 422 | Horas < 0 / minutos fuera de 0–59 / duración total 0. |
| `OVERTIME_FACTOR_INVALID` / `OVERTIME_FACTOR_NOTE_REQUIRED` | 422 | Factor ≤ 0 / factor distinto del tipo sin nota (P-06). |
| `OVERTIME_DAILY_CAP_EXCEEDED` | 422 | Suma de minutos del día > tope (P-05). |
| `OVERTIME_WORK_DATE_TOO_FAR` | 422 | `workDate` > +366 días (tope de sanidad). |
| `OVERTIME_TYPE_INVALID` / `OVERTIME_JUSTIFICATION_INVALID` | 422 | Tipo/justificación inexistente o inactivo. |
| `OVERTIME_PAYROLL_TYPE_INVALID` | 422 | `payrollTypeCode` inexistente/inactivo. |
| `OVERTIME_ASSIGNED_POSITION_INVALID` | 422 | Plaza ajena al expediente. |
| `OVERTIME_REQUESTER_INVALID` | 422 | `requesterFilePublicId` inexistente. |
| `OVERTIME_STATUS_INVALID` | 422 | `targetStatusCode` de resolución no es `AUTORIZADA`/`RECHAZADA`. |
| `OVERTIME_DECISION_NOTE_REQUIRED` | 422 | Rechazo sin `note`. |
| `OVERTIME_ANNULMENT_REASON_REQUIRED` | 422 | Anulación/revocación/reversión sin `reason`. |
| `OVERTIME_SELF_APPROVAL_FORBIDDEN` | 403 | Sujeto, registrador o solicitante-con-login resuelve/revoca (anti-self triple). |
| `OVERTIME_SELF_SERVICE_DISABLED` | 403 | Autoservicio con la preferencia off (P-01). |
| `OVERTIME_STATE_RULE_VIOLATION` | 422/409 | Operación no válida para el estado (editar/descartar un no-`EN_REVISION`, etc.). |
| `OVERTIME_NOT_RETARGETABLE` | 422 | Re-imputar periodo sobre un registro no `AUTORIZADA`. |
| `OVERTIME_NOT_APPLICABLE` / `OVERTIME_WORK_DATE_NOT_ELAPSED` / `OVERTIME_ALREADY_APPLIED` | 422 | Aplicar sobre no-`AUTORIZADA` / jornada futura no transcurrida / ya con aplicación activa. |
| `OVERTIME_APPLICATION_NOT_REVERTIBLE` / `OVERTIME_APPLICATION_NOT_FOUND` | 422/404 | Reversión de aplicación. |
| `OVERTIME_APPLICATION_PAYROLL_PERIOD_INVALID` | 422 | Periodo de la aplicación inexistente/inactivo. |
| `OVERTIME_APPLY_PERIOD_CONFLICT` | 422 | Conflicto en el lote por periodo (revierte todo el lote). |
| `OVERTIME_PAYROLL_INPUT_FILTER_REQUIRED` | 400 | Insumo sin `payrollTypeCode`/`payrollPeriod`. |
| `OVERTIME_TYPE_CODE_TAKEN` / `OVERTIME_JUSTIFICATION_TYPE_CODE_TAKEN` | 422 | Código de maestro duplicado. |
| `OVERTIME_TYPE_NOT_FOUND` / `OVERTIME_JUSTIFICATION_TYPE_NOT_FOUND` | 404 | Maestro inexistente. |
| `OVERTIME_TYPE_IN_USE` / `OVERTIME_JUSTIFICATION_TYPE_IN_USE` | 422 | (Si aplica) maestro referenciado. |
| `SETTLEMENT_CONCEPT_INVALID` | 422 | Intento de agregar `HORAS_EXTRAS_PENDIENTES_PAGO` como línea MANUAL en la liquidación (concepto de sistema). |
| `REPORT_EXPORT_TOO_LARGE` / `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` | 413 / 400 | Export. |
| `CONCURRENCY_CONFLICT` / (sin If-Match) | 409 / 400 | Convenciones de concurrencia. |

---

## 12. Flujo recomendado + pasos de adopción por empresa

**Adopción (una vez por empresa):**
1. Verificar el catálogo país sembrado (`overtime-record-statuses`; `payroll-types` ya existe de módulos previos).
2. **Cargar la plantilla** de maestros (`load-template`) o crearlos a mano — **confirmar con el contador los factores** (HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 + posible «día de descanso» Art. 175) antes de producción.
3. Configurar preferencias: `OvertimeSelfServiceEnabled` (**piloto recomendado**, default off) y `OvertimeMaxDailyMinutes` (opcional) vía **PUT** de preferencias.
4. Asignar los **3 permisos** a los roles: `View`/`Manage` al gestor RRHH; `AuthorizeOvertimeRecords` a un rol **autorizador distinto** (`Admin` NO lo cubre).

**Pantallas:**
1. **Maestros**: tipos de hora extra (con factor) + tipos de justificación; activar/inactivar; cargar plantilla.
2. **Hora extra (per-file)**: alta con tipo + duración h:m (mostrar la decimal derivada) + factor (con nota si difiere) + justificación + plaza opcional (default principal) + solicitante; jornadas **futuras** permitidas (no aplicables hasta transcurrir). Editar/descartar solo en `EN_REVISION`. **Canal portal** para el empleado sujeto si la preferencia está on.
3. **Autorización**: cola de `EN_REVISION` para el rol autorizador (autorizar/rechazar con nota); revocar `AUTORIZADA`. **Ocultar el botón para sujeto/registrador/solicitante** (anti-self triple).
4. **Aplicación (per-file)**: aplicar el `AUTORIZADA` transcurrido; anular la aplicación (reabre); historial.
5. **Lote por periodo (empresa)**: elegir tipo de planilla, marcar exclusiones (posposición), ejecutar; las futuras se omiten.
6. **Consulta/exports**: registros (chips por `statusCounts`, **totales en horas** + por tipo), pendientes/atrasadas, e **insumo de planilla** (tipo+periodo obligatorios) para la nómina externa.
7. **Liquidación**: la línea `HORAS_EXTRAS_PENDIENTES_PAGO` aparece **calculada** (editable en horas-factor / excluible) por la plaza; al emitir **incluida**, aplica las transcurridas y anula las futuras; al anular, reabre. **PDF/impresión de reportes = FRONTEND** (el backend no genera PDF de horas extras).

> **Costura REQ-002**: en el detalle mostrar la insignia "compensada" (`compensatedByCreditPublicId` no nulo) y excluir esos registros del pago; ya se excluyen del insumo y del contexto de liquidación.

---

## 13. Notas de despliegue

- **Sin storage** (P-10): el módulo no lleva adjuntos; no requiere `purposes` ni contenedores.
- **Migraciones** (aplicar en orden): **M1** `AddOvertimeConfiguration` (`20260709192609` — 2 maestros + catálogo país `overtime-record-statuses` + 2 columnas de preferencias), **M2** `AddOvertimeRecords` (`20260709201156` — tablas de dominio: registro + aplicación, índices/CHECKs + índice único parcial de aplicación activa), **M3** `AddOvertimeSettlementConcept` (`20260709222254` — concepto de liquidación **`HORAS_EXTRAS_PENDIENTES_PAGO = -9915`**, **`IsSystemCalculated=true`**; habilita la **línea calculada** — retrocompatible, no cambia la superficie HTTP de liquidación).
- Los **3 permisos** (`View`/`Manage`/`AuthorizeOvertimeRecords`) se agregan al catálogo de aprovisionamiento; **asignarlos a los roles** (recordar: `AuthorizeOvertimeRecords` fuera de `Admin`).
- `payroll-types` es catálogo **compartido** (ya sembrado si un REQ previo se desplegó primero; no duplicar).
- Todos los mensajes llegan localizados (EN/ES/es-SV) en `problemDetails.detail`. La integración con liquidación es **puramente lógica** (contexto + spec + `case` calculado + hooks emisión/anulación): **no cambia el contrato** de la API de liquidación.
