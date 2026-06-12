# Organización — Work Center Types

Catálogo de **tipos de centro de trabajo** (Oficina, Planta, Sucursal…). Cada tipo define si los
work centers de ese tipo **exigen dirección y/o geolocalización** y si **permiten biometría**.

> Antes de consumir, leé las [Convenciones](./_conventions.md). Acá solo lo específico.

**Permisos:** `GET` → `Locations.Read` · `POST/PUT/PATCH` → `Locations.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/work-center-types` | listar (paginado) |
| `GET` | `/work-center-types/{publicId}` | detalle (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/work-center-types` | crear |
| `PUT` | `/work-center-types/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/work-center-types/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/work-center-types/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/work-center-types/{publicId}/inactivate` | inactivar (`If-Match`) |

**Filtros del listado:** `isActive`, `q` (≥2), `page`, `pageSize`, `includeAllowedActions`.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación / Efecto |
|-------|------|------|---------------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `name` | string | Sí | máx 150 |
| `requiresAddress` | bool | Sí | si `true`, los work centers de este tipo **exigen** `address` |
| `requiresGeo` | bool | Sí | si `true`, exigen `geoLat` + `geoLong` |
| `allowsBiometric` | bool | Sí | habilita biometría para los work centers del tipo |

```json
{ "code": "PLANT", "name": "Planta", "requiresAddress": true, "requiresGeo": true, "allowsBiometric": true }
```

**Patch** patchables: `/code`, `/name`, `/requiresAddress`, `/requiresGeo`, `/allowsBiometric`.

## Responses

`WorkCenterTypeResponse`: `publicId`, `code`, `name`, `requiresAddress`, `requiresGeo`,
`allowsBiometric`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`,
`allowedActions?`.

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `WORK_CENTER_TYPE_CODE_CONFLICT` | 409 | código duplicado |
| `WORK_CENTER_TYPE_NOT_FOUND` | 404 | inexistente / otro tenant |
| `WORK_CENTER_TYPE_IN_USE` | 409 | inactivar un tipo referenciado por work centers activos |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **`requiresAddress` / `requiresGeo` gobiernan la validación de los Work Centers**: al crear/editar
  un work center de este tipo, el backend exige los campos correspondientes (`422` si faltan). El FE
  debe leer estos flags del tipo elegido para mostrar/ocultar y requerir esos campos en el form.
- **Inactivar** está bloqueado si hay work centers activos de ese tipo (`409 WORK_CENTER_TYPE_IN_USE`).
- `activate` no tiene guardas.

## Guía FE

- Cargá los tipos activos para el dropdown del form de [Work Center](./work-centers.md). Al
  seleccionar un tipo, usá sus flags (`requiresAddress`/`requiresGeo`) para condicionar los campos
  de dirección/coordenadas.
