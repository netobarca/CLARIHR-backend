# Definiciones técnicas del backend CLARIHR

Catálogo de las reglas técnicas vigentes del proyecto: naming, identidad, persistencia, contrato de API,
errores, multi-tenancy, autorización, rendimiento, guardrails y comportamientos de plataforma.

Convención de marcado: 🔒 obligatorio · ⚙️ convención · ⚠️ comportamiento no obvio.

---

## 1. Convenciones de nombres

### 1.1 Organización de archivos

⚙️ Patrón *administration file*: un archivo por superficie de caso de uso, con orden fijo interno
**DTOs → Queries → Commands → Validators → Handlers**.

```
Application/Features/<Modulo>/
  <Entidad>Administration.cs
  <Subárea>/<Entidad>Administration.cs
  Common/<Modulo>Policies.cs            ← nombres de política de autorización
  Common/<Modulo>PermissionCodes.cs     ← códigos RBAC + ResourceKey
  Common/<Modulo>RateLimitPolicies.cs   ← nombres de políticas de rate limit
  Common/<Modulo>ValidationRules.cs     ← MaxPageSize, DefaultPageSize, MinSearchLength, regex

Application/Abstractions/<Modulo>/      ← interfaces de repositorio y servicios
Domain/<Modulo>/                        ← entidades e invariantes
Infrastructure/<Modulo>/                ← repositorios e implementaciones
Infrastructure/Persistence/Configurations/<Modulo>/
Api/Controllers/<Entidad>Controller.cs
```

🔒 El nombre del módulo es **idéntico en las cuatro capas** (`Domain.Payroll`, `Application.Features.Payroll`, `Application.Abstractions.Payroll`, `Infrastructure.Payroll`, `Persistence/Configurations/Payroll`).

### 1.2 Tipos

| Elemento | Convención | Ejemplo |
|---|---|---|
| Query | `<Verbo><Entidad>Query` | `SearchCostCentersQuery`, `GetCostCenterByIdQuery` |
| Command | `<Verbo><Entidad>Command` | `CreateCostCenterCommand`, `InactivateCostCenterCommand` |
| Handler | `<Query\|Command>Handler` | `SearchCostCentersQueryHandler` |
| Validator | `<Query\|Command>Validator`, `internal sealed` | `SearchCostCentersQueryValidator` |
| DTO de respuesta | `<Entidad>Response` · `<Entidad>ListItemResponse` · `<Entidad>ExportRow` · `<Entidad>UsageResponse` | — |
| Entidad de dominio | Sustantivo singular | `CostCenter` |
| Repositorio | `I<Entidad>Repository` | — |
| Servicio de autorización | `<Modulo>AuthorizationService` con métodos `EnsureCan<Acción>Async` | — |
| Política de autorización | `"<Modulo>.Read"` / `"<Modulo>.Manage"` | `CostCenterPolicies.Read` |
| Estado intermedio de PATCH | `<Entidad>PatchState` | — |

⚙️ `sealed` por defecto en records y clases concretas. DTOs = `record` posicional inmutable.

#### Tipo de fecha: día vs instante 🔒

El tipo lo decide **lo que dice el negocio**, no la comodidad del serializador.

| Lo que dice el negocio | Dominio | Columna | En el wire |
|---|---|---|---|
| «el **día** en que pasó» — `hireDate`, `birthDate`, `effectiveFrom`, `startDate` | `DateOnly` | `date` | `"2026-08-15"` |
| «el **momento** en que pasó» — `createdUtc`, `annulledUtc`, `expirationUtc` | `DateTime` (UTC) | `timestamptz` | `"2026-08-15T18:07:00Z"` |

⚠️ **El sufijo `Utc` es para instantes.** En un campo de día es la señal de que se modeló mal: un día no tiene zona horaria que fijar, así que fijarla en UTC no resuelve una ambigüedad — la inventa.

⚠️ **Guardar un día como instante corre el día.** Con la fecha modelada como `DateTime`, la frontera convierte un cuerpo con offset (`ToUniversalTime`) y el día salta en las últimas horas del día local: desde El Salvador (−06:00), `2026-08-15T18:07:00-06:00` se almacena como **16 de agosto**. Con `DateOnly` no puede pasar: `LenientDateOnlyJsonConverter` lee el día **como está escrito** y nunca lo desplaza.

⚙️ Para convertir un campo, **borrar el tipo y dejar que el compilador enumere**: no hay conversión implícita `DateOnly`↔`DateTime`, así que ningún cruce queda silencioso.

### 1.3 Tests

🔒 El nombre del método empieza por la sección: **`<Seccion>_<LoQueHace>`**
(`OrgUnits_Create_ShouldReject_WhenTypeIsMissing`). Permite filtrar por prefijo sin anotar nada.
⚠️ El filtro de `dotnet test` es por **nombre cualificado (clase.método)**, no por archivo: un test dentro de una `partial class` no se encuentra por el nombre del archivo.

### 1.4 Ramas y commits

⚙️ `<tipo>/<dominio>/<slug-kebab>` con `<tipo> ∈ feat|fix|perf|refactor|chore|docs`.
🔒 Sin trailer de coautoría de IA en commits ni en cuerpos de PR.

---

## 2. Identidad y contrato de datos

### 2.1 Id interno vs PublicId 🔒

- Toda entidad persistida conserva `Id` interno **`BIGINT`** para PK, FK, joins, EF e índices.
- Toda entidad persistida expone además `PublicId` **`Guid` persistido** (`public_id`).
- **Ningún request, response, exportación o contrato público expone `id` ni `internalId`.**
- Hacia afuera el nombre es siempre `publicId` o `<entidad>PublicId`.
- La resolución por `PublicId` → `Id` interno ocurre **solo dentro de Application/Infrastructure**.
- Seeds y catálogos globales usan `PublicId` **determinístico**: `Entity.CreateDeterministicPublicId(seed)` (MD5 del seed trimmeado).

🔒 **En C# el parámetro/propiedad se llama `*Id` (tipo `Guid`), nunca `*PublicId`.** La reescritura al wire es automática (§4.7).

### 2.2 Jerarquía de entidades base

```
Entity                      → Id (long), PublicId (Guid), CreateDeterministicPublicId(seed)
  AggregateRoot
    AuditableEntity         → CreatedUtc, ModifiedUtc, MarkCreated(), MarkModified()
      TenantEntity          → TenantId, SetTenantId()  [ITenantScopedEntity]
      SystemScopedCatalogItem   → Code, NormalizedCode, Name, NormalizedName, IsActive, SortOrder
      CountryScopedCatalogItem  → + CountryCatalogItemId, CountryCode
```

⚙️ `SortOrder` no admite negativos (lanza en el constructor). `ConcurrencyToken` se inicializa con `Guid.NewGuid()` en el constructor.

### 2.3 Código de negocio

⚙️ Si un recurso tiene código de negocio, el contrato público incluye `code` **y** `normalizedCode`, ambos publicados en **UPPERCASE**.

---

## 3. Persistencia

🔒
- Toda entidad con actualización optimista declara `.IsConcurrencyToken()` sobre `ConcurrencyToken` (Guid).
- Índices compuestos **tenant-first**: `(TenantId, …)`.
- Índices únicos incluyen siempre el tenant y, en catálogos por país, el país.
- Índices únicos **parciales** cuando el dominio lo exija (p. ej. "una sola aplicación activa por ítem").
- Filtro global de query (`HasQueryFilter`) sobre `ITenantScopedEntity`.
- `IgnoreQueryFilters` solo donde esté justificado y **gobernado por guardrail**.
- Transacciones cortas. Trabajo pesado fuera del request path.
- Columna según el tipo de fecha (§1.2): día → `date`, instante → `timestamptz`. **Nunca un día en `timestamptz`.**

⚠️ Trampas verificadas:
- **Migrar `timestamptz` → `date` sin anclar la zona retrocede las fechas un día.** El cast se resuelve con el `TimeZone` de la **SESIÓN**, no con UTC. Medido contra `clarihr_dev`: con `TimeZone='America/El_Salvador'`, `('2026-12-01 00:00:00+00'::timestamptz)::date` → **`2026-11-30`**. EF genera el cast desnudo, así que hay que escribirlo a mano:
  `ALTER COLUMN x TYPE date USING (x AT TIME ZONE 'UTC')::date`.
- **Un `timestamptz` leído como fecha en consulta usa el `TimeZone` de la sesión.** Cualquier informe que agrupe por día puede correr un día según qué sesión lo ejecute. Con `date` en la columna el riesgo desaparece.
- `AsNoTracking()` **antes** de `SaveChanges` en métodos `Add*` rompe el guardado (estuvo replicado en 11 repositorios).
- EF no traduce **funciones locales** dentro de una expresión de consulta.
- Fragmentos de GUID mal copiados en un `Designer.cs` de migración pasan el build y fallan en runtime.
- Un índice único demasiado estrecho en catálogo por país hace que el seeding fresco choque consigo mismo.

---

## 4. Contrato de API

### 4.1 Rutas y versionado 🔒

- Prefijo **`api/v1/...`** (no `/v1/...`). Ruta declarada: `[Route("api/v{version:apiVersion}")]`.
- Backoffice/plataforma: `api/platform/*`, host y `openapi` propios.
- Colecciones hijas bajo el padre cuando la operación depende del contexto del padre: `companies/{companyId}/cost-centers`.
- Si el hijo ya es direccionable por `publicId` y expone acciones propias → **ruta plana**: `cost-centers/{id}/usage`.
- 🔒 Prohibido el patrón híbrido `/parent-resource/child-resource/{childPublicId}/...`.

⚠️ Dos parámetros `Guid` "pelados" homónimos en una ruta producen `RoutePatternException` **en el arranque**: nombrarlos distinto y anidados. En `CreatedAtAction` los valores de ruta deben usar las claves **ya reescritas** (`xxxPublicId`), no los nombres C#.

### 4.2 Anotaciones obligatorias 🔒

Por controller:
```csharp
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("<Familia>")]
[AuthorizationPolicySet(<Modulo>Policies.Read, <Modulo>Policies.Manage)]
[ResourceActions(<Modulo>PermissionCodes.ResourceKey)]
public sealed class <X>Controller(ICommandDispatcher …, IQueryDispatcher …) : ControllerBase
```

Por endpoint: `[ProducesResponseType<T>]` · `[ProducesStandardErrors(StandardErrorSet.<Query|Read|Write>)]` · `[SwaggerOperation(Summary, Description)]` · `[EnableRateLimiting]` en los costosos.

⚠️ Un controller con `[ResourceActions]` exige que **cada DTO de PUT/PATCH** implemente `ISupportsAllowedActions`.

### 4.3 Paginación 🔒

Todo listado: `page`, `pageSize` con `[Range(1, <Modulo>ValidationRules.MaxPageSize)]`.
**El máximo del `[Range]` debe ser idéntico al máximo del validator.** Respuesta `PagedResponse<T>`.

### 4.4 Concurrencia optimista 🔒

- Se transporta por header **`If-Match`** → `[FromIfMatch] Guid concurrencyToken` (binder dedicado). **No** va en el body.
- El token vigente viaja en el body del GET; el nuevo se devuelve en el header **`ETag`** de create/update.
- **`If-Match` ausente → `400`** (validación, key `If-Match`). **Token stale → `409`** (`CONCURRENCY_CONFLICT`).
- ⚠️ **No existe `412` ni `428`** en ninguna parte del contrato.
- ⚙️ `*PatchState` **no** transporta el token: la concurrencia vive exclusivamente en el command / `If-Match` (ADR-0003).

### 4.5 Verbos y formatos

- **PATCH = RFC 6902 JSON Patch**, `Content-Type: application/json-patch+json`.
- **DELETE devuelve `{ parentConcurrencyToken }`** (body + `ETag`) = token refrescado del padre. Físico vs. lógico varía por módulo y se documenta por endpoint.
- Enums se serializan como **string** (`JsonStringEnumConverter`).
- ⚠️ En records posicionales, `[property:Required]` provoca **500s**: usar `[Required]` con *param target*.
- ⚠️ Los exports en JSON salen en **PascalCase**, no camelCase.

🔒 **Las fechas se normalizan en la frontera, y la frontera tiene TRES puertas.** Cada una necesita su normalización propia; olvidar una devuelve la fecha con un `Kind` que `timestamptz` rechaza (`500`) o con el día corrido:

| Puerta | Quién normaliza |
|---|---|
| Cuerpo JSON | `UtcDateTimeJsonConverter` (instantes) · `LenientDateOnlyJsonConverter` (días) |
| Query / route / form | `UtcDateTimeModelBinder` — el serializador no los toca |
| **Cuerpo de JSON Patch** | El *patch applier*: recibe un `JsonElement`, no un `DateTime`, así que **ningún converter se le aplica** |

⚠️ `JsonElement.TryGetDateTime()` **no es la frontera**: devuelve `Kind=Unspecified` si el texto no trae zona y `Kind=Local` si trae offset — y para un campo de día, el offset además corre el día. Los *patch appliers* leen con `CalendarDateReader`, nunca con `TryGetDateTime` en crudo.

### 4.6 `allowedActions` y autoservicio

- `includeAllowedActions=true` devuelve por ítem: `canEdit`, `canDelete`, `canArchive`, `canActivate`, `canInactivate`, `canView`, `canCreate`, `canSubmit`, `canApprove`, `canReject`, `canCancel`, `canPublish`, `canFinalize`, `reasons`, `actionPermissions`.
- 🔒 **En listados, `allowedActions` deriva únicamente del permiso del usuario**, nunca del estado de dependencia. El bloqueo real se enforcea server-side en el handler (ADR-0001).
- 🔒 Las capacidades anunciadas nunca pueden exceder lo que el gate real enforce: los códigos del registro espejan los del gate.
- ⚙️ **El autoservicio se resuelve server-side**, comparando el usuario vinculado del expediente (`LinkedUserPublicId`) con el caller. El frontend no lo decide por permiso.

### 4.7 Reescritura automática del contrato público ⚠️

`PublicContractJsonTypeInfoResolver` + `PublicContractNaming` + convención de rutas + filtros de Swagger reescriben, **en la serialización y en el openapi generado**:

| En C# | En el wire |
|---|---|
| `Guid Xxx**Id**` | `xxx**PublicId**` |
| `Guid[] XxxIds` | `xxxPublicIds` |
| `Guid Id` | `publicId` |
| `*InternalId` | **suprimido** |
| propiedad llamada exactamente `Code` / `NormalizedCode` (string) | valor `Trim()+ToUpperInvariant()`, más `normalizedCode` sintético |
| resto | camelCase plano |

⚠️ Que el openapi muestre `…PublicId` mientras el C# dice `…Id` **no es drift**: es la reescritura funcionando.
⚠️ `XxxCode` (p. ej. `PositionSlotCode`) **no** matchea la regla de normalización: su valor pasa verbatim.

### 4.8 Emisión de tokens ⚠️

El access token de `/auth/login` se genera con `includeAuthorizationClaims: false`: trae `sub`/`email`/`tid`/`user_status` pero **cero claims de rol y permiso**. Los policy-sets evalúan claims y devuelven **403** antes del fallback a BD. Solo `RefreshAsync`, `GenerateAsync` (confirmar correo) y `GenerateForTenantAsync` (aceptar invitación) usan `includeAuthorizationClaims: true`.
**Consecuencia operativa: `login → /auth/refresh` antes de llamar endpoints protegidos.**

---

## 5. Errores

### 5.1 Modelo

```csharp
public sealed record Error(
    string Code, string Message, ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    IReadOnlyCollection<ErrorDetail>? Details = null,
    IReadOnlyList<object?>? MessageArguments = null,
    IReadOnlyDictionary<string, object?>? Extensions = null);
```

`ErrorType` ∈ `Validation(1), UnprocessableEntity(2), Unauthorized(3), Forbidden(4), NotFound(5), Conflict(6), TooManyRequests(7), Failure(8), Unexpected(9), PayloadTooLarge(10), Gone(11), ServiceUnavailable(12), MethodNotAllowed(13)`.

`Result` / `Result<T>`: un resultado exitoso no puede portar error y uno fallido no puede carecer de él (se valida en el constructor). `Result<T>.Value` lanza si el resultado es fallido.

⚙️ `Extensions` es el payload estructurado y accionable (p. ej. el desglose que alimenta un diálogo de confirmación). No puede ir en el mensaje porque el localizador **reemplaza** el `detail` por el texto catalogado.

### 5.2 Mapa de estados

| Situación | HTTP |
|---|---|
| Éxito | `200` / `201` |
| Validación de request/binding | `400` |
| No autenticado | `401` |
| No autorizado | `403` |
| No encontrado / no visible por política de tenant | `404` |
| Método no soportado | `405` |
| Conflicto de estado, duplicado, concurrencia | `409` |
| Regla de negocio (`ErrorType.UnprocessableEntity`) | `422` |
| Payload demasiado grande | `413` |
| Rate limit | `429` |
| Inesperado | `500` |

⚙️ **`409` es la convención app-wide para conflictos de regla de negocio** (`AGENTS.md §17.6`: no migrar a `422`); `CONCURRENCY_CONFLICT` identifica específicamente la concurrencia.
⚙️ Formas de `code` en uso: `área.snake_case` (`auth.forbidden`, `common.not_found`, `common.validation`, `common.conflict`, `common.unexpected`) y `SCREAMING_SNAKE` para códigos de negocio (`CONCURRENCY_CONFLICT`, `TENANT_MISMATCH`, `INDEBTEDNESS_LIMIT_EXCEEDED`).

### 5.3 Forma en el wire ⚠️

- `ProblemDetails.Extensions` está marcado `[JsonExtensionData]`: **en el wire NO existe un objeto `extensions`**. `code`, `traceId` y cualquier campo agregado por un handler son **miembros RAÍZ** del JSON. Leer `extensions.code` produce `KeyNotFound`; se lee **`code` en la raíz**.
- `type` es siempre `https://httpstatuses.com/{status}`, **nunca** el `code`.
- Los errores de campo van bajo `errors`.

🔒 Errores de negocio por **excepción tipada + enum** o por `Result`; nunca por match sobre el mensaje. Sin stack traces ni datos internos hacia afuera.

---

## 6. Multi-tenancy

🔒
1. El `TenantId` proviene del claim **`tid`** del JWT.
2. ⚠️ **`MapInboundClaims` está activo por defecto** y renombra `tid` → `http://schemas.microsoft.com/identity/claims/tenantid`. Un componente que lea literalmente `"tid"` sale temprano y deja al usuario sin permisos → **403 en toda petición con tenant**. Fuente única obligatoria: `TenantClaimTypes` (en `Application.Abstractions.Tenancy`), que contempla las tres formas. Prohibido leer el string `"tid"` a mano.
3. Toda lectura y escritura está acotada al tenant activo: filtro global **más** validación de pertenencia a nivel de aplicación.
4. Tablas, índices y consultas contemplan siempre el tenant.
5. ⚙️ Regla de exposición: cuando la seguridad lo exija, la respuesta no revela que un recurso existe fuera del tenant.
6. ⚙️ **Los catálogos de tipo son system-scoped (globales), no tenant-scoped.** Su caché global **no** es violación de tenant-scope — decisión zanjada.

---

## 7. Autorización y seguridad

### 7.1 Autorización fuera del JWT 🔒

- El token prueba **identidad**: `sub`, `tid`, `language`.
- Roles y permisos se resuelven **por request** desde la base: `IEffectiveAccessResolver` + `IMemoryCache` con **TTL 60 s**.
- `EffectiveAccessClaimsTransformation` (`IClaimsTransformation`) inyecta el acceso efectivo, para no tocar los servicios que leen `ICurrentUserService.Permissions`.
- Invalidación estructural vía `EffectiveAccessInvalidationInterceptor` (`SaveChangesInterceptor`).
- Efecto: token de ~5.4 KB → ~700 bytes.

🔒 **Partición obligatoria de la interfaz:** `IEffectiveAccessInvalidator` (invalidación) lo implementa un **singleton que solo depende de `IMemoryCache`**; `IEffectiveAccessResolver` (consulta) es **scoped** con el `DbContext`. El interceptor depende solo del invalidador.
⚠️ Sin esa partición se forma el ciclo `DbContext → DbContextOptions (lambda de AddDbContext) → interceptor → resolver → DbContext`. MS DI **no lo detecta** (cruza el lambda, opaco al análisis de call sites) y **no desborda la pila** (`StackGuard` traslada la continuación a otro hilo del pool, que espera el lock que el primero sostiene): **deadlock a 0 % CPU, sin excepción, sin log**. `ValidateOnBuild = true` **pasa con el ciclo presente**.
⚠️ `IMemoryCache` es por instancia: en granja el techo de revocación es el TTL de 60 s. Revocación inmediata multi-instancia exigiría Redis.

### 7.2 RBAC nivel 3

- Validación por **rol → módulo → acción**, más **permisos de campo** donde aplique.
- **Dos capas de enforcement**: política declarativa en el borde (`[AuthorizationPolicySet]`) + gate `EnsureCan*` en el handler.
- ⚙️ Convención de la política: **Read → GET/HEAD, Manage → el resto**.
- 🔒 **La política declarativa debe ser superset (⊇) del gate del handler.** Si es más estricta, un usuario legítimo recibe un **403 falso**. ⚠️ **Ningún guardrail lo verifica**: es punto de revisión **manual**.
- ⚙️ Campos sensibles (salariales/compensación) detrás de **gate a nivel de campo**, no solo de endpoint.
- ⚙️ **Anti-self**: quien registra o solicita no autoriza. En flujos críticos el gate es doble o triple (solicitante ≠ autorizador ≠ aplicador).
- ⚠️ Las exclusiones de rol (p. ej. "Admin no puede autorizar") viven **en la política**, no dentro del handler.
- ⚠️ Los policy-sets declarados **class-only** no admiten records: verificar antes de modelar el DTO.

### 7.3 Superficie anónima 🔒

- Un handler `[AllowAnonymous]` **no** puede inyectar el servicio de auditoría tenant-scoped: `AuditService.LogAsync` lanza si `TenantId` es null y sin JWT no hay `tid` → **500 determinista**.
  Alternativas: `LogForTenantAsync(<tenantPublicId>, …)` o `IPlatformAuditService`.
- La auditoría **nunca** corre dentro de la transacción de negocio si su fallo puede revertirla (un `LogAsync` posterior al `SaveChangesAsync` dentro de la transacción hizo rollback de una activación completa).
- El envío de correo tampoco va dentro de la transacción.

### 7.4 Línea base

- HTTPS obligatorio en producción; configuración correcta detrás de reverse proxy.
- Refresh tokens con rotación, revocación y almacenamiento seguro.
- **Rate limiting** en endpoints sensibles y costosos (export, search, graph), **particionado por user/tenant**.
- Lockout ante intentos fallidos; **respuestas genéricas** para evitar enumeración de usuarios.
- **Cap de nodos** en operaciones que expanden grafos (anti-amplificación).
- Logs de input **acotados en longitud**.
- Nunca registrar secretos, contraseñas ni tokens. Minimizar PII en logs y respuestas.
- Secretos por variables de entorno o `user-secrets`; `appsettings.Development.json` no se versiona (existe plantilla `.example`).
- **Auditoría mínima**: login/logout, cambios de permisos, aprobaciones, cambios salariales, ejecuciones críticas, exportaciones, acciones administrativas.

---

## 8. Rendimiento

🔒
1. **Ningún endpoint de listado sin paginación.**
2. **`AsNoTracking()` por defecto en queries**; proyección directa a DTO; sin cargar agregados completos ni columnas no usadas; sin includes innecesarios.
3. **Sin N+1 ni full scans evitables.**
4. **`hasDependents` nunca se calcula por ítem en listados** (ni post-caché). En superficies de **detalle** se resuelve con **una sola proyección SQL `EXISTS`** dentro de la query de lectura, no cargando la entidad + `AnyAsync` aparte (ADR-0001).
5. **Búsqueda free-text**: `MinSearchLength = 2` tras `Trim()`; `q` vacía/whitespace = "sin filtro" (válido); el rechazo es **400 en el validador**, antes de tocar caché o BD.
   - Supuesto de escala declarado: `Normalized*.Contains(q)` → `LIKE '%x%'` **no-sargable**; los índices B-tree compuestos no aplican. Aceptable mientras los catálogos sean ≲ unos miles de filas/tenant.
   - **Trigger de escalado**: si el p95 o las filas/tenant superan el supuesto → `pg_trgm` (índice GIN `gin_trgm_ops`) + `EF.Functions.ILike` (ADR-0002).
6. **Caché**: solo datos de baja volatilidad y alta repetición, **tenant-scoped**, con política de invalidación explícita. Excepción declarada: catálogos system-scoped (caché global legítima).
7. **Índices** orientados al patrón real de consulta, compuestos tenant-first; revisarlos cuando cambie el patrón de acceso.
8. Procesos pesados (reportes, exportaciones, lotes) preparados para **ejecución asíncrona** fuera del request path.
9. Observabilidad prevista para: latencia, errores, consultas lentas, throughput, rendimiento por endpoint, comportamiento de BD bajo carga.

---

## 9. Guardrails (fitness functions en CI)

🔒 **Receta de construcción**: reflexión sobre el assembly → filtro de **familia por regex** (no lista mantenida a mano) → agregación de **todas** las violaciones en **un solo `Assert`** → **centinela zero-match** que falla ruidosamente si el filtro no matchea ningún tipo.
🔒 **Extensión**: se **amplía el regex de familia**, no se duplica el test; siempre con su centinela; y se prueba el **rojo→verde** del regex ampliado antes del PR.
⚠️ Para `[Tags]`, reflexionar por **simple-name** (`Name == "TagsAttribute"`): es `Microsoft.AspNetCore.Http.TagsAttribute`, no un atributo propio.

| Categoría | Invariante que enforcea |
|---|---|
| **AUTHZ dos capas** | Todo controller de familia gobernada lleva `[AuthorizationPolicySet]`; convención Read→GET/HEAD, Manage→resto. **El superset política ⊇ gate NO lo verifica ningún test: revisión manual.** |
| **RATE LIMIT** | Endpoints costosos (export, search, graph) con `[EnableRateLimiting]` particionado user/tenant |
| **OPENAPI / VERSIONING** | `[ApiVersion("1.0")]` + ruta versionada + `[Tags]` + `[ProducesStandardErrors]` + `[SwaggerOperation]`, con tag esperado por familia |
| **PAGINACIÓN** | Todo listado con `[Range(1, MaxPageSize)]` y el máximo idéntico al del validator |
| **CONCURRENCY TOKEN** | Entidades con update optimista declaran `.IsConcurrencyToken()`; conflicto → `CONCURRENCY_CONFLICT` |
| **PUBLIC ID EN EL WIRE** | Params `Guid *Id` se exponen como `*PublicId`; en C# se nombra `*Id` |
| **TENANT SCOPE** | `IgnoreQueryFilters` solo donde está justificado y gobernado |
| **CATÁLOGO SYSTEM-SCOPED** | Catálogos de tipo globales; su caché global no es violación |
| **EXCEPCIONES TIPADAS** | Errores de negocio por excepción tipada + enum, mapeados a code/HTTP; nunca por mensaje |
| **CAP DE GRAFO** | Operaciones que expanden grafos topan nodos |
| **EXPORT / REPORTS** | Formatos compatibles por recurso; gobernanza de export jobs; params del job async verbatim del cliente |
| **BINDING DE CATÁLOGO** | Mapa de binding feature↔catálogo completo y consistente |
| **GATE DE CAMPO SENSIBLE** | Campos salariales detrás de gate a nivel de campo |
| **ANÓNIMOS** | La superficie `[AllowAnonymous]` no inyecta auditoría tenant-scoped (allow-list justificada) |
| **DI SIN CICLOS** | El `DbContext` se resuelve en un hilo con deadline. ⚠️ Con `using`, el `Dispose()` del provider se cuelga en el mismo lock y el fallo nunca se reporta: hay que **fugar** el provider en el camino de fallo |
| **CONTRATO PÚBLICO** | Superficie pública estable, sin fugas accidentales |
| **TIPO DE FECHA** | Dos invariantes de §1.2/§4.5: (a) una propiedad de dominio cuyo nombre denota un **día** es `DateOnly`, no `DateTime` — con *allow-list* explícita y motivada para los instantes deliberados y para lo que aún no se ha convertido; (b) ningún lector de JSON Patch usa `TryGetDateTime` en crudo. ⚠️ La *allow-list* nace poblada con el inventario pendiente: **vaciarla es el avance**, no un efecto colateral. **Seguimiento de los 64 campos que faltan, por olas de consecuencia: `ComentariosPruebasBackend/00000-CreateCompany.md` §3.8** (los otros 15 de la lista son instantes deliberados de H-26, no deuda) |

---

## 10. Plataforma: middlewares, filtros y localización

**Middlewares del pipeline** (orden vigente): `CorrelationIdMiddleware` · `RequestIdentityContextResolver` · `RequestLocaleMiddleware` · `RequestLoggingMiddleware` · `SecurityHeadersMiddleware` · `UnhandledExceptionMiddleware`.

**Filtros de resultado**: `AllowedActionsResultFilter` · `ConditionalRequestResultFilter` (ETag/If-Match) · `PersonnelFilePhotoUrlResultFilter`.

**Configuración del contrato público**: `PublicContractJsonTypeInfoResolver` · `PublicContractRouteConvention` · `PublicContractSchemaFilter` · `PublicContractOperationFilter` · `PublicContractBindingMetadataProvider` · `AuthorizationPolicyOperationFilter` · `CatalogTypeSlugOperationFilter`.

**Localización**: recursos `BackendMessages.resx` / `.es` / `.es-SV`, con `RequestLanguageResolver` e `IBackendMessageLocalizer`. ⚠️ El localizador **reemplaza el `detail`** del ProblemDetails por el texto catalogado — por eso los datos accionables van en `Extensions`, no en el mensaje.

**Todo código de error nuevo exige dos entradas**: una en `BackendMessages.resx` (inglés) y una en `BackendMessages.es.resx`. Lo vigila `BackendMessageLocalizationTests.ResourceCatalog_ShouldContainAllApplicationErrorCodes_InEnglishAndSpanish`, que extrae los códigos de `CLARIHR.Application` y falla nombrando los que falten. No hay orden alfabético que respetar: el archivo ya trae 309 pares desordenados: se inserta junto a las claves hermanas.

⚠️ **`BackendMessages.es-SV.resx` es inalcanzable.** `RequestLanguageResolver.TryNormalizeLanguage` corta la región (`es-SV` → `es`) y `ResolveCulture` devuelve siempre la cultura neutra, así que `CurrentUICulture` nunca vale `es-SV` y el `ResourceManager` jamás consulta ese archivo — la reserva de recursos va de específico a neutro, nunca al revés. Sus 23 claves no aportan nada: **0 son exclusivas** y su contenido ya se fusionó en `.es`.

⚠️ **Varios planes técnicos afirman que olvidar una clave en `.es-SV.resx` «rompe el build». Es falso**: `BackendMessageLocalizationTests` solo compara inglés y `es`. La convención de «3 resx» que repiten esos planes apunta a un archivo que no se sirve.

**La calidad del español la vigila `SpanishMessageQualityTests`** (paridad de claves ≠ calidad del texto): prohíbe fragmentos en inglés, pérdida de tildes o eñes, y valores idénticos al inglés. Al introducirlo el 2026-08-18 cazaba 196 y 204 mensajes respectivamente.
