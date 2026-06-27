# Guía de integración Frontend — Múltiples Plazas por Empleado

| | |
|---|---|
| **Para** | Equipo Frontend |
| **Tipo** | Guía de integración + **cambios de contrato (BREAKING)** |
| **Módulos** | Personnel Files · Employee Profile · Finalización · Employment Assignments · Catálogos |
| **Backend de referencia** | commit `feat: Implement multi-position employment assignment rules` (2026-06-20) |
| **Análisis funcional** | `docs/business/analisis-multiples-plazas-empleado.md` |

---

## 1. TL;DR (qué cambió y qué tenés que hacer)

1. **La plaza de un empleado ahora vive en UN solo lugar: la colección de "assigned-positions"** (asignaciones de plaza). Un empleado puede tener **varias plazas activas**: una **principal** (`isPrimary: true`) y varias **secundarias**.
2. **Se eliminaron campos** que el FE quizá enviaba/leía:
   - Del **expediente** (personnel-file): `assignedPositionSlotPublicId` (ya no existe en requests ni responses).
   - Del **perfil laboral** (employee-profile): `positionSlotPublicId` y `jobProfilePublicId` (ya no existen).
3. **La finalización cambió**: ahora el `PATCH /finalize` **requiere `positionSlotPublicId` en el body** (la plaza principal con la que se finaliza al empleado). De esa plaza el backend deriva el **rol IAM** del usuario que se aprovisiona.
4. **`assignmentTypeCode` ya no es texto libre**: debe venir de un **catálogo** (`GET /api/v1/general-catalogs/assignment-types`).
5. **Hay reglas nuevas** que el FE debe manejar (mostrar errores): una sola principal, validación de **cupo por vigencia**, sin plazas duplicadas/solapadas, no se puede dejar al empleado sin principal.

> ⚠️ Estos son **cambios incompatibles** de contrato. El FE debe actualizarse. No hay migración de datos: los campos viejos desaparecen.

---

## 2. Modelo conceptual: antes vs ahora

**Antes** — la "plaza" estaba en 3 lugares (ambiguo):

```
PersonnelFile.assignedPositionSlotPublicId   (1 plaza, en el alta/onboarding)
EmployeeProfile.positionSlotPublicId/jobProfilePublicId  (1 plaza, post-completado)
EmploymentAssignments[]                       (N plazas)  ← solo este queda
```

**Ahora** — **fuente única de verdad**:

```
EmploymentAssignments[]   ← la(s) plaza(s) del empleado
   ├─ 1 principal activa (isPrimary=true, isActive=true)
   └─ 0..N secundarias activas
```

El rol IAM del empleado se deriva de la plaza **principal** (en la finalización, de la plaza que se envía; luego, el re-aprovisionamiento usa la asignación principal activa).

---

## 3. El nuevo flujo de vida (lifecycle)

```
┌─────────────┐   POST personnel-files         ┌──────────────┐
│  (no existe)│ ─────────────────────────────▶ │ Empleado     │
└─────────────┘   recordType=Employee          │ DRAFT        │
                  (SIN plaza)                   └──────┬───────┘
                                                       │ PATCH /finalize
                                                       │ body: { positionSlotPublicId } ← plaza principal
                                                       ▼
                                                ┌──────────────┐
                                                │ Empleado     │  ← se crea la cuenta de usuario
                                                │ COMPLETED    │     con el ROL de esa plaza
                                                └──────┬───────┘
                                                       │ POST/PUT/PATCH/DELETE
                                                       │ assigned-positions  (solo si COMPLETED)
                                                       ▼
                                                Gestión de plazas:
                                                 - agregar secundarias
                                                 - cambiar la principal (auto-degrada la anterior)
                                                 - finalizar/activar/desactivar
```

**Puntos clave para el FE:**
- El **alta del expediente ya NO captura una plaza**. No mandes `assignedPositionSlotPublicId` al crear/editar el expediente (ya no existe el campo).
- La **plaza principal se elige en la pantalla de finalización** (es lo que define el rol del usuario). El FE debe pedir `positionSlotPublicId` ahí.
- **Las assigned-positions solo se pueden crear/editar cuando el empleado está COMPLETED** (un `POST` sobre un Draft responde `422`). Esto ya era así.
- Tras finalizar, la pantalla de "plazas del empleado" gestiona las asignaciones (principal + secundarias).

---

## 4. Cambios de contrato (BREAKING) — resumen

| Endpoint | Cambio | Acción FE |
|---|---|---|
| `POST /companies/{companyId}/personnel-files` | **eliminado** `assignedPositionSlotPublicId` del body | quitarlo del request |
| `PUT /personnel-files/{id}` | **eliminado** `assignedPositionSlotPublicId` | quitarlo |
| `PATCH /personnel-files/{id}` | **eliminado** el path JSON-Patch `/assignedPositionSlotPublicId` | quitar esa operación |
| `GET /personnel-files/{id}` (y shell, list, personal-info, export) | **eliminado** `assignedPositionSlotPublicId` de la respuesta | dejar de leerlo |
| `PUT /personnel-files/{id}/employee-profile` | **eliminados** `positionSlotPublicId` y `jobProfilePublicId` del body | quitarlos |
| `GET /personnel-files/{id}/employee-profile` | **eliminados** `positionSlotId` y `jobProfileId` de la respuesta | dejar de leerlos |
| `PATCH /personnel-files/{id}/finalize` | **agregado** `positionSlotPublicId` (requerido) al body | enviarlo |
| `GET /personnel-files/{id}/finalize/preview` | **agregado** query param `positionSlotPublicId` | enviarlo para preview correcto |
| `POST/PUT /personnel-files/{id}/assigned-positions` | `positionSlotPublicId` ahora **obligatorio**; `assignmentTypeCode` debe ser del **catálogo** | validar |

---

## 5. Endpoints clave con payloads

### 5.1 Crear expediente (sin plaza)

`POST /api/v1/companies/{companyPublicId}/personnel-files`

```jsonc
{
  "recordType": "Employee",         // o "Candidate"
  "firstName": "Ana", "lastName": "Mendoza",
  "birthDate": "1990-01-01",
  "maritalStatusCode": "SOLTERO_A", "professionCode": "ANALISTA_DE_DATOS",
  "nationality": "SV",
  "institutionalEmail": "ana@empresa.test",
  "orgUnitPublicId": null
  // ❌ NO enviar assignedPositionSlotPublicId (ya no existe)
}
```

### 5.2 Finalizar (define la plaza principal y el rol del usuario)

`PATCH /api/v1/personnel-files/{publicId}/finalize` · header `If-Match: "<concurrencyToken>"`

```jsonc
{
  "createUserAccount": true,
  "positionSlotPublicId": "77aa…12"   // ⬅️ NUEVO y requerido: la plaza principal
}
```

- Si `createUserAccount: true`, la plaza **debe tener un rol IAM configurado** (si no → `422 PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT_ROLE`).
- Si no se envía `positionSlotPublicId` → `422 PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT`.

**Preview** (no muta, valida elegibilidad):
`GET /api/v1/personnel-files/{publicId}/finalize/preview?createUserAccount=true&positionSlotPublicId=77aa…12`
→ devuelve `{ isEligible, issues: [{ code, message, section, fieldKey, navigationKey, isBlocking }] }`. Usá los `code`/`fieldKey` para resaltar campos.

> **Recomendación UX:** en la pantalla de finalización, pedí la **plaza principal** (selector de PositionSlot) y, opcionalmente, llamá al `preview` con esa plaza antes de confirmar.

### 5.3 Gestionar plazas (assigned-positions)

Base: `/api/v1/personnel-files/{publicId}/assigned-positions` · **solo sobre empleado COMPLETED**
**Permisos:** `GET` → `PersonnelFiles.Read` · escrituras → `PersonnelFiles.Manage`.

| Método | Ruta | Notas |
|---|---|---|
| `GET` | `/assigned-positions` | lista (principal primero) |
| `POST` | `/assigned-positions` | crear (sin `If-Match`) → `201` + `ETag` |
| `GET` | `/assigned-positions/{id}` | detalle |
| `PUT` | `/assigned-positions/{id}` | reemplaza campos; **preserva `isActive`**; `If-Match` |
| `PATCH` | `/assigned-positions/{id}` | JSON-Patch (incluye `/isActive`); `If-Match` |
| `DELETE` | `/assigned-positions/{id}` | `If-Match` |

**Body (`POST`/`PUT`):**

```jsonc
{
  "assignmentTypeCode": "INDEFINIDO",  // ⬅️ REQUERIDO + debe existir en el catálogo (ver §7)
  "positionSlotPublicId": "77aa…12",   // ⬅️ REQUERIDO
  "orgUnitPublicId": null,
  "workCenterPublicId": null,
  "costCenterPublicId": null,
  "startDate": "2026-01-06T00:00:00Z",
  "endDate": null,                      // null = sin fin
  "isPrimary": true,
  "isActive": true,
  "notes": null
}
```

**Respuesta del ítem:**

```jsonc
{
  "employmentAssignmentPublicId": "7a1c…e9",
  "assignmentTypeCode": "INDEFINIDO",
  "positionSlotPublicId": "77aa…12",
  "orgUnitPublicId": null, "workCenterPublicId": null, "costCenterPublicId": null,
  "startDate": "2026-01-06T00:00:00Z", "endDate": null,
  "isPrimary": true, "isActive": true,
  "notes": null,
  "concurrencyToken": "a1b2…c3"
}
```

---

## 6. Reglas de negocio que el FE debe manejar

El backend las valida y devuelve errores (ver §8). El FE debería **anticiparlas en la UI** para mejor UX:

1. **Una sola principal activa.** Si marcás otra plaza como principal, el backend **degrada automáticamente** la anterior a secundaria. No necesitás hacerlo vos; solo refrescá la lista tras la operación.
2. **La primera plaza activa se vuelve principal automáticamente** aunque envíes `isPrimary: false`.
3. **No se puede dejar al empleado sin principal.** Intentar **degradar/desactivar/eliminar** la única principal activa (teniendo otras activas) → `422 EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED`. Flujo correcto: **primero** marcá otra como principal (auto-degrada), **después** quitás/desactivás la anterior.
4. **Cupo por vigencia.** Cada plaza tiene `maxEmployees`. No se puede asignar si no hay cupo disponible **en el período** → `422 EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED`.
5. **Sin duplicar la misma plaza activa** ni **solapar fechas** en la misma plaza → `409`. El solape **entre plazas distintas sí se permite** (es el objetivo del multi-plaza).
6. **Plaza asignable.** Debe existir, ser del mismo tenant, no estar `Suspended` y estar dentro de su vigencia → si no, `404`/`422`.

---

## 7. Catálogo de tipos de asignación (`assignmentTypeCode`)

`assignmentTypeCode` debe ser un código activo del catálogo **AssignmentType** (country-scoped). Obtenelo con:

`GET /api/v1/general-catalogs/assignment-types?countryCode=SV`

```jsonc
[
  { "code": "LEY_SALARIOS", "name": "Ley de Salarios", "isActive": true, "sortOrder": 10 },
  { "code": "CONTRATO", "name": "Contrato", ... },
  { "code": "INDEFINIDO", "name": "Tiempo indefinido", ... },
  { "code": "PLAZO_FIJO", "name": "Plazo fijo", ... },
  { "code": "INTERINO", "name": "Interinato", ... },
  { "code": "POR_OBRA", "name": "Por obra o servicio", ... },
  { "code": "AD_HONOREM", "name": "Ad honorem", ... },
  { "code": "SERVICIOS_PROFESIONALES", "name": "Servicios profesionales", ... },
  { "code": "RECARGO_FUNCIONES", "name": "Recargo de funciones", ... }
]
```

- Usá un **`<select>`** con estos códigos en el form de plaza (no input libre).
- El catálogo viene **seedeado en todos los entornos** (SV, vía migración) — el `<select>` siempre tendrá estas 9 opciones (antes solo se sembraba en dev, riesgo de quedar vacío fuera de dev).
- Es **ortogonal** a `isPrimary`: una plaza `INDEFINIDO` puede ser principal o secundaria.

---

## 8. Catálogo de errores (mostrar al usuario)

Todas las respuestas de error son **ProblemDetails** con `status`, `title`/`detail` (mensaje **localizado ES/EN** según `Accept-Language`/claim de idioma) y un campo **`code`** estable. Mapeá por `code`:

| `code` | HTTP | Cuándo | Sugerencia UX |
|---|---|---|---|
| `EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_FOUND` | 404 | la plaza no existe / otro tenant | "La plaza seleccionada no existe" |
| `EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE` | 422 | plaza suspendida o fuera de vigencia | "La plaza no está disponible para asignar" |
| `EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED` | 422 | sin cupo en el período | "La plaza no tiene cupo disponible" |
| `EMPLOYMENT_ASSIGNMENT_DUPLICATE_POSITION_SLOT` | 409 | ya tiene esa plaza activa | "El empleado ya está asignado a esta plaza" |
| `EMPLOYMENT_ASSIGNMENT_OVERLAPPING_DATES` | 409 | solape de vigencia en la misma plaza | "Las fechas se solapan con otra asignación a esta plaza" |
| `EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED` | 422 | dejaría al empleado sin principal | "Designá otra plaza principal antes de quitar/desactivar esta" |
| `EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID` | 422 | `assignmentTypeCode` fuera de catálogo | "Tipo de asignación inválido" |
| `EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_REQUIRED` | 422 | PATCH que deja la asignación sin plaza | "La plaza es obligatoria" |
| `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT` | 422 | finalizar sin `positionSlotPublicId` | resaltar el selector de plaza |
| `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT_ROLE` | 422 | la plaza no tiene rol IAM | "La plaza no tiene un rol configurado" |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` desactualizado | recargar y reintentar |

> **Campos requeridos del body (`POST`/`PUT`):** `assignmentTypeCode`, `positionSlotPublicId` y `startDate`. Si omitís o enviás `null` en `assignmentTypeCode` o `positionSlotPublicId`, recibís un **`400` de validación** (no el 422 de abajo). El OpenAPI ahora los declara `required` (antes aparecían `nullable`, lo que confundía: el runtime siempre los exigió). El 422 `EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID` aplica **solo** cuando `assignmentTypeCode` viene con un valor pero ese código no existe / no está activo en el catálogo.

**Concurrencia:** las mutaciones de plazas (`PUT`/`PATCH`/`DELETE`) requieren `If-Match: "<concurrencyToken>"`; tras cada operación reemplazá el token por el nuevo (`ETag`/body). `POST` no lleva `If-Match`.

---

## 9. Checklist de migración para el Frontend

- [ ] **Crear/editar expediente:** quitar `assignedPositionSlotPublicId` del request y de la lectura de la respuesta.
- [ ] **Perfil laboral (employee-profile):** quitar `positionSlotPublicId` y `jobProfilePublicId` del request y de la respuesta.
- [ ] **Onboarding:** quitar el campo "plaza" del alta del empleado (ya no se captura ahí).
- [ ] **Finalización:** agregar selector de **plaza principal** y enviar `positionSlotPublicId` en el `PATCH /finalize` (y en el query del `preview`).
- [ ] **Pantalla de plazas del empleado:** consumir `assigned-positions` como fuente única; soportar principal + secundarias.
- [ ] **Form de plaza:** `positionSlotPublicId` obligatorio; `assignmentTypeCode` desde el catálogo `assignment-types` (select).
- [ ] **Cambiar principal:** implementar como "marcar otra como principal" (el backend degrada la anterior). Refrescar lista.
- [ ] **Quitar/desactivar principal:** manejar `EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED` (guiar al usuario a designar otra principal primero).
- [ ] **Manejo de errores:** mapear los `code` de §8 a mensajes (el `detail` ya viene localizado ES/EN).
- [ ] **Concurrencia:** usar `If-Match` con el `concurrencyToken` en `PUT`/`PATCH`/`DELETE`.

---

## 10. Notas

- **Sin migración de datos:** los campos eliminados desaparecen; no se preservan valores (acordado: el FE se ajusta).
- **Idioma:** los mensajes de error vienen en **ES/EN** según el `Accept-Language` o el claim de idioma del usuario; el `code` es estable para el mapeo.
- **Fuera de alcance** (no disponible aún): nómina multi-plaza, % de dedicación/FTE por plaza, workflow de aprobación de asignaciones.
- Para el detalle funcional completo (reglas RN-01…RN-16, criterios de aceptación) ver `docs/business/analisis-multiples-plazas-empleado.md`.
