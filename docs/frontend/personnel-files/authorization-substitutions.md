# Authorization Substitutions — Sustituciones de autorización

Sub‑recurso de **empleo**: registra quién sustituye a la persona en sus autorizaciones durante un período (suplencias). Pertenece a un archivo de personal ya creado.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> ⚠️ **Solo sobre archivo finalizado.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) solo se permiten sobre un archivo **finalizado** (empleado, `lifecycleStatus = Completed`). Sobre un archivo en `Draft` responden **422**. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/authorization-substitutions` | Listar todas las sustituciones del archivo |
| `POST` | `/api/v1/personnel-files/{publicId}/authorization-substitutions` | Agregar una sustitución |
| `GET`  | `/api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` | Obtener una sustitución por id |
| `PUT`  | `/api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` | Reemplazar los campos de negocio |
| `PATCH`| `/api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` | Cambios parciales (incluye `isActive`) |
| `DELETE`| `/api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` | Eliminar una sustitución |

`publicId` = id del archivo de personal. `authorizationSubstitutionPublicId` = id de la sustitución.

---

## `GET` Listar sustituciones

`GET /api/v1/personnel-files/{publicId}/authorization-substitutions`

Devuelve el array completo (no paginado). Cada ítem trae su propio `concurrencyToken` para `If-Match`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/authorization-substitutions" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "authorizationSubstitutionPublicId": "5b2d...a1",
    "substitutionTypeCode": "VACACIONES",
    "substitutePersonnelFilePublicId": "8c3e...b2",
    "substitutePositionTitle": "Jefe de Compras (s)",
    "startDate": "2026-07-01T00:00:00Z",
    "endDate": "2026-07-15T00:00:00Z",
    "isActive": true,
    "notes": "Cubre aprobaciones de órdenes de compra",
    "concurrencyToken": "a1b2...c3"
  }
]
```

**Errores:** `401`, `403`, `404`.

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` → `200` con un ítem (mismos campos que en la lista). El `concurrencyToken` que devuelve va en `If-Match`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/authorization-substitutions/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una sustitución

`POST /api/v1/personnel-files/{publicId}/authorization-substitutions`

Sin `If-Match` (ítem nuevo). Responde `201` con el ítem creado + headers `Location` y `ETag` (token inicial).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `substitutionTypeCode` | string | no | Código de catálogo (tipo de sustitución). |
| `substitutePersonnelFilePublicId` | uuid | sí | Archivo de personal del sustituto. |
| `substitutePositionTitle` | string | no | Título del puesto del sustituto (texto libre). |
| `startDate` | string (date-time) | sí | Inicio de la suplencia. |
| `endDate` | string (date-time) | no | Fin de la suplencia (nullable). |
| `isActive` | boolean | sí | Estado activo de la sustitución. |
| `notes` | string | no | |

**Respuesta `201`** — campos del ítem:

| Campo | Tipo |
|-------|------|
| `authorizationSubstitutionPublicId` | uuid |
| `substitutionTypeCode` | string (nullable) |
| `substitutePersonnelFilePublicId` | uuid |
| `substitutePositionTitle` | string (nullable) |
| `startDate` | string (date-time) |
| `endDate` | string (date-time, nullable) |
| `isActive` | boolean |
| `notes` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/authorization-substitutions" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "substitutionTypeCode": "VACACIONES",
    "substitutePersonnelFilePublicId": "8c3e...b2",
    "substitutePositionTitle": "Jefe de Compras (s)",
    "startDate": "2026-07-01T00:00:00Z",
    "endDate": "2026-07-15T00:00:00Z",
    "isActive": true
  }'
```

```jsonc
// 201 Created   Location: .../authorization-substitutions/5b2d...a1   ETag: "a1b2...c3"
{
  "authorizationSubstitutionPublicId": "5b2d...a1",
  "substitutionTypeCode": "VACACIONES",
  "substitutePersonnelFilePublicId": "8c3e...b2",
  "substitutePositionTitle": "Jefe de Compras (s)",
  "startDate": "2026-07-01T00:00:00Z",
  "endDate": "2026-07-15T00:00:00Z",
  "isActive": true,
  "notes": null,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `409` (concurrencia), `422` (archivo en `Draft` / regla de estado).

---

## `PUT` Reemplazar una sustitución

`PUT /api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza los campos de negocio. El estado activo **se preserva** (se cambia solo por `PATCH`). Body = mismo shape que el `POST`. Devuelve `200` con el ítem actualizado y el nuevo token en el body y en `ETag`.

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/authorization-substitutions/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "substitutionTypeCode": "VACACIONES",
    "substitutePersonnelFilePublicId": "8c3e...b2",
    "substitutePositionTitle": "Jefe de Compras (s)",
    "startDate": "2026-07-01T00:00:00Z",
    "endDate": "2026-07-20T00:00:00Z",
    "isActive": true
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de estado).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` **+ `isActive`** (mecanismo para activar/desactivar). Devuelve `200` con el ítem y el nuevo `concurrencyToken`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/authorization-substitutions/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/endDate", "value": "2026-07-20T00:00:00Z" },
    { "op": "replace", "path": "/isActive", "value": false }
  ]'
```

**Errores:** `400` (patch inválido), `409`, `422`.

---

## `DELETE` Eliminar una sustitución

`DELETE /api/v1/personnel-files/{publicId}/authorization-substitutions/{authorizationSubstitutionPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Devuelve `200` con el **token del archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/authorization-substitutions/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "9f8e...d4" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.
