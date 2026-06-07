# Auditoría Técnica por Controlador — LocationLevelsController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Relacionada: [doc 13 — LocationHierarchyController](13-location-hierarchy-controller-audit-2026-06-07.md) (config; comparte el mismo archivo de aplicación).

## 1. Resumen ejecutivo

`LocationLevelsController` administra los **niveles de la jerarquía de ubicaciones** por empresa (7 endpoints: list, get-by-id, create, update, patch, activate, inactivate). Es el controlador con **la lógica de negocio más rica de la familia Locations**: reglas de orden de nivel único, "work centers sólo en el último nivel activo", "debe quedar ≥1 nivel activo", y bloqueos por grupos/work-centers activos. Comparte vertical de aplicación (`LocationHierarchyAdministration.cs`), repositorio (`ILocationHierarchyRepository`), dominio (`LocationLevel`), errores (`LocationErrors`) y políticas (`LocationPolicies`) con `LocationHierarchyController`, con un **invariante cruzado**: pasar la config a single-level exige exactamente un nivel activo.

Veredicto: **APROBADO CON OBSERVACIONES**. 0 críticos / 0 altos. La seguridad (autorización de dos capas, aislamiento por tenant fail-closed, anti-mass-assignment, concurrencia optimista, hardening JSON Patch) es sólida y está blindada con *guardrails* drift-proof de toda la familia. El hallazgo material es un **vacío de pruebas (Media)** en las 4 rutas de escritura (Create/Update/Activate/Inactivate), justo donde vive esa lógica de negocio. El resto son nits (Baja/Observación).

| Indicador | Resultado |
|---|---|
| Build (Release, proyecto de pruebas + deps) | ✅ 0/0 |
| Unit tests familia Location (**ejecutados esta sesión**) | ✅ 45/45 passed |
| Patch applier de niveles (unit) | ✅ 11 casos verdes |
| Integration tests niveles (revisados, no ejecutados — requieren DB) | 6 (List, GetById, Patch×4) |
| Hallazgos | 0 Crít · 0 Alto · **1 Media · 1 Baja · 3 Observación** |

## 2. Alcance

**Incluido (vertical directa):**
- Controlador `src/CLARIHR.Api/Controllers/LocationLevelsController.cs` (7 endpoints) — **primario**.
- Aplicación: en `Features/Locations/Hierarchy/LocationHierarchyAdministration.cs` → records `LocationLevelResponse`, queries `GetLocationLevelsQuery`/`GetLocationLevelByIdQuery`, commands `Create/Update/Activate/Inactivate/PatchLocationLevelCommand`, sus validators y handlers, `LocationLevelPatchApplier`/`LocationLevelPatchState` y `LocationHierarchyMapper.Map(LocationLevel)`.
- Dominio: `Domain/Locations/LocationLevel.cs`, `LocationNormalization.cs`.
- Persistencia: métodos de nivel de `ILocationHierarchyRepository`/`LocationHierarchyRepository` (`GetLevelsAsync`, `GetLevelByIdAsync`, `LevelExistsOutsideTenantAsync`, `LevelOrderExistsAsync`, `CountActiveLevelsAsync`, `HasAnyActiveWorkCenterLevelAsync`, `GetHighestActiveLevelOrderAsync`, `AddLevel`); EF `LocationLevelConfiguration`; filtro global por tenant.
- Dependencia directa: `ILocationGroupRepository.HasActiveGroupsAtLevelAsync` (bloqueos de inactivación/cambio de nivel).
- Seguridad/contrato: `LocationPolicies`, `LocationPermissionCodes`, `ILocationAuthorizationService`, `Program.cs` (policies), `IfMatchModelBinder`, `JsonPatchHardening`, `JsonPatchOperationMapper`, `ResultExtensions`, `UnhandledExceptionMiddleware`.
- Provisión/i18n: `LocationSeedService` (3 niveles por defecto), `BackendMessages(.es).resx`.
- Pruebas: `LocationLevelPatchApplierTests`, `LocationDomainTests`, `LocationRulesTests`, `LocationPaginationGuardrailsTests`, `LocationRateLimitingGovernanceTests`, `AuthorizationPolicyConventionGovernanceTests`, `ConcurrencyTokenMappingGuardrailsTests`, `OpenApiContractGuardrailsTests`; integración `ApiIntegrationTests.cs`.

**Excluido:** auditoría integral de la API; `LocationHierarchyController` (config) salvo por el invariante cruzado (auditado en doc 13); `LocationGroups`/`WorkCenters*` salvo dependencia directa; pruebas de carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en las reglas de negocio de niveles y su coherencia transaccional/concurrencia, verificando los *guardrails* drift-proof. Evidencia de ejecución: build Release (0/0) + 45 unit tests de Location ejecutados en esta sesión (verde, incluye los 11 del patch applier de niveles). Integración revisada por código (requiere Postgres/Testcontainers; no ejecutada → limitación declarada).

## 4. Inventario de endpoints

| Método | Ruta | Propósito | Handler | Request | Response | Riesgo |
|---|---|---|---|---|---|---|
| GET | `/companies/{companyId}/location-levels` | Listar niveles ordenados por `levelOrder` | `GetLocationLevelsQueryHandler` | — | `IReadOnlyCollection<LocationLevelResponse>` (200) | Bajo |
| GET | `/location-levels/{id}` | Obtener nivel por id (tenant del JWT) | `GetLocationLevelByIdQueryHandler` | — | `LocationLevelResponse` (200) | Bajo |
| POST | `/companies/{companyId}/location-levels` | Crear nivel | `CreateLocationLevelCommandHandler` | `CreateLocationLevelRequest` | `LocationLevelResponse` (201 + `Location` + `ETag`) | Medio |
| PUT | `/location-levels/{id}` | Reconfigurar (displayName + flags como unidad) | `UpdateLocationLevelCommandHandler` | `UpdateLocationLevelRequest` + `If-Match` | `LocationLevelResponse` (200) | Medio |
| PATCH | `/location-levels/{id}` | Renombrar (JSON Patch RFC-6902, sólo `/displayName`) | `PatchLocationLevelCommandHandler` | `JsonPatchDocument` + `If-Match` | `LocationLevelResponse` (200) | Bajo |
| PATCH | `/location-levels/{id}/activate` | Reactivar nivel inactivo | `ActivateLocationLevelCommandHandler` | `If-Match` | `LocationLevelResponse` (200) | Medio |
| PATCH | `/location-levels/{id}/inactivate` | Inactivar nivel | `InactivateLocationLevelCommandHandler` | `If-Match` | `LocationLevelResponse` (200) | Medio |

`levelOrder` inmutable; activación sólo por `/activate`-`/inactivate`; sin `DELETE` (se desactiva). Patrón de rutas mixto correcto: list/create llevan `companyId` (validado contra tenant); id-only resuelven tenant del JWT (sin IDOR).

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller sin lógica de negocio | PASS | Sólo despacha CQRS y mapea `ActionResult` |
| Arquitectura | Validators separados + dominio con invariantes | PASS | `FluentValidation` + `LocationLevel` lanza en estados inválidos (defensa en profundidad) |
| Arquitectura | Transacciones en cambios críticos | PASS | Los 4 commands de escritura: `BeginTransactionAsync` + 2 `SaveChanges` (mutación + audit) + commit/rollback |
| Arquitectura | Async/await | PASS (nit) | Async en toda la vertical; ver OBS-2 (`ContinueWith`) |
| Seguridad | Autenticación | PASS | `[Authorize]` |
| Seguridad | Autorización por operación (Read vs Manage) | PASS | `[AuthorizationPolicySet(Read,Manage)]` + gate `EnsureCanReadAsync/EnsureCanManageAsync` por handler |
| Seguridad | Superset declarativa ⊇ handler | PASS | `AuthorizationPolicyConventionGovernanceTests`; controller en `GovernedFamilyRegex` |
| Seguridad | BOLA/IDOR | PASS | Rutas id-only sin companyId (tenant del JWT); filtro global por tenant |
| Seguridad | Tenant isolation + 404/403 correctos | PASS | `LevelExistsOutsideTenantAsync` (IgnoreQueryFilters) decide 404 vs `TENANT_MISMATCH` 403 |
| Seguridad | Mass assignment | PASS | Request DTOs explícitos; `levelOrder` inmutable en PUT/PATCH |
| Seguridad | DoS por JSON Patch | PASS | `[RequestSizeLimit(64KB)]` + tope 50 ops en validator (`JsonPatchHardening`) |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(Locations)` en el gate |
| Contrato | RESTful + verbos + versionado | PASS | `api/v{version}`, verbos correctos, `[ApiVersion("1.0")]` |
| Contrato | 201 + Location + ETag en Create | PASS | `ToCreatedAtActionResult(..., publicId, ConcurrencyToken)` |
| Contrato | Status codes + errores documentados | PASS | `[ProducesResponseType]` + `[ProducesStandardErrors]` + `[SwaggerOperation]` |
| Contrato | OpenAPI guardrail enrolado | PASS | `OpenApiContractGuardrailsTests.Families` → `LocationLevels`/"Location Levels" |
| Contrato | ETag/If-Match en updates | PASS | `[FromIfMatch]` (400 si falta/malformado) + `ToActionResultWithETag` en 4 endpoints |
| Contrato | Paginación en listados | OBS | List devuelve colección completa (no `PagedResponse`) — ver **OBS-3** (cardinalidad acotada, por diseño) |
| Rendimiento | Índices | PASS | `uq_location_levels__tenant_order` (único), `ix_location_levels__tenant_active_order` (compuesto) |
| Rendimiento | N+1 / AsNoTracking | PASS (nit) | `GetLevelsAsync` `AsNoTracking`; sin N+1; ver OBS-2 |
| Rendimiento | Rate limiting | NO APLICA (justificado) | §LG3 limita sólo `PagedResponse`/`/tree`; list de niveles excluido por diseño |
| Concurrencia | Optimista + If-Match + 409 | PASS | Token Guid rotado; check manual→409; `.IsConcurrencyToken()` backstop (auto-cubierto) |
| Concurrencia | Read-check-write race → 409 | PASS | `UnhandledExceptionMiddleware` mapea `DbUpdateConcurrencyException`→409 |
| Concurrencia | Unique-constraint en Create → 409 | **FAIL** | Ver **LV-002**: race `levelOrder` duplicado → 500 |
| Concurrencia | Validación de estado/transiciones | PASS | last-active-level, required-active, work-centers-last-level, has-active-groups, allows-WC-in-use |
| Observabilidad | Audit logs en escrituras | PASS | `LogAsync`+`SaveChanges` en los 4 commands; `AuditEventTypes.LocationLevelCreated/Updated/Activated/Inactivated` |
| Pruebas | Patch applier (unit) | PASS | `LocationLevelPatchApplierTests` 11 casos |
| Pruebas | Handlers de escritura (unit/integración) | **WARNING** | Ver **LV-001**: Create/Update/Activate/Inactivate sin cobertura |
| Pruebas | Lectura/Patch (integración) | PASS | List, GetById, Patch (valid/no-If-Match/stale/structural-flag) |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS limpia y consistente con la familia. **Defensa en profundidad por capas** en las reglas de flags: (1) `FluentValidation` (`Must(!IsRequired || IsActive)`, `Must(!AllowsWorkCenters || IsActive)`) → 400; (2) handler (re-check + reglas que requieren DB) → 409; (3) dominio (`LocationLevel.Create/Update/Inactivate` lanzan `InvalidOperationException`) → backstop. El `RequestDispatcher` ejecuta validators **antes** del handler (retorno temprano), por lo que algunas ramas del handler quedan inalcanzables (ver **OBS-1**). El patch applier restringe deliberadamente la superficie a `/displayName` (flags estructurales se validan como unidad en PUT, activación por endpoints dedicados), decisión bien documentada y con prueba de candado. Cada escritura envuelve mutación + auditoría en transacción.

### 6.2 Seguridad
**Autorización de dos capas**: política declarativa superset + gate preciso (tenant-match + entitlement módulo + claims/grants RBAC), enforced por governance test, controller dentro de `GovernedFamilyRegex`. **Sin IDOR**: las rutas `/location-levels/{id}` no aceptan companyId; el tenant sale del JWT y el filtro global EF (fail-closed) hace invisible cualquier nivel ajeno; `LevelExistsOutsideTenantAsync` (con `IgnoreQueryFilters`) sólo distingue 404 vs 403. **Anti-mass-assignment**: `levelOrder` inmutable, request DTOs cerrados. **DoS JSON Patch** mitigado (64KB + 50 ops). Sin PII. OWASP API Top 10 aplicable: cubierto.

### 6.3 Contrato API
Totalmente canónico: versionado, `[Tags]`/`[SwaggerOperation]` descriptivos, `[ProducesStandardErrors]`, `201 + Location` (con truco `publicId` para `PublicContractRouteConvention`) + `ETag` en Create, `If-Match` (400 si falta) + `ETag` en las 4 mutaciones. Enrolado en el OpenAPI guardrail. Convención 409 para conflictos de negocio (consistente app-wide).

### 6.4 Rendimiento
Índices correctos (único `(tenant,levelOrder)` respalda la unicidad; compuesto `(tenant,isActive,levelOrder)` respalda las consultas de "último nivel activo"/"conteo de activos"). Cardinalidad por tenant pequeña (≈3 niveles). `AsNoTracking` en lecturas. Sin N+1. Nits: OBS-2 (`ContinueWith` para cast) y el re-`OrderBy` en memoria del list handler (datos ya ordenados en SQL).

### 6.5 Concurrencia y consistencia
Robusto: token rotado en cada mutación, `If-Match` obligatorio, check manual→409 **y** `.IsConcurrencyToken()` (auto-descubierto por `ConcurrencyTokenMappingGuardrailsTests`). La carrera read-check-write degrada a **409** vía middleware. **Excepción (LV-002)**: el Create no traduce la violación de unicidad de `levelOrder` a 409 → 500.

### 6.6 Observabilidad
Auditoría persistida (dentro de la transacción) en los 4 commands de escritura con tipos por constantes y before/after; logging estructurado con scope Tenant/User/TraceId. Adecuado.

### 6.7 Pruebas
**Ejecutadas (sesión)**: 45 unit tests de Location verdes, incluidos los **11** del patch applier (cobertura excelente de la superficie PATCH: replace displayName, flags/levelOrder/concurrencyToken rechazados, op no soportada, path anidado/desconocido, remove rechazado, blank-tras-replace, sin-ops). **Revisadas (no ejecutadas)**: 6 integration tests de niveles → List, GetById, Patch (valid/sin-If-Match/stale/structural-flag). **Vacío (LV-001)**: ni unit ni integración ejercitan los handlers **Create/Update/Activate/Inactivate**, donde está la lógica más rica (level-order-conflict, required-must-remain-active, work-centers-only-on-last-level en sus 3 variantes, last-active-level-required, location-level-has-active-groups, allows-work-centers-in-use) ni los caminos negativos de autz de función (Read→403 en Manage) o cross-tenant en niveles (404 vs 403).

### 6.8 Build / DevSecOps
`dotnet build -c Release` (proyecto de pruebas + deps): **0/0**. Sin secretos hardcodeados.

## 7. Hallazgos

### LV-001 — Handlers de escritura de niveles sin cobertura de pruebas
**Severidad:** Media · **Categoría:** Pruebas · **Ubicación:** `LocationHierarchyAdministration.cs` → `CreateLocationLevelCommandHandler` (293-371), `UpdateLocationLevelCommandHandler` (373-476), `ActivateLocationLevelCommandHandler` (478-555), `InactivateLocationLevelCommandHandler` (557-646).
**Condición:** búsqueda en `tests/` → 0 pruebas que despachen esos commands. Integración cubre sólo lecturas + Patch.
**Criterio esperado:** las reglas de negocio críticas y los caminos negativos de autz/tenant deben tener pruebas (unit de handler y/o integración).
**Impacto:** una regresión en la lógica de niveles (la más compleja de la familia) no sería detectada por CI. No es un defecto de corrección (la lógica revisada es correcta), sino riesgo de regresión/mantenibilidad.
**Evidencia:** §6.7; ausencia de los nombres de comando en `tests/`.
**Recomendación:** unit tests de handler con repos fake (reusar `TestLocationHierarchyRepository`/`TestLocationGroupRepository` de `LocationRulesTests`) para cada regla, + integración para Create/Update/Activate/Inactivate (incl. 201+Location+ETag, work-centers-last-level, last-active-level, has-active-groups) + un negativo Read→403 + un cross-tenant 404/403.
**Prioridad:** Alta · **Esfuerzo:** Medio · **Estado:** Abierto

### LV-002 — Race de `levelOrder` duplicado en Create devuelve 500 en vez de 409
**Severidad:** Baja · **Categoría:** Concurrencia/Contrato · **Ubicación:** `CreateLocationLevelCommandHandler` (pre-check `LevelOrderExistsAsync` línea 315 + `SaveChanges` línea 347).
**Condición:** dos creates concurrentes con el mismo `levelOrder` pasan el pre-check; el segundo viola `uq_location_levels__tenant_order`. `UnitOfWork` traduce a `UniqueConstraintViolationException`, el handler hace `catch { rollback; throw; }`, y el middleware sólo mapea `DbUpdateConcurrencyException`→409 → cae a **500**.
**Criterio esperado:** conflicto de unicidad bajo carrera → `409 LOCATION_LEVEL_ORDER_CONFLICT` (mismo patrón ya resuelto en CostCenters R2: capturar `UniqueConstraintViolationException`, idealmente comparando `ConstraintName`).
**Impacto:** 500 espurio en ventana de carrera estrecha; **sin corrupción** (el índice único preserva integridad). Endpoint admin, concurrencia muy baja.
**Evidencia:** `LocationLevelConfiguration.cs:55-57`; `UnitOfWork.cs:18-20`; `UnhandledExceptionMiddleware.cs:44,67`.
**Recomendación:** capturar `UniqueConstraintViolationException` en el Create → `LocationErrors.LevelOrderConflict`; single-source del nombre de índice. Sistémico (varios create handlers); candidato a solución de plataforma.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### OBS-1 — Ramas defensivas inalcanzables en Create/Update (status inconsistente)
**Severidad:** Observación · **Categoría:** Mantenibilidad · **Ubicación:** `CreateLocationLevelCommandHandler` 320-323 y `UpdateLocationLevelCommandHandler` 414-417.
**Condición:** el re-check `command.IsRequired && !command.IsActive → RequiredLevelMustRemainActive (409)` es inalcanzable: el validator (`Must(!IsRequired || IsActive)`) ya rechaza con **400** antes del handler (`RequestDispatcher` valida primero).
**Impacto:** código muerto; además devolvería 409 donde el camino real devuelve 400 (inconsistencia teórica). El dominio también lanza como backstop (triple capa).
**Recomendación:** eliminar el re-check del handler o documentarlo explícitamente como backstop intencional (alineando el status si se conserva).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### OBS-2 — `GetLevelsAsync` usa `.ContinueWith(...)` + re-orden redundante
**Severidad:** Observación · **Categoría:** Rendimiento/Mantenibilidad · **Ubicación:** `LocationHierarchyRepository.cs:26-32`; `GetLocationLevelsQueryHandler` línea 285.
**Condición:** `GetLevelsAsync` convierte `Task<List<T>>`→`Task<IReadOnlyList<T>>` con `ContinueWith` (scheduler por defecto) en vez de `async/await`; y el list handler vuelve a `OrderBy(LevelOrder)` datos ya ordenados en SQL.
**Impacto:** mínimo (anti-patrón menor; listas ≈3 ítems).
**Recomendación:** método `async` con `await ... ToListAsync()` (covarianza implícita); quitar el `OrderBy` en memoria.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### OBS-3 — `location-levels` list sin paginación
**Severidad:** Observación · **Categoría:** Contrato/Rendimiento · **Ubicación:** `LocationLevelsController.List` → `IReadOnlyCollection<LocationLevelResponse>`.
**Condición:** a diferencia de los listados paginados de la familia, devuelve la colección completa.
**Impacto:** despreciable y **por diseño**: la cardinalidad de niveles por tenant está acotada (≈3, limitada por el único `(tenant,levelOrder)` y las reglas de negocio). Excluido explícitamente de los guardrails de rate-limit y paginación (no es `PagedResponse`).
**Recomendación:** mantener; no convertir a paginado. Documentado para no re-flaggear.
**Prioridad:** Baja · **Esfuerzo:** N/A · **Estado:** Aceptado

## 8. Hallazgos fuera de alcance / trazabilidad

- **Rate-limiting N/A (no es hallazgo):** list de niveles y get-by-id quedan **intencionadamente** fuera del rate-limit (§LG3: sólo `PagedResponse`/`/tree`). Codificado en `LocationRateLimitingGovernanceTests`. No re-flaggear.
- **Invariante cruzado config↔niveles:** la regla single-level (config exige 1 nivel activo) se valida en `UpdateLocationHierarchyConfigCommandHandler` (controlador hermano, doc 13). El `UpdateLocationLevel`/`Inactivate` consistentemente protege "≥1 nivel activo" desde el lado de niveles.
- **LV-002 es sistémico** (varios create handlers de la familia); si se adopta solución de plataforma para unique-constraint→409, incluir este Create.
- **Dependencia `ILocationGroupRepository.HasActiveGroupsAtLevelAsync`** correctamente tenant-scoped (`group.TenantId == tenantId`).

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Impacto | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|---|
| LV-001 | Media | Pruebas | Handlers Create/Update/Activate/Inactivate sin tests | Regresión no detectada | Medio | Alta | Unit de handler + integración + negativos authz/tenant |
| LV-002 | Baja | Concurrencia | Race levelOrder → 500 (no 409) | 500 espurio, sin corrupción | Bajo | Baja | Catch `UniqueConstraintViolationException`→409 |
| OBS-1 | Obs | Mantenibilidad | Ramas defensivas inalcanzables (status inconsistente) | Código muerto | Bajo | Baja | Eliminar o documentar |
| OBS-2 | Obs | Rendimiento | `ContinueWith` + re-orden redundante | Estilo | Bajo | Baja | `async/await`; quitar `OrderBy` |
| OBS-3 | Obs | Contrato | List sin paginación | Despreciable (acotado) | N/A | — | Aceptado por diseño |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`LocationLevelsController`) | **Aprobado con observaciones** |
| Endpoints internos del controller | Parcialmente cubiertos por tests (lectura+Patch sí; escritura no — LV-001) |
| Seguridad | Aprobado |
| Arquitectura | Aprobado |
| Performance | Aprobado |
| Concurrencia | Aprobado con observaciones (LV-002) |
| Pruebas | Observaciones (LV-001) |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**El controlador puede avanzar a QA** con seguimiento de LV-001 (recomendado cerrarlo antes de productivo, por concentrar la lógica de negocio sin pruebas) y LV-002 como hardening.

## 11. Recomendaciones finales

1. **Cerrar LV-001** antes de productivo (mayor ROI): unit de handler para cada regla + integración Create/Update/Activate/Inactivate + negativos Read→403 y cross-tenant 404/403.
2. **LV-002**: traducir unique-constraint→409 en el Create (mirror de CostCenters R2), preferible vía solución de plataforma reutilizable.
3. **OBS-1/OBS-2**: limpieza oportunista en un PR.
4. **OBS-3**: dejar como está (documentado).
5. Mantener los *guardrails* drift-proof (authz convention, rate-limit governance, concurrency-token mapping, OpenAPI families, pagination) — son la red que sostiene el bajo riesgo de la familia.

## 12. Anexos / Evidencia revisada

- Controller: `LocationLevelsController.cs` (7 endpoints + request DTOs anidados).
- Aplicación: `LocationHierarchyAdministration.cs` (commands/queries/validators/handlers de nivel + `LocationLevelPatchApplier`/`State` + mapper), `LocationCommon.cs` (`LocationErrors`, `LocationPermissionCodes`, `LocationValidationRules`), `LocationPolicies.cs`, `JsonPatchHardening.cs`, `RequestDispatcher.cs`.
- Dominio: `LocationLevel.cs`, `LocationNormalization.cs`, `TenantEntity.cs`.
- Persistencia: `ILocationHierarchyRepository.cs`, `LocationHierarchyRepository.cs`, `LocationLevelConfiguration.cs`, `ApplicationDbContext.cs` (filtro tenant), `UnitOfWork.cs`, `LocationGroupRepository.cs` (`HasActiveGroupsAtLevelAsync`).
- Seguridad/contrato: `LocationAuthorizationService.cs`, `Program.cs` (policies), `IfMatchModelBinder.cs`, `ResultExtensions.cs`, `UnhandledExceptionMiddleware.cs`.
- Provisión/i18n: `LocationSeedService.cs`, `BackendMessages(.es).resx`.
- Pruebas: `LocationLevelPatchApplierTests.cs` (11), `LocationDomainTests.cs`, `LocationRulesTests.cs`, `LocationPaginationGuardrailsTests.cs`, `LocationRateLimitingGovernanceTests.cs`, `AuthorizationPolicyConventionGovernanceTests.cs`, `ConcurrencyTokenMappingGuardrailsTests.cs`, `OpenApiContractGuardrailsTests.cs`, `ApiIntegrationTests.cs` (≈3577-3732).
- Ejecución: `dotnet build -c Release` → 0/0; `dotnet test --filter ~Location|~ConcurrencyTokenMappingGuardrails|~AuthorizationPolicyConventionGovernance` → **45/45 passed** (sesión 2026-06-07).
