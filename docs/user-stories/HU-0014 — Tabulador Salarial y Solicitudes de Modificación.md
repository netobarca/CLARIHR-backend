# HU-0014 — Tabulador Salarial y Solicitudes de Modificación (Clases y Escalas)

## 1) Descripción del requerimiento (visión de negocio)
Como **Dueño de la cuenta / Administrador de Empresa / RRHH Compensaciones**,
quiero **gestionar el tabulador salarial por clases y escalas** y **crear solicitudes de modificación**,
para que los cambios salariales estructurales se ejecuten solo después de autorización formal y queden trazables.

Regla clave de negocio:
- La modificación del tabulador **no se aplica directamente** al capturarla.
- Primero debe pasar por flujo de autorización; una vez autorizada, el sistema aplica cambios de salario por clase/escala.

---

## 2) Objetivo funcional (qué habilita)
- Gobernanza de compensaciones por tenant (empresa).
- Control formal de cambios salariales con separación entre solicitante y aprobador.
- Versionado y vigencia efectiva del tabulador.
- Trazabilidad para auditoría interna, cumplimiento y revisiones futuras.
- Base reutilizable para módulos posteriores de nómina, presupuesto y analítica.

---

## 3) Alcance API (backend)
### Incluye
- Gestión del tabulador salarial por `SalaryClass + SalaryScale`.
- Solicitudes de modificación del tabulador con detalle por línea.
- Flujo de estado de solicitud:
  - `Draft`
  - `Submitted`
  - `Approved` (aplicada)
  - `Rejected`
  - `Canceled`
- Aprobación/rechazo con comentario obligatorio de decisión.
- Aplicación transaccional de cambios al tabulador al aprobar.
- Consultas:
  - listado paginado de líneas del tabulador
  - listado paginado de solicitudes
  - detalle de solicitud
  - vista de impacto previa a aprobación
  - export de tabulador (`csv|xlsx`)

### Fuera de alcance (por ahora, pero preparado)
- Reajuste automático de salario individual de empleados en tiempo real.
- Integración obligatoria con nómina externa.
- Flujo de aprobación multinivel configurable por política (v1 usa 1 nivel).
- Simulación presupuestaria avanzada con forecast financiero.

---

## 4) Actores y permisos
- **AccountOwner / Dueño de la cuenta**: administración total en su empresa activa.
- **CompanyAdmin**: administración funcional del tabulador.
- **HRCompensationAdmin**: crea, envía y gestiona solicitudes.
- **Approver de Compensaciones**: aprueba/rechaza solicitudes.
- **HRRead / FinanceRead**: consulta y exportación.

Permisos sugeridos (patrón funcional vigente):
- `SalaryTabulator.Read`
- `SalaryTabulator.Request`
- `SalaryTabulator.Approve`
- `SalaryTabulator.Admin`
- `iam.administration.manage` (override)
- `platform_admin` (override)

Regla base:
- La API valida siempre coincidencia entre `companyId` de la ruta y claim `tid`.

---

## 5) Relaciones internas y externas

### 5.1 Relaciones internas (dentro de CLARIHR)
- **JobProfiles**: campos de compensación referencian clase/escala vigente del tabulador.
- **PositionSlots**: cada plaza puede alinearse a clase/escala para control de costo.
- **Audit Logs**: toda solicitud y decisión genera auditoría `before/after`.
- **IAM/RBAC**: autorización por permisos funcionales del módulo.
- **Company/Tenant context**: aislamiento estricto por `tid`.

### 5.2 Relaciones externas (fuera de CLARIHR)
- **Nómina externa/ERP** (futuro): export/evento de cambios aprobados.
- **Benchmark salarial de mercado** (futuro): referencias para soporte de decisiones.
- **Herramientas financieras** (futuro): consumo de export para planeación presupuestaria.

---

## 6) Datos que debe manejar el backend (modelo mínimo)

### 6.1 Entidad principal: `SalaryTabulatorLine`
Campos sugeridos:
- `Id` (GUID público)
- `CompanyId` (GUID, obligatorio)
- `SalaryClassCode` (string, obligatorio)
- `SalaryScaleCode` (string, obligatorio)
- `CurrencyCode` (string, obligatorio, ISO-4217)
- `BaseAmount` (decimal, obligatorio)
- `MinAmount` (decimal, opcional)
- `MaxAmount` (decimal, opcional)
- `EffectiveFromUtc` (date, obligatorio)
- `EffectiveToUtc` (date, opcional)
- `IsActive` (bool)
- `Version` (int)
- `Notes` (string, opcional)
- `ConcurrencyToken` (GUID)
- `CreatedAtUtc`, `UpdatedAtUtc`

### 6.2 Entidad de proceso: `SalaryTabulatorChangeRequest`
- `Id` (GUID público)
- `CompanyId` (GUID)
- `RequestNumber` (string secuencial por tenant)
- `Reason` (string, obligatorio)
- `Status` (enum): `Draft|Submitted|Approved|Rejected|Canceled`
- `RequestedByUserId` (GUID)
- `SubmittedAtUtc` (date, opcional)
- `DecidedByUserId` (GUID, opcional)
- `DecidedAtUtc` (date, opcional)
- `DecisionComment` (string, opcional/obligatorio en approve-reject)
- `EffectiveFromUtc` (date, obligatorio)
- `ConcurrencyToken` (GUID)
- `CreatedAtUtc`, `UpdatedAtUtc`

### 6.3 Entidad de detalle: `SalaryTabulatorChangeRequestItem`
- `Id` (GUID público)
- `ChangeRequestId` (GUID)
- `SalaryClassCode` (string, obligatorio)
- `SalaryScaleCode` (string, obligatorio)
- `CurrentBaseAmount` (decimal)
- `ProposedBaseAmount` (decimal, obligatorio)
- `CurrentMinAmount` (decimal, opcional)
- `ProposedMinAmount` (decimal, opcional)
- `CurrentMaxAmount` (decimal, opcional)
- `ProposedMaxAmount` (decimal, opcional)
- `ChangeType` (enum): `Create|Update|Inactivate`
- `Notes` (string, opcional)

---

## 7) Reglas de negocio (backend)

### RN-01 Unicidad por clase/escala
- Solo una línea activa por combinación `SalaryClassCode + SalaryScaleCode` y fecha efectiva.
- No permitir traslape de vigencias para la misma combinación.

### RN-02 Flujo de autorización
- `Draft` permite edición completa.
- `Submitted` bloquea edición de items.
- Solo `SalaryTabulator.Approve`/`Admin` puede aprobar o rechazar.
- El solicitante no puede autoaprobar (excepto `platform_admin` si se habilita override).

### RN-03 Aplicación de cambios
- Aprobar ejecuta aplicación transaccional:
  - actualiza/crea/inactiva líneas del tabulador
  - marca solicitud como `Approved`
  - incrementa versión y refresca `ConcurrencyToken`
- Si falla una línea, se revierte toda la operación.

### RN-04 Validaciones salariales
- `BaseAmount > 0`.
- Si existen `MinAmount` y `MaxAmount`: `MinAmount <= BaseAmount <= MaxAmount`.
- No permitir reducción por debajo de mínimos legales/políticas internas (regla configurable).

### RN-05 Vigencia
- `EffectiveFromUtc` obligatorio.
- `EffectiveToUtc` opcional, pero si existe debe ser `>= EffectiveFromUtc`.
- No permitir aprobación con fecha efectiva inválida.

### RN-06 Concurrencia y auditoría
- Escrituras por id validan `ConcurrencyToken`.
- Toda operación relevante registra `before/after`.

### RN-07 Seguridad tenant-scoped
- Toda operación por `{id}` distingue `NotFound` vs `TenantMismatch` usando `ExistsOutsideTenantAsync` + `IgnoreQueryFilters`.

---

## 8) Requerimientos técnicos del Backend (arquitectura + persistencia)

### RT-01 Arquitectura
- Mantener patrón actual:
  - Clean Architecture
  - CQRS (`Commands/Queries + Handlers`)
  - FluentValidation
  - `ProblemDetails`
  - tenant-scoped por `tid`
  - `ConcurrencyToken` GUID

### RT-02 Persistencia (PostgreSQL)
Tablas sugeridas:
- `salary_tabulator_lines`
- `salary_tabulator_change_requests`
- `salary_tabulator_change_request_items`

Índices recomendados:
- `uq_salary_tabulator_lines__tenant_class_scale_effective_from`
- `ix_salary_tabulator_lines__tenant_class_scale_active`
- `ix_salary_tabulator_requests__tenant_status_created`
- `ix_salary_tabulator_requests__tenant_request_number`
- `ix_salary_tabulator_items__request`

### RT-03 Rendimiento
- Listados paginados obligatorios (`page`, `pageSize`).
- Filtros indexables por `class`, `scale`, `status`, `effectiveFrom`.
- Proyecciones para listado/detalle y `AsNoTracking` en lecturas.
- Export `csv/xlsx` basado en dataset único proyectado.

### RT-04 Seguridad y autorización
- Servicio `ISalaryTabulatorAuthorizationService` (patrón de módulos funcionales existentes).
- Lectura:
  - `SalaryTabulator.Read`
  - `SalaryTabulator.Request`
  - `SalaryTabulator.Approve`
  - `SalaryTabulator.Admin`
- Escritura de solicitudes:
  - `SalaryTabulator.Request`
  - `SalaryTabulator.Admin`
- Aprobación/rechazo:
  - `SalaryTabulator.Approve`
  - `SalaryTabulator.Admin`

### RT-05 Auditoría
Eventos mínimos sugeridos:
- `SALARY_TABULATOR_REQUEST_CREATED`
- `SALARY_TABULATOR_REQUEST_SUBMITTED`
- `SALARY_TABULATOR_REQUEST_APPROVED`
- `SALARY_TABULATOR_REQUEST_REJECTED`
- `SALARY_TABULATOR_LINE_APPLIED`
- `SALARY_TABULATOR_LINE_INACTIVATED`

---

## 9) Endpoints propuestos (API v1)

### 9.1 Tabulador salarial
- `GET    /api/v1/companies/{companyId}/salary-tabulator?salaryClass=&salaryScale=&isActive=&q=&page=&pageSize=`
- `GET    /api/v1/salary-tabulator/lines/{id}`
- `GET    /api/v1/companies/{companyId}/salary-tabulator/export?format=csv|xlsx&salaryClass=&salaryScale=&isActive=&q=`

### 9.2 Solicitudes de modificación
- `POST   /api/v1/companies/{companyId}/salary-tabulator/change-requests`
- `GET    /api/v1/companies/{companyId}/salary-tabulator/change-requests?status=&requestedBy=&effectiveFrom=&effectiveTo=&page=&pageSize=`
- `GET    /api/v1/salary-tabulator/change-requests/{id}`
- `PUT    /api/v1/salary-tabulator/change-requests/{id}`
- `PATCH  /api/v1/salary-tabulator/change-requests/{id}/submit`
- `PATCH  /api/v1/salary-tabulator/change-requests/{id}/approve`
- `PATCH  /api/v1/salary-tabulator/change-requests/{id}/reject`
- `PATCH  /api/v1/salary-tabulator/change-requests/{id}/cancel`
- `GET    /api/v1/salary-tabulator/change-requests/{id}/impact`

---

## 10) Contratos (DTOs) mínimos

### 10.1 `CreateSalaryTabulatorChangeRequest`
- `reason`*
- `effectiveFromUtc`*
- `items[]`*
  - `salaryClassCode`*
  - `salaryScaleCode`*
  - `changeType`* (`Create|Update|Inactivate`)
  - `proposedBaseAmount`*
  - `proposedMinAmount`
  - `proposedMaxAmount`
  - `notes`

### 10.2 `UpdateSalaryTabulatorChangeRequest`
- mismo contrato de create + `concurrencyToken`*

### 10.3 `SubmitSalaryTabulatorChangeRequest`
- `concurrencyToken`*

### 10.4 `ApproveSalaryTabulatorChangeRequest`
- `decisionComment`*
- `concurrencyToken`*

### 10.5 `RejectSalaryTabulatorChangeRequest`
- `decisionComment`*
- `concurrencyToken`*

### 10.6 Respuestas
- `SalaryTabulatorLineResponse`
- `SalaryTabulatorChangeRequestResponse`
- `SalaryTabulatorChangeRequestListItem`
- `SalaryTabulatorImpactResponse`

---

## 11) Errores y códigos recomendados
- `400` `VALIDATION_ERROR`
- `401` `UNAUTHENTICATED`
- `403` `FORBIDDEN` / `TENANT_MISMATCH`
- `404` `SALARY_TABULATOR_LINE_NOT_FOUND` / `SALARY_TABULATOR_REQUEST_NOT_FOUND`
- `409` `CONCURRENCY_CONFLICT`, `SALARY_TABULATOR_REQUEST_STATE_CONFLICT`, `SALARY_TABULATOR_EFFECTIVE_DATE_OVERLAP`
- `422` `SALARY_TABULATOR_AMOUNT_RULE_VIOLATION`, `SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION`

---

## 12) Criterios de aceptación (backend)
1. Puedo consultar el tabulador salarial vigente por clase y escala.
2. Puedo crear una solicitud en `Draft` con múltiples cambios.
3. Puedo enviar la solicitud a `Submitted`.
4. Un usuario aprobador puede aprobar o rechazar con comentario.
5. Al aprobar, los cambios se aplican al tabulador en una sola transacción.
6. La API bloquea autoaprobación del solicitante (según política definida).
7. La API valida reglas de monto y vigencia antes de aprobar.
8. La API rechaza operaciones cross-tenant.
9. La API valida concurrencia en escrituras por id.
10. Toda acción crítica queda auditada.
11. Puedo exportar el tabulador en `csv` y `xlsx`.

---

## 13) Plan de pruebas

### 13.1 Unit tests
- Dominio:
  - reglas de monto (`min/base/max`)
  - reglas de vigencia y traslape
  - transición de estados de solicitud
  - bloqueo de autoaprobación
  - refresh de `ConcurrencyToken`
- Aplicación:
  - validación `NotFound` vs `TenantMismatch`
  - aplicación transaccional al aprobar
  - conflictos de estado y concurrencia

### 13.2 Integration tests (HTTP)
- Flujo feliz:
  - create draft -> update -> submit -> approve -> verificar líneas aplicadas
- Seguridad:
  - `403` por tenant mismatch
  - `403` por falta de permisos
  - `403/422` por autoaprobación bloqueada
- Conflictos:
  - `409` por token stale
  - `409` por estado inválido
  - `422` por montos/fechas inválidas
- Auditoría:
  - registro en `audit_logs` para create/submit/approve/reject/apply

### 13.3 Validación final
- `dotnet build CLARIHR.slnx`
- `dotnet test CLARIHR.slnx --no-build`

---

## 14) Recomendaciones de implementación (mejores prácticas HRIS)
- Implementar por fases:
  - **Fase 1:** tabulador + solicitudes + aprobación + aplicación + auditoría + export base.
  - **Fase 2:** workflow multinivel, reglas avanzadas de compliance, integración con nómina.
- Mantener separación estricta entre:
  - solicitud de cambio
  - autorización
  - aplicación de cambios
- Proteger decisiones de compensación con trazabilidad completa y comentarios obligatorios.
- Preparar extensión futura para:
  - integración de presupuesto
  - reglas por país/legislación
  - publicación de bandas para reclutamiento interno.
