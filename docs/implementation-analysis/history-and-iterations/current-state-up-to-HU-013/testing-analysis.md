# Testing Analysis

## Summary

HU-013 queda cubierta por pruebas unitarias y de integracion HTTP.

## Covered Scenarios

- Dominio `PositionSlot`:
  - normalizacion de codigo
  - transiciones de estado y ocupacion
  - bloqueo de ocupacion en `Suspended`
  - deteccion de ciclo directo
- Integracion API:
  - flujo feliz completo:
    - create, list, get, update
    - dependencies, occupancy, status
    - graph, diagram-export, export
  - conflictos:
    - `CONCURRENCY_CONFLICT`
    - `POSITION_SLOT_CODE_CONFLICT`
    - `POSITION_SLOT_DEPENDENCY_CYCLE`
    - `POSITION_SLOT_CAPACITY_RULE_VIOLATION` (`422`)
  - seguridad:
    - tenant mismatch (`403`)
    - falta de permisos (`403`)
  - auditoria:
    - evento `POSITION_SLOT_CREATED` visible en `audit_logs`

## Validation Run

- `dotnet build CLARIHR.slnx` -> success
- `dotnet test CLARIHR.slnx` -> success
  - `CLARIHR.Application.UnitTests`: 119 passed
  - `CLARIHR.Api.IntegrationTests`: 75 passed

## Remaining Gap

- no hay pruebas de carga para grafos de dependencias grandes ni exportes masivos.
