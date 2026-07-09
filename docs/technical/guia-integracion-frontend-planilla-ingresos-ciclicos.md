# Guía de integración Frontend — Planilla: ingresos cíclicos

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-09 |
| **Rama backend** | `feature/vacaciones-incapacidades` (REQ-005 PR-1…PR-6 completos) |
| **Documentos** | `docs/business/analisis-planilla-ingresos-ciclicos.md` (D-01…D-20 · P-01…P-15) · `docs/technical/plan-tecnico-planilla-ingresos-ciclicos.md` |
| **Alcance** | Registro de **ingresos cíclicos** (prestaciones permanentes pagadas en cuotas: ayuda de alimentación, gastos de representación, combustible…) · autorización con anti-self doble · plan de cuotas finito/indefinido · aplicación unitaria + lote por periodo con posposición · proyección (schedule) e historial · bandejas/exports + insumo de planilla externa · integración con la liquidación (línea sugerida `INGRESO_CICLICO_PENDIENTE`) |

El módulo registra, por empleado, un **ingreso que se paga en cuotas** a lo largo del tiempo. En Fase 1 **no hay motor de nómina** (P-01 — "el motor se realizará aparte"): el módulo cubre el **registro + autorización + aplicación manual de cuotas por periodo** y expone un **insumo exportable** que consume la nómina externa. Convenciones de la casa:

- Prefijo `api/v1`.
- Error de negocio en `problemDetails.extensions.code` (mensajes bilingües EN/ES/es-SV en `problemDetails.detail` — mostrar tal cual).
- Concurrencia optimista con `If-Match` en **todo write** (sin header → `400`; token obsoleto → `409`; el token nuevo viaja en `ETag` y en `concurrencyToken`/`recurringIncomeConcurrencyToken` del body). El `DELETE` de borrador devuelve el token del **expediente padre** en `parentConcurrencyToken`.
- Enums/códigos como **strings**; todo `Guid XxxId` se serializa como `xxxPublicId` (p.ej. `recurringIncomePublicId`, `registeredByUserPublicId`, `decidedByUserPublicId`, `closedByUserPublicId`, `costCenterPublicId`).
- `registrationDate` e `installmentStartDate` viajan como `date` (`"2026-07-15"`, sin hora); las fechas de auditoría (`decidedUtc`, `suspendedUtc`, `closedUtc`, `annulledUtc`) son `date-time` UTC; los rangos de bandeja/registro (`registeredFromUtc`/`registeredToUtc`) son `date-time`.

> **Sin montos en operación regular.** El plan lleva importes (valor de cuota / total), pero el módulo **no** genera asientos de nómina ni escribe en la contabilidad; los importes son el insumo que la nómina externa reconcilia. El único punto donde un monto entra a un cálculo es la **liquidación** (§9), y ahí es una **línea sugerida editable**.

---

## 1. Permisos (RBAC)

| Permiso (wire) | Habilita | Notas |
|---|---|---|
| `PersonnelFiles.ViewRecurringIncomes` | Leer ingresos, schedule, historial, bandejas/exports/insumo | `Admin` lo implica. **HR-only, sin autoservicio** (P-11). |
| `PersonnelFiles.ManageRecurringIncomes` | Crear/editar/descartar borrador, suspender/reanudar, cerrar indefinido, aplicar/anular cuotas, lote por periodo | `Admin` lo implica. |
| `PersonnelFiles.AuthorizeRecurringIncomes` | **Resolver** (autorizar/rechazar) y **revocar** un ingreso | **`Admin` NO lo implica** (separación de funciones, molde `AuthorizeRetirement`). Va por su **controlador dedicado**. |

- **Sin autoservicio en F1** (P-11): no hay lectura ni escritura por el empleado dueño; todas las operaciones exigen permiso RRHH sobre el expediente.
- **`Authorize` excluye `Admin`** (`Program.cs`): un usuario con solo `Admin` recibe `403` al resolver/revocar; se requiere el grant dedicado. Modelar en el FE un rol "autorizador" separado del "gestor".
- **Anti-self doble** (RN sobre resolución/revocación): ni el **empleado sujeto** (usuario vinculado al expediente) ni el **registrador** del ingreso pueden resolver o revocar → `403 RECURRING_INCOME_SELF_APPROVAL_FORBIDDEN`. El botón de autorizar debe ocultarse/deshabilitarse para esos dos usuarios.

---

## 2. Catálogos y máquina de estados

Catálogos país-scoped (SV sembrado) vía `GET /api/v1/general-catalogs/{key}?countryCode=SV` — los **códigos son estructurales** (el nombre es i18n/editable):

| Wire key | Códigos | Uso |
|---|---|---|
| `recurring-income-statuses` | `EN_REVISION`, `VIGENTE`, `RECHAZADO`, `SUSPENDIDO`, `FINALIZADO`, `ANULADO` | Estado del ingreso. |
| `recurring-income-settlement-actions` | `PAGAR_SALDO`, `CANCELAR` | Qué hacer con el saldo al liquidar (§9). |
| `recurring-income-types` | `AYUDA_ALIMENTACION`, `GASTOS_REPRESENTACION`, `COMBUSTIBLE`, `OTRO` | **Tipo de ingreso cíclico** (prestación permanente, independiente del salario) — `recurringIncomeTypeCode`. |
| `payroll-types` | `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO` | Tipo de planilla — `payrollTypeCode` (catálogo compartido REQ-004; ya sembrado). |
| `compensation-concept-types` | (por empresa; filtrar `nature = Ingreso`) | **Tipo de ingreso** / concepto de compensación — `conceptTypeCode`. Se snapshotea el nombre en `conceptNameSnapshot`. |
| `pay-periods` | `MENSUAL`, `QUINCENAL`, `SEMANAL`, `UNICA`… | **Frecuencia de cuota** — `installmentFrequencyCode`. |

```
Ingreso cíclico:

  EN_REVISION ──resolution: VIGENTE──► VIGENTE ──closure (indefinido) / plan completo (finito)──► FINALIZADO (terminal)
       │                                  │
       │ resolution: RECHAZADO            ├── suspension: suspend=true ──► SUSPENDIDO ──suspend=false──► VIGENTE
       │        └─► RECHAZADO (terminal)  │
       │                                  └── revocation ──► ANULADO (terminal)
       └── annulment (borrador) ──► ANULADO (terminal)
       └── DELETE (borrador) ──► soft-delete (isActive=false)
```

- **Solo `EN_REVISION` es editable** (PUT header+plan) y descartable (DELETE). Un ingreso autorizado se **revoca** o **cierra**, no se edita ni se borra.
- **Solo `VIGENTE`** admite aplicación de cuotas, suspensión, cierre manual (indefinido) y revocación.
- La aplicación de la **última** cuota de un plan finito lleva el ingreso a `FINALIZADO`; **anular** esa cuota lo **reabre** a `VIGENTE` (§6).
- Al **emitir la liquidación** del empleado, sus ingresos `VIGENTE` pasan a `FINALIZADO` (§9); **anular** la liquidación los reabre a `VIGENTE`.

---

## 3. Registrar y editar un ingreso cíclico (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/recurring-incomes`. **HR-only** (`ManageRecurringIncomes` para write, `ViewRecurringIncomes` para read).

### 3.1 Registrar — `POST …/recurring-incomes` → `201`

```json
{
  "registrationDate": "2026-07-09",             // ≤ hoy (RECURRING_INCOME_REGISTRATION_DATE_IN_FUTURE)
  "reference": "AYUDA-ALIM-2026",               // libre, opcional (etiqueta de la línea en la liquidación)
  "recurringIncomeTypeCode": "AYUDA_ALIMENTACION", // catálogo recurring-income-types
  "conceptTypeCode": "BONO",                    // concepto de compensación Nature=Ingreso (activo) → snapshot de nombre
  "observations": null,
  "assignedPositionPublicId": null,             // OPCIONAL: null ⇒ plaza principal (P-15)
  "installmentStartDate": "2026-07-15",         // fecha de la 1ª cuota teórica
  "currencyCode": "USD",
  "payrollTypeCode": "MENSUAL",                 // catálogo payroll-types
  "installmentFrequencyCode": "MENSUAL",        // catálogo pay-periods
  "isIndefinite": false,
  "installmentValue": 50,                       // valor de cada cuota (> 0)
  "installmentCount": 6,                         // finito: número de cuotas…
  "totalAmount": null,                           // …o total; se da UNO y se deriva el otro
  "settlementActionCode": "PAGAR_SALDO"         // PAGAR_SALDO | CANCELAR
}
```

- **Plan finito**: `isIndefinite=false` y se envía **`installmentCount` o `totalAmount`** (uno; el faltante se deriva; enviar los dos coherentes también vale). En el ejemplo: `50 × 6 = 300` → `totalAmount=300`, `installmentCount=6`. Si `installmentValue` no divide exacto el total, **la última cuota absorbe el ajuste** (p.ej. valor 33.33 + total 100 → cuotas 33.33 / 33.33 / **33.34**).
- **Plan indefinido**: `isIndefinite=true`, **`installmentCount` y `totalAmount` deben ser null** (`RECURRING_INCOME_PLAN_INDEFINITE_WITH_LIMITS` si viaja alguno) y `settlementActionCode` **debe ser `CANCELAR`** (`RECURRING_INCOME_SETTLEMENT_ACTION_INDEFINITE` si es `PAGAR_SALDO` — no hay "saldo" que pagar sin total).
- **P-15 centro de costo obligatorio y ligado a la plaza**: la plaza (default principal) debe tener **centro de costo**; se **deriva y snapshotea** en `costCenterPublicId` + `costCenterNameSnapshot`. Plaza sin centro de costo → `422 RECURRING_INCOME_COST_CENTER_MISSING`. Plaza ajena al expediente → `422 RECURRING_INCOME_ASSIGNED_POSITION_INVALID`.
- Nace en **`EN_REVISION`**. Respuesta = `RecurringIncomeResponse` (header + plan normalizado + `statusCode` + `concurrencyToken`); el `ETag` lleva el token inicial. Un intento sobre un perfil `RETIRADO` → `422 EMPLOYEE_PROFILE_RETIRED_LOCKED`.

### 3.2 Listar / obtener / editar / descartar

| Operación | Endpoint (+ `If-Match` en write) | Notas |
|---|---|---|
| Listar | `GET …/recurring-incomes` | Todos los del expediente (cada ítem con su `concurrencyToken`). |
| Detalle | `GET …/recurring-incomes/{recurringIncomePublicId}` | |
| Editar header + plan | `PUT …/{recurringIncomePublicId}` | **Solo `EN_REVISION`** (RN-02) — mismo body que el POST. Autorizado ⇒ `422 RECURRING_INCOME_STATE_RULE_VIOLATION`. |
| Descartar borrador | `DELETE …/{recurringIncomePublicId}` | **Solo `EN_REVISION`** (soft-delete `isActive=false`); devuelve `parentConcurrencyToken`. |

---

## 4. Resolución y revocación del autorizador (controlador dedicado)

Requieren el grant **`AuthorizeRecurringIncomes`** (que `Admin` **no** implica) y respetan el **anti-self doble** (§1). `If-Match` con el token del ingreso.

| Operación | Endpoint | Body | Efecto |
|---|---|---|---|
| Resolver | `PATCH …/{recurringIncomePublicId}/resolution` | `{ "targetStatusCode": "VIGENTE" \| "RECHAZADO", "note": "…" }` | `EN_REVISION` → `VIGENTE` (autorizar) o `RECHAZADO` (rechazar; **`note` obligatoria** → `422 RECURRING_INCOME_DECISION_NOTE_REQUIRED`). |
| Revocar | `PATCH …/{recurringIncomePublicId}/revocation` | `{ "reason": "…" }` | `VIGENTE` → `ANULADO` (terminal); **`reason` obligatorio**. |

Respuesta = `RecurringIncomeResponse` con el estado nuevo y `decidedByUserPublicId`/`decidedUtc`/`decisionNote` poblados. Sujeto o registrador (aun con el grant) → `403 RECURRING_INCOME_SELF_APPROVAL_FORBIDDEN`. Admin sin el grant → `403` (policy).

---

## 5. Suspensión, cierre manual y anulación de borrador (Manage)

| Operación | Endpoint (+ `If-Match`) | Body | Notas |
|---|---|---|---|
| Suspender / reanudar | `PATCH …/{id}/suspension` | `{ "suspend": true, "note": "…" }` / `{ "suspend": false, "note": null }` | `VIGENTE` ⇄ `SUSPENDIDO` (P-03). Un ingreso `SUSPENDIDO` **no** entra al lote por periodo. |
| Cerrar indefinido | `PATCH …/{id}/closure` | `{ "reason": "…" }` | Solo INDEFINIDO `VIGENTE` → `FINALIZADO`; **`reason` obligatorio** (`RECURRING_INCOME_CLOSURE_REASON_REQUIRED`). Un finito finaliza solo al completar el plan. |
| Anular borrador | `PATCH …/{id}/annulment` | `{ "reason": "…" }` | Solo `EN_REVISION` → `ANULADO`; **`reason` obligatorio**. (Un `VIGENTE` se revoca por el autorizador, §4.) |

---

## 6. Cuotas: aplicación unitaria, lote por periodo y anulación

### 6.1 Aplicar la siguiente cuota (unitaria) — `POST …/{id}/installments` → `201`

```json
{ "appliedDate": null, "payrollPeriodPublicId": null, "notes": null }
```

- Aplica **la siguiente** cuota de un ingreso `VIGENTE` (RF-006). El **número y el monto los deriva el motor** — **no editables** (P-04). `appliedDate` default hoy; `payrollPeriodPublicId` (opcional) imputa la cuota a una instancia de periodo de planilla (valida activa → `RECURRING_INCOME_INSTALLMENT_PAYROLL_PERIOD_INVALID`) y snapshotea id + etiqueta.
- Respuesta = `RecurringIncomeInstallmentApplicationResult`: `{ installment{…}, recurringIncomeStatusCode, recurringIncomeConcurrencyToken }`. **Usar `recurringIncomeConcurrencyToken` como el nuevo `If-Match` del ingreso.** La última cuota lleva el ingreso a `FINALIZADO`.
- `If-Match` = token del **ingreso**. Errores: exceder el plan → `RECURRING_INCOME_INSTALLMENT_EXCEEDS_PLAN`; ingreso no `VIGENTE` → `…_INSTALLMENT_NOT_APPLICABLE`; plan ya completo → `…_INSTALLMENT_PLAN_COMPLETE`.

### 6.2 Lote por periodo (empresa) — `POST /api/v1/companies/{companyId}/recurring-incomes/apply-period` → `200`

```json
{
  "payrollTypeCode": "MENSUAL",
  "payrollPeriodPublicId": null,                 // su fecha-fin es el corte y su id/etiqueta se snapshotean…
  "cutoffDate": "2026-07-31",                     // …o un corte "pelón"
  "excludedIncomePublicIds": ["…"]                // POSPOSICIÓN: quedan pendientes para el próximo lote
}
```

- Aplica **atómicamente** toda cuota vencida (incluidas las **atrasadas** de periodos previos) de los ingresos `VIGENTE` del `payrollTypeCode` hasta el corte (RF-007). Cualquier conflicto **revierte todo el lote** (`422 RECURRING_INCOME_APPLY_PERIOD_CONFLICT`). HR-only (`ManageRecurringIncomes`).
- **Posposición = exclusión**: los ids en `excludedIncomePublicIds` **no** se aplican y siguen vencidos → se aplican en el siguiente lote sin exclusión (efecto "enviar a otro periodo").
- Respuesta: `{ aplicadas, finalizados, pospuestas }` (cuotas aplicadas · ingresos finalizados · ingresos pospuestos). El lote está protegido por lock; dos corridas concurrentes del mismo filtro no duplican (una `200`, la otra puede devolver `200`/`422` sin duplicar la cuota).

### 6.3 Anular una cuota aplicada — `PATCH …/{id}/installments/{installmentPublicId}/annulment` → `200`

- `{ "reason": "…" }` (**obligatorio**). `APLICADA` → `ANULADA`. Si esto deja **incompleto** un plan finito, el ingreso **reabre** (`FINALIZADO` → `VIGENTE`) y el número puede **re-aplicarse** (índice único parcial: solo cuenta la cuota activa). Respuesta = `RecurringIncomeInstallmentApplicationResult` (usar su `recurringIncomeConcurrencyToken`).

---

## 7. Proyección (schedule) e historial

| Pantalla | Endpoint | Respuesta |
|---|---|---|
| Schedule (proyección DERIVADA) | `GET …/{id}/schedule` | `{ statusCode, isIndefinite, installmentValue, installmentCount, totalAmount, remainingAmount, isPlanComplete, nextInstallmentNumber, installments:[{ installmentNumber, theoreticalDueDate, amount, isApplied, … }] }`. **Nunca persistido** (D-07): cuotas aplicadas + proyectadas + atrasadas con saldo corrido. |
| Historial de cuotas | `GET …/{id}/installments?pageNumber=&pageSize=` | Página de `RecurringIncomeInstallmentResponse` (`APLICADA` + `ANULADA`, actividad más reciente primero): `installmentNumber`, `appliedDate`, `theoreticalDueDate`, `amount`, `payrollPeriodLabel`, `originCode` (`MANUAL`/`MOTOR`), `statusCode`, `annulmentReason`, … |

En un plan indefinido `installmentCount`/`totalAmount`/`remainingAmount` son `null` (mostrar "—", no 0).

---

## 8. Bandejas, exportaciones e insumo de planilla (nivel empresa)

| Pantalla | Endpoint | Notas |
|---|---|---|
| Bandeja de ingresos (query) | `POST /api/v1/companies/{companyId}/recurring-incomes/query` — `{ employeeId?, statusCode?, recurringIncomeTypeCode?, payrollTypeCode?, registeredFromUtc?, registeredToUtc?, pageNumber?, pageSize? }` | Devuelve `items[]`, `totalCount` y `statusCounts` (cubren **todos** los estados). Sin `statusCode` se listan todos (incluidos `ANULADO`/`RECHAZADO`). Rate-limited. |
| Export de ingresos | `GET …/recurring-incomes/export?format=xlsx\|csv\|json` + los mismos filtros como query-string | Filas con header del plan, acción de liquidación, estado, moneda y los ids de registrador/decisor. |
| Bandeja de cuotas pendientes/atrasadas | `POST …/recurring-incomes/pending-installments/query` — `{ payrollTypeCode?, payrollPeriodPublicId?, cutoffDate?, startDate?, employeeId?, pageNumber?, pageSize? }` | Cuotas **teóricas** de ingresos `VIGENTE` con vencimiento ≤ corte y **no aplicadas** (RF-011, aproximación F1 del backlog "transacciones no aplicadas en planilla"). Marca las **atrasadas** (`theoreticalDueDate < hoy`). El corte = fin del periodo / `cutoffDate` / hoy. |
| Export de cuotas pendientes | `GET …/recurring-incomes/pending-installments/export?format=…` | Mismos filtros; con fecha teórica, monto, moneda y flag de atraso. |
| **Insumo de planilla** (RF-012) | `GET …/recurring-incomes/payroll-input/export?format=…&payrollTypeCode=&startDate=&endDate=` | Cuotas **`APLICADA` activas** del rango **`startDate`..`endDate` OBLIGATORIO** (sobre la fecha de aplicación); una fila por cuota (empleado, concepto, tipo de planilla, periodo imputado, fecha, número, monto, moneda, centro de costo). Es el **puente con la nómina externa** mientras no existe el motor interno; cuadra exactamente contra las pendientes del mismo filtro una vez aplicadas. **Falta un extremo del rango → `422 RECURRING_INCOME_PAYROLL_INPUT_RANGE_REQUIRED`.** |

Exports **síncronos**, rate-limited y con tope (`413 REPORT_EXPORT_TOO_LARGE` → sugerir filtrar); formato inválido → `400 PERSONNEL_FILE_EXPORT_FORMAT_INVALID`. Todo lo de empresa es insumo RRHH (sin autoservicio).

---

## 9. Integración con la liquidación

Cuando se **genera la liquidación** de un empleado con retiro, el motor lee sus ingresos cíclicos y — **solo en la plaza principal** (el ingreso es por empleado, la liquidación es por plaza; se evita doble sugerencia en retiros multi-plaza) — agrega una sugerencia por cada ingreso **`VIGENTE` con `settlementActionCode = PAGAR_SALDO`**:

- Concepto **`INGRESO_CICLICO_PENDIENTE`** (seed `-9888`, **`isSystemCalculated = false`** ⇒ es una **línea MANUAL sugerida**, no calculada por el motor — como `OTRO_INGRESO`). `conceptCode = "INGRESO_CICLICO_PENDIENTE"`, `description` = la `reference` del ingreso (o el `conceptNameSnapshot`), `calculatedAmount`/`finalAmount` = **saldo del plan** = `totalAmount − Σ cuotas APLICADA activas`. Saldo ≤ 0 → **sin línea**.
- La línea llega **incluida y editable/excluible**: el liquidador puede **cambiar el monto** (`PUT …/lines/{lineId}` con `manualAmount`) o **excluirla** (`isIncluded=false`). Una **regeneración** de líneas (`POST …/lines/regenerate`) descarta ajustes y **vuelve a leer el saldo** del plan.
- **`CANCELAR` → sin línea** (no se paga saldo). **`SUSPENDIDO`/no principal/sin saldo → sin línea** (retrocompatible: la liquidación existente no cambia).
- **Al EMITIR** la liquidación, **todos** los ingresos `VIGENTE` del empleado pasan a `FINALIZADO` (un empleado liquidado deja de devengar cíclicos — aplica a `PAGAR_SALDO` y a `CANCELAR` por igual). **Al ANULAR** la liquidación, se **reabren exactamente** los ingresos que esa liquidación cerró (`FINALIZADO` → `VIGENTE`).
- El FE de liquidación **no requiere cambios de contrato** (la sugerencia viaja por el canal de líneas existente). Recomendado: mostrar en la boleta/pantalla el origen "ingreso cíclico" y permitir editar/excluir la línea como cualquier ingreso manual.

---

## 10. Tabla de errores del módulo

| `extensions.code` | HTTP | Cuándo |
|---|---|---|
| `RECURRING_INCOME_TYPE_INVALID` | 422 | `recurringIncomeTypeCode` inexistente/inactivo. |
| `RECURRING_INCOME_CONCEPT_INVALID` | 422 | `conceptTypeCode` no es un concepto de compensación activo `Nature=Ingreso`. |
| `RECURRING_INCOME_PAYROLL_TYPE_INVALID` | 422 | `payrollTypeCode` inexistente/inactivo. |
| `RECURRING_INCOME_FREQUENCY_INVALID` | 422 | `installmentFrequencyCode` inexistente/inactivo. |
| `RECURRING_INCOME_STATUS_INVALID` | 422 | `targetStatusCode` de resolución no es `VIGENTE`/`RECHAZADO`. |
| `RECURRING_INCOME_ASSIGNED_POSITION_INVALID` | 422 | Plaza ajena al expediente. |
| `RECURRING_INCOME_COST_CENTER_MISSING` | 422 | La plaza (default principal) no tiene centro de costo (P-15). |
| `RECURRING_INCOME_PLAN_INDEFINITE_WITH_LIMITS` | 422 | Plan indefinido con `installmentCount`/`totalAmount`. |
| `RECURRING_INCOME_PLAN_FINITE_WITHOUT_LIMITS` | 422 | Plan finito sin `installmentCount` ni `totalAmount`. |
| `RECURRING_INCOME_PLAN_INCOHERENT` / `_PLAN_VALUE_INVALID` / `_PLAN_COUNT_INVALID` / `_PLAN_TOTAL_INVALID` | 422 | Valor/número/total incoherentes o ≤ 0. |
| `RECURRING_INCOME_SETTLEMENT_ACTION_INDEFINITE` | 422 | `PAGAR_SALDO` en un plan indefinido (P-06). |
| `RECURRING_INCOME_REGISTRATION_DATE_IN_FUTURE` | 422 | `registrationDate` > hoy. |
| `RECURRING_INCOME_DECISION_NOTE_REQUIRED` | 422 | Rechazo sin `note`. |
| `RECURRING_INCOME_CLOSURE_REASON_REQUIRED` | 422 | Cierre manual sin `reason`. |
| `RECURRING_INCOME_ANNULMENT_REASON_REQUIRED` | 422 | Anulación de borrador sin `reason`. |
| `RECURRING_INCOME_SELF_APPROVAL_FORBIDDEN` | 403 | Sujeto o registrador resuelve/revoca (anti-self doble). |
| `RECURRING_INCOME_STATE_RULE_VIOLATION` | 422/409 | Operación no válida para el estado (editar un no-`EN_REVISION`, suspender un no-`VIGENTE`, etc.). |
| `RECURRING_INCOME_INSTALLMENTS_APPLIED` | 422 | Editar/descartar un ingreso con cuotas ya aplicadas. |
| `RECURRING_INCOME_INSTALLMENT_NOT_APPLICABLE` | 422 | Aplicar cuota sobre un ingreso no `VIGENTE`. |
| `RECURRING_INCOME_INSTALLMENT_EXCEEDS_PLAN` / `_INSTALLMENT_PLAN_COMPLETE` | 422 | La cuota excede el plan / plan ya completo. |
| `RECURRING_INCOME_INSTALLMENT_SEQUENCE_INVALID` | 422 | Número de cuota fuera de secuencia. |
| `RECURRING_INCOME_INSTALLMENT_PAYROLL_PERIOD_INVALID` | 422 | `payrollPeriodPublicId` inexistente/inactivo. |
| `RECURRING_INCOME_INSTALLMENT_ANNULMENT_REASON_REQUIRED` | 422 | Anular cuota sin `reason`. |
| `RECURRING_INCOME_INSTALLMENT_NOT_FOUND` | 404 | Cuota inexistente. |
| `RECURRING_INCOME_APPLY_PERIOD_CONFLICT` | 422 | Conflicto en el lote por periodo (revierte todo el lote). |
| `RECURRING_INCOME_PAYROLL_INPUT_RANGE_REQUIRED` | 422 | Insumo de planilla sin `startDate`/`endDate`. |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` | 422 | Cualquier escritura del módulo sobre un perfil `RETIRADO`. |
| `REPORT_EXPORT_TOO_LARGE` / `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` | 413 / 400 | Export. |
| `CONCURRENCY_CONFLICT` / (sin If-Match) | 409 / 400 | Convenciones de concurrencia. |

---

## 11. Flujo recomendado + pasos de adopción por empresa

**Adopción (una vez por empresa):**
1. Verificar los catálogos país sembrados (`recurring-income-statuses/-settlement-actions/-types`, `payroll-types`); `compensation-concept-types` (Nature=Ingreso) y `pay-periods` ya existen de módulos previos.
2. Asegurar que las **plazas tengan centro de costo** (P-15) — sin él el alta falla.
3. Asignar los **3 permisos** a los roles: `View`/`Manage` al gestor RRHH; `AuthorizeRecurringIncomes` a un rol **autorizador distinto** del gestor (separación de funciones; `Admin` NO lo cubre).

**Pantallas:**
1. **Ingreso cíclico (per-file)**: alta con tipo + concepto + plan (finito valor+número/total o indefinido) + acción de liquidación; plaza opcional (default principal) con centro de costo derivado (solo lectura). Editar/descartar solo en `EN_REVISION`.
2. **Autorización**: cola de `EN_REVISION` para el rol autorizador (autorizar/rechazar con nota); revocar `VIGENTE`. Ocultar el botón para sujeto/registrador.
3. **Ciclo `VIGENTE`**: suspender/reanudar; cerrar indefinido con motivo.
4. **Cuotas (per-file)**: schedule (proyección con saldo corrido) + historial; aplicar la siguiente cuota (monto no editable); anular cuota con motivo (reabre plan finito).
5. **Lote por periodo (empresa)**: elegir tipo de planilla + corte, marcar exclusiones (posposición), ejecutar → resumen `{ aplicadas, finalizados, pospuestas }`.
6. **Bandejas/exports**: ingresos (chips por `statusCounts`), cuotas pendientes/atrasadas, e **insumo de planilla** (rango obligatorio) para la nómina externa.
7. **Liquidación**: la línea `INGRESO_CICLICO_PENDIENTE` aparece sugerida (editable/excluible) cuando el ingreso es `PAGAR_SALDO` en la plaza principal.

---

## 12. Notas de despliegue

- **Sin storage** (el módulo no lleva adjuntos — P-07): no requiere `purposes` ni contenedores de blobs.
- **Migraciones** (aplicar en orden): **M1** `AddRecurringIncomeCatalogs` (catálogos país `recurring-income-statuses/-settlement-actions/-types` + `payroll-types`, HasData SV), **M2** `AddRecurringIncomes` (tablas de dominio: ingreso + cuotas, con índice único parcial de número de cuota), **M3** `AddRecurringIncomeSettlementConcept` (concepto de liquidación **`INGRESO_CICLICO_PENDIENTE = -9888`**, `IsSystemCalculated=false`; habilita la sugerencia en la liquidación — **retrocompatible**, no cambia la superficie HTTP de liquidación).
- Los **3 permisos** (`View`/`Manage`/`AuthorizeRecurringIncomes`) se agregan al catálogo de aprovisionamiento; **asignarlos a los roles** (recordar: `AuthorizeRecurringIncomes` fuera de `Admin`).
- `payroll-types` es catálogo **compartido con REQ-004** (ya sembrado si REQ-004 se desplegó primero; la primera migración que lo defina lo siembra — no duplicar).
- Todos los mensajes llegan localizados (EN/ES/es-SV) en `problemDetails.detail`. La integración con liquidación es **puramente lógica** (canal de sugerencias + hooks emisión/anulación): no cambia el contrato de la API de liquidación.
