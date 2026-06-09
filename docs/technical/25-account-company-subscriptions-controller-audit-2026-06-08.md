# Auditoría Técnica por Controlador — AccountCompanySubscriptionsController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-08 · Rama: `master` · Auditor: Claude (Opus 4.8)
> Contexto previo: esta auditoría sigue a la **evaluación de alineación canónica** del mismo controlador (15 criterios) que produjo un plan de 3 PRs (PR-A swagger/tags/errores + 409; PR-B `ConcurrencyToken`+`If-Match`; PR-C migración de la familia `api/account/*` → `api/v1/account/*`). Aquí el foco es **seguridad / corrección / rendimiento / operación**, no la adherencia canónica (ya capturada en ese plan).

## 1. Resumen ejecutivo

`AccountCompanySubscriptionsController` es la **fachada de autoservicio (command/RPC) del *owner*** sobre el agregado `CompanySubscription` y su *ledger* de cambios (`CompanySubscriptionPlanChange`, `CompanyCommercialAddonChange`). 8 endpoints: overview, catálogo de planes, preview de plan, **cambio de plan** (PUT), add-ons activos, marketplace, preview de add-on y **aplicar cambio de add-on** (POST). La autorización es **ownership delegada en el handler** (`company.CreatedByUserPublicId == JWT.sub`, vía `AccountCompanyActorResolver`) — **NO** RBAC; por eso la ausencia de `[AuthorizationPolicySet]` es **por diseño** (igual que el hermano `AccountCompaniesController`).

Veredicto: **APROBADO CON OBSERVACIONES**. **0 críticos / 0 altos.** El núcleo de seguridad es sólido y está **probado**: el gate de ownership es explícito y uniforme en los 8 handlers, con un test de integración negativo (`NonOwner → 403`); la **escalada de privilegios al plan MASTER está gateada** (preview/cambio → 403 sin operador de plataforma; el listado filtra MASTER) y testeada en ambos sentidos. No hay mass-assignment (DTOs cerrados, `Observations ≤ 2000`), no hay fuga cross-tenant (la comparación de ownership es el gate; las consultas siguientes usan el `Id` interno de la company ya validada), y la auditoría se persiste dentro de la transacción (Before/After). El hallazgo de mayor valor es de **robustez**: las carreras de doble-submit chocan contra los índices ÚNICOS filtrados y hoy salen como **500** en vez de **409** (ACS-A). El resto son rendimiento/UX/observación.

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila (solución completa) |
| Unit tests (Subscription, **ejecutados**) | ✅ 15/15 passed |
| Integration tests (revisados, no ejecutados) | 4 (incl. negativo-autz `NonOwner→403` + MASTER-gating en ambos sentidos) |
| Enrolamiento en guardrails de familia | **OpenAPI ✅ (PR-A) · concurrency-token ✅ (PR-B)** · authz/paginación N/A por diseño · rate-limit opcional |
| Hallazgos | 0 Crít · 0 Alto · **1 Media · 3 Baja · 3 Observación** |

## 2. Alcance

**Incluido:** controlador `AccountCompanySubscriptionsController.cs`; aplicación `AccountCompanySubscriptionAdministration.cs` (8 handlers + `AccountCompanySubscriptionHelper`) y `AccountCompanySubscriptionContracts.cs` (DTOs/queries/commands/validators); gate de ownership `AccountCompanyActorResolver` + `AccountCompanyErrors`; dominio `CompanySubscription` + transiciones; persistencia `ICompanySubscriptionRepository` / `CompanySubscriptionRepository`; EF `CompanySubscriptionConfiguration` (+ tablas hermanas del ledger). Resolvers de negocio compartidos `PlatformSubscriptionPlanChangeResolver` / `PlatformCompanyAddonChangeResolver` revisados por su interacción con el flujo account.

**Excluido:** el controlador hermano de plataforma (`PlatformCompanySubscriptionsController`, Backoffice) salvo por contraste; el `CompanySubscriptionLifecycleProcessor`/`BackgroundService` (promoción de agendadas / expiración) salvo por su efecto en el dominio; el `IPlanEntitlementService`; auditoría integral; pruebas de carga.

## 3. Metodología

Revisión estática endpoint→SQL con foco en autorización (ownership delegado, consistencia entre los 8 ops), aislamiento por ownership/tenant, mass-assignment, escalada de privilegios (plan MASTER), concurrencia (modelo replace + índices únicos filtrados) y rendimiento (N+1, caps). Evidencia: unit tests de la vertical ejecutados; integración **revisada por código** (requiere DB; no ejecutada → limitación, igual que en auditorías hermanas).

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Autz | Handler |
|---|---|---|---|---|---|
| 1 | GET | `/api/account/companies/{publicId}/subscription` | Overview (plan actual + add-ons activos + módulos efectivos) | ownership | `GetOwnedCompanySubscriptionQueryHandler` |
| 2 | GET | `.../subscription/plans` | Planes comerciales disponibles (MASTER filtrado salvo operador) | ownership + master-gate | `GetOwnedCompanySubscriptionPlansQueryHandler` |
| 3 | POST | `.../subscription/preview` | Previsualizar cambio de plan (query-vía-POST) | ownership + master-gate | `PreviewOwnedCompanySubscriptionPlanChangeQueryHandler` |
| 4 | PUT | `.../subscription` | **Cambiar de plan** (cancela actual + activa nueva, en tx) | ownership + master-gate | `ChangeOwnedCompanySubscriptionCommandHandler` |
| 5 | GET | `.../subscription/addons` | Add-ons activos de la company | ownership | `GetOwnedCompanySubscriptionAddonsQueryHandler` |
| 6 | GET | `.../subscription/addons/marketplace` | Catálogo de add-ons adquiribles (IsOwned/CanAcquire) | ownership | `GetOwnedCompanySubscriptionMarketplaceQueryHandler` |
| 7 | POST | `.../subscription/addons/preview` | Previsualizar cambio de add-on (query-vía-POST) | ownership | `PreviewOwnedCompanyAddonChangeQueryHandler` |
| 8 | POST | `.../subscription/addons` | **Aplicar cambio de add-on** (Activate/Deactivate, en tx) | ownership | `CreateOwnedCompanyAddonChangeCommandHandler` |

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / CQRS | PASS | Despacho puro; lógica en handlers/helper/resolvers |
| Arquitectura | Dominio (máquina de estados) | PASS | `CompanySubscription`: Active/Suspended/Cancelled/Expired/Scheduled con `SubscriptionStatusPolicy` |
| Seguridad | Autenticación | PASS | `[Authorize]` |
| Seguridad | Autz ownership (8/8 ops) | PASS | `AccountCompanyActorResolver.ResolveOwnedCompanyAsync`: `CreatedByUserPublicId == JWT.sub` → 403 |
| Seguridad | Escalada a plan MASTER | PASS (fuerte) | `EnsureMasterPlanAccessAsync` en preview+cambio; `GetPlans` filtra MASTER; testeado ambos sentidos |
| Seguridad | Aislamiento (tenant/ownership) | PASS | Gate por ownership; consultas posteriores por `company.Id` interno; sin `IgnoreQueryFilters` |
| Seguridad | Mass assignment | PASS | DTOs cerrados (`CommercialPlanId`, `Action`, `Observations≤2000`); precios/estados se sellan server-side |
| Seguridad | Enumeración (403 vs 404) | PASS (descartado) | El split existe pero sobre `publicId` = Guid aleatorio (no enumerable) → no explotable |
| Seguridad | Autz negativa testeada | PASS | Integ `NonOwner→403` + MASTER-forbidden (preview+PUT) |
| Contrato | Versionado `/v1` | FAIL (plan) | `api/account/...` sin versión → PR-C (familia) |
| Contrato | `[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors]` | FAIL (plan) | Ausentes → PR-A |
| Contrato | `[AuthorizationPolicySet]` | NO APLICA | Ownership delegado — **por diseño** |
| Contrato | Cada mutación retorna su entidad | PASS | PUT/POST devuelven el overview resultante (FE no re-consulta) |
| Concurrencia | Token optimista en el agregado | FAIL (plan) | `CompanySubscription` sin `ConcurrencyToken` → PR-B (`If-Match`) |
| Concurrencia | Carrera doble-submit → 409 | **FAIL** | **ACS-A**: índices únicos filtrados atrapan la colisión pero sale **500** (no 409) |
| Concurrencia | Integridad estructural | PASS | `uq_*__company_live` / `__company_scheduled` / plan-change `__company_scheduled`; mutaciones en tx |
| Rendimiento | N+1 | **OBS→FAIL** | **ACS-B**: `GetActiveAddonsAsync` itera `GetByIdAsync` por add-on; overview se construye 2× en mutaciones |
| Rendimiento | Cap de colección | OBS | **ACS-D**: `AddonPageSize=100` silencioso en overview/preview |
| Rendimiento | Rate limit | OBS | **ACS-E**: sin rate-limit; reads de referencia descartados por precedente; mutaciones/previews opcionales |
| Corrección | Overview refleja estado real | OBS | **ACS-C**: `GetActiveByCompanyIdAsync` (solo Active) → Trial/Suspended/Scheduled ⇒ 404 |
| Corrección | Borrado / ciclo de vida | PASS | Sin DELETE; máquina de estados; hard-delete bloqueado por FK `Restrict` + integridad financiera/auditoría |
| Observabilidad | Auditoría | PASS | `LogForTenantAsync` con Before/After **dentro** de la tx (sin await perdido) |
| Pruebas | Unit | PASS | Vertical de suscripción (dominio/state/addon/helper) |
| Pruebas | Integración | PASS (gap) | 4 métodos; sin caso de carrera→409 ni overview en estado no-Active |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS limpia: el controller sólo despacha; la lógica vive en los 8 handlers + `AccountCompanySubscriptionHelper` + los resolvers compartidos. El dominio `CompanySubscription` es una **máquina de estados** con política de transiciones (`SubscriptionStatusPolicy.CanTransition/IsReasonAllowed`) y un **ledger inmutable** de transiciones/plan-changes/addon-changes. El cambio de plan no muta in-place: **cancela** la suscripción vigente y **activa una nueva fila** (patrón replace/append) dentro de una transacción. Nit: el controller declara plantillas de ruta **absolutas** duplicadas en cada método (redundante con `[Route]`) y un helper `ResolveCompanyPublicId` que parsea `RouteData` — humo derivado del naming `{publicId}` (ver ACS-G).

### 6.2 Seguridad
**Modelo de ownership, sólido y uniforme.** Los 8 handlers invocan `ResolveOwnershipAsync` → `ResolveCurrentUserAsync` (JWT `sub` → `User`) + `ResolveOwnedCompanyAsync`, que compara `company.CreatedByUserPublicId == ownerUserPublicId` y devuelve **403 `COMPANY_OWNERSHIP_FORBIDDEN`** si no coincide, **404 `COMPANY_NOT_FOUND`** si no existe. No es RBAC: es ownership por-recurso (un único dueño = el creador). El test `AccountCompanySubscription_NonOwner_ShouldReturnForbidden` ancla este gate.

**Control de escalada de privilegios (fortaleza).** El plan **MASTER** (interno de CLARI) está reservado a operadores de plataforma: `EnsureMasterPlanAccessAsync` gatea **preview** y **cambio** (→ 403 `ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN`) y `GetPlans` **filtra** MASTER salvo que el usuario sea operador. Testeado en ambos sentidos (sin operador → forbidden + invisible; con operador → visible + elegible). Esto impide que un owner se auto-asigne el plan irrestricto.

**Sin mass-assignment / sin fuga cross-tenant.** Los request son records cerrados; precios, versiones de plan, fechas y estados se **sellan server-side** desde el catálogo (`CommercialPlan.GetVersionEffectiveOn`, snapshots en el ledger). Tras validar ownership, todas las consultas usan el `company.Id` **interno**; los reads de catálogo (`/plans`, `/marketplace`) exponen sólo el catálogo comercial **público** (precios/módulos), no datos de otro tenant. La distinción 403-vs-404 sobre `publicId` no es explotable por ser Guid aleatorio (no enumerable).

### 6.3 Concurrencia y consistencia
El modelo es **replace + índices únicos filtrados**: como mucho una suscripción "viva" (`uq_company_subscriptions__company_live`) y una "agendada" por company, y un plan-change agendado por company. Las mutaciones corren en `BeginTransactionAsync`. **Brecha (ACS-A):** ante doble-submit/dos-pestañas, la segunda escritura choca contra el índice único y lanza `DbUpdateException`/`UniqueConstraintViolationException`, que **no se captura** → **HTTP 500**. Lo canónico (CostCenters R2, OrgUnits OU-004, LocationGroups) es mapearla a **409 `CONCURRENCY_CONFLICT`** (error que **ya existe** en `AccountCompanyErrors`, sin resx nuevo). El agregado tampoco tiene `ConcurrencyToken` (decisión ya tomada en el plan → PR-B `If-Match`), por lo que un owner puede actuar sobre un overview obsoleto sin detección optimista hasta que el índice lo frene.

### 6.4 Rendimiento
**N+1 (ACS-B):** `AccountCompanySubscriptionHelper.GetActiveAddonsAsync` pagina los add-ons activos y luego itera `commercialAddonRepository.GetByIdAsync(...)` **por cada add-on** para enriquecer descripción/entitlements. Se invoca en `BuildOverviewAsync`, `BuildPlanPreviewAsync` y `BuildAddonPreviewAsync`; y las mutaciones construyen el overview **dos veces** (Before/After para auditoría), compuesto cada vez por (suscripción + plan + add-ons N+1 + módulos efectivos). Acotado por el número de add-ons activos (pequeño) y por `AddonPageSize=100`, pero es N+1 real y duplicado — candidato a `GetByIdsAsync` batch (patrón ya usado en CompanyUsers F1 / CompetencyFramework / Provisioning PV3). **Cap silencioso (ACS-D):** sólo los primeros 100 add-ons activos entran al overview/preview; >100 se omiten sin señal (irreal en la práctica, pero la casa evita "silent caps").

### 6.5 Corrección / UX
**Overview "Active-only" (ACS-C):** el overview, `/plans` y `/marketplace` resuelven la suscripción con `GetActiveByCompanyIdAsync` (**solo `Active`**). Un owner cuya suscripción esté `Trial`, `Suspended` o `Scheduled` recibe **404 `SubscriptionNotFound`** y queda ciego a su propio estado — mientras el hermano de plataforma usa `GetCurrentByCompanyIdAsync` (Draft/Trial/Active/Suspended). Para un portal de autoservicio esto es una brecha de visibilidad: conviene confirmar la intención de producto y, de ser el caso, exponer el estado no-Active en modo lectura (al menos `Suspended`/`Trial`).

### 6.6 Observabilidad
Las mutaciones auditan vía `auditService.LogForTenantAsync(company.PublicId, AuditLogEntry(..., Before, After))` **dentro** de la transacción (SaveChanges → audit → SaveChanges → Commit), sin `await` perdido. `Before`/`After` capturan el overview pre/post — trazabilidad correcta del cambio comercial. Adecuado.

### 6.7 Pruebas
Vertical unit de suscripción ejecutada (dominio/state-management/addon-management/helper). Integración **revisada**: 4 métodos — flujo owner end-to-end (upgrade FREE→PRO + adquirir add-on + downgrade a FREE con baja automática de add-ons), `NonOwner→403`, y MASTER-gating con/sin operador. **Gaps:** sin caso de **carrera→409** (ACS-A) ni de **overview en estado no-Active** (ACS-C).

### 6.8 Build / DevSecOps
Compila; sin secretos hardcodeados; sin `IgnoreQueryFilters` en esta vertical. Snake_case completo en EF (sin migración pendiente por columnas).

## 7. Hallazgos

### ACS-A — Carrera en cambio de plan/add-on → 500 en vez de 409
**Severidad:** Media · **Categoría:** Corrección/Robustez (concurrencia) · **Ubicación:** `ChangeOwnedCompanySubscriptionCommandHandler` (~266-364) y `CreateOwnedCompanyAddonChangeCommandHandler` (~609-699).
**Condición:** ambos corren en transacción y dependen de los índices únicos filtrados (`uq_company_subscriptions__company_live`/`__company_scheduled`, `uq_company_subscription_plan_changes__company_scheduled`). Un doble-submit/dos-pestañas hace que la segunda escritura viole el índice → `DbUpdateException` propagada (el `catch` solo hace rollback+rethrow) → **HTTP 500**.
**Criterio esperado:** las violaciones de unicidad concurrentes se mapean a **409 `CONCURRENCY_CONFLICT`** (convención app-wide; CostCenters R2 / OrgUnits OU-004 / LocationGroups).
**Impacto:** 500 espurio en una operación de negocio recurrente (cambios de plan/add-on); ruido de errores y mala UX. Sin riesgo de corrupción (el índice protege la integridad). 
**Recomendación:** capturar `UniqueConstraintViolationException` en ambos handlers → 409 `AccountCompanyErrors.ConcurrencyConflict` (ya existe, sin resx). Single-source del nombre de índice. Añadir test de integración de carrera→409. Converge con PR-A (mapping) y PR-B (`If-Match`).
**Prioridad:** Alta · **Esfuerzo:** Bajo · **Estado:** Abierto

### ACS-B — N+1 en `GetActiveAddonsAsync` (amplificado 2× en mutaciones)
**Severidad:** Baja (Media en companies con muchos add-ons) · **Categoría:** Rendimiento · **Ubicación:** `AccountCompanySubscriptionHelper.GetActiveAddonsAsync` (~776-798); amplificado por el doble `BuildOverviewAsync` en las mutaciones.
**Condición:** tras paginar los add-ons activos, itera `commercialAddonRepository.GetByIdAsync` por add-on; el overview se reconstruye Before+After en cada mutación.
**Impacto:** N consultas extra por overview (×2 en mutaciones). Acotado por add-ons activos (pequeño) — por eso Baja.
**Recomendación:** batch `GetByIdsAsync(addonIds)` + diccionario; opcionalmente reutilizar el `Before` ya construido. Patrón de de-N+1 ya establecido en el repo.
**Prioridad:** Media · **Esfuerzo:** Bajo-Medio · **Estado:** Abierto

### ACS-C — Overview "Active-only" oculta Trial/Suspended/Scheduled
**Severidad:** Baja · **Categoría:** Corrección/UX · **Ubicación:** `BuildOverviewAsync` / `GetPlans` / `GetMarketplace` → `GetActiveByCompanyIdAsync`.
**Condición:** sólo resuelve `Status == Active`; cualquier otro estado ⇒ `null` ⇒ 404 `SubscriptionNotFound`.
**Impacto:** un owner en Trial/Suspended no ve su suscripción ni puede entender por qué (contraste con el `GetCurrentByCompanyIdAsync` del hermano de plataforma).
**Recomendación:** confirmar intención de producto; si aplica, exponer el estado actual no-Active en lectura (mínimo `Suspended`/`Trial`) reutilizando `GetCurrentByCompanyIdAsync`. Añadir test.
**Prioridad:** Media · **Esfuerzo:** Medio · **Estado:** Abierto (requiere decisión de producto)

### ACS-D — Cap silencioso `AddonPageSize = 100`
**Severidad:** Baja · **Categoría:** Rendimiento/Corrección · **Ubicación:** `AccountCompanySubscriptionHelper` (`AddonPageSize`, `GetActiveAddonsAsync`).
**Condición:** el overview/preview sólo consideran los primeros 100 add-ons activos; el resto se omite sin señal.
**Impacto:** irreal hoy (ninguna company tiene >100 add-ons activos), pero es un truncamiento silencioso.
**Recomendación:** documentar el supuesto de escala con comentario, o `log` si se trunca (política "no silent caps").
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### ACS-E — Sin rate-limiting (mutaciones/previews)
**Severidad:** Observación · **Categoría:** Operación · **Ubicación:** controlador (sin `[EnableRateLimiting]`).
**Condición:** ningún endpoint está limitado. Los reads de referencia (`/plans`, `/addons`, `/marketplace`) se invocan en carga de formulario → **descartado por precedente** (GeneralCatalogs). Las mutaciones (PUT plan / POST add-on) y los previews son recompute-pesados.
**Impacto:** bajo — el gate de ownership + los índices únicos ya acotan el abuso (no se puede thrashear más allá de 1 viva/1 agendada).
**Recomendación:** **opcional** — política modesta por usuario+tenant en las 2 mutaciones + 2 previews si se observa abuso. No bloqueante.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto (opcional)

### ACS-F — Drift canónico (ya planificado)
**Severidad:** Observación · **Categoría:** Contrato · **Ubicación:** controlador.
**Condición:** sin `[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors]`; ruta `api/account/...` no versionada; param `{publicId}` para un company id; agregado sin `ConcurrencyToken`.
**Impacto:** documentación/consistencia; **ya capturado** en el plan de 3 PRs (PR-A/B/C). No re-litigar aquí.
**Recomendación:** ejecutar PR-A → PR-B → PR-C.
**Prioridad:** Media · **Esfuerzo:** Medio · **Estado:** Planificado

### ACS-G — `ResolveCompanyPublicId` (RouteData fallback) + rutas absolutas duplicadas
**Severidad:** Observación · **Categoría:** Mantenibilidad · **Ubicación:** controlador (`ResolveCompanyPublicId`, `[HttpGet("/api/account/...")]` por método).
**Condición:** plantillas absolutas duplican el `[Route]`; el helper parsea `RouteData` cuando `publicId == Guid.Empty` (humo del naming `{publicId}`).
**Impacto:** ninguno funcional (un Guid vacío no matchea company alguna); ruido de mantenimiento.
**Recomendación:** eliminar con el rename `{publicId}`→`{companyPublicId}` de PR-A (usar binding directo, borrar el helper y las plantillas absolutas).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Planificado (PR-A)

## 8. Considerados y descartados (no-hallazgos)

- **403 vs 404 (enumeración):** descartado — `publicId` es Guid aleatorio (122 bits), no enumerable secuencialmente; consistente con el tratamiento de public ids en el repo.
- **Escalada a MASTER:** control **presente y testeado** (no es brecha; es fortaleza).
- **Modelo de único dueño (sin delegación/co-owners):** **por diseño** (igual que CompanyUsers F3); delegación = roadmap, no deuda.
- **Reads de catálogo (`/plans`/`/marketplace`) en el controller de suscripción:** el catálogo comercial es público (precios/módulos), no secreto de tenant; fachada de UX aceptable (se relaciona con los criterios canónicos #1/#6/#13, no con seguridad).
- **Resolvers compartidos con Platform:** se invocan **después** de validar ownership y operan sobre la company ya validada; sin acceso cross-company adicional.

## 9. Conclusión

Controlador **maduro y seguro** en su núcleo: ownership explícito y uniforme, anti-escalada a MASTER, sin mass-assignment ni fuga cross-tenant, auditoría transaccional, y un modelo de concurrencia estructural (índices únicos filtrados) correcto. **0 crít / 0 alto.** La acción de mayor valor es **ACS-A** (mapear la carrera a 409, esfuerzo bajo, error ya existente), seguida de **ACS-B** (de-N+1) y **ACS-C** (visibilidad de estados no-Active, requiere decisión de producto). El drift canónico (ACS-F/G) ya está cubierto por el plan de 3 PRs acordado.

| ID | Severidad | Prioridad | Esfuerzo | Estado |
|---|---|---|---|---|
| ACS-A | Media | Alta | Bajo | ✅ **Remediado (PR-A)** |
| ACS-B | Baja | Media | Bajo-Medio | Abierto |
| ACS-C | Baja | Media | Medio | Abierto (decisión producto) |
| ACS-D | Baja | Baja | Bajo | Abierto |
| ACS-E | Observación | Baja | Bajo | Abierto (opcional) |
| ACS-F | Observación | Media | Medio | ✅ **Resuelto** — Swagger/Tags/errores (PR-A) + token+If-Match (PR-B) + `/v1` familia (PR-C) |
| ACS-G | Observación | Baja | Bajo | ✅ **Resuelto (PR-C)** — `{companyPublicId}` + plantillas relativas + `ResolveCompanyPublicId` eliminado |

## 10. Estado de remediación — PR-A (no-breaking, sin migración, uncommitted)

Entregado y verificado (build Release limpio · unit **1760/1760**, incl. 9 nuevos · integración no ejecutada, sin cambios de ruta/contrato que la afecten):

- **ACS-A ✅** — `ChangeOwnedCompanySubscriptionCommandHandler` y `CreateOwnedCompanyAddonChangeCommandHandler` capturan `UniqueConstraintViolationException` → **409 `CONCURRENCY_CONFLICT`** (`AccountCompanyErrors.ConcurrencyConflict`, ya existía → sin resx). Nombres de índice **single-sourced** en `CompanySubscriptionConstraintViolations` (Application), referenciados por la EF config (`CompanySubscriptionConfiguration`) y los handlers; guardrail `CompanySubscriptionConstraintViolationsTests` (9) fija los nombres con literales.
- **C15 ✅** — `[SwaggerOperation]` (Summary+Description) en los 8 endpoints.
- **C13 ✅** — `[Tags("Account Subscription")]` + **enrolado** en `OpenApiContractGuardrailsTests` (familia `^AccountCompanySubscriptions`, sin colisión de prefijo); **NO** `[AuthorizationPolicySet]` (ownership por diseño).
- **Errores declarativos ✅** — `[ProducesStandardErrors(Read|SubResourceWrite)]` reemplaza las listas verbosas de `[ProducesResponseType<ProblemDetails>]`.
- **C7 ✅** — `POST /addons` documentado como comando reversible (200, no 201) en su `[SwaggerOperation]`.

**Pendiente PR-A:** regenerar la sección de `docs/technical/api/openapi.yaml` (requiere levantar la API; sin test de drift que lo bloquee).

## 11. Estado de remediación — PR-B (criterio 2 · ConcurrencyToken + If-Match · **breaking writes** · con migración, uncommitted)

Entregado y verificado (build Release limpio — src + ambos proyectos de test · unit **1760/1760** · integración compila, no ejecutada sin DB):

- **Dominio** — `CompanySubscription` gana `Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();` + `RefreshConcurrencyToken()` invocado en `ApplyStatusChange` (rota en toda transición de estado); espejo 1:1 de `CompanySubscriptionPlanChange`.
- **EF** — `CompanySubscriptionConfiguration` mapea `concurrency_token` con `.IsConcurrencyToken()`. Guardrail `ConcurrencyTokenMappingGuardrailsTests` extendido con `typeof(CompanySubscription)`.
- **Migración** `20260609035830_AddConcurrencyTokenToCompanySubscriptions` — añade `concurrency_token uuid not null` con backfill `defaultValueSql: "gen_random_uuid()"` (espejo de `AddConcurrencyTokenToPersonnelFileInterestsEntities`); snapshot sin default → sin drift. **El usuario la aplica** (ver comando abajo).
- **Contrato** — `AccountCompanySubscriptionOverviewResponse` expone `ConcurrencyToken`; `GET /subscription` lo devuelve en el header `ETag`. `PUT /subscription` y `POST /addons` exigen **`[FromIfMatch]`** (missing → 400, stale → **409 `CONCURRENCY_CONFLICT`**) y devuelven el nuevo token por `ETag`. Validators con `ConcurrencyToken NotEmpty` (sentinela). ⚠️ **Breaking FE: debe enviar `If-Match` en ambos writes.**
- **Handlers** — `ChangePlan` y `ApplyAddonChange` validan `context.CurrentSubscription.ConcurrencyToken == command.ConcurrencyToken` (tras el gate MASTER, antes de elegibilidad) → 409. Reusa `AccountCompanyErrors.ConcurrencyConflict` (sin resx nuevo).
- **Tests** — integración: token enhebrado por todos los writes del happy-path + `OwnerWithoutPlatformOperator` ahora envía If-Match; **nuevo** `AccountCompanySubscription_StalePlanChange_ShouldReturnConflict` (token rancio → 409, determinista).

**Pendiente PR-B:** aplicar la migración (`dotnet ef database update --project src/CLARIHR.Infrastructure --startup-project src/CLARIHR.Api`); correr integración con DB.

## 12. Estado de remediación — PR-C (criterio 14 · migración de familia a `/v1` · **breaking URLs** · sin migración EF, uncommitted)

Entregado y verificado (build Release limpio — src + ambos proyectos de test, **0/0** · unit **1760/1760** · integración compila, no ejecutada sin DB):

- **Familia versionada** — los **3** controllers `api/account/*` → `api/v1/account/*`: `AccountCompaniesController` (`api/v1/account/companies`), `AccountCompanyAuthorizationController` (`.../{companyPublicId}/authorization`), `AccountCompanySubscriptionsController` (`.../{companyPublicId}/subscription`). Comentarios "must not be versioned" actualizados (decisión revertida explícitamente).
- **ACS-G** — el subscriptions controller pasa a **plantillas relativas** (`[HttpGet]`, `[HttpGet("plans")]`, `[HttpPut]`, `[HttpPost("addons")]`…), param `{companyPublicId}` (antes `{publicId}`), y se **elimina** `ResolveCompanyPublicId` (el hack de `RouteData`). Alineado 1:1 con el hermano `AccountCompanies`.
- **Guardrail** — `PublicContractGuardrailsIntegrationTests` actualizado: asserts a `/api/v1/account/companies/{companyPublicId}` (+`/switch`) y bans del variante `{publicId}` bajo v1. El §S6 genérico acepta `{companyPublicId}`; rutas de 6 segmentos no entran al check de "hybrid nested collection". `IdentityAndPublicContractStandardsTests` (fixture del transform, sin slash) **intacto** a propósito.
- **Tests** — barrido `/api/account/` → `/api/v1/account/` en 5 archivos (ApiIntegrationTests, PlatformAuthentication, AccountCompanyAuthorization, AccountCompanySubscriptions, PublicContractGuardrails); **0** refs sin versionar restantes.

⚠️ **Breaking FE:** todas las URLs de la familia account cambian a `/api/v1/account/...` (incl. el cambio de PR-B: `If-Match` en los writes de suscripción). **El frontend debe repuntar las base URLs.**

**Pendiente:** regenerar `docs/technical/api/openapi.yaml` (24 refs `api/account` quedan stale; requiere levantar la API; sin test de drift que bloquee CI). ACS-B/C/D/E abiertos como mejora.
