# Guia Manual: Migraciones EF y Despliegue en Azure

## Objetivo
Este documento describe:
- como ejecutar migraciones manualmente desde consola,
- y como realizar despliegue manual en Azure App Service.

## 1. Migraciones manuales (consola)

### 1.1 Ir a la raiz del repositorio
```bash
cd "/Users/christophercanas/Developments/CLARI NEW VERSION/clarihr-backend/CLARIHR-backend"
```

### 1.2 Configurar entorno (opcional)
```bash
export ASPNETCORE_ENVIRONMENT=Development
# Opcional para forzar cadena de conexion:
export Database__ConnectionString="Host=localhost;Port=5432;Database=clarihr;Username=postgres;Password=postgres"
# Configuracion Blob para fotos de expediente:
export BlobStorage__ConnectionString="<azure-blob-connection-string>"
export BlobStorage__AccountName="clarifydevblobstorage"
export BlobStorage__ProfileImagesContainer="clarihr-profile-images"
export BlobStorage__ProfileImageSasTtlMinutes="15"
```

### 1.3 Alinear herramienta EF con la version del proyecto
```bash
dotnet tool update --global dotnet-ef --version 9.0.9
dotnet ef --version
```

### 1.4 Restaurar y compilar
```bash
dotnet restore
dotnet build src/CLARIHR.Api/CLARIHR.Api.csproj
```

### 1.5 Crear migracion manualmente
```bash
dotnet ef migrations add NombreMigracion \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```

### 1.6 Aplicar migraciones pendientes
```bash
dotnet ef database update \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```

### 1.7 Verificar que no hay cambios de modelo pendientes
```bash
dotnet ef migrations has-pending-model-changes \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj --no-build
```

## 2. Publicar build para despliegue manual
```bash
dotnet publish src/CLARIHR.Api/CLARIHR.Api.csproj -c Release -o ./publish
```

## 3. Despliegue manual en Azure (Kudu / PowerShell)

Ejecutar estos comandos en la consola de Azure App Service:

```powershell
cd C:\home\site\wwwroot
New-Item app_offline.htm -ItemType File -Force
Get-ChildItem -Force | Where-Object { $_.Name -ne 'app_offline.htm' } | Remove-Item -Recurse -Force
```

Luego copiar el contenido de `publish` hacia `C:\home\site\wwwroot`.

Al finalizar, retirar el archivo offline:

```powershell
Remove-Item C:\home\site\wwwroot\app_offline.htm -Force
```

## 4. Verificacion post-despliegue
1. Revisar logs de arranque.
2. Validar endpoint de salud.
3. Probar endpoint critico del modulo desplegado.
