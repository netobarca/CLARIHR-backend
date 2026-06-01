# Employee Profile — Perfil de empleado

Sub‑recurso de **empleo**: la sección **única** de datos laborales del empleado (código de empleado, estado de empleo, tipo de contrato, fechas de ingreso/retiro, jornada, tipo de planilla, plaza/perfil/unidad asignados, configuración de vacaciones). Hay **uno solo** por archivo de personal.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> ⚠️ **Solo sobre archivo finalizado.** Tanto el `GET` como el `PUT` requieren un archivo **finalizado** (empleado, `lifecycleStatus = Completed`). Sobre un archivo en `Draft` responden **422**. Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

> **Upsert por `PUT`.** No hay `POST`, `PATCH` ni `DELETE`: el `PUT` crea el perfil la primera vez y lo reemplaza en lo sucesivo.

**Permisos:** `GET` → `PersonnelFiles.Read` · `PUT` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/employee-profile` | Obtener el perfil de empleado |
| `PUT`  | `/api/v1/personnel-files/{publicId}/employee-profile` | Crear o reemplazar el perfil (upsert) |

`publicId` = id del archivo de personal.

---

## `GET` Obtener el perfil

`GET /api/v1/personnel-files/{publicId}/employee-profile` → `200` con el perfil. El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para el `PUT` cuando el perfil ya existe.

```bash
curl "$BASE/api/v1/personnel-files/$ID/employee-profile" \
  -H "Authorization: Bearer $TOKEN"
```

**Respuesta `200`** — campos del perfil:

| Campo | Tipo |
|-------|------|
| `publicId` | uuid |
| `employeeCode` | string (nullable) |
| `employmentStatusCode` | string (nullable) |
| `isEmploymentActive` | boolean |
| `contractTypeCode` | string (nullable) |
| `hireDate` | string (date-time) |
| `retirementCategoryCode` | string (nullable) |
| `retirementReasonCode` | string (nullable) |
| `retirementNotes` | string (nullable) |
| `retirementDate` | string (date-time, nullable) |
| `workdayCode` | string (nullable) |
| `payrollTypeCode` | string (nullable) |
| `positionSlotPublicId` | uuid (nullable) |
| `jobProfilePublicId` | uuid (nullable) |
| `orgUnitPublicId` | uuid (nullable) |
| `workCenterPublicId` | uuid (nullable) |
| `costCenterPublicId` | uuid (nullable) |
| `contractStartDate` | string (date-time, nullable) |
| `contractEndDate` | string (date-time, nullable) |
| `vacationConfigurationJson` | string (nullable) |
| `concurrencyToken` | uuid |
| `createdAtUtc` | string (date-time) |
| `modifiedAtUtc` | string (date-time, nullable) |

```jsonc
// 200 OK
{
  "publicId": "3a7b...e1",
  "employeeCode": "EMP-00421",
  "employmentStatusCode": "ACTIVO",
  "isEmploymentActive": true,
  "contractTypeCode": "INDEFINIDO",
  "hireDate": "2026-01-06T00:00:00Z",
  "retirementCategoryCode": null,
  "retirementReasonCode": null,
  "retirementNotes": null,
  "retirementDate": null,
  "workdayCode": "TIEMPO_COMPLETO",
  "payrollTypeCode": "MENSUAL",
  "positionSlotPublicId": "77aa...12",
  "jobProfilePublicId": "55bb...34",
  "orgUnitPublicId": "0b1d...e9",
  "workCenterPublicId": "33cd...90",
  "costCenterPublicId": "44ef...01",
  "contractStartDate": "2026-01-06T00:00:00Z",
  "contractEndDate": null,
  "vacationConfigurationJson": "{\"annualDays\":15}",
  "concurrencyToken": "a1b2...c3",
  "createdAtUtc": "2026-01-06T12:00:00Z",
  "modifiedAtUtc": null
}
```

**Errores:** `401`, `403`, `404`, `422` (archivo en `Draft`).

---

## `PUT` Crear o reemplazar el perfil (upsert)

`PUT /api/v1/personnel-files/{publicId}/employee-profile`

- **Primera creación:** **NO** lleva `If-Match` (todavía no existe el perfil).
- **Reemplazos posteriores:** **requieren `If-Match`** con el `concurrencyToken` actual del perfil (del `GET` o de la respuesta previa). Si no coincide → `409`.

En ambos casos devuelve `200` con el perfil resultante; el nuevo `concurrencyToken` viene en el body y en el header `ETag`.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `employeeCode` | string | no | Código de empleado. |
| `employmentStatusCode` | string | no | Código de catálogo (estado de empleo). |
| `isEmploymentActive` | boolean | sí | Si el empleo está activo. |
| `contractTypeCode` | string | no | Código de catálogo (tipo de contrato). |
| `hireDate` | string (date-time) | sí | Fecha de ingreso. |
| `retirementCategoryCode` | string | no | Código de catálogo (categoría de retiro). |
| `retirementReasonCode` | string | no | Código de catálogo (motivo de retiro). |
| `retirementNotes` | string | no | |
| `retirementDate` | string (date-time) | no | Fecha de retiro (nullable). |
| `workdayCode` | string | no | Código de catálogo (jornada). |
| `payrollTypeCode` | string | no | Código de catálogo (tipo de planilla). |
| `positionSlotPublicId` | uuid | no | Plaza asignada. |
| `jobProfilePublicId` | uuid | no | Perfil de puesto. |
| `orgUnitPublicId` | uuid | no | Unidad organizativa. |
| `workCenterPublicId` | uuid | no | Centro de trabajo. |
| `costCenterPublicId` | uuid | no | Centro de costo. |
| `contractStartDate` | string (date-time) | no | Inicio de contrato (nullable). |
| `contractEndDate` | string (date-time) | no | Fin de contrato (nullable). |
| `vacationConfigurationJson` | string | no | Configuración de vacaciones serializada en JSON (string). |

**Respuesta `200`** — el perfil (mismos campos que el `GET`).

```bash
# Primera creación (sin If-Match)
curl -X PUT "$BASE/api/v1/personnel-files/$ID/employee-profile" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "employeeCode": "EMP-00421",
    "employmentStatusCode": "ACTIVO",
    "isEmploymentActive": true,
    "contractTypeCode": "INDEFINIDO",
    "hireDate": "2026-01-06T00:00:00Z",
    "workdayCode": "TIEMPO_COMPLETO",
    "payrollTypeCode": "MENSUAL",
    "positionSlotPublicId": "77aa...12",
    "jobProfilePublicId": "55bb...34",
    "orgUnitPublicId": "0b1d...e9",
    "contractStartDate": "2026-01-06T00:00:00Z",
    "vacationConfigurationJson": "{\"annualDays\":15}"
  }'

# Reemplazo posterior (con If-Match del token actual)
curl -X PUT "$BASE/api/v1/personnel-files/$ID/employee-profile" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{ "employeeCode": "EMP-00421", "employmentStatusCode": "ACTIVO", "isEmploymentActive": true, "hireDate": "2026-01-06T00:00:00Z" }'
```

**Errores:** `400` (validación / `If-Match` malformado), `409` (token desactualizado), `422` (archivo en `Draft` / regla de estado).
