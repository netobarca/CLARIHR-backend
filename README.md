CLARIHR backend

## Autenticación local para probar `platform_admin`

En `Development`, el backend marca como `platform_admin` a los correos configurados en `Authentication:Jwt:PlatformAdminEmails`.

La configuración local actual incluye:

- email: `dev@clarihr.local`
- password: `DevPassword123!`

Ese usuario se siembra automáticamente en arranque de desarrollo y puede autenticarse por `POST /api/auth/login` para probar endpoints globales como `GET /api/account/commercial-plans` desde Postman sin generar JWT manualmente.
