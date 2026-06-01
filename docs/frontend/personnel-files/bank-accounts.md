# Bank Accounts — Sub‑recurso de compensación

Las **bank accounts** (cuentas bancarias) son las cuentas registradas para un empleado dentro de su archivo de personal, usadas como destino de pago. Referencian un banco por `bankPublicId` y exponen datos resueltos del banco (nombre, alias, SWIFT, routing).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> **Solo sobre archivos finalizados.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) requieren un archivo de personal **finalizado** (`Completed`, empleado). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/bank-accounts` | Listar las cuentas del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/bank-accounts` | Agregar una cuenta |
| `GET`    | `/api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` | Obtener una cuenta por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` | Reemplazar una cuenta |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` | Cambios parciales sobre una cuenta |
| `DELETE` | `/api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` | Eliminar una cuenta |

`{publicId}` = id del archivo de personal padre. `{bankAccountPublicId}` = id de la cuenta.

---

## `GET` Listar cuentas

`GET /api/v1/personnel-files/{publicId}/bank-accounts`

Devuelve el **array completo** de cuentas del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `BankAccountResponse`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/bank-accounts" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una cuenta

`POST /api/v1/personnel-files/{publicId}/bank-accounts`

Crea una cuenta. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `bankPublicId` | uuid | sí | Id del banco (referencia al catálogo de bancos). |
| `currencyCode` | string (nullable) | no | Código de moneda ISO (p. ej. `USD`). |
| `accountNumber` | string (nullable) | no | Número de cuenta. |
| `accountTypeCode` | string (nullable) | no | Código de catálogo: tipo de cuenta (ahorro/corriente). |
| `isPrimary` | boolean | sí | Si es la cuenta principal del empleado. |

**Respuesta `201`** — `BankAccountResponse` (+ headers `Location` y `ETag`). Los campos `bankCode`/`bankName`/`bankAlias`/`swiftCode`/`routingCode` son **derivados** del banco referenciado (solo lectura):

| Campo | Tipo |
|-------|------|
| `bankAccountPublicId` | uuid |
| `bankPublicId` | uuid (nullable) |
| `bankCode` | string (nullable) |
| `bankName` | string (nullable) |
| `bankAlias` | string (nullable) |
| `swiftCode` | string (nullable) |
| `routingCode` | string (nullable) |
| `currencyCode` | string (nullable) |
| `accountNumber` | string (nullable) |
| `accountTypeCode` | string (nullable) |
| `isPrimary` | boolean |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/bank-accounts" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "bankPublicId": "4f1a...c8",
    "currencyCode": "USD",
    "accountNumber": "001-234567-8",
    "accountTypeCode": "CUENTA_AHORRO",
    "isPrimary": true
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{id}/bank-accounts/2b6e...d9   ETag: "f5a6...e7"
{
  "bankAccountPublicId": "2b6e...d9",
  "bankPublicId": "4f1a...c8",
  "bankCode": "BAC",
  "bankName": "Banco de América Central",
  "bankAlias": "BAC Credomatic",
  "swiftCode": "BAMCSVSS",
  "routingCode": null,
  "currencyCode": "USD",
  "accountNumber": "001-234567-8",
  "accountTypeCode": "CUENTA_AHORRO",
  "isPrimary": true,
  "concurrencyToken": "f5a6...e7"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Obtener una cuenta por id

`GET /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` → `200` `BankAccountResponse` (mismos campos que el `201`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/bank-accounts/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar una cuenta

`PUT /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` · **requiere `If-Match`** con el `concurrencyToken` **de la cuenta** (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza todos los campos de negocio (los del `POST`). Los campos derivados del banco se recalculan a partir del `bankPublicId` enviado. Body = mismo shape que el `POST`.

**Respuesta `200`** — `BankAccountResponse` (con el `concurrencyToken` nuevo + header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/bank-accounts/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "f5a6...e7"' \
  -d '{
    "bankPublicId": "4f1a...c8",
    "currencyCode": "USD",
    "accountNumber": "001-234567-8",
    "accountTypeCode": "CUENTA_CORRIENTE",
    "isPrimary": true
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` (`bankPublicId`, `currencyCode`, `accountNumber`, `accountTypeCode`, `isPrimary`). Los campos derivados del banco no se parchan directamente.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/bank-accounts/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "f5a6...e7"' \
  -d '[
    { "op": "replace", "path": "/isPrimary", "value": false },
    { "op": "replace", "path": "/accountNumber", "value": "001-999888-7" }
  ]'
```

**Respuesta `200`** — `BankAccountResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `404`.

---

## `DELETE` Eliminar una cuenta

`DELETE /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}` · **requiere `If-Match`** con el `concurrencyToken` de la cuenta.

**Respuesta `200`** — `{ "parentConcurrencyToken": "..." }` (el token del archivo padre tras quitar el ítem; ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/bank-accounts/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "f5a6...e7"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "a7b8...f9" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.

---

> El `concurrencyToken` cambia con cada escritura exitosa: usá siempre el último (del body o del header `ETag`) para la próxima operación.
> Los métodos de pago (`payment-methods`) pueden referenciar una cuenta bancaria por su `bankAccountPublicId`; ver [payment-methods.md](./payment-methods.md).
