# Respuesta Backend — Incidentes del Frontend (28/06/2026)

| | |
| --- | --- |
| **De** | Equipo Backend (.NET API) |
| **Para** | Equipo Frontend |
| **Fecha** | 2026-06-28 |
| **Alcance** | Validación, causa raíz y resolución de los 3 incidentes en `docs/questions-incidents/` (`bug-bank-accounts`, `bug-catalogos`, `bug-finalize`) |
| **Estado global** | ✅ Resuelto en backend (2 migraciones nuevas, build limpio 0 warnings, **2049 unit + 456 integration tests verdes**) · ⚠️ **requiere despliegue** para INC‑01 e INC‑02 |

> **Cómo leer este documento.** Cada incidente tiene su sección con **(1) Causa raíz**, **(2) Qué se cambió** y **(3) Acción Frontend**. Al final: una **nota de despliegue** (crítica), convenciones de API, un bug adicional encontrado, y lo que viene para "requisitos mínimos para publicar".

---

## Resumen ejecutivo

| # | Incidente | Veredicto | Estado |
| --- | --- | --- | --- |
| **INC‑01** | `bank-accounts`: falta catálogo para `accountTypeCode` | **Confirmado** (y peor: tampoco había 422 — se guardaba texto libre). Catálogo `account-types` **creado** + **422** | ✅ pend. deploy |
| **INC‑02** | Catálogos de personnel-files vacíos en el tenant | Los **3 reportados ya estaban** sembrados (mig 27/06) → era **falta de deploy**. Además se sembraron 10 catálogos más que sí faltaban | ✅ pend. deploy |
| **INC‑03** | `finalize` exige `positionSlotPublicId` pese a tener plaza primaria | **Confirmado**. El backend ahora **deduce la plaza activa+primaria** | ✅ |

---

## INC‑01 — `bank-accounts`: falta catálogo para `accountTypeCode`

**Endpoints:** `POST` / `PUT` / `PATCH` `/api/v1/personnel-files/{publicId}/bank-accounts`

**Causa raíz confirmada.** `accountTypeCode` era **texto libre puro**: no existía entidad, ni catálogo expuesto, ni siembra, ni validación. **Corrección al reporte:** un código inválido **no** devolvía 422 — se guardaba tal cual (cualquier string ≤80 chars). El riesgo real era inconsistencia silenciosa (`SAVINGS` / `savings` / `Ahorro` / `01` coexistiendo), no rechazo.

**Qué se cambió.**
- **Catálogo nuevo `account-types`** (country-scoped, mismo patrón que `asset-access-types`), expuesto en:
  ```
  GET /api/v1/general-catalogs/account-types?countryCode=SV
  ```
- Sembrado para SV vía migración `20260628235800` (HasData → **todos los entornos**): `AHORRO`, `CORRIENTE`, `PLANILLA`, `A_LA_VISTA`, `OTRO`.
- Un `accountTypeCode` inválido ahora devuelve **422 controlado** en `POST` / `PUT` / `PATCH` (campo `accountTypeCode` en `errors`).

Respuesta del catálogo (igual que `banks`/`currencies`, que ya consumes):
```jsonc
[
  { "id": "…uuid…", "code": "AHORRO",    "name": "Cuenta de ahorro",   "isActive": true, "sortOrder": 10 },
  { "id": "…uuid…", "code": "CORRIENTE", "name": "Cuenta corriente",   "isActive": true, "sortOrder": 20 },
  { "id": "…uuid…", "code": "PLANILLA",  "name": "Cuenta de planilla", "isActive": true, "sortOrder": 30 }
  // A_LA_VISTA, OTRO …
]
```

**Acción Frontend.**
- Cambia `accountTypeCode` de **texto libre → combobox** de `account-types` (el mismo patrón que ya aplicaste a `bankPublicId`/`currencyCode`). El **valor enviado es `code`** (p. ej. `"AHORRO"`), el **label es `name`**.
- Maneja el **422** si llega un código inválido (mismo formato que las demás validaciones: `extensions.code` + `errors.accountTypeCode`).
- ⚠️ El catálogo aparece **tras el despliegue** (ver nota de despliegue).

---

## INC‑02 — Catálogos de personnel-files vacíos en el tenant

**Endpoint:** `GET /api/v1/general-catalogs/{catalogKey}?countryCode=SV` (y `reference-catalogs` para los jerárquicos)

**Causa raíz confirmada.** Patrón ya conocido: catálogos sembrados **solo** en `DevSeedService` (entorno Development) **nunca llegan al servidor**; solo los sembrados vía **`HasData` en migración** llegan a todos los entornos (es lo que hace `MigrateAsync` al arrancar).

**Hallazgo clave (cambia el diagnóstico).** Los **3 catálogos que reportaron** — `asset-access-types`, `delivery-statuses`, `substitution-types` — más `payment-methods`, `medical-claim-types`, `off-payroll-transaction-types` — **ya estaban sembrados vía HasData** en la migración `20260627212537` (mergeada a `master` el 27/06). Si seguían vacíos en el servidor, **es porque esa migración no se ha desplegado**, no un hueco de código.

**Qué se cambió (la "revisión general" que pidieron).** Auditamos todos los catálogos de personnel-files y sembramos vía HasData (migración `20260628234354`) los que **sí** seguían siendo solo-dev y vacíos en servidor:

| Catálogo | Endpoint | Notas |
| --- | --- | --- |
| `insurance-types` | `reference-catalogs/insurance-types` | country-scoped |
| `insurance-ranges` | `reference-catalogs/insurance-ranges` | **jerárquico**: filtra por tipo con `&parentCode=VIDA` |
| `compensation-concept-types` | `general-catalogs/compensation-concept-types` | trae defaults de nómina (naturaleza, tasas ISSS/AFP) |
| `pay-periods` | `general-catalogs/pay-periods` | MENSUAL/QUINCENAL/SEMANAL/UNICA |
| `calculation-bases` | `general-catalogs/calculation-bases` | SALARIO_BASE/SALARIO_BRUTO/IBC/… |
| `education-statuses` | `general-catalogs/education-statuses` | system-scoped (sin país) |
| `education-study-types` | `general-catalogs/education-study-types` | system-scoped |
| `education-shifts` | `general-catalogs/education-shifts` | system-scoped |
| `education-modalities` | `general-catalogs/education-modalities` | system-scoped |
| `education-careers` | `general-catalogs/education-careers` | system-scoped |

**Acción Frontend.**
- **Ninguna de código** — ya consumes estos catálogos correctamente; es puramente data del backend.
- Tras el despliegue, **confirmar que poblan**.
- Recordatorios: los country-scoped requieren `?countryCode=SV`; `insurance-ranges` es **jerárquico** (pásale `&parentCode=<código del tipo de seguro>`); los `education-*` son system-scoped (el `countryCode` se ignora).

---

## INC‑03 — `finalize` exige `positionSlotPublicId` pese a tener plaza primaria

**Endpoints:** `GET …/finalize/preview` · `PATCH …/finalize`

**Causa raíz confirmada.** El resolver de elegibilidad leía `positionSlotPublicId` **solo del request**; nunca consultaba las plazas asignadas del empleado ni miraba `isPrimary`. Por eso, aunque el expediente ya tuviera una plaza marcada como principal, el preview respondía `isEligible: false` con `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT`.

**Qué se cambió.**
- Cuando el cliente **no** envía `positionSlotPublicId`, el backend **deduce la plaza activa+primaria** del expediente. Si existe una plaza activa marcada `isPrimary`, **se usa** y el requisito se da por cumplido (`isEligible: true`) sin reenviarla.
- `positionSlotPublicId` se mantiene como **override opcional** (cuando hay varias y el usuario quiere elegir una específica).
- Si hay **varias plazas activas sin ninguna primaria** (o ninguna plaza activa), **se sigue exigiendo** la selección → el preview devuelve `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT`.
- El fix vive en el resolver compartido, así que cubre **preview (GET)** y **finalize (PATCH)** por igual.

Ejemplo — el caso común ahora es elegible sin enviar la plaza:
```jsonc
GET …/finalize/preview?createUserAccount=true          // sin positionSlotPublicId
// → 200 { "isEligible": true, "issues": [] }           // usa la plaza isPrimary ya asignada
```

**Acción Frontend.**
- En el caso común, llama a `preview`/`finalize` **sin** `positionSlotPublicId` — el backend usa la plaza primaria. Solo envía `positionSlotPublicId` como **override**.
- Sobre el defecto FE coexistente (el diálogo no muestra el selector de plaza cuando `isEligible: false`): con la deducción, el caso común pasa a ser **elegible sin selección**. El selector **solo** hace falta cuando el preview devuelve `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT` (varias plazas activas sin primaria) — conviene mostrarlo **en ese caso** para que el usuario elija.

---

## ⚠️ Nota de despliegue (crítica para INC‑01 e INC‑02)

Los catálogos **no aparecen en el servidor hasta desplegar**: las filas `HasData` se aplican cuando el `MigrateAsync` del arranque corre las migraciones nuevas (rellena todos los tenants, no solo dev).

**Confirmar con infra que el servidor aplica estas migraciones:**
- `20260627212537` (27/06) — los 3 catálogos reportados + payment-methods/medical-claim/off-payroll. **Si esta no está aplicada, ese es el origen del reporte INC‑02.**
- `20260628234354` (esta sesión) — insurance/compensation/pay-periods/calculation-bases/education.
- `20260628235800` (esta sesión) — `account-types` (INC‑01).

---

## Convenciones de API (recordatorio)

- Prefijo **`api/v1`** (no `/v1`). Enums como **strings**. Código de error en **`extensions.code`**.
- Catálogos country-scoped → **`?countryCode=SV`**. `general-catalogs` para los simples; `reference-catalogs` para los jerárquicos (insurance, professions, etc.).
- Los `Guid XxxId` del CLR serializan como **`xxxPublicId`** en el wire (no es drift del openapi).

## Bug adicional encontrado al investigar (transparente para FE)

Al sembrar los `education-*` vía `HasData`, un guard de dominio (`PersonnelFileEducation`) rechazaba los IDs de catálogo **negativos** (la convención de seed del repo). Eso habría roto la **creación de registros de educación** en el servidor. **Corregido** — es transparente para el FE (referencias por `code`/`publicId`, nunca por el ID interno).

## Próximamente — checklist guiado de "requisitos mínimos para publicar"

INC‑03 resuelve el bloqueo inmediato. El **checklist completo** (qué falta para publicar más allá de email+plaza: identidad, contrato, compensación…) está **diseñado** en `docs/technical/plan-tecnico-requisitos-minimos-publicacion-expediente.md`, pendiente de **decisiones de negocio** (qué requisitos bloquean vs. solo advierten). El contrato de `finalize/preview` ya es la base: `issues[]` con `{ code, message, section, fieldKey, navigationKey, isBlocking }`. Cuando se amplíe: (a) dejará de hacer *short-circuit* (listará **todo** lo que falta de una vez) y (b) `isBlocking` distinguirá **bloqueos** (rojo, impiden publicar) de **advertencias** (ámbar, informativas).
