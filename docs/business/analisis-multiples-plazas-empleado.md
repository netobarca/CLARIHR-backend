# Análisis de Negocio — Múltiples Plazas por Empleado

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Plazas (`PositionSlots`) |
| **Estado** | Definido — listo para diseño técnico e implementación |
| **Fecha** | 2026-06-20 |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN según el caso) |

---

## Contexto del cambio

Hoy la "plaza" (puesto/`PositionSlot`) de un empleado se almacena en **tres lugares distintos** del modelo, con cardinalidades diferentes y **sin reglas que los mantengan consistentes**. Esto vuelve ambiguo "cuál es la plaza real" de la persona y propaga inconsistencias a otros módulos.

El negocio necesita que **un empleado pueda ocupar varias plazas a la vez** — **una principal** y **varias secundarias** — de forma explícita y **validada contra la capacidad máxima de empleados por plaza** y contra escenarios mal diseñados (plazas duplicadas, dos principales, vigencias solapadas, plazas inexistentes/no asignables).

**Decisión estructural (P-01):** la **colección de asignaciones** (`PersonnelFileEmploymentAssignment`) será la **única fuente de verdad**. Los otros dos lugares se **eliminan** (columnas/datos), no se "sincronizan". No se requiere preservar datos; el frontend se ajustará y solo se le comunicarán los cambios.

### Estado actual verificado en el código (línea base)

| # | Dónde | Campo(s) | Cardinalidad | Acción objetivo |
|---|---|---|---|---|
| 1 | `PersonnelFile` (raíz) | `AssignedPositionSlotPublicId` (`Guid?`) — inmutable tras completar (`PersonnelFile.cs:109`, `:228-231`) | 1 | **ELIMINAR** |
| 2 | `PersonnelFileEmployeeProfile` (perfil laboral) | `PositionSlotPublicId` + `JobProfilePublicId` (`PersonnelFileEmployee.cs:84-86`) | 1 | **ELIMINAR** |
| 3 | `PersonnelFileEmploymentAssignment` (asignaciones) | colección N con `PositionSlotPublicId`, `IsPrimary`, `IsActive`, fechas, tipo (`PersonnelFileEmployee.cs:190-301`) | **N** | **CONSERVAR como fuente única + agregar reglas** |

**Huecos confirmados que esta iniciativa cierra** (hoy el recurso 3 es solo un contenedor de datos sin reglas):

- (a) No se garantiza **una sola principal** — pueden coexistir varias `IsPrimary = true`.
- (b) No hay **deduplicación** — el mismo empleado puede repetir la misma plaza.
- (c) No se previenen **vigencias solapadas** en la misma plaza.
- (d) No se valida la **capacidad** — `positionSlotPublicId` se acepta sin comprobar que exista, sea asignable, ni tenga cupo (`EmploymentAssignments.Handlers.cs:20-86`).
- (e) No hay **coherencia** entre los 3 lugares (la raíz puede contradecir la asignación principal).
- (f) `assignmentTypeCode` es **texto libre** (máx. 80), sin catálogo. Hoy los docs usan `PERMANENTE`/`TEMPORAL`.
- (g) El **PATCH** salta casi toda la validación (solo revalida que `assignmentTypeCode` no quede vacío).
- (h) `PositionSlot.OccupiedEmployees` (cupo) es **manual** (`PATCH /position-slots/{id}/occupancy`, `PositionSlot.cs:207-224`) y **no** se mueve al crear/eliminar asignaciones. No existe `CanAssign()` ni cálculo de "cupos disponibles".

---

## Decisiones del negocio (resueltas) — referencia rápida para el equipo

| # | Pregunta | Decisión |
|---|---|---|
| P-01 | Fuente de verdad | Las **asignaciones** son la fuente única. **Se eliminan** los campos de plaza de la raíz y del perfil (columnas + datos). |
| P-02 | ¿La principal también consume cupo? | **Sí.** Todas las plazas (principal y secundarias) validan y consumen capacidad. |
| P-03 | Cambio de principal | Al marcar otra como principal, la anterior se **degrada automáticamente** a secundaria (atómico). |
| P-04 | Baja de la principal | **No se permite remover/eliminar.** El sistema **advierte** y **lista los empleados** que tienen esa plaza como principal activa; primero deben **cambiar a otra plaza principal**. |
| P-05 | Tipo de asignación | Es **ortogonal** a `isPrimary`. Tiene su **propio catálogo** (con *seed* de ejemplos de El Salvador). `isPrimary` es independiente. |
| P-06 | ¿Cuándo "ocupa cupo"? | Se considera la **vigencia (fechas)** de la asignación a la plaza: una asignación cuenta para el cupo mientras esté **activa y vigente** dentro de su rango de fechas. |
| P-07 | % dedicación / FTE | **Fuera de alcance.** |
| P-08 | Solape entre plazas distintas | **Permitido** (es el objetivo). Solo se prohíbe el solape sobre la **misma** plaza. |
| P-09 | Permisos / workflow | **Sin workflow de aprobación.** Puede asignar quien tenga permiso sobre el módulo/pantalla según la **matriz de permisos** del sistema (`PersonnelFiles.Manage`). |
| P-10 | Datos contradictorios | **Resolución manual** (no se preservan datos; limpieza de esquema). |
| P-11 | Consumidores externos | **No hay.** Solo se comunican los cambios al frontend. |

---

## 1. Resumen del producto o requerimiento

Se **rediseña la gestión de plazas del empleado** en CLARIHR para soportar de forma robusta que **un empleado tenga múltiples plazas simultáneas**: **una principal** (`isPrimary = true`) y **una o varias secundarias**.

El cambio hace dos cosas a la vez:

1. **Elimina la ambigüedad estructural**: consolida todo en la colección de asignaciones como **fuente única de verdad** y **elimina** los campos de plaza de la raíz (`PersonnelFile`) y del perfil laboral (`PersonnelFileEmployeeProfile`).
2. **Incorpora las reglas de negocio ausentes**: exactamente una principal, sin duplicados, sin solapes en la misma plaza, y **validando cada plaza contra la capacidad máxima (`maxEmployees`)** considerando la **vigencia** de cada asignación.

El problema que resuelve: inconsistencias de datos que se propagan a módulos posteriores y la imposibilidad de confiar en "cuál es la plaza de esta persona".

## 2. Objetivos del negocio

1. **Fuente única de verdad**: un solo lugar autoritativo (las asignaciones) para las plazas de un empleado.
2. **Soporte formal de multi-plaza**: N plazas activas por empleado, con distinción **principal/secundaria**.
3. **Integridad por reglas**: una sola principal activa, sin plazas duplicadas, sin vigencias solapadas en la misma plaza.
4. **Respeto de la capacidad**: ninguna asignación puede superar el `maxEmployees` de su plaza, considerando la vigencia.
5. **Eliminar la duplicidad de modelo**: remover los campos de plaza de la raíz y del perfil.
6. **Trazabilidad y auditoría**: saber quién asignó qué plaza, cuándo y con qué vigencia.

> **Nota:** la *migración sin pérdida* **no** es un objetivo. Se pueden eliminar columnas y datos; el frontend se ajustará.

## 3. Alcance funcional

- **F1.** CRUD de **asignaciones de plaza** (crear, listar, ver, editar, activar/desactivar, eliminar) como colección autoritativa. *(Recurso ya existente; se le agregan reglas.)*
- **F2.** Gobierno de **plaza principal** (`isPrimary`): exactamente una principal activa por empleado, con **degradación automática** de la anterior al cambiarla.
- **F3.** **Validación de capacidad** por plaza al crear/activar/editar, considerando la **vigencia** de la asignación (cupo disponible = `maxEmployees` − asignaciones activas vigentes que se solapan).
- **F4.** **Gestión del cupo** (`occupiedEmployees`) acoplada al ciclo de vida de las asignaciones (alta/activación/baja/finalización/eliminación).
- **F5.** **Validaciones de coherencia**: plaza existente, del mismo tenant y **asignable** (no `Suspended`, dentro de su vigencia); sin duplicar plaza activa por empleado; sin solapar vigencias en la **misma** plaza; fechas coherentes.
- **F6.** **Eliminación** de `PersonnelFile.AssignedPositionSlotPublicId` y de `PersonnelFileEmployeeProfile.PositionSlotPublicId/JobProfilePublicId`. Donde se necesite el `JobProfile`, se obtiene de la **plaza** de la asignación principal (la plaza ya referencia obligatoriamente un `JobProfile`).
- **F7.** **Catálogo de tipo de asignación** (`assignmentTypeCode`) controlado y **ortogonal** a `isPrimary`, con *seed* de El Salvador.
- **F8.** **Protección de baja de principal / plaza** (P-04): bloquear con advertencia y listado de empleados afectados.
- **F9.** **Endurecer el endpoint PATCH** para que aplique las mismas reglas que Add/Update.
- **F10.** **Limpieza de esquema**: remover columnas obsoletas; recomputar/inicializar el cupo desde las asignaciones; sin preservación de datos (resolución manual de casos borde).
- **F11.** **Mensajería/errores bilingües** (ES/EN según el caso), siguiendo el catálogo de códigos existente.

## 4. Fuera de alcance (esta fase)

- **Nómina / remuneración multi-plaza** (prorrateo, multi-salario). *No existe módulo de nómina aún.*
- **% de dedicación / jornada / FTE** por plaza (P-07).
- **Workflow / aprobaciones** para autorizar una asignación (P-09).
- **Preservación/migración de datos** de los campos eliminados (P-10): se descartan; el frontend se ajusta.
- **Rediseño del onboarding** del candidato y del organigrama de posiciones (dependencias jerárquicas/funcionales entre plazas).
- **Reportes nuevos** específicos de multi-plaza.
- **Integraciones externas** (no hay consumidores externos — P-11).

## 5. Actores o usuarios involucrados

| Actor | Rol en este requerimiento |
|---|---|
| **Analista / Administrador de RRHH** | Gestiona las plazas del empleado (crea, edita, activa/desactiva, define principal). Requiere permiso `PersonnelFiles.Manage`. Actor principal. |
| **Supervisor / Jefe** | Consulta las plazas de su personal (`PersonnelFiles.Read`). |
| **Empleado (autoservicio)** | Consulta (solo lectura) sus propias plazas. |
| **Administrador de Plazas** | Define las `PositionSlot`, su `maxEmployees`, estado y vigencia (`PositionSlots.Manage`). |
| **Sistema (reglas/capacidad)** | Valida invariantes y mantiene el cupo coherente con las asignaciones. |
| **Auditor** | Revisa la trazabilidad de cambios. |

> La autorización se rige por la **matriz de permisos** existente del sistema, por módulo/pantalla (P-09). No se define un rol nuevo; se reutilizan los permisos `PersonnelFiles.Read/Manage` y `PositionSlots.Read/Manage`.

## 6. Requerimientos funcionales

### RF-001 — Registrar una plaza (asignación) para un empleado

**Descripción:** Asignar una plaza a un expediente **completado**, indicando tipo (catálogo), vigencia, si es principal y si está activa.

**Reglas de negocio:**
- El expediente debe estar **completado** (`IsCompletedEmployee = true`) — comportamiento actual (`EmploymentAssignments.Handlers.cs:46-49`; FE: `422` sobre `Draft`).
- `positionSlotPublicId` pasa a ser **obligatorio** para una asignación de plaza real *(hoy es opcional)*.
- La plaza debe **existir**, ser del mismo tenant y estar **asignable**: no `Suspended` y **dentro de su vigencia** (`effectiveFromUtc/effectiveToUtc`).
- La plaza debe tener **cupo disponible** considerando la **vigencia** de la nueva asignación (RF-005).
- No se permite una **segunda asignación activa a la misma plaza** para el mismo empleado (RF-007).
- `startDate` obligatoria; `endDate` opcional y `≥ startDate` (validador + *check constraint* actuales).
- `assignmentTypeCode` ∈ **catálogo** (RF-008).
- Si es la **primera** plaza activa del empleado, se toma como **principal** por defecto.

**Criterios de aceptación:**
- Dado un empleado completado y una plaza vigente con cupo, cuando registro una asignación válida, entonces queda creada y aparece en el listado.
- Dado que la plaza no tiene cupo en el período de vigencia, cuando intento asignarla, entonces se rechaza (`422 EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED`).
- Dado que el empleado ya tiene una asignación activa a esa misma plaza, entonces se rechaza (`409 EMPLOYMENT_ASSIGNMENT_DUPLICATE_POSITION_SLOT`).
- Dado un expediente en `Draft`, entonces se rechaza (`422`).

**Prioridad:** Alta · **Dependencias:** RF-005, RF-007, RF-008.

---

### RF-002 — Definir y gobernar la plaza principal

**Descripción:** Cada empleado con ≥1 plaza activa debe tener **exactamente una** principal (`isPrimary = true`, activa).

**Reglas de negocio:**
- A lo sumo **una** asignación `isPrimary = true` **activa** por empleado.
- Al marcar una nueva como principal, la anterior se **degrada automáticamente** a secundaria, en la **misma transacción** (P-03).
- Si el empleado tiene ≥1 plaza activa, **siempre** debe existir una principal (no puede quedar sin principal — ver RF-008/P-04 para la baja).

**Criterios de aceptación:**
- Dado un empleado con principal A, cuando marco B como principal, entonces A pasa a secundaria y B queda principal (una sola principal).
- Dado un empleado sin plazas, cuando registro la primera, entonces queda principal automáticamente.
- En ningún estado consistente existen dos principales activas.

**Prioridad:** Alta · **Dependencias:** RF-001.

---

### RF-003 — Editar una asignación de plaza

**Descripción:** Modificar campos de una asignación (tipo, plaza, centros, vigencia, notas, principal) respetando todas las invariantes.

**Reglas de negocio:**
- Toda edición re-evalúa: capacidad (si cambia de plaza o de vigencia), unicidad de principal, deduplicación, solape en la misma plaza, coherencia de fechas, catálogo de tipo.
- Cambiar la **plaza** o la **vigencia** equivale a recalcular cupo de la plaza anterior y de la nueva (RF-005).
- **Concurrencia optimista** vía `If-Match`/`concurrencyToken` (ya existente).
- **PUT** conserva `isActive` (la activación se hace por PATCH) — comportamiento actual a preservar.

**Criterios de aceptación:**
- Dado que cambio a una plaza sin cupo en el período, entonces se rechaza y no se altera ningún cupo.
- Dado un `If-Match` desactualizado, entonces `409 CONCURRENCY_CONFLICT`.

**Prioridad:** Alta · **Dependencias:** RF-001, RF-005.

---

### RF-004 — Activar / desactivar (finalizar) una plaza

**Descripción:** Activar o desactivar una asignación (incluye fin de vigencia), reflejándolo en el cupo.

**Reglas de negocio:**
- **Desactivar/finalizar** libera cupo de la plaza (RF-005).
- **Activar** vuelve a consumir cupo y re-valida capacidad y deduplicación.
- No se puede **desactivar la principal** dejando al empleado con plazas activas pero sin principal (ver RF-008).
- El **PATCH** (único medio para mutar `isActive`) debe aplicar estas reglas (RF-009).

**Criterios de aceptación:**
- Dado que desactivo una asignación vigente, entonces el cupo de su plaza se libera.
- Dado que activo una asignación a una plaza llena en el período, entonces se rechaza por capacidad.

**Prioridad:** Alta · **Dependencias:** RF-005, RF-008.

---

### RF-005 — Validar y mantener la capacidad de la plaza (por vigencia)

**Descripción:** Impedir que una plaza supere su `maxEmployees` **en cualquier punto de su vigencia** y mantener el cupo coherente con las asignaciones activas vigentes.

**Reglas de negocio:**
- Una asignación **cuenta para el cupo** mientras esté **activa y vigente** dentro de su rango `[startDate, endDate]` (P-06).
- **Capacidad por solape**: al crear/activar/editar una asignación con ventana `[s, e]` a la plaza X, el sistema cuenta las asignaciones **activas** a X cuyas ventanas **se solapan** con `[s, e]`; si en algún punto del solape el conteo alcanzaría `maxEmployees`, **rechaza** (`422 EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED`).
- La principal **también** consume cupo, igual que una secundaria (P-02).
- La mutación de la asignación y del cupo es **atómica** (misma transacción) y **segura ante concurrencia** (dos altas simultáneas no deben exceder el máximo).
- Las plazas **suspendidas** no admiten cambios de ocupación (regla existente: `POSITION_SLOT_SUSPENDED_OCCUPANCY_CONFLICT`).
- Se expone un valor derivado de **cupo disponible** por período.

**Criterios de aceptación:**
- Dado `maxEmployees = 2` y dos asignaciones vigentes que se solapan, cuando intento una tercera solapada, entonces se rechaza.
- Dadas dos solicitudes concurrentes por el último cupo del mismo período, entonces solo una tiene éxito.
- Dado que finalizo una asignación vigente, entonces ese cupo queda disponible.
- Dadas dos asignaciones a la misma plaza cuyas vigencias **no** se solapan, entonces ambas pueden coexistir si `maxEmployees = 1`.

**Prioridad:** Alta · **Dependencias:** modelo `PositionSlot` (`maxEmployees`, `occupiedEmployees`, estado, vigencia); estrategia de concurrencia (bloqueo de fila / constraint).

> **Nota de diseño (para el equipo):** como el cupo depende de la **vigencia**, `occupiedEmployees` deja de ser un contador manual fiable y debe **derivarse** de las asignaciones activas vigentes (cálculo por solape), o recalcularse en cada transición de vigencia. El `PATCH /position-slots/{id}/occupancy` manual puede mantenerse como override administrativo, pero el flujo de asignaciones debe ser la vía normal.

---

### RF-006 — Eliminar los campos de plaza redundantes (fuente única)

**Descripción:** Remover del modelo los campos de plaza de la raíz y del perfil laboral, dejando la colección de asignaciones como única fuente.

**Reglas de negocio:**
- Se **eliminan**: `PersonnelFile.AssignedPositionSlotPublicId`, `PersonnelFileEmployeeProfile.PositionSlotPublicId`, `PersonnelFileEmployeeProfile.JobProfilePublicId` (columnas + datos).
- Donde otros módulos necesiten "la plaza/el perfil de puesto del empleado", se obtiene de la **asignación principal activa** (y de su `PositionSlot → JobProfile`).
- Desaparece la restricción de **inmutabilidad** tras completar (`PersonnelFile.cs:228-231`) por dejar de existir el campo.

**Criterios de aceptación:**
- Tras el cambio, no existe ninguna ruta de escritura ni lectura a los campos eliminados.
- La "plaza principal" de un empleado se resuelve siempre desde la asignación principal activa.

**Prioridad:** Alta · **Dependencias:** RF-002; revisión de lecturas internas que hoy usan esos campos.

---

### RF-007 — Prevenir duplicados y solapes (escenarios mal diseñados)

**Descripción:** Impedir asignaciones duplicadas o con vigencias solapadas que generen estados ambiguos.

**Reglas de negocio:**
- Un empleado **no** puede tener **dos asignaciones activas a la misma plaza**.
- En la **misma** plaza, dos asignaciones del mismo empleado **no pueden solapar** sus rangos `[startDate, endDate]`.
- El solape **entre plazas distintas** **sí** está permitido (P-08) — es el objetivo del multi-plaza.

**Criterios de aceptación:**
- Dado un empleado con asignación activa a la plaza X, cuando intento otra a X solapada, entonces se rechaza (`409 EMPLOYMENT_ASSIGNMENT_DUPLICATE_POSITION_SLOT` / `..._OVERLAPPING_DATES`).
- Dadas dos plazas distintas vigentes el mismo período, entonces ambas se permiten.

**Prioridad:** Media-Alta · **Dependencias:** RF-001.

---

### RF-008 — Protección de baja de la plaza principal (P-04)

**Descripción:** No permitir remover/eliminar una plaza que sea **principal activa** de empleados; advertir y listar los afectados.

**Reglas de negocio:**
- **A nivel de empleado:** no se permite **desactivar/eliminar** la asignación **principal** si el empleado tiene otras plazas activas, **sin antes** designar otra principal (no hay auto-promoción). El sistema **bloquea** y **advierte**.
- **A nivel de plaza (`PositionSlot`):** no se permite **eliminar** una plaza mientras sea la **principal activa** de uno o más empleados. El sistema **advierte** y **devuelve el listado de empleados** que la tienen como principal activa; esos empleados deben **cambiar a otra plaza principal** antes.

**Criterios de aceptación:**
- Dado un empleado cuya única principal intento eliminar teniendo otras activas, entonces se bloquea con mensaje claro (`422 EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED`).
- Dada una plaza que es principal activa de N empleados, cuando intento eliminarla, entonces se bloquea (`409 POSITION_SLOT_IN_USE_AS_PRIMARY`) y la respuesta incluye los empleados afectados.

**Prioridad:** Media-Alta · **Dependencias:** RF-002; punto de integración con la eliminación de `PositionSlot`.

---

### RF-009 — Endurecer el endpoint PATCH

**Descripción:** El `PATCH` (JSON Patch) debe aplicar **todas** las reglas tras aplicar las operaciones, no solo "tipo no vacío".

**Reglas de negocio:**
- Tras el patch, revalidar: catálogo de tipo, fechas, capacidad (si cambió plaza/estado/vigencia), unicidad de principal (con degradación automática si corresponde), deduplicación y solapes.
- Mantener los límites de *hardening* de JSON Patch existentes (máx. operaciones).

**Criterios de aceptación:**
- Un patch que dejaría dos principales se resuelve por degradación automática o se rechaza, nunca produce dos principales.
- Un patch que activa una asignación a plaza llena se rechaza.

**Prioridad:** Media-Alta · **Dependencias:** RF-002, RF-005, RF-007, RF-008.

---

### RF-010 — Catálogo de tipo de asignación (ortogonal)

**Descripción:** Reemplazar `assignmentTypeCode` de texto libre por un **catálogo controlado**, **independiente** de `isPrimary`.

**Reglas de negocio:**
- Solo se aceptan códigos del **catálogo vigente** (`422/400 EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID`).
- `assignmentTypeCode` describe la **naturaleza/modalidad** de la asignación; **no** determina si es principal. `isPrimary` es independiente (P-05).
- Se entrega un *seed* inicial con ejemplos de **El Salvador** (ver §12), con nombre ES/EN.

**Criterios de aceptación:**
- Dado un código fuera del catálogo, entonces se rechaza.
- Una asignación `PERMANENTE` puede ser principal o secundaria indistintamente.

**Prioridad:** Media · **Dependencias:** definición final del catálogo con el negocio.

---

### RF-011 — Limpieza de esquema (sin preservación de datos)

**Descripción:** Migrar el esquema al modelo consolidado eliminando columnas obsoletas e inicializando el cupo desde las asignaciones.

**Reglas de negocio:**
- Eliminar columnas obsoletas (RF-006). No se preservan datos (P-01/P-10).
- Inicializar/recalcular `occupiedEmployees` de cada plaza a partir de las asignaciones activas vigentes.
- Los casos borde (p. ej. múltiples principales preexistentes, cupos ya excedidos) se resuelven **manualmente** (P-10).

**Criterios de aceptación:**
- Tras la limpieza, no quedan columnas obsoletas y el cupo refleja las asignaciones activas vigentes.

**Prioridad:** Alta · **Dependencias:** todas las anteriores; ventana de despliegue.

## 7. Requerimientos no funcionales

- **Seguridad / Autorización:** respeta la matriz de permisos existente — `PersonnelFiles.Read` (lectura) y `PersonnelFiles.Manage` (escritura) para asignaciones; `PositionSlots.Read/Manage` para plazas. Aislamiento **multi-tenant** estricto (`TenantId` en toda consulta). Cross-tenant → `404/403`.
- **Integridad / Consistencia:** invariantes garantizadas **transaccionalmente** y reforzadas con **restricciones de BD** donde aplique (índices únicos parciales).
- **Concurrencia:** control optimista por `If-Match`/`concurrencyToken` (existente) + estrategia para el cupo (bloqueo de fila de la plaza o constraint que impida exceder capacidad en el período). Sin carreras que superen `maxEmployees`.
- **Rendimiento:** validaciones de capacidad/unicidad/solape eficientes, apoyadas en índices (hoy existen `…__tenant_file_active_primary` y `…__tenant_file_start`); evitar N+1 al validar.
- **Auditoría:** registrar alta, cambio de principal y baja (quién/cuándo/qué). Conservar histórico (las finalizadas no se borran salvo política).
- **Mantenibilidad:** consolidar la lógica de plaza en el agregado/servicio de dominio; eliminar rutas de escritura redundantes.
- **Compatibilidad:** los endpoints `/api/v1/personnel-files/{id}/employment-assignments` se mantienen; los cambios de contrato (campos eliminados, tipo catalogado, nuevos errores) se **comunican al frontend** (no hay consumidores externos — P-11).
- **Localización:** mensajes y catálogos **bilingües ES/EN** según el caso, coherentes con el catálogo de errores existente.
- **Observabilidad:** métricas/logs de rechazos por capacidad, duplicados y bloqueos de baja de principal.

## 8. Historias de usuario

### HU-001 — Asignar una segunda plaza
Como **Analista de RRHH**, quiero **asignar una plaza secundaria a un empleado que ya tiene principal**, para **reflejar que desempeña dos funciones** (p. ej. programador y ordenanza).
- Dado un empleado con principal y una plaza secundaria vigente con cupo, cuando registro la segunda, entonces queda activa como secundaria y la principal no cambia.
- Dado que la secundaria no tiene cupo en el período, entonces recibo error de capacidad y no se crea.

### HU-002 — Cambiar la plaza principal
Como **Analista de RRHH**, quiero **promover una secundaria a principal**, para **reflejar el cambio de responsabilidad primaria**.
- Dado un empleado con principal A y secundaria B, cuando marco B como principal, entonces A pasa a secundaria automáticamente y queda una sola principal.

### HU-003 — Finalizar una plaza
Como **Analista de RRHH**, quiero **finalizar (desactivar) una plaza secundaria con su fecha de fin**, para **liberar el cupo y dejar histórico**.
- Cuando finalizo, entonces el cupo de esa plaza se libera para el período correspondiente.

### HU-004 — Ver todas las plazas de un empleado
Como **Supervisor**, quiero **ver todas las plazas (principal y secundarias) de mi personal**, para **entender su carga y funciones**.

### HU-005 — Evitar errores de doble asignación
Como **Analista de RRHH**, quiero **que el sistema impida duplicar una plaza o crear dos principales**, para **no generar inconsistencias**.

### HU-006 — No perder la plaza principal por error
Como **Analista de RRHH**, quiero **que el sistema me impida eliminar la plaza principal sin antes designar otra** (y me advierta qué empleados dependen de una plaza), para **mantener siempre una principal válida**.

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** pueden tener asignaciones de plaza.
- **RN-02.** Un empleado puede tener **N plazas activas** simultáneas.
- **RN-03.** Exactamente **una** plaza **principal activa** por empleado (si tiene ≥1 activa).
- **RN-04.** Cambiar la principal **degrada automáticamente** la anterior (atómico).
- **RN-05.** **No** se permite más de una asignación **activa a la misma plaza** por empleado.
- **RN-06.** En la **misma** plaza, las asignaciones de un empleado **no pueden solapar** vigencias. El solape **entre plazas distintas** sí se permite.
- **RN-07.** Toda asignación referencia una plaza **existente, del mismo tenant, asignable** (no `Suspended`, dentro de su vigencia).
- **RN-08.** Ninguna plaza puede superar su **`maxEmployees`** en ningún punto de la vigencia; la principal **también** consume cupo.
- **RN-09.** Una asignación **cuenta para el cupo** mientras esté **activa y vigente** (dentro de `[startDate, endDate]`).
- **RN-10.** `startDate` obligatoria; `endDate` opcional y **≥ `startDate`**.
- **RN-11.** `assignmentTypeCode` ∈ **catálogo**; es **ortogonal** a `isPrimary`.
- **RN-12.** Los campos de plaza de la raíz y del perfil **se eliminan**; la plaza se resuelve desde la asignación principal activa.
- **RN-13.** **No** se puede eliminar/desactivar la **principal** sin designar otra; **no** se puede eliminar una **plaza** que sea principal activa de empleados (se advierte y se listan los afectados).
- **RN-14.** Mutaciones de cupo y asignación **atómicas** y resistentes a concurrencia.
- **RN-15.** Las plazas **suspendidas** no admiten cambios de ocupación.
- **RN-16.** Las asignaciones finalizadas se **conservan** (histórico/auditoría).

## 10. Flujos principales

### Flujo A — Registrar una plaza secundaria
1. El Analista abre el expediente (completado) del empleado.
2. Indica plaza, tipo (catálogo), vigencia, y si es principal.
3. El sistema valida: completado, plaza existente/asignable/vigente, **cupo disponible en el período**, no duplicada, fechas coherentes, tipo en catálogo.
4. El sistema crea la asignación y **ajusta el cupo** de la plaza (atómico). Si se marcó principal, **degrada** la principal anterior.
5. Confirma.

### Flujo B — Cambiar la plaza principal
1. El Analista marca una secundaria como principal.
2. El sistema **degrada** la anterior a secundaria y promueve la nueva (atómico).
3. Confirma. (La "plaza del empleado" se resuelve ahora desde esta principal.)

### Flujo C — Finalizar una plaza
1. El Analista finaliza/desactiva una asignación (fecha fin).
2. El sistema **libera el cupo** para el período.
3. Si era la principal y quedan otras activas, **bloquea** y exige designar nueva principal (Flujo B).
4. Confirma; la asignación pasa a histórico.

### Flujo D — Limpieza de esquema (una vez)
1. Eliminar columnas obsoletas de raíz y perfil.
2. Recalcular `occupiedEmployees` por plaza desde asignaciones activas vigentes.
3. Resolver manualmente los casos borde detectados.

## 11. Flujos alternativos y excepciones

| Código | Escenario | Resultado |
|---|---|---|
| E1 | Capacidad agotada en el período de vigencia | `422 EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED` |
| E2 | Plaza inexistente / otro tenant | `404 EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_FOUND` |
| E3 | Plaza `Suspended` o fuera de su vigencia | `422 EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE` |
| E4 | Duplicado de plaza activa para el empleado | `409 EMPLOYMENT_ASSIGNMENT_DUPLICATE_POSITION_SLOT` |
| E5 | Solape de vigencias en la **misma** plaza | `409 EMPLOYMENT_ASSIGNMENT_OVERLAPPING_DATES` |
| E6 | Eliminar/desactivar la principal sin designar otra | `422 EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED` |
| E7 | Eliminar una plaza que es principal activa de empleados | `409 POSITION_SLOT_IN_USE_AS_PRIMARY` (+ listado de afectados) |
| E8 | Conflicto de concurrencia (`If-Match` stale o carrera por cupo) | `409 CONCURRENCY_CONFLICT` |
| E9 | Expediente en `Draft` | `422` (regla de estado actual) |
| E10 | Fechas incoherentes (`endDate < startDate`) | `422` (validador + check constraint) |
| E11 | Tipo fuera de catálogo | `422/400 EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID` |
| E12 | PATCH que violaría invariantes | rechazo según la regla aplicable (RF-009) |

> Códigos `EMPLOYMENT_ASSIGNMENT_*` y `POSITION_SLOT_IN_USE_AS_PRIMARY` son **propuestos**, siguiendo la convención del catálogo existente (`POSITION_SLOT_CAPACITY_RULE_VIOLATION`, etc.). A confirmar en diseño técnico.

## 12. Datos requeridos

### Entidad: PersonnelFileEmploymentAssignment (asignación de plaza) — fuente única

| Campo (wire) | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `employmentAssignmentPublicId` | uuid | Sí (generado) | Único | Id de la asignación |
| (padre) `personnelFilePublicId` | uuid | Sí | Expediente existente y **completado** | Empleado dueño |
| `assignmentTypeCode` | string | Sí | **∈ catálogo** (nuevo) | Modalidad de la asignación (ortogonal a `isPrimary`) |
| `positionSlotPublicId` | uuid | **Sí** (nuevo: hoy opcional) | Existe, mismo tenant, asignable, con cupo en el período | Plaza ocupada |
| `isPrimary` | boolean | Sí | **Única principal activa** por empleado | Marca de principal |
| `isActive` | boolean | Sí | — | Activa (cuenta para cupo si además está vigente) |
| `startDate` | date-time | Sí | — | Inicio de vigencia |
| `endDate` | date-time | No | `≥ startDate` | Fin de vigencia (null = sin fin) |
| `orgUnitPublicId` | uuid | No | Existe | Unidad organizativa |
| `workCenterPublicId` | uuid | No | Existe | Centro de trabajo |
| `costCenterPublicId` | uuid | No | Existe | Centro de costo |
| `notes` | string (≤2000) | No | — | Notas |
| `concurrencyToken` | uuid | Sí (sistema) | `If-Match` | Concurrencia optimista |

### Entidad: PositionSlot (plaza) — capacidad

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `publicId` | uuid | Sí | Único | Id de la plaza |
| `jobProfilePublicId` | uuid | Sí | Existe | Perfil de puesto instanciado |
| `maxEmployees` | int | Sí | `≥ 1` | Capacidad máxima |
| `occupiedEmployees` | int | Sí | `0..maxEmployees` | Cupo ocupado — pasa a **derivarse** de asignaciones activas vigentes |
| `status` | enum | Sí | `Vacant`/`Occupied`/`Suspended` | Estado |
| `effectiveFromUtc` / `effectiveToUtc` | date-time | Sí / No | `to ≥ from` | Vigencia de la **plaza** (precondición de asignabilidad) |
| (derivado) `availableSlots` | int | — | `max − ocupadas vigentes` | Cupo disponible (nuevo, expuesto) |

### Entidad (nueva): AssignmentType (catálogo) — *seed* propuesto El Salvador

| `code` | Nombre (ES) | Name (EN) | Descripción |
|---|---|---|---|
| `LEY_SALARIOS` | Ley de Salarios | Civil-service permanent post | Plaza permanente del sector público (Ley de Salarios). |
| `CONTRATO` | Contrato | Contract | Vinculación por contrato (público/privado). |
| `INDEFINIDO` | Tiempo indefinido | Indefinite-term | Contrato por tiempo indefinido (Código de Trabajo). |
| `PLAZO_FIJO` | Plazo fijo | Fixed-term | Contrato a tiempo/plazo determinado. |
| `INTERINO` | Interinato | Interim | Cubre temporalmente a un titular ausente. |
| `POR_OBRA` | Por obra o servicio | By work/service | Vinculación por obra o servicio determinado. |
| `AD_HONOREM` | Ad honórem | Ad honorem | Designación sin remuneración. |
| `SERVICIOS_PROFESIONALES` | Servicios profesionales | Professional services | Honorarios profesionales. |
| `RECARGO_FUNCIONES` | Recargo de funciones | Additional duties | Encargaduría / recargo temporal de funciones. |

> *Seed* inicial sujeto a validación del negocio. Campos del catálogo: `code` (único), `nameEs`, `nameEn`, `description`, `isActive`.

### Entidades con campos a **eliminar**

| Entidad | Campo a eliminar |
|---|---|
| `PersonnelFile` | `AssignedPositionSlotPublicId` |
| `PersonnelFileEmployeeProfile` | `PositionSlotPublicId`, `JobProfilePublicId` |

## 13. Integraciones necesarias

- **Interna — Plazas (`PositionSlots`):** lectura de `maxEmployees`, `status`, vigencia y `JobProfile`; y actualización del cupo acoplada al ciclo de vida de las asignaciones. Endpoint relacionado existente: `PATCH /position-slots/{id}/occupancy`.
- **Interna — Expedientes (`PersonnelFiles`):** ciclo de vida candidato→empleado; resolución de "plaza del empleado" desde la asignación principal.
- **Interna — Autorización:** matriz de permisos existente (`PersonnelFiles.*`, `PositionSlots.*`).
- **Interna — Catálogos / Localización:** nuevo catálogo `AssignmentType` (ES/EN).
- **Sin integraciones externas** (P-11). Los cambios se **comunican al frontend**.

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador / Analista de RRHH | Crear/editar/activar-desactivar/eliminar asignaciones; definir principal (`PersonnelFiles.Manage`) | Solo su tenant; respeta capacidad y reglas |
| Supervisor / Jefe | Ver plazas de su personal (`PersonnelFiles.Read`) | Solo lectura por defecto |
| Empleado | Ver sus propias plazas (`PersonnelFiles.Read`) | Solo lectura |
| Administrador de Plazas | Definir `PositionSlot`, `maxEmployees`, estado, vigencia (`PositionSlots.Manage`) | No gestiona asignaciones de personas |
| Auditor | Ver histórico/trazabilidad | Solo lectura |

> Quién puede ejecutar cada acción se rige por la **matriz de permisos** del sistema por módulo/pantalla; sin workflow de aprobación adicional (P-09).

## 15. Criterios de aceptación generales

- Un empleado puede tener **N plazas activas**, con **exactamente una principal activa** en todo estado consistente.
- **Ninguna** operación permite exceder `maxEmployees` de una plaza **en ningún punto de la vigencia**; el cupo refleja las asignaciones activas vigentes.
- **No** existen plazas duplicadas activas ni vigencias solapadas en la **misma** plaza; el solape **entre plazas distintas** sí se permite.
- Los campos de plaza de la raíz y del perfil **fueron eliminados**; la plaza del empleado se resuelve desde la principal activa.
- **No** se puede eliminar/desactivar la principal sin designar otra, ni eliminar una plaza que sea principal activa de empleados (con advertencia y listado).
- **PUT/POST/PATCH/DELETE** aplican todas las reglas (incluido PATCH).
- `assignmentTypeCode` validado contra el **catálogo**; mensajes **bilingües** ES/EN.
- Cobertura de **pruebas unitarias e integración** por invariante (capacidad por vigencia, principal única, dedup, solape, concurrencia, bloqueo de baja).
- Sin regresiones en los flujos existentes de expediente/onboarding.

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1 — Cupo por vigencia:** el cálculo por solape de fechas es más complejo que un contador; mal implementado puede permitir sobre-cupo o falsos positivos. Mitigación: cálculo por solape probado + constraint/lock.
- **R2 — Concurrencia por el último cupo:** sin bloqueo/constraint adecuado puede excederse `maxEmployees`. Mitigación: bloqueo de fila de la plaza o índice/constraint + reintento.
- **R3 — Lecturas internas dependientes** de los campos eliminados (raíz/perfil): pueden existir consultas/proyecciones internas que los usen. Mitigación: inventario de usos antes de eliminar y redirigir a la principal activa.
- **R4 — Datos preexistentes inconsistentes** (múltiples principales, cupos excedidos): se resuelven **manualmente** (P-10); requiere un reporte/consulta de apoyo.

### Supuestos
- **S1** — La colección de asignaciones es la fuente única; los otros campos se eliminan (P-01).
- **S2** — "Principal/secundaria" se modela con `isPrimary` (booleano), **independiente** de `assignmentTypeCode` (P-05).
- **S3** — Una plaza con `maxEmployees > 1` puede ser compartida; un empleado puede ocupar varias plazas (N‑a‑N vía asignaciones).
- **S4** — El cupo cuenta por asignación **activa y vigente** (P-06).
- **S5** — Nómina, FTE/% dedicación y workflow quedan fuera de alcance (P-02 nómina, P-07, P-09).
- **S6** — No hay consumidores externos; los cambios se comunican al frontend (P-11).

### Dependencias
- **D1** — Definición final del **catálogo `AssignmentType`** (sobre el *seed* propuesto).
- **D2** — Estrategia de **concurrencia/cupo por vigencia** acordada con arquitectura.
- **D3** — **Ventana de despliegue** para la limpieza de esquema.
- **D4** — Inventario de **lecturas internas** que usan los campos a eliminar.
- **D5** — Punto de integración con la **eliminación de `PositionSlot`** (para RF-008).

## 17. Decisiones resueltas (antes "preguntas abiertas")

Todas las preguntas abiertas fueron resueltas por el negocio; ver la tabla **"Decisiones del negocio"** al inicio (P-01…P-11). Resumen de las de mayor impacto técnico:

- **P-01 / P-10:** asignaciones = fuente única; **se eliminan** los otros campos y datos; casos borde **manuales**.
- **P-05:** `assignmentTypeCode` con **catálogo propio**, ortogonal a `isPrimary`; *seed* El Salvador (§12).
- **P-06:** el cupo se evalúa por **vigencia** (solape de fechas).
- **P-04:** baja de principal/plaza **bloqueada** con advertencia y listado de empleados afectados.

> Pendiente menor (no bloqueante): validar el **contenido final** del catálogo `AssignmentType` y los **códigos de error** propuestos contra el catálogo canónico.

## 18. Recomendaciones del Analista de Negocio

1. **Derivar `occupiedEmployees` de las asignaciones** (no contador manual): dado que el cupo depende de la vigencia (P-06), conviene calcular la ocupación como conteo de asignaciones activas vigentes por solape, y reforzar con **constraint/lock** para impedir sobre-cupo concurrente. El `PATCH /occupancy` manual queda como override administrativo.

2. **Reforzar invariantes en dos capas** (aplicación **y** BD): p. ej. **índice único parcial** para `isPrimary = true AND isActive = true` por `(tenant_id, personnel_file_id)`, y para la plaza activa por `(tenant_id, personnel_file_id, position_slot_public_id)`. Así la integridad no depende solo del código.

3. **Cerrar el hueco del PATCH (RF-009)** como prioridad de calidad: hoy es la vía más fácil de violar invariantes.

4. **Inventariar lecturas internas** de los campos a eliminar (RF-006) antes de removerlos, y redirigirlas a la asignación principal activa.

5. **Fases sugeridas / MVP:**
   - **Fase 1 (MVP de integridad):** principal única con degradación automática, deduplicación, **validación de capacidad por vigencia**, bloqueo de baja de principal. (RF-001…005, 007, 008)
   - **Fase 2 (consolidación):** **eliminar** campos de raíz/perfil, catálogo de tipo, endurecer PATCH. (RF-006, 009, 010)
   - **Fase 3 (limpieza):** limpieza de esquema, recálculo de cupo, resolución manual de casos borde. (RF-011)

6. **Cobertura de pruebas dirigida por invariantes:** cada regla crítica (RN-03/04/05/06/08/09/13/14) con su prueba; incluir la **prueba de concurrencia** por el último cupo y casos de **solape de vigencias**.

---

## Verificación / cómo validar (en implementación)

- **Pruebas unitarias** (`tests/CLARIHR.Application.UnitTests`, junto a `PersonnelFileEmploymentAssignmentPatchTests.cs` y `PositionSlotDomainTests.cs`): principal única + degradación, dedup, solape de vigencias, capacidad por solape, hardening de PATCH, bloqueo de baja de principal.
- **Pruebas de integración**: alta/baja reflejada en el cupo; rechazo por capacidad en el período; carrera concurrente por el último cupo; bloqueo al eliminar plaza usada como principal. *(Se pueden correr sin Docker con Postgres.app + `CLARIHR_INTEGRATION_TEST_CONNECTION_STRING`.)*
- **End-to-end** vía `/api/v1/personnel-files/{id}/employment-assignments` (POST/PUT/PATCH/DELETE), confirmando los rechazos E1–E12 y los mensajes **bilingües**.
- **Comandos:** `dotnet restore && dotnet build && dotnet test`.

---

> **Naturaleza del documento:** esta es la **documentación de requerimientos / análisis de negocio** (el "qué" y el "por qué", con reglas y criterios). El diseño técnico detallado (refactor de dominio, índices/constraints, cálculo de cupo por vigencia, scripts de limpieza) se aterriza en una HU/épica de implementación a partir de este documento.
