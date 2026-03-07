# Remediation Plan

## Status

No se detecto un blocker critico nuevo de arquitectura, seguridad o testing introducido por HU-013.

## Follow-up Candidates (Non-Infrastructure)

1. Partir `PositionSlotAdministration.cs` por casos de uso para reducir complejidad y facilitar mantenimiento.
2. Extraer generacion de archivos (`csv/xlsx/graphml/dot`) desde `PositionSlotsController` a servicios de aplicacion reutilizables.
3. Agregar pruebas de integracion negativas adicionales para `diagram-export` y `export` con formatos invalidos y `rootId` fuera de tenant.
4. Extender cobertura de auditoria para validar eventos de `status`, `dependencies` y `occupancy` (no solo create).
5. Evaluar reglas de consistencia cruzada entre `OrgUnit` y `WorkCenter` para futuras validaciones funcionales avanzadas.
