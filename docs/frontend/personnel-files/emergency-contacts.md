# Personnel Files — Emergency Contacts

Contactos de emergencia asociados a un archivo de personal. Cada archivo puede tener varios contactos.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/emergency-contacts` | Listar los contactos de emergencia del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/emergency-contacts` | Agregar un contacto de emergencia |
| `GET`    | `/api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}` | Obtener un contacto por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}` | Reemplazar un contacto |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}` | Quitar un contacto |

**Path params:** `publicId` (uuid) = archivo de personal · `emergencyContactPublicId` (uuid) = ítem de contacto.

---

## `GET` Listar

`GET /api/v1/personnel-files/{publicId}/emergency-contacts`

Devuelve el array completo (no paginado) de contactos de emergencia del archivo. Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `EmergencyContactResponse` (ver tabla de campos en el `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/emergency-contacts" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar

`POST /api/v1/personnel-files/{publicId}/emergency-contacts`

No lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)). Responde `201` + headers `Location` y `ETag`.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `name` | string | no | Nombre completo del contacto. |
| `relationship` | string | no | Parentesco / relación (texto libre). |
| `phone` | string | no | Teléfono. |
| `address` | string | no | Dirección. |
| `workplace` | string | no | Lugar de trabajo. |

**Respuesta `201`** — `EmergencyContactResponse` (ver tabla en el `GET` por id), con header `ETag` = `concurrencyToken` inicial.

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/emergency-contacts" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "María Méndez",
    "relationship": "Madre",
    "phone": "+503 7000-1111",
    "address": "Col. Escalón, San Salvador",
    "workplace": "Hospital Central"
  }'
```

```jsonc
// 201 Created   Location: .../emergency-contacts/9c4f...22   ETag: "a1b2...c3"
{
  "emergencyContactPublicId": "9c4f...22",
  "name": "María Méndez",
  "relationship": "Madre",
  "phone": "+503 7000-1111",
  "address": "Col. Escalón, San Salvador",
  "workplace": "Hospital Central",
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`

**Respuesta `200`** — `EmergencyContactResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `emergencyContactPublicId` | uuid | Id del ítem. |
| `name` | string (nullable) | |
| `relationship` | string (nullable) | |
| `phone` | string (nullable) | |
| `address` | string (nullable) | |
| `workplace` | string (nullable) | |
| `concurrencyToken` | uuid | Token para `If-Match` en `PUT`/`PATCH`/`DELETE`. |

```bash
curl "$BASE/api/v1/personnel-files/$ID/emergency-contacts/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar

`PUT /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza todos los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `EmergencyContactResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/emergency-contacts/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "name": "María Méndez Soto",
    "relationship": "Madre",
    "phone": "+503 7000-2222",
    "address": "Santa Tecla, La Libertad",
    "workplace": "Hospital Central"
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/name`, `/relationship`, `/phone`, `/address`, `/workplace`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/emergency-contacts/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/phone", "value": "+503 7000-3333" }
  ]'
```

**Respuesta `200`** — `EmergencyContactResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar

`DELETE /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo padre tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f4e5...d6" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/emergency-contacts/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400`, `404`, `409` (token desactualizado).
