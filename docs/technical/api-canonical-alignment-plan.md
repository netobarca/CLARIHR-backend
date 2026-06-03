# Plan de alineación canónica de la API

> **Documento vivo / tracker.** Se actualiza con cada PR de alineación.
> **Creado:** 2026-06-03 · **Estado:** 🟡 En progreso · **Owner:** equipo backend
> **Alcance acordado:** Core HR/Org primero; backoffice como ola final. 1 PR por controlador. Política de borrado decidida **por entidad/flujo** (sin estándar único).

---

## 1. Contexto

Ya están alineados al patrón canónico cuatro dominios: **JobProfiles, Positions, Reports y PersonnelFiles**. El resto de la API (≈32 controladores candidatos de 61) sigue un patrón viejo y heterogéneo. Una auditoría (32 controladores × 8 criterios) detectó un **drift sistémico** compartido por casi todos:

- ConcurrencyToken viaja en el **body** en vez del header `If-Match`; respuestas sin `ETag`.
- `PATCH` son acciones RPC (`/activate`, `/inactivate`, `/reassign-group`) en vez de **JSON Patch RFC-6902**.
- IDs expuestos como `id` pelado en vez de `{Entidad}PublicId`.
- `POST` devuelve `201` sin header `Location`.
- Faltan decoradores canónicos (`[AuthorizationPolicySet]`, `[Tags]`, `[ProducesStandardErrors]`, `[SwaggerOperation]`, versionado).

**Hallazgo que reduce el riesgo:** casi todas las entidades candidatas **ya tienen `concurrency_token` mapeado** → las primeras olas son **cero migraciones**; el retrofit es solo de *superficie de API*.

---

## 2. Dashboard de progreso

Leyenda de estado: ⬜ pendiente · 🟡 en progreso · ✅ hecho · ⏸️ bloqueado/spike · ➖ excluido

| Ola | Dominio | Controladores | Progreso |
|---|---|---|---|
| 0 | Piloto (fija la receta) | 1 | 1/1 ✅ verificado (build 0/0, unit 1294/0, integration 274/0/24) |
| 1 | Estructura org. & Locations | 7 | 0/7 |
| 2 | Company / Users / Preferences | 4 | 0/4 |
| 3 | Backoffice & catálogos | 6 | 0/6 |
| — | Limpieza / tombstone | 1 | 0/1 |
| — | Excluidos (no-CRUD) | 9 | ➖ |
| — | Spikes de diseño | 3 | ⏸️ |

---

## 3. La receta canónica (patrón a replicar)

Referencia gold-standard: `src/CLARIHR.Api/Controllers/JobProfileFunctionsController.cs` y `PersonnelFileInterestsController.cs`. Building blocks reutilizables (ya existen, **no se crean**):

| Pieza | Ubicación | Para qué |
|---|---|---|
| `[AuthorizationPolicySet(Read, Manage)]` | `src/CLARIHR.Api/Common/Conventions/AuthorizationPolicySetAttribute.cs` | Authz declarativa por verbo (GET→Read, resto→Manage) |
| `[ProducesStandardErrors(StandardErrorSet.X)]` | `src/CLARIHR.Api/Common/Conventions/StandardErrorSet.cs` | ProblemDetails estándar (`Read`/`Query`/`Command`/`SubResourceWrite`) |
| `[FromIfMatch] Guid concurrencyToken` | `src/CLARIHR.Api/Common/Binders/FromIfMatchAttribute.cs` + `IfMatchModelBinder.cs` | Token desde header `If-Match`; 400 si falta/malformado |
| `this.ToActionResultWithETag(result, v => v.ConcurrencyToken)` | `src/CLARIHR.Api/Common/ResultExtensions.cs` | 200 + objeto + header `ETag` |
| `this.ToCreatedAtActionResult(result, nameof(GetById), routeValues, v => v.ConcurrencyToken)` | `src/CLARIHR.Api/Common/ResultExtensions.cs` | 201 + `Location` (→GetById) + `ETag` + objeto |
| `JsonPatchDocument<T>` + `[Consumes("application/json-patch+json")]` + `[RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]` | infra global (`Microsoft.AspNetCore.JsonPatch.SystemTextJson`) | PATCH RFC-6902 |
| `JsonPatchOperationMapper.Map(patchDoc, (op,path,from,value) => new {Entidad}PatchOperation(...))` | `src/CLARIHR.Application/Common/JsonPatch/JsonPatchOperationMapper.cs` | Traduce ops → comando de dominio |
| `ConditionalRequestResultFilter` / `ETagHeader` | `src/CLARIHR.Api/Common/` | If-None-Match→304; formateo ETag (global) |

**Convenciones:**
- **Naming:** ruta `api/v{version:apiVersion}/.../{entidadPublicId:guid}`, param C# `entidadPublicId`, campo del response record `EntidadPublicId` (+ `[JsonIgnore] public Guid Id => EntidadPublicId` si el código interno lo requiere). FKs también `*PublicId`.
- **GET lista = único endpoint que retorna array.** Debe existir **GET-by-id** para anclar el `Location` del POST.
- **Toda mutación (POST/PUT/PATCH) devuelve el objeto completo** con su `ConcurrencyToken`.
- **Concurrencia:** mismatch → `409 CONCURRENCY_CONFLICT`; `If-Match` ausente → `400`. (No se usan 412/428.)
- **Acciones de ciclo de vida** (`/activate`, `/inactivate`) se conservan pero migran su token a `If-Match` y emiten `ETag`.

---

## 4. Infraestructura transversal a extender por dominio

1. **Policies:** crear `src/CLARIHR.Application/Features/{Dominio}/Common/{Dominio}Policies.cs` (`Read`/`Manage`); registrar en `src/CLARIHR.Api/Program.cs` (`AddPolicy(...)`).
   - ⚠️ **No olvidar (gotcha de Ola 0):** agregar el caso `{Dominio}Policies.Read or .Manage => {Dominio}Errors.Forbidden` en `ProblemDetailsAuthorizationMiddlewareResultHandler.ResolveForbiddenError` (`src/CLARIHR.Api/Common/Authorization/`). Sin esto, un 403 denegado en la **capa de policy** devuelve el genérico `auth.forbidden` en vez del código de dominio `{DOMINIO}_FORBIDDEN`, y el integration test `*_WithoutPermission_ShouldReturn403` falla. (No lo cubre ningún guardrail unitario — solo el integration test.)
2. **Guardrail authz:** ampliar `GovernedFamilyRegex` en `tests/CLARIHR.Application.UnitTests/AuthorizationPolicyConventionGovernanceTests.cs:39-40` + `{Dominio}PolicyNames` (Inv-1/Inv-3). Integración: `AuthorizationPolicyConventionGuardrailsIntegrationTests.cs`.
3. **Guardrail OpenAPI:** fila en `Families[]` de `tests/CLARIHR.Application.UnitTests/OpenApiContractGuardrailsTests.cs` + `[Tags("...")]` y `[SwaggerOperation(...)]`.
4. **Rate limiting** (si export/search pesado): `[EnableRateLimiting(policy)]` + Program.cs + `RateLimitingGovernanceTests.cs`.
5. **OpenAPI doc:** la **fuente de verdad** del contrato es el **Swagger en runtime** (`/swagger/v1/swagger.json`, generado por Swashbuckle en `dotnet build`), nunca los DTOs de C#. El `openapi.yaml`/`openapi-backoffice.yaml` commiteado es un **suplemento PARCIAL mantenido a mano** (lo dice su propio encabezado): actualizar **solo los schemas tocados** contra el swagger vivo. **No hay export automatizado** (diferido por diseño — "before automated export/versioning is introduced"); los scripts `extract_contract.py`/`validate_contract_docs.py` que una nota vieja mencionaba **no existen ni se necesitan**. Los guardrails (`OpenApiContractGuardrailsTests` unit+integración) validan los **decoradores del controlador** (`[Tags]`, `[ApiVersion]`, `[ProducesStandardErrors]`, `[SwaggerOperation]`) contra el swagger vivo — no el archivo `.yaml`. Gotcha: `openapi.yaml` concatena core+backoffice (claves `paths:`/`components:` duplicadas); para validar un schema, parsear ese bloque aislado.
6. **Migración EF** (solo si la entidad NO tiene token): `dotnet ef migrations add ...` → **el usuario la aplica manualmente**.

---

## 5. Checklist de retrofit por controlador (mecánico)

- [ ] IDs → `{entidad}PublicId` (ruta, params C#, response DTO, FKs)
- [ ] `GET` lista (único array) + `GET {id}` (anclaje de Location)
- [ ] `POST` → `ToCreatedAtActionResult(...)` (201 + Location + ETag + objeto)
- [ ] `PUT` → token a `[FromIfMatch]`; objeto vía `ToActionResultWithETag`
- [ ] **Nuevo `PATCH` RFC-6902** → `JsonPatchDocument<{Entidad}MutateRequest>` + `JsonPatchOperationMapper` + record `{Entidad}PatchOperation` + handler; objeto + ETag
- [ ] `/activate` `/inactivate` migran a `If-Match` + ETag
- [ ] **Decidir DELETE hard vs soft por flujo** (heurística abajo)
- [ ] Decoradores: `[ApiVersion]`+ruta versionada, `[AuthorizationPolicySet]`, `[Tags]`, `[Consumes]/[Produces]`, `[ProducesStandardErrors]` por acción, `[SwaggerOperation]`
- [ ] `ConcurrencyToken` expuesto en el response record
- [ ] Enganchar guardrails (policies, regex, Families, openapi)
- [ ] Tests: If-Match (400 ausente, 409 mismatch) + PATCH RFC-6902 + 201/Location
- [ ] PR `feat/<dominio>/<slug>`, build+unit+guardrails verdes, merge `--no-ff`

**Decisión DELETE por entidad (sin estándar único — por flujo):**
- **Soft (`PATCH /inactivate`)** si la entidad es referenciada por datos históricos/transaccionales (grepear FKs entrantes). Default para estructura (CostCenter, WorkCenter, OrgUnit, LegalRepresentative...).
- **Hard `DELETE`** (con `If-Match`) solo en hojas/catálogos sin referencias históricas y con check de uso previo.
- Documentar la decisión en el PR del controlador.

---

## 6. Olas de ejecución

### Ola 0 — Piloto / fija la receta (sin migración)
- [x] **`CostCentersController`** · S · sin migración · ✅ **verificado** (build 0/0, unit 1294/0, integration 274/0/24, todos los guardrails de integración verdes). Dejó la plantilla canónica reutilizable + `CostCenterPolicies.cs` + registro Program.cs + extensión de `GovernedFamilyRegex`/`Families[]`. Aplicado: PATCH RFC-6902 scalar-only (nuevo), If-Match+ETag en todas las mutaciones, 201+Location, `[ApiVersion]`/`[Tags]`/`[AuthorizationPolicySet]`/`[ProducesStandardErrors]`/`[SwaggerOperation]`. **Hallazgo:** el naming `*PublicId` NO requería cambios (la convención `PublicContractRouteConvention`/`PublicContractJsonTypeInfoResolver` renderiza `Id`→`publicId` automáticamente). **Bug atrapado en review:** `ToCreatedAtActionResult` necesita clave de ruta `publicId` (no `id`) porque la convención reescribe el template. **Diferido:** rate-limiting (su guardrail solo cubre PositionSlots; no obligatorio). Archivos: `CostCentersController.cs`, `Features/CostCenters/CostCenterAdministration.cs`, `Common/CostCenterPolicies.cs` (nuevo), `Program.cs`, 2 guardrails, + tests (3 existentes migrados a If-Match, 4 nuevos de integración RFC-6902, 12 unit del applier).

### Ola 1 — Estructura organizativa & Locations (cero migraciones)
- [ ] **`WorkCentersController`** · M · alta centralidad. Decidir `/reassign-group` (conservar acción o absorber en PATCH).
- [ ] **`WorkCenterTypesController`** · S · trivial; junto a WorkCenters.
- [ ] **`OrgUnitsController`** · M · **god-file (~479 líneas)**: separar CRUD de `export/diagram/graph`; decidir destino de esos endpoints.
- [ ] **`LegalRepresentativesController`** · S · conservar `set-primary` (migrar a If-Match).
- [ ] **`LocationGroupsController`** · M · **dos arrays** (`/tree` + search): definir array canónico; **agregar GetById**.
- [ ] **`LocationLevelsController`** · M · CRUD limpio.
- [ ] **`LocationHierarchyController`** · S · **singleton** (GET+PUT): solo alinear concurrencia (If-Match) + decoradores; sin POST/DELETE/array.

### Ola 2 — Company / Users / Preferences
- [ ] **`CompanyPreferencesController`** · S · ya 75% (token pass, PUT devuelve objeto); falta PATCH RFC-6902 + If-Match.
- [ ] **`UserPreferencesController`** · M · **requiere migración**: `UserPreference` sin `ConcurrencyToken` (agregar prop + mapeo `.IsConcurrencyToken()`/`concurrency_token`).
- [ ] **`AccountCompaniesController`** · M · analizar entidad raíz de empresa.
- [ ] ⏸️ **`CompanyUsersController`** · L · **spike**: proyección sobre `User` (Auth), multi-entidad, sin token; posible migración en `User`. No es CRUD simple.

### Ola 3 — Backoffice & catálogos (prioridad final)
Usan `openapi-backoffice.yaml`; mayoría con token mapeado.
- [ ] **`BankCatalogsController`** · S/M
- [ ] **`DocumentTypeCatalogsController`** · S/M
- [ ] **`EducationCatalogsController`** · S/M
- [ ] **`JobProfileCatalogTypesController`** · S/M
- [ ] **`CommercialAddonsController`** · S/M
- [ ] **`CommercialPlansController`** · S/M

---

## 7. Limpieza, exclusiones y spikes

### Limpieza (PR independiente)
- [ ] **Borrar tombstone:** `src/CLARIHR.Api/Controllers/PersonnelEducationCatalogsController.cs` (vacío; su función migró a `EducationCatalogsController` del backoffice).

### ➖ Excluidos del patrón (no son CRUD de recurso — documentar exclusión / lookahead negativo en guardrails)
`AccountCompanyAuthorization` (fachada IAM) · `AccountCompanySubscriptions` y `PlatformSubscriptions` (RPC comercial) · `GeneralCatalogs` y `AccountInternalCatalogs` (proyecciones read-only) · `CommercialModules` (catálogo de sistema en código) · `Audit` (append-only) · `LocationHierarchy` (singleton — alineación parcial en Ola 1).

### ⏸️ Spikes de diseño antes de alinear (no CRUD simple)
- `CompetencyFramework` — sin aggregate root (value objects/catálogos).
- `SalaryTabulator` — sin aggregate root; patrón change-request; `SalaryTabulatorChangeRequest` sin token (auditoría aparte).
- `CompanyUsers` — proyección sobre Auth `User`.

### 🔧 Mejoras técnicas (opcionales — fuera del flujo de alineación)
- [ ] **Automatizar el export del contrato OpenAPI** (cierra el drift del `openapi.yaml`). Hoy el `openapi.yaml`/`openapi-backoffice.yaml` se mantiene **a mano** y tiende a derivar respecto al swagger vivo (la fuente de verdad). Propuesta:
  - Agregar `Swashbuckle.AspNetCore.Cli` como dotnet tool en `.config/dotnet-tools.json` (no existe aún).
  - Paso de build/CI: `dotnet swagger tofile --output docs/technical/api/openapi.yaml <CLARIHR.Api.dll> v1` (y equivalente backoffice → `openapi-backoffice.yaml`).
  - Opcional: **drift-gate** en CI que falle si el `openapi.yaml` commiteado difiere del export fresco (igual filosofía que los guardrails actuales).
  - ⚠️ **Decisión de diseño:** el `openapi.yaml` actual es un **subconjunto público curado** (solo endpoints externamente relevantes), no el contrato exhaustivo. Un export crudo sería exhaustivo → decidir si (a) exportar todo, o (b) marcar la superficie pública con un document filter de Swagger y exportar solo esa. Esto es lo que la nota del encabezado llama *"before automated export/versioning is introduced"*.
  - **No bloquea** ninguna ola de alineación; es una mejora de infraestructura independiente.

---

## 8. Modelo de ramas/PR (AGENTS.md §16)

- 1 PR por controlador: `feat/<dominio>/<slug>` desde `origin/master` fresco; merge `--no-ff`; **el usuario commitea/mergea** salvo delegación explícita.
- Verde local obligatorio: `dotnet build src/CLARIHR.slnx` + unit + guardrails.
- Prod por tag manual (no desde rama).

## 9. Verificación (por cada PR)

1. **Build/unit:** `dotnet build src/CLARIHR.slnx` (0 warnings) + unit + **guardrails** (authz governance, openapi contract, rate-limit).
2. **Integración:** `CLARIHR.Api.IntegrationTests` — `If-Match` ausente→400, mismatch→409, PATCH RFC-6902 aplica ops, POST→201+`Location`+`ETag`, GET lista = único array.
3. **Contrato:** regenerar swagger (`dotnet build`), comparar `/swagger/v1/swagger.json` vivo, actualizar `openapi.yaml` solo schemas tocados; guardrails `OpenApiContractGuardrails*` verdes.
4. **Migración (si aplica):** `dotnet ef migrations add ...` y **entregar comando al usuario** (no aplicar).
5. **Smoke manual:** un CRUD end-to-end del recurso contra `apiclarihrdev` si el dominio es frontend-facing.

## 10. Riesgos / decisiones abiertas

- **Breaking changes para el frontend:** mover token a `If-Match`, renombrar `id→*PublicId` y POST sin-body→con-body rompen consumidores actuales (igual que el caso Files `/complete`). Cada PR frontend-facing debe traer **doc de consumo actualizada** y aviso al equipo.
- **OrgUnits god-file** y **LocationGroups dos-arrays**: mini-decisión de diseño dentro de su PR.
- **Regeneración de openapi.yaml:** no hay export automatizado (diferido por diseño); se actualiza a mano contra el swagger vivo. **No es bloqueante.** Mejora futura opcional: agregar `dotnet swagger tofile` (Swashbuckle.AspNetCore.Cli) para automatizar el export y frenar el drift del contrato.
- **CompanyUsers / CompetencyFramework / SalaryTabulator:** requieren spike antes de su PR.

---

## 11. Bitácora de cambios

| Fecha | PR | Controlador | Notas |
|---|---|---|---|
| 2026-06-03 | — | — | Plan creado a partir de auditoría de 32 controladores. |
| 2026-06-03 | — | — | Corregido caveat OpenAPI (scripts `extract_contract.py`/`validate_contract_docs.py` no existen ni se necesitan; contrato a mano + swagger vivo). Agregado ítem de mejora §7: automatizar export con `dotnet swagger tofile`. |
| 2026-06-03 | (sin commit) | CostCenters | **Ola 0 piloto VERIFICADO.** Alineación canónica + PATCH RFC-6902. Build 0/0, unit 1294/0, **integration 274/0/24 (todo verde incl. guardrails)**. Integration atrapó 1 defecto real: faltaba el mapeo `CostCenterPolicies.*→CostCenterErrors.Forbidden` en `ProblemDetailsAuthorizationMiddlewareResultHandler` (un 403 de policy daba `auth.forbidden` en vez de `COST_CENTERS_FORBIDDEN`) — corregido + documentado en §4.1 como gotcha de la receta. Pendiente: commit en rama `feat/cost-centers/...`. |
