# IAM — Roles y Permisos — Guía de consumo (frontend) · Fase 5

> **Prerequisitos:** [Account Companies](../account-companies/account-companies.md) (Fase 2 — todo
> opera sobre la **compañía activa**; el `role-builder-catalog` se introdujo ahí en §15) y
> [Company Users](../company-users/company-users.md) (Fase 4 — los `rolePublicIds` que ese módulo
> asigna salen de acá).
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

> ⚠️ **Estado backend:** esta superficie incluye cambios recientes (concurrencia con `If-Match` en
> los writes de rol + nuevo `PATCH`) que dependen de la migración
> `AddConcurrencyTokenToIamRoles`. Si el ambiente contra el que probás no la tiene aplicada, los
> writes pueden comportarse distinto — confirmá con backend que esté migrado y desplegado antes de
> integrar los `If-Match` de roles.

---

## Overview

Un controlador (**Account Authorization**), **8 endpoints**, base
**`/api/v1/account/companies/{companyPublicId}/authorization`**. Es la **única puerta** de
administración de IAM: roles (CRUD + grants) y la asignación de roles a usuarios.

| Método | Ruta (relativa a la base) | Para qué | Permiso (pantalla:acción) |
|--------|---------------------------|----------|---------------------------|
| `GET` | `/roles` | listar/buscar roles (paginado) | `Roles:Read` |
| `POST` | `/roles` | crear rol (con grants iniciales opcionales) | `Roles:Create` |
| `GET` | `/roles/{rolePublicId}` | detalle de un rol + sus grants | `Roles:Read` |
| `PUT` | `/roles/{rolePublicId}` | reemplazar nombre + descripción | `Roles:Update` |
| `PATCH` | `/roles/{rolePublicId}` | JSON Patch (`/name`, `/description`) | `Roles:Update` |
| `DELETE` | `/roles/{rolePublicId}` | borrar rol (hard delete) | `Roles:Delete` |
| `GET` | `/roles/{rolePublicId}/grants` | ver solo los grants del rol | `Roles:Read` |
| `PUT` | `/roles/{rolePublicId}/grants` | reemplazar el set de permisos del rol | `Permissions:Update` |
| `PUT` | `/users/{userPublicId}/roles` | reemplazar los roles de un usuario | `Users:Update` |

> Son 9 operaciones sobre 6 rutas. El **role-builder-catalog**
> (`GET …/authorization/role-builder-catalog`) está documentado en la Fase 2 §15 — es la fuente de
> los permisos disponibles (`availablePermissions`) que se asignan acá; lo repasamos abajo.

### Conceptos clave (leer primero)

- **El `companyPublicId` de la URL debe ser SIEMPRE la compañía activa** del JWT. Si no coincide →
  **`403` plano** (sin cuerpo ProblemDetails — es un Forbid del framework, no un error de dominio).
  No hay forma de administrar el IAM de una compañía que no sea tu tenant activo (hacé `switch`
  primero, Fase 2).
- **Autorización por pantalla**: el módulo distingue 3 "pantallas" RBAC — **Roles**, **Permissions**
  y **Users** — cada una con sus acciones. Un usuario puede tener `Roles:Read` sin `Roles:Update`,
  o administrar roles pero no asignar permisos (`Permissions:Update`). Gateá cada botón por su
  permiso (visible en `access-context.currentUserAccess.permissions`, Fase 2 §13).
- **Roles de sistema** (`isSystemRole: true`): no se pueden editar, parchear ni borrar (`403`).
  Vienen sembrados (ej. el rol de administrador). En la UI marcá esas filas como solo-lectura.
- **Dos modelos de concurrencia distintos en el mismo controlador**:
  - **Rol** → token **fuerte**: `concurrencyToken` (GUID) en el body y header `ETag: "<guid>"`;
    en los writes va como `If-Match: "<guid>"`.
  - **Roles-de-usuario** → token **débil**: `weakETag` (hash) en el body y header `ETag: W/"<hash>"`;
    en el write va como `If-Match: W/"<hash>"` (o `*` para escritura incondicional).
  - En ambos: faltante → `400`; stale → `409 CONCURRENCY_CONFLICT`.
- **Grants = permisos**. Un grant tiene `kind`:
  - `ScreenAction` → acción sobre una pantalla (`action` = `Read`/`Create`/…).
  - `Field` → acceso a un campo (`fieldName` + `fieldAccessState` = `Read`/`Write`).
  Esto es lo que produce el filtrado a nivel campo que viste en Company Users (Fase 4) y en
  `resource-policies` (Fase 2 §14).

### Dónde encaja con las otras fases

```
role-builder-catalog (Fase 2 §15)  → availablePermissions, scopeTypes, fieldPoliciesCatalog
        │  (catálogo para armar el rol)
        ▼
POST /authorization/roles {name, permissionPublicIds}   → crea el rol
        │
        ├─ PUT /roles/{id}/grants   → ajustar permisos del rol
        │
        ▼
asignar el rol a un usuario:
   · CompanyUsers (Fase 4): POST/PUT/PATCH con rolePublicIds   (door A, RBAC_USERS)
   · acá:  PUT /authorization/users/{userPublicId}/roles        (door B, Users:Update)
   (ambas puertas coexisten por diseño, con modelos de authz distintos)
```

---

# Endpoints

## 1. Listar / buscar roles

### Endpoint
`GET …/authorization/roles`

### Description
Roles IAM del tenant (paginado, filtrable por `search`). Con `includeAllowedActions=true` cada fila
trae las acciones permitidas (editar/borrar) para la botonera.

### Authentication
Bearer requerido (tenant activo = `companyPublicId`).

### Authorization
`Roles:Read`.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la compañía **activa** |

### Query Parameters
| Param | Tipo | Req. | Default | Validación |
|-------|------|------|---------|------------|
| `pageNumber` | int | No | `1` | > 0 |
| `pageSize` | int | No | `20` | 1–100 |
| `search` | string | No | — | máx 100 |
| `includeAllowedActions` | bool | No | `false` | enriquece con `allowedActions` |

### Request Body
N/A.

### Responses
`200` — paginado de `AuthorizationRoleSummaryResponse`:

```json
{
  "items": [{
    "publicId": "…",
    "name": "HR Admin",
    "description": "Gestiona expedientes y nómina",
    "isSystemRole": false,
    "grantCount": 24,
    "userCount": 3
  }],
  "pageNumber": 1, "pageSize": 20, "totalCount": 5
}
```

`400` / `401` / `403` ProblemDetails.

### Business Rules
- `grantCount` / `userCount` son contadores para la lista (cuántos permisos tiene, a cuántos
  usuarios está asignado) — útiles para advertir antes de borrar.
- El listado **no** trae los grants completos (usá el detalle).

### Validation Rules / Security Considerations
Las de query params. La lista es estrictamente del tenant activo.

---

## 2. Crear rol

### Endpoint
`POST …/authorization/roles`

### Description
Crea un rol personalizado con un set inicial de permisos opcional. Devuelve `201` con el rol y sus
grants resueltos.

### Authentication / Authorization
Bearer / `Roles:Create`.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |

### Path Parameters
`companyPublicId` (uuid, la activa).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `name` | string | Sí | máx 100 (único en el tenant) |
| `description` | string | No | máx 500 |
| `permissionPublicIds` | uuid[] | No | máx **1000**; ids de permisos del `role-builder-catalog` |

```json
{
  "name": "Supervisor de Nómina",
  "description": "Lectura de expedientes + edición de nómina",
  "permissionPublicIds": ["a1b2c3d4-…", "e5f6a7b8-…"]
}
```

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `201` | `AuthorizationRoleResponse` + `Location` + `ETag: "<guid>"` | creado |
| `400` | `common.validation` | validación |
| `401` / `403` | — / `iam.management.forbidden` / `iam.module.disabled_by_plan` | token / permiso / plan |
| `404` | `iam.permissions.collection_not_found` | algún `permissionPublicId` no existe en el tenant |
| `409` | `iam.roles.name_conflict` | ya hay un rol con ese nombre |

```json
{
  "publicId": "…",
  "name": "Supervisor de Nómina",
  "description": "…",
  "isSystemRole": false,
  "userCount": 0,
  "concurrencyToken": "8f3a1c2e-…",
  "grants": [{
    "publicId": "…", "code": "PAYROLL.EDIT", "name": "Editar nómina",
    "module": "Payroll", "resourceKey": "PAYROLL", "kind": "ScreenAction",
    "action": "Update", "fieldName": null, "fieldAccessState": null
  }]
}
```

### Business Rules
- Los `permissionPublicIds` salen de `role-builder-catalog.availablePermissions[].publicId`. No
  inventes ids: el catálogo ya está filtrado por las capacidades del plan de la compañía.
- Guardá el `concurrencyToken` del body para el primer write.

### Validation Rules
Tabla del body.

### Security Considerations
El backend valida que cada permiso exista y sea otorgable en el tenant; un permiso "dormant" del
catálogo (`isDormant: true`) no debería ofrecerse en la UI.

---

## 3. Obtener un rol

### Endpoint
`GET …/authorization/roles/{rolePublicId}`

### Description
Un rol con su **set completo de grants**. Trae el `concurrencyToken` (body + header `ETag`) para
los writes siguientes.

### Authentication / Authorization
Bearer / `Roles:Read`.

### Request Headers
`Authorization` (req).

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la activa |
| `rolePublicId` | uuid | el rol |

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200` `AuthorizationRoleResponse` (+ `ETag: "<guid>"`); `401` / `403`;
`404 iam.roles.not_found` (incluye id de otro tenant — no revela existencia).

### Business Rules
El `concurrencyToken` de acá es el que va en `If-Match` del PUT/PATCH/grants.

### Validation Rules / Security Considerations
Un rol de otra compañía da `404`.

---

## 4. Actualizar rol (PUT)

### Endpoint
`PUT …/authorization/roles/{rolePublicId}`

### Description
Reemplaza los campos editables: `name` y `description`. Los permisos NO se tocan acá (usar
`/grants`).

### Authentication / Authorization
Bearer / `Roles:Update`.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` (token **fuerte**, GUID citado) |

### Path Parameters
`companyPublicId`, `rolePublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `name` | string | Sí | máx 100, único |
| `description` | string | No | máx 500 |

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `AuthorizationRoleResponse` + `ETag` nuevo | actualizado |
| `400` | `common.validation` | validación / If-Match faltante |
| `403` | `iam.roles.protected_role.forbidden` | es un rol de sistema |
| `404` | `iam.roles.not_found` | inexistente |
| `409` | `iam.roles.name_conflict` | nombre duplicado |
| `409` | `CONCURRENCY_CONFLICT` | token stale → `GET` y reintentar |

### Business Rules
Roles de sistema → bloqueá la edición en la UI (no muestres el form).

### Validation Rules / Security Considerations
Igual que el resto: el token rota en cada write exitoso.

---

## 5. Patch de rol (RFC 6902)

### Endpoint
`PATCH …/authorization/roles/{rolePublicId}`

### Description
Cambios parciales por JSON Patch. Paths: **`/name`** (requerido, no removible) y **`/description`**
(`null`/`remove` lo limpia). Los grants van por su endpoint; roles de sistema bloqueados.

### Authentication / Authorization / Headers
Como el PUT, pero `Content-Type: application/json-patch+json`. `If-Match: "<guid>"` requerido.

### Path Parameters
`companyPublicId`, `rolePublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
Array desnudo RFC 6902 (máx 50 ops / 64 KB):

```json
[ { "op": "replace", "path": "/description", "value": "Solo lectura de nómina" } ]
```

### Responses
Iguales al PUT (`200` + `ETag` / `400` / `403` protected / `404` / `409`×2).

### Business Rules
Un patch no-op (sin cambios reales) no rota el token.

### Validation Rules
`/name` máx 100; paths fuera de los 2 permitidos → `400`.

---

## 6. Borrar rol

### Endpoint
`DELETE …/authorization/roles/{rolePublicId}`

### Description
Borra **permanentemente** un rol personalizado (hard delete, sin estado de soft-delete). Guardado
por reglas de negocio.

### Authentication / Authorization
Bearer / `Roles:Delete`.

### Request Headers
`Authorization` (req). **Sin `If-Match`** (no muta campos versionados — es un borrado guardado).

### Path Parameters
`companyPublicId`, `rolePublicId` (uuid).

### Query Parameters / Request Body
Ninguno / sin body.

### Responses
| Status | `code` | Cuándo |
|--------|--------|--------|
| `204` | — | borrado |
| `401` / `403` | — / `iam.management.forbidden` | token / permiso |
| `403` | `iam.roles.protected_role.delete_forbidden` | rol de sistema |
| `404` | `iam.roles.not_found` | inexistente |
| `409` | `iam.roles.in_use` | el rol aún está asignado a usuarios |

### Business Rules
- No se puede borrar un rol con usuarios asignados (`409 iam.roles.in_use`) — primero reasigná esos
  usuarios. Usá `userCount` del listado para anticiparlo y `allowedActions.canDelete` si pediste
  `includeAllowedActions=true`.
- Es irreversible (no hay papelera).

### Security Considerations
Confirmación explícita en UX.

---

## 7. Ver grants de un rol

### Endpoint
`GET …/authorization/roles/{rolePublicId}/grants`

### Description
Solo el set de permisos del rol (más liviano que el detalle completo). Trae el `concurrencyToken`
del rol para el PUT de grants.

### Authentication / Authorization
Bearer / `Roles:Read`.

### Request Headers
`Authorization` (req).

### Path Parameters
`companyPublicId`, `rolePublicId` (uuid).

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200` `AuthorizationRoleGrantsResponse` (+ `ETag: "<guid>"`):

```json
{
  "rolePublicId": "…",
  "roleName": "Supervisor de Nómina",
  "isSystemRole": false,
  "concurrencyToken": "8f3a1c2e-…",
  "grants": [{
    "publicId": "…", "code": "PAYROLL.EDIT", "name": "Editar nómina",
    "module": "Payroll", "resourceKey": "PAYROLL", "kind": "ScreenAction",
    "action": "Update", "fieldName": null, "fieldAccessState": null
  }]
}
```

`401` / `403`; `404 iam.roles.not_found`.

### Business Rules
Es la pantalla de "permisos del rol"; el `concurrencyToken` que devuelve es el del rol (mismo que
el detalle).

---

## 8. Reemplazar grants de un rol

### Endpoint
`PUT …/authorization/roles/{rolePublicId}/grants`

### Description
Reemplaza el **set completo** de permisos del rol en una sola llamada (replace-all). Aplica el
invariante "mantener al menos un administrador de seguridad RBAC".

### Authentication / Authorization
Bearer / **`Permissions:Update`** (distinto de `Roles:Update`).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` (token fuerte del rol) |

### Path Parameters
`companyPublicId`, `rolePublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `permissionPublicIds` | uuid[] | Sí | máx 1000; ids del `role-builder-catalog` (replace-all; `[]` deja el rol sin permisos) |

```json
{ "permissionPublicIds": ["a1b2c3d4-…", "e5f6a7b8-…"] }
```

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `AuthorizationRoleGrantsResponse` + `ETag` nuevo | reemplazado |
| `400` | `common.validation` | validación / If-Match faltante |
| `403` | `iam.roles.protected_role.forbidden` | rol de sistema |
| `403` | `iam.management.forbidden` | sin `Permissions:Update` |
| `404` | `iam.roles.not_found` / `iam.permissions.collection_not_found` | rol o algún permiso inexistente |
| `409` | `iam.roles.last_administrator_required` | el cambio dejaría al tenant sin administrador de seguridad |
| `409` | `CONCURRENCY_CONFLICT` | token stale |

### Business Rules
- **Replace-all**: mandá el set final (quitar un permiso = mandarlo sin él).
- El invariante de "último administrador" impide quitarle a un rol los permisos que dejarían a la
  compañía sin nadie capaz de administrar seguridad — anticipalo en la UI con una advertencia.
- Cambiar grants rota el `concurrencyToken` del rol (afecta también el detalle/GET grants).

### Validation Rules
Tabla del body.

### Security Considerations
Pantalla solo para quien tenga `Permissions:Update` (más restringido que editar el rol).

---

## 9. Reemplazar los roles de un usuario

### Endpoint
`PUT …/authorization/users/{userPublicId}/roles`

### Description
Reemplaza el **set completo** de roles del usuario IAM. Es una **segunda puerta** a la asignación
de roles (se solapa con `CompanyUsers PATCH /rolePublicIds`, Fase 4, con otro modelo de authz —
coexisten por diseño). Aplica el invariante "al menos un administrador activo".

### Authentication / Authorization
Bearer / **`Users:Update`**.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `W/"<hash>"` (token **débil**) **o** `*` (escritura incondicional) |

> El token débil del usuario se obtiene del header `ETag: W/"<hash>"` de la respuesta de **este
> mismo endpoint** o del `weakETag` en su body. No hay un `GET` de user-roles para sembrarlo; por
> eso se admite `*` en la primera escritura.

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la activa |
| `userPublicId` | uuid | el usuario IAM |

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `rolePublicIds` | uuid[] | Sí | máx **200**; roles del tenant (replace-all) |

```json
{ "rolePublicIds": ["3c4d5e6f-…", "9a8b7c6d-…"] }
```

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `AuthorizationUserRolesResponse` + `ETag: W/"…"` nuevo | reemplazado |
| `400` | `common.validation` | validación / If-Match faltante o malformado |
| `401` / `403` | — / `iam.management.forbidden` | token / sin `Users:Update` |
| `404` | `iam.users.not_found` / `iam.roles.collection_not_found` | usuario o algún rol inexistente |
| `409` | `iam.roles.last_administrator_required` | dejaría al tenant sin administrador activo |
| `409` | `CONCURRENCY_CONFLICT` | weak ETag stale |

```json
{
  "userPublicId": "…",
  "email": "carlos@empresa.com",
  "firstName": "Carlos",
  "lastName": "Mejía",
  "isActive": true,
  "weakETag": "abc123…",
  "roles": [{ "publicId": "…", "name": "HR Admin", "description": "…", "isSystemRole": false }]
}
```

### Business Rules
- **Replace-all** con máx 200 roles.
- No se puede dejar a la compañía sin administrador activo (`409 last_administrator_required`).
- El usuario afectado ve sus permisos nuevos al refrescar su token (≤15 min) o re-loguearse.
- Coexiste con Company Users (Fase 4): elegí **una** puerta por pantalla para no confundir el
  estado de concurrencia (acá el token débil es independiente del ETag de Company Users).

### Validation Rules
Tabla del body.

### Security Considerations
`*` salta solo la verificación de concurrencia, **no** la de autorización — sigue exigiendo
`Users:Update`. Usá `*` solo en la primera asignación; después usá el `weakETag` devuelto.

---

# Role-builder catalog (repaso — endpoint de la Fase 2 §15)

`GET …/authorization/role-builder-catalog` → `AccountCompanyRoleBuilderCatalogResponse`. Es el
catálogo que alimenta el form de rol. Requiere ownership de la compañía (Fase 2). Estructura:

| Campo | Para qué |
|-------|----------|
| `availableModules` | módulos habilitados por el plan (agrupar la UI) |
| `availableCapabilities` | capacidades efectivas del plan |
| `availablePermissions` | **los permisos asignables** → su `publicId` va en `permissionPublicIds` |
| `fieldPoliciesCatalog` | catálogo de políticas por campo (para permisos `kind: Field`) |
| `scopeTypes` | tipos de alcance soportados |

Cada `availablePermissions[]` (`AccountCompanyAccessPermissionResponse`) trae: `publicId`, `code`,
`name`, `module`, `screen`, `kind` (`ScreenAction`/`Field`), `action`, `fieldName`, `fieldAccess`
(`Read`/`Write`), `capabilityCodes`, `isDormant`, `supportedScopeTypes`. **No ofrezcas en la UI los
`isDormant: true`** (no son otorgables aunque aparezcan).

---

# Referencia compartida

## Enums (wire, serializan como string)

| Enum | Valores | Dónde |
|------|---------|-------|
| `IamPermissionKind` | `ScreenAction` · `Field` | `grants[].kind`, `availablePermissions[].kind` |
| `IamFieldAccessLevel` | `Read` · `Write` | `grants[].fieldAccessState`, `availablePermissions[].fieldAccess` |
| Acciones RBAC | `Read` · `Create` · `Update` · `Delete` (entre otras) | `grants[].action` para `kind: ScreenAction` |

## Modelos de concurrencia (resumen)

| Recurso | Token | Header de lectura | Header de write | Stale |
|---------|-------|-------------------|-----------------|-------|
| Rol (PUT/PATCH/grants) | fuerte (GUID) | `ETag: "<guid>"` + body `concurrencyToken` | `If-Match: "<guid>"` | `409 CONCURRENCY_CONFLICT` |
| Roles de usuario (PUT) | débil (hash) | `ETag: W/"<hash>"` + body `weakETag` | `If-Match: W/"<hash>"` o `*` | `409 CONCURRENCY_CONFLICT` |

`DELETE` de rol y el `GET` de listado/búsqueda no usan `If-Match`.

## Catálogo de códigos de error de la fase

| `code` | HTTP | Cuándo | Acción FE |
|--------|------|--------|-----------|
| `common.validation` | 400 | validación / If-Match faltante | errores por campo |
| `iam.tenant.required` | 401 | sin tenant activo | hacer switch |
| `iam.current_user.invalid` | 401 | contexto de usuario inválido | re-login |
| (403 plano, sin body) | 403 | `companyPublicId` ≠ tenant activo | corregir routing / switch |
| `iam.management.forbidden` | 403 | sin el permiso de pantalla | ocultar acción |
| `iam.module.disabled_by_plan` | 403 | plan sin el módulo | upsell / ocultar |
| `iam.roles.protected_role.forbidden` | 403 | editar/parchear rol de sistema | marcar solo-lectura |
| `iam.roles.protected_role.delete_forbidden` | 403 | borrar rol de sistema | ocultar borrar |
| `iam.roles.not_found` | 404 | rol inexistente/otro tenant | volver a la lista |
| `iam.users.not_found` | 404 | usuario inexistente | recargar |
| `iam.roles.collection_not_found` | 404 | algún rol del set inexistente | recargar catálogo de roles |
| `iam.permissions.collection_not_found` | 404 | algún permiso inexistente | recargar role-builder-catalog |
| `iam.roles.name_conflict` | 409 | nombre de rol duplicado | pedir otro nombre |
| `iam.roles.in_use` | 409 | borrar rol con usuarios | reasignar usuarios primero |
| `iam.roles.last_administrator_required` | 409 | dejaría sin admin/seguridad | bloquear con advertencia |
| `CONCURRENCY_CONFLICT` | 409 | token (fuerte o débil) stale | recargar + reintentar |

## Límites

| Cosa | Valor |
|------|-------|
| `name` de rol | máx 100, único en el tenant |
| `description` de rol | máx 500 |
| `permissionPublicIds` por rol | máx 1000 |
| `rolePublicIds` por usuario | máx 200 |
| `pageSize` / `search` listado | 1–100 / máx 100 |
| JSON Patch | máx 50 ops / 64 KB |

Sin rate limits específicos (superficie admin-gated).

## Guía de implementación del cliente

1. **Gating por pantalla**: leé `access-context.currentUserAccess.permissions` (Fase 2 §13) y
   habilitá cada acción por su permiso (`Roles:Read/Create/Update/Delete`, `Permissions:Update`,
   `Users:Update`). Son granularidades independientes — no asumas que "puede ver roles" ⇒ "puede
   editar permisos".
2. **Form de rol**: cargá `role-builder-catalog` una vez por compañía; armá el selector de permisos
   desde `availablePermissions` (agrupá por `module`/`screen`, omití `isDormant`), y mandá los
   `publicId` elegidos en `permissionPublicIds`.
3. **Concurrencia mixta**: para roles usá el GUID (`concurrencyToken`/`ETag` fuerte); para
   roles-de-usuario usá el `weakETag`/`ETag: W/"…"`. No los mezcles. Reemplazá el token con el de
   cada respuesta; ante `409` recargá y reintentá.
4. **Roles de sistema**: render solo-lectura (sin editar/borrar/parchear).
5. **Borrado seguro**: chequeá `userCount` (o `allowedActions.canDelete`) antes de ofrecer borrar;
   manejá `iam.roles.in_use` por si cambió entre el render y el click.
6. **Invariantes de "último admin"**: tanto en grants (`Permissions:Update`) como en roles-de-usuario
   (`Users:Update`) el backend puede devolver `last_administrator_required` — mostralo como
   bloqueo explicado, no como error genérico.
7. **Una sola puerta de asignación de roles por pantalla**: elegí Company Users (Fase 4) **o** este
   `PUT users/{id}/roles`, no ambos en la misma vista (sus ETags son independientes).

## Próximas fases (orden sugerido)

1. **Suscripción** — `AccountCompanySubscriptionsController` (`/subscription`, `/plans`, `/addons`,
   `/preview`): el plan define qué módulos/capacidades aparecen en el role-builder-catalog y el
   access-context.
2. **General Catalogs** — catálogos de referencia transversales (países, bancos, tipos de
   documento, etc.) que alimentan los formularios de los módulos de negocio.
3. **Módulos de negocio** — Organización (Org Units, Work Centers, Cost Centers…), Personnel Files,
   Job Profiles, etc.
