# Job Profiles — Convenciones de consumo (frontend)

> Reglas transversales para **todos** los endpoints de Job Profiles (shell, sus 9 sub‑recursos y
> los 2 catálogos). Cada doc de recurso asume estas reglas y solo documenta lo específico.
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## 1. Base, versión y compañía

```
https://<host>/api/v1
```

- El **shell** (crear/buscar) es scoped por compañía (`/companies/{companyPublicId}/job-profiles`),
  y el `companyPublicId` **debe ser la compañía activa** del JWT (Fase 2). El resto opera por id del
  perfil (`/job-profiles/{publicId}/...`) resolviendo el tenant del token.
- Cross-tenant → `404`.

## 2. Autenticación y permisos (RBAC)

- **Bearer JWT** en `Authorization: Bearer <token>` en toda request.
- Permisos:

  | Acción | Permiso (cualquiera de) |
  |--------|-------------------------|
  | **Leer** (`GET`) | `JobProfiles.Read` · `JobProfiles.Admin` · `JobCatalogs.Admin` · `iam.administration.manage` |
  | **Escribir** perfiles y sub‑recursos (`POST`/`PUT`/`PATCH`/`DELETE`) | `JobProfiles.Admin` · `iam.administration.manage` |
  | **Administrar catálogos** (`job-catalogs` write) | `JobCatalogs.Admin` · `iam.administration.manage` |

- Sin permiso → `403 JOB_PROFILES_FORBIDDEN`. Verificá los permisos en
  `access-context.currentUserAccess.permissions` (Fase 2 §13).

## 3. Identificadores (`publicId`)

- En el wire siempre GUIDs `publicId`. El id del perfil padre en las rutas de sub‑recurso es
  `jobProfilePublicId`; el de cada ítem es `<recurso>PublicId` (ej. `functionPublicId`,
  `requirementPublicId`).
- Las FKs en los bodies se nombran `<recurso>PublicId` (ej. `orgUnitPublicId`,
  `catalogItemPublicId`, `salaryTabulatorLinePublicId`).

## 4. Estados del perfil (Draft / Published / Archived) — IMPORTANTE

Un Job Profile es un **aggregate root con ciclo de vida**:

| Estado | Significado | Editable |
|--------|-------------|----------|
| `Draft` | en construcción (estado inicial al crear) | sí (perfil + sub‑recursos) |
| `Published` | publicado/vigente | sí (perfil + sub‑recursos) |
| `Archived` | archivado | **no** (todo bloqueado) |

- **El estado solo se cambia por `PATCH` del shell** (no por `PUT`): mandá una operación
  `{ "op": "replace", "path": "/status", "value": "Published" }` (o `"Archived"`).
- **Publicar valida prerrequisitos**: el perfil debe tener `objective`, `responsibilities`, **≥1
  requirement** y **≥1 function**; si falta algo → `422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`.
  El FE debería avisar antes de intentar publicar.
- Sobre un perfil `Archived` cualquier escritura (perfil o sub‑recurso) falla. Reactivá pasándolo
  de nuevo a `Draft`/`Published` (según permita el backend) antes de editar.

## 5. Concurrencia optimista — `If-Match` (token fuerte GUID)

Token fuerte en todo: cada respuesta trae `concurrencyToken` (GUID) en el body **y** header `ETag`.

- **`PUT` / `PATCH` / `DELETE` REQUIEREN** `If-Match: "<concurrencyToken>"` del propio recurso.
  Faltante/malformado → `400`; stale → `409 CONCURRENCY_CONFLICT`.
- Cada mutación devuelve el token nuevo (body + `ETag`).
- **`POST` (crear) NO lleva `If-Match`**; responde `201` + `Location` + `ETag`.
- **`DELETE` de un sub‑recurso** devuelve `JobProfileParentConcurrencyResult`
  (`{ parentConcurrencyToken }`) — el token del **perfil padre** tras quitar el ítem; usalo para la
  siguiente mutación del shell.

## 6. `PATCH` = JSON Patch (RFC 6902) — array desnudo

> ⚠️ El esquema `{ "operations": [...] }` del Swagger es engañoso. El wire real es un **array
> desnudo** de operaciones.

- **Content-Type:** `application/json-patch+json`
- **Body:** array RFC 6902 (`add`/`replace`/`remove`), paths de primer nivel. Cada recurso define
  sus paths patchables; `remove` solo aplica a campos opcionales.

## 7. Paginación y búsqueda

- El **shell** (`GET /companies/{id}/job-profiles`) es paginado: `page` (1), `pageSize` (20, máx
  100), filtros (`status`, `orgUnitPublicId`, `salaryClassPublicId`), `q` (máx 150),
  `includeAllowedActions`.
- Las **listas de sub‑recursos** (`GET /job-profiles/{id}/<recurso>`) son paginadas con `page`/`pageSize`,
  **excepto `compensations`** (array completo — es uno-por-perfil).

## 8. Patrón canónico de los 9 sub‑recursos

Todos cuelgan de `/api/v1/job-profiles/{jobProfilePublicId}/<recurso>` con CRUD completo:

```
GET    .../<recurso>                       listar
GET    .../<recurso>/{<recurso>PublicId}   detalle
POST   .../<recurso>                       crear (201 + Location + ETag)
PUT    .../<recurso>/{id}      If-Match     reemplazar
PATCH  .../<recurso>/{id}      If-Match     JSON Patch
DELETE .../<recurso>/{id}      If-Match     quitar (→ parentConcurrencyToken)
```

Casi todos comparten: `catalogItemPublicId` (FK opcional a un [Job Catalog](./job-catalogs.md)),
`sortOrder` (≥0, orden de presentación), `notes`. Cada doc detalla sus campos propios.

## 9. Errores (ProblemDetails)

RFC 7807 con `code` estable + `traceId`. Comunes a la familia:

| HTTP | `code` típico | Cuándo |
|------|---------------|--------|
| `400` | `common.validation` | validación, If-Match faltante, JSON Patch inválido |
| `401` | — | token faltante/expirado |
| `403` | `JOB_PROFILES_FORBIDDEN` | sin permiso |
| `404` | `JOB_PROFILE_NOT_FOUND` / `JOB_PROFILE_<RECURSO>_NOT_FOUND` / `JOB_CATALOG_ITEM_NOT_FOUND` | perfil, ítem o catálogo inexistente |
| `409` | `CONCURRENCY_CONFLICT` | If-Match stale |
| `409` | `JOB_CATALOG_ITEM_INACTIVE` | el catálogo referenciado está inactivo |
| `409` | `JOB_PROFILE_CODE_CONFLICT` / `JOB_PROFILE_DEPENDENCY_CYCLE` | código duplicado / ciclo en jerarquía |
| `422` | `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` | publicar sin los prerrequisitos |

La lógica del FE siempre sobre `code`, nunca sobre el mensaje (se localiza en/es por `Accept-Language`).

## 10. Catálogos que alimentan los forms

Cada campo `*CatalogItemPublicId` apunta a un [Job Catalog](./job-catalogs.md) de cierta categoría.
En vez de hardcodear, el shell expone **`GET /job-profiles/catalog-manifest`** que describe, por
sub‑recurso y campo, qué catálogo lo alimenta y desde qué endpoint cargarlo. Ver
[job-profiles.md](./job-profiles.md).

---

### Índice

Volvé al [README](./README.md) para el modelo de datos y la lista de documentos por recurso.
