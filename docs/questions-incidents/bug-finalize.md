# Bug Backend — `finalize` exige `positionSlotPublicId` aunque el empleado ya tiene plaza primaria

| | |
| --- | --- |
| **Endpoints** | `GET …/finalize/preview` · `PATCH …/finalize` |
| **Severidad** | Media-Alta — bloquea la publicación pese a tener los datos requeridos |
| **Tipo** | El backend no deduce la plaza primaria ya asignada |
| **Fecha** | 2026-06-27 |
| **Estado** | 🟡 Abierto |

## Resumen

El empleado **ya tiene una plaza asignada** (en `assigned-positions`, una marcada `isPrimary: true`). Aun así, `finalize/preview` responde `isEligible: false` con el issue `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT`. El backend solo da por satisfecho el requisito si el cliente **reenvía** `positionSlotPublicId`, en vez de **deducir la plaza primaria** que el empleado ya configuró.

## Evidencia

```jsonc
GET …/personnel-files/e5ac558f-…/finalize/preview?CreateUserAccount=true
// → 200
{
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

El expediente ya tiene plaza asignada (incl. `isPrimary`), pero el preview no la considera.

## Contrato relevante

```
GET  …/finalize/preview?createUserAccount&positionSlotPublicId   ← positionSlotPublicId opcional
PATCH …/finalize  body: { createUserAccount, positionSlotPublicId: uuid|null }
```

El campo es opcional/nullable. El problema no es el contrato, sino que **cuando no se envía, el backend no recurre a la plaza primaria existente** — la trata como ausente.

## El problema

- Redundancia/contradicción: el empleado ya asignó y marcó una plaza como **principal** (`isPrimary`). Exigir reenviar `positionSlotPublicId` duplica esa decisión.
- Si hay exactamente una plaza activa, o una marcada `isPrimary`, **el backend debería usarla automáticamente** para finalizar y considerar el requisito cumplido (`isEligible: true`).
- Mismo principio que ya pedimos para los campos derivables de la plaza: si el dato ya está en el agregado, no se debería pedir de nuevo.

## Acción requerida

1. **Deducir la plaza primaria** del empleado al evaluar `finalize/preview` y `finalize`: si existe una plaza activa marcada `isPrimary` (o una única plaza activa), usarla y marcar `isEligible: true` sin requerir `positionSlotPublicId` en la petición.
2. Mantener `positionSlotPublicId` como **override opcional** (cuando hay varias y el usuario quiere elegir una específica), pero no como requisito cuando la primaria ya es deducible.
3. Aclarar la regla cuando hay **varias** plazas sin ninguna `isPrimary`: ahí sí tiene sentido exigir la selección.

## Nota FE

- Hoy el FE llama al preview **sin** `positionSlotPublicId` (el usuario aún no eligió), por eso el backend responde "falta plaza". Como mitigación, el FE podría **autopasar la plaza `isPrimary`** ya asignada al preview/finalize. Pero la fuente de verdad de "cuál es la plaza principal" es el backend (la marca `isPrimary` vive en el agregado), por lo que la corrección natural es que el backend la deduzca.
- Defecto FE coexistente (menor): cuando `isEligible: false` por falta de plaza, el diálogo **no muestra** el selector de plaza (solo aparece cuando `isEligible: true`), así que ni siquiera hay workaround manual desde ese diálogo. Se evaluará por separado.
 