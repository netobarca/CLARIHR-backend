# Plan Técnico de Implementación — Reclamos de Seguro Médico (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA, Cumplimiento/Privacidad |
| **Documento de negocio** | [`docs/business/analisis-reclamos-seguro-medico-empleado.md`](../business/analisis-reclamos-seguro-medico-empleado.md) (decisiones D-01…D-13) |
| **Módulos** | `PersonnelFiles` (Compensación/Beneficios) · Seguros/Beneficiarios (`PersonnelFileInsurance`/`…Beneficiary`) · Subsistema de archivos (`StoredFile`/`IFileStorageProvider`/`FilePurpose`) · `DocumentTypeCatalogs` · `GeneralCatalogs` (country-scoped) · `CompanyPreference` · `IdentityAccess`/Provisioning (RBAC) · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-23 |
| **País de referencia** | El Salvador (SV) |

---

## 1. Objetivo y enfoque

Endurecer la funcionalidad **ya existente** `PersonnelFileMedicalClaim` (registro documental con CRUD completo) conforme a las decisiones **D-01…D-13**, manteniéndola como **módulo solo de registro** (D-01) y protegiendo el **diagnóstico** (dato de salud) con un permiso dedicado.

**Insight central del análisis de código.** La entidad, el CQRS (Add/Update/Patch/Delete/Get/GetList), la API (6 endpoints), la concurrencia (`ConcurrencyToken`/`If-Match`), el soft-delete y la auditoría (`PersonnelFileEmployeeAudits.LogUpdateAsync`) **ya están construidos y funcionando** (`MedicalClaims.cs` + `.Handlers.cs`). El trabajo NO es construir el CRUD; es:

1. **Agregar la dimensión paciente** (núcleo del requisito, D-02): `ClaimantType` (`TITULAR`/`BENEFICIARIO`) + `BeneficiaryPublicId` + snapshots, validados contra los beneficiarios de la póliza (RF-001).
2. **Hacer el seguro obligatorio y validado** (D-03): `InsurancePublicId` pasa a requerido; se valida vía `GetInsuranceAsync(fileId, insuranceId)` (que ya filtra por expediente) y se **proyecta/snapshotea** `InsuranceCode`+`PolicyNumber` en la respuesta (RF-002). *(Hallazgo: el "nombre del seguro" es `InsuranceCode` — **texto guardado**, no un catálogo a resolver.)*
3. **Catalogar tipo y estado** (D-04/D-10) con el patrón **country-scoped** ya estandarizado (espejo exacto de `substitution-types`): `medical-claim-types` (SV) y `medical-claim-status`.
4. **Capturar `ResolutionDateUtc` y DERIVAR `ResponseTimeDays`** (D-07) — deja de ser un entero manual.
5. **Endurecer el validador**: hoy solo valida `ClaimTypeCode.NotEmpty().MaximumLength(80)`. Añadir montos `≥ 0`, moneda ISO(3), fechas, coherencia paciente/beneficiario.
6. **Default de moneda por país** desde `CompanyPreference.CurrencyCode` (D-05).
7. **Permiso dedicado + 403 + autoservicio** (D-08/D-09): `ViewMedicalClaims`/`ManageMedicalClaims` (espejo de `ViewCompensation`/`ManageSubstitutions`) **más** un **gate de escritura self-service nuevo** para que el empleado **cree** sus propios reclamos. *(Hallazgo crítico: hoy NO existe ningún gate de escritura self-service; el de compensación es solo de lectura. Hay que construirlo — §3.6, R-T1.)*
8. **Adjuntos** (D-11/RF-012): **reutilizar** el subsistema de archivos (`StoredFile`/`IFileStorageProvider`/`FilePurpose` + flujo `upload-session`→`complete` + SAS + limpieza) y **espejar** `PersonnelFileDocument` en un nuevo `MedicalClaimDocument` con `FilePurpose.MedicalClaimDocument`. **No** se construye almacenamiento nuevo.
9. **Módulo de reglas puro** `MedicalClaims.Rules.cs` (G-10): errores dedicados + derivación de tiempo de respuesta + aviso de reembolso.
10. **Localizar** ~5 errores nuevos (EN/es/es-SV) y mantener la auditoría de **cambios** (D-12).

Todo sigue patrones ya existentes (catálogos country-scoped `substitution-types`, permisos `ViewCompensation`/`ManageSubstitutions`, gate self-service de compensación, recipe de documentos `PersonnelFileDocument`, recursos `BackendMessages*`). El **único** componente sin patrón directo es el **gate de escritura self-service** (§3.6).

---

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Agregado | `PersonnelFileMedicalClaim` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:957`): ctor priv. (`:1000`), `Create(:1105)`, `Update(:1068)`, `SetActive(:1099)`. Props: `InsurancePublicId?`, `AccountNumber?`, `ClaimTypeCode`, `Diagnosis?`, `ClaimAmount?`, `CurrencyCode?`, `PaidAmount?`, `ResponseTimeDays?`, `Notes?`, `ClaimDateUtc`, `Source*`, `IsActive`, `ConcurrencyToken`. Normaliza vía `PersonnelFileNormalization.*`. | Añadir 7 campos (paciente/snapshots/resolución/estado); ampliar `Create`/`Update`. |
| 2 | CQRS | `Application/Features/PersonnelFiles/Compensation/MedicalClaims.cs`: `MedicalClaimInput(:42)`, response `PersonnelFileMedicalClaimResponse(:20)` (devuelve `InsuranceId` Guid, **no** nombre), `MedicalClaimInputValidator(:94)` = **solo** `ClaimTypeCode NotEmpty/Max80`, patch state(`:168`)+applier. | Enriquecer input/response; endurecer validador; nuevos segmentos de patch. |
| 3 | Handlers | `MedicalClaims.Handlers.cs`: WRITE `LoadForManageAsync` (`:34,105,190,295`), READ `LoadCompletedEmployeeForReadAsync` (`:362,390`); `IsCompletedEmployee` (`:46,117,202,307`); concurrencia (`:128,213,318`); audit `LogUpdateAsync(…, response, …)` (`:77,162,267`) y `null` en delete (`:335`). **NO** valida seguro, beneficiario, catálogo, montos ni fechas. | Insertar validaciones + reglas + default moneda; **swap de gates** (read/add/manage). |
| 4 | Repositorio | `IPersonnelFileEmployeeRepository`: `AddMedicalClaimAsync(:526)`, `UpdateMedicalClaimAsync(:532)`, `PatchMedicalClaimAsync(:550)`, `DeleteMedicalClaimAsync(:570)`, `GetMedicalClaimsAsync(:575)`, `GetMedicalClaimAsync(:579)`. Impl `PersonnelFileEmployeeRepository.cs` (Add `:1175`, Update `:1190`, Patch `:1215`, Get `:1259/1269`), `Map(:1902)`. | Ampliar firmas Update/Patch (+7 params) y `Map` (+7 campos). |
| 5 | EF config | `PersonnelFileMedicalClaimConfiguration` (`Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs:403-441`): tabla `personnel_file_medical_claims`, `claim_type_code(80)`, `diagnosis(1000)`, `currency_code(40)`, montos `numeric(18,2)`, `notes(2000)`; FK solo a `personnel_files` (Cascade); `insurance_public_id` **sin FK**; índices `uq…public_id` + `(tenant,file,date,type)`. **Sin** check constraints. | +7 columnas; `insurance_public_id` NOT NULL; (opc.) check `resolution ≥ claim`. |
| 6 | API | Contratos `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs`: `AddMedicalClaimRequest(:436)`, `UpdateMedicalClaimRequest(:451)`, `PatchMedicalClaimRequest(:466)`. Controller `PersonnelFileCompensationController.cs`: clase `[AuthorizationPolicySet(Read, Manage)](:27)`; endpoints `:933/952/974/1017/1060/1092` **sin** override de método. | Ampliar DTOs (+7); políticas a nivel de método; sub-recurso de adjuntos. |
| 7 | Permisos (patrón) | `ViewCompensation`: const `PersonnelFileCommon.cs:98` + policy `PersonnelFilePolicies.cs:23` + seed `ProvisioningConstants.cs:71` + `Program.cs:447` (authn-only superset) + `IPersonnelFileAuthorizationService.cs:26` + impl `PersonnelFileAuthorizationService.cs:27`. `ManageSubstitutions`: const `:106`, policy `:32`, seed `:72`, `Program.cs:458` (**con** `RequireAssertion`), iface `:35`, impl `:51`. | Añadir `ViewMedicalClaims` (authn-only) + `ManageMedicalClaims` (con assertion). |
| 8 | Self-service (read) | `PersonnelFileEmployeeHandlerBases.cs`: `LoadCompletedEmployeeForCompensationReadAsync(:272)` con rama self-service inline (`:296-306`: `LinkedUserPublicId == currentUser`). WRITE `LoadForManageAsync(:90)` = solo `EnsureCanManageAsync` (Admin). **NO** existe ningún gate de escritura self-service ni método `EnsureIsOwner*`. | Reusar para read; **construir** gate de escritura self-service (Add). |
| 9 | Moneda default | `CompanyPreference.CurrencyCode(:22, default "USD")`; `ICompanyPreferenceRepository.GetByTenantIdAsync(:9)`; impl `CompanyPreferenceRepository.cs:12`. | Inyectar repo; default cuando moneda omitida y hay monto. |
| 10 | Seguro/beneficiario | `GetInsuranceAsync(fileId, insuranceId)(:466)` y `GetInsuranceBeneficiaryAsync(fileId, insuranceId, beneficiaryId)(:483)` **ya filtran por expediente/póliza** → null = no existe/no pertenece. `InsuranceResponse` expone `InsuranceCode`(texto), `PolicyNumber`. `BeneficiaryResponse` expone `BeneficiaryPublicId`, `FullName`, `KinshipCode`. | Una llamada valida pertenencia y devuelve datos para snapshot. |
| 11 | Subsistema de archivos | `StoredFile`(`Domain/Files/StoredFile.cs`) + `IFileStorageProvider`(`Abstractions/Files/`) + `FilePurpose`(`Domain/Files/FileEnums.cs:12`: ProfileImage/PersonnelDocument/ReportExport/CompanyLogo/Attachment) + `IFilePurposeRuleProvider` (reglas por purpose desde `FileStorageOptions.Purposes[...]`). Flujo `CreateUploadSessionCommand`→`CompleteFileUploadCommand` (`Features/Files/`), `FilesController` (`/upload-session`, `/{id}/complete`, `/{id}/read-url`). Limpieza `PendingFileCleanupBackgroundService` (borra `PendingUpload` vencidos). | Añadir `FilePurpose.MedicalClaimDocument` + config en appsettings; reusar todo el flujo. |
| 12 | Documento (recipe) | `PersonnelFileDocument`(`Domain/PersonnelFiles/PersonnelFile.cs:2392`): `FilePublicId`(ref. suelta, **sin FK**), `FileName`, `ContentType`, `SizeBytes`, `DocumentTypeCatalogItemId`(FK→`DocumentTypeCatalogItem`, Restrict), `Observations`, `IsActive`, `ConcurrencyToken`; `Create(:2455)`, `ReplaceFileReference(:2466)`, `UpdateMetadata(:2484)`, `Inactivate(:2499)`. CQRS `Features/PersonnelFiles/Documents/Documents.Handlers.cs`; controller `PersonnelFileDocumentsController.cs` (incl. `…/{id}/read-url` **autorizado por expediente**, `:70-92`). Config `PersonnelFileConfiguration.cs:758`; DbSet `:201`; tipo validado con `IDocumentTypeCatalogRepository.GetActiveLookupByIdAsync`. | Espejar como `MedicalClaimDocument` + `MedicalClaimDocumentsController`; **reusar** `DocumentTypeCatalogItem`. |
| 13 | Catálogos country-scoped (patrón) | `substitution-types`: clase en `Domain/GeneralCatalogs/GeneralCatalogItems.cs`, categoría en `PersonnelReferenceCatalogs.cs` (`PersonnelCurriculumCatalogCategories`), wire key en `GeneralCatalogKeyMap.cs`, config `GeneralCatalogItemConfigurationBase<T>`, DbSet, validación `IPersonnelFileRepository.CatalogCodeIsActiveAsync(companyId, category, code)`, seed en migración. | Replicar 2× (`medical-claim-types`, `medical-claim-status`). |
| 14 | Localización | `Error(code,msg,ErrorType)` en `*Errors.cs`; recursos `BackendMessages.resx`/`.es.resx`/`.es-SV.resx`; paridad `BackendMessageLocalizationTests`. | ~5 códigos nuevos × 3 resx. |

---

## 3. Arquitectura de la solución

### 3.1 Dominio — `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs`

Añadir 7 campos a `PersonnelFileMedicalClaim` (`:957`) y ampliar `Create(:1105)`/`Update(:1068)`:

```csharp
// --- NUEVOS ---
public string ClaimantType { get; private set; } = MedicalClaimClaimantTypes.Titular; // "TITULAR" | "BENEFICIARIO" (D-02)
public Guid? BeneficiaryPublicId { get; private set; }      // requerido si BENEFICIARIO (RF-001)
public string? PatientNameSnapshot { get; private set; }    // snapshot del beneficiario (UI/historial)
public string? KinshipCodeSnapshot { get; private set; }    // snapshot del parentesco
public string? InsuranceNameSnapshot { get; private set; }  // snapshot de InsuranceCode (RF-002)
public DateTime? ResolutionDateUtc { get; private set; }    // fecha resolución/pago (D-07)
public string? ClaimStatusCode { get; private set; }        // catálogo medical-claim-status (D-10)
// InsurancePublicId pasa de Guid? a Guid (obligatorio, D-03) — ver §3.10 migración
```

- `Create`/`Update` reciben los nuevos parámetros; normalizar con `PersonnelFileNormalization.Clean*` y `NormalizeDate(resolutionDateUtc)`.
- **`ResponseTimeDays` derivado:** dejar de aceptarlo como entrada de negocio; se **calcula** en `Create`/`Update` con `MedicalClaimRules.DeriveResponseTimeDays(claimDateUtc, resolutionDateUtc)` (§3.3) y se asigna a la propiedad existente. *(Se mantiene la columna; pasa de manual a derivada.)*
- `SetActive(:1099)` no cambia. `ConcurrencyToken` se regenera igual.

Constantes de paciente (mismo estilo "code constreñido" que `BeneficiaryType` PRINCIPAL/CONTINGENTE del plan de seguros §3.1 — **sin** crear catálogo para 2 valores):

```csharp
public static class MedicalClaimClaimantTypes
{
    public const string Titular = "TITULAR";
    public const string Beneficiario = "BENEFICIARIO";
    public static readonly IReadOnlyCollection<string> All = new[] { Titular, Beneficiario };
}
```

> **Nota de diseño (claimant-types).** El negocio (RF-001) lo describió como "catálogo `TITULAR`/`BENEFICIARIO`". Por ser un conjunto **fijo de 2 valores semánticos**, se modela como **código constreñido** validado en el validador/reglas, igual que `BeneficiaryType`. Alternativa (si se quisiera administrable por país): catálogo country-scoped `claimant-types` con la recipe de §3.2. **Recomendado:** código constreñido.

#### Nueva entidad hija `MedicalClaimDocument` (adjuntos, RF-012)

Espejo **exacto** de `PersonnelFileDocument` (`PersonnelFile.cs:2392-2504`), colgando del **reclamo**:

```csharp
public sealed class MedicalClaimDocument : TenantEntity
{
    public long MedicalClaimId { get; private set; }
    public PersonnelFileMedicalClaim MedicalClaim { get; private set; } = null!;
    public long DocumentTypeCatalogItemId { get; private set; }                 // REUSA DocumentTypeCatalogItem
    public Guid FilePublicId { get; private set; }                              // ref. suelta a StoredFile (sin FK)
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int SizeBytes { get; private set; }
    public string? Observations { get; private set; }
    public bool IsActive { get; private set; }
    public Guid ConcurrencyToken { get; private set; }

    public static MedicalClaimDocument Create(Guid publicId, long documentTypeCatalogItemId, Guid filePublicId,
        string fileName, string contentType, int sizeBytes, string? observations) => new(/* … */);
    public void ReplaceFileReference(Guid filePublicId, string fileName, string contentType, int sizeBytes) { /* +token */ }
    public void UpdateMetadata(long documentTypeCatalogItemId, string? observations) { /* IsActive=true; +token */ }
    public void Inactivate() { IsActive = false; /* +token */ }
}
```

Y en `PersonnelFileMedicalClaim`: colección `_documents` + `AddDocument(MedicalClaimDocument)` + `MarkDocumentsUpdated()` (espejo de `PersonnelFile.AddDocument(:823)`).

> **Reutilización del tipo de documento.** En lugar del catálogo nuevo `medical-claim-document-types` (propuesto en el análisis), se **reutiliza** `DocumentTypeCatalogItem` (lo que ya usa `PersonnelFileDocument`), sembrando los tipos `FORMULARIO_RECLAMO`, `FACTURA`, `RECETA`, `EOB`, `INFORME_MEDICO`, `OTRO`. Evita un catálogo paralelo y mantiene una sola taxonomía documental. *(Si se prefiere aislarlos, queda como variante.)*

### 3.2 Catálogos country-scoped `medical-claim-types` (SV) y `medical-claim-status` (D-04/D-10)

Recipe estándar (espejo **idéntico** a `substitution-types`), 2 veces. Por catálogo: 5 archivos + seed.

**a) Clase de dominio** — `Domain/GeneralCatalogs/GeneralCatalogItems.cs`:
```csharp
public sealed class MedicalClaimTypeCatalogItem : GeneralCatalogItem { /* espejo de SubstitutionTypeCatalogItem */ }
public sealed class MedicalClaimStatusCatalogItem : GeneralCatalogItem { /* idem */ }
```

**b) Constantes de categoría** — `Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` → `PersonnelCurriculumCatalogCategories`:
```csharp
public const string MedicalClaimType = "CurriculumMedicalClaimType";
public const string MedicalClaimStatus = "CurriculumMedicalClaimStatus";
```

**c) Wire keys** — `Features/PersonnelFiles/Catalogs/GeneralCatalogKeyMap.cs` (dict `CatalogKeys`):
```csharp
["medical-claim-types"]  = PersonnelCurriculumCatalogCategories.MedicalClaimType,
["medical-claim-status"] = PersonnelCurriculumCatalogCategories.MedicalClaimStatus,
```

**d) EF config** — `Configurations/GeneralCatalogs/` (2 clases espejo de `SubstitutionTypeCatalogItemConfiguration`, con sus 5 nombres de tabla/índice).

**e) DbSets** — `Persistence/ApplicationDbContext.cs`:
```csharp
public DbSet<MedicalClaimTypeCatalogItem> MedicalClaimTypeCatalogItems => Set<MedicalClaimTypeCatalogItem>();
public DbSet<MedicalClaimStatusCatalogItem> MedicalClaimStatusCatalogItems => Set<MedicalClaimStatusCatalogItem>();
```

**f) Validación** — `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs` → `CatalogCodeIsActiveAsync` (switch por categoría normalizada):
```csharp
"CURRICULUMMEDICALCLAIMTYPE"   => await IsCountryScopedCatalogCodeActiveAsync<MedicalClaimTypeCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
"CURRICULUMMEDICALCLAIMSTATUS" => await IsCountryScopedCatalogCodeActiveAsync<MedicalClaimStatusCatalogItem>(country.CountryCatalogItemId, normalizedCode, ct),
```

**g) Seed** (en la migración, `insertData`):
- `medical-claim-types` (SV): `AMBULATORIO`, `HOSPITALARIO`, `EMERGENCIA`, `FARMACIA`, `LABORATORIO`, `DENTAL`, `OFTALMOLOGICO`, `MATERNIDAD`, `OTRO`.
- `medical-claim-status`: `PRESENTADO`, `EN_REVISION`, `PENDIENTE_DOCUMENTACION`, `APROBADO`, `RECHAZADO`, `PAGADO`, `PAGO_PARCIAL`, `ANULADO`.

> Los endpoints `GET /api/v1/general-catalogs/medical-claim-types?countryCode=SV` y `…/medical-claim-status` quedan disponibles **automáticamente** vía el key map (sin tocar el controlador de catálogos).

### 3.3 Módulo de reglas puro — nuevo `Features/PersonnelFiles/Compensation/MedicalClaims.Rules.cs`

Espejo de `EmploymentAssignmentRules`/`InsuranceRules` (testeable sin BD). Aloja los **errores dedicados** y los **helpers puros**. Las validaciones de **campo** (montos, fechas, longitud de moneda, claimant-type ∈ conjunto) van en el **validador** (400), no aquí.

```csharp
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal static class MedicalClaimErrors
{
    public static readonly Error InsuranceRequired = new(
        "MEDICAL_CLAIM_INSURANCE_REQUIRED",
        "A medical claim must be linked to an insurance policy.", ErrorType.UnprocessableEntity);

    public static readonly Error InsuranceNotFound = new(
        "MEDICAL_CLAIM_INSURANCE_NOT_FOUND",
        "The selected insurance does not exist for this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error BeneficiaryRequired = new(
        "MEDICAL_CLAIM_BENEFICIARY_REQUIRED",
        "A beneficiary must be selected when the claimant is a beneficiary.", ErrorType.UnprocessableEntity);

    public static readonly Error BeneficiaryNotOwned = new(
        "MEDICAL_CLAIM_BENEFICIARY_NOT_OWNED",
        "The selected beneficiary does not belong to the claim's insurance.", ErrorType.UnprocessableEntity);

    public static readonly Error TypeCodeInvalid = new(
        "MEDICAL_CLAIM_TYPE_CODE_INVALID",
        "The claim type code is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusCodeInvalid = new(
        "MEDICAL_CLAIM_STATUS_CODE_INVALID",
        "The claim status code is not valid for the active catalog.", ErrorType.UnprocessableEntity);
}

internal static class MedicalClaimRules
{
    /// <summary>(D-07) Tiempo de respuesta derivado = resolución − reclamo (en días); null si no hay resolución.</summary>
    public static int? DeriveResponseTimeDays(DateTime claimDateUtc, DateTime? resolutionDateUtc) =>
        resolutionDateUtc is { } resolution && resolution.Date >= claimDateUtc.Date
            ? (int)(resolution.Date - claimDateUtc.Date).TotalDays
            : null;

    /// <summary>(D-06) Reembolso: pagado > reclamado NO bloquea; señala aviso suave informativo.</summary>
    public static bool IsReimbursementOverpay(decimal? claimAmount, decimal? paidAmount) =>
        claimAmount is { } c && paidAmount is { } p && p > c;
}
```

> **Nota de diseño.** No hay reglas de "hermanos" (no hay solape ni suma como en sustituciones/seguros); el reclamo es un registro independiente. Por eso el módulo de reglas es **delgado**: errores de referencia/catálogo (resueltos en el handler tras consultar BD) + 2 helpers puros. `MEDICAL_CLAIM_TYPE_CODE_INVALID`/`STATUS_CODE_INVALID` se devuelven en el **handler** (necesitan BD) pero su `Error` vive aquí por cohesión y para el test de paridad.

### 3.4 Aplicación — comandos, validadores y patch (`MedicalClaims.cs`)

- **`MedicalClaimInput`**: `InsurancePublicId` de `Guid?` → `Guid`; **+** `ClaimantType` (string), `BeneficiaryPublicId` (Guid?), `ResolutionDateUtc` (DateTime?), `ClaimStatusCode` (string?). **Quitar `ResponseTimeDays` del input** (pasa a derivado) — o ignorarlo si se conserva por compatibilidad.
- **`PersonnelFileMedicalClaimResponse`**: **+** `InsuranceName` (snapshot de `InsuranceCode`), `PolicyNumber`, `ClaimantType`, `BeneficiaryPublicId`, `PatientName`, `KinshipCode`, `ResolutionDateUtc`, `ClaimStatusCode`. `ResponseTimeDays` se mantiene (ahora derivado).
- **`MedicalClaimInputValidator(:94)`** — endurecer:
```csharp
RuleFor(i => i.ClaimTypeCode).NotEmpty().MaximumLength(80);
RuleFor(i => i.InsurancePublicId).NotEmpty();                                              // D-03 → E2
RuleFor(i => i.ClaimantType).NotEmpty().Must(MedicalClaimClaimantTypes.All.Contains);      // D-02
RuleFor(i => i.BeneficiaryPublicId).NotEmpty()
    .When(i => i.ClaimantType == MedicalClaimClaimantTypes.Beneficiario);                  // RF-001 → E4
RuleFor(i => i.ClaimAmount).GreaterThanOrEqualTo(0).When(i => i.ClaimAmount.HasValue);     // RF-005
RuleFor(i => i.PaidAmount).GreaterThanOrEqualTo(0).When(i => i.PaidAmount.HasValue);       // RF-005 (pagado>reclamado NO se bloquea)
RuleFor(i => i.CurrencyCode).Length(3).When(i => !string.IsNullOrWhiteSpace(i.CurrencyCode)); // D-05 (convención de la casa)
RuleFor(i => i.ClaimDateUtc).NotEmpty().LessThanOrEqualTo(_ => DateTime.UtcNow);           // RF-009 → E10
RuleFor(i => i.ResolutionDateUtc).GreaterThanOrEqualTo(i => i.ClaimDateUtc)
    .When(i => i.ResolutionDateUtc.HasValue);                                              // RF-006 → E9
```
- **Patch state/applier** (`:168`): añadir segmentos `claimantType`, `beneficiaryId`, `resolutionDateUtc`, `claimStatusCode` (lecturas tipadas); `insurancePublicId` **no removible** (obligatorio). `responseTimeDays` deja de ser segmento editable (derivado). `Validate(state)` exige `InsurancePublicId != Guid.Empty` y coherencia claimant/beneficiary.

### 3.5 Aplicación — handlers (`MedicalClaims.Handlers.cs`)

En **Add**, **Update** y la rama de negocio de **Patch**, tras `IsCompletedEmployee` y **antes** de persistir, insertar:

```csharp
// 1) Seguro obligatorio + pertenencia + datos para snapshot (D-03 / RF-002).
//    GetInsuranceAsync ya filtra por expediente → null ⇒ no existe o no es del empleado.
var insurance = await employeeRepository.GetInsuranceAsync(personnelFile.PublicId, input.InsurancePublicId, ct);
if (insurance is null) return Fail(MedicalClaimErrors.InsuranceNotFound);
var insuranceNameSnapshot = insurance.InsuranceCode;          // "nombre" = texto guardado (no catálogo)
var policyNumber = insurance.PolicyNumber;

// 2) Paciente: si BENEFICIARIO, validar pertenencia a ESE seguro y snapshotear (RF-001).
string? patientNameSnapshot = null, kinshipSnapshot = null;
if (input.ClaimantType == MedicalClaimClaimantTypes.Beneficiario)
{
    var beneficiary = await employeeRepository.GetInsuranceBeneficiaryAsync(
        personnelFile.PublicId, input.InsurancePublicId, input.BeneficiaryPublicId!.Value, ct);
    if (beneficiary is null) return Fail(MedicalClaimErrors.BeneficiaryNotOwned);
    patientNameSnapshot = beneficiary.FullName; kinshipSnapshot = beneficiary.KinshipCode;
}

// 3) Catálogos: tipo (D-04) y estado (D-10, si viene).
if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
        personnelFile.TenantId, PersonnelCurriculumCatalogCategories.MedicalClaimType, input.ClaimTypeCode, ct))
    return Fail(MedicalClaimErrors.TypeCodeInvalid);
if (!string.IsNullOrWhiteSpace(input.ClaimStatusCode) && !await personnelFileRepository.CatalogCodeIsActiveAsync(
        personnelFile.TenantId, PersonnelCurriculumCatalogCategories.MedicalClaimStatus, input.ClaimStatusCode!, ct))
    return Fail(MedicalClaimErrors.StatusCodeInvalid);

// 4) Moneda default por país si se omitió y hay monto (D-05).
var currency = input.CurrencyCode;
if (string.IsNullOrWhiteSpace(currency) && (input.ClaimAmount.HasValue || input.PaidAmount.HasValue))
    currency = (await companyPreferenceRepository.GetByTenantIdAsync(personnelFile.TenantId, ct))?.CurrencyCode ?? "USD";
```

`ResponseTimeDays` lo deriva el dominio (`MedicalClaimRules.DeriveResponseTimeDays`) dentro de `Create`/`Update`. Inyectar `ICompanyPreferenceRepository` en los handlers de escritura. La auditoría (`LogUpdateAsync`) se mantiene; ver §3.12 para el diff antes/después.

**Swap de gates** (§3.6):
| Handler | Gate hoy | Gate nuevo |
|---|---|---|
| Get / GetById (`:362,390`) | `LoadCompletedEmployeeForReadAsync` | `LoadCompletedEmployeeForMedicalClaimReadAsync` (perm-o-self) |
| Add (`:34`) | `LoadForManageAsync` | `LoadForCreateOwnOrManageMedicalClaimAsync` (**self-service create**) |
| Update / Patch / Delete (`:105,190,295`) | `LoadForManageAsync` | `LoadForManageMedicalClaimsAsync` (solo RRHH) |

### 3.6 Permiso dedicado + 403 + autoservicio (D-08/D-09) — núcleo de privacidad

Dos permisos (espejo de `ViewCompensation` y `ManageSubstitutions`) y **tres** gates de handler.

**Permisos** (mismo patrón que la tabla de la línea base #7):

| Archivo | Cambio |
|---|---|
| `Common/PersonnelFileCommon.cs` | `public const string ViewMedicalClaims = "PersonnelFiles.ViewMedicalClaims";` y `ManageMedicalClaims = "PersonnelFiles.ManageMedicalClaims";` |
| `Common/PersonnelFilePolicies.cs` | Las 2 constantes equivalentes. |
| `Provisioning/Common/ProvisioningConstants.cs` | 2 entradas en `CompanyAdminPermissions` (módulo `PersonnelFiles`) → el owner las recibe. |
| `Api/Program.cs` | `ViewMedicalClaims` = **authn-only superset** (como `ViewCompensation:447`). `ManageMedicalClaims` = superset **+ `RequireAssertion(HasAnyPermission(ManageMedicalClaims, Admin, ManageAdministration))`** (como `ManageSubstitutions:458`). |
| `Abstractions/PersonnelFiles/IPersonnelFileAuthorizationService.cs` | `EnsureCanViewMedicalClaimsAsync` (default fail-closed) + `EnsureCanManageMedicalClaimsAsync`. |
| `Infrastructure/PersonnelFiles/PersonnelFileAuthorizationService.cs` | View → `EnsureHasAnyClaimAsync([ViewMedicalClaims, Admin, ManageAdministration], Read)`. Manage → idem `[ManageMedicalClaims, …]` con `Update`. |

**Gates de handler** — `Features/PersonnelFiles/Common/PersonnelFileEmployeeHandlerBases.cs`:

1. **`LoadCompletedEmployeeForMedicalClaimReadAsync`** — copia de `LoadCompletedEmployeeForCompensationReadAsync(:272)` cambiando la llamada a `EnsureCanViewMedicalClaimsAsync`. Conserva la **rama self-service** (`:296-306`): permiso **o** titular (`LinkedUserPublicId == currentUser`). → D-08 (403 a terceros) + D-09 (lectura self-service).

2. **`LoadForCreateOwnOrManageMedicalClaimAsync`** — **NUEVO** (no existe equivalente). Copia de `LoadForManageAsync(:90)` pero la decisión de autorización es **`EnsureCanManageMedicalClaimsAsync` O titular**:
```csharp
var canManage = (await authorizationService.EnsureCanManageMedicalClaimsAsync(tenantContext.TenantId.Value, ct)).IsSuccess;
if (!canManage)
{
    var isSelf = personnelFile.LinkedUserPublicId is { } linked
        && Guid.TryParse(currentUserService.UserId, out var caller) && linked == caller;
    if (!isSelf) return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
}
// resto idéntico a LoadForManageAsync: tenant-mismatch, completed-employee, etc.
```
Inyecta `ICurrentUserService` en el handler de **Add** (como los read-handlers de compensación).

3. **`LoadForManageMedicalClaimsAsync`** — copia de `LoadForManageAsync(:90)` llamando `EnsureCanManageMedicalClaimsAsync` (sin self-service). Para Update/Patch/Delete.

> **Por qué la política del POST debe ser authn-only.** El autoservicio (D-09) exige que un empleado **sin** `ManageMedicalClaims` pueda **crear** su reclamo. Si el endpoint POST exigiera el permiso vía `RequireAssertion`, lo bloquearía. Por eso **Add** se protege con la política **authn-only** `ViewMedicalClaims` (no la de manage), y la decisión fina (manage-o-titular) la toma el gate del handler. PUT/PATCH/DELETE sí usan `ManageMedicalClaims` (con assertion) — solo RRHH (resuelve el pendiente (a) del negocio: empleado **ver+crear**; editar/baja por RRHH).

> **Datos sensibles (D-08).** El diagnóstico nunca se enmascara: o se autoriza (permiso o titular) o **403**. Vale igual para los **adjuntos** (§3.7): su `read-url` se autoriza por el mismo gate de lectura del reclamo.

### 3.7 Adjuntos — `MedicalClaimDocument` reutilizando el subsistema de archivos (RF-012, D-11)

**No se construye almacenamiento.** Se reutiliza el flujo genérico (`FilesController`: `POST /files/upload-session` → `PATCH /files/{id}/complete`) y solo se añade el **purpose** + la **entidad espejo** + su **controller autorizado**.

**a) `FilePurpose`** — `Domain/Files/FileEnums.cs:12`: añadir `MedicalClaimDocument`.

**b) `appsettings.json`** (`Storage.Purposes`): nueva sección `MedicalClaimDocument` (copiar `PersonnelDocument`: 10 MB, `application/pdf`/`image/jpeg`/`image/png`, contenedor p. ej. `clarihr-medical-claim-documents`). `IFilePurposeRuleProvider` la resuelve por nombre de enum — **sin** código nuevo.

**c) Entidad + colección** — §3.1.

**d) EF config + DbSet** — `MedicalClaimDocumentConfiguration` espejo de `PersonnelFileDocumentConfiguration(:758)`: tabla `medical_claim_documents`, FK `medical_claim_id`→`personnel_file_medical_claims` (Cascade), FK `document_type_catalog_item_id`→catálogo (Restrict), `file_public_id` **ref. suelta indexada (sin FK)**, índices `uq…public_id` + `(tenant, medical_claim_id, is_active)`. DbSet en `ApplicationDbContext`.

**e) CQRS** — nuevo `Features/PersonnelFiles/Compensation/MedicalClaimDocuments*.cs` espejo de `Documents.Handlers.cs`: Add/Update/Patch/Delete/Get/GetById/GetReadUrl. El **Add** valida (i) que el `StoredFile` exista, esté `Active` y su `Purpose == MedicalClaimDocument`; (ii) el `DocumentTypeCatalogItem` activo (`IDocumentTypeCatalogRepository.GetActiveLookupByIdAsync`). El **read-url** delega en `IFileStorageProvider.CreateReadSessionAsync`. El **Delete** marca `Inactivate()` y el `StoredFile` como `Deleted` (para que `PendingFileCleanupBackgroundService` lo recoja).

**f) Controller** — `MedicalClaimDocumentsController` espejo de `PersonnelFileDocumentsController`, rutas bajo `…/medical-claims/{medicalClaimPublicId}/documents`. **Autorización con los gates del reclamo** (no la política genérica): lectura/`read-url` por el gate de lectura (perm-o-titular); escritura por `ManageMedicalClaims` (o create-own si se decide permitir adjuntos al crear). El `read-url` debe ser **autorizado por el reclamo** (igual que `PersonnelFileDocumentsController.cs:70-92`, no el `/files/{id}/read-url` owner-only).

**g) Repositorio** — métodos Add/Get/Delete de `MedicalClaimDocument` (espejo de los de documentos del expediente); reusar `IFileRepository.GetByPublicIdAsync` para validar el `StoredFile`.

### 3.8 API — contratos y controlador

`Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs` (`:436-482`): ampliar `Add/Update/PatchMedicalClaimRequest` con `ClaimantType`, `BeneficiaryPublicId`, `ResolutionDateUtc`, `ClaimStatusCode`; `InsurancePublicId` pasa a `Guid` (no nullable). Quitar `ResponseTimeDays` de entrada (o ignorarlo). Añadir DTOs de `MedicalClaimDocument`.

`PersonnelFileCompensationController.cs` (`:933-1107`): mapear los nuevos campos a `MedicalClaimInput`. **Políticas a nivel de método** (override del `[AuthorizationPolicySet(Read, Manage)]` de clase, que sigue rigiendo el resto del controller — patrón del plan de seguros R-T6):
- GET (`:933,952`) y **POST** (`:974`): `[Authorize(Policy = PersonnelFilePolicies.ViewMedicalClaims)]` (authn-only; el gate decide perm-o-titular).
- PUT/PATCH/DELETE (`:1017,1060,1092`): `[Authorize(Policy = PersonnelFilePolicies.ManageMedicalClaims)]`.

### 3.9 Infraestructura — repositorio

`IPersonnelFileEmployeeRepository` + `PersonnelFileEmployeeRepository.cs`:
- `UpdateMedicalClaimAsync(:532)` / `PatchMedicalClaimAsync(:550)`: **+** parámetros `claimantType`, `beneficiaryPublicId`, snapshots, `resolutionDateUtc`, `claimStatusCode`; `insurancePublicId` pasa a `Guid`. Llamada interna `item.Update(...)` ampliada.
- `AddMedicalClaimAsync(:1175)`: sin cambio de firma (recibe la entidad ya construida).
- `Map(:1902)`: proyectar los 7 campos nuevos **+** `InsuranceName`(snapshot)/`PolicyNumber` en el response.
- **Sin métodos nuevos** para validación: se **reusan** `GetInsuranceAsync(:466)` y `GetInsuranceBeneficiaryAsync(:483)` (ya filtran por expediente/póliza).

### 3.10 Infraestructura — EF config y migración

**Config** `PersonnelFileMedicalClaimConfiguration(:403)`:
```csharp
builder.Property(i => i.ClaimantType).HasColumnName("claimant_type").HasMaxLength(40).IsRequired();
builder.Property(i => i.BeneficiaryPublicId).HasColumnName("beneficiary_public_id");
builder.Property(i => i.PatientNameSnapshot).HasColumnName("patient_name_snapshot").HasMaxLength(260);
builder.Property(i => i.KinshipCodeSnapshot).HasColumnName("kinship_code_snapshot").HasMaxLength(80);
builder.Property(i => i.InsuranceNameSnapshot).HasColumnName("insurance_name_snapshot").HasMaxLength(120);
builder.Property(i => i.ResolutionDateUtc).HasColumnName("resolution_date_utc");
builder.Property(i => i.ClaimStatusCode).HasColumnName("claim_status_code").HasMaxLength(80);
builder.Property(i => i.InsurancePublicId).HasColumnName("insurance_public_id").IsRequired();   // D-03
// (opcional) check: "resolution_date_utc is null or resolution_date_utc >= claim_date_utc"
```
Nuevas configs: 2 catálogos (§3.2.d) + `MedicalClaimDocumentConfiguration` (§3.7.d). Todas se autodescubren por `ApplyConfigurationsFromAssembly`.

**Migración** (una sola):
```bash
DOTNET_ROLL_FORWARD=Major dotnet ef migrations add HardenMedicalClaimsAndAttachments \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```
Contendrá: `CreateTable` de los 2 catálogos + `medical_claim_documents` (+ índices/FK) · `insertData` seed SV (tipos/estados + nuevos `DocumentTypeCatalogItem`) · `AddColumn` ×7 en `personnel_file_medical_claims` (con `claimant_type` default `'TITULAR'`) · `AlterColumn insurance_public_id` NOT NULL (ver §4) · (opc.) check constraint. Validar con `dotnet ef migrations has-pending-model-changes`.

> **Gotcha conocido:** `dotnet ef` requiere `DOTNET_ROLL_FORWARD=Major` en este entorno (memoria equipo-acceso).

### 3.11 Localización (≈5 códigos × 3 resx)

Solo los errores **dedicados** (los de campo son `common.validation`, sin resx). Añadir a `BackendMessages.resx` (EN), `.es.resx`, `.es-SV.resx`:

| Code | EN | ES |
|---|---|---|
| `MEDICAL_CLAIM_INSURANCE_NOT_FOUND` | The selected insurance does not exist for this employee. | El seguro seleccionado no existe para este empleado. |
| `MEDICAL_CLAIM_BENEFICIARY_NOT_OWNED` | The selected beneficiary does not belong to the claim's insurance. | El beneficiario seleccionado no pertenece al seguro del reclamo. |
| `MEDICAL_CLAIM_TYPE_CODE_INVALID` | The claim type code is not valid for the active catalog. | El tipo de reclamo no es válido en el catálogo activo. |
| `MEDICAL_CLAIM_STATUS_CODE_INVALID` | The claim status code is not valid for the active catalog. | El estado del reclamo no es válido en el catálogo activo. |
| `MEDICAL_CLAIM_INSURANCE_REQUIRED` | A medical claim must be linked to an insurance policy. | Un reclamo médico debe estar asociado a una póliza de seguro. |

*(`INSURANCE_REQUIRED`/`BENEFICIARY_REQUIRED` también pueden quedar como `common.validation` 400 desde el validador — ver §5. Se incluyen como Error dedicado por si se prefiere 422 uniforme.)*

### 3.12 Auditoría con diff (D-12)

La auditoría ya se invoca por operación (`LogUpdateAsync`, `:77,162,267,335`), hoy pasando **solo** el `response` (after). Para el **diff antes/después**, pasar también el `existing` en Update/Patch/Delete (misma mejora pendiente que sustituciones/seguros). **No** se auditan lecturas (D-12). El diagnóstico, por ser dato de salud, se audita como cambio (no como acceso).

---

## 4. Migración de datos (cambio breaking — D-03)

Hacer `insurance_public_id` NOT NULL, catalogar `claim_type_code` y poblar `claimant_type` **rompen** datos existentes.

- **Sin datos en QA/prod (recomendado verificar — S1):** la migración hace `AddColumn` con default `claimant_type='TITULAR'`, `CreateTable` de catálogos, y `AlterColumn insurance_public_id NOT NULL` directo. Camino simple.
- **Con datos:** por pasos — (1) `AddColumn` los 7 (claimant default `'TITULAR'`, resto nullable); (2) backfill: asociar cada reclamo a un seguro válido del empleado (o marcar para saneamiento los que no tengan), normalizar `claim_type_code` al catálogo, derivar `response_time_days` desde fechas si las hubiera; (3) `AlterColumn insurance_public_id NOT NULL` + (opc.) check. Documentar el script.

> **Acción previa:** confirmar existencia de datos en `personnel_file_medical_claims` (QA/prod). Determina el camino. Definir además el comportamiento al **eliminar un seguro** con reclamos (bloquear vs. conservar `insurance_name_snapshot`) — el snapshot ya mitiga el orfanato.

---

## 5. Mapa de errores (resumen)

| Disparador | Código | ErrorType → HTTP | Capa |
|---|---|---|---|
| `insurancePublicId` / `claimantType` vacíos; beneficiario faltante si BENEFICIARIO | `common.validation` | Validation → **400** | Validador |
| Monto negativo / moneda longitud ≠ 3 / fecha futura o ausente | `common.validation` | Validation → **400** | Validador |
| `resolutionDateUtc < claimDateUtc` | `common.validation` (campo `resolutionDateUtc`) | Validation → **400** | Validador |
| Seguro no existe / no es del empleado | `MEDICAL_CLAIM_INSURANCE_NOT_FOUND` | UnprocessableEntity → **422** | Handler |
| Beneficiario no pertenece al seguro | `MEDICAL_CLAIM_BENEFICIARY_NOT_OWNED` | UnprocessableEntity → **422** | Handler |
| Tipo fuera de catálogo | `MEDICAL_CLAIM_TYPE_CODE_INVALID` | UnprocessableEntity → **422** | Handler |
| Estado fuera de catálogo | `MEDICAL_CLAIM_STATUS_CODE_INVALID` | UnprocessableEntity → **422** | Handler |
| `paidAmount > claimAmount` | (ninguno) | **Aceptado** (reembolso, D-06); aviso suave opcional | Reglas |
| Adjunto con purpose/tipo/tamaño no permitido | `common.validation` / regla de `FilePurpose` | 400/413 | Files |
| Sin `ViewMedicalClaims` y no titular (lectura/creación) | (gate) | Forbidden → **403** | Handler |
| Sin `ManageMedicalClaims` (edición/baja) | (política/gate) | Forbidden → **403** | API/Policy |
| `If-Match` no coincide (existente) | `CONCURRENCY_CONFLICT` | Conflict → **409** | Handler |
| Expediente no completado (existente) | `STATE_RULE_VIOLATION` | → **409/422** | Handler |

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- `MedicalClaimRulesTests` (nuevo): `DeriveResponseTimeDays` (sin resolución → null; resolución=reclamo → 0; bordes; resolución<reclamo → null), `IsReimbursementOverpay`.
- `MedicalClaimInputValidatorTests` (nuevo): seguro requerido, claimant ∈ conjunto, beneficiario requerido si BENEFICIARIO, montos ≥0, moneda len 3, fecha no futura, resolución≥reclamo.
- `PersonnelFileMedicalClaimPatchTests` (existente/ampliar): nuevos segmentos; `insurancePublicId` no removible; `responseTimeDays` ya no editable.
- `BackendMessageLocalizationTests` (existente): verde con los 5 códigos nuevos en EN+ES.
- (Opcional) tests de handler con mocks para 422/403/409 y para el **gate self-service** (titular crea/lee lo suyo; tercero sin permiso → 403).

**Integración (`tests/CLARIHR.Api.IntegrationTests/`):**
- `IntegrationTestSeeder`: sembrar `medical-claim-types`/`medical-claim-status` (país de prueba), tipos de documento, permisos `ViewMedicalClaims`/`ManageMedicalClaims`, un empleado con seguro + beneficiarios, y un **usuario empleado vinculado** (`LinkedUserPublicId`) para el self-service.
- Casos: alta feliz (titular y beneficiario); seguro ausente (400) / inexistente (422); beneficiario ajeno (422); tipo/estado inválido (422); montos/fechas inválidos (400); `paidAmount>claimAmount` **aceptado**; tiempo de respuesta **derivado**; **self-service**: titular crea+lee lo suyo (201/200), tercero sin permiso → **403**, edición por titular → **403**; adjunto: upload-session→complete→link→read-url autorizado; tercero descarga adjunto → **403**; `If-Match` → 409.

**Guardrail:** `GeneralCatalogKeyMapGuardrailsTests` — biyección `medical-claim-types`/`medical-claim-status` ↔ categorías.

---

## 7. Orden de implementación (PRs sugeridos)

1. **PR-1 — Catálogos** (§3.2): 2 clases + categorías + key map + EF config + DbSets + switches de validación + seed SV + migración parcial (`CreateTable`+`insertData`). Aislado, verde con guardrail.
2. **PR-2 — Permisos + gates** (§3.6): constantes + políticas + semilla + servicio (iface/impl) + 3 gates (incl. el **self-service-create nuevo**) + swap en los 6 handlers + atributos de método. Aislado.
3. **PR-3 — Endurecimiento del agregado + EF + migración** (§3.1, §3.10): 7 campos + `insurance_public_id` NOT NULL + `Create/Update`; config; migración (alter/add) + estrategia de datos (§4).
4. **PR-4 — Aplicación** (§3.3, §3.4, §3.5, §3.9): reglas, input/response, validador, patch, validaciones de handler (seguro/beneficiario/catálogo/moneda) + derivación + firmas de repo/`Map`.
5. **PR-5 — Adjuntos** (§3.7): `FilePurpose` + appsettings + `MedicalClaimDocument` + CQRS + controller + repo + EF/migración.
6. **PR-6 — API + localización + auditoría + tests** (§3.8, §3.11, §3.12, §6): contratos, 5 errores × 3 resx, diff antes/después, batería completa.

> PR-3/4 pueden fusionarse. PR-1, PR-2 y PR-5 conviene aislarlos (PR-5 es el de mayor superficie nueva).

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Gate de escritura self-service (D-09): sin patrón previo.** Hoy **ningún** write es self-service; el de compensación es solo lectura. Hay que **construir** `LoadForCreateOwnOrManageMedicalClaimAsync` (§3.6) y asegurar que la política del POST sea **authn-only** (no exigir `ManageMedicalClaims`), o el empleado quedaría 403. **Único componente sin patrón directo.** *Mitiga:* reusar la verificación de titularidad ya probada del gate de compensación (`:296-306`) + tests dedicados.
- **R-T2 — Seguro obligatorio (D-03), breaking.** Reclamos sin seguro rompen la validación → backfill/saneamiento previo (§4). Definir el borrado de seguro con reclamos (snapshot ya mitiga).
- **R-T3 — Privacidad del diagnóstico y adjuntos (D-08).** El `read-url` del adjunto **debe** autorizarse por el gate del reclamo (no el `/files/{id}/read-url` owner-only). Verificar que el contenedor del purpose y los SAS hereden el control de acceso del reclamo.
- **R-T4 — "Nombre del seguro" ≠ catálogo.** `InsuranceCode` es **texto**, no un código de catálogo resoluble. RF-002 se cumple **proyectando/snapshoteando** `InsuranceCode`+`PolicyNumber` desde `GetInsuranceAsync`, no resolviendo un nombre de catálogo.
- **R-T5 — `ResponseTimeDays` derivado vs. histórico.** Al pasar de manual a derivado, los valores manuales previos se recomputan en el backfill; documentar que deja de ser editable (E del negocio: no editable si hay fechas).
- **R-T6 — Política de controller compartido.** El `[AuthorizationPolicySet(Read, Manage)]` de clase cubre otros sub-recursos; aplicar `ViewMedicalClaims`/`ManageMedicalClaims` **solo a nivel de método** en los endpoints de reclamos para no afectar bank-accounts/benefits/seguros.
- **R-T7 — `claimant-types` como código vs. catálogo.** Se eligió código constreñido (2 valores). Si el negocio lo quiere administrable por país, es la recipe de §3.2 (cambio acotado).

---

## 9. Checklist de implementación

- [ ] **Dominio:** 7 campos nuevos + `MedicalClaimClaimantTypes`; `Create/Update` ampliados; `ResponseTimeDays` derivado; entidad `MedicalClaimDocument` + colección.
- [ ] **Catálogos:** 2 clases + categorías + key map + EF config + 2 DbSets + switches `CatalogCodeIsActiveAsync` + seed SV (tipos/estados).
- [ ] **Reglas:** `MedicalClaims.Rules.cs` (`MedicalClaimErrors` + `DeriveResponseTimeDays` + `IsReimbursementOverpay`).
- [ ] **Aplicación:** `MedicalClaimInput`/response ampliados + validador endurecido + patch state/applier (+4 segmentos, `insurancePublicId` no removible).
- [ ] **Handlers:** seguro+beneficiario (reuso `GetInsurance*`) + catálogos + moneda default; derivación; swap a los 3 gates; inyectar `ICompanyPreferenceRepository`/`ICurrentUserService`.
- [ ] **Permisos/gates:** `ViewMedicalClaims` (authn-only) + `ManageMedicalClaims` (assertion); servicio iface/impl; **gate self-service-create nuevo** + read-gate perm-o-self + manage-gate.
- [ ] **Adjuntos:** `FilePurpose.MedicalClaimDocument` + appsettings; entidad/CQRS/controller/repo/EF; read-url autorizado por reclamo; delete marca `StoredFile` Deleted.
- [ ] **Infra:** firmas de repo (Update/Patch +7) + `Map` (+InsuranceName/PolicyNumber); EF config (7 columnas + `insurance_public_id` NOT NULL + 2 catálogos + documentos); 1 migración + estrategia de datos.
- [ ] **API:** contratos (+4 campos, `InsurancePublicId` Guid) + mapeo; políticas a nivel de método (POST=View authn-only; PUT/PATCH/DELETE=Manage).
- [ ] **Localización:** 5 códigos en EN + es + es-SV.
- [ ] **Auditoría:** pasar `existing` para diff antes/después (Update/Patch/Delete).
- [ ] **Tests:** rules + validator + patch unit + paridad + guardrail + integración (felices/errores/403/409 + self-service + adjuntos) + seeder.
- [ ] **Verificación:** `dotnet build`, `dotnet test`, `DOTNET_ROLL_FORWARD=Major dotnet ef migrations has-pending-model-changes` (sin pendientes).

---

> **Trazabilidad.** Este plan implementa la Fase 1 del análisis de negocio (D-01…D-13, RF-001…RF-012). RF-010 (consulta enriquecida) y RF-011 (integración aseguradora, D-13) quedan **diferidos**. Todo cambio sigue patrones verificados en el código: catálogos country-scoped (`substitution-types`), permisos espejo de `ViewCompensation`/`ManageSubstitutions`, gate self-service de compensación, recipe documental de `PersonnelFileDocument` + subsistema de archivos genérico, recursos `BackendMessages*`. El **único** componente sin patrón directo es el **gate de escritura self-service** (§3.6, R-T1). El módulo permanece **solo de registro** (D-01): no aprueba, no paga, no tramita.
