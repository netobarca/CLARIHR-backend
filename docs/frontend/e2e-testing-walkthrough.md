# CLARIHR — Guía de prueba E2E de TODA la API (paso a paso)

> **Objetivo:** un runbook ejecutable para **probar el sistema completo de punta a punta**, módulo por
> módulo, creando **cada prerrequisito en el orden correcto** para que ningún paso falle por una
> dependencia faltante. Es la versión "accionable" del orden de integración.
>
> - **Orden y dependencias:** [INTEGRATION-ORDER.md](./INTEGRATION-ORDER.md) (mapa por bloques).
> - **Detalle por módulo:** cada doc en `docs/frontend/<módulo>/` (contrato completo).
> - **Job Profiles** tiene su propio walkthrough profundo:
>   [job-profiles/e2e-testing-walkthrough.md](./job-profiles/e2e-testing-walkthrough.md).
>
> **Procedencia / exactitud:** compilado desde los docs de `docs/frontend/` (contrato FE, verificado
> contra código) + verificación directa de rutas/DTOs en código (Bloques 3–5 y Job Profiles).
> Los bodies de abajo son el **camino mínimo** (solo lo necesario para avanzar); el contrato completo
> (campos opcionales, validaciones, errores) vive en el doc de cada módulo. **La verdad en runtime es
> Swagger UI (`/swagger`)** — úsalo para correr cada paso y ver el esquema exacto.

---

## Cómo usar esta guía

- **Herramienta:** lo más simple es **Swagger UI** autenticado con el Bearer; o Postman/Insomnia/`.http`.
- **Base URL:** `https://<host>/api/v1` (en los ejemplos se omite `/api/v1`).
- **Auth:** `Authorization: Bearer <accessToken>` en TODAS las requests (salvo login/refresh).
- **Compañía activa:** tras `switch`, usa el **nuevo** token; el `{companyId}` de las rutas debe ser la
  compañía activa del token (cross-tenant → `404`/`403`).
- **Concurrencia (`If-Match`):** `PUT`/`PATCH`/`DELETE` requieren `If-Match: "<concurrencyToken>"` (con
  comillas). `POST` no lo lleva. Token viejo → `409 CONCURRENCY_CONFLICT`; faltante → `400`.
- **`PATCH` = JSON Patch:** `Content-Type: application/json-patch+json`, body = **array desnudo**
  `[ { "op":"replace", "path":"/campo", "value":... } ]`.
- **Regla de oro:** cada mutación devuelve un `concurrencyToken` nuevo (body + header `ETag`). Guarda el
  último y úsalo en la siguiente mutación **del mismo recurso**.
- **📌 Captura:** en cada paso, anota el `publicId`/token que necesitarás después.
- **Errores:** la lógica del FE va siempre por el `code` del ProblemDetails (RFC 7807), nunca por el mensaje.

### El grafo de dependencias (resumen)

```
auth → account/switch/access-context ──┬─→ subscription (HABILITA módulos)
                                       ├─→ iam · company-users · preferences
                                       └─→ general-catalogs ─→ organization
                                            (cc-types→cost-centers; unit-types→org-units;
                                             hierarchy→levels→groups; wc-types→work-centers)
                                                  │
        position-description-catalogs ─┐          │
        salary-tabulator ──────────────┴─→ job-profiles ─┬─→ competency-framework
                                                         └─→ position-slots
                          files ─→ personnel-files (shell → finalize → empleado/documents)
        legal-representatives · audit · report-export-jobs   (transversales)
```

> 🔑 **La trampa que más cuesta:** un módulo de negocio **no se puede probar** si el plan del tenant no lo
> habilita (`effectiveModules`), aunque seas **Owner** con el permiso. Por eso el **Bloque 1
> (Subscription)** va temprano: habilita ahí lo que vas a probar después.

---

## Bloque 0 — Columna vertebral (sesión + compañía)

### 0.1 Auth — `api/v1/auth`
```
POST /auth/login                      { "email": "...", "password": "..." }     → 📌 accessToken, refreshToken
POST /auth/refresh                    { "refreshToken": "..." }                 → 📌 nuevo par (reemplaza ambos)
POST /auth/logout                     (Bearer)                                  → 204
```
✅ Login `200` con JWT. ⚠️ **Refresh single-flight**: dos refresh en paralelo con el mismo token → reuse
detection revoca la sesión (serializa con mutex). ⚠️ Registro/verificación de email es **stub de logging**
(no llega correo real).

### 0.2 System — `api/v1/system`
```
GET /system/status      → { isAuthenticated, tenantPublicId, userPublicId, utcNow }
```
✅ Smoke de liveness. ⚠️ **Nunca da 401**: token vencido → `200 isAuthenticated:false` (distingue "backend
caído" de "sesión muerta").

### 0.3 Account Companies — `api/v1/account/companies`
```
GET  /account/companies/countries                                   → 📌 country code (ej. "HN")
GET  /account/companies/company-types?countryCode=HN                → 📌 companyTypePublicId
POST /account/companies                                             → 📌 company publicId
     { "name":"Mi Empresa S.A.", "countryCode":"HN",
       "companyTypePublicId":"{..}",
       "initialLegalRepresentative": { "firstName":"Ana","lastName":"Garcia",
         "documentType":"DNI","documentNumber":"0801-1990-12345",
         "positionTitle":"Gerente General","representationType":"PrimaryLegalRepresentative",
         "effectiveFromUtc":"2026-06-10T00:00:00Z" } }
POST /account/companies/{id}/switch    (body vacío)                 → 📌 NUEVO accessToken/refreshToken + accessContext
GET  /account/companies/{id}/access-context                        → 📌 effectiveModules[], currentUserAccess.permissions[], rol "Owner"
```
✅ **Punto de control del Bloque 0:** login → crear compañía → `switch` → leer `access-context` con
`effectiveModules` + `permissions`. Ya sos **Owner** (permisos completos). ⚠️ No se puede archivar/cambiar
la compañía activa sin salir de ella primero; `If-Match` en todas las mutaciones de compañía.

---

## Bloque 1 — Administración del tenant (gating + cuenta)

### 1.1 Subscription — `api/v1/account/companies/{companyId}/subscription`  ⚠️ habilita módulos
```
GET  /account/companies/{id}/subscription                 → 📌 concurrencyToken, effectiveModules[]
GET  /account/companies/{id}/subscription/plans           → 📌 commercialPlanPublicId destino
POST /account/companies/{id}/subscription/preview         { "commercialPlanPublicId":"{..}" }  → isEligible, addedModuleKeys[]
PUT  /account/companies/{id}/subscription   If-Match      { "commercialPlanPublicId":"{..}", "observations":"Habilitar negocio" }
```
✅ `effectiveModules` cambia según el preview. ⚠️ Owner-only. Tras aplicar, el `access-context` se
invalida → re-léelo.

### 1.2 Preferences
```
GET /account/me/preferences                               → 📌 token, language
PUT /account/me/preferences   If-Match                    { "language":"es" }
GET /companies/{id}/preferences                           → 📌 currencyCode, timeZone   (perm CompanyPreferences.Read)
PUT /companies/{id}/preferences   If-Match                { "currencyCode":"HNL", "timeZone":"America/Tegucigalpa" }
```
✅ La preferencia persiste al re-leer. ⚠️ `currencyCode` se guarda en MAYÚSCULAS; `403 TENANT_MISMATCH` si
el `{id}` ≠ tenant del token.

### 1.3 IAM / Authorization — `api/v1/account/companies/{companyId}/authorization`
```
GET  .../authorization/role-builder-catalog              → 📌 permissionPublicIds (ignora isDormant:true)
POST .../authorization/roles                             { "name":"Supervisor","description":"...","permissionPublicIds":[".."] }  → 📌 rolePublicId, token
PUT  .../authorization/roles/{roleId}/grants   If-Match  { "permissionPublicIds":[".."] }
PUT  .../authorization/users/{userId}/roles    If-Match  { "rolePublicIds":[".."] }
```
✅ El rol queda con sus grants (re-léelo). ⚠️ Dos modelos de concurrencia: **token fuerte (GUID)** en roles,
**ETag débil** en user-roles. ⚠️ Requiere migración `AddConcurrencyTokenToIamRoles`. Invariante "último
admin": no podés quitar todos los grants de admin.

### 1.4 Company Users — `api/v1/company/users`  (tenant implícito)
```
POST  /company/users                          { "email":"..","firstName":"..","lastName":"..","rolePublicIds":[".."] }  → 📌 user.publicId, ETag(W/"..")
GET   /company/users/{id}                     → 📌 ETag débil para editar
PUT   /company/users/{id}      If-Match: W/".."   { "firstName":"..","lastName":"..","rolePublicIds":[".."] }
PATCH /company/users/{id}/deactivate   If-Match
```
✅ El usuario aparece en la lista con su rol. ⚠️ **Email es stub** → invitar→aceptar **no es e2e** sin
proveedor real (coordina el token con backend). ⚠️ `rolePublicIds` es **reemplazo total**.

---

## Bloque 2 — Datos de referencia (read-only)

### 2.1 General Catalogs — `api/v1/companies/{companyId}/...`
```
GET /companies/{id}/general-catalogs/{key}     keys: countries, currencies, banks, languages, language-levels,
                                                     training-types, duration-units, reference-types, education-*,
                                                     file-document-types
GET /companies/{id}/reference-catalogs/{key}   keys: professions, marital-statuses, identification-types,
                                                     kinships, departments, municipalities?parentCode={dept}
```
✅ Los dropdowns base resuelven. ⚠️ **Guardá el `code`, no el `publicId`** — es el valor que esperan los
validadores de Personnel Files. Municipalities se filtran por `parentCode` (código del departamento).

---

## Bloque 3 — Estructura organizativa (orden por FK)  · *verificado en código*

### 3.1 Cost Center Types → Cost Centers — `api/v1/companies/{companyId}/...`
```
POST /companies/{id}/cost-center-types     { "code":"SALARY-EXP","name":"Gasto salarial" }   → 📌 publicId
POST /companies/{id}/cost-centers          { "code":"CC-001","name":"Nomina Admin","costCenterTypePublicId":"{..}" }  → 📌 code
```
⚠️ Las Org Units referencian el centro de costo por **`code`** (no por id). No se inactiva un tipo/centro en uso (`409 ..._IN_USE`).

### 3.2 Org Structure Catalogs → Org Units
```
POST /companies/{id}/organization-structure-catalogs/unit-types        { "code":"DIR","name":"Direccion","sortOrder":10 }   → 📌 orgUnitTypePublicId
POST /companies/{id}/organization-structure-catalogs/functional-areas  { "code":"FIN","name":"Finanzas","sortOrder":1 }      → 📌 (opcional) functionalAreaPublicId
POST /companies/{id}/organization-units                                → 📌 orgUnitPublicId
     { "code":"DIR-FIN","name":"Direccion de Finanzas","orgUnitTypePublicId":"{..}",
       "functionalAreaPublicId":null, "parentPublicId":null, "sortOrder":1, "costCenterCode":null }
```
⚠️ Reparentar es vía `PATCH .../move` (no `PUT`); no se inactiva una unidad con hijos activos
(`409 ORG_UNIT_HAS_ACTIVE_CHILDREN`).

### 3.3 Locations: Hierarchy (singleton) → Levels → Groups → Work Centers
```
GET  /companies/{id}/location-hierarchy                  → ya existe (singleton); 📌 defaultGroupCode
POST /companies/{id}/location-levels    { "levelOrder":1,"displayName":"Pais","isActive":true,"isRequired":true,"allowsWorkCenters":false }
                                        (repite niveles; SOLO el último activo puede allowsWorkCenters:true)
POST /companies/{id}/location-groups    { "levelOrder":1,"code":"HN","name":"Honduras","parentPublicId":null }  → 📌 publicId (padre del siguiente)
POST /companies/{id}/work-center-types  { "code":"OFFICE","name":"Oficina","requiresAddress":true,"requiresGeo":false,"allowsBiometric":false }  → 📌 workCenterTypePublicId
POST /companies/{id}/work-centers       { "code":"WC-01","name":"Sede","workCenterTypePublicId":"{..}","locationGroupPublicId":"{grupo último nivel}","address":"..." }  → 📌 workCenterPublicId
```
✅ **Punto de control Bloque 3:** `/organization-units/tree` y `/location-groups/tree` navegables. Esto
desbloquea Job Profiles y Personnel Files. ⚠️ El grupo "General" por defecto está protegido; un grupo nivel
N exige padre activo nivel N-1; los flags del work-center-type fuerzan `422` si faltan address/geo.

---

## Bloque 4 — Catálogos de descriptor + tabulador  · *verificado en código*

### 4.1 Position Description Catalogs — `api/v1/companies/{companyId}/...`
```
POST /companies/{id}/position-description-catalogs/{slug}/items   { "code":"..","name":"..","sortOrder":10 }
     slugs: position-function-types, position-contract-types, strategic-objectives, frequencies,
            requirement-types, general-functions, salary-classes, work-equipments, responsibilities-catalog
POST /companies/{id}/position-category-classifications
     { "code":"CLASS-OPE","name":"..","positionFunctionTypePublicId":"{..}","positionContractTypePublicId":"{..}","orgUnitTypePublicId":"{..}","sortOrder":10 }  → 📌 classificationPublicId
POST /companies/{id}/position-categories
     { "code":"CAT-ANALISTA","name":"Analista","classificationPublicId":"{..}","sortOrder":10 }   → 📌 positionCategoryPublicId
```
⚠️ La terna (función, contrato, tipo-unidad) de la clasificación es **única**.

### 4.2 Salary Tabulator (maker-checker) — para `compensations` de Job Profile
```
POST  /companies/{id}/salary-tabulator/change-requests
      { "effectiveFromUtc":"2026-07-01T00:00:00Z","effectiveToUtc":null,
        "items":[ { "salaryClassPublicId":"{salary-class de 4.1}","salaryScaleCode":"E1","currencyCode":"HNL",
                    "changeType":"Create","proposedBaseAmount":26000,"proposedMinAmount":23000,"proposedMaxAmount":31000 } ] }   → 📌 requestId, token
PATCH /salary-tabulator/change-requests/{id}/submit    If-Match
PATCH /salary-tabulator/change-requests/{id}/approve   If-Match   { "decisionComment":"OK" }
GET   /salary-tabulator/lines/{lineId}                 → 📌 salaryTabulatorLinePublicId
```
⚠️ **Maker ≠ checker**: quien crea no puede aprobar (permisos `SalaryTabulator.Request` vs `.Approve`) →
`403 ...APPROVAL_POLICY_VIOLATION`. `If-Match` en cada transición.

---

## Bloque 5 — Perfiles de puesto + competencias

### 5.1 Job Profiles  →  **ver el walkthrough dedicado**
👉 [job-profiles/e2e-testing-walkthrough.md](./job-profiles/e2e-testing-walkthrough.md) cubre: crear shell
(Draft) → 9 sub‑recursos → **publicar** → **editar publicado** (categoría) → archivar → negativos.
Resumen del estado: `POST .../job-profiles` (Draft) → `+función` + `+requisito` → `PATCH status=Published`
(requiere objective + responsibilities + ≥1 function + ≥1 requirement, si no `422`).
📌 Captura `jobProfilePublicId` (lo usan competency-matrix y position-slots).

### 5.2 Competency Framework — `api/v1/companies/{companyId}/...`  ⚠️ cambio 2026-06-18 (CRUD por ítem)
```
POST /companies/{id}/occupational-pyramid-levels   { "code":"OPER","name":"Operativo","levelOrder":1 }   → 📌 levelPublicId
POST /companies/{id}/competency-conducts            { "competencyPublicId":"{job-catalog Competency}","competencyTypePublicId":"{..}","behaviorLevelPublicId":"{..}","description":"..","sortOrder":1 }  → 📌 conductPublicId
POST /job-profiles/{profileId}/competency-matrix/items
     { "occupationalPyramidLevelPublicId":"{..}","conductPublicIds":["{..}"],"expectedEvidence":"..","sortOrder":1 }   → 📌 itemPublicId + token POR ÍTEM
```
⚠️ El antiguo `PUT /competency-matrix` con `items[]` **ya no existe** (CRUD por ítem). Cada ítem usa **su
propio** `concurrencyToken` en `If-Match`. Los `conductPublicIds` (1..50) deben compartir la misma terna
competencia/tipo/nivel; competencia/tipo/nivel se **derivan** de los conducts (no se envían). Máx 200 ítems/perfil.

---

## Bloque 6 — Posiciones ocupables

### 6.1 Position Slots — `api/v1/...`  (requiere un Job Profile)
```
POST  /companies/{id}/position-slots    { "code":"POS-001","title":"Analista","jobProfilePublicId":"{..}","status":"Vacant","maxEmployees":2,"occupiedEmployees":0,"effectiveFromUtc":"2026-06-10T00:00:00Z" }  → 📌 publicId
PATCH /position-slots/{id}/status      If-Match   { "status":"Occupied" }
PATCH /position-slots/{id}/occupancy   If-Match   { "occupiedEmployees":1 }
```
✅ Hereda org unit/categoría/contrato/cost center del perfil (no los repitas). ⚠️ `occupied > max` →
`422 ...CAPACITY_RULE_VIOLATION`; transiciones de estado inválidas → `409 ...STATUS_CONFLICT`. Si el perfil
no tiene org unit → `422 ...JOB_PROFILE_ORG_UNIT_NOT_CONFIGURED`.

---

## Bloque 7 — Archivos + Expedientes de personal (el integrador)

### 7.1 Files — `api/v1/files`  (flujo de subida en 3 pasos; feeder de PF documents)
```
POST  /files/upload-session    { "fileName":"doc.pdf","contentType":"application/pdf","sizeBytes":102400,"purpose":"PersonnelDocument" }  → 📌 filePublicId, uploadUrl, token
PUT   <uploadUrl>              (binario directo a storage, con los requiredHeaders devueltos)
PATCH /files/{filePublicId}/complete    { "concurrencyToken":"{token}" }   (⚠️ token en el BODY, no en If-Match)
GET   /files/{filePublicId}/read-url    → 📌 readUrl (SAS de lectura)
```
⚠️ Sin RBAC: control por **propietario** (solo quien sube completa/lee/borra). `purpose`:
`ProfileImage|PersonnelDocument|ReportExport|CompanyLogo|Attachment`.

### 7.2 Personnel Files — `api/v1/...`  ⚠️ COMPUERTA DE ESTADO: `Draft → finalize → Completed`
```
POST  /companies/{id}/personnel-files   { "recordType":"Employee","firstName":"..","lastName":"..","institutionalEmail":"..","orgUnitPublicId":"{..}","assignedPositionSlotPublicId":"{..}" }  → 📌 publicId, token
POST  /personnel-files/{id}/identifications   { "identificationTypeCode":"DUI","identificationNumber":"..","isPrimary":true }
  (+ personal-info via PUT del shell, addresses, family-members, educations… — editables en Draft)
GET   /personnel-files/{id}/finalize/preview?createUserAccount=true       → isEligible
PATCH /personnel-files/{id}/finalize   If-Match   { "createUserAccount":true }     → status: Completed (+ usuario opcional)
POST  /personnel-files/{id}/employment-assignments   { "positionSlotPublicId":"{..}","orgUnitPublicId":"{..}","startDate":"2026-06-01T00:00:00Z","isPrimary":true,"isActive":true }
```
✅ **Compuerta:** los sub‑recursos de **Empleo / Talento / Compensación** (employment-assignments, salary-items,
bank-accounts, evaluations, etc.) sobre un `Draft` devuelven **`422` (lifecycle inválido)** — primero
**finalize**. La "mitad personal" (identidad/personal/formación) solo necesita General Catalogs, así que
podés empezarla en paralelo desde el Bloque 2. ⚠️ Finalizar exige `institutionalEmail`.

> Otros grupos de sub‑recursos (mismo patrón canónico CRUD): identidad/personal/formación (Draft) ·
> empleo/talento/compensación (Completed). Ver [personnel-files/README.md](./personnel-files/README.md).

---

## Bloque 8 — Transversales (en cualquier momento tras el Bloque 3)

### 8.1 Legal Representatives — `api/v1/...`
```
POST  /companies/{id}/legal-representatives   { "firstName":"..","lastName":"..","documentType":"DNI","documentNumber":"..","positionTitle":"..","representationType":"PrimaryLegalRepresentative","effectiveFromUtc":"..","isPrimary":true }
PATCH /legal-representatives/{id}/set-primary   If-Match
GET   /legal-representatives/{id}/usage         → canInactivate
```
⚠️ Siempre ≥1 activo (no se inactiva el último → `409 ...ACTIVE_MIN_REQUIRED`); documento único por compañía.

### 8.2 Audit Logs — `api/v1/audit/logs`  (read-only)
```
GET /audit/logs?page=1&pageSize=20&fromUtc=..&toUtc=..&entityPublicId=..&entityType=..
GET /audit/logs/{id}     → before/after/diff (saneado)
```
✅ Tiene sentido cuando ya hay actividad que auditar (orden descendente). ⚠️ Solo lectura; deriva los
dropdowns de `entityType`/`eventType` desde los datos.

### 8.3 Report Export Jobs — `api/v1/companies/{companyId}/report-export-jobs`  (async)
```
POST /companies/{id}/report-export-jobs   { "resourceKey":"POSITION_SLOTS","format":"xlsx","parameters":{ "status":"Vacant" } }  → 📌 jobId (202)
GET  /report-export-jobs/{jobId}          → poll hasta status: Succeeded
GET  /report-export-jobs/{jobId}/download → archivo (409 si no está listo; 410 si expiró)
```
⚠️ Requiere el permiso de lectura del `resourceKey`. El artefacto **expira** (`expiresUtc`): no caches el link.

---

## ✅ Checklist maestro (secuencia completa)

```
0. auth/login → system/status → crear compañía → switch → access-context
1. subscription preview→apply (HABILITAR módulos) · preferences · iam (rol) · company-users
2. general-catalogs (GET; guardar codes)
3. cost-center-types→cost-centers · unit-types/functional-areas→org-units · hierarchy→levels→groups · wc-types→work-centers
4. position-description-catalogs (function/contract/…/classification→category) · salary-tabulator (request→submit→approve→line)
5. job-profiles (ver walkthrough dedicado) · competency-framework (levels→conducts→matrix items)
6. position-slots (sobre un job profile)
7. files (upload-session→PUT→complete) · personnel-files (shell→identidad/personal→finalize→empleo)
8. legal-representatives · audit · report-export-jobs
```

## Errores comunes (RFC 7807, por `code`)

| HTTP | `code` | Cuándo |
|------|--------|--------|
| 400 | `common.validation` | validación, `If-Match` faltante, JSON Patch inválido |
| 401 | — | token faltante/expirado |
| 403 | `<MODULO>_FORBIDDEN` / `TENANT_MISMATCH` | sin permiso / módulo off / cross-tenant |
| 404 | `<ENTIDAD>_NOT_FOUND` | inexistente o cross-tenant enmascarado |
| 409 | `CONCURRENCY_CONFLICT` | `If-Match` viejo |
| 409 | `<ENTIDAD>_IN_USE` / `..._HAS_ACTIVE_CHILDREN` / `..._DEPENDENCY_CYCLE` | reglas de integridad |
| 422 | `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` / `PERSONNEL_FILE_LIFECYCLE_STATE_INVALID` | compuertas de estado |

---

### Índice

[INTEGRATION-ORDER.md](./INTEGRATION-ORDER.md) (orden por dependencias) ·
[README.md](./README.md) (mapa temático por fases) ·
[job-profiles/e2e-testing-walkthrough.md](./job-profiles/e2e-testing-walkthrough.md) (walkthrough profundo).
