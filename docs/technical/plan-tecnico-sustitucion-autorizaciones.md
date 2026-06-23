# Plan Técnico de Implementación — Sustitución para Autorizaciones (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-sustitucion-autorizaciones-empleado.md`](../business/analisis-sustitucion-autorizaciones-empleado.md) (decisiones D-01…D-12) |
| **Módulos** | `PersonnelFiles` (Empleo) · `PositionSlots` · `GeneralCatalogs` · `IdentityAccess`/Provisioning (RBAC) · Localization |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-21 |

---

## 1. Objetivo y enfoque

Endurecer la funcionalidad **ya existente** `PersonnelFileAuthorizationSubstitution` (registro documental) conforme a las decisiones **D-01…D-12**, manteniéndola como **registro documental/informativo** (D-01) y dejándola lista como fuente de verdad para los futuros módulos de Aprobaciones/Ausencias (diferido, RF-010).

**Insight central del análisis de código.** La entidad, el CQRS (Add/Update/Patch/Delete/Get/GetList), la API (6 endpoints), la concurrencia (`ConcurrencyToken`/`If-Match`) y la auditoría (`IAuditService`) **ya están construidos y funcionando**. El trabajo NO es construir; es:

1. **Cambiar 1 campo del agregado**: `SubstitutePositionTitle` (texto libre) → `SubstitutePositionSlotPublicId` (FK a `PositionSlot`) + snapshot opcional del título (D-02).
2. **Hacer `EndDate` obligatoria** (D-03) — hoy es `DateTime?`.
3. **Añadir un catálogo** `substitution-types` (D-08) — patrón country-scoped ya estandarizado.
4. **Añadir un módulo de reglas puro** `AuthorizationSubstitutions.Rules.cs`, espejo de `EmploymentAssignmentRules`, para no-solape + único-vigente + sustituto-disponible (D-04, D-06, D-07).
5. **Añadir validaciones de referencia** en los handlers: sustituto elegible (D-existencia/tenant/empleado/activo) y propiedad de la plaza (RF-001, RF-003).
6. **Añadir un permiso dedicado** `PersonnelFiles.ManageSubstitutions` (D-09).
7. **Completar la auditoría** con diff antes/después + historial visible (D-12).
8. **Localizar** ~6 errores nuevos (EN/es/es-SV).

Todo sigue patrones que ya existen en el repo (catálogos country-scoped, `EmploymentAssignmentRules`, permisos `AuthorizeRehire`/`ViewCompensation`). No hay decisiones técnicas abiertas de fondo.

---

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Agregado | `PersonnelFileAuthorizationSubstitution` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:421-501`): `SubstitutionTypeCode`, `SubstitutePersonnelFilePublicId`, `SubstitutePositionTitle?`, `StartDate`, `EndDate?`, `IsActive`, `Notes?`, `ConcurrencyToken`. Factory `Create(...)` y `Update(...)` regeneran token. | Cambiar `SubstitutePositionTitle` → `SubstitutePositionSlotPublicId`; `EndDate` → no-nullable; firmas de `Create/Update`. |
| 2 | CQRS | `AuthorizationSubstitutions.cs`: `AuthorizationSubstitutionInput`, comandos/queries, validadores. `AuthorizationSubstitutionInputValidator` (`:81-89`) solo valida `StartDate ≤ EndDate` cuando hay fin. | Endurecer validador: `SubstitutePositionSlotPublicId` `NotEmpty`; `EndDate` `NotNull`; catálogo en handler. |
| 3 | Handlers | `AuthorizationSubstitutions.Handlers.cs`: estado `IsCompletedEmployee` (`:46`), anti-auto-sustitución (`:51`), `LoadForManageAsync` (`:34,108,195,302`), transacción + `PersonnelFileEmployeeAudits.LogUpdateAsync`. **NO** valida sustituto, ni catálogo, ni solape. | Insertar validaciones + invocación de reglas + permiso dedicado. |
| 4 | Patch applier | `PersonnelFileAuthorizationSubstitutionPatchApplier` (`AuthorizationSubstitutions.cs:415-532`): segmentos `substitutionTypeCode/substitutePersonnelFileId/substitutePositionTitle/startDate/endDate/notes/isActive`; `endDate` permite `remove`→null. | Renombrar segmento a `substitutePositionSlotId`; `endDate` **no removible**. |
| 5 | Reglas (patrón oro) | `EmploymentAssignments.Rules.cs:1-158`: `internal static class EmploymentAssignmentErrors` + `EmploymentAssignmentRules.Evaluate(...) : Result<Evaluation>`; `RangesOverlap(DateTime,DateTime?,DateTime,DateTime?)` (`:97`). Namespace `CLARIHR.Application.Features.PersonnelFiles`. | Crear `AuthorizationSubstitutions.Rules.cs` espejo; **reusar** `RangesOverlap`. |
| 6 | Repositorio | `IPersonnelFileEmployeeRepository` (`:253-295`) + impl (`PersonnelFileEmployeeRepository.cs:564-654`, `Map` `:1896`). `UpdateAuthorizationSubstitutionAsync`/`PatchAuthorizationSubstitutionAsync` reciben `string? substitutePositionTitle`. `GetAuthorizationSubstitutionsAsync(publicId)` y `GetEmploymentAssignmentsAsync(publicId)` ya existen. | Cambiar firmas (title→slotId); ajustar `Map`; **reusar** Get* para reglas y propiedad de plaza. |
| 7 | Resolución de expediente | `IPersonnelFileRepository`: `GetForAccessCheckAsync(publicId)` (header, sin colecciones), `ExistsOutsideTenantAsync(publicId)` (`IgnoreQueryFilters`). | Validar sustituto: cargar header, chequear `RecordType/LifecycleStatus/IsActive`; cross-tenant ⇒ `ExistsOutsideTenant`. |
| 8 | Catálogos country-scoped | Constantes en `Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` (`PersonnelCurriculumCatalogCategories`); key map `GeneralCatalogKeyMap.cs`; clases en `Domain/GeneralCatalogs/GeneralCatalogItems.cs`; config base `GeneralCatalogItemConfigurationBase<T>`; validación `IPersonnelFileRepository.CatalogCodeIsActiveAsync(companyId, category, code, ct)`. | Añadir `SubstitutionTypeCatalogItem` siguiendo el patrón exacto (D-08). |
| 9 | Permisos (patrón) | `PersonnelFilePermissionCodes` (`PersonnelFileCommon.cs:82-101`: `Read/Admin/AuthorizeRehire/ViewCompensation`); `PersonnelFilePolicies`; `Program.cs` registra políticas; semilla `ProvisioningConstants.CompanyAdminPermissions`; `IPersonnelFileAuthorizationService.EnsureCan*Async`. | Añadir `ManageSubstitutions` (constante + política + semilla + método de servicio + base de handler). |
| 10 | EF config | `PersonnelFileAuthorizationSubstitutionConfiguration` (`Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs:164-200`): tabla `personnel_file_authorization_substitutions`, check `end_date is null or end_date >= start_date`, `substitute_position_title (120)`, índice `(tenant, file, type, active)`, `concurrency_token IsConcurrencyToken`. | Alterar columnas; cambiar check a `end_date >= start_date`; nueva config de catálogo. |
| 11 | DbContext / migraciones | `ApplicationDbContext.cs` (`ApplyConfigurationsFromAssembly`); comandos EF en `docs/technical/operations/manual-migrations-and-azure-deploy.md` (`--project src/CLARIHR.Infrastructure --startup-project src/CLARIHR.Api`). | DbSet del catálogo + 1 migración (crear tabla + alterar entidad + seed SV). |
| 12 | API | Contratos `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:194-220` (`Add/Update/PatchAuthorizationSubstitutionRequest`, hoy `SubstitutePositionTitle?`/`EndDate?`); controlador `PersonnelFileEmploymentController.cs:559-727` (6 endpoints, `[FromIfMatch]`, `[AuthorizationPolicySet]`). | Cambiar DTOs (title→slotId, endDate requerido) + mapeo a comandos + política. |
| 13 | Localización | Errores `Error(code,msg,ErrorType)` en `*Errors.cs`; recursos `BackendMessages.resx`/`.es.resx`/`.es-SV.resx`; paridad `BackendMessageLocalizationTests`. | 6 códigos nuevos × 3 resx. |

---

## 3. Arquitectura de la solución

### 3.1 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs`

Cambiar la entidad `PersonnelFileAuthorizationSubstitution` (`:421-501`): `EndDate` no-nullable, reemplazar `SubstitutePositionTitle` por `SubstitutePositionSlotPublicId`, añadir snapshot opcional.

```csharp
public string SubstitutionTypeCode { get; private set; } = string.Empty;
public Guid SubstitutePersonnelFilePublicId { get; private set; }
public Guid SubstitutePositionSlotPublicId { get; private set; }      // NUEVO (FK lógica a PositionSlot)
public string? SubstitutePositionTitleSnapshot { get; private set; }  // NUEVO (snapshot para historial/UI)
public DateTime StartDate { get; private set; }
public DateTime EndDate { get; private set; }                         // CAMBIO: DateTime (obligatoria, D-03)
public bool IsActive { get; private set; }
public string? Notes { get; private set; }
public Guid ConcurrencyToken { get; private set; }

public static PersonnelFileAuthorizationSubstitution Create(
    string substitutionTypeCode,
    Guid substitutePersonnelFilePublicId,
    Guid substitutePositionSlotPublicId,
    string? substitutePositionTitleSnapshot,
    DateTime startDate,
    DateTime endDate,
    bool isActive,
    string? notes) => new(...);

public void Update(
    string substitutionTypeCode,
    Guid substitutePersonnelFilePublicId,
    Guid substitutePositionSlotPublicId,
    string? substitutePositionTitleSnapshot,
    DateTime startDate,
    DateTime endDate,
    string? notes) { ConcurrencyToken = Guid.NewGuid(); /* ... */ }
```

`SetActive(bool)` no cambia. La normalización (`PersonnelFileNormalization`) se mantiene para `SubstitutionTypeCode` y notas.

### 3.2 Catálogo nuevo `substitution-types` (D-08)

Recipe estándar country-scoped (5 archivos + seed), igual que `assignment-types`:

**a) Clase de dominio** — `Domain/GeneralCatalogs/GeneralCatalogItems.cs` (espejo de `AssignmentTypeCatalogItem`):
```csharp
public sealed class SubstitutionTypeCatalogItem : GeneralCatalogItem
{
    private SubstitutionTypeCatalogItem() { }
    private SubstitutionTypeCatalogItem(Guid publicId, long countryCatalogItemId, string countryCode,
        string code, string name, bool isActive, int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder) { }
    public static SubstitutionTypeCatalogItem Create(long countryCatalogItemId, string countryCode,
        string code, string name, bool isActive, int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}
```

**b) Constante de categoría** — `Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` → `PersonnelCurriculumCatalogCategories`:
```csharp
public const string SubstitutionType = "CurriculumSubstitutionType";
```

**c) Wire key** — `Features/PersonnelFiles/Catalogs/GeneralCatalogKeyMap.cs` (dict `CatalogKeys`):
```csharp
["substitution-types"] = PersonnelCurriculumCatalogCategories.SubstitutionType,
```

**d) EF config** — nuevo `Configurations/GeneralCatalogs/SubstitutionTypeCatalogItemConfiguration.cs`:
```csharp
internal sealed class SubstitutionTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<SubstitutionTypeCatalogItem>
{
    public SubstitutionTypeCatalogItemConfiguration() : base(
        "substitution_type_catalog_items",
        "pk_substitution_type_catalog_items",
        "uq_substitution_type_catalog_items__public_id",
        "uq_substitution_type_catalog_items__country_code",
        "ix_substitution_type_catalog_items__country_active_sort") { }
}
```

**e) DbSet** — `Persistence/ApplicationDbContext.cs` (junto a los demás catálogos):
```csharp
public DbSet<SubstitutionTypeCatalogItem> SubstitutionTypeCatalogItems => Set<SubstitutionTypeCatalogItem>();
```

**f) Validación** — `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs` → `CatalogCodeIsActiveAsync` (switch por categoría normalizada):
```csharp
"CURRICULUMSUBSTITUTIONTYPE" => await IsCountryScopedCatalogCodeActiveAsync<SubstitutionTypeCatalogItem>(
    companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
```

**g) Seed SV** (D-08) — en la migración (`insertData`) y/o el path de provisión de catálogos por país, mirando cómo se sembró `assignment-types`. Valores:
`VACACIONES`, `INCAPACIDAD`, `PERMISO`, `MISION_OFICIAL`, `LICENCIA`, `OTRO`.

> El endpoint `GET /api/v1/general-catalogs/substitution-types?countryCode=SV` queda disponible automáticamente vía el key map (sin tocar el controlador de catálogos).

### 3.3 Módulo de reglas puro — nuevo `Features/PersonnelFiles/Employment/AuthorizationSubstitutions.Rules.cs`

Espejo de `EmploymentAssignmentRules` (testeable sin BD). Ámbito = **empleado completo** (D-04): el solape se evalúa entre **todas** las sustituciones activas del titular. Bloqueo (D-07). Sustituto no disponible (D-06).

```csharp
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Errores de las reglas de sustitución. Cada code requiere entrada EN+ES (paridad: BackendMessageLocalizationTests).</summary>
internal static class AuthorizationSubstitutionErrors
{
    public static readonly Error SubstituteNotFound = new(
        "AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_FOUND",
        "The selected substitute could not be found.", ErrorType.NotFound);

    public static readonly Error SubstituteNotEligible = new(
        "AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_ELIGIBLE",
        "The selected substitute must be an active, completed employee in the same company.", ErrorType.UnprocessableEntity);

    public static readonly Error TypeCodeInvalid = new(
        "AUTHORIZATION_SUBSTITUTION_TYPE_CODE_INVALID",
        "The substitution type code is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error PositionNotOwned = new(
        "AUTHORIZATION_SUBSTITUTION_POSITION_NOT_OWNED",
        "The selected position must be an active assignment of the substitute.", ErrorType.UnprocessableEntity);

    public static readonly Error PeriodOverlap = new(
        "AUTHORIZATION_SUBSTITUTION_PERIOD_OVERLAP",
        "The employee already has an active substitution with an overlapping effective period.", ErrorType.Conflict);

    public static readonly Error SubstituteUnavailable = new(
        "AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_UNAVAILABLE",
        "The substitute is unavailable: they are being substituted (absent) during an overlapping period.", ErrorType.UnprocessableEntity);
}

internal static class AuthorizationSubstitutionRules
{
    /// <summary>Sustitución existente del titular (la candidata se excluye por PublicId).</summary>
    internal sealed record ExistingSubstitution(Guid PublicId, DateTime StartDate, DateTime EndDate, bool IsActive);

    /// <summary>La sustitución propuesta tras aplicar el comando (PublicId nulo en Add).</summary>
    internal sealed record Candidate(Guid? PublicId, DateTime StartDate, DateTime EndDate, bool IsActive);

    /// <summary>Resultado exitoso (sin efectos colaterales; bloqueo, no supersesión — D-07).</summary>
    internal sealed record Evaluation;

    public static Result<Evaluation> Evaluate(
        Candidate candidate,
        IReadOnlyCollection<ExistingSubstitution> titularSubstitutions,
        IReadOnlyCollection<ExistingSubstitution> substituteAsTitularSubstitutions)
    {
        // Las reglas de solape/disponibilidad sólo aplican a la candidata ACTIVA.
        if (!candidate.IsActive)
        {
            return Result<Evaluation>.Success(new Evaluation());
        }

        // (D-04/D-07) Único vigente por titular: ninguna otra sustitución ACTIVA del titular puede solapar.
        var overlapsTitular = titularSubstitutions.Any(other =>
            other.IsActive
            && other.PublicId != candidate.PublicId
            && EmploymentAssignmentRules.RangesOverlap(candidate.StartDate, candidate.EndDate, other.StartDate, other.EndDate));
        if (overlapsTitular)
        {
            return Result<Evaluation>.Failure(AuthorizationSubstitutionErrors.PeriodOverlap);
        }

        // (D-06) Sustituto no disponible: no puede ser titular de otra sustitución ACTIVA solapada (está siendo sustituido/ausente).
        var substituteBusy = substituteAsTitularSubstitutions.Any(other =>
            other.IsActive
            && EmploymentAssignmentRules.RangesOverlap(candidate.StartDate, candidate.EndDate, other.StartDate, other.EndDate));
        if (substituteBusy)
        {
            return Result<Evaluation>.Failure(AuthorizationSubstitutionErrors.SubstituteUnavailable);
        }

        return Result<Evaluation>.Success(new Evaluation());
    }
}
```

> **Nota de diseño.** `SUBSTITUTION_END_DATE_REQUIRED` (E7 del negocio) se resuelve en el **validador** (400 `common.validation`, campo `endDate`), no como código 422 — evita redundancia. `SUBSTITUTION_ALREADY_ACTIVE` queda **subsumido** por `PERIOD_OVERLAP`: con `EndDate` obligatoria, "dos activas a la vez" ≡ "dos activas que solapan". Se permiten sustituciones futuras **no solapadas** (programación), coherente con el estado efectivo (RF-006).

### 3.4 Aplicación — comandos, validadores y patch

**`AuthorizationSubstitutions.cs`:**

- `AuthorizationSubstitutionInput`: `SubstitutePositionTitle` (string?) → `SubstitutePositionSlotPublicId` (Guid). Mantener `EndDate` como `DateTime?` en el input (para distinguir "ausente"→400) pero exigirla en el validador.
- `PersonnelFileAuthorizationSubstitutionResponse`: `SubstitutePositionTitle` → `SubstitutePositionSlotId` (Guid) + `SubstitutePositionTitle` (string?, snapshot resuelto para UI).
- `AuthorizationSubstitutionInputValidator` (`:81-89`):
```csharp
RuleFor(i => i.SubstitutionTypeCode).NotEmpty().MaximumLength(80);
RuleFor(i => i.SubstitutePersonnelFileId).NotEmpty();
RuleFor(i => i.SubstitutePositionSlotPublicId).NotEmpty();                 // NUEVO (D-02)
RuleFor(i => i.EndDate).NotNull();                                          // NUEVO (D-03) → E7 = 400
RuleFor(i => i.StartDate).LessThanOrEqualTo(i => i.EndDate!.Value).When(i => i.EndDate.HasValue);
```
- `PersonnelFileAuthorizationSubstitutionPatchState` + `...PatchApplier` (`:415-532`):
  - Renombrar segmento `substitutePositionTitle` → `substitutePositionSlotId` (lee `ReadNullableGuid`/`ReadRequiredGuid`; **no** removible).
  - `endDate`: cambiar a **no removible** (como `startDate`) y `ReadRequiredDateTime` (D-03).
  - `Validate(state)`: añadir requeridos `SubstitutePositionSlotId != Guid.Empty` y `EndDate` presente.

### 3.5 Aplicación — handlers (`AuthorizationSubstitutions.Handlers.cs`)

En **Add** y **Update** (y la rama de **Patch** cuando mutan campos de negocio), insertar tras los chequeos existentes (estado + anti-auto-sustitución) y **antes** de persistir:

```csharp
// 1) Tipo contra catálogo (D-08)
if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
        personnelFile.TenantId, PersonnelCurriculumCatalogCategories.SubstitutionType,
        input.SubstitutionTypeCode, cancellationToken))
    return Fail(AuthorizationSubstitutionErrors.TypeCodeInvalid);

// 2) Sustituto elegible (RF-001): existe + mismo tenant + Empleado + Completado + Activo
var substitute = await personnelFileRepository.GetForAccessCheckAsync(input.SubstitutePersonnelFileId, cancellationToken);
if (substitute is null)
    return Fail(await personnelFileRepository.ExistsOutsideTenantAsync(input.SubstitutePersonnelFileId, cancellationToken)
        ? AuthorizationSubstitutionErrors.SubstituteNotEligible   // cross-tenant ⇒ no se filtra info
        : AuthorizationSubstitutionErrors.SubstituteNotFound);
if (substitute.RecordType != PersonnelFileRecordType.Employee
    || substitute.LifecycleStatus != PersonnelFileLifecycleStatus.Completed
    || !substitute.IsActive)
    return Fail(AuthorizationSubstitutionErrors.SubstituteNotEligible);

// 3) La plaza pertenece a una asignación ACTIVA del sustituto (D-02 / RF-003)
var substituteAssignments = await employeeRepository.GetEmploymentAssignmentsAsync(input.SubstitutePersonnelFileId, cancellationToken);
var ownedSlot = substituteAssignments.FirstOrDefault(a => a.IsActive && a.PositionSlotId == input.SubstitutePositionSlotPublicId);
if (ownedSlot is null)
    return Fail(AuthorizationSubstitutionErrors.PositionNotOwned);

// 4) Reglas de vigencia/disponibilidad (D-04/D-06/D-07)
var titularSubs = (await employeeRepository.GetAuthorizationSubstitutionsAsync(personnelFile.PublicId, cancellationToken))
    .Select(s => new AuthorizationSubstitutionRules.ExistingSubstitution(s.Id, s.StartDate, s.EndDate, s.IsActive)).ToArray();
var substituteAsTitular = (await employeeRepository.GetAuthorizationSubstitutionsAsync(input.SubstitutePersonnelFileId, cancellationToken))
    .Select(s => new AuthorizationSubstitutionRules.ExistingSubstitution(s.Id, s.StartDate, s.EndDate, s.IsActive)).ToArray();
var candidate = new AuthorizationSubstitutionRules.Candidate(
    existingPublicIdOrNull, input.StartDate, input.EndDate!.Value, isActiveForThisOperation);
var evaluation = AuthorizationSubstitutionRules.Evaluate(candidate, titularSubs, substituteAsTitular);
if (evaluation.IsFailure) return Fail(evaluation.Error);

// 5) Snapshot del título de la plaza (D-02) — desde ownedSlot o el PositionSlot resuelto
var titleSnapshot = ownedSlot.PositionTitle; // o resolver vía PositionSlot si la respuesta de asignación no lo trae
```

Notas:
- En **Add**, `candidate.PublicId = null` e `isActive = input.IsActive`. En **Update**, `PublicId = command.AuthorizationSubstitutionPublicId` e `isActive = existing.IsActive` (PUT no muta `IsActive`). En **Patch**, `isActive = state.IsActive` (puede mutar).
- **Delete** no requiere reglas nuevas (no hay invariante de "debe quedar uno"); se conserva tal cual.
- El `GetAuthorizationSubstitutionsAsync(input.SubstitutePersonnelFileId)` devuelve las sustituciones **donde el sustituto es titular** — exactamente lo necesario para D-06.

### 3.6 Permiso dedicado `PersonnelFiles.ManageSubstitutions` (D-09)

Patrón de `AuthorizeRehire`/`ViewCompensation`:

| Archivo | Cambio |
|---|---|
| `Features/PersonnelFiles/Common/PersonnelFileCommon.cs` | `public const string ManageSubstitutions = "PersonnelFiles.ManageSubstitutions";` |
| `Features/PersonnelFiles/Common/PersonnelFilePolicies.cs` | `public const string ManageSubstitutions = "PersonnelFiles.ManageSubstitutions";` |
| `Features/Provisioning/Common/ProvisioningConstants.cs` | Entrada en `CompanyAdminPermissions` (code, nombre ES, descripción ES, módulo `PersonnelFiles`, screen `PersonnelFiles`, action `ManageSubstitutions`) → el **owner** lo recibe. |
| `Api/Program.cs` | Registrar política: `RequireAssertion(PermissionClaimEvaluator.HasAnyPermission(ctx, ManageSubstitutions, Admin, ManageAdministration))`. |
| `Abstractions/PersonnelFiles/IPersonnelFileAuthorizationService.cs` | `Task<Result> EnsureCanManageSubstitutionsAsync(Guid companyId, CancellationToken ct);` |
| `Infrastructure/PersonnelFiles/PersonnelFileAuthorizationService.cs` | Implementar con `EnsureHasAnyClaimAsync([ManageSubstitutions, Admin, ManageAdministration], RbacPermissionAction.Update, ct)`. |
| `Features/PersonnelFiles/Common/PersonnelFileEmployeeHandlerBases.cs` | Nuevo `LoadForManageSubstitutionsAsync<T>(...)` (copia de `LoadForManageAsync` pero llamando `EnsureCanManageSubstitutionsAsync`). |
| `AuthorizationSubstitutions.Handlers.cs` (`:34,108,195,302`) | Reemplazar `LoadForManageAsync` → `LoadForManageSubstitutionsAsync` en los 4 handlers de escritura. Lecturas siguen con `PersonnelFiles.Read`. |
| `Api/Controllers/PersonnelFileEmploymentController.cs` | (Opcional) `[AuthorizationPolicySet(PersonnelFilePolicies.ManageSubstitutions, PersonnelFilePolicies.ManageSubstitutions)]` en los métodos de escritura de sustitución. |

### 3.7 API — contratos y controlador

`Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:194-220`:
```csharp
public sealed record AddAuthorizationSubstitutionRequest(
    string SubstitutionTypeCode, Guid SubstitutePersonnelFilePublicId,
    Guid SubstitutePositionSlotPublicId,          // CAMBIO (era string? SubstitutePositionTitle)
    DateTime StartDate, DateTime EndDate,          // CAMBIO (era DateTime?)
    bool IsActive, string? Notes);

public sealed record UpdateAuthorizationSubstitutionRequest(
    string SubstitutionTypeCode, Guid SubstitutePersonnelFilePublicId,
    Guid SubstitutePositionSlotPublicId, DateTime StartDate, DateTime EndDate, string? Notes);

public sealed class PatchAuthorizationSubstitutionRequest
{
    public string SubstitutionTypeCode { get; set; } = string.Empty;
    public Guid SubstitutePersonnelFileId { get; set; }
    public Guid SubstitutePositionSlotId { get; set; }   // CAMBIO
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }               // PATCH conserva nullable; applier exige presencia
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
```
`PersonnelFileEmploymentController.cs:600-704`: actualizar el mapeo request→`AuthorizationSubstitutionInput` (pasar `SubstitutePositionSlotPublicId`/`SubstitutePositionSlotId`). El resto (`[FromIfMatch]`, `ToCreatedAtActionResult`, ETag) no cambia.

### 3.8 Infraestructura — repositorio

`IPersonnelFileEmployeeRepository` + `PersonnelFileEmployeeRepository.cs`:
- `UpdateAuthorizationSubstitutionAsync` / `PatchAuthorizationSubstitutionAsync` (`:579-620`): cambiar parámetro `string? substitutePositionTitle` → `Guid substitutePositionSlotPublicId` (+ `string? positionTitleSnapshot`), y la llamada interna `item.Update(...)`.
- `AddAuthorizationSubstitutionAsync`: sin cambios de firma (recibe la entidad ya construida).
- `Map` (`:1896-1905`): proyectar `SubstitutePositionSlotPublicId` + `SubstitutePositionTitleSnapshot`.
- **No** se requieren métodos nuevos: se reusa `GetAuthorizationSubstitutionsAsync` y `GetEmploymentAssignmentsAsync` (ambos existentes) para reglas y propiedad de plaza; y `IPersonnelFileRepository.GetForAccessCheckAsync`/`ExistsOutsideTenantAsync` para elegibilidad. *(Confirmar que `PersonnelFileEmploymentAssignmentResponse` expone `PositionSlotId` y un título de plaza; si no trae título, resolverlo vía `IPositionSlotRepository.GetByIdAsync` para el snapshot.)*

### 3.9 Infraestructura — EF config y migración

**Config** `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs:164-200`:
```csharp
builder.Property(i => i.SubstitutePositionSlotPublicId).HasColumnName("substitute_position_slot_public_id");          // NUEVO
builder.Property(i => i.SubstitutePositionTitleSnapshot).HasColumnName("substitute_position_title_snapshot").HasMaxLength(120); // NUEVO
builder.Property(i => i.EndDate).HasColumnName("end_date").IsRequired();                                              // CAMBIO
// quitar: builder.Property(i => i.SubstitutePositionTitle)...
builder.HasIndex(i => i.SubstitutePositionSlotPublicId)
    .HasDatabaseName("ix_personnel_file_authorization_substitutions__substitute_slot");                              // opcional
// check constraint: "end_date >= start_date"  (antes: "end_date is null or end_date >= start_date")
```
Nueva config de catálogo: `Configurations/GeneralCatalogs/SubstitutionTypeCatalogItemConfiguration.cs` (§3.2.d). Ambas se autodescubren por `ApplyConfigurationsFromAssembly`.

**Migración** (un solo migration):
```bash
dotnet ef migrations add SubstitutionTypesAndHardenAuthSubstitution \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```
Contendrá: `CreateTable substitution_type_catalog_items` (+ índices + FK a `country_catalog_items`) · `insertData` seed SV · `AddColumn substitute_position_slot_public_id` + `substitute_position_title_snapshot` · `AlterColumn end_date` (NOT NULL) · `DropColumn substitute_position_title` · `DropCheckConstraint`/`AddCheckConstraint` (`end_date >= start_date`). Validar luego con `dotnet ef migrations has-pending-model-changes`.

### 3.10 Localización (6 códigos × 3 resx)

Añadir a `BackendMessages.resx` (EN), `BackendMessages.es.resx` (ES) y `BackendMessages.es-SV.resx` (SV):

| Code | EN | ES |
|---|---|---|
| `AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_FOUND` | The selected substitute could not be found. | No se encontró al sustituto seleccionado. |
| `AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_ELIGIBLE` | The selected substitute must be an active, completed employee in the same company. | El sustituto debe ser un empleado activo y completado de la misma empresa. |
| `AUTHORIZATION_SUBSTITUTION_TYPE_CODE_INVALID` | The substitution type code is not valid for the active catalog. | El tipo de sustitución no es válido en el catálogo activo. |
| `AUTHORIZATION_SUBSTITUTION_POSITION_NOT_OWNED` | The selected position must be an active assignment of the substitute. | La plaza seleccionada debe ser una asignación activa del sustituto. |
| `AUTHORIZATION_SUBSTITUTION_PERIOD_OVERLAP` | The employee already has an active substitution with an overlapping effective period. | El empleado ya tiene una sustitución activa con un período vigente que se solapa. |
| `AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_UNAVAILABLE` | The substitute is unavailable: they are being substituted (absent) during an overlapping period. | El sustituto no está disponible: está siendo sustituido (ausente) en un período que se solapa. |

### 3.11 Auditoría con diff + historial visible (D-12)

- La auditoría ya se invoca en cada operación (`PersonnelFileEmployeeAudits.LogUpdateAsync`). Asegurar que el log capture **estado anterior y nuevo** (pasar el `existing` además del `response` en Update/Patch/Delete; hoy se pasa solo el resultado).
- **Historial visible**: confirmar si existe un endpoint/consulta de auditoría reutilizable por entidad. Si existe, exponer el filtro por esta entidad; si no, es un pequeño RF adicional (lectura de auditoría) — **a confirmar** con el módulo de Audit. *(Único punto que requiere verificación de infraestructura existente.)*

---

## 4. Migración de datos (cambio breaking)

`SubstitutePositionTitle → SubstitutePositionSlotPublicId`, `EndDate` NOT NULL y `SubstitutionTypeCode` a catálogo **rompen** datos existentes.

- **Sin datos en QA/prod (recomendado verificar — S1):** la migración hace `DropColumn`/`AddColumn` directo y `AlterColumn NOT NULL` sin backfill. Camino simple.
- **Con datos:** dividir en pasos — (1) `AddColumn` slot id **nullable** + snapshot; (2) script de backfill (mapear título→plaza principal del sustituto; completar `end_date` faltantes; normalizar tipos al catálogo); (3) `AlterColumn` slot id/end_date a NOT NULL + `DropColumn` título. Documentar el script.

> Acción previa a la implementación: **confirmar existencia de datos** en `personnel_file_authorization_substitutions` (QA/prod). El resultado determina el camino de migración.

---

## 5. Mapa de errores (resumen)

| Disparador | Código | ErrorType → HTTP | Capa |
|---|---|---|---|
| `endDate` ausente | `common.validation` (campo `endDate`) | Validation → **400** | Validador |
| `substitutePositionSlotPublicId` vacío | `common.validation` | Validation → **400** | Validador |
| Sustituto no existe | `…SUBSTITUTE_NOT_FOUND` | NotFound → **404** | Handler |
| Sustituto inelegible / cross-tenant | `…SUBSTITUTE_NOT_ELIGIBLE` | UnprocessableEntity → **422** | Handler |
| Tipo fuera de catálogo | `…TYPE_CODE_INVALID` | UnprocessableEntity → **422** | Handler |
| Plaza no es del sustituto | `…POSITION_NOT_OWNED` | UnprocessableEntity → **422** | Handler |
| Solape con sustitución activa del titular | `…PERIOD_OVERLAP` | Conflict → **409** | Reglas |
| Sustituto ausente/sustituido | `…SUBSTITUTE_UNAVAILABLE` | UnprocessableEntity → **422** | Reglas |
| Auto-sustitución (existente) | `common.validation` | Validation → **400** | Handler |
| `If-Match` no coincide (existente) | `CONCURRENCY_CONFLICT` | Conflict → **409** | Handler |
| Sin `ManageSubstitutions` | (política) | Forbidden → **403** | API/Policy |

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- `AuthorizationSubstitutionRulesTests` (nuevo) — espejo de `EmploymentAssignmentRulesTests`: solape sí/no (incluyendo bordes y períodos futuros no solapados), sustituto ocupado, candidata inactiva (no aplica), `[Theory]` de overlap.
- `PersonnelFileAuthorizationSubstitutionPatchTests` (existente) — actualizar: segmento `substitutePositionSlotId`, `endDate` **no removible**, nuevos requeridos en `Validate`.
- `BackendMessageLocalizationTests` (existente) — debe seguir verde con los 6 códigos nuevos en EN+ES.
- (Opcional) tests de handler con mocks (`IPersonnelFileEmployeeRepository`, `IPersonnelFileRepository`, `IPersonnelFileAuthorizationService`, `IAuditService`, `ITenantContext`, `IUnitOfWork`) para las rutas de error 404/422/409 y 403.

**Integración (`tests/CLARIHR.Api.IntegrationTests/`):**
- `IntegrationTestSeeder`: sembrar `substitution-types` (país de prueba) + permiso `ManageSubstitutions` en rol de prueba + un empleado sustituto con plaza activa.
- Casos: alta feliz; sustituto inexistente/inelegible; tipo inválido; plaza no propia; solape bloqueado; sustituto no disponible; `endDate` requerida; 403 sin permiso; 409 por `If-Match`.

**Guardrail:** `GeneralCatalogKeyMapGuardrailsTests` valida que `substitution-types` ↔ `CurriculumSubstitutionType` quede biyectivo.

---

## 7. Orden de implementación (PRs sugeridos)

1. **PR-1 — Catálogo `substitution-types`** (§3.2): dominio + constante + key map + EF config + DbSet + switch de validación + seed SV + migración parcial (solo `CreateTable`+`insertData`). Bajo riesgo, aislado. Verde con guardrail.
2. **PR-2 — Permiso `ManageSubstitutions`** (§3.6): constante + política + semilla + servicio + base de handler. Aislado.
3. **PR-3 — Endurecimiento del agregado + EF + migración** (§3.1, §3.9): entidad (slot id, endDate NOT NULL, snapshot), config, migración (alter/drop), estrategia de datos (§4).
4. **PR-4 — Aplicación** (§3.3, §3.4, §3.5): reglas, validadores, patch applier, validaciones de handler + invocación de reglas + swap de permiso.
5. **PR-5 — API + repo + localización + tests** (§3.7, §3.8, §3.10, §6): contratos, mapeo, firmas de repo + `Map`, 6 errores × 3 resx, batería de pruebas.

> PR-3/4/5 pueden fusionarse si se prefiere un solo cambio cohesivo; PR-1 y PR-2 conviene aislarlos.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Datos existentes (§4):** el cambio de columna es breaking; confirmar datos antes de elegir camino. *Mitiga:* verificación previa (S1) + backfill documentado.
- **R-T2 — Snapshot de título de plaza:** si `PersonnelFileEmploymentAssignmentResponse` no expone el título de la plaza, resolver vía `IPositionSlotRepository.GetByIdAsync` (una consulta extra) para `SubstitutePositionTitleSnapshot`.
- **R-T3 — Historial visible (D-12):** depende de si ya existe consulta de auditoría por entidad; si no, añadir un endpoint de lectura (pequeño alcance adicional). **Único punto sin patrón verificado.**
- **R-T4 — Semántica de "activo":** se adoptó solape-de-vigencias (no flag único permanente), permitiendo sustituciones futuras no solapadas. Si el negocio quisiera "una sola activa jamás", es un endurecimiento trivial en `Evaluate` (cualquier otra `IsActive` bloquea).
- **R-T5 — Inconsistencia de naming heredada:** `Add/Update` usan `SubstitutePersonnelFilePublicId` y `Patch` usa `SubstitutePersonnelFileId`. Mantener tal cual (fuera de alcance) o normalizar en PR-5.

---

## 9. Checklist de implementación

- [ ] **Dominio:** entidad con `SubstitutePositionSlotPublicId`, `SubstitutePositionTitleSnapshot`, `EndDate` no-nullable; `Create/Update` nuevos.
- [ ] **Catálogo:** clase + constante + key map + EF config + DbSet + switch `CatalogCodeIsActiveAsync` + seed SV (6 valores).
- [ ] **Reglas:** `AuthorizationSubstitutions.Rules.cs` (errores + `Evaluate`) reusando `RangesOverlap`.
- [ ] **Aplicación:** `AuthorizationSubstitutionInput`/response + validadores + patch state/applier (slot id, endDate requerida/no removible).
- [ ] **Handlers:** validación de catálogo + sustituto elegible + plaza propia + reglas; swap a `LoadForManageSubstitutionsAsync`.
- [ ] **Permiso:** constante + política + `Program.cs` + semilla owner + servicio (interfaz+impl) + base de handler.
- [ ] **Infra:** firmas de repo (Update/Patch) + `Map`; EF config (entidad + catálogo); 1 migración + estrategia de datos.
- [ ] **API:** contratos (Add/Update/Patch) + mapeo en controlador + política en endpoints de escritura.
- [ ] **Localización:** 6 códigos en EN + es + es-SV.
- [ ] **Tests:** rules unit + patch unit + paridad + guardrail + integración (felices/errores/403/409) + seeder.
- [ ] **Verificación:** `dotnet build`, `dotnet test`, `dotnet ef migrations has-pending-model-changes` (sin pendientes).

---

> **Trazabilidad.** Este plan implementa la Fase 1 del análisis de negocio (D-01…D-12, RF-001…RF-009). RF-010 (delegación efectiva + vínculo con Ausencias) queda **diferido**. Todo cambio sigue patrones verificados en el código (catálogos country-scoped, `EmploymentAssignmentRules`, permisos `AuthorizeRehire`/`ViewCompensation`, recursos `BackendMessages*`).
