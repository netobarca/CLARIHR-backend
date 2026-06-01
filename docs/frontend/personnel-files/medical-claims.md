# Medical Claims — Sub‑recurso de compensación

Los **medical claims** (reclamos médicos) registran las solicitudes de reembolso o cobertura médica de un empleado, típicamente asociadas a un seguro (`insurancePublicId`), con diagnóstico, montos reclamado/pagado y datos de origen (cuando provienen de un sistema externo).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> **Solo sobre archivos finalizados.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) requieren un archivo de personal **finalizado** (`Completed`, empleado). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/medical-claims` | Listar los reclamos del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/medical-claims` | Agregar un reclamo |
| `GET`    | `/api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` | Obtener un reclamo por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` | Reemplazar un reclamo |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` | Cambios parciales sobre un reclamo |
| `DELETE` | `/api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` | Eliminar un reclamo |

`{publicId}` = id del archivo de personal padre. `{medicalClaimPublicId}` = id del reclamo.

---

## `GET` Listar reclamos

`GET /api/v1/personnel-files/{publicId}/medical-claims`

Devuelve el **array completo** de reclamos del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `MedicalClaimResponse`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/medical-claims" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un reclamo

`POST /api/v1/personnel-files/{publicId}/medical-claims`

Crea un reclamo. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `insurancePublicId` | uuid (nullable) | no | Seguro asociado (ver [insurances.md](./insurances.md)). |
| `accountNumber` | string (nullable) | no | Número de cuenta/póliza del reclamo. |
| `claimTypeCode` | string (nullable) | no | Código de catálogo: tipo de reclamo. |
| `diagnosis` | string (nullable) | no | Diagnóstico. |
| `claimAmount` | number (double, nullable) | no | Monto reclamado. |
| `currencyCode` | string (nullable) | no | Código de moneda ISO (p. ej. `USD`). |
| `paidAmount` | number (double, nullable) | no | Monto pagado. |
| `responseTimeDays` | int (nullable) | no | Días de respuesta. |
| `notes` | string (nullable) | no | Notas libres. |
| `claimDateUtc` | string (date-time) | sí | Fecha del reclamo. |
| `sourceSystem` | string (nullable) | no | Sistema de origen (integración externa). |
| `sourceReference` | string (nullable) | no | Referencia en el sistema de origen. |
| `sourceSyncedUtc` | string (date-time, nullable) | no | Última sincronización desde el origen. |

**Respuesta `201`** — `MedicalClaimResponse` (+ headers `Location` y `ETag`). Incluye además `isActive` (gestionado por el backend):

| Campo | Tipo |
|-------|------|
| `medicalClaimPublicId` | uuid |
| `insurancePublicId` | uuid (nullable) |
| `accountNumber` | string (nullable) |
| `claimTypeCode` | string (nullable) |
| `diagnosis` | string (nullable) |
| `claimAmount` | number (double, nullable) |
| `currencyCode` | string (nullable) |
| `paidAmount` | number (double, nullable) |
| `responseTimeDays` | int (nullable) |
| `notes` | string (nullable) |
| `claimDateUtc` | string (date-time) |
| `sourceSystem` | string (nullable) |
| `sourceReference` | string (nullable) |
| `sourceSyncedUtc` | string (date-time, nullable) |
| `isActive` | boolean |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/medical-claims" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "insurancePublicId": "8e3a...c4",
    "accountNumber": "POL-00123",
    "claimTypeCode": "CONSULTA_MEDICA",
    "diagnosis": "Control anual",
    "claimAmount": 120.00,
    "currencyCode": "USD",
    "paidAmount": 96.00,
    "responseTimeDays": 5,
    "claimDateUtc": "2026-05-20T00:00:00Z"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{id}/medical-claims/6f9a...d2   ETag: "e1f2...a3"
{
  "medicalClaimPublicId": "6f9a...d2",
  "insurancePublicId": "8e3a...c4",
  "accountNumber": "POL-00123",
  "claimTypeCode": "CONSULTA_MEDICA",
  "diagnosis": "Control anual",
  "claimAmount": 120.00,
  "currencyCode": "USD",
  "paidAmount": 96.00,
  "responseTimeDays": 5,
  "notes": null,
  "claimDateUtc": "2026-05-20T00:00:00Z",
  "sourceSystem": null,
  "sourceReference": null,
  "sourceSyncedUtc": null,
  "isActive": true,
  "concurrencyToken": "e1f2...a3"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Obtener un reclamo por id

`GET /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` → `200` `MedicalClaimResponse` (mismos campos que el `201`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/medical-claims/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un reclamo

`PUT /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` · **requiere `If-Match`** con el `concurrencyToken` **del reclamo** (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza todos los campos de negocio. Body = mismo shape que el `POST`.

**Respuesta `200`** — `MedicalClaimResponse` (con el `concurrencyToken` nuevo + header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/medical-claims/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "e1f2...a3"' \
  -d '{
    "insurancePublicId": "8e3a...c4",
    "accountNumber": "POL-00123",
    "claimTypeCode": "CONSULTA_MEDICA",
    "diagnosis": "Control anual (ajustado)",
    "claimAmount": 120.00,
    "currencyCode": "USD",
    "paidAmount": 120.00,
    "responseTimeDays": 3,
    "claimDateUtc": "2026-05-20T00:00:00Z"
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` (`insurancePublicId`, `accountNumber`, `claimTypeCode`, `diagnosis`, `claimAmount`, `currencyCode`, `paidAmount`, `responseTimeDays`, `notes`, `claimDateUtc`, `sourceSystem`, `sourceReference`, `sourceSyncedUtc`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/medical-claims/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "e1f2...a3"' \
  -d '[
    { "op": "replace", "path": "/paidAmount", "value": 120.00 },
    { "op": "replace", "path": "/responseTimeDays", "value": 3 }
  ]'
```

**Respuesta `200`** — `MedicalClaimResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `404`.

---

## `DELETE` Eliminar un reclamo

`DELETE /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del reclamo.

**Respuesta `200`** — `{ "parentConcurrencyToken": "..." }` (el token del archivo padre tras quitar el ítem; ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/medical-claims/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "e1f2...a3"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "f3a4...b5" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.

---

> El `concurrencyToken` cambia con cada escritura exitosa: usá siempre el último (del body o del header `ETag`) para la próxima operación.
