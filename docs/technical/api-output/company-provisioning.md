# Company Provisioning

## Scope

El provisioning de empresa se ejecuta desde:

- `POST /api/account/companies`

`POST /api/auth/register` y `POST /api/auth/external` ya no ejecutan provisioning.
Ambos endpoints solo crean/autentican usuario y emiten tokens.

## Provisioned resources

Cuando se crea una empresa desde `POST /api/account/companies`, el backend provisiona en una sola transaccion:

- `Company` activa
- representante legal inicial activo (`LegalRepresentative`)
- `CompanySubscription` con plan `FREE`
- `PlanEntitlement` base para `FREE` si aun no existe
- roles base por tenant:
  - `Admin de Empresa`
  - `Usuario Estandar`
- permisos admin base:
  - `iam.administration.manage`
  - `RBAC.USERS.MANAGE`
  - `RBAC.ROLES.MANAGE`
  - `RBAC.PERMISSIONS.MANAGE`
- `IamUser` enlazado al mismo `PublicId` del usuario auth
- `UserCompanyMembership` (primaria solo cuando el usuario aun no tiene empresa primaria)
- base de ubicaciones tenant-scoped:
  - `LocationHierarchyConfig`
  - nivel `General`
  - grupo default `GENERAL`

## Input requirements

- `POST /api/account/companies` requiere `initialLegalRepresentative`.
- El cambio de empresa activa se hace despues con `POST /api/account/companies/{companyId}/switch`.

## Token behavior

- El token emitido por `register/external/login/refresh` puede no traer `tid` si el usuario aun no tiene empresa primaria.
- Despues de crear/switch de empresa (`POST /api/account/companies/{companyId}/switch`), el nuevo token incluye claim `tid`.
- En creacion de empresas adicionales, el tenant nuevo no se vuelve activo automaticamente.

## Plan gating

- El backend evalua entitlements por plan via `IPlanEntitlementService`.
- En esta iteracion `FREE` habilita:
  - `RBAC`
  - `USERS`
- Si un modulo no esta habilitado por el plan, la API responde `403` aunque el rol tenga permiso.

## Testing checklist

1. Registrar un usuario con `POST /api/auth/register`.
2. Verificar respuesta `201` con `refreshToken` y token sin `tid`.
3. Crear primera empresa con `POST /api/account/companies`.
4. Cambiar contexto con `POST /api/account/companies/{companyId}/switch`.
5. Decodificar token de `switch` y confirmar claim `tid`.
6. Usar ese token para llamar endpoints tenant-scoped (`/api/iam/*`).
7. Confirmar en BD:
   - `companies`
   - `company_subscriptions`
   - `plan_entitlements`
   - `user_companies`
   - `iam_users`, `iam_roles`, `iam_permissions`
   - `location_hierarchy_configs`
   - `location_levels`
   - `location_groups`

## Failure behavior

- Si falla cualquier paso del provisioning en `POST /api/account/companies`, responde `500` con code `provisioning.failed`.
- La transaccion revierte empresa, suscripcion, membership, seeds IAM y seed de Locations.
