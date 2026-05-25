# Local environment — setup desde cero y referencia de configuración

> **Para qué sirve este documento**: levantar el backend en una máquina nueva (o re-levantarlo si borras todo y clonas de nuevo), conectarte a los servicios externos, y saber **de dónde sale el valor de cada propiedad de configuración**.
>
> **Por qué existe (§N1)**: `appsettings.Development.json` ya **no se versiona** (está en `.gitignore`) porque contenía secretos. En su lugar se versiona una plantilla `appsettings.Development.json.example`. Cada quien crea su archivo local a partir de la plantilla.

---

## 1. Requisitos

- **.NET SDK 10**
- **Docker** (Docker Desktop o equivalente) — para Postgres, Azurite y Gotenberg
- **dotnet-ef** (para migraciones): `dotnet tool update --global dotnet-ef`

---

## 2. Setup desde cero (o re-setup tras borrar todo)

```bash
# 1. Clonar
git clone <repo-url> && cd CLARIHR-backend

# 2. Levantar servicios locales (Postgres + Azurite + Gotenberg)
docker compose up -d

# 3. Crear la config local desde la plantilla versionada (NO se versiona; tiene tus valores locales)
cp src/CLARIHR.Api/appsettings.Development.json.example src/CLARIHR.Api/appsettings.Development.json
cp src/CLARIHR.Backoffice.Api/appsettings.Development.json.example src/CLARIHR.Backoffice.Api/appsettings.Development.json

# 4. Aplicar migraciones (ver manual-migrations-and-azure-deploy.md §1)
dotnet ef database update \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj

# 5. Ejecutar
dotnet run --project src/CLARIHR.Api
```

La plantilla ya apunta a los servicios de `docker compose`, así que **con esos pasos exporta PDF/CSV/XLSX en local sin tocar Azure**. Para usuario seed de pruebas (`dev@clarihr.local` / `DevPassword123!`) ver `README.md`.

### 2.1 Servicios de `docker compose`

| Servicio | Puerto | Imagen | Reemplaza en prod a |
|---|---|---|---|
| `postgres` | `localhost:5433` | `postgres:16` | PostgreSQL gestionado |
| `azurite` | `localhost:10000` | `mcr.microsoft.com/azure-storage/azurite` | Azure Blob Storage |
| `gotenberg` | `localhost:3000` | `gotenberg/gotenberg:8` | Gotenberg desplegado |

> **Postgres en el puerto host `5433` (no `5432`)**: a propósito, para no chocar con un PostgreSQL local que muchos devs ya corren en `5432` (p. ej. Postgres.app). El contenedor internamente sigue en 5432; solo cambia el puerto publicado al host (por eso la connection string usa `Port=5433`). Si el volumen quedó inicializado por una corrida vieja sin el rol `clarihr`, resetéalo: `docker compose down -v && docker compose up -d`.
>
> **Un solo Postgres**: los integration tests usan **este mismo** Postgres de compose (crean DBs efímeras `clarihr_integration_tests_*` y las eliminan al terminar). No necesitas un PostgreSQL nativo en el host — **solo Docker**. (Para CI, sobreescribe con `CLARIHR_INTEGRATION_TEST_CONNECTION_STRING`.)

---

## 3. Referencia de configuración — de dónde sale cada valor

> Orden de carga (mayor gana): `appsettings.json` → `appsettings.Development.json` → **User Secrets** (solo Development) → **variables de entorno** (`Seccion__Subseccion__Clave`).
>
> **Secretos reales en local** (p. ej. un Google ClientId real): NO los pongas en `appsettings.Development.json`; usa User Secrets:
> ```bash
> dotnet user-secrets --project src/CLARIHR.Api set "Authentication:Google:ClientId" "<valor-real>"
> ```

| Propiedad | Qué es | Valor LOCAL (de dónde) | Valor PROD (de dónde) |
|---|---|---|---|
| `Database:ConnectionString` | Cadena PostgreSQL | Compose: `Host=localhost;Port=5433;Database=clarihr_dev;Username=clarihr;Password=clarihr` (ver `docker-compose.yml`) | Servidor PostgreSQL gestionado. Host/usuario/clave desde el portal del proveedor (Azure DB for PostgreSQL → *Connection strings*) o el secret store. **No reutilizar la credencial expuesta en §N1 — rotarla.** |
| `Authentication:Jwt:SigningKey` | Clave de firma de JWT (HS256, ≥32 chars) | Placeholder local-only de la plantilla | Generar secreto fuerte: `openssl rand -base64 48`. Guardar en App Service *Application settings* / secret store. **Único por entorno; nunca reutilizar el de §N1.** |
| `Authentication:Jwt:Issuer` / `Audience` / `PlatformAudience` | Emisor/audiencias del token | `clarihr-local` / `clarihr-platform-local` | Valores del entorno (p. ej. `clarihr` / `clarihr-platform`). No son secretos. |
| `Authentication:Google:ClientId` | OAuth de Google | Vacío (o tu client de pruebas vía User Secrets) | Google Cloud Console → *APIs & Services → Credentials → OAuth 2.0 Client IDs*. **Rotar el client id expuesto en §N1.** |
| `Storage:DefaultProvider` | Proveedor de archivos | `AzureBlob` | `AzureBlob` |
| `Storage:AzureBlob:AccountName` | Cuenta de Storage | `devstoreaccount1` (Azurite) | Azure Portal → *Storage account → Overview* (nombre). |
| `Storage:AzureBlob:AccountKey` | Clave de cuenta (Shared Key) | **Clave well-known pública de Azurite** (no es secreto) | **VACÍA en prod.** Prod usa managed identity (ver `UseManagedIdentity`). El código solo activa Shared Key si hay `AccountKey`. |
| `Storage:AzureBlob:BlobEndpoint` | Endpoint del blob | `http://127.0.0.1:10000/devstoreaccount1` (Azurite) | `https://<cuenta>.blob.core.windows.net` (Portal → *Endpoints*). |
| `Storage:AzureBlob:UseManagedIdentity` | Autenticación por identidad administrada | `false` (local usa Shared Key) | **`true`** (la App Service usa su identidad administrada; sin clave). |
| `Storage:AzureBlob:DefaultContainer` | Contenedor por defecto | `clarihr-files` | `clarihr-files` (crear en el Storage account). |
| `Reporting:Pdf:Engine` | Motor de PDF | `Gotenberg` (o `QuestPdf` para render in-process sin servicio) | `Gotenberg` (requiere el servicio desplegado) o `QuestPdf`. |
| `Reporting:Pdf:Gotenberg:BaseUrl` | URL de Gotenberg | `http://localhost:3000` (compose) | URL del Gotenberg desplegado (red interna / sidecar). |
| `Authentication:PasswordReset:FrontendResetUrl` | URL del front para reset | `http://localhost:3000/reset-password` | URL del frontend de producción. |
| `Reporting:Performance:*`, `Caching:*`, `Companies:Ownership:*`, `Billing:Subscriptions:*` | Tuning (lotes, TTLs, límites) | Defaults de `appsettings.json` | Defaults; ajustar por carga. No son secretos. |
| `Swagger:Enabled` | Exponer Swagger UI | `true` | `false` en producción. |
| **Backoffice** `BlobStorage:ConnectionString` | Storage del Backoffice (sección legacy, connection-string) | Connection string de Azurite (en la plantilla del Backoffice) | Connection string del Storage account (Portal → *Access keys*) o migrar a managed identity. |

---

## 4. Servicios externos — cómo obtener credenciales

- **PostgreSQL (prod)**: panel del proveedor (Azure DB for PostgreSQL / el host gestionado). Crear un usuario de aplicación con permisos mínimos; **restringir acceso por red/IP** (la IP pública `34.19.232.60` de §N1 debe dejar de aceptar conexiones abiertas).
- **Azure Blob Storage (prod)**: Azure Portal → *Storage account*. Preferir **managed identity** (asignar el rol *Storage Blob Data Contributor* a la identidad de la App Service) en vez de Access Keys.
- **Google OAuth**: [Google Cloud Console](https://console.cloud.google.com/) → *APIs & Services → Credentials*. El `ClientId` no es ultra-secreto pero el expuesto en §N1 conviene rotarlo.
- **JWT SigningKey**: generar local (`openssl rand -base64 48`), guardar en el secret store del entorno. Uno por entorno.
- **Gotenberg**: servicio propio (contenedor). No requiere credenciales; solo la URL alcanzable.

---

## 5. Troubleshooting de arranque

Errores típicos al hacer `dotnet run` en una máquina recién clonada o tras limpiar la config local.

### 5.1 `No database provider has been configured for this DbContext`
Excepción no controlada al iniciar (en `StartupInitializationExtensions.InitializeInfrastructureAsync` → `MigrateAsync`).

- **Causa**: `Database:ConnectionString` llegó **vacío**. `PostgreSqlOptionsConfigurator.Configure` hace `return` silencioso si la cadena está vacía/null → el `DbContext` queda **sin proveedor** y al migrar en el arranque lanza esto. El `appsettings.json` base trae la cadena vacía a propósito (§N1); el valor real debe venir de tu config local.
- **Causa habitual**: no creaste `appsettings.Development.json` desde la plantilla (§2 paso 3), o lo creaste pero `Database:ConnectionString` quedó vacío y tampoco está en User Secrets.
- **Fix** (cualquiera de los dos):
  ```bash
  # Opción A — crear la config local desde la plantilla (recomendado, restaura TODO)
  cp src/CLARIHR.Api/appsettings.Development.json.example src/CLARIHR.Api/appsettings.Development.json

  # Opción B — solo la cadena, vía User Secrets
  dotnet user-secrets --project src/CLARIHR.Api \
    set "Database:ConnectionString" "Host=localhost;Port=5433;Database=clarihr_dev;Username=clarihr;Password=clarihr"
  ```
- Verifica además que Postgres esté arriba: `docker compose up -d` y `docker ps` (contenedor `clarihr-postgres` en `5433`).

> **Ojo con configs parciales**: si tienes algunas claves en User Secrets y otras no, es fácil dejar huecos (p. ej. la cadena de DB o `Storage:AzureBlob:*` sin setear). La **Opción A** evita esto porque la plantilla trae el set completo y funcional; User Secrets/variables de entorno solo deberían usarse para *sobrescribir* secretos reales (§3), no para armar la config base pieza por pieza.

### 5.2 Warning `JWT authentication is not fully configured`
- **Causa**: faltan una o más de `Authentication:Jwt:Issuer` / `Audience` / `PlatformAudience` / `SigningKey`. La app **arranca igual** (es Warning, no error), pero la autenticación queda a medias — endpoints que validan tokens de plataforma (`PlatformAudience`) fallarán.
- **Fix**: usar la Opción A de §5.1 (la plantilla los trae todos), o setear los faltantes en User Secrets. `PlatformAudience` local = `clarihr-platform-local`.

### 5.3 Errores 5xx en subida/descarga de archivos o export de reportes
- **Causa**: `Storage:AzureBlob:AccountName` / `AccountKey` / `BlobEndpoint` vacíos → el proveedor de Blob no se configura contra Azurite. El `appsettings.json` base los trae vacíos con `UseManagedIdentity=true` (modo prod); en local necesitas los valores de Azurite con `UseManagedIdentity=false`.
- **Fix**: Opción A de §5.1 (la plantilla ya apunta a Azurite) y `docker compose up -d` para tener el contenedor `azurite` en `10000`.

---

## 6. Relación con §N1 (incidente de seguridad)

Este documento + el `.gitignore` + las plantillas resuelven la parte **estructural** de §N1 (dejar de versionar secretos + gate de CI `secret-scan`). **Queda pendiente, como acción operativa tuya**:

1. **Rotar** las credenciales ya expuestas en el historial: usuario `rh_usr` de PostgreSQL, `SigningKey` JWT, y revisar el `ClientId` de Google. Asumirlas como **comprometidas**.
2. **Restringir** por red/IP el PostgreSQL `34.19.232.60`.
3. **Purgar el historial git** (opcional pero recomendado): `git filter-repo --path src/CLARIHR.Api/appsettings.Development.json --path src/CLARIHR.Backoffice.Api/appsettings.Development.json --invert-paths` (o BFG), seguido de force-push **coordinado con todo el equipo** (reescribe la historia). Si no se purga, las credenciales del historial siguen expuestas → la rotación (paso 1) es la mitigación real.
