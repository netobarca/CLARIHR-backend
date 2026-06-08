# Auditoría Técnica por Controlador — CompanyPreferencesController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-08 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Contexto: alineado en "Ola 2 #1" (commit `9c970df`, 2026-06-03); ver `api-canonical-alignment-plan.md`. Hermano de [[20-user-preferences-controller-audit-2026-06-07]] (la familia `Preferences`).

## 1. Resumen ejecutivo

`CompanyPreferencesController` administra el **singleton de preferencias por empresa** (`/api/v{version}/companies/{companyId}/preferences`): código de moneda (3 chars) y zona horaria (≤100 chars). La fila se **auto-aprovisiona con la empresa** (`CompanyProvisioningService` + backfill de la migración para empresas legacy), **no** en el primer acceso. 3 endpoints (GET, PUT, PATCH).

A diferencia de su hermano `UserPreferences` (self-scoped, authn-only), `CompanyPreferences` es **tenant-scoped con RBAC real** vía `ICompanyPreferenceAuthorizationService` (handler-gated): valida la **pertenencia al tenant** (anti-IDOR) y luego claims/grants (`CompanyPreferences.Read`/`.Admin`/`iam.administration.manage`).

Veredicto: **APROBADO CON OBSERVACIONES** — controlador maduro, seguro y totalmente canónico. 0 críticos / 0 altos / 0 medios / 0 bajos: **3 observaciones, todas resueltas** (CP-A y CP-B remediadas; CP-C implementado para Company tras decisión del usuario, mientras UserPreferences permanece sin auditar by-design). Canónico (`[ApiVersion]`/`[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors]`/If-Match obligatorio/JSON-Patch hardened con allow-list scalar-only), **anti-IDOR cross-tenant** (chequeo `tenantContext.TenantId == companyId` → `403 TENANT_MISMATCH` **antes** de tocar datos), **concurrencia en triple defensa** (check manual + `.IsConcurrencyToken()` EF + mapeo global `DbUpdateConcurrencyException`→`409`), y la ausencia de `[AuthorizationPolicySet]` es **por diseño documentado** (familia `Preferences` fuera de `GovernedFamilyRegex`, decisión del equipo). El bug HIGH de `currencyCode` con espacios (`" US"`→`500`) ya fue atrapado y corregido en la Ola 2.

| Indicador | Resultado |
|---|---|
| Build | ✅ 0/0 |
| Unit tests (suite completa, **ejecutada**) | ✅ 1740/0 |
| Integration tests (`CompanyPreferences_*`, **ejecutados**) | ✅ 15/15 (11 previos + **3 CP-B** + **1 CP-C** audit-persistence) |
| Enrolamiento en guardrails de familia | concurrency ✓ (auto); authz ✗ (by-design); OpenAPI ✓ (**CP-A: enrolado esta sesión**) |
| Hallazgos | 0 Crít · 0 Alto · 0 Media · 0 Baja · **3 Observación** (CP-A ✅, CP-B ✅, CP-C ✅ Company / User by-design) |

## 2. Alcance

**Incluido:** controlador `CompanyPreferencesController.cs`; aplicación `CompanyPreferenceAdministration.cs` (DTOs, 1 query + 2 commands, 3 validators, 3 handlers, `CompanyPreferencePatchApplier`/`State`, helpers/mapper), `PreferenceCommon.cs` (errores, permission codes); dominio `CompanyPreference.cs` + `PreferenceNormalization`/`CompanyNormalization`; persistencia `ICompanyPreferenceRepository` + `CompanyPreferenceRepository`, EF `CompanyPreferenceConfiguration`, migración `20260416225856`; autorización `ICompanyPreferenceAuthorizationService` + `CompanyPreferenceAuthorizationService` + `TenantPermissionGrantEvaluator`; aprovisionamiento `CompanyProvisioningService` (rama de preferencias); middleware `UnhandledExceptionMiddleware` (mapeo de concurrencia); pruebas (`CompanyPreferencePatchApplierTests`, `CompanyPreferenceAdministrationTests`, integración).

**Excluido:** `UserPreferences` (hermano self-scoped, auditado en doc 20); el resto del flujo de `CompanyProvisioningService` (IAM/roles/legal-rep/suscripción); el subsistema RBAC salvo la ruta de evaluación de permisos de preferencias; auditoría integral; carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en el riesgo característico de un recurso tenant-scoped con id de tenant en la ruta (**IDOR cross-tenant**), concurrencia (If-Match + token EF + race read-check-write), validación de entrada (moneda/zona horaria, JSON Patch), aprovisionamiento del singleton y adherencia canónica. Adicionalmente se ejecutó una verificación **dinámica** de la autorización (los tests CP-B nuevos prueban el rechazo cross-tenant y por-permiso contra la DB real). Evidencia: suite unitaria completa ejecutada (verde) + 14 integration tests `CompanyPreferences_*` ejecutados contra PostgreSQL.

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | AuthZ | Concurrencia |
|---|---|---|---|---|---|
| 1 | GET | `/companies/{companyId}/preferences` | Leer el singleton (moneda + zona horaria) | `EnsureCanReadAsync` (Read\|Admin\|manage) | — (token en body para el siguiente If-Match) |
| 2 | PUT | `/companies/{companyId}/preferences` | Reemplazar moneda + zona horaria | `EnsureCanManageAsync` (Admin\|manage) | **If-Match** + ETag (400 si falta, 409 stale) |
| 3 | PATCH | `/companies/{companyId}/preferences` | Patch RFC-6902 de `/currencyCode`,`/timeZone` | `EnsureCanManageAsync` (Admin\|manage) | **If-Match** + ETag (400 si falta, 409 stale) |

Singleton garantizado por unique `(tenant_id)`. Sin POST/GetById/DELETE: la fila se aprovisiona con la empresa (no es cliente-creable, no es borrable). Sin paginación/search/export.

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / DTOs | PASS | Sólo despacha CQRS; DTOs cerrados (`UpdateCompanyPreferencesRequest`, `PatchCompanyPreferencesRequest`) |
| Arquitectura | Aggregate design | PASS | `CompanyPreference : TenantEntity`; `Update()` re-normaliza y rota token; ctor privado |
| Arquitectura | Transacciones / consistencia | PASS | Singleton por tenant; cambio atómico (1 SaveChanges) |
| Seguridad | Autenticación | PASS | `[Authorize]` + `EnsureAuthorizedAsync` (IsAuthenticated + TenantId + UserId) |
| Seguridad | **IDOR / cross-tenant** | PASS | `tenantContext.TenantId.Value != companyId` → `403 TENANT_MISMATCH` **antes** de leer; **probado** por CP-B (GET+PUT) |
| Seguridad | RBAC (claims + grants) | PASS | Claims del JWT o fallback a `TenantPermissionGrantEvaluator`; **probado** por CP-B (sin-permiso → 403) |
| Seguridad | `[AuthorizationPolicySet]` | NO APLICA | Handler-gated **por diseño documentado** (familia `Preferences` fuera de `GovernedFamilyRegex`) |
| Seguridad | `IgnoreQueryFilters` seguro | PASS | El bypass en el grant evaluator aplica filtro **explícito** `user.TenantId == companyPublicId` + el tenant-match ya garantizó `companyId == JWT tenant` |
| Seguridad | Validación de entrada | PASS | Moneda trim len==3 (validator + applier + dominio); zona horaria ≤100; `companyId` NotEmpty |
| Seguridad | Mass assignment | PASS | PATCH allow-list scalar-only: rechaza `concurrencyToken`/`id`/`publicId`/timestamps/paths anidados/desconocidos/`remove` |
| Seguridad | DoS JSON Patch | PASS | `[Consumes(json-patch)]` + `[RequestSizeLimit(64KB)]` + tope de ops (`JsonPatchHardening`) |
| Contrato | Versionado / Tags / Swagger | PASS | `[ApiVersion("1.0")]`+`[Route(v{version})]`+`[Tags("Company Preferences")]`+`[SwaggerOperation]`×3 |
| Contrato | Error contract | PASS | `[ProducesStandardErrors]` GET=`Read`(401/403/404), PUT/PATCH=`Command`(400/401/403/404/409/422) |
| Contrato | OpenAPI guardrail | PASS | **CP-A** (remediado): enrolado `^CompanyPreferences`→"Company Preferences" en `Families` |
| Contrato | If-Match / ETag en updates | PASS | `[FromIfMatch] Guid`+`ToActionResultWithETag` en PUT/PATCH (ETag == token rotado, probado) |
| Rendimiento | Índices | PASS | unique `(tenant_id)` + unique `(public_id)`; FK a `companies(public_id)` |
| Rendimiento | N+1 / queries por request | PASS | GET=1 query; PUT/PATCH=1 read + 1 update; grant eval (≤3 queries) sólo en miss de claim |
| Rendimiento | Rate limit | NO APLICA | Singleton sin search/export; perfil no abusable (consistente con familia) |
| Concurrencia | Optimista + If-Match + 409 | PASS | Check manual→409 + `.IsConcurrencyToken()` (auto-guardrail) + `DbUpdateConcurrencyException`→409 (middleware) = **triple defensa** |
| Concurrencia | Race read-check-write | PASS | Dos managers concurrentes: el perdedor recibe `DbUpdateConcurrencyException` (token EF) → `409 CONCURRENCY_CONFLICT` |
| Concurrencia | Auto-provisión concurrente | NO APLICA | No auto-aprovisiona en acceso (provisión con la empresa + backfill); el race UP-A **no existe aquí** |
| Observabilidad | Audit logs | PASS | **CP-C** (remediado): Update/Patch auditan `COMPANY_PREFERENCES_UPDATED` con before/after (§LG1); `UserPreferences` sin auditar by-design (decisión del usuario) |
| Pruebas | Unit (handlers + patch applier) | PASS | `CompanyPreferencePatchApplierTests` (17) + `CompanyPreferenceAdministrationTests` (6, incl. aserciones de auditoría); suite 1740 verde |
| Pruebas | Integración | PASS | 15 (GET, PUT/PATCH valid/no-If-Match/stale/conflict, token-path, len, `" US"`, **+ CP-B: cross-tenant×2, sin-permiso**, **+ CP-C: audit-persistence**) |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS minimalista y correcta. `CompanyPreference` extiende `TenantEntity` (la preferencia pertenece al tenant) con ctor privado, factory `Create`/`CreateDefault("USD","UTC")` y `Update()` que re-normaliza ambos campos y rota el token. El applier de PATCH usa un `CompanyPreferencePatchState` intermedio (no muta el dominio hasta validar) — patrón espejo de `OrgUnitPatchApplier`. Mapper expone `PublicId` como `Id`.

### 6.2 Seguridad — RBAC tenant-scoped + anti-IDOR (riesgo central)
A diferencia de `UserPreferences` (self-scoped sin RBAC), aquí el id del tenant viaja en la ruta, así que el **IDOR cross-tenant es el riesgo central** y está **bien mitigado**: `EnsureAuthorizedAsync` rechaza `tenantContext.TenantId.Value != companyId` con `403 TENANT_MISMATCH` **antes** de cualquier lectura — y este chequeo precede al de permisos, así que ni siquiera un admin de su propio tenant puede leer otro. El segundo nivel resuelve permisos por claims del JWT o, en su defecto, por `TenantPermissionGrantEvaluator` (membership activa + grant IAM). El `IgnoreQueryFilters()` del evaluator es **seguro**: aplica un filtro de tenant **explícito** (`user.TenantId == companyPublicId`) y, además, el tenant-match previo ya garantizó `companyId == JWT tenant` (mismo patrón falso-positivo descartado en [[company-user-provisioning-audit]]). **Antes de esta auditoría, toda esta lógica no tenía ningún test** (los handlers usan un mock del servicio; integración sólo cubría el camino feliz) → ver **CP-B**. PATCH con allow-list estricta bloquea mass-assignment.

### 6.3 Contrato API
Totalmente canónico. `[ProducesStandardErrors]` usa los presets estándar (`Read` en GET, `Command` en PUT/PATCH). El `422` que `Command` declara es estrictamente alcanzable sólo en el PATCH (vía `ErrorCatalog.Validation` del applier); el PUT lo sobre-declara levemente, pero es el preset canónico para mutaciones de agregado y se acepta como tal. Única deuda de contrato: **no estaba enrolado en `OpenApiContractGuardrailsTests.Families`** (CP-A) pese a cumplir todos los requisitos — **remediado**.

### 6.4 Rendimiento
Índices únicos en `tenant_id` y `public_id`. GET = 1 query; PUT/PATCH = 1 read + 1 update. El grant evaluator añade hasta ~3 queries **sólo** cuando el usuario carece del claim directo (la mayoría de admins lo tienen → short-circuit). Sin search/export → rate-limit N/A.

### 6.5 Concurrencia y consistencia
**Triple defensa**: (1) check manual `preference.ConcurrencyToken != command.ConcurrencyToken` → `409` (corta temprano); (2) `.IsConcurrencyToken()` EF → el `UPDATE` lleva `WHERE concurrency_token = <original>` (red real contra el race read-check-write entre dos requests que pasan el check manual); (3) `UnhandledExceptionMiddleware` mapea `DbUpdateConcurrencyException`→`409 CONCURRENCY_CONFLICT`. If-Match obligatorio (`400` si falta, vía `[FromIfMatch]`); token rotado y devuelto en body + header ETag (igualdad probada). **No hay race de auto-provisión** (UP-A) porque el singleton se crea con la empresa, no en acceso.

### 6.6 Aprovisionamiento del singleton
`CompanyProvisioningService` crea `CompanyPreference.CreateDefault()` con `tenant_id = company.PublicId` para cada empresa nueva, y la migración `20260416225856` hizo **backfill** (`INSERT ... SELECT 'USD','UTC',... FROM companies`) para las empresas existentes. En consecuencia, el `404 COMPANY_PREFERENCE_NOT_FOUND` es prácticamente inalcanzable en el flujo normal (sólo si una empresa se creara saltándose el provisioning). El `concurrency_token` inicial de las filas backfilled es igual al `public_id` de la empresa — predecible pero **inofensivo** (no es secreto; se rota al primer `Update`, y mutar requiere RBAC manage).

### 6.7 Observabilidad
Update/Patch ahora **auditan** la mutación (`COMPANY_PREFERENCES_UPDATED`, entidad `CompanyPreference`, con snapshot before/after) igual que los hermanos admin tenant-scoped, escribiendo la fila de auditoría en la misma transacción que la mutación con `SaveChanges` tras el log (§LG1) → ver **CP-C**. La decisión del usuario fue auditar **solo** la config de empresa: `UserPreferences` permanece **sin auditar by-design** porque es self-scoped y el audit log es tenant-scoped (un usuario sin tenant rompería `LogAsync`, y una preferencia personal no es naturalmente una acción de un tenant).

### 6.8 Pruebas
**Ejecutadas**: suite unitaria completa **1740/1740** (incl. `CompanyPreferencePatchApplierTests` con la allow-list scalar-only y el caso `" US"`, y `CompanyPreferenceAdministrationTests` con authz-fail/conflict/patch). **Ejecutados**: 14 integration tests `CompanyPreferences_*` contra PostgreSQL, incluidos los **3 nuevos de CP-B** que prueban dinámicamente el rechazo cross-tenant (GET+PUT → `TENANT_MISMATCH`) y por-permiso (GET → `COMPANY_PREFERENCES_FORBIDDEN`).

## 7. Hallazgos

### CP-A — No enrolado en el OpenAPI guardrail pese a ser canónico
**Severidad:** Observación · **Categoría:** Contrato/Gobernanza · **Ubicación:** `OpenApiContractGuardrailsTests.Families`.
**Condición:** el controlador cumple `[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors]` pero no estaba en la tabla `Families` → su contrato no estaba protegido contra drift por CI (los comentarios de las filas `CompanyUsers`/`UserPreferences` incluso notaban que sus regex *no* lo cubren, pero nadie había creado su fila).
**Impacto:** un futuro cambio que rompiera la canonicidad no fallaría en CI.
**Recomendación:** añadir `^CompanyPreferences`→"Company Preferences" a `Families` (enrolamiento gratuito; ya pasa los invariantes).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** ✅ Cerrado (esta sesión)

### CP-B — Sin cobertura de la autorización negativa (anti-IDOR + denegación RBAC)
**Severidad:** Observación · **Categoría:** Pruebas/Seguridad · **Ubicación:** `ApiIntegrationTests.cs` (`CompanyPreferences_*`); `CompanyPreferenceAuthorizationService`.
**Condición:** la pieza de seguridad más crítica (rechazo cross-tenant `TENANT_MISMATCH` + denegación por permiso) **no tenía ningún test**: los handler tests usan un mock (`TestCompanyPreferenceAuthorizationService`), el servicio real no se ejercita en unit (vive en Infrastructure, requiere DbContext), y los integration tests sólo cubrían el camino feliz con `CompanyPreferences.Admin` en el mismo tenant. Un refactor que rompiera el check `tenantContext.TenantId != companyId` no haría fallar ningún test.
**Impacto:** riesgo de regresión silenciosa de la defensa anti-IDOR (el patrón que sí está anclado en hermanos como [[files-audit]] REX-1 / PositionSlots).
**Recomendación:** anclar con integration tests negativos (intruso→403 / dueño→200).
**Prioridad:** Media · **Esfuerzo:** Bajo · **Estado:** ✅ Cerrado (esta sesión)

### CP-C — Update/Patch no registraban auditoría (a diferencia de hermanos admin)
**Severidad:** Observación · **Categoría:** Observabilidad · **Ubicación:** `UpdateCompanyPreferencesCommandHandler`, `PatchCompanyPreferencesCommandHandler`.
**Condición:** cambiar la moneda/zona horaria del tenant no dejaba rastro de quién/cuándo. Los controladores admin tenant-scoped de la API (CostCenters/WorkCenterTypes/LocationGroups/OrgUnits) sí auditan sus mutaciones vía `IAuditService`.
**Impacto:** falta de trazabilidad de un cambio de config que afecta cálculos financieros/temporales de toda la empresa. No es una falla de seguridad ni de corrección.
**Decisión del usuario:** auditar **solo Company** (config administrativa tenant-scoped); `UserPreferences` permanece **sin auditar by-design** porque es self-scoped y el audit log es estrictamente tenant-scoped (`AuditService.LogAsync` lanza sin tenant; un usuario sin empresa rompería el endpoint, y una preferencia personal no es naturalmente una acción de un tenant).
**Remediación:** nuevos `AuditEventTypes.CompanyPreferencesUpdated` + `AuditEntityTypes.CompanyPreference` (con sus listas `All`); ambos handlers auditan vía `LogForTenantAsync(companyId)` con snapshot before/after, dentro de una transacción y con `SaveChanges` tras el log (§LG1, [[location-groups-audit]]). Camino compartido en `CompanyPreferenceAdministrationHelpers.ApplyUpdateAndAuditAsync` (PUT y PATCH reducen a "set currency + time zone").
**Prioridad:** Baja · **Esfuerzo:** Medio · **Estado:** ✅ Cerrado (Company) · `UserPreferences` by-design (decisión del usuario)

## 8. Hallazgos fuera de alcance / trazabilidad (NO re-flaggear)

- **Decisión por-diseño:** sin `[AuthorizationPolicySet]`/RBAC declarativo (familia `Preferences` handler-gated, fuera de `GovernedFamilyRegex`) — decisión del equipo registrada en el plan canónico. **El authz handler-gated sí existe y es robusto** (a diferencia de `UserPreferences`, que es authn-only).
- **Rate-limiting N/A** (singleton sin search/export; perfil no abusable).
- **Sin validación semántica** de `currencyCode` (no se valida contra ISO 4217) ni `timeZone` (no se valida contra la base IANA) — sólo longitud. Consistente con el hermano y con un catálogo abierto; el servidor nunca resuelve el `timeZone` a un `TimeZoneInfo` dentro de esta vertical. Aceptado.
- **`422` sobre-declarado en PUT** — es el preset canónico `Command`; no se reduce.
- **`concurrency_token` inicial predecible** en filas backfilled (== `public_id`) — inofensivo (no es secreto; se rota al primer update).
- **Bug `" US"`→500 ya corregido** en la Ola 2 (validator + applier validan la forma **trimeada**). No re-flaggear.
- **Race de auto-provisión (UP-A del hermano) N/A** — no aplica porque no se aprovisiona en acceso.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción | Estado |
|---|---|---|---|---|---|---|---|
| CP-A | Obs | Contrato | No enrolado en OpenAPI guardrail | Bajo | Baja | Añadir a `Families` | ✅ |
| CP-B | Obs | Pruebas/Seguridad | Sin test anti-IDOR/RBAC negativo | Bajo | Media | +3 integration tests | ✅ |
| CP-C | Obs | Observabilidad | Sin audit logs en mutaciones | Medio | Baja | `IAuditService` en Company; User by-design | ✅ |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`CompanyPreferencesController`) | **Aprobado con observaciones** (maduro, canónico; 3 observaciones, 2 ya cerradas) |
| Seguridad (IDOR cross-tenant / RBAC / validación / mass-assignment) | Aprobado (anti-IDOR probado dinámicamente) |
| Arquitectura | Aprobado |
| Contrato | Aprobado (canónico; CP-A cerrado) |
| Performance | Aprobado |
| Concurrencia | Aprobado (triple defensa) |
| Pruebas | Aprobado (1740 unit + 14 integración, incl. negativos) |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**Controlador maduro, seguro y canónico. Puede avanzar a QA sin reservas;** las observaciones no son bloqueantes (CP-A/CP-B ya remediadas; CP-C es una decisión de diseño de la familia).

## 11. Recomendaciones finales

1. **CP-A** ✅ — enrolado en `OpenApiContractGuardrailsTests.Families` (drift-proofing).
2. **CP-B** ✅ — anclada la defensa anti-IDOR/RBAC con 3 integration tests negativos.
3. **CP-C** ✅ — auditoría implementada en `Company` (decisión del usuario: solo Company; `User` permanece by-design porque el audit es tenant-scoped y el recurso es self-scoped).
4. Mantener las fortalezas: anti-IDOR (tenant-match primero), concurrencia en triple defensa, PATCH allow-list scalar-only, If-Match obligatorio, aprovisionamiento con backfill.

## 12. Anexos / Evidencia revisada

- Controller: `src/CLARIHR.Api/Controllers/CompanyPreferencesController.cs` (3 endpoints + DTOs).
- Aplicación: `src/CLARIHR.Application/Features/Preferences/Company/CompanyPreferenceAdministration.cs`, `.../Common/PreferenceCommon.cs`.
- Dominio: `src/CLARIHR.Domain/Preferences/CompanyPreference.cs`, `PreferenceNormalization`/`CompanyNormalization`.
- Persistencia: `ICompanyPreferenceRepository.cs`, `src/CLARIHR.Infrastructure/Preferences/CompanyPreferenceRepository.cs`, `.../Configurations/Preferences/CompanyPreferenceConfiguration.cs`, migración `20260416225856_MoveLocaleToUserPreferencesAndAddCompanyPreferences.cs`.
- Autorización: `ICompanyPreferenceAuthorizationService.cs`, `src/CLARIHR.Infrastructure/Preferences/CompanyPreferenceAuthorizationService.cs`, `TenantPermissionGrantEvaluator.cs`.
- Aprovisionamiento: `src/CLARIHR.Application/Features/Provisioning/CompanyProvisioningService.cs` (líneas 78-80).
- Middleware: `src/CLARIHR.Api/Middleware/UnhandledExceptionMiddleware.cs` (mapeo concurrencia→409).
- Pruebas: `CompanyPreferencePatchApplierTests.cs`, `PreferenceAdministrationTests.cs` (`CompanyPreferenceAdministrationTests`), `ApiIntegrationTests.cs` (`CompanyPreferences_*`, líneas ≈1275-1575), `OpenApiContractGuardrailsTests.cs` (fila `CompanyPreferences`).
- Ejecución: `dotnet test` suite unitaria → **1740/1740**; `--filter ~CompanyPreferences` (integración) → **14/14** (sesión 2026-06-08).

## 13. Estado de remediación (2026-06-08, uncommitted)

**CP-A, CP-B y CP-C cerrados** (CP-C: `Company` auditado; `UserPreferences` by-design por decisión del usuario). Verificación: build **0/0** · suite unitaria completa **1740/1740** · integración `CompanyPreferences_*` **15/15** · **sin migración · sin resx · sin cambio de contrato** → `openapi.yaml` intacto (CP-A es sólo el guardrail de test; CP-B/CP-C son sólo tests + auditoría interna; no cambian códigos HTTP — `403 TENANT_MISMATCH`/`COMPANY_PREFERENCES_FORBIDDEN` ya estaban en `Command`/`Read`; la tabla `audit_logs` ya existía y los `AuditEventTypes`/`AuditEntityTypes` son constantes, no requieren resx).

| ID | Estado | Remediación |
|---|---|---|
| **CP-A** | ✅ Cerrado | Enrolado `("CompanyPreferences", new Regex(@"^CompanyPreferences"), "Company Preferences")` en `OpenApiContractGuardrailsTests.Families`. Prefijo disjunto de `^CompanyUsers`; matchea sólo `CompanyPreferencesController`. El controller ya era canónico → enrolamiento gratuito; CI ahora detecta regresión de `[Tags]`/`[SwaggerOperation]`. Guardrail verde (60 casos). |
| **CP-B** | ✅ Cerrado | +3 integration tests negativos en `ApiIntegrationTests.cs`: `CompanyPreferences_Get_WhenTenantMismatch_ShouldReturnForbidden` y `..._Put_WhenTenantMismatch_...` (autenticado en tenant A, accede a `OtherTenantId` → `403 TENANT_MISMATCH`, probando que el tenant-match precede a la lógica de datos/concurrencia), y `..._Get_WhenMissingPermission_...` (usuario del tenant correcto sin permiso ni grant → `403 COMPANY_PREFERENCES_FORBIDDEN`, ejercitando el `TenantPermissionGrantEvaluator` real). 14/14 verde. |
| **CP-C** | ✅ Cerrado (Company) | Auditoría en `Company` (decisión del usuario: solo Company). Nuevos `AuditEventTypes.CompanyPreferencesUpdated` (`COMPANY_PREFERENCES_UPDATED`) + `AuditEntityTypes.CompanyPreference` (con entradas en ambas listas `All`). `UpdateCompanyPreferencesCommandHandler` y `PatchCompanyPreferencesCommandHandler` auditan vía el camino compartido `CompanyPreferenceAdministrationHelpers.ApplyUpdateAndAuditAsync`: transacción → `Update` → `SaveChanges` → `LogForTenantAsync(companyId, before/after)` → `SaveChanges` (§LG1) → commit; el `catch` hace rollback+throw (un `DbUpdateConcurrencyException` del token EF sigue mapeando a `409`). Se usa `LogForTenantAsync(companyId)` (no `LogAsync`) porque el authz ya probó `companyId == JWT tenant`. Pruebas: `CompanyPreferenceAdministrationTests` (valid PUT/PATCH afirman la entrada de auditoría con before/after; conflict/no-patchable afirman `Empty`) + integ `CompanyPreferences_Put_ShouldPersistAuditLog` (afirma que la fila `COMPANY_PREFERENCES_UPDATED` llega a la DB bajo el tenant — ancla §LG1). **`UserPreferences` NO auditado by-design** (self-scoped vs audit tenant-scoped). |

**By-design (confirmado, NO re-flaggear):** sin `[AuthorizationPolicySet]` (handler-gated, familia `Preferences` fuera de `GovernedFamilyRegex`) · rate-limit N/A (singleton sin search/export) · sin validación ISO-4217/IANA (catálogo abierto) · `concurrency_token` inicial predecible en backfill (inofensivo) · race UP-A N/A (no auto-aprovisiona en acceso) · **`UserPreferences` sin audit logs (self-scoped; el audit es tenant-scoped) — decisión del usuario 2026-06-08**.

**Pendiente:** commit (lo maneja el usuario).
