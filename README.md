CLARIHR backend

## Desarrollo local (Docker Compose)

Requisitos: .NET SDK 10 y Docker. El entorno `Development` **no versiona secretos**: `appsettings.Development.json` apunta a servicios locales que provee `docker-compose.yml`, sin dependencia de Azure (§3.5).

```bash
docker compose up -d                 # postgres + azurite (storage) + gotenberg (PDF)
# aplicar migraciones — ver docs/technical/operations/manual-migrations-and-azure-deploy.md
dotnet run --project src/CLARIHR.Api
```

| Servicio | Puerto | Para qué | Config |
|---|---|---|---|
| `postgres` | `localhost:5432` | Base de datos | `Database:ConnectionString` |
| `azurite` | `localhost:10000` | Emulador Azure Blob (Shared Key) para exportaciones | `Storage:AzureBlob` |
| `gotenberg` | `localhost:3000` | Render HTML→PDF | `Reporting:Pdf:Engine=Gotenberg` |

Notas:

- **Storage local** usa Shared Key contra Azurite (la `AccountKey` en `appsettings.Development.json` es la **clave well-known pública de Azurite**, no un secreto). En producción se usa managed identity: `Storage:AzureBlob:UseManagedIdentity=true` y **sin** `AccountKey` (el código solo activa Shared Key cuando hay `AccountKey`).
- **PDF** usa Gotenberg por defecto (requiere el contenedor). Para render in-process sin servicio: `Reporting:Pdf:Engine=QuestPdf`.
- **Secretos reales** (BD de prod, OAuth de Google, firma JWT de prod) van por variables de entorno o `dotnet user-secrets`, nunca en `appsettings.Development.json`.

## Autenticación local para probar `platform_admin`

En `Development`, el backend marca como `platform_admin` a los correos configurados en `Authentication:Jwt:PlatformAdminEmails`.

La configuración local actual incluye:

- email: `dev@clarihr.local`
- password: `DevPassword123!`

Ese usuario se siembra automáticamente en arranque de desarrollo y puede autenticarse por `POST /api/auth/login` para probar endpoints globales como `GET /api/account/commercial-plans` desde Postman sin generar JWT manualmente.
