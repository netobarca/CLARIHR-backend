# Plan Técnico de Implementación — Retiro Definitivo de Empleado (Retiros · Entrevista · Reversión)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-retiro-definitivo-empleado.md`](../business/analisis-retiro-definitivo-empleado.md) (v1.1, decisiones **D-01…D-19 ratificadas 2026-07-04**) |
| **Módulos** | `PersonnelFiles` (Employment/Retirements) · `GeneralCatalogs` (country-scoped) · `ExitInterviews` (adaptación del gate + bandeja) · `CompanyUsers`/IAM (desactivación/reactivación de login) · Provisioning (RBAC) · Reporting/Export · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-07-04 |
| **País de referencia** | El Salvador (SV) |
| **Endurecimientos de la ratificación** | D-01 **sin fallbacks ni contrato legado** · D-08 **eliminar retiros legados de prueba** · D-13 **solicitante ≠ autorizador** (403 dedicado) · **ventana de reversión 30 días** (RN-012.4) |

---

## 0. Aclaraciones pre-desarrollo (ratificadas 2026-07-04, según recomendación del desarrollador senior)

1. **Puerta única en ambas direcciones:** mientras el perfil tenga `RetirementDate != null`, el `PUT …/employment-information` se **rechaza completo** (422 `EMPLOYMENT_STATUS_RETIRADO_RESERVED`) — un perfil retirado solo lo tocan la reversión y la recontratación. Evita "des-retirar" manualmente vía cambio de estado.
2. **Semántica de fechas:** `FechaRetiro ≤ hoy` compara **fecha-UTC** (`IDateTimeProvider.UtcNow.Date`); la ventana de reversión es **timestamp exacto**: `ahora ≤ FechaEjecucionUtc + 30 días`. Tolerancia de ±6h respecto de la hora local SV documentada en la guía FE; no se construye infraestructura de timezone por empresa en Fase 1.
3. **Anular una `AUTORIZADA` archiva TODAS las submissions no archivadas** (borrador y enviadas) — mismo tratamiento que la reversión (la baja no ocurrió, no cuenta en rotación). RN-005.3 se implementa con el mecanismo bulk existente.
4. **Último administrador activo:** la ejecución se bloquea con 422 `RETIREMENT_LAST_ADMIN_CONFLICT` si el empleado a retirar es el último admin activo (transferir la administración primero).
5. **`AUTORIZADA` no se edita** (confirmado): cambiar la fecha de retiro tras autorizar = anular → re-registrar → re-autorizar.
6. **Despliegue:** PR-1…PR-8 son aditivos; PR-9 (breaking del PUT + migración de datos destructiva M3) va en el mismo release coordinado con frontend. Se construye TODO (ambas olas) en la rama `feature/retiro-definitivo`.

---

## 1. Objetivo y enfoque

Construir el módulo de **retiro definitivo**: solicitud con ciclo `SOLICITADA → AUTORIZADA → EJECUTADA → REVERTIDA` (+ `RECHAZADA`/`ANULADA`), **ejecución orquestada y transaccional** de la baja (perfil + expediente + cierre de plazas/contratos + desactivación de login + snapshot + acción `BAJA`), **bandeja de entrevistas** de autorizados y **reversión** con restauración exacta desde el snapshot (ventana de 30 días).

**Insight central del análisis de código.** No existe ninguna operación de retiro (la baja hoy es una edición de campos dispersa). El módulo se arma combinando **tres plantillas probadas** más **un precedente de orquestación**:

1. **Entidad + ciclo de solicitud** — `PersonnelFileEconomicAidRequest` (`PersonnelFileEmployee.cs:1643-1822`): estados canónicos + catálogo (híbrido), acciones `PATCH` con `[FromIfMatch]`, anti-auto-aprobación (`EconomicAidRequests.Handlers.cs:371-376`), gates por handler (`PersonnelFileEmployeeHandlerBases.cs:272-403`).
2. **Bandeja empresa + export** — constancias (`CertificateRequestsReportingController` + `ReportExportDeliveryService.CreateFileResultAsync`, contadores por estado, límite síncrono 413, rate-limits `Search`/`Export`).
3. **Snapshot persistido** — patrón "columnas planas + tabla hija" de `ExitInterviewSubmission` (+ `ExitInterviewAnswer`); **no** hay `jsonb` en entidades de dominio (solo en auditoría — `AuditLogConfiguration.cs:57-65`).
4. **Orquestación transaccional** — `RehireEmployee.cs:147-282`: `IUnitOfWork.BeginTransactionAsync` + triple `SaveChanges` (flush intermedio / dominio / auditoría) + commit/rollback manual. La ejecución y la reversión replican esta forma.

**Las dos piezas sin plantilla directa** (foco de riesgo, §8):
- **Reapertura de plazas/contratos** (reversión): `Close(endDate)` existe (`PersonnelFileEmployee.cs:262-267, 345-350`) pero **no hay `Reopen()`** — se añade como método de dominio. Verificado: **no existe ningún índice único filtrado** sobre asignaciones/contratos que la reapertura pueda violar (la invariante "única primaria activa" vive solo en `EmploymentAssignmentRules`).
- **Desactivación/reactivación de login dentro de la transacción**: se extrae el núcleo de `DeactivateCompanyUser.cs:89-104` / `ReactivateCompanyUser.cs:81-89` a un servicio compartido (mismo movimiento que hizo rehire con `PersonnelFileFinalizationService`).

**Decisiones ratificadas que gobiernan el diseño**: puerta única sin fallbacks (D-01) · solicitante = expediente + snapshot (D-02) · sin autoservicio (D-03) · ciclo híbrido canónico+catálogo (D-04) · ejecución manual con `FechaRetiro ≤ hoy` (D-05) · cierre de plazas/contratos + login (D-06) · entrevista desde `AUTORIZADA` (D-07) · reversión solo de retiros del módulo + borrar legados (D-08) · archivar entrevista al revertir (D-09) · bloqueo por rehire posterior (D-10) · snapshot mínimo (D-11) · 4 permisos, Authorize/Revert **no** implicados por Admin (D-12) · sujeto ≠ actor **y solicitante ≠ autorizador** (D-13) · sin liquidación (D-14) · acciones `BAJA`/`REVERSION_BAJA` + fix `COMPLETADA` (D-15) · catálogo `retirement-request-statuses` (D-16) · sin notificaciones (D-17) · bloqueo de rehire en la ejecución (D-18) · sin adjuntos (D-19) · **ventana de reversión 30 días** (RN-012.4).

---

## 2. Línea base verificada en el código (qué se reutiliza / qué se toca)

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Plantilla de solicitud | `PersonnelFileEconomicAidRequest` + `EconomicAidRequestStatuses` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:1620-1822`); handlers + anti-self (`…/Compensation/EconomicAidRequests.Handlers.cs:371-376`); EF config con índices `tenant_file_status`/`tenant_file_date` | Espejo casi 1:1 de entidad, CQRS, controller y validadores. |
| 2 | Bandeja + export | `CertificateRequestsReportingController.cs:16-21` (comentario: **sin** `[AuthorizationPolicySet]`, gate por handler), `:30` query, `:66` export; repo `FilteredCertificateRequests`/`QueryCertificateRequestsAsync`/`GetCertificateRequestExportRowsAsync` (`PersonnelFileEmployeeRepository.cs:2861-3039`); `ReportExportDeliveryService.cs:49-84` (resourceKey literal, 413); rate-limits `Program.cs:288-292` | Clonar para `retirement-requests` (query + export + contadores). |
| 3 | Orquestación transaccional | `RehireEmployee.cs:147-282` (BeginTransaction → cerrar periodo → mutar → Save → audit → Save → Commit); rollback en cada rama de fallo | Forma exacta de `Execute` y `Revert`. |
| 4 | Cierre de plazas/contratos | Repo `CloseActiveEmploymentAssignmentsAsync`/`CloseActiveContractHistoriesAsync` (`PersonnelFileEmployeeRepository.cs:205-248`); dominio `Close(endDate)` = `EndDate` + `IsActive=false` (`PersonnelFileEmployee.cs:262-267, 345-350`) | La ejecución necesita variantes **con captura** (qué filas cerró y su `EndDate` previo) para el snapshot. |
| 5 | Reapertura (inexistente) | Sin método `Reopen()`; sin índices únicos filtrados en `personnel_file_employment_assignments`/`personnel_file_contract_histories` (`PersonnelFileEmployeeConfiguration.cs:53-132`; solo `uq__public_id` + checks de fechas) | Añadir `Reopen(previousEndDate)` de dominio; la BD no estorba; respetar regla de única-primaria (aplicación). |
| 6 | Login (IAM) | `DeactivateCompanyUser.cs:71-104` (`user.Deactivate()`→`Status=Inactive`, membership, `iamUser.SetActive(false)`, revoca refresh tokens, invariante **último admin** `:71-73`); `ReactivateCompanyUser.cs:81-89`; reuso en provisioning `CompanyUserProvisioningService.cs:74-95`; **sin transacción propia** (un `SaveChanges`) | Extraer núcleo a servicio compartido invocable dentro de la transacción de ejecución/reversión. |
| 7 | Link de login | `PersonnelFile.LinkedUserPublicId` (`PersonnelFile.cs:120`) con índice único filtrado `uq_personnel_files__tenant_linked_user` (`PersonnelFileConfiguration.cs:81-84`) | La ejecución **NO** limpia el link (solo desactiva el usuario) — así la reversión restaura sin re-vincular. Solo el rehire limpia (`ReopenForRehire`, `PersonnelFile.cs:899-915`). |
| 8 | Vía legada a cerrar (D-01) | PUT `…/employment-information`: request `PersonnelFileRequests.cs:115-125`; command `EmployeeProfiles.cs:77-90`; mapeo controller `PersonnelFileEmploymentController.cs:201-204`; validación jerárquica en handler `EmployeeProfiles.cs:191-202`; dominio `PersonnelFileEmployeeProfile.Create/Update` (`PersonnelFileEmployee.cs:61-96`); repo upsert `PersonnelFileEmployeeRepository.cs:31-38` | Quitar los 4 campos `Retirement*` de TODO el camino de escritura; el **response/GET conserva** los campos (`Map` `:2216-2232`). Rechazar `EmploymentStatusCode=RETIRADO` (hoy se valida contra catálogo en `EmployeeProfiles.cs:184-189`). |
| 9 | PATCH raíz (isActive/bloqueo) | Appliers `PersonnelFileCore.PatchAppliers.cs:194-212`; handler `PersonnelFileCore.Handlers.cs:456-487`; dominio `BlockRehire/ClearRehireBlock` (`PersonnelFile.cs:878-890`) | Se conserva como capacidad administrativa (RN-015.1). La ejecución aplica `Inactivate()` + bloqueo opcional por sí misma (D-18). |
| 10 | Gate de entrevista (punto único) | `GetSubmissionSnapshotAsync` (`ExitInterviewRepository.cs:412-445`) + record `ExitInterviewSubmissionSnapshot` (`IExitInterviewRepository.cs:21-26`); consumidores: SAVE `ExitInterviewSubmissions.Handlers.cs:107-119` y GET `:47-55`; `Period` `:138`; formulario por motivo `GetActiveFormForReasonAsync` (`ExitInterviewRepository.cs:244-258`) | **Re-apuntar SOLO el snapshot** a la solicitud vigente; ambos handlers quedan intactos si el record conserva su shape. Sin fallback al perfil (D-01/D-08). |
| 11 | Archivado de entrevista | `ArchiveSubmissionsForFileAsync` (`ExitInterviewRepository.cs:447-454`, bulk `Status=Archived`); único llamador hoy: `RehireEmployee.cs:185`; `Archive()` sin inverso (`ExitInterviewSubmission.cs:156-160`) | Reusar en reversión (D-09) y en anulación de `AUTORIZADA` (RN-005.3). |
| 12 | Acciones de personal | Factory `PersonnelFilePersonnelAction.Create(...)` (`PersonnelFileEmployee.cs:573-584`); journaling sistema en `RehireEmployee.cs:247-260`; **bug**: const `"COMPLETADA"` (`RehireEmployee.cs:80`) no sembrada en `ActionStatus` (`GlobalCatalogSeedData.cs:729-738`) | Journal `BAJA`/`REVERSION_BAJA` con estado **`APLICADA`** (sembrado, `-9495`); corregir la const del rehire + data-fix (D-15). |
| 13 | Catálogos de retiro (existentes) | Categorías `-9200…-9207` + motivos `-9220…-9242` (`GlobalCatalogSeedData.cs:290-337`); jerarquía por **FK long** `RetirementCategoryCatalogItemId` (`RetirementCatalogItems.cs:78`); validación `ValidateRetirementCodesAsync` (`PersonnelReferenceCatalogs.cs:352-415`) | Reusar tal cual para validar categoría/motivo de la solicitud. |
| 14 | Receta de catálogo nuevo | Subclase de `GeneralCatalogItem` (p. ej. `CertificateRequestStatusCatalogItem`, `GeneralCatalogItems.cs:421`) + const en `PersonnelCurriculumCatalogCategories` (`PersonnelReferenceCatalogs.cs:80-125`) + wire-key en `GeneralCatalogKeyMap.CatalogKeys` (`GeneralCatalogKeyMap.cs:21-67`) + switch `CatalogCodeIsActiveAsync` (`PersonnelFileRepository.cs:1560-1597`) + config con índice acortado (63 chars, `GeneralCatalogItemConfiguration.cs:502-519`) + seed `GlobalCatalogSeedData` | Replicar 1× para `retirement-request-statuses`. Guardrail de biyección: `GeneralCatalogKeyMapGuardrailsTests`. |
| 15 | IDs de seed libres | Más negativo en uso: **-9804** (`GlobalCatalogSeedData.cs:964`); ActionType ocupa `-9470…-9481` | Estados de solicitud: **-9810…-9815**; tipos de acción `BAJA=-9482`, `REVERSION_BAJA=-9483`. |
| 16 | Permisos (receta 8 archivos) | Codes `PersonnelFileCommon.cs:82-209`; seed `ProvisioningConstants.cs:33-87`; policies `PersonnelFilePolicies.cs:14-138` + `Program.cs:439-547`; gates `PersonnelFileAuthorizationService.cs` (patrón `EnsureHasAnyClaimAsync` `:277-322`); **Admin excluido** en `HasRehireAuthorizationAsync:244-251`; governance HashSet `AuthorizationPolicyConventionGovernanceTests.cs:81-122` + regex de familia `:56-57` | 4 permisos nuevos; `AuthorizeRetirement`/`RevertRetirement` copian la exclusión de Admin. Controllers nuevos NO empiezan con `PersonnelFile` (evita el marcador forzoso del regex; se declara igualmente donde aplica). |
| 17 | Reloj / If-Match / auditoría | `IDateTimeProvider.UtcNow`; `[FromIfMatch]` (`Api.Common/Binders/FromIfMatchAttribute.cs`; ejemplo `CertificateRequestsController.cs:164-189`); patrón `PersonnelFileEmployeeAudits.LogUpdateAsync` + doble `SaveChanges` (`EconomicAidRequests.Handlers.cs:123-135`) | Convenciones obligatorias en todos los handlers nuevos. |
| 18 | Localización | `BackendMessages.resx`/`.es.resx`(+`.es-SV`); paridad `BackendMessageLocalizationTests` (códigos `:64`, validation-messages `:86`, `BuildValidationKey:224-235`) | ~18 códigos nuevos EN+ES **en el mismo PR que los introduce** (el test corre por PR). |
| 19 | Tests existentes afectados | `ApiIntegrationTests.Rehire.cs:88-95` (siembra la baja por la vía legada), `PersonnelFileEmployeeProfileEmailChangeTests.cs:133-142`, `PersonnelFileEmployeeProfileQueryTests.cs:134-137`. **No existe ningún test del gate de submissions** (cero regresión al re-apuntarlo) | PR-9 los actualiza; el de rehire pasa a sembrar la baja **ejecutando el módulo** (E2E más realista). |
| 20 | Wire | `PublicContractNaming.cs:162-185` (Guid `XxxId`→`xxxPublicId`); nunca exponer PK/FK `long` | Los `…ByUserId`/`…FilePublicId` del response son seguros; el solicitante se referencia por `PublicId` (patrón `PositionSlotPublicId`). |

---

## 3. Arquitectura de la solución

### 3.1 Catálogo `retirement-request-statuses` + tipos de acción (D-15/D-16)

**Nueva subclase** `RetirementRequestStatusCatalogItem : GeneralCatalogItem` (en `GeneralCatalogs/GeneralCatalogItems.cs`, junto a `CertificateRequestStatusCatalogItem:421`). Cableado (receta #14):

1. Const `PersonnelCurriculumCatalogCategories.RetirementRequestStatus = "RetirementRequestStatus"` (`PersonnelReferenceCatalogs.cs:80-125`).
2. Wire-key `["retirement-request-statuses"]` en `GeneralCatalogKeyMap.CatalogKeys` (`GeneralCatalogKeyMap.cs:21-67`) → se sirve por `GET api/v1/general-catalogs/retirement-request-statuses`.
3. Switch `CatalogCodeIsActiveAsync`: caso `"RETIREMENTREQUESTSTATUS"` (`PersonnelFileRepository.cs:1560-1597`). (No requiere `GetCatalogItemNameAsync`: el estado no se snapshotea por nombre.)
4. EF config `RetirementRequestStatusCatalogItemConfiguration` → tabla `retirement_request_status_catalog_items`, **nombre de índice acortado** pasado al ctor base (límite 63 chars, patrón `GeneralCatalogItemConfiguration.cs:502-519`): `ix_retirement_request_status_catalog_items__active_sort`.
5. Seed SV en `GlobalCatalogSeedData` (helper `CreateGeneralCatalogSeed`), IDs **-9810…-9815**:

| ID | Code | Name | Sort |
|---|---|---|---|
| -9810 | `SOLICITADA` | Solicitada | 10 |
| -9811 | `AUTORIZADA` | Autorizada | 20 |
| -9812 | `RECHAZADA` | Rechazada | 30 |
| -9813 | `ANULADA` | Anulada | 40 |
| -9814 | `EJECUTADA` | Ejecutada | 50 |
| -9815 | `REVERTIDA` | Revertida | 60 |

**Tipos de acción** (seed `ACTION_TYPE_CATALOG`, contiguos al bloque `-9470…-9481`): `BAJA = -9482`, `REVERSION_BAJA = -9483`.

**Fix D-15**: `RehireEmployee.cs:80` cambia `"COMPLETADA"` → `"APLICADA"` (sembrado `-9495`); la migración de este PR incluye el data-fix `UPDATE personnel_file_personnel_actions SET action_status_code='APLICADA' WHERE action_status_code='COMPLETADA'`.

### 3.2 Permisos, políticas y gates (D-12/D-13)

Receta de 8 archivos (línea base #16):

| Pieza | Contenido |
|---|---|
| `PersonnelFilePermissionCodes` (`PersonnelFileCommon.cs`) | `ViewRetirements = "PersonnelFiles.ViewRetirements"`, `ManageRetirements`, `AuthorizeRetirement`, `RevertRetirement` |
| `ProvisioningConstants.CompanyAdminPermissions` (`:33-87`) | 4 entradas `ProvisioningPermissionDefinition` (Module/Screen `PersonnelFiles`) |
| `PersonnelFilePolicies` (`:14-138`) | 4 policy names nuevos |
| `Program.cs` (`:439-547`) | `AddPolicy` × 4. **View/Manage**: patrón estándar (claim propio ∨ `Admin` ∨ `iam.administration.manage`). **AuthorizeRetirement / RevertRetirement**: assertion SOLO {claim propio, `iam.administration.manage`} — **Admin deliberadamente excluido** (espejo de `HasRehireAuthorizationAsync`, `PersonnelFileAuthorizationService.cs:244-251`) |
| `IPersonnelFileAuthorizationService` + impl | 4 gates fail-closed: `EnsureCanViewRetirementsAsync`, `EnsureCanManageRetirementsAsync`, `EnsureCanAuthorizeRetirementAsync` (sin Admin), `EnsureCanRevertRetirementAsync` (sin Admin) + `EnsureCanViewRetirementInterviewTrayAsync` (acepta `ViewExitInterviews` ∨ `ViewRetirements` ∨ Admin ∨ MA — RN-008.1) |
| Governance test (`AuthorizationPolicyConventionGovernanceTests.cs:81-122`) | Añadir los 4 policy names al HashSet `PersonnelFilePolicyNames` |
| Bases de handler (`PersonnelFileEmployeeHandlerBases.cs`) | `LoadForManageRetirementsAsync` / `LoadForViewRetirementsAsync` (espejo de los `LoadFor…EconomicAid…` `:272-403`, **sin** rama self-service: D-03 = sin autoservicio) |
| Resx | — (los permisos no generan mensajes) |

**Separación de funciones (dos capas, en los handlers de acción):**
- **Sujeto ≠ actor** (autorizar/ejecutar/revertir): `personnelFile.LinkedUserPublicId == currentUserId` → 403 `RETIREMENT_SELF_ACTION_FORBIDDEN` (patrón `EconomicAidRequests.Handlers.cs:371-376`).
- **Solicitante ≠ autorizador** (solo resolución/anulación de autorizada): cargar el expediente solicitante por `RequesterFilePublicId` y comparar su `LinkedUserPublicId` con el usuario actual → 403 `RETIREMENT_REQUESTER_CANNOT_AUTHORIZE`. (Si el expediente solicitante no tiene login vinculado, la regla no puede dispararse — se documenta como comportamiento esperado: la desigualdad se evalúa sobre identidades de login.)

### 3.3 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs`

#### 3.3.1 `RetirementRequestStatuses` (códigos canónicos, patrón `EconomicAidRequestStatuses:1620-1635`)

```csharp
public static class RetirementRequestStatuses
{
    public const string Solicitada = "SOLICITADA";
    public const string Autorizada = "AUTORIZADA";
    public const string Rechazada  = "RECHAZADA";
    public const string Anulada    = "ANULADA";
    public const string Ejecutada  = "EJECUTADA";
    public const string Revertida  = "REVERTIDA";

    public static readonly IReadOnlyCollection<string> Open = [Solicitada, Autorizada];          // RN-001.2
    public static readonly IReadOnlyCollection<string> ResolutionTargets = [Autorizada, Rechazada];
}
```

#### 3.3.2 Entidad `PersonnelFileRetirementRequest : TenantEntity`

Campos (todos `{ get; private set; }`, espejo estructural de `PersonnelFileEconomicAidRequest:1643-1704`):

| Grupo | Campos |
|---|---|
| Identidad/relación | `PersonnelFileId (long)` + nav `PersonnelFile`; `PublicId`; `TenantId` |
| Solicitante (D-02) | `RequesterFilePublicId (Guid)` — referencia por PublicId, patrón `PositionSlotPublicId` (sin FK long); `RequesterNameSnapshot (string)`; `RequestedByUserId (Guid)` — quién registró en el sistema |
| Datos de negocio | `RequestDate (DateTime)`, `RetirementDate (DateTime)`, `RetirementCategoryCode`, `RetirementCategoryNameSnapshot`, `RetirementReasonCode`, `RetirementReasonNameSnapshot`, `Notes (≤2000, opcional)` |
| Ciclo | `RequestStatusCode` (canónico+catálogo); `ResolvedByUserId?/ResolutionDateUtc?/ResolutionNotes?`; `ExecutedByUserId?/ExecutionDateUtc?`; `RevertedByUserId?/ReversalDateUtc?/ReversalReason?` |
| Snapshot de ejecución (D-11, columnas planas) | `PriorEmploymentStatusCode (string?)`, `PriorLoginWasActive (bool?)` (`null` = sin login vinculado), `PriorRehireBlocked (bool?)`, `PriorRehireBlockReason (string?)` |
| Infra | `IsActive`, `ConcurrencyToken` |

Métodos de dominio (guards → `InvalidOperationException`, como EconomicAid `:1741-1801`):
- `Create(...)` → nace `SOLICITADA`.
- `Update(...)` — **solo** en `SOLICITADA` (RN-003.1).
- `Resolve(targetStatusCode, decidedByUserId, decidedAtUtc, notes)` — solo desde `SOLICITADA`; target ∈ `ResolutionTargets`; **rechazo exige `notes`** (RN-004.3).
- `Cancel(canceledByUserId, canceledAtUtc, notes?)` — desde `SOLICITADA` o `AUTORIZADA` → `ANULADA` (la distinción de permiso por estado se hace en el handler, RN-005.1).
- `MarkExecuted(executedByUserId, executedAtUtc, priorStatusCode, priorLoginWasActive, priorRehireBlocked, priorRehireBlockReason)` — solo desde `AUTORIZADA` → `EJECUTADA` + persiste el snapshot.
- `MarkReverted(revertedByUserId, revertedAtUtc, reason)` — solo desde `EJECUTADA` → `REVERTIDA`; `reason` obligatorio (RN-010.3).
- `SetActive(bool)` / `BindToPersonnelFile(long)` (infra estándar).

#### 3.3.3 Tabla hija del snapshot — `RetirementRequestClosedRecord`

Precedente "columnas planas + tabla hija" de `ExitInterviewSubmission`/`ExitInterviewAnswer`. Registra **exactamente** qué cerró la ejecución para reabrirlo (D-11):

| Campo | Tipo | Nota |
|---|---|---|
| `RetirementRequestId (long)` | FK cascade | |
| `EntityKind (string)` | `"ASSIGNMENT"` \| `"CONTRACT"` | |
| `EntityPublicId (Guid)` | PublicId de la fila cerrada | |
| `PreviousEndDate (DateTime?)` | `EndDate`/`ContractEndDate` que la fila tenía ANTES de cerrar | Clave: `Close()` preserva un `EndDate` ya fijado (repo `:205-248` hace `SetActive(false)` en ese caso). La reversión restaura `EndDate = PreviousEndDate` + `IsActive = true` — restauración exacta, no asume `null`. |

#### 3.3.4 Métodos nuevos en entidades existentes

| Entidad | Método nuevo | Contenido |
|---|---|---|
| `PersonnelFileEmployeeProfile` (`:5-97`) | `ApplyRetirement(categoryCode, reasonCode, notes, retirementDate)` | Setea los 4 campos + `EmploymentStatusCode = "RETIRADO"`; **única vía de escritura** de la baja (D-01). Guard: `retirementDate >= HireDate` (RF-016). |
| ídem | `ClearRetirement(restoreStatusCode)` | Limpia los 4 campos + restaura el estado laboral del snapshot (no asume `ACTIVO`, D-11). |
| ídem | `Create/Update` **pierden** los 4 parámetros `Retirement*` (`:61-96`) | El PUT legado ya no puede escribirlos (D-01, §3.11). |
| `PersonnelFileEmploymentAssignment` (`:99-268`) | `Reopen(DateTime? previousEndDate)` | `EndDate = previousEndDate; IsActive = true;` refresh token. No toca `IsPrimary` (el cierre tampoco lo tocó → restauración simétrica). |
| `PersonnelFileContractHistory` (`:270-351`) | `Reopen(DateTime? previousContractEndDate)` | Ídem sobre `ContractEndDate`. |

### 3.4 Módulo de reglas puro — `Features/PersonnelFiles/Retirements/RetirementRequest.Rules.cs`

Patrón `EconomicAidRequest.Rules.cs` (errores + funciones puras sin BD, testeables):

- `RetirementErrors` — catálogo de `Error` (§5).
- `RetirementRequestRules`:
  - `IsEligibleForRequest(recordType, lifecycleStatus, fileIsActive, profileRetirementDate)` — `Employee`+`Completed`, activo, `RetirementDate == null` (RN-001.1).
  - `AreDatesCoherent(requestDate, retirementDate, hireDate, asOfUtc)` — `RequestDate ≤ hoy` ∧ `RetirementDate ≥ HireDate` (RN-001.4/RF-016).
  - `IsExecutableOn(retirementDate, asOfUtc)` — `RetirementDate ≤ hoy` (D-05).
  - `IsWithinReversalWindow(executionDateUtc, asOfUtc)` — `asOfUtc ≤ executionDateUtc + 30 días` calendario (**RN-012.4 ratificada**).
  - `HasClosingBlockers(activeAssignmentStartDates, retirementDate)` — ninguna fila activa con `StartDate > RetirementDate` (protege los check constraints `end_date >= start_date` / `contract_end_date >= contract_date`, §8 R-T5).

El reloj SIEMPRE entra por parámetro `asOfUtc` (`IDateTimeProvider.UtcNow` en el handler — nunca `DateTime.UtcNow`).

### 3.5 Aplicación — intake (RF-001…RF-003, RF-005 parcial)

`Features/PersonnelFiles/Retirements/RetirementRequests.cs` + `.Handlers.cs` (espejo de `EconomicAidRequests.cs`):

- **Commands/Queries**: `AddPersonnelFileRetirementRequestCommand`, `UpdatePersonnelFileRetirementRequestCommand`, `CancelRetirementRequestCommand`, `GetPersonnelFileRetirementRequestsQuery`, `GetPersonnelFileRetirementRequestByIdQuery`. **Sin DELETE**: el registro nunca se elimina (RN-010.4); la salida es `ANULADA`.
- **Validadores**: código categoría/motivo `MaxLength(80)`; `Notes ≤ 2000`; fechas requeridas; `RequesterFilePublicId` requerido.
- **Handler de alta** (gates `LoadForManageRetirementsAsync` — D-03 sin autoservicio):
  1. Elegibilidad del empleado (reglas §3.4) → 422 `RETIREMENT_REQUEST_EMPLOYEE_NOT_ELIGIBLE`.
  2. **Única solicitud abierta**: repo `HasOpenRetirementRequestAsync(fileId)` → 422 `RETIREMENT_REQUEST_ALREADY_OPEN` (+ respaldo en BD, §3.12).
  3. **Solicitante**: repo resuelve el expediente por `RequesterFilePublicId` dentro del tenant (puede ser el mismo empleado — renuncia); si no existe/inactivo → 422 `RETIREMENT_REQUEST_REQUESTER_INVALID`; snapshot del nombre.
  4. Catálogos: `ValidateRetirementCodesAsync` (existente, `PersonnelReferenceCatalogs.cs:352-415`) + snapshots de nombre vía nuevo `GetRetirementCatalogNamesAsync(companyId, categoryCode, reasonCode)` (espejo interno de `ReferenceRetirementReasonBelongsToCategoryAsync`, `PersonnelFileRepository.cs:1713-1735`).
  5. Fechas coherentes → 422 `RETIREMENT_REQUEST_DATE_INCOHERENT`.
  6. Persistir + transacción/auditoría patrón `EconomicAidRequests.Handlers.cs:123-135`.
- **Editar**: solo `SOLICITADA` (dominio lanza → 422 `RETIREMENT_REQUEST_STATE_RULE_VIOLATION`); mismas validaciones; `If-Match`.
- **Anular** (`PATCH …/cancel`): si el estado es `SOLICITADA` exige `ManageRetirements`; si es `AUTORIZADA` exige `AuthorizeRetirement` (RN-005.1 — el handler bifurca el gate por estado); al anular una `AUTORIZADA`, archiva la submission de entrevista en borrador si existe (RN-005.3, `ArchiveSubmissionsForFileAsync`); `EJECUTADA` → 422 indicando reversión (E-09).

**Repo (`IPersonnelFileEmployeeRepository`)**: `AddRetirementRequestAsync`, `UpdateRetirementRequestAsync`, `GetRetirementRequestsAsync(filePublicId)`, `GetRetirementRequestAsync(filePublicId, requestPublicId)`, `GetRetirementRequestEntityAsync`, `HasOpenRetirementRequestAsync`, `GetLatestRetirementRequestForInterviewAsync` (§3.9), `QueryRetirementRequestsAsync`/`GetRetirementRequestExportRowsAsync` (§3.10), `GetRetirementInterviewTrayAsync` (§3.9).

### 3.6 Resolución — autorizar / rechazar (RF-004) + anulación de autorizada

Endpoint de acción `PATCH …/resolution` con `[FromIfMatch]` (patrón `SalaryTabulator`/EconomicAid):

1. Gate `EnsureCanAuthorizeRetirementAsync` (Admin excluido — D-12).
2. Solo desde `SOLICITADA`; target ∈ {`AUTORIZADA`, `RECHAZADA`}; rechazo exige nota → 422 `RETIREMENT_RESOLUTION_NOTES_REQUIRED`.
3. **Anti-auto-gestión**: sujeto ≠ actor → 403 `RETIREMENT_SELF_ACTION_FORBIDDEN`.
4. **Solicitante ≠ autorizador (D-13 ratificada)** → 403 `RETIREMENT_REQUESTER_CANNOT_AUTHORIZE` (§3.2).
5. Re-verifica elegibilidad del empleado (RN-004.4) → 422 estado-conflicto si divergió.
6. `request.Resolve(...)` + auditoría.

Al autorizar, el empleado aparece en la bandeja de entrevistas (§3.9) — no hay efecto adicional sobre el expediente (los efectos ocurren en la ejecución).

### 3.7 Ejecución orquestada de la baja (RF-006/RF-007, D-05/D-06/D-18) — el corazón del módulo

`ExecuteRetirementRequestCommand` (`PATCH …/execution`, `[FromIfMatch]`, body opcional `{ blockRehire: bool, rehireBlockReason?: string }` — D-18). Handler espejo estructural de `RehireEmployee.cs:82-289`:

```
PATCH …/retirement-requests/{id}/execution   (If-Match; ManageRetirements)
        │
[1] LoadForManageRetirementsAsync (tenant + gate)
[2] Guards previos:
      estado == AUTORIZADA                     → 422 STATE_RULE_VIOLATION
      RetirementDate ≤ hoy (IDateTimeProvider) → 422 EXECUTION_DATE_NOT_REACHED
      sujeto ≠ ejecutor                        → 403 SELF_ACTION_FORBIDDEN
      perfil existe ∧ RetirementDate == null   → 422 EXECUTION_STATE_CONFLICT
      sin filas activas con StartDate > FechaRetiro → 422 DATE_INCOHERENT (R-T5)
      si hay login vinculado ∧ es el último admin activo → 422 LAST_ADMIN_CONFLICT
        │
        ▼  ──────────────── BEGIN TX (IUnitOfWork) ────────────────
[3] Capturar snapshot: PriorEmploymentStatusCode (perfil), PriorLoginWasActive
    (IUserRepository por LinkedUserPublicId; null si no hay), PriorRehireBlocked/Reason (file)
[4] profile.ApplyRetirement(category, reason, notes, RetirementDate)   → + RETIRADO
[5] personnelFile.Inactivate();  si blockRehire → BlockRehire(reason)  (D-18)
[6] Cerrar CON CAPTURA (repo nuevo):
      CloseActiveEmploymentAssignmentsCapturingAsync(fileId, tenant, RetirementDate)
      CloseActiveContractHistoriesCapturingAsync(fileId, tenant, RetirementDate)
      → List<(EntityPublicId, PreviousEndDate)> → filas RetirementRequestClosedRecord
[7] Desactivar login (si existe): ICompanyUserLifecycleService.DeactivateCoreAsync
      (núcleo extraído de DeactivateCompanyUser.cs:89-104: user.Deactivate() +
       membership.Deactivate() + iamUser.SetActive(false) + revocar refresh tokens;
       SIN authz/ETag/audit propios — el handler de retiro es dueño de la unidad de trabajo)
      ⚠ NO limpiar LinkedUserPublicId (línea base #7)
[8] PersonnelFilePersonnelAction.Create("BAJA", "APLICADA", actionDate: hoy,
      effectiveFromUtc: RetirementDate, isSystemGenerated: true)          (D-15)
[9] request.MarkExecuted(executor, hoy, snapshot…)
        │  SaveChanges → PersonnelFileEmployeeAudits.LogUpdateAsync(before/after) → SaveChanges
        ▼  ──────────────── COMMIT ─────────────────
   200 OK + nuevo ETag  (respuesta incluye el resumen de efectos aplicados)
```

**Refactor IAM (paso [7])**: nuevo `ICompanyUserLifecycleService` en `Features/CompanyUsers/` con `DeactivateCoreAsync(userPublicId)` y `ReactivateCoreAsync(userPublicId)`; `DeactivateCompanyUser.cs`/`ReactivateCompanyUser.cs` se refactorizan para delegarle el núcleo **conservando** su autorización RBAC, weak-ETag, invariante último-admin y auditoría propias (cero cambio de comportamiento — mismo movimiento que `PersonnelFileFinalizationService`). La invariante de último admin se re-usa en el guard [2] (`IsLastActiveAdministratorAsync`, `DeactivateCompanyUser.cs:71-73`).

### 3.8 Reversión (RF-010…RF-012, D-09/D-10/D-11 + ventana 30 días) — Ola 2

`RevertRetirementRequestCommand` (`PATCH …/reversal`, `[FromIfMatch]`, body `{ reason: string }` obligatorio):

```
PATCH …/retirement-requests/{id}/reversal   (If-Match; RevertRetirement — Admin NO)
        │
[1] EnsureCanRevertRetirementAsync
[2] Guards (RN-011 — cuatro bloqueos + básicos):
      estado == EJECUTADA                          → 422 STATE_RULE_VIOLATION
      reason presente                              → 422 REVERSAL_REASON_REQUIRED
      sujeto ≠ revertidor                          → 403 SELF_ACTION_FORBIDDEN
      dentro de 30 días desde ExecutionDateUtc     → 422 REVERSAL_WINDOW_EXPIRED   (RN-012.4)
      sin RECONTRATACION posterior a la ejecución
        (acción journaleada > ExecutionDateUtc)    → 422 REVERSAL_BLOCKED_BY_REHIRE (D-10)
      es la solicitud EJECUTADA más reciente       → 422 REVERSAL_NOT_MOST_RECENT
      estado no divergido: perfil.RetirementDate == request.RetirementDate ∧
        motivo/categoría coinciden ∧ file.IsActive == false ∧ status == RETIRADO
                                                   → 422 REVERSAL_STATE_DIVERGED   (RN-012.2)
        │
        ▼  ──────────────── BEGIN TX ────────────────
[3] profile.ClearRetirement(PriorEmploymentStatusCode)      (restaura, no asume ACTIVO)
[4] personnelFile.Activate(); restaurar bloqueo: PriorRehireBlocked
      ? BlockRehire(PriorRehireBlockReason) : ClearRehireBlock()
[5] Reabrir EXACTAMENTE lo cerrado (RetirementRequestClosedRecord):
      assignment.Reopen(PreviousEndDate) / contract.Reopen(PreviousEndDate)
      (defensivo: como no hubo rehire, no existen filas nuevas → la regla de
       única-primaria se restaura sola; verificación barata en handler)
[6] Reactivar login SOLO si PriorLoginWasActive == true:
      ICompanyUserLifecycleService.ReactivateCoreAsync   (E-16: si estaba
      desactivado antes de la baja, permanece desactivado)
[7] ArchiveSubmissionsForFileAsync (entrevista, D-09 — mismo mecanismo que
      RehireEmployee.cs:185; no hay des-archivado: ExitInterviewSubmission.cs:156-160)
[8] PersonnelFilePersonnelAction.Create("REVERSION_BAJA", "APLICADA", …, system)
[9] request.MarkReverted(revertidor, hoy, reason)
        │  SaveChanges → Audit → SaveChanges
        ▼  ──────────────── COMMIT ─────────────────
   200 OK + ETag  (tras revertir, el empleado es elegible para una NUEVA solicitud — RN-010.5;
   la antigüedad vuelve a correr CONTINUA porque HireDate nunca cambió — RN-13)
```

### 3.9 Integración con la entrevista (RF-008/RF-009, D-07)

**RF-009 — re-apuntar el gate (diff mínimo).** El acople es un punto único: `GetSubmissionSnapshotAsync` (`ExitInterviewRepository.cs:412-445`). Cambio:

- Resuelve la **solicitud de retiro vigente** del expediente (`RequestStatusCode ∈ {AUTORIZADA, EJECUTADA}`, la más reciente) y de ella toma `RetirementReasonCode`, `RetirementCategoryCode` y `RetirementDate`. **Sin fallback al perfil** (D-01/D-08 ratificadas: la solicitud es la única fuente).
- `SeparationType` se deriva igual que hoy (join a `RetirementCategoryCatalogItem`, `:425-429`).
- **Fix necesario**: la resolución de plaza hoy toma "el assignment activo" (`:432-437`); tras la ejecución las plazas quedan **cerradas** → fallback a la asignación **más reciente** (por `StartDate`/`EndDate`) para que la entrevista siga capturable en `EJECUTADA` (D-07).
- El record `ExitInterviewSubmissionSnapshot` (`IExitInterviewRepository.cs:21-26`) **conserva su shape** → los dos consumidores (SAVE `ExitInterviewSubmissions.Handlers.cs:107-119` y GET `:47-55`) y la derivación de `Period` (`:138`, ahora desde la `RetirementDate` de la solicitud) quedan sin cambios estructurales; solo se ajusta el mensaje de precondición ("no hay solicitud de retiro vigente").

**RF-008 — bandeja de entrevistas.** Nuevo endpoint en el reporting controller (§3.10): `GET api/v1/companies/{companyId}/retirement-requests/interview-tray` (filtros: estado de entrevista, categoría/motivo, rango de fecha de retiro). Query repo `GetRetirementInterviewTrayAsync`:

- Base: solicitudes en `AUTORIZADA`/`EJECUTADA` (RN-008.2 excluye `REVERTIDA`/`ANULADA`/`RECHAZADA`).
- Left-join a formularios publicados activos por motivo (`GetActiveFormForReasonAsync` como referencia de filtro) y a submissions **no archivadas** del expediente.
- **Estado de entrevista derivado por fila**: `SIN_FORMULARIO` (no hay form activo para el motivo) → `PENDIENTE` (sin submission) → `BORRADOR` (draft) → `ENVIADA` (submitted).
- Gate: `EnsureCanViewRetirementInterviewTrayAsync` (`ViewExitInterviews` ∨ `ViewRetirements` — RN-008.1; la lectura de **respuestas** sigue gobernada por el módulo de entrevista, D-14 de entrevista).

### 3.10 Bandeja de retiros + exportación (RF-002)

`RetirementRequestsReportingController` — clon de `CertificateRequestsReportingController`:

- **Sin `[AuthorizationPolicySet]`** (mismo comentario-razón `:16-21`: el POST /query es un READ; gate por handler `EnsureCanViewRetirementsAsync`). El nombre NO empieza con `PersonnelFile` → exento del regex de gobernanza (`:56-57`).
- `POST api/v1/companies/{companyId}/retirement-requests/query` — `[EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]`; filtros: `statusCode, categoryCode, reasonCode, employeeId, requestFromUtc/ToUtc, retirementFromUtc/ToUtc, q`; paginación 1/25; **`StatusCounts`** (patrón `QueryCertificateRequestsAsync:2919-2989`). El detalle incluye la línea de tiempo (solicitó/autorizó/ejecutó/revirtió + notas) leyendo los campos de la entidad (RN-002.3: snapshots sobreviven a cambios posteriores).
- `GET …/retirement-requests/export` — `[EnableRateLimiting(…Export)]`; `ReportExportDeliveryService.CreateFileResultAsync` con `resourceKey: "RETIREMENT_REQUESTS"` (literal, evita el guardrail de `ReportExportResources.All`), `fileNamePrefix: "retirement-requests"`; formatos `xlsx/csv/json` + 413 al exceder `SynchronousReadLimit`. Fila de export `RetirementRequestExportRow` con **propiedades en español** (headers por reflexión, `ReportExportFileWriter.GetExportProperties:155-159`): Empleado, Solicitante, FechaSolicitud, FechaRetiro, Categoria, Motivo, Estado, Autorizador, FechaAutorizacion, Ejecutor, FechaEjecucion, Observacion.
- `GET …/retirement-requests/interview-tray` (§3.9).

### 3.11 Puerta única (D-01) + limpieza de legados (D-08) — cierre de la vía legada

**Contrato (breaking, sin fallback — ratificado):**
- `UpdatePersonnelFileEmployeeProfileRequest` (`PersonnelFileRequests.cs:115-125`) pierde `RetirementCategoryCode/ReasonCode/Notes/Date`.
- `UpdatePersonnelFileEmployeeProfileCommand` (`EmployeeProfiles.cs:77-90`), el mapeo del controller (`PersonnelFileEmploymentController.cs:201-204`), la validación jerárquica del handler (`EmployeeProfiles.cs:191-202`) y el repo upsert (`PersonnelFileEmployeeRepository.cs:31-38`) se limpian en cadena; `PersonnelFileEmployeeProfile.Create/Update` pierden los parámetros (§3.3.4).
- **El handler rechaza `EmploymentStatusCode == "RETIRADO"`** → 422 `EMPLOYMENT_STATUS_RETIRADO_RESERVED` (el código queda reservado a `ApplyRetirement`). El resto de estados sigue validándose contra catálogo (`EmployeeProfiles.cs:184-189`).
- **El GET/response conserva** los campos de retiro (`PersonnelFileEmployeeProfileResponse`, `Map:2216-2232`) — solo se cierra la escritura.
- `openapi.yaml` regenerado; guía frontend `guia-integracion-frontend-retiro-definitivo.md`.

**Limpieza de datos legados (D-08 ratificada, migración de datos):** los retiros pre-módulo son datos de prueba y se eliminan. SQL indicativo (verificar nombres de columna exactos al generar; tablas confirmadas):

```sql
-- 1) archivar submissions de entrevista ligadas a bajas legadas (coherencia con D-09)
UPDATE exit_interview_submissions s SET status = <Archived>
FROM personnel_file_employee_profiles p
WHERE s.personnel_file_id = p.personnel_file_id AND s.tenant_id = p.tenant_id
  AND p.retirement_date IS NOT NULL AND s.status <> <Archived>;
-- 2) limpiar la baja legada del perfil (a la fecha del despliegue TODO retirement_date es legado)
UPDATE personnel_file_employee_profiles
SET retirement_category_code = NULL, retirement_reason_code = NULL,
    retirement_notes = NULL, retirement_date = NULL,
    employment_status_code = CASE WHEN employment_status_code = 'RETIRADO'
                                  THEN 'ACTIVO' ELSE employment_status_code END
WHERE retirement_date IS NOT NULL OR employment_status_code = 'RETIRADO';
```

**Deliberadamente NO se toca `personnel_files.is_active`** (hay expedientes de prueba inactivos por otras razones; reactivar en masa sería incorrecto). Tras la limpieza: todo `RetirementDate` no nulo proviene del módulo (RN-015.3) y el consumo derivado (rehire `RehireEligibilityRules.cs:33-38`, antigüedad `EmployeeProfiles.cs:114`, tablero `PersonnelFileDashboard.Rules.cs:52-56`) queda alimentado únicamente por la ejecución orquestada — sin cambios en esos consumidores.

### 3.12 API — controllers, contratos y persistencia

**Tres controllers de escritura** (la política de `[AuthorizationPolicySet]` es de clase — separación por permiso, precedente sustitución/constancias; ninguno empieza con `PersonnelFile`):

| Controller | PolicySet (Read, Manage) | Endpoints |
|---|---|---|
| `RetirementRequestsController` | `(ViewRetirements, ManageRetirements)` | `GET/POST api/v1/personnel-files/{publicId:guid}/retirement-requests` · `GET/PUT …/{requestPublicId:guid}` · `PATCH …/cancel` (bifurca gate por estado en handler) · `PATCH …/execution` |
| `RetirementRequestResolutionController` | `(ViewRetirements, AuthorizeRetirement)` | `PATCH …/retirement-requests/{id}/resolution` (autorizar/rechazar) |
| `RetirementRequestReversalController` (Ola 2) | `(ViewRetirements, RevertRetirement)` | `PATCH …/retirement-requests/{id}/reversal` |

> ⚠ La anulación de una `AUTORIZADA` viaja por el MISMO `PATCH …/cancel` pero su gate de handler exige `AuthorizeRetirement`; como el policy-set de la clase exige `ManageRetirements` en el PATCH, el autorizador "puro" debe recibir también `ViewRetirements`+`ManageRetirements`… **No**: para evitar ese acople, `cancel` sobre `AUTORIZADA` se expone en el **resolution controller** como `PATCH …/annulment` (policy `AuthorizeRetirement`), y `cancel` en el controller principal solo acepta `SOLICITADA`. Dos rutas, una transición cada una — el frontend elige por estado (documentado en la guía FE).

Todos los PATCH/PUT con `[FromIfMatch] Guid concurrencyToken` (`FromIfMatchAttribute`; 400 sin header / 409 obsoleto). **Ningún controller opta a `[ResourceActions]`** (igual que EconomicAid — evita el requisito `ISupportsAllowedActions` de `AllowedActionsCoverageIntegrationTests`).

**Contratos** (`Api/Contracts/PersonnelFiles/RetirementRequestContracts.cs`): `AddRetirementRequestRequest(RequesterFilePublicId, RequestDate, RetirementDate, RetirementCategoryCode, RetirementReasonCode, Notes)`, `UpdateRetirementRequestRequest(…)`, `ResolveRetirementRequestRequest(TargetStatusCode, Notes)`, `CancelRetirementRequestRequest(Notes?)`, `ExecuteRetirementRequestRequest(BlockRehire, RehireBlockReason?)`, `RevertRetirementRequestRequest(Reason)`. Response `PersonnelFileRetirementRequestResponse` con todos los campos + snapshots (los `Guid …Id` se renombran a `…PublicId` en el wire automáticamente — `PublicContractNaming.cs:162-185`).

**EF config** (`PersonnelFileRetirementRequestConfiguration`): tabla `personnel_file_retirement_requests` — índices `uq_…__public_id`; `ix_…__tenant_file_status`; `ix_…__tenant_file_request_date`; y **respaldo de RN-001.2 en BD**: índice único filtrado `uq_personnel_file_retirement_requests__tenant_file_open` sobre `(tenant_id, personnel_file_id)` con `HasFilter("request_status_code in ('SOLICITADA','AUTORIZADA') and is_active")` (precedente de filtered-unique: `uq_personnel_files__tenant_linked_user`). Tabla hija `personnel_file_retirement_closed_records` con FK cascade + `ix_…__tenant_request`. Todos los nombres < 63 chars (verificar al generar).

### 3.13 Localización

~18 códigos nuevos (§5) en `BackendMessages.resx` + `.es.resx` **dentro del PR que los introduce** (`BackendMessageLocalizationTests` corre por PR). Todo `.WithMessage(...)` nuevo en validadores exige su clave `validation.message.*` en ambos resx (`BuildValidationKey:224-235`) — preferir códigos de dominio y minimizar `WithMessage` custom (lección de constancias PR-10).

### 3.14 Auditoría

Patrón `PersonnelFileEmployeeAudits.LogUpdateAsync(before/after)` + doble `SaveChanges` dentro de la transacción (ejemplo `EconomicAidRequests.Handlers.cs:123-135`) en: alta, edición, resolución, anulación, **ejecución** (before/after del expediente completo — espejo del rehire `:264-277`) y **reversión**. Las acciones `BAJA`/`REVERSION_BAJA` quedan además en el journal append-only (visible en el export existente de acciones).

---

## 4. Migraciones y seeds

| # | Migración (PR) | Contenido |
|---|---|---|
| M1 (PR-1) | `AddRetirementRequestStatusCatalogAndBajaActionTypes` | `CreateTable retirement_request_status_catalog_items` + `InsertData` 6 estados (**-9810…-9815**, SV) + `InsertData` ActionType `BAJA=-9482` / `REVERSION_BAJA=-9483` (SV) + `Sql` data-fix `COMPLETADA→APLICADA` (D-15) |
| M2 (PR-3) | `AddRetirementRequests` | Tablas `personnel_file_retirement_requests` + `personnel_file_retirement_closed_records` (índices §3.12, incl. filtered-unique de solicitud abierta) |
| M3 (PR-9) | `CloseLegacyRetirementPathAndCleanupTestData` | Solo datos (`migrationBuilder.Sql`, §3.11): archivar submissions legadas + limpiar perfil (D-08). El cambio de contrato del PUT no toca esquema. |

Generación/verificación (convención del repo): `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add <Name> -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · drift: `… dotnet ef migrations has-pending-model-changes` (vacío). Guardrail: `MigrationSeedingIntegrationTests` (aplica todas las migraciones a BD real). Última migración a la fecha: `20260702044855_AddAfpCatalogAffiliationAndPensionParams`.

---

## 5. Mapa de errores (resumen)

| Código | HTTP | Dónde |
|---|---|---|
| `RETIREMENT_REQUEST_EMPLOYEE_NOT_ELIGIBLE` | 422 | Alta/autorización (RN-001.1, RN-004.4) |
| `RETIREMENT_REQUEST_ALREADY_OPEN` | 422 | Alta (RN-001.2) |
| `RETIREMENT_REQUEST_REQUESTER_INVALID` | 422 | Alta/edición (D-02) |
| `RETIREMENT_REQUEST_DATE_INCOHERENT` | 422 | Alta/edición/ejecución (RN-001.4, RF-016, R-T5) |
| `RETIREMENT_REQUEST_STATE_RULE_VIOLATION` | 422 | Toda transición inválida (editar no-`SOLICITADA`, anular `EJECUTADA` → mensaje señala reversión, etc.) |
| `RETIREMENT_RESOLUTION_TARGET_INVALID` | 422 | Resolución (target ∉ {AUTORIZADA, RECHAZADA}) |
| `RETIREMENT_RESOLUTION_NOTES_REQUIRED` | 422 | Rechazo sin nota (RN-004.3) |
| `RETIREMENT_SELF_ACTION_FORBIDDEN` | **403** | Autorizar/ejecutar/revertir siendo el sujeto (D-13) |
| `RETIREMENT_REQUESTER_CANNOT_AUTHORIZE` | **403** | Autorizar/anular-autorizada siendo el solicitante (D-13 ratificada) |
| `RETIREMENT_EXECUTION_DATE_NOT_REACHED` | 422 | Ejecutar con `FechaRetiro` futura (D-05) |
| `RETIREMENT_EXECUTION_STATE_CONFLICT` | 422 | Perfil divergió antes de ejecutar (E-07) |
| `RETIREMENT_LAST_ADMIN_CONFLICT` | 422 | El sujeto es el último administrador activo (invariante IAM) |
| `RETIREMENT_REVERSAL_REASON_REQUIRED` | 422 | Revertir sin motivo (RN-010.3) |
| `RETIREMENT_REVERSAL_WINDOW_EXPIRED` | 422 | > 30 días desde la ejecución (**RN-012.4**, E-17) |
| `RETIREMENT_REVERSAL_BLOCKED_BY_REHIRE` | 422 | Rehire posterior (D-10, E-10) |
| `RETIREMENT_REVERSAL_STATE_DIVERGED` | 422 | Estado no coincide con lo ejecutado (RN-012.2) |
| `RETIREMENT_REVERSAL_NOT_MOST_RECENT` | 422 | No es el retiro ejecutado más reciente (RN-012.3) |
| `EMPLOYMENT_STATUS_RETIRADO_RESERVED` | 422 | PUT legado intenta `RETIRADO` (D-01) |

Reusados: `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` (400), `REPORT_EXPORT_TOO_LARGE` (413), `EXIT_INTERVIEW_*` (gate/inmutabilidad de entrevista), 400/409 de `If-Match`. Los mensajes inline `common.validation` del gate de entrevista se conservan (sin resx, como hoy) con texto actualizado a "solicitud vigente".

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- `RetirementRequestRulesTests` (nuevo): elegibilidad (cada condición), coherencia de fechas (bordes: retiro == ingreso; solicitud mañana), `IsExecutableOn` (hoy/futuro), **`IsWithinReversalWindow`** (día 30 ok / día 31 falla / bordes de hora), `HasClosingBlockers`.
- Transiciones de dominio: cada guard de `Create/Update/Resolve/Cancel/MarkExecuted/MarkReverted` (rechazo sin nota lanza; resolver no-`SOLICITADA` lanza; revertir no-`EJECUTADA` lanza; snapshot persiste en `MarkExecuted`).
- `Reopen()` de asignación/contrato: restaura `PreviousEndDate` exacto (null y no-null) + `IsActive`.
- `ApplyRetirement/ClearRetirement` del perfil: setea/limpia + status; `retirementDate < HireDate` lanza.
- Validadores; governance (4 policies en el HashSet); paridad de localización (~18 códigos EN+ES).
- Actualizar: `PersonnelFileEmployeeProfileEmailChangeTests` / `PersonnelFileEmployeeProfileQueryTests` (pierden los args `Retirement*`).

**Integración (`tests/CLARIHR.Api.IntegrationTests/`):**
- `ApiIntegrationTests.Retirement.cs` (nuevo, espejo de `ApiIntegrationTests.Rehire.cs`):
  - **Round-trip feliz**: crear → autorizar → interview-tray lo muestra (`PENDIENTE`/`SIN_FORMULARIO`) → ejecutar → verificar TODOS los efectos (perfil con datos + `RETIRADO`, expediente inactivo, plazas/contratos cerrados a la fecha, login `Inactive`, acción `BAJA`, snapshot) → **revertir** → verificar restauración exacta (estado laboral previo, filas reabiertas con su `EndDate` previo, login reactivado, submission archivada, acción `REVERSION_BAJA`) → nueva solicitud posible.
  - 403: sujeto se auto-autoriza/ejecuta/revierte; **solicitante intenta autorizar**.
  - 422: segunda solicitud abierta; ejecutar con fecha futura; anular ejecutada; editar autorizada; revertir con rehire posterior; PUT legado con `RETIRADO` (tras PR-9).
  - `If-Match` obsoleto → 409.
  - E-16: login desactivado manualmente ANTES de ejecutar → la reversión NO lo reactiva.
  - Bandeja/export: filtros + contadores + 413 (formato inválido 400).
- **Actualizar `ApiIntegrationTests.Rehire.cs:88-95`**: sembrar la baja **ejecutando el módulo** (create→authorize→execute) en lugar del PUT legado — valida de paso que la ejecución deja al empleado recontratable (`RetirementDate != null`).
- La regla de ventana (30 días) se cubre en unit (el reloj de integración es real); opcionalmente sembrar `ExecutionDateUtc` viejo por SQL directo para el 422.
- Guardrails existentes: `MigrationSeedingIntegrationTests`, `GeneralCatalogKeyMapGuardrailsTests`, `PublicContractGuardrailsIntegrationTests`, `AuthorizationPolicyConventionGovernanceTests`.

---

## 7. Orden de implementación (PRs sugeridos)

**Ola 1 — intake + ejecución + bandejas (RF-001…RF-009, RF-013…RF-016):**

1. **PR-1 — Catálogo + tipos de acción + fix D-15** (§3.1, M1). Aislado; verde con guardrail de biyección y seeding.
2. **PR-2 — Permisos + políticas + gates** (§3.2): 4 codes + provisioning + 4 policies (Authorize/Revert sin Admin) + governance + 5 métodos `EnsureCan…`.
3. **PR-3 — Dominio + reglas + EF + M2** (§3.3, §3.4, §3.12): entidad + hija + statuses + `ApplyRetirement/ClearRetirement` + `Reopen()` + configs + migración. Batería unitaria de dominio/reglas.
4. **PR-4 — Intake CQRS + controller principal** (§3.5, §3.12): crear/leer/editar/anular-`SOLICITADA` + contratos + validadores + repo + resx del PR.
5. **PR-5 — Resolución** (§3.6): autorizar/rechazar + `annulment` de `AUTORIZADA` + anti-auto-gestión + **solicitante ≠ autorizador** + archivado de borrador al anular (RN-005.3).
6. **PR-6 — Ejecución orquestada** (§3.7): cierre-con-captura + `ICompanyUserLifecycleService` (refactor IAM sin cambio de comportamiento) + snapshot + acción `BAJA` + D-18 + guard de último admin.
7. **PR-7 — Bandeja + export** (§3.10): reporting controller + query/contadores + export + rate-limits.
8. **PR-8 — Integración entrevista** (§3.9): re-apuntar `GetSubmissionSnapshotAsync` (sin fallback) + fix de plaza post-ejecución + interview-tray + gate dual.
9. **PR-9 — Puerta única + limpieza (D-01/D-08, M3)** (§3.11): contrato del PUT + rechazo `RETIRADO` + migración de datos + actualización de tests legados + `openapi.yaml` regenerado.

**Ola 2 — reversión (RF-010…RF-012):**

10. **PR-10 — Reversión** (§3.8): revert handler + `Reopen` + reactivación condicionada de login + 4 bloqueos (incl. **ventana 30 días**) + `REVERSION_BAJA` + archivado + reversal controller.
11. **PR-11 — E2E + guía frontend**: `ApiIntegrationTests.Retirement.cs` completo (round-trip con reversión), verificación integral (suites verdes, drift vacío) + `docs/technical/guia-integracion-frontend-retiro-definitivo.md`.

> **Cada PR lleva sus claves resx y sus tests** (paridad y governance corren por PR). MVP demostrable al cierre de PR-9 (las tres pantallas del requerimiento operan; la reversión llega en Ola 2 — mismo corte que el análisis §18.2).

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Reapertura de filas cerradas (la pieza realmente nueva).** Mitigado: verificado que NO hay índices únicos filtrados en asignaciones/contratos (solo `uq__public_id` + check de fechas); `Reopen(previousEndDate)` restaura el estado exacto; el bloqueo por rehire posterior (D-10) garantiza que no existan filas nuevas en conflicto con la única-primaria. Test dedicado de restauración exacta (EndDate previo no-null — caso `SetActive(false)` del cierre).
- **R-T2 — Reutilizar la desactivación de login dentro de la transacción.** `DeactivateCompanyUser` no abre transacción propia (un solo `SaveChanges`) → su núcleo extraído se enlista en la transacción del módulo. Riesgo de regresión en los endpoints IAM → el refactor conserva sus handlers intactos (delegación); sus tests existentes deben seguir verdes sin tocar.
- **R-T3 — Invariante "último administrador activo".** Ejecutar la baja del último admin dejaría a la empresa sin administración. Decisión: guard previo → 422 `RETIREMENT_LAST_ADMIN_CONFLICT` (transferir la administración primero). Documentar en la guía FE.
- **R-T4 — `cancel` con permiso dependiente del estado.** El policy-set es de clase → la anulación de `AUTORIZADA` vive en el resolution controller (`PATCH …/annulment`) y la de `SOLICITADA` en el principal (`PATCH …/cancel`). Dos rutas explícitas > un endpoint con autorización imposible de expresar en la convención.
- **R-T5 — Check constraints de fechas al cerrar.** `end_date >= start_date` / `contract_end_date >= contract_date`: una fila activa con `StartDate > FechaRetiro` (alta posterior a una baja retroactiva) rompería el `Close`. Guard previo `HasClosingBlockers` → 422 con mensaje accionable.
- **R-T6 — Estado híbrido canónico+catálogo.** Igual que economic aid (R-T2 de aquel plan): el `code` sembrado es estructural (renombrar el *nombre* sí; el *código* no). Sin CRUD admin de este catálogo en Fase 1.
- **R-T7 — Breaking change del PUT (D-01, sin fallback).** El frontend cambia en el mismo release (ratificado). Coordinar despliegue BE+FE; `openapi.yaml` regenerado en PR-9; la guía FE lista el diff de contrato campo a campo.
- **R-T8 — Migración de datos destructiva (D-08).** El UPDATE de limpieza es irreversible por diseño (datos de prueba, ratificado). Mitigación: la migración registra el conteo afectado en el log de despliegue; se ejecuta después de que la puerta única esté cerrada (mismo PR-9) para que no se re-creen legados.
- **R-T9 — Bandeja de entrevistas con datos derivados.** El estado de entrevista se computa con joins (form activo + submission) — cuidar N+1: una sola query proyectada (patrón `QueryCertificateRequestsAsync`).
- **R-T10 — Reloj.** Ventana de 30 días y `FechaRetiro ≤ hoy` SIEMPRE vía `IDateTimeProvider.UtcNow` + parámetro `asOfUtc` en reglas puras (tests deterministas).
- **R-T11 — `dotnet ef`.** Requiere `DOTNET_ROLL_FORWARD=Major` en este entorno.

---

## 9. Checklist de implementación

- [ ] **Catálogo:** `RetirementRequestStatusCatalogItem` + categoría + key map + switch + EF config (índice acortado) + seed SV `-9810…-9815`; ActionType `BAJA=-9482`/`REVERSION_BAJA=-9483`; fix `COMPLETADA→APLICADA` (const + data).
- [ ] **Permisos:** 4 codes + provisioning + 4 policies (`AuthorizeRetirement`/`RevertRetirement` sin Admin) + governance HashSet + 5 gates `EnsureCan…`.
- [ ] **Dominio:** `PersonnelFileRetirementRequest` + `RetirementRequestStatuses` + `RetirementRequestClosedRecord` + métodos con guards; `ApplyRetirement/ClearRetirement`; `Reopen()` en asignación y contrato; `Create/Update` del perfil sin `Retirement*`.
- [ ] **Reglas:** `RetirementRequest.Rules.cs` (elegibilidad, fechas, ejecutabilidad, **ventana 30 días**, bloqueos de cierre) + errores.
- [ ] **Aplicación:** intake CQRS + resolución (anti-auto + **solicitante≠autorizador**) + anulación por estado + ejecución orquestada (snapshot + cierre-con-captura + login + acción `BAJA` + D-18 + último-admin) + reversión (4 bloqueos + restauración + archivado + `REVERSION_BAJA`).
- [ ] **IAM:** `ICompanyUserLifecycleService` (núcleo deactivate/reactivate) + refactor de los 2 handlers existentes sin cambio de comportamiento.
- [ ] **Entrevista:** `GetSubmissionSnapshotAsync` → solicitud vigente (sin fallback) + plaza más-reciente post-ejecución + interview-tray + gate dual.
- [ ] **API:** 3 controllers de escritura (+1 reporting) + contratos + `If-Match` en toda mutación; sin `[ResourceActions]`.
- [ ] **Bandeja/export:** query + `StatusCounts` + export (`resourceKey "RETIREMENT_REQUESTS"`, 413) + rate-limits.
- [ ] **Puerta única:** PUT sin `Retirement*` + rechazo `RETIRADO` + response intacto + `openapi.yaml` + guía FE.
- [ ] **Migraciones:** M1 (catálogo+acciones+data-fix), M2 (tablas + filtered-unique de solicitud abierta), M3 (limpieza D-08); `has-pending-model-changes` vacío.
- [ ] **Localización:** ~18 códigos EN+ES (por PR); `validation.message.*` para cada `WithMessage` nuevo.
- [ ] **Auditoría:** before/after en todas las transiciones (doble `SaveChanges` en transacción).
- [ ] **Tests:** reglas + transiciones + `Reopen` + validadores + governance + paridad + integración round-trip (ejecutar→revertir) + 403/422/409 + actualización de Rehire/perfil + guardrails verdes.
- [ ] **Verificación final:** `dotnet build` (0 err), `dotnet test` (unit + integración), drift vacío, seeds verificados en BD real.

---

> **Trazabilidad decisión → componente.** D-01 → §3.11 (contrato PUT + `EMPLOYMENT_STATUS_RETIRADO_RESERVED`); D-02 → §3.3.2 (RequesterFilePublicId + snapshot) + §3.5; D-03 → gates sin rama self (§3.2/§3.5); D-04/D-16 → §3.1 + §3.3.1 (híbrido); D-05 → §3.7 guard fecha + `IsExecutableOn`; D-06 → §3.7 pasos [6]-[7]; D-07 → §3.9 (gate desde `AUTORIZADA`); D-08 → §3.11 (M3) + reversión solo con snapshot; D-09 → §3.8 paso [7]; D-10 → guard rehire (§3.8 [2]); D-11 → snapshot §3.3.2/§3.3.3 + restauración §3.8; D-12 → §3.2 (4 permisos, Admin excluido en 2); D-13 → §3.2/§3.6 (dos desigualdades, 403 dedicados); D-14 → sin componente (fuera de alcance; punto de integración = pasos [8]/[9] del diagrama, donde un futuro módulo de nómina se engancharía); D-15 → §3.1 (seeds + fix); D-17/D-19 → sin componente (Fase 2); D-18 → §3.7 paso [5]; RN-012.4 (30 días) → `IsWithinReversalWindow` + `RETIREMENT_REVERSAL_WINDOW_EXPIRED`. Este plan implementa la Fase 1 completa del análisis (RF-001…RF-016) en 2 olas, clonando plantillas verificadas y con **dos únicas piezas net-new de riesgo** (reapertura de filas + login transaccional), ambas acotadas con precedente directo.
