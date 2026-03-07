# Security Analysis

## Scope

Analisis de seguridad para HU-015 (`CostCenters`) sobre baseline HU-014.

## Findings

1. Tenant-scope estricto:
   - endpoints por `companyId` validan coincidencia con claim `tid`
   - operaciones por `id` distinguen `NotFound` vs `TenantMismatch` con `IgnoreQueryFilters`.
2. Autorizacion funcional aplicada por modulo:
   - lectura: `CostCenters.Read|CostCenters.Admin|iam.administration.manage|platform_admin`
   - escritura: `CostCenters.Admin|iam.administration.manage|platform_admin`
3. Concurrencia optimista:
   - operaciones `PUT/PATCH` exigen `ConcurrencyToken`.
4. Auditoria:
   - eventos de create/update/activate/inactivate con `before/after` en transaccion UoW.
5. Integracion segura:
   - `OrgUnits` y `PositionSlots` rechazan `costCenterCode` inexistente/inactivo en nuevas escrituras.

## Risks

- Datos preexistentes legacy pueden conservar codigos no vigentes hasta su correccion funcional.

## Conclusion

HU-015 mantiene el nivel de seguridad esperado para la plataforma en esta iteracion.
