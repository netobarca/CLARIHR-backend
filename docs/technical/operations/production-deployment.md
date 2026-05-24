# Producción — runbook de despliegue paso a paso

> **Para qué sirve**: llevar el sistema a producción de forma reproducible y ejecutar el paso a paso en el servidor. Cubre el **aprovisionamiento por una sola vez**, la **configuración (secretos por entorno, nunca en archivos)**, el **despliegue**, la **verificación** y el **rollback**.
>
> **Target actual**: Azure App Service (mecánica de migraciones y copia de archivos en `manual-migrations-and-azure-deploy.md`). El `Dockerfile` del repo permite además un despliegue containerizado (§4.1).
>
> **Regla de oro (§N1)**: los secretos de producción **no viven en `appsettings.*.json`** (el base va con placeholders vacíos). Se inyectan como **Application Settings / variables de entorno** del App Service. Ver tabla en `local-environment-setup.md §3`.

---

## 1. Aprovisionamiento por entorno (una sola vez)

1. **PostgreSQL gestionado**: crear servidor + base + usuario de aplicación con permisos mínimos. **Restringir acceso por red/IP** (no exponer la BD a `0.0.0.0`). Anotar host/db/usuario; la clave va al secret store.
2. **Azure Storage account**: crear la cuenta + contenedor `clarihr-files`. **Preferir managed identity**: asignar a la identidad administrada de la App Service el rol **Storage Blob Data Contributor** sobre la cuenta. (Evita usar Access Keys.)
3. **Gotenberg**: desplegar `gotenberg/gotenberg:8` como contenedor/servicio alcanzable por la API (red interna). Anotar su URL. *(Alternativa: usar `Reporting:Pdf:Engine=QuestPdf` — render in-process, sin servicio.)*
4. **App Service**: crear la app (.NET 10). Habilitar **managed identity** (Identity → System assigned → On).
5. **Google OAuth**: crear/obtener el `ClientId` en Google Cloud Console (Credentials).

---

## 2. Configuración de producción (Application Settings / env vars)

En App Service → **Configuration → Application settings**, definir (ASP.NET mapea `Seccion:Clave` → `Seccion__Clave`):

```
ASPNETCORE_ENVIRONMENT = Production
Database__ConnectionString = Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<secret>
Authentication__Jwt__Issuer = clarihr
Authentication__Jwt__Audience = clarihr
Authentication__Jwt__PlatformAudience = clarihr-platform
Authentication__Jwt__SigningKey = <openssl rand -base64 48>
Authentication__Google__ClientId = <google-oauth-client-id>
Storage__DefaultProvider = AzureBlob
Storage__AzureBlob__AccountName = <storage-account>
Storage__AzureBlob__BlobEndpoint = https://<storage-account>.blob.core.windows.net
Storage__AzureBlob__DefaultContainer = clarihr-files
Storage__AzureBlob__UseManagedIdentity = true        # sin AccountKey en prod
Reporting__Pdf__Engine = Gotenberg                   # o QuestPdf
Reporting__Pdf__Gotenberg__BaseUrl = http://<gotenberg-host>:3000
Swagger__Enabled = false
```

> De dónde sale cada valor: ver `local-environment-setup.md §3` y `§4`. **No reutilizar** las credenciales expuestas en §N1 (rotarlas).

---

## 3. Despliegue paso a paso

```bash
# 1. Posicionarse en master actualizado y verde
git checkout master && git pull --ff-only
dotnet build CLARIHR.slnx -warnaserror
dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj

# 2. Aplicar migraciones pendientes contra la BD de producción
#    (exportar la cadena de prod solo en la sesión; ver manual-migrations-and-azure-deploy.md §1)
export Database__ConnectionString="Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<secret>"
dotnet ef database update \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
unset Database__ConnectionString

# 3. Publicar
dotnet publish src/CLARIHR.Api/CLARIHR.Api.csproj -c Release -o ./publish
```

4. **Desplegar al App Service** (Kudu/PowerShell — pasos exactos en `manual-migrations-and-azure-deploy.md §3`): poner `app_offline.htm`, limpiar `wwwroot`, copiar `./publish`, retirar el offline.

> **Alternativa containerizada**: `docker build -t <registry>/clarihr-api:<tag> .` → push → actualizar la App Service / orquestador a esa imagen. El `Dockerfile` ya instala `libfontconfig1` para el render PDF (§4.1).

---

## 4. Verificación post-despliegue

1. Revisar logs de arranque (sin errores de configuración — `IsConfigured` de JWT/Storage en verde).
2. Endpoint de salud / un endpoint crítico autenticado.
3. **Probar una exportación PDF end-to-end** (encolar → procesar → descargar) — valida BD + Storage + Gotenberg juntos.
4. Confirmar que **Swagger no está expuesto** (`Swagger__Enabled=false`).

---

## 5. Rollback

- App Service: redeploy del `publish` del release anterior (mismo procedimiento §3.4) o *swap* de slot si se usan deployment slots.
- Contenedor: re-apuntar a la imagen/tag anterior.
- **Migraciones**: las migraciones EF no se revierten automáticamente con el rollback de código. Si una migración rompe, revertir con `dotnet ef database update <MigracionAnterior>` antes de re-desplegar el código viejo. Preferir migraciones backward-compatible.

---

## 6. Checklist de seguridad pre-producción

- [ ] Secretos **solo** en Application Settings / secret store (no en `appsettings.*.json`).
- [ ] `Storage:AzureBlob:UseManagedIdentity = true` y **sin** `AccountKey`.
- [ ] PostgreSQL restringido por red/IP; usuario de app con permisos mínimos.
- [ ] `Swagger:Enabled = false`.
- [ ] Credenciales de §N1 **rotadas** (no reutilizadas).
- [ ] CI `secret-scan` (gitleaks) en verde.
- [ ] `SigningKey` JWT único del entorno.
