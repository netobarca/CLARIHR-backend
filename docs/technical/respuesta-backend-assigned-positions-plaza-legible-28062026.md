# Respuesta backend — `GET assigned-positions`: 422 + texto legible de plaza

| | |
| --- | --- |
| **Endpoint** | `GET /api/v1/personnel-files/{publicId}/assigned-positions` (y `/{employmentAssignmentPublicId}`) |
| **Reporte FE** | 2026-06-27 — sección **Sustituciones** (combobox de plazas del sustituto) |
| **Respuesta** | 2026-06-28 |
| **Estado** | Hallazgo 1: ✅ ya resuelto (retest) · Hallazgo 2: ✅ implementado |

---

## Hallazgo 1 — El GET devolvía 422 → **ya resuelto en `master`**

Tenían razón en el principio (un GET de lectura no debe devolver 422), y **ya estaba corregido** antes de este reporte. Probaron contra un build anterior al merge.

- El handler de lectura (lista y por-id) usa `LoadForReadAsync` → solo valida **auth (401) / permiso (403) / existencia+tenant (404)**. **No hay gate de estado.** Lee en **cualquier** `lifecycleStatus` (Draft incluido) y para **cualquier** `recordType`, devolviendo `200` con la lista (posiblemente vacía).
- El `PERSONNEL_FILE_STATE_RULE_VIOLATION` (422) hoy **solo** existe en los handlers de **escritura** (POST/PUT/PATCH/DELETE) y únicamente cuando el registro es **Candidato** (`recordType != Employee`). La **lectura nunca lo lanza**.
- El contrato del GET ya declara exactamente `200/401/403/404` (sin 422) — coincide con el runtime.

**Acción FE:** reprobar contra el deploy que incluya el merge de `master` (la corrección entró el 2026-06-27 y se mergeó vía PR #52 el 2026-06-28). Si en su ambiente aún sale 422, es **rezago de despliegue**, no código. Su filtro a `lifecycleStatus === 'Completed'` para los sustitutos sigue siendo razonable a nivel UX, pero ya **no** es necesario para evitar el 422.

---

## Hallazgo 2 — Texto legible de la plaza → **implementado**

La respuesta `PersonnelFileEmploymentAssignmentResponse` ahora incluye **dos campos legibles**, resueltos en backend desde la plaza referenciada:

| Campo | Tipo | Notas |
| --- | --- | --- |
| `positionSlotCode` | `string \| null` | Código de la plaza (p. ej. `PS-MP-A`). Se devuelve **tal cual** se almacenó (no se normaliza). |
| `positionSlotTitle` | `string \| null` | Título/nombre de la plaza (p. ej. `Plaza MP A`). |

Detalles:

- Aparecen en **todas** las respuestas de assigned-positions: `GET` lista, `GET` por id, y también en las respuestas de `POST`/`PUT`/`PATCH` (consistencia: no verán `null` justo después de crear/editar).
- **Semántica de `null`:** la referencia a plaza es un id suelto (no FK), así que si la asignación no tiene plaza o la plaza fue eliminada, ambos campos vienen `null`. **Recomendación de UI:** usar `positionSlotTitle ?? positionSlotCode` como etiqueta, y solo como último recurso el id — nunca el UUID crudo.
- Resolución eficiente: la lista hace **una** consulta extra por lote (no N+1).

### Sobre el "solo trae IDs"

El id de la plaza en la respuesta es **`positionSlotPublicId`** (UUID) — ya estaba presente y es correcto (la API expone los GUID públicos como `...PublicId`). Lo que faltaba era el texto, que es justo lo que agregan `positionSlotCode` / `positionSlotTitle`. Con eso el combobox arma el `value` con `positionSlotPublicId` y el `label` con `positionSlotTitle`/`positionSlotCode`, sin llamadas extra a `GET /position-slots/{id}`.

### No incluido (por alcance)

Pidieron mínimo `code` + `title`; eso es lo entregado. **`orgUnitName` no se incluyó** en esta iteración (se acordó code+title). Si lo necesitan para el combobox, lo agregamos igual (mismo patrón).

---

## Contrato

`docs/technical/api/openapi.yaml` → esquema `PersonnelFileEmploymentAssignmentResponse` actualizado con `positionSlotCode` y `positionSlotTitle` (ambos `string`, `nullable`). Aplica a todos los endpoints de assigned-positions (comparten el esquema por `$ref`).

## Verificación

- Build limpio (0 warnings / 0 errores); **2019** unit tests verdes.
- Integración (Docker `clarihr-postgres` :5433): assigned-positions **11/11**, incluido un test nuevo que valida `positionSlotCode`/`positionSlotTitle` en la respuesta de `POST` y en la lista; Rehire + AllowedActions **13/13** sin regresiones.
