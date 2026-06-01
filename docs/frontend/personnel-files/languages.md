# Languages — Idiomas del archivo de personal

Sub‑recurso que registra los **idiomas** que domina la persona y, por cada uno, su nivel y qué destrezas tiene (habla / escribe / lee). Cuelga de un archivo de personal ya existente.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este sub‑recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/languages` | Listar todos los idiomas del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/languages` | Agregar un idioma |
| `GET`    | `/api/v1/personnel-files/{publicId}/languages/{languagePublicId}` | Obtener un idioma por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/languages/{languagePublicId}` | Reemplazar un idioma |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/languages/{languagePublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/languages/{languagePublicId}` | Quitar un idioma |

Los ids van como GUIDs `publicId` (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)). El id del archivo padre es `{publicId}`; el de cada ítem es `{languagePublicId}`.

---

## `GET` Listar idiomas

`GET /api/v1/personnel-files/{publicId}/languages`

Devuelve el **array completo** (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)) de los idiomas del archivo.

**Respuesta `200`** — array de `PersonnelFileLanguageResponse` (campos en la tabla del `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/languages" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404` (el archivo no existe en esta compañía).

---

## `POST` Agregar un idioma

`POST /api/v1/personnel-files/{publicId}/languages`

Crea un ítem nuevo. **No** lleva `If-Match`. Responde `201` con el ítem creado, el header `Location` y el header `ETag` con su `concurrencyToken` inicial (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `languageCode` | string | no | Código de catálogo del idioma (p. ej. `EN`, `ES`). |
| `levelCode` | string | no | Código de catálogo del nivel (p. ej. `B2`, `NATIVE`). |
| `speaks` | boolean | sí | ¿Lo habla? |
| `writes` | boolean | sí | ¿Lo escribe? |
| `reads` | boolean | sí | ¿Lo lee? |

**Respuesta `201`** — `PersonnelFileLanguageResponse` (mismos campos que el `GET` por id, abajo).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/languages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "languageCode": "EN",
    "levelCode": "B2",
    "speaks": true,
    "writes": true,
    "reads": true
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/3d9e...05/languages/7c41...88   ETag: "a1b2...c3"
{
  "languagePublicId": "7c41...88",
  "languageCode": "EN",
  "levelCode": "B2",
  "speaks": true,
  "writes": true,
  "reads": true,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/languages/{languagePublicId}` → `200` `PersonnelFileLanguageResponse`. El `concurrencyToken` que devuelve (también en `ETag`) es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

**Respuesta `200`:**

| Campo | Tipo |
|-------|------|
| `languagePublicId` | uuid |
| `languageCode` | string (nullable) |
| `levelCode` | string (nullable) |
| `speaks` | boolean |
| `writes` | boolean |
| `reads` | boolean |
| `concurrencyToken` | uuid |

```bash
curl "$BASE/api/v1/personnel-files/$ID/languages/7c41...88" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un idioma

`PUT /api/v1/personnel-files/{publicId}/languages/{languagePublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `PersonnelFileLanguageResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/languages/7c41...88" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "languageCode": "EN",
    "levelCode": "C1",
    "speaks": true,
    "writes": true,
    "reads": true
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/languages/{languagePublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/languageCode`, `/levelCode`, `/speaks`, `/writes`, `/reads`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/languages/7c41...88" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/levelCode", "value": "C1" },
    { "op": "replace", "path": "/writes", "value": false }
  ]'
```

**Respuesta `200`** — `PersonnelFileLanguageResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar un idioma

`DELETE /api/v1/personnel-files/{publicId}/languages/{languagePublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo **padre** tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f2e1...90" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/languages/7c41...88" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400` (`If-Match` faltante/malformado), `404`, `409` (token desactualizado).
