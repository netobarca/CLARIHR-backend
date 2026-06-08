# Auditoría Técnica por Controlador — LocationHierarchyController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)

## 1. Resumen ejecutivo

`LocationHierarchyController` expone la **configuración de jerarquía de ubicaciones por empresa** (2 endpoints: `GET`/`PUT`). Su vertical de aplicación —el archivo `LocationHierarchyAdministration.cs`— está **compartido** con la administración de **niveles de ubicación**, cuya superficie HTTP vive en el controlador hermano `LocationLevelsController` (7 endpoints). Ambos comparten repositorio, dominio, errores, políticas y un **invariante cruzado** (cambiar a jerarquía single-level exige exactamente un nivel activo). Por eso la auditoría cubre la config como **alcance primario** y los niveles como **vertical directa compartida**.

Veredicto: **APROBADO CON OBSERVACIONES**. No hay hallazgos críticos ni altos. La seguridad (autorización de dos capas, aislamiento por tenant fail-closed, anti-mass-assignment, concurrencia optimista) es sólida y está blindada con *guardrails* drift-proof a nivel de toda la familia Locations. El único hallazgo material es un **vacío de pruebas (Media)** en las rutas de escritura de niveles (Create/Update/Activate/Inactivate) y en la regla single-level de la config; el resto son nits de consistencia/rendimiento (Baja/Observación).

| Indicador | Resultado |
|---|---|
| Build (Release, proyecto de pruebas + deps) | ✅ 0 warnings / 0 errors |
| Unit tests familia Location (ejecutados) | ✅ 45/45 passed |
| Integration tests (revisados, no ejecutados — requieren DB) | 11 tests presentes (5 config + 6 niveles) |
| Hallazgos | 0 Críticos · 0 Altos · 1 Media · 2 Bajas · 2 Observación |

## 2. Alcance

**Incluido (vertical directa):**
- Controlador `src/CLARIHR.Api/Controllers/LocationHierarchyController.cs` (GET/PUT config) — **primario**.
- Controlador hermano `src/CLARIHR.Api/Controllers/LocationLevelsController.cs` (7 endpoints de niveles) — incluido por compartir el mismo archivo de aplicación, repositorio, dominio y el invariante cruzado config↔niveles.
- Aplicación: `Features/Locations/Hierarchy/LocationHierarchyAdministration.cs` (queries, commands, validators, handlers, patch applier, mapper).
- Dominio: `Domain/Locations/LocationHierarchyConfig.cs`, `LocationLevel.cs`, `LocationNormalization.cs`.
- Persistencia: `ILocationHierarchyRepository` + `LocationHierarchyRepository`; EF configs `LocationHierarchyConfigConfiguration`, `LocationLevelConfiguration`; filtro global por tenant en `ApplicationDbContext`.
- Seguridad: `LocationPolicies`, `LocationPermissionCodes`, `ILocationAuthorizationService` + impl; wiring de políticas en `Program.cs`.
- Errores/i18n: `LocationCommon.cs` (`LocationErrors`), resx `BackendMessages(.es).resx`.
- Provisión: `LocationSeedService` (config + niveles por defecto al crear empresa).
- Pruebas: unit (`LocationRulesTests`, `LocationDomainTests`, `LocationLevelPatchApplierTests`, `LocationPaginationGuardrailsTests`, `LocationRateLimitingGovernanceTests`, `AuthorizationPolicyConventionGovernanceTests`, `ConcurrencyTokenMappingGuardrailsTests`, `OpenApiContractGuardrailsTests`) + integración (`ApiIntegrationTests.cs`).

**Excluido:** auditoría integral de la API; controladores no relacionados; `LocationGroupsController`/`WorkCenters*` salvo por dependencias directas (`ILocationGroupRepository.HasActiveGroupsAtLevelAsync`, consumida por los handlers de nivel); pruebas de carga.

## 3. Metodología

Revisión estática de la vertical completa contra el estándar interno (arquitectura, seguridad/OWASP API Top 10, contrato, rendimiento, concurrencia, observabilidad, pruebas), trazando cada endpoint hasta SQL y verificando los *guardrails* drift-proof de la familia. Evidencia de ejecución: build Release + 45 unit tests de Location ejecutados (verde). Integración revisada por código (requiere Postgres/Testcontainers; no ejecutada en esta sesión → declarado como limitación).

## 4. Inventario de endpoints

### Config — `LocationHierarchyController` (alcance primario)

| Método | Ruta | Propósito | Handler | Request | Response | Riesgo |
|---|---|---|---|---|---|---|
| GET | `/api/v1/companies/{companyId}/location-hierarchy` | Leer config (isMultiLevel + default group) | `GetLocationHierarchyQueryHandler` | — | `LocationHierarchyConfigResponse` | Bajo |
| PUT | `/api/v1/companies/{companyId}/location-hierarchy` | Actualizar `isMultiLevel` (If-Match) | `UpdateLocationHierarchyConfigCommandHandler` | `UpdateLocationHierarchyConfigRequest` + `If-Match` | `LocationHierarchyConfigResponse` | Medio |

### Niveles — `LocationLevelsController` (vertical compartida)

| Método | Ruta | Propósito | Handler | Request | Response | Riesgo |
|---|---|---|---|---|---|---|
| GET | `/companies/{companyId}/location-levels` | Listar niveles ordenados | `GetLocationLevelsQueryHandler` | — | `IReadOnlyCollection<LocationLevelResponse>` | Bajo |
| GET | `/location-levels/{id}` | Obtener nivel por id | `GetLocationLevelByIdQueryHandler` | — | `LocationLevelResponse` | Bajo |
| POST | `/companies/{companyId}/location-levels` | Crear nivel | `CreateLocationLevelCommandHandler` | `CreateLocationLevelRequest` | `LocationLevelResponse` (201) | Medio |
| PUT | `/location-levels/{id}` | Reconfigurar nivel (flags como unidad) | `UpdateLocationLevelCommandHandler` | `UpdateLocationLevelRequest` + `If-Match` | `LocationLevelResponse` | Medio |
| PATCH | `/location-levels/{id}` | Renombrar (JSON Patch, sólo `/displayName`) | `PatchLocationLevelCommandHandler` | `JsonPatchDocument` + `If-Match` | `LocationLevelResponse` | Bajo |
| PATCH | `/location-levels/{id}/activate` | Reactivar | `ActivateLocationLevelCommandHandler` | `If-Match` | `LocationLevelResponse` | Medio |
| PATCH | `/location-levels/{id}/inactivate` | Inactivar | `InactivateLocationLevelCommandHandler` | `If-Match` | `LocationLevelResponse` | Medio |

Sin `DELETE` (config permanente; niveles se desactivan vía `/inactivate`). Sin endpoint de creación de config (singleton por tenant auto-provisionado por `LocationSeedService`, respaldado por índice único `uq_location_hierarchy_configs__tenant_id`).

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller sin lógica de negocio | PASS | Controllers sólo despachan CQRS y mapean `ActionResult` |
| Arquitectura | Separación por capas / DTOs (sin exponer entidades) | PASS | `*Response` records; entidades nunca expuestas |
| Arquitectura | Async/await, sin sync-over-async | PASS | Toda la vertical es async; ver H-004 (nit `ContinueWith`) |
| Arquitectura | Transacciones en cambios críticos | PASS | Cada command: `BeginTransactionAsync` + commit/rollback + 2 `SaveChanges` (mutación + audit) |
| Seguridad | Autenticación | PASS | `[Authorize]` en ambos controllers |
| Seguridad | Autorización por operación (Read vs Manage) | PASS | `[AuthorizationPolicySet(Read,Manage)]` + gate `EnsureCanReadAsync/EnsureCanManageAsync` |
| Seguridad | Invariante superset declarativa ⊇ handler | PASS | `Program.cs` policies ⊇ gate; `AuthorizationPolicyConventionGovernanceTests` lo enforce |
| Seguridad | BOLA/IDOR | PASS | Filtro global por tenant fail-closed; rutas id-only resuelven tenant del JWT, no de la ruta |
| Seguridad | Tenant isolation | PASS | `HasQueryFilter(HasTenantScope && TenantId==current)`; `*ExistsOutsideTenantAsync` sólo decide 404 vs 403 |
| Seguridad | Mass assignment | PASS | Request DTOs explícitos; config sólo acepta `isMultiLevel` (code/name inmutables vía API) |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(Locations)` en el gate |
| Seguridad | Manejo seguro de errores / PII | PASS | `ProblemDetails` vía `Result`; sin PII en estas entidades; sin stack traces |
| Contrato | RESTful + verbos + versionado | PASS | `api/v{version}`; verbos correctos; `[ApiVersion("1.0")]` |
| Contrato | Status codes + error contract documentados | PASS | `[ProducesResponseType]` + `[ProducesStandardErrors]` + `[SwaggerOperation]` |
| Contrato | OpenAPI guardrail enrolado | PASS | `OpenApiContractGuardrailsTests.Families` incluye `LocationHierarchy`/`LocationLevels` |
| Contrato | ETag / If-Match en updates | PASS | `[FromIfMatch]` (400 si falta/malformado) + `ToActionResultWithETag` |
| Rendimiento | Índices de búsqueda/filtro | PASS | `uq_location_levels__tenant_order`, `ix_location_levels__tenant_active_order`, `uq_..._configs__tenant_id` |
| Rendimiento | N+1 / proyección / AsNoTracking | PASS (nit) | Lecturas `AsNoTracking`; sets pequeños; ver H-005 (re-sort redundante) |
| Rendimiento | Page size máx / search mínimo | NO APLICA | Esta vertical no tiene endpoints paginados ni search (config + lista pequeña) |
| Rendimiento | Rate limiting | NO APLICA (con justificación) | §LG3: sólo lecturas costosas (`PagedResponse`/`/tree`) se limitan; config/lista quedan excluidas por diseño |
| Concurrencia | Concurrencia optimista + If-Match + 409 | PASS | Token Guid rotado en cada mutación; check manual→409; `.IsConcurrencyToken()` como backstop |
| Concurrencia | Mapeo `DbUpdateConcurrencyException`→409 | PASS | `UnhandledExceptionMiddleware` mapea la carrera read-check-write a 409 |
| Concurrencia | Unique-constraint en create→409 | **FAIL** | Ver **H-002**: race de `levelOrder` duplicado → 500 (no 409) |
| Concurrencia | Validación de estado / transiciones | PASS | Reglas last-active-level, required-active, work-centers-last-level, has-active-groups |
| Observabilidad | Audit logs en acciones sensibles | PASS | `IAuditService.LogAsync` (+ `SaveChanges`) en los 5 commands con `AuditEventTypes`/`AuditEntityTypes` constantes |
| Observabilidad | Logs estructurados / correlación | PASS | `UnhandledExceptionMiddleware` con scope Tenant/User/TraceId |
| Pruebas | Unit (dominio/reglas/patch/guardrails) | PASS parcial | 45 verdes; patch applier sólido; ver H-001 (handlers de escritura sin cobertura) |
| Pruebas | Integración config (Get/Update/409/400/403) | PASS | 5 tests |
| Pruebas | Integración niveles (escritura) | **WARNING** | Ver **H-001**: sólo List/GetById/Patch; falta Create/Update/Activate/Inactivate |
| Build/DevSecOps | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
Vertical CQRS limpia y consistente con sus hermanas (CostCenters/LegalRepresentatives/LocationGroups). Controllers delgados; validators `FluentValidation` separados; dominio con invariantes propios (`LocationLevel.Create/Update/Inactivate` lanzan en estados inválidos como defensa en profundidad); mapper único. El patch applier de niveles está restringido deliberadamente a `/displayName` (los flags estructurales se validan como unidad en PUT, la activación va por endpoints dedicados), una decisión bien documentada en código. Cada command envuelve mutación + auditoría en una transacción con rollback. **Observación menor**: el archivo de aplicación es grande (≈945 líneas) y mezcla config + niveles; aceptable, pero candidato a *split* si crece (mismo patrón que los "god-file split" de otras familias).

### 6.2 Seguridad
**Autorización de dos capas**: política declarativa `[AuthorizationPolicySet(LocationPolicies.Read, LocationPolicies.Manage)]` (superset) + gate preciso `ILocationAuthorizationService` (tenant-match + entitlement de módulo + claims/grants RBAC). La invariante superset (declarativa ⊇ handler) está documentada y **enforced** por `AuthorizationPolicyConventionGovernanceTests`, con ambos controllers dentro de `GovernedFamilyRegex`. **Aislamiento por tenant**: el filtro global EF es *fail-closed* (`HasTenantScope` es `false` si no hay tenant → 0 filas). Las rutas id-only (`/location-levels/{id}`) **no** aceptan companyId, resolviendo el tenant del JWT → **sin IDOR**; el `*ExistsOutsideTenantAsync` (con `IgnoreQueryFilters`) sólo sirve para distinguir 404 vs 403 `TENANT_MISMATCH`. **Mass assignment**: imposible; la config sólo acepta `isMultiLevel`. **OWASP API Top 10**: BOLA/BFLA/mass-assignment cubiertos; sin PII; rate-limiting justificadamente N/A (ver checklist).

### 6.3 Contrato API
Totalmente canónico: `[ApiVersion]`, rutas RESTful, `[Tags]`, `[SwaggerOperation]` descriptivos, `[ProducesResponseType]` + `[ProducesStandardErrors]`, `ETag`/`If-Match` (400 si falta), `201 + Location` en Create (con el truco `publicId` para `PublicContractRouteConvention`). Enrolado en `OpenApiContractGuardrailsTests.Families`. Convención 409 para conflictos de negocio (consistente con la política app-wide; `CONCURRENCY_CONFLICT` reservado a concurrencia).

### 6.4 Rendimiento
Índices correctos (único `(tenant,levelOrder)` y compuesto `(tenant,isActive,levelOrder)`). Lecturas `AsNoTracking`. Cardinalidad por tenant pequeña (≈3 niveles). Sin N+1. Nits: H-004 (`ContinueWith` para cast covariante) y H-005 (re-`OrderBy` en memoria sobre datos ya ordenados en SQL).

### 6.5 Concurrencia y consistencia
Modelo robusto: token `Guid` rotado en cada mutación, `If-Match` obligatorio, check manual→409 **y** `.IsConcurrencyToken()` en EF (auto-descubierto y enforced por `ConcurrencyTokenMappingGuardrailsTests`). La carrera read-check-write degrada a **409** vía middleware. **Excepción**: el create de niveles no traduce la violación de unicidad a 409 (**H-002**).

### 6.6 Observabilidad
Auditoría persistida (`LogAsync` + `SaveChanges` dentro de la transacción) en los 5 commands, con tipos de evento/entidad por constantes (`AuditEventTypes.LocationHierarchyUpdated`, `LocationLevelCreated/Updated/Activated/Inactivated`) y before/after. Logging estructurado con scope Tenant/User/TraceId en el middleware. Adecuado para QA/producción.

### 6.7 Pruebas
**Ejecutadas**: 45 unit tests de la familia Location → 100% verdes (incluye reglas de work-center, patch applier, paginación, rate-limit governance, authz-convention governance, concurrency-token mapping). **Revisadas (no ejecutadas, requieren DB)**: 11 integration tests — config cubierta de forma robusta (Get, Update-OK, stale→409, sin-If-Match→400, tenant-mismatch→403); niveles cubren List/GetById/Patch(×4). **Vacío (H-001)**: ni unit ni integración ejercitan `Create/Update/Activate/Inactivate` de niveles ni el invariante single-level de la config — justo donde vive la lógica de negocio más rica (work-centers-only-on-last-level, last-active-level, has-active-groups, level-order-conflict) y los caminos negativos de autorización a nivel función (usuario Read → 403 en Manage).

### 6.8 Build / DevSecOps
`dotnet build -c Release` del proyecto de pruebas + dependencias: **0 warnings / 0 errors**. Sin secretos hardcodeados en la vertical.

## 7. Hallazgos

### H-001 — Handlers de escritura de niveles y regla single-level sin cobertura de pruebas
**Severidad:** Media · **Categoría:** Pruebas · **Ubicación:** `LocationHierarchyAdministration.cs` (`CreateLocationLevelCommandHandler`, `UpdateLocationLevelCommandHandler`, `ActivateLocationLevelCommandHandler`, `InactivateLocationLevelCommandHandler`, regla `LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL` en `UpdateLocationHierarchyConfigCommandHandler`).
**Condición:** `grep` confirma 0 tests (unit o integración) que despachen esos commands. Integración de niveles sólo cubre List/GetById/Patch.
**Criterio esperado:** las reglas de negocio críticas (work-centers-only-on-last-level, last-active-level-required, level-order-conflict, location-level-has-active-groups, allows-work-centers-in-use, single-level=1-activo) y los caminos negativos de autz de función deben tener pruebas.
**Impacto:** una regresión en la lógica de jerarquía/niveles no sería detectada por CI. No es un defecto de corrección (la lógica revisada es correcta), sino riesgo de mantenibilidad/regresión.
**Evidencia:** §6.7; búsqueda de comandos en `tests/` → vacío.
**Recomendación:** agregar tests de handler (con repos fake, ya existe el patrón `TestLocationHierarchyRepository` en `LocationRulesTests`) para cada regla, e integración para Create/Update/Activate/Inactivate + single-level + un negativo Read→403 en Manage.
**Prioridad:** Alta · **Esfuerzo:** Medio · **Estado:** Abierto

### H-002 — Race de `levelOrder` duplicado en Create devuelve 500 en vez de 409
**Severidad:** Baja · **Categoría:** Concurrencia/Contrato · **Ubicación:** `CreateLocationLevelCommandHandler` (`LocationLevelsController` POST) — *vertical compartida, hermano del controlador primario*.
**Condición:** el handler valida `LevelOrderExistsAsync` y luego inserta; dos creates concurrentes con el mismo `levelOrder` pasan el pre-check y el segundo viola `uq_location_levels__tenant_order`. `UnitOfWork` traduce a `UniqueConstraintViolationException`, pero el handler hace `catch { rollback; throw; }` y el middleware sólo mapea `DbUpdateConcurrencyException`→409 → cae a **500**.
**Criterio esperado:** conflicto de unicidad bajo carrera → `409 LOCATION_LEVEL_ORDER_CONFLICT` (mismo patrón ya resuelto en CostCenters R2).
**Impacto:** 500 espurio en una ventana de carrera estrecha; **sin corrupción** (el índice único preserva la integridad). Endpoint admin, concurrencia muy baja.
**Evidencia:** `LocationLevelConfiguration.cs:55-57`; `UnitOfWork.cs:18-20`; `UnhandledExceptionMiddleware.cs:44,67`.
**Recomendación:** envolver el `SaveChanges` del create en `catch (UniqueConstraintViolationException) → LocationErrors.LevelOrderConflict`, idealmente comparando `ConstraintName` con la constante del índice (como CostCenters R2). Sistémico: aplica a varios create handlers; podría resolverse a nivel plataforma.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### H-003 — Error inline en vez del catálogo `LocationErrors`
**Severidad:** Baja · **Categoría:** Mantenibilidad · **Ubicación:** `UpdateLocationHierarchyConfigCommandHandler` líneas 229-232.
**Condición:** `LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL` se construye con `new Error(...)` inline, a diferencia de todos los demás errores de la familia que viven en `LocationErrors`.
**Criterio esperado:** un único punto de definición por error (catálogo).
**Impacto:** nulo funcionalmente — **está localizado** (resx en+es presentes, `BackendMessages.resx:352`). Sólo inconsistencia/duplicación menor.
**Evidencia:** `LocationHierarchyAdministration.cs:229`; `BackendMessages(.es).resx:352-353`.
**Recomendación:** mover a `LocationErrors.SingleLevelRequiresOneActiveLevel`.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### H-004 — `GetLevelsAsync` usa `.ContinueWith(...)` para cast covariante
**Severidad:** Observación · **Categoría:** Rendimiento/Mantenibilidad · **Ubicación:** `LocationHierarchyRepository.cs:26-32`.
**Condición:** convierte `Task<List<T>>`→`Task<IReadOnlyList<T>>` con `ContinueWith` (scheduler por defecto) en vez de `async/await`.
**Impacto:** mínimo; anti-patrón menor (más difícil de razonar; `TaskScheduler.Current` implícito).
**Recomendación:** método `async` con `await ... ToListAsync()` y retorno directo (covarianza implícita).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### H-005 — Re-ordenamiento redundante en memoria
**Severidad:** Observación · **Categoría:** Rendimiento · **Ubicación:** `GetLocationLevelsQueryHandler` línea 285.
**Condición:** re-`OrderBy(LevelOrder)` aunque `GetLevelsAsync` ya ordena por `LevelOrder` en SQL.
**Impacto:** despreciable (listas ≈3 ítems). Limpieza.
**Recomendación:** eliminar el `OrderBy` en memoria o documentar la intención.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

## 8. Hallazgos fuera de alcance / trazabilidad

- **Rate-limiting N/A (no es hallazgo):** la config y la lista de niveles quedan **intencionadamente** fuera del rate-limit (§LG3: sólo `PagedResponse`/`/tree`). Documentado en `LocationRateLimitingGovernanceTests`. No re-flaggear.
- **H-002 pertenece a `LocationLevelsController`** (hermano), incluido por compartir vertical. Si la familia adopta una solución de plataforma para unique-constraint→409, cubrir también este create.
- **Overflow de paginación no aplica:** el riesgo sistémico de `pageSize` sin tope → int overflow (anotado en auditorías hermanas) **no aplica** aquí: esta vertical no tiene endpoints paginados.
- **Tamaño del archivo de aplicación** (≈945 líneas, config+niveles): candidato a *split* si crece; hoy aceptable.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Impacto | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|---|
| H-001 | Media | Pruebas | Handlers de escritura de niveles + single-level sin tests | Regresión no detectada | Medio | Alta | Tests de handler + integración + negativo authz |
| H-002 | Baja | Concurrencia | Race de levelOrder → 500 (no 409) | 500 espurio, sin corrupción | Bajo | Baja | Catch `UniqueConstraintViolationException`→409 |
| H-003 | Baja | Mantenibilidad | Error inline vs catálogo | Consistencia | Bajo | Baja | Mover a `LocationErrors` |
| H-004 | Observación | Rendimiento | `ContinueWith` cast | Estilo | Bajo | Baja | `async/await` |
| H-005 | Observación | Rendimiento | Re-sort redundante | Limpieza | Bajo | Baja | Quitar `OrderBy` |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`LocationHierarchyController`) | **Aprobado con observaciones** |
| Endpoints internos del controller | Cubiertos (config: 2/2 con tests de integración) |
| Vertical compartida (`LocationLevelsController`) | Aprobado con observaciones (gap de pruebas en escritura) |
| Seguridad | Aprobado |
| Arquitectura | Aprobado |
| Performance | Aprobado |
| Concurrencia | Aprobado con observaciones (H-002) |
| Pruebas | Observaciones (H-001) |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**El controlador puede avanzar a QA** con seguimiento de H-001 (recomendado cerrarlo antes de productivo) y H-002 como hardening.

## 11. Recomendaciones finales

1. **Cerrar H-001** antes de productivo: tests de handler para las reglas de niveles + integración Create/Update/Activate/Inactivate + single-level + negativo Read→403. (Mayor ROI.)
2. **H-002**: traducir unique-constraint→409 en el create de niveles (mirror de CostCenters R2), preferible vía solución de plataforma reutilizable.
3. **H-003/H-004/H-005**: limpieza oportunista (un solo PR).
4. Mantener los *guardrails* drift-proof existentes (authz convention, rate-limit governance, concurrency-token mapping, OpenAPI families) — son la red de seguridad que sostiene el bajo riesgo de esta familia.

## 12. Anexos / Evidencia revisada

- Controllers: `LocationHierarchyController.cs`, `LocationLevelsController.cs`.
- Aplicación: `LocationHierarchyAdministration.cs`, `LocationCommon.cs`, `LocationPolicies.cs`.
- Dominio: `LocationHierarchyConfig.cs`, `LocationLevel.cs`, `LocationNormalization.cs`, `TenantEntity.cs`.
- Persistencia: `ILocationHierarchyRepository.cs`, `LocationHierarchyRepository.cs`, `LocationHierarchyConfigConfiguration.cs`, `LocationLevelConfiguration.cs`, `ApplicationDbContext.cs` (filtro tenant), `UnitOfWork.cs`.
- Seguridad: `ILocationAuthorizationService.cs`, `LocationAuthorizationService.cs`, `Program.cs` (policies), `UnhandledExceptionMiddleware.cs`, `IfMatchModelBinder.cs`.
- Provisión/i18n: `LocationSeedService.cs`, `BackendMessages(.es).resx`.
- Pruebas: `LocationRulesTests.cs`, `LocationDomainTests.cs`, `LocationLevelPatchApplierTests.cs`, `LocationPaginationGuardrailsTests.cs`, `LocationRateLimitingGovernanceTests.cs`, `AuthorizationPolicyConventionGovernanceTests.cs`, `ConcurrencyTokenMappingGuardrailsTests.cs`, `OpenApiContractGuardrailsTests.cs`, `ApiIntegrationTests.cs` (líneas ≈3494-3732).
- Ejecución: `dotnet build -c Release` → 0/0; `dotnet test --filter ~Location|~ConcurrencyTokenMappingGuardrails|~AuthorizationPolicyConventionGovernance` → **45/45 passed**.

## 13. Estado de remediación (2026-06-07, uncommitted)

**Los 5 hallazgos cerrados.** Verificación global: build **0/0** · unit **1687/0** · integración Locations (Levels+Hierarchy) **16/16**. (Nota: el PATCH de niveles que figuraba en el inventario §4 fue **eliminado** por separado en esta sesión —era un PATCH degenerado de un solo campo `/displayName`—; ya no forma parte de la superficie.)

| ID | Estado | Remediación |
|---|---|---|
| **H-001** | ✅ Cerrado | 9 tests de integración nuevos para los handlers de escritura de niveles + la regla single-level + el negativo de autz de función: `LocationLevels_Create_ShouldReturnCreatedLevel`, `_Create_WithDuplicateLevelOrder_ShouldReturn409`, `_Create_WhenAllowsWorkCentersButAnotherLevelAlreadyDoes_ShouldReturn409`, `_Create_WhenReadOnlyUser_ShouldReturn403`, `_Update_WithValidIfMatch_ShouldReconfigureAndRotateToken`, `_ActivateAndInactivate_ShouldToggleActiveState`, `_Inactivate_WhenLevelIsRequired_ShouldReturn409`, `_Inactivate_WhenLevelHasActiveGroups_ShouldReturn409`, `LocationHierarchy_UpdateToSingleLevel_WithMultipleActiveLevels_ShouldReturn409`. Cubren las reglas work-centers-only-on-last-level, level-order-conflict, required-active, has-active-groups, single-level=1-activo, y el camino Read→403 en Manage. |
| **H-002** | ✅ Cerrado | `CreateLocationLevelCommandHandler` ahora atrapa `UniqueConstraintViolationException` (vía `when LocationConstraintViolations.IsLevelOrderConflict(ex.ConstraintName)`) y devuelve `409 LOCATION_LEVEL_ORDER_CONFLICT`, mirror de CostCenters R2. Nombre del índice **single-sourced** en `LocationValidationRules.LevelOrderUniqueConstraintName`, usado tanto por `LocationLevelConfiguration.HasDatabaseName(...)` como por el helper de comparación. |
| **H-003** | ✅ Cerrado | El error inline `LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL` se movió a `LocationErrors.SingleLevelRequiresOneActiveLevel` (mensaje sin cambios → resx en/es existentes intactas). |
| **H-004** | ✅ Cerrado | `LocationHierarchyRepository.GetLevelsAsync` reescrito a `async`/`await` (`ToListAsync`, covarianza implícita `List<T>`→`IReadOnlyList<T>`); eliminado el `.ContinueWith(...)`. |
| **H-005** | ✅ Cerrado | Eliminado el `.OrderBy(level => level.LevelOrder)` redundante en `GetLocationLevelsQueryHandler` (la query SQL ya ordena por `LevelOrder`); comentario que documenta el orden garantizado por SQL. |

**Pendiente:** commit (lo maneja el usuario). Sin migración (los cambios son de aplicación/infra-config + tests; el nombre del índice no cambia, sólo se single-sourcea).
