# HU-BILL-011 - Suscripciones owner, modulos efectivos y marketplace sin pagos

## 1. Resumen

Este cambio lleva la administracion comercial desde el backoffice hacia la superficie owner del core RH y convierte el acceso a modulos en una capacidad comercial viva.

Resultado funcional:

- toda empresa creada por un owner sigue naciendo con suscripcion `FREE`
- los planes y add-ons ahora administran `moduleKeys`
- la plataforma puede editar los modulos del plan `FREE`
- el owner puede consultar su plan, ver modulos efectivos, previsualizar cambios de plan y aplicar upgrades o downgrades inmediatos
- el owner puede ver el marketplace de add-ons y adquirirlos solo si su plan activo no es `FREE`
- los modulos efectivos del sistema RH salen de `plan entitlements + active company add-ons`
- al bajar a `FREE`, los add-ons activos se desactivan automaticamente

## 2. Cambios implementados

### 2.1 Catalogo comercial y entitlements

- se agrega `CommercialModuleCatalog` como catalogo canonico de modulos comerciales
- `CommercialPlan` y `CommercialAddon` pasan a persistir y administrar `moduleKeys`
- se agrega `CommercialAddonEntitlement` y la tabla `commercial_addon_entitlements`
- el bootstrap de `FREE` deja de reinyectar modulos fijos en cada provisioning y mantiene solo un baseline seed editable

### 2.2 API de plataforma

- se agrega `GET /api/platform/commercial-modules`
- planes y add-ons comerciales ahora exponen `moduleCount` y `moduleKeys`
- los requests `create/update` de planes y add-ons aceptan `moduleKeys`
- preview y activacion de add-ons bloquean `FREE`

### 2.3 Superficie owner

- se agrega `AccountCompanySubscriptionsController`
- nuevos endpoints:
  - `GET /api/account/companies/{publicId}/subscription`
  - `GET /api/account/companies/{publicId}/subscription/plans`
  - `POST /api/account/companies/{publicId}/subscription/preview`
  - `PUT /api/account/companies/{publicId}/subscription`
  - `GET /api/account/companies/{publicId}/subscription/addons`
  - `GET /api/account/companies/{publicId}/subscription/addons/marketplace`
  - `POST /api/account/companies/{publicId}/subscription/addons/preview`
  - `POST /api/account/companies/{publicId}/subscription/addons`

### 2.4 Gating comercial real

- `IPlanEntitlementService` resuelve modulos efectivos por empresa
- los authorization services de RH ahora validan que el modulo este comercialmente habilitado antes de permitir operar
- el gating se resuelve en backend por empresa activa; no cambia el shape del JWT

## 3. Documentos vivos actualizados

- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

## 4. Validacion ejecutada

- `dotnet build`
- `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~BackofficeCommercialPlansIntegrationTests|FullyQualifiedName~BackofficeCommercialAddonsIntegrationTests|FullyQualifiedName~AccountCompanySubscriptionsIntegrationTests|FullyQualifiedName~BackofficeCompanySubscriptionsIntegrationTests|FullyQualifiedName~BackofficeCompanySubscriptionAddonsIntegrationTests"`
- `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ApiIntegrationTests.OrgUnits_Inactivate_WhenHasActiveChildren_ShouldReturn409Conflict|FullyQualifiedName~ApiIntegrationTests.PositionSlots_UpdateOccupancy_WhenOverCapacity_ShouldReturn422|FullyQualifiedName~ApiIntegrationTests.CostCenters_Create_WithDuplicateCode_ShouldReturn409Conflict|FullyQualifiedName~ApiIntegrationTests.PersonnelFiles_Export_WithColumnFilters_ShouldApplySameFilters|FullyQualifiedName~ApiIntegrationTests.SalaryTabulator_FullFlow_ShouldCreateSubmitApproveAndApplyLine"`

## 5. Riesgos o pendientes

- `tests/CLARIHR.Application.UnitTests` sigue teniendo fallas preexistentes no relacionadas a esta HU sobre normalizacion en otros modulos del dominio
- `docs/technical/api/openapi.yaml` sigue siendo un suplemento manual parcial; se actualizo solo la superficie comercial impactada por esta HU
