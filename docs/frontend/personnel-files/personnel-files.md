# Personnel Files — Recurso principal (shell)

El **archivo de personal** (shell) es el registro raíz de una persona dentro de una compañía. Se crea primero y luego se le agregan sub‑recursos (identificaciones, direcciones, compensación, etc.) por sus propios endpoints.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `POST` | `/api/v1/companies/{companyPublicId}/personnel-files` | Crear un archivo (shell) |
| `GET`  | `/api/v1/companies/{companyPublicId}/personnel-files` | Buscar/listar archivos (paginado, con filtros) |
| `GET`  | `/api/v1/personnel-files/{publicId}` | Obtener un archivo por id |
| `PUT`  | `/api/v1/personnel-files/{publicId}` | Reemplazar los datos núcleo (info personal) |
| `PATCH`| `/api/v1/personnel-files/{publicId}` | Cambios parciales + activar/desactivar (`isActive`) |

Operaciones relacionadas: **finalizar** el archivo (`PATCH /personnel-files/{publicId}/finalize`) → ver [finalize.md](./finalize.md); leer la info personal completa → ver [personal-info.md](./personal-info.md).

---

## `POST` Crear un archivo

`POST /api/v1/companies/{companyPublicId}/personnel-files`

Crea el shell. Los sub‑recursos **no** se aceptan acá; se agregan después por sus endpoints. Sujeto a rate limit (`429`).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `recordType` | enum string | sí | Tipo de archivo (p. ej. `Employee`). |
| `firstName` | string | sí | |
| `lastName` | string | sí | |
| `birthDate` | string (date) | no | `YYYY-MM-DD`. |
| `maritalStatusCode` | string | no | Código de catálogo (país). |
| `professionCode` | string | no | Código de catálogo (país). |
| `nationality` | string | no | |
| `personalEmail` / `institutionalEmail` | string | no | |
| `personalPhone` / `institutionalPhone` | string | no | |
| `birthCountryCode` / `birthDepartmentCode` / `birthMunicipalityCode` | string | no | Códigos de catálogo de ubicación. |
| `photoFilePublicId` | uuid | no | Id de un archivo previamente subido (foto). |
| `orgUnitPublicId` | uuid | no | Unidad organizativa asignada. |
| `assignedPositionSlotPublicId` | uuid | no | Plaza asignada (necesaria para finalizar como empleado). |

**Respuesta `201`** — `PersonnelFileShellResponse` (+ headers `Location` y `ETag`):

| Campo | Tipo |
|-------|------|
| `publicId` | uuid |
| `companyPublicId` | uuid |
| `recordType` | enum string |
| `lifecycleStatus` | enum string (`Draft` / `Completed`) |
| `fullName` | string |
| `photoUrl` | string (nullable) |
| `isActive` | boolean |
| `orgUnitPublicId` | uuid (nullable) |
| `assignedPositionSlotPublicId` | uuid (nullable) |
| `linkedUserPublicId` | uuid (nullable) |
| `concurrencyToken` | uuid |
| `createdAtUtc` / `modifiedAtUtc` | string (date-time) |
| `allowedActions` | object (acciones permitidas al usuario actual) |

**Ejemplo:**

```bash
curl -X POST "$BASE/api/v1/companies/$COMPANY/personnel-files" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "recordType": "Employee",
    "firstName": "Lucía",
    "lastName": "Méndez",
    "birthDate": "1992-04-18",
    "maritalStatusCode": "SOLTERO_A",
    "professionCode": "ANALISTA_DE_DATOS",
    "nationality": "SV",
    "institutionalEmail": "lucia.mendez@acme.com",
    "orgUnitPublicId": "0b1d...e9"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/3d9e...05   ETag: "a1b2...c3"
{
  "publicId": "3d9e...05",
  "companyPublicId": "659c...05",
  "recordType": "Employee",
  "lifecycleStatus": "Draft",
  "fullName": "Lucía Méndez",
  "isActive": true,
  "concurrencyToken": "a1b2...c3",
  "createdAtUtc": "2026-05-31T14:02:11Z"
}
```

**Errores:** `400` (validación / `items` legacy no aceptado), `409` (identificación duplicada), `422` (regla de negocio), `429` (rate limit).

---

## `GET` Buscar archivos (paginado)

`GET /api/v1/companies/{companyPublicId}/personnel-files`

**Query params** (todos opcionales):

| Param | Tipo | Notas |
|-------|------|-------|
| `q` | string | Texto libre sobre el nombre completo (mín. 2 caracteres). |
| `isActive` | boolean | |
| `recordType` | enum string | |
| `orgUnitPublicId` | uuid | |
| `minAge` / `maxAge` | int | |
| `maritalStatus` / `nationality` / `profession` | string | |
| `createdFromUtc` / `createdToUtc` | date-time | Rango de creación. |
| `sortBy` | string | Campo de orden permitido. |
| `sortDirection` | enum (`Asc`/`Desc`) | Default `Asc`. |
| `page` | int | 1‑based, default `1`. |
| `pageSize` | int | Default `20`, **máx `100`**. |
| `includeAllowedActions` | boolean | Incluye, por ítem, las acciones que el usuario puede ejecutar. |

**Respuesta `200`** — `PagedResponse<PersonnelFileListItemResponse>`:

```jsonc
{
  "items": [
    {
      "publicId": "3d9e...05",
      "companyPublicId": "659c...05",
      "recordType": "Employee",
      "lifecycleStatus": "Completed",
      "fullName": "Lucía Méndez",
      "age": 34,
      "maritalStatusCode": "SOLTERO_A", "maritalStatusName": "Soltero/a",
      "professionCode": "ANALISTA_DE_DATOS", "professionName": "Analista de Datos",
      "orgUnitPublicId": "0b1d...e9",
      "assignedPositionSlotPublicId": "77aa...12",
      "linkedUserPublicId": "91cc...34",
      "isActive": true,
      "createdAtUtc": "2026-05-20T10:00:00Z",
      "modifiedAtUtc": "2026-05-28T09:15:00Z",
      "allowedActions": { "canRead": true, "canManage": true }
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 137
}
```

```bash
curl "$BASE/api/v1/companies/$COMPANY/personnel-files?q=mend&isActive=true&page=1&pageSize=20&includeAllowedActions=true" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `400` (`q` < 2 / `pageSize` fuera de rango), `429`.

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}` → `200` `PersonnelFileShellResponse` (mismos campos que el `201` de creación). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH` del shell.

```bash
curl "$BASE/api/v1/personnel-files/$ID" -H "Authorization: Bearer $TOKEN"
```

**Errores:** `404`.

---

## `PUT` Actualizar (reemplazo de campos núcleo)

`PUT /api/v1/personnel-files/{publicId}` · **requiere `If-Match`** con el `concurrencyToken` del archivo.

Reemplaza los campos núcleo (info personal). **No** cambia el estado activo/inactivo (eso es por `PATCH`). El `recordType` no se puede transicionar acá. Body = mismo shape que el `POST`.

**Respuesta `200`** — `PersonnelFilePersonalInfoResponse` (info personal completa, incluye `concurrencyToken` nuevo + nombres resueltos de catálogos y `photoUrl`). El nuevo token también viene en el header `ETag`.

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{ "recordType": "Employee", "firstName": "Lucía", "lastName": "Méndez Soto", "nationality": "SV" }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (campos bloqueados tras finalizar, p. ej. `assignedPositionSlot`/`institutionalEmail`).

---

## `PATCH` Cambios parciales + activar/desactivar

`PATCH /api/v1/personnel-files/{publicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los de info personal **+ `isActive`**. Setear `isActive` es **el** mecanismo para **activar/desactivar** el archivo (reemplaza los antiguos `/activate` `/inactivate`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/isActive", "value": false },
    { "op": "replace", "path": "/personalPhone", "value": "+503 7000-0000" }
  ]'
```

**Respuesta `200`** — `PersonnelFilePersonalInfoResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `429`.

> La transición de ciclo de vida **Draft → Completed** NO se hace acá: es el flujo de [finalize](./finalize.md).
