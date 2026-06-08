# Auditoría Técnica por Controlador — PositionSlotsController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Contexto: el feature Position tuvo una remediación de deuda técnica previa (docs `technical-debt/Position/03/05/08/09`, "deuda = 0" al 2026-05-24). Esta es una **auditoría fresca e independiente** con la lente de la skill (no una verificación de la deuda).

## 1. Resumen ejecutivo

`PositionSlotsController` administra las **plazas/posiciones** (position slots) de una empresa: 10 endpoints (búsqueda paginada, get-by-id, grafo de dependencias, export de diagrama, export tabular, create, update, y 3 PATCH especializados: status, dependencies, occupancy). Cada slot referencia un job profile (del que infiere org unit, categoría, clasificación, contract type y cost center), opcionalmente un rol/work center y dependencias directa/funcional a otros slots.

Veredicto: **APROBADO CON OBSERVACIONES**. 0 críticos / 0 altos / 0 medios. **Es el controlador más maduro y canónico de los auditados** en esta ola (Location*, OrgUnits, OrgStructureCatalogs): contrato canónico completo (`[ApiVersion]`/`[Tags]`/`[AuthorizationPolicySet]`/`[SwaggerOperation]`/`[ProducesStandardErrors]`), rate-limiting con governance, autorización de dos capas con optimización `EvaluateAccessAsync` (≤1 probe DB), `MinSearchLength=2`, join único §PS3 (sin N+1), cap de grafo §PS4 antes del wide-load, errores de dominio tipados §PS5 con guardrail, consistencia status/ocupación §PS6, índices ricos, detección de ciclos y builder cycle-safe. La remediación de deuda previa se confirma intacta. Los hallazgos nuevos son todos **Baja/Observación**: concurrencia por body-token (no `If-Match`) con un doc Swagger inexacto, Create sin `Location`/`ETag`, race dup-code→500, y nits menores. **Sirve de modelo canónico para alinear OrgStructureCatalogs (doc 16).**

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila |
| Unit tests (PositionSlot + guardrails, **ejecutados**) | ✅ 167/167 passed |
| Integration tests (revisados, no ejecutados) | 11 |
| Enrolamiento en guardrails de familia | **4/5** (authz ✓ · OpenAPI ✓ · rate-limit ✓ · concurrency ✓ · paginación ✗) |
| Hallazgos | 0 Crít · 0 Alto · 0 Media · **3 Baja · 4 Observación** |

## 2. Alcance

**Incluido:** controlador `PositionSlotsController.cs`; aplicación `PositionSlotAdministration.cs` (DTOs, 10 validators, 10 handlers, `PositionSlotPolicyAdapter`, `PositionSlotGraphBuilder`, `PositionSlotCommandSupport`, `PositionSlotDependencyAnalyzer`); dominio `PositionSlot.cs`, `PositionSlotDomainException.cs`, `PositionSlotEnums.cs`, `PositionSlotNormalization.cs`; persistencia `IPositionSlotRepository` + `PositionSlotRepository`, EF `PositionSlotConfiguration`; seguridad `PositionSlotPolicies`, `PositionSlotPermissionCodes`, `IPositionSlotAuthorizationService` + impl (`EvaluateAccessAsync`); rate-limit `PositionSlotRateLimitPolicies`; reportes `PositionSlotsExportHandler` + `ReportExportDeliveryService`; errores/i18n `PositionSlotCommon.cs` + resx; dependencias directas `IIamAdministrationRepository`, `ICostCenterRepository`, `IPositionSlotRepository.GetJobProfileLookupAsync`; pruebas (6 archivos unit + integración).

**Excluido:** auditoría integral de la API; `JobProfiles`/`OrgUnits`/`CostCenters`/`IAM` salvo como referencias resueltas; pruebas de carga. Migración `20260411225512` (rol) revisada sólo por contexto.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en seguridad (gate por handler + dos capas + tenant), concurrencia, ciclos de dependencia, sargabilidad y adherencia canónica. Evidencia de ejecución: 167 unit tests ejecutados (verde). Integración revisada por código (requiere DB; no ejecutada → limitación). **Nota de entorno:** durante la sesión hubo una edición en curso, no relacionada, en la vertical de Locations que rompió el build transitoriamente; al ejecutar las pruebas de PositionSlots el build estaba estable.

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Rate-limit | Handler |
|---|---|---|---|---|---|
| 1 | GET | `/companies/{companyId}/position-slots` | Búsqueda paginada (6 filtros + `q`≥2 + allowedActions) | Search | `SearchPositionSlotsQueryHandler` |
| 2 | GET | `/position-slots/{id}` | Get by id (tenant del JWT) | — | `GetPositionSlotByIdQueryHandler` |
| 3 | GET | `/companies/{companyId}/position-slots/graph` | Grafo dependencias (JSON, 413 si excede) | Export | `GetPositionSlotGraphQueryHandler` |
| 4 | GET | `/companies/{companyId}/position-slots/diagram-export` | Export diagrama (graphml/json/dot) | Export | (controller + graph query) |
| 5 | GET | `/companies/{companyId}/position-slots/export` | Export tabular (xlsx/csv) | Export | `GetPositionSlotExportRowsQueryHandler` |
| 6 | POST | `/companies/{companyId}/position-slots` | Crear | — | `CreatePositionSlotCommandHandler` |
| 7 | PUT | `/position-slots/{id}` | Actualizar core (body token) | — | `UpdatePositionSlotCommandHandler` |
| 8 | PATCH | `/position-slots/{id}/status` | Cambiar status (reconcilia ocupación) | — | `UpdatePositionSlotStatusCommandHandler` |
| 9 | PATCH | `/position-slots/{id}/dependencies` | Set dependencias (ciclo directo → 409) | — | `UpdatePositionSlotDependenciesCommandHandler` |
| 10 | PATCH | `/position-slots/{id}/occupancy` | Set ocupación (capacidad → 422) | — | `UpdatePositionSlotOccupancyCommandHandler` |

Sin `DELETE` (Suspended via status). Concurrencia: token en el **body** (PS-A). Mutaciones especializadas en PATCH sub-recursos (status/dependencies/occupancy) — buena separación.

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / capas / DTOs | PASS (nit) | Delgado salvo ~100 líneas de serialización GraphML/DOT (PS-E) |
| Arquitectura | Dominio con invariantes tipados | PASS | `PositionSlotDomainException` + enum §PS5; capacidad/fechas/status-ocupación |
| Arquitectura | Transacciones en escrituras | PASS | 6 commands: transacción + 2 SaveChanges + audit + rollback |
| Seguridad | Autenticación / Autorización por handler | PASS | `[Authorize]` + los 10 handlers aplican el gate (Read/Manage) |
| Seguridad | Dos capas declarativa ⊇ handler | PASS | `[AuthorizationPolicySet]` + `AuthorizationPolicyConventionGovernanceTests` (regex incluye `PositionSlot`) |
| Seguridad | Optimización de autz (read amplifier) | PASS | §PS1 `EvaluateAccessAsync`: 1 preámbulo + claim short-circuit + ≤1 probe DB |
| Seguridad | BOLA/IDOR + Tenant isolation | PASS | id-only routes (tenant del JWT); filtro global; `*ExistsOutsideTenant*` → 404 vs 403 en slot/jobProfile/workCenter/dependency |
| Seguridad | Entitlement de módulo | PASS | `IsModuleEnabledAsync(CommercialModuleKeys.PositionSlots)` |
| Seguridad | Mass assignment | PASS | DTOs cerrados; status/occupancy sólo vía PATCH dedicados; cost-center inferido (no del cliente) |
| Seguridad | Inyección export diagrama | WARNING | GraphML vía `XmlWriter` (safe); **DOT `EscapeDot` sólo escapa `"`, no `\`** (PS-E) |
| Seguridad | Authz export async (REX-1) | PASS | `ReportExportResources.PositionSlots` gated en `ReportExportResourceAuthorizer` |
| Contrato | Versionado / Tags / Swagger / errores | PASS | `[ApiVersion]`+`[Route(v{version})]`+`[Tags("Position Slots")]`+`[SwaggerOperation]`×10+`[ProducesStandardErrors]`×10 (+413/429 inline) |
| Contrato | OpenAPI guardrail | PASS | `OpenApiContractGuardrailsTests.Families` → `^PositionSlot`→"Position Slots" |
| Contrato | 201 + Location + ETag en Create | **FAIL** | **PS-B**: `StatusCode(201, value)` plano (sin Location/ETag) |
| Contrato | ETag / If-Match en updates | **FAIL** | **PS-A**: token en body, no `If-Match`; GetById Swagger dice "ETag" pero usa `ToActionResult` (no emite ETag) |
| Contrato | Paginación: page size | PASS (nit) | Validator `InclusiveBetween(1,100)` ✓; **PS-F**: sin `[Range]` en el param + sin pagination guardrail |
| Contrato | Search: longitud mínima | PASS | §PS2 `MinSearchLength=2` en validator (`IsValidSearchLength`) |
| Rendimiento | Join único / N+1 | PASS | §PS3 `BuildJoinedQuery()` reusado por 4 reads; allowedActions usa `canManage` (sin probe por-ítem) |
| Rendimiento | Cap de grafo antes del wide-load | PASS | §PS4 `CountSlotsAsync` → 413 antes de `GetGraphNodesAsync` |
| Rendimiento | Índices | PASS | `uq_(tenant,normalized_code)` + 6 índices `(tenant,*)` (status/jobProfile/role/workCenter/2 deps) |
| Rendimiento | Search sargable | PASS (mitigado) | `Contains` no-sargable PERO con `MinSearchLength=2` (§PS2) |
| Rendimiento | Rate limit | PASS | Search (120) + Export/Graph/Diagram (10) + `RateLimitingGovernanceTests` |
| Concurrencia | Optimista + 409 | PASS | Token rotado; check manual→409; `.IsConcurrencyToken()` (auto-guardrail); race read-check-write→409 vía middleware |
| Concurrencia | Unique-constraint en Create → 409 | **FAIL** | **PS-C**: race dup-code → 500 |
| Concurrencia | Ciclos de dependencia | PASS (nit) | Directa: `WouldCreateDirectCycle` → 409 + self-ref; **funcional: sólo self-ref, sin cycle-check** (PS-D; builder es cycle-safe) |
| Observabilidad | Audit logs + **constantes** | PASS | 6 commands con `AuditEventTypes.PositionSlot*`/`AuditEntityTypes.PositionSlot` |
| Pruebas | Unit (dominio/authz/validators/mapping/graph-cap/search) | PASS | 6 archivos; 167 verdes en esta sesión |
| Pruebas | Integración | PASS | 11 métodos (incl. WithoutPermission/WithTenantMismatch) |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS ejemplar. Dominio rico con invariantes **tipados** (`PositionSlotDomainException` + `PositionSlotDomainErrorCode`, §PS5) — el mapeo a errores de aplicación es por código, no por texto, con un guardrail (`PositionSlotDomainErrorMappingGuardrailsTests`) y un `_ => throw` fail-loud para códigos sin mapear. §PS6: distinción intencional y documentada entre `Create` (rechaza status+ocupación contradictorios → 422) y `ChangeStatus` (reconcilia, status-only). `PositionSlotCommandSupport` factoriza la resolución de referencias (jobProfile/workCenter/role/dependency) con manejo de tenant-mismatch uniforme. Única desviación arquitectónica: ~100 líneas de serialización GraphML/DOT en el controller (PS-E, mismo patrón que OrgUnits).

### 6.2 Seguridad
**Modelo de dos capas robusto y optimizado.** Declarativa `[AuthorizationPolicySet]` (superset, enforced por governance, controller en `GovernedFamilyRegex`) + gate por handler en los 10 endpoints. §PS1 `EvaluateAccessAsync` resuelve read+manage en un solo preámbulo con ≤1 probe DB (manage⊂read) — pensado para el amplificador `includeAllowedActions`. **Sin IDOR**: rutas id-only (tenant del JWT), filtro global fail-closed, y `*ExistsOutsideTenant*` en slot/jobProfile/workCenter/dependency para 404-vs-403. **Anti-mass-assignment**: el cost-center NO lo provee el cliente (se infiere del org unit del job profile), status/occupancy sólo por PATCH dedicados. Export async gated (REX-1). **Única observación**: `EscapeDot` escapa sólo `"` (no `\`); como `Title` admite hasta 180 chars de texto ~libre, un `\` podría malformar el DOT (el código es regex-safe; GraphML usa XmlWriter seguro) — PS-E.

### 6.3 Contrato API
Canónico casi por completo (versionado, tags, swagger, produces-standard-errors, rate-limit, OpenAPI guardrail). **Dos derivas de concurrencia/creación** respecto al estándar más reciente (SalaryTabulator/Locations/OrgUnits):
- **PS-A:** las 4 mutaciones leen `concurrencyToken` del **body**, no del header `If-Match`. Además, el Swagger de GetById afirma "The concurrencyToken is emitted as the ETag header" pero el handler usa `ToActionResult` (no `ToActionResultWithETag`) → **no se emite ningún ETag**: el doc es inexacto.
- **PS-B:** Create devuelve `StatusCode(201, value)` plano (sin `Location` ni `ETag`) en lugar de `ToCreatedAtActionResult`.

Ambas son consistentes con cómo está OrgStructureCatalogs (doc 16 OSC-001), pero PositionSlots las tiene aisladas (todo lo demás es canónico).

### 6.4 Rendimiento
Excelente. §PS3: un único `BuildJoinedQuery()` (~8 tablas, dependencias LEFT JOIN) reusado por Search/GetById/GraphNodes/Export. `includeAllowedActions` **no** hace probe por-ítem (usa el `canManage` del §PS1) → **sin N+1** (contraste con OrgStructureCatalogs OSC-002). §PS4: `CountSlotsAsync` capa el grafo a `MaxDiagramNodes` ANTES de cargar el wide-join → 413. Search no-sargable pero con `MinSearchLength=2`. Índices ricos que respaldan los 6 filtros + las 2 dependencias. Export acotado por `SynchronousReadLimit`. **Nit (PS-G)**: el cycle-check de `UpdateDependencies` carga el grafo completo (`GetGraphNodesAsync`) sin el cap §PS4 (la ruta de lectura sí lo tiene; la de mutación no).

### 6.5 Concurrencia y consistencia
Token rotado en cada mutación, check manual→409, `.IsConcurrencyToken()` (auto-cubierto por `ConcurrencyTokenMappingGuardrailsTests`), race read-check-write→409 vía middleware. Ciclos: la dependencia **directa** se valida con `WouldCreateDirectCycle` (BFS sobre el grafo) + self-ref; la **funcional** sólo self-ref (PS-D) — el `PositionSlotGraphBuilder.Select` usa un set `selected.TryAdd` que lo hace cycle-safe, así que un ciclo funcional no rompe el render, pero puede persistirse. **PS-C**: el Create no traduce la violación de `uq_position_slots__tenant_code` a 409.

### 6.6 Observabilidad
Auditoría persistida (en transacción) en los 6 commands con **constantes** `AuditEventTypes.PositionSlot*` (Created/…/DependencyUpdated/OccupancyChanged) y `AuditEntityTypes.PositionSlot`. Export audita con claves `*PublicId` (§PS7). Logging estructurado vía middleware. Excelente.

### 6.7 Pruebas
**Ejecutadas (sesión): 167 unit tests verdes** — 6 archivos dedicados (`PositionSlotDomainTests`, `PositionSlotAuthorizationServiceTests` matriz claim→access + deny-gates §X-TEST1, `PositionSlotAdministrationTests` validators §X-TEST2, `PositionSlotDomainErrorMappingGuardrailsTests`, `PositionSlotGraphCapGuardrailsTests`, `PositionSlotSearchValidatorTests`) + los 4 guardrails de familia. **Revisadas**: 11 integration tests (incl. `WithoutPermission`/`WithTenantMismatch`). Cobertura de pruebas **muy fuerte** — la mejor de la ola.

### 6.8 Build / DevSecOps
Compila; sin secretos hardcodeados; localización completa (resx en+es, mapping por código).

## 7. Hallazgos

### PS-A — Concurrencia por body-token (no `If-Match`) + Swagger de GetById inexacto sobre ETag
**Severidad:** Baja · **Categoría:** Concurrencia/Contrato · **Ubicación:** `PositionSlotsController` Update/UpdateStatus/UpdateDependencies/UpdateOccupancy (token en `*Request`); GetById líneas 78-92.
**Condición:** las 4 mutaciones reciben `ConcurrencyToken` en el body, no en el header `If-Match` (`[FromIfMatch]`), a diferencia del estándar reciente. Además GetById documenta "The concurrencyToken is emitted as the `ETag` header" pero usa `ToActionResult` (no `ToActionResultWithETag`), por lo que **no emite ETag**.
**Criterio esperado:** concurrencia HTTP estándar vía `If-Match`/`ETag` (como SalaryTabulator/Locations/OrgUnits); documentación que coincida con el comportamiento.
**Impacto:** funcionalmente correcto (el body-token produce 409 ante token viejo), pero inconsistente con el resto y con un doc engañoso (clientes que esperen el ETag no lo recibirán).
**Evidencia:** DTOs `UpdatePositionSlotRequest.ConcurrencyToken` etc.; GetById usa `this.ToActionResult(result)` con Swagger que promete ETag.
**Recomendación:** o (a) migrar a `If-Match` + `ToActionResultWithETag` (alinea con el estándar — coordinar con frontend, breaking), o (b) como mínimo **corregir el Swagger de GetById** (quitar la mención del ETag) y emitir el ETag realmente si se desea conservarlo. Decidir junto con OrgStructureCatalogs OSC-001 (mismo tema de concurrencia).
**Prioridad:** Baja · **Esfuerzo:** Bajo (doc) / Medio (If-Match) · **Estado:** Abierto

### PS-B — Create sin `Location`/`ETag`
**Severidad:** Baja · **Categoría:** Contrato · **Ubicación:** `Create` líneas 317-319.
**Condición:** devuelve `StatusCode(StatusCodes.Status201Created, result.Value)` — sin header `Location` ni `ETag`.
**Criterio esperado:** `ToCreatedAtActionResult(... nameof(GetById), publicId, ConcurrencyToken)` (201 + Location + ETag), como OrgUnits/Locations.
**Impacto:** el cliente no recibe la URL del recurso creado ni su token inicial vía headers.
**Recomendación:** usar `ToCreatedAtActionResult`.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### PS-C — Race de código duplicado en Create → 500 (no 409)
**Severidad:** Baja · **Categoría:** Concurrencia · **Ubicación:** `CreatePositionSlotCommandHandler` (pre-check `CodeExistsAsync` 599 + `SaveChanges` 711).
**Condición:** dos creates concurrentes con el mismo código pasan el pre-check; el segundo viola `uq_position_slots__tenant_code`; `UniqueConstraintViolationException` no atrapada → 500. Sistémico (= CostCenters R2, OrgUnits OU-004, OrgStructureCatalogs OSC-005).
**Impacto:** 500 espurio en ventana estrecha; sin corrupción.
**Recomendación:** capturar `UniqueConstraintViolationException`→`PositionSlotErrors.CodeConflict` (409); idealmente solución de plataforma para toda la familia.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### PS-D — La dependencia funcional carece de guard de ciclo en escritura
**Severidad:** Observación · **Categoría:** Correctness · **Ubicación:** `UpdatePositionSlotDependenciesCommandHandler` líneas 1047-1055 (sólo `directDependency` entra al `WouldCreateDirectCycle`).
**Condición:** la dependencia directa se valida contra ciclos; la funcional sólo contra self-reference. Un ciclo de dependencias funcionales puede persistirse.
**Impacto:** bajo — `PositionSlotGraphBuilder.Select` usa un set visitado (`selected.TryAdd`), así que el render del grafo es cycle-safe (no hay loop infinito). Es una asimetría de validación, no un fallo de disponibilidad.
**Recomendación:** decidir explícitamente: si los ciclos funcionales no tienen sentido de negocio, extender `WouldCreate*Cycle` a la dependencia funcional; si son válidos (relación "punteada"), documentarlo. (El builder ya está protegido.)
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### PS-E — Serialización GraphML/DOT en el controller + `EscapeDot` incompleto
**Severidad:** Observación · **Categoría:** Arquitectura/Robustez · **Ubicación:** `PositionSlotsController.BuildGraphMl`/`BuildDot`/`EscapeDot` (≈426-528).
**Condición:** ~100 líneas de formateo en el controller (mismo patrón que OrgUnits OU-006). `EscapeDot` sólo reemplaza `"`→`\"`, no escapa `\`; `Title` (≤180 chars) puede contener `\`. Además `BuildDot` emite doble salto de línea (`AppendLine` + `\n`).
**Impacto:** menor — DOT potencialmente malformado si un título trae `\` (GraphML/JSON no afectados). Testabilidad de la serialización sólo vía integración.
**Recomendación:** extraer a un `PositionSlotDiagramWriter` (paridad con `ReportExportRowWriter`); que `EscapeDot` escape también `\` (antes que `"`).
**Prioridad:** Baja · **Esfuerzo:** Medio · **Estado:** Abierto

### PS-F — `pageSize` sin `[Range]` + sin pagination guardrail
**Severidad:** Observación · **Categoría:** Contrato · **Ubicación:** `Search` (`[FromQuery] int pageSize = DefaultPageSize`).
**Condición:** el param carece de `[Range(1, MaxPageSize)]`; no hay `PositionSlotPaginationGuardrailsTests`. El validator `InclusiveBetween(1,100)` cubre el caso funcional (→400).
**Impacto:** inconsistencia de contrato; `page` sin tope (riesgo sistémico int-overflow). Mismo que OrgUnits OU-005.
**Recomendación:** `[Range]` + guardrail (o aceptar como deuda sistémica de plataforma).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### PS-G — Cycle-check de `UpdateDependencies` sin cap §PS4
**Severidad:** Observación · **Categoría:** Rendimiento · **Ubicación:** `UpdatePositionSlotDependenciesCommandHandler` línea 1049 (`GetGraphNodesAsync` sin `CountSlotsAsync`).
**Condición:** carga el grafo completo del tenant para el chequeo de ciclo, sin el cap que sí protege la ruta de lectura (§PS4).
**Impacto:** bajo — mutaciones son menos frecuentes; pero un tenant patológico pagaría el wide-load en `/dependencies`.
**Recomendación:** reusar `CountSlotsAsync` (o una traversal acotada) antes del load.
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

## 8. Hallazgos fuera de alcance / trazabilidad

- **Remediación de deuda previa confirmada:** §X-AUTHZ/§X-RATE/§X-OPENAPI/§X-VER/§PS1-8/§X-TEST1-2 verificados presentes y verdes (167 unit). Esta auditoría **no contradice** "deuda = 0"; añade hallazgos nuevos (PS-A..G) que la lente de deuda no cubría (concurrencia-If-Match, Create-Location, ciclo-funcional, EscapeDot, [Range]).
- **PS-C** sistémico (unique-constraint→409) — candidato a solución de plataforma con CC/OU/OSC.
- **Modelo de referencia:** PositionSlots es el ejemplo canónico a seguir por OrgStructureCatalogs (doc 16 §11) en todo salvo la concurrencia (donde ambos comparten el body-token a migrar).
- **Edición en curso de Locations** (no relacionada): rompió el build transitoriamente; estable al ejecutar las pruebas.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|
| PS-A | Baja | Concurrencia/Contrato | Body-token (no If-Match) + Swagger ETag inexacto | Bajo/Medio | Media | Corregir doc GetById; evaluar If-Match |
| PS-B | Baja | Contrato | Create sin Location/ETag | Bajo | Baja | `ToCreatedAtActionResult` |
| PS-C | Baja | Concurrencia | Race dup-code → 500 | Bajo | Baja | Catch `UniqueConstraintViolationException`→409 |
| PS-D | Obs | Correctness | Dep. funcional sin cycle-check | Bajo | Baja | Decidir/extender o documentar |
| PS-E | Obs | Arquitectura | Serialización en controller + EscapeDot | Medio | Baja | Extraer writer; escapar `\` |
| PS-F | Obs | Contrato | pageSize sin `[Range]` | Bajo | Baja | `[Range]` + guardrail |
| PS-G | Obs | Rendimiento | Cycle-check sin cap §PS4 | Bajo | Baja | Cap antes del load |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`PositionSlotsController`) | **Aprobado con observaciones** |
| Seguridad | Aprobado |
| Arquitectura | Aprobado (con PS-E) |
| Contrato | Aprobado con observaciones (PS-A/PS-B) |
| Performance | Aprobado |
| Concurrencia | Aprobado con observaciones (PS-C) |
| Pruebas | Aprobado (cobertura fuerte) |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**El controlador es maduro, seguro y canónico — el de mayor calidad de la ola.** Puede avanzar a QA. Los hallazgos son consistencia/robustez menores; ninguno bloquea.

## 11. Recomendaciones finales

1. **PS-A (doc):** corregir el Swagger de GetById (no emite ETag) — quick win de exactitud. Evaluar la migración a `If-Match` junto con OrgStructureCatalogs para una concurrencia coherente en toda la API.
2. **PS-B/PS-C:** `ToCreatedAtActionResult` en Create + catch unique-constraint→409 (preferible solución de plataforma con CC/OU/OSC).
3. **PS-D/PS-E/PS-F/PS-G:** limpieza oportunista (decisión sobre ciclo funcional; extraer diagram-writer + escapar `\`; `[Range]`; cap del cycle-check).
4. **Usar PositionSlots como plantilla canónica** para alinear OrgStructureCatalogs (doc 16) — comparten todo el patrón salvo la concurrencia.
5. Mantener las fortalezas: §PS1 autz optimizada, §PS3 join único, §PS4 cap, §PS5 errores tipados, MinSearchLength, índices, guardrails 4/5, cobertura de pruebas.

## 12. Anexos / Evidencia revisada

- Controller: `PositionSlotsController.cs` (10 endpoints + serializadores + 5 DTOs).
- Aplicación: `PositionSlotAdministration.cs` (DTOs/10 validators/10 handlers + `PolicyAdapter`/`GraphBuilder`/`CommandSupport`/`DependencyAnalyzer`), `PositionSlotCommon.cs`, `PositionSlotPolicies.cs`, `PositionSlotRateLimitPolicies.cs`.
- Dominio: `PositionSlot.cs`, `PositionSlotDomainException.cs`, `PositionSlotEnums.cs`, `PositionSlotNormalization.cs`.
- Persistencia: `IPositionSlotRepository.cs`, `PositionSlotRepository.cs`, `PositionSlotConfiguration.cs`.
- Seguridad/reportes: `PositionSlotAuthorizationService.cs` (`EvaluateAccessAsync`), `Program.cs` (policies + rate-limit), `PositionSlotsExportHandler.cs`, `ReportExportResourceAuthorizer.cs`.
- Pruebas: `PositionSlotDomainTests.cs`, `PositionSlotAuthorizationServiceTests.cs`, `PositionSlotAdministrationTests.cs`, `PositionSlotDomainErrorMappingGuardrailsTests.cs`, `PositionSlotGraphCapGuardrailsTests.cs`, `PositionSlotSearchValidatorTests.cs`, `ApiIntegrationTests.cs` (11 PositionSlots_* métodos).
- Ejecución: `dotnet test --filter ~PositionSlot|~RateLimitingGovernance|~OpenApiContractGuardrails|~AuthorizationPolicyConventionGovernance|~ConcurrencyTokenMappingGuardrails` → **167/167 passed** (sesión 2026-06-07).

## 13. Estado de remediación (2026-06-07, uncommitted)

**Los 7 hallazgos cerrados** (PS-A…PS-G), decididos por el usuario: **PS-A/PS-B = alineación canónica completa incl. el breaking `If-Match`** (coherente con OrgStructureCatalogs doc 16) y **PS-D = ciclos funcionales tratados como inválidos** (extender el cycle-check). Verificación: build **0/0** · unit **1696/1696** (incl. nuevos) · integración PositionSlots **14/14** · guardrails de contrato (OpenAPI/Public) **7/7** · `openapi.yaml` editado (sección PositionSlots) y validado. **Sin migración** (el nombre del índice no cambia, sólo se single-sourcea) · **resx**: 1 mensaje generalizado (en+es).

> ⚠️ **BREAKING (frontend debe actualizar):** las 4 mutaciones (`PUT` + `PATCH /status·/dependencies·/occupancy`) ahora reciben el `concurrencyToken` en el header **`If-Match`** (ya no en el body). `Create` devuelve `201 + Location + ETag`; `GET by id` emite el `ETag`.

| ID | Estado | Remediación |
|---|---|---|
| **PS-A** | ✅ Cerrado | Las 4 mutaciones migradas a `[FromIfMatch] Guid concurrencyToken` + `ToActionResultWithETag`; `GetById` → `ToActionResultWithETag` (emite el `ETag` real, el Swagger ya es veraz); `ConcurrencyToken` eliminado de los 4 request DTOs. Integración: las 12 pruebas migradas (`PutJsonAsync`/`PatchJsonAsync` espejan el token al header) + nueva `PositionSlots_Update_WithoutIfMatch_ShouldReturn400`. |
| **PS-B** | ✅ Cerrado | `Create` usa `ToCreatedAtActionResult(nameof(GetById), publicId, ConcurrencyToken)` → `201 + Location + ETag` (antes `StatusCode(201, value)` plano). |
| **PS-C** | ✅ Cerrado | `CreatePositionSlotCommandHandler` atrapa `UniqueConstraintViolationException when PositionSlotConstraintViolations.IsCodeConflict(...)` → `409 POSITION_SLOT_CODE_CONFLICT`; nombre de índice **single-sourced** en `PositionSlotValidationRules.CodeUniqueConstraintName` (usado por `PositionSlotConfiguration.HasDatabaseName` + el helper; valor idéntico → **sin migración**). |
| **PS-D** | ✅ Cerrado | La dependencia **funcional** ahora se valida contra ciclos simétricamente con la directa: `PositionSlotDependencyAnalyzer` generalizado (`WouldCreateDirectCycle`/`WouldCreateFunctionalCycle` sobre un selector); el handler evalúa ambas → `409 POSITION_SLOT_DEPENDENCY_CYCLE`. Mensaje del error generalizado (en+es) a "dependency" (ya no "direct"). +3 unit del analyzer + integración `PositionSlots_Dependencies_WhenFunctionalCycleDetected_ShouldReturn409Conflict`. |
| **PS-E** | ✅ Cerrado | Serialización GraphML/DOT/JSON extraída a `PositionSlotDiagramWriter` (stateless, `CLARIHR.Api.Common`, `AddSingleton`); el controller sólo despacha formato + audit. `EscapeDot` ahora escapa `\` **antes** que `"` (defecto real corregido) y se eliminó el doble salto de línea. +5 unit `PositionSlotDiagramWriterTests`. |
| **PS-F** | ✅ Cerrado | `[Range(1, PositionSlotValidationRules.MaxPageSize)]` en `Search.pageSize` + nuevo `PositionSlotPaginationGuardrailsTests` (estructural `^PositionSlot`). **Guardrails de familia ahora 5/5.** `openapi.yaml`: `minimum:1/maximum:100` en `pageSize`. |
| **PS-G** | ✅ Cerrado | El cycle-check de `/dependencies` ya **no** carga el wide-join de 8 tablas (`GetGraphNodesAsync`): nuevo `IPositionSlotRepository.GetDependencyAdjacencyAsync` (proyección de 1 tabla, sólo ids de adyacencia) alimenta el analyzer. Resuelve PS-G y habilita PS-D en una sola carga ligera. |

**Estado:** **commiteado en `c420434`** (2026-06-08). El cambio de contrato es **breaking** para el frontend (concurrencia por `If-Match`). Ver §14 (re-auditoría).

## 14. Re-auditoría (2026-06-08)

**Veredicto: APROBADO — la remediación PS-A…PS-G se confirma íntegra, correcta y commiteada (`c420434`).** Re-auditoría independiente con lente de seguridad/correctness (mismo patrón que CostCenters/GeneralCatalogs/Files el 2026-06-07). **No se hallaron vulnerabilidades críticas/altas/medias nuevas.**

**Verificación de las 7 correcciones (en código commiteado):**

| ID | Verificado |
|---|---|
| PS-A | `[FromIfMatch] Guid concurrencyToken` en las 4 mutaciones + `GetById`/`Update`/`UpdateStatus`/`UpdateDependencies`/`UpdateOccupancy` con `ToActionResultWithETag`; los request DTOs ya no llevan token; integ `PositionSlots_Update_WithoutIfMatch_ShouldReturn400` (línea 8349). |
| PS-B | `Create` → `ToCreatedAtActionResult(nameof(GetById), {publicId}, ConcurrencyToken)` (201 + Location + ETag). |
| PS-C | `catch UniqueConstraintViolationException when PositionSlotConstraintViolations.IsCodeConflict → 409`; nombre de índice single-sourced en `PositionSlotValidationRules.CodeUniqueConstraintName` (EF `HasDatabaseName` línea 99 == el guard; sin migración). |
| PS-D | `WouldCreateFunctionalCycle` simétrico con el directo (analyzer generalizado sobre un selector `next`); error `DependencyCycle` type-agnostic; integ `PositionSlots_Dependencies_WhenFunctionalCycleDetected_ShouldReturn409Conflict` (línea 8317). |
| PS-E | `PositionSlotDiagramWriter` extraído (`CLARIHR.Api.Common`, `AddSingleton`); `EscapeDot` escapa `\` **antes** que `"` (defecto real corregido); doble salto eliminado; +5 unit `PositionSlotDiagramWriterTests`. |
| PS-F | `[Range(1, MaxPageSize)]` en `Search.pageSize` + `PositionSlotPaginationGuardrailsTests` (estructural `^PositionSlot`). **Guardrails de familia 5/5.** |
| PS-G | `GetDependencyAdjacencyAsync` (proyección de 1 tabla, filtrada por `TenantId`, sólo ids internos) alimenta el cycle-check; el wide-join de 8 tablas queda sólo en la ruta de lectura (capada §PS4). |

**Evidencia:** build **0/0**; unit PositionSlot + familia (rate-limit / OpenAPI / authz-convention / concurrency / pagination) **191/191 verdes**; integración presente (tenant-mismatch / without-permission / without-If-Match / functional-cycle), no ejecutada (sin DB → limitación).

**Re-verificación de seguridad (ampliando la lente más allá de PS-A…G):**
- **Aislamiento de tenant / anti-IDOR intacto:** `tenantContext.TenantId == companyId` en `EvaluateAccessAsync` y `EnsureAuthorizedAsync`; rutas id-only resuelven el tenant del JWT + filtro global fail-closed + `*ExistsOutsideTenant*` (404-vs-403).
- **Confirmado (no detallado en la auditoría original):** no se puede adjuntar un **rol de otro tenant** a un slot — `IamAdministrationRepository.FindRoleByPublicIdAsync` consulta `dbContext.IamRoles` **sin** `IgnoreQueryFilters`, así que el filtro global de tenant aplica (work-center / job-profile / dependencias direct+funcional ya se resuelven tenant-scoped vía `Resolve*IdAsync`).
- **Superficie adyacente limpia** (contraste con Files/REX-1): los únicos lectores cross-feature de `position_slots` son la finalización de PersonnelFiles (`FinalizePersonnelFile` — linkage validado in-tenant con manejo de `TenantMismatch`, gated por la autz de PersonnelFiles) y el export async (`PositionSlotsExportHandler`, gated por REX-1 / doc 18). **Ningún backdoor de exposición de datos sin autorización.**
- **Anti-mass-assignment intacto:** DTOs cerrados; status/occupancy sólo por PATCH dedicados; cost-center inferido del org unit del job profile.

**Hallazgos nuevos (todos Observación/nit — ninguno bloquea; ✅ AMBOS REMEDIADOS 2026-06-08 por decisión del usuario, ver §14.1):**

### RA-1 — TOCTOU del cycle-check cross-slot (Observación · Baja · pre-existente)
**Ubicación:** `UpdatePositionSlotDependenciesCommandHandler` línea 1060 (`GetDependencyAdjacencyAsync` se lee **fuera** de la transacción).
**Condición:** el token de concurrencia protege un único slot, pero la invariante de aciclicidad abarca varios slots. Dos `/dependencies` concurrentes sobre slots distintos (A→B y B→A) pueden pasar ambos el chequeo contra una adyacencia obsoleta y persistir conjuntamente un ciclo (cada write toca una fila distinta → tokens distintos, sin colisión).
**Impacto:** bajo. El builder del grafo es cycle-safe (`PositionSlotGraphBuilder.Select` usa `selected.TryAdd`) → sin loop infinito / DoS en la lectura; el ciclo persistido es una anomalía de datos benigna. **No es regresión** — el cycle-check directo ya tenía esta propiedad antes de PS-D; PS-D la extendió al funcional sin cambiar el perfil de carrera.
**Recomendación:** documentar como limitación conocida y aceptada (un fix robusto exige aislamiento serializable en la mutación o un lock por-tenant — no justificado para esta severidad), o cerrar la ventana re-chequeando dentro de la transacción con isolation serializable.

### RA-2 — `DiagramExport` computa el grafo antes de validar `format` (nit)
**Ubicación:** `PositionSlotsController.DiagramExport` (la query `GetPositionSlotGraphQuery` corre antes del branch por formato).
**Condición:** un `format` desconocido dispara `CountSlotsAsync` + cap §PS4 + wide-join antes de devolver `400 DIAGRAM_FORMAT_INVALID`.
**Impacto:** trabajo desperdiciado en una petición inválida, acotado por el rate-limit Export (10/ventana).
**Recomendación:** validar `format` contra el set conocido al inicio del endpoint (fix trivial). Marginal.

### RA-3 — overflow de `page` (nota, no re-flagged)
`(page - 1) * pageSize` en `SearchAsync` con `page` sin tope superior sigue siendo **deuda sistémica de plataforma aceptada** (PS-F la difirió explícitamente; candidato a `IPipelineBehavior` de clamp para toda la API). No es específico de PositionSlots.

### 14.1 Remediación RA-1 / RA-2 (2026-06-08, uncommitted)

El usuario optó por **arreglar ambos en código** (no sólo documentar). build **0/0** · unit **1703/1703** (incl. nuevo test del lock) · **sin migración** · **sin cambio de contrato → openapi.yaml intacto** (RA-2 sólo adelanta el `400` ya existente; RA-1 es un lock interno) · sin resx.

| ID | Estado | Remediación |
|---|---|---|
| **RA-1** | ✅ Cerrado | El cycle-check se movió **dentro de la transacción** de `UpdatePositionSlotDependenciesCommandHandler`, precedido por un **advisory lock por-tenant** transaction-scoped: nuevo `IPositionSlotRepository.AcquireDependencyMutationLockAsync` → `pg_advisory_xact_lock(classId, tenantKey)` vía `ExecuteSqlRawAsync` (se enlista en la transacción del `UnitOfWork` —mismo `ApplicationDbContext`— y se libera en commit/rollback). Serializa sólo las mutaciones de dependencias del mismo tenant; `Create` no lo toma (un slot nuevo no tiene aristas entrantes → no puede cerrar un ciclo). En ciclo → rollback + `409 POSITION_SLOT_DEPENDENCY_CYCLE`. Regresión cubierta por `Dependencies_WithValidDirectDependency_AcquiresTenantLockAndSucceeds` (afirma que el lock se adquiere 1×) + la integ de ciclo funcional ya existente. |
| **RA-2** | ✅ Cerrado | `DiagramExport` valida `format` **antes** de computar el grafo vía `ResolveDiagramFormat` (single source del set soportado + content-type + filename + writer); un formato desconocido → `400` sin pagar `CountSlotsAsync`/cap/wide-join. De paso elimina la triplicación de `LogExportAsync`. |
| **RA-3** | ➖ | Sin cambios — deuda sistémica de plataforma aceptada (clamp de `page` a nivel `IPipelineBehavior`). |

**Pendiente:** commit (lo maneja el usuario; default [[user-handles-commits-and-merges]]).
