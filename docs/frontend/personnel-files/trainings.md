# Trainings — Capacitaciones del archivo de personal

Sub‑recurso que registra las **capacitaciones / cursos** de la persona: nombre, tipo, institución, duración, calificación y costo. Cuelga de un archivo de personal ya existente.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este sub‑recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/trainings` | Listar todas las capacitaciones del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/trainings` | Agregar una capacitación |
| `GET`    | `/api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` | Obtener una capacitación por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` | Reemplazar una capacitación |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` | Quitar una capacitación |

Los ids van como GUIDs `publicId` (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)). El id del archivo padre es `{publicId}`; el de cada ítem es `{trainingPublicId}`.

---

## `GET` Listar capacitaciones

`GET /api/v1/personnel-files/{publicId}/trainings`

Devuelve el **array completo** (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)) de las capacitaciones del archivo.

**Respuesta `200`** — array de `PersonnelFileTrainingResponse` (campos en la tabla del `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/trainings" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404` (el archivo no existe en esta compañía).

---

## `POST` Agregar una capacitación

`POST /api/v1/personnel-files/{publicId}/trainings`

Crea un ítem nuevo. **No** lleva `If-Match`. Responde `201` con el ítem creado, el header `Location` y el header `ETag` con su `concurrencyToken` inicial (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `trainingName` | string | no | Nombre de la capacitación. |
| `trainingTypeCode` | string | no | Código de catálogo del tipo (p. ej. `CURSO`, `DIPLOMADO`). |
| `description` | string | no | Descripción. |
| `topic` | string | no | Tema. |
| `institution` | string | no | Institución que la impartió. |
| `instructors` | string | no | Instructores. |
| `score` | number (double) | no | Calificación obtenida. |
| `startDate` | string (date-time) | sí | Fecha de inicio. |
| `endDate` | string (date-time) | no | Fecha de fin. |
| `isInternal` | boolean | sí | ¿Capacitación interna (de la empresa)? |
| `isLocal` | boolean | sí | ¿Local (vs. en el exterior)? |
| `countryCode` | string | no | Código de país. |
| `durationValue` | number (double) | sí | Valor de duración (según la unidad). |
| `durationUnitCode` | string | no | Código de unidad de duración (p. ej. `HORAS`, `DIAS`). |
| `costAmount` | number (double) | no | Costo. |
| `costCurrencyCode` | string | no | Código de moneda del costo (p. ej. `USD`). |

> Las fechas viajan como `date-time` ISO‑8601 (p. ej. `2024-02-10T00:00:00Z`).

**Respuesta `201`** — `PersonnelFileTrainingResponse` (mismos campos que el `GET` por id, abajo).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/trainings" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "trainingName": "Excel Avanzado",
    "trainingTypeCode": "CURSO",
    "topic": "Análisis de datos",
    "institution": "Centro de Formación ACME",
    "instructors": "M. Rivas",
    "score": 95,
    "startDate": "2024-02-10T00:00:00Z",
    "endDate": "2024-03-10T00:00:00Z",
    "isInternal": false,
    "isLocal": true,
    "countryCode": "SV",
    "durationValue": 40,
    "durationUnitCode": "HORAS",
    "costAmount": 120.00,
    "costCurrencyCode": "USD"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/3d9e...05/trainings/e91a...77   ETag: "a1b2...c3"
{
  "trainingPublicId": "e91a...77",
  "trainingName": "Excel Avanzado",
  "trainingTypeCode": "CURSO",
  "description": null,
  "topic": "Análisis de datos",
  "institution": "Centro de Formación ACME",
  "instructors": "M. Rivas",
  "score": 95,
  "startDate": "2024-02-10T00:00:00Z",
  "endDate": "2024-03-10T00:00:00Z",
  "isInternal": false,
  "isLocal": true,
  "countryCode": "SV",
  "durationValue": 40,
  "durationUnitCode": "HORAS",
  "costAmount": 120.00,
  "costCurrencyCode": "USD",
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` → `200` `PersonnelFileTrainingResponse`. El `concurrencyToken` que devuelve (también en `ETag`) es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

**Respuesta `200`:**

| Campo | Tipo |
|-------|------|
| `trainingPublicId` | uuid |
| `trainingName` | string (nullable) |
| `trainingTypeCode` | string (nullable) |
| `description` | string (nullable) |
| `topic` | string (nullable) |
| `institution` | string (nullable) |
| `instructors` | string (nullable) |
| `score` | number (double, nullable) |
| `startDate` | string (date-time) |
| `endDate` | string (date-time, nullable) |
| `isInternal` | boolean |
| `isLocal` | boolean |
| `countryCode` | string (nullable) |
| `durationValue` | number (double) |
| `durationUnitCode` | string (nullable) |
| `costAmount` | number (double, nullable) |
| `costCurrencyCode` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl "$BASE/api/v1/personnel-files/$ID/trainings/e91a...77" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar una capacitación

`PUT /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `PersonnelFileTrainingResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/trainings/e91a...77" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "trainingName": "Excel Avanzado",
    "trainingTypeCode": "CURSO",
    "institution": "Centro de Formación ACME",
    "score": 98,
    "startDate": "2024-02-10T00:00:00Z",
    "endDate": "2024-03-10T00:00:00Z",
    "isInternal": false,
    "isLocal": true,
    "durationValue": 40,
    "durationUnitCode": "HORAS"
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/trainingName`, `/trainingTypeCode`, `/description`, `/topic`, `/institution`, `/instructors`, `/score`, `/startDate`, `/endDate`, `/isInternal`, `/isLocal`, `/countryCode`, `/durationValue`, `/durationUnitCode`, `/costAmount`, `/costCurrencyCode`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/trainings/e91a...77" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/score", "value": 98 },
    { "op": "replace", "path": "/durationValue", "value": 48 }
  ]'
```

**Respuesta `200`** — `PersonnelFileTrainingResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar una capacitación

`DELETE /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo **padre** tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f2e1...90" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/trainings/e91a...77" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400` (`If-Match` faltante/malformado), `404`, `409` (token desactualizado).
