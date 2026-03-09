# HU-0018 — Competencias para Asociar a los Puestos (Marco de Competencias, Conductas, Comportamientos y Pirámide Ocupacional)

## 0) Validación del estado actual
Resultado del análisis del backend/documentación actual:

- Cobertura parcial del requerimiento:
  - Ya existe asociación de `competencies[]` dentro de `JobProfile` (HU-012).
  - Ya existe catálogo tenant-scoped en `job_catalog_items` con categoría `Competency`.
  - Un puesto puede tener varias competencias (lista en `job_profile_competencies`).
- Brechas críticas respecto al requerimiento fuente:
  - No existen catálogos específicos para:
    - tipos de competencia
    - niveles de comportamiento
    - comportamientos
  - No existe entidad de `conductas` asociadas a competencia + nivel + tipo.
  - `expectedLevel` actual es texto libre; no está normalizado ni ligado a catálogo jerárquico.
  - No existe `pirámide ocupacional` como catálogo/estructura jerárquica.
  - No existe vínculo formal entre niveles de pirámide ocupacional y puestos.
  - No existe matriz de asociación "competencia-conducta-comportamiento-nivel" por puesto.
- Seguridad y permisos:
  - Hoy hay permisos de `JobProfiles`/`JobCatalogs`, pero no permisos funcionales explícitos para un marco de competencias completo.
- API:
  - No hay endpoints dedicados para administrar tipos de competencia, niveles de comportamiento, conductas, comportamientos ni pirámide ocupacional.

### 0.1) Descomposición exhaustiva del requerimiento fuente
Trazabilidad del texto original a capacidades backend:

1. **Competencias y comportamientos**
- Requiere modelo para múltiples competencias por puesto.
- Requiere vínculo explícito de competencia con comportamientos observables.
- Requiere asociación por diferentes niveles jerárquicos.

2. **Niveles de comportamientos**
- Requiere catálogo jerárquico y reutilizable de niveles.
- Debe servir de base para grupos organizativos.

3. **Tipos de competencia**
- Requiere catálogo independiente de tipos.

4. **Competencias**
- Requiere catálogo de competencias con opción de registrar conductas por tipo y nivel.

5. **Comportamientos**
- Requiere catálogo de comportamientos relacionados a conductas.

6. **Pirámide ocupacional**
- Requiere catálogo de niveles de pirámide.
- Requiere asociación de esos niveles con puestos para conocer competencias/conductas esperadas.

Conclusión:
- El requerimiento está **parcialmente cubierto** solo en su capa más básica (competencias en perfil).
- Se requiere HU nueva para incorporar un **marco estructurado de competencias** y su integración con puestos.

---

## 1) Descripción del requerimiento (visión de negocio)
Como **Dueño de la cuenta / Administrador de Empresa / RRHH**,
quiero **definir competencias, tipos de competencia, niveles de comportamiento, conductas, comportamientos y niveles de pirámide ocupacional**,
y **asociarlos a los puestos**,
para que la institución tenga una base formal y auditable de las competencias y comportamientos esperados por nivel jerárquico.

Resultado esperado:
- Un puesto puede tener múltiples competencias asociadas.
- Cada competencia puede tener conductas y comportamientos esperados por nivel.
- La pirámide ocupacional define expectativas diferenciales por jerarquía.
- La información es reutilizable para gestión de talento, evaluación de desempeño y reportes.

---

## 2) Objetivo funcional (qué habilita)
- Normalizar el lenguaje institucional de competencias y comportamientos.
- Estandarizar expectativas por nivel jerárquico mediante pirámide ocupacional.
- Evitar niveles y descripciones en texto libre no gobernado.
- Permitir asociación explícita y trazable de competencias esperadas por puesto.
- Preparar base para módulos futuros:
  - evaluación de desempeño
  - planes de desarrollo
  - sucesión y carrera
  - analítica de brechas de competencias

---

## 3) Alcance API (backend)
### Incluye
- Catálogo de `Tipos de competencia`.
- Catálogo de `Niveles de comportamiento`.
- Catálogo de `Competencias`.
- Gestión de `Conductas` asociadas a competencia + tipo + nivel.
- Catálogo de `Comportamientos` y su relación con conductas.
- Catálogo de `Pirámide ocupacional`.
- Asociación de niveles de pirámide ocupacional con `JobProfile` (puestos).
- Asociación de matriz de competencias esperadas por puesto.
- Consultas y export de matriz de competencias por puesto.

### Fuera de alcance (por ahora, pero preparado)
- Evaluación/calificación de empleados contra competencias.
- Recomendación automática de capacitación por brechas.
- Machine learning para inferir competencias.
- Integración con LMS o plataformas externas de evaluación.

---

## 4) Actores y permisos
- **AccountOwner / CompanyAdmin**: administración total del marco de competencias.
- **HRAdmin / TalentAdmin**: administra catálogos y asociaciones por puesto.
- **HRRead / LeadershipRead**: consulta y exportación.

Permisos sugeridos:
- `CompetencyFramework.Read`
- `CompetencyFramework.Admin`
- `JobProfiles.Read`
- `JobProfiles.Admin`
- `iam.administration.manage` (override)
- `platform_admin` (override)

Regla base:
- Validar siempre tenant (`tid`) y coincidencia de `companyId` en rutas tenant-scoped.

---

## 5) Datos que debe manejar el backend (modelo mínimo)

### 5.1 Catálogos base
Se propone extender `job_catalog_items` con nuevas categorías:
- `CompetencyType`
- `BehaviorLevel`
- `Behavior`

Se mantiene categoría existente:
- `Competency`

### 5.2 Entidad nueva: `OccupationalPyramidLevel`
Campos sugeridos:
- `Id` (GUID público)
- `CompanyId` (GUID)
- `Code` (único por empresa)
- `Name`
- `LevelOrder` (entero jerárquico)
- `Description`
- `IsActive`
- `ConcurrencyToken`
- `CreatedAtUtc`, `UpdatedAtUtc`

### 5.3 Entidad nueva: `CompetencyConduct`
Representa la conducta esperada dentro de una competencia.

Campos sugeridos:
- `Id` (GUID público)
- `CompanyId`
- `CompetencyCatalogItemId` (catálogo `Competency`)
- `CompetencyTypeCatalogItemId` (catálogo `CompetencyType`)
- `BehaviorLevelCatalogItemId` (catálogo `BehaviorLevel`)
- `Description` (descripción de la conducta)
- `SortOrder`
- `IsActive`
- `ConcurrencyToken`
- `CreatedAtUtc`, `UpdatedAtUtc`

### 5.4 Entidad nueva: `CompetencyConductBehavior`
Relaciona comportamientos con conductas.

Campos sugeridos:
- `Id` (GUID público)
- `CompetencyConductId`
- `BehaviorCatalogItemId` (catálogo `Behavior`)
- `Notes` (opcional)
- `SortOrder`
- `CompanyId`

### 5.5 Asociación a puestos: `JobProfileCompetencyExpectation`
Matriz esperada por puesto (recomendada para no depender de texto libre).

Campos sugeridos:
- `Id` (GUID público)
- `JobProfileId`
- `OccupationalPyramidLevelId`
- `CompetencyCatalogItemId`
- `CompetencyTypeCatalogItemId`
- `BehaviorLevelCatalogItemId`
- `ExpectedEvidence` (opcional)
- `SortOrder`
- `CompanyId`
- `ConcurrencyToken`

### 5.6 Relación de conductas esperadas por puesto
`JobProfileCompetencyExpectationConduct`:
- `JobProfileCompetencyExpectationId`
- `CompetencyConductId`
- `SortOrder`
- `CompanyId`

---

## 6) Reglas de negocio (backend)

### RN-01 Múltiples competencias por puesto
- Un `JobProfile` puede asociar múltiples competencias activas.
- La combinación `(JobProfile, Competency, OccupationalPyramidLevel)` no se duplica.

### RN-02 Consistencia de tipo y nivel
- Toda conducta debe tener `tipo de competencia` y `nivel de comportamiento` válidos y activos.
- No se permite conducta sin competencia base.

### RN-03 Comportamientos asociados a conducta
- Todo comportamiento asociado debe existir y estar activo.
- Un comportamiento no puede repetirse dentro de la misma conducta.

### RN-04 Pirámide ocupacional
- `LevelOrder` debe ser único por empresa.
- No se permite inactivar un nivel en uso por puestos activos sin reasignación previa.

### RN-05 Asociación de puestos a pirámide
- Un puesto puede tener uno o más niveles de pirámide según diseño organizacional.
- Si el modelo define nivel único por puesto, validar unicidad por `JobProfileId`.

### RN-06 Integridad tenant-scoped
- Toda FK debe pertenecer al mismo tenant.
- Diferenciar `NotFound` vs `TenantMismatch`.

### RN-07 Estados y eliminación
- Catálogos operativos usan eliminación lógica (`activate/inactivate`).
- Bloquear inactivación/eliminación si hay dependencias activas.

### RN-08 Concurrencia
- Toda escritura por id valida `ConcurrencyToken`.

### RN-09 Auditoría
- Toda operación de escritura debe registrar before/after.

---

## 7) Requerimientos técnicos del Backend (arquitectura + persistencia)

### RT-01 Arquitectura
- Mantener patrón actual:
  - Clean Architecture
  - CQRS
  - FluentValidation
  - ProblemDetails
  - control tenant-scoped por `tid`

### RT-02 Persistencia (PostgreSQL)
Tablas nuevas sugeridas:
- `occupational_pyramid_levels`
- `competency_conducts`
- `competency_conduct_behaviors`
- `job_profile_competency_expectations`
- `job_profile_competency_expectation_conducts`

Ajustes sugeridos:
- ampliar catálogo funcional con categorías `CompetencyType`, `BehaviorLevel`, `Behavior`.

Índices recomendados:
- `uq_occupational_pyramid_levels__tenant_code`
- `uq_occupational_pyramid_levels__tenant_level_order`
- `ix_competency_conducts__tenant_competency_type_level`
- `ix_competency_conduct_behaviors__tenant_conduct_sort`
- `ix_job_profile_competency_expectations__tenant_profile_sort`
- `uq_job_profile_competency_expectations__tenant_profile_competency_level`

### RT-03 Rendimiento
- Listados paginados obligatorios.
- Resolver matrices en una sola proyección para evitar N+1.
- Cache corto por tenant para catálogos de tipos/niveles/comportamientos.

### RT-04 Seguridad y autorización
- Servicio funcional sugerido: `ICompetencyFrameworkAuthorizationService`.
- Lectura: `CompetencyFramework.Read|Admin`.
- Escritura: `CompetencyFramework.Admin`.

### RT-05 Auditoría
Eventos mínimos sugeridos:
- `COMPETENCY_TYPE_CREATED|UPDATED|ACTIVATED|INACTIVATED`
- `BEHAVIOR_LEVEL_CREATED|UPDATED|ACTIVATED|INACTIVATED`
- `COMPETENCY_CREATED|UPDATED|ACTIVATED|INACTIVATED`
- `COMPETENCY_CONDUCT_CREATED|UPDATED|ACTIVATED|INACTIVATED`
- `COMPETENCY_BEHAVIOR_LINKED|UNLINKED`
- `OCCUPATIONAL_PYRAMID_LEVEL_CREATED|UPDATED|ACTIVATED|INACTIVATED`
- `JOB_PROFILE_COMPETENCY_MATRIX_UPDATED`

---

## 8) Endpoints propuestos (API v1)

### 8.1 Catálogos base
- `GET    /api/v1/companies/{companyId}/competency-types?q=&isActive=&page=&pageSize=`
- `POST   /api/v1/companies/{companyId}/competency-types`
- `PUT    /api/v1/competency-types/{id}`
- `PATCH  /api/v1/competency-types/{id}/activate`
- `PATCH  /api/v1/competency-types/{id}/inactivate`

- `GET    /api/v1/companies/{companyId}/behavior-levels?q=&isActive=&page=&pageSize=`
- `POST   /api/v1/companies/{companyId}/behavior-levels`
- `PUT    /api/v1/behavior-levels/{id}`
- `PATCH  /api/v1/behavior-levels/{id}/activate`
- `PATCH  /api/v1/behavior-levels/{id}/inactivate`

- `GET    /api/v1/companies/{companyId}/competencies?q=&typeId=&isActive=&page=&pageSize=`
- `POST   /api/v1/companies/{companyId}/competencies`
- `PUT    /api/v1/competencies/{id}`
- `PATCH  /api/v1/competencies/{id}/activate`
- `PATCH  /api/v1/competencies/{id}/inactivate`

- `GET    /api/v1/companies/{companyId}/behaviors?q=&isActive=&page=&pageSize=`
- `POST   /api/v1/companies/{companyId}/behaviors`
- `PUT    /api/v1/behaviors/{id}`
- `PATCH  /api/v1/behaviors/{id}/activate`
- `PATCH  /api/v1/behaviors/{id}/inactivate`

### 8.2 Conductas
- `GET    /api/v1/companies/{companyId}/competency-conducts?competencyId=&competencyTypeId=&behaviorLevelId=&isActive=&q=&page=&pageSize=`
- `POST   /api/v1/companies/{companyId}/competency-conducts`
- `GET    /api/v1/competency-conducts/{id}`
- `PUT    /api/v1/competency-conducts/{id}`
- `PATCH  /api/v1/competency-conducts/{id}/activate`
- `PATCH  /api/v1/competency-conducts/{id}/inactivate`
- `PUT    /api/v1/competency-conducts/{id}/behaviors`

### 8.3 Pirámide ocupacional
- `GET    /api/v1/companies/{companyId}/occupational-pyramid-levels?q=&isActive=&page=&pageSize=`
- `POST   /api/v1/companies/{companyId}/occupational-pyramid-levels`
- `PUT    /api/v1/occupational-pyramid-levels/{id}`
- `PATCH  /api/v1/occupational-pyramid-levels/{id}/activate`
- `PATCH  /api/v1/occupational-pyramid-levels/{id}/inactivate`

### 8.4 Asociación a puestos
- `GET    /api/v1/job-profiles/{id}/competency-matrix`
- `PUT    /api/v1/job-profiles/{id}/competency-matrix`
- `GET    /api/v1/job-profiles/{id}/competency-matrix/export?format=json|csv|xlsx`

---

## 9) Contratos (DTOs) mínimos

### 9.1 `CreateCompetencyConductRequest`
- `competencyId`*
- `competencyTypeId`*
- `behaviorLevelId`*
- `description`*
- `sortOrder`

### 9.2 `UpdateCompetencyConductBehaviorsRequest`
- `behaviorIds[]`*
- `concurrencyToken`*

### 9.3 `JobProfileCompetencyMatrixItemRequest`
- `occupationalPyramidLevelId`*
- `competencyId`*
- `competencyTypeId`*
- `behaviorLevelId`*
- `conductIds[]`
- `expectedEvidence`
- `sortOrder`

### 9.4 `UpdateJobProfileCompetencyMatrixRequest`
- `items[]` (`JobProfileCompetencyMatrixItemRequest`)
- `concurrencyToken`*

### 9.5 `ConcurrencyRequest`
- `concurrencyToken`*

---

## 10) Errores y códigos recomendados
- `COMPETENCY_TYPE_NOT_FOUND`
- `BEHAVIOR_LEVEL_NOT_FOUND`
- `BEHAVIOR_NOT_FOUND`
- `COMPETENCY_NOT_FOUND`
- `COMPETENCY_CONDUCT_NOT_FOUND`
- `COMPETENCY_CONDUCT_DUPLICATE`
- `OCCUPATIONAL_PYRAMID_LEVEL_NOT_FOUND`
- `OCCUPATIONAL_PYRAMID_LEVEL_IN_USE`
- `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT`
- `RESOURCE_IN_USE`
- `TENANT_MISMATCH`
- `CONCURRENCY_CONFLICT`

---

## 11) Criterios de aceptación (backend)
- Se pueden crear, editar y desactivar tipos de competencia.
- Se pueden crear, editar y desactivar niveles de comportamiento.
- Se pueden crear, editar y desactivar competencias.
- Se pueden crear conductas ligadas a competencia + tipo + nivel.
- Se pueden asociar comportamientos a una conducta.
- Se pueden crear niveles de pirámide ocupacional y mantener orden jerárquico.
- Se puede asociar matriz de competencias por puesto con múltiples entradas.
- Todas las validaciones de tipo/estado/dependencia ocurren en backend.
- Todas las operaciones respetan tenant y permisos.
- Todas las operaciones de escritura quedan auditadas.

---

## 12) Plan de pruebas
### Unit tests
- Validaciones de combinación competencia-tipo-nivel.
- Reglas de unicidad por nivel de pirámide.
- Bloqueos por dependencias activas.
- Reglas de concurrencia.

### Integration tests (HTTP)
- CRUD de catálogos base con permisos válidos/invalidos.
- CRUD de conductas y asociación de comportamientos.
- Actualización de matriz por puesto (casos permitidos y bloqueados).
- `403` tenant mismatch/permiso insuficiente.
- `409` conflictos de negocio/concurrencia.

### Validación final
- `dotnet build CLARIHR.slnx`
- `dotnet test CLARIHR.slnx --no-build`

---

## 13) Recomendaciones de implementación
Implementar por fases:
1. Extensión de catálogos base (tipos, niveles, comportamientos).
2. Conductas y relación con comportamientos.
3. Pirámide ocupacional.
4. Matriz de asociación por puesto.
5. Export, auditoría y hardening de performance.

---

## 14) Supuestos y decisiones cerradas
- Se mantiene compatibilidad de `JobProfiles` actual; la nueva matriz convivirá durante transición.
- La gestión de desempeño individual de empleados no entra en esta HU.
- "Eliminar" en catálogos se resuelve por inactivación (soft delete) en v1.
- La fuente de verdad para restricciones de negocio está en backend, no en frontend.
