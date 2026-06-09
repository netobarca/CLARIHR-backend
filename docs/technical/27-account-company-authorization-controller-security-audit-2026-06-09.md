# Auditoría de Seguridad y Correctitud — `AccountCompanyAuthorizationController`

> Nivel: **Controller + su vertical IAM directa** (roles + asignación de roles de usuario). Auditoría que *sigue* a la evaluación de alineación canónica (doc 26), igual que el doc 25 siguió a la eval de Subscriptions.
> Fecha: 2026-06-09 · Rama: `master` · Autor: Claude (Opus 4.8) · Método: 4 sub-agentes adversariales independientes (authz/IDOR · concurrencia · auditoría/perf/rate-limit · validación/mass-assign/superficie adyacente) + verificación propia.
> Estado del código auditado: el de las 4 PR de alineación canónica (doc 26), **uncommitted**.

## 1. Resumen ejecutivo y veredicto

**APROBADO CON OBSERVACIONES.** **0 críticas · 0 altas · 3 medias · 3 bajas · 2 informativas.** La superficie de seguridad **central es sólida**: autorización RBAC handler-gated correcta y completa en los 9 endpoints, aislamiento multi-tenant garantizado por el filtro global de EF sobre `IamRole`/`IamUser` (sin IDOR intra ni cross-tenant), concurrencia correcta (token fuerte doble-guardado + `DbUpdateConcurrencyException`→409, weak-ETag sin ventana de lost-update), mass-assignment del PATCH bien endurecido, y hard-delete con cascada segura. Los hallazgos son de **forense (auditoría del borrado), ergonomía de concurrencia, escalabilidad y robustez de entrada** — no hay vulnerabilidad de autorización ni de tenant-isolation.

La observación más relevante es **A-1 (DELETE de rol no emite auditoría)**: contradice de hecho la línea 48 del doc 26, que asumió "auditoría por snapshot" para justificar el hard-delete — ese snapshot nunca se escribe.

## 2. Cobertura de autorización (verificada endpoint por endpoint) — ✅ SÓLIDA

| # | Endpoint | Comando/Query | `EnsureAuthorizedAsync(screen, action)` | Evidencia |
|---|---|---|---|---|
| 1 | `GET roles` | `ListIamRolesQuery` | `(Roles, Read)` | RoleAdministration.cs |
| 2 | `POST roles` | `CreateIamRoleCommand` | `(Roles, Create)` | RoleAdministration.cs |
| 3 | `GET roles/{id}` | `GetIamRoleByIdQuery` | `(Roles, Read)` | RoleAdministration.cs |
| 4 | `PUT roles/{id}` | `UpdateIamRoleCommand` | `(Roles, Update)` | RoleAdministration.cs |
| 5 | `PATCH roles/{id}` | `PatchIamRoleCommand` | `(Roles, Update)` | PatchIamRole.cs |
| 6 | `DELETE roles/{id}` | `DeleteIamRoleCommand` | `(Roles, Delete)` | RoleAdministration.cs |
| 7 | `GET roles/{id}/grants` | `GetIamRoleByIdQuery` | `(Roles, Read)` | RoleAdministration.cs |
| 8 | `PUT roles/{id}/grants` | `SyncIamRolePermissionsCommand` | `(Permissions, Update)` | RoleAdministration.cs |
| 9 | `PUT users/{id}/roles` | `SyncIamUserRolesCommand` | `(Users, Update)` | UserAdministration.cs |

Los 9 handlers gatean RBAC como primera acción; `IsCompanyScopeMismatch` (path `companyPublicId`==JWT tenant, fail-closed con tenant nulo) está en los 9. El split de 3 pantallas (Roles=entidad · Permissions=edición de grants · Users=asignación de roles a usuario) es coherente. **No re-flaggear.**

## 3. Hallazgos

### 🟠 A-1 (MEDIA, forense — la más importante) — `DELETE roles/{id}` no escribe auditoría
`DeleteIamRoleCommandHandler` **no inyecta `IAuditService`** y no hay evento `RoleDeleted` en `AuditEventTypes` ni `AuditActions.Delete`. Es la **única** de las 6 mutaciones de la familia sin rastro de auditoría. El hard-delete cascada a `iam_role_permission_assignments` + `iam_user_role_assignments` (`DeleteBehavior.Cascade`), destruyendo el rol, sus grants y todos los enlaces usuario→rol **sin registro de quién/cuándo/qué contenía**. Para una superficie RBAC es el evento forense más crítico. ⚠️ **Contradice el doc 26 §48** ("auditoría por snapshot" — nunca se escribe). **Fix:** inyectar `IAuditService`, añadir `AuditEventTypes.RoleDeleted="ROLE_DELETED"` + `AuditActions.Delete`, cargar el rol con `includePermissions:true` y `LogAsync(... Before: CreateRoleSnapshot(role, perms))` **antes** de `SaveChangesAsync` (mismo tx). Sin resx (los event-types no son `Error` codes). Drift-proof: test unit/integración que afirme que el delete emite el evento.

### 🟠 A-2 (MEDIA, concurrencia/ergonomía) — sin GET para sembrar el weak-ETag de user-roles
A diferencia de su precedente `CompanyUsers` (cuyo GET devuelve el weak-ETag — `GetCompanyUser.cs`), este controller expone **solo** `PUT users/{id}/roles` y `GetUserAsync` deja `WeakETag=null`. Un cliente en su **primera** escritura no tiene endpoint que le devuelva un `W/"hash"` fresco → su única opción ergonómica es `If-Match: *` (que **opta por salir** de la concurrencia) o recalcular el SHA-256 client-side. El `*` en sí es un escape RFC-7232 legítimo (no un hueco), pero al ser la única vía práctica, el guard de concurrencia queda fácil de saltar. **Fix:** añadir `GET users/{userPublicId}/roles` que devuelva la proyección con `WeakETag = IamUserRolesETag.Compute(...)` (espeja `GetCompanyUser`), documentado como semilla del If-Match. *(decisión de superficie de API — ver §5)*

### 🟠 A-3 (MEDIA, escalabilidad) — el sync de grants carga el grafo completo de usuarios activos
`SyncIamRolePermissionsCommandHandler` llama `GetActiveUsersAsync(includeRoles:true)` para el guard de último-administrador: materializa **todos** los usuarios activos del tenant con `Include` de 4 niveles (`RoleAssignments→Role→PermissionAssignments→Permission`), sin paginación ni proyección (≈ N·R·P filas) en **cada** edición de grants. No es N+1 (1 query) pero crece con el tamaño del tenant. **Fix:** acotar a usuarios con rol administrativo (espeja `GetActiveAdministratorUserIdsAsync`, repo:213) o evaluar el invariante como query set-based. *(El guard necesita datos cross-usuario; no puede ser un simple COUNT.)*

### 🟡 A-4 (BAJA→MEDIA, robustez) — colecciones `permissionIds`/`roleIds` sin tope
Sin cota superior en `CreateAuthorizationRoleRequest.PermissionIds`, `UpdateAuthorizationRoleGrantsRequest.PermissionIds`, `SyncAuthorizationUserRolesRequest.RoleIds` (los validadores solo hacen `RuleForEach NotEqual(Guid.Empty)`). Un caller admin podría enviar 50k–100k GUIDs → `WHERE PublicId IN (@p0…@pN)` gigante (presión de parámetros Npgsql/plan-cache). Es la brecha que `CompetencyFramework` cerró con `.Must(Count<=N)`. Blast acotado (admin-gated) → BAJA/MEDIA. **Fix:** `.Must(ids => ids is null || ids.Count <= N)` en los 3 validadores (+ resx en/es) + guardrail de caps. Las PUT grants/user-roles tampoco llevan `[RequestSizeLimit]` (solo el PATCH lo tiene).

### 🟡 A-5 (BAJA, mantenibilidad) — 5 comandos IdentityAccess no despachados (superficie adyacente)
`CreateIamUserCommand`, `GetIamUserByIdQuery`, `ListIamUsersQuery`, `SyncIamRoleUsersCommand`, `CloneIamRoleCommand` están completos (record+validator+handler) pero **ningún controller los despacha** (búsqueda en `CLARIHR.Api` + `CLARIHR.Backoffice.Api` + tests). Latentes, no vulnerables (llevan sus gates de authz). **Chequeo REX-1 ✅ PASS:** el único otro controller que escribe `iam_users`/`iam_user_role_assignments` es `CompanyUsersController`, gateado con `[AuthorizeResource("RBAC_USERS",...)]` por acción — no hay segunda puerta sin authz (a diferencia de Files/REX-1). **Decisión:** borrar el código muerto, o dejarlo como roadmap de un futuro `IamUsersController` (ver §5). `CreateIamUserCommand` es el candidato natural a una futura "alta de usuario IAM" — si se cablea, debe conservar el front-door `IsCompanyScopeMismatch`.

### 🟡 A-6 (BAJA, tests) — cero cobertura del path weak-ETag de user-roles
Los flujos de token fuerte SÍ tienen tests (update sin If-Match→400, grants/PATCH stale→409). Pero el **weak-ETag de user-roles** no tiene test de integración (los legacy `/api/iam/users/...` están `[Fact(Skip)]`) ni unit test de `IamUserRolesETag` (su precedente sí tiene `CompanyUserETagTests`). Los comportamientos stale→409, wildcard→200 y superset-field→409 quedan **sin verificar**. **Fix:** `IamUserRolesETagTests` (espeja `CompanyUserETagTests`, incl. `*`) + integración `PUT users/{id}/roles` con stale/wildcard/fresh.

### ➖ A-7 (INFO, rate-limiting) — ningún endpoint con `[EnableRateLimiting]`
Aceptable diferir: todos son **admin-RBAC-gated** (no anónimos, sin perfil Search/Export de abuso masivo), consistente con la justificación documentada de GeneralCatalogs. Dos carve-outs candidatos si se persigue: `GET roles?includeAllowedActions=true` (análogo a CompanyUsers Search=120, añade 2 round-trips authz) y las 2 PUT que cargan el grafo (A-3). **No** poner limiter al CRUD por-id. *(decisión — ver §5)*

### ➖ A-8 (INFO, by-design) — oráculo de existencia cross-tenant 403-vs-404
`Resolve*LookupErrorAsync` distingue 403 `TENANT_MISMATCH` (id existe en otro tenant) de 404 (no existe) vía `IgnoreQueryFilters` (solo bool, sin datos). Convención app-wide intencional (CostCenters CC3, OrgUnits). **No-acción.** *(Nit menor relacionado: los validadores PUT/Create miden longitud pre-trim mientras el PATCH mide post-trim → inconsistencia de UX, sin riesgo de 500; opcional alinear.)*

## 4. Confirmado SEGURO (no re-investigar)

- **Authz**: 9/9 endpoints gateados, split Roles/Permissions/Users correcto; `IsCompanyScopeMismatch` en los 9 + fail-closed con tenant nulo.
- **Tenant-isolation / IDOR**: filtro global EF `HasTenantScope && TenantId==CurrentTenantId` (ApplicationDbContext.cs:401) sobre ambas entidades IAM. **Todos** los `IgnoreQueryFilters` alcanzables desde este controller son (a) probes booleanos de existencia (404↔403) o (b) re-anclados explícitamente al tenant del caller. Sin fuga cross-tenant.
- **SyncIamUserRoles cross-tenant**: no se puede adjuntar rol de otro tenant (`GetRolesByPublicIdsAsync` filtrado → `RolesNotFound`) ni asignar roles a usuario de otro tenant (`ResolveUserEntityAsync` filtrado / fallback re-anclado). Reafirma el falso-positivo resuelto en la auditoría CompanyUsers.
- **`*` weak-ETag**: solo concurrencia, no bypassa authz (se llega tras `EnsureAuthorizedAsync` + resolución tenant-scoped).
- **Concurrencia token fuerte**: TOCTOU cubierto por EF `.IsConcurrencyToken()`; `DbUpdateConcurrencyException`→**409** `CONCURRENCY_CONFLICT` (UnhandledExceptionMiddleware.cs:44,67-90), no 500. Validadores exigen `ConcurrencyToken NotEmpty`.
- **Weak-ETag**: cubre todos los campos que la escritura muta (roles) + superset (perfil/estado → a lo sumo 409 espurio correcto); identidad de la proyección BEFORE consistente con la entidad resuelta (incl. fallback linked-user) — sin lost-update ni cross-identity.
- **Mass-assignment PATCH**: whitelist de ops {add,replace,remove}, path de 1 segmento, blocklist read-only (id/publicId/isSystemRole/userCount/grants/permissions/concurrencyToken), unescape `~0/~1` antes de comparar, `remove /name` bloqueado, `from` inerte.
- **Overflow/500**: validadores + applier + normalizador de dominio ≤ columnas DB (name 100/desc 500). Sin 500 por overflow.
- **List no es N+1**: `resourceActionPolicyService.Evaluate` es puro in-memory; los 2 round-trips authz de `includeAllowedActions` se hoistean fuera del loop. Paginación acotada [1,100].
- **Hard-delete cascada**: guards (system-role 403, RoleAssignedToUsers 409) + cascada FK configurada en ambas tablas hijas + audit por snapshot sin FK a iam_roles → sin orphan/FK-break. *(El defecto A-1 es la ausencia del snapshot, no la cascada.)*
- **Las 5 mutaciones no-delete auditan** correctamente (Create/Update/Patch/Grants/UserRoles).

## 5. Decisiones pendientes para el usuario (antes de remediar)

1. **A-1 (DELETE audit)** — fix recomendado (claro). ¿Proceder?
2. **A-2 (GET user-roles para sembrar weak-ETag)** — añade superficie de API (1 endpoint GET). ¿Añadir, o aceptar `*` como vía de primera-escritura documentada?
3. **A-3 (acotar query de grants)** — refactor de perf interno (no breaking). ¿Aplicar ahora o diferir como roadmap?
4. **A-4 (caps de colección)** — añade validación + guardrail. ¿Tope sugerido (p.ej. 200/500)?
5. **A-5 (5 comandos muertos)** — ¿borrar el código muerto, o conservarlo como roadmap de un futuro `IamUsersController`?
6. **A-6 (tests weak-ETag)** — añadir (recomendado).
7. **A-7 (rate-limit)** — diferir con justificación (recomendado), o añadir los 2 carve-outs.

## 6. Veredicto final
Núcleo de seguridad **sólido** (authz + tenant-isolation + concurrencia + mass-assignment verificados). **Sin críticas/altas.** Remediar prioritariamente **A-1** (gap forense que el doc 26 asumió cubierto). A-2/A-3/A-4/A-6 son mejoras de robustez/ergonomía/escalabilidad de valor claro; A-5/A-7 son decisiones de alcance.

## 7. Estado de remediación (2026-06-09) — decisiones de usuario: **núcleo A-1+A-4+A-6 + borrar muerto A-5**

`build 0/0 · unit 1774/0 (+11: 6 caps + 5 ETag) · integ AccountCompanyAuthorization 7/7 (incl. delete-audit + weak-ETag)`. Uncommitted.

- **A-1 ✅** — `DeleteIamRoleCommandHandler` inyecta `IAuditService`, carga el rol con `includePermissions:true`, y `LogAsync(RoleDeleted, …, Before: CreateRoleSnapshot(role, perms))` **antes** de `RemoveRole`/`SaveChanges` (transaccional). Añadidos `AuditEventTypes.RoleDeleted="ROLE_DELETED"` (+ array `All`) y `AuditActions.Delete="Delete"` (event-types no son `Error` codes → **sin resx**). Integ test `DeleteRole_ShouldWriteAuditEntry` afirma la fila `ROLE_DELETED` vía dbContext. Corrige el supuesto erróneo de doc 26 §48.
- **A-4 ✅** — `IdentityAccessValidationRules.MaxPermissionIdsPerRole=1000` / `MaxRoleIdsPerUser=200` (topes anti-DoS, muy por encima de un rol con todo el catálogo o cualquier set realista). `.Must(Count<=N)` en los 3 validadores (CreateIamRole, SyncIamRolePermissions, SyncIamUserRoles) + resx en/es + `IdentityAccessCollectionCapGuardrailTests` (Max aceptado / Max+1 rechazado, drift-proof contra la constante).
- **A-5 ✅** — borrados los 5 pipelines muertos (`CreateIamUserCommand`/`GetIamUserByIdQuery`/`ListIamUsersQuery` + `SyncIamRoleUsersCommand`/`CloneIamRoleCommand`: record+validator+handler) + los 2 helpers exclusivos (`RoleAdministrationGuards.WouldRemoveLastAdministrator`, `UserAdministrationLookups.ResolveUserResponseAsync`) + las 2 factories de dominio huérfanas (`IamRole.Clone`, `IamUser.Create` no-linked). **Retenidos** (no borrados): `IIamAdministrationRepository.GetUsersAsync`/`GetUsersByPublicIdsAsync` + `IamUserSummaryResponse` — son superficie de repositorio implementada por ~5 fakes de test; borrarlas era churn desproporcionado para un audit de seguridad y son capacidades reutilizables por un futuro `IamUsersController`. Chequeo REX-1 reconfirmado (CompanyUsers es la única otra puerta, gateada). build/tests verdes tras el borrado.
- **A-6 ✅** — `IamUserRolesETagTests` (5: determinismo, orden-independiente, rota por roles/perfil/estado, `*`/hash-erróneo/vacío) + integ `SyncUserRoles_ShouldEnforceWeakIfMatch` (ausente→400, `*`→200, stale→409, token correcto→200 + rota).
- **A-2 ➖ diferido** (decisión usuario): el `*` queda como vía documentada de primera-escritura; añadir el GET semilla queda en backlog.
- **A-3 ➖ diferido** (decisión usuario): la carga del grafo de admins en grants-sync se mantiene (roadmap perf).
- **A-7 ➖ diferido** con justificación (admin-gated, sin perfil de abuso anónimo).
