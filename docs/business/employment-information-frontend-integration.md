# Guía de integración Frontend — Información Laboral del Empleado

| | |
|---|---|
| **Para** | Equipo Frontend |
| **Tipo** | Guía de integración + **cambios de contrato (BREAKING)** |
| **Módulos** | Personnel Files · **Información Laboral** (antes "Employee Profile") · Employment Assignments · Catálogos · Recontratación |
| **Backend de referencia** | migración `20260621191139_SimplifyEmployeeProfileAndAssignmentContract` |
| **Relacionado** | `docs/business/multi-plaza-frontend-integration.md` (múltiples plazas) |

---

## 1. TL;DR (qué cambió y qué tenés que hacer)

0. **El endpoint se renombró**: `…/employee-profile` → **`…/employment-information`**. El concepto pasa de "Perfil/Ficha del Empleado" a **"Información Laboral"**. Hay que actualizar la URL en el FE.
1. **La Información Laboral del empleado (`employment-information`) se adelgazó.** Ahora **solo** maneja: código de empleado, **estado del empleado** (catálogo), fecha de ingreso, antigüedad (calculada), correo institucional (lectura) y los días vigentes de vacaciones/incapacidad (lectura, aún nulos). Todo lo demás salió de esta sección.
2. **Los datos de contrato y de estructura salieron de aquí y viven por plaza** en las **assigned-positions**: `contractTypeCode`, `workdayCode`, `payrollTypeCode`, y `orgUnitId` / `workCenterId` / `costCenterId`.
3. **`employmentStatusCode` ya no es texto libre**: debe venir de un **catálogo nuevo** → `GET /api/v1/general-catalogs/employment-statuses?countryCode=SV`. Enviar un código inválido devuelve `422 EMPLOYMENT_STATUS_CODE_INVALID`.
4. **Desapareció el booleano `isEmploymentActive`.** El estado activo/baja ahora se deriva del **`employmentStatusCode`** (p. ej. `RETIRADO` = baja) y/o de `retirementDate`.
5. **La antigüedad la calcula el backend** y viene en la respuesta como objeto `seniority { years, months, days, totalDays }`. El FE ya no necesita calcularla.
6. **`institutionalEmail` ahora es editable** desde el `PUT` (antes era solo lectura): es el correo de **login** del empleado, así que cambiarlo re-sincroniza su cuenta. Omitir/`null` = sin cambios; `409 PERSONNEL_FILE_LINKED_USER_CONFLICT` si el correo ya está en uso. Ver §3.2.
7. **Nuevos campos de solo lectura en la respuesta**: `seniority`, y `vacationDaysAvailable` / `disabilityDaysAvailable` que **por ahora siempre son `null`** (los gestionará el futuro módulo de vacaciones/incapacidades).

> ⚠️ Son **cambios incompatibles** de contrato (incluido el renombre de la ruta). No hay migración de datos: los campos viejos desaparecen. El FE debe ajustar URL, requests y lecturas.

---

## 2. Modelo conceptual: antes vs ahora

**Antes** — la "Información Laboral" (entonces `employee-profile`) mezclaba identidad + contrato + estructura (~18 campos):

```
EmploymentInformation {
  employeeCode, employmentStatusCode, isEmploymentActive, contractTypeCode,
  hireDate, retirement*, workdayCode, payrollTypeCode,
  orgUnitId, workCenterId, costCenterId, contractStartDate, contractEndDate,
  vacationConfigurationJson
}
```

**Ahora** — la sección es mínima; el contrato/estructura se resuelven desde la plaza:

```
EmploymentInformation {                   EmploymentAssignment (plaza principal activa) {
  employeeCode,                              assignmentTypeCode,
  employmentStatusCode,   ← catálogo         contractTypeCode,   ← MOVIDO aquí
  hireDate,                                  workdayCode,        ← MOVIDO aquí
  retirement*,                               payrollTypeCode,    ← MOVIDO aquí
  // derivados (solo lectura):               orgUnitId,          ← se resuelve aquí
  institutionalEmail,                        workCenterId,       ← se resuelve aquí
  seniority,                                 costCenterId,       ← se resuelve aquí
  vacationDaysAvailable,  (null por ahora)   startDate, endDate  ← fechas del contrato
  disabilityDaysAvailable (null por ahora)
}                                          }
```

---

## 3. Endpoint — `GET` / `PUT /api/v1/personnel-files/{id}/employment-information`

> Renombrado desde `…/employee-profile`. El prefijo `personnel-files` y los demás sub-recursos (datos personales, direcciones, aficiones, beneficiarios, assigned-positions, etc.) **no cambian**.

### 3.1 Request del `PUT` (`UpdatePersonnelFileEmployeeProfileRequest`)

**Campos que se ELIMINARON del body** (ya no los mandes):
`isEmploymentActive`, `contractTypeCode`, `workdayCode`, `payrollTypeCode`, `orgUnitPublicId`, `workCenterPublicId`, `costCenterPublicId`, `contractStartDate`, `contractEndDate`, `vacationConfigurationJson`.

**Body nuevo (completo):**

```jsonc
// PUT /api/v1/personnel-files/{id}/employment-information
// Header: If-Match: "<concurrencyToken>"   (en la 1ª creación no se requiere)
{
  "employeeCode": "EMP-0001",
  "employmentStatusCode": "ACTIVO",          // ← debe existir en el catálogo (ver §4)
  "hireDate": "2024-03-01T00:00:00Z",        // fecha de ingreso a la compañía (ancla de antigüedad)
  "retirementCategoryCode": null,            // metadatos de baja (opcionales)
  "retirementReasonCode": null,
  "retirementNotes": null,
  "retirementDate": null,
  "institutionalEmail": "ana.perez@empresa.test"  // ← EDITABLE: mándalo para cambiarlo; omítelo/null para dejarlo igual
}
```

### 3.2 Response del `GET`/`PUT` (`PersonnelFileEmployeeProfileResponse`)

```jsonc
{
  "id": "…",
  "employeeCode": "EMP-0001",
  "employmentStatusCode": "ACTIVO",
  "institutionalEmail": "ana.perez@empresa.test",  // ← editable vía el PUT (es el login del empleado)
  "hireDate": "2024-03-01T00:00:00Z",
  "seniority": {                                    // ← NUEVO, solo lectura (lo calcula el backend)
    "years": 2, "months": 3, "days": 20, "totalDays": 842
  },
  "retirementCategoryCode": null,
  "retirementReasonCode": null,
  "retirementNotes": null,
  "retirementDate": null,
  "vacationDaysAvailable": null,                    // ← NUEVO, hoy SIEMPRE null (módulo futuro)
  "disabilityDaysAvailable": null,                  // ← NUEVO, hoy SIEMPRE null (módulo futuro)
  "concurrencyToken": "…",
  "createdAtUtc": "…",
  "modifiedAtUtc": null
}
```

> El `GET` sigue devolviendo **`200` con body `null`** si la sección aún no se creó (no es un `404`). Sin cambios ahí.

**Acciones FE:**
- Actualizar la URL del endpoint a `…/employment-information`.
- Quitar del formulario los campos de contrato/estructura. Esos datos se editan ahora en la **pantalla de plazas (assigned-positions)**.
- Mostrar `seniority` directamente (no recalcular). Si querés un texto: `"{years}a {months}m {days}d"`.
- Tratar `vacationDaysAvailable` / `disabilityDaysAvailable` `null` como "no disponible" (—). No asumas `0`.
- `institutionalEmail` ahora es **editable** desde este `PUT`. Es el identificador de la **cuenta de inicio de sesión** del empleado, así que al cambiarlo el backend actualiza también esa cuenta (el empleado inicia sesión con el nuevo correo, misma contraseña). Mándalo para cambiarlo; **omítelo o `null` para dejarlo igual** (no se puede vaciar mientras haya cuenta vinculada). Si el correo ya pertenece a otra cuenta devuelve `409 PERSONNEL_FILE_LINKED_USER_CONFLICT`; si no es un email válido, `422`.

---

## 4. Catálogo nuevo — Estado del Empleado

```
GET /api/v1/general-catalogs/employment-statuses?countryCode=SV
```

Respuesta (mismo shape que los demás general-catalogs):

```jsonc
[
  { "id":"…", "category":"EmploymentStatus", "code":"ACTIVO",      "name":"Activo",      "isSystem":false, "isActive":true, "sortOrder":10 },
  { "id":"…", "category":"EmploymentStatus", "code":"SUSPENDIDO",  "name":"Suspendido",  "isSystem":false, "isActive":true, "sortOrder":20 },
  { "id":"…", "category":"EmploymentStatus", "code":"LICENCIA",    "name":"Licencia",    "isSystem":false, "isActive":true, "sortOrder":30 },
  { "id":"…", "category":"EmploymentStatus", "code":"INCAPACIDAD", "name":"Incapacidad", "isSystem":false, "isActive":true, "sortOrder":40 },
  { "id":"…", "category":"EmploymentStatus", "code":"RETIRADO",    "name":"Retirado",    "isSystem":false, "isActive":true, "sortOrder":50 }
]
```

> Para `SV` este catálogo viene **sembrado por defecto** (ACTIVO/SUSPENDIDO/LICENCIA/INCAPACIDAD/RETIRADO) en todos los ambientes vía migración; el endpoint ya no responde `404`. Si un ambiente venía sin estos datos, se rellenan al aplicar las migraciones.

**Acciones FE:**
- Poblar el dropdown de "Estado del Empleado" con este endpoint (usar `code` para enviar, `name` para mostrar, `sortOrder` para ordenar).
- Es **country-scoped**: pasar `countryCode` (hoy `SV`).
- Enviar un `employmentStatusCode` que no esté en el catálogo → `422 EMPLOYMENT_STATUS_CODE_INVALID`.

---

## 5. ¿Dónde leo ahora lo que salió de la Información Laboral?

| Dato que antes estaba aquí | Dónde leerlo ahora |
|---|---|
| `contractTypeCode`, `workdayCode`, `payrollTypeCode` | En la **employment-assignment** del empleado (la principal activa: `isPrimary && isActive`). |
| `contractStartDate` / `contractEndDate` | Son las **fechas de la asignación**: `startDate` / `endDate` de la plaza. |
| `orgUnitId`, `workCenterId`, `costCenterId` | En la **employment-assignment** (principal activa). |
| `isEmploymentActive` (booleano) | **Derivar del estado:** `employmentStatusCode === "RETIRADO"` (o `retirementDate != null`) ⇒ baja; si no, activo. |
| `vacationConfigurationJson` | Eliminado. Los saldos los servirá el futuro módulo de vacaciones/incapacidades (`vacationDaysAvailable` / `disabilityDaysAvailable`). |

Para obtener la plaza principal activa:
```
GET /api/v1/personnel-files/{id}/assigned-positions
→ filtrar item.isPrimary === true && item.isActive === true
```

---

## 6. Employment-assignments — campos de contrato nuevos (ADITIVO)

Las plazas (`assigned-positions`) **ganaron 3 campos opcionales** de contrato. Es aditivo: lo que ya enviabas sigue funcionando.

**Request (`POST` / `PUT` / `PATCH`):** ahora acepta `contractTypeCode`, `workdayCode`, `payrollTypeCode` (todos opcionales, máx. 80 chars).

```jsonc
// POST /api/v1/personnel-files/{id}/assigned-positions
{
  "assignmentTypeCode": "INDEFINIDO",   // ya existía (catálogo assignment-types)
  "contractTypeCode": "INDEFINIDO",     // ← NUEVO (opcional, texto libre por ahora)
  "workdayCode": "DIURNA",              // ← NUEVO (opcional)
  "payrollTypeCode": "MENSUAL",         // ← NUEVO (opcional)
  "positionSlotPublicId": "…",
  "orgUnitPublicId": "…",
  "workCenterPublicId": "…",
  "costCenterPublicId": "…",
  "startDate": "2024-03-01T00:00:00Z",  // = inicio del contrato de esta plaza
  "endDate": null,                      // = fin del contrato (null = indefinido)
  "isPrimary": true,
  "isActive": true,
  "notes": null
}
```

**Response (`GET`):** incluye los 3 campos nuevos (después de `assignmentTypeCode`):

```jsonc
{
  "employmentAssignmentPublicId": "…",
  "assignmentTypeCode": "INDEFINIDO",
  "contractTypeCode": "INDEFINIDO",   // ← NUEVO
  "workdayCode": "DIURNA",            // ← NUEVO
  "payrollTypeCode": "MENSUAL",       // ← NUEVO
  "positionSlotPublicId": "…",
  "orgUnitPublicId": "…", "workCenterPublicId": "…", "costCenterPublicId": "…",
  "startDate": "…", "endDate": null,
  "isPrimary": true, "isActive": true,
  "notes": null,
  "concurrencyToken": "…"
}
```

**PATCH (JSON Patch):** ahora son parcheables `/contractTypeCode`, `/workdayCode`, `/payrollTypeCode` (además de los existentes).

> Nota: `assignmentTypeCode` (catálogo `assignment-types`) describe la **modalidad** de la asignación; `contractTypeCode` es un campo libre adicional para el tipo de contrato de la plaza. Si en tu UI ya cubrís la modalidad con `assignmentTypeCode`, `contractTypeCode` puede quedar opcional/oculto.

---

## 7. Recontratación — sin cambios de contrato, pero ojo con las lecturas

El endpoint `POST /api/v1/personnel-files/{id}/rehire` y su body **no cambiaron** (sigue con `contractTypeCode`, `contractStartDate`, `contractEndDate`, `positionSlotPublicId`, `assignmentTypeCode`, etc.). Internamente, esos datos de contrato ahora alimentan la **nueva plaza** del período recontratado.

**Lo que cambia para el FE es la lectura post-recontratación:**
- La Información Laboral queda con `employmentStatusCode: "ACTIVO"` y `retirementDate: null` (ya no existe `isEmploymentActive`).
- La elegibilidad para recontratar la determina el backend por `retirementDate != null` (empleado retirado). El FE no necesita calcularla; basta con leer el estado.

> Nota: si el `finalize` devuelve *validation issues*, la clave de navegación de esta sección cambió de `"employee-profile"` a **`"employment-information"`** (consistente con la ruta nueva).

---

## 8. Códigos de error nuevos / relevantes

| HTTP | Code | Cuándo |
|---|---|---|
| `422` | `EMPLOYMENT_STATUS_CODE_INVALID` | `employmentStatusCode` no existe / no está activo en el catálogo del país. |
| `422` | `PERSONNEL_FILE_STATE_RULE_VIOLATION` | Intentar `PUT` sobre un expediente que no es **empleado completado**. (sin cambios) |
| `422` | `PERSONNEL_FILE_CONCURRENCY_CONFLICT` | `If-Match` desactualizado en el `PUT`. (sin cambios) |

---

## 9. Checklist de migración FE

- [ ] **Cambiar la URL** del endpoint: `…/employee-profile` → `…/employment-information` (GET y PUT). Rebrandear la pantalla a "Información Laboral".
- [ ] **Quitar** del form/payload: `isEmploymentActive`, `contractTypeCode`, `workdayCode`, `payrollTypeCode`, `orgUnitPublicId`, `workCenterPublicId`, `costCenterPublicId`, `contractStartDate`, `contractEndDate`, `vacationConfigurationJson`.
- [ ] **Estado del empleado**: cambiar el input libre por un dropdown alimentado de `GET …/general-catalogs/employment-statuses?countryCode=SV`. Manejar `422 EMPLOYMENT_STATUS_CODE_INVALID`.
- [ ] **Eliminar** cualquier uso del booleano `isEmploymentActive`; derivar activo/baja desde `employmentStatusCode` (`RETIRADO`) / `retirementDate`.
- [ ] **Antigüedad**: leer y mostrar `seniority` (objeto) en vez de calcular desde `hireDate`.
- [ ] **Correo institucional**: permitir **editarlo** en el formulario y mandarlo en el `PUT`. Manejar `409 PERSONNEL_FILE_LINKED_USER_CONFLICT` (correo en uso) y `422` (formato). Avisar al usuario que cambia el correo de **inicio de sesión** del empleado.
- [ ] **Días de vacaciones/incapacidad**: leer `vacationDaysAvailable` / `disabilityDaysAvailable`, tolerar `null` (mostrar "—").
- [ ] **Contrato y estructura (UO/CC/CT)**: mover su edición/visualización a la pantalla de **assigned-positions**; leerlos de la **plaza principal activa**.
- [ ] **Plazas**: (opcional) agregar inputs para `contractTypeCode` / `workdayCode` / `payrollTypeCode` en el alta/edición de la asignación.
- [ ] Revisar pantallas de **recontratación**: post-rehire el estado es `ACTIVO` (no `isEmploymentActive`); y la nav key del finalize es `employment-information`.
