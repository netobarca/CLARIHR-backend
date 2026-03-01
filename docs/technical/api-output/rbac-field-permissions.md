# RBAC Field Permissions

## Scope

HU-006 agrega RBAC nivel 3 por campo sobre los recursos ya protegidos por matriz.

En esta entrega el catalogo inicial queda sobre `RBAC_USERS` y se aplica de forma efectiva al modulo `CompanyUsers`.

Campos iniciales:

- `RBAC_USERS.EMAIL`
- `RBAC_USERS.FIRST_NAME`
- `RBAC_USERS.LAST_NAME`
- `RBAC_USERS.ROLE`
- `RBAC_USERS.STATUS`

`RBAC_USERS.ID` permanece visible y fuera de configuracion para no perder visibilidad minima del sistema.

## Endpoints

- `GET /api/rbac/resources/{resourceKey}/fields`
- `GET /api/rbac/roles/{roleId}/field-permissions?resourceKey=RBAC_USERS`
- `PUT /api/rbac/roles/{roleId}/field-permissions`

Request de actualizacion:

```json
{
  "resourceKey": "RBAC_USERS",
  "fields": [
    {
      "fieldKey": "RBAC_USERS.EMAIL",
      "isVisible": true,
      "isEditable": false,
      "isRequired": false,
      "isMasked": true
    }
  ]
}
```

## Enforcement

- Si el rol objetivo no tiene `Access` sobre el recurso, la configuracion por campo se rechaza.
- `isVisible=false` se normaliza con `isEditable=false`, `isRequired=false` e `isMasked=false`.
- Las lecturas de `GET /api/company/users` filtran o enmascaran valores segun el perfil efectivo del usuario actual.
- `POST /api/company/users` y `PUT /api/company/users/{userId}` bloquean escritura si el campo no es editable.
- El backend nunca confia en la UI; la validacion ocurre en servidor.

## Data model

- `field_catalog`: catalogo global de campos configurables
- `role_field_permissions`: override tenant-scoped por rol y campo
- `field_permission_audit_logs`: auditoria de cambios sobre HU-006

Los overrides por rol se cachean en memoria por `(tenant, role, resource)` y se invalidan al actualizar la configuracion.
