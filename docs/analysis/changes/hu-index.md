# Indice de historias de usuario y cambios implementados

## 1. Proposito

Este archivo centraliza la trazabilidad de cambios puntuales por HU o requerimiento ya trabajados en el proyecto.

## 2. Estado actual

La documentacion inicial del proyecto ya esta creada y este indice ya registra los cambios puntuales que se han ido cerrando.

Cada nuevo requerimiento implementado debe agregarse aqui y, si aplica, dejar su archivo `HU-XXXX.md` asociado.

## 3. Convenciones

- usar un codigo consistente del tipo `HU-001`, `HU-002` o la convencion oficial que adopte el equipo
- mantener una fila por HU o requerimiento
- listar solo documentos vivos realmente impactados
- no duplicar aqui analisis extensos

## 4. Estados sugeridos

- Pendiente
- En analisis
- En implementacion
- En validacion
- Implementada
- Cerrada
- Bloqueada
- Descartada

## 5. Indice maestro

| Codigo HU | Titulo | Modulo | Estado | Fecha ultima actualizacion | Archivo de cambio | Documentos vivos actualizados | Observaciones |
|---|---|---|---|---|---|---|---|
| HU-2026-03-24-01 | Agregar rol tenant-scoped al JWT | Auth / Account companies | Implementada | 2026-03-24 | `docs/analysis/changes/HU-2026-03-24-01.md` | `docs/technical/api/endpoint-reference.md`, `docs/business/current-system-business-flows.md` | El `access token` ahora incluye claim `role` con el nombre normalizado del rol resuelto para el tenant emitido. |
| HU-2026-03-19-01 | IsPrimary opcional para representante legal inicial | Account companies / Legal representatives | Implementada | 2026-03-19 | `docs/analysis/changes/HU-2026-03-19-01.md` | `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml` | `InitialLegalRepresentativeInput.IsPrimary` pasa a nullable y se persiste con migracion EF. |

## 6. Regla de mantenimiento

Actualizar este archivo cuando:

- se agregue una HU nueva
- cambie el estado de una HU
- cambie el archivo de cambio asociado
- cambie el conjunto de documentos vivos actualizados
- exista una observacion breve relevante para seguimiento
