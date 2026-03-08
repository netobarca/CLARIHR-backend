# Auth Login API

## Endpoint

- `POST /api/auth/login`

## Request

```json
{
  "email": "ana@clarihr.test",
  "password": "StrongP@ss1"
}
```

## Success response

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
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

## Validation rules

- `email` requerido, formato valido, max 320
- `password` requerida, max 100

## Error behavior

- `400 Bad Request` para validacion
- `401 Unauthorized` si el usuario no existe, no es local, esta inactivo o el password no coincide (`auth.login.invalid_credentials`)
- `500 Internal Server Error` si JWT no esta configurado

## Notes

- El login local solo aplica a usuarios `authProvider=Local`
- El endpoint emite un nuevo par `accessToken + refreshToken`
- Si tu refresh token expiro/revoco, usa este endpoint para reautenticacion
