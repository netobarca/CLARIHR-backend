# Security Analysis

## Summary

El modulo HU-010 cumple con los requisitos de seguridad del alcance actual.

## Confirmed Controls

- Todos los endpoints de Locations requieren autenticacion.
- El backend valida `companyId` de ruta contra `tid`.
- Los accesos por `{id}` resuelven entidad tenant-scoped y devuelven `TENANT_MISMATCH` cuando aplica.
- No hay hard delete en grupos, tipos ni centros.
- El grupo default `GENERAL` queda protegido.
- Los cambios relevantes generan auditoria.

## Residual Risk

- La autorizacion del modulo usa claims funcionales (`Locations.Read`, `Locations.Admin`) y `iam.administration.manage`, no la matriz RBAC completa del sistema. Para el alcance actual es suficiente, pero si Locations requiere futuro control por recurso/accion/campo, habrá que integrarlo al catálogo RBAC formal.
