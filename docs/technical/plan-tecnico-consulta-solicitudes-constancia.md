# Plan Técnico de Implementación — Solicitud y Consulta de Constancias (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA, Cumplimiento/Privacidad |
| **Documento de negocio** | [`docs/business/analisis-consulta-solicitudes-constancia.md`](../business/analisis-consulta-solicitudes-constancia.md) (decisiones D-01…D-20 **ratificadas 2026-06-28**) |
| **Módulos** | `PersonnelFiles` (Solicitudes/Autoservicio) · `GeneralCatalogs` (country-scoped ×4) · Subsistema de archivos (`StoredFile`/`IFileStorageProvider`/`FilePurpose`) · **Reportes/PDF** (`DocumentModel`/QuestPDF, `ReportExportDeliveryService`) · Compensación (lectura para el merge) · `IdentityAccess`/Provisioning (RBAC, incl. `ViewCompensation`) · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-28 |
| **País de referencia** | El Salvador (SV) |

---

## 1. Objetivo y enfoque

Construir un **módulo nuevo (net-new)** que permita al **empleado solicitar una constancia** (de salario / laboral / embajada / tiempo laborado / no descuento / recomendación) en autoservicio, a **RR. HH. procesarla, GENERAR el PDF y emitirla**, y a RR. HH. **consultarla en una bandeja company-scoped con exportación a Excel** (entregable nombrado).

**Insight central del análisis de código.** No existe nada de "constancia" (búsqueda nula). El módulo **clona el esqueleto de `EconomicAidRequest`** (la solicitud de autoservicio más reciente, 2026-06-28) **PERO sin su maquinaria financiera** (sin monto/moneda/desembolso/aprobación) y agregando cuatro piezas propias:

1. **Generación del PDF** — reutiliza QuestPDF (ya referenciado) con un **renderer dedicado de constancia** (carta con membrete/firmante/pie), fusionando datos del expediente leídos por *key/repo* (salario, cargo, antigüedad, identidad).
2. **Bandeja company-scoped + export Excel** — espejo de `PersonnelFileReportingController` + `ReportExportDeliveryService` (no la lista por expediente).
3. **Cuatro catálogos country-scoped** (tipo, estado, medio de entrega, propósito), seed SV.
4. **Configuración por empresa** (`CompanyCertificateSettings`: membrete/firmante/pie).

Resumen de decisiones que guían el diseño (de §19 del negocio): slice completo (D-01) · autoservicio del empleado (D-02) · **sin dinero** (D-03) · **ciclo lineal sin aprobación** (D-04) · **documento generado + override manual** (D-05) · destinatario obligatorio en embajada (D-06) · idioma es/en (D-07) · **bandeja company-scoped + export** (D-08) · **generación por layout en código** (D-15) · **salario server-side desde compensación** (D-16) · **config de empresa** membrete/firmante/pie (D-17) · **medio de entrega + propósito = catálogos seed SV** (D-18) · **6 tipos** SV (D-19) · **`ViewCompensation` para constancias con salario; sin firma electrónica** (D-20).

---

## 2. Línea base verificada en el código (qué se reutiliza)

| # | Tema | Hallazgo (archivo) | Implicación |
|---|---|---|---|
| 1 | **Esqueleto de la solicitud** | `PersonnelFileEconomicAidRequest` + `EconomicAidRequestDocument` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:1643-1900`); CQRS `…/Compensation/EconomicAidRequests*.cs`; reglas `EconomicAidRequest.Rules.cs`; controlador `Api/Controllers/EconomicAidRequestsController.cs` | Espejo estructural 1:1 (entidad + estados canónicos + CQRS + adjuntos + acciones PATCH + gates). **Quitar** amount/currency/disbursement. |
| 2 | Catálogo country-scoped (recipe) | base `GeneralCatalogItem` (`GeneralCatalogItems.cs:5-22`); config `GeneralCatalogItemConfigurationBase<T>`; categorías `PersonnelCurriculumCatalogCategories` (`Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs`); wire-keys `GeneralCatalogKeyMap.CatalogKeys`; validación `IPersonnelFileRepository.CatalogCodeIsActiveAsync(companyId, category, code)` + `GetCatalogItemNameAsync(...)` | Replicar **4×** (`certificate-types`, `certificate-request-statuses`, `certificate-delivery-methods`, `certificate-purposes`). |
| 3 | Seed por país (HasData) | `GlobalCatalogSeedData.CreateGeneralCatalogSeed(prefix,id,country,code,name,sort)` + `ResolveCountryId("SV")`; economic-aid usó `-9520…-9546` (bloque libre `-9520…-9599`) | Seed SV en bloque **`-9560…-9595`** (verificar máx. en uso). |
| 4 | **Generación PDF** | QuestPDF `2024.12.3` (`Directory.Packages.props:23`); `DocumentModel`/`IDocumentModelRenderer`/`QuestPdfDocumentRenderer` (`Application/Abstractions/Reports/Documents/DocumentModel.cs`, `Infrastructure/Reports/Documents/QuestPdfDocumentRenderer.cs`); ejemplo `JobProfilePdfRenderer` + `IJobProfileDocumentMapper`; registro `DocumentPdfRenderingRegistration.AddDocumentPdfRendering`; **motor por defecto `Reporting:Pdf:Engine` = Gotenberg** (QuestPDF fallback) | El `DocumentModel` genérico es **report-shaped** (no logo/carta). Se construye un **renderer dedicado** de constancia con QuestPDF directo (§3.8, R-T1). |
| 5 | Subir/leer archivos (server-side) | `IFileStorageProvider.UploadStreamAsync/OpenReadStreamAsync` (`Application/Abstractions/Files/IFileStorageProvider.cs`); `StoredFile.Create(... FileUploadType.ServerSideUpload ...)` + `MarkActive` (`Domain/Files/StoredFile.cs`); patrón de subida server-side en `ReportExportJobProcessor.cs:156-186` (`UploadStreamAsync` + `provider`/`ruleProvider`) | El PDF generado se **persiste** como `StoredFile`(ServerSideUpload)+`CertificateRequestDocument` (D-05). El logo se **lee** del Blob para el merge. |
| 6 | Bandeja + export | `PersonnelFileReporting` (`Features/PersonnelFiles/Reporting/PersonnelFileReporting.cs`): `DynamicQuery…`/`Export…` + `IPersonnelFileRepository.DynamicQueryAsync/GetExportRowsAsync`; `PersonnelFileReportingController` (rutas `companies/{companyId}/personnel-files/dynamic-query` y `…/export`); `ReportExportDeliveryService.CreateFileResultAsync<TRow>(...)` (`Api/Common/ReportExportDeliveryService.cs`); `ReportExportFileWriter` (csv/xlsx/json, **columnas = props públicas por reflexión**); `ReportPerformanceOptions.MaxSynchronousExportRows` (5000) | Bandeja y export **company-scoped** propios (§3.10): query+row DTO + controlador + `CreateFileResultAsync`. |
| 7 | **Datos del merge** | Assignment activo+primario: `IPersonnelFileEmployeeRepository.GetEmploymentAssignmentsAsync` (filtrar `IsActive && IsPrimary`); salario: `GetCompensationConceptsAsync` → `PersonnelFileCompensationConceptResponse(Value, CurrencyCode, Nature, CalculationType, AssignedPositionPublicId, IsActive)`; cargo: `IPositionSlotRepository.GetResponseByIdAsync` (`PositionSlot.Title`) / `IJobProfileRepository.GetResponseByIdAsync` (`JobProfile.Title`); antigüedad: `EmployeeSeniority.Between(HireDate, asOfUtc)` (`Features/PersonnelFiles/Employment/EmployeeProfiles.cs:42-74`); `HireDate` en el perfil; identidad: `IPersonnelFileRepository.GetIdentificationsAsync` (filtrar `IsPrimary`) | El `ICertificatePrintDataProvider` (§3.8) compone el payload del merge. |
| 8 | **`ViewCompensation`** | `IPersonnelFileAuthorizationService.EnsureCanViewCompensationAsync(companyId, ct)` (`Abstractions/.../IPersonnelFileAuthorizationService.cs`); impl en `PersonnelFileAuthorizationService` | Gate adicional para tipos que **imprimen salario** (D-20). **Ya existe** — no se crea permiso nuevo. |
| 9 | Permisos (patrón) | `View/ManageEconomicAidRequests`: const `PersonnelFileCommon.cs`/`PersonnelFilePolicies.cs`; seed `ProvisioningConstants.cs`; policies `Program.cs` (View=authn-only superset, Manage=+`RequireAssertion`); iface/impl `IPersonnelFileAuthorizationService`/impl | Añadir `View/ManageCertificateRequests`. |
| 10 | Self-service (gates) | `LoadForCreateOwnOrManageEconomicAidAsync`, `LoadCompletedEmployeeForEconomicAidReadAsync`, `LoadForManageEconomicAidRequestsAsync` (`Common/PersonnelFileEmployeeHandlerBases.cs`) | Espejo (titular = `LinkedUserPublicId == currentUser`). |
| 11 | Adjuntos (cliente) | `StoredFile`/`FilePurpose` (`Domain/Files/FileEnums.cs`: …`EconomicAidRequestDocument`); flujo `upload-session`→`complete`→`read-url`; `appsettings.json` `Storage.Purposes` | + `FilePurpose.CertificateRequestDocument` + sección appsettings (para el generado **y** el override manual). |
| 12 | Config por empresa (patrón) | `CompanyPreference` + `ICompanyPreferenceRepository` (`Domain/Preferences/CompanyPreference.cs`); logo: `FilePurpose.CompanyLogo` (ya existe) | Nueva entidad `CompanyCertificateSettings` (tenant-scoped), repo + GET/PUT (§3.11). |
| 13 | `[ResourceActions]`/AllowedActions | `ISupportsAllowedActions` + `AllowedActionsCoverageIntegrationTests` | Si un controlador opta a `[ResourceActions]`, los DTO de PUT/PATCH/acción **deben** implementar `ISupportsAllowedActions` (gotcha competencias; unit tests no lo detectan). |
| 14 | Localización | `BackendMessages.resx`/`.es.resx`/`.es-SV.resx`; paridad `BackendMessageLocalizationTests` | ~11 códigos nuevos × 3 resx. |
| 15 | Reloj | `IDateTimeProvider.UtcNow` (`Abstractions/Time/IDateTimeProvider.cs`) | **No** usar `DateTime.UtcNow` directo. |
| 16 | Reglas puras (test) | `EconomicAidRequest.Rules.cs` + `EconomicAidRequestTests` | Espejo `CertificateRequest.Rules.cs` + test. |

---

## 3. Arquitectura de la solución

### 3.1 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs`

Nueva entidad `PersonnelFileCertificateRequest : TenantEntity` (espejo de `PersonnelFileEconomicAidRequest` **sin** los campos financieros):

```csharp
public sealed class PersonnelFileCertificateRequest : TenantEntity
{
    public long PersonnelFileId { get; private set; }
    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Solicitud (autoservicio)
    public string CertificateTypeCode { get; private set; } = string.Empty;   // catálogo certificate-types
    public string? TypeNameSnapshot { get; private set; }                     // snapshot de la descripción
    public string RequestStatusCode { get; private set; } = CertificateRequestStatuses.Solicitada; // catálogo (híbrido)
    public string PurposeCode { get; private set; } = string.Empty;           // catálogo certificate-purposes
    public string? AddressedTo { get; private set; }                          // "dirigida a" (obligatorio embajada, D-06)
    public string DeliveryMethodCode { get; private set; } = string.Empty;    // catálogo certificate-delivery-methods
    public string LanguageCode { get; private set; } = "es";                  // es/en (D-07)
    public int Copies { get; private set; } = 1;                              // ≥1
    public DateTime RequestDateUtc { get; private set; }                      // no futura
    public DateTime? NeededByDateUtc { get; private set; }                    // ≥ RequestDateUtc
    public Guid RequestedByUserId { get; private set; }                       // quién creó (titular o RR. HH.)

    // Procesamiento / emisión (RR. HH., D-04 lineal)
    public Guid? IssuedByUserId { get; private set; }                         // quién emitió
    public DateTime? IssuedDateUtc { get; private set; }                      // ≥ RequestDateUtc
    public DateTime? DeliveredDateUtc { get; private set; }                   // ≥ IssuedDateUtc
    public string? ResolutionNotes { get; private set; }                      // observaciones / motivo de rechazo
    public int? ResponseTimeDays { get; private set; }                        // DERIVADO (emisión − solicitud)

    public bool IsActive { get; private set; } = true;                        // baja lógica
    public Guid ConcurrencyToken { get; private set; }

    private readonly List<CertificateRequestDocument> _documents = new();
    public IReadOnlyCollection<CertificateRequestDocument> Documents => _documents.AsReadOnly();
}
```

**Códigos canónicos (híbrido código + catálogo).** El estado se **valida** contra el catálogo (D-08/i18n), pero la **lógica de transición** referencia constantes canónicas (espejo de `EconomicAidRequestStatuses`). Igual para los **tipos que imprimen salario** (RN-17/D-20):

```csharp
public static class CertificateRequestStatuses
{
    public const string Solicitada = "SOLICITADA";
    public const string EnProceso  = "EN_PROCESO";
    public const string Emitida    = "EMITIDA";
    public const string Entregada  = "ENTREGADA";
    public const string Rechazada  = "RECHAZADA";
    public const string Anulada    = "ANULADA";

    public static readonly IReadOnlyCollection<string> Pending = new[] { Solicitada, EnProceso };
}

public static class CertificateTypes
{
    public const string Salario   = "CONSTANCIA_SALARIO";
    public const string Laboral   = "CONSTANCIA_LABORAL";
    public const string Embajada  = "CONSTANCIA_EMBAJADA";
    public const string TiempoLaborado = "CONSTANCIA_TIEMPO_LABORADO";
    public const string NoDescuento    = "CONSTANCIA_NO_DESCUENTO";
    public const string Recomendacion  = "CARTA_RECOMENDACION";

    /// (RN-17/D-20) Tipos canónicos cuya constancia imprime salario → requieren ViewCompensation.
    public static readonly IReadOnlyCollection<string> PrintsSalary = new[] { Salario, Embajada };
}
```

**Métodos de dominio** (transiciones con guardias; regeneran `ConcurrencyToken`; el reloj y los `userId` los pasa el handler con `IDateTimeProvider`/`ICurrentUserService`):

```csharp
public static PersonnelFileCertificateRequest Create(
    Guid publicId, string typeCode, string? typeNameSnapshot, string purposeCode, string? addressedTo,
    string deliveryMethodCode, string languageCode, int copies, DateTime requestDateUtc,
    DateTime? neededByDateUtc, Guid requestedByUserId);   // status := SOLICITADA

public void StartProcessing();   // SOLICITADA → EN_PROCESO

// Emisión (D-04). El handler genera el PDF (§3.8) ANTES de llamar a Issue y adjunta el documento.
public void Issue(Guid issuedByUserId, DateTime issuedAtUtc, string? notes)
{
    if (!CertificateRequestStatuses.Pending.Contains(RequestStatusCode))
        throw new InvalidOperationException("Only a pending certificate request can be issued."); // → STATE_RULE_VIOLATION
    RequestStatusCode = CertificateRequestStatuses.Emitida;
    IssuedByUserId = issuedByUserId;
    IssuedDateUtc = issuedAtUtc;
    ResolutionNotes = PersonnelFileNormalization.CleanOptional(notes);
    ResponseTimeDays = CertificateRequestRules.DeriveResponseTimeDays(RequestDateUtc, issuedAtUtc);
    ConcurrencyToken = Guid.NewGuid();
}

public void Deliver(DateTime deliveredAtUtc)   // EMITIDA → ENTREGADA (deliveredAtUtc ≥ IssuedDateUtc en validador/handler)
public void Reject(string? notes)              // pendiente → RECHAZADA
public void Cancel()                           // pendiente → ANULADA (autoservicio del titular o RR. HH.)
public void Update(/* type/purpose/addressedTo/delivery/language/copies/neededBy */); // edición RR. HH.
public void SetActive(bool isActive);
public void AddDocument(CertificateRequestDocument document);
```

**Entidad hija `CertificateRequestDocument`** (espejo de `EconomicAidRequestDocument`, **+ `IsSystemGenerated`** para distinguir el PDF generado del override manual — D-05/RF-008):

```csharp
public sealed class CertificateRequestDocument : TenantEntity
{
    public long CertificateRequestId { get; private set; }
    public PersonnelFileCertificateRequest CertificateRequest { get; private set; } = null!;
    public bool IsSystemGenerated { get; private set; }              // PDF generado (true) vs cargado manual (false)
    public Guid FilePublicId { get; private set; }                   // ref. suelta a StoredFile (sin FK)
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int SizeBytes { get; private set; }
    public string? Observations { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid ConcurrencyToken { get; private set; }
    public static CertificateRequestDocument Create(/* … incl. isSystemGenerated */);
    public void Inactivate();   // al regenerar/reemplazar, el anterior queda inactivo (historial)
}
```

**Entidad de configuración `CompanyCertificateSettings : TenantEntity`** (tenant-scoped, una por empresa — D-17):

```csharp
public sealed class CompanyCertificateSettings : TenantEntity
{
    public Guid? LogoFilePublicId { get; private set; }     // StoredFile (FilePurpose.CompanyLogo)
    public string? IssuingCity { get; private set; }        // ciudad de emisión por defecto
    public string? SignatoryName { get; private set; }
    public string? SignatoryTitle { get; private set; }
    public string? FooterText { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public void Update(Guid? logoFilePublicId, string? issuingCity, string? signatoryName, string? signatoryTitle, string? footerText);
}
```

### 3.2 Catálogos country-scoped (×4) — D-18/D-19

Recipe estándar (espejo idéntico a `economic-aid-types`), **4 veces**.

**a) Clases de dominio** — `Domain/GeneralCatalogs/GeneralCatalogItems.cs` (espejo de `EconomicAidTypeCatalogItem`):
```csharp
public sealed class CertificateTypeCatalogItem : GeneralCatalogItem { /* ctors priv + Create(...) */ }
public sealed class CertificateRequestStatusCatalogItem : GeneralCatalogItem { /* idem */ }
public sealed class CertificateDeliveryMethodCatalogItem : GeneralCatalogItem { /* idem */ }
public sealed class CertificatePurposeCatalogItem : GeneralCatalogItem { /* idem */ }
```

**b) Constantes de categoría** — `Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` → `PersonnelCurriculumCatalogCategories`:
```csharp
public const string CertificateType = "CertificateType";
public const string CertificateRequestStatus = "CertificateRequestStatus";
public const string CertificateDeliveryMethod = "CertificateDeliveryMethod";
public const string CertificatePurpose = "CertificatePurpose";
```

**c) Wire-keys** — `Features/PersonnelFiles/Catalogs/GeneralCatalogKeyMap.cs` (dict `CatalogKeys`, plural):
```csharp
["certificate-types"]             = PersonnelCurriculumCatalogCategories.CertificateType,
["certificate-request-statuses"]  = PersonnelCurriculumCatalogCategories.CertificateRequestStatus,
["certificate-delivery-methods"]  = PersonnelCurriculumCatalogCategories.CertificateDeliveryMethod,
["certificate-purposes"]          = PersonnelCurriculumCatalogCategories.CertificatePurpose,
```

**d) EF config** — `Configurations/GeneralCatalogs/GeneralCatalogItemConfiguration.cs` (4 clases `…Configuration : GeneralCatalogItemConfigurationBase<T>`). **Vigilar el límite de 63 chars de PostgreSQL** en nombres de índice: `ix_certificate_delivery_method_catalog_items__country_active_sort` = **64 → excede**. Acortar a un nombre ≤63 (p. ej. tabla `certificate_delivery_method_items` o índice `ix_cert_delivery_method_items__country_active_sort`). Verificar los 4 nombres antes de migrar.

**e) DbSets** — `Persistence/ApplicationDbContext.cs`: 4 `DbSet<…>`.

**f) Validación por key** — `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs` → `CatalogCodeIsActiveAsync` (switch por categoría normalizada) **y** `GetCatalogItemNameAsync` (para el snapshot del tipo):
```csharp
"CERTIFICATETYPE"            => await IsCountryScopedCatalogCodeActiveAsync<CertificateTypeCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
"CERTIFICATEREQUESTSTATUS"   => await IsCountryScopedCatalogCodeActiveAsync<CertificateRequestStatusCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
"CERTIFICATEDELIVERYMETHOD"  => await IsCountryScopedCatalogCodeActiveAsync<CertificateDeliveryMethodCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
"CERTIFICATEPURPOSE"         => await IsCountryScopedCatalogCodeActiveAsync<CertificatePurposeCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
// + en GetCatalogItemNameAsync: "CERTIFICATETYPE" => GetCountryScopedCatalogNameAsync<CertificateTypeCatalogItem>(...)
```

**g) Seed SV (HasData)** — `Persistence/GlobalCatalogSeedData.cs`, **bloque `-9560…-9595`** (verificar máx. en uso; economic-aid llegó a `-9546`):
```csharp
public static IEnumerable<object> GetCertificateTypeCatalogItems() =>
[
    CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9560L, "SV", "CONSTANCIA_SALARIO",        "Constancia de salario", 10),
    CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9561L, "SV", "CONSTANCIA_LABORAL",        "Constancia de trabajo (laboral)", 20),
    CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9562L, "SV", "CONSTANCIA_EMBAJADA",       "Constancia para embajada", 30),
    CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9563L, "SV", "CONSTANCIA_TIEMPO_LABORADO","Constancia de tiempo laborado", 40),
    CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9564L, "SV", "CONSTANCIA_NO_DESCUENTO",   "Constancia de no descuento", 50),
    CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9565L, "SV", "CARTA_RECOMENDACION",       "Carta de recomendación laboral", 60),
];

public static IEnumerable<object> GetCertificateRequestStatusCatalogItems() =>
[
    CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9570L, "SV", "SOLICITADA", "Solicitada", 10),
    CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9571L, "SV", "EN_PROCESO", "En proceso", 20),
    CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9572L, "SV", "EMITIDA",    "Emitida", 30),
    CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9573L, "SV", "ENTREGADA",  "Entregada", 40),
    CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9574L, "SV", "RECHAZADA",  "Rechazada", 50),
    CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9575L, "SV", "ANULADA",    "Anulada", 60),
];

public static IEnumerable<object> GetCertificateDeliveryMethodCatalogItems() =>
[
    CreateGeneralCatalogSeed("CERTIFICATE_DELIVERY_METHOD_CATALOG", -9580L, "SV", "PRESENCIAL",         "Entrega presencial", 10),
    CreateGeneralCatalogSeed("CERTIFICATE_DELIVERY_METHOD_CATALOG", -9581L, "SV", "CORREO_ELECTRONICO", "Correo electrónico", 20),
    CreateGeneralCatalogSeed("CERTIFICATE_DELIVERY_METHOD_CATALOG", -9582L, "SV", "PORTAL",             "Descarga desde el portal", 30),
];

public static IEnumerable<object> GetCertificatePurposeCatalogItems() =>
[
    CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9590L, "SV", "TRAMITE_BANCARIO",   "Trámite bancario", 10),
    CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9591L, "SV", "CREDITO",            "Solicitud de crédito", 20),
    CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9592L, "SV", "VISA_EMBAJADA",      "Visa / trámite ante embajada", 30),
    CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9593L, "SV", "TRAMITE_MIGRATORIO", "Trámite migratorio", 40),
    CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9594L, "SV", "USO_PERSONAL",       "Uso personal", 50),
    CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9595L, "SV", "OTRO",               "Otro", 60),
];
```
> Los endpoints `GET /api/v1/general-catalogs/certificate-types?countryCode=SV` (y los otros 3) quedan disponibles **automáticamente** vía el key map. **Guardrail:** `GeneralCatalogKeyMapGuardrailsTests` exige biyección wire-key↔categoría.

### 3.3 Módulo de reglas puro — `Features/PersonnelFiles/Compensation/CertificateRequest.Rules.cs`

Errores dedicados + helpers puros (testeables sin BD). Las validaciones de **campo** (copies ≥1, fecha no futura, longitudes) van en el **validador** (400).

```csharp
internal static class CertificateRequestErrors
{
    public static readonly Error TypeCodeInvalid           = new("CERTIFICATE_TYPE_CODE_INVALID",            "...", ErrorType.UnprocessableEntity);
    public static readonly Error StatusCodeInvalid         = new("CERTIFICATE_REQUEST_STATUS_CODE_INVALID",  "...", ErrorType.UnprocessableEntity);
    public static readonly Error PurposeCodeInvalid        = new("CERTIFICATE_PURPOSE_CODE_INVALID",         "...", ErrorType.UnprocessableEntity);
    public static readonly Error DeliveryMethodCodeInvalid = new("CERTIFICATE_DELIVERY_METHOD_CODE_INVALID", "...", ErrorType.UnprocessableEntity);
    public static readonly Error AddresseeRequired         = new("CERTIFICATE_ADDRESSEE_REQUIRED",           "...", ErrorType.UnprocessableEntity);
    public static readonly Error DateIncoherent            = new("CERTIFICATE_DATE_INCOHERENT",              "...", ErrorType.UnprocessableEntity);
    public static readonly Error StateRuleViolation        = new("CERTIFICATE_STATE_RULE_VIOLATION",         "...", ErrorType.UnprocessableEntity);
    public static readonly Error GenerationDataUnavailable = new("CERTIFICATE_GENERATION_DATA_UNAVAILABLE",  "...", ErrorType.UnprocessableEntity);
    public static readonly Error CompensationForbidden     = new("CERTIFICATE_COMPENSATION_FORBIDDEN",       "...", ErrorType.Forbidden);
    public static readonly Error GenerationFailed          = new("CERTIFICATE_GENERATION_FAILED",            "...", ErrorType.Failure);
    public static readonly Error ExportFormatInvalid       = new("CERTIFICATE_EXPORT_FORMAT_INVALID",        "...", ErrorType.Validation);
}

internal static class CertificateRequestRules
{
    /// Tiempo de respuesta derivado = emisión − solicitud (días); null si emisión < solicitud.
    public static int? DeriveResponseTimeDays(DateTime requestDateUtc, DateTime? issuedDateUtc) =>
        issuedDateUtc is { } i && i.Date >= requestDateUtc.Date ? (int)(i.Date - requestDateUtc.Date).TotalDays : null;

    /// (D-06) Destinatario obligatorio para embajada.
    public static bool RequiresAddressee(string typeCode) =>
        string.Equals(typeCode, CertificateTypes.Embajada, StringComparison.OrdinalIgnoreCase);

    /// (RN-17/D-20) ¿El tipo imprime salario? → requiere ViewCompensation + dato de salario disponible.
    public static bool PrintsSalary(string typeCode) => CertificateTypes.PrintsSalary.Contains(typeCode.ToUpperInvariant());
}
```

### 3.4 Aplicación — solicitud + consulta por expediente (`CertificateRequests.cs` + `.Handlers.cs`)

- **Input/commands:** `CertificateRequestInput(TypeCode, PurposeCode, AddressedTo?, DeliveryMethodCode, LanguageCode?, Copies?, RequestDateUtc, NeededByDateUtc?)`. Comandos: `Add…`, `Update…`, `Delete…` (soft-delete) y las **acciones** (§3.7). Queries por expediente: `GetCertificateRequestsQuery(PersonnelFileId)`, `GetCertificateRequestByIdQuery(...)` (autoservicio: titular ve solo lo suyo).
- **Response:** `PersonnelFileCertificateRequestResponse` con todos los campos + `ResponseTimeDays` + `IssuedDocument` (metadatos del PDF emitido, si existe). **Si el controlador opta a `[ResourceActions]`**, terminar con `AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions` (§8 R-T6).
- **Validador de input** (400):
```csharp
RuleFor(i => i.TypeCode).NotEmpty().MaximumLength(80);
RuleFor(i => i.PurposeCode).NotEmpty().MaximumLength(80);
RuleFor(i => i.DeliveryMethodCode).NotEmpty().MaximumLength(80);
RuleFor(i => i.AddressedTo).MaximumLength(500);
RuleFor(i => i.LanguageCode).Must(c => c is null or "es" or "en");
RuleFor(i => i.Copies).GreaterThan(0).When(i => i.Copies.HasValue);
RuleFor(i => i.RequestDateUtc).NotEmpty().LessThanOrEqualTo(_ => dateTimeProvider.UtcNow.AddDays(1));
RuleFor(i => i.NeededByDateUtc).GreaterThanOrEqualTo(i => i.RequestDateUtc).When(i => i.NeededByDateUtc.HasValue);
```
- **Handler de crear (autoservicio):** gate `LoadForCreateOwnOrManageCertificateAsync` (§3.6). Tras `IsCompletedEmployee`:
```csharp
// Catálogos activos (tipo/propósito/medio) + snapshot del nombre del tipo.
foreach (var (cat, code, err) in new[] {
    (PersonnelCurriculumCatalogCategories.CertificateType, input.TypeCode, CertificateRequestErrors.TypeCodeInvalid),
    (PersonnelCurriculumCatalogCategories.CertificatePurpose, input.PurposeCode, CertificateRequestErrors.PurposeCodeInvalid),
    (PersonnelCurriculumCatalogCategories.CertificateDeliveryMethod, input.DeliveryMethodCode, CertificateRequestErrors.DeliveryMethodCodeInvalid) })
    if (!await personnelFileRepository.CatalogCodeIsActiveAsync(tenantId, cat, code, ct)) return Fail(err);

if (CertificateRequestRules.RequiresAddressee(input.TypeCode) && string.IsNullOrWhiteSpace(input.AddressedTo))
    return Fail(CertificateRequestErrors.AddresseeRequired);                                   // D-06

var typeName = await personnelFileRepository.GetCatalogItemNameAsync(tenantId, PersonnelCurriculumCatalogCategories.CertificateType, input.TypeCode, ct);
Guid.TryParse(currentUserService.UserId, out var requestedByUserId);
var entity = PersonnelFileCertificateRequest.Create(Guid.NewGuid(), input.TypeCode, typeName, input.PurposeCode,
    input.AddressedTo, input.DeliveryMethodCode, input.LanguageCode ?? "es", input.Copies ?? 1,
    NormalizeDate(input.RequestDateUtc), input.NeededByDateUtc, requestedByUserId);
```

### 3.5 Aplicación — acciones de RR. HH. (`CertificateRequests.Handlers.cs`)

**Procesar:** gate `LoadForManageCertificateRequestsAsync` (manage-only); `entity.StartProcessing()`.

**Emitir (`issue`) — núcleo de la generación (§3.8):** gate manage-only.
```csharp
if (entity.ConcurrencyToken != command.ConcurrencyToken) return Fail(ConcurrencyConflict);   // 409
// (D-20) Tipos con salario exigen ViewCompensation, además de Manage.
if (CertificateRequestRules.PrintsSalary(entity.CertificateTypeCode))
{
    var comp = await authorizationService.EnsureCanViewCompensationAsync(tenantId, ct);
    if (comp.IsFailure) return Fail(CertificateRequestErrors.CompensationForbidden);          // 403
}
// 1) Componer datos del merge (servicio §3.8). Falla si faltan datos del tipo (sin assignment/salario/cargo).
var payload = await certificatePrintDataProvider.BuildAsync(personnelFile.PublicId, entity, ct);
if (payload is null) return Fail(CertificateRequestErrors.GenerationDataUnavailable);         // 422 (E-17)
// 2) Generar PDF + 3) persistir como StoredFile(ServerSideUpload) + CertificateRequestDocument(IsSystemGenerated=true).
var document = await certificateIssuanceService.GenerateAndStoreAsync(entity, payload, ct);   // §3.8
entity.AddDocument(document);
// 4) Transición de dominio.
Guid.TryParse(currentUserService.UserId, out var issuedByUserId);
entity.Issue(issuedByUserId, dateTimeProvider.UtcNow, command.Notes);
```

**Entregar (`deliver`):** manage-only; valida `DeliveredDateUtc ≥ IssuedDateUtc` (→ `DATE_INCOHERENT`); `entity.Deliver(...)`. **Rechazar (`reject`):** manage-only; `entity.Reject(notes)`. **Cancelar (`cancel`):** gate `LoadForCreateOwnOrManageCertificateAsync` (titular-o-manage); `entity.Cancel()`. Inyectar `ICurrentUserService`, `IDateTimeProvider`, `ICertificatePrintDataProvider`, `ICertificateIssuanceService`, `IPersonnelFileAuthorizationService`. Las transiciones inválidas del dominio se **pre-validan** (`CertificateRequestStatuses.Pending.Contains(...)`) y devuelven `STATE_RULE_VIOLATION` sin lanzar.

### 3.6 Permisos + gates (D-02/D-20)

**Permisos** (espejo `View/ManageEconomicAidRequests`):

| Archivo | Cambio |
|---|---|
| `Common/PersonnelFileCommon.cs` | `ViewCertificateRequests = "PersonnelFiles.ViewCertificateRequests"`; `ManageCertificateRequests = "PersonnelFiles.ManageCertificateRequests"` |
| `Common/PersonnelFilePolicies.cs` | Las 2 constantes |
| `Provisioning/Common/ProvisioningConstants.cs` | 2 entradas (módulo `PersonnelFiles`) |
| `Api/Program.cs` | `ViewCertificateRequests` = **authn-only superset**; `ManageCertificateRequests` = superset **+ `RequireAssertion(HasAnyPermission(ManageCertificateRequests, Admin, ManageAdministration))`** |
| `Abstractions/.../IPersonnelFileAuthorizationService.cs` | `EnsureCanViewCertificateRequestsAsync` + `EnsureCanManageCertificateRequestsAsync` (default fail-closed) |
| `Infrastructure/.../PersonnelFileAuthorizationService.cs` | View → `EnsureHasAnyClaimAsync([View…, Admin, ManageAdministration], Read)`; Manage → idem con `Update` |

> **`ViewCompensation` NO es permiso nuevo** — ya existe (`EnsureCanViewCompensationAsync`). Se invoca **dentro** del handler de emisión para tipos con salario (D-20).

**Gates** — `Features/PersonnelFiles/Common/PersonnelFileEmployeeHandlerBases.cs` (espejo de los de economic-aid):
1. `LoadCompletedEmployeeForCertificateReadAsync` — lectura **perm-o-titular**.
2. `LoadForCreateOwnOrManageCertificateAsync` — **crear/cancelar** lo propio o `Manage`.
3. `LoadForManageCertificateRequestsAsync` — **procesar / emitir / entregar / rechazar / editar / baja** (solo RR. HH.).

> El **POST** y el **cancel** se protegen con la política **authn-only** `ViewCertificateRequests` (decisión fina en el gate); el resto con `ManageCertificateRequests`.

### 3.7 Acciones de RR. HH. — endpoints (ciclo lineal, D-04)

| Acción | Verbo + ruta | Política | Gate |
|---|---|---|---|
| Procesar | `PATCH …/{id}/processing` | `Manage` | manage-only |
| **Emitir** | `PATCH …/{id}/issue` (body: `notes?`) | `Manage` (+ `ViewCompensation` si imprime salario) | manage-only |
| Entregar | `PATCH …/{id}/delivery` (body: `deliveredDateUtc`) | `Manage` | manage-only |
| Rechazar | `PATCH …/{id}/reject` (body: `notes?`) | `Manage` | manage-only |
| Cancelar | `PATCH …/{id}/cancel` | `View` (authn-only) | titular-o-manage |

Todas con `[FromIfMatch] Guid concurrencyToken` + `this.ToActionResultWithETag(result, v => v.ConcurrencyToken)`.

### 3.8 Generación del PDF (D-05/D-15/D-16/D-17) — la pieza propia

Tres componentes nuevos, **aislados del render genérico de reportes** (no se toca `DocumentModel`/`QuestPdfDocumentRenderer`):

**a) Proveedor de datos del merge** — `ICertificatePrintDataProvider` (Application) + impl (Infrastructure). Compone un `CertificatePrintPayload` leyendo por repo (§2 #7):
```csharp
public sealed record CertificatePrintPayload(
    string CertificateTypeCode, string LanguageCode, string? AddressedTo, string PurposeName, int Copies,
    string FullName, string IdentificationTypeName, string IdentificationNumber,
    string JobTitle, DateTime HireDate, EmployeeSeniority Seniority,
    decimal? MonthlySalary, string? CurrencyCode,                 // null si el tipo NO imprime salario
    CompanyCertificateSettingsSnapshot Settings, byte[]? LogoBytes, DateTime GeneratedAtUtc);
```
Lógica: assignment activo+primario → `PositionSlotPublicId` → cargo (`PositionSlot.Title` ?? `JobProfile.Title`); identidad primaria; `HireDate`+`EmployeeSeniority.Between(HireDate, UtcNow)`. **Si el tipo imprime salario** (`PrintsSalary`), busca el concepto `Ingreso`/`Fijo` activo del assignment (`AssignedPositionPublicId`); si no hay assignment/cargo/salario requerido → **devuelve null** (→ `GENERATION_DATA_UNAVAILABLE`, E-17). Lee `CompanyCertificateSettings` y, si hay `LogoFilePublicId`, baja los bytes del logo (`IFileStorageProvider.OpenReadStreamAsync`).

**b) Renderer dedicado de constancia** — `ICertificateDocumentRenderer` + `CertificateQuestPdfRenderer` (Infrastructure), **QuestPDF directo** (`Document.Create`) para layout de **carta**: membrete/logo (imagen), ciudad+fecha alineadas a la derecha, título centrado por tipo, **cuerpo en prosa** (texto legal por tipo/idioma, con los datos fusionados), bloque de **firma** (línea + `SignatoryName`/`SignatoryTitle`) y **pie** (`FooterText`). El texto por tipo/idioma vive en código (D-15), p. ej. un `CertificateBodyComposer` con un método por código canónico + un layout **genérico** para tipos personalizados (sin salario).
```csharp
public interface ICertificateDocumentRenderer { Task RenderAsync(CertificatePrintPayload payload, Stream destination, CancellationToken ct); }
```

**c) Servicio de emisión** — `ICertificateIssuanceService.GenerateAndStoreAsync(entity, payload, ct)` (Infrastructure): renderiza a `MemoryStream` → sube server-side y persiste:
```csharp
var objectKey = $"tenants/{tenantId:D}/certificates/{entity.PublicId:D}/{fileName}";
var rule = ruleProvider.GetRule(FilePurpose.CertificateRequestDocument);
var provider = providerResolver.Resolve(rule!.DefaultProvider);
var artifact = await provider.UploadStreamAsync(rule.ContainerOverride ?? "clarihr-files", objectKey, "application/pdf", pdfStream, ct);
var stored = StoredFile.Create(fileName, "application/pdf", artifact.SizeBytes, ".pdf",
    StorageProvider.AzureBlob, rule.ContainerOverride ?? "clarihr-files", objectKey,
    FilePurpose.CertificateRequestDocument, FileUploadType.ServerSideUpload, currentUserId, entityId: entity.PublicId);
stored.MarkActive(artifact.SizeBytes, "application/pdf");
fileRepository.Add(stored);
return CertificateRequestDocument.Create(/* isSystemGenerated: true, FilePublicId = stored.PublicId, ... */);
```
Si al regenerar ya hay un documento generado vigente, marcar el anterior `Inactivate()` (historial).

**Registro DI** — `Infrastructure/.../DependencyInjection.cs` (o `DocumentPdfRenderingRegistration`): registrar `ICertificatePrintDataProvider`, `ICertificateDocumentRenderer` (+ **`QuestPDF.Settings.License = LicenseType.Community`**, porque el motor por defecto es Gotenberg y la licencia solo se fija hoy cuando `Engine=QuestPdf`), `ICertificateIssuanceService` (scoped).

### 3.9 Documentos — descarga del PDF + override manual (RF-008)

Reutilizar el subsistema de archivos:
- **`FilePurpose`** — `Domain/Files/FileEnums.cs`: añadir `CertificateRequestDocument`.
- **`appsettings.json`** (`Storage.Purposes`): sección `CertificateRequestDocument` (copiar `EconomicAidRequestDocument`: 10 MB, pdf/jpeg/png, `ContainerOverride` `clarihr-certificate-request-documents`). Replicar en `appsettings.Development.json` si define purposes.
- **CQRS + controller anidado** `…/certificate-requests/{id}/documents`: `GET` (list/by-id), **`GET …/{docId}/read-url`** (descarga del PDF emitido, autorizada por el gate de lectura del request — titular o RR. HH.), **`POST`** (override manual: linkea un `StoredFile` ya subido vía upload-session/complete con `purpose=CertificateRequestDocument`, `IsSystemGenerated=false`), `DELETE` (manage-only). El documento generado lo crea el servicio de emisión (§3.8); este controlador solo expone lectura/override.

### 3.10 Bandeja company-scoped + export Excel (D-08/RF-004/RF-006) — entregable nombrado

Espejo de `PersonnelFileReporting` + `PersonnelFileReportingController`, **company-scoped**:
- **Query/handler** `QueryCertificateRequestsQuery(CompanyId, TypeCode?, StatusCode?, PurposeCode?, OrgUnitId?, EmployeeId?, FromUtc?, ToUtc?, Search?, PageNumber, PageSize)` → `PagedResponse<CertificateRequestListItemResponse>` (+ opcional conteos por estado, RF-004/P-06). `ExportCertificateRequestsQuery(... MaxRows?)` → `IReadOnlyCollection<CertificateRequestExportRow>`. Gate: `EnsureCanViewCertificateRequestsAsync` (RR. HH.); el empleado autoservicio no usa la bandeja (ve su lista por expediente).
- **Repo** `IPersonnelFileEmployeeRepository`: `QueryCertificateRequestsAsync(tenantId, filtros, page, size, ct)` + `GetCertificateRequestExportRowsAsync(tenantId, filtros, maxRows, ct)` (tenant-scoped; join a `PersonnelFile`/orgUnit/identidad para las columnas).
- **Export row** `CertificateRequestExportRow` (record; **las props públicas se vuelven columnas por reflexión** — `ReportExportFileWriter`): Empleado, DocumentoIdentidad, UnidadOrganizativa, Tipo, Proposito, Estado, DirigidaA, MedioEntrega, FechaSolicitud, FechaEmision, FechaEntrega, ResponsableEmision, TiempoRespuestaDias.
- **Controlador** `CertificateRequestsReportingController` (`[AuthorizationPolicySet(ViewCertificateRequests, ManageCertificateRequests)]`):
  - `POST api/v1/companies/{companyId:guid}/certificate-requests/query` → bandeja paginada/filtrada.
  - `GET api/v1/companies/{companyId:guid}/certificate-requests/export?format=xlsx&...` → reusa el servicio de exportación:
```csharp
return await reportExportDeliveryService.CreateFileResultAsync(
    this, rows, format, "certificate-requests", "CertificateRequests",
    AuditEntityTypes.PersonnelFile, ReportExportResources.CertificateRequests,
    "Exported certificate requests report.", new { typeCode, statusCode, purposeCode, /* … */ },
    CertificateRequestErrors.ExportFormatInvalid, cancellationToken);
```
  Añadir `ReportExportResources.CertificateRequests` (constante de recurso) y, si aplica, una entrada de `AuditEntityTypes`.

### 3.11 Configuración por empresa — `CompanyCertificateSettings` (D-17)

- **Dominio** (§3.1) + **EF config** `CompanyCertificateSettingsConfiguration` (tabla `company_certificate_settings`, único por `tenant_id`).
- **Repo** `ICompanyCertificateSettingsRepository.GetByTenantAsync/Upsert`.
- **CQRS** `GetCompanyCertificateSettingsQuery` / `UpdateCompanyCertificateSettingsCommand` (logo `FilePublicId` de un `StoredFile` `CompanyLogo` ya subido; ciudad/firmante/pie texto).
- **Controlador** `CompanyCertificateSettingsController`: `GET`/`PUT api/v1/companies/{companyId:guid}/certificate-settings`, gate `ManageCertificateRequests` (sin permiso nuevo). Devuelve defaults razonables si no hay configuración.

### 3.12 API — controladores dedicados + contratos

Tres controladores dedicados (clase-only por `AuthorizationPolicySet`):
1. **`CertificateRequestsController`** — `api/v1/personnel-files/{publicId:guid}/certificate-requests` (intake autoservicio + CRUD + acciones §3.7 + documentos §3.9). Políticas por método como en §3.6.
2. **`CertificateRequestsReportingController`** — bandeja + export (§3.10).
3. **`CompanyCertificateSettingsController`** — config (§3.11).

Contratos en `Api/Contracts/PersonnelFiles/CertificateRequestContracts.cs` (Add/Update/Issue/Deliver/Reject/Document/Query/Settings requests).

### 3.13 Infraestructura — repositorio, EF config y migración

- **`IPersonnelFileEmployeeRepository` + impl:** `Add/Update/SoftDelete/Get/GetById CertificateRequest…`, acciones (`StartProcessing/Issue/Deliver/Reject/Cancel` persistidas), documentos, **y** los 2 métodos company-scoped (§3.10). `Map(...)` proyecta todos los campos + `ResponseTimeDays` + metadatos del documento emitido.
- **EF config** `PersonnelFileCertificateRequestConfiguration` (espejo de economic-aid, **sin** columnas monetarias): tabla `personnel_file_certificate_requests`; `addressed_to` 500, `resolution_notes` 2000, `language_code` 10, `purpose_code`/`delivery_method_code`/`certificate_type_code`/`request_status_code` 80; índices `uq…public_id` + `(tenant, personnel_file_id, request_status_code)` + `(tenant, request_date_utc)` (para la bandeja). `CertificateRequestDocumentConfiguration` (FK cascade al request; `is_system_generated` bool). `CompanyCertificateSettingsConfiguration` (único por tenant). Autodescubiertas por `ApplyConfigurationsFromAssembly`.
- **Migración única:**
```bash
DOTNET_ROLL_FORWARD=Major dotnet ef migrations add AddCertificateRequestsAndCatalogs \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```
  Contendrá: `CreateTable` de los **4 catálogos** + `personnel_file_certificate_requests` + `certificate_request_documents` + `company_certificate_settings` (+ índices/FK) · `insertData` seed SV (4 catálogos). Verificar `… migrations has-pending-model-changes` (sin drift) y **nombres de índice ≤63 chars** (§3.2 d).

### 3.14 Localización (~11 códigos × 3 resx)

Añadir a `BackendMessages.resx` (EN), `.es.resx`, `.es-SV.resx` los códigos de `CertificateRequestErrors`. Convención `UPPER_SNAKE`; los de campo del validador usan `common.validation`. Paridad por `BackendMessageLocalizationTests`. **El texto del PDF** (cuerpos por tipo/idioma) **no** va en `.resx` de errores: vive en el `CertificateBodyComposer` (es/en) — D-15.

### 3.15 Auditoría (RF-012)

Auditar alta / procesar / **emisión (incl. generación)** / entrega / rechazo / edición / baja / cancelación + documentos + **exportación** con `IAuditService`/`PersonnelFileEmployeeAudits.LogUpdateAsync` (before/after). La emisión registra `IssuedByUserId` en la entidad y en la auditoría. La descarga del PDF (read-url) no se audita como escritura, pero sí la generación.

---

## 4. Migración de datos

**Net-new → sin cambios breaking.** Solo `CreateTable` (4 catálogos + 3 tablas de negocio) + `insertData` (seed SV de los 4 catálogos). Sin backfill. Confirmar `has-pending-model-changes` vacío.

---

## 5. Mapa de errores (resumen)

| Disparador | Código | ErrorType → HTTP | Capa |
|---|---|---|---|
| `copies ≤ 0` / fecha futura / `languageCode` inválido / longitudes | `common.validation` | Validation → **400** | Validador |
| Tipo / propósito / medio fuera de catálogo | `CERTIFICATE_*_CODE_INVALID` | UnprocessableEntity → **422** | Handler |
| Estado destino fuera de catálogo | `CERTIFICATE_REQUEST_STATUS_CODE_INVALID` | → **422** | Handler |
| Embajada sin destinatario (D-06) | `CERTIFICATE_ADDRESSEE_REQUIRED` | → **422** | Handler |
| Fechas incoherentes (requerida<solicitud / emisión<solicitud / entrega<emisión) | `CERTIFICATE_DATE_INCOHERENT` | → **422** | Handler |
| Transición inválida (emitir anulada / entregar no emitida / cancelar emitida) | `CERTIFICATE_STATE_RULE_VIOLATION` | → **422** | Handler |
| Faltan datos para generar (sin assignment/salario/cargo) (E-17) | `CERTIFICATE_GENERATION_DATA_UNAVAILABLE` | → **422** | Handler/Servicio |
| Constancia con salario sin `ViewCompensation` (D-20) | `CERTIFICATE_COMPENSATION_FORBIDDEN` | Forbidden → **403** | Handler/Gate |
| Fallo de render del PDF | `CERTIFICATE_GENERATION_FAILED` | Failure → **500/422** | Servicio |
| Formato de exportación no soportado | `CERTIFICATE_EXPORT_FORMAT_INVALID` | Validation → **400** | Controlador export |
| Sin `View…` y no titular (lectura/creación/cancelación) | (gate) | Forbidden → **403** | Handler |
| Sin `Manage…` (procesar/emitir/entregar/rechazar/editar/baja) | (política/gate) | Forbidden → **403** | API/Policy |
| `If-Match` no coincide / ausente | `CONCURRENCY_CONFLICT` / validación | **409** / **400** | Handler/Filtro |
| Expediente no completado | `STATE_RULE_VIOLATION` | → **409/422** | Handler |
| Request/adjunto inexistente | `ITEM_NOT_FOUND` | → **404** | Handler |

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- `CertificateRequestRulesTests` (nuevo): `DeriveResponseTimeDays` (bordes), `RequiresAddressee` (embajada vs resto), `PrintsSalary` (salario/embajada true; resto false; tipo personalizado false).
- `CertificateRequestValidatorTests` (nuevo): copies>0, idioma es/en, fecha no futura, neededBy≥solicitud, longitudes.
- Transición de dominio: `Issue` desde no-pendiente lanza; `Deliver` desde no-emitida lanza; `Cancel` desde resuelta lanza; `Reject` desde resuelta lanza.
- `CertificatePrintDataProviderTests` (con mocks de repos): arma payload completo; **devuelve null** cuando un tipo con salario no tiene assignment/salario (E-17); no exige salario en tipos sin salario.
- `BackendMessageLocalizationTests` (existente): verde con los ~11 códigos en EN+ES(+es-SV).
- (Opcional) handler tests con mocks: 422/403/409 + gate self-service (titular crea/lee lo suyo; tercero → 403); emisión de tipo con salario sin `ViewCompensation` → 403.

**Integración (`tests/CLARIHR.Api.IntegrationTests/`):**
- Seeder: 4 catálogos (país de prueba), permisos `View/ManageCertificateRequests` **y** `ViewCompensation`, empleado completado con **usuario vinculado** (`LinkedUserPublicId`), assignment activo+primario con **concepto de salario** (`Ingreso`/`Fijo`), identidad primaria, `CompanyCertificateSettings`.
- Casos: alta feliz (autoservicio); embajada sin destinatario (422); tipo/propósito/medio inválido (422); **emisión** de constancia laboral (sin salario) → genera PDF, `EMITIDA`, documento descargable; **emisión** de constancia de salario con `ViewCompensation` (ok) y sin él (403); emisión de salario **sin salario cargado** (422, E-17); **entregar** (ok) y entregar una no emitida (422); **cancelar** por titular (ok) y sobre emitida (422); lectura: titular ve solo lo suyo, tercero → 403; documento generado: `read-url` autorizado por el request; **override manual** (upload-session→complete→link); **bandeja** company-scoped filtra por tipo/estado/fecha y pagina; **export** xlsx con columnas correctas; `If-Match` → 409.
- **Guardrails:** `GeneralCatalogKeyMapGuardrailsTests` (biyección, 4 wire-keys); **`AllowedActionsCoverageIntegrationTests`** si algún controlador opta a `[ResourceActions]`.

> **PDF en pruebas:** el renderer de constancia usa **QuestPDF directo** (sin Gotenberg), así que la generación corre en CI sin servicios externos. Asegurar `QuestPDF.Settings.License = Community` en el registro (R-T2).

---

## 7. Orden de implementación (PRs sugeridos)

1. **PR-1 — Catálogos (×4) + seed SV** (§3.2): 4 clases + categorías + key map + EF config (nombres ≤63) + DbSets + switch `CatalogCodeIsActiveAsync`/`GetCatalogItemNameAsync` + seed SV + migración parcial. Aislado; verde con guardrail.
2. **PR-2 — Permisos + gates** (§3.6): `View/ManageCertificateRequests` (const + políticas + provisioning + iface/impl) + 3 gates.
3. **PR-3 — Dominio + reglas + EF + migración** (§3.1, §3.3, §3.13): entidad + `CertificateRequestStatuses`/`CertificateTypes` + métodos + `CertificateRequestDocument` + `CompanyCertificateSettings` + `CertificateRequest.Rules.cs` + configs + migración (tablas + seed). Sin drift.
4. **PR-4 — Aplicación: solicitud + consulta por expediente** (§3.4): input/response + validadores + handlers crear/leer/editar/baja + validación por key + autoservicio + repo/`Map`.
5. **PR-5 — Generación del PDF** (§3.8): `ICertificatePrintDataProvider` (merge reads) + `ICertificateDocumentRenderer`/`CertificateQuestPdfRenderer` (QuestPDF directo, layout carta) + `CertificateBodyComposer` (es/en) + `ICertificateIssuanceService` (persistencia server-side) + DI + licencia QuestPDF. **Testeable de forma aislada.**
6. **PR-6 — Acciones RR. HH.** (§3.5, §3.7): `processing`/`issue` (invoca PR-5 + `ViewCompensation`)/`delivery`/`reject`/`cancel` + endpoints PATCH con `If-Match`.
7. **PR-7 — Documentos** (§3.9): `FilePurpose.CertificateRequestDocument` + appsettings + CQRS/controller/repo (read-url del generado + override manual + delete).
8. **PR-8 — Bandeja company-scoped + export Excel** (§3.10): query+row DTO + repo company-scoped + `CertificateRequestsReportingController` + `ReportExportResources.CertificateRequests`.
9. **PR-9 — Configuración de empresa** (§3.11): `CompanyCertificateSettings` API (GET/PUT) + repo + logo.
10. **PR-10 — Localización + auditoría + pruebas + guía frontend** (§3.14, §3.15, §6): ~11 errores × 3 resx, diff before/after, batería unitaria + integración + `docs/technical/guia-integracion-frontend-consulta-solicitudes-constancia.md`.

> **MVP (entregable nombrado primero):** **PR-1…PR-4 + PR-8** dan la **solicitud + bandeja/consulta + export Excel** sin generación. La **generación + emisión + config** (PR-5…PR-7, PR-9) se entregan en la segunda tanda. (Coincide con §18 R-7 del negocio.)

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — El `DocumentModel` genérico es report-shaped (no carta/logo).** Decisión: **renderer dedicado con QuestPDF directo** (§3.8) para membrete/título centrado/cuerpo en prosa/firma/pie, **sin** tocar `QuestPdfDocumentRenderer` (que sirve a JobProfile). Aísla el riesgo a código nuevo.
- **R-T2 — Licencia QuestPDF + motor por defecto Gotenberg.** `QuestPDF.Settings.License = Community` solo se fija hoy cuando `Reporting:Pdf:Engine=QuestPdf`. El renderer de constancia usa QuestPDF **siempre** → fijar la licencia en su propio registro DI, o fallará en entornos con `Engine=Gotenberg`. (Ventaja: el PDF de constancia no depende del servicio Gotenberg.)
- **R-T3 — Disponibilidad de datos para generar (E-17).** Constancia de salario/embajada exige assignment activo+primario **con** concepto de salario `Ingreso`/`Fijo`. Mitigación: el `ICertificatePrintDataProvider` valida y devuelve null → `GENERATION_DATA_UNAVAILABLE` (422) con mensaje claro; el negocio asegura la carga de compensación.
- **R-T4 — Acoplamiento `ViewCompensation` (D-20).** La emisión de tipos con salario exige `EnsureCanViewCompensationAsync` **además** de Manage; el salario se inyecta server-side (nunca del cliente). Tests dedicados (403 sin permiso).
- **R-T5 — Estado/tipo: catálogo vs. lógica.** Híbrido (catálogo valida/i18n + códigos canónicos para transición y `PrintsSalary`). Riesgo si un admin **renombra el código** canónico → tratar el **código** como estructural (editar nombre/orden sí; código no). Documentar en la guía de catálogos.
- **R-T6 — `[ResourceActions]`/`ISupportsAllowedActions`.** Si un controlador opta a `[ResourceActions]`, **todo** DTO de PUT/PATCH/acción debe implementar `ISupportsAllowedActions` (trailing `AllowedActionsResponse? AllowedActions = null`) o `AllowedActionsCoverageIntegrationTests` falla (los unit tests no lo detectan). Replicar lo de `EconomicAidRequestsController` (no optar salvo que se requiera).
- **R-T7 — Nombres de índice >63 chars.** `certificate_delivery_method` produce identificadores largos; verificar los 4 índices/tablas y acortar (§3.2 d) antes de migrar.
- **R-T8 — Export por reflexión.** `ReportExportFileWriter` vuelve **columnas** las props públicas del `CertificateRequestExportRow` en **orden de declaración**; cuidar el orden/типos (fechas, nullables) y no exponer GUIDs internos.
- **R-T9 — Privacidad.** El `read-url` del PDF emitido debe autorizarse por el gate de lectura del request (titular o RR. HH.), no por el `/files/{id}/read-url` owner-only. El PDF contiene datos personales/salariales.
- **R-T10 — Reloj.** `IDateTimeProvider.UtcNow` en handlers/validador/reglas/merge; nunca `DateTime.UtcNow`.
- **R-T11 — `dotnet ef`.** Requiere `DOTNET_ROLL_FORWARD=Major` en este entorno.
- **R-T12 — Sin firma electrónica (D-20).** El PDF es informativo/para firma manual; no es documento firmado. La firma electrónica + folio/QR es Fase 2.

---

## 9. Checklist de implementación

- [ ] **Catálogos (×4):** 4 clases + categorías + key map + EF config (nombres ≤63) + 4 DbSets + switch `CatalogCodeIsActiveAsync`/`GetCatalogItemNameAsync` + seed SV (`-9560…-9595`).
- [ ] **Dominio:** `PersonnelFileCertificateRequest` + `CertificateRequestStatuses`/`CertificateTypes` + métodos (`Create`/`StartProcessing`/`Issue`/`Deliver`/`Reject`/`Cancel`/`Update`/`SetActive`) + `CertificateRequestDocument` (`IsSystemGenerated`) + `CompanyCertificateSettings`.
- [ ] **Reglas:** `CertificateRequest.Rules.cs` (~11 errores + `DeriveResponseTimeDays` + `RequiresAddressee` + `PrintsSalary`).
- [ ] **Aplicación:** input/response (+ marcador AllowedActions si aplica) + validadores + handlers (crear/leer/editar/baja + acciones) + validación por key + autoservicio + anti-terceros.
- [ ] **Generación PDF:** `ICertificatePrintDataProvider` (merge) + `ICertificateDocumentRenderer`/QuestPDF directo + `CertificateBodyComposer` (es/en) + `ICertificateIssuanceService` (persistencia server-side) + DI + licencia QuestPDF + `ViewCompensation` en emisión de salario.
- [ ] **Documentos:** `FilePurpose.CertificateRequestDocument` + appsettings + CQRS/controller/repo (read-url + override manual + delete).
- [ ] **Bandeja + export:** query+row DTO + repo company-scoped + `CertificateRequestsReportingController` + `ReportExportResources.CertificateRequests` + `CreateFileResultAsync`.
- [ ] **Config empresa:** `CompanyCertificateSettings` API (GET/PUT) + repo + logo.
- [ ] **Permisos/gates:** `View` (authn-only) + `Manage` (assertion); servicio iface/impl; 3 gates; `ViewCompensation` reutilizado.
- [ ] **API:** 3 controladores dedicados + rutas + políticas por método + acciones con `If-Match`; contratos.
- [ ] **Infra:** firmas de repo + `Map` + 2 métodos company-scoped; EF configs; 1 migración (tablas + seed); `has-pending-model-changes` vacío.
- [ ] **Localización:** ~11 códigos en EN + es + es-SV.
- [ ] **Auditoría:** before/after en alta/procesar/emisión/entrega/rechazo/edición/baja/cancelación + documentos + export.
- [ ] **Tests:** rules + validator + transición dominio + data-provider (incl. E-17) + paridad + guardrail (4 wire-keys) + integración (felices/errores/403/409 + self-service + `ViewCompensation` + generación + bandeja + export) + seeder.
- [ ] **Verificación:** `dotnet build`, `dotnet test`, `DOTNET_ROLL_FORWARD=Major dotnet ef migrations has-pending-model-changes`.

---

> **Trazabilidad.** Este plan implementa la Fase 1 del análisis (D-01…D-20, RF-001…RF-018). Clona patrones verificados: solicitud de autoservicio (`EconomicAidRequest`), catálogos country-scoped (`economic-aid-types`), permisos espejo de `View/ManageEconomicAidRequests`, gates self-service, subsistema de archivos genérico, render QuestPDF (`JobProfilePdfRenderer`), bandeja + export (`PersonnelFileReporting`/`ReportExportDeliveryService`), `ViewCompensation` para datos de compensación. **Las piezas propias** son: la **generación del PDF de constancia** (renderer dedicado + merge server-side, D-15/D-16), la **configuración por empresa** (D-17), los **2 catálogos extra** (D-18) y la **bandeja company-scoped + export** (D-08). **Sin** dinero, **sin** motor de aprobación, **sin** plantillas editables y **sin** firma electrónica (Fase 2).
