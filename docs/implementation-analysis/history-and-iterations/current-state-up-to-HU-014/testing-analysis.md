# Testing Analysis

## Summary

HU-014 queda cubierta por pruebas unitarias y de integracion HTTP.

## Covered Scenarios

- Dominio:
  - normalizacion y versionado de lineas de tabulador
  - transiciones de estado de solicitudes
  - bloqueo de autoaprobacion
- Integracion API:
  - flujo feliz completo create/update/submit/approve/apply/export
  - `422` por autoaprobacion
  - `409` por `ConcurrencyToken` stale
  - `403` tenant mismatch y falta de permisos
  - verificacion de auditoria en aprobacion

## Validation Run

- `dotnet build CLARIHR.slnx` -> success
- `dotnet test CLARIHR.slnx --no-build` -> success
  - `CLARIHR.Application.UnitTests`: 123 passed
  - `CLARIHR.Api.IntegrationTests`: 81 passed

## Remaining Gap

- no hay pruebas de carga para exportes masivos y escenarios de alta concurrencia de aprobaciones.
