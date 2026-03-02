# IAM RBAC Administration API

## Scope

Este modulo implementa la base de administracion tenant-scoped para RBAC nivel 3:

- usuarios
- roles
- permisos de pantalla/accion
- permisos de campo lectura/escritura
- asignacion de roles a usuarios
- asignacion de permisos a roles

## Authorization

Todos los endpoints requieren autenticacion JWT y un `tid` valido.

Acceso permitido si se cumple cualquiera de estas condiciones:

- claim de rol `platform_admin`
- claim de permiso `iam.administration.manage`
- permiso `iam.administration.manage` asignado al usuario por RBAC en la base de datos

## Endpoints

### Users

- `POST /api/iam/users`
- `GET /api/iam/users?pageNumber=1&pageSize=20&search=...`
- `GET /api/iam/users/{userId}`
- `PUT /api/iam/users/{userId}/roles`

#### Create user request

```json
{
  "firstName": "Carla",
  "lastName": "Lopez",
  "email": "carla@tenant.com",
  "isActive": true,
  "roleIds": [
    "9f3e3a83-f57a-41f2-a53c-5d0db2cbaf31"
  ]
}
```

#### Sync user roles request

```json
{
  "roleIds": [
    "9f3e3a83-f57a-41f2-a53c-5d0db2cbaf31"
  ]
}
```

### Roles

- `POST /api/iam/roles`
- `GET /api/iam/roles?pageNumber=1&pageSize=20&search=...`
- `GET /api/iam/roles/{roleId}`
- `PUT /api/iam/roles/{roleId}`
- `POST /api/iam/roles/{roleId}/clone`
- `GET /api/iam/roles/{roleId}/permission-matrix`
- `PUT /api/iam/roles/{roleId}/permission-matrix`
- `DELETE /api/iam/roles/{roleId}`
- `PUT /api/iam/roles/{roleId}/permissions`
- `PUT /api/iam/roles/{roleId}/users`

### RBAC matrix endpoints

- `GET /api/rbac/resources`
- `GET /api/rbac/roles/{roleId}/permissions`
- `PUT /api/rbac/roles/{roleId}/permissions`
- `GET /api/rbac/audit?roleId=&resourceKey=&from=&to=&page=&pageSize=`

#### Create role request

```json
{
  "name": "Tenant Admin",
  "description": "Administra seguridad del tenant",
  "permissionIds": [
    "f4f2fd20-0a1a-42d5-9d83-97f1b1ab3771"
  ]
}
```

#### Update role request

```json
{
  "name": "Supervisor",
  "description": "Rol editable del tenant"
}
```

#### Clone role request

```json
{
  "name": "Supervisor Copy",
  "description": "Clon editable para ajustes"
}
```

#### Sync role permissions request

```json
{
  "permissionIds": [
    "f4f2fd20-0a1a-42d5-9d83-97f1b1ab3771"
  ]
}
```

#### Sync role users request

```json
{
  "userIds": [
    "9f3e3a83-f57a-41f2-a53c-5d0db2cbaf31",
    "fe51eb9e-9e50-4ed0-bffd-6f7bf61a4a9f"
  ]
}
```

#### Update role permission matrix request

```json
{
  "screens": [
    {
      "screen": "Users",
      "access": true,
      "read": true,
      "create": true,
      "update": false,
      "delete": false
    },
    {
      "screen": "Roles",
      "access": true,
      "read": true,
      "create": false,
      "update": false,
      "delete": false
    }
  ]
}
```

### Permissions

- `POST /api/iam/permissions`
- `GET /api/iam/permissions?pageNumber=1&pageSize=20&search=...`
- `GET /api/iam/permissions/{permissionId}`

#### Screen action permission

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

#### Field permission

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

Si `code` no se envia, el backend lo genera automaticamente.

## Response conventions

- listas siempre paginadas
- errores usando `ProblemDetails`
- `201 Created` para creacion
- `201 Created` para clonacion
- `200 OK` para consultas y sincronizacion de relaciones
- `204 No Content` para eliminacion exitosa
- `409 Conflict` para duplicados por tenant
- `404 Not Found` para ids inexistentes o fuera del tenant
- `403 Forbidden` para modificar roles protegidos del sistema

## Permission matrix

- HU-005 se expone via `GET/PUT /api/rbac/roles/{roleId}/permissions`
- `GET/PUT /api/iam/roles/{roleId}/permission-matrix` permanece por compatibilidad
- la matriz trabaja sobre permisos `screen/action` estandarizados por catalogo
- `Access` controla acceso a pantalla
- `Read/Create/Update/Delete` controlan el comportamiento de API y UI en el siguiente request
- si faltan permisos estandar del catalogo en el tenant, el backend los crea bajo demanda al guardar la matriz
- cambios de matriz generan auditoria basica via logs estructurados con actor, tenant, rol y diff de permisos

## Data model

Tablas previstas:

- `iam_users`
- `iam_roles`
- `iam_permissions`
- `iam_user_role_assignments`
- `iam_role_permission_assignments`

Todas son tenant-scoped y usan `TenantId` en indices compuestos.

## Current scope limits

- no documenta los flujos de auth; el registro local vive en `docs/technical/api-output/auth-register.md`
- no incluye enforcement de field masking sobre otros modulos
- no incluye endpoints de delete para usuarios o permisos
- no incluye migraciones EF en esta entrega

## Protected roles

- `isSystemRole=true` marca roles protegidos del tenant, por ejemplo `Admin de Empresa`
- los roles protegidos no permiten editar nombre/descripcion ni reasignar permisos
- la eliminacion de roles solo aplica a roles no protegidos y sin usuarios asignados
- las operaciones de asignacion de roles rechazan dejar al tenant sin administradores activos

## Permission evaluation

Los cambios de roles impactan en el siguiente request porque la autorizacion IAM consulta los permisos activos directamente desde la base de datos en cada llamada protegida.
