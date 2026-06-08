# Auditoría Técnica por Controlador — SalaryTabulatorController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Contexto: alineado en un "spike" de concurrencia (If-Match obligatorio, 0 migraciones); ver [[salarytabulator-spike-canonical]].

## 1. Resumen ejecutivo

`SalaryTabulatorController` administra el **tabulador salarial** (datos sensibles = PII): las **líneas** salariales (clase/escala, montos base/min/max por moneda y vigencia — solo lectura por API) y un **workflow maker-checker de solicitudes de cambio** (`SalaryTabulatorChangeRequest`: Draft→Submitted→Approved/Rejected/Canceled). Aprobar una solicitud **aplica** sus ítems a las líneas. 12 endpoints.

Veredicto: **APROBADO CON OBSERVACIONES**. 0 críticos / 0 altos. **Es el controlador más maduro en seguridad/correctitud de la ola** para datos sensibles: separación de funciones (permisos `Request` vs `Approve`), **bloqueo de auto-aprobación** (`allowSelfApproval=false` + `decidedByUserId` del usuario, no spoofeable), **If-Match obligatorio** en las 5 mutaciones (la mejor historia de concurrencia auditada), **aplicación atómica en transacción** con invariante de cobertura de job-profiles + auditoría completa (solicitud + cada línea), EF con precisión monetaria y unique de solapamiento. El hallazgo material es la **ausencia de rate-limiting (Media)** sobre el export de PII salarial, más nits de contrato (MinSearchLength, `[SwaggerOperation]` en los GET). La autz handler-gated y el `api/v1` literal son decisiones **por diseño documentadas** (spike).

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila |
| Unit tests (SalaryTabulator + guardrails, **ejecutados**) | ✅ 6/6 passed |
| Integration tests (revisados, no ejecutados) | 11 (incl. coverage-invariant→409, If-Match/stale) |
| Enrolamiento en guardrails de familia | concurrency ✓ (auto); OpenAPI ✗ (pese a tener `[Tags]`); authz ✗ (by-design) |
| Hallazgos | 0 Crít · 0 Alto · **1 Media · 2 Baja · 2 Observación** |

## 2. Alcance

**Incluido:** controlador `SalaryTabulatorController.cs`; aplicación `SalaryTabulatorAdministration.cs` (DTOs, 12 commands/queries, validators, 12 handlers, `SalaryTabulatorCommandSupport`, mapper), `SalaryTabulatorCommon.cs`; dominio `SalaryTabulatorLine.cs`, `SalaryTabulatorChangeRequest.cs`, `SalaryTabulatorChangeRequestItem.cs`, enums, normalization; persistencia `ISalaryTabulatorRepository` + `SalaryTabulatorRepository`, EF (3 configs); seguridad `ISalaryTabulatorAuthorizationService` + impl (Read/Request/Approve); export `SalaryTabulatorExportHandler` (async, gated REX-1); errores/i18n + resx; pruebas (`SalaryTabulatorDomainTests` + integración).

**Excluido:** el pipeline de export async (auditado en doc 18); `JobProfiles`/`PositionDescriptionCatalogs` salvo como referencias (coverage invariant, salary-class lookup); auditoría integral; carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en **seguridad de un dato sensible** (PII salarial): separación de funciones, auto-aprobación, autz de lectura, aislamiento por tenant, concurrencia (If-Match), atomicidad del apply-on-approve y auditoría. Evidencia: 6 unit tests ejecutados (verde). Integración revisada por código (requiere DB; no ejecutada → limitación). **Nota:** edición en curso no relacionada en Locations rompió el build transitoriamente; estable al ejecutar.

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Autz (handler) | Concurrencia |
|---|---|---|---|---|---|
| 1 | GET | `/companies/{companyId}/salary-tabulator/lines` | Buscar líneas (PII) | Read | — |
| 2 | GET | `/salary-tabulator/lines/{id}` | Línea por id | Read | — |
| 3 | GET | `/companies/{companyId}/salary-tabulator/export` | **Export líneas (PII)** | Read | — |
| 4 | GET | `/companies/{companyId}/salary-tabulator/change-requests` | Buscar solicitudes | Read | — |
| 5 | GET | `/salary-tabulator/change-requests/{id}` | Solicitud por id | Read | — |
| 6 | GET | `/salary-tabulator/change-requests/{id}/impact` | Análisis de impacto | Read | — |
| 7 | POST | `/companies/{companyId}/salary-tabulator/change-requests` | Crear (Draft) | **Request** | 201+Location+ETag |
| 8 | PUT | `/salary-tabulator/change-requests/{id}` | Editar Draft | **Request** | **If-Match** |
| 9 | PATCH | `…/{id}/submit` | Draft→Submitted | **Request** | **If-Match** |
| 10 | PATCH | `…/{id}/approve` | Submitted→Approved + aplica | **Approve** | **If-Match** |
| 11 | PATCH | `…/{id}/reject` | Submitted→Rejected | **Approve** | **If-Match** |
| 12 | PATCH | `…/{id}/cancel` | Draft/Submitted→Canceled | **Request** | **If-Match** |

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / DTOs / capas | PASS | Sólo despacha CQRS; mapeo de items en helper |
| Arquitectura | Aggregate design | PASS | `ChangeRequest` con items por field-access + cascade; line/request roots con token; item sin token (hijo) |
| Arquitectura | Transacciones en escrituras | PASS | Create/Update/Submit/Approve/Reject/Cancel en transacción + audit + rollback |
| Seguridad | Autenticación | PASS | `[Authorize]` |
| Seguridad | Autz handler-gated (Read/Request/Approve) | PASS | Los 12 handlers invocan el Ensure correcto; permisos separados |
| Seguridad | **Separación de funciones (maker-checker)** | PASS | `SalaryTabulator.Request` (maker) ≠ `SalaryTabulator.Approve` (checker) |
| Seguridad | **Bloqueo de auto-aprobación** | PASS | `allowSelfApproval=false` + dominio lanza si `decidedBy==requestedBy`; `decidedByUserId` del JWT (no body) |
| Seguridad | Lectura de PII restringida | PASS | Salario gated tras `SalaryTabulator.Read` (permiso dedicado, no todo usuario) |
| Seguridad | BOLA/IDOR + Tenant isolation | PASS | id-only routes (tenant del JWT); `*ExistsOutsideTenant*` → 404 vs 403 |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(CommercialModuleKeys.SalaryTabulator)` |
| Seguridad | Export async gated (REX-1) | PASS | `ReportExportResources.SalaryTabulator` → `EnsureCanReadAsync` |
| Seguridad | **Rate limiting** | **FAIL** | **ST-A**: sin `[EnableRateLimiting]` ni política; export de PII sin guard de abuso |
| Contrato | `[Tags]` | PASS | `[Tags("Salary Tabulator")]` |
| Contrato | Versionado / `[AuthorizationPolicySet]` | NO APLICA | `api/v1` literal + handler-gated = **por diseño documentado** (spike) |
| Contrato | `[SwaggerOperation]` | WARNING | **ST-C**: presente en mutaciones; **ausente en los 6 GET** |
| Contrato | Error contract | WARNING | **ST-C**: `[ProducesResponseType]` inline (no `[ProducesStandardErrors]`); no enrolado en OpenAPI guardrail |
| Contrato | 201 + Location + ETag en Create | PASS | `ToCreatedAtActionResult` |
| Contrato | If-Match / ETag en updates | PASS | `[FromIfMatch]`+`ToActionResultWithETag` en las 5 mutaciones (400 si falta, 409 stale) |
| Contrato | Paginación: page size | PASS (nit) | Validator `InclusiveBetween(1,100)`; sin `[Range]` en el param |
| Contrato | Search: longitud mínima | WARNING | **ST-B**: sin `MinSearchLength` |
| Rendimiento | Índices | PASS | unique `(tenant,class,scale,effectiveFrom)`; `(tenant,class,scale,active)`; `(tenant,status,created)`; `(tenant,requestNumber)` |
| Rendimiento | N+1 | PASS | `includeAllowedActions` usa flags únicos (canRequest/canApprove), sin probe por-ítem |
| Rendimiento | Money precision | PASS | `decimal` `HasPrecision(18,2)` en todos los montos |
| Rendimiento | Search sargable | WARNING | `Contains` no-sargable sin MinSearchLength (ST-B); cardinalidad acotada |
| Concurrencia | Optimista + If-Match + 409 | PASS | Token en ambos roots + `.IsConcurrencyToken()` (auto-guardrail); check manual→409; race→409 middleware |
| Concurrencia | RequestNumber único | PASS | `STR-{ts-ms}-{GUID}` → sin race realista (GUID); unique `(tenant,requestNumber)` de respaldo |
| Concurrencia | Apply-on-approve atómico | PASS | Items aplicados + coverage-invariant + `Approve()` + audit en UNA transacción con rollback |
| Concurrencia | Invariante de cobertura | PASS | `HasUncoveredJobProfileCompensationReferenceAsync` → 409 si dejaría job profiles sin cobertura |
| Observabilidad | Audit logs + constantes | PASS | `AuditEventTypes.SalaryTabulator*`; aprueba audita la solicitud **+ cada línea** afectada (before/after) |
| Pruebas | Unit | PASS (ligero) | 6 (dominio/state-machine/self-approval/amounts); **ST-D** handlers cubiertos por integración |
| Pruebas | Integración | PASS | 11 (incl. coverage→409, If-Match/stale) |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS limpia con un aggregate bien modelado: `SalaryTabulatorChangeRequest` encapsula sus `Items` (field-access, cascade-delete), ambos roots editables tienen `ConcurrencyToken`, el item (hijo) no (cubierto por el token del padre). `SalaryTabulatorCommandSupport` factoriza `BuildItemsAsync` (validación contra catálogo de clases salariales), `ApplyChangeRequestItemAsync` (aplicación a líneas) y la generación de `RequestNumber`. Cada escritura es transaccional.

### 6.2 Seguridad — fortaleza central (dato sensible)
**Separación de funciones real**: tres permisos (`Read` para ver PII, `Request` para crear/editar/someter/cancelar, `Approve` para aprobar/rechazar); `Admin`/`iam.administration.manage` son superset. **Bloqueo de auto-aprobación**: `ApproveSalaryTabulatorChangeRequestCommandHandler` fija `allowSelfApproval=false` y el dominio `Approve()` lanza si `decidedByUserId == RequestedByUserId`; crucialmente `decidedByUserId` se toma del JWT (`currentUserService.UserId`), **no del body**, así que no se puede suplantar al aprobador — incluso un `Admin` (que satisface Request y Approve) no puede aprobar su propia solicitud. **Lectura de PII** gated tras `SalaryTabulator.Read` (permiso dedicado, no todo usuario autenticado). **Aislamiento por tenant** vía `*ExistsOutsideTenant*` (404 vs 403). **Export async** gated por-recurso (REX-1). La única brecha es rate-limiting (ST-A).

### 6.3 Contrato API
Concurrencia **canónica completa** (If-Match obligatorio + ETag + 201/Location en Create) — superior a PositionSlots/OrgStructureCatalogs (body-token). `[Tags]` presente. Decisiones por-diseño documentadas: `api/v1` literal (sin `[ApiVersion]`) y handler-gated (sin `[AuthorizationPolicySet]`, como AccountCompanies). **Drift residual del spike (ST-C)**: los 6 GET no tienen `[SwaggerOperation]` (las mutaciones sí), se usa `[ProducesResponseType]` inline en vez de `[ProducesStandardErrors]`, y el controller **no está en `OpenApiContractGuardrailsTests.Families`** pese a tener Tag → contrato no guardado. Falta `MinSearchLength` (ST-B).

### 6.4 Rendimiento
Índices a medida: unique de solapamiento `(tenant,class,scale,effectiveFrom)` (impide líneas duplicadas para la misma clase/escala/fecha), `(tenant,class,scale,active)` para lecturas, `(tenant,status,created)` para solicitudes. `decimal(18,2)` en montos. `includeAllowedActions` sin N+1. Export acotado (`SynchronousReadLimit`). Search libre `Contains` no-sargable sin MinSearchLength (ST-B; cardinalidad acotada por clase×escala).

### 6.5 Concurrencia y consistencia
La más robusta de la ola: If-Match obligatorio en las 5 mutaciones, token en ambos roots (`.IsConcurrencyToken()`, auto-guardrail), check manual→409, race→409 vía middleware. **Apply-on-approve atómico**: en UNA transacción aplica cada ítem a las líneas, valida la **invariante de cobertura** (`HasUncoveredJobProfileCompensationReferenceAsync` → 409 si dejaría algún job profile sin cobertura salarial), transiciona `Approve()`, y audita; rollback ante cualquier fallo. `RequestNumber` = `STR-{timestamp-ms}-{GUID}` → unicidad garantizada (sin race a pesar del unique constraint).

### 6.6 Observabilidad
Auditoría de grado compliance para datos financieros: aprobar registra `SalaryTabulatorRequestApproved` **+ un evento por cada línea afectada** (before/after de montos). Create/Submit/Reject/Cancel también auditan. Constantes de auditoría. Trazabilidad completa de quién pidió/decidió y cuándo.

### 6.7 Pruebas
**Ejecutadas: 6 unit tests verdes** (`SalaryTabulatorDomainTests`: state-machine, self-approval block, amount rules) + concurrency guardrail. **Revisadas: 11 integration tests**, incl. `Approve_WhenInactivationLeavesJobProfileCompensationUncovered→409` (coverage invariant), If-Match faltante→400 y stale→409. **ST-D**: la cobertura **unit** de los handlers/`CommandSupport` (apply-on-approve, item-building, coverage) es ligera; descansa en integración.

### 6.8 Build / DevSecOps
Compila; sin secretos hardcodeados; localización completa.

## 7. Hallazgos

### ST-A — Sin rate-limiting (incluye el export de PII salarial)
**Severidad:** Media · **Categoría:** Seguridad/Rendimiento · **Ubicación:** `SalaryTabulatorController` SearchLines/ExportLines/SearchRequests.
**Condición:** ningún endpoint declara `[EnableRateLimiting]`; no hay política SalaryTabulator en `Program.cs`. El `ExportLines` produce un reporte con **datos salariales (PII)**.
**Criterio esperado:** limitador por usuario+tenant en lecturas costosas/exports, como CostCenters (search 120/export 10), PositionSlots, Locations.
**Impacto:** vector de abuso/scraping sobre el dato más sensible del sistema. **Mitigado**: gated tras `SalaryTabulator.Read` + export acotado por `SynchronousReadLimit`; por eso Media, no Alta.
**Evidencia:** controller sin `[EnableRateLimiting]`; `Program.cs` sin política SalaryTabulator.
**Recomendación:** añadir `SalaryTabulatorRateLimitPolicies` (search + export) + `[EnableRateLimiting]` + governance test (mirror de CostCenters/PositionSlots). Dado el perfil PII, priorizarlo sobre los demás rate-limits pendientes.
**Prioridad:** Alta · **Esfuerzo:** Bajo · **Estado:** Abierto

### ST-B — Search/Export sin `MinSearchLength`
**Severidad:** Baja · **Categoría:** Rendimiento/Contrato · **Ubicación:** `SearchSalaryTabulatorLinesQueryValidator` (sólo `MaximumLength(150)`); repo `SearchLinesAsync` líneas 87-93 (`Contains` sobre 3 columnas).
**Condición:** `q` de 1 char → LIKE '%x%' no-sargable. Mismo patrón que OrgUnits OU-002 / §LG5.
**Impacto:** menor (cardinalidad acotada), pero compone con la ausencia de rate-limit (ST-A).
**Recomendación:** `MinSearchLength=2` + `IsValidSearchLength` en los validators de Search y Export.
**Prioridad:** Media · **Esfuerzo:** Bajo · **Estado:** Abierto

### ST-C — GET sin `[SwaggerOperation]` + `[ProducesResponseType]` inline + no enrolado en OpenAPI guardrail
**Severidad:** Baja · **Categoría:** Contrato · **Ubicación:** los 6 GET; `OpenApiContractGuardrailsTests.Families`.
**Condición:** las mutaciones tienen `[SwaggerOperation]` (del spike) pero los 6 GET no; se usa `[ProducesResponseType]` inline en todo el controller; pese a tener `[Tags("Salary Tabulator")]`, **no** está en el OpenAPI guardrail → drift de contrato no detectable por CI.
**Criterio esperado:** documentación uniforme + enrolamiento en el guardrail (como PositionSlots).
**Impacto:** Swagger incompleto en lecturas; drift no guardado.
**Recomendación:** `[SwaggerOperation]` en los GET + migrar a `[ProducesStandardErrors]` + enrolar `^SalaryTabulator`→"Salary Tabulator" en `Families`.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### ST-D — Cobertura unit ligera de handlers/CommandSupport
**Severidad:** Observación · **Categoría:** Pruebas · **Ubicación:** `tests/CLARIHR.Application.UnitTests/SalaryTabulatorDomainTests.cs` (6) vs vertical de 1853 líneas.
**Condición:** la lógica de apply-on-approve, item-building y coverage-invariant está cubierta por integración (11), no por unit.
**Impacto:** bajo — las rutas críticas (self-approval, coverage→409, If-Match) sí están cubiertas (dominio + integración).
**Recomendación:** unit tests de `SalaryTabulatorCommandSupport` (item-building, apply, MapDomainValidation) para velocidad de regresión.
**Prioridad:** Baja · **Esfuerzo:** Medio · **Estado:** Abierto

### ST-E — Swagger de Cancel dice "Draft" pero el dominio permite cancelar Submitted
**Severidad:** Observación · **Categoría:** Contrato · **Ubicación:** `CancelRequest` Swagger vs `SalaryTabulatorChangeRequest.Cancel`.
**Condición:** `Cancel()` permite Draft o Submitted (sólo bloquea Approved/Rejected); el Swagger dice "Cancels a `Draft` change request".
**Impacto:** menor — inexactitud de documentación.
**Recomendación:** corregir el Swagger ("Draft o Submitted") o restringir el dominio a Draft si esa es la intención.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

## 8. Hallazgos fuera de alcance / trazabilidad

- **Decisiones por-diseño (spike, confirmadas):** `api/v1` literal (sin `[ApiVersion]`) + handler-gated (sin `[AuthorizationPolicySet]`, no en `GovernedFamilyRegex`, mirror AccountCompanies). **No re-flaggear** como drift — son intencionales y documentadas en el comentario de cabecera del controller.
- **Export async** auditado en doc 18; SalaryTabulator gated por `ReportExportResourceAuthorizer` (REX-1). Recordatorio: el **Search de jobs** de ReportExportJobs (REX-A) filtra metadata de exports de SalaryTabulator a usuarios sin permiso — cerrar allí.
- **Patrón de referencia:** el bloqueo de auto-aprobación + apply-atómico + auditoría por-línea es el modelo a replicar en otros workflows de aprobación sobre datos sensibles.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|
| ST-A | Media | Seguridad/Perf | Sin rate-limit (export PII) | Bajo | Alta | Políticas + `[EnableRateLimiting]` + governance |
| ST-B | Baja | Rendimiento | Sin MinSearchLength | Bajo | Media | `MinSearchLength=2` |
| ST-C | Baja | Contrato | GET sin SwaggerOperation + no en OpenAPI guardrail | Bajo | Baja | `[SwaggerOperation]`+`[ProducesStandardErrors]`+enrolar |
| ST-D | Obs | Pruebas | Unit ligero de handlers | Medio | Baja | Unit de CommandSupport |
| ST-E | Obs | Contrato | Swagger Cancel "Draft" inexacto | Bajo | Baja | Corregir doc |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`SalaryTabulatorController`) | **Aprobado con observaciones** |
| Seguridad (maker-checker / self-approval / PII / tenant) | Aprobado (modelo de referencia) |
| Seguridad (rate-limit del export PII) | Observación Media (ST-A) — cerrar recomendado |
| Arquitectura | Aprobado |
| Contrato | Aprobado con observaciones (ST-C; concurrencia canónica completa) |
| Performance | Aprobado con observaciones (ST-B) |
| Concurrencia | Aprobado (la más robusta de la ola) |
| Pruebas | Aprobado (unit ligero, ST-D) |
| Readiness productivo completo | No certificado |

**Funcional, seguro y correcto a un nivel alto para un dato sensible.** Se recomienda cerrar **ST-A (rate-limit del export PII)** antes de exposición productiva amplia; el resto son nits de contrato/pruebas.

## 11. Recomendaciones finales

1. **ST-A (prioridad por ser PII):** rate-limiting en search/export + governance test.
2. **ST-B/ST-C:** `MinSearchLength=2`; `[SwaggerOperation]` en los GET + `[ProducesStandardErrors]` + enrolar en el OpenAPI guardrail (1 PR de alineación de contrato).
3. **ST-D/ST-E:** unit tests de `CommandSupport`; corregir el Swagger de Cancel.
4. Mantener las fortalezas (modelo de referencia): separación de funciones, bloqueo de auto-aprobación con `decidedBy` del JWT, If-Match obligatorio, apply-on-approve atómico + coverage-invariant, auditoría por-línea, money precision, aggregate encapsulation.

## 12. Anexos / Evidencia revisada

- Controller: `SalaryTabulatorController.cs` (12 endpoints + DTOs).
- Aplicación: `SalaryTabulatorAdministration.cs` (DTOs/validators/12 handlers + `SalaryTabulatorCommandSupport` + mapper), `SalaryTabulatorCommon.cs`.
- Dominio: `SalaryTabulatorLine.cs`, `SalaryTabulatorChangeRequest.cs`, `SalaryTabulatorChangeRequestItem.cs`, enums, normalization.
- Persistencia: `ISalaryTabulatorRepository.cs`, `SalaryTabulatorRepository.cs`, `SalaryTabulatorConfiguration.cs` (3 configs).
- Seguridad/export: `SalaryTabulatorAuthorizationService.cs` (Read/Request/Approve), `SalaryTabulatorExportHandler.cs`, `ReportExportResourceAuthorizer.cs`.
- Pruebas: `SalaryTabulatorDomainTests.cs`, `ApiIntegrationTests.cs` (11 SalaryTabulator_* métodos).
- Ejecución: `dotnet test --filter ~SalaryTabulator|~ConcurrencyTokenMappingGuardrails` → **6/6 passed** (sesión 2026-06-07).
