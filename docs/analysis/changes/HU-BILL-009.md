# HU-BILL-009 — Activar o desactivar add-ons en una empresa

## 1. Informacion general

- **Codigo HU:** HU-BILL-009
- **Titulo:** Activar o desactivar add-ons en una empresa
- **Modulo:** Platform / Billing / Company Subscription Add-ons
- **Fecha de cierre:** 2026-04-02
- **Estado:** Completada
- **Responsable:** Codex
- **Referencia funcional:** HU-BILL-009
- **Referencia tecnica:** `src/CLARIHR.Domain/Companies`, `src/CLARIHR.Application/Features/PlatformSubscriptions`, `src/CLARIHR.Infrastructure/Companies`, `src/CLARIHR.Backoffice.Api/Controllers/PlatformCompanySubscriptionsController.cs`, `src/CLARIHR.Infrastructure/Persistence/Migrations/20260402234555_AddCompanySubscriptionAddons.cs`

## 2. Objetivo de la HU

Permitir que el backoffice global active o desactive add-ons para una empresa con suscripcion elegible, preservando vigencia, historial, motivo, preview comercial, auditoria durable y aplicacion automatica de cambios programados sin tratar el caso como una simple bandera booleana.

## 3. Alcance implementado

### Incluye
- Estado comercial por empresa y add-on con snapshots de catalogo, moneda, precio, periodicidad y fecha efectiva.
- Flujo `preview + create + history + cancel + lifecycle processor` para activaciones y desactivaciones inmediatas o programadas.
- Listado de add-ons actuales, catalogo elegible por empresa, historial paginado de cambios y auditoria durable de plataforma.
- Validaciones de empresa, suscripcion elegible, add-on activo en catalogo, no duplicidad, no conflicto pendiente, motivo obligatorio y coherencia de fecha efectiva.
- Compatibilidad con el patron actual de `subscription/plan-changes`; tambien se alineo la documentacion y las pruebas de esa superficie para evitar regresiones.

### No incluye
- Entitlements operativos, gating tenant, asignacion de seats, consumo por volumen, descuentos o prorrateo detallado.
- Generacion de cobro final, pagos o facturacion electronica.

## 4. Impacto funcional

- El backoffice `platform` ahora puede consultar add-ons activos y pendientes por empresa, asi como el catalogo comercial elegible para contratacion.
- Antes de confirmar una activacion o desactivacion, el operador puede ver una proyeccion informativa con fecha efectiva, estado resultante, cantidad base y efecto estimado en el siguiente cobro.
- Los cambios programados se aplican automaticamente al llegar su fecha, y cada solicitud o cancelacion conserva historial auditable y trazabilidad del actor.

## 5. Impacto tecnico

- Capas afectadas: Domain, Application, Infrastructure, API, Tests, SQL/Data y Documentation.
- Componentes principales: `CompanyCommercialAddon`, `CompanyCommercialAddonChange`, enums de add-on empresarial, `PlatformSubscriptionAddonAdministration`, `ICompanySubscriptionRepository`, `CompanySubscriptionRepository`, `CompanySubscriptionLifecycleProcessor`, `PlatformCompanySubscriptionsController` y migracion EF de add-ons empresariales.
- Resumen: se reutilizo el patron CQRS y lifecycle ya existente para cambios de plan, se separo estado vigente de historial y se mantuvo el alcance comercial desacoplado de billing final y entitlements.

## 6. Cambios en API

### Endpoints nuevos
- `GET /api/platform/companies/{companyPublicId}/subscription/addons`
- `GET /api/platform/companies/{companyPublicId}/subscription/addons/eligible`
- `POST /api/platform/companies/{companyPublicId}/subscription/addon-changes/preview`
- `POST /api/platform/companies/{companyPublicId}/subscription/addon-changes`
- `GET /api/platform/companies/{companyPublicId}/subscription/addon-changes`
- `POST /api/platform/companies/{companyPublicId}/subscription/addon-changes/{addonChangePublicId}/cancel`

### Endpoints alineados en documentacion
- `POST /api/platform/companies/{companyPublicId}/subscription/plan-changes/preview`
- `POST /api/platform/companies/{companyPublicId}/subscription/plan-changes`
- `GET /api/platform/companies/{companyPublicId}/subscription/plan-changes`
- `POST /api/platform/companies/{companyPublicId}/subscription/plan-changes/{planChangePublicId}/cancel`

## 7. Cambios en datos y persistencia

- Tablas nuevas: `company_commercial_addons` y `company_commercial_addon_changes`.
- Indices nuevos para unicidad operativa por empresa y add-on, historial por fecha efectiva y localizacion rapida de cambios programados.
- Migracion: `src/CLARIHR.Infrastructure/Persistence/Migrations/20260402234555_AddCompanySubscriptionAddons.cs`.
- El lifecycle processor ahora tambien consume cambios programados de add-ons y puede aplicar o rechazar filas vencidas de manera idempotente.

## 8. Seguridad

- Las lecturas de add-ons y de historial permiten `platform` autenticado; las mutaciones y cancelaciones quedan restringidas a `PlatformOperatorRole.Admin`.
- Todas las validaciones criticas permanecen en backend: pertenencia de empresa, existencia de suscripcion vigente elegible, reglas de conflicto y motivo obligatorio.
- Cada cambio aplicado, cancelado o rechazado deja auditoria durable mediante `PlatformAuditLog`.

## 9. Rendimiento

- Los listados de add-ons actuales, elegibles e historial son paginados y proyectados a DTOs.
- La aplicacion de cambios programados se ejecuta fuera del request path mediante background processing.
- Se corrigio tambien la traduccion EF de listados de `plan-changes` y de add-ons empresariales ordenando server-side antes de proyectar, lo que elimino errores `500` en integracion y evita regresiones observables.

## 10. Pruebas realizadas

- Unit tests: `CompanySubscriptionAddonManagementTests`, `CompanySubscriptionStateManagementTests`, `CommercialAddonDomainTests`, `ProvisionCompanyForUserCommandHandlerTests`.
- Integration tests: `BackofficeCompanySubscriptionAddonsIntegrationTests`, `BackofficeCompanySubscriptionsIntegrationTests`, `BackofficeCommercialAddonsIntegrationTests`.
- Ejecucion validada:
  - `dotnet build CLARIHR.slnx`
  - `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter "CompanySubscriptionAddonManagementTests|CompanySubscriptionStateManagementTests|CommercialAddonDomainTests|ProvisionCompanyForUserCommandHandlerTests"`
  - `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter "BackofficeCompanySubscriptionsIntegrationTests|BackofficeCommercialAddonsIntegrationTests|BackofficeCompanySubscriptionAddonsIntegrationTests"`

## 11. Documentacion actualizada

- `docs/business/current-system-business-flows.md`
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`
- `docs/analysis/changes/hu-index.md`

## 12. Riesgos, limitaciones y pendientes

- La estimacion economica del preview sigue siendo informativa y deliberadamente desacoplada del calculo final de billing.
- `NextBillingCycle` queda anclado a la suscripcion activa actual; si negocio necesita otra semantica debera formalizarse en otra HU.
- La HU no activa todavia dependencias complejas entre add-ons ni compatibilidades avanzadas por plan.
