# HU-BILL-012 — Rebalance de planes `FREE` y `MASTER`

## 1. Informacion general

- **Codigo HU:** HU-BILL-012
- **Titulo:** Rebalance de planes `FREE` y `MASTER`
- **Modulo:** Platform Billing / Commercial Catalog / Owner Subscriptions
- **Fecha de cierre:** 2026-04-07
- **Estado:** Implementada
- **Responsable:** Codex
- **Referencia funcional:** Requerimiento de CLARI para convertir el plan interno a `MASTER`, retirar `Enterprise legacy` y dejar `FREE` con la misma cobertura modular operativa que `MASTER`
- **Referencia tecnica:** catalogo comercial, provisioning owner, `PlanEntitlementService`, handlers owner, migracion EF `20260408014517_RebalanceSystemPlansMasterAndFree`

---

## 2. Objetivo de la HU

### Resumen

Reordenar los planes de sistema para que `FREE` siga siendo el ancla canonica del provisioning owner, pero sin quedar limitado por modulo frente a `MASTER`, mientras `MASTER` se mantiene como plan interno reservado para CLARI y sincronizado automaticamente con todo modulo comercial nuevo.

---

## 3. Alcance implementado

### Incluye

- creacion y sincronizacion de `MASTER` como plan de sistema interno con todos los modulos comerciales conocidos
- alineacion del catalogo base de `FREE` al mismo set modular completo sembrado para `MASTER`
- filtro owner para ocultar y bloquear `MASTER` salvo que el actor tambien tenga `PlatformOperator` activo
- migracion de datos para normalizar referencias `Enterprise legacy` hacia `FREE` y eliminar el plan legacy
- cobertura de pruebas unitarias e integracion para seeds, owner gating y proteccion de `MASTER`

### No incluye

- cambios de billing real, cobros o prorrateo
- reglas de redundancia o compatibilidad avanzada del marketplace por modulo
- actualizacion de skills o automatizaciones externas para nuevos modulos

---

## 4. Impacto funcional

### Cambios funcionales

- el catalogo global ahora expone dos planes de sistema activos: `FREE` y `MASTER`
- los owners normales siguen viendo `FREE` y planes comerciales regulares, pero no pueden ver ni seleccionar `MASTER`
- un owner que tambien es `PlatformOperator` activo si puede ver y previsualizar `MASTER`
- cualquier referencia legacy `Enterprise legacy` pasa a resolverse como `FREE`

### Flujo afectado

- catalogo comercial base de suscripciones
- autoservicio owner sobre suscripciones

### ¿Requiere actualizacion de flujo de negocio?

- Si

### Documento vivo afectado

- `docs/business/current-system-business-flows.md`

---

## 5. Impacto tecnico

### Capas afectadas

- [x] Domain
- [x] Application
- [x] Infrastructure
- [ ] API
- [x] Tests
- [x] Documentation
- [x] SQL / Data

### Componentes modificados

- `CommercialModuleCatalog`
- `ProvisioningConstants`
- `IPlanEntitlementService`
- `PlanEntitlementService`
- `CompanyProvisioningService`
- `StartupInitializationExtensions`
- `AccountCompanySubscriptionAdministration`
- `CommercialPlanAdministration`
- migracion `20260408014517_RebalanceSystemPlansMasterAndFree`

### Resumen tecnico

El backend ahora mantiene dos planes de sistema con diferenciacion comercial pero sin brecha modular inicial. `FREE` conserva el codigo canonico del provisioning y se resiembra con el mismo catalogo completo de modulos que `MASTER`. `MASTER` sigue autocorrigiendose siempre al catalogo completo incluso cuando un update intente recortarlo. Los handlers owner usan `IPlatformOperatorRepository` para filtrar `MASTER` y responder `ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN` ante accesos directos no autorizados.

---

## 6. Cambios en API

### Endpoints modificados

- `GET /api/platform/commercial-plans` — ahora documenta y devuelve `FREE` y `MASTER` como planes de sistema visibles en backoffice
- `PUT /api/platform/commercial-plans/{publicId}` — `MASTER` siempre persiste el catalogo completo de `moduleKeys`
- `GET /api/account/companies/{publicId}/subscription/plans` — oculta `MASTER` para owners que no sean `PlatformOperator`
- `POST /api/account/companies/{publicId}/subscription/preview` — devuelve `403` para `MASTER` cuando el owner no es operador activo
- `PUT /api/account/companies/{publicId}/subscription` — devuelve `403` para `MASTER` cuando el owner no es operador activo

### Contratos afectados

- Request: No
- Response: No
- Codigos de error: Si
- Paginacion / filtros / sorting: No
- Autenticacion / autorizacion: Si

### Documentacion actualizada

- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

---

## 7. Cambios en datos y persistencia

### Cambios realizados

- seed dual para planes de sistema `FREE` y `MASTER`
- resincronizacion de entitlements de `FREE`
- resincronizacion de entitlements y version comercial de `MASTER`
- remapeo de `company_subscriptions` y `company_subscription_plan_changes` desde aliases `Enterprise legacy` hacia `FREE`
- eliminacion del plan legacy y sus entitlements asociados

### Scripts o migraciones relacionados

- `src/CLARIHR.Infrastructure/Persistence/Migrations/20260408014517_RebalanceSystemPlansMasterAndFree.cs`

### Consideraciones

La migracion no cambia contratos publicos, pero si corrige datos existentes para que el provisioning siga apuntando al mismo codigo `FREE` y para que el catalogo comercial deje de depender del plan legacy eliminado.

---

## 8. Seguridad

### Validaciones de seguridad aplicadas

- [ ] Tenant isolation
- [x] Autenticacion
- [x] Autorizacion / RBAC
- [ ] Permisos por accion
- [ ] Permisos por campo
- [ ] Proteccion de datos sensibles
- [ ] Auditoria
- [ ] No aplica

### Resumen

El acceso owner a `MASTER` ya no depende solo de ownership sobre la compania: requiere que el actor tambien tenga un `PlatformOperator` activo, reutilizando la misma autoridad persistida del backoffice global.

### Documento vivo afectado

- `docs/analysis/current-state/security-analysis.md`

---

## 9. Rendimiento

### Consideraciones de rendimiento

- [ ] Paginacion
- [ ] Proyeccion a DTO
- [ ] `AsNoTracking()`
- [ ] Indices revisados
- [ ] Evitar N+1
- [ ] Proceso pesado fuera del request path
- [x] No aplica

### Resumen

El cambio no introduce nuevas rutas pesadas; se limita a seeds, filtros owner y sincronizacion de catalogo de planes.

### Documento vivo afectado

- `docs/analysis/current-state/remediation-plan.md`

---

## 10. Pruebas realizadas

### Unit tests agregados o modificados

- `CommercialPlanAdministrationTests` — valida que `MASTER` no acepte recortes manuales de `moduleKeys`
- `AccountCompanySubscriptionHelperTests` — valida el guard `MASTER` con y sin `PlatformOperator`
- `ProvisionCompanyForUserCommandHandlerTests` — actualiza el contrato de sincronizacion de planes de sistema

### Cobertura minima validada

- [x] Happy path
- [x] Validaciones
- [x] Errores esperados
- [x] Permisos
- [ ] Tenant scope
- [x] Reglas criticas
- [ ] No aplica

### Ejecucion

- `dotnet build CLARIHR.slnx`
- `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter "CommercialPlanAdministrationTests|ProvisionCompanyForUserCommandHandlerTests|AccountCompanySubscriptionHelperTests"`
- `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj --filter "MigrationSeedingIntegrationTests|BackofficeCommercialPlansIntegrationTests|AccountCompanySubscriptionsIntegrationTests"`

### Documento vivo afectado

- `docs/analysis/current-state/testing-analysis.md`

---

## 11. Documentacion actualizada

### Documentos actualizados

- `docs/business/current-system-business-flows.md`
- `docs/analysis/current-state/architecture-analysis.md`
- `docs/analysis/current-state/security-analysis.md`
- `docs/analysis/current-state/testing-analysis.md`
- `docs/analysis/current-state/remediation-plan.md`
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`
- `docs/analysis/changes/hu-index.md`

---

## 12. Riesgos o pendientes

- la gobernanza comercial aun no resuelve compatibilidad o redundancia avanzada de add-ons por modulo
- `MASTER` queda sincronizado por codigo, pero la matriz canonica `producto -> modulo -> permiso` sigue siendo una remediacion en progreso
- el suite completo del repositorio todavia conserva fallas legacy no relacionadas fuera del alcance de esta HU
