# Auditoría CompetencyFramework — seguimiento

> **Documento vivo / tracker.** Se actualiza al cerrar cada hallazgo.
> **Creado:** 2026-06-05 · **Reauditado:** 2026-06-06 · **Estado:** ✅ CERRADO (11/11 + R1/R2 de la reauditoría = 13/13 corregidos) · **Owner:** equipo backend
> **Alcance:** dominio CompetencyFramework completo — 3 controladores (`OccupationalPyramidLevelsController`, `CompetencyConductsController`, `JobProfileCompetencyMatrixController`) + capa Application (`Features/CompetencyFramework/`) + repositorio + dominio + EF config.
> **Dimensiones:** seguridad · performance · arquitectura, contra los estándares definidos en `AGENTS.md` (§17) y `docs/technical/overview/project-foundation.md`.

---

## 1. Veredicto

Dominio **maduro y canónico** (alineado a los 15 criterios — ver `api-canonical-alignment-plan.md` §7). **Cero vulnerabilidades críticas/altas:**

- **Aislamiento de tenant verificado** — `ApplicationDbContext.ApplyTenantFilters` aplica `HasQueryFilter(... TenantId == CurrentTenantIdOrDefault)` a todo `ITenantScopedEntity`; las 3 entidades CF extienden `TenantEntity` → los lookups por public-id ya están tenant-scoped y el patrón `*ExistsOutsideTenantAsync` (404 vs 403) es correcto. **No hay IDOR cross-tenant.**
- **Authz handler-gated correcto** en los 12 handlers (`ICompetencyFrameworkAuthorizationService.EnsureCanRead/ManageAsync`), read-gates en GET, manage-gates en mutaciones.
- **Deny-lists de PATCH completas**, RFC-6902 endurecido (`[RequestSizeLimit]` + max-ops), sin inyección (`LIKE` parametrizado), sin fuga de datos en errores, concurrencia OK.

Lo accionable es **performance (N+1), un hueco funcional (lectura de la matriz) y endurecimiento (rate-limit, guardrails, dedupe)** — todo **P2/P3**.

**Reauditoría 2026-06-06:** doble pase — (A) anti-regresión: los 11 fixes (F1-F11) siguen presentes y verificados en el código actual (incl. los 4 guardrails: rate-limit, paginación, tenant-scope, sentinel de tokens). (B) Pase adversarial fresco sobre los 3 controladores + handlers + repo + dominio + appliers PATCH. Re-confirmado: aislamiento de tenant sólido (getters confían en el filtro global EF; `*ExistsOutsideTenantAsync` es el único `IgnoreQueryFilters`, comentado; mutaciones filtran `TenantId` explícito), authz handler-gated en los 12 handlers, tokens de concurrencia reales (rotan en cada mutación), **PATCH deny-lists completas**, el *miss* de la resolución batch falla rápido (sin N+1). **Sin nuevas brechas de seguridad.** Dos mejoras nuevas, **ambas resueltas en la misma reauditoría: R1 (perf/hardening, P3) + R2 (consistencia, P3-low).**

## 2. Leyenda de estado

⬜ pendiente · 🟡 en progreso · ✅ resuelto · ⏸️ diferido (backlog) · ➖ descartado

| Severidad | P1 crítico | P2 alto | P3 medio/bajo |
|---|---|---|---|
| Conteo | 0 | 3 | 10 |

*(P3 incluye R1 y R2, añadidos en la reauditoría 2026-06-06 y resueltos en PR-G.)*

**Progreso:** **13/13 corregidos = AUDIT COMPLETO (0 pendientes).** F1,F4 (PR-A) · F2 (PR-B) · F3,F6,F7 (PR-C) · F5,F8 (PR-D) · F10,F11 (PR-E) · F9 (PR-F), 2026-06-05; **R1,R2 (PR-G), 2026-06-06 (reauditoría).**

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
| **F9** | ARCH | P3 (low) | ✅ | **Inconsistencia de código de error**: `CompetencyConductInUse` usaba el genérico `"RESOURCE_IN_USE"` mientras **12 recursos** (incl. su hermano OPL) usan código específico `<RECURSO>_IN_USE`. | `CompetencyFrameworkCommon.cs:64-67` | **Corregido (PR-F):** renombrado a `COMPETENCY_CONDUCT_IN_USE` (+ resx en/es), alineado con la convención dominante. Bonus bug-fix: el mensaje resx de `RESOURCE_IN_USE` era específico de conducta → ahora genérico (corrige el mensaje equivocado en `OrgStructureCatalogs`, el único que aún lo usa). **Nota:** el "won't-fix" de PR-D estaba mal fundamentado — un censo de los `*_IN_USE` mostró que los códigos específicos SON la norma (12 vs 2), así que la conducta era el outlier. |
| **F10** | SEC | P3 (low) | ✅ | **Defense-in-depth de tenant**: los getters por public-id confían en el filtro global EF (verificado correcto). **No es vuln hoy.** | `CompetencyFrameworkRepository.cs:114,276,378` | **Cerrado vía guardrail (PR-E):** se descartó la opción literal (`.Where` explícito) por anti-patrón — ningún repo inyecta `ITenantContext`; el filtro global EF es la convención. Se fija con un guardrail drift-proof (`CompetencyFrameworkTenantScopeGuardrailsTests`) que asegura que las 5 entidades CF son `ITenantScopedEntity` (cubiertas por el filtro global). |
| **F11** | ARCH | P3 (low) | ✅ | **Dominio no auto-valida longitudes/regex** (confía en la capa app). Sin path de bypass real hoy (appliers+validators cubren), pero viola self-defending domain. | `OccupationalPyramidLevel.SetCode/SetName`, `CompetencyConduct.Update` | Opcional: guardas de longitud/regex en el dominio (endurecimiento DDD; sin bug vivo). |
| **R1** | PERF | P3 | ✅ | **Mutaciones *replace* de colección sin tope de tamaño** (hallazgo de la reauditoría 2026-06-06). El replace de matriz (`Items` + `ConductIds` anidados) y el de behaviors aceptan colecciones ilimitadas — sin `.Must(Count <= N)` en sus validators. El F1 original anotó "Sin tope de ítems", pero PR-A sólo eliminó el N+1; el conteo ilimitado quedó. El codebase **sí tiene convención** (`ReplaceCurrentUserSocialLinks` → `.Must(items.Count <= 10)`); CF era el outlier. Riesgo: un caller con `CompetencyFramework.Admin` envía una colección enorme (sólo acotada por Kestrel ~30MB) → build en memoria de millones de entidades + INSERT masivo. Acotado intra-tenant + caller privilegiado → P3. | `JobProfileCompetencyMatrixAdministration.cs` (validators), `CompetencyConductAdministration.cs` (validator behaviors) | **Resuelto (PR-G · 2026-06-06):** constantes `CompetencyFrameworkValidationRules.MaxMatrixItems=200 / MaxConductsPerMatrixItem=50 / MaxBehaviorsPerConduct=50` (single source of truth) + `.Must(Count <= …)` en los 3 puntos, espejo de `ReplaceCurrentUserSocialLinks`. Anclado por `CompetencyFrameworkCollectionCapGuardrailTests` (drift-proof: Max+1 rechazado / Max aceptado, construido desde la constante). Mensajes de cap con entradas resx en/es (las exige `BackendMessageLocalizationTests`). |
| **R2** | ARCH | P3 (low) | ✅ | **Código de error impreciso en behaviors** (hallazgo de la reauditoría 2026-06-06). El handler de `UpdateCompetencyConductBehaviors` devolvía `JobProfileCompetencyMatrixConflict` ("…competency **matrix** change is not valid…") ante un behavior **duplicado** en el request — un código de *matriz* reusado en un endpoint de *conducts*. Adyacente a F9 (precisión de códigos). | `CompetencyConductAdministration.cs` (dedupe de behaviors) | **Resuelto (PR-G · 2026-06-06):** nuevo error específico `COMPETENCY_CONDUCT_BEHAVIOR_DUPLICATE` (Conflict, + resx en/es) usado en el dedupe. Anclado por integración `CompetencyConductBehaviors_WhenBehaviorIsDuplicated_ShouldReturn409WithSpecificCode` (provider real, behavior duplicado → 409 con el código específico). |

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
| **PR-F** ✅ | F9 | Precisión de código de error (`RESOURCE_IN_USE`→`COMPETENCY_CONDUCT_IN_USE`, reabierto desde won't-fix). **Hecho 2026-06-05.** |
| **PR-G** ✅ | R1, R2 | Reauditoría: cap de colecciones (matriz/conductas/behaviors) + código específico `COMPETENCY_CONDUCT_BEHAVIOR_DUPLICATE`. **Hecho 2026-06-06.** |

---

## 6. Bitácora

| Fecha | Cambio |
|---|---|
| 2026-06-06 | **Reauditoría + PR-G (R1+R2) ✅.** Doble pase a petición: (A) anti-regresión — los 11 fixes (F1-F11) verificados presentes en el código actual, incl. los 4 guardrails. (B) Pase adversarial fresco sobre los 3 controladores + handlers + repo + dominio + appliers PATCH. Re-confirmado sin nuevas brechas de seguridad (tenant-scope, authz handler-gated, deny-lists PATCH, miss-de-batch sin N+1). Dos mejoras nuevas resueltas: **R1 (perf/hardening)** — el replace de matriz (`Items`+`ConductIds`) y el de behaviors no acotaban el conteo (el F1 anotó "Sin tope de ítems" pero PR-A sólo quitó el N+1); CF era el outlier vs la convención `ReplaceCurrentUserSocialLinks` `.Must(Count <= N)`. Añadidas constantes `MaxMatrixItems=200/MaxConductsPerMatrixItem=50/MaxBehaviorsPerConduct=50` + `.Must(Count <= …)` (drift-proof `CompetencyFrameworkCollectionCapGuardrailTests`) + mensajes resx en/es. **R2 (consistencia)** — el dedupe de behaviors devolvía `JobProfileCompetencyMatrixConflict` (código de matriz) en un endpoint de conducts; nuevo `COMPETENCY_CONDUCT_BEHAVIOR_DUPLICATE` (+ resx en/es) + integración `CompetencyConductBehaviors_WhenBehaviorIsDuplicated_…`. Verificado: build 0/0, unit 1639/0, CF integración 23/23 (+1 R2). |
| 2026-06-05 | Auditoría inicial (4 agentes: perf/seguridad/arquitectura/estándares + verificación adversarial del filtro de tenant). 11 hallazgos accionables (0 P1, 3 P2, 8 P3); 6 temas descartados como falsos positivos / por-diseño. Todos ⬜ pendientes. |
| 2026-06-05 | **PR-F — F9 ✅ corregido (reabierto desde won't-fix); AUDIT 11/11.** El "won't-fix" de PR-D estaba mal fundamentado: un censo de los códigos `*_IN_USE` muestra **12 recursos con código específico** `<RECURSO>_IN_USE` (incl. el hermano OPL) y **solo 2 con el genérico** `RESOURCE_IN_USE` → la conducta era el outlier, no la norma. Renombrado `CompetencyConductInUse` `RESOURCE_IN_USE`→`COMPETENCY_CONDUCT_IN_USE` + entradas resx en/es. **Bonus bug-fix:** el mensaje resx de `RESOURCE_IN_USE` era específico de conducta (se mostraba mal en `OrgStructureCatalogs`, el único que aún lo usa) → ahora genérico. Breaking: clientes que key-eaban `RESOURCE_IN_USE` para conductas → `COMPETENCY_CONDUCT_IN_USE` (coordinar FE). Sin migración. Verificado: build 0/0, unit 1598/0 (localization guardrail verde), CF+OrgStructure integration 28/28. Pendiente: commit. |
| 2026-06-05 | **PR-E — F10+F11 ✅; AUDIT CERRADO (10 corregidos + 1 won't-fix).** F11: invariantes de longitud en el dominio (`OccupationalPyramidLevel.MaxCode/Name/DescriptionLength`, `CompetencyConduct.MaxDescriptionLength`) con guardas en los setters; validators + appliers referencian esas constantes (single source of truth). Las guardas solo disparan ante un bug de capa (convierten un 500 de constraint de DB en `ArgumentException` clara) → sin cambio de comportamiento. F10: cerrado vía guardrail drift-proof `CompetencyFrameworkTenantScopeGuardrailsTests` (las 5 entidades CF son `ITenantScopedEntity` → cubiertas por el filtro global EF); se descartó la opción literal `.Where` explícito por anti-patrón (ningún repo inyecta `ITenantContext`). Sin migración. Verificado: build 0/0, unit 1598/0 (+5), CompetencyFramework integration 26/26. Pendiente: commit. |
| 2026-06-05 | **PR-D — F5+F8 ✅ resueltos; F9 ➖ won't-fix.** F5: god-file `CompetencyFrameworkAdministration.cs` (1953 líneas) dividido en 3 archivos por recurso (`OccupationalPyramidLevelAdministration` 519 / `CompetencyConductAdministration` 798 / `JobProfileCompetencyMatrixAdministration` 453); el original (189 líneas) queda solo con los helpers compartidos `CompetencyFrameworkPolicyAdapter` + `CompetencyFrameworkCatalogResolver`. 63 tipos preservados verbatim (0 agregados / 0 perdidos, verificado por git diff); mismo namespace → assembly-scanning de handlers intacto. F8: las 4 overloads idénticas de `ApplyAllowedActions` extraídas a `StandardAllowedActions` (la 5ª, matrix, queda aparte). F9: ➖ won't-fix (`RESOURCE_IN_USE` es convención compartida/localizada cross-module). Sin migración. Verificado: build 0/0, unit 1593/0, CompetencyFramework integration 26/26. Pendiente: commit. |
| 2026-06-05 | **PR-C — F3+F6+F7 ✅ resueltos (hardening).** F3: nueva clase `CompetencyFrameworkRateLimitPolicies` (Export 10/min + Search 120/min, espejo de PositionSlot/PersonnelFile) registrada en `Program.cs`; `[EnableRateLimiting(Export)]` en el matrix export + `[EnableRateLimiting(Search)]` en los 2 search; nuevo guardrail `CompetencyFrameworkRateLimitingGovernanceTests` + 2 integration `_ShouldRateLimit` (export→429, search→429). F6: `[Range(1, MaxPageSize)]` en los 2 `pageSize` + nuevo guardrail `CompetencyFrameworkPaginationGuardrailsTests` (espejo §J2). F7: las 3 entidades CF agregadas al sentinel de `ConcurrencyTokenMappingGuardrailsTests`. Sin migración. Verificado: build 0/0, unit 1593/0 (+5 guardrails), CompetencyFramework integration 26/26 (+2 rate-limit). Pendiente: commit. |
| 2026-06-05 | **PR-B — F2 ✅ resuelto.** Cableado `GET /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix` (despacha el `GetJobProfileCompetencyMatrixQuery` que estaba implementado pero huérfano) → la matriz se lee como JSON (items + conductos + `concurrencyToken` del `JobProfile` para el `If-Match`), ya no solo vía export xlsx. `ToActionResult` (token en body, consistente con los GET-by-id de OPL/Conduct), `[SwaggerOperation]`, `[ProducesStandardErrors(Read)]`; authz handler-gated sin cambios. Sin migración. Verificado: build 0/0, unit 1588/0, CompetencyFramework integration **24/24** (+3 tests: round-trip, matriz vacía→200, perfil inexistente→404). Pendiente: commit. |
| 2026-06-05 | **PR-A — F1+F4 ✅ resueltos.** N+1 eliminado vía batch-resolución: 3 métodos batch en el repositorio (`ResolveActive{OccupationalPyramidLevels,CompetencyConducts,CatalogItems}Async` → diccionario por `PublicId`, `AsNoTracking`, short-circuit en vacío) + 3 helpers `Resolve*FromMapAsync` en `CompetencyFrameworkCatalogResolver` (misma semántica `TenantMismatch`/`NotFound` en el miss). El update de matriz pre-carga 5 mapas → queries **constantes** vs `4N+ΣC`; el replace de behaviors pre-carga 1 mapa. Removidos los 2 resolvers single + 2 métodos repo single huérfanos (cero dead code). **Sin migración** (no cambia el modelo). Verificado: build 0/0, unit 1588/0, CompetencyFramework integration **21/21** (+1 test multi-ítem/multi-conducta que ejercita el path batch con N>1). Pendiente: commit. |
