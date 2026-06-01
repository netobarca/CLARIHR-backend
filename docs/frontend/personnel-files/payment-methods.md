# Payment Methods — Sub‑recurso de compensación

Los **payment methods** (métodos de pago) definen **cómo** se le paga a un empleado (transferencia, cheque, efectivo, etc.) y, opcionalmente, a qué cuenta bancaria se dirige el pago, con su rango de vigencia.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> **Solo sobre archivos finalizados.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) requieren un archivo de personal **finalizado** (`Completed`, empleado). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/payment-methods` | Listar los métodos de pago del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/payment-methods` | Agregar un método de pago |
| `GET`    | `/api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` | Obtener un método por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` | Reemplazar un método |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` | Cambios parciales sobre un método |
| `DELETE` | `/api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` | Eliminar un método |

`{publicId}` = id del archivo de personal padre. `{paymentMethodPublicId}` = id del método de pago.

---

## `GET` Listar métodos de pago

`GET /api/v1/personnel-files/{publicId}/payment-methods`

Devuelve el **array completo** de métodos del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `PaymentMethodResponse`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/payment-methods" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un método de pago

`POST /api/v1/personnel-files/{publicId}/payment-methods`

Crea un método de pago. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `paymentMethodCode` | string (nullable) | no | Código de catálogo: método de pago. |
| `bankAccountPublicId` | uuid (nullable) | no | Cuenta bancaria destino (ver [bank-accounts.md](./bank-accounts.md)). |
| `isPrimary` | boolean | sí | Si es el método principal. |
| `isActive` | boolean | sí | Si el método está activo. |
| `effectiveFromUtc` | string (date-time) | sí | Inicio de vigencia. |
| `effectiveToUtc` | string (date-time, nullable) | no | Fin de vigencia (abierto si se omite). |
| `notes` | string (nullable) | no | Notas libres. |

**Respuesta `201`** — `PaymentMethodResponse` (+ headers `Location` y `ETag`):

| Campo | Tipo |
|-------|------|
| `paymentMethodPublicId` | uuid |
| `paymentMethodCode` | string (nullable) |
| `bankAccountPublicId` | uuid (nullable) |
| `isPrimary` | boolean |
| `isActive` | boolean |
| `effectiveFromUtc` | string (date-time) |
| `effectiveToUtc` | string (date-time, nullable) |
| `notes` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/payment-methods" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "paymentMethodCode": "TRANSFERENCIA",
    "bankAccountPublicId": "2b6e...d9",
    "isPrimary": true,
    "isActive": true,
    "effectiveFromUtc": "2026-06-01T00:00:00Z",
    "notes": "Depósito a cuenta principal"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{id}/payment-methods/5d8f...b1   ETag: "c9d0...e1"
{
  "paymentMethodPublicId": "5d8f...b1",
  "paymentMethodCode": "TRANSFERENCIA",
  "bankAccountPublicId": "2b6e...d9",
  "isPrimary": true,
  "isActive": true,
  "effectiveFromUtc": "2026-06-01T00:00:00Z",
  "effectiveToUtc": null,
  "notes": "Depósito a cuenta principal",
  "concurrencyToken": "c9d0...e1"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Obtener un método por id

`GET /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` → `200` `PaymentMethodResponse` (mismos campos que el `201`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/payment-methods/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un método

`PUT /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` · **requiere `If-Match`** con el `concurrencyToken` **del método** (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza todos los campos de negocio. Body = mismo shape que el `POST`.

**Respuesta `200`** — `PaymentMethodResponse` (con el `concurrencyToken` nuevo + header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/payment-methods/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "c9d0...e1"' \
  -d '{
    "paymentMethodCode": "TRANSFERENCIA",
    "bankAccountPublicId": "2b6e...d9",
    "isPrimary": true,
    "isActive": true,
    "effectiveFromUtc": "2026-06-01T00:00:00Z",
    "effectiveToUtc": "2026-12-31T00:00:00Z",
    "notes": "Vigencia acotada al ejercicio"
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` (`paymentMethodCode`, `bankAccountPublicId`, `isPrimary`, `isActive`, `effectiveFromUtc`, `effectiveToUtc`, `notes`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/payment-methods/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "c9d0...e1"' \
  -d '[
    { "op": "replace", "path": "/isPrimary", "value": false },
    { "op": "replace", "path": "/effectiveToUtc", "value": "2026-09-30T00:00:00Z" }
  ]'
```

**Respuesta `200`** — `PaymentMethodResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `404`.

---

## `DELETE` Eliminar un método

`DELETE /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del método.

**Respuesta `200`** — `{ "parentConcurrencyToken": "..." }` (el token del archivo padre tras quitar el ítem; ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/payment-methods/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "c9d0...e1"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "d1e2...f3" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.

---

> El `concurrencyToken` cambia con cada escritura exitosa: usá siempre el último (del body o del header `ETag`) para la próxima operación.
