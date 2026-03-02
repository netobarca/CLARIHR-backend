# Architecture Analysis

## Context

- Delivery baseline: current state up to HU-008
- Date: 2026-03-01
- Validation basis: codigo fuente, documentacion tecnica, build y tests actuales

## Architectural Compliance

### 1. Layered boundaries

Estado: compliant

Evidencia:

- La solucion mantiene separacion entre `src/CLARIHR.Domain`, `src/CLARIHR.Application`, `src/CLARIHR.Infrastructure` y `src/CLARIHR.Api`.
- La API compone middleware, auth y controllers, pero no implementa logica de negocio principal en controladores.
- Los handlers y contratos viven en Application y los repositorios/servicios concretos viven en Infrastructure.

Conclusion:

- La intencion de Clean Architecture definida al inicio sigue vigente y visible en la estructura del codigo.

### 2. CQRS and application orchestration

Estado: compliant

Evidencia:

- `src/CLARIHR.Application/Common/CQRS/RequestDispatcher.cs` centraliza el despacho de commands y queries.
- El dispatcher ejecuta validaciones antes de resolver handlers, manteniendo un punto comun de enforcement.
- Los endpoints principales llaman al dispatcher o a servicios de aplicacion bien delimitados en lugar de acoplarse directo a EF.

Conclusion:

- El sistema mantiene una separacion clara entre queries, commands y transporte HTTP.

### 3. Cross-cutting concerns

Estado: compliant

Evidencia:

- `src/CLARIHR.Api/Common/ProblemDetailsFactory.cs` unifica el contrato de errores.
- `src/CLARIHR.Api/Middleware/UnhandledExceptionMiddleware.cs` evita manejo ad hoc de excepciones.
- `src/CLARIHR.Infrastructure/Persistence/ApplicationDbContext.cs` centraliza tenant filters y metadata de auditoria.
- `src/CLARIHR.Api/Authorization/AuthorizeResourceFilter.cs` aplica enforcement reutilizable a nivel endpoint.

Conclusion:

- Los concerns transversales importantes no quedaron duplicados por feature.

### 4. Multi-tenant architecture

Estado: compliant

Evidencia:

- `ApplicationDbContext` aplica query filters a toda entidad `ITenantScopedEntity`.
- Los writes tenant-scoped se normalizan en `SaveChangesAsync`.
- El backend combina aislamiento automatico con validaciones explicitas de tenant mismatch en servicios/autorizacion.

Conclusion:

- El tenant context no es un detalle opcional; es parte de la infraestructura base.

### 5. RBAC architecture

Estado: compliant for current scope

Evidencia:

- `AuthorizeResourceFilter` resuelve L1/L2 a nivel endpoint via `IRbacAuthorizationService`.
- `RbacAuthorizationService` y `FieldAccessProfileService` soportan L3 por campo con perfil de acceso efectivo.
- `FieldPermissionService` administra catalogo, overrides, auditoria e invalidacion de cache.

Conclusion:

- El RBAC nivel 1, 2 y 3 existe ya como backend concern reusable y no como logica local de UI.

## Positive Architectural Decisions

- Uso de `Result` y `ProblemDetails` para evitar control de flujo basado en excepciones de negocio.
- Aplicacion de configuraciones EF por assembly, manteniendo el modelo persistente aislado.
- Introduccion de `IFieldAccessProfileService` para romper la dependencia circular detectada en DI.
- Auditoria y tenant context resueltos como servicios transversales, no como implementaciones dispersas.
- Particion del modulo de `CompanyUsers` por caso de uso y de `FieldPermissionService` por concern, reduciendo hotspots sin cambiar contratos.

## Tradeoffs And Technical Debt

### 1. Residual service concentration after decomposition

Observacion:

- `src/CLARIHR.Application/Features/CompanyUsers/CompanyUserManagement.cs` ya quedo reducido a contratos y ahora comparte carpeta con handlers/validators/helpers especializados.
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService.cs` ahora esta dividido entre `FieldPermissionService.Read.cs`, `FieldPermissionService.Write.cs` y `FieldPermissionService.Support.cs`.

Impacto:

- La deuda estructural principal bajo materialmente.
- Aun existe una concentracion moderada de conocimiento en helpers y servicios RBAC, pero ya es mas facil de mantener y extender.

Recomendacion:

- Mantener el mismo criterio de particion en HUs futuras y evitar volver a crecer archivos "catch-all".

### 2. Custom dispatcher without richer pipeline behaviors

Observacion:

- El dispatcher actual es suficiente para validacion y resolucion, pero no incorpora behaviors formales para auditoria, autorizacion o tracing.

Impacto:

- Funciona para el alcance actual, pero dificulta escalar cross-cutting concerns de manera uniforme si se agregan muchos modulos.

Recomendacion:

- Evaluar behaviors o decoradores para autorizacion, auditoria y telemetria cuando crezca el volumen de casos de uso.

### 3. Service placement

Observacion:

- Parte de la logica de autorizacion y RBAC vive en servicios de Infrastructure, lo cual es razonable por depender de persistencia y runtime context.

Impacto:

- No rompe la arquitectura, pero exige disciplina para que la logica de negocio pura no migre indebidamente a Infrastructure.

## Final Assessment

Veredicto:

- La arquitectura actual cumple bien con lo requerido desde el inicio y mantiene un diseño defendible para la etapa actual del producto.
- La deuda identificada es manejable y no invalida la base construida.
- El siguiente salto de calidad arquitectonica no es rediseñar, sino modularizar hotspots y endurecer pipelines de integracion.
