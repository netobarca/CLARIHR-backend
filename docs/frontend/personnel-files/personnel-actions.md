# Personnel Actions — Acciones de personal

Sub‑recurso de **empleo**: bitácora **append‑only** (registro de auditoría) de acciones de personal sobre la persona (movimientos, sanciones, bonos, etc.), con tipo, estado, fechas de efecto y un monto opcional. Pertenece a un archivo de personal ya creado.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> ⚠️ **Solo sobre archivo finalizado.** El registro (`POST`) solo se permite sobre un archivo **finalizado** (empleado, `lifecycleStatus = Completed`). Sobre un archivo en `Draft` responde **422**. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

> **Append‑only:** NO hay `PUT`, `PATCH` ni `DELETE`. Las acciones se crean y se consultan; no se editan ni se borran. Algunas son generadas por el sistema (`isSystemGenerated = true`).

**Permisos:** `GET` (incl. `/export`) → `PersonnelFiles.Read` · `POST` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/personnel-actions` | Buscar/listar acciones (**paginado**, con filtros) |
| `POST` | `/api/v1/personnel-files/{publicId}/personnel-actions` | Registrar una acción |
| `GET`  | `/api/v1/personnel-files/{publicId}/personnel-actions/{personnelActionPublicId}` | Obtener una acción por id |
| `GET`  | `/api/v1/personnel-files/{publicId}/personnel-actions/export` | Exportar las acciones filtradas (archivo) |

`publicId` = id del archivo de personal. `personnelActionPublicId` = id de la acción.

---

## `GET` Buscar acciones (paginado)

`GET /api/v1/personnel-files/{publicId}/personnel-actions`

Lista paginada y filtrable de las acciones del archivo, ordenable. A diferencia de los demás sub‑recursos (que devuelven el array completo), este **sí es paginado** (ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)).

**Query params** (todos opcionales):

| Param | Tipo | Notas |
|-------|------|-------|
| `fromUtc` / `toUtc` | date-time | Rango por fecha de la acción. |
| `type` | string | Filtra por código de tipo de acción. |
| `status` | string | Filtra por código de estado de la acción. |
| `q` | string | Texto libre. |
| `sortBy` | string | Campo de orden permitido. |
| `sortDirection` | enum (`Asc`/`Desc`) | Default `Desc`. |
| `page` | int | 1‑based, default `1`. |
| `pageSize` | int | Default `20`, **máx `100`**. |

**Respuesta `200`** — `PagedResponse<PersonnelFilePersonnelActionResponse>`:

```jsonc
{
  "items": [
    {
      "personnelActionPublicId": "4e6f...d5",
      "actionTypeCode": "AUMENTO_SALARIAL",
      "actionStatusCode": "APROBADA",
      "actionDateUtc": "2026-03-01T00:00:00Z",
      "effectiveFromUtc": "2026-04-01T00:00:00Z",
      "effectiveToUtc": null,
      "description": "Ajuste por desempeño anual",
      "reference": "RES-2026-0142",
      "amount": 250.0,
      "currencyCode": "USD",
      "isSystemGenerated": false,
      "createdAtUtc": "2026-03-01T15:20:00Z",
      "modifiedAtUtc": null,
      "concurrencyToken": "a1b2...c3"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 12
}
```

```bash
curl "$BASE/api/v1/personnel-files/$ID/personnel-actions?type=AUMENTO_SALARIAL&fromUtc=2026-01-01T00:00:00Z&page=1&pageSize=20&sortDirection=Desc" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `400` (`pageSize` fuera de rango / filtro inválido), `401`, `403`, `404`, `422` (archivo en `Draft`).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/personnel-actions/{personnelActionPublicId}` → `200` con una acción (mismos campos que cada ítem de la lista).

```bash
curl "$BASE/api/v1/personnel-files/$ID/personnel-actions/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Registrar una acción

`POST /api/v1/personnel-files/{publicId}/personnel-actions`

Sin `If-Match` (ítem nuevo). Responde `201` con la acción creada + headers `Location` y `ETag` (token inicial).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `actionTypeCode` | string | no | Código de catálogo (tipo de acción). |
| `actionStatusCode` | string | no | Código de catálogo (estado de la acción). |
| `actionDateUtc` | string (date-time) | sí | Fecha de la acción. |
| `effectiveFromUtc` | string (date-time) | no | Inicio de efecto (nullable). |
| `effectiveToUtc` | string (date-time) | no | Fin de efecto (nullable). |
| `description` | string | no | |
| `reference` | string | no | Referencia externa (acta, resolución, etc.). |
| `amount` | number (double) | no | Monto asociado (nullable). |
| `currencyCode` | string | no | Código de moneda del `amount`. |

**Respuesta `201`** — campos del ítem:

| Campo | Tipo |
|-------|------|
| `personnelActionPublicId` | uuid |
| `actionTypeCode` | string (nullable) |
| `actionStatusCode` | string (nullable) |
| `actionDateUtc` | string (date-time) |
| `effectiveFromUtc` | string (date-time, nullable) |
| `effectiveToUtc` | string (date-time, nullable) |
| `description` | string (nullable) |
| `reference` | string (nullable) |
| `amount` | number (double, nullable) |
| `currencyCode` | string (nullable) |
| `isSystemGenerated` | boolean |
| `createdAtUtc` | string (date-time) |
| `modifiedAtUtc` | string (date-time, nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/personnel-actions" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "actionTypeCode": "AUMENTO_SALARIAL",
    "actionStatusCode": "APROBADA",
    "actionDateUtc": "2026-03-01T00:00:00Z",
    "effectiveFromUtc": "2026-04-01T00:00:00Z",
    "description": "Ajuste por desempeño anual",
    "reference": "RES-2026-0142",
    "amount": 250.0,
    "currencyCode": "USD"
  }'
```

```jsonc
// 201 Created   Location: .../personnel-actions/4e6f...d5   ETag: "a1b2...c3"
{
  "personnelActionPublicId": "4e6f...d5",
  "actionTypeCode": "AUMENTO_SALARIAL",
  "actionStatusCode": "APROBADA",
  "actionDateUtc": "2026-03-01T00:00:00Z",
  "effectiveFromUtc": "2026-04-01T00:00:00Z",
  "effectiveToUtc": null,
  "description": "Ajuste por desempeño anual",
  "reference": "RES-2026-0142",
  "amount": 250.0,
  "currencyCode": "USD",
  "isSystemGenerated": false,
  "createdAtUtc": "2026-03-01T15:20:00Z",
  "modifiedAtUtc": null,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `409` (concurrencia), `422` (archivo en `Draft` / regla de estado).

---

## `GET` Exportar

`GET /api/v1/personnel-files/{publicId}/personnel-actions/export`

Exporta las acciones **filtradas** como archivo (default `xlsx`). Acepta los mismos filtros que la búsqueda. Devuelve el archivo binario (`200`). Sujeto a **rate limit** (`429`) y a un límite de tamaño (`413` si el resultado excede el límite síncrono).

**Query params** (todos opcionales):

| Param | Tipo | Notas |
|-------|------|-------|
| `format` | string | Formato de salida. Default `xlsx`. |
| `fromUtc` / `toUtc` | date-time | Rango por fecha de la acción. |
| `type` | string | Filtra por código de tipo. |
| `status` | string | Filtra por código de estado. |
| `q` | string | Texto libre. |
| `sortBy` | string | Campo de orden permitido. |
| `sortDirection` | enum (`Asc`/`Desc`) | Default `Desc`. |

```bash
curl "$BASE/api/v1/personnel-files/$ID/personnel-actions/export?format=xlsx&type=AUMENTO_SALARIAL" \
  -H "Authorization: Bearer $TOKEN" \
  -o personnel-actions.xlsx
```

**Errores:** `400` (formato/filtro inválido), `401`, `403`, `404`, `413` (resultado demasiado grande), `422` (archivo en `Draft`), `429` (rate limit).
