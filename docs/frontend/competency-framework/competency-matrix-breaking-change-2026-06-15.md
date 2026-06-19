# 🔴 Breaking Change — Matriz de Competencias del Job Profile

**Endpoint afectado:** `PUT /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix`
**Fecha:** 2026-06-15
**Tipo:** Breaking change de contrato (el body del item cambió)
**Acción requerida:** Actualizar el payload y el flujo del editor de matriz.

---

## TL;DR

1. El item de la matriz **ya no envía** `competencyPublicId`, `competencyTypePublicId` ni `behaviorLevelPublicId`. Esos 3 campos **se eliminaron**.
2. El backend ahora **deriva** la competencia/tipo/nivel de las **conductas** del item.
3. `conductPublicIds` pasó a ser **obligatorio** con **mínimo 1** conducta por item.
4. **Todas las conductas de un item deben pertenecer a la misma** competencia + tipo + nivel de comportamiento.
5. El `concurrencyToken` va en el header **`If-Match`** (esto ya era así, no cambia).

---

## El endpoint

```
PUT /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix
```

| Header | Obligatorio | Notas |
|--------|-------------|-------|
| `Authorization: Bearer <token>` | Sí | — |
| `Content-Type: application/json` | Sí | — |
| `If-Match: <concurrencyToken>` | Sí | El token **del job profile** (no de la matriz). Falta → `400`, desactualizado → `409`. Acepta el GUID con o sin comillas. |

> El `concurrencyToken` se obtiene del `GET` de la matriz o del detalle del perfil. **No va en el body.**

---

## Qué cambió (antes → después)

### ❌ Antes (ya no válido)

```json
{
  "items": [
    {
      "occupationalPyramidLevelPublicId": "…",
      "competencyPublicId": "…",
      "competencyTypePublicId": "…",
      "behaviorLevelPublicId": "…",
      "conductPublicIds": ["…"],
      "expectedEvidence": "…",
      "sortOrder": 1
    }
  ]
}
```

### ✅ Después (nuevo contrato)

```json
{
  "items": [
    {
      "occupationalPyramidLevelPublicId": "…",
      "conductPublicIds": ["…", "…"],
      "expectedEvidence": "…",
      "sortOrder": 1
    }
  ]
}
```

---

## Campos del item

| Campo | Tipo | Obligatorio | Reglas |
|-------|------|-------------|--------|
| `occupationalPyramidLevelPublicId` | uuid | Sí | Nivel de la pirámide ocupacional |
| `conductPublicIds` | uuid[] | **Sí** | **Mín 1**, máx **50**. Todas deben compartir la misma competencia/tipo/nivel de comportamiento |
| `expectedEvidence` | string | No | Texto (máx 1000) |
| `sortOrder` | int | Sí | ≥ 0 |

A nivel de la matriz: `items` admite **máx 200** items; `items: []` **vacía** la matriz (borra todas las expectativas).

> **Campos eliminados:** `competencyPublicId`, `competencyTypePublicId`, `behaviorLevelPublicId`. Si los seguís enviando, se **ignoran** (no rompe, pero son inútiles).

---

## Reglas de negocio

- **Derivación:** la competencia/tipo/nivel del item se calculan a partir de las conductas. No se envían.
- **≥1 conducta por item:** un item sin conductas se rechaza con `400`.
- **Misma terna:** si un item mezcla conductas de distinta competencia/tipo/nivel → `409`.
- **Replace completo:** el `PUT` reemplaza toda la matriz; reenviá siempre el set completo de items.
- **Perfil debe existir** (en `Draft` o `Published`). Un perfil `Archived` no admite editar la matriz (`409`).
- **La respuesta no cambia:** el `GET`/`PUT` siguen devolviendo cada item con la terna **ya resuelta** (códigos/nombres de competencia, tipo, nivel) + sus conductas. Solo cambió lo que se **envía**, no lo que se **lee**.

---

## Ejemplo completo (cURL)

```bash
curl -X PUT "https://apiclarihrdev.azurewebsites.net/api/v1/job-profiles/{jobProfilePublicId}/competency-matrix" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -H "If-Match: {profileConcurrencyToken}" \
  -d '{
    "items": [
      {
        "occupationalPyramidLevelPublicId": "8a72dd45-6094-4565-acb5-a1e46bc3a4d9",
        "conductPublicIds": ["e1e4f00c-9d44-438f-9bcf-5a939c7112ff"],
        "expectedEvidence": "Lidera reuniones de equipo",
        "sortOrder": 1
      }
    ]
  }'
```

---

## Tabla de errores

| HTTP | `code` / situación | Causa |
|------|--------------------|-------|
| `400` | validación | `If-Match` faltante, **item sin conductas**, o campos inválidos |
| `404` | `JOB_PROFILE_NOT_FOUND` | el job profile no existe |
| `404` | FK inexistente (nivel o conducta) | alguna referencia no existe o está inactiva |
| `409` | `CONCURRENCY_CONFLICT` | `If-Match` (token del perfil) desactualizado |
| `409` | `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT` | tuplas de item duplicadas, **conductas de distinta terna en un item**, o perfil `Archived` |

> El detalle por campo viene en el objeto `errors` del `ProblemDetails` de la respuesta. Loguealo para depurar sin adivinar.

---

## Cómo migrar el editor de matriz (UI)

El cambio simplifica el flujo: **la conducta es el punto de entrada**, no la competencia.

**Flujo recomendado por item:**

1. El usuario selecciona **una o más conductas** de "Conductas de Competencias".
2. El front **deriva** `competencyId` / `competencyTypeId` / `behaviorLevelId` del objeto conducta (campos calculados, no editables; ni siquiera hace falta mostrarlos como inputs).
3. Si el item permite varias conductas, **filtrá el selector** para mostrar solo conductas de la misma terna (ver endpoint de apoyo abajo), así nunca se arma una combinación inválida.
4. El usuario completa lo propio del item: **nivel de pirámide**, **evidencia esperada**, **orden**.
5. Enviás solo: `occupationalPyramidLevelPublicId` + `conductPublicIds` + `expectedEvidence` + `sortOrder`.

Esto elimina tener que "recordar combinaciones" y hace **imposible** el `409` por mismatch.

---

## Endpoints de apoyo

**Listar conductas (filtrable por terna):**

```
GET /api/v1/companies/{companyId}/competency-conducts?competencyId={…}&competencyTypeId={…}&behaviorLevelId={…}&isActive=true
```

Cada conducta del listado expone `competencyId`, `competencyTypeId`, `behaviorLevelId` — útil para derivar la terna o para filtrar conductas compatibles dentro de un mismo item.

**Obtener el token del perfil + matriz actual:**

```
GET /api/v1/job-profiles/{jobProfilePublicId}/competency-matrix
```

Devuelve `concurrencyToken` (el del perfil, para el `If-Match`) y los `items` actuales con la terna resuelta.

---

## Checklist de migración (frontend)

- [ ] Quitar `competencyPublicId`, `competencyTypePublicId`, `behaviorLevelPublicId` del payload de cada item.
- [ ] Garantizar **≥1 conducta por item** (bloquear "Guardar" si algún item no tiene conductas).
- [ ] Filtrar/validar que **todas las conductas de un item compartan terna**.
- [ ] Enviar el `concurrencyToken` **del perfil** en el header `If-Match` (no en el body).
- [ ] Derivar/mostrar la terna desde la conducta seleccionada (read-only).
- [ ] Manejar `400` (validación), `404` (FK/perfil), `409` (token / mismatch / archived) con mensajes claros.
- [ ] El parseo de la **respuesta** no cambia: los items siguen trayendo la terna resuelta.
