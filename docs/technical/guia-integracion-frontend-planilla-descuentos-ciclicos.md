# Guía de integración frontend — Planilla: descuentos cíclicos (REQ-008)

| | |
|---|---|
| **Módulo** | Descuentos cíclicos: créditos que la empresa le descuenta al empleado en cuotas (préstamos bancarios, procuraduría, cooperativas, asociaciones…) |
| **Backend** | `feature/planilla-descuentos` (PR-1…PR-6) |
| **Contrato** | `docs/technical/api/openapi.yaml` — 17 rutas nuevas, 26 esquemas nuevos |
| **Permisos** | `PersonnelFiles.ViewRecurringDeductions` · `ManageRecurringDeductions` · `AuthorizeRecurringDeductions` |
| **Prefijo** | Todo bajo `api/v1`. El código de error viaja en `extensions.code` del ProblemDetails |

---

## 1. El modelo mental (léelo antes de maquetar)

Un **descuento cíclico** es un **crédito con saldo**, no un concepto de nómina estructural. Tiene tres piezas:

1. **La cabecera**: la referencia crediticia, el tipo, el concepto, la **institución financiera** (a quién se le paga) y la **fecha de vigencia** — que **puede ser futura**: el crédito se registra y se autoriza, pero no se le cobra nada al empleado hasta que esa fecha llega.
2. **El plan**, que se expresa de **una de dos formas mutuamente excluyentes** (esto define tu formulario, ver §3).
3. **Los cobros**, que son las filas del libro contable.

### ⚠️ La unidad del libro es el COBRO, no la cuota

El crédito tiene una **frecuencia de cuota** (cada cuánto se *devenga* una cuota) y una **frecuencia de aplicación** (cada cuánto se *cobra* en planilla). Si la de aplicación es más rápida, **la cuota se parte y cada parte es un cobro real**:

> Una cuota **mensual de $100** aplicada **quincenalmente** se cobra como **2 cobros de $50**.

Por eso `installmentNumber` **numera cobros**, y la respuesta trae **dos contadores distintos**:

- `installmentCount` — cuántas **cuotas** tiene el plan (12).
- `chargeCount` — cuántos **cobros** se le harán al empleado (24, si se aplica quincenalmente).

Cuando ambas frecuencias coinciden, `chargeCount == installmentCount` y la distinción desaparece. **La frecuencia de aplicación no puede ser más lenta que la de cuota** → `422 RECURRING_DEDUCTION_APPLICATION_FREQUENCY_INVALID`.

---

## 2. Estados y quién puede hacer qué

```
EN_REVISION ──autorizar──▶ VIGENTE ──suspender──▶ SUSPENDIDO ──reanudar──▶ VIGENTE
     │                        │                                                │
     ├──rechazar──▶ RECHAZADO │                                                │
     └──anular────▶ ANULADO   ├──revocar─────▶ ANULADO                         │
                              ├──cerrar (solo indefinidos)──▶ FINALIZADO       │
                              └──plan completo / payoff──────▶ FINALIZADO ◀────┘
```

| Acción | Permiso | Notas |
|---|---|---|
| Ver, bandejas, exports | `ViewRecurringDeductions` | |
| Crear, editar, suspender, cerrar, **aplicar cobros**, **abonos** | `ManageRecurringDeductions` | |
| **Autorizar / rechazar / revocar** | `AuthorizeRecurringDeductions` | **`PersonnelFiles.Admin` NO lo implica** |

### 🔒 Anti-autoaprobación DOBLE

Ni el **empleado sujeto** del crédito ni **quien lo registró** pueden decidirlo o revocarlo → **403 `RECURRING_DEDUCTION_SELF_APPROVAL_FORBIDDEN`**. Si tu usuario es cualquiera de los dos, **oculta o deshabilita** los botones de autorizar/rechazar/revocar: el backend los va a rechazar igual.

Solo se edita en `EN_REVISION`. Solo se puede **borrar** (soft-delete) un borrador `EN_REVISION`; un crédito autorizado se **revoca** o se **cierra**, nunca se borra.

---

## 3. El formulario de alta: es DUAL

`POST api/v1/personnel-files/{publicId}/recurring-deductions`

El campo `usesCompoundInterest` parte el formulario en dos ramas **excluyentes**:

### Rama A — sin interés (plan por tramos)

Envía `segments[]` y **ningún** campo de interés. Los tramos son **contiguos desde la cuota 1, sin huecos ni solapes**:

```json
{
  "usesCompoundInterest": false,
  "segments": [
    { "fromInstallment": 1, "toInstallment": 6,  "installmentValue": 50 },
    { "fromInstallment": 7, "toInstallment": 12, "installmentValue": 75 }
  ]
}
```
→ el backend **deriva** `installmentCount: 12` y `totalAmount: 750`. **No los mandes tú.**

- **Plan indefinido** (`isIndefinite: true`): **exactamente un tramo abierto** (`toInstallment: null`). No tiene total ni saldo.
- Un hueco (1–6 y luego 8–12) → `422 RECURRING_DEDUCTION_SEGMENTS_NOT_CONTIGUOUS`.

### Rama B — con interés compuesto

Envía `principalAmount` + `interestRatePercent` + `plannedInstallments`, y **`segments: []`** (mandar tramos aquí → `422 RECURRING_DEDUCTION_SEGMENTS_WITH_INTEREST`).

```json
{
  "usesCompoundInterest": true,
  "principalAmount": 1000,
  "interestRatePercent": 12,
  "plannedInstallments": 12,
  "segments": []
}
```

- `interestRatePercent` es la tasa **NOMINAL ANUAL**. El backend la divide entre los periodos de la frecuencia de cuota (mensual → /12, quincenal → /24).
- El plan se **deriva** por amortización francesa: $1,000 al 12 % en 12 cuotas → cuota **$88.85**, de la cual la primera lleva **$10.00 de interés y $78.85 de capital**.
- Un crédito con interés **no puede ser indefinido** → `422 RECURRING_DEDUCTION_INTEREST_INDEFINITE`.
- **Precarga la tasa** desde la preferencia de empresa `recurringDeductionDefaultInterestRatePercent` (ver §8). Es solo un default del formulario: la tasa que gobierna el crédito es la que se persiste en él.

### Validaciones que te van a pegar

| Código | Qué pasó |
|---|---|
| `RECURRING_DEDUCTION_CONCEPT_INVALID` | El concepto no es un **egreso activo y NO estatutario**. ISSS/AFP/Renta **nunca** pueden respaldar un crédito |
| `RECURRING_DEDUCTION_FINANCIAL_INSTITUTION_REQUIRED` | El concepto es **externo** → la institución financiera es **obligatoria** |
| `RECURRING_DEDUCTION_SETTLEMENT_ACTION_INDEFINITE` | `DESCONTAR_SALDO` en un plan indefinido: no hay saldo que descontar → usa `CANCELAR` |
| `RECURRING_DEDUCTION_APPLICATION_FREQUENCY_INVALID` | La frecuencia de aplicación no divide a la de cuota |

**El `PUT` de edición es REPLACE-ALL en los tramos**: manda el plan completo, no un delta.

---

## 4. La pantalla del crédito: `GET .../{id}/schedule`

Es **la pantalla principal**. Todo lo que devuelve es **derivado** (nunca persistido), así que siempre cuadra:

| Campo | Qué es |
|---|---|
| `installments[]` | **Un renglón por COBRO**: número, fecha teórica, monto, y —si hay interés— `capitalAmount` / `interestAmount`. `isApplied` y `isOverdue` |
| `totalCharged` | **Total cobrado** |
| `totalOutstanding` | **Total no cobrado** (lo que falta del plan) |
| `outstandingBalance` | **El saldo / payoff** ⚠️ |
| `chargeCount` / `installmentCount` | Cobros vs cuotas (§1) |
| `nextInstallmentNumber` | El siguiente cobro a aplicar |

### ⚠️ `totalOutstanding` y `outstandingBalance` NO son lo mismo

En un crédito **con interés**, `outstandingBalance` es el **capital pendiente** — y es **MENOR** que `totalOutstanding`, porque **pagar por adelantado no debe los intereses futuros**. Ese es el número que se usa para el payoff y para la liquidación. Muéstralos como dos cosas distintas o el usuario va a creer que la app se equivoca.

Las **fechas teóricas saltan los meses de excepción** (`exceptionMonths: [12]` → diciembre se salta y **el plan se corre**, no se pierde ninguna cuota).

---

## 5. Cobrar

### Cuota normal
`POST .../{id}/installments` — aplica el **siguiente** cobro. No mandes número ni monto: **los deriva el backend** (no son editables).

- Requiere `If-Match` con el `concurrencyToken` del **crédito**.
- Si la **vigencia no ha llegado** → `422 RECURRING_DEDUCTION_INSTALLMENT_NOT_DUE_YET`.
- Al completar el plan, el crédito pasa a **`FINALIZADO`** solo. Mira `recurringDeductionStatusCode` en la respuesta.

### Abono extraordinario (payoff)
`POST .../{id}/extraordinary-installments` con `{ "amount": 200 }`.

- Va **100 % contra capital** y **REDUCE EL PLAZO** — la cuota no cambia, el crédito simplemente termina antes. La tabla derivada lo refleja sola.
- Pagar **exactamente** `outstandingBalance` es un **payoff** → `FINALIZADO` inmediato.
- Por encima del saldo → `422 …_EXTRAORDINARY_EXCEEDS_BALANCE`. Sobre un `SUSPENDIDO` → `422 …_EXTRAORDINARY_NOT_APPLICABLE`.
- **Pre-llena el campo con `outstandingBalance`** y pon un botón "Cancelar el crédito completo".

### Anular un cobro
`PATCH .../{id}/installments/{installmentId}/annulment` con motivo. Libera el número **y si el crédito estaba `FINALIZADO`, lo REABRE a `VIGENTE`**.

---

## 6. Operación por periodo (la pantalla de planilla)

1. `POST api/v1/companies/{companyId}/recurring-deductions/pending-installments/query` → **qué se va a cobrar** este periodo (incluye lo **vencido** de periodos anteriores, `isOverdue: true`).
2. El usuario **desmarca** los que quiere posponer.
3. `POST api/v1/companies/{companyId}/recurring-deductions/apply-period` con esos ids en `excludedDeductionPublicIds` → aplica todo lo demás.

**El lote es ATÓMICO**: si algo falla, **no se aplica nada** (`422 RECURRING_DEDUCTION_APPLY_PERIOD_CONFLICT`). No muestres éxitos parciales. La respuesta trae `{ aplicadas, finalizados, pospuestas }`.

La bandeja de pendientes usa **la misma proyección que el lote**, así que lo que le muestras al usuario es exactamente lo que se va a aplicar.

---

## 7. Exports (⚠️ ojo con el JSON)

`GET .../export` · `GET .../pending-installments/export` · **`GET .../payroll-input/export`**

- `format=xlsx|csv|json`.
- El **insumo de planilla** exige **`startDate` + `endDate`** → sin ellos, `422 RECURRING_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED`. Lleva la **institución financiera**, la **referencia** y el **desglose capital/interés** de cada cobro, e **incluye los abonos extraordinarios**.

> **Gotcha de contrato**: los exports en `format=json` serializan los nombres de propiedad en **PascalCase** (`Referencia`, `Monto`, `NumeroCuota`), **no** en camelCase como el resto de la API.

---

## 8. Preferencia de empresa

`PUT api/v1/companies/{companyId}/preferences` — campo `recurringDeductionDefaultInterestRatePercent` (nullable, rango `(0, 100]`). **Va en el PUT**, no en el PATCH (que es solo moneda/zona horaria). Úsalo para precargar la tasa del formulario.

---

## 9. Liquidación (finiquito)

Cuando el empleado se retira:

- Un crédito `VIGENTE` con **`DESCONTAR_SALDO`** aporta una línea **`DESCUENTO_CICLICO_PENDIENTE`** al finiquito, por su **`outstandingBalance`** (con interés: el **capital**). Es una **línea de DESCUENTO** — **reduce el neto** — y es **editable y excluible** por el liquidador.
- Un crédito con **`CANCELAR`** se **condona**: no aporta línea, pero igual se cierra al emitir.
- **Emitir** el finiquito finaliza todos los créditos `VIGENTE`; **anularlo** los **reabre** con su saldo intacto.

---

## 10. Catálogos

| Wire key | Para |
|---|---|
| `recurring-deduction-statuses` | Los 6 estados |
| `recurring-deduction-settlement-actions` | `DESCONTAR_SALDO` / `CANCELAR` |
| `recurring-deduction-types` | Tipos de crédito (plantilla editable por la empresa) |
| `pay-periods` | Las **dos** frecuencias (cuota y aplicación) |
| `payroll-types`, `currencies` | Tipo de planilla y moneda |

Todos por `GET api/v1/general-catalogs/{catalogKey}?countryCode=SV`.

---

## 11. Convenciones (recordatorio)

- `If-Match` en **todos** los writes: falta → `400`, obsoleto → `409`.
- Los `Guid XxxId` se serializan como `xxxPublicId`.
- Los enums viajan como **string**.
- El código de error siempre en `extensions.code`.
