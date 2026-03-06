# Architecture Analysis

## Summary

HU-011 y HU-012 mantienen la linea arquitectonica actual:

- entidades y reglas de negocio en `Domain`
- contratos, CQRS, validadores y orquestacion en `Application`
- repositorios EF, autorizacion funcional y persistencia en `Infrastructure`
- controladores versionados `/api/v1` en `Api`

## What Went Well

- OrgUnits y JobProfiles nacieron como modulos separados y tenant-scoped.
- Se mantuvo el patron de handlers con transaccion UoW + auditoria `before/after`.
- Los contratos de API son consistentes con `ProblemDetails` y codigos de error del repositorio.
- La incorporacion de catalogos de puesto no afecto el modulo Locations ni IAM RBAC existente.
- SQL incremental por HU (`hu011_org_units.sql`, `hu012_job_profiles.sql`) sin romper scripts previos.

## Controlled Debt

- `JobProfileAdministration.cs` concentra mucha logica; conviene partir por casos de uso para mejorar mantenibilidad.
- El modelo de catalogos de HU-012 es modulo-especifico (`JobCatalogItem`); reusable hoy, pero aun no extraido como componente transversal.
- `JobProfilesController` incluye logica de export CSV; funcional para v1, pero conviene moverla a capa de aplicacion/servicio dedicado si crece.
