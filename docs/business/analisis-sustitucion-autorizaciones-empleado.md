# Análisis de Negocio — Sustitución para Autorizaciones (ausencia del empleado)

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Empleo/Plazas (`PersonnelFileEmploymentAssignment`, `PersonnelFileAuthorizationSubstitution`) · Plazas (`PositionSlots`) · Catálogos generales (`GeneralCatalogs`) · Identidad/Permisos (`IdentityAccess`/Provisioning) · (futuro) Ausencias/Vacaciones · (futuro) Aprobaciones/Workflow |
| **Estado** | **Definido / Cerrado (Fase 1 — registro documental).** Decisiones ratificadas **D-01…D-12** (respuestas del negocio del 2026-06-21). **Fases 2–3** (delegación efectiva de autoridad, vínculo con Ausencias) **diferidas**. Listo para diseño técnico de Fase 1. |
| **Versión** | v2 (incorpora decisiones del negocio P-01…P-12) |
| **Fecha** | 2026-06-21 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |

---

## Contexto del cambio

En el **expediente del empleado** se requiere una sección para **definir quién podrá sustituir al empleado en caso de ausencia**, registrando: **empleado sustituto, puesto, fecha inicio y fecha fin**. El objetivo declarado fue **doble**: (1) **validar** que esto esté **bien alineado con las sustituciones ya implementadas** y (2) **analizar y agregar** la información/reglas necesarias para un HRIS robusto.

> **Hallazgo clave (confirmado en código).** Esto **no es un desarrollo desde cero**. CLARIHR **ya tiene implementada** la sustitución para autorizaciones como entidad `PersonnelFileAuthorizationSubstitution`, con **CRUD completo** (Domain + Application/CQRS + API REST + auditoría + concurrencia). Lo que existe hoy es un **registro documental** (quién sustituye, tipo, vigencia, notas). El negocio **ratificó** (D-01) que **en esta fase la sustitución es documental/informativa**: **no** delega autoridad de aprobación todavía (no existe motor de aprobaciones). El trabajo de Fase 1 es de **endurecimiento y alineación**: validar la referencia al sustituto, catalogar el tipo, convertir "puesto" en **referencia a la plaza del sustituto**, hacer **obligatoria la fecha fin**, **bloquear** solapes/duplicados, fijar **ámbito = empleado completo**, y proteger la gestión con un **permiso dedicado**.

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) | Decisión que aplica |
|---|---|---|---|
| 1 | **Entidad** | `PersonnelFileAuthorizationSubstitution` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:421`). Campos: `SubstitutionTypeCode`, `SubstitutePersonnelFilePublicId`, `SubstitutePositionTitle?`, `StartDate`, `EndDate?`, `IsActive`, `Notes?`, `ConcurrencyToken`. | Base correcta; se endurece (D-02…D-09). |
| 2 | **Sustituto = referencia** | `SubstitutePersonnelFilePublicId` es `Guid` a otro expediente; solo se valida `NotEmpty()` (`AuthorizationSubstitutions.cs:86`). | Validar existencia/tenant/empleado/activo (RF-001). |
| 3 | **Tipo de sustitución** | `SubstitutionTypeCode`: `NotEmpty().MaximumLength(80)` + `Clean()`; **sin** catálogo (`AuthorizationSubstitutions.cs:85`). | Catálogo `substitution-types` (D-08, RF-002). |
| 4 | **Puesto** | `SubstitutePositionTitle?` **texto libre opcional** (`PersonnelFileEmployee.cs:455`). | Pasa a **referencia a la plaza del sustituto** (D-02, RF-003). |
| 5 | **Vigencia** | `StartDate` requerida; `EndDate?` **opcional**; única regla `StartDate ≤ EndDate` (`AuthorizationSubstitutions.cs:87`). **Sin** solape/único activo. | `EndDate` obligatoria (D-03); **bloquear** solape/duplicado (D-07, RF-004/005). |
| 6 | **Estado activo** | `IsActive` **manual**; en `PUT` se preserva, solo cambia vía `PATCH` (`…Handlers.cs:145`). | Estado **efectivo** derivado de vigencia (RF-006). |
| 7 | **Reglas existentes** | Solo expedientes **completados** (`IsCompletedEmployee`, `…Handlers.cs:46`); **prohíbe auto-sustitución** (`…Handlers.cs:51`). | Se conservan (RN-01, RN-02). |
| 8 | **Capa de aplicación** | CQRS completo Add/Update/Patch/Delete/Get/GetList (`AuthorizationSubstitutions.cs` + `.Handlers.cs`). **No existe** `AuthorizationSubstitutions.Rules.cs`. | Crear módulo de reglas puro (RNF). |
| 9 | **API** | 6 endpoints REST `/api/v1/personnel-files/{publicId}/authorization-substitutions` (`Api/Controllers/PersonnelFileEmploymentController.cs`); contratos `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:194`. | Se conservan; se agregan validaciones y permiso (D-09). |
| 10 | **Permisos** | Escrituras vía `LoadForManageAsync` (**Manage** genérico); lecturas vía `LoadCompletedEmployeeForReadAsync` (**Read**). Sin permiso específico. | **Permiso dedicado** de escritura (D-09, RF-008). |
| 11 | **Concurrencia/Auditoría** | `ConcurrencyToken`+`If-Match`/`ETag`; auditoría por operación vía `PersonnelFileEmployeeAudits.LogUpdateAsync` (`…Handlers.cs:80,167,274,342`). | Auditoría con **diff** + **historial visible** (D-12, RF-009). |
| 12 | **Autoridad / Aprobaciones** | **No existe** motor de aprobaciones general (solo `SalaryTabulator.Approve`); **no existe** jerarquía empleado→jefe (solo `OrgUnit.ManagerEmployeeId`); **no existe** módulo de **ausencias**. | Delegación efectiva **diferida** (D-01, D-10, RF-010). |

---

## Decisiones del negocio (ratificadas — 2026-06-21)

| # | Pregunta | Decisión |
|---|---|---|
| **D-01** | Naturaleza (¿delega autoridad?) | **Documental / informativa** en esta fase. **No** delega autoridad de aprobación (no hay workflow). La delegación efectiva (G-01) queda **diferida** al futuro módulo de Aprobaciones (RF-010). |
| **D-02** | "Puesto" del campo | **Referencia a la plaza** del **sustituto** (`PositionSlot`), validada contra sus **asignaciones activas**; **default = plaza principal** del sustituto. Sustituye al texto libre `SubstitutePositionTitle`. |
| **D-03** | ¿Fecha fin obligatoria? | **Sí, obligatoria.** **No** se permiten sustituciones indefinidas. |
| **D-04** | Ámbito | **Empleado completo** (cubre todas sus plazas/funciones). **No** se modela ámbito por plaza. |
| **D-05** | ¿Aprueba el jefe? ¿Relación jerárquica? | **No** hay aprobación del jefe (no existe workflow). El sustituto **no** requiere relación jerárquica con el titular. |
| **D-06** | Sustituto ausente/sustituido en el período | **Bloquear.** No se puede designar como sustituto a quien está él mismo **siendo sustituido** (ausente) en un período solapado. *(La "ausencia real" vía módulo de Ausencias se concilia cuando exista — D-10.)* |
| **D-07** | Traslape / segundo activo | **Bloquear** (no supersesión automática). A lo sumo **una** sustitución vigente por titular. |
| **D-08** | Catálogo de tipos (SV) | `substitution-types`: **VACACIONES, INCAPACIDAD, PERMISO, MISION_OFICIAL, LICENCIA, OTRO**. |
| **D-09** | ¿Quién gestiona? Permiso | **Solo RRHH** (gestión). Controlado por **permiso dedicado** `PersonnelFiles.ManageSubstitutions`. **Sin** autoservicio del empleado. |
| **D-10** | Vínculo con ausencia real | **Diferido.** Se relacionará con la solicitud de vacaciones/incapacidad **cuando exista** el módulo de Ausencias. |
| **D-11** | Notificaciones | **No** se requieren en esta fase. |
| **D-12** | Auditoría | **Sí**: registrar **diff antes/después** y exponer un **historial visible** al usuario. |

---

## Brechas verificadas y su resolución (GAP → Decisión)

| # | Brecha (as-is) | Resolución (to-be) |
|---|---|---|
| **G-01** | "Para autorizaciones" sugiere delegar autoridad; hoy es documental. | **Diferida** (D-01). En Fase 1 se comunica como registro documental; la delegación llega con Aprobaciones (RF-010). |
| **G-02** | `SubstitutionTypeCode` texto libre. | Catálogo `substitution-types` (D-08, RF-002). |
| **G-03** | `SubstitutePersonnelFileId` sin validar (existencia/tenant/estado/tipo). | Validación referencial completa (RF-001). |
| **G-04** | `SubstitutePositionTitle` texto libre ambiguo. | Referencia a la **plaza del sustituto** (D-02, RF-003). |
| **G-05** | Sin detección de solape. | **Bloqueo** por solape (D-07, RF-004) reusando `RangesOverlap` (`Employment/EmploymentAssignments.Rules.cs:96`). |
| **G-06** | Sin "único activo"/supersesión. | **Único activo por titular**, **bloqueando** el segundo (D-04, D-07, RF-005). |
| **G-07** | `IsActive` manual, no derivado de vigencia. | **Estado efectivo** derivado de fechas (RF-006). |
| **G-08** | `EndDate` opcional. | **Obligatoria** (D-03, RF-004). |
| **G-09** | Sin módulo de reglas puro. | Crear `AuthorizationSubstitutions.Rules.cs` (RNF). |
| **G-10** | Sin validar disponibilidad del sustituto. | **Bloqueo** si el sustituto está sustituido/ausente en el período (D-06, RF-007). |
| **G-11** | Sin notificaciones. | **Fuera de alcance** en esta fase (D-11). |
| **G-12** | Ámbito indefinido (multi-plaza). | **Empleado completo** (D-04) — no se agrega campo de ámbito por plaza. |

---

## 1. Resumen del producto o requerimiento

Sección del expediente para **designar un sustituto durante la ausencia del empleado** (vacaciones, incapacidad, permiso, etc.), indicando **quién** sustituye, **qué plaza ocupa el sustituto**, y **desde/hasta** cuándo. En esta fase es un **registro documental/informativo** (D-01): deja constancia trazable del responsable designado, **sin** delegar autoridad de aprobación (eso requiere el futuro módulo de Aprobaciones).

La funcionalidad **ya existe** (`PersonnelFileAuthorizationSubstitution`); este alcance la **valida y endurece** conforme a las decisiones D-01…D-12: validación del sustituto, catálogo de tipos, puesto como referencia a la plaza del sustituto, fecha fin obligatoria, bloqueo de solapes/duplicados, ámbito de empleado completo, permiso dedicado y auditoría con historial visible.

---

## 2. Objetivos del negocio

- **O-1.** Garantizar **continuidad operativa**: toda ausencia con un **responsable designado** explícito y trazable.
- **O-2.** **Integridad de datos**: sustituto **real, activo y del mismo tenant**; tipo **estandarizado** (catálogo); puesto como **referencia a plaza**.
- **O-3.** **Claridad de vigencia**: saber sin ambigüedad **quién es el sustituto vigente** en una fecha dada (un solo activo por titular, sin solapes).
- **O-4.** **Trazabilidad y cumplimiento**: historial **auditable y visible** de designaciones (D-12).
- **O-5.** **Escalabilidad**: dejar la entidad lista como **fuente de verdad** para que los futuros módulos de **Aprobaciones** y **Ausencias** la consuman (D-01, D-10).

---

## 3. Alcance funcional (Fase 1)

- **F1.** Endurecimiento del **registro de sustituciones** existente (CRUD vía API REST).
- **F2.** **Catálogo** `substitution-types` con seed SV (D-08).
- **F3.** **Validación referencial** del sustituto (existencia, tenant, tipo Empleado, activo) + anti–auto-sustitución (ya existe).
- **F4.** **Puesto = referencia a la plaza del sustituto** (D-02).
- **F5.** **Reglas de vigencia**: orden de fechas (existe), **fecha fin obligatoria** (D-03), **bloqueo** de solape y **único activo** por titular (D-04, D-07), **estado efectivo** derivado.
- **F6.** **Bloqueo** si el sustituto está sustituido/ausente en el período (D-06).
- **F7.** **Permiso dedicado** de gestión `PersonnelFiles.ManageSubstitutions` (D-09).
- **F8.** **Auditoría** con diff antes/después e **historial visible** (D-12).

---

## 4. Fuera de alcance

- **FA-1.** **Motor de aprobaciones/workflow** y **delegación efectiva** de autoridad (D-01; se consumirá la sustitución cuando exista — RF-010).
- **FA-2.** **Módulo de Ausencias/Vacaciones/Incapacidades** y el **vínculo** con la ausencia real (D-10).
- **FA-3.** **Jerarquía/aprobación del jefe** sobre la designación (D-05).
- **FA-4.** **Notificaciones** al sustituto/RRHH/jefe (D-11).
- **FA-5.** **Ámbito por plaza/función** (D-04: ámbito = empleado completo).
- **FA-6.** **Autoservicio** del empleado para gestionar su sustituto (D-09: solo RRHH).
- **FA-7.** Delegación de **permisos técnicos/RBAC** del titular al sustituto.
- **FA-8.** **Sustitución en cascada** (sustituto del sustituto) y **firma electrónica**.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Analista / Gestor de RRHH** | **Único** que crea/edita/activa/elimina sustituciones (permiso dedicado `PersonnelFiles.ManageSubstitutions`) y consulta el historial (D-09). |
| **Empleado titular (ausente)** | Sujeto de la sustitución. **Sin** autoservicio en esta fase (D-09). |
| **Empleado sustituto** | Empleado designado para cubrir al titular. **No** requiere relación jerárquica con el titular (D-05). Sin notificación en esta fase (D-11). |
| **Auditor / Cumplimiento** | Consulta la bitácora/historial visible de designaciones (D-12). |
| **Sistema — futuro módulo de Aprobaciones** | (Diferido) Consumirá la sustitución **vigente** para enrutar autorizaciones (RF-010). |
| **Sistema — futuro módulo de Ausencias** | (Diferido) Fuente del **período de ausencia** a conciliar (D-10). |

---

## 6. Requerimientos funcionales

### RF-001 — Validar la referencia del empleado sustituto
**Descripción:** Validar que `SubstitutePersonnelFileId` corresponda a un expediente **existente**, del **mismo tenant**, de tipo **Empleado** (no candidato), **completado** y **activo**; reafirmar la regla **anti–auto-sustitución** (ya existe).
**Reglas de negocio:**
- El sustituto **debe existir** y ser del **mismo tenant** (hoy no se valida — G-03).
- El sustituto **debe** ser **empleado completado** y **activo** a la fecha de la designación.
- El sustituto **no puede** ser el propio titular (ya implementado — `…Handlers.cs:51`).
**Criterios de aceptación:**
- Dado un `substitutePersonnelFileId` inexistente o de otro tenant, cuando se guarda, entonces **422** `SUBSTITUTE_NOT_FOUND` / `SUBSTITUTE_INVALID_TENANT`.
- Dado un sustituto **candidato** o **inactivo/baja**, entonces **422** `SUBSTITUTE_NOT_ELIGIBLE`.
- Dado el propio titular como sustituto, entonces **422** (auto-sustitución — ya cubierto).
**Prioridad:** Alta · **Dependencias:** `IPersonnelFileEmployeeRepository` / `IPersonnelFileRepository`.

### RF-002 — Catalogar el tipo de sustitución
**Descripción:** Sustituir `SubstitutionTypeCode` de **texto libre** por el **catálogo** `substitution-types` (país-scoped), validado como activo.
**Reglas de negocio:**
- `SubstitutionTypeCode` **debe** existir y estar **activo** en el catálogo del tenant/país.
- **Seed SV (D-08):** `VACACIONES`, `INCAPACIDAD`, `PERMISO`, `MISION_OFICIAL`, `LICENCIA`, `OTRO`.
**Criterios de aceptación:**
- Dado un código fuera de catálogo, entonces **422** `SUBSTITUTION_TYPE_CODE_INVALID`.
- Dado un código válido, entonces se acepta y persiste normalizado.
**Prioridad:** Alta · **Dependencias:** `GeneralCatalogs` (`CountryScopedCatalogItem`).

### RF-003 — Puesto como referencia a la plaza del sustituto
**Descripción:** Reemplazar `SubstitutePositionTitle` (texto) por `SubstitutePositionSlotPublicId` (**FK a `PositionSlot`**), que **debe** ser una de las **plazas/asignaciones activas del sustituto**; **default = plaza principal** del sustituto (D-02). Conservar un *snapshot* opcional del título para historial.
**Reglas de negocio:**
- La plaza referenciada **debe** pertenecer a una **asignación activa** del **sustituto** (no del titular).
- Si el sustituto tiene una sola plaza activa, se **autoselecciona** (su principal); si tiene varias, RRHH **elige** entre ellas.
- Campo **obligatorio** (al igual que los demás campos del requerimiento).
**Criterios de aceptación:**
- Dada la selección del sustituto, cuando se abre el campo puesto, entonces el sistema **sugiere su plaza principal activa** (editable entre sus plazas activas).
- Dada una plaza que **no** pertenece a una asignación activa del sustituto, entonces **422** `SUBSTITUTE_POSITION_NOT_OWNED`.
**Prioridad:** Alta · **Dependencias:** `PersonnelFileEmploymentAssignment`, `PositionSlot`.

### RF-004 — Vigencia: fecha fin obligatoria y sin solape
**Descripción:** Reglas de fechas: orden (existe), **fecha fin obligatoria** (D-03) y **no solapamiento** de sustituciones del **mismo titular** (ámbito = empleado completo, D-04).
**Reglas de negocio:**
- `StartDate ≤ EndDate` (ya implementado — `AuthorizationSubstitutions.cs:87`).
- `EndDate` **obligatoria**; **no** se permiten sustituciones indefinidas (D-03).
- **No** se permiten dos sustituciones del mismo titular cuyas vigencias **se solapen** — **bloqueo** (D-07), reusando `RangesOverlap` (`Employment/EmploymentAssignments.Rules.cs:96`).
**Criterios de aceptación:**
- Dado `EndDate` ausente, entonces **422** `SUBSTITUTION_END_DATE_REQUIRED`.
- Dado `EndDate < StartDate`, entonces **422** (ya cubierto).
- Dadas dos sustituciones del mismo titular **solapadas**, cuando se guarda la segunda, entonces **422** `SUBSTITUTION_PERIOD_OVERLAP`.
**Prioridad:** Alta · **Dependencias:** módulo de reglas (G-09).

### RF-005 — Único sustituto activo por titular (bloqueo)
**Descripción:** A lo sumo **una** sustitución **activa/vigente** por titular (D-04). Activar/crear una segunda que coincida en el tiempo se **bloquea** (sin supersesión automática — D-07).
**Reglas de negocio:**
- A lo sumo **un** sustituto activo por titular y momento.
- Intentar activar otro que solape → **rechazo** (no se cierra el anterior automáticamente).
**Criterios de aceptación:**
- Dado un sustituto activo vigente, cuando se activa/crea otro para un período coincidente, entonces **422** `SUBSTITUTION_ALREADY_ACTIVE`.
**Prioridad:** Alta · **Dependencias:** RF-004.

### RF-006 — Estado efectivo derivado de la vigencia
**Descripción:** Exponer un **estado efectivo** (`PROGRAMADA` / `VIGENTE` / `VENCIDA` / `INACTIVA`) derivado de `StartDate`/`EndDate` vs. fecha actual, además del `IsActive`; permitir consultar "**sustituto vigente hoy**" sin lógica adicional.
**Reglas de negocio:**
- `IsActive=true` **fuera** de `[StartDate, EndDate]` se señala como inconsistencia o se impide (G-07).
**Criterios de aceptación:**
- Dada una sustitución `IsActive=true` con `EndDate` pasada, cuando se consulta el estado efectivo, entonces es **VENCIDA**.
- Dada una fecha dentro de la vigencia de una sustitución activa, cuando se consulta "vigente", entonces se devuelve esa sustitución.
**Prioridad:** Media · **Dependencias:** RF-004.

### RF-007 — Bloqueo por sustituto no disponible (ausente/sustituido)
**Descripción:** **Bloquear** la designación si el empleado elegido como sustituto está él mismo **siendo sustituido** (es titular de otra sustitución **activa** que **solapa** el período) — proxy de "ausente" hasta que exista el módulo de Ausencias (D-06, D-10).
**Reglas de negocio:**
- Si `substitutePersonnelFileId` aparece como **titular** de otra sustitución activa con vigencia solapada, **se bloquea**.
- La conciliación con **ausencias reales** (vacaciones/incapacidad aprobadas) se **difiere** al módulo de Ausencias (D-10).
- *(No se bloquea que el sustituto cubra a varios titulares a la vez; eso está permitido.)*
**Criterios de aceptación:**
- Dado un sustituto que es titular de una sustitución activa solapada, cuando se guarda, entonces **422** `SUBSTITUTE_UNAVAILABLE`.
**Prioridad:** Media · **Dependencias:** RF-004; (futuro) módulo de Ausencias.

### RF-008 — Permiso dedicado de gestión
**Descripción:** Proteger las **escrituras** de sustituciones con un **permiso dedicado** `PersonnelFiles.ManageSubstitutions` (D-09), separado del Manage genérico; lecturas con `PersonnelFiles.Read`. Solo **RRHH**; **sin** autoservicio.
**Reglas de negocio:**
- Crear/editar/activar/eliminar requiere `PersonnelFiles.ManageSubstitutions` (sigue el patrón de `PersonnelFiles.AuthorizeRehire` / `PersonnelFiles.ViewCompensation`).
- Sin el permiso → **403**.
**Criterios de aceptación:**
- Dado un usuario sin `ManageSubstitutions`, cuando intenta crear/editar, entonces **403 FORBIDDEN**.
- Dado un usuario con `Read` pero sin `ManageSubstitutions`, cuando consulta, entonces **200** (lectura permitida).
**Prioridad:** Alta · **Dependencias:** Provisioning/`IdentityAccess` (alta del permiso + seed).

### RF-009 — Auditoría con diff e historial visible
**Descripción:** Registrar cada alta/cambio/baja con **valores antes/después (diff)** y **exponer un historial visible** al usuario (RRHH/auditor) (D-12). La auditoría base ya existe (`IAuditService`); se completa el *diff* y la **vista** de historial.
**Reglas de negocio:**
- Cada operación de escritura genera una entrada con **estado anterior y nuevo**.
- El historial es **consultable** desde la UI por usuarios autorizados.
**Criterios de aceptación:**
- Dada una edición, cuando se consulta el historial, entonces se ve **qué cambió** (campo, valor anterior, valor nuevo, quién y cuándo).
**Prioridad:** Media · **Dependencias:** `IAuditService`; endpoint/vista de historial.

### RF-010 — (Diferido) Punto de extensión para delegación efectiva
**Descripción:** Cuando exista el módulo de **Aprobaciones**, este **consultará la sustitución vigente** del titular para **enrutar/permitir** autorizaciones en su ausencia; y se **conciliará** con el módulo de **Ausencias** (D-01, D-10). Esta fase **solo** deja la entidad como fuente de verdad y define el contrato de consulta.
**Reglas de negocio:** La sustitución es **fuente de verdad**; los módulos futuros **no** duplican la designación.
**Criterios de aceptación:**
- Dado el contrato `GetActiveSubstituteFor(titular, fecha)`, cuando el módulo de aprobaciones lo invoca, entonces obtiene el sustituto vigente (o vacío).
**Prioridad:** Alta conceptual / **Diferido** · **Dependencias:** futuros módulos de Aprobaciones y Ausencias.

---

## 7. Requerimientos no funcionales

- **Seguridad / Multi-tenant.** Toda operación filtrada por **tenant**; la referencia al sustituto no debe permitir fuga cross-tenant (RF-001). Escritura tras **permiso dedicado** (RF-008).
- **Integridad / Consistencia.** Reglas de vigencia, único-activo y disponibilidad en un **módulo de reglas puro** `AuthorizationSubstitutions.Rules.cs`, **unit-testeable** (patrón de `EmploymentAssignments.Rules.cs`) — G-09.
- **Concurrencia.** `ConcurrencyToken` + `If-Match`/`ETag` en escrituras (ya implementado).
- **Auditoría.** Diff antes/después + historial visible (D-12, RF-009).
- **Rendimiento.** Consulta "sustituto vigente" **indexada** por (titular, vigencia) para el consumidor futuro (RF-010).
- **Usabilidad.** Selector de empleado con **búsqueda** (no GUID); sugerencia de **plaza** del sustituto (RF-003); visualización del **estado efectivo** (RF-006).
- **Compatibilidad / API.** Mantener los 6 endpoints; los cambios son retro-compatibles salvo el endurecimiento de validación (breaking solo para datos inválidos, aceptable).
- **Localización.** Mensajes/errores **bilingües (ES/EN)** con código estable.
- **Mantenibilidad.** Tests de reglas y de **paridad de localización** (convención de la casa).

---

## 8. Historias de usuario

### HU-001 — Designar un sustituto para una ausencia
Como **Analista de RRHH**, quiero **designar a un empleado como sustituto de otro durante un período**, para **garantizar la continuidad operativa durante su ausencia**.
- Dado un titular **empleado completado**, cuando registro un sustituto válido con tipo, plaza, **fecha inicio y fin**, entonces se guarda y queda auditado.
- Dado un sustituto **inexistente/inactivo/de otro tenant/candidato**, entonces **bloquea** (RF-001).
- Dado el **propio titular** como sustituto, entonces **bloquea** (auto-sustitución).

### HU-002 — Estandarizar el motivo de la sustitución
Como **Analista de RRHH**, quiero **elegir el tipo de sustitución de una lista**, para **mantener datos consistentes y reportables**.
- Dado el catálogo `substitution-types`, cuando selecciono "VACACIONES", entonces se acepta.
- Dado un texto fuera de catálogo, entonces **bloquea** (RF-002).

### HU-003 — Registrar el puesto del sustituto desde su plaza
Como **Analista de RRHH**, quiero que **el puesto se tome de la plaza del sustituto**, para **evitar texto libre y reflejar su cargo real**.
- Dado un sustituto con una plaza principal activa, cuando lo selecciono, entonces el puesto se **sugiere** automáticamente (RF-003).
- Dada una plaza que no pertenece al sustituto, entonces **bloquea** (RF-003).

### HU-004 — Evitar designaciones contradictorias
Como **Analista de RRHH**, quiero que **no existan dos sustitutos vigentes a la vez** ni un **sustituto no disponible**, para **saber con certeza quién cubre al titular**.
- Dadas dos sustituciones del titular con vigencias **solapadas**, cuando guardo la segunda, entonces **bloquea** (RF-004/005).
- Dado un sustituto que está **siendo sustituido** (ausente) en el período, entonces **bloquea** (RF-007).

### HU-005 — Saber quién es el sustituto vigente hoy
Como **RRHH / Sistema consumidor**, quiero **consultar quién sustituye a un empleado en una fecha dada**, para **dirigir responsabilidades al responsable correcto**.
- Dada una fecha dentro de la vigencia de una sustitución activa, cuando consulto, entonces obtengo al sustituto; fuera de vigencia, **vacío** (RF-006, RF-010).

### HU-006 — Auditar los cambios de designación
Como **Auditor / RRHH**, quiero **ver el historial de cambios de cada sustitución**, para **cumplimiento y trazabilidad**.
- Dada una edición, cuando abro el historial, entonces veo **valor anterior y nuevo**, autor y fecha (RF-009).

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** (`IsCompletedEmployee`) pueden tener sustituciones (ya implementado).
- **RN-02.** El sustituto **no puede** ser el propio titular (ya implementado).
- **RN-03.** El sustituto **debe existir**, ser del **mismo tenant**, tipo **Empleado** (no candidato), **completado** y **activo** (RF-001).
- **RN-04.** `SubstitutionTypeCode` **debe** pertenecer al catálogo `substitution-types` activo (D-08, RF-002).
- **RN-05.** El **puesto** es una **plaza activa del sustituto** (default: principal); **no** del titular (D-02, RF-003).
- **RN-06.** `StartDate ≤ EndDate` y **`EndDate` obligatoria** (D-03, RF-004).
- **RN-07.** **Ámbito = empleado completo** (D-04): las reglas de solape/único-activo aplican por **titular** (no por plaza).
- **RN-08.** **No** se permiten sustituciones **solapadas** del mismo titular; el segundo activo/solapado se **bloquea** (D-07, RF-004/005).
- **RN-09.** **Bloquear** si el sustituto está **siendo sustituido** (ausente) en un período solapado (D-06, RF-007).
- **RN-10.** El **estado efectivo** se deriva de la vigencia; `IsActive=true` fuera de `[StartDate, EndDate]` es inconsistente (RF-006).
- **RN-11.** La sustitución es **documental**; **no** otorga permisos técnicos (RBAC) ni aprueba nada por sí sola hasta que exista el módulo de Aprobaciones (D-01).
- **RN-12.** Gestión **solo RRHH** con permiso `PersonnelFiles.ManageSubstitutions`; **sin** autoservicio (D-09).
- **RN-13.** Toda escritura exige **If-Match** y queda **auditada con diff**; el historial es **visible** (D-12).

---

## 10. Flujos principales

**Flujo: Crear sustitución (RRHH)**
1. RRHH (con `ManageSubstitutions`) abre el expediente del **titular** (debe estar **completado**).
2. Entra a **"Sustitución para Autorizaciones"** → **Agregar**.
3. **Busca y selecciona** al **sustituto** → el sistema **valida** existencia/tenant/estado/tipo (RF-001).
4. El sistema **sugiere la plaza principal activa** del sustituto como **puesto** (editable entre sus plazas activas) (RF-003).
5. Selecciona el **tipo** del catálogo (RF-002) e ingresa **fecha inicio** y **fecha fin (obligatoria)** (RF-004).
6. El sistema valida **fechas**, **no solape/único activo** del titular (RF-004/005) y **disponibilidad** del sustituto (RF-007).
7. Guarda → persiste, **audita con diff**, devuelve `ETag`. Muestra **estado efectivo** (PROGRAMADA/VIGENTE) (RF-006/009).

**Flujo: Consultar sustituto vigente (consumidor/RRHH)**
1. Se consulta "¿quién sustituye a **X** el **dd/mm/aaaa**?" (RF-010).
2. El sistema devuelve la sustitución **activa y vigente** del titular (o vacío).

**Flujo: Ver historial (Auditor/RRHH)**
1. Abre el historial de una sustitución → ve **diff antes/después**, autor y fecha (RF-009).

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Titular **no completado**. | **Bloqueo** `STATE_RULE_VIOLATION` (ya implementado — `…Handlers.cs:46`). |
| **E2** | Sustituto = titular (auto-sustitución). | **Bloqueo** `422` self-substitution (ya implementado — `…Handlers.cs:51`). |
| **E3** | `substitutePersonnelFileId` inexistente / otro tenant. | **Bloqueo** `422 SUBSTITUTE_NOT_FOUND` / `SUBSTITUTE_INVALID_TENANT` (RF-001). |
| **E4** | Sustituto **candidato/inactivo/baja**. | **Bloqueo** `422 SUBSTITUTE_NOT_ELIGIBLE` (RF-001). |
| **E5** | `SubstitutionTypeCode` fuera de catálogo. | **Bloqueo** `422 SUBSTITUTION_TYPE_CODE_INVALID` (RF-002). |
| **E6** | Plaza de puesto que **no** pertenece al sustituto. | **Bloqueo** `422 SUBSTITUTE_POSITION_NOT_OWNED` (RF-003). |
| **E7** | **Fecha fin ausente**. | **Bloqueo** `422 SUBSTITUTION_END_DATE_REQUIRED` (RF-004). |
| **E8** | `EndDate < StartDate`. | **Bloqueo** `422` (ya implementado). |
| **E9** | Vigencias **solapadas** del mismo titular / segundo activo. | **Bloqueo** `422 SUBSTITUTION_PERIOD_OVERLAP` / `SUBSTITUTION_ALREADY_ACTIVE` (RF-004/005). |
| **E10** | Sustituto **siendo sustituido** (ausente) en el período. | **Bloqueo** `422 SUBSTITUTE_UNAVAILABLE` (RF-007). |
| **E11** | Usuario **sin** `ManageSubstitutions` intenta escribir. | **403 FORBIDDEN** (RF-008). |
| **E12** | `If-Match`/`ConcurrencyToken` no coincide. | **409 CONFLICT** (ya implementado — `…Handlers.cs:140,218,325`). |
| **E13** | `IsActive=true` con `EndDate` pasada. | Estado efectivo **VENCIDA**; señalar inconsistencia (RF-006). |

---

## 12. Datos requeridos

### Entidad: `PersonnelFileAuthorizationSubstitution` *(ya existe — `Domain/PersonnelFiles/PersonnelFileEmployee.cs:421`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `personnelFileId` | long (FK) | Sí | del tenant | ✅ existe | **Titular** (empleado sustituido); dueño |
| `substitutePersonnelFileId` | GUID | Sí | **existe + tenant + Empleado + activo + ≠ titular** | 🔧 endurecer (RF-001) | **Sustituto** |
| `substitutionTypeCode` | Texto → **catálogo** | Sí | catálogo `substitution-types` activo (D-08) | 🔧 cambiar a catálogo (RF-002) | Tipo/motivo |
| `substitutePositionSlotPublicId` | GUID (FK `PositionSlot`) | **Sí** | plaza de una **asignación activa del sustituto**; default principal | 🔁 **reemplaza** `substitutePositionTitle` (RF-003) | **Plaza del sustituto** |
| `substitutePositionTitleSnapshot` | Texto | No | — | 🆕 opcional (historial) | *Snapshot* del título a la fecha |
| `startDate` | Fecha | Sí | — | ✅ existe | Inicio de vigencia |
| `endDate` | Fecha | **Sí** | `endDate ≥ startDate`; sin solape (titular) | 🔧 **ahora obligatoria** (RF-004) | Fin de vigencia |
| `isActive` | Booleano | Sí | coherente con vigencia; **único activo por titular** | 🔧 estado efectivo (RF-005/006) | Estado activo |
| `notes` | Texto | No | — | ✅ existe | Observaciones |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

> **Ámbito (D-04):** la sustitución cubre al **empleado completo**; **no** se agrega `assignedPositionPublicId` (no hay ámbito por plaza).

### Entidad: `SubstitutionTypeCatalogItem` *(nueva — RF-002 / D-08)*

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `publicId` | GUID | Sí | único | Identidad |
| `countryCode` | Texto | Sí | catálogo de países | País (patrón `CountryScopedCatalogItem`) |
| `code` | Texto | Sí | único por país | VACACIONES, INCAPACIDAD, PERMISO, MISION_OFICIAL, LICENCIA, OTRO |
| `name` | Texto | Sí | — | Nombre visible (ES/EN) |
| `isActive` | Booleano | Sí | — | Estado |
| `sortOrder` | Número | No | — | Orden de despliegue |

---

## 13. Integraciones necesarias

- **Catálogos generales (`GeneralCatalogs`).** Nuevo `substitution-types` (país-scoped, seed SV — D-08). Interno.
- **Repositorio de expedientes (`IPersonnelFileEmployeeRepository` / `IPersonnelFileRepository`).** Validar sustituto (RF-001) y resolver sus **plazas activas** (RF-003).
- **Plazas (`PositionSlots`) + Asignaciones (`PersonnelFileEmploymentAssignment`).** Para el puesto = plaza del sustituto (RF-003) y la consulta de disponibilidad.
- **Identidad/Provisioning (`IdentityAccess`).** Alta y seed del permiso `PersonnelFiles.ManageSubstitutions` (RF-008).
- **Auditoría (`IAuditService`).** Diff + historial visible (RF-009).
- **Futuro módulo de Aprobaciones / Ausencias** *(diferido)*. Consumidores del contrato RF-010 / vínculo D-10.
- **Notificaciones:** **no aplica** en esta fase (D-11).

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **RRHH / Administrador de Expedientes** | Crear/editar/activar/eliminar y leer sustituciones (**`PersonnelFiles.ManageSubstitutions`** + `Read`). | Solo expedientes **completados**; solo su **tenant**. |
| **Consulta / Auditor** | Leer sustituciones e **historial** (**`PersonnelFiles.Read`**). | Sin escritura. |
| **Empleado titular / sustituto** | — | **Sin** autoservicio en esta fase (D-09). |
| **Sistema (Aprobaciones/Ausencias, diferido)** | Consultar sustituto vigente (RF-010). | Solo lectura del contrato. |

> **Permiso nuevo (D-09):** `PersonnelFiles.ManageSubstitutions`, siguiendo el patrón de `PersonnelFiles.AuthorizeRehire` y `PersonnelFiles.ViewCompensation`. Lectura sigue usando `PersonnelFiles.Read`.

---

## 15. Criterios de aceptación generales

- ✅ No se crea una sustitución con sustituto **inexistente, inactivo, candidato o de otro tenant** (RF-001).
- ✅ El **tipo** proviene del **catálogo** `substitution-types` (RF-002).
- ✅ El **puesto** es una **plaza activa del sustituto** (RF-003).
- ✅ **Fecha fin obligatoria**; no hay indefinidas (RF-004).
- ✅ No coexisten **dos sustituciones vigentes** del mismo titular; el solape/duplicado se **bloquea** (RF-004/005).
- ✅ Se **bloquea** designar a un sustituto **no disponible** (ausente/sustituido) (RF-007).
- ✅ La escritura exige **`ManageSubstitutions`**; la lectura `Read` (RF-008).
- ✅ Toda operación es **concurrencia-segura (If-Match)** y queda **auditada con diff visible** (RF-009).
- ✅ Las reglas viven en un **módulo puro** con **tests unitarios** (G-09).
- ✅ Mensajes/errores **bilingües (ES/EN)** con código estable.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1.** **Brecha de expectativa (G-01):** "para autorizaciones" implica delegación que **aún no ocurre** (D-01). Mitigación: comunicar el carácter **documental** y considerar el rename (ver Recomendaciones).
- **R2.** **Datos sucios existentes:** registros con tipo de texto libre, sin fecha fin, o referencias inválidas; el endurecimiento (RF-001/002/003/004) es *breaking* para esos datos → requiere limpieza/migración.
- **R3.** **Selección de plaza del sustituto:** si el sustituto tiene **varias plazas**, RRHH debe elegir; definir UX para no confundir (default principal mitiga).

### Supuestos
- **S1.** Verificar si hay datos productivos/QA de sustituciones; si no los hay, aplica **drop & recreate**/normalización directa (como en otros módulos); si los hay, **migración** previa.
- **S2.** El sustituto, por ser **empleado activo completado**, tiene **≥1 plaza activa** (principal) — habilita el default de RF-003.
- **S3.** País de referencia **SV**; catálogo sembrado para SV.
- **S4.** La sustitución es **documental** (D-01): no traspasa RBAC ni aprueba.

### Dependencias
- **D1.** `GeneralCatalogs` (catálogo `substitution-types`).
- **D2.** `IPersonnelFileEmployeeRepository` / `IPersonnelFileRepository`, `PositionSlots`, `PersonnelFileEmploymentAssignment`.
- **D3.** `IdentityAccess`/Provisioning (permiso `ManageSubstitutions`).
- **D4.** `IAuditService` + vista de historial.
- **D5.** (Diferido) Módulos de **Aprobaciones** y **Ausencias** (RF-010, D-10).

---

## 17. Decisiones resueltas (cierre de preguntas abiertas)

| Pregunta | Decisión | Ref. |
|---|---|---|
| P-01 ¿Delega autoridad o es documental? | **Documental/informativa**; delegación diferida. | D-01, RF-010 |
| P-02 ¿"Puesto" = sustituto o titular? ¿texto o plaza? | **Referencia a la plaza del sustituto** (default principal). | D-02, RF-003 |
| P-03 ¿Fecha fin obligatoria? | **Sí**; sin indefinidas. | D-03, RF-004 |
| P-04 ¿Ámbito? | **Empleado completo.** | D-04 |
| P-05 ¿Aprueba el jefe? ¿Jerarquía? | **No** aprueba; **sin** relación jerárquica requerida. | D-05 |
| P-06 ¿Sustituto ausente: advertir o bloquear? | **Bloquear.** | D-06, RF-007 |
| P-07 ¿Traslape: bloquear o supersesión? | **Bloquear.** | D-07, RF-004/005 |
| P-08 ¿Catálogo de tipos? | VACACIONES, INCAPACIDAD, PERMISO, MISION_OFICIAL, LICENCIA, OTRO. | D-08, RF-002 |
| P-09 ¿Quién gestiona? ¿Permiso? | **Solo RRHH** con permiso dedicado `ManageSubstitutions`. | D-09, RF-008 |
| P-10 ¿Vínculo con ausencia real? | **Diferido** al módulo de Ausencias. | D-10 |
| P-11 ¿Notificaciones? | **No** en esta fase. | D-11 |
| P-12 ¿Auditoría con diff e historial visible? | **Sí.** | D-12, RF-009 |

> **Pendiente menor de diseño (no bloqueante):** nombre exacto del permiso (`PersonnelFiles.ManageSubstitutions`, sujeto a confirmación de convención) y UX de selección de plaza cuando el sustituto tiene varias.

---

## 18. Recomendaciones del Analista de Negocio

1. **Reposicionar como "validación + endurecimiento", no construcción nueva:** la entidad, CRUD, API, auditoría y concurrencia **ya existen**. El esfuerzo es de **reglas y validaciones** (RF-001…RF-009) + un **catálogo** + un **permiso** + el **historial visible**.

2. **Fase 1 (MVP documental) — todo lo ratificado:** RF-001 (validar sustituto), RF-002 (catálogo), RF-003 (puesto = plaza del sustituto), RF-004 (fecha fin obligatoria + no solape), RF-005 (único activo, bloqueo), RF-006 (estado efectivo), RF-007 (bloqueo por no disponible), RF-008 (permiso dedicado), RF-009 (auditoría con diff + historial). Extraer un **módulo de reglas puro** `AuthorizationSubstitutions.Rules.cs` reusando `RangesOverlap` (`Employment/EmploymentAssignments.Rules.cs:96`).

3. **Datos y migración:** verificar datos existentes (S1). Sin datos → **drop & recreate**/normalización directa; con datos → **migrar** (tipos→catálogo, completar fecha fin, convertir título→plaza, depurar referencias) antes de activar validaciones duras. El cambio `substitutePositionTitle → substitutePositionSlotPublicId` requiere migración explícita.

4. **Fase 2/3 (diferidas):** **RF-010** — al existir **Aprobaciones**, consumir la sustitución **vigente** para enrutar autorizaciones; al existir **Ausencias**, **conciliar** el período (D-10). Mantener la entidad estable como **fuente de verdad**.

5. **Naming/UX (R1):** considerar renombrar la sección a algo que refleje su alcance real de Fase 1 (p. ej. "**Sustituto designado en ausencia**") y reservar "para autorizaciones" para cuando RF-010 esté implementado, evitando la brecha de expectativa.

---

> **Naturaleza del documento.** Análisis de **validación + brechas (GAP)** sobre funcionalidad **ya implementada** (`PersonnelFileAuthorizationSubstitution`). El "estado as-is" está **verificado contra el código** (referencias `archivo:línea`). Las **decisiones del negocio están ratificadas** (D-01…D-12, 2026-06-21); el documento queda **cerrado para Fase 1** y listo para diseño técnico. Fases 2–3 (delegación efectiva y vínculo con Ausencias) quedan **diferidas**.
