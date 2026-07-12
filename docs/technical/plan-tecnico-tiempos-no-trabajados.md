# Plan técnico — Tiempos no trabajados (REQ-011)

| | |
|---|---|
| **Requerimiento** | REQ-011 — Tiempos no trabajados: maestro por empresa + registro con descuento calculado |
| **Análisis** | [`analisis-planilla-descuentos-y-endeudamiento.md`](../business/analisis-planilla-descuentos-y-endeudamiento.md) — **Plan 4**, RATIFICADO 2026-07-12 (D-18/D-19; P-16…P-21) |
| **Rama** | `feature/planilla-descuentos` (se acumula; el análisis lo declaraba paralelizable, pero no hay razón para abrir rama: **el merge lo hace el orquestador** y REQ-011 no colisiona con nada) |
| **PRs** | 4 |
| **Depende de** | **Nada.** Todos sus insumos están en master (asuetos, día de descanso, motor de días, journal, chasis de disponibilidad) |

---

## 0. Lo que hay que saber antes de escribir la primera línea

### 0.1 Casi nada de esto es nuevo — es un **ensamblaje**

Los cuatro insumos duros ya están construidos y certificados. El trabajo real es el **maestro** y el **séptimo**:

| Pieza | Ya existe en | Qué se hace |
|---|---|---|
| **Scan de días con exclusiones** | `IncapacityCalculation.Rules.cs:341-355` (`IsExcluded`) | Se **calca** el predicado (mismos 3 flags + `RestDay`) |
| **Descuento = días × salario/30** | `IncapacityCalculation.Rules.cs:120,144,317-328` | Se calca (`MonthDivisorDays = 30m`, `Round2` una sola vez sobre el diario) |
| **Asuetos del rango** | `IPersonnelFileVacationRepository.GetHolidaysInRangeAsync` | Se **reutiliza tal cual** |
| **Día de descanso** | `LeaveCalculationDataProvider.cs:154-159` | Se calca la cadena: **plaza referida → plaza principal → preferencia de empresa → domingo** |
| **Horas-día** | `CompanyPreference.CompensatoryTimeStandardDailyHours` | Se **reutiliza**; el default `?? 8m` se resuelve **al consumir**, nunca se persiste |
| **Asiento en el journal** | `Incapacities.Handlers.cs:341-363` | Se calca (`PersonnelFilePersonnelAction.Create` + `AddPersonnelActionAsync`) |
| **Maestro con plantilla** | `LeaveTemplateSeeder` + `POST …/load-template` | Se calca el seeder idempotente |
| **Fuente de disponibilidad** | `TimeAvailability.cs:8-23` | **El propio código lo anticipa textualmente**: "a new repository source method + a new category — the wire contract does not change" |

### 0.2 ⚠️ Lo ÚNICO que no tiene precedente: **el séptimo día**

> **P-18 (ratificada):** el séptimo = **+1 día completo de descanso por cada semana afectada**.

El motor de incapacidades **no** hace esto: allí el día de descanso se *excluye* del conteo (`CountsSeventhDay=false`)
o se *incluye* (`true`), pero **nunca se agrega**. Aquí el descuento **suma un día extra** por semana afectada,
porque el empleado que no trabajó la semana pierde también el descanso remunerado.

**El gate de PR-2 es la validación del contador.** Igual que en REQ-008 (la amortización), si no se obtiene la firma:
se construye a la regla ratificada, se verifica la aritmética a mano, y **el sign-off queda como ítem de NEGOCIO en
el checklist de despliegue** — si el contador discrepa, se corrige el **cálculo**, no el modelo (el `%` y los flags
son datos del maestro, editables).

### 0.3 Las plazas **no tienen jornada real** (G-11 verificado)

`WorkdayCode` es un código libre, no una jornada con horas. Por eso el modo horas (`usesWorkSchedule`) se valora con
`CompensatoryTimeStandardDailyHours` (null → 8), y **no** con la plaza.

### 0.4 El «ojo repetido» de los REQs anteriores **no aplica**

REQ-011 **no sugiere nada a la liquidación** (el descuento se calcula y va al insumo de planilla; no hay saldo
pendiente que arrastrar al finiquito). ⇒ **Cero toques a `ResolveClass`** y cero conceptos de liquidación.

### 0.5 Seeds

- **`-9960…-9969`** para este REQ (estados, ActionType).
- **`-9950…-9959` quedó LIBRE** (REQ-010 no consumió ninguno) → el piso global real es **libre desde `-9950`**.
- **ActionType NUEVO `TIEMPO_NO_TRABAJADO`** (P-20). ⚠️ **NO reutilizar `PERMISO = -9479`**: ese queda reservado
  al futuro módulo de *solicitudes* de permiso. Los ActionTypes viven en `GlobalCatalogSeedData.GetActionTypeCatalogItems()`.

---

## 1. PR-1 — El maestro + la plantilla + los permisos

### 1.1 `NotWorkedTimeType` — los 10 campos literales del levantamiento

Entidad `Domain/Leave/NotWorkedTimeType.cs : TenantEntity` (**molde `IncapacityRisk`**, mismos flags):

| Campo | Tipo | Qué significa |
|---|---|---|
| `Code` / `NormalizedCode` | `string` | único por empresa (índice `(TenantId, NormalizedCode)`) |
| `Name` / `NormalizedName` | `string` | |
| `AppliesToPermission` | `bool` | **clasificación** para el futuro módulo de permisos (P-17: sin lógica hoy) |
| `UsesWorkSchedule` | `bool` | `true` ⇒ el registro se captura **en horas** |
| `CountsHoliday` | `bool` | ⎫ |
| `CountsSaturday` | `bool` | ⎬ los 3 flags del scan — **idénticos a `IncapacityRisk`** |
| `CountsRestDay` | `bool` | ⎭ (el `CountsSeventhDay` del molde) |
| `CountsSeventhDayPenalty` | `bool` | **el séptimo** (§0.2): +1 día por semana afectada |
| `DiscountPercent` | `decimal` | `[0,100]`. **0 = con goce** (sin descuento) · **100 = sin goce pleno** |
| `DeductionConceptTypeCode` | `string` | concepto **Egreso** (obligatorio si `DiscountPercent > 0`) |
| `IncomeConceptTypeCode` | `string?` | concepto Ingreso **opcional** (para tipos con goce parcial) |
| `IsActive`, `ConcurrencyToken` | | |

**Controller** `NotWorkedTimeTypesController` — molde **`CostCenter`** (governed): `GET` (lista + por id) · `POST` ·
`PUT` · `PATCH` · `PATCH …/activate` · `PATCH …/inactivate`. **SIN DELETE** (la baja es lógica, con guard de uso
activo, como `HasActiveUsageAsync`).

### 1.2 La plantilla (`load-template`) — molde `LeaveTemplateSeeder`

`INotWorkedTimeTemplateSeeder` + `POST companies/{companyId}/not-worked-time-configuration/load-template`
(molde `LeaveConfigurationController.cs:27`). **Idempotente por `NormalizedCode`**: la fila existente se **salta**,
nunca se sobrescribe (`created`/`skipped` en la respuesta). Se engancha también en `CompanyProvisioningService`.

Plantilla F1 (4 tipos, del levantamiento):

| Código | % | Flags |
|---|---|---|
| `AUSENCIA_SIN_GOCE` | 100 | no cuenta asueto/sábado/descanso; **séptimo SÍ** |
| `AUSENCIA_CON_GOCE` | 0 | (sin descuento — el registro es documental) |
| `SUSPENSION_CON_DESCUENTO` | 100 | séptimo SÍ |
| `LLEGADA_TARDIA` | 100 | **`UsesWorkSchedule = true`** (se captura en horas); séptimo NO |

### 1.3 Estados + ActionType + permisos

- Catálogo `not-worked-time-statuses`: **`REGISTRADO` `-9960`** · **`ANULADO` `-9961`** (P-16: **sin decisión** en F1
  — el hecho ya ocurrió, como la incapacidad).
- **ActionType `TIEMPO_NO_TRABAJADO` `-9965`** en `GetActionTypeCatalogItems()`.
- Permisos `View/ManageNotWorkedTimes` (**sin `Authorize*`** — P-16) + `ManageNotWorkedTimeTypes` para el maestro.
  ⚠️ **Registrarlos ANTES de que aterricen los controllers** o el governance test truena.

**Migración M1**.

---

## 2. PR-2 — El motor puro (**el gate del REQ**)

`Application/Features/PersonnelFiles/Absences/NotWorkedTime.Rules.cs` — **puro** (sin I/O, sin reloj):

```csharp
public sealed record NotWorkedTimeCalculationInput(
    DateOnly StartDate, DateOnly EndDate,
    bool CountsRestDay, bool CountsSaturday, bool CountsHoliday, bool CountsSeventhDayPenalty,
    bool UsesWorkSchedule, decimal? Hours,           // modo horas: el rango es 1 día y se digita la duración
    decimal DiscountPercent,
    IReadOnlySet<DateOnly> Holidays, DayOfWeek RestDay,
    decimal MonthlyBaseSalary, decimal StandardDailyHours);

public sealed record NotWorkedTimeCalculationResult(
    int CalendarDays,
    int ComputableDays,          // los que sobreviven al scan
    int SeventhDayPenaltyDays,   // +1 por SEMANA AFECTADA
    decimal DiscountedDays,      // computables + séptimos
    decimal DailySalary,         // salario/30, redondeado UNA vez
    decimal DiscountAmount,      // round2(discountedDays × dailySalary × % / 100)
    IReadOnlyList<NotWorkedTimeDayDetail> Details);
```

**Las 6 reglas y sus dorados (el gate):**

| # | Regla | Por qué carga peso |
|---|---|---|
| 1 | **`IsExcluded` es EL MISMO predicado** que el de incapacidades (descanso/sábado/asueto según flags; el descanso es `RestDay`, **NO domingo hardcodeado**) | Divergir aquí haría que dos módulos cuenten días distintos para el mismo calendario |
| 2 | **Séptimo = +1 día por SEMANA AFECTADA** (P-18). Una semana está afectada si tiene ≥1 día computable | La regla NUEVA. Golden: lun–vie (5 computables) ⇒ 5 + **1** = **6** días descontados |
| 3 | **`DiscountPercent = 0` ⇒ descuento 0**, aunque haya días | Es el «con goce»: el registro existe, el dinero no se toca |
| 4 | **`dailySalary = Round2(salario / 30)`, redondeado UNA sola vez**; todo monto deriva de esa cifra | Redondear al final da centavos distintos — el molde ya lo fijó así |
| 5 | **Modo horas** (`UsesWorkSchedule`): `días = horas / standardDailyHours` (default 8) | Una llegada tardía de 2 h con jornada de 8 h descuenta **0.25 días**, no 1 |
| 6 | **Salario 0 ⇒ descuento 0** (no reventar, no dividir por cero) | Un empleado sin salario configurado no debe romper el registro |

**Suite dorada obligatoria** (~20 casos) — es lo que se le enseña al contador.

---

## 3. PR-3 — El registro end-to-end

`PersonnelFileNotWorkedTime` (dominio) + flujo **directo**, sin decisión (P-16):

```
REGISTRADO ──(anular)──> ANULADO
```

- **Snapshot al registrar**: el tipo puede cambiar mañana; el registro guarda su `code`, su `%`, sus flags **y el
  resultado del cálculo** (días, séptimos, monto, `DetailJson`). Es el mismo principio que la huella de REQ-010.
- **Cálculo automático** (RF): el handler resuelve los insumos (salario, día de descanso, asuetos) y llama al motor.
  El monto **no lo digita el usuario**.
- **Asiento en el journal**: `TIEMPO_NO_TRABAJADO` / `APLICADA`, `isSystemGenerated: true` (molde
  `AddIncapacityJournalAsync`). **Anular el registro ⇒ `action.Annul()`** (el mutador ya existe, REQ-003).
- **Costura de biometría** (P-21, sin construir): campo `OriginCode` = `MANUAL` hoy, `MARCACION` mañana.

**Migración M2**.

---

## 4. PR-4 — Bandeja, insumo, disponibilidad y cierre

1. **Bandeja corporativa** `POST companies/{id}/not-worked-times/query` (molde REQ-008/009: `StatusCounts` +
   totales, ambos **sobre todos los estados**) + **export**.
2. **Insumo de planilla** `GET …/payroll-input/export` — **rango OBLIGATORIO** (falta un extremo → 422), **excluye
   los ANULADOS** (P-19: insumo propio; la costura hacia REQ-009 queda documentada como F2).
3. **Fuente de «Disponibilidad de tiempo»** — exactamente **3 toques + 1 impl** (el chasis lo anticipa):
   - `TimeAvailability.cs`: const `TIEMPO_NO_TRABAJADO` + meterla en `ActiveSources`;
   - `IPersonnelTransactionRepository`: método fuente nuevo;
   - `TimeAvailability.Handlers.cs` (`CollectRowsAsync`): un bloque `if (IsIncluded(...))`.
   - **El contrato wire NO cambia** — `categoryCounts`, orden, paginado y export salen gratis.
4. **openapi** por volcado del swagger real (**no transcribir a mano**; ⚠️ el script de merge solo AÑADE: los
   schemas modificados hay que **reemplazarlos**) + **guía FE**.

---

## 5. Riesgos

| Riesgo | Mitigación |
|---|---|
| **La regla del séptimo no es la que el contador espera** | §0.2 — dorados explícitos + sign-off como ítem de negocio en el checklist. Se corrige el cálculo, no el modelo. |
| **Divergir del scan de incapacidades** | Regla 1 de §2: el predicado se calca. Test que compara ambos motores sobre el mismo calendario. |
| **Empresas sin asuetos / sin día de descanso configurados** | El cálculo degrada a domingo (la cadena ya lo hace) — pero el resultado sería silenciosamente distinto: **ítem de despliegue** (verificar asuetos y `RestDayOfWeek` en las empresas piloto). |
| Redondeo | Regla 4: `Round2` una sola vez sobre el diario, como el molde. |
