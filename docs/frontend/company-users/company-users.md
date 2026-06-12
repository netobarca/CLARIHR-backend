# Company Users — Guía de consumo (frontend) · Fase 4

> **Prerequisitos:** [Autenticación](../auth/authentication.md) (Fase 1 — el invitado acepta con
> `POST /auth/company-user-invitations/accept`) y
> [Account Companies](../account-companies/account-companies.md) (Fase 2 — todo opera sobre la
> **compañía activa** del switch).
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## Overview

Un controlador (**Company Users**), **8 endpoints**, base **`/api/v1/company/users`**. Administra
los usuarios de la compañía: invitar, listar/buscar, editar nombre y roles, activar/desactivar y
reenviar invitaciones.

| Método | Ruta | Para qué | Permiso (`RBAC_USERS`) |
|--------|------|----------|------------------------|
| `GET` | `/company/users` | listar/buscar (paginado) | Read |
| `GET` | `/company/users/{publicId}` | detalle de un usuario | Read |
| `POST` | `/company/users` | **invitar** un usuario | Create |
| `PUT` | `/company/users/{publicId}` | reemplazar nombre + roles | Update |
| `PATCH` | `/company/users/{publicId}` | JSON Patch (`/firstName`, `/lastName`, `/rolePublicIds`) | Update |
| `PATCH` | `/company/users/{publicId}/deactivate` | desactivar (revoca sesiones) | Update |
| `PATCH` | `/company/users/{publicId}/reactivate` | reactivar | Update |
| `POST` | `/company/users/{publicId}/reset-invitation` | reenviar invitación | Update |

### Las 4 diferencias clave vs. las fases anteriores

1. **Tenant implícito**: la ruta NO lleva `companyPublicId` — siempre opera sobre la compañía
   activa de la sesión (la del último `switch`). Cambiar de compañía cambia qué usuarios ves.
2. **ETag débil (`W/"<hash>"`), solo en header**: el recurso es una proyección sin token
   persistido, así que el body **no** trae `concurrencyToken`. El token de concurrencia viaja
   únicamente en el header **`ETag`** de `GET`/mutaciones — capturalo de ahí y reenvialo en
   `If-Match` (sirve `W/"hash"` o `"hash"`). Faltante → `400`; stale → `409 CONCURRENCY_CONFLICT`.
3. **Permisos a nivel de campo**: las respuestas vienen **ya filtradas** según lo que el caller
   puede leer — un campo oculto llega `null`, uno enmascarado llega tipo `a***@dominio.com`/`***`.
   En escrituras, tocar un campo que no podés editar da `403 FIELD_EDIT_FORBIDDEN` (solo se
   evalúan los campos que realmente cambian). El detalle por campo se consulta con
   `GET …/authorization/resource-policies/RBAC_USERS` (Fase 2 §14); los `fieldKey` son
   `RBAC_USERS.EMAIL`, `RBAC_USERS.FIRST_NAME`, `RBAC_USERS.LAST_NAME`, `RBAC_USERS.ROLE`,
   `RBAC_USERS.STATUS`.
4. **El módulo depende del plan**: si el plan de la compañía no habilita gestión de usuarios →
   `403 company_users.module.disabled_by_plan` (gateá la pantalla con `effectiveModules` del
   access-context).

### El ciclo de invitación (cierra con la Fase 1)

```
POST /company/users {email, firstName, lastName, rolePublicIds}
  → 201 { user (status: PendingActivation), invitationExpiresUtc }   (token TTL 72 h)
  → email al invitado con el link de aceptación
invitado (anónimo): POST /auth/company-user-invitations/accept {token, password}
  → 200 AuthResponse con sesión tenant-scoped → status pasa a Active
¿no llegó / venció? → POST /company/users/{publicId}/reset-invitation (nuevo token, revoca los previos)
```

> ⚠️ **Hoy el envío de email es un stub de logging en el backend** (`LoggingEmailService` — no
> sale correo real y el token completo no se loguea). El response del invite **no** incluye el
> token. Para probar el ciclo end-to-end coordiná con backend (proveedor real de email o
> extracción del token en dev).

---

# Endpoints

## 1. Listar / buscar usuarios

### Endpoint
`GET /api/v1/company/users`

### Description
Lista paginada de los usuarios de la compañía activa, ya filtrada por los permisos de campo del
caller. Filtros por estado, rol y texto.

### Authentication
Bearer requerido (con tenant activo).

### Authorization
`RBAC_USERS` acción **Read**.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
Ninguno.

### Query Parameters
| Param | Tipo | Req. | Default | Validación |
|-------|------|------|---------|------------|
| `status` | enum string | No | — | `Active` \| `Inactive` \| `PendingActivation` |
| `roleId` | uuid | No | — | filtra por rol asignado |
| `search` | string | No | — | máx 100 (busca en nombre/email) |
| `page` | int | No | `1` | > 0 |
| `pageSize` | int | No | `20` | 1–100 |
| `includeAllowedActions` | bool | No | `false` | enriquece cada fila con `allowedActions` |

### Request Body
N/A.

### Responses
`200` — paginado canónico:

```json
{
  "items": [{
    "publicId": "…",
    "email": "carlos@empresa.com",
    "firstName": "Carlos",
    "lastName": "Mejía",
    "roles": [{ "publicId": "…", "name": "HR Admin", "description": "…", "isSystemRole": false }],
    "status": "Active",
    "allowedActions": { "canEdit": true, "canInactivate": false, "reasons": ["…"] }
  }],
  "pageNumber": 1, "pageSize": 20, "totalCount": 8
}
```

`400` / `401` / `403` ProblemDetails; `429` (rate limit **120/min** por usuario+tenant).

### Business Rules
- Campos que el caller no puede leer → `null`; enmascarados → valor enmascarado.
- `allowedActions.canInactivate=false` con razón cuando el usuario es el último admin activo —
  usalo para la botonera en vez de replicar la regla.

### Validation Rules
Las de query params.

### Security Considerations
La lista solo contiene usuarios del tenant activo — no hay forma de consultar otra compañía.

---

## 2. Obtener un usuario

### Endpoint
`GET /api/v1/company/users/{publicId}`

### Description
Detalle de un usuario de la compañía activa. La respuesta trae el header **`ETag: W/"<hash>"`**
— guardalo para el `If-Match` de las mutaciones siguientes.

### Authentication / Authorization
Bearer / `RBAC_USERS` Read.

### Request Headers
`Authorization` (req).

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `publicId` | uuid | id público del usuario |

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200` (+ header `ETag: W/"…"`):

```json
{
  "publicId": "…",
  "email": "carlos@empresa.com",
  "firstName": "Carlos",
  "lastName": "Mejía",
  "roles": [{ "publicId": "…", "name": "HR Admin", "isSystemRole": false }],
  "status": "PendingActivation"
}
```

| Status | `code` | Cuándo |
|--------|--------|--------|
| `401` / `403` | — / `company_users.management.forbidden` | token / sin permiso |
| `404` | `company_users.user.not_found` | no existe en esta compañía |

### Business Rules
El body **no** incluye token de concurrencia — es solo el header `ETag`. El hash se calcula sobre
la proyección sin filtrar, así que es estable aunque a vos te enmascaren campos.

### Validation Rules / Security Considerations
Un usuario de otra compañía da `404` en lectura (no revela existencia).

---

## 3. Invitar un usuario

### Endpoint
`POST /api/v1/company/users`

### Description
Invita un usuario a la compañía activa con sus roles iniciales. Crea (o reutiliza, si el email ya
tiene cuenta local) el usuario en `PendingActivation` y dispara el email de invitación
(token single-use, **72 h**).

### Authentication / Authorization
Bearer / `RBAC_USERS` **Create** (+ permiso de edición sobre los campos email/nombre/rol).

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
| `email` | string | Sí | formato email, máx 320 |
| `firstName` / `lastName` | string | Sí | máx 100; letras unicode/espacios/`'`/`-` (mismo formato que el registro) |
| `rolePublicIds` | uuid[] | Sí | **≥1**, sin vacíos, **únicos**; cada rol debe existir en la compañía activa |

```json
{
  "email": "carlos@empresa.com",
  "firstName": "Carlos",
  "lastName": "Mejía",
  "rolePublicIds": ["3c4d5e6f-…"]
}
```

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `201` | `CompanyUserInvitationResponse` + `Location` + `ETag: W/"…"` | invitado |
| `400` | `common.validation` | validación |
| `401` / `403` | — / `company_users.management.forbidden` / `FIELD_EDIT_FORBIDDEN` / `company_users.module.disabled_by_plan` | token / permisos / plan |
| `404` | `company_users.role.not_found` | algún rol no existe en esta compañía |
| `409` | `company_users.user_already_in_company` | el email ya pertenece a un usuario de esta compañía — **respuesta uniforme** (también cubre emails tomados en otros contextos, anti-enumeración) |
| `429` | `common.too_many_requests` | rate limit **10/min** por usuario+tenant |

```json
{
  "user": {
    "publicId": "…", "email": "carlos@empresa.com", "firstName": "Carlos",
    "lastName": "Mejía", "roles": [{ "publicId": "…", "name": "HR Admin" }],
    "status": "PendingActivation"
  },
  "invitationExpiresUtc": "2026-06-13T15:00:00Z"
}
```

### Business Rules
- Los roles se eligen del IAM de la compañía (la pantalla de roles llega en la Fase 5; mientras
  tanto los roles existentes se ven en `access-context.currentUserAccess.roles` o se piden a
  backend).
- Si el email ya tiene cuenta local en la plataforma (p. ej. se registró por su cuenta), se
  **reutiliza** ese usuario y se lo invita a esta compañía.
- El response **no** incluye el token de invitación (viaja solo en el email).
- Usá `invitationExpiresUtc` para mostrar la vigencia y habilitar el "reenviar".

### Validation Rules
Tabla del body.

### Security Considerations
El `409` es deliberadamente ambiguo — no distingas en UX entre "ya está en tu compañía" y otros
conflictos de email; mostrá "ese email ya está en uso".

---

## 4. Actualizar un usuario (PUT)

### Endpoint
`PUT /api/v1/company/users/{publicId}`

### Description
Reemplaza los campos editables: `firstName`, `lastName` y el set completo de roles. El email es
**inmutable**; el estado va por `/deactivate`–`/reactivate`.

### Authentication / Authorization
Bearer / `RBAC_USERS` **Update**. La autorización de campo se evalúa **solo sobre lo que cambia**
(mandar el mismo valor no exige permiso de edición de ese campo).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `W/"<hash>"` (del último `GET`/mutación) |

### Path Parameters
`publicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `firstName` / `lastName` | string | Sí | máx 100, formato persona |
| `rolePublicIds` | uuid[] | Sí | ≥1, únicos, roles de la compañía activa (replace-all) |

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `CompanyUserResponse` + `ETag` nuevo | actualizado |
| `400` | `common.validation` | validación / If-Match faltante |
| `403` | `FIELD_EDIT_FORBIDDEN` | cambiaste un campo que no podés editar |
| `403` | `TENANT_MISMATCH` | el usuario existe pero en otra compañía |
| `404` | `company_users.user.not_found` / `company_users.role.not_found` | usuario o rol inexistente |
| `409` | `CONCURRENCY_CONFLICT` | ETag stale → `GET` fresco y reintentar |

### Business Rules
- `rolePublicIds` es **replace-all**: mandá el set final (quitar un rol = mandarlo sin él).
- Cambiar roles re-emite efectos en IAM; el usuario afectado ve sus permisos nuevos al refrescar
  su token (≤15 min) o re-loguearse.

### Validation Rules
Tabla del body.

### Security Considerations
Pre-deshabilitá los inputs según `resource-policies/RBAC_USERS` (`readonly`/`hidden`) para no
descubrir el `FIELD_EDIT_FORBIDDEN` recién al guardar.

---

## 5. Patch de un usuario (RFC 6902)

### Endpoint
`PATCH /api/v1/company/users/{publicId}`

### Description
Cambios parciales por JSON Patch. Paths permitidos: **`/firstName`**, **`/lastName`**,
**`/rolePublicIds`**. Email inmutable; estado por `/deactivate`–`/reactivate`. Internamente
converge al mismo camino que el PUT (mismas reglas y errores).

### Authentication / Authorization / Headers
Como el PUT, pero `Content-Type: application/json-patch+json`.

### Path Parameters
`publicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
Array desnudo RFC 6902 (máx 50 ops / 64 KB):

```json
[
  { "op": "replace", "path": "/lastName", "value": "Mejía Soto" },
  { "op": "replace", "path": "/rolePublicIds", "value": ["3c4d5e6f-…", "9a8b7c6d-…"] }
]
```

### Responses
Iguales al PUT (`200` + `ETag` / `400` / `403` / `404` / `409`).

### Business Rules / Validation Rules / Security Considerations
Las del PUT; paths fuera de los 3 permitidos → `400`.

---

## 6. Desactivar un usuario

### Endpoint
`PATCH /api/v1/company/users/{publicId}/deactivate`

### Description
Desactiva al usuario en la compañía activa (usuario funcional + membresía + usuario IAM) y
**revoca sus refresh tokens** (sus sesiones mueren al expirar el access token, ≤15 min).

### Authentication / Authorization
Bearer / `RBAC_USERS` Update.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `If-Match` | Sí | `W/"<hash>"` |

### Path Parameters
`publicId` (uuid).

### Query Parameters / Request Body
Ninguno / sin body.

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `CompanyUserResponse` (status `Inactive`) + `ETag` nuevo | desactivado |
| `400` / `401` / `403` / `404` | estándar (+ `TENANT_MISMATCH` cross-tenant) | |
| `409` | `company_users.last_admin_required` | es el **último administrador activo** |
| `409` | `CONCURRENCY_CONFLICT` | ETag stale |

### Business Rules
- Invariante dura: la compañía siempre conserva ≥1 admin activo — el FE puede anticiparlo con
  `allowedActions` de la lista.
- No borra al usuario; es reversible con `/reactivate`.

### Security Considerations
Confirmación explícita en UX: corta el acceso del usuario en ≤15 min.

---

## 7. Reactivar un usuario

### Endpoint
`PATCH /api/v1/company/users/{publicId}/reactivate`

### Description
Reactiva la membresía del usuario en la compañía activa (y su usuario IAM cuando el usuario
funcional queda `Active`).

### Authentication / Authorization / Headers / Path / Query / Body
Igual que `/deactivate` (Bearer, Update, `If-Match`, sin body).

### Responses
`200` `CompanyUserResponse` (status `Active`) + `ETag` nuevo; `400`/`401`/`403`
(+`TENANT_MISMATCH`)/`404`; `409 CONCURRENCY_CONFLICT`.

### Business Rules
El usuario recupera acceso con sus roles previos (las asignaciones no se pierden al desactivar).

---

## 8. Reenviar invitación

### Endpoint
`POST /api/v1/company/users/{publicId}/reset-invitation`

### Description
Emite una invitación fresca para un usuario local pendiente/re-enviable: revoca los tokens de
invitación anteriores, genera uno nuevo (72 h) y envía el email.

### Authentication / Authorization
Bearer / `RBAC_USERS` Update.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

(**Sin `If-Match`** — no muta los campos proyectados del usuario.)

### Path Parameters
`publicId` (uuid).

### Query Parameters / Request Body
Ninguno / sin body.

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `CompanyUserInvitationResponse` (con nuevo `invitationExpiresUtc`) | reenviada |
| `401` / `403` / `404` | estándar (+ `TENANT_MISMATCH`) | |
| `409` | `company_users.reset_invitation.external_user_not_supported` | el usuario es de proveedor externo (Google) — no aplica invitación por contraseña |
| `429` | `common.too_many_requests` | rate limit **10/min** por usuario+tenant (compartido con invitar) |

### Business Rules
- El link anterior queda inválido al instante (single-use + revocación).
- Pensado para `PendingActivation`; el botón "reenviar" debería mostrarse solo en ese estado.

---

# Referencia compartida

## Enum `UserStatus` (wire, serializa como string)

| Valor | Significado |
|-------|-------------|
| `PendingActivation` | invitado, aún no aceptó (no puede loguearse) |
| `Active` | operativo |
| `Inactive` | desactivado en la compañía |

## Mecánica del ETag débil (distinta al resto de la API)

1. `GET` detalle (o cualquier mutación) → leer header `ETag: W/"abc123…"`.
2. Mutación siguiente → header `If-Match: W/"abc123…"` (o `"abc123…"` — el prefijo `W/` es opcional).
3. La respuesta trae el `ETag` rotado — reemplazá el guardado.
4. `409 CONCURRENCY_CONFLICT` → `GET` fresco, rehacer el cambio, reintentar.

> No busques `concurrencyToken` en el body: en este módulo no existe.

## Catálogo de códigos de error de la fase

| `code` | HTTP | Cuándo | Acción FE |
|--------|------|--------|-----------|
| `common.validation` | 400 | validación / If-Match faltante | errores por campo |
| `company_users.tenant.required` | 401 | JWT sin tenant activo | hacer switch primero |
| `company_users.current_user.invalid` | 401 | contexto de usuario inválido | re-login |
| `company_users.management.forbidden` | 403 | sin permiso RBAC_USERS | ocultar pantalla |
| `company_users.module.disabled_by_plan` | 403 | plan sin el módulo | upsell / ocultar módulo |
| `FIELD_EDIT_FORBIDDEN` | 403 | campo cambiado no editable | deshabilitar input (resource-policies) |
| `TENANT_MISMATCH` | 403 | escritura sobre usuario de otra compañía | bug de routing — revisar estado |
| `company_users.company.not_found` | 404 | compañía del tenant irresoluble | re-switch / soporte |
| `company_users.user.not_found` | 404 | usuario inexistente acá | volver a la lista |
| `company_users.role.not_found` | 404 | rol no existe en esta compañía | recargar catálogo de roles |
| `company_users.user_already_in_company` | 409 | email ya en uso (uniforme, anti-enum) | "ese email ya está en uso" |
| `company_users.last_admin_required` | 409 | desactivar al último admin | explicar la regla |
| `company_users.reset_invitation.external_user_not_supported` | 409 | reenviar a usuario Google | ocultar el botón para externos |
| `CONCURRENCY_CONFLICT` | 409 | ETag stale | recargar + reintentar |
| `common.too_many_requests` | 429 | rate limit | respetar `Retry-After` |

## Rate limits (por usuario + tenant)

| Endpoints | Límite |
|-----------|--------|
| `GET` lista | 120/min |
| `POST` invitar + `POST` reset-invitation | 10/min (compartido) |

## Guía de implementación del cliente

1. **Gating**: pantalla visible si el access-context trae permisos `RBAC_USERS` y el módulo está
   en `effectiveModules`; inputs del form según `resource-policies/RBAC_USERS` (campo
   `hidden`/`masked`/`readonly`/`editable`).
2. **ETag por fila**: guardá el `ETag` del `GET` detalle (no del listado) antes de editar; tras
   cada mutación reemplazalo por el del response.
3. **Render de campos filtrados**: `null` en un campo que el schema marca como presente puede
   significar "oculto para vos" — render como "—" y no lo trates como dato faltante.
4. **Roles**: dropdown de roles desde el IAM de la compañía (Fase 5); `rolePublicIds` siempre
   replace-all con ≥1 rol.
5. **Invitaciones**: tras el `201`, mostrá el estado `PendingActivation` con la fecha de
   expiración y el botón reenviar; el invitado termina en la página de aceptación de la Fase 1.
6. **Doble guardia al desactivar**: `allowedActions` para deshabilitar el botón + manejo del
   `409 last_admin_required` por si la condición cambió entre el render y el click.

## Próximas fases (orden sugerido)

1. **Roles y permisos (IAM)** — `AccountCompanyAuthorizationController` (CRUD de roles, grants,
   roles por usuario) + el role-builder-catalog de la Fase 2 §15. Da el catálogo de roles que
   este módulo consume.
2. **Suscripción** — `AccountCompanySubscriptionsController` (plan, add-ons, preview).
3. **General Catalogs** y módulos de negocio (Org, Personnel Files, …).
