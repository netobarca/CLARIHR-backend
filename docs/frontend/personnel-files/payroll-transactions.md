# Payroll Transactions — Sub‑recurso de compensación

Las **payroll transactions** (transacciones de nómina) son los movimientos de nómina de un empleado (devengos y deducciones) dentro de su archivo de personal. Son **registros de auditoría inmutables**: una vez creados, **solo** se puede parchar su `isActive`; no admiten reemplazo (`PUT`) ni borrado físico (`DELETE`).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> **Solo sobre archivos finalizados.** Las escrituras (`POST`/`PATCH`) y también la búsqueda/exportación requieren un archivo de personal **finalizado** (`Completed`, empleado). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PATCH` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`   | `/api/v1/personnel-files/{publicId}/payroll-transactions` | Buscar transacciones (**paginado**, con filtros) |
| `POST`  | `/api/v1/personnel-files/{publicId}/payroll-transactions` | Agregar una transacción |
| `GET`   | `/api/v1/personnel-files/{publicId}/payroll-transactions/export` | Exportar transacciones a archivo (CSV/XLSX/JSON) |
| `GET`   | `/api/v1/personnel-files/{publicId}/payroll-transactions/{payrollTransactionPublicId}` | Obtener una transacción por id |
| `PATCH` | `/api/v1/personnel-files/{publicId}/payroll-transactions/{payrollTransactionPublicId}` | Cambios parciales (en la práctica, `isActive`) |

`{publicId}` = id del archivo de personal padre. `{payrollTransactionPublicId}` = id de la transacción. **No hay `PUT` ni `DELETE`** para este recurso.

---

## `GET` Buscar transacciones (paginado)

`GET /api/v1/personnel-files/{publicId}/payroll-transactions`

A diferencia del resto de sub‑recursos, esta lista **sí es paginada y filtrable** (ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). Cada ítem trae su propio `concurrencyToken`.

**Query params** (todos opcionales):

| Param | Tipo | Notas |
|-------|------|-------|
| `fromUtc` | date-time | Filtra por fecha de transacción ≥. |
| `toUtc` | date-time | Filtra por fecha de transacción ≤. |
| `type` | string | Código de tipo de transacción. |
| `status` | string | Estado de la transacción. |
| `q` | string | Texto libre (p. ej. sobre la descripción). |
| `sortBy` | string | Campo de orden permitido. |
| `sortDirection` | enum (`Asc`/`Desc`) | Default `Asc`. |
| `page` | int | 1‑based, default `1`. |
| `pageSize` | int | Default `20`, **máx `100`**. |

**Respuesta `200`** — `PagedResponse<PayrollTransactionResponse>` (forma estándar `items` / `pageNumber` / `pageSize` / `totalCount`):

```jsonc
{
  "items": [
    {
      "payrollTransactionPublicId": "9b2c...f1",
      "transactionTypeCode": "DEVENGO_SALARIO",
      "transactionDateUtc": "2026-05-30T00:00:00Z",
      "payrollPeriodCode": "2026-05",
      "description": "Salario mensual mayo",
      "amount": 1850.00,
      "currencyCode": "USD",
      "isDebit": false,
      "sourceSystem": "PAYROLL_CORE",
      "sourceReference": "TXN-0001",
      "sourceSyncedUtc": "2026-05-30T06:00:00Z",
      "createdAtUtc": "2026-05-30T06:00:01Z",
      "modifiedAtUtc": null,
      "isActive": true,
      "concurrencyToken": "e2f3...a4"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 42
}
```

```bash
curl "$BASE/api/v1/personnel-files/$ID/payroll-transactions?fromUtc=2026-05-01T00:00:00Z&type=DEVENGO_SALARIO&page=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `400` (`pageSize` fuera de rango / filtros inválidos), `401`, `403`, `404`, `422` (archivo en `Draft`).

---

## `POST` Agregar una transacción

`POST /api/v1/personnel-files/{publicId}/payroll-transactions`

Crea una transacción de nómina. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `transactionTypeCode` | string (nullable) | no | Código de catálogo: tipo de transacción. |
| `transactionDateUtc` | string (date-time) | sí | Fecha de la transacción. |
| `payrollPeriodCode` | string (nullable) | no | Código del período de nómina (p. ej. `2026-05`). |
| `description` | string (nullable) | no | Descripción. |
| `amount` | number (double) | sí | Monto. |
| `currencyCode` | string (nullable) | no | Código de moneda ISO (p. ej. `USD`). |
| `isDebit` | boolean | sí | `true` = deducción/débito; `false` = devengo/crédito. |
| `sourceSystem` | string (nullable) | no | Sistema de origen (integración externa). |
| `sourceReference` | string (nullable) | no | Referencia en el sistema de origen. |
| `sourceSyncedUtc` | string (date-time, nullable) | no | Última sincronización desde el origen. |

**Respuesta `201`** — `PayrollTransactionResponse` (+ headers `Location` y `ETag`):

| Campo | Tipo |
|-------|------|
| `payrollTransactionPublicId` | uuid |
| `transactionTypeCode` | string (nullable) |
| `transactionDateUtc` | string (date-time) |
| `payrollPeriodCode` | string (nullable) |
| `description` | string (nullable) |
| `amount` | number (double) |
| `currencyCode` | string (nullable) |
| `isDebit` | boolean |
| `sourceSystem` | string (nullable) |
| `sourceReference` | string (nullable) |
| `sourceSyncedUtc` | string (date-time, nullable) |
| `createdAtUtc` | string (date-time) |
| `modifiedAtUtc` | string (date-time, nullable) |
| `isActive` | boolean |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/payroll-transactions" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "transactionTypeCode": "DEVENGO_SALARIO",
    "transactionDateUtc": "2026-05-30T00:00:00Z",
    "payrollPeriodCode": "2026-05",
    "description": "Salario mensual mayo",
    "amount": 1850.00,
    "currencyCode": "USD",
    "isDebit": false
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{id}/payroll-transactions/9b2c...f1   ETag: "e2f3...a4"
{
  "payrollTransactionPublicId": "9b2c...f1",
  "transactionTypeCode": "DEVENGO_SALARIO",
  "transactionDateUtc": "2026-05-30T00:00:00Z",
  "payrollPeriodCode": "2026-05",
  "description": "Salario mensual mayo",
  "amount": 1850.00,
  "currencyCode": "USD",
  "isDebit": false,
  "sourceSystem": null,
  "sourceReference": null,
  "sourceSyncedUtc": null,
  "createdAtUtc": "2026-05-30T06:00:01Z",
  "modifiedAtUtc": null,
  "isActive": true,
  "concurrencyToken": "e2f3...a4"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Exportar transacciones

`GET /api/v1/personnel-files/{publicId}/payroll-transactions/export`

Descarga la lista **filtrada** como archivo. Acepta los **mismos filtros** que la búsqueda (`fromUtc`, `toUtc`, `type`, `status`, `q`, `sortBy`, `sortDirection`) más el formato. Sujeto a rate limit y a límite de tamaño (`413`).

**Query params** (todos opcionales):

| Param | Tipo | Notas |
|-------|------|-------|
| `format` | string | `csv`, `xlsx` o `json`. Default `xlsx`. |
| `fromUtc` / `toUtc` | date-time | Mismo rango de fecha que la búsqueda. |
| `type` | string | Código de tipo. |
| `status` | string | Estado. |
| `q` | string | Texto libre. |
| `sortBy` | string | Campo de orden. |
| `sortDirection` | enum (`Asc`/`Desc`) | Default `Asc`. |

**Respuesta `200`** — stream binario del archivo. El `Content-Type` depende del formato: `text/csv`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (xlsx) o `application/json`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/payroll-transactions/export?format=xlsx&fromUtc=2026-05-01T00:00:00Z" \
  -H "Authorization: Bearer $TOKEN" \
  -o payroll-mayo.xlsx
```

**Errores:** `400` (filtros/formato inválido), `401`, `403`, `404`, `413` (export demasiado grande), `422` (archivo en `Draft`).

---

## `GET` Obtener una transacción por id

`GET /api/v1/personnel-files/{publicId}/payroll-transactions/{payrollTransactionPublicId}` → `200` `PayrollTransactionResponse` (mismos campos que el `201`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para el `PATCH`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/payroll-transactions/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/payroll-transactions/{payrollTransactionPublicId}` · **requiere `If-Match`** con el `concurrencyToken` **de la transacción** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Por ser registros de auditoría **inmutables**, en la práctica solo se parcha el flag **`isActive`** (para anular/reactivar). Intentar mutar otros campos puede rechazarse con `422`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/payroll-transactions/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "e2f3...a4"' \
  -d '[
    { "op": "replace", "path": "/isActive", "value": false }
  ]'
```

**Respuesta `200`** — `PayrollTransactionResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409` (token desactualizado), `422` (archivo en `Draft` / campo no mutable), `404`.

---

> No hay `PUT` ni `DELETE`: las transacciones son inmutables. Para "quitar" una, se anula con `PATCH` sobre `isActive`.
> El `concurrencyToken` cambia con cada `PATCH` exitoso: usá siempre el último (del body o del header `ETag`) para la próxima operación.
