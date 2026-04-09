# Analisis actual de testing

## 1. Resumen ejecutivo

La estrategia de pruebas del backend es mejor de lo que reflejaba la version anterior de este documento. La validacion actualizada del **3 de abril de 2026** verifico:

- `5` unit tests dirigidos aprobados para manejo de estados de suscripcion empresarial
- `6` integration tests dirigidos aprobados para Platform Backoffice API de suscripciones empresariales
- `7` unit tests dirigidos aprobados para catalogos internos globales
- `5` integration tests dirigidos aprobados para catalogos internos globales y autopoblado desde `JobProfiles`
- `20` unit tests dirigidos aprobados para gobernanza comercial de planes y provisioning owner, incluyendo el guard de `MASTER`
- `9` integration tests dirigidos aprobados para seeds/migraciones comerciales y visibilidad owner/backoffice de `FREE` y `MASTER`
- `dotnet build` limpio con `0 warnings`
- el suite completo aun conserva fallas previas no relacionadas en normalizacion y algunos escenarios legacy de integracion

La base actual de testing es saludable para evolucion diaria del backend, pero sigue teniendo huecos justamente en las zonas de riesgo mas alto detectadas por la auditoria revisada.

## 2. Estado validado

### 2.1 Unit tests

Proyecto: `tests/CLARIHR.Application.UnitTests`

Cobertura visible sobre:

- auth local y externa
- auth separada `core` / `platform`
- refresh tokens
- provisionamiento
- dispatchers y dependency injection
- tenancy
- autorizacion RBAC
- autorizacion de plataforma por `PlatformOperator`
- permisos por campo
- personnel files
- salary tabulator
- org units, locations, cost centers
- job profiles, position slots y competency framework
- catalogo global `CommercialAddon`
- catalogo global `CommercialPlan`
- reemplazo de suscripciones empresariales
- ciclo de vida de estados de suscripcion empresarial, incluyendo suspension, reactivacion, cancelacion, expiracion y politica de capacidades por estado
- catalogos internos globales para requisitos de `JobProfiles`, incluyendo normalizacion, thresholds `0.70/0.90`, reuse exacto y reuse por similitud en uso

### 2.2 Integration tests

Proyecto: `tests/CLARIHR.Api.IntegrationTests`

El harness actual usa:

- `WebApplicationFactory<Program>`
- factories separadas para Core API y Backoffice API con JWT reales
- autenticacion de prueba via `TestAuthenticationHandler`
- base PostgreSQL efimera
- `EnsureDeletedAsync()` seguido de `MigrateAsync()`
- seeding controlado por `IntegrationTestSeeder`

Evidencia:

- [IntegrationTestWebApplicationFactory.cs#L89](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/tests/CLARIHR.Api.IntegrationTests/IntegrationTestWebApplicationFactory.cs#L89) aplica migraciones reales en tests

## 3. Fortalezas actuales

### 3.1 Auth y tokens mejor cubiertos de lo esperado

Hay cobertura para:

- emision del claim `client_type` para tokens `core` y `platform`
- rotacion de refresh token
- deteccion de reuse en la familia de refresh tokens
- rechazo cruzado de tokens entre Core API y Backoffice API

### 3.2 Tenant y permisos si tienen pruebas visibles

Hay pruebas de tenant mismatch y permisos en modulos sensibles, incluyendo auditoria y surface global de `CommercialPlan`.

### 3.3 El flujo global de plataforma si esta ejercitado

`BackofficeCommercialAddonsIntegrationTests`, `BackofficeCommercialPlansIntegrationTests`, `BackofficeCompanySubscriptionsIntegrationTests`, `AccountCompanySubscriptionsIntegrationTests` y `PlatformAuthenticationIntegrationTests` ejercitan el login del backoffice, el CRUD de add-ons y planes globales, el reemplazo de suscripciones empresariales, la separacion de audiencias `core/platform`, el rol `ReadOnly` y la auditoria durable de plataforma en writes globales. Desde HU-BILL-007 y HU-BILL-010, esa cobertura de suscripciones tambien valida suspension, preview de reactivacion, reactivacion inmediata o programada, `pendingStatusChange` en overview, conflicto por duplicado pendiente, consulta de historial de estados, expiracion automatica y bloqueo de cambios manuales para operadores `ReadOnly`. La remediacion de abril agrega coverage explicita para seeds/migraciones de `FREE` y `MASTER`, visibilidad owner filtrada por `PlatformOperator`, rechazo `403` del plan `MASTER` para owners normales y proteccion de `MASTER` contra recortes manuales de `moduleKeys`. Desde HU-BILL-004, la cobertura de add-ons incluye configuraciones `Massive` y `Specialized`, filtros por `type` y `billingModel`, y compatibilidad de lectura para filas masivas preexistentes.

### 3.4 Los catalogos internos globales ya tienen cobertura dirigida

`InternalCatalogAdministrationTests` valida normalizacion, mapping de tipos de requisito, thresholds `0.70` para busqueda y `0.90` para duplicados, ademas de reuse exacto o por similitud. `InternalCatalogsIntegrationTests` cubre acceso autenticado sin tenant, visibilidad global cross-tenant, `409` por casi duplicado, y el autopoblado del catalogo global cuando `JobProfiles` crea o actualiza requisitos `Education`, `Knowledge` o `Certification`, dejando fuera `Experience` y `Other`.

## 4. Hallazgos relevantes sobre cobertura

### 4.1 La documentacion previa de testing estaba desactualizada

La version anterior de este analisis ya no representaba el codigo real:

- reportaba un snapshot anterior de pruebas ya superado por la suite actual y por los nuevos tests del backoffice
- afirmaba `EnsureCreatedAsync()` como estrategia principal, pero el harness actual usa `MigrateAsync()`

Esto no empeora la calidad de las pruebas, pero si la confiabilidad de la documentacion viva.

### 4.2 Cobertura de auditoria insuficiente para PII real de RRHH

[AuditAdministrationTests.cs#L18](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/tests/CLARIHR.Application.UnitTests/AuditAdministrationTests.cs#L18) valida remocion de secretos y tokens, pero tambien confirma que `Email` permanece en el JSON auditado.

No hay pruebas visibles para:

- cuentas bancarias
- salarios
- beneficiarios
- `customData`
- payloads completos de expedientes o secciones de empleado

### 4.3 No hay pruebas para controles anti abuso

No se observa cobertura para:

- rate limiting
- lockout
- throttling por IP o identidad
- abuso del flujo de `refresh`

### 4.4 No hay pruebas para confianza de `X-Forwarded-*`

No existen pruebas que validen proxies confiables, spoofing de IP o consistencia entre forwarded headers y auditoria.

### 4.5 La auditoria durable de plataforma ya tiene cobertura parcial

El backoffice de suscripciones empresariales ya tiene pruebas de integracion para reemplazo, suspension, reactivacion inmediata, reactivacion programada, `status/preview`, historial, conflicto por solicitud pendiente y expiracion automatica, de modo que el flujo falla si pierde enforcement de rol, consistencia de historial o trazabilidad durable. El CRUD de `CommercialAddon` ya valida esa persistencia para create/update/activate/inactivate, incluyendo add-ons especializados. Aun falta extender esa exigencia de auditoria durable al CRUD completo de `CommercialPlan`.

### 4.6 No hay pruebas de contrato versionado

La API sigue sin snapshots o validacion automatica de `openapi.yaml` contra Swagger runtime o contra la superficie real de controllers.

## 5. Huecos actuales priorizados

1. Redaccion de PII de RRHH en auditoria.
2. Controles anti abuso en auth.
3. `X-Forwarded-*` y confianza de proxy.
4. Cobertura explicita de auditoria durable para todo el CRUD global restante de plataforma.
5. Contrato API versionado y validado automaticamente.

## 6. Conclusiones

La suite actual sirve bien para detectar regresiones funcionales y de wiring. No es correcto decir que el proyecto "no tiene pruebas"; si las tiene y cubren bastante. El problema real es mas especifico: los huecos de prueba estan concentrados justo en riesgos de seguridad operativa, trazabilidad y gobernanza contractual.

## 7. Criterio recomendado para nuevas remediaciones

Toda correccion derivada de esta auditoria deberia agregar, como minimo, una de estas coberturas segun corresponda:

- prueba unitaria de redaccion de auditoria con DTOs reales de RRHH
- integration test de anti abuso para auth
- prueba de configuracion o integracion para forwarded headers
- prueba de auditoria global para todo el backoffice de plataforma
- snapshot o validacion automatica de OpenAPI
