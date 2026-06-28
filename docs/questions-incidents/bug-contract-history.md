# Bug Backend — `contract-history` lanza 500 + `contractTypeCode` sin catálogo

| | |
| --- | --- |
| **Endpoint** | `POST` `/api/v1/personnel-files/{publicId}/contract-history` |
| **Severidad** | Alta — 500 no controlado al crear historial de contrato |
| **Fecha** | 2026-06-27 |
| **Estado** | 🟡 Abierto |

## Evidencia

Request **válido según el contrato** (`AddContractHistoryRequest`):

```jsonc
{
  "contractTypeCode": "TIPO",          // texto libre — no validado contra catálogo
  "contractDate": "2026-06-27T00:00:00Z",
  "contractEndDate": null,
  "positionSlotPublicId": null,
  "isActive": true,
  "notes": null
}
```

Respuesta:

```jsonc
{ "status": 500, "code": "common.unexpected", "title": "Ocurrio un error inesperado.",
  "traceId": "00-f594dfa811ee02d676e549ea5d51e626-02ebe573df8b3c92-00" }
```

## Problemas

1. **500 no controlado.** Un `contractTypeCode` inexistente debería devolver un **422 controlado** (p. ej. `CONTRACT_TYPE_CODE_INVALID`), no `500 common.unexpected`. El FE no puede distinguir un dato malo de una caída del servidor.
2. **No hay catálogo `contract-types` expuesto.** El campo es código de catálogo (la plaza ya expone `contractTypeCode`/`contractTypeName`), pero no existe `GET …/general-catalogs/contract-types` ni equivalente. Sin catálogo, el FE solo puede ofrecer **texto libre**, lo que garantiza códigos inválidos → el 500 de arriba.

## Preguntas / acción requerida

1. ¿Pueden hacer que un `contractTypeCode` inválido devuelva **422** controlado en lugar de 500? (POST y PUT).
2. ¿Pueden **exponer el catálogo de tipos de contrato** (`contract-types`, country-scoped como los demás) para alimentar un combobox y eliminar el texto libre?
3. Pregunta de diseño: ¿el historial de contratos se **deriva** internamente (plaza/asignaciones) y debería ser mayormente solo-lectura, o la creación/edición manual es intencional? Esto define si el FE debe seguir ofreciendo el alta manual.

## Nota FE

Sin catálogo no podemos validar el código en el cliente; el input de `contractTypeCode` sigue siendo texto libre por ahora. Una vez expuesto el catálogo, se convertirá a combobox (como `assignment-types`).
 