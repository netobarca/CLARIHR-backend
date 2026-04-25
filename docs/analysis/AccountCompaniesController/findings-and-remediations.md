# AccountCompaniesController — Hallazgos y Recomendaciones de Remediación

> **Fecha de auditoría:** 2026-04-24
> **Alcance:** Controller `src/CLARIHR.Api/Controllers/AccountCompaniesController.cs`, sus handlers en `src/CLARIHR.Application/Features/AccountCompanies/`, repositorios asociados (`CompanyRepository`, `UserCompanyRepository`) y el flujo de `JwtTokenService` cuando aplica.
> **Marco de referencia:** `AGENTS.md` (Clean Architecture, CQRS, multi-tenant, seguridad, performance).
> **Veredicto general:** El controller respeta los pilares arquitectónicos exigidos. No hay incumplimientos bloqueantes. Los hallazgos descritos son de maduración (seguridad defensiva y eficiencia de queries), no de diseño roto.

---

## Índice

- [Resumen ejecutivo](#resumen-ejecutivo)
- [Sección A — Arquitectura](#sección-a--arquitectura)
  - [A1. Boilerplate repetido de resolución de actor + ownership por handler](#a1-boilerplate-repetido-de-resolución-de-actor--ownership-por-handler)
  - [A2. DTOs de request anidados dentro del controller](#a2-dtos-de-request-anidados-dentro-del-controller)
  - [A3. Excepción `InvalidOperationException` en lugar de `Result.Failure` tras crear empresa](#a3-excepción-invalidoperationexception-en-lugar-de-resultfailure-tras-crear-empresa)
- [Sección S — Seguridad](#sección-s--seguridad)
  - [S1. `[Authorize]` plano sin policy/RBAC explícito](#s1-authorize-plano-sin-policyrbac-explícito)
  - [S2. Modelo de ownership atado al creador, divergente del modelo IAM](#s2-modelo-de-ownership-atado-al-creador-divergente-del-modelo-iam)
  - [S3. Diferenciación entre `404 NotFound` y `403 Forbidden` permite enumeración](#s3-diferenciación-entre-404-notfound-y-403-forbidden-permite-enumeración)
  - [S4. `access-context` y `role-builder-catalog` solo validan ownership por creador](#s4-access-context-y-role-builder-catalog-solo-validan-ownership-por-creador)
  - [S5. Falta auditoría en queries que exponen información sensible](#s5-falta-auditoría-en-queries-que-exponen-información-sensible)
  - [S6. Sin rate limiting visible en `POST /{id}/switch`](#s6-sin-rate-limiting-visible-en-post-idswitch)
- [Sección P — Performance](#sección-p--performance)
  - [P1. Cuatro sub-selects redundantes para resolver metadata de `CompanyType`](#p1-cuatro-sub-selects-redundantes-para-resolver-metadata-de-companytype)
  - [P2. Sub-select de `PlanCode` ejecutado por fila en listado paginado](#p2-sub-select-de-plancode-ejecutado-por-fila-en-listado-paginado)
  - [P3. Doble lectura `FindOwnedByUserAsync` por command para snapshots `Before/After`](#p3-doble-lectura-findownedbyuserasync-por-command-para-snapshots-beforeafter)
  - [P4. `CountOwnedByUserInternalAsync` materializa lista en memoria innecesariamente](#p4-countownedbyuserinternalasync-materializa-lista-en-memoria-innecesariamente)
  - [P5. `includeAllowedActions=true` puede esconder N+1 en `ResourceActionPolicyService`](#p5-includeallowedactionstrue-puede-esconder-n1-en-resourceactionpolicyservice)
  - [P6. `Switch` ejecuta doble lectura del modelo IAM (JWT + access context)](#p6-switch-ejecuta-doble-lectura-del-modelo-iam-jwt--access-context)
- [Priorización sugerida](#priorización-sugerida)

---

## Resumen ejecutivo

| Dimensión | Calificación | Resumen |
|---|---|---|
| Arquitectura / Clean / CQRS | 🟢 Cumple | Controller delgado, dispatcher, separación correcta, transacciones acotadas, auditoría en commands. |
| Seguridad | 🟡 Cumple con observaciones | Ownership funcional, pero `[Authorize]` plano, modelo de ownership rígido, falta defensa contra enumeración y rate limiting. |
| Performance | 🟡 Cumple con observaciones | Paginación + `AsNoTracking` + proyecciones OK; existen sub-selects redundantes y dobles lecturas evitables. |

**Hallazgos totales:** 15 (3 arquitectura, 6 seguridad, 6 performance).
**Bloqueantes:** 0.
**Severidad media:** 9. **Severidad baja:** 6.

---

## Sección A — Arquitectura

### A1. Boilerplate repetido de resolución de actor + ownership por handler

**Severidad:** 🟢 Baja
**Ubicación:** Todos los handlers en `AccountCompanyAdministration.cs`.

#### Hallazgo
Cada `CommandHandler` y `QueryHandler` repite el mismo patrón:
```csharp
var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(...);
if (currentUserResult.IsFailure) { return Result.Failure(...); }

var companyResult = await AccountCompanyActorResolver.ResolveOwnedCompanyAsync(...);
if (companyResult.IsFailure) { return Result.Failure(...); }
```
Está extraído a un helper estático, pero el ruido se sigue propagando 5–8 líneas por handler. La intención del caso de uso queda diluida.

#### Por qué importa
- Aumenta superficie de error: cualquier nuevo handler puede olvidar uno de los dos checks.
- Disipa la lógica del caso de uso real (renombrar, archivar, reactivar) entre infraestructura.
- Inconsistente con `AGENTS.md` §6.2 (handlers claros y pequeños).

#### Solución propuesta
Implementar un **pipeline behavior** que resuelva actor y ownership para requests marcadas con una interfaz declarativa.

1. Definir un marker en `Application`:
   ```csharp
   public interface IAccountCompanyScopedRequest
   {
       Guid CompanyId { get; }
   }
   ```
2. Pipeline behavior que:
   - Resuelve el `User` actual.
   - Resuelve la `Company` y verifica ownership.
   - Inyecta ambos al handler vía un `AccountCompanyExecutionContext` scoped.
3. Refactor de comandos/queries existentes para implementar la interfaz.

**Impacto esperado:** −5 a −8 líneas por handler, una sola fuente de verdad para la regla de ownership.
**Riesgo:** Bajo. Cambio mecánico cubierto por unit tests existentes.

---

### A2. DTOs de request anidados dentro del controller

**Severidad:** 🟢 Baja
**Ubicación:** `AccountCompaniesController.cs` (final del archivo).

#### Hallazgo
```csharp
public sealed record CreateAccountCompanyRequest(...);
public sealed record InitialLegalRepresentativeRequest(...);
public sealed record UpdateAccountCompanyRequest(...);
```
Los DTOs HTTP viven anidados al final del controller. El resto del proyecto suele declararlos en una carpeta `Contracts/` por feature.

#### Por qué importa
- Inconsistencia organizacional con el resto del codebase.
- Reutilización futura (Swagger schemas, mappers) más difícil cuando los tipos son tipos anidados.
- `AGENTS.md` §11.2 — favorecer convenciones del proyecto.

#### Solución propuesta
1. Mover los `record` a `src/CLARIHR.Api/Contracts/AccountCompanies/`:
   - `CreateAccountCompanyRequest.cs`
   - `InitialLegalRepresentativeRequest.cs`
   - `UpdateAccountCompanyRequest.cs`
2. Actualizar `using` en el controller.

**Impacto esperado:** Cero funcional. Solo orden.
**Riesgo:** Mínimo (rename guiado por compilador).

---

### A3. Excepción `InvalidOperationException` en lugar de `Result.Failure` tras crear empresa

**Severidad:** 🟢 Baja
**Ubicación:** `CreateAccountCompanyCommandHandler` en `AccountCompanyAdministration.cs`.

#### Hallazgo
```csharp
var response = await companyRepository.FindOwnedByUserAsync(...)
    ?? throw new InvalidOperationException("Company response could not be resolved after creation.");
```
Si la lectura post-creación retorna `null` (race condition, falla de proyección), se lanza una excepción que escapa de la transacción ya cometida y termina en el middleware genérico como `500 Internal Server Error` sin metadatos de dominio.

#### Por qué importa
- Inconsistencia con el patrón `Result` adoptado en el resto del archivo.
- Pierde la oportunidad de mapear a `ProblemDetails` con un código de error de dominio rastreable.
- `AGENTS.md` §7.5 — usar `Result` y mapear adecuadamente a `ProblemDetails`.

#### Solución propuesta
1. Definir error de dominio en `AccountCompanyErrors`:
   ```csharp
   public static readonly Error PostProvisioningReadFailed =
       Error.Failure("AccountCompany.PostProvisioningReadFailed",
                     "The company was provisioned but could not be retrieved.");
   ```
2. Reemplazar la excepción por:
   ```csharp
   var response = await companyRepository.FindOwnedByUserAsync(...);
   if (response is null)
   {
       await transaction.RollbackAsync(cancellationToken);
       return Result<AccountCompanyDetailResponse>.Failure(
           AccountCompanyErrors.PostProvisioningReadFailed);
   }
   ```
3. Considerar si el rollback es viable o si la creación debe quedar (la excepción actual deja la fila creada y devuelve 500).

**Impacto esperado:** Errores trazables y auditables; comportamiento determinístico.
**Riesgo:** Bajo (camino infrecuente).

---

## Sección S — Seguridad

### S1. `[Authorize]` plano sin policy/RBAC explícito

**Severidad:** 🟡 Media
**Ubicación:** `AccountCompaniesController.cs` cabecera de clase.

#### Hallazgo
```csharp
[ApiController]
[Authorize]                       // ← solo "estás autenticado"
[Route("api/account/companies")]
```
La autorización real ocurre dentro del handler vía `OwnershipForbidden`. Cualquier usuario autenticado puede llegar al handler y disparar queries (DB hits) antes de ser rechazado. No hay defense-in-depth declarativa a nivel API.

#### Por qué importa
- **Enumeración de IDs**: combinado con S3, permite que un atacante use 404 vs 403 para mapear empresas existentes.
- **Costo en DB para requests no autorizadas**: cada intento dispara `ResolveCurrentUserAsync` + `FindByPublicIdAsync`.
- **`AGENTS.md` §4.4**: pide aplicar RBAC cuando aplique. Aquí solo hay ownership, no roles.

#### Solución propuesta
1. Definir una policy mínima en `AuthorizationPolicies`:
   ```csharp
   options.AddPolicy("AccountCompany.Manage", policy =>
       policy.RequireAuthenticatedUser()
             .RequireClaim("client_type", AuthClientType.Core.ToClaimValue()));
   ```
2. Aplicarla a nivel del controller:
   ```csharp
   [Authorize(Policy = "AccountCompany.Manage")]
   ```
3. Para endpoints administrativos (ej. `POST /`, `PUT /{id}`, `PATCH /archive`), evaluar agregar un requirement extra (`RequireRoleOrCreator`).
4. Documentar en ADR la decisión sobre cuándo se exige rol IAM `company-owner` y cuándo basta con creador.

**Impacto esperado:** Defense-in-depth declarativa; auditoría más fácil; menor costo en DB para requests rechazadas.
**Riesgo:** Medio — requiere alinear con la decisión de S2/S4.

---

### S2. Modelo de ownership atado al creador, divergente del modelo IAM

**Severidad:** 🟡 Media
**Ubicación:** `AccountCompanyActorResolver.ResolveOwnedCompanyAsync` y `CompanyRepository.BuildOwnedCompanyQuery`.

#### Hallazgo
```csharp
return company.CreatedByUserPublicId == ownerUserPublicId
    ? Result<Company>.Success(company)
    : Result<Company>.Failure(AccountCompanyErrors.OwnershipForbidden);
```
La definición de "dueño" es `Company.CreatedByUserPublicId`. Esto coexiste con el modelo IAM de roles (`company-owner`, `admin`, etc.), generando dos verdades sobre quién puede administrar la empresa.

#### Por qué importa
- Si el creador deja la organización, **nadie más** puede archivar/reactivar/editar la empresa desde este controller, aunque otro usuario tenga rol IAM `company-owner`.
- Riesgo de divergencia futura entre lo que muestra `/access-context` (basado en IAM) y lo que permite este controller (basado en creador).
- En la práctica, fuerza recovery manual cuando un creador se va.

#### Solución propuesta
1. Crear un servicio de dominio `IAccountCompanyManagementPolicy`:
   ```csharp
   public interface IAccountCompanyManagementPolicy
   {
       Task<bool> CanManageAsync(Guid userPublicId, Company company, CancellationToken ct);
   }
   ```
2. Implementación inicial: `CanManage = IsCreator(userPublicId) || HasIamRole(userPublicId, company, "company-owner")`.
3. Reemplazar la comparación directa en el helper por una llamada a la policy.
4. Documentar en `docs/decisions/ADR-XXXX.md` la regla canónica.

**Impacto esperado:** Modelo unificado; resilencia ante rotación de creadores; alineación con IAM.
**Riesgo:** Medio — cambio de comportamiento. Requiere tests de regresión y migración planificada.

---

### S3. Diferenciación entre `404 NotFound` y `403 Forbidden` permite enumeración

**Severidad:** 🟡 Baja-Media
**Ubicación:** `GetOwnedCompanyByIdQueryHandler` en `AccountCompanyAdministration.cs`.

#### Hallazgo
```csharp
var company = await companyRepository.FindOwnedByUserAsync(...);
if (company is not null) return Success(company);

var existingCompany = await companyRepository.FindByPublicIdAsync(...);
return existingCompany is null
    ? Failure(CompanyNotFound)         // 404
    : Failure(OwnershipForbidden);     // 403
```
Un atacante con un `companyPublicId` ajeno puede distinguir entre "no existe" y "existe pero no es tuya".

#### Por qué importa
- En multi-tenant, la práctica recomendada es **no revelar la existencia** de recursos ajenos.
- Aunque los GUIDs v4 son no-predecibles, IDs pueden filtrarse por logs, URLs compartidas, errores de cliente, etc.
- Realiza dos queries para responder un caso de error.

#### Solución propuesta
1. Unificar la respuesta a `404 NotFound` cuando el usuario no es dueño:
   ```csharp
   var company = await companyRepository.FindOwnedByUserAsync(...);
   return company is null
       ? Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyNotFound)
       : Result<AccountCompanyDetailResponse>.Success(company);
   ```
2. Para flujos donde el usuario **debe** saber que la empresa existe (ej. invitaciones, pantallas administrativas internas), mantener el 403 explícitamente y documentarlo.
3. Documentar la política en `docs/analysis/current-state/security-analysis.md`.

**Impacto esperado:** Cierra vector de enumeración; reduce una query por request.
**Riesgo:** Bajo. Cambia ligeramente UX en casos límite.

---

### S4. `access-context` y `role-builder-catalog` solo validan ownership por creador

**Severidad:** 🟡 Media
**Ubicación:** `GetOwnedCompanyAccessContextQueryHandler`, `GetOwnedCompanyRoleBuilderCatalogQueryHandler`.

#### Hallazgo
Estos endpoints exponen información sensible:
- Permisos del usuario en el tenant.
- Catálogo completo de permisos disponibles del tenant (administrativamente sensible).
- Plan, addons, capabilities, scopes.

Y solo validan `CreatedByUserPublicId`. Comparten la limitación de S2.

#### Por qué importa
- Si en el futuro hay co-administradores con rol IAM `Owner` pero no creador, no podrán cargar el bootstrap de la app ni administrar roles, aunque IAM diga lo contrario.
- El catálogo de permisos no debería depender solo de quién creó la fila; debería depender del rol activo en el tenant.

#### Solución propuesta
- **Corto plazo:** marcar dependencia explícita con S2; resolverlo cuando se implemente la policy unificada.
- **Largo plazo (post-S2):** estos handlers usan la nueva `IAccountCompanyManagementPolicy.CanReadAdministrativeContextAsync`, que combina creador + rol IAM.

**Impacto esperado:** Sin cambios funcionales hoy; preparación para evolución del modelo de ownership.
**Riesgo:** Depende de la cadencia con la que se aborde S2.

---

### S5. Falta auditoría en queries que exponen información sensible

**Severidad:** 🟢 Baja
**Ubicación:** `GetOwnedCompanyAccessContextQueryHandler`, `GetOwnedCompanyRoleBuilderCatalogQueryHandler`, `GetOwnedCompanyResourcePolicyQueryHandler`.

#### Hallazgo
Estos endpoints exponen permisos, plan y scope del usuario, pero **no se auditan**. Solo se auditan los commands.

#### Por qué importa
- `AGENTS.md` §4.4: trazabilidad cuando el flujo lo requiera.
- Lecturas masivas o anómalas (ej. enumeración de catálogos) pasan desapercibidas.
- En postmortems de incidentes de seguridad, no hay evidencia de quién leyó qué.

#### Solución propuesta
1. Definir nuevos `AuditEventTypes`:
   - `AccessContextRead`
   - `RoleBuilderCatalogRead`
   - `ResourcePolicyRead`
2. Loguear con nivel `Information` en `auditService.LogForTenantAsync` al final del query exitoso, sin payload (solo evento + actor + tenant).
3. Definir retención corta para estos eventos (alta cardinalidad).

**Impacto esperado:** Trazabilidad de lecturas administrativas.
**Riesgo:** Bajo — observabilidad pura.

---

### S6. Sin rate limiting visible en `POST /{id}/switch`

**Severidad:** 🟡 Media
**Ubicación:** `AccountCompaniesController.Switch`.

#### Hallazgo
`Switch` emite un nuevo `accessToken` + `refreshToken` y persiste un `RefreshToken`, además de log de auditoría. No se aprecia rate limiting específico.

#### Por qué importa
- Un cliente comprometido podría rotar tokens en bucle, generando:
  - Crecimiento sostenido de `RefreshToken`.
  - Volumen alto de `AuditLog` "ActiveCompanySwitched".
  - Carga adicional sobre IAM/Subscription en cada switch.
- `AGENTS.md` §8: aplicar rate limiting cuando el flujo es costoso o sensible.

#### Solución propuesta
1. Definir una policy `SwitchActiveCompany` en el rate limiter global (.NET 8/9 `RateLimiter`):
   - Sliding window 5 requests / 60 s por usuario.
2. Aplicar en el endpoint:
   ```csharp
   [EnableRateLimiting("SwitchActiveCompany")]
   [HttpPost("{companyPublicId:guid}/switch")]
   ```
3. Considerar también `Create` (provisioning costoso).

**Impacto esperado:** Mitigación de abuso y de impacto en DB.
**Riesgo:** Bajo. Configurable por entorno.

---

## Sección P — Performance

### P1. Cuatro sub-selects redundantes para resolver metadata de `CompanyType`

**Severidad:** 🟡 Media
**Ubicación:** `CompanyRepository.BuildOwnedCompanyQuery`.

#### Hallazgo
```csharp
CompanyTypeId       = dbContext.CompanyTypeCatalogItems.Where(...).Select(...).FirstOrDefault(),
CompanyTypeCode     = dbContext.CompanyTypeCatalogItems.Where(...).Select(...).FirstOrDefault(),
CompanyTypeName     = dbContext.CompanyTypeCatalogItems.Where(...).Select(...).FirstOrDefault(),
CompanyTypeIsActive = dbContext.CompanyTypeCatalogItems.Where(...).Select(...).FirstOrDefault(),
```
Cada propiedad se traduce a un sub-select separado contra la misma tabla y misma fila. EF puede consolidar parcialmente, pero el patrón se replica por cada fila resultante de la query principal.

#### Por qué importa
- En `GET /` con `pageSize=100`, es **400 sub-selects** potenciales contra `CompanyTypeCatalogItems`.
- Crece linealmente con el tamaño de página.
- `AGENTS.md` §4.5: evitar full scans evitables y consultas sin estrategia.

#### Solución propuesta
1. Consolidar los cuatro selectores en uno usando objeto anónimo:
   ```csharp
   CompanyType = dbContext.CompanyTypeCatalogItems
       .AsNoTracking()
       .Where(item => item.Id == company.CompanyTypeCatalogItemId)
       .Select(item => new
       {
           item.PublicId,
           item.Code,
           item.Name,
           item.IsActive
       })
       .FirstOrDefault()
   ```
2. Adaptar la proyección posterior y el `OwnedCompanyProjection`.
3. Validar el SQL generado con un log de EF tras el cambio.

**Impacto esperado:** Reducción de hasta 75% en sub-selects de la query principal.
**Riesgo:** Bajo. Cambio interno del repositorio.

---

### P2. Sub-select de `PlanCode` ejecutado por fila en listado paginado

**Severidad:** 🟡 Media
**Ubicación:** `CompanyRepository.BuildOwnedCompanyQuery`.

#### Hallazgo
```csharp
PlanCode = dbContext.CompanySubscriptions
    .Where(subscription => subscription.CompanyId == company.Id &&
                          (subscription.Status == SubscriptionStatus.Active ||
                           subscription.Status == SubscriptionStatus.Trial))
    .OrderByDescending(subscription => subscription.StartDateUtc)
    .Select(subscription => subscription.PlanCode)
    .FirstOrDefault() ?? string.Empty,
```
Sub-select por cada empresa en la lista. En `GET /` con `pageSize=100`, son 100 sub-selects sobre `CompanySubscriptions`.

#### Por qué importa
- Costo crece con `pageSize`.
- Si EF no lo materializa como `OUTER APPLY` con índice cubriente, se convierte en un cuello de botella claro.
- Historicamente, este patrón es donde aparecen N+1 disfrazados de "una sola query".

#### Solución propuesta
1. **Verificar primero** el SQL real con logging de EF y un dataset realista.
2. Si el plan es subóptimo:
   - Materializar `IDs` de companies primero, luego un único `Where(IN)` sobre `CompanySubscriptions` y unir en memoria.
   - O agregar un índice cubriente: `(CompanyId, Status, StartDateUtc DESC) INCLUDE (PlanCode)`.
3. Para volumen alto en el futuro, considerar denormalizar `PlanCode` en `Company` con eventos de actualización.

**Impacto esperado:** Reducción medible en p99 del listado.
**Riesgo:** Medio — requiere medición previa para no optimizar prematuramente.

---

### P3. Doble lectura `FindOwnedByUserAsync` por command para snapshots `Before/After`

**Severidad:** 🟡 Media
**Ubicación:** `Update`, `Archive`, `Reactivate` handlers.

#### Hallazgo
```csharp
var before = await companyRepository.FindOwnedByUserAsync(...);  // 1ª lectura completa
// ... mutate ...
var after  = await companyRepository.FindOwnedByUserAsync(...);  // 2ª lectura completa
```
Cada llamada arrastra la query completa con sub-selects de legal-representatives y company-type.

#### Por qué importa
- Multiplica por 2 el costo de cada command.
- En `Archive` y `Reactivate`, los datos que cambian son mínimos (solo `Status`/`ModifiedUtc`); cargar legal-representatives en `Before` es desperdicio.
- `AGENTS.md` §4.5: evitar carga innecesaria.

#### Solución propuesta
1. Construir el snapshot `Before` desde el agregado ya cargado:
   ```csharp
   var before = AccountCompanyDetailMapper.FromDomain(company, ...);
   ```
2. Para los legal-representatives en el snapshot, evaluar si realmente se requieren en cada audit (ej. en `Archive` no cambian → omitirlos del before).
3. Alternativa: introducir un DTO ligero `AccountCompanyAuditSnapshot` que contenga solo campos relevantes para auditar.

**Impacto esperado:** Hasta −50% de queries por command de mutación.
**Riesgo:** Bajo si se cubre con tests existentes.

---

### P4. `CountOwnedByUserInternalAsync` materializa lista en memoria innecesariamente

**Severidad:** 🟢 Baja
**Ubicación:** `CompanyRepository.CountOwnedByUserInternalAsync`.

#### Hallazgo
```csharp
var ownedStatuses = await dbContext.Companies
    .AsNoTracking()
    .Where(company => company.CreatedByUserPublicId == ownerUserPublicId)
    .Select(company => company.Status)
    .ToListAsync(cancellationToken);

return ownedStatuses.Count(statuses.Contains);
```
Trae todos los `Status` a memoria y luego filtra en C#.

#### Por qué importa
- Si un usuario tiene N empresas, materializa N enums.
- Aunque hoy N es bajo, el patrón se replica fácil y degrada.
- `AGENTS.md` §4.5.

#### Solución propuesta
```csharp
var statuses = filter.Statuses;
if (statuses.Length == 0)
{
    return 0;
}

return await dbContext.Companies
    .AsNoTracking()
    .Where(company => company.CreatedByUserPublicId == ownerUserPublicId
                   && statuses.Contains(company.Status))
    .CountAsync(cancellationToken);
```

**Impacto esperado:** `COUNT(*)` puro en DB, sin transferir filas.
**Riesgo:** Mínimo.

---

### P5. `includeAllowedActions=true` puede esconder N+1 en `ResourceActionPolicyService`

**Severidad:** 🟡 Media (depende de implementación)
**Ubicación:** `GetOwnedCompaniesQueryHandler` (rama `IncludeAllowedActions`).

#### Hallazgo
Cuando el cliente envía `includeAllowedActions=true`, por cada item de la página se invoca:
```csharp
resourceActionPolicyService.Evaluate(new ResourceActionContext(...))
```
Si `Evaluate` es CPU-only, OK. Si toca DB/cache, hay un N+1 oculto.

#### Por qué importa
- Riesgo latente: cualquier evolución futura del servicio puede degradar este endpoint sin que se detecte.
- `AGENTS.md` §4.5.

#### Solución propuesta
1. **Verificar** la implementación de `IResourceActionPolicyService.Evaluate`. Si es puro (no I/O), dejar nota en código y unit test que lo declare:
   ```csharp
   // PERFORMANCE CONTRACT: Evaluate must be pure (no I/O).
   ```
2. Si toca DB:
   - Cargar todos los inputs requeridos en un solo round-trip antes del bucle.
   - Pasar al servicio un `IReadOnlyDictionary<...>` precalculado.
3. Agregar test que verifique el número de queries ejecutadas para el endpoint con `pageSize=N` con `includeAllowedActions=true` (ej. usando interceptor de `DbCommand`).

**Impacto esperado:** Garantía explícita del contrato; protección ante regresiones.
**Riesgo:** Bajo.

---

### P6. `Switch` ejecuta doble lectura del modelo IAM (JWT + access context)

**Severidad:** 🟡 Media
**Ubicación:** `SwitchActiveCompanyCommandHandler` + `JwtTokenService.CreateIdentityClaimsAsync` + `AccountCompanyAccessContextBuilder.BuildAsync`.

#### Hallazgo
En el flujo de `Switch`:
1. `tokenService.GenerateForTenantAsync` ejecuta `iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(includeRoles: true)` para construir claims de roles+permisos.
2. Inmediatamente después, `AccountCompanyAccessContextBuilder.BuildAsync` vuelve a leer IAM (entre otras cosas) para construir el contexto.

Las dos lecturas comparten el dato base (roles + permisos del usuario en el tenant).

#### Por qué importa
- Switch es una operación frecuente para usuarios multi-tenant.
- Doble round-trip a IAM (incluye includes profundos).
- `AGENTS.md` §4.5.

#### Solución propuesta
1. Extraer un único `IAccountCompanyAccessLoader` que cargue **una sola vez**:
   - User IAM con roles + permission assignments.
   - Subscription + plan + addons.
2. `JwtTokenService` recibe el resultado vía un parámetro opcional para no forzar re-lectura cuando se le pasan claims pre-resueltos.
3. `AccessContextBuilder` consume el mismo objeto.

**Impacto esperado:** −1 query pesada en cada Switch.
**Riesgo:** Medio — requiere alineación con `JwtTokenService`. Mantener compatibilidad con flujos donde el contexto no está pre-cargado (Login).

---

## Priorización sugerida

### Quick wins (alto valor, bajo riesgo)
| # | Hallazgo | Razón |
|---|---|---|
| P1 | Consolidar sub-selects de `CompanyType` | Cambio mecánico, gana en cada listado |
| P4 | `CountAsync` con `Contains` | Cambio de 4 líneas |
| A3 | Reemplazar excepción por `Result.Failure` | Consistencia con patrón del proyecto |
| S3 | Unificar respuesta a 404 en `GET /{id}` | Mejora postura de seguridad sin cambios estructurales |

### Medium term
| # | Hallazgo | Razón |
|---|---|---|
| P3 | Snapshot `Before` desde agregado | Reduce 50% de queries en commands |
| P2 | Investigar y/o indexar `PlanCode` sub-select | Requiere medición previa |
| S5 | Auditar queries sensibles | Observabilidad |
| S6 | Rate limit en `Switch` (y `Create`) | Defensa básica contra abuso |
| A2 | Mover DTOs a `Contracts/` | Consistencia |

### Strategic
| # | Hallazgo | Razón |
|---|---|---|
| S2 + S4 | Unificar policy de management (creador + IAM) | Cambio de modelo, requiere ADR |
| S1 | Policies y RBAC declarativos | Depende de S2 |
| P5 | Garantizar contrato puro de `Evaluate` o eliminar N+1 | Requiere inspección dedicada |
| P6 | Unificar carga IAM en `Switch` | Refactor cross-componente |
| A1 | Pipeline behavior de ownership | Refactor amplio, alto valor a futuro |

---

## Anexo — Convención para futuras auditorías por controlador

Este documento sigue el patrón:
- Una sola fuente de hallazgos por controlador en `docs/analysis/<NombreDelController>/findings-and-remediations.md`.
- Cada hallazgo tiene: ubicación, severidad, descripción, impacto y solución propuesta concreta.
- Los hallazgos generales del sistema se consolidan en los documentos vivos de `docs/analysis/current-state/` (no aquí).
- Cuando un hallazgo se accione, mover a `docs/analysis/changes/HU-XXXX.md` y dejar referencia desde aquí.
