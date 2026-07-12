# Plan técnico — Planilla: descuentos cíclicos (REQ-008)

| | |
|---|---|
| **Fuente** | [`analisis-planilla-descuentos-y-endeudamiento.md`](../business/analisis-planilla-descuentos-y-endeudamiento.md) — **RATIFICADO 2026-07-12** (D-01…D-22; P-01…P-21 respondidas; P-22 eliminada). Este plan implementa el **Plan 1** (RF-001…RF-013); REQ-009/010/011 tienen plan propio al arrancar |
| **Fecha** | 2026-07-12 (anclas verificadas contra HEAD `ca94d8c` — master con REQ-001…007) |
| **Estado** | **Escrito** — desarrollo pendiente de arranque (REQ-008 = primer 🔴 del backlog) |
| **Módulos** | `PersonnelFiles` (RecurringDeductions + PlanSegments + Installments — net-new) · `GeneralCatalogs` (3 subclases nuevas) · `Compensation` (2 conceptos país faltantes) · Provisioning (3 permisos RBAC) · Preferences (1 columna nueva — tasa default P-03) · Settlements (**bloque de sugerencia espejo + 1 toque de 1 línea en `ResolveClass` + hooks**) · Reporting/Export · Localization · Auditoría. **Sin storage nuevo**, **sin ActionTypes nuevos** (D-22), **sin centro de costo** (D-13/P-08), **sin validación de endeudamiento** (P-13 — llega con REQ-010), **sin tocar** `PersonnelFilePayrollTransaction` ni `PersonnelFileCompensationConcept` (RN-14/RN-16) |
| **Rama** | `feature/planilla-descuentos` (desde `master`; acumulará REQ-008…010) |
| **Migraciones** | **M1** (PR-1: 3 catálogos + 2 conceptos país + concepto liquidación `-9928` + columna de preferencia) · **M2** (PR-2: 3 tablas de dominio + índices/CHECKs) |
| **Endurecimientos de la ratificación** | Tasa de interés **default configurable por empresa** (P-03 — preferencia, vía PUT) · extraordinaria = **reducir plazo**, no sobre suspendidos (P-04) · meses de excepción **corren el plan**, sin catálogo BD (P-05) · división entera aplicación ≥ cuota, inversas → 422 (P-06) · institución financiera **texto libre, obligatoria para tipos externos** (P-07) · **sin centro de costo** (P-08) · sin catálogo de familias (P-09) · sembrar `COOPERATIVA`/`PROCURADURIA` (P-10) · **sin adopción/fallbacks/legacy** (P-22 eliminada — data de prueba) |

---

## 0. Aclaraciones quirúrgicas (verificadas contra el código, 2026-07-12)

1. **Frontera P-01 heredada**: el módulo **registra, aplica y exporta** — cero cálculo de nómina. El único punto que "cobra" es la **línea de deducción sugerida** en el motor de liquidación (№7/№8). El modelo queda listo para el futuro motor (`originCode=MOTOR` sobre los mismos mutadores).
2. **El molde REQ-005 está EN MASTER y es as-built** (gana sobre su plan): entidad en **archivo propio** `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileRecurringIncome.cs` (agregado `:77-723`, hija `:732-911`, constantes `:12-62`) — las 3 entidades nuevas van en `PersonnelFileRecurringDeduction.cs` espejo. Reglas puras `Features/PersonnelFiles/Compensation/RecurringIncomes.Rules.cs:124-470` (`Round2:142`, `NormalizePlan:155`, `InstallmentAmountFor:233`, `BuildProjection:266`, `CanApplyInstallment:347`, `RemainingAmount:378`, `CanTransition:419`, `ValidateSettlementAction:448`). Repo: 18 métodos en `IPersonnelFileEmployeeRepository.cs:1142-1297` (espejarlos con `RecurringDeduction*`); impl desde `PersonnelFileEmployeeRepository.cs:5315`; **idioma Add/AsNoTracking** (`:1393-1403`).
3. **`[AuthorizationPolicySet]` es CLASS-ONLY** → trío de controllers espejo exacto: `RecurringIncomesController.cs` (CRUD+ciclo+cuotas, policy `View`,`Manage`) · `RecurringIncomeResolutionController.cs:25` (`View`,`Authorize`) · `RecurringIncomesReportingController.cs:21` (company-wide **sin** policy-set, gates por handler).
4. **`Authorize*` sin Admin**: la exclusión vive en la **policy** (`RequireAssertion` — molde `PersonnelFilePolicies.cs:296-299` de `AuthorizeRecurringIncomes` + registro en `Program.cs`); code nuevo `PersonnelFiles.AuthorizeRecurringDeductions` + gate fail-closed + governance test.
5. **Anti-autoaprobación DOBLE** (D-05 — sin solicitante en este módulo): molde exacto en la resolución de ingresos cíclicos (sujeto `LinkedUserPublicId` + registrador `RegisteredByUserId` → 403 `SELF_APPROVAL_FORBIDDEN`); re-verificación de `EN_REVISION` en transacción.
6. **Catálogos generales NO son TPH** — receta de subclase (8 toques): clase `sealed` en `GeneralCatalogItems.cs` + config `GeneralCatalogItemConfigurationBase<T>` (auto-descubierta) + `DbSet` + getter de seed en `GlobalCatalogSeedData` + wire key en `GeneralCatalogKeyMap` + categoría en `PersonnelReferenceCatalogs` + 2 switch en `PersonnelFileRepository` (lectura + `CatalogCodeIsActiveAsync`) + migración `HasData`.
7. **⚠️ HALLAZGO NUEVO — `ResolveClass` exige un toque de 1 línea al motor** (verificado `SettlementCalculation.Rules.cs:396-402`): es un switch cerrado — `ISSS/AFP/RENTA/DESCUENTO_EXTERNO/OTRO_DESCUENTO → Descuento`, patronales → `PagoPatronal`, **default → `Ingreso`**. Sin el toque, la sugerencia del saldo se clasificaría como INGRESO (pagaría en vez de descontar). Cambio: (a) constante `DescuentoCiclicoPendiente = "DESCUENTO_CICLICO_PENDIENTE"` en `SettlementConceptCodes` (`:724-742`, junto a `IngresoCiclicoPendiente:735`); (b) añadirla al brazo `Descuento` de `ResolveClass`. Es el **único** cambio al motor puro; 100 % aditivo (los cases existentes no se tocan) — a diferencia de REQ-005 que necesitó cero ediciones del motor. La suite de liquidación existente debe seguir verde sin edición.
8. **La línea de deducción manual YA fluye por el motor** (verificado): `BuildSuggestedSpecs:371-373` convierte cada `PlazaItem` en `LineSpec(IsManual:true, ManualAmount, Description, CounterpartyName)` con clase de `ResolveClass`; `ComputeDeductionLine:563` la resuelve por la **rama default `:621-625`** (`calculated = Round2(spec.ManualAmount)`, detail `"Última cuota — {counterparty}"` o `"Monto manual"`). Neto = ingresos − descuentos (`:288`, warning si < 0 `:291-294`). Refinamiento cosmético opcional (№2 de §7): mini-`case` para `DESCUENTO_CICLICO_PENDIENTE` con detail `"Saldo pendiente — {institución}"` (el default diría "Última cuota", engañoso).
9. **Bloque de sugerencia = espejo literal del cíclico de ingresos** (`SettlementRepository.GetCalculationContextAsync:221-255`): mismo gate `isPrincipalPlaza` (`:206`), filtro `StatusCode==VIGENTE && SettlementActionCode==DESCONTAR_SALDO`, saldo > 0 → `SettlementSuggestedItemDto(ConceptCode, Description, Amount, CounterpartyName)` (`ISettlementRepository.cs:18-22`) con `CounterpartyName = financial_institution`. **Saldo a sugerir**: sin interés = `Σ segmentos − Σ cuotas activas APLICADA` (espejo `:238-245`); **con interés = capital pendiente** de la tabla (el payoff no cobra intereses futuros; la línea es editable si el banco cotiza distinto) — caso dorado del contador.
10. **Hooks al emitir/anular = espejo literal** (`Settlements.Handlers.cs:1027-1031` / `:1160-1164`): al emitir → `FinalizeBySettlement(settlementPublicId, now)` de **todos** los `VIGENTE` del expediente (con `DESCONTAR_SALDO` quedan saldados; con `CANCELAR` = **condonación**, sin línea); al anular → `ReopenFromSettlement` **solo** de los que tengan `closed_by_settlement_public_id == esa liquidación`. 2 métodos de repo espejo (`GetVigenteRecurringDeductionsForSettlementAsync` / `GetRecurringDeductionsClosedBySettlementAsync`). La reversión de retiro no se toca.
11. **Lock anti-carrera**: `AcquireRecurringDeductionMutationLockAsync(publicId)` — classId ASCII nuevo **`"RDED"` = `0x52_44_45_44`** (molde `RecurringIncomeMutationLockClassId = "RINC"`, `PersonnelFileEmployeeRepository.cs:3740-3749`); el handler abre la transacción antes; el lote toma locks **ordenados por Id** (anti-deadlock).
12. **Segmentos del plan (delta №1)**: tabla hija de **definición** (no confundir con las cuotas aplicadas): tramos contiguos desde 1, sin huecos/solapes; editable **solo `EN_REVISION`** con semántica replace-all en el `Update`; **con interés → cero segmentos** (el plan se **deriva** de principal+tasa+n, nunca se materializa — misma filosofía D-07 "proyección derivada"); sin interés → ≥ 1 segmento (finito: `to_installment` del último cierra el plan; indefinido: exactamente 1 segmento abierto `to_installment NULL`).
13. **Amortización (delta №2)**: sistema **francés**, tasa **nominal anual ÷ periodos de la frecuencia de cuota** (P-03: MENSUAL n/12, QUINCENAL n/24, SEMANAL n/52); cuota fija = `P·i/(1−(1+i)^−n)` con `Round2` half-up **único** (reutilizar el patrón `Round2` del molde); por cuota: interés = `Round2(saldo·i)`, capital = cuota − interés; **última cuota ajusta contra el capital restante exacto** (Σ capital = P). **Extraordinaria → reducir plazo** (P-04): regenerar la tabla **desde el saldo** con la misma cuota (la última se ajusta). El desglose se **snapshotea al aplicar** (`capital_amount`/`interest_amount` en la cuota hija); la tabla proyectada es derivación pura. **Golden del contador = GATE de PR-2** (A.3 casos 1/3/4 + saldo-para-liquidación).
14. **Frecuencias y meses de excepción (delta №3)**: la **frecuencia de aplicación** manda en proyección/lote/insumo; si difiere de la de cuota → **división entera** (P-06: cuota MENSUAL aplicada QUINCENAL = 2 partes `Round2(valor/2)`, la última parte ajusta; combinaciones inversas — aplicación más lenta que la cuota — → 422 `APPLICATION_FREQUENCY_INVALID`); validación **blanda** del par con la planilla de la plaza (warning, no bloqueo). **Meses de excepción** (P-05): multi-valor 1..12 **sin catálogo BD**; la fecha teórica que caiga en mes exceptuado **se corre** al siguiente periodo hábil del plan (el plan se alarga; el crédito se debe completo).
15. **Preferencia de tasa default (P-03 — ajuste de la ratificación)**: columna nullable nueva en `CompanyPreference` + setter rico `SetRecurringDeductionPolicies` + cableado en el **PUT** (`UpdateCompanyPreferencesCommand`/handler `CompanyPreferenceAdministration.cs:235-259`) — **el PATCH es scalar-only** (`:317-320`, solo currency/timezone) y NO se toca. Patrón verificado: nullable = "sin default", se resuelve al consumir (precarga del formulario en FE; el backend solo la expone — la tasa persistida es siempre la del crédito).
16. **Endeudamiento NO va en este REQ (P-13)**: sin chequeo adelantado. Los **puntos de costura quedan localizados** para REQ-010: el handler de create (post-validaciones, pre-save) y el de resolution (pre-`Approve`) — anotarlos con comentario de diseño; las columnas de huella del override las añade REQ-010 (aditivo).
17. **Conceptos país faltantes (P-10)**: sembrar `COOPERATIVA` y `PROCURADURIA` como `CompensationConceptTypeCatalogItem` `Nature=Egreso`, `DeductionClass=Externo`, `Fixed` — serie de conceptos `-9720…-9736` ocupada → usar **`-9737`/`-9738`** (verificar libres al abrir PR-1; son del catálogo de conceptos, NO del bloque `-9920…-9939` del módulo).
18. **openapi a mano** (`docs/technical/api/openapi.yaml`, skill `.agents/skills/update-api-reference-openapi/`); guardrails `OpenApiContractGuardrails*`. PR-1 anota el wire key; las rutas al cierre (PR-6) vía skill. **Gotchas memorados**: `[Required]` con target de **parámetro** en records posicionales; los defaults de ctor de records NO aplican en deserialización (validators explícitos para `isIndefinite`/`usesCompoundInterest`/plan); user IDs en response = `Guid?`; `DOTNET_ROLL_FORWARD=Major` para `dotnet ef`; trampa de fragmentos GUID en `Designer.cs` al verificar seeds (buscar con sufijo `L`).

---

## 1. Alcance del plan

Implementa RF-001…RF-013 del análisis ratificado (Plan 1). Fuera: descuentos eventuales (REQ-009), endeudamiento (REQ-010 — costura №16), tiempos no trabajados (REQ-011), motor de planilla, maestro de instituciones financieras (P-07 F2), autoservicio, notificaciones, adopción/migración de datos (P-22 eliminada). Todo aditivo salvo el toque №7 (1 línea, retrocompatible); cero breaking changes.

---

## 2. Modelo de datos (M2)

### `personnel_file_recurring_deductions` (`PersonnelFileRecurringDeduction : TenantEntity`)

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `personnel_file_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Cabecera | `effective_date` (**vigencia — puede ser futura**, D-04), `reference` (200, **NOT NULL** — referencia crediticia, ancla de extraordinarias), `recurring_deduction_type_code` (80 — catálogo nuevo), `concept_type_code` (80) + `concept_name_snapshot` (200) — concepto país `Nature=Egreso` **no estatutario** (RN-04), `financial_institution` (200, NULL — **obligatoria por handler si el tipo es externo**: préstamo/procuraduría/cooperativa, P-07), `observations` (1000, NULL) |
| Plaza (D-13) | `assigned_position_public_id` (**NOT NULL**, default plaza principal resuelta en handler) — **SIN centro de costo** (P-08) |
| Plan | `installment_start_date`, `exception_months` (multi-valor 1..12 — representación №1 de §7), `currency_code` (3), `payroll_type_code` (80), `installment_frequency_code` (80 — `pay-periods`), `application_frequency_code` (80 — `pay-periods`, №14), `is_indefinite` (bool), `settlement_action_code` (80 — `DESCONTAR_SALDO`/`CANCELAR`) |
| Interés (D-08) | `uses_compound_interest` (bool), `principal_amount numeric(18,2) NULL`, `interest_rate_percent numeric(9,4) NULL` (nominal anual), `planned_installments int NULL` |
| Flujo | `status_code` (80), `registered_by_user_id`, `decided_by_user_id`/`decided_utc`/`decision_note` (500), `suspended_utc`/`suspension_note` (500), `closed_utc`/`closure_reason` (500)/`closed_by_user_id`, `closed_by_settlement_public_id uuid NULL` (№10) |

- CHECKs: `uses_compound_interest = false OR (principal_amount > 0 AND interest_rate_percent > 0 AND planned_installments >= 1)` · `NOT (is_indefinite AND uses_compound_interest)` (el interés exige plan finito) · `principal_amount IS NULL OR principal_amount > 0`.
- Reglas (no CHECK): interés ⊕ segmentos manuales (№12); `DESCONTAR_SALDO` × indefinido → 422 (D-12); coherencia de frecuencias (№14).
- Índices: `(tenant_id, personnel_file_id, status_code)` · `(tenant_id, status_code, payroll_type_code)` · UQ `public_id`.
- Las columnas de huella de endeudamiento (`acknowledged_*`) **NO van aquí** — las añade REQ-010 (№16).

### `personnel_file_recurring_deduction_plan_segments` (`…PlanSegment : TenantEntity` — definición del plan, №12)

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `recurring_deduction_id` (FK), `created_utc`/`modified_utc`, `is_active` |
| Tramo | `from_installment int` (≥1), `to_installment int NULL` (NULL = abierto — solo el último tramo de un indefinido), `installment_value numeric(18,2)` (>0) |

- UQ parcial `(tenant_id, recurring_deduction_id, from_installment) WHERE is_active`; CHECKs: `from_installment >= 1` · `to_installment IS NULL OR to_installment >= from_installment` · `installment_value > 0`. Contigüidad sin huecos/solapes = regla pura (`ValidateSegments`) + guard de dominio; replace-all en `Update` (solo `EN_REVISION`).

### `personnel_file_recurring_deduction_installments` (`…Installment : TenantEntity` — cuota aplicada; espejo del molde + deltas)

| Grupo | Columnas |
|---|---|
| Identidad | espejo del molde (id, public_id, tenant, FK, concurrency, is_active) |
| Cuota | `kind` (20: **`REGULAR`/`EXTRAORDINARIA`**, D-09), `installment_number int NULL` (NOT NULL si REGULAR — secuencia del plan), `extraordinary_number int NULL` (NOT NULL si EXTRAORDINARIA — serie E1, E2…), `applied_date`, `theoretical_due_date NULL` (solo REGULAR), `amount numeric(18,2)` (>0), **`capital_amount numeric(18,2) NULL` / `interest_amount numeric(18,2) NULL`** (snapshot №13 — solo planes con interés; extraordinaria = 100 % capital), `currency_code` (3, snapshot), `payroll_type_code` (80, snapshot), `payroll_period_id bigint NULL` (**FK real** a `payroll_period_definitions` — igual REQ-005/006/007) + `payroll_period_label` (80, snapshot), `origin_code` (20: `MANUAL`/`MOTOR`), `status_code` (20: `APLICADA`/`ANULADA`), `applied_by_user_id`, `annulment_reason` (500)/`annulled_by_user_id`/`annulled_utc`, `notes` (500, NULL) |

- **Índices únicos parciales**: `(tenant_id, recurring_deduction_id, installment_number) WHERE is_active AND kind='REGULAR'` · `(tenant_id, recurring_deduction_id, extraordinary_number) WHERE is_active AND kind='EXTRAORDINARIA'` — la anulación (`is_active=false` + `ANULADA`) permite re-aplicar el número.
- CHECKs: `(kind='REGULAR') = (installment_number IS NOT NULL)` · `(kind='EXTRAORDINARIA') = (extraordinary_number IS NOT NULL)` · `amount > 0` · `capital_amount IS NULL OR capital_amount >= 0` · `interest_amount IS NULL OR interest_amount >= 0` (capital+interés=amount tras redondeo = regla, no CHECK).
- Índices: `(tenant_id, recurring_deduction_id, installment_number)` · `(tenant_id, payroll_type_code, applied_date)` (insumo).

### `CompanyPreference` (M1 — №15)

- Columna nueva `recurring_deduction_default_interest_rate_percent numeric(9,4) NULL` (`RecurringDeductionDefaultInterestRatePercent decimal?` — null = sin default) + `SetRecurringDeductionPolicies(...)` + PUT + response/openapi.

### Dominio (constantes y mutadores custodiados — espejo del molde `:12-62` / `:238-576`)

- `RecurringDeductionStatuses`: `EN_REVISION`, `VIGENTE`, `RECHAZADO`, `SUSPENDIDO`, `FINALIZADO`, `ANULADO` (+ `Editable`/`Applicable`/`Terminal`). `…SettlementActions`: `DESCONTAR_SALDO`/`CANCELAR`. `…InstallmentOrigins`: `MANUAL`/`MOTOR`. `…InstallmentStatuses`: `APLICADA`/`ANULADA`. `…InstallmentKinds`: `REGULAR`/`EXTRAORDINARIA`.
- Mutadores: `Create` (factoría — valida plan/segmentos/interés vía reglas) · `Update` (solo `EN_REVISION`; replace-all de segmentos) · `Approve`/`Reject` · `Suspend`/`Resume` · `Annul` (retiro del trámite o revocación) · `Deactivate` (soft-delete de borrador) · `CloseManually` (indefinidos) · `FinalizeByPlanCompletion` · `FinalizeBySettlement`/`ReopenFromSettlement` · `ApplyInstallment` (REGULAR — secuencia/estado/vigencia) · **`ApplyExtraordinaryInstallment`** (valor ≤ saldo; payoff total → finaliza en la misma transacción) · `AnnulInstallment` (reabre `FINALIZADO→VIGENTE` si el plan deja de estar completo/saldado). Cada mutación rota `ConcurrencyToken`.

---

## 3. Arquitectura

### 3.1 Configuración (PR-1, M1)

- **3 subclases catálogo** (№6) + seeds §4: `RecurringDeductionStatusCatalogItem`, `RecurringDeductionSettlementActionCatalogItem`, `RecurringDeductionTypeCatalogItem` (plantilla editable P-10). Wire keys: `recurring-deduction-statuses`, `recurring-deduction-settlement-actions`, `recurring-deduction-types`. `payroll-types`/`pay-periods`/`currencies` **solo se consumen** (ya sembrados).
- **2 conceptos país faltantes** (№17): `COOPERATIVA=-9737`, `PROCURADURIA=-9738` (`Nature=Egreso`, `Externo`, `Fixed`) — verificar IDs libres.
- **Concepto de liquidación** `DESCUENTO_CICLICO_PENDIENTE=-9928`: `ConceptClass=Descuento`, **`IsSystemCalculated=false`**, `Affects*=false`, `ExemptionRule=Ninguna`, sort ~96 (espejo de parámetros de `-9842 DESCUENTO_EXTERNO`, firma `CreateSettlementConceptSeed:1338-1375`). Puede sembrarse en M1 (una sola migración de seeds) aunque el motor lo consuma en PR-6.
- **Permisos** (D-06): codes `PersonnelFiles.View/Manage/AuthorizeRecurringDeductions` — receta completa (№4): `ProvisioningConstants` + `PersonnelFilePermissionCodes` + policies (`Authorize*` **sin Admin**) + gates `EnsureCanView/Manage/AuthorizeRecurringDeductionsAsync` fail-closed + registro `Program.cs` + governance tests.
- **Preferencia de tasa default** (№15) + openapi temprano (wire keys).

### 3.2 Reglas puras — `RecurringDeductionRules` (PR-2, gate)

Módulo estático puro en `Features/PersonnelFiles/Compensation/RecurringDeductions.Rules.cs` (molde `RecurringIncomeRules`), errores bilingües `RECURRING_DEDUCTION_*` con paridad de localización:

- `ValidateSegments(segments, isIndefinite)` → contiguos desde 1, sin huecos/solapes, valores > 0; indefinido = exactamente 1 tramo abierto; finito = último tramo cerrado (№12).
- `NormalizePlan(...)` → deriva `installmentCount`/`totalAmount` de los segmentos (finito) o de la amortización (interés); indefinido sin totales.
- **`BuildAmortizationSchedule(principal, annualRatePercent, installments, frequency)`** (№13) → cuota fija francesa + filas (nº, cuota, interés, capital, saldo) con `Round2` y ajuste final; **`RecomputeFromBalance(balance, quota, rate, frequency)`** (post-extraordinaria, reducir plazo — P-04).
- `InstallmentAmountFor(n, plan)` → por segmento (última del plan ajusta el remanente del tramo/total) o por amortización.
- `BuildProjection(plan, applied, extraordinarias, from, horizon)` → fechas teóricas por **frecuencia de aplicación** (№14: división entera si difiere de la de cuota), **saltando meses de excepción** (el plan se corre); marca vencidas; con interés incluye desglose capital/interés.
- `SettlementBalance(plan, applied)` → sin interés: `Σ segmentos − Σ aplicadas`; con interés: **capital pendiente** (№9).
- `CanApplyInstallment(status, effectiveDate, today, n, plan)` (vigencia alcanzada + secuencia + no exceder) · `CanApplyExtraordinary(status, value, balance)` (P-04: no sobre `SUSPENDIDO`; ≤ saldo; solo finitos) · `NextInstallmentNumber` / `NextExtraordinaryNumber` · `RemainingAmount` / `IsPlanComplete` (incluye saldado-por-extraordinarias) · `CanTransition` · `ValidateSettlementAction` (P-06 espejo) · `ValidateFrequencyPair` (№14).
- **Gate del PR**: golden A.3 del análisis (casos 1–6 + payoff + saldo-para-liquidación con interés) **bendecidos por el contador ANTES de construir** (checklist §8) como suite `RecurringDeductionRulesTests`.

### 3.3 Application + endpoints (PR-3/PR-4/PR-5)

Archivos en `Features/PersonnelFiles/Compensation/`: `RecurringDeductions.cs` (DTOs/commands/validators — segmentos como colección anidada; user IDs response `Guid?`), `.Handlers.cs`, `RecurringDeductionInstallments.cs` + `.Handlers.cs`, `RecurringDeductionsBandeja.cs` (queries + export rows en español).

| Controller | PolicySet | Rutas (`api/v1/…`) |
|---|---|---|
| `RecurringDeductionsController` | (`ViewRecurringDeductions`, `ManageRecurringDeductions`) | `GET/POST personnel-files/{publicId}/recurring-deductions` · `GET/PUT/DELETE …/{id}` · `PATCH …/{id}/suspension` · `PATCH …/{id}/closure` (cierre manual indefinido) · `PATCH …/{id}/annulment` · `GET …/{id}/schedule` (proyección + **tabla de amortización** + totales cobrado/no cobrado) · `GET/POST …/{id}/installments` (historial + aplicación unitaria) · **`POST …/{id}/extraordinary-installments`** (RF-008; payoff si valor = saldo) · `PATCH …/{id}/installments/{instId}/annulment` |
| `RecurringDeductionResolutionController` (№3) | (`ViewRecurringDeductions`, **`AuthorizeRecurringDeductions`**) | `PATCH …/{id}/resolution` (anti-self doble №5) · `PATCH …/{id}/revocation` |
| `RecurringDeductionsReportingController` (sin policy-set, gates por handler) | — | `POST companies/{companyId}/recurring-deductions/query` · `POST …/pending-installments/query` · `POST …/apply-period` (**gate Manage**) · `GET …/export` · `GET …/pending-installments/export` · `GET …/payroll-input/export` (insumo: incluye institución/referencia/capital-interés; filtros tipo+periodo obligatorios → 400) — exports con `[EnableRateLimiting(Export)]` |

Convenciones: If-Match en todos los writes (+ `parentConcurrencyToken` en sub-recursos), ETag rotativo, errores `extensions.code` bilingües, enums string, auditoría en transacción. **Costura REQ-010** (№16): comentario de diseño en create/resolution handlers.

### 3.4 Aplicación de cuotas y extraordinarias (PR-4 — el corazón operativo)

- **Unitaria**: transacción → `AcquireRecurringDeductionMutationLockAsync` (№11) → recarga tracked + re-verificación (`VIGENTE` + **vigencia alcanzada** + secuencia + mes no exceptuado) → `ApplyInstallment` (monto de reglas — no editable; snapshot moneda/planilla/periodo FK + desglose capital/interés №13; `origin=MANUAL`) → `IsPlanComplete` → `FinalizeByPlanCompletion` misma transacción.
- **Lote por periodo** (`apply-period`): input = `payrollTypeCode` + periodo/rango + exclusiones. Candidatos = proyección por **frecuencia de aplicación** con fecha teórica ≤ fin del periodo (incluye vencidas; excluye suspendidos/no vigentes/meses exceptuados) − excluidos → locks ordenados por Id → aplicar → commit. **Atómico** (conflicto → rollback + 422 con detalle). **Test de carrera obligatorio**.
- **Extraordinaria**: mismo lock → `CanApplyExtraordinary` → `ApplyExtraordinaryInstallment` (serie E, 100 % capital, par planilla/periodo del levantamiento) → **regenerar proyección desde el saldo** (reducir plazo №13) → payoff total → `FINALIZADO` misma transacción.
- **Anulación** (regular o extraordinaria): lock + `AnnulInstallment` (motivo); si el plan deja de estar completo/saldado → `FINALIZADO→VIGENTE` misma transacción.

### 3.5 Integración con liquidación (PR-6 — espejo + 1 toque, №7…№10)

1. Constante + **`ResolveClass`** (№7 — el único toque al motor) y, opcional №2 §7, mini-case de detail en `ComputeDeductionLine`.
2. `SettlementRepository.GetCalculationContextAsync`: bloque espejo de `:221-255` tras el de one-time incomes → `SuggestedItemDto(DESCUENTO_CICLICO_PENDIENTE, "Saldo descuento cíclico — {tipo} {referencia}", SettlementBalance, financial_institution)`.
3. Guard anti-duplicado de la sugerencia (línea manual existente con mismo concepto+referencia → no re-sugerir; espejo del molde).
4. Hooks Issue/Annul (№10) + 2 métodos de repo.
5. **Indefinidos**: `DESCONTAR_SALDO` bloqueado desde el alta (P-06 espejo) → al liquidar solo `CANCELAR` (condonación/cierre).

Retrocompatibilidad: suite de liquidación existente verde sin edición; test nuevo verifica que la línea **reduce el neto** y dispara el warning de neto negativo cuando aplica.

### 3.6 DevSeed + guía FE (PR-6)

- DevSeed **solo si** los empleados DevSeed tienen plaza+salario (lección REQ-007: se omitió por datos incompletos — decidir al llegar; no es gate).
- `guia-integracion-frontend-planilla-descuentos-ciclicos.md`: contratos, 6 estados, segmentos vs interés (formulario dual), tabla de amortización, extraordinarias/payoff, meses de excepción y frecuencias (división), operación por periodo, liquidación (DESCONTAR_SALDO/CANCELAR), preferencia de tasa default, catálogos wire y errores.

---

## 4. Migraciones y seeds

**M1 — `AddRecurringDeductionConfiguration`** (PR-1): 3 tablas de catálogo + `HasData` + 2 conceptos país + concepto de liquidación + columna de preferencia:

| Seed | Códigos → IDs |
|---|---|
| `RECURRING_DEDUCTION_STATUS_CATALOG` | `EN_REVISION=-9920` · `VIGENTE=-9921` · `RECHAZADO=-9922` · `SUSPENDIDO=-9923` · `FINALIZADO=-9924` · `ANULADO=-9925` |
| `RECURRING_DEDUCTION_SETTLEMENT_ACTION_CATALOG` | `DESCONTAR_SALDO=-9926` · `CANCELAR=-9927` |
| Concepto liquidación (`SETTLEMENT_CONCEPT_CATALOG`) | `DESCUENTO_CICLICO_PENDIENTE=-9928` (**clase Descuento, `IsSystemCalculated=false`**) |
| `RECURRING_DEDUCTION_TYPE_CATALOG` (plantilla editable) | `PRESTAMO_BANCARIO=-9930` · `PROCURADURIA=-9931` · `COOPERATIVA=-9932` · `ASOCIACION=-9933` · `OTRO=-9934` |
| Conceptos país (serie de conceptos) | `COOPERATIVA=-9737` · `PROCURADURIA=-9738` (`Egreso`/`Externo`/`Fixed`) |

Holgura del bloque: `-9929`, `-9935…-9939`. **Verificar IDs libres al abrir PR-1 con sufijo `L`** (piso actual `-9915`; trampa GUID №18).

**M2 — `AddRecurringDeductions`** (PR-2): 3 tablas de dominio + índices/CHECKs/índices únicos parciales (§2).

Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add <M> -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · `MigrationSeedingIntegrationTests`.

---

## 5. Pruebas

- **Unitarias**: `RecurringDeductionRulesTests` — golden A.3 **como gate de PR-2** (amortización $1,000/12 %/12 → cuota $88.85, cuota 1 = $10.00 interés + $78.85 capital, Σ capital = $1,000.00 exacto; segmentos 1–6×$50 + 7–12×$75; extraordinaria $200 reduce plazo; payoff; meses de excepción corren; división $100→2×$50 y $33.33/$33.34; saldo-para-liquidación con interés = capital pendiente) + `ValidateSegments` (huecos/solapes/abierto), transiciones, `DESCONTAR_SALDO`×indefinido, `ValidateFrequencyPair`, **paridad de localización** de todos los errores `RECURRING_DEDUCTION_*`.
- **Integración** (`ApiIntegrationTests.RecurringDeductions.cs`): CRUD con segmentos (replace-all solo `EN_REVISION`; hueco → 422) · alta con interés (schedule expone la tabla) · concepto estatutario/`IsBaseSalary` → 422 · institución obligatoria por tipo externo → 422 · vigencia futura (crear+autorizar OK, aplicar → 422 hasta la fecha) · flujo completo con **anti-self doble** + Admin sin grant → 403 · aplicación unitaria/lote con exclusión + **carrera** · mes exceptuado fuera del lote y plan corrido · división de frecuencia en el lote · **extraordinaria** (reduce plazo; anulación reabre; payoff → `FINALIZADO`; > saldo → 422; sobre suspendido → 422) · anulación de cuota con reapertura · retirado (alta 422; pendiente solo rechazo/anulación) · bandejas con `StatusCounts` + **insumo cuadrado** contra pendientes (incluye institución/capital/interés) · **liquidación e2e** (línea Descuento editable/excluible **reduce el neto**; `CANCELAR` condona al emitir; anular reabre; indefinido → solo `CANCELAR`) · **test de no-escritura** (`PersonnelFilePayrollTransaction`/`PersonnelFileCompensationConcept` sin filas nuevas) · **suite de liquidación existente sin regresión** (№7) · preferencia de tasa (PUT + precarga expuesta) · governance/permisos.
- **Guardrails**: openapi (№18) + seeds.

---

## 6. Orden de PRs

- **PR-1 — Configuración (M1)**: 3 catálogos + concepto `-9928` + conceptos país `-9737`/`-9738` + 3 permisos/policies/gates (Authorize sin Admin) + **preferencia de tasa default (PUT)** + governance + openapi temprano. *Verificar seeds libres (sufijo `L`).*
- **PR-2 — Dominio + reglas (M2)**: 3 entidades + mutadores custodiados + EF config/índices/CHECKs + `RecurringDeductionRules` con **amortización y golden del contador en verde (GATE)** + repo (idioma Add/AsNoTracking) + lock `RDED`. *Prerrequisito: casos dorados bendecidos por el contador (§8).*
- **PR-3 — Flujo end-to-end**: `RecurringDeductionsController` (CRUD con segmentos/interés, suspensión, cierre, anulación) + `RecurringDeductionResolutionController` (anti-self doble + revocación) + validaciones (concepto egreso no estatutario, institución por tipo, vigencia futura) + costura REQ-010 anotada (№16).
- **PR-4 — Aplicación + extraordinarias**: `schedule` (proyección+amortización+totales) + unitaria + **lote por periodo** (frecuencia de aplicación, meses de excepción, atómico, carrera) + **extraordinarias/payoff** + anulaciones con reapertura + FK de periodo.
- **PR-5 — Bandejas + exportaciones**: bandeja empresa (`StatusCounts`, totales por moneda) + pendientes/vencidas + 3 exports + **insumo** cuadrado en tests.
- **PR-6 — Integración liquidación + cierre**: №7 (`ResolveClass` + constante ± mini-case detail) + bloque de sugerencia espejo + hooks Issue/Annul + suite E2E + verificación de **no-regresión de la suite de liquidación** + `openapi.yaml` final (skill) + **guía FE** + DevSeed (si aplica).

Cada PR con build 0 err/0 warn + unit verde + integración dirigida `~RecurringDeduction` verde; certificación completa al cerrar el REQ (orquestador). Convención de commits del repo (**sin trailer de co-autoría de IA**).

---

## 7. Riesgos técnicos y decisiones del plan

- **№1 — Representación de `exception_months`** (PR-2): propuesta `integer[]` (Npgsql lo mapea nativo); si la convención del repo no usa arrays, degradar a `varchar(40)` CSV validado (`"1,7,12"`) — decidir al escribir la config EF; el contrato API es `int[]` en ambos casos.
- **№2 — Detail de la línea en `ComputeDeductionLine`** (PR-6, cosmético): la rama default produce `"Última cuota — {counterparty}"` (№8), engañoso para un saldo. Opciones: mini-`case` para `DESCUENTO_CICLICO_PENDIENTE` (`"Saldo pendiente — {institución}"`, 3 líneas) o aceptar el texto genérico con la Description correcta. Recomendado: el mini-case.
- **№3 — Nombre/precisión de la preferencia de tasa** (PR-1): propuesto `RecurringDeductionDefaultInterestRatePercent numeric(9,4)`, rango `(0,100]` validado en el PUT; confirmar nombre final contra el estilo de `CompanyPreference`.
- **Riesgo aritmético**: la amortización es la pieza sin precedente — mitigado por el gate del contador (§8/PR-2) y el plan editable (la tabla del acreedor manda, P-03).
- **Semántica del lote**: atómico por diseño (rollback total) — espejo REQ-005; anotar en guía FE.
- **Multi-plaza**: sugerencia de liquidación solo en la plaza principal (№9 — el saldo es del empleado); el insumo usa la plaza del descuento. Documentado en guía FE.

---

## 8. Checklist de despliegue

- [ ] **Confirmar con el contador los casos dorados de amortización (A.3: 1/3/4 + saldo con interés) ANTES de PR-2** — es el gate.
- [ ] Migraciones **M1–M2** aplicadas; `has-pending-model-changes` vacío.
- [ ] **Sin storage nuevo**, sin jobs, sin appsettings nuevos.
- [ ] Asignar permisos: `AuthorizeRecurringDeductions` a los autorizadores (Admin **no** decide sin grant); `View`/`Manage` a RRHH/planilla.
- [ ] Configurar por empresa: preferencia de **tasa de interés default** (P-03, vía PUT — opcional, null = sin precarga) y revisar/editar el catálogo de **tipos de descuento cíclico** (plantilla P-10).
- [ ] Comunicar la **operación manual F1** (cuotas por periodo desde la bandeja + insumo exportado hacia la nómina externa) y la **frontera RN-16** (concepto Externo de la plaza = config estructural; descuento cíclico = crédito con saldo).
- [ ] **Sin paso de adopción de datos** (P-22 eliminada — data de prueba).
- [ ] Recordatorio para REQ-010: los puntos de costura de endeudamiento quedaron anotados (№16); el gap pre-REQ-010 está documentado en su entrada del backlog.
