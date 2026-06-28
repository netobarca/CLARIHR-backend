# Hallazgo Backend — `personnel-actions`: códigos sin catálogo + dudas de campos

| | |
| --- | --- |
| **Endpoint** | `POST` / `PUT` `/api/v1/personnel-files/{publicId}/personnel-actions` |
| **Tipo** | Códigos de catálogo sin endpoint + claridad de contrato |
| **Fecha** | 2026-06-27 |
| **Estado** | 🟡 Abierto |

## Contrato actual

```
actionTypeCode: string        ← código, pero NO hay catálogo expuesto → texto libre
actionStatusCode: string      ← código, pero NO hay catálogo expuesto → texto libre
actionDateUtc: string(date-time)
effectiveFromUtc: string(date-time) | null
effectiveToUtc: string(date-time) | null
description: string | null
reference: string | null
amount: number(double) | null
currencyCode: string | null   ← código de moneda, sin catálogo en este flujo → texto libre
```

## Incoherencias detectadas

1. **`actionTypeCode` y `actionStatusCode` son texto libre.** Son códigos de catálogo (el usuario teclea "TIPO"/"OTRO"/"ESTADO"), pero no hay `GET …/general-catalogs/action-types` ni `…/action-statuses`. Sin catálogo, el FE solo puede ofrecer input libre → datos inconsistentes y riesgo de error en backend.
2. **`currencyCode` sin catálogo de monedas.** El campo es código de moneda pero no se valida contra un catálogo. Existe `general-catalogs/currencies` en otros flujos — ¿aplica aquí? Si sí, exponerlo/whitelistearlo para este editor.
3. **`reference` (Referencia de origen) — propósito poco claro.** Junto a `sourceSystem`/`sourceSyncedUtc` sugiere que la acción puede provenir de un sistema externo (nómina). Para alta **manual** no está claro qué debe ingresar el usuario. ¿Es opcional / solo lo llena el sistema? Documentar.

## Preguntas / acción requerida

1. ¿Pueden exponer catálogos para **`action-types`** y **`action-statuses`** (country-scoped, como `assignment-types`)? Así el FE usa combobox y elimina el texto libre.
2. ¿**`currencyCode`** debe validarse contra `general-catalogs/currencies`? Si sí, confirmar para habilitarlo en este flujo.
3. ¿Qué representa **`reference`** en un alta manual y cuándo debe llenarlo el usuario vs. el sistema?
4. Un `actionTypeCode`/`actionStatusCode`/`currencyCode` inválido, ¿devuelve **422 controlado** o revienta? (preferimos 422).

## Nota FE (ya aplicado)

- Corregido un bug de etiquetas: el form repetía "Fecha de inicio" dos veces. Ahora `actionDateUtc` = **"Fecha de la acción"**, y `effectiveFromUtc`/`effectiveToUtc` = **"Vigente desde/hasta"** (coinciden con la semántica del contrato).
- Fechas/strings vacíos se envían como `null` (antes iban como `""` → 400). Ver editor `personnel-actions-editor.component.ts`.
- Pendiente FE: convertir tipo/estado/moneda a combobox **cuando** existan los catálogos (punto 1 y 2).
 