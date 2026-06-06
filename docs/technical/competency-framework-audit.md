# Auditoría CompetencyFramework — seguimiento

> **Documento vivo / tracker.** Se actualiza al cerrar cada hallazgo.
> **Creado:** 2026-06-05 · **Estado:** ✅ CERRADO (10 corregidos + 1 won't-fix) · **Owner:** equipo backend
> **Alcance:** dominio CompetencyFramework completo — 3 controladores (`OccupationalPyramidLevelsController`, `CompetencyConductsController`, `JobProfileCompetencyMatrixController`) + capa Application (`Features/CompetencyFramework/`) + repositorio + dominio + EF config.
> **Dimensiones:** seguridad · performance · arquitectura, contra los estándares definidos en `AGENTS.md` (§17) y `docs/technical/overview/project-foundation.md`.

---

## 1. Veredicto

Dominio **maduro y canónico** (alineado a los 15 criterios — ver `api-canonical-alignment-plan.md` §7). **Cero vulnerabilidades críticas/altas:**

- **Aislamiento de tenant verificado** — `ApplicationDbContext.ApplyTenantFilters` aplica `HasQueryFilter(... TenantId == CurrentTenantIdOrDefault)` a todo `ITenantScopedEntity`; las 3 entidades CF extienden `TenantEntity` → los lookups por public-id ya están tenant-scoped y el patrón `*ExistsOutsideTenantAsync` (404 vs 403) es correcto. **No hay IDOR cross-tenant.**
- **Authz handler-gated correcto** en los 12 handlers (`ICompetencyFrameworkAuthorizationService.EnsureCanRead/ManageAsync`), read-gates en GET, manage-gates en mutaciones.
- **Deny-lists de PATCH completas**, RFC-6902 endurecido (`[RequestSizeLimit]` + max-ops), sin inyección (`LIKE` parametrizado), sin fuga de datos en errores, concurrencia OK.

Lo accionable es **performance (N+1), un hueco funcional (lectura de la matriz) y endurecimiento (rate-limit, guardrails, dedupe)** — todo **P2/P3**.

## 2. Leyenda de estado

⬜ pendiente · 🟡 en progreso · ✅ resuelto · ⏸️ diferido (backlog) · ➖ descartado

| Severidad | P1 crítico | P2 alto | P3 medio/bajo |
|---|---|---|---|
| Conteo | 0 | 3 | 8 |

**Progreso:** **10/11 corregidos + F9 ➖ won't-fix = AUDIT COMPLETO (0 pendientes).** F1,F4 (PR-A) · F2 (PR-B) · F3,F6,F7 (PR-C) · F5,F8 (PR-D) · F10,F11 (PR-E), 2026-06-05.

---

## 3. Hallazgos accionables

| # | Dim | Sev | Estado | Hallazgo | Evidencia (`file:line`, puede driftar) | Fix propuesto (alineado al estándar) |
|---|-----|-----|--------|----------|------------------------------------------|--------------------------------------|
| **F1** | PERF | P2 | ✅ | **N+1 en update de la matriz**: el loop resuelve 4 catálogos/ítem + 1/conducta, cada uno round-trip propio → `~4N+ΣC` queries (matriz 30×5 ≈ 180 por save). Sin tope de ítems. | `CompetencyFrameworkAdministration.cs:1540-1646`; resolver `:1817-1891` | Batch-load por set de public-ids (1 query por categoría + 1 de conductas), resolver en memoria. Patrón EXISTS/batch de `project-foundation §12.7`. |
| **F2** | ARCH | P2 | ✅ | **`GetJobProfileCompetencyMatrixQuery` huérfano**: handler+validator implementados pero ningún controlador lo despacha. La matriz solo se lee por `export` (xlsx) → el FE no puede cargarla como JSON para editar. Código muerto + hueco funcional. | handler `…Administration.cs:1415-1450`; `JobProfileCompetencyMatrixController` solo PUT+export | Cablear `GET /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix` (JSON + ETag) **o** borrar el handler. Recomendado: cablearlo (el FE lo necesita). |
| **F3** | PERF/SEC | P2 | ✅ | **Export sin rate-limiting**: `competency-matrix/export` es el "heavy endpoint" canónico y no tiene `[EnableRateLimiting]`; PersonnelFiles/PositionSlots sí (10/min). | `JobProfileCompetencyMatrixController.ExportJobProfileCompetencyMatrix` | `CompetencyFrameworkRateLimitPolicies.Export` (10/min) espejando `PersonnelFileRateLimitPolicies.Export` + `Program.cs` + `RateLimitingGovernanceTests`. *(Rate-limit = backlog no-bloqueante en el proyecto.)* |
| **F4** | PERF | P3 | ✅ | **N+1 en replace de behaviors**: 1 query por behavior. | `…Administration.cs:1343-1365` | Batch-resolver behaviors por set de ids (mismo fix que F1, menor escala). |
| **F5** | ARCH | P3 | ✅ | **God-file**: `CompetencyFrameworkAdministration.cs` (1893 líneas) con las 3 familias; tras el split de controladores debería partirse por recurso (como `JobProfile*Administration.cs`). | archivo completo | Partir en `OccupationalPyramidLevelAdministration.cs` / `CompetencyConductAdministration.cs` / `JobProfileCompetencyMatrixAdministration.cs` (mecánico, sin cambio funcional). |
| **F6** | GUARDRAIL | P3 | ✅ | **`[Range]` de paginación ausente en el boundary**: los `pageSize` solo se acotan en el validator del handler, no con `[Range(1,Max)]` en el controlador; CF no está enrolado en `PaginationRangeGuardrailsTests`. | `OccupationalPyramidLevelsController:37`, `CompetencyConductsController:40` | `[Range(1, MaxPageSize)]` + enrolar la familia CF en el guardrail (defense-in-depth §J2). |
| **F7** | GUARDRAIL | P3 | ✅ | **Sentinel de tokens incompleto**: `ConcurrencyTokenMappingGuardrailsTests` recorre todas las entidades pero el sentinel "must-be-present" no lista las 3 de CF → pasaría en vacío si se cayeran del modelo. | `ConcurrencyTokenMappingGuardrailsTests:43-48` | Agregar OPL/Conduct/Expectation al sentinel (drift-proof, per playbook). |
| **F8** | ARCH | P3 | ✅ | **Duplicación `ApplyAllowedActions`**: 4 overloads idénticos salvo el tipo de response. | `…Administration.cs:1706-1794` | Extraer a genérico (interfaz `IHasAllowedActions` o helper). Cleanup. |
| **F9** | ARCH | P3 (low) | ➖ | **Inconsistencia de código de error**: `CompetencyConductInUse` usa `"RESOURCE_IN_USE"` mientras OPL usa `"OCCUPATIONAL_PYRAMID_LEVEL_IN_USE"`. | `CompetencyFrameworkCommon.cs:64-67` | **WON'T-FIX (re-evaluado en PR-D):** `RESOURCE_IN_USE` es un código **compartido/localizado cross-module** — también lo usa `OrgStructureCatalogs` y tiene entrada en `BackendMessages.resx`/`.es.resx`. Conduct lo usa **correctamente**; renombrarlo fragmentaría la convención compartida, exigiría resx nuevo y rompería el contrato sin beneficio real. OPL es el outlier menor (código específico, aceptable). Se acepta como está. |
| **F10** | SEC | P3 (low) | ✅ | **Defense-in-depth de tenant**: los getters por public-id confían en el filtro global EF (verificado correcto). **No es vuln hoy.** | `CompetencyFrameworkRepository.cs:114,276,378` | **Cerrado vía guardrail (PR-E):** se descartó la opción literal (`.Where` explícito) por anti-patrón — ningún repo inyecta `ITenantContext`; el filtro global EF es la convención. Se fija con un guardrail drift-proof (`CompetencyFrameworkTenantScopeGuardrailsTests`) que asegura que las 5 entidades CF son `ITenantScopedEntity` (cubiertas por el filtro global). |
| **F11** | ARCH | P3 (low) | ✅ | **Dominio no auto-valida longitudes/regex** (confía en la capa app). Sin path de bypass real hoy (appliers+validators cubren), pero viola self-defending domain. | `OccupationalPyramidLevel.SetCode/SetName`, `CompetencyConduct.Update` | Opcional: guardas de longitud/regex en el dominio (endurecimiento DDD; sin bug vivo). |

---

## 4. Descartado / ya cumple (revisado — no son hallazgos)

| Tema | Resolución |
|---|---|
| ➖ IDOR cross-tenant | Mitigado por filtro global EF (`ApplicationDbContext:345-401`). Verificado. |
| ➖ Bare `[Authorize]` sin `[AuthorizationPolicySet]` | **Por diseño** (handler-gated documentado, como CompanyPreferences/SalaryTabulator; el guardrail authz lo excluye con `(?!CompetencyMatrix)`). |
| ➖ Tamaño por-operación de RFC-6902 | Cubierto por `[RequestSizeLimit(64KB)]` + `Validate` (≤1000). |
| ➖ Doble re-query before/after | **Intencional** (snapshots para audit). |
| ➖ Índice de búsqueda faltante | `LIKE '%x%'` es non-sargable por diseño aceptado (`project-foundation §12.8`, catálogos chicos/tenant). |
| ➖ `CompanyId` en el response | Es el id público del propio tenant del caller, no fuga. |
| ✅ Cumple | Tokens mapeados (`.IsConcurrencyToken()`), OpenAPI/`[SwaggerOperation]`, naming `*PublicId`, `AsNoTracking` en reads, paginación acotada (validator), export bounded por `SynchronousReadLimit`, sin fuga en errores. |

---

## 5. Plan de PRs sugerido

| PR | Hallazgos | Tema |
|---|---|---|
| **PR-A** ✅ | F1 + F4 | Perf: batch-resolución (mayor impacto). **Hecho 2026-06-05.** |
| **PR-B** ✅ | F2 | Funcional: GET matriz JSON + test (cierra código muerto y desbloquea el FE). **Hecho 2026-06-05.** |
| **PR-C** ✅ | F3 + F6 + F7 | Hardening: rate-limit export + `[Range]` + sentinel de tokens (cada uno con su guardrail). **Hecho 2026-06-05.** |
| **PR-D** ✅ | F5 + F8 (F9 ➖ won't-fix) | Cleanup: split god-file, dedupe `ApplyAllowedActions`. **Hecho 2026-06-05.** |
| **PR-E** ✅ | F10, F11 | Endurecimiento (F11 invariantes de longitud en el dominio; F10 guardrail de tenant-scope). **Hecho 2026-06-05.** |

---

## 6. Bitácora

| Fecha | Cambio |
|---|---|
| 2026-06-05 | Auditoría inicial (4 agentes: perf/seguridad/arquitectura/estándares + verificación adversarial del filtro de tenant). 11 hallazgos accionables (0 P1, 3 P2, 8 P3); 6 temas descartados como falsos positivos / por-diseño. Todos ⬜ pendientes. |
| 2026-06-05 | **PR-E — F10+F11 ✅; AUDIT CERRADO (10 corregidos + 1 won't-fix).** F11: invariantes de longitud en el dominio (`OccupationalPyramidLevel.MaxCode/Name/DescriptionLength`, `CompetencyConduct.MaxDescriptionLength`) con guardas en los setters; validators + appliers referencian esas constantes (single source of truth). Las guardas solo disparan ante un bug de capa (convierten un 500 de constraint de DB en `ArgumentException` clara) → sin cambio de comportamiento. F10: cerrado vía guardrail drift-proof `CompetencyFrameworkTenantScopeGuardrailsTests` (las 5 entidades CF son `ITenantScopedEntity` → cubiertas por el filtro global EF); se descartó la opción literal `.Where` explícito por anti-patrón (ningún repo inyecta `ITenantContext`). Sin migración. Verificado: build 0/0, unit 1598/0 (+5), CompetencyFramework integration 26/26. Pendiente: commit. |
| 2026-06-05 | **PR-D — F5+F8 ✅ resueltos; F9 ➖ won't-fix.** F5: god-file `CompetencyFrameworkAdministration.cs` (1953 líneas) dividido en 3 archivos por recurso (`OccupationalPyramidLevelAdministration` 519 / `CompetencyConductAdministration` 798 / `JobProfileCompetencyMatrixAdministration` 453); el original (189 líneas) queda solo con los helpers compartidos `CompetencyFrameworkPolicyAdapter` + `CompetencyFrameworkCatalogResolver`. 63 tipos preservados verbatim (0 agregados / 0 perdidos, verificado por git diff); mismo namespace → assembly-scanning de handlers intacto. F8: las 4 overloads idénticas de `ApplyAllowedActions` extraídas a `StandardAllowedActions` (la 5ª, matrix, queda aparte). F9: ➖ won't-fix (`RESOURCE_IN_USE` es convención compartida/localizada cross-module). Sin migración. Verificado: build 0/0, unit 1593/0, CompetencyFramework integration 26/26. Pendiente: commit. |
| 2026-06-05 | **PR-C — F3+F6+F7 ✅ resueltos (hardening).** F3: nueva clase `CompetencyFrameworkRateLimitPolicies` (Export 10/min + Search 120/min, espejo de PositionSlot/PersonnelFile) registrada en `Program.cs`; `[EnableRateLimiting(Export)]` en el matrix export + `[EnableRateLimiting(Search)]` en los 2 search; nuevo guardrail `CompetencyFrameworkRateLimitingGovernanceTests` + 2 integration `_ShouldRateLimit` (export→429, search→429). F6: `[Range(1, MaxPageSize)]` en los 2 `pageSize` + nuevo guardrail `CompetencyFrameworkPaginationGuardrailsTests` (espejo §J2). F7: las 3 entidades CF agregadas al sentinel de `ConcurrencyTokenMappingGuardrailsTests`. Sin migración. Verificado: build 0/0, unit 1593/0 (+5 guardrails), CompetencyFramework integration 26/26 (+2 rate-limit). Pendiente: commit. |
| 2026-06-05 | **PR-B — F2 ✅ resuelto.** Cableado `GET /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix` (despacha el `GetJobProfileCompetencyMatrixQuery` que estaba implementado pero huérfano) → la matriz se lee como JSON (items + conductos + `concurrencyToken` del `JobProfile` para el `If-Match`), ya no solo vía export xlsx. `ToActionResult` (token en body, consistente con los GET-by-id de OPL/Conduct), `[SwaggerOperation]`, `[ProducesStandardErrors(Read)]`; authz handler-gated sin cambios. Sin migración. Verificado: build 0/0, unit 1588/0, CompetencyFramework integration **24/24** (+3 tests: round-trip, matriz vacía→200, perfil inexistente→404). Pendiente: commit. |
| 2026-06-05 | **PR-A — F1+F4 ✅ resueltos.** N+1 eliminado vía batch-resolución: 3 métodos batch en el repositorio (`ResolveActive{OccupationalPyramidLevels,CompetencyConducts,CatalogItems}Async` → diccionario por `PublicId`, `AsNoTracking`, short-circuit en vacío) + 3 helpers `Resolve*FromMapAsync` en `CompetencyFrameworkCatalogResolver` (misma semántica `TenantMismatch`/`NotFound` en el miss). El update de matriz pre-carga 5 mapas → queries **constantes** vs `4N+ΣC`; el replace de behaviors pre-carga 1 mapa. Removidos los 2 resolvers single + 2 métodos repo single huérfanos (cero dead code). **Sin migración** (no cambia el modelo). Verificado: build 0/0, unit 1588/0, CompetencyFramework integration **21/21** (+1 test multi-ítem/multi-conducta que ejercita el path batch con N>1). Pendiente: commit. |
