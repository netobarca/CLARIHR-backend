# Análisis de Negocio — Retiro Definitivo de Empleado (Retiros · Entrevista · Reversión)

| | |
|---|---|
| **Fecha** | 2026-07-03 (v1 propuesta) · **2026-07-04 (v1.1 ratificada)** |
| **Analista** | Analista de Negocio Senior (asistido por auditoría de código de 4 agentes) |
| **Estado** | **Ratificado (v1.1) — 2026-07-04.** Decisiones **D-01…D-19 ratificadas por el negocio** (lo no modificado quedó según la recomendación del analista). Ajustes sobre la propuesta v1: **D-01 endurecida** (sin fallbacks ni contrato legado — el frontend cambia en el mismo release), **D-08 endurecida** (los retiros legados de prueba se **eliminan**), **D-13 endurecida** (se exige además **solicitante ≠ autorizador**) y **ventana máxima de reversión de 30 días** (RN-012.4, antes sin límite). Preguntas abiertas 1–10 resueltas (§17). Siguiente paso: plan técnico. |
| **Naturaleza del módulo** | **Orquestación NET-NEW sobre piezas existentes.** Los catálogos de categoría/motivo de retiro y el módulo de entrevista de retiro **ya existen** (Fase 1 de entrevista, mergeada a `master` vía PR #51). Lo nuevo es: (1) la **solicitud de retiro** con ciclo de autorización, (2) la **ejecución orquestada** de la baja (hoy es un procedimiento manual descompuesto), (3) la **bandeja de empleados autorizados** como punto de entrada a la entrevista y (4) la **reversión de retiro** (concepto inexistente hoy, distinto de la recontratación). |
| **Documentos hermanos** | `analisis-entrevista-retiro-empleado.md` (D-01…D-14 ratificadas), `analisis-recontratacion-empleados.md`, `plan-tecnico-recontratacion-empleados.md`, `analisis-ayuda-economica-empleado.md` y `analisis-consulta-solicitudes-constancia.md` (plantillas de "solicitud con ciclo de vida") |

---

## Contexto del cambio (requerimiento original)

> Esta opción deberá proporcionar opciones para registrar el retiro definitivo de un empleado de la institución. Las principales opciones serán las siguientes:
>
> **Retiros:** Se utilizarán los catálogos solicitados en Categoría de motivos de retiro y motivos de retiro. La información principal que se ingresará es: Solicitante, empleado, fecha de solicitud, fecha de retiro, categoría de retiro, motivo (catálogo), observación.
>
> **Entrevista de retiro:** En esta opción se deberá mostrar el listado de empleados a los que se ha autorizado el retiro de la institución. Se deberá elegir el empleado para visualizar la entrevista e ingresar las respuestas aportadas por el empleado.
>
> **Reversión de retiro:** esta opción deberá permitir la reversión de retiro de un empleado. Se deberá actualizar todos los estados que se hayan modificado con el retiro, incluyendo la liquidación, entrevista de retiro.

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Estado | Evidencia |
|---|---|---|
| **Catálogos jerárquicos de retiro** (`RetirementCategory` → `RetirementReason`) | ✅ Implementados y sembrados (entrevista Fase 1). País-scoped; la categoría lleva `SeparationType` (`Voluntaria`/`Involuntaria`/`Otra`) para roll-up. Seed SV: **8 categorías / 23 motivos**. **Solo lectura** (sin CRUD admin — RF-015 de entrevista quedó diferido). | `RetirementCatalogItems.cs:20,58`; seed `GlobalCatalogSeedData.cs:290-337`; endpoints `GET /api/v1/reference-catalogs/retirement-categories` y `retirement-reasons?parentCode=` (`GeneralCatalogKeyMap.cs:83-84`) |
| **Módulo de entrevista de retiro** | ✅ Completo (constructor de formularios + submissions + score ponderado + anonimato + bandeja de submissions). Permisos `PersonnelFiles.ManageExitInterviewForms` / `ViewExitInterviews` / `ManageExitInterviews`. | `ExitInterview.cs`, `ExitInterviewSubmission.cs`, `ExitInterviewFormsController.cs`, `ExitInterviewsController.cs` |
| **Recontratación (rehire)** | ✅ Completa: `POST .../rehire` cierra el periodo anterior, reabre el expediente, archiva entrevistas, crea contrato+asignación nuevos, re-aprovisiona el login y journalea la acción `RECONTRATACION`. Permiso de excepción `PersonnelFiles.AuthorizeRehire`. | `RehireEmployee.cs:82-289`, `PersonnelFile.cs:899-915` (`ReopenForRehire`) |
| **Catálogo de estados laborales** (`EmploymentStatus`) | ✅ Sembrado SV: `ACTIVO`, `SUSPENDIDO`, `LICENCIA`, `INCAPACIDAD`, `RETIRADO`. | `GlobalCatalogSeedData.cs:175-181` |
| **Plantillas de "solicitud con ciclo de vida"** | ✅ `PersonnelFileEconomicAidRequest` (estados canónicos + catálogo, PATCH `/resolution` `/disbursement` `/cancel`, **anti-auto-aprobación**) y `PersonnelFileCertificateRequest` (ciclo lineal + bandeja empresa + export Excel). | `PersonnelFileEmployee.cs:1643-1822`; `PersonnelFileCertificateRequest.cs:46-245`; `EconomicAidRequests.Handlers.cs:371-376` (anti-auto-aprobación) |
| **Acciones de personal** (journal append-only) | ✅ Existe con catálogos `ActionType`/`ActionStatus`, pero **no hay tipo `BAJA`/`RETIRO` sembrado** y el retiro **no** journalea nada hoy. | `PersonnelFileEmployee.cs:513-584`; seed `GlobalCatalogSeedData.cs:713-738` |

### Cómo se registra un retiro HOY — el hallazgo central

**No existe una operación de "retiro" en el sistema.** La baja es un **procedimiento manual descompuesto** en 2–3 llamadas genéricas que el usuario debe conocer y encadenar por su cuenta:

| Paso | Operación | Qué hace | Evidencia |
|---|---|---|---|
| 1 | `PUT /api/v1/personnel-files/{id}/employment-information` | Escribe `RetirementDate`, `RetirementCategoryCode`, `RetirementReasonCode`, `RetirementNotes` y (si el usuario se acuerda) `EmploymentStatusCode = RETIRADO` sobre el perfil 1:1 del empleado. | `EmployeeProfiles.cs:144-266`; campos en `PersonnelFileEmployee.cs:49-55` |
| 2 | `PATCH /api/v1/personnel-files/{id}` | Marca `isActive=false` y, opcionalmente, el bloqueo de recontratación (`IsRehireBlocked` + razón). | `PersonnelFilesController.cs:225-259` |
| 3 (opcional, manual) | Desactivar el usuario de login | Acción administrativa **separada** en Company Users; el flujo de recontratación *asume* que se hizo, pero nada lo garantiza. | `CompanyUsersController.cs:215`; `CompanyUserProvisioningService.cs:84-92` |

**Señal canónica de "retirado" = `RetirementDate != null`** (no el estado del catálogo): así lo consumen la recontratación (`RehireEligibilityRules.cs:33-38`), la antigüedad computada (`EmployeeProfiles.cs:22-24`) y el tablero (`PersonnelFileDashboard.Rules.cs:53-56`).

⚠️ **Trampa de nombres:** las clases `FinalizePersonnelFile*` / `PersonnelFileFinalizationService` son la finalización del **onboarding** (Draft → Completed + aprovisionar login), **no** el retiro. El nuevo módulo debe evitar el término "finalización" para la baja.

**Lo que el retiro NO hace hoy** (todo queda a criterio/memoria del operador):

- **No cierra plazas ni contratos**: las `EmploymentAssignment`/`ContractHistory` activas quedan abiertas; solo la **recontratación** las cierra después (`RehireEmployee.cs:152-153`). Un retirado sigue "ocupando" su plaza.
- **No desactiva el login** (paso manual separado).
- **No journalea** ninguna acción de personal (no existe tipo `BAJA`).
- **No valida fechas**: `RetirementDate` puede ser **anterior a `HireDate`** o futura sin restricción (contrastar con `PersonnelFilePreviousEmployment`, que sí valida — `PersonnelFile.cs:2211-2213`).
- **No exige coherencia** entre `RetirementDate` y `EmploymentStatusCode` (puede haber "RETIRADO" sin fecha, o fecha sin estado).
- **No registra solicitante, fecha de solicitud ni autorización** — el único control es el permiso genérico `PersonnelFiles.Manage`.

### Lo que NO existe (verificado exhaustivamente)

| Concepto | Resultado de la búsqueda |
|---|---|
| **Solicitud/autorización de retiro** (solicitante, fecha solicitud, estado pendiente/autorizado) | ❌ No existe ninguna entidad ni endpoint (`RetirementRequest`, `TerminationRequest`, etc. — cero resultados). |
| **Reversión de retiro** | ❌ No existe ni se menciona en ninguna documentación previa. Lo más cercano es la recontratación, que es **otro concepto** (nuevo periodo laboral, no anulación del retiro). |
| **Liquidación / finiquito / indemnización** | ❌ No existe módulo ni entidad. La recontratación lo resolvió con una **confirmación manual** (`PriorPeriodClosureConfirmed`, D-17 de recontratación) y los docs lo difieren a un "(Futuro) módulo de nómina o de baja de personal" (`analisis-recontratacion-empleados.md:613`). |
| **Saldos de vacaciones/incapacidades** que un retiro deba liquidar | ❌ No existe entidad de saldo/acumulado (módulo futuro). |
| **Listado de "empleados autorizados a retiro"** | ❌ No existe. `SearchPersonnelFilesQuery` no filtra por retiro; la bandeja de entrevistas (`GET /api/v1/exit-interviews`) lista **submissions ya existentes**, no empleados pendientes de entrevistar. |
| **Notificaciones reales** (correo/in-app) | ❌ Solo stubs de log (`LoggingEmailService`, `LoggingAuthEmailService`); no hay `INotificationService`. |

### Cómo se habilita la entrevista HOY (gate actual)

`SaveExitInterviewSubmissionCommandHandler` exige, en orden (`ExitInterviewSubmissions.Handlers.cs:96-147`): (1) expediente `Employee` + `Completed`; (2) **que el perfil ya tenga `RetirementReasonCode` grabado** ("register the baja reason first"); (3) formulario **publicado y activo para ese motivo** (D-03 de entrevista). `RetirementDate` **no** es requisito (solo deriva el `Period`). Es decir: hoy la entrevista se "habilita" grabando el motivo en el perfil vía el PUT genérico — exactamente la puerta dispersa que este módulo viene a ordenar.

---

## Brechas identificadas (GAP → resolución ratificada)

| # | Brecha verificada | Resolución (ratificada 2026-07-04) |
|---|---|---|
| **G-01** | No existe entidad de **solicitud de retiro** (solicitante, fechas, estado). | Nueva entidad `PersonnelFileRetirementRequest` con ciclo `SOLICITADA → AUTORIZADA → EJECUTADA` (+ `RECHAZADA`/`ANULADA`/`REVERTIDA`), plantilla EconomicAid/Certificate (RF-001…RF-005). |
| **G-02** | La **ejecución de la baja** está dispersa en 2–3 llamadas manuales sin orquestación ni garantías. | Operación única "ejecutar retiro" que aplica todos los efectos de forma transaccional y deja **snapshot para reversión** (RF-006). |
| **G-03** | No hay **listado de empleados autorizados a retiro** para la entrevista. | Bandeja de retiros autorizados con estado de entrevista por empleado (RF-008). |
| **G-04** | El gate de la entrevista lee el motivo **solo del perfil** (ya consumada la baja). | Adaptarlo para resolver el motivo desde la **solicitud autorizada** (entrevista *antes* de la salida), sin fallback al perfil (D-01/D-08 ratificadas eliminan la vía legada y sus datos) (RF-009). |
| **G-05** | No existe **reversión**; la entrevista solo sabe archivarse (no hay des-archivado) y nada restaura estados. | Reversión con restauración de estados desde el snapshot de ejecución + archivado de la submission (RF-010…RF-012). |
| **G-06** | El retiro **no journalea** acción de personal (no hay tipo `BAJA`). | Sembrar `BAJA` y `REVERSION_BAJA` en `ActionType` y journalear automáticamente (RF-007). |
| **G-07** | **Validaciones de fecha ausentes** (retiro < ingreso permitido hoy). | Endurecer reglas de coherencia de fechas (RF-016). |
| **G-08** | **Doble puerta**: el PUT genérico seguiría permitiendo bajas directas que la bandeja no vería. | Cerrar la puerta legada: retirar los campos `Retirement*` del PUT y reservar `RETIRADO` al módulo (RF-015, D-01). |
| **G-09** | La mención del requerimiento a **"revertir la liquidación"** no tiene contraparte en el sistema (no hay módulo). | Declararlo fuera de alcance Fase 1 con punto de integración futuro; gestionar expectativa (D-14, R-02). |
| **G-10** | Sin notificaciones para avisar al autorizador/solicitante. | Fase 2 (requiere infraestructura real de correo; hoy solo stubs) (D-17). |

---

## Decisiones ratificadas por el negocio (2026-07-04) — D-01…D-19

| # | Pregunta | Resolución ratificada (2026-07-04) |
|---|---|---|
| **D-01** | ¿El módulo se convierte en la **única puerta** para registrar bajas? | **Sí.** Retirar `RetirementCategoryCode`/`RetirementReasonCode`/`RetirementNotes`/`RetirementDate` del `PUT .../employment-information` y rechazar `EmploymentStatusCode=RETIRADO` manual. Sin esto, la bandeja y la reversión operan sobre datos incompletos (G-08). **Endurecida en la ratificación:** el frontend cambia en el mismo release — **sin fallbacks ni compatibilidad legada**; los campos se retiran del contrato de una sola vez. |
| **D-02** | ¿Qué es el **"Solicitante"**? | **Referencia a un expediente de la empresa** (picker) + snapshot del nombre, pudiendo ser **el propio empleado** (renuncia) o su jefatura/RRHH (despido u otros). Adicionalmente el sistema siempre audita `RequestedByUserId` (quién registró en el sistema). |
| **D-03** | ¿**Autoservicio** de renuncia (el empleado registra su propia solicitud)? | **No en Fase 1** (captura por RRHH, como pide el requerimiento). Autoservicio como evolución Fase 2. |
| **D-04** | **Ciclo de estados** de la solicitud | `SOLICITADA → AUTORIZADA → EJECUTADA → REVERTIDA`, con salidas `RECHAZADA` (por autorizador, nota obligatoria) y `ANULADA` (por gestor/autorizador antes de ejecutar). Modelo **híbrido**: constantes canónicas + catálogo país-scoped para visualización (patrón ayuda económica). |
| **D-05** | ¿Cuándo se **ejecuta** la baja? | **Acción manual explícita** ("ejecutar retiro") disponible desde `AUTORIZADA` y solo cuando `FechaRetiro ≤ hoy`. Sin ejecución automática por fecha en Fase 1 (no existe scheduler de dominio); permite bajas retroactivas (registrar y ejecutar el mismo día una baja ya consumada). |
| **D-06** | **Efectos de la ejecución**: ¿cerrar plazas/contratos y desactivar login? | **Sí a ambos.** Cerrar asignaciones y contratos activos con fecha efectiva = `FechaRetiro` (libera la plaza; hoy queda ocupada hasta una eventual recontratación) y desactivar el usuario de login (hoy es un paso manual que se olvida). Ambos quedan registrados en el snapshot de ejecución para poder revertirse. |
| **D-07** | ¿Desde qué estado se **habilita la entrevista**? | **Desde `AUTORIZADA`** (y sigue disponible en `EJECUTADA`). Es la lectura literal del requerimiento ("empleados a los que se ha autorizado el retiro") y la mejor práctica: capturar la entrevista **antes** de que el empleado se vaya. |
| **D-08** | ¿La **reversión** aplica a retiros registrados fuera del módulo (legados)? | **No en Fase 1**: solo retiros ejecutados por el módulo (tienen snapshot). Los datos legados son de prueba (precedente D-11 de entrevista) y, **por ratificación, se eliminan** en una limpieza única al desplegar el módulo — tras ella, todo retiro del sistema proviene de la puerta única. |
| **D-09** | ¿Qué pasa con la **entrevista** al revertir? | **Archivar** la submission (borrador o enviada): la baja "no ocurrió", no debe contar en la tabulación de rotación. Coherente con D-12 de entrevista (archivar en rehire, nunca borrar). |
| **D-10** | ¿Reversión tras una **recontratación** posterior? | **Bloqueada.** Si hubo rehire después de la ejecución, el retiro pertenece a un periodo ya cerrado; revertirlo corrompería la línea de tiempo. Regla dura. |
| **D-11** | ¿Cómo se restaura el **estado laboral previo**? | **Snapshot mínimo en la ejecución**: `EmploymentStatusCode` previo, si el login estaba activo, e IDs de asignaciones/contratos cerrados. La reversión restaura exactamente eso (no asume "ACTIVO"). |
| **D-12** | **Permisos** | 4 dedicados: `PersonnelFiles.ViewRetirements` (bandeja/detalle), `ManageRetirements` (registrar/editar/anular/ejecutar), `AuthorizeRetirement` (autorizar/rechazar), `RevertRetirement` (revertir). Los dos últimos **no** implicados por `PersonnelFiles.Admin` (patrón `AuthorizeRehire`, `PersonnelFileAuthorizationService.cs:244-246`). Lectura solo RRHH (coherente con D-14 de entrevista). |
| **D-13** | **Anti-auto-aprobación** | Regla dura: el **empleado sujeto** no puede autorizar/ejecutar/revertir su propio retiro (`LinkedUserPublicId ≠ usuario actual`, patrón `ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN`). **Endurecida en la ratificación: se exige además solicitante ≠ autorizador** — quien pide el retiro nunca lo autoriza; si el autorizador habitual es quien se retira, debe autorizar **su superior** (otro usuario con el permiso dedicado `AuthorizeRetirement`). Sin enrutamiento jerárquico automático en Fase 1: lo garantizan las dos desigualdades + el permiso. |
| **D-14** | **Liquidación** | **Fuera de alcance Fase 1** — no existe módulo de nómina/liquidación que revertir (verificado). La reversión cubre los estados del sistema; cualquier liquidación pagada en el mundo real se gestiona administrativamente fuera. Declarar el punto de integración para cuando exista nómina. **Ratificada explícitamente por el negocio (2026-07-04):** la liquidación se gestiona administrativamente fuera del sistema; su reversión queda como integración futura. |
| **D-15** | **Acción de personal** | Sembrar tipos `BAJA` y `REVERSION_BAJA` (SV) y journalear automáticamente en ejecución y reversión con estado `APLICADA` (sembrado). De paso corregir que la recontratación emite `COMPLETADA`, código no sembrado en `ActionStatus` (inconsistencia preexistente, `RehireEmployee.cs:79-80`). |
| **D-16** | **Catálogo de estados de solicitud** | Nuevo catálogo país-scoped `retirement-request-statuses` seed SV (6 códigos del ciclo D-04), patrón híbrido canónico+catálogo. |
| **D-17** | **Notificaciones** de autorización | **Fase 2 / futuro.** No hay infraestructura real de correo ni in-app (solo stubs de log). El flujo Fase 1 opera por bandeja con filtros por estado. |
| **D-18** | **Bloqueo de recontratación** en la baja | Mantener la marca `IsRehireBlocked` + razón como parte opcional de la **ejecución** (hoy se hace en el PATCH raíz), para que el flujo quede completo en una sola operación. |
| **D-19** | **Adjuntos** en la solicitud (carta de renuncia/despido) | **Fase 2 (confirmado por el negocio).** La pila de adjuntos está estandarizada (3 precedentes) y es barata de añadir después; la entrevista ya decidió no llevar adjuntos (D-09 de entrevista). |

---

## 1. Resumen del producto o requerimiento

Se construirá el módulo de **Retiro Definitivo de Empleado**, que formaliza el proceso de baja de la institución en un **ciclo controlado de solicitud → autorización → ejecución**, integra la **entrevista de retiro** existente como paso natural del proceso (bandeja de empleados con retiro autorizado) y añade la **reversión de retiro** para deshacer bajas registradas por error o rescindidas.

**Problema que resuelve.** Hoy el retiro es una edición de campos dispersa en dos o tres pantallas/llamadas genéricas, sin registro de quién lo solicitó ni quién lo autorizó, sin efectos garantizados (la plaza queda ocupada, el login activo, sin traza en el journal de acciones) y **sin vuelta atrás**: si una baja se registró por error, no existe ninguna operación que restaure el estado del empleado. Además, la entrevista de retiro —ya construida— carece de un punto de entrada operativo: RRHH no tiene forma de listar a quiénes debe entrevistar.

**Objetivo principal.** Un único lugar donde RRHH registra la solicitud de retiro (solicitante, empleado, fechas, categoría y motivo de catálogo, observación), un autorizador la aprueba o rechaza, el sistema **ejecuta la baja de forma orquestada y transaccional** (perfil, estado laboral, expediente, plazas, contratos, login, journal) dejando constancia suficiente para **revertirla íntegramente** si corresponde, y la entrevista se ofrece a los empleados con retiro autorizado.

## 2. Objetivos del negocio

- **O-1. Control y gobierno de la baja:** toda baja queda registrada como solicitud con solicitante, autorizador, fechas y motivo de catálogo — trazabilidad completa del proceso más sensible del ciclo laboral.
- **O-2. Integridad de datos:** la ejecución orquestada elimina los estados a medias de hoy (retirado con plaza ocupada, login activo, estado laboral incoherente, fechas inválidas).
- **O-3. Reducción de riesgo operativo:** la reversión formal permite corregir bajas erróneas o rescindidas sin manipulación manual de datos (hoy imposible sin tocar la base).
- **O-4. Mejor captación de causas de rotación:** la bandeja de autorizados convierte la entrevista (opcional, D-05 de entrevista) en un paso visible del proceso, aumentando la tasa de respuesta **antes** de que el empleado se desvincule; alimenta la tabulación Fase 2 de entrevista y los indicadores de bajas del tablero RRHH.
- **O-5. Cumplimiento del flujo institucional:** separación de funciones (quien solicita ≠ quien autoriza ≠ el sujeto) con permisos dedicados y anti-auto-aprobación.

## 3. Alcance funcional

### Fase 1 — MVP

- **F1.** Registro de la **solicitud de retiro** con: solicitante (referencia a expediente + snapshot), empleado, fecha de solicitud, fecha de retiro, categoría de retiro (catálogo), motivo (catálogo, coherente con la categoría), observación — RF-001.
- **F2.** **Bandeja de retiros** de la empresa con filtros (estado, categoría, motivo, rango de fechas, empleado, texto), detalle y **exportación** (xlsx/csv/json) — RF-002.
- **F3.** **Edición** de la solicitud en estado `SOLICITADA` — RF-003.
- **F4.** **Autorizar / rechazar** (permiso dedicado, anti-auto-aprobación, nota) y **anular** — RF-004, RF-005.
- **F5.** **Ejecución orquestada de la baja** (transaccional): perfil de empleo (fechas/códigos/notas + `RETIRADO`), expediente inactivo, cierre de plazas y contratos a la fecha efectiva, desactivación del login, bloqueo de recontratación opcional, snapshot de reversión, acción `BAJA` en el journal — RF-006, RF-007.
- **F6.** **Bandeja de entrevistas**: listado de empleados con retiro **autorizado** (y ejecutado) con el estado de su entrevista (sin formulario configurado / pendiente / borrador / enviada) — RF-008.
- **F7.** **Habilitación de la entrevista desde la solicitud autorizada** (el gate del módulo de entrevista resuelve el motivo desde la solicitud — única fuente tras D-01/D-08) — RF-009; la captura/visualización reutiliza el módulo existente sin cambios.
- **F8.** **Reversión del retiro ejecutado**: restauración de todos los estados modificados por la ejecución (desde el snapshot), archivado de la entrevista, motivo de reversión obligatorio, permiso dedicado, **ventana máxima de 30 días desde la ejecución**, acción `REVERSION_BAJA` — RF-010…RF-012.
- **F9.** **Catálogo de estados** de la solicitud (seed SV) — RF-013; **permisos dedicados** — RF-014; **puerta única** (cierre de la vía legada) — RF-015; **endurecimiento de validaciones de fechas** — RF-016.

### Fase 2 — Evoluciones

- Notificaciones (correo/in-app) al autorizador y al solicitante (D-17; requiere infraestructura real).
- Autoservicio de renuncia (el empleado inicia su solicitud) (D-03).
- Adjuntos de la solicitud (carta de renuncia/despido) (D-19).
- Integración con el futuro módulo de nómina/liquidación (D-14).
- CRUD administrativo de los catálogos de categoría/motivo (retoma RF-015 del análisis de entrevista).

## 4. Fuera de alcance

- **FA-1. Liquidación / finiquito:** ni el cálculo ni su reversión — **no existe módulo de nómina** (D-14). La reversión Fase 1 restaura exclusivamente estados del sistema.
- **FA-2. Motor de aprobación genérico / multinivel:** un solo nivel de autorización con permiso dedicado (patrón vigente en toda la casa; los flujos multinivel están consistentemente diferidos).
- **FA-3. Saldos de vacaciones/incapacidades:** no existen entidades de saldo que liquidar o restaurar (módulo futuro).
- **FA-4. Cambios al constructor/captura de la entrevista:** el módulo de entrevista se consume tal cual (D-01…D-14 de entrevista siguen vigentes); solo se adapta su **gate** y se añade la bandeja.
- **FA-5. Recontratación:** ya existe y no se modifica (salvo la corrección menor del estado `COMPLETADA`, D-15). La reversión es un concepto distinto y complementario.
- **FA-6. Ejecución automática por fecha (scheduler):** la ejecución es manual en Fase 1 (D-05).
- **FA-7. Notificaciones y autoservicio:** Fase 2 (D-17, D-03).
- **FA-8. Bajas parciales por plaza:** el retiro definitivo es **del empleado** respecto de la institución; cierra **todas** sus plazas activas. Dejar una plaza específica se gestiona en el módulo de asignaciones.

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Gestor de RRHH** | Registra, edita, anula y **ejecuta** solicitudes de retiro; consulta la bandeja y exporta. |
| **Autorizador de retiros** (RRHH senior / dirección) | **Autoriza o rechaza** solicitudes; no puede autorizar su propio retiro (anti-auto-aprobación) **ni una solicitud en la que figura como solicitante** (separación de funciones — D-13; si el autorizador habitual es quien se retira, autoriza su superior). Puede anular una autorizada antes de ejecutar. |
| **Revertidor** (rol de alta confianza) | Ejecuta la **reversión** de un retiro ejecutado, con motivo obligatorio. |
| **Entrevistador de RRHH** | Desde la bandeja de autorizados, abre la entrevista del empleado y **captura** sus respuestas (permisos existentes del módulo de entrevista). |
| **Empleado que se retira** | Sujeto de la solicitud; como solicitante cuando es renuncia (referenciado, no autoservicio en Fase 1). Puede llenar su entrevista por autoservicio (capacidad existente, opcional — D-05 de entrevista). |
| **Solicitante** | Persona (expediente) que pide el retiro: el propio empleado (renuncia), su jefatura o RRHH (D-02). |
| **Sistema** | Orquesta la ejecución transaccional, valida catálogos y fechas, journalea acciones `BAJA`/`REVERSION_BAJA`, mantiene el snapshot de reversión y audita todo. |

## 6. Requerimientos funcionales

### Grupo A — Registro y ciclo de vida del retiro

### RF-001 — Registrar solicitud de retiro

**Descripción:**
Crear una solicitud de retiro para un empleado con: **solicitante** (referencia a expediente + snapshot de nombre — D-02), **empleado**, **fecha de solicitud**, **fecha de retiro** (efectiva; pasada o futura), **categoría de retiro** y **motivo** (catálogos existentes `RetirementCategory`/`RetirementReason`) y **observación**. Nace en estado `SOLICITADA`.

**Reglas de negocio:**
- RN-001.1 Empleado elegible: expediente `Employee` + `Completed`, activo y **no retirado** (`RetirementDate` nulo en el perfil).
- RN-001.2 **A lo sumo una solicitud abierta** (`SOLICITADA` o `AUTORIZADA`) por empleado.
- RN-001.3 Categoría activa en el catálogo del país; motivo activo y **perteneciente a la categoría** (validación jerárquica existente, `PersonnelReferenceCatalogs.cs:352-415`).
- RN-001.4 `FechaSolicitud ≤ hoy`; `FechaRetiro ≥ HireDate` del perfil (RF-016). La fecha de retiro puede ser futura (baja programada) o pasada (baja retroactiva).
- RN-001.5 Solicitante: expediente válido de la empresa; puede ser el mismo empleado (renuncia). El sistema audita además `RequestedByUserId`.
- RN-001.6 Observación opcional, ≤ 2000 caracteres.

**Criterios de aceptación:**
- Solicitud creada en `SOLICITADA` con todos los datos y snapshot del solicitante; retorna `ETag`/token de concurrencia.
- Rechazo 422 con código bilingüe si: empleado no elegible, ya retirado, solicitud abierta duplicada, catálogo inválido o incoherente, fechas inválidas.
- Auditoría de creación registrada.

**Prioridad:** Alta
**Dependencias:** Catálogos `RetirementCategory`/`RetirementReason` (existentes); RF-013 (estados); RF-014 (permisos).

### RF-002 — Bandeja de retiros (consulta, detalle y exportación)

**Descripción:**
Consulta a nivel empresa de todas las solicitudes con filtros (estado, categoría, motivo, empleado, rango de fecha de solicitud/retiro, texto libre), paginación, contadores por estado, detalle individual con su línea de tiempo (solicitada/autorizada/ejecutada/revertida, por quién y cuándo) y **exportación** xlsx/csv/json (patrón bandeja de constancias: `CertificateRequestsReportingController` + `ReportExportDeliveryService`).

**Reglas de negocio:**
- RN-002.1 Acceso solo con `ViewRetirements` (lectura RRHH — D-12); autorización por handler en el controlador de reporting (convención: sin `AuthorizationPolicySet` en bandejas para evitar el falso 403 del POST-consulta).
- RN-002.2 La exportación respeta los filtros activos, se audita y aplica el límite síncrono estándar (413 si excede).
- RN-002.3 El detalle muestra los snapshots (solicitante, notas de resolución/reversión) aunque los expedientes cambien después.

**Criterios de aceptación:**
- Listado filtrable + contadores por estado; export descarga el mismo conjunto filtrado; rate-limit de búsqueda aplicado.

**Prioridad:** Alta
**Dependencias:** RF-001; infraestructura de reporting existente.

### RF-003 — Editar solicitud

**Descripción:**
Modificar los datos de negocio de una solicitud (solicitante, fechas, categoría/motivo, observación) **solo en estado `SOLICITADA`**.

**Reglas de negocio:**
- RN-003.1 Solo `SOLICITADA` es editable; una `AUTORIZADA` no se edita (anular y re-registrar, o rechazar), garantizando que lo autorizado sea exactamente lo registrado.
- RN-003.2 Mismas validaciones de RF-001; concurrencia optimista `If-Match` (400 sin header / 409 obsoleto).

**Criterios de aceptación:**
- Edición exitosa refresca token; intento sobre estado ≠ `SOLICITADA` retorna 422 con código de regla de estado.

**Prioridad:** Media
**Dependencias:** RF-001.

### RF-004 — Autorizar / rechazar solicitud

**Descripción:**
Acción de resolución del autorizador sobre una `SOLICITADA`: **autorizar** (pasa a `AUTORIZADA`, habilita la entrevista — D-07 — y la ejecución) o **rechazar** (pasa a `RECHAZADA`, terminal) con **nota obligatoria** en el rechazo. Endpoint de acción tipo `PATCH .../resolution` (patrón ayuda económica).

**Reglas de negocio:**
- RN-004.1 Requiere permiso `AuthorizeRetirement` (no implicado por `PersonnelFiles.Admin` — D-12).
- RN-004.2 **Anti-auto-aprobación** (D-13): si el usuario actual es el empleado sujeto (`LinkedUserPublicId`), 403 con código dedicado.
- RN-004.3 Solo desde `SOLICITADA`; registra `ResolvedByUserId`, `ResolutionDateUtc`, `ResolutionNotes` (obligatoria en rechazo, opcional en autorización).
- RN-004.4 La autorización re-verifica la elegibilidad del empleado (sigue activo y no retirado).
- RN-004.5 **Separación de funciones (D-13 ratificada): solicitante ≠ autorizador.** Si el usuario actual corresponde al expediente solicitante (su `LinkedUserPublicId`), 403 con código dedicado; si el autorizador habitual es quien se retira, autoriza su superior (otro usuario con el permiso).

**Criterios de aceptación:**
- Autorizada aparece en la bandeja de entrevistas (RF-008); rechazada es terminal y permite registrar una nueva solicitud.
- Auto-autorización (empleado sujeto) y autorización por el propio solicitante bloqueadas con 403; transición inválida 422.

**Prioridad:** Alta
**Dependencias:** RF-001, RF-014.

### RF-005 — Anular solicitud

**Descripción:**
Dejar sin efecto una solicitud **antes de su ejecución**: `SOLICITADA → ANULADA` (gestor) o `AUTORIZADA → ANULADA` (autorizador), con nota opcional. Terminal.

**Reglas de negocio:**
- RN-005.1 `SOLICITADA` anulable con `ManageRetirements`; `AUTORIZADA` anulable solo con `AuthorizeRetirement` (deshace una autorización).
- RN-005.2 Una `EJECUTADA` **no** se anula: se **revierte** (RF-010).
- RN-005.3 Al anular una `AUTORIZADA`, el empleado sale de la bandeja de entrevistas; una entrevista en borrador asociada se archiva (coherente con D-09).

**Criterios de aceptación:**
- Transiciones válidas aplicadas con auditoría; anulación de `EJECUTADA` rechazada con 422 indicando la vía de reversión.

**Prioridad:** Media
**Dependencias:** RF-001, RF-004.

### RF-006 — Ejecutar el retiro (orquestación de la baja)

**Descripción:**
Acción explícita sobre una `AUTORIZADA` (D-05) que **consuma la baja de forma transaccional**:
1. Escribe en el perfil de empleo: `RetirementDate = FechaRetiro`, `RetirementCategoryCode`, `RetirementReasonCode`, `RetirementNotes = observación`, `EmploymentStatusCode = RETIRADO`.
2. Marca el expediente `IsActive = false` (y opcionalmente `IsRehireBlocked` + razón — D-18).
3. **Cierra** las asignaciones de plaza y contratos activos con fecha efectiva = `FechaRetiro` (D-06).
4. **Desactiva** el usuario de login vinculado, si existe (D-06).
5. Guarda el **snapshot de reversión** (D-11): estado laboral previo, login activo previo, IDs de filas cerradas, marca de bloqueo previa.
6. Journalea la acción `BAJA` (`IsSystemGenerated`, estado `APLICADA`) — RF-007.
7. Solicitud pasa a `EJECUTADA` (`EjecutadoPorUserId`, `FechaEjecucionUtc`).

**Reglas de negocio:**
- RN-006.1 Solo desde `AUTORIZADA`, con `ManageRetirements`, y **solo si `FechaRetiro ≤ hoy`** (una baja programada a futuro se ejecuta cuando la fecha llega — D-05).
- RN-006.2 Anti-auto-ejecución: el sujeto no ejecuta su propia baja (D-13).
- RN-006.3 Re-verifica que el empleado siga elegible (no retirado por otra vía); si el estado del perfil divergió, 422 con código de conflicto de estado.
- RN-006.4 **Todo o nada:** los pasos 1–7 en una única transacción (patrón rehire, `RehireEmployee.cs:147-289`).
- RN-006.5 La ejecución es idempotente frente a reintentos vía token de concurrencia.

**Criterios de aceptación:**
- Tras ejecutar: perfil con datos de retiro coherentes, expediente inactivo, plazas/contratos cerrados a la fecha, login desactivado, acción `BAJA` en el journal, snapshot persistido, antigüedad congelada a `FechaRetiro` (cálculo existente), y el empleado elegible para **recontratación** (`RetirementDate != null`).
- Intento con `FechaRetiro` futura retorna 422 explicando la ventana.

**Prioridad:** Alta
**Dependencias:** RF-004; módulos de plazas/contratos e IAM (desactivación de usuario); RF-013/RF-014.

### RF-007 — Trazabilidad: acción de personal y auditoría

**Descripción:**
Sembrar los tipos de acción `BAJA` y `REVERSION_BAJA` (catálogo `ActionType`, SV) y journalear automáticamente en la ejecución y la reversión (append-only, `IsSystemGenerated = true`, estado `APLICADA`), replicando el patrón `RECONTRATACION` (`RehireEmployee.cs:246-260`). Toda transición de la solicitud queda auditada (quién, cuándo, antes/después).

**Reglas de negocio:**
- RN-007.1 Corregir de paso la inconsistencia preexistente: la acción `RECONTRATACION` usa estado `COMPLETADA`, código no sembrado en `ActionStatus` (D-15).

**Criterios de aceptación:**
- Ejecutar una baja produce exactamente una acción `BAJA`; revertirla, una `REVERSION_BAJA`; visibles en el journal y export existentes.

**Prioridad:** Media
**Dependencias:** RF-006, RF-010; catálogo `ActionType` (migración de seed).

### Grupo B — Entrevista de retiro (integración)

### RF-008 — Bandeja de entrevistas: empleados con retiro autorizado

**Descripción:**
Listado a nivel empresa de los empleados cuya solicitud de retiro está en `AUTORIZADA` o `EJECUTADA` (D-07), mostrando: empleado, categoría/motivo, fecha de retiro, estado de la solicitud y **estado de la entrevista** (`SIN_FORMULARIO` — no hay formulario activo para el motivo —, `PENDIENTE`, `BORRADOR`, `ENVIADA`). Desde cada fila se navega a la entrevista del empleado (visualizar el formulario e ingresar las respuestas — funcionalidad existente).

**Reglas de negocio:**
- RN-008.1 Acceso con `ViewExitInterviews` (permiso existente del módulo de entrevista) o `ViewRetirements`; la lectura de respuestas sigue siendo solo RRHH (D-14 de entrevista).
- RN-008.2 Los retiros `REVERTIDA`/`ANULADA`/`RECHAZADA` no aparecen.
- RN-008.3 La entrevista sigue siendo **opcional** (D-05 de entrevista): la bandeja es operativa, no bloquea la ejecución de la baja.
- RN-008.4 Filtros mínimos: estado de entrevista, categoría/motivo, rango de fecha de retiro.

**Criterios de aceptación:**
- Autorizar una solicitud hace aparecer al empleado en la bandeja; revertir/anular lo remueve; el estado de entrevista refleja el ciclo real de la submission.

**Prioridad:** Alta
**Dependencias:** RF-004; módulo de entrevista existente.

### RF-009 — Habilitar la entrevista desde el retiro autorizado

**Descripción:**
Adaptar la precondición del guardado de submissions: hoy exige `RetirementReasonCode` **en el perfil** (`ExitInterviewSubmissions.Handlers.cs:107-112`), que solo existe tras consumar la baja. Con el nuevo flujo, el motivo debe resolverse desde la **solicitud de retiro vigente (`AUTORIZADA`/`EJECUTADA`)**. La v1 proponía un fallback al perfil para bajas legadas; **con D-01/D-08 ratificadas (puerta única + eliminación de los datos legados de prueba) el fallback se descarta**: la solicitud es la única fuente del motivo. El `Period` de la submission deriva de la `FechaRetiro` de la solicitud.

**Reglas de negocio:**
- RN-009.1 Resolución del motivo: **únicamente desde la solicitud de retiro vigente** (sin fallback al perfil — D-01/D-08 ratificadas eliminan la vía legada y sus datos); el formulario aplicable sigue siendo el activo para ese motivo (D-03 de entrevista, sin cambios).
- RN-009.2 El autoservicio del empleado (llenar su propia entrevista) queda habilitado desde `AUTORIZADA` (aún tiene login activo — la ejecución lo desactiva, D-06; tras la ejecución solo captura RRHH salvo formulario anónimo llenado antes).
- RN-009.3 Snapshots de la submission (motivo/categoría/`SeparationType`/plaza/periodo) sin cambios de estructura.

**Criterios de aceptación:**
- Con solicitud `AUTORIZADA` y formulario activo para el motivo, la entrevista se puede visualizar y capturar sin que el perfil tenga aún datos de retiro; tras la ejecución, sigue disponible; sin solicitud vigente, 422 de precondición.

**Prioridad:** Alta
**Dependencias:** RF-004; módulo de entrevista (ajuste puntual del gate).

### Grupo C — Reversión

### RF-010 — Revertir retiro ejecutado

**Descripción:**
Acción sobre una solicitud `EJECUTADA` que **restaura los estados modificados por la ejecución** usando el snapshot (D-11), con **motivo de reversión obligatorio** y permiso dedicado `RevertRetirement`:
1. Perfil: limpia `RetirementDate`/`RetirementCategoryCode`/`RetirementReasonCode`/`RetirementNotes`; restaura el `EmploymentStatusCode` previo.
2. Expediente: `IsActive = true`; restaura el estado previo del bloqueo de recontratación.
3. **Reabre** exactamente las asignaciones/contratos que la ejecución cerró (limpia fecha de fin y reactiva).
4. Reactiva el login **si estaba activo antes** de la ejecución.
5. **Archiva** la submission de entrevista vinculada, si existe (D-09).
6. Journalea `REVERSION_BAJA`; solicitud pasa a `REVERTIDA` (`RevertidoPorUserId`, `FechaReversionUtc`, `MotivoReversion`).

**Reglas de negocio:**
- RN-010.1 Solo desde `EJECUTADA`; transaccional (todo o nada).
- RN-010.2 Anti-auto-reversión: el sujeto no revierte su propia baja (D-13).
- RN-010.3 Motivo de reversión obligatorio (≤ 2000).
- RN-010.4 La solicitud revertida **conserva** todos sus datos históricos (categoría, motivo, fechas, autorizaciones) — la reversión no borra el registro, cambia su estado.
- RN-010.5 Tras revertir, el empleado vuelve a ser elegible para una **nueva** solicitud de retiro.

**Criterios de aceptación:**
- Tras revertir: empleado activo con su estado laboral previo, antigüedad **continua** (sin corte — a diferencia de la recontratación, que resetea con nueva fecha de ingreso), plazas/contratos reabiertos, login restaurado, entrevista archivada (excluida de la tabulación), acciones `BAJA` y `REVERSION_BAJA` visibles en el journal.

**Prioridad:** Alta
**Dependencias:** RF-006 (snapshot); RF-014.

### RF-011 — Efectos de la reversión sobre la entrevista

**Descripción:**
Al revertir (o anular una `AUTORIZADA` — RN-005.3), las submissions de entrevista del empleado asociadas a ese retiro (borrador o enviadas) se **archivan** (D-09), reutilizando el mecanismo existente (`ArchiveSubmissionsForFileAsync`, `ExitInterviewRepository.cs:447-454`). No se borran (precedente D-12 de entrevista) ni cuentan en la tabulación de rotación.

**Reglas de negocio:**
- RN-011.1 El archivado es parte de la misma transacción de reversión.
- RN-011.2 No existe des-archivado de submissions (verificado: solo `Archive()`, `ExitInterviewSubmission.cs:156-160`); revertir una reversión = registrar una nueva solicitud (la entrevista se vuelve a llenar).

**Criterios de aceptación:**
- La bandeja global de entrevistas (que excluye archivadas) deja de mostrar la submission tras la reversión.

**Prioridad:** Alta
**Dependencias:** RF-010; módulo de entrevista.

### RF-012 — Bloqueos y ventanas de reversión

**Descripción:**
Reglas duras que protegen la línea de tiempo laboral:

**Reglas de negocio:**
- RN-012.1 **Recontratación posterior bloquea la reversión** (D-10): si tras la ejecución hubo rehire (perfil con `HireDate` posterior o acción `RECONTRATACION` posterior a la ejecución), 422 con código dedicado.
- RN-012.2 **Divergencia de estado bloquea:** si el perfil ya no coincide con lo que la ejecución dejó (p. ej. `RetirementDate` distinto del de la solicitud), la reversión automática se rechaza (evita restaurar sobre datos manipulados por otra vía).
- RN-012.3 Solo se revierte el retiro **más reciente** ejecutado del empleado.
- RN-012.4 **Ventana máxima de reversión: 30 días calendario desde la ejecución** (`FechaEjecucionUtc`) — ratificada por el negocio (2026-07-04; la v1 proponía sin límite). Vencida la ventana, la reversión se rechaza (422 con código dedicado); la vía para reincorporar al empleado después es la **recontratación** (nuevo periodo laboral).

**Criterios de aceptación:**
- Los cuatro bloqueos (recontratación posterior, divergencia de estado, no-más-reciente, ventana de 30 días vencida) retornan 422 con códigos bilingües distintos y mensaje accionable.

**Prioridad:** Alta
**Dependencias:** RF-010; módulo de recontratación (solo lectura de señales).

### Grupo D — Transversales

### RF-013 — Catálogo de estados de solicitud de retiro

**Descripción:**
Nuevo catálogo país-scoped `retirement-request-statuses` (D-16) con seed SV: `SOLICITADA`, `AUTORIZADA`, `RECHAZADA`, `ANULADA`, `EJECUTADA`, `REVERTIDA`. Modelo híbrido: constantes canónicas en dominio (máquina de estados) + catálogo para visualización/filtrado (patrón `EconomicAidRequestStatuses`, `PersonnelFileEmployee.cs:1620-1635`).

**Reglas de negocio:**
- RN-013.1 Seed vía `GlobalCatalogSeedData` + `HasData` en migración (todos los ambientes — convención verificada, no DevSeed).
- RN-013.2 Endpoint de lectura por key en el controlador general de catálogos.

**Prioridad:** Alta
**Dependencias:** ninguna.

### RF-014 — Permisos dedicados y política de acceso

**Descripción:**
Cuatro permisos nuevos (D-12): `PersonnelFiles.ViewRetirements`, `PersonnelFiles.ManageRetirements`, `PersonnelFiles.AuthorizeRetirement`, `PersonnelFiles.RevertRetirement`, sembrados en el catálogo de permisos (`ProvisioningConstants`) y con controlador(es) dedicado(s) — recordar que `AuthorizationPolicySet` es de clase, no de método, por lo que las acciones con política distinta van en controlador propio (convención verificada).

**Reglas de negocio:**
- RN-014.1 `AuthorizeRetirement` y `RevertRetirement` **no** son implicados por `PersonnelFiles.Admin` (espejo de `AuthorizeRehire`).
- RN-014.2 Sin autoservicio de escritura en Fase 1 (D-03); las políticas de vista/gestión siguen el patrón vista-o-gestión de la casa.

**Prioridad:** Alta
**Dependencias:** aprovisionamiento IAM.

### RF-015 — Puerta única de baja (cierre de la vía legada)

**Descripción:**
Retirar del contrato `PUT .../employment-information` los campos `RetirementCategoryCode`, `RetirementReasonCode`, `RetirementNotes`, `RetirementDate` (D-01) y rechazar el valor `RETIRADO` en `EmploymentStatusCode` manual (reservado a la ejecución del módulo). Regenerar `openapi.yaml` y documentar el breaking change en la guía frontend.

**Reglas de negocio:**
- RN-015.1 El `PATCH` raíz de expediente (isActive/bloqueo de rehire) se conserva como capacidad administrativa general, pero la baja formal siempre pasa por el módulo (la señal canónica sigue siendo `RetirementDate`, que ya solo escribe el módulo).
- RN-015.2 El frontend cambia en el mismo release — **sin fallbacks ni doble contrato transitorio** (D-01 ratificada); los campos se retiran de una sola vez (precedente: D-11 de entrevista eliminó los motivos legados).
- RN-015.3 **Limpieza de datos legados (D-08 ratificada):** los retiros de prueba registrados por la vía legada se **eliminan** en una tarea única de despliegue; tras ella, todo `RetirementDate` no nulo proviene de la ejecución del módulo.

**Prioridad:** Alta
**Dependencias:** RF-006 operativo; guía de integración frontend.

### RF-016 — Endurecimiento de validaciones de fechas de retiro

**Descripción:**
Cerrar los vacíos verificados: `FechaRetiro ≥ HireDate` (hoy no se valida en el perfil — sí en empleos anteriores, `PersonnelFile.cs:2211-2213`), `FechaSolicitud ≤ hoy`, coherencia `RetirementDate` ↔ `EmploymentStatusCode=RETIRADO` garantizada por construcción (solo escribe la ejecución orquestada).

**Prioridad:** Alta
**Dependencias:** RF-001, RF-006.

## 7. Requerimientos no funcionales

- **Seguridad:** multi-tenant estricto (todas las entidades `TenantEntity`); RBAC con los 4 permisos nuevos; anti-auto-aprobación/ejecución/reversión (403 dedicado); la desactivación/reactivación de login reutiliza los comandos IAM existentes con su auditoría.
- **Auditoría:** toda transición registra usuario, fecha y antes/después (patrón `PersonnelFileEmployeeAudits` con doble `SaveChanges` en transacción); acciones `BAJA`/`REVERSION_BAJA` en el journal append-only.
- **Concurrencia:** optimista con `If-Match`/`ETag` en toda mutación (400 sin header, 409 obsoleto — convención de la casa); ejecución y reversión transaccionales (todo o nada).
- **Rendimiento:** bandeja paginada con rate-limit de búsqueda (`PersonnelFileRateLimitPolicies.Search`); export con límite síncrono (413 al exceder).
- **Usabilidad (API):** errores 422/403 con `extensions.code` bilingüe (recursos EN/ES obligatorios — test de localización los exige); enums como strings; `Guid XxxId` serializa como `xxxPublicId` en el wire.
- **Compatibilidad:** `openapi.yaml` regenerado sin drift; DTOs de PUT/PATCH implementan `ISupportsAllowedActions` si el controlador opta por `[ResourceActions]` (lo exige el test de integración de cobertura de AllowedActions).
- **Mantenibilidad:** reglas de elegibilidad/transición en **módulo de reglas puro** testeable sin infraestructura (patrón `RehireEligibilityRules`); cobertura unitaria + integración (suite verde como criterio de salida).
- **Disponibilidad/escalabilidad:** sin componentes nuevos de infraestructura (sin scheduler, sin colas) en Fase 1.

## 8. Historias de usuario

### HU-001 — Registrar la solicitud de retiro
Como **gestor de RRHH**, quiero **registrar la solicitud de retiro de un empleado con solicitante, fechas, categoría, motivo y observación**, para **iniciar formalmente el proceso de baja con trazabilidad completa**.
**Criterios de aceptación:**
- Dado un empleado activo sin retiro vigente ni solicitud abierta, cuando registro la solicitud con datos válidos, entonces queda en `SOLICITADA` y aparece en la bandeja.
- Dado un empleado ya retirado o con solicitud abierta, cuando intento registrar, entonces recibo un error 422 explicativo.
- Dado un motivo que no pertenece a la categoría elegida, cuando registro, entonces recibo el error de coherencia de catálogo.

### HU-002 — Consultar la bandeja de retiros
Como **gestor de RRHH**, quiero **ver todas las solicitudes con filtros por estado, motivo y fechas, y exportarlas**, para **dar seguimiento al proceso y reportar**.
**Criterios de aceptación:**
- Dado que existen solicitudes en varios estados, cuando filtro por estado o periodo, entonces veo solo las coincidentes con contadores por estado.
- Cuando exporto, entonces descargo el mismo conjunto filtrado en xlsx/csv/json.

### HU-003 — Autorizar una solicitud
Como **autorizador de retiros**, quiero **autorizar una solicitud pendiente**, para **habilitar la ejecución de la baja y la entrevista de retiro**.
**Criterios de aceptación:**
- Dada una `SOLICITADA`, cuando autorizo, entonces pasa a `AUTORIZADA` y el empleado aparece en la bandeja de entrevistas.
- Dado que soy el empleado sujeto de la solicitud, cuando intento autorizarla, entonces recibo 403 (anti-auto-aprobación).
- Dado que figuro como solicitante de la solicitud, cuando intento autorizarla, entonces recibo 403 (separación de funciones: solicitante ≠ autorizador — D-13).

### HU-004 — Rechazar una solicitud
Como **autorizador de retiros**, quiero **rechazar una solicitud con una nota obligatoria**, para **dejar constancia de por qué no procede la baja**.
**Criterios de aceptación:**
- Dada una `SOLICITADA`, cuando rechazo con nota, entonces pasa a `RECHAZADA` (terminal) y se puede registrar una nueva solicitud después.
- Cuando rechazo sin nota, entonces recibo un error de validación.

### HU-005 — Anular una solicitud
Como **gestor o autorizador**, quiero **anular una solicitud antes de ejecutarla**, para **cancelar procesos que ya no aplican**.
**Criterios de aceptación:**
- Dada una `SOLICITADA` (gestor) o `AUTORIZADA` (autorizador), cuando anulo, entonces pasa a `ANULADA` y sale de la bandeja de entrevistas.
- Dada una `EJECUTADA`, cuando intento anular, entonces el sistema me indica que corresponde una reversión.

### HU-006 — Ejecutar el retiro
Como **gestor de RRHH**, quiero **ejecutar una baja autorizada cuando llega la fecha de retiro**, para **que el sistema aplique todos los efectos de una sola vez** (perfil, estado, plazas, contratos, login, journal).
**Criterios de aceptación:**
- Dada una `AUTORIZADA` con `FechaRetiro ≤ hoy`, cuando ejecuto, entonces el perfil queda con los datos de retiro, el expediente inactivo, las plazas y contratos cerrados a la fecha, el login desactivado y se journalea la acción `BAJA`.
- Dada una `AUTORIZADA` con fecha futura, cuando intento ejecutar, entonces recibo 422 indicando la ventana.

### HU-007 — Ver la bandeja de empleados autorizados para entrevista
Como **entrevistador de RRHH**, quiero **ver el listado de empleados con retiro autorizado y el estado de su entrevista**, para **saber a quién debo entrevistar antes de su salida**.
**Criterios de aceptación:**
- Dado un retiro autorizado, cuando abro la bandeja, entonces veo al empleado con su motivo, fecha de retiro y estado de entrevista (sin formulario / pendiente / borrador / enviada).
- Dado un retiro revertido o anulado, entonces el empleado ya no aparece.

### HU-008 — Capturar la entrevista de un empleado autorizado
Como **entrevistador de RRHH**, quiero **elegir al empleado desde la bandeja, visualizar su formulario e ingresar las respuestas que me aporta**, para **documentar las causas de su salida**.
**Criterios de aceptación:**
- Dado un empleado con retiro `AUTORIZADA` y formulario activo para su motivo, cuando abro su entrevista, entonces veo el formulario correcto aunque el perfil aún no tenga datos de retiro.
- Cuando guardo como borrador o envío, entonces aplican las reglas existentes del módulo de entrevista (requeridos, score, anonimato, inmutabilidad tras envío).

### HU-009 — Llenar mi propia entrevista (existente, punto de entrada nuevo)
Como **empleado con retiro autorizado**, quiero **llenar mi entrevista de retiro por autoservicio antes de mi salida**, para **dejar mi retroalimentación de forma voluntaria** (opcional — D-05 de entrevista).
**Criterios de aceptación:**
- Dado mi retiro autorizado y un formulario activo para el motivo, cuando accedo a mi entrevista, entonces puedo llenarla y enviarla mientras mi usuario siga activo (antes de la ejecución).

### HU-010 — Revertir un retiro ejecutado
Como **usuario con permiso de reversión**, quiero **revertir una baja ejecutada indicando el motivo**, para **restaurar íntegramente al empleado cuando la baja fue un error o se rescindió**.
**Criterios de aceptación:**
- Dada una `EJECUTADA` sin recontratación posterior, cuando revierto con motivo, entonces el empleado vuelve a estar activo con su estado laboral previo, sus plazas y contratos reabiertos, su login restaurado, la entrevista archivada y la solicitud en `REVERTIDA`.
- Dado que hubo una recontratación posterior, cuando intento revertir, entonces recibo 422 con el bloqueo explicado.
- Dado que pasaron más de 30 días desde la ejecución, cuando intento revertir, entonces recibo 422 de ventana vencida.
- Cuando revierto sin motivo, entonces recibo un error de validación.

### HU-011 — Trazabilidad del proceso
Como **gestor de RRHH**, quiero **ver en el detalle de la solicitud la línea de tiempo completa (quién solicitó, autorizó, ejecutó o revirtió y cuándo) y las acciones `BAJA`/`REVERSION_BAJA` en el journal del empleado**, para **auditar el proceso de principio a fin**.
**Criterios de aceptación:**
- Dada una solicitud con historia, cuando abro su detalle, entonces veo cada transición con actor, fecha y notas; y el journal de acciones del expediente refleja la baja y su eventual reversión.

## 9. Reglas de negocio (consolidadas)

1. **RN-01.** La baja formal se registra únicamente a través del módulo (puerta única — D-01); la señal canónica de retirado sigue siendo `RetirementDate` en el perfil, escrita solo por la ejecución.
2. **RN-02.** Empleado elegible para solicitud: `Employee` + `Completed`, activo, sin retiro vigente y sin otra solicitud abierta (`SOLICITADA`/`AUTORIZADA`).
3. **RN-03.** Categoría y motivo validados por código contra los catálogos país-scoped existentes; el motivo debe pertenecer a la categoría (jerarquía D-02 de entrevista).
4. **RN-04.** Máquina de estados: `SOLICITADA → AUTORIZADA → EJECUTADA → REVERTIDA`; salidas `RECHAZADA` (desde `SOLICITADA`, nota obligatoria) y `ANULADA` (desde `SOLICITADA`/`AUTORIZADA`). `RECHAZADA`, `ANULADA` y `REVERTIDA` son terminales; toda otra transición es inválida.
5. **RN-05.** Solo se edita en `SOLICITADA`.
6. **RN-06.** Separación de funciones: el empleado sujeto no autoriza, ejecuta ni revierte su propio retiro; y el **solicitante nunca autoriza** la solicitud que él mismo pidió (si el autorizador habitual es quien se retira, autoriza su superior) — D-13 ratificada.
7. **RN-07.** Ejecución solo con `FechaRetiro ≤ hoy`; transaccional; efectos: perfil (fechas/códigos/notas/`RETIRADO`), expediente inactivo, cierre de plazas y contratos a la fecha efectiva, desactivación de login, snapshot de reversión, acción `BAJA`.
8. **RN-08.** El retiro definitivo es del **empleado**: cierra **todas** sus plazas activas (multi-plaza incluido).
9. **RN-09.** La entrevista se habilita desde `AUTORIZADA` (motivo resuelto desde la solicitud — única fuente, sin fallback al perfil); sigue siendo opcional y nunca bloquea la ejecución.
10. **RN-10.** Reversión solo de `EJECUTADA`, con motivo obligatorio, restaurando desde el snapshot; conserva el registro histórico (no borra).
11. **RN-11.** Reversión bloqueada si: hubo recontratación posterior, el estado del perfil divergió de lo ejecutado, no es el retiro más reciente, o **pasaron más de 30 días desde la ejecución** (ventana ratificada — RN-012.4).
12. **RN-12.** Al revertir (o anular una autorizada), las submissions de entrevista asociadas se archivan — no cuentan para la tabulación de rotación.
13. **RN-13.** Tras reversión, la antigüedad es **continua** (sin corte); tras recontratación, se reinicia (distinción clave entre ambos conceptos).
14. **RN-14.** Fechas: `FechaSolicitud ≤ hoy`; `FechaRetiro ≥ HireDate`; retroactiva o futura permitida en el registro, pero la ejecución espera a la fecha.
15. **RN-15.** Un código de catálogo en uso no se borra: se inactiva (convención existente).
16. **RN-16.** Toda mutación exige `If-Match` y refresca el token; toda transición se audita con actor y fecha.

## 10. Flujos principales

### Flujo 1 — Registro, autorización y ejecución de la baja
1. El gestor de RRHH abre "Retiros" y registra la solicitud (solicitante, empleado, fechas, categoría, motivo, observación).
2. El sistema valida elegibilidad, catálogos y fechas; crea la solicitud en `SOLICITADA`.
3. El autorizador revisa la bandeja (filtro `SOLICITADA`), abre el detalle y **autoriza** (o rechaza con nota).
4. La solicitud pasa a `AUTORIZADA`; el empleado aparece en la bandeja de entrevistas.
5. Llegada la `FechaRetiro`, el gestor **ejecuta el retiro**.
6. El sistema aplica transaccionalmente: perfil con datos de retiro + `RETIRADO`, expediente inactivo, plazas y contratos cerrados a la fecha, login desactivado, snapshot guardado, acción `BAJA` journaleada; solicitud en `EJECUTADA`.
7. El sistema muestra la confirmación con el resumen de efectos aplicados.

### Flujo 2 — Entrevista de retiro desde la bandeja de autorizados
1. El entrevistador abre la bandeja de entrevistas (empleados con retiro autorizado/ejecutado y estado de entrevista).
2. Elige al empleado; el sistema resuelve el formulario activo para su motivo (existente, D-03 de entrevista) y muestra la entrevista.
3. El entrevistador ingresa las respuestas aportadas por el empleado (o el empleado la llena por autoservicio antes de su salida).
4. Se guarda como borrador o se envía; al enviar, el sistema valida requeridos, calcula el score ponderado y la marca inmutable.
5. La bandeja actualiza el estado de entrevista del empleado (`ENVIADA`).

### Flujo 3 — Reversión de retiro
1. El usuario con permiso de reversión abre el detalle de la solicitud `EJECUTADA`.
2. Selecciona "Revertir retiro" e ingresa el **motivo de reversión** (obligatorio).
3. El sistema verifica los bloqueos (sin recontratación posterior, estado no divergente, retiro más reciente, dentro de la ventana de 30 días desde la ejecución).
4. El sistema restaura transaccionalmente: perfil limpio con estado laboral previo, expediente activo, plazas/contratos reabiertos, login reactivado (si procedía), entrevista archivada, acción `REVERSION_BAJA`.
5. La solicitud pasa a `REVERTIDA` conservando toda su historia; el sistema confirma con el resumen de estados restaurados.

## 11. Flujos alternativos y excepciones

| # | Escenario | Comportamiento esperado |
|---|---|---|
| E-01 | Registrar solicitud para empleado ya retirado, en borrador o candidato | 422 con código de elegibilidad. |
| E-02 | Segunda solicitud abierta para el mismo empleado | 422 (única solicitud abierta). |
| E-03 | Motivo no pertenece a la categoría / código inactivo | 422 de coherencia de catálogo (validación existente reutilizada). |
| E-04 | `FechaRetiro` anterior al ingreso, o `FechaSolicitud` futura | 422 de coherencia de fechas. |
| E-05 | Autorizar/ejecutar/revertir siendo el empleado sujeto, o autorizar siendo el solicitante | 403 de separación de funciones (códigos dedicados). |
| E-06 | Ejecutar con `FechaRetiro` futura | 422 indicando que debe esperarse la fecha (o corregirse la solicitud). |
| E-07 | Ejecutar cuando el perfil divergió (retirado por otra vía) | 422 de conflicto de estado; resolver manualmente antes. |
| E-08 | Editar una solicitud `AUTORIZADA` | 422; el flujo correcto es anular y re-registrar (o rechazar). |
| E-09 | Anular una `EJECUTADA` | 422 señalando que corresponde reversión. |
| E-10 | Revertir con recontratación posterior | 422 bloqueo D-10. |
| E-11 | Revertir sin motivo | 400/422 de validación (motivo obligatorio). |
| E-12 | Abrir entrevista sin formulario activo para el motivo | La bandeja marca `SIN_FORMULARIO`; el guardado retorna el 422 existente ("no hay formulario configurado"). |
| E-13 | Enviar entrevista ya enviada | 422 existente `SubmissionAlreadySubmitted` (inmutable). |
| E-14 | Mutación sin `If-Match` / token obsoleto | 400 / 409 (convención de la casa). |
| E-15 | Usuario sin permiso (vista, gestión, autorización o reversión) | 403; `Admin` no implica autorizar ni revertir. |
| E-16 | Reversión cuando el login fue desactivado manualmente antes de la baja | Se restaura el estado **previo a la ejecución** (snapshot): permanece desactivado — no se "activa de más". |
| E-17 | Revertir pasados más de 30 días desde la ejecución (ventana vencida — RN-012.4) | 422 con código dedicado; la vía de reincorporación posterior es la **recontratación** (nuevo periodo). |

## 12. Datos requeridos

### Entidad: Solicitud de Retiro (`PersonnelFileRetirementRequest` — nueva)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Auto | Único | Identificador público (wire: `publicId`) |
| Empleado (PersonnelFileId) | Referencia expediente | Sí | Employee+Completed, activo, sin retiro vigente ni solicitud abierta | Empleado a retirar |
| Solicitante (SolicitanteFileId) | Referencia expediente | Sí | Expediente válido de la empresa; puede ser el mismo empleado | Quien pide el retiro (D-02) |
| SolicitanteNombreSnapshot | Texto | Auto | — | Nombre del solicitante al momento del registro |
| FechaSolicitud | Fecha | Sí | ≤ hoy | Fecha de la petición |
| FechaRetiro | Fecha | Sí | ≥ HireDate del empleado | Fecha efectiva de la baja (pasada o futura) |
| RetirementCategoryCode | Código catálogo | Sí | Activo en catálogo del país | Categoría de retiro |
| RetirementReasonCode | Código catálogo | Sí | Activo + pertenece a la categoría | Motivo de retiro |
| Observacion | Texto (≤2000) | No | — | Observación; se copia a `RetirementNotes` al ejecutar |
| RequestStatusCode | Código | Auto | Canónico + catálogo (RF-013) | Estado del ciclo |
| RequestedByUserId | GUID | Auto | — | Usuario que registró (auditoría) |
| ResolvedByUserId / ResolutionDateUtc / ResolutionNotes | GUID/Fecha/Texto | Auto | Nota obligatoria en rechazo | Resolución (autorización o rechazo) |
| EjecutadoPorUserId / FechaEjecucionUtc | GUID/Fecha | Auto | — | Ejecución |
| SnapshotEjecucion (estado laboral previo, login activo previo, IDs de asignaciones/contratos cerrados, bloqueo rehire previo) | Interno | Auto | — | Base de la reversión (D-11) |
| RevertidoPorUserId / FechaReversionUtc / MotivoReversion | GUID/Fecha/Texto (≤2000) | Auto / motivo Sí al revertir | — | Reversión |
| IsActive | Booleano | Auto | — | Soft-delete (conserva historia) |
| ConcurrencyToken | GUID | Auto | If-Match | Concurrencia optimista |

### Entidad: Catálogo de estados de solicitud (`retirement-request-statuses` — nueva, país-scoped)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| CountryCode / Code / Name / SortOrder / IsActive | Convención catálogos | Sí | Código único por país | Seed SV: `SOLICITADA`, `AUTORIZADA`, `RECHAZADA`, `ANULADA`, `EJECUTADA`, `REVERTIDA` |

### Entidades existentes afectadas (sin campos nuevos, salvo indicación)

| Entidad | Efecto en ejecución | Efecto en reversión |
|---|---|---|
| `PersonnelFileEmployeeProfile` (`PersonnelFileEmployee.cs:49-55`) | `RetirementDate/Category/Reason/Notes` + `EmploymentStatusCode=RETIRADO` | Limpieza de los 4 campos + restauración del estado previo |
| `PersonnelFile` | `IsActive=false` (+ `IsRehireBlocked` opcional — D-18) | `IsActive=true` + bloqueo previo restaurado |
| `PersonnelFileEmploymentAssignment` / `PersonnelFileContractHistory` | Cierre a `FechaRetiro` (D-06) | Reapertura exacta de las filas cerradas (snapshot) |
| Usuario de login (IAM) | Desactivación | Reactivación si estaba activo antes |
| `ExitInterviewSubmission` | — (la entrevista se habilita desde `AUTORIZADA`) | Archivado (D-09) |
| `PersonnelFilePersonnelAction` | + acción `BAJA` (`APLICADA`, sistema) | + acción `REVERSION_BAJA` |
| Catálogo `ActionType` | +2 seeds: `BAJA`, `REVERSION_BAJA` (D-15) | — |

## 13. Integraciones necesarias

**Externas:** ninguna en Fase 1.

**Internas (puntos de contacto):**
- **Módulo de entrevista de retiro** — adaptación del gate (RF-009) y archivado en reversión/anulación (RF-011); resolución de formulario por motivo sin cambios.
- **Recontratación** — solo lectura de señales para el bloqueo D-10; la elegibilidad de rehire (`RetirementDate != null`) queda naturalmente satisfecha por la ejecución del módulo. Tras una reversión el empleado ya no es "retirado", por lo que no es recontratable (correcto).
- **IAM / Company Users** — reutilizar los comandos de desactivación/reactivación de usuario dentro de la transacción de ejecución/reversión.
- **Plazas y contratos** — reutilizar los cierres existentes (`CloseActiveEmploymentAssignmentsAsync` / `CloseActiveContractHistoriesAsync`, hoy solo invocados por rehire) parametrizados a `FechaRetiro`; construir la reapertura (nueva).
- **Acciones de personal** — journaling automático (RF-007).
- **Reporting/exportación** — bandeja + export vía `ReportExportDeliveryService` (patrón constancias).
- **Auditoría** — patrón existente de auditoría de expediente.
- **(Futuro)** módulo de nómina/liquidación — punto de integración declarado en la ejecución y la reversión (D-14).

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Gestor de RRHH (retiros) | `ViewRetirements`, `ManageRetirements` (registrar, editar, anular `SOLICITADA`, ejecutar) | No autoriza, no rechaza, no revierte; anti-auto-gestión si es el sujeto |
| Autorizador de retiros | `ViewRetirements`, `AuthorizeRetirement` (autorizar, rechazar, anular `AUTORIZADA`) | No implicado por `PersonnelFiles.Admin`; anti-auto-aprobación; **no autoriza solicitudes donde figura como solicitante** (D-13) |
| Revertidor | `ViewRetirements`, `RevertRetirement` | No implicado por `Admin`; motivo obligatorio; bloqueos RN-11 (incl. ventana de 30 días) |
| Entrevistador RRHH | `ViewExitInterviews`, `ManageExitInterviews` (existentes) + bandeja RF-008 | Lectura de respuestas solo RRHH (D-14 de entrevista) |
| Empleado | Autoservicio de su entrevista (existente, opcional) | Sin acceso al módulo de retiros en Fase 1 (D-03); no ve la bandeja |
| Administrador de expedientes (`PersonnelFiles.Admin`) | Hereda vista/gestión | **No** hereda autorizar ni revertir (D-12) |

## 15. Criterios de aceptación generales

1. Compilación de la solución sin errores; suites **unitaria e integración verdes** (incluida la prueba de cobertura de `AllowedActions` y la de localización de mensajes).
2. Migraciones aplicadas **sin drift** de modelo; seeds SV verificados (estados de solicitud, tipos de acción `BAJA`/`REVERSION_BAJA`).
3. Los 4 permisos aprovisionados y visibles en el catálogo de permisos; políticas verificadas (Admin no implica autorizar/revertir).
4. Flujo completo demostrable: registro → autorización → bandeja de entrevistas → captura de entrevista → ejecución → reversión → estados restaurados (verificación end-to-end).
5. La ejecución deja el sistema coherente: perfil, estado laboral, expediente, plazas, contratos, login y journal en un solo commit.
6. La reversión restaura exactamente los estados del snapshot y archiva la entrevista; los cuatro bloqueos (RN-11, incluida la ventana de 30 días) probados.
7. Puerta legada cerrada **sin fallbacks** (D-01): el `PUT employment-information` ya no acepta campos de retiro; `openapi.yaml` regenerado; guía de integración frontend publicada (`docs/technical/guia-integracion-frontend-retiro-definitivo.md`); datos legados de prueba eliminados (RN-015.3).
8. Errores bilingües (EN/ES) para todos los códigos nuevos; auditoría verificable de cada transición.
9. Los flujos existentes de recontratación y entrevista siguen funcionando sin regresión.

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **R-01 — Doble puerta transitoria:** si el frontend siguiera enviando campos de retiro al PUT legado, habría bajas invisibles para la bandeja. Mitigación (ratificada D-01): RF-015 en el mismo release, **sin fallback ni contrato transitorio**, + guía FE.
- **R-02 — Expectativa de "revertir la liquidación":** el requerimiento la menciona, pero **no existe módulo de liquidación** que revertir. **Mitigado — D-14 ratificada explícitamente (2026-07-04):** la liquidación se gestiona fuera del sistema; la reversión Fase 1 restaura estados del sistema, no pagos.
- **R-03 — Deriva de estado entre ejecución y reversión:** cambios manuales intermedios (PATCH raíz, IAM) pueden invalidar el snapshot. Mitigación: bloqueo por divergencia (RN-11) + puerta única.
- **R-04 — Catálogos de motivo sin CRUD admin:** siguen siendo seed-only (RF-015 de entrevista diferido); si el negocio necesita motivos propios, hay que priorizar ese CRUD (Fase 2).
- **R-05 — Sin notificaciones:** el autorizador depende de revisar la bandeja; riesgo de bajas demoradas. Mitigación: filtros/contadores por estado; notificaciones en Fase 2.
- **R-06 — Semántica de cierre de plaza:** cerrar asignaciones en la ejecución interactúa con el módulo multi-plaza y sus reportes de ocupación; coordinar validaciones (p. ej. plaza liberada visible para nueva asignación).
- **R-07 — Cambio de contrato (breaking):** retirar campos del PUT afecta pantallas actuales de "información de empleo"; coordinar versión FE/BE.
- **R-08 — Reapertura de contratos:** "reabrir" un contrato cerrado (limpiar fecha fin) debe respetar las reglas del módulo de contratos; diseñar la reapertura como operación de dominio explícita, no un update genérico.

### Supuestos

- **S-01** No hay datos productivos reales de bajas; los retiros legados en el perfil son datos de prueba (precedente D-11 de entrevista). Por eso la reversión Fase 1 cubre solo retiros del módulo (D-08). **Ratificado: esos datos de prueba se eliminan** en la limpieza de despliegue (RN-015.3).
- **S-02** Los catálogos existentes (8 categorías / 23 motivos SV, `SeparationType`) cubren la necesidad; no se re-modelan.
- **S-03** La entrevista permanece **opcional** (D-05 de entrevista): la bandeja de autorizados no introduce obligatoriedad.
- **S-04** El Salvador es el primer país; el diseño multi-país queda resuelto por los catálogos país-scoped.
- **S-05** La baja es del empleado respecto de la institución (cierra todas las plazas); no existen bajas parciales en este módulo.
- **S-06** Un solo nivel de autorización es suficiente en Fase 1 (sin flujo multinivel).

### Dependencias

- Catálogos `RetirementCategory`/`RetirementReason` (existentes — entrevista Fase 1).
- Módulo de entrevista de retiro (existente; ajuste puntual del gate RF-009).
- Módulo de recontratación (señales de bloqueo D-10; sin cambios funcionales).
- IAM/Company Users (desactivación/reactivación de login) y aprovisionamiento de permisos.
- Infraestructura de reporting/export existente.
- **(Futura)** Módulo de nómina/liquidación para la integración D-14.

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-04)

Las decisiones **D-01…D-19** fueron **ratificadas por el negocio el 2026-07-04** (lo no modificado quedó según la recomendación del analista — tabla al inicio). Respuestas a las diez preguntas planteadas en la v1:

1. **(D-14) Liquidación** — **Confirmado:** la liquidación se gestiona administrativamente fuera del sistema; la reversión Fase 1 restaura solo estados del sistema y el punto de integración queda declarado para el futuro módulo de nómina.
2. **(D-01) Puerta única** — **Sí:** se cierra la vía legada. El frontend cambia en el mismo release; **sin fallbacks ni contrato legado**.
3. **(D-06) Efectos de la ejecución** — **Sí a ambos:** cierre de plazas/contratos a la fecha efectiva y desactivación automática del login; ambos quedan en el snapshot de reversión.
4. **(D-05) Ejecución** — **Manual** cuando llega la fecha (`FechaRetiro ≤ hoy`); sin scheduler en Fase 1; bajas retroactivas permitidas.
5. **(D-07) Entrevista** — Se habilita **desde la autorización** (y sigue disponible tras la ejecución).
6. **Ventana de reversión** — **Máximo 30 días calendario desde la ejecución** (la v1 proponía sin límite; RN-012.4 actualizada). Vencida la ventana, la vía de reincorporación es la recontratación.
7. **Notas** — El **rechazo requiere siempre nota**; la **autorización admite nota opcional** (como propuesto).
8. **Roles y separación de funciones** — Se exige **solicitante ≠ autorizador** además del anti-auto-aprobación del sujeto, con el **permiso dedicado** `AuthorizeRetirement`; si el autorizador habitual es quien se retira, autoriza su superior (D-13 endurecida).
9. **Adjuntos** — **Fase 2** (D-19 confirmada).
10. **Bandeja de retiros** — **Exclusiva de RRHH** (sin vista para jefaturas en Fase 1), coherente con D-14 de entrevista.

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar D-14 (liquidación) antes que nada — ✅ hecho (2026-07-04).** El negocio confirmó que la liquidación se gestiona fuera del sistema y su reversión queda como integración futura; la única promesa del requerimiento que el sistema no podía cumplir queda alineada.
2. **Construir en dos olas dentro de Fase 1:**
   - **Ola 1 (MVP operativo):** RF-001…RF-009 + RF-013…RF-016 — registro, autorización, ejecución orquestada y bandeja de entrevistas. Con esto las tres pantallas del requerimiento quedan funcionales (la reversión aún no).
   - **Ola 2 (reversión):** RF-010…RF-012 — depende del snapshot introducido en la Ola 1 y es el componente de mayor riesgo técnico (reapertura de plazas/contratos); aislarlo reduce el riesgo del release.
3. **Reutilizar plantillas probadas:** entidad y ciclo calcados de `EconomicAidRequest` (estados híbridos, PATCH de acciones, anti-auto-aprobación) y bandeja/export de `CertificateRequest` — minimiza diseño nuevo y mantiene consistencia de API para el frontend.
4. **No reutilizar la recontratación para revertir.** Son conceptos opuestos en su efecto sobre la línea de tiempo (rehire = nuevo periodo con antigüedad reiniciada; reversión = continuidad como si la baja no hubiera existido). Compartir solo utilidades de bajo nivel (cierres/reaperturas), nunca el flujo.
5. **Cerrar la puerta legada en el mismo release (D-01/RF-015).** La experiencia del proyecto (doble vía de estados en formas de pago, motivos free-text de bajas) muestra que las dobles puertas generan datos inconsistentes que luego cuestan migraciones.
6. **Aprovechar el momento para los quick wins de integridad** (RF-016 fechas, D-15 acción `BAJA` + corrección de `COMPLETADA`): son baratos ahora y cierran vacíos reales verificados.
7. **Sinergia con el Tablero de Indicadores RRHH:** la entidad de solicitud da, por primera vez, datos confiables de bajas con motivo/categoría/`SeparationType` para el indicador de rotación (hoy derivado solo de `RetirementDate`); considerar este módulo como prerrequisito suave de esos indicadores.
8. **Siguiente paso:** con D-01…D-19 **ratificadas (2026-07-04)**, elaborar el plan técnico (`docs/technical/plan-tecnico-retiro-definitivo.md`) con el desglose en PRs, siguiendo el patrón de los planes hermanos e incorporando los endurecimientos de la ratificación (sin fallbacks ni legacy, limpieza de datos de prueba, solicitante ≠ autorizador, ventana de reversión de 30 días).
