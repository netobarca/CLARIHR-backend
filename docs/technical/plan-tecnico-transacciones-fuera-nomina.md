# Plan Técnico — Transacciones Fuera de Nómina del Empleado

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación (Fase 1) |
| **Audiencia** | Equipo de desarrollo backend, QA, tech lead |
| **Documento de negocio** | [`analisis-transacciones-fuera-nomina-empleado.md`](../business/analisis-transacciones-fuera-nomina-empleado.md) (v2.1, D-01…D-13 ratificadas) |
| **Módulos** | Expediente de Personal → Compensación (`PersonnelFiles/Compensation`); Catálogos; Archivos (Blob); IAM |
| **Estado** | Propuesto — listo para implementar |
| **Fecha** | 2026-06-23 |
| **País de referencia (seed)** | El Salvador (SV) |

---

## 1. Objetivo y enfoque

Construir un **módulo nuevo** ("slice vertical") para registrar **gastos que la empresa asume por un empleado fuera de la planilla**, con su **catálogo de tipos**. El requerimiento **no existe** en el código; el "primo" `PersonnelFilePayrollTransaction` es un concepto distinto (movimientos **dentro** de nómina, inmutable, importable, tipo free-text) y **no se reutiliza**.

**Patrón base:** se replica la feature **`MedicalClaims`** (recién completada: entidad hija + catálogo country-scoped + módulo de reglas puro + permisos dedicados + controlador dedicado + adjuntos + migración), **con estas diferencias derivadas de las decisiones de negocio**:

1. **Sin autoservicio** (D-06): uso **interno de RR. HH.** Los gates son *permiso-solo* (no existe la rama "es el propio empleado" de `MedicalClaims`).
2. **Tipo desde catálogo dedicado** (D-02/D-03): catálogo propio `OffPayrollTransactionType`, con **`Code` ingresado por el usuario** (no autogenerado), **no compartido** con `asset-access-types`.
3. **Período de imputación** (D-05): campos explícitos `Year` + `Month` (1–12), independientes de la fecha.
4. **Valor con signo** (D-04/D-12): `Amount ≠ 0`, admite **negativos**; un negativo **debe referenciar** la transacción original que corrige (`CorrectsTransactionPublicId`).
5. **Vínculo opcional a AssetAccess** (D-01): `AssetAccessPublicId?` validado contra el **mismo empleado** + snapshot.
6. **Adjuntos** (D-07/D-11): comprobante de cualquier índole, nuevo `FilePurpose.OffPayrollTransactionDocument`, hereda la política general de archivos.
7. **Totalización por moneda a nivel de empleado** (D-08/D-13): consulta de totales agrupados por `CurrencyCode` (con signos), sin conversión (FX).
8. **Aprobación diferida** (D-09): se reserva un punto de extensión (`StatusCode`) sin implementar workflow.

Operaciones: **CRUD** (alta, edición completa, baja lógica), listado, consulta por id, **adjuntos** (subir/listar/leer-URL/eliminar), **totales por moneda**, exportación. Concurrencia optimista (`If-Match`), auditoría before/after, multi-tenant.

---

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Existencia | Búsqueda nula de `OffPayroll`/`FueraNomina` en `src/` | Net-new; crear todo el slice |
| 2 | "Primo" | `PersonnelFilePayrollTransaction` (`PersonnelFileEmployee.cs:587-677`); inmutable, `PayrollPeriodCode`/`IsDebit`/`Source*`; tipo free-text (`PayrollTransactions.cs:118`) | NO reutilizar; entidad hermana nueva |
| 3 | Plantilla | `MedicalClaims.cs` / `.Handlers.cs` / `.Rules.cs`; entidad `PersonnelFileMedicalClaim` (`PersonnelFileEmployee.cs:994-1243`) | Copiar estructura; quitar self-service |
| 4 | Entidad hija | Persistida **standalone vía repositorio** (no colección en el agregado); EF `.WithMany()` con FK a `personnel_files` | Repo `Add/Get/Update/Patch/Delete` + `Map` |
| 5 | Catálogo | Receta: clase `: GeneralCatalogItem` (`GeneralCatalogItems.cs:220` AssetAccessType) + categoría (`PersonnelReferenceCatalogs.cs:105`) + wire key (`GeneralCatalogKeyMap.cs:47`) + `CatalogCodeIsActiveAsync` switch (`PersonnelFileRepository.cs`) + DbSet + EF config + seed | Catálogo `OffPayrollTransactionType` |
| 6 | Moneda | `string CurrencyCode` ISO‑4217; default de empresa vía `ICompanyPreferenceRepository`; validada en `Insurances.cs`/`MedicalClaims` | Required + default empresa |
| 7 | Permisos | `PersonnelFilePermissionCodes` (`PersonnelFileCommon.cs`), políticas authn-only (`PersonnelFilePolicies.cs:45,53`), registro (`Program.cs:466-474`), servicio (`IPersonnelFileAuthorizationService.cs:47-63` / impl `:63-85`) | 2 permisos dedicados (View/Manage) |
| 8 | Controlador dedicado | `MedicalClaimsController.cs:17-29` (`AuthorizationPolicySet` es **class-only** → método no puede tener política propia) | `OffPayrollTransactionsController` dedicado |
| 9 | Gates | `PersonnelFileEmployeeHandlerBases.cs` — `LoadForManageMedicalClaimsAsync` (manage-only, `:183-219`) / `LoadCompletedEmployeeForMedicalClaimReadAsync` (`:452-494`) | Gates **manage-only** y **view-only** (sin rama self) |
| 10 | Adjuntos | `FilePurpose` (`FileEnums.cs:12-20`); `MedicalClaimDocument` (`PersonnelFileEmployee.cs:1265-1373`) + `MedicalClaimDocuments.*` + endpoints `MedicalClaimsController.cs:178-297` (list/get/read-url/add/delete) + validación `Purpose` | Replicar `OffPayrollTransactionDocument` |
| 11 | AssetAccess | `PersonnelFileAssetAccess` (`PersonnelFileEmployee.cs:679-760`): `PublicId`, `AssetOrAccessName`; seed tipos `DevSeedService.cs:396-406` | Vínculo + snapshot; lookup por mismo empleado |
| 12 | Localización | `BackendMessages.resx` + `.es.resx` (+ `.es-SV.resx`); test de paridad `BackendMessageLocalizationTests.cs:64-104` | Cada error en 3 resx |
| 13 | Gobernanza | `AuthorizationPolicyConventionGovernanceTests.cs:81-96` (`PersonnelFilePolicyNames`) | Agregar las 2 políticas nuevas |
| 14 | Migración | EF 9.0.9 requiere `DOTNET_ROLL_FORWARD=Major` (memoria equipo-acceso) | Exportar var antes de `dotnet ef` |

---

## 3. Arquitectura de la solución

### 3.1 Dominio

**Entidad hija** `PersonnelFileOffPayrollTransaction` (en `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs`):

```csharp
public sealed class PersonnelFileOffPayrollTransaction : TenantEntity
{
    private PersonnelFileOffPayrollTransaction() { }

    private PersonnelFileOffPayrollTransaction(
        string transactionTypeCode, string? transactionTypeNameSnapshot,
        DateTime transactionDateUtc, string currencyCode, decimal amount,
        int year, int month, string? comment,
        Guid? assetAccessPublicId, string? assetNameSnapshot,
        Guid? correctsTransactionPublicId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Apply(transactionTypeCode, transactionTypeNameSnapshot, transactionDateUtc, currencyCode,
              amount, year, month, comment, assetAccessPublicId, assetNameSnapshot, correctsTransactionPublicId);
    }

    public long PersonnelFileId { get; private set; }
    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string OffPayrollTransactionTypeCode { get; private set; } = string.Empty;
    public string? TransactionTypeNameSnapshot { get; private set; }   // snapshot descripción del tipo
    public DateTime TransactionDateUtc { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;   // ISO-4217 (D-08, requerido tras resolución)
    public decimal Amount { get; private set; }                        // ≠ 0, admite negativos (D-04)
    public int Year { get; private set; }                              // período de imputación (D-05)
    public int Month { get; private set; }                             // 1..12 (D-05)
    public string? Comment { get; private set; }
    public Guid? AssetAccessPublicId { get; private set; }             // vínculo opcional (D-01)
    public string? AssetNameSnapshot { get; private set; }
    public Guid? CorrectsTransactionPublicId { get; private set; }     // requerido si Amount < 0 (D-12)
    // Punto de extensión para aprobación futura (D-09): public string? StatusCode { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;
    public void SetActive(bool isActive) { IsActive = isActive; ConcurrencyToken = Guid.NewGuid(); }

    public static PersonnelFileOffPayrollTransaction Create(/* mismos parámetros */) => new(/* … */);

    public void Update(/* mismos parámetros */)
    {
        ConcurrencyToken = Guid.NewGuid();
        Apply(/* … */);
    }

    private void Apply(
        string transactionTypeCode, string? transactionTypeNameSnapshot,
        DateTime transactionDateUtc, string currencyCode, decimal amount,
        int year, int month, string? comment,
        Guid? assetAccessPublicId, string? assetNameSnapshot, Guid? correctsTransactionPublicId)
    {
        OffPayrollTransactionTypeCode = PersonnelFileNormalization.Clean(transactionTypeCode, nameof(transactionTypeCode)).ToUpperInvariant();
        TransactionTypeNameSnapshot = PersonnelFileNormalization.CleanOptional(transactionTypeNameSnapshot);
        TransactionDateUtc = PersonnelFileNormalization.NormalizeDate(transactionDateUtc);
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode)).ToUpperInvariant();
        Amount = amount;
        Year = year;
        Month = month;
        Comment = PersonnelFileNormalization.CleanOptional(comment);
        AssetAccessPublicId = assetAccessPublicId;
        AssetNameSnapshot = PersonnelFileNormalization.CleanOptional(assetNameSnapshot);
        CorrectsTransactionPublicId = correctsTransactionPublicId;
    }
}
```

**Catálogo** `OffPayrollTransactionTypeCatalogItem` (en `src/CLARIHR.Domain/GeneralCatalogs/GeneralCatalogItems.cs`, idéntico a `AssetAccessTypeCatalogItem:220`):

```csharp
public sealed class OffPayrollTransactionTypeCatalogItem : GeneralCatalogItem
{
    private OffPayrollTransactionTypeCatalogItem() { }
    private OffPayrollTransactionTypeCatalogItem(
        Guid publicId, long countryCatalogItemId, string countryCode,
        string code, string name, bool isActive, int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder) { }

    public static OffPayrollTransactionTypeCatalogItem Create(
        long countryCatalogItemId, string countryCode, string code, string name, bool isActive, int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}
```

> `Code` lo ingresa el usuario (D-03); `Name` = la **Descripción** del negocio. `GeneralCatalogItem` ya impone unicidad `(country, normalized_code)`.

### 3.2 Catálogos — cableado

| Paso | Archivo | Cambio |
|---|---|---|
| Categoría | `PersonnelReferenceCatalogs.cs` | `public const string OffPayrollTransactionType = "OffPayrollTransactionType";` |
| Wire key | `GeneralCatalogKeyMap.cs` | `["off-payroll-transaction-types"] = PersonnelCurriculumCatalogCategories.OffPayrollTransactionType,` |
| Validación | `PersonnelFileRepository.cs` (`CatalogCodeIsActiveAsync`) | `case "OFFPAYROLLTRANSACTIONTYPE" => await IsCountryScopedCatalogCodeActiveAsync<OffPayrollTransactionTypeCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, ct),` |
| DbSet | `ApplicationDbContext.cs` | `public DbSet<OffPayrollTransactionTypeCatalogItem> OffPayrollTransactionTypeCatalogItems => Set<…>();` |
| EF config | `Configurations/GeneralCatalogs/GeneralCatalogItemConfiguration.cs` | `OffPayrollTransactionTypeCatalogItemConfiguration : GeneralCatalogItemConfigurationBase<…>` (tabla `off_payroll_transaction_type_catalog_items` + nombres de índices) |
| Seed | `DevSeedService.cs` | bucle con los 6 tipos de negocio (abajo) |

**Seed inicial (SV):**

```csharp
var offPayrollTransactionTypes = new (string Code, string Name, int SortOrder)[]
{
    ("HERRAMIENTAS",    "Herramientas de trabajo", 10),
    ("EPP",             "Equipo de protección",    20),
    ("UNIFORMES",       "Uniformes",               30),
    ("PROMOCIONALES",   "Promocionales",           40),
    ("RECONOCIMIENTOS", "Reconocimientos",         50),
    ("REGALOS",         "Regalos",                 60),
};
foreach (var t in offPayrollTransactionTypes)
    dbContext.OffPayrollTransactionTypeCatalogItems.Add(
        OffPayrollTransactionTypeCatalogItem.Create(companyCountry.CountryCatalogItemId, companyCountry.CountryCode, t.Code, t.Name, true, t.SortOrder));
```

### 3.3 Módulo de reglas puro

`src/CLARIHR.Application/Features/PersonnelFiles/Compensation/OffPayrollTransactions.Rules.cs`:

```csharp
internal static class OffPayrollTransactionErrors
{
    public static readonly Error TypeCodeInvalid = new(
        "OFF_PAYROLL_TX_TYPE_CODE_INVALID",
        "The off-payroll transaction type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error CurrencyRequired = new(
        "OFF_PAYROLL_TX_CURRENCY_REQUIRED",
        "A currency is required and no default currency is configured for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error AssetAccessNotFound = new(
        "OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND",
        "The linked asset/access does not exist for this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error CorrectionRequired = new(
        "OFF_PAYROLL_TX_CORRECTION_REQUIRED",
        "A negative amount must reference the original transaction it corrects.", ErrorType.UnprocessableEntity);

    public static readonly Error CorrectedNotFound = new(
        "OFF_PAYROLL_TX_CORRECTED_NOT_FOUND",
        "The referenced original transaction does not exist for this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error CorrectedInvalid = new(
        "OFF_PAYROLL_TX_CORRECTED_INVALID",
        "The referenced original transaction must be an active original expense in the same currency.", ErrorType.UnprocessableEntity);
}

internal static class OffPayrollTransactionRules
{
    /// <summary>(D-12) Un valor negativo exige referencia al original; positivo no la requiere.</summary>
    public static bool RequiresCorrectionReference(decimal amount, Guid? correctsTransactionPublicId) =>
        amount < 0 && correctsTransactionPublicId is null;

    /// <summary>(D-05) Período de imputación válido.</summary>
    public static bool IsValidPeriod(int year, int month) =>
        month is >= 1 and <= 12 && year is >= 2000 and <= 2100;
}
```

### 3.4 Aplicación — comandos, consultas, validador, patch

`src/CLARIHR.Application/Features/PersonnelFiles/Compensation/OffPayrollTransactions.cs`:

```csharp
public sealed record OffPayrollTransactionInput(
    string TransactionTypeCode, DateTime TransactionDateUtc, string? CurrencyCode,
    decimal Amount, int Year, int Month, string? Comment,
    Guid? AssetAccessPublicId, Guid? CorrectsTransactionPublicId);

public sealed record PersonnelFileOffPayrollTransactionResponse(
    Guid TransactionPublicId, string TransactionTypeCode, string? TransactionTypeName,
    DateTime TransactionDateUtc, string CurrencyCode, decimal Amount, int Year, int Month,
    string? Comment, Guid? AssetAccessPublicId, string? AssetName, Guid? CorrectsTransactionPublicId,
    bool IsActive, DateTime CreatedAtUtc, DateTime? ModifiedAtUtc, Guid ConcurrencyToken)
{ [JsonIgnore] public Guid Id => TransactionPublicId; }

public sealed record OffPayrollTransactionCurrencyTotalResponse(string CurrencyCode, decimal Total, int Count); // D-13

public sealed record AddPersonnelFileOffPayrollTransactionCommand(Guid PersonnelFileId, OffPayrollTransactionInput Item)
    : ICommand<PersonnelFileOffPayrollTransactionResponse>;
public sealed record UpdatePersonnelFileOffPayrollTransactionCommand(Guid PersonnelFileId, Guid TransactionPublicId, OffPayrollTransactionInput Item, Guid ConcurrencyToken)
    : ICommand<PersonnelFileOffPayrollTransactionResponse>;
public sealed record DeletePersonnelFileOffPayrollTransactionCommand(Guid PersonnelFileId, Guid TransactionPublicId, Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;   // baja lógica (RN-10)
public sealed record GetPersonnelFileOffPayrollTransactionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>;
public sealed record GetPersonnelFileOffPayrollTransactionByIdQuery(Guid PersonnelFileId, Guid TransactionPublicId)
    : IQuery<PersonnelFileOffPayrollTransactionResponse>;
public sealed record GetPersonnelFileOffPayrollTransactionTotalsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>;   // totales por moneda (D-13)

internal sealed class OffPayrollTransactionInputValidator : AbstractValidator<OffPayrollTransactionInput>
{
    public OffPayrollTransactionInputValidator()
    {
        RuleFor(x => x.TransactionTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Amount).Must(a => a != 0).WithMessage("Amount must be non-zero.");          // D-04
        RuleFor(x => x.Month).InclusiveBetween(1, 12);                                              // D-05
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);                                          // D-05
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrWhiteSpace(x.CurrencyCode));
        RuleFor(x => x.TransactionDateUtc).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1));      // no futura
        RuleFor(x => x.Comment).MaximumLength(2000);
        RuleFor(x => x.CorrectsTransactionPublicId)                                                 // D-12 (parte pura)
            .NotNull().When(x => x.Amount < 0)
            .WithMessage("A negative amount must reference the original transaction (corrects).");
    }
}
```

> PATCH (RFC 6902) opcional, solo `isActive` (igual que `PersonnelFilePayrollTransaction`), si se quiere alternar estado sin un PUT completo: `PersonnelFileOffPayrollTransactionPatchState` + `…PatchApplier` calcando `PayrollTransactions.cs:348-427`.

### 3.5 Aplicación — handlers y soporte de escritura

`OffPayrollTransactions.Handlers.cs`. El `WriteSupport` concentra la validación cruzada (catálogo, moneda, AssetAccess, referencia de ajuste) y produce los snapshots:

```csharp
internal sealed record OffPayrollTransactionResolved(
    string CurrencyCode, string? TypeName, string? AssetName);

internal static class OffPayrollTransactionWriteSupport
{
    public static async Task<Result<OffPayrollTransactionResolved>> ResolveAndValidateAsync(
        OffPayrollTransactionInput input, PersonnelFile file,
        IPersonnelFileRepository repo, IPersonnelFileEmployeeRepository employeeRepo,
        ICompanyPreferenceRepository preferences, CancellationToken ct)
    {
        // 1) Tipo contra catálogo activo (D-03)
        if (!await repo.CatalogCodeIsActiveAsync(file.TenantId, PersonnelCurriculumCatalogCategories.OffPayrollTransactionType, input.TransactionTypeCode, ct))
            return Fail(OffPayrollTransactionErrors.TypeCodeInvalid);
        var typeName = await repo.GetCatalogNameAsync(file.TenantId, PersonnelCurriculumCatalogCategories.OffPayrollTransactionType, input.TransactionTypeCode, ct); // snapshot

        // 2) Moneda requerida; default de empresa si viene vacía (D-08)
        var currency = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? (await preferences.GetByTenantIdAsync(file.TenantId, ct))?.CurrencyCode
            : input.CurrencyCode!.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency)) return Fail(OffPayrollTransactionErrors.CurrencyRequired);

        // 3) Vínculo opcional a AssetAccess del MISMO empleado (D-01) + snapshot
        string? assetName = null;
        if (input.AssetAccessPublicId is { } assetId)
        {
            var asset = await employeeRepo.GetAssetAccessAsync(file.PublicId, assetId, ct);  // filtra por expediente
            if (asset is null) return Fail(OffPayrollTransactionErrors.AssetAccessNotFound);
            assetName = asset.AssetOrAccessName;
        }

        // 4) Ajuste negativo → referencia válida al original (D-12)
        if (OffPayrollTransactionRules.RequiresCorrectionReference(input.Amount, input.CorrectsTransactionPublicId))
            return Fail(OffPayrollTransactionErrors.CorrectionRequired);
        if (input.CorrectsTransactionPublicId is { } correctedId)
        {
            var original = await employeeRepo.GetOffPayrollTransactionAsync(file.PublicId, correctedId, ct);
            if (original is null) return Fail(OffPayrollTransactionErrors.CorrectedNotFound);
            if (!original.IsActive || original.CorrectsTransactionPublicId is not null || original.CurrencyCode != currency)
                return Fail(OffPayrollTransactionErrors.CorrectedInvalid);   // recomendado: misma moneda, no encadenar ajustes
        }

        return Result<OffPayrollTransactionResolved>.Success(new(currency!, typeName, assetName));
    }
}
```

Cada handler sigue el patrón de `MedicalClaims.Handlers.cs`: **gate** → estado `IsCompletedEmployee` → `ResolveAndValidateAsync` → `Create/Update` con snapshots → repo → `TouchPersonnelFile` → `PersonnelFileEmployeeAudits.LogUpdateAsync` (before/after) → commit. **Gate = manage-only** (sin self, D-06):

```csharp
var (failure, file) = await LoadForManageOffPayrollTransactionsAsync<PersonnelFileOffPayrollTransactionResponse>(
    command.PersonnelFileId, command.ConcurrencyToken /* Guid.Empty en Add */,
    tenantContext, authorizationService, personnelFileRepository, cancellationToken);
if (failure is not null) return failure;
```

### 3.6 Permiso dedicado + gates (sin autoservicio)

Dos permisos **dedicados** (lectura sensible, D-06). Archivos a tocar (igual que `MedicalClaims`, **menos** la rama self):

| Archivo | Cambio |
|---|---|
| `PersonnelFileCommon.cs` (`PersonnelFilePermissionCodes`) | `ViewOffPayrollTransactions = "PersonnelFiles.ViewOffPayrollTransactions"`, `ManageOffPayrollTransactions = "PersonnelFiles.ManageOffPayrollTransactions"` |
| `PersonnelFilePolicies.cs` | mismas 2 como `const` (políticas authn-only superset) |
| `ProvisioningConstants.cs` (semilla de roles IAM) | otorgar ambos permisos a los roles de RR. HH./Admin |
| `Program.cs` (~466-474) | `options.AddPolicy(ViewOffPayrollTransactions, b => b.Combine(policy)); options.AddPolicy(ManageOffPayrollTransactions, b => b.Combine(policy));` |
| `IPersonnelFileAuthorizationService.cs` + impl | `EnsureCanViewOffPayrollTransactionsAsync` / `EnsureCanManageOffPayrollTransactionsAsync` (vía `EnsureHasAnyClaimAsync` con permiso + `Admin` + `ManageAdministration`) |
| `PersonnelFileEmployeeHandlerBases.cs` | 2 gates nuevos **sin rama self**: `LoadForManageOffPayrollTransactionsAsync` (escritura) y `LoadCompletedEmployeeForOffPayrollReadAsync` (lectura) |
| `AuthorizationPolicyConventionGovernanceTests.cs` | agregar ambas políticas a `PersonnelFilePolicyNames` |

```csharp
// Gate de escritura — permiso-solo (D-06, sin self-service)
protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageOffPayrollTransactionsAsync<TResponse>(
    Guid personnelFileId, Guid concurrencyToken, ITenantContext tenant,
    IPersonnelFileAuthorizationService authz, IPersonnelFileRepository repo, CancellationToken ct)
{
    if (tenant.TenantId is null) return (Result<TResponse>.Failure(PersonnelFileErrors.Unauthorized), null);
    var auth = await authz.EnsureCanManageOffPayrollTransactionsAsync(tenant.TenantId.Value, ct);
    if (auth.IsFailure) return (Result<TResponse>.Failure(auth.Error), null);
    var file = await repo.GetByPublicIdAsync(personnelFileId, ct);
    if (file is null) return (Result<TResponse>.Failure(PersonnelFileErrors.NotFound), null);
    return (null, file);
}
```

### 3.7 Adjuntos (comprobantes)

Replica el stack `MedicalClaimDocument` (D-07), heredando la política general de archivos (D-11):

- **`FileEnums.cs`**: agregar `OffPayrollTransactionDocument` a `FilePurpose`.
- **Entidad** `OffPayrollTransactionDocument : TenantEntity` (calcando `PersonnelFileEmployee.cs:1265-1373`): FK a la transacción, `FilePublicId`, `DocumentTypeCatalogItemId?` (clasificación **opcional** — "de cualquier índole"), `FileName`/`ContentType`/`SizeBytes`, `Observations?`, `IsActive`, `ConcurrencyToken`.
- **CQRS** `OffPayrollTransactionDocuments.cs` + `.Handlers.cs`: `Add/Get/List/ReadUrl/Delete` (mismos gates manage-only).
- **Controlador** (endpoints anidados, ver §3.8): list / get / `read-url` (SAS) / add / delete.
- **Validación de `Purpose`**: `if (storedFile.Purpose != FilePurpose.OffPayrollTransactionDocument) return FileErrors.InvalidPurpose(...)`.
- **DbSet** `OffPayrollTransactionDocuments` + EF config + tabla `off_payroll_transaction_documents`.

### 3.8 API — controlador dedicado y rutas

`OffPayrollTransactionsController` (dedicado, por `AuthorizationPolicySet` class-only — hallazgo #8):

```csharp
[ApiController, Authorize, Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOffPayrollTransactions, PersonnelFilePolicies.ManageOffPayrollTransactions)]
public sealed class OffPayrollTransactionsController(ICommandDispatcher commands, IQueryDispatcher queries) : ControllerBase
```

| Verbo | Ruta | Operación |
|---|---|---|
| GET | `…/{publicId}/off-payroll-transactions` | Listar |
| GET | `…/off-payroll-transactions/totals` | **Totales por moneda** (D-13) |
| GET | `…/off-payroll-transactions/export` | Exportar (xlsx) con subtotales por moneda |
| GET | `…/off-payroll-transactions/{txId}` | Consultar por id |
| POST | `…/off-payroll-transactions` | Crear (201 + ETag) |
| PUT | `…/off-payroll-transactions/{txId}` | Editar (If-Match) |
| PATCH | `…/off-payroll-transactions/{txId}` | Activar/desactivar (If-Match) |
| DELETE | `…/off-payroll-transactions/{txId}` | Baja lógica (If-Match) |
| GET/POST/DELETE | `…/off-payroll-transactions/{txId}/documents[/{docId}][/read-url]` | Adjuntos |

Contratos de request en `PersonnelFileRequests.cs` (mapeo a `OffPayrollTransactionInput`).

### 3.9 Infraestructura — repositorio

`IPersonnelFileEmployeeRepository` (+ impl `PersonnelFileEmployeeRepository.cs`), calcando los métodos de `MedicalClaim`:

```csharp
Task<PersonnelFileOffPayrollTransactionResponse>  AddOffPayrollTransactionAsync(PersonnelFileOffPayrollTransaction e, CancellationToken ct);
Task<PersonnelFileOffPayrollTransactionResponse?> UpdateOffPayrollTransactionAsync(Guid txId, Guid tenantId, OffPayrollTransactionInput input, OffPayrollTransactionResolved resolved, CancellationToken ct);
Task<PersonnelFileParentConcurrencyResult?>       SoftDeleteOffPayrollTransactionAsync(Guid txId, Guid tenantId, CancellationToken ct);   // SetActive(false), RN-10
Task<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>> GetOffPayrollTransactionsAsync(Guid fileId, CancellationToken ct);
Task<PersonnelFileOffPayrollTransactionResponse?> GetOffPayrollTransactionAsync(Guid fileId, Guid txId, CancellationToken ct);
Task<AssetAccessSnapshot?>                        GetAssetAccessAsync(Guid fileId, Guid assetId, CancellationToken ct);                    // D-01 lookup
Task<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>> GetOffPayrollTransactionTotalsAsync(Guid fileId, CancellationToken ct); // D-13
```

Totales por moneda a nivel de empleado (con signos, solo activos):

```csharp
public async Task<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>> GetOffPayrollTransactionTotalsAsync(Guid fileId, CancellationToken ct) =>
    await dbContext.Set<PersonnelFileOffPayrollTransaction>().AsNoTracking()
        .Where(t => t.PersonnelFile.PublicId == fileId && t.IsActive)
        .GroupBy(t => t.CurrencyCode)
        .Select(g => new OffPayrollTransactionCurrencyTotalResponse(g.Key, g.Sum(x => x.Amount), g.Count()))
        .OrderBy(r => r.CurrencyCode)
        .ToArrayAsync(ct);
```

### 3.10 Infraestructura — EF config y migración

EF config de la transacción (tabla `personnel_file_off_payroll_transactions`): `Amount numeric(18,2)`, `currency_code` (3), `year`/`month` int, `comment` (2000), `asset_access_public_id`/`corrects_transaction_public_id` (uuid?), `concurrency_token` `IsConcurrencyToken()`, FK a `personnel_files` (cascade), índice único `public_id`, índices `(tenant_id, personnel_file_id, transaction_date_utc)`, `(tenant_id, personnel_file_id, year, month)`, `(currency_code)`, `(asset_access_public_id)`.

```bash
export DOTNET_ROLL_FORWARD=Major   # EF 9.0.9 (memoria equipo-acceso)
dotnet ef migrations add AddOffPayrollTransactionsAndCatalog \
  --project src/CLARIHR.Infrastructure --startup-project src/CLARIHR.Api
```

La migración crea 3 tablas (`off_payroll_transaction_type_catalog_items`, `personnel_file_off_payroll_transactions`, `off_payroll_transaction_documents`). Sin `HasData` (los tipos se siembran por `DevSeedService`; si se requiere en todos los entornos → mover a `GlobalCatalogSeedData` con `HasData`, ver memoria employment-status).

### 3.11 Localización

6 errores × 3 archivos (`BackendMessages.resx`, `.es.resx`, `.es-SV.resx`). ES sugerido:

| Código | ES |
|---|---|
| `OFF_PAYROLL_TX_TYPE_CODE_INVALID` | El tipo de transacción fuera de nómina no es válido en el catálogo activo. |
| `OFF_PAYROLL_TX_CURRENCY_REQUIRED` | Se requiere una moneda y la empresa no tiene una moneda por defecto configurada. |
| `OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND` | El equipo/acceso vinculado no existe para este empleado. |
| `OFF_PAYROLL_TX_CORRECTION_REQUIRED` | Un valor negativo debe referenciar la transacción original que corrige. |
| `OFF_PAYROLL_TX_CORRECTED_NOT_FOUND` | La transacción original referenciada no existe para este empleado. |
| `OFF_PAYROLL_TX_CORRECTED_INVALID` | La transacción original debe ser un gasto original activo en la misma moneda. |

### 3.12 Auditoría

Toda alta/edición/baja y operación de adjunto: `PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, file, "…off-payroll transaction…", response, ct)` (overload before/after), dentro de la transacción del `unitOfWork`.

---

## 4. Migración de datos

Feature **net-new**: no hay datos preexistentes que migrar. Solo `CreateTable` × 3 + `insertData` del seed de tipos (dev). Rollback = `Down` elimina las 3 tablas. Sin alteración de tablas existentes salvo el nuevo valor de enum `FilePurpose` (no requiere cambio de esquema; es columna de texto/int existente en `stored_files`).

---

## 5. Mapa de errores

| Disparador | Código | ErrorType → HTTP | Capa |
|---|---|---|---|
| `transactionTypeCode` vacío / `amount`=0 / `month`∉[1,12] / `year` fuera de rango / fecha futura | `common.validation` | Validation → **400** | Validador |
| Negativo sin referencia (parte pura) | `common.validation` | Validation → **400** | Validador |
| Tipo fuera de catálogo | `OFF_PAYROLL_TX_TYPE_CODE_INVALID` | Unprocessable → **422** | Handler |
| Moneda ausente y sin default de empresa | `OFF_PAYROLL_TX_CURRENCY_REQUIRED` | Unprocessable → **422** | Handler |
| AssetAccess inexistente / de otro empleado | `OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND` | Unprocessable → **422** | Handler |
| Negativo sin referencia (defensa servidor) | `OFF_PAYROLL_TX_CORRECTION_REQUIRED` | Unprocessable → **422** | Handler |
| Original referenciado inexistente / de otro empleado | `OFF_PAYROLL_TX_CORRECTED_NOT_FOUND` | Unprocessable → **422** | Handler |
| Original inactivo / es ajuste / distinta moneda | `OFF_PAYROLL_TX_CORRECTED_INVALID` | Unprocessable → **422** | Handler |
| Adjunto con `Purpose` incorrecto / tamaño no permitido | `FILE_*` / `common.validation` | 400/413/422 | Files |
| Sin `Manage/ViewOffPayrollTransactions` | (gate) | Forbidden → **403** | Handler |
| Expediente no completado | `STATE_RULE_VIOLATION` | Conflict/422 | Handler |
| `If-Match` no coincide | `CONCURRENCY_CONFLICT` | Conflict → **409** | Handler |
| Transacción/adjunto inexistente | `ITEM_NOT_FOUND` | NotFound → **404** | Handler |

---

## 6. Plan de pruebas

**Unitarias** (`OffPayrollTransactionRulesAndValidatorTests.cs`):
- `RequiresCorrectionReference`: negativo sin ref → true; negativo con ref → false; positivo → false.
- `IsValidPeriod`: 0/13 mes → false; 1/12 → true; año límites.
- Validador: amount=0 falla; month=13 falla; fecha futura falla; negativo sin `corrects` falla; currency len≠3 falla; caso válido pasa.

**Integración** (`CLARIHR.Api.IntegrationTests`):
- CRUD completo (alta/lectura/edición/baja lógica) con `If-Match`.
- 422 por tipo fuera de catálogo; por AssetAccess de otro empleado; por negativo sin/with referencia inválida.
- Totales por moneda a nivel de empleado (con signos: positivo + ajuste negativo neto correcto).
- Adjuntos: subir con `Purpose` correcto, listar, `read-url`, eliminar; rechazo de `Purpose` incorrecto.
- 403 sin permiso `Manage`/`View`; aislamiento multi-tenant.

**Guardrail:**
- `AuthorizationPolicyConventionGovernanceTests` verde con las 2 políticas nuevas en `PersonnelFilePolicyNames`.
- `BackendMessageLocalizationTests` verde (6 códigos en EN/ES/es-SV, paridad de claves).
- Guardrail de `GeneralCatalogKeyMap` (bijección categoría↔wire key).

---

## 7. Orden de implementación (PRs sugeridos)

1. **PR-1 — Catálogo de tipos** (§3.1, §3.2): clase `OffPayrollTransactionTypeCatalogItem` + categoría + wire key + `CatalogCodeIsActiveAsync` + DbSet + EF config + seed SV (6 tipos) + migración parcial. Aislado, verde con guardrail de key map.
2. **PR-2 — Permisos + gates** (§3.6): constantes + políticas + `Program.cs` + servicio (iface/impl) + 2 gates **manage-only/view-only** (sin self) + semilla de roles + actualización del governance test.
3. **PR-3 — Dominio + EF + migración** (§3.1, §3.10): entidad `PersonnelFileOffPayrollTransaction` (campos incl. `Year/Month`, `AssetAccessPublicId`, `CorrectsTransactionPublicId`, snapshots) + EF config + índices + migración (tabla principal).
4. **PR-4 — Reglas + aplicación** (§3.3, §3.4, §3.5, §3.9): `Rules`/`Errors`, `Input/Response`, validador, `WriteSupport` (catálogo + moneda default + **AssetAccess (D-01)** + **referencia de ajuste (D-12)** + snapshots), handlers, repo `Add/Update/SoftDelete/Get`.
5. **PR-5 — API** (§3.8): `OffPayrollTransactionsController` dedicado + rutas CRUD + contratos + concurrencia `If-Match`.
6. **PR-6 — Totalización por moneda** (§3.4, §3.9): query + endpoint `…/totals` + `GroupBy` por `CurrencyCode` a nivel de empleado (D-13) + exportación.
7. **PR-7 — Adjuntos** (§3.7): `FilePurpose.OffPayrollTransactionDocument` + entidad `OffPayrollTransactionDocument` + CQRS + endpoints (list/get/read-url/add/delete) + EF/migración.
8. **PR-8 — Localización + auditoría + tests** (§3.11, §3.12, §6): 6 errores × 3 resx, auditoría before/after, batería unitaria + integración + guardrails verdes.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 (confusión con `PayrollTransaction`):** nombres/rutas explícitos `off-payroll-transactions` vs `payroll-transactions`; revisar en code review que no se mezclen DbSets/repos.
- **R-T2 (moneda de los ajustes):** un ajuste negativo en moneda distinta a la original rompería la totalización por moneda. Mitigado por `OFF_PAYROLL_TX_CORRECTED_INVALID` (misma moneda).
- **R-T3 (cadenas de ajustes):** evitar que un ajuste corrija a otro ajuste (`CorrectsTransactionPublicId is not null` ⇒ inválido), para mantener totales coherentes.
- **R-T4 (aprobación futura, D-09):** dejar el `StatusCode` como punto de extensión reservado evita una migración disruptiva al añadir el workflow.
- **R-T5 (EF 9.0.9):** `DOTNET_ROLL_FORWARD=Major` antes de `dotnet ef`, o el comando falla.
- **R-T6 (paridad de localización):** olvidar una clave en `.es-SV.resx` rompe el build → completar las 3 resx en el mismo PR (PR-8).
- **R-T7 (governance):** olvidar registrar las 2 políticas en `PersonnelFilePolicyNames` rompe el governance test → incluido en PR-2.

---

## 9. Checklist de implementación

- [ ] Catálogo `OffPayrollTransactionTypeCatalogItem` + categoría + wire key + `CatalogCodeIsActiveAsync` + DbSet + EF config + seed (6 tipos)
- [ ] Permisos `View/ManageOffPayrollTransactions` (constantes, políticas, `Program.cs`, servicio iface/impl, semilla de roles)
- [ ] Gates **sin self-service** (`LoadForManageOffPayrollTransactionsAsync`, `LoadCompletedEmployeeForOffPayrollReadAsync`)
- [ ] Entidad `PersonnelFileOffPayrollTransaction` (Year/Month, AssetAccess, Corrects, snapshots, IsActive, ConcurrencyToken) + EF config + índices
- [ ] `Rules`/`Errors` + validador (amount≠0, month 1-12, año, fecha no futura, negativo⇒referencia)
- [ ] `WriteSupport`: catálogo + moneda default + AssetAccess (mismo empleado) + referencia de ajuste (existe/activa/mismo empleado/misma moneda/no-ajuste) + snapshots
- [ ] Handlers (Add/Update/SoftDelete/Get/GetById) + repo + `Map`
- [ ] Controlador dedicado + rutas CRUD + `If-Match`
- [ ] Query + endpoint de **totales por moneda** (nivel empleado) + exportación
- [ ] Adjuntos: `FilePurpose` + entidad + CQRS + endpoints + EF/migración
- [ ] 6 errores × 3 resx (EN/ES/es-SV)
- [ ] Auditoría before/after en todas las escrituras
- [ ] Migración `AddOffPayrollTransactionsAndCatalog` (`DOTNET_ROLL_FORWARD=Major`)
- [ ] `AuthorizationPolicyConventionGovernanceTests` + `BackendMessageLocalizationTests` + key-map guardrail verdes
- [ ] Unitarias (reglas/validador) + integración (CRUD, 422s, totales, adjuntos, 403, multi-tenant)

---

> **Trazabilidad:** Este plan implementa el documento de negocio v2.1 (D-01…D-13). Reutiliza los patrones consolidados de `MedicalClaims` (entidad hija + catálogo country-scoped + reglas puras + permisos dedicados + controlador dedicado + adjuntos + auditoría/concurrencia), **omitiendo el autoservicio** (D-06, uso interno de RR. HH.) y **agregando** lo propio del requerimiento: año/mes de imputación (D-05), valores con signo + referencia de ajuste (D-04/D-12), vínculo opcional a AssetAccess (D-01), totalización por moneda a nivel de empleado (D-08/D-13). El **flujo de aprobación** queda **diferido** (D-09) con un punto de extensión (`StatusCode`) reservado. 8 PRs aislados, cada uno verde con sus guardrails.
