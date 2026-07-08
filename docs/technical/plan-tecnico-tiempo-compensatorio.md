# Plan Técnico de Implementación — Gestión de tiempo compensatorio (Acreditaciones · Ausencias · Fondo/estado de cuenta · Catálogo de tipos · Integración con liquidación)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-tiempo-compensatorio-empleado.md`](../business/analisis-tiempo-compensatorio-empleado.md) (**RATIFICADO 2026-07-05: D-01…D-21 + P-01…P-14**; única pendiente **P-15** — tarifa de liquidación, no bloqueante: default parametrizable 1.00) |
| **Módulos** | `PersonnelFiles` (CompensatoryTime — net-new) · maestro por empresa `CompensatoryTimeType` (net-new, **sin semilla**) · `GeneralCatalogs` (2 TPH + 2 ActionTypes) · `Preferences` (+4 columnas) · Files (nuevo purpose + patrón **nuevo** de adjunto obligatorio al crear) · **Settlements (integración D-19: línea automática `HORAS_EXTRAS_PENDIENTES`)** · `EmployeeProfiles` (saldo aditivo) · Provisioning (RBAC) · Reporting/Export · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-07-05 |
| **País de referencia** | El Salvador (SV, `CountryCatalogItemId = -7068L`) |
| **Secuenciación** | Desarrollo **después de REQ-001** (asuetos + `restDayOfWeek` + periodos de planilla + solapes cruzados). La integración con liquidación (§3.11) **no** depende de REQ-001 (Settlements mergeado, PR #56) |
| **Endurecimientos de la ratificación** | Fondo **por empleado en HORAS** sin caducidad (ledger derivado, D-02/D-03/D-04) · saldo **nunca negativo** con re-verificación transaccional (D-08/D-09) · **adjunto de autorización de jefatura PDF obligatorio** al acreditar (D-20) · maestro de tipos **inicia vacío** (D-05) · **el saldo al retiro se paga**: línea automática en liquidación (D-19) · costura `overtimeRecordPublicId` (D-21) · empleado F1 solo lectura (D-01) |

---

## 0. Aclaraciones pre-desarrollo (recomendación del desarrollador senior; ratificación ya cerrada)

1. **Un solo lugar para la aritmética del saldo.** `CompensatoryTimeRules` (módulo puro) define la agregación (Σ acreditadas − Σ debitadas de registros `REGISTRADA` activos) y toda derivada (saldo corrido, sugerencia, valoración). Estado de cuenta, perfil, validaciones de escritura y liquidación consumen la misma regla — cuadre por construcción (patrón `IncapacityBalanceRules` del plan de REQ-001).
2. **Redondeo y convención monetaria (D-19).** `decimal` en toda la cadena; única regla half-up 2 decimales — en el lado liquidación se reutiliza `SettlementCalculationRules.Round2` (`SettlementCalculation.Rules.cs:179`, `AwayFromZero`). Horas acreditadas = `Round(horas × factor, 2)`. Valor hora = `Round2(derived.DailySalary / standardDailyHours)` (el diario ya sale de `MonthlyBaseSalary / MonthDivisorDays` en el motor — **no** hardcodear 30). Monto = `Round2(horas × valorHora × rateFactor)`. Prohibido redondear en handlers.
3. **Anti-carrera del saldo (RN-03/RN-06).** Toda escritura que **reduzca** el saldo (crear/editar ausencia; editar/anular acreditación) toma `SELECT pg_advisory_xact_lock({tenantKey}, {personnelFileKey})` dentro de la transacción **antes** de re-verificar el saldo. Precedentes exactos a imitar: `PositionSlotRepository.cs:192-202` y `CompanyRepository.cs:24-34` (+ abstracción no-op para fakes de test, patrón `ICompanyRepository.cs:25-27`). El módulo de liquidación NO usa locks (solo optimistic concurrency) — aquí sí se necesita porque el invariante es cross-row. Test de integración de la carrera obligatorio (dos débitos concurrentes → uno 422).
4. **Adjunto obligatorio = patrón NUEVO (verificado: sin precedente).** Ningún módulo exige hoy `filePublicId` en el POST del registro padre (los documentos son sub-recurso post-creación). Aquí el comando de alta lleva `authorizationFilePublicId`; con la preferencia activa (default sí) el handler lo exige, aplica el **gate de purpose** (espejo exacto de `MedicalClaimDocuments.Handlers.cs:172-191`: existe → `Active` → mismo tenant → `Purpose == CompensatoryTimeDocument`) y crea el documento hijo **en la misma transacción** del alta. La obligatoriedad vive en el handler (lee preferencia); el **dominio es agnóstico** al documento (DevSeed y fixtures pueden crear créditos sin adjunto).
5. **Costura overtime (D-21).** `OvertimeRecordPublicId` (`Guid?`) columna sin FK, sin validación, comentada como *"seam for the future overtime module"*; viaja en request/response. Nada más en F1.
6. **Solapes cruzados (RN-05).** Se implementan contra las entidades de REQ-001 (incapacidades activas; solicitudes/goces vivos de vacaciones) porque el desarrollo va después. Si REQ-001 se pospusiera, esas dos queries se omiten (modo degradado consciente D-18) — la estructura del validador las aísla en un solo método del repositorio.
7. **Maestro sin semilla (D-05).** Cero seeder/plantilla/`load-template` en producción. **DevSeed sí** crea 3 tipos demo para el tenant de desarrollo (FE necesita el combo poblado).
8. **Integración con liquidación — decisiones finas (D-19):**
   - **El catálogo NO cambia**: `HORAS_EXTRAS_PENDIENTES` (seed `-9837L`, `GlobalCatalogSeedData.cs:969`) conserva `IsSystemCalculated=false` para que la **vía manual actual siga existiendo** (tenants sin datos del módulo). La línea automática nace con `isSystemCalculated: true` **a nivel de línea** (flag del `LineSpec`, no del catálogo).
   - **Solo en la liquidación de la plaza principal**: el fondo es por empleado y la liquidación es por plaza — si el empleado retira con 2 plazas habría doble sugerencia. El data provider resuelve el saldo **únicamente** cuando `assignedPositionPublicId` es la plaza principal (criterio de plaza principal ya usado por liquidación: `IsPrimary` entre activas; fallback `StartDate` más antiguo).
   - **Guard anti-duplicado**: `AddSettlementManualLineCommandHandler` (`Settlements.Handlers.cs:590-640`) rechaza agregar manualmente un concepto que ya existe como línea de sistema en esa liquidación → 422 `SETTLEMENT_CONCEPT_ALREADY_SUGGESTED` (evita doble pago auto + manual).
   - **Regenerate descarta la línea y la re-sugiere fresca** (mecánica existente: `ClearLines()` primero, `Settlements.Handlers.cs:61-63`); el recálculo normal preserva `UnitsOverridden`/`OverrideAmount` por `PublicId` (`Settlements.Handlers.cs:91-110`) — mismo trade-off documentado que `VACACION_PROPORCIONAL`.
9. **Fondo congelado tras el retiro.** El bloqueo de perfil `RETIRADO` (RN-10) aplica a **todas** las escrituras del módulo — también ediciones y anulaciones. Así el saldo no puede cambiar después de generada la liquidación (el snapshot de la línea queda consistente). La **reversión de retiro** (ventana 30 días) reabre el perfil y descongela el fondo; el gancho existente de reversión ya anula borradores de liquidación.
10. **Nomenclatura real de Settlements (corrección al plan de REQ-001).** Los flags reales de la línea son `UnitsOrDays` + `UnitsOverridden` y `OverrideAmount` + `OverrideReason` (`FinalAmount = OverrideAmount ?? CalculatedAmount`); **no existen** `UnitsOrFactorUsed` ni `IsOverridden` (nombres que usa el plan de vacaciones §3.11 — corregir allí al implementar RF-019 de REQ-001).
11. **Fechas y reloj.** Comparaciones vía `IDateTimeProvider` y parámetro `asOf` en reglas puras (fecha trabajada ≤ hoy, RN-15); tests deterministas.
12. **Seeds e infraestructura.** IDs del bloque **≤ -9865** (verificar contra `GlobalCatalogSeedData` al abrir PR-1; piso actual -9846; REQ-001 reserva -9850…-9862 y -9485…-9489; REQ-003 reserva ≤ -9875 **respetando** nuestro -9865…-9871; trampa: -9490…-9496 = `ACTION_STATUS_CATALOG`). `dotnet ef` requiere `DOTNET_ROLL_FORWARD=Major`. Migraciones en `src/CLARIHR.Infrastructure/Persistence/Migrations/` (última actual: `20260705010644_AddSettlementLineUnitsOverridden`).

---

## 1. Objetivo y enfoque

Construir el módulo de **tiempo compensatorio**: maestro por empresa de tipos (operación + factor, sin semilla), **acreditaciones declarativas** (horas × factor snapshot, detalle + autorizado-por + **documento de autorización PDF obligatorio**, costura a horas extras), **ausencias** con verificación transaccional de fondo y sugerencia de horas (descanso/asuetos de REQ-001), **estado de cuenta** con saldo corrido (RRHH + autogestión lectura), saldo aditivo en el perfil, asientos automáticos, bandeja + exportaciones (movimientos y saldos), y la **integración con liquidación**: el saldo pendiente entra automáticamente al cálculo como línea `HORAS_EXTRAS_PENDIENTES` editable.

**Insight central del análisis de código.** El módulo es greenfield total, pero el 90 % es composición de recetas existentes verificadas (maestro governed, TPH, permisos, gates, adjuntos, bandeja/export, asientos, preferencias). Las **tres piezas sin plantilla directa** (foco de riesgo, §8):

1. **El invariante de saldo cross-row con carreras** — primer módulo con un fondo derivado que debe re-verificarse bajo lock (`pg_advisory_xact_lock`, precedente PositionSlots/Companies pero primera vez sobre expediente).
2. **El adjunto obligatorio en el POST del padre** — patrón nuevo (hoy los documentos son siempre sub-recurso post-creación); se construye sobre el gate de purpose de medical-claims.
3. **La primera línea automática nueva del motor de liquidación post-merge** — `BuildSuggestedSpecs` + `case` nuevo en `ComputeIncomeLine` + campo nuevo en `SettlementCalculationInput`/`SettlementCalculationContext`, con retrocompatibilidad estricta (sin datos del módulo → motor intacto).

---

## 2. Línea base verificada en el código (qué se reutiliza / qué se toca)

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Línea de liquidación | `PersonnelFileSettlementLine` (`PersonnelFileSettlement.cs:578-772`): `ConceptCode/ConceptNameSnapshot`, `Description` (obligatoria en manuales, :613-614), `IsSystemCalculated` (:616), `CalculationBase` (:619), `UnitsOrDays` (:622, numeric 12,4), `UnitsOverridden` (:628), `OverrideAmount/OverrideReason` (:638-640), `FinalAmount = Override ?? Calculated` (:643), `IsIncluded` (:645), `CalculationDetail` varchar(500) (:653); guards `ApplyComputation` (:678), `SetOverride` (:703), `SetUnitsOrDays` (:752) | La línea automática usa la mecánica existente sin columnas nuevas: `UnitsOrDays` = horas, `CalculationBase` = valor hora, `CalculationDetail` = traza |
| 2 | Concepto `HORAS_EXTRAS_PENDIENTES` | Const `SettlementConceptCodes.HorasExtrasPendientes` (`SettlementCalculation.Rules.cs:657`); seed `-9837L` Ingreso, afecta ISSS/AFP/Renta, exención `Ninguna`, **`IsSystemCalculated=false`**, SortOrder 80 (`GlobalCatalogSeedData.cs:969`); hoy **solo manual** (no aparece en `BuildSuggestedSpecs`); vía manual: `AddSettlementManualLineCommandHandler` (`Settlements.Handlers.cs:590-640`, validación de concepto :621-627) | Se convierte en línea de sistema **condicional** (spec-level), sin tocar el seed; la vía manual sigue viva + guard anti-duplicado |
| 3 | Motor de liquidación | `SettlementCalculationRules.Calculate` (`SettlementCalculation.Rules.cs:181-283`); specs automáticos en `BuildSuggestedSpecs` (:300-336, `LineSpec` :287-297); recálculo `BuildSpecsFromExisting` (:339-344); switch de ingresos `ComputeIncomeLine` (:380-439; `VACACION_PROPORCIONAL` :391-396); `Round2` (:179); input `SettlementCalculationInput` (:93-105) con `ExistingLines: SettlementLineState[]` (:80-90) | Puntos de inyección exactos §3.11: spec condicional + `case` nuevo + campo nuevo del input |
| 4 | Data provider de liquidación | `ISettlementRepository.GetCalculationContextAsync` (`ISettlementRepository.cs:94`; contexto record :42-53 — "resuelto en UN viaje", comentarios :38-41); impl `SettlementRepository.cs:25-205` (salario base :119-124; lectura de `CompanyPreference` :185-189); mapeo al input en `SettlementCalculationSupport.Recalculate` (`Settlements.Handlers.cs:66-87`); estados persistidos `BuildStates` (:34-47); regenerate limpia primero (:61-63); re-materialización por `PublicId` (:91-110) | El saldo y las preferencias viajan **dentro del contexto** (un viaje), no como fetch extra en los 4 call-sites de `Recalculate` |
| 5 | Ciclo de vida de liquidación | `SettlementsController.cs` (crear :120, escenario :207, editar línea :279, línea manual :320, regenerate :369, emitir :155, anular :182); estados `BORRADOR/EMITIDA/ANULADA` (`PersonnelFileSettlement.cs:11-16`), `EnsureEditable` (:506) | La línea automática entra en generación y recálculos de BORRADOR/escenario; tras `EMITIDA` todo es inmutable (snapshot) |
| 6 | Stack de adjuntos | `FilePurpose` (`Domain/Files/FileEnums.cs:12-23`, 9 valores); reglas `Storage:Purposes` keyed por `FilePurpose.ToString()` (`FileStorageOptions.cs:5-59`; `FilePurposeRuleProvider.cs:14-38`; `appsettings.json:45-120`, bloque MedicalClaim :88-95); flujo 3 patas `FilesController.cs:21/:53/:82`; gate de purpose `MedicalClaimDocuments.Handlers.cs:172-191`; entidad espejo `MedicalClaimDocument` (`PersonnelFileEmployee.cs:1323-1416`) | +`FilePurpose.CompensatoryTimeDocument` + bloque appsettings **base** (PDF) + contenedor + entidad documento espejo + **patrón nuevo**: gate aplicado en el CREATE del padre (aclaración №4) |
| 7 | Preferencias | `CompanyPreference` (`Domain/Preferences/CompanyPreference.cs`): 5 columnas actuales + mutadores dedicados (ej. `SetEconomicAidEligibility` :74-83); admin `CompanyPreferencesController` GET/PUT/PATCH (:39/:60/:94); lectura `ICompanyPreferenceRepository.GetByTenantIdAsync` (patrón `EconomicAidRequests.Handlers.cs:42,86-91`) | +4 columnas anulables + `SetCompensatoryTimePolicies(...)` + exposición en el PATCH admin |
| 8 | Gates de handler | `PersonnelFileEmployeeHandlerBases.cs`: manage-only `LoadForManageOffPayrollTransactionsAsync` (:836); lectura View-or-self `LoadCompletedEmployeeForMedicalClaimReadAsync` (:1044, rama `isSelf` :1071-1073); `IPersonnelFileAuthorizationService` con defaults fail-closed (:53/:62/:211) | +`LoadForManageCompensatoryTimeAsync` + `LoadCompletedEmployeeForCompensatoryTimeReadAsync`; **sin** gemelo create-own (F1 manage-only, D-01) |
| 9 | Receta TPH | Subclases en `GeneralCatalogItems.cs` (molde `EconomicAidTypeCatalogItem` :321-347); wire keys `GeneralCatalogKeyMap.cs:21-70` (+resolver :93); switch `CatalogCodeIsActiveAsync` (`PersonnelFileRepository.cs:1580`, ramas :1602-1642; nombres :1660); guardrail `GeneralCatalogKeyMapGuardrailsTests` (:26/:47/:68); `CreateGeneralCatalogSeed(seedPrefix, id, country, code, name, sortOrder)` (`GlobalCatalogSeedData.cs:1247`); FE: `GET api/v1/general-catalogs/{key}` (`GeneralCatalogsController.cs:23`) | 2 TPH nuevos: `compensatory-time-statuses`, `compensatory-time-operations` |
| 10 | Receta de permisos | Codes `PersonnelFilePolicies.cs` (:171/:178) + `PersonnelFileCommon.cs` PermissionCodes (:233/:240) + tupla `ProvisioningConstants.CompanyAdminPermissions` (:33, ej. :92) + policy en `Program.cs` (manage → `RequireAssertion` superset :468-474; view self-service → authn-only :476-481) + `Ensure…` fail-closed + governance (`AuthorizationPolicyConventionGovernanceTests` :197/:276/:301; `AuthorizationPolicyConventionGuardrailsIntegrationTests` :31/:75) | 2 codes: `ViewCompensatoryTime` (authn-only: lectura tiene rama self) / `ManageCompensatoryTime` (RequireAssertion con fallback Admin/ManageAdministration) |
| 11 | Perfil | `PersonnelFileEmployeeProfileResponse` (`EmployeeProfiles.cs:25-43`; balances :39-40); repo `Map` con nulls (`PersonnelFileEmployeeRepository.cs:2214-2231`, comentario :2212-2213); enriquecimiento en capa de aplicación (`EmployeeProfileResponseEnricher` para email/seniority) | +`CompensatoryTimeHoursAvailable` (decimal?) aditivo, calculado con la misma agregación del estado de cuenta (alinear mecánica con la que REQ-001 implemente en su §3.10) |
| 12 | Bandeja + export | `SettlementsBandeja.cs` (response con `StatusCounts` :33-38; export row en español :45-61; PageSize 1-100 :98); `SettlementsReportingController.cs` **sin** `[AuthorizationPolicySet]` (comentario :16-21) con `[EnableRateLimiting(Search)]` :29 / `(Export)` :70 y `SynchronousReadLimit` → `MaxRows` (:112); `ReportExportFileWriter.WriteAsync<TRow>` (:39, headers por reflexión); 413 en `ReportExportDeliveryService.cs:67-70` | Clonar para `compensatory-time` (query de movimientos + export de movimientos + export de saldos) |
| 13 | Asiento de personal en transacción | Patrón `ExecuteRetirementRequest.cs:178-192`: `PersonnelFilePersonnelAction.Create(type, "APLICADA", …, isSystemGenerated: true)` + `BindToPersonnelFile` + `SetTenantId` + `AddPersonnelActionAsync`, dentro de la misma transacción (:200-216); mismo patrón en `Settlements.Handlers.cs:995` | 2 ActionTypes nuevos; asiento en la transacción del alta |
| 14 | Locks | `pg_advisory_xact_lock` en `PositionSlotRepository.cs:192-202` y `CompanyRepository.cs:24-34`; abstracción no-op para fakes (`ICompanyRepository.cs:25-27`); **no existe** `FOR UPDATE`/`IsolationLevel` en el repo | Aclaración №3: lock por (tenant, expediente) en escrituras que reducen saldo |
| 15 | Localización / DevSeed / governed | Resx en `Infrastructure/Localization/` + `BackendMessageLocalizationTests` (:64/:86/:108/:132) + convención `validation.message.*`; `DevSeedService.cs` (`SeedPersonnelFileAsync` :485, patrón factory+aggregate :501-536); familia governed `ResourceActionsAttribute.cs` + `AllowedActionsResultFilter.cs` + `ISupportsAllowedActions` + `AllowedActionsCoverageIntegrationTests` (:23/:60) — gotcha memorado: **todo DTO PUT/PATCH del maestro implementa `ISupportsAllowedActions`** | Convenciones obligatorias en el maestro y los recursos nuevos |

---

## 3. Arquitectura de la solución

### 3.1 Maestro de tipos por empresa (D-05/RF-001) — `src/CLARIHR.Domain/Leave/CompensatoryTimeType.cs`

`CompensatoryTimeType : TenantEntity` → tabla `compensatory_time_types`:

| Campo | Regla |
|---|---|
| `Code` / `NormalizedCode` / `Name` | Código único por tenant (índice único filtrado por `is_active` sobre normalizado) |
| `OperationCode` | `ACREDITA` / `DEBITA` / `AMBAS` — validado contra el TPH `compensatory-time-operations` en handlers; constantes canónicas `CompensatoryTimeOperations` en dominio |
| `CreditFactor` | `decimal(5,2)` > 0, default 1.00; CHECK en BD |
| `SortOrder` / `IsActive` / `ConcurrencyToken` | Convenciones estándar |

- **Sin semilla, sin `load-template`** (D-05): el maestro inicia vacío; el Anexo A.2 del análisis es guía documental del administrador.
- **Controller** `CompensatoryTimeTypesController` (familia governed): rutas `companies/{companyId}/compensatory-time-types`, `[ResourceActions]` + `ISupportsAllowedActions` en **todos** los DTOs PUT/PATCH (gotcha #15), If-Match, baja lógica con guard de uso (tipo referenciado por crédito/ausencia activa → 422 `COMPENSATORY_TIME_TYPE_IN_USE`).
- Editar `CreditFactor` **no** recalcula históricos (RN-02: `FactorApplied` es snapshot en cada crédito).

### 3.2 Catálogos TPH + tipos de acción (D-14/D-15)

Receta estándar (subclase + const de categoría + `GeneralCatalogKeyMap` + switch `CatalogCodeIsActiveAsync` + seed + guardrail de biyección):

| Catálogo | Wire key | Códigos (ID seed, SV `-7068L`) |
|---|---|---|
| `CompensatoryTimeStatusCatalogItem` | `compensatory-time-statuses` | `REGISTRADA=-9865`, `ANULADA=-9866` |
| `CompensatoryTimeOperationCatalogItem` | `compensatory-time-operations` | `ACREDITA=-9867`, `DEBITA=-9868`, `AMBAS=-9869` |
| `ACTION_TYPE_CATALOG` (existente, +2) | `action-types` | `ACREDITACION_TIEMPO_COMPENSATORIO=-9870`, `GOCE_TIEMPO_COMPENSATORIO=-9871` |

Estados **híbridos** (D-15): constantes `CompensatoryTimeStatuses` (`Registrada`, `Anulada` + set `Vigentes`) en dominio; el catálogo aporta i18n/UI. F2 agrega estados de solicitud de forma aditiva.

### 3.3 Permisos, políticas y gates (D-13/D-01)

- **Codes**: `PersonnelFiles.ViewCompensatoryTime` / `PersonnelFiles.ManageCompensatoryTime` — receta completa (fila #10 de §2): consts en `PersonnelFilePolicies` + `PersonnelFilePermissionCodes`, tupla en `ProvisioningConstants.CompanyAdminPermissions`, policies en `Program.cs` (**View = authn-only** `.Combine(policy)` porque la lectura tiene rama self, patrón ViewMedicalClaims :476-481; **Manage = RequireAssertion** con fallback `Admin`/`ManageAdministration`), `EnsureCanViewCompensatoryTimeAsync`/`EnsureCanManageCompensatoryTimeAsync` con default fail-closed, governance tests verdes.
- **Gates** (en `PersonnelFileEmployeeHandlerBases.cs`):
  - `LoadForManageCompensatoryTimeAsync` — todas las escrituras (espejo :836; F1 sin rama self, D-01).
  - `LoadCompletedEmployeeForCompensatoryTimeReadAsync` — lecturas `ViewCompensatoryTime OR isSelf` (espejo :1044).
- F2 (referenciado, no implementado): `AuthorizeCompensatoryTime` con RequireAssertion que excluye Admin (patrón `AuthorizeRetirement`).

### 3.4 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileCompensatoryTime.cs`

**`PersonnelFileCompensatoryTimeCredit : TenantEntity`** (tabla `personnel_file_compensatory_time_credits`):
- FK expediente + `BindToPersonnelFile`; `CompensatoryTimeTypeId` (FK dura al maestro) + `TypeNameSnapshot` (patrón `TransactionTypeNameSnapshot` de off-payroll); `WorkDate` (DateOnly), `StartTime?`/`EndTime?` (informativas, coherentes si viajan ambas); `HoursWorked` (`decimal(5,2)` > 0), `FactorApplied` (snapshot del tipo), `HoursCredited` (`decimal(6,2)` = Round2(worked × factor)), `IsOverridden` + `OverrideNote` (ajuste manual de `HoursCredited`, nota obligatoria); `WorkDetail` (500, req.), `AuthorizedByText` (200, req.), `AuthorizerFilePublicId?`; `AssignedPositionPublicId?`; **`OvertimeRecordPublicId?`** (costura D-21, sin FK); `RegisteredByUserId`; `StatusCode`; anulación (`AnnulmentReason`, `AnnulledByUserId`, `AnnulledUtc`); `Notes?`; `IsActive`; `ConcurrencyToken` rotativo.
- Guards: `Create(...)` (horas > 0; rango horario coherente; detalle/autorizado-por no vacíos; estado inicial `REGISTRADA`), `ApplyCreditedHours(factor, hours, isOverridden, note)` (única vía de escritura del cálculo; nota obligatoria si override), `Update(...)` (solo `REGISTRADA`), `Annul(reason, byUserId, at)` (motivo obligatorio; solo `REGISTRADA`).
- **La validación fecha ≤ hoy y el invariante de saldo NO son guards de dominio** (dependen de reloj/agregación) — viven en `CompensatoryTimeRules` + handler bajo lock (aclaración №3).

**`PersonnelFileCompensatoryTimeCreditDocument : TenantEntity`** (tabla `personnel_file_compensatory_time_credit_documents`): espejo exacto de `MedicalClaimDocument` (`PersonnelFileEmployee.cs:1323`): `CreditId` FK, `FilePublicId` (≠ empty), snapshots `FileName/ContentType/SizeBytes`, `Observations?`, `IsActive`, token; `Create` + `BindToCredit`.

**`PersonnelFileCompensatoryTimeAbsence : TenantEntity`** (tabla `personnel_file_compensatory_time_absences`):
- FK expediente; `CompensatoryTimeTypeId` + `TypeNameSnapshot`; `StartDate`/`EndDate` (inicio ≤ fin; futuras permitidas); `HoursDebited` (`decimal(6,2)` > 0); `Reason` (500, req.); `PayrollPeriodPublicId?` (instancia del maestro de REQ-001, opcional — imputación, no contención); `RegisteredByUserId`; `StatusCode`; anulación; `Notes?`; `IsActive`; token.
- Guards: `Create(...)`, `Update(...)` (solo `REGISTRADA`), `Annul(reason, byUserId, at)`.

CHECK constraints en BD (precedente equipo-acceso): `hours_worked > 0`, `hours_credited > 0`, `factor_applied > 0`, `hours_debited > 0`, `start_date <= end_date`.

Índices: créditos `(tenant_id, personnel_file_id, status_code)` + `(tenant_id, work_date)`; ausencias `(tenant_id, personnel_file_id, status_code)` + `(tenant_id, personnel_file_id, start_date, end_date)` (solapes). Nombres ≤ 63 chars.

**`CompanyPreference`** (+4 columnas anulables + `SetCompensatoryTimePolicies(...)`): `CompensatoryTimeStandardDailyHours` (`decimal(4,2)`, null = 8), `CompensatoryTimeMaxBalanceHours` (`decimal(6,2)`, null = sin tope, P-10), `CompensatoryTimeCreditRequiresDocument` (`bool?`, null = **sí**, P-11), `CompensatoryTimeSettlementRateFactor` (`decimal(5,2)`, null = 1.00, P-15). Mutador dedicado con validaciones > 0 + refresh de token (patrón :74-83); expuestas en GET/PUT/PATCH de `CompanyPreferencesController`.

### 3.5 Módulo de reglas puro — `Features/PersonnelFiles/CompensatoryTime/CompensatoryTimeRules.cs`

Estático, sin side-effects, sin reloj:
- `CreditedHours(hoursWorked, factor)` → `Round(worked × factor, 2, AwayFromZero)`.
- `Balance(totalCredited, totalDebited)` y `BuildStatement(movements[])` → movimientos cronológicos (fecha, desempate `CreatedUtc`) con saldo corrido + totales; los `ANULADA` se marcan y excluyen del saldo.
- `SuggestAbsenceHours(startDate, endDate, restDay, holidays, standardDailyHours)` → días del rango excluyendo `restDay` y asuetos × horas estándar (con REQ-001 ausente: días calendario × horas estándar — modo degradado D-18).
- `ValidateDebit(balance, hoursToDebit)` / `MaxAnnullable(balance)` / `MaxCreditable(balance, cap)` → soportes de RN-03/RN-06/RN-11 (la re-verificación real ocurre en el handler bajo lock).
- `HourlyRate(dailySalary, standardDailyHours)` y `SettlementAmount(hours, hourlyRate, rateFactor)` → valoración D-19 (aclaración №2), consumidos por el `case` del motor de liquidación (§3.11) para que la fórmula viva en un solo lugar.

Paridad de localización: cada código de error del módulo tiene recurso EN/ES/es-SV (test espejo).

### 3.6 Aplicación — feature folders

```
Application/Features/Leave/
  CompensatoryTimeTypes.cs / .Handlers.cs            ← maestro governed (sin seeder)
Application/Features/PersonnelFiles/CompensatoryTime/
  CompensatoryTimeCredits.cs / .Handlers.cs          ← alta con adjunto obligatorio + edición/anulación
  CompensatoryTimeCreditDocuments.cs / .Handlers.cs  ← sub-recurso documentos (espejo medical-claims)
  CompensatoryTimeAbsences.cs / .Handlers.cs         ← alta/edición/anulación con lock + sugerencia
  CompensatoryTimeStatement.cs / .Handlers.cs        ← estado de cuenta (RRHH + self) + totales
  CompensatoryTimeRules.cs                           ← módulo puro (§3.5)
  CompensatoryTimeBandeja.cs / .Handlers.cs          ← query empresa + export rows
Abstractions/PersonnelFiles/ICompensatoryTimeRepository.cs  ← agregación de saldo, statement, bandeja/export, lock
Infrastructure/PersonnelFiles/CompensatoryTimeRepository.cs
```

Convenciones en todos los handlers: CQRS + FluentValidation; validación de tipo por referencia activa + operación (RN-04); auditoría doble-`SaveChanges`; **asiento de personal en la misma transacción** (patrón fila #13: `PersonnelFilePersonnelAction.Create(código, "APLICADA", …, isSystemGenerated: true)`); DTOs response con `[JsonIgnore] Id => XxxPublicId`; `[Required]` param-target en records posicionales (gotcha memorado).

`ICompensatoryTimeRepository` (núcleo):
- `GetBalanceAsync(personnelFileId)` — agregación Σ créditos − Σ débitos vigentes (única fuente, consumida por statement/perfil/validaciones/liquidación).
- `GetStatementAsync(...)` — proyección común crédito/ausencia (`Concat`) ordenada + paginada.
- `AcquireFundLockAsync(tenantId, personnelFileId)` — `pg_advisory_xact_lock` (aclaración №3; no-op en fakes).
- `QueryMovementsAsync` / `GetMovementExportRowsAsync` / `GetBalanceExportRowsAsync` — bandeja/exports.
- Consultas de solape (ausencias propias; incapacidades/vacaciones de REQ-001 aisladas en un método, aclaración №6).

### 3.7 API — controllers y contratos

| Controller | Endpoints clave | Gate |
|---|---|---|
| `CompensatoryTimeTypesController` (empresa, governed) | `GET/POST /companies/{companyId}/compensatory-time-types` · `GET/PUT/DELETE /…/{id}` | `[ResourceActions]` familia governed |
| `PersonnelFileCompensatoryTimeCreditsController` | `GET/POST /personnel-files/{publicId}/compensatory-time-credits` · `GET/PUT /…/{id}` · `PATCH /…/{id}/annulment` | Escrituras: Manage · lecturas: View OR self |
| `PersonnelFileCompensatoryTimeCreditDocumentsController` | `GET/POST /…/compensatory-time-credits/{id}/documents` · `DELETE /…/documents/{docId}` (`parentConcurrencyToken`) · `GET /…/documents/{docId}/read-url` | mismo corte que medical-claims |
| `PersonnelFileCompensatoryTimeAbsencesController` | `GET/POST /personnel-files/{publicId}/compensatory-time-absences` · `GET/PUT /…/{id}` · `PATCH /…/{id}/annulment` · `GET /…/absence-hours-suggestion?start=&end=` | Escrituras: Manage · lecturas: View OR self |
| `PersonnelFileCompensatoryTimeStatementController` | `GET /personnel-files/{publicId}/compensatory-time-statement` (movimientos + saldo corrido + totales, paginado, filtros fecha/tipo/estado, `includeAnnulled`) | View OR self |
| `CompensatoryTimeReportingController` (empresa, **sin** `[AuthorizationPolicySet]` — precedente fila #12) | `POST /companies/{companyId}/compensatory-time-movements/query` (`StatusCounts`, rate-limit Search) · `GET /…/compensatory-time-movements/export` (rate-limit Export) · `GET /…/compensatory-time-balances/export` | View (gate por handler) |

Contratos: If-Match en todo write (`[FromIfMatch]`), DELETE → `parentConcurrencyToken`, códigos como strings, `xxxPublicId`, errores `extensions.code` bilingües. El POST de crédito lleva `authorizationFilePublicId` (aclaración №4) y `overtimeRecordPublicId?` (D-21). El 422 de fondo insuficiente incluye `saldoDisponible` y `horasFaltantes` en extensions.

### 3.8 Adjunto de autorización (D-20/RF-012)

`FilePurpose.CompensatoryTimeDocument` (valor nuevo del enum) + bloque en appsettings **base**:
```json
"CompensatoryTimeDocument": {
  "MaxSizeBytes": 10485760,
  "AllowedContentTypes": [ "application/pdf" ],
  "AllowedExtensions": [ ".pdf" ],
  "DefaultProvider": "AzureBlob",
  "RequiresMalwareScan": false,
  "ContainerOverride": "clarihr-compensatory-time-documents"
}
```
(P-11: solo PDF; ampliable por configuración). Contenedor pre-aprovisionado (checklist §9 — gotcha: config faltante → 422).

Flujo: upload-session → complete → **el POST del crédito** valida y asocia (aclaración №4) → documentos adicionales vía sub-recurso → read-url por documento (patrón `MedicalClaimsController.cs:220`). Obligatoriedad por preferencia (`CompensatoryTimeCreditRequiresDocument`, default sí); la **ausencia no** lleva adjuntos.

### 3.9 Estado de cuenta, perfil y exportaciones (D-10/D-11/RF-011)

- **Statement**: proyección común (`Fecha`, `Operacion` ACREDITACION/AUSENCIA, tipo, detalle, `Horas ±`, estado, saldo corrido) + totales (`totalAcreditado`, `totalDebitado`, `saldoDisponible`). Saldo corrido calculado por `CompensatoryTimeRules.BuildStatement` sobre la página completa del rango (orden fecha + `CreatedUtc`).
- **Perfil**: `CompensatoryTimeHoursAvailable` (`decimal?`) aditivo en `PersonnelFileEmployeeProfileResponse` (tras :40); `null` sin movimientos; poblado con `GetBalanceAsync` en la misma costura que REQ-001 use para sus saldos (§3.10 de su plan: proyección o enriquecedor — alinear al construir; gotcha member-init en proyección EF).
- **Export rows** (propiedades en español, `ReportExportFileWriter`):
  - `MovimientoTiempoCompensatorioExportRow`: Empleado, CodigoEmpleado, Operacion, Tipo, FechaInicio, FechaFin, HorasTrabajadas, Factor, Horas (±), Detalle, AutorizadoPor, Estado, PeriodoPlanilla (etiqueta+fechas si viaja), FechaRegistro. Anulados excluidos por defecto (filtro documentado).
  - `SaldoTiempoCompensatorioExportRow`: Empleado, CodigoEmpleado, TotalAcreditado, TotalDebitado, SaldoDisponible, UltimoMovimiento.
- Rate limits `Search`/`Export` + límite síncrono existente (413).

### 3.10 Asientos y bloqueo por retiro

- Alta de crédito → acción `ACREDITACION_TIEMPO_COMPENSATORIO`; alta de ausencia → `GOCE_TIEMPO_COMPENSATORIO` (vigencias = fechas del registro; `"APLICADA"`; `isSystemGenerated: true`), **misma transacción** (fila #13). Anulaciones: trazables en el registro fuente (sin ActionType propio, corte REQ-001).
- Perfil `RETIRADO` → **422 en toda escritura del módulo** (altas, ediciones y anulaciones — aclaración №9); reuso del código existente de perfil bloqueado.

### 3.11 Integración con liquidación (D-19/RF-013) — toca Settlements

**Cadena completa (5 toques quirúrgicos):**

1. **Contexto** — `SettlementCalculationContext` (`ISettlementRepository.cs:42-53`): +`CompensatoryTimeContext? CompensatoryTime` = `(decimal PendingHours, decimal StandardDailyHours, decimal RateFactor)`. Resuelto dentro de `GetCalculationContextAsync` (`SettlementRepository.cs`, junto a la lectura de `CompanyPreference` :185-189): **solo si** la plaza de la liquidación es la **plaza principal** del empleado (aclaración №8) **y** `GetBalanceAsync > 0`; preferencias con defaults (8 / 1.00). Sin datos → `null` (un solo viaje, sin fetch extra en los call-sites de `Recalculate`).
2. **Input del motor** — `SettlementCalculationInput` (`SettlementCalculation.Rules.cs:93-105`): +`CompensatoryTimeInput? CompensatoryTime` (espejo). Mapeado en `SettlementCalculationSupport.Recalculate` (`Settlements.Handlers.cs:66-87`).
3. **Spec automático** — `BuildSuggestedSpecs` (:300-336): si `input.CompensatoryTime is { PendingHours: > 0 }` → agregar `EngineSpec(SettlementConceptCodes.HorasExtrasPendientes)` (línea de **sistema** a nivel spec; el seed del catálogo NO cambia — aclaración №8). En `BuildSpecsFromExisting` no hay cambio (la línea persistida ya viaja como estado).
4. **`case` nuevo en `ComputeIncomeLine`** (switch :380-439), espejo del de `VACACION_PROPORCIONAL` (:391-396):
   ```csharp
   case SettlementConceptCodes.HorasExtrasPendientes:   // solo specs de sistema (los manuales cortan antes, :373-377)
       units = spec.UnitsOrDays ?? input.CompensatoryTime?.PendingHours ?? 0m;   // horas (refresca si no hay UnitsOverridden)
       calculationBase = CompensatoryTimeRules.HourlyRate(derived.DailySalary, input.CompensatoryTime?.StandardDailyHours ?? 8m);
       calculated = CompensatoryTimeRules.SettlementAmount(units.Value, calculationBase.Value, input.CompensatoryTime?.RateFactor ?? 1m);
       detail = $"{units:0.##} h × {calculationBase:C}/h × factor {rateFactor:0.##}";
       break;
   ```
   La descripción por defecto de la línea: `"Saldo de tiempo compensatorio ({units} h al {retirementDate})"`. Afectaciones ISSS/AFP/Renta: **automáticas** — el concepto ya está configurado (Ingreso, afecta las 3 bases, exención `Ninguna`); las bases del paso 4 del motor (:222-246) lo toman solas por `FinalAmount` incluido.
5. **Guard anti-duplicado** — `AddSettlementManualLineCommandHandler` (:590-640): si ya existe una línea de sistema con el mismo `ConceptCode` en la liquidación → 422 `SETTLEMENT_CONCEPT_ALREADY_SUGGESTED` (aclaración №8).

**Comportamiento resultante** (verificable con los helpers de test existentes `StateOf`/`ExistingFrom`):
- Generación con saldo 12 h, salario $600, 8 h/día, tarifa 1.00 → línea `HORAS_EXTRAS_PENDIENTES`: `UnitsOrDays=12`, `CalculationBase=2.50`, `CalculatedAmount=30.00`, `CalculationDetail` con la traza (golden A.4-10).
- Liquidador edita horas (`SetUnitsOrDays` → `UnitsOverridden=true`) o monto (`SetOverride`) → sobrevive recálculos normales (estado por `PublicId`, `Settlements.Handlers.cs:91-110`); **regenerate** la descarta y re-sugiere fresca (mecánica existente, documentada en guía FE).
- Sin módulo/sin saldo/plaza no principal → contexto `null` → ni spec ni case activo → **suite de settlements existente intacta** (test de retrocompatibilidad en ambos sentidos, patrón `Renta_WithoutBrackets_IsZeroWithWarning`).
- La vía manual actual sigue operativa cuando no hay línea automática (tenants sin módulo).

### 3.12 Localización y auditoría

- ~18 códigos nuevos (mapa §5) EN+ES+es-SV con paridad (`BackendMessageLocalizationTests`) + `validation.message.*` por cada `WithMessage` de los ~8 validadores nuevos.
- Auditoría: doble-`SaveChanges` en cada write; `ReportExported` en exports (ya lo hace el delivery service); asientos con vigencias del registro fuente; anulaciones con motivo/quién/cuándo.

---

## 4. Migraciones y seeds

| # | Migración (PR) | Contenido |
|---|---|---|
| M1 (PR-1) | `AddCompensatoryTimeConfiguration` | `CreateTable compensatory_time_types` (+índice único normalizado filtrado) + seeds TPH `compensatory-time-statuses` (**-9865/-9866**) y `compensatory-time-operations` (**-9867…-9869**) + `InsertData` 2 ActionTypes (**-9870/-9871**, SV) + `AddColumn` × 4 en `company_preferences` |
| M2 (PR-2) | `AddPersonnelFileCompensatoryTime` | Tablas `personnel_file_compensatory_time_credits` (+`overtime_record_public_id uuid NULL` — costura D-21), `personnel_file_compensatory_time_credit_documents`, `personnel_file_compensatory_time_absences` + índices §3.4 + CHECK constraints |

- **Sin seeds de tipos** (D-05) → no hay seeder de plantilla. Verificar IDs libres contra `GlobalCatalogSeedData` al abrir PR-1 (aclaración №12).
- **DevSeed** (tenant demo): 3 `CompensatoryTimeType` (`TRABAJO_FUERA_JORNADA` ACREDITA 1.00 · `TRABAJO_ASUETO` ACREDITA 2.00 · `GOCE_TIEMPO_COMPENSATORIO` DEBITA), preferencias default, 2 créditos (8 h y 4 h, sin documento — el requisito es del handler, aclaración №4) + 1 ausencia de 8 h → saldo demo 4 h visible en perfil/statement.
- Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add … -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · guardrail `MigrationSeedingIntegrationTests`.

---

## 5. Mapa de errores (resumen)

| Código | HTTP | Dónde |
|---|---|---|
| `COMPENSATORY_TIME_TYPE_INVALID` | 422 | Tipo inexistente/inactivo (crédito, ausencia) |
| `COMPENSATORY_TIME_TYPE_OPERATION_MISMATCH` | 422 | Tipo `DEBITA` en crédito / `ACREDITA` en ausencia (RN-04) |
| `COMPENSATORY_TIME_TYPE_IN_USE` | 422 | Baja/edición de código de tipo referenciado por registro activo |
| `COMPENSATORY_TIME_WORK_DATE_IN_FUTURE` | 422 | `workDate` > hoy (RN-15) |
| `COMPENSATORY_TIME_TIME_RANGE_INVALID` | 422 | `startTime`/`endTime` incoherentes |
| `COMPENSATORY_TIME_DOCUMENT_REQUIRED` | 422 | Alta de crédito sin `authorizationFilePublicId` con preferencia activa (D-20) |
| `COMPENSATORY_TIME_OVERRIDE_NOTE_REQUIRED` | 422 | Ajuste de `hoursCredited` sin nota (RN-02) |
| `COMPENSATORY_TIME_MAX_BALANCE_EXCEEDED` | 422 | Tope P-10; extensions con máximo acreditable (RN-11) |
| `COMPENSATORY_TIME_BALANCE_INSUFFICIENT` | 422 | Débito > saldo; extensions `saldoDisponible`/`horasFaltantes` (RN-03) |
| `COMPENSATORY_TIME_BALANCE_WOULD_GO_NEGATIVE` | 422 | Editar/anular crédito descubre débitos (RN-06) |
| `COMPENSATORY_TIME_ABSENCE_OVERLAP` | 422 | Solape con ausencia compensatoria vigente (RN-05) |
| `COMPENSATORY_TIME_INCAPACITY_OVERLAP` / `COMPENSATORY_TIME_VACATION_OVERLAP` | 422 | Solape con incapacidad activa / solicitud-goce vivo (REQ-001 presente) |
| `COMPENSATORY_TIME_PAYROLL_PERIOD_INVALID` | 422 | Instancia de periodo inexistente/inactiva, si viaja (P-14) |
| `COMPENSATORY_TIME_ANNULMENT_REASON_REQUIRED` | 422 | Anulación sin motivo (RN-07) |
| `COMPENSATORY_TIME_STATE_RULE_VIOLATION` | 422/409 | Editar/anular registro `ANULADA` |
| `SETTLEMENT_CONCEPT_ALREADY_SUGGESTED` | 422 | Línea manual de un concepto que ya existe como línea de sistema (§3.11.5) |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` (reuso) | 422 | Toda escritura del módulo sobre perfil `RETIRADO` (aclaración №9) |

Reusados: 400/409 de If-Match, 403 de gates (`ViewCompensatoryTime OR self` / `ManageCompensatoryTime`), errores de purpose/tamaño/tipo de archivos (`InvalidPurpose`, config faltante → 422), `PERSONNEL_FILE_EXPORT_FORMAT_INVALID`, `REPORT_EXPORT_TOO_LARGE` (413).

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- **`CompensatoryTimeRulesTests`** — golden cases del Anexo A.4 (ratificados) como `[Theory]` bloqueantes:
  - A.4-1/3: acreditación 3 h factor 1.00 → 3.00; acumulación 8+4 → saldo 12; débito 8 → saldo 4; débito > saldo → violación con faltante.
  - A.4-4: factor 2.00 × 4 h → 8.00 (redondeo Round2; propiedad: `CreditedHours` estable con céntimos conflictivos).
  - A.4-5: `MaxAnnullable` — con saldo 4 (12−8), anular el crédito de 12 → violación; tras anular la ausencia → permitido.
  - A.4-7: tope 40, saldo 38, acreditar 6 → violación con máximo 2.00; sin tope (null) → sin límite.
  - A.4-8: sugerencia lun–mié con asueto martes y descanso domingo → 16 h; sin calendario (degradado) → días × 8.
  - A.4-10: `HourlyRate(20.00, 8) = 2.50`; `SettlementAmount(12, 2.50, 1.00) = 30.00`; tarifa 2.00 → 60.00.
  - `BuildStatement`: orden cronológico, saldo corrido, anulados marcados/excluidos.
- **Dominio**: guards de `Create/Update/Annul` (motivo obligatorio, horas > 0, rango horario, transiciones inválidas), `ApplyCreditedHours` con override sin nota lanza, documento con `filePublicId` vacío lanza, tipo con factor ≤ 0 lanza.
- **Motor de liquidación** (`SettlementCalculationRulesTests` — extender fixture existente): con `CompensatoryTime` en el input → línea presente con units/base/calculated correctos y detail; `ExistingFrom`+`StateOf with { UnitsOrDays = 10 }` → horas editadas alimentan la fórmula (patrón `EditedDays_FeedTheFormula` :334-348); `OverrideAmount` sobrevive; **sin** `CompensatoryTime` → resultado idéntico al actual (retrocompatibilidad, patrón :266-274); bases ISSS/AFP/Renta incluyen la línea.
- Validadores; governance (2 policies); **paridad de localización** (~18 códigos).

**Integración (`tests/CLARIHR.Api.IntegrationTests/ApiIntegrationTests.CompensatoryTime.cs`):**
- **Adopción**: tenant nuevo → maestro vacío; crear crédito sin tipos → 422; admin crea tipos (AllowedActions presentes — guardrail); tipo `ACREDITA` usado en ausencia → 422.
- **Round-trip crédito**: upload PDF (purpose nuevo) → POST crédito con adjunto → 201 con horas acreditadas + asiento `ACREDITACION_TIEMPO_COMPENSATORIO` + documento asociado; sin adjunto → 422; propósito ajeno → 422; con preferencia en `false` → 201 sin adjunto; `workDate` futura → 422; override sin nota → 422.
- **Round-trip ausencia**: sugerencia de horas → POST con saldo suficiente → 201 + asiento `GOCE`; insuficiente → 422 con `saldoDisponible`; solape → 422; imputación a periodo de planilla inválido → 422.
- **Invariantes + carrera**: anular crédito que descubre débitos → 422; anular ausencia → saldo restaurado; **dos débitos concurrentes** (`Task.WhenAll` de 2 POST contra saldo que solo cubre uno) → exactamente un 201 y un 422 (lock advisory).
- **Statement/perfil**: totales cuadran contra movimientos en cada paso; `compensatoryTimeHoursAvailable` del perfil = saldo del statement; autogestión ve lo propio, otro expediente → 403; `RETIRADO` → 422 en alta/edición/anulación.
- **Bandeja/exports**: query pagina con `StatusCounts`; export de movimientos excluye anulados por defecto e incluye periodo imputado; export de saldos por empleado.
- **Liquidación E2E**: retiro + saldo 12 h → liquidación con línea automática $30.00 editable (editar horas → recalcula; regenerate → re-sugiere); **sin saldo → sin línea** y suite `ApiIntegrationTests.Settlements.cs` existente verde; línea manual duplicada del concepto → 422; empleado multi-plaza → línea solo en la liquidación de la plaza principal.
- Guardrails existentes verdes: `MigrationSeedingIntegrationTests`, `GeneralCatalogKeyMapGuardrailsTests`, `OpenApiContractGuardrailsIntegrationTests`, `AuthorizationPolicyConvention*`, `AllowedActionsCoverageIntegrationTests`, `BackendMessageLocalizationTests`.

---

## 7. Orden de implementación (PRs sugeridos)

> Rama `feature/tiempo-compensatorio`, creada desde `master` **después del merge de REQ-001** (D-18). Cada PR lleva sus claves resx y sus tests; convención de commits del repo (sin trailer de co-autoría de IA).

1. **PR-1 — Configuración (M1)** (§3.1/§3.2/§3.3): maestro `CompensatoryTimeType` governed (sin semilla) + 2 TPH + 2 ActionTypes + 2 permisos/policies/gates + 4 columnas de preferencias con PATCH admin + `FilePurpose.CompensatoryTimeDocument` + bloque appsettings base + openapi temprano (contratos de maestro y preferencias para FE).
2. **PR-2 — Dominio + reglas (M2)** (§3.4/§3.5): 3 entidades + configs EF + índices/CHECKs + `CompensatoryTimeRules` puro con **golden A.4 en verde (gate de la ola)** + batería unitaria de dominio + `ICompensatoryTimeRepository` (agregación + lock).
3. **PR-3 — Acreditaciones end-to-end** (§3.7/§3.8): CRUD + **adjunto obligatorio en el POST** (patrón nuevo) + sub-recurso documentos + read-url + asientos + invariante anti-descubierto bajo lock + integración completa.
4. **PR-4 — Ausencias + fondo end-to-end** (§3.7/§3.9): CRUD con verificación transaccional + carrera + sugerencia de horas (asuetos/`restDayOfWeek` de REQ-001) + solapes cruzados + imputación a periodo + statement (RRHH/self) + **saldo en perfil** + asientos.
5. **PR-5 — Bandeja + exportaciones** (§3.9): reporting controller (query movimientos + export movimientos + export saldos) con rate limits y 413.
6. **PR-6 — Integración liquidación + cierre** (§3.11): contexto/input/spec/case/guard en Settlements + tests de motor extendidos + E2E integral + verificación (suites verdes, drift vacío, seeds en BD real) + `openapi.yaml` final + `docs/technical/guia-integracion-frontend-tiempo-compensatorio.md` (incluye **paso de adopción**: crear tipos + 4 preferencias + carga de saldos iniciales, y el comportamiento del regenerate sobre la línea automática).

> **Gate de la ola**: A.4 en verde en PR-2 (los números ya están ratificados — sin hito externo). P-15 (tarifa) se confirma con el negocio **antes del despliegue**; el default 1.00 parametrizable permite no bloquear ningún PR.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Carrera del saldo (el riesgo real).** El invariante saldo ≥ 0 es cross-row; optimistic concurrency no basta. Mitigación: `pg_advisory_xact_lock` por (tenant, expediente) en toda escritura que reduzca saldo + re-verificación dentro de la transacción + test de carrera con `Task.WhenAll` (aclaración №3). La clave del lock se deriva con el mismo hash de 2 enteros que PositionSlots.
- **R-T2 — Retrocompatibilidad del motor de liquidación.** Primer cambio al motor post-merge. Mitigación: campo de input **nullable** (sin datos → cero ramas nuevas ejecutadas), test espejo "sin input → output idéntico", suite Settlements existente como guardrail bloqueante en PR-6.
- **R-T3 — Doble línea auto + manual.** El concepto sigue siendo manual-eligible en el catálogo. Mitigación: guard `SETTLEMENT_CONCEPT_ALREADY_SUGGESTED` en `AddManualLine` (§3.11.5) + test.
- **R-T4 — Doble sugerencia multi-plaza.** Fondo por empleado vs liquidación por plaza. Mitigación: contexto resuelto solo para la plaza principal (aclaración №8) + test con 2 plazas.
- **R-T5 — Nomenclatura del plan de REQ-001.** `UnitsOrFactorUsed`/`IsOverridden` no existen (reales: `UnitsOrDays`/`UnitsOverridden`/`OverrideAmount`) — corregir al implementar RF-019 de vacaciones para no introducir campos duplicados (aclaración №10).
- **R-T6 — Adjunto obligatorio (patrón nuevo).** Riesgo de divergencia con el flujo 3 patas existente. Mitigación: reutilizar el gate de purpose de medical-claims tal cual; el documento inicial se crea con la misma entidad/validaciones que el sub-recurso; test de los 3 caminos (con adjunto, sin adjunto+preferencia on → 422, preferencia off → 201).
- **R-T7 — Fondo congelado vs snapshot de liquidación.** Si una anulación de crédito fuera posible tras emitir la liquidación, el pago quedaría desincronizado. Mitigación: bloqueo total de escrituras en `RETIRADO` (aclaración №9) + test; la reversión de retiro (que reabre) ya anula borradores por el gancho existente.
- **R-T8 — Proyección del perfil.** Un agregado más en el GET de perfil. Mitigación: agregación única indexada por `(tenant, personnel_file, status)`; alinear con la mecánica que REQ-001 implemente para sus 2 saldos (mismo lugar, un solo viaje); gotcha member-init.
- **R-T9 — Statement con saldo corrido paginado.** El saldo corrido de una página intermedia requiere el acumulado previo. Mitigación: el repositorio calcula el acumulado anterior al corte (una agregación extra) y `BuildStatement` arranca de ese offset; test con paginación.
- **R-T10 — Maestro vacío en adopción.** Sin tipos el módulo no opera (tipo obligatorio). Mitigación: documentado como paso de adopción en guía FE + error claro `COMPENSATORY_TIME_TYPE_INVALID`; DevSeed puebla el tenant demo.
- **R-T11 — `[ResourceActions]`/`ISupportsAllowedActions`** en el maestro: cada DTO PUT/PATCH lo implementa (solo la integración lo detecta — memoria del repo).
- **R-T12 — `dotnet ef`** requiere `DOTNET_ROLL_FORWARD=Major`; nombres de índices/constraints ≤ 63 chars.

---

## 9. Checklist de implementación

- [ ] **Maestro:** `CompensatoryTimeType` + config EF + controller governed (`ISupportsAllowedActions` en todos los DTOs) + guard de uso + **sin semilla** (D-05).
- [ ] **Catálogos/acciones:** 2 TPH (`-9865/-9866`, `-9867…-9869`) + key map + switch + guardrails de biyección + 2 ActionTypes (`-9870/-9871`) — verificar IDs libres al abrir PR-1 (trampa -9490…-9496).
- [ ] **Permisos:** 2 codes + provisioning + policies (`View` authn-only, `Manage` RequireAssertion) + `Ensure…` fail-closed + gates (`LoadForManage…`, `LoadCompletedEmployeeFor…ReadAsync`) + governance verde.
- [ ] **Preferencias:** 4 columnas anulables + `SetCompensatoryTimePolicies` + PATCH admin + defaults documentados (8 h / sin tope / adjunto sí / tarifa 1.00).
- [ ] **Dominio:** crédito (+`OvertimeRecordPublicId` costura, +snapshot de tipo/factor, +override con nota), documento espejo, ausencia; guards completos; CHECKs e índices (≤ 63 chars).
- [ ] **Reglas:** `CompensatoryTimeRules` (saldo, statement, sugerencia, valoración) + golden A.4 en verde + paridad de localización.
- [ ] **Repositorio:** agregación de saldo única + statement con offset de saldo corrido + `pg_advisory_xact_lock` (no-op en fakes) + solapes (propios + REQ-001 aislados).
- [ ] **Acreditaciones:** POST con adjunto obligatorio por preferencia (gate de purpose espejo medical-claims, misma transacción) + edición/anulación anti-descubierto bajo lock + asiento.
- [ ] **Ausencias:** verificación transaccional + carrera cubierta + sugerencia (asuetos/restDay REQ-001, degradable) + solapes + imputación opcional a periodo + asiento.
- [ ] **Fondo:** statement (View OR self) + `compensatoryTimeHoursAvailable` aditivo en perfil (cuadre por construcción).
- [ ] **Adjuntos:** `FilePurpose.CompensatoryTimeDocument` + `Storage:Purposes` en appsettings **base** (PDF) + contenedor aprovisionado + sub-recurso documentos + read-url.
- [ ] **Bandeja/exports:** query con `StatusCounts` + 2 export-rows en español + rate limits + 413.
- [ ] **Liquidación:** contexto (+plaza principal) → input nullable → spec condicional → `case` con `HourlyRate`/`SettlementAmount` → guard anti-duplicado; retrocompatibilidad probada en ambos sentidos; suite Settlements existente verde.
- [ ] **Localización:** ~18 códigos EN/ES/es-SV + paridad + `validation.message.*`.
- [ ] **Pruebas:** unitarias (§6) + `ApiIntegrationTests.CompensatoryTime.cs` + extensión de `SettlementCalculationRulesTests` + guardrails existentes verdes + suite completa del repo en verde.
- [ ] **Cierre:** `openapi.yaml` regenerado sin drift · DevSeed actualizado (3 tipos demo + movimientos) · checklist de despliegue (migraciones M1-M2, `Storage:Purposes:CompensatoryTimeDocument` base + contenedor, **paso de adopción por empresa**: tipos + preferencias + saldos iniciales con acta, **confirmar P-15**) · `guia-integracion-frontend-tiempo-compensatorio.md`.
