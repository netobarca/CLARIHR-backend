# Bug Backend — `bank-accounts`: falta catálogo para `accountTypeCode`

| | |
| --- | --- |
| **Endpoint** | `POST` / `PUT` `/api/v1/personnel-files/{publicId}/bank-accounts` |
| **Tipo** | Código de catálogo sin endpoint expuesto |
| **Fecha** | 2026-06-27 |
| **Estado** | 🟡 Abierto |

## Contrato (`AddBankAccountRequest`)

```
bankPublicId: string(uuid)   ← Institución → catálogo `banks` ✅ (resuelto en FE)
currencyCode: string         ← Moneda → catálogo `currencies` ✅ (resuelto en FE)
accountNumber: string        ← texto libre (correcto)
accountTypeCode: string      ← Tipo de cuenta → SIN catálogo expuesto ❌
isPrimary: boolean
```

## Problema

`accountTypeCode` es un código de catálogo (ahorro / corriente / etc.), pero **no hay endpoint** que lo exponga: ni `general-catalogs/account-types` ni equivalente. Sin catálogo, el FE solo puede ofrecer **texto libre**, lo que produce códigos inconsistentes y posibles rechazos del backend.

## Acción requerida

1. **Exponer el catálogo de tipos de cuenta bancaria** (p. ej. `general-catalogs/account-types`, country-scoped como los demás) para poblar un combobox.
2. Un `accountTypeCode` inválido, ¿devuelve **422 controlado** o revienta? (preferimos 422).

## Nota FE (ya aplicado lo que sí se podía)

- **Institución** (`bankPublicId`) → ahora combobox de `banks` (el backend exige UUID; antes era texto libre y fallaba con 400 "must be a valid UUID").
- **Moneda** (`currencyCode`) → ahora combobox de `currencies`.
- **Tipo de cuenta** (`accountTypeCode`) → sigue como texto libre hasta que exista el catálogo. Se convertirá a combobox cuando el backend lo exponga.

Archivo: `bank-accounts-editor.component.ts`.
 