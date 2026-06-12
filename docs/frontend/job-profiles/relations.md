# Job Profiles — Relations

Las **relaciones** del puesto: con quién se relaciona (internas dentro de la organización o externas).
Sub‑recurso CRUD canónico bajo `/job-profiles/{jobProfilePublicId}/relations`.

> Leé las [Convenciones](./_conventions.md) (patrón de sub‑recurso §8). Acá solo lo específico.
> **Path param del ítem:** `relationPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobProfiles.Admin`.
Lista **paginada**.

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `relationType` | enum string | Sí | `Internal` \| `External` |
| `counterpart` | string | Sí | máx 500 (con quién se relaciona) |
| `catalogItemPublicId` | uuid | No | catálogo del tipo de relación (categoría `RelationType`) |
| `notes` | string | No | máx 1000 |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{ "relationType": "Internal", "counterpart": "Gerencia de Finanzas", "sortOrder": 1 }
```

**Patch** patchables: `/relationType`, `/catalogItemPublicId`, `/counterpart`, `/notes`, `/sortOrder`.

## Responses

`JobProfileRelationResponse`: `relationPublicId`, `relationType`, `counterpart`, `catalogItemPublicId`,
`notes`, `sortOrder`, `concurrencyToken`.

## Errores / reglas

- `JOB_PROFILE_RELATION_NOT_FOUND` (404), `JOB_CATALOG_ITEM_NOT_FOUND` (404), + comunes.
- `Internal` = relación dentro de la organización; `External` = con entidades externas
  (proveedores, clientes, entes). El `counterpart` describe la contraparte.
