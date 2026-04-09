# Guia de integracion Frontend - Autorizacion por suscripcion, capacidades, roles y policies

## 1. Proposito

Este documento explica como debe integrarse frontend Core con la nueva superficie de autorizacion del backend.

Fuentes canonicas del contrato:

- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

Esta guia no reemplaza esos documentos. Su objetivo es dejar claro:

- que endpoints debe consumir frontend
- en que orden debe consumirlos
- que parte del payload debe usar para cada decision de UI
- que no debe inferir frontend por su cuenta

## 2. Principios obligatorios de integracion

Frontend debe trabajar con estas reglas:

- la suscripcion habilita `capacidades`, no solo modulos
- `effectiveCapabilities` es la fuente funcional de verdad
- `effectiveModules` se usa para navegacion y organizacion visual
- los roles del usuario son `multi-rol`
- la autorizacion siempre depende de la `compania activa`
- frontend no debe inferir acceso desde claims del JWT
- frontend no debe inferir acceso solo desde `roles`
- frontend no debe inferir acceso solo desde `effectiveModules`
- para acciones y campos, la fuente de verdad es `resourcePolicy`

## 3. Fuentes canonicas por necesidad

Frontend debe usar cada fuente para un objetivo especifico:

### 3.1 Navegacion principal

Usar:

- `accessContext.effectiveModules`

### 3.2 Habilitacion funcional dentro de un modulo

Usar:

- `accessContext.effectiveCapabilities`

### 3.3 Catalogo para construir o editar roles

Usar:

- `roleBuilderCatalog.availableModules`
- `roleBuilderCatalog.availableCapabilities`
- `roleBuilderCatalog.availablePermissions`
- `roleBuilderCatalog.fieldPoliciesCatalog`
- `roleBuilderCatalog.scopeTypes`

### 3.4 Acciones de pantalla y estados de campos

Usar:

- `resourcePolicy.actions`
- `resourcePolicy.fields`

### 3.5 Roles asignados al usuario actual

Usar:

- `accessContext.currentUserAccess.roles`

### 3.6 Permisos efectivos del usuario actual

Usar:

- `accessContext.currentUserAccess.permissions`

## 4. Flujo recomendado de frontend

## 4.1 Inicio de sesion o refresh de sesion

Endpoints involucrados:

- `POST /api/auth/login`
- `POST /api/auth/register`
- `POST /api/auth/refresh`

Estos endpoints devuelven tokens, pero no devuelven `accessContext`.

Por lo tanto, despues de autenticar:

1. Guardar `accessToken`, `refreshToken` y `expiresIn`.
2. Llamar `GET /api/account/companies`.
3. Identificar la compania activa buscando `isActiveContext = true`.
4. Si frontend necesita trabajar sobre esa misma compania activa, llamar `GET /api/account/companies/{companyPublicId}/access-context`.
5. Si el usuario elige otra compania, llamar `POST /api/account/companies/{companyPublicId}/switch`.

Importante:

- no decodificar el JWT para resolver autorizacion
- no asumir que login ya devolvio el contexto funcional

## 4.2 Cambio de compania activa

Endpoint:

- `POST /api/account/companies/{companyPublicId}/switch`

Este endpoint es la forma recomendada de cambiar de tenant activo en UI.

Devuelve:

- nuevo `accessToken`
- nuevo `refreshToken`
- `activeCompany`
- `accessContext`

Flujo recomendado:

1. El usuario selecciona una compania.
2. Frontend llama `switch`.
3. Reemplaza inmediatamente el token actual por el nuevo token.
4. Reemplaza el `accessContext` actual por el que vino en la respuesta.
5. Invalida cache de pantallas, permisos y menus dependientes del tenant.
6. Redirige al home del tenant o a la pantalla elegida.

Importante:

- despues de `switch`, no es necesario llamar inmediatamente `access-context` otra vez
- el `accessContext` que viene en `switch` es la fuente de verdad inicial para ese tenant

## 4.3 Carga de menu y shell principal

Para construir la navegacion del tenant:

1. Tomar `accessContext.effectiveModules`.
2. Mostrar solo los modulos presentes.
3. Para features internas dentro de un modulo, usar `effectiveCapabilities`.

Regla importante:

- si un modulo no esta en `effectiveModules`, no debe aparecer en el menu
- si una capacidad no esta en `effectiveCapabilities`, no debe mostrarse la feature asociada

## 4.4 Carga de una pantalla protegida

Para cada pantalla sensible, frontend debe resolver la policy del recurso.

Endpoint:

- `GET /api/account/companies/{companyPublicId}/authorization/resource-policies/{resourceKey}`

Flujo recomendado:

1. Entrar a una pantalla protegida.
2. Resolver el `resourceKey` de esa pantalla.
3. Pedir `resourcePolicy`.
4. Usar `actions` para habilitar o deshabilitar acciones.
5. Usar `fields` para decidir visibilidad, masking y edicion.

## 4.5 Construccion y edicion de roles

Endpoint catalogo:

- `GET /api/account/companies/{companyPublicId}/authorization/role-builder-catalog`

Endpoints de roles:

- `GET /api/account/companies/{companyPublicId}/authorization/roles`
- `POST /api/account/companies/{companyPublicId}/authorization/roles`
- `GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`
- `PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`
- `DELETE /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`
- `GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`
- `PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`

Flujo recomendado:

1. Pedir `role-builder-catalog`.
2. Renderizar el arbol o lista de grants usando `availablePermissions`.
3. Crear el rol con `name`, `description` y opcionalmente `permissionIds`.
4. Para editar grants, usar `GET role grants` y luego `PUT role grants`.
5. Si `isSystemRole = true`, frontend debe renderizar el rol como no editable y no eliminable.

Importante:

- frontend debe usar los `id` de `availablePermissions` como `permissionIds`
- frontend no debe permitir seleccionar permisos fuera de `availablePermissions`
- `availablePermissions` ya viene filtrado por capacidades efectivas y permisos delegables del actor

## 4.6 Asignacion de roles a usuarios

Hay dos flujos validos:

### Flujo operativo desde Company Users

Endpoints:

- `POST /api/company/users`
- `PUT /api/company/users/{userId}`

Request:

- `rolePublicIds`

### Flujo puro de autorizacion para usuario existente

Endpoint:

- `PUT /api/account/companies/{companyPublicId}/authorization/users/{userPublicId}/roles`

Request:

- `roleIds`

Regla recomendada:

- usar `api/company/users` cuando el frontend esta en la pantalla operativa de usuarios
- usar `authorization/users/{userPublicId}/roles` cuando el frontend esta en una pantalla de administracion de autorizacion

## 5. Endpoints que frontend debe consumir

## 5.1 Resolver companias del usuario

`GET /api/account/companies?page=1&pageSize=20`

Campos importantes:

- `publicId`
- `name`
- `status`
- `planCode`
- `isActiveContext`
- `isOwnedByCurrentUser`

Uso:

- selector de compania
- bootstrap despues de login

## 5.2 Cambiar compania activa

`POST /api/account/companies/{companyPublicId}/switch`

Respuesta importante:

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "expiresIn": 900,
  "activeCompany": {
    "publicId": "00000000-0000-0000-0000-000000000001",
    "name": "ACME",
    "slug": "acme",
    "countryCode": "SV",
    "status": "Active"
  },
  "accessContext": {
    "companyContext": {
      "publicId": "00000000-0000-0000-0000-000000000001",
      "name": "ACME",
      "slug": "acme",
      "countryCode": "SV",
      "status": "Active"
    },
    "commercialContext": {
      "subscription": {
        "commercialPlanId": "11111111-1111-1111-1111-111111111111",
        "code": "FREE",
        "name": "Free",
        "description": "Plan base",
        "capabilityCodes": [
          "RBAC_ADMINISTRATION",
          "USER_ADMINISTRATION"
        ]
      },
      "extensions": []
    },
    "effectiveCapabilities": [],
    "effectiveModules": [],
    "currentUserAccess": {
      "roles": [],
      "permissions": [],
      "scopes": []
    }
  }
}
```

Nota:

- los ejemplos muestran estructura en `camelCase`, que es como frontend la recibe en JSON

## 5.3 Obtener access context

`GET /api/account/companies/{companyPublicId}/access-context`

Este endpoint devuelve el contexto consolidado para frontend.

Campos importantes de respuesta:

### `companyContext`

Representa la compania resuelta.

### `commercialContext.subscription`

Representa el plan activo y sus `capabilityCodes`.

### `commercialContext.extensions`

Representa add-ons activos y sus `capabilityCodes`.

### `effectiveCapabilities`

Es la fuente funcional de verdad.

Cada item incluye:

- `capabilityCode`
- `moduleKey`
- `displayName`
- `description`
- `source`
- `grantedByPlan`
- `grantedByAddon`

### `effectiveModules`

Se usa para menu y navegacion.

Cada item incluye:

- `moduleKey`
- `displayName`
- `description`
- `source`
- `grantedByPlan`
- `grantedByAddon`

### `currentUserAccess.roles`

Cada item incluye:

- `id`
- `name`
- `description`
- `isSystemRole`

### `currentUserAccess.permissions`

Cada item incluye:

- `id`
- `code`
- `name`
- `description`
- `module`
- `screen`
- `kind`
- `action`
- `fieldName`
- `fieldAccess`
- `capabilityCodes`
- `isDormant`
- `supportedScopeTypes`

Reglas de uso:

- si `isDormant = true`, frontend no debe usar ese permiso para habilitar UI
- `supportedScopeTypes` hoy debe tratarse como informativo; el enforcement real en v1 es `COMPANY`

### `currentUserAccess.scopes`

Cada item incluye:

- `permissionCode`
- `scopeType`
- `values`
- `isImplicit`

Regla actual:

- en v1 el backend devuelve scopes implicitos de `COMPANY`

## 5.4 Obtener role builder catalog

`GET /api/account/companies/{companyPublicId}/authorization/role-builder-catalog`

Respuesta:

- `availableModules`
- `availableCapabilities`
- `availablePermissions`
- `fieldPoliciesCatalog`
- `scopeTypes`

`availablePermissions` es el insumo principal del constructor de roles.

Cada permiso disponible incluye:

- `id`
- `code`
- `name`
- `description`
- `module`
- `screen`
- `kind`
- `action`
- `fieldName`
- `fieldAccess`
- `capabilityCodes`
- `isDormant`
- `supportedScopeTypes`

Uso correcto:

- usar `availablePermissions` para checkbox tree, selectors y agrupacion por modulo o pantalla
- usar `id` al enviar `permissionIds`
- no mezclar este catalogo con permisos guardados manualmente en frontend

## 5.5 Obtener resource policy

`GET /api/account/companies/{companyPublicId}/authorization/resource-policies/{resourceKey}`

Respuesta:

```json
{
  "resourceKey": "RBAC_USERS",
  "actions": {
    "canAccess": true,
    "canRead": true,
    "canCreate": true,
    "canUpdate": false,
    "canDelete": false
  },
  "fields": [
    {
      "fieldKey": "RBAC_USERS.EMAIL",
      "propertyName": "Email",
      "displayName": "Email",
      "access": "masked",
      "isRequired": false,
      "isSensitive": true
    }
  ]
}
```

Reglas de UI:

- `canAccess = false` -> no renderizar la pantalla o redirigir a `403`
- `canRead = false` -> no pedir data de lectura de esa pantalla
- `canCreate = true` -> habilitar boton de crear
- `canUpdate = true` -> habilitar acciones de editar
- `canDelete = true` -> habilitar acciones destructivas

Para campos:

- `hidden` -> no renderizar
- `masked` -> renderizar valor oculto y no permitir edicion
- `readonly` -> renderizar visible y no editable
- `editable` -> renderizar editable

## 5.6 Listar roles

`GET /api/account/companies/{companyPublicId}/authorization/roles?pageNumber=1&pageSize=20`

Cada item incluye:

- `id`
- `name`
- `description`
- `isSystemRole`
- `grantCount`
- `userCount`

Uso:

- lista de roles
- selector de roles
- filtros en administracion de usuarios

## 5.7 Crear rol

`POST /api/account/companies/{companyPublicId}/authorization/roles`

Body:

```json
{
  "name": "People Admin",
  "description": "Administra usuarios operativos",
  "permissionIds": [
    "22222222-2222-2222-2222-222222222222",
    "33333333-3333-3333-3333-333333333333"
  ]
}
```

## 5.8 Obtener rol

`GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`

Respuesta:

- `id`
- `name`
- `description`
- `isSystemRole`
- `userCount`
- `grants`

## 5.9 Actualizar metadatos de rol

`PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`

Body:

```json
{
  "name": "People Admin",
  "description": "Administra usuarios operativos y roles"
}
```

## 5.10 Obtener grants de rol

`GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`

Respuesta:

- `id`
- `name`
- `isSystemRole`
- `grants`

Cada grant incluye:

- `id`
- `code`
- `name`
- `description`
- `module`
- `resourceKey`
- `kind`
- `action`
- `fieldName`
- `fieldAccessState`

Importante:

- en esta respuesta, `resourceKey` debe tratarse como descriptor del grant para UI de roles
- para enforcement real de una pantalla, frontend debe seguir usando `resourcePolicy` y no este campo

## 5.11 Actualizar grants de rol

`PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`

Body:

```json
{
  "permissionIds": [
    "22222222-2222-2222-2222-222222222222",
    "33333333-3333-3333-3333-333333333333"
  ]
}
```

Regla:

- el conjunto enviado reemplaza el conjunto de grants del rol

## 5.12 Sincronizar roles de un usuario existente

`PUT /api/account/companies/{companyPublicId}/authorization/users/{userPublicId}/roles`

Body:

```json
{
  "roleIds": [
    "44444444-4444-4444-4444-444444444444",
    "55555555-5555-5555-5555-555555555555"
  ]
}
```

Regla:

- el conjunto enviado reemplaza los roles actuales del usuario dentro de la compania activa

## 5.13 Crear usuario de compania

`POST /api/company/users`

Body:

```json
{
  "email": "analista@acme.com",
  "firstName": "Ana",
  "lastName": "Lopez",
  "rolePublicIds": [
    "44444444-4444-4444-4444-444444444444"
  ]
}
```

## 5.14 Actualizar usuario de compania

`PUT /api/company/users/{userId}`

Body:

```json
{
  "firstName": "Ana Maria",
  "lastName": "Lopez",
  "rolePublicIds": [
    "44444444-4444-4444-4444-444444444444",
    "55555555-5555-5555-5555-555555555555"
  ]
}
```

## 6. Como debe decidir frontend

## 6.1 Mostrar menu

Usar `effectiveModules`.

Ejemplo:

- `RBAC` visible -> mostrar menu de autorizacion
- `USERS` visible -> mostrar menu de company users

## 6.2 Mostrar feature interna

Usar `effectiveCapabilities`.

Ejemplo:

- `USER_ADMINISTRATION` presente -> habilitar flows de company users
- `RBAC_ADMINISTRATION` presente -> habilitar flows de roles y grants

## 6.3 Mostrar o no una pantalla

Usar `resourcePolicy.actions.canAccess`.

## 6.4 Mostrar botones de accion

Usar:

- `canCreate`
- `canUpdate`
- `canDelete`

## 6.5 Renderizar campos de formulario

Usar `resourcePolicy.fields`.

Ejemplo:

- `access = hidden` -> no montar el input
- `access = masked` -> mostrar valor protegido
- `access = readonly` -> deshabilitar input
- `access = editable` -> input normal

## 6.6 Construir selector de permisos para roles

Usar exclusivamente `roleBuilderCatalog.availablePermissions`.

No usar:

- `currentUserAccess.permissions` como catalogo de seleccion
- valores hardcodeados en frontend

## 6.7 Renderizar selector de roles para usuarios

Usar `GET authorization/roles`.

No usar:

- roles del usuario actual como fuente de roles asignables

## 7. Claves y valores que frontend debe conocer

## 7.1 Module keys actuales

- `RBAC`
- `USERS`
- `ORG_STRUCTURE_CATALOGS`
- `POSITION_DESCRIPTION_CATALOGS`
- `JOB_PROFILES`
- `POSITION_SLOTS`
- `SALARY_TABULATOR`
- `COST_CENTERS`
- `LEGAL_REPRESENTATIVES`
- `COMPETENCY_FRAMEWORK`
- `ORG_UNITS`
- `LOCATIONS`
- `PERSONNEL_FILES`

## 7.2 Capability codes actuales

- `RBAC_ADMINISTRATION`
- `USER_ADMINISTRATION`
- `ORG_STRUCTURE_CATALOG_ADMINISTRATION`
- `POSITION_DESCRIPTION_CATALOG_ADMINISTRATION`
- `JOB_PROFILE_ADMINISTRATION`
- `POSITION_SLOT_ADMINISTRATION`
- `SALARY_TABULATOR_ADMINISTRATION`
- `COST_CENTER_ADMINISTRATION`
- `LEGAL_REPRESENTATIVE_ADMINISTRATION`
- `COMPETENCY_FRAMEWORK_ADMINISTRATION`
- `ORG_UNIT_ADMINISTRATION`
- `LOCATION_ADMINISTRATION`
- `PERSONNEL_FILE_ADMINISTRATION`

## 7.3 Scope types catalogados

- `COMPANY`
- `LOCATION`
- `DEPARTMENT`
- `EMPLOYEE`
- `COUNTRY`

Regla actual:

- en v1 el enforcement real expuesto al frontend debe considerarse solo `COMPANY`

## 7.4 Resource keys actualmente resolubles por `resourcePolicy`

- `RBAC_USERS`
- `RBAC_ROLES`
- `RBAC_PERMISSIONS`
- `AUDIT_LOGS`

## 7.5 Field keys actualmente catalogados

Para `RBAC_USERS`:

- `RBAC_USERS.ID`
- `RBAC_USERS.EMAIL`
- `RBAC_USERS.FIRST_NAME`
- `RBAC_USERS.LAST_NAME`
- `RBAC_USERS.ROLE`
- `RBAC_USERS.STATUS`

## 8. Reglas de invalidacion de cache

Frontend debe refrescar `accessContext` y caches relacionadas cuando ocurra cualquiera de estos eventos:

- cambio de compania activa
- cambio de plan o add-ons de la compania
- cambio de roles del usuario actual
- cambio de grants de un rol que afecte al usuario actual
- re-login del usuario

Regla recomendada:

- despues de `switch`, usar el `accessContext` devuelto y limpiar cache anterior
- despues de cambios de autorizacion que afecten al usuario actual, volver a pedir `access-context`

## 9. Reglas que frontend no debe romper

- no inferir autorizacion desde el JWT
- no mostrar un modulo solo porque el usuario tenga un rol
- no mostrar una accion solo porque el usuario tenga el permiso si la capability no existe
- no ignorar `isDormant`
- no asumir que un campo editable en UI sigue siendo editable sin consultar `resourcePolicy`
- no hardcodear permisos como fuente de verdad
- no llamar endpoints de `authorization` con una `companyPublicId` distinta de la compania activa del token

## 10. Estado actual de la implementacion

Esta guia cubre la superficie Core tenant-scoped hoy disponible.

Importante para frontend:

- el flujo nuevo ya no usa `api/iam/*`
- el flujo nuevo ya no usa `api/rbac/*`
- el bootstrap de autorizacion debe hacerse con `access-context`
- el constructor de roles debe hacerse con `role-builder-catalog`
- la resolucion de acciones y campos debe hacerse con `resourcePolicy`
- `company/users` ya opera con `rolePublicIds` multi-rol

## 11. Flujo minimo recomendado de implementacion en frontend

1. Autenticar usuario.
2. Pedir `GET /api/account/companies`.
3. Detectar compania activa o permitir seleccion.
4. Si hay cambio de tenant, llamar `switch`.
5. Guardar `accessContext`.
6. Construir menu con `effectiveModules`.
7. Hacer feature gating con `effectiveCapabilities`.
8. Antes de renderizar pantallas protegidas, pedir `resourcePolicy`.
9. Para administrar roles, usar `role-builder-catalog` + endpoints `authorization/roles`.
10. Para usuarios, usar `company/users` y `authorization/users/{userPublicId}/roles` segun el flujo.
