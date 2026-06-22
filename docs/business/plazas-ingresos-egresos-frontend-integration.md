# Guía de integración Frontend — Plazas Asignadas: Ingresos y Egresos

| | |
|---|---|
| **Para** | Equipo Frontend |
| **Tipo** | Guía de integración + **cambios de contrato (BREAKING)** |
| **Módulos** | Plazas Asignadas (`assigned-positions`) · Compensación (`compensation-concepts`) · Catálogos de compensación · Tabla de Renta (`income-tax-brackets`) · Plazas (`PositionSlots`) |
| **Documentos base** | `docs/business/analisis-plazas-ingresos-egresos.md` (D-01…D-20) · `docs/technical/plan-tecnico-plazas-ingresos-egresos.md` |
| **Idioma de errores** | Bilingüe (ES/EN) según `Accept-Language` / claim de idioma; el `code` es estable |

---

## 0. Estado de implementación (backend)

| Pieza | Estado |
|---|---|
| Catálogos de compensación (tipos, periodicidades, bases) + permiso `ViewCompensation` | ✅ Implementado |
| `compensation-concepts` (ingresos/egresos, fijo/%) — CRUD | ✅ Implementado |
| Endpoint enriquecido `compensation-concept-types` | ✅ Implementado |
| Salario 3 niveles + **bloqueo de rango** (R-3) | ✅ Implementado |
| `PositionSlot.configuredBaseSalary` (valor de referencia de la plaza) | ✅ Implementado |
| Auto-sugerencia ISSS/AFP al crear plaza (D-20) | ✅ Implementado |
| Tabla de Renta `income-tax-brackets` (config, sin cálculo) | ✅ Implementado |
| **Rename ruta `employment-assignments` → `assigned-positions`** (D-10) | ✅ Implementado |
| Lectura de compensación con `ViewCompensation` + autoservicio del empleado | ✅ Implementado |

> Todas las piezas están implementadas en el backend (build verde, 1847 pruebas unitarias en verde). Esta guía es el contrato vigente.

---

## 1. TL;DR (qué cambió y qué tenés que hacer)

1. **La compensación es ahora un modelo único: "conceptos de compensación"** (`compensation-concepts`). Cada concepto es un **ingreso** o un **egreso**, configurado como **monto fijo** o **porcentaje**. Reemplaza por completo a los antiguos `salary-items` (**eliminados**).
2. **El salario del empleado es un concepto INGRESO `SALARIO_BASE` asociado a la plaza** (no un campo aparte). Una plaza puede tener **un solo** salario base activo.
3. **El salario negociado se valida (BLOQUEA) contra el rango del puesto** (tabulador del job profile). Si lo supera → `422`.
4. **Los egresos se clasifican** en `Ley` / `Interno` / `Externo` (editable por instancia).
5. **Al crear una plaza, el backend sugiere automáticamente ISSS y AFP** (egresos de ley, `isSystemSuggested: true`) — el FE debe mostrarlos como precargados y editables/eliminables.
6. **La sección "Asignaciones" se renombra a "Plazas Asignadas"**; la ruta pasa de `employment-assignments` → **`assigned-positions`** (BREAKING).
7. **Hay catálogos nuevos** para poblar los selectores: tipos de concepto (enriquecido), periodicidades, bases de cálculo.
8. **Hay una tabla de Renta configurable** (`income-tax-brackets`). El backend **solo configura**; el **cálculo de planilla/retención NO existe aún** (módulo futuro).

> ⚠️ **Sin migración de datos.** `salary-items` desapareció; no se preservan valores.

---

## 2. Modelo conceptual

```
Empleado (PersonnelFile, COMPLETED)
 ├─ Plazas Asignadas (assigned-positions)   ← 1..N; una principal + secundarias
 │     └─ cada plaza: contrato, fechas, tipo, PositionSlot
 └─ Conceptos de compensación (compensation-concepts)   ← 1..N ingresos + 1..N egresos
       ├─ ámbito: nivel EMPLEADO (assignedPositionPublicId = null)
       │          o nivel PLAZA (assignedPositionPublicId = <plaza>)
       ├─ nature: Ingreso | Egreso
       ├─ calculationType: Fixed (monto) | Percentage (% sobre base)
       └─ egreso → deductionClass: Ley | Interno | Externo
```

**Salario en 3 niveles:**
1. **Perfil de puesto** → rango `[min, max]` (del tabulador salarial del job profile).
2. **Plaza** (`PositionSlot.configuredBaseSalary`) → valor configurado/referencia (informativo).
3. **Empleado** → **salario negociado** = concepto `INGRESO` / `SALARIO_BASE` en su plaza (la fuente de verdad). **Debe estar dentro del rango del nivel 1** o el backend bloquea.

---

## 3. Valores de enum (wire)

Se serializan como **el nombre del enum** (PascalCase). Enviar/leer exactamente:

| Campo | Valores |
|---|---|
| `nature` | `Ingreso`, `Egreso` |
| `calculationType` | `Fixed`, `Percentage` |
| `deductionClass` | `Ley`, `Interno`, `Externo` (o `null` para ingresos) |

> La deserialización es **case-insensitive** (podés mandar `INGRESO` o `Ingreso`), pero el backend **responde** con el nombre (`Ingreso`).

---

## 4. Endpoints — Conceptos de compensación

Base: `/api/v1/personnel-files/{personnelFilePublicId}/compensation-concepts` · **solo sobre empleado COMPLETED**.
**Permisos:** lectura → `PersonnelFiles.ViewCompensation` (o el empleado su propia ficha); escritura → `PersonnelFiles.Manage`.

| Método | Ruta | Notas |
|---|---|---|
| `GET` | `/compensation-concepts` | lista (activos primero, por fecha) |
| `GET` | `/compensation-concepts/{id}` | detalle |
| `POST` | `/compensation-concepts` | crear → `201` + `ETag` |
| `PUT` | `/compensation-concepts/{id}` | reemplaza campos de negocio; **preserva `isActive`**; `If-Match` |
| `PATCH` | `/compensation-concepts/{id}` | JSON-Patch (incluye `/isActive`); `If-Match` |
| `DELETE` | `/compensation-concepts/{id}` | `If-Match` |

**Body (`POST`):**
```jsonc
{
  "assignedPositionPublicId": "7a1c…e9",   // null = concepto a nivel empleado; con valor = a nivel plaza
  "nature": "Egreso",                       // Ingreso | Egreso
  "conceptTypeCode": "ISSS",                // del catálogo compensation-concept-types
  "deductionClass": "Ley",                  // requerido si Egreso; null si Ingreso
  "calculationType": "Percentage",          // Fixed | Percentage
  "value": 3.00,                            // monto (si Fixed) o % 0–100 con hasta 8 decimales (si Percentage)
  "calculationBaseCode": "IBC",             // requerido si Percentage (catálogo calculation-bases)
  "employerRate": 7.50,                     // carga patronal (ISSS/AFP); opcional
  "contributionCap": 1000.00,               // tope/base máxima; opcional
  "currencyCode": "USD",                    // catálogo currencies
  "payPeriodCode": "MENSUAL",               // catálogo pay-periods
  "counterpartyName": null,                 // egreso externo (préstamo): contraparte; opcional
  "externalReference": null,                // egreso externo: referencia; opcional
  "startDate": "2026-01-06T00:00:00Z",
  "endDate": null,                          // null = sin fin
  "isActive": true,
  "notes": null
}
```

**Respuesta del ítem:**
```jsonc
{
  "compensationConceptPublicId": "…",
  "assignedPositionPublicId": "7a1c…e9",
  "nature": "Egreso",
  "conceptTypeCode": "ISSS",
  "deductionClass": "Ley",
  "calculationType": "Percentage",
  "value": 3.00,
  "calculationBaseCode": "IBC",
  "employerRate": 7.50,
  "contributionCap": 1000.00,
  "currencyCode": "USD",
  "payPeriodCode": "MENSUAL",
  "counterpartyName": null,
  "externalReference": null,
  "startDate": "2026-01-06T00:00:00Z",
  "endDate": null,
  "isActive": true,
  "isSystemSuggested": true,    // true = lo creó el sistema (ISSS/AFP al crear la plaza)
  "notes": null,
  "concurrencyToken": "a1b2…"
}
```

- `PUT` usa el mismo body **sin** `isActive` (se preserva; se cambia solo por `PATCH`).
- `PATCH` (RFC 6902, `application/json-patch+json`): rutas raíz mutables — `/nature`, `/conceptTypeCode`, `/deductionClass`, `/calculationType`, `/value`, `/calculationBaseCode`, `/employerRate`, `/contributionCap`, `/currencyCode`, `/payPeriodCode`, `/counterpartyName`, `/externalReference`, `/assignedPositionPublicId`, `/startDate`, `/endDate`, `/notes`, `/isActive`.

---

## 5. Salario base (regla especial)

Para registrar el **salario del empleado** en una plaza: crear un concepto con `nature: "Ingreso"`, `conceptTypeCode: "SALARIO_BASE"`, `calculationType: "Fixed"`, `assignedPositionPublicId: <la plaza>`, `value: <monto>`.

Reglas que el backend valida (mostrar al usuario):
- **Único salario base activo por plaza** → segundo activo solapado: `422 COMPENSATION_BASE_SALARY_ALREADY_ACTIVE`.
- **Dentro del rango del puesto** (tabulador): si `value` supera el `[min, max]` de la plaza → `422 COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE` (**bloquea**). Si la plaza no tiene tabulador asociado, no hay banda y no se bloquea.

> El **valor de referencia de la plaza** (`PositionSlot.configuredBaseSalary`) es informativo (nivel 2); el negociado puede diferir mientras esté dentro del rango.

---

## 6. Egresos: ley / interno / externo

- **Ley** (ISSS, AFP, Renta): `deductionClass: "Ley"`. ISSS/AFP suelen ser `Percentage` con `employerRate` (carga patronal) y `contributionCap` (tope).
- **Interno** (daño de equipo, anticipo, préstamo interno): `deductionClass: "Interno"`.
- **Externo** (préstamo bancario, embargo, cuota alimenticia): `deductionClass: "Externo"`; usar `counterpartyName`/`externalReference` para contexto. (Saldo/amortización **fuera de alcance** — solo descuento recurrente.)

**Auto-sugerencia (D-20):** al crear una plaza (ver §8), el backend agrega automáticamente conceptos `ISSS` y `AFP` (`isSystemSuggested: true`) con los defaults del catálogo. El FE debe listarlos como precargados; el usuario puede editarlos o eliminarlos.

---

## 7. Catálogos (poblar selectores)

| Selector | Endpoint | Notas |
|---|---|---|
| Tipos de concepto (enriquecido) | `GET /api/v1/compensation-concept-types?countryCode=SV&nature=Egreso` | trae defaults: `nature`, `isStatutory`, `defaultDeductionClass`, `defaultCalculationType`, `defaultCalculationBaseCode`, `defaultEmployeeRate`, `defaultEmployerRate`, `contributionCap` → usar para **precargar** el form |
| Periodicidades | `GET /api/v1/general-catalogs/pay-periods?countryCode=SV` | `MENSUAL`, `QUINCENAL`, `SEMANAL`, `UNICA` |
| Bases de cálculo | `GET /api/v1/general-catalogs/calculation-bases?countryCode=SV` | `SALARIO_BASE`, `SALARIO_BRUTO`, `IBC`, `RUBRO_ESPECIFICO` |
| Monedas | `GET /api/v1/general-catalogs/currencies?countryCode=SV` | `USD` |

`compensation-concept-types` response (ítem):
```jsonc
{
  "id": "…", "code": "ISSS", "name": "ISSS",
  "nature": "Egreso", "isStatutory": true,
  "defaultDeductionClass": "Ley",
  "defaultCalculationType": "Percentage",
  "defaultCalculationBaseCode": "IBC",
  "defaultEmployeeRate": 3.00, "defaultEmployerRate": 7.50, "contributionCap": 1000.00,
  "isActive": true, "sortOrder": 100
}
```
> Recomendación UX: al elegir un `conceptTypeCode`, precargar `nature`/`deductionClass`/`calculationType`/`calculationBaseCode`/`value`(=defaultEmployeeRate)/`employerRate`/`contributionCap` desde el catálogo; dejarlos editables.

---

## 8. Plazas Asignadas (rename) y auto-sugerencia

La gestión de plazas **no cambia de forma**, solo de **ruta**: `…/employment-assignments` → **`…/assigned-positions`** (mismos verbos y body; ver la guía de multi-plaza para reglas de principal/cupo/solape).

Al hacer `POST …/assigned-positions` (crear plaza), el backend, **en la misma operación**, crea los conceptos ISSS/AFP sugeridos para esa plaza. Tras crear la plaza, **refrescá la lista de `compensation-concepts`** para mostrarlos.

---

## 9. Tabla de Renta (ISR) — configuración

Base: `/api/v1/income-tax-brackets` · lectura `PersonnelFiles.Read`/`ViewCompensation`, escritura `PersonnelFiles.Manage`.

| Método | Ruta | Notas |
|---|---|---|
| `GET` | `/income-tax-brackets?payPeriodCode=MENSUAL` | lista de tramos del tenant (filtrable por período) |
| `PUT` | `/income-tax-brackets` | **reemplaza** todos los tramos de un período (se edita la tabla como conjunto) |

**`PUT` body:**
```jsonc
{
  "payPeriodCode": "MENSUAL",
  "brackets": [
    { "bracketOrder": 1, "lowerBound": 0.01, "upperBound": 472.00, "fixedFee": 0.00, "ratePercent": 0.00, "excessOver": 0.00, "effectiveFromUtc": "2026-01-01T00:00:00Z", "effectiveToUtc": null, "isActive": true },
    { "bracketOrder": 2, "lowerBound": 472.01, "upperBound": 895.24, "fixedFee": 17.67, "ratePercent": 10.00, "excessOver": 472.00, "effectiveFromUtc": "2026-01-01T00:00:00Z", "effectiveToUtc": null, "isActive": true }
    // … tramos III, IV
  ]
}
```
> El backend **solo almacena** la tabla (seed SV mensual incluido). El **cálculo** de la retención (aplicar el tramo) es del **módulo de nómina futuro** — fuera de alcance.

---

## 10. Catálogo de errores (mapear por `code`)

| `code` | HTTP | Cuándo |
|---|---|---|
| `COMPENSATION_CONCEPT_TYPE_CODE_INVALID` | 422 | `conceptTypeCode` fuera de catálogo |
| `COMPENSATION_CONCEPT_CALCULATION_BASE_REQUIRED` | 422 | `Percentage` sin `calculationBaseCode` |
| `COMPENSATION_CONCEPT_CALCULATION_BASE_INVALID` | 422 | `calculationBaseCode` fuera de catálogo |
| `COMPENSATION_CONCEPT_PERCENTAGE_OUT_OF_RANGE` | 422 | `%` fuera de 0–100 |
| `COMPENSATION_CONCEPT_CURRENCY_INVALID` | 422 | `currencyCode` fuera de catálogo |
| `COMPENSATION_CONCEPT_PAY_PERIOD_INVALID` | 422 | `payPeriodCode` fuera de catálogo |
| `COMPENSATION_CONCEPT_DEDUCTION_CLASS_REQUIRED` | 422 | egreso sin `deductionClass` |
| `COMPENSATION_CONCEPT_ASSIGNED_POSITION_NOT_FOUND` | 422 | `assignedPositionPublicId` no es una plaza del empleado |
| `COMPENSATION_BASE_SALARY_ALREADY_ACTIVE` | 422 | ya hay salario base activo en la plaza |
| `COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE` | 422 | salario negociado fuera del rango del puesto |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` desactualizado |

> Validaciones estructurales (campo requerido, longitud, enum inválido) responden `400` de validación.

---

## 11. Concurrencia

Las mutaciones (`PUT`/`PATCH`/`DELETE`) de conceptos requieren `If-Match: "<concurrencyToken>"`; tras cada operación reemplazá el token por el nuevo (`ETag`/body). `POST` no lleva `If-Match`. (La tabla de Renta `PUT` reemplaza el conjunto y no usa `If-Match`.)

---

## 12. Checklist de migración Frontend

- [ ] Quitar todo uso de `salary-items` (eliminado).
- [ ] Implementar la pantalla de **compensación** por empleado/plaza usando `compensation-concepts` (ingresos + egresos, fijo/%).
- [ ] **Salario:** registrarlo como concepto `INGRESO`/`SALARIO_BASE` en la plaza; manejar el **bloqueo de rango** y la **unicidad**.
- [ ] Poblar selectores con `compensation-concept-types` (precarga de defaults), `pay-periods`, `calculation-bases`, `currencies`.
- [ ] Egresos: selector de `deductionClass` (Ley/Interno/Externo); para externos, campos contraparte/referencia.
- [ ] Tras crear una plaza, **refrescar** conceptos para mostrar ISSS/AFP sugeridos (`isSystemSuggested`).
- [ ] Cambiar ruta de plazas `employment-assignments` → `assigned-positions`.
- [ ] (Admin nómina) Pantalla de **tabla de Renta** con `income-tax-brackets` (GET + PUT por período).
- [ ] Mapear errores por `code` (§10); mensajes ya vienen localizados ES/EN.
- [ ] Concurrencia `If-Match` en conceptos.

---

## 13. Notas / fuera de alcance

- **No hay cálculo de planilla.** Esto **configura** (qué ingresos/egresos, fijo/%, base, tabla de Renta, % ISSS/AFP). El cómputo del neto, retención de Renta por tramos y generación de movimientos es **módulo de nómina futuro** (D-08).
- **Multi-moneda** permitida por concepto, **sin conversión**.
- Los **valores legales** (% ISSS/AFP, topes, tabla de Renta) son **editables en cualquier momento**; los seeds son solo defaults.
- Para el detalle funcional completo (RF/RN/decisiones) ver `docs/business/analisis-plazas-ingresos-egresos.md`.
