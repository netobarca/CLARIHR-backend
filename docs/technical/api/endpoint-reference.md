# Referencia de endpoints API

## 1. Proposito

Este documento es la referencia humana inicial de la API actual. No intenta duplicar todos los schemas runtime en markdown. Su funcion es explicar:

- inventario de modulos
- familias de rutas
- autenticacion y tenant scope
- flujos criticos
- comportamientos observables relevantes

El backend expone actualmente `308` endpoints en `34` controladores.

## 2. Modelo de autenticacion y tenant

### 2.1 Endpoints publicos

El acceso publico se limita intencionalmente a:

- `POST /api/auth/register`
- `POST /api/auth/external`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `GET /api/system/status`

### 2.2 Alcance de cuenta autenticada

Las rutas `/api/account/*` operan sobre la cuenta autenticada y el contexto de companias del usuario.

### 2.3 API operativa tenant-scoped

La mayoria de modulos funcionales usan:

- `/api/v1/companies/{companyPublicId:guid}/...` para operaciones tenant-scoped de coleccion o listado
- `/api/v1/{resource}/{publicId:guid}` para operar recursos directos una vez establecido el contexto tenant

### 2.4 Estandar de identificadores y codigos

Convencion obligatoria de la API publica:

- la superficie externa usa `publicId` o `<Entidad>PublicId`
- `id` e `internalId` quedan reservados para persistencia y logica interna
- cuando un recurso tiene codigo de negocio, la API expone `code` y `normalizedCode`
- `code` y `normalizedCode` se publican en `UPPERCASE`

Swagger runtime es la referencia canonica del contrato. Las secciones narrativas de este documento ya siguen este estandar aunque algunos nombres historicos puedan aparecer en explicaciones de negocio.

### 2.5 Flujo de activacion de compania

La secuencia actual de onboarding es:

1. `POST /api/auth/register`
2. `POST /api/account/companies`
3. `POST /api/account/companies/{companyPublicId}/switch`

Despues del `switch`, el `access token` devuelto incluye contexto tenant y habilita los modulos `api/v1`.

## 3. Inventario de modulos

| Modulo | Controladores | Familias principales de rutas | Proposito |
|---|---|---|---|
| System | `SystemController` | `/api/system/status` | salud y estado de runtime |
| Auth | `AuthController` | `/api/auth/*` | registro, auth externa, login, refresh y logout |
| Account companies | `AccountCompaniesController` | `/api/account/companies*` | crear, listar, archivar, reactivar, cambiar compania activa y resolver catalogos previos al contexto tenant |
| Commercial plans | `CommercialPlansController` | `/api/account/commercial-plans*` | administrar el catalogo comercial global de planes reutilizables para futuras suscripciones |
| Company users | `CompanyUsersController` | `/api/company/users*` | invitar y administrar usuarios del tenant |
| IAM users | `IamUsersController` | `/api/iam/users*` | administracion de usuarios a nivel IAM |
| IAM roles | `IamRolesController` | `/api/iam/roles*` | CRUD de roles, clonacion, matrices y asignaciones |
| IAM permissions / RBAC | `IamPermissionsController`, `RbacController` | `/api/iam/permissions*`, `/api/rbac*` | catalogo de permisos, matrices, permisos por campo y auditoria RBAC |
| Audit | `AuditController` | `/api/audit*` | consulta y detalle de logs de auditoria |
| Org structure catalogs | `OrgStructureCatalogsController` | `/api/account/org-structure-catalogs/*`, `/api/v1/companies/{companyPublicId}/org-structure-catalogs/*` | tipos de compania, tipos de unidad y areas funcionales |
| Locations | `LocationHierarchyController`, `LocationLevelsController`, `LocationGroupsController`, `WorkCenterTypesController`, `WorkCentersController` | `/api/v1/companies/{companyPublicId}/location-*`, `/api/v1/companies/{companyPublicId}/work-*` | modelo geografico del tenant, grupos y work centers |
| Org units and cost centers | `OrgUnitsController`, `CostCentersController` | `/api/v1/companies/{companyPublicId}/org-units*`, `/api/v1/companies/{companyPublicId}/cost-centers*` | arbol organizacional, grafos, exportes y centros de costo |
| Legal representatives | `LegalRepresentativesController` | `/api/v1/companies/{companyPublicId}/legal-representatives*` | ciclo de vida y uso de representantes legales |
| Job and competency design | `JobCatalogsController`, `JobProfilesController`, `CompetencyFrameworkController`, `PositionDescriptionCatalogsController`, `PositionSlotsController` | `/api/v1/companies/{companyPublicId}/job-*`, `/api/v1/companies/{companyPublicId}/occupational-*`, `/api/v1/companies/{companyPublicId}/position-*` | diseno de puestos, competencias, catalogos y posiciones |
| Personnel files | `PersonnelFilesController`, `PersonnelFileProfileController`, `PersonnelFileEmploymentController`, `PersonnelFileCompensationController`, `PersonnelFileTalentController`, `PersonnelFileDocumentsController`, `PersonnelFileAdministrationController`, `PersonnelFileReportingController` | `/api/v1/companies/{companyPublicId}/personnel-files*`, `/api/v1/personnel-files/*`, `/api/v1/personnel-custom-field-definitions*` | ciclo de vida del expediente, perfil, empleo, compensacion, talento, documentos y reporting |
| Salary governance | `SalaryTabulatorController` | `/api/v1/companies/{companyPublicId}/salary-tabulator*` | lineas salariales, exportes y change requests |
| Report capabilities | `ReportsController` | `/api/v1/companies/{companyPublicId}/reports/capabilities` | descubrimiento de capacidades de reporte para frontend |

## 4. Flujos criticos y endpoints representativos

### 4.1 Autenticacion

- `POST /api/auth/register`: crea una cuenta de usuario y devuelve tokens
- `POST /api/auth/external`: crea o autentica un usuario mediante proveedor externo
- `POST /api/auth/login`: inicia sesion local
- `POST /api/auth/refresh`: rota la sesion usando un refresh token
- `POST /api/auth/logout`: revoca la sesion autenticada

### 4.2 Onboarding de compania

- `GET /api/account/companies/legal-representative-document-types`
- `GET /api/account/companies`
- `POST /api/account/companies`
- `POST /api/account/companies/{companyPublicId}/switch`

Comportamiento observable:

- el catalogo de tipos documentales del representante legal devuelve opciones activas y ordenadas para poblar el formulario de creacion de compania
- la creacion de compania siembra datos iniciales de locations segun el pais
- el cambio de compania modifica el contexto tenant activo del usuario

### 4.3 Catalogo comercial de planes

- `GET /api/account/commercial-plans`
- `GET /api/account/commercial-plans/{publicId}`
- `POST /api/account/commercial-plans`
- `PUT /api/account/commercial-plans/{publicId}`
- `PATCH /api/account/commercial-plans/{publicId}/activate`
- `PATCH /api/account/commercial-plans/{publicId}/inactivate`

Comportamiento observable:

- el catalogo es global y no depende del tenant activo del token
- solo usuarios con rol `platform_admin` pueden consultar o administrar planes comerciales
- cada plan define `fee` mensual base, precio por empleado activo, estado y limites configurables
- `FREE` existe sembrado como plan de sistema para el provisioning actual y no puede renombrarse ni inactivarse
- este modulo no activa suscripciones, no calcula cobros y no administra add-ons ni descuentos

### 4.4 Locations y organizacion

Endpoints representativos de lectura:

- `GET /api/v1/companies/{companyPublicId}/location-hierarchy`
- `GET /api/v1/companies/{companyPublicId}/location-levels`
- `GET /api/v1/companies/{companyPublicId}/location-groups/tree`
- `GET /api/v1/companies/{companyPublicId}/org-units/tree`
- `GET /api/v1/companies/{companyPublicId}/org-units/graph`
- `GET /api/v1/companies/{companyPublicId}/cost-centers/export`

Endpoints representativos de escritura:

- `POST /api/v1/companies/{companyPublicId}/location-groups`
- `POST /api/v1/companies/{companyPublicId}/work-centers`
- `POST /api/v1/companies/{companyPublicId}/org-units`
- `PATCH /api/v1/org-units/{publicId}/move`

### 4.5 IAM y permisos por campo

Endpoints representativos:

- `GET /api/iam/roles`
- `POST /api/iam/roles`
- `PUT /api/iam/roles/{rolePublicId}/permission-matrix`
- `GET /api/rbac/resources`
- `GET /api/rbac/roles/{rolePublicId}/field-permissions`
- `PUT /api/rbac/roles/{rolePublicId}/field-permissions`
- `GET /api/rbac/audit`

Comportamiento observable:

- la plataforma soporta permisos por recurso, accion y overrides por campo
- los cambios RBAC son auditables

### 4.6 Personnel files

Core:

- `POST /api/v1/companies/{companyPublicId}/personnel-files`
- `GET /api/v1/companies/{companyPublicId}/personnel-files`
- `GET /api/v1/personnel-files/{publicId}`
- `PATCH /api/v1/personnel-files/{publicId}/activate`
- `PATCH /api/v1/personnel-files/{publicId}/inactivate`

Profile:

- `PUT /api/v1/personnel-files/{publicId}/personal-info`
- `PUT /api/v1/personnel-files/{publicId}/identifications`
- `PUT /api/v1/personnel-files/{publicId}/family-members`
- `PUT /api/v1/personnel-files/{publicId}/educations`

Employment:

- `POST /api/v1/personnel-files/{publicId}/hire`
- `PUT /api/v1/personnel-files/{publicId}/employee-profile`
- `GET /api/v1/personnel-files/{publicId}/personnel-actions`

Compensation:

- `PUT /api/v1/personnel-files/{publicId}/salary-items`
- `GET /api/v1/personnel-files/{publicId}/payroll-transactions/export`
- `PUT /api/v1/personnel-files/{publicId}/bank-accounts`

Talent:

- `PUT /api/v1/personnel-files/{publicId}/evaluations`
- `GET /api/v1/personnel-files/{publicId}/position-competencies`

Documents and reporting:

- `POST /api/v1/personnel-files/{publicId}/documents`
- `GET /api/v1/personnel-file-documents/{documentPublicId}/download`
- `GET /api/v1/personnel-files/{publicId}/print`
- `POST /api/v1/companies/{companyPublicId}/personnel-files/dynamic-query`

Comportamiento observable:

- la mayoria de actualizaciones por seccion reemplazan el payload completo de la subseccion
- `hire` es una transicion funcional dedicada, no solo una edicion del tipo de registro
- los endpoints de reporting y export existen tanto a nivel tenant como a nivel recurso
- la profundizacion completa del modulo esta en `5.10 Personnel files`

### 4.7 Salary tabulator

Endpoints representativos:

- `GET /api/v1/companies/{companyPublicId}/salary-tabulator`
- `GET /api/v1/companies/{companyPublicId}/salary-tabulator/export`
- `GET /api/v1/companies/{companyPublicId}/salary-tabulator/change-requests`
- `POST /api/v1/companies/{companyPublicId}/salary-tabulator/change-requests`
- `PATCH /api/v1/salary-tabulator/change-requests/{publicId}/approve`

Comportamiento observable:

- los cambios salariales se modelan como `change requests` con transiciones explicitas de estado

## 5. Profundizacion por modulo

La profundizacion modular se ira completando por iteraciones. Esta version ya documenta varios modulos base del sistema y sigue avanzando por bloques funcionales.

### 5.1 IAM

#### 5.1.1 Alcance

Este bloque cubre la administracion de acceso del tenant activo e incluye:

- `CompanyUsersController` con base `/api/company/users`
- `IamUsersController` con base `/api/iam/users`
- `IamRolesController` con base `/api/iam/roles`
- `IamPermissionsController` con base `/api/iam/permissions`
- `RbacController` con base `/api/rbac`

Aunque `CompanyUsersController` no usa el prefijo `/api/iam`, forma parte del dominio funcional IAM porque administra membresias de usuarios del tenant, invitaciones, sincronizacion con roles y permisos por campo sobre `RBAC_USERS`.

#### 5.1.2 Proposito funcional en CLARIHR

El modulo IAM sirve para:

- administrar quien puede entrar y operar dentro de la compania activa
- definir roles del tenant y sus permisos
- exponer el catalogo de recursos RBAC que el frontend usa para matrices y pantallas de seguridad
- administrar permisos por campo para respuestas y formularios sensibles
- auditar cambios de permisos y matrices

En la practica, `api/company/users` resuelve la administracion funcional de usuarios del tenant, mientras que `api/iam/*` y `api/rbac/*` resuelven el plano tecnico de roles, permisos y matrices de autorizacion.

#### 5.1.3 Modelo operativo y reglas transversales del modulo

- El scope es tenant-scoped implicito. No usa `companyId` en la ruta; depende del tenant activo del token despues de `company switch`.
- Si no existe tenant activo, las operaciones sensibles fallan con errores del tipo `company_users.tenant.required`, `iam.tenant.required` o `TENANT_MISMATCH`, segun el caso.
- El modulo combina permisos de pantalla por accion y permisos por campo. Los recursos base observables son `RBAC_USERS`, `RBAC_ROLES`, `RBAC_PERMISSIONS` y `AUDIT_LOGS`.
- El gating por plan se evalua por pantalla. `RBAC_USERS` depende del plan module `USERS`. `RBAC_ROLES`, `RBAC_PERMISSIONS` y `AUDIT_LOGS` dependen del plan module `RBAC`.
- Un `platform_admin`, un usuario con `ManageAdministration` o con el permiso `manage` especifico de la pantalla puede bypassear la matriz granular de esa pantalla.
- Las respuestas y escrituras de `CompanyUsersController` respetan permisos por campo. Los campos afectados hoy son `RBAC_USERS.EMAIL`, `RBAC_USERS.FIRST_NAME`, `RBAC_USERS.LAST_NAME`, `RBAC_USERS.ROLE` y `RBAC_USERS.STATUS`.
- Los roles de sistema (`IsSystemRole = true`) son protegidos. No pueden modificarse ni eliminarse.
- El tenant siempre debe conservar al menos un administrador activo. Esa invariante se protege al cambiar roles, usuarios asignados a roles, matrices de permisos y usuarios de compania.
- Los cambios de roles, invitaciones, matrices RBAC y field permissions generan auditoria.

#### 5.1.4 Errores observables relevantes en IAM

- `UNAUTHENTICATED`: `401`, autenticacion requerida.
- `RBAC_DENIED`: `403`, el usuario autenticado no tiene permiso para la accion pedida.
- `TENANT_MISMATCH`: `403`, el recurso existe pero pertenece a otro tenant o no hay tenant valido para resolverlo.
- `FIELD_EDIT_FORBIDDEN`: `403`, la pantalla esta permitida pero uno o mas campos del payload no pueden editarse.
- `iam.roles.protected_role.forbidden` o `iam.roles.protected_role.delete_forbidden`: `403`, el rol es de sistema.
- `iam.roles.last_administrator_required` o `company_users.last_admin_required`: `409`, el cambio dejaria al tenant sin administrador activo.
- `iam.users.email_conflict`, `iam.roles.name_conflict`, `iam.permissions.code_conflict`: `409`, ya existe el valor unico dentro del tenant.
- `iam.users.not_found`, `iam.roles.not_found`, `iam.permissions.not_found`, `company_users.user.not_found`: `404`, el recurso no existe en el tenant actual.

#### 5.1.5 `CompanyUsersController` - usuarios operativos del tenant

Base route: `/api/company/users`

Usar este controlador cuando el frontend necesita administrar usuarios reales de la compania activa: invitarlos, cambiarles rol, desactivarlos o reenviar su invitacion. Es el entry point funcional para administracion de usuarios del tenant.

Contratos principales:

- `CompanyUserSummaryResponse`: `id`, `email`, `firstName`, `lastName`, `roleId`, `role`, `status`
- `CompanyUserResponse`: mismo shape para detalle/escritura
- `CompanyUserInvitationResponse`: `{ user, invitationExpiresUtc }`

Reglas comunes:

- Todas las rutas exigen `RBAC_USERS` con la accion correspondiente.
- Las lecturas y respuestas de escritura se filtran por field permissions; algunos campos pueden salir ocultos o enmascarados.
- El campo `status` usa `UserStatus` (`Active`, `Inactive`, `PendingActivation`).
- Este bloque sincroniza el usuario funcional (`User`), la membresia a compania y su proyeccion IAM (`IamUser`).

##### `GET /api/company/users`

- Proposito: listar usuarios de la compania activa.
- Autorizacion: `RBAC_USERS:Read`.
- Query: `page`, `pageSize`, `status`, `roleId`, `search`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`, `roleId` no puede ser `Guid.Empty`, `search` maximo `100`.
- Response: `PagedResponse<CompanyUserSummaryResponse>`.
- Observaciones: la lista ya sale filtrada por permisos de campo del usuario autenticado.

##### `POST /api/company/users`

- Proposito: invitar un usuario a la compania activa y asignarle un rol inicial.
- Autorizacion: `RBAC_USERS:Create`.
- Request body: `email`, `firstName`, `lastName`, `roleId`.
- Response: `201 Created` con `CompanyUserInvitationResponse`.
- Errores relevantes: `company_users.role.not_found`, `company_users.user_already_in_company`, `company_users.user_in_another_company`, `FIELD_EDIT_FORBIDDEN`.
- Observaciones: si el email ya existe como usuario local reutiliza el usuario; si no existe, crea un usuario invitado local, crea membresia, sincroniza `IamUser`, genera token de invitacion, revoca invitaciones activas previas para esa combinacion usuario/compania y envia correo.

##### `PUT /api/company/users/{userId}`

- Proposito: actualizar nombre/apellido del usuario y cambiar su rol dentro de la compania activa.
- Autorizacion: `RBAC_USERS:Update`.
- Request body: `firstName`, `lastName`, `roleId`.
- Response: `200 OK` con `CompanyUserResponse`.
- Errores relevantes: `company_users.role.not_found`, `company_users.last_admin_required`, `FIELD_EDIT_FORBIDDEN`, `TENANT_MISMATCH`.
- Observaciones: la autorizacion por campo se evalua solo para los campos realmente cambiados; tambien sincroniza el `IamUser` asociado para que el rol efectivo del tenant quede consistente.

##### `PATCH /api/company/users/{userId}/deactivate`

- Proposito: desactivar al usuario en la compania activa.
- Autorizacion: `RBAC_USERS:Update`.
- Response: `200 OK` con `CompanyUserResponse`.
- Errores relevantes: `company_users.last_admin_required`, `TENANT_MISMATCH`.
- Observaciones: desactiva el usuario funcional, la membresia y el `IamUser` vinculado; ademas revoca refresh tokens con razon `company-user-deactivated`.

##### `PATCH /api/company/users/{userId}/reactivate`

- Proposito: reactivar al usuario en la compania activa.
- Autorizacion: `RBAC_USERS:Update`.
- Response: `200 OK` con `CompanyUserResponse`.
- Errores relevantes: `TENANT_MISMATCH`, `company_users.user.not_found`.
- Observaciones: reactiva membresia y vuelve a marcar activo el `IamUser` si el estado funcional del usuario queda `Active`.

##### `POST /api/company/users/{userId}/reset-invitation`

- Proposito: emitir una nueva invitacion para un usuario pendiente o reenviable.
- Autorizacion: `RBAC_USERS:Update`.
- Response: `200 OK` con `CompanyUserInvitationResponse`.
- Errores relevantes: `company_users.reset_invitation.external_user_not_supported`, `TENANT_MISMATCH`.
- Observaciones: solo aplica a usuarios `Local`; revoca tokens de invitacion previos, emite uno nuevo y envia correo con `CompanyUserInvitationEmailKind.ResetInvitation`.

#### 5.1.6 `IamUsersController` - usuarios IAM del tenant

Base route: `/api/iam/users`

Usar este controlador cuando se necesita administrar la entidad IAM del tenant directamente, no la invitacion/membresia operativa. Su foco es el usuario IAM y su conjunto de roles.

Contratos principales:

- `IamUserSummaryResponse`: `id`, `email`, `firstName`, `lastName`, `isActive`, `roleCount`
- `IamUserResponse`: `id`, `email`, `firstName`, `lastName`, `isActive`, `roles[]`

Reglas comunes:

- Todas las rutas usan el recurso `RBAC_USERS`.
- `Create` y `SyncRoles` trabajan sobre la entidad IAM pura; no generan workflow de invitacion ni correo.
- Los `roleIds` se normalizan a un conjunto distinto.

##### `POST /api/iam/users`

- Proposito: crear un usuario IAM directamente en el tenant activo.
- Autorizacion: `RBAC_USERS:Create`.
- Request body: `firstName`, `lastName`, `email`, `isActive` y `roleIds`.
- Validaciones: `firstName` y `lastName` obligatorios, `email` valido, `roleIds` sin `Guid.Empty`.
- Response: `201 Created` con `IamUserResponse`.
- Errores relevantes: `iam.users.email_conflict`, `iam.roles.collection_not_found`.
- Observaciones: la operacion solo crea el usuario IAM, asigna roles y registra auditoria `UserCreated`; no dispara workflow de invitacion ni correo.

##### `GET /api/iam/users`

- Proposito: listar usuarios IAM del tenant.
- Autorizacion: `RBAC_USERS:Read`.
- Query: `pageNumber`, `pageSize`, `search`.
- Validaciones: `pageNumber > 0`, `pageSize` entre `1` y `100`, `search` maximo `100`.
- Response: `PagedResponse<IamUserSummaryResponse>`.

##### `GET /api/iam/users/{userId}`

- Proposito: obtener el detalle IAM de un usuario.
- Autorizacion: `RBAC_USERS:Read`.
- Response: `IamUserResponse`.
- Errores relevantes: `iam.users.not_found`, `TENANT_MISMATCH`.

##### `PUT /api/iam/users/{userId}/roles`

- Proposito: reemplazar el conjunto completo de roles asignados a un usuario IAM.
- Autorizacion: `RBAC_USERS:Update`.
- Request body: `{ "roleIds": [...] }`.
- Response: `IamUserResponse`.
- Errores relevantes: `iam.roles.collection_not_found`, `iam.roles.last_administrator_required`, `TENANT_MISMATCH`.
- Observaciones: un array vacio es valido para limpiar roles, pero si el usuario activo es el ultimo administrador efectivo del tenant la operacion falla con conflicto.

#### 5.1.7 `IamRolesController` - roles del tenant

Base route: `/api/iam/roles`

Este controlador administra el catalogo de roles del tenant, sus asignaciones crudas y su matriz de permisos. Es la pieza central de RBAC a nivel de rol.

Contratos principales:

- `IamRoleSummaryResponse`: `id`, `name`, `description`, `isSystemRole`, `permissionCount`, `userCount`
- `IamRoleResponse`: `id`, `name`, `description`, `isSystemRole`, `userCount`, `permissions[]`
- `RolePermissionMatrixResponse`: `roleId`, `roleName`, `isSystemRole`, `screens[]`

Reglas comunes:

- CRUD base usa `RBAC_ROLES`.
- La matriz y la sincronizacion de permisos usan permisos sobre `RBAC_PERMISSIONS`.
- Los nombres de rol son unicos por tenant.
- Los roles de sistema no pueden modificarse ni borrarse.
- El sistema no permite dejar al tenant sin administrador activo o sin administrador de seguridad RBAC.

##### `POST /api/iam/roles`

- Proposito: crear un rol nuevo con permisos iniciales opcionales.
- Autorizacion: `RBAC_ROLES:Create`.
- Request body: `name`, `description`, `permissionIds`.
- Validaciones: `name` obligatorio, maximo `100`; `description` maximo `500`.
- Response: `201 Created` con `IamRoleResponse`.
- Errores relevantes: `iam.roles.name_conflict`, `iam.permissions.collection_not_found`.

##### `GET /api/iam/roles`

- Proposito: listar roles del tenant.
- Autorizacion: `RBAC_ROLES:Read`.
- Query: `pageNumber`, `pageSize`, `search`.
- Response: `PagedResponse<IamRoleSummaryResponse>`.

##### `GET /api/iam/roles/{roleId}`

- Proposito: obtener el detalle de un rol.
- Autorizacion: `RBAC_ROLES:Read`.
- Response: `IamRoleResponse`.
- Errores relevantes: `iam.roles.not_found`, `TENANT_MISMATCH`.

##### `PUT /api/iam/roles/{roleId}`

- Proposito: actualizar nombre y descripcion del rol.
- Autorizacion: `RBAC_ROLES:Update`.
- Request body: `name`, `description`.
- Response: `IamRoleResponse`.
- Errores relevantes: `iam.roles.name_conflict`, `iam.roles.protected_role.forbidden`, `TENANT_MISMATCH`.

##### `POST /api/iam/roles/{roleId}/clone`

- Proposito: clonar un rol existente conservando su set actual de permisos.
- Autorizacion: `RBAC_ROLES:Create`.
- Request body: `name`, `description`, ambos opcionales.
- Response: `201 Created` con `IamRoleResponse`.
- Errores relevantes: `iam.roles.name_conflict`, `TENANT_MISMATCH`.
- Observaciones: si no se envia `name`, el backend intenta generar `"{Role} Copy"`, luego `"{Role} Copy 2"`, etc.

##### `GET /api/iam/roles/{roleId}/permission-matrix`

- Proposito: obtener la matriz de permisos por pantalla del rol.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Response: `RolePermissionMatrixResponse`.
- Observaciones: cada `screen` devuelve `resourceKey`, `displayName`, `module`, `screen`, `managedByOverride` y el estado de `access/read/create/update/delete`; cada accion reporta si esta soportada y si esta concedida.

##### `PUT /api/iam/roles/{roleId}/permission-matrix`

- Proposito: reemplazar la matriz de permisos por pantalla para el subconjunto de recursos enviado.
- Autorizacion: `RBAC_PERMISSIONS:Update`.
- Request body: `{ "screens": [{ "resourceKey", "access", "read", "create", "update", "delete" }] }`.
- Validaciones: al menos una pantalla, sin duplicados, `resourceKey` conocido, no pedir acciones no soportadas por la pantalla.
- Response: `RolePermissionMatrixResponse`.
- Errores relevantes: `iam.roles.protected_role.forbidden`, `iam.roles.last_administrator_required`, `TENANT_MISMATCH`.
- Observaciones: si faltan permisos matriciales en catalogo, el backend los crea automaticamente; cada cambio genera audit logs RBAC por recurso afectado.

##### `DELETE /api/iam/roles/{roleId}`

- Proposito: eliminar un rol.
- Autorizacion: `RBAC_ROLES:Delete`.
- Response: `204 No Content`.
- Errores relevantes: `iam.roles.protected_role.delete_forbidden`, `iam.roles.in_use`, `TENANT_MISMATCH`.
- Observaciones: no se puede borrar un rol que aun tenga usuarios asignados.

##### `PUT /api/iam/roles/{roleId}/permissions`

- Proposito: sincronizar el set crudo de permisos asignados al rol por `permissionId`.
- Autorizacion: `RBAC_PERMISSIONS:Update`.
- Request body: `{ "permissionIds": [...] }`.
- Response: `IamRoleResponse`.
- Errores relevantes: `iam.permissions.collection_not_found`, `iam.roles.protected_role.forbidden`, `iam.roles.last_administrator_required`.
- Observaciones: esta ruta es mas baja a nivel contrato que `permission-matrix`; trabaja con IDs de permisos y no con acciones por recurso.

##### `PUT /api/iam/roles/{roleId}/users`

- Proposito: sincronizar el conjunto completo de usuarios asignados al rol.
- Autorizacion: `RBAC_ROLES:Update`.
- Request body: `{ "userIds": [...] }`.
- Response: `IamRoleResponse`.
- Errores relevantes: `iam.users.collection_not_found`, `iam.roles.last_administrator_required`, `TENANT_MISMATCH`.
- Observaciones: agregar y remover usuarios se hace como reemplazo completo del set; el guard de ultimo administrador se evalua antes de guardar.

#### 5.1.8 `IamPermissionsController` - catalogo de permisos IAM

Base route: `/api/iam/permissions`

Este controlador administra el catalogo de permisos disponibles en el tenant. Es mas cercano al modelo tecnico de permisos que al modelo de matrices para frontend.

Contrato principal:

- `IamPermissionSummaryResponse` e `IamPermissionResponse`: `publicId`, `code`, `normalizedCode`, `name`, `description`, `module`, `screen`, `kind`, `action`, `fieldName`, `fieldAccess`

Reglas comunes:

- Todas las rutas usan `RBAC_PERMISSIONS`.
- Hoy existen endpoints de crear, listar y consultar; no hay `update` ni `delete`.
- `Kind = ScreenAction` obliga `action` y prohbe `fieldName`/`fieldAccess`.
- `Kind = Field` obliga `fieldName` y `fieldAccess`, y prohbe `action`.

##### `POST /api/iam/permissions`

- Proposito: crear un permiso IAM nuevo.
- Autorizacion: `RBAC_PERMISSIONS:Create`.
- Request body: `name`, `description`, `code`, `module`, `screen`, `kind`, `action`, `fieldName`, `fieldAccess`.
- Response: `201 Created` con `IamPermissionResponse`.
- Errores relevantes: `iam.permissions.code_conflict`.
- Observaciones: si `code` no se envia, el backend lo genera desde `module/screen/action` o `module/screen/fieldName/fieldAccess` con slug normalizado.

##### `GET /api/iam/permissions`

- Proposito: listar permisos IAM del tenant.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Query: `pageNumber`, `pageSize`, `search`.
- Response: `PagedResponse<IamPermissionSummaryResponse>`.

##### `GET /api/iam/permissions/{permissionPublicId}`

- Proposito: obtener el detalle de un permiso IAM.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Response: `IamPermissionResponse`.
- Errores relevantes: `iam.permissions.not_found`, `TENANT_MISMATCH`.

#### 5.1.9 `RbacController` - recursos, field permissions y auditoria RBAC

Base route: `/api/rbac`

Este controlador expone la vista que el frontend necesita para construir matrices y permisos por campo. En varios casos es una proyeccion mas ergonomica del mismo caso de uso que ya existe en `/api/iam/roles/{roleId}/permission-matrix`.

Contratos principales:

- `RbacResourcesResponse`: `items[]` con `resourceKey` y `displayName`
- `RbacRolePermissionsResponse`: `roleId`, `roleName`, `isSystemRole`, `permissions[]`
- `RoleFieldPermissionsResponse`: `roleId`, `roleName`, `resourceKey`, `fields[]`
- `PagedResponse<RbacPermissionAuditEntryResponse>` para auditoria RBAC

Reglas comunes:

- Todas las rutas usan `RBAC_PERMISSIONS` con `Read` o `Update`.
- `resourceKey` acepta claves de recurso del catalogo RBAC, por ejemplo `RBAC_USERS`, `RBAC_ROLES`, `RBAC_PERMISSIONS`, `AUDIT_LOGS`.
- Para field permissions, el rol objetivo debe tener `Access` sobre el recurso antes de poder configurar overrides de campo.

##### `GET /api/rbac/resources`

- Proposito: devolver el catalogo de recursos RBAC visibles para matrices.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Response: `RbacResourcesResponse`.
- Observaciones: si hay recursos activos persistidos en base, usa ese catalogo; si no, cae al catalogo estatico de `PermissionMatrixCatalog`.

##### `GET /api/rbac/resources/{resourceKey}/fields`

- Proposito: devolver el catalogo de campos configurables para un recurso RBAC.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Response: `ResourceFieldsResponse`.
- Errores relevantes: `400` si `resourceKey` es desconocido.
- Observaciones: solo devuelve campos configurables; hoy `RBAC_USERS` expone `EMAIL`, `FIRST_NAME`, `LAST_NAME`, `ROLE` y `STATUS`.

##### `GET /api/rbac/roles/{roleId}/permissions`

- Proposito: obtener una vista plana de permisos RBAC por recurso para un rol.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Response: `RbacRolePermissionsResponse`.
- Observaciones: es una proyeccion del mismo caso de uso que alimenta `GET /api/iam/roles/{roleId}/permission-matrix`, pero en shape mas simple para frontend.

##### `PUT /api/rbac/roles/{roleId}/permissions`

- Proposito: actualizar permisos RBAC por recurso usando un contrato plano.
- Autorizacion: `RBAC_PERMISSIONS:Update`.
- Request body: `{ "permissions": [{ "resourceKey", "hasAccess", "canRead", "canCreate", "canUpdate", "canDelete" }] }`.
- Response: `RbacRolePermissionsResponse`.
- Errores relevantes: `400` por `resourceKey` desconocido, `iam.roles.protected_role.forbidden`, `iam.roles.last_administrator_required`.
- Observaciones: internamente reutiliza `UpdateRolePermissionMatrixCommand`; es otra superficie del mismo caso de uso.

##### `GET /api/rbac/roles/{roleId}/field-permissions`

- Proposito: obtener los overrides efectivos de campo para un rol sobre un recurso.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Query: `resourceKey`.
- Response: `RoleFieldPermissionsResponse`.
- Errores relevantes: `400` por `resourceKey` desconocido, `iam.field_permissions.resource_access_required`, `TENANT_MISMATCH`.
- Observaciones: la respuesta devuelve solo campos configurables e incluye `isVisible`, `isEditable`, `isRequired`, `isMasked` e `isReadOnly`.

##### `PUT /api/rbac/roles/{roleId}/field-permissions`

- Proposito: crear o actualizar overrides de permisos por campo para un rol.
- Autorizacion: `RBAC_PERMISSIONS:Update`.
- Request body: `{ "resourceKey": "...", "fields": [{ "fieldKey", "isVisible", "isEditable", "isRequired", "isMasked" }] }`.
- Response: `RoleFieldPermissionsResponse`.
- Errores relevantes: `400` si hay `fieldKey` duplicados o no configurables, `iam.roles.protected_role.forbidden`, `iam.field_permissions.resource_access_required`, `TENANT_MISMATCH`.
- Observaciones: persiste overrides tenant-scoped, limpia cache de overrides del rol/recurso y registra auditoria dedicada de field permissions.

##### `GET /api/rbac/audit`

- Proposito: consultar auditoria de cambios RBAC por rol y recurso.
- Autorizacion: `RBAC_PERMISSIONS:Read`.
- Query: `roleId`, `resourceKey`, `from`, `to`, `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`, `from <= to`, `resourceKey` conocido si se envia.
- Response: `PagedResponse<RbacPermissionAuditEntryResponse>`.
- Observaciones: cada item devuelve `before` y `after` con `HasAccess/CanRead/CanCreate/CanUpdate/CanDelete`, mas `changedByUserId` y `changedAtUtc`.

#### 5.1.10 Relacion entre superficies IAM

- `api/company/users` resuelve membresia, invitacion y lifecycle operativo del usuario dentro de una compania.
- `api/iam/users` resuelve la entidad IAM y sus roles.
- `api/iam/roles` resuelve el catalogo de roles y su administracion principal.
- `api/iam/permissions` resuelve el catalogo tecnico de permisos.
- `api/rbac` resuelve vistas de frontend para matrices, field permissions y auditoria.

La superposicion mas importante es esta:

- `/api/iam/roles/{roleId}/permission-matrix`
- `/api/rbac/roles/{roleId}/permissions`

Ambas rutas actualizan o consultan la misma realidad RBAC, pero con contratos distintos: una orientada a matriz por pantalla y otra a lista plana por recurso.

### 5.2 Auth

#### 5.2.1 Alcance

Este bloque cubre la autenticacion publica y la gestion de sesiones iniciales a traves de `AuthController`, con base `/api/auth`.

Incluye:

- `POST /api/auth/register`
- `POST /api/auth/external`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

#### 5.2.2 Proposito funcional en CLARIHR

El modulo `Auth` sirve para:

- registrar cuentas locales
- autenticar usuarios locales
- autenticar o registrar usuarios via proveedor externo
- emitir y rotar pares `access token` + `refresh token`
- cerrar sesion invalidando tokens de refresco

Este modulo no administra companias ni permisos RBAC. Su responsabilidad termina en emitir identidad autenticada. El contexto tenant llega despues, cuando el usuario ya tiene membresias y selecciona compania activa.

#### 5.2.3 Modelo operativo y reglas transversales del modulo

- `register`, `external`, `login` y `refresh` son publicos (`AllowAnonymous`).
- `logout` requiere `Bearer` valido.
- La respuesta canonica del modulo es `AuthResponse`: `accessToken`, `refreshToken`, `expiresIn`, `user`.
- `user` devuelve `id`, `email`, `firstName`, `lastName` y `authProvider`.
- El contrato permite `refreshToken` nulo, pero la implementacion actual con `JwtTokenService` emite refresh token en `register`, `external`, `login` y `refresh`.
- El `access token` siempre incluye identidad basica (`sub`, email, nombre, `auth_provider`, `user_status`). Cuando el token queda asociado a un tenant, tambien incluye `tid` y el claim `role` tenant-scoped con el nombre normalizado del rol activo para esa compania.
- Un usuario recien registrado que aun no tiene compania recibe token sin tenant activo.
- `login` solo permite usuarios `Local` y `Active`.
- `external` hoy solo soporta `Google` en runtime; `Microsoft` y `Apple` existen en el enum `AuthProvider`, pero hoy no tienen provider service configurado.
- `refresh` rota el refresh token en cada uso valido. Si detecta reutilizacion de un token ya rotado, revoca la familia completa.
- `logout` revoca todos los refresh tokens del usuario autenticado, no solo uno.

#### 5.2.4 Errores observables relevantes en Auth

- `auth.user_already_exists`: `409`, el email ya existe.
- `auth.login.invalid_credentials`: `401`, email/password invalidos o usuario local no elegible para login.
- `auth.external.invalid_token`: `401`, el `idToken` externo no pudo validarse.
- `auth.external.email_missing`: `422`, el proveedor externo no devolvio email.
- `auth.external.provider_not_supported`: `400`, se pidio un proveedor externo que el backend no soporta.
- `auth.external.provider_configuration_invalid`: `500`, la configuracion del proveedor externo es invalida.
- `auth.external.provider_link_conflict`: `409`, el usuario ya esta vinculado a otro proveedor externo.
- `auth.external.email_link_not_allowed`: `409`, el backend no puede autovincular por email el usuario existente.
- `auth.user_not_active`: `401`, la cuenta existe pero no esta activa para flujo externo.
- `auth.refresh.invalid_token`: `401`, el refresh token es invalido, expiro o fue revocado.
- `auth.logout.invalid_current_user`: `401`, el contexto autenticado no puede resolverse a un usuario valido.
- `auth.token_configuration_invalid`: `500`, JWT no esta configurado correctamente.

#### 5.2.5 `AuthController`

Base route: `/api/auth`

Contratos principales:

- `RegisterUserRequest`: `firstName`, `lastName`, `email`, `password`, `country`, `source`
- `RegisterExternalRequest`: `provider`, `idToken`, `country`, `source`
- `LoginRequest`: `email`, `password`
- `RefreshTokenRequest`: `refreshToken`
- `AuthResponse`: `accessToken`, `refreshToken`, `expiresIn`, `user`

##### `POST /api/auth/register`

- Proposito: registrar una cuenta local nueva y devolver sesion autenticada.
- Autenticacion: publica.
- Request body: `firstName`, `lastName`, `email`, `password`, `country`, `source`.
- Validaciones: nombres maximo `100`; email valido maximo `320`; password entre `12` y `100` con mayuscula, minuscula, numero y caracter especial, sin espacios y sin incluir nombre, apellido o correo del usuario; `country` y `source` tienen regex controlada.
- Response: `201 Created` con `AuthResponse`.
- Errores relevantes: `common.validation`, `auth.user_already_exists`.
- Observaciones: hace hash del password, crea `User` local, guarda en transaccion y emite un par de tokens inmediatamente.

##### `POST /api/auth/external`

- Proposito: autenticar via proveedor externo y registrar o vincular usuario segun corresponda.
- Autenticacion: publica.
- Request body: `provider`, `idToken`, `country`, `source`.
- Validaciones: `provider` no puede ser `Local`; `idToken` obligatorio y maximo `8000`.
- Response: `201 Created` con `AuthResponse` si el usuario se crea en ese flujo; `200 OK` con `AuthResponse` si el usuario ya existia y solo se autentico/vinculo.
- Errores relevantes: `auth.external.invalid_token`, `auth.external.email_missing`, `auth.external.provider_not_supported`, `auth.external.provider_configuration_invalid`, `auth.external.provider_link_conflict`, `auth.external.email_link_not_allowed`, `auth.user_not_active`.
- Observaciones: hoy valida Google ID tokens; si el usuario no existe se crea como externo; si existe y esta activo puede vincularse automaticamente por email solo cuando el proveedor lo permite, por ejemplo email verificado de Gmail o hosted domain en Google Workspace.

##### `POST /api/auth/login`

- Proposito: autenticar un usuario local con email y password.
- Autenticacion: publica.
- Request body: `email`, `password`.
- Validaciones: email valido maximo `320`; password obligatorio maximo `100`.
- Response: `200 OK` con `AuthResponse`.
- Errores relevantes: `auth.login.invalid_credentials`.
- Observaciones: devuelve el mismo error para email inexistente, usuario inactivo, proveedor no local o password incorrecto.

##### `POST /api/auth/refresh`

- Proposito: rotar la sesion usando un refresh token valido.
- Autenticacion: publica.
- Request body: `refreshToken`.
- Validaciones: `refreshToken` obligatorio y maximo `2048`.
- Response: `200 OK` con `AuthResponse`.
- Errores relevantes: `auth.refresh.invalid_token`, `auth.token_configuration_invalid`.
- Observaciones: cada refresh exitoso invalida el refresh token anterior y emite uno nuevo; si se intenta reutilizar un token ya rotado, el backend revoca toda la familia de refresh tokens asociada.

##### `POST /api/auth/logout`

- Proposito: cerrar la sesion del usuario autenticado.
- Autenticacion: `Bearer` requerido.
- Request body: ninguno.
- Response: `204 No Content`.
- Errores relevantes: `auth.logout.invalid_current_user`.
- Observaciones: revoca todos los refresh tokens del usuario actual con motivo `logout`; no devuelve body.

### 5.3 Account companies

#### 5.3.1 Alcance

Este bloque cubre la administracion de companias propias del usuario autenticado mediante `AccountCompaniesController`, con base `/api/account/companies`.

Incluye:

- `GET /api/account/companies/legal-representative-document-types`
- `GET /api/account/companies/legal-representative-representation-types`
- `GET /api/account/companies`
- `GET /api/account/companies/{companyId}`
- `POST /api/account/companies`
- `PUT /api/account/companies/{companyId}`
- `PATCH /api/account/companies/{companyId}/archive`
- `PATCH /api/account/companies/{companyId}/reactivate`
- `POST /api/account/companies/{companyId}/switch`

#### 5.3.2 Proposito funcional en CLARIHR

El modulo `Account companies` sirve para:

- resolver catalogos necesarios antes de crear compania cuando aun no existe contexto tenant
- listar las companias creadas por la cuenta autenticada
- crear una nueva compania con provisionamiento inicial
- consultar y actualizar metadata basica de una compania propia
- archivar o reactivar companias propias
- cambiar el contexto tenant activo y recibir un nuevo token con ese `tid`

Este modulo es el puente entre `Auth` y `api/v1`. Primero autentica la cuenta, luego crea o selecciona una compania, y a partir de ese cambio de contexto el usuario puede operar los modulos tenant-scoped.

#### 5.3.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El scope aqui es por ownership del usuario autenticado, no por RBAC tenant-scoped.
- Las consultas y mutaciones principales solo operan sobre companias cuyo `CreatedByUserPublicId` coincide con el usuario autenticado.
- `switch` agrega una regla adicional: ademas de ser owner, el usuario debe tener membresia activa en la compania destino.
- `list` y `detail` exponen `IsActiveContext`, calculado desde el `tenantId` actual del token.
- `create` no cambia automaticamente la compania activa ni devuelve tokens nuevos; solo provisiona y devuelve detalle de la nueva compania.
- `create` provisiona la compania en plan `FREE`, crea representante legal inicial, siembra locations por pais, crea roles de sistema iniciales, genera permisos admin/matrix RBAC y vincula al owner como administrador.
- `update` solo cambia `name` y `companyTypeId`; no cambia pais ni representantes legales.
- `archive` no permite archivar la compania activa ni la compania primaria actual del usuario.
- `reactivate` respeta el limite de companias activas/suspended permitido para la cuenta.
- `switch` marca la compania destino como primaria para el usuario y devuelve un nuevo par de tokens ya emitido para ese tenant.

#### 5.3.4 Errores observables relevantes en Account companies

- `account_companies.current_user.invalid`: `401`, el token no resuelve un usuario actual valido.
- `account_companies.user.not_found`: `404`, el usuario autenticado ya no existe en persistencia.
- `COMPANY_NOT_FOUND`: `404`, la compania pedida no existe.
- `COMPANY_OWNERSHIP_FORBIDDEN`: `403`, la compania existe pero no pertenece al usuario autenticado.
- `COMPANY_LIMIT_REACHED`: `409`, la cuenta no puede crear otra compania activa/suspended.
- `COMPANY_REACTIVATION_LIMIT_REACHED`: `409`, no puede reactivarse porque se excederia el limite de companias activas/suspended.
- `COMPANY_ALREADY_ARCHIVED`: `409`, la compania ya estaba archivada.
- `COMPANY_ALREADY_ACTIVE`: `409`, la compania ya estaba activa.
- `ACTIVE_COMPANY_ARCHIVE_FORBIDDEN`: `409`, primero debe cambiarse la compania activa/primaria antes de archivar.
- `ACTIVE_COMPANY_SWITCH_FORBIDDEN`: `409`, la compania no puede convertirse en contexto activo.
- `COMPANY_TYPE_NOT_FOUND`: `404`, el tipo de compania seleccionado no existe o esta inactivo.
- `provisioning.country_not_found`: `400`, el `countryCode` enviado no existe en el catalogo global activo de paises.

#### 5.3.5 `AccountCompaniesController`

Base route: `/api/account/companies`

Contratos principales:

- `AccountCompanySummaryResponse`: `companyPublicId`, `name`, `slug`, `countryCode`, `status`, `planCode`, `isActiveContext`, `isOwnedByCurrentUser`, `createdAtUtc`, `companyType`
- `AccountCompanyDetailResponse`: agrega `modifiedAtUtc`, `activeLegalRepresentatives` y metadata completa del tipo de compania
- `SwitchActiveCompanyResponse`: `accessToken`, `refreshToken`, `expiresIn`, `activeCompany`
- `CountryCatalogItemResponse`: `id`, `code`, `name`, `sortOrder`
- `LegalRepresentativeDocumentTypeCatalogItemResponse`: `publicId`, `code`, `normalizedCode`, `name`, `sortOrder`
- `LegalRepresentativePositionTitleCatalogItemResponse`: `publicId`, `code`, `normalizedCode`, `name`, `sortOrder`
- `LegalRepresentativeRepresentationTypeCatalogItemResponse`: `publicId`, `code`, `normalizedCode`, `name`, `sortOrder`
- `CreateAccountCompanyRequest`: `name`, `countryCode`, `companyTypePublicId`, `initialLegalRepresentative`

##### `GET /api/account/companies/countries`

- Proposito: obtener el catalogo global activo de paises para el onboarding de companias.
- Autenticacion: `Bearer` requerido.
- Response: `IReadOnlyCollection<CountryCatalogItemResponse>`.
- Observaciones: devuelve items globales, ordenados por `sortOrder`; el frontend debe usar `code` como valor estable para `countryCode` al crear la compania y puede usar `name` solo para display.

##### `GET /api/account/companies/legal-representative-document-types`

- Proposito: obtener el catalogo activo de tipos de documento permitido para el representante legal inicial durante la creacion de compania.
- Autenticacion: `Bearer` requerido.
- Response: `IReadOnlyCollection<LegalRepresentativeDocumentTypeCatalogItemResponse>`.
- Observaciones: devuelve items globales, ordenados por `sortOrder`; `code` coincide con el valor string que debe enviarse en `initialLegalRepresentative.documentType` al crear la compania.

##### `GET /api/account/companies/legal-representative-position-titles`

- Proposito: obtener el catalogo activo de cargos iniciales permitido para el representante legal durante la creacion de compania.
- Autenticacion: `Bearer` requerido.
- Response: `IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>`.
- Observaciones: devuelve items globales, ordenados por `sortOrder`; `code` es una clave estable de frontend y `name` es el valor string que debe enviarse en `initialLegalRepresentative.positionTitle` al crear la compania.

##### `GET /api/account/companies/legal-representative-representation-types`

- Proposito: obtener el catalogo activo de tipos de representacion permitido para el representante legal inicial durante la creacion de compania.
- Autenticacion: `Bearer` requerido.
- Response: `IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>`.
- Observaciones: devuelve items globales, ordenados por `sortOrder`; `code` coincide con el valor string que debe enviarse en `initialLegalRepresentative.representationType` al crear la compania.

##### `GET /api/account/companies`

- Proposito: listar las companias propias del usuario autenticado.
- Autenticacion: `Bearer` requerido.
- Query: `status`, `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`.
- Response: `PagedResponse<AccountCompanySummaryResponse>`.
- Observaciones: no tiene busqueda libre; la lista se ordena por `name` y luego `companyPublicId`, y marca con `isActiveContext` la compania alineada al tenant del token actual.

##### `GET /api/account/companies/{companyPublicId}`

- Proposito: obtener el detalle de una compania propia.
- Autenticacion: `Bearer` requerido.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`.
- Observaciones: devuelve solo representantes legales activos, ordenados con los marcados como primarios primero, mas metadata del tipo de compania si existe. `activeLegalRepresentatives[].isPrimary` puede venir en `true`, `false` o `null`; `null` indica que el registro fue creado sin marcar prioridad inicial.

##### `POST /api/account/companies`

- Proposito: crear una nueva compania para la cuenta autenticada.
- Autenticacion: `Bearer` requerido.
- Request body: `name`, `countryCode`, `companyTypePublicId`, `initialLegalRepresentative`.
- Validaciones base: `name` obligatorio maximo `150`; `countryCode` de `2` o `3` letras y existente en el catalogo global activo de paises; `companyTypePublicId` no puede ser `Guid.Empty`.
- Validaciones del representante inicial: `firstName`, `lastName`, `documentNumber`, `positionTitle`, `effectiveFromUtc`; `effectiveToUtc >= effectiveFromUtc`; `email` opcional maximo `320`; `phone` opcional maximo `40`; `isPrimary` es opcional.
- Response: `201 Created` con `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_LIMIT_REACHED`, `COMPANY_TYPE_NOT_FOUND`, `provisioning.country_not_found`.
- Observaciones: provisiona plan `FREE`, crea representante legal inicial, crea rol de sistema `Admin de Empresa`, crea rol de sistema `Usuario Estándar`, vincula al owner como admin, siembra locations segun el pais y deja la nueva compania sin hacer auto-switch. `SV` conserva una plantilla estructurada de locations; el resto de paises recibe una jerarquia minima generica de un solo nivel para permitir el arranque del tenant. Para `countryCode`, el frontend debe usar el `code` devuelto por `GET /api/account/companies/countries`. Para `initialLegalRepresentative.documentType` y `initialLegalRepresentative.representationType`, el frontend debe usar el `code` devuelto por sus endpoints de catalogo respectivos. Para `initialLegalRepresentative.positionTitle`, el frontend debe usar el `name` devuelto por el endpoint de catalogo de cargos. Si `initialLegalRepresentative.isPrimary` se omite o se envia `null`, el registro se persiste con `isPrimary = null`.

##### `PUT /api/account/companies/{companyPublicId}`

- Proposito: actualizar nombre y tipo de compania de una compania propia.
- Autenticacion: `Bearer` requerido.
- Request body: `name`, `companyTypePublicId`.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `COMPANY_TYPE_NOT_FOUND`.
- Observaciones: no cambia pais, plan ni representantes legales; solo metadata basica.

##### `PATCH /api/account/companies/{companyPublicId}/archive`

- Proposito: archivar una compania propia.
- Autenticacion: `Bearer` requerido.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_ALREADY_ARCHIVED`, `ACTIVE_COMPANY_ARCHIVE_FORBIDDEN`, `COMPANY_OWNERSHIP_FORBIDDEN`.
- Observaciones: no puede archivarse si la compania coincide con el `tenantId` actual del token o con la compania primaria actual del usuario.

##### `PATCH /api/account/companies/{companyPublicId}/reactivate`

- Proposito: reactivar una compania propia archivada.
- Autenticacion: `Bearer` requerido.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_ALREADY_ACTIVE`, `COMPANY_REACTIVATION_LIMIT_REACHED`, `COMPANY_OWNERSHIP_FORBIDDEN`.
- Observaciones: la reactivacion vuelve a estado `Active`, pero no cambia automaticamente el tenant activo del usuario.

##### `POST /api/account/companies/{companyPublicId}/switch`

- Proposito: cambiar la compania activa del usuario autenticado.
- Autenticacion: `Bearer` requerido.
- Request body: ninguno.
- Response: `200 OK` con `SwitchActiveCompanyResponse`.
- Errores relevantes: `ACTIVE_COMPANY_SWITCH_FORBIDDEN`, `COMPANY_OWNERSHIP_FORBIDDEN`, `COMPANY_NOT_FOUND`.
- Observaciones: solo funciona si la compania destino esta `Active` y el usuario tiene membresia activa en ella; marca esa membresia como primaria, emite un nuevo token con `tid` de la compania destino y el claim `role` correspondiente a esa membresia, y devuelve `activeCompany` con `companyPublicId`, `name`, `slug`, `countryCode` y `status`.

#### 5.3.6 Relacion con `Auth` y onboarding

- `POST /api/auth/register` crea la cuenta y devuelve identidad autenticada, normalmente aun sin tenant.
- `GET /api/account/companies/countries` resuelve el catalogo global de paises requerido por el formulario.
- `GET /api/account/companies/legal-representative-document-types` resuelve el catalogo de tipos documentales requerido por el formulario.
- `GET /api/account/companies/legal-representative-position-titles` resuelve el catalogo de cargos requerido por el formulario.
- `POST /api/account/companies` crea la primera o siguiente compania propiedad de esa cuenta.
- `POST /api/account/companies/{companyPublicId}/switch` emite el token ya tenant-scoped para operar `api/v1`.

Esa secuencia explica por que `Account companies` no usa `/api/v1`: todavia esta resolviendo el paso previo al contexto tenant estable del resto del sistema.

### 5.3A Commercial plans

#### 5.3A.1 Alcance

Este bloque cubre la administracion global del catalogo comercial mediante `CommercialPlansController`, con base `/api/account/commercial-plans`.

Incluye:

- `GET /api/account/commercial-plans`
- `GET /api/account/commercial-plans/{publicId}`
- `POST /api/account/commercial-plans`
- `PUT /api/account/commercial-plans/{publicId}`
- `PATCH /api/account/commercial-plans/{publicId}/activate`
- `PATCH /api/account/commercial-plans/{publicId}/inactivate`

#### 5.3A.2 Proposito funcional en CLARIHR

El modulo `Commercial plans` sirve para:

- definir el catalogo comercial base que luego podran consumir las futuras suscripciones a empresas
- estandarizar el `fee` mensual base y el precio por empleado activo por plan
- mantener limites incluidos por plan de forma reutilizable
- administrar el estado operativo del plan (`Draft`, `Active`, `Inactive`)
- preservar un plan canonico `FREE` alineado al provisioning actual

No resuelve aun versionamiento de precios, add-ons, descuentos corporativos, activacion de suscripciones ni calculo de cobros.

#### 5.3A.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El scope es global de plataforma; no dependen de `tenantId` ni usan RBAC tenant-scoped.
- La autorizacion se resuelve por rol `platform_admin`.
- `list` soporta paginacion, filtro por `status` y busqueda libre `q` sobre codigo y nombre.
- `create` registra `code`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `status` y `limits`.
- `update` reemplaza completamente la coleccion de limites del plan; no actualiza `status`.
- Las transiciones de estado se hacen solo con `activate` e `inactivate`.
- `FREE` existe sembrado como `system plan`, con precios `0` y lista de limites vacia.
- Los planes de sistema no pueden cambiar `code` o `name`, y tampoco pueden inactivarse.
- El modulo usa concurrencia optimista mediante `concurrencyToken`.

#### 5.3A.4 Errores observables relevantes en Commercial plans

- `UNAUTHENTICATED`: `401`, autenticacion requerida.
- `COMMERCIAL_PLAN_FORBIDDEN`: `403`, el usuario autenticado no tiene rol `platform_admin`.
- `COMMERCIAL_PLAN_NOT_FOUND`: `404`, el plan solicitado no existe.
- `COMMERCIAL_PLAN_CODE_CONFLICT`: `409`, ya existe otro plan con ese `code`.
- `CONCURRENCY_CONFLICT`: `409`, el recurso fue modificado por otra solicitud.
- `COMMERCIAL_PLAN_ALREADY_ACTIVE`: `409`, el plan ya estaba activo.
- `COMMERCIAL_PLAN_ALREADY_INACTIVE`: `409`, el plan ya estaba inactivo.
- `COMMERCIAL_PLAN_SYSTEM_RENAME_FORBIDDEN`: `409`, un plan de sistema no puede cambiar `code` o `name`.
- `COMMERCIAL_PLAN_SYSTEM_INACTIVATION_FORBIDDEN`: `409`, un plan de sistema no puede inactivarse.

#### 5.3A.5 `CommercialPlansController`

Base route: `/api/account/commercial-plans`

Contratos principales:

- `CommercialPlanSummaryResponse`: `publicId`, `code`, `normalizedCode`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `status`, `isSystemPlan`, `createdAtUtc`, `modifiedAtUtc`
- `CommercialPlanResponse`: agrega `concurrencyToken` y `limits`
- `CommercialPlanLimitInput`: `code`, `value`
- `CommercialPlanLimitResponse`: `code`, `normalizedCode`, `value`

##### `GET /api/account/commercial-plans`

- Proposito: listar planes comerciales globales.
- Autenticacion: `Bearer` requerido con rol `platform_admin`.
- Query: `status`, `q`, `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`, `q` maximo `150`.
- Response: `PagedResponse<CommercialPlanSummaryResponse>`.
- Observaciones: no exige tenant activo; ordena por `name` y luego `code`.

##### `GET /api/account/commercial-plans/{publicId}`

- Proposito: obtener el detalle de un plan comercial global.
- Autenticacion: `Bearer` requerido con rol `platform_admin`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`.
- Observaciones: `limits` siempre se devuelve como coleccion, aunque este vacia.

##### `POST /api/account/commercial-plans`

- Proposito: registrar un nuevo plan comercial global.
- Autenticacion: `Bearer` requerido con rol `platform_admin`.
- Request body: `code`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `status`, `limits`.
- Validaciones base: `code` obligatorio maximo `40`; `name` obligatorio maximo `150`; `description` maximo `500`; montos y limites no negativos; maximo `2` decimales; `limits[].code` obligatorio maximo `80`.
- Response: `201 Created` con `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_CODE_CONFLICT`.
- Observaciones: `limits` puede venir vacio; el `status` inicial puede registrarse como `Draft`, `Active` o `Inactive`.

##### `PUT /api/account/commercial-plans/{publicId}`

- Proposito: actualizar datos base de un plan comercial existente.
- Autenticacion: `Bearer` requerido con rol `platform_admin`.
- Request body: `code`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `limits`, `concurrencyToken`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`, `COMMERCIAL_PLAN_CODE_CONFLICT`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_PLAN_SYSTEM_RENAME_FORBIDDEN`.
- Observaciones: la coleccion `limits` reemplaza completamente la configuracion anterior y `status` no se modifica por esta ruta.

##### `PATCH /api/account/commercial-plans/{publicId}/activate`

- Proposito: activar un plan comercial existente.
- Autenticacion: `Bearer` requerido con rol `platform_admin`.
- Request body: `concurrencyToken`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_PLAN_ALREADY_ACTIVE`.
- Observaciones: no existe ruta para volver un plan a `Draft`.

##### `PATCH /api/account/commercial-plans/{publicId}/inactivate`

- Proposito: inactivar un plan comercial existente.
- Autenticacion: `Bearer` requerido con rol `platform_admin`.
- Request body: `concurrencyToken`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_PLAN_ALREADY_INACTIVE`, `COMMERCIAL_PLAN_SYSTEM_INACTIVATION_FORBIDDEN`.
- Observaciones: `FREE` no puede inactivarse mientras siga siendo el plan canonico del provisioning.

#### 5.3A.6 Relacion con provisioning y futuras suscripciones

- `PlanEntitlementService` asegura que el plan comercial `FREE` exista y siga alineado con los `PlanEntitlement` usados por el provisioning.
- `Commercial plans` define el catalogo comercial base, pero no crea ni activa suscripciones empresariales.
- `CompanySubscription` y `PlanEntitlement` siguen referenciando `planCode`; la relacion formal con el catalogo comercial queda para historias futuras.

### 5.4 Org structure catalogs

#### 5.4.1 Alcance

Este bloque cubre `OrgStructureCatalogsController` y hoy expone tres familias de catalogos:

- `company-types` a nivel de cuenta
- `unit-types` a nivel tenant
- `functional-areas` a nivel tenant

Familias de rutas:

- `/api/account/org-structure-catalogs/company-types`
- `/api/v1/companies/{companyPublicId}/org-structure-catalogs/unit-types`
- `/api/v1/org-structure-catalogs/unit-types/{publicId}`
- `/api/v1/companies/{companyPublicId}/org-structure-catalogs/functional-areas`
- `/api/v1/org-structure-catalogs/functional-areas/{publicId}`

#### 5.4.2 Proposito funcional en CLARIHR

El modulo `Org structure catalogs` sirve para mantener catalogos base que luego usan otros modulos:

- `company-types` alimenta creacion y edicion de companias en `Account companies`
- `unit-types` alimenta la estructura organizativa y clasificaciones relacionadas
- `functional-areas` alimenta org units y otras definiciones organizativas

Es un modulo fundacional: no modela organigramas ni companias completas, sino los catalogos que esas piezas consumen.

#### 5.4.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- Este modulo mezcla dos scopes distintos: `company-types` es account-scoped, mientras `unit-types` y `functional-areas` son tenant-scoped.
- `company-types` no depende del `tenantId` para autorizar; se resuelve por usuario autenticado y ownership del catalogo.
- `unit-types` y `functional-areas` si dependen del `tenantId` y de claims tenant-scoped.
- En `unit-types` y `functional-areas`, `search/create` usan `companyPublicId` en la ruta y exigen que coincida con el tenant del token.
- En `unit-types` y `functional-areas`, `get/update/activate/inactivate` usan solo `{publicId}` y resuelven el tenant desde el token actual. Si el item existe en otro tenant, la API devuelve `TENANT_MISMATCH` en vez de `404` plano.
- Los tres catalogos comparten el mismo shape observable: `publicId`, `code`, `normalizedCode`, `name`, `description`, `sortOrder`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`.
- Todas las escrituras validan `code` con regex alfanumerica mas `_` o `-`, longitud maxima `50`, `name` maximo `150`, `description` maximo `500` y `sortOrder >= 0`.
- Los endpoints de `update`, `activate` e `inactivate` usan concurrencia optimista con `ConcurrencyToken`.
- Los endpoints de busqueda usan `isActive`, `q`, `page` y `pageSize`; el `pageSize` maximo es `100` y el default es `20`.
- La inactivacion esta protegida por reglas de uso: no puede inactivarse un item que siga referenciado por recursos dependientes.

#### 5.4.4 Autorizacion observable

- `company-types` exige autenticacion valida de cuenta; no aplica RBAC tenant para consultar o administrar.
- `unit-types` y `functional-areas` en lectura aceptan alguno de estos permisos: `OrgStructureCatalogs.Read`, `OrgStructureCatalogs.Admin`, `OrgUnits.Read`, `OrgUnits.Admin`, `iam.administration.manage` o `platform_admin`.
- `unit-types` y `functional-areas` en escritura exigen alguno de estos permisos: `OrgStructureCatalogs.Admin`, `OrgUnits.Admin`, `iam.administration.manage` o `platform_admin`.
- Si el `companyPublicId` de la ruta no coincide con el tenant actual, la respuesta observable es `TENANT_MISMATCH`.
- Si el usuario esta autenticado pero no cumple permisos tenant-scoped, la respuesta observable es `ORG_STRUCTURE_CATALOG_FORBIDDEN`.

#### 5.4.5 Errores observables relevantes en Org structure catalogs

- `UNAUTHENTICATED`: `401`, autenticacion requerida o no hay tenant valido en rutas tenant-scoped.
- `ORG_STRUCTURE_CATALOG_FORBIDDEN`: `403`, el usuario no tiene permisos sobre catalogos de estructura.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `ORG_STRUCTURE_CATALOG_NOT_FOUND`: `404`, el item solicitado no existe en el scope correcto.
- `ORG_STRUCTURE_CATALOG_CODE_CONFLICT`: `409`, ya existe otro item con el mismo `code` en el mismo scope.
- `ORG_STRUCTURE_CATALOG_IN_USE`: `409`, no puede inactivarse porque sigue referenciado.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `400` de validacion: `page/pageSize` invalidos, `code` invalido, campos obligatorios vacios o `ConcurrencyToken` ausente.

#### 5.4.6 `company-types` - catalogo account-scoped

Route family:

- `GET /api/account/org-structure-catalogs/company-types`
- `GET /api/account/org-structure-catalogs/company-types/{publicId}`
- `POST /api/account/org-structure-catalogs/company-types`
- `PUT /api/account/org-structure-catalogs/company-types/{publicId}`
- `PATCH /api/account/org-structure-catalogs/company-types/{publicId}/activate`
- `PATCH /api/account/org-structure-catalogs/company-types/{publicId}/inactivate`

Uso principal:

- clasificar companias al crearlas o actualizarlas desde `Account companies`
- mantener un catalogo propio del usuario/owner, no del tenant activo

Observaciones funcionales:

- `search` permite `isActive`, `q`, `page`, `pageSize`.
- El backend asegura de forma idempotente un catalogo base por owner durante el registro de nuevas cuentas y antes de responder `search`; si faltan seeds, inserta solo los faltantes sin duplicar codigos existentes.
- El set base inicial cubre `SA_DE_CV`, `LIMITED_LIABILITY`, `INDIVIDUAL_ENTERPRISE`, `BRANCH_OFFICE`, `COOPERATIVE`, `ASSOCIATION`, `FOUNDATION` y `PUBLIC_INSTITUTION`.
- `get by id` solo devuelve items cuyo `OwnerUserPublicId` coincide con la cuenta autenticada; si existe pero pertenece a otra cuenta, responde `ORG_STRUCTURE_CATALOG_NOT_FOUND`.
- `create` valida unicidad de `code` por owner.
- `update` exige `ConcurrencyToken` y conserva el mismo scope account-scoped.
- `activate` y `inactivate` tambien exigen `ConcurrencyToken`.
- `inactivate` falla con `ORG_STRUCTURE_CATALOG_IN_USE` si alguna compania ya usa ese tipo.
- Cuando existe `tenantId` activo, las escrituras tambien dejan auditoria tenant-scoped; cuando no existe, la operacion sigue siendo valida por tratarse de catalogo de cuenta.

#### 5.4.7 `unit-types` - catalogo tenant-scoped

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-structure-catalogs/unit-types`
- `GET /api/v1/org-structure-catalogs/unit-types/{publicId}`
- `POST /api/v1/companies/{companyPublicId}/org-structure-catalogs/unit-types`
- `PUT /api/v1/org-structure-catalogs/unit-types/{publicId}`
- `PATCH /api/v1/org-structure-catalogs/unit-types/{publicId}/activate`
- `PATCH /api/v1/org-structure-catalogs/unit-types/{publicId}/inactivate`

Uso principal:

- alimentar tipos de unidad organizativa que luego usa `OrgUnits`
- servir como catalogo base para clasificaciones relacionadas con puestos/estructura

Observaciones funcionales:

- `search` y `create` usan `companyPublicId` en la ruta y exigen que coincida con el tenant del token.
- `get/update/activate/inactivate` usan el `tenantId` del token actual y el `publicId` del item.
- Si el item existe pero pertenece a otro tenant, la API devuelve `TENANT_MISMATCH`.
- `create` asigna `TenantId = companyPublicId` al nuevo item.
- `update`, `activate` e `inactivate` exigen `ConcurrencyToken`.
- `inactivate` falla con `ORG_STRUCTURE_CATALOG_IN_USE` si el item esta siendo usado por org units o por position category classifications.
- Todas las escrituras generan auditoria de tenant.

#### 5.4.8 `functional-areas` - catalogo tenant-scoped

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-structure-catalogs/functional-areas`
- `GET /api/v1/org-structure-catalogs/functional-areas/{publicId}`
- `POST /api/v1/companies/{companyPublicId}/org-structure-catalogs/functional-areas`
- `PUT /api/v1/org-structure-catalogs/functional-areas/{publicId}`
- `PATCH /api/v1/org-structure-catalogs/functional-areas/{publicId}/activate`
- `PATCH /api/v1/org-structure-catalogs/functional-areas/{publicId}/inactivate`

Uso principal:

- mantener areas funcionales del tenant para la estructura organizativa

Observaciones funcionales:

- comparte el mismo patron de autorizacion y scoping que `unit-types`.
- `search` y `create` trabajan con `companyPublicId` en la ruta; `get/update/activate/inactivate` operan por `publicId` usando el tenant actual del token.
- `create` asigna `TenantId = companyPublicId`.
- `update`, `activate` e `inactivate` exigen `ConcurrencyToken`.
- `inactivate` falla con `ORG_STRUCTURE_CATALOG_IN_USE` si alguna org unit sigue usando el area funcional.
- Todas las escrituras generan auditoria de tenant.

#### 5.4.9 Relacion con otros modulos

- `Account companies` consume `company-types` para clasificar companias.
- `OrgUnits` consume `unit-types` y `functional-areas`.
- Modulos posteriores de diseno organizativo y de puestos dependen indirectamente de estos catalogos.

Por eso este modulo aparece temprano en el flujo documental: define catalogos base que otros modulos reutilizan, pero no reemplaza la administracion de organigramas ni de companias.

### 5.5 OrgUnits

#### 5.5.1 Alcance

Este bloque cubre `OrgUnitsController` y expone la administracion operativa de unidades organizativas del tenant.

Familias de rutas:

- `/api/v1/companies/{companyPublicId}/org-units`
- `/api/v1/org-units/{publicId}`
- `/api/v1/companies/{companyPublicId}/org-units/tree`
- `/api/v1/companies/{companyPublicId}/org-units/graph`
- `/api/v1/companies/{companyPublicId}/org-units/export`
- `/api/v1/companies/{companyPublicId}/org-units/diagram-export`
- `/api/v1/org-units/{publicId}/move`
- `/api/v1/org-units/{publicId}/activate`
- `/api/v1/org-units/{publicId}/inactivate`

#### 5.5.2 Proposito funcional en CLARIHR

El modulo `OrgUnits` modela el organigrama operativo del tenant. Su funcion es crear y mantener las unidades que componen la estructura organizativa real de la compania, con relaciones padre-hijo, tipo de unidad, area funcional, centro de costo, responsable y estado activo.

No es un catalogo base como `Org structure catalogs`; aqui ya se administran nodos concretos del arbol organizativo que luego consumen modulos laborales, reportes y vistas de estructura.

#### 5.5.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El modulo es tenant-scoped por defecto. En rutas con `companyPublicId`, ese valor debe coincidir con el tenant activo del token.
- `search`, `tree`, `graph`, `export`, `diagram-export` y `create` usan `companyPublicId` en la ruta.
- `get by id`, `update`, `move`, `activate` e `inactivate` usan solo `{publicId}` y resuelven el tenant desde el token actual.
- Si una org unit existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver un `404` plano.
- El shape principal de lectura es `OrgUnitResponse`: `publicId`, `code`, `normalizedCode`, `name`, `orgUnitType`, `functionalArea`, `parentPublicId`, `sortOrder`, `description`, `costCenterCode`, `managerEmployeePublicId`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc` y opcionalmente `allowedActions`.
- `code` es obligatorio, maximo `50`, y debe cumplir regex alfanumerica con `_` o `-`.
- `name` es obligatorio y maximo `150`.
- `description` acepta hasta `500` caracteres.
- `sortOrder` debe ser `>= 0`.
- `pageSize` maximo es `100`, el default es `20`.
- La profundidad maxima soportada para arbol y grafo es `15`.
- `update`, `move`, `activate` e `inactivate` usan concurrencia optimista con `ConcurrencyToken`.
- Todas las escrituras y ambos exportes generan auditoria.

#### 5.5.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `OrgUnits.Read`, `OrgUnits.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura acepta alguno de estos permisos: `OrgUnits.Admin`, `iam.administration.manage` o `platform_admin`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyPublicId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `ORG_UNITS_FORBIDDEN`.

#### 5.5.5 Errores observables relevantes en OrgUnits

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido para rutas tenant-scoped.
- `ORG_UNITS_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de org units.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `ORG_UNIT_NOT_FOUND`: `404`, la org unit o `rootId` solicitado no existe en el scope correcto.
- `ORG_UNIT_PARENT_NOT_FOUND`: `404`, el `parentPublicId` o `newParentPublicId` solicitado no existe en el tenant correcto.
- `ORG_UNIT_TYPE_NOT_FOUND`: `404`, el `orgUnitTypePublicId` no existe o esta inactivo en los catalogos del tenant correcto.
- `FUNCTIONAL_AREA_NOT_FOUND`: `404`, el `functionalAreaPublicId` no existe o esta inactivo en los catalogos del tenant correcto.
- `ORG_UNIT_CODE_CONFLICT`: `409`, otra org unit del mismo tenant ya usa ese `code`.
- `ORG_UNIT_CYCLE_DETECTED`: `409`, el movimiento solicitado crearia un ciclo en la jerarquia.
- `ORG_UNIT_DEPTH_LIMIT_EXCEEDED`: `409`, la nueva ubicacion excede los `15` niveles soportados.
- `ORG_UNIT_HAS_ACTIVE_CHILDREN`: `409`, no puede inactivarse porque todavia tiene hijos activos.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `ORG_UNIT_COST_CENTER_INVALID`: `422`, el `costCenterCode` no existe o esta inactivo para la compania.
- `REPORT_FORMAT_NOT_SUPPORTED`: `400`, `format` no soportado en export o diagram-export.
- `400` de validacion: `page/pageSize` invalidos, `depth` fuera de `1..15`, `code` invalido, ids vacios o `ConcurrencyToken` ausente.

#### 5.5.6 Search y detalle de org units

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-units`
- `GET /api/v1/org-units/{publicId}`

Uso principal:

- listar org units para administracion operativa del organigrama
- abrir el detalle canonical de una org unit concreta

Observaciones funcionales:

- `search` soporta filtros `isActive`, `orgUnitTypePublicId`, `functionalAreaPublicId`, `parentPublicId`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `code`, `name`, tipo de unidad, area funcional y nombre del padre.
- el orden observable del listado es `sortOrder`, luego `name`, luego `code`.
- `search` devuelve `PagedResponse<OrgUnitResponse>`.
- `get by id` devuelve una sola `OrgUnitResponse`.
- `get by id` siempre calcula `allowedActions` usando el estado activo y la existencia de hijos activos.
- `search` solo calcula `allowedActions` si `includeAllowedActions=true`.

#### 5.5.7 Jerarquia y visualizacion estructural

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-units/tree`
- `GET /api/v1/companies/{companyPublicId}/org-units/graph`

Uso principal:

- renderizar arboles de navegacion del organigrama
- alimentar vistas graficas o diagramas de relaciones padre-hijo

Observaciones funcionales:

- ambos endpoints aceptan `rootPublicId` opcional y `depth` opcional.
- si no se envian `rootPublicId` ni `depth`, el resultado cubre toda la jerarquia del tenant.
- si se envia `rootPublicId`, la respuesta se acota a ese subarbol.
- `depth` debe estar entre `1` y `15`.
- `tree` devuelve nodos anidados con `children`.
- `graph` devuelve `nodes` y `edges`.
- en `graph`, cada edge va de `parentPublicId` a `childPublicId`.
- el orden observable de nodos y ramas sigue `sortOrder`, luego `name`, luego `code`.
- si `rootPublicId` existe en otro tenant, la API devuelve `TENANT_MISMATCH`; si no existe, devuelve `ORG_UNIT_NOT_FOUND`.

#### 5.5.8 Exportes

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-units/export`
- `GET /api/v1/companies/{companyPublicId}/org-units/diagram-export`

Uso principal:

- exportar la estructura para analisis, intercambio o respaldo operativo

Observaciones funcionales:

- `export` soporta `format=csv|xlsx` y reutiliza los mismos filtros de busqueda operativa, salvo paginacion.
- `export` devuelve filas planas con columnas de tipo de unidad, area funcional, padre, centro de costo, responsable y timestamps.
- `diagram-export` soporta `format=graphml|json|dot`.
- `diagram-export` reutiliza `rootPublicId` y `depth` del endpoint `graph`.
- ambos endpoints generan auditoria de tipo `ReportExported`.
- si `format` no es soportado, la respuesta observable es `REPORT_FORMAT_NOT_SUPPORTED`.

#### 5.5.9 Escrituras: create, update, move, activate e inactivate

Route family:

- `POST /api/v1/companies/{companyPublicId}/org-units`
- `PUT /api/v1/org-units/{publicId}`
- `PATCH /api/v1/org-units/{publicId}/move`
- `PATCH /api/v1/org-units/{publicId}/activate`
- `PATCH /api/v1/org-units/{publicId}/inactivate`

Uso principal:

- crear nuevas unidades del organigrama
- corregir datos operativos
- reorganizar la jerarquia
- controlar el estado activo de cada nodo

Observaciones funcionales:

- `create` devuelve `201 Created` con la `OrgUnitResponse` creada.
- `create` exige `code`, `name`, `orgUnitTypePublicId` y valida opcionalmente `functionalAreaPublicId`, `parentPublicId`, `sortOrder`, `description`, `costCenterCode` y `managerEmployeePublicId`.
- `create` exige que `orgUnitTypePublicId` y `functionalAreaPublicId` apunten a catalogos activos del tenant.
- `create` valida unicidad de `code` por tenant.
- `create` valida que `costCenterCode`, si se envia, exista y este activo en la compania.
- `create` valida `parentPublicId` dentro del tenant correcto y rechaza profundidades mayores a `15`.
- `update` modifica datos escalares de la org unit, pero no cambia el padre; para eso existe `move`.
- `update` exige `ConcurrencyToken`.
- `move` permite cambiar `newParentPublicId` y opcionalmente `sortOrder`.
- `move` exige `ConcurrencyToken`, rechaza mover una unidad bajo si misma y rechaza ciclos indirectos.
- `move` tambien valida que la nueva ubicacion no exceda la profundidad maxima soportada.
- `activate` exige `ConcurrencyToken` y reactiva la org unit.
- `inactivate` exige `ConcurrencyToken` y falla si la unidad todavia tiene hijos activos.

#### 5.5.10 Relacion con otros modulos

- `Org structure catalogs` provee `unit-types` y `functional-areas`, dependencias directas de este modulo.
- `Cost centers` se integra via `costCenterCode`; una org unit no puede guardar un centro de costo inexistente o inactivo.
- Modulos posteriores de puestos, personal y reporting consumen la estructura resultante para asignaciones y visualizacion.

`OrgUnits` es el primer modulo donde el tenant deja de configurar catalogos y pasa a modelar su estructura organizativa real.

### 5.6 CostCenters

#### 5.6.1 Alcance

Este bloque cubre `CostCentersController` y expone la administracion tenant-scoped de centros de costo.

Familias de rutas:

- `/api/v1/companies/{companyId}/cost-centers`
- `/api/v1/cost-centers/{id}`
- `/api/v1/cost-centers/{id}/usage`
- `/api/v1/companies/{companyId}/cost-centers/export`
- `/api/v1/cost-centers/{id}/activate`
- `/api/v1/cost-centers/{id}/inactivate`

#### 5.6.2 Proposito funcional en CLARIHR

El modulo `CostCenters` mantiene el catalogo operativo de centros de costo del tenant. Su funcion es clasificar costos laborales y organizativos para que otros modulos puedan referenciar un codigo valido al asignar organigramas, posiciones y movimientos relacionados con gasto.

No modela jerarquias; modela un catalogo operativo con estado, tipo y codigos contables auxiliares.

#### 5.6.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El modulo es tenant-scoped por defecto. En rutas con `companyId`, ese valor debe coincidir con el tenant activo del token.
- `search`, `export` y `create` usan `companyId` en la ruta.
- `get by id`, `usage`, `update`, `activate` e `inactivate` usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un centro de costo existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver `404` plano.
- `CostCenterListItemResponse` devuelve `id`, `code`, `name`, `type`, codigos contables opcionales, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc` y opcionalmente `allowedActions`.
- `CostCenterResponse` agrega `companyId` y `description`.
- `CostCenterUsageResponse` devuelve contadores de referencias activas e inactivas desde `OrgUnits` y `PositionSlots`, mas `hasActiveReferences`.
- `code` es obligatorio, maximo `50`, y debe cumplir regex alfanumerica con `_` o `-`.
- `name` es obligatorio y maximo `150`.
- `description` acepta hasta `500` caracteres.
- Los tres account codes opcionales aceptan hasta `100` caracteres y usan regex alfanumerica con `_`, `.` o `-`.
- `pageSize` maximo es `100`, el default es `20`.
- `update`, `activate` e `inactivate` usan concurrencia optimista con `ConcurrencyToken`.
- Todas las escrituras y el export generan auditoria.
- El campo `type` usa el enum `CostCenterType`: `SalaryExpense`, `EmployerContribution`, `ProvisionReserve` y `Mixed`.

#### 5.6.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `CostCenters.Read`, `CostCenters.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura acepta alguno de estos permisos: `CostCenters.Admin`, `iam.administration.manage` o `platform_admin`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `COST_CENTERS_FORBIDDEN`.

#### 5.6.5 Errores observables relevantes en CostCenters

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido para rutas tenant-scoped.
- `COST_CENTERS_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de centros de costo.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `COST_CENTER_NOT_FOUND`: `404`, el centro de costo solicitado no existe en el scope correcto.
- `COST_CENTER_CODE_CONFLICT`: `409`, otro centro de costo del mismo tenant ya usa ese `code`.
- `COST_CENTER_IN_USE`: `409`, no puede inactivarse porque sigue referenciado por org units activas o position slots activas.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `COST_CENTER_EXPORT_FORMAT_INVALID`: `400`, `format` no soportado en el export.
- `400` de validacion: `page/pageSize` invalidos, `code` invalido, account codes invalidos, ids vacios o `ConcurrencyToken` ausente.

#### 5.6.6 Search y detalle de cost centers

Route family:

- `GET /api/v1/companies/{companyId}/cost-centers`
- `GET /api/v1/cost-centers/{id}`

Uso principal:

- listar el catalogo operativo de centros de costo
- abrir el detalle canonical de un centro de costo concreto

Observaciones funcionales:

- `search` soporta filtros `type`, `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca solo por `code` y `name`.
- el orden observable del listado es `name`, luego `code`.
- `search` devuelve `PagedResponse<CostCenterListItemResponse>`.
- `get by id` devuelve `CostCenterResponse`.
- `get by id` siempre calcula `allowedActions` usando el estado activo y la existencia de referencias activas.
- `search` solo calcula `allowedActions` si `includeAllowedActions=true`.

#### 5.6.7 Uso y dependencias activas

Route family:

- `GET /api/v1/cost-centers/{id}/usage`

Uso principal:

- inspeccionar antes de inactivar si un centro de costo sigue siendo usado por otros modulos

Observaciones funcionales:

- devuelve conteos separados para `OrgUnits` activas e inactivas.
- devuelve conteos separados para `PositionSlots` activas e inactivas.
- `hasActiveReferences` se vuelve `true` si existe al menos una referencia activa en cualquiera de esos dos modulos.
- la logica de bloqueo de `inactivate` se basa en referencias activas, no en referencias historicas inactivas.

#### 5.6.8 Exportes

Route family:

- `GET /api/v1/companies/{companyId}/cost-centers/export`

Uso principal:

- exportar el catalogo de centros de costo para analisis, conciliacion o trabajo operativo fuera del sistema

Observaciones funcionales:

- soporta `format=csv|xlsx`.
- reutiliza los mismos filtros de busqueda operativa, salvo paginacion.
- devuelve filas planas con `code`, `name`, `type`, account codes, `description`, `isActive` y timestamps.
- genera auditoria de tipo `ReportExported`.
- si `format` no es soportado, la respuesta observable es `COST_CENTER_EXPORT_FORMAT_INVALID`.

#### 5.6.9 Escrituras: create, update, activate e inactivate

Route family:

- `POST /api/v1/companies/{companyId}/cost-centers`
- `PUT /api/v1/cost-centers/{id}`
- `PATCH /api/v1/cost-centers/{id}/activate`
- `PATCH /api/v1/cost-centers/{id}/inactivate`

Uso principal:

- crear centros de costo del tenant
- corregir datos operativos y contables
- controlar el estado activo del catalogo

Observaciones funcionales:

- `create` devuelve `201 Created` con la `CostCenterResponse` creada.
- `create` exige `code`, `name` y `type`; acepta `payrollExpenseAccountCode`, `employerContributionAccountCode`, `provisionAccountCode` y `description`.
- `create` valida unicidad de `code` por tenant.
- `update` modifica datos escalares del centro de costo, exige `ConcurrencyToken` y puede cambiar `type` y account codes.
- `activate` exige `ConcurrencyToken` y reactiva el centro de costo.
- `inactivate` exige `ConcurrencyToken` y falla si existe uso activo en `OrgUnits` o `PositionSlots`.

#### 5.6.10 Relacion con otros modulos

- `OrgUnits` puede referenciar centros de costo por `costCenterCode`.
- `PositionSlots` tambien puede referenciarlos por `costCenterCode`.
- La respuesta `usage` existe precisamente para hacer visible esa dependencia cruzada antes de desactivar un codigo.

`CostCenters` cierra el bloque organizacional base junto con `Org structure catalogs` y `OrgUnits`: primero se definen catalogos, luego la estructura y luego las claves de imputacion de costo que esa estructura y los puestos reutilizan.

### 5.7 Legal representatives

#### 5.7.1 Alcance

Este bloque cubre `LegalRepresentativesController` y expone la administracion tenant-scoped de representantes legales de la compania.

Familias de rutas:

- `/api/v1/companies/{companyId}/legal-representatives`
- `/api/v1/legal-representatives/{id}`
- `/api/v1/legal-representatives/{id}/usage`
- `/api/v1/companies/{companyId}/legal-representatives/export`
- `/api/v1/legal-representatives/{id}/activate`
- `/api/v1/legal-representatives/{id}/inactivate`
- `/api/v1/legal-representatives/{id}/set-primary`

#### 5.7.2 Proposito funcional en CLARIHR

El modulo `Legal representatives` mantiene las personas que representan legalmente a la compania dentro del tenant. Su funcion es registrar identidad, documento, cargo, tipo de representacion, vigencia, datos de contacto y cual representante activo es el principal.

Es un modulo administrativo sensible porque afecta onboarding de compania, datos corporativos y futuras referencias documentales o contractuales.

#### 5.7.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El modulo es tenant-scoped por defecto. En rutas con `companyId`, ese valor debe coincidir con el tenant activo del token.
- `search`, `export` y `create` usan `companyId` en la ruta.
- `get by id`, `usage`, `update`, `activate`, `inactivate` y `set-primary` usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un representante legal existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver `404` plano.
- `LegalRepresentativeListItemResponse` devuelve `companyId`, nombre, documento, cargo, `representationType`, `isPrimary`, `isActive`, vigencias, `concurrencyToken`, timestamps y opcionalmente `allowedActions`.
- `LegalRepresentativeResponse` agrega `authorityDescription`, `appointmentInstrument`, `appointmentDateUtc`, `email` y `phone`.
- `isPrimary` es nullable en respuestas. `true` significa primario vigente; `false`, no primario; `null`, prioridad no especificada en el alta original.
- `LegalRepresentativeUsageResponse` hoy devuelve `legalRepresentativeId`, `activeDocumentReferencesCount` y `canInactivate`.
- `firstName` y `lastName` son obligatorios, maximo `100`, y usan validacion de nombre.
- `documentNumber` es obligatorio, maximo `80`, y usa regex alfanumerica con `_`, `.`, `/` o `-`.
- `positionTitle` es obligatorio, maximo `150`, y usa regex controlada.
- `authorityDescription` y `appointmentInstrument` aceptan hasta `500` caracteres.
- `email` es opcional, pero si se envia debe ser email valido y maximo `320`.
- `phone` es opcional y maximo `40`.
- `effectiveFromUtc` es obligatorio.
- `effectiveToUtc` es opcional, pero no puede ser menor que `effectiveFromUtc`.
- `pageSize` maximo es `100`, el default es `20`.
- `update`, `activate`, `inactivate` y `set-primary` usan concurrencia optimista con `ConcurrencyToken`.
- Todas las escrituras y el export generan auditoria.
- El sistema exige que siempre exista al menos un representante legal activo por compania.
- Solo puede existir un representante activo marcado como primario a la vez.

#### 5.7.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `LegalRepresentatives.Read`, `LegalRepresentatives.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura acepta alguno de estos permisos: `LegalRepresentatives.Admin`, `iam.administration.manage` o `platform_admin`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `LEGAL_REPRESENTATIVES_FORBIDDEN`.

#### 5.7.5 Errores observables relevantes en Legal representatives

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido para rutas tenant-scoped.
- `LEGAL_REPRESENTATIVES_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de representantes legales.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `LEGAL_REPRESENTATIVE_NOT_FOUND`: `404`, el representante solicitado no existe en el scope correcto.
- `LEGAL_REPRESENTATIVE_DOCUMENT_CONFLICT`: `409`, otro representante del mismo tenant ya usa esa combinacion de tipo y numero de documento.
- `LEGAL_REPRESENTATIVE_ACTIVE_MIN_REQUIRED`: `409`, no puede inactivarse el ultimo representante activo de la compania.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `LEGAL_REPRESENTATIVE_EFFECTIVE_DATES_INVALID`: `422`, `effectiveToUtc` es menor que `effectiveFromUtc`.
- `LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION`: `422`, la operacion no es valida para el estado actual, por ejemplo marcar primario un registro inactivo.
- `LEGAL_REPRESENTATIVE_EXPORT_FORMAT_INVALID`: `400`, `format` no soportado en el export.
- `400` de validacion: `page/pageSize` invalidos, nombres invalidos, `documentNumber` invalido, `positionTitle` invalido, email invalido, ids vacios o `ConcurrencyToken` ausente.

#### 5.7.6 Search y detalle de legal representatives

Route family:

- `GET /api/v1/companies/{companyId}/legal-representatives`
- `GET /api/v1/legal-representatives/{id}`

Uso principal:

- listar representantes legales de la compania
- abrir el detalle canonical de un representante concreto

Observaciones funcionales:

- `search` soporta filtros `isActive`, `isPrimary`, `representationType`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `fullName`, `positionTitle` y `documentNumber`.
- el orden observable del listado prioriza los registros con `isPrimary = true` y luego `fullName`; `false` y `null` no se tratan como primarios.
- `search` devuelve `PagedResponse<LegalRepresentativeListItemResponse>`.
- `get by id` devuelve `LegalRepresentativeResponse`.
- `search` solo calcula `allowedActions` si `includeAllowedActions=true`.
- `search` aplica acciones permitidas basicas por estado y tipo, pero no incorpora la regla de minimo activo.
- `get by id` si incorpora la regla de minimo activo y puede devolver `CanInactivate = false` en `allowedActions` con la razon `At least one active legal representative is required.`.

#### 5.7.7 Uso y restriccion de inactivacion

Route family:

- `GET /api/v1/legal-representatives/{id}/usage`

Uso principal:

- verificar si el representante puede inactivarse sin romper la invariante minima del modulo

Observaciones funcionales:

- hoy `activeDocumentReferencesCount` se devuelve en `0`; el contrato existe, pero la implementacion actual no contabiliza referencias documentales activas.
- `canInactivate` depende de una sola regla observable hoy: si el registro ya esta inactivo, devuelve `true`; si esta activo, solo devuelve `true` cuando la compania tiene mas de un representante activo.
- la inactivacion no se bloquea por ser primario; al inactivarse, el dominio limpia `IsPrimary`.

#### 5.7.8 Exportes

Route family:

- `GET /api/v1/companies/{companyId}/legal-representatives/export`

Uso principal:

- exportar el registro de representantes legales para trabajo operativo, soporte legal o auditoria documental

Observaciones funcionales:

- soporta `format=csv|xlsx`.
- reutiliza los mismos filtros de busqueda operativa, salvo paginacion.
- devuelve filas planas con nombre, documento, cargo, tipo de representacion, vigencias, contacto, `isPrimary`, `isActive` y timestamps. Cuando `isPrimary` es `null`, el valor sale vacio en el export.
- genera auditoria de tipo `ReportExported`.
- si `format` no es soportado, la respuesta observable es `LEGAL_REPRESENTATIVE_EXPORT_FORMAT_INVALID`.

#### 5.7.9 Escrituras: create, update, activate, inactivate y set-primary

Route family:

- `POST /api/v1/companies/{companyId}/legal-representatives`
- `PUT /api/v1/legal-representatives/{id}`
- `PATCH /api/v1/legal-representatives/{id}/activate`
- `PATCH /api/v1/legal-representatives/{id}/inactivate`
- `PATCH /api/v1/legal-representatives/{id}/set-primary`

Uso principal:

- crear y mantener representantes legales de la compania
- controlar vigencia operativa
- designar el representante principal activo

Observaciones funcionales:

- `create` devuelve `201 Created` con la `LegalRepresentativeResponse` creada.
- `create` exige identidad, documento, cargo, tipo de representacion y `effectiveFromUtc`; acepta descripcion de autoridad, instrumento, fecha de nombramiento, contacto y `isPrimary`.
- `create` valida unicidad por `documentType + documentNumber` dentro del tenant.
- si `create` recibe `isPrimary=true`, el sistema quita la marca primaria al representante activo actual en la misma transaccion; no devuelve conflicto por primario duplicado.
- `update` exige `ConcurrencyToken` y puede cambiar todos los datos escalares, incluida la bandera `isPrimary`.
- `update` falla con `LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION` si se intenta dejar como primario un representante inactivo.
- `activate` exige `ConcurrencyToken` y reactiva el registro, pero no lo convierte automaticamente en primario.
- `inactivate` exige `ConcurrencyToken`, limpia `isPrimary` y falla si dejaria a la compania sin representantes activos.
- `set-primary` exige `ConcurrencyToken`, solo funciona sobre registros activos y desplaza al primario activo anterior en la misma transaccion.

#### 5.7.10 Relacion con otros modulos

- `Account companies` expone los catalogos auxiliares de `document type` y `representation type` en `/api/account/companies/legal-representative-document-types` y `/api/account/companies/legal-representative-representation-types`.
- `Account companies` tambien consume `InitialLegalRepresentativeInput` durante la creacion de una compania, donde `isPrimary` ahora es opcional.
- Los detalles de compania reutilizan resumenes de representantes activos para mostrar la representacion vigente del tenant.

`Legal representatives` queda asi como el modulo operativo posterior al onboarding: los catalogos se consultan desde `Account companies`, pero la administracion continua del registro legal ya ocurre aqui.

### 5.8 Locations

#### 5.8.1 Alcance

Este bloque cubre cinco controladores que juntos modelan la estructura geografica y operativa del tenant:

- `LocationHierarchyController`
- `LocationLevelsController`
- `LocationGroupsController`
- `WorkCenterTypesController`
- `WorkCentersController`

Familias de rutas:

- `/api/v1/companies/{companyId}/location-hierarchy`
- `/api/v1/companies/{companyId}/location-levels`
- `/api/v1/location-levels/{id}`
- `/api/v1/companies/{companyId}/location-groups`
- `/api/v1/companies/{companyId}/location-groups/tree`
- `/api/v1/location-groups/{id}`
- `/api/v1/companies/{companyId}/work-center-types`
- `/api/v1/work-center-types/{id}`
- `/api/v1/companies/{companyId}/work-centers`
- `/api/v1/work-centers/{id}`

#### 5.8.2 Proposito funcional en CLARIHR

El modulo `Locations` define donde existe operativamente la compania dentro del tenant. Su funcion es configurar:

- si la jerarquia territorial es multi-nivel o no
- que niveles geografico-operativos existen
- que grupos de ubicacion concretos se registran en cada nivel
- que tipos de work center existen
- y en que grupo se ubica cada work center

Es el modulo base de localizacion del tenant. Primero define la estructura, luego los nodos geografico-organizativos y finalmente los work centers utilizables por otros modulos.

#### 5.8.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- Todo el modulo es tenant-scoped.
- Las rutas de coleccion usan `companyId` en la ruta y exigen que coincida con el tenant activo del token.
- Las rutas por recurso usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un recurso existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver `404` plano.
- Todos los listados usan `page/pageSize` con maximo `100` y default `20` cuando aplican.
- Los `code` de grupos, tipos y work centers usan regex alfanumerica con `_` o `-`, longitud maxima `50`.
- Las operaciones de `update/activate/inactivate/move/reassign-group` usan concurrencia optimista con `ConcurrencyToken`.
- Todo el modulo comparte una sola superficie de permisos: `Locations.Read`, `Locations.Admin`, `iam.administration.manage` o `platform_admin`.
- La jerarquia admite una unica regla fuerte para work centers: solo el ultimo nivel activo puede tener `AllowsWorkCenters = true`.
- El modulo soporta configuracion `defaultGroupCode/defaultGroupName` en la jerarquia y protege grupos marcados como `IsDefault` si existen.

#### 5.8.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `Locations.Read`, `Locations.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura acepta alguno de estos permisos: `Locations.Admin`, `iam.administration.manage` o `platform_admin`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `LOCATIONS_FORBIDDEN`.

#### 5.8.5 Errores observables relevantes en Locations

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido.
- `LOCATIONS_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de ubicaciones.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `LOCATION_HIERARCHY_NOT_FOUND`: `404`, no existe configuracion de jerarquia para la compania.
- `LOCATION_LEVEL_NOT_FOUND`: `404`, el nivel solicitado no existe o no esta disponible para la operacion.
- `LOCATION_GROUP_NOT_FOUND`: `404`, el grupo solicitado no existe en el scope correcto.
- `WORK_CENTER_TYPE_NOT_FOUND`: `404`, el tipo de work center solicitado no existe en el scope correcto.
- `WORK_CENTER_NOT_FOUND`: `404`, el work center solicitado no existe en el scope correcto.
- `LOCATION_LEVEL_ORDER_CONFLICT`: `409`, otro nivel ya usa ese `LevelOrder`.
- `LOCATION_GROUP_CODE_CONFLICT`: `409`, otro grupo ya usa ese `code`.
- `WORK_CENTER_TYPE_CODE_CONFLICT`: `409`, otro tipo ya usa ese `code`.
- `WORK_CENTER_CODE_CONFLICT`: `409`, otro work center ya usa ese `code`.
- `LOCATION_GROUP_PARENT_REQUIRED`: `409`, un grupo no root no puede existir sin padre.
- `LOCATION_GROUP_INVALID_PARENT`: `409`, el padre no corresponde al nivel esperado.
- `LOCATION_GROUP_CYCLE_DETECTED`: `409`, el movimiento solicitado crearia un ciclo.
- `LOCATION_GROUP_HAS_ACTIVE_CHILDREN`: `409`, no puede inactivarse porque tiene hijos activos.
- `LOCATION_GROUP_HAS_ACTIVE_WORK_CENTERS`: `409`, no puede inactivarse porque tiene work centers activos.
- `DEFAULT_GROUP_PROTECTED`: `409`, un grupo marcado como default no puede renombrarse, recodificarse ni inactivarse.
- `LAST_ACTIVE_LEVEL_REQUIRED`: `409`, no puede dejarse al tenant sin niveles activos.
- `LOCATION_LEVEL_REQUIRED_ACTIVE`: `409`, un nivel requerido debe permanecer activo.
- `WORK_CENTERS_ALLOWED_ONLY_ON_LAST_LEVEL`: `409`, solo el ultimo nivel activo puede permitir work centers.
- `LOCATION_LEVEL_HAS_ACTIVE_GROUPS`: `409`, no puede inactivarse un nivel con grupos activos.
- `LOCATION_LEVEL_ALLOWS_WORK_CENTERS_IN_USE`: `409`, no puede quitarse `AllowsWorkCenters` a un nivel si grupos activos de ese nivel sostienen work centers activos.
- `WORK_CENTER_TYPE_IN_USE`: `409`, no puede inactivarse un tipo si work centers activos aun lo usan.
- `WORK_CENTER_TYPE_INACTIVE`: `409`, no puede asignarse un tipo inactivo a un work center.
- `LOCATION_GROUP_INACTIVE`: `409`, no puede asignarse un grupo inactivo a un work center.
- `LOCATION_GROUP_PARENT_INACTIVE`: `409`, no puede crearse o moverse bajo un padre inactivo.
- `LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`: `409`, el grupo seleccionado pertenece a un nivel que no permite work centers.
- `WORK_CENTER_ADDRESS_REQUIRED`: `400`, el tipo seleccionado exige `Address`.
- `WORK_CENTER_GEO_REQUIRED`: `400`, el tipo seleccionado exige `GeoLat` y `GeoLong`.
- `WORK_CENTER_INVALID_COORDINATES`: `400`, latitud o longitud fuera de rango.
- `LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL`: `409`, no puede activarse modo single-level si no existe exactamente un nivel activo.
- `400` de validacion: `page/pageSize` invalidos, `LevelOrder <= 0`, `code` invalido, ids vacios, email invalido o `ConcurrencyToken` ausente.

#### 5.8.6 Jerarquia de ubicacion

Route family:

- `GET /api/v1/companies/{companyId}/location-hierarchy`
- `PUT /api/v1/companies/{companyId}/location-hierarchy`

Uso principal:

- consultar o cambiar la configuracion global de jerarquia de ubicaciones del tenant

Observaciones funcionales:

- `get` devuelve `LocationHierarchyConfigResponse` con `isMultiLevel`, `defaultGroupCode`, `defaultGroupName` y `concurrencyToken`.
- `update` solo modifica `isMultiLevel`.
- `update` exige `ConcurrencyToken`.
- si se intenta pasar a single-level, la API exige que exista exactamente un nivel activo.

#### 5.8.7 Location levels

Route family:

- `GET /api/v1/companies/{companyId}/location-levels`
- `POST /api/v1/companies/{companyId}/location-levels`
- `PUT /api/v1/location-levels/{id}`
- `PATCH /api/v1/location-levels/{id}/activate`
- `PATCH /api/v1/location-levels/{id}/inactivate`

Uso principal:

- definir y mantener los niveles del modelo territorial del tenant

Observaciones funcionales:

- `list` devuelve todos los niveles ordenados por `LevelOrder`.
- `create` exige `LevelOrder > 0`, `DisplayName`, `IsActive`, `IsRequired` y `AllowsWorkCenters`.
- un nivel requerido debe estar activo.
- un nivel que permite work centers tambien debe estar activo.
- `create` y `update` bloquean configuraciones donde mas de un nivel activo permita work centers o donde ese nivel no sea el ultimo activo.
- `update` exige `ConcurrencyToken`.
- `update/inactivate` bloquean desactivar el ultimo nivel activo.
- `update/inactivate` bloquean desactivar niveles requeridos.
- `update/inactivate` bloquean desactivar niveles con grupos activos.
- `update` tambien puede bloquear quitar `AllowsWorkCenters` si grupos activos de ese nivel sostienen work centers activos.

#### 5.8.8 Location groups

Route family:

- `GET /api/v1/companies/{companyId}/location-groups/tree`
- `GET /api/v1/companies/{companyId}/location-groups`
- `POST /api/v1/companies/{companyId}/location-groups`
- `PUT /api/v1/location-groups/{id}`
- `PATCH /api/v1/location-groups/{id}/move`
- `PATCH /api/v1/location-groups/{id}/activate`
- `PATCH /api/v1/location-groups/{id}/inactivate`

Uso principal:

- registrar los nodos concretos de la jerarquia territorial

Observaciones funcionales:

- `tree` devuelve la jerarquia completa anidada.
- `search` soporta `levelOrder`, `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code` y `name`.
- `search` ordena por `LevelOrder` y luego por `Name`.
- `tree` tambien ordena por `LevelOrder` y luego por `Name`.
- `create` exige que el `LevelOrder` exista y este activo.
- grupos de `LevelOrder = 1` no pueden tener padre.
- grupos de niveles superiores exigen padre activo del nivel inmediatamente anterior.
- `move` mantiene fijo el `LevelOrder` del grupo; solo cambia el padre.
- `move` detecta ciclos y bloquea mover bajo un descendiente.
- `update` protege grupos `IsDefault` contra cambio de `code` o `name`.
- `inactivate` protege grupos `IsDefault` y ademas bloquea si hay hijos activos o work centers activos colgando del grupo.

#### 5.8.9 Work center types

Route family:

- `GET /api/v1/companies/{companyId}/work-center-types`
- `POST /api/v1/companies/{companyId}/work-center-types`
- `PUT /api/v1/work-center-types/{id}`
- `PATCH /api/v1/work-center-types/{id}/activate`
- `PATCH /api/v1/work-center-types/{id}/inactivate`

Uso principal:

- mantener el catalogo de tipos de work center y sus requerimientos operativos

Observaciones funcionales:

- `list` soporta `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code` y `name`.
- el orden observable del listado es `name`, luego `code`.
- el response incluye `RequiresAddress`, `RequiresGeo` y `AllowsBiometric`.
- `create` y `update` validan unicidad de `code` por tenant.
- `update/activate/inactivate` exigen `ConcurrencyToken`.
- `inactivate` falla si work centers activos siguen usando ese tipo.

#### 5.8.10 Work centers

Route family:

- `GET /api/v1/companies/{companyId}/work-centers`
- `GET /api/v1/work-centers/{id}`
- `POST /api/v1/companies/{companyId}/work-centers`
- `PUT /api/v1/work-centers/{id}`
- `PATCH /api/v1/work-centers/{id}/reassign-group`
- `PATCH /api/v1/work-centers/{id}/activate`
- `PATCH /api/v1/work-centers/{id}/inactivate`

Uso principal:

- registrar sedes, oficinas o puntos operativos concretos del tenant

Observaciones funcionales:

- `search` soporta `groupId`, `typeId`, `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code`, `name`, nombre del grupo y nombre del tipo.
- el orden observable del listado es `name`, luego `code`.
- `get by id` devuelve tipo y grupo resueltos junto con `LocationGroupLevelOrder`.
- `create` y `update` exigen `Code`, `Name`, `WorkCenterTypeId` y `LocationGroupId`.
- `create` y `update` validan unicidad de `code` por tenant.
- el tipo asignado debe existir y estar activo.
- el grupo asignado debe existir, estar activo y pertenecer a un nivel activo que permita work centers.
- si el tipo exige direccion, `Address` pasa a ser obligatorio.
- si el tipo exige geo, `GeoLat` y `GeoLong` pasan a ser obligatorios.
- `GeoLat` debe estar en `[-90, 90]` y `GeoLong` en `[-180, 180]`.
- `reassign-group` solo cambia el grupo y revalida que el nuevo grupo pueda alojar work centers.
- `update/reassign-group/activate/inactivate` exigen `ConcurrencyToken`.

#### 5.8.11 Seed y relacion con onboarding

- el flujo de provisioning de compania inicializa este modulo automaticamente al crear la compania.
- hoy la semilla visible soporta un solo template de pais: `SV`.
- para `SV`, el sistema crea jerarquia multi-nivel con niveles `Pais`, `Departamento` y `Municipio`.
- en esa semilla, solo `Municipio` permite work centers.
- tambien se siembran grupos iniciales desde la plantilla del pais; por eso `Locations` no nace vacio tras el onboarding.
- `OrgUnits` y modulos posteriores reutilizan esta base geografico-operativa para ubicar estructuras y centros de trabajo.

`Locations` es el modulo que convierte el tenant de una compania nueva en una compania ubicable: primero se define la jerarquia, luego los nodos de ubicacion y finalmente los work centers operativos.

### 5.9 Job and competency design

#### 5.9.1 Alcance

Este bloque cubre cinco controladores que juntos definen el diseno formal de puestos, competencias y plazas operativas del tenant:

- `JobCatalogsController`
- `JobProfilesController`
- `CompetencyFrameworkController`
- `PositionDescriptionCatalogsController`
- `PositionSlotsController`

Familias de rutas:

- `/api/v1/companies/{companyId}/job-catalogs/{category}`
- `/api/v1/job-catalogs/{id}/activate`
- `/api/v1/job-catalogs/{id}/inactivate`
- `/api/v1/companies/{companyId}/job-profiles`
- `/api/v1/job-profiles/{id}`
- `/api/v1/job-profiles/{id}/vacancy-template`
- `/api/v1/job-profiles/{id}/print`
- `/api/v1/job-profiles/{id}/export`
- `/api/v1/job-profiles/{id}/publish`
- `/api/v1/job-profiles/{id}/archive`
- `/api/v1/companies/{companyId}/occupational-pyramid-levels`
- `/api/v1/occupational-pyramid-levels/{id}`
- `/api/v1/companies/{companyId}/competency-conducts`
- `/api/v1/competency-conducts/{id}`
- `/api/v1/competency-conducts/{id}/behaviors`
- `/api/v1/job-profiles/{id}/competency-matrix`
- `/api/v1/job-profiles/{id}/competency-matrix/export`
- `/api/v1/companies/{companyId}/position-function-types`
- `/api/v1/companies/{companyId}/position-contract-types`
- `/api/v1/companies/{companyId}/strategic-objectives`
- `/api/v1/companies/{companyId}/frequencies`
- `/api/v1/companies/{companyId}/requirement-types`
- `/api/v1/companies/{companyId}/requirements`
- `/api/v1/companies/{companyId}/general-functions`
- `/api/v1/companies/{companyId}/salary-classes`
- `/api/v1/companies/{companyId}/work-equipments`
- `/api/v1/companies/{companyId}/responsibilities-catalog`
- `/api/v1/companies/{companyId}/benefits-catalog`
- `/api/v1/companies/{companyId}/work-condition-types`
- `/api/v1/companies/{companyId}/work-conditions`
- `/api/v1/companies/{companyId}/position-category-classifications`
- `/api/v1/position-category-classifications/{id}`
- `/api/v1/companies/{companyId}/position-categories`
- `/api/v1/position-categories/{id}`
- `/api/v1/companies/{companyId}/position-slots`
- `/api/v1/position-slots/{id}`
- `/api/v1/companies/{companyId}/position-slots/graph`
- `/api/v1/companies/{companyId}/position-slots/diagram-export`
- `/api/v1/companies/{companyId}/position-slots/export`
- `/api/v1/position-slots/{id}/status`
- `/api/v1/position-slots/{id}/dependencies`
- `/api/v1/position-slots/{id}/occupancy`

#### 5.9.2 Proposito funcional en CLARIHR

Este bloque convierte la estructura organizacional en un modelo formal de puestos y plazas:

- `Job catalogs` mantiene diccionarios reutilizables para conocimientos, competencias, capacitaciones, niveles conductuales y otros ejes de diseno.
- `Job profiles` define el puesto tipo: objetivo, dependencias, requisitos, funciones, relaciones, compensaciones, beneficios y condiciones.
- `Competency framework` modela la piramide ocupacional, los conductos esperados y la matriz de expectativas por perfil.
- `Position description catalogs` provee el vocabulario formal del descriptor de puestos y sus clasificaciones.
- `Position slots` aterriza ese diseno en plazas concretas dentro de la empresa: una posicion real, ubicada en un `OrgUnit`, opcionalmente en un `WorkCenter`, con capacidad, ocupacion y dependencias.

En terminos funcionales, este bloque es la base del diseno organizacional y del gobierno del talento: sin estos endpoints no hay perfiles de puesto consistentes, matrices de competencia por perfil ni plazas concretas para dotacion.

#### 5.9.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- Todo el bloque es tenant-scoped.
- Las rutas de coleccion usan `companyId` en la ruta y exigen que coincida con el tenant activo del token.
- Las rutas por recurso usan solo `{id}` y resuelven el tenant desde el token actual.
- Si el recurso existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de un `404` plano.
- Los listados paginados usan `page/pageSize`, con maximo `100` y default `20`.
- `PositionSlots /graph` acepta `depth` opcional, pero si se envia debe estar entre `1` y `15`.
- Los codigos del bloque usan regex alfanumerica con `_` o `-`, longitud maxima `50`.
- `q/search` admite hasta `150` caracteres.
- `update`, `activate`, `inactivate`, `publish`, `archive`, `status`, `dependencies`, `occupancy` y `competency-matrix` usan concurrencia optimista con `ConcurrencyToken`.
- Todas las escrituras generan auditoria.
- `print` y `export` de `JobProfiles`, `export` de matriz de competencias y `export/diagram-export` de `PositionSlots` tambien generan auditoria.
- No existen endpoints de borrado fisico en este bloque.
- Las escrituras complejas son de reemplazo, no incrementales:
- `PUT /job-profiles/{id}` reemplaza todas las colecciones anidadas del perfil; si una coleccion llega `null`, el controller la convierte en `[]` y el handler limpia la seccion existente.
- `PUT /competency-conducts/{id}/behaviors` reemplaza el conjunto completo de behaviors del conducto.
- `PUT /job-profiles/{id}/competency-matrix` reemplaza la matriz completa del perfil; una lista vacia limpia la matriz.
- `PATCH /position-slots/{id}/dependencies` sobrescribe tanto la dependencia directa como la funcional; `null` limpia la relacion.
- `JobProfileStatus` hoy expone `Draft`, `Published` y `Archived`.
- `PositionSlotStatus` hoy expone `Vacant`, `Occupied` y `Suspended`.
- En `PositionSlots`, el tipo de contrato no lo envia el cliente: se deriva desde `JobProfile -> PositionCategory -> PositionCategoryClassification -> PositionContractType`.

#### 5.9.4 Autorizacion observable

- Lectura de `JobProfiles` y `JobCatalogs` acepta alguno de estos permisos: `JobProfiles.Read`, `JobProfiles.Admin`, `JobCatalogs.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura de perfiles acepta alguno de estos permisos: `JobProfiles.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura de job catalogs acepta alguno de estos permisos: `JobCatalogs.Admin`, `iam.administration.manage` o `platform_admin`.
- Lectura de `CompetencyFramework` acepta alguno de estos permisos: `CompetencyFramework.Read`, `CompetencyFramework.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura de `CompetencyFramework` acepta alguno de estos permisos: `CompetencyFramework.Admin`, `iam.administration.manage` o `platform_admin`.
- Lectura de `PositionDescriptionCatalogs` acepta alguno de estos permisos: `PositionDescriptionCatalogs.Read`, `PositionDescriptionCatalogs.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura de `PositionDescriptionCatalogs` acepta alguno de estos permisos: `PositionDescriptionCatalogs.Admin`, `iam.administration.manage` o `platform_admin`.
- Lectura de `PositionSlots` acepta alguno de estos permisos: `PositionSlots.Read`, `PositionSlots.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura de `PositionSlots` acepta alguno de estos permisos: `PositionSlots.Admin`, `iam.administration.manage` o `platform_admin`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado en el tenant correcto pero no cumple permisos, la API responde el `FORBIDDEN` especifico del submodulo:
- `JOB_PROFILES_FORBIDDEN`
- `COMPETENCY_FRAMEWORK_FORBIDDEN`
- `POSITION_DESCRIPTION_CATALOG_FORBIDDEN`
- `POSITION_SLOTS_FORBIDDEN`

#### 5.9.5 Errores observables relevantes en Job and competency design

Errores transversales:

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `CONCURRENCY_CONFLICT`: `409`, el `ConcurrencyToken` ya no coincide con la version actual.

Errores relevantes en `JobProfiles` y `JobCatalogs`:

- `JOB_PROFILE_NOT_FOUND`: `404`, el perfil solicitado no existe en el scope correcto.
- `JOB_CATALOG_ITEM_NOT_FOUND`: `404`, el catalog item solicitado no existe en el scope correcto.
- `JOB_PROFILE_ORG_UNIT_NOT_FOUND`: `404`, el `OrgUnit` referenciado no se pudo resolver.
- `JOB_PROFILE_REPORTS_TO_NOT_FOUND`: `404`, el perfil superior referenciado no se pudo resolver.
- `JOB_PROFILE_CODE_CONFLICT`: `409`, otro perfil ya usa ese `code`.
- `JOB_CATALOG_ITEM_CODE_CONFLICT`: `409`, otro item de la misma categoria ya usa ese `code`.
- `JOB_PROFILE_DEPENDENCY_CYCLE`: `409`, `reportsTo` o `dependentPositions` crearian un ciclo.
- `JOB_PROFILE_STATE_CONFLICT`: `409`, la operacion no aplica al estado actual, por ejemplo editar un perfil archivado.
- `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`: `422`, faltan requisitos minimos para publicar.
- `JOB_CATALOG_INLINE_CREATE_FORBIDDEN`: `403`, el payload quiso crear catalogos inline sin permisos de catalog admin.
- `JOB_PROFILE_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `json|csv`.

Errores relevantes en `CompetencyFramework`:

- `OCCUPATIONAL_PYRAMID_LEVEL_NOT_FOUND`: `404`, el nivel solicitado no existe en el scope correcto.
- `OCCUPATIONAL_PYRAMID_LEVEL_CODE_CONFLICT`: `409`, otro nivel ya usa ese `code`.
- `OCCUPATIONAL_PYRAMID_LEVEL_ORDER_CONFLICT`: `409`, otro nivel ya usa ese `LevelOrder`.
- `OCCUPATIONAL_PYRAMID_LEVEL_IN_USE`: `409`, no puede inactivarse porque matrices activas aun lo usan.
- `COMPETENCY_CONDUCT_NOT_FOUND`: `404`, el conducto solicitado no existe en el scope correcto.
- `COMPETENCY_CONDUCT_DUPLICATE`: `409`, ya existe un conducto con la misma combinacion de competencia, tipo, behavior level y descripcion.
- `RESOURCE_IN_USE`: `409`, no puede inactivarse un conducto usado por expectativas activas.
- `COMPETENCY_NOT_FOUND`: `404`, la competencia referenciada no existe o esta inactiva.
- `COMPETENCY_TYPE_NOT_FOUND`: `404`, el tipo de competencia referenciado no existe o esta inactivo.
- `BEHAVIOR_LEVEL_NOT_FOUND`: `404`, el behavior level referenciado no existe o esta inactivo.
- `BEHAVIOR_NOT_FOUND`: `404`, el behavior referenciado no existe o esta inactivo.
- `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT`: `409`, la matriz pedida tiene duplicados, cruces invalidos, un conducto no corresponde al eje declarado o el perfil esta archivado.
- `COMPETENCY_FRAMEWORK_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `json|csv|xlsx`.

Errores relevantes en `PositionDescriptionCatalogs`:

- `POSITION_DESCRIPTION_CATALOG_ITEM_NOT_FOUND`: `404`, no existe el catalog item solicitado.
- `POSITION_CATEGORY_CLASSIFICATION_NOT_FOUND`: `404`, no existe la clasificacion solicitada.
- `POSITION_CATEGORY_NOT_FOUND`: `404`, no existe la categoria solicitada.
- `POSITION_DESCRIPTION_CATALOG_CODE_CONFLICT`: `409`, otro item del mismo tipo ya usa ese `code`.
- `POSITION_CATEGORY_CLASSIFICATION_CODE_CONFLICT`: `409`, otra clasificacion ya usa ese `code`.
- `POSITION_CATEGORY_CODE_CONFLICT`: `409`, otra categoria ya usa ese `code`.
- `POSITION_CATEGORY_CLASSIFICATION_DUPLICATE_AXES`: `409`, ya existe una clasificacion con la misma combinacion de `positionFunctionType`, `positionContractType` y `orgUnitType`.
- `POSITION_DESCRIPTION_CATALOG_IN_USE`: `409`, no puede inactivarse el item porque aun esta referenciado.
- `POSITION_CATEGORY_CLASSIFICATION_IN_USE`: `409`, no puede inactivarse la clasificacion porque aun tiene categorias activas asociadas.
- `POSITION_CATEGORY_IN_USE`: `409`, no puede inactivarse la categoria porque aun la usan job profiles.
- `POSITION_DESCRIPTION_CATALOG_RELATED_ITEM_NOT_FOUND`: `404`, un catalogo relacionado requerido no existe o esta inactivo.
- `ORG_UNIT_TYPE_NOT_FOUND`: `404`, el `OrgUnitType` usado por la clasificacion no existe o esta inactivo.
- `SALARY_CLASS_NOT_FOUND`: `404`, la salary class referenciada no existe.
- `REQUIREMENT_TYPE_NOT_FOUND`: `404`, el requirement type referenciado no existe.
- `FREQUENCY_NOT_FOUND`: `404`, la frecuencia referenciada no existe.
- `WORK_CONDITION_TYPE_NOT_FOUND`: `404`, el tipo de condicion de trabajo referenciado no existe.

Errores relevantes en `PositionSlots`:

- `POSITION_SLOT_NOT_FOUND`: `404`, la plaza solicitada no existe en el scope correcto.
- `POSITION_SLOT_JOB_PROFILE_NOT_FOUND`: `404`, el job profile referenciado no se pudo resolver.
- `POSITION_SLOT_ORG_UNIT_NOT_FOUND`: `404`, el `OrgUnit` referenciado no se pudo resolver.
- `POSITION_SLOT_WORK_CENTER_NOT_FOUND`: `404`, el `WorkCenter` referenciado no se pudo resolver.
- `POSITION_SLOT_DEPENDENCY_NOT_FOUND`: `404`, la plaza usada como dependencia no se pudo resolver.
- `POSITION_SLOT_CONTRACT_TYPE_NOT_RESOLVED`: `422`, el job profile no resuelve un tipo de contrato activo.
- `POSITION_SLOT_COST_CENTER_INVALID`: `422`, el `costCenterCode` no existe o esta inactivo en la compania.
- `POSITION_SLOT_CODE_CONFLICT`: `409`, otra plaza ya usa ese `code`.
- `POSITION_SLOT_DEPENDENCY_CYCLE`: `409`, la dependencia directa crearia un ciclo.
- `POSITION_SLOT_DEPENDENCY_SELF_REFERENCE`: `409`, la plaza no puede depender de si misma.
- `POSITION_SLOT_STATUS_CONFLICT`: `409`, el estado solicitado no es consistente con la ocupacion actual.
- `POSITION_SLOT_SUSPENDED_OCCUPANCY_CONFLICT`: `409`, una plaza suspendida no puede cambiar ocupacion.
- `POSITION_SLOT_CAPACITY_RULE_VIOLATION`: `422`, `occupiedEmployees` esta fuera del rango permitido.
- `POSITION_SLOT_EFFECTIVE_DATES_INVALID`: `422`, rango de fechas invalido.
- `POSITION_SLOT_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `csv|xlsx`.
- `POSITION_SLOT_DIAGRAM_FORMAT_INVALID`: `400`, `format` distinto de `graphml|json|dot`.

Errores `400` de validacion frecuentes:

- `page/pageSize` fuera de rango.
- `depth` fuera de `1..15`.
- ids vacios.
- `code` invalido.
- `ConcurrencyToken` ausente.
- `EffectiveToUtc < EffectiveFromUtc`.

#### 5.9.6 Job catalogs

Route family:

- `GET /api/v1/companies/{companyId}/job-catalogs/{category}`
- `POST /api/v1/companies/{companyId}/job-catalogs/{category}`
- `PATCH /api/v1/job-catalogs/{id}/activate`
- `PATCH /api/v1/job-catalogs/{id}/inactivate`

Uso principal:

- mantener catalogos reutilizables del diseno de puestos y del framework de competencias

Observaciones funcionales:

- `category` es enum-driven y hoy expone: `EducationLevel`, `KnowledgeArea`, `Competency`, `Training`, `SalaryClass`, `BenefitType`, `WorkingCondition`, `RelationType`, `DecisionLevel`, `CompetencyType`, `BehaviorLevel` y `Behavior`.
- `search` soporta `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code` y `name`.
- el orden observable del listado es `name`, luego `code`.
- el response incluye `IsSystem`, `IsActive` y `ConcurrencyToken`.
- no existe `get by id`.
- no existe endpoint `update`.
- `create` exige unicidad de `code` por tenant + categoria.
- `activate` e `inactivate` usan solo `id + ConcurrencyToken`; no revalidan categoria en la ruta.
- la API no aplica chequeos de uso antes de inactivar un item de `job-catalogs`.
- el efecto observable de esa decision es que los historicos ya enlazados siguen existiendo, pero futuras resoluciones "activas" para `JobProfiles` o `CompetencyFramework` ya no aceptaran el item inactivo.

#### 5.9.7 Job profiles

Route family:

- `GET /api/v1/companies/{companyId}/job-profiles`
- `GET /api/v1/job-profiles/{id}`
- `GET /api/v1/job-profiles/{id}/vacancy-template`
- `GET /api/v1/job-profiles/{id}/print`
- `GET /api/v1/job-profiles/{id}/export`
- `POST /api/v1/companies/{companyId}/job-profiles`
- `PUT /api/v1/job-profiles/{id}`
- `PATCH /api/v1/job-profiles/{id}/publish`
- `PATCH /api/v1/job-profiles/{id}/archive`

Uso principal:

- crear y mantener el descriptor maestro de un puesto dentro del tenant
- preparar una version imprimible o exportable del perfil
- publicar o archivar el perfil formal

Observaciones funcionales:

- `search` soporta filtros `status`, `orgUnitId`, `salaryClass`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `code` y `title`.
- el orden observable del listado es `title`, luego `code`.
- `search` devuelve `PagedResponse<JobProfileListItemResponse>`.
- `get by id` devuelve el agregado completo: datos base, dependencias, requisitos, funciones, relaciones, competencias, trainings, compensaciones, beneficios, condiciones y puestos dependientes.
- `vacancy-template` devuelve una vista resumida para reclutamiento: objetivo, responsabilidades, resumen de condiciones/beneficios y las colecciones mas relevantes del perfil.
- `print` devuelve `JobProfilePrintResponse` con `Profile + GeneratedAtUtc` y registra auditoria `ReportPrinted`.
- `export` soporta solo `json|csv`.
- el payload de `create/update` mezcla campos escalares y colecciones anidadas.
- `update` es de reemplazo total sobre las colecciones; no es un merge parcial.
- `create/update` resuelven referencias a `OrgUnit`, `ReportsToJobProfile`, `PositionCategory`, `StrategicObjective`, `AssignedWorkEquipment` y `Responsibility`.
- `PositionCategory` debe existir y estar activa para poder asociarse al perfil.
- las referencias a `StrategicObjective`, `AssignedWorkEquipment`, `Responsibility`, `RequirementType`, `Frequency` y `WorkConditionType` deben existir y estar activas.
- el sistema detecta ciclos tanto en `reportsTo` como en `dependentPositions`.
- `Published` no es un estado inmutable: un job profile publicado todavia puede editarse y volver a publicarse mientras no este archivado.
- `Archived` si es terminal para edicion: cualquier `update` o `publish` sobre un perfil archivado falla con `JOB_PROFILE_STATE_CONFLICT`.
- `publish` exige al menos estas precondiciones: `Objective`, minimo un `Requirement`, minimo una `Function` y `Responsibilities`.
- `publish` no exige competencias, trainings, beneficios, compensaciones ni categoria de puesto.
- `publish` usa el `ConcurrencyToken` del perfil y, si el estado es valido, incrementa `Version`.
- `archive` es practicamente idempotente: si el perfil ya estaba archivado y el `ConcurrencyToken` coincide, devuelve la representacion actual sin volver a mutar.
- `AllowInlineCatalogCreate=true` permite que el mismo payload cree items faltantes en ciertas categorias de `job-catalogs`.
- ese inline create solo funciona si el usuario tambien tiene permisos de catalog admin; de lo contrario responde `JOB_CATALOG_INLINE_CREATE_FORBIDDEN`.
- para inline create se requieren `code + name`; si ya existe un item activo con el mismo `name` normalizado, la API lo reutiliza en vez de crear duplicado.

#### 5.9.8 Competency framework

Route family:

- `GET /api/v1/companies/{companyId}/occupational-pyramid-levels`
- `GET /api/v1/occupational-pyramid-levels/{id}`
- `POST /api/v1/companies/{companyId}/occupational-pyramid-levels`
- `PUT /api/v1/occupational-pyramid-levels/{id}`
- `PATCH /api/v1/occupational-pyramid-levels/{id}/activate`
- `PATCH /api/v1/occupational-pyramid-levels/{id}/inactivate`
- `GET /api/v1/companies/{companyId}/competency-conducts`
- `GET /api/v1/competency-conducts/{id}`
- `POST /api/v1/companies/{companyId}/competency-conducts`
- `PUT /api/v1/competency-conducts/{id}`
- `PATCH /api/v1/competency-conducts/{id}/activate`
- `PATCH /api/v1/competency-conducts/{id}/inactivate`
- `PUT /api/v1/competency-conducts/{id}/behaviors`
- `GET /api/v1/job-profiles/{id}/competency-matrix`
- `PUT /api/v1/job-profiles/{id}/competency-matrix`
- `GET /api/v1/job-profiles/{id}/competency-matrix/export`

Uso principal:

- definir niveles ocupacionales y conductos de comportamiento
- asociar behaviors a cada conducto
- mantener la matriz de expectativas por job profile

Observaciones funcionales:

- `occupational-pyramid-levels` soporta `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- el orden observable de niveles es `LevelOrder`, luego `Code`.
- `create/update` de niveles exigen unicidad de `code` y unicidad de `LevelOrder`.
- `inactivate` de nivel falla si matrices activas aun lo estan usando.
- `competency-conducts` soporta filtros `competencyId`, `competencyTypeId`, `behaviorLevelId`, `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por descripcion del conducto, por `code/name` de la competencia y por `code` de `competencyType` y `behaviorLevel`.
- el orden observable de conductos es `SortOrder`, luego `Description`.
- `create/update` de conductos resuelven `Competency`, `CompetencyType` y `BehaviorLevel` desde `job-catalogs`, siempre como referencias activas.
- un conducto es unico por `competency + competencyType + behaviorLevel + description normalizada`.
- `PUT /competency-conducts/{id}/behaviors` reemplaza todo el set de behaviors.
- en ese endpoint no se permiten `BehaviorId` duplicados dentro de la misma solicitud.
- cada `BehaviorId` debe existir como item activo de `JobCatalogCategory.Behavior`.
- `inactivate` de conducto falla si el conducto sigue vinculado a expectativas activas de perfiles.
- `GET /job-profiles/{id}/competency-matrix` devuelve `JobProfileId`, `JobProfileCode`, `JobProfileTitle`, `JobProfileStatus`, `JobProfileVersion`, `ConcurrencyToken` e `Items`.
- `PUT /job-profiles/{id}/competency-matrix` es de reemplazo total; una lista vacia limpia la matriz del perfil.
- cada item de matriz debe ser unico por combinacion `OccupationalPyramidLevelId + CompetencyId + CompetencyTypeId + BehaviorLevelId`.
- dentro de un item, `ConductIds` tambien deben ser unicos.
- cada conducto referenciado debe pertenecer exactamente al mismo eje `competency + competencyType + behaviorLevel` declarado en el item.
- perfiles archivados no pueden mutar su matriz y responden `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT`.
- al actualizar la matriz, el sistema incrementa la `Version` del `JobProfile` y regenera su `ConcurrencyToken`, aun si no cambio ningun otro campo escalar del perfil.
- `competency-matrix/export` soporta `json|csv|xlsx` y registra auditoria de export.

#### 5.9.9 Position description catalogs

Route family:

- simples: `position-function-types`, `position-contract-types`, `strategic-objectives`, `frequencies`, `requirement-types`, `requirements`, `general-functions`, `salary-classes`, `work-equipments`, `responsibilities-catalog`, `benefits-catalog`, `work-condition-types` y `work-conditions`
- clasificaciones: `/api/v1/companies/{companyId}/position-category-classifications` y `/api/v1/position-category-classifications/{id}`
- categorias: `/api/v1/companies/{companyId}/position-categories` y `/api/v1/position-categories/{id}`

Uso principal:

- mantener el vocabulario formal del descriptor de puestos
- clasificar puestos por funcion, contrato y tipo organizacional
- definir categorias de puesto que luego usan `JobProfiles` y `PositionSlots`

Observaciones funcionales de los catalogos simples:

- cada familia simple expone `search`, `get by id`, `create`, `update`, `activate` e `inactivate`.
- `search` soporta `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code` y `name`.
- el orden observable es `SortOrder`, luego `Name`, luego `Code`.
- el payload simple usa `Code`, `Name`, `Description` y `SortOrder`.
- `update/activate/inactivate` usan `ConcurrencyToken`.
- no existen exportes para estos catalogos.
- el bloqueo por uso para inactivar depende del tipo:
- `position-function-types` y `position-contract-types` se bloquean si alguna `PositionCategoryClassification` los usa.
- `frequencies` se bloquea si alguna funcion de job profile la usa.
- `requirement-types` se bloquea si algun requirement de job profile la usa.
- `work-condition-types` se bloquea si alguna condicion de job profile la usa.
- el resto de catalogos simples se bloquea si algun `JobProfile` los usa en sus referencias principales.
- detalle, update, activate e inactivate resuelven el item simple solo por `{id}`; el nombre de la familia en la ruta no agrega un segundo filtro por `CatalogType`.
- por eso la respuesta siempre expone `CatalogType`, que es la fuente observable real del tipo del item recuperado.

Observaciones funcionales de `position-category-classifications`:

- `search` soporta filtros `positionFunctionTypeId`, `positionContractTypeId`, `orgUnitTypeId`, `isActive`, `q`, `page` y `pageSize`.
- el orden observable es `SortOrder`, luego `Name`, luego `Code`.
- `create/update` exigen referencias activas a `PositionFunctionType`, `PositionContractType` y `OrgUnitType`.
- la clasificacion es unica por `code`.
- ademas la combinacion `PositionFunctionType + PositionContractType + OrgUnitType` tambien debe ser unica.
- `inactivate` falla si alguna `PositionCategory` activa sigue usando la clasificacion.

Observaciones funcionales de `position-categories`:

- `search` soporta filtros `classificationId`, `isActive`, `q`, `page` y `pageSize`.
- el orden observable es `SortOrder`, luego `Name`, luego `Code`.
- `create/update` exigen `ClassificationId`.
- la categoria es unica por `code`.
- `inactivate` falla si algun `JobProfile` sigue usando la categoria.

#### 5.9.10 Position slots

Route family:

- `GET /api/v1/companies/{companyId}/position-slots`
- `GET /api/v1/position-slots/{id}`
- `GET /api/v1/companies/{companyId}/position-slots/graph`
- `GET /api/v1/companies/{companyId}/position-slots/diagram-export`
- `GET /api/v1/companies/{companyId}/position-slots/export`
- `POST /api/v1/companies/{companyId}/position-slots`
- `PUT /api/v1/position-slots/{id}`
- `PATCH /api/v1/position-slots/{id}/status`
- `PATCH /api/v1/position-slots/{id}/dependencies`
- `PATCH /api/v1/position-slots/{id}/occupancy`

Uso principal:

- materializar plazas reales a partir de job profiles
- controlar estado, jerarquia operativa y ocupacion de cada plaza
- exportar la vision tabular o de grafo de la estructura de puestos

Observaciones funcionales:

- `search` soporta filtros `status`, `jobProfileId`, `orgUnitId`, `workCenterId`, `contractTypeId`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `slot code`, `slot title`, `job profile`, `org unit` y `work center`.
- el orden observable del listado es `Code`, luego `Title`.
- el detail devuelve referencias resueltas a `JobProfile`, `OrgUnit`, `WorkCenter`, `DirectDependency`, `FunctionalDependency`, `PositionCategory`, `PositionCategoryClassification` y `ContractType`.
- `create` exige `Code`, `JobProfileId`, `OrgUnitId`, `Status`, `MaxEmployees`, `OccupiedEmployees` y `EffectiveFromUtc`.
- `WorkCenterId`, `CostCenterCode`, dependencias directas/funcionales, `EffectiveToUtc` y `Notes` son opcionales.
- `JobProfileId` se resuelve por tenant, no por estado; el API no exige que el perfil este publicado para crear una plaza.
- la validacion critica real no es el estado del perfil sino que ese perfil resuelva a un tipo de contrato activo.
- `ContractType` no viene del cliente y se infiere desde la clasificacion del job profile.
- la bandera interna `IsFixedTerm` se infiere automaticamente por heuristica de `contractTypeCode/contractTypeName` con tokens como `TEMP`, `FIXED`, `PLAZO` o `FIJO`.
- si se envia `CostCenterCode`, debe existir activo dentro del tenant.
- `Vacant` exige `OccupiedEmployees = 0`.
- `Occupied` exige `OccupiedEmployees > 0`.
- `Suspended` no cambia la ocupacion existente, pero pone `IsActive = false`.
- `PUT /position-slots/{id}` actualiza solo el nucleo de la plaza; no cambia ni `Status` ni `OccupiedEmployees`.
- `PATCH /position-slots/{id}/status` cambia solo el estado y revalida consistencia contra la ocupacion actual.
- `PATCH /position-slots/{id}/occupancy` cambia solo `OccupiedEmployees` y recalcula automaticamente el estado a `Vacant` u `Occupied`.
- una plaza `Suspended` no puede usar el endpoint de `occupancy`.
- `PATCH /position-slots/{id}/dependencies` sobrescribe ambas dependencias.
- la dependencia directa si se valida contra ciclos.
- la dependencia funcional solo se protege contra autorreferencia; el API no aplica una deteccion general de ciclos funcionales.
- `graph` devuelve nodos y edges de la estructura de plazas.
- si se envia `rootId`, el grafo se acota a ese subarbol/subgrafo; si no, parte de las raices de dependencia directa.
- `includeFunctional=true` agrega traversal y edges funcionales.
- `diagram-export` reutiliza exactamente esos filtros (`rootId`, `depth`, `includeFunctional`) y soporta `graphml|json|dot`.
- `export` reutiliza los filtros del listado sin paginacion y soporta `csv|xlsx`.
- este controller no tiene `activate/inactivate`; el estado operativo de la plaza se controla con `PATCH /status`.

#### 5.9.11 Relacion con otros modulos

- `OrgUnits` aporta la estructura organizacional sobre la que se ubican `JobProfiles` y `PositionSlots`.
- `Locations` aporta `WorkCenters`, que `PositionSlots` puede usar como ubicacion operativa concreta.
- `CostCenters` valida `CostCenterCode` para plazas.
- `Org structure catalogs` aporta `OrgUnitTypes`, que son obligatorios para construir `PositionCategoryClassifications`.
- `PositionDescriptionCatalogs` alimenta a `JobProfiles` con tipos de funcion, contratos, frecuencias, salary classes, work equipments, responsabilidades, benefits y work conditions.
- `Job catalogs` alimenta tanto a `JobProfiles` como a `CompetencyFramework`.
- `CompetencyFramework` se apoya en `JobProfiles`: la matriz siempre cuelga de un `job-profile`.
- `PositionSlots` se apoya en `JobProfiles`: la plaza es la instancia operativa de un perfil, no un recurso independiente del diseno.

`Job and competency design` es asi el modulo que une diseno, clasificacion y operacion: primero se definen catalogos y clasificaciones, luego el perfil de puesto, despues el framework de competencias y finalmente la plaza concreta dentro de la empresa.

### 5.10 Personnel files

#### 5.10.1 Alcance

Este bloque cubre ocho controladores que juntos administran el expediente de personal del tenant:

- `PersonnelFilesController`
- `PersonnelFileProfileController`
- `PersonnelFileEmploymentController`
- `PersonnelFileCompensationController`
- `PersonnelFileTalentController`
- `PersonnelFileDocumentsController`
- `PersonnelFileAdministrationController`
- `PersonnelFileReportingController`

Familias de rutas:

- `/api/v1/companies/{companyId}/personnel-files`
- `/api/v1/personnel-files/{id}`
- `/api/v1/personnel-files/{id}/personal-info`
- `/api/v1/personnel-files/{id}/identifications`
- `/api/v1/personnel-files/{id}/addresses`
- `/api/v1/personnel-files/{id}/emergency-contacts`
- `/api/v1/personnel-files/{id}/family-members`
- `/api/v1/personnel-files/{id}/hobbies`
- `/api/v1/personnel-files/{id}/employee-relations`
- `/api/v1/personnel-files/{id}/associations`
- `/api/v1/personnel-files/{id}/educations`
- `/api/v1/personnel-files/{id}/languages`
- `/api/v1/personnel-files/{id}/trainings`
- `/api/v1/personnel-files/{id}/previous-employments`
- `/api/v1/personnel-files/{id}/references`
- `/api/v1/personnel-files/{id}/hire`
- `/api/v1/personnel-files/{id}/employee-profile`
- `/api/v1/personnel-files/{id}/employment-assignments`
- `/api/v1/personnel-files/{id}/contract-history`
- `/api/v1/personnel-files/{id}/position-hierarchy`
- `/api/v1/personnel-files/{id}/authorization-substitutions`
- `/api/v1/personnel-files/{id}/personnel-actions`
- `/api/v1/personnel-files/{id}/personnel-actions/export`
- `/api/v1/personnel-files/{id}/assets-accesses`
- `/api/v1/personnel-files/{id}/salary-items`
- `/api/v1/personnel-files/{id}/additional-benefits`
- `/api/v1/personnel-files/{id}/payment-methods`
- `/api/v1/personnel-files/{id}/payroll-transactions`
- `/api/v1/personnel-files/{id}/payroll-transactions/export`
- `/api/v1/personnel-files/{id}/insurances`
- `/api/v1/personnel-files/{id}/medical-claims`
- `/api/v1/personnel-files/{id}/bank-accounts`
- `/api/v1/personnel-files/{id}/evaluations`
- `/api/v1/personnel-files/{id}/position-competency-results`
- `/api/v1/personnel-files/{id}/position-competencies`
- `/api/v1/personnel-files/{id}/selection-contests`
- `/api/v1/personnel-files/{id}/curricular-competencies`
- `/api/v1/personnel-files/{id}/documents`
- `/api/v1/personnel-files/{id}/observations`
- `/api/v1/personnel-files/{id}/print`
- `/api/v1/companies/{companyId}/personnel-files/dynamic-query`
- `/api/v1/companies/{companyId}/personnel-files/export`
- `/api/v1/companies/{companyId}/personnel-files/analytics/summary`
- `/api/v1/personnel-file-documents/{documentId}/inactivate`
- `/api/v1/personnel-file-documents/{documentId}/download`
- `/api/v1/companies/{companyId}/personnel-catalogs/{category}`
- `/api/v1/companies/{companyId}/personnel-custom-field-definitions`
- `/api/v1/personnel-custom-field-definitions/{id}`

#### 5.10.2 Proposito funcional en CLARIHR

`PersonnelFiles` es el expediente maestro de personas del tenant. Su rol funcional es cubrir todo el ciclo base alrededor de una persona dentro de RRHH:

- alta y consulta del expediente
- llenado del perfil personal, familiar y curricular
- transicion formal de candidato a empleado
- mantenimiento de informacion laboral, compensacion y talento
- almacenamiento de documentos y observaciones
- catalogos propios del modulo
- reportes, exportes y consulta dinamica

En terminos de negocio, este bloque une dos mundos:

- el expediente personal base, que sirve para datos biograficos, antecedentes y documentos
- la capa operativa de RRHH, donde el mismo expediente se convierte en empleado, recibe asignaciones, salario, historial de acciones y evidencias

#### 5.10.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- Todo el bloque es tenant-scoped.
- Las rutas de coleccion usan `companyId` en la ruta y exigen que coincida con el tenant activo del token.
- Las rutas por recurso usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un expediente o documento existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de un `404` plano.
- La separacion de permisos es binaria:
- lectura usa `EnsureCanReadAsync`
- escritura usa `EnsureCanManageAsync`
- Los listados paginados del modulo usan `page/pageSize`, con default `20` y maximo `100`.
- No existen endpoints de borrado fisico.
- Todas las escrituras generan auditoria.
- `print`, `export`, `personnel-actions/export` y `payroll-transactions/export` tambien generan auditoria.
- La mayoria de escrituras por subseccion son de reemplazo total, no de merge parcial.
- En `Profile`, cada `PUT` reemplaza la coleccion completa de la subseccion y devuelve el `PersonnelFileResponse` completo con el nuevo `ConcurrencyToken` del expediente.
- En `Employment`, `Compensation` y `Talent`, las escrituras tambien usan el `ConcurrencyToken` del expediente padre, pero la respuesta suele ser una coleccion o un `employee profile`, no el expediente completo.
- El efecto observable de ese diseno es que el cliente normalmente necesita releer `/api/v1/personnel-files/{id}` antes de encadenar otra mutacion file-scoped sobre el mismo expediente.
- Los endpoints de `Employment`, `Compensation` y `Talent` son de uso exclusivo para expedientes con `RecordType = Employee`.
- `hire` es la unica transicion permitida de `Candidate` a `Employee`; no puede hacerse desde `personal-info`.
- `GET /api/v1/personnel-files/{id}` y `GET /print` solo cubren el agregado base del expediente:
- datos personales y curriculares
- cuentas bancarias
- documentos
- observaciones
- no incluyen `employee profile`, asignaciones, acciones de personal, transacciones de planilla, seguros, evaluaciones ni otros subrecursos del bloque laboral
- `bank-accounts` se muta desde `PersonnelFileCompensationController`, pero sigue viviendo sobre el agregado base y por eso aparece en `GetById` y en `print`.
- `search` y `dynamic-query` soportan busqueda libre por nombre completo y por numero de identificacion.
- `export` y `analytics/summary` reutilizan la ruta de export rows, por lo que su `q` solo opera sobre nombre completo; no busca por identificaciones.

#### 5.10.4 Autorizacion observable

- Lectura de `PersonnelFiles` acepta alguno de estos permisos: `PersonnelFiles.Read`, `PersonnelFiles.Admin`, `iam.administration.manage` o `platform_admin`.
- Escritura de `PersonnelFiles` acepta alguno de estos permisos: `PersonnelFiles.Admin`, `iam.administration.manage` o `platform_admin`.
- `platform_admin` hace bypass completo del chequeo tenant-permission basado en membresias y claims.
- Si los claims directos no bastan, la API tambien resuelve permisos a traves de la membresia activa de compania y del rol asignado en ese tenant.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado en el tenant correcto pero no cumple permisos, la API responde `PERSONNEL_FILES_FORBIDDEN`.
- `search` y `dynamic-query` pueden devolver `AllowedActions` cuando el cliente lo pide con `includeAllowedActions=true`.
- `get by id` siempre aplica `AllowedActions` sobre el expediente base.
- `print` reutiliza la misma lectura autorizada del expediente y devuelve el expediente filtrado por secciones, no un bypass de autorizacion.

#### 5.10.5 Errores observables relevantes en Personnel files

Errores transversales:

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido.
- `TENANT_MISMATCH`: `403`, se intenta operar un recurso de otro tenant.
- `PERSONNEL_FILES_FORBIDDEN`: `403`, el usuario no cumple permisos de lectura o administracion del modulo.
- `PERSONNEL_FILE_NOT_FOUND`: `404`, el expediente solicitado no existe en el scope correcto.
- `CONCURRENCY_CONFLICT`: `409`, el `ConcurrencyToken` del expediente o del documento ya no coincide.

Errores funcionales del expediente:

- `PERSONNEL_FILE_IDENTIFICATION_CONFLICT`: `409`, otra persona del tenant ya usa la misma identificacion.
- `PERSONNEL_FILE_STATE_RULE_VIOLATION`: `422`, la operacion no aplica al estado o tipo actual del expediente.
- `PERSONNEL_FILE_HIRE_ENDPOINT_REQUIRED`: `422`, se intento convertir un candidato a empleado desde `personal-info`.
- `PERSONNEL_FILE_EFFECTIVE_DATES_INVALID`: `422`, hay rangos de fechas invalidos en identifications, associations, trainings, previous employments u otras subsecciones con vigencias.
- `PERSONNEL_FILE_FAMILY_MEMBER_RULE_VIOLATION`: `422`, campos condicionales de familiares no cumplen las reglas visibles del dominio.
- `PERSONNEL_CUSTOM_DATA_INVALID`: `400`, el JSON de custom data no es valido o no coincide con los tipos configurados.
- `PERSONNEL_CUSTOM_FIELD_KEY_CONFLICT`: `409`, otra definicion custom del tenant ya usa la misma key.
- `PERSONNEL_CUSTOM_FIELD_DEFINITION_NOT_FOUND`: `404`, la definicion custom solicitada no existe.

Errores de documentos y reporting:

- `PERSONNEL_FILE_DOCUMENT_NOT_FOUND`: `404`, el documento solicitado no existe.
- `PERSONNEL_FILE_DOCUMENT_FILE_REQUIRED`: `400`, el upload no trajo archivo.
- `PERSONNEL_FILE_DOCUMENT_DATES_INVALID`: `422`, las fechas de prestamo y devolucion del documento no son consistentes.
- `PERSONNEL_FILE_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `csv|xlsx` en exportes de expedientes, acciones de personal o transacciones de planilla.

Errores `400` de validacion frecuentes:

- `q` mayor a `150` caracteres en `search`, `dynamic-query`, `export` o `analytics`.
- `page` o `pageSize` fuera de rango.
- `sortBy` o campos dinamicos no soportados.
- `groupBy` con mas de `3` campos o con campos no groupables.
- `Field/Operator` dinamicos no soportados o con payload incompleto.
- `sections` de `print` con valores no soportados.
- nombres, telefonos, codigos, keys o identificaciones con formato invalido.
- `customDataJson` sin los campos requeridos configurados.
- codigos curriculares inactivos o inexistentes, devueltos como errores de validacion por campo y categoria.

#### 5.10.6 Core

Route family:

- `POST /api/v1/companies/{companyId}/personnel-files`
- `GET /api/v1/companies/{companyId}/personnel-files`
- `GET /api/v1/personnel-files/{id}`
- `PATCH /api/v1/personnel-files/{id}/activate`
- `PATCH /api/v1/personnel-files/{id}/inactivate`

Uso principal:

- abrir un expediente nuevo
- listar expedientes del tenant
- consultar el agregado base del expediente
- activar o inactivar logicamente el expediente

Observaciones funcionales:

- `RecordType` hoy expone `Candidate` y `Employee`.
- `create` exige al menos una identificacion inicial.
- `create` valida formato de nombres, emails y telefonos, valida custom fields activos y evita duplicidad tenant-wide de identificaciones.
- `search` soporta filtros `isActive`, `recordType`, `orgUnitId`, `minAge`, `maxAge`, `maritalStatus`, `nationality`, `profession`, `createdFromUtc`, `createdToUtc`, `q`, `sortBy`, `sortDirection`, `page`, `pageSize` e `includeAllowedActions`.
- `search` usa como orden default `FullName ASC`.
- `search` acepta estos `sortBy`: `fullname`, `firstname`, `lastname`, `birthdate`, `age`, `recordtype`, `maritalstatus`, `nationality`, `profession`, `orgunitid`, `isactive`, `createdatutc`, `modifiedatutc`.
- `search` busca por nombre completo normalizado y por numero de identificacion.
- `get by id` devuelve el agregado base del expediente con identificaciones, direcciones, contactos, familiares, hobbies, relaciones, bank accounts, asociaciones, educacion, idiomas, trainings, previous employments, referencias, documentos y observaciones.
- `get by id` no devuelve `employee profile`, asignaciones, contract history, personnel actions, payroll transactions, seguros, evaluaciones ni concursos.
- `activate` e `inactivate` son transiciones soft-state y requieren `ConcurrencyToken`.

#### 5.10.7 Profile

Route family:

- `PUT /api/v1/personnel-files/{id}/personal-info`
- `PUT /api/v1/personnel-files/{id}/identifications`
- `PUT /api/v1/personnel-files/{id}/addresses`
- `PUT /api/v1/personnel-files/{id}/emergency-contacts`
- `PUT /api/v1/personnel-files/{id}/family-members`
- `PUT /api/v1/personnel-files/{id}/hobbies`
- `PUT /api/v1/personnel-files/{id}/employee-relations`
- `PUT /api/v1/personnel-files/{id}/associations`
- `PUT /api/v1/personnel-files/{id}/educations`
- `PUT /api/v1/personnel-files/{id}/languages`
- `PUT /api/v1/personnel-files/{id}/trainings`
- `PUT /api/v1/personnel-files/{id}/previous-employments`
- `PUT /api/v1/personnel-files/{id}/references`

Uso principal:

- completar y mantener el contenido personal, familiar y curricular del expediente

Observaciones funcionales:

- Todos los endpoints de `Profile` usan el `ConcurrencyToken` del expediente y devuelven el `PersonnelFileResponse` completo.
- Todos los `PUT` de colecciones son de reemplazo total de la subseccion.
- `personal-info` actualiza los campos escalares del expediente, valida custom data contra definiciones activas y no permite `Candidate -> Employee`.
- `identifications` revalida unicidad tenant-wide por `IdentificationType + IdentificationNumber` normalizado.
- `family-members` exige consistencia entre banderas condicionales y datos dependientes:
- si `IsStudying=true`, se esperan `StudyPlace` y `AcademicLevel`
- si `IsWorking=true`, se esperan `Workplace` y `JobTitle`
- si `IsDeceased=true`, se espera `DeceasedDate`
- `educations`, `languages`, `trainings`, `previous-employments` y `references` validan codigos contra catalogos activos del modulo.
- Los catalogos curriculares observables usados desde este bloque incluyen `CurriculumEducationStatus`, `CurriculumStudyType`, `CurriculumShift`, `CurriculumModality`, `CurriculumLanguage`, `CurriculumLanguageLevel`, `CurriculumTrainingType`, `CurriculumDurationUnit`, `CurriculumReferenceType`, `Country` y `Currency`.
- `educations` exige `EndDate` cuando `IsCurrentlyStudying=false` y evita `ApprovedSubjects > TotalSubjects`.
- `languages` exige que al menos uno de `Speaks`, `Writes` o `Reads` sea `true`.
- `trainings` valida pais, tipo, unidad de duracion y moneda de costo contra catalogos activos.

#### 5.10.8 Employment

Route family:

- `POST /api/v1/personnel-files/{id}/hire`
- `PUT /api/v1/personnel-files/{id}/employee-profile`
- `PUT /api/v1/personnel-files/{id}/employment-assignments`
- `PUT /api/v1/personnel-files/{id}/contract-history`
- `GET /api/v1/personnel-files/{id}/position-hierarchy`
- `PUT /api/v1/personnel-files/{id}/authorization-substitutions`
- `POST /api/v1/personnel-files/{id}/personnel-actions`
- `GET /api/v1/personnel-files/{id}/personnel-actions`
- `GET /api/v1/personnel-files/{id}/personnel-actions/export`
- `PUT /api/v1/personnel-files/{id}/assets-accesses`

Uso principal:

- formalizar la contratacion
- mantener la capa laboral del expediente
- consultar y exportar movimientos laborales

Observaciones funcionales:

- Todo el bloque, salvo `hire`, exige que el expediente ya sea `Employee`; si no, responde `PERSONNEL_FILE_STATE_RULE_VIOLATION`.
- `hire` exige que el expediente sea `Candidate`; si ya no lo es, tambien responde `PERSONNEL_FILE_STATE_RULE_VIOLATION`.
- `hire` cambia el `RecordType` del expediente a `Employee` y crea el `employee profile` inicial.
- en `hire`, `ContractStartDate` se inicializa con `HireDate` y `OrgUnitId` hereda el `OrgUnit` actual del expediente.
- `employee-profile` hace upsert del perfil laboral y permite vincular `PositionSlotId`, `JobProfileId`, `OrgUnitId`, `WorkCenterId`, `CostCenterId`, vigencias contractuales y `VacationConfigurationJson`.
- `employment-assignments`, `contract-history`, `authorization-substitutions` y `assets-accesses` reemplazan la coleccion completa de ese subrecurso.
- `position-hierarchy` devuelve `ImmediateSupervisorPersonnelFileId`, `ImmediateSupervisorName` y la coleccion de subordinados.
- `personnel-actions` agrega un evento individual de personal con fechas efectivas, monto opcional, moneda, descripcion y referencia.
- `search personnel-actions` soporta `fromUtc`, `toUtc`, `type`, `status`, `q`, `sortBy`, `sortDirection`, `page` y `pageSize`.
- `search personnel-actions` busca por `Description`, `Reference`, `ActionTypeCode` y `ActionStatusCode`.
- `search personnel-actions` usa `ActionDateUtc DESC` por defecto.
- `personnel-actions/export` soporta `csv|xlsx`.
- `personnel-actions/export` admite `sortBy` observable en `actionDateUtc`, `createdAtUtc`, `type/actionTypeCode`, `status/actionStatusCode` y `amount`.
- Las escrituras de `Employment` tocan deliberadamente el expediente padre, por lo que rotan su `ConcurrencyToken` aunque la respuesta sea de tipo laboral y no `PersonnelFileResponse`.

#### 5.10.9 Compensation

Route family:

- `PUT /api/v1/personnel-files/{id}/salary-items`
- `PUT /api/v1/personnel-files/{id}/additional-benefits`
- `PUT /api/v1/personnel-files/{id}/payment-methods`
- `PUT /api/v1/personnel-files/{id}/payroll-transactions`
- `GET /api/v1/personnel-files/{id}/payroll-transactions`
- `GET /api/v1/personnel-files/{id}/payroll-transactions/export`
- `PUT /api/v1/personnel-files/{id}/insurances`
- `PUT /api/v1/personnel-files/{id}/medical-claims`
- `PUT /api/v1/personnel-files/{id}/bank-accounts`

Uso principal:

- mantener salario, beneficios, medios de pago, historico de planilla, seguros y reclamos

Observaciones funcionales:

- Todo el bloque exige que el expediente sea `Employee`.
- Todos los `PUT` son de reemplazo total de la subseccion.
- `salary-items` mantiene rubros salariales con vigencias, tipo de ingreso, rubrica, moneda y periodo de pago.
- `additional-benefits` mantiene beneficios adicionales activos o historicos.
- `payment-methods` usa vigencias `EffectiveFromUtc/EffectiveToUtc` y puede apuntar a un `BankAccountId`.
- `payroll-transactions` mantiene el historico detallado de movimientos de planilla, con metadatos de integracion (`SourceSystem`, `SourceReference`, `SourceSyncedUtc`).
- `search payroll-transactions` soporta `fromUtc`, `toUtc`, `type`, `status`, `q`, `sortBy`, `sortDirection`, `page` y `pageSize`.
- en `payroll-transactions`, el filtro `status` no representa un campo persistido; hoy mapea a polaridad del movimiento:
- `DEBIT` o `DISCOUNT` filtra `IsDebit=true`
- `CREDIT` o `EARNING` filtra `IsDebit=false`
- `search payroll-transactions` usa `TransactionDateUtc DESC` por defecto.
- `payroll-transactions/export` soporta `csv|xlsx`.
- `payroll-transactions/export` admite `sortBy` observable en `transactionDateUtc`, `createdAtUtc`, `type/transactionTypeCode` y `amount`.
- `insurances` reemplaza tambien el conjunto de beneficiarios de cada seguro.
- `bank-accounts` es una excepcion del bloque:
- se expone desde `Compensation`
- muta el agregado base del expediente
- devuelve `PersonnelFileResponse` completo
- aparece tambien en `get by id` y `print`
- Las escrituras de `Compensation` tambien tocan el expediente padre y por eso hacen rotar su `ConcurrencyToken`.

#### 5.10.10 Talent

Route family:

- `PUT /api/v1/personnel-files/{id}/evaluations`
- `GET /api/v1/personnel-files/{id}/evaluations`
- `PUT /api/v1/personnel-files/{id}/position-competency-results`
- `GET /api/v1/personnel-files/{id}/position-competencies`
- `PUT /api/v1/personnel-files/{id}/selection-contests`
- `GET /api/v1/personnel-files/{id}/selection-contests`
- `PUT /api/v1/personnel-files/{id}/curricular-competencies`

Uso principal:

- mantener evidencias de desempeno, brechas de competencia, concursos y competencias curriculares

Observaciones funcionales:

- Todo el bloque exige que el expediente sea `Employee`.
- Todos los `PUT` son de reemplazo total.
- Los endpoints `GET` del bloque devuelven la coleccion completa del subrecurso; no hay paginacion.
- `evaluations` mantiene resultados de evaluacion con score cuantitativo, score cualitativo y comentario.
- `position-competency-results` mantiene resultados observados por `CompetencyCode`; el endpoint lee el estado persistido del expediente, no una recomputacion en vivo desde `CompetencyFramework`.
- `selection-contests` registra resultados de concursos internos o externos.
- `curricular-competencies` registra requerimientos, dominio y experiencia observada en el expediente.
- Todos los subrecursos de `Talent` exponen metadatos de integracion (`SourceSystem`, `SourceReference`, `SourceSyncedUtc`) cuando aplica.
- Las escrituras de `Talent` tambien tocan el expediente padre y rotan su `ConcurrencyToken`.

#### 5.10.11 Documents

Route family:

- `POST /api/v1/personnel-files/{id}/documents`
- `PATCH /api/v1/personnel-file-documents/{documentId}/inactivate`
- `GET /api/v1/personnel-file-documents/{documentId}/download`
- `POST /api/v1/personnel-files/{id}/observations`

Uso principal:

- adjuntar evidencias documentales al expediente
- descargar documentos
- inactivar documentos historicos
- registrar observaciones internas

Observaciones funcionales:

- `upload document` usa `multipart/form-data`.
- `upload document` exige archivo no vacio y usa el `ConcurrencyToken` actual del expediente.
- `upload document` calcula y persiste `sha256` del binario cargado.
- `upload document` valida fechas de entrega, prestamo y devolucion; rangos invalidos responden `PERSONNEL_FILE_DOCUMENT_DATES_INVALID`.
- `upload document` devuelve `PersonnelFileDocumentMetadataResponse`, no el expediente completo.
- `inactivate document` usa `ConcurrencyToken` del documento, no del expediente.
- `inactivate document` es soft-delete logico sobre el documento.
- `download document` devuelve el binario y valida tenant scope del documento.
- `add observation` usa el `ConcurrencyToken` del expediente y devuelve solo la observacion creada.
- Comportamiento observable actual de `inactivate document`:
- la respuesta tiene shape de metadata
- pero varios campos se rehidratan de forma parcial en la implementacion actual
- por eso solo deben considerarse confiables `Id`, `FileName`, `ContentType`, `SizeBytes`, `IsActive` y `ConcurrencyToken`

#### 5.10.12 Administration

Route family:

- `GET /api/v1/companies/{companyId}/personnel-catalogs/{category}`
- `GET /api/v1/companies/{companyId}/personnel-custom-field-definitions`
- `POST /api/v1/companies/{companyId}/personnel-custom-field-definitions`
- `PUT /api/v1/personnel-custom-field-definitions/{id}`

Uso principal:

- exponer catalogos funcionales del modulo
- administrar campos custom del expediente

Observaciones funcionales:

- `personnel-catalogs/{category}` es read-only y devuelve solo items activos.
- `personnel-catalogs/{category}` ordena por `SortOrder`, luego `Name`.
- `personnel-custom-field-definitions` soporta filtro opcional `isActive`.
- `PersonnelCustomFieldType` hoy expone `String`, `Number`, `Date`, `Bool` y `Select`.
- `create/update custom-field-definition` valida `Key` con la misma regex de codigos del modulo, exige `Label`, `SortOrder >= 0` y limita `OptionsJson` a `12000` caracteres.
- la key custom es unica por tenant en comparacion normalizada.
- `update custom-field-definition` requiere `ConcurrencyToken`.
- `create` y `personal-info update` validan `customDataJson` contra las definiciones activas del tenant.
- esa validacion exige presencia de campos requeridos y coherencia de tipo por key.
- la validacion actual no rechaza propiedades extra que no tengan definicion activa; simplemente valida las keys conocidas.

#### 5.10.13 Reporting

Route family:

- `GET /api/v1/personnel-files/{id}/print`
- `POST /api/v1/companies/{companyId}/personnel-files/dynamic-query`
- `GET /api/v1/companies/{companyId}/personnel-files/export`
- `GET /api/v1/companies/{companyId}/personnel-files/analytics/summary`

Uso principal:

- imprimir el expediente base
- ejecutar consultas dinamicas sobre expedientes
- exportar listados masivos
- resumir indicadores del modulo

Observaciones funcionales:

- `print` acepta `sections` como lista separada por comas.
- las secciones soportadas hoy son:
- `personal-info`
- `identifications`
- `addresses`
- `emergency-contacts`
- `family-members`
- `hobbies`
- `employee-relations`
- `bank-accounts`
- `associations`
- `educations`
- `languages`
- `trainings`
- `previous-employments`
- `references`
- `documents`
- `observations`
- si `sections` se omite, `print` incluye todas las secciones soportadas.
- `print` filtra el `PersonnelFileResponse` del agregado base; nunca imprime employment, compensation ni talent.
- Comportamiento observable actual de `print`:
- la seccion `personal-info` es aceptada en la API
- pero el filtro actual solo vacia colecciones
- por eso los campos escalares base del expediente siguen presentes aunque `personal-info` no se incluya explicitamente
- `dynamic-query` acepta body con `Filters`, `GroupBy`, `Sort`, `Q`, `Page`, `PageSize` e `IncludeAllowedActions`.
- `dynamic-query` permite como maximo `3` campos de agrupacion.
- cada campo agrupado devuelve como maximo `100` buckets.
- los campos groupables hoy son `recordtype`, `maritalstatus`, `nationality`, `orgunitid` e `isactive`.
- los campos sortables hoy son `fullname`, `firstname`, `lastname`, `birthdate`, `age`, `recordtype`, `maritalstatus`, `nationality`, `profession`, `orgunitid`, `isactive`, `createdatutc` y `modifiedatutc`.
- `dynamic-query` soporta estos filtros observables:
- `recordtype`: `eq`, `in`
- `maritalstatus`: `eq`, `in`
- `nationality`: `eq`, `in`
- `profession`: `eq`, `contains`
- `orgunitid`: `eq`, `in`
- `isactive`: `eq`
- `age`: `eq`, `gte`, `lte`, `between`
- `birthdate`: `eq`, `gte`, `lte`, `between`
- `createdatutc`: `eq`, `gte`, `lte`, `between`
- `firstname`: `eq`, `contains`
- `lastname`: `eq`, `contains`
- `fullname`: `eq`, `contains`
- `dynamic-query` tambien busca por numero de identificacion cuando se usa `Q`.
- `export` soporta `csv|xlsx` y expone filtros equivalentes al `search` core.
- `export` devuelve un dataset fino del expediente: no incluye employment, compensation, talent ni documentos.
- `analytics/summary` soporta `isActive`, `recordType`, `orgUnitId`, `minAge`, `maxAge` y `q`.
- `analytics/summary` devuelve `TotalCount`, `ActiveCount`, `InactiveCount` y breakdowns por `RecordType`, rango de edad y `OrgUnit`.
- `export` y `analytics/summary` no incluyen matching por identificacion en `q`; solo operan sobre nombre completo.

#### 5.10.14 Relacion con otros modulos

- `IAM` y `Auth` aportan autenticacion, roles, permisos y contexto tenant para todo el expediente.
- `Account companies` aporta la compania activa sobre la que se crea o consulta el expediente.
- `OrgUnits` alimenta `OrgUnitId` en el expediente base, en `employee-profile`, en asignaciones y en analytics.
- `Locations` alimenta `WorkCenterId` en `employee-profile` y en `employment-assignments`.
- `CostCenters` alimenta `CostCenterId` o `CostCenterCode` dentro de la capa laboral.
- `Job and competency design` aporta `JobProfiles`, `PositionSlots`, tipos de requerimiento y el contexto de competencias que luego se reflejan en `employee-profile`, jerarquia de posicion y resultados de competencias.
- `Reports` consume `PERSONNEL_FILES` como `resourceKey` para capacidades de export e impresion.

`PersonnelFiles` es asi el modulo que aterriza el resto del sistema sobre una persona real: primero nace el expediente, luego se completa el perfil, despues puede convertirse en empleado y finalmente se conecta con puestos, estructura, compensacion, talento y evidencia documental.

## 6. Reglas observables transversales

- las rutas autenticadas `api/v1` son tenant-scoped por defecto
- los listados usan normalmente paginacion, filtros o ambos
- muchas escrituras usan `concurrency token`
- export e impresion existen como endpoints explicitos, no como flags genericos
- los modulos administrativos sensibles usan RBAC y, en algunas areas, permisos por campo

## 7. Estado de OpenAPI

El repositorio ahora reserva [openapi.yaml](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api/openapi.yaml), pero el contrato exhaustivo machine-readable sigue generandose en runtime por Swagger en `Development`.

Estado actual:

- `endpoint-reference.md` es el resumen humano canonico
- Swagger runtime es el contrato generado exhaustivo
- `openapi.yaml` es un placeholder bootstrap hasta automatizar export/versionado
