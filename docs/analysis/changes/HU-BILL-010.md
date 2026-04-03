# HU-BILL-010 — Reactivar una suscripcion suspendida

## 1. Informacion general

- **Codigo HU:** HU-BILL-010
- **Titulo:** Reactivar una suscripcion suspendida
- **Modulo:** Platform / Billing / Company Subscriptions
- **Fecha de cierre:** 2026-04-02
- **Estado:** Completada
- **Responsable:** Codex
- **Referencia funcional:** HU-BILL-010
- **Referencia tecnica:** `src/CLARIHR.Domain/Companies`, `src/CLARIHR.Application/Features/PlatformSubscriptions`, `src/CLARIHR.Infrastructure/Companies`, `src/CLARIHR.Backoffice.Api/Controllers/PlatformCompanySubscriptionsController.cs`, `src/CLARIHR.Infrastructure/Persistence/Migrations/20260403004331_AddCompanySubscriptionStatusChangeRequests.cs`

## 2. Objetivo de la HU

Permitir que una suscripcion suspendida vuelva a operar mediante una reactivacion comercial formal, inmediata o programada, sin crear una suscripcion nueva ni perder trazabilidad del actor, motivo, fecha efectiva y condiciones comerciales vigentes.

## 3. Alcance implementado

### Incluye
- Preview de reactivacion `Suspended -> Active` desde backoffice `platform`.
- Reactivacion inmediata reutilizando la misma suscripcion suspendida y preservando plan, version y add-ons.
- Reactivacion programada por fecha especifica mediante una solicitud interna auditable.
- Exposicion de `pendingStatusChange` en overview e historial de suscripciones cuando existe una reactivacion pendiente.
- Aplicacion y rechazo automatico de reactivaciones programadas desde el lifecycle processor con auditoria durable.
- Validaciones backend de permiso, estado suspendido, motivo obligatorio, fecha efectiva, expiracion y conflicto por solicitud pendiente.

### No incluye
- Reactivacion automatica por pago, grace periods, recalculo financiero o reapertura de facturas.
- Cambio de plan, cambio de version, activacion avanzada de add-ons o reinstalacion tecnica de configuraciones.

## 4. Impacto funcional

- El operador `Admin` ahora puede previsualizar y confirmar una reactivacion manual desde `status/preview` y `PATCH .../status`.
- La reactivacion inmediata devuelve la suscripcion suspendida a `Active` en la misma transaccion; la reactivacion futura conserva el estado `Suspended` y muestra una accion pendiente visible en el overview.
- Cuando llega la fecha efectiva, el lifecycle processor aplica primero la reactivacion programada y luego los cambios de plan o add-ons del mismo dia, o bien rechaza la solicitud si la suscripcion ya no es elegible.

## 5. Impacto tecnico

- Capas afectadas: Domain, Application, Infrastructure, API, Tests, SQL/Data, Documentation.
- Componentes principales: `CompanySubscriptionStatusChangeRequest`, `SubscriptionStatusChangeRequestStatus`, `PlatformSubscriptionAdministration`, `CompanySubscriptionRepository`, `CompanySubscriptionLifecycleProcessor`, `PlatformCompanySubscriptionsController` y migracion EF de solicitudes de cambio de estado.
- Resumen: se extendio el flujo existente de status sin crear un modulo publico paralelo, se desacoplo la programacion futura en un agregado interno y se mantuvo la reactivacion inmediata sobre la misma suscripcion para no romper continuidad comercial.

## 6. Cambios en API

### Endpoints nuevos
- `POST /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status/preview`

### Endpoints modificados
- `PATCH /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status` ahora acepta `effectiveDateUtc` para reactivaciones.
- `GET /api/platform/companies/{companyPublicId}/subscription`
- `GET /api/platform/companies/{companyPublicId}/subscriptions`

### Contratos afectados
- Request: si.
- Response: si.
- Codigos de error: si.
- Paginacion / filtros / sorting: no.
- Autenticacion / autorizacion: no cambia el modelo, pero el write sigue restringido a `PlatformOperatorRole.Admin`.

## 7. Cambios en datos y persistencia

- Tabla nueva: `company_subscription_status_change_requests`.
- Indices nuevos: busqueda por `status + effective_date_utc`, historial por empresa y unicidad parcial por suscripcion para solicitudes `Scheduled`.
- Migracion: `src/CLARIHR.Infrastructure/Persistence/Migrations/20260403004331_AddCompanySubscriptionStatusChangeRequests.cs`.
- El lifecycle processor ahora consume reactivaciones programadas antes de cambios de plan y add-ons del mismo dia.

## 8. Seguridad

- Las lecturas y previews requieren token `platform`; la confirmacion del cambio sigue restringida a `PlatformOperatorRole.Admin`.
- La elegibilidad critica se valida en backend: empresa activa, suscripcion suspendida, fecha valida, motivo permitido y ausencia de conflicto pendiente.
- Cada solicitud, aplicacion o rechazo de reactivacion deja `PlatformAuditLog` persistente.

## 9. Rendimiento

- El overview y el historial reutilizan proyecciones existentes y agregan `pendingStatusChange` sin traer entidades completas al controller.
- Las solicitudes programadas usan indices dedicados y se aplican fuera del request path mediante background processing.
- La ejecucion por lotes del lifecycle evita hacer verificacion de fechas efectivas complejas durante el request interactivo.

## 10. Pruebas realizadas

- Unit tests: `PlatformSubscriptionStatusChangeTests`, `CompanySubscriptionStateManagementTests`, `ProvisionCompanyForUserTests`.
- Integration tests: `BackofficeCompanySubscriptionsIntegrationTests`.
- Ejecucion validada:
  - `dotnet build CLARIHR.slnx -v minimal`
  - `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter "PlatformSubscriptionStatusChangeTests|CompanySubscriptionStateManagementTests|ProvisionCompanyForUserTests" -v minimal`
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

- En este MVP la programacion futura solo soporta `Suspended -> Active`; otros estados destino quedan fuera.
- La reactivacion no resuelve automaticamente add-ons complejos ni cambios comerciales adicionales.
- No se agrego endpoint publico para cancelar una solicitud de reactivacion programada; si negocio lo requiere, debe salir en una HU separada.
