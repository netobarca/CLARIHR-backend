# Analisis actual de seguridad

## 1. Resumen ejecutivo

Esta reevaluacion profunda se actualizo el **29 de marzo de 2026** sobre el codigo, configuracion, pruebas y documentacion versionada del repositorio.

Base verificada durante la auditoria:

- `dotnet build CLARIHR.slnx -v minimal`: `0 warnings`, `0 errors`
- `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter "...auth/plataforma/suscripciones..."`: `43/43` pruebas dirigidas aprobadas
- `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter "...Backoffice..."`: `9/9` pruebas dirigidas aprobadas
- superficie HTTP medida en el repositorio: `36` controllers y `326` acciones HTTP
- contrato machine-readable versionado en `openapi.yaml`: `15` paths
- el suite completo aun conserva fallas legacy no relacionadas fuera del alcance de esta remediacion

La conclusion revisada es que la solucion **no esta aprobada para produccion** en su estado actual para un SaaS multi-tenant de RRHH. La base tecnica sigue siendo buena, y la actualizacion del 29 de marzo ya cerro la brecha critica de acceso global por email allow-list y tambien agrego auditoria durable de plataforma. Aun asi, siguen vigentes hallazgos de riesgo alto con evidencia verificable:

- persistencia y reexposicion de PII sensible de RRHH en auditoria
- ausencia visible de controles anti abuso para auth
- filtro tenant global en modo `fail-open`
- confianza excesiva en `X-Forwarded-*` si el entorno no la cierra externamente
- drift contractual y documental severo entre codigo, OpenAPI y analisis vivos

## 2. Criterio y alcance

El alcance de esta auditoria fue exclusivamente el repositorio:

- codigo fuente versionado
- tests versionados
- configuracion versionada
- documentacion viva versionada

No se asumieron protecciones externas de WAF, API gateway, reverse proxy, SIEM o procesos operativos si no estaban representados en el repo.

## 3. Hallazgos confirmados de la auditoria revisada

### 3.1 Separacion del acceso global de plataforma respecto al auth core

- Severidad previa: `Critico`
- Estado actual: `Remediado el 29 de marzo de 2026`
- Nuevo respecto a la auditoria anterior: `Si`
- Cambio aplicado:
  el acceso global de plataforma ya no nace desde registro o login del core, ya no depende de `PlatformAdminEmails` y ya no usa el claim `platform_admin`.
- Evidencia:
  `CLARIHR.Api/Program.cs` exige `client_type=core` en la Core API.
  `CLARIHR.Backoffice.Api/Program.cs` exige `client_type=platform` en el backoffice y valida una audiencia separada.
  `JwtTokenService.cs` ahora emite tokens `core` y `platform` distintos, sin `tid` en los tokens de plataforma.
  `PlatformOperator` persiste el acceso global de plataforma en base de datos y `PlatformAuthAdministration.cs` lo exige para login y refresh del backoffice.
  `PlatformAuthenticationIntegrationTests` valida que el login core nunca emite privilegios de plataforma, que un usuario sin `PlatformOperator` recibe `PLATFORM_ACCESS_FORBIDDEN`, y que los tokens `core` y `platform` no cruzan APIs.
- Residual operativo:
  el bootstrap inicial del primer operador ahora depende del comando `bootstrap-platform-operator`; ese paso deja de ser una brecha de runtime, pero requiere control operativo y trazabilidad de quien lo ejecuta.

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

### 3.6 Auditoria durable para writes globales de plataforma

- Severidad previa: `Importante`
- Estado actual: `Remediado el 29 de marzo de 2026`
- Nuevo respecto a la auditoria anterior: `Si`
- Cambio aplicado:
  las mutaciones globales de `CommercialPlan` y el reemplazo de suscripciones empresariales ya no dependen solo de logging operativo; ahora escriben `PlatformAuditLog` persistente y no tenant-scoped.
- Evidencia:
  `PlatformAuditLog` y su configuracion EF existen en el modelo persistente.
  `PlatformAuditService` registra eventos globales con actor, entidad, accion y payload serializado.
  `CommercialPlanAdministration.cs` y `PlatformSubscriptionAdministration.cs` invocan esa auditoria persistente en writes globales.
  la migracion `AddPlatformBackofficeAndFormalSubscriptionPlans` crea la tabla `platform_audit_logs`.
- Cobertura actual:
  `BackofficeCompanySubscriptionsIntegrationTests` falla si el reemplazo de suscripcion no deja rastro persistente. El CRUD de `CommercialPlan` ya usa la misma infraestructura, aunque aun conviene agregar una prueba especifica por endpoint.

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

Los hallazgos realmente nuevos que esta reevaluacion agrega y que siguen vigentes sobre la auditoria anterior son:

1. confianza excesiva en `X-Forwarded-*` por limpieza de proxies y redes confiables
2. drift contractual y documental ya severo, no solo "pendiente"

## 5. Hallazgos de apoyo y limites de esta auditoria

- El problema de `tenant isolation` por filtro global esta confirmado como riesgo estructural, pero en esta reevaluacion no se demostro un bypass HTTP universal y repetible para todos los modulos.
- El subsistema de refresh tokens es mas fuerte de lo que parecia a primera vista:
  [JwtTokenService.cs#L69](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auth/JwtTokenService.cs#L69) persiste hashes y [JwtTokenService.cs#L111](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Auth/JwtTokenService.cs#L111) aplica rotacion con deteccion de reuse.
- El uso extensivo de `IgnoreQueryFilters()` requiere una matriz de revision obligatoria, pero no todos los usos revisados en esta reevaluacion constituyen por si mismos una fuga confirmada.

## 6. Cobertura actual y huecos de prueba relevantes

Cobertura positiva visible:

- tokens y refresh rotation con reuse detection
- separacion de audiencias y `client_type` entre core y backoffice
- tenant mismatch en auditoria
- CRUD global de `CommercialPlan` sin tenant
- login de backoffice bloqueado sin `PlatformOperator`
- auditoria durable en reemplazo de suscripcion empresarial

Huecos de prueba que aumentan riesgo real:

- no hay pruebas de redaccion de PII real de RRHH en auditoria
- no hay pruebas de rate limiting, lockout o throttling de auth
- no hay pruebas de spoofing o confianza de `X-Forwarded-*`
- no hay pruebas de auditoria durable para cada mutacion del CRUD de `CommercialPlan`
- no hay snapshots o validacion automatica del contrato OpenAPI versionado

## 7. Recomendaciones inmediatas

1. Redisenar la auditoria para que no persista ni exponga payloads completos de RRHH.
2. Incorporar rate limiting y controles anti abuso en `register`, `login`, `refresh` y `external`.
3. Volver el filtro tenant global a `fail-closed` y auditar todos los `IgnoreQueryFilters()`.
4. Cerrar la confianza de `X-Forwarded-*` con proxies o redes explicitas por entorno.
5. Extender pruebas de auditoria durable a todo el CRUD de `CommercialPlan`.
6. Automatizar el contrato OpenAPI y usarlo para frenar drift documental.

## 8. Veredicto

La solucion mantiene una base tecnica capaz y una cobertura automatica mejor de lo habitual, pero el nivel de riesgo acumulado sigue siendo demasiado alto para aprobar salida a produccion en un contexto de RRHH multi-tenant. Los hallazgos criticos no son cosmeticos: tocan privilegios globales, confidencialidad de PII y resistencia operativa ante abuso.
