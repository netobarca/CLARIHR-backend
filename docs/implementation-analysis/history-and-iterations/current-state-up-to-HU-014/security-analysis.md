# Security Analysis

## Summary

HU-014 cumple los controles de seguridad definidos para el alcance actual.

## Confirmed Controls

- Todos los endpoints de SalaryTabulator requieren JWT.
- Se valida coincidencia `companyId` vs claim `tid`.
- Operaciones por `{id}` distinguen `NotFound` vs `TenantMismatch` usando `IgnoreQueryFilters`.
- Permisos funcionales separados por lectura, solicitud y aprobacion:
  - `SalaryTabulator.Read`
  - `SalaryTabulator.Request`
  - `SalaryTabulator.Approve`
  - `SalaryTabulator.Admin`
  - overrides: `iam.administration.manage`, `platform_admin`
- Autoaprobacion bloqueada por defecto (politica de segregacion de funciones).
- Auditoria de lifecycle de solicitudes y aplicacion/inactivacion de lineas.

## Residual Risk

- El modelo usa autorizacion funcional por claims/roles y no RBAC fino por campo.
- Reglas legales/locales de compensacion por pais no se aplican en v1.
- Integracion externa con nomina/ERP aun no implementada.
