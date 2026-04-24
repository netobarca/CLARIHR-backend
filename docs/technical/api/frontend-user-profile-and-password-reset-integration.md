# Guia de integracion Frontend - Perfil de usuario, password reset y detalle de company user

## 1. Proposito

Este documento explica como debe integrar frontend los cambios recientes en:

- preferencias del usuario autenticado con `socialLinks`
- recuperacion de contrasena
- detalle puntual de `company users` por `userPublicId`

Fuentes canonicas del contrato:

- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

Esta guia no reemplaza esos documentos. Su objetivo es dejar claro:

- que endpoints debe consumir frontend
- en que orden debe consumirlos
- que payload debe enviar y que payload debe esperar
- que decisiones de UI no debe inferir por su cuenta

## 2. Resumen funcional

Frontend debe asumir estas reglas:

- `socialLinks` pertenecen al usuario autenticado, no al `personnel file`
- `socialLinks` viven dentro de preferencias de cuenta, junto con `language`
- el flujo de `password reset` es `request -> validate -> redeem`
- el backend no hace auto-login despues de redimir la nueva contrasena
- el token de reset es de un solo uso y expira rapido
- `GET /api/company/users/{userPublicId}` devuelve el mismo shape base de `CompanyUserResponse`
- en `CompanyUserResponse`, el campo `id` representa el `publicId` externo del usuario

## 3. Preferencias del usuario autenticado

Base route:

- `/api/account/me/preferences`

### 3.1 Obtener preferencias del usuario

Endpoint:

- `GET /api/account/me/preferences`

Autenticacion:

- requiere `Bearer accessToken` Core

Response esperado:

```json
{
  "id": "b3f0b8bf-65ec-4f2e-8bb9-9e7d09d2f21f",
  "language": "en",
  "socialLinks": [
    {
      "providerCode": "LINKEDIN",
      "url": "https://www.linkedin.com/in/jane-doe"
    },
    {
      "providerCode": "GITHUB",
      "url": "https://github.com/jane-doe"
    }
  ],
  "createdAtUtc": "2026-04-24T02:00:00Z",
  "modifiedAtUtc": "2026-04-24T02:05:00Z"
}
```

Reglas observables:

- si el usuario no tenia preferencias todavia, backend crea el registro automaticamente
- `socialLinks` siempre viene como arreglo
- `providerCode` llega normalizado en uppercase

### 3.2 Actualizar solo el idioma

Endpoint:

- `PUT /api/account/me/preferences`

Request:

```json
{
  "language": "es"
}
```

Regla importante:

- este endpoint sigue enfocado en `language`
- frontend no debe intentar mandar `socialLinks` aqui

### 3.3 Reemplazar links sociales del usuario

Endpoint:

- `PUT /api/account/me/preferences/social-links`

Request:

```json
{
  "items": [
    {
      "providerCode": "linkedin",
      "url": "https://www.linkedin.com/in/jane-doe"
    },
    {
      "providerCode": "github",
      "url": "https://github.com/jane-doe"
    }
  ]
}
```

Response:

- devuelve `UserPreferenceResponse` completo
- incluye `language` y la coleccion final normalizada de `socialLinks`

Semantica obligatoria:

- este endpoint hace `replace` completo
- si frontend manda `items: []`, backend elimina todos los social links
- frontend debe enviar el estado final completo, no un patch parcial

Validaciones que frontend debe respetar:

- maximo `10` links
- `providerCode` unico por usuario
- `providerCode` con caracteres `[A-Za-z0-9_.-]`
- `url` absoluta `https`
- longitud maxima segura `500`

Recomendacion de UI:

- manejar la lista localmente como coleccion editable
- guardar con un solo submit final
- si el backend responde `400`, renderizar errores por campo usando `ProblemDetails.errors`

## 4. Password reset

Base route:

- `/api/auth/password-reset`

## 4.1 Flujo completo

Frontend debe implementar este orden:

1. pantalla "forgot password"
2. `POST /api/auth/password-reset/request`
3. mostrar siempre mensaje generico de exito
4. usuario abre link recibido por correo
5. frontend toma `token` desde query string
6. `POST /api/auth/password-reset/validate`
7. si es valido, mostrar formulario de nueva contrasena
8. `POST /api/auth/password-reset/redeem`
9. redirigir a login

Reglas importantes:

- frontend no debe intentar inferir si la cuenta existe desde el resultado de `request`
- frontend no debe guardar el token mas tiempo del necesario
- frontend no debe intentar iniciar sesion automaticamente despues de `redeem`

## 4.2 Solicitar recuperacion

Endpoint:

- `POST /api/auth/password-reset/request`

Request:

```json
{
  "email": "user@company.com"
}
```

Response:

- `202 Accepted`

Comportamiento observable:

- la respuesta es uniforme aunque el usuario no exista, sea externo, este inactivo o no sea elegible
- el objetivo es evitar enumeracion de cuentas
- este endpoint tiene rate limiting puntual; frontend debe manejar `429`

Mensaje recomendado de UI:

- "Si la cuenta es valida, enviamos un correo con instrucciones para restablecer la contrasena."

## 4.3 Validar token del link

Endpoint:

- `POST /api/auth/password-reset/validate`

Request:

```json
{
  "token": "TOKEN_DEL_LINK"
}
```

Response exitosa:

```json
{
  "expiresAtUtc": "2026-04-24T03:10:00Z",
  "maskedEmail": "j******e@company.com"
}
```

Uso recomendado:

- llamar este endpoint al montar la pantalla de reset
- si responde `200`, mostrar formulario final
- si responde `401`, mostrar pantalla de link invalido o expirado

Regla de UX:

- frontend no debe asumir que el token sigue valido solo porque existe en la URL
- siempre debe validarlo con backend antes de mostrar el submit final

## 4.4 Redimir token con nueva contrasena

Endpoint:

- `POST /api/auth/password-reset/redeem`

Request:

```json
{
  "token": "TOKEN_DEL_LINK",
  "newPassword": "StrongPass123!"
}
```

Response:

- `204 No Content`

Errores relevantes:

- `400` si la nueva contrasena no cumple politica
- `401` si el token es invalido, expirado, usado o revocado

Politica de frontend:

- tras `204`, invalidar cualquier estado local de reset y llevar al usuario al login
- mostrar mensaje del tipo: "Tu contrasena fue actualizada. Inicia sesion nuevamente."

## 4.5 Manejo del link de correo

Backend construye el link apuntando al frontend usando configuracion:

- `Authentication:PasswordReset:FrontendResetUrl`

Frontend debe exponer una ruta compatible, por ejemplo:

- `/reset-password?token=...`

Recomendacion:

- usar una pagina dedicada
- leer el query param `token`
- no loggear el token en analytics, console ni breadcrumbs

## 5. Detalle puntual de company user

Base route:

- `/api/company/users`

### 5.1 Obtener un usuario puntual por publicId

Endpoint:

- `GET /api/company/users/{userPublicId}`

Autenticacion:

- requiere token Core con tenant activo
- requiere permiso `RBAC_USERS:Read`

Response:

```json
{
  "id": "7f7e92b7-3d2b-43e7-bbc9-b6b7ebf1d7d8",
  "email": "user@company.com",
  "firstName": "Jane",
  "lastName": "Doe",
  "roles": [
    {
      "id": "45fba50d-0f8e-42a7-a9c5-79a8bf2d97be",
      "name": "Admin de Empresa",
      "description": "Rol administrativo principal",
      "isSystemRole": true
    }
  ],
  "status": "Active"
}
```

Reglas importantes:

- el parametro de ruta es `userPublicId`
- el `id` del response tambien es `userPublicId`
- el endpoint ya aplica tenant scope implicito
- los campos pueden salir ocultos o filtrados segun field permissions del usuario autenticado

Uso recomendado:

- pantalla de detalle o edicion de usuario
- rehidratacion de formulario despues de navegar desde un listado
- refresco puntual despues de `PUT /api/company/users/{userPublicId}`

## 6. Manejo recomendado de errores

Frontend debe usar el envelope normal de `ProblemDetails`.

Campos relevantes:

- `title`
- `status`
- `code`
- `traceId`
- `errors`

Reglas practicas:

- para `400`, mostrar errores por campo desde `errors`
- para `401` en `password-reset/validate` o `redeem`, tratarlo como token invalido o expirado
- para `429` en `password-reset/request`, mostrar mensaje de reintento breve
- para `403` en `company/users/{userPublicId}`, tratarlo como falta de permiso, no como ausencia del usuario
- para `404` en `company/users/{userPublicId}`, tratarlo como usuario inexistente o fuera del tenant activo

## 7. Do y Don't

### Do

- usar `GET /api/account/me/preferences` como fuente de verdad para `language` y `socialLinks`
- enviar la coleccion completa en `PUT /api/account/me/preferences/social-links`
- validar el token de reset antes de renderizar el formulario final
- redirigir a login despues de redimir la nueva contrasena
- usar `GET /api/company/users/{userPublicId}` cuando la UI necesite un registro puntual del usuario

### Don't

- no guardar `socialLinks` en `personnel files`
- no usar `PUT /api/account/me/preferences` para mandar links sociales
- no inferir existencia de cuenta desde `password-reset/request`
- no hacer auto-login despues de `password-reset/redeem`
- no asumir que `id` en `CompanyUserResponse` es un entero interno

## 8. Checklist rapido para frontend

1. Ajustar el tipo `UserPreferenceResponse` para incluir `socialLinks`
2. Crear UI de edicion de social links con semantica de reemplazo completo
3. Crear pantalla `forgot password`
4. Crear pantalla `reset password` basada en `token` de query string
5. Manejar `202`, `401`, `400` y `429` en el flujo de reset
6. Agregar cliente para `GET /api/company/users/{userPublicId}`
7. Tratar `CompanyUserResponse.id` como `userPublicId`
