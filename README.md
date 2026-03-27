CLARIHR backend

## Docker local

Levanta la API y PostgreSQL con:

```bash
docker compose up --build
```

La API queda disponible en `http://localhost:5000` y Swagger en `http://localhost:5000/swagger`.

Variables opcionales:

- `CLARIHR_API_PORT` para cambiar el puerto expuesto de la API.
- `CLARIHR_DB_PORT` para cambiar el puerto expuesto de PostgreSQL.

Verificación rápida:

```bash
curl http://localhost:5000/api/system/status
```

## Autenticación local para probar `platform_admin`

En `Development`, el backend marca como `platform_admin` a los correos configurados en `Authentication:Jwt:PlatformAdminEmails`.

La configuración local actual incluye:

- email: `dev@clarihr.local`
- password: `DevPassword123!`

Ese usuario se siembra automáticamente en arranque de desarrollo y puede autenticarse por `POST /api/auth/login` para probar endpoints globales como `GET /api/account/commercial-plans` desde Postman sin generar JWT manualmente.
