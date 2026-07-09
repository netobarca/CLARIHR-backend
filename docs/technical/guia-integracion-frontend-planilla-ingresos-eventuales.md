# Guía de integración Frontend — Planilla: ingresos eventuales

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-09 |
| **Rama backend** | `feature/vacaciones-incapacidades` (REQ-006 PR-1…PR-6 completos) |
| **Documentos** | `docs/business/analisis-planilla-ingresos-eventuales.md` (D-01…D-20 · P-01…P-15) · `docs/technical/plan-tecnico-planilla-ingresos-eventuales.md` |
| **Alcance** | Registro de **ingresos eventuales** (pagos de una sola vez: horas extra, comisiones, bonos ocasionales…) · autorización con **anti-self triple** · valor fijo o por factores · aplicación unitaria + lote por periodo con posposición · reversión de la aplicación · bandejas/exports + insumo de planilla externa · integración con la liquidación (línea sugerida `INGRESO_EVENTUAL_PENDIENTE`) |

El módulo registra, por empleado, un **ingreso que se paga una sola vez**. En Fase 1 **no hay motor de nómina** (P-01): el módulo cubre el **registro + autorización + aplicación (manual unitaria o por lote)** y expone un **insumo exportable** que consume la nómina externa. Convenciones de la casa:

- Prefijo `api/v1`.
- Error de negocio en `problemDetails.extensions.code` (mensajes bilingües EN/ES/es-SV en `problemDetails.detail` — mostrar tal cual).
- Concurrencia optimista con `If-Match` en **todo write** (sin header → `400`; token obsoleto → `409`; el token nuevo viaja en `ETag` y en `concurrencyToken`/`oneTimeIncomeConcurrencyToken` del body). El `DELETE` de borrador devuelve el token del **expediente padre** en `parentConcurrencyToken`.
- Enums/códigos como **strings**; todo `Guid XxxId` se serializa como `xxxPublicId` (p.ej. `oneTimeIncomePublicId`, `requesterFilePublicId`, `costCenterPublicId`, `applicationPublicId`).
- `incomeDate` y `appliedDate` viajan como `date` (`"2026-07-15"`, sin hora); las fechas de auditoría (`decidedUtc`, `annulledUtc`) son `date-time` UTC; los rangos de bandeja (`fromDate`/`toDate`) son `date`.

> **Sin montos en operación regular.** El ingreso lleva un importe, pero el módulo **no** genera asientos de nómina ni escribe en la contabilidad (RN-14/RN-16): el importe es el insumo que la nómina externa reconcilia. El único punto donde un monto entra a un cálculo es la **liquidación** (§9), y ahí es una **línea sugerida editable**.

---

## 1. Permisos (RBAC)

| Permiso (wire) | Habilita | Notas |
|---|---|---|
| `PersonnelFiles.ViewOneTimeIncomes` | Leer ingresos, historial de aplicaciones, bandejas/exports/insumo | `Admin` lo implica. **HR-only, sin autoservicio**. |
| `PersonnelFiles.ManageOneTimeIncomes` | Crear/editar/descartar borrador, re-imputar periodo, anular borrador, aplicar/anular aplicación, lote por periodo | `Admin` lo implica. |
| `PersonnelFiles.AuthorizeOneTimeIncomes` | **Resolver** (autorizar/rechazar) y **revocar** un ingreso | **`Admin` NO lo implica** (separación de funciones, molde `AuthorizeRetirement`). Va por su **controlador dedicado**. |

- **Sin autoservicio en F1**: todas las operaciones exigen permiso RRHH sobre el expediente.
- **`Authorize` excluye `Admin`** (`Program.cs`): un usuario con solo `Admin` recibe `403` al resolver/revocar; se requiere el grant dedicado. Modelar en el FE un rol "autorizador" separado del "gestor".
- **Anti-self TRIPLE** (RN sobre resolución/revocación): ni el **empleado sujeto** (usuario vinculado al expediente), ni el **registrador** del ingreso, ni el **solicitante** (`requesterFilePublicId`, cuando su expediente tiene login vinculado) pueden resolver o revocar → `403 ONE_TIME_INCOME_SELF_APPROVAL_FORBIDDEN`. El botón de autorizar debe ocultarse/deshabilitarse para esos tres usuarios.

---

## 2. Catálogos y máquina de estados

Catálogos país-scoped (SV sembrado) vía `GET /api/v1/general-catalogs/{key}?countryCode=SV` — los **códigos son estructurales** (el nombre es i18n/editable):

| Wire key | Códigos | Uso |
|---|---|---|
| `one-time-income-statuses` | `EN_REVISION`, `AUTORIZADO`, `RECHAZADO`, `APLICADO`, `ANULADO` | Estado del ingreso. |
| `payroll-types` | `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO` | Tipo de planilla — `payrollTypeCode` (catálogo compartido REQ-004; ya sembrado). |
| `compensation-concept-types` | (por empresa; filtrar `nature = Ingreso`) | **Tipo de ingreso** / concepto de compensación — `conceptTypeCode`. Se snapshotea el nombre en `conceptNameSnapshot`. No admite el salario base (D-03). |

```
Ingreso eventual:

  EN_REVISION ──resolution: AUTORIZADO──► AUTORIZADO ──application (unitaria / lote / al emitir liquidación)──► APLICADO
       │                                     │  ▲                                                                  │
       │ resolution: RECHAZADO               │  └──────────── application annulment / settlement annul ────────────┘
       │        └─► RECHAZADO (terminal)     │        (APLICADO es REVERSIBLE → vuelve a AUTORIZADO)
       │                                     └── revocation ──► ANULADO (terminal)
       └── annulment (borrador) ──► ANULADO (terminal)
       └── DELETE (borrador) ──► soft-delete (isActive=false)
```

- **Solo `EN_REVISION` es editable** (PUT) y descartable (DELETE). Un ingreso autorizado se **revoca** o se **aplica**, no se edita ni se borra.
- **Solo `AUTORIZADO`** admite aplicación (unitaria, lote y sugerencia de liquidación), re-imputación de periodo y revocación.
- **`APLICADO` NO es terminal**: anular la aplicación (o anular la liquidación que lo aplicó) lo **reabre** a `AUTORIZADO` (§6, §9). Solo `RECHAZADO` y `ANULADO` son terminales.

---

## 3. Registrar y editar un ingreso eventual (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/one-time-incomes`. **HR-only** (`ManageOneTimeIncomes` para write, `ViewOneTimeIncomes` para read).

### 3.1 Registrar — `POST …/one-time-incomes` → `201`

**Valor fijo** (`isFixedValue = true`): se envía `amount` y **ningún** componente/método.

```json
{
  "incomeDate": "2026-07-09",                   // ≤ hoy (ONE_TIME_INCOME_INCOME_DATE_IN_FUTURE)
  "reference": "COMISION-2026",                 // libre, opcional (etiqueta de la línea en la liquidación)
  "conceptTypeCode": "COMISION",                // concepto de compensación Nature=Ingreso (activo) → snapshot de nombre
  "observations": null,
  "isFixedValue": true,
  "calculationMethod": null, "quantity": null, "unitValue": null, "multiplier": null,
  "percentage": null, "baseAmount": null,
  "amount": 150,                                // > 0
  "currencyCode": "USD",                        // omitible → default de la empresa
  "assignedPositionPublicId": null,             // OPCIONAL: null ⇒ plaza principal (P-15)
  "requesterFilePublicId": "…",                 // OBLIGATORIO (el trío) → snapshot de nombre
  "payrollTypeCode": "QUINCENAL",               // catálogo payroll-types
  "payrollPeriodPublicId": null,                // opcional: imputa a una instancia de periodo de planilla
  "payrollPeriodLabel": "Quincena 13/2026",
  "payrollPeriodEndDate": null
}
```

**Valor por factores** (`isFixedValue = false`): se envía `calculationMethod` con sus componentes; el `amount` lo **deriva el backend** (envíalo `null` o coherente).

- `calculationMethod = "CANTIDAD_POR_VALOR"`: `quantity > 0` × `unitValue > 0` × `multiplier` (default `1.00`). No lleva `percentage`/`baseAmount`.
- `calculationMethod = "PORCENTAJE_SOBRE_BASE"`: `percentage > 0` sobre `baseAmount > 0`. No lleva `quantity`/`unitValue`/`multiplier`.

- **P-15 centro de costo obligatorio y ligado a la plaza**: la plaza (default principal) debe tener **centro de costo**; se **deriva y snapshotea** en `costCenterPublicId` + `costCenterNameSnapshot`. Plaza sin centro de costo → `422 ONE_TIME_INCOME_COST_CENTER_MISSING`. Plaza ajena al expediente → `422 ONE_TIME_INCOME_ASSIGNED_POSITION_INVALID`.
- Nace en **`EN_REVISION`**. Respuesta = `OneTimeIncomeResponse` (header + valor normalizado + `statusCode` + `concurrencyToken`); el `ETag` lleva el token inicial. Un intento sobre un perfil `RETIRADO` → `422 EMPLOYEE_PROFILE_RETIRED_LOCKED`.

### 3.2 Listar / obtener / editar / descartar

| Operación | Endpoint (+ `If-Match` en write) | Notas |
|---|---|---|
| Listar | `GET …/one-time-incomes` | Todos los del expediente (cada ítem con su `concurrencyToken`). |
| Detalle | `GET …/one-time-incomes/{oneTimeIncomePublicId}` | |
| Editar | `PUT …/{oneTimeIncomePublicId}` | **Solo `EN_REVISION`** — mismo body que el POST. Autorizado ⇒ `422 ONE_TIME_INCOME_STATE_RULE_VIOLATION`. |
| Descartar borrador | `DELETE …/{oneTimeIncomePublicId}` | **Solo `EN_REVISION`** (soft-delete `isActive=false`); devuelve `parentConcurrencyToken`. |
| Anular borrador | `PATCH …/{oneTimeIncomePublicId}/annulment` | `EN_REVISION` o `AUTORIZADO` → `ANULADO`; **`reason` obligatorio**. (El `AUTORIZADO` también se cierra por el autorizador vía §4 revocación — misma transición.) |
| Re-imputar periodo | `PATCH …/{oneTimeIncomePublicId}/period` | **Solo `AUTORIZADO`**: re-apunta `payrollTypeCode` + periodo + etiqueta + fecha fin. No-`AUTORIZADO` → `422 ONE_TIME_INCOME_NOT_RETARGETABLE`. |

---

## 4. Resolución y revocación del autorizador (controlador dedicado)

Requieren el grant **`AuthorizeOneTimeIncomes`** (que `Admin` **no** implica) y respetan el **anti-self triple** (§1). `If-Match` con el token del ingreso.

| Operación | Endpoint | Body | Efecto |
|---|---|---|---|
| Resolver | `PATCH …/{oneTimeIncomePublicId}/resolution` | `{ "targetStatusCode": "AUTORIZADO" \| "RECHAZADO", "note": "…" }` | `EN_REVISION` → `AUTORIZADO` (autorizar) o `RECHAZADO` (rechazar; **`note` obligatoria** → `422 ONE_TIME_INCOME_DECISION_NOTE_REQUIRED`). |
| Revocar | `PATCH …/{oneTimeIncomePublicId}/revocation` | `{ "reason": "…" }` | `AUTORIZADO` → `ANULADO` (terminal); **`reason` obligatorio**. Un `APLICADO` exige revertir la aplicación primero. |

Respuesta = `OneTimeIncomeResponse` con el estado nuevo y `decidedByUserPublicId`/`decidedUtc`/`decisionNote` poblados. Sujeto, registrador o solicitante-con-login (aun con el grant) → `403 ONE_TIME_INCOME_SELF_APPROVAL_FORBIDDEN`. Admin sin el grant → `403` (policy). `targetStatusCode` inválido → `422 ONE_TIME_INCOME_STATUS_INVALID`.

---

## 5. Aplicación: unitaria, lote por periodo y reversión

### 5.1 Aplicar (unitaria) — `POST …/{oneTimeIncomePublicId}/applications` → `201`

```json
{ "appliedDate": null, "payrollPeriodPublicId": null, "payrollPeriodLabel": null, "notes": null }
```

- Registra **la única** aplicación de un ingreso `AUTORIZADO` (RN-06: a lo más una aplicación activa; el índice único parcial es la red final). El **monto lo lleva el ingreso** (no editable). `appliedDate` default hoy; `payrollPeriodPublicId` (opcional) imputa la aplicación a una instancia de periodo (valida activa) y snapshotea id + etiqueta. Origen = `MANUAL`.
- El ingreso pasa a **`APLICADO`**. Respuesta = `{ application{…}, oneTimeIncomeStatusCode, oneTimeIncomeConcurrencyToken }`. **Usar `oneTimeIncomeConcurrencyToken` como el nuevo `If-Match` del ingreso.**
- `If-Match` = token del **ingreso**. Errores: ingreso no `AUTORIZADO` → `422 ONE_TIME_INCOME_NOT_APPLICABLE`; ya con aplicación activa → `422 ONE_TIME_INCOME_ALREADY_APPLIED`.

### 5.2 Lote por periodo (empresa) — `POST /api/v1/companies/{companyId}/one-time-incomes/apply-period` → `200`

```json
{
  "payrollTypeCode": "QUINCENAL",
  "payrollPeriodPublicId": null,                 // su id/etiqueta se snapshotean en las aplicaciones…
  "payrollPeriodLabel": "Quincena 13/2026",      // …o una etiqueta de override
  "excludedIncomePublicIds": ["…"]               // POSPOSICIÓN: quedan pendientes para el próximo lote
}
```

- Aplica **atómicamente** todos los ingresos `AUTORIZADO` sin aplicación activa del `payrollTypeCode` (incluye los "atrasados"). Cualquier conflicto **revierte todo el lote** (`422 ONE_TIME_INCOME_APPLY_PERIOD_CONFLICT`). HR-only (`ManageOneTimeIncomes`).
- **Posposición = exclusión**: los ids en `excludedIncomePublicIds` **no** se aplican y siguen pendientes → se aplican en el siguiente lote sin exclusión (efecto "enviar a otro periodo").
- El lote está protegido por lock (`pg_advisory_xact_lock`); dos corridas concurrentes del mismo filtro **no duplican** (índice único parcial).

### 5.3 Anular la aplicación (reversión) — `PATCH …/{oneTimeIncomePublicId}/applications/{applicationPublicId}/annulment` → `200`

- `{ "reason": "…" }` (**obligatorio**). La aplicación `APLICADA` → `ANULADA` y el ingreso **reabre** `APLICADO` → `AUTORIZADO` (índice único parcial: solo cuenta la aplicación activa, así que puede re-aplicarse). Respuesta con el `oneTimeIncomeConcurrencyToken` nuevo. Aplicación ya anulada → `422 ONE_TIME_INCOME_APPLICATION_NOT_REVERTIBLE`.

### 5.4 Historial de aplicaciones

`GET …/{oneTimeIncomePublicId}/applications` → `OneTimeIncomeApplicationResponse[]` (`APLICADA` + `ANULADA`, actividad más reciente primero): `appliedDate`, `payrollTypeCode`, `payrollPeriodLabel`, `originCode` (`MANUAL`/`MOTOR`/`LIQUIDACION`), `statusCode`, `annulmentReason`, …

---

## 6. Bandejas, exportaciones e insumo de planilla (nivel empresa)

| Pantalla | Endpoint | Notas |
|---|---|---|
| Bandeja + **agregación (groupBy)** | `POST /api/v1/companies/{companyId}/one-time-incomes/query` | Filtros: `statusCodes[]`, `employeeId`, `conceptTypeCode`, `fromDate`/`toDate`, `isFixedValue`, `payrollTypeCode`, `payrollPeriod`, `costCenterPublicId`, `currencyCode`, `requesterFilePublicId`, `search`, `pageNumber`/`pageSize`. Devuelve `items[]`, `totalCount`, `statusCounts` (cubren **todos** los estados) y los totales **por moneda** (RN-13). Rate-limited. |
| Export de ingresos | `GET …/one-time-incomes/export?format=xlsx\|csv\|json` + los mismos filtros como query-string | Filas con header, valor, destino, solicitante, estado y los ids de registrador/decisor. |
| Bandeja de pendientes/atrasados | `POST …/one-time-incomes/pending/query` — `{ payrollTypeCode?, onlyOverdue? }` | Los `AUTORIZADO` **sin aplicación activa** (RF-012); marca **`Vencido`** cuando el `payrollPeriodEndDate` declarado ya pasó. |
| Export de pendientes | `GET …/one-time-incomes/pending/export?format=…&payrollTypeCode=&onlyOverdue=` | Mismos filtros. |
| **Insumo de planilla** (RF-012) | `GET …/one-time-incomes/payroll-input/export?format=…&payrollTypeCode=&payrollPeriod=` | Ingresos **`AUTORIZADO` no aplicados** del **`payrollTypeCode` + `payrollPeriod` OBLIGATORIOS** (contra la etiqueta de periodo declarada) — una fila por ingreso (empleado, concepto, tipo de planilla, periodo, monto, moneda, centro de costo). Es el **puente con la nómina externa**; cuadra exactamente contra las pendientes del mismo filtro. **Falta tipo/periodo → `400`.** |

**`groupBy` (8 dimensiones · №14):** en `POST …/query` envía `groupBy` con **una** de: `estado`, `tipo`, `empleado`, `tipoPlanilla`, `periodo`, `centroCosto`, `moneda`, `mes` (case-insensitive). La respuesta añade los buckets de agregación con **clave compuesta (dimensión, moneda)** para no cruzar monedas (RN-13); los grupos **CUADRAN** contra los totales planos del mismo filtro. Token fuera del whitelist → `400`.

Exports **síncronos**, rate-limited y con tope (`413 REPORT_EXPORT_TOO_LARGE` → sugerir filtrar); formato inválido → `400 PERSONNEL_FILE_EXPORT_FORMAT_INVALID`. Todo lo de empresa es insumo RRHH (sin autoservicio).

---

## 7. Integración con la liquidación

Cuando se **genera la liquidación** de un empleado con retiro, el motor lee sus ingresos eventuales y — **solo en la plaza principal** (el ingreso es por empleado, la liquidación es por plaza; se evita doble sugerencia en retiros multi-plaza) — agrega una sugerencia por cada ingreso **`AUTORIZADO`** (por definición, sin aplicación activa):

- Concepto **`INGRESO_EVENTUAL_PENDIENTE`** (seed `-9905`, **`isSystemCalculated = false`** ⇒ es una **línea MANUAL sugerida**, no calculada por el motor — como `INGRESO_CICLICO_PENDIENTE` de REQ-005). `conceptCode = "INGRESO_EVENTUAL_PENDIENTE"`, `description` = la `reference` del ingreso (o el `conceptNameSnapshot`), `calculatedAmount`/`finalAmount` = **el importe del ingreso**.
- La línea llega **incluida y editable/excluible**: el liquidador puede **cambiar el monto** (`PUT …/lines/{lineId}` con `manualAmount`) o **excluirla** (`isIncluded=false`). Una **regeneración** de líneas (`POST …/lines/regenerate`) descarta ajustes y **vuelve a leer** los ingresos `AUTORIZADO`.
- **No principal → sin línea. Sin ingresos `AUTORIZADO` → sin línea** (retrocompatible: la liquidación existente no cambia).
- **Al EMITIR** la liquidación, los ingresos `AUTORIZADO` **cuya línea quedó INCLUIDA** pasan a **`APLICADO`** (origen `LIQUIDACION`). **⚠️ Diferencia con los cíclicos:** una **línea EXCLUIDA** deja el ingreso **`AUTORIZADO`** (no se paga por la liquidación). **Al ANULAR** la liquidación, se **reabren exactamente** los ingresos que esa liquidación aplicó (`APLICADO` → `AUTORIZADO`).
- El FE de liquidación **no requiere cambios de contrato** (la sugerencia viaja por el canal de líneas existente). Recomendado: mostrar el origen "ingreso eventual" y permitir editar/excluir la línea como cualquier ingreso manual.

---

## 8. Tabla de errores del módulo

| `extensions.code` | HTTP | Cuándo |
|---|---|---|
| `ONE_TIME_INCOME_CONCEPT_INVALID` / `_CONCEPT_NOT_INCOME` | 422 | `conceptTypeCode` inexistente/inactivo o no es `Nature=Ingreso`. |
| `ONE_TIME_INCOME_CONCEPT_IS_BASE_SALARY` | 422 | El concepto es el salario base (D-03). |
| `ONE_TIME_INCOME_PAYROLL_TYPE_INVALID` | 422 | `payrollTypeCode` inexistente/inactivo. |
| `ONE_TIME_INCOME_ASSIGNED_POSITION_INVALID` | 422 | Plaza ajena al expediente. |
| `ONE_TIME_INCOME_COST_CENTER_MISSING` | 422 | La plaza (default principal) no tiene centro de costo (P-15). |
| `ONE_TIME_INCOME_REQUESTER_INVALID` | 422 | `requesterFilePublicId` inexistente. |
| `ONE_TIME_INCOME_VALUE_AMOUNT_INVALID` | 422 | `amount` ≤ 0 o incoherente con los factores. |
| `ONE_TIME_INCOME_VALUE_FIXED_WITH_COMPONENTS` | 422 | Valor fijo con método/componentes. |
| `ONE_TIME_INCOME_VALUE_METHOD_REQUIRED` / `_METHOD_INVALID` | 422 | Valor por factores sin método o con método desconocido. |
| `ONE_TIME_INCOME_VALUE_COMPONENTS_INVALID` | 422 | Componentes del método incompletos/≤ 0 o cruzados. |
| `ONE_TIME_INCOME_AMOUNT_MISMATCH` | 422 | `amount` enviado ≠ el derivado de los factores. |
| `ONE_TIME_INCOME_INCOME_DATE_IN_FUTURE` | 422 | `incomeDate` > hoy. |
| `ONE_TIME_INCOME_STATUS_INVALID` | 422 | `targetStatusCode` de resolución no es `AUTORIZADO`/`RECHAZADO`. |
| `ONE_TIME_INCOME_DECISION_NOTE_REQUIRED` | 422 | Rechazo sin `note`. |
| `ONE_TIME_INCOME_ANNULMENT_REASON_REQUIRED` | 422 | Anulación/revocación/reversión sin `reason`. |
| `ONE_TIME_INCOME_SELF_APPROVAL_FORBIDDEN` | 403 | Sujeto, registrador o solicitante-con-login resuelve/revoca (anti-self triple). |
| `ONE_TIME_INCOME_STATE_RULE_VIOLATION` | 422/409 | Operación no válida para el estado (editar/descartar un no-`EN_REVISION`, etc.). |
| `ONE_TIME_INCOME_NOT_RETARGETABLE` | 422 | Re-imputar periodo sobre un ingreso no `AUTORIZADO`. |
| `ONE_TIME_INCOME_NOT_APPLICABLE` | 422 | Aplicar sobre un ingreso no `AUTORIZADO`. |
| `ONE_TIME_INCOME_ALREADY_APPLIED` | 422 | Aplicar un ingreso que ya tiene aplicación activa (RN-06). |
| `ONE_TIME_INCOME_APPLICATION_NOT_REVERTIBLE` | 422 | Anular una aplicación ya anulada. |
| `ONE_TIME_INCOME_APPLY_PERIOD_CONFLICT` | 422 | Conflicto en el lote por periodo (revierte todo el lote). |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` | 422 | Cualquier escritura del módulo sobre un perfil `RETIRADO`. |
| `REPORT_EXPORT_TOO_LARGE` / `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` | 413 / 400 | Export. |
| `CONCURRENCY_CONFLICT` / (sin If-Match) | 409 / 400 | Convenciones de concurrencia. |

---

## 9. Flujo recomendado + pasos de adopción por empresa

**Adopción (una vez por empresa):**
1. Verificar los catálogos país sembrados (`one-time-income-statuses`, `payroll-types`); `compensation-concept-types` (Nature=Ingreso) ya existe de módulos previos.
2. Asegurar que las **plazas tengan centro de costo** (P-15) — sin él el alta falla.
3. Asignar los **3 permisos** a los roles: `View`/`Manage` al gestor RRHH; `AuthorizeOneTimeIncomes` a un rol **autorizador distinto** del gestor (separación de funciones; `Admin` NO lo cubre).

**Pantallas:**
1. **Ingreso eventual (per-file)**: alta con concepto + valor **fijo** (monto) o **por factores** (cantidad×valor×multiplicador o porcentaje×base, monto derivado de solo lectura); plaza opcional (default principal) con centro de costo derivado (solo lectura); solicitante obligatorio. Editar/descartar solo en `EN_REVISION`.
2. **Autorización**: cola de `EN_REVISION` para el rol autorizador (autorizar/rechazar con nota); revocar `AUTORIZADO`. **Ocultar el botón para sujeto/registrador/solicitante** (anti-self triple).
3. **Aplicación (per-file)**: aplicar el ingreso `AUTORIZADO` (monto no editable); anular la aplicación con motivo (reabre a `AUTORIZADO`); historial de aplicaciones.
4. **Lote por periodo (empresa)**: elegir tipo de planilla, marcar exclusiones (posposición), ejecutar.
5. **Bandejas/exports**: ingresos (chips por `statusCounts`, **agregación por 8 dimensiones**), pendientes/atrasados, e **insumo de planilla** (tipo+periodo obligatorios) para la nómina externa.
6. **Liquidación**: la línea `INGRESO_EVENTUAL_PENDIENTE` aparece sugerida (editable/excluible) por cada ingreso `AUTORIZADO` en la plaza principal; al emitir, los de línea **incluida** pasan a `APLICADO`.

---

## 10. Notas de despliegue

- **Sin storage** (el módulo no lleva adjuntos): no requiere `purposes` ni contenedores de blobs.
- **Migraciones** (aplicar en orden): **M1** `AddOneTimeIncomeCatalogs` (`20260709131811` — catálogo país `one-time-income-statuses` + `payroll-types`, HasData SV), **M2** `AddOneTimeIncomes` (`20260709133959` — tablas de dominio: ingreso + aplicación, con índice único parcial de aplicación activa), **M3** `AddOneTimeIncomeSettlementConcept` (`20260709173205` — concepto de liquidación **`INGRESO_EVENTUAL_PENDIENTE = -9905`**, `IsSystemCalculated=false`; habilita la sugerencia en la liquidación — **retrocompatible**, no cambia la superficie HTTP de liquidación).
- Los **3 permisos** (`View`/`Manage`/`AuthorizeOneTimeIncomes`) se agregan al catálogo de aprovisionamiento; **asignarlos a los roles** (recordar: `AuthorizeOneTimeIncomes` fuera de `Admin`).
- `payroll-types` es catálogo **compartido con REQ-004/005** (ya sembrado si un módulo previo se desplegó primero; la primera migración que lo defina lo siembra — no duplicar).
- Todos los mensajes llegan localizados (EN/ES/es-SV) en `problemDetails.detail`. La integración con liquidación es **puramente lógica** (canal de sugerencias + hooks emisión/anulación): no cambia el contrato de la API de liquidación.
