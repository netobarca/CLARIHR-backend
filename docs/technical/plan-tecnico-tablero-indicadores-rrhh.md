# Plan Técnico — Tablero de Gráficos e Indicadores de RRHH (HR Analytics Dashboard)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación (Fase 1) |
| **Audiencia** | Equipo de desarrollo backend, QA, tech lead |
| **Documento de negocio** | [`analisis-tablero-indicadores-rrhh.md`](../business/analisis-tablero-indicadores-rrhh.md) (alcance Fase 1 CERRADO, decisiones **D-01…D-14** ratificadas 2026-06-27) |
| **Módulos** | Expediente de Personal → Reportería/Analítica (`PersonnelFiles/Reporting`, `Reports`); Asignaciones/Plazas; Estructura org (`OrgUnits`/`WorkCenters`/`JobProfiles`/`PositionCategory`); Catálogos; Preferencias de empresa; IAM |
| **Estado** | Propuesto — listo para implementar |
| **Fecha** | 2026-06-27 |
| **País de referencia (seed)** | El Salvador (SV) |
| **Naturaleza** | **Capa analítica read-only** (agregaciones); **no** muta datos. Consumidor de **módulos existentes** (principio de alcance). |

---

## 1. Objetivo y enfoque

Construir una **capa de analítica de RRHH** read-only que agrega el padrón de personal en indicadores gráficos, con filtros transversales por **año, área funcional, unidad, tipo de puesto, puesto y centro de trabajo**.

**Hallazgo base (validado):** ya existe reportería expediente-céntrica (`PersonnelFileReportingController`: `analytics/summary`, `dynamic-query`, `export`), pero **no une la asignación activa** ni cubre las dimensiones de puesto/estructura. El núcleo técnico de este plan es la **capa dimensional (RF-002)**: unir `PersonnelFile → asignación activa (IsActive && IsPrimary) → PositionSlot/JobProfile/OrgUnit/WorkCenter` y, sobre esa proyección, calcular los indicadores.

**Principio de alcance (D-07/D-14):** se construyen **solo** indicadores sobre módulos existentes. **Bajas, índice de rotación y filtro por nivel de pirámide quedan FUERA** (dependen del módulo *Baja de Personal* y de la relación puesto↔nivel). Las **altas** sí se incluyen (dato existente, `HireDate`).

**Patrón base:** se replica el estilo de `PersonnelFileReporting.cs` (queries CQRS read-only, gate en handler `EnsureCanReadAsync`, controlador **sin** `[AuthorizationPolicySet]`), añadiendo: (a) la proyección dimensional, (b) el permiso dedicado `ViewReports` modelado como `ViewInsurance`, (c) 2 catálogos país-scoped de rangos (edad/antigüedad) modelados como `InsuranceRangeCatalogItem` (con cotas numéricas), (d) parametrización por empresa extendiendo `CompanyPreference`.

Indicadores Fase 1: **composición por categorías · edad (rangos) · antigüedad (rangos) · estado civil · altas · colaboradores por jefe · RRHH/100 · expedientes actualizados/desactualizados · plazas vacantes/ocupadas**.

---

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Reportería existente | `PersonnelFileReportingController.cs:31/72/162` (dynamic-query / export / analytics-summary); handlers en `PersonnelFiles/Reporting/PersonnelFileReporting.cs` | Extender la **familia Reporting** (read-only, gate en handler) |
| 2 | Agregación (patrón) | `PersonnelFileRepository.cs:1755-1859` `GetAnalyticsSummaryAsync`: `GroupBy(_=>1)` para totales + `GroupBy(dimensión)` para desgloses; buckets de edad **hardcodeados** | Mirror para nuevas dimensiones; reemplazar buckets hardcode por catálogo |
| 3 | Asignación activa (join) | `PersonnelFileEmployeeRepository.cs:330-370` `GetPositionHierarchyAsync`: `Where(... && IsActive && IsPrimary)` + fallback a `file.OrgUnitPublicId` | Patrón canónico para resolver dimensiones por asignación |
| 4 | Dimensiones (FKs verificadas) | `PersonnelFileEmploymentAssignment` lleva `PositionSlotPublicId/OrgUnitPublicId/WorkCenterPublicId/ContractTypeCode` (`PersonnelFileEmployee.cs:99-175`); `PositionSlot.JobProfileId/WorkCenterId/DirectDependencyPositionSlotId` (`PositionSlot.cs:73-81`); `JobProfile.PositionCategoryId/OrgUnitId` (`JobProfile.cs:41-45`); `OrgUnit.FunctionalAreaCatalogItemId/ManagerEmployeeId` (`OrgUnit.cs:62,72`); `PositionSlot.MaxEmployees/OccupiedEmployees` (`PositionSlot.cs:85-87`) | Todas las cadenas de join existen (joins **por PublicId**) |
| 5 | Demografía/baja | `PersonnelFileEmployeeProfile.HireDate` (`:46`), `BirthDate`/`MaritalStatus` en `PersonnelFile`; `RetirementDate` (`:55`) **solo referencia** (módulo Baja futuro) | Altas=`HireDate`; bajas/rotación diferidas |
| 6 | Permiso dedicado (patrón) | `PersonnelFilePermissionCodes`/`PersonnelFilePolicies` (`PersonnelFileCommon.cs:82-172`, `PersonnelFilePolicies.cs:14-107`); registro `Program.cs:439-528`; gate `IPersonnelFileAuthorizationService` (`EnsureCanViewInsuranceAsync` como modelo) | Añadir `ViewReports` como `ViewInsurance` (authn-only + gate en handler) |
| 7 | Catálogo país-scoped con cotas | `CountryScopedCatalogItem.cs:5-135` (base, **sin** campos numéricos) → `InsuranceRangeCatalogItem` (`PersonnelReferenceCatalogItem.cs:250`) añade FK/campos + override `ConfigureUniqueCodeIndex` (`PersonnelReferenceCatalogItemConfiguration.cs:212`) | Rangos edad/antigüedad = catálogo con cotas numéricas |
| 8 | Wire key + controlador catálogo | `GeneralCatalogKeyMap.cs:16-95` (dicts + guardrail); `GeneralCatalogsController.cs:18-94` (GET por key) | Wire keys `age-ranges`/`seniority-ranges`; bounds vía `dashboard/metadata` |
| 9 | Seed país (HasData) | `GlobalCatalogSeedData.cs` (p.ej. `GetActionTypeCatalogItems():572`); SV = `CountryCatalogDefinition(-7068L,"SV")` | Seed SV de rangos (D-10) |
| 10 | Parametrización empresa | `CompanyPreference.cs:5-39` (entidad **tipada**: `CurrencyCode/TimeZone/ConcurrencyToken`); `CompanyPreferencesController.cs:15-112` (GET/PUT/PATCH, If-Match) | **Extender** `CompanyPreference`: `HrFunctionalAreaCode?`, `FileUpToDateThresholdMonths?` |
| 11 | DI handlers | `DependencyInjection.cs:21-29` (assembly scan de `IQueryHandler<,>`) | Sin registro explícito |
| 12 | Gobernanza/convención | `AuthorizationPolicyConvention.cs:27-63`; test `AuthorizationPolicyConventionGovernanceTests.cs:78-112`; **Reporting excluido** vía regex `(?!Reporting)` (`:44-52`) | Dashboard vive en la familia Reporting (handler-gated, sin `[AuthorizationPolicySet]`) |
| 13 | Migración EF | EF 9.0.9 requiere `DOTNET_ROLL_FORWARD=Major` (memoria equipo) | Exportar var antes de `dotnet ef` |

---

## 3. Arquitectura de la solución

### 3.1 Capa dimensional (RF-002) — el núcleo

Nueva proyección read-only en `PersonnelFileRepository` (o un `PersonnelFileDashboardRepository` dedicado en `Infrastructure/PersonnelFiles`) que produce, por empleado, una **fila dimensional**:

```csharp
internal sealed record EmployeeDimensionRow(
    long FileId,
    bool IsActive,
    DateTime BirthDate,
    DateTime HireDate,
    DateTime? RetirementDate,          // solo para "activo-a-fecha" del filtro año (R-02, aprox.)
    string? MaritalStatus,
    string RecordType,
    string? EmploymentStatusCode,
    string? ContractTypeCode,          // de la asignación activa
    Guid? OrgUnitPublicId,             // asignación activa → fallback file.OrgUnitPublicId
    long? FunctionalAreaCatalogItemId, // OrgUnit.FunctionalAreaCatalogItemId
    Guid? WorkCenterPublicId,
    Guid? PositionSlotPublicId,
    long? JobProfileId,
    long? PositionCategoryId);
```

**Construcción (mirror de `GetPositionHierarchyAsync`, joins por PublicId):**
1. Base: `Set<PersonnelFile>().AsNoTracking().Where(f => f.TenantId == tenantId && f.RecordType == Employee)`; `IsActive` por defecto `true` salvo `includeInactive` (D-03).
2. LEFT JOIN a la **asignación activa primaria** (`IsActive && IsPrimary`, `OrderBy(StartDate)` → primera) por `PersonnelFileId`.
3. Desde la asignación: `OrgUnitPublicId` (fallback `file.OrgUnitPublicId`), `WorkCenterPublicId`, `PositionSlotPublicId`, `ContractTypeCode`.
4. LEFT JOIN `OrgUnit` por `PublicId == OrgUnitPublicId` → `FunctionalAreaCatalogItemId`.
5. LEFT JOIN `PositionSlot` por `PublicId == PositionSlotPublicId` → `JobProfileId`.
6. LEFT JOIN `JobProfile` por `Id == JobProfileId` → `PositionCategoryId`.
7. `HireDate`/`EmploymentStatusCode` de `PersonnelFileEmployeeProfile`; `BirthDate`/`MaritalStatus`/`RecordType` del expediente.

**Filtro común** (`DashboardFilter`) se aplica sobre la proyección: `functionalAreaId`, `orgUnitId`, `positionCategoryId`, `jobProfileId`, `workCenterId` (resueltos a `Id`/`PublicId` antes de consultar), `includeInactive`, `year`.

- **Semántica de `year` (R-02, aproximada):** indicadores *snapshot* sin `year` → estado actual. Con `year` → "activo al cierre del año Y": `HireDate <= 31-Dic-Y` **y** (`RetirementDate == null || RetirementDate > 31-Dic-Y`). Es una **reconstrucción aproximada** (no hay snapshots históricos). El indicador de **altas** usa `year` de forma exacta sobre `HireDate`.

> **Performance (R-01):** la proyección es **una** consulta indexada. Para edad/antigüedad se proyectan filas mínimas `(dimensiones, edad, antigüedadMeses)` y se bucketiza en memoria con las cotas del catálogo (headcount acotado). Si crece, materializar una vista. **No** traer entidades completas (siempre `AsNoTracking` + `Select`).

### 3.2 Endpoints (familia Reporting — read-only, gate en handler)

Ruta base: `api/v1/companies/{companyId:guid}/personnel-files/dashboard/...`. Todos `GET`, gate `EnsureCanViewReportsAsync`, `[EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]`.

| Endpoint | Respuesta (resumen) | Indicadores |
|---|---|---|
| `GET .../dashboard/overview` | `headcount{total,active,inactive}` + `byRecordType/byEmploymentStatus/byContractType/byPositionCategory/byFunctionalArea/byOrgUnit/byWorkCenter` + `byAgeRange/bySeniorityRange/byMaritalStatus` + `fileFreshness{upToDate,outdated,thresholdMonths}` + `positionOccupancy{max,occupied,vacant}` + `hrRatio{hrHeadcount,total,ratioPer100,configured}` | Composición, edad, antigüedad, estado civil, expedientes, plazas, RRHH/100 |
| `GET .../dashboard/hires` | `{ year, byMonth:[{month,count}], total }` (bajas = `null`, diferidas — D-02) | Altas |
| `GET .../dashboard/span-of-control` | `{ managers:[{managerEmployeeId,managerName,positionTitle,directReports}], withoutManagerCount }` | Colaboradores por jefe |
| `GET .../dashboard/metadata` | `{ ageRanges:[{code,label,lowerBound,upperBound}], seniorityRanges:[...], thresholdMonths, hrFunctionalAreaCode }` (las listas de filtros — unidades, puestos, etc. — reutilizan endpoints existentes) | Catálogos de rangos + parametrización resuelta |

Todos aceptan el **mismo `DashboardFilter`** vía query string. `overview` puede partirse por indicador si el payload pesa; se documenta como decisión de implementación.

### 3.3 Catálogos de rangos + parametrización

**Catálogos país-scoped con cotas numéricas** (modelados como `InsuranceRangeCatalogItem`, que ya añade campos a la base):

```csharp
public sealed class AgeRangeCatalogItem : GeneralCatalogItem   // o PersonnelReferenceCatalogItemBase
{
    public int LowerBoundYears { get; private set; }            // inclusive
    public int? UpperBoundYears { get; private set; }           // null = abierto (56+)
}
public sealed class SeniorityRangeCatalogItem : GeneralCatalogItem
{
    public int LowerBoundMonths { get; private set; }           // inclusive
    public int? UpperBoundMonths { get; private set; }          // null = abierto (10+ años)
}
```

- EF config: índice único `(country, code)` (base); columnas `lower_bound`/`upper_bound`.
- DbSets en `ApplicationDbContext`.
- Wire keys `age-ranges` / `seniority-ranges` en `GeneralCatalogKeyMap` (listado nombre/código para i18n del FE); **las cotas** se exponen vía `dashboard/metadata` (el `GeneralCatalogsController` devuelve una forma fija sin cotas).
- **Seed SV (HasData, D-10):** edad `18-25,26-35,36-45,46-55,56+` (+ `<18` si aplica); antigüedad `<1, 1-3, 3-5, 5-10, 10+` años (en meses: `0-11, 12-35, 36-59, 60-119, 120+`).
- **Admin CRUD diferido** (seed-only en Fase 1, consistente con otros catálogos); GET disponible.

**Parametrización por empresa — extender `CompanyPreference`** (entidad tipada existente):

```csharp
public string? HrFunctionalAreaCode { get; private set; }          // D-06: área funcional = RRHH
public int? FileUpToDateThresholdMonths { get; private set; }      // D-08: default 12 si null
```

- Extender `CompanyPreference.Update(...)`, los handlers GET/PUT/PATCH y los paths JSON-Patch (`/hrFunctionalAreaCode`, `/fileUpToDateThresholdMonths`) en `CompanyPreferencesController`.
- Reutiliza If-Match/concurrencia/auth existentes.

### 3.4 Autorización — permiso `ViewReports` (D-09)

Modelado **idéntico a `ViewInsurance`** (read dedicado, authn-only, gate en handler — **no** `[AuthorizationPolicySet]`):
1. `PersonnelFilePermissionCodes.ViewReports = "PersonnelFiles.ViewReports"` (`PersonnelFileCommon.cs`).
2. `PersonnelFilePolicies.ViewReports` (`PersonnelFilePolicies.cs`).
3. Registro authn-only en `Program.cs` (`.Combine(policy)`), superset: `HasAnyPermission(ViewReports, Read, Admin)`.
4. `IPersonnelFileAuthorizationService.EnsureCanViewReportsAsync(companyId)` (+ impl) → éxito si `ViewReports ∨ Read ∨ Admin`.
5. Los handlers del dashboard gatean con `EnsureCanViewReportsAsync`.
6. **Gobernanza:** la familia Reporting está excluida del convention-test (regex `(?!Reporting)`); el dashboard vive ahí (mismo controlador `PersonnelFileReportingController` **o** un `PersonnelFileDashboardController` añadido a la exclusión `(?!Reporting|Dashboard)`). Si `ViewReports` se enumera en el set de políticas del test, tratarlo como handler-gated (igual que `ViewInsurance`).

### 3.5 Reglas puras (`*.Rules.cs`)

Módulo `PersonnelFileDashboard.Rules.cs` (funciones puras, testeables):
- `BucketAge(birthDate, refDate, ranges)` / `BucketSeniority(hireDate, refDate, ranges)`.
- `IsFileUpToDate(lifecycleStatus, modifiedAtUtc, refDate, thresholdMonths)` → `Completed && modifiedAtUtc >= refDate.AddMonths(-threshold)`.
- `ComputeHrRatio(hrHeadcount, totalHeadcount)` → `total==0 ? N/D : hr/total*100`.
- `IsActiveAtYearEnd(hireDate, retirementDate, year)` (R-02 aprox.).

---

## 4. Detalle de cálculo por indicador

| Indicador | Cálculo (sobre `EmployeeDimensionRow`, ya filtrado) |
|---|---|
| **Composición por categorías** | `GroupBy` por cada dimensión (recordType, employmentStatus, contractType, positionCategory→nombre, functionalArea→nombre, orgUnit→nombre, workCenter→nombre); buckets `{key,label,count}`; `Sin asignar` explícito para nulos (RN-10) |
| **Edad** | proyectar `edad = AgeAt(refDate, BirthDate)`; bucketizar con `AgeRangeCatalogItem` (cotas) |
| **Antigüedad** | `meses = MonthsBetween(HireDate, refDate)`; bucketizar con `SeniorityRangeCatalogItem` |
| **Estado civil** | `GroupBy(MaritalStatus)` + label desde catálogo `marital-statuses` |
| **Altas** (D-02) | empleados con `HireDate.Year == year` (o rango); `GroupBy(HireDate.Month)`; total |
| **Colaboradores por jefe** (D-05) | por empleado: `assignment.PositionSlotPublicId → PositionSlot.DirectDependencyPositionSlotId (Id) → PositionSlot (jefe) → ocupante vía asignación activa → empleado jefe`; `GroupBy(jefe)`; plaza-jefe vacante → `withoutManagerCount` |
| **RRHH/100** (D-06) | `hrHeadcount = count(FunctionalArea.Code == prefs.HrFunctionalAreaCode)`; `ratio = hr/total*100`; si `HrFunctionalAreaCode == null` → `configured=false` |
| **Expedientes actualizados** (D-08) | `IsFileUpToDate(LifecycleStatus, file.ModifiedAtUtc, refDate, prefs.FileUpToDateThresholdMonths ?? 12)` → conteo upToDate vs outdated |
| **Plazas vacantes/ocupadas** (D-13) | sobre `PositionSlot` (filtrado por dimensiones vía JobProfile/OrgUnit/WorkCenter): `occupied = Σ OccupiedEmployees`, `vacant = Σ (MaxEmployees - OccupiedEmployees)` |

> **Riesgo a validar — "expediente actualizado":** `file.ModifiedAtUtc` puede **no** actualizarse cuando se edita una **entidad hija** del expediente (las hijas se persisten standalone vía repositorio). **Verificar** en `Infrastructure/Auditing`/SaveChanges si las ediciones de hijas "tocan" la raíz; si no, "actualizado" reflejará solo ediciones de la raíz. Si es insuficiente, derivar `LastTouchedUtc = MAX(ModifiedAtUtc de raíz + hijas relevantes)` (más costoso). Decidir en PR-4.

---

## 5. Modelo de datos / migración

**Una migración** `AddHrDashboardRangeCatalogsAndPreferences`:
- `age_range_catalog_items` (country-scoped + `lower_bound_years`, `upper_bound_years` nullable; uq `(country_code, normalized_code)`, idx `(country, active, sort)`).
- `seniority_range_catalog_items` (idem con `lower_bound_months`, `upper_bound_months`).
- `company_preferences`: `+ hr_functional_area_code (varchar, null)`, `+ file_up_to_date_threshold_months (int, null)`.
- **HasData** seed SV de ambos catálogos (D-10).
- Sin cambios a tablas de personal/plazas (solo lectura).

Comando: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add AddHrDashboardRangeCatalogsAndPreferences -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api`. Verificar **sin drift** (`ApplicationDbContextModelSnapshot`).

---

## 6. Plan de PRs

| PR | Alcance | Entregable |
|---|---|---|
| **PR-1** | **Permiso `ViewReports`** (codes, policy, registro `Program.cs`, `EnsureCanViewReportsAsync` + impl, ajuste gobernanza) + esqueleto de endpoints dashboard (gate, rate-limit, contratos vacíos) | Auth lista; 404→200 vacío |
| **PR-2** | **Catálogos de rangos** (edad/antigüedad: dominio + EF config + DbSet + wire keys) + **extensión `CompanyPreference`** (2 campos + PUT/PATCH) + **migración + seed SV** | Catálogos consultables; parametrización editable |
| **PR-3** | **Capa dimensional (RF-002)** + endpoint **`overview`** (composición, edad, antigüedad, estado civil) + `Rules.cs` (bucketización) | Indicadores demográficos/composición |
| **PR-4** | **`overview` (estructura/calidad):** RRHH/100, expedientes actualizados (resolver el riesgo `ModifiedAtUtc`), plazas vacantes/ocupadas | Indicadores de estructura/calidad |
| **PR-5** | **`hires`** (altas serie temporal) + **`span-of-control`** (colaboradores por jefe) + **`metadata`** | Altas + tramo de control + metadata |
| **PR-6** | **Pruebas** (unit Rules + integración endpoints + gobernanza/localización) + **guía de integración frontend** `docs/technical/guia-integracion-frontend-tablero-indicadores-rrhh.md` | Cobertura + doc FE |

> Export por indicador (RF-015) **diferido a Fase 2** (D-11): reutilizará `report-export-jobs` con nuevos resource keys.

---

## 7. Pruebas

- **Unit (`*.Rules` y handlers):** bucketización edad/antigüedad (cotas, bordes, abierto-superior), `IsFileUpToDate` (umbral, Completed), `ComputeHrRatio` (división por cero → N/D), resolución de jefe (plaza vacante), `IsActiveAtYearEnd`. Conteos con `Sin asignar`.
- **Localización:** paridad EN/ES/es-SV (test `BackendMessageLocalizationTests`) si se añaden mensajes (mínimos; estados como `hrRatio.configured=false` **no** son errores).
- **Gobernanza:** `AuthorizationPolicyConventionGovernanceTests` actualizado para `ViewReports` (handler-gated) y el controlador dashboard.
- **Integración:** cada endpoint con datos sembrados (composición, edad/antigüedad por catálogo, RRHH/100 con/sin parametrización, expedientes actualizados, plazas, altas por mes, span-of-control). Verificar filtros transversales. **Gotcha** (memoria 27/06): con connection-string compartida hay colisión de doble-seed de catálogos país → usar **BD por-GUID en :5432**.

---

## 8. Riesgos técnicos y mitigaciones

| # | Riesgo | Mitigación |
|---|---|---|
| **T-01** | Costo de la proyección dimensional (joins por PublicId) | Una consulta `AsNoTracking`+`Select`; bucketizar en memoria; vista materializada si escala (R-01) |
| **T-02** | `year` sobre snapshots = aproximado (sin histórico) | Documentar como aproximación (R-02); `year` exacto solo en altas |
| **T-03** | `ModifiedAtUtc` puede no reflejar edición de hijas | Verificar SaveChanges/auditoría; si insuficiente, `LastTouchedUtc` derivado (PR-4) |
| **T-04** | Gobernanza/convención al añadir `ViewReports` sin `[AuthorizationPolicySet]` | Seguir el patrón `ViewInsurance` + exclusión regex de la familia Reporting/Dashboard |
| **T-05** | `OccupiedEmployees` (contador en `PositionSlot`) desincronizado | Validar que se mantiene en alta/baja de asignación; si dudoso, derivar conteo real de asignaciones activas |
| **T-06** | Cotas de rango en `GeneralCatalogsController` (forma fija) | Exponer cotas vía `dashboard/metadata`, no por el controlador genérico |
| **T-07** | Migración con drift / EF 9.0.9 | `DOTNET_ROLL_FORWARD=Major`; verificar snapshot sin drift |

---

## 9. Checklist de entrega / gotchas

- [ ] `DOTNET_ROLL_FORWARD=Major` antes de `dotnet ef`.
- [ ] Migración **sin drift** (revisar `ApplicationDbContextModelSnapshot`).
- [ ] `ViewReports` modelado como `ViewInsurance` (authn-only + handler gate, **no** `[AuthorizationPolicySet]`).
- [ ] Catálogos rangos: índice único `(country, code)`, seed SV HasData, wire keys en `GeneralCatalogKeyMap` (+ guardrail test pasa).
- [ ] `CompanyPreference` extendido: GET/PUT/PATCH + paths JSON-Patch.
- [ ] Resuelto el comportamiento de `ModifiedAtUtc` para "expediente actualizado".
- [ ] Buckets `Sin asignar`/`Sin dato` explícitos (RN-10); división por cero → N/D.
- [ ] Contrato `api/v1`, enums como strings, errores con `extensions.code`, bilingüe.
- [ ] Pruebas de integración con **BD por-GUID en :5432** (evitar colisión de doble-seed).
- [ ] Guía de integración frontend escrita.

---

## 10. Resumen de archivos (nuevos / tocados)

**Nuevos:**
- `Domain/.../AgeRangeCatalogItem.cs`, `SeniorityRangeCatalogItem.cs`
- `Infrastructure/Persistence/Configurations/.../AgeRangeCatalogItemConfiguration.cs`, `SeniorityRangeCatalogItemConfiguration.cs`
- `Application/Features/PersonnelFiles/Reporting/PersonnelFileDashboard.cs` (queries/handlers/DTOs) + `PersonnelFileDashboard.Rules.cs`
- `Infrastructure/PersonnelFiles/PersonnelFileDashboardRepository.cs` (proyección dimensional + agregaciones)
- Migración `..._AddHrDashboardRangeCatalogsAndPreferences.cs`
- `docs/technical/guia-integracion-frontend-tablero-indicadores-rrhh.md`

**Tocados:**
- `PersonnelFileReportingController.cs` (endpoints dashboard) **o** nuevo `PersonnelFileDashboardController.cs`
- `PersonnelFileCommon.cs` / `PersonnelFilePolicies.cs` / `Program.cs` / `IPersonnelFileAuthorizationService.cs` (+impl) — `ViewReports`
- `GeneralCatalogKeyMap.cs` (+ guardrail test) — wire keys de rangos
- `ApplicationDbContext.cs` (DbSets) + `GlobalCatalogSeedData.cs` (seed SV)
- `Domain/Preferences/CompanyPreference.cs` + config + `CompanyPreferencesController.cs` (+ handlers) — parametrización
- `AuthorizationPolicyConventionGovernanceTests.cs` — `ViewReports`/dashboard
