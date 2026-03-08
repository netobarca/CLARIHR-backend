# Company Provisioning

## Scope

HU-002 ejecuta provisioning inicial al finalizar:

- `POST /api/auth/register`
- `POST /api/auth/external`

HU-009 reutiliza el mismo motor de provisioning para crear empresas adicionales desde:

- `POST /api/account/companies`

No expone endpoint tecnico separado de provisioning; se reutiliza internamente desde auth y account companies.

## Provisioned resources

Cuando el usuario no tiene empresa primaria, el backend crea en una sola transaccion:

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
- `UserCompanyMembership` primaria con rol admin
- base de ubicaciones tenant-scoped:
  - `LocationHierarchyConfig`
  - nivel `General`
  - grupo default `GENERAL`

## Initial provisioning idempotency

- Si el usuario ya tiene empresa primaria, el provisioning no duplica company ni seeds.
- El flujo retorna exito interno `AlreadyProvisioned`.

## Additional company behavior

- Cuando la empresa se crea desde `POST /api/account/companies`, el provisioning:
  - crea tenant nuevo y recursos base
  - exige `initialLegalRepresentative` y lo crea en la misma transaccion
  - crea membership activa no primaria
  - no cambia el `tid` del token actual

## Input requirements

- `POST /api/auth/register` requiere `initialLegalRepresentative`.
- `POST /api/account/companies` requiere `initialLegalRepresentative`.
- `POST /api/auth/external` exige `initialLegalRepresentative` solo cuando provisiona una empresa nueva.
- El cambio de empresa activa se hace despues con `POST /api/account/companies/{companyId}/switch`

## Token behavior

- El `accessToken` emitido despues del provisioning incluye claim `tid` con el `PublicId` de la empresa primaria.
- Ese claim habilita el uso inmediato de endpoints tenant-scoped como IAM.
- En empresas adicionales, el nuevo tenant no se vuelve activo automaticamente.

## Plan gating

- El backend evalua entitlements por plan via `IPlanEntitlementService`.
- En esta iteracion `FREE` habilita:
  - `RBAC`
  - `USERS`
- Si un modulo no esta habilitado por el plan, la API responde `403` aunque el rol tenga permiso.

## Testing checklist

1. Registrar un usuario nuevo con `POST /api/auth/register`.
2. Verificar respuesta `201` con `refreshToken`.
3. Decodificar `accessToken` y confirmar claim `tid`.
4. Usar ese token para llamar endpoints `/api/iam/*`.
5. Confirmar en BD:
   - `companies`
   - `company_subscriptions`
   - `plan_entitlements`
   - `user_companies`
   - `iam_users`, `iam_roles`, `iam_permissions`
   - `location_hierarchy_configs`
   - `location_levels`
   - `location_groups`

## Failure behavior

- Si falla cualquier paso del provisioning, el registro retorna `500` con code `provisioning.failed`.
- La transaccion revierte usuario, empresa, suscripcion, membership, seeds IAM, seed de Locations y refresh token.
