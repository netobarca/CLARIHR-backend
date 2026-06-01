# Personnel Files — Personal Info

Lectura consolidada de la **información personal** de un archivo de personal (datos núcleo: nombre, nacimiento, estado civil, profesión, contactos, ubicación de nacimiento, foto). Es un recurso **de solo lectura**: un único `GET` que devuelve el objeto completo.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read`.

> Este recurso **no** tiene escrituras propias. Para **modificar** la info personal se usa el shell: `PUT` / `PATCH /api/v1/personnel-files/{publicId}` (ver [`personnel-files.md`](./personnel-files.md)). Ambas operaciones devuelven exactamente el mismo shape que este `GET` (`PersonnelFilePersonalInfoResponse`).

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/api/v1/personnel-files/{publicId}/personal-info` | Obtener la info personal consolidada del archivo |

**Path params:** `publicId` (uuid) = archivo de personal.

---

## `GET` Obtener la info personal

`GET /api/v1/personnel-files/{publicId}/personal-info`

Devuelve el objeto completo de info personal del archivo, con los **nombres resueltos** de los catálogos (estado civil, profesión, ubicación de nacimiento) y la URL de la foto. Es el mismo objeto (`PersonnelFilePersonalInfoResponse`) que devuelven el `PUT` y el `PATCH` del shell.

**Respuesta `200`** — `PersonnelFilePersonalInfoResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `publicId` | uuid | Id del archivo de personal. |
| `firstName` | string | |
| `lastName` | string | |
| `fullName` | string | Nombre completo compuesto. |
| `birthDate` | string (date, nullable) | `YYYY-MM-DD`. |
| `maritalStatusCode` | string (nullable) | Código de catálogo (país). |
| `maritalStatusName` | string (nullable) | Nombre resuelto del catálogo. |
| `professionCode` | string (nullable) | Código de catálogo (país). |
| `professionName` | string (nullable) | Nombre resuelto del catálogo. |
| `nationality` | string (nullable) | |
| `personalEmail` | string (nullable) | |
| `institutionalEmail` | string (nullable) | |
| `personalPhone` | string (nullable) | |
| `institutionalPhone` | string (nullable) | |
| `birthCountryCode` | string (nullable) | Código de catálogo de ubicación. |
| `birthCountryName` | string (nullable) | Nombre resuelto. |
| `birthDepartmentCode` | string (nullable) | Código de catálogo de ubicación. |
| `birthDepartmentName` | string (nullable) | Nombre resuelto. |
| `birthMunicipalityCode` | string (nullable) | Código de catálogo de ubicación. |
| `birthMunicipalityName` | string (nullable) | Nombre resuelto. |
| `photoUrl` | string (nullable) | URL de la foto (si hay `photoFilePublicId` cargado). |
| `concurrencyToken` | uuid | Token del archivo padre (usalo en `If-Match` para `PUT`/`PATCH` del shell). |

> El contrato exacto de este objeto es el mismo que el de la respuesta del `PUT`/`PATCH` del shell; la tabla de arriba lo enumera a modo de referencia. La fuente de verdad es el Swagger en runtime (ver [Convenciones](./_conventions.md)).

```bash
curl "$BASE/api/v1/personnel-files/$ID/personal-info" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
{
  "publicId": "3d9e...05",
  "firstName": "Lucía",
  "lastName": "Méndez",
  "fullName": "Lucía Méndez",
  "birthDate": "1992-04-18",
  "maritalStatusCode": "SOLTERO_A", "maritalStatusName": "Soltero/a",
  "professionCode": "ANALISTA_DE_DATOS", "professionName": "Analista de Datos",
  "nationality": "SV",
  "personalEmail": "lucia.mendez@gmail.com",
  "institutionalEmail": "lucia.mendez@acme.com",
  "personalPhone": "+503 7000-0000",
  "institutionalPhone": "+503 2200-0000",
  "birthCountryCode": "SV", "birthCountryName": "El Salvador",
  "birthDepartmentCode": "SAN_SALVADOR", "birthDepartmentName": "San Salvador",
  "birthMunicipalityCode": "SAN_SALVADOR", "birthMunicipalityName": "San Salvador",
  "photoUrl": "https://files.acme.com/photos/3d9e...05.jpg",
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `401` (token faltante/expirado), `403` (sin permiso o tenant ajeno), `404` (el archivo no existe en esta compañía).
