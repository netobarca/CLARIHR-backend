# API Endpoints Reference

## Scope

Este documento describe el estado actual de la API.

Incluye:

- endpoints expuestos hoy por `CLARIHR.Api`
- request y response reales a nivel contrato
- flujo de backend por endpoint
- autorizacion y reglas multi-tenant vigentes
- contrato comun de errores

## Base conventions

### Base path

Todos los endpoints usan prefijo `/api`.

Excepcion controlada:

- el modulo de Locations introducido en HU-010 usa rutas bajo `/api/v1`

### Authentication

- Endpoints publicos:
  - `GET /api/system/status`
  - `POST /api/auth/register`
  - `POST /api/auth/external`
  - `POST /api/auth/refresh`
- Todo endpoint administrativo o de negocio requiere `Authorization: Bearer <jwt>`.
- Los endpoints protegidos esperan contexto de tenant via claim `tid`.
- Excepcion: `api/account/companies` sigue requiriendo JWT, pero su enforcement principal es por ownership de cuenta y no por RBAC tenant-scoped.

### Authorization model

La API es el guardia real. La UI no decide acceso.

Reglas vigentes:

1. Validar autenticacion.
2. Resolver tenant actual.
3. Validar permiso RBAC nivel 1 y 2 por `resourceKey` + `action`.
4. En updates sensibles, validar RBAC nivel 3 por campo.
5. Consultar y modificar siempre dentro del tenant actual.

Excepcion controlada:

- Los endpoints `api/account/companies` son `account-level`.
- No usan `AuthorizeResource(...)`.
- Validan ownership funcional por `CreatedByUserPublicId`.
- El `tid` del token solo se usa para marcar contexto activo y para bloquear el archivado de la empresa activa.

Excepcion controlada adicional:

- El modulo `Locations` no usa la matriz RBAC HU-005/HU-006 en esta iteracion.
- Usa validacion por claims/policies funcionales:
  - `Locations.Read`
  - `Locations.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Aun asi, sigue siendo tenant-scoped y exige coincidencia entre `companyId` de la ruta y claim `tid`.

Recursos RBAC actuales:

- `RBAC_USERS`
- `RBAC_ROLES`
- `RBAC_PERMISSIONS`
- `AUDIT_LOGS`

Acciones RBAC actuales:

- `Access`
- `Read`
- `Create`
- `Update`
- `Delete`

### JSON conventions

- Los enums se serializan como string.
- Ejemplos de enums actuales:
  - `AuthProvider`: `Local`, `Google`, `Microsoft`, `Apple`
  - `UserStatus`: `Active`, `Inactive`, `PendingActivation`
  - `IamPermissionKind`: `ScreenAction`, `Field`
  - `IamFieldAccessLevel`: `Read`, `Write`

### Success responses

- Las respuestas exitosas no usan envelope `success`.
- Las listas paginadas usan este contrato:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

### Error contract

Los errores se devuelven como `ProblemDetails`.

Formato base:

```json
{
  "title": "You do not have permission to perform this action.",
  "detail": "You do not have permission to perform this action.",
  "status": 403,
  "type": "https://httpstatuses.com/403",
  "code": "RBAC_DENIED",
  "traceId": "00-abc-123",
  "details": [
    {
      "resourceKey": "RBAC_USERS",
      "action": "Update",
      "fieldKey": "RBAC_USERS.ROLE",
      "endpoint": "/api/company/users/{userId}"
    }
  ]
}
```

Codigos relevantes hoy:

- `UNAUTHENTICATED`
- `RBAC_DENIED`
- `TENANT_MISMATCH`
- `FIELD_EDIT_FORBIDDEN`
- `COMPANY_LIMIT_REACHED`
- `COMPANY_NOT_FOUND`
- `COMPANY_OWNERSHIP_FORBIDDEN`
- `COMPANY_ALREADY_ARCHIVED`
- `COMPANY_ALREADY_ACTIVE`
- `ACTIVE_COMPANY_ARCHIVE_FORBIDDEN`
- `ACTIVE_COMPANY_SWITCH_FORBIDDEN`
- `COMPANY_REACTIVATION_LIMIT_REACHED`
- `LOCATION_HIERARCHY_NOT_FOUND`
- `LOCATION_LEVEL_NOT_FOUND`
- `LOCATION_GROUP_NOT_FOUND`
- `WORK_CENTER_TYPE_NOT_FOUND`
- `WORK_CENTER_NOT_FOUND`
- `LOCATION_LEVEL_ORDER_CONFLICT`
- `LOCATION_GROUP_CODE_CONFLICT`
- `WORK_CENTER_TYPE_CODE_CONFLICT`
- `WORK_CENTER_CODE_CONFLICT`
- `LOCATION_GROUP_PARENT_REQUIRED`
- `LOCATION_GROUP_INVALID_PARENT`
- `LOCATION_GROUP_CYCLE_DETECTED`
- `LOCATION_GROUP_HAS_ACTIVE_CHILDREN`
- `LOCATION_GROUP_HAS_ACTIVE_WORK_CENTERS`
- `WORK_CENTER_TYPE_IN_USE`
- `WORK_CENTER_HAS_ACTIVE_DEPENDENCIES`
- `DEFAULT_GROUP_PROTECTED`
- `LAST_ACTIVE_LEVEL_REQUIRED`
- `WORK_CENTERS_ALLOWED_ONLY_ON_LAST_LEVEL`
- `LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`
- `CONCURRENCY_CONFLICT`

Estados HTTP usados:

- `200 OK`
- `201 Created`
- `204 No Content`
- `400 Bad Request`
- `401 Unauthorized`
- `403 Forbidden`
- `404 Not Found`
- `409 Conflict`
- `422 Unprocessable Entity`
- `500 Internal Server Error`

## Reusable response schemas

### AuthResponse

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token-or-null",
  "expiresIn": 3600,
  "user": {
    "id": "guid",
    "email": "admin@company.com",
    "firstName": "Admin",
    "lastName": "User",
    "authProvider": "Local"
  }
}
```

### CompanyUserResponse

Nota: por RBAC nivel 3, algunos campos pueden venir ocultos, `null` o masked segun configuracion del rol del usuario autenticado.

```json
{
  "id": "guid",
  "email": "user@company.com",
  "firstName": "Jane",
  "lastName": "Doe",
  "roleId": "guid",
  "role": "Supervisor",
  "status": "PendingActivation"
}
```

### CompanyUserInvitationResponse

```json
{
  "user": {
    "id": "guid",
    "email": "user@company.com",
    "firstName": "Jane",
    "lastName": "Doe",
    "roleId": "guid",
    "role": "Supervisor",
    "status": "PendingActivation"
  },
  "invitationExpiresUtc": "2026-03-04T12:00:00Z"
}
```

### AccountCompanySummaryResponse

```json
{
  "companyId": "guid",
  "name": "Acme Services",
  "slug": "acme-services",
  "status": "Active",
  "planCode": "FREE",
  "isActiveContext": false,
  "isOwnedByCurrentUser": true,
  "createdAtUtc": "2026-03-01T10:00:00Z"
}
```

### AccountCompanyDetailResponse

```json
{
  "companyId": "guid",
  "name": "Acme Services",
  "slug": "acme-services",
  "status": "Active",
  "planCode": "FREE",
  "isActiveContext": false,
  "isOwnedByCurrentUser": true,
  "createdAtUtc": "2026-03-01T10:00:00Z",
  "modifiedAtUtc": "2026-03-01T11:00:00Z"
}
```

### SwitchActiveCompanyResponse

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "expiresIn": 3600,
  "activeCompany": {
    "companyId": "guid",
    "name": "Acme Services",
    "slug": "acme-services",
    "status": "Active"
  }
}
```

### LocationHierarchyConfigResponse

```json
{
  "id": "guid",
  "isMultiLevel": false,
  "defaultGroupCode": "GENERAL",
  "defaultGroupName": "General",
  "concurrencyToken": "guid"
}
```

### LocationLevelResponse

```json
{
  "id": "guid",
  "levelOrder": 1,
  "displayName": "General",
  "isActive": true,
  "isRequired": true,
  "allowsWorkCenters": true,
  "concurrencyToken": "guid"
}
```

### LocationGroupResponse

```json
{
  "id": "guid",
  "levelOrder": 1,
  "code": "GENERAL",
  "name": "General",
  "parentId": null,
  "description": "Default location group.",
  "isActive": true,
  "isDefault": true,
  "concurrencyToken": "guid",
  "createdAtUtc": "2026-03-01T10:00:00Z",
  "modifiedAtUtc": "2026-03-01T10:00:00Z"
}
```

### WorkCenterTypeResponse

```json
{
  "id": "guid",
  "code": "AGENCY",
  "name": "Agency",
  "requiresAddress": true,
  "requiresGeo": false,
  "allowsBiometric": true,
  "isActive": true,
  "concurrencyToken": "guid",
  "createdAtUtc": "2026-03-01T10:00:00Z",
  "modifiedAtUtc": "2026-03-01T10:00:00Z"
}
```

### WorkCenterResponse

```json
{
  "id": "guid",
  "code": "CEN-001",
  "name": "Centro General",
  "workCenterTypeId": "guid",
  "workCenterTypeCode": "AGENCY",
  "workCenterTypeName": "Agency",
  "locationGroupId": "guid",
  "locationGroupCode": "GENERAL",
  "locationGroupName": "General",
  "locationGroupLevelOrder": 1,
  "address": "San Salvador",
  "geoLat": 13.6929,
  "geoLong": -89.2182,
  "phone": "2222-2222",
  "email": "centro@company.com",
  "notes": "Notas",
  "isActive": true,
  "concurrencyToken": "guid",
  "createdAtUtc": "2026-03-01T10:00:00Z",
  "modifiedAtUtc": "2026-03-01T10:00:00Z"
}
```

### IamUserResponse

```json
{
  "id": "guid",
  "email": "user@company.com",
  "firstName": "Jane",
  "lastName": "Doe",
  "isActive": true,
  "roles": [
    {
      "id": "guid",
      "name": "Tenant Admin",
      "description": "Admin role"
    }
  ]
}
```

### IamRoleResponse

```json
{
  "id": "guid",
  "name": "Tenant Admin",
  "description": "Admin role",
  "isSystemRole": true,
  "userCount": 2,
  "permissions": [
    {
      "id": "guid",
      "code": "RBAC.USERS.READ",
      "name": "Users Read",
      "description": "Allows read operations on the Users screen.",
      "module": "RBAC",
      "screen": "Users",
      "kind": "ScreenAction",
      "action": "Read",
      "fieldName": null,
      "fieldAccess": null
    }
  ]
}
```

### IamPermissionResponse

```json
{
  "id": "guid",
  "code": "employees.profile.salary.write",
  "name": "Write salary field",
  "description": "Allows editing salary",
  "module": "Employees",
  "screen": "Profile",
  "kind": "Field",
  "action": null,
  "fieldName": "Salary",
  "fieldAccess": "Write"
}
```

### RolePermissionMatrixResponse

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "isSystemRole": false,
  "screens": [
    {
      "resourceKey": "RBAC_USERS",
      "displayName": "Users",
      "module": "RBAC",
      "screen": "Users",
      "managedByOverride": true,
      "access": {
        "supported": true,
        "granted": true,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.ACCESS"
      },
      "read": {
        "supported": true,
        "granted": true,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.READ"
      },
      "create": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.CREATE"
      },
      "update": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.UPDATE"
      },
      "delete": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.DELETE"
      }
    }
  ]
}
```

### RbacRolePermissionsResponse

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "isSystemRole": false,
  "permissions": [
    {
      "resourceKey": "RBAC_USERS",
      "displayName": "Users",
      "hasAccess": true,
      "canRead": true,
      "canCreate": false,
      "canUpdate": false,
      "canDelete": false
    }
  ]
}
```

### RoleFieldPermissionsResponse

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "resourceKey": "RBAC_USERS",
  "fields": [
    {
      "fieldKey": "RBAC_USERS.EMAIL",
      "propertyName": "Email",
      "displayName": "Email",
      "dataType": "string",
      "isSensitive": true,
      "isVisible": true,
      "isEditable": false,
      "isRequired": false,
      "isMasked": false,
      "isReadOnly": true
    }
  ]
}
```

### AuditLogSummaryResponse

```json
{
  "id": "guid",
  "createdAtUtc": "2026-03-01T10:00:00Z",
  "actorUserId": "guid",
  "actorEmail": "admin@company.com",
  "eventType": "ROLE_RESOURCE_PERMISSIONS_UPDATED",
  "entityType": "Permission",
  "entityId": null,
  "entityKey": "RBAC_USERS",
  "action": "PermissionChange",
  "summary": "Updated role permissions for RBAC_USERS",
  "diff": {
    "RBAC_USERS": {
      "canUpdate": {
        "before": false,
        "after": true
      }
    }
  }
}
```

## Endpoint catalog

## 1. System

### GET /api/system/status

- Auth: publico
- RBAC: no aplica
- Request body: none
- Query params: none

Response `200 OK`:

```json
{
  "applicationName": "CLARIHR.Api",
  "utcNow": "2026-03-01T10:00:00Z",
  "tenantId": "guid-or-null",
  "userId": "guid-or-null",
  "isAuthenticated": true
}
```

Flow:

1. Lee el principal actual.
2. Resuelve si hay usuario autenticado y tenant.
3. Devuelve estado operativo basico del API.

Main errors:

- `400`
- `500`

## 2. Auth

### POST /api/auth/register

- Auth: publico
- RBAC: no aplica

Request:

```json
{
  "firstName": "Admin",
  "lastName": "User",
  "email": "admin@company.com",
  "password": "StrongPass123!",
  "companyName": "Acme Inc",
  "country": "SV",
  "source": "landing"
}
```

Response `201 Created`: `AuthResponse`

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "expiresIn": 3600,
  "user": {
    "id": "guid",
    "email": "admin@company.com",
    "firstName": "Admin",
    "lastName": "User",
    "authProvider": "Local"
  }
}
```

Flow:

1. Valida payload de registro.
2. Crea usuario local.
3. Provisiona tenant inicial si aplica.
4. Emite access token y refresh token.
5. Devuelve sesion autenticada.

Main errors:

- `400`
- `409`
- `500`

### POST /api/auth/external

- Auth: publico
- RBAC: no aplica

Request:

```json
{
  "provider": "Google",
  "idToken": "external-provider-id-token",
  "companyName": "Acme Inc",
  "country": "SV",
  "source": "google-oauth"
}
```

Response:

- `201 Created` si el usuario fue creado
- `200 OK` si el usuario ya existia y solo se autentico

Body de respuesta: `AuthResponse`

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "expiresIn": 3600,
  "user": {
    "id": "guid",
    "email": "admin@company.com",
    "firstName": "Admin",
    "lastName": "User",
    "authProvider": "Google"
  }
}
```

Flow:

1. Valida proveedor e `idToken`.
2. Verifica identidad contra el proveedor externo.
3. Si el usuario no existe, lo crea y provisiona tenant si corresponde.
4. Si ya existe, reusa el usuario.
5. Emite access token y refresh token.

Main errors:

- `400`
- `401`
- `409`
- `422`
- `500`

### POST /api/auth/refresh

- Auth: publico
- RBAC: no aplica

Request:

```json
{
  "refreshToken": "refresh-token"
}
```

Response `200 OK`: `AuthResponse`

```json
{
  "accessToken": "new-jwt",
  "refreshToken": "new-refresh-token-or-same-policy",
  "expiresIn": 3600,
  "user": {
    "id": "guid",
    "email": "admin@company.com",
    "firstName": "Admin",
    "lastName": "User",
    "authProvider": "Local"
  }
}
```

Flow:

1. Valida refresh token recibido.
2. Comprueba vigencia y pertenencia del token.
3. Emite un nuevo access token.
4. Devuelve sesion renovada.

Main errors:

- `400`
- `401`
- `500`

## 3. Account Companies

Estos endpoints permiten gestionar empresas propias a nivel cuenta sin mezclar ese flujo con RBAC tenant-scoped.

Notas:

- Requieren JWT.
- No usan `AuthorizeResource(...)`.
- Aplican ownership funcional sobre empresas creadas por el usuario autenticado.
- `slug` es inmutable en esta version.
- La baja es logica por `Archived`.
- El limite temporal actual es configurable y por defecto permite la empresa inicial mas una adicional activa.

### GET /api/account/companies

- Auth: JWT requerido
- RBAC: no aplica, ownership account-level

Query params:

- `status`: `Active`, `Suspended`, `Archived`
- `page`
- `pageSize`

Example:

`GET /api/account/companies?status=Active&page=1&pageSize=20`

Response `200 OK`: `PagedResponse<AccountCompanySummaryResponse>`

Flow:

1. Resuelve el usuario actual.
2. Busca empresas creadas por ese usuario.
3. Marca `isActiveContext` comparando con el `tid` actual del JWT.
4. Devuelve listado paginado.

Main errors:

- `400`
- `401`
- `500`

### GET /api/account/companies/{companyId}

- Auth: JWT requerido
- RBAC: no aplica, ownership account-level

Response `200 OK`: `AccountCompanyDetailResponse`

Flow:

1. Resuelve el usuario actual.
2. Verifica si la empresa existe.
3. Si existe pero no pertenece al usuario, devuelve `COMPANY_OWNERSHIP_FORBIDDEN`.
4. Si no existe, devuelve `COMPANY_NOT_FOUND`.
5. Si pertenece al usuario, devuelve detalle.

Main errors:

- `401`
- `403`
- `404`
- `500`

### POST /api/account/companies

- Auth: JWT requerido
- RBAC: no aplica, ownership account-level

Request:

```json
{
  "name": "Acme Services"
}
```

Response `201 Created`: `AccountCompanyDetailResponse`

Flow:

1. Resuelve usuario actual.
2. Valida el limite temporal de empresas activas permitidas para esa cuenta.
3. Provisiona tenant nuevo reutilizando el provisioning actual.
4. Crea membership no primaria.
5. Emite auditoria `COMPANY_CREATED`.
6. Devuelve la nueva empresa sin cambiar el contexto activo de la sesion.

Main errors:

- `400`
- `401`
- `409`
- `500`

### PUT /api/account/companies/{companyId}

- Auth: JWT requerido
- RBAC: no aplica, ownership account-level

Request:

```json
{
  "name": "Acme Services Group"
}
```

Response `200 OK`: `AccountCompanyDetailResponse`

Flow:

1. Resuelve usuario actual.
2. Valida ownership.
3. Cambia solo `name`.
4. Mantiene `slug` sin cambios.
5. Registra `COMPANY_UPDATED`.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

### PATCH /api/account/companies/{companyId}/archive

- Auth: JWT requerido
- RBAC: no aplica, ownership account-level

Response `200 OK`: `AccountCompanyDetailResponse`

Flow:

1. Resuelve usuario actual.
2. Valida ownership.
3. Bloquea el archivado si la empresa coincide con la empresa activa del token o con la membership primaria actual.
4. Cambia estado a `Archived`.
5. Registra `COMPANY_ARCHIVED`.

Main errors:

- `401`
- `403`
- `404`
- `409`
- `500`

### PATCH /api/account/companies/{companyId}/reactivate

- Auth: JWT requerido
- RBAC: no aplica, ownership account-level

Response `200 OK`: `AccountCompanyDetailResponse`

Flow:

1. Resuelve usuario actual.
2. Valida ownership.
3. Verifica que la empresa este archivada.
4. Revalida el limite temporal de empresas activas.
5. Cambia estado a `Active`.
6. Registra `COMPANY_REACTIVATED`.

Main errors:

- `401`
- `403`
- `404`
- `409`
- `500`

### POST /api/account/companies/{companyId}/switch

- Auth: JWT requerido
- RBAC: no aplica, ownership account-level

Response `200 OK`: `SwitchActiveCompanyResponse`

Flow:

1. Resuelve usuario actual.
2. Valida ownership.
3. Verifica que la empresa destino este activa.
4. Verifica membership activa del usuario en esa empresa.
5. Actualiza la membership primaria.
6. Reemite JWT con nuevo `tid`.
7. Registra `ACTIVE_COMPANY_SWITCHED`.

Main errors:

- `401`
- `403`
- `404`
- `409`
- `500`

## 3B. Locations

Este modulo fue agregado en HU-010 y usa rutas bajo `/api/v1`.

Reglas globales del modulo:

- Auth: JWT requerido
- Seguridad: claims/policies de Locations, no matriz RBAC HU-005/HU-006
- Tenant: `companyId` de la ruta debe coincidir con `tid`
- Concurrencia: updates con `concurrencyToken`
- Provisioning: toda empresa nueva nace con config, nivel y grupo default `General`

### GET /api/v1/companies/{companyId}/location-hierarchy

- Auth: JWT requerido
- Authorization: `Locations.Read` o `Locations.Admin`

Response `200 OK`: `LocationHierarchyConfigResponse`

Flow:

1. Valida JWT.
2. Valida `companyId` contra `tid`.
3. Autoriza lectura de Locations.
4. Carga configuracion tenant-scoped.
5. Devuelve la config actual.

Main errors:

- `401`
- `403`
- `404`

### PUT /api/v1/companies/{companyId}/location-hierarchy

- Auth: JWT requerido
- Authorization: `Locations.Admin`

Request:

```json
{
  "isMultiLevel": true,
  "concurrencyToken": "guid"
}
```

Response `200 OK`: `LocationHierarchyConfigResponse`

Main errors:

- `401`
- `403`
- `404`
- `409`

### GET /api/v1/companies/{companyId}/location-levels

- Auth: JWT requerido
- Authorization: `Locations.Read` o `Locations.Admin`

Response `200 OK`: `IReadOnlyCollection<LocationLevelResponse>`

### POST /api/v1/companies/{companyId}/location-levels

- Auth: JWT requerido
- Authorization: `Locations.Admin`

Request:

```json
{
  "levelOrder": 2,
  "displayName": "Region",
  "isActive": true,
  "isRequired": false,
  "allowsWorkCenters": false
}
```

Response `201 Created`: `LocationLevelResponse`

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`

### PUT /api/v1/location-levels/{id}
### PATCH /api/v1/location-levels/{id}/activate
### PATCH /api/v1/location-levels/{id}/inactivate

- Auth: JWT requerido
- Authorization: `Locations.Admin`
- Concurrencia: `concurrencyToken` requerido

### GET /api/v1/companies/{companyId}/location-groups/tree
### GET /api/v1/companies/{companyId}/location-groups

- Auth: JWT requerido
- Authorization: `Locations.Read` o `Locations.Admin`

Busqueda soportada:

- `levelOrder?`
- `isActive?`
- `q?`
- `page`
- `pageSize`

### POST /api/v1/companies/{companyId}/location-groups

- Auth: JWT requerido
- Authorization: `Locations.Admin`

Request:

```json
{
  "levelOrder": 1,
  "code": "WEST",
  "name": "West",
  "parentId": null,
  "description": "Western location group"
}
```

Response `201 Created`: `LocationGroupResponse`

### PUT /api/v1/location-groups/{id}
### PATCH /api/v1/location-groups/{id}/move
### PATCH /api/v1/location-groups/{id}/activate
### PATCH /api/v1/location-groups/{id}/inactivate

- Auth: JWT requerido
- Authorization: `Locations.Admin`
- Reglas criticas:
  - padre del nivel inmediato superior
  - grupo default protegido
  - inactivacion bloqueada con hijos o centros activos

### GET /api/v1/companies/{companyId}/work-center-types
### POST /api/v1/companies/{companyId}/work-center-types
### PUT /api/v1/work-center-types/{id}
### PATCH /api/v1/work-center-types/{id}/activate
### PATCH /api/v1/work-center-types/{id}/inactivate

- Auth: JWT requerido
- Authorization: `Locations.Read` o `Locations.Admin` para lectura; `Locations.Admin` para escritura
- Reglas criticas:
  - `code` unico por tenant
  - inactivacion bloqueada si hay centros activos usando el tipo

### GET /api/v1/companies/{companyId}/work-centers
### GET /api/v1/work-centers/{id}
### POST /api/v1/companies/{companyId}/work-centers
### PUT /api/v1/work-centers/{id}
### PATCH /api/v1/work-centers/{id}/reassign-group
### PATCH /api/v1/work-centers/{id}/activate
### PATCH /api/v1/work-centers/{id}/inactivate

- Auth: JWT requerido
- Authorization: `Locations.Read` o `Locations.Admin` para lectura; `Locations.Admin` para escritura
- Reglas criticas:
  - tipo y grupo deben pertenecer al tenant
  - el grupo debe estar en un nivel que permita centros
  - direccion y geo son obligatorias segun flags del tipo
  - inactivacion usa `ILocationDependencyPolicy`

## 4. Company Users

Todos estos endpoints son tenant-scoped y aplican RBAC real en backend.

Notas:

- Las lecturas filtran visibilidad de campos por HU-006.
- Los updates validan editabilidad por campo.
- El `CompanyId` nunca llega en request.
- Todos los cambios relevantes generan auditoria administrativa.

### GET /api/company/users

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Read`

Query params:

- `page` default `1`
- `pageSize` default `20`
- `status` optional: `Active`, `Inactive`, `PendingActivation`
- `roleId` optional: `guid`
- `search` optional: texto

Ejemplo:

`GET /api/company/users?page=1&pageSize=20&status=Active&search=jane`

Response `200 OK`: `PagedResponse<CompanyUserSummaryResponse>`

```json
{
  "items": [
    {
      "id": "guid",
      "email": "jane@company.com",
      "firstName": "Jane",
      "lastName": "Doe",
      "roleId": "guid",
      "role": "Supervisor",
      "status": "Active"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Read`.
3. Consulta solo usuarios del tenant actual.
4. Obtiene perfil de permisos por campo del usuario autenticado.
5. Filtra o mascara campos no visibles.
6. Retorna respuesta paginada.

Main errors:

- `400`
- `401`
- `403`

### POST /api/company/users

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Create`

Request:

```json
{
  "email": "jane@company.com",
  "firstName": "Jane",
  "lastName": "Doe",
  "roleId": "guid"
}
```

Response `201 Created`: `CompanyUserInvitationResponse`

```json
{
  "user": {
    "id": "guid",
    "email": "jane@company.com",
    "firstName": "Jane",
    "lastName": "Doe",
    "roleId": "guid",
    "role": "Supervisor",
    "status": "PendingActivation"
  },
  "invitationExpiresUtc": "2026-03-04T12:00:00Z"
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Create`.
3. Valida permisos RBAC nivel 3 para los campos del payload.
4. Verifica que el rol pertenezca al tenant actual.
5. Crea usuario invitado y membresia de empresa.
6. Genera token de invitacion.
7. Registra auditoria `USER_INVITED`.
8. Devuelve usuario y expiracion de invitacion.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### PUT /api/company/users/{userId}

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Update`

Request:

```json
{
  "firstName": "Jane",
  "lastName": "Smith",
  "roleId": "guid"
}
```

Response `200 OK`: `CompanyUserResponse`

```json
{
  "id": "guid",
  "email": "jane@company.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "roleId": "guid",
  "role": "Supervisor",
  "status": "Active"
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Update`.
3. Verifica que el usuario objetivo pertenezca al tenant actual.
4. Determina campos modificados.
5. Valida RBAC nivel 3 sobre campos editables.
6. Aplica cambios permitidos.
7. Registra auditoria `USER_UPDATED`.
8. Devuelve el usuario actualizado filtrado por permisos de visibilidad.

Main errors:

- `400`
- `401`
- `403` `RBAC_DENIED`
- `403` `TENANT_MISMATCH`
- `403` `FIELD_EDIT_FORBIDDEN`
- `404`
- `409`
- `500`

### PATCH /api/company/users/{userId}/deactivate

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Update`
- Request body: none

Response `200 OK`: `CompanyUserResponse`

```json
{
  "id": "guid",
  "email": "jane@company.com",
  "firstName": "Jane",
  "lastName": "Doe",
  "roleId": "guid",
  "role": "Supervisor",
  "status": "Inactive"
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Update`.
3. Verifica pertenencia del usuario al tenant actual.
4. Desactiva membresia y acceso.
5. Invalida sesiones o tokens relacionados cuando corresponde.
6. Registra auditoria `USER_DEACTIVATED`.
7. Devuelve estado actualizado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### PATCH /api/company/users/{userId}/reactivate

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Update`
- Request body: none

Response `200 OK`: `CompanyUserResponse`

```json
{
  "id": "guid",
  "email": "jane@company.com",
  "firstName": "Jane",
  "lastName": "Doe",
  "roleId": "guid",
  "role": "Supervisor",
  "status": "Active"
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Update`.
3. Verifica pertenencia del usuario al tenant actual.
4. Reactiva membresia y acceso.
5. Registra auditoria `USER_REACTIVATED`.
6. Devuelve estado actualizado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### POST /api/company/users/{userId}/reset-invitation

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Update`
- Request body: none

Response `200 OK`: `CompanyUserInvitationResponse`

```json
{
  "user": {
    "id": "guid",
    "email": "jane@company.com",
    "firstName": "Jane",
    "lastName": "Doe",
    "roleId": "guid",
    "role": "Supervisor",
    "status": "PendingActivation"
  },
  "invitationExpiresUtc": "2026-03-07T12:00:00Z"
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Update`.
3. Verifica pertenencia del usuario al tenant actual.
4. Revoca invitaciones anteriores.
5. Emite nueva invitacion.
6. Registra auditoria `USER_INVITATION_RESET`.
7. Devuelve nueva expiracion.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

## 5. IAM Users

Estos endpoints administran usuarios IAM del tenant actual.

### POST /api/iam/users

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Create`

Request:

```json
{
  "firstName": "Carla",
  "lastName": "Lopez",
  "email": "carla@tenant.com",
  "isActive": true,
  "roleIds": [
    "guid"
  ]
}
```

Response `201 Created`: `IamUserResponse`

```json
{
  "id": "guid",
  "email": "carla@tenant.com",
  "firstName": "Carla",
  "lastName": "Lopez",
  "isActive": true,
  "roles": [
    {
      "id": "guid",
      "name": "Tenant Admin",
      "description": "Admin role"
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Create`.
3. Verifica unicidad de email dentro del tenant.
4. Resuelve y valida roles del tenant.
5. Crea usuario IAM.
6. Registra auditoria administrativa.
7. Devuelve `201` con `Location` a `GET /api/iam/users/{userId}`.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### GET /api/iam/users

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Read`

Query params:

- `pageNumber` default `1`
- `pageSize` default `20`
- `search` optional

Ejemplo:

`GET /api/iam/users?pageNumber=1&pageSize=20&search=carla`

Response `200 OK`: `PagedResponse<IamUserSummaryResponse>`

```json
{
  "items": [
    {
      "id": "guid",
      "email": "carla@tenant.com",
      "firstName": "Carla",
      "lastName": "Lopez",
      "isActive": true,
      "roleCount": 1
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Read`.
3. Consulta usuarios IAM solo del tenant actual.
4. Devuelve lista paginada.

Main errors:

- `400`
- `401`
- `403`
- `500`

### GET /api/iam/users/{userId}

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Read`

Request body: none

Response `200 OK`: `IamUserResponse`

```json
{
  "id": "guid",
  "email": "carla@tenant.com",
  "firstName": "Carla",
  "lastName": "Lopez",
  "isActive": true,
  "roles": [
    {
      "id": "guid",
      "name": "Tenant Admin",
      "description": "Admin role"
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Read`.
3. Busca usuario dentro del tenant actual.
4. Si existe fuera del tenant, puede responder `TENANT_MISMATCH`.
5. Devuelve detalle del usuario.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

### PUT /api/iam/users/{userId}/roles

- Auth: JWT requerido
- RBAC: `RBAC_USERS` + `Update`

Request:

```json
{
  "roleIds": [
    "guid",
    "guid"
  ]
}
```

Response `200 OK`: `IamUserResponse`

```json
{
  "id": "guid",
  "email": "carla@tenant.com",
  "firstName": "Carla",
  "lastName": "Lopez",
  "isActive": true,
  "roles": [
    {
      "id": "guid",
      "name": "Supervisor",
      "description": "Editable role"
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_USERS:Update`.
3. Verifica usuario objetivo dentro del tenant.
4. Valida que todos los roles pertenezcan al mismo tenant.
5. Sincroniza asignaciones.
6. Registra auditoria administrativa.
7. Devuelve usuario con roles actualizados.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

## 6. IAM Roles

### POST /api/iam/roles

- Auth: JWT requerido
- RBAC: `RBAC_ROLES` + `Create`

Request:

```json
{
  "name": "Tenant Admin",
  "description": "Administra seguridad del tenant",
  "permissionIds": [
    "guid"
  ]
}
```

Response `201 Created`: `IamRoleResponse`

```json
{
  "id": "guid",
  "name": "Tenant Admin",
  "description": "Administra seguridad del tenant",
  "isSystemRole": false,
  "userCount": 0,
  "permissions": [
    {
      "id": "guid",
      "code": "RBAC.USERS.READ",
      "name": "Users Read",
      "description": "Allows read operations on the Users screen.",
      "module": "RBAC",
      "screen": "Users",
      "kind": "ScreenAction",
      "action": "Read",
      "fieldName": null,
      "fieldAccess": null
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Create`.
3. Verifica unicidad del nombre en el tenant.
4. Resuelve permisos iniciales dentro del tenant.
5. Crea rol.
6. Registra auditoria `ROLE_CREATED`.
7. Devuelve `201` con `Location` a `GET /api/iam/roles/{roleId}`.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### GET /api/iam/roles

- Auth: JWT requerido
- RBAC: `RBAC_ROLES` + `Read`

Query params:

- `pageNumber` default `1`
- `pageSize` default `20`
- `search` optional

Response `200 OK`: `PagedResponse<IamRoleSummaryResponse>`

```json
{
  "items": [
    {
      "id": "guid",
      "name": "Supervisor",
      "description": "Editable role",
      "isSystemRole": false,
      "permissionCount": 5,
      "userCount": 3
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Read`.
3. Consulta roles del tenant actual.
4. Devuelve lista paginada.

Main errors:

- `400`
- `401`
- `403`
- `500`

### GET /api/iam/roles/{roleId}

- Auth: JWT requerido
- RBAC: `RBAC_ROLES` + `Read`
- Request body: none

Response `200 OK`: `IamRoleResponse`

```json
{
  "id": "guid",
  "name": "Supervisor",
  "description": "Editable role",
  "isSystemRole": false,
  "userCount": 3,
  "permissions": [
    {
      "id": "guid",
      "code": "RBAC.USERS.READ",
      "name": "Users Read",
      "description": "Allows read operations on the Users screen.",
      "module": "RBAC",
      "screen": "Users",
      "kind": "ScreenAction",
      "action": "Read",
      "fieldName": null,
      "fieldAccess": null
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Read`.
3. Busca rol dentro del tenant actual.
4. Devuelve detalle del rol y sus permisos asignados.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

### PUT /api/iam/roles/{roleId}

- Auth: JWT requerido
- RBAC: `RBAC_ROLES` + `Update`

Request:

```json
{
  "name": "Supervisor",
  "description": "Rol editable del tenant"
}
```

Response `200 OK`: `IamRoleResponse`

```json
{
  "id": "guid",
  "name": "Supervisor",
  "description": "Rol editable del tenant",
  "isSystemRole": false,
  "userCount": 3,
  "permissions": []
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Update`.
3. Verifica que el rol pertenezca al tenant actual.
4. Rechaza modificacion de roles protegidos del sistema cuando aplique.
5. Actualiza nombre y descripcion.
6. Registra auditoria `ROLE_UPDATED`.
7. Devuelve rol actualizado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### POST /api/iam/roles/{roleId}/clone

- Auth: JWT requerido
- RBAC: `RBAC_ROLES` + `Create`

Request:

```json
{
  "name": "Supervisor Copy",
  "description": "Clon editable para ajustes"
}
```

Response `201 Created`: `IamRoleResponse`

```json
{
  "id": "guid",
  "name": "Supervisor Copy",
  "description": "Clon editable para ajustes",
  "isSystemRole": false,
  "userCount": 0,
  "permissions": [
    {
      "id": "guid",
      "code": "RBAC.USERS.READ",
      "name": "Users Read",
      "description": "Allows read operations on the Users screen.",
      "module": "RBAC",
      "screen": "Users",
      "kind": "ScreenAction",
      "action": "Read",
      "fieldName": null,
      "fieldAccess": null
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Create`.
3. Busca rol origen en el tenant actual.
4. Duplica metadatos y permisos del rol origen.
5. Registra auditoria `ROLE_CLONED`.
6. Devuelve `201` con el nuevo rol.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### GET /api/iam/roles/{roleId}/permission-matrix

- Auth: JWT requerido
- RBAC endpoint: `RBAC_ROLES` + `Read`
- Validacion adicional del handler: `RBAC_PERMISSIONS` + `Read`

Request body: none

Response `200 OK`: `RolePermissionMatrixResponse`

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "isSystemRole": false,
  "screens": [
    {
      "resourceKey": "RBAC_USERS",
      "displayName": "Users",
      "module": "RBAC",
      "screen": "Users",
      "managedByOverride": true,
      "access": {
        "supported": true,
        "granted": true,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.ACCESS"
      },
      "read": {
        "supported": true,
        "granted": true,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.READ"
      },
      "create": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.CREATE"
      },
      "update": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.UPDATE"
      },
      "delete": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.DELETE"
      }
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza a nivel endpoint `RBAC_ROLES:Read`.
3. Autoriza a nivel handler `RBAC_PERMISSIONS:Read`.
4. Carga rol y permisos catalogados.
5. Construye matriz por pantalla y accion.
6. Devuelve snapshot consolidado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

### PUT /api/iam/roles/{roleId}/permission-matrix

- Auth: JWT requerido
- RBAC endpoint: `RBAC_ROLES` + `Update`
- Validacion adicional del handler: `RBAC_PERMISSIONS` + `Update`

Request:

Nota: el campo `screen` acepta nombre de pantalla legacy o `resourceKey`. Hoy los valores validos son `Users`, `Roles`, `Permissions`, `AuditLogs`, o sus resource keys `RBAC_USERS`, `RBAC_ROLES`, `RBAC_PERMISSIONS`, `AUDIT_LOGS`.

```json
{
  "screens": [
    {
      "screen": "RBAC_USERS",
      "access": true,
      "read": true,
      "create": true,
      "update": false,
      "delete": false
    },
    {
      "screen": "RBAC_ROLES",
      "access": true,
      "read": true,
      "create": false,
      "update": false,
      "delete": false
    }
  ]
}
```

Response `200 OK`: `RolePermissionMatrixResponse`

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "isSystemRole": false,
  "screens": [
    {
      "resourceKey": "RBAC_USERS",
      "displayName": "Users",
      "module": "RBAC",
      "screen": "Users",
      "managedByOverride": true,
      "access": {
        "supported": true,
        "granted": true,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.ACCESS"
      },
      "read": {
        "supported": true,
        "granted": true,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.READ"
      },
      "create": {
        "supported": true,
        "granted": true,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.CREATE"
      },
      "update": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.UPDATE"
      },
      "delete": {
        "supported": true,
        "granted": false,
        "permissionId": "guid",
        "permissionCode": "RBAC.USERS.DELETE"
      }
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza a nivel endpoint `RBAC_ROLES:Update`.
3. Autoriza a nivel handler `RBAC_PERMISSIONS:Update`.
4. Valida pantallas y acciones soportadas por el catalogo.
5. Crea permisos faltantes del catalogo si es necesario.
6. Aplica overrides por rol.
7. Registra auditoria administrativa y auditoria RBAC de matriz.
8. Devuelve matriz resultante.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### DELETE /api/iam/roles/{roleId}

- Auth: JWT requerido
- RBAC: `RBAC_ROLES` + `Delete`
- Request body: none

Response `204 No Content`

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Delete`.
3. Verifica que el rol exista en el tenant actual.
4. Rechaza eliminacion de roles del sistema.
5. Rechaza eliminacion si el rol aun tiene usuarios asignados.
6. Elimina el rol.
7. No retorna body.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### PUT /api/iam/roles/{roleId}/permissions

- Auth: JWT requerido
- RBAC endpoint: `RBAC_ROLES` + `Update`
- Validacion adicional del handler: `RBAC_PERMISSIONS` + `Update`

Request:

```json
{
  "permissionIds": [
    "guid",
    "guid"
  ]
}
```

Response `200 OK`: `IamRoleResponse`

```json
{
  "id": "guid",
  "name": "Supervisor",
  "description": "Editable role",
  "isSystemRole": false,
  "userCount": 3,
  "permissions": [
    {
      "id": "guid",
      "code": "RBAC.USERS.READ",
      "name": "Users Read",
      "description": "Allows read operations on the Users screen.",
      "module": "RBAC",
      "screen": "Users",
      "kind": "ScreenAction",
      "action": "Read",
      "fieldName": null,
      "fieldAccess": null
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Update`.
3. Busca rol dentro del tenant actual.
4. Resuelve permisos recibidos dentro del tenant.
5. Sincroniza asignaciones del rol.
6. Registra auditoria RBAC de permisos matriciales cuando aplica.
7. Devuelve rol actualizado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

### PUT /api/iam/roles/{roleId}/users

- Auth: JWT requerido
- RBAC: `RBAC_ROLES` + `Update`

Request:

```json
{
  "userIds": [
    "guid",
    "guid"
  ]
}
```

Response `200 OK`: `IamRoleResponse`

```json
{
  "id": "guid",
  "name": "Supervisor",
  "description": "Editable role",
  "isSystemRole": false,
  "userCount": 2,
  "permissions": []
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_ROLES:Update`.
3. Busca rol dentro del tenant actual.
4. Valida que todos los usuarios pertenezcan al mismo tenant.
5. Sincroniza miembros del rol.
6. Devuelve rol actualizado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

## 7. IAM Permissions

### POST /api/iam/permissions

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Create`

Request para permiso `ScreenAction`:

```json
{
  "name": "Export employees",
  "description": "Allows employee export",
  "code": "employees.directory.export",
  "module": "Employees",
  "screen": "Directory",
  "kind": "ScreenAction",
  "action": "Export",
  "fieldName": null,
  "fieldAccess": null
}
```

Request para permiso `Field`:

```json
{
  "name": "Write salary field",
  "description": "Allows editing salary",
  "code": "employees.profile.salary.write",
  "module": "Employees",
  "screen": "Profile",
  "kind": "Field",
  "action": null,
  "fieldName": "Salary",
  "fieldAccess": "Write"
}
```

Reglas de request:

- Si `kind = ScreenAction`, `action` es obligatorio.
- Si `kind = Field`, `fieldName` y `fieldAccess` son obligatorios.
- Si `code` no se envia, el backend lo genera automaticamente.

Response `201 Created`: `IamPermissionResponse`

```json
{
  "id": "guid",
  "code": "employees.profile.salary.write",
  "name": "Write salary field",
  "description": "Allows editing salary",
  "module": "Employees",
  "screen": "Profile",
  "kind": "Field",
  "action": null,
  "fieldName": "Salary",
  "fieldAccess": "Write"
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Create`.
3. Valida combinacion de `kind`, `action`, `fieldName` y `fieldAccess`.
4. Normaliza o genera `code`.
5. Verifica unicidad por tenant.
6. Crea permiso.
7. Devuelve `201` con `Location` a `GET /api/iam/permissions/{permissionId}`.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### GET /api/iam/permissions

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Read`

Query params:

- `pageNumber` default `1`
- `pageSize` default `20`
- `search` optional

Response `200 OK`: `PagedResponse<IamPermissionSummaryResponse>`

```json
{
  "items": [
    {
      "id": "guid",
      "code": "RBAC.USERS.READ",
      "name": "Users Read",
      "description": "Allows read operations on the Users screen.",
      "module": "RBAC",
      "screen": "Users",
      "kind": "ScreenAction",
      "action": "Read",
      "fieldName": null,
      "fieldAccess": null
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Read`.
3. Consulta permisos del tenant actual.
4. Devuelve lista paginada.

Main errors:

- `400`
- `401`
- `403`
- `500`

### GET /api/iam/permissions/{permissionId}

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Read`
- Request body: none

Response `200 OK`: `IamPermissionResponse`

```json
{
  "id": "guid",
  "code": "RBAC.USERS.READ",
  "name": "Users Read",
  "description": "Allows read operations on the Users screen.",
  "module": "RBAC",
  "screen": "Users",
  "kind": "ScreenAction",
  "action": "Read",
  "fieldName": null,
  "fieldAccess": null
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Read`.
3. Busca permiso dentro del tenant actual.
4. Devuelve detalle o error segun pertenencia.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

## 8. RBAC

Estos endpoints exponen catalogos, permisos por recurso y permisos por campo.

### GET /api/rbac/resources

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Read`
- Request body: none

Response `200 OK`: `RbacResourcesResponse`

```json
{
  "items": [
    {
      "resourceKey": "RBAC_USERS",
      "displayName": "Users"
    },
    {
      "resourceKey": "RBAC_ROLES",
      "displayName": "Roles"
    },
    {
      "resourceKey": "RBAC_PERMISSIONS",
      "displayName": "Permissions"
    },
    {
      "resourceKey": "AUDIT_LOGS",
      "displayName": "Audit Logs"
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Read`.
3. Lee recursos activos desde BD.
4. Si no existen recursos en BD, usa el catalogo interno.
5. Devuelve lista de recursos RBAC.

Main errors:

- `400`
- `401`
- `403`
- `500`

### GET /api/rbac/resources/{resourceKey}/fields

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Read`

Request body: none

Response `200 OK`: `ResourceFieldsResponse`

```json
{
  "resourceKey": "RBAC_USERS",
  "fields": [
    {
      "fieldKey": "RBAC_USERS.EMAIL",
      "propertyName": "Email",
      "displayName": "Email",
      "dataType": "string",
      "isConfigurable": true,
      "isSensitive": true
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Read`.
3. Busca el catalogo de campos del recurso.
4. Devuelve solo campos configurables del recurso solicitado.

Nota actual:

- El catalogo de campos implementado hoy esta preparado y seeded para `RBAC_USERS`.

Main errors:

- `400`
- `401`
- `403`
- `500`

### GET /api/rbac/roles/{roleId}/permissions

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Read`
- Request body: none

Response `200 OK`: `RbacRolePermissionsResponse`

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "isSystemRole": false,
  "permissions": [
    {
      "resourceKey": "RBAC_USERS",
      "displayName": "Users",
      "hasAccess": true,
      "canRead": true,
      "canCreate": false,
      "canUpdate": false,
      "canDelete": false
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Read`.
3. Busca rol dentro del tenant actual.
4. Resuelve matriz L1 y L2 por recurso.
5. Devuelve snapshot consolidado del rol.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

### PUT /api/rbac/roles/{roleId}/permissions

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Update`

Request:

```json
{
  "permissions": [
    {
      "resourceKey": "RBAC_USERS",
      "hasAccess": true,
      "canRead": true,
      "canCreate": true,
      "canUpdate": false,
      "canDelete": false
    },
    {
      "resourceKey": "RBAC_ROLES",
      "hasAccess": true,
      "canRead": true,
      "canCreate": false,
      "canUpdate": false,
      "canDelete": false
    }
  ]
}
```

Response `200 OK`: `RbacRolePermissionsResponse`

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "isSystemRole": false,
  "permissions": [
    {
      "resourceKey": "RBAC_USERS",
      "displayName": "Users",
      "hasAccess": true,
      "canRead": true,
      "canCreate": true,
      "canUpdate": false,
      "canDelete": false
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Update`.
3. Verifica que el rol pertenezca al tenant actual.
4. Valida cada `resourceKey` contra el catalogo RBAC.
5. Aplica upsert de permisos por recurso y accion.
6. Invalida cache de autorizacion si aplica.
7. Registra auditoria administrativa y auditoria RBAC.
8. Devuelve estado final del rol.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### GET /api/rbac/roles/{roleId}/field-permissions?resourceKey={resourceKey}

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Read`

Ejemplo:

`GET /api/rbac/roles/{roleId}/field-permissions?resourceKey=RBAC_USERS`

Response `200 OK`: `RoleFieldPermissionsResponse`

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "resourceKey": "RBAC_USERS",
  "fields": [
    {
      "fieldKey": "RBAC_USERS.EMAIL",
      "propertyName": "Email",
      "displayName": "Email",
      "dataType": "string",
      "isSensitive": true,
      "isVisible": true,
      "isEditable": false,
      "isRequired": false,
      "isMasked": false,
      "isReadOnly": true
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Read`.
3. Verifica rol y recurso dentro del tenant.
4. Lee catalogo de campos y overrides por rol.
5. Calcula campos visibles, editables, required y masked.
6. Devuelve snapshot consolidado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### PUT /api/rbac/roles/{roleId}/field-permissions

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Update`

Request:

```json
{
  "resourceKey": "RBAC_USERS",
  "fields": [
    {
      "fieldKey": "RBAC_USERS.EMAIL",
      "isVisible": true,
      "isEditable": false,
      "isRequired": false,
      "isMasked": false
    },
    {
      "fieldKey": "RBAC_USERS.ROLE",
      "isVisible": true,
      "isEditable": true,
      "isRequired": true,
      "isMasked": false
    }
  ]
}
```

Response `200 OK`: `RoleFieldPermissionsResponse`

```json
{
  "roleId": "guid",
  "roleName": "Supervisor",
  "resourceKey": "RBAC_USERS",
  "fields": [
    {
      "fieldKey": "RBAC_USERS.EMAIL",
      "propertyName": "Email",
      "displayName": "Email",
      "dataType": "string",
      "isSensitive": true,
      "isVisible": true,
      "isEditable": false,
      "isRequired": false,
      "isMasked": false,
      "isReadOnly": true
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Update`.
3. Verifica que el rol pertenezca al tenant actual.
4. Verifica que cada `fieldKey` exista y pertenezca al `resourceKey`.
5. Normaliza reglas, por ejemplo `isVisible = false` fuerza `isEditable = false`.
6. Hace upsert de permisos por campo.
7. Invalida cache de permisos por rol.
8. Registra auditoria `ROLE_FIELD_PERMISSIONS_UPDATED`.
9. Devuelve estado final consolidado.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `409`
- `500`

### GET /api/rbac/audit

- Auth: JWT requerido
- RBAC: `RBAC_PERMISSIONS` + `Read`

Query params:

- `roleId` optional
- `resourceKey` optional
- `from` optional UTC date
- `to` optional UTC date
- `page` optional, default `1`
- `pageSize` optional, default `20`, max `100`

Ejemplo:

`GET /api/rbac/audit?roleId={roleId}&resourceKey=RBAC_USERS&from=2026-03-01T00:00:00Z&to=2026-03-01T23:59:59Z&page=1&pageSize=20`

Response `200 OK`: `PagedResponse<RbacPermissionAuditEntryResponse>`

```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1,
  "items": [
    {
      "id": 1,
      "companyId": "guid",
      "roleId": "guid",
      "resourceKey": "RBAC_USERS",
      "changedByUserId": "guid",
      "changeType": "Upsert",
      "before": {
        "hasAccess": false,
        "canRead": false,
        "canCreate": false,
        "canUpdate": false,
        "canDelete": false
      },
      "after": {
        "hasAccess": true,
        "canRead": true,
        "canCreate": true,
        "canUpdate": false,
        "canDelete": false
      },
      "changedAtUtc": "2026-03-01T10:00:00Z"
    }
  ]
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `RBAC_PERMISSIONS:Read`.
3. Filtra auditoria RBAC por tenant actual.
4. Aplica filtros opcionales de rol, recurso, rango de fechas y paginacion.
5. Devuelve historial paginado de cambios de matriz por recurso.

Main errors:

- `400`
- `401`
- `403`
- `500`

## 9. Audit Logs

Estos endpoints consultan la auditoria administrativa consolidada introducida en HU-008 y ampliada en HU-009 con eventos de lifecycle de empresa.

Reglas:

- Requieren `AUDIT_LOGS:Read`.
- Siempre filtran por tenant actual.
- Los logs son inmutables desde la aplicacion.

Valores utiles para filtros:

- `entityType`: `User`, `Role`, `Permission`
- `eventType`:
  - `USER_CREATED`
  - `USER_UPDATED`
  - `USER_DEACTIVATED`
  - `USER_REACTIVATED`
  - `USER_INVITED`
  - `USER_INVITATION_RESET`
  - `ROLE_CREATED`
  - `ROLE_UPDATED`
  - `ROLE_CLONED`
  - `ROLE_RESOURCE_PERMISSIONS_UPDATED`
  - `ROLE_FIELD_PERMISSIONS_UPDATED`

### GET /api/audit/logs

- Auth: JWT requerido
- RBAC: `AUDIT_LOGS` + `Read`

Query params:

- `fromUtc` optional UTC date
- `toUtc` optional UTC date
- `actorUserId` optional
- `entityType` optional
- `eventType` optional
- `search` optional
- `page` default `1`
- `pageSize` default `20`

Ejemplo:

`GET /api/audit/logs?fromUtc=2026-03-01T00:00:00Z&toUtc=2026-03-01T23:59:59Z&entityType=Role&eventType=ROLE_UPDATED&page=1&pageSize=20`

Response `200 OK`: `PagedResponse<AuditLogSummaryResponse>`

```json
{
  "items": [
    {
      "id": "guid",
      "createdAtUtc": "2026-03-01T10:00:00Z",
      "actorUserId": "guid",
      "actorEmail": "admin@company.com",
      "eventType": "ROLE_UPDATED",
      "entityType": "Role",
      "entityId": "guid",
      "entityKey": null,
      "action": "Update",
      "summary": "Updated role Supervisor",
      "diff": {
        "name": {
          "before": "Supervisor Base",
          "after": "Supervisor"
        }
      }
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `AUDIT_LOGS:Read`.
3. Filtra logs por tenant actual.
4. Normaliza `entityType` y `eventType`.
5. Aplica filtros de fecha, actor y search.
6. Devuelve resultado paginado.

Main errors:

- `400`
- `401`
- `403`
- `500`

### GET /api/audit/logs/{auditLogId}

- Auth: JWT requerido
- RBAC: `AUDIT_LOGS` + `Read`
- Request body: none

Response `200 OK`: `AuditLogDetailResponse`

```json
{
  "id": "guid",
  "companyId": "guid",
  "createdAtUtc": "2026-03-01T10:00:00Z",
  "actorUserId": "guid",
  "actorEmail": "admin@company.com",
  "eventType": "ROLE_FIELD_PERMISSIONS_UPDATED",
  "entityType": "Permission",
  "entityId": null,
  "entityKey": "RBAC_USERS",
  "action": "PermissionChange",
  "summary": "Updated role field permissions for RBAC_USERS",
  "before": {
    "fields": []
  },
  "after": {
    "fields": [
      {
        "fieldKey": "RBAC_USERS.EMAIL",
        "isVisible": true,
        "isEditable": false
      }
    ]
  },
  "diff": {
    "RBAC_USERS.EMAIL": {
      "isEditable": {
        "before": true,
        "after": false
      }
    }
  },
  "ipAddress": "203.0.113.10",
  "userAgent": "Mozilla/5.0"
}
```

Flow:

1. Valida JWT y tenant actual.
2. Autoriza `AUDIT_LOGS:Read`.
3. Busca log por id dentro del tenant actual.
4. Si existe fuera del tenant, responde `TENANT_MISMATCH`.
5. Devuelve detalle completo con `before`, `after` y `diff`.

Main errors:

- `400`
- `401`
- `403`
- `404`
- `500`

## Current endpoint inventory

Estado actual del API documentado en este archivo:

- `GET /api/system/status`
- `POST /api/auth/register`
- `POST /api/auth/external`
- `POST /api/auth/refresh`
- `GET /api/account/companies`
- `GET /api/account/companies/{companyId}`
- `POST /api/account/companies`
- `PUT /api/account/companies/{companyId}`
- `PATCH /api/account/companies/{companyId}/archive`
- `PATCH /api/account/companies/{companyId}/reactivate`
- `POST /api/account/companies/{companyId}/switch`
- `GET /api/company/users`
- `POST /api/company/users`
- `PUT /api/company/users/{userId}`
- `PATCH /api/company/users/{userId}/deactivate`
- `PATCH /api/company/users/{userId}/reactivate`
- `POST /api/company/users/{userId}/reset-invitation`
- `POST /api/iam/users`
- `GET /api/iam/users`
- `GET /api/iam/users/{userId}`
- `PUT /api/iam/users/{userId}/roles`
- `POST /api/iam/roles`
- `GET /api/iam/roles`
- `GET /api/iam/roles/{roleId}`
- `PUT /api/iam/roles/{roleId}`
- `POST /api/iam/roles/{roleId}/clone`
- `GET /api/iam/roles/{roleId}/permission-matrix`
- `PUT /api/iam/roles/{roleId}/permission-matrix`
- `DELETE /api/iam/roles/{roleId}`
- `PUT /api/iam/roles/{roleId}/permissions`
- `PUT /api/iam/roles/{roleId}/users`
- `POST /api/iam/permissions`
- `GET /api/iam/permissions`
- `GET /api/iam/permissions/{permissionId}`
- `GET /api/rbac/resources`
- `GET /api/rbac/resources/{resourceKey}/fields`
- `GET /api/rbac/roles/{roleId}/permissions`
- `PUT /api/rbac/roles/{roleId}/permissions`
- `GET /api/rbac/roles/{roleId}/field-permissions`
- `PUT /api/rbac/roles/{roleId}/field-permissions`
- `GET /api/rbac/audit`
- `GET /api/audit/logs`
- `GET /api/audit/logs/{auditLogId}`
