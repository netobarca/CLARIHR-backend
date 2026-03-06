# Security Analysis

## Summary

HU-011 y HU-012 cumplen los controles de seguridad del alcance actual.

## Confirmed Controls

- Todos los endpoints de OrgUnits/JobProfiles/JobCatalogs requieren autenticacion JWT.
- El backend valida coincidencia entre `companyId` de ruta y claim `tid`.
- Operaciones por `{id}` distinguen `NotFound` vs `TenantMismatch` usando `ExistsOutsideTenantAsync` con `IgnoreQueryFilters`.
- Permisos funcionales separados por lectura y escritura:
  - OrgUnits: `OrgUnits.Read`, `OrgUnits.Admin`
  - JobProfiles: `JobProfiles.Read`, `JobProfiles.Admin`
  - JobCatalogs: `JobCatalogs.Admin`
  - overrides: `iam.administration.manage`, `platform_admin`
- Concurrencia optimista validada en operaciones de escritura via `ConcurrencyToken`.
- Auditoria activa en create/update/move/activate/inactivate/publish/archive/catalog updates con `before/after`.

## Residual Risk

- OrgUnits y JobProfiles usan autorizacion funcional por claims/roles, no la matriz RBAC fina de HU-005/HU-006 por recurso/campo.
- Los bloques salariales y de valuacion en HU-012 no aplican todavia controles de campo granulares.
- La creacion inline de catalogos incrementa superficie de escritura y debe mantenerse cubierta por pruebas de autorizacion negativa.
