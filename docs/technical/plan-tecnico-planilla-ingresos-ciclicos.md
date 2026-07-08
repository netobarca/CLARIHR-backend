# Plan técnico — Planilla: ingresos cíclicos (REQ-005)

| | |
|---|---|
| **Fuente** | [`analisis-planilla-ingresos-ciclicos.md`](../business/analisis-planilla-ingresos-ciclicos.md) — **RATIFICADO 2026-07-05** (D-01…D-18; P-01…P-15 respondidas; P-01: el motor de planilla se realizará aparte) |
| **Fecha** | 2026-07-05 |
| **Estado** | **Escrito** — desarrollo pendiente de arranque según orden del backlog (REQ-005; adelantable con la degradación de la aclaración №13) |
| **Módulos** | `PersonnelFiles` (RecurringIncomes + Installments — net-new) · `GeneralCatalogs` (4 subclases nuevas) · Provisioning (3 permisos RBAC) · Settlements (5 toques quirúrgicos retrocompatibles) · Reporting/Export · Localization · Auditoría. **Sin storage nuevo** (P-07), **sin ActionTypes nuevos** (D-18), **sin tocar** `PersonnelFilePayrollTransaction` ni `PersonnelFileCompensationConcept` (RN-14/RN-16) |
| **Rama** | `feature/planilla-ingresos-ciclicos` |
| **Migraciones** | **M1** (PR-1: 4 catálogos `HasData`, IDs `-9880…-9899`) · **M2** (PR-2: 2 tablas de dominio + índices/CHECKs) |
| **Endurecimientos de la ratificación** | Catálogo **tipos de ingresos cíclicos** con plantilla (P-02: `AYUDA_ALIMENTACION`/`GASTOS_REPRESENTACION`/`COMBUSTIBLE`/`OTRO`) · **plaza obligatoria + centro de costo derivado de la plaza** (P-15; plaza sin CC → 422) · `SUSPENDIDO` en F1 (P-03, 6 estados) · indefinido → `CANCELAR` forzado (P-06, 422) · monto de cuota **no editable** al aplicar (P-04) · sin adjuntos (P-07) · sin autoservicio (P-11) · permisos dedicados (P-14) |

---

## 0. Aclaraciones quirúrgicas (verificadas contra el código, 2026-07-05)

1. **Frontera P-01**: el módulo **registra, aplica y exporta** — cero cálculo de nómina. El único punto que "paga" es la sugerencia en el motor de liquidación existente (№10). El modelo de cuotas queda listo para que el futuro motor aplique con `originCode=MOTOR` sobre los mismos mutadores.
2. **Ubicación de las entidades**: los sub-registros del expediente viven como clases en `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs` (off-payroll `:1442`, ayuda económica `:1701`) con su EF config en `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs` (off-payroll `:247`/`:505`). Las 2 entidades nuevas (`PersonnelFileRecurringIncome`, `PersonnelFileRecurringIncomeInstallment`) siguen ese molde (misma parcial + misma config file).
3. **Repositorio**: los sub-recursos van en **`IPersonnelFileEmployeeRepository`/`PersonnelFileEmployeeRepository`** (no en `PersonnelFileRepository`): moldes `AddOffPayrollTransactionAsync :590`, `AddEconomicAidRequestAsync :658` y lifecycle `Resolve/Disburse/CancelEconomicAidRequestAsync :688-706`. **Idioma post-fix obligatorio en los `Add*`**: `.Set<T>().Add(entity)` + re-query `.AsNoTracking()` + `.Append(entity)` antes de mapear (comentario y código en `PersonnelFileEmployeeRepository.cs:1393-1403` — el `SaveChanges` ocurre después en el handler).
4. **`[AuthorizationPolicySet]` es CLASS-ONLY** (`AuthorizationPolicySetAttribute.cs:24`): no hay `[Authorize(Policy=…)]` por acción en el repo — se **separan controllers por policy**. Molde exacto: `RetirementRequestResolutionController.cs:26` → `[AuthorizationPolicySet(ViewRetirements, AuthorizeRetirement)]` con PATCH `…/resolution` y PATCH `…/annulment`. Este módulo replica el trío: CRUD (`View`,`Manage`) · **resolución/revocación en controller dedicado** (`View`,`Authorize`) · bandeja/exports en controller estilo reporting **sin** policy-set con gates por handler (molde `SettlementsReportingController.cs:18`).
5. **`Authorize*` sin Admin**: registro de policy con `RequireAssertion(HasAnyPermission(AuthorizeRecurringIncomes, ManageAdministration))` — **excluye `PersonnelFiles.Admin`** (molde `Program.cs:557-575`, comentario `:550-554`); el code se declara en `ProvisioningConstants.cs:33-93` (grant separable del rol Admin de Empresa) + constante en `PersonnelFilePermissionCodes` + gate espejo en `PersonnelFileAuthorizationService`.
6. **Anti-autoaprobación doble**: pata (a) molde `EconomicAidRequests.Handlers.cs:371-376` (`personnelFile.LinkedUserPublicId == decidedByUserId` → 403 `SELF_APPROVAL_FORBIDDEN`); pata (b) nueva contra `RegisteredByUserId` del registro (mismo corte que el plan de REQ-003). La decisión valida además el estado destino contra el catálogo país (`CatalogCodeIsActiveAsync`, molde `:379-384`) y re-verifica `EN_REVISION` dentro de la transacción (segunda decisión concurrente → 409/422).
7. **Catálogos generales NO son TPH**: cada subclase es entidad `sealed` con **tabla propia** vía `GeneralCatalogItemConfigurationBase<T>` (`GeneralCatalogItemConfiguration.cs:7`, `ToTable(...)` `:19`). Alta de subclase = (a) clase en `GeneralCatalogItems.cs`, (b) config `…ConfigurationBase<X>` (auto-descubierta — `ApplicationDbContext.cs:423`), (c) `DbSet` (precedentes `:242`/`:250`), (d) getter de seed en `GlobalCatalogSeedData`, (e) migración. **4 subclases nuevas** (№ §4) + alta en `GeneralCatalogKeyMap` + categorías en `PersonnelReferenceCatalogs`.
8. **Seeds — colisión resuelta**: los tentativos de REQ-004 `-9520…-9525` están **ocupados** por `ECONOMIC_AID_TYPE_CATALOG` (`GlobalCatalogSeedData.cs:539-544`). Bloque de este módulo: **`-9880…-9899`** (§4); `payroll-types` re-ubicado a `-9890…-9895` (definición única compartida con REQ-004/REQ-001 — la siembra el primero que construya). Verificar IDs libres al abrir PR-1 (trampa vigente `-9490…-9496`; reservas REQ-001 `-9850…-9862`/`-9485…-9489`, REQ-002 `-9865…-9871`, REQ-003 `-9875…-9879`).
9. **Lock anti-carrera**: sin helper compartido — cada repo define constante de clase + método privado `Acquire…LockAsync` con `ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})")` y objectKey = `BitConverter.ToInt32(guid.ToByteArray(), 0)` (moldes `CompanyRepository.cs:29-31`, `PositionSlotRepository.cs:196-198`; contrato documentado `ICompanyRepository.cs:25-27`, fake in-memory no-op). Nuevo: `AcquireRecurringIncomeMutationLockAsync(recurringIncomePublicId)` — **el handler abre la transacción antes** (el xact-lock se libera al commit). El lote toma locks **ordenados por Id** (anti-deadlock).
10. **Integración con liquidación = SUGERENCIA, no línea de motor**: a diferencia de REQ-002 (línea condicional por spec — el monto se calcula), aquí el monto es **conocido** (saldo del plan) → se usa el canal existente de sugerencias: `SettlementRepository.GetCalculationContextAsync:126-152` (hoy mapea conceptos de la plaza a `SuggestedItems`) se **extiende** para leer ingresos cíclicos `VIGENTE` con `PAGAR_SALDO` (plaza **principal**, precedente REQ-002 anti doble pago) y emitir un item con concepto nuevo **`INGRESO_CICLICO_PENDIENTE`** (seed `-9888`, `IsSystemCalculated=false`, familia de conceptos de liquidación `-9830…-9846`); `SettlementCalculationSupport.Recalculate` ya los convierte en `SuggestedPlazaItem` (`Settlements.Handlers.cs` clase `:19`, mapeo `:79-81`) → línea manual sugerida editable/excluible. Guard anti-duplicado: si ya existe línea manual con el mismo concepto + referencia, no se re-sugiere.
11. **Cierre al emitir + reapertura simétrica**: `IssueSettlementCommandHandler` (`Settlements.Handlers.cs:947`, `MarkIssued :992`, asiento `:995-1010`) → tras `MarkIssued`, **finalizar** los ingresos `VIGENTE` del expediente (mutador `FinalizeBySettlement(settlementPublicId, now)` — motivo sistema; idempotente si otra liquidación ya los cerró) en la misma transacción. `AnnulSettlementCommandHandler` (`:1030`) → **reabrir** (`ReopenFromSettlement`) solo los que tengan `ClosedBySettlementPublicId == esa liquidación`. La reversión de retiro (`RevertRetirementRequest.cs:137-154`) **no se toca**: anula borradores (sin efecto en ingresos, nunca se cerraron) y exige anular emitidas primero → la reapertura viaja por el hook de anulación. Contrato del motor **intacto** (solo un `SuggestedItem` más + dos hooks post-estado).
12. **Ledger y configuración intactos**: `PersonnelFilePayrollTransaction` (inmutable, PATCH solo `isActive`) y `PersonnelFileCompensationConcept` no se escriben desde este módulo — **test de no-escritura** en la suite (RN-14/RN-16).
13. **Periodo de imputación (par REQ-001)**: `PayrollPeriodDefinition` **no está construido**. Si REQ-005 arranca **después** de REQ-001 (orden del backlog): columna `PayrollPeriodId bigint NULL` con FK real + `PayrollPeriodLabel` snapshot. Si se **adelanta**: `payroll_period_public_id uuid NULL` **sin FK dura** + etiqueta obligatoria al aplicar (degradación D-10); el endurecimiento a FK llega en migración posterior. **Decidir al abrir PR-4** según el estado del backlog.
14. **openapi.yaml es mantenido a mano** (`docs/technical/api/openapi.yaml`, skill `.agents/skills/update-api-reference-openapi/`); la verificación son los guardrails `OpenApiContractGuardrailsIntegrationTests` / `PublicContractGuardrailsIntegrationTests` / `OpenApiContractGuardrailsTests` sobre el swagger vivo. PR-1 anota rutas nuevas (openapi temprano) y PR-6 actualiza el yaml final vía skill.
15. **Gotchas memorados aplicables**: `[Required]` con target de parámetro en records posicionales (property-target causa 500); los defaults de ctor de records **no** aplican en deserialización → validators explícitos para `isIndefinite`/plan; `DOTNET_ROLL_FORWARD=Major` para `dotnet ef` 9.0.9. Los DTOs de este módulo **no** usan `[ResourceActions]`/`ISupportsAllowedActions` (familia de sub-recursos por policy-set + gates, no maestros governed).

---

## 1. Alcance del plan

Implementa RF-001…RF-013 del análisis ratificado. Fuera: motor de planilla, descuentos cíclicos/endeudamiento (siguiente levantamiento, P-10), correlación con ledger externo (P-09), adjuntos (P-07), autoservicio (P-11), notificaciones. Todo aditivo; cero breaking changes.

---

## 2. Modelo de datos (M2)

### `personnel_file_recurring_incomes` (`PersonnelFileRecurringIncome : TenantEntity`)

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `personnel_file_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Cabecera | `registration_date` (≤ hoy), `reference` (200, null), `recurring_income_type_code` (80 — catálogo P-02), `concept_type_code` (80) + `concept_name_snapshot` (200) — catálogo país `Nature=Ingreso`, `observations` (1000, null) |
| Plaza/costo (P-15) | `assigned_position_public_id` (**NOT NULL**, default plaza principal resuelta en handler), `cost_center_public_id` (**NOT NULL**, derivado de la plaza) + `cost_center_name_snapshot` (200) |
| Plan de cuotas | `installment_start_date`, `currency_code` (3), `payroll_type_code` (80 — catálogo REQ-004), `installment_frequency_code` (80 — `PAY_PERIOD_CATALOG`), `is_indefinite` (bool), `installment_value numeric(18,2)` (>0), `installment_count int NULL`, `total_amount numeric(18,2) NULL`, `settlement_action_code` (80) |
| Flujo | `status_code` (80), `registered_by_user_id`, `decided_by_user_id`/`decided_utc`/`decision_note` (500), `suspended_utc`/`suspension_note` (500), `closed_utc`/`closure_reason` (500)/`closed_by_user_id`, `closed_by_settlement_public_id uuid NULL` (№11) |

- CHECKs: `installment_value > 0`; `total_amount IS NULL OR total_amount > 0`; `installment_count IS NULL OR installment_count >= 1`; `is_indefinite = false OR (installment_count IS NULL AND total_amount IS NULL)` (indefinido sin plan finito).
- Índices: `(tenant_id, personnel_file_id, status_code)` · `(tenant_id, status_code, payroll_type_code)` (bandeja/insumo) · UQ `public_id`.
- La coherencia valor×número×total y el ajuste de última cuota se validan en dominio/reglas (no en CHECK — el redondeo es regla, RN-05).

### `personnel_file_recurring_income_installments` (`PersonnelFileRecurringIncomeInstallment : TenantEntity`)

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `recurring_income_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Cuota | `installment_number int` (≥1), `applied_date`, `theoretical_due_date`, `amount numeric(18,2)` (>0 — última = ajuste), `currency_code` (3, snapshot), `payroll_type_code` (80, snapshot), `payroll_period_id bigint NULL` **o** `payroll_period_public_id uuid NULL` (№13) + `payroll_period_label` (80, snapshot), `origin_code` (20: `MANUAL`/`MOTOR`), `status_code` (20: `APLICADA`/`ANULADA`), `applied_by_user_id`, `annulment_reason` (500)/`annulled_by_user_id`/`annulled_utc`, `notes` (500, null) |

- **Índice único parcial**: `(tenant_id, recurring_income_id, installment_number) WHERE is_active` — la anulación pone `is_active=false` + `status_code=ANULADA`, permitiendo re-aplicar el mismo número (RF-008); el guard de dominio replica la regla.
- Índices: `(tenant_id, recurring_income_id, installment_number)` · `(tenant_id, payroll_type_code, applied_date)` (insumo).

### Dominio (constantes y mutadores custodiados)

- `RecurringIncomeStatuses` (estático): `EN_REVISION`, `VIGENTE`, `RECHAZADO`, `SUSPENDIDO`, `FINALIZADO`, `ANULADO` + conjuntos `Editable={EN_REVISION}`, `Applicable={VIGENTE}`, `Terminal={RECHAZADO,ANULADO,FINALIZADO}`. `RecurringIncomeInstallmentStatuses`: `APLICADA`/`ANULADA`. `RecurringIncomeSettlementActions`: `PAGAR_SALDO`/`CANCELAR`. `RecurringIncomeInstallmentOrigins`: `MANUAL`/`MOTOR`.
- Mutadores de `PersonnelFileRecurringIncome`: `Create(...)` (factoría, valida plan vía reglas) · `Update(...)` (solo `EN_REVISION`) · `Approve(decidedBy, now)` / `Reject(decidedBy, now, note)` · `Suspend(note, now)` / `Resume(now)` · `Annul(reason, byUser, now)` (desde `EN_REVISION` o `VIGENTE` — revocación) · `CloseManually(reason, byUser, now)` (indefinidos) · `FinalizeByPlanCompletion(now)` · `FinalizeBySettlement(settlementPublicId, now)` / `ReopenFromSettlement(settlementPublicId, now)` · `ApplyInstallment(...)` (crea la hija validando secuencia/estado) · `AnnulInstallment(installmentPublicId, reason, byUser, now)` (reabre `FINALIZADO→VIGENTE` si el plan deja de estar completo). Cada mutación rota `ConcurrencyToken`.

---

## 3. Arquitectura

### 3.1 Configuración (PR-1, M1)

- **4 subclases catálogo** (№7) en `GeneralCatalogItems.cs` + configs + DbSets + seeds §4: `RecurringIncomeStatusCatalogItem`, `RecurringIncomeSettlementActionCatalogItem`, `RecurringIncomeTypeCatalogItem`, `PayrollTypeCatalogItem` (espec REQ-004, IDs re-ubicados; si REQ-004/REQ-001 ya lo sembró al arrancar, **omitir** y solo consumir).
- `GeneralCatalogKeyMap`: `recurring-income-statuses`, `recurring-income-settlement-actions`, `recurring-income-types`, `payroll-types`. `PersonnelReferenceCatalogs`: categorías `RecurringIncomeStatus`, `RecurringIncomeSettlementAction`, `RecurringIncomeType`, `PayrollType` (validación `CatalogCodeIsActiveAsync`).
- **Verificación de conceptos** (P-02a): confirmar filas país `Nature=Ingreso` para los ejemplos del negocio (Salario, Aguinaldo, horas extras, vacaciones, bonificaciones); completar seed solo si falta alguna (IDs de la holgura `-9889` o del banco de conceptos).
- **Permisos** (D-06/P-14): codes `PersonnelFiles.ViewRecurringIncomes` / `ManageRecurringIncomes` / `AuthorizeRecurringIncomes` en `ProvisioningConstants` + `PersonnelFilePermissionCodes` + policies en `PersonnelFilePolicies` + registro en `Program.cs` (View/Manage receta estándar con fallback `Admin`/`ManageAdministration`; **Authorize con `RequireAssertion` sin Admin**, №5) + gates `EnsureCanView/Manage/AuthorizeRecurringIncomesAsync` fail-closed + governance tests.
- **openapi temprano**: anotaciones Swashbuckle de las rutas del módulo (№14).

### 3.2 Reglas puras — `RecurringIncomeRules` (PR-2, gate)

Módulo estático puro (molde `SettlementCalculationRules` — sin I/O), errores bilingües con paridad de localización:

- `NormalizePlan(value, count?, total?, isIndefinite)` → deriva el tercero, valida coherencia (tolerancia = ajuste de última cuota), rechaza indefinido con count/total y finito sin ninguno (RN-05).
- `InstallmentAmountFor(n, plan)` → valor de la cuota `n` (la última absorbe el remanente: `total − valor×(n−1)`).
- `BuildProjection(plan, appliedNumbers, from, horizon)` → cuotas teóricas (fecha = inicio + frecuencia×(n−1); `MENSUAL`=+1 mes, `QUINCENAL`=+15 días, `SEMANAL`=+7, `UNICA`=1 cuota) — derivada, nunca persistida (D-07); marca vencidas (`theoreticalDueDate < today`).
- `NextInstallmentNumber(applied)` / `CanApplyInstallment(status, number, plan)` (secuencia estricta, no exceder plan finito, solo `VIGENTE`).
- `RemainingAmount(plan, applied)` / `IsPlanComplete(plan, applied)`.
- `CanTransition(from, to)` (RN-01/RN-02) · `ValidateSettlementAction(action, isIndefinite)` (P-06: `PAGAR_SALDO` × indefinido → error).
- **Gate del PR**: los 10 casos dorados del Anexo A.3 del análisis en verde como suite unitaria (`RecurringIncomeRulesTests`).

### 3.3 Application + endpoints (PR-3/PR-4/PR-5)

Archivos en `Features/PersonnelFiles/Compensation/` (familia de compensación): `RecurringIncomes.cs` (DTOs/commands/queries/validators), `RecurringIncomes.Handlers.cs`, `RecurringIncomes.Rules.cs` (errores/guards de handler), `RecurringIncomeInstallments.cs` + `.Handlers.cs`, `RecurringIncomesBandeja.cs` (molde `SettlementsBandeja.cs:63-108`: `QueryRecurringIncomesQuery` → response con `StatusCounts`; `QueryPendingInstallmentsQuery`; `ExportRecurringIncomesQuery`/`ExportPendingInstallmentsQuery`/`ExportPayrollInputQuery` → export rows en español).

| Controller | PolicySet | Rutas (`api/v1/…`) |
|---|---|---|
| `RecurringIncomesController` | (`ViewRecurringIncomes`, `ManageRecurringIncomes`) | `GET/POST personnel-files/{publicId}/recurring-incomes` · `GET/PUT/DELETE …/{id}` · `PATCH …/{id}/suspension` (suspender/reanudar) · `PATCH …/{id}/closure` (cierre manual indefinido) · `PATCH …/{id}/annulment` (anulación desde `EN_REVISION`) · `GET …/{id}/schedule` (proyección) · `GET/POST …/{id}/installments` (historial + aplicación unitaria) · `PATCH …/{id}/installments/{instId}/annulment` |
| `RecurringIncomeResolutionController` (№4) | (`ViewRecurringIncomes`, **`AuthorizeRecurringIncomes`**) | `PATCH personnel-files/{publicId}/recurring-incomes/{id}/resolution` (autorizar/rechazar — anti-self doble №6) · `PATCH …/{id}/revocation` (revocación desde `VIGENTE`, motivo obligatorio) |
| `RecurringIncomesReportingController` (sin policy-set, gates por handler — molde `SettlementsReportingController`) | — | `POST companies/{companyId}/recurring-incomes/query` (bandeja) · `POST companies/{companyId}/recurring-incomes/pending-installments/query` (RF-011) · `POST companies/{companyId}/recurring-incomes/apply-period` (lote — **gate Manage**) · `GET companies/{companyId}/recurring-incomes/export` · `GET …/pending-installments/export` · `GET …/payroll-input/export` (insumo, rango obligatorio) — exports con `[EnableRateLimiting(Export)]` |

Convenciones: If-Match en todos los writes (+ `parentConcurrencyToken` en sub-recursos), ETag rotativo, errores `extensions.code` bilingües, enums string, auditoría `IAuditService` en la transacción.

### 3.4 Aplicación de cuotas (PR-4 — el corazón operativo)

- **Unitaria**: handler abre transacción → `AcquireRecurringIncomeMutationLockAsync(incomePublicId)` (№9) → recarga y re-verifica `VIGENTE` + `NextInstallmentNumber` → `ApplyInstallment` (monto de reglas — no editable, P-04; snapshots moneda/tipo planilla/periodo №13; `origin=MANUAL`) → si `IsPlanComplete` → `FinalizeByPlanCompletion` en la misma transacción → commit.
- **Lote por periodo** (`apply-period`): input = `payrollTypeCode` + periodo/rango + `excludedIncomePublicIds[]` (posponer). Handler: transacción única → candidatos = proyección de cuotas con fecha teórica ≤ fin del periodo de ingresos `VIGENTE` del filtro (incluye **vencidas** anteriores) − excluidos → locks **ordenados por Id** → aplicar cada una (misma rutina) → commit. **Atómico**: cualquier violación → rollback + 422 con el detalle de la cuota en conflicto. Respuesta: aplicadas + finalizados + pospuestas.
- **Anulación de cuota**: lock + `AnnulInstallment` (motivo; `is_active=false`); si el plan deja de estar completo → `FINALIZADO→VIGENTE` en la misma transacción (RF-008).
- **Test de carrera obligatorio**: dos `apply-period` concurrentes del mismo filtro → cero duplicados (el índice único parcial es la red final; el lock evita el 500 feo convirtiéndolo en 409/422).

### 3.5 Integración con liquidación (PR-6 — 5 toques, №10/№11)

1. `SettlementRepository.GetCalculationContextAsync`: + lectura de ingresos `VIGENTE` con `PAGAR_SALDO` del expediente (solo si la liquidación es de la **plaza principal**) → `SuggestedItemDto(INGRESO_CICLICO_PENDIENTE, "Saldo ingreso cíclico — {tipo} {referencia}", RemainingAmount, null)`.
2. Seed del concepto de liquidación `INGRESO_CICLICO_PENDIENTE = -9888` (`IsSystemCalculated=false`, naturaleza ingreso, familia `-9830…-9846`).
3. Guard anti-duplicado en la sugerencia (línea manual existente con mismo concepto+referencia → no re-sugerir).
4. `IssueSettlementCommandHandler` (`:947`): tras `MarkIssued` (`:992`) → `FinalizeBySettlement` de los `VIGENTE` del expediente (idempotente).
5. `AnnulSettlementCommandHandler` (`:1030`): → `ReopenFromSettlement` de los cerrados por **esa** liquidación (`closed_by_settlement_public_id`).

Retrocompatibilidad verificada: el motor puro no cambia (`SettlementCalculationRules` intacto); la suite existente de liquidación debe seguir verde sin edición (más allá de fixtures nuevos).

### 3.6 DevSeed + guía FE

- `DevSeedService.SeedAsync` (`DevSeedService.cs:39`): nuevo `SeedRecurringIncomesAsync` — 1 finito `VIGENTE` con 2 cuotas aplicadas, 1 indefinido `VIGENTE`, 1 `EN_REVISION` (para probar la bandeja del autorizador en FE).
- `guia-integracion-frontend-planilla-ingresos-ciclicos.md` (PR-6): contratos, 6 estados y transiciones, flujo de aplicación por periodo (lote + exclusión), proyección vs aplicadas, modo degradado del periodo (№13), catálogos wire y errores.

---

## 4. Migraciones y seeds

**M1 — `AddRecurringIncomeCatalogs`** (PR-1): 4 tablas de catálogo (№7) + `HasData`:

| Catálogo (tabla nueva) | Códigos → IDs |
|---|---|
| `RECURRING_INCOME_STATUS_CATALOG` | `EN_REVISION=-9880` · `VIGENTE=-9881` · `RECHAZADO=-9882` · `SUSPENDIDO=-9883` · `FINALIZADO=-9884` · `ANULADO=-9885` |
| `RECURRING_INCOME_SETTLEMENT_ACTION_CATALOG` | `PAGAR_SALDO=-9886` · `CANCELAR=-9887` |
| `PAYROLL_TYPE_CATALOG` (espec REQ-004 re-ubicada — omitir si ya sembrado) | `MENSUAL=-9890` · `QUINCENAL=-9891` · `SEMANAL=-9892` · `POR_DIA=-9893` · `POR_OBRA=-9894` · `OTRO=-9895` |
| `RECURRING_INCOME_TYPE_CATALOG` (P-02, plantilla editable) | `AYUDA_ALIMENTACION=-9896` · `GASTOS_REPRESENTACION=-9897` · `COMBUSTIBLE=-9898` · `OTRO=-9899` |

Holgura: `-9889` (reservado; `-9888` = concepto de liquidación `INGRESO_CICLICO_PENDIENTE`, sembrado en la familia de conceptos de liquidación en PR-6 o PR-1 si se prefiere una sola migración de seeds).

**M2 — `AddRecurringIncomes`** (PR-2): 2 tablas de dominio + índices/CHECKs/índice único parcial (§2).

Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add <M> -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · guardrail `MigrationSeedingIntegrationTests` (idempotencia `HasData`).

---

## 5. Pruebas

- **Unitarias** (`tests/CLARIHR.Application.UnitTests`): `RecurringIncomeRulesTests` — golden A.3 del análisis (10 casos) como **gate de PR-2**, + transiciones inválidas, ajuste de última cuota (incl. no divisible), proyección por frecuencia, `PAGAR_SALDO`×indefinido, **paridad de localización** de todos los errores nuevos.
- **Integración** (`tests/CLARIHR.Api.IntegrationTests`, parcial `ApiIntegrationTests.RecurringIncomes.cs` + fixture existente `IntegrationTestWebApplicationFactory`): CRUD + flujo completo (anti-self **doble**: registrador → 403 y sujeto con permiso → 403; Admin sin `Authorize*` → 403) · plan incoherente → 422 con detalle · plaza sin centro de costo → 422 (P-15) · aplicación unitaria + lote con exclusión + **test de carrera** (doble submit) · anulación de cuota con reapertura `FINALIZADO→VIGENTE` · suspensión bloquea lote · retirado (alta 422, pendiente solo rechazo/anulación) · bandejas con `StatusCounts` + insumo cuadrado contra pendientes del mismo filtro · liquidación e2e (sugerencia `PAGAR_SALDO`, sin sugerencia con `CANCELAR`, finalización al emitir, reapertura al anular) · **test de no-escritura** (`PersonnelFilePayrollTransaction` y `PersonnelFileCompensationConcept` sin filas nuevas tras el flujo completo) · gates de permisos y governance.
- **Guardrails**: openapi (№14) + seeds (`MigrationSeedingIntegrationTests`).

---

## 6. Orden de PRs

- **PR-1 — Configuración (M1)**: 4 catálogos + seeds `-9880…-9899` + key map/categorías + verificación de conceptos `Nature=Ingreso` (P-02a) + 3 permisos/policies/gates (Authorize sin Admin) + governance tests + openapi temprano. *Verificar IDs libres contra `GlobalCatalogSeedData` y el estado de `payroll-types` (REQ-004/REQ-001).*
- **PR-2 — Dominio + reglas (M2)**: 2 entidades + mutadores custodiados + EF config/índices/CHECKs + `RecurringIncomeRules` puro con **golden A.3 en verde (gate)** + métodos de repositorio (idioma Add/AsNoTracking №3) + `AcquireRecurringIncomeMutationLockAsync`.
- **PR-3 — Flujo end-to-end**: `RecurringIncomesController` (CRUD `EN_REVISION`, suspensión/reanudación, cierre, anulación) + `RecurringIncomeResolutionController` (resolución con anti-self doble + revocación) + consulta en ficha + validaciones P-15.
- **PR-4 — Cuotas e historial**: proyección (`schedule`) + aplicación unitaria y **lote por periodo** (atómico, lock, test de carrera) + anulación con reapertura + historial paginado + par de periodo (№13 — decidir FK vs degradación).
- **PR-5 — Bandejas + exportaciones**: bandeja empresa (`StatusCounts`) + cuotas pendientes/vencidas + 3 exports con rate limiting + **insumo de planilla** cuadrado en tests.
- **PR-6 — Integración liquidación + cierre**: 5 toques (№10/№11) + concepto `-9888` + suite E2E completa + `openapi.yaml` final (skill) + **guía FE** + DevSeed.

Cada PR con la suite completa en verde; convención de commits del repo (**sin trailer de co-autoría de IA**).

---

## 7. Riesgos técnicos y decisiones del plan

- **№13 (periodo)**: única decisión abierta — se resuelve al abrir PR-4 según el estado de REQ-001 (FK real vs degradación documentada).
- **Coordinación `payroll-types`**: si REQ-004 o REQ-001 arrancan antes, siembran el catálogo con esta especificación (IDs `-9890…-9895`) y PR-1 de este módulo lo omite — una sola definición (backlog ya lo anota en ambos REQs).
- **Semántica del lote**: atómico por diseño (rollback total) — si el negocio prefiriera "aplicar lo que se pueda", es un cambio de handler sin tocar modelo (anotar en guía FE el comportamiento actual).
- **Multi-plaza**: la sugerencia y el insumo usan la plaza del ingreso (obligatoria P-15); la sugerencia de liquidación solo en la plaza principal (precedente REQ-002) — si el ingreso apunta a una plaza secundaria, la línea sugerida sale igualmente en la liquidación de la plaza principal (el saldo es del empleado, no de la plaza); documentado en guía FE.

---

## 8. Checklist de despliegue

- [ ] Migraciones **M1–M2** aplicadas (verificar en la bitácora del despliegue el estado previo de `payroll-types` si REQ-004/REQ-001 ya corrió).
- [ ] **Sin storage nuevo** (P-07), sin jobs, sin configuración de appsettings nueva.
- [ ] Paso de adopción por empresa: revisar/editar el catálogo de **tipos de ingresos cíclicos** (plantilla P-02) y confirmar que los conceptos `Nature=Ingreso` del país cubren su operación; registrar compromisos preexistentes de forma retroactiva (P-13) aplicando las cuotas históricas con sus periodos reales.
- [ ] Comunicar la **operación manual F1**: las cuotas se aplican por periodo desde la bandeja (unitaria o lote); el insumo exportado es el puente con la nómina externa hasta que exista el motor (P-01).
- [ ] Asignar el permiso `AuthorizeRecurringIncomes` a los autorizadores (Admin **no** decide sin el grant).
