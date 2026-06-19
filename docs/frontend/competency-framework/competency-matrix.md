# Competency Framework — Job Profile Competency Matrix

La **matriz de competencias de un job profile**: define, por nivel de pirámide ocupacional y
competencia, qué se espera (conductas + evidencia). Cuelga de un [Job Profile](../job-profiles/job-profiles.md)
(Fase 10) y es la versión estructurada de sus "competencias legacy".

> Leé el [README](./README.md) (convenciones de la familia). Acá solo lo específico.

**Permisos:** `GET` → `CompetencyFramework.Read` · `PUT` → `CompetencyFramework.Admin`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/job-profiles/{jobProfilePublicId}/competency-matrix` | leer la matriz (JSON) |
| `PUT` | `/job-profiles/{jobProfilePublicId}/competency-matrix` | **reemplazar** toda la matriz (`If-Match`) |
| `GET` | `/job-profiles/{jobProfilePublicId}/competency-matrix/export` | exportar (xlsx/…) |

## Concurrencia — IMPORTANTE

El `concurrencyToken` de la matriz **es el del job profile** (no uno propio). Obtenelo del `GET` de
la matriz (o del detalle del perfil) y mandalo en `If-Match` del `PUT`. Stale → `409`.

## `PUT` — reemplazar la matriz

Es un **replace completo**: mandás todos los items; lo que no venga se borra. Body:

| Campo | Tipo | Notas |
|-------|------|-------|
| `items` | array | máx **200** items; `[]` vacía la matriz |

Cada item de `items[]`:

| Campo | Tipo | Req. | FK / Validación |
|-------|------|------|-----------------|
| `occupationalPyramidLevelPublicId` | uuid | Sí | [Occupational Pyramid Level](./occupational-pyramid-levels.md) |
| `conductPublicIds` | uuid[] | Sí | [Competency Conducts](./competency-conducts.md); **mín 1**, máx **50** por item. Todas deben compartir la misma competencia/tipo/behaviorLevel |
| `expectedEvidence` | string | No | evidencia esperada (texto) |
| `sortOrder` | int | Sí | ≥ 0 |

```json
{
  "items": [
    {
      "occupationalPyramidLevelPublicId": "…",
      "conductPublicIds": ["…", "…"],
      "expectedEvidence": "Lidera reuniones de equipo",
      "sortOrder": 1
    }
  ]
}
```

> La competencia, el tipo y el behavior level del item **no se envían**: se **derivan** de las
> conductas (`conductPublicIds`). Cada conducta ya define esa terna, y todas las conductas de un item
> deben compartir la misma (si difieren → `409`). Por eso cada item exige **al menos una conducta**
> (item sin conductas → `400`). En la respuesta el item sí trae la terna ya resuelta, para mostrarla.

## Responses

`GET`/`PUT` → `JobProfileCompetencyMatrixResponse`: `jobProfilePublicId`, `jobProfileCode`,
`jobProfileTitle`, `jobProfileStatus`, `jobProfileVersion`, `concurrencyToken` (el del perfil),
`items[]` (con todas las referencias resueltas: códigos/nombres de nivel, competencia, tipo, behavior
level + los conducts), `allowedActions?`. Un perfil sin matriz aún devuelve `200` con `items: []`.

### `GET /export`
Descarga (query `format`, default `xlsx`). Matriz muy grande → `413`. Rate limit 10/min.

## Errores específicos

| `code` / situación | HTTP | Cuándo |
|--------------------|------|--------|
| `400` validación | 400 | error estructural/de campos, `If-Match` faltante, o **item sin conductas** |
| tuplas de item duplicadas / **conductas de distinta terna en un item** / **job profile `Archived`** | 409 | violación de restricción de la matriz |
| FK inexistente (nivel / conducta) | 404 | alguna referencia no existe |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` (token del perfil) stale |

## Reglas de negocio

- **No se puede editar la matriz de un job profile `Archived`** (`409`) — ver el
  [ciclo de vida del perfil](../job-profiles/job-profiles.md).
- Replace-all: para agregar/quitar un item, reenviá el set completo.
- Caps: **200 items**, **50 conducts por item**.

## Guía FE

- Editor de matriz: cargá los niveles de pirámide y las conductas (el listado de conductas es
  filtrable por `competencyId`/`competencyTypeId`/`behaviorLevelId`). Por cada item el usuario elige
  **una o más conductas que compartan terna** — la competencia/tipo/behaviorLevel se **derivan** de
  ellas, no se piden aparte. Guardá con un solo `PUT` (replace-all) usando el `concurrencyToken` del
  perfil.
- Si el perfil está `Archived`, deshabilitá la edición (read-only); el `GET` igual funciona.
