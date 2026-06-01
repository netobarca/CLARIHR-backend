# Personnel Files — Performance Evaluations (evaluaciones de desempeño)

Las **evaluations** registran las evaluaciones de desempeño del empleado: evaluador, fecha, puntaje (cuantitativo y/o cualitativo), comentario y referencia al sistema origen (para datos sincronizados desde un sistema externo). Cuelgan de un archivo ya existente.

> ⚠️ **Sub‑recurso de empleado (state‑gated):** solo se puede **crear/editar/eliminar** sobre un archivo **finalizado** (`Completed`). Sobre un archivo en `Draft`, las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) responden **422** (regla de estado). El `GET` funciona en cualquier estado. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment) y [finalize](./finalize.md).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/evaluations` | Listar las evaluaciones del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/evaluations` | Agregar una evaluación |
| `GET`    | `/api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` | Obtener una evaluación por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` | Reemplazar una evaluación |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` | Cambios parciales sobre una evaluación |
| `DELETE` | `/api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` | Eliminar una evaluación |

`{publicId}` = id del archivo de personal. `{evaluationPublicId}` = id de la evaluación (GUID; ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

---

## Campos del recurso

Mismos campos para el body de `POST`/`PUT` y para la respuesta (la respuesta agrega el id y el token):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `evaluatorName` | string | no | Nombre del evaluador. |
| `evaluationDateUtc` | string (date-time) | sí | Fecha de la evaluación (UTC). |
| `score` | number (double) | no | Puntaje cuantitativo. |
| `qualitativeScoreCode` | string | no | Código de calificación cualitativa (catálogo). |
| `comment` | string | no | Comentario/observación. |
| `sourceSystem` | string | no | Sistema de origen (si fue sincronizada externamente). |
| `sourceReference` | string | no | Referencia/id en el sistema de origen. |
| `sourceSyncedUtc` | string (date-time) | no | Fecha de la última sincronización (UTC). |

**Campos adicionales en la respuesta** (`PerformanceEvaluationResponse`):

| Campo | Tipo |
|-------|------|
| `evaluationPublicId` | uuid |
| `concurrencyToken` | uuid |

---

## `GET` Listar evaluaciones

`GET /api/v1/personnel-files/{publicId}/evaluations`

Devuelve el **array completo** de evaluaciones del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)).

```bash
curl "$BASE/api/v1/personnel-files/$ID/evaluations" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "evaluationPublicId": "e1f2a3b4-5c6d-4e7f-8091-a2b3c4d5e6f7",
    "evaluatorName": "Mariana López",
    "evaluationDateUtc": "2026-03-15T00:00:00Z",
    "score": 4.5,
    "qualitativeScoreCode": "SOBRESALIENTE",
    "comment": "Cumplió todos los objetivos del periodo.",
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

`GET /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` → `200` `PerformanceEvaluationResponse`. El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` de este ítem.

```bash
curl "$BASE/api/v1/personnel-files/$ID/evaluations/$EVAL" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una evaluación

`POST /api/v1/personnel-files/{publicId}/evaluations` · **sin `If-Match`** (ver [Convenciones §6](./_conventions.md#6-crear-post)). **Requiere archivo `Completed`** (en `Draft` → `422`).

**Body** (`application/json`): ver la [tabla de campos](#campos-del-recurso).

**Respuesta `201`** — `PerformanceEvaluationResponse` (+ headers `Location` y `ETag` con el `concurrencyToken` inicial).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/evaluations" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "evaluatorName": "Mariana López",
    "evaluationDateUtc": "2026-03-15T00:00:00Z",
    "score": 4.5,
    "qualitativeScoreCode": "SOBRESALIENTE",
    "comment": "Cumplió todos los objetivos del periodo."
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{publicId}/evaluations/e1f2...e6f7   ETag: "a1b2...f809"
{
  "evaluationPublicId": "e1f2a3b4-5c6d-4e7f-8091-a2b3c4d5e6f7",
  "evaluatorName": "Mariana López",
  "evaluationDateUtc": "2026-03-15T00:00:00Z",
  "score": 4.5,
  "qualitativeScoreCode": "SOBRESALIENTE",
  "comment": "Cumplió todos los objetivos del periodo.",
  "sourceSystem": null,
  "sourceReference": null,
  "sourceSyncedUtc": null,
  "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
}
```

**Errores:** `400` (validación), `401`, `403`, `404`, `409`, **`422`** (archivo en `Draft` o regla de negocio).

---

## `PUT` Reemplazar una evaluación

`PUT /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)). **Requiere archivo `Completed`**.

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `PerformanceEvaluationResponse` con el `concurrencyToken` **nuevo** (también en el header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/evaluations/$EVAL" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '{
    "evaluatorName": "Mariana López",
    "evaluationDateUtc": "2026-03-15T00:00:00Z",
    "score": 4.8,
    "qualitativeScoreCode": "SOBRESALIENTE",
    "comment": "Ajuste tras calibración."
  }'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado), **`422`** (archivo en `Draft`).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`. **Requiere archivo `Completed`**.

Body = **array desnudo** de operaciones JSON Patch (RFC 6902), **sin** envoltorio `operations` (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: `evaluatorName`, `evaluationDateUtc`, `score`, `qualitativeScoreCode`, `comment`, `sourceSystem`, `sourceReference`, `sourceSyncedUtc`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/evaluations/$EVAL" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '[
    { "op": "replace", "path": "/score", "value": 4.8 },
    { "op": "replace", "path": "/comment", "value": "Ajuste tras calibración." }
  ]'
```

**Respuesta `200`** — `PerformanceEvaluationResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `401`, `403`, `404`, `409`, **`422`** (archivo en `Draft`).

---

## `DELETE` Eliminar una evaluación

`DELETE /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem. **Requiere archivo `Completed`**.

**Respuesta `200`** — devuelve el token del **archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)):

```jsonc
// 200 OK
{ "parentConcurrencyToken": "f0e1d2c3-b4a5-6978-8a9b-0c1d2e3f4a5b" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/evaluations/$EVAL" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...f809"'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado).
