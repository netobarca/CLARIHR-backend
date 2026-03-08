# Auth External API

## Endpoint

- `POST /api/auth/external`

## Request

```json
{
  "provider": "Google",
  "idToken": "<google-id-token>",
  "companyName": "Acme HR",
  "initialLegalRepresentative": {
    "firstName": "Ana",
    "lastName": "Mendoza",
    "documentType": "TaxId",
    "documentNumber": "0614-290190-102-3",
    "positionTitle": "Representante Legal",
    "representationType": "PrimaryLegalRepresentative",
    "authorityDescription": "Representacion general",
    "appointmentInstrument": "Acta de nombramiento",
    "appointmentDateUtc": "2026-01-01T00:00:00Z",
    "effectiveFromUtc": "2026-01-01T00:00:00Z",
    "effectiveToUtc": null,
    "email": "ana@clarihr.test",
    "phone": "+50370000000",
    "isPrimary": true
  },
  "country": "SV",
  "source": "landing-page"
}
```

## Current provider support

- `Google`

En esta iteracion el backend valida el `idToken` real de Google.

## Configuration

Configura el `client_id` del backend con:

- variable de entorno `Authentication__Google__ClientId`
- o `Authentication:Google:ClientId` en configuracion

En desarrollo local ya queda seteado en [launchSettings.json](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Properties/launchSettings.json).

## Validation rules

El token debe cumplir, como minimo:

- firma valida con llaves publicas de Google
- `aud` igual al `client_id` configurado
- `iss` igual a `https://accounts.google.com` o `accounts.google.com`
- `exp` vigente
- claim `sub` presente

## Behavior

- `201 Created` si se crea usuario nuevo
- `200 OK` si el usuario ya existia
- `200 OK` en `POST /api/auth/refresh` cuando la rotacion es valida
- `401 Unauthorized` si el token es invalido
- `422 Unprocessable Entity` si el proveedor no retorna email
- `400 Bad Request` si el proveedor no es soportado o el request es invalido
- `409 Conflict` si existe un usuario local con el mismo email y el backend no puede enlazarlo de forma segura

## Notes

- Se aceptan cuentas Google personales y Google Workspace
- Si el usuario no existe, se crea usando `sub` como `provider_user_id`
- Si el usuario no tiene empresa primaria, el backend ejecuta provisioning inicial durante este login/registro
- `companyName` en este flujo solo se usa para la empresa inicial si el usuario aun no tenia empresa primaria
- `initialLegalRepresentative` se exige unicamente cuando el flujo provisiona empresa nueva
- Si el usuario ya tiene empresa primaria, `initialLegalRepresentative` se ignora
- Las empresas adicionales no se crean desde este endpoint; se crean con `POST /api/account/companies`
- El login externo tambien emite refresh token persistido y rotado por backend
- Si el usuario existe por email y no tenia `provider_user_id`, solo se enlaza automaticamente cuando Google devuelve un email confiable para linking:
  `email_verified = true` y el email es `@gmail.com`, o viene con `hd` de Google Workspace
- Si el usuario ya esta enlazado a otro proveedor/identidad externa, el endpoint responde `409 Conflict`
