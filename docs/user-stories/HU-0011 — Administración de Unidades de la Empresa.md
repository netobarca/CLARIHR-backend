# HU-0011 — Administración de Unidades de la Empresa (Estructura Organizativa / Organigrama)

## 1) Descripción del requerimiento (visión de negocio)
Como **Dueño de la cuenta / Administrador de Empresa**,
quiero **definir las unidades organizativas** de cada empresa (Direcciones, Gerencias, Departamentos, Coordinaciones, Unidades),
estableciendo para cada una su **jerarquía (padre-hijo)**, su **dependencia** y su **estado**,
para que el sistema pueda **representar el organigrama** y consumirlo en **modo lista** y **modo gráfico**.

> Nota: “jerarquía” y “dependencia” se traducen técnicamente en una estructura jerárquica (árbol) donde cada unidad tiene un `ParentUnitId` (dependencia) y una `Level` o `Depth` (jerarquía). El “organigrama” es la visualización de esa estructura.

---

## 2) Objetivo funcional (qué habilita)
- Definir la **estructura formal** de la empresa (cómo se organiza).
- Habilitar:
  - Reportes por unidad
  - Permisos/visibilidad por unidad (seguridad de contenido)
  - Flujos de aprobación por cadena jerárquica
  - Asignación de empleados a unidad (cuando exista ese módulo)
  - Visualización del organigrama en **lista** y **gráfico**.

---

## 3) Alcance API (backend)
### Incluye
- CRUD de unidades organizativas por empresa.
- Manejo de jerarquía (crear raíz, crear hijas, mover nodo).
- Estado de la unidad (Activa/Inactiva).
- Consultas:
  - listado plano (modo lista)
  - árbol jerárquico (modo gráfico o lista jerárquica)
  - grafo (nodes/edges) para organigrama visual.

### Fuera de alcance (por ahora, pero se deja preparado)
- Asignación de empleados a unidades (solo se deja la base para referenciar).
- Puestos/plazas dentro de unidades (otro módulo).
- Versionado histórico de organigrama por fecha (puede agregarse luego si ANDA/cliente lo exige).

---

## 4) Actores y permisos
- **AccountOwner / Dueño de la cuenta**: puede administrar unidades de cualquier empresa del tenant.
- **CompanyAdmin / Admin de Empresa**: administra unidades de su empresa.
- **OrgStructureAdmin**: administra unidades (rol específico).
- **OrgStructureRead**: consulta/visualiza.

Regla base:
- La API valida que el usuario solo opere sobre `CompanyId` permitido por sus claims/roles.

---

## 5) Datos que debe manejar el backend (modelo mínimo)

### Entidad: `OrgUnit` (Unidad Organizativa)
Campos sugeridos:
- `Id` (GUID)
- `CompanyId` (GUID) **obligatorio**
- `Code` (string, único por empresa) **obligatorio**
- `Name` (string) **obligatorio**
- `UnitType` (enum o catálogo): `Direccion | Gerencia | Departamento | Coordinacion | Unidad | Otro`
- `ParentUnitId` (GUID, nullable)  
  - `null` = unidad raíz
- `SortOrder` (int, opcional) para ordenar en UI
- `IsActive` (bool)
- `Description` (string, opcional)
- `CostCenterCode` (string, opcional) si el negocio lo usa para finanzas/planilla
- `ManagerEmployeeId` (GUID, opcional) si luego se enlaza jefatura directa
- Auditoría:
  - `CreatedAt`, `CreatedBy`
  - `UpdatedAt`, `UpdatedBy`
- Concurrencia:
  - `RowVersion` (rowversion en SQL Server)

### Catálogo opcional (si quieres que tipos sean configurables por empresa)
- `OrgUnitType` por empresa (code/name/isActive)
> Si no quieres complejidad: `UnitType` puede ser un enum fijo con opción `Otro`.

---

## 6) Reglas de negocio (backend)

### RN-01 Estructura jerárquica válida (sin ciclos)
- Una unidad no puede ser padre de sí misma.
- No se permiten ciclos: al asignar `ParentUnitId` se valida que el padre NO sea descendiente del nodo.

### RN-02 Coherencia por empresa
- `ParentUnitId` debe pertenecer a la **misma empresa**.

### RN-03 Unicidad
- `Code` es único por empresa: `Unique(CompanyId, Code)`.
- (Opcional) `Name` único por padre: `Unique(CompanyId, ParentUnitId, Name)`.

### RN-04 Estados (Activa/Inactiva)
- No se elimina físicamente (soft delete):
  - `IsActive=false` representa inactiva.
- Inactivar unidad:
  - Bloquear si tiene **hijos activos** (recomendado).
  - Alternativa (si negocio lo pide): inactivación en cascada (no recomendado sin control).

### RN-05 Creación y jerarquía mínima
- Se permiten múltiples unidades raíz (si negocio lo requiere).
- Si el negocio exige 1 sola raíz:
  - Validar que solo exista una raíz activa (regla configurable).

### RN-06 Movimiento de unidad (cambio de dependencia)
- Mover una unidad (cambiar `ParentUnitId`) debe:
  - Validar RN-01 (sin ciclos)
  - Validar RN-02 (misma empresa)
  - Registrar auditoría (antes/después)

### RN-07 Profundidad máxima (protección)
- Establecer una profundidad máxima razonable (ej. 10–15 niveles) para evitar estructuras patológicas y consultas costosas.

### RN-08 Auditoría obligatoria
- Todas estas acciones generan evento:
  - Create, Update, Activate, Inactivate, Move

---

## 7) Requerimientos técnicos del Backend (arquitectura + persistencia)

### RT-01 Estándar de arquitectura
- Clean Architecture + CQRS:
  - Commands/Handlers + Validators
  - Queries/Handlers
- Respuestas estandarizadas (envelope):
  - `data`, `errors`, `traceId`, `timestamp`

### RT-02 Persistencia (SQL Server)
Tabla: `OrgUnits`
- Índices:
  - `IX_OrgUnits_CompanyId`
  - `UX_OrgUnits_CompanyId_Code`
  - `IX_OrgUnits_CompanyId_ParentUnitId`
- `RowVersion` para concurrencia optimista (409 Conflict si cambia).

### RT-03 Consultas de árbol
Opción recomendada (simple + suficiente):
- Adjacency List (`ParentUnitId`) + consultas con CTE recursivo para:
  - Tree completo
  - Descendientes
  - Ancestros

Opción avanzada (si esperas >10k nodos por empresa y muchas lecturas):
- Closure table (`OrgUnitClosure`) para lecturas ultra rápidas.
> Inicia con adjacency + CTE; migra a closure si el rendimiento lo exige.

### RT-04 Validaciones (FluentValidation)
Validar en commands:
- Create/Update:
  - `Code` requerido y formato (sin espacios, longitud)
  - `Name` requerido
  - `UnitType` requerido
- Move:
  - `ParentUnitId` != `Id`
  - `ParentUnitId` existe, es misma empresa
  - sin ciclos (consulta descendientes)
- Inactivate:
  - validar hijos activos = 0 (si aplicas RN-04)

### RT-05 Seguridad / Autorización
- Policies:
  - `OrgUnits.Admin`
  - `OrgUnits.Read`
- Validación tenant/company:
  - `CompanyId` debe estar dentro del scope del usuario.

### RT-06 Auditoría
- Pipeline Behavior de MediatR para:
  - capturar `before/after`
  - guardar en `AuditEvents`
- Campos mínimos:
  - actor, companyId, action, entity, entityId, timestamp, beforeJson, afterJson

---

## 8) Endpoints propuestos (API v1)

### 8.1 CRUD de Unidades
- `POST   /api/v1/companies/{companyId}/org-units`
- `GET    /api/v1/companies/{companyId}/org-units?isActive=&q=&type=&parentId=`
- `GET    /api/v1/org-units/{id}`
- `PUT    /api/v1/org-units/{id}`
- `PATCH  /api/v1/org-units/{id}/activate`
- `PATCH  /api/v1/org-units/{id}/inactivate`

### 8.2 Jerarquía / Organigrama
- `GET /api/v1/companies/{companyId}/org-units/tree?rootId=&depth=`
  - Devuelve estructura anidada (children[])
- `GET /api/v1/companies/{companyId}/org-units/graph?rootId=&depth=`
  - Devuelve `{ nodes:[], edges:[] }` para render gráfico
- `PATCH /api/v1/org-units/{id}/move`
  - body: `{ newParentId, sortOrder?, rowVersion }`

### 8.3 Utilidades (opcionales, pero útiles)
- `GET /api/v1/org-units/{id}/children`
- `GET /api/v1/org-units/{id}/ancestors`
- `GET /api/v1/org-units/{id}/descendants?depth=`

---

## 9) Contratos (DTOs) mínimos

### CreateOrgUnitRequest
- `code` (string)*
- `name` (string)*
- `unitType` (string/enum)*
- `parentUnitId` (guid|null)
- `sortOrder` (int|null)
- `description` (string|null)
- `costCenterCode` (string|null)

### OrgUnitResponse
- `id`
- `companyId`
- `code`
- `name`
- `unitType`
- `parentUnitId`
- `isActive`
- `sortOrder`
- `path` (opcional: “/Direccion A/Gerencia B/Depto C” si lo calculas)
- `createdAt`, `updatedAt`
- `rowVersion`

### TreeNodeResponse
- `id`, `code`, `name`, `unitType`, `isActive`, `children: TreeNodeResponse[]`

### GraphResponse
- `nodes: [{ id, label, type, isActive }]`
- `edges: [{ fromId, toId }]`

---

## 10) Errores y códigos recomendados
- 400 `VALIDATION_ERROR`
- 403 `FORBIDDEN`
- 404 `NOT_FOUND`
- 409 `CONFLICT` (ej. rowVersion, código duplicado)
- 422 `BUSINESS_RULE_VIOLATION` (ej. ciclo detectado, inactivar con hijos activos)

---

## 11) Criterios de aceptación (backend)
1. Puedo crear unidades por empresa con tipo y estado.
2. Puedo asignar dependencia (`parentUnitId`) y el sistema respeta jerarquía.
3. El sistema bloquea ciclos al mover/editar dependencias.
4. Puedo consultar organigrama como:
   - lista plana (para modo lista)
   - árbol anidado y/o grafo (para modo gráfico)
5. Puedo inactivar una unidad; el sistema bloquea si tiene hijos activos (según regla).
6. Toda operación queda auditada con before/after.
7. Los códigos son únicos por empresa y se responde 409 en duplicados.
8. Se valida concurrencia con `rowVersion`.

---

## 12) Recomendaciones de implementación (para evitar retrabajo)
- Implementar desde ya:
  - `rowVersion`
  - endpoint `tree` y `graph`
  - validación anti-ciclos en `move`
- Mantener `UnitType` como enum fijo inicialmente, pero dejar listo para evolucionar a catálogo por empresa.
