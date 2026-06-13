# Organización — Cost Center Types

Catálogo de **tipos de centro de costo** por compañía (Gasto salarial, Aporte patronal,
Provisión/Reserva, Mixto…). Clasifica los [Cost Centers](./cost-centers.md): cada centro de costo
referencia **un tipo activo** de este catálogo por `publicId`. Reemplaza al antiguo enum `type` —
ahora cada compañía administra sus propios tipos.

> Antes de consumir, leé las [Convenciones](./_conventions.md). Acá solo lo específico.

**Permisos:** `GET` → `CostCenters.Read` · `POST/PUT/PATCH` → `CostCenters.Manage` (comparte los
permisos del módulo Cost Centers).

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/cost-center-types` | listar (paginado) |
| `GET` | `/cost-center-types/{publicId}` | detalle (+ `ETag`) |
| `POST` | `/companies/{companyPublicId}/cost-center-types` | crear |
| `PUT` | `/cost-center-types/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/cost-center-types/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/cost-center-types/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/cost-center-types/{publicId}/inactivate` | inactivar (`If-Match`) |

**Filtros del listado:** `isActive`, `q` (≥2), `page`, `pageSize` (máx 100),
`includeAllowedActions`. Orden: `name`, luego `code`. **Rate limit:** comparte el de search de cost
centers (120/min).

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `name` | string | Sí | máx 150 |
| `description` | string | No | máx 500 |

```json
{ "code": "SALARY-EXPENSE", "name": "Gasto salarial", "description": "Centros de costo de gasto salarial." }
```

**Patch** patchables: `/code`, `/name`, `/description` (el estado va por activate/inactivate).

## Responses

`CostCenterTypeResponse` (detalle y escrituras): `publicId`, `code`, `name`, `description|null`,
`isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`, `allowedActions?`.

Los items del **listado paginado** tienen el mismo shape **sin `description`** (solo detalle) —
para mostrarla o precargar el form de edición, pedí el tipo por id.

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `COST_CENTER_TYPE_CODE_CONFLICT` | 409 | código duplicado en la compañía |
| `COST_CENTER_TYPE_NOT_FOUND` | 404 | inexistente |
| `COST_CENTER_TYPE_IN_USE` | 409 | inactivar un tipo referenciado por cost centers **activos** |
| `COST_CENTER_TYPE_INACTIVE` | 409 | asignar un tipo inactivo a un cost center (create/update/patch) |
| `TENANT_MISMATCH` | 403 | recurso de otra compañía |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **Inactivar** está bloqueado mientras cost centers **activos** usen el tipo
  (`409 COST_CENTER_TYPE_IN_USE`). Para anticiparlo, consultá
  `/cost-centers?typeId={publicId}&isActive=true` y mostrá cuántos lo usan.
- **Asignar** un tipo a un cost center exige que esté **activo**; pero un cost center que ya tiene
  un tipo luego inactivado **sigue siendo editable** mientras no cambie de tipo (inactivar un tipo
  nunca "brickea" sus centros).
- `activate` no tiene guardas.
- **Datos sembrados:** las compañías existentes antes de la migración traen 4 tipos
  (`SALARY-EXPENSE`, `EMPLOYER-CONTRIBUTION`, `PROVISION-RESERVE`, `MIXED`). Las compañías nuevas
  inician con el **catálogo vacío**: hay que crear al menos un tipo antes del primer cost center.

## Guía FE

- **Integrá este catálogo antes que Cost Centers**: el form de cost center no puede enviarse sin un
  `costCenterTypePublicId` válido.
- Pantalla del catálogo: CRUD estándar (mismo patrón que
  [Work Center Types](./work-center-types.md), sin flags extra).
- Selector de tipo en el form de [Cost Center](./cost-centers.md): listá
  `/cost-center-types?isActive=true` y mandá el `publicId` elegido como `costCenterTypePublicId`.
  Si la compañía no tiene tipos activos, ofrecé un atajo para crear uno.
- El listado de cost centers ya devuelve `costCenterTypeId`, `costCenterTypeCode` y
  `costCenterTypeName` por item — la columna/filtro de tipo no necesita un fetch extra de este
  catálogo (el filtro del listado es `typeId`).
