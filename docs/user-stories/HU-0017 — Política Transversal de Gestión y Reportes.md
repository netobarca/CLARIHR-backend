# HU-0017 — Política Transversal de Gestión y Reportes (CRUD con Restricciones por Tipo/Estado + Impresión/Exportación)

## 0) Validación del estado actual
Resultado del análisis del backend/documentación actual:

- Cobertura parcial del requisito "agregar, modificar, eliminar":
  - En catálogos operativos predomina `POST/PUT` + `PATCH activate|inactivate|archive` (soft delete).
  - `DELETE` explícito existe solo en casos puntuales (por ejemplo, roles IAM).
  - Las restricciones por estado ya existen en varios módulos, pero no están estandarizadas bajo una política transversal única.
- Cobertura parcial del requisito "imprimir o exportar en formatos gráficos o tablas":
  - Ya existen exportaciones tabulares (`csv|xlsx`) en varios módulos (`position-slots`, `salary-tabulator`, `cost-centers`, `legal-representatives`).
  - Ya existen salidas gráficas en módulos jerárquicos (`org-units/graph`, `position-slots/graph`, `diagram-export`).
  - `print` formal está implementado solo en módulos específicos (`job-profiles/{id}/print`).
  - No hay estandarización global de capacidades de reporte por módulo.

Conclusión:
- El requerimiento está **parcialmente cubierto**.
- Se requiere HU transversal para normalizar:
  - reglas de alta/modificación/eliminación por tipo y estado;
  - política uniforme de reportes (print/export, formato tabla/gráfico según naturaleza del reporte).

---

## 1) Descripción del requerimiento (visión de negocio)
Como **Administrador de la empresa / Responsable funcional del módulo**,
quiero **agregar, modificar y eliminar información con restricciones según tipo y estado**,
y **contar con opción para imprimir o exportar reportes en formatos gráficos o tablas**,
para garantizar consistencia operativa, trazabilidad y disponibilidad de información para gestión y auditoría.

Texto base del requerimiento:
- "Para todas las opciones, deberá permitir agregar, modificar, eliminar, con las restricciones correspondientes según el tipo y estado de la información."
- "Para todos los reportes debe contar con opción para imprimir o exportar la información en formatos gráficos o tablas."

---

## 2) Objetivo funcional (qué habilita)
- Política única de operaciones por recurso:
  - `create`
  - `update`
  - `delete` (físico o lógico según política del recurso)
- Gobierno explícito por `tipo` y `estado` del dato.
- Contrato uniforme para capacidades de reporte:
  - impresión (`print`)
  - exportación (`export`)
  - salida tabular y/o gráfica según el reporte.
- Reducción de ambigüedad entre módulos y menor retrabajo de frontend/QA.

---

## 3) Alcance API (backend)
### Incluye
- Definir y aplicar política transversal de operaciones por recurso.
- Estandarizar "eliminar":
  - por defecto: soft delete (`archive|inactivate|deactivate`);
  - hard delete solo cuando la naturaleza del recurso lo permita.
- Estandarizar capacidades de reportes por módulo:
  - `print` y/o `export`;
  - formato de salida de tipo `table` y/o `graph`.
- Devolver capacidades calculadas de acciones permitidas por ítem (`allowedActions`).
- Auditoría obligatoria para operaciones de escritura y eliminación.

### Fuera de alcance (por ahora, pero preparado)
- Motor de plantillas PDF avanzado con branding por tenant.
- Exportadores binarios de gráficos a imágenes rasterizadas (`png/jpg`) para todos los módulos.
- Integraciones externas de BI (Power BI/Tableau) como destino directo.

---

## 4) Actores y permisos
- `Owner/CompanyAdmin`: administración total.
- `FunctionalAdmin` del módulo: crea, modifica, elimina según política de recurso.
- `ReadOnly/Analyst`: consulta y reportes.
- `platform_admin`: override.

Permisos transversales sugeridos:
- `<Resource>.Read`
- `<Resource>.Admin`
- `<Resource>.Delete` (cuando aplique hard delete)
- `<Resource>.Export`
- `<Resource>.Print`

Regla base:
- Validar siempre tenant (`tid`) y ownership/alcance de recurso.

---

## 5) Modelo funcional transversal
### 5.1 Capacidades por registro (`allowedActions`)
Respuesta recomendada por item/detalle:

- `canAddChild` (si aplica jerarquía)
- `canEdit`
- `canDelete`
- `canArchive`
- `canActivate`
- `canInactivate`
- `reasons[]` (bloqueos de regla)

### 5.2 Capacidades de reporte por módulo
Contrato recomendado:

- `supportsPrint` (`true|false`)
- `supportsExport` (`true|false`)
- `supportedTableFormats[]` (`csv|xlsx|json|...`)
- `supportedGraphFormats[]` (`graphml|dot|json|...`)

### 5.3 Política de eliminación por recurso
- `SoftDeleteOnly`
- `HardDeleteAllowedWhenDraft`
- `HardDeleteAllowedWhenNoDependencies`
- `HardDeleteForbiddenSystemResource`

---

## 6) Reglas de negocio (backend)
### RN-01 Operaciones permitidas por estado
- `create` y `update` deben validar estado del recurso padre y del recurso actual.
- No permitir edición cuando el estado sea final/cerrado/publicado (según recurso).

### RN-02 Eliminación por tipo de recurso
- Recursos de catálogo operacional: eliminación lógica por defecto.
- Recursos del sistema (`isSystem`, `default`, `seeded`) no se eliminan físicamente.
- Hard delete solo si política explícita del recurso lo autoriza.

### RN-03 Restricciones por dependencias
- Bloquear eliminación si existen referencias activas.
- Entregar error de negocio específico con detalle de dependencia.

### RN-04 Restricciones por tipo
- Tipos críticos (ejemplo: principal, default, system) requieren reglas reforzadas:
  - no eliminar;
  - o reemplazo previo obligatorio.

### RN-05 Concurrencia
- Toda operación destructiva o de escritura por id exige `concurrencyToken`.
- En conflicto devolver `409 CONCURRENCY_CONFLICT`.

### RN-06 Seguridad tenant-scoped
- Distinguir `NotFound` vs `TenantMismatch`.
- No exponer datos cruzados entre tenants.

### RN-07 Reportes obligatorios
- Cada reporte debe ofrecer al menos una opción de salida:
  - impresión (`print`) o exportación (`export`).
- Si la salida es exportación:
  - debe estar en formato tabular y/o gráfico, según naturaleza del reporte.
- Si el recurso es jerárquico/dependencias:
  - debe soportar salida gráfica.

### RN-08 Auditoría obligatoria
- Registrar eventos de creación, actualización, eliminación lógica/física, impresión y exportación.

---

## 7) Requerimientos técnicos del Backend (arquitectura + persistencia)
### RT-01 Arquitectura
- Mantener patrón actual:
  - Clean Architecture
  - CQRS
  - FluentValidation
  - ProblemDetails
  - control tenant-scoped

### RT-02 Política centralizada de acciones
- Introducir servicio transversal:
  - `IResourceActionPolicyService`
- Evalúa acciones permitidas por:
  - tipo
  - estado
  - dependencias
  - permisos

### RT-03 Estandar de endpoints de reporte
Convención mínima por módulo:

- `GET /...` (consulta de datos)
- `GET /.../export?format=...` (tabular/gráfico según aplique)
- `GET /.../print?...` (cuando aplique impresión formal)

### RT-04 Performance
- Paginación obligatoria en listados.
- Export con proyección directa y `AsNoTracking`.
- Límites de tamaño por export y timeout controlado.

### RT-05 Seguridad
- Permisos específicos para export/print.
- Logging de actor, tenant, formato, filtros y volumen exportado.

### RT-06 Persistencia
- Sin tabla obligatoria nueva si se mantiene política en código por recurso.
- Opcional (v2): tabla de configuración de capacidades por recurso:
  - `resource_capabilities`
  - `report_capabilities`

---

## 8) Endpoints propuestos (estándar transversal)
### 8.1 Gestión de información (por recurso)
- `POST   /api/v1/companies/{companyId}/{resource}`
- `PUT    /api/v1/{resource}/{id}`
- `PATCH  /api/v1/{resource}/{id}/activate|inactivate|archive|reactivate`
- `DELETE /api/v1/{resource}/{id}` (solo si política del recurso lo permite)

### 8.2 Reportes
- `GET /api/v1/companies/{companyId}/{resource}/export?format=...`
- `GET /api/v1/{resource}/{id}/print` o `GET /api/v1/companies/{companyId}/{resource}/print?...`
- `GET /api/v1/companies/{companyId}/{resource}/graph` (si aplica)
- `GET /api/v1/companies/{companyId}/{resource}/diagram-export?format=...` (si aplica)

---

## 9) Contratos (DTOs) transversales mínimos
### 9.1 `ConcurrencyRequest`
- `concurrencyToken`*

### 9.2 `DeleteResourceRequest`
- `concurrencyToken`*
- `reason` (opcional recomendado)

### 9.3 `AllowedActionsResponse`
- `canEdit`
- `canDelete`
- `canArchive`
- `canActivate`
- `canInactivate`
- `reasons[]`

### 9.4 `ReportCapabilitiesResponse`
- `supportsPrint`
- `supportsExport`
- `supportedTableFormats[]`
- `supportedGraphFormats[]`

---

## 10) Errores y códigos recomendados
- `RESOURCE_ACTION_FORBIDDEN_BY_STATE`
- `RESOURCE_ACTION_FORBIDDEN_BY_TYPE`
- `RESOURCE_DELETE_BLOCKED_BY_DEPENDENCIES`
- `RESOURCE_DELETE_NOT_ALLOWED`
- `REPORT_NOT_AVAILABLE`
- `REPORT_FORMAT_NOT_SUPPORTED`
- `TENANT_MISMATCH`
- `CONCURRENCY_CONFLICT`

---

## 11) Criterios de aceptación (backend)
- Para cada módulo en alcance, el backend expone operaciones para agregar y modificar.
- La operación de eliminar existe y respeta la política del recurso (lógica o física).
- Toda restricción por tipo/estado está validada en backend, no solo en frontend.
- Todo reporte tiene al menos una opción operativa de impresión o exportación.
- La exportación soporta formato tabular o gráfico según la naturaleza del reporte.
- Todas las operaciones escribibles quedan auditadas.
- Todas las operaciones respetan tenant y permisos.

---

## 12) Plan de pruebas
### Unit tests
- Evaluación de políticas por tipo/estado.
- Bloqueo de eliminación por dependencia.
- Reglas de transición de estados.
- Validación de formatos de reporte.

### Integration tests (HTTP)
- Flujos create/update/delete por recurso con casos permitidos y bloqueados.
- `409` por concurrencia.
- `403` por permisos y tenant mismatch.
- `400` por formato de reporte inválido.
- `200` en export/print con content-type correcto.

### Validación final
- `dotnet build CLARIHR.slnx`
- `dotnet test CLARIHR.slnx --no-build`

---

## 13) Recomendaciones de implementación
- Implementar por fases:
  1. Normalización de políticas de acción por recurso.
  2. Homologación de eliminación lógica/física.
  3. Homologación de print/export por módulo.
  4. Observabilidad/auditoría transversal.
- Mantener backward compatibility de endpoints existentes y agregar capacidades faltantes por módulo.

---

## 14) Supuestos y decisiones cerradas
- "Eliminar" no implica hard delete obligatorio; se permite soft delete según política del recurso.
- Para reportes no jerárquicos, formato tabular cubre el requisito.
- Para reportes jerárquicos/dependencias, salida gráfica es obligatoria.
- La validación de restricciones reside en backend como fuente de verdad.
