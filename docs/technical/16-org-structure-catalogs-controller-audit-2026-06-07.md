# Auditoría Técnica por Controlador — OrgStructureCatalogsController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Relacionada: [doc 15 — OrgUnitsController](15-org-units-controller-audit-2026-06-07.md) (consume estos catálogos).

## 1. Resumen ejecutivo

`OrgStructureCatalogsController` administra los **catálogos de referencia de la estructura organizativa**: **tipos de unidad** (`unit-types`) y **áreas funcionales** (`functional-areas`), con CRUD + activar/inactivar por catálogo (10 endpoints). Estos catálogos alimentan a `OrgUnits` (tipo y área funcional de cada unidad). El dominio incluye además `CompanyType`, pero **no se expone en este controlador** (sólo lecturas para creación de empresa → fuera de alcance).

Veredicto: **APROBADO CON OBSERVACIONES**. 0 críticos / 0 altos. La **seguridad es correcta**: los 10 handlers aplican el gate de autorización (`EnsureCanReadTenantAsync`/`EnsureCanManageTenantAsync` con tenant-match + entitlement de módulo + RBAC), el aislamiento por tenant es fail-closed, la auditoría usa constantes y la integridad referencial se respeta al inactivar. **Sin embargo, es el controlador menos canónico del área**: a diferencia de `OrgUnits`, carece de `[ApiVersion]`, `[Tags]`, **`[AuthorizationPolicySet]`**, `[SwaggerOperation]` y `[ProducesStandardErrors]`, usa **token de concurrencia en el body** (no `If-Match`/ETag), y su `Create` no devuelve `Location`/`ETag`. Esto responde directamente a la observación del usuario: **no es coherente con los demás endpoints y debería alinearse a la estructura canónica** (ver §6.3 y §11). El hallazgo material adicional es un **N+1 (Media)** en el enriquecimiento `includeAllowedActions`.

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila (ver nota de build) |
| Unit tests (OrgStructureCatalog + guardrails, **ejecutados**) | ✅ 53/53 passed |
| Integration tests dedicados (revisados, no ejecutados) | **2** (cobertura delgada) |
| Hallazgos | 0 Crít · 0 Alto · **2 Media · 4 Baja · 2 Observación** |
| **Estado de remediación (2026-06-07, uncommitted)** | ✅ **LOS 8 HALLAZGOS RESUELTOS** (OSC-001…OSC-008). Alineación canónica completa (incl. ambos breaking: If-Match + rename de ruta). build 0/0 (solución) · unit 1687/1687 · integración compila (tests requieren DB) · **sin migraciones · sin resx**. ⚠️ Breaking: ruta `org-structure-catalogs`→`organization-structure-catalogs` + concurrencia body-token→`If-Match` (frontend debe actualizar). `openapi.yaml`: paths renombrados mecánicamente; **la sección requiere regen completa** por el cambio de contrato (Tag/If-Match/ETag/Swagger). |

> **Nota de build (fuera de alcance):** durante la sesión se observó una edición **en curso, sin commitear**, en la vertical de **Locations** (`LocationHierarchyAdministration.cs`, `LocationLevelsController.cs`) que **elimina el endpoint JSON-Patch de LocationLevels** (acción, `PatchLocationLevelCommand`, `LocationLevelPatchOperation`, `LocationLevelPatchApplier`/`State` y sus tests). Provocó un break de compilación transitorio; al finalizar la edición el build y los 53 tests relevantes quedaron verdes. **No forma parte de OrgStructureCatalogs**; se reporta por transparencia.

## 2. Alcance

**Incluido:** controlador `OrgStructureCatalogsController.cs`; aplicación `OrgStructureCatalogsAdministration.cs` (DTOs, 10 queries/commands, validators, 10 handlers, `OrgStructureCatalogPolicyAdapter`); dominio `OrgUnitTypeCatalogItem.cs`, `FunctionalAreaCatalogItem.cs`, `OrgStructureCatalogNormalization.cs`; persistencia `IOrgStructureCatalogRepository` + `OrgStructureCatalogRepository`, EF `OrgStructureCatalogsConfiguration`; seguridad `OrgStructureCatalogPermissionCodes`, `IOrgStructureCatalogAuthorizationService` + impl; provisión `OrgStructureCatalogSeedService`; errores/i18n `OrgStructureCatalogCommon.cs` + resx; pruebas `OrgStructureCatalogDomainTests` + integración.

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

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / capas / DTOs | PASS | Sólo despacha CQRS; records cerrados |
| Arquitectura | Transacciones en escrituras | PASS | Los 8 commands: transacción + 2 SaveChanges + audit |
| Seguridad | Autenticación | PASS | `[Authorize]` |
| Seguridad | Autorización por handler (Read/Manage) | PASS | **Los 10 handlers** invocan `EnsureCanReadTenantAsync/EnsureCanManageTenantAsync` como 1er paso |
| Seguridad | Declarativa `[AuthorizationPolicySet]` | **FAIL** | **OSC-001/OSC-003**: ausente; sólo `[Authorize]` (authn). Sin backstop declarativo ni governance |
| Seguridad | BOLA/IDOR + Tenant isolation | PASS | id-only routes (tenant del JWT); filtro global fail-closed; `ExistsOutsideTenant` → 404 vs 403 |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(CommercialModuleKeys.OrgStructureCatalogs)` |
| Seguridad | Integridad referencial (inactivar) | PASS | UnitType: `HasOrgUnitsUsing…`/`HasPositionCategoryClassificationsUsing…`; FA: `HasOrgUnitsUsing…` → 409 `CatalogInUse` |
| Seguridad | Mass assignment | PASS | DTOs cerrados |
| Contrato | Versionado `[ApiVersion]` | **FAIL** | **OSC-001**: sin `[ApiVersion]`; rutas con literal `api/v1` |
| Contrato | `[Tags]` (grupo Swagger) | **FAIL** | **OSC-001**: sin `[Tags]` → no agrupado, no enrolable en OpenAPI guardrail |
| Contrato | `[SwaggerOperation]` docs | **FAIL** | **OSC-001**: ausente |
| Contrato | Error contract `[ProducesStandardErrors]` | **FAIL** | **OSC-001**: usa `[ProducesResponseType]` verboso inline |
| Contrato | 201 + Location + ETag en Create | **FAIL** | **OSC-001**: `StatusCode(201, value)` plano (sin Location ni ETag) |
| Contrato | ETag / If-Match en updates | **FAIL** | **OSC-001**: token de concurrencia en el **body**, no `If-Match`; sin ETag en respuesta |
| Contrato | Paginación: page size máx | PASS | Validators `InclusiveBetween(1,100)` ✓ (pero sin `[Range]` en el param) |
| Contrato | Search: longitud mínima | WARNING | **OSC-004**: sin `MinSearchLength` |
| Rendimiento | Índices | PASS | `uq_(tenant,normalized_code)` + `(tenant,name)`/`(tenant,active)` por catálogo |
| Rendimiento | N+1 | **FAIL** | **OSC-002**: `includeAllowedActions` hace 2-3 queries por ítem |
| Rendimiento | Rate limiting | OBS | **OSC-007**: 2 Search sin rate-limit (perfil de catálogo, menor) |
| Concurrencia | Optimista + 409 | PASS | Token rotado; check manual→409; `.IsConcurrencyToken()` (auto-guardrail); race→409 vía middleware |
| Concurrencia | Unique-constraint en Create → 409 | **FAIL** | **OSC-005**: race dup-code → 500 |
| Observabilidad | Audit logs + **constantes** | PASS | Los 8 commands con `AuditEventTypes.*`/`AuditEntityTypes.*` (✅ mejor que OrgUnits) |
| Gobernanza | Enrolado en guardrails de familia | **FAIL** | **OSC-003**: sólo concurrency-token (auto). Falta OpenAPI/authz/paginación/rate-limit |
| Pruebas | Unit (dominio) | PASS | `OrgStructureCatalogDomainTests` (62 líneas) |
| Pruebas | Integración | WARNING | **OSC-006**: sólo 2 tests dedicados para 10 endpoints |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS correcta y consistente internamente; validators separados; dominio con invariantes (sortOrder ≥ 0, normalización code/name); `OrgStructureCatalogPolicyAdapter` para `AllowedActions`. Cada escritura envuelve mutación + auditoría en transacción. El controlador es delgado (sólo despacha). El problema no es la arquitectura interna sino la **adherencia al contrato canónico** (§6.3).

### 6.2 Seguridad
**Correcta a nivel de comportamiento**: el gate de autorización se aplica en los 10 handlers (no hay endpoint sin autz), con tenant-match + entitlement de módulo + claims/grants RBAC que **acepta permisos `OrgStructureCatalogs.*` y `OrgUnits.*`** (sensato: quien administra unidades administra sus catálogos). Aislamiento por tenant fail-closed; `ExistsOutsideTenant` para 404 vs 403. Integridad referencial al inactivar (no se puede inactivar un tipo/área en uso → 409). **Riesgo estructural (no actual)**: al carecer de `[AuthorizationPolicySet]` y no estar en `GovernedFamilyRegex`, la seguridad depende **enteramente** del gate manual del handler; si un futuro handler omitiera el gate, **ningún guardrail ni capa declarativa lo detectaría** (a diferencia de OrgUnits/Locations). Por eso OSC-001/OSC-003 son Media pese a no ser una vulnerabilidad hoy.

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

Es un controlador **pre-canónico**: probablemente escrito antes de la ola de alineación (o nunca alineado). Funciona y es seguro, pero rompe la coherencia del contrato público. **Sí puede —y debería— adoptar la misma estructura que OrgUnits** (ver §11, candidato natural del `api-canonical-alignment-plan`).

### 6.4 Rendimiento
Índices correctos por catálogo. Search `AsNoTracking` + paginado. **N+1 (OSC-002)**: cuando `includeAllowedActions=true`, cada Search itera los ítems de la página y por cada uno ejecuta `GetXByIdAsync` + 1-2 chequeos de dependencia (`HasOrgUnitsUsing…`, `HasPositionCategoryClassificationsUsing…`) → hasta ~60 queries por página de 20. Se debe batchear con una consulta set-based ("cuáles de estos N están en uso"), patrón §12.7/§LG2. Search libre no-sargable sin `MinSearchLength` (OSC-004), aunque la cardinalidad de catálogos es pequeña.

### 6.5 Concurrencia y consistencia
Token rotado en cada mutación; check manual→409; `.IsConcurrencyToken()` en las 3 entidades (auto-cubierto por `ConcurrencyTokenMappingGuardrailsTests`, **verificado verde**); race read-check-write→409 vía middleware. **Pero** el token viaja en el body, no en `If-Match` (OSC-001), y el Create no traduce la violación de unicidad a 409 (OSC-005).

### 6.6 Observabilidad
**Punto fuerte**: auditoría persistida en los 8 commands usando **constantes** `AuditEventTypes.OrgUnitTypeCatalogItem*`/`FunctionalAreaCatalogItem*` y `AuditEntityTypes.*` (✅ a diferencia de los literales crudos de OrgUnits OU-003). Logging estructurado vía middleware.

### 6.7 Pruebas
**Ejecutadas**: 53 unit tests verdes (dominio de catálogos + guardrails concurrency/OpenAPI/authz). **Revisadas**: sólo **2** integration tests dedicados (`UnitTypes_List_WithOrgUnitsReadFallback` — valida el fallback de permisos OrgUnits; `FunctionalAreas_Inactivate_WhenInUse→409` — integridad referencial). **Gap (OSC-006)**: sin tests de integración para Create/Update/Activate/GetById/code-conflict 409/tenant-mismatch 403/concurrencia en los catálogos. Cubre los dos casos más sutiles, pero es delgado para 10 endpoints.

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
**Prioridad:** Alta · **Esfuerzo:** Medio · **Estado:** Abierto

### OSC-002 — N+1 en el enriquecimiento `includeAllowedActions`
**Severidad:** Media · **Categoría:** Rendimiento · **Ubicación:** `SearchOrgUnitTypesQueryHandler` líneas 307-324 y `SearchFunctionalAreasQueryHandler` líneas 689-705.
**Condición:** por cada ítem activo de la página se ejecuta `GetXByIdAsync` + 1-2 chequeos de dependencia → hasta ~3 queries/ítem (≈60 por página de 20).
**Criterio esperado:** resolución batch/set-based de dependencias (patrón §12.7/§LG2; OrgUnits lo evita usando un flag fijo en la lista).
**Impacto:** latencia y carga DB proporcionales al page size cuando el flag está activo.
**Recomendación:** una consulta set-based `WHERE catalogItemId IN (@ids) AND IsActive GROUP BY` para marcar dependencias en un solo viaje; o proyectar el id interno + `hasDependencies` en el Search.
**Prioridad:** Media · **Esfuerzo:** Medio · **Estado:** Abierto

### OSC-003 — No enrolado en los guardrails drift-proof de la familia
**Severidad:** Baja · **Categoría:** Seguridad/Gobernanza · **Ubicación:** guardrails de `tests/CLARIHR.Application.UnitTests`.
**Condición:** OrgStructureCatalogs no está en `OpenApiContractGuardrailsTests.Families` (no tiene Tag), ni en `AuthorizationPolicyConventionGovernanceTests.GovernedFamilyRegex`, ni tiene guardrail de paginación/rate-limit. Sólo el concurrency-token está auto-cubierto.
**Criterio esperado:** los controllers gobernados se enrolan para que el drift falle en CI.
**Impacto:** una regresión de contrato/autz no sería detectada automáticamente.
**Recomendación:** tras OSC-001, enrolar en `Families` (Tag) y `GovernedFamilyRegex`, y añadir guardrails de paginación/rate-limit si aplica.
**Prioridad:** Media · **Esfuerzo:** Bajo · **Estado:** Abierto

### OSC-004 — Search sin `MinSearchLength`
**Severidad:** Baja · **Categoría:** Rendimiento · **Ubicación:** `SearchOrgUnitTypesQueryValidator`/`SearchFunctionalAreasQueryValidator` (sólo `MaximumLength(150)`); repo líneas 106-112, 153-159.
**Condición:** `q` de 1 char → `Contains` (LIKE '%x%') no-sargable. Mismo patrón que OrgUnits OU-002 y la familia §LG5/§12.8.
**Impacto:** menor (cardinalidad de catálogos pequeña), pero inconsistente con el estándar.
**Recomendación:** `MinSearchLength=2` en ambos validators.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### OSC-005 — Race de código duplicado en Create → 500 (no 409)
**Severidad:** Baja · **Categoría:** Concurrencia · **Ubicación:** `CreateOrgUnitTypeCommandHandler` línea 389-401; `CreateFunctionalAreaCommandHandler` línea 812-824.
**Condición:** pre-check `…CodeExistsAsync` + insert; race viola `uq_*_catalog_items__tenant_code`; `UniqueConstraintViolationException` no atrapada → 500. Mismo patrón que CostCenters R2 / OrgUnits OU-004.
**Impacto:** 500 espurio en ventana estrecha; sin corrupción.
**Recomendación:** capturar `UniqueConstraintViolationException` → `CatalogCodeConflict` (409).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### OSC-006 — Cobertura de integración mínima
**Severidad:** Baja · **Categoría:** Pruebas · **Ubicación:** `ApiIntegrationTests.cs` (2 métodos: líneas 4789, 4825).
**Condición:** sólo `UnitTypes_List_WithOrgUnitsReadFallback` y `FunctionalAreas_Inactivate_WhenInUse→409` para 10 endpoints.
**Impacto:** Create/Update/Activate/GetById/code-conflict/tenant-mismatch/concurrencia sin test de integración.
**Recomendación:** completar el set canónico (incl. 201/Location/ETag tras OSC-001, code-conflict 409, tenant 403, concurrencia 409).
**Prioridad:** Baja · **Esfuerzo:** Medio · **Estado:** Abierto

### OSC-007 — Search sin rate-limit
**Severidad:** Observación · **Categoría:** Rendimiento · **Ubicación:** endpoints 1 y 7 (Search).
**Condición:** sin `[EnableRateLimiting]` (como OrgUnits OU-001), aunque el perfil es de catálogo de referencia (lecturas pequeñas en form-load).
**Recomendación:** evaluar si se rules-out por diseño (rationale GeneralCatalogs) o se añade por consistencia al alinear (§11).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### OSC-008 — Naming: abreviatura "Org" + ausencia de Tag
**Severidad:** Observación · **Categoría:** Contrato/Naming · **Ubicación:** clase `OrgStructureCatalogsController`, ruta `org-structure-catalogs`, Tag inexistente.
**Condición:** la clase/ruta usan la abreviatura "Org" y no hay Tag; el estándar de facto del proyecto es Tag de palabras completas en Title Case (ver análisis de naming, doc 15 §OU-009).
**Impacto:** incoherencia con el resto de la API.
**Recomendación:** al añadir `[Tags]` (OSC-001), usar **"Organization Structure Catalogs"** para alinear con la propuesta "Organization Units"; renombrar clase/namespace es seguro; renombrar la ruta `org-structure-catalogs`→`organization-structure-catalogs` es **breaking** (decidir junto con OrgUnits — ver doc 15 §OU-009).
**Prioridad:** Baja · **Esfuerzo:** Bajo (Tag/clase) / Medio (ruta, breaking) · **Estado:** Abierto

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
| Controller auditado (`OrgStructureCatalogsController`) | **Aprobado con observaciones** |
| Seguridad (comportamiento) | Aprobado |
| Seguridad (gobernanza/drift) | Observaciones (OSC-001/OSC-003) |
| Arquitectura | Aprobado |
| Contrato | **No canónico** (OSC-001) — alinear |
| Performance | Aprobado con observaciones (OSC-002) |
| Concurrencia | Aprobado con observaciones (OSC-005) |
| Pruebas | Observaciones (OSC-006) |
| Readiness productivo completo | No certificado |

**El controlador es funcional y seguro, pero NO es coherente con el estándar canónico de la API.** Puede avanzar a QA, pero se **recomienda alinearlo (OSC-001) antes de exposición productiva**, idealmente en el mismo esfuerzo que la decisión de naming de OrgUnits.

## 11. Recomendaciones finales — ¿puede tener la misma estructura que OrgUnits? **Sí.**

Respondiendo a la pregunta del usuario: **OrgStructureCatalogs debe adoptar la estructura canónica** (es candidato natural del `api-canonical-alignment-plan`). Checklist de alineación:

**No-breaking (hacer ya):**
1. `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}")]` (o mantener `api/v1` literal como SalaryTabulator, pero con `[ApiVersion]`).
2. `[Tags("Organization Structure Catalogs")]` (ver naming, doc 15 §OU-009).
3. `[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, OrgStructureCatalogPolicies.Manage)]` + crear `OrgStructureCatalogPolicies` y wirearlas en `Program.cs` (superset del gate) + enrolar en `GovernedFamilyRegex`.
4. `[SwaggerOperation]` + `[ProducesStandardErrors]` en cada endpoint; enrolar en `OpenApiContractGuardrailsTests.Families`.
5. `ToCreatedAtActionResult` (201 + Location + ETag) en los Create; `ToActionResultWithETag` en Update/Activate/Inactivate.
6. `MinSearchLength=2` (OSC-004); batch del N+1 (OSC-002); catch unique-constraint→409 (OSC-005).
7. Completar tests de integración (OSC-006).

**Breaking (decidir):**
8. Migrar concurrencia de **body token** a **`If-Match` header** (`[FromIfMatch]`) — cambia el contrato de Update/Activate/Inactivate; coordinar con el frontend.
9. Renombrar la ruta `org-structure-catalogs` (y opcionalmente `org-units`) — ver doc 15 §OU-009.

Mantener las fortalezas: gate por handler en todos los endpoints, constantes de auditoría, integridad referencial al inactivar, índices correctos.

## 12. Anexos / Evidencia revisada

- Controller: `OrgStructureCatalogsController.cs` (10 endpoints + 3 DTOs).
- Aplicación: `OrgStructureCatalogsAdministration.cs` (DTOs/validators/10 handlers + `OrgStructureCatalogPolicyAdapter`), `OrgStructureCatalogCommon.cs`.
- Dominio: `OrgUnitTypeCatalogItem.cs`, `FunctionalAreaCatalogItem.cs`, `CompanyTypeCatalogItem.cs`, `OrgStructureCatalogNormalization.cs`.
- Persistencia: `IOrgStructureCatalogRepository.cs`, `OrgStructureCatalogRepository.cs`, `OrgStructureCatalogsConfiguration.cs`.
- Seguridad/provisión: `OrgStructureCatalogAuthorizationService.cs`, `OrgStructureCatalogSeedService.cs`, `BackendMessages(.es).resx`.
- Pruebas: `OrgStructureCatalogDomainTests.cs`, `ApiIntegrationTests.cs` (líneas 4789, 4825).
- Ejecución: `dotnet test --filter ~OrgStructureCatalog|~ConcurrencyTokenMappingGuardrails|~OpenApiContractGuardrails|~AuthorizationPolicyConventionGovernance` → **53/53 passed** (sesión 2026-06-07).
