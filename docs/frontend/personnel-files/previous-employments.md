# Previous Employments — Empleos anteriores del archivo de personal

Sub‑recurso que registra la **trayectoria laboral previa** de la persona: empresas donde trabajó, cargo, jefe, fechas, motivo de salida y datos salariales históricos. Cuelga de un archivo de personal ya existente.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este sub‑recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/previous-employments` | Listar todos los empleos anteriores del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/previous-employments` | Agregar un empleo anterior |
| `GET`    | `/api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` | Obtener un empleo anterior por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` | Reemplazar un empleo anterior |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` | Quitar un empleo anterior |

Los ids van como GUIDs `publicId` (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)). El id del archivo padre es `{publicId}`; el de cada ítem es `{previousEmploymentPublicId}`.

---

## `GET` Listar empleos anteriores

`GET /api/v1/personnel-files/{publicId}/previous-employments`

Devuelve el **array completo** (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)) de los empleos anteriores del archivo.

**Respuesta `200`** — array de `PersonnelFilePreviousEmploymentResponse` (campos en la tabla del `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/previous-employments" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404` (el archivo no existe en esta compañía).

---

## `POST` Agregar un empleo anterior

`POST /api/v1/personnel-files/{publicId}/previous-employments`

Crea un ítem nuevo. **No** lleva `If-Match`. Responde `201` con el ítem creado, el header `Location` y el header `ETag` con su `concurrencyToken` inicial (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `institution` | string | no | Empresa / institución. |
| `place` | string | no | Lugar / ubicación. |
| `lastPosition` | string | no | Último cargo ocupado. |
| `managerName` | string | no | Nombre del jefe directo. |
| `entryDate` | string (date-time) | sí | Fecha de ingreso. |
| `retirementDate` | string (date-time) | no | Fecha de retiro/salida. |
| `companyPhone` | string | no | Teléfono de la empresa. |
| `exitReason` | string | no | Motivo de salida. |
| `firstSalaryAmount` | number (double) | no | Salario inicial. |
| `lastSalaryAmount` | number (double) | no | Salario final. |
| `averageCommissionAmount` | number (double) | no | Comisión promedio. |
| `currencyCode` | string | no | Código de moneda (p. ej. `USD`). |

> Las fechas viajan como `date-time` ISO‑8601 (p. ej. `2020-01-15T00:00:00Z`).

**Respuesta `201`** — `PersonnelFilePreviousEmploymentResponse` (mismos campos que el `GET` por id, abajo).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/previous-employments" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "institution": "Distribuidora del Norte",
    "place": "San Salvador",
    "lastPosition": "Analista Senior",
    "managerName": "Carla Funes",
    "entryDate": "2019-03-01T00:00:00Z",
    "retirementDate": "2023-08-31T00:00:00Z",
    "companyPhone": "+503 2500-0000",
    "exitReason": "Mejor oportunidad",
    "firstSalaryAmount": 950.00,
    "lastSalaryAmount": 1400.00,
    "currencyCode": "USD"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/3d9e...05/previous-employments/c47d...19   ETag: "a1b2...c3"
{
  "previousEmploymentPublicId": "c47d...19",
  "institution": "Distribuidora del Norte",
  "place": "San Salvador",
  "lastPosition": "Analista Senior",
  "managerName": "Carla Funes",
  "entryDate": "2019-03-01T00:00:00Z",
  "retirementDate": "2023-08-31T00:00:00Z",
  "companyPhone": "+503 2500-0000",
  "exitReason": "Mejor oportunidad",
  "firstSalaryAmount": 950.00,
  "lastSalaryAmount": 1400.00,
  "averageCommissionAmount": null,
  "currencyCode": "USD",
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` → `200` `PersonnelFilePreviousEmploymentResponse`. El `concurrencyToken` que devuelve (también en `ETag`) es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

**Respuesta `200`:**

| Campo | Tipo |
|-------|------|
| `previousEmploymentPublicId` | uuid |
| `institution` | string (nullable) |
| `place` | string (nullable) |
| `lastPosition` | string (nullable) |
| `managerName` | string (nullable) |
| `entryDate` | string (date-time) |
| `retirementDate` | string (date-time, nullable) |
| `companyPhone` | string (nullable) |
| `exitReason` | string (nullable) |
| `firstSalaryAmount` | number (double, nullable) |
| `lastSalaryAmount` | number (double, nullable) |
| `averageCommissionAmount` | number (double, nullable) |
| `currencyCode` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl "$BASE/api/v1/personnel-files/$ID/previous-employments/c47d...19" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un empleo anterior

`PUT /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `PersonnelFilePreviousEmploymentResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/previous-employments/c47d...19" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "institution": "Distribuidora del Norte",
    "lastPosition": "Coordinador de Análisis",
    "entryDate": "2019-03-01T00:00:00Z",
    "retirementDate": "2023-08-31T00:00:00Z",
    "lastSalaryAmount": 1500.00,
    "currencyCode": "USD"
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/institution`, `/place`, `/lastPosition`, `/managerName`, `/entryDate`, `/retirementDate`, `/companyPhone`, `/exitReason`, `/firstSalaryAmount`, `/lastSalaryAmount`, `/averageCommissionAmount`, `/currencyCode`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/previous-employments/c47d...19" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/lastPosition", "value": "Coordinador de Análisis" },
    { "op": "replace", "path": "/lastSalaryAmount", "value": 1500.00 }
  ]'
```

**Respuesta `200`** — `PersonnelFilePreviousEmploymentResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar un empleo anterior

`DELETE /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo **padre** tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f2e1...90" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/previous-employments/c47d...19" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400` (`If-Match` faltante/malformado), `404`, `409` (token desactualizado).
