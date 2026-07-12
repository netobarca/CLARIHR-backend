# Guía de integración frontend — Tiempos no trabajados (REQ-011)

> **Qué es**: el registro de las **ausencias** (con o sin goce), las **suspensiones con descuento** y las **llegadas
> tardías**, **con el descuento ya calculado por el servidor**.
>
> **Qué NO es**: un módulo de *solicitudes* de permiso. Aquí se registra un hecho que **ya ocurrió** — por eso **no
> hay flujo de aprobación**: el registro nace `REGISTRADO` y lo único que puede pasarle es que lo anulen.

---

## 1. 🚨 La regla que tenés que entender antes de maquetar: **el séptimo día**

Un empleado que faltó **de lunes a viernes** no perdió cinco días: perdió **seis**.

Los cinco días que no trabajó, **más el día de descanso remunerado que no se ganó**. Eso es el «séptimo», y lo aplica
el tipo que tenga `countsSeventhDayPenalty: true`.

```
Ausencia lun→vie · salario $900 (⇒ $30/día)

  días computables      5
  séptimo              +1        ← una semana afectada ⇒ un día
  ─────────────────────────
  días descontados      6   ⇒   $180
```

**Mostralo desglosado** (`computableDays`, `seventhDayPenaltyDays`, `discountedDays`). Si solo mostrás el monto, el
usuario va a creer que el sistema se equivocó — porque «solo faltó cinco días».

Una semana está **afectada** si tiene al menos un día computable. Dos semanas ⇒ dos séptimos. Una ausencia solo de
sábado y domingo (que no cuentan) ⇒ **cero** días y **cero** séptimos.

---

## 2. El maestro de tipos (pantalla de configuración)

`/api/v1/companies/{companyId}/not-worked-time-types` — `GET` · `POST` · `PUT` · `PATCH …/{id}/activation`.

> **No hay DELETE.** La baja es lógica (`activation` con `isActive: false`): un tipo ya usado por un registro tiene
> que seguir siendo legible.

| Campo | Qué hace |
|---|---|
| `countsHoliday` / `countsSaturday` / `countsRestDay` | Si están en `false`, **ese día no se cuenta ni se descuenta**. |
| `countsSeventhDayPenalty` | **El séptimo** (§1). |
| `usesWorkSchedule` | El registro se captura **en horas** (una llegada tardía), no en días. |
| `discountPercent` | `0` = **con goce** (se registra, no se descuenta) · `100` = sin goce pleno. |
| `deductionConceptTypeCode` | **Obligatorio si `discountPercent > 0`** → `422 NOT_WORKED_TIME_TYPE_DEDUCTION_CONCEPT_REQUIRED`. Un descuento sin concepto nunca llegaría al insumo de planilla. |
| `appliesToPermission` | **Solo clasificación**: marca los tipos que el futuro módulo de solicitudes ofrecerá. Hoy **no hace nada**. |

### La plantilla — `POST /api/v1/companies/{companyId}/not-worked-time-configuration/load-template`

Crea los 4 tipos base: `AUSENCIA_SIN_GOCE` (100 %, con séptimo) · `AUSENCIA_CON_GOCE` (0 %) ·
`SUSPENSION_CON_DESCUENTO` (100 %, con séptimo) · `LLEGADA_TARDIA` (100 %, en horas).

**Es idempotente**: un código que la empresa ya tiene se **salta**, nunca se sobrescribe (aunque lo hayan editado o
inactivado). La respuesta trae `typesCreated` / `typesSkipped`. Podés ofrecer el botón «cargar plantilla» sin miedo.

---

## 3. El registro — `POST /api/v1/personnel-files/{fileId}/not-worked-times`

```jsonc
{
  "typeCode": "AUSENCIA_SIN_GOCE",
  "assignedPositionPublicId": null,   // null ⇒ la plaza principal del empleado
  "startDate": "2026-07-06",
  "endDate": "2026-07-10",
  "hours": null,                      // ← ver abajo
  "reason": "No se presentó"
}
```

### 🚨 El monto **NO se digita**

El servidor lo calcula: escanea día por día, excluye lo que el tipo diga que no cuenta, agrega el séptimo, y aplica
el porcentaje. **No hay campo `amount` en el request, y no debe haberlo en tu formulario.** Lo que sí podés (y
deberías) hacer es mostrar la respuesta desglosada.

### `hours`: solo para los tipos en horas, y **obligatorio** en ellos

| Situación | Resultado |
|---|---|
| Tipo con `usesWorkSchedule: true` **sin** `hours` | `422 NOT_WORKED_TIME_HOURS_REQUIRED` |
| Tipo con `usesWorkSchedule: false` **con** `hours` | `422 NOT_WORKED_TIME_HOURS_NOT_APPLICABLE` |

**2 horas tarde de una jornada de 8 h = 0.25 días**, no 1. Cobrar el día entero sería un castigo, no un descuento.

### Anular — `PATCH …/not-worked-times/{id}/annulment` · `{ "reason": "…" }` (obligatorio)

Es **la única transición**. Anular dos veces → `422 NOT_WORKED_TIME_ALREADY_ANNULLED`.

Un registro anulado **desaparece del insumo de planilla y de la vista de disponibilidad**: una ausencia anulada
nunca ocurrió.

---

## 4. La bandeja corporativa

`POST /api/v1/companies/{companyId}/not-worked-times/query`
→ `items` + `totalCount` + **`statusCounts`** + **`amountByCurrency`**.

> ⚠️ `statusCounts` y `amountByCurrency` **siempre cubren TODOS los estados**, aunque filtres por uno: son los
> números de las **pestañas**. No los recalcules desde `items`.
>
> `amountByCurrency` suma **solo los `REGISTRADO`** — lo anulado ya no es dinero.

---

## 5. Exports

| Export | Endpoint |
|---|---|
| Bandeja | `GET /api/v1/companies/{companyId}/not-worked-times/export` |
| **Insumo de planilla** | `GET /api/v1/companies/{companyId}/not-worked-times/payroll-input/export` |

`?format=xlsx|csv|json` (default `xlsx`).

> 🚨 En `format=json` las propiedades vienen en **PascalCase y en español** (`Empleado`, `Monto`, `DiasDescontados`,
> `ConceptoEgreso`…): son los encabezados de columna del Excel.

**El insumo exige el rango** (`startDate` + `endDate`) → si falta un extremo:
`422 NOT_WORKED_TIME_PAYROLL_INPUT_RANGE_REQUIRED`. Y trae **solo lo que de verdad se descuenta**: fuera los
**anulados** y fuera las **ausencias con goce** (esas no tienen descuento — son documentación, no insumo).

---

## 6. Disponibilidad de tiempo

Los tiempos no trabajados son ahora la **tercera fuente** de `POST /api/v1/companies/{companyId}/time-availability/query`:

- `activeSources[]` incluye **`TIEMPO_NO_TRABAJADO`**;
- cada fila trae `categoryCode: "TIEMPO_NO_TRABAJADO"` y su `referencePublicId` = el id del registro.

**El contrato no cambió**: si ya consumías la vista, simplemente empezás a ver una categoría más.

---

## 7. Permisos

| Código | Qué habilita |
|---|---|
| `PersonnelFiles.ViewNotWorkedTimes` | Ver la ficha, la bandeja, los exports y el insumo. |
| `PersonnelFiles.ManageNotWorkedTimes` | Registrar y anular. |
| `PersonnelFiles.ManageNotWorkedTimeTypes` | Configurar el maestro y cargar la plantilla. |

**No hay permiso de autorización** — no hay nada que autorizar (§ intro). `Admin` de fichas cubre los tres.

---

## 8. Convenciones (recordatorio)

- Prefijo `api/v1`. Un `Guid` cuyo nombre termina en `…Id` **serializa como `…PublicId`**.
- Toda escritura lleva `If-Match: "{concurrencyToken}"` (falta → `400`; viejo → `409`).
- El código de negocio va en **`code`**, miembro **RAÍZ** del ProblemDetails (**no** existe un objeto `extensions`
  en el JSON). **Ramificá por el código, nunca por el texto** (está localizado ES/EN).
