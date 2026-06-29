# Bug Backend — Catálogos de personnel-files vacíos en el tenant (sin sembrar)

| | |
| --- | --- |
| **Endpoint** | `GET /api/v1/general-catalogs/{catalogKey}?countryCode=SV` |
| **Severidad** | Alta — bloquea el alta en secciones cuyo `code` requerido sale de un combobox vacío |
| **Fecha** | 2026-06-27 |
| **Estado** | 🟢 Resuelto (backend) — pendiente deploy · 2026-06-28 |

## ✅ Resolución backend (2026-06-28)

**Los 3 catálogos reportados** (`asset-access-types`, `delivery-statuses`, `substitution-types`) — más `payment-methods`, `medical-claim-types`, `off-payroll-transaction-types` — **ya estaban sembrados vía HasData** en la migración `20260627212537` (mergeada a `master` el 27/06). Si seguían vacíos en el servidor era **falta de deploy** de esa migración, no un hueco de código. → **Acción: desplegar `master`** (el `MigrateAsync` del arranque los rellena en todos los tenants).

Revisión general (lo que el reporte pedía): se detectaron y **sembraron vía HasData** (migración `20260628234354`) los catálogos que SÍ seguían siendo solo-dev y por tanto vacíos en servidor: `insurance-types`, `insurance-ranges`, `compensation-concept-types`, `pay-periods`, `calculation-bases` y los 5 `education-*`. Raíz del patrón: catálogos sembrados solo en `DevSeedService` (solo dev) nunca llegan al servidor; solo `HasData` en migración llega a todos los entornos. Verificado: 2049 unit + 456 integration tests verdes.

## Problema

Varios catálogos devuelven **lista vacía** en el tenant de prueba (`4252b16d-…`). Como el FE puebla los comboboxes desde estos catálogos, el usuario **no tiene opciones que elegir** y no puede guardar (el `code` es requerido por el backend).

Es el mismo patrón ya resuelto con `assignment-types` (que se sembró vía migración en todos los entornos). Pedimos sembrar los demás igual.

## Catálogos afectados (confirmados vacíos o sospechosos)

| catalogKey | Usado en sección | ¿Requerido para guardar? |
| --- | --- | --- |
| `asset-access-types` | Activos y accesos → "Tipo" | ✅ sí (`assetTypeCode` requerido) |
| `delivery-statuses` | Activos y accesos → "Estado de entrega" | opcional |
| `substitution-types` | Sustituciones → "Tipo" | ✅ sí (`substitutionTypeCode` requerido) |

> Si hay más catálogos de personnel-files sin sembrar (p. ej. `payment-methods`, `substitution-types`, `medical-claim-types`, `off-payroll-transaction-types`), conviene revisarlos de una vez para no iterar.

## Acción requerida

1. **Sembrar** `asset-access-types`, `delivery-statuses` y `substitution-types` (SV, y demás países activos) vía migración, como se hizo con `assignment-types`.
2. Confirmar que la siembra aplica a **todos los entornos** (dev/staging/prod), no solo dev.
3. Pasar una **revisión general** de qué catálogos de personnel-files están sembrados por país, para detectar otros vacíos antes de que el usuario los encuentre.

## Nota FE

El FE ya está correcto: los comboboxes consumen `asset-access-types` y `delivery-statuses` desde `general-catalogs`. No hay cambio de FE pendiente — es puramente data del backend.
 