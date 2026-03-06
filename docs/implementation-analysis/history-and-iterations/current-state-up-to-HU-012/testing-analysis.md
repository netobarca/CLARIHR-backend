# Testing Analysis

## Summary

HU-011 y HU-012 quedan cubiertas por pruebas unitarias y de integracion HTTP reales.

## Covered Scenarios

- OrgUnits:
  - create, update, move, activate, inactivate
  - conflicto por `ConcurrencyToken` stale
  - bloqueo por ciclo y por hijos activos
  - tenant mismatch y falta de permisos
  - auditoria de creacion
- JobProfiles/JobCatalogs:
  - flujo create/update/publish/get/print/export
  - conflicto por codigo duplicado
  - conflicto por ciclo en dependencias
  - conflicto por `ConcurrencyToken` stale
  - tenant mismatch y falta de permisos
  - creacion inline de catalogo con y sin permiso
  - auditoria de create de perfil
- Cobertura unitaria de reglas de dominio:
  - normalizacion de code/name/title
  - refresco de `ConcurrencyToken`
  - transiciones de estado y validaciones de publicacion
  - detectores de ciclo (org chart y dependencias de perfiles)

## Validation Run

- `dotnet build CLARIHR.slnx` -> success
- `dotnet test CLARIHR.slnx --no-build` -> success
  - `CLARIHR.Application.UnitTests`: 114 passed
  - `CLARIHR.Api.IntegrationTests`: 67 passed

## Remaining Gap

- no hay pruebas de carga/stress para arboles organizativos grandes o perfiles con alta cardinalidad de secciones.
