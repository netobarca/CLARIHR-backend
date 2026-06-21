# Análisis de Negocio — Recontratación de Empleados

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Plazas (`PositionSlots`) · Provisión de Usuarios (Identity) · Catálogos internos (`InternalCatalogs`) |
| **Estado** | Definido — listo para diseño técnico e implementación |
| **Fecha** | 2026-06-20 |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN según el caso) |

---

## Contexto del cambio

El negocio necesita **recontratar** a empleados que **salieron de la compañía** —por **renuncia** o por **finalización de contrato**— y que vuelven a ser contratados, generando una **nueva contratación**.

Hoy en CLARIHR el ciclo de vida del empleado soporta el **alta** y el **retiro**, pero **no existe** ningún flujo de recontratación (reingreso/reincorporación). Se verificó en el código que **no hay** lógica de `recontratacion`, `reingreso`, `reincorporacion` ni `reactivar` para empleados (solo existe reactivación para *empresas/suscripciones*, que es ajena a este requerimiento).

Dos hechos del sistema condicionan el diseño:

1. **El retiro es un *soft-delete*.** Cuando un empleado se va, su expediente **se conserva**: se marca `PersonnelFileEmployeeProfile.IsEmploymentActive = false`, se llenan los campos de retiro (`RetirementDate`, `RetirementCategoryCode`, `RetirementReasonCode`, `RetirementNotes`) y normalmente se desactiva el expediente (`PersonnelFile.IsActive = false`). El registro y su historial permanecen.
2. **La cédula es única por empresa.** Existe el índice único `uq_personnel_file_identifications__tenant_type_number` sobre `TenantId + IdentificationType + NormalizedIdentificationNumber`, **no filtrado por estado**. Por lo tanto, **dentro del mismo tenant no se puede crear un expediente nuevo con la misma cédula** de un ex-empleado. Esto obliga a que la recontratación **reutilice el expediente existente**, no que cree uno nuevo.

**Decisión estructural (D-01):** la recontratación **reactiva el expediente existente** (lo localiza por documento, preserva su historial y abre un **nuevo período laboral**), **acotada al mismo tenant**. El mismo documento en **otro tenant** es un empleado/vínculo **independiente** (la persona puede estar contratada en varias empresas a la vez) y **no** constituye una recontratación.

### Estado actual verificado en el código (línea base)

| # | Tema | Hallazgo (línea base) | Implicación para recontratación |
|---|---|---|---|
| 1 | Identidad del empleado | `PersonnelFile` (raíz) + `PersonnelFileEmployeeProfile` (perfil laboral 1:1). | La recontratación opera sobre el **mismo** `PersonnelFile`. |
| 2 | Unicidad de documento | Índice único `(TenantId, IdentificationType, NormalizedIdentificationNumber)` en `PersonnelFileIdentification`, **sin** filtro por estado. | **No** se puede duplicar el expediente; se **reutiliza**. Validación por-tenant ⇒ multi-empresa OK. |
| 3 | Retiro | `IsEmploymentActive=false`, `RetirementDate/Category/Reason/Notes`, y `PersonnelFile.IsActive=false` vía `PATCH /personnel-files/{id}`. Registro conservado. | El estado de partida del recontratado es **retirado/inactivo**. |
| 4 | Plaza | Vive **solo** en `PersonnelFileEmploymentAssignment` (colección N, con `IsPrimary/IsActive`, fechas, `AssignmentTypeCode`), tras el rediseño de multi-plazas. `PositionSlot` valida cupo (`MaxEmployees`) y estado (Vacant/Occupied/Suspended). | El nuevo período **reutiliza** las reglas de asignación multi-plaza ya existentes. |
| 5 | Contrato | `PersonnelFileContractHistory` (tipo, fechas, plaza, `IsActive`, notas) + campos de contrato en el perfil (`ContractTypeCode`, `ContractStartDate/EndDate`). | El nuevo período genera **nuevo contrato**; el anterior queda en historial. |
| 6 | Ciclo de vida | `PersonnelFileLifecycleStatus` = `Draft`/`Completed`. Alta vía `PATCH /personnel-files/{id}/finalize` (exige `RecordType=Employee`, email institucional único, `PositionSlot` con `RoleId`; **el finalize actual exige `LinkedUserPublicId == null`**). | La recontratación debe **reabrir** el expediente y **re-finalizar**; el finalize requiere ajuste (ver §16–17). |
| 7 | Auditoría | `PersonnelFilePersonnelAction` (log *append-only*: `actionTypeCode`, `actionStatusCode`, `actionDateUtc`, `effectiveFromUtc/To`, `description`, autor). | La recontratación se registra como **acción de personal**. |
| 8 | Catálogos | Motivos/categorías de retiro y tipos de contrato son **códigos de catálogo** (`InternalCatalogValue`), configurables por tenant. | La **elegibilidad** de recontratación se modela como **atributo de catálogo**. |

---

## Decisiones del negocio (resueltas) — referencia rápida para el equipo

| # | Pregunta | Decisión |
|---|---|---|
| D-01 | Modelo de re-ingreso | **Reactivar el expediente existente** (localizar por documento, preservar historial, abrir nuevo período). **Una sola ficha por persona y por tenant.** |
| D-02 | Multi-tenant | El mismo documento en **otro tenant** = empleado/vínculo **independiente**; **no es recontratación**. La búsqueda y la unicidad son **por-tenant**. |
| D-03 | Antigüedad / continuidad | **Nuevo período laboral, nueva fecha de ingreso.** Antigüedad, vacaciones y prestaciones **se reinician desde cero**. El período anterior queda como **referencia**. |
| D-04 | Elegibilidad | **Advertir pero permitir con autorización.** Si el expediente está **marcado "no recontratable"** (marca manual, D-11), el sistema **advierte** y exige **autorización** mediante un permiso facultado (D-10). |
| D-05 | Datos reutilizados | **Reutilizar todos los datos no laborales** (identificación, contacto, documentos, bancario, familia/beneficiarios), **pre-cargados y editables**. |
| D-06 | Liquidación previa | El **cálculo** de la liquidación está **fuera de alcance** (no hay módulo de nómina). La recontratación, no obstante, **exige** que el período anterior esté **cerrado/liquidado** (ver D-13). |
| D-07 | Recontratación masiva | **Fuera de alcance** (una recontratación a la vez). **No se prevé** versión por lote en fases futuras. |
| D-08 | Reapertura del expediente | Se usa la transición **`Completed → Draft`** (reabrir y re-finalizar). Implica **ajustar el `Finalize`** para admitir un expediente que **ya** tuvo usuario vinculado. |
| D-09 | Email institucional | Si el email anterior **no está en uso**, se **conserva**. Si **ya está en uso**, se **exige** capturar el **nuevo** email del recontratado. |
| D-10 | Autorización del override | **No existe** el permiso; se **crea uno nuevo** para autorizar la recontratación de "no recontratables". |
| D-11 | Definición de "no recontratable" | Por **marca manual** en el expediente (**no** por catálogo de motivo de retiro). |
| D-12 | Período mínimo de espera | **No** aplica; el sistema **no** valida tiempo mínimo entre retiro y recontratación. |
| D-13 | Recontratar antes de liquidar | **No se permite**. El período anterior debe estar **cerrado/liquidado** antes de recontratar. |
| D-14 | Historial de períodos | **Derivado** de registros existentes (`ContractHistory` + `PersonnelActions` + asignaciones con fechas). **Sin** entidad nueva. |
| D-15 | Evaluaciones/competencias previas | Se **archivan** (se conservan como histórico del período anterior; no continúan activas en el nuevo período). |
| D-16 | Plaza del nuevo período | Se **elige siempre** de forma explícita (puede ser la misma); el sistema **no** la propone por defecto. |
| D-17 | Cierre/liquidación (mecanismo) | **Confirmación manual** en el flujo de recontratación, **hasta** que exista un módulo de **nómina** o de **baja de personal** que provea la señal de cierre. |
| D-18 | Captura de "no recontratable" | Se fija **al retirar** al empleado. El permiso lo define la **matriz RBAC** por rol de cada empresa; el **owner** de la empresa puede hacerlo por defecto. |
| D-19 | Visibilidad de lo archivado | Las evaluaciones/competencias archivadas quedan **consultables como histórico** en la ficha del empleado. |

---

## 1. Resumen del producto o requerimiento

Se incorpora a CLARIHR el flujo de **Recontratación de Empleados**: la capacidad de **volver a vincular** a una persona que **ya trabajó** en la compañía y **se retiró** (por renuncia o por fin de contrato), generando una **nueva contratación** sobre su **expediente existente**.

Funcionalmente, recontratar significa: **localizar** al ex-empleado por su documento (dentro del tenant), **validar su elegibilidad**, **preservar** el historial de su período anterior, **reactivar** su expediente y **abrir un nuevo período laboral** —con **nueva fecha de ingreso**, nuevo contrato y nueva asignación de plaza— **reutilizando** sus datos personales no laborales y **re-provisionando** sus accesos.

**Problema que resuelve:** hoy no hay forma soportada de re-vincular a un ex-empleado. Recrearlo "desde cero" es **imposible** (la cédula es única por empresa) y, aunque fuera posible, **duplicaría** la ficha y **perdería** el historial. Este requerimiento ofrece un flujo **único, trazable y conforme** que respeta la integridad del modelo y la realidad legal (nuevo vínculo = nueva antigüedad).

## 2. Objetivos del negocio

1. **Re-vincular sin duplicar:** recontratar reutilizando el expediente existente, manteniendo **una sola ficha por persona y por tenant**.
2. **Preservar la historia laboral:** conservar de forma **inmutable** los períodos anteriores (fechas, motivo de retiro, contratos, plazas) para consulta y auditoría.
3. **Tratar el reingreso como nuevo vínculo:** **nueva fecha de ingreso**, con antigüedad y acumulados **desde cero** (el contrato previo ya se liquidó).
4. **Controlar la elegibilidad:** advertir y exigir **autorización** cuando el motivo de salida lo amerite (p. ej. "no recontratable"), sin bloquear casos legítimos.
5. **Acelerar el reingreso:** **reutilizar** los datos no laborales para minimizar recaptura y errores.
6. **Trazabilidad total:** registrar **quién** recontrató, **cuándo**, **con qué autorización** y **sobre qué período**.
7. **Reutilizar lo existente:** apoyarse en los flujos ya construidos (asignaciones multi-plaza, contratos, finalización, provisión de usuarios, catálogos) en lugar de crear mecanismos paralelos.

## 3. Alcance funcional

- **F1. Localización del ex-empleado** por tipo + número de documento, **dentro del tenant**, restringida a expedientes con empleo **inactivo/retirado**.
- **F2. Validación de elegibilidad** según el motivo de retiro previo y/o una marca "no recontratable", con **advertencia + autorización** del rol facultado.
- **F3. Preservación del período anterior** (cierre inmutable en el historial: fechas, motivo de retiro, contrato y asignaciones) antes de abrir el nuevo período.
- **F4. Reactivación del expediente** (`IsActive=true`) y **apertura de un nuevo período laboral** con **nueva fecha de ingreso** y estado de empleo activo.
- **F5. Reutilización editable de datos no laborales** (identificación, datos personales, contacto, direcciones, documentos, cuenta bancaria, familia/beneficiarios).
- **F6. Nuevo contrato y nueva asignación de plaza**, reutilizando las reglas de **multi-plaza** (cupo `maxEmployees`, estado de plaza, una principal, sin solapes en la misma plaza).
- **F7. Reinicio de acumulados** (antigüedad, vacaciones, prestaciones) desde la nueva fecha de ingreso.
- **F8. Re-provisión de la cuenta de usuario y accesos** (email institucional único, rol derivado de la plaza), reutilizando el flujo de **finalización**.
- **F9. Registro de auditoría** de la recontratación (acción de personal tipo `RECONTRATACION`, con autor y autorizador).
- **F10. Consulta del historial de períodos** del empleado (línea de tiempo de ingresos/retiros).
- **F11. Mensajería/errores bilingües** (ES/EN), siguiendo el catálogo de códigos existente y la concurrencia con `If-Match`.

## 4. Fuera de alcance (esta fase)

- **Cálculo de liquidación / prestaciones** del período anterior (D-06). *No existe módulo de nómina aún.* El **cierre/liquidación** del período previo es **precondición** de la recontratación (D-13), pero su **cálculo** no se aborda aquí.
- **Continuidad automática de antigüedad** o reconocimiento de tiempo previo para prestaciones (D-03 lo excluye explícitamente).
- **Recontratación masiva / por lote** (D-07) — **no prevista** en esta fase ni en fases futuras.
- **Transferencia o portabilidad cross-tenant** del empleado entre empresas (cada empresa es un vínculo independiente — D-02).
- **Recontratación de `Candidate`** (solo aplica a `RecordType = Employee`).
- **Motor de aprobaciones multi-paso / workflow** para la autorización (en esta fase es **una sola** autorización, no un flujo BPM).
- **Validación de período mínimo de espera** entre retiro y reingreso: **no se valida** (D-12).
- **Reactivación/recálculo de saldos de vacaciones** del período anterior (el nuevo período inicia en cero).

## 5. Actores o usuarios involucrados

| Actor | Rol en este requerimiento |
|---|---|
| **Analista / Gestor de RRHH** | Actor principal. Localiza al ex-empleado, ejecuta la recontratación, captura el nuevo período (contrato, plaza, datos). Requiere permiso de gestión de expedientes (`PersonnelFiles.Manage`). |
| **Aprobador / Jefe de RRHH** | Autoriza la recontratación cuando el ex-empleado es "no recontratable" o el motivo de salida es bloqueante (override controlado). Requiere un permiso de autorización facultado. |
| **Administrador de Plazas** | Define las `PositionSlot`, su `maxEmployees`, estado y rol (`PositionSlots.Manage`). Provee la plaza del nuevo período. |
| **Ex-empleado (sujeto)** | Persona recontratada. **No es usuario** del flujo; es el sujeto del expediente. Tras finalizar, vuelve a ser usuario del sistema con sus accesos. |
| **Sistema CLARIHR** | Valida invariantes (unicidad, elegibilidad, cupo), preserva historial, reactiva el expediente, re-provisiona accesos y registra auditoría. |
| **Auditor** | Consulta la trazabilidad: períodos, autorizaciones y acciones de personal. |
| **(Futuro) Nómina / Vacaciones** | Consumidores del nuevo período laboral para cálculos de antigüedad/acumulados. Fuera de alcance en esta fase. |

> La autorización se rige por la **matriz de permisos** existente del sistema (por módulo/pantalla). Se reutiliza `PersonnelFiles.Read/Manage`; se introduce un permiso de **autorización de recontratación** para el override (ver §14 y §17).

## 6. Requerimientos funcionales

### RF-001 — Localización de ex-empleado recontratable

**Descripción:**
Permitir buscar, por **tipo + número de documento** y **dentro del tenant**, el expediente de un ex-empleado cuyo empleo esté **inactivo/retirado**, para iniciar la recontratación reutilizando su ficha.

**Reglas de negocio:**
- Aplica solo a `RecordType = Employee` con `IsEmploymentActive = false` (RN-02).
- La búsqueda usa el documento **normalizado** y es **por-tenant** (RN-01, RN-03).
- Si el documento **no existe** en el tenant ⇒ no es recontratación, es **alta nueva** (flujo de contratación normal).
- Si el empleado está **activo** ⇒ no aplica recontratación (es edición del expediente vigente).

**Criterios de aceptación:**
- Dado un ex-empleado **retirado** en el tenant, cuando el usuario busca su documento, entonces el sistema muestra su expediente con un **resumen del período anterior** (fechas, motivo de retiro) y la acción **"Recontratar"**.
- Dado un documento **inexistente** en el tenant, cuando se busca, entonces el sistema ofrece **"crear nueva contratación"** (no recontratación).
- Dado un empleado **activo**, cuando se intenta recontratar, entonces el sistema indica que **ya está activo** y no habilita la recontratación.

**Prioridad:** Alta
**Dependencias:** Índice único de identificación; búsqueda de expedientes por tenant.

---

### RF-002 — Validación de elegibilidad y autorización

**Descripción:**
Antes de recontratar, evaluar si el ex-empleado es **"no recontratable"** (por el **motivo de retiro** previo o por una **marca explícita** del expediente). Si lo es, **advertir** y exigir **autorización** de un rol facultado para continuar.

**Reglas de negocio:**
- La elegibilidad se determina por una **marca manual** `RehireBlocked` en el expediente (con su motivo `RehireBlockedReason`), fijada **al retirar** al empleado; el permiso para fijarla lo define la **matriz RBAC** por rol (el **owner** puede por defecto). **No** se usa el catálogo de motivo de retiro para esto (RN-06, D-11, D-18).
- El **override** requiere un **permiso nuevo** de autorización de recontratación (D-10); sin él, la recontratación de un "no recontratable" **no continúa**.
- La autorización se **registra** (autorizador, fecha, justificación) y se vincula a la acción de personal (RN-10).
- La advertencia es **informativa-bloqueante**: muestra que el expediente está marcado "no recontratable" y su motivo.

**Criterios de aceptación:**
- Dado un ex-empleado **marcado como "no recontratable"**, cuando se intenta recontratar, entonces el sistema muestra **advertencia** y exige **autorización**; sin ella, la operación queda **bloqueada**.
- Dado un usuario **sin** el permiso de autorización, cuando enfrenta la advertencia, entonces **no puede** aprobar y la operación no avanza.
- Dado un rol **con el permiso** que autoriza, cuando confirma, entonces se **registra** la autorización (autor, fecha, motivo) y el flujo continúa.
- Dado un ex-empleado **sin** la marca, cuando se recontrata, entonces el flujo avanza **sin** paso de autorización.

**Prioridad:** Alta
**Dependencias:** Marca "no recontratable" en el expediente (captura manual, p. ej. al retirar); **nuevo permiso** de autorización de recontratación (D-10).

---

### RF-003 — Reactivación del expediente y apertura de nuevo período laboral

**Descripción:**
Reutilizar el expediente del ex-empleado, **reactivarlo** (`IsActive = true`) y **abrir un nuevo período laboral** con **nueva fecha de ingreso** y estado de empleo **activo**.

**Reglas de negocio:**
- **No** se crea un expediente nuevo; se reutiliza el existente (RN-03).
- El nuevo período fija una **nueva `HireDate`** (RN-04) y `IsEmploymentActive = true`.
- Los campos de retiro del período anterior se **respaldan** (RF-004) y luego se **limpian** para reflejar solo el período vigente.
- La reactivación **reabre** el expediente a `Draft` (transición `Completed → Draft`, D-08) para capturar el nuevo período y exige **`Finalize`** para volver a `Completed` (RN-12). El `Finalize` se **ajusta** para admitir un expediente que ya tuvo usuario vinculado.

**Criterios de aceptación:**
- Dado un ex-empleado seleccionado, cuando se confirma la recontratación, entonces el **mismo** expediente queda **activo**, con **nueva fecha de ingreso** y estado de empleo activo.
- Dado el nuevo período, cuando se consulta el expediente, entonces los campos de retiro están **vacíos** (corresponden solo al período vigente) y el período anterior es consultable como **historial**.

**Prioridad:** Alta
**Dependencias:** RF-004; ciclo de vida del expediente (`Draft`/`Completed`).

---

### RF-004 — Preservación del historial del período anterior (derivado)

**Descripción:**
Antes de sobrescribir los campos "vigentes" del perfil (1:1) con el nuevo período, garantizar que el período anterior quede **completamente representado en los registros existentes** —`PersonnelFileContractHistory` (contrato cerrado), `PersonnelFilePersonnelAction` (retiro) y `PersonnelFileEmploymentAssignment` (asignaciones con fecha de fin)— de modo que el historial se **derive** de ellos, **sin** una entidad nueva (D-14).

**Reglas de negocio:**
- El historial **no** se sobrescribe ni elimina (RN-05).
- Antes de actualizar `HireDate`/campos de retiro del perfil, el período anterior debe estar **cerrado** en los registros históricos (contrato `IsActive=false` con fechas, asignaciones con `EndDate`, acción de retiro registrada).
- Cada recontratación **incrementa** el número de períodos derivables del empleado.

**Criterios de aceptación:**
- Dado un ex-empleado con un período anterior, cuando se recontrata, entonces ese período queda registrado con sus **fechas** y **motivo de retiro** y **no** puede modificarse.
- Dado un empleado recontratado **varias** veces, cuando se consulta su historial, entonces se ven **todos** los períodos en orden cronológico.

**Prioridad:** Alta
**Dependencias:** RF-003.

---

### RF-005 — Reutilización editable de datos no laborales

**Descripción:**
En la recontratación, los **datos no laborales** del expediente (identificación, datos personales, contacto, direcciones, documentos, cuenta bancaria, familia/beneficiarios) se **conservan** y se presentan **pre-cargados y editables**.

**Reglas de negocio:**
- El usuario debe **revisar y confirmar** la vigencia de los datos (RN-07).
- La **identificación no cambia** (es la llave del expediente).
- Las actualizaciones se aplican sobre el **mismo** expediente.

**Criterios de aceptación:**
- Dado un ex-empleado recontratado, cuando se abre el formulario de recontratación, entonces sus datos no laborales aparecen **pre-cargados** y **editables**.
- Dado que el usuario actualiza un dato (p. ej. dirección o cuenta bancaria), cuando guarda, entonces se actualiza en el **mismo** expediente.

**Prioridad:** Media
**Dependencias:** RF-003.

---

### RF-006 — Registro de nuevo contrato y asignación de plaza

**Descripción:**
Capturar el **nuevo contrato** (tipo, fechas) y **asignar la(s) plaza(s)** del nuevo período, validando **cupo** y **estado** de la `PositionSlot`, reutilizando las reglas de **multi-plaza**.

**Reglas de negocio:**
- Reutiliza la lógica de asignaciones multi-plaza: capacidad (`maxEmployees`), una sola **principal** activa, sin **solapes** en la misma plaza, plaza no `Suspended` y vigente (RN-08).
- Crea un **nuevo** registro de contrato (`PersonnelFileContractHistory`) y **nueva(s)** asignación(es) activas del nuevo período.
- El contrato/asignaciones del período anterior permanecen **cerrados** en el historial.
- La plaza del nuevo período se **elige siempre de forma explícita** (puede ser la misma del período anterior, pero el sistema **no** la propone por defecto — D-16).

**Criterios de aceptación:**
- Dado el nuevo período, cuando se asigna una plaza con **cupo disponible**, entonces la asignación se crea **activa** y consume cupo.
- Dado una plaza **sin cupo** o **suspendida**, cuando se intenta asignar, entonces el sistema **rechaza** con el error correspondiente.
- Dado el nuevo contrato, cuando se guarda, entonces queda como contrato **activo** y el anterior permanece en historial.

**Prioridad:** Alta
**Dependencias:** Módulo de plazas/asignaciones (multi-plaza); RF-003.

---

### RF-007 — Reinicio de acumulados laborales

**Descripción:**
El nuevo período inicia los acumulados (**antigüedad, vacaciones, prestaciones**) **desde la nueva fecha de ingreso**; **no** se heredan saldos del período anterior.

**Reglas de negocio:**
- La antigüedad y los saldos se calculan **desde la nueva `HireDate`** (RN-04).
- La configuración de vacaciones (`VacationConfigurationJson`) se **reinicia** para el nuevo período.
- Los saldos del período anterior quedan como **histórico** y no se suman.

**Criterios de aceptación:**
- Dado un empleado recontratado, cuando inicia el nuevo período, entonces su **antigüedad** y **saldo de vacaciones** se calculan desde la nueva fecha de ingreso.
- Dado el período anterior, cuando se consulta, entonces sus saldos quedan como **histórico** y **no** se acumulan al nuevo período.

**Prioridad:** Media
**Dependencias:** RF-003; (futuro) módulos de vacaciones/nómina.

---

### RF-008 — Re-provisión de cuenta de usuario y accesos

**Descripción:**
Al **finalizar** la recontratación, **re-habilitar o crear** la cuenta de usuario del empleado (**email institucional** único, **rol** derivado de la plaza), reutilizando el flujo de finalización existente.

**Reglas de negocio:**
- El email institucional debe ser **único por tenant** (RN-09).
- **Por defecto se conserva el email institucional anterior** del empleado **si no está en uso**; en ese caso se **reactiva** su cuenta.
- Si el email anterior **ya está en uso** (reasignado a otro expediente), el sistema **exige capturar un nuevo email** para el recontratado antes de finalizar (D-09).
- La plaza del nuevo período debe tener **rol** asignado para provisionar el usuario (consistente con el `Finalize`).

**Criterios de aceptación:**
- Dado un empleado recontratado cuyo **email anterior está libre**, cuando se finaliza, entonces se **conserva** ese email y su cuenta queda **activa** con el rol de la plaza.
- Dado que el **email anterior está en uso**, cuando se va a finalizar, entonces el sistema **solicita un nuevo email** y no permite continuar hasta capturarlo.
- Dado un email institucional **válido y único** y plaza **con rol**, cuando se finaliza, entonces queda con **cuenta activa** y **rol** asignado.

**Prioridad:** Alta
**Dependencias:** Módulo de provisión de usuarios/Identity; **ajuste del flujo `Finalize`** (hoy exige `LinkedUserPublicId == null` — ver §16–17).

---

### RF-009 — Registro de acción de personal "Recontratación"

**Descripción:**
Cada recontratación genera un registro de auditoría (`PersonnelFilePersonnelAction`) de tipo **`RECONTRATACION`**, con **fecha efectiva**, **autor** y, si aplicó, **autorizador** del override.

**Reglas de negocio:**
- El registro es **append-only** (RN-10).
- Debe incluir la **fecha efectiva** del nuevo período y la referencia al período creado.
- Si hubo override de elegibilidad, incluye autorizador y motivo (vínculo con RF-002).

**Criterios de aceptación:**
- Dado una recontratación confirmada, cuando se completa, entonces existe un `PersonnelAction` tipo `RECONTRATACION` con **fecha efectiva** y **autor**.
- Dado que hubo **autorización** de override, cuando se consulta la acción, entonces incluye **autorizador** y **motivo**.

**Prioridad:** Alta
**Dependencias:** RF-002, RF-003.

---

### RF-010 — Notificación / onboarding de recontratación (opcional)

**Descripción:**
Opcionalmente, **notificar** (correo) al empleado recontratado y/o disparar el **onboarding de reincorporación**.

**Reglas de negocio:**
- Comportamiento **configurable**; candidato a quedar fuera del MVP.
- Reutiliza el servicio de correo existente.

**Criterios de aceptación:**
- Dado un empleado recontratado, cuando se finaliza y la notificación está habilitada, entonces recibe el **correo de reincorporación**.

**Prioridad:** Baja
**Dependencias:** Servicio de correo; RF-008.

---

### RF-011 — Consulta del histórico de períodos laborales

**Descripción:**
Visualizar la **línea de tiempo** de períodos del empleado (cada ingreso/retiro, motivo, contrato, plaza), en UI y por API.

**Reglas de negocio:**
- Solo **lectura**; se alimenta del historial inmutable (RF-004 / RN-05).
- Ordenado cronológicamente, con el período **vigente** destacado.

**Criterios de aceptación:**
- Dado un empleado con **varios** períodos, cuando se consulta su historial, entonces se ve la **línea de tiempo** completa y ordenada, con el período actual señalado.

**Prioridad:** Media
**Dependencias:** RF-004.

## 7. Requerimientos no funcionales

- **Seguridad:** la recontratación requiere `PersonnelFiles.Manage`; el override de elegibilidad requiere un **permiso de autorización** específico. Aislamiento estricto **por tenant** (búsqueda, unicidad y reactivación nunca cruzan empresas). Autorizaciones registradas con autor y fecha.
- **Rendimiento:** la localización por documento se apoya en el **índice único** existente (`tenant + tipo + número normalizado`), de costo O(log n). El historial de períodos se consulta paginado.
- **Disponibilidad:** la recontratación reutiliza endpoints transaccionales existentes; debe ser **atómica** (reactivar + cierre del período anterior + nuevo período) o totalmente revertible.
- **Escalabilidad:** modelo **multi-tenant**; la misma persona puede existir como empleado en N tenants sin colisión.
- **Usabilidad:** datos no laborales **pre-cargados**; advertencias de elegibilidad **claras** (motivo, notas); confirmación explícita de "nuevo período / nueva antigüedad" para evitar confusiones legales.
- **Auditoría:** toda recontratación deja `PersonnelAction` + autorización (si aplica); el período anterior es **inmutable**.
- **Compatibilidad:** reutiliza contratos/endpoints actuales (`employee-profile`, `employment-assignments`, `contract-history`, `personnel-actions`, `finalize`) y la concurrencia con `If-Match`/`concurrencyToken`.
- **Mantenibilidad:** se apoya en las **reglas multi-plaza** y en los **catálogos** ya existentes; evita lógica paralela.
- **Accesibilidad:** UI conforme al estándar del producto (navegación por teclado, contraste, lectores de pantalla).
- **Internacionalización:** mensajes/errores **bilingües** (ES/EN) según `Accept-Language`, alineado al catálogo de códigos.

## 8. Historias de usuario

### HU-001 — Recontratar a un ex-empleado
Como **Analista de RRHH**,
quiero **localizar a un ex-empleado por su documento y recontratarlo**,
para **reincorporarlo sin duplicar su ficha ni recapturar todos sus datos**.

**Criterios de aceptación:**
- Dado que el documento corresponde a un ex-empleado retirado del tenant, cuando lo busco, entonces veo su expediente con el resumen del período anterior y la opción "Recontratar".
- Cuando confirmo la recontratación con la nueva fecha de ingreso, entonces el expediente queda activo con un nuevo período laboral.
- Entonces sus datos no laborales aparecen pre-cargados y editables.

### HU-002 — Preservar el historial al recontratar
Como **Auditor**,
quiero **que cada período laboral quede registrado de forma inmutable**,
para **rastrear ingresos, retiros y motivos a lo largo del tiempo**.

**Criterios de aceptación:**
- Dado un empleado recontratado, cuando consulto su historial, entonces veo el período anterior con sus fechas y motivo de retiro, sin posibilidad de alterarlo.
- Cuando hay varias recontrataciones, entonces todos los períodos se listan en orden cronológico.

### HU-003 — Controlar recontratación de "no recontratables"
Como **Jefe de RRHH**,
quiero **ser advertido y tener que autorizar** la recontratación de alguien marcado como no recontratable,
para **mantener control sobre casos sensibles sin bloquear reingresos legítimos**.

**Criterios de aceptación:**
- Dado un ex-empleado con motivo de salida bloqueante, cuando un analista intenta recontratarlo, entonces el sistema advierte y exige autorización.
- Cuando autorizo con una justificación, entonces el flujo continúa y queda registrada mi autorización.
- Dado que el analista no tiene permiso de autorización, entonces no puede aprobar por sí mismo.

### HU-004 — Asignar plaza y contrato del nuevo período
Como **Analista de RRHH**,
quiero **asignar la plaza y registrar el contrato del nuevo período**,
para **formalizar la nueva vinculación respetando la capacidad de las plazas**.

**Criterios de aceptación:**
- Dado que la plaza tiene cupo y está vigente, cuando la asigno, entonces se crea la asignación activa y consume cupo.
- Dado que la plaza está suspendida o sin cupo, entonces el sistema rechaza la asignación con un mensaje claro.

### HU-005 — Recuperar accesos del empleado recontratado
Como **Empleado recontratado**,
quiero **recuperar mi cuenta y accesos al reincorporarme**,
para **volver a operar en el sistema con el rol de mi nueva plaza**.

**Criterios de aceptación:**
- Dado que finaliza mi recontratación con email institucional válido y plaza con rol, entonces mi cuenta queda activa con el rol correspondiente.
- Dado que mi email institucional ya está en uso por otro expediente, entonces el sistema lo impide y solicita corrección.

### HU-006 — Entender que es un nuevo vínculo
Como **Analista de RRHH**,
quiero **que el sistema deje claro que la recontratación inicia un nuevo período (nueva antigüedad)**,
para **evitar errores en vacaciones y prestaciones**.

**Criterios de aceptación:**
- Cuando recontrato, entonces el sistema muestra que la antigüedad y los acumulados se reinician desde la nueva fecha de ingreso.
- Entonces los saldos del período anterior no se suman al nuevo período.

## 9. Reglas de negocio

- **RN-01.** La recontratación aplica **solo dentro del mismo tenant**; el mismo documento en otro tenant es un vínculo **independiente** (no es recontratación).
- **RN-02.** Solo se puede recontratar a un expediente cuyo empleo esté **inactivo/retirado** (`IsEmploymentActive = false`). No aplica a empleados activos.
- **RN-03.** La cédula sigue siendo **única por (tenant, tipo, número)**; la recontratación **reutiliza** el expediente existente, **no** crea uno nuevo.
- **RN-04.** La recontratación inicia un **nuevo período laboral**: **nueva fecha de ingreso**; antigüedad, vacaciones y prestaciones se **reinician desde cero**.
- **RN-05.** El período laboral anterior (fechas, motivo de retiro, contrato, asignaciones) se conserva como **historial inmutable**; no se sobrescribe sin respaldo previo.
- **RN-06.** Si el expediente tiene la **marca manual "no recontratable"** (D-11), el sistema **advierte** y exige **autorización** mediante un **permiso nuevo** de autorización de recontratación (D-10) para continuar.
- **RN-07.** Los **datos no laborales** se reutilizan **pre-cargados y editables**; el usuario debe revisar/confirmar su vigencia.
- **RN-08.** La nueva asignación de plaza respeta el **cupo** (`maxEmployees`) y el **estado/vigencia** de la `PositionSlot` (no `Suspended`), una sola **principal** y sin **solapes** en la misma plaza.
- **RN-09.** El **email institucional** debe ser **único por tenant**. En la recontratación se **conserva** el email anterior si está **libre**; si está **en uso**, se **exige** uno **nuevo** (D-09).
- **RN-10.** Toda recontratación genera un **registro de auditoría** (acción de personal) con **autor**, **fecha efectiva** y, si aplica, **autorizador** del override.
- **RN-11.** La operación es **segura ante concurrencia** (`If-Match` / `concurrencyToken`), como el resto de mutaciones del expediente.
- **RN-12.** La reactivación **reabre** el expediente a `Draft` (transición `Completed → Draft`, D-08) para capturar el nuevo período y exige **`Finalize`** para volver a `Completed`; el `Finalize` se ajusta para admitir un expediente que ya tuvo usuario.
- **RN-13.** Las **evaluaciones y competencias** del período anterior se **archivan**: no continúan activas en el nuevo período, pero quedan **consultables como histórico** en la ficha (D-15, D-19).
- **RN-14.** La recontratación exige que el **período anterior esté cerrado/liquidado**; no se permite recontratar con el período previo abierto (D-13). Mientras no exista módulo de **nómina** o de **baja de personal**, el cierre se **confirma manualmente** en el flujo (D-17).
- **RN-15.** **No** se valida período mínimo de espera entre retiro y recontratación (D-12).
- **RN-16.** La plaza del nuevo período se **elige explícitamente** (puede ser la misma; sin propuesta automática) (D-16).
- **RN-17.** El **historial de períodos** es **derivado** de los registros existentes (contratos, acciones, asignaciones); no hay entidad dedicada (D-14).

## 10. Flujos principales

**Flujo feliz — Recontratación de un ex-empleado:**

1. El Analista de RRHH busca a la persona por **tipo + número de documento** (dentro del tenant).
2. El sistema encuentra el expediente del ex-empleado (estado **retirado/inactivo**) y muestra el **resumen del período anterior** (fecha de ingreso/retiro, motivo de retiro) y la acción **"Recontratar"**. El usuario **confirma manualmente** que el período anterior está **cerrado/liquidado** (precondición; mecanismo provisional hasta que exista módulo de nómina/baja de personal — D-13, D-17).
3. El sistema **evalúa la elegibilidad** según la **marca manual "no recontratable"** del expediente (D-11).
   - Si **no** está marcado → continúa al paso 5.
   - Si está marcado → muestra **advertencia** y solicita **autorización** con el permiso facultado (paso 4).
4. Un usuario con el **permiso de autorización** autoriza con justificación; el sistema **registra** la autorización.
5. El sistema **cierra y preserva en el historial** el período anterior: fechas, motivo de retiro, contrato y asignaciones (RF-004).
6. El sistema **reactiva** el expediente (`IsActive = true`) y lo **reabre** (`Draft`) para el nuevo período.
7. El Analista captura el **nuevo período**: **nueva fecha de ingreso**, tipo y fechas de **contrato**, y **elige explícitamente la(s) plaza(s)** (puede ser la misma del período anterior, pero se selecciona; sin propuesta automática — D-16), con validación de cupo/estado; jornada/nómina.
8. El sistema **reutiliza** los **datos no laborales** (pre-cargados); el Analista los **revisa/actualiza**.
9. El Analista **finaliza** la recontratación: el sistema valida el **email institucional** (se **conserva** el anterior si está libre, o se **captura uno nuevo** si está en uso — D-09; plaza con rol), **re-provisiona** la cuenta de usuario y pasa el expediente a `Completed`.
10. El sistema **reinicia acumulados** (antigüedad/vacaciones desde la nueva fecha) y **registra** la acción de personal `RECONTRATACION`.
11. (Opcional) El sistema **notifica** al empleado / dispara el onboarding de reincorporación.
12. El sistema muestra la **confirmación** y el expediente activo con su **historial de períodos**.

## 11. Flujos alternativos y excepciones

| ID | Escenario | Resultado esperado |
|---|---|---|
| **E1** | El documento **no existe** en el tenant. | El sistema indica que **no hay** ex-empleado con ese documento y ofrece **"crear nueva contratación"** (alta normal). |
| **E2** | El empleado encontrado está **activo**. | El sistema indica que **ya está activo**; no permite recontratar (sugiere editar el expediente vigente). |
| **E3** | El ex-empleado es **"no recontratable"** y el usuario **no** tiene permiso de autorización. | El sistema **bloquea** la recontratación y muestra el motivo; sugiere solicitar autorización a un rol facultado. |
| **E4** | El **rol facultado deniega** la autorización. | La recontratación se **cancela**; se registra la decisión (sin crear nuevo período). |
| **E5** | La **plaza** elegida está **suspendida**, vencida o **sin cupo**. | El sistema **rechaza** la asignación con el error correspondiente y solicita elegir otra plaza o liberar cupo. |
| **E6** | El **email institucional anterior** ya está en uso por otro expediente del tenant. | El sistema **solicita capturar un nuevo email** para el recontratado; no permite finalizar hasta hacerlo (D-09, RN-09). |
| **E7** | La **plaza no tiene rol** y se solicita crear cuenta de usuario. | El sistema impide finalizar la provisión hasta asignar rol a la plaza (consistente con `Finalize`). |
| **E8** | **Conflicto de concurrencia** (otro usuario modificó el expediente). | El sistema responde **409** (token desactualizado); el usuario recarga y reintenta (RN-11). |
| **E9** | Falla a mitad de la operación (p. ej. tras reactivar pero antes de crear el contrato). | La operación es **atómica/revertible**: el expediente **no** queda en estado inconsistente (ni medio-reactivado ni con período incompleto). |
| **E10** | Se intenta recontratar un **`Candidate`** (no empleado). | El sistema indica que la recontratación aplica **solo a empleados**; fuera de alcance. |
| **E11** | El expediente tiene **datos no laborales vencidos** (p. ej. documento de identidad expirado). | El sistema **advierte** y solicita actualizar antes de finalizar (no bloquea la captura, sí la finalización si es obligatorio). |
| **E12** | El **período anterior no está cerrado/liquidado** (sin confirmación manual de cierre). | El sistema **bloquea** la recontratación hasta **confirmar manualmente** el cierre/liquidación del período previo (D-13, D-17, RN-14). |

## 12. Datos requeridos

### Entidad: Solicitud de Recontratación *(entrada del flujo — nueva)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| identificationType | Texto | Sí | Tipo de documento válido del catálogo | Tipo de documento del ex-empleado a localizar |
| identificationNumber | Texto | Sí | Normalizado; existe en el tenant | Número de documento |
| newHireDate | Fecha | Sí | ≥ fecha de retiro del período anterior | Nueva fecha de ingreso (inicio del nuevo período) |
| contractTypeCode | Texto | Sí | Código de catálogo de tipo de contrato | Tipo de contrato del nuevo período |
| contractStartDate | Fecha | Sí | Coherente con `newHireDate` | Inicio del nuevo contrato |
| contractEndDate | Fecha | No | > `contractStartDate` (si aplica) | Fin del contrato (nulo = indefinido) |
| positionSlotPublicId | GUID | Sí | Plaza existente, del tenant, asignable, con cupo y rol; **elegida explícitamente** (D-16) | Plaza principal del nuevo período |
| newInstitutionalEmail | Texto (email) | Condicional | Requerido **solo si** el email anterior está en uso (D-09) | Nuevo email institucional del recontratado |
| authorizationReason | Texto | Condicional | Requerido si el expediente está marcado "no recontratable" | Justificación del override de elegibilidad |
| concurrencyToken (If-Match) | GUID | Sí | Debe coincidir con el token vigente | Control de concurrencia |

### Entidad: PersonnelFileEmployeeProfile *(afectada — campos relevantes)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| HireDate | Fecha | Sí | Nueva fecha del período vigente | Se **actualiza** a la fecha de recontratación |
| EmploymentStatusCode | Texto | Sí | Código de catálogo (estado activo) | Pasa a estado **activo** |
| IsEmploymentActive | Booleano | Sí | — | Se fija en **true** |
| ContractTypeCode | Texto | Sí | Código de catálogo | Tipo de contrato del nuevo período |
| ContractStartDate / ContractEndDate | Fecha | No | Coherencia de fechas | Vigencia del contrato vigente |
| RetirementCategoryCode / ReasonCode / Notes / Date | Texto/Fecha | No | Se **limpian** tras respaldar (RF-004) | Pertenecen solo al período vigente; vacíos al recontratar |
| VacationConfigurationJson | Texto (JSON) | No | — | Se **reinicia** para el nuevo período |

### Entidad: Período de Empleo *(vista derivada — sin entidad nueva, D-14)*

El historial de períodos **se deriva** de registros existentes; **no** se crea una entidad ni una tabla nueva. Cada período se reconstruye así:

| Dato del período | Fuente derivada |
|---|---|
| Fecha de ingreso | `PersonnelFileContractHistory.ContractDate` (o acción de ingreso) del período |
| Fecha de retiro | `PersonnelFileEmployeeProfile.RetirementDate` (período vigente) / acción de retiro (períodos previos) |
| Categoría/motivo/notas de retiro | Campos `Retirement*` vigentes (período actual) / `PersonnelFilePersonnelAction` de retiro (previos) |
| Tipo de contrato | `PersonnelFileContractHistory.ContractTypeCode` |
| Plaza(s) | `PersonnelFileEmploymentAssignment` (con `StartDate/EndDate`) del período |
| Período vigente vs. histórico | Contrato/asignaciones con `IsActive=true` = vigente |

> El negocio solo exige que el historial sea **completo e inmutable**; la implementación lo **deriva** de contratos, acciones y asignaciones (D-14). Esto implica que, al recontratar, el período anterior debe quedar **bien cerrado** en esos registros antes de abrir el nuevo (RF-004).

### Entidad: PersonnelFileEmploymentAssignment *(afectada)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PositionSlotPublicId | GUID | Sí | Plaza válida, del tenant, con cupo | Plaza asignada en el nuevo período |
| AssignmentTypeCode | Texto | Sí | Catálogo de tipo de asignación | Tipo de asignación |
| IsPrimary | Booleano | Sí | Exactamente una principal activa | Plaza principal del período |
| StartDate / EndDate | Fecha | Sí/No | Sin solape en la misma plaza | Vigencia de la asignación |
| IsActive | Booleano | Sí | — | Las del período anterior quedan inactivas |

### Entidad: PersonnelFileContractHistory *(afectada)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| ContractTypeCode | Texto | Sí | Catálogo | Tipo de contrato |
| ContractDate | Fecha | Sí | — | Inicio del contrato |
| ContractEndDate | Fecha | No | > ContractDate | Fin del contrato |
| PositionSlotPublicId | GUID | No | Plaza válida | Plaza del contrato |
| IsActive | Booleano | Sí | Solo el del período vigente activo | Estado del contrato |
| Notes | Texto | No | — | Notas |

### Entidad: PersonnelFilePersonnelAction *(auditoría)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| actionTypeCode | Texto | Sí | = `RECONTRATACION` | Tipo de acción |
| actionStatusCode | Texto | Sí | Catálogo | Estado de la acción |
| actionDateUtc | Fecha/hora | Sí | — | Fecha de registro |
| effectiveFromUtc | Fecha/hora | Sí | = nueva fecha de ingreso | Inicio de efectividad del nuevo período |
| description | Texto | No | máx. definido | Detalle (incl. autorizador/motivo si hubo override) |
| isSystemGenerated | Booleano | Sí | — | Marca de origen del registro |

### Entidad: Elegibilidad de Recontratación *(marca manual en el expediente — nueva)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| RehireBlocked | Booleano | No | — | Marca **manual** "no recontratable" a nivel de expediente (D-11) |
| RehireBlockedReason | Texto | No | máx. definido | Justificación del bloqueo manual |
| RehireBlockedByUserId / At | GUID / Fecha | No | — | Quién y cuándo fijó la marca (trazabilidad) |

> La marca se fija **manualmente al retirar** al empleado; quién puede fijarla/retirarla lo define la **matriz RBAC** por rol (el **owner** puede por defecto, D-18). **No** se usa el catálogo de motivo de retiro para determinar la elegibilidad (D-11).

## 13. Integraciones necesarias

- **Catálogos internos (`InternalCatalogs`):** motivos/categorías de retiro, tipos de contrato, tipos de asignación, estados de empleo. Configurables por tenant. *(La elegibilidad "no recontratable" es una **marca manual** del expediente, no un atributo de catálogo — D-11.)*
- **Módulo de Plazas (`PositionSlots`):** validación de existencia, estado (no `Suspended`), vigencia, **cupo** (`maxEmployees`) y **rol** (`RoleId`) para provisión.
- **Provisión de Usuarios / Identity:** reactivación o creación de la cuenta del empleado (email institucional único, rol de la plaza) vía el flujo de **finalización**.
- **Auditoría (`PersonnelActions`):** registro append-only de la acción de recontratación y de la autorización del override.
- **Servicio de correo (opcional):** notificación/onboarding de reincorporación (RF-010).
- **(Futuro) Nómina / Vacaciones:** consumirán el **nuevo período** para antigüedad y acumulados. Sin integración en esta fase.
- **Sin integraciones externas** ni con otros tenants (la recontratación es intra-tenant).

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Analista / Gestor de RRHH** | Localizar ex-empleado, iniciar y completar recontratación, capturar nuevo período, asignar plaza, editar datos no laborales (`PersonnelFiles.Manage`). | **No** puede autorizar el override de "no recontratable". Opera solo dentro de su tenant. |
| **Aprobador / Jefe de RRHH** | Todo lo del Analista **+** **autorizar** la recontratación de "no recontratables" (permiso de autorización de recontratación). | La autorización queda **registrada** (autor, fecha, motivo). |
| **Administrador de Plazas** | Definir/disponer plazas, cupo, estado y rol (`PositionSlots.Manage`). | No ejecuta la recontratación; provee la plaza. |
| **Empleado (autoservicio)** | Consultar su propio expediente e historial de períodos (solo lectura). | No puede recontratarse a sí mismo. |
| **Auditor** | Consultar historial de períodos, acciones de personal y autorizaciones (`PersonnelFiles.Read`). | Solo lectura. |
| **Sistema** | Validar invariantes, cerrar/preservar el período anterior, reactivar, re-provisionar, registrar auditoría. | Atómico; respeta concurrencia y aislamiento por tenant. |

> Se reutiliza la **matriz de permisos** existente. Se **crea un permiso nuevo** de **autorización de recontratación** para el override (D-10, RN-06), distinto de `PersonnelFiles.Manage` (p. ej. `PersonnelFiles.AuthorizeRehire`). La capacidad de **fijar la marca "no recontratable"** (al retirar) se rige por la **matriz RBAC** por rol; el **owner** puede por defecto (D-18).

## 15. Criterios de aceptación generales

- Un ex-empleado **retirado** puede ser **recontratado** reutilizando su expediente, sin violar la unicidad de documento (RF-001, RF-003).
- La recontratación **preserva** de forma inmutable el período anterior y crea un **nuevo período** con **nueva fecha de ingreso** (RF-003, RF-004; RN-04, RN-05).
- Los **"no recontratables"** generan **advertencia + autorización**; sin autorización, la operación se **bloquea**; la autorización queda **auditada** (RF-002; RN-06, RN-10).
- Los **datos no laborales** se **reutilizan** (pre-cargados, editables) y la **identificación** no cambia (RF-005; RN-07).
- El nuevo período registra **contrato** y **plaza** respetando **cupo/estado** y reglas multi-plaza (RF-006; RN-08).
- Los acumulados (**antigüedad, vacaciones**) se **reinician** desde la nueva fecha (RF-007; RN-04).
- La cuenta de usuario se **re-provisiona** con email único y rol de la plaza (RF-008; RN-09).
- La operación es **atómica**, **concurrencia-segura** (`If-Match`) y **multi-tenant** (RN-01, RN-11).
- Mensajes y errores **bilingües** y consistentes con el catálogo de códigos.
- El **historial de períodos** es consultable en UI y por API (RF-011).

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **Ajuste del `Finalize` (decidido: `Completed → Draft`, D-08):** el `Finalize` actual exige `LinkedUserPublicId == null`. Como se reabre y re-finaliza un expediente que **ya** tuvo usuario, **es obligatorio ajustar** esa invariante para la recontratación; de lo contrario, la re-provisión falla. *(Trabajo técnico confirmado.)*
- **Pérdida de historial:** como el historial es **derivado** (D-14) y el perfil es 1:1, sobrescribir `HireDate`/retiro **sin** haber cerrado antes el período anterior en contratos/acciones/asignaciones lo haría irrecuperable. **Mitigación:** RF-004/RN-05 (cierre obligatorio del período previo antes de abrir el nuevo).
- **Confusión legal de antigüedad:** si la UX no deja claro que es un **nuevo vínculo**, se pueden cometer errores en vacaciones/prestaciones. **Mitigación:** confirmación explícita (HU-006).
- **Colisión de email institucional:** si el email anterior fue reasignado, la re-provisión puede chocar con la unicidad por tenant. **Mitigación:** RN-09 + validación en finalize.
- **Estado inconsistente ante fallos:** una operación no atómica podría dejar el expediente medio-reactivado. **Mitigación:** transaccionalidad (E9).
- **Cupo de plaza:** recontratar a una plaza sin cupo o suspendida. **Mitigación:** reglas multi-plaza (RN-08, E5).

### Supuestos
- La recontratación **exige** que el período anterior esté **cerrado/liquidado** (D-13); el **cálculo** de la liquidación está fuera de este módulo (D-06). Mientras no exista módulo de **nómina** o de **baja de personal**, el cierre se confirma de forma **manual** en el flujo (D-17).
- El sistema es **multi-tenant** y todas las búsquedas/validaciones son **por-tenant** (la misma persona puede ser empleada en varias empresas — D-02).
- La elegibilidad "no recontratable" es una **marca manual** del expediente (D-11), no derivada de catálogo; se fija **al retirar** y su permiso se rige por la matriz RBAC (D-18).
- La recontratación es **una a la vez** (no masiva — D-07).
- Las evaluaciones/competencias previas se **archivan** y quedan **consultables como histórico** en la ficha (D-15, D-19, RN-13).

### Dependencias
- **Catálogos** de tipos de contrato y de asignación, estados de empleo (`InternalCatalogs`). *(La marca "no recontratable" es manual, no de catálogo — D-11.)*
- **Módulo de Plazas** (`PositionSlots`): cupo, estado, rol.
- **Provisión de usuarios / Identity** y **ajuste del flujo `Finalize`** (transición `Completed → Draft`, D-08).
- **Auditoría** (`PersonnelActions`).
- **Matriz de permisos**: **nuevo permiso** de autorización de recontratación (D-10).
- **Captura de la marca "no recontratable"** al **retirar** al empleado; permiso por **matriz RBAC** (owner por defecto) (D-11, D-18).
- **(Futuro) Módulo de nómina o de baja de personal** que provea la señal de cierre/liquidación, reemplazando la confirmación manual (D-17).
- **Reglas de asignación multi-plaza** ya implementadas (commit reciente de multi-position).

## 17. Preguntas abiertas para el cliente o stakeholders

> **No quedan preguntas abiertas.** Las **13** preguntas del levantamiento (10 iniciales + 3 de detalle) fueron resueltas y trazadas como decisiones **D-04…D-19** (ver tabla al inicio). Resolución de las últimas tres:

| Punto | Resolución | Decisión |
|---|---|---|
| Cierre/liquidación del período anterior (mecanismo) | **Confirmación manual** en el flujo, hasta que exista módulo de **nómina** o de **baja de personal**. | D-17 |
| Captura de la marca "no recontratable" | Se fija **al retirar**; permiso por **matriz RBAC** (owner por defecto). | D-18 |
| Visibilidad de evaluaciones/competencias archivadas | **Consultables como histórico** en la ficha. | D-19 |

## 18. Recomendaciones del Analista de Negocio

1. **Ajustar el `Finalize` para la recontratación (decidido: `Completed → Draft`, D-08).** Es el mayor trabajo técnico. Recomendación: un **comando orquestador `RehireEmployee`** que ejecute atómicamente (validar marca/autorización → cerrar período anterior → reabrir a `Draft` → capturar nuevo período → re-finalizar/re-provisionar → auditar), **relajando** la invariante `LinkedUserPublicId == null` solo en el camino de recontratación.
2. **MVP enfocado (Fase 1):** RF-001, RF-002, RF-003, RF-004, RF-006, RF-008, RF-009 — es decir, **localizar → validar/autorizar → preservar → reactivar con nuevo período (contrato + plaza) → re-provisionar → auditar**. Cubre el caso de negocio completo de forma trazable.
3. **Fase 2 (mejoras):** RF-005 (pre-carga editable enriquecida), RF-007 (integración con acumulados/vacaciones cuando exista nómina), RF-010 (notificaciones/onboarding), RF-011 (línea de tiempo de períodos en UI).
4. **Modelar la elegibilidad como marca manual del expediente** (`RehireBlocked` + motivo + trazabilidad de quién/cuándo, D-11), fijada por RRHH al retirar o editar; protegerla con permiso.
5. **Cierre del período anterior como invariante bloqueante** antes de sobrescribir el perfil 1:1: dado que el historial es **derivado** (D-14), asegurar que contrato/asignaciones/acción de retiro queden bien cerrados protege RN-05.
6. **UX explícita de "nuevo vínculo":** mostrar claramente "nueva fecha de ingreso / antigüedad desde cero" y el resumen del período anterior, para prevenir errores legales.
7. **Reutilizar al máximo** los endpoints existentes (`employment-assignments`, `contract-history`, `personnel-actions`, `finalize`) bajo la orquestación del nuevo comando, en lugar de duplicar lógica.
8. **Documento técnico de seguimiento:** producir un **plan de implementación** (ajuste de `Finalize`/comando `RehireEmployee`, entidad o derivación del historial, atributo de catálogo + migración, pruebas de parity y E2E) una vez aprobado este alcance.

---

> **Nota de trazabilidad.** Este análisis se construyó verificando el código base de CLARIHR (entidades `PersonnelFile`/`PersonnelFileEmployeeProfile`, `PersonnelFileEmploymentAssignment`, `PersonnelFileContractHistory`, `PersonnelFilePersonnelAction`, `PositionSlot`; el índice único `uq_personnel_file_identifications__tenant_type_number`; y el flujo de `Finalize`). Los puntos no determinables desde el código se marcaron como **supuestos** (§16) o **preguntas abiertas** (§17), sin inventar información crítica.
