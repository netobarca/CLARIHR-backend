# Analisis actual de testing

## 1. Resumen ejecutivo

La estrategia de pruebas del backend es mejor de lo que reflejaba la version anterior de este documento. La validacion actualizada del **30 de marzo de 2026** verifico:

- `56` unit tests dirigidos aprobados para auth, platform authorization, provisioning, commercial plans y commercial add-ons
- `13` integration tests dirigidos aprobados para Platform Backoffice API
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

`BackofficeCommercialAddonsIntegrationTests`, `BackofficeCommercialPlansIntegrationTests`, `BackofficeCompanySubscriptionsIntegrationTests` y `PlatformAuthenticationIntegrationTests` ejercitan el login del backoffice, el CRUD de add-ons y planes globales, el reemplazo de suscripciones empresariales, la separacion de audiencias `core/platform`, el rol `ReadOnly` y la auditoria durable de plataforma en writes globales. Desde HU-BILL-004, esa cobertura de add-ons incluye configuraciones `Massive` y `Specialized`, filtros por `type` y `billingModel`, y compatibilidad de lectura para filas masivas preexistentes.

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

El reemplazo de suscripciones empresariales ya tiene una prueba de integracion que falla si no se persiste `PlatformAuditLog` y el CRUD de `CommercialAddon` ya valida esa persistencia para create/update/activate/inactivate, incluyendo add-ons especializados. Aun falta extender esa exigencia de auditoria durable al CRUD completo de `CommercialPlan`.

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
