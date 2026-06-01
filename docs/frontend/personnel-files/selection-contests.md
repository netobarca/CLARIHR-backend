# Personnel Files — Selection Contests (concursos de selección)

Los **selection contests** registran la participación del empleado en concursos/procesos de selección internos: código y nombre del concurso, fecha, resultado y notas, con la referencia al sistema origen. Cuelgan de un archivo ya existente.

> ⚠️ **Sub‑recurso de empleado (state‑gated):** solo se puede **crear/editar/eliminar** sobre un archivo **finalizado** (`Completed`). Sobre un archivo en `Draft`, las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) responden **422** (regla de estado). El `GET` funciona en cualquier estado. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment) y [finalize](./finalize.md).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/selection-contests` | Listar los concursos del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/selection-contests` | Agregar un concurso |
| `GET`    | `/api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` | Obtener un concurso por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` | Reemplazar un concurso |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` | Cambios parciales sobre un concurso |
| `DELETE` | `/api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` | Eliminar un concurso |

`{publicId}` = id del archivo de personal. `{selectionContestPublicId}` = id del concurso (GUID; ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

---

## Campos del recurso

Mismos campos para el body de `POST`/`PUT` y para la respuesta (la respuesta agrega el id y el token):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `contestCode` | string | no | Código del concurso. |
| `contestName` | string | no | Nombre del concurso. |
| `contestDateUtc` | string (date-time) | sí | Fecha del concurso (UTC). |
| `resultCode` | string | no | Código del resultado (catálogo). |
| `notes` | string | no | Notas/observaciones. |
| `sourceSystem` | string | no | Sistema de origen (si fue sincronizado externamente). |
| `sourceReference` | string | no | Referencia/id en el sistema de origen. |
| `sourceSyncedUtc` | string (date-time) | no | Fecha de la última sincronización (UTC). |

**Campos adicionales en la respuesta** (`SelectionContestResponse`):

| Campo | Tipo |
|-------|------|
| `selectionContestPublicId` | uuid |
| `concurrencyToken` | uuid |

---

## `GET` Listar concursos

`GET /api/v1/personnel-files/{publicId}/selection-contests`

Devuelve el **array completo** de concursos del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)).

```bash
curl "$BASE/api/v1/personnel-files/$ID/selection-contests" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "selectionContestPublicId": "f3a4b5c6-7d8e-4f90-a1b2-c3d4e5f6a7b8",
    "contestCode": "CONC-2026-014",
    "contestName": "Coordinador de Operaciones",
    "contestDateUtc": "2026-02-10T00:00:00Z",
    "resultCode": "GANADOR",
    "notes": "Promovido al puesto.",
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

`GET /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` → `200` `SelectionContestResponse`. El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` de este ítem.

```bash
curl "$BASE/api/v1/personnel-files/$ID/selection-contests/$CONTEST" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un concurso

`POST /api/v1/personnel-files/{publicId}/selection-contests` · **sin `If-Match`** (ver [Convenciones §6](./_conventions.md#6-crear-post)). **Requiere archivo `Completed`** (en `Draft` → `422`).

**Body** (`application/json`): ver la [tabla de campos](#campos-del-recurso).

**Respuesta `201`** — `SelectionContestResponse` (+ headers `Location` y `ETag` con el `concurrencyToken` inicial).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/selection-contests" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "contestCode": "CONC-2026-014",
    "contestName": "Coordinador de Operaciones",
    "contestDateUtc": "2026-02-10T00:00:00Z",
    "resultCode": "GANADOR",
    "notes": "Promovido al puesto."
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{publicId}/selection-contests/f3a4...a7b8   ETag: "a1b2...f809"
{
  "selectionContestPublicId": "f3a4b5c6-7d8e-4f90-a1b2-c3d4e5f6a7b8",
  "contestCode": "CONC-2026-014",
  "contestName": "Coordinador de Operaciones",
  "contestDateUtc": "2026-02-10T00:00:00Z",
  "resultCode": "GANADOR",
  "notes": "Promovido al puesto.",
  "sourceSystem": null,
  "sourceReference": null,
  "sourceSyncedUtc": null,
  "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
}
```

**Errores:** `400` (validación), `401`, `403`, `404`, `409`, **`422`** (archivo en `Draft` o regla de negocio).

---

## `PUT` Reemplazar un concurso

`PUT /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)). **Requiere archivo `Completed`**.

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `SelectionContestResponse` con el `concurrencyToken` **nuevo** (también en el header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/selection-contests/$CONTEST" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '{
    "contestCode": "CONC-2026-014",
    "contestName": "Coordinador de Operaciones",
    "contestDateUtc": "2026-02-10T00:00:00Z",
    "resultCode": "FINALISTA",
    "notes": "Quedó en segundo lugar."
  }'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado), **`422`** (archivo en `Draft`).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`. **Requiere archivo `Completed`**.

Body = **array desnudo** de operaciones JSON Patch (RFC 6902), **sin** envoltorio `operations` (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: `contestCode`, `contestName`, `contestDateUtc`, `resultCode`, `notes`, `sourceSystem`, `sourceReference`, `sourceSyncedUtc`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/selection-contests/$CONTEST" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '[
    { "op": "replace", "path": "/resultCode", "value": "FINALISTA" },
    { "op": "replace", "path": "/notes", "value": "Quedó en segundo lugar." }
  ]'
```

**Respuesta `200`** — `SelectionContestResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `401`, `403`, `404`, `409`, **`422`** (archivo en `Draft`).

---

## `DELETE` Eliminar un concurso

`DELETE /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem. **Requiere archivo `Completed`**.

**Respuesta `200`** — devuelve el token del **archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)):

```jsonc
// 200 OK
{ "parentConcurrencyToken": "f0e1d2c3-b4a5-6978-8a9b-0c1d2e3f4a5b" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/selection-contests/$CONTEST" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...f809"'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado).
