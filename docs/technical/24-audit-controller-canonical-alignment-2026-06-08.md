# Alineación Canónica por Controlador — AuditController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-08 · Rama: `master` · Auditor/Implementador: Claude (líder técnico)
> Marco: evaluación contra la definición canónica de 15 puntos del equipo + endurecimiento acordado.

## 1. Resumen ejecutivo

`AuditController` expone el **rastro de auditoría tenant-scoped** (`/api/v1/audit/logs`): `List` (paginado con filtros) y `GetById` (detalle con before/after/diff sanitizados). El `AuditLog` es **inmutable, append-only y de solo lectura**: los registros se escriben **internamente** vía `IAuditService`, nunca por el cliente — por eso no hay (ni debe haber) POST/PUT/PATCH/DELETE.

Veredicto: **APROBADO — alineado a canónico.** De los 15 criterios: **4 ya cumplían**, **7 no aplican por la naturaleza append-only** (justificados técnicamente), y **4 gaps reales fueron remediados** (#5, #13, #14, #15) junto con el **endurecimiento** acordado (rate-limiting, MinSearchLength, ProducesStandardErrors, guardrail OpenAPI). **0 cambios de esquema → 0 migraciones.**

⚠️ **Breaking (frontend):** la URL pasó de `/api/audit/logs` a **`/api/v1/audit/logs`** (decisión del usuario) + se descontinuó el alias legacy de query `?entityId=` en favor del canónico `?entityPublicId=`.

| Indicador | Resultado |
|---|---|
| Build | ✅ 0/0 |
| Unit tests (suite completa, **ejecutada**) | ✅ 1743/1743 |
| Integration tests (`AuditLog*`, **ejecutados**) | ✅ 6/6 (RBAC 403, tenant-mismatch 403, detail, **filtro `entityPublicId` canónico**, + 2 cross-controller audit-persistence) |
| Guardrails | ✅ OpenAPI `^Audit`→"Audit Logs" + `AuditRateLimitingGovernanceTests` 3/3 |
| Migración | ⛔ No aplica (sin cambios de esquema) |

## 2. Evaluación de los 15 criterios

| # | Criterio | Estado | Detalle |
|---|----------|--------|---------|
| 1 | Administra solo su entidad | ✅ Cumplía | Solo `AuditLog` vía `IAuditLogRepository`; filtros por otras entidades son lectura |
| 2 | ConcurrencyToken | ⛔ No aplica | **Entidad inmutable sin updates** → un token sería columna muerta (ver §3) |
| 3 | Cada endpoint retorna su entidad | ✅ Cumplía | `List`→`AuditLogSummaryResponse[]`, `GetById`→`AuditLogDetailResponse` |
| 4 | PATCH JSON Patch | ⛔ No aplica | Solo lectura, sin campos mutables |
| 5 | IDs `{Entidad}PublicId` | ✅ Remediado | Ya se cumplía vía el mecanismo global `PublicContractNaming`; se eliminó el `NormalizeListQuery` redundante (ver §4) |
| 6 | GET único que retorna array | ✅ Cumplía | `List` paginado; `GetById` objeto |
| 7 | POST 201 + objeto | ⛔ No aplica | No es cliente-creable (integridad de auditoría — ver §3) |
| 8 | snake_case en BD | ✅ Cumplía | `AuditLogConfiguration` todo snake (`actor_user_id`, `event_type`, `before_json`…) |
| 9 | Ejecutar migración | ⛔ No aplica | Sin cambios de esquema |
| 10 | Nuevo PATCH de estado | ⛔ No aplica | No hay |
| 11 | Sin fallback de eliminación | ⛔ No aplica | No hay eliminación |
| 12 | Delete / soft delete | ⛔ No (correcto) | Integridad probatoria (ver §3) |
| 13 | Subrecursos / tags | ✅ Remediado | `[Tags("Audit Logs")]` + enrolado en OpenAPI guardrail; sin subrecursos |
| 14 | URL `/v1/` | ✅ Remediado | `api/audit/logs` → `api/v1/audit/logs` (literal, precedente ReportExportJobs) |
| 15 | OpenAPI `[SwaggerOperation]` | ✅ Remediado | `[SwaggerOperation]`×2 + `[ProducesStandardErrors]` (reemplaza los `[ProducesResponseType<ProblemDetails>]` manuales) |

## 3. Justificación técnica de los "no aplica" (#2, #7, #12)

- **#2 ConcurrencyToken — NO debe agregarse.** El `AuditLog` no tiene operaciones de actualización (ni en dominio ni en API) y nunca debe tenerlas. El token de concurrencia resuelve escrituras concurrentes en `UPDATE`; sin mutación sería una columna muerta que jamás se compara ni se rota, pesando en cada INSERT y contradiciendo la inmutabilidad. Agregarlo sería deuda, no canonicidad.
- **#7 POST — NO debe existir.** Los logs los genera el sistema vía `IAuditService`. Un POST permitiría **falsificar registros de auditoría** (eventos falsos / suplantación del actor) — vulnerabilidad grave de integridad/compliance.
- **#12 Sin delete (ni hard ni soft).** Un rastro borrable pierde valor probatorio; un soft-delete ocultaría registros, contradiciendo la trazabilidad. La purga por retención (si se requiere) debe ser un proceso batch administrativo, jamás un endpoint expuesto.

## 4. Hallazgo clave del #5 — el naming canónico ya estaba resuelto globalmente

El proyecto aplica `PublicContractNaming` (`Id`→`PublicId`, `XxxId`→`XxxPublicId`) de forma **global y consistente** en cuatro capas: binding de entrada (`PublicContractBindingMetadataProvider`), serialización de salida (`PublicContractJsonTypeInfoResolver`), rutas (`PublicContractRouteConvention`) y documentación (`PublicContractOperationFilter`/`PublicContractSchemaFilter`). Por tanto `auditLogId`/`actorUserId`/`entityId`/`companyId` ya se exponen en el wire como `auditLogPublicId`/`actorUserPublicId`/`entityPublicId`/`companyPublicId` — el #5 **ya se cumplía**. La convención del proyecto es nombrar C# como `*Id` y dejar que el mecanismo transforme.

La única deuda del #5 era el `NormalizeListQuery` del controlador: ~45 líneas de aliasing manual que **duplicaban** el binding global y, además, rescataban el nombre **legacy** `?entityId=`. Se **eliminó** (junto con `ResolveGuidFilter` y el `ILogger` que solo servía a ese log), dejando `List` como dispatch puro. El test de integración `AuditLogs_WithEntityPublicIdFilter` **confirma end-to-end** que `?entityPublicId=` se bindea correctamente sin el aliasing manual.

## 5. Cambios implementados

**Contrato del controlador (`AuditController.cs`)** — `[Route("api/v1/audit/logs")]`; `[Tags("Audit Logs")]`; comentario que documenta el patrón handler-gated (`[AuthorizeResource]`) + append-only; `[SwaggerOperation]` en ambos endpoints; `[ProducesStandardErrors(Query)]` (List) y `(Read)` (GetById); `[EnableRateLimiting(AuditRateLimitPolicies.Search)]` + `[ProducesResponseType(429)]` en List; eliminado `NormalizeListQuery`/`ResolveGuidFilter`/`ILogger`.

**Application (`AuditLogAdministration.cs`)** — `GetAuditLogsQueryValidator`: `Search` ahora exige `MinimumLength(2)` (además de `MaximumLength(200)`), mirror §LG5/§LR3.

**Rate-limiting** — nuevo `AuditRateLimitPolicies.Search` (`"audit-logs-search"`); registro en `Program.cs` (`CreateUserTenantPartitionedLimiter`, 120/min por user+tenant); `AuditRateLimitingGovernanceTests` drift-proof (detecta acciones por presencia de `Http*Attribute`, no por template — porque `List` rutea sobre el `[Route]` de clase y su `[HttpGet]` no lleva template).

**Guardrail OpenAPI** — enrolado `^Audit`→"Audit Logs" en `OpenApiContractGuardrailsTests.Families` (handler-gated, fuera de `GovernedFamilyRegex` por diseño).

**Tests** — eliminado `AuditControllerTests.cs` (probaba el `NormalizeListQuery` ya retirado; el controlador es ahora dispatch puro, cubierto por handler + integración); eliminado el test de integración del alias legacy `?entityId=`; versionadas todas las rutas de integración a `/api/v1/audit/logs`.

## 6. Fuera de alcance (documentado, no tocado)

- **Authz `[AuthorizeResource]` → `[Authorize]`+`[AuthorizationPolicySet]`**: el modelo actual es two-layer (filtro `[AuthorizeResource]` + re-check en handler) y correcto; migrarlo es solo consistencia de estilo. La familia Audit permanece fuera de `GovernedFamilyRegex` por diseño.
- **`openapi.yaml`**: el archivo committeado en `docs/technical/api/` no contenía la sección de audit y está [[broadly stale]]; el swagger en runtime ya queda correcto con los `[SwaggerOperation]`/`[Tags]` añadidos. La regeneración del `openapi.yaml` debe hacerse vía el proceso de extracción desde swagger (no a mano desde DTOs), como follow-up.

## 7. Verificación

`build 0/0` · suite unitaria **1743/1743** · integración `AuditLog*` **6/6** (incl. filtro `entityPublicId` canónico, RBAC 403, tenant-mismatch 403, detail contract) · `AuditRateLimitingGovernanceTests` 3/3 · OpenAPI guardrail verde. **Sin migración · sin resx.**

> Nota: un test de integración de **Backoffice** (capturado por el filtro `~Audit` por referenciar `AuditEventTypes`) falla en `BackofficeIntegrationTestWebApplicationFactory.InitializeAsync` por inicialización de su **BD de test** — ambiental, ajeno a este trabajo (no se toca Backoffice).

## 8. Pendiente

- Commit (lo maneja el usuario).
- **Coordinar con frontend** el doble breaking: ruta `→ /api/v1/audit/logs` y query `→ ?entityPublicId=` / `?actorUserPublicId=`.
- Regenerar `openapi.yaml` (sección audit) vía tooling de swagger.
