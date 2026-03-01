# Company Users API

## Scope

HU-003 agrega administracion de usuarios por empresa sobre el tenant actual.

- `GET /api/company/users`
- `POST /api/company/users`
- `PUT /api/company/users/{userId}`
- `PATCH /api/company/users/{userId}/deactivate`
- `PATCH /api/company/users/{userId}/reactivate`
- `POST /api/company/users/{userId}/reset-invitation`

Todos los endpoints requieren JWT con claim `tid` y permiso tenant-scoped `RBAC.USERS.MANAGE` o `iam.administration.manage`.

## Tenant rules

- Solo retorna usuarios del tenant actual.
- No permite asignar roles de otro tenant.
- No permite gestionar usuarios de otra empresa.
- El `CompanyId` nunca viaja en el request y no puede ser cambiado por API.

## User states

- `PendingActivation`: usuario invitado, sin login habilitado.
- `Active`: usuario activo.
- `Inactive`: usuario desactivado, login bloqueado.

`user_companies.status` se mantiene alineado con la membresia activa/inactiva del tenant. Para usuarios invitados se mantiene:

- `auth_users.status = PendingActivation`
- `user_companies.status = Active`
- `iam_users.is_active = false`

## Invite request

```json
{
  "email": "jane@acme.test",
  "firstName": "Jane",
  "lastName": "Doe",
  "roleId": "2fdaf5cc-3d79-4f93-9e58-31bc96bf39d6"
}
```

## Invite response

```json
{
  "user": {
    "id": "665e4088-cd54-4d33-b6d0-8c7b48055d32",
    "email": "jane@acme.test",
    "firstName": "Jane",
    "lastName": "Doe",
    "roleId": "2fdaf5cc-3d79-4f93-9e58-31bc96bf39d6",
    "role": "Usuario Estandar",
    "status": "PendingActivation"
  },
  "invitationExpiresUtc": "2026-03-04T12:00:00Z"
}
```

## List response

La respuesta usa el contrato paginado del backend:

```json
{
  "items": [
    {
      "id": "665e4088-cd54-4d33-b6d0-8c7b48055d32",
      "email": "jane@acme.test",
      "firstName": "Jane",
      "lastName": "Doe",
      "roleId": "2fdaf5cc-3d79-4f93-9e58-31bc96bf39d6",
      "role": "Usuario Estandar",
      "status": "PendingActivation"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

## Business safeguards

- No hay eliminacion fisica.
- No se puede dejar a la empresa sin al menos un admin activo.
- Desactivar usuario invalida refresh tokens activos.
- `reset-invitation` invalida tokens anteriores y genera uno nuevo.
- Usuarios `Inactive` o `PendingActivation` no pueden autenticarse por login externo existente.

## Email delivery

En esta etapa `IEmailService` publica un evento/log estructurado con la invitacion. No expone el token por API.

## Testing checklist

1. Registrar o autenticar un admin de empresa para obtener JWT con `tid`.
2. Consultar `GET /api/company/users` y verificar que solo aparecen usuarios del tenant actual.
3. Invitar un usuario nuevo con `POST /api/company/users` y validar `PendingActivation`.
4. Confirmar en BD:
   - `auth_users.status = PendingActivation`
   - `user_companies.status = Active`
   - `company_invitation_tokens` tiene un token activo
5. Desactivar un usuario con `PATCH /api/company/users/{userId}/deactivate`.
6. Reactivarlo con `PATCH /api/company/users/{userId}/reactivate`.
7. Reemitir invitacion con `POST /api/company/users/{userId}/reset-invitation` y verificar token previo revocado.
