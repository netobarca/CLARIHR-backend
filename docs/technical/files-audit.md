# Auditoría Files — seguimiento

> **Documento vivo / tracker.** Se actualiza al cerrar cada hallazgo.
> **Creado:** 2026-06-06 · **Estado:** 🟢 Cerrado (FILE-1 ✅ PR-A · FILE-3/4/5 ✅ PR-B · FILE-2 ➖ observación · 2026-06-06; resta sólo el sub-ítem `[ApiVersion]`/`[ProducesStandardErrors]` diferido al plan de alineación canónica) · **Owner:** equipo backend
> **Alcance:** dominio Files completo — `FilesController` (`api/v1/files`, 4 endpoints: upload-session/complete/read-url/delete) + `Features/Files/` (CreateUploadSession/CompleteFileUpload/GetFileReadUrl/DeleteFile + Common) + `FileRepository` + `StoredFile` + Azure (`AzureBlobStorageProvider`, `BlobServiceClientFactory`, `FileObjectKeyBuilder`, `FilePurposeRuleProvider`) + `PendingFileCleanupBackgroundService`.
> **Dimensiones:** seguridad · arquitectura · rendimiento, contra `AGENTS.md` (§8, §17) y `docs/technical/overview/project-foundation.md` (§11).

---

## 1. Veredicto

Servicio de archivos **sólido en su infraestructura** (SAS bien acotadas, verificación server-side del upload, cliente blob singleton + delegation key cacheada, índices completos, cleanup batched), pero con **un hueco de autorización real en la lectura** y **deriva respecto al patrón canónico** (es un controlador de infraestructura previo a la estandarización).

- 🔴 **`GET /files/{id}/read-url` no autoriza** más allá de `[Authorize]` + filtro de tenant → **IDOR intra-tenant**: cualquier usuario autenticado del tenant puede obtener una SAS de lectura de **cualquier** archivo del tenant conociendo su `publicId`, incluidos documentos de personal sensibles. Asimétrico con `complete`/`delete`, que **sí** gatean por `CreatedByUserId`. (Cross-tenant **sí** está bloqueado — `StoredFile : TenantEntity`, filtro global EF.)
- **Verificado sólido:** aislamiento cross-tenant; SAS acotadas al blob exacto (write-only upload / read-only read, expiración corta, firmadas; user-delegation key en prod); `complete` verifica el blob server-side (`ExistsAsync` + `GetObjectInfo`) y usa metadata del servidor (no confía en el cliente) + gate de ownership; object-key construido con GUIDs server-side (sin path traversal); validación de content-type/extensión/tamaño por `purpose` (422/413).
- **Diferido (no re-flag):** `complete` usa PATCH con `concurrencyToken` en el body (no `If-Match`/RFC-6902). Decisión de diseño previa documentada — fuera de alcance de este tracker.

Lo accionable: **el gate de lectura ausente (P1, requiere decisión de modelo de authz), y deuda de alineación canónica/contrato + una micro-optimización del cleanup (P3).**

## 2. Leyenda de estado

⬜ pendiente · 🟡 en progreso · ✅ resuelto · ⏸️ diferido · ➖ descartado

| Severidad | P1 crítico/alto | P2 alto | P3 medio/bajo |
|---|---|---|---|
| Conteo | 1 | 0 | 4 |

---

## 3. Hallazgos accionables

| # | Dim | Sev | Estado | Hallazgo | Evidencia (`file:line`) | Fix propuesto |
|---|-----|-----|--------|----------|-------------------------|---------------|
| **FILE-1** | SEC | P1 | ✅ | **`read-url` sin autorización → IDOR intra-tenant de lectura.** `GetFileReadUrlQueryHandler` carga por `publicId` (filtrado por tenant), valida `Status==Active` y devuelve una SAS de lectura — **sin ningún chequeo de ownership/permiso** (ni siquiera inyecta `ICurrentUserService`). `complete` y `delete` **sí** validan `file.CreatedByUserId == userId`. Un usuario autenticado del tenant que obtenga/adivine el `publicId` de un archivo de otro usuario (p. ej. un documento de personal) recibe una SAS válida y lo descarga. Cross-tenant bloqueado (filtro global EF). | `GetFileReadUrl.cs:22-48` (sin gate) vs `CompleteFileUpload.cs:55-59` y `DeleteFile.cs:38-42` (gate `CreatedByUserId`) | **Requiere decisión de modelo de read-authz** (no un gate trivial): distintos `purpose` tienen necesidades distintas — `ProfileImage` se ve de forma amplia, `PersonnelDocument` debe restringirse. Opciones: (a) política de lectura por `purpose`; (b) delegar la autorización al dominio consumidor (que ya autoriza la entidad) y no exponer `read-url` como endpoint directo sin gate; (c) como mínimo inmediato, exigir ownership (`CreatedByUserId`) salvo para purposes explícitamente públicos. Nota: un gate uploader-only ciego **rompería** la visualización de avatares (relacionado con el rediseño de `complete` ya diferido). <br>**✅ Resuelto (decisión: delegado al dominio · PR-A · 2026-06-06):** (1) el endpoint genérico `GET /files/{id}/read-url` ahora es **owner-only** (`CreatedByUserId == caller` → `FileOwnershipMismatch` 403, espejo de complete/delete) — cierra el IDOR; +403 declarado en `[ProducesResponseType]`. (2) Nuevo endpoint **autorizado por dominio** `GET /personnel-files/{id}/documents/{docId}/read-url` (`GetPersonnelFileDocumentReadUrlQuery`/Handler) que reutiliza la **misma** authz del expediente que `GetPersonnelFileDocumentByIdQuery` (base `EnsureCanReadAsync` + 404/403-TenantMismatch) y mintea la SAS server-side (patrón del `PersonnelFileProfilePhotoService`). (3) Prosa Swagger de los GET de documentos actualizada para apuntar al nuevo endpoint. Las fotos de perfil ya usaban el servicio server-side (no afectadas). Anclado en `FileAccessControlTests` (owner→200, no-owner→403; red→green) + smoke DI. Build 0/0, unit 1618/0. |
| **FILE-2** | SEC | P3 | ➖ | **`upload-session` acepta `EntityId` arbitrario sin autorización.** `CreateUploadSessionCommand` recibe `EntityId` del cliente y lo persiste en `StoredFile.EntityId` sin verificar que el caller pueda adjuntar a esa entidad. **VERIFICADO: hoy es latente** — ningún repositorio/consulta lee archivos por `StoredFile.EntityId` (no hay método ni `Where` por `EntityId`), así que sólo queda un dato denormalizado sin uso. Riesgo si un futuro flujo lista/asocia archivos por ese campo. | `CreateUploadSession.cs` (persiste `command.EntityId`); `IFileRepository.cs`/`FileRepository.cs` (sin lookup por EntityId) | Si/cuando un consumidor lea por `StoredFile.EntityId`, autorizar el `EntityId` contra el tenant/permiso del caller en `upload-session`. Por ahora: observación documentada (no hay ruta de explotación). <br>**➖ Riesgo aceptado / observación (2026-06-06):** sin ruta de explotación hoy (nada lee por `StoredFile.EntityId`); reabrir si un consumidor empieza a listar/asociar archivos por ese campo. |
| **FILE-3** | ARCH | P3 | ✅ | **Deriva del patrón canónico + no enrolado en guardrails de contrato.** `FilesController` no tiene `[ApiVersion]` (rutas `api/v1/...` hardcodeadas), `[Tags]`, `[SwaggerOperation]` ni `[ProducesStandardErrors]`; usa `[ProducesResponseType<ProblemDetails>]` por endpoint. **No está en `OpenApiContractGuardrailsTests.Families[]`** ni hay carve-out → el guardrail no lo cubre (regresión silenciosa de documentación OpenAPI). *(La ausencia de `[AuthorizationPolicySet]` es by-design — handler-gated, sin `IFileAuthorizationService`; pero ese gating es justo el que falta en `read-url` → FILE-1.)* | `FilesController.cs:11-16`; `OpenApiContractGuardrailsTests.cs` (Families sin Files) | Decidir: alinear (`[ApiVersion]`+`[Tags("Files")]`+`[SwaggerOperation]` por endpoint, enrolar en el guardrail OpenAPI) o **carve-out explícito documentado** si Files se considera infra fuera del contrato canónico. <br>**✅ Resuelto (PR-B · 2026-06-06):** decisión = **alinear el contrato OpenAPI**. `[Tags("Files")]` en el controller + `[SwaggerOperation(Summary, Description)]` en los 4 endpoints + familia `Files` (`^Files`, sólo matchea `FilesController`) enrolada en `OpenApiContractGuardrailsTests.Families` (red→green). La authz declarativa (`[AuthorizationPolicySet]`) se mantiene **by-design** fuera (handler-gated; gating ya completado en FILE-1). **Diferido al plan de alineación canónica** (sub-ítem): `[ApiVersion]`+route-template `api/v{version}` y `[ProducesStandardErrors]` — es una reestructuración de routing que produce rutas idénticas y conviene batchear con esa iniciativa. Build 0/0, unit 1621/0. |
| **FILE-4** | ARCH/DOCS | P3 | ✅ | **Contrato OpenAPI incompleto en `complete`.** El handler puede devolver `409 CONCURRENCY_CONFLICT` (token stale) y `503 StorageProviderNotConfigured`, pero el endpoint sólo declara `200/401/403/404/422` (`[ProducesResponseType]`). El cliente que consulte el contrato no espera 409/503. | `FilesController.cs:39-44`; `FileErrors.cs` (ConcurrencyConflict→409, StorageProviderNotConfigured→503) | Declarar `409` (y `503` donde aplique) en los `[ProducesResponseType]` de `complete` (y revisar el resto de endpoints vs los `ErrorType` que producen). <br>**✅ Resuelto (PR-B · 2026-06-06):** `409 Conflict` declarado en `complete`. **`503` NO se declara — verificado:** `StorageProviderNotConfigured` está **definido pero nunca usado** (ningún handler lo devuelve), así que no es un código alcanzable. Revisados los 4 endpoints vs sus `ErrorType` reales. |
| **FILE-5** | PERF | P3 | ✅ | **Doble round-trip por archivo en el cleanup.** `PendingFileCleanupBackgroundService` hace `provider.ExistsAsync` y luego `provider.DeleteAsync` por cada archivo expirado; `DeleteAsync` ya usa `DeleteIfExistsAsync` internamente → el `ExistsAsync` previo es un round-trip de blob redundante. Bajo impacto (background, batched, default 100/30min). | `PendingFileCleanupBackgroundService.cs:~64-68`; `AzureBlobStorageProvider.cs` (`DeleteAsync`=DeleteIfExists) | Eliminar el `ExistsAsync` previo y confiar en `DeleteAsync`/`DeleteIfExistsAsync` (1 round-trip por archivo). <br>**✅ Resuelto (PR-B · 2026-06-06):** `IFileStorageProvider.DeleteAsync` ahora devuelve `bool` (semántica delete-if-exists: el `DeleteIfExistsAsync` de Azure ya retorna si existía); el cleanup elimina el `ExistsAsync` previo y usa el bool para `deletedFromStorageCount` (1 round-trip/archivo, telemetría precisa preservada). Actualizados los 4 implementadores (`AzureBlobStorageProvider` + 3 test doubles) y el otro llamador (`ReportExportJobProcessor`, descarta el bool). Build 0/0, unit 1621/0, integración ReportExportJobs 2/2. |

---

## 4. Descartado / ya cumple (verificado — no son hallazgos)

| Tema | Resolución |
|---|---|
| ➖ IDOR cross-tenant | **Bloqueado:** `StoredFile : TenantEntity`; `GetByPublicIdAsync` no usa `IgnoreQueryFilters` → filtro global EF acota al tenant. La fuga de FILE-1 es intra-tenant. |
| ➖ Seguridad de las SAS | Acotadas al blob exacto (`Resource=b`, BlobName=objectKey); upload = Create+Write, read = Read; expiración corta configurable; firmadas (user-delegation key en prod, shared key local). Sin token a nivel contenedor. |
| ➖ Confianza del `complete` en metadata del cliente | `complete` verifica el blob server-side (`ExistsAsync` + `GetObjectInfoAsync`) y `MarkActive` usa el tamaño/content-type **del servidor**, no los declarados; además gate de ownership. |
| ➖ Path traversal en object key | `FileObjectKeyBuilder` arma la ruta con GUIDs server-side (`tenants/{tenant}/users/{user}/{purpose}/{fileId}{ext}`); la extensión se sanitiza (`..`,`/`,`\`); el filename del cliente no entra en la key. |
| ➖ Validación de contenido | Content-type/extensión por whitelist por `purpose` (422) + tamaño máximo (413), pre-upload. |
| ➖ `complete` con token en body / PATCH semántico | **Diferido (decisión previa documentada)** — no re-flag. Inconsistente con `If-Match`/RFC-6902 canónico, pero intencional para el flujo de upload. |
| ➖ Lifetime del cliente blob / delegation key | `BlobServiceClientFactory` singleton; `BlobServiceClient` cacheado; user-delegation key cacheada 1h con refresh anticipado + `SemaphoreSlim`. Buenas prácticas. |
| ➖ Índices | `(Provider,ObjectKey)` único, `(CreatedByUserId,Purpose)`, `(Status,CreatedUtc)` (cleanup), `(TenantId,PublicId)` único. Cobertura completa de los lookups. |
| ✅ Cumple | Cleanup batched + configurable + `IgnoreQueryFilters` intencional (global), `AsNoTracking` correcto (tracking sólo en mutaciones), handlers limpios sin god-file ni código muerto, SAS gen local/cacheada. |

---

## 5. Plan de PRs sugerido

| PR | Hallazgos | Tema |
|---|---|---|
| **PR-A** ✅ | FILE-1 | Seguridad: read-authz **delegado al dominio** + endpoint genérico owner-only. **Hecho 2026-06-06** (decisión: delegado al dominio). |
| **PR-B** ✅ | FILE-3 + FILE-4 + FILE-5 | Alineación/contrato + perf: `[Tags]`/`[SwaggerOperation]` + enrolar guardrail OpenAPI; declarar 409 en `complete`; `DeleteAsync→bool` + quitar `ExistsAsync` del cleanup. **Hecho 2026-06-06.** |
| **Observación** ➖ | FILE-2 | Autorizar `EntityId` si/cuando un consumidor lea por `StoredFile.EntityId`. Riesgo aceptado (latente). |
| Diferido | FILE-3 (sub) | `[ApiVersion]`+route-template + `[ProducesStandardErrors]` → plan de alineación canónica. |

---

## 6. Bitácora

| Fecha | Cambio |
|---|---|
| 2026-06-06 | **PR-B (FILE-3 + FILE-4 + FILE-5) ✅ resuelto — auditoría CERRADA para acción.** **FILE-3:** `[Tags("Files")]` + `[SwaggerOperation]` en los 4 endpoints + familia `Files` enrolada en el guardrail OpenAPI (decisión = alinear contrato; `[ApiVersion]`/route-template + `[ProducesStandardErrors]` diferidos al plan canónico). **FILE-4:** `409` declarado en `complete`; `503` descartado (error `StorageProviderNotConfigured` definido pero nunca usado). **FILE-5:** `IFileStorageProvider.DeleteAsync→Task<bool>` (delete-if-exists) + cleanup sin el `ExistsAsync` redundante (1 round-trip/archivo, contador preciso). **FILE-2 ➖** observación (sin ruta de explotación). Build 0/0, unit 1621/0 (+3 casos del guardrail por la familia Files), integración ReportExportJobs 2/2. |
| 2026-06-06 | **PR-A (FILE-1) ✅ resuelto** (decisión de producto: **read-authz delegado al dominio**). Cerrado el IDOR intra-tenant de lectura: (1) `GET /files/{id}/read-url` ahora es owner-only (`CreatedByUserId==caller`→403 `FileOwnershipMismatch`, espejo de complete/delete) +403 declarado; (2) nuevo `GET /personnel-files/{id}/documents/{docId}/read-url` autorizado por el expediente (mismo gate que el GET de documento) que mintea la SAS server-side; (3) prosa Swagger de documentos repuntada al nuevo endpoint. Fotos de perfil ya server-side (sin cambio). +2 unit `FileAccessControlTests` (owner→200 / no-owner→403, red→green) + smoke DI. Build 0/0, unit 1618/0. Quedan FILE-2/3/4/5 (P3). |
| 2026-06-06 | Auditoría inicial (3 agentes Explore: seguridad/perf/arquitectura + verificación adversarial). **Hallazgo clave (P1):** `read-url` no autoriza (IDOR intra-tenant de lectura), asimétrico con `complete`/`delete` que gatean por `CreatedByUserId`. Verificaciones que ajustaron severidad: cross-tenant **bloqueado** (filtro global EF) → FILE-1 es intra-tenant; `EntityId` sin authz es **latente** (nada lee por `StoredFile.EntityId` hoy) → FILE-2 degradado a P3; el token-en-body/PATCH semántico es **diferido conocido** (no re-flag). 5 hallazgos accionables (1 P1, 0 P2, 4 P3); 9 temas descartados (by-design / verificados). Todos ⬜ pendientes. |
