# Competency Framework — Job Profile Competency Matrix

La **matriz de competencias de un job profile**: define, por nivel de pirámide ocupacional y
competencia, qué se espera (conductas + evidencia). Cuelga de un [Job Profile](../job-profiles/job-profiles.md)
(Fase 10) y es la versión estructurada de sus "competencias legacy".

> Leé el [README](./README.md) (convenciones de la familia). Acá solo lo específico.

**Permisos:** `GET` → `CompetencyFramework.Read` · escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) → `CompetencyFramework.Admin`.

> 🔴 **Breaking change (2026-06-18):** se eliminó el `PUT` masivo `…/competency-matrix` con `items[]`
> (replace completo). La matriz ahora se edita **por registro** bajo `…/competency-matrix/items`.
> Ver [competency-matrix-breaking-change-2026-06-15.md](./competency-matrix-breaking-change-2026-06-15.md).

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/job-profiles/{jobProfilePublicId}/competency-matrix` | leer la matriz completa (JSON) |
| `GET` | `/job-profiles/{jobProfilePublicId}/competency-matrix/items/{itemPublicId}` | leer **un** item |
| `POST` | `/job-profiles/{jobProfilePublicId}/competency-matrix/items` | crear **un** item (`201`) |
| `PUT` | `/job-profiles/{jobProfilePublicId}/competency-matrix/items/{itemPublicId}` | reemplazar **un** item (`If-Match`) |
| `PATCH` | `/job-profiles/{jobProfilePublicId}/competency-matrix/items/{itemPublicId}` | actualización parcial JSON Patch (`If-Match`) |
| `DELETE` | `/job-profiles/{jobProfilePublicId}/competency-matrix/items/{itemPublicId}` | borrar **un** item (`If-Match`) |
| `GET` | `/job-profiles/{jobProfilePublicId}/competency-matrix/export` | exportar (xlsx/csv/json) |

## Concurrencia — IMPORTANTE

El `concurrencyToken` ahora es **por item**, no del perfil. Cada item del `GET` trae su propio
`concurrencyToken` (y su `itemPublicId`); mandalo en `If-Match` del `PUT`/`PATCH`/`DELETE` de **ese**
item. El `POST`/`PUT`/`PATCH` devuelven el nuevo token del item en el `ETag`. Stale → `409`.
A nivel raíz, el `GET` igual expone el `concurrencyToken` del perfil (contexto) + `jobProfileStatus`,
`jobProfileVersion` y `allowedActions`. El `DELETE` devuelve `{ parentConcurrencyToken }` (el del perfil).

## Crear / editar / borrar un item

La matriz se edita **fila por fila**: `POST` agrega, `PUT`/`PATCH` editan, `DELETE` quita. **No** hay
reemplazo masivo. Body de `POST` y `PUT` (un solo item):

| Campo | Tipo | Req. | FK / Validación |
|-------|------|------|-----------------|
| `occupationalPyramidLevelPublicId` | uuid | Sí | [Occupational Pyramid Level](./occupational-pyramid-levels.md) |
| `conductPublicIds` | uuid[] | Sí | [Competency Conducts](./competency-conducts.md); **mín 1**, máx **50** por item. Todas deben compartir la misma competencia/tipo/behaviorLevel |
| `expectedEvidence` | string | No | evidencia esperada (texto) |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{
  "occupationalPyramidLevelPublicId": "…",
  "conductPublicIds": ["…", "…"],
  "expectedEvidence": "Lidera reuniones de equipo",
  "sortOrder": 1
}
```

`PATCH` usa JSON Patch (RFC 6902, media type `application/json-patch+json`); paths permitidos:
`/occupationalPyramidLevelPublicId`, `/conductPublicIds`, `/expectedEvidence`, `/sortOrder`.

> La competencia, el tipo y el behavior level del item **no se envían**: se **derivan** de las
> conductas (`conductPublicIds`). Cada conducta ya define esa terna, y todas las conductas de un item
> deben compartir la misma (si difieren → `409`). Por eso cada item exige **al menos una conducta**
> (item sin conductas → `400`). En la respuesta el item sí trae la terna ya resuelta, para mostrarla.

## Responses

`GET` (matriz) → `JobProfileCompetencyMatrixResponse`: `jobProfilePublicId`, `jobProfileCode`,
`jobProfileTitle`, `jobProfileStatus`, `jobProfileVersion`, `concurrencyToken` (el del perfil),
`items[]` (cada item con su `itemPublicId` y su `concurrencyToken`, y con todas las referencias
resueltas: códigos/nombres de nivel, competencia, tipo, behavior level + los conducts),
`allowedActions?`. Un perfil sin matriz aún devuelve `200` con `items: []`.

`POST`/`PUT`/`PATCH`/`GET` de un item → el item resuelto, con su `concurrencyToken` en el `ETag` (el
`POST` además devuelve `201` con `Location` al nuevo item). `DELETE` → `{ parentConcurrencyToken }`.

### `GET /export`
Descarga (query `format`, default `xlsx`). Matriz muy grande → `413`. Rate limit 10/min.

## Errores específicos

| `code` / situación | HTTP | Cuándo |
|--------------------|------|--------|
| `400` validación | 400 | error estructural/de campos, `If-Match` faltante, o **item sin conductas** |
| `JOB_PROFILE_NOT_FOUND` | 404 | el job profile no existe |
| `JOB_PROFILE_COMPETENCY_MATRIX_ITEM_NOT_FOUND` | 404 | el `itemPublicId` no existe en ese perfil |
| FK inexistente (nivel / conducta) | 404 | alguna referencia no existe |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` (token del **item**) stale |
| `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT` | 409 | tupla de item duplicada, **conductas de distinta terna en un item**, o job profile `Archived` |
| `JOB_PROFILE_COMPETENCY_MATRIX_ITEM_LIMIT_REACHED` | 409 | se alcanzó el cap de **200 items** (al hacer `POST`) |

## Reglas de negocio

- **No se puede editar la matriz de un job profile `Archived`** (`409`) — ver el
  [ciclo de vida del perfil](../job-profiles/job-profiles.md).
- CRUD por item: para agregar/quitar/editar una fila, operá **solo sobre ese item** (`POST`/`PUT`/`PATCH`/`DELETE`); no hay replace-all.
- Caps: **200 items** por perfil, **50 conducts por item**.

## Guía FE

- Editor de matriz: cargá los niveles de pirámide y las conductas (el listado de conductas es
  filtrable por `competencyId`/`competencyTypeId`/`behaviorLevelId`). Por cada item el usuario elige
  **una o más conductas que compartan terna** — la competencia/tipo/behaviorLevel se **derivan** de
  ellas, no se piden aparte. Guardá **fila por fila**: `POST` para agregar, `PUT`/`PATCH` para editar,
  `DELETE` para quitar, usando el `concurrencyToken` **de cada item** en `If-Match` (rastreá el token
  de cada fila y actualizalo con el `ETag` tras cada mutación). Al editar una fila, mapeá
  `conducts[].conductId` del `GET` → `conductPublicIds` en el body.
- Si el perfil está `Archived`, deshabilitá la edición (read-only); el `GET` igual funciona.
