# Plan Técnico de Implementación — Plazas Asignadas: Ingresos y Egresos configurables

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-plazas-ingresos-egresos.md`](../business/analisis-plazas-ingresos-egresos.md) (decisiones D-01…D-20, resoluciones R-1…R-6) |
| **Módulos** | `PersonnelFiles` (Compensación) · `PositionSlots` · `GeneralCatalogs` · `SalaryTabulator`/`JobProfiles` · `IdentityAccess` (RBAC) · Localization · (nuevo) Renta/ISR |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-21 |

---

## 1. Objetivo y enfoque

Entregar la **configuración** de compensación por **plaza** y por **empleado**: un modelo unificado de **conceptos** (ingresos/egresos) **fijos o porcentuales**, con **salario en 3 niveles**, **descuentos de ley** (ISSS/AFP con aporte patronal) **sugeridos automáticamente**, y una **tabla de Renta** configurable. El **cálculo de planilla queda fuera** (D-08): esto produce los datos que una futura nómina consumirá.

**Insights centrales del análisis de código (esto NO es greenfield):**

1. **Existe un módulo de Compensación** con `PersonnelFileSalaryItem` (ingresos fijos, ligados al expediente) + `PersonnelFileCompensationController`. Lo **reemplazamos** por un modelo unificado `PersonnelFileCompensationConcept` ligado a la **plaza** (`PersonnelFileEmploymentAssignment`). Como **no hay datos en producción** (D-11), la migración es **drop & recreate** de `personnel_file_salary_items`.
2. **El patrón a espejar ya está construido**: la feature `EmploymentAssignments` (`Features/PersonnelFiles/Employment/`) es la plantilla exacta para `CompensationConcepts` — módulo de reglas puro (`EmploymentAssignmentRules.Evaluate`), `*CommandSupport`, `*PatchApplier`, handlers transaccionales y repo de colección hija (`IPersonnelFileEmployeeRepository`).
3. **El catálogo genérico es pobre en atributos**, pero un catálogo derivado **sí puede llevar columnas extra** (probado por `BankCatalogItem` con `Alias/SwiftCode/RoutingCode`). El `CompensationConceptTypeCatalogItem` será un catálogo **enriquecido** (nature, isStatutory, rates, cap). La respuesta genérica `PersonnelCatalogItemResponse` (Id/Category/Code/Name/IsSystem/IsActive/SortOrder) **no** puede transportar esos atributos ⇒ se añade un **endpoint de lectura dedicado**.
4. **La cadena de rango salarial ya existe**: `PositionSlot.JobProfileId → JobProfile → JobProfileCompensation.SalaryTabulatorLineId → SalaryTabulatorLine.{MinAmount,MaxAmount}`. El **bloqueo** del salario negociado (R-3) se valida contra ese `[Min,Max]`.
5. **El rename `employment-assignments → assigned-positions`** afecta el **wire** del controller + tests de integración; los tipos C# son opcionales (ver §3.8).

El alcance se reduce a: **3 entidades nuevas** (concepto, catálogo enriquecido, tabla de Renta) + **2 catálogos simples** + **1 campo en `PositionSlot`** + **1 feature CQRS espejada** + **1 permiso** + **el hook de auto-sugerencia** + **drop de SalaryItem** + **rename de rutas** + **errores localizados**.

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo) | Implicación |
|---|---|---|---|
| 1 | Plaza (asignación) | `PersonnelFileEmploymentAssignment` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:99`); feature `Features/PersonnelFiles/Employment/EmploymentAssignments{,.Rules,.Handlers}.cs`; controller `PersonnelFileEmploymentController.cs` (rutas `…/employment-assignments`). | Plantilla a espejar para `CompensationConcepts`; objetivo del rename a `assigned-positions`. |
| 2 | Ingresos actuales | `PersonnelFileSalaryItem` (`…:335`), feature `Compensation/SalaryItems{,.Handlers}.cs`, 6 rutas `…/salary-items`, contratos `*SalaryItem*`, config `personnel_file_salary_items`, métodos repo en `IPersonnelFileEmployeeRepository`. | **Drop & recreate** (D-11); reemplazo total por conceptos. |
| 3 | Catálogo base | `CountryScopedCatalogItem` (`Domain/Common/CountryScopedCatalogItem.cs`) → `GeneralCatalogItem` (abstracto). Derivado enriquecido: `BankCatalogItem` (Alias/Swift/Routing). EF base `GeneralCatalogItemConfigurationBase<T>`; enriquecido `BankCatalogItemConfiguration`. | Clonar AssignmentType (simple) y Bank (enriquecido) para los catálogos nuevos. |
| 4 | Wiring de catálogos | Constantes `PersonnelCurriculumCatalogCategories` (`Catalogs/PersonnelReferenceCatalogs.cs:80`); `GeneralCatalogKeyMap.CatalogKeys`; guardrail `GeneralCatalogKeyMapGuardrailsTests`; seed `DevSeedService.SeedGeneralCatalogItems` (`:269-379`); validación `PersonnelFileRepository.CatalogCodeIsActiveAsync` (`:1359`, switch por categoría); lectura `GeneralCatalogsController` + `PersonnelCatalogItemResponse`. | Agregar 3 categorías + 3 wire-keys + seed + casos del switch. El enriquecido necesita **endpoint dedicado**. |
| 5 | Registro EF | `ApplicationDbContext`: `DbSet<>` explícito + `ApplyConfigurationsFromAssembly` (`OnModelCreating:305`). | Por cada entidad nueva: clase + `IEntityTypeConfiguration<T>` + `DbSet<>`. |
| 6 | PositionSlot | `Domain/PositionSlots/PositionSlot.cs:5` (sin salario); `Create/UpdateCore`; config `position_slots`; comandos `Create/UpdatePositionSlotCommand`; controller `companies/{companyId}/position-slots`. | Agregar `configuredBaseSalary` + `currencyCode` (D-02 nivel 2, R-2). |
| 7 | Rango salarial | `PositionSlot.JobProfileId → JobProfile → JobProfileCompensation.SalaryTabulatorLineId → SalaryTabulatorLine.{MinAmount,MaxAmount,CurrencyCode}`. Join ya usado en `JobProfileRepository`. | Resolver `[Min,Max]` para el **bloqueo** del salario negociado (R-3). |
| 8 | Permisos | `PersonnelFilePermissionCodes` (`Common/PersonnelFileCommon.cs:82`: Read/Admin/AuthorizeRehire/ManageAdministration); `PersonnelFilePolicies` (Read/Manage); políticas en `Program.cs:429`; semilla `ProvisioningConstants.cs:70`; atributo `AuthorizationPolicySet` → `AuthorizationPolicyConvention` (GET→Read, escritura→Manage). **No existe** patrón de autoservicio por propiedad del expediente. | Agregar `ViewCompensation` (espejo de `AuthorizeRehire`) + **chequeo de autoservicio nuevo** (§3.7). |
| 9 | Tabulador | `SalaryTabulatorLine` (`Domain/SalaryTabulator/`, **tenant-scoped**, `Min/Max/Base`, vigencia, versión). | Plantilla para la tabla de Renta (admin tenant-scoped, editable — D-19). |
| 10 | CQRS + base | `ICommand<T>`/handlers auto-registrados por reflexión; `PersonnelFileEmployeeCommandHandlerBase.LoadForManageAsync<T>`; `IUnitOfWork.BeginTransactionAsync` + `IAuditService`. | Seguir el patrón en los handlers de conceptos. |
| 11 | Localización | `Error(code,msg,ErrorType)` en `*Errors.cs`; `BackendMessages.resx`/`.es.resx`(/`.es-SV.resx`); test `BackendMessageLocalizationTests` (EN+ES obligatorio). | Todo error nuevo bilingüe o falla el test. |
| 12 | Dinero | Sin `Money` VO; `decimal`+`CurrencyCode(40)`+`numeric(18,2)`. Catálogo `currencies`. | Montos `numeric(18,2)`; **porcentajes `numeric(11,8)`** (D-15). |

## 3. Arquitectura de la solución

### 3.1 Dominio — entidad unificada `PersonnelFileCompensationConcept`

Nuevo archivo `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileCompensation.cs` (se elimina `PersonnelFileSalaryItem` de `PersonnelFileEmployee.cs`). Colección hija del expediente, patrón idéntico a `PersonnelFileEmploymentAssignment` (`BindToPersonnelFile`, `Create`, `Update`, `SetActive`, `ConcurrencyToken`):

```csharp
public sealed class PersonnelFileCompensationConcept : TenantEntity
{
    public long PersonnelFileId { get; private set; }
    public Guid? AssignedPositionPublicId { get; private set; }   // null = nivel empleado; valor = nivel plaza (D-03)
    public CompensationNature Nature { get; private set; }         // INGRESO | EGRESO
    public string ConceptTypeCode { get; private set; }           // catálogo (por nature)
    public DeductionClass? DeductionClass { get; private set; }    // LEY|INTERNO|EXTERNO (editable por instancia, D-06) — solo EGRESO
    public CompensationCalculationType CalculationType { get; private set; } // FIXED | PERCENTAGE
    public decimal Value { get; private set; }                    // monto (18,2) o % (11,8). value >= 0
    public string? CalculationBaseCode { get; private set; }      // requerido si PERCENTAGE (D-05)
    public decimal? EmployerRate { get; private set; }            // carga patronal (ISSS/AFP, 11,8) (D-13)
    public decimal? ContributionCap { get; private set; }         // tope/base máxima (18,2)
    public string CurrencyCode { get; private set; }              // multi-moneda sin conversión (D-12)
    public string PayPeriodCode { get; private set; }             // MENSUAL|QUINCENAL|SEMANAL|UNICA (D-09)
    public string? CounterpartyName { get; private set; }         // egreso externo (D-09)
    public string? ExternalReference { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSystemSuggested { get; private set; }           // ISSS/AFP autogenerados (D-20)
    public string? Notes { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    // Create / Update / SetActive / BindToPersonnelFile ...
}
```

Enums nuevos en `Domain/PersonnelFiles/` (o `Domain/Common/`): `CompensationNature { Ingreso, Egreso }`, `CompensationCalculationType { Fixed, Percentage }`, `DeductionClass { Ley, Interno, Externo }`.

**EF config** `PersonnelFileCompensationConceptConfiguration` (en `…/Configurations/PersonnelFiles/PersonnelFileCompensationConfiguration.cs`), tabla `personnel_file_compensation_concepts`:
- `value` `numeric(18,2)`; `employer_rate` `numeric(11,8)`; `contribution_cap` `numeric(18,2)`; checks `value >= 0`, `end_date is null or end_date >= start_date`.
- FK `personnel_file_id` → `personnel_files` (Cascade), espejo de `PersonnelFileEmploymentAssignmentConfiguration`.
- Índices `uq_…__public_id`, `ix_…__tenant_file_nature_active` `(TenantId, PersonnelFileId, Nature, IsActive)`, `ix_…__tenant_assigned_position` `(TenantId, AssignedPositionPublicId)`.

> **Nota de precisión (D-15):** el `value` cumple doble función. Para `FIXED` es dinero (`numeric(18,2)`); para `PERCENTAGE` es porcentaje con **8 decimales**. Para no perder precisión del %, se mapea `value` como `numeric(18,8)` (cubre montos y % a 8 decimales) **o** se separa en `fixed_amount numeric(18,2)` + `percentage numeric(11,8)`. **Recomendado:** una sola columna `value numeric(18,8)` + validación por `CalculationType` (más simple para el unificado). A decidir en PR (§9).

### 3.2 Catálogos

**(a) Enriquecido — `CompensationConceptTypeCatalogItem`** en `Domain/GeneralCatalogs/GeneralCatalogItems.cs`, derivado de `GeneralCatalogItem`, **con columnas extra** (patrón `BankCatalogItem`):

```csharp
public sealed class CompensationConceptTypeCatalogItem : GeneralCatalogItem
{
    public CompensationNature Nature { get; private set; }
    public bool IsStatutory { get; private set; }
    public DeductionClass? DefaultDeductionClass { get; private set; }
    public CompensationCalculationType DefaultCalculationType { get; private set; }
    public string? DefaultCalculationBaseCode { get; private set; }
    public decimal? DefaultEmployeeRate { get; private set; }   // numeric(11,8)
    public decimal? DefaultEmployerRate { get; private set; }   // numeric(11,8)
    public decimal? ContributionCap { get; private set; }       // numeric(18,2)
    public static CompensationConceptTypeCatalogItem Create(...);
}
```
EF: `CompensationConceptTypeCatalogItemConfiguration : GeneralCatalogItemConfigurationBase<…>` con columnas extra (`nature`, `is_statutory`, `default_deduction_class`, `default_calculation_type`, `default_calculation_base_code`, `default_employee_rate`, `default_employer_rate`, `contribution_cap`).

**(b) Simples — `PayPeriodCatalogItem`, `CalculationBaseCatalogItem`** (clones de `AssignmentTypeCatalogItem`, sin columnas extra), con sus `*Configuration : GeneralCatalogItemConfigurationBase<…>`.

**(c) Wiring (los tres):**
- `PersonnelCurriculumCatalogCategories`: `CompensationConceptType = "CompensationConceptType"`, `PayPeriod = "PayPeriod"`, `CalculationBase = "CalculationBase"`.
- `GeneralCatalogKeyMap.CatalogKeys`: `["compensation-concept-types"]`, `["pay-periods"]`, `["calculation-bases"]`. *(El guardrail `GeneralCatalogKeyMapGuardrailsTests` exige la biyección — agregar o falla.)*
- `PersonnelFileRepository.CatalogCodeIsActiveAsync` (`:1359`): tres casos nuevos en el `switch` → `IsCountryScopedCatalogCodeActiveAsync<T>()`.
- `DevSeedService.SeedGeneralCatalogItems`: seed SV (ver §3.6 para valores ISSS/AFP/Renta). Ingresos, egresos (ley/interno/externo), periodicidades, bases.
- `ApplicationDbContext`: `DbSet<>` para los tres.

**(d) Lectura enriquecida (endpoint dedicado).** La respuesta genérica `PersonnelCatalogItemResponse` no transporta los atributos. Se añade:
- Query `GetCompensationConceptTypesQuery(countryCode, nature?)` → `IReadOnlyCollection<CompensationConceptTypeResponse>` (con todos los atributos enriquecidos), handler que lee `CompensationConceptTypeCatalogItems`.
- Endpoint `GET /api/v1/compensation-concept-types?countryCode=SV&nature=EGRESO`.
- `pay-periods` y `calculation-bases` usan el **endpoint genérico** existente (`GET /api/v1/general-catalogs/{key}`) sin cambios.

### 3.3 Feature CQRS `CompensationConcepts` (espejo de `EmploymentAssignments`)

Tres archivos nuevos en `src/CLARIHR.Application/Features/PersonnelFiles/Compensation/`:

- **`CompensationConcepts.cs`** — `CompensationConceptInput`, `PersonnelFileCompensationConceptResponse`, comandos `Add/Update/Patch/Delete`, queries `Get(s)/ById`, validadores FluentValidation (`CompensationConceptInputValidator`: `value >= 0`; si `PERCENTAGE` → `value` 0–100 con escala 8 y `CalculationBaseCode` no vacío; `ConceptTypeCode/CurrencyCode/PayPeriodCode` no vacíos), y `PersonnelFileCompensationConceptPatchState`.
- **`CompensationConcepts.Rules.cs`** — módulo **puro** `CompensationConceptRules.Evaluate(candidate, existing, slotSalaryRange?)` + `CompensationConceptErrors`. Reglas (testeables sin BD):
  - `PERCENTAGE` ⇒ `CalculationBaseCode` requerido (`CALCULATION_BASE_REQUIRED`).
  - `%` fuera de 0–100 (`PERCENTAGE_OUT_OF_RANGE`).
  - `Nature=EGRESO` ⇒ `DeductionClass` requerido (default del tipo si no viene).
  - **Salario** (`ConceptTypeCode=SALARIO_BASE`, `INGRESO`): único activo por plaza (`BASE_SALARY_ALREADY_ACTIVE`); **bloqueo** si fuera de `[Min,Max]` (`SALARY_OUT_OF_PROFILE_RANGE`) usando `slotSalaryRange` (§3.4).
- **`CompensationConcepts.Handlers.cs`** — handlers `Add/Update/Patch/Delete/Get(s)` (patrón `PersonnelFileEmployeeCommandHandlerBase.LoadForManageAsync`, transacción + `IAuditService`), `CompensationConceptCommandSupport.ValidateAsync` (carga conceptos, resuelve rango de plaza, llama a las reglas), y `PersonnelFileCompensationConceptPatchApplier` (propiedades patchables, incl. `isActive`).

**Repositorio** `IPersonnelFileEmployeeRepository` (+ impl `PersonnelFileEmployeeRepository.cs`): agregar `AddCompensationConceptAsync`, `Update…`, `Patch…`, `Delete…`, `GetCompensationConceptsAsync(personnelFileId)`, `GetCompensationConceptAsync(personnelFileId, publicId)`. **Eliminar** los 6 métodos `*SalaryItem*`.

**Controller** `PersonnelFileCompensationController`: **reemplazar** las 6 acciones `…/salary-items` por `…/compensation-concepts` (mismas convenciones: `201`+`ETag`, `If-Match` en PUT/PATCH/DELETE, JSON-Patch). Contratos en `PersonnelFileRequests.cs`: `Add/Update/PatchCompensationConceptRequest` (reemplazan `*SalaryItemRequest`).

### 3.4 Salario en 3 niveles y bloqueo de rango (RF-002 / R-3)

- **Nivel 2 (plaza):** `PositionSlot.configuredBaseSalary` (`decimal?`) + `currencyCode` (`string?`). Modificar `PositionSlot.Create/UpdateCore`, su config (`position_slots`), `Create/UpdatePositionSlotCommand` + request DTOs + handler. Valor de referencia/presupuesto (no bloqueante por sí mismo).
- **Nivel 1 (rango, bloqueante):** nuevo método repo `IPositionSlotRepository.GetSalaryRangeAsync(Guid positionSlotPublicId, CancellationToken) → SalaryRange?` que hace el join `PositionSlot → JobProfileCompensation → SalaryTabulatorLine` y devuelve `(MinAmount, MaxAmount, CurrencyCode)` de la línea **activa/vigente**.
- **Resolución de la plaza del concepto:** un concepto `SALARIO_BASE` trae `AssignedPositionPublicId` (la `PersonnelFileEmploymentAssignment`); el handler resuelve su `PositionSlotPublicId` y de ahí el rango. Si el negociado **supera** `MaxAmount` (o cae fuera de `[Min,Max]`), las reglas devuelven `COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE` (**422, bloqueo**, R-3).

> Si la plaza no tiene línea de tabulador asociada, el rango es `null` ⇒ no se bloquea (no hay banda definida). Documentar este caso (§9).

### 3.5 Auto-sugerencia de ISSS/AFP al crear plaza (D-20 / R-4)

Extender `AddPersonnelFileEmploymentAssignmentCommandHandler` (`Employment/EmploymentAssignments.Handlers.cs`): tras crear la asignación (plaza), **dentro de la misma transacción**, crear conceptos `EGRESO` **sugeridos** `ISSS` y `AFP` (`IsSystemSuggested=true`, `DeductionClass=LEY`, `CalculationType` y rates desde `CompensationConceptTypeCatalogItem.Default*`), con `AssignedPositionPublicId = nuevaPlaza.PublicId`. **No bloquea** y son **editables/eliminables**. 
- Idempotencia: solo sugerir si la plaza no tiene ya un `ISSS`/`AFP` activo (evitar duplicados al reactivar).
- Implementar como helper `CompensationConceptSuggestionService.SuggestStatutoryForAssignmentAsync(personnelFile, assignment, ct)` reutilizable (también por el flujo de recontratación si abre nuevas plazas).

### 3.6 Tabla de Renta (ISR) configurable (RF-009 / D-14)

Entidad **tenant-scoped** (espejo de `SalaryTabulatorLine`, editable — D-19): `src/CLARIHR.Domain/Compensation/IncomeTaxWithholdingBracket.cs`:

```csharp
public sealed class IncomeTaxWithholdingBracket : TenantEntity
{
    public string PayPeriodCode { get; private set; }   // MENSUAL|QUINCENAL|SEMANAL
    public int BracketOrder { get; private set; }
    public decimal LowerBound { get; private set; }      // 18,2
    public decimal? UpperBound { get; private set; }     // null en el último tramo
    public decimal FixedFee { get; private set; }        // 18,2
    public decimal RatePercent { get; private set; }     // 11,8
    public decimal ExcessOver { get; private set; }      // 18,2
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public bool IsActive { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
}
```
- Feature admin `Features/Compensation/IncomeTaxBracketAdministration.cs` (CRUD + query por período) + controller `IncomeTaxBracketsController` (`api/v1/income-tax-brackets`), gobernado por el permiso de administración de catálogos. EF config + `DbSet<>` + tabla `income_tax_withholding_brackets`.
- **Seed SV (mensual) — editable (D-19):** I `$0.01–$472.00` exento; II `$472.01–$895.24` 10% + `$17.67` sobre `$472.00`; III `$895.25–$2,038.10` 20% + `$60.00` sobre `$895.24`; IV `$2,038.11+` 30% + `$288.57` sobre `$2,038.10`. **Cargar también** las tablas **quincenal** y **semanal** (misma estructura, montos oficiales por período — R-6).
- **No se calcula aquí** (D-08): la tabla es insumo del módulo de nómina futuro.

### 3.7 Permiso `ViewCompensation` + visibilidad/autoservicio (RF-014 / D-16 / R-5)

- **Permiso:** `PersonnelFilePermissionCodes.ViewCompensation = "PersonnelFiles.ViewCompensation"` (`Common/PersonnelFileCommon.cs`); semilla owner en `ProvisioningConstants.cs` (espejo de `AuthorizeRehire`); política `PersonnelFilePolicies.ViewCompensation` en `Program.cs` (`HasAnyPermission(ViewCompensation, Admin, ManageAdministration)`).
- **Lectura de conceptos:** las acciones `GET …/compensation-concepts` usan la política `ViewCompensation` (en vez de `Read`). Como `AuthorizationPolicySet` aplica una sola `ReadPolicy` por controller, **separar** los GET de compensación en un controller dedicado `PersonnelFileCompensationConceptsController` con `[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompensation, PersonnelFilePolicies.Manage)]`, o aplicar `[Authorize(PersonnelFilePolicies.ViewCompensation)]` por acción (la convención respeta el override por método).
- **Autoservicio (nuevo — no existe patrón hoy):** el empleado puede ver **su propia** compensación. En los handlers de query, resolver el `PersonnelFile` ligado al usuario llamante (vía su `LinkedUserPublicId`/claim) y permitir si coincide con el `publicId` solicitado; si no coincide, exigir `ViewCompensation`. Encapsular en `ICompensationReadAuthorization.CanReadAsync(callerUserId, personnelFilePublicId, ct)`. **Sin** workflow de aprobación (D-16).

### 3.8 Rename `employment-assignments` → `assigned-positions` (D-10 / R / breaking)

- **Wire (obligatorio):** `PersonnelFileEmploymentController.cs` (6 rutas) `…/employment-assignments` → `…/assigned-positions`. Actualizar tests de integración: `ApiIntegrationTests.EmploymentAssignments.cs` (14 usos) y `ApiIntegrationTests.Rehire.cs` (1 uso).
- **Contratos API (recomendado):** renombrar los request DTOs `*EmploymentAssignmentRequest` → `*AssignedPositionRequest` (superficie pública).
- **Tipos C# internos (opcional):** comandos/handlers/entidad `*EmploymentAssignment*` pueden conservarse (rename cosmético diferible); el **negocio exige el cambio de ruta/labels**, no necesariamente el rename de clases internas. Decisión de alcance en §9.
- Sin compatibilidad hacia atrás (el FE ajusta — D-10). Documentar en la **guía de integración Frontend**.

### 3.9 Eliminación de `SalaryItem` (D-11 / drop & recreate)

Eliminar (sin preservar datos): `Compensation/SalaryItems{,.Handlers}.cs`; contratos `*SalaryItemRequest`; las 6 acciones `…/salary-items` del controller; `PersonnelFileSalaryItem` (de `PersonnelFileEmployee.cs`) + su config (`personnel_file_salary_items`); 6 métodos `*SalaryItem*` del repo (interfaz+impl); tests `PersonnelFileSalaryItemPatchTests.cs` + el bloque de `salary-items` en `ApiIntegrationTests.cs`. La migración **dropa** la tabla.

### 3.10 Errores + localización (RF-016)

Nuevo `Features/PersonnelFiles/Common/CompensationErrors.cs`: `COMPENSATION_CONCEPT_TYPE_CODE_INVALID`, `…_CALCULATION_BASE_REQUIRED`, `…_PERCENTAGE_OUT_OF_RANGE`, `…_CURRENCY_INVALID`, `…_PAY_PERIOD_INVALID`, `COMPENSATION_BASE_SALARY_ALREADY_ACTIVE`, `COMPENSATION_CONCEPT_ASSIGNED_POSITION_NOT_FOUND`, `COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE`. Cada código en `BackendMessages.resx` (EN) + `.es.resx` (ES) o **falla `BackendMessageLocalizationTests`** (es-SV opcional, como en multi-plaza).

### 3.11 Persistencia / migraciones

Migraciones cohesivas (comando `dotnet ef migrations add <Name> --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj`):
1. `AddCompensationCatalogs` — `compensation_concept_type_catalog_items` (enriquecido), `pay_period_catalog_items`, `calculation_base_catalog_items`.
2. `ReplaceSalaryItemsWithCompensationConcepts` — **drop** `personnel_file_salary_items`; **create** `personnel_file_compensation_concepts`.
3. `AddPositionSlotConfiguredBaseSalary` — columnas `configured_base_salary`, `currency_code` en `position_slots`.
4. `AddIncomeTaxWithholdingBrackets` — `income_tax_withholding_brackets`.

## 4. Diagramas de flujo

**A — Crear plaza con auto-sugerencia (D-20):**
```
POST /personnel-files/{id}/assigned-positions   (plaza)
   │  LoadForManage (tenant + PersonnelFiles.Manage)
   ▼  ── BEGIN TX ──
 [1] EmploymentAssignmentRules.Evaluate (cupo/primaria/solape)  → crea la plaza
 [2] SuggestStatutoryForAssignment → crea EGRESO ISSS + AFP (IsSystemSuggested, LEY, rates del catálogo)
     SaveChanges + Audit
   ▼  ── COMMIT ──  201 + ETag
```

**B — Registrar salario negociado (bloqueo de rango, R-3):**
```
POST /personnel-files/{id}/compensation-concepts  { nature:INGRESO, conceptTypeCode:SALARIO_BASE,
                                                     assignedPositionPublicId, calculationType:FIXED, value:700 }
   │  LoadForManage
   ▼
 [1] resolver plaza → PositionSlot → JobProfileCompensation → SalaryTabulatorLine [Min,Max]
 [2] CompensationConceptRules.Evaluate:
        - único SALARIO_BASE activo por plaza
        - value ∈ [Min,Max] ?  ── no → 422 COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE (BLOQUEA)
   ▼ (ok)  guarda concepto + Audit → 201 + ETag
```

**C — Egreso porcentual de ley (ISSS 3%):**
```
PUT/PATCH …/compensation-concepts/{id}  { nature:EGRESO, conceptTypeCode:ISSS, deductionClass:LEY,
                                          calculationType:PERCENTAGE, value:3, calculationBaseCode:IBC,
                                          employerRate:7.5, contributionCap:1000, payPeriodCode:MENSUAL }
   │  CompensationConceptRules: PERCENTAGE ⇒ base requerida; % ∈ [0,100]; EGRESO ⇒ deductionClass
   ▼  guarda  → 200 + ETag   (el CÁLCULO del 3% es de nómina — D-08)
```

## 5. Archivos a crear / modificar

| # | Archivo | Acción |
|---|---|---|
| 1 | `Domain/PersonnelFiles/PersonnelFileCompensation.cs` | **New**: `PersonnelFileCompensationConcept` + enums (`CompensationNature`, `CompensationCalculationType`, `DeductionClass`). |
| 2 | `Domain/PersonnelFiles/PersonnelFileEmployee.cs` | **Mod**: **eliminar** `PersonnelFileSalaryItem`. |
| 3 | `Domain/GeneralCatalogs/GeneralCatalogItems.cs` | **Mod**: `CompensationConceptTypeCatalogItem` (enriquecido), `PayPeriodCatalogItem`, `CalculationBaseCatalogItem`. |
| 4 | `Domain/PositionSlots/PositionSlot.cs` | **Mod**: `ConfiguredBaseSalary`, `CurrencyCode` + `Create`/`UpdateCore`. |
| 5 | `Domain/Compensation/IncomeTaxWithholdingBracket.cs` | **New**: tabla de Renta (tenant-scoped). |
| 6 | `Infrastructure/Persistence/Configurations/PersonnelFiles/PersonnelFileCompensationConfiguration.cs` | **New**: config del concepto (drop de `PersonnelFileSalaryItemConfiguration`). |
| 7 | `…/Configurations/GeneralCatalogs/GeneralCatalogItemConfiguration.cs` | **Mod**: configs de los 3 catálogos (enriquecido + 2 simples). |
| 8 | `…/Configurations/PositionSlots/PositionSlotConfiguration.cs` | **Mod**: columnas `configured_base_salary`, `currency_code`. |
| 9 | `…/Configurations/Compensation/IncomeTaxWithholdingBracketConfiguration.cs` | **New**. |
| 10 | `Infrastructure/Persistence/ApplicationDbContext.cs` | **Mod**: `DbSet<>` de las 4 entidades nuevas; quitar `PersonnelFileSalaryItem` si tuviera DbSet. |
| 11 | `Infrastructure/Persistence/Migrations/` | **New**: 4 migraciones (§3.11). |
| 12 | `Infrastructure/Persistence/DevSeedService.cs` | **Mod**: seed SV de catálogos de compensación + tabla de Renta (3 períodos). |
| 13 | `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs` | **Mod**: 3 casos nuevos en `CatalogCodeIsActiveAsync`; query enriquecida de tipos. |
| 14 | `Infrastructure/PersonnelFiles/PersonnelFileEmployeeRepository.cs` | **Mod**: métodos CRUD de conceptos; **eliminar** los de SalaryItem. |
| 15 | `Infrastructure/PositionSlots/PositionSlotRepository.cs` | **Mod**: `GetSalaryRangeAsync` (join a tabulador). |
| 16 | `Application/Abstractions/PersonnelFiles/IPersonnelFileEmployeeRepository.cs` | **Mod**: firmas de conceptos; quitar SalaryItem. |
| 17 | `Application/Features/PersonnelFiles/Compensation/CompensationConcepts{,.Rules,.Handlers}.cs` | **New**: feature CQRS + reglas puras. |
| 18 | `Application/Features/PersonnelFiles/Compensation/SalaryItems{,.Handlers}.cs` | **Del**. |
| 19 | `Application/Features/PersonnelFiles/Compensation/CompensationConceptSuggestionService.cs` | **New**: auto-sugerencia ISSS/AFP (D-20). |
| 20 | `Application/Features/PersonnelFiles/Employment/EmploymentAssignments.Handlers.cs` | **Mod**: invocar la sugerencia al crear plaza. |
| 21 | `Application/Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` + `GeneralCatalogKeyMap.cs` | **Mod**: 3 categorías + 3 wire-keys. |
| 22 | `Application/Features/Compensation/IncomeTaxBracketAdministration.cs` | **New**: CRUD/query de la tabla de Renta. |
| 23 | `Application/Features/PersonnelFiles/Common/PersonnelFileCommon.cs` | **Mod**: `ViewCompensation` permission. |
| 24 | `Application/Features/PersonnelFiles/Common/PersonnelFilePolicies.cs` | **Mod**: `ViewCompensation` policy. |
| 25 | `Application/Features/Provisioning/Common/ProvisioningConstants.cs` | **Mod**: semilla owner de `ViewCompensation`. |
| 26 | `Application/Features/PersonnelFiles/Common/CompensationErrors.cs` | **New**: errores. |
| 27 | `Application/.../Compensation/CompensationReadAuthorization.cs` | **New**: autoservicio del empleado (§3.7). |
| 28 | `Api/Program.cs` | **Mod**: política `ViewCompensation`. |
| 29 | `Api/Controllers/PersonnelFileCompensationController.cs` | **Mod**: rutas `…/compensation-concepts` (quitar `…/salary-items`); GET con `ViewCompensation`. |
| 30 | `Api/Controllers/PersonnelFileEmploymentController.cs` | **Mod**: rutas `…/employment-assignments` → `…/assigned-positions`. |
| 31 | `Api/Controllers/PositionSlotsController.cs` | **Mod**: campos `configuredBaseSalary`/`currencyCode`. |
| 32 | `Api/Controllers/CompensationConceptTypesController.cs` + `IncomeTaxBracketsController.cs` | **New**: lectura enriquecida + admin Renta. |
| 33 | `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs` | **Mod**: `*CompensationConceptRequest`; quitar `*SalaryItemRequest`; rename `*AssignedPositionRequest`. |
| 34 | `Infrastructure/Localization/BackendMessages{,.es,.es-SV}.resx` | **Mod**: EN/ES de los errores nuevos. |
| 35 | `tests/CLARIHR.Application.UnitTests/CompensationConceptRulesTests.cs` | **New**: reglas puras. |
| 36 | `tests/CLARIHR.Api.IntegrationTests/ApiIntegrationTests.CompensationConcepts.cs` | **New**: round-trip; rename de assigned-positions; drop de salary-items. |

## 6. Plan por fases (incremental, cada fase desplegable y verificable)

- **Fase 0 — Catálogos + dominio.** Items #1, #3, #6–#7, #10–#12 (catálogos), #21, #23–#25. Catálogos enriquecidos + simples seedeados, permiso, enums. *Salida:* migración de catálogos + lectura enriquecida verde, guardrail de key-map verde.
- **Fase 1 — Concepto + feature CQRS (sin salario/sugerencia).** Items #1, #6, #14, #16, #17, #26, #29, #33, #35. Drop de SalaryItem (#2, #18). CRUD de conceptos genérico (ingresos/egresos fijo/%). *Salida:* `/compensation-concepts` funcional + reglas puras testeadas.
- **Fase 2 — Salario en 3 niveles + bloqueo de rango.** Items #4, #8, #15, #31, #33 (PositionSlot), reglas de salario en #17. *Salida:* `configuredBaseSalary` + bloqueo `[Min,Max]` (R-3).
- **Fase 3 — Auto-sugerencia ISSS/AFP.** Items #19, #20. *Salida:* crear plaza propone ISSS/AFP (D-20), idempotente.
- **Fase 4 — Tabla de Renta.** Items #5, #9, #22, #32 (Renta). *Salida:* admin CRUD + seed 3 períodos.
- **Fase 5 — Visibilidad + rename + E2E.** Items #27, #28, #30 (rename), #32 (tipos), #34, #36; autoservicio. Round-trip de integración, paridad de localización, **guía de integración Frontend**.

## 7. Estrategia de pruebas

- **Reglas puras** (`CompensationConceptRulesTests`, patrón `EmploymentAssignmentRulesTests`): `%` sin base → `CALCULATION_BASE_REQUIRED`; `%` 150 → `PERCENTAGE_OUT_OF_RANGE`; `EGRESO` sin `deductionClass` → default del tipo; 2.º `SALARIO_BASE` activo → `BASE_SALARY_ALREADY_ACTIVE`; negociado $1200 con rango [500,1000] → `SALARY_OUT_OF_PROFILE_RANGE`; negociado $700 → OK.
- **Handlers** (fakes de repos): add/update/patch/delete de conceptos; resolución de rango; auto-sugerencia ISSS/AFP al crear plaza (idempotente); autoservicio (empleado lee su ficha, no la de otro).
- **Integración** (`ApiIntegrationTests.CompensationConcepts.cs`, Postgres :5432 — ver [[running-integration-tests-without-docker]]): crear plaza → asertar ISSS/AFP sugeridos; salario fuera de rango → 422; egreso % de ley; `/salary-items` **ya no existe** (404); `/assigned-positions` responde (rename); `compensation-concept-types` enriquecido; Renta CRUD.
- **Paridad de localización** (`BackendMessageLocalizationTests`): todos los `COMPENSATION_*` con EN+ES.
- **Guardrail de catálogos** (`GeneralCatalogKeyMapGuardrailsTests`): biyección con las 3 categorías nuevas.

## 8. Verificación (comandos)

```bash
dotnet build CLARIHR.slnx

# Migraciones (4)
dotnet ef migrations add AddCompensationCatalogs            --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
dotnet ef migrations add ReplaceSalaryItemsWithCompensationConcepts --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
dotnet ef migrations add AddPositionSlotConfiguredBaseSalary --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
dotnet ef migrations add AddIncomeTaxWithholdingBrackets    --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj

# Unit + paridad + guardrail
dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj

# Integración (Postgres :5432; CLARIHR_INTEGRATION_TEST_CONNECTION_STRING si Docker está abajo)
dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Compensation"
```

## 9. Riesgos y decisiones técnicas a resolver en diseño/PR

1. **Forma del `value` (medio):** una columna `numeric(18,8)` unificada vs. `fixed_amount(18,2)` + `percentage(11,8)` separados. **Recomendado:** columna única + validación por `CalculationType` (más simple para el modelo unificado, D-15). Confirmar serialización del % a 8 decimales en el contrato.
2. **Autoservicio del empleado (alto):** **no existe** patrón de autorización por propiedad del expediente. Hay que implementar `CompensationReadAuthorization` (resolver el `PersonnelFile` del usuario llamante y comparar). Definir cómo se obtiene el expediente del caller (claim/`LinkedUserPublicId`).
3. **Lectura con `ViewCompensation` (medio):** `AuthorizationPolicySet` aplica una sola `ReadPolicy` por controller. Decidir: **controller dedicado** para los GET de compensación o `[Authorize]` por acción. Recomendado: controller dedicado `PersonnelFileCompensationConceptsController`.
4. **Scope de la tabla de Renta (medio):** tenant-scoped (como `SalaryTabulator`, editable por empresa) vs. country-scoped (ley nacional). **Recomendado:** tenant-scoped seedeado de los defaults SV (editable — D-19); evita acoplar tenants.
5. **Plaza sin línea de tabulador (medio):** si la plaza no resuelve `[Min,Max]`, el bloqueo de rango no aplica (no hay banda). Confirmar comportamiento: ¿permitir libre o exigir banda? (Recomendado: permitir, registrar advertencia de datos).
6. **Egresos de ley por plaza + topes (medio):** el tope (p. ej. ISSS $1,000) se **guarda** por concepto pero **se aplica** en nómina; con multi-plaza el tope por persona es responsabilidad del **cálculo** (D-18/D-08). Documentar que aquí no se consolida.
7. **Alcance del rename (bajo):** confirmar si se renombran solo rutas+contratos (recomendado) o también los tipos C# `*EmploymentAssignment*` (cosmético, diferible).
8. **Reutilización de la sugerencia en recontratación (bajo):** `CompensationConceptSuggestionService` debería invocarse también cuando la recontratación abre nuevas plazas (coherencia con [[recontratacion-empleados-implementation]]).
9. **ADR sugerido:** registrar "modelo unificado de compensación + salario 3 niveles con bloqueo de rango" como ADR (`docs/technical/adr/`).

## 10. Trazabilidad: decisión de negocio → componente técnico

| Decisión | Implementación |
|---|---|
| D-01 modelo unificado | `PersonnelFileCompensationConcept` (`Nature` INGRESO/EGRESO) (§3.1) |
| D-02/R-2/R-3 salario 3 niveles | `PositionSlot.configuredBaseSalary` (nivel 2) + `SALARIO_BASE` por plaza (nivel 3) + **bloqueo** contra tabulador `[Min,Max]` (§3.4) |
| D-03 cardinalidad/ámbito | `AssignedPositionPublicId` nullable; ≥1 vía `SALARIO_BASE` + ISSS/AFP sugeridos (§3.1, §3.5) |
| D-04/D-05/D-06 fijo-%-clasificación | `CalculationType`, `CalculationBaseCode`, `DeductionClass` editable (§3.1, §3.3) |
| D-07 catálogos enriquecidos | `CompensationConceptTypeCatalogItem` + endpoint dedicado (§3.2) |
| D-08 solo configuración | Sin motor de cálculo; reglas validan, no resuelven montos (§3.3, §3.6) |
| D-09 externos/período | `CounterpartyName/ExternalReference` + `PayPeriodCode` (§3.1) |
| D-10/R rename | `assigned-positions` + contratos (§3.8) |
| D-11 drop&recreate | Migración que dropa `personnel_file_salary_items` (§3.9, §3.11) |
| D-12 multi-moneda | `CurrencyCode` por concepto, sin conversión (§3.1) |
| D-13/D-18/R-4 ISSS/AFP | `EmployerRate`+`ContributionCap` por plaza + auto-sugerencia (§3.1, §3.5) |
| D-14/R-6 Renta | `IncomeTaxWithholdingBracket` (3 períodos), admin CRUD (§3.6) |
| D-15 precisión | `value`/rates con escala 8; montos `numeric(18,2)` (§3.1) |
| D-16/R-5 visibilidad | `ViewCompensation` + autoservicio (§3.7) |
| D-17 ingresos recurrentes | Tipos de ingreso en catálogo (aguinaldo/horas extra/viáticos) (§3.2) |
| D-19 valores editables | Catálogos + tabla de Renta editables; seeds = defaults (§3.2, §3.6) |
| D-20 sugerencia | `CompensationConceptSuggestionService` en el Add de plaza (§3.5) |

---

> **Nota.** Plan basado en el código a 2026-06-21. Símbolos clave verificados: `PersonnelFileEmploymentAssignment`/`EmploymentAssignmentRules` (plantilla), `PersonnelFileSalaryItem` (a eliminar), `CountryScopedCatalogItem`/`BankCatalogItem` (catálogo enriquecido), `GeneralCatalogKeyMap`/`GeneralCatalogKeyMapGuardrailsTests`, `PersonnelFileRepository.CatalogCodeIsActiveAsync`, `PositionSlot` + cadena `JobProfileCompensation→SalaryTabulatorLine.{Min,Max}`, `PersonnelFilePermissionCodes`/`PersonnelFilePolicies`/`ProvisioningConstants`/`AuthorizationPolicyConvention`, `ApplicationDbContext.ApplyConfigurationsFromAssembly`, `BackendMessageLocalizationTests`. Antes de implementar, resolver los 9 puntos de §9. Documento de negocio: [`analisis-plazas-ingresos-egresos.md`](../business/analisis-plazas-ingresos-egresos.md).
