# Personnel Files API — Contrato de Integración Frontend

Documento de contrato (request / response / uso) para el controlador **Personnel Files**
(la **entidad shell** del expediente de personal). Solo lo necesario para consumir los
endpoints desde el frontend.

> **Alcance: solo la entidad PersonnelFiles (shell). NO incluye sus hijos / sub-recursos**
> (identificaciones, direcciones, contactos de emergencia, familiares, hobbies, relaciones de
> empleado, cuentas bancarias, asociaciones, educación, idiomas, capacitaciones, empleos previos,
> referencias, documentos, observaciones, perfil de empleo, métodos de pago, finalización, etc.).

- **Versión API:** `v1`
- **Base URL:** `https://{host}/api/v1`
- **Autenticación:** `Authorization: Bearer <jwt>` (todos los endpoints requieren usuario autenticado)
- **Content-Type peticiones JSON:** `application/json`
- **Content-Type del PATCH:** `application/json-patch+json` (RFC 6902)
- **Accept:** `application/json`

> Fuente del contrato: `swagger.json` generado por el API en ejecución (no los DTOs de C#),
> verificado contra el API real.

---

## Endpoints (resumen)

| # | Método | Ruta | Descripción | Devuelve |
|---|---|---|---|---|
| 1 | `GET` | `/companies/{companyPublicId}/personnel-files` | Buscar / listar (paginado) | `PagedResponse<PersonnelFileListItemResponse>` |
| 2 | `POST` | `/companies/{companyPublicId}/personnel-files` | Crear | `PersonnelFileShellResponse` (201) |
| 3 | `GET` | `/personnel-files/{publicId}` | Obtener por id | `PersonnelFileShellResponse` |
| 4 | `PUT` | `/personnel-files/{publicId}` | Actualizar campos del núcleo | `PersonnelFilePersonalInfoResponse` |
| 5 | `PATCH` | `/personnel-files/{publicId}` | JSON Patch (campos núcleo + `isActive`) | `PersonnelFilePersonalInfoResponse` |

> **No existe `DELETE`.** La **inactivación** se hace con `PATCH` poniendo `isActive=false`
> (reactivar con `isActive=true`). El ciclo de vida `Draft → Completed` (finalización) es un
> flujo aparte, fuera del alcance de este documento.

---

## Permisos requeridos

| Acción | Permiso (cualquiera de) |
|---|---|
| Leer / buscar / obtener (`GET`) | `PersonnelFiles.Read`, `PersonnelFiles.Admin`, `iam.administration.manage` |
| Crear / actualizar / patch (`POST`/`PUT`/`PATCH`) | `PersonnelFiles.Admin`, `iam.administration.manage` |

Sin permiso → `403 Forbidden` con `code = PERSONNEL_FILES_FORBIDDEN`. El módulo **Personnel
Files** debe estar habilitado en el plan del tenant (si no, también `403`). Todo es
**tenant-scoped**: el `companyPublicId` debe coincidir con el tenant del token o se devuelve `403`.

---

## Control de concurrencia (ETag / If-Match)

Obligatorio para `PUT` y `PATCH`.

1. `POST` y `GET /personnel-files/{publicId}` devuelven `concurrencyToken` (GUID) en el body **y**
   la cabecera `ETag: "<guid>"`.
2. Para actualizar (`PUT`/`PATCH`) envía la cabecera **`If-Match`** con ese token.
   - Acepta `If-Match: "1f0c9b6e-..."` (con comillas, recomendado) o `If-Match: 1f0c9b6e-...`.
3. La respuesta de `PUT`/`PATCH` trae un **nuevo** `concurrencyToken` y nuevo `ETag`; guárdalo.
4. Token desactualizado → `409 Conflict` con `code = CONCURRENCY_CONFLICT` (refrescar y reintentar).
5. Falta `If-Match` → `400 Bad Request`.

---

## Formato de errores (estándar)

RFC 7807 Problem Details, enriquecido con `code` y `traceId` (ejemplo **real** del API):

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "No tienes permiso para acceder a personnel file administration.",
  "detail": "No tienes permiso para acceder a personnel file administration.",
  "status": 403,
  "code": "PERSONNEL_FILES_FORBIDDEN",
  "traceId": "0HNLQKQ2HNG50:00000001"
}
```

Errores de validación (`400`) agregan el diccionario `errors`:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "code": "common.validation",
  "errors": {
    "firstName": ["FirstName format is invalid."],
    "assignedPositionSlotPublicId": ["AssignedPositionSlotPublicId is required for employee personnel files."]
  },
  "traceId": "..."
}
```

> Usa el campo `code` (no el `title`) para lógica de UI/i18n.

### Códigos de error relevantes

| HTTP | code | Cuándo |
|---|---|---|
| 400 | `common.validation` | Body/query inválido; `q` con menos de 2 caracteres; JSON Patch malformado |
| 401 | `auth.unauthorized` | Falta o expiró el token |
| 403 | `PERSONNEL_FILES_FORBIDDEN` | Sin permiso, módulo deshabilitado, o tenant distinto |
| 404 | `PERSONNEL_FILE_NOT_FOUND` | El expediente no existe (en el tenant actual) |
| 409 | `CONCURRENCY_CONFLICT` | `If-Match` desactualizado |
| 409 | `PERSONNEL_FILE_IDENTIFICATION_CONFLICT` | Otra ficha ya usa la identificación enviada |
| 409 | `PERSONNEL_FILE_LINKED_USER_CONFLICT` | El email institucional ya está vinculado a otra ficha |
| 422 | `PERSONNEL_FILE_RECORD_TYPE_TRANSITION_NOT_ALLOWED` | Se intentó cambiar `recordType` en `PUT`/`PATCH` |
| 422 | `PERSONNEL_FILE_PROVISIONING_FIELDS_LOCKED` | Se intentó cambiar `institutionalEmail`/`assignedPositionSlotPublicId` tras completar |
| 422 | `PERSONNEL_FILE_STATE_RULE_VIOLATION` | Operación no permitida para el estado actual |
| 429 | `common.too_many_requests` | Rate limit excedido (`POST`, `GET` búsqueda, `PATCH`) |

---

## Enumeraciones (se serializan como **string**)

**`recordType`**: `"Candidate"` (candidato, sin posición) · `"Employee"` (empleado, requiere `assignedPositionSlotPublicId`).
**`lifecycleStatus`** (solo lectura): `"Draft"` (inicial) · `"Completed"` (finalizada).
**`sortDirection`** (query): `"Asc"` · `"Desc"`.

> **IDs en el cable:** todos los GUID viajan como `*PublicId` (`publicId`, `companyPublicId`,
> `orgUnitPublicId`, `assignedPositionSlotPublicId`, `photoFilePublicId`, `linkedUserPublicId`).
> `recordType` no puede transicionarse tras crear (ni por `PUT` ni por `PATCH`).

---

# Endpoints

## 1. `GET /api/v1/companies/{companyPublicId}/personnel-files`

**Buscar / listar expedientes** (paginado). *Rate limited.*

### Query params
| Param | Tipo | Default | Descripción |
|---|---|---|---|
| `isActive` | bool | — | Filtra activos/inactivos |
| `recordType` | string | — | `"Candidate"` / `"Employee"` |
| `orgUnitPublicId` | GUID | — | Filtra por unidad organizativa |
| `minAge` / `maxAge` | int | — | Rango de edad (>= 0; `minAge <= maxAge`) |
| `maritalStatus` / `nationality` / `profession` | string | — | Filtros por código/valor |
| `createdFromUtc` / `createdToUtc` | datetime | — | ISO 8601 UTC (`from <= to`) |
| `q` | string (**mín 2**, máx 150) | — | Texto libre (nombre completo / nº identificación). Vacío = sin filtro; 1 carácter → `400` |
| `sortBy` | string | — | Campo de orden permitido |
| `sortDirection` | string | `Asc` | `"Asc"` / `"Desc"` |
| `page` | int | `1` | Página (>= 1) |
| `pageSize` | int | `20` | 1–100 |
| `includeAllowedActions` | bool | `false` | Incluye `allowedActions` por ítem |

### Ejemplo request
```http
GET /api/v1/companies/659cc560-1c7b-454b-bde0-c76562f5c005/personnel-files?recordType=Employee&isActive=true&q=ramirez&page=1&pageSize=20&includeAllowedActions=true
Authorization: Bearer <jwt>
Accept: application/json
```

### Respuesta `200 OK`
```json
{
  "items": [
    {
      "publicId": "2b9a3f10-0000-0000-0000-000000000001",
      "companyPublicId": "659cc560-1c7b-454b-bde0-c76562f5c005",
      "recordType": "Employee",
      "lifecycleStatus": "Completed",
      "fullName": "Ana Ramirez",
      "age": 34,
      "maritalStatusCode": "SOLTERO_A",
      "maritalStatusName": "Soltero/a",
      "professionCode": "ANALISTA_DE_DATOS",
      "professionName": "Analista de Datos",
      "orgUnitPublicId": "0a1b2c3d-0000-0000-0000-000000000010",
      "assignedPositionSlotPublicId": "7f7f7f7f-0000-0000-0000-000000000020",
      "linkedUserPublicId": "5c5c5c5c-0000-0000-0000-000000000030",
      "isActive": true,
      "createdAtUtc": "2026-02-10T15:30:00Z",
      "modifiedAtUtc": "2026-05-20T11:05:00Z",
      "allowedActions": {
        "canEdit": true, "canDelete": false, "canArchive": false,
        "canActivate": false, "canInactivate": true, "reasons": [],
        "canSubmit": false, "canApprove": false, "canReject": false,
        "canCancel": false, "canPublish": false, "canFinalize": false,
        "actionPermissions": []
      }
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

> El ítem de lista **no** trae `concurrencyToken` ni `birthDate`. `allowedActions` es `null`
> cuando `includeAllowedActions=false`.

### Errores
`400` (query inválido / `q` < 2), `401`, `403`, `429`.

---

## 2. `POST /api/v1/companies/{companyPublicId}/personnel-files`

**Crear un expediente.** Inicia en `lifecycleStatus = "Draft"`. *Rate limited.*

### Body (`application/json`)
| Campo | Tipo | Req. | Reglas |
|---|---|---|---|
| `recordType` | string | ✅ | `"Candidate"` / `"Employee"` |
| `firstName` | string | ✅ | máx 100, formato nombre |
| `lastName` | string | ✅ | máx 100, formato nombre |
| `birthDate` | datetime | ✅ | ISO 8601 |
| `maritalStatusCode` | string? | — | máx 80; catálogo |
| `professionCode` | string? | — | máx 120; catálogo |
| `nationality` | string? | — | |
| `personalEmail` / `institutionalEmail` | string? | — | email válido |
| `personalPhone` / `institutionalPhone` | string? | — | máx 40, formato teléfono |
| `birthCountryCode` | string? | — | máx 3; catálogo país |
| `birthDepartmentCode` / `birthMunicipalityCode` | string? | — | catálogo (depende de país/depto) |
| `photoFilePublicId` | GUID? | — | Archivo de foto previamente subido |
| `orgUnitPublicId` | GUID? | — | Unidad organizativa |
| `assignedPositionSlotPublicId` | GUID? | — | **Requerido si `Employee`; prohibido si `Candidate`** |

### Ejemplo request
```http
POST /api/v1/companies/659cc560-1c7b-454b-bde0-c76562f5c005/personnel-files
Authorization: Bearer <jwt>
Content-Type: application/json
```
```json
{
  "recordType": "Employee",
  "firstName": "Ana",
  "lastName": "Ramirez",
  "birthDate": "1991-04-05T00:00:00Z",
  "maritalStatusCode": "SOLTERO_A",
  "professionCode": "ANALISTA_DE_DATOS",
  "nationality": "SV",
  "personalEmail": "ana.ramirez@example.com",
  "institutionalEmail": "ana.ramirez@empresa.com",
  "personalPhone": "+503 7000-1111",
  "institutionalPhone": null,
  "birthCountryCode": "SV",
  "birthDepartmentCode": "SAN_SALVADOR",
  "birthMunicipalityCode": "SAN_SALVADOR_CENTRO",
  "photoFilePublicId": null,
  "orgUnitPublicId": "0a1b2c3d-0000-0000-0000-000000000010",
  "assignedPositionSlotPublicId": "7f7f7f7f-0000-0000-0000-000000000020"
}
```

### Respuesta `201 Created`
- Cabecera `Location: /api/v1/personnel-files/{publicId}` + `ETag: "<concurrencyToken>"`
- Body = `PersonnelFileShellResponse`:
```json
{
  "publicId": "2b9a3f10-0000-0000-0000-000000000001",
  "companyPublicId": "659cc560-1c7b-454b-bde0-c76562f5c005",
  "recordType": "Employee",
  "lifecycleStatus": "Draft",
  "fullName": "Ana Ramirez",
  "photoUrl": null,
  "isActive": true,
  "orgUnitPublicId": "0a1b2c3d-0000-0000-0000-000000000010",
  "assignedPositionSlotPublicId": "7f7f7f7f-0000-0000-0000-000000000020",
  "linkedUserPublicId": null,
  "concurrencyToken": "9b8a7c6d-0000-0000-0000-0000000000aa",
  "createdAtUtc": "2026-05-25T12:00:00Z",
  "modifiedAtUtc": null,
  "allowedActions": null
}
```

### Errores
`400`, `401`, `403`, `404` (catálogo), `409` (`..._IDENTIFICATION_CONFLICT`, `..._LINKED_USER_CONFLICT`), `422`, `429`.

---

## 3. `GET /api/v1/personnel-files/{publicId}`

**Obtener el shell por id.**

```http
GET /api/v1/personnel-files/2b9a3f10-0000-0000-0000-000000000001
Authorization: Bearer <jwt>
Accept: application/json
```

### Respuesta `200 OK`
- Cabecera `ETag: "<concurrencyToken>"`
- Body = `PersonnelFileShellResponse` (misma forma que el `POST`). `photoUrl` viene resuelto a una URL temporal cuando hay foto.

> Guarda `concurrencyToken` (o lee `ETag`) — lo necesitas para `PUT`/`PATCH`.

### Errores
`401`, `403`, `404` (`PERSONNEL_FILE_NOT_FOUND`).

---

## 4. `PUT /api/v1/personnel-files/{publicId}`

**Actualizar (reemplazar) los campos del núcleo (personal-info).**

- Requiere cabecera **`If-Match`** con el `concurrencyToken` actual.
- **No** cambia el estado activo (eso es `PATCH`) ni el `recordType`.
- Body = mismos campos que el `POST` (sin `concurrencyToken`; va en `If-Match`). Mismas validaciones.
- Devuelve la **proyección completa de personal-info** (no el shell liviano).

### Ejemplo request
```http
PUT /api/v1/personnel-files/2b9a3f10-0000-0000-0000-000000000001
Authorization: Bearer <jwt>
Content-Type: application/json
If-Match: "9b8a7c6d-0000-0000-0000-0000000000aa"
```
```json
{
  "recordType": "Employee",
  "firstName": "Ana María",
  "lastName": "Ramirez",
  "birthDate": "1991-04-05T00:00:00Z",
  "maritalStatusCode": "CASADO_A",
  "professionCode": "ANALISTA_DE_DATOS",
  "nationality": "SV",
  "personalEmail": "ana.ramirez@example.com",
  "institutionalEmail": "ana.ramirez@empresa.com",
  "personalPhone": "+503 7000-2222",
  "institutionalPhone": null,
  "birthCountryCode": "SV",
  "birthDepartmentCode": "SAN_SALVADOR",
  "birthMunicipalityCode": "SAN_SALVADOR_CENTRO",
  "photoFilePublicId": null,
  "orgUnitPublicId": "0a1b2c3d-0000-0000-0000-000000000010",
  "assignedPositionSlotPublicId": "7f7f7f7f-0000-0000-0000-000000000020"
}
```

### Respuesta `200 OK`
- Cabecera `ETag: "<nuevo concurrencyToken>"`
- Body = `PersonnelFilePersonalInfoResponse`:
```json
{
  "publicId": "2b9a3f10-0000-0000-0000-000000000001",
  "companyPublicId": "659cc560-1c7b-454b-bde0-c76562f5c005",
  "recordType": "Employee",
  "lifecycleStatus": "Draft",
  "firstName": "Ana María",
  "lastName": "Ramirez",
  "fullName": "Ana María Ramirez",
  "birthDate": "1991-04-05T00:00:00Z",
  "age": 34,
  "maritalStatusCode": "CASADO_A",
  "maritalStatusName": "Casado/a",
  "professionCode": "ANALISTA_DE_DATOS",
  "professionName": "Analista de Datos",
  "nationality": "SV",
  "personalEmail": "ana.ramirez@example.com",
  "institutionalEmail": "ana.ramirez@empresa.com",
  "personalPhone": "+503 7000-2222",
  "institutionalPhone": null,
  "birthCountryCode": "SV",
  "birthCountryName": "El Salvador",
  "birthDepartmentCode": "SAN_SALVADOR",
  "birthDepartmentName": "San Salvador",
  "birthMunicipalityCode": "SAN_SALVADOR_CENTRO",
  "birthMunicipalityName": "San Salvador Centro",
  "photoUrl": null,
  "orgUnitPublicId": "0a1b2c3d-0000-0000-0000-000000000010",
  "assignedPositionSlotPublicId": "7f7f7f7f-0000-0000-0000-000000000020",
  "linkedUserPublicId": null,
  "isActive": true,
  "concurrencyToken": "1c2d3e4f-0000-0000-0000-0000000000bb",
  "createdAtUtc": "2026-05-25T12:00:00Z",
  "modifiedAtUtc": "2026-05-25T12:30:00Z"
}
```

### Errores
`400` (validación / falta `If-Match`), `401`, `403`, `404`, `409` (`CONCURRENCY_CONFLICT`),
`422` (`..._RECORD_TYPE_TRANSITION_NOT_ALLOWED`, `..._PROVISIONING_FIELDS_LOCKED`).

---

## 5. `PATCH /api/v1/personnel-files/{publicId}`

**Aplicar un JSON Patch (RFC 6902).** *Rate limited.*

- `Content-Type: application/json-patch+json`.
- Requiere cabecera **`If-Match`** con el `concurrencyToken` actual.
- **El body es un array RFC 6902 desnudo** `[{ "op", "path", "value" }, …]`, **no** un objeto con
  `operations`. (Verificado contra el API: enviar `{ "operations": [...] }` devuelve `400`
  `common.validation` "The JSON patch document was malformed and could not be parsed". El esquema
  de Swagger lo dibuja como `{ operations: [...] }`, pero ese no es el formato del cable.)
- Miembros parcheables: `recordType` (no se permite cambiarlo → `422`), `firstName`, `lastName`,
  `birthDate`, `maritalStatusCode`, `professionCode`, `nationality`, `personalEmail`,
  `institutionalEmail`, `personalPhone`, `institutionalPhone`, `birthCountryCode`,
  `birthDepartmentCode`, `birthMunicipalityCode`, `photoFilePublicId`, `orgUnitPublicId`,
  `assignedPositionSlotPublicId`, **`isActive`**.
- **Activar / inactivar** se hace aquí con `op replace /isActive` (reemplaza a los antiguos
  `/activate` e `/inactivate`). Devuelve `PersonnelFilePersonalInfoResponse` (igual que el `PUT`).

### Ejemplo A — inactivar
```http
PATCH /api/v1/personnel-files/2b9a3f10-0000-0000-0000-000000000001
Authorization: Bearer <jwt>
Content-Type: application/json-patch+json
If-Match: "1c2d3e4f-0000-0000-0000-0000000000bb"
```
```json
[
  { "op": "replace", "path": "/isActive", "value": false }
]
```

### Ejemplo B — reactivar
```json
[
  { "op": "replace", "path": "/isActive", "value": true }
]
```

### Ejemplo C — editar campos puntuales del núcleo
```json
[
  { "op": "replace", "path": "/personalPhone", "value": "+503 7000-3333" },
  { "op": "replace", "path": "/maritalStatusCode", "value": "CASADO_A" }
]
```

### Respuesta `200 OK`
- Cabecera `ETag: "<nuevo concurrencyToken>"`
- Body = `PersonnelFilePersonalInfoResponse` (igual que el `PUT`).

### Errores
`400` (patch inválido / falta `If-Match` / payload > 64 KiB), `401`, `403`, `404`,
`409` (`CONCURRENCY_CONFLICT`), `422` (`..._RECORD_TYPE_TRANSITION_NOT_ALLOWED`,
`..._PROVISIONING_FIELDS_LOCKED`), `429`.

---

## Apéndice — Esquemas

### `PersonnelFileShellResponse` (POST / GET por id)
`publicId` (GUID), `companyPublicId` (GUID), `recordType` (string enum), `lifecycleStatus`
(string enum), `fullName` (string?), `photoUrl` (string?), `isActive` (bool),
`orgUnitPublicId` (GUID?), `assignedPositionSlotPublicId` (GUID?), `linkedUserPublicId` (GUID?),
`concurrencyToken` (GUID), `createdAtUtc` (datetime), `modifiedAtUtc` (datetime?),
`allowedActions` (`AllowedActionsResponse?`).

### `PersonnelFilePersonalInfoResponse` (PUT / PATCH)
`publicId`, `companyPublicId`, `recordType`, `lifecycleStatus`, `firstName?`, `lastName?`,
`fullName?`, `birthDate`, `age` (int), `maritalStatusCode?`, `maritalStatusName?`,
`professionCode?`, `professionName?`, `nationality?`, `personalEmail?`, `institutionalEmail?`,
`personalPhone?`, `institutionalPhone?`, `birthCountryCode?`, `birthCountryName?`,
`birthDepartmentCode?`, `birthDepartmentName?`, `birthMunicipalityCode?`, `birthMunicipalityName?`,
`photoUrl?`, `orgUnitPublicId?`, `assignedPositionSlotPublicId?`, `linkedUserPublicId?`,
`isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc?`. (Sin `allowedActions`.)

### `PersonnelFileListItemResponse` (ítem de búsqueda)
`publicId`, `companyPublicId`, `recordType`, `lifecycleStatus`, `fullName?`, `age` (int),
`maritalStatusCode?`, `maritalStatusName?`, `professionCode?`, `professionName?`,
`orgUnitPublicId?`, `assignedPositionSlotPublicId?`, `linkedUserPublicId?`, `isActive`,
`createdAtUtc`, `modifiedAtUtc?`, `allowedActions?`. (Sin `concurrencyToken` ni `birthDate`.)

### `AllowedActionsResponse`
`canEdit`, `canDelete`, `canArchive`, `canActivate`, `canInactivate` (bool), `reasons` (string[]),
`canSubmit`, `canApprove`, `canReject`, `canCancel`, `canPublish`, `canFinalize` (bool),
`actionPermissions` (`{ action, permissionCode, allowed, reasons[] }[]`).

### `PagedResponse<T>`
`items` (T[]), `pageNumber` (int), `pageSize` (int), `totalCount` (int).

---

## Flujo recomendado en el frontend

1. **Listar:** `GET /companies/{companyPublicId}/personnel-files` con filtros +
   `includeAllowedActions=true` (respeta `q` mínimo 2 caracteres).
2. **Crear:** `POST /companies/{companyPublicId}/personnel-files` → guardar `publicId` + `concurrencyToken` (`ETag`).
3. **Ver/editar:** `GET /personnel-files/{publicId}` → leer `ETag` / `concurrencyToken`.
4. **Guardar cambios:** `PUT /personnel-files/{publicId}` con `If-Match` → actualizar el token en memoria.
5. **Inactivar / reactivar:** `PATCH /personnel-files/{publicId}` con `If-Match` y array
   `[{ "op": "replace", "path": "/isActive", "value": false|true }]`.
6. Ante `409 CONCURRENCY_CONFLICT`: re-`GET`, avisar y reintentar con el token fresco.
