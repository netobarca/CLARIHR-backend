# Cambios requeridos en Backend .NET para File Management seguro, escalable y multi-provider

## 1. Objetivo

Centralizar en el backend .NET la gestión de archivos para eliminar credenciales del frontend, mantener containers/buckets privados y permitir múltiples proveedores de almacenamiento como:

- Azure Blob Storage
- AWS S3
- Google Cloud Storage
- Otros proveedores futuros

El backend debe actuar como:

```text
File Management API
+
Storage Provider Broker
+
Authorization Layer
+
Metadata Owner
```

## 2. Principios de diseño

1. El frontend no conoce credenciales permanentes.
2. El frontend no decide rutas finales de almacenamiento.
3. El backend valida usuario, permisos, tipo de archivo, tamaño y propósito.
4. El backend genera URLs temporales por operación.
5. El archivo se sube directo al storage para evitar cargar el backend.
6. PostgreSQL guarda metadata, no binarios.
7. El diseño debe ser provider-agnostic.

## 3. Flujo de carga recomendado

```text
1. Angular Browser solicita sesión de carga al Angular Server.
2. Angular Server llama a .NET API.
3. .NET valida autorización y reglas de negocio.
4. .NET crea metadata en PostgreSQL con estado PENDING_UPLOAD.
5. .NET resuelve proveedor de storage.
6. .NET genera URL temporal de upload.
7. Angular Browser sube directo al storage.
8. Angular Browser confirma carga.
9. .NET verifica existencia del objeto.
10. .NET marca archivo como ACTIVE.
```

## 4. Endpoints requeridos

### 4.1 Crear sesión de carga

```http
POST /api/files/upload-session
Authorization: Bearer <token>
Content-Type: application/json
```

Request:

```json
{
  "fileName": "profile.png",
  "contentType": "image/png",
  "sizeBytes": 420000,
  "purpose": "PROFILE_IMAGE",
  "entityId": "user-123"
}
```

Response:

```json
{
  "fileId": "9cfd3f9a-508a-41f0-b728-5b1d0d739e6d",
  "provider": "AzureBlob",
  "uploadType": "SignedUrl",
  "method": "PUT",
  "url": "https://storage-provider/object?<temporary-token>",
  "headers": {
    "Content-Type": "image/png",
    "x-ms-blob-type": "BlockBlob"
  },
  "formFields": null,
  "expiresAt": "2026-05-01T18:30:00Z"
}
```

### 4.2 Confirmar carga

```http
POST /api/files/{fileId}/complete
Authorization: Bearer <token>
```

Responsabilidad:

- Validar que el usuario puede completar ese archivo.
- Verificar que el objeto existe en el provider.
- Leer metadata del objeto si aplica.
- Marcar estado `ACTIVE`.

Response:

```json
{
  "fileId": "9cfd3f9a-508a-41f0-b728-5b1d0d739e6d",
  "status": "ACTIVE"
}
```

### 4.3 Obtener URL temporal de lectura

```http
GET /api/files/{fileId}/read-url
Authorization: Bearer <token>
```

Responsabilidad:

- Validar permisos del usuario.
- Validar que el archivo está `ACTIVE`.
- Generar URL temporal de lectura.

Response:

```json
{
  "fileId": "9cfd3f9a-508a-41f0-b728-5b1d0d739e6d",
  "readUrl": "https://storage-provider/object?<temporary-read-token>",
  "expiresAt": "2026-05-01T18:45:00Z"
}
```

### 4.4 Eliminación lógica

```http
DELETE /api/files/{fileId}
Authorization: Bearer <token>
```

Responsabilidad:

- Marcar `DELETED`.
- Opcionalmente eliminar físicamente por job asíncrono.
- Evitar romper auditoría o referencias históricas.

## 5. Modelo de datos recomendado en PostgreSQL

```sql
CREATE TABLE files (
    id UUID PRIMARY KEY,
    tenant_id UUID NULL,
    owner_id UUID NULL,
    entity_id TEXT NULL,
    purpose VARCHAR(50) NOT NULL,

    provider VARCHAR(50) NOT NULL,
    bucket_or_container VARCHAR(255) NOT NULL,
    object_key TEXT NOT NULL,

    original_file_name TEXT NOT NULL,
    content_type VARCHAR(150) NOT NULL,
    extension VARCHAR(20) NULL,
    size_bytes BIGINT NULL,
    checksum_sha256 TEXT NULL,

    status VARCHAR(30) NOT NULL,
    visibility VARCHAR(30) NOT NULL DEFAULT 'PRIVATE',

    created_by UUID NULL,
    created_at TIMESTAMP NOT NULL,
    upload_url_expires_at TIMESTAMP NULL,
    uploaded_at TIMESTAMP NULL,
    activated_at TIMESTAMP NULL,
    deleted_at TIMESTAMP NULL,

    metadata JSONB NULL
);

CREATE UNIQUE INDEX ux_files_provider_object_key
ON files(provider, bucket_or_container, object_key);

CREATE INDEX ix_files_owner_purpose
ON files(owner_id, purpose);

CREATE INDEX ix_files_status_created_at
ON files(status, created_at);
```

Estados recomendados:

```text
PENDING_UPLOAD
UPLOADED
SCANNING
ACTIVE
FAILED
DELETED
QUARANTINED
```

## 6. Reglas por tipo de archivo

Ejemplo inicial:

```json
{
  "PROFILE_IMAGE": {
    "maxSizeBytes": 5242880,
    "allowedContentTypes": ["image/jpeg", "image/png", "image/webp"],
    "allowedExtensions": [".jpg", ".jpeg", ".png", ".webp"],
    "defaultProvider": "AzureBlob",
    "requiresMalwareScan": false
  },
  "DOCUMENT": {
    "maxSizeBytes": 26214400,
    "allowedContentTypes": [
      "application/pdf",
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    ],
    "allowedExtensions": [".pdf", ".docx", ".xlsx"],
    "defaultProvider": "AzureBlob",
    "requiresMalwareScan": true
  }
}
```

## 7. Generación de object keys

El backend debe generar la ruta final. No usar directamente el nombre original como llave.

Formato sugerido:

```text
tenants/{tenantId}/users/{userId}/profile/{fileId}{extension}
tenants/{tenantId}/documents/{entityId}/{fileId}{extension}
```

Ejemplo:

```text
tenants/acme/users/123/profile/9cfd3f9a-508a-41f0-b728-5b1d0d739e6d.png
```

Beneficios:

- Evita colisiones.
- Evita path traversal.
- Facilita lifecycle policies.
- Facilita segregación por tenant.
- Facilita auditoría.

## 8. Abstracción multi-provider

Crear una interfaz común:

```csharp
public interface IFileStorageProvider
{
    StorageProvider Provider { get; }

    Task<CreateUploadSessionResult> CreateUploadSessionAsync(
        CreateUploadSessionCommand command,
        CancellationToken cancellationToken);

    Task<CreateReadSessionResult> CreateReadSessionAsync(
        CreateReadSessionCommand command,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        string bucketOrContainer,
        string objectKey,
        CancellationToken cancellationToken);

    Task<FileObjectInfo?> GetObjectInfoAsync(
        string bucketOrContainer,
        string objectKey,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string bucketOrContainer,
        string objectKey,
        CancellationToken cancellationToken);
}
```

Resolver:

```csharp
public interface IFileStorageProviderResolver
{
    IFileStorageProvider Resolve(StorageProvider provider);
}
```

Implementaciones:

```text
AzureBlobStorageProvider
AwsS3StorageProvider
GoogleCloudStorageProvider
```

## 9. DTOs sugeridos

```csharp
public sealed record CreateUploadSessionRequest(
    string FileName,
    string ContentType,
    long SizeBytes,
    string Purpose,
    string? EntityId
);

public sealed record CreateUploadSessionResponse(
    Guid FileId,
    string Provider,
    string UploadType,
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string>? FormFields,
    DateTimeOffset ExpiresAt
);
```

## 10. Azure Blob Storage Provider

### 10.1 Autenticación recomendada

Para Azure Blob, usar:

```text
Managed Identity
+
Azure RBAC
+
User Delegation SAS
```

En producción, preferir `ManagedIdentityCredential` cuando el backend corre en Azure.

Paquetes NuGet:

```bash
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Identity
```

### 10.2 Configuración

```json
{
  "Storage": {
    "DefaultProvider": "AzureBlob",
    "Providers": {
      "AzureBlob": {
        "AccountName": "clarifydevblobstorage",
        "ContainerName": "clarihr-profile-images",
        "BlobEndpoint": "https://clarifydevblobstorage.blob.core.windows.net",
        "UploadUrlExpirationMinutes": 10,
        "ReadUrlExpirationMinutes": 15
      }
    }
  }
}
```

### 10.3 Cliente BlobServiceClient

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;

var blobServiceClient = new BlobServiceClient(
    new Uri("https://clarifydevblobstorage.blob.core.windows.net"),
    new ManagedIdentityCredential());
```

Para desarrollo local puede usarse `DefaultAzureCredential`, siempre que el desarrollador tenga los roles RBAC adecuados.

### 10.4 Generación conceptual de User Delegation SAS

```csharp
using Azure.Storage.Sas;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

public async Task<CreateUploadSessionResult> CreateUploadSessionAsync(
    CreateUploadSessionCommand command,
    CancellationToken cancellationToken)
{
    var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
    var expiresOn = DateTimeOffset.UtcNow.AddMinutes(10);

    var userDelegationKey = await _blobServiceClient.GetUserDelegationKeyAsync(
        startsOn,
        expiresOn,
        cancellationToken);

    var containerClient = _blobServiceClient.GetBlobContainerClient(command.ContainerName);
    var blobClient = containerClient.GetBlobClient(command.ObjectKey);

    var sasBuilder = new BlobSasBuilder
    {
        BlobContainerName = command.ContainerName,
        BlobName = command.ObjectKey,
        Resource = "b",
        StartsOn = startsOn,
        ExpiresOn = expiresOn,
        Protocol = SasProtocol.Https
    };

    sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

    var sas = sasBuilder.ToSasQueryParameters(
        userDelegationKey,
        _storageAccountName);

    var uploadUrl = $"{blobClient.Uri}?{sas}";

    return new CreateUploadSessionResult(
        Url: uploadUrl,
        Method: "PUT",
        Headers: new Dictionary<string, string>
        {
            ["x-ms-blob-type"] = "BlockBlob",
            ["Content-Type"] = command.ContentType
        },
        ExpiresAt: expiresOn
    );
}
```

### 10.5 URL de lectura

Para lectura:

```csharp
sasBuilder.SetPermissions(BlobSasPermissions.Read);
```

La URL de lectura debe expirar rápido, por ejemplo entre 5 y 30 minutos.

## 11. AWS S3 Provider

Para S3, el provider debe generar:

```text
Presigned PUT URL
```

O, si el flujo lo requiere:

```text
Presigned POST Policy
```

El contrato debe soportar ambos:

```csharp
public enum UploadType
{
    SignedUrl,
    SignedPost
}
```

No modificar Angular cuando se agregue S3. Angular solo ejecuta las instrucciones recibidas.

## 12. Google Cloud Storage Provider

Para GCS, el provider debe generar:

```text
V4 Signed URL
```

La misma respuesta normalizada puede usarse con `method`, `url`, `headers` y `expiresAt`.

## 13. Seguridad y autorización

El backend debe validar:

- Usuario autenticado.
- Permiso para subir archivo en la entidad solicitada.
- Permiso para leer archivo.
- Propósito del archivo.
- Tenant correcto.
- Tamaño máximo permitido.
- MIME type permitido.
- Extensión permitida.
- Estado del archivo.
- Que el usuario no pueda confirmar un archivo de otro usuario.

No confiar en:

- Nombre del archivo enviado por frontend.
- Content-Type del navegador como única validación.
- Rutas enviadas por frontend.
- URLs firmadas antiguas.
- Metadata no verificada.

## 14. Integración con Angular Server / BFF

El backend .NET debe aceptar llamadas desde el Angular Server y validar identidad.

Opciones:

1. El Angular Server propaga JWT del usuario.
2. El Angular Server usa token interno para llamadas server-to-server y envía identidad del usuario en claims confiables.
3. Usar OAuth2 client credentials para Angular Server -> .NET.
4. Agregar mTLS o API gateway si el entorno lo requiere.

## 15. Confirmación robusta de upload

Al recibir `/complete`, el backend debe:

1. Buscar registro en PostgreSQL.
2. Verificar que está en `PENDING_UPLOAD`.
3. Consultar el provider con `ExistsAsync`.
4. Leer tamaño y content type real si el provider lo permite.
5. Comparar contra metadata esperada.
6. Marcar `ACTIVE` o `FAILED`.

## 16. Limpieza de archivos huérfanos

Crear un job programado:

```text
files.status = PENDING_UPLOAD
AND files.created_at < now() - interval '24 hours'
```

Acciones:

- Marcar como `FAILED` o `EXPIRED`.
- Eliminar objeto físico si existe.
- Registrar auditoría.

## 17. Observabilidad

Registrar eventos:

```text
file.upload_session.created
file.upload.completed
file.upload.failed
file.read_url.created
file.deleted
file.provider.error
```

Campos mínimos:

```text
fileId
provider
container/bucket
objectKey
userId
tenantId
purpose
status
correlationId
durationMs
```

Métricas:

- Upload sessions creadas.
- Uploads completados.
- Uploads expirados.
- Errores por provider.
- Latencia por provider.
- Tamaño promedio de archivos.
- Conteo por tipo de archivo.

## 18. Criterios de aceptación backend

- El backend genera URLs temporales de upload por archivo.
- El backend genera URLs temporales de lectura para archivos privados.
- El backend puede usar Azure Blob sin connection string ni account key.
- La Managed Identity tiene permisos por RBAC.
- PostgreSQL guarda metadata de archivos.
- Angular no necesita saber el proveedor real.
- El diseño permite agregar S3 o GCS sin cambiar el contrato del frontend.
- Las URLs temporales expiran.
- El storage se mantiene privado.
- Se puede limpiar uploads incompletos.
- Se registran eventos y errores relevantes.

## 19. Referencias oficiales

- Azure Blob Storage con .NET: https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-dotnet-get-started
- User Delegation SAS con .NET: https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-user-delegation-sas-create-dotnet
- Autorizar Blob Storage con Microsoft Entra ID: https://learn.microsoft.com/en-us/azure/storage/blobs/authorize-access-azure-active-directory
- Azure RBAC para acceso a Blob Storage: https://learn.microsoft.com/en-us/azure/storage/blobs/assign-azure-role-data-access
- Azure Identity para .NET: https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme
