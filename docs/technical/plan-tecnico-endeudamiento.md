# Plan técnico — Endeudamiento del empleado (REQ-010)

| | |
|---|---|
| **Requerimiento** | REQ-010 — Endeudamiento: parámetros, validación con confirmación y consulta/simulación |
| **Análisis** | [`analisis-planilla-descuentos-y-endeudamiento.md`](../business/analisis-planilla-descuentos-y-endeudamiento.md) — **Plan 3**, RATIFICADO 2026-07-12 (D-16/D-17; RF-020…RF-023; P-11…P-15) |
| **Rama** | `feature/planilla-descuentos` (acumulada con REQ-008 y REQ-009) |
| **Línea base** | HEAD `853f886` · build 0/0 · unit **2713/2713** |
| **PRs** | 3 |
| **Depende de** | **REQ-008** (los descuentos cíclicos son la carga y el punto de enganche) — ya construido en esta rama |

---

## 0. Lo que hay que saber antes de escribir la primera línea

### 0.1 ⚠️ El hallazgo que cambia el diseño: **no existe la mensualización**

El análisis asumía que `MonthlyBaseSalary` (`SettlementRepository.cs:89-126`) entrega un salario **mensualizado**. **No lo hace.** Toma `concept.Value` **crudo** y ni siquiera proyecta `PayPeriodCode`:

```csharp
var monthlyBaseSalary = instances
    .Where(item => item.Nature == CompensationNature.Ingreso
        && item.CalculationType == CompensationCalculationType.Fixed
        && baseSalaryCodes.Contains(item.ConceptTypeCode.ToUpperInvariant()))
    .Select(item => (decimal?)item.Value)
    .FirstOrDefault();
```

El factor **×4.33 semanal de P-11 no existe en el código** (cero hits en `src/`): vive solo en el análisis. Consecuencia dura:

> **La mensualización es CÓDIGO NUEVO de REQ-010, y NO se implementa tocando `MonthlyBaseSalary`.**
> Ese valor alimenta el **motor de liquidación** (y su gemelo alimenta el de vacaciones): cambiarle la semántica
> alteraría cálculos de finiquito **certificados**. REQ-010 deriva **su propia** base de ingreso, en su propio
> método de repositorio, y deja los dos consumidores existentes intactos.

Regla de mensualización de REQ-010 (RN-13): `mensual = valor × PeriodsPerYear(payPeriodCode) ÷ 12`
→ MENSUAL ×1 · QUINCENAL ×2 · SEMANAL ×52/12 = **4.3333…** (el "×4.33" del análisis, sin redondear el factor).

### 0.2 El "ojo repetido" del backlog **no aplica aquí**

REQ-008 y REQ-009 tuvieron que registrar su concepto en el brazo `Descuento` de `ResolveClass` (el switch cae por
`default` en `Ingreso`). **REQ-010 no toca el motor de liquidación en absoluto**: no sugiere líneas, no crea
conceptos de liquidación, no persiste montos a cobrar. Es **validar + consultar**. Cero toques a
`SettlementCalculation.Rules.cs`.

### 0.3 Seeds: **REQ-010 no consume ninguno**

El bloque `-9950…-9959` estaba reservado "por holgura". Al aterrizar el diseño no hace falta:
- los **parámetros** son tablas *tenant* (no catálogos globales),
- los **permisos** viven en `ProvisioningConstants` (no en el espacio de IDs de catálogo),
- **P-09 ratificó que NO hay catálogo de familias** de descuento (la clasificación `Nature`+`DeductionClass` cumple ese rol).

→ **El bloque `-9950…-9959` queda libre.** Piso global sigue en `-9949`; **libre desde `-9950`**.

### 0.4 Retrocompatibilidad: **sin parámetros no hay validación**

Es la propiedad que hace este REQ seguro de desplegar: si la empresa no configuró `MaxIndebtednessPercent` **ni**
una fila de límite por tipo, el chequeo **no corre** y los descuentos se registran exactamente como hoy. Es opt-in
por configuración, no por feature-flag. Todos los tests de REQ-008 deben quedar **verdes sin editarlos**.

### 0.5 El gap ratificado (P-13) que este REQ salda — y el que NO salda

Los descuentos registrados **antes** de REQ-010 **no se re-validan retroactivamente**: solo aparecen en la consulta
(que sí los suma a la carga). No hay migración de datos ni barrido. Ratificado y documentado.

---

## 1. Alcance

| RF | Qué | PR |
|----|-----|----|
| RF-020 | Parámetros: `MaxIndebtednessPercent` global (preferencia, **PUT**) + tabla `IndebtednessLimit` por tipo de descuento | PR-1 |
| RF-021 | Validación al **crear** y al **autorizar** un descuento cíclico, con **override confirmado y auditado** | PR-2 |
| RF-022 | Consulta del nivel de endeudamiento por empleado (base, carga desglosada, %, límites, semáforo, overrides) | PR-3 |
| RF-023 | **Simulación** sin persistencia | PR-3 |

**Fuera de alcance (F2, ratificado):** ingresos adicionales en la base (solo salario base — P-11) · descuentos
**eventuales** en la carga (**P-12: fuera**; solo cíclicos `VIGENTE` no estatutarios) · consulta **self** del empleado
(P-15) · neto en vez de bruto (P-11).

> ⚠️ **P-12 tiene una consecuencia directa: los hooks van SOLO en REQ-008 (cíclicos).** No se toca REQ-009.

---

## 2. PR-1 — Parámetros + permisos

### 2.1 Preferencia global (5 toques, molde exacto de `RecurringDeductionDefaultInterestRatePercent` de REQ-008)

| # | Archivo | Qué |
|---|---------|-----|
| 1 | `Domain/Preferences/CompanyPreference.cs` | `public decimal? MaxIndebtednessPercent { get; private set; }` + setter rico `SetIndebtednessPolicies(decimal?)` (valida `(0,100]`, refresca `ConcurrencyToken`) |
| 2 | `Application/Features/Preferences/Company/CompanyPreferenceAdministration.cs` | el campo en `CompanyPreferenceResponse` y en `UpdateCompanyPreferencesCommand` |
| 3 | idem, validator | `RuleFor(c => c.MaxIndebtednessPercent).GreaterThan(0m).LessThanOrEqualTo(100m).When(c => c.MaxIndebtednessPercent.HasValue)` |
| 4 | idem, handler del PUT (`:264-265`) | `preference.SetIndebtednessPolicies(command.MaxIndebtednessPercent);` justo después de `SetRecurringDeductionPolicies` |
| 5 | `Api/Controllers/CompanyPreferencesController.cs` | el campo en el request record (default `= null`) y en el mapeo |

> **El PATCH es scalar-only** (`CompanyPreferencePatchState` solo tiene `CurrencyCode`/`TimeZone`) → el campo es
> **PUT-only**. No se toca el PATCH.

### 2.2 Tabla `IndebtednessLimit` (molde `IncomeTaxWithholdingBracket`)

**Entidad** `Domain/Compensation/IndebtednessLimit.cs` : `TenantEntity`

| Campo | Tipo | Nota |
|-------|------|------|
| `RecurringDeductionTypeCode` | `string` | normalizado UPPER; **el tipo del catálogo de REQ-008** |
| `MaxPercent` | `decimal` | `numeric(11,8)`; `(0,100]` |
| `IsActive` | `bool` | baja lógica |
| `ConcurrencyToken` | `Guid` | |

**EF config** `Infrastructure/Persistence/Configurations/Compensation/IndebtednessLimitConfiguration.cs`:
- tabla `indebtedness_limits` + check `ck_indebtedness_limits__percent` (`max_percent > 0 and max_percent <= 100`)
- `uq_indebtedness_limits__public_id`
- **índice único** `uq_indebtedness_limits__tenant_type` sobre `(tenant_id, recurring_deduction_type_code)` **`WHERE is_active`** — "un límite por tipo" (RN de RF-020)
- `DbSet` en `ApplicationDbContext`

**Repo** `IIndebtednessRepository` / `IndebtednessRepository` (+ DI):
```csharp
Task<IReadOnlyCollection<IndebtednessLimitResponse>> GetLimitsAsync(Guid tenantId, CancellationToken ct);
Task ReplaceLimitsAsync(Guid tenantId, IReadOnlyCollection<IndebtednessLimit> limits, CancellationToken ct);
```
(delete+add sin `SaveChanges` — lo commitea el handler, como el de brackets).

**CQRS + controller** `IndebtednessParametersController`:
- `GET /api/v1/indebtedness-limits` — policy `ViewIndebtedness`
- `PUT /api/v1/indebtedness-limits` — **replace-all**, policy `ManageIndebtednessParameters`
- Validación de handler: cada `recurringDeductionTypeCode` debe existir y estar **activo** en el catálogo de tipos
  de REQ-008 → 422 `INDEBTEDNESS_LIMIT_TYPE_INVALID`. Sin esto se configurarían límites sobre tipos fantasma.

### 2.3 Permisos (⚠️ **antes** de que existan los controllers, o el governance test truena cuando aterricen)

`ViewIndebtedness` · `ManageIndebtednessParameters` — receta de 5 toques
(`ProvisioningConstants` → `PersonnelFilePermissionCodes` → `PersonnelFilePolicies` → registro en `Program.cs` →
gates `EnsureCanViewIndebtednessAsync` / `EnsureCanManageIndebtednessParametersAsync` en
`IPersonnelFileAuthorizationService`) + alta en la allow-list de `AuthorizationPolicyConventionGovernanceTests`.

**Admin fallback estándar** (no son `Authorize*`: aquí Admin **sí** cubre ambos).

**Migración M1** `AddIndebtednessConfiguration` (columna de preferencia + tabla).

**Suites al cerrar PR-1:** build 0/0 · unit · integración dirigida `~Indebtedness` (seeding + guardrail de policies + CRUD de límites).

---

## 3. PR-2 — El motor + los hooks (el corazón del REQ)

### 3.1 `IndebtednessRules` — módulo puro (`Application/Features/PersonnelFiles/Compensation/Indebtedness.Rules.cs`)

```csharp
public static decimal Round2(decimal value);                       // half-up away-from-zero — LA regla del módulo

/// mensual = valor × PeriodsPerYear(code) ÷ 12   (MENSUAL ×1 · QUINCENAL ×2 · SEMANAL ×52/12 · UNICA ×1/12)
public static decimal Monthlyize(decimal amount, string payPeriodCode);

public sealed record IndebtednessAssessment(
    decimal BaseIncome,          // Σ salario base mensualizado de plazas ACTIVAS
    decimal CurrentLoad,         // Σ cuota mensualizada de cíclicos VIGENTE no estatutarios
    decimal NewInstallment,      // 0 en la consulta; la cuota mensualizada del candidato en la validación
    decimal ProjectedPercent,    // (CurrentLoad + NewInstallment) / BaseIncome × 100  — 2 dec
    decimal? LimitPercent,       // el límite APLICABLE (por tipo si hay fila; si no, el global; si no, null)
    string? LimitSource,         // "TIPO" | "GLOBAL" | null
    bool IsExceeded);            // false cuando LimitPercent es null (sin parámetros → sin control)

public static IndebtednessAssessment Assess(
    decimal baseIncome,
    IReadOnlyCollection<IndebtednessLoadItem> load,   // cuota + frecuencia por descuento vigente
    decimal? newInstallment, string? newPayPeriodCode,
    decimal? globalLimitPercent,
    IReadOnlyDictionary<string, decimal> limitsByType,
    string? candidateTypeCode);
```

**Reglas que el motor debe honrar (y sus tests dorados):**

| # | Regla | Test |
|---|-------|------|
| 1 | **`baseIncome == 0` ⇒ `IsExceeded = false`** y `ProjectedPercent = 0`. Sin salario no hay porcentaje: dividir daría ∞ y bloquearía a un empleado sin salario configurado. **Nunca dividir por cero.** | dorado |
| 2 | Sin `LimitPercent` aplicable ⇒ **`IsExceeded = false`** (opt-in, §0.4) | dorado |
| 3 | El límite **por tipo prevalece** sobre el global — **aunque sea MÁS PERMISIVO** (RF-020: "un préstamo valida contra 25%", no contra `min(25,30)`) | dorado |
| 4 | Golden del análisis: base **$1,200**, carga **$340**, cuota nueva **$80**, límite **30%** → **35%** → excedido | dorado |
| 5 | Mensualización: SEMANAL $100 ⇒ **$433.33** (×52/12, `Round2`) · QUINCENAL $100 ⇒ $200 · MENSUAL $100 ⇒ $100 | dorado |
| 6 | La comparación es **estricta**: `IsExceeded = ProjectedPercent > LimitPercent` (exactamente en el límite **no** excede) | dorado |

### 3.2 La base y la carga (repositorio — **código nuevo**, §0.1)

`IPersonnelFileEmployeeRepository`:
```csharp
Task<IndebtednessSnapshotData> GetIndebtednessSnapshotAsync(Guid tenantId, long personnelFileId, CancellationToken ct);
```
Devuelve, en **una** ida a la BD:
- **Base**: los `PersonnelFileCompensationConcept` `Ingreso`+`Fixed`+`IsBaseSalary` de las **plazas ACTIVAS** del
  empleado (`PersonnelFileEmploymentAssignment.IsActive`), **proyectando `PayPeriodCode`** (que hoy nadie proyecta) →
  Σ mensualizada. Nota: los conceptos de plaza `null` (nivel empleado) cuentan **una sola vez**, como en el motor de
  liquidación cuando la plaza es principal.
- **Carga**: los `PersonnelFileRecurringDeduction` en estado **`VIGENTE`**, con concepto **no estatutario**, con su
  `InstallmentFrequencyCode` y su **cuota vigente** (`RecurringDeductionRules.InstallmentAmountFor(next, plan)` —
  `ApplicationFrequencyCode` **NO** entra: parte la cuota en cargos, no cambia la carga mensual).
  Los **`SUSPENDIDO` se excluyen** de la carga pero **se devuelven marcados** (la consulta los muestra — P-12).

### 3.3 Los hooks — dónde exactamente

**(a) Al crear/editar** — la costura ya está anotada en el código, `RecurringDeductions.Handlers.cs:206-208`, dentro
de `RecurringDeductionWriteSupport.ResolveAndValidateAsync`, **después** de `NormalizePlan` (la cuota ya se conoce) y
**antes** de persistir. El chequeo cabe literalmente entre `:208` y el `return` de `:210`.

**(b) Al autorizar** — `ResolvePersonnelFileRecurringDeductionCommandHandler.Handle` (`:722`), entre el anti-self
doble (`:775-778`) y la transición `entity.Approve(actingUserId, now)` (`:781-784`).

En ambos: si `IsExceeded` **y** el request **no** trae `acknowledgeIndebtednessExceeded = true` →
**422 `INDEBTEDNESS_LIMIT_EXCEEDED`**. Si lo trae → procede **y estampa la huella**.

> ⚠️ **La cifra del desglose NO viaja en el `detail`** — el localizador lo reemplaza por el mensaje catalogado
> (lección de REQ-009). El desglose viaja como **miembros RAÍZ** del ProblemDetails (`baseIncome`, `currentLoad`,
> `newInstallment`, `projectedPercent`, `limitPercent`, `limitSource`), que el localizador **no** toca.
>
> ⚠️⚠️ **Ojo con la forma del JSON**: el backend escribe estos datos en `ProblemDetails.Extensions`, pero
> `System.Text.Json` **aplana** ese diccionario (`[JsonExtensionData]`) → **NO existe un objeto `extensions` en el
> cuerpo**; `code` y el desglose son miembros de la raíz. (Esto ya mordió: un test de REQ-009 aseveraba
> `extensions.code` y estaba en rojo.) **Verificarlo con un test E2E.**

### 3.4 La huella del override (P-14: "se estampa en **cada** punto que la exigió")

Tabla hija de `PersonnelFileRecurringDeduction`:
`recurring_deduction_indebtedness_overrides` — **una fila por evento de override** (no un flag: el mismo crédito
puede excederse al crear y otra vez al autorizar, con cifras distintas).

| Campo | Nota |
|-------|------|
| `Stage` | `CREACION` \| `AUTORIZACION` (constantes, no catálogo) |
| `AcknowledgedByUserId` | quién confirmó |
| `AcknowledgedUtc` | cuándo |
| `BaseIncome`, `MonthlyLoad`, `NewInstallment`, `ProjectedPercent`, `LimitPercent`, `LimitSource` | **el snapshot del momento** — los parámetros cambian; la huella no |

Se expone en la ficha del descuento (`RecurringDeductionResponse.IndebtednessOverrides[]`) y en la consulta.
**Contrato aditivo** — no rompe al FE.

**Migración M2** `AddIndebtednessOverrides`.

**Suites al cerrar PR-2:** build 0/0 · unit (**los dorados de §3.1**) · **integración `~RecurringDeduction`
completa VERDE SIN EDITARLA** (retrocompatibilidad: sin parámetros configurados, nada cambia) · E2E nuevo:
sin parámetros → registra · con límite excedido → 422 **con el desglose en `extensions`** · reenvío con
`acknowledgeIndebtednessExceeded` → registra **y la huella queda** · el override al **autorizar** se estampa aparte.

---

## 4. PR-3 — Consulta, simulación y cierre

### 4.1 Consulta (RF-022) — `GET /api/v1/personnel-files/{fileId}/indebtedness` · policy `ViewIndebtedness`

```jsonc
{
  "baseIncome": 1200.00,
  "baseBreakdown": [ { "assignedPositionPublicId": "…", "conceptTypeCode": "SALARIO_BASE",
                       "value": 1200.00, "payPeriodCode": "MENSUAL", "monthlyValue": 1200.00 } ],
  "currentLoad": 340.00,
  "loadBreakdown": [ { "recurringDeductionPublicId": "…", "typeCode": "PRESTAMO_BANCARIO",
                       "financialInstitution": "Banco X", "reference": "PR-001",
                       "installmentAmount": 170.00, "installmentFrequencyCode": "QUINCENAL",
                       "monthlyAmount": 340.00, "statusCode": "VIGENTE", "isIncludedInLoad": true,
                       "limitPercent": 25.00, "limitSource": "TIPO" } ],
  "currentPercent": 28.33,
  "globalLimitPercent": 30.00,
  "limitsByType": { "PRESTAMO_BANCARIO": 25.00 },
  "status": "DENTRO",                    // DENTRO | EXCEDIDO | SIN_CONTROL
  "overrides": [ /* la huella histórica, la más reciente primero */ ]
}
```
Los `SUSPENDIDO` aparecen con `isIncludedInLoad: false` (visibles, **no suman** — P-12).
`status: "SIN_CONTROL"` cuando no hay ningún límite aplicable (§0.4) — es un estado legítimo, no un error.

### 4.2 Simulación (RF-023) — `POST /api/v1/personnel-files/{fileId}/indebtedness/simulation` · `ViewIndebtedness`

Body: `{ baseIncomeOverride?, additionalDeduction: { amount, payPeriodCode, typeCode? } }`
(`baseIncomeOverride` = el "ingreso digitado" del levantamiento; si se omite, se usa el derivado).
Respuesta: el mismo shape + `simulatedPercent` + `wouldExceed` + `limitPercent`.

> 🚨 **"Solo simulación y no debe afectar la planilla" es literal del levantamiento.**
> El handler es un **`IQueryHandler`** (aunque el verbo HTTP sea POST — es POST por el body, no por mutar) y **no
> inyecta `IUnitOfWork`**. **Test de no-escritura obligatorio**: contar filas antes/después y assertar que el
> `ConcurrencyToken` de la preferencia y de todos los descuentos **no cambió**.

### 4.3 Cierre

- `openapi.yaml` **por volcado del swagger real** (receta REQ-008/009: test temporal `GET /swagger/v1/swagger.json`
  + script de inyección — **no transcribir a mano**).
- **Guía FE** `guia-integracion-frontend-endeudamiento.md`. Debe decir explícitamente:
  1. el desglose del 422 va como **miembros raíz** del ProblemDetails (no en `detail`, y **no** bajo un objeto
     `extensions` — ese objeto no existe en el JSON);
  2. el flujo de confirmación es **reenviar el MISMO request** con `acknowledgeIndebtednessExceeded: true`
     (no hay endpoint de confirmación aparte);
  3. **sin parámetros configurados no hay advertencia** — y eso es correcto, no un bug;
  4. `status: "SIN_CONTROL"` se pinta en gris, no en rojo.
- **Certificación**: suite de **integración COMPLETA** (el REQ toca los handlers de REQ-008).

---

## 5. Riesgos

| Riesgo | Mitigación |
|--------|------------|
| **Tocar `MonthlyBaseSalary` y alterar el finiquito** | §0.1 — REQ-010 deriva su **propia** base; los 2 consumidores existentes no se tocan. Gate: la suite de liquidación verde **sin editarla**. |
| **Bloquear un registro que el negocio quiere permitir** | El levantamiento es literal: *advertir, nunca bloquear*. El 422 es **reintentable** con la confirmación. Test explícito. |
| **División por cero** (empleado sin salario base) | Regla 1 de §3.1: base 0 ⇒ `IsExceeded=false`. Dorado. |
| **El desglose no llega al FE** (localizador) | §3.3 — viaja en `extensions`. E2E que lo verifica. |
| **Romper REQ-008** | Sin parámetros ⇒ sin validación. Su suite completa debe quedar verde **sin editarla**. |
