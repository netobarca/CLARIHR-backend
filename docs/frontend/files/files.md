# Files — Guía de consumo (frontend) · Fase 16

> **Prerequisitos:** [onboarding 1–6](../README.md). Esta es la API **genérica de archivos**: la usan
> los [documentos de Personnel Files](../personnel-files/documents.md) (Fase 9), las fotos de perfil,
> los logos, etc. Convenciones globales en el [índice maestro](../README.md).

---

## Overview

La **Files API** gestiona la subida y descarga de binarios mediante **subida directa a storage** (el
cliente sube el archivo a una URL pre-firmada, no a través de la API). El registro guarda solo la
metadata; el binario vive en el blob storage.

Un controlador, **4 endpoints** (base `/api/v1/files`):

| Método | Ruta | Para qué |
|--------|------|----------|
| `POST` | `/api/v1/files/upload-session` | iniciar una subida (reserva el registro + da la URL de subida) |
| `PATCH` | `/api/v1/files/{filePublicId}/complete` | finalizar la subida (marca el archivo `Active`) |
| `GET` | `/api/v1/files/{filePublicId}/read-url` | URL de descarga (**solo el uploader**) |
| `DELETE` | `/api/v1/files/{filePublicId}` | borrar (soft-delete) |

**Permiso:** solo autenticación (`Bearer`). **No hay RBAC**; el control es por **ownership** (solo
quien subió el archivo puede completarlo, descargarlo o borrarlo).

### Conceptos clave (leer primero)

- **Subida directa**: el binario **no pasa por la API** — se sube a la `uploadUrl` pre-firmada que
  devuelve `upload-session`. La API solo reserva el registro y valida la metadata.
- **Owner-only**: `complete`, `read-url` y `delete` están restringidos al **uploader** (`403
  files.ownership_mismatch` si no). El `read-url` genérico **solo sirve al que subió el archivo** —
  para que **otros usuarios autorizados** descarguen (ej. RRHH viendo documentos de un empleado),
  cada dominio expone su **propio** `read-url` autorizado (ej.
  [`/personnel-files/{id}/documents/{docId}/read-url`](../personnel-files/documents.md)). **No uses el
  genérico para servir archivos a terceros.**
- **`purpose`** (obligatorio): define las reglas de validación (tipos/extensiones/tamaño permitidos)
  y dónde se guarda. Ver enum abajo.
- **Sin rate limit excesivo pero presente**: upload 20/min, read 120/min, lifecycle
  (complete/delete) 30/min, por usuario.

---

## El flujo de subida (3 pasos)

```
1. POST /files/upload-session {fileName, contentType, sizeBytes, purpose}
      → { filePublicId, uploadUrl, requiredHeaders, expiresUtc, concurrencyToken }
2. PUT  <uploadUrl>   (subí el binario directo a storage, con los requiredHeaders)
3. PATCH /files/{filePublicId}/complete { concurrencyToken: <el del paso 1> }
      → { filePublicId, status: "Active" }
```

> Recién en el paso 3 el archivo queda usable. Antes está `PendingUpload`.

### 1. `POST /api/v1/files/upload-session`

**Request body:**

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `fileName` | string | Sí | nombre original (la extensión se valida según el `purpose`) |
| `contentType` | string | Sí | MIME; debe estar permitido por el `purpose` (→ `422 files.content_type_not_allowed`) |
| `sizeBytes` | int64 | Sí | tamaño; excede el máximo del `purpose` → `413 files.too_large` |
| `purpose` | enum string | Sí | `ProfileImage`/`PersonnelDocument`/`ReportExport`/`CompanyLogo`/`Attachment`; desconocido → `400 files.invalid_purpose` |
| `entityPublicId` | uuid | No | id de la entidad relacionada (opcional) |

**Response `200`** — `CreateUploadSessionResponse`:

```json
{
  "filePublicId": "…",
  "uploadUrl": "https://<storage>/…?<SAS write-only>",
  "requiredHeaders": { "x-ms-blob-type": "BlockBlob", "Content-Type": "application/pdf" },
  "expiresUtc": "2026-06-10T16:00:00Z",
  "concurrencyToken": "…"
}
```

- Subí el binario a `uploadUrl` (HTTP `PUT`) **con los `requiredHeaders` exactos**.
- Guardá el `concurrencyToken` para el `complete`.
- La `uploadUrl` es **write-only** y de corta duración (`expiresUtc`).

Errores: `400 files.invalid_purpose` · `422` (tipo/extensión no permitidos) · `413 files.too_large`.

### 2. Subir el binario

`PUT <uploadUrl>` con el archivo como cuerpo y los `requiredHeaders`. Esto va **directo al storage**,
no a la API CLARIHR. (No requiere el token Bearer — la SAS autoriza.)

### 3. `PATCH /api/v1/files/{filePublicId}/complete`

**Request body:**

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `concurrencyToken` | uuid | Sí | el del `upload-session`; stale → `409 files.concurrency_conflict` |

> ⚠️ El token va en el **body** (no en un header `If-Match`) — es el único endpoint con esta forma.

El backend verifica que el binario exista en storage, adopta su `sizeBytes`/`contentType` reales y
marca el archivo `Active`. **Response `200`** — `{ filePublicId, status: "Active" }`.

Errores: `403 files.ownership_mismatch` (no sos el uploader) · `404 files.not_found` · `409
files.concurrency_conflict` · `422 files.not_pending_upload` / `files.upload_not_found` (el archivo no
estaba pendiente o el binario no llegó a storage).

---

## Descargar — `GET /api/v1/files/{filePublicId}/read-url`

Devuelve una URL pre-firmada **read-only** de corta duración para descargar el binario. **Solo el
uploader** (`403 files.ownership_mismatch`).

```json
{ "readUrl": "https://<storage>/…?<SAS read-only>", "expiresUtc": "…" }
```

Errores: `403 files.ownership_mismatch` · `404 files.not_found` · `422 files.not_active` (el archivo
no está `Active`).

> Para descargar archivos de **otros** usuarios autorizados, usá el endpoint del dominio
> correspondiente (no este genérico). Ej.: documentos de personal →
> [`/personnel-files/{id}/documents/{docId}/read-url`](../personnel-files/documents.md).

---

## Borrar — `DELETE /api/v1/files/{filePublicId}`

Soft-delete: marca el archivo `Deleted` (el binario lo recupera un proceso de limpieza en segundo
plano). **Solo el uploader** (`403`). Response `200` — `{ filePublicId, status: "Deleted" }`.

---

## Enums (wire, string)

| Enum | Valores |
|------|---------|
| `FilePurpose` | `ProfileImage` · `PersonnelDocument` · `ReportExport` · `CompanyLogo` · `Attachment` |
| `FileStatus` | `PendingUpload` · `Active` · `Failed` · `Deleted` · `Quarantined` |

## Catálogo de errores

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `files.invalid_purpose` | 400 | `purpose` desconocido |
| `files.content_type_not_allowed` | 422 | el MIME no está permitido para ese `purpose` |
| `files.extension_not_allowed` | 422 | la extensión del `fileName` no está permitida |
| `files.too_large` | 413 | excede el tamaño máximo del `purpose` |
| `files.ownership_mismatch` | 403 | no sos el uploader del archivo |
| `files.tenant_mismatch` | 403 | el archivo es de otra compañía |
| `files.not_found` | 404 | archivo inexistente |
| `files.not_pending_upload` | 422 | `complete` de un archivo que no está pendiente |
| `files.upload_not_found` | 422 | el binario no se encontró en storage al completar |
| `files.not_active` | 422 | `read-url` de un archivo que no está `Active` |
| `files.concurrency_conflict` | 409 | `complete` con `concurrencyToken` stale |

## Guía de implementación del cliente

1. **Subida en 3 pasos**: `upload-session` (elegí el `purpose` correcto) → `PUT` a `uploadUrl` con
   los `requiredHeaders` → `complete` con el `concurrencyToken`. Mostrá progreso durante el `PUT`
   (es la transferencia real).
2. **Validá del lado cliente** tipo/extensión/tamaño antes del `upload-session` para evitar los
   `422`/`413` (las reglas dependen del `purpose`).
3. **Descarga propia vs de terceros**: para archivos que subió el propio usuario, `read-url` genérico;
   para archivos de otros (ej. documentos de empleados que ve RRHH), usá el `read-url` del dominio.
4. **URLs efímeras**: tanto `uploadUrl` como `readUrl` expiran (`expiresUtc`) — pedilas justo antes de
   usarlas, no las caches.
5. **Limpieza**: un `upload-session` sin `complete` queda `PendingUpload` y lo recoge el cleanup; no
   hace falta que el FE lo borre, pero `DELETE` está disponible.

## Estado de la documentación

Files es un módulo de soporte transversal. Ver el [índice maestro](../README.md) para todas las áreas.
