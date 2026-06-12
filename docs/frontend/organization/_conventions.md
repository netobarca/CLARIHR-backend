# Organización — Convenciones de consumo (frontend)

> Reglas transversales para **todos** los controladores del dominio Organización (Org Units, Org
> Structure Catalogs, Cost Centers, Location Hierarchy/Levels/Groups, Work Centers, Work Center
> Types). Cada doc de recurso asume estas reglas y solo documenta lo específico de su contrato.
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## 1. Base, versión y compañía

```
https://<host>/api/v1
```

- **Compañía activa**: las rutas de **listado/creación** son scoped por compañía
  (`/companies/{companyPublicId}/<recurso>`), y el `companyPublicId` **debe ser la compañía activa**
  del JWT (la del último `switch`, ver Fase 2). El resto opera por id de recurso
  (`/<recurso>/{publicId}`) resolviendo el tenant del token.
- Cross-tenant: pedir un recurso de otra compañía responde **`404`** (no revela existencia) o
  **`403 TENANT_MISMATCH`** según el recurso — ver cada doc.

## 2. Autenticación y permisos (RBAC)

- **Bearer JWT** en `Authorization: Bearer <token>` en toda request.
- Cada familia tiene su par de permisos Read/Manage (gateados en el handler):

  | Familia | Leer (`GET`) | Mutar (`POST`/`PUT`/`PATCH`) |
  |---------|--------------|------------------------------|
  | Organization Units | `OrgUnits.Read` | `OrgUnits.Manage` |
  | Org Structure Catalogs | `OrgStructureCatalogs.Read` | `OrgStructureCatalogs.Manage` |
  | Cost Centers | `CostCenters.Read` | `CostCenters.Manage` |
  | Locations (Hierarchy/Levels/Groups/Work Centers/Types) | `Locations.Read` o `Locations.Admin` o `iam.administration.manage` | `Locations.Admin` o `iam.administration.manage` |

- Sin el permiso → `403`. Verificá los permisos del usuario en
  `access-context.currentUserAccess.permissions` (Fase 2 §13) para mostrar/ocultar pantallas y
  botones.

## 3. Identificadores (`publicId`)

- En el wire **siempre** GUIDs `publicId`; los ids internos nunca se exponen.
- Las FKs en los bodies se nombran `<recurso>PublicId` (ej. `orgUnitTypePublicId`,
  `locationGroupPublicId`, `parentPublicId`). Excepción: Cost Center se referencia **por código**
  (`costCenterCode`), no por id.

## 4. Concurrencia optimista — `If-Match` (token fuerte GUID)

Todos estos recursos usan **token fuerte**: cada respuesta trae `concurrencyToken` (GUID) en el
body **y** en el header `ETag: "<guid>"`.

- **`PUT` / `PATCH` / acciones (`activate` / `inactivate` / `move` / `reassign-group`) REQUIEREN**
  el header **`If-Match: "<concurrencyToken>"`** del propio recurso.
- Faltante o malformado → **`400`**. Stale (no coincide) → **`409 CONCURRENCY_CONFLICT`**
  (alguien lo modificó: recargá con `GET` y reintentá).
- Cada mutación exitosa devuelve el **nuevo** token (body + `ETag`); usalo para la siguiente.
- **`POST` (crear) NO lleva `If-Match`** (no hay nada con qué chocar); responde `201` con header
  `Location` y `ETag` inicial.

## 5. `PATCH` = JSON Patch (RFC 6902) — array desnudo

> ⚠️ El esquema que muestra Swagger para PATCH (`{ "operations": [...] }`) es **engañoso**. El wire
> real es un **array desnudo** de operaciones (convención de toda la API).

- **Content-Type:** `application/json-patch+json`
- **Body:** array JSON de operaciones RFC 6902, sin envoltorio:

  ```json
  [ { "op": "replace", "path": "/name", "value": "Nuevo nombre" } ]
  ```

- Operaciones: `add`, `replace`, `remove`. Solo paths de primer nivel. Cada recurso define qué
  paths son patchables; los inmutables (code/tipo/jerarquía/estado) se cambian por `PUT` o por las
  acciones dedicadas (`/move`, `/activate`, `/inactivate`, `/reassign-group`) — patchearlos falla.
- Límite de operaciones y tamaño de body acotados (excederlos → `400`/`413`).

## 6. `code` y `name` (campos comunes)

Casi todos los recursos tienen `code` + `name`:

- **`code`**: requerido, máx **50**, formato `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$` (empieza
  alfanumérico; luego alfanuméricos, `_` o `-`). Se normaliza a **MAYÚSCULAS** y es **único por
  compañía**. Un código duplicado → **`409 *_CODE_CONFLICT`** (cada familia tiene su código de
  error; ver cada doc). El `code` se edita por `PUT` pero **no** por `PATCH` en varios recursos.
- **`name`**: requerido, máx **150**.
- **`description`** (donde existe): opcional, máx **500**.

## 7. Activar / Inactivar (soft-delete)

No hay borrado físico (salvo donde se indique). El ciclo de vida es:

- **`PATCH /{id}/inactivate`** — soft-delete; **guardado** por reglas de negocio (no se puede
  inactivar algo en uso: con hijos activos, work centers activos, o referenciado por otros). →
  `409 *_IN_USE` / `*_HAS_ACTIVE_*`.
- **`PATCH /{id}/activate`** — reactiva (normalmente sin guardas).
- Ambas requieren `If-Match` y devuelven el recurso con su estado nuevo + `ETag` rotado.

## 8. Paginación y búsqueda

Endpoints de listado paginado:

```json
{ "items": [ /* ... */ ], "pageNumber": 1, "pageSize": 20, "totalCount": 137 }
```

- `page` (default `1`, ≥1) y `pageSize` (default `20`, **máx 100**). Fuera de rango → `400`.
- `q` (búsqueda libre por code/name): **mínimo 2 caracteres** cuando se provee (vacío = sin
  filtro); máx 150. Menos de 2 → `400`.
- `includeAllowedActions=true` enriquece cada ítem con `allowedActions`
  (`canEdit`/`canActivate`/`canInactivate` + `reasons[]`). **Importante**: estos flags derivan del
  **permiso del caller**, no del estado de dependencias por ítem (para evitar N+1). El guard real de
  inactivación corre en el servidor — usá el endpoint `/usage` o `/inactivate` y manejá el `409`.
- Algunas listas **no** son paginadas (ej. Location Levels, árboles `/tree`): devuelven el array
  completo.

## 9. Errores (ProblemDetails)

Todos los errores son RFC 7807 con `code` estable + `traceId`. Tabla transversal:

| HTTP | Cuándo |
|------|--------|
| `400` | validación, `If-Match` faltante/malformado, JSON Patch inválido, `q` < 2, `catalogKey`/format inválido |
| `401` | token faltante/expirado |
| `403` | sin permiso, o `TENANT_MISMATCH` (recurso de otra compañía) |
| `404` | el recurso no existe en esta compañía |
| `409` | conflicto de concurrencia (`CONCURRENCY_CONFLICT`) **o** regla de negocio (código duplicado, en-uso, ciclo, profundidad, padre inválido…) |
| `413` | body/exportación excede el límite |
| `422` | regla de estado/negocio (ej. dirección/geo requeridas por el tipo) |
| `429` | rate limit (búsqueda/árbol/export) |

La lógica del FE siempre sobre `code`, nunca sobre el mensaje (se localiza en/es por `Accept-Language`).

## 10. Rate limits

Por usuario + tenant, solo en lecturas costosas:

- **Búsqueda** (`GET` listado): generoso (≈120/min).
- **Árbol / grafo** (`/tree`, `/graph`): más ajustado (≈60/min).
- **Export** (`/export`, `/diagram-export`): el más ajustado (≈10/min).
- El resto (getById, mutaciones) sin rate limit específico.

Excepción: **Location Hierarchy/Levels no tienen rate limit** (por diseño: listas chicas, sin
search/tree/export).

---

### Índice

Volvé al [README](./README.md) para el modelo de datos y la lista de documentos por recurso.
