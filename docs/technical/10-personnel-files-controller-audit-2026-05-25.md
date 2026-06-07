# 10 — Auditoría `PersonnelFilesController` (shell) post-remediación CRUD (2026-05-25)

> **Propósito**: certificar si la remediación del 2026-05-25 (que llevó `PersonnelFilesController` al patrón canónico GET/POST/PUT/PATCH de JobProfiles, branch `feat/personnel-files/shell-crud-canonical-patterns`) dejó deuda técnica en el controlador, validado contra la definición del proyecto en **seguridad, performance y arquitectura** (`docs/technical/overview/project-foundation.md` §4, §9, §12.5–§12.8) y el playbook de auditoría `AGENTS.md §17.4`.
>
> **Metodología**: lectura completa de `PersonnelFilesController` + sus handlers/validators en Application (`Create`, `Search`, `GetById`, `Update`/`UpdatePersonnelFilePersonalInfoCommand`, `Patch`/`PatchPersonnelFileCommand` + `PersonnelFilePatchApplier`) + repositorio (`PersonnelFileRepository.SearchAsync`/`GetShellByIdAsync`/`GetPersonalInfoAsync`) + plumbing de authz (`Program.cs`, `PersonnelFileAuthorizationService`, `AuthorizationPolicyConvention`). Verificación item por item del checklist `§17.4` contra el código real, no contra prosa. Spot-checks de esta auditoría: `dotnet build CLARIHR.slnx` **0/0**; unit suite **686/686** (incluye el guardrail de governance extendido + 3 tests nuevos de PATCH); `dotnet ef migrations has-pending-model-changes` → *"No changes since the last migration"*. La suite de integración (testcontainers) **no se ejecutó** (Docker no disponible en el entorno de auditoría); los tests se actualizaron al nuevo contrato y compilan.
>
> **Alcance**: **`PersonnelFilesController`** (los 5 endpoints del shell: `POST /companies/{companyPublicId}/personnel-files`, `GET …/personnel-files`, `GET /personnel-files/{publicId}`, `PUT /personnel-files/{publicId}`, `PATCH /personnel-files/{publicId}`) + su vertical de Application/Infra. **Fuera de alcance** (registrado en §3 sólo para trazabilidad): los **8 controllers hermanos** del feature (`PersonnelFile{PersonalInfo,Background,Compensation,Documents,Employment,Interests,Talent,Reporting}Controller`), que **no** fueron parte de esta remediación.
>
> **Veredicto headline**: **El shell quedó sin deuda técnica significativa. Sin brecha de seguridad explotable. Sin violación de arquitectura. Sin regresión de tenant-isolation / concurrencia / contrato de error.** Backlog propio del shell: **0×🔴, 0×🟠, 1×🟡 (pre-existente y rastreada), 3×🟢**. La deuda material del feature es de **consistencia a nivel de familia** (los 8 hermanos no migrados) — **alcance separado**: **1×🟠, 3×🟡**.

---

## 1. Certificación del checklist `§17.4` contra código (shell)

Verificado contra código 2026-05-25. **Todos PASS salvo §12.8 (ver §PF1).**

| Checklist §17.4 | Verificado en código | Estado |
|---|---|---|
| **Authz (dos capas)** | `[AuthorizationPolicySet(PersonnelFilePolicies.Read, Manage)]` en la clase; `AuthorizationPolicyConvention` asigna Read→GET, Manage→POST/PUT/PATCH. | ✅ PASS |
| **Authz superset (revisión MANUAL)** | Política declarativa `Program.cs` Read=`{Read,Admin,iam.administration.manage}`, Manage=`{Admin,iam.administration.manage}` **⊇** gate del handler `PersonnelFileAuthorizationService.EnsureCanRead/ManageAsync` (mismos claims). Sin 403 falso. | ✅ PASS |
| **Tenant** | Todo handler valida `tenantContext.TenantId` + `EnsureCan*Async(companyId)` (tenant-mismatch → `TenantMismatch`); repo filtra por `tenantId` (`BuildBaseQuery`, query filter global de `TenantEntity`); 404 vs cross-tenant vía `ExistsOutsideTenantAsync`. Sin `IgnoreQueryFilters`. | ✅ PASS |
| **Rate limit** | `personnel-files-create` (POST), `personnel-files-search` (GET search), `personnel-files-lifecycle` (PATCH). | ✅ PASS |
| **Paginación** | `Search` declara `[Range(1, PersonnelFileValidationRules.MaxPageSize)]` en `pageSize`; el validator hace `InclusiveBetween(1, MaxPageSize)` (max == max). | ✅ PASS |
| **OpenAPI** | `[ApiVersion("1.0")]` + ruta `api/v{version:apiVersion}` + `[Tags("Personnel Files")]` + `[ProducesStandardErrors]` (×5) + `[SwaggerOperation]` (×5). | ✅ PASS |
| **Errores** | `Result` + `ProblemDetails`; conflicto de negocio/estado → 409/422 vía `PersonnelFileErrors`; concurrencia → `CONCURRENCY_CONFLICT` (409); `If-Match` ausente/malformado lo maneja `IfMatchModelBinder`. Sin stack traces. | ✅ PASS |
| **Concurrency** | `PersonnelFile.ConcurrencyToken` mapeado `.IsConcurrencyToken()`; PUT/PATCH exigen `[FromIfMatch]`; handlers comparan token y devuelven el nuevo en `ETag`. | ✅ PASS |
| **PII / sensibles** | El shell expone PII (nombres, emails, fechas) detrás del gate del módulo (RBAC + tenant + entitlement `CommercialModuleKeys.PersonnelFiles`). Sin campos salariales en el shell (viven en sub-recursos). Sin logging de input no acotado. | ✅ PASS |
| **Wire contract** | Rutas conservan `{companyPublicId}`/`{publicId}` (sin cambio de contrato); queries proyectan a DTO (sin exponer la entidad). | ✅ PASS |
| **Performance (lecturas)** | `SearchAsync`/`GetShellByIdAsync`/`GetPersonalInfoAsync` proyectan con `.Select(...)` (sin entidad completa) y `AsNoTracking`; nombres de catálogo resueltos en **batch** por código distinto (sin N+1); `AllowedActions` deriva sólo de `canManage` (§12.7, sin dependencia por ítem). | ✅ PASS |
| **Performance (free-text §12.8)** | `Search` filtra `Normalized*.Contains(q)` (`LIKE '%x%'`, no-sargable) **sin longitud mínima de `q`**. | ⚠️ Ver §PF1 |
| **Guardrail drift-proof** | `AuthorizationPolicyConventionGovernanceTests` extendido (allow-list `PersonnelFilePolicyNames` + Inv-1 para `IPersonnelFileAuthorizationService`); `GovernedFamilyRegex` **no** extendido a propósito (alcance shell). Sanity rojo→verde demostrado al construir. | ✅ PASS |

> **Conclusión §1**: el shell es **limpio en seguridad y arquitectura**, con tenant-isolation, concurrencia optimista, contrato de error y autorización de dos capas (declarativa ⊇ gate) verificados. La única desviación del foundation es §12.8 (search free-text), que es **deuda pre-existente y rastreada** (§PF1), no introducida por esta remediación.

---

## 2. Hallazgos en el shell

### §PF1 🟡 `Search` sin longitud mínima de `q` (§12.8) — *performance, pre-existente y rastreada*
**Dónde**: `SearchPersonnelFilesQueryValidator` (`PersonnelFileAdministration.cs:1006`) sólo declara `RuleFor(q => q.Search).MaximumLength(150)`, sin mínimo; `PersonnelFileRepository.ApplySearch` (`:114`) aplica `Normalized*.Contains(q)` → `LIKE '%x%'` no-sargable (con `includeIdentificationMatch: true`).
**Qué pasa**: `project-foundation.md §12.8` exige **longitud mínima de `q` (tras `Trim()`, `MinSearchLength = 2`) en el validador** como guardrail obligatorio para nuevos search. Esta búsqueda **no** la aplica. **No fue introducida por esta remediación** (el endpoint y su validador preexisten; el cambio sólo añadió `[Range]` en `pageSize`) y es **deuda app-wide conocida y aceptada**: §12.8 declara explícitamente *"el mismo patrón `.Contains()` existe en el search de Job Profiles y en 23+ repositorios… Deuda conocida"*, registrada en `ADR-0002`. JobProfiles (el controlador de referencia) tampoco la aplica.
**Mitigación vigente**: el endpoint está rate-limited (`personnel-files-search`, 120/ventana) y el volumen por tenant de expedientes es acotado (supuesto de escala §12.8).
**Severidad**: 🟡 Media-baja (perf bajo carga; no explotable; mitigada por rate limit + escala acotada).
**Acción concreta**: opcional pero recomendada para dejar el shell por delante del patrón — añadir `MinSearchLength = 2` en `SearchPersonnelFilesQueryValidator` (rechazo 400 antes de DB) y declarar el supuesto de escala; o diferir al barrido app-wide de `ADR-0002`. No es bloqueante.

> ✅ **Cerrada (2026-05-25)** — branch `perf/personnel-files/search-min-length`. Remediado y **extendido a las 4 superficies free-text** del módulo (Search / Export / DynamicQuery / Analytics), no sólo el shell: `PersonnelFileValidationRules.MinSearchLength = 2` + `MaxSearchLength = 150` + helper `IsValidSearchLength` (espejo exacto de PositionSlots §PS2 / PDC §P2) con el supuesto de escala declarado en el comentario (§12.8 / ADR-0002). Guardrail nuevo `PersonnelFileSearchValidatorTests` (43 casos: las 4 superficies + helper + pin al umbral del precedente) con sanity **rojo (10 fallos) → verde**. `dotnet build` 0/0, unit suite **729/729**.

### §PF2 🟢 Pérdida de granularidad de auditoría activar/inactivar — *observabilidad*
**Dónde**: `PatchPersonnelFileCommandHandler` emite siempre `AuditEventTypes.PersonnelFileUpdated`. Las constantes `PersonnelFileActivated`/`PersonnelFileInactivated` (`AuditCatalog.cs:106-107,236-237`) quedaron **definidas pero sin referencia** tras eliminar los handlers `Activate`/`Inactivate`.
**Qué pasa**: antes, activar/inactivar generaban eventos de auditoría específicos; ahora un PATCH que sólo togglea `isActive` se audita como `PersonnelFileUpdated` (con `before`/`after`, así que el cambio sí queda trazado, pero con menos especificidad de evento). Además quedan 2 constantes muertas.
**Severidad**: 🟢 Baja (no se pierde el registro del cambio, sólo la etiqueta del evento; sin impacto de seguridad/compliance del dato).
**Acción concreta**: o (a) detectar la transición de `isActive` en el handler y emitir `PersonnelFileActivated`/`PersonnelFileInactivated` (preserva la señal), o (b) eliminar las 2 constantes muertas si se acepta `PersonnelFileUpdated` como única señal.

> ✅ **Cerrada (2026-05-25)** — branch `perf/personnel-files/search-min-length`. Elegida la **opción (a)** por ser la que **no quiebra el patrón**: el PATCH de JobProfiles (controlador de referencia) ya emite eventos específicos en transición de `status` (`JobProfilePublished`/`JobProfileArchived`, si no `JobProfileUpdated`). `PatchPersonnelFileCommandHandler` ahora espeja ese idioma: captura `wasActive` y, vía un `switch` sobre `(wasActive, IsActive)`, emite `PersonnelFileActivated` (Reactivate) / `PersonnelFileInactivated` (Deactivate) en la transición, o `PersonnelFileUpdated` en patch de sólo-núcleo — el `before/after` sigue capturando todo. Las 2 constantes dejan de estar muertas. Guardrail: `TestAuditService.LastEntry` + 3 asserts de `EventType` en `PersonnelFilesCoreCommandTests` (Activated/Inactivated/Updated) con sanity **rojo (2 fallos) → verde**. Build 0/0, unit **730/730**.

### §PF3 🟢 `PatchPersonnelFileCommandHandler` duplica el cuerpo de `UpdatePersonnelFilePersonalInfoCommandHandler` — *mantenibilidad*
**Dónde**: el handler de PATCH replica el flujo del de UPDATE (plan de foto + `UpdatePersonalInfo` + auditoría + persistencia transaccional) y añade el toggle de `isActive`.
**Qué pasa**: decisión **deliberada** de la remediación para no refactorizar un handler con cobertura de tests (menor riesgo). Costo: un cambio futuro a la persistencia de personal-info debe tocar ambos.
**Severidad**: 🟢 Baja (duplicación acotada y aislada; ambos cubiertos por tests).
**Acción concreta**: opcional — extraer un método privado compartido (p. ej. en `ReplacePersonnelFileSectionCommandHandlerBase`) cuando se vuelva a tocar personal-info.

> ✅ **Cerrada (2026-05-25)** — branch `perf/personnel-files/search-min-length`. Extraído `ReplacePersonnelFileSectionCommandHandlerBase.ApplyPersonalInfoAsync` (validación de catálogo + plan de foto + transacción `UpdatePersonalInfo` + toggle opcional de `isActive` + auditoría + persistencia). Ambos handlers ahora delegan ahí: el de UPDATE pasa `desiredIsActive: <actual>` (no togglea) + `auditFactory` fijo `PersonnelFileUpdated`; el de PATCH pasa `desiredIsActive: state.IsActive` + `auditFactory` por transición. El `auditFactory` se evalúa **post-mutación**, preservando exactamente la semántica de auditoría de cada uno (incl. `FullName` y la etiqueta de evento — §PF2 intacto). UPDATE pasó de ~100 a ~12 líneas; PATCH de ~127 a ~15. Verificado: build 0/0, unit **730/730** (los tests de PATCH ejercitan el método compartido). Sin red→verde porque es refactor (sin nuevo guardrail): la suite existente es la red de seguridad.

### §PF4 🟢 Naming/cohesión menores — *claridad*
**Dónde**: (a) el `PUT /personnel-files/{publicId}` despacha `UpdatePersonnelFilePersonalInfoCommand` (el nombre del comando ya no refleja que es el "update del shell", sólo se conservó para no tocar tests); (b) `PatchPersonnelFileCommandHandler` instancia `new UpdatePersonnelFilePersonalInfoCommandValidator()` directamente (no vía DI) para reusar las reglas.
**Qué pasa**: ambos son nits internos sin efecto en el contrato público ni en el comportamiento. El validador no tiene dependencias de DI, así que instanciarlo es seguro.
**Severidad**: 🟢 Baja (cosmético/cohesión).
**Acción concreta**: opcional — renombrar el comando a `UpdatePersonnelFileCommand`, o dejar como YAGNI.

> ✅ **Cerrada (2026-05-25)** — branch `perf/personnel-files/search-min-length`. **(a)** Renombrado `UpdatePersonnelFilePersonalInfoCommand` → `UpdatePersonnelFileCommand` (+ `…CommandHandler`, `…CommandValidator`, y todas las referencias en controller / PATCH handler / método compartido / tests), alineado con `UpdateJobProfileCommand`. Sin impacto en el contrato público (el comando es interno; el PUT sigue recibiendo `UpdatePersonnelFileRequest`). **(b)** Se **mantiene** `new UpdatePersonnelFileCommandValidator()` en el PATCH (no se inyecta vía DI): la validación está centralizada en `RequestDispatcher` y **ningún handler inyecta `IValidator<T>`** (el único uso de `IValidator<>` es composición de validadores con `SetValidator`); inyectarlo solo aquí rompería ese patrón, y el validador no tiene dependencias de DI, así que `new` es seguro (la propia finding lo declara YAGNI). Verificado: build 0/0, unit **730/730**.

---

## 3. Hallazgos a nivel de familia (fuera del alcance del shell — trazabilidad)

> Estos hallazgos **no** son del shell remediado; son de los **8 controllers hermanos** que conservan el estilo divergente previo. Se registran aquí porque son la deuda **material** del feature y el siguiente paso lógico, pero son **alcance separado** (cada uno es un finding propio, espejo de cómo doc `04`/`06` separaron JobCatalogs y los transversales). Verificado 2026-05-25: de los 9 controllers `PersonnelFile*`, **sólo `PersonnelFilesController` (shell)** porta `[AuthorizationPolicySet]`/`[ApiVersion]`/`[Tags]`/`[ProducesStandardErrors]`/`[SwaggerOperation]`/rate-limit; los otros 8 tienen todos esos contadores en **0**.

### §PF-FAM1 🟠 Los 8 hermanos fuera de `AuthorizationPolicyConvention` *(seguridad — defensa en profundidad)*
**Qué pasa**: `PersonnelFile{PersonalInfo,Background,Compensation,Documents,Employment,Interests,Talent,Reporting}Controller` no portan `[AuthorizationPolicySet]` → heredan sólo la `FallbackPolicy` (autenticado + `client_type=Core`), sin la policy declarativa `PersonnelFiles.Read/Manage`. **No es un hueco real**: cada handler aplica `EnsureCanRead/ManageAsync` (gate preciso intacto, incluye campos salariales en `Compensation`). Es deuda de defensa en profundidad/consistencia. `GovernedFamilyRegex` se dejó **intencionalmente sin** `PersonnelFile` (si se añadiera, exigiría el marker en los 9 y rompería el build hasta migrarlos todos).
**Severidad**: 🟠 Media (def. en profundidad ausente en controllers con mutación de PII/salario; sin explotabilidad hoy).
**Acción concreta**: migrar los 8 a `[AuthorizationPolicySet(PersonnelFilePolicies.Read, Manage)]` y **extender `GovernedFamilyRegex`** a `PersonnelFile` (cierra el drift por construcción). `Compensation` podría requerir su propia policy de campo salarial (evaluar superset).

### §PF-FAM2 🟡 Los 8 hermanos sin contrato OpenAPI canónico *(arquitectura / contrato)*
**Qué pasa**: sin `[ApiVersion]`/ruta versionada (usan `api/v1` absoluta), sin `[Tags("Personnel Files")]`, sin `[ProducesStandardErrors]` (usan listas crudas `[ProducesResponseType<ProblemDetails>]`), sin `[SwaggerOperation]`. Swagger agrupa cada uno como huérfano y sin documentar.
**Severidad**: 🟡 Media (consistencia de superficie de contrato).
**Acción concreta**: espejo de la remediación del shell en los 8 (clase + endpoints).

### §PF-FAM3 🟡 Listados de sub-recursos sin paginación *(performance — §4.5)*
**Qué pasa**: 28 GET de colección en los hermanos devuelven `IReadOnlyCollection<T>` sin paginar (Background 5, Compensation 6, PersonalInfo 4, Employment 4, Talent 4, Interests 3, Documents 2). `project-foundation §4.5` prohíbe listados sin paginación; JobProfiles pagina sus sub-recursos (`PagedResponse<T>`). Atenuante: son colecciones hijas acotadas por expediente.
**Severidad**: 🟡 Media-baja (riesgo acotado por cardinalidad; desviación de patrón y de §4.5).
**Acción concreta**: decidir explícitamente — paginar (espejo JobProfiles) o documentar el supuesto de escala "colección hija acotada" como excepción registrada (ADR), para no dejarlo implícito.

### §PF-FAM4 🟡 Sub-recursos con token en body y `201` manual *(consistencia)*
**Qué pasa**: los Add/Update/Delete de sub-recursos usan `[FromBody] ConcurrencyRequest` (token en cuerpo) y `StatusCode(201, …)` manual, en vez de `[FromIfMatch]` + `ToCreatedAtActionResult` (exactamente la divergencia que el shell acaba de corregir).
**Severidad**: 🟡 Media-baja (consistencia de concurrencia/creación).
**Acción concreta**: migrar a `[FromIfMatch]` + helpers `ResultExtensions` (espejo del shell).

---

## 4. Tabla de priorización

| Item | Alcance | Severidad | Categoría | Esfuerzo | Acción |
|---|---|---|---|---|---|
| §PF1 ✅ | Shell | 🟡 | Performance (§12.8) | hecho | **Cerrada 2026-05-25** — `MinSearchLength=2` en las 4 superficies free-text + guardrail `PersonnelFileSearchValidatorTests` |
| §PF2 ✅ | Shell | 🟢 | Observabilidad | hecho | **Cerrada 2026-05-25** — opción (a): PATCH emite Activated/Inactivated/Updated por transición (espejo JobProfiles) + guardrail de `EventType` |
| §PF3 ✅ | Shell | 🟢 | Mantenibilidad | hecho | **Cerrada 2026-05-25** — `ApplyPersonalInfoAsync` compartido (UPDATE+PATCH); ~225 líneas duplicadas → 1 método |
| §PF4 ✅ | Shell | 🟢 | Naming | hecho | **Cerrada 2026-05-25** — (a) comando renombrado a `UpdatePersonnelFileCommand`; (b) `new` validator se mantiene (sin precedente de inyectar `IValidator` en handlers) |
| §PF-FAM1 | Familia | 🟠 | Seguridad (def. prof.) | 1–2 h | `[AuthorizationPolicySet]` ×8 + extender `GovernedFamilyRegex` |
| §PF-FAM2 | Familia | 🟡 | Arquitectura/OpenAPI | 2–3 h | OpenAPI canónico ×8 |
| §PF-FAM3 | Familia | 🟡 | Performance (§4.5) | 3–4 h | Paginar sub-recursos o ADR de excepción |
| §PF-FAM4 | Familia | 🟡 | Consistencia | 2–3 h | `[FromIfMatch]` + `ToCreatedAtActionResult` ×8 |

> **Sin items 🔴.** Ningún hallazgo es brecha explotable. **El shell (objeto de esta auditoría) está certificado sin deuda significativa**: lo único propio con sustancia (§PF1) es deuda app-wide pre-existente y rastreada (ADR-0002), y el resto son 🟢 menores. La deuda 🟠/🟡 real es de **familia** (los 8 hermanos) — el siguiente trabajo recomendado, como remediación separada espejo de la del shell.

## 5. Política de seguimiento

- Mover items a una sección "Cerradas" con fecha + commit/PR cuando se remedien (convención docs `02`/`03`/`04`).
- §PF-FAM1 es el candidato de mayor valor/seguridad: al migrar los 8 hermanos, **extender `GovernedFamilyRegex`** a `PersonnelFile` y el guardrail `EveryGovernedFamilyController_DeclaresPolicySetMarker` pasa a exigir el marker en los 9 por construcción (cierra §PF-FAM1 drift-proof).
- §PF1 **✅ cerrada (2026-05-25)**: se adelantó el guardrail §12.8 a las **4 superficies free-text** del módulo PersonnelFiles (Search/Export/DynamicQuery/Analytics), con `MinSearchLength=2` + guardrail de tests. El resto del patrón `.Contains()` app-wide (JobProfiles + 23 repos) sigue bajo `ADR-0002`.
- Cualquier controller nuevo del feature PersonnelFiles debe entrar con `[AuthorizationPolicySet]` + `[ApiVersion]`/`[Tags]`/`[ProducesStandardErrors]`/`[SwaggerOperation]` + `[Range]` + `[FromIfMatch]` desde el día 1 (no repetir el drift).
- Próxima revisión: al abrir la remediación de familia (§PF-FAM1–4) o si se toca personal-info (revisar §PF3).
