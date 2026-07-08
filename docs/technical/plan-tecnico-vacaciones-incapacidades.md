# Plan Técnico de Implementación — Vacaciones e Incapacidades (Incapacidades · Lactancia · Fondo de vacaciones · Plan anual · Solicitudes/Devolución)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-vacaciones-incapacidades-empleado.md`](../business/analisis-vacaciones-incapacidades-empleado.md) (**D-01…D-27 + P-01…P-18 ratificadas 2026-07-04; Anexo A.2/A.3/A.4 confirmados por el negocio**) |
| **Módulos** | `PersonnelFiles` (Incapacities · Lactation · Vacations — net-new) · **maestros de configuración por empresa** (clínicas, riesgos, tipos, asuetos, periodos de planilla — net-new) · `EmploymentAssignments` (+`restDayOfWeek`) · `EmployeeProfiles` (saldos derivados) · `Preferences` (columnas nuevas) · `GeneralCatalogs` (3 TPH + 5 ActionTypes) · Provisioning (RBAC + plantillas) · Files (nuevo purpose) · Reporting/Export · Settlements (sugerencia de goce) · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-07-04 |
| **País de referencia** | El Salvador (SV, `CountryCatalogItemId = -7068L`) |
| **Endurecimientos de la ratificación** | Fondo **POR EMPLEADO** (D-05) · motor de **días + montos referenciales** (D-21, salario/30) · devolución **total y parcial en F1** (D-14) · **auto-registro** de incapacidad `EN_REVISION` + confirmación RRHH (D-18) · riesgos/tipos **maestros por empresa** con plantilla SV **confirmada** (D-07/D-08, A.2) · **maestro de periodos de planilla** por empresa, sin plantilla global (D-23) · séptimo = `restDayOfWeek` **del empleado** (D-26) · tope patronal **9 días/año** parametrizable (D-27) · clínica **opcional** (maestro inicia vacío) · constancia **obligatoria default** (D-22) · Art. 178 CT como default de fechas (D-24) |

---

## 0. Aclaraciones pre-desarrollo (recomendación del desarrollador senior; ratificación ya cerrada)

1. **Plaza principal como ancla personal (D-05/D-06):** el fondo es por empleado, pero el aniversario y la base salarial salen de la **plaza principal**: `IsPrimary=true` entre las activas; si ninguna lo es (edge), la de `StartDate` más antiguo — mismo criterio P-03 del plan de liquidación. El motor de incapacidades usa la misma plaza para `restDayOfWeek` y salario cuando el registro no referencia plaza explícita.
2. **Snapshot al escribir + recálculo interno:** el desglose (días + montos + base salarial + consumo de tope) se calcula y persiste en **cada escritura de campos de cálculo** (crear, editar fechas/riesgo, cerrar indefinida). Cambios posteriores de maestros/asuetos/salario **no** recalculan históricos; no hay endpoint `regenerate` (a diferencia de liquidación: aquí la edición ya recalcula inline).
3. **Redondeo y convención monetaria (D-21):** `decimal` en toda la cadena; salario diario = `Math.Round(mensual / 30m, 2, AwayFromZero)`; monto por tramo = `Math.Round(días × diario × %/100, 2, AwayFromZero)`; totales = suma de tramos redondeados. Un único helper en el módulo de reglas — prohibido redondear en handlers. La convención "/30" viaja documentada en la exportación (riesgo R-T3).
4. **Conteo del tope patronal (D-27):** el tope (`employerCoveredIncapacityDaysPerYear` + `additionalIncapacityBenefitDaysPerYear`) se consume por **año calendario de la fecha de inicio** del evento, solo por incapacidades `REGISTRADA` (las `EN_REVISION`/`ANULADA` no cuentan). El "tope disponible" se pasa al motor como input; la verificación definitiva ocurre dentro de la transacción de escritura (releyendo consumo del año) para evitar carreras.
5. **Cadena de prórrogas:** `ExtendsIncapacityPublicId` forma una cadena lineal (una prórroga por eslabón). El **offset de día acumulado** = Σ días computables de la cadena previa, se pasa al motor para segmentar tramos. Editar fechas de un eslabón con prórrogas posteriores → 422 `INCAPACITY_CHAIN_LOCKED` (anular primero las posteriores).
6. **Clínica opcional (ratificación 2026-07-04):** no existe catálogo inicial → `clinicPublicId` nullable; si viaja, se valida activa y del tenant. El maestro es CRUD simple (solo descripción obligatoria).
7. **Periodos de planilla sin plantilla:** cada empresa carga su calendario (D-23 confirmada). `payrollPeriodPublicId` es **opcional** en la incapacidad (la empresa puede no haberlo cargado aún); si viaja, se valida activo. Sin validación de que las fechas de la incapacidad caigan dentro del periodo (es imputación, no contención — la 1.ª quincena puede pagar incapacidades de la anterior).
8. **Plantillas por empresa (riesgos/tipos/asuetos):** servicio idempotente `LeaveTemplateSeeder` (SV, valores del Anexo A.2/A.3 **confirmados**) invocado (a) en el provisioning de tenants nuevos y (b) vía endpoints admin `POST …/load-template` para tenants existentes (idempotente por código: no pisa ediciones, solo crea faltantes).
9. **Warnings ≠ errores:** plan anual devuelve `warnings[]` por línea (disponibilidad, asuetos); las solicitudes y las incapacidades usan ProblemDetails 422. Mismo contrato de warnings que liquidación (código + mensaje bilingüe).
10. **Devoluciones LIFO:** la reversa de asignaciones por defecto deshace primero la última asignación (más reciente) — editable enviando la distribución explícita. `DEVUELTA_PARCIAL` pasa a `DEVUELTA` automáticamente cuando el acumulado devuelto == consumido.
11. **Fechas y reloj:** todas las comparaciones vía `IDateTimeProvider` y parámetro `asOf` en reglas puras (patrón retiro/liquidación); los tests son deterministas.
12. **`dotnet ef`** requiere `DOTNET_ROLL_FORWARD=Major` en este entorno (gotcha conocido).

---

## 1. Objetivo y enfoque

Construir el módulo de **vacaciones e incapacidades**: registro de incapacidades con **motor determinista de días y montos referenciales** por tramos ISSS (parámetros por riesgo, calendario de asuetos, descanso semanal del empleado, tope patronal anual), prórrogas encadenadas, lactancia con horarios, **fondo anual de vacaciones por empleado** (generación individual/masiva, días de ley + beneficio), plan anual indicativo, solicitudes con verificación de fondo y decisión RRHH (anti-autoaprobación), **devolución total/parcial**, adjuntos (constancia obligatoria por defecto), asientos automáticos en expediente, bandejas + exportaciones (insumo de planilla externa + provisión financiera para Finanzas), saldos publicados en el perfil e integración con la liquidación.

**Insight central del análisis de código.** No existe ni una línea de vacaciones/incapacidades en `src/`, pero el sistema ya declaró las costuras y tiene plantilla para casi todo:

1. **Entidad + ciclo híbrido + anti-autoaprobación** — `PersonnelFileEconomicAidRequest` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:1678-1880`): estados canónicos + catálogo, `Resolve/Disburse/Cancel` custodiados, `SelfApprovalForbidden` 403 (`EconomicAidRequests.Handlers.cs:371-380`). Plantilla directa del ciclo de solicitudes de vacaciones y de la confirmación de incapacidades.
2. **Gates de autogestión** — `PersonnelFileEmployeeHandlerBases.cs` (`LoadForCreateOwnOrManageEconomicAidAsync:~316`, `LoadForCreateOwnOrManageMedicalClaimAsync:~227`, lecturas `View… OR isSelf`): se añaden los gemelos `…Incapacity…` y `…VacationRequest…`.
3. **Adjuntos con propósito** — stack completo de medical-claims (`MedicalClaimDocuments.Handlers.cs:126-132,188`; `FilePurpose` en `Domain/Files/FileEnums.cs`; reglas por `Storage:Purposes` en `FileStorageOptions`/`FilePurposeRuleProvider`): se replica con `IncapacityDocument`.
4. **Motor como módulo de reglas puro + casos dorados** — patrón `SettlementCalculation.Rules.cs` (constantes `SvDefaults:24-35`, `DaysSinceAnniversary:592`): aquí vive el conteo día-a-día, la segmentación por tramos y los montos. Los golden cases del **Anexo A.4 ya están confirmados** — se codifican como `[Theory]` bloqueantes desde el día 1 (ventaja sobre liquidación, que esperó la firma del contador).
5. **Bandeja + export tabular** — `SettlementsBandeja.cs` + `SettlementsReportingController.cs` + `ReportExportDeliveryService.cs:49-110` (filas con propiedades en español, 413, auditoría, rate limits): se clona para `incapacities` y `vacation-requests`.
6. **Acciones de personal** — `PersonnelFilePersonnelAction.Create` (`PersonnelFileEmployee.cs:573-584`) + `ACTION_TYPE_CATALOG` (`BAJA=-9482`, `REVERSION_BAJA=-9483`, `LIQUIDACION=-9484`): 5 códigos nuevos `-9485…-9489`.
7. **Costuras públicas esperando dueño** — `VacationDaysAvailable`/`DisabilityDaysAvailable` en `EmployeeProfiles.cs:39-40` (hoy `null` hardcodeado en `PersonnelFileEmployeeRepository.cs:2213-2227`) y el default `DaysSinceAnniversary` de `VACACION_PROPORCIONAL` en liquidación (G-04/§17.4 de su análisis): este módulo las **puebla**, no crea campos paralelos.

**Las cuatro piezas sin plantilla directa** (foco de riesgo, §8):
- **El motor de conteo/segmentación** — primer motor de calendario del sistema (día a día, flags, cadena de prórrogas, tope anual). Mitigación: 100 % puro + A.4 como tests bloqueantes.
- **La familia de maestros por empresa con plantilla** — riesgos (con hijos parámetro), tipos, clínicas, asuetos, periodos de planilla: primer conjunto de maestros tenant-scoped con seeder de plantilla + load-template idempotente.
- **El fondo con asignaciones/devoluciones** — contabilidad de días (FIFO al consumir, LIFO al devolver) con invariantes fuertes (Σ asignaciones = solicitados; Σ devoluciones ≤ consumidos).
- **La proyección de saldos en el perfil** — sustituir los `null` por subconsultas eficientes sin degradar el GET de perfil (gotcha: proyección EF con member-init).

---

## 2. Línea base verificada en el código (qué se reutiliza / qué se toca)

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Saldos del perfil | `PersonnelFileEmployeeProfileResponse.VacationDaysAvailable/DisabilityDaysAvailable` (`EmployeeProfiles.cs:39-40`, comentario "owned by the future vacations/incapacities module"); `null` hardcodeado en `PersonnelFileEmployeeRepository.cs:2213-2227`; ya publicados en `openapi.yaml` | Se reemplazan los `null` por cálculo en la proyección (§3.10). **Contrato aditivo-cero**: los campos ya existen. |
| 2 | Sugerencia de goce en liquidación | `SettlementParametersInput.SvDefaults` (15/30 %/365, `SettlementCalculation.Rules.cs:24-35`); default `DaysSinceAnniversary:592`; unidades editables `UnitsOrFactorUsed`+`IsOverridden` (`PersonnelFileSettlement.cs:621-625`) | Query de pendientes por empleado → nuevo default de `VACACION_PROPORCIONAL`; sin fondo cae al actual (retrocompatible, §3.11). |
| 3 | Plantilla de ciclo + anti-self | `PersonnelFileEconomicAidRequest` + `EconomicAidRequestStatuses` (`PersonnelFileEmployee.cs:1678-1880`); PATCH `resolution/disbursement/cancel` con `[FromIfMatch]` (`EconomicAidRequestsController.cs`); `SelfApprovalForbidden` (`EconomicAidRequests.Handlers.cs:371-380`); auditoría doble-`SaveChanges` (`:123-135`) | Espejo para `PersonnelFileVacationRequest` (decisión/devolución) y para la confirmación de incapacidades. |
| 4 | Gates de autogestión | `PersonnelFileEmployeeHandlerBases.cs` — `LoadForCreateOwnOrManage{MedicalClaim,EconomicAid,Certificate}Async`; lecturas `View… OR isSelf`; manage-only sin rama self | +2 gates de creación (`…Incapacity…`, `…VacationRequest…`), +lecturas propias (incapacidades = dato de salud: 403 sin enmascarar), manage-only para confirmar/decidir/devolver/generar. |
| 5 | Adjuntos | `FilePurpose` (`Domain/Files/FileEnums.cs`) con `MedicalClaimDocument`…; flujo 3 patas (`MedicalClaimDocuments.Handlers.cs:126-132`, purpose-gate `:188`); reglas `Storage:Purposes` (`FileStorageOptions.cs` + `FilePurposeRuleProvider.cs`) | +`FilePurpose.IncapacityDocument` + entidad documento espejo + entrada de config en appsettings **base** + contenedor pre-aprovisionado (gotcha conocido: config faltante → 422). |
| 6 | Plaza / contrato | `PersonnelFileEmploymentAssignment` (`PersonnelFileEmployee.cs:133`: `StartDate`, `IsPrimary`, `WorkdayCode` texto libre, `PayrollTypeCode`, `Close/Reopen`); feature `EmploymentAssignments*.cs` | +columna `rest_day_of_week smallint NULL` + parámetro en `Create/Update` + request/response + validador (0–6). Aditivo, no breaking (mismo corte que `MinimumMonthlyWage` de liquidación). |
| 7 | Salario base | `PersonnelFileCompensationConcept` con `IsBaseSalary`/`SALARIO_BASE` por `AssignedPositionPublicId` (regla `CompensationConcepts.Rules.cs`) — ya lo resuelve `SettlementRepository` | `LeaveCalculationDataProvider` reutiliza la misma resolución (plaza referida o principal) para la base mensual del motor. Sin salario → 422 `INCAPACITY_BASE_SALARY_MISSING`. |
| 8 | Preferencias | `CompanyPreference` tipada (`Domain/Preferences/CompanyPreference.cs`, columnas + mutadores; lectura `ICompanyPreferenceRepository.GetByTenantIdAsync`; patrón de consumo `EconomicAidRequests.Handlers.cs:42,86-91`) | +8 columnas anulables + mutador `SetLeavePolicies(...)` + exposición en la administración de preferencias existente. |
| 9 | Catálogos TPH + wire keys | Receta subclase `GeneralCatalogItem` + const de categoría + `GeneralCatalogKeyMap` + switch `CatalogCodeIsActiveAsync` + guardrail de biyección; seeds `CreateGeneralCatalogSeed` en `GlobalCatalogSeedData.cs` | 3 TPH nuevos: `incapacity-statuses`, `vacation-request-statuses`, `clinic-sectors`. |
| 10 | Acciones de personal | Factory `PersonnelFilePersonnelAction.Create` (`PersonnelFileEmployee.cs:573-584`); tipos hasta `LIQUIDACION=-9484`; estado `APLICADA=-9495` | 5 ActionTypes nuevos **`-9485…-9489`** (INCAPACIDAD, PRORROGA_INCAPACIDAD, LACTANCIA, GOCE_VACACIONES, DEVOLUCION_VACACIONES). |
| 11 | Permisos (receta 8 archivos) | Codes en `PersonnelFilePolicies.cs`; tuples `ProvisioningConstants.CompanyAdminPermissions`; policies en `Program.cs`; gates fail-closed `IPersonnelFileAuthorizationService`; governance tests | 4 codes nuevos (`ViewIncapacities`/`ManageIncapacities`/`ViewVacations`/`ManageVacations`), todos con fallback `Admin`/`ManageAdministration` (los `Authorize*` que excluyen Admin son Fase 2). |
| 12 | Bandeja + export | `SettlementsBandeja.cs` (+`StatusCounts`), `SettlementsReportingController.cs` (`POST …/query`, `GET …/export` con `[EnableRateLimiting(Export)]`), `ReportExportFileWriter.WriteAsync<TRow>` (filas en español), `ReportExportDeliveryService.cs:49-110` | Clonar 2×: `IncapacitiesReportingController` y `VacationRequestsReportingController` (+ export de fondo/provisión y query de calendario). |
| 13 | Maestros tenant governed | Familia `[ResourceActions]` + `ISupportsAllowedActions` (`Api/Authorization/ResourceActionsAttribute.cs`, `AllowedActionsResultFilter.cs`; precedentes JobProfiles/CostCenters/OrgUnits) — **gotcha memorado: TODOS los DTOs de PUT/PATCH deben implementar `ISupportsAllowedActions`** (solo los tests de integración lo detectan) | Los 5 maestros nuevos (clínicas, riesgos, tipos, asuetos, periodos de planilla) siguen esta familia; los sub-recursos de expediente NO (van por policy-set + gates, corte de retiro/liquidación). |
| 14 | Multi-tenant | `TenantEntity` + query filter global (`ApplicationDbContext.cs:514-517`) | Todas las entidades nuevas son `TenantEntity` (no hay catálogos país nuevos: los maestros son por empresa — D-07/D-08/D-09/D-23). |
| 15 | IDs de seed libres | Piso general **-9846** (INCAF); acciones hasta **-9484**; SV `-7068L` | **TPH estados incapacidad = -9850…-9852** · **estados solicitud = -9853…-9858** · **sector clínica = -9860…-9862** · **ActionTypes = -9485…-9489**. Verificar contra `GlobalCatalogSeedData` al abrir PR-1. |
| 16 | Wire / If-Match / auditoría | `PublicContractNaming` (Guid `XxxId`→`xxxPublicId`); `[FromIfMatch]`; DELETE `parentConcurrencyToken`; `[JsonIgnore] Id => XxxPublicId` en responses; errores `extensions.code` bilingües | Convenciones obligatorias en todos los handlers/controllers nuevos. Gotcha memorado: `[Required]` en records posicionales debe ser param-target (no `[property:…]`) o produce 500s. |
| 17 | Localización | `BackendMessages.resx`/`.es.resx`/`.es-SV.resx` + paridad `BackendMessageLocalizationTests` | ~30 códigos nuevos EN+ES **en el mismo PR que los introduce** + `validation.message.*` por cada `WithMessage`. |
| 18 | DevSeed | `DevSeedService.cs` (tenant demo) | Sembrar en dev: maestros de plantilla aplicados, asuetos 2026, 2 periodos de planilla, fondo demo, 1 incapacidad + 1 solicitud aprobada (para FE). |

---

## 3. Arquitectura de la solución

### 3.1 Maestros de configuración por empresa (D-03/D-07/D-08/D-09/D-23/RF-001…004/026)

Nuevo namespace de dominio **`src/CLARIHR.Domain/Leave/`** — 6 entidades `TenantEntity`:

| Entidad | Tabla | Campos de negocio | Reglas |
|---|---|---|---|
| `MedicalClinic` | `medical_clinics` | `Description` (200, req.), `Specialty` (150), `SectorCode` (catálogo TPH `clinic-sectors`) | Descripción única normalizada por tenant; **sin semilla** (inicia vacío) |
| `IncapacityRisk` | `incapacity_risks` | `Code`/`NormalizedCode`, `Name`, flags `CountsSeventhDay/CountsSaturday/CountsHoliday/UsesWorkSchedule/AllowsIndefinite/AllowsExtension/UsesFund/HasSubsidy` | Código único por tenant; `HasSubsidy=false` ⇒ sin parámetros ISSS; editar NO recalcula históricos |
| `IncapacityRiskParameter` | `incapacity_risk_parameters` | `DayFrom` (≥1), `DayTo` (null=∞), `SubsidyPercent` (0–100), `PayerCode` (`ISSS`/`EMPRESA`/`SIN_PAGO`), `SortOrder` | Colección hija del riesgo; **contiguos desde 1, sin solapes** (guard de dominio `ReplaceParameters(...)` valida la secuencia completa) |
| `IncapacityType` | `incapacity_types` | `Code`, `Name`, `DeductionTypeText` (150), `IncomeTypeText` (150), `AppliesToWorkAccident` | Texto informativo (P-07); plantilla incluye `LACTANCIA` |
| `CompanyHoliday` | `company_holidays` | `Date` (única por tenant), `Description` (200), `ScopeCode` (`NACIONAL`/`LOCAL`/`INSTITUCIONAL`) | Fechas concretas por año; editar no recalcula históricos (snapshot en cada incapacidad) |
| `PayrollPeriodDefinition` | `payroll_period_definitions` | `PayPeriodTypeCode` (valida `PAY_PERIOD_CATALOG`), `Year`, `Number`, `Label` (80), `StartDate`, `EndDate` | Único `(tenant, tipo, año, número)`; sin solape de rangos por tipo/año (guard + validación en handler); **sin plantilla** (cada empresa carga su calendario) |

- **Plantilla SV (`LeaveTemplateSeeder`, `Application/Features/Leave/Templates/`)**: aplica riesgos A.2 (con parámetros **confirmados**: `ENFERMEDAD_COMUN`/`ACCIDENTE_COMUN` 1–3→75 % EMPRESA + 4–∞→75 % ISSS; `ACCIDENTE_TRABAJO`/`ENFERMEDAD_PROFESIONAL` 1–∞→100 % ISSS sin tope patronal; `MATERNIDAD` 1–112→100 % ISSS), tipos mínimos (incl. `LACTANCIA`) y asuetos A.3 del año indicado. **Idempotente por código/fecha** (crea faltantes, nunca pisa ediciones). Invocación: (a) hook de provisioning de tenant nuevo; (b) `POST /api/v1/companies/{companyId}/leave-configuration/load-template?year=YYYY` (admin, para tenants existentes).
- **Controllers** (familia governed): `MedicalClinicsController`, `IncapacityRisksController` (riesgo + `PUT …/parameters` reemplaza el set completo, patrón income-tax-brackets), `IncapacityTypesController`, `CompanyHolidaysController`, `PayrollPeriodsController` — rutas company-scoped, `[ResourceActions]` + **`ISupportsAllowedActions` en TODOS los DTOs de PUT/PATCH** (gotcha memorado), If-Match, baja lógica con guard de uso (riesgo/tipo/periodo referenciado por incapacidad activa → 422).

### 3.2 Catálogos TPH + tipos de acción (D-16/D-19)

Receta estándar (subclase + categoría + wire key + switch + seed + guardrail de biyección):

| Catálogo | Wire key | Códigos (ID seed, SV `-7068L`) |
|---|---|---|
| `IncapacityStatusCatalogItem` | `incapacity-statuses` | `EN_REVISION=-9850`, `REGISTRADA=-9851`, `ANULADA=-9852` |
| `VacationRequestStatusCatalogItem` | `vacation-request-statuses` | `SOLICITADA=-9853`, `APROBADA=-9854`, `RECHAZADA=-9855`, `ANULADA=-9856`, `DEVUELTA_PARCIAL=-9857`, `DEVUELTA=-9858` |
| `ClinicSectorCatalogItem` | `clinic-sectors` | `ISSS=-9860`, `PUBLICA=-9861`, `PRIVADA=-9862` |
| `ACTION_TYPE_CATALOG` (existente, +5) | `action-types` | `INCAPACIDAD=-9485`, `PRORROGA_INCAPACIDAD=-9486`, `LACTANCIA=-9487`, `GOCE_VACACIONES=-9488`, `DEVOLUCION_VACACIONES=-9489` |

Los estados son **híbridos** (D-16): constantes canónicas en dominio (`IncapacityStatuses`, `VacationRequestStatuses` con sets curados `Pending`, `DecisionTargets`, `Returnable`) + validación por catálogo en handlers (`CatalogCodeIsActiveAsync`).

### 3.3 Permisos, políticas y gates (D-17/D-18)

- **Codes** (en `PersonnelFilePolicies.cs`): `PersonnelFiles.ViewIncapacities`, `PersonnelFiles.ManageIncapacities` (cubre lactancia), `PersonnelFiles.ViewVacations`, `PersonnelFiles.ManageVacations` (cubre fondo, plan, decisión y devolución). Los 4 con fallback `Admin`/`ManageAdministration` (los `AuthorizeVacations/AuthorizeIncapacities` con `RequireAssertion` que excluye Admin son **Fase 2** — patrón `AuthorizeRetirement` queda referenciado, no implementado).
- **Registro**: tuples en `ProvisioningConstants.CompanyAdminPermissions` + policies en `Program.cs` + governance tests.
- **Gates** (en `PersonnelFileEmployeeHandlerBases.cs` + `IPersonnelFileAuthorizationService`):
  - `LoadForCreateOwnOrManageIncapacityAsync` — crear: `ManageIncapacities` OR `isSelf` (D-18; el origen `AUTOSERVICIO` se deriva de la rama que autorizó).
  - `LoadForCreateOwnOrManageVacationRequestAsync` — crear/anular propia `SOLICITADA`: `ManageVacations` OR `isSelf`.
  - Lecturas: `ViewIncapacities OR isSelf` (dato de salud, 403 sin enmascarar — patrón medical-claims) · `ViewVacations OR isSelf` (fondo, saldos, solicitudes propias).
  - Manage-only: confirmar/cerrar/anular incapacidad, lactancia, generar fondo, decidir, devolver, plan anual.
  - **Anti-autoacción** (helper compartido, patrón `SelfApprovalForbidden`): confirmar incapacidad propia → 403 `INCAPACITY_CONFIRM_SELF_FORBIDDEN`; decidir/devolver solicitud del propio expediente → 403 `VACATION_DECISION_SELF_FORBIDDEN`.

### 3.4 Dominio — nuevos archivos en `src/CLARIHR.Domain/PersonnelFiles/`

**`PersonnelFileIncapacity.cs`** — entidad + documento + estados:
- `PersonnelFileIncapacity : TenantEntity`: FK expediente + `BindToPersonnelFile`; solicitante (`RequesterFilePublicId?`, `RequesterNameSnapshot?`, `RequestedByUserId`); `OriginCode` (`RRHH`/`AUTOSERVICIO`); referencias `RiskId` (FK dura al maestro + snapshot `RiskCodeSnapshot`/flags usados), `ClinicId?`, `IncapacityTypeId`, `AssignedPositionPublicId?`, `PayrollTypeCode?`, `PayrollPeriodId?`; fechas `StartDate`, `EndDate?`; conteos `CalendarDays`, `ComputableDays` (+`IsOverridden`, `OverrideNote`); desglose snapshot `SubsidizedDays/DiscountDays/EmployerDays` + `MonthlyBaseSalary/DailySalary` + `SubsidyAmount/DiscountAmount/EmployerAmount` + `TrancheDetailJson` (detalle por tramo: rango, %, pagador, días, monto — auditable, patrón `CalculationDetail` de liquidación); `StatusCode`; `ExtendsIncapacityId?` (cadena); `Notes`; `IsActive`; `ConcurrencyToken` rotativo.
- Guards: `Create(...)` (fechas coherentes; indefinida solo si riesgo permite; estado inicial por origen), `ApplyCalculation(snapshot)` (única vía de escritura del desglose), `Confirm(byUserId, at)` (`EN_REVISION→REGISTRADA`), `CloseIndefinite(endDate)` (solo `EndDate == null`), `Annul(reason)` (desde `EN_REVISION|REGISTRADA`), `OverrideComputableDays(value, note)` (nota obligatoria).
- `PersonnelFileIncapacityDocument`: espejo exacto de `MedicalClaimDocument` (FilePublicId + snapshots + `DocumentTypeCatalogItemId?` + `Observations`).
- `IncapacityStatuses` (consts + sets) y `IncapacityOrigins`.

**`PersonnelFileLactation.cs`**:
- `PersonnelFileLactationPeriod : TenantEntity` (solicitante, `IncapacityTypeId` — plantilla `LACTANCIA` —, `StartDate/EndDate`, `StatusCode` reutilizando `incapacity-statuses` sin `EN_REVISION`, `IsActive`, token) + colección `LactationSchedule` (`StartDate/EndDate` dentro del periodo, `DailyPermitsCount ≥1`, `MinutesPerPermit ≥1`); guard `ReplaceSchedules(...)` valida contención y no-solape.

**`PersonnelFileVacations.cs`**:
- `PersonnelFileVacationPeriod : TenantEntity`: `PeriodYear`, `PeriodStartDate/PeriodEndDate` (derivadas: aniversario plaza principal o año calendario), `LegalDaysGranted` (>0), `BenefitDaysGranted` (≥0), `GeneratesEnjoymentDays`, `UsedAnniversary`, `UsedCompanyPolicy`, `SourceCode` (`MANUAL`/`GENERACION_MASIVA`), `IsActive`, token. Índice único filtrado `(tenant, personnel_file_id, period_year) WHERE is_active` (D-05). Saldos **derivados** (no columnas).
- `PersonnelFileVacationRequest : TenantEntity`: solicitante + `RequestedByUserId`; `StartDate/EndDate/RequestedDays`; `StatusCode`; `PlanLinePublicId?`; decisión (`DecidedByUserId?`, `DecisionDateUtc?`, `DecisionNotes?`); colecciones `Allocations` (`VacationRequestAllocation`: `VacationPeriodId`, `Days>0`) y `Returns` (`VacationReturn`: `Days>0`, `ReturnDateUtc`, `Reason`, `DecidedByUserId`, distribución de reversa persistida). Guards: `Approve(allocations, by, at)` (solo `SOLICITADA`; Σ = `RequestedDays`), `Reject(by, at, notes)`, `Cancel()` (solo `SOLICITADA`), `Return(days, reason, by, at, distribution)` (solo `APROBADA|DEVUELTA_PARCIAL`; acumulado ≤ consumido; transición automática a `DEVUELTA` al agotar).
- `VacationRequestStatuses` (consts + sets).

**`src/CLARIHR.Domain/Leave/VacationPlan.cs`** (nivel empresa): `VacationPlan : TenantEntity` (`PlanYear`, `RequestDate`, solicitante + snapshot, `StatusCode` `VIGENTE/ANULADO`) + `VacationPlanLine` (`PersonnelFilePublicId`, `StartDate/EndDate`, `Days`; sin solape entre líneas del mismo empleado — guard en agregado).

**`CompanyPreference`** (+8 columnas anulables + `SetLeavePolicies(...)`): `AnnualVacationDaysDefault`, `AdditionalVacationBenefitDaysDefault`, `AllowVacationStartOnHoliday`, `AllowVacationEndOnHoliday`, `AllowVacationStartOnRestDay`, `DefaultUseAnniversary`, `CompanyRestDayOfWeek`, `EmployerCoveredIncapacityDaysPerYear`, `AdditionalIncapacityBenefitDaysPerYear`, `IncapacityRequiresDocument` — defaults nulos = legales (15 / 0 / **no** / sí / **no** / sí / domingo / 9 / 0 / **sí**).

**`PersonnelFileEmploymentAssignment`** (+1): `RestDayOfWeek` (`DayOfWeek?`) + parámetro en `Create/Update` + contrato de assignments (aditivo).

### 3.5 Módulo de reglas puro — `Features/PersonnelFiles/Incapacities/IncapacityCalculation.Rules.cs` (+ `Vacations/VacationRules.cs`, `Lactation/LactationRules.cs`)

**`IncapacityCalculationRules.Calculate(input) → IncapacityCalculationResult`** — estático, sin side-effects, sin reloj. Input: `StartDate`, `EndDate?` (indefinida ⇒ solo validación, desglose diferido al cierre), flags del riesgo, parámetros ordenados, `HashSet<DateOnly> holidays`, `DayOfWeek restDay`, `chainOffsetDays` (Σ computables de la cadena previa), `MonthlyBaseSalary`, `employerCapRemaining` (decimal, ∞ si sin tope). Pasos:
1. **Días naturales** = `end − start + 1`.
2. **Computables**: escaneo día a día excluyendo `restDay` si `!CountsSeventhDay`, sábado si `!CountsSaturday`, asueto si `!CountsHoliday` (un día excluido no cuenta para tramos ni montos).
3. **Numeración de cadena**: día computable *k* del registro = día `chainOffsetDays + k` para la segmentación (RN-03).
4. **Segmentación por tramos**: partición de los días computables según `[DayFrom, DayTo]`; riesgo `HasSubsidy=false` ⇒ todo a `SIN_PAGO` 0 %.
5. **Resolución de pagador con tope** (D-27): días de tramo `EMPRESA` consumen `employerCapRemaining`; el excedente del tramo se reclasifica a `SIN_PAGO` (descuento). Tramos `ISSS` no consumen tope.
6. **Montos** (D-21): `daily = round(monthly/30, 2)`; por tramo `round(días × daily × pct/100, 2)`; agregados `SubsidyAmount/DiscountAmount/EmployerAmount`. `DiscountDays/Amount` = días `SIN_PAGO` (lo que la planilla descuenta al empleado).
7. **Resultado**: conteos + montos + `TrancheDetail[]` (desde–hasta absoluto de cadena, %, pagador, días, monto) + `Warnings[]` (p. ej. `INCAPACITY_WARNING_CAP_EXHAUSTED`).

**`VacationRules`**: `ValidateRequestDates(start, end, holidays, restDay, prefs)` (Art. 178 defaults — RN-27), `AvailableDays(period, consumptions)` (otorgados ley+beneficio − aprobado-no-devuelto), `SuggestFifoAllocations(periods, requestedDays)`, `SuggestLifoReturn(allocations, daysToReturn)`, `PeriodBounds(year, useAnniversary, primaryPlazaStartDate)`, `IsEligible(seniority ≥ 1 año al inicio del periodo)`. **`LactationRules`**: contención y no-solape de horarios.

Paridad de localización: cada código de error/warning del módulo de reglas tiene recurso EN/ES (test espejo del patrón multiples-plazas).

### 3.6 Aplicación — feature folders

```
Application/Features/Leave/                      ← maestros por empresa + plantilla
  MedicalClinics.cs / .Handlers.cs               IncapacityRisks.cs / .Handlers.cs / .Rules.cs
  IncapacityTypes.cs / .Handlers.cs              CompanyHolidays.cs / .Handlers.cs
  PayrollPeriods.cs / .Handlers.cs / .Rules.cs   Templates/LeaveTemplateSeeder.cs
  VacationPlans.cs / .Handlers.cs / .Rules.cs    ← plan anual (nivel empresa, warnings)
Application/Features/PersonnelFiles/Incapacities/
  Incapacities.cs / .Handlers.cs                 IncapacityCalculation.Rules.cs
  IncapacityDocuments.cs / .Handlers.cs          IncapacityBalances.cs (saldos: acumulado/tope/restante)
  IncapacitiesBandeja.cs / .Handlers.cs          LeaveCalculationDataProvider.cs (salario, restDay, asuetos, tope, cadena)
Application/Features/PersonnelFiles/Lactation/
  LactationPeriods.cs / .Handlers.cs / LactationRules.cs
Application/Features/PersonnelFiles/Vacations/
  VacationPeriods.cs / .Handlers.cs              VacationPeriodsMassGeneration.cs (resumen creados/omitidos/errores)
  VacationRequests.cs / .Handlers.cs             VacationRules.cs
  VacationFundDetail.cs (detalle + provisión)    VacationsCalendar.cs (query anual)
  VacationRequestsBandeja.cs / .Handlers.cs
```

Convenciones en todos los handlers: CQRS + FluentValidation; validación por catálogo/referencia activa; auditoría doble-`SaveChanges` (patrón `EconomicAidRequests.Handlers.cs:123-135`); asiento de personal en la **misma transacción** que el evento (RN-08); DTOs response con `[JsonIgnore] Id => XxxPublicId`.

### 3.7 API — controllers y contratos

| Controller | Endpoints clave | Gate |
|---|---|---|
| `PersonnelFileIncapacitiesController` | `GET/POST /personnel-files/{publicId}/incapacities` · `GET/PUT /…/{id}` · `PATCH /…/{id}/confirmation` · `PATCH /…/{id}/closure` (indefinida) · `PATCH /…/{id}/annulment` · `POST /…/{id}/extensions` (prórroga) · `GET /personnel-files/{publicId}/incapacity-balance` | Crear: Manage OR self (D-18) · confirmar/cerrar/anular/prórroga: Manage (+anti-self en confirmar) · leer: View OR self |
| `PersonnelFileIncapacityDocumentsController` | `GET/POST /…/incapacities/{id}/documents` · `DELETE /…/documents/{docId}` · `GET /…/documents/{docId}/read-url` | mismo corte que medical-claims |
| `PersonnelFileLactationController` | `GET/POST /personnel-files/{publicId}/lactation-periods` · `PUT /…/{id}` (incluye reemplazo de horarios) · `PATCH /…/{id}/annulment` | Manage; leer View OR self |
| `PersonnelFileVacationPeriodsController` | `GET/POST /personnel-files/{publicId}/vacation-periods` · `PUT /…/{id}` (solo sin consumos) · `DELETE` (soft, sin consumos, `parentConcurrencyToken`) · `GET /personnel-files/{publicId}/vacation-fund` (detalle + provisión) | Manage; fondo legible View OR self |
| `PersonnelFileVacationRequestsController` | `GET/POST /personnel-files/{publicId}/vacation-requests` · `PATCH /…/{id}/decision` (aprobar con asignaciones editables / rechazar) · `PATCH /…/{id}/cancellation` (self mientras `SOLICITADA`) · `POST /…/{id}/returns` (devolución total/parcial) | Crear/anular: Manage OR self · decidir/devolver: Manage + anti-self |
| `VacationPeriodsAdministrationController` (empresa) | `POST /companies/{companyId}/vacation-periods/generate` (masivo idempotente, filtros) | Manage |
| `VacationPlansController` (empresa) | `GET/POST /companies/{companyId}/vacation-plans` · `PUT /…/{id}` (líneas) · `PATCH /…/{id}/annulment` — responses con `warnings[]` por línea | Manage; leer View |
| `IncapacitiesReportingController` | `POST /companies/{companyId}/incapacities/query` (bandeja + `StatusCounts`) · `GET /…/incapacities/export` (xlsx/csv/json, rate-limited; **excluye `EN_REVISION` del insumo planilla vía filtro default `status=REGISTRADA` documentado**) | View |
| `VacationsReportingController` | `POST /companies/{companyId}/vacation-requests/query` · `GET /…/vacation-requests/export` · `GET /…/vacations/calendar?year=` (goces `APROBADA/DEVUELTA_PARCIAL` + plan vigente) · `GET /…/vacation-fund/export` (provisión para Finanzas, D-25) | View |
| Maestros (§3.1) | CRUD company-scoped + `POST /companies/{companyId}/leave-configuration/load-template?year=` | `[ResourceActions]` familia governed |

Contratos: If-Match en todo write (`[FromIfMatch]`), DELETE→`parentConcurrencyToken`, enums/códigos como strings, `xxxPublicId`. **Sin `[ResourceActions]` en los controllers de expediente** (mismo corte que retiro/liquidación).

### 3.8 Adjuntos (D-22/RF-011)

`FilePurpose.IncapacityDocument` + regla `Storage:Purposes:IncapacityDocument` en **appsettings base** (tamaño máx + content-types: pdf/jpg/png) + contenedor pre-aprovisionado (checklist §9 — gotcha conocido: config faltante → 422). Flujo 3 patas espejo de medical-claims. **Constancia obligatoria** (`IncapacityRequiresDocument`, default sí): el `POST` de incapacidad exige `filePublicId` inicial (validado con purpose) cuando la preferencia está activa — el documento se asocia en la misma transacción del alta.

### 3.9 Exportaciones (RF-013/RF-025/D-25)

Filas con propiedades en español vía `ReportExportFileWriter` (sin librerías nuevas):
- `IncapacidadExportRow`: empleado/código, plaza, riesgo, tipo, clínica, estado, origen, fechas, naturales/computables, subsidiados/descuento/patrono (días **y montos**), % por tramo (aplanado del `TrancheDetailJson`), base mensual/diaria, tipo de planilla + periodo (etiqueta + fechas de corte), `utilizaFondo`.
- `GoceVacacionesExportRow`: solicitudes `APROBADA/DEVUELTA_PARCIAL/DEVUELTA` con fechas, días, devueltos, periodos de origen.
- `FondoProvisionExportRow` (Finanzas, D-25): empleado, periodo, otorgados ley/beneficio, gozados, pendientes, salario diario, **valor provisión = pendientes × diario × 1.30**.
Boleta PDF: **no en F1** (el pipeline `DocumentModel` queda disponible para F2).

### 3.10 Saldos en el perfil (RF-018/D-20) — toca `PersonnelFileEmployeeRepository`

Sustituir los `null` de `:2213-2227` por subconsultas en la proyección (cuidado con el gotcha de **member-init en proyección EF**):
- `VacationDaysAvailable` = Σ pendientes de periodos activos con `GeneratesEnjoymentDays` (otorgados − aprobado-no-devuelto).
- `DisabilityDaysAvailable` = `(tope + beneficio) − Σ EmployerDays` de incapacidades `REGISTRADA` del año en curso (null si la preferencia no está configurada… default 9 aplica ⇒ solo null si el módulo aún no tiene datos — documentar en guía FE).
Además `GET /personnel-files/{publicId}/incapacity-balance` devuelve el detalle (acumulado, tope ley/política, beneficio, restante) — misma fórmula, un solo módulo de reglas (`IncapacityBalanceRules`) para que perfil y consulta **cuadren por construcción**.

### 3.11 Integración con liquidación (RF-019) — toca `SettlementRepository`/`SettlementCalculation`

`ISettlementRepository.GetPendingVacationDaysAsync(personnelFileId)` (suma de pendientes con goce). En la sugerencia de `VACACION_PROPORCIONAL`: si hay valor → `units = pendientes` (en lugar de `DaysSinceAnniversary`); el override del liquidador (`UnitsOrFactorUsed`+`IsOverridden`) se preserva intacto; sin fondo → comportamiento actual (retrocompatible, test dedicado en ambos sentidos).

### 3.12 Localización y auditoría

- ~30 códigos nuevos (mapa §5 + warnings) EN+ES+es-SV con paridad (`BackendMessageLocalizationTests`); `validation.message.*` por cada `WithMessage` de los ~12 validadores nuevos.
- Auditoría: doble-`SaveChanges` en cada write; `ReportExported` en exports (ya lo hace `ReportExportDeliveryService`); asientos de personal con `ACTION_STATUS` `APLICADA=-9495` y vigencias del registro fuente.

---

## 4. Migraciones y seeds

| # | Migración (PR) | Contenido |
|---|---|---|
| M1 (PR-1) | `AddLeaveConfigurationMasters` | `CreateTable` × 7 (`medical_clinics`, `incapacity_risks`, `incapacity_risk_parameters`, `incapacity_types`, `company_holidays`, `payroll_period_definitions`, + `vacation_plans`/`vacation_plan_lines` pueden diferirse a M3) + índices únicos normalizados por tenant + seed TPH `clinic-sectors` (**-9860…-9862**) |
| M2 (PR-2) | `AddLeaveStatusCatalogsPermissionsAndPreferences` | Seeds TPH `incapacity-statuses` (**-9850…-9852**) + `vacation-request-statuses` (**-9853…-9858**) + `InsertData` 5 ActionTypes (**-9485…-9489**, SV) + `AddColumn` × 10 en `company_preferences` + `AddColumn personnel_file_employment_assignments.rest_day_of_week smallint NULL` |
| M3 (PR-3) | `AddPersonnelFileIncapacitiesAndLactation` | Tablas `personnel_file_incapacities` (+ FK autoreferente cadena, índices `(tenant, personnel_file_id, status_code)` y `(tenant, start_date)`), `personnel_file_incapacity_documents`, `personnel_file_lactation_periods` + `lactation_schedules` |
| M4 (PR-7) | `AddVacationFundAndRequests` | Tablas `personnel_file_vacation_periods` (**filtered-unique** `(tenant, personnel_file_id, period_year) WHERE is_active`), `personnel_file_vacation_requests`, `vacation_request_allocations`, `vacation_returns` (+ `vacation_plans` si se difirió) |

DevSeed: aplicar `LeaveTemplateSeeder` al tenant demo + asuetos 2026 + 2 `PayrollPeriodDefinition` de ejemplo + fondo demo (periodo 2026 con 15+0) + 1 incapacidad `REGISTRADA` con desglose + 1 solicitud `APROBADA`. Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add … -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · guardrail `MigrationSeedingIntegrationTests`.

---

## 5. Mapa de errores (resumen)

| Código | HTTP | Dónde |
|---|---|---|
| `INCAPACITY_RISK_INVALID` / `INCAPACITY_TYPE_INVALID` / `INCAPACITY_CLINIC_INVALID` / `INCAPACITY_PAYROLL_PERIOD_INVALID` | 422 | Referencia inexistente/inactiva (clínica/periodo solo si viajan) |
| `INCAPACITY_OVERLAP` | 422 | Solape con incapacidad activa del empleado (RN-14) |
| `INCAPACITY_END_DATE_REQUIRED` | 422 | `endDate` nula con riesgo sin `AllowsIndefinite` (D-11) |
| `INCAPACITY_DOCUMENT_REQUIRED` | 422 | Alta sin constancia con preferencia activa (D-22) |
| `INCAPACITY_BASE_SALARY_MISSING` | 422 | Sin `SALARIO_BASE` resoluble en plaza referida/principal (D-21) |
| `INCAPACITY_EXTENSION_NOT_ALLOWED` / `…_NOT_CONTIGUOUS` / `…_SOURCE_INVALID` | 422 | Prórroga: riesgo sin flag / inicio ≠ fin+1 / origen anulado, indefinido o `EN_REVISION` (RN-04) |
| `INCAPACITY_CHAIN_LOCKED` | 422 | Editar fechas con prórrogas posteriores vigentes (aclaración №5) |
| `INCAPACITY_STATE_RULE_VIOLATION` | 422/409 | Confirmar no-`EN_REVISION`, cerrar no-indefinida, anular `ANULADA`, editar anulada |
| `INCAPACITY_CONFIRM_SELF_FORBIDDEN` | **403** | El empleado confirma su propia incapacidad (D-18) |
| `INCAPACITY_OVERRIDE_NOTE_REQUIRED` | 422 | Ajuste de computables sin nota (RN-07) |
| `LACTATION_SCHEDULE_OUT_OF_RANGE` / `LACTATION_SCHEDULE_OVERLAP` | 422 | Horario fuera del periodo / solapado (RF-015) |
| `VACATION_PERIOD_DUPLICATE` | 422 | `(expediente, año)` ya existe (RN-19) |
| `VACATION_PERIOD_HAS_CONSUMPTION` | 422 | Editar días / borrar periodo con asignaciones (RF-016) |
| `VACATION_ELIGIBILITY_NOT_MET` | 422 (individual) / fila de resumen (masivo) | < 1 año de servicio al inicio del periodo (Art. 177) |
| `VACATION_FUND_INSUFFICIENT` | 422 | Días solicitados > disponibles (al crear Y al aprobar, RN-10) |
| `VACATION_REQUEST_OVERLAP` / `VACATION_INCAPACITY_OVERLAP` | 422 | Solape con solicitud viva / incapacidad activa (RN-15/16) |
| `VACATION_START_ON_HOLIDAY_FORBIDDEN` / `VACATION_START_ON_REST_DAY_FORBIDDEN` / `VACATION_END_ON_HOLIDAY_FORBIDDEN` | 422 | Defaults legales Art. 178 / flag (RN-27; en plan anual = warning) |
| `VACATION_ALLOCATION_MISMATCH` | 422 | Σ asignaciones ≠ días solicitados al aprobar (D-13) |
| `VACATION_DECISION_SELF_FORBIDDEN` | **403** | Decidir/devolver solicitud del propio expediente (RN-17) |
| `VACATION_STATE_RULE_VIOLATION` | 422/409 | Decidir no-`SOLICITADA`, devolver no-aprobada, anular decidida |
| `VACATION_RETURN_EXCEEDS_CONSUMED` | 422 | Devolución > consumido restante (RN-11) |
| `LEAVE_MASTER_IN_USE` | 422 | Baja de riesgo/tipo/periodo/clínica referenciado por registro activo |
| `PAYROLL_PERIOD_OVERLAP` / `HOLIDAY_DUPLICATE` / `RISK_PARAMETERS_INVALID` | 422 | Maestros: solape de rangos / fecha duplicada / tramos no contiguos-solapados |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` (reuso del código existente del perfil retirado) | 422 | Todo alta sobre perfil `RETIRADO` (RN-18) |
| **Warnings**: `INCAPACITY_WARNING_CAP_EXHAUSTED` · `VACATION_PLAN_WARNING_INSUFFICIENT_FUND` · `VACATION_PLAN_WARNING_DATE_RULE` | — | Response de cálculo / plan anual (aclaración №9) |

Reusados: 400/409 de If-Match, 403 de gates, `PERSONNEL_FILE_EXPORT_FORMAT_INVALID`, `REPORT_EXPORT_TOO_LARGE` (413), errores de purpose de archivos.

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- **`IncapacityCalculationRulesTests` — la suite crítica** (los 8 golden del **Anexo A.4 ya confirmados** se codifican como `[Theory]` bloqueantes desde el inicio — no hay espera de firma):
  - A.4-1: enfermedad común 5 días mié→dom, descanso domingo, sin séptimo → computables 4; 1–3 EMPRESA 75 %, día 4 ISSS 75 %; salario $600 → patrono $45.00, ISSS $15.00.
  - A.4-2: 2 días → todo patrono; consume 2 del tope (quedan 7 de 9).
  - A.4-3: cadena 3+4 → prórroga arranca en día 4 (tramo ISSS), no reinicia.
  - A.4-4: tope agotado → días EMPRESA reclasificados a SIN_PAGO + warning `CAP_EXHAUSTED`.
  - A.4-5: accidente de trabajo 10 días → 100 % ISSS desde día 1; no consume tope.
  - A.4-6: maternidad 112 días → 100 % ISSS, sin descuento.
  - A.4-7: asueto dentro del rango con riesgo sin asueto → día excluido de conteo y monto.
  - Bordes: rango que solo contiene días excluidos (computables 0), `restDay` ≠ domingo, sábado+domingo consecutivos, tramo con `DayTo` exacto en el corte, riesgo sin subsidio, salario con céntimos conflictivos (propiedad: totales = Σ tramos redondeados), indefinida (sin desglose hasta cierre), cierre con fecha = inicio.
- **`VacationRulesTests`**: A.4-8 (inicio en asueto → violación; 15+5 → disponible 20; devolución parcial 4 exacta al origen), FIFO multi-periodo, LIFO de devolución, bounds por aniversario (año bisiesto, aniversario 29-feb) vs calendario, elegibilidad < 1 año, disponibilidad con solicitudes vivas.
- **`LactationRulesTests`**: contención, solapes, permisos/minutos ≥ 1.
- Dominio: guards de todas las transiciones inválidas (confirm/close/annul; approve/reject/cancel/return; `ReplaceParameters` con tramos rotos; `ReplaceSchedules`); override sin nota lanza; período con consumo no editable.
- `IncapacityBalanceRulesTests`: perfil y consulta cuadran por construcción.
- Validadores; governance (4 policies); **paridad de localización** (~30 códigos).

**Integración (`tests/CLARIHR.Api.IntegrationTests/` — `ApiIntegrationTests.Leave.cs` + `ApiIntegrationTests.Vacations.cs`, espejo de `ApiIntegrationTests.Settlements.cs`):**
- **Round-trip incapacidad (RRHH)**: cargar plantilla → crear con constancia → verificar desglose días+montos + asiento `INCAPACIDAD` → prórroga (tramo continúa) → export con montos → anular (revierte tope).
- **Round-trip autoservicio**: login vinculado crea la suya (`EN_REVISION`, no aparece en export default, no consume tope) → confirma RRHH (`REGISTRADA`, consume) → intento de confirmar la propia → 403; sin constancia → 422; sobre otro expediente → 403.
- **Tope patronal**: 3 eventos (3+3+3) agotan 9; 4.º evento → SIN_PAGO + warning; `incapacity-balance` y `disabilityDaysAvailable` del perfil cuadran en cada paso.
- **Lactancia**: alta con horarios → asiento `LACTANCIA`; horario solapado → 422.
- **Fondo**: generación masiva (idempotencia: 2.ª corrida = 0 creados; elegibilidad reporta por fila) → `vacation-fund` con provisión (pendientes × diario × 1.30) → `vacationDaysAvailable` poblado; duplicado manual → 422.
- **Round-trip solicitud**: autogestión crea (valida Art. 178: inicio en asueto → 422; inicio en `restDayOfWeek` configurado miércoles → 422) → RRHH aprueba (FIFO 2 periodos; re-verifica saldo) + asiento `GOCE_VACACIONES` → devolución parcial (LIFO exacto, `DEVUELTA_PARCIAL`) → devolución del resto (`DEVUELTA`) → saldo idéntico al inicial; decidir la propia → 403; fondo insuficiente al aprobar (carrera con otra aprobación) → 422.
- **Plan anual**: líneas con warnings (disponibilidad/asueto) sin bloquear; calendario del año devuelve plan + goces.
- **Maestros**: CRUD 5 maestros (AllowedActions presentes — guardrail), load-template idempotente, baja en uso → 422, tramos de riesgo rotos → 422, solape de periodos de planilla → 422.
- **Perfil/contrato**: `PUT` de assignment acepta `restDayOfWeek`; perfil retirado bloqueado en todas las altas.
- **Liquidación**: retiro + fondo con pendientes → liquidación sugiere pendientes; sin fondo → `DaysSinceAnniversary` (retrocompatible); override del liquidador sobrevive.
- Guardrails existentes verdes: `MigrationSeedingIntegrationTests`, `GeneralCatalogKeyMapGuardrailsTests`, `OpenApiContractGuardrailsIntegrationTests`, `AuthorizationPolicyConventionGovernanceTests`, `AllowedActionsCoverageIntegrationTests`.

---

## 7. Orden de implementación (PRs sugeridos)

**Ola 1 — configuración + incapacidades (el insumo urgente de planilla):**

1. **PR-1 — Maestros por empresa + plantilla (M1)** (§3.1): 6 entidades + configs + controllers governed + `LeaveTemplateSeeder` (A.2/A.3 confirmados) + hook provisioning + `load-template` + TPH `clinic-sectors` + resx + guardrails.
2. **PR-2 — Catálogos de estado + ActionTypes + permisos + preferencias + `restDayOfWeek` (M2)** (§3.2/§3.3): 2 TPH + 5 acciones + 4 codes/policies/gates + columnas de preferencias con PATCH admin + campo en assignment + openapi regenerado temprano (contrato de plaza).
3. **PR-3 — Dominio incapacidades + lactancia + EF (M3)** (§3.4): entidades + guards + batería unitaria de dominio.
4. **PR-4 — Motor de cálculo + data provider** (§3.5): `IncapacityCalculationRules` + `LeaveCalculationDataProvider` + **suite A.4 completa en verde** (gate de la ola).
5. **PR-5 — Incapacidades end-to-end** (§3.6/§3.7/§3.8): CRUD + confirmación/cierre/anulación + prórrogas + adjuntos (purpose + config) + saldos + asientos + autoservicio + integración completa.
6. **PR-6 — Lactancia end-to-end + bandeja/export de incapacidades** (§3.9): registro + horarios + asiento + reporting controller con filtro default `REGISTRADA`.

**Ola 2 — vacaciones:**

7. **PR-7 — Fondo (M4)** (§3.4/§3.6): periodos CRUD + generación masiva idempotente + detalle/provisión + **saldos del perfil** (§3.10) + integración.
8. **PR-8 — Solicitudes**: crear (autogestión/RRHH, validaciones Art. 178) + decisión (FIFO editable, anti-self) + devoluciones total/parcial (LIFO) + asientos + integración.
9. **PR-9 — Plan anual + calendario + bandeja/export de solicitudes + export de provisión** (§3.7/§3.9).
10. **PR-10 — Integración liquidación + E2E + guía FE** (§3.11): sugerencia de pendientes + suite E2E integral + verificación (suites verdes, drift vacío, seeds en BD real) + `openapi.yaml` final + `docs/technical/guia-integracion-frontend-vacaciones-incapacidades.md`.

> **Cada PR lleva sus claves resx y sus tests.** El gate entre olas es la suite A.4 en verde (PR-4) — a diferencia de liquidación, los números ya están confirmados por el negocio, así que no hay hito externo bloqueante.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Exactitud del motor de conteo (el riesgo real).** Día a día + cadena + tope es fácil de romper en bordes. Mitigación: 100 % puro, A.4 bloqueante, `TrancheDetailJson` hace auditable cada número, snapshot inmutable (RN-06/RN-25).
- **R-T2 — Carrera del tope patronal.** Dos incapacidades simultáneas del mismo año pueden sobre-consumir el tope. Mitigación: recomputar el consumo del año **dentro de la transacción** de escritura (aclaración №4) + test de integración de la carrera (aprobar 2.ª tras agotar).
- **R-T3 — Convención salario/30 vs planilla real.** Documentada en exportaciones y guía FE; los montos son referenciales (D-21); reconciliación llegará con el módulo de planilla.
- **R-T4 — Proyección del perfil.** Los saldos añaden 2 subconsultas al GET de perfil; medir y, si degrada, mover a lookup dedicado manteniendo el contrato. Gotcha: proyección EF con member-init (memoria del repo).
- **R-T5 — Familia de maestros nueva.** Primer paquete tenant-scoped con plantilla + load-template; el seeder debe ser idempotente por código (no por Id) y **nunca** pisar ediciones. Guardrail: 2.ª corrida = 0 cambios.
- **R-T6 — Coherencia `EN_REVISION`.** El estado debe excluirse en 4 lugares (export default, tope, saldos, sugerencia a liquidación… esta última no aplica a incapacidades pero sí el patrón): centralizar el predicado en un solo lugar (`IncapacityStatuses.CountsAsRegistered`) y testear cada superficie.
- **R-T7 — Índices filtrados** (`vacation_periods` por año, unicidad de solicitud viva si se agrega): nombres ≤ 63 chars (convención Postgres, patrón retiro).
- **R-T8 — `restDayOfWeek` en contrato compartido de assignments.** Aditivo y nullable ⇒ sin breaking; openapi se regenera en PR-2 (temprano, para FE). Gotcha `[Required]` param-target en records posicionales.
- **R-T9 — Devoluciones y consistencia del fondo.** Invariantes (Σ asignaciones = solicitados; Σ devoluciones ≤ consumido; reversa exacta al periodo de origen) viven como guards de dominio + property-tests; nunca en el handler.
- **R-T10 — Generación masiva.** Corridas grandes por lotes con resumen por fila (no transacción global); idempotencia permite re-correr tras fallo parcial.
- **R-T11 — `[ResourceActions]`/`ISupportsAllowedActions`** en los 5 maestros: cada DTO PUT/PATCH lo implementa (solo la integración lo detecta — memoria del repo).
- **R-T12 — `dotnet ef`** requiere `DOTNET_ROLL_FORWARD=Major`.

---

## 9. Checklist de implementación

- [ ] **Maestros:** 6 entidades `Domain/Leave/` + configs + 5 controllers governed (`ISupportsAllowedActions` en todos los DTOs) + `LeaveTemplateSeeder` idempotente (A.2/A.3) + hook provisioning + `load-template` + guards de uso (`LEAVE_MASTER_IN_USE`).
- [ ] **Catálogos/acciones:** 3 TPH (`-9850…-9852`, `-9853…-9858`, `-9860…-9862`) + key map + switch + 5 ActionTypes (`-9485…-9489`) + guardrails de biyección/seeding (verificar IDs libres al abrir PR-1/PR-2).
- [ ] **Permisos:** 4 codes + provisioning + 4 policies (fallback Admin) + governance + gates fail-closed (2 create-own-or-manage, lecturas self, manage-only) + anti-self compartido (2 códigos 403).
- [ ] **Preferencias:** 10 columnas anulables + `SetLeavePolicies` + PATCH admin + defaults legales documentados.
- [ ] **Plaza:** `rest_day_of_week` + contrato assignments + validador 0–6 + openapi temprano.
- [ ] **Dominio expediente:** `PersonnelFileIncapacity` (+documento, cadena, snapshot días/montos/`TrancheDetailJson`), `PersonnelFileLactationPeriod`(+horarios), `PersonnelFileVacationPeriod` (filtered-unique por año), `PersonnelFileVacationRequest` (+allocations+returns) — guards completos.
- [ ] **Motor:** `IncapacityCalculationRules` (7 pasos §3.5) + `VacationRules` + `LactationRules` + `IncapacityBalanceRules` + redondeo único + warnings + suite **A.4 confirmada** en verde.
- [ ] **Data provider:** salario base plaza referida→principal, `restDayOfWeek`→default empresa, asuetos del rango, tope restante del año, offset de cadena.
- [ ] **Aplicación/API:** endpoints §3.7 con If-Match/`parentConcurrencyToken`/anti-self + asientos en transacción + `warnings[]` en plan.
- [ ] **Adjuntos:** `FilePurpose.IncapacityDocument` + `Storage:Purposes` en appsettings base + contenedor aprovisionado + obligatoriedad por preferencia.
- [ ] **Exportaciones:** 3 export-rows en español (incapacidades con montos y periodo de planilla; goces; provisión Finanzas) + filtro default `REGISTRADA` + rate limits.
- [ ] **Perfil:** saldos calculados en proyección + `incapacity-balance` (cuadre por construcción).
- [ ] **Liquidación:** `GetPendingVacationDaysAsync` + swap del default preservando override + tests de retrocompatibilidad.
- [ ] **Localización:** ~30 códigos EN/ES/es-SV + paridad + `validation.message.*`.
- [ ] **Pruebas:** unitarias (§6) + integración (`ApiIntegrationTests.Leave.cs` / `.Vacations.cs`) + guardrails existentes verdes + suite completa del repo en verde.
- [ ] **Cierre:** `openapi.yaml` regenerado sin drift · DevSeed actualizado · checklist de despliegue (migraciones M1–M4, `Storage:Purposes:IncapacityDocument`, contenedor, `load-template` en el tenant productivo + carga de periodos de planilla y asuetos del año por la empresa) · `guia-integracion-frontend-vacaciones-incapacidades.md`.
