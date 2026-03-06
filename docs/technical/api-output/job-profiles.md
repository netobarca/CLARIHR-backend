# Job Profiles

## Scope

HU-012 introduce el modulo tenant-scoped para manual descriptivo de puestos:

- CRUD de perfiles de puesto
- estado `Draft | Published | Archived`
- catalogos por categoria con activacion/inactivacion
- creacion inline de catalogos durante comandos de perfil (con permiso explicito)
- salidas para vacantes, print y export (`json/csv`)

## Endpoints

Perfiles:

- `POST /api/v1/companies/{companyId}/job-profiles`
- `GET /api/v1/companies/{companyId}/job-profiles`
- `GET /api/v1/job-profiles/{id}`
- `PUT /api/v1/job-profiles/{id}`
- `PATCH /api/v1/job-profiles/{id}/publish`
- `PATCH /api/v1/job-profiles/{id}/archive`
- `GET /api/v1/job-profiles/{id}/vacancy-template`
- `GET /api/v1/job-profiles/{id}/print`
- `GET /api/v1/job-profiles/{id}/export?format=json|csv`

Catalogos:

- `GET /api/v1/companies/{companyId}/job-catalogs/{category}`
- `POST /api/v1/companies/{companyId}/job-catalogs/{category}`
- `PATCH /api/v1/job-catalogs/{id}/activate`
- `PATCH /api/v1/job-catalogs/{id}/inactivate`

## Security

- Requiere JWT.
- El `companyId` de la ruta debe coincidir con el claim `tid`.
- Lectura de perfiles:
  - `JobProfiles.Read`
  - `JobProfiles.Admin`
  - `JobCatalogs.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura de perfiles:
  - `JobProfiles.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura de catalogos (incluye inline):
  - `JobCatalogs.Admin`
  - `iam.administration.manage`
  - `platform_admin`

## Reglas importantes

- `code` de perfil es unico por tenant.
- `concurrencyToken` se valida en operaciones de escritura por id.
- Publicar exige minimo: `objective`, al menos un `requirement`, al menos una `function`, y `responsibilities`.
- No se permiten ciclos en `reportsToJobProfileId` ni en `dependentPositions`.
- Inline catalog create solo es valido cuando `allowInlineCatalogCreate=true` y el usuario tiene permiso de catalogos.
- Export soportado en v1: `json` y `csv`.

## Contratos principales

Perfiles:

- `CreateJobProfileRequest`
  - `code`, `title`, `objective?`, `orgUnitId?`, `reportsToJobProfileId?`
  - `decisionScope?`, `assignedResources?`, `responsibilities?`
  - `benefitsSummary?`, `workingConditionSummary?`, `marketSalaryReference?`, `valuationNotes?`
  - `effectiveFromUtc?`, `effectiveToUtc?`
  - `allowInlineCatalogCreate`
  - `requirements[]`, `functions[]`, `relations[]`, `competencies[]`, `trainings[]`, `compensations[]`, `benefits[]`, `workingConditions[]`, `dependentPositions[]`
- `UpdateJobProfileRequest`
  - mismo contrato que create + `concurrencyToken`
- `ConcurrencyRequest`
  - `concurrencyToken`

Catalogos:

- `CreateJobCatalogItemRequest`
  - `code`, `name`
- `ConcurrencyRequest`
  - `concurrencyToken`

Respuestas:

- `JobProfileListItemResponse` para listados paginados
- `JobProfileResponse` para detalle completo
- `JobProfileVacancyTemplateResponse` para consumo de vacantes
- `JobProfilePrintResponse` para print/export
- `JobCatalogItemResponse` para catalogos

## Filtros de busqueda

`GET /api/v1/companies/{companyId}/job-profiles`:

- `status?`
- `orgUnitId?`
- `salaryClass?`
- `q?`
- `page`
- `pageSize`

`GET /api/v1/companies/{companyId}/job-catalogs/{category}`:

- `q?`
- `isActive?`
- `page`
- `pageSize`

## Errores esperados

- `400` validacion
- `403` forbidden o tenant mismatch
- `404` perfil/catalogo no encontrado
- `409` conflictos (codigo, ciclo, estado, concurrencia, item inactivo)
- `422` publicacion incompleta

## Auditoria

Eventos auditados:

- `JOB_PROFILE_CREATED`
- `JOB_PROFILE_UPDATED`
- `JOB_PROFILE_PUBLISHED`
- `JOB_PROFILE_ARCHIVED`
- `JOB_CATALOG_ITEM_CREATED`
- `JOB_CATALOG_ITEM_UPDATED`
