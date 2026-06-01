# Assets & Accesses — Activos y accesos

Sub‑recurso de **empleo**: registra los activos y accesos asignados a la persona (equipos, llaves, credenciales, permisos de sistema), con su nivel de acceso, vigencia y estado de entrega. Pertenece a un archivo de personal ya creado.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> ⚠️ **Solo sobre archivo finalizado.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) solo se permiten sobre un archivo **finalizado** (empleado, `lifecycleStatus = Completed`). Sobre un archivo en `Draft` responden **422**. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/assets-accesses` | Listar todos los activos/accesos del archivo |
| `POST` | `/api/v1/personnel-files/{publicId}/assets-accesses` | Agregar un activo/acceso |
| `GET`  | `/api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` | Obtener un activo/acceso por id |
| `PUT`  | `/api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` | Reemplazar los campos de negocio |
| `PATCH`| `/api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` | Cambios parciales (incluye `isActive`) |
| `DELETE`| `/api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` | Eliminar un activo/acceso |

`publicId` = id del archivo de personal. `assetAccessPublicId` = id del activo/acceso.

---

## `GET` Listar activos/accesos

`GET /api/v1/personnel-files/{publicId}/assets-accesses`

Devuelve el array completo (no paginado). Cada ítem trae su propio `concurrencyToken` para `If-Match`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/assets-accesses" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "assetAccessPublicId": "6c4d...f2",
    "assetTypeCode": "LAPTOP",
    "assetOrAccessName": "Dell Latitude 5440 (SN ABC123)",
    "accessLevelCode": "ESTANDAR",
    "startDateUtc": "2026-01-06T00:00:00Z",
    "endDateUtc": null,
    "deliveryDateUtc": "2026-01-06T09:30:00Z",
    "deliveryStatusCode": "ENTREGADO",
    "isActive": true,
    "notes": "Cargador incluido",
    "concurrencyToken": "a1b2...c3"
  }
]
```

**Errores:** `401`, `403`, `404`.

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` → `200` con un ítem (mismos campos que en la lista). El `concurrencyToken` que devuelve va en `If-Match`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/assets-accesses/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un activo/acceso

`POST /api/v1/personnel-files/{publicId}/assets-accesses`

Sin `If-Match` (ítem nuevo). Responde `201` con el ítem creado + headers `Location` y `ETag` (token inicial).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `assetTypeCode` | string | no | Código de catálogo (tipo de activo/acceso). |
| `assetOrAccessName` | string | no | Nombre/descripción del activo o acceso. |
| `accessLevelCode` | string | no | Código de catálogo (nivel de acceso). |
| `startDateUtc` | string (date-time) | sí | Inicio de vigencia. |
| `endDateUtc` | string (date-time) | no | Fin de vigencia (nullable). |
| `deliveryDateUtc` | string (date-time) | no | Fecha de entrega (nullable). |
| `deliveryStatusCode` | string | no | Código de catálogo (estado de entrega). |
| `isActive` | boolean | sí | Estado activo. |
| `notes` | string | no | |

**Respuesta `201`** — campos del ítem:

| Campo | Tipo |
|-------|------|
| `assetAccessPublicId` | uuid |
| `assetTypeCode` | string (nullable) |
| `assetOrAccessName` | string (nullable) |
| `accessLevelCode` | string (nullable) |
| `startDateUtc` | string (date-time) |
| `endDateUtc` | string (date-time, nullable) |
| `deliveryDateUtc` | string (date-time, nullable) |
| `deliveryStatusCode` | string (nullable) |
| `isActive` | boolean |
| `notes` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/assets-accesses" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "assetTypeCode": "LAPTOP",
    "assetOrAccessName": "Dell Latitude 5440 (SN ABC123)",
    "accessLevelCode": "ESTANDAR",
    "startDateUtc": "2026-01-06T00:00:00Z",
    "deliveryDateUtc": "2026-01-06T09:30:00Z",
    "deliveryStatusCode": "ENTREGADO",
    "isActive": true
  }'
```

```jsonc
// 201 Created   Location: .../assets-accesses/6c4d...f2   ETag: "a1b2...c3"
{
  "assetAccessPublicId": "6c4d...f2",
  "assetTypeCode": "LAPTOP",
  "assetOrAccessName": "Dell Latitude 5440 (SN ABC123)",
  "accessLevelCode": "ESTANDAR",
  "startDateUtc": "2026-01-06T00:00:00Z",
  "endDateUtc": null,
  "deliveryDateUtc": "2026-01-06T09:30:00Z",
  "deliveryStatusCode": "ENTREGADO",
  "isActive": true,
  "notes": null,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `409` (concurrencia), `422` (archivo en `Draft` / regla de estado).

---

## `PUT` Reemplazar un activo/acceso

`PUT /api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza los campos de negocio. El estado activo **se preserva** (se cambia solo por `PATCH`). Body = mismo shape que el `POST`. Devuelve `200` con el ítem actualizado y el nuevo token en el body y en `ETag`.

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/assets-accesses/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "assetTypeCode": "LAPTOP",
    "assetOrAccessName": "Dell Latitude 5440 (SN ABC123)",
    "accessLevelCode": "ESTANDAR",
    "startDateUtc": "2026-01-06T00:00:00Z",
    "endDateUtc": "2026-12-31T00:00:00Z",
    "deliveryStatusCode": "DEVUELTO",
    "isActive": true
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de estado).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` **+ `isActive`** (mecanismo para activar/desactivar). Devuelve `200` con el ítem y el nuevo `concurrencyToken`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/assets-accesses/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/deliveryStatusCode", "value": "DEVUELTO" },
    { "op": "replace", "path": "/isActive", "value": false }
  ]'
```

**Errores:** `400` (patch inválido), `409`, `422`.

---

## `DELETE` Eliminar un activo/acceso

`DELETE /api/v1/personnel-files/{publicId}/assets-accesses/{assetAccessPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Devuelve `200` con el **token del archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/assets-accesses/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "9f8e...d4" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.
