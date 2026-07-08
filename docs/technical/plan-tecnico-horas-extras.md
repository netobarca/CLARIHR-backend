# Plan técnico — Horas extras del empleado (REQ-007)

| | |
|---|---|
| **Fuente** | [`analisis-horas-extras-empleado.md`](../business/analisis-horas-extras-empleado.md) — **RATIFICADO 2026-07-06** (D-01…D-21; P-01…P-14 respondidas: 12 recomendaciones aceptadas + **P-02** jornadas futuras organizadas por cada área + **P-08** la liquidación **calcula** las pendientes de pago) |
| **Fecha** | 2026-07-06 |
| **Estado** | **Escrito** — desarrollo pendiente de arranque según orden del backlog (REQ-007; recomendado **después de REQ-001** y **cerca de REQ-002** — D-20; adelantable con las degradaciones №9/№13/№14) |
| **Módulos** | `PersonnelFiles` (OvertimeRecords + Applications — net-new) · **2 maestros por empresa** (`OvertimeType`/`OvertimeJustificationType` + `OvertimeTemplateSeeder` — net-new) · `GeneralCatalogs` (1 subclase de estados + `payroll-types` condicional) · Provisioning (3 permisos RBAC) · `CompanyPreference` (+2 columnas) · **Settlements** (integración RF-014: **línea calculada** + hooks de cierre/reapertura — retrocompatible) · Reporting/Export · Localization · Auditoría. **Sin storage nuevo** (P-10), **sin ActionTypes nuevos** (D-19), **sin tocar** `PersonnelFilePayrollTransaction` ni `PersonnelFileCompensationConcept` (RN-20) |
| **Rama** | `feature/horas-extras` |
| **Migraciones** | **M1** (PR-1: 2 tablas de maestros + catálogo de estados `HasData` `-9910…-9914` + `payroll-types` `-9890…-9895` si nadie lo sembró + 2 columnas de preferencias) · **M2** (PR-2: 2 tablas de dominio + índices/CHECKs + índice único parcial de aplicación) · **M3** (PR-6: seed del concepto de liquidación `-9915` — puede adelantarse a M1) |
| **Endurecimientos de la ratificación** | **P-01: canal dual** RRHH + autoservicio del propio empleado (preferencia default-off, origen trazado) · **P-02: fecha pasada O futura** (jornada organizada; aplicar exige fecha transcurrida; futuras no trabajadas se anulan al liquidar) · P-03/P-04: plantilla con factores de referencia (2.00/2.50/4.00/5.00 — **contador en checklist de despliegue**) · P-05: tope diario por preferencia (bloquea 422) · P-06: factor editable con nota · **P-07: ciclo con `APLICADA`** + lote por periodo + bandeja pendientes/atrasadas · **P-08: línea calculada** `HORAS_EXTRAS_PENDIENTES_PAGO=-9915` · P-10 sin adjuntos · P-11 consulta = todas + filtro origen · P-12 lectura self · P-13 históricos por flujo normal · P-14 costura REQ-002 condicionada (Media) |

---

## 0. Aclaraciones quirúrgicas (verificadas contra el código, 2026-07-06 — HEAD `62b341b`)

1. **Frontera monetaria**: el módulo registra/autoriza/aplica/exporta **horas × factor** — cero montos, monedas, centros de costo persistidos y cero lookup de salario. La **única monetización** es la línea calculada del motor de liquidación (№15), donde el contexto salarial ya existe (`derived.DailySalary`). El insumo por periodo entrega a la nómina externa lo que necesita para pagar (RN-11/RN-13).
2. **Ubicación de las entidades**: los 2 sub-registros transaccionales (`PersonnelFileOvertimeRecord`, `PersonnelFileOvertimeRecordApplication`) van como clases en `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs` (moldes: off-payroll `:1442`, ayuda económica `:1701`; la plaza `PersonnelFileEmploymentAssignment` está en `:133` con `PayrollTypeCode :187`, `CostCenterPublicId :201`, `IsPrimary :207`) + EF config en `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs`. La referencia a la plaza desde un sub-registro sigue el precedente **`AssignedPositionPublicId`** (liquidación `PersonnelFileSettlement.cs:92`; compensación `PersonnelFileCompensation.cs:65`; resolución del vínculo `SettlementRepository.cs:42-44`). Los **2 maestros por empresa** (`OvertimeType`, `OvertimeJustificationType`) son entidades standalone `TenantEntity` — molde-lista **`CostCenters/CostCenter.cs`** (`Code :38`/`NormalizedCode :40`/`IsActive :56`/`ConcurrencyToken :58`, `Activate :102`/`Inactivate :108`, UQ `(TenantId, NormalizedCode)` `CostCenterConfiguration.cs:78-80`) — con configs propias.
3. **Repositorio**: sub-recursos en `IPersonnelFileEmployeeRepository`/`PersonnelFileEmployeeRepository` con el **idioma post-fix obligatorio en los `Add*`**: `.Set<T>().Add(entity)` + re-query `.AsNoTracking()` + `.Append(entity)` (molde con el comentario del porqué en `PersonnelFileEmployeeRepository.cs:1387-1404`; el `SaveChanges` ocurre en el handler). Consultas corporativas (query/pendientes/insumo/totales) en repositorio de reporting (molde `SettlementRepository`); maestros con repositorio propio (normalización + duplicado por `(tenant, normalized_code)`).
4. **`[AuthorizationPolicySet]` es CLASS-ONLY** (`AuthorizationPolicySetAttribute.cs:24`) → **cuatro superficies con dos sabores de policy sobre los mismos permission codes**: (a) maestros con policy-set **estricto propio** (`OvertimeConfigurationPolicies.Read/Manage` — assertion por permiso + fallback Admin; molde `CostCentersController.cs:25-28` con `CostCenterPolicies.cs:14-15`); (b) `OvertimeRecordsController` con policy-set (`ViewOvertimeRecords`,`ManageOvertimeRecords`) donde **ambas policies quedan authn-only** y el gate fino vive en el handler — es el mecanismo que habilita el canal portal (№7; molde exacto: reclamos médicos, comentario de diseño en `PersonnelFilePolicies.cs:47-53`; precedente del sabor dual documentado en los comentarios del governance test — retiro usa «authn-only read superset + RequireAssertion write»); (c) **resolución en controller dedicado** (`ViewOvertimeRecords`,`AuthorizeOvertimeRecords` — molde `RetirementRequestResolutionController.cs:27/:30/:58`); (d) reporting **sin** policy-set con gates por handler (molde `SettlementsReportingController.cs:17-25` + exención documentada en `AuthorizationPolicyConventionGovernanceTests.cs:38` — facts `:197/:276/:301`; el set `PersonnelFilePolicyNames :81` registra los nombres nuevos).
5. **`Authorize*` sin Admin — precisión verificada**: el permiso **SÍ** se aprovisiona en `CompanyAdminPermissions` como grant asignable (`ProvisioningConstants.cs:89`, descripción «No implicado por la administración de expedientes»); la separación de deberes vive en la **policy**: `AuthorizeRetirement` en `Program.cs:564-569` acepta solo el permiso dedicado + `ManageAdministration` y **omite `PersonnelFiles.Admin`** (contraste `ManageRetirements` `:557-563`; diseño comentado `:552-554`). `AuthorizeOvertimeRecords` replica ese registro exacto + gate espejo en `PersonnelFileAuthorizationService`.
6. **Anti-autoaprobación TRIPLE**: molde **exacto y completo** en los guards del retiro — `RetirementAuthorizerGuards.CheckAsync` (`RetirementRequestResolution.cs:64-87`): pata (a) sujeto `personnelFile.LinkedUserPublicId == actingUserId` (`:74-77`) y pata (c) solicitante vía lookup del expediente (`GetRetirementRequesterLookupAsync` → `RequesterCannotAuthorize`, `:82-87`); pata (b) registrador contra `RequestedByUserId` (corte REQ-003/005/006). Nuevo `OvertimeAuthorizerGuards` con las tres patas + re-verificación de `EN_REVISION` dentro de la transacción (segunda decisión concurrente → 409/422). En el canal portal (a)=(b)=(c) — un tercero facultado decide siempre.
7. **Canal portal (P-01)**: precedente triple construido — helpers `LoadForCreateOwnOrManage*` (`PersonnelFileEmployeeHandlerBases.cs:227/:316/:678`) con bloque `isSelf` (`:254-256`: `LinkedUserPublicId == usuario autenticado`). Nuevo `LoadForCreateOwnOrManageOvertimeAsync`: permite crear si `Manage` **o** (`isSelf` **y** `CompanyPreference.OvertimeSelfServiceEnabled == true`); preferencia off → 403/422 accionable (`OVERTIME_SELF_SERVICE_DISABLED`). El registro propio: `originChannel=PORTAL`, solicitante = el propio expediente; **self-edit/retiro** solo sobre registro propio `EN_REVISION` con origen `PORTAL`; **lectura self** (P-12) por el mismo gate (`View` o `isSelf`). `originChannel` (`RRHH`/`PORTAL`) es **constante de dominio**, no catálogo.
8. **Catálogos generales NO son TPH** (tabla propia por subclase; molde completo verificado con estados de retiro/liquidación): subclase `GeneralCatalogItems.cs:454/:488` sobre base `:5`; config concreta sobre `GeneralCatalogItemConfigurationBase<T>` (`GeneralCatalogItemConfiguration.cs:7`, UQ `(CountryCatalogItemId, NormalizedCode)` `:47-49`, seed inyectado por ctor `:522`) **auto-descubierta** (`ApplicationDbContext.cs:423`); wire name en `GeneralCatalogKeyMap.cs:67-68` (+ `TryResolveCatalogCategory :93`) servido por `GeneralCatalogsController.cs:23` (`GET api/v1/general-catalogs/{catalogKey}`); seed getter molde `GlobalCatalogSeedData.cs:599/:611`; validación de escritura `CatalogCodeIsActiveAsync` (`PersonnelFileRepository.cs:1580`, uso `EmploymentAssignments.Handlers.cs:199`). Este módulo agrega **1 subclase**: `OvertimeRecordStatusCatalogItem` (`overtime-record-statuses`) + `PayrollTypeCatalogItem` **solo si** ningún REQ lo sembró (espec REQ-004 `-9890…-9895`). Los **orígenes de aplicación** (`MANUAL`/`MOTOR`/`LIQUIDACION`) y el **canal** (`RRHH`/`PORTAL`) son constantes de dominio.
9. **Maestros con plantilla**: patrón REQ-001 §3.1 (`LeaveTemplateSeeder` — idempotente **por código**, nunca pisa ediciones, hook de provisioning + endpoint admin `load-template`). Nuevo `OvertimeTemplateSeeder` (`Application/Features/PersonnelFiles/Overtime/Templates/`) con los 4 tipos (factores de referencia A.2: `HED=2.00`/`HEN=2.50`/`HEDF=4.00`/`HENF=5.00`) + 6 justificaciones; invocación (a) hook de provisioning de tenant nuevo, (b) `POST /api/v1/companies/{companyId}/overtime-configuration/load-template`. **Si REQ-001 corrió primero, reutilizar su base de seeder** (misma aclaración №8 de REQ-003); si REQ-007 se adelanta, el seeder es autónomo. Guardrail: 2.ª corrida = 0 cambios.
10. **Seeds — bloque `-9910…-9919` verificado libre** (2026-07-06: piso real en código **`-9846`** — `GlobalCatalogSeedData.cs:980`; **banda `-9847…-9999` completamente libre**; reservas de planes terminan en `-9909` con REQ-006). Estados `-9910…-9914` · concepto de liquidación `HORAS_EXTRAS_PENDIENTES_PAGO=-9915` · holgura `-9916…-9919`. **Trampa nueva documentada**: los aparentes `-9914`/`-9897`/`-9938` en `Migrations/*.Designer.cs` son **fragmentos de GUID**, no IDs — filtrar por sufijo `L` al re-verificar. Trampa vigente `-9490…-9496` (`ACTION_STATUS_CATALOG`).
11. **Lock anti-carrera + invariante de aplicación única**: constante de clase + `ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})")` (moldes `CompanyRepository.cs:28-35` — classId ASCII `"ACCP"` — y `PositionSlotRepository.cs:196-203`; el handler abre la transacción **antes** — molde `PositionSlotAdministration.cs:1066-1078`; fake in-memory no-op). Nuevo `AcquireOvertimeRecordMutationLockAsync(overtimeRecordPublicId)`; el lote toma locks **ordenados por Id** (anti-deadlock). **Red final a nivel BD**: índice único parcial `(tenant_id, overtime_record_id) WHERE is_active` sobre la tabla de aplicaciones (RN-09).
12. **Duración h:m — greenfield verificado** (0 hits de `DurationMinutes`/`DurationHours`; los `TimeSpan` existentes son infraestructura no persistida): `duration_hours int ≥ 0` + `duration_minutes int 0–59` (fuente de verdad) + **`duration_decimal_hours numeric(6,2)` persistida** (derivada en la factoría con `MidpointRounding.AwayFromZero` — habilita sumas/índices/insumo sin recomputar; 2 h 30 m = 2.50). `start_time`/`end_time` (`time`) opcionales informativas. **Tope diario (P-05)**: validación en handler — suma de minutos activos (`EN_REVISION`+`AUTORIZADA`+`APLICADA`) del `(expediente, workDate)` + nuevos ≤ `OvertimeMaxDailyMinutes` → 422 con el límite y lo ya registrado; **best-effort sin lock** (carrera de dos altas simultáneas aceptada y documentada — no hay invariante monetario).
13. **Fecha (P-02)**: `work_date` pasada **o futura** (jornada organizada). Guards derivados: `CanApply` exige `work_date ≤ hoy` (no se constata el pago de una jornada no trabajada); al liquidar, las futuras de la plaza se **anulan** con marcador `annulled_by_settlement_public_id` (reapertura simétrica si la liquidación se anula — №15). **Límite de sanidad** de fecha futura: decidir en PR-3 (sugerencia: ≤ +366 días → 422 `OVERTIME_WORK_DATE_TOO_FAR`; evita typos de año).
14. **Periodo destino (par REQ-004/REQ-001)**: espejo exacto del №13 de REQ-005/REQ-006. Si REQ-007 arranca **después** de REQ-001: `payroll_period_id bigint NULL` (FK real) + `payroll_period_label` snapshot + `payroll_period_end_date date NULL` (habilita «atrasada»). Si se **adelanta**: `payroll_period_public_id uuid NULL` sin FK + etiqueta obligatoria + end-date NULL → «atrasada» limitada al modo degradado. **Decidir al abrir PR-4** según el estado del backlog (coordinar con REQ-005/006 si conviven).
15. **Integración con liquidación (RF-014) = línea CALCULADA por el motor + hooks de cierre/reapertura** (anclas re-verificadas hoy contra el código):
    - **Vía correcta verificada**: el canal `SuggestedItems` existente (`SettlementRepository.cs:126-152` — hoy transporta `BONO_PENDIENTE`/`COMISION_PENDIENTE`/`DESCUENTO_EXTERNO`) **NO sirve** para una línea calculada: sus ítems se mapean a `PlazaItems` con `IsManual: true` (`SettlementCalculation.Rules.cs:321-323`) → monto manual. La vía es **campo nuevo de contexto + spec del motor + `case`** (como `VACACION_PROPORCIONAL`, que es spec puro en `BuildSuggestedSpecs :305`).
    - **Cadena calculada (4 toques)**: (i) `SettlementCalculationContext` (`ISettlementRepository.cs:42-53`) + `OvertimeContext? PendingOvertime` = `(IReadOnlyList<(Guid RecordPublicId, decimal DecimalHours, decimal Factor)> Records, decimal StandardDailyHours)`, resuelto dentro de `GetCalculationContextAsync` (`SettlementRepository.cs:25-205`, junto a la lectura de `CompanyPreference` `:185-189`) — **solo registros `AUTORIZADA` de LA PLAZA que se liquida** (`assigned_position_public_id == settlement.AssignedPositionPublicId`, molde de emparejamiento `:42-44`; cada registro tiene plaza → sin doble pago por construcción, a diferencia del scope plaza-principal de REQ-002), sin aplicación activa, **no compensados** (RF-013) y con `work_date ≤` fecha de retiro; (ii) `SettlementCalculationInput` (`SettlementCalculation.Rules.cs:93-105`; el salario entra por `MonthlyBaseSalary :98`, poblado en `Settlements.Handlers.cs:71`) + mapeo en `SettlementCalculationSupport.Recalculate` (`Settlements.Handlers.cs:66-88`); (iii) alta del **spec del motor** en `BuildSuggestedSpecs` (`:302-335`; `EngineSpec` con `IsManual: false` `:333-335`) condicionada a `PendingOvertime is { Records.Count: > 0 }`; (iv) **`case` nuevo en `ComputeIncomeLine`** (firma `:356-361`, switch `:380-439`, **molde `SALARIO` `:382-389`**; los manuales cortan antes en `:373-377` con `spec.IsManual || !concept.IsSystemCalculated`): `units = spec.UnitsOrDays ?? Σ(horas × factor)` (**horas-factor**), `calculationBase = HourlyRate(derived.DailySalary, StandardDailyHours)` — `derived.DailySalary` ya existe (`Calculate :193` = `MonthlyBaseSalary / MonthDivisorDays(=30, :34)`, expuesto en `SettlementDerivedResult.DailySalary :137`) —, `calculated = Round2(units × base)`, `detail = "{n} jornada(s) · {units:0.##} h-factor × {base:C}/h"`; afectaciones ISSS/AFP/Renta **automáticas** por configuración del concepto (los ingresos se iteran por `ConceptClass` en `Calculate :217-220`).
    - **Anti-duplicado por construcción**: el concepto se siembra con **`isSystemCalculated=true`** → `AddSettlementManualLineCommandHandler` ya **rechaza** líneas manuales de conceptos de sistema (`Settlements.Handlers.cs:622-627` → `ConceptInvalid`); no se necesita guard nuevo. La línea manual «horas extras» actual (concepto `-9837`, descripción obligatoria — validador `Settlements.cs:437`, refuerzo dominio `PersonnelFileSettlement.cs:745`) queda intacta.
    - **Hooks de cierre (2 toques)**: `IssueSettlementCommandHandler` (`Settlements.Handlers.cs:947-1028`) → tras `MarkIssued :992` (su único efecto actual es el asiento `LIQUIDACION` `:995-1008`, transacción `:1011-1026`), si la línea `-9915` quedó **incluida** (`IsIncluded`), `MarkAppliedBySettlement` de los registros del contexto (aplicación `origin=LIQUIDACION` + `settlement_public_id`, idempotente) **y** anulación de las **futuras** de la plaza (`annulled_by_settlement_public_id`); `AnnulSettlementCommandHandler` (`:1030-1102`) → tras `settlement.Annul :1082` (hoy **sin** efectos colaterales — ahí se cuelga), `ReopenFromSettlement` (aplicados por esa liquidación → `AUTORIZADA`) + reapertura de las futuras anuladas por ella. **Molde de cascada de efectos cross-módulo**: `RevertRetirementRequest.cs:140-196` (inyección directa + efectos secuenciales en la transacción + `Reopen` con snapshot).
    - **Semántica de edición**: `SetUnitsOrDays` (`PersonnelFileSettlement.cs:752-762`, marca `UnitsOverridden :628`) / `SetOverride` (`:703-714`; `FinalAmount :643` = `OverrideAmount ?? CalculatedAmount` `:771`) sobreviven recálculos (estado por `PublicId`, `Settlements.Handlers.cs:91-110`); **regenerate** re-sugiere fresco; **excluir la línea** (`IsIncluded=false`) → ningún registro se cierra (todos siguen `AUTORIZADA`; granularidad por-registro = operar antes de liquidar; documentado en guía FE).
    - **Horas-día estándar**: reutilizar `CompanyPreference.CompensatoryTimeStandardDailyHours` (la define REQ-002 PR-1, null=8) si la columna existe; si REQ-007 se adelanta → **default constante 8** y un punto único de lectura (**decisión №3 de §7, se cierra al abrir PR-6**).
    - Retrocompatibilidad total: sin pendientes → contexto `null` → ni spec ni case activo (patrón de test `Renta_WithoutBrackets_IsZeroWithWarning`); coexistencia aditiva con `-9837` (REQ-002), `-9888` (REQ-005) y `-9905` (REQ-006). **⚠️ Hallazgo para REQ-002** (anotado en backlog): su plan §3.11 declara «el seed de `-9837` no cambia» pero el corte `:373-377` exige `concept.IsSystemCalculated=true` para llegar a su `case` — con el seed actual (`false`, `GlobalCatalogSeedData.cs:969`) el case sería código muerto; al implementar REQ-002 PR-6 hay que voltear el seed o ajustar la condición.
16. **Consulta y exports**: bandeja corporativa molde `SettlementsReportingController.cs:30/:71` (`POST /query` + `GET /export`) con `StatusCounts` (rollup `SettlementRepository.cs:355-363`) — aquí se agregan **totales EN HORAS** (`TotalHours` global + `TotalsByType[]` con `GroupBy` en BD sobre `duration_decimal_hours`); **sin `groupBy` dimensional** (no lo pide el levantamiento — el de REQ-006 queda como referencia aditiva). Export: OpenXML a mano (`ReportExportFileWriter.cs:37/:78` — propiedades en español = cabeceras) + `ReportExportDeliveryService.cs:15/:49` (400/413/auditoría `ReportExported`) + `[EnableRateLimiting(PersonnelFileRateLimitPolicies.Search/Export)]` (`:19/:27`). El **insumo** deriva el centro de costo de la plaza al exportar (join a `PersonnelFileEmploymentAssignment` — sin snapshot monetario en el registro, D-12).
17. **Preferencias**: `CompanyPreference` hoy tiene 5 columnas de dominio (`CompanyPreference.cs:5`, columnas `:22-:34`, setter molde `SetEconomicAidEligibility :74`); controller `CompanyPreferencesController` (`:18`, `companies/{companyId}/preferences` — GET `:24`, PUT `:43`, PATCH `:73`). **Nuance verificada**: el PATCH actual es *scalar-only* (estado `CompanyPreferencePatchState :237-240` + applier `:256` solo cubren currency/timezone); los setters ricos se cablean por el **PUT** (`UpdateCompanyPreferencesCommandHandler`, `CompanyPreferenceAdministration.cs:157-158`). +2 columnas anulables: `OvertimeSelfServiceEnabled bool?` (null = **off**, P-01) y `OvertimeMaxDailyMinutes int?` (null = sin tope, P-05) + mutador `SetOvertimePolicies(...)` cableado por el **PUT** (extender el PatchState/applier solo si el FE lo pide).
18. **openapi.yaml es mantenido a mano** (`docs/technical/api/openapi.yaml`, skill `.agents/skills/update-api-reference-openapi/`); PR-1 anota rutas nuevas y PR-6 actualiza el yaml final vía skill. **Gotchas memorados**: `[Required]` con target de **parámetro** en records posicionales; los defaults de ctor de records **no** aplican en deserialización (validators explícitos para horas/minutos/factor); `DOTNET_ROLL_FORWARD=Major` para `dotnet ef` 9.0.9; paridad de localización (`BackendMessageLocalizationTests`).

---

## 1. Alcance del plan

Implementa RF-001…RF-014 del análisis ratificado (RF-013 **condicionado** a que REQ-002 esté mergeado al construir — P-14). Fuera: motor de planilla y cálculo de valor-hora operativo (la única monetización es RF-014), conciliación planificado-vs-trabajado (P-02 F2), portal de jefaturas (P-01 F2), notificaciones, adjuntos (P-10), `groupBy` dimensional, importador masivo. Todo aditivo; cero breaking changes; suite de settlements existente intacta.

---

## 2. Modelo de datos

### M1 — Maestros por empresa (PR-1)

**`overtime_types`** (`OvertimeType : TenantEntity`) y **`overtime_justification_types`** (`OvertimeJustificationType : TenantEntity`):

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `concurrency_token`, `created_utc`/`modified_utc` |
| Gobierno | `code` (40) + `normalized_code` (40) — **UQ `(tenant_id, normalized_code)`**, `name` (120), `sort_order int`, `is_active` (baja lógica; inactivo no admite registros nuevos, históricos conservan snapshot) |
| Solo tipos | `default_factor numeric(5,2)` (> 0 — CHECK) + `payroll_effect_description` (500, null — «condiciones/efecto en el pago» del levantamiento) |
| Solo justificaciones | `description` (500, null) |

### M2 — Dominio transaccional (PR-2)

**`personnel_file_overtime_records`** (`PersonnelFileOvertimeRecord : TenantEntity`):

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `personnel_file_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Jornada | `work_date date` (pasada **o futura** — RN-07/№13), `overtime_type_public_id` (**NOT NULL** — referencia lógica al maestro) + `overtime_type_code_snapshot` (80) + `overtime_type_name_snapshot` (200), `type_factor_snapshot numeric(5,2)` (factor vigente del tipo al registrar), `factor_applied numeric(5,2)` (> 0 — el que viaja al insumo), `factor_override_note` (300, NULL — **obligatoria en reglas si `factor_applied ≠ type_factor_snapshot`**), `duration_hours int` (≥ 0), `duration_minutes int` (0–59), **`duration_decimal_hours numeric(6,2)`** (derivada persistida, №12), `start_time time NULL`, `end_time time NULL` |
| Motivo | `justification_type_public_id` (**NOT NULL**) + `justification_code_snapshot` (80) + `justification_name_snapshot` (200), `observations` (1000, null) |
| Solicitante (№6) | `requester_file_public_id` (**NOT NULL**), `requester_name_snapshot` (200), `requested_by_user_id` (auditoría de digitación) |
| Canal (№7) | `origin_channel` (20: `RRHH`/`PORTAL`) |
| Plaza (D-12) | `assigned_position_public_id` (**NOT NULL**, default plaza principal resuelta en handler — ancla del insumo y del scope de liquidación) |
| Destino (№14) | `payroll_type_code` (80 — catálogo REQ-004; default el de la plaza si está clasificada), `payroll_period_id`/`payroll_period_public_id` (NULL, №14) + `payroll_period_label` (80, obligatoria) + `payroll_period_end_date date NULL` |
| Flujo | `status_code` (80), `decided_by_user_id`/`decided_utc`/`decision_note` (500), `annulled_by_user_id`/`annulled_utc`/`annulment_reason` (500), **`annulled_by_settlement_public_id uuid NULL`** (futuras anuladas al liquidar — №13/№15) |
| Costura (RF-013) | `compensated_by_credit_public_id uuid NULL` (poblado por la validación cruzada cuando REQ-002 conviva) |

- CHECKs: `duration_minutes BETWEEN 0 AND 59` · `(duration_hours * 60 + duration_minutes) > 0` · `factor_applied > 0` · `type_factor_snapshot > 0`.
- Índices: `(tenant_id, personnel_file_id, status_code)` · `(tenant_id, status_code, work_date)` (consulta + totales) · `(tenant_id, status_code, payroll_type_code)` (pendientes/insumo) · `(tenant_id, personnel_file_id, work_date)` (tope diario №12) · UQ `public_id`.

**`personnel_file_overtime_record_applications`** (`PersonnelFileOvertimeRecordApplication : TenantEntity`):

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `overtime_record_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Aplicación | `applied_date`, `payroll_type_code` (80, snapshot del periodo **real**), `payroll_period_id`/`payroll_period_public_id` (NULL, №14) + `payroll_period_label` (80), `origin_code` (20: `MANUAL`/`MOTOR`/`LIQUIDACION`), `status_code` (20: `APLICADA`/`ANULADA`), `applied_by_user_id`, `settlement_public_id uuid NULL` (solo `origin=LIQUIDACION`, №15), `annulment_reason` (500)/`annulled_by_user_id`/`annulled_utc`, `notes` (500, null) |

- **Índice único parcial**: `(tenant_id, overtime_record_id) WHERE is_active` — a lo sumo **una aplicación activa** por solicitud (№11); la reversión pone `is_active=false` + `ANULADA` y la solicitud vuelve a `AUTORIZADA` (histórico visible, RN-10).
- Índices: `(tenant_id, overtime_record_id)` · `(tenant_id, payroll_type_code, applied_date)`.

### Dominio (constantes y mutadores custodiados)

- `OvertimeRecordStatuses`: `EN_REVISION`, `AUTORIZADA`, `RECHAZADA`, `APLICADA`, `ANULADA` + conjuntos `Editable={EN_REVISION}`, `Applicable={AUTORIZADA}`, `Terminal={RECHAZADA, ANULADA}` (`APLICADA` no es terminal — la reversión devuelve a `AUTORIZADA`). `OvertimeRecordChannels`: `RRHH`/`PORTAL`. `OvertimeApplicationOrigins`: `MANUAL`/`MOTOR`/`LIQUIDACION`. `OvertimeApplicationStatuses`: `APLICADA`/`ANULADA`.
- Mutadores de `PersonnelFileOvertimeRecord`: `Create(...)` (factoría — deriva `duration_decimal_hours`, valida duración/factor-nota vía reglas) · `Update(...)` (solo `EN_REVISION`) · `Approve(decidedBy, now)` / `Reject(decidedBy, now, note)` · `Annul(reason, byUser, now, settlementPublicId? = null)` (desde `EN_REVISION` — retiro — o `AUTORIZADA` — revocación / anulación por liquidación de futuras) · `RetargetPeriod(...)` (solo `AUTORIZADA`) · `Apply(...)` (valida `AUTORIZADA` + `work_date ≤ hoy` + sin aplicación activa → hija + `APLICADA`) · `AnnulApplication(appPublicId, reason, byUser, now)` (→ `AUTORIZADA`) · `MarkAppliedBySettlement(settlementPublicId, now)` / `ReopenFromSettlement(settlementPublicId, now)` (№15 — reabre aplicadas **y** futuras anuladas por esa liquidación) · `MarkCompensated(creditPublicId)` / `ClearCompensation(creditPublicId)` (RF-013). Cada mutación rota `ConcurrencyToken`.
- Maestros: `Create`/`Update`/`Activate`/`Inactivate` con normalización de código (molde `CompetencyRatingScale.SetCode :159`).

---

## 3. Arquitectura

### 3.1 Configuración (PR-1, M1)

- **Subclase de catálogo** (№8): `OvertimeRecordStatusCatalogItem` + config + DbSet + seed §4 + `GeneralCatalogKeyMap` (`overtime-record-statuses`) + categoría de validación. `PayrollTypeCatalogItem` **condicional** (omitir si REQ-001/004/005/006 ya lo sembró).
- **Permisos** (D-07): codes `PersonnelFiles.ViewOvertimeRecords` / `ManageOvertimeRecords` / `AuthorizeOvertimeRecords` en `ProvisioningConstants` + `PersonnelFilePermissionCodes` + **cinco policies sobre esos tres codes**: registros `ViewOvertimeRecords`/`ManageOvertimeRecords` (**authn-only** con comentario de diseño — canal portal №7), maestros `OvertimeConfigurationPolicies.Read/Manage` (**estrictas** con assertion + fallback Admin — №4a), y **`AuthorizeOvertimeRecords` con `RequireAssertion` sin Admin** (№5); registro en `Program.cs` + gates `EnsureCanView/Manage/AuthorizeOvertimeRecordsAsync` fail-closed + alta en `AuthorizationPolicyConventionGovernanceTests` (№4).
- **Preferencias** (№17): 2 columnas + `SetOvertimePolicies` + GET/PUT/PATCH en `CompanyPreferencesController`.
- **openapi temprano** (№18).

### 3.2 Maestros por empresa + plantilla (PR-1, M1) — molde `CostCenter`

- Entidades §2 + repositorios: normalización en dominio (molde `CostCenterNormalization.cs:20` + `SetCode :114-118`), **probe de duplicado** (`CostCenterRepository.cs:23` `CodeExistsAsync`) + **fallback anti-carrera** por el índice único `(tenant_id, normalized_code)` (`CostCenterConfiguration.cs:78-80`; `catch (UniqueConstraintViolationException)` molde `CostCenterAdministration.cs:509/:618`) → 422 `OVERTIME_TYPE_CODE_TAKEN`/equivalente.
- Controllers (molde exacto `CostCentersController.cs:25-28` — **sin DELETE físico**): `GET companies/{companyId}/overtime-types` (list) · `GET …/{publicId}` · `POST` · `PUT` · `PATCH …/activation` / `…/inactivation` (baja lógica — inactivar siempre permitido: los históricos conservan snapshot y los registros nuevos quedan bloqueados por RN-19; ídem `overtime-justification-types`). Policy-set **estricto propio** (№4a): `OvertimeConfigurationPolicies.Read/Manage` sobre los mismos codes `View/ManageOvertimeRecords` + fallback estándar (`Admin`/`ManageAdministration`) — distinto de las policies authn-only de los registros. Si se declaran `[ResourceActions]` governed → gotcha `ISupportsAllowedActions` en todos los DTOs PUT/PATCH.
- `OvertimeTemplateSeeder` + hook provisioning + `POST companies/{companyId}/overtime-configuration/load-template` (№9 — idempotente por código; los 4 tipos A.2 + 6 justificaciones; guardrail 2.ª corrida = 0 cambios).

### 3.3 Reglas puras — `OvertimeRecordRules` (PR-2, gate)

Módulo estático puro (molde `SettlementCalculationRules` — sin I/O), errores bilingües con paridad:

- `DeriveDecimalHours(hours, minutes)` → valida ≥0 / 0–59 / total > 0 → decimal 2 AwayFromZero.
- `ValidateFactor(factorApplied, typeFactorSnapshot, note)` → > 0; nota obligatoria si difiere (P-06) → `OVERTIME_FACTOR_NOTE_REQUIRED`.
- `ValidateDailyCap(existingActiveMinutes, newMinutes, capMinutes?)` (P-05) → `OVERTIME_DAILY_CAP_EXCEEDED` con límite y acumulado.
- `CanTransition(from, to)` (RN-01/RN-02) · `CanApply(status, hasActiveApplication, workDate, today)` (**fecha transcurrida** — №13) · `CanRetarget(status)` · `CanRevertApplication(status)`.
- `IsOverdue(periodEndDate?, today)` (№14; NULL → no derivable).
- `ValidateWorkDate(workDate, today, sanityCapDays?)` (№13 — futura permitida; límite de sanidad).
- **Settlement helpers** (№15): `FactoredHours(records)` = Σ(horas × factor) redondeo 2 · `HourlyRate(dailySalary, standardDailyHours)` (reutilizar el de `CompensatoryTimeRules` si REQ-002 corrió — un solo redondeo en el sistema) · `SettlementAmount(factoredHours, hourlyRate)`.
- **Gate del PR**: los casos dorados A.4 del análisis (1, 3–7, 9–12, 15–16 en su parte pura) en verde como `OvertimeRecordRulesTests`.

### 3.4 Application + endpoints (PR-3/PR-4/PR-5)

Archivos en `Features/PersonnelFiles/Overtime/`: `OvertimeRecords.cs` (DTOs/commands/validators), `OvertimeRecords.Handlers.cs`, `OvertimeRecords.Rules.cs` (guards de handler + `OvertimeAuthorizerGuards` №6), `OvertimeRecordApplications.cs` + `.Handlers.cs`, `OvertimeRecordsBandeja.cs` (query corporativa + pendientes + exports + insumo), `OvertimeMasters.cs` + `.Handlers.cs`, `Templates/OvertimeTemplateSeeder.cs`.

| Controller | PolicySet | Rutas (`api/v1/…`) |
|---|---|---|
| `OvertimeTypesController` / `OvertimeJustificationTypesController` | (`OvertimeConfigurationPolicies.Read`, `.Manage`) — estricto №4a | CRUD §3.2 (sin DELETE; activation/inactivation) + `load-template` |
| `OvertimeRecordsController` | (`ViewOvertimeRecords`, `ManageOvertimeRecords`) — ambas authn-only, gates en handler (№7) | `GET/POST personnel-files/{publicId}/overtime-records` (POST: Manage **o** self+preferencia — canal portal) · `GET/PUT/DELETE …/{id}` (self solo propio `EN_REVISION` origen `PORTAL`) · `PATCH …/{id}/annulment` (retiro del trámite; self ídem) · `PATCH …/{id}/period` (re-imputación — solo `AUTORIZADA`, solo Manage) · `GET/POST …/{id}/applications` (histórico + unitaria — solo Manage) · `PATCH …/{id}/applications/{appId}/annulment` (reversión — solo Manage) |
| `OvertimeRecordResolutionController` (№4) | (`ViewOvertimeRecords`, **`AuthorizeOvertimeRecords`**) | `PATCH personnel-files/{publicId}/overtime-records/{id}/resolution` (autorizar/rechazar — **anti-self triple** №6) · `PATCH …/{id}/revocation` (desde `AUTORIZADA`, motivo obligatorio) |
| `OvertimeRecordsReportingController` (sin policy-set — №4) | — | `POST companies/{companyId}/overtime-records/query` (consulta RF-011 — gate View; filtros + `StatusCounts` + **totales en horas** №16) · `POST …/pending/query` (pendientes/atrasadas — gate View) · `POST …/apply-period` (lote con exclusión — **gate Manage**) · `GET …/export` · `GET …/pending/export` · `GET …/payroll-input/export` (insumo — tipo de planilla + periodo obligatorios → 400) — exports con `[EnableRateLimiting(Export)]` |

Filtros del `query` (RF-011): `statusCodes[]`, `employeeId`, `overtimeTypePublicId`, `justificationTypePublicId`, `fromWorkDate`/`toWorkDate`, `payrollTypeCode`, `payrollPeriod`, `requesterFilePublicId`, **`originChannel`** (la vista literal del levantamiento = `PORTAL`), `assignedPositionPublicId`, `search`, paginación (1–100). Respuesta: `Items` + `StatusCounts` + `TotalHours` + `TotalsByType[]`. Convenciones: If-Match en writes, ETag rotativo, errores `extensions.code` bilingües, enums string, auditoría en transacción. **Lectura self** (P-12): el listado por expediente pasa con `isSelf` (gate №7).

### 3.5 Aplicación y reversión (PR-4 — el corazón operativo)

- **Unitaria**: transacción → `AcquireOvertimeRecordMutationLockAsync` (№11) → recarga y re-verifica `AUTORIZADA` + `work_date ≤ hoy` + sin aplicación activa → `Apply` (snapshot del periodo real — default el destino; `origin=MANUAL`) → `APLICADA` → commit.
- **Lote por periodo** (`apply-period`): input = `payrollTypeCode` + periodo + `excludedRecordPublicIds[]` (posponer). Candidatos = `AUTORIZADA` del filtro con fecha transcurrida (incluye atrasadas) − excluidas → locks ordenados → aplicar → commit. **Atómico** (violación → rollback + 422 con el registro en conflicto). Respuesta: aplicadas + pospuestas + **omitidas por fecha futura** (informativo).
- **Reversión**: lock + `AnnulApplication` → `AUTORIZADA` en la misma transacción (histórico visible).
- **Re-imputación**: If-Match + solo `AUTORIZADA`; cambia destino sin tocar horas/factor.
- **Test de carrera obligatorio**: dos `apply-period` concurrentes → cero dobles aplicaciones (índice único parcial = red final).

### 3.6 Integración con liquidación (PR-6 — №15, RF-014)

Los **6 toques** del №15 (contexto → input → spec → case → hook de emisión → hook de anulación; el anti-duplicado viene gratis por el flag del seed), con:

- Seed del concepto `HORAS_EXTRAS_PENDIENTES_PAGO = -9915` (`CreateSettlementConceptSeed` — helper `GlobalCatalogSeedData.cs:1184-1221`, 13 parámetros posicionales): ConceptClass **Ingreso**, afecta ISSS/AFP/Renta = true (espejo `-9837` `:969`), exención Ninguna, **`isSystemCalculated=true` (parámetro 11 — imprescindible: con `false` el case nunca se ejecuta por el corte `:373-377`; contraste `SALARIO :962` true)**, defaultRatePercent null, sortOrder 81.
- Cierre al emitir: registros del contexto → `MarkAppliedBySettlement` (una aplicación `origin=LIQUIDACION` por registro, idempotente); **futuras de la plaza → `Annul(…, settlementPublicId)`**; anulación de la liquidación → reapertura de ambas cosas (`ReopenFromSettlement`).
- Golden del case: 2.50 h × 2.00 + 1.50 h × 2.50 = 8.75 h-factor; salario diario $10, 8 h/día → base $1.25 → **$10.94** (A.4-16).
- Retrocompatibilidad: suite de settlements existente verde sin edición (solo fixtures nuevos); test en ambos sentidos (sin módulo → todo igual; con pendientes → línea presente/editable/excluible).

### 3.7 Costura con REQ-002 (PR-6 — RF-013, **condicional a REQ-002 mergeado**)

- Servicio `IOvertimeCompensationLinkService`: `ValidateAndLinkAsync(overtimeRecordPublicId, creditPublicId, personnelFileId)` — registro existe + mismo expediente + `AUTORIZADA` (no `APLICADA` — doble beneficio → 422) → `MarkCompensated`; `UnlinkAsync` al anular la acreditación → `ClearCompensation`. Invocado desde los handlers de acreditación de REQ-002 (validación del `overtimeRecordPublicId` que hoy viaja sin validar — su RN-19).
- Efectos aquí: insignia «compensada» en detalle/consulta; **exclusión del insumo de pago** y del contexto de liquidación (№15); advertencia/422 al aplicar unitariamente una compensada.
- Si REQ-002 **no** está mergeado al construir: RF-013 se difiere a un PR de integración posterior (el campo `compensated_by_credit_public_id` queda en el modelo desde M2 — costo cero).

### 3.8 DevSeed + guía FE (PR-6)

- `DevSeedService`: `SeedOvertimeAsync` — plantilla aplicada al tenant demo + 1 `EN_REVISION` origen `PORTAL` (2 h 30 m HED), 1 `AUTORIZADA` con fecha futura (jornada organizada), 1 `APLICADA`, 1 `AUTORIZADA` atrasada.
- `guia-integracion-frontend-horas-extras.md`: contratos, 5 estados, canal portal (preferencia + gates + origen), duración h:m/decimal, factor con nota, jornadas futuras (no aplicables hasta la fecha), aplicar/posponer/re-imputar/revertir, consulta con filtro de origen + totales en horas, insumo, línea de liquidación (edición/exclusión y su efecto), modo degradado del periodo, catálogos wire y errores.

### 3.9 Localización y auditoría

~16 códigos nuevos EN+ES+es-SV con paridad (`BackendMessageLocalizationTests`) + `validation.message.*` por validador. Auditoría: doble-`SaveChanges` por write; `ReportExported` en exports; motivos obligatorios; snapshots (tipo+factor, justificación, solicitante, periodo real); `originChannel` persistido.

---

## 4. Migraciones y seeds

**M1 — `AddOvertimeConfiguration`** (PR-1): tablas `overtime_types`/`overtime_justification_types` + catálogo de estados + 2 columnas de preferencias.

| Catálogo | Códigos → IDs |
|---|---|
| `OVERTIME_RECORD_STATUS_CATALOG` (tabla nueva) | `EN_REVISION=-9910` · `AUTORIZADA=-9911` · `RECHAZADA=-9912` · `APLICADA=-9913` · `ANULADA=-9914` |
| `PAYROLL_TYPE_CATALOG` (espec REQ-004 — **omitir si ya sembrado**) | `MENSUAL=-9890` … `OTRO=-9895` |

**M2 — `AddOvertimeRecords`** (PR-2): 2 tablas de dominio + índices/CHECKs + índice único parcial (§2).

**M3 — `AddOvertimeSettlementConcept`** (PR-6, adelantable a M1): concepto `HORAS_EXTRAS_PENDIENTES_PAGO = -9915`. Holgura restante del bloque: `-9916…-9919`.

Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add <M> -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · guardrail `MigrationSeedingIntegrationTests` (idempotencia; **plantilla NO es HasData** — es seeder por empresa). Verificar IDs libres al abrir PR-1 (№10 — filtrar por sufijo `L`; trampa GUID-fragments).

---

## 5. Pruebas

- **Unitarias**: `OvertimeRecordRulesTests` — golden A.4 como **gate de PR-2**: derivación 2 h 30 m = 2.50 / 0 h 45 m = 0.75, minutos 65 → error, total 0 → error, factor editado sin nota → error, tope diario, transiciones, `CanApply` (aplicación activa / **fecha futura**), `IsOverdue` (± end-date), settlement helpers ($10.94 del A.4-16) + **paridad de localización**.
- **Integración** (`ApiIntegrationTests.Overtime.cs`): maestros (CRUD + duplicado + en-uso + `load-template` idempotente — 2.ª corrida 0 cambios) · CRUD + flujo (anti-self **triple**: registrador/sujeto/solicitante → 403; Admin sin `Authorize*` → 403) · **canal portal** (preferencia off → rechazo accionable; on → crea propio origen `PORTAL`; expediente ajeno → 403; self-edit solo `EN_REVISION` propio; lectura self) · fecha futura (crear/autorizar OK, **aplicar → 422**) · tope diario (2 registros que suman sobre el límite → 422) · factor con nota · re-imputación (sale de un insumo, entra al otro) · aplicación unitaria + **lote con exclusión + test de carrera** · reversión → `AUTORIZADA` con histórico · retirado (alta 422; pendiente solo rechazo/anulación) · consulta (filtros incl. **origen** + `StatusCounts` + totales en horas cuadrando contra la búsqueda plana) · exports + **insumo cuadrado contra pendientes** (excluye anuladas/aplicadas/compensadas/futuras) · **liquidación e2e** (línea calculada $10.94 editable/excluible; emitir → `APLICADA` origen `LIQUIDACION` + futuras anuladas; anular → reapertura de ambas; excluir línea → nada se cierra; guard anti-duplicado manual) · costura REQ-002 si aplica (vínculo inválido/ajeno/aplicada → 422; compensada fuera del insumo y del contexto) · **test de no-escritura** (`PersonnelFilePayrollTransaction`, `PersonnelFileCompensationConcept`, `PersonnelFilePersonnelAction` sin filas nuevas) · governance (policies + exención reporting).
- **Guardrails**: openapi (№18) + seeds (`MigrationSeedingIntegrationTests`) + `AuthorizationPolicyConventionGovernanceTests` (facts `:197/:276/:301`) + `RateLimitingGovernanceTests` (exports nuevos) + paridad resx `BackendMessageLocalizationTests` (`:132` misma-llave EN/ES; la paridad NO vive en los `*RulesTests`).

---

## 6. Orden de PRs

- **PR-1 — Configuración (M1)** (§3.1/§3.2): 2 maestros + CRUD governed + `OvertimeTemplateSeeder` + `load-template` + catálogo de estados `-9910…-9914` + `payroll-types` condicional + key map/categoría + 3 permisos/policies/gates (**Authorize sin Admin №5; View/Manage authn-only №7**) + 2 preferencias + governance tests + openapi temprano. *Verificar IDs libres (№10) y el estado de `payroll-types` y del seeder de REQ-001 (№9).*
- **PR-2 — Dominio + reglas (M2)** (§2/§3.3): 2 entidades + mutadores custodiados + configs/índices/CHECKs (único parcial) + `OvertimeRecordRules` puro con **golden A.4 en verde (gate)** + métodos de repositorio (idioma №3) + `AcquireOvertimeRecordMutationLockAsync`.
- **PR-3 — Flujo end-to-end** (§3.4): `OvertimeRecordsController` (CRUD `EN_REVISION` + **canal portal** №7 + anulación + tope diario + límite de sanidad de fecha №13) + `OvertimeRecordResolutionController` (resolución **anti-self triple** №6 + revocación) + re-imputación.
- **PR-4 — Aplicación** (§3.5): unitaria + **lote por periodo con exclusión** (atómico, locks ordenados, **test de carrera**) + guard fecha-transcurrida + reversión + bandeja de pendientes/**atrasadas** + par de periodo (**decidir №14** — coordinar con REQ-005/006).
- **PR-5 — Consulta + exportaciones** (§3.4/№16): `query` corporativo (filtros + `StatusCounts` + **totales en horas**) + consulta en ficha + lectura self + 3 exports con rate limiting + **insumo de planilla** cuadrado en tests.
- **PR-6 — Integración liquidación + cierre** (§3.6/§3.7/§3.8): 6 toques №15 (contexto→input→spec→`case`→hooks Issue/Annul) + concepto `-9915` **`IsSystemCalculated=TRUE`** (M3) + **decidir №3 horas-día** + costura REQ-002 (RF-013, condicional) + suite E2E completa + test de no-escritura + `openapi.yaml` final (skill) + **guía FE** + DevSeed.

Cada PR con la suite completa en verde; convención de commits del repo (**sin trailer de co-autoría de IA**).

---

## 7. Riesgos técnicos y decisiones del plan

- **№1 (periodo — espejo №13 de REQ-005/006)**: FK real + `end_date` (si REQ-001 corrió) vs degradación a etiqueta — **se decide al abrir PR-4**; si REQ-005/006 conviven, tomar la misma decisión en los tres.
- **№2 (sanity cap de fecha futura)**: sugerencia ≤ +366 días → 422; **se decide en PR-3** (P-02 no fijó tope normativo).
- **№3 (horas-día del valor-hora)**: reutilizar `CompensatoryTimeStandardDailyHours` (REQ-002) si la columna existe; si REQ-007 se adelanta → constante 8 con punto único de lectura (un-line swap cuando REQ-002 llegue). **Se decide al abrir PR-6** según el backlog.
- **Granularidad del cierre por liquidación**: la línea es **una** (agregada); excluirla → ningún registro se cierra. La granularidad por-registro se logra operando antes de liquidar (revocar/aplicar) — documentado en guía FE. Si el negocio pidiera exclusión por-registro dentro del finiquito, sería evolución del contexto (no del modelo).
- **Tope diario best-effort** (№12): sin lock — dos altas simultáneas podrían exceder el tope; riesgo aceptado (sin invariante monetario). Si se endurece: lock por `(file, workDate)` — cambio local al handler.
- **Coordinación `payroll-types`**: definición única ahora compartida por **cinco** REQs (`-9890…-9895`) — la siembra el primero que construya (anotado en el backlog de todos).
- **Volumen** (el mayor de la familia — registros potencialmente diarios): índices §2 + totales agregados en BD + paginación 1–100; EXPLAIN del `(tenant_id, status_code, work_date)` en PR-5 con volumen sintético; pipeline async de exports como válvula.
- **Coexistencia en liquidación**: `-9915` (este) + `-9837` (REQ-002) + `-9888`/`-9905` (REQ-005/006) — specs y lecturas de contexto **aditivos e independientes**; sin doble conteo con REQ-002 por RN-16 (compensadas excluidas). El orden de construcción no importa: cada módulo agrega su lectura y sus hooks.
- **⚠️ Hallazgo cross-REQ (para REQ-002)**: el corte del motor (`ComputeIncomeLine:373-377`: `spec.IsManual || !concept.IsSystemCalculated` → monto manual) exige `IsSystemCalculated=true` para que un `case` se ejecute; el plan de REQ-002 §3.11 declara «el seed de `-9837` no cambia» (hoy `false`, `GlobalCatalogSeedData.cs:969`) — con eso su case de saldo compensatorio sería código muerto. **Anotado en el backlog de REQ-002**: al implementar su PR-6, voltear el seed a `true` (y con ello su vía manual actual queda bloqueada por `Settlements.Handlers.cs:622-627` — decidir si conservan concepto manual aparte) o ajustar la condición del corte.

---

## 8. Checklist de despliegue

- [ ] Migraciones **M1–M3** aplicadas (verificar el estado previo de `payroll-types` si otro REQ corrió antes).
- [ ] **Sin storage nuevo** (P-10), sin jobs, sin appsettings nuevos.
- [ ] **Confirmar con el contador los factores de la plantilla** (P-03: HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 + posible tipo «día de descanso» Art. 175) **antes** de ejecutar `load-template` en el tenant productivo.
- [ ] Paso de adopción por empresa: `load-template` de los 2 maestros (o creación manual — factores editables), configurar preferencias (`OvertimeSelfServiceEnabled` — **piloto recomendado**, default off — y `OvertimeMaxDailyMinutes` si aplica) y asignar `AuthorizeOvertimeRecords` a los autorizadores (Admin **no** decide sin el grant).
- [ ] Comunicar la **operación por periodo** (bandeja de pendientes/atrasadas + lote + insumo = puente con la nómina externa) y el **triángulo jornada/pago/compensación** (Anexo A.5 del análisis — capacitación; el pago del monto vive en REQ-006/nómina externa; la compensación en REQ-002).
- [ ] Si REQ-002 ya está en producción: verificar la costura RF-013 (validación del vínculo + insignia + exclusión) y la fuente de horas-día (№3).
