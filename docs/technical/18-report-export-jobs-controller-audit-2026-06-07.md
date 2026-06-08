# Auditoría Técnica por Controlador — ReportExportJobsController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Contexto previo: el sistema de export async fue endurecido en la auditoría de Files (REX-1/2/3 = autz por-recurso en download/getById/cancel).

## 1. Resumen ejecutivo

`ReportExportJobsController` gestiona la **cola de trabajos de exportación de reportes asíncronos**: 5 endpoints (Create `202`, Search, GetById, Download, Cancel). Un job referencia un **resourceKey** (PersonnelFiles, OrgUnits, PositionSlots, SalaryTabulator, CostCenters, LegalRepresentatives, JobProfileCompetencyMatrix, JobProfilePdf) y un formato; un worker en background lo procesa (lease/retry/expiración) y produce un artefacto descargable. La autorización es **delegada por-recurso** (no un par de políticas único), lo que explica la ausencia de `[AuthorizationPolicySet]` — es **por diseño**.

Veredicto: **APROBADO CON OBSERVACIONES**. 0 críticos / 0 altos. **Dos hallazgos Media:** (REX-A) `Search` es el **único** read-ish op **no gated por-recurso** → fuga de **metadata** intra-tenant (cualquier usuario del tenant ve resourceKey/fileName/rowCount/errores de exports de recursos que no puede leer); (REX-B) **sin `[Tags]`** + naming semánticamente inconsistente con los demás controllers (lo solicitado). Las fortalezas son notables: autz por-recurso single-source (`ReportExportResourceAuthorizer`, REX-1), manejo de PII §N2/§N3 (strip del `includeCompensation` del cliente + stamp server-side), máquina de estados limpia con claiming por-lease, índices a medida, y un test de integración negativo fuerte (intruder→403 en download/getById/cancel).

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila |
| Unit tests (ReportExport + guardrails, **ejecutados**) | ✅ 25/25 passed |
| Integration tests (revisados, no ejecutados) | 5 (incl. negativo-autz intruder→403) |
| Enrolamiento en guardrails de familia | **~1/5** (concurrency auto; sin OpenAPI/authz/paginación/rate-limit) |
| Hallazgos | 0 Crít · 0 Alto · **2 Media · 2 Baja · 4 Observación** |

## 2. Alcance

**Incluido:** controlador `ReportExportJobsController.cs`; aplicación `ReportExportJobAdministration.cs` (DTOs, 5 commands/queries, validators, 5 handlers, `ReportExportJobMapper`), `ReportExportResourceAuthorizer.cs`, `ReportExportResources.cs`, `ReportExportResourceFormatCompatibility.cs`; dominio `ReportExportJob.cs` + `ReportExportJobStatus.cs`; persistencia `IReportExportJobRepository` + `ReportExportJobRepository`, EF `ReportExportJobConfiguration`; seguridad `IReportExportResourceAuthorizer` (8 servicios de autz delegados); entrega `IFileStorageProviderResolver`/`IFilePurposeRuleProvider` (download). Hermano `ReportsController` revisado por contexto de naming.

**Excluido:** el pipeline de background (`ReportExportJobProcessor`/`Generator`/`BackgroundService`) salvo por su interacción con el dominio; los handlers de export por-recurso (`*ExportHandler`); auditoría integral; pruebas de carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en autorización (delegada por-recurso, consistencia entre los 5 ops), aislamiento por tenant, manejo de PII, concurrencia y adherencia canónica/naming. Evidencia: 25 unit tests ejecutados (verde). Integración revisada por código (requiere DB; no ejecutada → limitación). **Nota:** durante la sesión hubo una edición en curso no relacionada en Locations que rompió el build transitoriamente; estable al ejecutar las pruebas.

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Autz | Handler |
|---|---|---|---|---|---|
| 1 | POST | `/companies/{companyId}/report-export-jobs` | Encolar export (202 + Location) | **por-recurso** (read) + tenant | `CreateReportExportJobCommandHandler` |
| 2 | GET | `/companies/{companyId}/report-export-jobs` | Listar jobs (paginado, filtro status) | **solo tenant** (REX-A) | `SearchReportExportJobsQueryHandler` |
| 3 | GET | `/report-export-jobs/{jobId}` | Get job por id | **por-recurso** (REX-2) | `GetReportExportJobQueryHandler` |
| 4 | GET | `/report-export-jobs/{jobId}/download` | Descargar artefacto (stream blob) | **por-recurso** (REX-1) + status/expiry | `GetReportExportJobDownloadQueryHandler` |
| 5 | PATCH | `/report-export-jobs/{jobId}/cancel` | Cancelar (body token) | **por-recurso** (REX-3) + 409 token | `CancelReportExportJobCommandHandler` |

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / DTOs | PASS (nit) | Delgado salvo storage/streaming en Download (REX-G) |
| Arquitectura | Dominio (máquina de estados) | PASS | `ReportExportJob`: Queued→Running→Succeeded/Failed/Cancelled/Expired; lease/retry/expiry |
| Seguridad | Autenticación | PASS | `[Authorize]` |
| Seguridad | Autz por-recurso (Create/Get/Download/Cancel) | PASS | `ReportExportResourceAuthorizer.EnsureCanReadResourceAsync` (8 recursos); REX-1/2/3 |
| Seguridad | **Autz en Search** | **FAIL** | **REX-A**: solo tenant; sin filtro por-recurso ni por-usuario → fuga de metadata |
| Seguridad | Tenant isolation | PASS | Handlers verifican `tenantContext == companyId`; repo filtra por tenant; worker usa `IgnoreQueryFilters` documentado |
| Seguridad | Manejo de PII | PASS (fuerte) | §N2/§N3: strip de `includeCompensation` del cliente (todas las casings) + stamp server-side según manage-profiles |
| Seguridad | Mass assignment | PASS | DTOs cerrados; ParametersJson validado (JSON object ≤20KB); resource/format whitelisted |
| Seguridad | Authz negativa testeada | PASS (parcial) | Integ intruder→403 en download/getById/cancel; **NO** cubre Search (REX-A) |
| Contrato | Versionado `[ApiVersion]` | FAIL | **REX-C**: literal `api/v1`, sin `[ApiVersion]` |
| Contrato | `[Tags]` (Swagger group) | **FAIL** | **REX-B**: sin `[Tags]` (igual que `ReportsController`) |
| Contrato | `[SwaggerOperation]` / `[ProducesStandardErrors]` | FAIL | **REX-C**: ausentes; `[ProducesResponseType]` inline |
| Contrato | `[AuthorizationPolicySet]` | NO APLICA | Autz delegada por-recurso (no un par de políticas) — **por diseño** |
| Contrato | Create async semantics | PASS | `202 Accepted` + `Location` |
| Contrato | Paginación: page size | PASS (nit) | Validator `InclusiveBetween(1,100)`; **REX-F**: sin `[Range]` en el param |
| Contrato | ETag / If-Match | FAIL | **REX-D**: Cancel usa token en body, no `If-Match` |
| Rendimiento | Índices | PASS | `(tenant,queued,public)`, `(tenant,status,queued)`, `(status,lease,queued)` worker-claim, `(status,expires)` |
| Rendimiento | Rate limit | OBS | **REX-E**: Search/Download sin rate-limit (download acotado por worker) |
| Concurrencia | Optimista + 409 | PASS | Token rotado en cada transición; Cancel check manual→409; `.IsConcurrencyToken()` (auto-guardrail) |
| Concurrencia | Worker claiming | PASS | Lease-based (`CanBeClaimed`/`LeaseUntilUtc`); retry con `Attempts<maxAttempts` |
| Concurrencia | Cancel de job terminal | OBS | **REX-H**: `Cancel()` no-op idempotente → 200 (no 409) si no cancelable |
| Observabilidad | Telemetría/auditoría | PASS | `ReportExportTelemetryEvents`; export audita vía `LogExportAsync` (en los sync); jobs con estados trazables |
| Pruebas | Unit | PASS | 25 verdes (dominio/validator/file-writer/governance/dispatch/telemetry/format-compat) |
| Pruebas | Integración | PASS (gap) | 5 métodos incl. intruder→403; **sin** caso negativo de Search (REX-A) |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS limpia. Dominio `ReportExportJob` = máquina de estados robusta (Queued/Running/Succeeded/Failed/Cancelled/Expired) con claiming por-lease para el worker (`CanBeClaimed` + `LeaseUntilUtc`), retry (`MarkProcessingFailed` re-encola si `Attempts<max`), y expiración de artefactos. `ReportExportResourceAuthorizer` es un single-source de "quién puede leer cada recurso" (REX-1) que despacha a 8 servicios de autz. Única desviación: el Download resuelve rule/provider/container y abre el stream en el controller (REX-G; parcialmente apropiado como delivery HTTP).

### 6.2 Seguridad
**Modelo delegado por-recurso, mayormente sólido.** Create/GetById/Download/Cancel invocan `EnsureCanReadResourceAsync(resourceKey, companyId)` → la autz del recurso real (REX-1/2/3 single-source, anti-drift). **Manejo de PII ejemplar (§N2/§N3)**: en el job-profile PDF, el `includeCompensation` del cliente se elimina (en todas las variantes de casing) y se sella server-side sólo si el usuario puede gestionar perfiles — el worker no tiene contexto de usuario, así que la decisión se toma aquí; impide filtrar salarios vía parámetros controlados por el cliente. El concurrency token NO es autorización (se entrega a todo usuario vía Search/GetById) y Cancel lo respalda con el gate por-recurso (REX-3).

**Brecha (REX-A):** `Search` es el **único** de los 4 read-ish ops que **no** aplica autz por-recurso — sólo verifica tenant. `SearchAsync` filtra por `TenantId` (+ status opcional), sin filtrar por recurso ni por `RequestedByUserId` (campo que existe en el dominio pero no se usa). Resultado: cualquier usuario autenticado del tenant lista la **metadata** de TODOS los jobs — `resourceKey`, `fileName`, `rowCount`, `sizeBytes`, `lastErrorCode/Message`, timestamps — incluso de recursos sensibles que no puede leer (p.ej. ver que existe `salary-tabulator-2026.xlsx` con 5.000 filas). La **data** del export sí está protegida (Download gated), pero la metadata se filtra a través de la misma frontera de autz que GetById/Download sí respetan. El test de integración negativo cubre download/getById/cancel→403 pero **no** Search.

### 6.3 Contrato API y Naming (lo solicitado)
**REX-B — naming/agrupación.** El censo de los 47 controllers muestra que el estándar de facto es `[Tags("Sustantivo de negocio")]` (Cost Centers, Org Units, Position Slots…). `ReportExportJobsController` **no tiene `[Tags]`** (igual que su hermano `ReportsController`), por lo que queda **sin agrupar** en Swagger y **fuera** del OpenAPI guardrail. Además, semánticamente "ReportExportJobs" pertenece a otra categoría: es un **mecanismo técnico** (cola de jobs async), no un recurso de negocio como los demás. 

Recomendación de naming (por costo/impacto):
- **No-breaking (recomendado):** agregar `[Tags("Reports")]` a **ambos** (`ReportsController` + `ReportExportJobsController`) → una sección Swagger "Reports" coherente para toda la reportería (capabilities + export jobs). Alternativa: `[Tags("Report Exports")]`. Esto resuelve la inconsistencia que el usuario señaló sin tocar el wire.
- **Breaking (opcional):** renombrar clase/ruta `report-export-jobs` es un cambio de contrato; el nombre técnico es defendible para una cola de jobs, así que sugiero **conservar la ruta** y resolver la coherencia vía el Tag.

**REX-C — drift canónico** (no por-diseño): faltan `[ApiVersion]` (usa literal `api/v1`), `[SwaggerOperation]` y `[ProducesStandardErrors]`. El `[AuthorizationPolicySet]` ausente SÍ es por diseño (autz por-recurso). No enrolado en guardrails (1/5).

### 6.4 Rendimiento
Índices a medida y bien pensados: `(tenant,queued,public)` y `(tenant,status,queued)` para la búsqueda; `(status,lease,queued)` para el claim del worker; `(status,expires)` para la limpieza de expiración. `ParametersJson` como `jsonb`. Sin join pesado (la búsqueda es sobre una sola tabla). Download streamea el blob (acotado por el límite del worker). Sin rate-limit (REX-E, menor).

### 6.5 Concurrencia y consistencia
Token rotado en cada transición de estado; `.IsConcurrencyToken()` (auto-cubierto por `ConcurrencyTokenMappingGuardrailsTests`, **verificado verde**); Cancel hace check manual→409 (REX-D: vía body-token). Claiming por-lease seguro para múltiples workers; retry idempotente. `Cancel()` es no-op si el job ya es terminal → el handler devuelve 200 con el job sin cambios (REX-H; aceptable idempotente, podría ser 409).

### 6.6 Observabilidad
`ReportExportTelemetryEvents` + estados trazables del job (attempts, started/completed/expires, lastError). Los exports síncronos auditan vía `LogExportAsync`. El job async registra worker/lease/attempts. Adecuado.

### 6.7 Pruebas
**Ejecutadas: 25 unit tests verdes** (dominio, `CreateReportExportJobCommandValidator`, `ReportExportFileWriter`, `ReportExportGovernance` —guardrail anti-duplicación de export-builders—, `ReportExportJobGeneratorDispatch`, `ReportExportTelemetry`, `ReportExportResourceFormatCompatibilityGuardrails`). **Revisadas: 5 integration tests**, incluido el negativo-autz fuerte (`intruder→403` en download/getById/cancel — ancla de REX-1/2/3). **Gap (parte de REX-A):** no hay caso negativo para Search (porque hoy Search no filtra por-recurso).

### 6.8 Build / DevSecOps
Compila; sin secretos hardcodeados. Worker con `IgnoreQueryFilters` documentado (claim/cleanup cross-tenant, sin exponer data a usuarios).

## 7. Hallazgos

### REX-A — `Search` sin autz por-recurso → fuga de metadata intra-tenant
**Severidad:** Media · **Categoría:** Seguridad (Information Disclosure / BFLA) · **Ubicación:** `SearchReportExportJobsQueryHandler` (líneas 292-321) + `ReportExportJobRepository.SearchAsync` (17-58).
**Condición:** el handler sólo verifica `tenantContext == companyId` y NO invoca `resourceAuthorizer`; el repo filtra sólo por `TenantId` (+ status). No filtra por recurso ni por `RequestedByUserId`. La respuesta incluye `resourceKey/fileName/rowCount/sizeBytes/lastErrorCode/Message/timestamps/concurrencyToken` de TODOS los jobs del tenant.
**Criterio esperado:** consistencia con GetById/Download/Cancel (REX-1/2/3), que sí gatean por-recurso; un usuario no debería ver metadata de exports de recursos que no puede leer.
**Impacto:** divulgación de metadata intra-tenant a través de la frontera de autz por-recurso (p.ej. existencia/tamaño/nombre de exports de SalaryTabulator/PersonnelFiles a usuarios sin esos permisos). **Mitigado**: es metadata, no la data del export (Download sí está protegido); intra-tenant; requiere autenticación; el concurrencyToken expuesto no es explotable (Cancel está gated). Por eso Media, no Alta. La memoria registró "Search stays tenant-scoped by-design" — se recomienda **reconsiderar** esa decisión a la luz de esta auditoría.
**Evidencia:** `SearchAsync` sin filtro por-recurso/usuario; el test de integración intruder cubre download/getById/cancel pero no Search.
**Recomendación:** (a) filtrar el listado por los recursos que el usuario puede leer (consistente con REX-1/2/3), o (b) acotar a los jobs del propio solicitante (`RequestedByUserId == currentUser`) si el dashboard es "mis exports". Añadir un test negativo de Search.
**Prioridad:** Alta · **Esfuerzo:** Bajo-Medio · **Estado:** Abierto

### REX-B — Sin `[Tags]` + naming semánticamente inconsistente (deuda solicitada)
**Severidad:** Media · **Categoría:** Contrato/Naming · **Ubicación:** `ReportExportJobsController` (y hermano `ReportsController`).
**Condición:** no declara `[Tags]` → sin agrupar en Swagger y fuera del OpenAPI guardrail; el nombre "ReportExportJobs" es un concepto técnico (cola async), de categoría distinta a los controllers de recurso de negocio (estándar de facto = `[Tags("Sustantivo")]`).
**Criterio esperado:** coherencia con el resto de la API (todos los controllers de cara al cliente tienen Tag de palabras completas).
**Impacto:** Swagger incoherente (área de reportería sin sección), drift de contrato no detectable por CI.
**Evidencia:** censo de `[Tags]` 2026-06-07; `ReportExportJobsController` y `ReportsController` son los únicos del área sin Tag.
**Recomendación:** agregar `[Tags("Reports")]` a `ReportsController` **y** `ReportExportJobsController` (sección Swagger única "Reports"); enrolar en `OpenApiContractGuardrailsTests.Families`. Conservar la ruta `report-export-jobs` (rename = breaking; el nombre técnico es aceptable para una cola). Coordinar con la decisión de naming de OrgUnits/OrgStructureCatalogs (docs 15/16) y [[api-naming-standard]].
**Prioridad:** Media · **Esfuerzo:** Bajo · **Estado:** Abierto

### REX-C — Drift canónico (versionado/Swagger/errores) + no enrolado en guardrails
**Severidad:** Baja · **Categoría:** Contrato · **Ubicación:** todo el controlador.
**Condición:** sin `[ApiVersion]` (literal `api/v1`), sin `[SwaggerOperation]`, sin `[ProducesStandardErrors]` (usa `[ProducesResponseType]` inline). Enrolado sólo en concurrency-token guardrail (1/5). (El `[AuthorizationPolicySet]` ausente es por diseño.)
**Criterio esperado:** contrato canónico (como PositionSlots doc 17).
**Impacto:** inconsistencia + drift no detectable.
**Recomendación:** `[ApiVersion]`+`[SwaggerOperation]`+`[ProducesStandardErrors]` (junto con REX-B `[Tags]`).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### REX-D — Cancel usa body-token (no `If-Match`)
**Severidad:** Baja · **Categoría:** Concurrencia/Contrato · **Ubicación:** `Cancel` + `CancelReportExportJobRequest.ConcurrencyToken`.
**Condición:** el token de concurrencia viaja en el body, no en `If-Match`. Mismo drift que OrgStructureCatalogs/PositionSlots.
**Impacto:** funcionalmente correcto (→409 ante token viejo); inconsistente con el estándar If-Match.
**Recomendación:** evaluar `If-Match` junto con la decisión de concurrencia de la API.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### REX-E — Sin rate-limiting en Search/Download
**Severidad:** Observación · **Categoría:** Rendimiento · **Ubicación:** Search/Download.
**Condición:** sin `[EnableRateLimiting]`. Download streamea un blob (acotado por el worker); Search es un list paginado.
**Recomendación:** evaluar un limitador (al menos en Download) por consistencia.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### REX-F — `pageSize` sin `[Range]`
**Severidad:** Observación · **Categoría:** Contrato · **Ubicación:** `Search` (`int pageSize = 20`).
**Condición:** validator `InclusiveBetween(1,100)` cubre; falta el `[Range]` en el param.
**Recomendación:** `[Range(1,100)]`.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### REX-G — Storage/streaming en el controller (Download)
**Severidad:** Observación · **Categoría:** Arquitectura · **Ubicación:** `Download` (101-115).
**Condición:** resuelve rule/provider/container y abre el stream en el controller.
**Impacto:** menor (es delivery HTTP); la resolución de proveedor/contenedor podría encapsularse.
**Recomendación:** extraer a un servicio de entrega (paridad con `ReportExportDeliveryService`).
**Prioridad:** Baja · **Esfuerzo:** Medio · **Estado:** Abierto

### REX-H — Cancel de job terminal devuelve 200 (no 409)
**Severidad:** Observación · **Categoría:** Contrato · **Ubicación:** `CancelReportExportJobCommandHandler` + `ReportExportJob.Cancel`.
**Condición:** `Cancel()` es no-op si el job no es cancelable; el handler no verifica `CanBeCancelled()` → 200 con el job sin cambios.
**Impacto:** menor — idempotente; podría confundir (no informa que no se canceló).
**Recomendación:** devolver 409 si `!CanBeCancelled()` (o documentar la idempotencia).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

## 8. Hallazgos fuera de alcance / trazabilidad

- **REX-1/2/3 confirmados intactos:** download/getById/cancel gated por-recurso (anclado por el test intruder→403). Esta auditoría **extiende** ese trabajo: identifica que **Search** quedó como el outlier no-gated (REX-A) — la decisión "Search tenant-scoped by-design" merece reconsideración.
- **Pipeline de background** (`ReportExportJobProcessor` 460 líneas, generator, background service) fuera de alcance; el worker usa `IgnoreQueryFilters` documentado para claim/cleanup cross-tenant sin exponer data.
- **`ReportsController`** comparte el problema de naming (sin Tag) — incluirlo en el fix de REX-B.
- **Manejo de PII §N2/§N3** es un patrón a replicar en otros exports con datos sensibles.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|
| REX-A | Media | Seguridad | Search sin autz por-recurso (fuga metadata) | Bajo-Medio | Alta | Filtrar por-recurso o por-usuario + test negativo |
| REX-B | Media | Contrato/Naming | Sin `[Tags]` + naming inconsistente | Bajo | Media | `[Tags("Reports")]` en ambos + enrolar OpenAPI |
| REX-C | Baja | Contrato | Drift canónico (version/swagger/errors) | Bajo | Baja | Atributos canónicos |
| REX-D | Baja | Concurrencia | Cancel body-token | Bajo | Baja | Evaluar If-Match |
| REX-E | Obs | Rendimiento | Sin rate-limit | Bajo | Baja | Limitador en Download |
| REX-F | Obs | Contrato | pageSize sin `[Range]` | Bajo | Baja | `[Range]` |
| REX-G | Obs | Arquitectura | Storage en controller | Medio | Baja | Extraer servicio |
| REX-H | Obs | Contrato | Cancel terminal → 200 | Bajo | Baja | 409 o documentar |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`ReportExportJobsController`) | **Aprobado con observaciones** |
| Seguridad (autz por-recurso Create/Get/Download/Cancel + PII) | Aprobado |
| Seguridad (Search metadata) | **Observación Media (REX-A)** — cerrar recomendado |
| Arquitectura | Aprobado (con REX-G) |
| Contrato/Naming | Observaciones (REX-B/REX-C) |
| Performance | Aprobado |
| Concurrencia | Aprobado con observaciones (REX-D) |
| Pruebas | Aprobado (gap en Search negativo) |
| Readiness productivo completo | No certificado |

**Funcional y, en la data, seguro (Download/GetById/Cancel gated + PII manejada). Se recomienda cerrar REX-A (fuga de metadata) y REX-B (Tags/naming) antes de exposición productiva amplia.**

## 11. Recomendaciones finales

1. **REX-A (prioridad):** gatear `Search` por-recurso (o por `RequestedByUserId`) + test negativo — cierra el último outlier de la consistencia REX-1/2/3.
2. **REX-B (naming, solicitado):** `[Tags("Reports")]` en `ReportsController` **y** `ReportExportJobsController` + enrolar en el OpenAPI guardrail; conservar la ruta (técnica, aceptable). Decidir junto con OrgUnits/OrgStructureCatalogs ([[api-naming-standard]]).
3. **REX-C/D/F:** atributos canónicos + `[Range]` + evaluar If-Match (1 PR de alineación).
4. **REX-E/G/H:** limpieza oportunista.
5. Mantener fortalezas: `ReportExportResourceAuthorizer` single-source, PII §N2/§N3, máquina de estados + claiming por-lease, índices a medida, test intruder.

## 12. Anexos / Evidencia revisada

- Controllers: `ReportExportJobsController.cs` (5 endpoints + DTO), `ReportsController.cs` (naming).
- Aplicación: `ReportExportJobAdministration.cs` (DTOs/validators/5 handlers + mapper), `ReportExportResourceAuthorizer.cs`, `ReportExportResources.cs`, `ReportExportResourceFormatCompatibility.cs`.
- Dominio: `ReportExportJob.cs`, `ReportExportJobStatus.cs`.
- Persistencia: `IReportExportJobRepository.cs`, `ReportExportJobRepository.cs`, `ReportExportJobConfiguration.cs`.
- Seguridad/entrega: `IReportExportResourceAuthorizer.cs`, `IFileStorageProviderResolver`/`IFilePurposeRuleProvider` (download).
- Pruebas: `ReportExportJobDomainTests.cs`, `CreateReportExportJobCommandValidatorTests.cs`, `ReportExportFileWriterTests.cs`, `ReportExportGovernanceTests.cs`, `ReportExportJobGeneratorDispatchTests.cs`, `ReportExportResourceFormatCompatibilityGuardrailsTests.cs`, `ReportExportTelemetryTests.cs`, `ReportExportJobsIntegrationTests.cs` (5 métodos, intruder→403).
- Ejecución: `dotnet test --filter ~ReportExport|~ConcurrencyTokenMappingGuardrails` → **25/25 passed** (sesión 2026-06-07).
