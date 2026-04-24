# Analisis actual de seguridad

## 1. Resumen ejecutivo

Esta reevaluacion profunda se actualizo el **18 de abril de 2026** sobre el codigo, configuracion, pruebas y documentacion versionada del repositorio.

Base verificada durante la auditoria:

- `dotnet build src/CLARIHR.Api/CLARIHR.Api.csproj --no-restore`: `0 warnings`, `0 errors`
- `dotnet build src/CLARIHR.Backoffice.Api/CLARIHR.Backoffice.Api.csproj --no-restore`: `0 warnings`, `0 errors`
- `dotnet build src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj --no-restore`: `0 warnings`, `0 errors`
- `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --no-restore`: `273/273` pruebas aprobadas
- `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter CompanySubscriptionStateManagementTests -v minimal`: `5/5` pruebas dirigidas aprobadas
- `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter BackofficeCompanySubscriptionsIntegrationTests -v minimal`: `6/6` pruebas dirigidas aprobadas
- `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter InternalCatalogAdministrationTests -v minimal`: `7/7` pruebas dirigidas aprobadas
- `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter InternalCatalogsIntegrationTests -v minimal`: `5/5` pruebas dirigidas aprobadas
- superficie HTTP medida en el repositorio: `37` controllers y `332` acciones HTTP
- contrato machine-readable versionado en `openapi.yaml`: `41` paths
- el suite completo aun conserva fallas legacy no relacionadas fuera del alcance de esta remediacion

La conclusion revisada es que la solucion **todavia requiere remediaciones antes de aprobar produccion** en un SaaS multi-tenant de RRHH, pero el hardening del 18 de abril cerro dos riesgos estructurales: el filtro tenant global ya es `fail-closed` y la auditoria ya redacta PII sensible en payloads serializados. Siguen vigentes hallazgos de riesgo alto con evidencia verificable:

- ausencia visible de controles anti abuso para auth, salvo el hardening puntual aplicado al request de `password-reset`
- confianza excesiva en `X-Forwarded-*` si el entorno no la cierra externamente
- drift contractual y documental severo entre codigo, OpenAPI y analisis vivos
- riesgo residual de auditoria por payloads completos, aunque ahora con PII redactada
- necesidad de mantener gobernanza estricta sobre usos intencionales de `IgnoreQueryFilters()`

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
- Estado actual: `Remediado y extendido el 30 de marzo de 2026`
- Nuevo respecto a la auditoria anterior: `Si`
- Cambio aplicado:
  el acceso global de plataforma ya no nace desde registro o login del core, ya no depende de `PlatformAdminEmails` y ya no usa el claim `platform_admin`.
- Evidencia:
  `CLARIHR.Api/Program.cs` exige `client_type=core` en la Core API.
  `CLARIHR.Backoffice.Api/Program.cs` exige `client_type=platform` en el backoffice y valida una audiencia separada.
  `JwtTokenService.cs` ahora emite tokens `core` y `platform` distintos, sin `tid` en los tokens de plataforma.
  `PlatformOperator` persiste el acceso global de plataforma en base de datos y `PlatformAuthAdministration.cs` lo exige para login y refresh del backoffice.
  La superficie owner de suscripciones tambien reutiliza `PlatformOperator` para ocultar y bloquear el plan interno `MASTER`; un token `core` de owner por si solo ya no puede ver ni seleccionar ese plan reservado para CLARI.
  `PlatformAuthenticationIntegrationTests` valida que el login core nunca emite privilegios de plataforma, que un usuario sin `PlatformOperator` recibe `PLATFORM_ACCESS_FORBIDDEN`, y que los tokens `core` y `platform` no cruzan APIs.
- Residual operativo:
  el bootstrap inicial del primer operador ahora depende del comando `bootstrap-platform-operator`; ese paso deja de ser una brecha de runtime, pero requiere control operativo y trazabilidad de quien lo ejecuta.

### 3.2 Auditoria redacta PII sensible de RRHH en payloads serializados

- Severidad: `Critico`
- Estado: `Remediado parcialmente el 18 de abril de 2026`
- Nuevo respecto a la auditoria anterior: `No`
- Cambio aplicado:
  `AuditSanitizer` elimina secretos y tokens como antes, y ahora redacta PII de RRHH con `"[REDACTED]"` para campos como emails, telefonos, direcciones, fechas de nacimiento, identificaciones, cuentas bancarias, `customDataJson` y `fileData`.
- Precondiciones:
  aplica a eventos nuevos que pasen por `AuditService` o `PlatformAuditService`; los logs historicos no se migran.
- Evidencia:
  `AuditSanitizer` mantiene listas separadas para secretos removidos y PII redactada.
  `AuditAdministrationTests` valida que emails, telefonos, fecha de nacimiento, direccion, nombre y cuenta bancaria ya no aparezcan en el JSON auditado, mientras los secretos se eliminan.
- Cobertura actual:
  la cobertura ya prueba redaccion de PII y remocion de secretos, pero no reemplaza una futura minimizacion de payloads por evento.
- Residual:
  el sistema aun audita `Before`, `After` y `Diff` completos en varios flujos; ahora se almacenan con PII redactada, pero la recomendacion de mediano plazo sigue siendo minimizar el payload en origen.

### 3.3 Auth con hardening parcial de anti abuso, aun insuficiente

- Severidad: `Importante`
- Estado: `Confirmado`
- Nuevo respecto a la auditoria anterior: `No`
- Impacto real:
  eleva el riesgo de credential stuffing, password spraying, abuso de refresh tokens y automatizacion agresiva de registro, login o activacion de invitaciones.
- Precondiciones:
  ninguna adicional; basta con exponer la API a trafico no confiable.
- Evidencia:
  `Program.cs` ya registra `AddRateLimiter` y `UseRateLimiter`, pero solo para la policy `auth-password-reset-request`.
  `AuthController` aplica `EnableRateLimiting("auth-password-reset-request")` a `POST /api/auth/password-reset/request`.
  `PasswordResetAdministration.cs` agrega respuesta uniforme para `request`, token hash persistido, un solo uso, expiracion corta, cooldown por cuenta y revocacion de sesiones al redimir.
  `register`, `external`, `login`, `refresh` y `company-user-invitations/accept` siguen sin throttling o lockout visible.
- Cobertura actual:
  no se observa cobertura automatica para rate limiting, lockout, retrasos progresivos ni abuso de `refresh`.
- Por que el hallazgo es valido:
  en una superficie de auth expuesta a internet, la ausencia de controles de anti abuso es una brecha operativa real aunque el flujo funcional y los tests actuales pasen.
- Remediacion concreta:
  extender rate limiting por IP e identidad a `register`, `login`, `refresh` y `company-user-invitations/accept`, agregar lockout o enfriamiento para credenciales invalidas y cubrir esas rutas con pruebas de integracion.

### 3.4 Filtro tenant global en modo `fail-closed`

- Severidad: `Importante`
- Estado: `Remediado el 18 de abril de 2026`
- Nuevo respecto a la auditoria anterior: `No`
- Cambio aplicado:
  el filtro global de entidades `ITenantScopedEntity` ahora exige tenant activo y coincidencia de `TenantId`. Si no hay tenant scope, el filtro no debe devolver filas tenant-scoped.
- Evidencia:
  `ApplicationDbContext` usa una expresion equivalente a `HasTenantScope && entity.TenantId == CurrentTenantIdOrDefault`.
  los usos restantes de `IgnoreQueryFilters()` en `Infrastructure` tienen comentario `Intentional tenant filter bypass:` y quedan cubiertos por una prueba de gobernanza.
- Cobertura actual:
  `ApplicationDbContextTenantFilterTests` valida la forma fail-closed del filtro y que escrituras tenant-scoped sin tenant siguen fallando. `IgnoreQueryFiltersGovernanceTests` falla si aparece un bypass no documentado.
- Residual:
  `IgnoreQueryFilters()` sigue existiendo para tenant mismatch, unicidad o grants IAM con tenant explicito. Nuevos usos deben justificar el bypass y preferir filtros `TenantId == ...` antes de materializar datos.

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

### 3.6 Auditoria durable para writes globales de plataforma y catalogos internos globales

- Severidad previa: `Importante`
- Estado actual: `Remediado y extendido el 2 de abril de 2026`
- Nuevo respecto a la auditoria anterior: `Si`
- Cambio aplicado:
  las mutaciones globales de `CommercialAddon`, `CommercialPlan`, el reemplazo de suscripciones empresariales, los cambios manuales o automaticos de estado de suscripcion y ahora tambien los valores aceptados de `InternalCatalogValue` ya no dependen solo de logging operativo; ahora escriben `PlatformAuditLog` persistente y no tenant-scoped. El catalogo de `CommercialAddon` sigue protegido por rol aun despues de generalizar add-ons `Massive` y `Specialized`, los cambios sensibles de estado de suscripcion quedaron restringidos a `PlatformOperatorRole.Admin`, y la nueva superficie de catalogos internos exige usuario autenticado valido aunque no requiera tenant activo.
- Evidencia:
  `PlatformAuditLog` y su configuracion EF existen en el modelo persistente.
  `PlatformAuditService` registra eventos globales con actor, entidad, accion y payload serializado.
  `CommercialAddonAdministration.cs`, `CommercialPlanAdministration.cs`, `PlatformSubscriptionAdministration.cs`, `CompanySubscriptionLifecycleProcessor.cs`, `InternalCatalogAdministration.cs` y `JobProfileAdministration.cs` invocan esa auditoria persistente en writes globales y transiciones automaticas.
  la migracion `AddPlatformBackofficeAndFormalSubscriptionPlans` crea la tabla `platform_audit_logs`.
- Cobertura actual:
  `BackofficeCompanySubscriptionsIntegrationTests` ya cubre suspension, reactivacion inmediata, preview de reactivacion, reactivacion programada, conflicto por duplicado pendiente, consulta del historial de estados y expiracion automatica de suscripciones empresariales, ademas de fallar si el flujo global de suscripciones pierde trazabilidad o enforcement de rol. `BackofficeCommercialAddonsIntegrationTests` ya verifica auditoria durable para create/update/activate/inactivate del catalogo global de add-ons, incluyendo configuraciones especializadas por seat o volumen. `InternalCatalogsIntegrationTests` ya cubre el acceso autenticado sin tenant, la visibilidad cross-tenant, el conflicto `409` por similitud `>= 0.90` y el autopoblado global desde `JobProfiles`. El CRUD de `CommercialPlan` sigue usando la misma infraestructura, aunque aun conviene agregar una prueba especifica por endpoint.

### 3.7 Drift contractual y documental severo

- Severidad: `Importante`
- Estado: `Confirmado`
- Nuevo respecto a la auditoria anterior: `Si`
- Impacto real:
  degrada gobernanza de cambios, confianza en la documentacion viva, pruebas de contrato y evaluacion de riesgos; tambien puede ocultar superficie real expuesta.
- Precondiciones:
  ninguna.
- Evidencia:
  la superficie real del repo mide `332` acciones HTTP, mientras que [openapi.yaml](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api/openapi.yaml) versiona solo `23` paths explicitos del contrato publico.
  [endpoint-reference.md](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api/endpoint-reference.md) reconoce que el contrato exhaustivo sigue solo en Swagger runtime.
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
- aprovisionamiento explicito de usuarios desde `PersonnelFiles` con rol tenant-scoped derivado de la plaza y activacion diferida por invitacion
- provisioning owner con rol `Admin de Empresa` resincronizado contra el catalogo default y todos los permisos tenant-scoped existentes, reduciendo drift de RBAC cuando se agregan permisos nuevos
- tenant mismatch en auditoria
- CRUD global de `CommercialPlan` y `CommercialAddon` sin tenant
- login de backoffice bloqueado sin `PlatformOperator`
- auditoria durable en reemplazo, suspension, reactivacion, cancelacion y expiracion automatica de suscripcion empresarial, ademas del CRUD de `CommercialAddon`

Huecos de prueba que aumentan riesgo real:

- no hay pruebas de rate limiting, lockout o throttling de auth, incluyendo `company-user-invitations/accept`
- no hay pruebas de spoofing o confianza de `X-Forwarded-*`
- no hay pruebas de auditoria durable para cada mutacion del CRUD de `CommercialPlan`
- no hay snapshots o validacion automatica del contrato OpenAPI versionado

## 7. Recomendaciones inmediatas

1. Continuar la minimizacion de auditoria para que cada evento persista solo metadata y diff minimo necesario.
2. Incorporar rate limiting y controles anti abuso en `register`, `login`, `refresh` y `external`.
3. Mantener la prueba de gobernanza de `IgnoreQueryFilters()` y revisar cualquier bypass nuevo en code review.
4. Cerrar la confianza de `X-Forwarded-*` con proxies o redes explicitas por entorno.
5. Extender pruebas de auditoria durable al CRUD global restante, especialmente `CommercialPlan`.
6. Automatizar el contrato OpenAPI y usarlo para frenar drift documental.

## 8. Veredicto

La solucion mantiene una base tecnica capaz y una cobertura automatica mejor de lo habitual, pero el nivel de riesgo acumulado sigue siendo demasiado alto para aprobar salida a produccion en un contexto de RRHH multi-tenant. Los hallazgos criticos no son cosmeticos: tocan privilegios globales, confidencialidad de PII y resistencia operativa ante abuso.
