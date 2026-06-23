# Plan Técnico de Implementación — Seguros del Empleado y Beneficiarios (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-seguros-empleado.md`](../business/analisis-seguros-empleado.md) (decisiones D-01…D-15) |
| **Módulos** | `PersonnelFiles` (Compensación/Beneficios) · Catálogos de referencia país (`PersonnelReferenceCatalog*`) · Catálogo `Currency` (GeneralCatalogs) · `IdentityAccess`/Provisioning (RBAC) · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-22 |

---

## 1. Objetivo y enfoque

Endurecer la funcionalidad **ya existente** `PersonnelFileInsurance` + `PersonnelFileInsuranceBeneficiary` (CRUD documental) conforme a las decisiones **D-01…D-15**, manteniéndola como **beneficio informativo** que **no** afecta nómina (D-01).

**Insight central del análisis de código.** Las entidades, el CQRS (Add/Update/Patch/Delete/Get/GetList para seguro y beneficiario), la API (12 endpoints), la concurrencia (`ConcurrencyToken`/`If-Match`) y la auditoría (`IAuditService`) **ya están construidos y funcionando**. **No se agregan campos estructurales al seguro**; el trabajo es:

1. **Catalogar nombre de seguro y rango** con **catálogos de referencia país** (D-02/D-03) — patrón **idéntico** a `Kinship` (plano) y `Municipality→Department` (jerárquico). El rango cuelga del seguro vía FK (igual que municipio→departamento).
2. **Reusar** el catálogo existente `IdentificationType` para el **tipo de documento** del beneficiario (D-10) y el catálogo existente `Currency` para validar la **moneda** ISO-4217 (D-12).
3. **Enriquecer la entidad beneficiario** con 3 campos: `DocumentTypeCode`, `AllocationPercentage`, `BeneficiaryType` (D-09/D-10).
4. **Añadir un módulo de reglas puro** `Insurances.Rules.cs` (espejo de `EmploymentAssignmentRules`) para anti-duplicado y suma de asignación (D-09, D-13).
5. **Endurecer validadores**: fechas (`Start ≤ End`), montos `≥ 0`, `%` ∈ [0,100], catálogos en handler.
6. **Añadir un permiso dedicado de lectura** `PersonnelFiles.ViewInsurance` (D-11) — espejo exacto de `ViewCompensation`.
7. **Completar la auditoría** con diff antes/después + historial visible (D-15).
8. **Localizar** ~3 errores nuevos de reglas (EN/es/es-SV).

Todo sigue patrones ya existentes (catálogos de referencia país tipados, `EmploymentAssignmentRules`, permiso `ViewCompensation`, recursos `BackendMessages*`). **Sin datos productivos** (D-14): **drop & recreate**, sin backfill.

---

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Agregado seguro | `PersonnelFileInsurance` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:774-889`): `InsuranceCode`, `EmployeeContribution?`, `EmployerContribution?`, `RangeCode?`, `PolicyNumber?`, `InsuredAmount?`, `CurrencyCode?`, `IsActive`, `StartDateUtc?`, `EndDateUtc?`, `Beneficiaries`, `ConcurrencyToken`. | **Sin cambios de campos.** Solo validación nueva en handler/validador/reglas. |
| 2 | Agregado beneficiario | `PersonnelFileInsuranceBeneficiary` (`…PersonnelFileEmployee.cs:891-955`): `FullName`, `DocumentNumber?`, `BirthDate?`, `KinshipCode`, `IsActive`, `ConcurrencyToken`. `Create(:949)`/`Update(:930)`. | **Añadir** `DocumentTypeCode?`, `AllocationPercentage?`, `BeneficiaryType?`; cambiar firmas `Create/Update`. |
| 3 | CQRS seguro | `Compensation/Insurances.cs`: `InsuranceInput(:39)`, comandos/queries, `InsuranceInputValidator(:90)` (solo `InsuranceCode NotEmpty/Max80`), patch state(`:164`)+applier (`Insurances.Handlers.cs:397`). | Endurecer validador (montos, fechas); **sin** nuevos campos en `InsuranceInput`. |
| 4 | CQRS beneficiario | `Compensation/InsuranceBeneficiaries.cs`: `InsuranceBeneficiaryInput`, validadores; patch state+applier (`InsuranceBeneficiaries.Handlers.cs:409`). | Añadir 3 campos a `InsuranceBeneficiaryInput`/response; nuevos segmentos de patch. |
| 5 | Handlers seguro | `Insurances.Handlers.cs`: estado `IsCompletedEmployee(:46)`, `LoadForManageAsync(:34,102,183,284)`, `LoadCompletedEmployeeForReadAsync(:351,379)`, transacción + `PersonnelFileEmployeeAudits.LogUpdateAsync`. **NO** valida catálogos, fechas, montos ni duplicados. | Insertar validación de catálogo + invocación de reglas + swap del read-gate. |
| 6 | Handlers beneficiario | `InsuranceBeneficiaries.Handlers.cs`: `ValidateKinshipCodeAsync(:51,116,231)` ya valida parentesco. | Añadir validación de `DocumentTypeCode`, `BeneficiaryType`, `%`, anti-duplicado y suma. |
| 7 | Reglas (patrón oro) | `EmploymentAssignments.Rules.cs`: `internal static class EmploymentAssignmentErrors` + `Evaluate(...) : Result<Evaluation>`; `RangesOverlap(...)`. Namespace `CLARIHR.Application.Features.PersonnelFiles`. | Crear `Insurances.Rules.cs` espejo (anti-duplicado + suma 100 %). |
| 8 | Catálogos de referencia (tipados) | Entidades `PersonnelReferenceCatalogItem.cs`: base `PersonnelReferenceCatalogItemBase(:5)`; `KinshipCatalogItem(:108)` (plano), `MunicipalityCatalogItem(:164)` con FK `DepartmentCatalogItemId(:184)` (jerárquico), `IdentificationTypeCatalogItem(:24)`. Config base `PersonnelReferenceCatalogItemConfigurationBase<T>(:7)`; `MunicipalityCatalogItemConfiguration(:125)` con FK(`:145`). Categorías en `PersonnelReferenceCatalog.cs:168`. | Añadir `InsuranceTypeCatalogItem` (espejo Department) e `InsuranceRangeCatalogItem` (espejo Municipality). |
| 9 | Validación de catálogo de referencia | `PersonnelReferenceCatalogValidation(:128)` en `Catalogs/PersonnelReferenceCatalogs.cs`: `ValidateKinshipCodeAsync(:191)`, `ValidateIdentificationTypeCodeAsync(:178)` (**ya existe**), patrón depto/municipio en `ValidateBirthLocationAsync(:205)`; helper `ValidateOptionalReferenceCodeForCompanyAsync(:337)`. Backend: `IPersonnelFileRepository.ReferenceCatalogCodeIsActiveAsync` / `ReferenceMunicipalityBelongsToDepartmentAsync`. | Añadir `ValidateInsuranceTypeCodeAsync` + `ValidateInsuranceRangeCodeAsync` (con verificación de pertenencia al seguro). Reusar `ValidateIdentificationTypeCodeAsync`. |
| 10 | Catálogo de moneda | Categoría `Currency` (`PersonnelCurriculumCatalogCategories.Currency`, `PersonnelReferenceCatalogs.cs:96`); validación vía `PersonnelCurriculumCatalogValidation.ValidateCodeAsync(:109)` → `repository.CatalogCodeIsActiveAsync(tenantId, category, code)`. | Validar `CurrencyCode` con el catálogo **existente** (confirmar que `CatalogCodeIsActiveAsync` resuelve `"CURRENCY"`). |
| 11 | Exposición de catálogos a UI | `GetPersonnelReferenceCatalogItemsQuery(Category, CountryCode, ParentCode?)` (`:34`) → `GetReferenceCatalogItemsAsync(countryCode, category, parentCode, ct)`. **Ya soporta `parentCode`**. | Dropdown dependiente seguro→rango **gratis** al añadir los `switch` del repo. |
| 12 | Repositorio de catálogos | `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs`: `ReferenceCatalogCodeIsActiveAsync(:1460)` (switch por categoría → `IsCountryScopedCatalogCodeActiveAsync<T>(:2487)`), `GetReferenceCatalogItemsAsync(:1370)` (switch → `GetFlatReferenceCatalogItemsAsync<T>(:2412)` / `GetMunicipalityCatalogItemsAsync(:2429)`), `ReferenceMunicipalityBelongsToDepartmentAsync(:1482)`. | Añadir casos `INSURANCETYPE`/`INSURANCERANGE` + helper `GetInsuranceRangeCatalogItemsAsync` + `ReferenceInsuranceRangeBelongsToTypeAsync`. |
| 13 | Repositorio seguro/beneficiario | `IPersonnelFileEmployeeRepository(:421-518)`; impl `PersonnelFileEmployeeRepository.cs`: insurance Add(`:943`)/Update(`:959`)/Patch(`:981`)/Delete(`:1010`)/Get(`:1022,1037`); beneficiary Add(`:1049`)/Get(`:1069,1081`)/Update(`:1097`)/Patch(`:1120`)/Delete(`:1150`); `Map(:1853,1880)`. | Ampliar firmas Update/Patch beneficiario (+3 params); `Map` proyecta 3 campos. **Reusar** Get* para reglas (anti-duplicado/suma). |
| 14 | Permisos | `PersonnelFilePermissionCodes(PersonnelFileCommon.cs:82)`; `PersonnelFilePolicies`; `Program.cs:444` (política `ViewCompensation` authn-only); `IPersonnelFileAuthorizationService.EnsureCanViewCompensationAsync`; impl `PersonnelFileAuthorizationService.cs:27` (`EnsureHasAnyClaimAsync:96`). Read-gate de compensación `PersonnelFileEmployeeHandlerBases.cs:248`. | Añadir `ViewInsurance` (constante + política + semilla + servicio + read-gate). |
| 15 | API | Contratos `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:366-422`; controlador `PersonnelFileCompensationController.cs:27` (`[AuthorizationPolicySet(Read, Manage)]` de clase), endpoints seguro/beneficiario `:570-923`. | Ampliar DTOs de beneficiario (+3 campos) + mapeo; `[AuthorizationPolicySet(ViewInsurance, Manage)]` a nivel de método. |
| 16 | EF config | `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs:316` (`personnel_file_insurances`), `:378` (`personnel_file_insurance_beneficiaries`). **Sin** check constraints. | Añadir 3 columnas al beneficiario; nuevas config de catálogo; (opcional) índices de apoyo anti-duplicado. |
| 17 | DbContext / migraciones | `ApplicationDbContext.cs:255-259` (DbSets de catálogos); `ApplyConfigurationsFromAssembly`. Comandos EF en `docs/technical/operations/manual-migrations-and-azure-deploy.md`. | 2 DbSets nuevos + 1 migración (tablas de catálogo + seed SV + 3 columnas beneficiario). |
| 18 | Localización | `Error(code,msg,ErrorType)` en `*Errors.cs`; recursos `BackendMessages.resx`/`.es.resx`/`.es-SV.resx`; paridad `BackendMessageLocalizationTests`. | ~3 códigos nuevos × 3 resx (duplicados/suma). |

---

## 3. Arquitectura de la solución

### 3.1 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs`

**El seguro NO cambia estructuralmente.** Solo el **beneficiario** (`:891-955`) recibe 3 campos:

```csharp
public string? DocumentTypeCode { get; private set; }      // NUEVO (catálogo IdentificationType, D-10)
public decimal? AllocationPercentage { get; private set; } // NUEVO (0–100, D-09)
public string? BeneficiaryType { get; private set; }        // NUEVO ("PRINCIPAL" | "CONTINGENTE", D-09)
```

Ajustar el constructor/`Create(:949)`/`Update(:930)`:

```csharp
private PersonnelFileInsuranceBeneficiary(
    string fullName, string? documentNumber, DateTime? birthDate, string kinshipCode,
    string? documentTypeCode, decimal? allocationPercentage, string? beneficiaryType)
{
    // ...existente...
    DocumentTypeCode = PersonnelFileNormalization.CleanOptional(documentTypeCode);
    AllocationPercentage = allocationPercentage;
    BeneficiaryType = PersonnelFileNormalization.CleanOptional(beneficiaryType);
}

public static PersonnelFileInsuranceBeneficiary Create(
    string fullName, string? documentNumber, DateTime? birthDate, string kinshipCode,
    string? documentTypeCode, decimal? allocationPercentage, string? beneficiaryType) => new(...);

public void Update(
    string fullName, string? documentNumber, DateTime? birthDate, string kinshipCode,
    string? documentTypeCode, decimal? allocationPercentage, string? beneficiaryType)
{ ConcurrencyToken = Guid.NewGuid(); /* set all */ }
```

> **`BeneficiaryType`**: se modela como **código string constreñido** a `{PRINCIPAL, CONTINGENTE}` (validado en el módulo de reglas), por consistencia con el estilo "code" del repo y para **no** crear un catálogo (el negocio no lo pidió). Alternativa: enum de dominio `InsuranceBeneficiaryType { Primary, Contingent }` persistido como string. Default = `PRINCIPAL`.

### 3.2 Catálogos de referencia país: `InsuranceType` (plano) e `InsuranceRange` (jerárquico)

Patrón **idéntico** a `Department` (plano) y `Municipality→Department` (jerárquico). El rango cuelga del seguro vía **FK explícita** (no por un `ParentId` genérico).

**a) Entidades** — `Domain/PersonnelFiles/PersonnelReferenceCatalogItem.cs` (espejo de `DepartmentCatalogItem(:136)` y `MunicipalityCatalogItem(:164)`):

```csharp
public sealed class InsuranceTypeCatalogItem : PersonnelReferenceCatalogItemBase
{
    private InsuranceTypeCatalogItem() { }
    private InsuranceTypeCatalogItem(Guid publicId, long countryCatalogItemId, string countryCode,
        string code, string name, bool isActive, int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder) { }
    public static InsuranceTypeCatalogItem Create(long countryCatalogItemId, string countryCode,
        string code, string name, bool isActive, int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class InsuranceRangeCatalogItem : PersonnelReferenceCatalogItemBase
{
    private InsuranceRangeCatalogItem() { }
    private InsuranceRangeCatalogItem(Guid publicId, long countryCatalogItemId, string countryCode,
        string code, string name, bool isActive, int sortOrder, long insuranceTypeCatalogItemId)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
        { SetInsuranceType(insuranceTypeCatalogItemId); }

    public long InsuranceTypeCatalogItemId { get; private set; }
    public InsuranceTypeCatalogItem? InsuranceTypeCatalogItem { get; private set; }

    public static InsuranceRangeCatalogItem Create(long countryCatalogItemId, string countryCode,
        string code, string name, bool isActive, int sortOrder, long insuranceTypeCatalogItemId) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, insuranceTypeCatalogItemId);

    private void SetInsuranceType(long insuranceTypeCatalogItemId)
    {
        if (insuranceTypeCatalogItemId == 0)
            throw new ArgumentOutOfRangeException(nameof(insuranceTypeCatalogItemId));
        InsuranceTypeCatalogItemId = insuranceTypeCatalogItemId;
        RefreshConcurrencyToken();
    }
}
```

**b) Constantes de categoría** — `Domain/PersonnelFiles/PersonnelReferenceCatalog.cs` → `PersonnelReferenceCatalogCategories(:168)`:
```csharp
public const string InsuranceType = "InsuranceType";
public const string InsuranceRange = "InsuranceRange";
```
> `PersonnelReferenceCatalog.BuildItems()` (lista estática con `ParentId`) parece **legado** tras la unificación a tablas tipadas (`grep` muestra que `Items` solo se referencia a sí misma). El **seed real** va en la migración (§3.10). *(Verificar; si `BuildItems()` aún se consume en algún seeder, añadir ahí también.)*

**c) EF config** — `Configurations/PersonnelFiles/PersonnelReferenceCatalogItemConfiguration.cs` (espejo de `DepartmentCatalogItemConfiguration(:111)` y `MunicipalityCatalogItemConfiguration(:125)`):
```csharp
internal sealed class InsuranceTypeCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<InsuranceTypeCatalogItem>
{
    public InsuranceTypeCatalogItemConfiguration() : base(
        "insurance_type_catalog_items", "pk_insurance_type_catalog_items",
        "uq_insurance_type_catalog_items__public_id", "uq_insurance_type_catalog_items__country_code",
        "ix_insurance_type_catalog_items__country_active_sort") { }
}

internal sealed class InsuranceRangeCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<InsuranceRangeCatalogItem>
{
    public InsuranceRangeCatalogItemConfiguration() : base(
        "insurance_range_catalog_items", "pk_insurance_range_catalog_items",
        "uq_insurance_range_catalog_items__public_id", "uq_insurance_range_catalog_items__country_code",
        "ix_insurance_range_catalog_items__country_active_sort") { }

    public override void Configure(EntityTypeBuilder<InsuranceRangeCatalogItem> builder)
    {
        base.Configure(builder);
        builder.Property(i => i.InsuranceTypeCatalogItemId).HasColumnName("insurance_type_catalog_item_id");
        builder.HasOne(i => i.InsuranceTypeCatalogItem).WithMany()
            .HasForeignKey(i => i.InsuranceTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_insurance_range_catalog_items__insurance_type");
        builder.HasIndex(i => new { i.InsuranceTypeCatalogItemId, i.IsActive, i.SortOrder })
            .HasDatabaseName("ix_insurance_range_catalog_items__type_active_sort");
    }
}
```

**d) DbSets** — `Persistence/ApplicationDbContext.cs:255-259` (junto a `KinshipCatalogItems`/`MunicipalityCatalogItems`):
```csharp
public DbSet<InsuranceTypeCatalogItem> InsuranceTypeCatalogItems => Set<InsuranceTypeCatalogItem>();
public DbSet<InsuranceRangeCatalogItem> InsuranceRangeCatalogItems => Set<InsuranceRangeCatalogItem>();
```

**e) Repositorio** — `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs`:
- `ReferenceCatalogCodeIsActiveAsync(:1460)` switch:
```csharp
"INSURANCETYPE" => IsCountryScopedCatalogCodeActiveAsync<InsuranceTypeCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
"INSURANCERANGE" => IsCountryScopedCatalogCodeActiveAsync<InsuranceRangeCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
```
- `GetReferenceCatalogItemsAsync(:1370)` switch:
```csharp
"INSURANCETYPE" => await GetFlatReferenceCatalogItemsAsync<InsuranceTypeCatalogItem>(countryCatalogItemId.Value, cancellationToken),
"INSURANCERANGE" => await GetInsuranceRangeCatalogItemsAsync(countryCatalogItemId.Value, normalizedParentCode, cancellationToken),
```
- Nuevo helper `GetInsuranceRangeCatalogItemsAsync` (espejo de `GetMunicipalityCatalogItemsAsync(:2429)`, filtrando por `InsuranceTypeCatalogItem.NormalizedCode == parentCode`).
- Nuevo `ReferenceInsuranceRangeBelongsToTypeAsync(countryCode, insuranceTypeCode, insuranceRangeCode, ct)` (espejo de `ReferenceMunicipalityBelongsToDepartmentAsync(:1482)`) → añadir a `IPersonnelFileRepository`.

### 3.3 Validación de catálogos (Application)

**a) Nuevos validadores** en `PersonnelReferenceCatalogValidation` (`Catalogs/PersonnelReferenceCatalogs.cs:128`), espejo de `ValidateKinshipCodeAsync(:191)`:
```csharp
public static Task<Error> ValidateInsuranceTypeCodeAsync(
    IPersonnelFileRepository repository, Guid companyId, string fieldName, string insuranceTypeCode, CancellationToken ct) =>
    ValidateOptionalReferenceCodeForCompanyAsync(repository, companyId, fieldName,
        PersonnelReferenceCatalogCategories.InsuranceType, insuranceTypeCode, ct);

// Rango: valida existencia + pertenencia al seguro (cuando ambos vienen). Espejo de la lógica depto/municipio.
public static async Task<Error> ValidateInsuranceRangeCodeAsync(
    IPersonnelFileRepository repository, Guid companyId, string insuranceTypeCode, string? rangeCode, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(rangeCode)) return Error.None; // opcional (D-03)
    var country = await ResolveCompanyCountryCodeAsync(repository, companyId, ct);
    var exists = await ValidateOptionalReferenceCodeAsync(repository, "rangeCode", country,
        PersonnelReferenceCatalogCategories.InsuranceRange, rangeCode, ct);
    if (exists != Error.None) return exists;
    return await repository.ReferenceInsuranceRangeBelongsToTypeAsync(country, insuranceTypeCode, rangeCode!, ct)
        ? Error.None
        : ErrorCatalog.Validation(new() { ["rangeCode"] = ["RangeCode does not belong to the selected InsuranceCode."] });
}
```
**b) Tipo de documento** del beneficiario: **reusar** `ValidateIdentificationTypeCodeAsync(:178)` (ya existe).
**c) Moneda**: validar con `PersonnelCurriculumCatalogValidation.ValidateCodeAsync(repository, tenantId, "currencyCode", PersonnelCurriculumCatalogCategories.Currency, code, ct)`. *(Confirmar que `CatalogCodeIsActiveAsync` resuelve `"CURRENCY"`; si no, añadir el caso — pequeño.)*

> **Semántica de error de catálogo.** Igual que el parentesco hoy, un código fuera de catálogo devuelve **400 `common.validation`** con mensaje de campo (no un 422 dedicado). Esto cubre `INSURANCE_CODE_INVALID`, `INSURANCE_RANGE_INVALID`, `INSURANCE_RANGE_NOT_OWNED`, `DOCUMENT_TYPE_INVALID`, `CURRENCY_CODE_INVALID` **sin** nuevos recursos de localización.

### 3.4 Módulo de reglas puro — nuevo `Features/PersonnelFiles/Compensation/Insurances.Rules.cs`

Espejo de `EmploymentAssignmentRules` (testeable sin BD). Cubre lo que necesita **contexto de hermanos** (D-09 suma, D-13 duplicados). Las validaciones de **campo** (fechas, montos, rango de `%`) van en el **validador** (400), no aquí.

```csharp
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal static class InsuranceErrors
{
    public static readonly Error PolicyDuplicate = new(
        "INSURANCE_POLICY_DUPLICATE",
        "The employee already has an insurance with the same policy number.", ErrorType.Conflict);

    public static readonly Error BeneficiaryDuplicate = new(
        "INSURANCE_BENEFICIARY_DUPLICATE",
        "This insurance already has a beneficiary with the same document.", ErrorType.Conflict);

    public static readonly Error AllocationExceeded = new(
        "INSURANCE_BENEFICIARY_ALLOCATION_INVALID",
        "Active primary beneficiaries cannot exceed 100% allocation for this insurance.", ErrorType.UnprocessableEntity);
}

internal static class InsuranceRules
{
    internal sealed record ExistingInsurance(Guid PublicId, string? NormalizedPolicyNumber);
    internal sealed record ExistingBeneficiary(Guid PublicId, string? NormalizedDocumentKey, bool IsActive, bool IsPrimary, decimal? Allocation);

    // (D-13) Póliza única por empleado (excluye la propia en Update).
    public static Result CheckPolicyUnique(Guid? candidatePublicId, string? normalizedPolicy, IReadOnlyCollection<ExistingInsurance> siblings)
    {
        if (string.IsNullOrWhiteSpace(normalizedPolicy)) return Result.Success();
        return siblings.Any(s => s.PublicId != candidatePublicId && s.NormalizedPolicyNumber == normalizedPolicy)
            ? Result.Failure(InsuranceErrors.PolicyDuplicate) : Result.Success();
    }

    // (D-13) Beneficiario único (tipo+documento) por seguro, entre ACTIVOS (excluye el propio en Update).
    public static Result CheckBeneficiaryUnique(Guid? candidatePublicId, string? normalizedDocKey, IReadOnlyCollection<ExistingBeneficiary> siblings)
    {
        if (string.IsNullOrWhiteSpace(normalizedDocKey)) return Result.Success();
        return siblings.Any(b => b.PublicId != candidatePublicId && b.IsActive && b.NormalizedDocumentKey == normalizedDocKey)
            ? Result.Failure(InsuranceErrors.BeneficiaryDuplicate) : Result.Success();
    }

    // (D-09) Suma de % de PRINCIPALES activos ≤ 100 (incluida la candidata).
    public static Result CheckPrimaryAllocation(Guid? candidatePublicId, bool candidateActive, bool candidatePrimary, decimal? candidateAllocation,
        IReadOnlyCollection<ExistingBeneficiary> siblings)
    {
        if (!candidateActive || !candidatePrimary) return TotalOthers(candidatePublicId, siblings) > 100m
            ? Result.Failure(InsuranceErrors.AllocationExceeded) : Result.Success();
        var total = (candidateAllocation ?? 0m) + TotalOthers(candidatePublicId, siblings);
        return total > 100m ? Result.Failure(InsuranceErrors.AllocationExceeded) : Result.Success();
    }

    private static decimal TotalOthers(Guid? candidatePublicId, IReadOnlyCollection<ExistingBeneficiary> siblings) =>
        siblings.Where(b => b.PublicId != candidatePublicId && b.IsActive && b.IsPrimary).Sum(b => b.Allocation ?? 0m);
}
```

> **Nota de diseño (suma 100 %).** Se valida **≤ 100 %** en cada escritura (bloqueo duro si excede). Exigir **exactamente 100 %** en cada alta **bloquearía** la carga incremental (se agregan beneficiarios uno a uno). El "= 100 %" como **completitud** se trata como verificación de cierre/advertencia, no como bloqueo por operación (ver R-T2). Los **contingentes** no computan en el 100 % de principales (decisión menor abierta — §17 del negocio).

### 3.5 Aplicación — comandos, validadores y patch

**`InsuranceBeneficiaries.cs`** (input/response/validador/patch):
- `InsuranceBeneficiaryInput`: +`string? DocumentTypeCode, decimal? AllocationPercentage, string? BeneficiaryType`.
- `PersonnelFileInsuranceBeneficiaryResponse`: +los 3 campos.
- Validador: `AllocationPercentage` `InclusiveBetween(0, 100)` cuando tiene valor; `BeneficiaryType` `Must(in {PRINCIPAL, CONTINGENTE})` cuando tiene valor.
- Patch state + applier (`InsuranceBeneficiaries.Handlers.cs:409`): añadir segmentos `documentTypeCode` (`ReadNullableString`), `allocationPercentage` (`ReadNullableDecimal`), `beneficiaryType` (`ReadNullableString`); marcar `KinshipCodeMutated`-style flags si requieren re-validación (documentType sí).

**`Insurances.cs`** (validador de seguro):
- `InsuranceInputValidator(:90)`: añadir
```csharp
RuleFor(i => i.EmployeeContribution).GreaterThanOrEqualTo(0).When(i => i.EmployeeContribution.HasValue);
RuleFor(i => i.EmployerContribution).GreaterThanOrEqualTo(0).When(i => i.EmployerContribution.HasValue);
RuleFor(i => i.InsuredAmount).GreaterThanOrEqualTo(0).When(i => i.InsuredAmount.HasValue);
RuleFor(i => i.StartDateUtc).LessThanOrEqualTo(i => i.EndDateUtc!.Value)
    .When(i => i.StartDateUtc.HasValue && i.EndDateUtc.HasValue);
```
(El patch de seguro valida los mismos invariantes tras aplicar, reusando un `Validate(state)` ampliado.)

### 3.6 Aplicación — handlers

**Seguro** (`Insurances.Handlers.cs`, en **Add**, **Update** y la rama de **Patch** que muta negocio), tras `IsCompletedEmployee` y antes de persistir:
```csharp
// 1) Catálogos: nombre de seguro (D-02), rango con pertenencia (D-03), moneda (D-12)
var typeErr = await PersonnelReferenceCatalogValidation.ValidateInsuranceTypeCodeAsync(
    personnelFileRepository, personnelFile.TenantId, "insuranceCode", input.InsuranceCode, ct);
if (typeErr != Error.None) return Fail(typeErr);
var rangeErr = await PersonnelReferenceCatalogValidation.ValidateInsuranceRangeCodeAsync(
    personnelFileRepository, personnelFile.TenantId, input.InsuranceCode, input.RangeCode, ct);
if (rangeErr != Error.None) return Fail(rangeErr);
if (!string.IsNullOrWhiteSpace(input.CurrencyCode))
{
    var curErr = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
        personnelFileRepository, personnelFile.TenantId, "currencyCode",
        PersonnelCurriculumCatalogCategories.Currency, input.CurrencyCode!, ct);
    if (curErr != Error.None) return Fail(curErr);
}

// 2) Anti-duplicado de póliza (D-13) — reusa GetInsurancesAsync (en memoria)
var siblings = (await employeeRepository.GetInsurancesAsync(personnelFile.PublicId, ct))
    .Select(i => new InsuranceRules.ExistingInsurance(i.Id, Normalize(i.PolicyNumber))).ToArray();
var dup = InsuranceRules.CheckPolicyUnique(existingPublicIdOrNull, Normalize(input.PolicyNumber), siblings);
if (dup.IsFailure) return Fail(dup.Error);
```

**Beneficiario** (`InsuranceBeneficiaries.Handlers.cs`, en **Add**/**Update**/**Patch**), junto al `ValidateKinshipCodeAsync` existente:
```csharp
// Tipo de documento (D-10) — reusa el validador existente
if (!string.IsNullOrWhiteSpace(item.DocumentTypeCode))
{
    var docErr = await PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync(
        personnelFileRepository, personnelFile.TenantId, item.DocumentTypeCode!, ct);
    if (docErr != Error.None) return Fail(docErr);
}

// Anti-duplicado + suma de asignación (D-09/D-13) — reusa GetInsuranceBeneficiariesAsync
var existing = (await employeeRepository.GetInsuranceBeneficiariesAsync(personnelFile.PublicId, command.InsurancePublicId, ct))
    .Select(b => new InsuranceRules.ExistingBeneficiary(b.Id, DocKey(b), b.IsActive, IsPrimary(b), b.AllocationPercentage)).ToArray();
var dupB = InsuranceRules.CheckBeneficiaryUnique(candidatePublicId, DocKey(item), existing);
if (dupB.IsFailure) return Fail(dupB.Error);
var alloc = InsuranceRules.CheckPrimaryAllocation(candidatePublicId, isActiveForOp, IsPrimary(item), item.AllocationPercentage, existing);
if (alloc.IsFailure) return Fail(alloc.Error);
```
Notas: en **Add** `candidatePublicId = null`; en **Update** = el `PublicId` editado; `IsPrimary` = `BeneficiaryType == "PRINCIPAL"` (default PRINCIPAL). **Delete** sin reglas nuevas.

### 3.7 Permiso dedicado de lectura `PersonnelFiles.ViewInsurance` (D-11)

Espejo **exacto** de `ViewCompensation` (authn-only superset + gate preciso en el read-handler). Escritura sigue con `Manage`.

| Archivo | Cambio |
|---|---|
| `Common/PersonnelFileCommon.cs:82` | `public const string ViewInsurance = "PersonnelFiles.ViewInsurance";` |
| `Common/PersonnelFilePolicies.cs` | `public const string ViewInsurance = "PersonnelFiles.ViewInsurance";` |
| `Provisioning/Common/ProvisioningConstants.cs:68` | `new("PersonnelFiles.ViewInsurance", "Ver seguros", "Consulta de los seguros y beneficiarios de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewInsurance"),` |
| `Api/Program.cs:444` | `options.AddPolicy(PersonnelFilePolicies.ViewInsurance, pb => pb.Combine(policy));` (authn-only, igual que `ViewCompensation`). |
| `Abstractions/PersonnelFiles/IPersonnelFileAuthorizationService.cs` | `Task<Result> EnsureCanViewInsuranceAsync(Guid companyId, CancellationToken ct) => Task.FromResult(Result.Failure(AuthorizationErrors.Unauthenticated));` (default fail-closed). |
| `Infrastructure/PersonnelFiles/PersonnelFileAuthorizationService.cs:27` | `EnsureCanViewInsuranceAsync` → `EnsureHasAnyClaimAsync([ViewInsurance, Admin, ManageAdministration], RbacPermissionAction.Read, ct)`. |
| `Common/PersonnelFileEmployeeHandlerBases.cs:248` | Nuevo `LoadCompletedEmployeeForInsuranceReadAsync<T>(...)` espejo de `…ForCompensationReadAsync` **sin** la rama de autoservicio (D-11: sin autoservicio): solo `EnsureCanViewInsuranceAsync`. |
| `Insurances.Handlers.cs:351,379` + `InsuranceBeneficiaries.Handlers.cs` (4 query handlers) | Swap `LoadCompletedEmployeeForReadAsync` → `LoadCompletedEmployeeForInsuranceReadAsync`. |
| `Api/Controllers/PersonnelFileCompensationController.cs:570-923` | A nivel de **método**, en los endpoints de seguro/beneficiario: `[AuthorizationPolicySet(PersonnelFilePolicies.ViewInsurance, PersonnelFilePolicies.Manage)]` (override del `[AuthorizationPolicySet(Read, Manage)]` de clase, que sigue rigiendo bank-accounts/benefits/etc.). |

### 3.8 API — contratos y controlador

`Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:403-422` (solo **beneficiario**; el seguro no cambia):
```csharp
public sealed record AddInsuranceBeneficiaryRequest(
    string FullName, string? DocumentNumber, DateTime? BirthDate, string KinshipCode,
    string? DocumentTypeCode, decimal? AllocationPercentage, string? BeneficiaryType);   // +3

public sealed record UpdateInsuranceBeneficiaryRequest(
    string FullName, string? DocumentNumber, DateTime? BirthDate, string KinshipCode,
    string? DocumentTypeCode, decimal? AllocationPercentage, string? BeneficiaryType);   // +3

public sealed class PatchInsuranceBeneficiaryRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public DateTime? BirthDate { get; set; }
    public string KinshipCode { get; set; } = string.Empty;
    public string? DocumentTypeCode { get; set; }            // +
    public decimal? AllocationPercentage { get; set; }       // +
    public string? BeneficiaryType { get; set; }             // +
    public bool IsActive { get; set; }
}
```
`PersonnelFileCompensationController.cs` (Add `:807-829`, Update `:843-865`): pasar los 3 campos a `InsuranceBeneficiaryInput`. El resto (`[FromIfMatch]`, `ToCreatedAtActionResult`, ETag, mapeo de JSON Patch) no cambia.

### 3.9 Infraestructura — repositorio seguro/beneficiario

`IPersonnelFileEmployeeRepository(:421-518)` + impl:
- `UpdateInsuranceBeneficiaryAsync(:1097)` / `PatchInsuranceBeneficiaryAsync(:1120)`: añadir parámetros `string? documentTypeCode, decimal? allocationPercentage, string? beneficiaryType`; llamar `item.Update(..., documentTypeCode, allocationPercentage, beneficiaryType)`.
- `AddInsuranceBeneficiaryAsync(:1049)`: ya recibe `InsuranceBeneficiaryInput` (enriquecido) → pasar los 3 a `PersonnelFileInsuranceBeneficiary.Create(...)`.
- `Map(PersonnelFileInsuranceBeneficiary)(:1880)`: proyectar los 3 campos nuevos.
- **No** se requieren métodos nuevos para reglas: se **reusan** `GetInsurancesAsync(:1022)` y `GetInsuranceBeneficiariesAsync(:1069)` (en memoria, como en `AuthorizationSubstitutionRules`).

### 3.10 Infraestructura — EF config y migración

**Config beneficiario** `PersonnelFileEmployeeConfiguration.cs:378`:
```csharp
builder.Property(i => i.DocumentTypeCode).HasColumnName("document_type_code").HasMaxLength(80);
builder.Property(i => i.AllocationPercentage).HasColumnName("allocation_percentage").HasColumnType("numeric(5,2)");
builder.Property(i => i.BeneficiaryType).HasColumnName("beneficiary_type").HasMaxLength(40);
```
**Índices de apoyo anti-duplicado (opcional):** índice filtrado único `(personnel_file_id, normalized_policy)` y `(insurance_id, normalized_document_key)`; o dejar la unicidad **a nivel de aplicación** (las reglas ya lo cubren) + índices no únicos de apoyo. *(La unicidad de póliza es por empleado, no global.)*

**Config catálogos** (§3.2.c). Todas se autodescubren por `ApplyConfigurationsFromAssembly`.

**Migración** (una sola):
```bash
dotnet ef migrations add InsuranceCatalogsAndBeneficiaryAllocation \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```
Contendrá: `CreateTable insurance_type_catalog_items` + `insurance_range_catalog_items` (FK rango→tipo + FK país, índices) · `InsertData` seed SV (siguiendo el patrón de `20260415040945_UnifySystemCatalogsByCountry` para `kinship_catalog_items`/`municipality_catalog_items`) · `AddColumn document_type_code` + `allocation_percentage` + `beneficiary_type` en `personnel_file_insurance_beneficiaries`. Validar con `dotnet ef migrations has-pending-model-changes`.

**Seed SV (propuesto, confirmar con negocio):**
`InsuranceType`: `VIDA` (Vida), `MEDICO_HOSPITALARIO` (Médico hospitalario), `GASTOS_MEDICOS` (Gastos médicos), `DENTAL` (Dental), `VISION` (Visión), `ACCIDENTES` (Accidentes personales), `OTRO` (Otro).
`InsuranceRange` (hijos, ejemplo): para `VIDA` → `BASICO`, `INTERMEDIO`, `PREMIUM`.

### 3.11 Localización (≈3 códigos × 3 resx)

Solo los **errores de reglas** (los de catálogo son `common.validation`, sin resx). Añadir a `BackendMessages.resx` (EN), `.es.resx`, `.es-SV.resx`:

| Code | EN | ES |
|---|---|---|
| `INSURANCE_POLICY_DUPLICATE` | The employee already has an insurance with the same policy number. | El empleado ya tiene un seguro con el mismo número de póliza. |
| `INSURANCE_BENEFICIARY_DUPLICATE` | This insurance already has a beneficiary with the same document. | Este seguro ya tiene un beneficiario con el mismo documento. |
| `INSURANCE_BENEFICIARY_ALLOCATION_INVALID` | Active primary beneficiaries cannot exceed 100% allocation for this insurance. | Los beneficiarios principales activos no pueden exceder el 100 % de asignación en este seguro. |

### 3.12 Auditoría con diff + historial visible (D-15)

- La auditoría ya se invoca por operación (`PersonnelFileEmployeeAudits.LogUpdateAsync`). Para el **diff**, pasar el `existing` además del `response` en Update/Patch/Delete (hoy se pasa solo el resultado).
- **Historial visible**: confirmar si existe consulta/endpoint de auditoría reutilizable por entidad; si existe, exponer filtro por seguro/beneficiario; si no, es un pequeño RF de lectura — **a confirmar** con el módulo de Audit (mismo punto pendiente que en sustituciones).

---

## 4. Migración de datos (D-14 — sin datos productivos)

Catalogar `InsuranceCode`/`RangeCode`/`CurrencyCode` y añadir columnas al beneficiario **rompería** datos existentes, pero el negocio confirmó **no hay datos productivos** (D-14). Por tanto:

- **Drop & recreate / normalización directa:** la migración hace `AddColumn` (beneficiario) y `CreateTable` (catálogos) sin backfill. Si en QA hubiese filas con `insurance_code`/`range_code`/`currency_code` no catalogables, se **eliminan/normalizan** directamente (no hay que preservarlas).
- **Sin script de backfill.** No se requiere ruta de migración por pasos.

> Acción previa (confirmada por negocio): no preservar datos de `personnel_file_insurances` / `personnel_file_insurance_beneficiaries` en entornos no productivos.

---

## 5. Mapa de errores (resumen)

| Disparador | Código | ErrorType → HTTP | Capa |
|---|---|---|---|
| Nombre de seguro fuera de catálogo | `common.validation` (campo `insuranceCode`) | Validation → **400** | Handler (validador de catálogo) |
| Rango fuera de catálogo / no pertenece al seguro | `common.validation` (campo `rangeCode`) | Validation → **400** | Handler |
| Moneda no ISO-4217 | `common.validation` (campo `currencyCode`) | Validation → **400** | Handler |
| Tipo de documento fuera de catálogo | `common.validation` (campo `documentTypeCode`) | Validation → **400** | Handler |
| `EndDate < StartDate` | `common.validation` (campo `startDateUtc`) | Validation → **400** | Validador |
| Monto negativo | `common.validation` | Validation → **400** | Validador |
| `%` fuera de [0,100] | `common.validation` (campo `allocationPercentage`) | Validation → **400** | Validador |
| Póliza duplicada por empleado | `INSURANCE_POLICY_DUPLICATE` | Conflict → **409** | Reglas |
| Beneficiario duplicado por seguro | `INSURANCE_BENEFICIARY_DUPLICATE` | Conflict → **409** | Reglas |
| Principales activos exceden 100 % | `INSURANCE_BENEFICIARY_ALLOCATION_INVALID` | UnprocessableEntity → **422** | Reglas |
| Parentesco fuera de catálogo (existente) | `common.validation` | Validation → **400** | Handler |
| `If-Match` no coincide (existente) | `CONCURRENCY_CONFLICT` | Conflict → **409** | Handler |
| Sin `ViewInsurance` (lectura) / sin `Manage` (escritura) | (política/gate) | Forbidden → **403** | API/Policy/Handler |

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- `InsuranceRulesTests` (nuevo): póliza duplicada sí/no (excluyendo la propia), beneficiario duplicado entre activos, suma de principales ≤100 (bordes 99.99/100/100.01, candidata inactiva/contingente no computa, exclusión de la propia).
- `PersonnelFileInsurancePatchTests` / `PersonnelFileInsuranceBeneficiaryPatchTests` (existentes): añadir segmentos `documentTypeCode`/`allocationPercentage`/`beneficiaryType`; round-trip; nuevos requeridos en `Validate`.
- `BackendMessageLocalizationTests` (existente): verde con los 3 códigos nuevos en EN+ES.
- (Opcional) tests de handler con mocks para rutas 400/409/422/403.

**Integración (`tests/CLARIHR.Api.IntegrationTests/`):**
- `IntegrationTestSeeder`: sembrar `InsuranceType`/`InsuranceRange` (país de prueba) + permiso `ViewInsurance` en rol de prueba.
- Casos: alta feliz (seguro + beneficiarios con %); nombre/rango/moneda/tipo-doc inválidos (400); rango no perteneciente al seguro (400); póliza duplicada (409); beneficiario duplicado (409); principales >100 % (422); `EndDate<StartDate` y monto negativo (400); **403** sin `ViewInsurance` en lectura; **409** por `If-Match`.
- Dropdown dependiente: `GET …/reference-catalog-items?category=InsuranceRange&countryCode=SV&parentCode=VIDA` devuelve solo rangos de VIDA.

**Guardrails:** si existe un test de biyección categoría↔entidad (estilo `GeneralCatalogKeyMapGuardrailsTests`), incluir `InsuranceType`/`InsuranceRange`.

---

## 7. Orden de implementación (PRs sugeridos)

1. **PR-1 — Catálogos `InsuranceType`/`InsuranceRange`** (§3.2, §3.3): entidades + config + DbSets + switches del repo + validadores + migración parcial (`CreateTable`+`InsertData` seed SV). Bajo riesgo, aislado; habilita los dropdowns.
2. **PR-2 — Permiso `ViewInsurance`** (§3.7): constante + política + semilla + servicio (interfaz+impl) + read-gate + swap en los 4 query handlers + atributos de método. Aislado.
3. **PR-3 — Enriquecimiento del beneficiario** (§3.1, §3.5, §3.8, §3.9, §3.10): 3 campos en dominio/EF/migración, input/response/validador/patch, contratos+mapeo, firmas de repo + `Map`.
4. **PR-4 — Reglas + validaciones de handler + localización** (§3.4, §3.6, §3.11, §6): `Insurances.Rules.cs`, validación de catálogo/moneda/duplicado/suma en handlers, montos/fechas en validadores, 3 errores × 3 resx, batería de pruebas.
5. **PR-5 — Auditoría diff + historial** (§3.12): pasar `existing` al log; exponer historial (sujeto a confirmación de infraestructura).

> PR-3/PR-4 pueden fusionarse. PR-1 y PR-2 conviene aislarlos.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Seed de catálogos (país).** El seed `InsuranceType`/`InsuranceRange` debe confirmarse con el negocio (§3.10). Incluir `OTRO` y dejar el catálogo extensible. La jerarquía rango→seguro debe sembrarse con la FK correcta (`insurance_type_catalog_item_id`).
- **R-T2 — Regla "suma 100 %".** Se implementa **≤100 %** por operación para no bloquear la carga incremental; el **=100 %** exacto es completitud (advertencia/cierre), no bloqueo por alta. Confirmar con negocio el tratamiento de **contingentes** (no computan en el 100 % de principales por defecto).
- **R-T3 — Catálogo de moneda.** Validar que `CatalogCodeIsActiveAsync` resuelve `"CURRENCY"` y que existe seed de monedas para el país; si no, añadir el caso/seed (pequeño).
- **R-T4 — `BuildItems()` legado.** Confirmar que `PersonnelReferenceCatalog.BuildItems()` ya no es fuente de seed (tras la unificación a tablas tipadas); el seed va en la migración. Si aún se consume, añadir las categorías también allí.
- **R-T5 — Historial visible (D-15).** Depende de si existe consulta de auditoría por entidad; si no, añadir endpoint de lectura (alcance pequeño). **Único punto sin patrón verificado.**
- **R-T6 — Política de controlador compartido.** El `[AuthorizationPolicySet(Read, Manage)]` de clase cubre varias sub-recursos; aplicar `ViewInsurance` **solo** a nivel de método en los endpoints de seguro/beneficiario para no afectar bank-accounts/benefits/medical-claims.

---

## 9. Checklist de implementación

- [ ] **Dominio:** `PersonnelFileInsuranceBeneficiary` con `DocumentTypeCode`/`AllocationPercentage`/`BeneficiaryType`; `Create/Update` nuevos. (Seguro **sin** cambios de campo.)
- [ ] **Catálogos:** `InsuranceTypeCatalogItem` (plano) + `InsuranceRangeCatalogItem` (FK al tipo) + config + 2 DbSets + categorías; switches `ReferenceCatalogCodeIsActiveAsync`/`GetReferenceCatalogItemsAsync` + `GetInsuranceRangeCatalogItemsAsync` + `ReferenceInsuranceRangeBelongsToTypeAsync`.
- [ ] **Validación:** `ValidateInsuranceTypeCodeAsync` + `ValidateInsuranceRangeCodeAsync` (pertenencia); reuso `ValidateIdentificationTypeCodeAsync`; moneda vía `ValidateCodeAsync(Currency)`.
- [ ] **Reglas:** `Insurances.Rules.cs` (póliza única, beneficiario único, suma principales ≤100).
- [ ] **Aplicación:** `InsuranceBeneficiaryInput`/response +3; validadores (montos/fechas/%); patch state/applier (+3 segmentos).
- [ ] **Handlers:** validación de catálogo/moneda + tipo-doc + anti-duplicado + suma; swap a `LoadCompletedEmployeeForInsuranceReadAsync`.
- [ ] **Permiso:** `ViewInsurance` (constante + política + `Program.cs` + semilla + servicio interfaz/impl + read-gate); atributos de método en endpoints.
- [ ] **Infra:** firmas de repo beneficiario (Update/Patch +3) + `Map`; EF config (3 columnas + 2 catálogos); 1 migración + seed SV.
- [ ] **API:** contratos beneficiario (Add/Update/Patch +3) + mapeo en controlador.
- [ ] **Localización:** 3 códigos en EN + es + es-SV.
- [ ] **Tests:** rules unit + patch unit + paridad + integración (felices/errores/403/409/422) + seeder + dropdown dependiente.
- [ ] **Verificación:** `dotnet build`, `dotnet test`, `dotnet ef migrations has-pending-model-changes` (sin pendientes).

---

> **Trazabilidad.** Este plan implementa la Fase 1 del análisis de negocio (D-01…D-15, RF-001…RF-013). Todo cambio sigue patrones verificados en el código: catálogos de referencia país **tipados** (`Kinship`/`Municipality→Department`), reuso de `IdentificationType` y `Currency`, módulo de reglas estilo `EmploymentAssignmentRules`, permiso espejo de `ViewCompensation`, recursos `BackendMessages*`. La independencia de nómina (D-01/RF-007) se mantiene: **no** se introduce ningún enlace a `CompensationConcept`/planilla.
