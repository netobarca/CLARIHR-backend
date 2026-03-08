# Salary Tabulator

## Scope

HU-014 introduce el modulo tenant-scoped de tabulador salarial y solicitudes de modificacion:

- lineas de tabulador por clase/escala
- solicitudes de cambio con estados `Draft|Submitted|Approved|Rejected|Canceled`
- aplicacion transaccional de cambios al aprobar
- validacion de politicas de montos/fechas y bloqueo de autoaprobacion
- export de lineas `csv|xlsx`

## Endpoints

- `GET /api/v1/companies/{companyId}/salary-tabulator`
- `GET /api/v1/salary-tabulator/lines/{id}`
- `GET /api/v1/companies/{companyId}/salary-tabulator/export?format=csv|xlsx`
- `POST /api/v1/companies/{companyId}/salary-tabulator/change-requests`
- `GET /api/v1/companies/{companyId}/salary-tabulator/change-requests`
- `GET /api/v1/salary-tabulator/change-requests/{id}`
- `PUT /api/v1/salary-tabulator/change-requests/{id}`
- `PATCH /api/v1/salary-tabulator/change-requests/{id}/submit`
- `PATCH /api/v1/salary-tabulator/change-requests/{id}/approve`
- `PATCH /api/v1/salary-tabulator/change-requests/{id}/reject`
- `PATCH /api/v1/salary-tabulator/change-requests/{id}/cancel`
- `GET /api/v1/salary-tabulator/change-requests/{id}/impact`

## Security

- Requiere JWT.
- El `companyId` de la ruta debe coincidir con el claim `tid`.
- Lectura:
  - `SalaryTabulator.Read`
  - `SalaryTabulator.Request`
  - `SalaryTabulator.Approve`
  - `SalaryTabulator.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura de solicitudes:
  - `SalaryTabulator.Request`
  - `SalaryTabulator.Admin`
  - overrides
- Aprobacion/Rechazo:
  - `SalaryTabulator.Approve`
  - `SalaryTabulator.Admin`
  - overrides

## Reglas importantes

- `baseAmount > 0`.
- Si existen min/max: `min <= base <= max`.
- `effectiveFromUtc` es obligatorio.
- `effectiveToUtc` no puede ser menor a `effectiveFromUtc`.
- `concurrencyToken` obligatorio en operaciones por id.
- No se permite autoaprobacion del solicitante salvo override de plataforma.
- Aprobar aplica cambios en una sola transaccion.

## Contratos principales

- `CreateSalaryTabulatorChangeRequest`
  - `reason`, `effectiveFromUtc`, `items[]`
- `UpdateSalaryTabulatorChangeRequest`
  - mismo contrato + `concurrencyToken`
- `SubmitSalaryTabulatorChangeRequest`
  - `concurrencyToken`
- `ApproveSalaryTabulatorChangeRequest`
  - `decisionComment`, `concurrencyToken`
- `RejectSalaryTabulatorChangeRequest`
  - `decisionComment`, `concurrencyToken`
- `CancelSalaryTabulatorChangeRequest`
  - `concurrencyToken`

Respuestas:

- `SalaryTabulatorLineListItemResponse`
- `SalaryTabulatorLineResponse`
- `SalaryTabulatorChangeRequestListItemResponse`
- `SalaryTabulatorChangeRequestResponse`
- `SalaryTabulatorChangeRequestImpactResponse`

Los contratos de listado/detalle de lineas y solicitudes incluyen `allowedActions` cuando `includeAllowedActions=true` en listados.

## Filtros de busqueda

`GET /api/v1/companies/{companyId}/salary-tabulator`:

- `salaryClass?`
- `salaryScale?`
- `isActive?`
- `q?`
- `page`
- `pageSize`
- `includeAllowedActions` (default `false`)

`GET /api/v1/companies/{companyId}/salary-tabulator/change-requests`:

- `status?`
- `requestedBy?`
- `effectiveFrom?`
- `effectiveTo?`
- `page`
- `pageSize`
- `includeAllowedActions` (default `false`)

## Errores esperados

- `400` validacion
- `403` forbidden o tenant mismatch
- `404` linea/solicitud inexistente
- `409` conflictos de estado, overlap o concurrencia
- `422` reglas de monto, fechas o politica de aprobacion

## Auditoria

Eventos auditados:

- `SALARY_TABULATOR_REQUEST_CREATED`
- `SALARY_TABULATOR_REQUEST_UPDATED`
- `SALARY_TABULATOR_REQUEST_SUBMITTED`
- `SALARY_TABULATOR_REQUEST_APPROVED`
- `SALARY_TABULATOR_REQUEST_REJECTED`
- `SALARY_TABULATOR_REQUEST_CANCELED`
- `SALARY_TABULATOR_LINE_APPLIED`
- `SALARY_TABULATOR_LINE_INACTIVATED`
