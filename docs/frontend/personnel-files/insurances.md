# Insurances (y beneficiarios) — Sub‑recurso de compensación

Los **insurances** (seguros) son las pólizas/coberturas asociadas a un empleado dentro de su archivo de personal (contribuciones empleado/empleador, rango, póliza, monto asegurado, vigencia). Cada seguro tiene un sub‑recurso **anidado** de **beneficiarios** (`beneficiaries`).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> **Solo sobre archivos finalizados.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`), tanto del seguro como de sus beneficiarios, requieren un archivo de personal **finalizado** (`Completed`, empleado). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

### Seguros

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/insurances` | Listar los seguros del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/insurances` | Agregar un seguro |
| `GET`    | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` | Obtener un seguro por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` | Reemplazar un seguro |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` | Cambios parciales sobre un seguro |
| `DELETE` | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` | Eliminar un seguro |

### Beneficiarios (anidados bajo un seguro)

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries` | Listar los beneficiarios del seguro |
| `POST`   | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries` | Agregar un beneficiario |
| `GET`    | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` | Obtener un beneficiario por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` | Reemplazar un beneficiario |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` | Cambios parciales sobre un beneficiario |
| `DELETE` | `/api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` | Eliminar un beneficiario |

`{publicId}` = id del archivo padre. `{insurancePublicId}` = id del seguro. `{beneficiaryPublicId}` = id del beneficiario.

---

# Parte 1 — Seguros

## `GET` Listar seguros

`GET /api/v1/personnel-files/{publicId}/insurances`

Devuelve el **array completo** de seguros del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). Cada seguro incluye su array de `beneficiaries` y su propio `concurrencyToken`.

**Respuesta `200`** — array de `InsuranceResponse` (ver tabla abajo).

```bash
curl "$BASE/api/v1/personnel-files/$ID/insurances" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un seguro

`POST /api/v1/personnel-files/{publicId}/insurances`

Crea un seguro. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)). Los beneficiarios se agregan después por su propio endpoint.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `insuranceCode` | string (nullable) | no | Código de catálogo: tipo de seguro. |
| `employeeContribution` | number (double, nullable) | no | Aporte del empleado. |
| `employerContribution` | number (double, nullable) | no | Aporte del empleador. |
| `rangeCode` | string (nullable) | no | Código de catálogo: rango/tramo. |
| `policyNumber` | string (nullable) | no | Número de póliza. |
| `insuredAmount` | number (double, nullable) | no | Monto asegurado. |
| `currencyCode` | string (nullable) | no | Código de moneda ISO (p. ej. `USD`). |
| `isActive` | boolean | sí | Si el seguro está activo. |
| `startDateUtc` | string (date-time, nullable) | no | Inicio de vigencia. |
| `endDateUtc` | string (date-time, nullable) | no | Fin de vigencia. |

**Respuesta `201`** — `InsuranceResponse` (+ headers `Location` y `ETag`):

| Campo | Tipo |
|-------|------|
| `insurancePublicId` | uuid |
| `insuranceCode` | string (nullable) |
| `employeeContribution` | number (double, nullable) |
| `employerContribution` | number (double, nullable) |
| `rangeCode` | string (nullable) |
| `policyNumber` | string (nullable) |
| `insuredAmount` | number (double, nullable) |
| `currencyCode` | string (nullable) |
| `isActive` | boolean |
| `startDateUtc` | string (date-time, nullable) |
| `endDateUtc` | string (date-time, nullable) |
| `beneficiaries` | array de `BeneficiaryResponse` (nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/insurances" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "insuranceCode": "SEGURO_MEDICO",
    "employeeContribution": 25.00,
    "employerContribution": 75.00,
    "rangeCode": "PLAN_FAMILIAR",
    "policyNumber": "POL-99887",
    "insuredAmount": 50000.00,
    "currencyCode": "USD",
    "isActive": true,
    "startDateUtc": "2026-06-01T00:00:00Z"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{id}/insurances/8e3a...c4   ETag: "a4b5...c6"
{
  "insurancePublicId": "8e3a...c4",
  "insuranceCode": "SEGURO_MEDICO",
  "employeeContribution": 25.00,
  "employerContribution": 75.00,
  "rangeCode": "PLAN_FAMILIAR",
  "policyNumber": "POL-99887",
  "insuredAmount": 50000.00,
  "currencyCode": "USD",
  "isActive": true,
  "startDateUtc": "2026-06-01T00:00:00Z",
  "endDateUtc": null,
  "beneficiaries": [],
  "concurrencyToken": "a4b5...c6"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Obtener un seguro por id

`GET /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` → `200` `InsuranceResponse` (mismos campos que el `201`, incluye `beneficiaries`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` del seguro.

```bash
curl "$BASE/api/v1/personnel-files/$ID/insurances/$INS" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un seguro

`PUT /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` · **requiere `If-Match`** con el `concurrencyToken` **del seguro** (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza los campos de negocio del seguro. Los beneficiarios se gestionan por su propio endpoint, **no** por acá. Body = mismo shape que el `POST`.

**Respuesta `200`** — `InsuranceResponse` (con el `concurrencyToken` nuevo + header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/insurances/$INS" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a4b5...c6"' \
  -d '{
    "insuranceCode": "SEGURO_MEDICO",
    "employeeContribution": 30.00,
    "employerContribution": 70.00,
    "rangeCode": "PLAN_FAMILIAR",
    "policyNumber": "POL-99887",
    "insuredAmount": 60000.00,
    "currencyCode": "USD",
    "isActive": true,
    "startDateUtc": "2026-06-01T00:00:00Z",
    "endDateUtc": "2027-05-31T00:00:00Z"
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `PATCH` Cambios parciales (seguro)

`PATCH /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` (`insuranceCode`, `employeeContribution`, `employerContribution`, `rangeCode`, `policyNumber`, `insuredAmount`, `currencyCode`, `isActive`, `startDateUtc`, `endDateUtc`). El array `beneficiaries` no se parcha acá.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/insurances/$INS" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a4b5...c6"' \
  -d '[
    { "op": "replace", "path": "/insuredAmount", "value": 60000.00 },
    { "op": "replace", "path": "/isActive", "value": false }
  ]'
```

**Respuesta `200`** — `InsuranceResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `404`.

---

## `DELETE` Eliminar un seguro

`DELETE /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}` · **requiere `If-Match`** con el `concurrencyToken` del seguro.

**Respuesta `200`** — `{ "parentConcurrencyToken": "..." }` (el token del archivo padre tras quitar el seguro; ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/insurances/$INS" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a4b5...c6"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "b6c7...d8" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.

---

# Parte 2 — Beneficiarios (anidados bajo un seguro)

Los **beneficiarios** son las personas designadas en un seguro. Viven bajo la ruta del seguro y siguen las **mismas** reglas de concurrencia: cada beneficiario tiene su propio `concurrencyToken`, y `PUT`/`PATCH`/`DELETE` requieren `If-Match` con el token **del beneficiario** (no el del seguro ni el del archivo).

## `GET` Listar beneficiarios

`GET /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries`

Devuelve el **array completo** de beneficiarios del seguro (no paginado). Es el mismo contenido que el campo `beneficiaries` del seguro.

**Respuesta `200`** — array de `BeneficiaryResponse` (ver tabla abajo).

```bash
curl "$BASE/api/v1/personnel-files/$ID/insurances/$INS/beneficiaries" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un beneficiario

`POST /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries`

Crea un beneficiario en el seguro. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `fullName` | string (nullable) | no | Nombre completo del beneficiario. |
| `documentNumber` | string (nullable) | no | Número de documento. |
| `birthDate` | string (date-time, nullable) | no | Fecha de nacimiento. |
| `kinshipCode` | string (nullable) | no | Código de catálogo: parentesco. |

**Respuesta `201`** — `BeneficiaryResponse` (+ headers `Location` y `ETag`):

| Campo | Tipo |
|-------|------|
| `beneficiaryPublicId` | uuid |
| `fullName` | string (nullable) |
| `documentNumber` | string (nullable) |
| `birthDate` | string (date-time, nullable) |
| `kinshipCode` | string (nullable) |
| `isActive` | boolean |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/insurances/$INS/beneficiaries" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Mateo Pérez",
    "documentNumber": "0801-2015-12345",
    "birthDate": "2015-09-10T00:00:00Z",
    "kinshipCode": "HIJO_A"
  }'
```

```jsonc
// 201 Created   Location: .../insurances/8e3a...c4/beneficiaries/3c7d...e5   ETag: "c8d9...e0"
{
  "beneficiaryPublicId": "3c7d...e5",
  "fullName": "Mateo Pérez",
  "documentNumber": "0801-2015-12345",
  "birthDate": "2015-09-10T00:00:00Z",
  "kinshipCode": "HIJO_A",
  "isActive": true,
  "concurrencyToken": "c8d9...e0"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Obtener un beneficiario por id

`GET /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` → `200` `BeneficiaryResponse` (mismos campos que el `201`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` del beneficiario.

```bash
curl "$BASE/api/v1/personnel-files/$ID/insurances/$INS/beneficiaries/$BEN" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un beneficiario

`PUT /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` · **requiere `If-Match`** con el `concurrencyToken` **del beneficiario**.

Reemplaza todos los campos de negocio del beneficiario. Body = mismo shape que el `POST`.

**Respuesta `200`** — `BeneficiaryResponse` (con el `concurrencyToken` nuevo + header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/insurances/$INS/beneficiaries/$BEN" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "c8d9...e0"' \
  -d '{
    "fullName": "Mateo Pérez López",
    "documentNumber": "0801-2015-12345",
    "birthDate": "2015-09-10T00:00:00Z",
    "kinshipCode": "HIJO_A"
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `PATCH` Cambios parciales (beneficiario)

`PATCH /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` (`fullName`, `documentNumber`, `birthDate`, `kinshipCode`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/insurances/$INS/beneficiaries/$BEN" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "c8d9...e0"' \
  -d '[
    { "op": "replace", "path": "/kinshipCode", "value": "HIJASTRO_A" }
  ]'
```

**Respuesta `200`** — `BeneficiaryResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `404`.

---

## `DELETE` Eliminar un beneficiario

`DELETE /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del beneficiario.

**Respuesta `200`** — `{ "parentConcurrencyToken": "..." }`. Acá el "padre" es el **seguro**: el token devuelto es el del seguro tras quitar el beneficiario (usalo si vas a seguir mutando el seguro).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/insurances/$INS/beneficiaries/$BEN" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "c8d9...e0"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "d0e1...f2" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.

---

> El `concurrencyToken` cambia con cada escritura exitosa: usá siempre el último (del body o del header `ETag`) para la próxima operación. Recordá que el seguro y cada beneficiario tienen tokens **independientes**.
> Los reclamos médicos pueden referenciar un seguro por su `insurancePublicId`; ver [medical-claims.md](./medical-claims.md).
