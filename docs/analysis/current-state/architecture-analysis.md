# Analisis actual de arquitectura

## 1. Proposito

Este documento describe el estado actual de la arquitectura del backend CLARIHR a partir del codigo vigente del repositorio.

## 2. Resumen ejecutivo

El backend implementa una arquitectura limpia con cuatro capas principales:

- `CLARIHR.Domain`
- `CLARIHR.Application`
- `CLARIHR.Infrastructure`
- `CLARIHR.Api`

La aplicacion usa CQRS con dispatchers propios, EF Core sobre PostgreSQL, autenticacion JWT, RBAC, auditoria y diseno tenant-scoped por defecto.

## 3. Estructura actual del repositorio

### 3.1 Domain

Contiene agregados, entidades y reglas de negocio puras por modulo:

- Companies
- IdentityAccess
- Locations
- OrgUnits
- JobProfiles
- PositionSlots
- PersonnelFiles
- SalaryTabulator
- CompetencyFramework
- LegalRepresentatives

### 3.2 Application

Contiene:

- commands y queries
- handlers
- validadores FluentValidation
- contratos de respuesta
- servicios de autorizacion abstractos
- dispatchers CQRS

El registro de handlers y validadores es automatico por ensamblado en [DependencyInjection.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/DependencyInjection.cs#L9).

### 3.3 Infrastructure

Contiene:

- implementaciones de repositorio
- `ApplicationDbContext`
- servicios de autenticacion
- resolucion de tenant y usuario actual
- auditoria
- politicas RBAC y de acciones permitidas
- caching de permisos por campo

El wiring principal esta en [DependencyInjection.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/DependencyInjection.cs#L1).

### 3.4 API

Contiene:

- `34` controladores
- `308` endpoints HTTP
- middleware de correlacion, logging, security headers y manejo global de excepciones
- configuracion de Swagger solo para `Development`

## 4. Flujo tecnico de una request

1. La request entra por ASP.NET Core Web API.
2. Los middleware aplican correlacion, logging, headers y manejo de excepciones.
3. El controller traduce request HTTP a command o query.
4. `ICommandDispatcher` o `IQueryDispatcher` resuelven el handler correspondiente.
5. FluentValidation valida la entrada.
6. El handler aplica autorizacion, tenant scope, reglas y persistencia.
7. Infrastructure ejecuta EF Core, auditoria y politicas auxiliares.
8. API convierte `Result` a `ProblemDetails` o a la respuesta DTO.

## 5. Modularidad funcional actual

La API esta organizada por modulos funcionales grandes:

- acceso y companias de cuenta
- IAM y RBAC
- locations y work centers
- org units y cost centers
- job profiles, competency framework y position slots
- personnel files
- salary tabulator
- audit y report capabilities

Dentro de `PersonnelFiles`, la modularidad mejoro con la separacion por capacidad:

- core
- profile
- employment
- compensation
- talent
- documents
- administration
- reporting

## 6. Decisiones arquitectonicas visibles en codigo

### 6.1 CQRS explicito

Las operaciones de lectura y escritura se separan por tipo, contratos y handlers. Esto simplifica validacion, autorizacion y testing por caso de uso.

### 6.2 Tenant-scoped by default

La mayoria de handlers leen `ITenantContext` o reciben `companyId` y luego validan coincidencia de tenant antes de leer o mutar datos.

### 6.2A Catalogos globales fuera de tenant

El agregado `CommercialPlan` vive dentro del dominio `Companies`, pero no es `TenantEntity`. Se administra por CQRS dedicado y expone endpoints `/api/account/commercial-plans` protegidos por rol global `platform_admin`, sin depender de `tenantId`.

### 6.3 Autorizacion por servicio de dominio aplicativo

Cada modulo relevante tiene su propio `AuthorizationService`, por ejemplo para personnel files, org units, locations o salary tabulator. Esto evita dejar la seguridad solo en atributos HTTP.

### 6.4 Auditoria transversal

Los cambios importantes registran auditoria con before/after, usuario actor, tenant y metadatos de request.

### 6.5 Controllers delgados

Los controllers se limitan a mapping HTTP y despacho. La logica funcional permanece en Application y Domain.

### 6.6 Compatibilidad transicional de suscripciones

El nuevo catalogo comercial convive con referencias string existentes en `CompanySubscription` y `PlanEntitlement` mediante `planCode`. El sistema siembra y protege `FREE` para mantener coherencia funcional mientras la relacion formal entre suscripciones y catalogo comercial se implementa en historias futuras.

## 7. Fortalezas actuales

- separacion clara de capas
- CQRS consistente en modulos principales
- autorizacion y tenant scope aplicados dentro del caso de uso
- validacion centralizada con FluentValidation
- auditoria amplia en operaciones sensibles
- creciente modularidad de controllers en dominios grandes
- runtime API documentable con Swagger

## 8. Riesgos y tensiones actuales

- hay modulos con mucha superficie HTTP y contratos extensos; el costo de documentacion y mantenimiento es alto
- algunos reportes y exportes se generan en request path y en memoria
- el contrato OpenAPI versionado no esta automatizado en el repositorio
- la estrategia documental iba retrasada respecto al crecimiento del codigo
- existen muchos endpoints administrativos, lo que eleva el costo de gobernanza de permisos y pruebas

## 9. Conclusiones

La base arquitectonica actual es coherente con el foundation document y ya muestra decisiones maduras en seguridad, multi-tenant y separacion de responsabilidades. La prioridad no es redisenar la arquitectura, sino fortalecer:

- automatizacion documental
- gobernanza de contratos API
- observabilidad operativa
- manejo de procesos pesados fuera del request path cuando el volumen lo exija
