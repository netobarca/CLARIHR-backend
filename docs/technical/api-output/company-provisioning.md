# Company Provisioning

## Scope

HU-002 ejecuta provisioning inicial al finalizar:

- `POST /api/auth/register`
- `POST /api/auth/external`

No expone endpoint nuevo para crear empresa inicial.

## Provisioned resources

Cuando el usuario no tiene empresa primaria, el backend crea en una sola transaccion:

- `Company` activa
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

## Idempotency

- Si el usuario ya tiene empresa primaria, el provisioning no duplica company ni seeds.
- El flujo retorna exito interno `AlreadyProvisioned`.

## Token behavior

- El `accessToken` emitido despues del provisioning incluye claim `tid` con el `PublicId` de la empresa primaria.
- Ese claim habilita el uso inmediato de endpoints tenant-scoped como IAM.

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

## Failure behavior

- Si falla cualquier paso del provisioning, el registro retorna `500` con code `provisioning.failed`.
- La transaccion revierte usuario, empresa, suscripcion, membership, seeds IAM y refresh token.
