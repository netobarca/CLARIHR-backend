# Account Companies

## Scope

Este documento resume la implementacion tecnica del modulo de gestion multiempresa a nivel cuenta.

## Functional intent

- permitir que un usuario autenticado cree empresas adicionales
- listar solo las empresas que el usuario creo
- editar nombre de empresa
- archivar y reactivar empresa
- cambiar la empresa activa de la sesion sin romper el modelo tenant-scoped actual

## Key design decisions

1. El modulo es `account-level`, no `tenant-level`.
2. No usa `AuthorizeResource(...)` porque el RBAC actual sigue siendo por tenant.
3. La autorizacion se resuelve por ownership:
   - el usuario solo opera empresas cuyo `CreatedByUserPublicId` coincide con su cuenta
4. No existe hard delete.
5. La empresa activa se cambia actualizando la membership primaria y reemitiendo JWT con un nuevo claim `tid`.
6. El limite temporal de empresas activas se controla por configuracion:
   - `Companies:Ownership:MaxOwnedActiveCompanies`

## Main components

- `Features/AccountCompanies`
- `ICompanyOwnershipPolicy`
- `ICompanyProvisioningService`
- `AccountCompaniesController`
- `CompanyOwnershipOptions`

## Main endpoints

- `GET /api/account/companies`
- `GET /api/account/companies/{companyId}`
- `POST /api/account/companies`
- `PUT /api/account/companies/{companyId}`
- `PATCH /api/account/companies/{companyId}/archive`
- `PATCH /api/account/companies/{companyId}/reactivate`
- `POST /api/account/companies/{companyId}/switch`

## Contract updates

- `POST /api/account/companies` requiere `initialLegalRepresentative` para crear la empresa.
- `GET /api/account/companies/{companyId}` devuelve `activeLegalRepresentatives[]` (resumen de activos ordenado por `isPrimary desc`, `fullName asc`).
- Una empresa no puede quedar sin al menos un representante legal activo.

## Security notes

1. Solo el owner funcional puede leer o modificar la empresa.
2. No se puede archivar la empresa activa del token actual.
3. No se puede hacer `switch` a una empresa archivada.
4. La reactivacion valida de nuevo el limite de empresas activas.
5. La creacion adicional reutiliza el provisioning tenant-scoped actual para crear roles, permisos, membership y plan base.

## Data and query notes

- No se agregaron tablas nuevas.
- Se agrego el script `docs/technical/sql/hu009_account_companies.sql` para indices de ownership/listado.
- El conteo para la policy de cupo se resuelve por usuario y hoy se hace sobre un conjunto pequeno, por lo que una proyeccion a memoria es aceptable para este alcance.

## Audit events

- `COMPANY_CREATED`
- `COMPANY_UPDATED`
- `COMPANY_ARCHIVED`
- `COMPANY_REACTIVATED`
- `ACTIVE_COMPANY_SWITCHED`
