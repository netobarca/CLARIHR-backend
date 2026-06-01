# Reporting — Consultas, exportación y analíticas (scoped por compañía)

Endpoints de **lectura/reportería** sobre los archivos de personal de **una compañía**. A diferencia de los sub‑recursos, estos cuelgan de la compañía (`/companies/{companyPublicId}/personnel-files/...`), no de un archivo concreto. Cubren: consulta dinámica (filtros/agrupación/orden + paginación), exportación a archivo (descarga sincrónica) y un resumen analítico de conteos agregados.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `publicId`, paginación, errores). Acá solo se documenta lo específico de estos endpoints.

**Permisos:** **todos son de lectura** → requieren `PersonnelFiles.Read`. (El `POST dynamic-query` usa `POST` por practicidad — manda filtros en el body —, pero **no muta**: solo necesita permiso de lectura.)

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `POST` | `/api/v1/companies/{companyPublicId}/personnel-files/dynamic-query` | Consulta dinámica (filtros/agrupación/orden + paginación) |
| `GET`  | `/api/v1/companies/{companyPublicId}/personnel-files/export` | Exportar a archivo (descarga sincrónica) |
| `GET`  | `/api/v1/companies/{companyPublicId}/personnel-files/analytics/summary` | Resumen analítico (conteos agregados) |

**Path params:** `companyPublicId` (uuid) = compañía (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

---

## `POST` Consulta dinámica

`POST /api/v1/companies/{companyPublicId}/personnel-files/dynamic-query` · `Content-Type: application/json`.

Búsqueda y agregación de solo lectura sobre los archivos de la compañía: filtros por campo arbitrario, agrupación, orden, texto libre (`q`) y paginación. Sujeto a rate limit (`429`).

**Body** (`application/json`, todos los campos opcionales):

| Campo | Tipo | Notas |
|-------|------|-------|
| `filters` | array de `filter` (nullable) | Condiciones por campo (ver `filter` abajo). |
| `groupBy` | array de string (nullable) | Campos por los que agrupar; devuelve `groups` con buckets contados. |
| `sort` | array de `sort` (nullable) | Criterios de orden (ver `sort` abajo). |
| `q` | string (nullable) | Texto libre sobre el nombre completo (mín. 2 caracteres, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). |
| `page` | int | 1‑based, default `1`. |
| `pageSize` | int | Default `20`, **máx `100`**. |
| `includeAllowedActions` | boolean | Incluye por ítem las acciones que el usuario puede ejecutar. Default `false`. |

`filter` (`DynamicPersonnelFileFilterRequest`):

| Campo | Tipo | Notas |
|-------|------|-------|
| `field` | string | Campo a filtrar. |
| `operator` | string | Operador de comparación (p. ej. igualdad, rango, pertenencia). |
| `value` | string (nullable) | Valor único. |
| `valueTo` | string (nullable) | Cota superior para operadores de rango. |
| `values` | array de string (nullable) | Conjunto de valores para operadores de pertenencia. |

`sort` (`DynamicPersonnelFileSortRequest`):

| Campo | Tipo | Notas |
|-------|------|-------|
| `field` | string | Campo de orden. |
| `direction` | enum (`Asc`/`Desc`) | Default `Asc`. |

**Respuesta `200`** — `PersonnelFileDynamicQueryResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `items` | array de `PersonnelFileListItemResponse` (nullable) | Página de archivos (mismo shape que el listado de [personnel-files.md](./personnel-files.md)). |
| `groups` | array de `group` (nullable) | Agrupaciones cuando se usó `groupBy`. |
| `totalCount` | int | Total de filas que matchean (sin paginar). |
| `pageNumber` | int | Página devuelta. |
| `pageSize` | int | Tamaño de página. |

Cada `group` (`PersonnelFileDynamicGroupResponse`): `field` (string) + `buckets` (array de `{ key, label, count }`).

```bash
curl -X POST "$BASE/api/v1/companies/$COMPANY/personnel-files/dynamic-query" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "filters": [
      { "field": "recordType", "operator": "eq", "value": "Employee" },
      { "field": "age", "operator": "between", "value": "25", "valueTo": "40" }
    ],
    "groupBy": ["orgUnit"],
    "sort": [ { "field": "fullName", "direction": "Asc" } ],
    "q": "men",
    "page": 1,
    "pageSize": 20,
    "includeAllowedActions": true
  }'
```

```jsonc
// 200 OK
{
  "items": [
    {
      "publicId": "3d9e...05",
      "companyPublicId": "659c...05",
      "recordType": "Employee",
      "lifecycleStatus": "Completed",
      "fullName": "Lucía Méndez",
      "age": 34,
      "orgUnitPublicId": "0b1d...e9",
      "isActive": true,
      "allowedActions": { "canRead": true, "canManage": true }
    }
  ],
  "groups": [
    {
      "field": "orgUnit",
      "buckets": [ { "key": "0b1d...e9", "label": "Tecnología", "count": 12 } ]
    }
  ],
  "totalCount": 12,
  "pageNumber": 1,
  "pageSize": 20
}
```

**Errores:** `400` (validación / `q` < 2 / `pageSize` fuera de rango), `401`, `403`, `422` (campo/operador no soportado), `429` (rate limit).

---

## `GET` Exportar (descarga sincrónica)

`GET /api/v1/companies/{companyPublicId}/personnel-files/export`

Devuelve los archivos filtrados como **archivo descargable** (default `xlsx`, también `csv`). Es la vía **sincrónica**, acotada por un límite de filas: si el resultado filtrado lo supera, responde **`413`**. Para volúmenes mayores, usá un job de exportación asíncrono (`POST /api/v1/companies/{companyPublicId}/report-export-jobs` con `resourceKey=PERSONNEL_FILES`, luego `GET /api/v1/report-export-jobs/{jobId}` y descargá el artefacto). Sujeto a rate limit (`429`).

**Query params** (todos opcionales salvo `format` que tiene default):

| Param | Tipo | Notas |
|-------|------|-------|
| `format` | string | `xlsx` (default) o `csv`. |
| `isActive` | boolean | |
| `recordType` | enum string | |
| `orgUnitPublicId` | uuid | |
| `minAge` / `maxAge` | int | |
| `maritalStatus` / `nationality` / `profession` | string | |
| `createdFromUtc` / `createdToUtc` | date-time | Rango de creación. |
| `q` | string | Texto libre sobre el nombre completo. |
| `sortBy` | string | Campo de orden permitido. |
| `sortDirection` | enum (`Asc`/`Desc`) | Default `Asc`. |

**Respuesta `200`** — el **archivo binario** (no JSON): `Content-Type` según `format` (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` para `xlsx`, `text/csv` para `csv`) y `Content-Disposition: attachment`.

```bash
curl "$BASE/api/v1/companies/$COMPANY/personnel-files/export?format=xlsx&isActive=true&recordType=Employee" \
  -H "Authorization: Bearer $TOKEN" \
  -o personnel-files.xlsx
```

**Errores:** `400` (`format` inválido / parámetros), `401`, `403`, `413` (el resultado excede el límite sincrónico — usá el job asíncrono).

---

## `GET` Resumen analítico

`GET /api/v1/companies/{companyPublicId}/personnel-files/analytics/summary`

Devuelve **conteos agregados** de solo lectura de los archivos de la compañía (totales + desgloses por tipo de registro, rango de edad y unidad organizativa). Honra los mismos filtros que el listado/exportación.

**Query params** (todos opcionales):

| Param | Tipo | Notas |
|-------|------|-------|
| `isActive` | boolean | |
| `recordType` | enum string | |
| `orgUnitPublicId` | uuid | |
| `minAge` / `maxAge` | int | |
| `q` | string | Texto libre sobre el nombre completo. |

**Respuesta `200`** — `PersonnelFileAnalyticsSummaryResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `totalCount` | int | Total de archivos que matchean. |
| `activeCount` | int | Cuántos están activos. |
| `inactiveCount` | int | Cuántos están inactivos. |
| `byRecordType` | array de `breakdown` | Desglose por tipo de registro. |
| `byAgeRange` | array de `breakdown` | Desglose por rango de edad. |
| `byOrgUnit` | array de `breakdown` | Desglose por unidad organizativa. |

Cada `breakdown` (`PersonnelFileAnalyticsBreakdownResponse`): `{ key, label, count }`.

```bash
curl "$BASE/api/v1/companies/$COMPANY/personnel-files/analytics/summary?isActive=true" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
{
  "totalCount": 137,
  "activeCount": 120,
  "inactiveCount": 17,
  "byRecordType": [ { "key": "Employee", "label": "Empleado", "count": 120 } ],
  "byAgeRange": [ { "key": "25-34", "label": "25 a 34", "count": 58 } ],
  "byOrgUnit": [ { "key": "0b1d...e9", "label": "Tecnología", "count": 24 } ]
}
```

**Errores:** `400` (parámetros), `401`, `403`.
