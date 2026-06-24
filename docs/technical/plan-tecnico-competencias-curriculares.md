# Plan Técnico — Competencias Curriculares del Empleado (Fase 1: endurecimiento)

| | |
|---|---|
| **Tipo de documento** | Plan técnico / Diseño de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA, DevOps |
| **Documento de negocio** | `docs/business/analisis-competencias-curriculares-empleado.md` (v2, decisiones **D-01…D-08**, 2026-06-23) |
| **Módulos** | `PersonnelFiles` (área *Talent*) · `PositionDescriptionCatalogs` (Estructura Organizativa) · `PersonnelReferenceCatalog` · `Localization` |
| **Estado** | Propuesto — listo para implementar |
| **Fecha** | 2026-06-23 |
| **País de referencia** | El Salvador (SV) |

---

## 1. Objetivo y enfoque

Endurecer la funcionalidad **ya existente** de competencias curriculares (`PersonnelFileCurricularCompetency`) para cumplir las decisiones del negocio:

- **D-01/D-02** — validar el **tipo de requisito** contra el catálogo `PositionDescriptionCatalogType.RequirementType` (Estructura Organizativa).
- **D-03** — validar el **dominio de la competencia** contra un catálogo **configurable por tenant**, usable como lista plana o **escala ordenada** (vía `SortOrder`).
- **D-04** — validar la **métrica** contra un catálogo de unidades **AÑOS / MESES / DIAS / HORAS**.
- **D-05** — **bloquear duplicados** (mismo tipo + nombre, normalizado) por expediente.
- **D-06** — validar **tiempo de experiencia ≥ 0** (admite 0).
- **D-07/D-08** — sin análisis de brecha (futuro), sin evidencias, sin permiso dedicado ni autoservicio (se mantiene `Read`/`Manage`).

> **Decisión arquitectónica central (validar por código, no rediseñar el contrato).** El repositorio tiene **dos** sistemas de catálogo:
> 1. **`PositionDescriptionCatalogItem`** — *tenant-scoped*, módulo Estructura Organizativa; ya alberga `RequirementType`. Hoy se valida **por `PublicId`** vía `IPositionCatalogLookup.GetActiveCatalogReferenceAsync` (lo usa JobProfiles).
> 2. **`PersonnelReferenceCatalog` / `PersonnelReferenceCatalogItem`** — *country-scoped*; se valida **por código** vía `IPersonnelFileRepository.CatalogCodeIsActiveAsync` (lo usan Seguros, Equipo/Acceso, etc.).
>
> Como esto es un **endurecimiento** (no un rediseño), se conserva el contrato actual: `RequirementTypeCode`, `CompetencyDomain` y `MetricCode` **siguen siendo códigos string**. Para validar los catálogos *tenant-scoped* por código se agrega un resolver `GetActiveCatalogReferenceByCodeAsync` al `IPositionCatalogLookup` (apoyado en el índice único `(TenantId, CatalogType, NormalizedCode)`). Así:
> - **Tipo de requisito** → `PositionDescriptionCatalogType.RequirementType` (existente), validado por código.
> - **Dominio** → **nuevo** `PositionDescriptionCatalogType.CompetencyDomain` (tenant, con `SortOrder` para escala), validado por código.
> - **Métrica** → **nueva** categoría country-scoped `ExperienceMetric` (`PersonnelReferenceCatalog`), validada por código.
>
> *(Alternativa considerada y descartada para esta fase: migrar `RequirementTypeCode`/`CompetencyDomain` a referencias por `PublicId`+snapshot, como `JobProfileRequirement.RequirementTypeCatalogItemId`. Es más “FK-correcto” pero rompe el contrato y la fila existente; se documenta como evolución futura — ver §8 R-T4.)*

---

## 2. Línea base verificada en el código

| Componente | Ubicación | Nota |
|---|---|---|
| Entidad | `Domain/PersonnelFiles/PersonnelFileEmployee.cs:1654` (`PersonnelFileCurricularCompetency`) | `Create`/`Update` solo hacen `Clean()`; sin validación de catálogo. |
| EF config | `Infrastructure/Persistence/Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs:549` | FK única a `personnel_files`; índice **no único** `(tenant_id, personnel_file_id, requirement_type_code)`; sin FK a catálogos. |
| Commands/validators/PATCH | `Application/Features/PersonnelFiles/Talent/CurricularCompetencies.cs` | `CurricularCompetencyInputValidator` (`:85`): solo `NotEmpty`+`MaxLength`. |
| Handlers | `Application/Features/PersonnelFiles/Talent/CurricularCompetencies.Handlers.cs` | Add `:20`, Update `:87`, Patch `:167`, Delete `:266`; gate `IsCompletedEmployee` `:46`. **No** valida catálogos. |
| Repositorio (abstracción) | `Application/Abstractions/PersonnelFiles/IPersonnelFileEmployeeRepository.cs:690-722` | Add/Update/Delete/Get(2). |
| Repositorio (impl) | `Infrastructure/PersonnelFiles/PersonnelFileEmployeeRepository.cs:1559-1627` + `Map` `:2042` | `Update` mapea inline; **no** hay query anti-duplicado (debe vivir en el handler). |
| Controller | `Api/Controllers/PersonnelFileTalentController.cs:534-702`; policy `:26` | 6 endpoints; `[AuthorizationPolicySet(Read, Manage)]`. Sin cambios (D-08). |
| Contratos | `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:583-616` | Add/Update/Patch requests. |
| Catálogo tipo de requisito (existe) | `Domain/PositionDescriptionCatalogs/PositionDescriptionCatalogEnums.cs:9` (`RequirementType=5`) | Entidad `PositionDescriptionCatalogItem` (Code/Name/SortOrder/IsActive). |
| Lookup tenant-scoped | `Application/Abstractions/PositionDescriptionCatalogs/IPositionCatalogLookup.cs:16` (`GetActiveCatalogReferenceAsync` **por PublicId**) | Impl `Infrastructure/PositionDescriptionCatalogs/PositionDescriptionCatalogRepository.cs:425`. |
| Precedente cross-módulo | `Application/Features/JobProfiles/JobProfileRequirementAdministration.cs:970` (`ResolveRequirementTypeInternalIdAsync`) | JobProfiles valida `RequirementType` por PublicId. |
| Catálogo country-scoped | `Domain/PersonnelFiles/PersonnelReferenceCatalog.cs` (`BuildItems`, `PersonnelReferenceCatalogCategories`) | Validación por código `IPersonnelFileRepository.CatalogCodeIsActiveAsync`. |
| Validación catálogo (helper) | `Application/Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` (`PersonnelReferenceCatalogValidation`, `ResolveCompanyCountryCodeAsync:407`) | Patrón a reusar para métrica. |
| Reglas/errores (plantilla) | `Application/Features/PersonnelFiles/Employment/AssetAccess.Rules.cs`; `…/Compensation/Insurances.Rules.cs` | Errores `Error` + reglas puras `Result`. |
| Localización | `Infrastructure/Localization/BackendMessages.resx` / `.es.resx` | Paridad EN/ES; test `BackendMessageLocalizationTests`. |
| Seed dev | `Infrastructure/Persistence/DevSeedService.cs` | **No** siembra competencias curriculares (riesgo R1 bajo en dev). |
| DI | `Application/DependencyInjection.cs:28` | Auto-registro por escaneo de ensamblado (handlers/validators). |

---

## 3. Arquitectura de la solución

### 3.1 Catálogos

**(a) Tipo de requisito — `RequirementType` (ya existe).** No se crea nada; se valida por código (§3.2). El catálogo se administra vía `PositionDescriptionCatalogItemsController` (slug `requirement-types`).

**(b) Dominio — nuevo `PositionDescriptionCatalogType.CompetencyDomain` (tenant-scoped, soporta escala).** Reusa toda la maquinaria existente (entidad, EF, controller, CQRS, `SortOrder`, `IsActive`). Tocar **exactamente** estos puntos (todos verificados):

1. `Domain/PositionDescriptionCatalogs/PositionDescriptionCatalogEnums.cs` → agregar `CompetencyDomain = 14`.
2. `Api/Controllers/PositionDescriptionCatalogRouteMap.cs:7-23` → `["competency-domains"] = PositionDescriptionCatalogType.CompetencyDomain`.
3. `Application/Features/PositionDescriptionCatalogs/Common/PositionDescriptionCatalogCommon.cs:146` → agregar `CompetencyDomain` a `IsSimpleCatalogType(...)`.
4. `Application/Features/JobProfileCatalogTypes/JobProfileCatalogBindingMap.cs:65-80` → agregar `new(CatalogFamilies.PositionDescription, "CompetencyDomain", "Competency Domain", "competency-domains")`.
5. `Application/Features/PositionDescriptionCatalogs/PositionDescriptionCatalogPatchAdministration.cs:232` → agregar caso `PositionDescriptionCatalogType.CompetencyDomain => Task.FromResult(false)` en `IsCatalogItemInUseAsync` (ver R-T3: se prefiere **inactivar** sobre borrar; no se crea dependencia inversa a PersonnelFiles).
6. `tests/CLARIHR.Application.UnitTests/PositionDescriptionCatalogRouteMapTests.cs:10-22,51-63` → `[InlineData("competency-domains", …)]` (TryResolve + ToSlug) y actualizar el set esperado de `Slugs_ShouldBeTheCanonicalResolvableSet_ForOpenApiContract`.
7. *(Opcional)* `Infrastructure/PositionDescriptionCatalogs/PositionDescriptionCatalogSeedService.cs` → seed por tenant (p. ej. `BASICO/INTERMEDIO/AVANZADO/EXPERTO` con `SortOrder` 10/20/30/40) para ofrecer una **escala** por defecto. Confirmar con negocio si se siembra o se deja vacío.

**(c) Métrica — nueva categoría country-scoped `ExperienceMetric`.** Reusa `PersonnelReferenceCatalog`:

1. `Domain/PersonnelFiles/PersonnelReferenceCatalog.cs` → `PersonnelReferenceCatalogCategories.ExperienceMetric = "ExperienceMetric"`; en `BuildItems()` un `AddSimpleCategory(..., [("ANOS","Años"),("MESES","Meses"),("DIAS","Días"),("HORAS","Horas")])` (IDs negativos como el resto del seed).
2. *(Reutilizar el item-base existente)* La métrica es una **categoría plana** dentro de `personnel_reference_catalog_items`; **no** requiere entidad/tabla nuevas si la categoría se modela como fila de `PersonnelReferenceCatalogItem` (índice único `(country_code, category, code)`). *(Confirmar en §3.7 si este catálogo usa la tabla compartida `personnel_reference_catalog_items` o tablas por-catálogo tipo `*_catalog_items`; seguir el patrón vigente del repo para categorías planas — el de InsuranceType usa tabla compartida por categoría.)*
3. `Application/Features/PersonnelFiles/Catalogs/PersonnelReferenceCatalogs.cs` → helper `ValidateExperienceMetricCodeAsync(repository, companyId, "metricCode", code, ct)` envolviendo `ValidateOptionalReferenceCodeForCompanyAsync(..., ExperienceMetric, ...)`.
4. **Wire-key** para el dropdown del front: `GeneralCatalogKeyMap` → `["experience-metrics"] = PersonnelReferenceCatalogCategories.ExperienceMetric`.
5. **Seed productivo**: migración con `InsertData`/`HasData` (SV) — el `DevSeedService` **no** backfillea bases ya provisionadas (lección de [insurance-range-seed]). Public IDs deterministas con `GlobalCatalogSeedData.CreateSeedPublicId("EXPERIENCE_METRIC","SV:ANOS")`.

### 3.2 Resolver por código para catálogo tenant-scoped (tipo y dominio)

Agregar a `Application/Abstractions/PositionDescriptionCatalogs/IPositionCatalogLookup.cs`:

```csharp
Task<CatalogReferenceInternal?> GetActiveCatalogReferenceByCodeAsync(
    Guid tenantId,
    PositionDescriptionCatalogType catalogType,
    string code,
    CancellationToken cancellationToken);
```

Impl en `Infrastructure/PositionDescriptionCatalogs/PositionDescriptionCatalogRepository.cs` (espejo del `…ByIdAsync` existente, filtrando por `NormalizedCode`):

```csharp
public Task<CatalogReferenceInternal?> GetActiveCatalogReferenceByCodeAsync(
    Guid tenantId, PositionDescriptionCatalogType catalogType, string code, CancellationToken ct)
{
    var normalized = PositionDescriptionCatalogNormalization.NormalizeCode(code); // mismo normalizador que NormalizedCode
    return dbContext.PositionDescriptionCatalogItems
        .AsNoTracking()
        .Where(i => i.TenantId == tenantId && i.CatalogType == catalogType
                    && i.NormalizedCode == normalized && i.IsActive)
        .Select(i => new CatalogReferenceInternal(i.Id, i.PublicId, i.Code, i.Name, i.IsActive))
        .SingleOrDefaultAsync(ct);
}
```

Apoyado en el índice único `uq_position_description_catalog_items__tenant_type_code` → resolución O(1). Devuelve el `Name` por si se desea snapshot (no obligatorio, §3.3).

### 3.3 Entidad, normalización y anti-duplicado

En `PersonnelFileCurricularCompetency` (`PersonnelFileEmployee.cs:1654`):

- **Agregar** `public string NormalizedRequirementName { get; private set; }` mantenido en `Create`/`Update`:
  `NormalizedRequirementName = PersonnelFileNormalization.NormalizeKey(requirementName)` (Trim + InvariantUpper; usar el helper de normalización vigente).
- **(Opcional, recomendado por claridad)** Renombrar `CompetencyDomain` → `CompetencyDomainCode` (columna `competency_domain` → `competency_domain_code`) para alinear con `RequirementTypeCode`/`MetricCode`. Es un *rename* (preserva datos). Si se descarta, mantener el nombre y documentar que su valor es un **código de catálogo**.
- **Snapshot de nombres (opcional):** la respuesta puede seguir devolviendo solo códigos; el front ya consume los endpoints de catálogo para poblar los dropdowns y puede resolver el nombre. Snapshot (`RequirementTypeName`/`CompetencyDomainName`) solo si se quiere resiliencia histórica ante renombres — fuera del núcleo de esta fase.

**Anti-duplicado (D-05):** doble defensa, como recomienda el negocio:
- **App-level (UX):** regla pura `CurricularCompetencyRules.CheckDuplicate(...)` sobre los hermanos ya cargados (espejo de `InsuranceRules.CheckPolicyUnique`), devolviendo `CURRICULAR_COMPETENCY_DUPLICATE` (error amigable).
- **DB (backstop):** índice **único** `(tenant_id, personnel_file_id, requirement_type_code, normalized_requirement_name)` (§3.7). `requirement_type_code` se persiste en su forma canónica del catálogo (el `Code` resuelto), de modo que la clave sea estable.

### 3.4 Módulo de reglas puro + errores

Nuevo `Application/Features/PersonnelFiles/Talent/CurricularCompetencies.Rules.cs` (namespace `CLARIHR.Application.Features.PersonnelFiles`), espejo de `AssetAccess.Rules.cs`/`Insurances.Rules.cs`:

```csharp
internal static class CurricularCompetencyErrors
{
    public static readonly Error RequirementTypeInvalid = new(
        "CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID",
        "The requirement type code is not valid for the active Organizational-Structure catalog.",
        ErrorType.UnprocessableEntity);
    public static readonly Error DomainInvalid = new(
        "CURRICULAR_COMPETENCY_DOMAIN_INVALID",
        "The competency domain code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);
    public static readonly Error MetricInvalid = new(
        "CURRICULAR_COMPETENCY_METRIC_INVALID",
        "The metric code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);
    public static readonly Error MetricRequired = new(
        "CURRICULAR_COMPETENCY_METRIC_REQUIRED",
        "A metric is required when an experience time value is provided.",
        ErrorType.UnprocessableEntity);
    public static readonly Error ExperienceNegative = new(
        "CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE",
        "The experience time value cannot be negative.",
        ErrorType.UnprocessableEntity);
    public static readonly Error Duplicate = new(
        "CURRICULAR_COMPETENCY_DUPLICATE",
        "The employee already has a curricular competency with the same requirement type and name.",
        ErrorType.Conflict);
}

internal static class CurricularCompetencyRules
{
    internal sealed record Existing(Guid PublicId, string Key);

    public static string Key(string requirementTypeCode, string requirementName) =>
        $"{requirementTypeCode.Trim().ToUpperInvariant()}|{requirementName.Trim().ToUpperInvariant()}";

    public static Result ValidateExperience(decimal? value, string? metricCode)
    {
        if (value is < 0m) return Result.Failure(CurricularCompetencyErrors.ExperienceNegative);
        if (value is not null && string.IsNullOrWhiteSpace(metricCode))
            return Result.Failure(CurricularCompetencyErrors.MetricRequired); // confirmar (open-Q2)
        return Result.Success();
    }

    public static Result CheckDuplicate(Guid? candidate, string requirementTypeCode, string requirementName,
        IReadOnlyCollection<Existing> siblings)
    {
        var key = Key(requirementTypeCode, requirementName);
        return siblings.Any(s => s.PublicId != candidate && s.Key == key)
            ? Result.Failure(CurricularCompetencyErrors.Duplicate)
            : Result.Success();
    }
}
```

> `MetricRequired` está sujeto a confirmación (negocio, open-Q2 del análisis). Si se decide que la métrica es siempre opcional, se elimina esa rama (y su error).

### 3.5 Validación de comandos (orquestación impura)

Nuevo `CurricularCompetencyCommandValidation.ValidateAsync(...)` (espejo de `AssetAccessCommandSupport.ValidateAsync` / `InsuranceCommandValidation.ValidateAsync`), que combina catálogos + reglas puras:

```csharp
internal static class CurricularCompetencyCommandValidation
{
    public static async Task<Result> ValidateAsync(
        IPositionCatalogLookup positionCatalog,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        PersonnelFile file,
        Guid? candidatePublicId,
        CurricularCompetencyInput input,
        CancellationToken ct)
    {
        // D-01/D-02: tipo de requisito (tenant catalog, por código)
        var type = await positionCatalog.GetActiveCatalogReferenceByCodeAsync(
            file.TenantId, PositionDescriptionCatalogType.RequirementType, input.RequirementTypeCode, ct);
        if (type is null) return Result.Failure(CurricularCompetencyErrors.RequirementTypeInvalid);

        // D-03: dominio (tenant catalog, por código)
        var domain = await positionCatalog.GetActiveCatalogReferenceByCodeAsync(
            file.TenantId, PositionDescriptionCatalogType.CompetencyDomain, input.CompetencyDomain, ct);
        if (domain is null) return Result.Failure(CurricularCompetencyErrors.DomainInvalid);

        // D-04: métrica (country catalog, por código; opcional)
        if (!string.IsNullOrWhiteSpace(input.MetricCode))
        {
            var metricError = await PersonnelReferenceCatalogValidation.ValidateExperienceMetricCodeAsync(
                personnelFileRepository, file.TenantId, "metricCode", input.MetricCode!, ct);
            if (metricError != Error.None) return Result.Failure(metricError);
        }

        // D-06 (+ open-Q2): experiencia >= 0 y métrica requerida si hay experiencia
        var range = CurricularCompetencyRules.ValidateExperience(input.ExperienceTimeValue, input.MetricCode);
        if (range.IsFailure) return range;

        // D-05: anti-duplicado (hermanos cargados)
        var siblings = (await employeeRepository.GetCurricularCompetenciesAsync(file.PublicId, ct))
            .Select(e => new CurricularCompetencyRules.Existing(
                e.CurricularCompetencyPublicId,
                CurricularCompetencyRules.Key(e.RequirementTypeCode, e.RequirementName)))
            .ToArray();
        return CurricularCompetencyRules.CheckDuplicate(
            candidatePublicId, type.Code, input.RequirementName, siblings); // usa el Code canónico del catálogo
    }
}
```

> Nota: se usa `type.Code` (forma canónica del catálogo) para construir/persistir `RequirementTypeCode`, garantizando que el índice único anti-duplicado sea estable independientemente de mayúsculas/espacios del input.

### 3.6 Handlers (Add / Update / Patch)

En `CurricularCompetencies.Handlers.cs`, inyectar `IPositionCatalogLookup positionCatalog` en los 3 handlers de escritura (Add/Update/Patch) y llamar la validación **antes de persistir**:

- **Add** (`:34-60`): tras el gate `IsCompletedEmployee`, antes de `PersonnelFileCurricularCompetency.Create(...)`.
- **Update** (`:118-141`): tras el chequeo de concurrencia, antes de `UpdateCurricularCompetencyAsync(...)`.
- **Patch** (`:209-240`): tras `PatchApplier.Validate(state)` y antes de `UpdateCurricularCompetencyAsync(...)`, pasando `state.ToInput()`.

Patrón (Add):

```csharp
var validation = await CurricularCompetencyCommandValidation.ValidateAsync(
    positionCatalog, personnelFileRepository, employeeRepository,
    personnelFile, candidatePublicId: null, command.Item, cancellationToken);
if (validation.IsFailure)
    return Result<PersonnelFileCurricularCompetencyResponse>.Failure(validation.Error);
```

En Update/Patch, `candidatePublicId: command.CurricularCompetencyPublicId` (excluye la propia fila del anti-duplicado). DI: auto-registro por escaneo (`DependencyInjection.cs:28`); `IPositionCatalogLookup` ya está registrado (lo usa JobProfiles) → sin wiring manual.

### 3.7 EF config, índices y check constraint

En `PersonnelFileCurricularCompetencyConfiguration` (`PersonnelFileEmployeeConfiguration.cs:549`):

- Mapear `NormalizedRequirementName` (`normalized_requirement_name`, `HasMaxLength(200)`).
- *(Si se renombra)* `CompetencyDomainCode` → `competency_domain_code`.
- **Reemplazar** el índice no-único `ix_…__tenant_file_requirement_type` por el **único**:
  ```csharp
  builder.HasIndex(i => new { i.TenantId, i.PersonnelFileId, i.RequirementTypeCode, i.NormalizedRequirementName })
      .IsUnique()
      .HasDatabaseName("uq_personnel_file_curricular_competencies__tenant_file_type_name");
  ```
- Check constraint de rango (espejo de AssetAccess):
  ```csharp
  builder.ToTable(t => t.HasCheckConstraint(
      "ck_personnel_file_curricular_competencies__experience_nonneg",
      "experience_time_value is null or experience_time_value >= 0"));
  ```

**Migración** `2026MMDDhhmmss_CurricularCompetenciesCatalogsAndHardening.cs` (+ `.Designer.cs` + snapshot):
1. Tabla/seed del catálogo **dominio**: ninguno nuevo (reusa `position_description_catalog_items`); el seed es opcional vía `PositionDescriptionCatalogSeedService` (no migración).
2. Catálogo **métrica** (`ExperienceMetric`): `InsertData` SV (AÑOS/MESES/DIAS/HORAS) en la tabla de referencia con índice único `(country, category, code)`.
3. Alterar `personnel_file_curricular_competencies`: `AddColumn normalized_requirement_name`; *(opcional)* `RenameColumn competency_domain → competency_domain_code`.
4. **Backfill** (§4) de `normalized_requirement_name` con `UPDATE … SET normalized_requirement_name = upper(trim(requirement_name))`.
5. `DropIndex ix_…__tenant_file_requirement_type`; `CreateIndex` único `(tenant_id, personnel_file_id, requirement_type_code, normalized_requirement_name)`.
6. `AddCheckConstraint ck_…__experience_nonneg`.

> **Gotcha de tooling** (memoria): `dotnet ef` 9.0.9 requiere `DOTNET_ROLL_FORWARD=Major`. Generar con:
> `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add CurricularCompetenciesCatalogsAndHardening -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api`.

### 3.8 Permisos y API (sin cambios — D-08)

Se mantiene `[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]` y los 6 endpoints. **No** se agrega permiso dedicado ni autoservicio. El `AuthorizationPolicyConventionGovernanceTests` no requiere cambios (no se introduce controller/política nueva). Contratos REST sin cambios (los campos siguen siendo códigos). *(Si se renombra `CompetencyDomain`→`CompetencyDomainCode`, actualizar `AddCurricularCompetencyRequest`/`Update`/`Patch` y el `CurricularCompetencyInput`/respuesta acordemente — es un cambio de contrato menor; coordinar con front. Si se prefiere no romper el contrato, conservar el nombre `competencyDomain`.)*

### 3.9 Localización (paridad EN/ES)

Agregar a `BackendMessages.resx` **y** `BackendMessages.es.resx` los 6 códigos (`CURRICULAR_COMPETENCY_*`). El test `BackendMessageLocalizationTests` escanea `new Error(...)` y exige paridad → ambos archivos obligatorios.

| Código | ES |
|---|---|
| `CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID` | El tipo de requisito no es válido en el catálogo de Estructura Organizativa activo. |
| `CURRICULAR_COMPETENCY_DOMAIN_INVALID` | El dominio de la competencia no es válido en el catálogo activo. |
| `CURRICULAR_COMPETENCY_METRIC_INVALID` | La métrica no es válida en el catálogo activo. |
| `CURRICULAR_COMPETENCY_METRIC_REQUIRED` | La métrica es obligatoria cuando se indica un tiempo de experiencia. |
| `CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE` | El tiempo de experiencia no puede ser negativo. |
| `CURRICULAR_COMPETENCY_DUPLICATE` | El empleado ya tiene una competencia curricular con el mismo tipo de requisito y nombre. |

---

## 4. Migración de datos (riesgo R1)

Activar validación estricta **rechazará** filas existentes con códigos fuera de catálogo. Antes de desplegar el PR de handlers:

1. **Auditar** valores presentes:
   ```sql
   select tenant_id, count(*),
          array_agg(distinct requirement_type_code) as types,
          array_agg(distinct competency_domain) as domains,
          array_agg(distinct metric_code) as metrics
   from personnel_file_curricular_competencies group by tenant_id;
   ```
2. **Sembrar** los catálogos con los valores distintos hallados:
   - `RequirementType` y `CompetencyDomain`: por **tenant** (insertar `PositionDescriptionCatalogItem` activos para cada código en uso).
   - `ExperienceMetric`: por **país** (la migración ya siembra AÑOS/MESES/DIAS/HORAS; mapear sinónimos como `AÑOS`↔`ANOS`, `YEARS`→`ANOS` si aparecen).
3. **Backfill** `normalized_requirement_name` (paso 4 de la migración).
4. **Verificar** que no queden filas con código sin catálogo activo antes de habilitar `CheckDuplicate`/validación (consulta de control anti-join).

> En **dev** el riesgo es bajo: `DevSeedService` no crea competencias. En entornos con datos reales, los pasos 1–4 son **obligatorios** y deben ejecutarse en la misma ventana del despliegue del PR-4.

---

## 5. Mapa de errores

| Código | HTTP | Disparador | Regla |
|---|---|---|---|
| `CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID` | 422 | tipo no existe/activo en `RequirementType` (tenant) | D-01/D-02 |
| `CURRICULAR_COMPETENCY_DOMAIN_INVALID` | 422 | dominio no existe/activo en `CompetencyDomain` (tenant) | D-03 |
| `CURRICULAR_COMPETENCY_METRIC_INVALID` | 422 | métrica informada fuera de `ExperienceMetric` (país) | D-04 |
| `CURRICULAR_COMPETENCY_METRIC_REQUIRED` | 422 | hay experiencia sin métrica *(confirmar)* | D-06/open-Q2 |
| `CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE` | 422 | `experienceTimeValue < 0` | D-06 |
| `CURRICULAR_COMPETENCY_DUPLICATE` | 409 | mismo tipo + nombre (normalizado) | D-05 |

---

## 6. Plan de pruebas

- **Unitarias (reglas puras)** — `CurricularCompetencyRulesTests` (espejo de `AssetAccessRulesTests`/`InsuranceRulesTests`): `ValidateExperience` (negativo→falla, 0→ok, experiencia sin métrica→`MetricRequired`), `CheckDuplicate` (distinto→ok, mismo case-insensitive→falla, self→ok, vacío→ok), `Key` normaliza.
- **Unitarias (validación de comandos)** — con dobles de `IPositionCatalogLookup`/repositorios: tipo inválido→422, dominio inválido→422, métrica inválida→422, duplicado→409, camino feliz→ok. Cobertura de Add/Update/Patch.
- **Resolver por código** — test del `GetActiveCatalogReferenceByCodeAsync` (activo/inactivo/otro-tenant/normalización).
- **Route map** — `PositionDescriptionCatalogRouteMapTests` con `competency-domains` (round-trip + set canónico).
- **Localización** — `BackendMessageLocalizationTests` (paridad de los 6 códigos) verde.
- **Integración** — `POST`/`PUT`/`PATCH` con código fuera de catálogo→422; duplicado→409; experiencia negativa→422; `GET` lista de catálogos `competency-domains` y `experience-metrics`.
- **Regresión** — suite completa (objetivo: ~1900+ tests verdes, según líneas base recientes).

---

## 7. Orden de implementación (PRs sugeridos)

| PR | Alcance | Riesgo |
|---|---|---|
| **PR-1 — Catálogos** | (a) `CompetencyDomain` como nuevo `PositionDescriptionCatalogType` (7 puntos de §3.1b) + tests de route map; (b) categoría `ExperienceMetric` (Domain seed + wire-key + migración `InsertData` SV) + helper de validación. **Sin** cambio de comportamiento en competencias. | Bajo |
| **PR-2 — Reglas + errores + localización + resolver** | `CurricularCompetencies.Rules.cs`, `CurricularCompetencyCommandValidation`, `GetActiveCatalogReferenceByCodeAsync` (+impl), 6 entradas resx EN/ES, tests unitarios de reglas. **Sin** tocar handlers todavía. | Bajo |
| **PR-3 — Entidad + persistencia** | `NormalizedRequirementName` (+rename opcional `CompetencyDomainCode`), EF config (índice único + check constraint), migración (alter + backfill + índice + constraint) + snapshot. | Medio (migración) |
| **PR-4 — Cableado de handlers (activa validación)** | Inyectar `IPositionCatalogLookup` y llamar `ValidateAsync` en Add/Update/Patch. **Precondición: §4 ejecutado** en el entorno destino. Tests de integración. | Medio (R1) |
| **PR-5 — (Opcional) Guía frontend** | `docs/technical/guia-integracion-frontend-competencias-curriculares.md` (endpoints de catálogo `competency-domains`/`experience-metrics`, nuevos 422/409, semántica lista vs escala). | Bajo |

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Datos legados (R1).** Validación estricta rompe filas con códigos libres. *Mitiga:* §4 (auditar→sembrar→backfill→verificar) como gate de PR-4.
- **R-T2 — Normalización de la clave anti-duplicado.** Si `requirement_type_code` se persiste con variantes de caja, el índice único podría no atrapar duplicados. *Mitiga:* persistir el `Code` canónico del catálogo (`type.Code`) y `normalized_requirement_name` en mayúsculas/trim.
- **R-T3 — Borrado de ítem de catálogo en uso.** Como la referencia es **por código** (sin FK), borrar un `RequirementType`/`CompetencyDomain` en uso dejaría códigos huérfanos. *Decisión:* preferir **inactivar** (patrón `IsActive`/`Inactivate()` existente); el caso `CompetencyDomain` en `IsCatalogItemInUseAsync` retorna `false` para no crear dependencia inversa PositionDescriptionCatalogs→PersonnelFiles. Filas históricas conservan su código (y nombre si se snapshotea). *(Resuelve open-Q5 del análisis.)*
- **R-T4 — Modelo por código vs por referencia.** Se eligió por-código para no romper el contrato. Evolución futura: migrar a `…CatalogItemId`+snapshot (como `JobProfileRequirement`) si se requiere integridad referencial dura y rename-cascade.
- **R-T5 — `MetricRequired` (open-Q2).** Sujeto a confirmación del negocio; si la métrica es siempre opcional, eliminar esa rama y su error antes de PR-2.
- **R-T6 — Cambio de contrato por rename.** El rename `CompetencyDomain→CompetencyDomainCode` toca el API; si se quiere cero-ruptura, mantener `competencyDomain` y solo validar.
- **R-T7 — Tooling EF.** `DOTNET_ROLL_FORWARD=Major` requerido para `dotnet ef` 9.0.9.

---

## 9. Checklist de implementación

**Catálogos (PR-1)**
- [ ] `PositionDescriptionCatalogType.CompetencyDomain = 14`
- [ ] Route slug `competency-domains` + `IsSimpleCatalogType` + `JobProfileCatalogBindingMap` + caso `IsCatalogItemInUseAsync` + tests route map
- [ ] `PersonnelReferenceCatalogCategories.ExperienceMetric` + seed `BuildItems` (AÑOS/MESES/DIAS/HORAS)
- [ ] Wire-key `experience-metrics` + helper `ValidateExperienceMetricCodeAsync`
- [ ] Migración `InsertData` SV métrica (+ public IDs deterministas)

**Reglas/validación (PR-2)**
- [ ] `CurricularCompetencies.Rules.cs` (errores + reglas puras)
- [ ] `CurricularCompetencyCommandValidation.ValidateAsync`
- [ ] `IPositionCatalogLookup.GetActiveCatalogReferenceByCodeAsync` (+impl)
- [ ] 6 códigos en `BackendMessages.resx` + `.es.resx`
- [ ] Tests unitarios de reglas + resolver

**Persistencia (PR-3)**
- [ ] `NormalizedRequirementName` (+rename opcional) en entidad + `Create`/`Update`
- [ ] EF config: índice único `(tenant,file,type,normalized_name)` + check `experience>=0`
- [ ] Migración (alter + backfill + índice + constraint) + `.Designer` + snapshot

**Handlers (PR-4)**
- [ ] Inyectar `IPositionCatalogLookup` en Add/Update/Patch
- [ ] Llamar `ValidateAsync` antes de persistir (candidate excl. en Update/Patch)
- [ ] §4 ejecutado en el entorno destino (auditar→sembrar→backfill→verificar)
- [ ] Tests de integración (422/409 + camino feliz)

**Cierre**
- [ ] Suite completa verde
- [ ] (Opcional) Guía frontend (PR-5)
- [ ] Actualizar `docs/business/analisis-competencias-curriculares-empleado.md` (estado → implementado)
