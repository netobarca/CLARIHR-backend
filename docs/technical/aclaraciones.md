# Respuesta Backend — Preguntas de Integración Frontend `/v1` (módulos 24-25 jun)

| | |
| --- | --- |
| **De** | Equipo Backend (.NET API) |
| **Para** | Equipo Frontend |
| **Fecha** | 2026-06-25 |
| **Estado merge** | Las **4 ramas ya están mergeadas a `master`** y desplegadas. Detalle de commits abajo. |
| **Método** | Todas las respuestas verificadas **contra el código fuente en `master`** (no contra el doc ni de memoria). Se citan archivos/líneas. |
| **Actualización 2026-06-25** | Deploy de seguimiento: (a) backfill de la escala de competencias para empresas existentes → `isConfigured=true` para todas (§3.3); (b) PATCH de matriz documenta `/expectedValue` (§3.4). Resto sin cambios. |

> ⚠️ **Dos hallazgos transversales que cambian su plan — léanlos antes de tocar nada:**
> 1. **El prefijo NO es `/v1/…`. Es `api/v1/…`** (con `/api`). Ver §0.2.
> 2. **El error code viaja en `extensions.code`** (no hay propiedad `code` de primer nivel "real"; es una extensión de ProblemDetails). Ver §0.3.
> 3. **`428` NO existe en ningún módulo.** Falta de `If-Match` → **`400`** en todos (incluido curriculares). La guía de curriculares que menciona 428 está **mal**; se corregirá. Ver §4.5.

---

## 0. General

### 0.1 — ¿Las 4 ramas están mergeadas y desplegadas? ✅ SÍ (todas en `master`)

Todas mergeadas a `master`. Commits exactos:

| Módulo | Commit | Asunto |
| --- | --- | --- |
| Reclamos seguro médico | `d958672` | `feat(medical-claims): harden medical insurance claims module (Fase 1)` |
| Competencias del puesto | `06e88e3` (PR #50) + `9e1ad5d` | `restructure competency rating scale and position competency result` |
| Competencias curriculares | `f07cbbc` | `feat(personnel-files): endurecer competencias curriculares…` |
| Transacciones fuera de nómina | `f07cbbc` | `…agregar transacciones fuera de nómina (Fase 1)` |
| Fix follow-up competencias | `6da3936` | `CompetencyRatingScaleResponse implementa ISupportsAllowedActions` |

**Por qué el MCP muestra shapes viejos:** el spec del MCP está cacheado/rezagado, NO el servidor. Acciones:
1. Regenerar el spec del FE (`clarihr_refresh_spec`).
2. Si tras regenerar **siguen** los shapes viejos, entonces el build desplegado es anterior a estos commits → pedir redeploy de `master` y verificar el SHA desplegado (commit `f07cbbc` o posterior). Confirmen contra `GET /api/v1/...` reales, no solo Swagger.

### 0.2 — Prefijo `/v1/…` sin `/api` → ❌ INCORRECTO. Es `api/v1/…`

El backend sirve **`api/v1/...`**. Versionado por segmento de URL (`UrlSegmentApiVersionReader`, `Program.cs:82-97`), `v` se sustituye por `v1`. Ejemplos reales:
- `api/v1/personnel-files/{publicId}/medical-claims`
- `api/v1/personnel-files/{publicId}/off-payroll-transactions`
- `api/v1/companies/{companyId}/competency-rating-scale`
- `api/v1/general-catalogs/{catalogKey}`

**Acción FE:** su `API_URL` / base del BFF debe terminar de modo que el path final sea `…/api/v1/…`. Si el BFF hoy compone `{API_URL}/v1/...` les va a faltar el `/api`. Todas las rutas de este documento se listan ya con el prefijo `api/v1` correcto.

### 0.3 — RFC 7807 con `code` estable → ✅ SÍ, en `extensions.code`

`ProblemDetailsFactory.cs:42-59` setea `problemDetails.Extensions["code"] = error.Code`. Como `Extensions` se serializa "aplanado", en el JSON de respuesta el `code` aparece como **propiedad de primer nivel `"code"`** junto a `title`, `detail`, `status`, `type`, `traceId`. Resumen del body:

```jsonc
{
  "type": "https://httpstatuses.com/422",   // SIEMPRE es httpstatuses.com/{status}, NO el code
  "title": "<mensaje localizado>",
  "detail": "<mensaje localizado>",
  "status": 422,
  "code": "CURRICULAR_COMPETENCY_METRIC_REQUIRED",  // ← el code estable (extensions.code)
  "traceId": "<trace>",
  "errors": { "body": ["…"] }               // solo en errores de validación de campo (400)
}
```

- **Lean `code` de la propiedad `code` (= `extensions.code`). NO usen `type` para el code.**
- **Mapa de status** (`ProblemDetailsFactory.cs:64-79`): Validación de request/binding → **400**; reglas de negocio "UnprocessableEntity" → **422**; Conflictos (concurrencia, duplicados) → **409**; Forbidden → 403; NotFound → 404.

### 0.x (transversal) — Concurrencia: `If-Match` header (NO token en body)

Aplica a **los 4 módulos** (PUT/PATCH/DELETE). Verificado en `IfMatchModelBinder.cs`, `ResultExtensions.cs`, controladores.

- El token se envía en el **header `If-Match`** (parámetro `[FromIfMatch] Guid concurrencyToken`). **No** se lee del body.
- Cada item trae su `concurrencyToken` en el body (GET/list) — ese valor es el que echan al `If-Match`.
- Respuestas de create/update devuelven el **nuevo token en el header `ETag`**.
- **Falta de `If-Match` → `400`** (validación), **token viejo (stale) → `409`** (ConcurrencyConflict). **Nunca 412 ni 428.**
- `DELETE` devuelve `{ parentConcurrencyToken }` (en body y en header `ETag`) — refresca el token del personnel-file padre para seguir mutando sin round-trip extra.
- **PATCH** = RFC 6902 JSON Patch, `Content-Type: application/json-patch+json`.
- **Enums serializan como string** (`JsonStringEnumConverter` global, `Program.cs:74-80`).

---

## 1. Reclamos de Seguro Médico (`medical-claims`)

### 1.1 — Nuevo request desplegado ✅. Shape final (¡trae MÁS campos de los que listaron!)

`AddMedicalClaimRequest` == `UpdateMedicalClaimRequest` (`PersonnelFileRequests.cs:436-470`):

```csharp
public sealed record AddMedicalClaimRequest(
    Guid InsurancePublicId,        // REQUERIDO, Guid NO-nullable (no Guid?)
    string? AccountNumber,
    string ClaimantType,           // REQUERIDO  "TITULAR" | "BENEFICIARIO"
    Guid? BeneficiaryPublicId,     // requerido SOLO si ClaimantType=BENEFICIARIO
    string ClaimTypeCode,          // REQUERIDO  ← catálogo medical-claim-types (lo habían omitido)
    string? Diagnosis,
    decimal? ClaimAmount,
    string? CurrencyCode,          // ISO-4217 (3) si se envía
    decimal? PaidAmount,
    string? Notes,
    DateTime ClaimDateUtc,         // REQUERIDO  (lo habían omitido)
    DateTime? ResolutionDateUtc,   // opcional
    string? ClaimStatusCode,       // opcional   ← catálogo medical-claim-status
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);
```

Checklist puntual:
- `insurancePublicId` → ✅ **obligatorio**, pero es `Guid` NO-nullable (no `Guid?`). Validado `NotEmpty`.
- `claimantType` → ✅ string, valores exactos `"TITULAR"` / `"BENEFICIARIO"`, serializa como string. (Es un **code string**, no un C# enum; validación case-insensitive, default `TITULAR`.)
- `beneficiaryPublicId` → ✅ requerido **solo** si `claimantType=BENEFICIARIO` (validador `When(...)`, `MedicalClaims.cs:112-121`). Además el backend valida que el beneficiario pertenezca a ese seguro (`422 BeneficiaryNotOwned`).
- `resolutionDateUtc`, `claimStatusCode` → ✅ opcionales.
- `responseTimeDays` → ✅ **ELIMINADO del request**. Es derivado (`resolution - claim` en días) y viaja **solo en la respuesta** (read-only).

> ⚠️ **No olviden `claimTypeCode` (requerido, catálogo) ni `claimDateUtc` (requerido)** — no estaban en su lista.

**Respuesta** `PersonnelFileMedicalClaimResponse` (`MedicalClaims.cs:20`): incluye todo lo anterior + derivados read-only `responseTimeDays`, `insuranceName`, `patientName`, `kinshipCode`, `isActive`, `concurrencyToken`. PATCH además permite togglear `isActive` (único camino para eso).

### 1.2 — Catálogos ✅ (ojo: status en singular)

- `medical-claim-types` → ✅ country-scoped.
- `medical-claim-status` → ✅ **(singular, NO "statuses")** country-scoped.
- Ruta: `GET /api/v1/general-catalogs/medical-claim-types?countryCode=SV` y `…/medical-claim-status?countryCode=SV`.
- (`GeneralCatalogKeyMap.cs:29-30`. Tablas con `country_code`, índice único por país.)

### 1.3 — Adjuntos ✅ (existen, no hay PUT/PATCH de doc)

Rutas (todas bajo `api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}`):
- `GET    …/documents`
- `POST   …/documents`
- `GET    …/documents/{documentPublicId}`
- `GET    …/documents/{documentPublicId}/read-url`
- `DELETE …/documents/{documentPublicId}`  (soft-delete del doc)
- ❌ **No hay** `PUT`/`PATCH` de documento.

- `purpose` para `POST /api/v1/files/upload-session` → ✅ **`"MedicalClaimDocument"`** (`FileEnums.cs:19`, serializa string). Flujo: upload-session → `PATCH /files/{id}/complete` → `POST …/documents { filePublicId, documentTypeCatalogItemPublicId?, observations? }`.
- `documentTypeCatalogItemPublicId` sale del catálogo **`file-document-types`**: `GET /api/v1/general-catalogs/file-document-types`.
- Tipos/size permitidos: se validan en la capa de Files (storedFile debe estar `Active`, mismo tenant, `purpose=MedicalClaimDocument`). Los límites concretos (PDF/JPG/PNG/10 MB) están en config de Files (`appsettings`); confírmenlos ahí — no están hardcodeados en este módulo.

### 1.4 — Concurrencia / DELETE

- `PUT/PATCH/DELETE` usan **`If-Match` header** (no token en body). Stale → 409, falta → 400.
- **`DELETE` de reclamo es FÍSICO (hard delete)** (`PersonnelFileEmployeeRepository.cs:1259`). Devuelve `{ parentConcurrencyToken }`.
- `DELETE` de **documento** es **lógico** (soft) y también devuelve `{ parentConcurrencyToken }`.

### 1.5 — RBAC

- Códigos exactos: `PersonnelFiles.ViewMedicalClaims` / `PersonnelFiles.ManageMedicalClaims` (`PersonnelFilePolicies.cs:45,53` y `PersonnelFileCommon.cs:120,126`).
- Llegan en `access-context` como permisos del usuario (el FE los lee de `currentUserAccess.permissions[].code`).
- **Autoservicio del titular: resuelto en BACKEND.** El titular puede **crear** su propio reclamo y **leer** sus reclamos sin permiso de gestión; el backend compara `personnelFile.LinkedUserPublicId` con el `UserId` autenticado (`PersonnelFileEmployeeHandlerBases.cs:227,541`). **No lo gateen en FE por permiso.** `PUT/PATCH/DELETE` sí son solo-HR (sin autoservicio).

---

## 2. Transacciones Fuera de Nómina (`off-payroll-transactions`)

### 2.1 — ✅ Desplegada (commit `f07cbbc`). Base:
`api/v1/personnel-files/{publicId}/off-payroll-transactions`

### 2.2 — Set completo de rutas ✅

| Verbo | Ruta |
| --- | --- |
| GET list | `…/off-payroll-transactions` |
| GET totals | `…/off-payroll-transactions/totals` |
| GET item | `…/off-payroll-transactions/{offPayrollTransactionPublicId}` |
| POST | `…/off-payroll-transactions` |
| PUT | `…/off-payroll-transactions/{offPayrollTransactionPublicId}` |
| PATCH | `…/off-payroll-transactions/{offPayrollTransactionPublicId}` (json-patch) |
| DELETE | `…/off-payroll-transactions/{offPayrollTransactionPublicId}` (soft) |
| GET docs | `…/{offPayrollTransactionPublicId}/documents` |
| GET doc | `…/{offPayrollTransactionPublicId}/documents/{documentPublicId}` |
| GET read-url | `…/{offPayrollTransactionPublicId}/documents/{documentPublicId}/read-url` |
| POST doc | `…/{offPayrollTransactionPublicId}/documents` |
| DELETE doc | `…/{offPayrollTransactionPublicId}/documents/{documentPublicId}` |

> ⚠️ Nombres de parámetros: `{publicId}`, **`{offPayrollTransactionPublicId}`** (NO `{txId}`), **`{documentPublicId}`** (NO `{docId}`).

### 2.3 — Catálogo ✅
`off-payroll-transaction-types`, country-scoped: `GET /api/v1/general-catalogs/off-payroll-transaction-types?countryCode=SV` (`GeneralCatalogKeyMap.cs:50`). El `code` lo ingresa el admin (no compartido con asset-access-types).

### 2.4 — Contrato ✅

```csharp
public sealed record AddOffPayrollTransactionRequest(
    string TransactionTypeCode,          // requerido (≤80)
    DateTime TransactionDateUtc,         // requerido, no futuro
    string? CurrencyCode,                // nullable; default = preferencia de la empresa
    decimal Amount,                      // ≠ 0 (negativos permitidos)
    int Year,                            // 2000..2100
    int Month,                           // 1..12
    string? Comment,                     // nullable (≤2000)
    Guid? AssetAccessPublicId,           // nullable
    Guid? CorrectsTransactionPublicId);  // nullable; REQUERIDO si Amount < 0
```

- ✅ `amount` admite **negativos** (solo prohíbe `0`).
- ✅ `correctsTransactionPublicId` **requerido cuando `amount < 0`** (validador + regla `OFF_PAYROLL_TX_CORRECTION_REQUIRED` 422). La original referenciada debe existir, estar activa, ser original (no un ajuste) y compartir moneda (`OFF_PAYROLL_TX_CORRECTED_NOT_FOUND` / `_INVALID`, 422).
- PATCH añade `isActive` (PUT no toca el estado activo).

### 2.5 — Totales ✅
`GET …/totals` → **array** `[{ currencyCode, total, count }]`, **sin conversión FX** (cada moneda por separado, solo transacciones activas). `OffPayrollTransactions.cs:33-36`.

### 2.6 — Adjuntos ✅
- `purpose = "OffPayrollTransactionDocument"` ✅ (`FileEnums.cs:20`).
- `documentTypeCatalogItemPublicId` ✅ **opcional** (`Guid?`).

### 2.7 — RBAC
- `PersonnelFiles.ViewOffPayrollTransactions` / `PersonnelFiles.ManageOffPayrollTransactions` (`PersonnelFilePolicies.cs:75,83`).
- ✅ **Sin autoservicio**: el empleado **siempre 403** (lectura y escritura son solo-HR; son registros internos con montos sensibles). No hay rama self en los gates.

### 2.8 — Concurrencia / DELETE
- `If-Match` header. **DELETE = lógico (soft, RN-10)**, devuelve `{ parentConcurrencyToken }`.

---

## 3. Competencias del Puesto (`position-competency-results` / `position-competencies`)

### 3.1 — Breaking del recurso ✅ desplegado

```csharp
public sealed record AddPositionCompetencyResultRequest(   // == Update
    Guid ExpectationPublicId,     // requerido (FK a la celda de la matriz)
    decimal AchievedScore,        // requerido
    DateTime EvaluationDateUtc,   // requerido, no futuro
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);
```
✅ Eliminados del request: `expectedScore`, `gapScore`, `competencyCode`, `desiredBehaviors`.

**Respuesta** `PersonnelFilePositionCompetencyResultResponse` (`PositionCompetencyResults.cs:20`): trae los derivados read-only `expectedScore`/`gapScore`, más `competencyCode/Name`, `competencyTypeCode/Name`, `evaluationDateUtc`, `concurrencyToken`. Nota: `expectationPublicId` en la respuesta es **nullable** (`Guid?`) — es null para filas legacy/huérfanas (LEFT JOIN).

### 3.2 — Consulta agrupada ✅ existe
`GET /api/v1/personnel-files/{publicId}/position-competencies` → `EmployeePositionCompetenciesResponse`:

```
EmployeePositionCompetenciesResponse
 ├─ personnelFileId, jobProfilePublicId?, jobProfileCode?, jobProfileTitle?
 ├─ hasAssignedPosition: bool
 └─ groups[] (EmployeePositionCompetencyTypeGroupResponse)
     ├─ competencyTypePublicId / Code / Name
     └─ competencies[] (EmployeePositionCompetencyResponse)
         ├─ expectationPublicId, competencyPublicId, competencyCode/Name
         ├─ occupationalPyramidLevel* , behaviorLevel* , expectedEvidence?
         ├─ expectedScore?, achievedScore?, gapScore?, evaluationDateUtc?  (la más reciente)
         ├─ desiredBehaviors[]  (string[])  ← sigue presente AQUÍ (derivado de conductas)
         └─ history[] (EmployeePositionCompetencyHistoryEntryResponse, desc por fecha)
              └─ positionCompetencyResultPublicId, expectedScore?, achievedScore, gapScore?, evaluationDateUtc
```
**`hasAssignedPosition=false`**: cuando no hay puesto asignado activo → HTTP **200** con `jobProfile*` en null y `groups: []` (`PersonnelFileEmployeeRepository.cs:1702-1706`). No es error.

### 3.3 — Escala de calificación ✅ existe
`GET` y `PUT` en `api/v1/companies/{companyId}/competency-rating-scale`.

```csharp
// GET → ActiveCompetencyRatingScaleResponse
record ActiveCompetencyRatingScaleResponse(bool IsConfigured, CompetencyRatingScaleResponse? Scale);

record CompetencyRatingScaleResponse(
    Guid Id, Guid CompanyId, string Code, string Name,
    CompetencyRatingScaleType ScaleType,   // "Numeric" | "Discrete"  (string)
    decimal? MinValue, decimal? MaxValue, int Decimals,
    bool IsActive, Guid ConcurrencyToken,
    IReadOnlyCollection<CompetencyRatingScaleLevelResponse> Levels,
    AllowedActionsResponse? AllowedActions);

record CompetencyRatingScaleLevelResponse(Guid Id, string Code, string Label, decimal Value, int SortOrder);

// PUT body
record SetCompetencyRatingScaleRequest(
    string Code, string Name, CompetencyRatingScaleType ScaleType,
    decimal? MinValue, decimal? MaxValue, int Decimals,
    IReadOnlyCollection<SetCompetencyRatingScaleLevelRequest>? Levels);
record SetCompetencyRatingScaleLevelRequest(string Code, string Label, decimal Value, int SortOrder);
```
- **Numeric**: usa `minValue < maxValue`, `decimals >= 0`, `levels` debe ir vacío.
- **Discrete**: `levels` ≥ 2, con `value` distintos; ignora min/max.
- **Default**: cada tenant se siembra con una escala **DISCRETE 1–5** (`ESCALA_1_5`, "Escala 1 a 5", labels Deficiente→Excelente) → `GET` devuelve `isConfigured=true` con la escala. ✅ **Resuelto para tenants existentes (deploy 2026-06-25):** se agregó un backfill idempotente en el arranque (`CompetencyFrameworkSeedService.EnsureSeededAsync`) que siembra la escala 1–5 a **toda empresa que no la tenga**, incluidas las creadas antes del feature. Así que `isConfigured=true` para todas. (Buena práctica: manejen `isConfigured=false` defensivamente igual, pero ya no debería ocurrir.)

### 3.4 — Matriz ✅
`…/job-profiles/{jobProfilePublicId}/competency-matrix/items` **acepta y devuelve** `expectedValue` (`decimal?`, opcional). ✅ El **PATCH también soporta `/expectedValue`** (y `remove`); la descripción Swagger se corrigió en el deploy 2026-06-25 para listarlo. Pueden usar `PUT` o `PATCH` indistintamente.

### 3.5 — RBAC
- Recurso del empleado: `PersonnelFiles.ViewCompetencies` / `PersonnelFiles.ManageCompetencies`.
- Escala + matriz (framework): `CompetencyFramework.Read` / `CompetencyFramework.Admin` (controladores gateados por `[ResourceActions("COMPETENCY_FRAMEWORK")]` y `[ResourceActions("JOB_PROFILE_COMPETENCY_MATRIX")]`).
- ✅ **Lectura del titular**: autoservicio resuelto en backend (match `LinkedUserPublicId`); escrituras solo-HR.

### 3.6 — Concurrencia
- ✅ `DELETE` → `{ parentConcurrencyToken }`. ✅ `PATCH` = RFC 6902 con `If-Match`. PUT también `If-Match`, nuevo token en `ETag`.

---

## 4. Competencias Curriculares (`curricular-competencies`)

### 4.1 — Shape sin cambios ✅
`AddCurricularCompetencyRequest` == `Update` (`PersonnelFileRequests.cs:634-654`):
`requirementTypeCode` (req), `requirementName` (req), `competencyDomain` (req), `experienceTimeValue?`, `metricCode?`, `notes?`, `sourceSystem?`, `sourceReference?`, `sourceSyncedUtc?`. Los 3 endurecidos (`requirementTypeCode`, `competencyDomain`, `metricCode`) **mantienen el mismo nombre**; solo se validan/normalizan contra catálogo.

### 4.2 — Catálogos
- **`requirement-types`** ✅: `GET /api/v1/companies/{companyPublicId}/position-description-catalogs/requirement-types/items` (param es `companyPublicId`). Enum `RequirementType=5`.
- **`competency-domains`** ✅ NUEVO: `GET /api/v1/companies/{companyPublicId}/position-description-catalogs/competency-domains/items` (enum `CompetencyDomain=14`). ⚠️ **Arranca VACÍO por empresa (no se siembra)** — un admin debe POSTear items antes de poder crear competencias curriculares. (Lo mismo aplica a `requirement-types`: tampoco se siembra.)
- **`experience-metrics`** ✅: `GET /api/v1/general-catalogs/experience-metrics?countryCode=SV` (country-scoped). **SV ya sembrado** vía HasData en TODOS los entornos: `ANOS`, `MESES`, `DIAS`, `HORAS` (codes ASCII; labels con acento "Años/Meses/Días/Horas"). `GlobalCatalogSeedData.cs:212-218`.

### 4.3 — Errores ✅ (5×422 + 1×409), en `extensions.code` (= propiedad `code`)

| Condición | `code` | HTTP |
| --- | --- | --- |
| tipo de requisito inválido | `CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID` | 422 |
| dominio inválido | `CURRICULAR_COMPETENCY_DOMAIN_INVALID` | 422 |
| métrica inválida | `CURRICULAR_COMPETENCY_METRIC_INVALID` | 422 |
| métrica requerida | `CURRICULAR_COMPETENCY_METRIC_REQUIRED` | 422 |
| experiencia negativa | `CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE` | 422 |
| duplicado | `CURRICULAR_COMPETENCY_DUPLICATE` | **409** |

(`CurricularCompetencies.Rules.cs:13-41`. Dedup = `requirementTypeCode|requirementName`, trim+upper.)

### 4.4 — RBAC ✅
- Lectura `PersonnelFiles.Read` / escritura `PersonnelFiles.Manage` (sin permiso dedicado). ✅
- Admin de `competency-domains` (y `requirement-types`): requiere **`PositionDescriptionCatalogs.Admin`** (o el super `iam.administration.manage`). ✅

### 4.5 — Concurrencia → ⚠️ es **400**, NO 428
Falta de `If-Match` → **`400 Bad Request`** (igual que todos los demás módulos). **No existe 412 ni 428** en ningún path. **La guía `guia-integracion-frontend-competencias-curriculares.md` que dice 428 está equivocada y se corregirá.** Token stale → `409`. PATCH = RFC 6902 (`application/json-patch+json`).

---

## 5. Resumen — desbloqueos

| Módulo | Estado | Nota clave para el FE |
| --- | --- | --- |
| **General** | ✅ | Prefijo real `api/v1` (no `/v1`); `code` en `extensions.code`; If-Match header; falta→400, stale→409. |
| Medical Claims | ✅ desplegado | Request trae además `claimTypeCode` (req) y `claimDateUtc` (req). `insurancePublicId` req no-nullable. Status key singular `medical-claim-status`. DELETE de reclamo es físico. Autoservicio titular = backend. |
| Off-Payroll | ✅ desplegado | Existe completo. Params `{offPayrollTransactionPublicId}`/`{documentPublicId}`. Negativos requieren `correctsTransactionPublicId`. Sin autoservicio (403). |
| Position Competencies | ✅ desplegado | Request = `expectationPublicId+achievedScore+evaluationDateUtc`. Agrupado + escala (default 1–5 discrete) + matriz `expectedValue` (PUT y PATCH). Escala: `isConfigured=true` para TODAS las empresas (backfill desplegado 2026-06-25). |
| Curricular Competencies | ✅ desplegado | Shape igual. `competency-domains` arranca **vacío** (sembrar por admin). `experience-metrics` SV ya sembrado. Concurrencia = **400** (no 428). |

> Tras regenerar el spec (`clarihr_refresh_spec`), si los shapes nuevos no aparecen, el build desplegado es anterior a `f07cbbc`/`d958672` → redeploy de `master`.
