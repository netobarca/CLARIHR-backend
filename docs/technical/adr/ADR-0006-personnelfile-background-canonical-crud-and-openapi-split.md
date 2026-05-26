# ADR-0006 — Canonicalización CRUD+PATCH de PersonnelFileBackground (concurrencia por sub-ítem) y separación del contrato OpenAPI por API

- **Estado:** Aprobado
- **Fecha:** 2026-05-26
- **Autores:** Equipo Backend (asistido por agente)
- **Relacionado con:** Remediación de controlador `PersonnelFileBackgroundController` (AGENTS.md §17) — alineación con el patrón sub-recurso canónico de JobProfiles
- **Reemplaza:** No aplica
- **Reemplazado por:** No aplica

---

## 1. Título

Los cinco sub-recursos de antecedentes del expediente de personal (educaciones, idiomas,
capacitaciones, empleos previos, referencias) adoptan el patrón sub-recurso canónico
(GET lista / GET by id / POST 201 / PUT / PATCH JSON Patch / DELETE) con **token de
concurrencia propio por sub-ítem** vía header `If-Match`; y el contrato OpenAPI
mantenido a mano se separa en **un archivo por API** (core y backoffice).

---

## 2. Contexto

### Contexto resumido
`PersonnelFileBackgroundController` usaba el patrón viejo (rutas `api/v1` literales,
concurrencyToken en el body, POST con respuestas 200/201 inconsistentes, sin PATCH, sin
GET-by-id, sin `[AuthorizationPolicySet]`/`[SwaggerOperation]`/`ProducesStandardErrors`).
El token de concurrencia era el del **agregado** `PersonnelFile`, no granular por sub-ítem.

### Situación actual
El dominio JobProfiles ya define el patrón sub-recurso canónico (p.ej.
`JobProfileFunctionsController` + `JobProfileFunctionAdministration`), y el shell
`PersonnelFilesController` ya fue remediado. Faltaba alinear los sub-recursos de
PersonnelFiles. Además, `docs/technical/api/openapi.yaml` contenía **dos documentos
OpenAPI concatenados sin separador `---`** (claves `paths:`/`components:` duplicadas), por
lo que un parser estricto solo veía el segundo documento y el contrato no era válido.

### Motivadores
- Consistencia con el patrón sub-recurso canónico y con el shell ya remediado.
- Concurrencia optimista **granular** por sub-ítem (evita falsos conflictos al editar ítems distintos del mismo expediente).
- Que el contrato OpenAPI versionado parsee como documento válido y sea administrable por API.

---

## 3. Decisión

### Decisión adoptada
**A. Canonicalización de los 5 sub-recursos de antecedentes.** Cada sub-entidad
(`PersonnelFileEducation`, `PersonnelFileLanguage`, `PersonnelFileTraining`,
`PersonnelFilePreviousEmployment`, `PersonnelFileReference`) recibe un `ConcurrencyToken`
propio (columna `concurrency_token`, `.IsConcurrencyToken()`), inicializado en el
constructor y rotado en cada `Update`. La API expone por recurso: `GET` (lista completa),
`GET {id}`, `POST` (201 + `Location` + `ETag`), `PUT`, `PATCH` (RFC 6902,
`application/json-patch+json`) y `DELETE`. `PUT`/`PATCH`/`DELETE` toman el token por header
`If-Match`; `DELETE` es borrado físico y devuelve el token refrescado del expediente padre
(`PersonnelFileParentConcurrencyResult`, 200) para evitar un re-fetch. El id del response se
expone como `{Entidad}PublicId` + `concurrencyToken`.

**B. Separación del contrato OpenAPI por API.** `docs/technical/api/openapi.yaml` queda como
contrato **Core** (`/api/v1`, `/api/account`, `/api/audit`, `/api/auth`) y se crea
`docs/technical/api/openapi-backoffice.yaml` para el contrato **Backoffice/Platform**
(`/api/platform/*`). Cada archivo es un documento OpenAPI 3.0 autocontenido (un `paths:` y un
`components:`), con sus `components.schemas` repartidos por cierre transitivo de `$ref`.

### Alcance de la decisión
- [x] Un módulo específico (PersonnelFiles — sub-recursos de antecedentes).
- [x] Una integración específica (contrato OpenAPI mantenido a mano).

### Reglas derivadas
- Los sub-recursos de colección de PersonnelFiles siguen el patrón canónico; la concurrencia
  se valida contra el token **del sub-ítem** (no del agregado) vía `If-Match`.
- Consistente con [ADR-0003]: el `*PatchState` **no** transporta el token de concurrencia; la
  concurrencia vive solo en el command/If-Match contra la entidad.
- El contrato OpenAPI versionado se mantiene como **un archivo por API**; cada archivo debe
  parsear como un único documento válido y sin `$ref` colgantes.

---

## 4. Alternativas evaluadas

### Alternativa 1
**Nombre:** Token de concurrencia del agregado (statu quo)

**Descripción:** Seguir validando concurrencia contra `PersonnelFile.ConcurrencyToken`.

**Ventajas:** Cero cambio de esquema.

**Desventajas:** Falsos conflictos al editar ítems distintos del mismo expediente; diverge del patrón JobProfiles.

**Razón de descarte:** No cumple el requisito de administración granular por entidad.

### Alternativa 2
**Nombre:** Patrón sub-recurso canónico con token por sub-ítem *(elegida)*

**Descripción:** Espejar el patrón de JobProfiles en los 5 sub-recursos.

**Ventajas:** Consistencia, concurrencia granular, contrato uniforme, guardrail drift-proof.

**Desventajas:** Cambio de esquema (migración) + cambio de contrato (no compatible hacia atrás).

**Razón de aceptación:** Cumple los requisitos y unifica el dominio.

### Alternativa 3 (para el contrato OpenAPI)
**Nombre:** Fusionar en un solo documento vs. separar por API

**Descripción:** Unir los dos bloques en un `openapi.yaml` único, o separarlos por API.

**Ventajas (separar):** "Un archivo por API", administrable por separado, refleja los dos proyectos (CLARIHR.Api / CLARIHR.Backoffice.Api).

**Desventajas (separar):** Dos archivos a mantener.

**Razón de aceptación:** Decisión explícita del equipo: un archivo por API.

---

## 5. Justificación

### Razones principales
- Alinea PersonnelFiles con el patrón sub-recurso canónico ya probado en JobProfiles.
- La concurrencia por sub-ítem evita falsos 409 y habilita edición concurrente de ítems distintos.
- Separar el contrato por API lo vuelve válido y administrable por separado.

### Factores considerados
- [x] Simplicidad
- [x] Mantenibilidad
- [x] Seguridad
- [x] Compatibilidad con arquitectura actual
- [x] Testing
- [x] Documentación / contrato

### Resumen de justificación
Extensión proporcional de un patrón ya establecido (no novel), con migración aditiva y guardrail
drift-proof, más la corrección estructural del contrato OpenAPI para que sea válido por API.

---

## 6. Consecuencias

### Consecuencias positivas
- Contrato uniforme y documentado de los 5 sub-recursos; concurrencia granular; PATCH parcial.
- `ConcurrencyTokenMappingGuardrailsTests` cubre automáticamente los 5 nuevos tokens.
- `OpenApiContractGuardrailsTests` inscribe `^PersonnelFileBackground` (Tags + SwaggerOperation).
- Los dos contratos OpenAPI ahora parsean como documentos válidos (0 `$ref` colgantes).

### Consecuencias negativas o trade-offs
- **Cambio de contrato no compatible hacia atrás:** concurrencia por header `If-Match` (no body),
  POST→201, nuevo PATCH, response `id`→`{Entidad}PublicId` + `concurrencyToken`, param de ruta
  `itemPublicId`→`{entidad}PublicId` (las URLs base no cambian). El frontend debe leer
  `concurrencyToken` del GET/response y enviarlo en `If-Match`.
- Dos archivos OpenAPI a mantener en vez de uno.

### Riesgos
- Clientes que aún manden el token en el body fallarán → mitigado documentando el cambio (esta ADR + endpoint-reference + openapi).

### Impacto técnico
- Migración `AddConcurrencyTokenToPersonnelFileBackgroundEntities` (columna `concurrency_token` en
  5 tablas, backfill `gen_random_uuid()` por fila).
- Capa Application: GetById + PATCH (state/applier) por sub-recurso; Update/Delete validan el token del sub-ítem.

### Impacto operativo o documental
- Esta ADR; `endpoint-reference.md` y los dos `openapi*.yaml` actualizados; AGENTS.md §15 (árbol de docs) y §17.6.

---

## 7. Impacto por capa o área

### Domain
`PersonnelFile.cs`: `ConcurrencyToken` en las 5 sub-entidades (init + rotación en `Update`).

### Application
`PersonnelFileAdministration.cs`: por sub-recurso — GetById query/handler, Patch command/handler/state/applier, Update/Delete con token del sub-ítem y `PersonnelFileParentConcurrencyResult` en Delete. Reusa `PersonnelFilePatchValueException`.

### Infrastructure
`PersonnelFileConfiguration.cs`: `concurrency_token` + `.IsConcurrencyToken()` ×5. `PersonnelFileRepository.cs`: token en proyecciones + `GetXAsync`. Migración nueva.

### API
`PersonnelFileBackgroundController.cs`: atributos de clase canónicos + 30 acciones (6×5) con `[FromIfMatch]`, JSON Patch, `ProducesStandardErrors`, `[SwaggerOperation]`. Contratos de request ajustados.

### Data / SQL
Columna `concurrency_token uuid not null default gen_random_uuid()` en las 5 tablas de antecedentes.

### Security
Sin cambio de superficie de autz (marker `[AuthorizationPolicySet(PersonnelFilePolicies.Read, Manage)]`; `GovernedFamilyRegex` **no** se extiende, consistente con el shell). Concurrencia optimista granular reduce sobre-escritura entre ítems.

### Performance
GET de listas devuelve la colección completa (sin paginar, por requisito); proyecciones `AsNoTracking`.

### Testing
~63 tests nuevos de patch appliers (unit) + fix del test de integración. Build 0/0; 814 unit; 260 integración (0 fallos, 27 skips documentados).

### Documentation
Esta ADR; `endpoint-reference.md`; `openapi.yaml` (core) + `openapi-backoffice.yaml` (backoffice).

---

## 8. Plan de implementación

### Cambios requeridos
- Dominio + EF + migración (5 tokens) → Application (GetById/PATCH/Update/Delete) → controlador → guardrail → tests.
- Split del contrato OpenAPI por API con cierre de `$ref`.

### Dependencias
- Migración aplicada a la BD (manual, por el equipo).

### Orden sugerido
1. Migración + dominio/EF. 2. Application + controlador. 3. Guardrail + tests. 4. Docs/contrato.

### Validaciones requeridas
- `dotnet build CLARIHR.slnx` 0/0; `CLARIHR.Application.UnitTests` y `CLARIHR.Api.IntegrationTests` en verde; sanity rojo→verde del guardrail de concurrency y del de OpenAPI.

---

## 9. Impacto en documentación

### Documentos a actualizar
- Esta ADR (nueva).
- `docs/technical/api/endpoint-reference.md` (endpoints canónicos).
- `docs/technical/api/openapi.yaml` (core) + `docs/technical/api/openapi-backoffice.yaml` (backoffice, nuevo).
- `AGENTS.md` §15 (árbol de docs) y §17.6 (test ya no es known-failure).

### Observación
Complementa el patrón sub-recurso canónico; no revierte reglas vigentes. Consistente con ADR-0003.

---

## 10. Impacto en historias de usuario o roadmap

### HUs impactadas
- Remediación del controlador `PersonnelFileBackground`.

### Iniciativas impactadas
- Canonicalización incremental de los controladores sub-recurso de PersonnelFiles.

### Requerimientos futuros habilitados
- Aplicar el mismo patrón a los demás controladores sub-recurso de PersonnelFiles (Employment, PersonalInfo, Compensation, Talent, Interests). Al hacerlo, ampliar el regex de la familia OpenAPI de `^PersonnelFileBackground` a `^PersonnelFile`.

---

## 11. Criterios de aceptación de la decisión

### Se considerará aplicada correctamente cuando:
- Los 5 sub-recursos exponen GET/GET-by-id/POST(201)/PUT/PATCH/DELETE con `If-Match` y `concurrencyToken` por sub-ítem.
- `ConcurrencyTokenMappingGuardrailsTests` y `OpenApiContractGuardrailsTests` (familia `PersonnelFileBackground`) en verde.
- `openapi.yaml` y `openapi-backoffice.yaml` parsean como documento único válido, 0 `$ref` colgantes.

### Evidencias esperadas
- Build 0/0; 814 unit tests; 260 tests de integración (0 fallos).
- Sanity rojo→verde de los guardrails de concurrency y OpenAPI.

---

## 12. Estado de seguimiento

### Estado actual
Adoptada

### Próxima revisión
Al canonicalizar el siguiente controlador sub-recurso de PersonnelFiles.

### Responsable de seguimiento
Equipo Backend

---

## 13. Notas adicionales

- La concurrencia por sub-ítem es consistente con [ADR-0003]: el patch state no porta el token; la concurrencia vive en el command/If-Match.
- El backfill de la migración usa `gen_random_uuid()` por fila (no `Guid.Empty`) para que las filas preexistentes no fallen el validador `NotEmpty()` del token.
- El split del contrato además corrigió 5 `$ref` colgantes preexistentes (`ConcurrencyRequest`, `CompanyStatus`, `PersonnelFamilyMemberSex`, `PersonnelFileSortDirection`, `PersonnelCatalogItemResponse`) definiéndolos desde el código.

---

## 14. Referencias

- Código: `src/CLARIHR.Api/Controllers/PersonnelFileBackgroundController.cs`, `src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs`, `src/CLARIHR.Domain/PersonnelFiles/PersonnelFile.cs`, `src/CLARIHR.Infrastructure/Persistence/Configurations/PersonnelFiles/PersonnelFileConfiguration.cs`
- Migración: `src/CLARIHR.Infrastructure/Persistence/Migrations/20260526181139_AddConcurrencyTokenToPersonnelFileBackgroundEntities.cs`
- Contrato: `docs/technical/api/openapi.yaml`, `docs/technical/api/openapi-backoffice.yaml`, `docs/technical/api/endpoint-reference.md`
- ADR relacionadas: `docs/technical/adr/ADR-0003-patchstate-no-concurrency-token.md`
- Reglas: `AGENTS.md §17` (remediación de controladores), `§15` (ubicaciones de docs)
