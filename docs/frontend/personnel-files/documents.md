# Documents — Documentos del archivo de personal

Sub‑recurso que registra los **documentos** (contratos, títulos, certificados, etc.) adjuntos a un archivo de personal. Cada documento es **metadata + una referencia a un archivo previamente subido** (`filePublicId`): el binario no se sube acá (no es multipart), se sube primero por la **Files API** y este recurso solo lo enlaza y lo tipifica. Cuelga de un archivo de personal ya existente.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este sub‑recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/documents` | Listar los documentos del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/documents` | Adjuntar un documento (enlazar un archivo subido) |
| `GET`    | `/api/v1/personnel-files/{publicId}/documents/{documentPublicId}` | Obtener un documento por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/documents/{documentPublicId}` | Reemplazar metadata (y opcionalmente el archivo) |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/documents/{documentPublicId}` | Cambios parciales de metadata |
| `DELETE` | `/api/v1/personnel-files/{publicId}/documents/{documentPublicId}` | Quitar un documento (borrado lógico) |

**Path params:** `publicId` (uuid) = archivo de personal · `documentPublicId` (uuid) = ítem de documento. Los ids van como GUIDs `publicId` (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

### Flujo de subida del archivo (referencia)

El documento **no recibe bytes**: enlaza un `filePublicId` ya existente. El binario se sube por la Files API **antes** de adjuntar el documento:

1. `POST /api/v1/files/upload-session` con `purpose=PersonnelDocument` → devuelve el `filePublicId` y la URL/SAS de subida.
2. Subí el archivo a esa URL.
3. `PATCH /api/v1/files/{filePublicId}/complete` → deja el archivo en estado `Active`.
4. Adjuntá el documento acá pasando ese `filePublicId` (paso `POST`, más abajo).

Para **descargar** el binario de un documento existente, usá su `filePublicId`: `GET /api/v1/files/{filePublicId}/read-url`.

---

## `GET` Listar documentos

`GET /api/v1/personnel-files/{publicId}/documents`

Devuelve el **array completo** (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)) de los documentos del archivo. Cada ítem trae su propio `concurrencyToken` y su `filePublicId`.

**Respuesta `200`** — array de `PersonnelFileDocumentMetadataResponse` (campos en la tabla del `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/documents" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Adjuntar documento

`POST /api/v1/personnel-files/{publicId}/documents`

Enlaza un archivo previamente subido (ver flujo arriba) como documento tipificado. No lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)). Responde `201` + headers `Location` y `ETag`.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `filePublicId` | uuid | sí | Id del archivo ya subido (`Active`, `purpose=PersonnelDocument`). |
| `documentTypeCatalogItemPublicId` | uuid | sí | Id del ítem de catálogo del tipo de documento. |
| `observations` | string (nullable) | no | Notas libres sobre el documento. |

**Respuesta `201`** — `PersonnelFileDocumentMetadataResponse` (ver tabla en el `GET` por id), con header `ETag` = `concurrencyToken` inicial. El nombre de archivo, `contentType` y `sizeBytes` se resuelven del archivo enlazado.

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/documents" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "filePublicId": "c4f7...8a",
    "documentTypeCatalogItemPublicId": "2b9d...41",
    "observations": "Contrato firmado, versión final"
  }'
```

```jsonc
// 201 Created   Location: .../documents/9e1a...07   ETag: "a1b2...c3"
{
  "publicId": "9e1a...07",
  "documentTypeCatalogItemPublicId": "2b9d...41",
  "documentTypeCode": "CONTRATO",
  "documentTypeName": "Contrato laboral",
  "documentType": "Contrato laboral",
  "observations": "Contrato firmado, versión final",
  "filePublicId": "c4f7...8a",
  "fileName": "contrato-lucia-mendez.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 248311,
  "isActive": true,
  "concurrencyToken": "a1b2...c3",
  "createdAtUtc": "2026-05-31T14:10:00Z"
}
```

**Errores:** `400` (validación), `404` (archivo o `filePublicId` inexistente), `409` (conflicto), `422` (regla de negocio, p. ej. archivo en estado inválido o `purpose` incorrecto).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/documents/{documentPublicId}`

**Respuesta `200`** — `PersonnelFileDocumentMetadataResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `publicId` | uuid | Id del ítem de documento. |
| `documentTypeCatalogItemPublicId` | uuid (nullable) | Id del ítem de catálogo del tipo. |
| `documentTypeCode` | string (nullable) | Código de catálogo del tipo. |
| `documentTypeName` | string (nullable) | Nombre resuelto del catálogo. |
| `documentType` | string (nullable) | Tipo de documento (etiqueta). |
| `observations` | string (nullable) | Notas. |
| `filePublicId` | uuid | Id del archivo enlazado (usalo en `GET /api/v1/files/{filePublicId}/read-url`). |
| `fileName` | string (nullable) | Nombre del archivo. |
| `contentType` | string (nullable) | MIME type del archivo. |
| `sizeBytes` | int | Tamaño del archivo en bytes. |
| `isActive` | boolean | `false` tras un borrado lógico. |
| `concurrencyToken` | uuid | Token para `If-Match` en `PUT`/`PATCH`/`DELETE`. |
| `createdAtUtc` | string (date-time) | |
| `modifiedAtUtc` | string (date-time, nullable) | |

```bash
curl "$BASE/api/v1/personnel-files/$ID/documents/$DOC_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar (metadata y opcionalmente el archivo)

`PUT /api/v1/personnel-files/{publicId}/documents/{documentPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza la metadata (tipo + observaciones) y, **opcionalmente**, el archivo subyacente si mandás `filePublicId` (el nuevo archivo debe ser `Active` y con `purpose=PersonnelDocument`). Si omitís `filePublicId`, el archivo enlazado no cambia.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `documentTypeCatalogItemPublicId` | uuid | sí | Id del ítem de catálogo del tipo. |
| `observations` | string (nullable) | no | Notas. |
| `filePublicId` | uuid (nullable) | no | Si viene, reemplaza el archivo enlazado; si se omite, se conserva. |

**Respuesta `200`** — `PersonnelFileDocumentMetadataResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/documents/$DOC_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "documentTypeCatalogItemPublicId": "2b9d...41",
    "observations": "Reemplazo: contrato con addendum",
    "filePublicId": "d5a8...9b"
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422` (archivo en estado/`purpose` inválido).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/documents/{documentPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = **solo metadata**: `/documentTypeCatalogItemPublicId` y `/observations`. El **archivo no se cambia por `PATCH`**; para reemplazarlo usá `PUT` con `filePublicId`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/documents/$DOC_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/observations", "value": "Vigente desde 2026" }
  ]'
```

**Respuesta `200`** — `PersonnelFileDocumentMetadataResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar (borrado lógico)

`DELETE /api/v1/personnel-files/{publicId}/documents/{documentPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Es un **borrado lógico**: el documento se desactiva (queda con `isActive: false` para retención) y su archivo de respaldo se marca para que el job de limpieza elimine el blob.

**Respuesta `200`** — devuelve el token del archivo padre tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)):

```jsonc
{ "parentConcurrencyToken": "f4e5...d6" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/documents/$DOC_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400`, `404`, `409` (token desactualizado).
