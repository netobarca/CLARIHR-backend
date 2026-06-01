# Educations — Formación académica del archivo de personal

Sub‑recurso que registra la **formación académica** de la persona: título, carrera, institución, fechas y avance de materias. Cuelga de un archivo de personal ya existente.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este sub‑recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

> **Importante (asimetría request/response):** al **escribir** (`POST`/`PUT`/`PATCH`) se mandan los catálogos como **GUIDs** (`statusPublicId`, `studyTypePublicId`, `careerPublicId`, `shiftPublicId`, `modalityPublicId`). Al **leer** (`GET`), esos mismos catálogos vuelven **resueltos** como objetos anidados (`status`, `studyType`, `career`, `shift`, `modality`) con la forma `{ id, code, name, isActive }`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/educations` | Listar toda la formación del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/educations` | Agregar una formación |
| `GET`    | `/api/v1/personnel-files/{publicId}/educations/{educationPublicId}` | Obtener una formación por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/educations/{educationPublicId}` | Reemplazar una formación |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/educations/{educationPublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/educations/{educationPublicId}` | Quitar una formación |

Los ids van como GUIDs `publicId` (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)). El id del archivo padre es `{publicId}`; el de cada ítem es `{educationPublicId}`.

---

## `GET` Listar formación

`GET /api/v1/personnel-files/{publicId}/educations`

Devuelve el **array completo** (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)) de la formación académica del archivo.

**Respuesta `200`** — array de `PersonnelFileEducationResponse` (campos en la tabla del `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/educations" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404` (el archivo no existe en esta compañía).

---

## `POST` Agregar una formación

`POST /api/v1/personnel-files/{publicId}/educations`

Crea un ítem nuevo. **No** lleva `If-Match`. Responde `201` con el ítem creado, el header `Location` y el header `ETag` con su `concurrencyToken` inicial (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `statusPublicId` | uuid | sí | Catálogo: estado de la formación (p. ej. en curso / finalizada). |
| `degreeTitle` | string | no | Título obtenido. |
| `studyTypePublicId` | uuid | sí | Catálogo: tipo de estudio (p. ej. universitario, técnico). |
| `careerPublicId` | uuid | sí | Catálogo: carrera. |
| `institution` | string | no | Institución educativa. |
| `countryCode` | string | no | Código de país. |
| `specialty` | string | no | Especialidad. |
| `isCurrentlyStudying` | boolean | sí | ¿Está estudiando actualmente? |
| `startDate` | string (date-time) | sí | Fecha de inicio. |
| `endDate` | string (date-time) | no | Fecha de fin. |
| `shiftPublicId` | uuid | no | Catálogo: jornada/turno. |
| `modalityPublicId` | uuid | no | Catálogo: modalidad (presencial, virtual…). |
| `totalSubjects` | int | no | Total de materias del plan. |
| `approvedSubjects` | int | no | Materias aprobadas. |

> Las fechas viajan como `date-time` ISO‑8601 (p. ej. `2015-02-01T00:00:00Z`). Los `*PublicId` de catálogo se obtienen de los endpoints de catálogos de educación correspondientes.

**Respuesta `201`** — `PersonnelFileEducationResponse` (mismos campos que el `GET` por id, abajo; los catálogos vienen resueltos como objetos).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/educations" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "statusPublicId": "11111111-1111-1111-1111-111111111101",
    "degreeTitle": "Licenciatura en Sistemas",
    "studyTypePublicId": "22222222-2222-2222-2222-222222222202",
    "careerPublicId": "33333333-3333-3333-3333-333333333303",
    "institution": "Universidad Nacional",
    "countryCode": "SV",
    "specialty": "Ingeniería de Software",
    "isCurrentlyStudying": false,
    "startDate": "2015-02-01T00:00:00Z",
    "endDate": "2019-11-30T00:00:00Z",
    "shiftPublicId": "44444444-4444-4444-4444-444444444404",
    "modalityPublicId": "55555555-5555-5555-5555-555555555505",
    "totalSubjects": 45,
    "approvedSubjects": 45
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/3d9e...05/educations/a72f...31   ETag: "a1b2...c3"
{
  "educationPublicId": "a72f...31",
  "status":    { "id": "11111111-1111-1111-1111-111111111101", "code": "FINALIZADA", "name": "Finalizada", "isActive": true },
  "degreeTitle": "Licenciatura en Sistemas",
  "studyType": { "id": "22222222-2222-2222-2222-222222222202", "code": "UNIVERSITARIO", "name": "Universitario", "isActive": true },
  "career":    { "id": "33333333-3333-3333-3333-333333333303", "code": "SISTEMAS", "name": "Ingeniería en Sistemas", "isActive": true },
  "institution": "Universidad Nacional",
  "countryCode": "SV",
  "specialty": "Ingeniería de Software",
  "isCurrentlyStudying": false,
  "startDate": "2015-02-01T00:00:00Z",
  "endDate": "2019-11-30T00:00:00Z",
  "shift":     { "id": "44444444-4444-4444-4444-444444444404", "code": "NOCTURNO", "name": "Nocturno", "isActive": true },
  "modality":  { "id": "55555555-5555-5555-5555-555555555505", "code": "PRESENCIAL", "name": "Presencial", "isActive": true },
  "totalSubjects": 45,
  "approvedSubjects": 45,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/educations/{educationPublicId}` → `200` `PersonnelFileEducationResponse`. El `concurrencyToken` que devuelve (también en `ETag`) es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

**Respuesta `200`:**

| Campo | Tipo |
|-------|------|
| `educationPublicId` | uuid |
| `status` | objeto catálogo `{ id, code, name, isActive }` |
| `degreeTitle` | string (nullable) |
| `studyType` | objeto catálogo `{ id, code, name, isActive }` |
| `career` | objeto catálogo `{ id, code, name, isActive }` |
| `institution` | string (nullable) |
| `countryCode` | string (nullable) |
| `specialty` | string (nullable) |
| `isCurrentlyStudying` | boolean |
| `startDate` | string (date-time) |
| `endDate` | string (date-time, nullable) |
| `shift` | objeto catálogo `{ id, code, name, isActive }` (nullable) |
| `modality` | objeto catálogo `{ id, code, name, isActive }` (nullable) |
| `totalSubjects` | int (nullable) |
| `approvedSubjects` | int (nullable) |
| `concurrencyToken` | uuid |

Forma del objeto catálogo (`status` / `studyType` / `career` / `shift` / `modality`):

| Campo | Tipo |
|-------|------|
| `id` | uuid |
| `code` | string |
| `name` | string |
| `isActive` | boolean |

```bash
curl "$BASE/api/v1/personnel-files/$ID/educations/a72f...31" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar una formación

`PUT /api/v1/personnel-files/{publicId}/educations/{educationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST` (catálogos como `*PublicId`).

**Respuesta `200`** — `PersonnelFileEducationResponse` con el `concurrencyToken` nuevo (también en `ETag`; catálogos resueltos como objetos).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/educations/a72f...31" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "statusPublicId": "11111111-1111-1111-1111-111111111101",
    "degreeTitle": "Licenciatura en Ingeniería de Sistemas",
    "studyTypePublicId": "22222222-2222-2222-2222-222222222202",
    "careerPublicId": "33333333-3333-3333-3333-333333333303",
    "institution": "Universidad Nacional",
    "isCurrentlyStudying": false,
    "startDate": "2015-02-01T00:00:00Z",
    "endDate": "2019-11-30T00:00:00Z",
    "totalSubjects": 45,
    "approvedSubjects": 45
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/educations/{educationPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (los catálogos se parchan por su **`*PublicId`**, no por el objeto resuelto): `/statusPublicId`, `/degreeTitle`, `/studyTypePublicId`, `/careerPublicId`, `/institution`, `/countryCode`, `/specialty`, `/isCurrentlyStudying`, `/startDate`, `/endDate`, `/shiftPublicId`, `/modalityPublicId`, `/totalSubjects`, `/approvedSubjects`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/educations/a72f...31" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/approvedSubjects", "value": 46 },
    { "op": "replace", "path": "/modalityPublicId", "value": "55555555-5555-5555-5555-555555555506" }
  ]'
```

**Respuesta `200`** — `PersonnelFileEducationResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar una formación

`DELETE /api/v1/personnel-files/{publicId}/educations/{educationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo **padre** tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f2e1...90" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/educations/a72f...31" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400` (`If-Match` faltante/malformado), `404`, `409` (token desactualizado).
