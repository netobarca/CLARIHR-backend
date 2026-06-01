# Personnel Files — Curricular Competencies (competencias curriculares)

Las **curricular competencies** registran los requisitos/competencias del perfil curricular del empleado: tipo de requisito, nombre, dominio, tiempo de experiencia (con su métrica) y notas, junto con la referencia al sistema origen. Cuelgan de un archivo ya existente.

> ⚠️ **Sub‑recurso de empleado (state‑gated):** solo se puede **crear/editar/eliminar** sobre un archivo **finalizado** (`Completed`). Sobre un archivo en `Draft`, las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) responden **422** (regla de estado). El `GET` funciona en cualquier estado. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment) y [finalize](./finalize.md).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/curricular-competencies` | Listar las competencias del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/curricular-competencies` | Agregar una competencia |
| `GET`    | `/api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` | Obtener una competencia por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` | Reemplazar una competencia |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` | Cambios parciales sobre una competencia |
| `DELETE` | `/api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` | Eliminar una competencia |

`{publicId}` = id del archivo de personal. `{curricularCompetencyPublicId}` = id de la competencia (GUID; ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

---

## Campos del recurso

Mismos campos para el body de `POST`/`PUT` y para la respuesta (la respuesta agrega el id y el token):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `requirementTypeCode` | string | no | Código del tipo de requisito (catálogo). |
| `requirementName` | string | no | Nombre del requisito/competencia. |
| `competencyDomain` | string | no | Dominio o área de la competencia. |
| `experienceTimeValue` | number (double) | no | Tiempo de experiencia (en la unidad indicada por `metricCode`). |
| `metricCode` | string | no | Código de la métrica/unidad de `experienceTimeValue` (catálogo). |
| `notes` | string | no | Notas/observaciones. |
| `sourceSystem` | string | no | Sistema de origen (si fue sincronizado externamente). |
| `sourceReference` | string | no | Referencia/id en el sistema de origen. |
| `sourceSyncedUtc` | string (date-time) | no | Fecha de la última sincronización (UTC). |

**Campos adicionales en la respuesta** (`CurricularCompetencyResponse`):

| Campo | Tipo |
|-------|------|
| `curricularCompetencyPublicId` | uuid |
| `concurrencyToken` | uuid |

---

## `GET` Listar competencias

`GET /api/v1/personnel-files/{publicId}/curricular-competencies`

Devuelve el **array completo** de competencias del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)).

```bash
curl "$BASE/api/v1/personnel-files/$ID/curricular-competencies" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "curricularCompetencyPublicId": "a5b6c7d8-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
    "requirementTypeCode": "EDUCACION",
    "requirementName": "Licenciatura en Administración",
    "competencyDomain": "Gestión",
    "experienceTimeValue": 3.0,
    "metricCode": "ANIOS",
    "notes": "Requisito cumplido.",
    "sourceSystem": null,
    "sourceReference": null,
    "sourceSyncedUtc": null,
    "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
  }
]
```

**Errores:** `401`, `403`, `404` (archivo inexistente o de otra compañía).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` → `200` `CurricularCompetencyResponse`. El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` de este ítem.

```bash
curl "$BASE/api/v1/personnel-files/$ID/curricular-competencies/$CC" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una competencia

`POST /api/v1/personnel-files/{publicId}/curricular-competencies` · **sin `If-Match`** (ver [Convenciones §6](./_conventions.md#6-crear-post)). **Requiere archivo `Completed`** (en `Draft` → `422`).

**Body** (`application/json`): ver la [tabla de campos](#campos-del-recurso).

**Respuesta `201`** — `CurricularCompetencyResponse` (+ headers `Location` y `ETag` con el `concurrencyToken` inicial).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/curricular-competencies" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "requirementTypeCode": "EDUCACION",
    "requirementName": "Licenciatura en Administración",
    "competencyDomain": "Gestión",
    "experienceTimeValue": 3.0,
    "metricCode": "ANIOS",
    "notes": "Requisito cumplido."
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{publicId}/curricular-competencies/a5b6...7c8d   ETag: "a1b2...f809"
{
  "curricularCompetencyPublicId": "a5b6c7d8-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
  "requirementTypeCode": "EDUCACION",
  "requirementName": "Licenciatura en Administración",
  "competencyDomain": "Gestión",
  "experienceTimeValue": 3.0,
  "metricCode": "ANIOS",
  "notes": "Requisito cumplido.",
  "sourceSystem": null,
  "sourceReference": null,
  "sourceSyncedUtc": null,
  "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
}
```

**Errores:** `400` (validación), `401`, `403`, `404`, `409`, **`422`** (archivo en `Draft` o regla de negocio).

---

## `PUT` Reemplazar una competencia

`PUT /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)). **Requiere archivo `Completed`**.

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `CurricularCompetencyResponse` con el `concurrencyToken` **nuevo** (también en el header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/curricular-competencies/$CC" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '{
    "requirementTypeCode": "EDUCACION",
    "requirementName": "Maestría en Administración",
    "competencyDomain": "Gestión",
    "experienceTimeValue": 5.0,
    "metricCode": "ANIOS",
    "notes": "Actualizado tras posgrado."
  }'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado), **`422`** (archivo en `Draft`).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`. **Requiere archivo `Completed`**.

Body = **array desnudo** de operaciones JSON Patch (RFC 6902), **sin** envoltorio `operations` (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: `requirementTypeCode`, `requirementName`, `competencyDomain`, `experienceTimeValue`, `metricCode`, `notes`, `sourceSystem`, `sourceReference`, `sourceSyncedUtc`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/curricular-competencies/$CC" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '[
    { "op": "replace", "path": "/experienceTimeValue", "value": 5.0 },
    { "op": "replace", "path": "/requirementName", "value": "Maestría en Administración" }
  ]'
```

**Respuesta `200`** — `CurricularCompetencyResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `401`, `403`, `404`, `409`, **`422`** (archivo en `Draft`).

---

## `DELETE` Eliminar una competencia

`DELETE /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem. **Requiere archivo `Completed`**.

**Respuesta `200`** — devuelve el token del **archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)):

```jsonc
// 200 OK
{ "parentConcurrencyToken": "f0e1d2c3-b4a5-6978-8a9b-0c1d2e3f4a5b" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/curricular-competencies/$CC" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...f809"'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado).
