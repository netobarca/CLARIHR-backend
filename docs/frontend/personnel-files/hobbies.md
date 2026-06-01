# Personnel Files — Hobbies (pasatiempos)

Los **hobbies** son los pasatiempos/intereses personales asociados a un archivo de personal. Es un sub‑recurso simple (un solo campo de texto) que cuelga de un archivo ya existente. Funciona sobre **cualquier** archivo, esté en `Draft` o `Completed` (no está sujeto a la finalización del empleado).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/hobbies` | Listar los hobbies del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/hobbies` | Agregar un hobby |
| `GET`    | `/api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` | Obtener un hobby por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` | Reemplazar un hobby |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` | Cambios parciales sobre un hobby |
| `DELETE` | `/api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` | Eliminar un hobby |

`{publicId}` = id del archivo de personal. `{hobbyPublicId}` = id del hobby (GUID; ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

---

## `GET` Listar hobbies

`GET /api/v1/personnel-files/{publicId}/hobbies`

Devuelve el **array completo** de hobbies del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)).

**Respuesta `200`** — array de `HobbyResponse`:

| Campo | Tipo |
|-------|------|
| `hobbyPublicId` | uuid |
| `hobbyName` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl "$BASE/api/v1/personnel-files/$ID/hobbies" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "hobbyPublicId": "7c1a9e44-2f6b-4d83-9b10-0e2f5a8c1d22",
    "hobbyName": "Fotografía",
    "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
  }
]
```

**Errores:** `401`, `403`, `404` (archivo inexistente o de otra compañía).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` → `200` `HobbyResponse` (mismos campos que arriba). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` de este ítem.

```bash
curl "$BASE/api/v1/personnel-files/$ID/hobbies/$HOBBY" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un hobby

`POST /api/v1/personnel-files/{publicId}/hobbies` · **sin `If-Match`** (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `hobbyName` | string | no | Nombre del pasatiempo. |

**Respuesta `201`** — `HobbyResponse` (+ headers `Location` y `ETag` con el `concurrencyToken` inicial).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/hobbies" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "hobbyName": "Fotografía" }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{publicId}/hobbies/7c1a...d22   ETag: "a1b2...f809"
{
  "hobbyPublicId": "7c1a9e44-2f6b-4d83-9b10-0e2f5a8c1d22",
  "hobbyName": "Fotografía",
  "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
}
```

**Errores:** `400` (validación), `401`, `403`, `404`, `409`, `422`.

---

## `PUT` Reemplazar un hobby

`PUT /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `HobbyResponse` con el `concurrencyToken` **nuevo** (también en el header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/hobbies/$HOBBY" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '{ "hobbyName": "Senderismo" }'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (RFC 6902), **sin** envoltorio `operations` (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campo parchable: `hobbyName`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/hobbies/$HOBBY" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '[
    { "op": "replace", "path": "/hobbyName", "value": "Ciclismo" }
  ]'
```

**Respuesta `200`** — `HobbyResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `401`, `403`, `404`, `409`, `422`.

---

## `DELETE` Eliminar un hobby

`DELETE /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del **archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)):

```jsonc
// 200 OK
{ "parentConcurrencyToken": "f0e1d2c3-b4a5-6978-8a9b-0c1d2e3f4a5b" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/hobbies/$HOBBY" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...f809"'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado).
