# Analisis actual de seguridad

## 1. Resumen ejecutivo

Esta reevaluacion profunda se ejecuto el **28 de marzo de 2026** sobre el codigo, configuracion, pruebas y documentacion versionada del repositorio.

Base verificada durante la auditoria:

- `dotnet build CLARIHR.slnx -v minimal`: `0 warnings`, `0 errors`
- `dotnet test CLARIHR.slnx -v minimal`: `322/322` pruebas aprobadas
- superficie HTTP medida en el repositorio: `34` controllers y `324` acciones HTTP
- contrato machine-readable versionado en `openapi.yaml`: `8` paths

La conclusion revisada es que la solucion **no esta aprobada para produccion** en su estado actual para un SaaS multi-tenant de RRHH. La base tecnica sigue siendo buena, pero la reevaluacion confirmo hallazgos criticos y sumo hallazgos nuevos con evidencia verificable:

- escalacion a `platform_admin` por registro local anonimo cuando el correo coincide con la allow-list
- persistencia y reexposicion de PII sensible de RRHH en auditoria
- ausencia visible de controles anti abuso para auth
- filtro tenant global en modo `fail-open`
- confianza excesiva en `X-Forwarded-*` si el entorno no la cierra externamente
- writes globales de plataforma sin `AuditLog` durable
- drift contractual y documental severo entre codigo, OpenAPI y analisis vivos

## 2. Criterio y alcance

El alcance de esta auditoria fue exclusivamente el repositorio:

- codigo fuente versionado
- tests versionados
- configuracion versionada
- documentacion viva versionada

No se asumieron protecciones externas de WAF, API gateway, reverse proxy, SIEM o procesos operativos si no estaban representados en el repo.

## 3. Hallazgos confirmados de la auditoria revisada

### 3.1 Escalacion a `platform_admin` por registro local anonimo

- Severidad: `Critico`
- Estado: `Confirmado`
- Nuevo respecto a la auditoria anterior: `Si`
- Impacto real:
  un actor que conozca o adivine un correo incluido en `Authentication:Jwt:PlatformAdminEmails` puede autoaprovisionarse un usuario local y recibir privilegios globales de plataforma, sin tenant y sin prueba previa de propiedad del correo.
- Precondiciones:
  debe existir al menos un correo en la allow-list y no debe existir una validacion externa de email ownership fuera del repo.
- Evidencia:
  [AuthController.cs#L18](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/AuthController.cs#L18) expone `POST /api/auth/register` como `[AllowAnonymous]`.
  [RegisterUserCommand.cs#L76](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/Auth/RegisterUser/RegisterUserCommand.cs#L76) crea un usuario local activo y emite tokens inmediatamente.
  [JwtTokenService.cs#L208](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auth/JwtTokenService.cs#L208) agrega el claim `platform_admin` solo por coincidencia de correo normalizado.
  [JwtTokenService.cs#L229](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auth/JwtTokenService.cs#L229) no exige verificacion previa del correo ni una fuente de identidad distinta del registro local.
  [PlatformAdminAuthenticationIntegrationTests.cs#L22](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/tests/CLARIHR.Api.IntegrationTests/PlatformAdminAuthenticationIntegrationTests.cs#L22) demuestra el flujo completo: registrar `dev@clarihr.local`, hacer login y recibir el claim `platform_admin`.
- Cobertura actual:
  existe una prueba positiva que valida la emision del claim, pero no existe una prueba negativa que bloquee auto-registro de correos allow-listed ni una prueba que exija verificacion de ownership.
- Por que el hallazgo es valido:
  no es una preferencia de diseno. El privilegio global depende de una cadena observable y suficiente: ruta anonima, creacion local inmediata, y grant por email allow-list. En un sistema de RRHH esto equivale a riesgo de administracion global.
- Remediacion concreta:
  quitar los correos allow-listed del flujo de registro anonimo, exigir email verificado o un IdP externo para `platform_admin`, y agregar pruebas negativas que impidan elevacion por self-registration.

### 3.2 Auditoria persiste y reexpone PII sensible de RRHH

- Severidad: `Critico`
- Estado: `Confirmado`
- Nuevo respecto a la auditoria anterior: `No`
- Impacto real:
  el sistema puede persistir y luego devolver desde auditoria datos personales, financieros y laborales que superan ampliamente un diff minimo justificable para RRHH.
- Precondiciones:
  basta con ejecutar flujos normales de expedientes o de otras secciones que auditan payloads completos.
- Evidencia:
  [AuditSanitizer.cs#L11](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auditing/AuditSanitizer.cs#L11) elimina solo nombres de propiedad orientados a secretos y tokens.
  [AuditSanitizer.cs#L64](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auditing/AuditSanitizer.cs#L64) conserva cualquier propiedad cuyo nombre no caiga en esa lista.
  [PersonnelFileAdministration.cs#L199](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs#L199) define `PersonnelFileResponse` con `PersonalEmail`, `InstitutionalEmail`, telefonos, `CustomDataJson`, identificaciones, contactos, familiares, cuentas bancarias, referencias y documentos.
  [PersonnelFileAdministration.cs#L92](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs#L92) incluye `AccountNumber` en cuentas bancarias.
  [PersonnelFileAdministration.cs#L62](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs#L62) incluye datos amplios de familiares, incluyendo `Salary`.
  [PersonnelFileAdministration.cs#L1992](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs#L1992) audita `After: response` completo al crear expediente.
  [PersonnelFileEmployeeAdministration.cs#L958](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileEmployeeAdministration.cs#L958) centraliza updates de empleado auditando un `after` arbitrario.
  [AuditService.cs#L56](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auditing/AuditService.cs#L56) persiste `Before`, `After` y `Diff` sanitizados, no minimizados.
  [AuditLogAdministration.cs#L26](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/Audit/AuditLogAdministration.cs#L26) y [AuditLogAdministration.cs#L170](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/Audit/AuditLogAdministration.cs#L170) devuelven `Before`, `After` y `Diff` crudos en el detalle.
  [AuditAdministrationTests.cs#L18](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/tests/CLARIHR.Application.UnitTests/AuditAdministrationTests.cs#L18) prueba explicitamente que `Email` se conserva en el JSON auditado.
- Cobertura actual:
  hay cobertura para remocion de `PasswordHash`, `RefreshTokens` y `RawToken`, pero no para redaccion de PII laboral, salarios, beneficiarios, cuentas bancarias ni `customData`.
- Por que el hallazgo es valido:
  el problema no es "que exista auditoria", sino que se almacena demasiada informacion en payloads completos y luego se vuelve a exponer por un endpoint de detalle. Para RRHH esto es un riesgo de confidencialidad y cumplimiento, no una cuestion de estilo.
- Remediacion concreta:
  pasar de auditoria por DTO completo a auditoria por metadata y diff minimo permitido, bloquear o enmascarar `Before/After` sensibles, y crear pruebas de redaccion para tipos de datos reales de RRHH.

### 3.3 Auth sin controles visibles de anti abuso

- Severidad: `Importante`
- Estado: `Confirmado`
- Nuevo respecto a la auditoria anterior: `No`
- Impacto real:
  eleva el riesgo de credential stuffing, password spraying, abuso de refresh tokens y automatizacion agresiva de registro/login.
- Precondiciones:
  ninguna adicional; basta con exponer la API a trafico no confiable.
- Evidencia:
  [Program.cs#L66](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Program.cs#L66) a [Program.cs#L105](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Program.cs#L105) no muestran `AddRateLimiter`, `UseRateLimiter` ni middleware equivalente.
  [AuthController.cs#L18](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/AuthController.cs#L18), [AuthController.cs#L45](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/AuthController.cs#L45), [AuthController.cs#L76](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/AuthController.cs#L76) y [AuthController.cs#L95](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/AuthController.cs#L95) exponen `register`, `external`, `login` y `refresh` como anonimos.
  [LoginCommand.cs#L34](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/Auth/Login/LoginCommand.cs#L34) falla por credenciales invalidas sin contador, enfriamiento o lockout.
- Cobertura actual:
  no se observa cobertura automatica para rate limiting, lockout, retrasos progresivos ni abuso de `refresh`.
- Por que el hallazgo es valido:
  en una superficie de auth expuesta a internet, la ausencia de controles de anti abuso es una brecha operativa real aunque el flujo funcional y los tests actuales pasen.
- Remediacion concreta:
  incorporar rate limiting por IP y por identidad, respuestas `429`, lockout o enfriamiento para credenciales invalidas, y pruebas de integracion sobre rutas de auth.

### 3.4 Filtro tenant global en modo `fail-open`

- Severidad: `Importante`
- Estado: `Confirmado` como riesgo estructural, `Pendiente de entorno` para un bypass HTTP universal
- Nuevo respecto a la auditoria anterior: `No`
- Impacto real:
  cualquier flujo tenant-scoped que se ejecute sin tenant activo puede terminar leyendo datos de todos los tenants si confia en el filtro global en lugar de aplicar restricciones propias.
- Precondiciones:
  ausencia o perdida de `tenant context` en un handler, job, refactor o ruta fuera del pipeline esperado.
- Evidencia:
  [ApplicationDbContext.cs#L287](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Persistence/ApplicationDbContext.cs#L287) define `HasQueryFilter(entity => !HasTenantScope || entity.TenantId == CurrentTenantIdOrDefault)`.
  el repositorio usa `IgnoreQueryFilters()` en multiples modulos, por lo que la disciplina de aislamiento depende tambien de revisiones puntuales y no solo del filtro global.
- Cobertura actual:
  existen pruebas de `tenant mismatch` en modulos y auditoria, pero no una prueba dedicada que verifique que el filtro global falle en cerrado cuando no hay tenant.
- Por que el hallazgo es valido:
  el riesgo existe en la propia expresion del filtro. Aunque no se haya demostrado un bypass HTTP generico en esta auditoria, el comportamiento del modelo es `fail-open` y eso debilita la garantia tenant-scoped by default.
- Remediacion concreta:
  cambiar el filtro global a `fail-closed`, crear una matriz documentada de usos de `IgnoreQueryFilters()` y cubrir con pruebas los casos legitimos de bypass.

### 3.5 Confianza excesiva en `X-Forwarded-*`

- Severidad: `Importante`
- Estado: `Condicional`
- Nuevo respecto a la auditoria anterior: `Si`
- Impacto real:
  si el despliegue no restringe proxies confiables fuera del repo, un cliente o proxy no confiable podria contaminar IP remota y esquema observado por la app, afectando logs, auditoria y decisiones dependientes del contexto de request.
- Precondiciones:
  exposicion detras de proxies no explicitamente cerrados en infraestructura.
- Evidencia:
  [Program.cs#L72](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Program.cs#L72) limpia `KnownIPNetworks` y `KnownProxies`.
  [Program.cs#L94](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Program.cs#L94) aplica `UseForwardedHeaders()`.
  [RequestLoggingMiddleware.cs#L28](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Middleware/RequestLoggingMiddleware.cs#L28) registra `RemoteIpAddress`.
  [AuditService.cs#L59](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auditing/AuditService.cs#L59) persiste la IP remota en auditoria.
  Microsoft endurecio este punto precisamente por seguridad en ASP.NET Core 8/9/10: <https://learn.microsoft.com/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies?view=aspnetcore-10.0>
- Cobertura actual:
  no existen pruebas de integracion ni unitarias para spoofing de `X-Forwarded-*` o para la lista de proxies confiables.
- Por que el hallazgo es valido:
  el repositorio deja la confianza abierta en forwarded headers y luego reutiliza esa informacion como dato de trazabilidad. La explotabilidad final depende del entorno, por eso se clasifica como `condicional`, pero el riesgo de configuracion es real.
- Remediacion concreta:
  definir proxies o redes confiables explicitas por entorno, no limpiar listas por defecto sin justificacion operativa y agregar pruebas o validaciones de configuracion.

### 3.6 Writes globales de plataforma sin `AuditLog` durable

- Severidad: `Importante`
- Estado: `Confirmado`
- Nuevo respecto a la auditoria anterior: `Si`
- Impacto real:
  mutaciones globales de plataforma quedan con logging operativo, pero sin before/after durable ni trazabilidad homogenea respecto al resto del sistema.
- Precondiciones:
  ejecutar operaciones sobre `CommercialPlan`.
- Evidencia:
  [AuditService.cs#L20](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auditing/AuditService.cs#L20) exige tenant para `LogAsync`.
  [CommercialPlanAdministration.cs#L101](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/CommercialPlans/CommercialPlanAdministration.cs#L101), [CommercialPlanAdministration.cs#L172](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/CommercialPlans/CommercialPlanAdministration.cs#L172), [CommercialPlanAdministration.cs#L228](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/CommercialPlans/CommercialPlanAdministration.cs#L228) y [CommercialPlanAdministration.cs#L289](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/CommercialPlans/CommercialPlanAdministration.cs#L289) usan `logger.LogInformation` para create/update/activate/inactivate, no `AuditService`.
  [CommercialPlansIntegrationTests.cs#L45](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/tests/CLARIHR.Api.IntegrationTests/CommercialPlansIntegrationTests.cs#L45) prueba que el CRUD global funciona sin tenant.
- Cobertura actual:
  hay cobertura funcional del CRUD global, pero no pruebas que exijan rastro durable en `AuditLog`.
- Por que el hallazgo es valido:
  en un backend que ya trata auditoria como control transversal, dejar fuera las mutaciones globales de plataforma rompe consistencia de trazabilidad y respuesta forense.
- Remediacion concreta:
  introducir auditoria persistente no tenant-scoped para writes globales o una variante de `AuditLog` de plataforma con before/after y actor.

### 3.7 Drift contractual y documental severo

- Severidad: `Importante`
- Estado: `Confirmado`
- Nuevo respecto a la auditoria anterior: `Si`
- Impacto real:
  degrada gobernanza de cambios, confianza en la documentacion viva, pruebas de contrato y evaluacion de riesgos; tambien puede ocultar superficie real expuesta.
- Precondiciones:
  ninguna.
- Evidencia:
  la superficie real del repo mide `324` acciones HTTP, mientras que [openapi.yaml#L1](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api/openapi.yaml#L1) y [openapi.yaml#L526](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api/openapi.yaml#L526) dejan visible un contrato bootstrap con `8` paths.
  [endpoint-reference.md#L2813](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api/endpoint-reference.md#L2813) reconoce que el contrato exhaustivo sigue solo en Swagger runtime.
  [testing-analysis.md](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/analysis/current-state/testing-analysis.md) reportaba `291` tests y `EnsureCreatedAsync()` antes de esta correccion documental, pero [IntegrationTestWebApplicationFactory.cs#L89](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/tests/CLARIHR.Api.IntegrationTests/IntegrationTestWebApplicationFactory.cs#L89) usa `MigrateAsync()`.
  [architecture-analysis.md](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/analysis/current-state/architecture-analysis.md) reportaba `308` endpoints y controllers delgados antes de esta correccion documental, pero el codigo actual ya contiene controllers con auditoria, `SaveChangesAsync` y generacion de archivos.
- Cobertura actual:
  no hay snapshots de contrato ni validacion automatica entre Swagger runtime y `openapi.yaml`.
- Por que el hallazgo es valido:
  el drift ya no es solo un pendiente documental; altera la confiabilidad del ecosistema de auditoria, testing y operacion porque algunas fuentes vivas contradicen el estado real del codigo.
- Remediacion concreta:
  automatizar export/versionado de OpenAPI, introducir pruebas de contrato y actualizar analisis vivos con base en el codigo real.

## 4. Hallazgos nuevos respecto a la auditoria anterior

Los hallazgos realmente nuevos que esta reevaluacion agrega sobre la auditoria anterior son:

1. autoelevacion a `platform_admin` por registro local anonimo combinado con email allow-list
2. confianza excesiva en `X-Forwarded-*` por limpieza de proxies y redes confiables
3. writes globales de `CommercialPlan` sin `AuditLog` durable
4. drift contractual y documental ya severo, no solo "pendiente"

## 5. Hallazgos de apoyo y limites de esta auditoria

- El problema de `tenant isolation` por filtro global esta confirmado como riesgo estructural, pero en esta reevaluacion no se demostro un bypass HTTP universal y repetible para todos los modulos.
- El subsistema de refresh tokens es mas fuerte de lo que parecia a primera vista:
  [JwtTokenService.cs#L69](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auth/JwtTokenService.cs#L69) persiste hashes y [JwtTokenService.cs#L111](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auth/JwtTokenService.cs#L111) aplica rotacion con deteccion de reuse.
- El uso extensivo de `IgnoreQueryFilters()` requiere una matriz de revision obligatoria, pero no todos los usos revisados en esta reevaluacion constituyen por si mismos una fuga confirmada.

## 6. Cobertura actual y huecos de prueba relevantes

Cobertura positiva visible:

- tokens y refresh rotation con reuse detection
- claim `platform_admin`
- tenant mismatch en auditoria
- CRUD global de `CommercialPlan` sin tenant

Huecos de prueba que aumentan riesgo real:

- no hay pruebas de redaccion de PII real de RRHH en auditoria
- no hay pruebas de rate limiting, lockout o throttling de auth
- no hay pruebas de spoofing o confianza de `X-Forwarded-*`
- no hay pruebas que exijan `AuditLog` durable para writes globales
- no hay snapshots o validacion automatica del contrato OpenAPI versionado

## 7. Recomendaciones inmediatas

1. Bloquear la autoelevacion a `platform_admin` desde registro local anonimo.
2. Redisenar la auditoria para que no persista ni exponga payloads completos de RRHH.
3. Incorporar rate limiting y controles anti abuso en `register`, `login`, `refresh` y `external`.
4. Volver el filtro tenant global a `fail-closed` y auditar todos los `IgnoreQueryFilters()`.
5. Cerrar la confianza de `X-Forwarded-*` con proxies o redes explicitas por entorno.
6. Introducir auditoria persistente para writes globales de plataforma.
7. Automatizar el contrato OpenAPI y usarlo para frenar drift documental.

## 8. Veredicto

La solucion mantiene una base tecnica capaz y una cobertura automatica mejor de lo habitual, pero el nivel de riesgo acumulado sigue siendo demasiado alto para aprobar salida a produccion en un contexto de RRHH multi-tenant. Los hallazgos criticos no son cosmeticos: tocan privilegios globales, confidencialidad de PII y resistencia operativa ante abuso.
