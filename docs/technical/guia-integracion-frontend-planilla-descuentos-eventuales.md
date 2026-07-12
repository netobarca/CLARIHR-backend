# Guía de integración frontend — Planilla: descuentos eventuales (REQ-009)

> **Qué es**: el descuento **de una sola vez** (una multa, un daño de equipo, un anticipo, un faltante de caja). No
> tiene cuotas ni intereses: se autoriza y se cobra **una vez**, en la planilla que le corresponda.
>
> **Qué NO es**: el descuento **cíclico** (crédito en cuotas). Ese es REQ-008 y vive en
> `.../recurring-deductions` — ver [`guia-integracion-frontend-planilla-descuentos-ciclicos.md`](./guia-integracion-frontend-planilla-descuentos-ciclicos.md).
>
> Si venís del módulo de **ingresos eventuales** (REQ-006), este es su espejo: **mismo flujo, mismas pantallas**,
> con dos diferencias que sí te tocan y están marcadas ⚠️ abajo.

---

## 1. El modelo mental

Un descuento eventual es **un solo cargo**. Su ciclo de vida es:

```
EN_REVISION ──(autorizar)──> AUTORIZADO ──(cobrar)──> APLICADO
     │                            │                       │
     │(rechazar)                  │(anular)               │(anular el cobro)
     ▼                            ▼                       └──> vuelve a AUTORIZADO
  RECHAZADO                   ANULADO
```

- **`APLICADO` es reversible.** Anular el cobro (`.../applications/{id}/annulment`) devuelve el descuento a
  `AUTORIZADO` y libera el cupo: se puede volver a cobrar. El registro del cobro anulado **no se borra** — queda en
  el historial con su motivo.
- Un descuento tiene **como máximo un cobro activo a la vez** (índice único parcial en base). Si dos operadores
  cobran al mismo tiempo, **uno gana y el otro recibe 422** — no hay doble cobro. No hace falta que bloquees la UI:
  manejá el 422.

### ⚠️ Diferencia 1 con ingresos eventuales: **NO hay centro de costo**

El ingreso eventual pide `costCenterPublicId`. El descuento **no** (P-08): un descuento no se imputa a un centro de
costo, se le resta al empleado. **No mandes el campo.** La plaza (`assignedPositionPublicId`) sí va: es opcional en
el body y, si la omitís, el servidor resuelve la **plaza principal** del empleado.

### ⚠️ Diferencia 2: el concepto debe ser de **egreso** y **no estatutario**

`conceptTypeCode` tiene que apuntar a un concepto de naturaleza **Egreso** que **no** sea de ley (ISSS, AFP, Renta
quedan fuera: esos los calcula la planilla, no los registra un usuario). Si mandás uno inválido:
`422 ONE_TIME_DEDUCTION_CONCEPT_INVALID`.

---

## 2. Permisos

| Código                                      | Qué habilita                                                        |
|---------------------------------------------|---------------------------------------------------------------------|
| `PersonnelFiles.ViewOneTimeDeductions`      | Ver la ficha, la bandeja corporativa, los exports y el insumo.      |
| `PersonnelFiles.ManageOneTimeDeductions`    | Crear, editar, anular, **cobrar** y correr el lote por periodo.     |
| `PersonnelFiles.AuthorizeOneTimeDeductions` | **Solo** resolver (autorizar / rechazar) y revocar.                 |

`Admin` de fichas cubre View y Manage, **pero NO Authorize** — igual que en retiros: la exclusión vive en la
*policy*. Un administrador **no** puede autorizar; para eso hace falta el permiso de autorización explícito.

### 🔒 Anti-autoaprobación TRIPLE

El autorizador no puede ser (a) el **empleado descontado**, (b) el **solicitante** del descuento, ni (c) **quien lo
registró**. Los tres chequeos son del servidor (`422 ONE_TIME_DEDUCTION_SELF_AUTHORIZATION`). En la UI:
**escondé el botón "Autorizar"** cuando el usuario en sesión sea cualquiera de esos tres — así no le mostrás un
botón que siempre falla.

---

## 3. El alta: `POST /api/v1/personnel-files/{fileId}/one-time-deductions`

El formulario es **dual**: valor fijo, o valor calculado.

### Rama A — valor fijo (`isFixedValue: true`)

```jsonc
{
  "deductionDate": "2026-07-12",
  "reference": "DANO-LAPTOP-001",       // opcional: el nro. de acta, de la multa, etc.
  "conceptTypeCode": "DANO_EQUIPO",
  "observations": null,
  "isFixedValue": true,
  "calculationMethod": null,            // ← null obligatorio en esta rama
  "amount": 75.00,                      // ← OBLIGATORIO
  "currencyCode": "USD",
  "assignedPositionPublicId": null,     // null ⇒ el servidor toma la plaza principal
  "requesterFilePublicId": "…",         // ficha de quien PIDE el descuento (≠ el empleado)
  "payrollTypeCode": "MENSUAL",
  "payrollPeriodPublicId": null,
  "payrollPeriodLabel": "Julio 2026",
  "payrollPeriodEndDate": null
}
```

### Rama B — valor calculado (`isFixedValue: false`)

Dos métodos, y **cada uno exige sus componentes**:

| `calculationMethod`      | Campos obligatorios              | Cálculo del servidor              |
|--------------------------|----------------------------------|-----------------------------------|
| `CANTIDAD_POR_VALOR`     | `quantity`, `unitValue`, `multiplier` (opcional, default 1) | `cantidad × valor × multiplicador` |
| `PORCENTAJE_SOBRE_BASE`  | `percentage`, `baseAmount`       | `base × porcentaje / 100`         |

Los componentes **se persisten** y viajan en la respuesta: la pantalla de detalle puede mostrar *"10% de $250"* y
no solo *"$25"*.

### 🚨 EL punto que te va a morder: **NO mandes `amount` en la rama B**

En la rama calculada **el monto lo deriva el servidor**. Si mandás `amount` y no coincide **al centavo** con lo que
el servidor calcula, el alta se rechaza con `422 ONE_TIME_DEDUCTION_AMOUNT_MISMATCH` y **no se persiste nada**.

**La recomendación es simple: omití `amount` (o mandalo `null`) cuando `isFixedValue` sea `false`.** Podés calcular
y mostrar la vista previa en pantalla —está bien—, pero **no la envíes**: el servidor es el dueño del número.

> ⚠️ Y ojo con esto: **la cifra esperada NO viaja en el `detail`** del ProblemDetails (la infraestructura de
> localización reemplaza ese texto por el mensaje catalogado). **El contrato es el CÓDIGO**
> (`extensions.code == "ONE_TIME_DEDUCTION_AMOUNT_MISMATCH"`). Si lo recibís, mostrá el error genérico y
> **recalculá desde los componentes**, que sí los tenés.

### Otras validaciones que te van a pegar

| Código                                           | Cuándo                                                           |
|--------------------------------------------------|------------------------------------------------------------------|
| `ONE_TIME_DEDUCTION_CONCEPT_INVALID`             | El concepto no es Egreso, o es de ley.                           |
| `ONE_TIME_DEDUCTION_AMOUNT_REQUIRED`             | Rama A sin `amount`.                                             |
| `ONE_TIME_DEDUCTION_CALCULATION_INVALID`         | Rama B sin sus componentes (o con `calculationMethod` desconocido). |
| `ONE_TIME_DEDUCTION_AMOUNT_MISMATCH`             | Rama B con un `amount` que no cuadra. **Ver arriba.**            |
| `ONE_TIME_DEDUCTION_REQUESTER_INVALID`           | El solicitante es el propio empleado descontado.                 |
| `ONE_TIME_DEDUCTION_STATUS_TRANSITION_INVALID`   | La acción no aplica al estado actual (p.ej. cobrar un `EN_REVISION`). |

---

## 4. Resolver, revocar, anular, re-apuntar

| Acción              | Endpoint                                                     | Estado requerido | Permiso    |
|---------------------|--------------------------------------------------------------|------------------|------------|
| Autorizar / rechazar| `PATCH .../{id}/resolution` `{ targetStatusCode, note }`      | `EN_REVISION`    | Authorize  |
| Revocar             | `PATCH .../{id}/revocation` `{ reason }`                      | `AUTORIZADO`     | Authorize  |
| Anular              | `PATCH .../{id}/annulment` `{ reason }`                       | no `APLICADO`    | Manage     |
| Re-apuntar periodo  | `PATCH .../{id}/period` `{ payrollPeriod… }`                  | no `APLICADO`    | Manage     |
| Editar              | `PUT .../{id}`                                                | `EN_REVISION`    | Manage     |

`targetStatusCode` es `AUTORIZADO` o `RECHAZADO`. Todos llevan `If-Match` (ver §9).

---

## 5. Cobrar

### Un cobro suelto

```
POST /api/v1/personnel-files/{fileId}/one-time-deductions/{id}/applications
If-Match: "{concurrencyToken}"
{ "appliedDate": null, "payrollPeriodPublicId": null, "notes": null }
```

`appliedDate: null` ⇒ hoy. Responde `{ application, oneTimeDeductionStatusCode, oneTimeDeductionConcurrencyToken }`
— **el token nuevo viene en `oneTimeDeductionConcurrencyToken`, no en `concurrencyToken`**. Guardalo.

Estado del descuento tras el cobro: `APLICADO`.

### Anular el cobro (la reversa)

```
PATCH .../{id}/applications/{applicationPublicId}/annulment
If-Match: "{token}"
{ "reason": "Cobro indebido" }        // ← el motivo es OBLIGATORIO
```

El descuento vuelve a `AUTORIZADO` y **se puede volver a cobrar**.

### El lote por periodo (la pantalla de planilla)

1. **La lista de trabajo**: `POST /api/v1/companies/{companyId}/one-time-deductions/pending/query`
   → todo lo `AUTORIZADO` que aún no se cobró, con `isOverdue: true` marcando lo que ya debió cobrarse y no se cobró.
2. **El lote**: `POST /api/v1/companies/{companyId}/one-time-deductions/apply-period`
   `{ payrollTypeCode, payrollPeriodPublicId?, excludedDeductionPublicIds[] }`
   → `{ aplicados, pospuestos }`.

> El lote es **atómico**: cualquier conflicto **revierte TODO** (422). No hay éxitos parciales, así que no muestres
> progreso por fila — mostrá el resultado. Lo excluido simplemente **queda pendiente** para la próxima corrida.

---

## 6. La bandeja corporativa

```
POST /api/v1/companies/{companyId}/one-time-deductions/query
{ employeeId?, statusCode?, conceptTypeCode?, payrollTypeCode?, deductionFrom?, deductionTo?, pageNumber, pageSize }
```

Devuelve `items` + `totalCount` + **`statusCounts`** + **`amountByCurrency`**.

> ⚠️ **`statusCounts` y `amountByCurrency` SIEMPRE cubren TODOS los estados**, aunque filtres por uno. Es a
> propósito: son los números de las **pestañas**. Si filtrás por `APLICADO`, `items` trae solo los aplicados, pero
> las pestañas siguen mostrando cuántos hay en revisión. **No los recalcules desde `items`.**

---

## 7. Exports (⚠️ ojo con el JSON)

| Export                | Endpoint                                                            |
|-----------------------|---------------------------------------------------------------------|
| Bandeja               | `GET /api/v1/companies/{companyId}/one-time-deductions/export`       |
| **Insumo de planilla**| `GET /api/v1/companies/{companyId}/one-time-deductions/payroll-input/export` |

`?format=xlsx|csv|json` (default `xlsx`). Descarga **síncrona** con tope de filas → `413` si te pasás.

> 🚨 En `format=json` las propiedades vienen en **PascalCase y en español** (`Empleado`, `Referencia`, `Monto`,
> `Estado`, `FechaAplicada`…), **no** en camelCase: son los encabezados de columna del Excel. Si parseás el JSON,
> usá esos nombres tal cual.

**El insumo de planilla exige el rango** (`startDate` + `endDate`). Si falta un extremo:
`422 ONE_TIME_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED` — no hay volcado completo silencioso. Trae **una fila por
descuento efectivamente cobrado**: los cobros **anulados quedan fuera**, así el insumo cuadra con lo que de verdad
se le descontó al empleado.

---

## 8. Liquidación (finiquito)

Al **crear** una liquidación, cada descuento eventual `AUTORIZADO` y no cobrado del empleado (solo en su **plaza
principal**) aparece como una línea **sugerida**:

- `conceptCode`: `DESCUENTO_EVENTUAL_PENDIENTE`
- `conceptClass`: **`Descuento`** → **resta del neto**.
- `isSystemCalculated`: **`false`** → la línea es **manual**: el liquidador puede **editar el monto o excluirla**.

Y entonces:

- **Emitir** la liquidación con la línea **incluida** ⇒ el descuento pasa a `APLICADO` (se cobró en el finiquito).
- **Emitir** con la línea **excluida** ⇒ el descuento **sigue `AUTORIZADO`**: la deuda no se cobró y sigue viva.
- **Anular** la liquidación ⇒ se reabre **exactamente** lo que esa liquidación cerró (vuelve a `AUTORIZADO`).

Si el empleado no debe nada, **no aparece ninguna línea** — la liquidación es idéntica a la de siempre.

---

## 9. Convenciones (recordatorio)

- Prefijo `api/v1`. Los `Guid` de dominio serializan como `xxxPublicId`.
- **Toda escritura lleva `If-Match: "{concurrencyToken}"`**: sin él → `400`; con uno viejo → `409`.
  Después de **cada** escritura, refrescá el token con el que viene en la respuesta.
- El código de error de negocio va en **`extensions.code`** del ProblemDetails. **Ramificá por ese código, nunca
  por el texto de `detail`** (está localizado ES/EN y puede cambiar).
- Los enums viajan como **strings**.
