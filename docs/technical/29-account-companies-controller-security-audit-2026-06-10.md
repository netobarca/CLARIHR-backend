# Auditoría de Seguridad y Correctitud — `AccountCompaniesController` (+ Access + Catalogs)

> Nivel: **Familia de controladores post-split (doc 28)**: `AccountCompaniesController` (8 eps, agregado Company), `AccountCompanyAccessController` (3 eps read-model), `AccountCompanyCatalogsController` (4 eps authn-only). Auditoría que *sigue* a la alineación canónica (doc 28), igual que el doc 27 siguió al doc 26 para Authorization.
> Fecha: 2026-06-10 · Rama: `master` (`40f90dd`, árbol limpio) · Autor: Claude (Fable 5) · Método: 3 sub-agentes de barrido (sesión/tokens · tests/guardrails/rate-limit · validadores/columnas) + verificación propia de primera mano de la vertical completa (handlers, repos, dominio, EF, provisioning, JWT).
> Hermanos ya cerrados (no re-auditados aquí): Subscriptions (doc 25), Authorization (docs 26+27).

## 1. Resumen ejecutivo y veredicto

**APROBADO CON OBSERVACIONES.** **0 críticas · 1 alta · 1 media · 5 bajas · 2 informativas.** El núcleo de autorización es **sólido**: ownership fail-closed verificado en los 12 handlers (`CreatedByUserPublicId == sujeto JWT`, resuelto server-side), sin IDOR intra ni cross-account, PATCH RFC-6902 endurecido sin mass-assignment, concurrencia con doble guarda (compare manual → 409 + `IsConcurrencyToken` EF) y rotación de token en todas las mutaciones de dominio, validadores 12/12 alineados a columnas (constante §LR2 compartida), provisioning atómico con stamping explícito de tenant.

Los dos hallazgos que importan están **fuera del controlador pero dentro de su superficie** (lección Files/REX-1, ampliando al ciclo de vida completo):

- **AC-1 (ALTA):** archivar una empresa **no se aplica en la capa de sesión** — ni refresh, ni login, ni accept-invitation validan `Company.Status`, y archivar no revoca nada. Los miembros cuya empresa primaria es la archivada siguen operándola **indefinidamente**.
- **AC-2 (MEDIA):** el filtro global multi-tenant recorta la subconsulta de representantes legales en `FindOwnedByUserAsync` → `activeLegalRepresentatives` sale **vacío** en todos los 201 de Create y en toda lectura/mutación de una empresa ≠ tenant activo (y los snapshots de auditoría Before/After heredan el dato incompleto).

## 2. Cobertura de autorización (verificada endpoint por endpoint) — ✅ SÓLIDA

| # | Endpoint | Handler | Gate | Evidencia |
|---|---|---|---|---|
| 1 | `GET companies` | GetOwnedCompanies | ownership por query (`CreatedByUserPublicId == caller`) | CompanyRepository.BuildOwnedCompanyQuery:131 |
| 2 | `GET {id}` | GetOwnedCompanyById | `FindOwnedByUserAsync` + oráculo 404/403 | AccountCompanyAdministration.cs:121-134 |
| 3 | `POST` | CreateAccountCompany | actor resuelto + cupo (`MaxOwnedActiveCompanies=2`) | :169-181 |
| 4-7 | `PUT` / `PATCH` / `archive` / `reactivate` | Update/Patch/Archive/Reactivate | `ResolveOwnedCompanyAsync` (404→403) + If-Match | :282-296 etc. |
| 8 | `POST {id}/switch` | SwitchActiveCompany | ownership + `Status==Active` + membresía activa | :874-893 |
| 9-11 | Access: `access-context` / `role-builder-catalog` / `resource-policies/{key}` | 3 queries | `ResolveOwnershipAsync` (mismo resolver); resource-policies exige además tenant activo == empresa | :967-1095 |
| 12-15 | Catalogs: countries / company-types / lr-position-titles / lr-representation-types | 4 queries | authn-only **by design** (lookups pre-empresa; doc 28 §4) | controlador |

`AccountCompanyActorResolver` es fail-closed (sujeto no parseable → `InvalidCurrentUser`; usuario inexistente → 404). El oráculo de existencia 403-vs-404 es la convención app-wide (doc 27 §A-8). Las 3 familias están **fuera de `GovernedFamilyRegex` by-design** (ownership/authn-only, no RBAC declarativo — doc 28 §7). **No re-flaggear.**

## 3. Hallazgos

### 🔴 AC-1 (ALTA, seguridad/ciclo de vida) — archivar una empresa no se aplica en la capa de sesión

`Archive` marca `Status=Archived` y nada más. Verificado de primera mano en las tres puertas:

1. **Refresh** (`JwtTokenService.RefreshAsync:142-168`): valida token activo + `user.Status` — **nunca** el status de la empresa. `CreateAccessTokenAsync:208-209` re-deriva el `tid` de `GetPrimaryCompanyPublicIdAsync`, que hace `Join(Companies)` **sin filtro de status** (UserCompanyRepository.cs:39-47). Refresh token = 14 días, rotación encadenable → renovación indefinida.
2. **Login** (`LoginCommand` → `GenerateLoginAsync:39-42`): mismo `GetPrimaryCompanyPublicIdAsync` sin filtro → emite token del tenant archivado **para siempre**.
3. **Accept-invitation** (`AcceptCompanyUserInvitationCommand:111`): `GenerateForTenantAsync(user, companyPublicId)` sin chequear status → una invitación pendiente aceptada tras el archive emite token del tenant archivado.

Además `ArchiveAccountCompanyCommandHandler` **no revoca ningún refresh token** (contraste: `DeactivateCompanyUser` sí revoca con razón `company-user-deactivated`). Ningún middleware per-request valida status (HttpTenantContext lee el claim `tid` y ya).

**Población afectada = el caso común**: todo empleado invitado a una sola empresa tiene esa empresa como primaria (`CompanyUserProvisioningService:101,122` — primera membresía ⇒ `isPrimary:true`), y el guard del archive solo protege la primaria/activa **del caller** (el owner), no la de los miembros. Resultado: el owner archiva la empresa para decomisarla y **toda su plantilla conserva lectura/escritura completa indefinidamente**. No hay fuga cross-account (eran miembros legítimos); es **retención de acceso post-baja**, que convierte el soft-delete del doc 28 §12 en cosmético.

**Fix recomendado (por capas, el (1) es el que cierra el grifo):**
1. Filtrar `company.Status == Active` en `GetPrimaryCompanyPublicIdAsync` (o en sus dos callers de emisión) → primaria archivada ⇒ token **sin** tenant (mismo estado que un usuario recién registrado; el FE ya maneja "sin empresa activa") — cierra login + refresh en un solo punto.
2. `AcceptInvitation`: rechazar (o aceptar-sin-token-tenant) si la empresa está archivada.
3. Belt-and-braces opcional: revocar refresh tokens de los miembros al archivar (espejo `DeactivateCompanyUser`; deja solo la ventana de 15 min del access token) y/o guard per-request de status.
4. Drift-proof: integración "archive → login del miembro → sin `tid` / refresh → sin `tid`" + test de accept-invitation post-archive.

### 🟠 AC-2 (MEDIA, correctitud/contrato) — `activeLegalRepresentatives` vacío por recorte del filtro multi-tenant

`LegalRepresentative : TenantEntity` ⇒ filtro global `HasTenantScope && TenantId == ambient` (ApplicationDbContext.cs:401, fail-closed). La subconsulta de `CompanyRepository.FindOwnedByUserAsync:39-52` re-ancla `lr.TenantId == company.CompanyId` pero **no hace `IgnoreQueryFilters()`** → el filtro ambiental se intersecta:

- **Todo `POST` (201)** devuelve `activeLegalRepresentatives: []` — el tenant ambiental nunca es la empresa recién creada (y en el flujo primera-empresa el `tid` es null ⇒ fail-closed ⇒ vacío igualmente) — pese a que el LR inicial es obligatorio y se acaba de crear.
- **GET/PUT/PATCH/archive/reactivate** sobre cualquier empresa propia ≠ tenant activo → `[]` (la vista multi-empresa de la cuenta, razón de ser del controlador, muestra empresas "sin representantes").
- Los **snapshots de auditoría** Before/After (Update/Patch/Archive/Reactivate usan esta misma proyección) persisten el dato incompleto.

Matiza el doc 28 §3 criterio 3: la respuesta es estructuralmente completa pero esa colección viaja vacía en los casos cross-tenant. **Enmascarado por los tests**: `GetById_ShouldReturnActiveLegalRepresentatives` consulta la empresa == tenant ambiental, y los tests de Create verifican el LR vía DbContext con `IgnoreQueryFilters()` o con un segundo cliente ya tenantizado — nunca el body del 201. `List` y `CountOwned` no se ven afectados (Subscriptions/UserCompanyMembership/CompanyType **no** son tenant-scoped; verificado).

**Fix:** `.IgnoreQueryFilters()` en la query de `FindOwnedByUserAsync` (seguro: todos los sets están anclados explícitamente — Companies por owner, LRs por `TenantId == company`), con el comentario-convención "Intentional tenant filter bypass" (espejo IamAdministrationRepository.cs:61-63). Integración: Create → 201 con el LR inicial presente; GetById de empresa no-activa → LRs presentes.

### 🟡 AC-3 (BAJA, concurrencia) — TOCTOU del cupo de empresas activas (Create/Reactivate)

`HasCapacityForAnotherActiveCompanyAsync` es check-then-act fuera de toda serialización (Create chequea antes de abrir la tx; Reactivate igual). Dos requests concurrentes pasan ambas el cupo (`<2`) y ambas provisionan/reactivan → el owner queda con 3+ activas (abuso free-tier menor; sin invariante DB que lo ataje). Espejo del precedente PositionSlots RA-1: `pg_advisory_xact_lock` per-owner dentro de la tx antes de re-chequear, o re-check post-insert dentro de la tx. Blast acotado (cupo propio, plan FREE) → BAJA.

### 🟡 AC-4 (BAJA, robustez) — carrera de slug duplicado → 500

`GenerateUniqueSlugAsync` (check `SlugExistsAsync` + sufijos 2..100 + fallback GUID) corre bajo READ COMMITTED: dos creates simultáneos con el mismo nombre pueden pasar ambos el exists-check y chocar contra `uq_companies__slug` → `DbUpdateException` sin catch → **500**. Precedente CostCenters R2 / OU-004 / LocationGroups: catch `UniqueConstraintViolationException`. Aquí el slug es **server-generated** (no input del cliente), así que mejor que un 409: **retry interno** (regenerar con sufijo GUID y reintentar una vez) o catch→409 genérico. Ventana ínfima → BAJA.

### 🟡 AC-5 (BAJA, semántica de errores) — códigos engañosos en access-context/switch/resource-policy

- `BuildAsync == null` (no hay **suscripción activa** o plan; la empresa existe y es del caller) devuelve `COMPANY_NOT_FOUND` 404 en `GetAccessContext`, `GetRoleBuilderCatalog` y `Switch` (rollback incluido) — el hermano Subscriptions devuelve `SUBSCRIPTION_NOT_FOUND` para la misma condición (AccountCompanySubscriptionAdministration.cs:56-58). Forense/FE confundidos: "la empresa no existe" tras haberla resuelto.
- `GetOwnedCompanyResourcePolicy` reutiliza `ACTIVE_COMPANY_SWITCH_FORBIDDEN` (409, mensaje "The requested company cannot become the active company") como guard de **lectura** "tenant activo ≠ empresa" — ni es switch ni debería sonar a switch.

**Fix:** códigos dedicados (p.ej. `SUBSCRIPTION_NOT_FOUND` reutilizado + `ACCOUNT_COMPANY_ACTIVE_CONTEXT_REQUIRED`) + resx en/es ([[localization-resx-required]]). ⚠️ Cambia códigos de error visibles → avisar a FE si los parsea.

### 🟡 AC-6 (BAJA, drift-proofing/tests) — huecos de guardrail y cobertura

1. **`Company` no está enrolada en `ConcurrencyTokenMappingGuardrailsTests`** (sí lo están LegalRepresentative, CompanySubscription, IamRole, LocationGroup…). Es la entidad con más superficie If-Match de la familia; enrolarla (§LG9, 1 línea).
2. **Sin pagination guardrail**: el tope existe (`GetOwnedCompaniesQueryValidator` `InclusiveBetween(1,100)` — la familia NO está desbordable) pero es la única familia paginada de las 9 sin test que lo pinee; un `AccountCompanyPaginationGuardrailTests` que asserte el validador evita el drift.
3. **0 tests de integración para los 3 endpoints de `AccountCompanyAccess`** (recién movidos en doc 28): falta al menos el negativo de intruso (no-owner → 403/404) estilo ReportExportJobs, y un happy-path de `access-context` (que además fijaría el contrato del builder).
4. Read-handlers sin unit tests (List/GetById/AccessContext/RoleBuilder/ResourcePolicy) — menor: integración cubre List/GetById/catalogs (29 tests).

### 🟡 AC-7 (BAJA, perf) — micro-perf agregable (sin N+1)

1. `GetOwnedCompanyResourcePolicyQueryHandler` hace **5×`AuthorizeAsync` secuenciales** → 5×`IsModuleEnabledAsync` contra BD (PlanEntitlementService no cachea) + las queries del field-profile; las 5 acciones pueden evaluarse con una sola carga de entitlement+permisos.
2. `CountOwnedByUserInternalAsync` materializa **todos** los status del owner y cuenta client-side (CompanyRepository.cs:119-125) → COUNT set-based (2 `Where` o `Contains` traducible).
3. `GetOwnedCompanyByIdQueryHandler:131` carga la **entidad completa** solo para distinguir 404/403 → probe booleano `ExistsByPublicIdAsync` (precedente CC1/LR5/R3).
4. La proyección de CompanyType usa **4 subconsultas correlacionadas** al mismo catálogo (CompanyRepository.cs:149-168) → un join/proyección única.
5. **No hay índice sobre `companies.created_by_user_public_id`** y toda la familia filtra por él (List/Count/Detail) → seq scan por request a medida que crezcan los tenants. Índice `ix_companies__created_by_user_public_id` (migración — la aplica el usuario).

### ➖ AC-8 (INFO, rate-limiting) — familia sin limiters: aceptable salvo un carve-out candidato

Catalogs = reference-reads de formulario (rationale GeneralCatalogs — **no flaggear**); CRUD/List = ownership-gated con volumen mínimo y Create capacity-capped. El único candidato real es **`POST switch`**: emite access+refresh token (equivalente funcional del login, que está a **5/min**) y construye el access-context (~10 queries). Un limiter estilo auth (p.ej. 10/min/usuario) lo alinearía con la política de la familia auth-*. Decisión de alcance, no defecto.

### ➖ AC-9 (INFO, by-design) — acumulación de refresh tokens en switch

Cada switch emite un refresh token nuevo sin revocar los previos (GenerateInternalAsync:83-91). Es el mismo modelo multi-dispositivo del login (no hay binding token↔tenant; el refresh re-deriva la primaria), con rotación en cada refresh + reuse-detection por familia + revoke-all en logout/deactivación. **No-acción** (anotado para no re-flaggear).

## 4. Confirmado SEGURO (no re-investigar)

- **Ownership**: 12/12 handlers gateados server-side; fail-closed; sin parámetro de usuario falsificable; `Company` correctamente **fuera** del scope multi-tenant (raíz account-level, sin TenantId).
- **Tenant-isolation alrededor**: el único `IgnoreQueryFilters` alcanzable (IamAdministrationRepository.FindUserByTenantAndLinkedUserPublicIdAsync:62) re-ancla explícitamente al `company.PublicId` ya verificado por ownership — claims/roles del switch y access-context correctos y sin fuga. CompanySubscription/UserCompanyMembership/CompanyTypeCatalogItem/CountryCatalogItem **no** son tenant-scoped (verificado) → List/Switch/Catalogs funcionan cross-company sin bypass.
- **PATCH**: default-deny (path desconocido → 400), whitelist {add,replace,remove}, 1 segmento, blocklist read-only completa (incl. concurrencyToken/status/slug), unescape `~0/~1`, `remove /name` bloqueado, `from` inerte, name trim ≤150 == columna, no-mutación ⇒ sin rotación ni audit espurio, `[RequestSizeLimit]` + cap de operaciones.
- **Concurrencia**: doble guarda (compare manual → 409 `CONCURRENCY_CONFLICT` + `concurrency_token` `IsConcurrencyToken` EF → DbUpdateConcurrencyException → 409 middleware); el dominio rota el token en Rename/SetCompanyType/Archive/Reactivate; If-Match obligatorio (faltante → 400) — integración stale/missing en verde. Switch sin If-Match **by design** (documentado).
- **Validación**: 12/12 validadores; longitudes == columnas (constante `MaxDocumentTypeLength=40` compartida §LR2 — sin 500 por overflow); countryCode `^[A-Za-z]{2,3}$`; paginación 1–100; cupo `Math.Max(1, MaxOwnedActiveCompanies)`.
- **Provisioning** (Create): país y plan validados **siempre** dentro de `ProvisionAsync` (el chequeo condicional del handler es redundante, no un hueco); `SetTenantId` explícito en todo lo sembrado (sin depender del interceptor); membresía del owner Activa + rol admin sembrado; todo dentro de la tx ambiental del handler (rollback íntegro).
- **Switch**: ownership + `Status==Active` + membresía activa; token y cambio de primaria en la **misma tx** (refresh token incluido — mismo DbContext scoped); audit `ActiveCompanySwitched`.
- **Auditoría**: 6/6 mutaciones auditan (`CompanyCreated/Updated×2/Archived/Reactivated/ActiveCompanySwitched`) con consts `AuditEventTypes`/`AuditEntityTypes`, transaccional pre-commit. (AC-2 afecta el contenido de Before/After, no la presencia.)
- **Contratos/guardrails**: 3 familias OpenAPI enroladas (doc 28), rutas pineadas en `PublicContractGuardrailsIntegrationTests`, resx en/es completo para los 12 códigos, `List` sin N+1 (los 2 round-trips de `includeAllowedActions` hoisteados; `Evaluate` in-memory).

## 5. Decisiones pendientes para el usuario (antes de remediar)

1. **AC-1** — ¿alcance del fix? Mínimo recomendado: (1)+(2) (filtro de status en la resolución de primaria + guard en accept-invitation). ¿Añadir también revocación al archivar (3) y/o guard per-request?
2. **AC-2** — `IgnoreQueryFilters` + tests cross-company (recomendado, no breaking). ¿Proceder?
3. **AC-3** — ¿advisory lock per-owner (espejo RA-1) o aceptar con rationale (cupo=2, abuso menor)?
4. **AC-4** — ¿retry interno de slug o catch→409?
5. **AC-5** — cambia códigos de error visibles para FE. ¿Proceder y avisar, o diferir?
6. **AC-6** — guardrails + tests (recomendado; sin breaking).
7. **AC-7** — micro-perf; el índice (5) requiere migración (la aplica el usuario). ¿Ahora o roadmap?
8. **AC-8** — ¿limiter en `switch` o diferir con justificación?

## 6. Verificación (línea base auditada, código committed `40f90dd`)

- **Build:** 0 warnings / 0 errors.
- **Unit:** **1783 / 0**.
- **Integración `--filter ~AccountCompan`:** **41 / 0** (familia Companies+Access+Catalogs + Authorization + Subscriptions) — cierra de paso el re-run pendiente del doc 28 §6 (Docker estaba caído entonces).

## 8. Estado de remediación (2026-06-10) — decisiones de usuario: **todas las recomendadas**

Ejecutado AC-1(capas 1+2+3) · AC-2 · AC-3 · AC-4 · AC-5 · AC-6 · AC-7(perf+índice) · AC-8. Uncommitted.

- **AC-1 ✅ (3 capas).** (1) Nuevo `IUserCompanyRepository.GetActivePrimaryCompanyPublicIdAsync` (filtra `Status==Active`) usado en los 3 puntos de emisión de token (`JwtTokenService.GenerateAsync`/`GenerateLoginAsync`/`CreateAccessTokenAsync` del refresh) → login/refresh con primaria archivada emiten token **sin `tid`** (estado "sin empresa", que el FE ya maneja). Se dejó `GetPrimaryCompanyPublicIdAsync` intacto para sus otros 3 callers (archive/list/provisioning). El método nuevo es **abstracto a propósito** (no default-interface): un futuro repo que lo olvide no puede heredar la variante sin-filtro y reintroducir el agujero. (2) `AcceptCompanyUserInvitationCommandHandler` inyecta `ICompanyRepository` y rechaza con nuevo `AuthErrors.InvitationCompanyUnavailable` (409, +resx en/es) si la empresa no está activa. (3) `ArchiveAccountCompanyCommandHandler` inyecta `IRefreshTokenRepository`+`IDateTimeProvider`, obtiene los miembros (`GetMemberUserIdsAsync`) y revoca sus refresh tokens Core (`RevokeUsersTokensAsync`, razón `company-archived`) en la misma tx (espejo `DeactivateCompanyUser`; deja viva solo la ventana ≤15 min del access token). Drift-proof: integ AC-1 abajo.
- **AC-2 ✅.** `CompanyRepository.FindOwnedByUserAsync` aplica `IgnoreQueryFilters()` a la subconsulta de representantes legales (re-anclada a `lr.TenantId == company.CompanyId`), con el comentario-convención exigido por `IgnoreQueryFiltersGovernanceTests`. Ahora el 201 de Create y toda lectura cross-tenant traen el LR. Integ Create existente verde + nuevos checks.
- **AC-3 ✅.** Nuevo `ICompanyRepository.AcquireOwnerCapacityLockAsync` (`pg_advisory_xact_lock`, class id `ACCP`, espejo PositionSlots RA-1); Create y Reactivate adquieren el lock per-owner **dentro de la tx** y re-chequean el cupo bajo él, cerrando el TOCTOU.
- **AC-4 ✅.** Create envuelve la provisión en un retry de 2 intentos: `catch UniqueConstraintViolationException when IsSlugConflict` → rollback + nuevo `IUnitOfWork.ClearTracked()` (limpia el ChangeTracker; la tx Postgres queda abortada tras el 23505) + reintento con slug regenerado. Constante de constraint single-sourced en `AccountCompanyValidationRules.SlugUniqueConstraintName`. Slug server-generated ⇒ retry interno (no 409 al cliente).
- **AC-5 ✅.** `BuildAsync==null` (sin suscripción activa) → nuevo `AccountCompanyErrors.SubscriptionContextUnavailable` (404 `ACCOUNT_COMPANY_SUBSCRIPTION_NOT_FOUND`) en AccessContext/RoleBuilder/Switch (ya no el engañoso `COMPANY_NOT_FOUND`). ResourcePolicy "tenant activo ≠ empresa" → nuevo `ActiveCompanyContextRequired` (409 `ACCOUNT_COMPANY_ACTIVE_CONTEXT_REQUIRED`). +resx en/es. ⚠️ **2 códigos de error nuevos visibles a FE** (status codes sin cambio).
- **AC-6 ✅.** (a) `Company` enrolada en `ConcurrencyTokenMappingGuardrailsTests`. (b) `[Range(1, AccountCompanyValidationRules.MaxPageSize)]` en `List.pageSize` + nuevo `AccountCompanyPaginationGuardrailsTests` (tope single-sourced, usado por validador+atributo+guardrail). (c) 3 integ nuevos de `AccountCompanyAccess`: access-context owner-happy (fija el contrato del builder), access-context intruso→403, role-builder-catalog owner-happy.
- **AC-7 ✅ (perf no-migración + índice aparte).** GetById usa `ExistsByPublicIdAsync` (probe booleano, ya no carga el agregado); `CountOwnedByUserInternalAsync` set-based (`WHERE owner AND status IN (...)`); proyección de CompanyType colapsada de 4 subconsultas correlacionadas a 1. **5×`AuthorizeAsync` en ResourcePolicy: NO reescrito — diferido con rationale**: un método batch replicaría la lógica de gateo del `RbacAuthorizationService` core y arriesgaría drift con el evaluador de authz (peor que 4 queries extra en un read frío); el endpoint no es hot-path. Índice `ix_companies__created_by_user_public_id` en `CompanyConfiguration` + migración **`20260610171143_AddCompanyOwnerIndex`** (solo CreateIndex/DropIndex — **el usuario la aplica**).
- **AC-8 ✅.** `[EnableRateLimiting(AccountCompanyRateLimitPolicies.Switch)]` en `POST switch` (mint de tokens = login) + registro `account-company-switch` (10/min/usuario+tenant) en `Program.cs` + `AccountCompanyRateLimitingGovernanceTests` drift-proof.

**Patrón default-interface (fakes):** los 5 métodos de infraestructura nuevos (`ExistsByPublicIdAsync`, `AcquireOwnerCapacityLockAsync`, `GetMemberUserIdsAsync`, `RevokeUsersTokensAsync`, `ClearTracked`) son default-interface methods (no-op / delegación segura) — idiomático aquí (espejo `GetMembershipsAsync`) — salvando ~20 fakes. Solo `GetActivePrimaryCompanyPublicIdAsync` (semántica de seguridad) quedó abstracto e implementado explícitamente en los 4 fakes de `IUserCompanyRepository`.

**Verificación:** `build 0/0 · unit 1789/1789 (+6: 2 pagination guardrail + 3 rate-limit governance + 1 accept-invitation-archivada capa-2) · integración AccountCompany+AC-1 46/46 (41 base + 3 Access: access-context owner/intruso + role-builder owner + 2 AC-1 e2e: login-sin-tid + archive-revoke) · Auth flows 28/29` (el 1 = `Register_ShouldReturnCreatedAndTokens` topó el limiter `auth-register` 5/min al concentrar 29 auth-tests con IP de test compartida; **pasa aislado** — artefacto del filtro, no regresión). **Gotcha .NET 10 cazado por la integración:** `CompanyStatus[].Contains(x)` en LINQ-to-SQL resuelve a `MemoryExtensions.Contains(ReadOnlySpan)` → EF no traduce → 500; fix = materializar a `List` (CountOwned set-based). AC-2 `IgnoreQueryFilters` en subquery de proyección **confirmado funcional** (Create construye su 201 vía `FindOwnedByUserAsync`).

**Cobertura de AC-1 por capa — las 3 con test:** capa 1 = integ e2e `Login_WhenPrimaryCompanyArchived_ShouldIssueTokenWithoutTenant` (register→create→switch→archive-in-DB→login real ⇒ token sin `tid`); capa 2 = unit `Handle_WhenAcceptingInvitationForArchivedCompany_ShouldReturnConflict`; capa 3 = integ e2e `Archive_ShouldRevokeMembersRefreshTokens` (register→create empresa no-primaria→archive endpoint ⇒ refresh tokens del miembro revocados con razón `company-archived` verificado en BD). Además, drift-proof estructural: el método `GetActivePrimaryCompanyPublicIdAsync` es **abstracto** (fuerza implementación filtrada).

**Pendiente:** aplicar migración `20260610171143_AddCompanyOwnerIndex` (`database update`); avisar a FE los 3 códigos nuevos (AC-5 `ACCOUNT_COMPANY_SUBSCRIPTION_NOT_FOUND`/`ACCOUNT_COMPANY_ACTIVE_CONTEXT_REQUIRED` + AC-1 `auth.invitation.company_unavailable`); commit.

## 7. Veredicto final

Núcleo del controlador **sólido** (ownership + concurrencia + PATCH + validación + provisioning verificados de primera mano). **Sin críticas.** La prioridad es **AC-1** — el ciclo de vida Archive no existe fuera del propio controlador y lo convierte en cosmético para los miembros — seguida de **AC-2** (contrato visiblemente roto en el caso multi-empresa que motiva esta familia). AC-3…AC-7 son robustez/consistencia/perf de valor claro; AC-8 es decisión de alcance.
