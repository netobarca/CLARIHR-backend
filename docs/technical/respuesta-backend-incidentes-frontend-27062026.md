# Respuesta Backend — Incidentes y preguntas del Frontend (27/06/2026)

| | |
| --- | --- |
| **De** | Equipo Backend (.NET API) |
| **Para** | Equipo Frontend |
| **Fecha** | 2026-06-27 |
| **Alcance** | Validación, causa raíz y resolución de los 7 incidentes/preguntas en `docs/questions-incidents/` |
| **Estado global** | ✅ Resuelto en backend (1 migración nueva, build limpio, 1966 tests unitarios verdes) |

> **Cómo leer este documento.** Cada incidente tiene su propia sección, con: **(1) Causa raíz** (qué encontramos en el código), **(2) Qué se cambió** (la solución o decisión), y **(3) Acción Frontend** (qué debes hacer tú ahora). Al final hay una sección transversal con un bug adicional que encontramos al investigar, y las notas de despliegue.

---

## Resumen ejecutivo

| # | Incidente | Veredicto | Estado |
| --- | --- | --- | --- |
| **INC‑01** | `GET assigned-positions` → 422 + sin texto legible de plaza | 422 en lectura **corregido**; texto legible: **derivable en cliente hoy** + mejora futura | ✅ / 🟡 parcial |
| **INC‑02** | Catálogos de personnel-files vacíos en el tenant | **Confirmado y corregido** (13 catálogos sembrados vía HasData) | ✅ |
| **INC‑03** | `POST contract-history` → 500 + sin catálogo `contract-types` | **Bug real corregido** (no era el código malo) + **catálogo `contract-types` creado** | ✅ |
| **INC‑04** | Deadlock al publicar (finalize ↔ assigned-positions) | **No era un deadlock de datos**; regla de estado **relajada** para Draft | ✅ |
| **INC‑05** | `POST exit-interview-forms` → 500 | **Bug real corregido** (no era anónimo ni seed; era la URL `Location`) | ✅ |
| **INC‑06** | `personnel-actions`: códigos sin catálogo + dudas de campos | **Catálogos `action-types`/`action-statuses` creados** + currency validada + dudas aclaradas | ✅ |
| **INC‑07** | `assigned-positions` acepta campos derivables de la plaza | **Backend ahora deriva** orgUnit/workCenter/contractType de la plaza | ✅ |

**Bug adicional encontrado (bonus):** 11 sub‑recursos de personnel-files devolvían **500 al crear** sobre un empleado finalizado (mismo patrón que el de contract-history). Todos corregidos. Ver [sección transversal](#transversal--11-errores-500-latentes-al-crear-sub-recursos-corregidos).

---

## INC‑01 — `GET assigned-positions`: 422 indebido + sin texto legible de plaza

**Endpoint:** `GET /api/v1/personnel-files/{publicId}/assigned-positions`

### Hallazgo 1 — El GET devolvía 422

**Causa raíz.** El handler de lectura usaba el mismo *gate* de escritura (`IsCompletedEmployee` = empleado **finalizado**). Un expediente en **Draft** (o un registro Candidato) caía en `PERSONNEL_FILE_STATE_RULE_VIOLATION` (422), incluso siendo una simple consulta. Y como ese 422 no estaba en el contrato del GET (solo 200/401/403/404), aparecía "de la nada".

**Qué se cambió.**
- El **GET** (lista y por id) ya **no** aplica la regla de empleado-finalizado: usa `LoadForReadAsync` (auth + not-found + tenant, sin gate de estado). Ahora **devuelve 200 con la lista** (posiblemente vacía) en cualquier estado, incluido Draft. **Ya no devuelve 422 en lectura.**

**Acción Frontend.**
- Puedes quitar el `try/catch → []` defensivo de `searchSubstitutePositionSlots`: el GET ya responde 200. (Puedes dejarlo si prefieres; ya no se dispara.)

### Hallazgo 2 — La respuesta solo trae IDs (UUID como etiqueta)

**Causa raíz confirmada.** `PersonnelFileEmploymentAssignmentResponse` solo expone IDs (`positionSlotId`, `orgUnitId`, …), sin `positionSlotCode`/`positionSlotTitle`/`orgUnitName`. La proyección no hace joins.

**Qué se cambió / decisión.** Enriquecer ese DTO con texto legible requiere una sub‑consulta correlacionada por fila (la asignación guarda el `positionSlotPublicId` como escalar, no como navegación). Para no introducir un cambio de proyección de alto riesgo sin pruebas de integración dedicadas, **lo dejamos como mejora planificada** (ver [Pendientes](#pendientes--mejoras-planificadas)).

**Acción Frontend (hoy, sin esperar al backend).** El texto de la plaza ya está disponible en `PositionSlotResponse` (`code`, `title`, `orgUnitName`, …). Como el combobox de plazas del sustituto **ya** se llena desde un listado de plazas, usa ese `code`/`title` como `label` (no necesitas un `GET /position-slots/{id}` por fila). El `positionSlotId` de la asignación cruza 1:1 con el `id` de la plaza.

---

## INC‑02 — Catálogos de personnel-files vacíos en el tenant

**Endpoint:** `GET /api/v1/general-catalogs/{catalogKey}?countryCode=SV`

**Causa raíz confirmada.** Muchos catálogos se sembraban **solo** en `DevSeedService` (entorno Development, un único tenant dev). En staging/prod salían **vacíos**, bloqueando el alta donde el `code` es requerido. Es exactamente el patrón ya resuelto con `assignment-types` (sembrado vía migración `HasData`, todos los entornos).

**Qué se cambió.** Se sembraron vía **HasData** (migración → **todos los entornos**, incluye backfill de tenants ya provisionados) los siguientes **13 catálogos** (valores idénticos al dev seed, SV):

| catalogKey | Códigos sembrados (SV) |
| --- | --- |
| `asset-access-types` | EQUIPO_COMPUTO, TELEFONO_MOVIL, UNIFORME, LICENCIA_SOFTWARE, ACCESO_SISTEMA, MOBILIARIO, HERRAMIENTA, OTRO |
| `delivery-statuses` | PENDIENTE, ENTREGADO, EN_USO, DEVUELTO, EXTRAVIADO, DANADO, NO_APLICA |
| `payment-methods` | TRANSFERENCIA, CHEQUE, EFECTIVO |
| `substitution-types` | VACACIONES, INCAPACIDAD, PERMISO, MISION_OFICIAL, LICENCIA, OTRO |
| `medical-claim-types` | AMBULATORIO, HOSPITALARIO, EMERGENCIA, FARMACIA, LABORATORIO, DENTAL, OFTALMOLOGICO, MATERNIDAD, OTRO |
| `medical-claim-status` ⚠️ *(clave en singular)* | PRESENTADO, EN_REVISION, PENDIENTE_DOCUMENTACION, APROBADO, RECHAZADO, PAGADO, PAGO_PARCIAL, ANULADO |
| `off-payroll-transaction-types` | HERRAMIENTAS, EPP, UNIFORMES, PROMOCIONALES, RECONOCIMIENTOS, REGALOS |
| `currencies` | USD |
| `languages` | ENGLISH, SPANISH |
| `language-levels` | ADVANCED, INTERMEDIATE, BASIC |
| `training-types` | COURSE, WORKSHOP, CERTIFICATION |
| `duration-units` | HOUR, DAY |
| `reference-types` | PERSONAL, PROFESSIONAL |

**Ya estaban OK (HasData, sin cambios):** `assignment-types`, `employment-statuses`, `experience-metrics`, `form-control-types`, y (vía `reference-catalogs`) `retirement-categories`, `retirement-reasons`.

**Notas importantes para el Frontend.**
- ⚠️ La clave de estados de reclamo es **`medical-claim-status`** (singular), no `medical-claim-statuses`.
- ⚠️ `retirement-categories` / `retirement-reasons` se sirven por **`/api/v1/reference-catalogs/{key}`**, no por `general-catalogs` (si los pides por `general-catalogs` da 400, no lista vacía).

**Acción Frontend.** Ninguna de código: los comboboxes ya consumen estos catálogos. Tras el despliegue de la migración, dejan de venir vacíos.

**Pendiente de backend (documentado abajo):** los catálogos de **seguros** (`insurance-types`/`insurance-ranges`, jerárquicos) y de **compensación** (`compensation-concept-types`, `pay-periods`, `calculation-bases`) siguen siendo DevSeed‑only — mismo patrón, los sembraremos en una siguiente iteración. Ver [Pendientes](#pendientes--mejoras-planificadas).

---

## INC‑03 — `POST contract-history` → 500 + `contractTypeCode` sin catálogo

**Endpoint:** `POST`/`PUT`/`PATCH` `/api/v1/personnel-files/{publicId}/contract-history`

**Causa raíz (importante: NO era el código malo).** El `contractTypeCode: "TIPO"` era un *red herring*. El 500 ocurría en **toda** creación válida sobre un empleado finalizado: el repositorio re‑consultaba con `AsNoTracking()` **antes** de `SaveChanges`, así que la fila recién agregada (aún sin persistir) **no** aparecía en el resultado, y el handler hacía `SingleOrDefault(...) ?? throw` → `InvalidOperationException` → 500. Era el mismo patrón que ya se había arreglado en `AddEmploymentAssignmentAsync` con `.Append(entity)`.

**Qué se cambió.**
1. **El 500 está corregido**: `AddContractHistoryAsync` ahora agrega la entidad en memoria al set devuelto (igual que asignaciones). El POST devuelve **201** correctamente. *(Este mismo bug afectaba a otros 10 sub‑recursos — todos corregidos, ver [sección transversal](#transversal--11-errores-500-latentes-al-crear-sub-recursos-corregidos).)*
2. **Catálogo `contract-types` creado** (country‑scoped, como `assignment-types`), expuesto en `GET /api/v1/general-catalogs/contract-types`. Códigos SV sembrados:

   `INDEFINIDO`, `PLAZO_FIJO`, `POR_OBRA`, `EVENTUAL`, `APRENDIZAJE`, `SERVICIOS_PROFESIONALES`, `TEMPORAL`, `OTRO`.
3. **Validación 422 controlada**: un `contractTypeCode` inactivo/inexistente ahora devuelve **422 `CONTRACT_TYPE_CODE_INVALID`** (en POST, PUT y PATCH), no 500.

**Respuesta a tus preguntas de diseño.**
- *¿422 en vez de 500 para código inválido?* → **Sí, hecho** (POST y PUT y PATCH).
- *¿Exponen el catálogo?* → **Sí, `contract-types`.**
- *¿El historial se deriva o es alta manual?* → Es una **colección persistida con CRUD manual intencional** (POST/PUT/PATCH/GET, sin DELETE). También lo escribe internamente el flujo de **recontratación**. La línea de tiempo de períodos de empleo se **deriva** de este historial, no al revés. **Sigue ofreciendo el alta manual.**

**Acción Frontend.** Convierte `contractTypeCode` a **combobox** desde `GET general-catalogs/contract-types`. Maneja `422 CONTRACT_TYPE_CODE_INVALID`. (Nota: el `contractTypeCode` de la **plaza** usa otro vocabulario — el de la descripción de puesto; este catálogo `contract-types` es para el alta manual del historial.)

---

## INC‑04 — Deadlock al publicar (finalize ↔ assigned-positions) 🔴

**Endpoints:** `GET …/finalize/preview` ↔ `GET/POST …/assigned-positions`

**Causa raíz (aclaración clave: NO había un deadlock de datos).** `finalize/preview` **no** lee ni exige una *fila* de `assigned-positions`. Exige el **parámetro escalar** `positionSlotPublicId` que se pasa en la **propia llamada** (`?positionSlotPublicId=<guid>` en preview; en el body al finalizar). El 422 de `assigned-positions` era el mismo problema de [INC‑01](#inc01--get-assigned-positions-422-indebido--sin-texto-legible-de-plaza): el recurso aplicaba el gate de empleado‑finalizado y bloqueaba leer/crear plazas en Draft. El mensaje del issue (`assignedPositionSlotPublicId`, sección `employment`) inducía a pensar que había que crear primero una asignación — que estaba bloqueada. De ahí la sensación de deadlock.

**Qué se cambió (decisión ratificada: "permitir gestionar plazas en Draft").**
- La regla de estado de `assigned-positions` se **relajó**: ahora se puede **leer (GET) y crear/editar/eliminar (POST/PUT/PATCH/DELETE)** mientras el expediente está en **Draft**, siempre que el registro sea de tipo **Empleado**. Solo se bloquea un registro **Candidato** (un candidato no tiene plazas). Esto elimina el bloqueo: ya puedes agregar la plaza requerida **antes** de finalizar.

**Flujo correcto ahora.**
1. Creas el expediente (Draft, tipo Empleado).
2. `GET/POST …/assigned-positions` → **funciona en Draft** (agregas la plaza).
3. `GET …/finalize/preview?positionSlotPublicId=<la plaza>` → `isEligible: true` (si lo demás está OK).
4. Finalizas pasando ese `positionSlotPublicId`.

**Nota sobre el mensaje del issue.** `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT` sigue existiendo, pero ahora es **satisfacible**: se cumple pasando `positionSlotPublicId` a la llamada de finalize (no requiere una fila pre‑creada). Tu navegación a la sección "Posiciones asignadas" ahora **sí** sirve (el usuario puede operar ahí en Draft).

**Acción Frontend.** Tu desbloqueo de la sección en Draft (`DRAFT_UNLOCKED_SECTION_EXCEPTIONS`) ahora es funcional. Asegúrate de pasar `positionSlotPublicId` en la llamada de `finalize`/`finalize/preview`.

---

## INC‑05 — `POST exit-interview-forms` → 500 `common.unexpected`

**Endpoint:** `POST /api/v1/exit-interview-forms`

**Causa raíz (ninguna de las dos hipótesis del FE).** **No** era `isAnonymous`, **no** era seed de tenant. El handler **creaba y commiteaba** el formulario correctamente; el 500 ocurría **después del commit**, al construir la URL `Location` del `201 Created`: la acción usaba la clave de ruta `formId`, pero una convención global (`PublicContractRouteConvention`) **renombra** ese parámetro de ruta a `publicId` (renombra los GUID de ruta que no terminan en `PublicId`). Como `Url.Action` no encontraba match con `formId`, lanzaba excepción → `common.unexpected`.

Por eso el reintento con el **mismo nombre** daba `409 EXIT_INTERVIEW_FORM_NAME_DUPLICATE`: **la fila sí quedaba persistida** (el throw es posterior al commit).

**Qué se cambió.** La acción de creación ahora usa la clave de ruta correcta (`publicId`). El `POST` devuelve **201** con `Location` válido. La ruta pública externa del GET‑by‑id es y sigue siendo `…/exit-interview-forms/{publicId}` (sin cambio de contrato).

**Acción Frontend.**
- El `POST` ya responde 201. No hay cambio de contrato (sigue `{ name, description, isAnonymous }`).
- ⚠️ **Limpieza de datos:** los formularios que el bug dejó "huérfanos" (commiteados pese al 500) siguen en el tenant `4252b16d-…` y son la causa de los `409` de nombre duplicado que viste. Puedes reutilizarlos (aparecen en `GET /exit-interview-forms`) o pedirnos su limpieza.

---

## INC‑06 — `personnel-actions`: códigos sin catálogo + dudas de campos

**Endpoint:** `POST` `/api/v1/personnel-files/{publicId}/personnel-actions` (recurso **append‑only**: solo POST + GET; **no hay PUT/PATCH/DELETE**).

**Qué se cambió.**
1. **`action-types` y `action-statuses` creados** (country‑scoped, como `assignment-types`), expuestos en `GET /api/v1/general-catalogs/action-types` y `…/action-statuses`. Códigos SV:
   - `action-types`: NOMBRAMIENTO, CONTRATACION, RECONTRATACION, ASCENSO, TRASLADO, CAMBIO_PUESTO, AUMENTO_SALARIAL, AMONESTACION, SUSPENSION, PERMISO, REINTEGRO, OTRO.
   - `action-statuses`: BORRADOR, PENDIENTE, EN_TRAMITE, APROBADA, RECHAZADA, APLICADA, ANULADA.
2. **Validación 422** en el POST: `actionTypeCode` → `ACTION_TYPE_CODE_INVALID`; `actionStatusCode` → `ACTION_STATUS_CODE_INVALID`.
3. **`currencyCode` validado** contra `general-catalogs/currencies` (opcional; si viene, debe ser válido) → `PERSONNEL_ACTION_CURRENCY_CODE_INVALID` (422).
4. **Robustez:** se añadieron límites de longitud (`description≤2000`, `reference≤120`, `currencyCode≤40`) para que un valor largo dé 400 controlado en vez de 500.

**Respuesta a tus preguntas.**
- *¿Exponen `action-types`/`action-statuses`?* → **Sí, creados.**
- *¿`currencyCode` se valida contra `currencies`?* → **Sí, habilitado.**
- *¿Qué es `reference` y cuándo lo llena el usuario vs. el sistema?* → Es una **nota/refererencia de texto libre opcional**; el sistema **nunca** la rellena (el único flujo sistema‑generado, recontratación, la deja `null`). En alta manual la llena el usuario con lo que quiera (o se omite). **Aclaración:** los campos `sourceSystem`/`sourceSyncedUtc` **no** pertenecen a este recurso — están en el recurso hermano `payroll-transactions` (ledger de nómina, de solo lectura). Los confundiste.
- *¿Código inválido devuelve 422?* → **Sí** (antes se persistía texto libre sin validar).
- *Recordatorio:* este recurso es **append‑only** (sin `PUT`). `isSystemGenerated` es de solo lectura (siempre `false` desde la API; `true` solo lo pone la recontratación).

**Acción Frontend.** Convierte tipo/estado/moneda a **combobox** (desde los 3 catálogos). Maneja los 3 nuevos códigos 422. El editor no debe esperar endpoint de actualización.

---

## INC‑07 — `assigned-positions` acepta campos derivables de la plaza

**Endpoint:** `POST`/`PUT` `/api/v1/personnel-files/{publicId}/assigned-positions`

**Causa raíz confirmada.** El backend **persistía literal** lo recibido (`orgUnitPublicId`, `workCenterPublicId`, `contractTypeCode`) — **no** los derivaba. Como ya removiste esos inputs (envías `null`), las asignaciones se estaban guardando **sin** esos datos. (Era el riesgo que advertiste.)

**Qué se cambió.** El backend ahora **deriva** estos tres campos de la plaza (`positionSlotPublicId`) en POST y PUT: cuando la plaza resuelve un valor, **gana la plaza** (precedencia: plaza > cliente). Así la asignación nunca contradice su plaza y se llena aunque envíes `null`.

**Respuesta a tus preguntas.**
1. *¿El backend deriva orgUnit/workCenter/contractType?* → **Sí, ahora sí** (POST y PUT).
2. *¿Retirar esos 3 del body?* → Pueden quedarse (se ignoran si la plaza resuelve valor) o quitarse; ya no causan datos faltantes.
3. *Regla de precedencia:* **la plaza gana**. El valor de cliente solo se usa como *fallback* si la plaza no resuelve ese dato.
4. *`costCenterPublicId`:* **se mantiene del lado cliente.** La plaza expone el centro de costo solo como **código** (derivado de la unidad organizativa), no como `publicId`, así que el backend no puede derivar su `publicId`. Exponer `costCenterPublicId` en `PositionSlotResponse` requiere un join nuevo a cost‑centers → lo dejamos como [pendiente](#pendientes--mejoras-planificadas). **Sigue enviando `costCenterPublicId`** como hasta ahora.
5. *¿Aplica a POST y PUT?* → **Sí, ambos.** (PATCH no deriva — es actualización parcial campo a campo.)

**Acción Frontend.** Puedes mantener removidos los inputs de orgUnit/workCenter/contractType (el backend los deriva). **Conserva** el `costCenterPublicId` (la plaza no lo expone aún).

---

## Transversal — 11 errores 500 latentes al crear sub‑recursos (corregidos)

Al investigar INC‑03 encontramos que **el mismo bug** (`.AsNoTracking()` re‑query antes de `SaveChanges` + `SingleOrDefault ?? throw`) afectaba a **11 métodos** del repositorio. Es decir, **crear** estos sub‑recursos sobre un empleado finalizado devolvía **500** en la ruta de éxito (no se detectó porque sus tests de integración solo cubrían el 422 de Draft, que retorna antes del throw):

`contract-history`, `compensation-concepts`, `additional-benefits`, `authorization-substitutions`, `asset-access`, `insurance`, **`medical-claims`** (ya desplegado), `off-payroll-transactions`, `performance-evaluations`, `selection-contests`, `curricular-competencies`.

**Todos corregidos** con el patrón `.Append(entity)` ya probado en asignaciones. Ahora todos devuelven **201** en creación. (Si el FE tenía workarounds por estos 500, pueden retirarse.)

---

## Notas de despliegue

- **Migración nueva:** `20260627212537_SeedGeneralCatalogsAndAddContractActionCatalogs`.
  - Crea 3 tablas: `contract_type_catalog_items`, `action_type_catalog_items`, `action_status_catalog_items`.
  - `InsertData` (SV) para los 16 catálogos (13 existentes + 3 nuevos).
  - **Sin model drift** (no toca otras tablas/columnas).
  - Es **idempotente para entornos limpios** (staging/prod tenían estas tablas vacías → siembra sin colisión). En bases dev ya pobladas por el viejo DevSeed, recrear/limpiar la BD dev (los bloques duplicados se removieron de `DevSeedService`).
- **Países:** sembrado **solo SV** en esta fase (igual que `assignment-types`/`employment-statuses`). Otros países activos requieren su siembra al onboarding.
- **Validación realizada:** build de solución limpio (0 warnings/0 errors); **1966 tests unitarios verdes** (incluye paridad de localización EN/ES de los 4 códigos nuevos y el guardrail de bijección de catálogos); migración aplica limpio contra PostgreSQL real; tests de integración de `assigned-positions` verdes.

### Códigos de error nuevos (para manejo en FE)

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `CONTRACT_TYPE_CODE_INVALID` | 422 | `contractTypeCode` no activo en `contract-types` |
| `ACTION_TYPE_CODE_INVALID` | 422 | `actionTypeCode` no activo en `action-types` |
| `ACTION_STATUS_CODE_INVALID` | 422 | `actionStatusCode` no activo en `action-statuses` |
| `PERSONNEL_ACTION_CURRENCY_CODE_INVALID` | 422 | `currencyCode` (opcional) no activo en `currencies` |
| `PERSONNEL_FILE_STATE_RULE_VIOLATION` | 422 | En `assigned-positions`: ahora **solo** para registros **Candidato** (Draft Empleado ya permitido) |

---

## Pendientes / mejoras planificadas

No bloquean al FE (hay alternativa hoy), las haremos en una siguiente iteración:

1. **Texto legible en `PersonnelFileEmploymentAssignmentResponse`** (`positionSlotCode`/`positionSlotTitle`/`orgUnitName`). Hoy: derivar el `label` del `PositionSlotResponse` que ya cargas (INC‑01 Hallazgo 2).
2. **`costCenterPublicId` en `PositionSlotResponse`**. Hoy: el FE sigue enviando `costCenterPublicId` (INC‑07 q4).
3. **Auto‑detección de plaza en `finalize`** (que finalize use la plaza ya asignada en Draft sin pasar el escalar). Hoy: pasa `positionSlotPublicId` en la llamada (INC‑04).
4. **Sembrar catálogos restantes DevSeed‑only:** `insurance-types`/`insurance-ranges` (jerárquicos, `reference-catalogs`), `compensation-concept-types`, `pay-periods`, `calculation-bases` (INC‑02). Mismo patrón HasData.
5. **Catálogos en más países** además de SV.
