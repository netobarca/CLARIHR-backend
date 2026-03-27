# Analisis actual de seguridad

## 1. Resumen ejecutivo

El backend mantiene una postura de seguridad consistente con un SaaS multi-tenant de RRHH:

- autenticacion JWT con refresh tokens
- soporte de autenticacion externa
- tenant isolation por claim y por validacion en handlers
- RBAC por recurso y accion
- permisos por campo
- auditoria de operaciones sensibles

La base es solida, pero aun hay controles operativos que conviene formalizar mejor, especialmente rate limiting, automatizacion de OpenAPI y endurecimiento documental.

## 2. Autenticacion

La autenticacion se configura en [Program.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Program.cs#L96) mediante `JwtBearer`.

El sistema soporta:

- registro local
- login local
- refresh token
- logout
- autenticacion externa con proveedor soportado

En registro local, la API exige una politica de password reforzada:

- longitud minima de `12`
- mayuscula, minuscula, numero y caracter especial
- sin espacios
- sin reutilizar nombre, apellido o correo del usuario dentro del password

El token de acceso cambia funcionalmente cuando el usuario activa una compania: el token con tenant incluye el claim que define el contexto operativo.

Adicionalmente, JWT puede emitir el rol global `platform_admin` cuando el correo autenticado pertenece al allow-list `Authentication:Jwt:PlatformAdminEmails`. Esto permite autenticar usuarios de plataforma sin depender de una membresia tenant-scoped.

## 3. Resolucion de tenant

`HttpTenantContext` resuelve el tenant desde claims como `tid`, `tenantid` y variantes equivalentes en [HttpTenantContext.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Tenancy/HttpTenantContext.cs#L1).

Esto soporta:

- tokens propios del sistema
- cierta tolerancia a nombres alternativos de claims

El tenant no depende solo de la ruta; tambien se verifica en Application e Infrastructure.

## 4. Autorizacion

La autorizacion actual se aplica en varias capas:

- `[Authorize]` en controllers
- `AuthorizeResource` para endpoints gobernados por RBAC
- servicios de autorizacion por modulo
- verificaciones `EnsureCanReadAsync` y `EnsureCanManageAsync` dentro de handlers

Este patron reduce el riesgo de que un endpoint aparente estar protegido solo por decoracion HTTP.

El catalogo global de planes comerciales agrega una ruta de autorizacion fuera del tenant activo: `/api/account/commercial-plans` usa un servicio dedicado que exige autenticacion valida y rol `platform_admin`, sin pasar por `AuthorizeResource` ni por matrices RBAC tenant-scoped.

## 5. Tenant isolation

La aislacion entre tenants es una regla central del sistema:

- las operaciones tenant-scoped exigen `tenant claim`
- los servicios de autorizacion comparan `companyId` solicitado contra el tenant activo
- cuando un recurso existe fuera del tenant, el sistema responde con `tenant mismatch` en vez de exponer el dato

Este patron aparece de forma consistente en modulos como personnel files, org units, locations, competency framework y salary tabulator.

La excepcion disenada actual es `CommercialPlansController`: el recurso es global y no tenant-scoped, por lo que la proteccion se basa en el rol de plataforma y no en `tenant mismatch`.

## 6. RBAC y permisos por campo

El sistema no se limita a permisos coarse-grained. Tambien implementa:

- catalogo de recursos RBAC
- matrices de permisos por rol
- perfiles efectivos por campo
- overrides por rol y recurso
- auditoria de cambios a permisos por campo

La infraestructura de permisos por campo se apoya en cache y servicios dedicados, lo que permite proteger informacion sensible mas alla del acceso al endpoint.

## 7. Auditoria

`AuditService` en [AuditService.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auditing/AuditService.cs#L1) exige tenant context para registrar auditoria.

La auditoria conserva:

- usuario actor
- correo del actor
- tenant
- accion
- entidad
- before / after / diff sanitizados
- IP remota
- user-agent

Esto es especialmente importante en expedientes, permisos, estructura, compensacion y gobierno de identidades.

Las escrituras globales sobre el catalogo comercial de planes no usan `AuditService` porque hoy ese servicio exige `tenant context`. Por ahora quedan registradas mediante logging estructurado de application handlers, lo que mantiene trazabilidad operativa pero no reemplaza una auditoria global persistente.

## 8. Controles HTTP y middleware

La API agrega controles basicos de seguridad en middleware:

- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: no-referrer`
- `Cache-Control: no-store` para rutas de auth e IAM
- correlacion de request mediante `X-Correlation-ID`
- `ProblemDetails` para manejo uniforme de errores

En produccion se aplica `UseHttpsRedirection()`. Swagger queda limitado a `Development`.

## 9. Exposicion de datos sensibles

Los modulos mas sensibles del sistema son:

- `PersonnelFiles`
- `SalaryTabulator`
- `IdentityAccess`
- `LegalRepresentatives`
- `Audit`

La combinacion de tenant scope, RBAC, permisos por campo y auditoria reduce el riesgo de exposicion lateral, aunque la disciplina debe mantenerse al agregar endpoints nuevos.

## 10. Riesgos y brechas actuales

### 10.1 Rate limiting no visible

No se observa en el pipeline actual un control explicito de rate limiting o proteccion anti abuso para autenticacion y operaciones de alto costo.

### 10.2 Swagger no versionado en docs

El contrato OpenAPI existe en runtime solo para `Development`, pero no hay exportacion automatizada versionada en `docs/technical/api/`.

### 10.3 Security headers minimos

Los headers actuales son utiles, pero todavia no cubren un set mas completo de endurecimiento HTTP.

### 10.4 Dependencia de disciplina por modulo

La seguridad esta bien distribuida, pero el numero alto de endpoints administrativos exige mantener mucha consistencia al seguir creando handlers y services.

### 10.5 Falta screening contra contrasenas comprometidas

La politica local ya endurece longitud, complejidad y uso de datos personales, pero todavia no existe verificacion contra listas de contrasenas comprometidas o comunmente reutilizadas.

### 10.6 Auditoria global pendiente para writes de plataforma

El sistema ya soporta writes globales de `CommercialPlan` administrados por `platform_admin`, pero todavia no existe un `AuditLog` no tenant-scoped para persistir before/after de estas acciones.

## 11. Recomendaciones inmediatas

1. Incorporar una estrategia de rate limiting para auth y endpoints costosos.
2. Automatizar la exportacion de OpenAPI desde Swagger para tener contrato versionado.
3. Revisar si algunos headers adicionales deben endurecer respuestas HTML o navegadas en el futuro.
4. Mantener obligatoria la validacion de tenant y permisos dentro de cada caso de uso nuevo.
5. Evaluar verificacion de contrasenas comprometidas o blocklists administradas para auth local.
6. Introducir auditoria persistente no tenant-scoped para operaciones globales de plataforma como `CommercialPlan`.
