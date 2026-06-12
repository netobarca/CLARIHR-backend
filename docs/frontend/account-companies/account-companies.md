# Account Companies — Guía de consumo (frontend) · Fase 2

> **Prerequisito:** [Autenticación](../auth/authentication.md). Este es el paso inmediato después
> del login: sin una **compañía activa** el JWT no tiene contexto de tenant y casi ningún módulo
> de la API es usable.
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## Overview

Tres controladores cubren el bootstrap de cuenta/compañía (15 endpoints, todos bajo
`/api/v1/account/companies`, todos **autenticados** con Bearer):

| Controlador (tag Swagger) | Endpoints | Para qué |
|---|---|---|
| **Account Companies Catalogs** | 4 × `GET` (countries, company-types, position-titles, representation-types) | catálogos del formulario "crear compañía" (pre-compañía) |
| **Account Companies** | `GET` list · `POST` create · `GET/PUT/PATCH {id}` · `PATCH {id}/archive` · `PATCH {id}/reactivate` · `POST {id}/switch` | administrar las compañías del usuario y **entrar** a una |
| **Account Access Context** | `GET {id}/access-context` · `GET {id}/authorization/resource-policies/{resourceKey}` · `GET {id}/authorization/role-builder-catalog` | permisos/capacidades efectivas para gatear la UI |

### El modelo (leer primero)

- **Ownership, no RBAC**: toda esta familia exige que el caller sea el **creador** de la compañía
  (`403 COMPANY_OWNERSHIP_FORBIDDEN` si no). Un usuario invitado a una compañía ajena NO la ve acá
  (su flujo entra por la invitación, ver doc de auth §8).
- **Crear ≠ entrar**: `POST` crea la compañía (con plan FREE) pero **no** la vuelve la compañía
  activa ni toca tu sesión. Para operar sobre ella hay que llamar **`POST {id}/switch`**, que la
  marca como primaria y **re-emite la sesión** (access + refresh nuevos con el tenant en el JWT).
- **Límite de compañías**: máximo **2** compañías activas/suspendidas por dueño
  (`409 COMPANY_LIMIT_REACHED` al crear, `409 COMPANY_REACTIVATION_LIMIT_REACHED` al reactivar).
- **Concurrencia optimista**: `PUT` / `PATCH` / `archive` / `reactivate` requieren header
  **`If-Match: "<concurrencyToken>"`** (el token viene en el body y en el header `ETag` de cada
  respuesta). Faltante → `400`; stale → `409 CONCURRENCY_CONFLICT`. `switch` NO lleva If-Match.

### Flujos

**Onboarding (usuario nuevo, 0 compañías):**

```
GET countries → GET company-types?countryCode=XX
GET legal-representative-position-titles + legal-representative-representation-types
POST /account/companies            (form: compañía + representante legal inicial)
POST /account/companies/{id}/switch  → guardar accessToken/refreshToken nuevos + accessContext
→ dashboard
```

**Arranque de sesión (usuario existente):**

```
login → GET /account/companies?includeAllowedActions=true
  ├─ 0 compañías → onboarding
  ├─ ≥1: la que tenga isActiveContext=true es la actual
  │     (el JWT del login ya trae ese tenant; para permisos: GET {id}/access-context)
  └─ selector de compañía → POST {id}/switch al elegir otra
```

### Orden de integración recomendado

1. `GET list` + `switch` + `access-context` — selector de compañía + gating de UI.
2. Catálogos + `POST create` — onboarding/crear compañía.
3. `GET {id}` + `PUT`/`PATCH` — pantalla de edición.
4. `archive`/`reactivate` — gestión del portafolio.
5. `resource-policies/{resourceKey}` — gating fino por campo (a medida que se construyan los forms).

---

# A. Catálogos del formulario (Account Companies Catalogs)

Los 4 son `GET` **read-only, solo autenticación** (no requieren compañía — alimentan el form
antes de que exista una). Sin path params; solo `company-types` lleva query param. Errores
comunes: `401` (token), `400` solo en `company-types`.

## 1. Países soportados

### Endpoint
`GET /api/v1/account/companies/countries`

### Description
Catálogo de países disponibles para aprovisionar una compañía (código ISO + metadata).

### Authentication / Authorization
Bearer requerido / solo autenticación (sin ownership).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path / Query Parameters
Ninguno.

### Request Body
N/A.

### Responses
`200` — array (no paginado) de:

```json
[{ "publicId": "…", "code": "HN", "name": "Honduras", "sortOrder": 1, "defaultLocale": "es-HN", "normalizedCode": "HN" }]
```

`401` ProblemDetails.

### Business Rules
El `code` de la opción elegida es lo que va como `countryCode` en el create.

### Validation Rules / Security Considerations
N/A — catálogo de referencia; cachealo por sesión.

---

## 2. Tipos de compañía por país

### Endpoint
`GET /api/v1/account/companies/company-types?countryCode={code}`

### Description
Tipos de compañía **activos** disponibles para un país (ej. S.A., S. de R.L.).

### Authentication / Authorization
Bearer / solo autenticación.

### Request Headers
`Authorization` (req).

### Path Parameters
Ninguno.

### Query Parameters
| Param | Tipo | Req. | Validación |
|-------|------|------|------------|
| `countryCode` | string | Sí | `^[A-Za-z]{2,3}$` (código ISO 2–3 letras); faltante/malformado → `400` |

### Request Body
N/A.

### Responses
`200` — array de:

```json
[{
  "publicId": "…", "code": "SA", "name": "Sociedad Anónima", "description": "…",
  "sortOrder": 1, "isActive": true, "concurrencyToken": "…",
  "createdAtUtc": "…", "modifiedAtUtc": null, "normalizedCode": "SA"
}]
```

`400` / `401` ProblemDetails.

### Business Rules
El `publicId` elegido va como `companyTypePublicId` en create/update (es **opcional** — se puede
crear compañía sin tipo).

### Validation Rules / Security Considerations
Recargar al cambiar el país seleccionado en el form.

---

## 3. Cargos del representante legal

### Endpoint
`GET /api/v1/account/companies/legal-representative-position-titles`

### Description
Catálogo de cargos (position titles) para el representante legal inicial.

### Authentication / Authorization / Headers / Params / Body
Igual que países (Bearer; sin params; sin body).

### Responses
`200` — array de `{ publicId, code, name, sortOrder, normalizedCode }`. `401` ProblemDetails.

### Business Rules
En el create, `positionTitle` es un **string** (mandá el `name` de la opción elegida — o texto
libre que cumpla el formato; el catálogo es para poblar el dropdown).

---

## 4. Tipos de representación legal

### Endpoint
`GET /api/v1/account/companies/legal-representative-representation-types`

### Description
Catálogo de tipos de representación para el dropdown del form.

### Authentication / Authorization / Headers / Params / Body
Igual que países.

### Responses
`200` — array de `{ publicId, code, name, sortOrder, normalizedCode }`. `401` ProblemDetails.

### Business Rules
En el create, `representationType` es el **enum string** (`PrimaryLegalRepresentative` |
`AlternateLegalRepresentative` | `AttorneyInFact`) — el `code` del catálogo coincide con esos
valores; lo que viaja es el enum, no el `publicId`.

---

# B. Compañías (Account Companies)

## 5. Listar mis compañías

### Endpoint
`GET /api/v1/account/companies`

### Description
Set paginado de compañías **del usuario autenticado** (creadas por él), filtrable por estado.
Único endpoint que devuelve la colección completa.

### Authentication
Bearer requerido.

### Authorization
Ownership implícito: solo devuelve compañías propias (las ajenas no aparecen).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
Ninguno.

### Query Parameters
| Param | Tipo | Req. | Default | Validación |
|-------|------|------|---------|------------|
| `status` | enum string | No | — | `Active` \| `Suspended` \| `Archived` |
| `page` | int | No | `1` | > 0 |
| `pageSize` | int | No | `20` | 1–100 (fuera de rango → `400`) |
| `includeAllowedActions` | bool | No | `false` | `true` enriquece cada fila con `allowedActions` |

### Request Body
N/A.

### Responses
`200` — paginado canónico:

```json
{
  "items": [{
    "publicId": "…", "name": "Mi Empresa", "slug": "mi-empresa", "countryCode": "HN",
    "status": "Active", "planCode": "FREE", "isActiveContext": true,
    "isOwnedByCurrentUser": true, "createdAtUtc": "…",
    "companyType": { "publicId": "…", "code": "SA", "name": "Sociedad Anónima", "isActive": true },
    "allowedActions": { "canEdit": true, "canArchive": false, "canActivate": false, "reasons": ["…"] }
  }],
  "pageNumber": 1, "pageSize": 20, "totalCount": 2
}
```

`400` / `401` ProblemDetails.

### Business Rules
- `isActiveContext: true` marca la compañía activa actual (la del tenant del JWT) — usala para
  resaltar la actual en el selector.
- `allowedActions` (solo con `includeAllowedActions=true`): `canEdit`/`canArchive`/`canActivate`
  con `reasons[]` explicando los bloqueos (ej. no se puede archivar la activa) — usalo para
  habilitar/deshabilitar botones sin replicar reglas en el FE.
- `companyType` y `allowedActions` pueden venir `null`.

### Validation Rules
Las de query params.

### Security Considerations
Lista vacía ≠ error: usuario invitado (no dueño) u onboarding pendiente.

---

## 6. Crear compañía

### Endpoint
`POST /api/v1/account/companies`

### Description
Aprovisiona una compañía propiedad del usuario (con su representante legal inicial y plan FREE).
**No cambia la sesión** — ver `switch`.

### Authentication
Bearer requerido.

### Authorization
Solo autenticación (la compañía nace siendo tuya).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |

### Path / Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `name` | string | Sí | máx 150 |
| `countryCode` | string | Sí | `^[A-Za-z]{2,3}$` (del catálogo de países) |
| `companyTypePublicId` | uuid | No | de `company-types`; inexistente → `404` |
| `initialLegalRepresentative` | object | Sí | ↓ |
| &nbsp;&nbsp;`.firstName` / `.lastName` | string | Sí | máx 100; `^[\p{L}][\p{L}\p{N} '.-]{0,99}$` |
| &nbsp;&nbsp;`.documentType` | string | Sí | máx 40 |
| &nbsp;&nbsp;`.documentNumber` | string | Sí | máx 80; `^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$` |
| &nbsp;&nbsp;`.positionTitle` | string | Sí | máx 150; `^[\p{L}\p{N}][\p{L}\p{N} '&().,/-]{0,149}$` |
| &nbsp;&nbsp;`.representationType` | enum string | Sí | `PrimaryLegalRepresentative` \| `AlternateLegalRepresentative` \| `AttorneyInFact` |
| &nbsp;&nbsp;`.authorityDescription` | string | No | máx 500 |
| &nbsp;&nbsp;`.appointmentInstrument` | string | No | máx 500 |
| &nbsp;&nbsp;`.appointmentDateUtc` | date-time | No | |
| &nbsp;&nbsp;`.effectiveFromUtc` | date-time | Sí | |
| &nbsp;&nbsp;`.effectiveToUtc` | date-time | No | fecha ≥ `effectiveFromUtc` |
| &nbsp;&nbsp;`.email` | string | No | formato email, máx 320 |
| &nbsp;&nbsp;`.phone` | string | No | máx 40 |
| &nbsp;&nbsp;`.isPrimary` | bool | No | |

```json
{
  "name": "Mi Empresa S.A.",
  "countryCode": "HN",
  "companyTypePublicId": "1f2e3d4c-…",
  "initialLegalRepresentative": {
    "firstName": "Ana", "lastName": "García",
    "documentType": "DNI", "documentNumber": "0801-1990-12345",
    "positionTitle": "Gerente General",
    "representationType": "PrimaryLegalRepresentative",
    "effectiveFromUtc": "2026-06-10T00:00:00Z"
  }
}
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `201` | `AccountCompanyDetailResponse` + headers `Location` y `ETag` | creada |
| `400` | `common.validation` | validación |
| `401` | ProblemDetails | sin token |
| `404` | ProblemDetails | `companyTypePublicId` inexistente |
| `409` | `COMPANY_LIMIT_REACHED` | ya hay 2 compañías activas/suspendidas |

### Business Rules
- Plan inicial **FREE**; el `slug` lo genera el servidor (no se manda).
- La nueva compañía **no** queda como activa: encadená `switch` inmediatamente en el onboarding.
- Tope de **2** compañías activas+suspendidas por dueño (las archivadas no cuentan).

### Validation Rules
Las de la tabla (todas las del representante legal aplican anidadas — los errores llegan como
`errors["initialLegalRepresentative.firstName"]` etc.).

### Security Considerations
El servidor ignora cualquier intento de fijar plan/slug/owner — no hay campos para eso.

---

## 7. Obtener una compañía

### Endpoint
`GET /api/v1/account/companies/{companyPublicId}`

### Description
Detalle de una compañía propia: representantes legales activos, tipo, y el `concurrencyToken`
para mutaciones.

### Authentication
Bearer requerido.

### Authorization
Ownership: ajena → `403 COMPANY_OWNERSHIP_FORBIDDEN`; inexistente → `404 COMPANY_NOT_FOUND`.

### Request Headers
`Authorization` (req).

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | id público de la compañía |

### Query Parameters
Ninguno.

### Request Body
N/A.

### Responses
`200` (+ header `ETag`):

```json
{
  "publicId": "…", "name": "Mi Empresa S.A.", "slug": "mi-empresa-sa", "countryCode": "HN",
  "status": "Active", "planCode": "FREE", "isActiveContext": true, "isOwnedByCurrentUser": true,
  "createdAtUtc": "…", "modifiedAtUtc": "…",
  "concurrencyToken": "8f3a1c2e-…",
  "activeLegalRepresentatives": [{
    "publicId": "…", "fullName": "Ana García",
    "representationType": "PrimaryLegalRepresentative",
    "positionTitle": "Gerente General", "isPrimary": true
  }],
  "companyType": { "publicId": "…", "code": "SA", "name": "Sociedad Anónima", "isActive": true }
}
```

`400` / `401` / `403` / `404` ProblemDetails.

### Business Rules
El `concurrencyToken` de esta respuesta es el que va en `If-Match` del siguiente
`PUT`/`PATCH`/`archive`/`reactivate`.

### Validation Rules / Security Considerations
Tratá `403` y `404` igual en UX (no revelar existencia).

---

## 8. Actualizar (PUT)

### Endpoint
`PUT /api/v1/account/companies/{companyPublicId}`

### Description
Reemplaza los campos editables: **`name` y `companyTypePublicId`** (nada más es editable).

### Authentication / Authorization
Bearer / ownership.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` (citado como ETag; faltante/malformado → `400`) |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `name` | string | Sí | máx 150 |
| `companyTypePublicId` | uuid | No | `null` limpia el tipo |

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `AccountCompanyDetailResponse` + `ETag` con el token **nuevo** | actualizada |
| `400` | ProblemDetails | validación / `If-Match` faltante |
| `401`/`403`/`404` | ProblemDetails | token / ownership / inexistente |
| `409` | `CONCURRENCY_CONFLICT` | token stale → recargar (`GET`) y reintentar |

### Business Rules
El estado NO se cambia por acá (usar `archive`/`reactivate`).

### Validation Rules
Tabla del body.

### Security Considerations
Siempre usá el token de la última respuesta (cada mutación lo rota).

---

## 9. Patch (RFC 6902)

### Endpoint
`PATCH /api/v1/account/companies/{companyPublicId}`

### Description
Actualización parcial por JSON Patch. Paths permitidos: **`/name`** (requerido, no removible) y
**`/companyTypePublicId`** (`null`/`remove` lo limpia).

### Authentication / Authorization
Bearer / ownership.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | **`application/json-patch+json`** |
| `If-Match` | Sí | `"<concurrencyToken>"` |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
> ⚠️ El wire real es un **array desnudo** de operaciones RFC 6902 (el esquema `{ "operations": [...] }`
> que muestra Swagger es engañoso — convención de toda la API).

```json
[
  { "op": "replace", "path": "/name", "value": "Nuevo Nombre S.A." }
]
```

Operaciones: `add` / `replace` / `remove`. Máx **50 operaciones** por documento; body máx **64 KB**
(excedido → `400` / `413`).

### Responses
Igual que el `PUT` (`200` + `ETag` nuevo; `400`/`401`/`403`/`404`/`409`).

### Business Rules
Para 2 campos editables, `PUT` suele ser más simple; `PATCH` existe por canonicidad.

### Validation Rules
`/name` no vacío máx 150 al aplicar; paths desconocidos → `400`.

### Security Considerations
Igual que `PUT`.

---

## 10. Archivar

### Endpoint
`PATCH /api/v1/account/companies/{companyPublicId}/archive`

### Description
Archiva una compañía propia (soft-disable: deja de contar para el cupo y no puede usarse).

### Authentication / Authorization
Bearer / ownership.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `If-Match` | Sí | `"<concurrencyToken>"` |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters / Request Body
Ninguno / sin body.

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `AccountCompanyDetailResponse` (status `Archived`) + `ETag` | archivada |
| `400`/`401`/`403`/`404` | ProblemDetails | estándar |
| `409` | `ACTIVE_COMPANY_ARCHIVE_FORBIDDEN` | es la compañía activa — hacé `switch` a otra primero |
| `409` | `COMPANY_ALREADY_ARCHIVED` | ya estaba archivada |
| `409` | `CONCURRENCY_CONFLICT` | If-Match stale |

### Business Rules
- **No se puede archivar la compañía activa/primaria** — el FE debe ofrecer cambiar a otra antes.
- Archivar libera cupo (permite crear/reactivar otra).

### Validation Rules
N/A (sin body).

### Security Considerations
Mostrá confirmación explícita: el módulo entero de esa compañía deja de estar disponible para
los usuarios.

---

## 11. Reactivar

### Endpoint
`PATCH /api/v1/account/companies/{companyPublicId}/reactivate`

### Description
Reactiva una compañía archivada (sujeto al cupo de activas).

### Authentication / Authorization / Headers / Path / Query / Body
Igual que `archive` (Bearer, ownership, `If-Match`, sin body).

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `AccountCompanyDetailResponse` (status `Active`) + `ETag` | reactivada |
| `409` | `COMPANY_REACTIVATION_LIMIT_REACHED` | cupo de 2 activas lleno |
| `409` | `COMPANY_ALREADY_ACTIVE` | no estaba archivada |
| `409` | `CONCURRENCY_CONFLICT` | If-Match stale |
| `400`/`401`/`403`/`404` | ProblemDetails | estándar |

### Business Rules
Reactivar **no** la vuelve activa de sesión — sigue haciendo falta `switch`.

---

## 12. Switch — entrar a una compañía ⭐

### Endpoint
`POST /api/v1/account/companies/{companyPublicId}/switch`

### Description
Marca la compañía como **primaria/activa** del usuario y **re-emite la sesión**: devuelve
access/refresh tokens nuevos (con el tenant de esa compañía en el JWT), el resumen de la compañía
activa y el **access context completo** (no hace falta llamar a `access-context` después).

### Authentication
Bearer requerido.

### Authorization
Ownership + la compañía debe estar `Active` + el caller debe tener **membresía activa** en ella.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters / Request Body
Ninguno / sin body (tampoco `If-Match` — la acción re-emite el JWT, no es una edición).

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `SwitchActiveCompanyResponse` ↓ | sesión re-emitida |
| `401`/`403`/`404` | ProblemDetails | token / ownership / inexistente |
| `404` | `ACCOUNT_COMPANY_SUBSCRIPTION_NOT_FOUND` | la compañía no tiene suscripción activa |
| `409` | `ACTIVE_COMPANY_SWITCH_FORBIDDEN` | compañía no-Active o sin membresía activa |
| `429` | `common.too_many_requests` | rate limit (10/min por usuario) |

```json
{
  "accessToken": "eyJ…",
  "refreshToken": "q7Zb1…",
  "expiresIn": 900,
  "activeCompany": { "publicId": "…", "name": "Mi Empresa S.A.", "slug": "…", "countryCode": "HN", "status": "Active" },
  "accessContext": { /* misma forma que GET access-context, ver §13 */ }
}
```

### Business Rules
- **Reemplazá ambos tokens** almacenados con los nuevos — el JWT viejo sigue siendo válido hasta
  expirar pero apunta al tenant anterior; usarlo mezclaría contextos.
- El refresh posterior re-resuelve este tenant (la compañía primaria quedó persistida).
- Es el único mecanismo de cambio de compañía: no existe "header de tenant" por request.

### Validation Rules
`companyPublicId` no vacío.

### Security Considerations
Tras el switch, invalidá **todo** estado/caché del FE que dependa del tenant (listas, permisos,
preferencias) — es efectivamente un nuevo login.

---

# C. Contexto de acceso (Account Access Context)

## 13. Access context ⭐

### Endpoint
`GET /api/v1/account/companies/{companyPublicId}/access-context`

### Description
Contexto de acceso efectivo de una compañía propia: plan + add-ons (contexto comercial),
capacidades y módulos efectivos, y el acceso del usuario actual (roles, permisos, scopes).
**Es la fuente para gatear la UI** (menú, módulos, botones).

### Authentication / Authorization
Bearer / ownership (ajena → `403`/`404`).

### Request Headers
`Authorization` (req).

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200`:

```json
{
  "companyContext": { "publicId": "…", "name": "…", "slug": "…", "countryCode": "HN", "status": "Active" },
  "commercialContext": {
    "subscription": { "commercialPlanPublicId": "…", "code": "FREE", "name": "Free", "capabilityCodes": ["…"] },
    "extensions": [{ "commercialAddonPublicId": "…", "code": "…", "name": "…", "capabilityCodes": ["…"] }]
  },
  "effectiveCapabilities": [{ "capabilityCode": "…", "moduleKey": "…", "displayName": "…", "grantedByPlan": true, "grantedByAddon": false }],
  "effectiveModules": [{ "moduleKey": "…", "displayName": "…", "grantedByPlan": true, "grantedByAddon": false }],
  "currentUserAccess": {
    "roles": [{ "publicId": "…", "name": "Owner", "isSystemRole": true }],
    "permissions": [{ "publicId": "…", "code": "PersonnelFiles.Read", "module": "…", "kind": "…", "action": "…", "capabilityCodes": ["…"], "isDormant": false }],
    "scopes": [{ /* alcances por permiso */ }]
  }
}
```

`401`/`403`/`404` ProblemDetails; `404 ACCOUNT_COMPANY_SUBSCRIPTION_NOT_FOUND` si no hay
suscripción activa.

### Business Rules
- **Gating en dos capas**: un módulo/pantalla se muestra si (a) el módulo está en
  `effectiveModules` (lo da el plan/add-on) **y** (b) el usuario tiene el permiso en
  `currentUserAccess.permissions`. Tener el permiso sin la capability no alcanza, y viceversa.
- `switch` ya devuelve este mismo objeto — al cambiar de compañía no repitas esta llamada.

### Validation Rules
N/A.

### Security Considerations
El gating del FE es UX, no seguridad: el backend re-verifica todo en cada endpoint (403 si no).

---

## 14. Política por recurso (gating fino de forms)

### Endpoint
`GET /api/v1/account/companies/{companyPublicId}/authorization/resource-policies/{resourceKey}`

### Description
La política efectiva del usuario actual para **un** recurso: acciones
(`canAccess/canRead/canCreate/canUpdate/canDelete`) y estado por campo
(`hidden` / `masked` / `readonly` / `editable` + flags `isRequired`/`isSensitive`). Útil para
renderizar un form respetando permisos a nivel campo.

### Authentication / Authorization
Bearer / ownership **y además** la compañía debe ser tu **contexto activo actual**
(si no → `409 ACCOUNT_COMPANY_ACTIVE_CONTEXT_REQUIRED`: hacé `switch` primero).

### Request Headers
`Authorization` (req).

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la compañía (debe ser la activa) |
| `resourceKey` | string | clave del recurso; desconocida → `400` |

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200`:

```json
{
  "resourceKey": "…",
  "actions": { "canAccess": true, "canRead": true, "canCreate": true, "canUpdate": false, "canDelete": false },
  "fields": [{ "fieldKey": "…", "propertyName": "salary", "displayName": "Salario", "access": "masked", "isRequired": false, "isSensitive": true }]
}
```

`400` (resourceKey desconocida) / `401` / `403` / `404` / `409 ACCOUNT_COMPANY_ACTIVE_CONTEXT_REQUIRED`.

### Business Rules
Consultalo lazy por pantalla (no precargues todos los recursos).

---

## 15. Role-builder catalog (fase posterior)

### Endpoint
`GET /api/v1/account/companies/{companyPublicId}/authorization/role-builder-catalog`

### Description
Catálogo para **construir roles** (módulos/capacidades disponibles, permisos otorgables, catálogo
de field-policies, scope types). Lo consume la pantalla de administración de roles, que se integra
junto con `AccountCompanyAuthorizationController` (CRUD de roles/grants) en una fase posterior —
solo se lista aquí para completitud. Bearer + ownership; `200` con el catálogo; errores estándar.

---

# Referencia compartida

## Enums (serializan como string)

| Enum | Valores |
|------|---------|
| `CompanyStatus` | `Active` · `Suspended` · `Archived` (`Suspended` lo fija la plataforma, no este módulo) |
| `LegalRepresentativeRepresentationType` | `PrimaryLegalRepresentative` · `AlternateLegalRepresentative` · `AttorneyInFact` |

## Catálogo de códigos de error de la familia

| `code` | HTTP | Cuándo | Acción FE |
|--------|------|--------|-----------|
| `common.validation` | 400 | validación | errores por campo |
| `account_companies.current_user.invalid` | 401 | JWT sin usuario resoluble | re-login |
| `account_companies.user.not_found` | 404 | usuario del JWT inexistente | re-login |
| `COMPANY_NOT_FOUND` | 404 | id inexistente | volver a la lista |
| `COMPANY_OWNERSHIP_FORBIDDEN` | 403 | compañía ajena | tratar como 404 en UX |
| `COMPANY_LIMIT_REACHED` | 409 | crear con cupo lleno (2) | ofrecer archivar otra |
| `COMPANY_REACTIVATION_LIMIT_REACHED` | 409 | reactivar con cupo lleno | ídem |
| `COMPANY_ALREADY_ARCHIVED` / `COMPANY_ALREADY_ACTIVE` | 409 | transición redundante | refrescar lista |
| `ACTIVE_COMPANY_ARCHIVE_FORBIDDEN` | 409 | archivar la activa | pedir switch previo |
| `ACTIVE_COMPANY_SWITCH_FORBIDDEN` | 409 | switch a no-Active o sin membresía | refrescar lista |
| `ACCOUNT_COMPANY_SUBSCRIPTION_NOT_FOUND` | 404 | sin suscripción activa | error terminal, soporte |
| `ACCOUNT_COMPANY_ACTIVE_CONTEXT_REQUIRED` | 409 | resource-policy de compañía no activa | switch primero |
| `CONCURRENCY_CONFLICT` | 409 | If-Match stale | recargar y reintentar |
| `common.too_many_requests` | 429 | rate limit switch (10/min/usuario) | respetar `Retry-After` |

## Límites

| Cosa | Valor |
|------|-------|
| Compañías activas+suspendidas por dueño | **2** |
| `pageSize` lista | 1–100 (default 20) |
| JSON Patch | máx 50 operaciones / 64 KB body |
| Rate limit `switch` | 10/min por usuario |

---

# Guía de implementación del cliente

1. **Bootstrap post-login**: `GET /account/companies` → si 0, onboarding; si ≥1, la de
   `isActiveContext=true` es la actual y el JWT del login ya sirve. Cargá permisos con
   `GET {id}/access-context` de la activa.
2. **Selector de compañía**: al elegir otra → `POST {id}/switch` → reemplazar tokens + access
   context + **invalidar todo caché tenant-dependiente**.
3. **Onboarding**: catálogos → `POST` → `switch` (encadenado) → dashboard.
4. **Gating**: módulos del menú por `effectiveModules`; botones por
   `currentUserAccess.permissions`; campos sensibles por `resource-policies` (lazy).
5. **Mutaciones**: guardá el `concurrencyToken` de la última respuesta por compañía y mandalo en
   `If-Match`; ante `409 CONCURRENCY_CONFLICT` → `GET` + reintento con el token fresco.
6. **Botonera de acciones**: usá `allowedActions` (`includeAllowedActions=true`) en vez de
   replicar las reglas de archive/reactivate.

## Próximas fases (en orden sugerido)

1. **Preferencias** — `GET/PUT/PATCH /api/v1/account/me/preferences` (idioma, social links) y
   `GET/PUT/PATCH /api/v1/companies/{companyId}/preferences` (moneda, zona horaria).
2. **Company Users** — invitar/administrar usuarios de la compañía (`CompanyUsersController`;
   el invitado entra por `auth/company-user-invitations/accept`).
3. **Roles y permisos (IAM)** — `AccountCompanyAuthorizationController` + role-builder-catalog (§15).
4. **Suscripción** — `AccountCompanySubscriptionsController` (`/subscription`, `/plans`,
   `/addons`, `/preview`).
5. Con el tenant operativo: **General Catalogs** y los módulos de negocio (Org, Personnel Files…).
