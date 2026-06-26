# Plan Técnico — Entrevista de Retiro (Exit Interview)

| | |
|---|---|
| **Tipo** | Plan técnico de implementación (Fase 1) |
| **Análisis de negocio** | `docs/business/analisis-entrevista-retiro-empleado.md` (v2 — D-01…D-14, RQ-01…RQ-06 ratificados) |
| **Estado** | Propuesto / listo para construir. **No** implementado aún. |
| **Rama sugerida** | `feature/entrevista-retiro-fase1` |
| **País de referencia / seed** | El Salvador (`SV`, `CountryCatalogDefinition(-7068L,"SV",…)` — `Domain/Locations/CountryCatalogData.cs:75`) |
| **Naturaleza** | Módulo **exclusivo** (D-01): catálogos jerárquicos de motivo + tipos de control + constructor de formularios + captación de respuestas + base de tabulación (Fase 2). |
| **Sin datos de producción** | D-11 → *drop & recreate*; los motivos legados en texto libre se **eliminan**. |

> Este plan **copia patrones ya probados** en el repositorio (medical-claims, off-payroll, insurance, competencias) y cita anclas `archivo:línea`. La regla de oro: **no inventar infraestructura** — reutilizar catálogos país-scoped + validar-por-código + `HasData` + controlador dedicado + `*.Rules.cs` + paridad EN/ES.

---

## 0. Decisiones de arquitectura (derivadas del negocio)

| # | Punto | Decisión técnica | Origen |
|---|---|---|---|
| A-1 | Familia de catálogos del motivo | **`PersonnelReferenceCatalogItem`** (país-scoped, **jerárquico con FK al padre** — es el template exacto de `InsuranceType → InsuranceRange`). | D-02 |
| A-2 | `SeparationType` | Columna extra (enum string) en `RetirementCategoryCatalogItem`. | D-02 |
| A-3 | Catálogo de tipos de control | **`GeneralCatalogItem`** (plano, de sistema) con metadatos `ValueKind/SupportsOptions/SupportsRange/SupportsMultiple`. | D-08 |
| A-4 | Admin CRUD de catálogos | **DIFERIDO.** Hoy los catálogos país-scoped son **seed-only/read-only** (no existe infraestructura de escritura). Fase 1 los gestiona por `HasData`. RF-015 → fase posterior. | hallazgo de código |
| A-5 | Asociación form↔motivo | `RetirementReasonCode` + `IsActiveForReason` **en el propio formulario** (1:1, sin tabla de unión). | D-03 |
| A-6 | Anonimato | Bandera `IsAnonymous` **en el formulario**; la submission anónima **no** persiste FK al expediente. | D-06 |
| A-7 | Score | Índice **0–100 derivado** (servicio puro); no se captura. | D-07 |
| A-8 | Adjuntos | **No** se implementan (sin `FilePurpose` nuevo). | D-09 |
| A-9 | Lectura de respuestas | **Solo RRHH** (gate en handler, sin self-service en lectura de submissions de terceros). | D-14 |
| A-10 | Área (tabulación) | **Plaza (`PositionSlot`) a la fecha de baja**, snapshot en la submission. | RQ-02 |

---

## 1. Mapa de capas y anclas de referencia (as-is)

**Catálogos país-scoped (template a copiar):**
- Base: `Domain/Common/CountryScopedCatalogItem.cs` (Id, PublicId, CountryCatalogItemId, CountryCode, Code, NormalizedCode, Name, NormalizedName, IsActive, SortOrder, ConcurrencyToken).
- Familia jerárquica: `Domain/PersonnelFiles/PersonnelReferenceCatalogItem.cs` → `InsuranceTypeCatalogItem` / `InsuranceRangeCatalogItem` (FK padre). Config con override de índice único `(country, type, code)`: `Infrastructure/Persistence/Configurations/PersonnelFiles/PersonnelReferenceCatalogItemConfiguration.cs:209-215` (`ConfigureUniqueCodeIndex`).
- Familia plana de sistema: `Domain/GeneralCatalogs/GeneralCatalogItems.cs` (p.ej. `EmploymentStatusCatalogItem`) + `Configurations/GeneralCatalogs/GeneralCatalogItemConfiguration.cs` (`GeneralCatalogItemConfigurationBase(... seedData)` → `HasData`).
- Wire keys: `Application/Features/PersonnelFiles/Catalogs/GeneralCatalogKeyMap.cs` (`CatalogKeys` + `TryResolveCatalogCategory` / `TryResolveReferenceCategory`) + categorías constantes `PersonnelCurriculumCatalogCategories`.
- Lectura API: `Api/Controllers/GeneralCatalogsController.cs` (`GET api/v1/general-catalogs/{catalogKey}?countryCode=`) y su equivalente `reference-catalogs`.
- Validar-por-código: `IPersonnelFileRepository.CatalogCodeIsActiveAsync(tenantId, category, code, ct)` (+ resolutor de nombre por `(CountryCatalogItemId, NormalizedCode)`).
- Seed: `Infrastructure/Persistence/GlobalCatalogSeedData.cs` (`CreateSeedPublicId`, `ResolveCountryId`, shape de objeto anónimo); opt-in en la config (ej. `EmploymentStatusCatalogItemConfiguration`, `ExperienceMetricCatalogItemConfiguration`).

**Entidades hijas de `PersonnelFile` (template a copiar):**
- Dominio: `Domain/PersonnelFiles/PersonnelFileEmployee.cs` (`PersonnelFileMedicalClaim` 994-1240; child-of-child `MedicalClaimDocument` 1265-1372; `PersonnelFileOffPayrollTransaction` 1384-1535) — patrón `TenantEntity` + `PublicId`/`ConcurrencyToken` + ctor privado + `Create` + `Update`/`Apply` + `BindToParent`.
- EF config: `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs` (medical 405-452; doc 454-502; off-payroll 504-547). Registro automático: `ApplicationDbContext.cs:344` (`ApplyConfigurationsFromAssembly`).
- Repositorio: `Infrastructure/PersonnelFiles/PersonnelFileEmployeeRepository.cs` (medical 1178-1290) — Add re-consulta la colección; Update/Patch cargan por `PublicId`+`TenantId`; reads `AsNoTracking` por `PersonnelFile.PublicId`.
- CQRS: `Application/Features/PersonnelFiles/Compensation/MedicalClaims.cs` (+ `.Handlers.cs`) y contrato `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs`. Dispatch: `ICommandDispatcher`/`IQueryDispatcher`.
- Auditoría: `PersonnelFileEmployeeAudits.LogUpdateAsync` (`Common/PersonnelFileEmployeeHandlerBases.cs:35-78`).

**Autorización (5 puntos de registro por permiso):**
1. `Application/Features/PersonnelFiles/Common/PersonnelFilePolicies.cs` (constantes de policy).
2. `Application/Features/PersonnelFiles/Common/PersonnelFileCommon.cs` → `PersonnelFilePermissionCodes` (77-150).
3. `Application/Features/Provisioning/Common/ProvisioningConstants.cs` (55-75, definiciones legibles + seed de rol admin).
4. `Api/Program.cs` (471-512, `AddPolicy` + `RequireAssertion`/`Combine`).
5. `tests/CLARIHR.Application.UnitTests/AuthorizationPolicyConventionGovernanceTests.cs` (87-108, `PersonnelFilePolicyNames`).
- Atributo + convención: `Api/Common/Conventions/AuthorizationPolicySetAttribute.cs` + `AuthorizationPolicyConvention.cs` (GET→read, write→manage; **solo a nivel de clase** → controlador dedicado).
- Gates self-service: `Common/PersonnelFileEmployeeHandlerBases.cs` (`LoadForCreateOwnOrManageMedicalClaimAsync` 227-265; `LoadCompletedEmployeeForMedicalClaimReadAsync` 541-588; HR-only `LoadCompletedEmployeeForInsuranceReadAsync` 499-540) + `IPersonnelFileAuthorizationService` (gates con default-interface fail-closed).

**Puntos de enganche:**
- Validación de baja: `Application/Features/PersonnelFiles/Employment/EmployeeProfiles.cs` (Create con códigos de retiro ~216-223; validador 118-134; patrón catálogo `CatalogCodeIsActiveAsync` 184-189).
- Rehire: `Application/Features/PersonnelFiles/Rehire/RehireEmployee.cs:149-176` (limpia retiro / crea nuevo periodo; `IUnitOfWork` disponible 65-74).
- Localización: `Infrastructure/Localization/BackendMessages.resx` + `.es.resx`; test de paridad `tests/.../BackendMessageLocalizationTests.cs:64-83` (escanea `Error` en `*.Rules.cs`).
- Migraciones: `Infrastructure/Persistence/Migrations/` (prefijo timestamp). **Gotcha:** `dotnet ef` 9.0.x requiere `DOTNET_ROLL_FORWARD=Major`.

---

## 2. Modelo de datos (to-be)

### 2.1 Catálogos (país-scoped, familia `PersonnelReferenceCatalogItem`)

**`RetirementCategoryCatalogItem : PersonnelReferenceCatalogItemBase`** — tabla `retirement_category_catalog_items`
| Columna | Tipo | Notas |
|---|---|---|
| (base) | — | id, public_id, country_catalog_item_id, country_code, code, normalized_code, name, normalized_name, is_active, sort_order, concurrency_token |
| separation_type | varchar(20) | `VOLUNTARIA`/`INVOLUNTARIA`/`OTRA` (D-02). Check constraint o validación de dominio. |
- Índice único: `(country_catalog_item_id, normalized_code)` (default de la base).

**`RetirementReasonCatalogItem : PersonnelReferenceCatalogItemBase`** — tabla `retirement_reason_catalog_items`
| Columna | Tipo | Notas |
|---|---|---|
| retirement_category_catalog_item_id | bigint FK | Padre (categoría) — `DeleteBehavior.Restrict`. Nav property. |
| (base) | — | … |
- Índice único **override** `(country_catalog_item_id, retirement_category_catalog_item_id, normalized_code)` — copiar `ConfigureUniqueCodeIndex` de `InsuranceRangeCatalogItem` (`PersonnelReferenceCatalogItemConfiguration.cs:209-215`). Nombre ≤ 63 chars (límite PG): `uq_retirement_reason_catalog_items__country_code`.

### 2.2 Catálogo de tipos de control (sistema, familia `GeneralCatalogItem`)

**`FormControlTypeCatalogItem : GeneralCatalogItem`** — tabla `form_control_type_catalog_items`
| Columna | Tipo | Notas |
|---|---|---|
| (base país-scoped) | — | code (`TEXTO_CORTO`…`ESCALA`), name localizable, etc. |
| value_kind | varchar(20) | `TEXT`/`NUMBER`/`DATE`/`BOOLEAN`/`OPTIONS` |
| supports_options | bool | lista/radio/múltiple |
| supports_range | bool | número/escala |
| supports_multiple | bool | selección múltiple |
- Seed por `HasData` (igual para todos los países; se siembra bajo `SV` para cumplir "seed por país"; el código es universal).

### 2.3 Agregado del formulario (tenant-scoped, `TenantEntity`)

**`ExitInterviewForm`** — `exit_interview_forms`
| Columna | Tipo | Notas |
|---|---|---|
| id / public_id / tenant_id / concurrency_token | — | base |
| name / normalized_name | varchar(200) | único `(tenant, normalized_name)` |
| description | varchar(1000) | opcional |
| is_anonymous | bool | A-6 / D-06; fijo tras publicar |
| status | varchar(20) | `Draft`/`Published`/`Archived` |
| version | int | ≥ 1 |
| retirement_reason_code | varchar(80) | nullable; **1 motivo** (A-5) |
| is_active_for_reason | bool | single-active por motivo |
| is_active | bool | soft-delete |
- Índices: uq `(tenant, public_id)`; uq parcial **`(tenant, retirement_reason_code) WHERE is_active_for_reason AND status='Published'`** (garantiza single-active); idx `(tenant, status)`.

**`ExitInterviewFormGroup`** — `exit_interview_form_groups` (FK form, cascade): name/title, description, display_order.

**`ExitInterviewFormField`** — `exit_interview_form_fields` (FK form cascade; FK group nullable restrict): control_type_code, field_key/normalized (uq `(form, normalized_field_key)`), title, description, weight (numeric(9,2) ≥0), is_required, display_order, min_value/max_value (numeric), max_length (int), scale_max (int). **Sin** `is_anonymous` (D-06).

**`ExitInterviewFormFieldOption`** — `exit_interview_form_field_options` (FK field cascade): option_code/normalized (uq `(field, normalized_option_code)`), label, score (numeric(6,2) nullable, 0–100), display_order.

### 2.4 Respuestas (tenant-scoped)

**`ExitInterviewSubmission`** — `exit_interview_submissions`
| Columna | Tipo | Notas |
|---|---|---|
| id / public_id / tenant_id / concurrency_token | — | base |
| form_id / form_version | FK / int | snapshot de versión |
| is_anonymous | bool | snapshot del flag del formulario |
| personnel_file_id | bigint FK **nullable** | **NULL si anónimo** (A-6); restrict |
| submitted_by_user_id | uuid nullable | NULL si anónimo |
| retirement_reason_code / retirement_category_code / separation_type | varchar | dimensiones (snapshot) |
| position_slot_public_id | uuid nullable | **plaza a la fecha de baja** (RQ-02) |
| plaza_snapshot | varchar(200) | etiqueta de plaza (snapshot) |
| period | varchar(7) | `YYYY-MM` (RetirementDate; fallback fecha de envío) |
| status | varchar(20) | `Draft`/`Submitted`/`Archived` (D-12) |
| submitted_utc | timestamptz nullable | no futura |
| total_score | numeric(6,2) nullable | índice 0–100 (derivado) |
- Índices: uq `(tenant, public_id)`; idx `(tenant, personnel_file_id, status)`; idx de tabulación `(tenant, period, retirement_category_code)`.

**`ExitInterviewAnswer`** — `exit_interview_answers` (FK submission cascade): field_key_snapshot, title_snapshot, control_type_code, value_text, value_number, value_date, value_bool, selected_option_codes (jsonb/text), weight_snapshot (numeric), normalized_score (numeric 0–100 nullable). *(Sin anonimato por respuesta — el anonimato es de la submission.)*

### 2.5 Perfil existente (a evolucionar)

`PersonnelFileEmployeeProfile.RetirementCategoryCode` / `RetirementReasonCode`: de **texto libre** → **validado por código** (PR-1). Sin migración de datos (D-11): limpiar columnas.

---

## 3. Modelo de puntaje (servicio puro — D-07)

`ExitInterviewScoring` (estático, sin BD; en `ExitInterview.Rules.cs` o sibling, unit-testeado):
- **Opción:** `Score` 0–100 (lo define RRHH).
- **Escala Likert (1..n):** `normalized = (value − 1) / (n − 1) × 100`.
- **Campos puntuables:** solo selección (con `Score`) y escala. Texto/fecha/número libre **no** puntúan.
- **Índice de submission:** `Σ(Weight_i × normScore_i) / Σ(Weight_i)` sobre campos puntuables **respondidos**; si `Σ(Weight)=0` → `null`.
- **Dirección:** mayor = experiencia más favorable (RQ-03).
- Se calcula al **enviar** (`Submitted`) y se recalcula en corrección RRHH (Fase 2, RF-020).

---

## 4. Permisos y controladores

**3 permisos nuevos** (registrar en los 5 puntos de §1):
- `PersonnelFiles.ManageExitInterviewForms` — diseñar/publicar/asociar formularios (**HR-only**, `RequireAssertion`).
- `PersonnelFiles.ManageExitInterviews` — llenar/capturar submissions (**authn-only superset** + gate self-service en handler — D-04).
- `PersonnelFiles.ViewExitInterviews` — leer submissions/tabulación (**authn-only superset**; el gate de handler restringe a **RRHH** la lectura de submissions ajenas — D-14).

**Gates nuevos** en `IPersonnelFileAuthorizationService` (default-interface fail-closed) + implementación: `EnsureCanManageExitInterviewFormsAsync`, `EnsureCanManageExitInterviewsAsync`, `EnsureCanViewExitInterviewsAsync`.

**Helpers de carga** en `PersonnelFileEmployeeHandlerBases` (copiar de medical-claims):
- `LoadForCreateOwnOrManageExitInterviewAsync` (fill: self-service **o** Manage — D-04).
- `LoadOwnDraftOrManageExitInterviewAsync` (leer/retomar **propio borrador**; RRHH para cualquiera; **no** lectura de enviados ajenos por el empleado — RQ-05).
- `LoadForViewExitInterviewsAsync` (lista/detalle de submissions: **RRHH-only**, sin self-service — D-14, copiar el patrón insurance HR-only 499-540).

**2 controladores dedicados:**
- `ExitInterviewFormsController` — `[AuthorizationPolicySet(ManageExitInterviewForms, ManageExitInterviewForms)]`. Diseño (HR): forms/groups/fields/options, publish/new-version/archive, set-reason, list/preview/clone. Rutas `api/v1/exit-interview-forms…`.
- `ExitInterviewsController` — `[AuthorizationPolicySet(ViewExitInterviews, ManageExitInterviews)]`. Uso: resolver-formulario-aplicable (self/manage), crear/guardar/enviar submission (self/manage), listar/ver submissions (RRHH). Rutas `api/v1/personnel-files/{id}/exit-interview…`.

> El empleado lee el **formulario a llenar** y su **borrador** por endpoints del `ExitInterviewsController` bajo el gate self-service; la **gestión de definiciones** vive en `ExitInterviewFormsController` (HR-only). Así la lectura de submissions ajenas queda exclusivamente en RRHH (D-14).

---

## 5. Plan por PRs

> Cada PR: compila la solución completa + suite unitaria en verde + paridad EN/ES. `DOTNET_ROLL_FORWARD=Major` para `dotnet ef`.

### PR-1 — Catálogos de motivo de retiro + seed SV + validar-por-código en la baja
**Objetivo:** dejar el "motivo de retiro" catalogado, jerárquico, validado (prerrequisito y mayor valor inmediato — RF-001).
- Dominio: `RetirementCategoryCatalogItem` (+`SeparationType`) y `RetirementReasonCatalogItem` (FK categoría) en la familia `PersonnelReferenceCatalogItem`.
- EF configs (+ override de índice único jerárquico) y descubrimiento automático en `ApplicationDbContext`.
- Wire keys en `GeneralCatalogKeyMap` (`retirement-categories`, `retirement-reasons` → `TryResolveReferenceCategory`) + constantes en `PersonnelCurriculumCatalogCategories` (`RetirementCategory`, `RetirementReason`).
- `GlobalCatalogSeedData`: `GetRetirementCategoryCatalogItems()` (con `SeparationType` por la tabla §7) y `GetRetirementReasonCatalogItems()` (con FK a la categoría) — `HasData` opt-in en las configs (patrón `EmploymentStatusCatalogItemConfiguration`).
- Repositorio: soporte para `CatalogCodeIsActiveAsync` en ambas categorías + resolutor `GetRetirementReasonResolvedAsync(tenant, reasonCode)` → `(reasonCode, categoryCode, separationType, names)` (para snapshot de submission).
- Enganche baja: en `EmployeeProfiles.cs` (validador + handler) validar `RetirementReasonCode`/`RetirementCategoryCode` por código (patrón `CatalogCodeIsActiveAsync` 184-189) y que el motivo sea **hijo** de la categoría. Errores `RETIREMENT_CATEGORY_INVALID`, `RETIREMENT_REASON_INVALID`, `RETIREMENT_REASON_CATEGORY_MISMATCH` (bilingües).
- Migración `…_AddRetirementReasonCatalogs` (incluye `HasData`). **Limpieza** de columnas legadas (D-11): set `NULL` en `retirement_*_code`.
- Tests: catálogo seed presente; validación rechaza inactivo/mismatch; el GET por key responde con `SeparationType`.

### PR-2 — Catálogo de tipos de control (sistema) + lectura
- Dominio `FormControlTypeCatalogItem` (familia `GeneralCatalogItem`) + metadatos.
- Wire key `form-control-types` (`TryResolveCatalogCategory`) + constante de categoría.
- Seed `HasData` de los 9 tipos (§7) bajo `SV`.
- Tests: GET por key con banderas (`supports_options`, etc.); set cerrado.

### PR-3 — Agregado de formulario (Form/Group/Field/Option) + reglas + validación
- Dominio: `ExitInterviewForm` + `…Group` + `…Field` + `…FieldOption` (ctor privado + `Create`/`Update`/`Apply` + `BindToParent`).
- EF configs (FKs cascade/restrict, índices únicos §2) + descubrimiento automático.
- Repositorio (en `PersonnelFileEmployeeRepository` o `ExitInterviewRepository` dedicado): Add/Update/Delete/Get(list) por `(tenant, public_id)` y travesía por `Form.PublicId`.
- CQRS: `ExitInterviewForms.cs` (records/commands/queries/validators) + `.Handlers.cs`; contratos en `PersonnelFileRequests.cs` (o `ExitInterviewRequests.cs`).
- `ExitInterview.Rules.cs`: `ExitInterviewErrors` (códigos `EXIT_INTERVIEW_*`) + `ExitInterviewRules` puro: coherencia tipo↔opciones/rango, claves únicas, peso/score ≥0, ≥1 campo, selección con ≥1 opción (RF-007). Devuelve **lista** de incidencias (estilo preview de finalize).
- Permiso `ManageExitInterviewForms` (5 puntos) + gate `EnsureCanManageExitInterviewFormsAsync` + `ExitInterviewFormsController` (CRUD forms/groups/fields/options + preview + list + clone).
- Localización `EXIT_INTERVIEW_*` (EN/ES) + auditoría en escrituras.
- Migración `…_AddExitInterviewForms`.
- Tests: reglas (unit) + handlers de CRUD + gobernanza de policy.

### PR-4 — Publicar / versionar / archivar + asociación a 1 motivo (single-active)
- Estado `Draft→Published` (exige pasar `ExitInterviewRules.ValidateDefinition`); `Published` inmutable → `new-version` clona a `Draft` v+1; `archive`.
- `set-reason` (un `reasonCode` validado por catálogo) + regla **single-active por motivo** (índice único parcial + verificación en handler; activar desactiva el anterior).
- Resolver: `GetApplicableFormByReasonAsync(tenant, reasonCode)` → definición renderizable + versión.
- Tests: publish-bloqueo, versionado-snapshot, single-active, resolución determinista, "sin formulario".

### PR-5 — Submissions/Answers (llenar) + anonimato + score + self-service
- Dominio `ExitInterviewSubmission` + `ExitInterviewAnswer` (anónima → `personnel_file_id` NULL; dimensiones de-identificadas; `position_slot`/`plaza`/`period` snapshot — RQ-02).
- EF configs (FK nullable a expediente; índices §2) + repositorio.
- Servicio `ExitInterviewScoring` (§3) + validación de valores por tipo de control (rango/opciones/longitud/fecha).
- CQRS: crear/guardar-parcial/enviar + resolver-form-aplicable + leer (mi borrador / lista RRHH / detalle RRHH).
- Permisos `ManageExitInterviews` + `ViewExitInterviews` (5 puntos) + gates (`LoadForCreateOwnOrManageExitInterviewAsync`, `LoadOwnDraftOrManageExitInterviewAsync`, `LoadForViewExitInterviewsAsync`) + `ExitInterviewsController`.
- Regla **una submission activa por empleado+baja** (RQ-06): reintentos editan el `Draft`.
- Migración `…_AddExitInterviewSubmissions`.
- Tests: self-service vs 403; obligatorios al enviar; score determinista (caso `(2×75+1×100)/3=83.3`); submission anónima sin FK; lectura RRHH-only.

### PR-6 — Rehire (archivar) + auditoría + gobernanza/localización + cierre
- Enganche en `RehireEmployee.cs` (tras upsert del nuevo perfil ~176, dentro de la transacción): `ArchiveSubmissionsForPriorPeriodAsync(personnelFileId, tenantId)` → `Submitted`/`Draft` del periodo previo pasan a `Archived` (D-12).
- Revisar `Finalize`/baja: la entrevista es **opcional** (D-05) — **no** se agrega gate bloqueante.
- Actualizar `AuthorizationPolicyConventionGovernanceTests` (3 policies) + verificar `BackendMessageLocalizationTests` (todos los `EXIT_INTERVIEW_*`/`RETIREMENT_*` con paridad).
- DevSeed (opcional, dev-only): un formulario de ejemplo publicado + asociado a un motivo, para QA/front.
- Suite completa en verde.

### Fase 2 (fuera de este plan) — Tabulación + corrección
- **PR-7 (RF-014):** reportes por motivo/categoría/**plaza(área)**/periodo/score + export; umbral **k ≥ 5** (RQ-04) para no re-identificar anónimos.
- **PR-8 (RF-020):** corrección/anulación de submission por RRHH (recalcula score, audita).

### Diferido (no Fase 1)
- **RF-015 (CRUD admin de catálogos de retiro):** requiere **infraestructura de escritura de catálogos** que hoy **no existe** (A-4). Se gestiona por `HasData`/seed; se evalúa cuando exista el patrón de catálogos editables por tenant.

---

## 6. Errores (bilingües `EXIT_INTERVIEW_*` / `RETIREMENT_*`)

| Código | ErrorType | Uso |
|---|---|---|
| `RETIREMENT_CATEGORY_INVALID` | UnprocessableEntity | categoría inexistente/inactiva (baja) |
| `RETIREMENT_REASON_INVALID` | UnprocessableEntity | motivo inexistente/inactivo (baja) |
| `RETIREMENT_REASON_CATEGORY_MISMATCH` | UnprocessableEntity | el motivo no pertenece a la categoría |
| `EXIT_INTERVIEW_FORM_NAME_DUPLICATE` | Conflict | nombre repetido por tenant |
| `EXIT_INTERVIEW_FIELD_KEY_DUPLICATE` | Conflict | clave de campo repetida |
| `EXIT_INTERVIEW_FIELD_WEIGHT_INVALID` | Validation | peso < 0 |
| `EXIT_INTERVIEW_FIELD_RANGE_INVALID` | Validation | min > max |
| `EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED` | UnprocessableEntity | selección sin opciones |
| `EXIT_INTERVIEW_OPTION_CODE_DUPLICATE` | Conflict | opción repetida en el campo |
| `EXIT_INTERVIEW_NOT_PUBLISHABLE` | UnprocessableEntity | definición incoherente (lista de issues) |
| `EXIT_INTERVIEW_REASON_ALREADY_HAS_ACTIVE_FORM` | Conflict | (si se opta por bloquear en vez de desactivar) |
| `EXIT_INTERVIEW_FORM_NOT_PUBLISHED` | UnprocessableEntity | asociar/usar un form no publicado |
| `EXIT_INTERVIEW_REQUIRED_ANSWER_MISSING` | UnprocessableEntity | obligatorio vacío al enviar (lista) |
| `EXIT_INTERVIEW_ANSWER_VALUE_INVALID` | Validation | valor fuera de rango/opción inexistente |
| `EXIT_INTERVIEW_SUBMISSION_ALREADY_SUBMITTED` | Conflict | editar tras `Submitted` |

(Agregar EN + ES en `BackendMessages*.resx`; el test de paridad los exige.)

---

## 7. Seed SV (D-13)

**Categorías (`retirement_category_catalog_items`) con `separation_type`** (RQ-01):

| code | separation_type |
|---|---|
| `VOLUNTARIA` | VOLUNTARIA |
| `JUBILACION` | VOLUNTARIA |
| `INVOLUNTARIA` | INVOLUNTARIA |
| `ABANDONO` | INVOLUNTARIA |
| `NO_SUPERA_PERIODO_PRUEBA` | INVOLUNTARIA |
| `FIN_CONTRATO` | OTRA |
| `MUTUO_ACUERDO` | OTRA |
| `FALLECIMIENTO` | OTRA |

**Motivos (`retirement_reason_catalog_items`, FK a categoría):** *VOLUNTARIA* → `MEJOR_OFERTA_SALARIAL`, `CRECIMIENTO_PROFESIONAL`, `AMBIENTE_LABORAL`, `RELACION_JEFATURA`, `MOTIVOS_PERSONALES`, `SALUD`, `ESTUDIOS`, `REUBICACION_GEOGRAFICA`, `DISTANCIA_TRANSPORTE`, `INSATISFACCION_FUNCIONES`; *INVOLUNTARIA* → `BAJO_DESEMPENO`, `REESTRUCTURACION`, `FALTA_DISCIPLINARIA`, `AUSENTISMO`, `INCUMPLIMIENTO_POLITICAS`, `RECORTE_PRESUPUESTARIO`; *OTRA* → `FIN_CONTRATO_TEMPORAL`, `FIN_OBRA_PROYECTO`, `JUBILACION_EDAD`, `MUTUO_ACUERDO`.

**Tipos de control (`form_control_type_catalog_items`):**

| code | value_kind | options | range | multiple |
|---|---|---|---|---|
| `TEXTO_CORTO` | TEXT | no | no | no |
| `TEXTO_LARGO` | TEXT | no | no | no |
| `NUMERO` | NUMBER | no | sí | no |
| `FECHA` | DATE | no | no | no |
| `LISTA_DESPLEGABLE` | OPTIONS | sí | no | no |
| `OPCION_UNICA` | OPTIONS | sí | no | no |
| `SELECCION_MULTIPLE` | OPTIONS | sí | no | sí |
| `CASILLA` | BOOLEAN | no | no | no |
| `ESCALA` | NUMBER | no | sí | no |

> Shape de seed: objeto anónimo con `Id` (negativo), `PublicId=CreateSeedPublicId(...)`, `CountryCatalogItemId=ResolveCountryId("SV")`, `Code`, `NormalizedCode`, `Name`, `NormalizedName`, `IsActive`, `SortOrder`, `ConcurrencyToken`, `CreatedUtc`/`ModifiedUtc` (patrón `GlobalCatalogSeedData`).

---

## 8. Plan de pruebas
- **Unitarias (`CLARIHR.Application.UnitTests`):** `ExitInterviewRules` (coherencia de definición), `ExitInterviewScoring` (índice 0–100, casos borde Σpeso=0/escala), validadores FluentValidation, gates de autorización (self vs RRHH vs 403).
- **Gobernanza:** `AuthorizationPolicyConventionGovernanceTests` reconoce las 3 policies y ambos controladores tienen el marcador.
- **Localización:** `BackendMessageLocalizationTests` (paridad EN/ES de todos los códigos nuevos).
- **Integración (cuando el entorno PG esté disponible):** seed SV presente; baja con motivo válido/ inválido; publish→associate→resolve→fill→submit→score; submission anónima sin FK; rehire archiva; lectura RRHH-only.
- **Smoke de migración** en PG vivo (como en módulos previos).

---

## 9. Riesgos y mitigaciones (técnicos)
- **Índices con nombre > 63 chars (PG):** nombrar explícitamente (`uq_…`, `ix_…`) y verificar longitud (gotcha conocido).
- **`dotnet ef` 9.0.x:** `DOTNET_ROLL_FORWARD=Major`.
- **Anonimato real:** el handler de submission **no** debe setear `personnel_file_id`/`submitted_by_user_id` si `IsAnonymous`; cubrir con test. Auditoría: registrar evento sin re-identificar.
- **Secuencia baja↔entrevista:** la resolución del formulario depende de `RetirementReasonCode` en el perfil; si no está, "sin formulario" (la entrevista es opcional). Documentar en la guía de frontend.
- **Single-active por motivo:** preferir índice único **parcial** + verificación en handler (no solo app-level) para evitar carreras.
- **Default-interface gates:** agregar el método en `IPersonnelFileAuthorizationService` **y** su implementación concreta (si falta, el default fail-closed bloquea todo).

---

## 10. Checklist de "definición de hecho" (Fase 1)
- [ ] Catálogos `retirement-categories`(+SeparationType)/`retirement-reasons`/`form-control-types` con **seed SV** (`HasData`) y lectura por key.
- [ ] Baja **valida por código** el motivo/categoría (jerárquico).
- [ ] Constructor: form (con `IsAnonymous`) + grupos + campos + opciones + reglas + preview + clone + list.
- [ ] Publicar/versionar/archivar + asociación a **1 motivo** (single-active) + resolver aplicable.
- [ ] Llenar (self-service + RRHH), guardar parcial, enviar; **una** submission activa; **score 0–100** derivado; **anónimo** disociado; **plaza/periodo** snapshot.
- [ ] **Lectura de respuestas solo RRHH**; empleado solo su borrador.
- [ ] **Rehire archiva** submissions del periodo previo.
- [ ] 3 permisos en los **5 puntos** + 2 controladores dedicados + gobernanza verde.
- [ ] Errores **EN/ES** con paridad; reglas puras unit-testeadas; solución y suite en verde.
- [ ] **Guía de integración frontend** `docs/technical/guia-integracion-frontend-entrevista-retiro.md`.

---

## 11. Entregables de documentación
- Este plan (`docs/technical/plan-tecnico-entrevista-retiro.md`).
- Guía frontend (post-implementación): `docs/technical/guia-integracion-frontend-entrevista-retiro.md` (endpoints, contratos, flujo construir→asociar→resolver→llenar→enviar, semántica anónimo/score, lectura RRHH).
