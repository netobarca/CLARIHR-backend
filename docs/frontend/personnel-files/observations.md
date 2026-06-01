# Observations — Bitácora de observaciones del archivo

Sub‑recurso de **bitácora**: anotaciones de texto (atribuidas al usuario que las crea) sobre un archivo de personal. Es un **log append‑only**: solo se puede **listar** y **agregar**; las observaciones son inmutables una vez creadas (no hay `PUT`/`PATCH`/`DELETE` ni `concurrencyToken`). Cuelga de un archivo de personal ya existente.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este sub‑recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/observations` | Listar las observaciones del archivo (más recientes primero) |
| `POST` | `/api/v1/personnel-files/{publicId}/observations` | Agregar una observación |

**Path params:** `publicId` (uuid) = archivo de personal (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

> No hay `PUT`/`PATCH`/`DELETE` **por diseño**: la bitácora es inmutable. Tampoco hay `If-Match` ni `concurrencyToken` en este recurso.

---

## `GET` Listar observaciones

`GET /api/v1/personnel-files/{publicId}/observations`

Devuelve el **array completo** (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)) de las observaciones del archivo, **ordenadas de la más reciente a la más antigua**.

**Respuesta `200`** — array de `PersonnelFileObservationResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `publicId` | uuid | Id de la observación. |
| `authorUserPublicId` | uuid | Usuario que la registró. |
| `note` | string (nullable) | Texto de la observación. |
| `createdAtUtc` | string (date-time) | Momento de creación. |

```bash
curl "$BASE/api/v1/personnel-files/$ID/observations" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "publicId": "5c8e...12",
    "authorUserPublicId": "91cc...34",
    "note": "Se actualizó el contrato tras la promoción.",
    "createdAtUtc": "2026-05-31T15:20:00Z"
  },
  {
    "publicId": "3a1b...90",
    "authorUserPublicId": "91cc...34",
    "note": "Documentación de ingreso completa.",
    "createdAtUtc": "2026-05-20T09:00:00Z"
  }
]
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar observación

`POST /api/v1/personnel-files/{publicId}/observations`

Anexa una observación al log, **atribuida automáticamente al usuario actual** (no se manda el autor en el body). No lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `note` | string (nullable) | no | Texto de la observación. |

**Respuesta `201`** — `PersonnelFileObservationResponse` (mismos campos que el `GET`). No devuelve `concurrencyToken` (las observaciones no se editan).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/observations" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "note": "Se actualizó el contrato tras la promoción." }'
```

```jsonc
// 201 Created
{
  "publicId": "5c8e...12",
  "authorUserPublicId": "91cc...34",
  "note": "Se actualizó el contrato tras la promoción.",
  "createdAtUtc": "2026-05-31T15:20:00Z"
}
```

**Errores:** `400` (validación), `404`, `409` (conflicto), `422` (regla de negocio).
