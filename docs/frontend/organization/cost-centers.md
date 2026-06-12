# Organización — Cost Centers

Centros de costo para imputación contable. Transversales: los referencian las Organization Units
(por código) y los Position Slots.

> Antes de consumir, leé las [Convenciones](./_conventions.md). Acá solo lo específico.

**Permisos:** `GET` → `CostCenters.Read` · `POST/PUT/PATCH` → `CostCenters.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/cost-centers` | listar (paginado, filtros) |
| `GET` | `/cost-centers/{publicId}` | detalle (+ `ETag`) |
| `GET` | `/cost-centers/{publicId}/usage` | conteo de referencias (org units + position slots) |
| `GET` | `/companies/{companyPublicId}/cost-centers/export` | exportar (xlsx/…) |
| `POST` | `/companies/{companyPublicId}/cost-centers` | crear |
| `PUT` | `/cost-centers/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/cost-centers/{publicId}` | JSON Patch (`If-Match`) |
| `PATCH` | `/cost-centers/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/cost-centers/{publicId}/inactivate` | inactivar (`If-Match`) |

**Filtros del listado:** `type`, `isActive`, `q` (≥2), `page`, `pageSize`, `includeAllowedActions`.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, formato código, único por compañía |
| `name` | string | Sí | máx 150 |
| `type` | enum string | Sí | `SalaryExpense` \| `EmployerContribution` \| `ProvisionReserve` \| `Mixed` |
| `payrollExpenseAccountCode` | string | No | máx 100, formato `^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$` |
| `employerContributionAccountCode` | string | No | ídem |
| `provisionAccountCode` | string | No | ídem |
| `description` | string | No | máx 500 |

```json
{
  "code": "CC-001", "name": "Nómina Administración", "type": "SalaryExpense",
  "payrollExpenseAccountCode": "5101.01", "description": "…"
}
```

**Patch** patchables: `/code`, `/name`, `/type`, los tres `*AccountCode`, `/description` (el estado
va por activate/inactivate).

## Responses

`CostCenterResponse`: `publicId`, `companyPublicId`, `code`, `name`, `type`, los tres
`*AccountCode|null`, `description`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`,
`allowedActions{read,manage,canInactivate}?`.

### `GET /usage`

```json
{
  "publicId": "…", "code": "CC-001", "name": "…",
  "orgUnitActiveReferences": 3, "orgUnitInactiveReferences": 1,
  "positionSlotActiveReferences": 12, "positionSlotInactiveReferences": 0,
  "hasActiveReferences": true
}
```

Llamalo **antes** de ofrecer inactivar — si `hasActiveReferences: true`, el inactivate dará `409`.

### `GET /export`
Descarga con los mismos filtros. Query `format` (default `xlsx`; desconocido →
`400 COST_CENTER_EXPORT_FORMAT_INVALID`). Excede el límite → `413`. **Rate limit 10/min** (search 120/min).

## Enum `CostCenterType` (wire, string)

| Valor | Significado |
|-------|-------------|
| `SalaryExpense` | gasto salarial |
| `EmployerContribution` | aportes patronales |
| `ProvisionReserve` | provisión/reserva |
| `Mixed` | mixto |

## Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `COST_CENTER_CODE_CONFLICT` | 409 | código duplicado |
| `COST_CENTER_NOT_FOUND` | 404 | inexistente |
| `COST_CENTER_IN_USE` | 409 | inactivar uno aún referenciado por org units / position slots activos |
| `COST_CENTER_EXPORT_FORMAT_INVALID` | 400 | formato de export desconocido |
| `TENANT_MISMATCH` | 403 | recurso de otra compañía |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Reglas de negocio

- **Inactivar** está bloqueado mientras tenga referencias activas (`409 COST_CENTER_IN_USE`); usá
  `/usage` para anticiparlo y mostrar dónde se usa.
- Las Organization Units referencian el cost center **por `code`** (no por id) — el `code` es la
  clave estable de imputación.

## Guía FE

- Selector de cost center en el form de Organization Unit: listá activos y mandá el `code` elegido
  como `costCenterCode`.
- Antes de inactivar, llamá `/usage`; si hay referencias activas, mostrá el detalle (cuántas org
  units / position slots) en vez de un error genérico.
