# References — Referencias del archivo de personal

Sub‑recurso que registra las **referencias** (personales o laborales) de la persona: a quién contactar, su ocupación, lugar de trabajo y desde hace cuánto la conoce. Cuelga de un archivo de personal ya existente.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este sub‑recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/references` | Listar todas las referencias del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/references` | Agregar una referencia |
| `GET`    | `/api/v1/personnel-files/{publicId}/references/{referencePublicId}` | Obtener una referencia por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/references/{referencePublicId}` | Reemplazar una referencia |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/references/{referencePublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/references/{referencePublicId}` | Quitar una referencia |

Los ids van como GUIDs `publicId` (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)). El id del archivo padre es `{publicId}`; el de cada ítem es `{referencePublicId}`.

---

## `GET` Listar referencias

`GET /api/v1/personnel-files/{publicId}/references`

Devuelve el **array completo** (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)) de las referencias del archivo.

**Respuesta `200`** — array de `PersonnelFileReferenceResponse` (campos en la tabla del `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/references" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404` (el archivo no existe en esta compañía).

---

## `POST` Agregar una referencia

`POST /api/v1/personnel-files/{publicId}/references`

Crea un ítem nuevo. **No** lleva `If-Match`. Responde `201` con el ítem creado, el header `Location` y el header `ETag` con su `concurrencyToken` inicial (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `personName` | string | no | Nombre de la persona de referencia. |
| `address` | string | no | Dirección. |
| `phone` | string | no | Teléfono personal. |
| `referenceTypeCode` | string | no | Código de catálogo del tipo de referencia (p. ej. `PERSONAL`, `LABORAL`). |
| `occupation` | string | no | Ocupación / cargo. |
| `workplace` | string | no | Lugar de trabajo. |
| `workPhone` | string | no | Teléfono laboral. |
| `knownTimeYears` | number (double) | sí | Tiempo de conocerla, en años. |

**Respuesta `201`** — `PersonnelFileReferenceResponse` (mismos campos que el `GET` por id, abajo).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/references" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "personName": "Roberto Alvarado",
    "phone": "+503 7123-4567",
    "referenceTypeCode": "LABORAL",
    "occupation": "Gerente de Operaciones",
    "workplace": "Distribuidora del Sur",
    "workPhone": "+503 2222-1010",
    "knownTimeYears": 5.5
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/3d9e...05/references/b18c...44   ETag: "a1b2...c3"
{
  "referencePublicId": "b18c...44",
  "personName": "Roberto Alvarado",
  "address": null,
  "phone": "+503 7123-4567",
  "referenceTypeCode": "LABORAL",
  "occupation": "Gerente de Operaciones",
  "workplace": "Distribuidora del Sur",
  "workPhone": "+503 2222-1010",
  "knownTimeYears": 5.5,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/references/{referencePublicId}` → `200` `PersonnelFileReferenceResponse`. El `concurrencyToken` que devuelve (también en `ETag`) es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

**Respuesta `200`:**

| Campo | Tipo |
|-------|------|
| `referencePublicId` | uuid |
| `personName` | string (nullable) |
| `address` | string (nullable) |
| `phone` | string (nullable) |
| `referenceTypeCode` | string (nullable) |
| `occupation` | string (nullable) |
| `workplace` | string (nullable) |
| `workPhone` | string (nullable) |
| `knownTimeYears` | number (double) |
| `concurrencyToken` | uuid |

```bash
curl "$BASE/api/v1/personnel-files/$ID/references/b18c...44" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar una referencia

`PUT /api/v1/personnel-files/{publicId}/references/{referencePublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `PersonnelFileReferenceResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/references/b18c...44" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "personName": "Roberto Alvarado",
    "phone": "+503 7123-4567",
    "referenceTypeCode": "LABORAL",
    "occupation": "Director de Operaciones",
    "workplace": "Distribuidora del Sur",
    "knownTimeYears": 6
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/references/{referencePublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/personName`, `/address`, `/phone`, `/referenceTypeCode`, `/occupation`, `/workplace`, `/workPhone`, `/knownTimeYears`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/references/b18c...44" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/occupation", "value": "Director de Operaciones" },
    { "op": "replace", "path": "/knownTimeYears", "value": 6 }
  ]'
```

**Respuesta `200`** — `PersonnelFileReferenceResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar una referencia

`DELETE /api/v1/personnel-files/{publicId}/references/{referencePublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo **padre** tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f2e1...90" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/references/b18c...44" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400` (`If-Match` faltante/malformado), `404`, `409` (token desactualizado).
