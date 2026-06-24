# Plan Técnico de Implementación — Competencias del Puesto (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-competencias-puesto-empleado.md`](../business/analisis-competencias-puesto-empleado.md) (decisiones D-01…D-12) |
| **Módulos** | `PersonnelFiles` (Talento, `PersonnelFilePositionCompetencyResult`) · `CompetencyFramework` (`OccupationalPyramidLevel`, `CompetencyConduct`, `JobProfileCompetencyExpectation`, **nuevo** `CompetencyRatingScale`) · `JobProfiles` (atar a nivel; `JobCatalogItem`) · `PositionSlots` · `EmploymentAssignments` · Provisioning/RBAC · Localización · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-23 |
| **País de referencia** | El Salvador (SV) |

---

## 1. Objetivo y enfoque

**Conectar** los dos subsistemas que hoy existen separados y **endurecer** el registro de evaluación, conforme a **D-01…D-12**. La consulta de los 6 datos ya funciona (`PersonnelFilePositionCompetencyResult` con CRUD/PATCH/concurrencia/auditoría completos); el trabajo **no** es construir el CRUD, es:

1. **Atar el perfil al nivel jerárquico** (prerrequisito R-2): `JobProfile` gana un FK opcional a `OccupationalPyramidLevel` (hoy **no existe**; el nivel solo vive por fila de matriz). Sin esto no se puede "derivar por el nivel del puesto" (D-02).
2. **Escala de calificación configurable** (D-04, prerrequisito R-1): **nueva** entidad `CompetencyRatingScale` (tenant-scoped) que soporta escala **numérica** (0–100, 1–5, 0–10) **y discreta** (A–F, niveles con valor ordinal). Necesaria porque `JobCatalogItem` **no tiene** ningún campo numérico (verificado) y el negocio pidió explícitamente 0–100 **y** A–F.
3. **Valor esperado en la matriz** (D-02): `JobProfileCompetencyExpectation` gana `ExpectedValue` (decimal en la escala), junto al `BehaviorLevelCatalogItemId` cualitativo que ya tiene.
4. **Vincular el resultado al marco** (D-03/D-10/D-12): el resultado deja el `CompetencyCode`/`DesiredBehaviors` de **texto libre** y pasa a **FKs** (`CompetencyCatalogItemId`, `CompetencyTypeCatalogItemId`, `JobProfileCompetencyExpectationId`) + **snapshots** para historial robusto.
5. **Brecha calculada** (D-05): `GapScore = ExpectedScore − AchievedScore` deja de ser entrada manual; se deriva en el dominio (espejo de `DeriveResponseTimeDays` de reclamos).
6. **Fecha obligatoria y no futura** (D-06): `EvaluationDateUtc` pasa de `DateTime?` a `DateTime` (como en `PersonnelFilePerformanceEvaluation`).
7. **Consulta derivada** (D-02, núcleo del requisito): nueva query que recorre **empleado → asignación vigente → plaza → perfil → nivel → matriz** y combina con las notas alcanzadas (LEFT JOIN), calculando la brecha y agrupando por **tipo**.
8. **Historial** (D-07): varias filas por competencia (cada una = una evaluación con su fecha); la **vigente** = la más reciente. Reusa la tabla existente (índice ya **no-único**).
9. **Permiso dedicado + autoservicio** (D-08/D-09): `ViewCompetencies`/`ManageCompetencies` (espejo de `ViewInsurance`/`ManageSubstitutions`) + rama self-service (espejo de compensación). **Gotcha:** `AuthorizationPolicySet` es **class-only** → se **carva un controlador dedicado** (patrón de `PersonnelFileAuthorizationSubstitutionController`), rutas idénticas.
10. **Seed SV** (D-03): `CompetencyType` = `GESTION`/`ORGANIZACIONAL`/`TECNICA` y una escala por defecto, vía **seed service por-tenant** (espejo de `OrgStructureCatalogSeedService`), porque `JobCatalogItem` es **tenant-scoped** (no country-scoped).
11. **Módulo de reglas puro** `PositionCompetencyResults.Rules.cs` (G-10) + localización de errores + auditoría con diff.
12. **Sin migración de datos** (D-11): **drop & recreate** de `personnel_file_position_competency_results`.

Patrones reutilizados (todos verificados): escala/seed por-tenant (`OrgStructureCatalogSeedService`), permisos (`ViewInsurance`/`ManageSubstitutions`), controlador dedicado (`PersonnelFileAuthorizationSubstitutionController`), gate self-service de compensación, reglas puras (`Insurances.Rules.cs`/`EmploymentAssignments.Rules.cs`), join slot→perfil (`PositionSlotRepository.GetSalaryRangeAsync`), recursos `BackendMessages*`. Los **dos** componentes con menos precedente directo son la **escala configurable** (§3.3) y la **consulta derivada** (§3.7).

---

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Entidad resultado | `PersonnelFilePositionCompetencyResult` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:1468`): ctor priv (`:1474`), `Create(:1547)`, `Update(:1524)`, `BindToPersonnelFile(:1522)`. 9 campos: `CompetencyCode`(req), `DesiredBehaviors`, `ExpectedScore`, `AchievedScore`, `GapScore`, `EvaluationDateUtc`(**nullable**), `Source*`×3, `ConcurrencyToken`. **Sin** `IsActive`. | Reestructurar a FKs + fecha obligatoria + gap derivado. |
| 2 | EF config resultado | `PersonnelFilePositionCompetencyResultConfiguration` (`Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs:537-571`): tabla `personnel_file_position_competency_results`; `competency_code(80)`, `desired_behaviors(2000)`, `expected/achieved/gap_score numeric(18,2)`, `evaluation_date_utc`; **única FK** → `personnel_files` (Cascade); UQ `public_id`; índice **no-único** `(tenant,file,competency_code)`. **Sin** check constraints ni FK al marco. | +FKs marco/escala; date NOT NULL; índices nuevos. |
| 3 | Aplicación | `Talent/PositionCompetencyResults.cs`: `Input(:37)`, `Response(:20)`, validador (`:85-149`) = **solo** `CompetencyCode NotEmpty/Max80`; `PatchState(:151)`. `.Handlers.cs`: Add(`:20`)/Update(`:87`)/Patch(`:167`)/Delete(`:266`) usan `LoadForManageAsync`; queries (`:335-401`) usan `LoadForReadAsync`; todos con `IsCompletedEmployee` inline; `GapScore` **passthrough**. | Enriquecer input/response; endurecer validador; gap derivado; swap de gates. |
| 4 | Repositorio | `IPersonnelFileEmployeeRepository(:623-655)`: `Add/Update/Delete/Get/List PositionCompetencyResult` (Update con 9 params planos). Impl `PersonnelFileEmployeeRepository.cs:1422-1489`; `Map(:2015-2027)`. Add/List **ordenan por `CompetencyCode`**. | Ampliar firmas/Map; ordenar por fecha (historial). |
| 5 | API | Controller `PersonnelFileTalentController.cs`: clase `[AuthorizationPolicySet(Read, Manage)](:26)`; 6 endpoints de `position-competency-results` (`:198-365`). DTOs `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:535-568`. | **Carvar** a controlador dedicado (permiso); ampliar DTOs. |
| 6 | `JobProfile` | `Domain/JobProfiles/JobProfile.cs`: FKs `OrgUnitId(:41)`, `ReportsToJobProfileId(:43)`, `PositionCategoryId(:45)`, 3 catalog ids (`:47-51`); `UpdateCore(:93-110)`. **NO** referencia a nivel. Config `JobProfileConfiguration.cs:14`; patrón FK opcional `:146-168`. | **Agregar** `OccupationalPyramidLevelId?` (FK Restrict). |
| 7 | `JobCatalogItem` | `Domain/JobProfiles/JobCatalogItem.cs:27-41`: `Category/Code/Name/IsSystem/IsActive/Token`. **SIN campo numérico** (no value/sortOrder/ordinal). `CompetencyType=10` (`JobProfileEnums.cs:20`). UQ `(tenant,category,code)` (`:288`). Es **TenantEntity**. | La escala **no** cabe en `JobCatalogItem` → entidad nueva. |
| 8 | Matriz | `JobProfileCompetencyExpectation.cs`: `JobProfileId(:64)`, `OccupationalPyramidLevelId(:66)`, competencia/tipo/nivel (`:68/:70/:72`), `ExpectedEvidence(:74)`, `SortOrder(:76)`. **Sin** valor numérico esperado. Config `CompetencyFrameworkConfiguration.cs:221-309` (UQ tuple `:256-266`). Admin `JobProfileCompetencyMatrixAdministration.cs` (Add `:93`, response `:26-47`, `BuildConductsAsync :828`). | **Agregar** `ExpectedValue` (escala) + en admin/validador/patch/export. |
| 9 | Nivel jerárquico | `OccupationalPyramidLevel.cs`: `LevelOrder(:43)` único por tenant; `Create(:51)`. | Reutilizar como nivel del perfil. |
| 10 | Permiso (patrón) | `ViewInsurance`: const `PersonnelFileCommon.cs:113`, policy `PersonnelFilePolicies.cs:34`, `Program.cs` authn-only superset `:444-453`, seed `ProvisioningConstants.cs:68-75`→`OwnerPermissionCatalog.cs:8-43`, iface `IPersonnelFileAuthorizationService.cs:38-45` (default fail-closed), impl `PersonnelFileAuthorizationService.cs:39-49` (`EnsureHasAnyClaimAsync :132-177`, `RbacPermissionAction.Read`). | Espejar `ViewCompetencies`/`ManageCompetencies`. |
| 11 | Gotcha controller | `AuthorizationPolicySet` es **class-only**; **no existe** ningún `[Authorize(Policy=...)]` de método (grep 0). Solución previa: **controlador dedicado** `PersonnelFileAuthorizationSubstitutionController.cs:23-30` (rutas idénticas, "authorization split, not API change"). | Carvar `PersonnelFileCompetencyController`. |
| 12 | Self-service | `PersonnelFileEmployeeHandlerBases.cs`: `LoadCompletedEmployeeForCompensationReadAsync(:361-403)` con rama self (`:385-395`: `LinkedUserPublicId == currentUser`); `LoadForManageAsync(:90-131)`; `LoadForReadAsync(:266-296)`. Audit `PersonnelFileEmployeeAudits(:35-78)` (before-less `:37`, before/after `:60`). | Read-gate perm-o-self; auditoría con diff. |
| 13 | Asignación vigente | `PersonnelFileEmploymentAssignment` (`PersonnelFileEmployee.cs`): `PositionSlotPublicId(:161)`, `StartDate(:169)`, `EndDate(:171)`, `IsPrimary(:173)`, `IsActive(:175)`. Único activo-primario (`EmploymentAssignments.Rules.cs:148-154`). Slot→perfil: `PositionSlot.JobProfileId(:73)`. Join modelo `PositionSlotRepository.GetSalaryRangeAsync(:23-30)`. | Vigente = `IsActive && IsPrimary` → slot → perfil → nivel → matriz. |
| 14 | Seed por-tenant | `JobCatalogItem` **sin seed** (verificado: ni DevSeed, ni HasData, ni migración). Patrón por-tenant: `OrgStructureCatalogSeedService.InitializeDefaultsAsync(tenantId)(:10)`, invocado en `CompanyProvisioningService.ProvisionAsync:150`. País-scoped (no aplica): `20260623042901_SeedEmploymentStatusCatalogForElSalvador`. | Crear `CompetencyFrameworkSeedService` por-tenant. |
| 15 | Reglas (patrón) | `Insurances.Rules.cs` (`*Errors :14-30` + `*Rules :38-131`); `EmploymentAssignments.Rules.cs` (`Evaluate :105-157`). | Crear `PositionCompetencyResults.Rules.cs`. |
| 16 | Tests | `AuthorizationPolicyConventionGovernanceTests.cs` (`PersonnelFilePolicyNames :81-96`), `BackendMessageLocalizationTests.cs(:63-149)`, `OpenApiContractGuardrailsTests.cs` (familia Talent `:64`), `PersonnelFilePositionCompetencyResultPatchTests.cs`. | Actualizar 4 + crear RulesTests. |
| 17 | Migración | Convención `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add … --project …Infrastructure --startup-project …Api` (planes equipo-acceso/reclamos). | 1–3 migraciones (ver §3.11). |

---

## 3. Arquitectura de la solución

### 3.1 Decisión central — modelo de escala y "esperado" (resuelve R-1)

El requerimiento ratificó escala **configurable** que incluye **0–100** (continua) **y** **A–F** (discreta). Como `JobCatalogItem` no tiene campo numérico (#7), se introduce una **entidad de escala** dedicada.

- **Recomendado (comprometido): `CompetencyRatingScale`** tenant-scoped con `ScaleType ∈ {Numeric, Discrete}`:
  - **Numeric** (0–100, 1–5, 0–10): `MinValue`, `MaxValue`, `Decimals`. La nota es el número crudo, validado a `[Min,Max]`.
  - **Discrete** (A–F, Básico/Intermedio/Avanzado): colección `CompetencyRatingScaleLevel { Code, Label, Value, SortOrder }` (p. ej. `A`→5 … `F`→0). La nota almacena el **`Value`** del nivel; la UI muestra el `Label`.
  - Una escala **activa por tenant** (`IsActive` + invariante de única activa). `ExpectedScore`/`AchievedScore`/`GapScore` se calculan sobre `Value` (decimal), por lo que la **brecha es numérica** incluso para A–F.
- **Alternativa ligera (si el negocio NUNCA usa 0–100):** agregar `Value`/`SortOrder` a `JobCatalogItem` y usar la categoría `BehaviorLevel` como escala discreta. **Descartada** por el requisito explícito de 0–100 y por contaminar un catálogo genérico. Se documenta como variante.

> **Esperado desde la matriz.** El "esperado" cualitativo ya existe (`BehaviorLevelCatalogItemId`). Se agrega `ExpectedValue` (decimal en la escala) a `JobProfileCompetencyExpectation` para que la consulta derivada (RF-002) tenga un número con que calcular la brecha. El `BehaviorLevel` se conserva como descriptor; `ExpectedValue` es el dato comparable.

### 3.2 Dominio — escala (`src/CLARIHR.Domain/CompetencyFramework/`)

```csharp
public sealed class CompetencyRatingScale : TenantEntity   // nuevo
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public CompetencyRatingScaleType ScaleType { get; private set; } // Numeric | Discrete
    public decimal? MinValue { get; private set; }   // Numeric
    public decimal? MaxValue { get; private set; }   // Numeric
    public int Decimals { get; private set; }        // Numeric (0 para enteros)
    public bool IsActive { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public IReadOnlyCollection<CompetencyRatingScaleLevel> Levels => _levels; // Discrete
    // Create/Update/Activate/Inactivate/ReplaceLevels (+ token), espejo de OccupationalPyramidLevel/CompetencyConduct
    public bool IsValueAllowed(decimal value) => /* Numeric: Min≤v≤Max; Discrete: v ∈ Levels.Value */;
}

public sealed class CompetencyRatingScaleLevel : TenantEntity
{
    public long CompetencyRatingScaleId { get; private set; }
    public string Code { get; private set; }     // "A".."F" | "1".."5"
    public string Label { get; private set; }    // "Excelente" …
    public decimal Value { get; private set; }   // ordinal numérico para la brecha
    public int SortOrder { get; private set; }
}
```

Normalización con `CompetencyFrameworkNormalization` (espejo de `OccupationalPyramidLevel`). Invariantes en ctor: `Numeric ⇒ Min<Max`, `Discrete ⇒ ≥2 niveles con Value único`.

### 3.3 Dominio — resultado reestructurado (`PersonnelFileEmployee.cs:1468`)

**Quitar** `CompetencyCode` y `DesiredBehaviors` (texto libre). **Agregar** FKs + snapshots; **fecha obligatoria**; **gap derivado**:

```csharp
// --- NUEVOS / CAMBIADOS ---
public long CompetencyCatalogItemId { get; private set; }            // FK JobCatalogItem/Competency (D-03/D-12)
public long CompetencyTypeCatalogItemId { get; private set; }        // FK JobCatalogItem/CompetencyType (D-03)
public long? JobProfileCompetencyExpectationId { get; private set; } // FK a la celda de matriz evaluada (D-02/D-10), nullable
public string CompetencyNameSnapshot { get; private set; }           // snapshot para historial robusto
public string? CompetencyTypeCodeSnapshot { get; private set; }      // snapshot (gestión/org/técnica)
public decimal ExpectedScore { get; private set; }                   // snapshot del ExpectedValue de la matriz (D-02)
public decimal AchievedScore { get; private set; }                   // en escala (D-04)
public decimal GapScore { get; private set; }                        // DERIVADA = Expected − Achieved (D-05)
public DateTime EvaluationDateUtc { get; private set; }              // OBLIGATORIA (D-06)  ← era DateTime?
// Source* se conservan (procedencia opcional, D-01)
```

`GapScore` se calcula en `Create`/`Update` con `PositionCompetencyResultRules.DeriveGap(expected, achieved)` (§3.6); deja de ser parámetro de negocio. `Create`/`Update` reciben los nuevos parámetros; las conductas deseadas **no** se almacenan (se derivan de la expectativa enlazada en lectura — D-10).

> **Historial (D-07).** Cada fila = **una evaluación** de **una competencia** en **una fecha**. Varias filas por competencia = la serie (el índice `(tenant,file,competency)` ya es **no-único**). La **vigente** = `OrderByDescending(EvaluationDateUtc).First()`. No se introduce colección hija; se reusa la tabla.

### 3.4 Dominio — perfil ↔ nivel (`JobProfile.cs`) (resuelve R-2)

Agregar `public long? OccupationalPyramidLevelId { get; private set; }` y enhebrarlo en el ctor/`UpdateCore(:93-110)` igual que `PositionCategoryId(:130)`. En `JobProfileConfiguration.cs` (importar `CLARIHR.Domain.CompetencyFramework`):

```csharp
builder.Property(p => p.OccupationalPyramidLevelId).HasColumnName("occupational_pyramid_level_id");
builder.HasOne<OccupationalPyramidLevel>()
    .WithMany()
    .HasForeignKey(p => p.OccupationalPyramidLevelId)
    .OnDelete(DeleteBehavior.Restrict)
    .HasConstraintName("fk_job_profiles__occupational_pyramid_level");
builder.HasIndex(p => new { p.TenantId, p.OccupationalPyramidLevelId })
    .HasDatabaseName("ix_job_profiles__tenant_occupational_pyramid_level");
```

Patrón idéntico a los FK opcionales `:146-168`. Validar en el admin de perfiles que el nivel exista/activo y del tenant. *(Opcional, recomendado:)* validar coherencia entre el nivel del perfil y el `OccupationalPyramidLevelId` de sus filas de matriz.

### 3.5 Dominio/Aplicación — valor esperado en la matriz (`JobProfileCompetencyExpectation.cs:64`)

Agregar `public decimal? ExpectedValue { get; private set; }` (en la escala activa), enhebrar en `Create(:82)`/`Update(:107)`, mapear en `CompetencyFrameworkConfiguration.cs(:221-309)` como `expected_value numeric(18,2)` (no afecta la UQ tuple `:256-266`). Ampliar en `JobProfileCompetencyMatrixAdministration.cs`: comando Add(`:93`)/Update(`:101`), `JobProfileCompetencyMatrixItemResponse(:26)`, validadores, patch state/applier, export row, y las llamadas `expectation.Create/Update`. Validar `ExpectedValue` contra la **escala activa** (`CompetencyRatingScale.IsValueAllowed`).

### 3.6 Aplicación — módulo de reglas puro (`Talent/PositionCompetencyResults.Rules.cs`, nuevo)

Espejo de `Insurances.Rules.cs`. Errores dedicados + helpers puros (la brecha y validaciones de escala). Las validaciones de campo (fecha no futura, requeridos) van en el validador (400); las de catálogo/escala/pertenencia en el handler (422).

```csharp
namespace CLARIHR.Application.Features.PersonnelFiles;

internal static class PositionCompetencyResultErrors
{
    public static readonly Error CompetencyInvalid = new(
        "POSITION_COMPETENCY_CODE_INVALID",
        "The competency does not exist in the active catalog.", ErrorType.UnprocessableEntity);
    public static readonly Error CompetencyTypeInvalid = new(
        "POSITION_COMPETENCY_TYPE_INVALID",
        "The competency type does not exist in the active catalog.", ErrorType.UnprocessableEntity);
    public static readonly Error NotInProfile = new(
        "POSITION_COMPETENCY_NOT_IN_PROFILE",
        "The competency is not part of the position's competency matrix for the employee's level.", ErrorType.UnprocessableEntity);
    public static readonly Error ScoreOutOfRange = new(
        "POSITION_COMPETENCY_SCORE_OUT_OF_RANGE",
        "The score is outside the company's active rating scale.", ErrorType.UnprocessableEntity);
    public static readonly Error ScaleNotConfigured = new(
        "POSITION_COMPETENCY_SCALE_NOT_CONFIGURED",
        "No active competency rating scale is configured for the company.", ErrorType.UnprocessableEntity);
}

internal static class PositionCompetencyResultRules
{
    /// <summary>(D-05) Brecha = esperada − alcanzada (sobre el valor de la escala).</summary>
    public static decimal DeriveGap(decimal expected, decimal achieved) => expected - achieved;
}
```

### 3.7 Aplicación — consulta derivada (núcleo, D-02/RF-002)

Nueva query `GetEmployeePositionCompetenciesQuery(personnelFileId)` → respuesta agrupada por **tipo** con, por competencia: nombre, conductas deseadas (de la matriz), **esperada**, **alcanzada (vigente)**, **brecha** e **historial**.

Flujo del handler:
1. Cargar expediente con gate de lectura **perm-o-self** (§3.8); `IsCompletedEmployee`.
2. Resolver la **asignación vigente** = `IsActive && IsPrimary` (#13) → `PositionSlotPublicId`.
3. Repo nuevo `GetCompetencyMatrixForSlotAsync(slotPublicId)` — espejo de `PositionSlotRepository.GetSalaryRangeAsync(:23-30)` — que une `PositionSlot.JobProfileId → JobProfile.OccupationalPyramidLevelId → JobProfileCompetencyExpectation` (filtrando por ese nivel) → competencia/tipo/conductas/`ExpectedValue`.
4. Cargar resultados del empleado (`PersonnelFilePositionCompetencyResult`) y, por competencia, tomar la **vigente** (máx. `EvaluationDateUtc`) + la **serie**.
5. **LEFT JOIN** esperadas ⟕ alcanzadas: competencias esperadas sin nota → brecha = esperada (achieved=null); calcular brecha donde haya ambas.

> Si no hay asignación vigente o el perfil no tiene nivel/matriz, responder estado "sin competencias esperadas" (no 500) — E2/E3.

### 3.8 Permiso dedicado + autoservicio + controlador dedicado (D-08/D-09)

**Permisos** (espejo `ViewInsurance`/`ManageSubstitutions`, los 5 enlaces de #10):

| Archivo | Cambio |
|---|---|
| `Common/PersonnelFileCommon.cs` | `ViewCompetencies = "PersonnelFiles.ViewCompetencies"`, `ManageCompetencies = "PersonnelFiles.ManageCompetencies"`. |
| `Common/PersonnelFilePolicies.cs` | Las 2 constantes equivalentes. |
| `Provisioning/Common/ProvisioningConstants.cs` | 2 filas en `CompanyAdminPermissions` (módulo `PersonnelFiles`) → owner las recibe vía `OwnerPermissionCatalog`. |
| `Api/Program.cs` | **Ambas authn-only superset** (`.Combine(policy)` sin assertion) como `ViewCompensation(:444-453)` — porque el **POST** debe permitir self-service (el empleado registra/lee lo suyo). |
| `IPersonnelFileAuthorizationService` | `EnsureCanViewCompetenciesAsync`/`EnsureCanManageCompetenciesAsync` (default fail-closed). |
| `PersonnelFileAuthorizationService` | View → `EnsureHasAnyClaimAsync([ViewCompetencies, Admin, ManageAdministration], Read)`; Manage → idem con `Update`. |

**Gates de handler** — `PersonnelFileEmployeeHandlerBases.cs`:
- `LoadCompletedEmployeeForCompetencyReadAsync` — copia de `LoadCompletedEmployeeForCompensationReadAsync(:361-403)` llamando `EnsureCanViewCompetenciesAsync`; conserva la rama self (`:385-395`). → D-08 (403 a terceros) + D-09 (lectura self).
- Escritura por RRHH (`ManageCompetencies`): copia de `LoadForManageAsync(:90)`. *(Decisión de negocio: la escritura es RRHH — D-01 CLARIHR fuente; el autoservicio es de **lectura**. Si se quiere que el empleado registre, se añade el gate create-own de reclamos como variante.)*

**Controlador dedicado (gotcha #11).** Carvar los endpoints de `position-competency-results` de `PersonnelFileTalentController` a **`PersonnelFileCompetencyController`** con `[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompetencies, PersonnelFilePolicies.ManageCompetencies)]`, **rutas idénticas** (`personnel-files/{publicId}/position-competency-results[...]`) + el nuevo GET derivado `…/position-competencies`. Es un *authorization split, no un cambio de API* (patrón `PersonnelFileAuthorizationSubstitutionController.cs:23-30`). Los otros 3 sub-recursos de talento quedan en `PersonnelFileTalentController`.

### 3.9 Aplicación — comandos, validador y patch (`PositionCompetencyResults.cs`)

- **`PositionCompetencyResultInput`**: quitar `CompetencyCode`/`DesiredBehaviors`; agregar `CompetencyPublicId` (Guid), `CompetencyTypePublicId` (Guid), `ExpectationPublicId` (Guid?), `AchievedScore` (decimal), `EvaluationDateUtc` (DateTime). Quitar `ExpectedScore`/`GapScore` del input (esperada=snapshot de matriz; brecha=derivada).
- **`...Response`**: agregar `CompetencyName`, `CompetencyTypeCode`, `DesiredBehaviors` (derivadas), `ExpectedScore`, `GapScore`; `EvaluationDateUtc` no-nullable.
- **Validador**: `CompetencyPublicId`/`CompetencyTypePublicId` `NotEmpty`; `EvaluationDateUtc` `NotEmpty().LessThanOrEqualTo(_ => DateTime.UtcNow)` (D-06); `AchievedScore` `NotNull`. (Rango de escala → handler, necesita BD.)
- **Patch**: segmentos `competencyPublicId`, `competencyTypePublicId`, `achievedScore`, `evaluationDateUtc`; `gapScore`/`expectedScore` **no editables** (derivados/snapshot). `Validate` exige competencia/tipo presentes y fecha válida.

### 3.10 Aplicación — handlers (`PositionCompetencyResults.Handlers.cs`)

En Add/Update/Patch, tras `IsCompletedEmployee` y antes de persistir:
1. Resolver **escala activa** (`ICompetencyRatingScaleRepository.GetActiveAsync(tenant)`); null → `ScaleNotConfigured`.
2. Validar competencia y tipo (catálogo activo del tenant) → `CompetencyInvalid`/`CompetencyTypeInvalid`; snapshotear `CompetencyName`/`CompetencyTypeCode`.
3. Resolver la **expectativa** (si `ExpectationPublicId`): debe pertenecer al perfil/nivel del empleado → `NotInProfile`; tomar `ExpectedValue` como **esperada snapshot** y sus **conductas**.
4. Validar `AchievedScore` (y `ExpectedScore`) contra la escala (`IsValueAllowed`) → `ScoreOutOfRange`.
5. `GapScore` lo deriva el dominio (`DeriveGap`). Auditoría con **diff** (before/after, overload `:60`).

Swap de gates: queries → `LoadCompletedEmployeeForCompetencyReadAsync`; Add/Update/Patch/Delete → gate manage de competencias. Inyectar `ICurrentUserService` (read self) y `ICompetencyRatingScaleRepository`.

### 3.11 Infraestructura — EF, repos y migraciones

- **EF config resultado** (`:537-571`): quitar `competency_code`/`desired_behaviors`; agregar `competency_catalog_item_id`(FK Restrict), `competency_type_catalog_item_id`(FK Restrict), `job_profile_competency_expectation_id`(FK Restrict, nullable), snapshots, `expected/gap_score numeric(18,2)` y `evaluation_date_utc NOT NULL`; índices `(tenant,file,competency,evaluation_date)` para historial. Check `gap = expected - achieved` (opcional).
- **Escala**: `CompetencyRatingScaleConfiguration` + `...LevelConfiguration` (espejo `OccupationalPyramidLevel`/`CompetencyConduct`): tablas `competency_rating_scales` / `competency_rating_scale_levels`, UQ `(tenant, normalized_code)`, índice filtrado de única activa, FK cascade a la escala.
- **Repos**: ampliar `Update/Add/Map` del resultado (FKs/snapshots/fecha); nuevos `ICompetencyRatingScaleRepository` + `IPositionSlotRepository.GetCompetencyMatrixForSlotAsync` (default no-op en la interfaz para test doubles, como `IPositionSlotRepository.cs:18-21`). `JobProfile` repo: validar/asignar nivel.
- **Migraciones** (sugerido 2): (M1) `AddJobProfilePyramidLevelAndMatrixExpectedValue` (FK perfil→nivel + `expected_value`); (M2) `RestructurePositionCompetencyResultsAndRatingScale` (escala + reestructura del resultado, **drop & recreate** D-11). Convención:
```bash
DOTNET_ROLL_FORWARD=Major dotnet ef migrations add RestructurePositionCompetencyResultsAndRatingScale \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```
Verificar con `… migrations has-pending-model-changes` (sin pendientes).

### 3.12 Seed por-tenant (D-03) — `CompetencyFrameworkSeedService`

`JobCatalogItem` es **tenant-scoped** (#7) → seed por-tenant (espejo `OrgStructureCatalogSeedService.InitializeDefaultsAsync(tenantId)`), invocado en `CompanyProvisioningService.ProvisionAsync(:150)` y replicado en `DevSeedService` para el tenant de desarrollo:
```csharp
JobCatalogItem.Create(JobCatalogCategory.CompetencyType, "GESTION", "Gestión", isSystem: true)
JobCatalogItem.Create(JobCatalogCategory.CompetencyType, "ORGANIZACIONAL", "Organizacional", isSystem: true)
JobCatalogItem.Create(JobCatalogCategory.CompetencyType, "TECNICA", "Técnica", isSystem: true)
// + 1 CompetencyRatingScale activa por defecto (p. ej. discreta 1–5) con sus niveles
```
Idempotente (guard "ya existe categoría CompetencyType para el tenant"). **Backfill prod**: migración de datos que itera tenants (`INSERT … SELECT … WHERE NOT EXISTS`). Como no hay datos productivos de resultados (D-11), no hay migración de datos del resultado.

### 3.13 Localización y auditoría

- **Localización**: ~5 códigos (`POSITION_COMPETENCY_*`) en `BackendMessages.resx` + `.es.resx` + `.es-SV.resx` (paridad `BackendMessageLocalizationTests`).
- **Auditoría**: usar el overload **before/after** (`PersonnelFileEmployeeAudits.LogUpdateAsync(:60)`) en Update/Patch/Delete (capturar el response previo); before-less en Add. Sin auditar lecturas.

---

## 4. Migración de datos

**No hay datos productivos en `personnel_file_position_competency_results` (D-11)** → **drop & recreate** de la tabla (cambia de texto libre a FKs; un backfill no es viable ni necesario). Las nuevas columnas de `job_profiles` (`occupational_pyramid_level_id`, nullable) y de la matriz (`expected_value`, nullable) son aditivas y no rompen datos. El seed de `CompetencyType`/escala se aplica por-tenant (§3.12). **Acción previa:** confirmar ausencia de datos productivos (S3); de existir, vaciar la tabla en entornos no productivos antes de la migración.

---

## 5. Mapa de errores (resumen)

| Disparador | Código | ErrorType → HTTP | Capa |
|---|---|---|---|
| Competencia/tipo vacíos; `achievedScore` ausente; fecha ausente o futura | `common.validation` | Validation → **400** | Validador |
| Sin escala activa configurada | `POSITION_COMPETENCY_SCALE_NOT_CONFIGURED` | UnprocessableEntity → **422** | Handler |
| Competencia / tipo fuera de catálogo | `POSITION_COMPETENCY_CODE_INVALID` / `..._TYPE_INVALID` | UnprocessableEntity → **422** | Handler |
| Competencia no pertenece a la matriz del puesto/nivel | `POSITION_COMPETENCY_NOT_IN_PROFILE` | UnprocessableEntity → **422** | Handler |
| Nota fuera de la escala activa | `POSITION_COMPETENCY_SCORE_OUT_OF_RANGE` | UnprocessableEntity → **422** | Handler |
| Sin `ViewCompetencies` y no titular (lectura) | (gate) | Forbidden → **403** | Handler |
| Sin `ManageCompetencies` (escritura) | (política/gate) | Forbidden → **403** | API/Handler |
| `If-Match` no coincide / expediente no completado | `CONCURRENCY_CONFLICT` / `STATE_RULE_VIOLATION` | 409 / 422 | Handler (existente) |

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- `PositionCompetencyResultRulesTests` (nuevo): `DeriveGap` (signos, ceros); escala `IsValueAllowed` (numérica bordes; discreta ∈ niveles).
- `CompetencyRatingScaleDomainTests` (nuevo): invariantes numérica (Min<Max) y discreta (≥2 niveles, Value único), única activa.
- `PositionCompetencyResultInputValidatorTests` (nuevo): competencia/tipo requeridos, fecha no futura/obligatoria, achieved requerido.
- `PersonnelFilePositionCompetencyResultPatchTests` (ampliar): nuevos segmentos; `gapScore`/`expectedScore` no editables; `Baseline()` con FKs.
- `BackendMessageLocalizationTests` (existente): verde con los ~5 códigos nuevos.
- `AuthorizationPolicyConventionGovernanceTests` (ajustar): registrar `ViewCompetencies`/`ManageCompetencies` en `PersonnelFilePolicyNames(:81-96)` y enrolar `PersonnelFileCompetencyController`.
- `OpenApiContractGuardrailsTests` (ajustar): enrolar el nuevo controller en la familia "Personnel Files" + `[SwaggerOperation]`.

**Integración (`tests/CLARIHR.Api.IntegrationTests/`):** (hoy **no hay** integración para esta feature — agregar)
- Seeder: nivel jerárquico, perfil atado a nivel + matriz con `ExpectedValue`, escala activa, `CompetencyType` SV, permisos, empleado con asignación vigente y **usuario vinculado** (self).
- Casos: consulta **derivada** (esperadas por nivel + alcanzadas + brecha, agrupado por tipo); alta de nota (gap calculado); competencia fuera de matriz (422); nota fuera de escala (422); fecha futura (400); historial (varias notas → vigente + serie); **self-service** (titular lee lo suyo 200; tercero sin permiso 403); `If-Match` 409.

---

## 7. Orden de implementación (PRs sugeridos)

1. **PR-1 — Escala + tipos (seed)** (§3.2, §3.12): `CompetencyRatingScale(+Level)` + CRUD + EF + repo + `CompetencyFrameworkSeedService` (CompetencyType SV + escala por defecto) + provisioning/DevSeed + migración. Aislado.
2. **PR-2 — Perfil↔nivel + valor esperado en matriz** (§3.4, §3.5): FK `JobProfile→OccupationalPyramidLevel` + `ExpectedValue` en la matriz (dominio/EF/admin/validador/patch/export) + migración M1.
3. **PR-3 — Reestructura del resultado + reglas + EF/migración** (§3.3, §3.6, §3.11): FKs/snapshots/fecha obligatoria/gap derivado + `PositionCompetencyResults.Rules.cs` + drop & recreate (M2).
4. **PR-4 — Consulta derivada** (§3.7): query + repo `GetCompetencyMatrixForSlotAsync` + combinación esperada/alcanzada/brecha + historial.
5. **PR-5 — Permiso + self-service + controlador dedicado** (§3.8): constantes/políticas/seed/servicio + gates + `PersonnelFileCompetencyController` (rutas idénticas) + ajustes de governance tests.
6. **PR-6 — API + localización + auditoría + tests** (§3.9, §3.13, §6): contratos, errores ×3 resx, diff, batería completa (unit + integración).

> PR-1 y PR-2 son aislables y de bajo riesgo (aditivos). PR-3/PR-4 son el corazón. PR-5 resuelve el gotcha de autorización.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Escala configurable (sin precedente directo).** Es el componente más nuevo. Mitiga: modelar `CompetencyRatingScale` como entidad simple (numérica o discreta), una activa por tenant, y centralizar la validación en `IsValueAllowed` + tests de dominio. La alternativa ligera (valor en `JobCatalogItem`) queda documentada si el negocio descarta 0–100.
- **R-T2 — Perfil↔nivel y coherencia con la matriz (R-2).** Hoy el `JobProfile` no tiene nivel y la matriz lo lleva por fila. Al atar el perfil a un nivel, validar que las filas de matriz usen ese nivel (o decidir que el nivel del perfil filtra la matriz). Sin nivel, la consulta derivada degrada a "sin esperadas" (E3), no falla.
- **R-T3 — Esperada: snapshot vs. vivo.** Se **snapshotea** `ExpectedValue` en el resultado al evaluar (historial robusto si la matriz cambia luego). La consulta derivada para competencias **no evaluadas** usa el `ExpectedValue` vivo de la matriz. Documentar esta dualidad.
- **R-T4 — Asignación vigente (multi-plaza).** La derivación usa `IsActive && IsPrimary` (#13). Si un empleado no tiene primaria activa (caso borde), responder "sin esperadas". Considerar fecha-vigencia (`StartDate/EndDate`) si el negocio lo pide.
- **R-T5 — Gotcha de autorización (class-only).** Sin controlador dedicado, no se puede dar permiso propio a competencias sin afectar a los otros 3 sub-recursos de talento. Se carva `PersonnelFileCompetencyController` (rutas idénticas) — confirmado como patrón en sustituciones.
- **R-T6 — Reestructura breaking del resultado.** Texto libre → FKs cambia DTOs/patch/repo/Map. Mitigado por drop & recreate (D-11, sin datos) y por los tests de patch existentes.
- **R-T7 — Seed por-tenant + backfill prod.** `CompetencyType`/escala son tenant-scoped; nuevos tenants vía provisioning, existentes vía migración iterando tenants. Verificar idempotencia.

---

## 9. Checklist de implementación

- [ ] **Escala:** `CompetencyRatingScale(+Level)` + invariantes + `IsValueAllowed` + CRUD + EF + repo + `ICompetencyRatingScaleRepository`.
- [ ] **Seed:** `CompetencyFrameworkSeedService` (CompetencyType GESTION/ORGANIZACIONAL/TECNICA + escala por defecto) + provisioning(:150) + DevSeed + backfill prod por tenant.
- [ ] **Perfil↔nivel:** `JobProfile.OccupationalPyramidLevelId` + ctor/`UpdateCore` + EF FK + validación en admin.
- [ ] **Matriz:** `ExpectedValue` en `JobProfileCompetencyExpectation` + Create/Update + EF + admin/validador/patch/export + validación de escala.
- [ ] **Resultado (dominio):** quitar CompetencyCode/DesiredBehaviors; +FKs competencia/tipo/expectativa +snapshots; `EvaluationDateUtc` NOT NULL; `GapScore` derivado (`DeriveGap`).
- [ ] **Reglas:** `PositionCompetencyResults.Rules.cs` (errores + DeriveGap).
- [ ] **Aplicación:** Input/Response/PatchState ajustados; validador endurecido; handlers (escala/catálogo/expectativa/rango + derivación) + inyectar `ICurrentUserService`/`ICompetencyRatingScaleRepository`.
- [ ] **Consulta derivada:** `GetEmployeePositionCompetenciesQuery` + repo `GetCompetencyMatrixForSlotAsync` (asignación vigente→slot→perfil→nivel→matriz, LEFT JOIN notas, brecha, historial, agrupado por tipo).
- [ ] **Permisos/gates:** `ViewCompetencies`/`ManageCompetencies` (authn-only) + servicio iface/impl + read-gate perm-o-self + manage-gate.
- [ ] **Controlador dedicado:** `PersonnelFileCompetencyController` (rutas idénticas) + GET derivado; ajustar governance/OpenAPI tests.
- [ ] **Infra:** EF resultado (FKs/snapshots/fecha/índices) + 2 migraciones (M1 aditiva, M2 drop&recreate) + repos/Map.
- [ ] **API:** contratos + mapeo.
- [ ] **Localización:** ~5 códigos en EN + es + es-SV.
- [ ] **Auditoría:** diff before/after en Update/Patch/Delete.
- [ ] **Tests:** rules + scale domain + validator + patch + paridad + governance/openapi + integración (derivada/errores/403/409/historial/self).
- [ ] **Verificación:** `dotnet build`, `dotnet test`, `DOTNET_ROLL_FORWARD=Major dotnet ef migrations has-pending-model-changes`.

---

> **Trazabilidad.** Implementa la Fase 1 del análisis (D-01…D-12, RF-001…RF-011). Patrones reutilizados y verificados: escala/seed por-tenant (`OrgStructureCatalogSeedService`), permisos (`ViewInsurance`/`ManageSubstitutions`), controlador dedicado (`PersonnelFileAuthorizationSubstitutionController`), gate self-service de compensación, reglas puras (`Insurances.Rules.cs`), join slot→perfil (`PositionSlotRepository.GetSalaryRangeAsync`), recursos `BackendMessages*`. Los dos componentes de mayor diseño nuevo son la **escala configurable** (§3.1-3.2) y la **consulta derivada** (§3.7); todo lo demás sigue recetas existentes. Convención de migración: `DOTNET_ROLL_FORWARD=Major dotnet ef …`.
