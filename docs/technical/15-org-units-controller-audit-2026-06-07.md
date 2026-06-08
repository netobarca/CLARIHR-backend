# Auditoría Técnica por Controlador — OrgUnitsController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)

## 1. Resumen ejecutivo

`OrgUnitsController` administra la **estructura organizativa jerárquica** por empresa: 12 endpoints (búsqueda paginada, get-by-id, árbol, grafo, export de reporte, export de diagrama, create, update, patch, move/reparent, activate, inactivate). Es un controlador **maduro y rico**: jerarquía con detección de ciclos y límite de profundidad, referencias a catálogos (tipo de unidad, área funcional), centro de costo (por código) y manager (por empleado), `AllowedActions` por ítem, y dos formatos de exportación (reporte tabular + diagrama GraphML/DOT/JSON).

Veredicto: **APROBADO CON OBSERVACIONES**. 0 críticos / 0 altos. La seguridad es sólida (autorización de dos capas, aislamiento por tenant fail-closed, entitlement de módulo, anti-mass-assignment, concurrencia optimista robusta, hardening JSON Patch, salida de diagrama escapada) y está respaldada por buena cobertura de pruebas (14 integración + dominio/builder/patch unit) y los *guardrails* drift-proof de la plataforma (OpenAPI, authz-convention, concurrency-token). El hallazgo material es la **ausencia total de rate-limiting (Media)** —deriva del estándar adoptado por toda la familia de controladores— acompañada de nits de consistencia (MinSearchLength, literales de auditoría, race dup-code, `[Range]`).

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ 0/0 |
| Unit tests (OrgUnit + guardrails, **ejecutados**) | ✅ 69/69 passed |
| Integration tests OrgUnit (revisados, no ejecutados — requieren DB) | 14 |
| Hallazgos | 0 Crít · 0 Alto · **1 Media · 4 Baja · 4 Observación** (OU-009 naming añadido 2026-06-07) |
| **Estado de remediación (2026-06-07, uncommitted)** | ✅ **LOS 9 HALLAZGOS RESUELTOS** (OU-001…OU-009). build 0/0 (solución) · unit 1677/1677 · integración compila (4 tests OU-008 requieren DB) · **sin migraciones · sin cambios de resx** · ⚠️ ruta `org-units`→`organization-units` es **breaking** (frontend debe actualizar) |

## 2. Alcance

**Incluido (vertical directa):**
- Controlador `src/CLARIHR.Api/Controllers/OrgUnitsController.cs` (12 endpoints + serializadores de diagrama).
- Aplicación `Features/OrgUnits/OrgUnitAdministration.cs` (DTOs, queries, commands, validators, 11 handlers, `OrgUnitPatchApplier`, `OrgUnitPolicyAdapter`, `OrgUnitHierarchyBuilder`).
- Dominio `Domain/OrgUnits/OrgUnit.cs`, `OrgUnitNormalization.cs`.
- Persistencia `IOrgUnitRepository` + `OrgUnitRepository`; EF `OrgUnitConfiguration`; filtro global por tenant.
- Seguridad `OrgUnitPolicies`, `OrgUnitPermissionCodes`, `IOrgUnitAuthorizationService` + impl; wiring en `Program.cs`.
- Errores/i18n `OrgUnitCommon.cs` (`OrgUnitErrors`), resx.
- Reportes: `OrgUnitsExportHandler` (job async) + `ReportExportResourceAuthorizer` (authz por-recurso) + `ReportExportDeliveryService` (export síncrono).
- Dependencias directas: `IOrgStructureCatalogRepository` (tipo/área funcional), `ICostCenterRepository` (validación de CC).
- Pruebas: `OrgUnitDomainTests`, `OrgUnitPatchApplierTests`, e integración `ApiIntegrationTests.cs`.

**Excluido:** auditoría integral de la API; `ReportExportJobsController` y `OrgStructureCatalogs`/`CostCenters` salvo dependencia directa; pruebas de carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en jerarquía (ciclo/profundidad), seguridad (OWASP API Top 10), exportación y concurrencia; verificación de *guardrails*. Evidencia de ejecución: build Release (0/0) + 69 unit tests ejecutados (verde). Integración revisada por código (requiere Postgres/Testcontainers; no ejecutada → limitación declarada).

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Handler | Riesgo |
|---|---|---|---|---|---|
| 1 | GET | `/companies/{companyId}/org-units` | Búsqueda paginada (filtros + `q` + allowedActions) | `SearchOrgUnitsQueryHandler` | Medio (read costoso, no rate-limit) |
| 2 | GET | `/org-units/{id}` | Get by id (tenant del JWT) | `GetOrgUnitByIdQueryHandler` | Bajo |
| 3 | GET | `/companies/{companyId}/org-units/tree` | Árbol jerárquico | `GetOrgUnitTreeQueryHandler` | Medio (carga jerarquía completa) |
| 4 | GET | `/companies/{companyId}/org-units/graph` | Grafo (nodos/aristas JSON) | `GetOrgUnitGraphQueryHandler` | Medio |
| 5 | GET | `/companies/{companyId}/org-units/export` | Export tabular (xlsx) | `GetOrgUnitExportRowsQueryHandler` | Medio (export, no rate-limit) |
| 6 | GET | `/companies/{companyId}/org-units/diagram-export` | Export diagrama (graphml/json/dot, 413 si excede) | (controller + graph query) | Medio |
| 7 | POST | `/companies/{companyId}/org-units` | Crear (201 + Location + ETag) | `CreateOrgUnitCommandHandler` | Medio |
| 8 | PUT | `/org-units/{id}` | Actualizar campos editables (If-Match) | `UpdateOrgUnitCommandHandler` | Medio |
| 9 | PATCH | `/org-units/{id}` | Patch descriptivo (name/sortOrder/description) | `PatchOrgUnitCommandHandler` | Bajo |
| 10 | PATCH | `/org-units/{id}/move` | Reparent (ciclo/profundidad → 409) | `MoveOrgUnitCommandHandler` | Medio |
| 11 | PATCH | `/org-units/{id}/activate` | Reactivar | `ActivateOrgUnitCommandHandler` | Bajo |
| 12 | PATCH | `/org-units/{id}/inactivate` | Inactivar (409 si tiene hijos activos) | `InactivateOrgUnitCommandHandler` | Medio |

Sin `DELETE` (soft-delete vía `/inactivate`). El parent cambia sólo vía `/move`; activación sólo vía `/activate`-`/inactivate`. `cost center` por código (422 si inválido); type/área/parent/manager por public id.

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller sin lógica de negocio | WARNING | Mayormente delgado; **OU-006**: ~110 líneas de serialización GraphML/DOT en el controller |
| Arquitectura | Separación por capas / DTOs | PASS | Records explícitos; entidades nunca expuestas |
| Arquitectura | Transacciones en escrituras | PASS | 6 commands: `BeginTransactionAsync` + 2 `SaveChanges` (mutación + audit) + commit/rollback |
| Seguridad | Autenticación / Autorización (Read vs Manage) | PASS | `[Authorize]` + `[AuthorizationPolicySet(Read,Manage)]` + gate `EnsureCanReadAsync/EnsureCanManageAsync` |
| Seguridad | Superset declarativa ⊇ handler | PASS | `AuthorizationPolicyConventionGovernanceTests`; controller en `GovernedFamilyRegex`; `OrgUnitPolicyNames` validado |
| Seguridad | BOLA/IDOR + Tenant isolation | PASS | Rutas id-only sin companyId (tenant del JWT); filtro global fail-closed; `ExistsOutsideTenantAsync` → 404 vs 403 |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(CommercialModuleKeys.OrgUnits)` en el gate |
| Seguridad | Mass assignment | PASS | DTOs cerrados; patch limitado a name/sortOrder/description |
| Seguridad | DoS JSON Patch | PASS | `[RequestSizeLimit(64KB)]` + tope 50 ops |
| Seguridad | Inyección en export de diagrama | PASS | GraphML vía `XmlWriter` (auto-escape); DOT vía `EscapeDot` (\\ y ") |
| Seguridad | Authz del export async (REX-1) | PASS | `ReportExportResourceAuthorizer` cubre `ORG_UNITS` → `EnsureCanReadAsync` (download/getById/cancel gated) |
| Seguridad | **Rate limiting** | **FAIL** | **OU-001**: ningún `[EnableRateLimiting]`; sin política ni governance test pese a 5 reads costosos + 2 exports |
| Contrato | RESTful + verbos + versionado + 201/Location/ETag | PASS | Canónico; `[ApiVersion]`, `[Tags("Org Units")]`, `[SwaggerOperation]` |
| Contrato | Error contract + OpenAPI guardrail | PASS | `[ProducesStandardErrors]`; enrolado en `OpenApiContractGuardrailsTests.Families` |
| Contrato | ETag/If-Match en updates | PASS | `[FromIfMatch]` (400 si falta) + `ToActionResultWithETag` en 6 mutaciones |
| Contrato | Paginación: page size máx | WARNING | Validator `InclusiveBetween(1,100)` ✓, pero **OU-005**: falta `[Range]` en el param + sin guardrail de paginación |
| Contrato | Search: longitud mínima | WARNING | **OU-002**: sin `MinSearchLength` → 1-char dispara scan no-sargable |
| Rendimiento | Índices | PASS | `uq_(tenant,normalized_code)` + 6 índices `(tenant,*)` para filtros/orden |
| Rendimiento | N+1 en jerarquía | PASS | `GetHierarchyAsync` = 1 query; árbol/grafo/export en memoria |
| Rendimiento | Lecturas redundantes | OBS | **OU-007**: GetById hace 3 queries; `.ContinueWith` cast |
| Rendimiento | Export acotado | PASS | `SynchronousReadLimit` (tabular) + `MaxDiagramNodes`→413 (diagrama) |
| Concurrencia | Optimista + If-Match + 409 | PASS | Token rotado; check manual→409; `.IsConcurrencyToken()` (auto-guardrail); race read-check-write→409 vía middleware |
| Concurrencia | Ciclo / profundidad | PASS | `WouldCreateCycle` + `CalculateDepth` (con `visited` defensivo, tope `MaxDepth=15`); self-parent → 409 |
| Concurrencia | Unique-constraint en Create → 409 | **FAIL** | **OU-004**: race dup-code → 500 (no atrapa `UniqueConstraintViolationException`) |
| Observabilidad | Audit logs en escrituras | PASS (nit) | Auditoría en los 6 commands; **OU-003**: usa literales crudos en vez de `AuditEventTypes`/`AuditEntityTypes` |
| Pruebas | Unit (dominio/builder/patch) | PASS | `OrgUnitDomainTests` (dominio + ciclo + profundidad) + `OrgUnitPatchApplierTests` |
| Pruebas | Integración (12 endpoints) | PASS (gaps menores) | 14 tests; **OU-008**: faltan activate/update/move happy-path + code-conflict 409 |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS consistente con la familia. Validators separados; dominio con invariantes (sortOrder ≥ 0, ids > 0, normalización code/name); `OrgUnitPatchApplier` restringido a name/sortOrder/description (rechaza code/parent/type/FA/manager/CC/isActive/concurrencyToken). `OrgUnitHierarchyBuilder` aísla la lógica de árbol/grafo/ciclo/profundidad (testeable). Cada escritura envuelve mutación + auditoría en transacción. **Única desviación**: el controller incluye ~110 líneas de serialización GraphML/DOT (`BuildGraphMl`/`BuildDot`) y un `JsonSerializer.Serialize` inline para el diagram-export — presentación que pertenece a un formatter/servicio (OU-006).

### 6.2 Seguridad
**Autorización de dos capas** idéntica al patrón de Locations/CostCenters: política declarativa superset + gate preciso (tenant-match + entitlement de módulo + claims/grants RBAC), enforced por governance test, controller en `GovernedFamilyRegex`. **Sin IDOR**: rutas `/org-units/{id}` resuelven el tenant del JWT (no aceptan companyId), el filtro global EF es *fail-closed*, y `ExistsOutsideTenantAsync` (con `IgnoreQueryFilters`) sólo distingue 404 vs 403. El self-join de `parent` también es tenant-scoped (ambos lados son `OrgUnits` filtrados). **Anti-mass-assignment** (DTOs cerrados, patch acotado). **Inyección de diagrama** mitigada (XmlWriter auto-escapa; `EscapeDot` neutraliza `\` y `"`). **Export async** (artefacto descargable) gated por `ReportExportResourceAuthorizer` (el fix REX-1 cubre `ORG_UNITS`). **Brecha**: **no hay rate-limiting** (OU-001) — los reads costosos y exports quedan sin guard por-tenant, a diferencia del resto de la plataforma.

### 6.3 Contrato API
Canónico (versionado, tags, swagger, 201+Location+ETag, If-Match→400, `[ProducesStandardErrors]`, OpenAPI guardrail). Convención de status: 409 conflictos de negocio (code/cycle/depth/children), 422 cost-center inválido (referencia no procesable, documentado). Desviaciones de consistencia menores: falta `[Range]` en `pageSize` (OU-005) y `MinSearchLength` en search (OU-002).

### 6.4 Rendimiento
Indexado rico y bien pensado (`(tenant,normalized_code)` único + `(tenant,parent)`, `(tenant,isActive)`, `(tenant,normalized_name)`, `(tenant,type)`, `(tenant,FA)`, `(tenant,CC)`). Jerarquía cargada en 1 query y construida en memoria (sin N+1; misma asunción de escala que `/tree` de Locations). **Puntos débiles**: el search libre usa `Contains` (LIKE '%x%', no-sargable) sobre 6 columnas sin `MinSearchLength` (OU-002) — los índices `(tenant,*)` no ayudan a un comodín inicial; y `GetOrgUnitByIdQueryHandler` ejecuta 3 lecturas del mismo recurso (OU-007). Exports acotados.

### 6.5 Concurrencia y consistencia
Robusto: token rotado en cada mutación, `If-Match` obligatorio, check manual→409, `.IsConcurrencyToken()` (auto-cubierto por `ConcurrencyTokenMappingGuardrailsTests`), y la carrera read-check-write degrada a 409 vía middleware. Jerarquía: ciclo (incl. self-parent) y profundidad validados con defensa contra ciclos preexistentes. **Excepción**: el Create no traduce la violación de `uq_org_units__tenant_code` a 409 (OU-004).

### 6.6 Observabilidad
Auditoría persistida (dentro de la transacción) en los 6 commands con before/after; logging estructurado con scope Tenant/User/TraceId; export auditado (`LogExportAsync`). **Nit**: los handlers emiten **literales de auditoría crudos** (`"ORG_UNIT_CREATED"`, `"OrgUnit"`…) aunque las constantes `AuditEventTypes.OrgUnit*` y `AuditEntityTypes.OrgUnit` ya existen (esta última usada en el propio controller Export) — OU-003.

### 6.7 Pruebas
**Ejecutadas (sesión)**: 69 unit tests verdes — dominio (normalización, move negativo, token), **hierarchy builder** (ciclo descendiente + profundidad > máx), patch applier (165 líneas) y los guardrails (OpenAPI/authz/concurrency). **Revisadas (no ejecutadas)**: 14 integration tests cubriendo create, list+getById (con parent), update (stale→409), move (ciclo→409), inactivate (hijos activos→409), patch (valid/sin-If-Match→400/stale→409/code-path→400), tree+graph+exports, tenant-mismatch→403, **sin-permiso→403** (function-level), audit-event, cost-center→422. **Gaps menores (OU-008)**: activate, update/move happy-path, code-conflict 409 y depth-limit vía API no tienen test explícito (la profundidad sí está unit-testeada en el builder).

### 6.8 Build / DevSecOps
`dotnet build -c Release`: **0/0**. Sin secretos hardcodeados.

## 7. Hallazgos

### OU-001 — Ausencia total de rate-limiting en el controlador
**Severidad:** Media · **Categoría:** Seguridad/Rendimiento · **Ubicación:** `OrgUnitsController` (endpoints 1,3,4,5,6: Search/Tree/Graph/Export/DiagramExport).
**Condición:** ningún endpoint declara `[EnableRateLimiting]`; no existe política de rate-limit OrgUnit en `Program.cs` (sólo las de **autorización** `OrgUnitPolicies.Read/Manage`) ni un governance test (a diferencia de `LocationRateLimitingGovernanceTests`/`CostCenterRateLimitingGovernanceTests`).
**Criterio esperado:** las lecturas costosas (search paginada, árbol/grafo que cargan la jerarquía completa) y los exports deben tener un limitador por usuario/tenant, como en CostCenters (search 120/export 10), Locations (tree 60/search 120), CompetencyFramework, LegalRepresentatives y Files.
**Impacto:** superficie de abuso/DoS sin guard por-tenant. Mitigado parcialmente porque los endpoints están autenticados, autorizados, tenant-scoped y los exports acotados (`SynchronousReadLimit`, `MaxDiagramNodes`→413); por eso Media y no Alta.
**Evidencia:** controller sin `[EnableRateLimiting]`; `Program.cs:399/407` sólo authz; sin governance test.
**Recomendación:** añadir `OrgUnitRateLimitPolicies` (Search + Tree/Export al estilo §LG3) + `[EnableRateLimiting]` en Search/Tree/Graph/Export/DiagramExport + `OrgUnitRateLimitingGovernanceTests` drift-proof (por return-type `PagedResponse`/ruta `/tree`/`/export`).
**Prioridad:** Alta · **Esfuerzo:** Bajo-Medio · **Estado:** ✅ **RESUELTO (2026-06-07)** — `OrgUnitRateLimitPolicies` (3 políticas single-source: `org-units-search` 120 / `org-units-tree` 60 [tree+graph] / `org-units-export` 10 [export+diagram-export]); registradas en `Program.cs` (defaults familia 120/60/10); `[EnableRateLimiting]` en los 5 reads costosos (Search/Tree/Graph/Export/DiagramExport); `OrgUnitRateLimitingGovernanceTests` drift-proof por return-type `PagedResponse` + sufijos de ruta `/tree`·`/graph`·`/export`·`/diagram-export` (3 invariantes: cada read costoso gated, sin política huérfana, las 3 políticas aplicadas). build 0/0 · unit 70/70.

### OU-002 — Search/Export sin longitud mínima (`MinSearchLength`)
**Severidad:** Baja · **Categoría:** Rendimiento/Contrato · **Ubicación:** `SearchOrgUnitsQueryValidator`/`GetOrgUnitExportRowsQueryValidator` (sólo `MaximumLength(150)`); `OrgUnitRepository.SearchAsync` líneas 77-89.
**Condición:** un `q` de 1 carácter dispara `NormalizedCode/Name.Contains(q)` (LIKE '%x%', no-sargable) sobre 6 columnas con 4 joins. No hay `OrgUnitValidationRules.MinSearchLength`.
**Criterio esperado:** mínimo tras `Trim` (≥2) como en Locations §LG5/§12.8 y CostCenters R4.
**Impacto:** scan costoso evitable; **compone con OU-001** (sin rate-limit, amplifica el abuso). Acotado por la cardinalidad por-tenant y los índices de filtro.
**Recomendación:** `MinSearchLength=2` + `IsValidSearchLength` en ambos validators (mirror de Locations); idealmente un `OrgUnitSearchValidatorTests`.
**Prioridad:** Media · **Esfuerzo:** Bajo · **Estado:** ✅ **RESUELTO (2026-06-07)** — `OrgUnitValidationRules.MinSearchLength=2` + `IsValidSearchLength` (mirror de `LocationValidationRules`); aplicado en `SearchOrgUnitsQueryValidator` **y** `GetOrgUnitExportRowsQueryValidator` (`.Must(...).WithMessage($"...")` interpolado → no requiere resx, igual que Locations); `OrgUnitSearchValidatorTests` cubre ambos validators (1-char rechazado en `Search`, vacío/whitespace = sin filtro, ≥2 válido). build 0/0 · unit 29/29 (incl. `BackendMessageLocalization`).

### OU-003 — Literales de auditoría crudos pese a existir constantes
**Severidad:** Baja · **Categoría:** Mantenibilidad/Observabilidad · **Ubicación:** los 6 command handlers (p.ej. líneas 655-656, 779-780, 884-885, 957-958, 1035, 1139-1140).
**Condición:** se emiten `"ORG_UNIT_CREATED"/"ORG_UNIT_UPDATED"/"ORG_UNIT_MOVED"/"ORG_UNIT_ACTIVATED"/"ORG_UNIT_INACTIVATED"` y entity `"OrgUnit"` como literales, aunque `AuditCatalog`/`AuditEventTypes` ya define `OrgUnitCreated…OrgUnitInactivated` (líneas 54-58, registradas en el array `All`) y `AuditEntityTypes.OrgUnit` ya se usa en el propio controller (Export, línea 174).
**Criterio esperado:** 0 literales de auditoría crudos (familia Locations §LG8).
**Impacto:** riesgo de drift (si una constante cambia de valor, los literales no la siguen); inconsistencia interna.
**Recomendación:** reemplazar por `AuditEventTypes.OrgUnit*` y `AuditEntityTypes.OrgUnit` en los handlers.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ **RESUELTO (2026-06-07)** — los 6 command handlers (Create/Update/Move/Activate/Inactivate/Patch) usan `AuditEventTypes.OrgUnit{Created,Updated,Moved,Activated,Inactivated}` + `AuditEntityTypes.OrgUnit`; 0 literales de auditoría crudos restantes (verificado por grep). Espeja la limpieza §LG8 de Locations (manual, sin guardrail dedicado — no existe uno en la familia). build 0/0 · unit 24/24.

### OU-004 — Race de código duplicado en Create devuelve 500 (no 409)
**Severidad:** Baja · **Categoría:** Concurrencia/Contrato · **Ubicación:** `CreateOrgUnitCommandHandler` (pre-check `CodeExistsAsync` línea 566 + `SaveChanges` línea 648).
**Condición:** dos creates concurrentes con el mismo código pasan el pre-check; el segundo viola `uq_org_units__tenant_code`. `UnitOfWork` lanza `UniqueConstraintViolationException`, que el handler no atrapa y el middleware sólo mapea concurrencia→409 → **500**.
**Criterio esperado:** `409 ORG_UNIT_CODE_CONFLICT` (mismo patrón que CostCenters R2 / LocationLevels LV-002).
**Impacto:** 500 espurio en ventana estrecha; sin corrupción (índice único protege). Admin-only.
**Recomendación:** capturar `UniqueConstraintViolationException`→`OrgUnitErrors.CodeConflict`; single-source del nombre de índice. Sistémico en la familia → candidato a solución de plataforma.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ **RESUELTO (2026-06-07)** — `OrgUnitValidationRules.CodeUniqueConstraintName="uq_org_units__tenant_code"` (single-source: `OrgUnitConfiguration.HasDatabaseName` ahora lo referencia, **valor idéntico → sin migración**); helper `OrgUnitConstraintViolations.IsCodeConflict` (mirror de `CostCenterConstraintViolations`); catch `UniqueConstraintViolationException when IsCodeConflict → 409 OrgUnitErrors.CodeConflict` añadido a **Create y Update** (ambos mutan `Code`; Patch no toca code → exento). build 0/0 · unit 24/24. (Sin test dedicado: el race no es determinístico y la familia tampoco lo testea; el path secuencial ya lo cubre el pre-check `CodeExistsAsync`.)

### OU-005 — `[Range]` ausente en `pageSize` + sin guardrail de paginación
**Severidad:** Baja · **Categoría:** Contrato · **Ubicación:** `OrgUnitsController.Search` (`[FromQuery] int pageSize = 20`, `int page = 1`).
**Condición:** los params carecen de `[Range(1, MaxPageSize)]` (presente en Location/CostCenter); no existe `OrgUnitPaginationGuardrailsTests`. El validator `InclusiveBetween(1,100)` cubre el caso funcional (→400), pero `page` no tiene tope superior.
**Criterio esperado:** `[Range]` en el límite del controller + guardrail drift-proof (§LG6/CC2).
**Impacto:** inconsistencia de contrato; `page` enorme → riesgo sistémico de int-overflow en `(page-1)*pageSize` (anotado a nivel plataforma).
**Recomendación:** añadir `[Range(1, OrgUnitValidationRules.MaxPageSize)]` a `pageSize` y un guardrail de paginación para la familia OrgUnits.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ **RESUELTO (2026-06-07)** — `[Range(1, OrgUnitValidationRules.MaxPageSize)]` en `OrgUnitsController.Search` (`pageSize` default también single-sourced a `OrgUnitValidationRules.DefaultPageSize`); `OrgUnitPaginationGuardrailsTests` drift-proof (estructural por namespace+`^OrgUnits` regex, mirror de `CostCenterPaginationGuardrailsTests`: bounds 1..MaxPageSize coinciden con el `InclusiveBetween` del handler + sentinel de no-drift). build 0/0 · unit 26/26. (`page` sin tope superior = riesgo sistémico de int-overflow, anotado a nivel plataforma — fuera de alcance de este fix por-controlador.)

### OU-006 — Serialización de diagrama (GraphML/DOT/JSON) en el controller
**Severidad:** Observación · **Categoría:** Arquitectura · **Ubicación:** `OrgUnitsController.BuildGraphMl`/`BuildDot`/`EscapeDot` + `DiagramExport` (≈líneas 215-260, 440-550).
**Condición:** ~110 líneas de formateo de salida viven en el controller.
**Impacto:** controller menos delgado; lógica de presentación difícil de testear unitariamente (hoy sólo vía integración).
**Recomendación:** extraer a un `OrgUnitDiagramWriter`/formatter inyectable (paridad con `ReportExportRowWriter`).
**Prioridad:** Baja · **Esfuerzo:** Medio · **Estado:** ✅ **RESUELTO (2026-06-07)** — `OrgUnitDiagramWriter` (stateless, `CLARIHR.Api.Common`, registrado `AddSingleton`) con `WriteGraphMl`/`WriteDot`/`WriteJson`; el controller lo inyecta y conserva solo el dispatch de formato + audit + content-type/filename. ~110 líneas de serialización fuera del controller; usings `System.Globalization`/`System.Text.Json`/`System.Xml` eliminados del controller. `OrgUnitDiagramWriterTests` (5 casos: GraphML well-formed+XML-escape, DOT digraph+escape `\`/`"`, JSON round-trip) — antes solo alcanzable por integración. build 0/0 · unit 31/31.

### OU-007 — Lecturas redundantes en GetById y cast `ContinueWith`
**Severidad:** Observación · **Categoría:** Rendimiento · **Ubicación:** `GetOrgUnitByIdQueryHandler` líneas 432-438; `OrgUnitRepository.GetHierarchyAsync` línea 182-183.
**Condición:** GetById ejecuta `GetResponseByIdAsync` + `GetByIdAsync` (sólo para el id interno) + `HasActiveChildrenAsync` (3 queries). `GetHierarchyAsync` usa `.ContinueWith` para el cast covariante. Además, la lista con `includeAllowedActions` pasa `hasActiveChildren: false` fijo (la acción `canInactivate` por ítem puede no reflejar hijos activos; el servidor igual lo enforce en el inactivate real → 409).
**Impacto:** mínimo (1-2 queries extra por get; exactitud de UI en la lista). 
**Recomendación:** proyectar el id interno + `hasActiveChildren` en `GetResponseByIdAsync`; `async/await` en `GetHierarchyAsync`; documentar el tradeoff de la lista.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ **RESUELTO (2026-06-07)** — (a) nuevo `IOrgUnitRepository.HasActiveChildrenByPublicIdAsync` (1 query join child→parent por public id) reemplaza `GetByIdAsync`+`HasActiveChildrenAsync` en GetById → **3 lecturas a 2** (la sobrecarga `HasActiveChildrenAsync(long)` queda para Inactivate, que ya tiene la entidad); (b) `GetHierarchyAsync` ahora `async/await` (eliminado el `.ContinueWith` para el cast covariante); (c) el `hasActiveChildren: false` de la lista documentado explícitamente como hint optimista by-design (evita N+1; el `canInactivate` real lo enforce el handler Inactivate→409). build 0/0 · unit 31/31.

### OU-008 — Gaps menores de cobertura de integración
**Severidad:** Observación · **Categoría:** Pruebas · **Ubicación:** `ApiIntegrationTests.cs`.
**Condición:** sin test explícito de activate, update/move happy-path, code-conflict 409, ni depth-limit vía API (la profundidad sí está unit-testeada en el builder).
**Impacto:** bajo — las rutas duras (ciclo, hijos activos, concurrencia, tenant, authz negativo, audit, cost-center) sí están cubiertas.
**Recomendación:** completar los happy-paths y el 409 de código duplicado.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ **RESUELTO (2026-06-07)** — 4 integration tests nuevos: `OrgUnits_Update_WithValidIfMatch_ShouldApplyChangesAndRotateToken`, `OrgUnits_Move_ToNewParent_ShouldReparentAndRotateToken`, `OrgUnits_Activate_AfterInactivation_ShouldReactivateAndRotateToken` (inactivate→activate), `OrgUnits_Create_WithDuplicateCode_ShouldReturn409Conflict` (path secuencial pre-check `ORG_UNIT_CODE_CONFLICT`). Compilan (0/0); requieren DB para ejecutar (convención del proyecto: integration revisados, no corridos esta sesión). **Depth-limit vía API queda deliberadamente cubierto por el unit test de `OrgUnitHierarchyBuilder`** (una cadena de 15 niveles vía API sería pesada/frágil; la lógica ya está unit-testeada).

### OU-009 — Naming: "Org Units" rompe el estándar de nombres de la API (deuda añadida a petición del usuario)
**Severidad:** Observación · **Categoría:** Contrato/Naming · **Ubicación:** `OrgUnitsController` → `[Tags("Org Units")]`, clase `OrgUnitsController`, ruta `org-units`.
**Condición:** censo de los 47 controllers (Tag vs ruta) → el estándar de facto del Tag es **palabras completas en Title Case** (Cost Centers, Location Groups, Location Hierarchy, Work Center Types, Legal Representatives, Personnel Files, Company Users, Position Description Catalogs, General Catalogs…). **"Org Units" es el único Tag que abrevia** el sustantivo de dominio ("Org" por "Organization"). La ruta `org-units` y el catálogo hermano `org-structure-catalogs` comparten la misma abreviatura.
**Criterio esperado:** coherencia con el estándar interno → **"Organization Units"**. (Matiz honesto: "Org Unit"/"OU" es término estándar de industria, por lo que es una decisión de **consistencia**, no de corrección; pero dentro de este código es el outlier.)
**Impacto:** inconsistencia del contrato público (Swagger) y de los identificadores internos; el usuario lo señaló explícitamente como pérdida del estándar.
**Evidencia:** censo de `[Tags]`/`[Route]` de `src/CLARIHR.Api/Controllers/*` (2026-06-07); único Tag abreviado.
**Recomendación (por costo/impacto):**
- **No-breaking (recomendado ya):** Tag `[Tags("Org Units")]`→`[Tags("Organization Units")]` (cosmético, sólo Swagger); opcionalmente renombrar clase/namespace `OrgUnitsController`→`OrganizationUnitsController` (interno, sin impacto de wire).
- **Breaking (decisión deliberada):** ruta `org-units`→`organization-units` rompe el contrato del frontend; hacerlo sólo si no hay consumidores productivos, o versionarlo. Coordinar con `org-structure-catalogs` (doc 16 §OSC-008) para un renombrado coherente del área.
**Prioridad:** Baja · **Esfuerzo:** Bajo (Tag/clase) / Medio (ruta) · **Estado:** ✅ **RESUELTO (2026-06-07) — rename COMPLETO (breaking, decidido por el usuario)**:
- **Tag** `[Tags("Org Units")]`→`[Tags("Organization Units")]`; **display label** del módulo comercial `CommercialModuleCatalog` "Org Units"→"Organization Units" (el entitlement key `CommercialModuleKeys.OrgUnits="ORG_UNITS"` se mantiene).
- **Clase** `OrgUnitsController`→`OrganizationUnitsController` (+ archivo renombrado `git mv`); doc-comments en `OrgUnitPolicies`/`OrgUnitRateLimitPolicies` actualizados.
- **Ruta (BREAKING)** `org-units`→`organization-units` en los 12 endpoints (`companies/{id}/organization-units[...]` + `organization-units/{id}[...]`). El **filename de export** (`"org-units"`/`"OrgUnits"`) y el namespace interno `Features.OrgUnits`/dominio `OrgUnit`/`ReportExportResources.OrgUnits` se mantienen (no son contrato de ruta).
- **Guardrails actualizados** (correctness-critical, dependían del nombre): `AuthorizationPolicyConventionGovernanceTests.GovernedFamilyRegex` (`OrgUnits`→`OrganizationUnits`); `OpenApiContractGuardrailsTests.Families` (regex `^OrganizationUnits` + tag "Organization Units"); `OrgUnitRateLimitingGovernanceTests`/`OrgUnitPaginationGuardrailsTests` regex.
- **Integration tests**: 27 refs de ruta `/org-units`→`/organization-units` (ApiIntegrationTests×25 + InternalCatalogsIntegrationTests×2).
- **`openapi.yaml`** regenerado mecánicamente (9 paths + 12 tags); idéntico a lo que produciría la regen vía swagger.
- Verificación: **build 0/0 (solución completa) · unit 1672/1672 · integration project compila**. ⚠️ **El frontend DEBE actualizar las llamadas a `/org-units`→`/organization-units`** (cambio de contrato deliberado). Integration tests requieren DB (no ejecutados esta sesión).

## 8. Hallazgos fuera de alcance / trazabilidad

- **Export async authz (positivo):** `ReportExportResourceAuthorizer` cubre `ReportExportResources.OrgUnits` → `EnsureCanReadAsync`, por lo que la descarga del artefacto de export (vía `ReportExportJobsController`) **está gated** (el fix REX-1 de la auditoría de Files aplica a OrgUnits). No re-flaggear como IDOR.
- **OU-004 y OU-005** comparten causa sistémica con la familia (unique-constraint→409 e int-overflow de paginación); idealmente resueltos a nivel plataforma.
- **Asunción de escala de jerarquía:** `GetHierarchyAsync` carga todas las unidades del tenant en memoria para árbol/grafo/export (igual que `/tree` de Locations §LG7). Aceptable con la cardinalidad esperada; reevaluar si un tenant supera miles de unidades.
- **`OrgStructureCatalogs` (tipo de unidad / área funcional)** es una vertical hermana referenciada; su auditoría es independiente.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Impacto | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|---|
| OU-001 | Media | Seguridad/Perf | Sin rate-limiting | Abuso/DoS sin guard | Bajo-Medio | Alta | ✅ **RESUELTO**: Políticas + `[EnableRateLimiting]` + governance test |
| OU-002 | Baja | Rendimiento | Sin MinSearchLength | Scan no-sargable | Bajo | Media | ✅ **RESUELTO**: `MinSearchLength=2` en ambos validators + `OrgUnitSearchValidatorTests` |
| OU-003 | Baja | Mantenibilidad | Literales de auditoría | Drift | Bajo | Baja | ✅ **RESUELTO**: `AuditEventTypes`/`AuditEntityTypes` en los 6 handlers |
| OU-004 | Baja | Concurrencia | Race dup-code → 500 | 500 espurio | Bajo | Baja | ✅ **RESUELTO**: Catch `UniqueConstraintViolationException`→409 (Create+Update, single-source índice) |
| OU-005 | Baja | Contrato | Falta `[Range]` + guardrail | Inconsistencia/overflow | Bajo | Baja | ✅ **RESUELTO**: `[Range]` + `OrgUnitPaginationGuardrailsTests` |
| OU-006 | Obs | Arquitectura | Serialización en controller | Testabilidad | Medio | Baja | ✅ **RESUELTO**: `OrgUnitDiagramWriter` inyectable + 5 unit tests |
| OU-007 | Obs | Rendimiento | Lecturas redundantes | Menor | Bajo | Baja | ✅ **RESUELTO**: GetById 3→2 queries + async/await + tradeoff documentado |
| OU-008 | Obs | Pruebas | Gaps de happy-path | Menor | Bajo | Baja | ✅ **RESUELTO**: +4 integ tests (update/move/activate happy + dup-code 409) |
| OU-009 | Obs | Naming | "Org Units" abrevia vs estándar | Inconsistencia | Bajo/Medio | Baja | ✅ **RESUELTO**: rename completo Tag+clase+ruta→"Organization Units"/`organization-units` (breaking; frontend debe actualizar) |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`OrgUnitsController`) | **Aprobado con observaciones** |
| Endpoints internos | Cubiertos (14 integ + unit; gaps menores OU-008) |
| Seguridad | Aprobado con observaciones (OU-001 rate-limit) |
| Arquitectura | Aprobado (con OU-006) |
| Performance | Aprobado con observaciones (OU-002/OU-007) |
| Concurrencia | Aprobado con observaciones (OU-004) |
| Pruebas | Aprobado |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**El controlador puede avanzar a QA.** Recomendado cerrar **OU-001** (rate-limiting) y **OU-002** (MinSearchLength) antes de exposición productiva amplia; OU-003/OU-004/OU-005 como hardening de consistencia.

## 11. Recomendaciones finales

1. **OU-001 + OU-002 (juntas, mayor ROI):** añadir rate-limiting (políticas + `[EnableRateLimiting]` + governance test) y `MinSearchLength=2`; ambos cierran la superficie de abuso del search/exports y alinean con la familia.
2. **OU-003:** reemplazar literales por constantes de auditoría (1 PR mecánico).
3. **OU-004:** unique-constraint→409 en Create.
4. **OU-005:** `[Range]` + guardrail de paginación.
5. **OU-006/OU-007/OU-008:** limpieza/extracción y completar tests de happy-path de forma oportunista.
6. Mantener las fortalezas: autorización de dos capas, jerarquía testeada, export gated, índices ricos, concurrencia robusta.

## 12. Anexos / Evidencia revisada

- Controller: `OrgUnitsController.cs` (12 endpoints + serializadores de diagrama).
- Aplicación: `OrgUnitAdministration.cs` (1614 líneas: DTOs/queries/commands/validators/handlers + `OrgUnitPatchApplier` + `OrgUnitPolicyAdapter` + `OrgUnitHierarchyBuilder`), `OrgUnitCommon.cs`, `OrgUnitPolicies.cs`.
- Dominio: `OrgUnit.cs`, `OrgUnitNormalization.cs`, `TenantEntity.cs`.
- Persistencia: `IOrgUnitRepository.cs`, `OrgUnitRepository.cs`, `OrgUnitConfiguration.cs`, `ApplicationDbContext.cs` (filtro tenant), `UnitOfWork.cs`.
- Seguridad/reportes: `OrgUnitAuthorizationService.cs`, `Program.cs` (399-414), `OrgUnitsExportHandler.cs`, `ReportExportResourceAuthorizer.cs`, `ReportExportResources.cs`, `AuditCatalog.cs`.
- Pruebas: `OrgUnitDomainTests.cs` (dominio + builder), `OrgUnitPatchApplierTests.cs`, `ApiIntegrationTests.cs` (≈4559-4928, 5176, 8491).
- Ejecución: `dotnet build -c Release` → 0/0; `dotnet test --filter ~OrgUnit|~OpenApiContractGuardrails|~AuthorizationPolicyConventionGovernance|~ConcurrencyTokenMappingGuardrails` → **69/69 passed** (sesión 2026-06-07).
