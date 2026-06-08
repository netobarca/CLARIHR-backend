# Auditoría Técnica por Controlador — WorkCenterTypesController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Familia: **Locations** (alineada feature-wide §LG1…§LG9; ver [[location-groups-audit]]). Hermanos auditados: LocationHierarchy (doc 13), LocationLevels (doc 14).

## 1. Resumen ejecutivo

`WorkCenterTypesController` administra el **catálogo de tipos de centro de trabajo** por empresa (p.ej. agencia, bodega, oficina): código, nombre y flags `requiresAddress`/`requiresGeo`/`allowsBiometric` que controlan la validación aplicada a los work centers de ese tipo. 7 endpoints (List, GetById, Create, Update, Patch, Activate, Inactivate).

Veredicto: **APROBADO CON OBSERVACIONES** — y es uno de los **controladores más maduros y canónicos de la ola**. 0 críticos / 0 altos / 0 medios. Totalmente alineado con la familia Locations: autorización de dos capas (`[AuthorizationPolicySet]` + `ILocationAuthorizationService`), **If-Match obligatorio** en las 4 mutaciones, `[EnableRateLimiting(Search)]` (§LG3), `[Range]` en pageSize (§LG6), `MinSearchLength=2` (§LG5), **`includeAllowedActions` sin N+1** (§12.7/ADR-0001, sólo `canManage`), integridad referencial al inactivar (409 si hay work centers activos), auditoría con **constantes** (§LG8), y **enrolado en TODOS los guardrails drift-proof de la familia**. El único hallazgo es el race de código duplicado → 500 (Baja, sistémico — la familia Locations no recibió el fix de CostCenters-R2).

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila |
| Unit tests (WorkCenterType + guardrails, **ejecutados**) | ✅ 67/67 passed |
| Integration tests (revisados, no ejecutados) | 6 |
| Enrolamiento en guardrails de familia | **5/5** (OpenAPI ✓ · authz ✓ · rate-limit ✓ · pagination ✓ · concurrency ✓) |
| Hallazgos | 0 Crít · 0 Alto · 0 Media · **1 Baja · 1 Observación** |

## 2. Alcance

**Incluido:** controlador `WorkCenterTypesController.cs`; aplicación `WorkCenterTypeAdministration.cs` (DTOs, 7 commands/queries, validators, 7 handlers, `WorkCenterTypePolicyAdapter`, `WorkCenterTypePatchApplier`, mapper); dominio `WorkCenterType.cs` + `LocationNormalization`; persistencia `IWorkCenterTypeRepository` + `WorkCenterTypeRepository`, EF `WorkCenterTypeConfiguration`; seguridad `LocationPolicies`/`LocationPermissionCodes`/`ILocationAuthorizationService` (compartidos de la familia); dependencia `ILocationDependencyPolicy` (check de inactivación); errores/i18n `LocationCommon.cs`; pruebas (`WorkCenterTypePatchApplierTests` + integración).

**Excluido:** `WorkCenters` (consumidor de los tipos; vertical hermana, auditar aparte); resto de la familia Locations salvo dependencia directa; auditoría integral; carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en seguridad (dos capas + tenant), integridad referencial (inactivación), concurrencia (If-Match), sargabilidad y adherencia canónica. Evidencia: 67 unit tests ejecutados (verde, incl. los guardrails de familia rate-limit/pagination/concurrency/OpenAPI). Integración revisada por código (requiere DB; no ejecutada → limitación). **Nota:** edición en curso no relacionada en Locations rompió el build transitoriamente; estable al ejecutar.

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Concurrencia | Rate-limit |
|---|---|---|---|---|---|
| 1 | GET | `/companies/{companyId}/work-center-types` | Listar (paginado, `isActive`+`q`≥2, allowedActions) | — | Search |
| 2 | GET | `/work-center-types/{id}` | Get by id (tenant del JWT) | — | — |
| 3 | POST | `/companies/{companyId}/work-center-types` | Crear (201+Location+ETag) | — | — |
| 4 | PUT | `/work-center-types/{id}` | Actualizar (code/name/flags) | **If-Match** | — |
| 5 | PATCH | `/work-center-types/{id}` | Patch (code/name/flags; code uniqueness-checked) | **If-Match** | — |
| 6 | PATCH | `/work-center-types/{id}/activate` | Reactivar | **If-Match** | — |
| 7 | PATCH | `/work-center-types/{id}/inactivate` | Inactivar (409 si en uso) | **If-Match** | — |

Sin `DELETE` (soft-delete vía `/inactivate`). El PATCH es de **superficie completa** (code/name/flags) salvo activación (endpoints dedicados); a diferencia de los patches descriptivos de LocationLevels/OrgUnits, **incluye `/code`** y por eso re-ejecuta el chequeo de unicidad.

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / DTOs / capas | PASS | Sólo despacha CQRS; records cerrados |
| Arquitectura | Transacciones en escrituras | PASS | Los 5 commands: transacción + 2 SaveChanges (mutación + audit) + rollback |
| Seguridad | Autenticación / Autorización dos capas | PASS | `[AuthorizationPolicySet(Read,Manage)]` + `ILocationAuthorizationService.EnsureCanRead/ManageAsync` en los 7 handlers |
| Seguridad | Superset declarativa ⊇ handler | PASS | Enrolado en `AuthorizationPolicyConventionGovernanceTests.GovernedFamilyRegex` (familia WorkCenter) |
| Seguridad | BOLA/IDOR + Tenant isolation | PASS | id-only routes (tenant del JWT); filtro global fail-closed; `ExistsOutsideTenantAsync` → 404 vs 403 |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(Locations)` en el gate |
| Seguridad | Integridad referencial (inactivar) | PASS | `ILocationDependencyPolicy.CanInactivateWorkCenterTypeAsync` → 409 `WORK_CENTER_TYPE_IN_USE` |
| Seguridad | Mass assignment | PASS | DTOs cerrados; patch acotado a code/name/flags (sin isActive) |
| Seguridad | DoS JSON Patch | PASS | `[RequestSizeLimit(64KB)]` + tope 50 ops |
| Contrato | Versionado / Tags / Swagger / errores | PASS | `[ApiVersion]`+`[Route(v{version})]`+`[Tags("Work Center Types")]`+`[SwaggerOperation]`×7+`[ProducesStandardErrors]`×7 |
| Contrato | OpenAPI guardrail | PASS | `OpenApiContractGuardrailsTests.Families` → `^WorkCenterTypes`→"Work Center Types" |
| Contrato | 201 + Location + ETag en Create | PASS | `ToCreatedAtActionResult(..., publicId, ConcurrencyToken)` |
| Contrato | If-Match / ETag en updates | PASS | `[FromIfMatch]`+`ToActionResultWithETag` en las 4 mutaciones (400 si falta, 409 stale) |
| Contrato | Paginación: page size | PASS | `[Range(1, MaxPageSize)]` en el param + validator (§LG6) |
| Contrato | Search: longitud mínima | PASS | `MinSearchLength=2` (`IsValidSearchLength`, §LG5) |
| Rendimiento | Índices | PASS | unique `(tenant,normalized_code)`; `(tenant,isActive)`; `(tenant,normalized_name)` |
| Rendimiento | N+1 en allowedActions | PASS | §12.7/ADR-0001: deriva sólo de `canManage`, sin estado por-ítem (comentado) |
| Rendimiento | Search sargable | PASS (mitigado) | `Contains` no-sargable PERO con `MinSearchLength=2`; cardinalidad de catálogo acotada |
| Rendimiento | Rate limit | PASS | `[EnableRateLimiting(LocationRateLimitPolicies.Search)]` (§LG3) |
| Concurrencia | Optimista + If-Match + 409 | PASS | Token rotado; check manual→409; `.IsConcurrencyToken()` (auto-guardrail); race read-check-write→409 vía middleware |
| Concurrencia | Unique-constraint en Create/Update/Patch → 409 | **FAIL** | **WCT-A**: race dup-code → 500 |
| Observabilidad | Audit logs + constantes | PASS | Los 5 commands con `AuditEventTypes.WorkCenterType*` + `AuditEntityTypes.WorkCenterType` (§LG8, 0 literales) |
| Pruebas | Unit | PASS (parcial) | `WorkCenterTypePatchApplierTests`; **WCT-B**: handlers cubiertos por integración |
| Pruebas | Integración | PASS | 6 métodos |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS limpia, idéntica a los hermanos alineados de Locations. Dominio `WorkCenterType` simple (catálogo con flags booleanos), normalización code/name compartida. `WorkCenterTypePatchApplier` aplica el patch a un `state` y `Validate` antes de mutar; el handler re-ejecuta el chequeo de unicidad de código (porque `/code` es patchable). Cada escritura es transaccional con auditoría.

### 6.2 Seguridad
**Autorización de dos capas** (declarativa `[AuthorizationPolicySet(LocationPolicies.Read, .Manage)]` + gate `ILocationAuthorizationService` con tenant-match + entitlement de módulo + RBAC), enrolada en el governance test. **Sin IDOR**: rutas id-only (tenant del JWT), filtro global EF fail-closed, `ExistsOutsideTenantAsync` para 404-vs-403. **Anti-mass-assignment**: DTOs cerrados; el patch no toca `isActive` (activación por endpoints dedicados). **Integridad referencial**: no se puede inactivar un tipo en uso por work centers activos (`ILocationDependencyPolicy` → 409). JSON-Patch hardened.

### 6.3 Contrato API
Totalmente canónico (versionado, tags, swagger, produces-standard-errors, 201/Location/ETag, If-Match obligatorio) y **enrolado en el OpenAPI guardrail**. El PATCH cubre la superficie completa (code/name/flags) con re-chequeo de unicidad — más amplio que los patches descriptivos de la familia, correctamente manejado.

### 6.4 Rendimiento
Índices correctos (`(tenant,normalized_code)` único + `(tenant,isActive)` + `(tenant,normalized_name)`). **`includeAllowedActions` sin N+1** (deriva sólo del permiso del caller, no del estado por-ítem — patrón §12.7/ADR-0001 explícitamente comentado, contraste positivo con OrgStructureCatalogs OSC-002). Search libre `Contains` no-sargable pero con `MinSearchLength=2`. Rate-limit en List (§LG3). Cardinalidad de catálogo acotada.

### 6.5 Concurrencia y consistencia
If-Match obligatorio en las 4 mutaciones; token rotado en cada cambio; `.IsConcurrencyToken()` (auto-cubierto por `ConcurrencyTokenMappingGuardrailsTests`, verificado verde); check manual→409; race read-check-write→409 vía middleware. **WCT-A**: Create/Update/Patch verifican `CodeExistsAsync` y luego mutan; un race con el mismo código viola `uq_work_center_types__tenant_code` → `UniqueConstraintViolationException` no atrapada → 500.

### 6.6 Observabilidad
Auditoría persistida (en transacción) en los 5 commands con **constantes** `AuditEventTypes.WorkCenterTypeCreated/Updated/Activated/Inactivated` y `AuditEntityTypes.WorkCenterType` (§LG8 — 0 literales crudos). Logging estructurado vía middleware.

### 6.7 Pruebas
**Ejecutadas: 67 unit tests verdes** — `WorkCenterTypePatchApplierTests` (allow-list code/name/flags, rechazo de isActive/token/read-only) + los guardrails de familia (`LocationRateLimitingGovernanceTests`, `LocationPaginationGuardrailsTests`, `ConcurrencyTokenMappingGuardrails`, `OpenApiContractGuardrails`), todos cubriendo WorkCenterTypes. **Revisadas: 6 integration tests**. **WCT-B**: la cobertura **unit** de los handlers (in-use→409, code-conflict, activate/inactivate) descansa en integración; sólo el patch applier tiene unit dedicado.

### 6.8 Build / DevSecOps
Compila; sin secretos; localización completa (errores `LocationErrors` con resx).

## 7. Hallazgos

### WCT-A — Race de código duplicado en Create/Update/Patch → 500 (no 409)
**Severidad:** Baja · **Categoría:** Concurrencia/Contrato · **Ubicación:** `CreateWorkCenterTypeCommandHandler` (246), `UpdateWorkCenterTypeCommandHandler` (326), `PatchWorkCenterTypeCommandHandler` (607).
**Condición:** los tres verifican `CodeExistsAsync` y luego mutan; dos operaciones concurrentes con el mismo código pasan el pre-check y la segunda viola `uq_work_center_types__tenant_code`; `UniqueConstraintViolationException` no atrapada → 500.
**Criterio esperado:** `409 WORK_CENTER_TYPE_CODE_CONFLICT` (el patrón ya resuelto en CostCenters R2, OrgUnits OU-004, OrgStructureCatalogs OSC-005).
**Impacto:** 500 espurio en ventana de carrera estrecha; **sin corrupción** (el índice único preserva integridad). Admin-only, baja concurrencia.
**Evidencia:** `WorkCenterTypeConfiguration.cs:67-69`; sin `catch (UniqueConstraintViolationException)` en la vertical.
**Recomendación:** capturar `UniqueConstraintViolationException`→`LocationErrors.WorkCenterTypeCodeConflict` en Create/Update/Patch; single-source del nombre de índice. **Sistémico en toda la familia Locations** (WorkCenterTypes + WorkCenters + LocationGroups + LocationLevels NO recibieron el fix de CostCenters-R2) → idealmente una solución de plataforma compartida.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### WCT-B — Cobertura unit ligera de handlers
**Severidad:** Observación · **Categoría:** Pruebas · **Ubicación:** `tests/CLARIHR.Application.UnitTests` (sólo `WorkCenterTypePatchApplierTests`).
**Condición:** los handlers (Create/Update/Activate/Inactivate + in-use check + code-conflict) están cubiertos por 6 integration tests, no por unit dedicado.
**Impacto:** bajo — las rutas se cubren end-to-end; los guardrails de familia protegen el contrato.
**Recomendación:** opcional — unit tests de handler para in-use→409 y code-conflict (velocidad de regresión).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

## 8. Hallazgos fuera de alcance / trazabilidad

- **Familia Locations completamente alineada (confirmado):** WorkCenterTypes está enrolado en los 5 guardrails (OpenAPI/authz/rate-limit/pagination/concurrency), usa MinSearchLength, evita el N+1 y usa constantes de auditoría — el trabajo §LG1…§LG9 se refleja íntegro.
- **WCT-A es sistémico de la familia Locations** (a diferencia de CostCenters/OrgUnits/OrgStructureCatalogs que sí lo cerraron). Candidato a remediación de plataforma transversal junto con el resto de la ola.
- **`WorkCenters`** (consumidor de los tipos vía `requiresAddress`/`requiresGeo`/`allowsBiometric`) es la vertical hermana; su auditoría es independiente.
- **Colisión de prefijo de guardrail evitada:** `^WorkCenters` y `^WorkCenterTypes` no colisionan ("WorkCenterTypes" no empieza por "WorkCenters") — no requiere carve-out.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|
| WCT-A | Baja | Concurrencia | Race dup-code → 500 (sistémico familia) | Bajo | Media | Catch `UniqueConstraintViolationException`→409 (plataforma) |
| WCT-B | Obs | Pruebas | Unit ligero de handlers | Bajo | Baja | Unit de in-use/code-conflict (opcional) |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`WorkCenterTypesController`) | **Aprobado con observaciones** (uno de los más limpios de la ola) |
| Seguridad | Aprobado |
| Arquitectura | Aprobado |
| Contrato | Aprobado (totalmente canónico + guardrails 5/5) |
| Performance | Aprobado (sin N+1, MinSearchLength, rate-limit) |
| Concurrencia | Aprobado con observaciones (WCT-A) |
| Pruebas | Aprobado (WCT-B = unit ligero) |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**Controlador maduro, seguro y totalmente canónico; puede avanzar a QA.** El único hallazgo (WCT-A) es el race de código duplicado, sistémico de la familia Locations y de impacto bajo.

## 11. Recomendaciones finales

1. **WCT-A:** capturar el unique-violation→409 (preferible como **solución de plataforma** para toda la familia Locations, que comparte el patrón sin fix).
2. **WCT-B:** unit tests de handler opcionales (in-use/code-conflict).
3. Mantener las fortalezas (modelo de catálogo canónico): dos capas de autz, If-Match obligatorio, rate-limit/pagination/MinSearchLength, allowedActions sin N+1, integridad referencial, constantes de auditoría, enrolamiento 5/5 en guardrails.

## 12. Anexos / Evidencia revisada

- Controller: `WorkCenterTypesController.cs` (7 endpoints + DTOs).
- Aplicación: `WorkCenterTypeAdministration.cs` (DTOs/validators/7 handlers + `WorkCenterTypePolicyAdapter` + `WorkCenterTypePatchApplier` + mapper), `LocationCommon.cs`, `LocationPolicies.cs`.
- Dominio: `WorkCenterType.cs`, `LocationNormalization.cs`, `TenantEntity.cs`.
- Persistencia: `IWorkCenterTypeRepository.cs`, `WorkCenterTypeRepository.cs`, `WorkCenterTypeConfiguration.cs`.
- Seguridad: `LocationAuthorizationService.cs`, `ILocationDependencyPolicy` (check de inactivación), `Program.cs` (policies + rate-limit).
- Pruebas: `WorkCenterTypePatchApplierTests.cs`, `ApiIntegrationTests.cs` (6 `WorkCenterType*` métodos); guardrails de familia.
- Ejecución: `dotnet test --filter ~WorkCenterType|~LocationRateLimiting|~LocationPagination|~ConcurrencyTokenMappingGuardrails|~OpenApiContractGuardrails` → **67/67 passed** (sesión 2026-06-07).

## 13. Estado de remediación (2026-06-08, uncommitted)

**WCT-A y WCT-B cerrados.** Verificación: build **0/0** · suite unitaria completa **1732/1732** · **sin migración · sin resx · sin cambio de contrato** → `openapi.yaml` intacto (el `409` ya estaba declarado por el pre-check `CodeExistsAsync`; WCT-A solo enruta el race al mismo `409`).

| ID | Estado | Remediación |
|---|---|---|
| **WCT-A** | ✅ Cerrado | `Create`/`Update`/`Patch` capturan `UniqueConstraintViolationException` (catch tipado antes del genérico → rollback + `409 WORK_CENTER_TYPE_CODE_CONFLICT`), con el nombre de índice **single-sourced** en `LocationValidationRules.WorkCenterTypeCodeUniqueConstraintName` (== EF `HasDatabaseName`) + helper `LocationConstraintViolations.IsWorkCenterTypeCodeConflict` (mismo patrón que `LevelOrder` / CostCenters R2). `LocationErrors.WorkCenterTypeCodeConflict` ya existía → **sin resx**. |
| **WCT-B** | ✅ Cerrado | Nuevo `WorkCenterTypeAdministrationTests` (handler-level): Create dup-code pre-check→409, **Create race unique-violation→409 (valida WCT-A vía `ThrowingUnitOfWork`)**, Inactivate in-use→409 (vía `ILocationDependencyPolicy`). El patch-applier ya tenía unit dedicado. |

**Sistémico (anotado, fuera de scope del doc 21):** el resto de la familia Locations (`WorkCenters` / `LocationGroups` / `LocationLevels`) comparte el patrón Create/Update sin el catch de unique-violation → mismo race dup-code→500 latente; candidato a cierre per-controller en sus docs o a solución de plataforma transversal. **Solo se remedió WorkCenterTypes (el controlador del doc 21).**

**Pendiente:** commit (lo maneja el usuario).
