# Personnel Files — Identifications

Documentos de identidad (DUI, pasaporte, NIT, etc.) asociados a un archivo de personal. Cada archivo puede tener varias identificaciones, una de ellas marcada como primaria.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/identifications` | Listar las identificaciones del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/identifications` | Agregar una identificación |
| `GET`    | `/api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}` | Obtener una identificación por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}` | Reemplazar una identificación |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}` | Quitar una identificación |

**Path params:** `publicId` (uuid) = archivo de personal · `identificationPublicId` (uuid) = ítem de identificación.

---

## `GET` Listar

`GET /api/v1/personnel-files/{publicId}/identifications`

Devuelve el array completo (no paginado) de identificaciones del archivo. Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `IdentificationResponse` (ver tabla de campos en el `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/identifications" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar

`POST /api/v1/personnel-files/{publicId}/identifications`

No lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)). Responde `201` + headers `Location` y `ETag`.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `identificationTypeCode` | string | no | Código de catálogo de tipo de documento (p. ej. `DUI`, `PASAPORTE`). |
| `identificationNumber` | string | no | Número del documento. |
| `issuedDate` | string (date-time) | no | Fecha de emisión. |
| `expiryDate` | string (date-time) | no | Fecha de vencimiento. |
| `issuer` | string | no | Entidad emisora. |
| `isPrimary` | boolean | no | Marca esta identificación como la primaria. |

**Respuesta `201`** — `IdentificationResponse` (ver tabla en el `GET` por id), con header `ETag` = `concurrencyToken` inicial.

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/identifications" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "identificationTypeCode": "DUI",
    "identificationNumber": "01234567-8",
    "issuedDate": "2020-03-10T00:00:00Z",
    "expiryDate": "2030-03-10T00:00:00Z",
    "issuer": "RNPN",
    "isPrimary": true
  }'
```

```jsonc
// 201 Created   Location: .../identifications/7b2c...91   ETag: "a1b2...c3"
{
  "identificationPublicId": "7b2c...91",
  "identificationTypeCode": "DUI",
  "identificationTypeName": "DUI",
  "identificationNumber": "01234567-8",
  "issuedDate": "2020-03-10T00:00:00Z",
  "expiryDate": "2030-03-10T00:00:00Z",
  "issuer": "RNPN",
  "isPrimary": true,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409` (identificación duplicada), `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`

**Respuesta `200`** — `IdentificationResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `identificationPublicId` | uuid | Id del ítem. |
| `identificationTypeCode` | string (nullable) | Código de catálogo. |
| `identificationTypeName` | string (nullable) | Nombre resuelto del catálogo. |
| `identificationNumber` | string (nullable) | |
| `issuedDate` | string (date-time, nullable) | |
| `expiryDate` | string (date-time, nullable) | |
| `issuer` | string (nullable) | |
| `isPrimary` | boolean | |
| `concurrencyToken` | uuid | Token para `If-Match` en `PUT`/`PATCH`/`DELETE`. |

```bash
curl "$BASE/api/v1/personnel-files/$ID/identifications/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar

`PUT /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza todos los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `IdentificationResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/identifications/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "identificationTypeCode": "DUI",
    "identificationNumber": "01234567-8",
    "issuedDate": "2020-03-10T00:00:00Z",
    "expiryDate": "2031-03-10T00:00:00Z",
    "issuer": "RNPN",
    "isPrimary": true
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado / duplicado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/identificationTypeCode`, `/identificationNumber`, `/issuedDate`, `/expiryDate`, `/issuer`, `/isPrimary`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/identifications/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/isPrimary", "value": false },
    { "op": "replace", "path": "/expiryDate", "value": "2032-03-10T00:00:00Z" }
  ]'
```

**Respuesta `200`** — `IdentificationResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar

`DELETE /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo padre tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f4e5...d6" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/identifications/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400`, `404`, `409` (token desactualizado).
