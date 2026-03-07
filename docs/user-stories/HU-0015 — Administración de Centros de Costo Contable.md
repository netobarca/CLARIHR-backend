# HU-0015 — Administración de Centros de Costo Contable (Núcleo para Contabilidad de Nómina)

## 0) Validación del estado actual
Resultado del análisis del backend/documentación actual:

- **No existe** módulo de `Cost Centers` como agregado propio (entidad + CRUD + seguridad + auditoría + tests).
- Hoy solo hay **campos sueltos** `costCenterCode` en:
  - `OrgUnits` (HU-0011)
  - `PositionSlots` (HU-0013)
- Esos campos son texto libre y **no garantizan**:
  - catálogo central de centros de costo
  - reglas contables por tipo de gasto (salarios, aportes patronales, provisiones)
  - integridad referencial, ciclo de vida y gobierno del dato

Conclusión:
- El requerimiento está **parcialmente cubierto** (captura libre de código), pero **falta implementación funcional completa**.

---

## 1) Descripción del requerimiento (visión de negocio)
Como **Dueño de la cuenta / Administrador de Empresa / RRHH / Finanzas**,
quiero **definir y administrar centros de costo contable**,
para agrupar y controlar la generación de partidas contables de:
- gastos de salarios
- aportes patronales
- reservas/provisiones contables

de manera que la empresa tenga trazabilidad, consistencia y reutilización transversal en procesos de talento y contabilidad.

---

## 2) Objetivo funcional (qué habilita)
- Catálogo único tenant-scoped de centros de costo.
- Estandarización del código contable usado por OrgUnits, PositionSlots y procesos de nómina.
- Reglas explícitas para clasificación de costos contables.
- Exportación y consulta confiable para conciliación contable.
- Base para integración futura con ERP/contabilidad.

---

## 3) Alcance API (backend)
### Incluye
- CRUD de `CostCenter` por empresa.
- Activación/inactivación con reglas de uso.
- Búsqueda paginada y detalle por id.
- Clasificación por tipo contable:
  - `SalaryExpense`
  - `EmployerContribution`
  - `ProvisionReserve`
  - `Mixed`
- Referencias opcionales a cuentas contables por tipo.
- Endpoint de validación de uso para consumo interno (`canInactivate`, `usageSummary`).
- Export de catálogo (`csv|xlsx`).

### Fuera de alcance (por ahora, pero preparado)
- Publicación automática de pólizas en ERP externo.
- Motor de distribución de costo por porcentaje entre múltiples centros.
- Reglas fiscales por país con parametrización avanzada.

---

## 4) Actores y permisos
- **AccountOwner / Dueño de la cuenta**: administración total.
- **CompanyAdmin**: administración funcional completa.
- **FinanceAdmin / Compensaciones**: crea/edita centros de costo.
- **HRRead / FinanceRead**: consulta/exporta.

Permisos sugeridos:
- `CostCenters.Read`
- `CostCenters.Admin`
- `iam.administration.manage` (override)
- `platform_admin` (override)

Regla base:
- La API valida siempre coincidencia entre `companyId` de la ruta y claim `tid`.

---

## 5) Datos que debe manejar el backend (modelo mínimo)

### 5.1 Entidad principal: `CostCenter`
- `Id` (GUID público)
- `CompanyId` (GUID)
- `Code` / `NormalizedCode` (string, único por empresa)
- `Name` / `NormalizedName` (string)
- `Type` (enum): `SalaryExpense|EmployerContribution|ProvisionReserve|Mixed`
- `PayrollExpenseAccountCode` (string, opcional)
- `EmployerContributionAccountCode` (string, opcional)
- `ProvisionAccountCode` (string, opcional)
- `Description` (string, opcional)
- `IsActive` (bool)
- `ConcurrencyToken` (GUID)
- `CreatedAtUtc`, `UpdatedAtUtc`

### 5.2 Relación de uso (proyección, no necesariamente tabla nueva en v1)
- Uso del `CostCenter.Code` en:
  - `OrgUnit.CostCenterCode`
  - `PositionSlot.CostCenterCode`
- En v1 la integridad se valida a nivel aplicación (lookup por code activo del tenant).

---

## 6) Reglas de negocio (backend)

### RN-01 Unicidad y formato
- `Code` único por tenant.
- `Code` y `Name` obligatorios, normalizados y sin espacios extremos.

### RN-02 Activación/Inactivación
- No se puede inactivar un centro de costo si está en uso activo por `OrgUnits` o `PositionSlots`, salvo política explícita de reemplazo.
- Reactivar conserva historial.

### RN-03 Integridad de uso
- `OrgUnits` y `PositionSlots` solo aceptan `CostCenterCode` existente y activo del mismo tenant (cuando se provea).
- Operaciones por id distinguen `NotFound` vs `TenantMismatch`.

### RN-04 Concurrencia y auditoría
- Toda escritura por id valida `ConcurrencyToken`.
- Toda escritura registra auditoría `before/after`.

---

## 7) Requerimientos técnicos del Backend (arquitectura + persistencia)

### RT-01 Arquitectura
- Mantener patrón vigente:
  - Clean Architecture
  - CQRS (`Commands/Queries + Handlers`)
  - FluentValidation
  - `ProblemDetails`
  - tenant-scope por `tid`

### RT-02 Persistencia (PostgreSQL)
Tablas mínimas sugeridas:
- `cost_centers`

Índices recomendados:
- `uq_cost_centers__public_id`
- `uq_cost_centers__tenant_code`
- `ix_cost_centers__tenant_type_active`
- `ix_cost_centers__tenant_normalized_name`

### RT-03 Rendimiento
- Listados paginados obligatorios.
- Filtros por `type`, `isActive`, `q`.
- Lecturas `AsNoTracking`.
- Export por proyección plana única.

### RT-04 Seguridad y autorización
- `ICostCenterAuthorizationService` siguiendo patrón `OrgUnitAuthorizationService`.
- Lectura:
  - `CostCenters.Read`
  - `CostCenters.Admin`
  - overrides
- Escritura:
  - `CostCenters.Admin`
  - overrides

### RT-05 Auditoría
Eventos sugeridos:
- `COST_CENTER_CREATED`
- `COST_CENTER_UPDATED`
- `COST_CENTER_ACTIVATED`
- `COST_CENTER_INACTIVATED`

---

## 8) Endpoints propuestos (API v1)

### 8.1 Catálogo de centros de costo
- `POST   /api/v1/companies/{companyId}/cost-centers`
- `GET    /api/v1/companies/{companyId}/cost-centers?type=&isActive=&q=&page=&pageSize=`
- `GET    /api/v1/cost-centers/{id}`
- `PUT    /api/v1/cost-centers/{id}`
- `PATCH  /api/v1/cost-centers/{id}/activate`
- `PATCH  /api/v1/cost-centers/{id}/inactivate`

### 8.2 Uso y export
- `GET    /api/v1/cost-centers/{id}/usage`
- `GET    /api/v1/companies/{companyId}/cost-centers/export?format=csv|xlsx&type=&isActive=&q=`

---

## 9) Contratos (DTOs) mínimos

### 9.1 `CreateCostCenterRequest`
- `code`*
- `name`*
- `type`*
- `payrollExpenseAccountCode`
- `employerContributionAccountCode`
- `provisionAccountCode`
- `description`

### 9.2 `UpdateCostCenterRequest`
- mismo contrato + `concurrencyToken`*

### 9.3 `ConcurrencyRequest`
- `concurrencyToken`*

### 9.4 `CostCenterResponse`
- `id`, `companyId`, `code`, `name`, `type`
- cuentas contables opcionales
- `isActive`, `concurrencyToken`, `createdAtUtc`, `updatedAtUtc`

### 9.5 `CostCenterUsageResponse`
- `costCenterId`
- `orgUnitsCount`
- `positionSlotsCount`
- `canInactivate`

---

## 10) Errores y códigos recomendados
- `400` `VALIDATION_ERROR`
- `401` `UNAUTHENTICATED`
- `403` `FORBIDDEN` / `TENANT_MISMATCH`
- `404` `COST_CENTER_NOT_FOUND`
- `409` `COST_CENTER_CODE_CONFLICT`, `CONCURRENCY_CONFLICT`, `COST_CENTER_IN_USE`
- `422` `COST_CENTER_ACCOUNT_POLICY_VIOLATION` (si aplica política contable)

---

## 11) Criterios de aceptación (backend)
1. Puedo crear, consultar, actualizar y activar/inactivar centros de costo por empresa.
2. La API garantiza unicidad de código por tenant.
3. `OrgUnits` y `PositionSlots` pueden validar referencia a centros de costo activos.
4. No se puede inactivar un centro de costo en uso activo.
5. Toda operación relevante queda auditada.
6. La API rechaza operaciones cross-tenant.
7. La API valida concurrencia en actualizaciones.
8. Puedo exportar catálogo en `csv|xlsx`.

---

## 12) Plan de pruebas

### Unit tests
- normalización de `code/name`
- unicidad de código por tenant
- reglas de inactivación con uso activo
- refresh de `ConcurrencyToken`

### Integration tests
- flujo feliz CRUD + activate/inactivate + export
- `409` por código duplicado
- `409` por inactivar en uso
- `403` por tenant mismatch
- `403` por falta de permisos
- auditoría de escrituras

### Validación final
- `dotnet build CLARIHR.slnx`
- `dotnet test CLARIHR.slnx --no-build`
