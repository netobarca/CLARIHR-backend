# Evaluación de Alineación Canónica + Plan — `AccountCompanyAuthorizationController`

> Nivel: **Controller** (controlador + su vertical IAM directa). Es la **evaluación de alineación canónica (15 criterios)** que *precede* a la auditoría de seguridad (esa vendrá después, como el doc 25 siguió a la eval de Subscriptions).
> Fecha: 2026-06-08 · Rama: `master` · Autor: Claude (Opus 4.8) · Estado: **PLAN — sin código aún** (decisiones de usuario tomadas)
> Hermanos ya alineados: `AccountCompaniesController` (`3b3a729`), `AccountCompanySubscriptionsController` (doc 25, PR-A/B/C uncommitted). Precedente de concurrencia Identity: **CompanyUsers weak-ETag** (§7 del tracker).

## 1. Resumen ejecutivo y veredicto

**Parcialmente canónico.** De 15 criterios: **8 ✅ cumplen**, **3 ❌ fallan** (2 ConcurrencyToken · 4 PATCH · 15 Swagger/Tags), **2 ⚠️ parciales** (1 "una sola entidad" · 13 subrecursos/tags), **2 ➖ N/A/condicional** (9 · 10). El **criterio 14 (`/v1`) ya está resuelto** (la familia `account/*` se versionó en `d901906`).

No es un CRUD sobre entidad propia: es una **fachada IAM** sobre el dominio `IdentityAccess`. Es la **única puerta de entrada a la administración de roles IAM** (no existe `RolesController`; los comandos `*IamRole*` solo se consumen aquí), así que la fachada *es* el surface canónico de Roles. La autorización es **RBAC handler-gated** (`IIamAdministrationAuthorizationService.EnsureAuthorizedAsync`), por lo que la ausencia de `[AuthorizationPolicySet]` es **por diseño** (fuera de `GovernedFamilyRegex`, igual que los hermanos). Hoy figura como **exclusión §7 ("fachada IAM")**; esta eval lo reclasifica a **alineable**.

## 2. Contexto arquitectónico (clave)

- Maneja **dos agregados**: `IamRole` (roles + grants) y asignaciones de rol de `IamUser` (ep. `PUT users/{userPublicId}/roles`).
- El ep. user-roles **solapa** con `CompanyUsers PATCH /rolePublicIds`, que ya sincroniza `IamUser.RoleAssignments` (distinto authz: RBAC `Users:Update` aquí vs. field-level `[AuthorizeResource("RBAC_USERS")]` en CompanyUsers).
- `iam_roles` lo escribe **solo** `RoleAdministration` (+ DevSeed) → blast radius contenido para un token fuerte. `iam_users` lo escriben **~8 call sites** (CompanyUsers Create/Update/Deactivate/Reactivate, AcceptInvitation, provisioning ×2, SyncIamRoleUsers) → token fuerte ahí sería caro.
- snake_case completo en las 3 tablas (`iam_roles`/`iam_users`/`iam_user_role_assignments`) → **cero migración por snake**.

## 3. Inventario de endpoints

| # | Método | Ruta (`api/v1/account/companies/{companyPublicId}/authorization/`) | Agregado | Despacha a |
|---|---|---|---|---|
| 1 | GET | `roles` | IamRole | `ListIamRolesQuery` (paged) |
| 2 | POST | `roles` | IamRole | `CreateIamRoleCommand` → 201 |
| 3 | GET | `roles/{rolePublicId}` | IamRole | `GetIamRoleByIdQuery` |
| 4 | PUT | `roles/{rolePublicId}` | IamRole | `UpdateIamRoleCommand` (name/desc) |
| 5 | DELETE | `roles/{rolePublicId}` | IamRole | `DeleteIamRoleCommand` (hard, guarded) |
| 6 | GET | `roles/{rolePublicId}/grants` | IamRole (sub) | `GetIamRoleByIdQuery` |
| 7 | PUT | `roles/{rolePublicId}/grants` | IamRole (sub) | `SyncIamRolePermissionsCommand` |
| 8 | PUT | `users/{userPublicId}/roles` | **IamUser** | `SyncIamUserRolesCommand` |

## 4. Evaluación criterio por criterio

| # | Criterio | Estado | Evidencia / razón |
|---|---|---|---|
| 1 | Administra solo su entidad | ⚠️ Parcial | Fachada sobre 2 agregados: `IamRole` (primario) + `IamUser` role-assignments (ep. 8). Decisión: se mantiene el ep. 8 aquí (ver §5). |
| 2 | ConcurrencyToken en la entidad | ❌ Falla | Ni `IamRole` ni `IamUser` tienen token. Factible/barato en `IamRole`; caro en `IamUser`. Decisión: token fuerte en `IamRole` + weak-ETag en user-roles (§5). |
| 3 | Cada endpoint retorna su entidad | ✅ Cumple | Todas las mutaciones devuelven el objeto completo; DELETE→204. |
| 4 | PATCH JSON Patch RFC-6902 | ❌ Falla | No hay PATCH (todo PUT). → PR-C. |
| 5 | IDs `{Entidad}PublicId` | ✅ Cumple | Wire vía auto-transform: id propio→`publicId`, FK→`rolePublicId`/`userPublicId` (confirmado en integration test). |
| 6 | GET = único array | ✅ Cumple | `ListRoles` único array de la entidad primaria; `/grants` = subcolección de un rol. |
| 7 | POST → 201 + objeto | ✅ Cumple | `CreatedAtAction` + 201. (Le faltará `ETag` hasta PR-B.) |
| 8 | snake_case en BD | ✅ Cumple | Las 3 tablas snake. Cero migración por snake. |
| 9 | Ejecutar migración | ⏸️ Condicional | Solo por el token de `IamRole` (PR-B). Se genera; **la aplica el usuario**. |
| 10 | PATCH estado-solo → reemplazar; lógica de negocio → separado | ➖ N/A | No hay PATCH de estado (roles sin `IsActive`). Grants/user-roles tienen lógica (last-admin/system-role) → se quedan como PUT. |
| 11 | Sin fallback en borrado | ✅ Cumple | `DeleteRole` = hard delete real (`RemoveRole`), sin soft-delete. |
| 12 | Delete vs soft-delete + motivo | ✅ Cumple (hard) | Sin FK histórica entrante (auditoría por snapshot, no FK); guard previo (no system-role, no rol asignado → `RoleAssignedToUsers`). Hard delete con check de uso = correcto. |
| 13 | Subrecursos / tags | ⚠️ Parcial | Falta `[Tags]`. `grants` ok (sub de Role); `users/{id}/roles` (sub de IamUser) se mantiene y documenta (§5). |
| 14 | URL `…/v1/…` | ✅ Cumple | Ya `api/v1/...` (`d901906`). |
| 15 | `[SwaggerOperation]` | ❌ Falla | Sin `[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors]`; no enrolado en `OpenApiContractGuardrailsTests.Families`. → PR-A. |

## 5. Decisiones de usuario (2026-06-08)

1. **Concurrencia (crit. 2):** **token fuerte en `IamRole`** (`.IsConcurrencyToken()` + 1 migración) + **weak-ETag** para el ep. user-roles (reusar la infra `CompanyUserETag`/`ToActionResultWithWeakETag` de CompanyUsers; 0 migración). Consistente con el precedente Identity.
2. **Ep. `users/{userPublicId}/roles` (crit. 1/13):** **mantener y alinear aquí** (weak-ETag + Swagger). El solapamiento con `CompanyUsers PATCH /rolePublicIds` se documenta como **aceptado** (dos puertas, distinto modelo de authz).

## 6. Plan de implementación (PRs incrementales por riesgo)

### PR-A — Contrato/documentación · *no-breaking, sin migración* · cubre **13, 15**
- `[Tags("Account Authorization")]` + `[SwaggerOperation(Summary+Description)]` en los 8 endpoints (espeja `AccountCompaniesController`).
- Reemplazar las listas verbosas `[ProducesResponseType<ProblemDetails>]` por `[ProducesStandardErrors(StandardErrorSet.Query/Read/SubResourceWrite)]`.
- Enrolar en `OpenApiContractGuardrailsTests.Families`: `("AccountAuthorization", new Regex(@"^AccountCompanyAuthorization"), "Account Authorization")` — prefijo disjunto de `AccountCompanies`/`AccountCompanySubscriptions` (ya documentado en el guardrail), sin colisión.
- Comentario de exclusión authz (handler-gated RBAC, **NO** `[AuthorizationPolicySet]`).
- Pendiente: regenerar la sección de `openapi.yaml` (requiere levantar la API; sin test de drift que bloquee CI).

### PR-B — ConcurrencyToken + If-Match en `IamRole` · *breaking writes, CON migración* · cubre **2, 7, 9**
- **Dominio:** `IamRole` gana `Guid ConcurrencyToken` + `RefreshConcurrencyToken()` en `UpdateDetails` **y en `SyncPermissions`** (los grants viven en tabla hija → el token del padre debe rotar también ahí).
- **EF:** `IamRoleConfiguration` mapea `concurrency_token` con `.IsConcurrencyToken()`; extender `ConcurrencyTokenMappingGuardrailsTests` con `typeof(IamRole)`.
- **Migración** `AddConcurrencyTokenToIamRoles` (`concurrency_token uuid not null`, backfill `defaultValueSql: "gen_random_uuid()"`; snapshot sin default). **La aplica el usuario** (`dotnet ef database update --project src/CLARIHR.Infrastructure --startup-project src/CLARIHR.Api`).
- **Contrato:** exponer `ConcurrencyToken`; `GET roles/{id}` emite `ETag`; `PUT roles/{id}` y `PUT roles/{id}/grants` exigen `[FromIfMatch]` (ausente→400, stale→**409 `CONCURRENCY_CONFLICT`**) y devuelven el token rotado; `POST` emite `ETag`. Validators con `ConcurrencyToken NotEmpty`.
- **Handlers:** validar `role.ConcurrencyToken == command.ConcurrencyToken` antes de mutar → 409 (error ya existe en IdentityAccess, sin resx nuevo).
- ⚠️ **Breaking FE:** `If-Match` obligatorio en writes de roles.

### PR-C — PATCH RFC-6902 de roles · *aditivo, no-breaking* · cubre **4**
- `PATCH roles/{rolePublicId}` (`application/json-patch+json`, `[RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]`), paths **escalares** `/name`, `/description`. Espeja `AccountCompanyPatch`/`CompetencyConductPatch`: re-valida unicidad de nombre (→409 `RoleAlreadyExists`), protege system-role, `If-Match`+`ETag`.
- `PatchIamRoleCommand` + `IamRolePatchApplier` (+ unit tests). PUT conservado. Grants/user-roles **no** se pliegan (crit. 10).

### PR-D — Weak-ETag en `users/{userPublicId}/roles` · *breaking write* · cubre **2 (parte user)**
- Reusar `CompanyUserETag.Compute` (o un cómputo equivalente sobre `IamUser` perfil+status+set de roles ordenado) y `ToActionResultWithWeakETag`/`TryGetWeakIfMatch`.
- `PUT users/{userPublicId}/roles` exige `If-Match: W/"hash"` (ausente→400, stale→**409 `CONCURRENCY_CONFLICT`**), devuelve el ETag rotado. `[SwaggerOperation]` + comentario de subrecurso IamUser + nota de solapamiento aceptado con CompanyUsers.
- ⚠️ **Breaking FE:** `If-Match` en el write de user-roles.

**Tests por PR:** If-Match (400 ausente / 409 stale) + PATCH RFC-6902 + 201/Location; preservar el negativo authz existente. El integration test actual (`AccountCompanyAuthorization_RoleCreationFlow…`) deberá migrar sus PUT a `If-Match`.

## 7. Criterios ya cumplidos (sin trabajo)
3, 5, 6, 8, 11, 12, 14 → no requieren cambios.

## 8. Notas / riesgos
- **Nit de mantenibilidad (no es criterio):** el controller redefine DTOs (`AuthorizationRoleResponse`, etc.) que **duplican** `IamRoleResponse` del Application + mapea a mano. Opcional: colapsar al response del Application (reduce superficie). No bloqueante.
- **`IsCompanyScopeMismatch`** (path `companyPublicId` == JWT tenant → `Forbid()`) es defendible (valida que el segmento de ruta no cruce tenant); se conserva.
- **Orden sugerido:** PR-A → PR-B → PR-C → PR-D. PR-A es seguro e inmediato; PR-B/D son breaking (coordinar FE); PR-C es aditivo.
