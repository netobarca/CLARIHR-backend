# Bug Backend — `GET assigned-positions`: 422 indebido + sin texto legible de plaza

| | |
| --- | --- |
| **Endpoint** | `GET /api/v1/personnel-files/{publicId}/assigned-positions` |
| **Contexto** | Sección **Sustituciones** (combobox de plazas del sustituto) |
| **Fecha** | 2026-06-27 |
| **Estado** | 🟡 Abierto |

Dos hallazgos del mismo GET, detectados al poblar el combobox de plazas tras elegir a la persona sustituta.

---

## Hallazgo 1 — El GET devuelve 422 (no está en el contrato)

### Evidencia

```jsonc
GET …/personnel-files/67ad2dd7-85a4-4696-97df-b918d948a578/assigned-positions
// → 422
{
  "status": 422,
  "code": "PERSONNEL_FILE_STATE_RULE_VIOLATION",
  "title": "La operacion solicitada no esta permitida para el personnel file state.",
  "traceId": "40002abf-0002-f600-b63f-84710c7967bb"
}
```

### Problema

1. **Un GET de lectura no debería devolver 422.** Listar plazas asignadas es una consulta; no debería depender del "estado" del personnel file ni aplicar una *state rule* de escritura.
2. **El 422 ni está en el contrato.** La OpenAPI de este GET solo declara **200 / 401 / 403 / 404**.
3. **Semántica incorrecta.** Si por diseño un expediente en cierto estado (borrador/no finalizado) no debe exponer sus plazas, la respuesta correcta es **200 con lista vacía** (o **403/404** si es por visibilidad), nunca 422.

### Acción

1. ¿Por qué un **GET** aplica `PERSONNEL_FILE_STATE_RULE_VIOLATION`? Debería ser de solo lectura.
2. Cambiar a **200 con `[]`** cuando no haya plazas consultables (o 403/404 si es visibilidad), y actualizar el contrato con la respuesta real.
3. Aclarar qué estados impiden leer las plazas y por qué afecta a la lectura.

---

## Hallazgo 2 — La respuesta no expone texto legible de la plaza

### Problema

La respuesta (`PersonnelFileEmploymentAssignmentResponse`) **solo trae IDs**, ningún texto legible:

```
positionSlotPublicId: string(uuid)   ← solo el ID
(no hay positionSlotCode / positionSlotTitle / orgUnitName)
```

Como no hay nada legible que mostrar, el combobox usa el **UUID como etiqueta** → el usuario ve `3ec70a79-a673-…` en vez del nombre de la plaza.

Contraste: el combobox de "Persona relacionada" sí muestra texto porque su endpoint devuelve `fullName`. Y `GET /position-slots/{id}` (`PositionSlotResponse`) sí expone `code`/`title`/`orgUnitName`/`jobProfileTitle` — pero la lista de `assigned-positions` no propaga esos campos.

### Acción

1. Enriquecer `PersonnelFileEmploymentAssignmentResponse` con campos legibles de la plaza. Mínimo: **`positionSlotCode`** y **`positionSlotTitle`** (idealmente también `orgUnitName`).
2. Alternativa: admitir expandir la plaza (`?expand=positionSlot`) o exponer el título directamente.

---

## Nota FE

- El FE ya es resiliente al 422: `searchSubstitutePositionSlots` (en `authorization-substitutions-editor.component.ts`) envuelve la llamada en `try/catch` → `[]`, así que no rompe la pantalla; solo deja el combobox vacío.
- El UUID como etiqueta se debe a la falta de un campo legible en la respuesta. En cuanto se incluya `positionSlotCode`/`positionSlotTitle`, el FE cambiará el `label` a ese texto. La única alternativa local sería un `GET /position-slots/{id}` por cada plaza (N llamadas) — descartado por costo.
- No se hará cambio FE; ambos arreglos corresponden al backend.
 