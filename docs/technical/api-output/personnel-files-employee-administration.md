# Personnel Files Employee Administration

## Scope

HU-PF-EMP-001 agrega el modulo administrativo integral del expediente de empleados sobre `PersonnelFiles`.

Cobertura:

- fase 1: nucleo laboral administrativo
- fase 2: historicos operativos y export
- fase 3: integraciones avanzadas via staging interno

## Endpoints

### Fase 1

- `POST /api/v1/personnel-files/{id}/hire`
- `PUT /api/v1/personnel-files/{id}/employee-profile`
- `PUT /api/v1/personnel-files/{id}/employment-assignments`
- `PUT /api/v1/personnel-files/{id}/contract-history`
- `GET /api/v1/personnel-files/{id}/position-hierarchy`
- `PUT /api/v1/personnel-files/{id}/salary-items`
- `PUT /api/v1/personnel-files/{id}/additional-benefits`
- `PUT /api/v1/personnel-files/{id}/payment-methods`
- `PUT /api/v1/personnel-files/{id}/authorization-substitutions`

### Fase 2

- `POST /api/v1/personnel-files/{id}/personnel-actions`
- `GET /api/v1/personnel-files/{id}/personnel-actions`
- `GET /api/v1/personnel-files/{id}/personnel-actions/export?format=csv|xlsx`
- `PUT /api/v1/personnel-files/{id}/payroll-transactions`
- `GET /api/v1/personnel-files/{id}/payroll-transactions`
- `GET /api/v1/personnel-files/{id}/payroll-transactions/export?format=csv|xlsx`
- `PUT /api/v1/personnel-files/{id}/assets-accesses`
- `PUT /api/v1/personnel-files/{id}/insurances`
- `PUT /api/v1/personnel-files/{id}/medical-claims`

### Fase 3

- `PUT /api/v1/personnel-files/{id}/evaluations`
- `GET /api/v1/personnel-files/{id}/evaluations`
- `PUT /api/v1/personnel-files/{id}/position-competency-results`
- `GET /api/v1/personnel-files/{id}/position-competencies`
- `PUT /api/v1/personnel-files/{id}/selection-contests`
- `GET /api/v1/personnel-files/{id}/selection-contests`
- `PUT /api/v1/personnel-files/{id}/curricular-competencies`

## Security

- JWT obligatorio.
- Tenant isolation por claim `tid`.
- Lectura: `PersonnelFiles.Read|Admin`.
- Escritura: `PersonnelFiles.Admin`.
- Exportes auditados con `REPORT_EXPORTED`.

## Reglas importantes

- `Candidate -> Employee` solo se permite con `POST /hire`.
- `PUT /personal-info` devuelve `PERSONNEL_FILE_HIRE_ENDPOINT_REQUIRED` si intenta esa conversion.
- Todas las escrituras por seccion usan `concurrencyToken`.
- Endpoints del modulo requieren `recordType=Employee` salvo `hire`.
- Historicos se consultan por endpoints dedicados paginados (no en `GET /personnel-files/{id}`).

## Filtros estandar en historicos

`GET /personnel-actions` y `GET /payroll-transactions`:

- `fromUtc`
- `toUtc`
- `type`
- `status`
- `q`
- `sortBy`
- `sortDirection`
- `page`
- `pageSize`

## Integracion staging

Entidades de fase 3 guardan metadata de origen para transicion a integraciones directas sin romper contrato:

- `source_system`
- `source_reference`
- `source_synced_utc`

## Persistencia

Scripts:

- `docs/technical/sql/hu028_personnel_files_employee_core.sql`
- `docs/technical/sql/hu029_personnel_files_employee_operations.sql`
- `docs/technical/sql/hu030_personnel_files_employee_integrations.sql`

Aplicacion automatica en bootstrap:

- `docker/postgres/init/001_apply_clarihr_schema.sql`

## Errores esperados

- `400` validacion
- `401` no autenticado
- `403` sin permiso o `TENANT_MISMATCH`
- `404` expediente no encontrado
- `409` `CONCURRENCY_CONFLICT`
- `422` `PERSONNEL_FILE_STATE_RULE_VIOLATION`, `PERSONNEL_FILE_HIRE_ENDPOINT_REQUIRED`
