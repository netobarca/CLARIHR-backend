# HU-0013 — Administración de Plazas y Dependencias (Dotación, Ocupación y Organigrama de Plazas)

## 1) Descripción del requerimiento (visión de negocio)
Como **Dueño de la cuenta / Administrador de Empresa / RRHH**,
quiero **definir y administrar las plazas de la empresa**,
registrando para cada plaza su puesto base, unidad organizativa, centro de trabajo, centro de costo, dependencia directa y funcional, estado de ocupación, capacidad máxima, vigencia y modalidad de tiempo definido,
para que el sistema tenga una **fuente única, auditable y reutilizable** de la estructura real de dotación y su cadena de dependencias.

Resultado esperado:
- Cada **puesto** (`JobProfile`) puede relacionarse con **múltiples plazas** (1:N).
- El sistema puede generar el **diagrama de dependencias de plazas** y exportarlo en formato interoperable con herramientas de diagramación.
- El sistema puede generar la **lista de plazas en Excel** para análisis operativo y reporting.

---

## 2) Objetivo funcional (qué habilita)
- Separar claramente:
  - definición del puesto (`JobProfile`)
  - dotación operativa real (`PositionSlot` / Plaza)
- Dar trazabilidad del ciclo de vida de una plaza:
  - vacante
  - ocupada
  - suspendida
- Mejorar planeación de headcount y cobertura de vacantes.
- Consumir la estructura de plazas para módulos futuros:
  - reclutamiento
  - movimientos internos
  - analítica de talento
  - planeación presupuestaria

---

## 3) Alcance API (backend)
### Incluye
- CRUD tenant-scoped de plazas (`PositionSlot`) por empresa.
- Relación obligatoria de plaza con `JobProfile` (1 puesto -> N plazas).
- Referencias de contexto organizativo:
  - `OrgUnitId`
  - `WorkCenterId`
  - `CostCenterCode`
- Dependencias:
  - directa (jerárquica)
  - funcional (matricial)
- Gestión de estado de plaza: `Vacant | Occupied | Suspended`.
- Gestión de capacidad (`MaxEmployees`) y ocupación actual (`OccupiedEmployees`).
- Consultas:
  - listado paginado con filtros
  - detalle por plaza
  - grafo de dependencias (nodes/edges)
  - export de diagrama
  - export de listado a Excel (`.xlsx`)

### Fuera de alcance (por ahora, pero preparado)
- Sincronización automática con nómina externa.
- Validaciones de presupuesto financiero en tiempo real.
- Simulación avanzada de escenarios de reestructuración.
- Exportación a todos los formatos de BI (se deja preparado para iteración posterior).

---

## 4) Actores y permisos
- **AccountOwner / Dueño de la cuenta**: administración total de plazas en su empresa activa.
- **CompanyAdmin / Admin de Empresa**: administración funcional completa.
- **HRAdmin / HR Specialist**: crea/edita/suspende/reactiva plazas.
- **HRRead / HiringManagerRead**: solo consulta/exporta.

Permisos sugeridos (patrón funcional actual):
- `PositionSlots.Read`
- `PositionSlots.Admin`
- `iam.administration.manage` (override administrativo)
- `platform_admin` (override plataforma)

Regla base:
- La API valida siempre coincidencia entre `companyId` de la ruta y claim `tid`.

---

## 5) Datos que debe manejar el backend (modelo mínimo)

### 5.1 Entidad principal: `PositionSlot` (Plaza)
Campos sugeridos:
- `Id` (GUID público)
- `CompanyId` (GUID, obligatorio)
- `Code` (string, único por empresa, obligatorio)
- `Title` (string opcional, fallback del nombre del `JobProfile`)
- `JobProfileId` (GUID, obligatorio)
- `OrgUnitId` (GUID, obligatorio)
- `WorkCenterId` (GUID, opcional)
- `CostCenterCode` (string, opcional)
- `DirectDependencyPositionSlotId` (GUID, opcional)
- `FunctionalDependencyPositionSlotId` (GUID, opcional)
- `Status` (enum): `Vacant | Occupied | Suspended`
- `MaxEmployees` (int, obligatorio, `>= 1`)
- `OccupiedEmployees` (int, obligatorio, `>= 0`)
- `IsFixedTerm` (bool)
- `EffectiveFromUtc` (date, obligatorio)
- `EffectiveToUtc` (date, opcional)
- `Notes` (string, opcional)
- `IsActive` (bool)
- `CreatedAtUtc`, `UpdatedAtUtc`
- `ConcurrencyToken` (GUID)

### 5.2 Entidad de ocupación (recomendado para trazabilidad): `PositionSlotOccupancy`
- `Id` (GUID público)
- `PositionSlotId` (GUID)
- `EmployeeId` (GUID o referencia externa)
- `StartedAtUtc`
- `EndedAtUtc` (nullable)
- `OccupancyType` (enum): `Primary | Interim | Temporary`
- `Notes`

> Si el módulo de empleados aún no está operativo, `EmployeeId` puede aceptar referencia externa estable y luego migrar a FK fuerte.

### 5.3 Vistas de proyección
- `PositionSlotListItemResponse` para listados rápidos.
- `PositionSlotGraphResponse` para diagramación (`nodes` + `edges`).
- `PositionSlotExportRow` para export a Excel.

---

## 6) Reglas de negocio (backend)

### RN-01 Relación puesto-plaza (1:N)
- Una plaza debe referenciar un `JobProfile` válido y del mismo tenant.
- Un `JobProfile` puede tener múltiples plazas activas.

### RN-02 Consistencia organizativa
- `OrgUnitId` y `WorkCenterId` deben pertenecer al mismo tenant.
- Si `WorkCenterId` existe, debe ser compatible con la `OrgUnit` (regla configurable según estructura vigente).

### RN-03 Unicidad y estado
- `Code` de plaza es único por empresa.
- Estados permitidos: `Vacant`, `Occupied`, `Suspended`.

### RN-04 Capacidad y ocupación
- `MaxEmployees >= 1`.
- `0 <= OccupiedEmployees <= MaxEmployees`.
- Si `OccupiedEmployees = 0` y no está suspendida -> `Vacant`.
- Si `OccupiedEmployees > 0` y no está suspendida -> `Occupied`.
- `Suspended` bloquea nuevas asignaciones y movimientos de ocupación.

### RN-05 Dependencias de plazas
- `DirectDependencyPositionSlotId` y `FunctionalDependencyPositionSlotId` no pueden apuntar a sí misma.
- No permitir ciclos en dependencia directa (`A -> B -> A`).
- Dependencia funcional puede coexistir con la directa, manteniendo validación anti-autorreferencia y anti-ciclo configurable.

### RN-06 Vigencia
- `EffectiveFromUtc` obligatorio.
- `EffectiveToUtc` opcional, pero si existe debe ser `>= EffectiveFromUtc`.
- Si la plaza está vencida (`EffectiveToUtc < now`) no puede pasar a `Occupied` sin reactivar vigencia.

### RN-07 Concurrencia y auditoría
- Toda operación de escritura valida `ConcurrencyToken`.
- Toda escritura registra auditoría `before/after`.

### RN-08 Seguridad tenant-scoped
- Toda operación por `{id}` distingue `NotFound` vs `TenantMismatch` con `ExistsOutsideTenantAsync` + `IgnoreQueryFilters`.

---

## 7) Requerimientos técnicos del Backend (arquitectura + persistencia)

### RT-01 Arquitectura
- Mantener patrón actual:
  - Clean Architecture
  - CQRS (`Commands/Queries + Handlers`)
  - FluentValidation
  - `ProblemDetails`
  - tenant-scoped por `tid`

### RT-02 Persistencia (PostgreSQL)
Tablas sugeridas:
- `position_slots`
- `position_slot_occupancies` (si se incluye trazabilidad de ocupación)

Índices recomendados:
- `uq_position_slots__tenant_code`
- `ix_position_slots__tenant_status`
- `ix_position_slots__tenant_job_profile`
- `ix_position_slots__tenant_org_unit`
- `ix_position_slots__tenant_work_center`
- `ix_position_slots__tenant_direct_dependency`
- `ix_position_slots__tenant_functional_dependency`
- `ix_position_slot_occupancies__tenant_slot_active`

### RT-03 Rendimiento
- Listados paginados obligatorios (`page`, `pageSize`).
- Filtros por `status`, `jobProfileId`, `orgUnitId`, `workCenterId`, `isFixedTerm`, `q`.
- Grafo generado desde una sola lectura del subconjunto (`rootId` + `depth`) y armado en memoria.
- Export Excel construido desde proyección plana para evitar N+1.

### RT-04 Seguridad y autorización
- Servicio `IPositionSlotAuthorizationService` (patrón de `OrgUnitAuthorizationService`).
- Lectura: `PositionSlots.Read`, `PositionSlots.Admin`, `iam.administration.manage`, `platform_admin`.
- Escritura: `PositionSlots.Admin`, `iam.administration.manage`, `platform_admin`.

### RT-05 Auditoría
Eventos mínimos sugeridos:
- `POSITION_SLOT_CREATED`
- `POSITION_SLOT_UPDATED`
- `POSITION_SLOT_STATUS_CHANGED`
- `POSITION_SLOT_MOVED`
- `POSITION_SLOT_DEPENDENCY_UPDATED`
- `POSITION_SLOT_OCCUPANCY_CHANGED`

---

## 8) Endpoints propuestos (API v1)

### 8.1 Plazas
- `POST   /api/v1/companies/{companyId}/position-slots`
- `GET    /api/v1/companies/{companyId}/position-slots?status=&jobProfileId=&orgUnitId=&workCenterId=&isFixedTerm=&q=&page=&pageSize=`
- `GET    /api/v1/position-slots/{id}`
- `PUT    /api/v1/position-slots/{id}`
- `PATCH  /api/v1/position-slots/{id}/status`
- `PATCH  /api/v1/position-slots/{id}/dependencies`
- `PATCH  /api/v1/position-slots/{id}/occupancy`

### 8.2 Diagrama de dependencias
- `GET    /api/v1/companies/{companyId}/position-slots/graph?rootId=&depth=&includeFunctional=`
- `GET    /api/v1/companies/{companyId}/position-slots/diagram-export?format=graphml|json|dot&rootId=&depth=`

### 8.3 Exportación de listado
- `GET    /api/v1/companies/{companyId}/position-slots/export?format=xlsx|csv&status=&jobProfileId=&orgUnitId=&workCenterId=&isFixedTerm=&q=`

---

## 9) Contratos (DTOs) mínimos

### 9.1 `CreatePositionSlotRequest`
- `code`*
- `title`
- `jobProfileId`*
- `orgUnitId`*
- `workCenterId`
- `costCenterCode`
- `directDependencyPositionSlotId`
- `functionalDependencyPositionSlotId`
- `maxEmployees`*
- `occupiedEmployees`
- `isFixedTerm`*
- `effectiveFromUtc`*
- `effectiveToUtc`
- `notes`

### 9.2 `UpdatePositionSlotRequest`
- mismo contrato base de create + `concurrencyToken`*

### 9.3 `UpdatePositionSlotStatusRequest`
- `status`* (`Vacant|Occupied|Suspended`)
- `concurrencyToken`*

### 9.4 `UpdatePositionSlotDependenciesRequest`
- `directDependencyPositionSlotId`
- `functionalDependencyPositionSlotId`
- `concurrencyToken`*

### 9.5 `UpdatePositionSlotOccupancyRequest`
- `occupiedEmployees`*
- `concurrencyToken`*

### 9.6 `PositionSlotResponse`
- `id`, `companyId`, `code`, `title`, `status`
- `jobProfileId`, `orgUnitId`, `workCenterId`, `costCenterCode`
- `directDependencyPositionSlotId`, `functionalDependencyPositionSlotId`
- `maxEmployees`, `occupiedEmployees`
- `isFixedTerm`, `effectiveFromUtc`, `effectiveToUtc`
- `notes`, `isActive`, `createdAtUtc`, `updatedAtUtc`, `concurrencyToken`

### 9.7 `PositionSlotGraphResponse`
- `nodes[]`: `{ id, code, label, status, orgUnitId, workCenterId }`
- `edges[]`: `{ fromId, toId, relationType: Direct|Functional }`

---

## 10) Errores y códigos recomendados
- `400` `VALIDATION_ERROR`
- `401` `UNAUTHENTICATED`
- `403` `FORBIDDEN` / `TENANT_MISMATCH`
- `404` `POSITION_SLOT_NOT_FOUND`
- `409` `POSITION_SLOT_CODE_CONFLICT`, `CONCURRENCY_CONFLICT`, `POSITION_SLOT_DEPENDENCY_CYCLE`, `POSITION_SLOT_STATUS_CONFLICT`
- `422` `POSITION_SLOT_CAPACITY_RULE_VIOLATION`, `POSITION_SLOT_EFFECTIVE_DATES_INVALID`

---

## 11) Criterios de aceptación (backend)
1. Puedo crear y actualizar plazas vinculadas a un `JobProfile` del mismo tenant.
2. Puedo relacionar una plaza con `OrgUnit`, `WorkCenter` y centro de costo.
3. Puedo registrar dependencia directa y funcional sin ciclos inválidos.
4. El sistema respeta la relación 1 puesto -> N plazas.
5. Puedo gestionar estado `Vacant`, `Occupied`, `Suspended` con reglas de capacidad.
6. Puedo consultar plazas con filtros y paginación.
7. Puedo obtener el diagrama de dependencias y exportarlo en formato interoperable (`graphml/json/dot`).
8. Puedo exportar la lista de plazas en Excel (`.xlsx`).
9. Toda operación relevante queda auditada.
10. La API rechaza operaciones cross-tenant.
11. La API valida concurrencia en operaciones de escritura.

---

## 12) Recomendaciones de implementación (mejores prácticas HRIS)
- Implementar por fases para reducir riesgo:
  - **Fase 1:** núcleo de plazas + dependencias + estado/capacidad + auditoría + export `xlsx`.
  - **Fase 2:** historial detallado de ocupación por empleado + analítica avanzada de cobertura.
- Mantener separación entre:
  - estructura (`JobProfile`, `OrgUnit`)
  - operación de dotación (`PositionSlot`)
- No derivar decisiones críticas desde frontend; toda regla de dependencia/capacidad debe validarse en backend.
- Diseñar exportación con contratos estables para consumo de herramientas externas.
- Preparar integración futura con:
  - reclutamiento/vacantes
  - movimientos internos
  - compensaciones y presupuesto
  - analítica de headcount

## 13) Supuestos y decisiones cerradas
- Esta HU se define para CLARIHR de forma tenant-scoped; no está acoplada a ANDA.
- Se aprovechan módulos ya existentes:
  - `JobProfiles` (HU-0012)
  - `OrgUnits` (HU-0011)
  - `WorkCenters` (HU-0010)
- `tid` representa la empresa activa (modelo vigente).
- V1 requiere export Excel (`xlsx`) y export de diagrama interoperable (`graphml/json/dot`).
- Si el módulo de empleados no está habilitado aún, la ocupación se maneja inicialmente por conteo (`OccupiedEmployees`) y referencia extensible.

## 14) Plan de pruebas (backend)
### Unit tests (`tests/CLARIHR.Application.UnitTests`)
- Dominio:
  - normalización de `code`
  - reglas de estado (`Vacant|Occupied|Suspended`)
  - validación de capacidad (`occupied <= max`)
  - reglas de vigencia (`effectiveFrom <= effectiveTo`)
- Reglas de dependencia:
  - bloqueo de autoreferencia
  - bloqueo de ciclo en dependencia directa
- Concurrencia:
  - refresh de `ConcurrencyToken` en updates válidos

### Integration tests (`tests/CLARIHR.Api.IntegrationTests`)
- Flujo feliz:
  - create -> list -> get -> update -> status change -> graph -> export (`xlsx`)
- Seguridad:
  - `403` por `companyId != tid`
  - `403` por falta de permisos
- Conflictos:
  - `409` por código duplicado
  - `409` por ciclo en dependencia
  - `409` por `ConcurrencyToken` stale
  - `422` por capacidad o vigencia inválida
- Auditoría:
  - validación de eventos de escritura en `audit_logs`

### Verificación final
- `dotnet build CLARIHR.slnx`
- `dotnet test CLARIHR.slnx --no-build`
