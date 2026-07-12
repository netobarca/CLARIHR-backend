# Guía de integración frontend — Endeudamiento del empleado (REQ-010)

> **Qué es**: el control del **% de endeudamiento**. Antes de meterle un descuento cíclico a un empleado, el sistema
> calcula qué porcentaje de su ingreso consumirían sus deudas, y **avisa** si eso pasa del techo configurado.
>
> **La regla que gobierna todo el módulo**: **avisar, NUNCA bloquear.** Es literal del levantamiento. El 422 que vas
> a recibir **no es un rechazo definitivo** — es una pregunta. Si el usuario confirma, el descuento se registra.

---

## 1. Lo primero, porque cambia cómo diseñás la pantalla

### 🚨 Si la empresa no configuró parámetros, **no hay control** — y eso está bien

Sin `maxIndebtednessPercent` (preferencia) **ni** una fila de límite por tipo, el chequeo **no corre**: los
descuentos se registran como siempre, sin advertencia, aunque se coman el 90 % del salario.

**No es un bug, es el diseño**: el módulo es *opt-in por configuración*. La consulta te lo dice explícitamente con
`status: "SIN_CONTROL"`. **Pintalo en gris, no en rojo.**

### 🚨 El desglose del 422 va en la **RAÍZ** del ProblemDetails, no en `detail` ni en un objeto `extensions`

El backend escribe el código y el desglose en `ProblemDetails.Extensions`, pero eso se **aplana** al serializar
(`[JsonExtensionData]`): **en el JSON no existe ningún objeto `extensions`**. Todo son miembros de la raíz.

Y el `detail` **no te sirve**: el localizador lo reemplaza por el mensaje catalogado en ES/EN. Las cifras solo
viajan en los campos estructurados.

```jsonc
// 422 — el cuerpo REAL
{
  "type": "https://httpstatuses.com/422",
  "title": "El descuento llevaría al empleado por encima del límite de endeudamiento aplicable.",
  "detail": "El descuento llevaría al empleado por encima del límite de endeudamiento aplicable.",
  "status": 422,
  "code": "INDEBTEDNESS_LIMIT_EXCEEDED",   // ← raíz
  "baseIncome": 1000.00,                   // ← raíz
  "currentLoad": 0.00,                     // ← raíz
  "newInstallment": 400.00,                // ← raíz
  "projectedPercent": 40.00,               // ← raíz
  "limitPercent": 30.00,                   // ← raíz
  "limitSource": "GLOBAL",                 // ← raíz — "GLOBAL" o "TIPO"
  "traceId": "…"
}
```

---

## 2. El flujo de confirmación (esto es TODO el módulo, del lado del usuario)

**No hay un endpoint de confirmación aparte.** Confirmar = **reenviar el MISMO request** con un campo más.

```
POST .../recurring-deductions           →  422 INDEBTEDNESS_LIMIT_EXCEEDED + el desglose
     ↓  (mostrás el diálogo con las cifras del 422)
     ↓  el usuario acepta
POST .../recurring-deductions           →  201 Created
     { …el mismo body…, "acknowledgeIndebtednessExceeded": true }
```

El diálogo se arma **con las cifras del 422**, no con las tuyas:

> «Este descuento llevaría a **Juan Pérez** al **40 %** de endeudamiento (ingreso **$1,000**, deuda actual **$0**,
> nueva cuota **$400**). El límite de la empresa es **30 %**. ¿Registrar de todas formas?»

El campo `acknowledgeIndebtednessExceeded` existe en **3 endpoints**:

| Endpoint | Cuándo salta |
|---|---|
| `POST .../recurring-deductions` | al **registrar** el crédito |
| `PUT .../recurring-deductions/{id}` | al **editarlo** (solo en `EN_REVISION`) |
| `PATCH .../recurring-deductions/{id}/resolution` | al **autorizarlo** |

### ⚠️ Sí: el chequeo corre DOS veces, y no es redundante

Entre que se registra un crédito y se autoriza, **la carga del empleado se mueve** (otros créditos suyos se
autorizan, otros terminan). Un crédito que cabía al registrarse puede **ya no caber** al decidirse — y ahí es donde
el segundo chequeo lo atrapa. Así que **la pantalla del autorizador también necesita el diálogo de confirmación.**

Rechazar (`RECHAZADO`) nunca dispara el chequeo: rechazar no endeuda a nadie.

---

## 3. La huella queda registrada

Cuando alguien confirma, se guarda **quién**, **cuándo** y **con qué cifras**. Viaja en la ficha del descuento:

```jsonc
// GET .../recurring-deductions/{id}
{
  "recurringDeductionPublicId": "…",
  // …
  "indebtednessOverrides": [
    {
      "indebtednessOverridePublicId": "…",
      "stage": "AUTORIZACION",              // CREACION | AUTORIZACION
      "acknowledgedByUserPublicId": "…",    // ⚠️ ...PublicId, no ...Id (ver §7)
      "acknowledgedUtc": "2026-07-12T21:30:00Z",
      "baseIncome": 1000.00,
      "monthlyLoad": 200.00,
      "newInstallment": 150.00,
      "projectedPercent": 35.00,
      "limitPercent": 30.00,
      "limitSource": "GLOBAL"
    }
  ]
}
```

Es **una fila por evento**, no un flag: el mismo crédito puede excederse al crearse **y otra vez** al autorizarse,
con cifras distintas. Mostralas todas (es la traza de responsabilidad).

El array es **aditivo** al contrato: si lo ignorás, todo lo demás sigue igual.

---

## 4. La consulta — `GET /api/v1/personnel-files/{fileId}/indebtedness`

Permiso: **`PersonnelFiles.ViewIndebtedness`** (dedicado — es un dato agregado sensible; `Admin` lo cubre).

```jsonc
{
  "baseIncome": 1000.00,
  "baseBreakdown": [
    { "assignedPositionPublicId": "…", "conceptTypeCode": "SALARIO_BASE",
      "value": 1000.00, "payPeriodCode": "MENSUAL", "monthlyValue": 1000.00 }
  ],
  "currentLoad": 200.00,
  "loadBreakdown": [
    { "recurringDeductionPublicId": "…", "typeCode": "PRESTAMO_BANCARIO",
      "financialInstitution": "Banco Agrícola", "reference": "PREST-BCO-2026-001",
      "installmentAmount": 200.00, "installmentFrequencyCode": "MENSUAL", "monthlyAmount": 200.00,
      "statusCode": "VIGENTE", "isIncludedInLoad": true,
      "limitPercent": 25.00, "limitSource": "TIPO" }
  ],
  "currentPercent": 20.00,
  "globalLimitPercent": 30.00,
  "limitsByType": { "PRESTAMO_BANCARIO": 25.00 },
  "status": "DENTRO",                  // DENTRO | EXCEDIDO | SIN_CONTROL
  "overrides": [ /* la huella histórica, la más reciente primero */ ]
}
```

Detalles que sí importan al maquetar:

- **`isIncludedInLoad: false`** ⇒ el crédito está **`SUSPENDIDO`**: se muestra, pero **no suma**. Mostralo atenuado
  con una etiqueta; si lo sumás vos, tu total no cuadrará con `currentLoad`.
- Cada fila trae **`limitPercent` / `limitSource`**: el techo que gobierna a **ESE** crédito. Sirve para señalar
  cuál es el que rompe el límite.
- **`monthlyAmount` ≠ `installmentAmount`** cuando la frecuencia no es mensual (ver §6).
- Un empleado **sin salario configurado** reporta `baseIncome: 0`, `currentPercent: 0` y **nunca** aparece como
  excedido. No lo pintes como error.

---

## 5. La simulación — `POST /api/v1/personnel-files/{fileId}/indebtedness/simulation`

Permiso: `ViewIndebtedness`. **Es POST solo porque lleva body: NO escribe nada.** («Solo simulación y no debe
afectar la planilla» — literal del levantamiento; el backend tiene un test que verifica que no toca ni una fila.)

```jsonc
// request
{
  "baseIncomeOverride": null,        // "ingreso digitado": null ⇒ usa el derivado del empleado
  "additionalDeduction": { "amount": 400.00, "payPeriodCode": "MENSUAL", "typeCode": "PRESTAMO_BANCARIO" }
}

// response
{
  "baseIncome": 1000.00,
  "currentLoad": 0.00,
  "currentPercent": 0.00,
  "additionalMonthlyDeduction": 400.00,   // ⚠️ MENSUALIZADO (ver §6)
  "simulatedPercent": 40.00,
  "limitPercent": 30.00,
  "limitSource": "GLOBAL",
  "wouldExceed": true,
  "status": "EXCEDIDO"
}
```

`typeCode` elige el techo que se compararía. Omitirlo ⇒ se compara contra el global.

---

## 6. ⚠️ La mensualización (la trampa aritmética del módulo)

Todo se compara **en pesos mensuales**. Una cuota **semanal de $100 no son $100 de deuda mensual: son $433.33**
(×52/12). Si mostrás la cuota cruda junto al porcentaje, tu pantalla no va a cuadrar.

| `payPeriodCode` | Factor | $100 de cuota ⇒ |
|---|---|---|
| `MENSUAL`   | ×1        | $100.00 |
| `QUINCENAL` | ×2        | $200.00 |
| `SEMANAL`   | ×52/12    | **$433.33** |
| `UNICA`     | ×1/12     | $8.33 |

El backend ya te da el número mensualizado (`monthlyAmount`, `monthlyValue`, `additionalMonthlyDeduction`,
`newInstallment`). **Usá esos, no recalcules.**

---

## 7. Los parámetros (pantalla de configuración)

### El techo global — vive en las **preferencias de la empresa**

`PUT /api/v1/companies/{companyId}/preferences` → campo **`maxIndebtednessPercent`** (`(0,100]`, o `null` = sin
control).

> ⚠️ **Es PUT-only.** El `PATCH` de preferencias es *scalar-only* (solo `currencyCode`/`timeZone`) — no intentes
> parchear este campo. Lleva `If-Match`.

### Los techos por tipo — `/api/v1/companies/{companyId}/indebtedness-limits`

| Método | Qué | Permiso |
|---|---|---|
| `GET`  | la lista de techos por tipo de descuento | `ViewIndebtedness` |
| `PUT`  | **replace-all** de la lista | `ManageIndebtednessParameters` |

```jsonc
// PUT — el body es el conjunto COMPLETO: lo que no mandes, se borra
{ "limits": [ { "recurringDeductionTypeCode": "PRESTAMO_BANCARIO", "maxPercent": 25.00 } ] }
```

- `422 INDEBTEDNESS_LIMIT_TYPE_INVALID` — el tipo no existe o está inactivo en el catálogo.
- `422 INDEBTEDNESS_LIMIT_TYPE_DUPLICATED` — mandaste el mismo tipo dos veces.

> **El techo por tipo PREVALECE sobre el global — incluso si es MÁS PERMISIVO.** Global 30 % + préstamo 45 % ⇒ un
> préstamo valida contra **45 %**, no contra 30. Es a propósito: darle a un tipo su propio techo es justamente para
> lo que existe la tabla. Decilo en la UI, o el usuario va a creer que configuró mal.

---

## 8. Convenciones (recordatorio)

- Prefijo `api/v1`. Un `Guid` cuyo nombre termina en `…Id` **serializa como `…PublicId`**
  (`acknowledgedByUserId` → **`acknowledgedByUserPublicId`**).
- Toda escritura lleva `If-Match: "{concurrencyToken}"` (falta → `400`; viejo → `409`).
- El código de negocio va en **`code`**, miembro **RAÍZ** del ProblemDetails. **Ramificá por el código, nunca por
  el texto** (está localizado ES/EN).
- Los enums viajan como **strings**.
