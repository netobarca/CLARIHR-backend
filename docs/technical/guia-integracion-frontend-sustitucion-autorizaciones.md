# Guía de Integración Frontend — Sustitución para Autorizaciones (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Guía de desarrollo, flujo e integración para el Frontend |
| **Audiencia** | Equipo Frontend, UX/UI, QA |
| **Documento de negocio** | [`docs/business/analisis-sustitucion-autorizaciones-empleado.md`](../business/analisis-sustitucion-autorizaciones-empleado.md) (D-01…D-12) |
| **Plan técnico** | [`docs/technical/plan-tecnico-sustitucion-autorizaciones.md`](./plan-tecnico-sustitucion-autorizaciones.md) |
| **Estado** | Implementado (Fase 1 — registro documental endurecido). Backend compilando, unit tests verdes, migración aplicada. |
| **País de referencia** | El Salvador (SV) |
| **Fecha** | 2026-06-22 |

---

## 1. Qué se desarrolló (resumen)

La sustitución para autorizaciones **ya existía** como registro documental. Esta entrega la **endurece** conforme a las decisiones del negocio (D-01…D-12). La sustitución sigue siendo **documental/informativa** (D-01): deja constancia trazable de quién cubre a un empleado durante su ausencia; **no** delega autoridad de aprobación (eso llega con el futuro módulo de Aprobaciones).

Cambios que impactan al Frontend:

1. **El "puesto" dejó de ser texto libre.** Ahora es una **referencia a una plaza activa del sustituto** (`substitutePositionSlotPublicId`). El backend guarda además un **snapshot del título** de esa plaza (`substitutePositionTitle`) para mostrarlo en listas/historial.
2. **`endDate` es obligatoria** (D-03). No se permiten sustituciones indefinidas.
3. **El tipo de sustitución es de catálogo** (`substitution-types`), ya no texto libre (D-08).
4. **Validación del sustituto**: debe existir, ser del mismo tenant, empleado **completado** y **activo** (RF-001).
5. **Bloqueo de solapes / único vigente** por titular (D-04/D-07) y **bloqueo si el sustituto no está disponible** (es titular de otra sustitución vigente que se solapa, D-06).
6. **Permiso dedicado de escritura** `PersonnelFiles.ManageSubstitutions` (D-09); las lecturas siguen con `PersonnelFiles.Read`.
7. **Auditoría** de cada operación (sin cambios de contrato para el Frontend).

> **Importante (breaking).** El payload de Add/Update cambió: `substitutePositionTitle` (string) → `substitutePositionSlotPublicId` (GUID), y `endDate` pasó de opcional a **obligatoria**. Hay que actualizar los formularios.

---

## 2. Modelo de datos (respuesta del API)

`PersonnelFileAuthorizationSubstitutionResponse` (JSON camelCase):

```jsonc
{
  "authorizationSubstitutionPublicId": "f3c1…",   // id de la sustitución
  "substitutionTypeCode": "VACACIONES",            // código de catálogo
  "substitutePersonnelFileId": "a91b…",            // expediente del sustituto
  "substitutePositionSlotPublicId": "7d22…",       // plaza del sustituto (FK PositionSlot)
  "substitutePositionTitle": "Supervisor de Soporte", // snapshot del título (solo lectura)
  "startDate": "2026-07-01T00:00:00Z",
  "endDate": "2026-07-15T00:00:00Z",               // obligatoria
  "isActive": true,
  "notes": "Cubre vacaciones.",
  "concurrencyToken": "11aa…"                       // usar en If-Match
}
```

- `substitutePositionTitle` es **solo lectura** (lo resuelve el backend desde la plaza al guardar). No se envía en el alta/edición.
- `concurrencyToken` se devuelve también en el header **`ETag`** y es obligatorio en el header **`If-Match`** de PUT/PATCH/DELETE.

---

## 3. Permisos y control de acceso

| Operación | Verbo | Permiso requerido |
|---|---|---|
| Listar / obtener | GET | `PersonnelFiles.Read` (o `Admin` / IAM super-admin) |
| Crear / editar / activar / eliminar | POST/PUT/PATCH/DELETE | **`PersonnelFiles.ManageSubstitutions`** (o `Admin` / IAM super-admin) |

- Las sustituciones viven ahora en un **controlador dedicado** (`PersonnelFileAuthorizationSubstitutionController`) cuyas escrituras exigen el permiso dedicado. **Las rutas no cambiaron** (ver §5), así que no hay impacto de URLs; solo cambia el permiso que el backend exige en escritura.
- Sin permiso de lectura → **401/403**. Sin `ManageSubstitutions` (ni `Admin`) al escribir → **403** (`PERSONNEL_FILES_FORBIDDEN`).
- **Sin autoservicio**: el empleado no gestiona su propio sustituto (solo RRHH).

> El Frontend debe **ocultar/inhabilitar** los botones de crear/editar/eliminar si el usuario no tiene `PersonnelFiles.ManageSubstitutions` ni `PersonnelFiles.Admin`.

---

## 4. Catálogos y datos de apoyo

### 4.1 Catálogo de tipos de sustitución (D-08)

```
GET /api/v1/general-catalogs/substitution-types?countryCode=SV
```

Respuesta (items activos, ordenados por `sortOrder`): cada item trae `{ id, category, code, name, isActive, sortOrder }`. Usar `code` como valor a enviar y `name` para mostrar.

Semilla SV:

| code | name |
|---|---|
| `VACACIONES` | Vacaciones |
| `INCAPACIDAD` | Incapacidad |
| `PERMISO` | Permiso |
| `MISION_OFICIAL` | Misión oficial |
| `LICENCIA` | Licencia |
| `OTRO` | Otro |

### 4.2 Plazas del sustituto (para el selector de "puesto", RF-003)

El "puesto" debe ser una **plaza de una asignación activa del sustituto**. Una vez elegido el sustituto, cargar sus asignaciones:

```
GET /api/v1/personnel-files/{substitutePersonnelFileId}/assigned-positions
```

- Filtrar por `isActive == true`; cada asignación expone `positionSlotPublicId` (el valor a enviar en `substitutePositionSlotPublicId`).
- **Default = plaza principal** del sustituto: si hay una sola plaza activa, autoseleccionarla; si hay varias, RRHH elige.
- El título legible de cada plaza se obtiene del módulo de Plazas (`PositionSlots`) si se necesita una etiqueta; el backend, al guardar, persiste el snapshot del título y lo devuelve en `substitutePositionTitle`.

---

## 5. Endpoints REST

Base: `/api/v1/personnel-files/{publicId}/authorization-substitutions` (donde `{publicId}` es el expediente **titular**, que debe estar **completado**).

| # | Método | Ruta | Permiso | Notas |
|---|---|---|---|---|
| 1 | GET | `/` | Read | Lista todas las sustituciones del titular |
| 2 | GET | `/{authorizationSubstitutionPublicId}` | Read | Una sustitución |
| 3 | POST | `/` | ManageSubstitutions | Crear → **201** + `Location` + `ETag` |
| 4 | PUT | `/{id}` | ManageSubstitutions | Reemplaza campos de negocio (no toca `isActive`); requiere `If-Match` |
| 5 | PATCH | `/{id}` | ManageSubstitutions | JSON Patch (incluye `isActive`); requiere `If-Match` |
| 6 | DELETE | `/{id}` | ManageSubstitutions | Requiere `If-Match`; devuelve el token del expediente padre |

### 5.1 Crear (POST)

```jsonc
// POST /api/v1/personnel-files/{titularId}/authorization-substitutions
{
  "substitutionTypeCode": "VACACIONES",
  "substitutePersonnelFilePublicId": "a91b…",
  "substitutePositionSlotPublicId": "7d22…",
  "startDate": "2026-07-01T00:00:00Z",
  "endDate":   "2026-07-15T00:00:00Z",
  "isActive": true,
  "notes": "Cubre vacaciones."
}
```

Respuesta **201 Created**, header `Location` → recurso creado, header `ETag` → `concurrencyToken` inicial.

### 5.2 Editar (PUT)

```jsonc
// PUT /api/v1/personnel-files/{titularId}/authorization-substitutions/{id}
// Header: If-Match: "{concurrencyToken}"
{
  "substitutionTypeCode": "INCAPACIDAD",
  "substitutePersonnelFilePublicId": "a91b…",
  "substitutePositionSlotPublicId": "7d22…",
  "startDate": "2026-07-01T00:00:00Z",
  "endDate":   "2026-07-20T00:00:00Z",
  "notes": "Se extiende por incapacidad."
}
```

- **PUT no modifica `isActive`** (se preserva). Para activar/desactivar usar PATCH.
- Devuelve **200** + nuevo `ETag`.

### 5.3 PATCH (JSON Patch, RFC 6902)

`Content-Type: application/json-patch+json`, header `If-Match` obligatorio.

```jsonc
[
  { "op": "replace", "path": "/isActive", "value": false },
  { "op": "replace", "path": "/endDate", "value": "2026-07-18T00:00:00Z" }
]
```

Rutas soportadas: `/substitutionTypeCode`, `/substitutePersonnelFileId`, `/substitutePositionSlotId`, `/startDate`, `/endDate`, `/notes`, `/isActive`.

- **No removibles**: `substitutePersonnelFileId`, `substitutePositionSlotId`, `startDate`, `endDate`, `isActive` (un `remove` sobre ellos → 400).
- `notes` y el tipo sí admiten `remove`/cambios.

> **Nota de naming (heredada).** En **PATCH** los campos son `substitutePersonnelFileId` y `substitutePositionSlotId`; en **Add/Update** son `substitutePersonnelFilePublicId` y `substitutePositionSlotPublicId`. Es una inconsistencia preexistente; respétala tal cual en cada endpoint.

### 5.4 DELETE

```
DELETE /api/v1/personnel-files/{titularId}/authorization-substitutions/{id}
Header: If-Match: "{concurrencyToken}"
```

Devuelve **200** con `{ "parentConcurrencyToken": "…" }` (token refrescado del expediente padre).

---

## 6. Concurrencia (If-Match / ETag)

1. GET la sustitución → leer `concurrencyToken` (o el header `ETag`).
2. En PUT/PATCH/DELETE enviar el header **`If-Match: "{concurrencyToken}"`**.
3. Si otro usuario modificó el recurso entre tanto → **409** `CONCURRENCY_CONFLICT`: refrescar (GET) y reintentar.
4. Tras una escritura exitosa, tomar el nuevo token del header `ETag` (o del body) para la siguiente operación.

---

## 7. Flujo de creación (perspectiva Frontend)

1. Abrir el expediente del **titular** (debe estar **completado**; si no, el alta dará `PERSONNEL_FILE_STATE_RULE_VIOLATION`).
2. **Buscar y seleccionar al sustituto** (selector con búsqueda por nombre, no GUID).
3. Cargar las **plazas activas del sustituto** (`GET …/assigned-positions`, filtrar `isActive`). Autoseleccionar la principal si hay una sola.
4. Seleccionar **tipo** del catálogo `substitution-types`.
5. Capturar **fecha inicio** y **fecha fin (obligatoria)**.
6. Enviar POST. Manejar errores según §8.
7. Mostrar el **estado efectivo** calculado (§9) y el `substitutePositionTitle` devuelto.

---

## 8. Validación y mapa de errores

Todos los mensajes son **bilingües (ES/EN)** y el `code` es estable. El body sigue el contrato ProblemDetails (`code`, `title`, `traceId`).

| Disparador | `code` | HTTP | Manejo sugerido en UI |
|---|---|---|---|
| `endDate` ausente | `common.validation` (campo `endDate`) | **400** | Marcar el campo fecha fin como requerido |
| `substitutePositionSlotPublicId` vacío | `common.validation` | **400** | Forzar selección de plaza |
| Auto-sustitución (sustituto == titular) | `common.validation` (campo `substitutePersonnelFileId`) | **400** | Bloquear elegir al propio titular |
| Sustituto no existe | `AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_FOUND` | **404** | "No se encontró al sustituto seleccionado." |
| Sustituto inelegible / de otro tenant | `AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_ELIGIBLE` | **422** | "Debe ser un empleado activo y completado de la misma empresa." |
| Tipo fuera de catálogo | `AUTHORIZATION_SUBSTITUTION_TYPE_CODE_INVALID` | **422** | Recargar catálogo / elegir un tipo válido |
| Plaza no es del sustituto | `AUTHORIZATION_SUBSTITUTION_POSITION_NOT_OWNED` | **422** | Recargar plazas del sustituto |
| Solape con sustitución vigente del titular | `AUTHORIZATION_SUBSTITUTION_PERIOD_OVERLAP` | **409** | "Ya existe una sustitución vigente que se solapa." |
| Sustituto no disponible (ausente) | `AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_UNAVAILABLE` | **422** | "El sustituto está siendo sustituido en ese período." |
| Titular no completado | `PERSONNEL_FILE_STATE_RULE_VIOLATION` | **422** | Completar el expediente primero |
| Sin permiso de escritura | `PERSONNEL_FILES_FORBIDDEN` | **403** | Ocultar acciones de gestión |
| `If-Match` no coincide | `CONCURRENCY_CONFLICT` | **409** | Refrescar y reintentar |
| Recurso/ítem no encontrado | `PERSONNEL_FILE_ITEM_NOT_FOUND` | **404** | — |

> `SUBSTITUTION_ALREADY_ACTIVE` (segundo vigente) queda **subsumido** por `…_PERIOD_OVERLAP`: con `endDate` obligatoria, "dos vigentes a la vez" equivale a "dos períodos activos que se solapan".

---

## 9. Estado efectivo (cálculo en cliente)

El API expone `isActive` + `startDate` + `endDate`. El **estado efectivo** se deriva en el Frontend respecto a "hoy":

- `INACTIVA` — `isActive == false`.
- `PROGRAMADA` — activa y `hoy < startDate`.
- `VIGENTE` — activa y `startDate <= hoy <= endDate`.
- `VENCIDA` — activa y `hoy > endDate` (señalar como inconsistencia: conviene desactivarla).

"¿Quién sustituye al titular hoy?" = la sustitución `isActive` cuyo rango contiene la fecha actual (a lo sumo una, por las reglas de no-solape).

---

## 10. Fuera de alcance (Fase 1)

- **No** delega autoridad de aprobación ni permisos técnicos (RBAC) — es documental (D-01).
- **No** hay notificaciones (D-11).
- **No** hay autoservicio del empleado (D-09).
- **No** hay vínculo con un módulo de Ausencias (diferido, D-10). La "disponibilidad" del sustituto se infiere solo de otras sustituciones, no de vacaciones/incapacidades reales.
- Ámbito = **empleado completo** (D-04): no hay sustitución por plaza individual.

---

## 11. Checklist de QA / casos de prueba

- [ ] Alta feliz (titular completado, sustituto válido con plaza activa, tipo de catálogo, fechas válidas) → 201.
- [ ] Sustituto inexistente / inactivo / candidato / de otro tenant → 404/422.
- [ ] Auto-sustitución (sustituto == titular) → 400.
- [ ] Tipo fuera de catálogo → 422.
- [ ] Plaza que no pertenece al sustituto (o inactiva) → 422.
- [ ] `endDate` ausente → 400; `endDate < startDate` → 400.
- [ ] Dos sustituciones del mismo titular con vigencias solapadas → 409.
- [ ] Sustituto que ya es titular de otra sustitución vigente solapada → 422.
- [ ] Programar una sustitución **futura no solapada** del mismo titular → permitido (201).
- [ ] PATCH `isActive=false` y luego reactivar sin solape → ok.
- [ ] PUT/PATCH/DELETE sin `If-Match` o con token viejo → 409.
- [ ] Usuario con solo `Read` intenta crear → 403; consulta → 200.

---

## 12. Trazabilidad

| Frontend | Decisión / RF |
|---|---|
| Plaza del sustituto (selector) | D-02 / RF-003 |
| Fecha fin obligatoria | D-03 / RF-004 |
| Catálogo de tipos | D-08 / RF-002 |
| Validación del sustituto | RF-001 |
| Bloqueo de solape / único vigente | D-04, D-07 / RF-004, RF-005 |
| Bloqueo por sustituto no disponible | D-06 / RF-007 |
| Permiso `ManageSubstitutions` | D-09 / RF-008 |
| Estado efectivo en cliente | RF-006 |
| Concurrencia If-Match/ETag | RNF |

> **Cobertura de pruebas backend.** Reglas puras (`AuthorizationSubstitutionRulesTests`), patch (`PersonnelFileAuthorizationSubstitutionPatchTests`), paridad de localización y gobernanza de políticas pasan (unit). El guardrail de políticas de integración valida el nuevo controlador end-to-end. Los casos de §11 quedan como verificación de integración cuando se siembre un empleado completado con plaza activa en el harness.
