# Autenticación — Guía de consumo (frontend)

> **Estos son los primeros endpoints que el frontend debe integrar.** Todo el resto de la API
> requiere el `accessToken` que se obtiene acá.
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); este documento
> fue verificado contra `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## Overview

El módulo de autenticación vive bajo `/api/v1/auth`. Son **11 endpoints, todos `POST`**, sin path
ni query parameters (todo viaja en el body JSON). Todos son **anónimos** excepto `logout`.

Cubre 5 flujos:

| # | Flujo | Endpoints |
|---|-------|-----------|
| 1 | **Registro local + verificación de email** | `register` → `email-verification/confirm` (+ `email-verification/resend`) |
| 2 | **Login / sesión / logout** | `login`, `refresh`, `logout` |
| 3 | **Login/registro con Google** | `external` |
| 4 | **Aceptar invitación de compañía** | `company-user-invitations/accept` |
| 5 | **Recuperación de contraseña** | `password-reset/request` → `password-reset/validate` → `password-reset/redeem` |

### Orden de integración recomendado

1. `POST /auth/login` + `POST /auth/refresh` + `POST /auth/logout` — el núcleo de sesión y el interceptor HTTP.
2. `POST /auth/register` + página `/verify-email` (`confirm` + `resend`).
3. Páginas `/reset-password` (`request` → `validate` → `redeem`).
4. `POST /auth/external` (Google Sign-In).
5. Página de aceptar invitación (`company-user-invitations/accept`).

### Páginas que el frontend debe implementar (los emails del backend apuntan acá)

| Ruta FE (configurable en backend) | Recibe | Consume |
|---|---|---|
| `/verify-email?token=...` | token de verificación (TTL **60 min**, single-use) | `POST /auth/email-verification/confirm` |
| `/reset-password?token=...` | token de reset (TTL **15 min**, single-use) | `validate` y luego `redeem` |
| página de invitación con token (TTL **72 h**) | token de invitación | `POST /auth/company-user-invitations/accept` |

> Las URLs base de los links están en la config del backend (`Authentication:EmailVerification:FrontendVerifyUrl`,
> `Authentication:PasswordReset:FrontendResetUrl`; default `http://localhost:3000/...`). El token llega
> URL-encoded en el query param `token`.

---

## Modelo de tokens y sesión (leer antes de implementar)

- **`accessToken`** — JWT (HS256). Se manda en `Authorization: Bearer <accessToken>` en **toda**
  request autenticada de la API. Expira a los **15 minutos** (`expiresIn: 900`, en **segundos**).
- **`refreshToken`** — token **opaco** (no es JWT), válido **14 días**. Es **single-use**: cada
  `POST /auth/refresh` lo consume y devuelve un par nuevo. **Siempre reemplazá ambos tokens** con
  la respuesta del refresh.
- **Reuse detection**: si se presenta un refresh token ya rotado, el backend lo trata como robo y
  **revoca toda la familia de tokens** (la sesión entera muere). No reintentes un refresh con un
  token viejo.
- **Tenant**: el JWT ya viene con el contexto de la **compañía primaria activa** del usuario
  (claim interno). No hay paso de "seleccionar compañía" en el login. En `company-user-invitations/accept`
  la sesión queda atada a la compañía de la invitación.
- **`logout`** revoca **todos** los refresh tokens activos del usuario en la app Core (todas sus
  sesiones/dispositivos). El `accessToken` vigente sigue siendo técnicamente válido hasta expirar
  (≤15 min): el FE debe descartarlo localmente.
- Tras un **password-reset redeem** se revocan **todas** las sesiones (Core + Platform): hay que
  loguearse de nuevo.

### `AuthResponse` (respuesta de éxito de login / confirm / external / accept / refresh)

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "q7Zb1...token-opaco-base64...",
  "expiresIn": 900,
  "user": {
    "publicId": "8f3a1c2e-5b6d-4e7f-9a0b-1c2d3e4f5a6b",
    "email": "ana@empresa.com",
    "firstName": "Ana",
    "lastName": "García",
    "authProvider": "Local"
  }
}
```

| Campo | Tipo | Notas |
|-------|------|-------|
| `accessToken` | string (JWT) | usar en `Authorization: Bearer ...` |
| `refreshToken` | string | opaco, single-use, guardarlo de forma segura |
| `expiresIn` | int | vida del access token **en segundos** (900) |
| `user.publicId` | uuid | id público del usuario |
| `user.email` | string | |
| `user.firstName` / `user.lastName` | string | |
| `user.authProvider` | enum string | `Local` \| `Google` \| `Microsoft` \| `Apple` |

---

## Errores — forma y manejo (transversal)

Todos los errores son **RFC 7807 `ProblemDetails`** (`application/problem+json`) con dos
extensiones estables: **`code`** (string estable para lógica del FE — *nunca* parsees el mensaje)
y **`traceId`** (para reportar a backend). `title`/`detail` vienen localizados (en/es) según
`Accept-Language`.

**Error simple:**

```json
{
  "type": "https://httpstatuses.com/401",
  "title": "The provided credentials are invalid.",
  "status": 401,
  "detail": "The provided credentials are invalid.",
  "code": "auth.login.invalid_credentials",
  "traceId": "0HN7..."
}
```

**Error de validación (`400`, `code: "common.validation"`)** — agrega el diccionario `errors`
con las keys de campo en **camelCase**:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "code": "common.validation",
  "traceId": "0HN7...",
  "errors": {
    "email": ["'Email' is not a valid email address."],
    "password": ["Password must be at least 12 characters long."]
  }
}
```

**Rate limit (`429`, `code: "common.too_many_requests"`)** — viene con header **`Retry-After`**
(segundos). Mostrá un mensaje de espera y deshabilitá el submit hasta que pase.

> ⚠️ El swagger marca casi todos los campos de request como `nullable`; en la práctica los
> validadores los hacen obligatorios (→ `400`). Usá la columna **Req.** de cada endpoint.

---

# Endpoints

## 1. Login

### Endpoint
`POST /api/v1/auth/login`

### Description
Intercambia email + contraseña por una sesión (`AuthResponse`) para una cuenta **local activa**.

### Authentication
Ninguna (anónimo).

### Authorization
N/A — no hay RBAC en este módulo; cualquier llamador puede intentarlo.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |
| `Accept-Language` | No | `es` / `en` (localiza mensajes de error) |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `email` | string | Sí | formato email, máx 320 |
| `password` | string | Sí | máx 100 (acá NO se valida la política de complejidad) |

```json
{ "email": "ana@empresa.com", "password": "MiClaveSegura#2026" }
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `AuthResponse` | credenciales válidas, cuenta local activa |
| `400` | ProblemDetails `common.validation` | email/contraseña vacíos o malformados |
| `401` | ProblemDetails `auth.login.invalid_credentials` | **uniforme**: contraseña incorrecta, cuenta inexistente, inactiva, pendiente de verificación, externa (Google) o lockeada |
| `429` | ProblemDetails `common.too_many_requests` + `Retry-After` | rate limit (5/min por IP) |
| `500` | ProblemDetails | error inesperado |

```bash
curl -X POST "$BASE/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"ana@empresa.com","password":"MiClaveSegura#2026"}'
```

### Business Rules
- Solo cuentas con `authProvider: Local` y estado activo pueden hacer login con contraseña; una
  cuenta Google debe usar `/auth/external`.
- **Throttle de login**: 10 intentos fallidos dentro de una ventana de 15 min → lockout de
  **15 min**. Durante el lockout la respuesta sigue siendo el mismo `401` (no se distingue). Un
  login exitoso resetea el contador.
- Una cuenta registrada pero sin verificar el email recibe `401` (debe completar la verificación).

### Validation Rules
Las de la tabla del body. El `401` nunca dice cuál condición falló.

### Security Considerations
- **Anti-enumeración**: el `401` es idéntico para todos los casos de fallo (incluye ecualización
  de timing del lado servidor). No intentes inferir "usuario no existe" — no se puede ni se debe.
- UX sugerida ante `401`: "Email o contraseña incorrectos", con link a recuperar contraseña y a
  reenviar verificación.
- No persistas la contraseña; enviala y descartala.

---

## 2. Refresh (rotación de sesión)

### Endpoint
`POST /api/v1/auth/refresh`

### Description
Intercambia el refresh token vigente por un **nuevo** par access/refresh. El token presentado
queda revocado en el acto (rotación single-use).

### Authentication
Ninguna (anónimo — el refresh token ES la credencial). No mandes `Authorization`.

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `refreshToken` | string | Sí | máx 2048 |

```json
{ "refreshToken": "q7Zb1...token-opaco..." }
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `AuthResponse` (par **nuevo**) | token válido y vigente |
| `400` | ProblemDetails `common.validation` | token vacío / demasiado largo |
| `401` | ProblemDetails `auth.refresh.invalid_token` | inválido, expirado, revocado o **reuso detectado** |
| `429` | ProblemDetails + `Retry-After` | rate limit (60/min por IP — generoso) |
| `500` | ProblemDetails | error inesperado |

### Business Rules
- El refresh re-resuelve el contexto de tenant (compañía primaria activa) — si cambió, el nuevo
  JWT lo refleja.
- Reusar un token ya rotado revoca **toda la familia** → la sesión muere definitivamente.

### Validation Rules
`refreshToken` no vacío, máx 2048.

### Security Considerations
- **Single-flight obligatorio en el FE**: si dos requests disparan refresh en paralelo con el
  mismo token, el segundo gatilla la detección de reuso y mata la sesión. Serializá el refresh
  (mutex/promesa compartida) y encolá las requests mientras tanto.
- Ante `401` acá: limpiá los tokens y mandá al usuario al login. No reintentes.

---

## 3. Logout

### Endpoint
`POST /api/v1/auth/logout`

### Description
Revoca todos los refresh tokens activos (Core) del usuario autenticado. Idempotente.

### Authentication
**Bearer requerido** — `Authorization: Bearer <accessToken>`. Único endpoint autenticado del módulo.

### Authorization
Solo requiere estar autenticado (sin permisos adicionales).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
Ninguno (sin body).

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `204` | — | sesiones revocadas (o ya estaban revocadas) |
| `401` | ProblemDetails | access token faltante/expirado/inválido |
| `500` | ProblemDetails | error inesperado |

```bash
curl -X POST "$BASE/api/v1/auth/logout" -H "Authorization: Bearer $TOKEN"
```

### Business Rules
- Revoca **todas** las sesiones Core del usuario (todos los dispositivos), no solo la actual.
- Idempotente: repetirlo da `204` igual.

### Validation Rules
N/A.

### Security Considerations
- El access token vigente sigue siendo válido hasta su expiración (≤15 min). El FE debe borrar
  ambos tokens localmente de inmediato.
- Si el access token ya expiró, podés tratar el `401` como logout exitoso local (borrar y salir).

---

## 4. Registro local

### Endpoint
`POST /api/v1/auth/register`

### Description
Inicia el registro local. Crea la cuenta en estado **pendiente de verificación** y envía un email
con el link de verificación. **No emite sesión**: la sesión llega recién al confirmar el email.

### Authentication
Ninguna (anónimo).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |
| `Accept-Language` | No | `es` / `en` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `firstName` | string | Sí | máx 100; solo letras (unicode), espacios, `'` y `-`; empieza con letra |
| `lastName` | string | Sí | ídem `firstName` |
| `email` | string | Sí | formato email, máx 320 |
| `password` | string | Sí | **política de contraseñas** (ver sección al final) |
| `country` | string | No | máx 100; letras/espacios/`'`/`-`, mín 2 caracteres |
| `source` | string | No | máx 100; alfanumérico + ` ._:/-`, empieza alfanumérico (ej. `landing-page`) |

```json
{
  "firstName": "Ana",
  "lastName": "García",
  "email": "ana@empresa.com",
  "password": "MiClaveSegura#2026",
  "country": "Honduras",
  "source": "landing-page"
}
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `202` | — (vacío) | **siempre**, exista o no el email (anti-enumeración) |
| `400` | ProblemDetails `common.validation` | violación de validación o de política de contraseña (mensajes por campo en `errors`) |
| `429` | ProblemDetails + `Retry-After` | rate limit (5/min por IP) |
| `500` | ProblemDetails | error inesperado |

### Business Rules
- `202` **no significa que la cuenta se creó**: si el email ya existe, no pasa nada visible
  (y si existía pendiente de verificación, se le reenvía el link respetando el cooldown de 2 min).
- La cuenta queda **inutilizable hasta confirmar el email** (login devuelve `401` mientras tanto).
- El link de verificación expira a los **60 minutos** y es single-use.

### Validation Rules
Las de la tabla. La política de contraseña devuelve **una entrada por regla violada** en
`errors.password`, ideal para checklist en vivo en el form.

### Security Considerations
- No muestres "este email ya está registrado" — el backend no lo revela y el FE no debe intentar
  inferirlo.
- UX post-202: pantalla "revisá tu correo para verificar la cuenta" con botón de reenviar
  (deshabilitado ~2 min por el cooldown).

---

## 5. Confirmar verificación de email

### Endpoint
`POST /api/v1/auth/email-verification/confirm`

### Description
Canjea el token del link de verificación: activa la cuenta pendiente y devuelve sesión completa
(`AuthResponse`) — **auto-login**, no hace falta pasar por `/login`.

### Authentication
Ninguna (anónimo — el token es la credencial).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno (el token llega al FE por la URL del email — `/verify-email?token=...` — pero a la API va en el body).

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `token` | string | Sí | máx 500 |

```json
{ "token": "<token del query param, ya URL-decoded por tu router>" }
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `AuthResponse` | cuenta activada + sesión emitida |
| `400` | ProblemDetails `common.validation` | token vacío |
| `401` | ProblemDetails `auth.email_verification.invalid_token` | token inválido, expirado (>60 min) o ya usado |
| `429` | ProblemDetails + `Retry-After` | rate limit (5/min por IP) |
| `500` | ProblemDetails | error inesperado |

### Business Rules
- Single-use: el segundo intento con el mismo token da `401` (cuidado con el double-mount de
  React StrictMode / prefetch — disparalo una sola vez, idealmente con un botón "Verificar").

### Validation Rules
`token` no vacío, máx 500.

### Security Considerations
- Ante `401` ofrecé reenviar el link (`email-verification/resend`) pidiendo el email — el token
  expirado no revela a qué cuenta pertenecía.

---

## 6. Reenviar verificación de email

### Endpoint
`POST /api/v1/auth/email-verification/resend`

### Description
Reenvía el link de verificación para una cuenta local que sigue pendiente.

### Authentication
Ninguna (anónimo).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `email` | string | Sí | formato email, máx 320 |

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `202` | — | **siempre** (exista o no, esté pendiente o no) |
| `400` | ProblemDetails `common.validation` | email malformado |
| `429` | ProblemDetails + `Retry-After` | rate limit (5/min por IP) |

### Business Rules
- Solo reenvía si la cuenta existe, es local y está pendiente; en cualquier otro caso `202`
  silencioso (anti-enumeración).
- **Cooldown de 2 minutos** por cuenta: dentro del cooldown responde `202` pero **no** manda
  email. Implementá un countdown de ~120 s en el botón.
- Reenviar invalida el link anterior (se emite token nuevo).

### Validation Rules
`email` no vacío, formato válido, máx 320.

### Security Considerations
- Mismo patrón anti-enumeración que register: nunca indiques si el email existe.

---

## 7. Login/registro con Google

### Endpoint
`POST /api/v1/auth/external`

### Description
Valida el `id_token` de Google y: si el usuario existe → login (`200`); si no existe → lo
provisiona vinculado a Google (`201`). Ambos devuelven `AuthResponse` (sesión inmediata, sin
verificación de email — Google ya la garantiza).

### Authentication
Ninguna (anónimo — el `idToken` de Google es la credencial).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `provider` | enum string | Sí | hoy solo `"Google"` (ver Enums; `"Local"` → 400; `Microsoft`/`Apple` → 400 `provider_not_supported`) |
| `idToken` | string | Sí | el **ID token** (JWT) de Google Identity Services, máx 8000 |
| `country` | string | No | igual que register |
| `source` | string | No | igual que register |

```json
{ "provider": "Google", "idToken": "eyJhbGciOiJSUzI1...", "source": "landing-page" }
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `AuthResponse` | usuario existente logueado |
| `201` | `AuthResponse` | usuario nuevo provisionado |
| `400` | ProblemDetails `common.validation` / `auth.external.provider_not_supported` | provider inválido o no soportado |
| `401` | ProblemDetails `auth.external.invalid_token` | id_token inválido/expirado/audience incorrecta |
| `409` | ProblemDetails `auth.external.provider_link_conflict` \| `auth.external.email_link_not_allowed` | el email ya pertenece a una cuenta de otro proveedor o local — no se auto-vincula |
| `422` | ProblemDetails `auth.external.email_missing` | Google no devolvió email |
| `500` | ProblemDetails `auth.external.provider_configuration_invalid` | config del proveedor rota |

### Business Rules
- Tratá `200` y `201` igual en el FE (podés usar el `201` para mostrar onboarding de primera vez).
- Una cuenta creada por Google tiene `authProvider: "Google"` y **no puede** hacer login con
  contraseña ni pedir password-reset.
- `409`: la cuenta de ese email ya existe con otro mecanismo → indicá al usuario que entre con su
  método original (ej. email+contraseña).

### Validation Rules
Las de la tabla del body.

### Security Considerations
- El `idToken` debe ser emitido para el **Client ID de Google configurado en el backend**
  (audience). Usá el mismo Client ID en el FE (Google Identity Services) que el de
  `Authentication:Google:ClientId`.
- El id_token de Google expira rápido (~1 h, validado server-side): obtenelo y enviálo
  inmediatamente, no lo caches.

---

## 8. Aceptar invitación de compañía

### Endpoint
`POST /api/v1/auth/company-user-invitations/accept`

### Description
Consume el token de invitación (emitido al invitar a un usuario desde el módulo Company Users),
define la contraseña del invitado, activa usuario + membresía + IAM y devuelve sesión
(`AuthResponse`) **con tenant de la compañía de la invitación**.

### Authentication
Ninguna (anónimo — el token de invitación es la credencial).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `token` | string | Sí | máx 500 |
| `password` | string | Sí | **política de contraseñas** (sin el chequeo de datos personales) |

```json
{ "token": "<token de la invitación>", "password": "MiClaveSegura#2026" }
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `AuthResponse` (sesión tenant-scoped) | invitación aceptada |
| `400` | ProblemDetails `common.validation` | token vacío / política de contraseña violada |
| `401` | ProblemDetails `auth.invitation.invalid_token` | token inválido, expirado (>72 h) o ya usado |
| `409` | ProblemDetails `auth.invitation.company_unavailable` | la compañía de la invitación fue archivada |
| `500` | ProblemDetails | error inesperado |

### Business Rules
- El token de invitación expira a las **72 horas** y es single-use. Si expiró, el admin de la
  compañía debe reemitir la invitación (no hay "resend" anónimo en este módulo).
- Tras el `200` el usuario queda logueado directo en la compañía que lo invitó.

### Validation Rules
`token` no vacío máx 500; `password` según política (12–100, mayúscula, minúscula, número, especial).

### Security Considerations
- Ante `409` mostrá un mensaje terminal ("esta invitación ya no está disponible") — no hay
  acción del usuario que lo resuelva.

---

## 9. Password reset — solicitar

### Endpoint
`POST /api/v1/auth/password-reset/request`

### Description
Inicia la recuperación de contraseña de una cuenta local: emite un token single-use y envía el
email con el link de reset.

### Authentication
Ninguna (anónimo).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `email` | string | Sí | formato email, máx 320 |

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `202` | — | **siempre** (exista o no, sea elegible o no) |
| `400` | ProblemDetails `common.validation` | email malformado |
| `429` | ProblemDetails + `Retry-After` | rate limit (5/min por IP) |

### Business Rules
- Solo cuentas **locales activas** reciben email; Google/inactivas/inexistentes → `202` silencioso.
- El token de reset expira a los **15 minutos** (avisalo en la UI: "el link vence en 15 minutos").
- Pedir de nuevo revoca los tokens de reset anteriores; **cooldown de 2 min** por cuenta (dentro
  del cooldown: `202` sin email).

### Validation Rules
`email` no vacío, formato válido, máx 320.

### Security Considerations
- Anti-enumeración: UX post-202 siempre igual — "si el email existe, te enviamos un link".

---

## 10. Password reset — validar token (pre-flight)

### Endpoint
`POST /api/v1/auth/password-reset/validate`

### Description
Chequeo previo que usa la página `/reset-password` al cargar, **antes** de mostrar los campos de
nueva contraseña. Para token activo devuelve expiración y email enmascarado.

### Authentication
Ninguna (anónimo).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `token` | string | Sí | máx 500 |

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `PasswordResetTokenValidationResponse` | token activo |
| `400` | ProblemDetails `common.validation` | token vacío |
| `401` | ProblemDetails `auth.password_reset.invalid_token` | inválido, expirado o ya usado |

**Respuesta `200`:**

```json
{ "expiresAtUtc": "2026-06-10T17:45:00Z", "maskedEmail": "a***@empresa.com" }
```

### Business Rules
- No consume el token (se puede validar y luego canjear).
- Usá `expiresAtUtc` para un countdown en la página de reset.

### Validation Rules
`token` no vacío, máx 500.

### Security Considerations
- Ante `401` mostrá pantalla de "link vencido" con CTA a pedir uno nuevo (`request`).

---

## 11. Password reset — canjear

### Endpoint
`POST /api/v1/auth/password-reset/redeem`

### Description
Consume el token single-use y establece la nueva contraseña. Revoca todos los demás tokens de
reset **y todas las sesiones** (Core + Platform).

### Authentication
Ninguna (anónimo).

### Authorization
N/A.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Content-Type` | Sí | `application/json` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `token` | string | Sí | máx 500 |
| `newPassword` | string | Sí | **política de contraseñas** (incluye el chequeo contra nombre/email del dueño del token) |

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `204` | — | contraseña cambiada |
| `400` | ProblemDetails `common.validation` | política de contraseña violada (detalle por regla en `errors`) |
| `401` | ProblemDetails `auth.password_reset.invalid_token` | token inválido/expirado/usado |

### Business Rules
- Tras el `204` **no hay sesión**: redirigí al login para entrar con la nueva contraseña.
- Todas las sesiones previas del usuario quedan revocadas (en todos los dispositivos).

### Validation Rules
`token` no vacío máx 500; `newPassword` según política completa (ver abajo).

### Security Considerations
- El backend re-valida la política contra los datos reales del usuario (nombre/apellido/email),
  aunque el FE no los conozca: un `400` acá puede incluir "Password cannot contain your name or
  email".

---

# Referencia compartida

## Política de contraseñas (register / accept-invitation / password-reset redeem)

| Regla | Mensaje (en) |
|-------|--------------|
| 12–100 caracteres | `Password must be at least 12 characters long.` / `... 100 characters or fewer.` |
| ≥1 mayúscula | `Password must contain at least one uppercase letter.` |
| ≥1 minúscula | `Password must contain at least one lowercase letter.` |
| ≥1 número | `Password must contain at least one number.` |
| ≥1 carácter especial (no alfanumérico) | `Password must contain at least one special character.` |
| No contener nombre, apellido ni la parte local del email (≥3 chars, comparación normalizada sin símbolos ni mayúsculas) | `Password cannot contain your name or email.` |

Cada violación llega como un mensaje separado en `errors.password` (o `errors.newPassword`) —
ideal para un checklist en vivo. En **login** la contraseña solo se valida no-vacía/máx 100 (las
cuentas viejas no se bloquean por política nueva).

## Enums

### `AuthProvider` (serializa como **string**)

| Valor | Significado | ¿Usable hoy en `/auth/external`? |
|-------|-------------|-----------------------------------|
| `Local` | cuenta email+contraseña | No — rechazado con `400` (no es proveedor externo) |
| `Google` | cuenta Google | **Sí** (único soportado) |
| `Microsoft` | reservado | No — `400 auth.external.provider_not_supported` |
| `Apple` | reservado | No — `400 auth.external.provider_not_supported` |

Aparece también en `AuthResponse.user.authProvider` (ahí sí puede valer `Local` o `Google`).

> Recordatorio general de la API: **todos** los enums viajan como strings (`"Google"`, nunca `1`).

## Catálogo de códigos de error del módulo

| `code` | HTTP | Endpoint(s) | Acción FE sugerida |
|--------|------|-------------|--------------------|
| `common.validation` | 400 | todos | mostrar `errors` por campo |
| `common.too_many_requests` | 429 | todos | respetar `Retry-After`, deshabilitar submit |
| `auth.login.invalid_credentials` | 401 | login | "email o contraseña incorrectos" |
| `auth.user_not_active` | 401 | (flujos de sesión) | tratar como credenciales inválidas |
| `auth.refresh.invalid_token` | 401 | refresh | limpiar sesión → login |
| `auth.logout.invalid_current_user` | 401 | logout | limpiar sesión local igual |
| `auth.email_verification.invalid_token` | 401 | email-verification/confirm | ofrecer resend |
| `auth.invitation.invalid_token` | 401 | invitations/accept | "invitación inválida o vencida" |
| `auth.invitation.company_unavailable` | 409 | invitations/accept | mensaje terminal |
| `auth.password_reset.invalid_token` | 401 | password-reset validate/redeem | CTA a pedir nuevo link |
| `auth.external.invalid_token` | 401 | external | reintentar Google Sign-In |
| `auth.external.provider_not_supported` | 400 | external | no ofrecer ese botón |
| `auth.external.email_missing` | 422 | external | pedir cuenta Google con email |
| `auth.external.provider_link_conflict` | 409 | external | "entrá con tu método original" |
| `auth.external.email_link_not_allowed` | 409 | external | "entrá con tu método original" |
| `auth.external.provider_configuration_invalid` | 500 | external | error genérico, reportar `traceId` |
| `auth.token_configuration_invalid` | 500 | emisión de tokens | error genérico, reportar `traceId` |

## Rate limits (fixed window por IP)

| Endpoint | Límite |
|----------|--------|
| `login`, `register`, `external`, `invitations/accept`, `email-verification/*`, `password-reset/*` | **5/min** |
| `refresh` | **60/min** |

Sin cola: el request 6º dentro del minuto recibe `429` directo con `Retry-After`.

## Cooldowns y TTLs (resumen para UX)

| Cosa | Valor |
|------|-------|
| Access token | 15 min (`expiresIn: 900` s) |
| Refresh token | 14 días, single-use |
| Token verificación de email | 60 min, single-use, cooldown resend 2 min |
| Token password reset | 15 min, single-use, cooldown request 2 min |
| Token invitación | 72 h, single-use |
| Lockout de login | 10 fallos / ventana 15 min → 15 min de lockout |

---

# Guía de implementación del cliente

1. **Interceptor de requests**: adjuntar `Authorization: Bearer <accessToken>` a toda request
   excepto los endpoints de `/auth/*` anónimos.
2. **Refresh proactivo**: programá el refresh ~60 s antes de `expiresIn` (basate en un timer desde
   la respuesta, no en el reloj del JWT). Alternativa reactiva: ante `401` de cualquier endpoint,
   intentar **un** refresh y reintentar la request original.
3. **Single-flight**: un solo refresh en vuelo; las demás requests esperan la promesa compartida.
   Dos refresh paralelos con el mismo token activan la detección de reuso y **matan la sesión**.
4. **Fallo de refresh (`401`)**: borrar tokens, estado global a "no autenticado", redirigir a login.
5. **Almacenamiento**: la API entrega los tokens en el body (no setea cookies). Para SPA:
   `accessToken` en memoria; `refreshToken` en el storage más protegido disponible. Asumí que
   cualquier cosa en `localStorage` es legible por XSS — minimizá su vida ahí.
6. **No parsees mensajes**: toda lógica sobre `code`; los textos (`title`/`detail`) cambian con
   `Accept-Language` (en/es).
7. **Anti-doble-submit en tokens single-use**: `confirm`, `redeem` y `accept` fallan con `401` al
   segundo disparo. Cuidado con re-mounts (React StrictMode) y prefetching de links de email.
8. **Google**: usá Google Identity Services con el mismo Client ID que el backend; mandá el
   `credential` (ID token JWT) como `idToken` apenas lo recibís.

## Próximo paso de integración (fuera de este doc)

Con la sesión emitida, la siguiente fase es el contexto de cuenta/compañía:
`GET /api/v1/account/companies/...` (compañías del usuario, `access-context` con permisos
efectivos por compañía). Se documentará en un doc aparte de esta misma carpeta.
