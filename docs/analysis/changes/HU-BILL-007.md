# HU-BILL-007 — Mantener estados de suscripcion

## 1. Informacion general

- **Codigo HU:** HU-BILL-007
- **Titulo:** Mantener estados de suscripcion
- **Modulo:** Platform / Billing / Company Subscriptions
- **Fecha de cierre:** 2026-04-02
- **Estado:** Completada
- **Responsable:** Codex
- **Referencia funcional:** HU-BILL-007
- **Referencia tecnica:** `src/CLARIHR.Domain/Companies`, `src/CLARIHR.Application/Features/PlatformSubscriptions`, `src/CLARIHR.Infrastructure/Companies`, `src/CLARIHR.Backoffice.Api/Controllers/PlatformCompanySubscriptionsController.cs`, `src/CLARIHR.Infrastructure/Persistence/Migrations/20260402185014_AddCompanySubscriptionStateManagement.cs`

## 2. Objetivo de la HU

Formalizar el ciclo de vida comercial y operativo de una suscripcion empresarial para que el backend pueda gobernar su estado actual, validar transiciones, conservar historial auditable y decidir si la empresa puede operar o entrar a procesos de cobro.

## 3. Alcance implementado

### Incluye
- Catalogo controlado de estados `Draft`, `Scheduled`, `Trial`, `Active`, `Suspended`, `Expired` y `Cancelled` en dominio.
- Historial persistente de transiciones con motivo, observaciones, actor u origen del sistema y fecha efectiva.
- Cambio manual de estado desde backoffice `platform`, historial paginado por suscripcion y vencimiento automatico por `expiresAtUtc`.
- Politica centralizada de capacidades por estado para `canOperate` y `canGenerateCharges`, reutilizada por overview, listados y servicios internos.

### No incluye
- Cobros, pagos, facturacion electronica o reactivacion automatica por pago.
- Renovaciones automaticas, grace periods o reglas diferenciadas enterprise fuera del alcance actual.

## 4. Impacto funcional

- El backoffice global ahora puede suspender, reactivar y cancelar suscripciones empresariales bajo una matriz controlada de transiciones.
- La plataforma puede consultar el historial completo de cambios de estado y distinguir cambios manuales de cambios automaticos por fecha.
- Los consumidores del overview y de los listados reciben capacidad operativa y de cobro derivada del estado actual, no solo el nombre del estado.

## 5. Impacto tecnico

- Capas afectadas: Domain, Application, Infrastructure, API, Tests, SQL/Data, Documentation.
- Componentes principales: `SubscriptionStatus`, `SubscriptionStatusPolicy`, `CompanySubscription`, `CompanySubscriptionStatusTransition`, `PlatformSubscriptionAdministration`, `CompanySubscriptionRepository`, `CompanySubscriptionLifecycleProcessor`, `PlatformCompanySubscriptionsController` y migracion EF de estado de suscripciones.
- Resumen: se desacoplo la logica de capacidades y transiciones del resto del billing, se hizo atomico el cambio de estado con historial, y se agrego procesamiento batch para promociones y vencimientos sin llevar ese costo al request path.

## 6. Cambios en API

### Endpoints nuevos
- `PATCH /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status`
- `GET /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status-history`

### Endpoints modificados
- `POST /api/platform/companies/{companyPublicId}/subscription/preview` ahora acepta `expiresAtUtc` y devuelve `canOperate` y `canGenerateCharges`.
- `PUT /api/platform/companies/{companyPublicId}/subscription` ahora acepta `expiresAtUtc` y devuelve metadata completa del estado actual.
- `GET /api/platform/companies/{companyPublicId}/subscription`, `GET /api/platform/companies/{companyPublicId}/subscriptions` y `GET /api/platform/company-subscriptions` ahora exponen `statusChangedAtUtc`, `currentStatusReasonCode`, `currentStatusOrigin` y banderas de capacidad operativa y de cobro.

## 7. Cambios en datos y persistencia

- Tabla nueva: `company_subscription_status_transitions`.
- Columnas nuevas en `company_subscriptions`: `expires_at_utc`, `status_changed_at_utc`, `current_status_reason_code`, `current_status_observations` y `current_status_origin`.
- Indices nuevos: unicidad parcial por empresa para filas vivas y para filas `Scheduled`, mas indice de historial por suscripcion y fecha.
- Migracion: `src/CLARIHR.Infrastructure/Persistence/Migrations/20260402185014_AddCompanySubscriptionStateManagement.cs`.
- Backfill: crea transiciones iniciales para filas legacy y reconstruye el cierre historico de suscripciones canceladas existentes.

## 8. Seguridad

- Los cambios manuales de estado quedaron restringidos a `PlatformOperatorRole.Admin`; `ReadOnly` solo puede consultar.
- La validacion de transicion, motivo obligatorio y pertenencia de la suscripcion a la empresa se hace en backend.
- Cada cambio manual o automatico deja auditoria durable de plataforma y trazabilidad detallada del historial.

## 9. Rendimiento

- Las consultas de overview, historial y listado global siguen paginadas y proyectadas a DTOs.
- El historial de estados usa ordenamiento server-side y `AsNoTracking()` en lectura.
- La promocion de `Scheduled` y la expiracion de `Active` se ejecutan por lotes en background, fuera del request path.

## 10. Pruebas realizadas

- Unit tests: `CompanySubscriptionStateManagementTests`.
- Integration tests: `BackofficeCompanySubscriptionsIntegrationTests`.
- Ejecucion validada:
  - `dotnet build CLARIHR.slnx -v minimal`
  - `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter CompanySubscriptionStateManagementTests -v minimal`
  - `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter BackofficeCompanySubscriptionsIntegrationTests -v minimal`

## 11. Documentacion actualizada

- `docs/business/current-system-business-flows.md`
- `docs/analysis/current-state/security-analysis.md`
- `docs/analysis/current-state/performance-analysis.md`
- `docs/analysis/current-state/testing-analysis.md`
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`
- `docs/analysis/changes/hu-index.md`

## 12. Riesgos, limitaciones y pendientes

- `Draft` y `Trial` ya existen en dominio y contrato, pero el backoffice actual no expone aun flujos manuales para crearlos o promoverlos.
- La politica de cobro sigue siendo basica y deliberadamente desacoplada de pagos, mora y renovaciones.
- Aun conviene ampliar pruebas de auditoria durable para el CRUD completo de `CommercialPlan`.
