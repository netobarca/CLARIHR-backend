# 🔴 Breaking Change — Matriz de Competencias del Job Profile

**Endpoint afectado:** la matriz dejó de tener un `PUT` masivo; ahora son operaciones CRUD por item bajo `/api/v1/job-profiles/{jobProfilePublicId}/competency-matrix/items`
**Fecha:** 2026-06-18
**Tipo:** Breaking change de contrato (se eliminó el `PUT` masivo `items[]`; pasa a CRUD por registro)
**Acción requerida:** Reescribir el flujo del editor de matriz: guardar **fila por fila**, no en bloque.

> Contexto histórico: el 2026-06-15 cambió el **body** del item (se quitaron `competencyPublicId`/`competencyTypePublicId`/`behaviorLevelPublicId`, la terna pasó a derivarse de las conductas). Ese cambio sigue vigente. Lo del 2026-06-18 es **adicional**: desaparece el reemplazo masivo de toda la matriz.

---

## TL;DR

1. **Ya NO existe** el `PUT /competency-matrix` con `{ "items": [...] }`. El **replace completo de la matriz se eliminó**.
2. El editor ahora opera **por item (por fila)**: `POST` para agregar una fila, `PUT`/`PATCH` para editar una fila, `DELETE` para quitar una fila.
3. El `If-Match` de cada mutación usa el **`concurrencyToken` de ESE item**, ya no el token del perfil. (Antes, el `PUT` masivo usaba el token del perfil — eso cambió.)
4. El `GET /competency-matrix` sigue devolviendo la matriz completa para mostrar/exportar, pero ahora **cada item trae su propio `itemPublicId` y su propio `concurrencyToken`**. A nivel raíz devuelve el `concurrencyToken`, `jobProfileStatus`, `jobProfileVersion` y `allowedActions` del perfil.
5. **Sigue igual (2026-06-15):** `conductPublicIds` es **obligatorio** (mín 1, máx 50 por item) y la competencia/tipo/nivel de comportamiento se **derivan** de las conductas (no se envían). Todas las conductas de un item deben compartir la misma terna.

---

## Endpoints (nuevo contrato)

Base: `/api/v1/job-profiles/{jobProfilePublicId}/competency-matrix`

| Método | Ruta | Para qué | `If-Match` | Respuesta |
|--------|------|----------|------------|-----------|
| `GET` | `/competency-matrix` | leer la matriz completa (mostrar/exportar) | — | matriz + `concurrencyToken`/`jobProfileStatus`/`jobProfileVersion`/`allowedActions` del perfil; cada item con `itemPublicId` + `concurrencyToken` |
| `GET` | `/competency-matrix/items/{itemPublicId}` | leer **un** item | — | el item (con su `concurrencyToken` en body y `ETag`) |
| `POST` | `/competency-matrix/items` | crear **un** item | — | `201 Created`; `Location` → el nuevo item; `ETag` = `concurrencyToken` del item |
| `PUT` | `/competency-matrix/items/{itemPublicId}` | reemplazar **un** item completo | **token del ITEM** | item actualizado (nuevo `concurrencyToken` en `ETag`) |
| `PATCH` | `/competency-matrix/items/{itemPublicId}` | actualización parcial (JSON Patch) | **token del ITEM** | item actualizado (nuevo `concurrencyToken` en `ETag`) |
| `DELETE` | `/competency-matrix/items/{itemPublicId}` | borrar **un** item | **token del ITEM** | `{ "parentConcurrencyToken": "…" }` (token del perfil, para seguir operando) |
| `GET` | `/competency-matrix/export?format=xlsx\|csv\|json` | exportar (sin cambios) | — | archivo |

| Header | Obligatorio | Notas |
|--------|-------------|-------|
| `Authorization: Bearer <token>` | Sí | — |
| `Content-Type: application/json` | Sí (POST/PUT) | Para `PATCH` usar `application/json-patch+json` |
| `If-Match: <itemConcurrencyToken>` | Sí en `PUT`/`PATCH`/`DELETE` | El token **del item** (no del perfil). Falta → `400`, desactualizado → `409`. Acepta el GUID con o sin comillas. |

> El `concurrencyToken` de cada item se obtiene del `GET` de la matriz (campo por item), del `GET` del item, o del `ETag` que devuelve el `POST`/`PUT`/`PATCH` de ese item. **No va en el body.**

---

## Qué cambió (antes → después)

### ❌ Antes (ya NO existe): un `PUT` masivo reemplazaba toda la matriz

```http
PUT /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix
If-Match: <profileConcurrencyToken>
```

```json
{
  "items": [
    { "occupationalPyramidLevelPublicId": "…", "conductPublicIds": ["…"], "expectedEvidence": "…", "sortOrder": 1 },
    { "occupationalPyramidLevelPublicId": "…", "conductPublicIds": ["…"], "expectedEvidence": "…", "sortOrder": 2 }
  ]
}
```

Este endpoint **se eliminó**. Ya no hay forma de mandar `items[]` en bloque ni de "vaciar la matriz" con `items: []`.

### ✅ Después: CRUD por item

- **Agregar una fila** → `POST /competency-matrix/items` (un item).
- **Editar una fila** → `PUT` (reemplazo total del item) o `PATCH` (parcial) en `/competency-matrix/items/{itemPublicId}`.
- **Quitar una fila** → `DELETE /competency-matrix/items/{itemPublicId}`.
- Cada mutación usa el **`concurrencyToken` de ese item** en `If-Match`.

Body de `POST` y `PUT` (un solo item):

```json
{
  "occupationalPyramidLevelPublicId": "8a72dd45-6094-4565-acb5-a1e46bc3a4d9",
  "conductPublicIds": ["e1e4f00c-9d44-438f-9bcf-5a939c7112ff", "2b1c0d9e-7a64-4f21-8c3a-9d4e5f6a7b80"],
  "expectedEvidence": "Lidera reuniones de equipo",
  "sortOrder": 1
}
```

---

## Campos del item (POST / PUT)

| Campo | Tipo | Obligatorio | Reglas |
|-------|------|-------------|--------|
| `occupationalPyramidLevelPublicId` | uuid | Sí | Nivel de la pirámide ocupacional |
| `conductPublicIds` | uuid[] | **Sí** | **Mín 1**, máx **50**. Todas deben compartir la misma competencia/tipo/nivel de comportamiento |
| `expectedEvidence` | string | No | Texto (máx 1000) |
| `sortOrder` | int | Sí | ≥ 0 |

A nivel del perfil: **máx 200 items** en total. Al superar el cap, el `POST` falla con `409 JOB_PROFILE_COMPETENCY_MATRIX_ITEM_LIMIT_REACHED`.

> **Campos derivados (no se envían):** la competencia, el tipo de competencia y el nivel de comportamiento del item se calculan a partir de las conductas. Si seguís enviando `competencyPublicId`/`competencyTypePublicId`/`behaviorLevelPublicId`, se **ignoran**.

---

## Reglas de negocio (vigentes)

- **CRUD por registro:** quitar una fila = `DELETE` ese item; agregar = `POST`; editar = `PUT`/`PATCH` ese item. **No** hay reemplazo masivo.
- **Concurrencia por item:** cada `PUT`/`PATCH`/`DELETE` exige `If-Match` con el `concurrencyToken` **del item**. Tras cada mutación, el item devuelve un **nuevo** token (en `ETag`): guardalo para la siguiente edición de esa fila.
- **Derivación:** la competencia/tipo/nivel del item se calculan de las conductas. No se envían.
- **≥1 conducta por item:** un item sin conductas se rechaza con `400`.
- **Misma terna:** si un item mezcla conductas de distinta competencia/tipo/nivel → `409`.
- **Sin duplicados:** dos items con la misma tupla (mismo nivel de pirámide + misma terna derivada) → `409`.
- **Cap por perfil:** máximo **200 items**; al excederlo el `POST` → `409 JOB_PROFILE_COMPETENCY_MATRIX_ITEM_LIMIT_REACHED`.
- **Perfil debe existir.** Un perfil `Archived` **no** admite editar la matriz (`409`).
- **La respuesta de lectura no cambió:** cada item sigue trayendo la terna **ya resuelta** (códigos/nombres de competencia, tipo, nivel) + sus conductas; ahora además trae `itemPublicId` y `concurrencyToken`.

---

## Ejemplos (cURL)

> Reemplazá `{token}` por tu Bearer. Los UUIDs son de ejemplo.

### Crear una fila — `POST`

```bash
curl -X POST "https://apiclarihrdev.azurewebsites.net/api/v1/job-profiles/3f9a1c2d-5e6b-4a7c-8d9e-0f1a2b3c4d5e/competency-matrix/items" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "occupationalPyramidLevelPublicId": "8a72dd45-6094-4565-acb5-a1e46bc3a4d9",
    "conductPublicIds": ["e1e4f00c-9d44-438f-9bcf-5a939c7112ff"],
    "expectedEvidence": "Lidera reuniones de equipo",
    "sortOrder": 1
  }'
# 201 Created
# Location: .../competency-matrix/items/7c4e2a10-9b3d-4f56-a1c8-2d3e4f5a6b7c
# ETag: "b2d4f6a8-1357-9bdf-2468-ace013579bdf"   ← concurrencyToken del item nuevo
```

### Reemplazar una fila completa — `PUT`

```bash
curl -X PUT "https://apiclarihrdev.azurewebsites.net/api/v1/job-profiles/3f9a1c2d-5e6b-4a7c-8d9e-0f1a2b3c4d5e/competency-matrix/items/7c4e2a10-9b3d-4f56-a1c8-2d3e4f5a6b7c" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -H "If-Match: \"b2d4f6a8-1357-9bdf-2468-ace013579bdf\"" \
  -d '{
    "occupationalPyramidLevelPublicId": "8a72dd45-6094-4565-acb5-a1e46bc3a4d9",
    "conductPublicIds": ["e1e4f00c-9d44-438f-9bcf-5a939c7112ff", "2b1c0d9e-7a64-4f21-8c3a-9d4e5f6a7b80"],
    "expectedEvidence": "Lidera y facilita reuniones de equipo",
    "sortOrder": 1
  }'
# 200 OK · nuevo ETag con el concurrencyToken actualizado del item
```

### Actualización parcial — `PATCH` (JSON Patch, RFC 6902)

Media type **`application/json-patch+json`**. Paths permitidos: `/occupationalPyramidLevelPublicId`, `/conductPublicIds`, `/expectedEvidence`, `/sortOrder`.

```bash
curl -X PATCH "https://apiclarihrdev.azurewebsites.net/api/v1/job-profiles/3f9a1c2d-5e6b-4a7c-8d9e-0f1a2b3c4d5e/competency-matrix/items/7c4e2a10-9b3d-4f56-a1c8-2d3e4f5a6b7c" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json-patch+json" \
  -H "If-Match: \"b2d4f6a8-1357-9bdf-2468-ace013579bdf\"" \
  -d '[
    { "op": "replace", "path": "/sortOrder", "value": 3 },
    { "op": "replace", "path": "/expectedEvidence", "value": "Evidencia actualizada" },
    { "op": "replace", "path": "/conductPublicIds", "value": ["e1e4f00c-9d44-438f-9bcf-5a939c7112ff"] }
  ]'
# 200 OK · nuevo ETag
```

### Borrar una fila — `DELETE`

```bash
curl -X DELETE "https://apiclarihrdev.azurewebsites.net/api/v1/job-profiles/3f9a1c2d-5e6b-4a7c-8d9e-0f1a2b3c4d5e/competency-matrix/items/7c4e2a10-9b3d-4f56-a1c8-2d3e4f5a6b7c" \
  -H "Authorization: Bearer {token}" \
  -H "If-Match: \"b2d4f6a8-1357-9bdf-2468-ace013579bdf\""
# 200 OK
# { "parentConcurrencyToken": "f0e1d2c3-b4a5-6978-8a9b-0c1d2e3f4a5b" }   ← token del perfil
```

### Leer la matriz completa — `GET`

```bash
curl "https://apiclarihrdev.azurewebsites.net/api/v1/job-profiles/3f9a1c2d-5e6b-4a7c-8d9e-0f1a2b3c4d5e/competency-matrix" \
  -H "Authorization: Bearer {token}"
# 200 OK · raíz: concurrencyToken, jobProfileStatus, jobProfileVersion, allowedActions
#          items[]: cada item con itemPublicId + concurrencyToken + la terna resuelta + conducts[]
```

---

## Tabla de errores

| HTTP | `code` / situación | Causa |
|------|--------------------|-------|
| `400` | validación | `If-Match` faltante (en `PUT`/`PATCH`/`DELETE`), **item sin conductas**, o campos inválidos |
| `404` | `JOB_PROFILE_NOT_FOUND` | el job profile no existe |
| `404` | `JOB_PROFILE_COMPETENCY_MATRIX_ITEM_NOT_FOUND` | el `itemPublicId` no existe en ese perfil |
| `404` | FK inexistente (nivel o conducta) | alguna referencia no existe o está inactiva |
| `409` | `CONCURRENCY_CONFLICT` | `If-Match` con el token del **item** desactualizado (stale) |
| `409` | `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT` | tupla de item duplicada, **conductas de distinta terna en un item**, o perfil `Archived` |
| `409` | `JOB_PROFILE_COMPETENCY_MATRIX_ITEM_LIMIT_REACHED` | se alcanzó el cap de **200 items** por perfil (al hacer `POST`) |

> El detalle por campo viene en el objeto `errors` del `ProblemDetails` de la respuesta. Loguealo para depurar sin adivinar.

---

## Cómo migrar el editor de matriz (UI)

El cambio de fondo: **se guarda por fila, no en bloque.** Cada fila de la grilla es una entidad con su propio `itemPublicId` y su propio `concurrencyToken`.

**Flujo recomendado:**

1. Al abrir el editor, hacé `GET /competency-matrix`. Guardá por cada fila su `itemPublicId` y su `concurrencyToken` (además del token del perfil para contexto).
2. **Agregar fila:** el usuario arma el item (conductas → la terna se deriva; nivel de pirámide, evidencia, orden) y disparás `POST /competency-matrix/items`. Guardá el `itemPublicId` (del `Location`) y el `concurrencyToken` (del `ETag`) en esa fila.
3. **Editar fila:** disparás `PUT` (reemplazo total) o `PATCH` (cambios puntuales) sobre `/items/{itemPublicId}` con `If-Match: <token del item>`. Actualizá el token de la fila con el `ETag` de la respuesta.
4. **Quitar fila:** `DELETE /items/{itemPublicId}` con `If-Match: <token del item>`. Quitá la fila de la grilla; podés usar `parentConcurrencyToken` de la respuesta para refrescar el contexto del perfil.
5. **Mapeo al editar:** del `GET`, cada item trae sus conductas resueltas como `conducts[]`. Para un `PUT`/`PATCH` mapeá `conducts[].conductId` → `conductPublicIds` (lo que el body espera). La terna (competencia/tipo/nivel) sigue siendo **derivada**, no la envíes.

La conducta sigue siendo el punto de entrada: filtrá el selector para mostrar solo conductas de la misma terna y así nunca armás una combinación inválida.

---

## Endpoints de apoyo

**Listar conductas (filtrable por terna):**

```
GET /api/v1/companies/{companyId}/competency-conducts?competencyId={…}&competencyTypeId={…}&behaviorLevelId={…}&isActive=true
```

Cada conducta del listado expone `competencyId`, `competencyTypeId`, `behaviorLevelId` — útil para derivar la terna o para filtrar conductas compatibles dentro de un mismo item.

**Obtener la matriz actual (con tokens por item):**

```
GET /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix
```

Devuelve el `concurrencyToken` del perfil + `jobProfileStatus`/`jobProfileVersion`/`allowedActions`, y cada item con su `itemPublicId`, su `concurrencyToken` y la terna resuelta.

---

## Checklist de migración (frontend)

- [ ] **Eliminar** la llamada al `PUT /competency-matrix` masivo (`items[]`) y el "Guardar todo" en bloque.
- [ ] Cambiar a **guardado por fila**: `POST` (agregar), `PUT`/`PATCH` (editar), `DELETE` (quitar) sobre `/competency-matrix/items[/{itemPublicId}]`.
- [ ] **Rastrear por cada fila** su `itemPublicId` y su `concurrencyToken`; actualizar el token con el `ETag` tras cada mutación.
- [ ] Usar el `concurrencyToken` **del item** en `If-Match` (ya **no** el del perfil).
- [ ] Al editar, mapear `conducts[].conductId` (del `GET`) → `conductPublicIds` en el body del `PUT`/`PATCH`.
- [ ] Garantizar **≥1 conducta por item** (bloquear "Guardar" de la fila si no tiene conductas).
- [ ] Filtrar/validar que **todas las conductas de un item compartan terna**.
- [ ] No enviar `competencyPublicId`/`competencyTypePublicId`/`behaviorLevelPublicId` (derivados).
- [ ] Para `PATCH`, usar media type `application/json-patch+json` y solo los paths permitidos.
- [ ] Manejar errores: `400` (validación/`If-Match`), `404` (`JOB_PROFILE_NOT_FOUND` / `JOB_PROFILE_COMPETENCY_MATRIX_ITEM_NOT_FOUND` / FK), `409` (`CONCURRENCY_CONFLICT` / `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT` / `JOB_PROFILE_COMPETENCY_MATRIX_ITEM_LIMIT_REACHED`) con mensajes claros.
- [ ] El parseo de la **respuesta de lectura** no cambia salvo los nuevos campos `itemPublicId` y `concurrencyToken` por item (y a nivel raíz `allowedActions`).
