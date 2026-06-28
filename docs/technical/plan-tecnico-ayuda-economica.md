# Plan Técnico de Implementación — Ayuda / Asistencia Económica del Empleado (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA, Cumplimiento/Privacidad |
| **Documento de negocio** | [`docs/business/analisis-ayuda-economica-empleado.md`](../business/analisis-ayuda-economica-empleado.md) (decisiones D-01…D-16 **ratificadas 2026-06-27**) |
| **Módulos** | `PersonnelFiles` (Compensación/Bienestar) · `GeneralCatalogs` (country-scoped) · `CompanyPreference` (D-08) · Subsistema de archivos (`StoredFile`/`IFileStorageProvider`/`FilePurpose`) · `DocumentTypeCatalogs` · `IdentityAccess`/Provisioning (RBAC) · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-28 |
| **País de referencia** | El Salvador (SV) |

---

## 1. Objetivo y enfoque

Construir un **módulo nuevo (net-new)** que permita al **empleado solicitar ayuda económica** por una emergencia (autoservicio) y a **RR. HH. validarla** (aprobar / rechazar / requerir documentación), dejando el modelo **preparado para un flujo de aprobación futuro** sin construirlo (OB-06).

**Insight central del análisis de código.** No existe nada de "ayuda económica" (búsqueda nula). El módulo se arma **combinando tres plantillas ya probadas**, sin reutilizar ninguna directamente (la ayuda es un **flujo entrante**: el empleado pide, la empresa decide y paga, RR. HH. valida):

1. **Comportamiento** — `MedicalClaims` (autoservicio de creación + estado-catálogo + resolución + adjuntos + `ResponseTimeDays` derivado). Es el espejo más cercano: gate self-service `LoadForCreateOwnOrManageMedicalClaimAsync`, lectura perm-o-titular, escritura RR. HH., catálogos country-scoped, derivación de tiempo de respuesta.
2. **Datos financieros** — `OffPayrollTransaction` (monto + moneda ISO + tipo-catálogo + snapshots + adjuntos + `numeric(18,2)`).
3. **Acción de validación con guardia anti-auto-aprobación** — `SalaryTabulatorChangeRequest` (`Approve(decidedByUserId, decidedAtUtc, comment, allowSelfApproval)`, endpoints `PATCH …/approve|reject`, `IDateTimeProvider.UtcNow`, `If-Match`).

**El único componente sin plantilla directa** es la **regla de elegibilidad por antigüedad mínima (D-08)**. El análisis de código confirma que es de bajo riesgo: `CompanyPreference` **ya tiene exactamente la forma necesaria** — un umbral nullable en meses (`FileUpToDateThresholdMonths`) con setter dedicado, validador y respuesta — y la antigüedad ya se calcula con `EmployeeSeniority.Between(startUtc, asOfUtc)`. Se replica ese patrón.

Resumen de decisiones que guían el diseño (de §17 del negocio): **subsidio NO reembolsable** (D-01/D-13) · **autoservicio** del empleado (D-02) · validación dentro de **`Manage`**, nunca auto-aprobación (D-03) · estado como **catálogo** country-scoped (D-04) · aprobación **parcial > 0** (D-05) · adjunto **opcional** (D-06) · **sin topes** (D-07) · **antigüedad mínima configurable** (D-08) · desembolso **informativo** (D-09) · motivo **sensible** (D-10) · auto-cancelación del titular si está pendiente (D-11) · rechazo en **texto libre** (D-12) · **7 tipos** SV (D-14).

---

## 2. Línea base verificada en el código (qué se reutiliza)

| # | Tema | Hallazgo (archivo) | Implicación |
|---|---|---|---|
| 1 | Plantilla de comportamiento | `PersonnelFileMedicalClaim` + `MedicalClaimDocument` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:994-1373`); `…/Compensation/MedicalClaims*.cs`; gate self-service `LoadForCreateOwnOrManageMedicalClaimAsync` (`…/Common/PersonnelFileEmployeeHandlerBases.cs:227-264`) | Espejo casi 1:1 (entidad + CQRS + adjuntos + gates). |
| 2 | Plantilla financiera | `PersonnelFileOffPayrollTransaction` (`PersonnelFileEmployee.cs:1384-1533`); `OffPayrollTransactionTypeCatalogItem` (`GeneralCatalogs/GeneralCatalogItems.cs:254-280`) | Monto `numeric(18,2)`, snapshots, `DocumentTypeCatalogItemId` **nullable**. |
| 3 | Acción de validación | `SalaryTabulatorChangeRequest.Approve/Reject/Cancel` (`Domain/SalaryTabulator/SalaryTabulatorChangeRequest.cs:126-176`): guardia `!allowSelfApproval && decidedByUserId == RequestedByUserId`; endpoints `PATCH …/approve|reject` (`Api/Controllers/SalaryTabulatorController.cs:299-345`) con `[FromIfMatch]` | Patrón de la acción `…/resolution`; reloj `IDateTimeProvider`. |
| 4 | Reloj | `IDateTimeProvider.UtcNow` (`Application/Abstractions/Time/IDateTimeProvider.cs`) | **No** usar `DateTime.UtcNow` directo en handlers/dominio. |
| 5 | Catálogo country-scoped (recipe) | base abstracta `GeneralCatalogItem` (`GeneralCatalogItems.cs:5-22`); config `GeneralCatalogItemConfigurationBase<T>` (`Configurations/GeneralCatalogs/GeneralCatalogItemConfiguration.cs:7-16`); categorías `PersonnelCurriculumCatalogCategories` (`Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs:80-113`); wire-keys `GeneralCatalogKeyMap.CatalogKeys` (`…/Catalogs/GeneralCatalogKeyMap.cs:16-55`); validación `IPersonnelFileRepository.CatalogCodeIsActiveAsync(companyId, category, code)` | Replicar 2× (`economic-aid-types`, `economic-aid-statuses`). |
| 6 | Seed por país (HasData) | `GlobalCatalogSeedData.CreateGeneralCatalogSeed(prefix,id,country,code,name,sort)` + `ResolveCountryId("SV")` (`Persistence/GlobalCatalogSeedData.cs:664-691`); IDs negativos en uso hasta **-9514**; **bloque libre `-9520…-9599`** | Seed SV vía el `base(...)` de la config (no `DevSeedService`). |
| 7 | Moneda default | `CompanyPreference.CurrencyCode` (`Domain/Preferences/CompanyPreference.cs:22`, default "USD"); `ICompanyPreferenceRepository.GetByTenantIdAsync` | Default cuando se omite moneda. |
| 8 | **D-08 — preferencia umbral** | `CompanyPreference.FileUpToDateThresholdMonths` (`CompanyPreference.cs:30`) + `SetDashboardSettings` (`:52-64`) + `CompanyPreferenceResponse`/`UpdateCompanyPreferencesCommand`/validador (`Features/Preferences/Company/CompanyPreferenceAdministration.cs:15-76`) | **Patrón exacto** para `MinimumSeniorityMonthsForEconomicAid`. |
| 9 | **D-08 — antigüedad** | `EmployeeSeniority.Between(startUtc, asOfUtc)` → `Years/Months/Days/TotalDays` (`Features/PersonnelFiles/Employment/EmployeeProfiles.cs:42-74`); `PersonnelFileEmployee.HireDate` (`PersonnelFileEmployee.cs:46`) | Meses de servicio = `Years*12 + Months`. |
| 10 | Permisos (patrón) | `ViewMedicalClaims`/`ManageMedicalClaims`: const `PersonnelFileCommon.cs`/`PersonnelFilePolicies.cs`; seed `ProvisioningConstants.cs`; policies `Program.cs` (View=authn-only superset, Manage=+`RequireAssertion`); iface/impl `IPersonnelFileAuthorizationService`/`PersonnelFileAuthorizationService` | Añadir `View/ManageEconomicAidRequests`. |
| 11 | Self-service (gates) | `LoadForCreateOwnOrManageMedicalClaimAsync` (create-own), `LoadCompletedEmployeeForMedicalClaimReadAsync` (read perm-o-self), `LoadForManage…` (`PersonnelFileEmployeeHandlerBases.cs`) | Reusar el patrón (titular = `LinkedUserPublicId == currentUser`). |
| 12 | Adjuntos | `StoredFile`/`IFileStorageProvider`/`FilePurpose` (`Domain/Files/FileEnums.cs`: …`MedicalClaimDocument, OffPayrollTransactionDocument`); flujo `upload-session`→`complete`→`read-url`; `appsettings.json` `Storage.Purposes` | + `FilePurpose.EconomicAidRequestDocument` + sección appsettings. |
| 13 | `[ResourceActions]`/AllowedActions | `ISupportsAllowedActions` + `AllowedActionsCoverageIntegrationTests` | Si el controlador opta a `[ResourceActions]`, los DTO de PUT/PATCH/acción **deben** implementar `ISupportsAllowedActions` (gotcha competencias). |
| 14 | Localización | `BackendMessages.resx`/`.es.resx`/`.es-SV.resx`; paridad `BackendMessageLocalizationTests` | ~9 códigos nuevos × 3 resx. |
| 15 | Reglas puras (test) | `MedicalClaims.Rules.cs` + `MedicalClaimRulesAndValidatorTests` | Espejo `EconomicAidRequest.Rules.cs` + test. |

---

## 3. Arquitectura de la solución

### 3.1 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs`

Nueva entidad `PersonnelFileEconomicAidRequest : TenantEntity` (espejo estructural de `PersonnelFileMedicalClaim` + campos de resolución de `SalaryTabulatorChangeRequest`):

```csharp
public sealed class PersonnelFileEconomicAidRequest : TenantEntity
{
    public long PersonnelFileId { get; private set; }
    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Solicitud (autoservicio)
    public string EconomicAidTypeCode { get; private set; } = string.Empty;   // catálogo economic-aid-types
    public string? TypeNameSnapshot { get; private set; }                     // snapshot de la descripción
    public string RequestStatusCode { get; private set; } = EconomicAidRequestStatuses.Solicitada; // catálogo
    public string Description { get; private set; } = string.Empty;           // motivo (dato sensible, D-10)
    public decimal RequestedAmount { get; private set; }                      // > 0 (D-05)
    public string CurrencyCode { get; private set; } = string.Empty;          // ISO-4217
    public DateTime RequestDateUtc { get; private set; }                      // no futura
    public Guid RequestedByUserId { get; private set; }                       // quién creó (titular o RR. HH.)

    // Validación por RR. HH. (un paso, D-03)
    public decimal? ApprovedAmount { get; private set; }                      // > 0 al aprobar (D-05)
    public Guid? ResolvedByUserId { get; private set; }                       // quién validó
    public DateTime? ResolutionDateUtc { get; private set; }                  // ≥ RequestDateUtc
    public string? ResolutionNotes { get; private set; }                      // texto libre (D-12)
    public int? ResponseTimeDays { get; private set; }                        // DERIVADO

    // Desembolso (informativo, D-09)
    public decimal? DisbursedAmount { get; private set; }
    public DateTime? DisbursementDateUtc { get; private set; }                // ≥ ResolutionDateUtc
    public string? PaymentMethodCode { get; private set; }                    // catálogo payment-methods (opcional)

    public bool IsActive { get; private set; } = true;                        // baja lógica
    public Guid ConcurrencyToken { get; private set; }

    private readonly List<EconomicAidRequestDocument> _documents = new();
    public IReadOnlyCollection<EconomicAidRequestDocument> Documents => _documents.AsReadOnly();
}
```

**Códigos canónicos (híbrido código + catálogo).** El estado se **valida** contra el catálogo (D-04, configurable/i18n), pero la **lógica de transición** del dominio referencia constantes canónicas — mismo patrón que `MedicalClaimClaimantTypes` (código constreñido) + catálogo. Así el flujo futuro puede **agregar** estados intermedios en el catálogo sin tocar el dominio (RF-011):

```csharp
public static class EconomicAidRequestStatuses
{
    public const string Solicitada = "SOLICITADA";
    public const string EnRevision = "EN_REVISION";
    public const string PendienteDocumentacion = "PENDIENTE_DOCUMENTACION";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Desembolsada = "DESEMBOLSADA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlyCollection<string> Pending =
        new[] { Solicitada, EnRevision, PendienteDocumentacion };
    public static readonly IReadOnlyCollection<string> ResolutionTargets =
        new[] { EnRevision, PendienteDocumentacion, Aprobada, Rechazada };
}
```

**Métodos de dominio** (transiciones con guardias; regeneran `ConcurrencyToken`; el reloj y el `decidedByUserId` los pasa el handler con `IDateTimeProvider`/`ICurrentUserService`):

```csharp
public static PersonnelFileEconomicAidRequest Create(
    Guid publicId, string typeCode, string? typeNameSnapshot, string description,
    decimal requestedAmount, string currencyCode, DateTime requestDateUtc, Guid requestedByUserId);
    // status := SOLICITADA

// Resolución por RR. HH. (APROBADA / RECHAZADA / PENDIENTE_DOCUMENTACION / EN_REVISION).
// La verificación anti-auto-aprobación (decidedByUserId != titular) la hace el handler (§3.6).
public void Resolve(string targetStatusCode, decimal? approvedAmount, Guid decidedByUserId,
    DateTime decidedAtUtc, string? notes)
{
    if (!EconomicAidRequestStatuses.Pending.Contains(RequestStatusCode))
        throw new InvalidOperationException("Only a pending request can be resolved."); // → STATE_RULE_VIOLATION
    if (targetStatusCode == EconomicAidRequestStatuses.Aprobada && !(approvedAmount > 0))
        throw new InvalidOperationException("Approved amount must be > 0.");             // → APPROVED_AMOUNT_INVALID
    RequestStatusCode = targetStatusCode;
    ApprovedAmount = targetStatusCode == EconomicAidRequestStatuses.Aprobada ? approvedAmount : null;
    ResolvedByUserId = decidedByUserId;
    ResolutionDateUtc = decidedAtUtc;
    ResolutionNotes = PersonnelFileNormalization.CleanOptional(notes);
    ResponseTimeDays = EconomicAidRequestRules.DeriveResponseTimeDays(RequestDateUtc, decidedAtUtc);
    ConcurrencyToken = Guid.NewGuid();
}

public void Disburse(decimal disbursedAmount, DateTime disbursementDateUtc, string? paymentMethodCode)
{
    if (RequestStatusCode != EconomicAidRequestStatuses.Aprobada)
        throw new InvalidOperationException("Only an approved request can be disbursed."); // → STATE_RULE_VIOLATION
    // disbursementDateUtc ≥ ResolutionDateUtc se valida en el validador/handler
    DisbursedAmount = disbursedAmount;
    DisbursementDateUtc = disbursementDateUtc;
    PaymentMethodCode = PersonnelFileNormalization.CleanOptional(paymentMethodCode);
    RequestStatusCode = EconomicAidRequestStatuses.Desembolsada;
    ConcurrencyToken = Guid.NewGuid();
}

public void Cancel()
{
    if (!EconomicAidRequestStatuses.Pending.Contains(RequestStatusCode))
        throw new InvalidOperationException("Only a pending request can be canceled."); // → STATE_RULE_VIOLATION
    RequestStatusCode = EconomicAidRequestStatuses.Anulada;
    ConcurrencyToken = Guid.NewGuid();
}

public void Update(string typeCode, string? typeNameSnapshot, string description,
    decimal requestedAmount, string currencyCode);          // edición RR. HH. (campos de negocio)
public void SetActive(bool isActive);                       // baja lógica
public void AddDocument(EconomicAidRequestDocument document);
```

**Entidad hija `EconomicAidRequestDocument`** (espejo de `OffPayrollTransactionDocument` — `DocumentTypeCatalogItemId` **nullable**, "de cualquier índole"):

```csharp
public sealed class EconomicAidRequestDocument : TenantEntity
{
    public long EconomicAidRequestId { get; private set; }
    public PersonnelFileEconomicAidRequest EconomicAidRequest { get; private set; } = null!;
    public long? DocumentTypeCatalogItemId { get; private set; }      // clasificación OPCIONAL (D-06)
    public DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }
    public Guid FilePublicId { get; private set; }                   // ref. suelta a StoredFile (sin FK)
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int SizeBytes { get; private set; }
    public string? Observations { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid ConcurrencyToken { get; private set; }
    public static EconomicAidRequestDocument Create(/* … */);
    public void Inactivate();
}
```

### 3.2 Catálogos country-scoped `economic-aid-types` y `economic-aid-statuses` (D-04/D-14)

Recipe estándar (espejo idéntico a `off-payroll-transaction-types`), 2 veces.

**a) Clase de dominio** — `Domain/GeneralCatalogs/GeneralCatalogItems.cs` (espejo de `OffPayrollTransactionTypeCatalogItem`):
```csharp
public sealed class EconomicAidTypeCatalogItem : GeneralCatalogItem { /* ctors priv + Create(...) */ }
public sealed class EconomicAidStatusCatalogItem : GeneralCatalogItem { /* idem */ }
```

**b) Constantes de categoría** — `Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` → `PersonnelCurriculumCatalogCategories` (sin prefijo `Curriculum`, convención de los catálogos recientes como `OffPayrollTransactionType`/`ActionStatus`):
```csharp
public const string EconomicAidType = "EconomicAidType";
public const string EconomicAidStatus = "EconomicAidStatus";
```

**c) Wire-keys** — `Features/PersonnelFiles/Catalogs/GeneralCatalogKeyMap.cs` (dict `CatalogKeys`, plural como `action-statuses`):
```csharp
["economic-aid-types"]    = PersonnelCurriculumCatalogCategories.EconomicAidType,
["economic-aid-statuses"] = PersonnelCurriculumCatalogCategories.EconomicAidStatus,
```

**d) EF config** — `Configurations/GeneralCatalogs/GeneralCatalogItemConfiguration.cs` (2 clases). Nombres < 63 chars (verificado: `ix_economic_aid_status_catalog_items__country_active_sort` = 57):
```csharp
internal sealed class EconomicAidTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<EconomicAidTypeCatalogItem>
{
    public EconomicAidTypeCatalogItemConfiguration() : base(
        "economic_aid_type_catalog_items",
        "pk_economic_aid_type_catalog_items",
        "uq_economic_aid_type_catalog_items__public_id",
        "uq_economic_aid_type_catalog_items__country_code",
        "ix_economic_aid_type_catalog_items__country_active_sort",
        GlobalCatalogSeedData.GetEconomicAidTypeCatalogItems()) { }
}
internal sealed class EconomicAidStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<EconomicAidStatusCatalogItem>
{
    public EconomicAidStatusCatalogItemConfiguration() : base(
        "economic_aid_status_catalog_items",
        "pk_economic_aid_status_catalog_items",
        "uq_economic_aid_status_catalog_items__public_id",
        "uq_economic_aid_status_catalog_items__country_code",
        "ix_economic_aid_status_catalog_items__country_active_sort",
        GlobalCatalogSeedData.GetEconomicAidStatusCatalogItems()) { }
}
```

**e) DbSets** — `Persistence/ApplicationDbContext.cs`:
```csharp
public DbSet<EconomicAidTypeCatalogItem> EconomicAidTypeCatalogItems => Set<EconomicAidTypeCatalogItem>();
public DbSet<EconomicAidStatusCatalogItem> EconomicAidStatusCatalogItems => Set<EconomicAidStatusCatalogItem>();
```

**f) Validación por key** — `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs` → `CatalogCodeIsActiveAsync` (switch por categoría normalizada):
```csharp
"ECONOMICAIDTYPE"   => await IsCountryScopedCatalogCodeActiveAsync<EconomicAidTypeCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
"ECONOMICAIDSTATUS" => await IsCountryScopedCatalogCodeActiveAsync<EconomicAidStatusCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
```

**g) Seed SV (HasData)** — `Persistence/GlobalCatalogSeedData.cs`, **bloque libre `-9520…`** (verificado: máximo en uso = -9514):
```csharp
public static IEnumerable<object> GetEconomicAidTypeCatalogItems() =>
[
    CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9520L, "SV", "EMERGENCIA_MEDICA",   "Emergencia médica", 10),
    CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9521L, "SV", "GASTOS_FUNEBRES",     "Gastos fúnebres / fallecimiento de familiar", 20),
    CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9522L, "SV", "DESASTRE_NATURAL",    "Desastre natural", 30),
    CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9523L, "SV", "INCENDIO_VIVIENDA",   "Incendio o daño en vivienda", 40),
    CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9524L, "SV", "CALAMIDAD_DOMESTICA", "Calamidad doméstica", 50),
    CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9525L, "SV", "ACCIDENTE",           "Accidente", 60),
    CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9526L, "SV", "OTRA",                "Otra emergencia", 70),
];

public static IEnumerable<object> GetEconomicAidStatusCatalogItems() =>
[
    CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9540L, "SV", "SOLICITADA",              "Solicitada", 10),
    CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9541L, "SV", "EN_REVISION",            "En revisión", 20),
    CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9542L, "SV", "PENDIENTE_DOCUMENTACION","Pendiente de documentación", 30),
    CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9543L, "SV", "APROBADA",               "Aprobada", 40),
    CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9544L, "SV", "RECHAZADA",              "Rechazada", 50),
    CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9545L, "SV", "DESEMBOLSADA",           "Desembolsada", 60),
    CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9546L, "SV", "ANULADA",                "Anulada", 70),
];
```

> Los endpoints `GET /api/v1/general-catalogs/economic-aid-types?countryCode=SV` y `…/economic-aid-statuses` quedan disponibles **automáticamente** vía el key map. **Guardrail:** `GeneralCatalogKeyMapGuardrailsTests` exige biyección wire-key↔categoría.

### 3.3 Módulo de reglas puro — `Features/PersonnelFiles/Compensation/EconomicAidRequest.Rules.cs`

Errores dedicados + helpers puros (testeables sin BD). Las validaciones de **campo** (monto > 0, moneda len 3, fecha no futura, descripción) van en el **validador** (400).

```csharp
internal static class EconomicAidErrors
{
    public static readonly Error TypeCodeInvalid       = new("ECONOMIC_AID_TYPE_CODE_INVALID",        "...", ErrorType.UnprocessableEntity);
    public static readonly Error StatusCodeInvalid     = new("ECONOMIC_AID_STATUS_CODE_INVALID",      "...", ErrorType.UnprocessableEntity);
    public static readonly Error CurrencyRequired      = new("ECONOMIC_AID_CURRENCY_REQUIRED",        "...", ErrorType.UnprocessableEntity);
    public static readonly Error ApprovedAmountInvalid = new("ECONOMIC_AID_APPROVED_AMOUNT_INVALID",  "...", ErrorType.UnprocessableEntity);
    public static readonly Error DateIncoherent        = new("ECONOMIC_AID_DATE_INCOHERENT",          "...", ErrorType.UnprocessableEntity);
    public static readonly Error EligibilityNotMet     = new("ECONOMIC_AID_ELIGIBILITY_NOT_MET",      "...", ErrorType.UnprocessableEntity);
    public static readonly Error StateRuleViolation    = new("ECONOMIC_AID_STATE_RULE_VIOLATION",     "...", ErrorType.UnprocessableEntity);
    public static readonly Error PaymentMethodInvalid  = new("ECONOMIC_AID_PAYMENT_METHOD_INVALID",   "...", ErrorType.UnprocessableEntity);
    public static readonly Error SelfApprovalForbidden = new("ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN",  "...", ErrorType.Forbidden);
}

internal static class EconomicAidRequestRules
{
    /// Tiempo de respuesta derivado = resolución − solicitud (días); null si resolución < solicitud.
    public static int? DeriveResponseTimeDays(DateTime requestDateUtc, DateTime? resolutionDateUtc) =>
        resolutionDateUtc is { } r && r.Date >= requestDateUtc.Date
            ? (int)(r.Date - requestDateUtc.Date).TotalDays : null;

    /// (D-08) Elegibilidad por antigüedad mínima en meses. null/≤0 ⇒ sin restricción.
    public static bool MeetsMinimumSeniority(DateTime hireDateUtc, DateTime asOfUtc, int? minimumMonths)
    {
        if (minimumMonths is not > 0) return true;
        var s = EmployeeSeniority.Between(hireDateUtc, asOfUtc);
        return (s.Years * 12 + s.Months) >= minimumMonths.Value;
    }

    /// (D-05) Aprobación: monto aprobado > 0 (parcial permitido).
    public static bool IsValidApprovedAmount(string targetStatusCode, decimal? approvedAmount) =>
        targetStatusCode != EconomicAidRequestStatuses.Aprobada || approvedAmount > 0;

    public static bool IsResolutionTarget(string code) => EconomicAidRequestStatuses.ResolutionTargets.Contains(code);
}
```

### 3.4 Aplicación — comandos, queries, validadores y patch (`EconomicAidRequests.cs`)

- **Input/commands:** `EconomicAidRequestInput(TypeCode, Description, RequestedAmount, CurrencyCode?, RequestDateUtc)`. Comandos: `Add…`, `Update…`, `Delete…`, `Patch…` (campos de negocio), **`ResolveEconomicAidRequestCommand(PersonnelFileId, RequestId, TargetStatusCode, ApprovedAmount?, Notes?, ConcurrencyToken)`**, **`DisburseEconomicAidRequestCommand(…, DisbursedAmount, DisbursementDateUtc, PaymentMethodCode?, ConcurrencyToken)`**, **`CancelEconomicAidRequestCommand(PersonnelFileId, RequestId, ConcurrencyToken)`**.
- **Queries:** `GetEconomicAidRequestsQuery`, `GetEconomicAidRequestByIdQuery` (+ documentos).
- **Response:** `PersonnelFileEconomicAidRequestResponse` con todos los campos + `ResponseTimeDays` (derivado). **Si el controlador opta a `[ResourceActions]`**, terminar con `AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions` (§8 R-T4).
- **Validador de input** (400):
```csharp
RuleFor(i => i.TypeCode).NotEmpty().MaximumLength(80);
RuleFor(i => i.Description).NotEmpty().MaximumLength(2000);
RuleFor(i => i.RequestedAmount).GreaterThan(0);                                              // D-05
RuleFor(i => i.CurrencyCode).Length(3).When(i => !string.IsNullOrWhiteSpace(i.CurrencyCode));
RuleFor(i => i.RequestDateUtc).NotEmpty().LessThanOrEqualTo(_ => dateTimeProvider.UtcNow.AddDays(1));
```
  Validador de resolución: `TargetStatusCode ∈ ResolutionTargets`; `ApprovedAmount > 0` cuando target = `APROBADA`. Validador de desembolso: `DisbursedAmount ≥ 0`, fecha presente.

### 3.5 Aplicación — handlers (`EconomicAidRequests.Handlers.cs`)

**Crear (autoservicio):** gate `LoadForCreateOwnOrManageEconomicAidAsync` (§3.6). Tras `IsCompletedEmployee`:
```csharp
// 1) Tipo activo (D-04) + snapshot del nombre.
if (!await personnelFileRepository.CatalogCodeIsActiveAsync(tenantId, PersonnelCurriculumCatalogCategories.EconomicAidType, input.TypeCode, ct))
    return Fail(EconomicAidErrors.TypeCodeInvalid);
var typeName = await personnelFileRepository.GetCatalogItemNameAsync(tenantId, PersonnelCurriculumCatalogCategories.EconomicAidType, input.TypeCode, ct);

// 2) Elegibilidad por antigüedad mínima (D-08).
var pref = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, ct);
if (!EconomicAidRequestRules.MeetsMinimumSeniority(personnelFile.EmployeeProfile.HireDate, dateTimeProvider.UtcNow, pref?.MinimumSeniorityMonthsForEconomicAid))
    return Fail(EconomicAidErrors.EligibilityNotMet);

// 3) Moneda default por empresa si se omite.
var currency = string.IsNullOrWhiteSpace(input.CurrencyCode) ? (pref?.CurrencyCode ?? "USD") : input.CurrencyCode!;

// 4) requestedByUserId = usuario actual (titular o RR. HH.).
Guid.TryParse(currentUserService.UserId, out var requestedByUserId);
var entity = PersonnelFileEconomicAidRequest.Create(Guid.NewGuid(), input.TypeCode, typeName, input.Description,
    input.RequestedAmount, currency, NormalizeDate(input.RequestDateUtc), requestedByUserId);
```

**Resolver (validación RR. HH.):** gate `LoadForManageEconomicAidRequestsAsync` (manage-only). **Anti-auto-aprobación (D-03):**
```csharp
Guid.TryParse(currentUserService.UserId, out var decidedByUserId);
var isSubject = personnelFile.LinkedUserPublicId is { } linked && linked == decidedByUserId;
if (isSubject) return Fail(EconomicAidErrors.SelfApprovalForbidden);                    // 403
if (entity.ConcurrencyToken != command.ConcurrencyToken) return Fail(ConcurrencyConflict); // 409
if (!string.IsNullOrWhiteSpace(targetStatus) && !await CatalogCodeIsActiveAsync(..., EconomicAidStatus, targetStatus, ct))
    return Fail(EconomicAidErrors.StatusCodeInvalid);
entity.Resolve(command.TargetStatusCode, command.ApprovedAmount, decidedByUserId, dateTimeProvider.UtcNow, command.Notes);
```
**Desembolsar:** manage-only; valida `DisbursementDateUtc ≥ ResolutionDateUtc` (→ `DATE_INCOHERENT`), `PaymentMethodCode` activo si viene (catálogo `payment-methods`); `entity.Disburse(...)`. **Cancelar:** gate `LoadForCreateOwnOrManageEconomicAidAsync` (titular-o-manage); `entity.Cancel()`. Inyectar `ICompanyPreferenceRepository`, `ICurrentUserService`, `IDateTimeProvider`. Las excepciones de transición del dominio se mapean a `STATE_RULE_VIOLATION` (capturar `InvalidOperationException` o, preferible, **pre-validar** con `EconomicAidRequestStatuses.Pending.Contains(...)` en el handler y devolver el `Error` sin lanzar).

### 3.6 Permisos + gates (D-02/D-03/D-10)

**Permisos** (espejo `ViewMedicalClaims`/`ManageMedicalClaims`):

| Archivo | Cambio |
|---|---|
| `Common/PersonnelFileCommon.cs` | `ViewEconomicAidRequests = "PersonnelFiles.ViewEconomicAidRequests"`; `ManageEconomicAidRequests = "PersonnelFiles.ManageEconomicAidRequests"` |
| `Common/PersonnelFilePolicies.cs` | Las 2 constantes |
| `Provisioning/Common/ProvisioningConstants.cs` | 2 entradas (módulo `PersonnelFiles`) → el owner las recibe |
| `Api/Program.cs` | `ViewEconomicAidRequests` = **authn-only superset**; `ManageEconomicAidRequests` = superset **+ `RequireAssertion(HasAnyPermission(ManageEconomicAidRequests, Admin, ManageAdministration))`** |
| `Abstractions/.../IPersonnelFileAuthorizationService.cs` | `EnsureCanViewEconomicAidRequestsAsync` + `EnsureCanManageEconomicAidRequestsAsync` (default fail-closed) |
| `Infrastructure/.../PersonnelFileAuthorizationService.cs` | View → `EnsureHasAnyClaimAsync([View…, Admin, ManageAdministration], Read)`; Manage → idem con `Update` |

**Gates** — `Features/PersonnelFiles/Common/PersonnelFileEmployeeHandlerBases.cs` (espejo de los de MedicalClaims):
1. `LoadCompletedEmployeeForEconomicAidReadAsync` — lectura **perm-o-titular** (`ViewEconomicAidRequests` o `LinkedUserPublicId == currentUser`). → D-10 (403 a terceros) + autoservicio de lectura.
2. `LoadForCreateOwnOrManageEconomicAidAsync` — **crear/cancelar** lo propio o `Manage`. Reusa el patrón de `LoadForCreateOwnOrManageMedicalClaimAsync`.
3. `LoadForManageEconomicAidRequestsAsync` — **resolver / desembolsar / editar / dar de baja** (solo RR. HH., sin self-service).

> **Por qué el POST y el cancel deben ser política authn-only.** El autoservicio exige que un empleado **sin** `ManageEconomicAidRequests` pueda crear y cancelar **lo suyo**. Por eso esos endpoints se protegen con la política **authn-only** `ViewEconomicAidRequests` y la decisión fina (manage-o-titular) la toma el gate. Resolver/desembolsar/editar/borrar usan `ManageEconomicAidRequests` (con assertion) — solo RR. HH.

### 3.7 Validación por RR. HH. — endpoints de acción (forward-compatible, RF-011)

La validación se modela como **acciones** (no edición de campo), igual que `SalaryTabulator`. Esto deja el camino abierto al flujo de aprobación (mañana la acción `resolution` se vuelve el paso terminal de un flujo multinivel):

| Acción | Verbo + ruta | Política | Gate |
|---|---|---|---|
| Resolver | `PATCH …/{id}/resolution` (body: `targetStatusCode`, `approvedAmount?`, `notes?`) | `ManageEconomicAidRequests` | manage-only + anti-auto-aprobación |
| Desembolsar | `PATCH …/{id}/disbursement` (body: `disbursedAmount`, `disbursementDateUtc`, `paymentMethodCode?`) | `ManageEconomicAidRequests` | manage-only |
| Cancelar | `PATCH …/{id}/cancel` | `ViewEconomicAidRequests` (authn-only) | titular-o-manage |

Todas con `[FromIfMatch] Guid concurrencyToken` y `this.ToActionResultWithETag(result, v => v.ConcurrencyToken)`.

### 3.8 Adjuntos — `EconomicAidRequestDocument` (D-06)

Reutilizar el subsistema de archivos (no se construye almacenamiento):
- **`FilePurpose`** — `Domain/Files/FileEnums.cs`: añadir `EconomicAidRequestDocument`.
- **`appsettings.json`** (`Storage.Purposes`): nueva sección `EconomicAidRequestDocument` (copiar `OffPayrollTransactionDocument`: `MaxSizeBytes` 10 MB, `AllowedContentTypes` pdf/jpeg/png, `AllowedExtensions`, `DefaultProvider` AzureBlob, `RequiresMalwareScan` false, `ContainerOverride` `clarihr-economic-aid-request-documents`). Replicar en `appsettings.Development.json` si define purposes.
- **Entidad + colección** (§3.1), **EF config + DbSet** (espejo `OffPayrollTransactionDocumentConfiguration`), **CQRS** (`EconomicAidRequestDocuments*.cs`: Add/Delete/Get/GetById/GetReadUrl) y **controller** anidado bajo `…/economic-aid-requests/{id}/documents`, **autorizado por los gates del request** (read-url por el gate de lectura perm-o-titular; subir/eliminar por create-own-or-manage). El `Add` valida que el `StoredFile` exista, esté `Active` y `Purpose == EconomicAidRequestDocument`; el `DocumentTypeCatalogItemId` (si viene) activo.

### 3.9 D-08 — Antigüedad mínima en `CompanyPreference` (única pieza sin plantilla directa)

Replica **exacta** del patrón `FileUpToDateThresholdMonths` ya presente:

**a) Dominio** `Domain/Preferences/CompanyPreference.cs`:
```csharp
public int? MinimumSeniorityMonthsForEconomicAid { get; private set; }

/// (D-08) Antigüedad mínima (meses) para solicitar ayuda económica. null/0 ⇒ sin restricción.
public void SetEconomicAidEligibility(int? minimumSeniorityMonthsForEconomicAid)
{
    if (minimumSeniorityMonthsForEconomicAid is <= 0)
        throw new ArgumentOutOfRangeException(nameof(minimumSeniorityMonthsForEconomicAid),
            "Minimum seniority months must be greater than zero when provided.");
    MinimumSeniorityMonthsForEconomicAid = minimumSeniorityMonthsForEconomicAid;
    ConcurrencyToken = Guid.NewGuid();
}
```

**b) Aplicación** `Features/Preferences/Company/CompanyPreferenceAdministration.cs`:
- `CompanyPreferenceResponse` += `int? MinimumSeniorityMonthsForEconomicAid` (y en `Map`).
- `UpdateCompanyPreferencesCommand` += el campo; validador `+= .GreaterThan(0).When(c => c.MinimumSeniorityMonthsForEconomicAid.HasValue)`.
- `UpdateCompanyPreferencesCommandHandler`: llamar `preference.SetEconomicAidEligibility(command.MinimumSeniorityMonthsForEconomicAid)` (junto a `SetDashboardSettings`, antes de `ApplyUpdateAndAuditAsync`).
- *(El PATCH applier solo cubre `currencyCode`/`timeZone`; el umbral va por PUT, igual que las settings del dashboard — sin cambios en el applier.)*

**c) EF config** `Configurations/Preferences/CompanyPreferenceConfiguration.cs`: mapear `minimum_seniority_months_for_economic_aid` (int, nullable).

**d) Migración:** `AddColumn` nullable en `company_preferences` (incluida en la migración del módulo, §3.11).

**e) API** `CompanyPreferencesController` (ya existente, en el working tree): añadir el campo al contrato de PUT y al response.

### 3.10 API — controlador dedicado + contratos

**`EconomicAidRequestsController`** (clase dedicada porque `AuthorizationPolicySet` es **class-only**), `[AuthorizationPolicySet(ViewEconomicAidRequests, ManageEconomicAidRequests)]`, ruta `api/v1/personnel-files/{publicId:guid}/economic-aid-requests`. Endpoints (políticas a nivel de método):

| Endpoint | Política | Gate |
|---|---|---|
| `GET …` / `GET …/{id}` / `GET …/totals` | `View` (authn-only) | read perm-o-titular |
| `POST …` (crear) | `View` (authn-only) | create-own-or-manage |
| `PUT …/{id}` (editar) / `DELETE …/{id}` | `Manage` | manage-only |
| `PATCH …/{id}/resolution` / `…/disbursement` | `Manage` | manage-only |
| `PATCH …/{id}/cancel` | `View` (authn-only) | titular-o-manage |
| `… /{id}/documents` (GET/POST/DELETE + `read-url`) | `View`/`Manage` según verbo | gates del request |

Contratos en `Api/Contracts/PersonnelFiles/` (Add/Update/Resolution/Disbursement/Document requests). Mapear a los inputs/commands.

### 3.11 Infraestructura — repositorio, EF config y migración

- **`IPersonnelFileEmployeeRepository` + impl:** `Add/Update/Patch/Delete/Get/GetById EconomicAidRequest…` + documentos (espejo de los de `OffPayrollTransaction`). `Map(...)` proyecta todos los campos + `ResponseTimeDays`.
- **EF config** `PersonnelFileEconomicAidRequestConfiguration` (espejo de `PersonnelFileOffPayrollTransactionConfiguration`): tabla `personnel_file_economic_aid_requests`; `requested_amount`/`approved_amount`/`disbursed_amount` `numeric(18,2)`; `description` 2000; `currency_code` (3); índices `uq…public_id` + `(tenant, personnel_file_id, request_status_code)` + `(tenant, personnel_file_id, request_date_utc)`. `EconomicAidRequestDocumentConfiguration` (FK cascade al request, FK restrict a `DocumentTypeCatalogItem`, `document_type_catalog_item_id` **nullable**). Autodescubiertas por `ApplyConfigurationsFromAssembly`.
- **Migración única:**
```bash
DOTNET_ROLL_FORWARD=Major dotnet ef migrations add AddEconomicAidRequestsAndCatalogs \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```
  Contendrá: `CreateTable` de los 2 catálogos + `personnel_file_economic_aid_requests` + `economic_aid_request_documents` (+ índices/FK) · `insertData` seed SV (tipos/estados) · `AddColumn minimum_seniority_months_for_economic_aid` en `company_preferences`. Verificar con `… migrations has-pending-model-changes` (sin pendientes — sin drift).

### 3.12 Localización (~9 códigos × 3 resx)

Añadir a `BackendMessages.resx` (EN), `.es.resx`, `.es-SV.resx` los 9 códigos de `EconomicAidErrors`. Convención: código `UPPER_SNAKE`; los de campo del validador usan `common.validation` (clave `validation.message.<texto-normalizado>` derivada del `.WithMessage`). Paridad verificada por `BackendMessageLocalizationTests`.

### 3.13 Auditoría (RF-010)

Auditar alta / resolución / desembolso / edición / baja / cancelación + adjuntos con `IAuditService`/`PersonnelFileEmployeeAudits.LogUpdateAsync` pasando `before`/`after` (diff). No se auditan lecturas. La resolución registra el `ResolvedByUserId` (quién validó) en la propia entidad **y** en la auditoría.

### 3.14 Diseño forward-compatible (RF-011) — qué se RESERVA (no se construye)

- **Estado en catálogo** (§3.2): agregar estados intermedios (p. ej. `PENDIENTE_APROBACION_NIVEL_2`) no toca el dominio.
- **Validación como acción** (§3.7): `resolution` se convierte mañana en el paso terminal del flujo.
- **Entidad hija `EconomicAidApprovalStep`** (nivel, aprobador, decisión, fecha, comentario, orden): **NO** se crea ahora; la entidad raíz no la impide. Los campos `ResolvedByUserId`/`ResolutionDateUtc`/`ResolutionNotes`/`ApprovedAmount` ya existen y los consumirá el flujo.
- **Permiso `ApproveEconomicAidRequests`** dedicado: se introduce en Fase 2 (hoy la validación vive en `Manage`, D-03).

---

## 4. Migración de datos

**Net-new → sin cambios breaking.** Solo `CreateTable` + `insertData` (seed SV) + un `AddColumn` **nullable** en `company_preferences`. No hay backfill ni datos existentes que migrar. Confirmar `has-pending-model-changes` vacío tras generar la migración.

---

## 5. Mapa de errores (resumen)

| Disparador | Código | ErrorType → HTTP | Capa |
|---|---|---|---|
| `requestedAmount ≤ 0` / `description` vacía / moneda len ≠ 3 / fecha futura | `common.validation` | Validation → **400** | Validador |
| `targetStatusCode` no es de resolución / desembolso sin monto | `common.validation` | Validation → **400** | Validador |
| Tipo fuera de catálogo | `ECONOMIC_AID_TYPE_CODE_INVALID` | UnprocessableEntity → **422** | Handler |
| Estado destino fuera de catálogo | `ECONOMIC_AID_STATUS_CODE_INVALID` | → **422** | Handler |
| Moneda omitida y sin default | `ECONOMIC_AID_CURRENCY_REQUIRED` | → **422** | Handler |
| Aprobar con `approvedAmount ≤ 0` | `ECONOMIC_AID_APPROVED_AMOUNT_INVALID` | → **422** | Dominio/Handler |
| Resolución < solicitud / desembolso < resolución | `ECONOMIC_AID_DATE_INCOHERENT` | → **422** | Handler |
| Antigüedad mínima no cumplida (D-08) | `ECONOMIC_AID_ELIGIBILITY_NOT_MET` | → **422** | Handler |
| Transición inválida (desembolsar no-aprobada, resolver/cancelar resuelta) | `ECONOMIC_AID_STATE_RULE_VIOLATION` | → **422** | Handler |
| Forma de pago inexistente/inactiva | `ECONOMIC_AID_PAYMENT_METHOD_INVALID` | → **422** | Handler |
| El titular intenta validar lo suyo (D-03) | `ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN` | Forbidden → **403** | Handler/Gate |
| Sin `View…` y no titular (lectura/creación/cancelación) | (gate) | Forbidden → **403** | Handler |
| Sin `Manage…` (resolver/desembolsar/editar/baja) | (política/gate) | Forbidden → **403** | API/Policy |
| `If-Match` no coincide / ausente | `CONCURRENCY_CONFLICT` / validación | **409** / **400** | Handler/Filtro |
| Expediente no completado | `STATE_RULE_VIOLATION` | → **409/422** | Handler |
| Request/adjunto inexistente | `ITEM_NOT_FOUND` | → **404** | Handler |

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- `EconomicAidRequestRulesTests` (nuevo): `DeriveResponseTimeDays` (bordes), `MeetsMinimumSeniority` (null/0 ⇒ true; justo en el umbral; por debajo ⇒ false; fecha de corte vía parámetro), `IsValidApprovedAmount`.
- `EconomicAidRequestValidatorTests` (nuevo): monto > 0, moneda len 3, descripción, fecha no futura, target de resolución válido, aprobado > 0.
- Tests de transición de dominio: `Resolve` desde no-pendiente lanza; aprobar con monto 0 lanza; `Disburse` desde no-aprobada lanza; `Cancel` desde resuelta lanza.
- `EconomicAidRequestPatchTests` (si se expone PATCH de negocio).
- `BackendMessageLocalizationTests` (existente): verde con los 9 códigos en EN+ES(+es-SV).
- (Opcional) handler tests con mocks: 422/403/409 + gate self-service (titular crea/lee lo suyo; tercero → 403; titular intenta resolver → 403).

**Integración (`tests/CLARIHR.Api.IntegrationTests/`):**
- Seeder: catálogos `economic-aid-types`/`economic-aid-statuses` (país de prueba), permisos `View/ManageEconomicAidRequests`, empleado completado con **usuario vinculado** (`LinkedUserPublicId`), `CompanyPreference` con y sin `MinimumSeniorityMonthsForEconomicAid`.
- Casos: alta feliz (autoservicio); tipo inválido (422); elegibilidad no cumplida con umbral configurado (422) y cumplida (201); moneda default aplicada; **resolución** aprobar parcial (>0) / rechazar / requerir-doc; aprobar con 0 (422); **anti-auto-aprobación** (HR que es el titular → 403); **desembolso** sobre aprobada (ok) y sobre no-aprobada (422); **cancelación** por el titular (ok) y sobre resuelta (422); lectura: titular ve solo lo suyo, tercero → 403; adjunto: upload-session→complete→link→read-url autorizado por el request; `If-Match` → 409.
- **Guardrails:** `GeneralCatalogKeyMapGuardrailsTests` (biyección); **`AllowedActionsCoverageIntegrationTests`** si el controlador opta a `[ResourceActions]`.

---

## 7. Orden de implementación (PRs sugeridos)

1. **PR-1 — Catálogos** (§3.2): 2 clases + categorías + key map + EF config + DbSets + switch `CatalogCodeIsActiveAsync` + seed SV + migración parcial (`CreateTable`+`insertData`). Aislado; verde con guardrail.
2. **PR-2 — Permisos + gates** (§3.6): constantes + políticas + seed provisioning + servicio iface/impl + 3 gates (incl. create-own + read perm-o-self + manage-only).
3. **PR-3 — Dominio + reglas + EF + migración** (§3.1, §3.3, §3.11): entidad + códigos canónicos + métodos (`Create`/`Resolve`/`Disburse`/`Cancel`/`Update`) + `EconomicAidRequest.Rules.cs` (incl. **elegibilidad por antigüedad** y derivación) + config + migración (tablas + `AddColumn` company_preferences). **Incluye D-08** (§3.9: campo + setter en `CompanyPreference` + command/validator/response).
4. **PR-4 — Aplicación (solicitud + consulta)** (§3.4, §3.5): input/response, validadores, handlers de crear/leer + validación por key + moneda default + elegibilidad + autoservicio + firmas de repo/`Map`.
5. **PR-5 — Validación RR. HH. (acciones)** (§3.5, §3.7): `resolution` (anti-auto-aprobación) + `disbursement` + `cancel` (titular-o-manage) + endpoints PATCH con `If-Match`.
6. **PR-6 — Controlador + contratos** (§3.10): `EconomicAidRequestsController` + rutas + políticas por método; contrato de `CompanyPreferences` (campo D-08).
7. **PR-7 — Adjuntos** (§3.8): `FilePurpose` + appsettings + entidad/CQRS/controller/repo/EF.
8. **PR-8 — Localización + auditoría + pruebas** (§3.12, §3.13, §6): 9 errores × 3 resx, diff before/after, batería unitaria + integración + guía de integración frontend.

> **MVP (si se recorta):** PR-1…PR-6 (solicitar + validar). Adjuntos, exportación y refinamientos van después.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Elegibilidad por antigüedad (D-08): única pieza sin plantilla directa.** Mitigado: `CompanyPreference` ya tiene el patrón exacto (`FileUpToDateThresholdMonths`) y la antigüedad se calcula con `EmployeeSeniority.Between`. Confirmar que el `PersonnelFile` cargado en el gate exponga el `HireDate` del perfil (`EmployeeProfile.HireDate`); si no, añadir la proyección al cargar.
- **R-T2 — Estado: catálogo vs. lógica de transición.** Se usa **híbrido** (catálogo para validar/i18n + códigos canónicos para la lógica). Riesgo: si un admin **renombra el código** de un estado canónico, la lógica se rompe. Mitigación: los estados se entregan semillados y su **código** se trata como estructural (editar nombre/orden sí; código no). Documentar en la guía de catálogos.
- **R-T3 — Anti-auto-aprobación.** El empleado no tiene `Manage`, así que el gate ya lo bloquea; el chequeo `decidedByUserId != LinkedUserPublicId` cubre el caso de un usuario de RR. HH. que sea también el titular. Tests dedicados.
- **R-T4 — `[ResourceActions]`/`ISupportsAllowedActions`.** Si el controlador opta a `[ResourceActions]`, **todo** DTO de PUT/PATCH/acción debe implementar `ISupportsAllowedActions` (trailing `AllowedActionsResponse? AllowedActions = null`), o `AllowedActionsCoverageIntegrationTests` falla (gotcha competencias; los tests unitarios no lo detectan). Decisión: replicar lo que hacen `MedicalClaimsController`/`OffPayrollTransactionsController` (no optar a `[ResourceActions]` salvo que se requiera).
- **R-T5 — Desembolso informativo.** No ejecuta pago. Etiquetar claramente; la integración con nómina/tesorería (reembolsables → deducción; subsidio → `OffPayrollTransaction`) es Fase 2.
- **R-T6 — Privacidad (D-10).** El `read-url` de adjuntos debe autorizarse por el gate de lectura del request (no el `/files/{id}/read-url` owner-only). El motivo y los adjuntos son sensibles: o se autoriza (permiso o titular) o **403**.
- **R-T7 — Reloj.** Usar `IDateTimeProvider.UtcNow` en handlers/validador/reglas (parámetro `asOfUtc`), nunca `DateTime.UtcNow` (rompería tests deterministas y la convención del repo).
- **R-T8 — `dotnet ef`.** Requiere `DOTNET_ROLL_FORWARD=Major` en este entorno.

---

## 9. Checklist de implementación

- [ ] **Catálogos:** 2 clases + categorías + key map + EF config + 2 DbSets + switch `CatalogCodeIsActiveAsync` + seed SV (`-9520…-9526` / `-9540…-9546`).
- [ ] **Dominio:** `PersonnelFileEconomicAidRequest` + `EconomicAidRequestStatuses` + métodos (`Create`/`Resolve`/`Disburse`/`Cancel`/`Update`/`SetActive`) + `EconomicAidRequestDocument` + colección.
- [ ] **Reglas:** `EconomicAidRequest.Rules.cs` (9 errores + `DeriveResponseTimeDays` + `MeetsMinimumSeniority` + `IsValidApprovedAmount`).
- [ ] **D-08:** `CompanyPreference.MinimumSeniorityMonthsForEconomicAid` + `SetEconomicAidEligibility` + response/command/validator + EF config + `AddColumn`.
- [ ] **Aplicación:** input/response (+ marcador AllowedActions si aplica) + validadores + handlers (crear/leer/resolver/desembolsar/cancelar) + validación por key + moneda default + elegibilidad + anti-auto-aprobación.
- [ ] **Permisos/gates:** `View` (authn-only) + `Manage` (assertion); servicio iface/impl; 3 gates.
- [ ] **API:** `EconomicAidRequestsController` + rutas + políticas por método + acciones `resolution`/`disbursement`/`cancel` con `If-Match`; contrato `CompanyPreferences` (D-08).
- [ ] **Adjuntos:** `FilePurpose.EconomicAidRequestDocument` + appsettings + entidad/CQRS/controller/repo/EF; read-url autorizado por el request.
- [ ] **Infra:** firmas de repo + `Map`; EF configs; 1 migración (tablas + seed + columna pref); `has-pending-model-changes` vacío.
- [ ] **Localización:** 9 códigos en EN + es + es-SV.
- [ ] **Auditoría:** before/after en alta/resolución/desembolso/edición/baja/cancelación + adjuntos.
- [ ] **Tests:** rules + validator + transición dominio + paridad + guardrail + integración (felices/errores/403/409 + self-service + anti-auto-aprobación + elegibilidad + adjuntos) + seeder.
- [ ] **Verificación:** `dotnet build`, `dotnet test`, `DOTNET_ROLL_FORWARD=Major dotnet ef migrations has-pending-model-changes`.

---

> **Trazabilidad.** Este plan implementa la Fase 1 del análisis de negocio (D-01…D-16, RF-001…RF-013). Sigue patrones verificados: catálogos country-scoped (`off-payroll-transaction-types`), permisos espejo de `View/ManageMedicalClaims`, gate self-service de `MedicalClaims`, acción de validación + anti-auto-aprobación de `SalaryTabulatorChangeRequest`, subsistema de archivos genérico, umbral en `CompanyPreference` (`FileUpToDateThresholdMonths`), recursos `BackendMessages*`. **El único componente sin plantilla directa es la elegibilidad por antigüedad mínima (D-08)**, y `CompanyPreference` + `EmployeeSeniority.Between` ya proveen la base. El módulo es **solo de registro + validación de un paso** (no aprueba en flujo, no paga): el desembolso es **informativo** y el flujo de aprobación queda **diseñado pero diferido** (RF-011 / Fase 2).
