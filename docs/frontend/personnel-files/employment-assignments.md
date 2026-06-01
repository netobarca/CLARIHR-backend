# Employment Assignments — Asignaciones de empleo

Sub‑recurso de **empleo**: registra las asignaciones de empleo de una persona (puesto, unidad organizativa, centro de trabajo/costo, vigencia, si es la principal). Pertenece a un archivo de personal ya creado.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> ⚠️ **Solo sobre archivo finalizado.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) solo se permiten sobre un archivo **finalizado** (empleado, `lifecycleStatus = Completed`). Sobre un archivo en `Draft` responden **422**. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/employment-assignments` | Listar todas las asignaciones del archivo |
| `POST` | `/api/v1/personnel-files/{publicId}/employment-assignments` | Agregar una asignación |
| `GET`  | `/api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` | Obtener una asignación por id |
| `PUT`  | `/api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` | Reemplazar los campos de negocio de una asignación |
| `PATCH`| `/api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` | Cambios parciales (incluye `isActive`) |
| `DELETE`| `/api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` | Eliminar una asignación |

`publicId` = id del archivo de personal. `employmentAssignmentPublicId` = id de la asignación.

---

## `GET` Listar asignaciones

`GET /api/v1/personnel-files/{publicId}/employment-assignments`

Devuelve el array completo (no paginado) de asignaciones del archivo. Cada ítem trae su propio `concurrencyToken`, que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/employment-assignments" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "employmentAssignmentPublicId": "7a1c...e9",
    "assignmentTypeCode": "PERMANENTE",
    "positionSlotPublicId": "77aa...12",
    "orgUnitPublicId": "0b1d...e9",
    "workCenterPublicId": "33cd...90",
    "costCenterPublicId": "44ef...01",
    "startDate": "2026-01-06T00:00:00Z",
    "endDate": null,
    "isPrimary": true,
    "isActive": true,
    "notes": "Asignación inicial",
    "concurrencyToken": "a1b2...c3"
  }
]
```

**Errores:** `401`, `403`, `404`.

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` → `200` con un ítem (mismos campos que en la lista). El `concurrencyToken` que devuelve es el que va en `If-Match`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/employment-assignments/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una asignación

`POST /api/v1/personnel-files/{publicId}/employment-assignments`

Sin `If-Match` (ítem nuevo). Responde `201` con el ítem creado + headers `Location` y `ETag` (token inicial).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `assignmentTypeCode` | string | no | Código de catálogo (tipo de asignación). |
| `positionSlotPublicId` | uuid | no | Plaza asignada. |
| `orgUnitPublicId` | uuid | no | Unidad organizativa. |
| `workCenterPublicId` | uuid | no | Centro de trabajo. |
| `costCenterPublicId` | uuid | no | Centro de costo. |
| `startDate` | string (date-time) | sí | Inicio de vigencia. |
| `endDate` | string (date-time) | no | Fin de vigencia (nullable). |
| `isPrimary` | boolean | sí | Si es la asignación principal. |
| `isActive` | boolean | sí | Estado activo de la asignación. |
| `notes` | string | no | |

**Respuesta `201`** — campos del ítem:

| Campo | Tipo |
|-------|------|
| `employmentAssignmentPublicId` | uuid |
| `assignmentTypeCode` | string (nullable) |
| `positionSlotPublicId` | uuid (nullable) |
| `orgUnitPublicId` | uuid (nullable) |
| `workCenterPublicId` | uuid (nullable) |
| `costCenterPublicId` | uuid (nullable) |
| `startDate` | string (date-time) |
| `endDate` | string (date-time, nullable) |
| `isPrimary` | boolean |
| `isActive` | boolean |
| `notes` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/employment-assignments" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "assignmentTypeCode": "PERMANENTE",
    "positionSlotPublicId": "77aa...12",
    "orgUnitPublicId": "0b1d...e9",
    "startDate": "2026-01-06T00:00:00Z",
    "isPrimary": true,
    "isActive": true,
    "notes": "Asignación inicial"
  }'
```

```jsonc
// 201 Created   Location: .../employment-assignments/7a1c...e9   ETag: "a1b2...c3"
{
  "employmentAssignmentPublicId": "7a1c...e9",
  "assignmentTypeCode": "PERMANENTE",
  "positionSlotPublicId": "77aa...12",
  "orgUnitPublicId": "0b1d...e9",
  "startDate": "2026-01-06T00:00:00Z",
  "endDate": null,
  "isPrimary": true,
  "isActive": true,
  "notes": "Asignación inicial",
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `409` (concurrencia), `422` (archivo en `Draft` / regla de estado).

---

## `PUT` Reemplazar una asignación

`PUT /api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza los campos de negocio. El estado activo **se preserva** (se cambia solo por `PATCH`). Body = mismo shape que el `POST`. Devuelve `200` con el ítem actualizado y el nuevo token en el body y en `ETag`.

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/employment-assignments/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "assignmentTypeCode": "TEMPORAL",
    "positionSlotPublicId": "77aa...12",
    "startDate": "2026-01-06T00:00:00Z",
    "endDate": "2026-12-31T00:00:00Z",
    "isPrimary": true,
    "isActive": true,
    "notes": "Prórroga"
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de estado).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` **+ `isActive`** (este es el mecanismo para activar/desactivar la asignación). Devuelve `200` con el ítem y el nuevo `concurrencyToken`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/employment-assignments/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/isPrimary", "value": false },
    { "op": "replace", "path": "/isActive", "value": false }
  ]'
```

**Errores:** `400` (patch inválido), `409`, `422`.

---

## `DELETE` Eliminar una asignación

`DELETE /api/v1/personnel-files/{publicId}/employment-assignments/{employmentAssignmentPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Devuelve `200` con el **token del archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/employment-assignments/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "9f8e...d4" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.
