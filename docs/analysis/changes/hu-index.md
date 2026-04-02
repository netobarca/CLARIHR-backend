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
| HU-BILL-007 | Mantener estados de suscripcion | Platform / Billing / Company Subscriptions | Implementada | 2026-04-02 | `docs/analysis/changes/HU-BILL-007.md` | `docs/business/current-system-business-flows.md`, `docs/analysis/current-state/security-analysis.md`, `docs/analysis/current-state/performance-analysis.md`, `docs/analysis/current-state/testing-analysis.md`, `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml` | Formaliza el ciclo de vida de suscripciones con historial de transiciones, cambios manuales auditados, vencimiento automatico por fecha y capacidades `canOperate`/`canGenerateCharges`. |
| HU-BILL-006 | Activar una suscripcion para una empresa | Platform / Billing / Company Subscriptions | Implementada | 2026-04-02 | `docs/analysis/changes/HU-BILL-006.md` | `docs/business/current-system-business-flows.md`, `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml` | Formaliza la activacion inmediata o programada de suscripciones empresariales, agrega `CommercialPlanVersion`, `Company.IsBillable`, preview de activacion, listado global y promocion automatica de filas `Scheduled`. |
| HU-BILL-004 | Add-ons especializados en el catálogo comercial global | Platform / Billing / Commercial Catalog | Implementada | 2026-03-30 | `docs/analysis/changes/HU-BILL-004.md` | `docs/business/current-system-business-flows.md`, `docs/analysis/current-state/architecture-analysis.md`, `docs/analysis/current-state/security-analysis.md`, `docs/analysis/current-state/performance-analysis.md`, `docs/analysis/current-state/testing-analysis.md`, `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml` | Generaliza `CommercialAddon` para soportar pricing `Massive` y `Specialized`, agrega `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity` y migra el catalogo existente sin crear un recurso paralelo. |
| HU-2026-03-30-02 | OrgUnits expone referencia compacta del padre en lecturas | Core API / OrgUnits | Implementada | 2026-03-30 | `docs/analysis/changes/HU-2026-03-30-02.md` | `docs/technical/api/endpoint-reference.md` | Enriquece `OrgUnitResponse` con `parent { publicId, code, normalizedCode, name }`, elimina `parentPublicId` del response y reutiliza el mismo `LEFT JOIN` ya existente sin agregar queries extra por fila. |
| HU-2026-03-30-01 | Catalogo de add-ons masivos en Backoffice | Platform / Billing / Commercial Catalog | Implementada | 2026-03-30 | `docs/analysis/changes/HU-2026-03-30-01.md` | `docs/business/current-system-business-flows.md`, `docs/analysis/current-state/architecture-analysis.md`, `docs/analysis/current-state/security-analysis.md`, `docs/analysis/current-state/performance-analysis.md`, `docs/analysis/current-state/testing-analysis.md`, `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml` | Agrega `CommercialAddon` como catalogo global de add-ons masivos en Backoffice, con auditoria durable, migracion propia y sin exponer superficie nueva en la Core API. |
| HU-2026-03-29-01 | Separación de suscripciones y Backoffice API de plataforma | Platform / Subscriptions / Auth | Implementada | 2026-03-29 | `docs/analysis/changes/HU-2026-03-29-01.md` | `docs/business/current-system-business-flows.md`, `docs/analysis/current-state/architecture-analysis.md`, `docs/analysis/current-state/security-analysis.md`, `docs/analysis/current-state/testing-analysis.md`, `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml` | Separa el backoffice global del core de RH, elimina la elevación por allow-list, formaliza la relación de suscripciones con `CommercialPlan` y agrega `PlatformAuditLog`. |
| HU-2026-03-26-01 | Catalogo comercial de planes | Companies / Subscriptions | Implementada | 2026-03-26 | `docs/analysis/changes/HU-2026-03-26-01.md` | `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml`, `docs/business/current-system-business-flows.md`, `docs/analysis/current-state/architecture-analysis.md`, `docs/analysis/current-state/security-analysis.md` | Agrega el catalogo global `CommercialPlan`, el seed protegido `FREE` y endpoints `platform_admin` sin tenant para administrar planes comerciales. |
| HU-2026-03-24-01 | Agregar rol tenant-scoped al JWT | Auth / Account companies | Implementada | 2026-03-24 | `docs/analysis/changes/HU-2026-03-24-01.md` | `docs/technical/api/endpoint-reference.md`, `docs/business/current-system-business-flows.md` | El `access token` ahora incluye claim `role` con el nombre normalizado del rol resuelto para el tenant emitido. |
| HU-2026-03-19-01 | IsPrimary opcional para representante legal inicial | Account companies / Legal representatives | Implementada | 2026-03-19 | `docs/analysis/changes/HU-2026-03-19-01.md` | `docs/technical/api/endpoint-reference.md`, `docs/technical/api/openapi.yaml` | `InitialLegalRepresentativeInput.IsPrimary` pasa a nullable y se persiste con migracion EF. |

## 6. Regla de mantenimiento

Actualizar este archivo cuando:

- se agregue una HU nueva
- cambie el estado de una HU
- cambie el archivo de cambio asociado
- cambie el conjunto de documentos vivos actualizados
- exista una observacion breve relevante para seguimiento
