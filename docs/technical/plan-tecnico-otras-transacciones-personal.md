# Plan Técnico de Implementación — Otras transacciones de personal (Reconocimientos · Amonestaciones · Catálogos maestros · Consulta de disponibilidad de tiempos)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-otras-transacciones-personal.md`](../business/analisis-otras-transacciones-personal.md) (**RATIFICADO 2026-07-05: D-01…D-18 sin ajustes + P-01…P-14 respondidas**; P-14: «no hay multas» → sin advertencia legal en F1, capacidad de descuento intacta) |
| **Módulos** | `PersonnelFiles` (Recognitions · DisciplinaryActions — net-new) · maestros por empresa `RecognitionType`/`DisciplinaryActionType`/`DisciplinaryActionCause` (net-new, **con plantilla sembrada**) · `GeneralCatalogs` (1 TPH + 1 ActionType; **reutiliza** `AMONESTACION=-9477`/`SUSPENSION=-9478`) · **toque quirúrgico al dominio existente**: mutador `Annul()` en `PersonnelFilePersonnelAction` · Files (2 purposes nuevos) · Provisioning (7 permisos RBAC; 2 `Authorize*` excluyen Admin) · Reporting/Export (bandejas + insumo de planilla + consulta de tiempos) · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-07-05 |
| **País de referencia** | El Salvador (SV, `CountryCatalogItemId = -7068L`) |
| **Secuenciación** | Desarrollo **después de REQ-001 y REQ-002** (D-18): hereda el patrón de plantilla de maestros (REQ-001 PR-1) y la consulta nace con más fuentes. Los submódulos disciplinares no tienen dependencia dura (adelantables extrayendo el seeder); la consulta F1 opera con 2 fuentes propias |
| **Endurecimientos de la ratificación** | Flujo de **una decisión** `EN_REVISION → APLICADA/RECHAZADA` + `ANULADA`/revocación (D-03) · anti-autoaprobación **doble** (sujeto + registrador, D-04) · `Authorize*` **excluye Admin** (D-05) · suspensión = rango de fechas + **días calendario**, sin tope y sin cambio de estado del empleado (P-04/P-05) · descuento = monto manual USD + concepto de egreso del catálogo país con **snapshot al aplicar** (P-02/P-03) · plantillas de los 3 maestros **se siembran** con causas sin concepto (P-12/P-14) · adjuntos F1 en ambas familias (P-07) · self = lectura solo `APLICADA` (P-06) · consulta con **payload mínimo** y permiso dedicado (P-09/P-10) |

---

## 0. Aclaraciones pre-desarrollo (recomendación del desarrollador senior; ratificación ya cerrada)

1. **Un solo módulo de reglas para ambas familias.** `PersonnelTransactionRules` (puro, sin reloj ni side-effects) define transiciones de estado, anti-autoaprobación, validación del bloque de suspensión/descuento, días calendario e **intersección de rangos** (compartida con la consulta de tiempos). Reconocimientos y amonestaciones consumen las mismas primitivas; los validadores específicos viven por familia.
2. **Anti-autoaprobación doble (D-04) — dos comparaciones en el handler de decisión/revocación:** (a) `personnelFile.LinkedUserPublicId == currentUser.UserId` (precedente exacto: `EconomicAidRequests.Handlers.cs:371-376`) y (b) `record.RegisteredByUserId == currentUser.UserId` (pata nueva). Ambas → 403 con código propio por familia. Aplica también a la **revocación** (quien registró no revoca lo suyo).
3. **Decisión atómica y carrera.** El PATCH de decisión exige If-Match, re-verifica `EN_REVISION` **dentro de la transacción** (segunda decisión concurrente → 409 por token o 422 por estado) y crea los asientos en la misma transacción (patrón `ExecuteRetirementRequest.cs:178-192`). Para el **solape de suspensiones** (RN-18), la ventana real de carrera es aplicar dos amonestaciones con rangos solapados a la vez: la decisión que **aplica** una amonestación con suspensión toma `pg_advisory_xact_lock(tenantKey, personnelFileKey)` antes de re-verificar el solape (helper/patrón que REQ-002 deja instalado; precedentes `PositionSlotRepository.cs:192-202`, `CompanyRepository.cs:24-34`; no-op en fakes). Los reconocimientos no llevan lock (no tienen invariante cross-row).
4. **Revocación de asientos = toque quirúrgico al dominio existente (verificado: hoy no hay mutadores).** `PersonnelFilePersonnelAction` solo expone `Create`/`BindToPersonnelFile` (`PersonnelFileEmployee.cs:571-643`). Se agrega el mutador custodiado **`Annul()`**: valida que no esté ya `ANULADA`, asigna `ActionStatusCode = "ANULADA"` (código existente `-9496`) y rota `ConcurrencyToken`. Solo lo invocan los handlers de revocación de este módulo sobre los asientos **propios** (`IsSystemGenerated`, referenciados por `personnelActionPublicId`/`suspensionActionPublicId` del registro) — el asiento manual documental no se toca.
5. **Snapshot del concepto de descuento al APLICAR, no al crear (RN-06).** Mientras la amonestación está `EN_REVISION` el concepto es referencia editable (default de la causa, sustituible por otro egreso activo); al aplicar se congela `DeductionConceptTypeCode` + `DeductionConceptNameSnapshot`. Si el concepto quedó inactivo o no-egreso entre creación y aplicación → 422 al aplicar (el autorizador ve el error y RRHH corrige).
6. **Consulta de tiempos: contrato estable + degradación visible.** Fila única `{personnelFile, position?, categoryCode, startDate, endDate, days, statusCode, sourceModule, referencePublicId}`; el response incluye **`activeSources[]`** (F1: `["SUSPENSION","FIN_CONTRATO_TEMPORAL"]`) para que el FE muestre qué familias están conectadas sin heurísticas. Cada fuente es **un método del repositorio** que devuelve filas; el handler concatena y ordena — conectar REQ-001/REQ-002 = agregar métodos + extender `activeSources` (sin tocar el contrato). Intersección por solape: `sourceStart <= rangeEnd && sourceEnd >= rangeStart`.
7. **Fuente `FIN_CONTRATO_TEMPORAL` (derivada, sin datos nuevos).** Plazas activas con `ContractTypeCode` cuyo catálogo tenga `IsTemporary = true` y `EndDate` dentro del rango (join por código normalizado + país del tenant — mismo criterio de la proyección existente de contract-types, `PersonnelFileRepository.cs:1503`; la vigencia contractual ES `StartDate/EndDate` de la plaza, comentario `PersonnelFileEmployee.cs:181-182`). Fila con `startDate = endDate = EndDate`, `days = 1`, `statusCode = "VIGENTE"`, sin referencia a registro del módulo (`referencePublicId` = publicId de la plaza).
8. **Los 3 maestros SÍ se siembran (P-12) — a diferencia de REQ-002.** Plantilla del Anexo A.2 vía seeder idempotente por código normalizado (nunca pisa ediciones; 2.ª corrida = 0 cambios) cableado al provisioning + endpoint `load-template` para tenants existentes. Si REQ-001 dejó `LeaveTemplateSeeder` con base genérica, reutilizarla; si no, `EmployeeRelationsTemplateSeeder` propio con la misma mecánica (patrón `OrgStructureCatalogSeedService.cs:10-55` + `CompanyProvisioningService.cs:151-153`). **Las causas se siembran sin `deductionConceptTypeCode`** (P-14).
9. **Política de cada permiso (D-05).** `ViewRecognitions`/`ViewDisciplinaryActions` = policy **authn-only** (`.Combine`) porque la lectura tiene rama self (patrón ViewMedicalClaims, `Program.cs:476-481`) + gate por handler. `ManageRecognitions`/`ManageDisciplinaryActions` = RequireAssertion con fallback `Admin`/`ManageAdministration`. `AuthorizeRecognitions`/`AuthorizeDisciplinaryActions` = **RequireAssertion SIN fallback Admin** (espejo exacto de `AuthorizeRetirement`: `ProvisioningConstants.cs:89` + `Program.cs:552-568`; se declaran en `Permissions` pero **fuera** de `CompanyAdminPermissions`). `ViewTimeAvailability` = RequireAssertion estándar con fallback (lectura corporativa sin rama self).
10. **Perfil `RETIRADO` (D-16).** Reuso del código existente de perfil bloqueado: altas/ediciones → 422; en la **decisión** sobre un retirado solo se admiten `RECHAZAR`/anular (aplicar → 422). Los históricos aplicados permanecen legibles.
11. **P-14 aplicada.** El negocio declara «no hay multas»: no se construye advertencia legal ni preferencia asociada en F1; la plantilla de causas nace sin conceptos; la capacidad de descuento (flag + monto + concepto) queda operativa tal como la pide el levantamiento para los marcos que la permitan.
12. **Seeds e infraestructura.** Bloque del módulo **≤ -9875** (verificar contra `GlobalCatalogSeedData` al abrir PR-1; piso actual -9846; reservas: REQ-001 `-9850…-9862`/`-9485…-9489`, REQ-002 `-9865…-9871`, REQ-004 tentativo `-9520…-9525`; trampa: `-9490…-9496` = `ACTION_STATUS_CATALOG`). **Se reutilizan sin seed nuevo**: `AMONESTACION=-9477`, `SUSPENSION=-9478` (asientos) y `SUSPENDIDO=-9101` (estado de empleo, solo uso manual). `dotnet ef` requiere `DOTNET_ROLL_FORWARD=Major`; nombres de índices/constraints ≤ 63 chars.

---

## 1. Objetivo y enfoque

Construir el módulo de **Otras transacciones de personal**: 3 maestros por empresa con plantilla sembrada, **reconocimientos** y **amonestaciones** (con suspensión sin goce y descuento documental) bajo un **flujo de una decisión** con anti-autoaprobación doble y asientos automáticos, adjuntos por familia, bandejas + exportaciones (incluido el **insumo de planilla**: descuentos y suspensiones del periodo) y la **consulta de disponibilidad de tiempos** por fuentes conectables (F1: suspensiones + fin de contratos temporales).

**Insight central del análisis de código.** ~95 % es composición de recetas verificadas (maestros governed, TPH, permisos/gates, flujo híbrido con PATCH, adjuntos espejo, asientos en transacción, bandeja/export). Las **tres piezas sin plantilla directa** (foco de riesgo, §8):

1. **La revocación que anula asientos** — primer módulo que muta el estado de un `PersonnelFilePersonnelAction` ya escrito (mutador `Annul()` nuevo, aclaración №4).
2. **El agregador multi-fuente de disponibilidad** — primera query corporativa que une familias heterogéneas bajo un contrato estable con `activeSources[]` (aclaración №6).
3. **El permiso `Authorize*` por familia con anti-autoaprobación doble** — extiende el precedente de retiro (un solo permiso global) a dos familias con segunda pata de anti-self (registrador).

---

## 2. Línea base verificada en el código (qué se reutiliza / qué se toca)

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Asiento de personal | `PersonnelFilePersonnelAction` (`PersonnelFileEmployee.cs:571-643`): `ActionTypeCode/ActionStatusCode/ActionDateUtc/EffectiveFromUtc/EffectiveToUtc/Description/Reference/Amount/CurrencyCode/IsSystemGenerated`; **solo** `Create` (:631) + `BindToPersonnelFile` (:629), sin mutadores | Asientos del módulo con `IsSystemGenerated: true`; **+mutador `Annul()`** para la revocación (aclaración №4) |
| 2 | Escritura de asientos en transacción | Patrón `ExecuteRetirementRequest.cs:178-192` (`Create(tipo, "APLICADA", …)` + `BindToPersonnelFile` + `SetTenantId` + `AddPersonnelActionAsync`); mismo patrón en `RevertRetirementRequest.cs:204`, `RehireEmployee.cs:250`, `Settlements.Handlers.cs:995` | Aplicar = registro + 1-2 asientos en la misma transacción |
| 3 | Endpoints de acciones existentes | `PersonnelFileEmploymentController.cs`: POST manual :562, GET by id :604, search :626, export :671; alta manual `Employment/PersonnelActions.cs:210` (cualquier tipo+estado) | Coexistencia declarada (RN-16): el manual sigue; el módulo escribe `IsSystemGenerated` |
| 4 | Catálogos de acciones | `ACTION_TYPE_CATALOG` (`GlobalCatalogSeedData.cs:736-750`): `AMONESTACION=-9477`, `SUSPENSION=-9478`, `PERMISO=-9479`, `REINTEGRO=-9480`, …, `LIQUIDACION=-9484`; `ACTION_STATUS_CATALOG` (:755-761): `-9490…-9496` con `APLICADA=-9495`, `ANULADA=-9496` | Reutilizar `-9477`/`-9478`; sembrar solo `RECONOCIMIENTO`; asientos `APLICADA`→`ANULADA` |
| 5 | Flujo de una decisión (molde) | Ayuda económica: constantes híbridas (`PersonnelFileEmployee.cs:1678-1692`), `Resolve(...)` (:1799), PATCH resolution/disbursement/cancel (`EconomicAidRequestsController.cs:151/:184/:216`), anti-self (`EconomicAidRequests.Handlers.cs:371-376`, error `EconomicAidRequest.Rules.cs:46`) | Espejo del ciclo `EN_REVISION → APLICADA/RECHAZADA/ANULADA` + segunda pata anti-self (aclaración №2) |
| 6 | Permiso que excluye Admin | `PersonnelFiles.AuthorizeRetirement` (`ProvisioningConstants.cs:89`, "No implicado por la administración…"); policy `Program.cs:552-568` (comentario :552-554: fuera del fallback Admin); ejemplo hermano `AuthorizeRehire` (:70) | Molde exacto de `AuthorizeRecognitions`/`AuthorizeDisciplinaryActions` (aclaración №9) |
| 7 | Receta de permisos | Codes `PersonnelFilePolicies.cs` + `PersonnelFileCommon.cs:82` (`PersonnelFilePermissionCodes`) + `ProvisioningConstants.Permissions` (:30-93) + policies `Program.cs` (View authn-only :476-481; Manage RequireAssertion :468-474) + `EnsureHasAnyClaimAsync` (`PersonnelFileAuthorizationService.cs:362`; ejemplo :147-155) + governance tests | 7 codes nuevos con las 3 variantes de policy (aclaración №9) |
| 8 | Gates self-service | `PersonnelFileEmployeeHandlerBases.cs`: create-own-or-manage :227 (`isSelf` :254-257), manage-only :272/:836, lecturas View-or-self :357/:1044 (rama self :1071-1073) | +`LoadForManage…` (2) + lecturas View-or-self con **filtro `APLICADA` en la rama self** (D-13) |
| 9 | Stack de adjuntos | `StoredFile` (`StoredFile.cs:5`) + `FilePurpose` (`FileEnums.cs:12`); espejos `MedicalClaimDocument` (`PersonnelFileEmployee.cs:1323-1416`) y `EconomicAidRequestDocuments*`; gate de purpose `MedicalClaimDocuments.Handlers.cs:172-191`; flujo 3 patas `FilesController.cs:21/:53/:82`; reglas `Storage:Purposes` keyed por enum (`FileStorageOptions.cs`, appsettings base) | 2 purposes (`RecognitionDocument`, `DisciplinaryActionDocument`) + 2 entidades documento espejo; sub-recurso post-creación clásico (adjuntos opcionales — NO el patrón de adjunto-en-POST de REQ-002) |
| 10 | Conceptos de compensación | `CompensationConceptTypeCatalogItem : CountryScopedCatalogItem` (`CompensationConceptTypeCatalogItem.cs:13`, campos :53-86, `Nature`); enums `CompensationNature { Ingreso=1, Egreso=2 }`/`DeductionClass` (`CompensationEnums.cs:4-22`); GET `api/v1/compensation-concept-types` (`CompensationConceptTypesController.cs:19`); seeds `-9720…-9736` | La causa referencia `Nature=Egreso` (validación por código); snapshot al aplicar (aclaración №5). **No se escriben** `PersonnelFileCompensationConcept` ni `PersonnelFilePayrollTransaction` (:645, ledger externo) |
| 11 | Plaza y contratos | `PersonnelFileEmploymentAssignment` (`PersonnelFileEmployee.cs:133`): `ContractTypeCode` :183, `StartDate/EndDate` :203-205 (= vigencia contractual, comentario :181-182), `IsPrimary/IsActive` :207-209, `Close` :296; `ContractTypeCatalogItem.IsTemporary` (`GeneralCatalogItems.cs:833-859`; seed `-9460…-9467`, `GlobalCatalogSeedData.cs:693-702`: PF/OBRA/EVEN/APREN/TEMP = true); proyección con `IsTemporary` (`PersonnelFileRepository.cs:1503`) | Fuente `FIN_CONTRATO_TEMPORAL` derivada (aclaración №7) — **query nueva** (hoy no existe cruce asignación+IsTemporary+EndDate) |
| 12 | Estado de empleo | `SUSPENDIDO` sembrado (`GlobalCatalogSeedData.cs:178`, `-9101`), sin productor automático | Sin automatismo (P-05); mención en guía FE (gestión manual) |
| 13 | Maestros por empresa | Patrón `TenantEntity` (ej. `ExitInterviewForm` `ExitInterview.cs:19`, `CompanyCertificateSettings.cs:10`); seeder al provisionar `OrgStructureCatalogSeedService.cs:10-55` + `CompanyProvisioningService.cs:151-153`; `LeaveTemplateSeeder` + `load-template` idempotente llegan con REQ-001 §3.1 | 3 maestros nuevos con plantilla sembrada (aclaración №8) |
| 14 | Receta TPH | Molde `EconomicAidTypeCatalogItem` (`GeneralCatalogItems.cs:321-347`); `GeneralCatalogKeyMap.cs:21-70` (+resolver :93); switch `CatalogCodeIsActiveAsync` (`PersonnelFileRepository.cs:1580+`); `CreateGeneralCatalogSeed` (`GlobalCatalogSeedData.cs:1247`); guardrails de biyección | 1 TPH nuevo `personnel-transaction-statuses` (compartido por ambas familias, D-15) |
| 15 | Bandeja + export | `SettlementsBandeja.cs` (`StatusCounts` :33-38, export row español :45-61, PageSize :98); `SettlementsReportingController.cs` sin `[AuthorizationPolicySet]` (:16-21), rate limits :29/:70, `SynchronousReadLimit` :112; `ReportExportFileWriter.WriteAsync` (:39, headers por reflexión, XLSX OpenXML manual :78); 413 `ReportExportDeliveryService.cs:67-70` | Clonar para 2 bandejas de familia + export de insumo + consulta de tiempos |
| 16 | Localización / governed / DevSeed | Resx + `BackendMessageLocalizationTests` (paridad EN/ES/es-SV); familia governed `[ResourceActions]` + `ISupportsAllowedActions` (gotcha memorado: en **todos** los DTOs PUT/PATCH de maestros); `DevSeedService.cs` (patrón :485-536) | Convenciones obligatorias en maestros y recursos nuevos |

---

## 3. Arquitectura de la solución

### 3.1 Maestros por empresa (D-06/RF-001…RF-003) — `src/CLARIHR.Domain/EmployeeRelations/`

Tres `TenantEntity` con la misma receta governed (índice único filtrado por `is_active` sobre código normalizado; `[ResourceActions]` + `ISupportsAllowedActions`; If-Match; baja lógica con guard de uso → 422 `…_IN_USE`):

| Entidad → tabla | Campos de negocio | Notas |
|---|---|---|
| `RecognitionType` → `recognition_types` | `Code/NormalizedCode/Name`, `SortOrder` | — |
| `DisciplinaryActionType` → `disciplinary_action_types` | + **`AppliesSuspension` (bool, default false)** | Cambiarlo no altera registros existentes (el registro manda) |
| `DisciplinaryActionCause` → `disciplinary_action_causes` | + **`DeductionConceptTypeCode` (string?, nullable)** | Validado contra catálogo país: existe, activo, `Nature=Egreso` → si no, 422 |

- **Plantilla sembrada** (aclaración №8): `EmployeeRelationsTemplateSeeder.InitializeDefaultsAsync` (idempotente por código, nunca pisa ediciones) cableado al provisioning + `POST /companies/{companyId}/employee-relations/load-template` para tenants existentes. Contenido = Anexo A.2 del análisis (5 tipos de reconocimiento; 4 tipos de amonestación con solo `SUSPENSION_SIN_GOCE` en true; 6 causas **sin concepto**).
- **Controllers**: `RecognitionTypesController`, `DisciplinaryActionTypesController`, `DisciplinaryActionCausesController` — rutas `companies/{companyId}/recognition-types|disciplinary-action-types|disciplinary-action-causes`.

### 3.2 Catálogo TPH + tipo de acción (D-11/D-15)

Receta estándar (subclase + key map + switch + seed + guardrail):

| Catálogo | Wire key | Códigos (ID seed, SV `-7068L`) |
|---|---|---|
| `PersonnelTransactionStatusCatalogItem` | `personnel-transaction-statuses` | `EN_REVISION=-9875`, `APLICADA=-9876`, `RECHAZADA=-9877`, `ANULADA=-9878` |
| `ACTION_TYPE_CATALOG` (existente, +1) | `action-types` | `RECONOCIMIENTO=-9879` (**`AMONESTACION=-9477` y `SUSPENSION=-9478` se reutilizan**) |

Estados **híbridos** (D-15): constantes `PersonnelTransactionStatuses` (`EnRevision`, `Aplicada`, `Rechazada`, `Anulada` + set `Editable = {EN_REVISION}` + set `Vigentes = {EN_REVISION, APLICADA}`) en dominio; un solo TPH compartido por ambas familias (mismo ciclo); F2 agrega estados multi-nivel de forma aditiva.

### 3.3 Permisos, políticas y gates (D-05, aclaración №9)

| Code | Policy | Gate |
|---|---|---|
| `PersonnelFiles.ViewRecognitions` / `ViewDisciplinaryActions` | authn-only (`.Combine`, rama self en handler) | `LoadCompletedEmployeeFor…ReadAsync` — `View… OR isSelf` (self **filtra `APLICADA`**, D-13) |
| `PersonnelFiles.ManageRecognitions` / `ManageDisciplinaryActions` | RequireAssertion + fallback `Admin`/`ManageAdministration` | `LoadForManage…Async` — altas, ediciones, anulación de `EN_REVISION`, adjuntos |
| `PersonnelFiles.AuthorizeRecognitions` / `AuthorizeDisciplinaryActions` | **RequireAssertion SIN Admin** (espejo `Program.cs:564-568`) | Handler de decisión y de revocación (+anti-self doble) |
| `PersonnelFiles.ViewTimeAvailability` | RequireAssertion + fallback estándar | Handler de la consulta/export de disponibilidad |

Tuplas en `ProvisioningConstants.Permissions` (7); los 2 `Authorize*` **fuera** de `CompanyAdminPermissions` (espejo AuthorizeRetirement). Governance tests verdes.

### 3.4 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFilePersonnelTransactions.cs`

**`PersonnelFileRecognition : TenantEntity`** → `personnel_file_recognitions`:
- FK expediente + `BindToPersonnelFile`; `RecognitionTypeId` (FK dura) + `TypeNameSnapshot`; `EventDate` (DateOnly, ≤ hoy — validado en reglas/handler); `Detail` (1000, req.); `Amount?`/`CurrencyCode?` (informativos, > 0 si viaja, RN-17); `AssignedPositionPublicId?`; `RegisteredByUserId`; `StatusCode`; decisión (`DecidedByUserId?`, `DecidedUtc?`, `DecisionNote?` — obligatoria al rechazar); anulación (`AnnulmentReason`, `AnnulledByUserId`, `AnnulledUtc`); **`PersonnelActionPublicId?`** (asiento generado); `Notes?`; `IsActive`; token rotativo.
- Guards: `Create(...)` (estado inicial `EN_REVISION`), `Update(...)` (solo `EN_REVISION`), `Apply(byUserId, at, actionPublicId)`, `Reject(byUserId, at, note)` (nota obligatoria), `Annul(reason, byUserId, at)` (desde `EN_REVISION` o `APLICADA`; motivo obligatorio).

**`PersonnelFileDisciplinaryAction : TenantEntity`** → `personnel_file_disciplinary_actions`:
- FK expediente; `DisciplinaryActionTypeId` + `TypeNameSnapshot` + `TypeAppliedSuspension` (snapshot del flag al crear); `DisciplinaryActionCauseId` + `CauseNameSnapshot`; `IncidentDate` (≤ hoy); `FactsDetail` (2000, req.); descuento: `HasPayrollDeduction` (bool), `DeductionAmount?` (>0 si flag), `CurrencyCode?` (default USD), `DeductionConceptTypeCode?` + `DeductionConceptNameSnapshot?` (**snapshot al aplicar**, aclaración №5); suspensión: `SuspensionStartDate?`/`SuspensionEndDate?` (solo tipos con flag; inicio ≤ fin; futuras permitidas) + `SuspensionDays?` (días calendario inclusive, derivado); `AssignedPositionPublicId?`; `RegisteredByUserId`; `StatusCode`; decisión/anulación (ídem reconocimiento); **`PersonnelActionPublicId?` + `SuspensionActionPublicId?`**; `Notes?`; `IsActive`; token.
- Guards: `Create` (coherencia suspensión↔flag del tipo y descuento↔monto en reglas/handler; estado `EN_REVISION`), `Update` (solo `EN_REVISION`), `Apply(byUserId, at, actionPublicId, suspensionActionPublicId?, conceptCode?, conceptName?)`, `Reject`, `Annul`.

**Documentos** (espejo exacto `MedicalClaimDocument`, `PersonnelFileEmployee.cs:1323-1416`): `PersonnelFileRecognitionDocument` → `personnel_file_recognition_documents` y `PersonnelFileDisciplinaryActionDocument` → `personnel_file_disciplinary_action_documents` (FK al padre, `FilePublicId`, snapshots nombre/tipo/tamaño, `Observations?`).

**Toque al dominio existente**: `PersonnelFilePersonnelAction.Annul()` (aclaración №4).

CHECK constraints: `deduction_amount > 0` (cuando no null), `suspension_start_date <= suspension_end_date`, `amount > 0` (reconocimiento, cuando no null). Índices: `(tenant_id, personnel_file_id, status_code)` en ambas; amonestaciones + `(tenant_id, suspension_start_date, suspension_end_date)` (consulta/solapes) y `(tenant_id, status_code, incident_date)` (bandeja/insumo).

### 3.5 Módulo de reglas puro — `Features/PersonnelFiles/PersonnelTransactions/PersonnelTransactionRules.cs`

Estático, sin reloj:
- `CanTransition(from, to, viaDecision|viaAnnulment)` — máquina `EN_REVISION → APLICADA/RECHAZADA`; `ANULADA` desde `EN_REVISION` (Manage) o `APLICADA` (Authorize/revocación) (RN-01).
- `IsSelfDecision(linkedUserId, registeredByUserId, currentUserId)` — anti-self doble (aclaración №2).
- `ValidateSuspensionBlock(typeAppliesSuspension, start?, end?)` — exigida con flag, prohibida sin flag; inicio ≤ fin (RN-05).
- `SuspensionDays(start, end)` — días calendario inclusive (P-04).
- `ValidateDeduction(hasDeduction, amount?)` — flag → monto > 0 (RN-06).
- `RangesOverlap(aStart, aEnd, bStart, bEnd)` — primitiva compartida por solape de suspensiones (RN-18) y por la consulta (RN-15).
- `BuildAvailabilityWindow(rangeStart, rangeEnd)` — normalización/validación del rango de la consulta (obligatorio, inicio ≤ fin).

Paridad de localización: cada código de error con recurso EN/ES/es-SV (test espejo).

### 3.6 Aplicación — feature folders

```
Application/Features/EmployeeRelations/
  RecognitionTypes.cs / .Handlers.cs                    ← maestros governed (3)
  DisciplinaryActionTypes.cs / .Handlers.cs
  DisciplinaryActionCauses.cs / .Handlers.cs
  EmployeeRelationsTemplateSeeder.cs                    ← plantilla + load-template (aclaración №8)
Application/Features/PersonnelFiles/PersonnelTransactions/
  Recognitions.cs / .Handlers.cs                        ← CRUD + decisión + anulación/revocación
  RecognitionDocuments.cs / .Handlers.cs                ← sub-recurso documentos (espejo medical-claims)
  DisciplinaryActions.cs / .Handlers.cs                 ← ídem + suspensión + descuento
  DisciplinaryActionDocuments.cs / .Handlers.cs
  PersonnelTransactionRules.cs                          ← módulo puro (§3.5)
  PersonnelTransactionsBandeja.cs / .Handlers.cs        ← bandejas por familia + export + insumo planilla
  TimeAvailability.cs / .Handlers.cs                    ← consulta agregadora + export (§3.11)
Abstractions/PersonnelFiles/IPersonnelTransactionRepository.cs
Infrastructure/PersonnelFiles/PersonnelTransactionRepository.cs
```

Convenciones en todos los handlers: CQRS + FluentValidation; referencias activas de maestro del tenant (RN-04); auditoría doble-`SaveChanges`; asientos en la misma transacción (fila #2); DTOs con `[JsonIgnore] Id => XxxPublicId`; `[Required]` param-target en records posicionales (gotcha memorado).

`IPersonnelTransactionRepository` (núcleo): loaders por familia; `HasOverlappingSuspensionAsync(personnelFileId, start, end, excludeId)`; `AcquireEmployeeRelationsLockAsync(tenantId, personnelFileId)` (aclaración №3); queries de bandeja/export por familia; `GetPayrollInputRowsAsync(range)`; **fuentes de disponibilidad**: `GetSuspensionAvailabilityRowsAsync(range, filters)` + `GetTemporaryContractEndRowsAsync(range, filters)` (aclaración №6/№7).

### 3.7 API — controllers y contratos

| Controller | Endpoints clave | Gate |
|---|---|---|
| `RecognitionTypesController` / `DisciplinaryActionTypesController` / `DisciplinaryActionCausesController` (empresa, governed) | `GET/POST /companies/{companyId}/…` · `GET/PUT/DELETE /…/{id}` · `POST /companies/{companyId}/employee-relations/load-template` | familia governed |
| `PersonnelFileRecognitionsController` | `GET/POST /personnel-files/{publicId}/recognitions` · `GET/PUT /…/{id}` · **`PATCH /…/{id}/decision`** (`{decision: APLICAR\|RECHAZAR, note?}`) · `PATCH /…/{id}/annulment` | Escrituras: Manage · decisión/revocación: **Authorize** · lecturas: View OR self(`APLICADA`) |
| `PersonnelFileRecognitionDocumentsController` | `GET/POST /…/recognitions/{id}/documents` · `DELETE /…/documents/{docId}` (`parentConcurrencyToken`) · `GET /…/documents/{docId}/read-url` | mismo corte que medical-claims |
| `PersonnelFileDisciplinaryActionsController` (+documentos) | espejo de reconocimientos sobre `/disciplinary-actions` | ídem |
| `EmployeeRelationsReportingController` (empresa, sin `[AuthorizationPolicySet]` — fila #15) | `POST /companies/{companyId}/recognitions/query` · `GET /…/recognitions/export` · `POST /…/disciplinary-actions/query` · `GET /…/disciplinary-actions/export` · **`GET /…/disciplinary-actions/payroll-input/export`** (insumo: rango obligatorio) | View de la familia (gate por handler); rate limits Search/Export |
| `TimeAvailabilityController` (empresa) | **`POST /companies/{companyId}/time-availability/query`** (rango obligatorio; filtros empleado/categoría/unidad; paginado; `activeSources[]`) · `GET /…/time-availability/export` | `ViewTimeAvailability` |

Contratos: If-Match en todo write, DELETE → `parentConcurrencyToken`, códigos como strings, `xxxPublicId`, errores `extensions.code` bilingües. La rama self de lecturas devuelve **solo** registros `APLICADA` del propio expediente (bandejas y estados intermedios no viajan al empleado). El PATCH de anulación exige `reason`; sobre `APLICADA` requiere `Authorize*` (revocación) y anula los asientos vinculados.

### 3.8 Adjuntos (D-12/P-07)

`FilePurpose.RecognitionDocument` y `FilePurpose.DisciplinaryActionDocument` (valores nuevos del enum) + 2 bloques en appsettings **base**:
```json
"RecognitionDocument": {
  "MaxSizeBytes": 10485760,
  "AllowedContentTypes": [ "application/pdf", "image/jpeg", "image/png" ],
  "AllowedExtensions": [ ".pdf", ".jpg", ".jpeg", ".png" ],
  "DefaultProvider": "AzureBlob",
  "RequiresMalwareScan": false,
  "ContainerOverride": "clarihr-recognition-documents"
},
"DisciplinaryActionDocument": { …ídem…, "ContainerOverride": "clarihr-disciplinary-action-documents" }
```
Flujo 3 patas clásico (upload-session → complete → asociar por sub-recurso post-creación; adjuntos **opcionales** — sin el patrón de adjunto-en-POST de REQ-002). Gate de purpose espejo `MedicalClaimDocuments.Handlers.cs:172-191`. Contenedores pre-aprovisionados (checklist §9; gotcha memorado: config faltante → 422).

### 3.9 Bandejas, exportaciones e insumo de planilla (RF-012)

- **Bandejas por familia**: query paginada con `StatusCounts` (filtros: estado, tipo, causa, empleado, rango de fechas del hecho/falta) + export xlsx/csv/json con filas en español:
  - `ReconocimientoExportRow`: Empleado, CodigoEmpleado, Tipo, FechaHecho, Detalle, Monto, Moneda, Estado, RegistradoPor, DecididoPor, FechaDecision, FechaRegistro.
  - `AmonestacionExportRow`: + Causa, TieneDescuento, MontoDescuento, ConceptoDescuento (snapshot), SuspensionDesde/Hasta/Dias, Estado…
- **Insumo de planilla** (`GET …/disciplinary-actions/payroll-input/export`, rango obligatorio): solo amonestaciones `APLICADA` vigentes del rango con efecto — una fila por efecto: `{Empleado, CodigoEmpleado, Efecto: DESCUENTO|SUSPENSION_SIN_GOCE, Causa, ConceptoDescuento?, Monto?, Moneda?, FechaInicio, FechaFin, Dias?}` (RN-14/RN-15: las revocadas no viajan).
- Rate limits `Search`/`Export` + límite síncrono existente (413).

### 3.10 Asientos, revocación y bloqueo por retiro

- **Aplicar reconocimiento** → asiento `RECONOCIMIENTO` (`ActionDateUtc` = fecha del hecho; `Amount/CurrencyCode` si el registro los lleva). **Aplicar amonestación** → asiento `AMONESTACION` (fecha de la falta; `Amount` = monto del descuento si existe) **+** asiento `SUSPENSION` con `EffectiveFromUtc/ToUtc` = rango cuando aplica. Todos `"APLICADA"`, `isSystemGenerated: true`, misma transacción (fila #2); los `PublicId` de los asientos quedan en el registro.
- **Revocación** (`ANULADA` desde `APLICADA`, permiso `Authorize*`, motivo obligatorio): registro `Annul(...)` + `personnelAction.Annul()` sobre cada asiento vinculado (aclaración №4), misma transacción. La suspensión revocada sale de la consulta y del insumo (RN-15).
- **Perfil `RETIRADO`** → 422 en altas/ediciones/aplicaciones; solo `RECHAZAR`/anular sobre pendientes (aclaración №10).

### 3.11 Consulta de disponibilidad de tiempos (D-14/RF-013)

**Contrato** (estable desde F1 — aclaración №6):
```json
POST /companies/{companyId}/time-availability/query
{ "startDate": "…", "endDate": "…", "personnelFilePublicId?": …, "categoryCodes?": […], "orgUnitPublicId?": …, "page": 1, "pageSize": 50 }
→ { "rows": [ { "personnelFilePublicId", "employeeName", "employeeCode", "positionPublicId?", "positionName?",
      "categoryCode", "startDate", "endDate", "days", "statusCode", "sourceModule", "referencePublicId" } ],
    "totalCount", "categoryCounts": { "SUSPENSION": n, "FIN_CONTRATO_TEMPORAL": m },
    "activeSources": [ "SUSPENSION", "FIN_CONTRATO_TEMPORAL" ] }
```
- **Fuente 1 — suspensiones**: amonestaciones `APLICADA` vigentes con bloque de suspensión que **intersecta** el rango (fechas reales del registro; `days = SuspensionDays`). Payload mínimo: sin causa, sin hechos, sin montos (P-10).
- **Fuente 2 — fin de contratos temporales**: aclaración №7 (plazas activas + `IsTemporary` + `EndDate ∈ rango`).
- **Fuentes futuras** (documentadas como costura, sin código F1): REQ-001 → `VACACION`, `INCAPACIDAD`, `PERMISO` (lactancia); REQ-002 → `PERMISO` (ausencias compensatorias); módulo futuro de permisos generales. Conectar = método nuevo del repositorio + categoría en `activeSources` + fila en la guía FE.
- **Export** `GET …/time-availability/export?format=…&startDate=…&endDate=…`: `DisponibilidadTiempoExportRow` (Empleado, CodigoEmpleado, Plaza, Categoria, FechaInicio, FechaFin, Dias, Estado, Fuente) — mismas fuentes/filtros.
- Orden: `startDate` asc, desempate empleado; paginado estándar 1-100.

### 3.12 Localización y auditoría

- ~20 códigos nuevos (mapa §5) EN+ES+es-SV con paridad (`BackendMessageLocalizationTests`) + `validation.message.*` por cada `WithMessage` de los ~10 validadores nuevos.
- Auditoría: doble-`SaveChanges` en cada write; decisión con quién/cuándo/nota; anulación/revocación con motivo; snapshots (tipo, causa, concepto, flag de suspensión del tipo); asientos trazables; `ReportExported` en exports (delivery service existente).

---

## 4. Migraciones y seeds

| # | Migración (PR) | Contenido |
|---|---|---|
| M1 (PR-1) | `AddEmployeeRelationsConfiguration` | `CreateTable` × 3 maestros (+índices únicos normalizados filtrados) + seed TPH `personnel-transaction-statuses` (**-9875…-9878**) + `InsertData` 1 ActionType (**`RECONOCIMIENTO=-9879`**, SV) |
| M2 (PR-2) | `AddPersonnelFileRecognitionsAndDisciplinaryActions` | Tablas `personnel_file_recognitions`, `personnel_file_recognition_documents`, `personnel_file_disciplinary_actions`, `personnel_file_disciplinary_action_documents` + índices §3.4 + CHECK constraints |

- **Plantillas de maestros** = datos por tenant (seeder al provisionar + `load-template`), **no** viajan en migraciones (patrón REQ-001).
- **DevSeed** (tenant demo): plantillas cargadas + 1 reconocimiento `APLICADA`, 1 amonestación `APLICADA` con suspensión de 3 días y descuento de $25 sobre concepto egreso demo, 1 amonestación `EN_REVISION` (para probar la bandeja del autorizador), 1 plaza demo con contrato `PLAZO_FIJO` y `EndDate` próximo (para la consulta).
- Verificar IDs libres contra `GlobalCatalogSeedData` al abrir PR-1 (aclaración №12). Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add … -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · guardrail `MigrationSeedingIntegrationTests`.

---

## 5. Mapa de errores (resumen)

| Código | HTTP | Dónde |
|---|---|---|
| `RECOGNITION_TYPE_INVALID` / `DISCIPLINARY_ACTION_TYPE_INVALID` / `DISCIPLINARY_ACTION_CAUSE_INVALID` | 422 | Maestro inexistente/inactivo/de otro tenant (RN-04) |
| `RECOGNITION_TYPE_IN_USE` / `DISCIPLINARY_ACTION_TYPE_IN_USE` / `DISCIPLINARY_ACTION_CAUSE_IN_USE` | 422 | Baja de maestro referenciado por registro activo |
| `DEDUCTION_CONCEPT_INVALID` | 422 | Concepto inexistente/inactivo/no-egreso (en causa, en registro o al aplicar — aclaración №5) |
| `RECOGNITION_EVENT_DATE_IN_FUTURE` / `DISCIPLINARY_ACTION_INCIDENT_DATE_IN_FUTURE` | 422 | Fecha del hecho/falta > hoy (RN-10) |
| `RECOGNITION_AMOUNT_INVALID` | 422 | Monto ≤ 0 o sin moneda (RN-17) |
| `SUSPENSION_NOT_ALLOWED_FOR_TYPE` | 422 | Fechas de suspensión sobre tipo sin `appliesSuspension` (RN-05) |
| `SUSPENSION_DATES_REQUIRED` | 422 | Tipo con flag sin fechas (RN-05) |
| `SUSPENSION_RANGE_INVALID` | 422 | inicio > fin |
| `SUSPENSION_OVERLAP` | 422 | Solape con suspensión vigente del empleado; extensions con el registro en conflicto (RN-18) |
| `DEDUCTION_AMOUNT_REQUIRED` | 422 | `hasPayrollDeduction` sin monto > 0 (RN-06) |
| `RECOGNITION_SELF_APPROVAL_FORBIDDEN` / `DISCIPLINARY_ACTION_SELF_APPROVAL_FORBIDDEN` | 403 | Decide/revoca el sujeto o el registrador (RN-02) |
| `PERSONNEL_TRANSACTION_STATE_RULE_VIOLATION` | 422/409 | Editar/decidir/anular fuera del ciclo (RN-01); segunda decisión concurrente |
| `DECISION_NOTE_REQUIRED` | 422 | Rechazo sin motivo (RN-07) |
| `ANNULMENT_REASON_REQUIRED` | 422 | Anulación/revocación sin motivo (RN-07) |
| `TIME_AVAILABILITY_RANGE_REQUIRED` / `TIME_AVAILABILITY_RANGE_INVALID` | 400/422 | Consulta sin rango / rango incoherente (RF-013) |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` (reuso) | 422 | Altas/ediciones/aplicaciones sobre perfil `RETIRADO` (aclaración №10) |

Reusados: 400/409 de If-Match, 403 de gates, errores de purpose/tamaño/tipo de archivos (config faltante → 422 — memoria del repo), `PERSONNEL_FILE_EXPORT_FORMAT_INVALID`, `REPORT_EXPORT_TOO_LARGE` (413).

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- **`PersonnelTransactionRulesTests`** — golden cases del Anexo A.4 (ratificados) como `[Theory]` bloqueantes:
  - A.4-1/2: transiciones válidas e inválidas; anti-self doble (`IsSelfDecision` con sujeto / registrador / tercero).
  - A.4-3/4: `ValidateSuspensionBlock` (flag↔fechas en las 4 combinaciones); `SuspensionDays(10,12) = 3`; `ValidateDeduction`.
  - A.4-6: `RangesOverlap` — intersección parcial (28→03 vs rango 01-15), contención, disyunción, bordes inclusivos.
  - Estado y snapshot: aplicar congela concepto; rechazar exige nota; anular exige motivo.
- **Dominio**: guards de `Create/Update/Apply/Reject/Annul` en ambas entidades (transiciones, snapshots, publicIds de asientos); `PersonnelFilePersonnelAction.Annul()` (idempotencia negativa: re-anular lanza; token rota); documentos con `FilePublicId` vacío lanzan.
- Validadores; governance (7 policies — `Authorize*` sin Admin verificado); **paridad de localización** (~20 códigos).

**Integración (`tests/CLARIHR.Api.IntegrationTests/ApiIntegrationTests.PersonnelTransactions.cs`):**
- **Maestros/plantilla**: provisioning siembra plantilla; `load-template` 2.ª corrida = 0 cambios y no pisa ediciones; causa con concepto de ingreso → 422; AllowedActions presentes (guardrail governed); tipo en uso no se elimina.
- **Round-trip reconocimiento**: POST `EN_REVISION` (sin asiento) → PATCH decision APLICAR por un **tercero** → 200 + asiento `RECONOCIMIENTO` `IsSystemGenerated` visible en `GET /personnel-actions`; RECHAZAR sin nota → 422; decisión por el registrador → 403; decisión por el propio sujeto (usuario vinculado con permiso) → 403; segunda decisión → 409/422.
- **Round-trip amonestación**: tipo `SUSPENSION_SIN_GOCE` + causa → POST con suspensión 3 días y descuento $25 → aplicar → **2 asientos** (`AMONESTACION` + `SUSPENSION` con vigencias) + snapshot del concepto; suspensión sobre tipo `ESCRITA` → 422; solape con suspensión vigente → 422; descuento sin monto → 422; carrera de aplicación con suspensiones solapadas (`Task.WhenAll`) → exactamente una aplicada (lock, aclaración №3).
- **Revocación**: amonestación aplicada con suspensión revocada con motivo → registro + 2 asientos `ANULADA`; desaparece de consulta e insumo; revocar reconocimiento → asiento `ANULADA`.
- **Autogestión**: empleado vinculado ve **solo** sus `APLICADA` (las `EN_REVISION`/`RECHAZADA` no viajan); otro expediente → 403.
- **RETIRADO**: alta → 422; aplicar pendiente de retirado → 422; rechazar/anular → 200.
- **Adjuntos**: subir PDF/JPG por purpose nuevo → asociar → read-url; purpose ajeno → 422.
- **Bandejas/exports**: queries paginan con `StatusCounts`; export de familia respeta filtros; **insumo de planilla** de un rango cuadra contra las amonestaciones aplicadas (descuentos + suspensiones; revocadas excluidas).
- **Consulta de tiempos**: sin rango → 400; suspensión que intersecta parcialmente aparece con fechas reales; plaza `PLAZO_FIJO` con `EndDate` en rango aparece como `FIN_CONTRATO_TEMPORAL`; plaza `INDEFINIDO` no; plaza cerrada (`IsActive=false`) no; `activeSources` = 2 fuentes F1; payload sin campos sensibles (asersión de shape); sin permiso → 403; export consistente con la query.
- Guardrails existentes verdes: `MigrationSeedingIntegrationTests`, `GeneralCatalogKeyMapGuardrailsTests`, `OpenApiContractGuardrailsIntegrationTests`, `AuthorizationPolicyConvention*`, `AllowedActionsCoverageIntegrationTests`, `BackendMessageLocalizationTests`.

---

## 7. Orden de implementación (PRs sugeridos)

> Rama `feature/otras-transacciones-personal`, creada desde `master` **después del merge de REQ-001 y REQ-002** (D-18). Cada PR lleva sus claves resx y sus tests; convención de commits del repo (sin trailer de co-autoría de IA).

1. **PR-1 — Configuración (M1)** (§3.1/§3.2/§3.3): 3 maestros governed + `EmployeeRelationsTemplateSeeder` + `load-template` + TPH `personnel-transaction-statuses` (`-9875…-9878`) + ActionType `RECONOCIMIENTO` (`-9879`) + 7 permisos/policies/gates (2 `Authorize*` sin Admin) + 2 `FilePurpose` + bloques appsettings base + openapi temprano (contratos de maestros para FE).
2. **PR-2 — Dominio + reglas (M2)** (§3.4/§3.5): 4 entidades nuevas + mutador `Annul()` del asiento + configs EF + índices/CHECKs + `PersonnelTransactionRules` puro con **golden A.4 en verde (gate de la ola)** + batería unitaria de dominio + `IPersonnelTransactionRepository` (solapes + lock).
3. **PR-3 — Reconocimientos end-to-end** (§3.7/§3.8/§3.10): CRUD `EN_REVISION` + decisión con anti-self doble + anulación/revocación (asiento `Annul()`) + sub-recurso documentos + read-url + ficha + rama self (`APLICADA`) + integración completa.
4. **PR-4 — Amonestaciones end-to-end** (§3.7/§3.8/§3.10): espejo + bloque suspensión (solape bajo lock) + bloque descuento (snapshot al aplicar) + doble asiento + revocación integral.
5. **PR-5 — Bandejas + exportaciones + insumo** (§3.9): reporting controller (2 queries + 2 exports de familia + `payroll-input/export`) con rate limits y 413.
6. **PR-6 — Consulta de disponibilidad + cierre** (§3.11): `TimeAvailabilityController` (query + export + `activeSources`) con las 2 fuentes F1 + suite E2E integral + verificación (suites verdes, drift vacío, seeds en BD real) + `openapi.yaml` final + `docs/technical/guia-integracion-frontend-otras-transacciones-personal.md` (incluye: coexistencia con el asiento manual, visibilidad self solo-aplicadas, fuentes activas de la consulta y su degradación, y el insumo de planilla).

> **Gate de la ola**: A.4 en verde en PR-2 (números y transiciones ya ratificados — sin hito externo). Si el negocio adelantara REQ-003 antes de REQ-001, PR-1 extrae la mecánica de plantilla (aclaración №8) y PR-6 sale con 2 fuentes (degradación documentada) — el resto del plan no cambia.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Revocación de asientos (la novedad real).** Primer mutador sobre `PersonnelFilePersonnelAction`. Mitigación: `Annul()` mínimo y custodiado (solo estado + token), invocado únicamente por los handlers de revocación sobre asientos propios (`IsSystemGenerated` + publicId vinculado); test de dominio + integración (revocar → asientos `ANULADA`; el asiento manual ajeno queda intacto).
- **R-T2 — Vínculo registro↔asientos.** Si el publicId del asiento no se persistiera, la revocación no sabría qué anular. Mitigación: `PersonnelActionPublicId`/`SuspensionActionPublicId` como columnas del registro, asignadas en la misma transacción del aplicar; asersión de integración.
- **R-T3 — Carrera de decisión / solape de suspensiones.** Dos autorizadores simultáneos o dos suspensiones solapadas aplicadas a la vez. Mitigación: If-Match + re-verificación de estado en transacción; `pg_advisory_xact_lock` por (tenant, expediente) al aplicar amonestaciones con suspensión (aclaración №3) + test de carrera.
- **R-T4 — Join `IsTemporary` país-scoped.** El cruce plaza→catálogo de contrato es por código normalizado + país del tenant (no FK dura). Mitigación: replicar el criterio de la proyección existente (`PersonnelFileRepository.cs:1503`); test con contrato `INDEFINIDO` (no aparece) y código de contrato nulo (no aparece).
- **R-T5 — Sensibilidad del payload de la consulta.** Riesgo de fuga de detalle disciplinario por la vista de planificación. Mitigación: proyección dedicada sin causa/hechos/montos (P-10) + test de shape + permiso dedicado.
- **R-T6 — Doble conteo con el asiento manual.** Un tenant puede tener asientos manuales `AMONESTACION`/`SUSPENSION` históricos. Mitigación: la consulta y el insumo leen **solo de las entidades** del módulo (RN-16); la guía FE documenta la coexistencia; los asientos del módulo se distinguen por `IsSystemGenerated`.
- **R-T7 — Dependencia del patrón de plantilla (REQ-001 PR-1).** Si REQ-003 se adelantara, el seeder genérico no existiría aún. Mitigación: mecánica idempotente autocontenida en `EmployeeRelationsTemplateSeeder` (contrato idéntico); convergencia posterior si REQ-001 la generaliza.
- **R-T8 — Concepto de egreso inválido al aplicar.** Ventana entre creación y decisión (aclaración №5). Mitigación: re-validación al aplicar → 422 accionable; test.
- **R-T9 — `[ResourceActions]`/`ISupportsAllowedActions`** en los 3 maestros: cada DTO PUT/PATCH lo implementa (solo la integración lo detecta — memoria del repo).
- **R-T10 — 2 purposes nuevos.** Config faltante en un ambiente → 422 en runtime (memoria del repo). Mitigación: bloques en appsettings **base** + checklist de despliegue con contenedores.
- **R-T11 — `dotnet ef`** requiere `DOTNET_ROLL_FORWARD=Major`; nombres de índices/constraints ≤ 63 chars; verificación de seeds al abrir PR-1 (aclaración №12).

---

## 9. Checklist de implementación

- [ ] **Maestros:** 3 entidades + configs EF + controllers governed (`ISupportsAllowedActions` en todos los DTOs) + guards de uso + **plantilla sembrada** (provisioning + `load-template` idempotente; causas sin concepto — P-14).
- [ ] **Catálogos/acciones:** TPH `personnel-transaction-statuses` (`-9875…-9878`) + key map + switch + guardrails + ActionType `RECONOCIMIENTO` (`-9879`) — verificar IDs libres al abrir PR-1 (respetar REQ-002 `-9865…-9871`; trampa `-9490…-9496`).
- [ ] **Permisos:** 7 codes + provisioning (2 `Authorize*` fuera de `CompanyAdminPermissions`) + policies (View authn-only ×2, Manage RequireAssertion ×2, Authorize sin Admin ×2, ViewTimeAvailability estándar) + `Ensure…` fail-closed + gates + governance verde.
- [ ] **Dominio:** `PersonnelFileRecognition` + `PersonnelFileDisciplinaryAction` (snapshots de tipo/causa/flag/concepto; publicIds de asientos) + 2 entidades documento + **`PersonnelFilePersonnelAction.Annul()`** + guards completos + CHECKs e índices (≤ 63 chars).
- [ ] **Reglas:** `PersonnelTransactionRules` (transiciones, anti-self doble, suspensión, días calendario, solape/intersección) + golden A.4 en verde + paridad de localización.
- [ ] **Repositorio:** loaders + `HasOverlappingSuspensionAsync` + lock advisory (no-op en fakes) + bandejas/exports + `GetPayrollInputRowsAsync` + 2 fuentes de disponibilidad.
- [ ] **Reconocimientos:** CRUD `EN_REVISION` + decisión (Authorize, anti-self doble) + anulación/revocación con asiento + adjuntos + rama self solo-`APLICADA`.
- [ ] **Amonestaciones:** espejo + suspensión (flag del tipo, solape bajo lock) + descuento (monto manual USD + concepto snapshot al aplicar) + doble asiento (`AMONESTACION`/`SUSPENSION` con vigencias) + revocación que anula ambos.
- [ ] **Adjuntos:** 2 `FilePurpose` + `Storage:Purposes` en appsettings **base** (PDF/JPG/PNG) + contenedores aprovisionados + sub-recursos + read-url.
- [ ] **Bandejas/exports:** 2 queries con `StatusCounts` + 2 export-rows en español + **insumo de planilla** (efectos DESCUENTO/SUSPENSION del rango, revocadas excluidas) + rate limits + 413.
- [ ] **Consulta de disponibilidad:** query + export con rango obligatorio, payload mínimo, `activeSources[]`, 2 fuentes F1 (suspensiones + `IsTemporary`+`EndDate`), permiso dedicado; costuras de REQ-001/REQ-002 documentadas.
- [ ] **Localización:** ~20 códigos EN/ES/es-SV + paridad + `validation.message.*`.
- [ ] **Pruebas:** unitarias (§6) + `ApiIntegrationTests.PersonnelTransactions.cs` + guardrails existentes verdes + suite completa del repo en verde.
- [ ] **Cierre:** `openapi.yaml` regenerado sin drift · DevSeed actualizado (§4) · checklist de despliegue (migraciones M1-M2, 2 `Storage:Purposes` base + contenedores, `load-template` en el tenant productivo) · `guia-integracion-frontend-otras-transacciones-personal.md`.
