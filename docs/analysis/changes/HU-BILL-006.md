# HU-BILL-006 — Activar una suscripcion para una empresa

## 1. Informacion general

- **Codigo HU:** HU-BILL-006
- **Titulo:** Activar una suscripcion para una empresa
- **Modulo:** Platform / Billing / Company Subscriptions
- **Fecha de cierre:** 2026-04-02
- **Estado:** Completada
- **Responsable:** Codex
- **Referencia funcional:** HU-BILL-006
- **Referencia tecnica:** `src/CLARIHR.Application/Features/PlatformSubscriptions`, `src/CLARIHR.Infrastructure/Companies`, `src/CLARIHR.Infrastructure/Persistence/Migrations/20260402170125_AddCompanySubscriptionVersioningAndScheduling.cs`

## 2. Objetivo de la HU

Formalizar la relacion comercial entre una empresa y un plan de billing para soportar activacion inmediata o programada, versionado historico del plan y entrada controlada al modelo de cobro.

## 3. Alcance implementado

### Incluye
- Activacion inmediata o programada de suscripciones empresariales desde backoffice `platform`.
- Versionado explicito de precios mediante `CommercialPlanVersion` y snapshot historico en `CompanySubscription`.
- Preview de activacion, overview comercial por empresa, historial enriquecido, listado global y promocion automatica de filas `Scheduled`.

### No incluye
- Cambios de plan con prorrateo o descuentos.
- Add-ons contratados, cargos, pagos o facturacion electronica.

## 4. Impacto funcional

- La empresa ahora puede quedar con una suscripcion `Active` o `Scheduled` ligada a una version exacta del plan.
- El backoffice puede previsualizar elegibilidad y condiciones comerciales antes de confirmar.
- La empresa se marca como facturable solo cuando entra en una suscripcion comercial activa no sistema.

## 5. Impacto tecnico

- Capas afectadas: Domain, Application, Infrastructure, API, Tests, SQL/Data, Documentation.
- Componentes principales: `CommercialPlan`, `CommercialPlanVersion`, `CompanySubscription`, `PlatformSubscriptionAdministration`, `CompanySubscriptionRepository`, `CompanySubscriptionLifecycleProcessor`, controladores backoffice y migracion EF.
- Resumen: se desacoplo el catalogo comercial del estado de suscripcion activa mediante versionado de plan, reglas de elegibilidad backend, auditoria durable y promocion automatica idempotente.

## 6. Cambios en API

### Endpoints nuevos
- `POST /api/platform/companies/{companyPublicId}/subscription/preview`
- `GET /api/platform/company-subscriptions`

### Endpoints modificados
- `GET /api/platform/companies/{companyPublicId}/subscription` ahora devuelve overview con `currentSubscription` y `scheduledReplacement`.
- `PUT /api/platform/companies/{companyPublicId}/subscription` ahora acepta `commercialPlanId`, `startDateUtc` y `periodicity`, y puede crear `Active` o `Scheduled`.
- `GET /api/platform/companies/{companyPublicId}/subscriptions` ahora expone version de plan, periodicidad, moneda y auditoria basica por fila.

## 7. Cambios en datos y persistencia

- Tabla nueva: `commercial_plan_versions`.
- Columnas nuevas: `companies.is_billable`, `companies.billable_since_utc`, y metadata comercial/auditoria en `company_subscriptions`.
- Indices nuevos: unicidad por empresa para `Active` y `Scheduled`, lookup por `status + start_date_utc`, y FK a `commercial_plan_versions`.
- Migracion: `src/CLARIHR.Infrastructure/Persistence/Migrations/20260402170125_AddCompanySubscriptionVersioningAndScheduling.cs`.
- Backfill: crea `v1` para planes existentes, liga suscripciones historicas a `v1` y reconstruye `IsBillable`.

## 8. Seguridad

- Autorizacion backend solo para superficie `platform`.
- Validaciones criticas en servidor: empresa elegible, representante legal activo, owner/admin activo, plan activo, version efectiva valida, fecha valida, unicidad de `Scheduled`.
- Auditoria durable en activacion inmediata, programada y promocion automatica.

## 9. Rendimiento

- Historial y listado global mantienen paginacion.
- Queries de lectura usan proyecciones y `AsNoTracking()`.
- La promocion de `Scheduled` ocurre fuera del request path mediante background service y procesamiento batch.

## 10. Pruebas realizadas

- Unit tests: `CommercialPlanDomainTests`, `CommercialPlanAdministrationTests`, `ProvisionCompanyForUserCommandHandlerTests`.
- Integration tests: `BackofficeCompanySubscriptionsIntegrationTests`.
- Ejecucion validada:
  - `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter "CommercialPlanDomainTests|CommercialPlanAdministrationTests|ProvisionCompanyForUserCommandHandlerTests"`
  - `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter "BackofficeCompanySubscriptionsIntegrationTests"`

## 11. Documentacion actualizada

- `docs/business/current-system-business-flows.md`
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`
- `docs/analysis/changes/hu-index.md`
