# File Management API — Guía de Integración Frontend

> **Versión**: 1.0  
> **Fecha**: 2026-05-01  
> **Base URL**: `https://<api-host>`  
> **Auth**: Bearer JWT en todas las rutas

---

## Tabla de contenidos

1. [Resumen del cambio](#1-resumen-del-cambio)
2. [Flujo completo: Subida de imagen de perfil](#2-flujo-completo-subida-de-imagen-de-perfil)
3. [Flujo: Mostrar imagen de perfil existente](#3-flujo-mostrar-imagen-de-perfil-existente)
4. [Flujo: Eliminar imagen de perfil](#4-flujo-eliminar-imagen-de-perfil)
5. [Endpoints — File Management](#5-endpoints--file-management)
6. [Cambios en Personnel Files](#6-cambios-en-personnel-files)
7. [Restricciones de la imagen de perfil](#7-restricciones-de-la-imagen-de-perfil)
8. [Errores posibles](#8-errores-posibles)
9. [Ejemplo de implementación](#9-ejemplo-de-implementación)

---

## 1. Resumen del cambio

### Antes (deprecado)
```
POST /api/v1/companies/{companyId}/personnel-files
PUT  /api/v1/personnel-files/{publicId}/personal-info

Body: { "photoUrl": "data:image/png;base64,..." }   ← el backend procesaba el binario
Response: { "photoUrl": "https://blob.../photo.png?sv=..." }  ← URL SAS directa
```

### Ahora (nuevo)
```
1. POST /api/v1/files/upload-session         ← obtener URL firmada de subida
2. PUT  <uploadUrl>                           ← subir binario DIRECTO al storage
3. POST /api/v1/files/{filePublicId}/complete ← confirmar la subida
4. PUT  /personal-info  { "photoFilePublicId": "<uuid>" }  ← asociar al expediente
```

**Cambio clave**: El campo `photoUrl` (string) fue reemplazado por `photoFilePublicId` (uuid) en `create` y `personal-info`. El frontend ya **no envía binarios ni base64** al backend.

---

## 2. Flujo completo: Subida de imagen de perfil

```
┌─────────┐      ┌─────────┐      ┌──────────────┐
│ Frontend │      │ Backend │      │ Azure Storage│
└────┬────┘      └────┬────┘      └──────┬───────┘
     │                │                   │
     │ 1. POST /files/upload-session      │
     │───────────────>│                   │
     │                │  (valida reglas,  │
     │                │   crea registro,  │
     │                │   genera SAS URL) │
     │<───────────────│                   │
     │  { filePublicId, uploadUrl,        │
     │    expiresUtc, requiredHeaders }   │
     │                │                   │
     │ 2. PUT uploadUrl + requiredHeaders │
     │────────────────────────────────────>│
     │<────────────────────────────────────│
     │  201 Created                       │
     │                │                   │
     │ 3. POST /files/{id}/complete       │
     │───────────────>│                   │
     │                │ (verifica blob    │
     │                │  existe, activa)  │
     │<───────────────│                   │
     │  { filePublicId, status: "Active" }│
     │                │                   │
     │ 4. PUT /personal-info              │
     │   { photoFilePublicId: "<uuid>" }  │
     │───────────────>│                   │
     │<───────────────│                   │
     │  expediente actualizado            │
     │                │                   │
```

---

## 3. Flujo: Mostrar imagen de perfil existente

Cuando el expediente tiene `photoFilePublicId` en la respuesta:

```
┌─────────┐      ┌─────────┐      ┌──────────────┐
│ Frontend │      │ Backend │      │ Azure Storage│
└────┬────┘      └────┬────┘      └──────┬───────┘
     │                │                   │
     │ GET /files/{photoFilePublicId}/read-url
     │───────────────>│                   │
     │<───────────────│                   │
     │  { readUrl, expiresUtc }           │
     │                │                   │
     │ GET readUrl (directa)              │
     │────────────────────────────────────>│
     │<────────────────────────────────────│
     │  imagen binaria                    │
     │                │                   │
```

> **Nota**: El `readUrl` expira en ~15 minutos. Si el usuario permanece en pantalla más tiempo, solicitar uno nuevo antes de usar el anterior.

---

## 4. Flujo: Eliminar imagen de perfil

```
1. PUT /personal-info  { "photoFilePublicId": null }   ← desasocia la foto
2. DELETE /api/v1/files/{filePublicId}                  ← (opcional) marca el archivo como eliminado
```

---

## 5. Endpoints — File Management

### 5.1 Crear sesión de subida

```
POST /api/v1/files/upload-session
```

**Headers**:
```
Authorization: Bearer <jwt>
Content-Type: application/json
```

**Request body**:
```json
{
  "fileName": "avatar.png",
  "contentType": "image/png",
  "sizeBytes": 245760,
  "purpose": "ProfileImage",
  "entityId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"  // opcional, publicId del personnel file
}
```

| Campo | Tipo | Requerido | Descripción |
|---|---|---|---|
| `fileName` | string | ✅ | Nombre original del archivo con extensión |
| `contentType` | string | ✅ | MIME type: `image/jpeg`, `image/png` o `image/webp` |
| `sizeBytes` | long | ✅ | Tamaño del archivo en bytes (máx 5 MB = 5242880) |
| `purpose` | string | ✅ | Siempre `"ProfileImage"` para fotos de perfil |
| `entityId` | uuid | ❌ | PublicId del personnel file (referencia opcional) |

**Response `200 OK`**:
```json
{
  "filePublicId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "uploadUrl": "https://clarifydevblobstorage.blob.core.windows.net/clarihr-profile-images/ProfileImage/tenant-id/user-id/f7e6d5c4.png?sv=2024-11-04&se=2026-05-01T22:30:00Z&sr=b&sp=cw&sig=...",
  "expiresUtc": "2026-05-01T22:30:00Z",
  "requiredHeaders": {
    "x-ms-blob-type": "BlockBlob",
    "Content-Type": "image/png"
  }
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `filePublicId` | uuid | ID público del archivo creado. **Guardar este valor.** |
| `uploadUrl` | string | URL firmada para subir el binario directo al storage |
| `expiresUtc` | datetime | Fecha de expiración de la URL (10 minutos) |
| `requiredHeaders` | object | Headers obligatorios para el PUT al storage |

**Errores**:

| Status | Código | Causa |
|---|---|---|
| 400 | `files.invalid_purpose` | Purpose no reconocido |
| 413 | `files.too_large` | Archivo excede 5 MB |
| 422 | `files.content_type_not_allowed` | MIME type no permitido |
| 422 | `files.extension_not_allowed` | Extensión de archivo no permitida |
| 422 | `files.purpose_not_configured` | Purpose sin reglas configuradas |

---

### 5.2 Subir binario al storage (directo, sin pasar por backend)

```
PUT <uploadUrl>
```

**Headers** (TODOS los que vienen en `requiredHeaders` + Content-Length):
```
x-ms-blob-type: BlockBlob
Content-Type: image/png
Content-Length: 245760
```

**Body**: El archivo binario crudo (NO base64, NO multipart, NO JSON).

**Response**: `201 Created` (de Azure Blob Storage, no del backend).

> ⚠️ **Importante**: Esta petición va DIRECTAMENTE a Azure Storage, no al backend de CLARIHR. No enviar el header `Authorization: Bearer`.

---

### 5.3 Confirmar subida

```
POST /api/v1/files/{filePublicId}/complete
```

**Headers**:
```
Authorization: Bearer <jwt>
```

**URL params**:
| Param | Tipo | Descripción |
|---|---|---|
| `filePublicId` | uuid | El `filePublicId` recibido en el paso 1 |

**Request body**: Vacío (no requiere body).

**Response `200 OK`**:
```json
{
  "filePublicId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "status": "Active"
}
```

**Errores**:

| Status | Código | Causa |
|---|---|---|
| 403 | `files.ownership_mismatch` | El usuario autenticado no es quien creó la sesión |
| 404 | `files.not_found` | El `filePublicId` no existe |
| 422 | `files.not_pending_upload` | El archivo ya fue completado o eliminado |
| 422 | `files.upload_not_found` | El binario no se encontró en Azure (subida falló o expiró) |

---

### 5.4 Obtener URL de lectura

```
GET /api/v1/files/{filePublicId}/read-url
```

**Headers**:
```
Authorization: Bearer <jwt>
```

**Response `200 OK`**:
```json
{
  "readUrl": "https://clarifydevblobstorage.blob.core.windows.net/clarihr-profile-images/ProfileImage/tenant-id/user-id/f7e6d5c4.png?sv=2024-11-04&se=2026-05-01T22:45:00Z&sr=b&sp=r&sig=...",
  "expiresUtc": "2026-05-01T22:45:00Z"
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `readUrl` | string | URL firmada de solo lectura. Usar directamente en `<img src>` |
| `expiresUtc` | datetime | Expiración de la URL (~15 minutos) |

**Errores**:

| Status | Código | Causa |
|---|---|---|
| 404 | `files.not_found` | El `filePublicId` no existe |
| 422 | `files.not_active` | El archivo no está activo (pendiente, eliminado o fallido) |

---

### 5.5 Eliminar archivo

```
DELETE /api/v1/files/{filePublicId}
```

**Headers**:
```
Authorization: Bearer <jwt>
```

**Response `200 OK`**:
```json
{
  "filePublicId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "status": "Deleted"
}
```

**Errores**:

| Status | Código | Causa |
|---|---|---|
| 403 | `files.ownership_mismatch` | El usuario autenticado no es el owner |
| 404 | `files.not_found` | El `filePublicId` no existe |

---

## 6. Cambios en Personnel Files

### 6.1 Crear expediente

```
POST /api/v1/companies/{companyPublicId}/personnel-files
```

**Cambio**: El campo `photoUrl` (string) fue reemplazado por `photoFilePublicId` (uuid).

```json
{
  "recordType": "Employee",
  "firstName": "Juan",
  "lastName": "Pérez",
  "birthDate": "1990-01-15T00:00:00Z",
  "photoFilePublicId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "assignedPositionSlotPublicId": "...",
  ...
}
```

### 6.2 Actualizar datos personales

```
PUT /api/v1/personnel-files/{publicId}/personal-info
```

**Cambio**: Mismo reemplazo de `photoUrl` → `photoFilePublicId`.

```json
{
  "recordType": "Employee",
  "firstName": "Juan",
  "lastName": "Pérez",
  "birthDate": "1990-01-15T00:00:00Z",
  "photoFilePublicId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "concurrencyToken": "...",
  ...
}
```

### 6.3 Respuestas del expediente

En el shell del expediente (`GET /api/v1/personnel-files/{publicId}`):

```json
{
  "id": "...",
  "companyId": "...",
  "recordType": "Employee",
  "lifecycleStatus": "Draft",
  "fullName": "Juan Pérez",
  "photoFilePublicId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "isActive": true,
  "orgUnitId": "...",
  "assignedPositionSlotId": "...",
  "concurrencyToken": "...",
  ...
}
```

> **Nota**: La respuesta devuelve `photoFilePublicId` como `string` (el Guid convertido). Para mostrar la imagen, usar `GET /api/v1/files/{photoFilePublicId}/read-url` y luego renderizar con la `readUrl` resultante.

---

## 7. Restricciones de la imagen de perfil

| Restricción | Valor |
|---|---|
| **Purpose** | `ProfileImage` |
| **Tamaño máximo** | 5 MB (5,242,880 bytes) |
| **Content types permitidos** | `image/jpeg`, `image/png`, `image/webp` |
| **Extensiones permitidas** | `.jpg`, `.jpeg`, `.png`, `.webp` |
| **Contenedor de destino** | `clarihr-profile-images` |
| **Expiración URL de subida** | 10 minutos |
| **Expiración URL de lectura** | 15 minutos |

---

## 8. Errores posibles

### Errores de File Management

| Código | HTTP | Descripción |
|---|---|---|
| `files.not_found` | 404 | Archivo no encontrado |
| `files.not_active` | 422 | El archivo no está activo |
| `files.not_pending_upload` | 422 | No se puede completar, ya fue procesado |
| `files.ownership_mismatch` | 403 | No eres el dueño del archivo |
| `files.tenant_mismatch` | 403 | El archivo no pertenece a tu organización |
| `files.too_large` | 413 | Archivo excede el tamaño máximo |
| `files.content_type_not_allowed` | 422 | MIME type no permitido |
| `files.extension_not_allowed` | 400 | Extensión no permitida |
| `files.upload_not_found` | 422 | Binario no encontrado en storage |
| `files.invalid_purpose` | 400 | Purpose no válido |
| `files.purpose_not_configured` | 422 | Purpose sin reglas configuradas |
| `files.provider_not_configured` | 503 | Proveedor de storage no configurado |

### Formato estándar de error (ProblemDetails)

```json
{
  "type": "files.too_large",
  "title": "The file exceeds the maximum allowed size.",
  "status": 413,
  "detail": "...",
  "instance": "/api/v1/files/upload-session"
}
```

---

## 9. Ejemplo de implementación

### TypeScript / React (ejemplo conceptual)

```typescript
// ─── Tipos ────────────────────────────────────────────────────
interface UploadSessionResponse {
  filePublicId: string;
  uploadUrl: string;
  expiresUtc: string;
  requiredHeaders: Record<string, string>;
}

interface CompleteResponse {
  filePublicId: string;
  status: string;
}

interface ReadUrlResponse {
  readUrl: string;
  expiresUtc: string;
}

// ─── Servicio de archivos ─────────────────────────────────────
class FileService {
  constructor(private apiBase: string, private getToken: () => string) {}

  private get headers() {
    return {
      'Authorization': `Bearer ${this.getToken()}`,
      'Content-Type': 'application/json',
    };
  }

  /** Paso 1: Crear sesión de subida */
  async createUploadSession(
    file: File,
    purpose: string = 'ProfileImage',
    entityId?: string
  ): Promise<UploadSessionResponse> {
    const res = await fetch(`${this.apiBase}/api/v1/files/upload-session`, {
      method: 'POST',
      headers: this.headers,
      body: JSON.stringify({
        fileName: file.name,
        contentType: file.type,
        sizeBytes: file.size,
        purpose,
        entityId: entityId ?? null,
      }),
    });

    if (!res.ok) throw await res.json();
    return res.json();
  }

  /** Paso 2: Subir binario directo al storage */
  async uploadToStorage(
    uploadUrl: string,
    file: File,
    requiredHeaders: Record<string, string>
  ): Promise<void> {
    const res = await fetch(uploadUrl, {
      method: 'PUT',
      headers: {
        ...requiredHeaders,
        'Content-Length': file.size.toString(),
      },
      body: file,  // binario directo, NO base64
    });

    if (!res.ok) {
      throw new Error(`Storage upload failed: ${res.status} ${res.statusText}`);
    }
  }

  /** Paso 3: Confirmar subida */
  async completeUpload(filePublicId: string): Promise<CompleteResponse> {
    const res = await fetch(
      `${this.apiBase}/api/v1/files/${filePublicId}/complete`,
      {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${this.getToken()}` },
      }
    );

    if (!res.ok) throw await res.json();
    return res.json();
  }

  /** Obtener URL de lectura */
  async getReadUrl(filePublicId: string): Promise<ReadUrlResponse> {
    const res = await fetch(
      `${this.apiBase}/api/v1/files/${filePublicId}/read-url`,
      {
        method: 'GET',
        headers: { 'Authorization': `Bearer ${this.getToken()}` },
      }
    );

    if (!res.ok) throw await res.json();
    return res.json();
  }

  /** Eliminar archivo */
  async deleteFile(filePublicId: string): Promise<void> {
    const res = await fetch(
      `${this.apiBase}/api/v1/files/${filePublicId}`,
      {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${this.getToken()}` },
      }
    );

    if (!res.ok) throw await res.json();
  }
}

// ─── Uso: Subir foto de perfil ────────────────────────────────
async function uploadProfilePhoto(
  fileService: FileService,
  personnelFilePublicId: string,
  file: File,
  concurrencyToken: string,
  updatePersonalInfo: (body: any) => Promise<any>
) {
  // Validación frontend rápida
  const MAX_SIZE = 5 * 1024 * 1024; // 5 MB
  const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

  if (file.size > MAX_SIZE) {
    throw new Error('La imagen no puede exceder 5 MB');
  }
  if (!ALLOWED_TYPES.includes(file.type)) {
    throw new Error('Solo se permiten imágenes JPG, PNG o WebP');
  }

  // 1. Crear sesión
  const session = await fileService.createUploadSession(
    file,
    'ProfileImage',
    personnelFilePublicId
  );

  // 2. Subir directo al storage
  await fileService.uploadToStorage(
    session.uploadUrl,
    file,
    session.requiredHeaders
  );

  // 3. Confirmar
  const completed = await fileService.completeUpload(session.filePublicId);
  console.log('Archivo activo:', completed.status);

  // 4. Asociar al expediente
  await updatePersonalInfo({
    photoFilePublicId: session.filePublicId,
    concurrencyToken,
    // ... demás campos de personal-info
  });

  return session.filePublicId;
}

// ─── Uso: Mostrar foto de perfil ─────────────────────────────
async function getProfilePhotoUrl(
  fileService: FileService,
  photoFilePublicId: string | null
): Promise<string | null> {
  if (!photoFilePublicId) return null;

  const { readUrl } = await fileService.getReadUrl(photoFilePublicId);
  return readUrl;  // usar como <img src={readUrl} />
}

// ─── Uso: Quitar foto de perfil ──────────────────────────────
async function removeProfilePhoto(
  fileService: FileService,
  personnelFilePublicId: string,
  currentPhotoFilePublicId: string,
  concurrencyToken: string,
  updatePersonalInfo: (body: any) => Promise<any>
) {
  // 1. Desasociar del expediente
  await updatePersonalInfo({
    photoFilePublicId: null,
    concurrencyToken,
    // ... demás campos
  });

  // 2. Eliminar archivo (opcional pero recomendado)
  await fileService.deleteFile(currentPhotoFilePublicId);
}
```

---

## Checklist de implementación frontend

- [ ] Reemplazar toda referencia a `photoUrl` por `photoFilePublicId` en tipos/interfaces
- [ ] Implementar servicio de File Management con los 4 endpoints
- [ ] Implementar flujo de subida: session → upload → complete → associate
- [ ] Implementar resolución de URL de lectura para mostrar imágenes
- [ ] Agregar validación frontend de tamaño (5 MB) y tipo (jpeg/png/webp) antes de iniciar
- [ ] Manejar expiración de URLs de lectura (refresh después de ~15 min)
- [ ] Manejar errores de cada paso del flujo con mensajes claros al usuario
- [ ] Actualizar payloads de `POST /personnel-files` y `PUT /personal-info`
- [ ] Probar flujo completo: subir → asociar → cerrar y reabrir → verificar que la imagen carga
