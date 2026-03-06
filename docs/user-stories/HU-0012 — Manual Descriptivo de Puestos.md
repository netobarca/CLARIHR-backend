# HU-0012 — Manual Descriptivo de Puestos (Perfiles de Puesto + Catálogos Dinámicos)

## 1) Descripción del requerimiento (visión de negocio)
Como **Dueño de la cuenta / Administrador de Empresa / RRHH**,
quiero **crear y administrar el Manual Descriptivo de Puestos**,
registrando por cada puesto su objetivo, requisitos, funciones generales y específicas, relaciones internas y externas, competencias, capacitaciones, clases salariales, toma de decisiones, dotación/equipo asignado, responsabilidades, prestaciones, valuación del puesto, condición laboral, referencia de salarios de mercado y plazas dependientes,
para que la organización tenga una **fuente única, auditable y reutilizable** para gestión interna, publicación de vacantes y documentación oficial.

Regla clave de negocio:
- El sistema debe operar con **catálogos prealimentados** por categoría.
- Si durante la captura falta una categoría/opción, debe poder **crearse en el momento** (inline), con controles de permiso y auditoría.

---

## 2) Objetivo funcional (qué habilita)
- Estandarizar la definición de puestos por empresa (tenant).
- Reducir ambigüedad en reclutamiento, compensaciones y gestión de desempeño.
- Reutilizar la información del perfil para:
  - publicación de plazas vacantes
  - generación de descripción de puesto
  - impresión y exportación de datos
  - reportes de estructura, responsabilidad y remuneración
- Trazabilidad completa de cambios sobre perfiles y catálogos.

---

## 3) Alcance API (backend)
### Incluye
- CRUD de perfiles de puesto (`JobProfile`) por empresa.
- Gestión de secciones del perfil (objetivo, requisitos, funciones, relaciones, etc.).
- Estado del perfil: `Draft | Published | Archived`.
- Catálogos tenant-scoped por categoría, con creación inline controlada.
- Consultas:
  - listado paginado con filtros
  - detalle completo por perfil
  - versión lista para vacante/publicación
  - formato para impresión/exportación (`json/csv`, dejando `pdf/xlsx` preparado)

### Fuera de alcance (por ahora, pero se deja preparado)
- Workflow formal de aprobación multinivel del perfil.
- Integración automática con bolsas de empleo externas.
- Motor de analítica salarial avanzada (percentiles, bandas automáticas).
- Firma digital del manual de puesto.

---

## 4) Actores y permisos
- **AccountOwner / Dueño de la cuenta**: administración total en su empresa activa.
- **CompanyAdmin / Admin de Empresa**: administración funcional completa de perfiles y catálogos.
- **HRAdmin / HR Specialist**: crea/edita/perfila puestos.
- **HRRead / HiringManagerRead**: solo consulta/descarga.

Permisos sugeridos (siguiendo patrón actual de módulos funcionales):
- `JobProfiles.Read`
- `JobProfiles.Admin`
- `JobCatalogs.Admin`
- `iam.administration.manage` (override administrativo)
- `platform_admin` (override plataforma)

Regla base:
- La API valida siempre coincidencia entre `companyId` de la ruta y claim `tid`.

---

## 5) Datos que debe manejar el backend (modelo mínimo)

### 5.1 Entidad principal: `JobProfile` (Perfil de Puesto)
Campos sugeridos:
- `Id` (GUID público)
- `CompanyId` (GUID, obligatorio)
- `Code` (string, único por empresa, obligatorio)
- `Title` (string, obligatorio)
- `Objective` (string, obligatorio para publicar)
- `OrgUnitId` (GUID, opcional al crear; recomendado para publicar)
- `ReportsToJobProfileId` (GUID, opcional)
- `DecisionScope` (string, opcional)
- `AssignedResources` (string, opcional)
- `Responsibilities` (string, opcional)
- `BenefitsSummary` (string, opcional)
- `WorkingConditionSummary` (string, opcional)
- `MarketSalaryReference` (string, opcional)
- `ValuationNotes` (string, opcional)
- `Status` (enum): `Draft | Published | Archived`
- `Version` (int)
- `EffectiveFrom` (date, opcional)
- `EffectiveTo` (date, opcional)
- `IsActive` (bool)
- `CreatedAt`, `UpdatedAt`
- `ConcurrencyToken` (GUID)

### 5.2 Entidades relacionadas del perfil
- `JobProfileRequirement`: estudios, experiencia, conocimientos, certificaciones.
- `JobProfileFunction`: funciones `General | Specific` con orden.
- `JobProfileRelation`: relaciones `Internal | External`.
- `JobProfileCompetency`: competencia + nivel esperado.
- `JobProfileTraining`: capacitación requerida/recomendada.
- `JobProfileCompensation`: clase salarial, rango y tipo de jornada.
- `JobProfileBenefit`: prestaciones asociadas.
- `JobProfileWorkingCondition`: condición laboral (riesgo, modalidad, horario).
- `JobProfileDependentPosition`: plazas/puestos dependientes (cantidad).

### 5.3 Catálogos dinámicos
- `JobCatalogItem` por tenant y categoría:
  - `Category` (enum/string controlado)
  - `Code`
  - `Name`
  - `IsSystem` (prealimentado)
  - `IsActive`
- Categorías mínimas:
  - `EducationLevel`, `KnowledgeArea`, `Competency`, `Training`, `SalaryClass`, `BenefitType`, `WorkingCondition`, `RelationType`, `DecisionLevel`.

---

## 6) Reglas de negocio (backend)

### RN-01 Unicidad y consistencia
- `Code` de perfil es único por empresa (`Unique(CompanyId, Code)`).
- `OrgUnitId` y `ReportsToJobProfileId` deben pertenecer a la misma empresa.

### RN-02 Estados y publicación
- `Draft`: editable totalmente.
- `Published`: visible para consumo por vacantes/impresión/exportación.
- `Archived`: no editable (solo consulta histórica).
- Para pasar a `Published`, validar mínimo:
  - objetivo
  - al menos un requisito
  - al menos una función general o específica
  - al menos una responsabilidad

### RN-03 Catálogos y captura inline
- Valores categorizados deben referenciar catálogo activo.
- Si no existe opción y el usuario tiene `JobCatalogs.Admin`, puede crearla inline en la misma transacción.
- Si no tiene permiso, responder conflicto/forbidden con detalle.

### RN-04 Relaciones internas y externas
- Relación `Internal` se vincula con unidad/rol/área interna.
- Relación `External` se vincula con cliente/proveedor/entidad regulatoria u otro actor externo (catálogo + texto libre opcional).

### RN-05 Plazas dependientes
- No permitir dependencias cíclicas entre perfiles (`A -> B -> A`).
- Cantidad de plazas dependientes debe ser `>= 0`.

### RN-06 Concurrencia y auditoría
- Toda operación de escritura valida `ConcurrencyToken`.
- Toda escritura deja rastro en auditoría (`before/after`).

### RN-07 Seguridad de datos sensibles
- Bloques salariales y valuación pueden restringirse por permiso/rol.
- Exportaciones deben respetar permisos del solicitante.

---

## 7) Requerimientos técnicos del Backend (arquitectura + persistencia)

### RT-01 Arquitectura
- Mantener patrón actual del backend:
  - Clean Architecture
  - CQRS (`Commands/Queries + Handlers`)
  - FluentValidation
  - `ProblemDetails` para errores
  - tenant-scoped por `tid`

### RT-02 Persistencia (PostgreSQL)
Tablas mínimas sugeridas:
- `job_profiles`
- `job_profile_requirements`
- `job_profile_functions`
- `job_profile_relations`
- `job_profile_competencies`
- `job_profile_trainings`
- `job_profile_compensations`
- `job_profile_benefits`
- `job_profile_working_conditions`
- `job_profile_dependent_positions`
- `job_catalog_items`

Índices recomendados:
- `uq_job_profiles__tenant_code`
- `ix_job_profiles__tenant_status`
- `ix_job_profiles__tenant_org_unit`
- `ix_job_catalog_items__tenant_category_name`
- índices por FK en tablas hijas

### RT-03 Rendimiento
- Listados paginados obligatorios (`page`, `pageSize`).
- Filtros por estado, unidad, clase salarial, texto (`q`).
- Carga de detalle por perfil con proyección optimizada (evitar N+1).
- Catálogos cacheables por tenant/categoría con TTL corto.

### RT-04 Seguridad y autorización
- Servicio de autorización funcional similar a `Locations` / `OrgUnits`.
- Validar acceso por claim/permiso y fallback por permisos del rol en DB.

### RT-05 Auditoría
Eventos mínimos sugeridos:
- `JOB_PROFILE_CREATED`
- `JOB_PROFILE_UPDATED`
- `JOB_PROFILE_PUBLISHED`
- `JOB_PROFILE_ARCHIVED`
- `JOB_CATALOG_ITEM_CREATED`

---

## 8) Endpoints propuestos (API v1)

### 8.1 Perfiles de puesto
- `POST   /api/v1/companies/{companyId}/job-profiles`
- `GET    /api/v1/companies/{companyId}/job-profiles?status=&orgUnitId=&salaryClass=&q=&page=&pageSize=`
- `GET    /api/v1/job-profiles/{id}`
- `PUT    /api/v1/job-profiles/{id}`
- `PATCH  /api/v1/job-profiles/{id}/publish`
- `PATCH  /api/v1/job-profiles/{id}/archive`

### 8.2 Catálogos
- `GET    /api/v1/companies/{companyId}/job-catalogs/{category}?q=&isActive=`
- `POST   /api/v1/companies/{companyId}/job-catalogs/{category}`
- `PATCH  /api/v1/job-catalogs/{id}/activate`
- `PATCH  /api/v1/job-catalogs/{id}/inactivate`

### 8.3 Consumo para vacantes e impresión/exportación
- `GET    /api/v1/job-profiles/{id}/vacancy-template`
- `GET    /api/v1/job-profiles/{id}/print`
- `GET    /api/v1/job-profiles/{id}/export?format=json|csv`

---

## 9) Contratos (DTOs) mínimos

### 9.1 `CreateJobProfileRequest`
- `code`*
- `title`*
- `objective`
- `orgUnitId`
- `reportsToJobProfileId`
- `requirements[]`
- `generalFunctions[]`
- `specificFunctions[]`
- `internalRelations[]`
- `externalRelations[]`
- `competencies[]`
- `trainings[]`
- `salaryClassId`
- `decisionScope`
- `assignedResources`
- `responsibilities`
- `benefits[]`
- `workingConditions[]`
- `dependentPositions[]`
- `allowInlineCatalogCreate` (bool)

### 9.2 `JobProfileResponse`
- `id`, `companyId`, `code`, `title`, `status`, `version`
- `objective`, `orgUnitId`, `reportsToJobProfileId`
- bloques del perfil (requirements/functions/relations/competencies/etc.)
- `createdAtUtc`, `updatedAtUtc`, `concurrencyToken`

### 9.3 `JobCatalogItemResponse`
- `id`, `category`, `code`, `name`, `isSystem`, `isActive`

### 9.4 `VacancyTemplateResponse`
- resumen estructurado del perfil listo para publicación de vacante

---

## 10) Errores y códigos recomendados
- 400 `VALIDATION_ERROR`
- 401 `UNAUTHENTICATED`
- 403 `FORBIDDEN` / `TENANT_MISMATCH`
- 404 `JOB_PROFILE_NOT_FOUND`
- 409 `JOB_PROFILE_CODE_CONFLICT`, `CONCURRENCY_CONFLICT`, `JOB_PROFILE_DEPENDENCY_CYCLE`
- 422 `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` (si se decide diferenciar reglas semánticas)

---

## 11) Criterios de aceptación (backend)
1. Puedo crear y actualizar un perfil de puesto por empresa con toda su estructura.
2. Puedo registrar funciones generales y específicas por perfil.
3. Puedo registrar relaciones internas y externas de forma estructurada.
4. Puedo asociar requisitos, competencias, capacitaciones, prestaciones y condiciones laborales.
5. Puedo seleccionar valores desde catálogos prealimentados.
6. Si falta un valor de catálogo, puedo crearlo inline (si tengo permiso) durante la captura.
7. Puedo publicar un perfil solo si cumple mínimos de completitud.
8. Puedo usar el perfil publicado para plantilla de vacante.
9. Puedo imprimir/exportar la información del perfil.
10. Toda operación relevante queda auditada con trazabilidad completa.
11. La API rechaza operaciones cross-tenant.
12. La API valida concurrencia en actualizaciones.

---

## 12) Recomendaciones de implementación (mejores prácticas HRIS)
- Implementar por fases para reducir riesgo:
  - **Fase 1:** núcleo del perfil + catálogos + publicación + auditoría.
  - **Fase 2:** exportaciones avanzadas (pdf/xlsx) + plantilla de vacante enriquecida.
- Mantener separación entre datos estructurados (catálogos) y texto libre, para facilitar reportes.
- Diseñar campos salariales/valuación con controles de visibilidad por rol.
- Incorporar versionado de perfil para trazabilidad legal/operativa.
- Preparar integración futura con módulos de:
  - Reclutamiento/vacantes
  - Desempeño
  - Compensaciones
  - Analítica de talento
