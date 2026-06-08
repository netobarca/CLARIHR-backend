# Auditoría Técnica por Controlador — WorkCentersController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Familia: **Locations** (alineada §LG1…§LG9). Cierra la familia: hermanos auditados = LocationHierarchy (13), LocationLevels (14), WorkCenterTypes (21).

## 1. Resumen ejecutivo

`WorkCentersController` administra los **centros de trabajo** (sitios físicos) por empresa: cada uno referencia un **tipo** (`WorkCenterType`, que dicta si requiere dirección/geo) y un **grupo de ubicación** (`LocationGroup`, cuyo nivel debe permitir work centers), con dirección, **coordenadas geo**, contacto (teléfono/email) y notas. 8 endpoints (List, GetById, Create, Update, Patch, ReassignGroup, Activate, Inactivate).

Veredicto: **APROBADO CON OBSERVACIONES** — y, como su hermano WorkCenterTypes, uno de los **controladores más maduros y canónicos de la ola**. 0 críticos / 0 altos / 0 medios. Totalmente alineado con la familia Locations: autorización de dos capas, **If-Match obligatorio** en las 5 mutaciones, `[EnableRateLimiting(Search)]` (§LG3), `[Range]` pageSize (§LG6), `MinSearchLength=2` (§LG5), **`includeAllowedActions` sin N+1** (§12.7/ADR-0001), validación de asignación robusta (tipo activo + grupo activo + nivel-permite-work-centers + dirección/geo requeridos por tipo + **rangos de coordenadas WGS84**), integridad referencial al inactivar (409), auditoría con constantes (§LG8), y **enrolado en los 5 guardrails de familia**. El único hallazgo de peso es el race de código duplicado → 500 (Baja, sistémico de la familia Locations).

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila |
| Unit tests (WorkCenter + reglas + guardrails, **ejecutados**) | ✅ 85/85 passed |
| Integration tests (revisados, no ejecutados) | 8 |
| Enrolamiento en guardrails de familia | **5/5** (OpenAPI ✓ · authz ✓ · rate-limit ✓ · pagination ✓ · concurrency ✓) |
| Hallazgos | 0 Crít · 0 Alto · 0 Media · **1 Baja · 2 Observación** |

## 2. Alcance

**Incluido:** controlador `WorkCentersController.cs`; aplicación `WorkCenterAdministration.cs` (DTOs, 8 commands/queries, validators, 8 handlers, `WorkCenterRules` —resolución de tipo/grupo + validación de asignación—, `WorkCenterPolicyAdapter`, `WorkCenterPatchApplier`, mapper); dominio `WorkCenter.cs` + `LocationNormalization`; persistencia `IWorkCenterRepository` + `WorkCenterRepository`, EF `WorkCenterConfiguration`; seguridad `LocationPolicies`/`ILocationAuthorizationService` (familia); dependencias directas `IWorkCenterTypeRepository`, `ILocationGroupRepository`, `ILocationHierarchyRepository` (niveles), `ILocationDependencyPolicy` (inactivación); errores/i18n `LocationCommon.cs` + resx; pruebas (`WorkCenterPatchApplierTests` + `LocationRulesTests` + integración).

**Excluido:** `WorkCenterTypes`/`LocationGroups`/`LocationLevels` salvo como referencias resueltas (auditados aparte); auditoría integral; carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en las **reglas de asignación** (tipo↔grupo↔geo), seguridad (dos capas + tenant), integridad referencial, concurrencia (If-Match) y adherencia canónica. Evidencia: 85 unit tests ejecutados (verde, incl. `WorkCenterRules` y los guardrails de familia). Integración revisada por código (requiere DB; no ejecutada → limitación). **Nota:** edición en curso no relacionada en Locations rompió el build transitoriamente; estable al ejecutar.

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Concurrencia | Rate-limit |
|---|---|---|---|---|---|
| 1 | GET | `/companies/{companyId}/work-centers` | Listar (paginado, `groupId`/`typeId`/`isActive`+`q`≥2, allowedActions) | — | Search |
| 2 | GET | `/work-centers/{id}` | Get by id (tenant del JWT) | — | — |
| 3 | POST | `/companies/{companyId}/work-centers` | Crear (201+Location+ETag; valida asignación) | — | — |
| 4 | PUT | `/work-centers/{id}` | Actualizar (code/name/type/group/address/geo/contacto) | **If-Match** | — |
| 5 | PATCH | `/work-centers/{id}` | Patch (code/name/address/geo/phone/email/notes; re-valida geo según tipo) | **If-Match** | — |
| 6 | PATCH | `/work-centers/{id}/reassign-group` | Mover a otro grupo (nivel debe permitir WC → 409) | **If-Match** | — |
| 7 | PATCH | `/work-centers/{id}/activate` | Reactivar | **If-Match** | — |
| 8 | PATCH | `/work-centers/{id}/inactivate` | Inactivar (409 si tiene dependencias activas) | **If-Match** | — |

Sin `DELETE`. El PATCH excluye type/group (vía PUT o `/reassign-group`, que requieren re-validación de asignación) — separación correcta.

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / DTOs | PASS | Sólo despacha CQRS; `WorkCenterRules` factoriza resolución + validación |
| Arquitectura | Transacciones en escrituras | PASS | Los 6 commands: transacción + 2 SaveChanges + audit + rollback |
| Seguridad | Autenticación / Autz dos capas | PASS | `[AuthorizationPolicySet(Read,Manage)]` + `ILocationAuthorizationService` en los 8 handlers |
| Seguridad | Superset declarativa ⊇ handler | PASS | Enrolado en `AuthorizationPolicyConventionGovernanceTests.GovernedFamilyRegex` |
| Seguridad | BOLA/IDOR + Tenant isolation | PASS | id-only routes (tenant del JWT); filtro global fail-closed; `ExistsOutsideTenantAsync` → 404 vs 403 (en WC, tipo y grupo) |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(Locations)` |
| Seguridad | Integridad referencial (inactivar) | PASS | `ILocationDependencyPolicy.CanInactivateWorkCenterAsync` → 409 `WORK_CENTER_HAS_ACTIVE_DEPENDENCIES` |
| Seguridad | Validación geo (coordenadas) | PASS | lat ∈ [-90,90], long ∈ [-180,180] → `InvalidCoordinates` (422); `decimal(9,6)` en DB |
| Seguridad | Mass assignment | PASS | DTOs cerrados; patch no toca type/group/isActive |
| Seguridad | DoS JSON Patch | PASS | `[RequestSizeLimit(64KB)]` + tope 50 ops |
| Contrato | Versionado / Tags / Swagger / errores | PASS | `[ApiVersion]`+`[Route(v{version})]`+`[Tags("Work Centers")]`+`[SwaggerOperation]`×8+`[ProducesStandardErrors]`×8 |
| Contrato | OpenAPI guardrail | PASS | `Families` → `^WorkCenters`→"Work Centers" |
| Contrato | 201 + Location + ETag en Create | PASS | `ToCreatedAtActionResult` |
| Contrato | If-Match / ETag en updates | PASS | `[FromIfMatch]`+`ToActionResultWithETag` en las 5 mutaciones |
| Contrato | Paginación / Search mínimo | PASS | `[Range(1,MaxPageSize)]` (§LG6) + `MinSearchLength=2` (§LG5) |
| Rendimiento | Índices | PASS | unique `(tenant,normalized_code)`; `(tenant,group,active)`; `(tenant,type,active)`; `(tenant,normalized_name)` |
| Rendimiento | N+1 en allowedActions | PASS | §12.7/ADR-0001: deriva sólo de `canManage` (comentado) |
| Rendimiento | Rate limit | PASS | `[EnableRateLimiting(Search)]` (§LG3) |
| Concurrencia | Optimista + If-Match + 409 | PASS | Token rotado; check manual→409; `.IsConcurrencyToken()` (auto-guardrail); race→409 vía middleware |
| Concurrencia | Unique-constraint en Create/Update/Patch → 409 | **FAIL** | **WC-A**: race dup-code → 500 |
| Observabilidad | Audit logs + constantes | PASS | Los 6 commands con `AuditEventTypes.WorkCenter*` + `AuditEntityTypes.WorkCenter` (§LG8) |
| Pruebas | Unit (reglas + patch) | PASS | `LocationRulesTests` (asignación) + `WorkCenterPatchApplierTests`; **WC-C**: handlers vía integración |
| Pruebas | Integración | PASS | 8 métodos |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS limpia, alineada con la familia. `WorkCenterRules` factoriza la resolución de tipo/grupo (con manejo de tenant-mismatch → 404 vs 403) y la **validación de asignación** reutilizada por Create/Update/Patch. Dominio `WorkCenter` simple (referencias por id + campos opcionales normalizados, email a minúsculas). Cada escritura es transaccional con auditoría.

### 6.2 Seguridad
**Autorización de dos capas** (declarativa + gate `ILocationAuthorizationService`), enrolada en el governance test. **Sin IDOR**: rutas id-only (tenant del JWT), filtro global fail-closed; el work center, el tipo y el grupo referenciados se resuelven tenant-scoped con `ExistsOutsideTenant*` para 404-vs-403. **Validación de asignación robusta**: tipo activo (`WorkCenterTypeInactive`), grupo activo (`LocationGroupInactive`), nivel del grupo permite work centers (`GroupLevelNotAllowedForWorkCenter`), dirección/geo requeridos según el tipo, y **rangos de coordenadas WGS84** (`InvalidCoordinates`). **Anti-mass-assignment**: el patch no toca type/group/isActive (endpoints dedicados). JSON-Patch hardened.

### 6.3 Contrato API
Totalmente canónico (versionado, tags, swagger, produces-standard-errors, 201/Location/ETag, If-Match obligatorio) y enrolado en el OpenAPI guardrail. El `/reassign-group` separa el cambio de grupo (que re-valida el nivel) del PUT/PATCH — buen diseño. El PATCH re-valida geo contra el tipo (sin cambiar el tipo).

### 6.4 Rendimiento
Índices correctos para los filtros (`(tenant,group,active)`, `(tenant,type,active)`) + código único + nombre. Geo `decimal(9,6)` (≈0.11 m de precisión, suficiente). **`includeAllowedActions` sin N+1** (sólo `canManage`). Search libre `Contains` no-sargable con `MinSearchLength=2`. La validación de asignación carga los niveles (1 query, ~3 filas) por mutación — aceptable. Rate-limit en List.

### 6.5 Concurrencia y consistencia
If-Match obligatorio en las 5 mutaciones; token rotado; `.IsConcurrencyToken()` (auto-guardrail, verificado verde); check manual→409; race read-check-write→409 vía middleware. FKs `Restrict` a grupo/tipo (no se pueden borrar con work centers). **WC-A**: Create/Update/Patch verifican `CodeExistsAsync` y luego mutan; un race con el mismo código viola `uq_work_centers__tenant_code` → `UniqueConstraintViolationException` no atrapada → 500.

### 6.6 Observabilidad
Auditoría persistida (en transacción) en los 6 commands con **constantes** `AuditEventTypes.WorkCenter*` + `AuditEntityTypes.WorkCenter` (§LG8). Logging estructurado vía middleware.

### 6.7 Pruebas
**Ejecutadas: 85 unit tests verdes** — `LocationRulesTests` (la validación de asignación: tipo-no-permite-WC→409, dirección-requerida→422), `WorkCenterPatchApplierTests`, y los guardrails de familia (rate-limit/pagination/concurrency/OpenAPI), todos cubriendo WorkCenters. **Revisadas: 8 integration tests**. **WC-C**: la cobertura **unit** de la orquestación de handlers descansa en integración (la lógica más compleja —reglas de asignación— sí tiene unit dedicado).

### 6.8 Build / DevSecOps
Compila; sin secretos; localización completa (los 2 errores inline de asignación están en resx).

## 7. Hallazgos

### WC-A — Race de código duplicado en Create/Update/Patch → 500 (no 409)
**Severidad:** Baja · **Categoría:** Concurrencia/Contrato · **Ubicación:** `CreateWorkCenterCommandHandler` (340), `UpdateWorkCenterCommandHandler` (466), `PatchWorkCenterCommandHandler` (860).
**Condición:** los tres verifican `CodeExistsAsync` y luego mutan; dos operaciones concurrentes con el mismo código pasan el pre-check y la segunda viola `uq_work_centers__tenant_code`; `UniqueConstraintViolationException` no atrapada → 500.
**Criterio esperado:** `409 WORK_CENTER_CODE_CONFLICT` (patrón ya resuelto en CostCenters R2 / OrgUnits OU-004 / OrgStructureCatalogs OSC-005).
**Impacto:** 500 espurio en ventana de carrera estrecha; **sin corrupción** (el índice único protege). Admin-only.
**Evidencia:** `WorkCenterConfiguration.cs:88-90`; sin `catch (UniqueConstraintViolationException)`.
**Recomendación:** capturar `UniqueConstraintViolationException`→`LocationErrors.WorkCenterCodeConflict`. **Sistémico en toda la familia Locations** (= WorkCenterTypes WCT-A, LocationGroups, LocationLevels) → solución de plataforma compartida.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### WC-B — Dos errores de asignación inline fuera del catálogo `LocationErrors`
**Severidad:** Observación · **Categoría:** Mantenibilidad · **Ubicación:** `WorkCenterRules.ValidateAssignmentAsync` líneas 1313-1316 (`WORK_CENTER_ADDRESS_REQUIRED`) y 1321-1324 (`WORK_CENTER_GEO_REQUIRED`).
**Condición:** se construyen con `new Error(...)` inline en vez de vivir en `LocationErrors` (donde sí está `InvalidCoordinates`). Mismo patrón que LocationHierarchy H-003.
**Impacto:** nulo funcionalmente — **ambos están localizados** (resx en+es presentes). Sólo inconsistencia/duplicación menor.
**Recomendación:** mover a `LocationErrors.WorkCenterAddressRequired` / `.WorkCenterGeoRequired`.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### WC-C — Cobertura unit de la orquestación de handlers vía integración
**Severidad:** Observación · **Categoría:** Pruebas · **Ubicación:** `tests/CLARIHR.Application.UnitTests`.
**Condición:** los handlers (Create/Update/Reassign/Activate/Inactivate) se cubren con 8 integration tests; la lógica más compleja (`WorkCenterRules` de asignación) sí tiene unit dedicado en `LocationRulesTests`.
**Impacto:** bajo — cobertura adecuada en conjunto; los guardrails de familia protegen el contrato.
**Recomendación:** opcional — unit de handler para code-conflict / reassign-group-not-allowed.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

## 8. Hallazgos fuera de alcance / trazabilidad

- **Familia Locations completa (cierre):** con WorkCenters auditado, los 5 controllers de Locations (LocationGroups, LocationHierarchy, LocationLevels, WorkCenterTypes, WorkCenters) quedan revisados — **todos canónicos y enrolados en los guardrails §LG**.
- **WC-A es el mismo race sistémico de toda la familia Locations** (a diferencia de CostCenters/OrgUnits/OrgStructureCatalogs que ya lo cerraron). Candidato a remediación de plataforma transversal.
- **WC-B = mismo patrón que H-003** (inline error) — podría limpiarse junto con los demás de la familia.
- **Colisión de prefijo evitada:** `^WorkCenters` y `^WorkCenterTypes` no colisionan.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|
| WC-A | Baja | Concurrencia | Race dup-code → 500 (sistémico familia) | Bajo | Media | Catch `UniqueConstraintViolationException`→409 (plataforma) |
| WC-B | Obs | Mantenibilidad | 2 errores inline vs catálogo | Bajo | Baja | Mover a `LocationErrors` |
| WC-C | Obs | Pruebas | Unit de handlers vía integración | Bajo | Baja | Unit opcional |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`WorkCentersController`) | **Aprobado con observaciones** (uno de los más limpios de la ola) |
| Seguridad | Aprobado (validación de asignación + geo + tenant + integridad referencial) |
| Arquitectura | Aprobado |
| Contrato | Aprobado (totalmente canónico + guardrails 5/5) |
| Performance | Aprobado (sin N+1, MinSearchLength, rate-limit, índices) |
| Concurrencia | Aprobado con observaciones (WC-A) |
| Pruebas | Aprobado (WC-C = unit de handlers vía integración) |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**Controlador maduro, seguro y totalmente canónico; puede avanzar a QA.** El hallazgo de peso (WC-A) es el race de código duplicado, sistémico de la familia Locations y de impacto bajo.

## 11. Recomendaciones finales

1. **WC-A:** capturar el unique-violation→409 (preferible como **solución de plataforma** para toda la familia Locations).
2. **WC-B:** mover los 2 errores de asignación al catálogo `LocationErrors`.
3. **WC-C:** unit de handler opcionales.
4. Mantener las fortalezas (modelo de entidad de ubicación canónica): dos capas de autz, validación de asignación tipo↔grupo↔geo, If-Match obligatorio, rate-limit/pagination/MinSearchLength, allowedActions sin N+1, integridad referencial, constantes de auditoría, enrolamiento 5/5.

## 12. Anexos / Evidencia revisada

- Controller: `WorkCentersController.cs` (8 endpoints + DTOs).
- Aplicación: `WorkCenterAdministration.cs` (DTOs/validators/8 handlers + `WorkCenterRules` + `WorkCenterPolicyAdapter` + `WorkCenterPatchApplier` + mapper), `LocationCommon.cs`, `LocationPolicies.cs`.
- Dominio: `WorkCenter.cs`, `LocationNormalization.cs`, `TenantEntity.cs`.
- Persistencia: `IWorkCenterRepository.cs`, `WorkCenterRepository.cs`, `WorkCenterConfiguration.cs`.
- Seguridad/dependencias: `LocationAuthorizationService.cs`, `IWorkCenterTypeRepository`/`ILocationGroupRepository`/`ILocationHierarchyRepository` (resolución), `ILocationDependencyPolicy` (inactivación), `Program.cs` (policies + rate-limit).
- Pruebas: `LocationRulesTests.cs` (asignación), `WorkCenterPatchApplierTests.cs`, `ApiIntegrationTests.cs` (8 `WorkCenters_*` métodos); guardrails de familia.
- Ejecución: `dotnet test --filter ~WorkCenter|~LocationRules|~LocationRateLimiting|~LocationPagination|~OpenApiContractGuardrails` → **85/85 passed** (sesión 2026-06-07).
