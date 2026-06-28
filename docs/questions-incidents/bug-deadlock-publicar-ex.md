# Bug Backend — Deadlock al publicar: exige plaza pero su regla de estado la bloquea

| | |
| --- | --- |
| **Endpoints** | `GET …/finalize/preview` ↔ `GET/POST …/assigned-positions` |
| **Severidad** | **Alta — bloquea la publicación de expedientes** (sin salida desde la UI) |
| **Tipo** | Contradicción entre reglas de estado del propio backend |
| **Fecha** | 2026-06-27 |
| **Estado** | 🔴 Abierto |

## Resumen

Para publicar (finalizar) un expediente en estado **Draft**, el backend **exige una plaza asignada**. Pero al intentar leer/gestionar las plazas de ese mismo expediente Draft, el backend responde **422 por regla de estado**. La condición que pide cumplir es **imposible de cumplir** por su propia regla → deadlock.

## Evidencia decisiva — mismo expediente Draft, segundos de diferencia

Expediente `e5ac558f-2a89-4fbb-8837-14886579f922`, estado Draft.

**1) `GET …/finalize/preview?CreateUserAccount=true` → 200** — EXIGE una plaza:

```jsonc
{
  "personnelFilePublicId": "e5ac558f-2a89-4fbb-8837-14886579f922",
  "isEligible": false,
  "issues": [{
    "code": "PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT",
    "message": "An assigned position slot is required to finalize the personnel file.",
    "section": "employment",
    "fieldKey": "assignedPositionSlotPublicId",
    "isBlocking": true
  }]
}
```

**2) `GET …/e5ac558f-…/assigned-positions` → 422** — PROHÍBE operar la plaza por estado:

```jsonc
{
  "status": 422,
  "code": "PERSONNEL_FILE_STATE_RULE_VIOLATION",
  "title": "La operacion solicitada no esta permitida para el personnel file state.",
  "traceId": "40001d4d-0002-f100-b63f-84710c7967bb"
}
```

## El problema

- **Contradicción interna del backend.** `finalize/preview` pide `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT`, pero `assigned-positions` rechaza con `PERSONNEL_FILE_STATE_RULE_VIOLATION` sobre el **mismo expediente en el mismo estado**.
- **No tiene salida desde la UI.** El usuario no puede agregar la plaza requerida porque el recurso de plazas está bloqueado por estado. La publicación queda imposible.
- **El FE no puede resolverlo.** Ambas llamadas son del expediente que se publica (no de un tercero). El FE necesita poder leer/crear plazas en Draft.

## Acción requerida

1. **Permitir gestionar `assigned-positions` (GET y POST) cuando el expediente está en Draft** — es prerrequisito directo para publicar. Quitar/ajustar la `PERSONNEL_FILE_STATE_RULE_VIOLATION` para este recurso en estado Draft.
2. Alternativamente, si `finalize/preview` espera que la plaza se provea por otra vía (p. ej. el `positionSlotPublicId` que el propio `finalize` recibe en el body), **documentarlo** y que `finalize/preview` deje de pedir `assigned-positions` cuando esa vía aplica.
3. Como mínimo, el **GET no debería ser 422** (es lectura): 200 con lista, o 403/404 — y reflejarlo en el contrato (hoy el GET solo declara 200/401/403/404).

## Nota FE

El FE ya desbloqueó la sección **Posiciones asignadas** en Draft (excepción `assignedPositions` en `DRAFT_UNLOCKED_SECTION_EXCEPTIONS`) y corrigió la navegación del issue de finalize para llevar a esa sección. Pero esos arreglos **no sirven** mientras el backend devuelva 422 al leer/crear plazas en Draft: el usuario llega a la sección y no puede operar. El desbloqueo real depende del backend.

Relacionado (mismo error code, contexto distinto): [BUG-BACKEND-assigned-positions-GET-422-y-sin-texto-plaza.md](BUG-BACKEND-assigned-positions-GET-422-y-sin-texto-plaza.md).
 