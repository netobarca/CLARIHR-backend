# Salary Items — Sub‑recurso de compensación

Los **salary items** (rubros salariales) son las líneas de ingreso que componen la compensación de un empleado dentro de su archivo de personal: cada una tiene un tipo de ingreso, una rúbrica, una moneda, una periodicidad de pago y un monto vigente en un rango de fechas.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> **Solo sobre archivos finalizados.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) requieren un archivo de personal **finalizado** (`Completed`, empleado). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/salary-items` | Listar los rubros salariales del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/salary-items` | Agregar un rubro salarial |
| `GET`    | `/api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` | Obtener un rubro por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` | Reemplazar un rubro |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` | Cambios parciales sobre un rubro |
| `DELETE` | `/api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` | Eliminar un rubro |

`{publicId}` = id del archivo de personal padre. `{salaryItemPublicId}` = id del rubro.

---

## `GET` Listar rubros salariales

`GET /api/v1/personnel-files/{publicId}/salary-items`

Devuelve el **array completo** de rubros del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `SalaryItemResponse` (ver tabla de campos abajo).

```bash
curl "$BASE/api/v1/personnel-files/$ID/salary-items" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un rubro salarial

`POST /api/v1/personnel-files/{publicId}/salary-items`

Crea un rubro. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `incomeTypeCode` | string (nullable) | no | Código de catálogo: tipo de ingreso. |
| `salaryRubricCode` | string (nullable) | no | Código de catálogo: rúbrica salarial. |
| `currencyCode` | string (nullable) | no | Código de moneda ISO (p. ej. `USD`). |
| `payPeriodCode` | string (nullable) | no | Código de catálogo: periodicidad de pago. |
| `amount` | number (double) | sí | Monto del rubro. |
| `startDate` | string (date-time) | sí | Inicio de vigencia. |
| `endDate` | string (date-time, nullable) | no | Fin de vigencia (abierto si se omite). |
| `isActive` | boolean | sí | Si el rubro está activo. |

**Respuesta `201`** — `SalaryItemResponse` (+ headers `Location` y `ETag`):

| Campo | Tipo |
|-------|------|
| `salaryItemPublicId` | uuid |
| `incomeTypeCode` | string (nullable) |
| `salaryRubricCode` | string (nullable) |
| `currencyCode` | string (nullable) |
| `payPeriodCode` | string (nullable) |
| `amount` | number (double) |
| `startDate` | string (date-time) |
| `endDate` | string (date-time, nullable) |
| `isActive` | boolean |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/salary-items" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "incomeTypeCode": "SALARIO_BASE",
    "salaryRubricCode": "SUELDO_MENSUAL",
    "currencyCode": "USD",
    "payPeriodCode": "MENSUAL",
    "amount": 1850.00,
    "startDate": "2026-06-01T00:00:00Z",
    "isActive": true
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{id}/salary-items/7a2c...e1   ETag: "b3d4...f5"
{
  "salaryItemPublicId": "7a2c...e1",
  "incomeTypeCode": "SALARIO_BASE",
  "salaryRubricCode": "SUELDO_MENSUAL",
  "currencyCode": "USD",
  "payPeriodCode": "MENSUAL",
  "amount": 1850.00,
  "startDate": "2026-06-01T00:00:00Z",
  "endDate": null,
  "isActive": true,
  "concurrencyToken": "b3d4...f5"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Obtener un rubro por id

`GET /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` → `200` `SalaryItemResponse` (mismos campos que el `201`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/salary-items/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un rubro

`PUT /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` · **requiere `If-Match`** con el `concurrencyToken` **del rubro** (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza todos los campos de negocio del rubro. Body = mismo shape que el `POST`.

**Respuesta `200`** — `SalaryItemResponse` (con el `concurrencyToken` nuevo + header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/salary-items/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "b3d4...f5"' \
  -d '{
    "incomeTypeCode": "SALARIO_BASE",
    "salaryRubricCode": "SUELDO_MENSUAL",
    "currencyCode": "USD",
    "payPeriodCode": "MENSUAL",
    "amount": 2000.00,
    "startDate": "2026-06-01T00:00:00Z",
    "endDate": "2026-12-31T00:00:00Z",
    "isActive": true
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` (`incomeTypeCode`, `salaryRubricCode`, `currencyCode`, `payPeriodCode`, `amount`, `startDate`, `endDate`, `isActive`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/salary-items/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "b3d4...f5"' \
  -d '[
    { "op": "replace", "path": "/amount", "value": 1950.00 },
    { "op": "replace", "path": "/isActive", "value": false }
  ]'
```

**Respuesta `200`** — `SalaryItemResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `404`.

---

## `DELETE` Eliminar un rubro

`DELETE /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del rubro.

**Respuesta `200`** — `{ "parentConcurrencyToken": "..." }` (el token del archivo padre tras quitar el ítem; ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/salary-items/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "b3d4...f5"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "c5e6...a7" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.

---

> El `concurrencyToken` cambia con cada escritura exitosa: usá siempre el último (del body o del header `ETag`) para la próxima operación.
