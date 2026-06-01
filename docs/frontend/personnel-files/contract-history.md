# Contract History — Historial de contratos

Sub‑recurso de **empleo**: registra el historial de contratos de la persona (tipo, fechas, plaza asociada). Pertenece a un archivo de personal ya creado.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> ⚠️ **Solo sobre archivo finalizado.** Las escrituras (`POST`/`PUT`/`PATCH`) solo se permiten sobre un archivo **finalizado** (empleado, `lifecycleStatus = Completed`). Sobre un archivo en `Draft` responden **422**. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

> **Sin `DELETE`.** El historial de contratos no se elimina (registro de trayectoria). Se corrige por `PUT`/`PATCH`.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/contract-history` | Listar todo el historial de contratos del archivo |
| `POST` | `/api/v1/personnel-files/{publicId}/contract-history` | Agregar una entrada de contrato |
| `GET`  | `/api/v1/personnel-files/{publicId}/contract-history/{contractHistoryPublicId}` | Obtener una entrada por id |
| `PUT`  | `/api/v1/personnel-files/{publicId}/contract-history/{contractHistoryPublicId}` | Reemplazar los campos de negocio |
| `PATCH`| `/api/v1/personnel-files/{publicId}/contract-history/{contractHistoryPublicId}` | Cambios parciales (incluye `isActive`) |

`publicId` = id del archivo de personal. `contractHistoryPublicId` = id de la entrada.

---

## `GET` Listar historial

`GET /api/v1/personnel-files/{publicId}/contract-history`

Devuelve el array completo (no paginado). Cada ítem trae su propio `concurrencyToken` para `If-Match`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/contract-history" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "contractHistoryPublicId": "9d5e...c4",
    "contractTypeCode": "INDEFINIDO",
    "contractDate": "2026-01-06T00:00:00Z",
    "contractEndDate": null,
    "positionSlotPublicId": "77aa...12",
    "isActive": true,
    "notes": "Contrato inicial",
    "concurrencyToken": "a1b2...c3"
  }
]
```

**Errores:** `401`, `403`, `404`.

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/contract-history/{contractHistoryPublicId}` → `200` con un ítem (mismos campos que en la lista). El `concurrencyToken` que devuelve va en `If-Match`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/contract-history/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una entrada

`POST /api/v1/personnel-files/{publicId}/contract-history`

Sin `If-Match` (ítem nuevo). Responde `201` con el ítem creado + headers `Location` y `ETag` (token inicial).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `contractTypeCode` | string | no | Código de catálogo (tipo de contrato). |
| `contractDate` | string (date-time) | sí | Fecha del contrato. |
| `contractEndDate` | string (date-time) | no | Fecha de fin (nullable). |
| `positionSlotPublicId` | uuid | no | Plaza asociada. |
| `isActive` | boolean | sí | Estado activo de la entrada. |
| `notes` | string | no | |

**Respuesta `201`** — campos del ítem:

| Campo | Tipo |
|-------|------|
| `contractHistoryPublicId` | uuid |
| `contractTypeCode` | string (nullable) |
| `contractDate` | string (date-time) |
| `contractEndDate` | string (date-time, nullable) |
| `positionSlotPublicId` | uuid (nullable) |
| `isActive` | boolean |
| `notes` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/contract-history" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "contractTypeCode": "INDEFINIDO",
    "contractDate": "2026-01-06T00:00:00Z",
    "positionSlotPublicId": "77aa...12",
    "isActive": true,
    "notes": "Contrato inicial"
  }'
```

```jsonc
// 201 Created   Location: .../contract-history/9d5e...c4   ETag: "a1b2...c3"
{
  "contractHistoryPublicId": "9d5e...c4",
  "contractTypeCode": "INDEFINIDO",
  "contractDate": "2026-01-06T00:00:00Z",
  "contractEndDate": null,
  "positionSlotPublicId": "77aa...12",
  "isActive": true,
  "notes": "Contrato inicial",
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `409` (concurrencia), `422` (archivo en `Draft` / regla de estado).

---

## `PUT` Reemplazar una entrada

`PUT /api/v1/personnel-files/{publicId}/contract-history/{contractHistoryPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza los campos de negocio. El estado activo **se preserva** (se cambia solo por `PATCH`). Body = mismo shape que el `POST`. Devuelve `200` con el ítem actualizado y el nuevo token en el body y en `ETag`.

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/contract-history/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "contractTypeCode": "PLAZO_FIJO",
    "contractDate": "2026-01-06T00:00:00Z",
    "contractEndDate": "2026-12-31T00:00:00Z",
    "positionSlotPublicId": "77aa...12",
    "isActive": true
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de estado).

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/contract-history/{contractHistoryPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` **+ `isActive`** (mecanismo para activar/desactivar). Devuelve `200` con el ítem y el nuevo `concurrencyToken`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/contract-history/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/contractEndDate", "value": "2026-12-31T00:00:00Z" },
    { "op": "replace", "path": "/isActive", "value": false }
  ]'
```

**Errores:** `400` (patch inválido), `409`, `422`.
