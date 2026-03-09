# Auth Register API

## Endpoint

- `POST /api/auth/register`

## Request

```json
{
  "firstName": "Ana",
  "lastName": "Mendoza",
  "email": "ana@clarihr.test",
  "password": "StrongP@ss1",
  "country": "SV",
  "source": "landing-page"
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

- `firstName` requerido, max 100, solo letras, espacios, apostrofe y guion
- `lastName` requerido, max 100, solo letras, espacios, apostrofe y guion
- `email` requerido, formato valido, max 320
- `password` requerida, 8-100 caracteres, con mayuscula, minuscula, numero y caracter especial
- `country` opcional, max 100, sin caracteres de control
- `source` opcional, max 100, solo caracteres seguros para metadata

## Error behavior

- `400 Bad Request` para validacion
- `409 Conflict` si el email ya existe
- `500 Internal Server Error` si JWT no esta configurado

## Notes

- El usuario se persiste en `auth_users`
- El password se guarda hasheado
- El email se persiste normalizado en lowercase
- El registro no crea empresa ni representante legal
- El onboarding de empresa inicial se realiza despues del login con `POST /api/account/companies`
- El cambio de contexto tenant se realiza con `POST /api/account/companies/{companyId}/switch`
- Se emite refresh token persistido en `auth_refresh_tokens`
- La rotacion se realiza con `POST /api/auth/refresh`
- Si el refresh token deja de ser usable, la reautenticacion local se hace con `POST /api/auth/login`
