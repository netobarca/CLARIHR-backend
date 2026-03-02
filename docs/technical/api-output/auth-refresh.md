# Auth Refresh API

## Endpoint

- `POST /api/auth/refresh`

## Request

```json
{
  "refreshToken": "<refresh-token>"
}
```

## Success response

```json
{
  "accessToken": "jwt",
  "refreshToken": "new-refresh-token",
  "expiresIn": 900,
  "user": {
    "id": "8d57da13-40f9-4cc7-aeb2-c238dd9d2943",
    "email": "ana@clarihr.test",
    "firstName": "Ana",
    "lastName": "Mendoza",
    "authProvider": "Local"
  }
}
```

## Behavior

- `200 OK` cuando el refresh token es valido y se rota
- `400 Bad Request` para request invalido
- `401 Unauthorized` si el refresh token es invalido, expiro o fue reutilizado
- `500 Internal Server Error` si JWT no esta configurado

## Notes

- Los refresh tokens se almacenan hasheados en `auth_refresh_tokens`
- Cada uso valido rota el refresh token anterior
- Si se detecta reuse de un refresh token ya rotado, se revoca la familia activa
- El `accessToken` emitido usa la empresa primaria vigente del usuario al momento del refresh
- Si el usuario hizo `switch` de empresa con `POST /api/account/companies/{companyId}/switch`, el refresh siguiente conserva ese contexto primario actualizado
- Los endpoints `/api/auth/*` responden con headers `Cache-Control: no-store`
