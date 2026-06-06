# Auditoría CompanyUsers — seguimiento

> **Documento vivo / tracker.** Se actualiza al cerrar cada hallazgo.
> **Creado:** 2026-06-05 · **Estado:** 🟡 Abierto · **Owner:** equipo backend
> **Alcance:** dominio CompanyUsers completo — `CompanyUsersController` (`api/v1/company/users`, 8 endpoints) + `Features/CompanyUsers/` (Create/Update/Patch/Deactivate/Reactivate/Get/GetList/ResetInvitation + Common) + `UserCompanyRepository` + authz (`AuthorizeResource` + `ICompanyUserAuthorizationService` + `IFieldPermissionService`) + `CompanyUserETag`.
> **Dimensiones:** seguridad · performance · arquitectura, contra `AGENTS.md` (§8, §17) y `docs/technical/overview/project-foundation.md` (§11).

---

## 1. Veredicto

Controlador **maduro y consciente de seguridad** — el más sensible auditado (Auth/Identity/RBAC). **Sin vulnerabilidades crítico/alto confirmadas** tras verificación adversarial:

- **Aislamiento de tenant VERIFICADO sólido:** toda mutación resuelve la membresía con `FindByUserPublicIdAsync(companyPublicId=JWT, userId)` y distingue 404 vs `TenantMismatch` (403); el tenant sale del JWT, no de un parámetro. **No hay IDOR cross-company.**
- **Sin asignación de roles cross-tenant:** `IamRole : TenantEntity` → el filtro global EF lo acota al tenant; `GetRolesByPublicIdsAsync` no usa `IgnoreQueryFilters` → solo devuelve roles del tenant del caller. *(La alarma "CRITICAL privilege escalation" del análisis inicial se degradó a una pregunta de diseño intra-tenant — ver F3.)*
- **Last-admin protegido** (update + deactivate), **PATCH whitelist** que re-despacha el Update (sin bypass), **field-level permissions** en lectura y escritura, **weak-ETag sólido** (hashea perfil+status+set de roles ordenado; rota; honra `W/"…"` y `*`), **two-layer authz superset** verificado (filtro `[AuthorizeResource]` ⊇ gate del handler).

Lo accionable: **1 N+1 (perf), rate-limiting ausente en endpoints de email/search, una pregunta de diseño de techo de privilegios, enumeración cross-tenant en la invitación, y enrolado en guardrails.**

## 2. Leyenda de estado

⬜ pendiente · 🟡 en progreso · ✅ resuelto · ⏸️ diferido · ➖ descartado

| Severidad | P1 crítico | P2 alto | P3 medio/bajo |
|---|---|---|---|
| Conteo | 0 | 3 | 4 |

---

## 3. Hallazgos accionables

| # | Dim | Sev | Estado | Hallazgo | Evidencia (`file:line`) | Fix propuesto |
|---|-----|-----|--------|----------|-------------------------|---------------|
| **F1** | PERF | P2 | ⬜ | **N+1 en el listado con `includeAllowedActions=true`**: el handler itera la página llamando `IsLastActiveAdministratorAsync` por usuario, y cada llamada re-ejecuta `GetActiveAdministratorUserIdsAsync` (varias queries) → ~N× por página (default 20). | `GetCompanyUsers.cs:46-72`; `UserCompanyRepository.cs:199-202,338-402` | Calcular el set de admins activos **una vez por página** y evaluar `isLastActiveAdministrator` en memoria (mismo patrón batch que CompetencyFramework F1). Sin el flag, el listado ya es eficiente (3-4 queries, batched). |
| **F2** | SEC/PERF | P2 | ⬜ | **Endpoints de email/search sin rate-limiting**: `POST /company/users` (invita, **envía email**) y `POST /{id}/reset-invitation` (**re-envía email**) no tienen `[EnableRateLimiting]` → abuso/email-bomb/enumeración. `GET` lista/search tampoco. Los endpoints de auth (register/login/invite-accept) **sí** están limitados (5/min/IP). | `CompanyUsersController.cs:74,228,35`; `Program.cs` (sin política CompanyUsers) | `CompanyUserRateLimitPolicies` (Invite tight ~5-10/min user+tenant; Search 120/min) + registro en `Program.cs` + `[EnableRateLimiting]` + guardrail `CompanyUserRateLimitingGovernanceTests`. Estándar: AGENTS.md §8/§17.3, foundation §11.4 ("rate limiting en endpoints sensibles"). |
| **F3** | SEC | P2 | ⬜ | **Sin techo de privilegios al asignar roles** (pregunta de diseño): con `RBAC_USERS.Update` + permiso de campo `Role`, un user-admin puede asignar **cualquier rol del tenant (incl. admin)**, sin chequeo de "solo roles ≤ los que ya tienes". Incluye auto-escalación (editarse a sí mismo). **VERIFICADO: NO hay exposición cross-tenant** (`IamRole` tenant-scoped). | `UpdateCompanyUser.cs:82-95`; `CreateCompanyUser.cs` (mismo patrón) | **Requiere decisión de producto:** si "gestionar usuarios = asignar cualquier rol del tenant" es lo intencional (modelo RBAC común) → **➖ won't-fix documentado**. Si se quiere un techo (delegación acotada) → agregar chequeo "roles solicitados ⊆ roles delegables del caller". |
| **F4** | SEC | P3 | ⬜ | **Enumeración cross-tenant en la invitación**: `POST create` devuelve el código distinto `company_users.user_in_another_company` cuando el email pertenece a **otra** empresa → un admin autenticado descubre la existencia cross-tenant de un email. (El caso misma-empresa `user_already_in_company` es aceptable.) | `CreateCompanyUser.cs:99-114`; `CompanyUserErrors.cs:42-50` | Para el caso cross-tenant, responder genérico (p. ej. invitación aceptada/encolada) sin revelar pertenencia a otra empresa. Foundation §11.4 ("respuestas genéricas para evitar enumeración"). Contexto: el caller ya es admin autenticado → severidad acotada. |
| **F5** | GUARDRAIL | P3 | ⬜ | **Sin enrolar en guardrails estructurales**: no está en `OpenApiContractGuardrailsTests.Families[]` (aunque el controller ya tiene `[Tags("Company Users")]`+`[SwaggerOperation]`), ni en `PaginationRangeGuardrailsTests` (sin `[Range]` en el boundary; el handler valida `InclusiveBetween(1,100)`), ni en un guardrail de rate-limit. | `OpenApiContractGuardrailsTests.cs:39-66`; `CompanyUsersController.cs:46` | Enrolar la familia CompanyUsers en el guardrail OpenAPI + `[Range(1, MaxPageSize)]` en `pageSize` + su guardrail de paginación (drift-proof, como CompetencyFramework F6). La exclusión de `AuthorizationPolicyConventionGovernance` **es by-design** (handler-gated `[AuthorizeResource]`, documentado en el controller). |
| **F6** | ARCH | P3 | ⬜ | **`reset-invitation` no adjunta `WeakETag`** en el `User` de la respuesta, a diferencia de `create` (que hace `with { WeakETag = Compute(...) }`) → el cliente no puede mutar inmediato sin un GET. Inconsistencia de contrato. | `ResetInvitation.cs:~190` vs `CreateCompanyUser.cs:~203` | Adjuntar `WeakETag` al `User` en la respuesta de reset-invitation (espejar `create`), o omitirlo consistentemente en ambas. |
| **F7** | ARCH | P3 (obs) | ⬜ | **`CompanyUserProvisioningService.SyncRoleAssignmentsForPositionSlot` asigna roles sin field-auth/audit** del flujo CompanyUsers. Es un flujo **distinto** (provisión por position-slot, system-driven) → fuera del alcance del controller CompanyUsers. | `CompanyUserProvisioningService.cs` | Observación: verificar que el bypass es intencional (sync system-driven con su propia auditoría de position-slot). Si no, alinear. Revisar en una auditoría aparte de provisioning. |

---

## 4. Descartado / ya cumple (verificado — no son hallazgos)

| Tema | Resolución |
|---|---|
| ➖ "CRITICAL privilege escalation cross-tenant" | **Falso positivo:** `IamRole` es tenant-scoped (filtro global EF), `GetRolesByPublicIdsAsync` no lo ignora → imposible asignar roles de otro tenant. Lo intra-tenant es F3 (diseño). |
| ➖ IDOR / aislamiento de tenant | Verificado sólido (membresía scoped al JWT; 404 vs TenantMismatch). |
| ➖ Weak-ETag "lossy / last-writer-wins" | **Incorrecto:** el `If-Match` mismatch devuelve `409 CONCURRENCY_CONFLICT` (previene el sobre-escrito). Weak-ETag computado = decisión deliberada (opción c; los 3 agregados no tienen token). |
| ➖ Transacción de 3 agregados "ventana de inconsistencia" | Es **un solo** `SaveChangesAsync` (atómico). Sin commit parcial. |
| ➖ Handler-gated `[AuthorizeResource]` (no `[AuthorizationPolicySet]`) | By-design, documentado en el controller; excluido de `GovernedFamilyRegex` a propósito. |
| ➖ Soft-delete vía deactivate (sin hard DELETE) | By-design (auditoría/compliance/re-invitación; revoca refresh tokens; last-admin guard). |
| ➖ `If-Match: *` wildcard | Manejado correctamente (`CompanyUserETag.Matches` honra `*`). |
| ➖ `search` `FirstName.ToUpper()` con null | EF traduce a SQL (null→null, sin NRE); no se evalúa client-side. |
| ✅ Cumple | Last-admin guard (update+deactivate), PATCH whitelist→re-dispatch Update, field-perms en lectura+escritura, two-layer authz superset, `[Tags]`+`[SwaggerOperation]` en los 8 endpoints, POST→201+Location, paginación acotada (validator), `AsNoTracking`, proyección SQL-side, split por operación limpio (sin god-file), sin código muerto. |

---

## 5. Plan de PRs sugerido

| PR | Hallazgos | Tema |
|---|---|---|
| **PR-A** | F1 | Perf: batch del set de admins en el listado (mayor impacto). |
| **PR-B** | F2 + F5 | Hardening: rate-limit de invite/reset/search + `[Range]` + enrolar guardrails (drift-proof). |
| **PR-C** | F6 | Consistencia: `WeakETag` en reset-invitation. |
| **Decisión** | F3 | Producto: ¿techo de privilegios en asignación de roles, o aceptar el modelo "user-admin asigna cualquier rol del tenant"? |
| **Decisión** | F4 | Privacidad: ¿respuesta genérica para la invitación cross-tenant, o aceptar (contexto admin autenticado)? |
| Aparte | F7 | Auditoría separada del provisioning (position-slot role-sync). |

---

## 6. Bitácora

| Fecha | Cambio |
|---|---|
| 2026-06-05 | Auditoría inicial (4 agentes: seguridad/perf/arquitectura/estándares + verificación adversarial). Hallazgo clave: la alarma "CRITICAL privilege escalation" se **degradó** a pregunta de diseño intra-tenant (F3) tras verificar que `IamRole` es tenant-scoped → sin exposición cross-tenant. 7 hallazgos accionables (0 P1, 3 P2, 4 P3); 8 temas descartados (falsos positivos / by-design). Todos ⬜ pendientes. |
