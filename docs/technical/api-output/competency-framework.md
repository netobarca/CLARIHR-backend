# Competency Framework

## Scope

HU-018 introduce el modulo tenant-scoped para gestionar competencias asociadas a puestos:

- niveles de piramide ocupacional
- conductas por competencia/tipo/nivel
- asociacion de comportamientos por conducta
- matriz de expectativas por `JobProfile`
- export de matriz (`json|csv|xlsx`)

## Endpoints

Piramide ocupacional:

- `GET /api/v1/companies/{companyId}/occupational-pyramid-levels`
- `GET /api/v1/occupational-pyramid-levels/{id}`
- `POST /api/v1/companies/{companyId}/occupational-pyramid-levels`
- `PUT /api/v1/occupational-pyramid-levels/{id}`
- `PATCH /api/v1/occupational-pyramid-levels/{id}/activate`
- `PATCH /api/v1/occupational-pyramid-levels/{id}/inactivate`

Conductas:

- `GET /api/v1/companies/{companyId}/competency-conducts`
- `GET /api/v1/competency-conducts/{id}`
- `POST /api/v1/companies/{companyId}/competency-conducts`
- `PUT /api/v1/competency-conducts/{id}`
- `PATCH /api/v1/competency-conducts/{id}/activate`
- `PATCH /api/v1/competency-conducts/{id}/inactivate`
- `PUT /api/v1/competency-conducts/{id}/behaviors`

Matriz por puesto:

- `GET /api/v1/job-profiles/{id}/competency-matrix`
- `PUT /api/v1/job-profiles/{id}/competency-matrix`
- `GET /api/v1/job-profiles/{id}/competency-matrix/export?format=json|csv|xlsx`

## Security

- Requiere JWT.
- El `companyId` de la ruta debe coincidir con el claim `tid`.
- Lectura:
  - `CompetencyFramework.Read`
  - `CompetencyFramework.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura:
  - `CompetencyFramework.Admin`
  - `iam.administration.manage`
  - `platform_admin`

## Reglas importantes

- `code` de nivel de piramide es unico por tenant.
- `levelOrder` de nivel de piramide es unico por tenant.
- Una conducta es unica por tenant para combinacion:
  - `competencyId`
  - `competencyTypeId`
  - `behaviorLevelId`
  - `normalizedDescription`
- Inactivacion de niveles y conductas se bloquea si tienen dependencias activas.
- `concurrencyToken` es obligatorio en updates por id.
- La matriz valida referencias tenant-scoped activas (piramide, catalogos y conductas).
- `job_profile_competencies.expected_level` se mantiene sin ruptura en v1.

## Contratos principales

Escrituras:

- `CreateOccupationalPyramidLevelRequest`
  - `code`, `name`, `levelOrder`, `description?`
- `UpdateOccupationalPyramidLevelRequest`
  - `code`, `name`, `levelOrder`, `description?`, `concurrencyToken`
- `CreateCompetencyConductRequest`
  - `competencyId`, `competencyTypeId`, `behaviorLevelId`, `description`, `sortOrder`
- `UpdateCompetencyConductRequest`
  - `competencyId`, `competencyTypeId`, `behaviorLevelId`, `description`, `sortOrder`, `concurrencyToken`
- `UpdateCompetencyConductBehaviorsRequest`
  - `behaviors[]` (`behaviorId`, `notes?`, `sortOrder`), `concurrencyToken`
- `UpdateJobProfileCompetencyMatrixRequest`
  - `items[]` (`occupationalPyramidLevelId`, `competencyId`, `competencyTypeId`, `behaviorLevelId`, `conductIds[]`, `expectedEvidence?`, `sortOrder`)
  - `concurrencyToken`

Lecturas:

- `OccupationalPyramidLevelResponse`
- `CompetencyConductResponse`
- `JobProfileCompetencyMatrixResponse`

## Catalogos base reutilizados

Los catalogos del framework se administran por endpoints existentes de `job-catalogs`:

- `CompetencyType`
- `BehaviorLevel`
- `Behavior`

No existen endpoints dedicados para esos tres catalogos en HU-018.

## Errores esperados

- `400` validacion o formato export invalido (`COMPETENCY_FRAMEWORK_EXPORT_FORMAT_INVALID`)
- `403` forbidden (`COMPETENCY_FRAMEWORK_FORBIDDEN`) o tenant mismatch (`TENANT_MISMATCH`)
- `404` recursos no encontrados (`COMPETENCY_CONDUCT_NOT_FOUND`, `OCCUPATIONAL_PYRAMID_LEVEL_NOT_FOUND`, `JOB_PROFILE_NOT_FOUND`)
- `409` concurrencia/uso/dependencias (`CONCURRENCY_CONFLICT`, `OCCUPATIONAL_PYRAMID_LEVEL_IN_USE`, `RESOURCE_IN_USE`, `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT`)
- `422` conflictos semanticos de matriz cuando aplica

## Auditoria

Eventos auditados:

- `OCCUPATIONAL_PYRAMID_LEVEL_CREATED`
- `OCCUPATIONAL_PYRAMID_LEVEL_UPDATED`
- `OCCUPATIONAL_PYRAMID_LEVEL_ACTIVATED`
- `OCCUPATIONAL_PYRAMID_LEVEL_INACTIVATED`
- `COMPETENCY_CONDUCT_CREATED`
- `COMPETENCY_CONDUCT_UPDATED`
- `COMPETENCY_CONDUCT_ACTIVATED`
- `COMPETENCY_CONDUCT_INACTIVATED`
- `COMPETENCY_BEHAVIOR_LINKED`
- `JOB_PROFILE_COMPETENCY_MATRIX_UPDATED`
- `REPORT_EXPORTED` (export de matriz)
