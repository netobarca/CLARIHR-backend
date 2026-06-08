# Auditoría Técnica por Controlador — OrgStructureCatalogsController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Relacionada: [doc 15 — OrgUnitsController](15-org-units-controller-audit-2026-06-07.md) (consume estos catálogos).

## 1. Resumen ejecutivo

`OrganizationStructureCatalogsController` (renombrado desde `OrgStructureCatalogsController` en OSC-008) administra los **catálogos de referencia de la estructura organizativa**: **tipos de unidad** (`unit-types`) y **áreas funcionales** (`functional-areas`), con CRUD + activar/inactivar por catálogo (10 endpoints). Estos catálogos alimentan a `OrgUnits` (tipo y área funcional de cada unidad). El dominio incluye además `CompanyType`, pero **no se expone en este controlador** (sólo lecturas para creación de empresa → fuera de alcance).

Veredicto: **APROBADO CON OBSERVACIONES**. 0 críticos / 0 altos. La **seguridad es correcta**: los 10 handlers aplican el gate de autorización (`EnsureCanReadTenantAsync`/`EnsureCanManageTenantAsync` con tenant-match + entitlement de módulo + RBAC), el aislamiento por tenant es fail-closed, la auditoría usa constantes y la integridad referencial se respeta al inactivar. **Sin embargo, es el controlador menos canónico del área**: a diferencia de `OrgUnits`, carece de `[ApiVersion]`, `[Tags]`, **`[AuthorizationPolicySet]`**, `[SwaggerOperation]` y `[ProducesStandardErrors]`, usa **token de concurrencia en el body** (no `If-Match`/ETag), y su `Create` no devuelve `Location`/`ETag`. Esto responde directamente a la observación del usuario: **no es coherente con los demás endpoints y debería alinearse a la estructura canónica** (ver §6.3 y §11). El hallazgo material adicional es un **N+1 (Media)** en el enriquecimiento `includeAllowedActions`.

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila (ver nota de build) |
| Unit tests (OrgStructureCatalog + guardrails, **ejecutados**) | ✅ 53/53 (auditoría) → **66/66** (re-ejecutado 2026-06-08 post-remediación, filtro OrgStructureCatalog+guardrails) |
| Integration tests dedicados | **7** (2 migrados al nuevo route + 5 nuevos tras OSC-006; requieren DB) |
| Hallazgos | 0 Crít · 0 Alto · **2 Media · 4 Baja · 2 Observación** — ✅ **los 8 resueltos** |
| **Estado de remediación (2026-06-07, ✅ committed en `master`: `95a4770`/`23521c9`/`963d507`/`9d24392`)** | ✅ **LOS 8 HALLAZGOS RESUELTOS** (OSC-001…OSC-008). Alineación canónica completa (incl. ambos breaking: If-Match + rename de ruta). build 0/0 (solución) · unit 1687/1687 · integración compila (tests requieren DB) · **sin migraciones · sin resx**. ⚠️ Breaking: ruta `org-structure-catalogs`→`organization-structure-catalogs` + concurrencia body-token→`If-Match` (frontend debe actualizar). `openapi.yaml`: ✅ **sección regenerada desde el swagger live** (2026-06-08): Tag→`Organization Structure Catalogs`, `If-Match` en los 6 mutadores, `UpsertCatalogItemRequest` en PUT, sin body en activate/inactivate, +422/404 + summaries; schema-IDs normalizados al estilo del archivo y huérfanos (`ConcurrencyRequest`/`UpdateCatalogItemRequest`) eliminados; 497/497 `$ref` resuelven, 239 paths intactos, YAML válido. |

> **Nota de build (fuera de alcance):** durante la sesión se observó una edición **en curso, sin commitear**, en la vertical de **Locations** (`LocationHierarchyAdministration.cs`, `LocationLevelsController.cs`) que **elimina el endpoint JSON-Patch de LocationLevels** (acción, `PatchLocationLevelCommand`, `LocationLevelPatchOperation`, `LocationLevelPatchApplier`/`State` y sus tests). Provocó un break de compilación transitorio; al finalizar la edición el build y los 53 tests relevantes quedaron verdes. **No forma parte de OrgStructureCatalogs**; se reporta por transparencia.

## 2. Alcance

**Incluido:** controlador `OrganizationStructureCatalogsController.cs` (renombrado en OSC-008); aplicación `OrgStructureCatalogsAdministration.cs` (DTOs, 10 queries/commands, validators, 10 handlers, `OrgStructureCatalogPolicyAdapter`); dominio `OrgUnitTypeCatalogItem.cs`, `FunctionalAreaCatalogItem.cs`, `OrgStructureCatalogNormalization.cs`; persistencia `IOrgStructureCatalogRepository` + `OrgStructureCatalogRepository`, EF `OrgStructureCatalogsConfiguration`; seguridad `OrgStructureCatalogPermissionCodes`, `IOrgStructureCatalogAuthorizationService` + impl; provisión `OrgStructureCatalogSeedService`; errores/i18n `OrgStructureCatalogCommon.cs` + resx; pruebas `OrgStructureCatalogDomainTests` + integración.

**Excluido:** `CompanyType` (catálogo de dominio leído por creación de empresa, no gestionado aquí); `OrgUnits` salvo como consumidor (doc 15); auditoría integral de la API; pruebas de carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en seguridad (gate por handler, tenant), integridad referencial (inactivación), concurrencia y **adherencia al estándar canónico** del proyecto (comparado contra `OrgUnits`/`Locations`/`CostCenters`). Evidencia de ejecución: 53 unit tests ejecutados (verde). Integración revisada por código (requiere DB; no ejecutada → limitación).

## 4. Inventario de endpoints

| # | Método | Ruta | Catálogo | Handler |
|---|---|---|---|---|
| 1 | GET | `/companies/{companyId}/org-structure-catalogs/unit-types` | UnitTypes | `SearchOrgUnitTypesQueryHandler` |
| 2 | GET | `/org-structure-catalogs/unit-types/{id}` | UnitTypes | `GetOrgUnitTypeByIdQueryHandler` |
| 3 | POST | `/companies/{companyId}/org-structure-catalogs/unit-types` | UnitTypes | `CreateOrgUnitTypeCommandHandler` |
| 4 | PUT | `/org-structure-catalogs/unit-types/{id}` | UnitTypes | `UpdateOrgUnitTypeCommandHandler` |
| 5 | PATCH | `/org-structure-catalogs/unit-types/{id}/activate` | UnitTypes | `ActivateOrgUnitTypeCommandHandler` |
| 6 | PATCH | `/org-structure-catalogs/unit-types/{id}/inactivate` | UnitTypes | `InactivateOrgUnitTypeCommandHandler` |
| 7 | GET | `/companies/{companyId}/org-structure-catalogs/functional-areas` | FunctionalAreas | `SearchFunctionalAreasQueryHandler` |
| 8 | GET | `/org-structure-catalogs/functional-areas/{id}` | FunctionalAreas | `GetFunctionalAreaByIdQueryHandler` |
| 9 | POST | `/companies/{companyId}/org-structure-catalogs/functional-areas` | FunctionalAreas | `CreateFunctionalAreaCommandHandler` |
| 10 | PUT/PATCH | `.../functional-areas/{id}` + `/activate` + `/inactivate` | FunctionalAreas | Update/Activate/Inactivate handlers |

Patrón id-only para mutaciones (tenant del JWT); sin `DELETE` (soft-delete vía `/inactivate`). Catálogos auto-provisionados por `OrgStructureCatalogSeedService` (3 tipos + 4 áreas por defecto).

> **Nota (post-remediación):** la tabla muestra el prefijo **auditado** `org-structure-catalogs`. Tras OSC-008 el prefijo es **`organization-structure-catalogs`** (breaking); además, tras OSC-001 los mutadores reciben el token de concurrencia vía header **`If-Match`** (no en el body) y `Create` responde **201 + `Location` + `ETag`**.

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / capas / DTOs | PASS | Sólo despacha CQRS; records cerrados |
| Arquitectura | Transacciones en escrituras | PASS | Los 8 commands: transacción + 2 SaveChanges + audit |
| Seguridad | Autenticación | PASS | `[Authorize]` |
| Seguridad | Autorización por handler (Read/Manage) | PASS | **Los 10 handlers** invocan `EnsureCanReadTenantAsync/EnsureCanManageTenantAsync` como 1er paso |
| Seguridad | Declarativa `[AuthorizationPolicySet]` | **PASS ✅** | **OSC-001/OSC-003 resuelto**: `[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, .Manage)]` + wired en `Program.cs` (superset del gate) + `GovernedFamilyRegex` |
| Seguridad | BOLA/IDOR + Tenant isolation | PASS | id-only routes (tenant del JWT); filtro global fail-closed; `ExistsOutsideTenant` → 404 vs 403 |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(CommercialModuleKeys.OrgStructureCatalogs)` |
| Seguridad | Integridad referencial (inactivar) | PASS | UnitType: `HasOrgUnitsUsing…`/`HasPositionCategoryClassificationsUsing…`; FA: `HasOrgUnitsUsing…` → 409 `CatalogInUse` |
| Seguridad | Mass assignment | PASS | DTOs cerrados |
| Contrato | Versionado `[ApiVersion]` | **PASS ✅** | **OSC-001 resuelto**: `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}")]` |
| Contrato | `[Tags]` (grupo Swagger) | **PASS ✅** | **OSC-001/OSC-008 resuelto**: `[Tags("Organization Structure Catalogs")]` + enrolado en `OpenApiContractGuardrailsTests.Families` |
| Contrato | `[SwaggerOperation]` docs | **PASS ✅** | **OSC-001 resuelto**: `[SwaggerOperation]` (Summary/Description) en los 10 endpoints |
| Contrato | Error contract `[ProducesStandardErrors]` | **PASS ✅** | **OSC-001 resuelto**: `[ProducesStandardErrors(StandardErrorSet.*)]` |
| Contrato | 201 + Location + ETag en Create | **PASS ✅** | **OSC-001 resuelto**: `ToCreatedAtActionResult` (201 + Location + ETag) |
| Contrato | ETag / If-Match en updates | **PASS ✅** | **OSC-001 resuelto**: `[FromIfMatch] Guid concurrencyToken` + `ToActionResultWithETag` (token de body eliminado) |
| Contrato | Paginación: page size máx | PASS | Validators `InclusiveBetween(1,100)` + `[Range(1, MaxPageSize)]` en el param + `OrgStructureCatalogPaginationGuardrailsTests` |
| Contrato | Search: longitud mínima | **PASS ✅** | **OSC-004 resuelto**: `MinSearchLength=2` en ambos validators + `OrgStructureCatalogSearchValidatorTests` |
| Rendimiento | Índices | PASS | `uq_(tenant,normalized_code)` + `(tenant,name)`/`(tenant,active)` por catálogo |
| Rendimiento | N+1 | **PASS ✅** | **OSC-002 resuelto**: `GetOrgUnitTypePublicIdsInUseAsync`/`GetFunctionalAreaPublicIdsInUseAsync` batch set-based (1 query) |
| Rendimiento | Rate limiting | **PASS ✅** | **OSC-007 resuelto**: `[EnableRateLimiting(OrgStructureCatalogRateLimitPolicies.Search)]` (120) + `Program.cs` + governance test |
| Concurrencia | Optimista + 409 | PASS | Token rotado; check manual→409; `.IsConcurrencyToken()` (auto-guardrail); race→409 vía middleware |
| Concurrencia | Unique-constraint en Create → 409 | **PASS ✅** | **OSC-005 resuelto**: catch `UniqueConstraintViolationException`→409 (Create+Update ×2; nombres de índice single-sourced) |
| Observabilidad | Audit logs + **constantes** | PASS | Los 8 commands con `AuditEventTypes.*`/`AuditEntityTypes.*` (✅ mejor que OrgUnits) |
| Gobernanza | Enrolado en guardrails de familia | **PASS ✅** | **OSC-003 resuelto**: `Families` + `GovernedFamilyRegex` + pagination + rate-limit governance tests |
| Pruebas | Unit (dominio) | PASS | `OrgStructureCatalogDomainTests` (62 líneas) |
| Pruebas | Integración | **PASS ✅** | **OSC-006 resuelto**: 7 tests (2 originales migrados + 5 nuevos: 201/Location/ETag, If-Match happy/stale 409, dup-code 409, tenant 403) |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS correcta y consistente internamente; validators separados; dominio con invariantes (sortOrder ≥ 0, normalización code/name); `OrgStructureCatalogPolicyAdapter` para `AllowedActions`. Cada escritura envuelve mutación + auditoría en transacción. El controlador es delgado (sólo despacha). El problema no es la arquitectura interna sino la **adherencia al contrato canónico** (§6.3).

### 6.2 Seguridad
**Correcta a nivel de comportamiento**: el gate de autorización se aplica en los 10 handlers (no hay endpoint sin autz), con tenant-match + entitlement de módulo + claims/grants RBAC que **acepta permisos `OrgStructureCatalogs.*` y `OrgUnits.*`** (sensato: quien administra unidades administra sus catálogos). Aislamiento por tenant fail-closed; `ExistsOutsideTenant` para 404 vs 403. Integridad referencial al inactivar (no se puede inactivar un tipo/área en uso → 409). **Riesgo estructural (no actual)**: al carecer de `[AuthorizationPolicySet]` y no estar en `GovernedFamilyRegex`, la seguridad depende **enteramente** del gate manual del handler; si un futuro handler omitiera el gate, **ningún guardrail ni capa declarativa lo detectaría** (a diferencia de OrgUnits/Locations). Por eso OSC-001/OSC-003 son Media pese a no ser una vulnerabilidad hoy. **✅ Resuelto:** con `[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, .Manage)]` (superset del gate, wired en `Program.cs`) y el enrolamiento en `GovernedFamilyRegex`, un futuro handler que omitiera el gate **sí** fallaría en CI.

### 6.3 Contrato API — desviación del estándar canónico (núcleo de la pregunta del usuario)
Comparado con `OrgUnitsController` y el resto de controllers alineados, `OrgStructureCatalogs` **no sigue la estructura canónica** en 7 dimensiones:

| Aspecto | Estándar (OrgUnits/Locations/CostCenters) | OrgStructureCatalogs | 
|---|---|---|
| Versionado | `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}")]` | ❌ sin `[ApiVersion]`, literal `api/v1` por ruta |
| Swagger group | `[Tags("…")]` | ❌ sin `[Tags]` |
| Autz declarativa | `[AuthorizationPolicySet(Read, Manage)]` | ❌ sólo `[Authorize]` |
| Docs | `[SwaggerOperation(Summary/Description)]` | ❌ ausente |
| Errores | `[ProducesStandardErrors(StandardErrorSet.X)]` | ❌ `[ProducesResponseType]` inline |
| Concurrencia | `If-Match` + `[FromIfMatch]` + `ETag` | ❌ token en el **body** (`ConcurrencyRequest`/`UpdateCatalogItemRequest`) |
| Create | `ToCreatedAtActionResult` (201 + Location + ETag) | ❌ `StatusCode(201, value)` plano |

Es un controlador **pre-canónico**: probablemente escrito antes de la ola de alineación (o nunca alineado). Funciona y es seguro, pero rompe la coherencia del contrato público. **Sí puede —y debería— adoptar la misma estructura que OrgUnits** (ver §11, candidato natural del `api-canonical-alignment-plan`). **✅ Resuelto (2026-06-07):** las 7 dimensiones se alinearon — el controller ahora es canónico (ver §11 y la matriz §9); las dos breaking (ruta y `If-Match`) se adoptaron con decisión explícita del usuario.

### 6.4 Rendimiento
Índices correctos por catálogo. Search `AsNoTracking` + paginado. **N+1 (OSC-002)**: cuando `includeAllowedActions=true`, cada Search itera los ítems de la página y por cada uno ejecuta `GetXByIdAsync` + 1-2 chequeos de dependencia (`HasOrgUnitsUsing…`, `HasPositionCategoryClassificationsUsing…`) → hasta ~60 queries por página de 20. Se debe batchear con una consulta set-based ("cuáles de estos N están en uso"), patrón §12.7/§LG2. Search libre no-sargable sin `MinSearchLength` (OSC-004), aunque la cardinalidad de catálogos es pequeña. **✅ Resuelto:** N+1 batcheado con `GetXPublicIdsInUseAsync` (1 query/página) y `MinSearchLength=2` en ambos validators.

### 6.5 Concurrencia y consistencia
Token rotado en cada mutación; check manual→409; `.IsConcurrencyToken()` en las 3 entidades (auto-cubierto por `ConcurrencyTokenMappingGuardrailsTests`, **verificado verde**); race read-check-write→409 vía middleware. **Pero** el token viaja en el body, no en `If-Match` (OSC-001), y el Create no traduce la violación de unicidad a 409 (OSC-005). **✅ Resuelto:** concurrencia vía `[FromIfMatch]`+ETag (OSC-001) y `UniqueConstraintViolationException`→409 en Create+Update de ambos catálogos (OSC-005).

### 6.6 Observabilidad
**Punto fuerte**: auditoría persistida en los 8 commands usando **constantes** `AuditEventTypes.OrgUnitTypeCatalogItem*`/`FunctionalAreaCatalogItem*` y `AuditEntityTypes.*` (✅ a diferencia de los literales crudos de OrgUnits OU-003). Logging estructurado vía middleware.

### 6.7 Pruebas
**Ejecutadas**: 53 unit tests verdes (dominio de catálogos + guardrails concurrency/OpenAPI/authz). **Revisadas**: sólo **2** integration tests dedicados (`UnitTypes_List_WithOrgUnitsReadFallback` — valida el fallback de permisos OrgUnits; `FunctionalAreas_Inactivate_WhenInUse→409` — integridad referencial). **Gap (OSC-006)**: sin tests de integración para Create/Update/Activate/GetById/code-conflict 409/tenant-mismatch 403/concurrencia en los catálogos. Cubre los dos casos más sutiles, pero es delgado para 10 endpoints. **✅ Resuelto:** ampliado a 7 tests (los 2 migrados al nuevo route + 5 nuevos: 201/Location/ETag, If-Match happy/stale 409, dup-code 409, tenant-mismatch 403).

### 6.8 Build / DevSecOps
Compila (ver nota de build §1 sobre la edición en curso de Locations). Sin secretos hardcodeados. Localización de errores completa (resx en+es).

## 7. Hallazgos

### OSC-001 — Controlador pre-canónico: desviación sistemática del contrato estándar
**Severidad:** Media · **Categoría:** Contrato/Arquitectura · **Ubicación:** `OrgStructureCatalogsController` (todo el archivo) + DTOs `UpsertCatalogItemRequest`/`UpdateCatalogItemRequest`/`ConcurrencyRequest`.
**Condición:** carece de `[ApiVersion]`, `[Tags]`, `[AuthorizationPolicySet]`, `[SwaggerOperation]`, `[ProducesStandardErrors]`; usa token de concurrencia en el body (no `If-Match`/ETag); `Create` devuelve `StatusCode(201, value)` sin `Location`/`ETag` (ver tabla §6.3).
**Criterio esperado:** el contrato canónico que siguen OrgUnits/Locations/CostCenters/LegalRepresentatives.
**Impacto:** incoherencia del contrato público (Swagger sin agrupar ni documentar, sin ETag para caché/concurrencia HTTP estándar, sin Location en Create); además el faltante `[AuthorizationPolicySet]` deja la autz sin capa declarativa ni governance (riesgo estructural de drift).
**Evidencia:** controller líneas 12-18 (sin atributos canónicos), 68-71 (Create plano), 80-96 (Update sin ETag), 259-266 (DTOs con token en body).
**Recomendación:** alinear a canónico (ver §11). Mínimo no-breaking: añadir `[ApiVersion]`+`[Tags]`+`[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, .Manage)]`+`[SwaggerOperation]`+`[ProducesStandardErrors]`, migrar a `If-Match`/`ToActionResultWithETag`/`ToCreatedAtActionResult`, y enrolar en los guardrails.
**Resolución (✅ commits `95a4770`/`23521c9`/`963d507`):** controller renombrado a `OrganizationStructureCatalogsController` con `[ApiVersion("1.0")]`, `[Tags("Organization Structure Catalogs")]`, `[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, .Manage)]` (wired en `Program.cs`, superset del gate), `[SwaggerOperation]` y `[ProducesStandardErrors]` en los 10 endpoints; concurrencia migrada a `[FromIfMatch]`; `Create`→`ToCreatedAtActionResult` (201+Location+ETag); updates→`ToActionResultWithETag`. **Breaking:** ruta `organization-structure-catalogs` + `If-Match`.
**Prioridad:** Alta · **Esfuerzo:** Medio · **Estado:** ✅ Resuelto

### OSC-002 — N+1 en el enriquecimiento `includeAllowedActions`
**Severidad:** Media · **Categoría:** Rendimiento · **Ubicación:** `SearchOrgUnitTypesQueryHandler` líneas 307-324 y `SearchFunctionalAreasQueryHandler` líneas 689-705.
**Condición:** por cada ítem activo de la página se ejecuta `GetXByIdAsync` + 1-2 chequeos de dependencia → hasta ~3 queries/ítem (≈60 por página de 20).
**Criterio esperado:** resolución batch/set-based de dependencias (patrón §12.7/§LG2; OrgUnits lo evita usando un flag fijo en la lista).
**Impacto:** latencia y carga DB proporcionales al page size cuando el flag está activo.
**Recomendación:** una consulta set-based `WHERE catalogItemId IN (@ids) AND IsActive GROUP BY` para marcar dependencias en un solo viaje; o proyectar el id interno + `hasDependencies` en el Search.
**Resolución (✅ commit `963d507`):** ambos Search resuelven dependencias con `GetOrgUnitTypePublicIdsInUseAsync(activeIds)` / `GetFunctionalAreaPublicIdsInUseAsync(activeIds)` — una sola query set-based por página (patrón §12.7/§LG2).
**Prioridad:** Media · **Esfuerzo:** Medio · **Estado:** ✅ Resuelto

### OSC-003 — No enrolado en los guardrails drift-proof de la familia
**Severidad:** Baja · **Categoría:** Seguridad/Gobernanza · **Ubicación:** guardrails de `tests/CLARIHR.Application.UnitTests`.
**Condición:** OrgStructureCatalogs no está en `OpenApiContractGuardrailsTests.Families` (no tiene Tag), ni en `AuthorizationPolicyConventionGovernanceTests.GovernedFamilyRegex`, ni tiene guardrail de paginación/rate-limit. Sólo el concurrency-token está auto-cubierto.
**Criterio esperado:** los controllers gobernados se enrolan para que el drift falle en CI.
**Impacto:** una regresión de contrato/autz no sería detectada automáticamente.
**Recomendación:** tras OSC-001, enrolar en `Families` (Tag) y `GovernedFamilyRegex`, y añadir guardrails de paginación/rate-limit si aplica.
**Resolución (✅ commit `963d507`):** enrolado en `OpenApiContractGuardrailsTests.Families` (`OrganizationStructureCatalogs` → "Organization Structure Catalogs") y `AuthorizationPolicyConventionGovernanceTests.GovernedFamilyRegex`; +`OrgStructureCatalogPaginationGuardrailsTests` + `OrgStructureCatalogRateLimitingGovernanceTests`.
**Prioridad:** Media · **Esfuerzo:** Bajo · **Estado:** ✅ Resuelto

### OSC-004 — Search sin `MinSearchLength`
**Severidad:** Baja · **Categoría:** Rendimiento · **Ubicación:** `SearchOrgUnitTypesQueryValidator`/`SearchFunctionalAreasQueryValidator` (sólo `MaximumLength(150)`); repo líneas 106-112, 153-159.
**Condición:** `q` de 1 char → `Contains` (LIKE '%x%') no-sargable. Mismo patrón que OrgUnits OU-002 y la familia §LG5/§12.8.
**Impacto:** menor (cardinalidad de catálogos pequeña), pero inconsistente con el estándar.
**Recomendación:** `MinSearchLength=2` en ambos validators.
**Resolución (✅ commit `963d507`):** `OrgStructureCatalogValidationRules.MinSearchLength = 2`, aplicado en ambos validators (`.WithMessage(...)`) + `OrgStructureCatalogSearchValidatorTests`.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ Resuelto

### OSC-005 — Race de código duplicado en Create → 500 (no 409)
**Severidad:** Baja · **Categoría:** Concurrencia · **Ubicación:** `CreateOrgUnitTypeCommandHandler` línea 389-401; `CreateFunctionalAreaCommandHandler` línea 812-824.
**Condición:** pre-check `…CodeExistsAsync` + insert; race viola `uq_*_catalog_items__tenant_code`; `UniqueConstraintViolationException` no atrapada → 500. Mismo patrón que CostCenters R2 / OrgUnits OU-004.
**Impacto:** 500 espurio en ventana estrecha; sin corrupción.
**Recomendación:** capturar `UniqueConstraintViolationException` → `CatalogCodeConflict` (409).
**Resolución (✅ commit `963d507`):** `catch (UniqueConstraintViolationException) when (…IsUnitTypeCodeConflict/…IsFunctionalAreaCodeConflict)` → `CatalogCodeConflict` (409) en Create+Update de ambos catálogos; nombres de índice single-sourced (`UnitTypeCodeUniqueConstraintName`/`FunctionalAreaCodeUniqueConstraintName`). Sin migración.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ Resuelto

### OSC-006 — Cobertura de integración mínima
**Severidad:** Baja · **Categoría:** Pruebas · **Ubicación:** `ApiIntegrationTests.cs` (2 métodos: líneas 4789, 4825).
**Condición:** sólo `UnitTypes_List_WithOrgUnitsReadFallback` y `FunctionalAreas_Inactivate_WhenInUse→409` para 10 endpoints.
**Impacto:** Create/Update/Activate/GetById/code-conflict/tenant-mismatch/concurrencia sin test de integración.
**Recomendación:** completar el set canónico (incl. 201/Location/ETag tras OSC-001, code-conflict 409, tenant 403, concurrencia 409).
**Resolución (✅ commit `963d507`):** 7 tests de integración (2 originales migrados al nuevo route + 5 nuevos): `Create_ShouldReturn201WithLocationAndETag`, `Update_WithValidIfMatch_ShouldApplyAndRotateToken`, `Update_WithStaleIfMatch_ShouldReturn409`, `Create_WithDuplicateCode_ShouldReturn409`, `List_WithTenantMismatch_ShouldReturn403`.
**Prioridad:** Baja · **Esfuerzo:** Medio · **Estado:** ✅ Resuelto

### OSC-007 — Search sin rate-limit
**Severidad:** Observación · **Categoría:** Rendimiento · **Ubicación:** endpoints 1 y 7 (Search).
**Condición:** sin `[EnableRateLimiting]` (como OrgUnits OU-001), aunque el perfil es de catálogo de referencia (lecturas pequeñas en form-load).
**Recomendación:** evaluar si se rules-out por diseño (rationale GeneralCatalogs) o se añade por consistencia al alinear (§11).
**Resolución (✅ commit `23521c9`):** se añadió por consistencia: `OrgStructureCatalogRateLimitPolicies.Search` (120, partición user+tenant) + `[EnableRateLimiting]` en los 2 Search + registro en `Program.cs` + `OrgStructureCatalogRateLimitingGovernanceTests` (drift-proof).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ Resuelto

### OSC-008 — Naming: abreviatura "Org" + ausencia de Tag
**Severidad:** Observación · **Categoría:** Contrato/Naming · **Ubicación:** clase `OrgStructureCatalogsController`, ruta `org-structure-catalogs`, Tag inexistente.
**Condición:** la clase/ruta usan la abreviatura "Org" y no hay Tag; el estándar de facto del proyecto es Tag de palabras completas en Title Case (ver análisis de naming, doc 15 §OU-009).
**Impacto:** incoherencia con el resto de la API.
**Recomendación:** al añadir `[Tags]` (OSC-001), usar **"Organization Structure Catalogs"** para alinear con la propuesta "Organization Units"; renombrar clase/namespace es seguro; renombrar la ruta `org-structure-catalogs`→`organization-structure-catalogs` es **breaking** (decidir junto con OrgUnits — ver doc 15 §OU-009).
**Resolución (✅ commit `95a4770`):** el usuario eligió la alineación completa (breaking): clase `OrganizationStructureCatalogsController`, `[Tags("Organization Structure Catalogs")]`, ruta `organization-structure-catalogs`, módulo etiquetado "Organization Structure Catalogs" (clave `ORG_STRUCTURE_CATALOGS` y namespace interno conservados). **El frontend debe actualizar la ruta.**
**Prioridad:** Baja · **Esfuerzo:** Bajo (Tag/clase) / Medio (ruta, breaking) · **Estado:** ✅ Resuelto

## 8. Hallazgos fuera de alcance / trazabilidad

- **Edición en curso de Locations** (nota de build §1): elimina el PATCH de LocationLevels; transitoriamente rompió el build (ya verde). No es de OrgStructureCatalogs.
- **`CompanyType`**: catálogo de dominio con repo-lookups (`GetActiveCompanyTypesByCountryCodeAsync`, `GetActiveCompanyTypeLookupAsync`) para creación de empresa por país; **no gestionado por este controlador**. Si se quisiera administrar vía API sería una extensión separada.
- **OSC-005** es sistémico (unique-constraint→409) — candidato a solución de plataforma.
- **Consumidor:** `OrgUnits` resuelve tipo/área vía `GetActiveOrgUnitTypeLookupAsync`/`GetActiveFunctionalAreaLookupAsync` (sólo activos) — acoplamiento correcto y tenant-scoped.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|
| OSC-001 | Media | Contrato | Pre-canónico (7 desviaciones) | Medio | Alta | ✅ **RESUELTO**: controller canónico (ApiVersion/Tags/AuthorizationPolicySet/SwaggerOperation/ProducesStandardErrors + If-Match + 201/Location/ETag) + `OrgStructureCatalogPolicies` wireadas |
| OSC-002 | Media | Rendimiento | N+1 en includeAllowedActions | Medio | Media | ✅ **RESUELTO**: `GetXPublicIdsInUseAsync` batch set-based (1 query) en ambos Search |
| OSC-003 | Baja | Gobernanza | No enrolado en guardrails | Bajo | Media | ✅ **RESUELTO**: Families + GovernedFamilyRegex + pagination + rate-limit governance tests |
| OSC-004 | Baja | Rendimiento | Sin MinSearchLength | Bajo | Baja | ✅ **RESUELTO**: `MinSearchLength=2` ambos validators + `OrgStructureCatalogSearchValidatorTests` |
| OSC-005 | Baja | Concurrencia | Race dup-code → 500 | Bajo | Baja | ✅ **RESUELTO**: catch `UniqueConstraintViolationException`→409 (Create+Update ×2 catálogos, índices single-sourced) |
| OSC-006 | Baja | Pruebas | Integración mínima (2/10) | Medio | Baja | ✅ **RESUELTO**: +5 integ tests (201/Location/ETag, If-Match update happy/stale, dup-code 409, tenant 403) + 2 existentes migrados |
| OSC-007 | Obs | Rendimiento | Search sin rate-limit | Bajo | Baja | ✅ **RESUELTO**: `org-structure-catalogs-search` 120 + `[EnableRateLimiting]` + governance test |
| OSC-008 | Obs | Naming | "Org" + sin Tag | Bajo/Medio | Baja | ✅ **RESUELTO**: Tag/clase/ruta→"Organization Structure Catalogs"/`organization-structure-catalogs` (breaking) + module label |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`OrganizationStructureCatalogsController`) | **Aprobado** (observaciones resueltas) |
| Seguridad (comportamiento) | Aprobado |
| Seguridad (gobernanza/drift) | ✅ Resuelto (OSC-001/OSC-003) — `[AuthorizationPolicySet]` + `GovernedFamilyRegex` |
| Arquitectura | Aprobado |
| Contrato | ✅ Canónico (OSC-001 resuelto) |
| Performance | ✅ Aprobado (OSC-002 resuelto) |
| Concurrencia | ✅ Aprobado (OSC-005 resuelto) |
| Pruebas | ✅ Aprobado (OSC-006 resuelto: 7 integ tests) |
| Readiness productivo completo | No certificado (nivel controller; alineación canónica ya completa) |

**El controlador es funcional y seguro, y ahora SÍ es coherente con el estándar canónico de la API** (alineación completada 2026-06-07, commits `95a4770`/`23521c9`/`963d507`). Los 8 hallazgos están resueltos y la sección de `openapi.yaml` ya fue **regenerada desde el swagger live** (2026-06-08, ver §11). Resta sólo **comunicar al frontend las dos breaking** (ruta `organization-structure-catalogs` + concurrencia vía `If-Match`).

## 11. Recomendaciones finales — ¿puede tener la misma estructura que OrgUnits? **Sí — y ya la tiene (✅ completado 2026-06-07).**

Respondiendo a la pregunta del usuario: **OrgStructureCatalogs adoptó la estructura canónica** (candidato del `api-canonical-alignment-plan`, ya ejecutado). Checklist de alineación — **todo aplicado**:

**No-breaking:**
1. ✅ `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}")]`.
2. ✅ `[Tags("Organization Structure Catalogs")]` (ver naming, doc 15 §OU-009).
3. ✅ `[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, OrgStructureCatalogPolicies.Manage)]` + `OrgStructureCatalogPolicies` creadas y wireadas en `Program.cs` (superset del gate) + enroladas en `GovernedFamilyRegex`.
4. ✅ `[SwaggerOperation]` + `[ProducesStandardErrors]` en cada endpoint; enrolado en `OpenApiContractGuardrailsTests.Families`.
5. ✅ `ToCreatedAtActionResult` (201 + Location + ETag) en los Create; `ToActionResultWithETag` en Update/Activate/Inactivate.
6. ✅ `MinSearchLength=2` (OSC-004); batch del N+1 (OSC-002); catch unique-constraint→409 (OSC-005).
7. ✅ Tests de integración ampliados a 7 (OSC-006).

**Breaking (decididos y aplicados — el frontend debe actualizar):**
8. ✅ Concurrencia migrada de **body token** a **`If-Match` header** (`[FromIfMatch]`) en Update/Activate/Inactivate.
9. ✅ Ruta renombrada `org-structure-catalogs`→`organization-structure-catalogs` (alineada con la decisión de OrgUnits, doc 15 §OU-009).

Se mantuvieron las fortalezas: gate por handler en todos los endpoints, constantes de auditoría, integridad referencial al inactivar, índices correctos.

**✅ openapi.yaml regenerado (2026-06-08):** la sección de `OrganizationStructureCatalogsController` en `docs/technical/api/openapi.yaml` se regeneró desde el **swagger live** (app en Development → `GET /swagger/v1/swagger.yaml`), normalizando el output crudo al estilo de curación del archivo: schema-IDs cortos (el swagger crudo emite IDs `assembly-qualified` por `CustomSchemaIds(FullName)`), claves de ruta sin comillas, y secuencias al estilo existente. Cambios aplicados a las 12 operaciones: Tag→`Organization Structure Catalogs`; `summary`/`description`; `If-Match` (string/uuid) en PUT+activate+inactivate (6); `UpsertCatalogItemRequest` en PUT (antes `UpdateCatalogItemRequest`); sin `requestBody` en activate/inactivate (antes `ConcurrencyRequest`); +`422`/`404` (de `[ProducesStandardErrors]`) y `maximum/minimum` en pageSize (de `[Range]`). Schemas huérfanos `ConcurrencyRequest` y `UpdateCatalogItemRequest` eliminados. **Validado:** YAML parsea, 239 paths intactos (sólo cambió esta sección — 0 cambios a otros controllers), 497/497 `$ref` resuelven. **Nota:** NO se regeneró el archivo completo a propósito — un dump crudo reformatearía los 30+ controllers (IDs `assembly-qualified`, comillas, reindent); el archivo es un artefacto curado y se mantiene por sección. Resta comunicar al frontend las dos breaking (ruta + `If-Match`).

## 12. Anexos / Evidencia revisada

- Controller: `OrganizationStructureCatalogsController.cs` (10 endpoints + 3 DTOs; renombrado en OSC-008).
- Aplicación: `OrgStructureCatalogsAdministration.cs` (DTOs/validators/10 handlers + `OrgStructureCatalogPolicyAdapter`), `OrgStructureCatalogCommon.cs`.
- Dominio: `OrgUnitTypeCatalogItem.cs`, `FunctionalAreaCatalogItem.cs`, `CompanyTypeCatalogItem.cs`, `OrgStructureCatalogNormalization.cs`.
- Persistencia: `IOrgStructureCatalogRepository.cs`, `OrgStructureCatalogRepository.cs`, `OrgStructureCatalogsConfiguration.cs`.
- Seguridad/provisión: `OrgStructureCatalogAuthorizationService.cs`, `OrgStructureCatalogSeedService.cs`, `BackendMessages(.es).resx`.
- Pruebas: `OrgStructureCatalogDomainTests.cs`, `ApiIntegrationTests.cs` (líneas 4789, 4825).
- Ejecución: `dotnet test --filter ~OrgStructureCatalog|~ConcurrencyTokenMappingGuardrails|~OpenApiContractGuardrails|~AuthorizationPolicyConventionGovernance` → **53/53 passed** (auditoría 2026-06-07); **re-ejecutado 66/66 passed** (2026-06-08, post-remediación, exit 0).
