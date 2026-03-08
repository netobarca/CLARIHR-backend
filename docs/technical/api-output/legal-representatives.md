# Legal Representatives

## Scope

HU-016 introduce administracion tenant-scoped de representantes legales:

- alta, actualizacion y consulta por empresa
- activacion/inactivacion y `set-primary`
- regla minima: siempre al menos 1 representante legal activo por empresa
- export tabular (`csv|xlsx`)

HU-017 agrega politica transversal:

- `allowedActions` en detalle
- `includeAllowedActions` en listados

## Endpoints

- `POST /api/v1/companies/{companyId}/legal-representatives`
- `GET /api/v1/companies/{companyId}/legal-representatives`
- `GET /api/v1/legal-representatives/{id}`
- `PUT /api/v1/legal-representatives/{id}`
- `PATCH /api/v1/legal-representatives/{id}/activate`
- `PATCH /api/v1/legal-representatives/{id}/inactivate`
- `PATCH /api/v1/legal-representatives/{id}/set-primary`
- `GET /api/v1/legal-representatives/{id}/usage`
- `GET /api/v1/companies/{companyId}/legal-representatives/export?format=csv|xlsx`

## Filtros de busqueda

`GET /api/v1/companies/{companyId}/legal-representatives`:

- `isActive?`
- `isPrimary?`
- `representationType?`
- `q?`
- `page`
- `pageSize`
- `includeAllowedActions` (default `false`)

## Contratos principales

- `LegalRepresentativeListItemResponse`
- `LegalRepresentativeResponse`
- `LegalRepresentativeUsageResponse`
- `LegalRepresentativeExportRow`

Los contratos de listado/detalle incluyen `allowedActions` cuando aplica la politica transversal.
