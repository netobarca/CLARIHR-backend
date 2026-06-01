# Personnel Files — Associations (asociaciones)

Las **associations** son las membresías/afiliaciones de la persona a gremios, cámaras, sindicatos u otras organizaciones, con su rol, fechas y aporte. Cuelgan de un archivo ya existente y funcionan sobre **cualquier** archivo, esté en `Draft` o `Completed` (no están sujetas a la finalización del empleado).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/associations` | Listar las asociaciones del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/associations` | Agregar una asociación |
| `GET`    | `/api/v1/personnel-files/{publicId}/associations/{associationPublicId}` | Obtener una asociación por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/associations/{associationPublicId}` | Reemplazar una asociación |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/associations/{associationPublicId}` | Cambios parciales sobre una asociación |
| `DELETE` | `/api/v1/personnel-files/{publicId}/associations/{associationPublicId}` | Eliminar una asociación |

`{publicId}` = id del archivo de personal. `{associationPublicId}` = id de la asociación (GUID; ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

---

## Campos del recurso

Mismos campos para el body de `POST`/`PUT` y para la respuesta (la respuesta agrega el id y el token):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `associationName` | string | no | Nombre de la organización/asociación. |
| `role` | string | no | Rol o cargo dentro de la asociación. |
| `joinedDate` | string (date-time) | no | Fecha de ingreso. |
| `leftDate` | string (date-time) | no | Fecha de salida (si aplica). |
| `payment` | number (double) | no | Monto del aporte/cuota. |

**Campos adicionales en la respuesta** (`AssociationResponse`):

| Campo | Tipo |
|-------|------|
| `associationPublicId` | uuid |
| `concurrencyToken` | uuid |

---

## `GET` Listar asociaciones

`GET /api/v1/personnel-files/{publicId}/associations`

Devuelve el **array completo** de asociaciones del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)).

```bash
curl "$BASE/api/v1/personnel-files/$ID/associations" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "associationPublicId": "b3d9c1a8-7e44-4f02-9c61-1a2b3c4d5e6f",
    "associationName": "Colegio de Profesionales en Informática",
    "role": "Miembro activo",
    "joinedDate": "2019-03-01T00:00:00Z",
    "leftDate": null,
    "payment": 25.00,
    "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
  }
]
```

**Errores:** `401`, `403`, `404` (archivo inexistente o de otra compañía).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/associations/{associationPublicId}` → `200` `AssociationResponse`. El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` de este ítem.

```bash
curl "$BASE/api/v1/personnel-files/$ID/associations/$ASSOC" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una asociación

`POST /api/v1/personnel-files/{publicId}/associations` · **sin `If-Match`** (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`): ver la [tabla de campos](#campos-del-recurso).

**Respuesta `201`** — `AssociationResponse` (+ headers `Location` y `ETag` con el `concurrencyToken` inicial).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/associations" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "associationName": "Colegio de Profesionales en Informática",
    "role": "Miembro activo",
    "joinedDate": "2019-03-01T00:00:00Z",
    "payment": 25.00
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{publicId}/associations/b3d9...e6f   ETag: "a1b2...f809"
{
  "associationPublicId": "b3d9c1a8-7e44-4f02-9c61-1a2b3c4d5e6f",
  "associationName": "Colegio de Profesionales en Informática",
  "role": "Miembro activo",
  "joinedDate": "2019-03-01T00:00:00Z",
  "leftDate": null,
  "payment": 25.00,
  "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
}
```

**Errores:** `400` (validación), `401`, `403`, `404`, `409`, `422`.

---

## `PUT` Reemplazar una asociación

`PUT /api/v1/personnel-files/{publicId}/associations/{associationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `AssociationResponse` con el `concurrencyToken` **nuevo** (también en el header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/associations/$ASSOC" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '{
    "associationName": "Colegio de Profesionales en Informática",
    "role": "Vocal",
    "joinedDate": "2019-03-01T00:00:00Z",
    "leftDate": "2026-01-31T00:00:00Z",
    "payment": 30.00
  }'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/associations/{associationPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (RFC 6902), **sin** envoltorio `operations` (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: `associationName`, `role`, `joinedDate`, `leftDate`, `payment`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/associations/$ASSOC" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '[
    { "op": "replace", "path": "/role", "value": "Vocal" },
    { "op": "replace", "path": "/leftDate", "value": "2026-01-31T00:00:00Z" }
  ]'
```

**Respuesta `200`** — `AssociationResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `401`, `403`, `404`, `409`, `422`.

---

## `DELETE` Eliminar una asociación

`DELETE /api/v1/personnel-files/{publicId}/associations/{associationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del **archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)):

```jsonc
// 200 OK
{ "parentConcurrencyToken": "f0e1d2c3-b4a5-6978-8a9b-0c1d2e3f4a5b" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/associations/$ASSOC" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...f809"'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado).
