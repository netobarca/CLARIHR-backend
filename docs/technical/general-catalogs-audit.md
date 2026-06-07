# Auditoría GeneralCatalogs — seguimiento

> **Documento vivo / tracker.** Se actualiza al cerrar cada hallazgo.
> **Creado:** 2026-06-06 · **Estado:** ✅ **Cerrada para acción** 2026-06-06 (GC1 ✅ · GC4 ✅ · GC2/GC3 ➖ by-design · GC5 ⏸️ diferido; 0 P1 · 0 P2 · 5 P3) · **Reauditado:** 2026-06-07 — sigue sólida, fixes intactos, seguridad re-verificada (sin hallazgos materiales); +GC6 ✅ (limpieza de usings muertos, PR-D) · **Owner:** equipo backend
> **Alcance:** `GeneralCatalogsController` (`api/v1/companies/{companyId}/general-catalogs/{key}` y `.../reference-catalogs/{key}`, 2 GET read-only) + las queries que despacha (`GetPersonnelCatalogItemsQuery` / `GetPersonnelReferenceCatalogItemsQuery`, en `Features/PersonnelFiles/Catalogs`) + sus handlers/authz (`IPersonnelFileAuthorizationService`) + repo (`GetCatalogItemsAsync`/`GetReferenceCatalogItemsAsync`) + `GeneralCatalogItems` + config EF.
> **Dimensiones:** seguridad · arquitectura · rendimiento, contra `AGENTS.md` (§8, §17) y `docs/technical/overview/project-foundation.md` (§11).

---

## 1. Veredicto

Controlador **read-only de catálogos**, **sin vulnerabilidades de seguridad** tras verificación adversarial. La deuda es de **alineación canónica, granularidad de autorización (acoplamiento a PersonnelFiles) y mantenibilidad** — todo P3.

- **Aislamiento de tenant VERIFICADO:** ambos handlers llaman `authorizationService.EnsureCanReadAsync(companyId)` → `PersonnelFileAuthorizationService` valida `companyId == tenantContext.TenantId` (TenantMismatch si difiere) **antes** de tocar la BD. El `companyId` del route **no** se confía → **sin IDOR cross-tenant**.
- **Catálogos system-scoped globales by-design** (educación/documento-tipos heredan de `SystemScopedCatalogItem`, sin `TenantId`); los country-scoped (idiomas, monedas, bancos, profesiones, municipios…) se filtran por el país de la **company autorizada**. Sin fuga cross-tenant.
- **Entrada validada:** `catalogKey` es un switch de **whitelist cerrada** (unknown → 400); `parentCode` validado (max 120 + formato) y acotado al país. Sin inyección.
- **Queries sanas:** `AsNoTracking`, proyección SQL-side, índices compuestos `(CountryCatalogItemId, IsActive, SortOrder)`; sin N+1.

Lo accionable (todo P3): **deriva canónica + no enrolado en guardrails, autz de lectura acoplada al permiso de PersonnelFiles, acoplamiento cross-feature, el switch `catalogKey→category` sin fuente única, y catálogos no cacheados.**

## 2. Leyenda de estado

⬜ pendiente · 🟡 en progreso · ✅ resuelto · ⏸️ diferido · ➖ descartado

| Severidad | P1 crítico | P2 alto | P3 medio/bajo |
|---|---|---|---|
| Conteo | 0 | 0 | 5 |

---

## 3. Hallazgos accionables

| # | Dim | Sev | Estado | Hallazgo | Evidencia (`file:line`) | Fix propuesto |
|---|-----|-----|--------|----------|-------------------------|---------------|
| **GC1** | ARCH | P3 | ✅ | **Deriva del patrón canónico + no enrolado en el guardrail OpenAPI.** Sin `[Tags]`, `[SwaggerOperation]`, `[ApiVersion]` (rutas `api/v1/...` hardcodeadas), `[ProducesStandardErrors]`, `[AuthorizationPolicySet]`; usa `[ProducesResponseType<ProblemDetails>]` por endpoint. No está en `OpenApiContractGuardrailsTests.Families[]` → regresión silenciosa de documentación. (Mismo patrón que Files FILE-3.) | `GeneralCatalogsController.cs:10-18,39-43`; `OpenApiContractGuardrailsTests.cs` (Families sin GeneralCatalogs) | **HECHO (PR-A):** `[Tags("General Catalogs")]` class-level + `[SwaggerOperation(Summary, Description)]` en ambos endpoints + familia `^GeneralCatalogs` enrolada en `OpenApiContractGuardrailsTests.Families`. `[AuthorizationPolicySet]` se mantiene fuera by-design (handler-gated, GC2 ➖) → **no** se añade a `GovernedFamilyRegex`; sin colisión de prefijo (`^GeneralCatalogs` matchea sólo el controller). **Diferido al plan de alineación canónica:** `[ApiVersion]`/route-template + `[ProducesStandardErrors]` + regenerar `openapi.yaml` desde swagger. |
| **GC2** | ARCH/SEC | P3 | ➖ | **Autz de lectura acoplada al permiso de PersonnelFiles.** Leer catálogos generales exige `IPersonnelFileAuthorizationService.EnsureCanReadAsync` → **permiso de personnel-files + módulo habilitado**. Pero estos catálogos (países, monedas, bancos, profesiones, idiomas…) los consumen **varias** features/dropdowns, no sólo personnel-files. Un usuario sin permiso de personnel-files que necesite un catálogo recibiría **403 falso**. **No es vuln** (es demasiado restrictivo, no demasiado permisivo). | `GeneralCatalogsController.cs:33-35,59-62`; handlers en `Features/PersonnelFiles/Catalogs`; `PersonnelFileAuthorizationService.cs:36-39` | **DESCARTADO (decisión de producto, 2026-06-06):** se mantiene el gate handler-gated de personnel-files **by-design**. Hoy los consumidores son flujos de personnel-files; el riesgo es de restrictividad (403 falso), no de seguridad. Si una feature futura necesita un catálogo sin permiso PF, reabrir con autz tenant-only o permiso `catalogs.read` dedicado (ver opciones evaluadas). |
| **GC3** | ARCH | P3 | ➖ | **Acoplamiento cross-feature.** `GeneralCatalogsController` (dominio/ruta propios) reutiliza queries y DTOs del feature **PersonnelFiles** (`GetPersonnelCatalogItemsQuery`, `PersonnelCatalogItemResponse`). Smell de capas; es la causa raíz de GC2. | `GeneralCatalogsController.cs:4,34,60` | **DESCARTADO (atado a GC2 ➖):** mientras la autz siga handler-gated en personnel-files, reutilizar sus queries/DTOs es coherente. Mover a un feature `GeneralCatalogs` propio sólo se justifica si se reabre GC2 (autz desacoplada). |
| **GC4** | ARCH | P3 | ✅ | **`catalogKey→category` sin fuente única + sin test de completitud.** Dos switches hardcodeados en el controller mapean keys a strings de categoría que se **duplican** en el repo y en una clase de constantes de categorías → riesgo de drift (renombrar una categoría no actualiza el switch). Ningún test valida que cada categoría de catálogo tenga su key. | `GeneralCatalogsController.cs:65-102`; repo `GetCatalogItemsAsync` (categorías); `PersonnelCurriculumCatalogCategories` | **HECHO (PR-A):** fuente única `GeneralCatalogKeyMap` (Application, `Features/PersonnelFiles/Catalogs/GeneralCatalogKeyMap.cs`) — dos diccionarios key↔categoría construidos sobre las **constantes** (`PersonnelCurriculumCatalogCategories`/`PersonnelReferenceCatalogCategories`), nunca literales. Se completaron las constantes faltantes (`Career`, `FileDocumentType`, `Bank`). El controller consume `TryResolveCatalogCategory`/`TryResolveReferenceCategory` (se eliminaron los dos `switch`). Guardrail `GeneralCatalogKeyMapGuardrailsTests` valida **bijección** constantes⇄mapa (ambas direcciones) + kebab/unicidad + trim/case-insensitive. (El `switch` UPPERCASE del repo queda como mapeo categoría→query EF, fuera del alcance del wire-key.) |
| **GC5** | PERF | P3 | ⏸️ | **Listas de catálogo no cacheadas.** Son datos de referencia estáticos que la UI pide en muchos form-loads, pero cada request va a la BD. Sólo el mapa de **resolución de nombres** se cachea (`IMemoryCache`, TTL 10 min); las listas de ítems no. Cada query es barata (AsNoTracking + proyección + índice), así que el impacto es bajo. | `PersonnelFileRepository.cs` (`GetCatalogItemsAsync`/`GetReferenceCatalogItemsAsync` sin cache; cache de nombres ~`:2514`) | **DIFERIDO (decisión, 2026-06-06):** no se cachea ahora; ROI marginal sin medir frecuencia real de fetch (queries ya con AsNoTracking + proyección SQL-side + índice compuesto). Reabrir con cache por `(category, countryCatalogItemId)` + TTL corto + invalidación en admin si la telemetría lo justifica. |

---

## 3-bis. Reauditoría 2026-06-07

**Veredicto: sigue sólida y cerrada — sin hallazgos nuevos de seguridad/correctitud.** Controlador read-only ya bien auditado; la reauditoría confirma todo y agrega 2 verificaciones que la auditoría original no hizo explícitas.

- **GC1 ✅ intacto** — `[Tags("General Catalogs")]` + `[SwaggerOperation]` en ambos GET + familia `^GeneralCatalogs` en `OpenApiContractGuardrailsTests`.
- **GC4 ✅ intacto** — `GeneralCatalogKeyMap` single-source (14+6 keys sobre constantes, nunca literales) + bijección `GeneralCatalogKeyMapGuardrailsTests`.
- **Seguridad re-verificada sólida** — ambos handlers llaman `EnsureCanReadAsync(companyId)` **antes** del repo (`PersonnelReferenceCatalogs.cs:375,395`); sin IDOR. **2 chequeos nuevos (lección de Files):** (a) **sin ruta de exposición adyacente** — las queries `GetPersonnelCatalogItemsQuery`/`...Reference...` y los métodos de repo `GetCatalogItemsAsync`/`GetReferenceCatalogItemsAsync` se invocan **solo** desde GeneralCatalogs (+ validación interna de personnel-files, ya gateada); no hay un 2.º endpoint que sirva estos datos sin authz. (b) **superficie de escritura (Backoffice) correctamente gateada** — `BankCatalogs`/`SystemCatalogs`/`EducationCatalogs`/`DocumentTypeCatalogs`Controller corren bajo la `FallbackPolicy` `client_type=Platform` (solo operadores de plataforma); no está abierta. (Dominio Ola 3 aparte — solo se confirmó el gate.)
- **GC2/GC3 ➖, GC5 ⏸️ sin cambios** (no apareció consumidor sin permiso PF que dispare el re-open de GC2; queries siguen baratas). **Rate-limiting ➖ descartado con criterio:** reads de listas pequeñas de referencia que todo usuario legítimo pide en cada form-load — perfil de abuso distinto al de Search/Export (caros/abusables); limitarlos rompería UX, valor de abuso mínimo (queries indexadas, sin mutación ni enumeración sensible).

| # | Dim | Sev | Estado | Hallazgo | Fix |
|---|-----|-----|--------|----------|-----|
| **GC6** | ARCH | P3-trivial | ✅ | **~22 `using` muertos** en `PersonnelReferenceCatalogs.cs` (Auditing, Banks, Files, EducationCatalogs, DocumentTypeCatalogs, JsonPatch, Pagination, Text.Json, Tenancy, Policies…), heredados de un split de god-file. Cero impacto funcional (build 0/0, no son warning), pero ensucian y aparentan un acoplamiento cross-feature falso. | **PR-D:** eliminados los 22 sin uso; quedan los 7 reales (`Abstractions.PersonnelFiles`, `Common.CQRS`, `Common.Errors`, `Features.Locations.Common`, `Features.PersonnelFiles.Common`, `Domain.PersonnelFiles`, `FluentValidation`). Auto-verificado por compilación. |

**Verificación:** build **0/0** · unit **1678/0** (incl. guardrails GeneralCatalogs) · integración de catálogos **56/58** (2 skip pre-existentes de RBAC-backfill, ajenos). **Sin migración.**

---

## 4. Descartado / ya cumple (verificado — no son hallazgos)

| Tema | Resolución |
|---|---|
| ➖ IDOR / aislamiento de tenant | Verificado: `EnsureCanReadAsync(companyId)` valida `companyId == tenantContext.TenantId` (TenantMismatch) antes de la BD; `companyId` del route no se confía. |
| ➖ Catálogos system-scoped "fuga cross-tenant" | By-design: educación/document-types heredan de `SystemScopedCatalogItem` (sin `TenantId`), son globales; country-scoped se filtran por el país de la company autorizada. Ver [[catalog-type-descriptor-system-scoped]]. |
| ➖ Inyección vía `catalogKey` | Whitelist cerrada (switch → string fijo); unknown → 400. La categoría no se interpola en SQL. |
| ➖ `parentCode` (inyección / unbounded / cross-tenant) | Validado (max 120 + formato `IsValidCode`), comparado vía EF `.Where` (sin SQL crudo), acotado al país de la company autorizada. |
| ➖ Eficiencia de query | `AsNoTracking`, proyección SQL-side al DTO, filtros SQL-side, índices `(CountryCatalogItemId, IsActive, SortOrder)` + `(CountryCatalogItemId, NormalizedCode)` único. Sin N+1, sin materialización ilimitada (catálogos pequeños). |
| ➖ HTTP semántica de key desconocida | `400 Bad Request` (validación) — correcto (no 404). |
| ✅ Cumple | Read-only sin mutaciones; controller pequeño y enfocado (sin god-file ni código muerto); CQRS limpio (controller→handler→repo). |

---

## 5. Plan de PRs sugerido

| PR | Hallazgos | Tema | Estado |
|---|---|---|---|
| **PR-A** | GC1 + GC4 | Contrato/mantenibilidad: `[Tags]`/`[SwaggerOperation]` + enrolar guardrail OpenAPI + fuente única `GeneralCatalogKeyMap` + test de completitud del mapeo key↔category. | ✅ HECHO |
| **Decisión** | GC2 (+ GC3) | Producto/arquitectura: ¿los catálogos son sólo de personnel-files (by-design) o cross-feature? | ✅ Resuelta → **by-design** (➖ GC2/GC3) |
| **PR-C** | GC5 | Perf (opcional): cachear listas por `(category, country)` si la frecuencia de fetch lo justifica. | ⏸️ Diferido |
| **PR-D** | GC6 | Limpieza (reauditoría): eliminar ~22 `using` muertos en `PersonnelReferenceCatalogs.cs` (leftover de god-file split). | ✅ HECHO 2026-06-07 (uncommitted) |

---

## 6. Bitácora

| Fecha | Cambio |
|---|---|
| 2026-06-07 | **Reauditoría ✅ — sigue sólida y cerrada; sin hallazgos nuevos de seguridad/correctitud.** GC1/GC4 verificados intactos. Seguridad re-verificada con 2 chequeos nuevos (lección de Files): (a) las queries y métodos de repo de catálogo se invocan **solo** desde GeneralCatalogs — sin ruta de exposición adyacente sin authz; (b) la superficie de escritura del Backoffice (`BankCatalogs`/`SystemCatalogs`/`EducationCatalogs`/`DocumentTypeCatalogs`Controller) está platform-gated (`FallbackPolicy` `client_type=Platform`). GC2/GC3 ➖ y GC5 ⏸️ sin cambios; rate-limiting ➖ descartado con criterio (reads de referencia frecuentes y legítimos, perfil de abuso distinto a Search/Export). **GC6 ✅ (PR-D):** eliminados ~22 `using` muertos en `PersonnelReferenceCatalogs.cs` (leftover de god-file split; quedan 7 reales), auto-verificado por compilación. Build 0/0, unit 1678/0, integración de catálogos 56/58 (2 skip pre-existentes). Sin migración. |
| 2026-06-06 | Auditoría inicial (2 agentes Explore: seguridad + perf/arquitectura, con verificación adversarial). Veredicto: read-only, **sin vulnerabilidades** (IDOR descartado — `companyId` validado contra el tenant; catálogos system-scoped globales by-design; whitelist de keys; parentCode validado/acotado). Severidades de arquitectura recalibradas de "HIGH" (etiqueta de los agentes) a **P3** (governance/docs/consistencia, no seguridad/correctitud). 5 hallazgos accionables (0 P1, 0 P2, 5 P3): GC1 deriva canónica + guardrail, GC2 autz acoplada a personnel-files (posibles 403 falsos cross-feature; decisión de producto), GC3 acoplamiento cross-feature, GC4 switch key→category sin fuente única, GC5 catálogos no cacheados. Todos ⬜ pendientes. |
| 2026-06-06 | **Cerrada para acción (PR-A + decisiones).** **GC1 ✅** — `[Tags("General Catalogs")]` + `[SwaggerOperation]` en ambos GET + familia `^GeneralCatalogs` enrolada en `OpenApiContractGuardrailsTests` (handler-gated → fuera de `GovernedFamilyRegex` by-design). **GC4 ✅** — fuente única `GeneralCatalogKeyMap` (key↔categoría sobre constantes, nunca literales); completadas constantes `Career`/`FileDocumentType`/`Bank`; controller consume el mapa (eliminados los 2 `switch`); guardrail `GeneralCatalogKeyMapGuardrailsTests` valida bijección constantes⇄mapa + kebab/unicidad + trim/case-insensitive. **GC2/GC3 ➖** — decisión de producto: autz handler-gated en personnel-files se mantiene **by-design** (riesgo = restrictividad, no seguridad). **GC5 ⏸️** — diferido (ROI marginal sin medir frecuencia de fetch; queries ya baratas). Verificación: build 0/0 · unit 1633/0 (incl. 54 de guardrails) · integración de catálogos 4/4 (bancos 2, reference/identification-types 2). Wire-keys preservados (sin cambios en tests de integración). |
