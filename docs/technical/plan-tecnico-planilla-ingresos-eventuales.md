# Plan técnico — Planilla: ingresos eventuales (REQ-006)

| | |
|---|---|
| **Fuente** | [`analisis-planilla-ingresos-eventuales.md`](../business/analisis-planilla-ingresos-eventuales.md) — **RATIFICADO 2026-07-06** (D-01…D-18; P-01…P-13 respondidas con todas las recomendaciones aceptadas; P-09: «no es el módulo de horas extras; ese será otro módulo») |
| **Fecha** | 2026-07-06 |
| **Estado** | **Escrito** — desarrollo pendiente de arranque según orden del backlog (REQ-006; **sinergia fuerte con REQ-005** — construir contiguos; adelantable con las degradaciones №8/№13) |
| **Módulos** | `PersonnelFiles` (OneTimeIncomes + Applications — net-new) · `GeneralCatalogs` (1 subclase nueva + `payroll-types` condicional) · Provisioning (3 permisos RBAC) · Settlements (hooks quirúrgicos retrocompatibles, espejo REQ-005) · Reporting/Export (**+ agrupación `groupBy`, net-new**) · Localization · Auditoría. **Sin storage nuevo** (P-07), **sin ActionTypes nuevos** (D-18), **sin tocar** `PersonnelFilePayrollTransaction` ni `PersonnelFileCompensationConcept` (RN-14/RN-16) |
| **Rama** | `feature/planilla-ingresos-eventuales` |
| **Migraciones** | **M1** (PR-1: catálogo de estados `HasData` `-9900…-9904` + `payroll-types` `-9890…-9895` si nadie lo sembró) · **M2** (PR-2: 2 tablas de dominio + índices/CHECKs + índice único parcial de aplicación) |
| **Endurecimientos de la ratificación** | **P-01: ciclo de 5 estados con autorización de una decisión** (alternativa sin autorización descartada) · **P-02: solicitante = trío expediente+snapshot y anti-autoaprobación TRIPLE** · P-03/P-04: dos métodos de factores, **sin tarifas legales ni lookup de salario** · P-05: fecha ≤ hoy, retro/futuro de periodo permitidos · P-06: corrección por **reversión de aplicación** (no contra-asiento) · P-07 sin adjuntos · P-10 sin autoservicio · P-11 permisos dedicados · P-12 sin umbral · P-13 gancho de liquidación **en F1** (Media) |

---

## 0. Aclaraciones quirúrgicas (verificadas contra el código, 2026-07-05/06)

1. **Frontera P-01/P-04**: el módulo **registra, autoriza, aplica y exporta** — cero cálculo de nómina y **cero cálculo normativo**: los componentes del valor no-fijo los digita RRHH y el servidor solo **recalcula la aritmética** (№11). El único punto que "paga" es la sugerencia en el motor de liquidación (№12). El modelo queda listo para que el futuro motor aplique con `originCode=MOTOR` sobre los mismos mutadores.
2. **Ubicación de las entidades**: sub-registros del expediente como clases en `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs` (moldes: off-payroll `:1442`, ayuda económica `:1701`) con EF config en `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs`. Las 2 entidades nuevas (`PersonnelFileOneTimeIncome`, `PersonnelFileOneTimeIncomeApplication`) siguen ese molde (misma parcial + misma config). Si REQ-005 ya está construido al arrancar, evaluar en PR-2 extraer base común mínima con `PersonnelFileRecurringIncome` (D-02) — sin bloquear: las entidades son independientes.
3. **Repositorio**: sub-recursos en **`IPersonnelFileEmployeeRepository`/`PersonnelFileEmployeeRepository`** (moldes `AddOffPayrollTransactionAsync :590`, `AddEconomicAidRequestAsync :658`, lifecycle `:688-706`). **Idioma post-fix obligatorio en los `Add*`**: `.Set<T>().Add(entity)` + re-query `.AsNoTracking()` + `.Append(entity)` antes de mapear (`PersonnelFileEmployeeRepository.cs:1393-1403`; el `SaveChanges` ocurre en el handler). Las consultas corporativas (búsqueda/agrupación/insumo) van en repositorio de reporting (molde `SettlementRepository`).
4. **`[AuthorizationPolicySet]` es CLASS-ONLY** (`AuthorizationPolicySetAttribute.cs:24`) → **trío de controllers por policy** (molde exacto REQ-005 №4): CRUD (`View`,`Manage`) · **resolución/revocación en controller dedicado** (`View`,`Authorize` — molde `RetirementRequestResolutionController.cs:26`) · búsqueda/exports/lote en controller estilo reporting **sin** policy-set con gates por handler (molde `SettlementsReportingController.cs:16-21` — el `POST /query` es lectura y la convención por verbo lo mapearía a Manage; los governance tests exigen el marcador o la exención documentada).
5. **`Authorize*` sin Admin**: policy con `RequireAssertion(HasAnyPermission(AuthorizeOneTimeIncomes, ManageAdministration))` — **excluye `PersonnelFiles.Admin`** (molde `Program.cs:557-575`, comentario `:550-554`); code en `ProvisioningConstants.cs:33-93` + constante en `PersonnelFilePermissionCodes` + gate espejo en `PersonnelFileAuthorizationService`.
6. **Anti-autoaprobación TRIPLE (P-02 ratificada)**: pata (a) sujeto — molde `EconomicAidRequests.Handlers.cs:371-376` (`personnelFile.LinkedUserPublicId == decidedByUserId` → 403 `SELF_APPROVAL_FORBIDDEN`); pata (b) registrador — contra `RequestedByUserId` del registro (corte REQ-003/REQ-005); pata (c) **solicitante — NUEVA**: el handler de resolución carga el expediente del solicitante (`RequesterFilePublicId`) y bloquea si su `LinkedUserPublicId == decidedByUserId` (solicitante sin usuario vinculado → la pata no aplica; una consulta extra solo en la decisión). La decisión valida el estado destino contra el catálogo (`CatalogCodeIsActiveAsync`) y re-verifica `EN_REVISION` dentro de la transacción (segunda decisión concurrente → 409/422).
7. **Catálogos generales NO son TPH** (tabla propia por subclase vía `GeneralCatalogItemConfigurationBase<T>`; configs auto-descubiertas). Este módulo agrega **1 subclase**: `OneTimeIncomeStatusCatalogItem` (+ `PayrollTypeCatalogItem` **solo si** ningún REQ lo sembró — espec REQ-004 re-ubicada). Alta = clase + config + DbSet + seed getter + migración + `GeneralCatalogKeyMap` (`one-time-income-statuses`) + categoría de validación. Los **métodos de cálculo** (`CANTIDAD_POR_VALOR`/`PORCENTAJE_SOBRE_BASE`) y los **orígenes** (`MANUAL`/`MOTOR`/`LIQUIDACION`) son **constantes de dominio, no catálogos** (no editables por el negocio).
8. **Seeds — bloque `-9900…-9909` verificado libre** (2026-07-05, doble pasada: piso real en código `-9846`; reservas de planes terminan en `-9899` con REQ-005; ni código ni docs ocupan ≤ `-9900`). Estados `-9900…-9904` · concepto de liquidación `INGRESO_EVENTUAL_PENDIENTE=-9905` · holgura `-9906…-9909`. `payroll-types` = `-9890…-9895` (definición única REQ-004 — la siembra el primero que construya entre REQ-001/004/005/006; si otro ya corrió, PR-1 lo **omite** y solo consume). Re-verificar al abrir PR-1 (trampa vigente `-9490…-9496`).
9. **Lock anti-carrera + invariante de aplicación única**: constante de clase + `ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})")` con objectKey = `BitConverter.ToInt32(guid.ToByteArray(), 0)` (moldes `CompanyRepository.cs:29-31`, `PositionSlotRepository.cs:196-198`; fake in-memory no-op). Nuevo: `AcquireOneTimeIncomeMutationLockAsync(oneTimeIncomePublicId)` — el handler abre la transacción antes (el xact-lock se libera al commit); el lote toma locks **ordenados por Id** (anti-deadlock). **Red final a nivel BD**: índice único parcial `(tenant_id, one_time_income_id) WHERE is_active` sobre la tabla de aplicaciones → es **físicamente imposible** tener dos aplicaciones activas del mismo ingreso (RN-06).
10. **Solicitante = trío del molde exacto**: `RequesterFilePublicId` + `RequesterNameSnapshot` + `RequestedByUserId` (liquidación `PersonnelFileSettlement.cs:114-123`; retiro `PersonnelFileEmployee.cs:2456-2478` — el comentario del molde documenta la semántica: el trío separa QUIÉN pide de quién digita). Las bandejas/exports ya exponen la columna «Solicitante» (`RequesterName`) — se replica.
11. **Cálculo por factores (D-07)**: componentes **persistidos**; el servidor **deriva `amount` de los componentes** (fuente de verdad) y, si el request lo incluye, valida igualdad exacta tras redondeo → 422 `ONE_TIME_INCOME_AMOUNT_MISMATCH` con el desglose esperado. Redondeo: **2 decimales `MidpointRounding.AwayFromZero`** — verificar en PR-2 si `SettlementCalculationRules` expone helper de redondeo y reutilizarlo (paridad con liquidación). `DefaultCalculationType` del concepto país (p. ej. `COMISION=-9722` → Percentage) **solo** informa el default del FE (documentar en guía FE; el backend no fuerza método por concepto). `IsBaseSalary` → 422 (D-03).
12. **Integración con liquidación = SUGERENCIA por el canal existente** (espejo №10/№11 de REQ-005, mismos puntos de anclaje): `SettlementRepository.GetCalculationContextAsync:126-152` se extiende para leer ingresos eventuales **`AUTORIZADO`** del expediente (liquidación de la **plaza principal**) → `SuggestedItemDto(INGRESO_EVENTUAL_PENDIENTE, "Ingreso eventual pendiente — {concepto} {referencia}", Amount, null)`; concepto seed **`-9905`** (`IsSystemCalculated=false`, naturaleza ingreso, familia `-9830…-9846`); guard anti-duplicado (línea manual existente con mismo concepto+referencia → no re-sugerir). `IssueSettlementCommandHandler` (`Settlements.Handlers.cs:947`, `MarkIssued :992`) → tras emitir, `MarkAppliedBySettlement(settlementPublicId, now)` de los `AUTORIZADO` **cuya línea quedó incluida** (aplicación con `origin=LIQUIDACION` + `settlement_public_id`; idempotente); `AnnulSettlementCommandHandler` (`:1030`) → `ReopenFromSettlement` solo de los aplicados por **esa** liquidación. **NO reutilizar `HORAS_EXTRAS_PENDIENTES=-9837`** (reclamado por REQ-002 para tiempo compensatorio). **Coexistencia con REQ-005**: cada módulo agrega su lectura al contexto y sus hooks — aditivos e independientes; si REQ-005 corrió primero, sus moldes de hook se reutilizan tal cual. Contrato del motor **intacto** (la suite existente de liquidación sigue verde sin edición).
13. **Periodo destino (par REQ-001)**: `PayrollPeriodDefinition` no está construido. Si REQ-006 arranca **después** de REQ-001: `payroll_period_id bigint NULL` con FK real + `payroll_period_label` snapshot + **`payroll_period_end_date date NULL`** (snapshot del fin del periodo — habilita la marca «atrasado» de la bandeja, RF-010). Si se **adelanta**: `payroll_period_public_id uuid NULL` sin FK dura + etiqueta obligatoria + `end_date NULL` → la marca de atrasado queda **limitada al modo degradado** (sin fecha fin no se deriva; documentar en guía FE). **Decidir al abrir PR-4** según el estado del backlog (misma decisión №13 de REQ-005 — coordinarlas si se construyen contiguos).
14. **Agrupación `groupBy` (RF-009) — pieza NET-NEW**: el único rollup existente es el conteo por estado (`SettlementRepository.cs:355-363`, EF `GroupBy` → `ToDictionaryAsync`). Se generaliza en el repositorio de reporting: **whitelist** de 8 dimensiones (`estado`, `tipo`, `empleado`, `tipoPlanilla`, `periodo`, `centroCosto`, `moneda`, `mes`) como enum de aplicación (dimensión inválida → 400 con la lista); agregación en BD con clave compuesta **(dimensión, moneda)** → `groups[]` = `{key, keyLabel, count, totalsByCurrency}` (RN-13: nunca sumar cruzando monedas; `mes` = truncado de `income_date` al mes). La agregación respeta todos los filtros de la búsqueda y viaja en la misma respuesta del `query` cuando `groupBy` está presente (los `items` paginados se mantienen). EXPLAIN sobre el índice `(tenant_id, status_code, income_date)` en PR-5.
15. **Export**: infraestructura completa existente — **OpenXML hecho a mano** (sin ClosedXML/EPPlus): `ReportExportFileWriter.WriteAsync` (xlsx/csv/json; columnas por reflexión — **nombres de propiedades en español = cabeceras**) + `ReportExportDeliveryService.CreateFileResultAsync` (formato inválido → 400, tope síncrono → 413 `ExportTooLarge`, auditoría `AuditEventTypes.ReportExported`, nombre `{prefix}.{format}`) + `[EnableRateLimiting(PersonnelFileRateLimitPolicies.Search/Export)]`. Tres export rows nuevos en español (búsqueda, pendientes, insumo).
16. **openapi.yaml es mantenido a mano** (`docs/technical/api/openapi.yaml`, skill `.agents/skills/update-api-reference-openapi/`); guardrails `OpenApiContractGuardrails*`/`PublicContractGuardrails*` sobre el swagger vivo. PR-1 anota rutas nuevas (openapi temprano) y PR-6 actualiza el yaml final vía skill.
17. **Gotchas memorados aplicables**: `[Required]` con target de **parámetro** en records posicionales (property-target causa 500); los defaults de ctor de records **no** aplican en deserialización → validators explícitos para `isFixedValue`/método/componentes; `DOTNET_ROLL_FORWARD=Major` para `dotnet ef` 9.0.9. Los DTOs de este módulo **no** usan `[ResourceActions]`/`ISupportsAllowedActions`.

---

## 1. Alcance del plan

Implementa RF-001…RF-012 del análisis ratificado. Fuera: motor de planilla y cálculo normativo (P-01/P-04), **registro de jornadas de horas extras** (P-09 — módulo futuro propio; la costura `overtimeRecordPublicId` de REQ-002 apunta a aquel, no a este), multi-nivel/umbrales (P-12), adjuntos (P-07), autoservicio (P-10), notificaciones, correlación con ledger externo. Todo aditivo; cero breaking changes.

---

## 2. Modelo de datos (M2)

### `personnel_file_one_time_incomes` (`PersonnelFileOneTimeIncome : TenantEntity`)

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `personnel_file_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Cabecera | `income_date` (≤ hoy, RN-09), `concept_type_code` (80) + `concept_name_snapshot` (200) — catálogo país `Nature=Ingreso`, no `IsBaseSalary` (D-03), `reference` (200, null), `observations` (1000, null) |
| Valor (D-07) | `is_fixed_value` (bool), `calculation_method` (30, NULL si fijo: `CANTIDAD_POR_VALOR`/`PORCENTAJE_SOBRE_BASE`), `quantity numeric(18,4) NULL`, `unit_value numeric(18,4) NULL`, `multiplier numeric(9,4) NULL` (default 1.00 si método cantidad), `percentage numeric(9,4) NULL`, `base_amount numeric(18,2) NULL`, `amount numeric(18,2)` (>0 — derivado si no fijo, №11), `currency_code` (3 — default `CompanyPreference.CurrencyCode`) |
| Plaza/costo (espejo P-15) | `assigned_position_public_id` (**NOT NULL**, default plaza principal resuelta en handler), `cost_center_public_id` (**NOT NULL**, derivado de la plaza; plaza sin CC → 422) + `cost_center_name_snapshot` (200) |
| Solicitante (№10) | `requester_file_public_id` (**NOT NULL**), `requester_name_snapshot` (200) |
| Destino (№13) | `payroll_type_code` (80 — catálogo REQ-004; default el de la plaza si está clasificada), `payroll_period_id`/`payroll_period_public_id` (NULL, №13) + `payroll_period_label` (80, obligatoria) + `payroll_period_end_date date NULL` |
| Flujo | `status_code` (80), `requested_by_user_id` (auditoría de digitación), `decided_by_user_id`/`decided_utc`/`decision_note` (500), `annulled_by_user_id`/`annulled_utc`/`annulment_reason` (500) |

- CHECKs: `amount > 0` · componentes `IS NULL OR > 0` (los cinco) · `is_fixed_value = true OR calculation_method IS NOT NULL`. La pareja método↔componentes y la coherencia aritmética se validan en dominio/reglas (el redondeo es regla, RN-05 — no en CHECK).
- Índices: `(tenant_id, personnel_file_id, status_code)` · `(tenant_id, status_code, payroll_type_code)` (bandeja de pendientes/insumo) · `(tenant_id, status_code, income_date)` (búsqueda avanzada + `groupBy`) · UQ `public_id`.

### `personnel_file_one_time_income_applications` (`PersonnelFileOneTimeIncomeApplication : TenantEntity`)

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `one_time_income_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Aplicación | `applied_date`, `payroll_type_code` (80, snapshot del periodo **real**), `payroll_period_id`/`payroll_period_public_id` (NULL, №13) + `payroll_period_label` (80, snapshot), `origin_code` (20: `MANUAL`/`MOTOR`/`LIQUIDACION`), `status_code` (20: `APLICADA`/`ANULADA`), `applied_by_user_id`, `settlement_public_id uuid NULL` (solo `origin=LIQUIDACION`, №12), `annulment_reason` (500)/`annulled_by_user_id`/`annulled_utc`, `notes` (500, null) |

- **Índice único parcial**: `(tenant_id, one_time_income_id) WHERE is_active` — a lo sumo **una aplicación activa** por ingreso (№9); la reversión pone `is_active=false` + `status_code=ANULADA` y el ingreso vuelve a `AUTORIZADO` (D-10/RF-007) — el histórico queda visible (RN-08).
- Índices: `(tenant_id, one_time_income_id)` · `(tenant_id, payroll_type_code, applied_date)` (conciliación/insumo histórico).

### Dominio (constantes y mutadores custodiados)

- `OneTimeIncomeStatuses` (estático): `EN_REVISION`, `AUTORIZADO`, `RECHAZADO`, `APLICADO`, `ANULADO` + conjuntos `Editable={EN_REVISION}`, `Applicable={AUTORIZADO}`, `Terminal={RECHAZADO, ANULADO}` (`APLICADO` **no** es terminal: la reversión lo devuelve a `AUTORIZADO`). `OneTimeIncomeApplicationStatuses`: `APLICADA`/`ANULADA`. `OneTimeIncomeApplicationOrigins`: `MANUAL`/`MOTOR`/`LIQUIDACION`. `OneTimeIncomeCalculationMethods`: `CANTIDAD_POR_VALOR`/`PORCENTAJE_SOBRE_BASE`.
- Mutadores de `PersonnelFileOneTimeIncome`: `Create(...)` (factoría; valida valor/componentes vía reglas) · `Update(...)` (solo `EN_REVISION`) · `Approve(decidedBy, now)` / `Reject(decidedBy, now, note)` · `Annul(reason, byUser, now)` (desde `EN_REVISION` — retiro — o `AUTORIZADO` — revocación) · `RetargetPeriod(payrollTypeCode, periodRef, label, endDate?, now)` (**solo `AUTORIZADO`** — «enviar a otro periodo», RF-005) · `Apply(...)` (crea la hija validando `AUTORIZADO` + sin aplicación activa; deja `APLICADO`) · `AnnulApplication(applicationPublicId, reason, byUser, now)` (→ `AUTORIZADO` en la misma operación) · `MarkAppliedBySettlement(settlementPublicId, now)` / `ReopenFromSettlement(settlementPublicId, now)` (№12). Cada mutación rota `ConcurrencyToken`.

---

## 3. Arquitectura

### 3.1 Configuración (PR-1, M1)

- **1 subclase catálogo** (№7): `OneTimeIncomeStatusCatalogItem` + config + DbSet + seed §4; `PayrollTypeCatalogItem` **condicional** (espec REQ-004 — omitir si ya sembrado). `GeneralCatalogKeyMap`: `one-time-income-statuses` (+ `payroll-types` si aplica). Categorías de validación (`CatalogCodeIsActiveAsync`).
- **Verificación de conceptos** (D-03): confirmar filas país `Nature=Ingreso` — `HORAS_EXTRA=-9721`, `COMISION=-9722`, `BONO=-9723`, `VIATICOS=-9724`, `AGUINALDO=-9725`, `OTRO_INGRESO=-9726` ya sembradas (`GlobalCatalogSeedData.cs:936-952`); completar solo si el negocio pide filas nuevas.
- **Permisos** (D-06/P-11): codes `PersonnelFiles.ViewOneTimeIncomes` / `ManageOneTimeIncomes` / `AuthorizeOneTimeIncomes` en `ProvisioningConstants` + `PersonnelFilePermissionCodes` + policies en `PersonnelFilePolicies` + registro en `Program.cs` (View/Manage receta estándar; **Authorize con `RequireAssertion` sin Admin**, №5) + gates `EnsureCanView/Manage/AuthorizeOneTimeIncomesAsync` fail-closed + governance tests.
- **openapi temprano** (№16).

### 3.2 Reglas puras — `OneTimeIncomeRules` (PR-2, gate)

Módulo estático puro (molde `SettlementCalculationRules` — sin I/O), errores bilingües con paridad de localización:

- `ComputeAmount(method, quantity?, unitValue?, multiplier?, percentage?, baseAmount?)` → monto redondeado (№11); valida pareja método↔componentes (faltantes/sobrantes → error) y positividad.
- `ValidateValue(isFixedValue, method?, components…, amount?)` → fijo sin componentes / no-fijo con componentes completos y `amount` coherente (igualdad tras redondeo) — 422 `ONE_TIME_INCOME_AMOUNT_MISMATCH` con desglose.
- `CanTransition(from, to)` (RN-01/RN-02) · `CanApply(status, hasActiveApplication)` (RN-06) · `CanRetarget(status)` · `CanRevertApplication(status)`.
- `IsOverdue(periodEndDate?, today)` → marca de atrasado (№13; NULL → no derivable).
- `ValidateConcept(nature, isBaseSalary)` (D-03).
- **Gate del PR**: los 12 casos dorados del Anexo A.3 del análisis en verde como suite unitaria (`OneTimeIncomeRulesTests`).

### 3.3 Application + endpoints (PR-3/PR-4/PR-5)

Archivos en `Features/PersonnelFiles/Compensation/`: `OneTimeIncomes.cs` (DTOs/commands/queries/validators), `OneTimeIncomes.Handlers.cs`, `OneTimeIncomes.Rules.cs` (errores/guards de handler), `OneTimeIncomeApplications.cs` + `.Handlers.cs`, `OneTimeIncomesBandeja.cs` (molde `SettlementsBandeja.cs`: `QueryOneTimeIncomesQuery` → response con `Items`/`StatusCounts`/`TotalsByCurrency`/**`Groups[]` cuando `groupBy` presente** (№14); `QueryPendingOneTimeIncomesQuery` (pendientes/atrasados); `ExportOneTimeIncomesQuery`/`ExportPendingOneTimeIncomesQuery`/`ExportPayrollInputQuery` → export rows en español, №15).

| Controller | PolicySet | Rutas (`api/v1/…`) |
|---|---|---|
| `OneTimeIncomesController` | (`ViewOneTimeIncomes`, `ManageOneTimeIncomes`) | `GET/POST personnel-files/{publicId}/one-time-incomes` · `GET/PUT/DELETE …/{id}` · `PATCH …/{id}/annulment` (retiro desde `EN_REVISION`) · `PATCH …/{id}/period` (re-imputación — solo `AUTORIZADO`, RF-005) · `GET/POST …/{id}/applications` (histórico + aplicación unitaria) · `PATCH …/{id}/applications/{appId}/annulment` (reversión → `AUTORIZADO`, RF-007) |
| `OneTimeIncomeResolutionController` (№4) | (`ViewOneTimeIncomes`, **`AuthorizeOneTimeIncomes`**) | `PATCH personnel-files/{publicId}/one-time-incomes/{id}/resolution` (autorizar/rechazar — **anti-self triple** №6) · `PATCH …/{id}/revocation` (revocación desde `AUTORIZADO`, motivo obligatorio) |
| `OneTimeIncomesReportingController` (sin policy-set, gates por handler — molde `SettlementsReportingController`) | — | `POST companies/{companyId}/one-time-incomes/query` (**búsqueda avanzada** + `groupBy` opcional — gate View) · `POST companies/{companyId}/one-time-incomes/pending/query` (bandeja pendientes/atrasados — gate View) · `POST companies/{companyId}/one-time-incomes/apply-period` (lote — **gate Manage**) · `GET companies/{companyId}/one-time-incomes/export` · `GET …/pending/export` · `GET …/payroll-input/export` (insumo — tipo de planilla + periodo obligatorios → 400 si faltan) — exports con `[EnableRateLimiting(Export)]` |

Filtros del `query` (RF-008): `statusCodes[]`, `employeeId`, `conceptTypeCode`, `fromDate`/`toDate`, **`isFixedValue`**, `payrollTypeCode`, `payrollPeriod`, `costCenterPublicId`, `currencyCode`, `requesterFilePublicId`, `search`, paginación (tope 1–100) + `groupBy` (№14). Convenciones: If-Match en todos los writes, ETag rotativo, errores `extensions.code` bilingües, enums string, auditoría `IAuditService` en la transacción.

### 3.4 Aplicación y reversión (PR-4 — el corazón operativo)

- **Unitaria** (`POST …/{id}/applications`): handler abre transacción → `AcquireOneTimeIncomeMutationLockAsync(publicId)` (№9) → recarga y re-verifica `AUTORIZADO` + sin aplicación activa → `Apply` (snapshot del periodo **real** — default el destino declarado, editable en el request; `origin=MANUAL`; el **monto no viaja**: es el del registro) → estado `APLICADO` → commit.
- **Lote por periodo** (`apply-period`): input = `payrollTypeCode` + periodo + `excludedIncomePublicIds[]` (posponer). Handler: transacción única → candidatos = `AUTORIZADO` del filtro (incluye **atrasados** de periodos vencidos) − excluidos → locks **ordenados por Id** → aplicar cada uno (misma rutina) → commit. **Atómico**: cualquier violación → rollback + 422 con el registro en conflicto. Respuesta: aplicados + pospuestos.
- **Reversión** (`PATCH …/applications/{appId}/annulment`): lock + `AnnulApplication` (motivo; `is_active=false` + `ANULADA`) → ingreso `AUTORIZADO` en la misma transacción; re-aplicable o revocable después.
- **Re-imputación** (`PATCH …/{id}/period`): If-Match + solo `AUTORIZADO`; cambia destino (tipo/periodo/etiqueta/end-date №13) sin tocar monto/cálculo; auditada.
- **Test de carrera obligatorio**: dos `apply-period` concurrentes del mismo filtro → cero dobles aplicaciones (el índice único parcial es la red final; el lock convierte el choque en 409/422 limpio).

### 3.5 Integración con liquidación (PR-6 — espejo №12)

1. `SettlementRepository.GetCalculationContextAsync:126-152`: + lectura de eventuales `AUTORIZADO` del expediente (liquidación de la **plaza principal**) → `SuggestedItemDto(INGRESO_EVENTUAL_PENDIENTE, …, Amount, null)`.
2. Seed del concepto de liquidación `INGRESO_EVENTUAL_PENDIENTE = -9905` (`IsSystemCalculated=false`, naturaleza ingreso, familia `-9830…-9846`).
3. Guard anti-duplicado en la sugerencia (línea manual con mismo concepto+referencia → no re-sugerir).
4. `IssueSettlementCommandHandler:947` → tras `MarkIssued:992`, `MarkAppliedBySettlement` de los `AUTORIZADO` cuya línea quedó **incluida** (aplicación `origin=LIQUIDACION` + `settlement_public_id`; idempotente). Los excluidos siguen `AUTORIZADO` (se resuelven manualmente — RN-11/RF-012).
5. `AnnulSettlementCommandHandler:1030` → `ReopenFromSettlement` solo de los aplicados por **esa** liquidación.

Retrocompatibilidad: motor puro intacto; suite de liquidación existente verde sin edición (solo fixtures nuevos). Coexistencia con REQ-005 verificada por diseño (lecturas y hooks aditivos e independientes, №12).

### 3.6 DevSeed + guía FE

- `DevSeedService.SeedAsync`: nuevo `SeedOneTimeIncomesAsync` — 1 fijo `AUTORIZADO` (comisión $150), 1 no-fijo `EN_REVISION` (horas extra 10×$2.50×1.5), 1 `APLICADO` (para bandeja del autorizador, búsqueda y agrupación en FE).
- `guia-integracion-frontend-planilla-ingresos-eventuales.md` (PR-6): contratos, 5 estados y transiciones, métodos de cálculo (default por concepto — №11), flujo aplicar/posponer/re-imputar/revertir, búsqueda avanzada + `groupBy` + exports, modo degradado del periodo y limitación de «atrasado» (№13), catálogos wire y errores.

---

## 4. Migraciones y seeds

**M1 — `AddOneTimeIncomeCatalogs`** (PR-1):

| Catálogo | Códigos → IDs |
|---|---|
| `ONE_TIME_INCOME_STATUS_CATALOG` (tabla nueva) | `EN_REVISION=-9900` · `AUTORIZADO=-9901` · `RECHAZADO=-9902` · `APLICADO=-9903` · `ANULADO=-9904` |
| `PAYROLL_TYPE_CATALOG` (espec REQ-004 re-ubicada — **omitir si ya sembrado** por REQ-001/004/005) | `MENSUAL=-9890` · `QUINCENAL=-9891` · `SEMANAL=-9892` · `POR_DIA=-9893` · `POR_OBRA=-9894` · `OTRO=-9895` |

Concepto de liquidación `INGRESO_EVENTUAL_PENDIENTE = -9905` (familia de conceptos de liquidación — sembrar en PR-6 con el gancho, o en PR-1 si se prefiere una sola migración de seeds). Holgura del bloque: `-9906…-9909`.

**M2 — `AddOneTimeIncomes`** (PR-2): 2 tablas de dominio + índices/CHECKs + índice único parcial de aplicación (§2).

Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add <M> -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · guardrail `MigrationSeedingIntegrationTests` (idempotencia `HasData`).

---

## 5. Pruebas

- **Unitarias** (`tests/CLARIHR.Application.UnitTests`): `OneTimeIncomeRulesTests` — golden A.3 del análisis (12 casos) como **gate de PR-2**: cálculo cantidad (10×$2.50×1.5=$37.50) y porcentaje (3 %×$10,000=$300), borde de redondeo, mismatch → error con desglose, pareja método↔componentes, transiciones inválidas, `CanApply` con aplicación activa, `IsOverdue` (con y sin end-date), `IsBaseSalary`, + **paridad de localización** de todos los errores nuevos.
- **Integración** (`tests/CLARIHR.Api.IntegrationTests`, parcial `ApiIntegrationTests.OneTimeIncomes.cs`): CRUD + flujo completo (**anti-self triple**: registrador → 403, sujeto → 403, **solicitante con usuario vinculado → 403**; Admin sin `Authorize*` → 403) · componentes incoherentes → 422 con desglose · plaza sin centro de costo → 422 (espejo P-15) · concepto egreso/inactivo/`IsBaseSalary` → 422 · re-imputación (sale de un insumo, entra al otro) · aplicación unitaria + lote con exclusión + **test de carrera** (doble submit → cero dobles aplicaciones) · reversión → `AUTORIZADO` con histórico visible + re-aplicación · retirado (alta 422, pendiente solo rechazo/anulación) · **búsqueda avanzada**: filtros combinados (incl. `isFixedValue`) + `StatusCounts` + totales por moneda + **`groupBy` cuadrando contra la búsqueda plana del mismo filtro** + dimensión inválida → 400 · insumo cuadrado contra pendientes del mismo filtro + sin periodo → 400 · liquidación e2e (sugerencia de pendientes, `APLICADO` origen `LIQUIDACION` al emitir con línea incluida, excluido sigue `AUTORIZADO`, reapertura al anular) · **test de no-escritura** (`PersonnelFilePayrollTransaction` y `PersonnelFileCompensationConcept` sin filas nuevas tras el flujo completo) · gates de permisos y governance.
- **Guardrails**: openapi (№16) + seeds (`MigrationSeedingIntegrationTests`).

---

## 6. Orden de PRs

- **PR-1 — Configuración (M1)**: catálogo de estados + seeds `-9900…-9904` (+ `payroll-types -9890…-9895` si nadie lo sembró) + key map/categorías + verificación de conceptos `Nature=Ingreso` (D-03) + 3 permisos/policies/gates (**Authorize sin Admin**) + governance tests + openapi temprano. *Verificar IDs libres contra `GlobalCatalogSeedData` (trampa `-9490…-9496`) y el estado de `payroll-types` (REQ-001/004/005).*
- **PR-2 — Dominio + reglas (M2)**: 2 entidades + mutadores custodiados + EF config/índices/CHECKs (índice único parcial de aplicación) + `OneTimeIncomeRules` puro con **golden A.3 en verde (gate)** + métodos de repositorio (idioma Add/AsNoTracking №3) + `AcquireOneTimeIncomeMutationLockAsync` + decisión de base común con REQ-005 si ya está construido (№2).
- **PR-3 — Flujo end-to-end**: `OneTimeIncomesController` (CRUD `EN_REVISION` con cálculo por factores + anulación) + `OneTimeIncomeResolutionController` (resolución con **anti-self triple** + revocación) + re-imputación de periodo + validaciones espejo P-15 (plaza→centro de costo) + solicitante (trío №10).
- **PR-4 — Aplicación**: unitaria + **lote por periodo con exclusión** (atómico, locks ordenados, **test de carrera**) + reversión con reapertura a `AUTORIZADO` + bandeja de pendientes/**atrasados** + par de periodo (№13 — decidir FK vs degradación, coordinar con REQ-005).
- **PR-5 — Búsqueda avanzada + exportaciones**: `query` corporativo con filtros del levantamiento + `StatusCounts` + totales por moneda + **`groupBy` de 8 dimensiones** (№14, EXPLAIN del índice) + 3 exports con rate limiting (№15) + **insumo de planilla** cuadrado en tests.
- **PR-6 — Integración liquidación + cierre**: hooks №12 + concepto `-9905` + suite E2E completa + test de no-escritura + `openapi.yaml` final (skill №16) + **guía FE** + DevSeed.

Cada PR con la suite completa en verde; convención de commits del repo (**sin trailer de co-autoría de IA**).

---

## 7. Riesgos técnicos y decisiones del plan

- **№13 (periodo)**: única decisión abierta — se resuelve al abrir PR-4 según el estado de REQ-001 (FK real + `end_date` para «atrasado» vs degradación documentada). Si REQ-005 y REQ-006 se construyen contiguos, tomar la **misma** decisión en ambos.
- **Coordinación `payroll-types`**: definición única compartida ahora por **cuatro** REQs (REQ-001/004/005/006, IDs `-9890…-9895`) — la siembra el primero que construya; los demás la omiten y consumen (el backlog lo anota en todos).
- **Coexistencia en liquidación**: si REQ-005 corre primero, sus hooks de emisión/anulación son el molde exacto (mutadores separados por módulo, lecturas aditivas del contexto); si REQ-006 corre primero, inaugura el patrón y REQ-005 lo reutiliza.
- **Semántica del lote**: atómico por diseño (rollback total) — si el negocio prefiriera «aplicar lo que se pueda», es cambio de handler sin tocar modelo (anotar comportamiento en guía FE).
- **Sugerencia y plaza**: el ingreso apunta a una plaza (P-15); la sugerencia de liquidación sale en la liquidación de la **plaza principal** aunque el ingreso sea de una secundaria (el monto es del empleado) — documentado en guía FE, espejo de REQ-005.
- **`groupBy` y volumen**: agregación en BD sobre `(tenant_id, status_code, income_date)`; verificar plan de ejecución en PR-5 con volumen sintético; si una empresa lo exige, el pipeline async de export jobs existe como válvula.

---

## 8. Checklist de despliegue

- [ ] Migraciones **M1–M2** aplicadas (verificar en la bitácora del despliegue el estado previo de `payroll-types` si otro REQ ya corrió).
- [ ] **Sin storage nuevo** (P-07), sin jobs, sin configuración de appsettings nueva.
- [ ] Paso de adopción por empresa: confirmar que los conceptos país `Nature=Ingreso` cubren su operación (`HORAS_EXTRA`/`COMISION`/`BONO`/`VIATICOS`/`AGUINALDO`/`OTRO_INGRESO` sembrados); registrar retroactivamente los eventuales históricos si se desea continuidad de reportes (P-05).
- [ ] Asignar el permiso `AuthorizeOneTimeIncomes` a los autorizadores (Admin **no** decide sin el grant).
- [ ] Comunicar la **operación manual F1** (aplicar por periodo desde la bandeja; el insumo exportado es el puente con la nómina externa) y la **frontera de los cuatro registros monetarios** (Anexo A.4 del análisis — capacitación: concepto estructural vs cíclico vs eventual vs fuera de nómina).
