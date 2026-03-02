# RBAC Permission Matrix

## Scope

HU-005 agrega una capa de matriz por pantalla y accion sobre el modelo IAM existente.

Recursos soportados actualmente:

- `RBAC_USERS`
- `RBAC_ROLES`
- `RBAC_PERMISSIONS`

Acciones soportadas por recurso:

- `Access`
- `Read`
- `Create`
- `Update`
- `Delete`

`Permissions` no soporta `Delete` en esta etapa.

## Endpoints

- `GET /api/rbac/resources`
- `GET /api/rbac/roles/{roleId}/permissions`
- `PUT /api/rbac/roles/{roleId}/permissions`
- `GET /api/rbac/resources/{resourceKey}/fields`
- `GET /api/rbac/roles/{roleId}/field-permissions?resourceKey=...`
- `PUT /api/rbac/roles/{roleId}/field-permissions`
- `GET /api/rbac/audit?roleId=&resourceKey=&from=&to=&page=&pageSize=`

Compatibilidad temporal:

- `GET /api/iam/roles/{roleId}/permission-matrix`
- `PUT /api/iam/roles/{roleId}/permission-matrix`

## Authorization model

- `Access` es independiente y se exige siempre junto con la accion especifica
- `Read` bloquea listados/consultas si no esta presente
- `Create` bloquea altas/invitaciones
- `Update` bloquea ediciones, reasignaciones y cambios de matriz
- `Delete` bloquea operaciones destructivas como eliminacion de roles o desactivacion de usuarios

Overrides permitidos:

- `platform_admin`
- `iam.administration.manage`
- permiso `Manage` especifico de pantalla:
  - `RBAC.USERS.MANAGE`
  - `RBAC.ROLES.MANAGE`
  - `RBAC.PERMISSIONS.MANAGE`

Si no se cumplen las reglas anteriores, la API responde `403` con `code = RBAC_DENIED`.

## Catalog

Los permisos de matriz son `ScreenAction` y usan estos codigos:

- `RBAC.USERS.ACCESS`
- `RBAC.USERS.READ`
- `RBAC.USERS.CREATE`
- `RBAC.USERS.UPDATE`
- `RBAC.USERS.DELETE`
- `RBAC.ROLES.ACCESS`
- `RBAC.ROLES.READ`
- `RBAC.ROLES.CREATE`
- `RBAC.ROLES.UPDATE`
- `RBAC.ROLES.DELETE`
- `RBAC.PERMISSIONS.ACCESS`
- `RBAC.PERMISSIONS.READ`
- `RBAC.PERMISSIONS.CREATE`
- `RBAC.PERMISSIONS.UPDATE`

El catalogo global persistido vive en `rbac_resource_catalog`.

Si el tenant aun no tiene alguno de esos permisos persistidos en `iam_permissions`, el backend lo crea automaticamente al guardar la matriz.

## Enforcement

API ya protegida por matriz en:

- `GET/POST/PUT/PATCH /api/company/users/*`
- `GET/POST/PUT/DELETE /api/iam/roles/*`
- `GET/POST /api/iam/permissions/*`
- `GET/PUT /api/iam/roles/{roleId}/permission-matrix`
- `GET/PUT /api/rbac/roles/{roleId}/permissions`

HU-006 suma enforcement por campo sobre `CompanyUsers` usando el recurso `RBAC_USERS`.

- `GET /api/company/users` filtra o enmascara propiedades segun permisos efectivos por campo
- `POST /api/company/users` y `PUT /api/company/users/{userId}` rechazan cambios de campos no editables con `code = FIELD_EDIT_FORBIDDEN`

Los cambios impactan en el siguiente request porque la autorizacion consulta permisos activos contra la base de datos del tenant.

El backend evita dejar al tenant sin al menos un usuario activo capaz de administrar `RBAC_ROLES` y `RBAC_PERMISSIONS`.

## Audit

La auditoria basica de cambios de permisos ahora queda persistida en `rbac_permission_audit_logs` y expuesta por API.

Cada entrada guarda:

- `changedByUserId`
- `companyId`
- `roleId`
- `resourceKey`
- `changeType`
- `before`
- `after`
- `changedAtUtc`
