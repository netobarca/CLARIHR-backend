# Security Analysis

## Summary

HU-013 cumple los controles de seguridad definidos para el alcance actual.

## Confirmed Controls

- Todos los endpoints de PositionSlots requieren autenticacion JWT.
- El backend valida coincidencia entre `companyId` de ruta y claim `tid`.
- Operaciones por `{id}` distinguen `NotFound` vs `TenantMismatch` usando `ExistsOutsideTenantAsync` con `IgnoreQueryFilters`.
- Permisos funcionales separados por lectura y escritura:
  - `PositionSlots.Read`
  - `PositionSlots.Admin`
  - overrides: `iam.administration.manage`, `platform_admin`
- Concurrencia optimista validada por `ConcurrencyToken` en escrituras por id.
- Auditoria activa en create/update/status/dependencies/occupancy con `before/after`.

## Residual Risk

- PositionSlots usa autorizacion funcional por claims/roles y no matriz RBAC de campo; suficiente para v1, pero sin granularidad por propiedad sensible.
- Exportes de diagrama/listado comparten permisos de lectura generales; no existe todavia segmentacion por columnas sensibles.
- No hay version historica de ocupacion por empleado en v1 (solo conteo), por lo que trazabilidad fina operativa queda para iteracion posterior.
