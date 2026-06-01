# Personnel Files — Addresses

Direcciones asociadas a un archivo de personal. Cada archivo puede tener varias direcciones, una de ellas marcada como la actual (`isCurrent`).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/addresses` | Listar las direcciones del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/addresses` | Agregar una dirección |
| `GET`    | `/api/v1/personnel-files/{publicId}/addresses/{addressPublicId}` | Obtener una dirección por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/addresses/{addressPublicId}` | Reemplazar una dirección |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/addresses/{addressPublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/addresses/{addressPublicId}` | Quitar una dirección |

**Path params:** `publicId` (uuid) = archivo de personal · `addressPublicId` (uuid) = ítem de dirección.

---

## `GET` Listar

`GET /api/v1/personnel-files/{publicId}/addresses`

Devuelve el array completo (no paginado) de direcciones del archivo. Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `AddressResponse` (ver tabla de campos en el `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/addresses" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar

`POST /api/v1/personnel-files/{publicId}/addresses`

No lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)). Responde `201` + headers `Location` y `ETag`.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `addressLine` | string | no | Calle, número, referencias. |
| `country` | string | no | Código de catálogo de país. |
| `department` | string | no | Código de catálogo de departamento. |
| `municipality` | string | no | Código de catálogo de municipio. |
| `postalCode` | string | no | Código postal. |
| `isCurrent` | boolean | no | Marca esta dirección como la actual. |

**Respuesta `201`** — `AddressResponse` (ver tabla en el `GET` por id), con header `ETag` = `concurrencyToken` inicial.

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/addresses" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "addressLine": "Calle El Mirador 123, Col. Escalón",
    "country": "SV",
    "department": "SAN_SALVADOR",
    "municipality": "SAN_SALVADOR",
    "postalCode": "1101",
    "isCurrent": true
  }'
```

```jsonc
// 201 Created   Location: .../addresses/5a8d...44   ETag: "a1b2...c3"
{
  "addressPublicId": "5a8d...44",
  "addressLine": "Calle El Mirador 123, Col. Escalón",
  "country": "SV",
  "department": "SAN_SALVADOR",
  "municipality": "SAN_SALVADOR",
  "postalCode": "1101",
  "isCurrent": true,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`

**Respuesta `200`** — `AddressResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `addressPublicId` | uuid | Id del ítem. |
| `addressLine` | string (nullable) | |
| `country` | string (nullable) | Código de catálogo de país. |
| `department` | string (nullable) | Código de catálogo de departamento. |
| `municipality` | string (nullable) | Código de catálogo de municipio. |
| `postalCode` | string (nullable) | |
| `isCurrent` | boolean | |
| `concurrencyToken` | uuid | Token para `If-Match` en `PUT`/`PATCH`/`DELETE`. |

```bash
curl "$BASE/api/v1/personnel-files/$ID/addresses/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar

`PUT /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza todos los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `AddressResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/addresses/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "addressLine": "Av. Las Magnolias 456",
    "country": "SV",
    "department": "LA_LIBERTAD",
    "municipality": "SANTA_TECLA",
    "postalCode": "1501",
    "isCurrent": true
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/addressLine`, `/country`, `/department`, `/municipality`, `/postalCode`, `/isCurrent`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/addresses/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/addressLine", "value": "Calle Nueva 789" },
    { "op": "replace", "path": "/isCurrent", "value": false }
  ]'
```

**Respuesta `200`** — `AddressResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar

`DELETE /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo padre tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f4e5...d6" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/addresses/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400`, `404`, `409` (token desactualizado).
