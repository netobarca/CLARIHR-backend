# Auditoría CostCenters — seguimiento

> **Documento vivo / tracker.** Se actualiza al cerrar cada hallazgo.
> **Creado:** 2026-06-06 · **Estado:** 🟢 **CERRADO** (CC1 ✅ PR-A · CC2/CC3 ✅ PR-B · 2026-06-06) · **Reauditado:** 2026-06-07 (R1–R4 ✅ PR-C/PR-D, uncommitted) · **Owner:** equipo backend
> **Alcance:** dominio CostCenters completo — `CostCentersController` (`api/v1`, 9 endpoints) + `Features/CostCenters/` (Search/GetById/Usage/Export/Create/Update/Patch/Activate/Inactivate + Common + PATCH applier) + `CostCenterRepository` + authz (`ICostCenterAuthorizationService`) + dominio (`CostCenter`/`CostCenterNormalization`) + `CostCentersExportHandler`.
> **Dimensiones:** seguridad · arquitectura · rendimiento, contra `AGENTS.md` (§8, §17) y `docs/technical/overview/project-foundation.md` (§11).

---

## 1. Veredicto

Controlador **maduro y canónico** — fue el piloto (wave 0) del plan de alineación canónica y lo refleja: `[AuthorizationPolicySet]` + two-layer authz, `If-Match`/ETag en **todas** las mutaciones, POST→201+Location+ETag, PATCH RFC-6902 con whitelist + hardening (size limit), soft-delete vía `inactivate` con guard de uso, export acotado por el límite síncrono (413), enrolado en los guardrails de OpenAPI y de authz. **Sin vulnerabilidades crítico/alto** tras verificación adversarial.

- **Aislamiento de tenant VERIFICADO sólido:** los endpoints por `{id}` resuelven el tenant del JWT y distinguen 404 (no existe) vs `TENANT_MISMATCH` 403 (existe en otro tenant); `CostCenter : TenantEntity` → filtro global EF. Search/Export/Create validan el `{companyId}` del route contra el tenant autenticado (no se confía en el route). **Sin IDOR.**
- **Two-layer authz superset VERIFICADO:** el `[AuthorizationPolicySet(Read, Manage)]` declarativo es superset de los gates `EnsureCanRead`/`EnsureCanManage` del handler → sin 403 falsos.
- **PATCH whitelist sólida:** sólo `code/name/type/*AccountCode/description`; `concurrencyToken` e `isActive` rechazados; paths anidados rechazados; sin bypass (state validado → `Update(...)`).
- **Validación consistente** entre command validators y PATCH applier (code/name/account-codes acotados + regex; sin gaps de inyección ni de longitud; sin pitfall trim/domain — los campos son MAX-length, no exact-length).

Lo accionable: **un sobre-fetch de `usage` en el GET de detalle (perf), la ausencia de `[Range]` en `pageSize` (enrolado en guardrails), y una imprecisión en el Swagger del caso cross-tenant.**

## 2. Leyenda de estado

⬜ pendiente · 🟡 en progreso · ✅ resuelto · ⏸️ diferido · ➖ descartado

| Severidad | P1 crítico | P2 alto | P3 medio/bajo |
|---|---|---|---|
| Conteo | 0 | 1 | 2 |

---

## 3. Hallazgos accionables

| # | Dim | Sev | Estado | Hallazgo | Evidencia (`file:line`) | Fix propuesto |
|---|-----|-----|--------|----------|-------------------------|---------------|
| **CC1** | PERF | P2 | ✅ | **El GET de detalle sobre-fetcha el `usage` completo.** `GetCostCenterByIdQueryHandler` invoca `GetUsageByIdAsync` en **cada** lectura de detalle sólo para poblar el booleano `hasActiveUsage` de `allowedActions`. Ese método ejecuta **5 queries** (load + 2 COUNT de OrgUnits + 2 COUNT de PositionSlots con **triple-join** PositionSlots→JobProfiles→OrgUnits). Además, el predicado `orgUnit.CostCenterCode.Trim().ToUpper() == NormalizedCode` aplica funciones sobre la columna → no usa índice (aunque sí traduce a SQL). Ya existe `HasActiveUsageAsync` (≤2 `Any()` con early-exit) que da exactamente el booleano. El `Search` (lista) **no** tiene este problema (allowedActions in-memory, sin usage por ítem). | `CostCenterAdministration.cs:331-340`; `CostCenterRepository.cs:115-184,186-228` | En el path de `GetById` usar un chequeo booleano barato (`Any` con early-exit, espejo de `HasActiveUsageAsync` pero por `publicId`) en vez del desglose de 5 queries. Conservar `GetUsageByIdAsync` (desglose completo) sólo para el endpoint dedicado `/usage`, que sí lo necesita. <br>**✅ Resuelto (PR-A · 2026-06-06):** nuevo `ICostCenterRepository.HasActiveUsageAsync(tenantId, normalizedCode)` (probe booleano con early-exit, 1-2 queries); el `HasActiveUsageAsync(long)` existente ahora delega en él. `GetById` lo invoca con `tenantContext.TenantId.Value` + `response.Code` (que es el código normalizado) en vez de `GetUsageByIdAsync` → el GET de detalle baja de ~6 a ~2-3 queries (sin los 2 triple-join salvo que el chequeo de org-units sea false). `GetUsageByIdAsync` intacto para el endpoint `/usage`. Anclado en `CostCenters_GetById_AllowedActions_ShouldReflectActiveUsage` (canInactivate=true sin uso → false con org-unit activo; guard de equivalencia de comportamiento). Build 0/0, unit 110/0 (CostCenter+PositionSlot), integración CostCenters 13/13. |
| **CC2** | ARCH | P3 | ✅ | **Falta `[Range]` en el `pageSize` del controller.** `Search` declara `[FromQuery] int pageSize = DefaultPageSize` **sin** `[Range(1, MaxPageSize)]`. El validator del handler **sí** acota (`InclusiveBetween(1, MaxPageSize)`), así que no hay bug funcional, pero el borde del controller queda sin defensa-en-profundidad e inconsistente con los controllers canónicos (CompanyUsers/JobProfiles declaran ambos). No está enrolado en ningún guardrail de paginación. | `CostCentersController.cs:48-49`; `CostCenterAdministration.cs:143` | Agregar `[Range(1, CostCenterValidationRules.MaxPageSize)]` al `pageSize` + un guardrail drift-proof `CostCenterPaginationGuardrailsTests` (espejo de CompanyUsers F5). <br>**✅ Resuelto (PR-B · 2026-06-06):** `[Range(1, CostCenterValidationRules.MaxPageSize)]` añadido al `pageSize` de `Search` (+ `using System.ComponentModel.DataAnnotations`); validator del handler intacto (defensa-en-profundidad, mismos bounds). Nuevo `CostCenterPaginationGuardrailsTests` (regex de familia `^CostCenters`, estructural/drift-proof; red→green). Build 0/0, unit 17/17, integración 13/13. |
| **CC3** | DOCS | P3 | ✅ | **El Swagger del caso cross-tenant dice 404, el comportamiento real es 403.** La descripción de `GetById` afirma "a cost center belonging to another tenant yields `404`", pero el handler devuelve `TENANT_MISMATCH` → `ErrorType.Forbidden` → **403** (la convención app-wide: 404 = no existe en ningún lado, 403 = existe en otro tenant). El código es correcto y consistente con ~16 features; **el doc es el que miente.** | `CostCentersController.cs:66-68`; `CostCenterAdministration.cs:345-348`; `AuthorizationErrors.cs:25-29` | Corregir la prosa del `[SwaggerOperation]` de `GetById` (y revisar `Usage`/mutaciones) para decir 403 `TENANT_MISMATCH` en el caso cross-tenant, alineado a la convención app-wide. (No cambiar el comportamiento.) <br>**✅ Resuelto (PR-B · 2026-06-06):** la prosa del `[SwaggerOperation]` de `GetById` ahora dice "a non-existent id yields `404`, while an id that belongs to another tenant yields `403 TENANT_MISMATCH`" — alineada al comportamiento real y a la convención app-wide. Comportamiento sin cambios. (Revisado: ninguna otra prosa del controller afirmaba 404 cross-tenant.) |

---

## 3-bis. Reauditoría 2026-06-07

**Verificación de fixes previos (intactos):** CC1 (`CostCenterAdministration.cs` GetById usa el probe `HasActiveUsageAsync(tenant, code)`, no las 5 queries), CC2 (`[Range(1, MaxPageSize)]` en `pageSize` + `CostCenterPaginationGuardrailsTests`), CC3 (Swagger cross-tenant `403 TENANT_MISMATCH`). **Seguridad re-verificada sólida** — sin IDOR (`{id}` scoped al JWT; `{companyId}` validado contra el tenant; filtro global EF), two-layer authz superset, PATCH whitelist estricta, concurrencia → **409 limpio** (`UnhandledExceptionMiddleware` mapea `DbUpdateConcurrencyException`). Audit con constantes (`AuditEventTypes`/`AuditEntityTypes`) y persistido antes del commit (patrón §LG1) ✅. **Sin crit/high.**

CostCenters fue el piloto wave 0; desde entonces los hermanos (LegalRepresentatives §LR, LocationGroups §LG, CompanyUsers) **subieron el listón canónico** (rate-limiting gobernado, piso de búsqueda) y el piloto quedó atrás. **4 hallazgos nuevos, todos P3, ninguno de seguridad — los 4 resueltos (PR-C/PR-D, uncommitted).**

| # | Dim | Sev | Estado | Hallazgo | Evidencia | Fix |
|---|-----|-----|--------|----------|-----------|-----|
| **R1** | ARCH | P3 | ✅ | **Sin rate-limiting alguno** (Search/Export sin protección), inconsistente con **todos** los hermanos recientes (Search 120/min, Export 10/min vía `*RateLimitPolicies` + `[EnableRateLimiting]` + governance test). El Export es la clase de abuso "scan síncrono + free-text" para la que se creó `legal-representatives-export`. | `CostCentersController.cs` Search/Export (sin atributo) · `Program.cs` AddRateLimiter | **PR-C:** nuevo `CostCenterRateLimitPolicies` (`cost-centers-search`/`-export`); registro en `Program.cs` (120/10, espejo de §LR); `[EnableRateLimiting]` en Search+Export; `CostCenterRateLimitingGovernanceTests` drift-proof (regex `^CostCenters`, Inv-R1/R2/R3, clon de `LegalRepresentativeRateLimitingGovernanceTests`). |
| **R2** | ROB | P3 | ✅ | **Carrera de código duplicado → HTTP 500, no 409.** Create/Update/Patch hacen `CodeExistsAsync` (TOCTOU) y confían en el índice único `uq_cost_centers__tenant_code`, pero el `UniqueConstraintViolationException` no se capturaba → burbujeaba al middleware → 500 + log Error (el happy-path sí da 409). | `CostCenterAdministration.cs` Create/Update/Patch · `UnitOfWork.cs:18-21` · espejo `JobProfileCompensationAdministration.cs:294` | **PR-D:** `catch (UniqueConstraintViolationException) when (CostCenterConstraintViolations.IsCodeConflict(...))` → `CodeConflict` (409) en los 3 handlers. Nombre del índice **single-sourced** en `CostCenterValidationRules.CodeUniqueConstraintName`, referenciado por la EF config **y** el matcher del handler → no puede driftar (un rename del índice mantiene el mapeo 409). |
| **R3** | PERF | P3-bajo | ✅ | **`Inactivate` hacía una query redundante.** Llamaba al overload `HasActiveUsageAsync(long)`, que re-consultaba `(TenantId, NormalizedCode)` ya presentes en la entidad cargada. El overload `long` no tenía otro caller en producción. | `CostCenterAdministration.cs` Inactivate · `CostCenterRepository.cs` overload `long` | **PR-D:** `Inactivate` llama directo a `HasActiveUsageAsync(costCenter.TenantId, costCenter.NormalizedCode)` (−1 query); overload `long` huérfano eliminado del interface, repo y test double de PositionSlot. Mismo espíritu que CC1. |
| **R4** | ARCH | P3-bajo | ✅ | **Sin `MinSearchLength` en Search/Export** (solo `MaximumLength(150)`), inconsistente con §LR3/§LG5 (piso de 2 contra scans de 1 char). Impacto **marginal** aquí (tabla pequeña por tenant), puro alineamiento. | `CostCenterAdministration.cs` Search/Export validators | **PR-D:** `MinSearchLength=2` + `IsValidSearchLength` en `CostCenterValidationRules`; `.Must(...).WithMessage(...)` en ambos validators (mensaje idéntico a §LR3 → reusa la resx existente, sin entradas nuevas) + `CostCenterSearchValidatorTests` con guard de precedente (`==` al de LegalRepresentatives). |

**Anotado, NO accionado (sistémico — no es defecto de CostCenters):** `page` sin cota superior → overflow `int` en `Skip((pageNumber-1)*pageSize)` → OFFSET negativo → 500. **Idéntico en ~30 repositorios**; ningún validator del codebase acota el número de página. Corresponde un `IPipelineBehavior` global de clamp (iniciativa de plataforma transversal), no un fix por-controlador — registrarlo como hallazgo de CostCenters sería engañoso.

**Verificación:** build **0/0** · unit **1675/0** (+11: governance 3 + search-validator 8) · integración CostCenters **16/16**.

---

## 4. Descartado / ya cumple (verificado — no son hallazgos)

| Tema | Resolución |
|---|---|
| ➖ N+1 en `Search` con `includeAllowedActions` | **Falso positivo:** `ApplyAllowedActions` es in-memory (`ResourceActionPolicyService`, sin DB). La lista cuesta 2 queries (count + page projection). |
| ➖ IDOR / aislamiento de tenant | Verificado sólido: `{id}` scoped al JWT (404 vs 403 TenantMismatch); `{companyId}` validado contra el tenant; filtro global EF sobre `TenantEntity`. |
| ➖ Two-layer authz / 403 falsos | Verificado: policy declarativa ⊇ gate del handler (Read/Manage). |
| ➖ PATCH bypass / campos no permitidos | Whitelist estricta; `concurrencyToken`/`isActive` rechazados; paths anidados rechazados. |
| ➖ Pitfall trim/domain en el PATCH applier | No aplica: los campos son **MAX-length** (no exact-length); la normalización del dominio (trim+upper) es intencional; código whitespace-only → falla `IsValidCode` (regex) → error de validación, no 500. |
| ➖ Mensajes de validación FluentValidation hardcoded en inglés | **No es desviación:** es el patrón app-wide (CompanyUsers/JobProfiles/CompetencyFramework hacen igual `.WithMessage("…")`). Lo que se localiza (resx en+es) son los **códigos de error** (`CostCenterErrors`), que CostCenters sí localiza. |
| ➖ "load entity + before/after" en command handlers (3 queries) | By-design app-wide: el load es para mutar; before/after alimentan el diff de auditoría. Mismo patrón en todos los command handlers. |
| ➖ `Contains()` en el free-text search → "full scan" | Traducible a SQL `LIKE` (no client-eval); coste esperado de búsqueda libre sobre columnas normalizadas. Hay índices `(TenantId, NormalizedCode)` y `(TenantId, NormalizedName)`. |
| ➖ Export sin acotar | Acotado por `SynchronousReadLimit` (`Take(maxRows)` antes de materializar) + 413; `AsNoTracking` + proyección SQL-side. |
| ✅ Cumple | If-Match/ETag en todas las mutaciones, POST→201+Location, PATCH RFC-6902 + size limit, soft-delete + guard de uso (409 InUse), 409 para conflictos de negocio + `CONCURRENCY_CONFLICT` sólo concurrencia, `[ProducesStandardErrors]`, `[Tags]`+`[SwaggerOperation]`, enrolado en guardrails OpenAPI + authz, índices adecuados, `AsNoTracking` en lecturas, sin god-file, sin código muerto. |

---

## 5. Plan de PRs sugerido

| PR | Hallazgos | Tema |
|---|---|---|
| **PR-A** ✅ | CC1 | Perf: el GET de detalle usa un chequeo booleano de uso (early-exit) en vez del desglose de 5 queries. **Hecho 2026-06-06.** |
| **PR-B** ✅ | CC2 + CC3 | Alineación/contrato: `[Range]` en `pageSize` + guardrail drift-proof; corrección de la prosa Swagger cross-tenant (404 → 403). **Hecho 2026-06-06.** |
| **PR-C** ✅ | R1 | Rate-limiting paridad: `CostCenterRateLimitPolicies` + registro en `Program.cs` + `[EnableRateLimiting]` en Search/Export + `CostCenterRateLimitingGovernanceTests`. **Hecho 2026-06-07 (uncommitted).** |
| **PR-D** ✅ | R2 + R3 + R4 | Robustez/alineación: dup-code race → 409 (índice single-sourced), `Inactivate` sin query redundante (+ overload `long` eliminado), `MinSearchLength=2` en Search/Export. **Hecho 2026-06-07 (uncommitted).** |

---

## 6. Bitácora

| Fecha | Cambio |
|---|---|
| 2026-06-07 | **Reauditoría ✅ — los 3 fixes previos (CC1/CC2/CC3) intactos; seguridad re-verificada sólida (sin crit/high).** 4 hallazgos nuevos P3 (deriva de consistencia del piloto wave 0 vs. hermanos posteriores), los 4 resueltos. **PR-C (R1):** rate-limiting paridad — `CostCenterRateLimitPolicies` (`cost-centers-search` 120/min + `cost-centers-export` 10/min) + registro `Program.cs` + `[EnableRateLimiting]` en Search/Export + `CostCenterRateLimitingGovernanceTests` (drift-proof, clon de §LR). **PR-D (R2+R3+R4):** dup-code race → 409 (`UniqueConstraintViolationException` capturado en Create/Update/Patch; índice single-sourced en `CostCenterValidationRules.CodeUniqueConstraintName` referenciado por EF config + handler); `Inactivate` usa `HasActiveUsageAsync(tenant, code)` directo (−1 query) y el overload `HasActiveUsageAsync(long)` huérfano se eliminó (interface/repo/test double); `MinSearchLength=2` + `IsValidSearchLength` en ambos validators + `CostCenterSearchValidatorTests`. Sistémico anotado (no accionado): `page` sin cota → overflow → 500 (idéntico en ~30 repos, fix de plataforma). Build 0/0, unit 1675/0, integración CostCenters 16/16. **Sin migración.** |
| 2026-06-06 | **PR-B (CC2 + CC3) ✅ resuelto — auditoría CERRADA.** **CC2:** `[Range(1, CostCenterValidationRules.MaxPageSize)]` en el `pageSize` de `Search` (defensa-en-profundidad, validator del handler intacto) + nuevo `CostCenterPaginationGuardrailsTests` (estructural, regex `^CostCenters`, drift-proof). **CC3:** corregida la prosa Swagger de `GetById` (cross-tenant 404 → `403 TENANT_MISMATCH`), alineada al comportamiento real y a la convención app-wide; sin cambio de comportamiento. Build 0/0, unit 17/17 (CostCenter, incl. 2 guardrails), integración CostCenters 13/13. **Los 3 hallazgos cerrados (CC1/CC2/CC3 ✅).** |
| 2026-06-06 | **PR-A (CC1) ✅ resuelto.** El GET de detalle ya no invoca `GetUsageByIdAsync` (5 queries, 2 triple-join) sólo para el booleano `hasActiveUsage`: nuevo `HasActiveUsageAsync(tenantId, normalizedCode)` (probe booleano con early-exit, 1-2 queries) — el `HasActiveUsageAsync(long)` existente delega en él, sin duplicar lógica. `GetById` lo invoca con el tenant del JWT + `response.Code`. GET de detalle: ~6 → ~2-3 queries. `GetUsageByIdAsync` se conserva para el endpoint `/usage`. +1 test de integración `CostCenters_GetById_AllowedActions_ShouldReflectActiveUsage` (guard de equivalencia: canInactivate=true sin uso → false con org-unit activo). Actualizado el único otro implementador de `ICostCenterRepository` (test double de PositionSlot). Build 0/0, unit 110/0, integración CostCenters 13/13. |
| 2026-06-06 | Auditoría inicial (3 agentes Explore: seguridad/perf/arquitectura + verificación adversarial). Veredicto: controlador maduro/canónico (piloto wave 0), **sin crit/high**. Verificaciones que degradaron falsos positivos: el "N+1 en Search" es in-memory (no DB); los mensajes FluentValidation hardcoded son el patrón app-wide (no desviación); el pitfall trim/domain no aplica (campos MAX-length). 3 hallazgos accionables (0 P1, 1 P2, 2 P3): CC1 sobre-fetch de usage en el GET de detalle (perf), CC2 `[Range]` ausente en `pageSize` (guardrail), CC3 imprecisión Swagger cross-tenant (404 vs 403 real). Todos ⬜ pendientes. |
