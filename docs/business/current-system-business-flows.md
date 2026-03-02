# Current System Business Flows

## Purpose

Este documento explica el flujo actual del sistema construido hasta este punto del backend.

El objetivo es describir, de forma funcional y paso a paso:

- como inicia el sistema
- como se crea el primer usuario
- como se crea o administra una empresa
- como se crean usuarios internos
- como se crean roles y permisos
- como se asignan roles y permisos
- como se configura RBAC por recurso y por campo
- como se consulta auditoria
- como se relacionan todos los endpoints actuales dentro del proceso operativo

Este documento es de negocio y operacion. La referencia tecnica detallada de contratos vive en [api-endpoints-reference.md](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api-reference/api-endpoints-reference.md).

## Current Scope

El backend actual cubre estos bloques funcionales:

1. disponibilidad y estado del API
2. autenticacion local y autenticacion externa
3. provisioning inicial de empresa y administrador
4. gestion multiempresa a nivel cuenta
5. administracion de usuarios por empresa
6. administracion IAM de usuarios, roles y permisos
7. RBAC por recurso y accion
8. RBAC por campo
9. auditoria administrativa

## Core Business Rules

1. Todo acceso sensible es multi-tenant. Un usuario solo opera dentro de su empresa actual.
2. La API aplica seguridad real. La UI no define permisos.
3. El modelo de autorizacion es jerarquico:
   `HasAccess -> CRUD action -> Field visibility/editability`
4. Si no existe permiso explicito, el acceso se deniega.
5. Si un campo no es editable, la API rechaza el cambio. No lo ignora silenciosamente.
6. Los cambios administrativos clave quedan auditados.
7. Los roles, permisos y configuraciones RBAC se gestionan por empresa.
8. Los endpoints `account-level` solo permiten operar empresas creadas por el usuario autenticado.
9. No existe hard delete de empresa; la baja es logica por `Archived`.

## Actors

- `Public User`: usuario no autenticado.
- `Account Owner`: usuario autenticado que puede operar varias empresas propias desde la misma cuenta.
- `Company Admin`: administrador de empresa con capacidad de gestionar accesos.
- `Security Admin`: administrador que gestiona roles, permisos y RBAC.
- `Auditor`: usuario con acceso a lectura de logs.
- `System/API`: backend que valida tenant, permisos y auditoria.

## Global Flow Map

Orden natural del sistema hoy:

1. validar que el API esta arriba
2. registrar el primer usuario y crear la empresa inicial
3. autenticarse y obtener JWT
4. crear empresas adicionales si la cuenta tiene capacidad
5. cambiar la empresa activa cuando sea necesario
6. crear o ajustar roles
7. crear permisos personalizados si hacen falta
8. asignar permisos a los roles
9. configurar permisos RBAC por recurso
10. configurar permisos RBAC por campo
11. crear usuarios de empresa o usuarios IAM
12. asignar roles a usuarios
13. operar el sistema bajo restricciones RBAC
14. revisar auditoria y trazabilidad

## Flow 1. Validate API Availability

### Business goal

Confirmar que el backend esta operativo antes de iniciar pruebas o uso administrativo.

### Endpoint

- `GET /api/system/status`

### Step by step

1. Un usuario o una herramienta llama al endpoint de estado.
2. El sistema responde con hora UTC, nombre de la aplicacion y, si existe, contexto de autenticacion.
3. Este flujo sirve como verificacion inicial de infraestructura.

### Result

- confirma que el API esta disponible
- no requiere JWT
- no ejecuta logica de negocio sensible

## Flow 2. Register First User And Bootstrap Initial Company

### Business goal

Crear el primer usuario operativo y la empresa inicial para comenzar a usar el sistema.

### Main endpoint

- `POST /api/auth/register`

### Step by step

1. Un usuario publico envia nombre, apellido, email, password y datos basicos de empresa.
2. El sistema valida el formato del request.
3. El sistema crea el usuario local.
4. El sistema provisiona la empresa inicial asociada a ese usuario.
5. El sistema crea la membresia principal del usuario dentro de la empresa.
6. El sistema siembra los elementos base de IAM y RBAC necesarios para operar el tenant.
7. El sistema genera access token y refresh token.
8. El sistema devuelve sesion autenticada.

### Business outcome

- existe una empresa activa en el sistema
- existe un usuario administrador inicial
- el usuario ya puede operar endpoints protegidos usando el JWT recibido

## Flow 3. Authenticate With External Identity Provider

### Business goal

Permitir acceso con proveedor externo cuando el negocio requiera onboarding o login federado.

### Main endpoint

- `POST /api/auth/external`

### Step by step

1. El usuario envia `provider` e `idToken`.
2. El backend valida el token contra Google.
3. Si el usuario no existe, lo crea.
4. Si corresponde, provisiona empresa o lo vincula a un contexto valido.
5. Emite access token y refresh token.
6. Devuelve `201` si el usuario fue creado o `200` si solo se autentico.

### Business outcome

- onboarding externo o login externo funcionando
- el usuario termina con una sesion equivalente al flujo local

## Flow 4. Refresh Session

### Business goal

Mantener la sesion sin volver a registrar o volver a autenticar completamente al usuario.

### Main endpoint

- `POST /api/auth/refresh`

### Step by step

1. El cliente envia un refresh token valido.
2. El backend valida vigencia, estado y pertenencia del token.
3. Si el token es valido, emite un nuevo access token.
4. Si aplica, rota el refresh token.
5. Devuelve la sesion renovada.

### Business outcome

- continuidad de sesion
- control de expiracion y rotacion de tokens

## Flow 4A. Account Company Administration

Este flujo permite que una misma cuenta opere varias empresas propias sin mezclar ese proceso con RBAC tenant-scoped.

### Endpoints

- `GET /api/account/companies`
- `GET /api/account/companies/{companyId}`
- `POST /api/account/companies`
- `PUT /api/account/companies/{companyId}`
- `PATCH /api/account/companies/{companyId}/archive`
- `PATCH /api/account/companies/{companyId}/reactivate`
- `POST /api/account/companies/{companyId}/switch`

### Flow 4A.1. List owned companies

1. El usuario autenticado consulta sus empresas con `GET /api/account/companies`.
2. El backend resuelve el usuario actual.
3. El backend lista solo empresas creadas por esa cuenta.
4. Marca cual coincide con el `tid` actual del JWT.
5. Devuelve lista paginada.

Business value:

- visibilidad clara de las empresas bajo la misma cuenta
- separacion entre ownership de cuenta y tenant activo

### Flow 4A.2. Create an additional company

1. El usuario decide crear una empresa nueva desde su cuenta.
2. Llama `POST /api/account/companies`.
3. El backend valida capacidad segun la policy temporal de ownership.
4. El backend provisiona el tenant nuevo reutilizando el flujo actual de bootstrap.
5. Crea membership no primaria.
6. Mantiene intacta la empresa activa actual.
7. Registra auditoria `COMPANY_CREATED`.

Business value:

- crecimiento multiempresa desde una misma cuenta
- bootstrap consistente de empresa nueva sin romper el contexto actual

### Flow 4A.3. Update a company owned by the current account

1. El usuario selecciona una empresa propia.
2. Llama `PUT /api/account/companies/{companyId}`.
3. El backend valida ownership.
4. Actualiza solo `name`.
5. Mantiene `slug` inmutable.
6. Registra auditoria `COMPANY_UPDATED`.

Business value:

- mantenimiento simple del tenant sin alterar identidad tecnica

### Flow 4A.4. Archive an owned company

1. El usuario intenta archivar una empresa propia.
2. El backend valida ownership.
3. Si la empresa es la activa del token o la primaria actual, bloquea la operacion.
4. Si no es la activa, cambia estado a `Archived`.
5. Registra auditoria `COMPANY_ARCHIVED`.

Business value:

- baja logica segura
- evita dejar la sesion apuntando a una empresa archivada

### Flow 4A.5. Reactivate an owned company

1. El usuario selecciona una empresa archivada.
2. Llama `PATCH /api/account/companies/{companyId}/reactivate`.
3. El backend valida ownership.
4. Revalida el limite temporal de empresas activas.
5. Si el limite lo permite, reactiva la empresa.
6. Registra auditoria `COMPANY_REACTIVATED`.

Business value:

- recuperacion de empresa sin reprovisionar tenant

### Flow 4A.6. Switch active company

1. El usuario decide operar otra empresa propia.
2. Llama `POST /api/account/companies/{companyId}/switch`.
3. El backend valida ownership y membership activa.
4. Cambia la membership primaria.
5. Reemite JWT con nuevo `tid`.
6. Registra auditoria `ACTIVE_COMPANY_SWITCHED`.

Business value:

- cambio seguro de contexto tenant
- continuidad con el modelo actual basado en JWT

## Flow 5. Company User Administration

Este flujo cubre el usuario de negocio interno dentro de una empresa.

### Purpose

Permitir que un administrador de empresa invite, actualice y controle el estado de sus usuarios.

### Endpoints

- `GET /api/company/users`
- `POST /api/company/users`
- `PUT /api/company/users/{userId}`
- `PATCH /api/company/users/{userId}/deactivate`
- `PATCH /api/company/users/{userId}/reactivate`
- `POST /api/company/users/{userId}/reset-invitation`

### Flow 5.1. List company users

1. El admin consulta la lista de usuarios de su empresa.
2. El backend valida JWT, tenant y permiso `RBAC_USERS:Read`.
3. El backend busca solo usuarios del tenant actual.
4. El backend aplica permisos por campo del usuario que consulta.
5. Devuelve lista paginada.

Business value:

- visibilidad controlada de la plantilla de usuarios
- proteccion de campos sensibles por RBAC nivel 3

### Flow 5.2. Invite a new company user

1. El admin decide incorporar un usuario a la empresa.
2. Selecciona o define el rol que debe llevar ese usuario.
3. Llama `POST /api/company/users` con email, nombre y `roleId`.
4. El backend valida `RBAC_USERS:Create`.
5. El backend valida permisos por campo para el request de creacion.
6. El backend confirma que el rol pertenece al mismo tenant.
7. El backend crea el usuario invitado y la membresia de empresa.
8. El backend genera una invitacion con expiracion.
9. El backend registra auditoria del evento.

Business value:

- onboarding controlado de usuarios
- asignacion inicial de rol desde el primer momento

### Flow 5.3. Update a company user

1. El admin selecciona un usuario existente.
2. Cambia nombre, apellido o rol.
3. Llama `PUT /api/company/users/{userId}`.
4. El backend valida `RBAC_USERS:Update`.
5. El backend comprueba que el usuario pertenece al tenant actual.
6. El backend detecta los campos modificados.
7. El backend valida editabilidad por campo.
8. Si algun campo no es editable, responde `FIELD_EDIT_FORBIDDEN`.
9. Si todo es valido, aplica el cambio y registra auditoria.

Business value:

- gobierno seguro de cambios sobre usuarios
- enforcement real de permisos por campo

### Flow 5.4. Deactivate a company user

1. El admin selecciona un usuario activo.
2. Llama `PATCH /api/company/users/{userId}/deactivate`.
3. El backend valida `RBAC_USERS:Update`.
4. El backend valida tenant ownership.
5. El backend desactiva el usuario.
6. El backend registra auditoria.

Business value:

- baja administrativa sin borrado fisico
- bloqueo operacional del acceso

### Flow 5.5. Reactivate a company user

1. El admin selecciona un usuario inactivo.
2. Llama `PATCH /api/company/users/{userId}/reactivate`.
3. El backend valida `RBAC_USERS:Update`.
4. El backend valida tenant ownership.
5. El backend reactiva el usuario.
6. El backend registra auditoria.

Business value:

- reactivacion controlada sin recrear identidades

### Flow 5.6. Reset an invitation

1. El admin detecta que una invitacion expiro o debe reenviarse.
2. Llama `POST /api/company/users/{userId}/reset-invitation`.
3. El backend valida `RBAC_USERS:Update`.
4. El backend valida tenant ownership.
5. El backend invalida invitaciones anteriores.
6. El backend emite una nueva invitacion.
7. El backend registra auditoria.

Business value:

- recuperacion del flujo de onboarding

## Flow 6. IAM User Administration

Este flujo administra usuarios IAM del tenant desde el modulo de identidad.

### Endpoints

- `POST /api/iam/users`
- `GET /api/iam/users`
- `GET /api/iam/users/{userId}`
- `PUT /api/iam/users/{userId}/roles`

### Flow 6.1. Create an IAM user

1. Un admin de seguridad decide crear un usuario IAM directamente.
2. Llama `POST /api/iam/users`.
3. El backend valida `RBAC_USERS:Create`.
4. El backend valida unicidad del email dentro del tenant.
5. El backend valida los `roleIds` enviados.
6. El backend crea el usuario.
7. El backend registra auditoria administrativa.

Business value:

- administracion IAM directa desde el backend
- base para escenarios de backoffice o soporte interno

### Flow 6.2. List IAM users

1. El admin consulta `GET /api/iam/users`.
2. El backend valida `RBAC_USERS:Read`.
3. Devuelve usuarios IAM del tenant actual.

### Flow 6.3. Get IAM user detail

1. El admin consulta un usuario puntual.
2. El backend valida `RBAC_USERS:Read`.
3. El backend busca el usuario dentro del tenant actual.
4. Si el usuario existe en otro tenant, puede responder `TENANT_MISMATCH`.

### Flow 6.4. Sync roles of an IAM user

1. El admin decide cambiar la asignacion de roles del usuario.
2. Llama `PUT /api/iam/users/{userId}/roles`.
3. El backend valida `RBAC_USERS:Update`.
4. El backend valida usuario y roles dentro del tenant.
5. El backend sincroniza los roles.
6. El backend registra auditoria.

Business value:

- control centralizado de acceso por rol

## Flow 7. Role Lifecycle Management

Este bloque define como se crean, actualizan y eliminan roles.

### Endpoints

- `POST /api/iam/roles`
- `GET /api/iam/roles`
- `GET /api/iam/roles/{roleId}`
- `PUT /api/iam/roles/{roleId}`
- `POST /api/iam/roles/{roleId}/clone`
- `DELETE /api/iam/roles/{roleId}`
- `PUT /api/iam/roles/{roleId}/permissions`
- `PUT /api/iam/roles/{roleId}/users`

### Flow 7.1. Create a role

1. El admin de seguridad identifica la necesidad de un perfil nuevo.
2. Llama `POST /api/iam/roles`.
3. El backend valida `RBAC_ROLES:Create`.
4. El backend valida nombre unico por tenant.
5. El backend crea el rol.
6. El backend registra `ROLE_CREATED`.

Business value:

- segmentacion funcional de accesos por perfil

### Flow 7.2. List roles

1. El admin consulta `GET /api/iam/roles`.
2. El backend valida `RBAC_ROLES:Read`.
3. Devuelve roles del tenant actual.

### Flow 7.3. Get role detail

1. El admin consulta `GET /api/iam/roles/{roleId}`.
2. El backend valida `RBAC_ROLES:Read`.
3. Devuelve el rol y sus permisos asignados.

### Flow 7.4. Update a role

1. El admin ajusta nombre o descripcion.
2. Llama `PUT /api/iam/roles/{roleId}`.
3. El backend valida `RBAC_ROLES:Update`.
4. El backend impide modificar roles protegidos del sistema.
5. El backend aplica cambios y registra `ROLE_UPDATED`.

Business value:

- evolucion controlada de perfiles existentes

### Flow 7.5. Clone a role

1. El admin desea partir de un rol existente.
2. Llama `POST /api/iam/roles/{roleId}/clone`.
3. El backend valida `RBAC_ROLES:Create`.
4. Duplica metadatos y permisos del rol origen.
5. Registra `ROLE_CLONED`.

Business value:

- acelerar la creacion de nuevos perfiles sin reconstruir todo desde cero

### Flow 7.6. Delete a role

1. El admin identifica un rol obsoleto.
2. Llama `DELETE /api/iam/roles/{roleId}`.
3. El backend valida `RBAC_ROLES:Delete`.
4. El backend rechaza roles protegidos.
5. El backend rechaza roles con usuarios asignados.
6. Si pasa las reglas, elimina el rol.

Business value:

- higiene del modelo de autorizacion

### Flow 7.7. Sync permissions assigned to a role

1. El admin necesita asignar permisos IAM directos al rol.
2. Llama `PUT /api/iam/roles/{roleId}/permissions`.
3. El backend valida `RBAC_ROLES:Update`.
4. El handler valida ademas `RBAC_PERMISSIONS:Update`.
5. El backend sincroniza la lista de permisos.
6. El backend registra la auditoria correspondiente cuando aplica.

Business value:

- union entre el rol y los permisos atómicos que lo componen

### Flow 7.8. Sync users assigned to a role

1. El admin decide quienes pertenecen a un rol dado.
2. Llama `PUT /api/iam/roles/{roleId}/users`.
3. El backend valida `RBAC_ROLES:Update`.
4. El backend valida que los usuarios pertenezcan al tenant.
5. El backend sincroniza miembros del rol.

Business value:

- gestion por rol desde la perspectiva del perfil y no solo desde la perspectiva del usuario

## Flow 8. Permission Catalog Management

Este bloque cubre la administracion de permisos IAM atomicos.

### Endpoints

- `POST /api/iam/permissions`
- `GET /api/iam/permissions`
- `GET /api/iam/permissions/{permissionId}`

### Flow 8.1. Create a screen action permission

1. El admin necesita un permiso funcional nuevo de tipo accion.
2. Llama `POST /api/iam/permissions` con `kind = ScreenAction`.
3. El backend valida `RBAC_PERMISSIONS:Create`.
4. El backend valida que `action` exista y que no se usen campos incompatibles.
5. El backend crea el permiso.

Business value:

- extender el catalogo de capacidades del sistema

### Flow 8.2. Create a field permission

1. El admin necesita un permiso de campo nuevo.
2. Llama `POST /api/iam/permissions` con `kind = Field`.
3. El backend valida `RBAC_PERMISSIONS:Create`.
4. El backend exige `fieldName` y `fieldAccess`.
5. El backend crea el permiso.

Business value:

- base para granularidad adicional y evolucion del modelo de acceso

### Flow 8.3. List permissions

1. El admin consulta `GET /api/iam/permissions`.
2. El backend valida `RBAC_PERMISSIONS:Read`.
3. Devuelve el catalogo del tenant.

### Flow 8.4. Get permission detail

1. El admin consulta `GET /api/iam/permissions/{permissionId}`.
2. El backend valida `RBAC_PERMISSIONS:Read`.
3. Devuelve el detalle del permiso.

## Flow 9. RBAC Matrix By Resource And Action

Este flujo administra RBAC nivel 1 y 2.

### Endpoints

- `GET /api/rbac/resources`
- `GET /api/rbac/roles/{roleId}/permissions`
- `PUT /api/rbac/roles/{roleId}/permissions`
- `GET /api/rbac/audit`
- `GET /api/iam/roles/{roleId}/permission-matrix`
- `PUT /api/iam/roles/{roleId}/permission-matrix`

### Flow 9.1. View available RBAC resources

1. El admin consulta `GET /api/rbac/resources`.
2. El backend valida `RBAC_PERMISSIONS:Read`.
3. Devuelve el catalogo de recursos configurables.

Business value:

- base para construir la matriz de permisos del tenant

### Flow 9.2. View role permissions by resource

1. El admin selecciona un rol.
2. Consulta `GET /api/rbac/roles/{roleId}/permissions`.
3. El backend valida `RBAC_PERMISSIONS:Read`.
4. Devuelve el estado por recurso:
   `hasAccess`, `canRead`, `canCreate`, `canUpdate`, `canDelete`.

Business value:

- ver el acceso efectivo del rol por pantalla o recurso

### Flow 9.3. Update role permissions by resource

1. El admin define la matriz del rol.
2. Llama `PUT /api/rbac/roles/{roleId}/permissions`.
3. El backend valida `RBAC_PERMISSIONS:Update`.
4. El backend valida recursos y tenant.
5. El backend hace upsert de la configuracion.
6. El backend invalida cache de autorizacion.
7. El backend registra auditoria administrativa y auditoria RBAC.

Business value:

- enforcement de acceso por pantalla y CRUD

### Flow 9.4. View RBAC permission audit

1. El admin o auditor consulta `GET /api/rbac/audit` con filtros y paginacion opcional.
2. El backend valida `RBAC_PERMISSIONS:Read`.
3. Filtra por tenant.
4. Puede filtrar por rol, recurso, rango de fechas, `page` y `pageSize`.
5. Devuelve cambios historicos de matriz RBAC en formato paginado.

Business value:

- trazabilidad de cambios de autorizacion

### Flow 9.5. Compatibility flow for permission matrix

Los endpoints de `permission-matrix` en `iam/roles` siguen vivos por compatibilidad y cumplen el mismo objetivo operativo de matriz RBAC.

Flujo:

1. `GET /api/iam/roles/{roleId}/permission-matrix` para consultar matriz consolidada.
2. `PUT /api/iam/roles/{roleId}/permission-matrix` para actualizarla.
3. El backend valida rol, permisos, catalogo y registra auditoria.

Business value:

- soporte al flujo de matriz historico sin romper compatibilidad

## Flow 10. RBAC By Field

Este bloque cubre RBAC nivel 3.

### Endpoints

- `GET /api/rbac/resources/{resourceKey}/fields`
- `GET /api/rbac/roles/{roleId}/field-permissions?resourceKey=...`
- `PUT /api/rbac/roles/{roleId}/field-permissions`

### Flow 10.1. View field catalog of a resource

1. El admin consulta `GET /api/rbac/resources/{resourceKey}/fields`.
2. El backend valida `RBAC_PERMISSIONS:Read`.
3. Devuelve los campos configurables del recurso.

Business value:

- conocer que partes del recurso pueden gobernarse por campo

### Flow 10.2. View field permissions of a role

1. El admin selecciona rol y recurso.
2. Llama `GET /api/rbac/roles/{roleId}/field-permissions?resourceKey=RBAC_USERS`.
3. El backend valida `RBAC_PERMISSIONS:Read`.
4. Devuelve visibilidad, editabilidad, required, masked y read-only derivado.

Business value:

- lectura clara del acceso granular efectivo

### Flow 10.3. Update field permissions of a role

1. El admin decide que campos se ven y cuales se editan.
2. Llama `PUT /api/rbac/roles/{roleId}/field-permissions`.
3. El backend valida `RBAC_PERMISSIONS:Update`.
4. El backend valida tenant, rol, recurso y existencia de `fieldKey`.
5. El backend normaliza reglas, por ejemplo:
   `isVisible = false` obliga `isEditable = false`.
6. El backend guarda overrides por campo.
7. El backend invalida cache.
8. El backend registra `ROLE_FIELD_PERMISSIONS_UPDATED`.

Business value:

- control granular de datos sensibles
- principio de minimo privilegio real

## Flow 11. Runtime Enforcement Of Authorization

Este flujo no es un endpoint aislado; es el comportamiento transversal de la API.

### What happens in every protected endpoint

1. La API valida autenticacion.
2. Resuelve el tenant actual.
3. Valida acceso por recurso y accion.
4. Si es lectura sensible, aplica visibilidad de campos.
5. Si es escritura, identifica campos modificados.
6. Valida editabilidad por campo.
7. Si el recurso consultado pertenece a otro tenant, responde `TENANT_MISMATCH`.
8. Si falta permiso, responde `RBAC_DENIED`.
9. Si se intenta editar un campo no permitido, responde `FIELD_EDIT_FORBIDDEN`.

### Business impact

- seguridad consistente en todos los endpoints sensibles
- aislamiento estricto por empresa
- cumplimiento de segregacion de funciones

## Flow 12. Administrative Audit Logs

Este bloque permite investigar cambios administrativos.

### Endpoints

- `GET /api/audit/logs`
- `GET /api/audit/logs/{auditLogId}`

### Flow 12.1. List audit logs

1. Un auditor o admin con permiso consulta `GET /api/audit/logs`.
2. El backend valida `AUDIT_LOGS:Read`.
3. El backend filtra siempre por tenant actual.
4. El usuario puede filtrar por:
   actor, fecha, entity type, event type y search.
5. El backend devuelve lista paginada de eventos.

Business value:

- visibilidad transversal de cambios administrativos

### Flow 12.2. View audit log detail

1. El auditor abre un evento puntual con `GET /api/audit/logs/{auditLogId}`.
2. El backend valida `AUDIT_LOGS:Read`.
3. El backend comprueba tenant ownership del log.
4. Devuelve `before`, `after`, `diff`, actor, IP y user agent si existen.

Business value:

- investigacion forense y trazabilidad operativa

## Flow 13. End-To-End Business Journeys

## Journey A. From zero to an operational company

1. Verificar salud del API con `GET /api/system/status`.
2. Registrar primer usuario con `POST /api/auth/register`.
3. Guardar `accessToken` y `refreshToken`.
4. Si la cuenta necesita otra empresa, crearla con `POST /api/account/companies`.
5. Si hace falta operar esa nueva empresa, cambiar contexto con `POST /api/account/companies/{companyId}/switch`.
6. Consultar roles existentes con `GET /api/iam/roles`.
7. Si hace falta, crear rol con `POST /api/iam/roles`.
8. Configurar permisos del rol con:
   `PUT /api/rbac/roles/{roleId}/permissions`
9. Configurar permisos por campo con:
   `PUT /api/rbac/roles/{roleId}/field-permissions`
10. Invitar usuarios de empresa con `POST /api/company/users`.
11. Revisar logs administrativos con `GET /api/audit/logs`.

## Journey B. Create a role and assign it to a user

1. Crear rol con `POST /api/iam/roles`.
2. Consultar recursos RBAC con `GET /api/rbac/resources`.
3. Configurar permisos por recurso con `PUT /api/rbac/roles/{roleId}/permissions`.
4. Consultar campos del recurso con `GET /api/rbac/resources/{resourceKey}/fields`.
5. Configurar permisos por campo con `PUT /api/rbac/roles/{roleId}/field-permissions`.
6. Crear usuario IAM o usuario de empresa.
7. Asignar el rol al usuario con:
   `PUT /api/iam/users/{userId}/roles`
   o creando el company user con ese `roleId`.
8. Validar en auditoria con `GET /api/audit/logs`.

## Journey C. Investigate who changed permissions

1. Abrir `GET /api/rbac/audit?page=1&pageSize=20` para cambios de matriz RBAC.
2. Abrir `GET /api/audit/logs` filtrando:
   `entityType=Permission`
3. Tomar el `auditLogId` relevante.
4. Abrir `GET /api/audit/logs/{auditLogId}`.
5. Revisar `actor`, `summary`, `before`, `after` y `diff`.

## Journey D. Update a user safely under RBAC level 3

1. El admin abre el detalle del usuario desde la UI o backoffice.
2. El backend ya limita la visibilidad de campos en el listado o lectura.
3. El admin intenta actualizar nombre o rol.
4. El backend valida permisos por recurso.
5. El backend valida permisos por campo.
6. Si el cambio esta permitido, lo aplica.
7. Si no, responde error estandar.
8. El evento queda auditado.

## Journey E. Operate multiple companies from one account

1. El usuario se registra y recibe su empresa inicial.
2. Crea una empresa adicional con `POST /api/account/companies`.
3. Confirma que el contexto activo no cambio.
4. Consulta sus empresas con `GET /api/account/companies`.
5. Cambia a la nueva empresa con `POST /api/account/companies/{companyId}/switch`.
6. Usa el nuevo JWT para gestionar roles, usuarios y permisos de ese tenant.
7. Si deja de usar una empresa, la archiva con `PATCH /api/account/companies/{companyId}/archive`.
8. Si necesita recuperarla, la reactiva con `PATCH /api/account/companies/{companyId}/reactivate`.

## Full API Flow Inventory

Todos los flujos actuales del API quedan cubiertos en este documento:

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

## Suggested Maintenance Rule

Cada vez que se agregue una HU o una nueva iteracion, este documento debe actualizar:

1. nuevos flujos de negocio
2. cambios en el journey operativo
3. nuevas dependencias entre usuarios, roles, permisos y auditoria
4. nuevos endpoints o cambios de comportamiento relevantes
