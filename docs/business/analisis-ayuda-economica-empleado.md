# Análisis de Negocio — Ayuda / Asistencia Económica del Empleado

> **Tipo de documento:** Análisis de requerimiento (validación + GAP) y especificación funcional.
> **Módulo:** Expediente de Personal → Compensación / Bienestar (`PersonnelFiles`).
> **Fecha:** 2026-06-27 · **Versión:** v1.1 (decisiones D-01…D-16 **RATIFICADAS 2026-06-27**).
> **Autor:** Analista de Negocio (CLARIHR).
> **Estado:** Requerimiento de negocio **cerrado**; listo para plan técnico.

---

## 0. Veredicto ejecutivo (resultado de la validación)

El requerimiento pedía: dar al empleado una opción para **solicitar ayuda económica** por una **emergencia**, que pueda ser **validada por un agente de RR. HH.**; **hoy no se construye el flujo de aprobaciones**, pero el modelo **debe quedar preparado** para integrarlo a futuro. Además: validar el alineamiento con lo ya implementado, **parametrizar catálogos**, **reutilizar servicios accedidos por keys** y **hacer el seed inicial por país (El Salvador)**.

**Resultado: el requerimiento NO está implementado. Es desarrollo nuevo (net-new).**

Búsqueda exhaustiva en `src/` y `docs/`: no existe ninguna entidad, catálogo, endpoint ni migración para "ayuda económica", "asistencia económica", "solicitud de ayuda", "emergency aid", "financial assistance", "préstamo a empleado" ni "anticipo solicitado por el empleado". (Sí aparecen los **conceptos de compensación** `ANTICIPO`, `PRESTAMO_INTERNO`, `PRESTAMO_BANCARIO` en `DevSeedService`, pero son **conceptos de deducción para la nómina**, no una solicitud del empleado — ver §0.1.)

### ⚠️ Hallazgo de alineación — los "primos cercanos" y la dirección del dinero

Existen módulos con forma parecida que **NO deben confundirse ni reutilizarse**. La diferencia clave es **quién pide y quién paga**:

| Concepto (ya existe) | Dirección | Naturaleza | ¿Solicita el empleado? | ¿Reutilizar? |
|---|---|---|---|---|
| `PersonnelFilePayrollTransaction` | Dentro de nómina | Bitácora **inmutable** de débitos/créditos de una corrida de planilla (importada de nómina externa) | No | **No** |
| `PersonnelFileOffPayrollTransaction` | Empresa → empleado | **Gasto que la empresa asume** por el empleado (herramientas, EPP, uniformes, regalos). **Interno de RR. HH., sin autoservicio** | No | **No** (pero es la **plantilla financiera**) |
| `PersonnelFileMedicalClaim` | Aseguradora → empleado | **Reclamo a un seguro** ya contratado (requiere póliza); el dinero lo paga la **aseguradora** | Sí (autoservicio) | **No** (pero es la **plantilla de comportamiento**) |
| Conceptos `ANTICIPO`/`PRESTAMO_*` (`CompensationConcept`) | Config de nómina | **Definición de deducción** para planilla, no una solicitud | No | Solo como **destino futuro** del repago |
| **Ayuda económica (SOLICITADO)** | **Empleado → empresa** | **El empleado PIDE dinero** a la empresa por una **emergencia**; la **empresa** decide y paga; **RR. HH. valida** | **Sí (autoservicio)** | **Net-new** |

**Conclusión de alineación:** la ayuda económica es un **flujo entrante** (el empleado **solicita** a la empresa) inexistente hoy. Se construye un **módulo nuevo** combinando tres plantillas ya probadas en el código:

1. **Comportamiento (autoservicio + estado + resolución + adjuntos):** `MedicalClaims` (el empleado crea lo suyo; RR. HH. resuelve).
2. **Datos financieros (monto + moneda + tipo-catálogo + período):** `OffPayrollTransaction`.
3. **Estado de aprobación preparado para el futuro:** `SalaryTabulatorChangeRequest` (la **única** máquina de estados real del sistema: `Draft → Submitted → Approved/Rejected/Canceled`, con `Approve(decidedBy, decidedAt, comment, allowSelfApproval)` que **impide la auto-aprobación**).

### 0.1. ¿Es necesario hacer algo? — Sí

El requerimiento indicaba: *"si no es necesario hacer nada más, no incluir nada y dar por cerrado"*. Dado que el módulo **no existe** y resuelve un proceso real de RR. HH. (asistencia/fondo de emergencia, *hardship/employee relief fund*), **sí es necesario construirlo**. Este documento define el alcance mínimo (MVP), deja **catálogos parametrizables con seed SV**, identifica los **servicios reutilizables accedidos por key**, y **diseña el modelo para el flujo de aprobación futuro sin construirlo ahora**.

> Las decisiones (§17) fueron **ratificadas el 2026-06-27** (D-01…D-16). El documento incorpora esas decisiones; el único cambio de alcance respecto a la propuesta inicial es **D-08**, que **incorpora una antigüedad mínima configurable** como requisito de elegibilidad.

---

## 1. Resumen del producto o requerimiento

Se requiere incorporar al **Expediente de Personal** la capacidad de que un **empleado solicite ayuda económica** a la empresa ante una **emergencia** (médica, fallecimiento de un familiar, desastre natural, incendio, calamidad doméstica, etc.), y que un **agente de RR. HH.** pueda **revisar y validar** esa solicitud (aprobarla, rechazarla o requerir documentación), dejando registro de **quién** validó, **cuándo**, con qué **monto aprobado** y bajo qué **observaciones**.

El módulo tiene **tres piezas**:

1. **Solicitud de ayuda económica** (operativo, autoservicio): el empleado registra el **tipo de ayuda/emergencia**, una **descripción/justificación**, el **monto solicitado**, la **moneda** y **documentos de respaldo**.
2. **Validación por RR. HH.** (operativo, interno): RR. HH. resuelve la solicitud cambiando su **estado** y registrando el **monto aprobado**, observaciones, actor y fecha. *(Esta validación es de un solo paso en Fase 1; el flujo de aprobación multinivel se difiere.)*
3. **Catálogos parametrizables** (configuración): **tipos de ayuda económica** y **estados de la solicitud**, *country-scoped* y con **seed inicial para El Salvador**.

**Problema que resuelve:** hoy no existe un canal estructurado, documentado y auditable para que el empleado solicite asistencia económica de emergencia ni para que RR. HH. la gestione. Centralizarlo da **trazabilidad** (quién pidió qué, por qué, cuánto, quién aprobó), **respaldo documental**, **confidencialidad** del motivo (dato sensible) y una **base lista** para automatizar el flujo de aprobación cuando se decida.

---

## 2. Objetivos del negocio

- **OB-01.** Ofrecer al empleado un **canal de autoservicio** para solicitar ayuda económica ante emergencias.
- **OB-02.** Permitir a RR. HH. **validar** cada solicitud (aprobar / rechazar / requerir documentación) con **registro del responsable, fecha y observaciones**.
- **OB-03.** **Estandarizar** los tipos de ayuda y los estados mediante **catálogos administrables**, evitando texto libre inconsistente.
- **OB-04.** Garantizar **trazabilidad y auditoría** completas (solicitud, validación, desembolso, adjuntos) con concurrencia optimista.
- **OB-05.** Tratar el **motivo de la emergencia como dato sensible**: acceso solo a RR. HH. y al propio empleado (confidencialidad).
- **OB-06.** **Dejar el modelo preparado** para integrar a futuro un **flujo de aprobación** (multinivel, por umbrales, con notificaciones) **sin requerir una migración disruptiva**.
- **OB-07.** **Reutilizar las convenciones y servicios existentes** (catálogos *country-scoped* validados por key, autoservicio, adjuntos, auditoría, concurrencia, moneda por defecto de la empresa) para minimizar costo y riesgo.
- **OB-08.** Sentar la base para **análisis posterior** (montos solicitados/aprobados por tipo, período y moneda) sin construir reportería avanzada en esta fase.

---

## 3. Alcance funcional

Incluye:

- **AF-01.** CRUD del **catálogo de tipos de ayuda económica** (Code ingresado por el usuario + Descripción), *country-scoped*, con **seed SV**.
- **AF-02.** CRUD/lectura del **catálogo de estados de la solicitud**, *country-scoped*, con **seed SV** (administrable, pero su edición se restringe por ser estructural — ver RF-002).
- **AF-03.** **Solicitar** ayuda económica (autoservicio del empleado) o registrarla **en su nombre** (RR. HH.): tipo, descripción/justificación, monto solicitado, moneda, fecha.
- **AF-04.** **Adjuntar documentos de respaldo** (constancia médica, acta de defunción, denuncia, fotos, cotizaciones, etc.).
- **AF-05.** **Consultar**: el empleado ve **solo sus propias** solicitudes; RR. HH. ve **todas**; listar / filtrar / paginar / detalle.
- **AF-06.** **Validar** por RR. HH.: cambiar estado (aprobar / rechazar / requerir documentación / en revisión), registrar **monto aprobado**, **observaciones**, **actor** y **fecha de resolución**.
- **AF-07.** **Registrar el desembolso** (informativo) de una solicitud aprobada (fecha, monto desembolsado, forma de pago opcional).
- **AF-08.** **Editar / dar de baja** (baja lógica) una solicitud por RR. HH.; **cancelar/retirar** la propia solicitud pendiente por el empleado (autoservicio, ver D-11).
- **AF-09.** **Validaciones:** tipo activo del catálogo; estado válido; moneda ISO‑4217; monto solicitado > 0; **monto aprobado > 0 al aprobar** (parcial permitido); fecha de solicitud no futura; fecha de resolución ≥ fecha de solicitud; expediente en estado **empleado completado y activo**; **antigüedad mínima configurable cumplida** (D-08).
- **AF-10.** **Auditoría** de toda operación (alta, validación, desembolso, edición, baja, adjuntos): actor, fecha, before/after.
- **AF-11.** **Diseño preparado para el flujo de aprobación futuro**: estado como **catálogo configurable**, validación modelada como **acción** (no como edición de campo) con actor/fecha/notas, y reserva para un **historial de aprobación**.
- **AF-12.** **Permisos dedicados** de consulta y gestión; **autoservicio** para crear/consultar lo propio; **sin auto-validación** (el empleado nunca aprueba su propia solicitud).
- **AF-13.** **Localización** bilingüe (es / en) de etiquetas y errores.
- **AF-14.** (Prioridad media/baja) **Búsqueda, paginación y exportación** del listado, con totalización por moneda.

---

## 4. Fuera de alcance (esta fase)

- **FA-01.** **Flujo de aprobación multinivel** (ruteo a aprobadores, umbrales por monto, escalamiento, delegación, notificaciones). **Reconocido como necesario a futuro (OB-06)**; en Fase 1 la validación es **de un solo paso por RR. HH.** El modelo queda preparado (§18).
- **FA-02.** **Integración con nómina / tesorería / contabilidad** para el **pago** efectivo. No existe motor de nómina (ver §16). El desembolso es **informativo**.
- **FA-03.** **Ayudas reembolsables** (anticipo de salario / préstamo) con **plan de pagos, saldos e intereses**. En Fase 1 la ayuda es **no reembolsable** (subsidio/donación por emergencia). El tipo de ayuda queda parametrizable para habilitarlas después (D-01/D-13).
- **FA-04.** **Topes / presupuesto / límites** por empleado, tipo o período, y alertas asociadas (D-07: sin topes en Fase 1).
- **FA-05.** **Reglas de elegibilidad complejas** (frecuencia máxima por año/período, restricción por tipo de contrato). En Fase 1 la elegibilidad es: empleado **activo y completado** **+ antigüedad mínima configurable** a nivel de empresa (D-08).
- **FA-06.** **Conversión de moneda (FX)** y reportería multimoneda consolidada. Solo **totalización por moneda** sin conversión.
- **FA-07.** **Cálculo de impacto tributario / fiscal** de la ayuda (taxabilidad). Diferido (depende de nómina/legislación — ver §17).
- **FA-08.** **Importación masiva** desde sistemas externos.
- **FA-09.** **Dashboards analíticos** avanzados más allá del listado, su exportación y la totalización por moneda.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en este módulo |
|---|---|
| **Empleado (autoservicio)** | **Crea** su solicitud de ayuda económica, **adjunta** respaldo, **consulta** el estado de **sus propias** solicitudes y (opcional, D-11) **cancela/retira** una propia aún pendiente. **No valida** ni ve solicitudes de otros. |
| **Gestor / Agente de RR. HH.** | **Revisa y valida** (aprobar / rechazar / requerir documentación), registra **monto aprobado** y observaciones, **registra el desembolso**, edita, da de baja, consulta y exporta. Puede crear **en nombre** de un empleado. |
| **Administrador / Configurador de catálogos** | Crea y mantiene los **catálogos** de tipos de ayuda y de estados; *country-scoped*. |
| **Supervisor / Gerente con permiso de consulta** | **Consulta** (solo lectura) y exporta, si se le otorga el permiso de vista. |
| **Aprobador(es) (FUTURO)** | En el flujo de aprobación de Fase 2: aprueban por nivel/umbral; pueden delegar (vía `AuthorizationSubstitution`). **No aplica en Fase 1.** |
| **Sistema CLARIHR** | Validaciones, normalización, autoservicio (resolución del empleado), auditoría, concurrencia, almacenamiento de adjuntos. |
| **Almacenamiento de archivos (Azure Blob)** | Persistencia de los documentos de respaldo. |
| **Sistema de notificaciones (FUTURO)** | Avisos de cambio de estado/aprobación. No integrado en Fase 1. |

---

## 6. Requerimientos funcionales

### RF-001 — Catálogo de tipos de ayuda económica

**Descripción:** El administrador crea/mantiene las **categorías de ayuda/emergencia** con **Code** (ingresado por el usuario) + **Descripción** (p. ej. `EMERGENCIA_MEDICA`, `GASTOS_FUNEBRES`, `DESASTRE_NATURAL`, `INCENDIO_VIVIENDA`, `CALAMIDAD_DOMESTICA`, `ACCIDENTE`, `OTRA`).

**Reglas de negocio:**
- **Code** obligatorio, ≤80, **único por país/tenant**, normalizado en mayúsculas.
- **Descripción** obligatoria, ≤200.
- Catálogo **country-scoped** (`GeneralCatalogItem`); ítem nace activo; `SortOrder` configurable.
- (Diseño futuro, D-13) puede incorporar un atributo **`IsRefundable`** para distinguir subsidio vs anticipo/préstamo; en Fase 1 todos **no reembolsables**.

**Criterios de aceptación:**
- Dado permiso de gestión de catálogos y Code+Descripción válidos, cuando se crea, entonces queda disponible para selección.
- Dado un Code duplicado en el mismo país, cuando se guarda, entonces se rechaza por unicidad.
- Existe **seed SV** inicial (ver §12).

**Prioridad:** Alta. **Dependencias:** convenciones de catálogo (`GeneralCatalogItem`, `GeneralCatalogKeyMap`, seed `HasData`).

---

### RF-002 — Catálogo de estados de la solicitud

**Descripción:** Catálogo **country-scoped** con los estados del ciclo de vida: `SOLICITADA`, `EN_REVISION`, `PENDIENTE_DOCUMENTACION`, `APROBADA`, `RECHAZADA`, `DESEMBOLSADA`, `ANULADA`. Modelar el estado como **catálogo** (no enum) lo deja **configurable** y **forward-compatible** con el flujo de aprobación futuro.

**Reglas de negocio:**
- Mismas reglas de catálogo *country-scoped* (Code único por país, Descripción, `SortOrder`, baja lógica).
- Los estados son **estructurales**: se entregan **semillados (seed SV)**; su edición/creación libre se restringe a administración (cambiar nombre/orden sí; alterar la semántica del ciclo, no recomendado).
- La aplicación valida la **existencia y actividad** del código de estado por key (ver §13), no asume valores hardcodeados en el dominio (salvo el estado inicial `SOLICITADA`).

**Criterios de aceptación:** existe **seed SV**; al validar una solicitud con un estado inexistente/inactivo → `422`.

**Prioridad:** Alta. **Dependencias:** RF-001 (mismas convenciones).

---

### RF-003 — Solicitar ayuda económica (autoservicio)

**Descripción:** El **empleado** registra una solicitud para **sí mismo** (autoservicio); RR. HH. también puede registrarla **en su nombre**. Datos: **tipo** (catálogo), **descripción/justificación**, **monto solicitado**, **moneda**, **fecha de solicitud**.

**Reglas de negocio:**
- **Autoservicio:** el empleado solo puede crear sobre **su propio** expediente (resolución por `LinkedUserPublicId` = usuario actual). RR. HH. con permiso de gestión puede crear sobre cualquiera (RN-15).
- **Tipo:** obligatorio; existe y **activo** en catálogo (validado por key, RN-03).
- **Descripción/justificación:** obligatoria; ≤2000 (es el "motivo de la emergencia" — **dato sensible**).
- **Monto solicitado:** obligatorio; **> 0**.
- **Moneda:** obligatoria; **ISO‑4217**; default = moneda de la empresa (RN-06).
- **Fecha de solicitud:** obligatoria; **no futura** (tolerancia +1 día por zona horaria).
- **Estado inicial:** `SOLICITADA` (asignado por el sistema; el solicitante no elige estado).
- Expediente en estado **empleado completado y activo** (RN-13).
- **Elegibilidad (D-08):** el empleado debe cumplir la **antigüedad mínima** configurada por la empresa (en meses), calculada sobre su antigüedad; si el parámetro es 0 o no está configurado, no hay restricción (RN-19).

**Criterios de aceptación:**
- Dado un empleado autenticado sobre su propio expediente y datos válidos, cuando solicita, entonces se persiste con estado `SOLICITADA` y retorna `201 Created` con `ETag`.
- Dado un empleado que intenta crear sobre el expediente de otro **sin** permiso de gestión, entonces `403 Forbidden`.
- Dado tipo inactivo/inexistente → `422`; monto ≤ 0 / fecha futura → `400/422`.

**Prioridad:** Alta. **Dependencias:** RF-001/RF-002; catálogo de monedas; preferencia de empresa; servicio de autoservicio.

---

### RF-004 — Adjuntar documentos de respaldo

**Descripción:** Subir, listar, descargar y eliminar **documentos de respaldo** de una solicitud (constancia médica, acta de defunción, denuncia policial, cotizaciones, fotos del daño, etc.), siguiendo el patrón de adjuntos de `MedicalClaim` / `OffPayrollTransaction`.

**Reglas de negocio:**
- **Uno o varios** adjuntos por solicitud.
- Tipos de archivo y tamaño según la **política de archivos existente**; almacenamiento en Azure Blob.
- Nuevo `FilePurpose.EconomicAidRequestDocument` (hoy el enum no lo tiene; ver §13).
- Clasificación del documento (`DocumentTypeCatalogItem`) **opcional**.
- El empleado puede adjuntar a **su propia** solicitud mientras esté en un estado editable por él (autoservicio); RR. HH. puede adjuntar siempre.

**Criterios de aceptación:** dado una solicitud existente, cuando se sube un respaldo válido, entonces queda asociado, listable y descargable; al eliminarlo, se remueve con auditoría sin afectar la solicitud.

**Prioridad:** Alta. **Dependencias:** infraestructura de archivos (Blob), `FilePurpose`, `DocumentTypeCatalogItem`.

---

### RF-005 — Consultar solicitudes (autoservicio + RR. HH.)

**Descripción:** Listar / filtrar / paginar / consultar por id. **Visibilidad segmentada:** el empleado ve **solo sus propias** solicitudes; RR. HH. (permiso de vista) ve **todas** las del expediente/tenant.

**Reglas de negocio:**
- Lectura de autoservicio resuelta por `LinkedUserPublicId` (RN-15).
- Filtros por **tipo**, **estado**, **rango de fechas**, **moneda** y **texto** (descripción).
- El **detalle** incluye los datos de validación (estado, monto aprobado, observaciones, actor, fecha de resolución) cuando existan.

**Criterios de aceptación:** dado un empleado autenticado, cuando consulta, entonces solo obtiene sus solicitudes; dado RR. HH. con permiso de vista, obtiene todas con metadatos de paginación.

**Prioridad:** Alta. **Dependencias:** RF-003.

---

### RF-006 — Validar la solicitud por RR. HH. (núcleo del requerimiento)

**Descripción:** RR. HH. **revisa y valida** la solicitud cambiando su **estado** (`EN_REVISION`, `PENDIENTE_DOCUMENTACION`, `APROBADA`, `RECHAZADA`) y registrando **monto aprobado**, **observaciones**, **actor** (quién validó) y **fecha de resolución**. Se modela como una **acción** dedicada (no como edición libre de campos) para ser **forward-compatible** con el flujo de aprobación futuro.

**Reglas de negocio:**
- **Solo RR. HH.** (permiso de gestión); **el empleado nunca valida su propia solicitud** (sin auto-aprobación — análogo a `allowSelfApproval=false`, RN-16).
- **Estado destino** válido y activo en catálogo (validado por key).
- Al **aprobar**: `ApprovedAmount` obligatorio y **> 0** (puede ser **parcial**, menor al solicitado; un monto de 0 no es válido — para no otorgar nada se usa `RECHAZADA`, D-05); registra `ResolvedByUserId`, `ResolutionDateUtc`, `ResolutionNotes`.
- Al **rechazar**: `ResolutionNotes` recomendado (motivo); registra actor y fecha.
- `ResolutionDateUtc` **≥ `RequestDateUtc`**.
- **Derivado:** `ResponseTimeDays = ResolutionDateUtc − RequestDateUtc` (días), recalculado en cada resolución (patrón `MedicalClaim.DeriveResponseTimeDays`).
- Transiciones razonables (recomendado, no bloqueante en Fase 1): no resolver una solicitud `ANULADA`/`DESEMBOLSADA`.

**Criterios de aceptación:**
- Dado RR. HH. y una solicitud en `SOLICITADA`/`EN_REVISION`, cuando aprueba con monto válido, entonces el estado pasa a `APROBADA`, se registran actor/fecha/notas/monto y se devuelve `200` con nuevo `ETag`.
- Dado el **propio empleado** intentando validar su solicitud, entonces `403 Forbidden`.
- Dado un monto aprobado negativo o fecha de resolución anterior a la solicitud → `422`.

**Prioridad:** Alta. **Dependencias:** RF-003; permisos.

---

### RF-007 — Registrar el desembolso (informativo)

**Descripción:** Para una solicitud **aprobada**, RR. HH. registra el **desembolso** (estado `DESEMBOLSADA`): **fecha de desembolso**, **monto desembolsado** y **forma de pago** (opcional, reutilizando el catálogo de formas de pago existente). **Informativo**: no ejecuta el pago ni integra con nómina/tesorería en Fase 1.

**Reglas de negocio:**
- Solo desde estado `APROBADA`; solo RR. HH.
- `DisbursedAmount` ≥ 0; `DisbursementDateUtc` ≥ `ResolutionDateUtc`.
- (Futuro) ayuda **reembolsable** → generar deducción en nómina; ayuda **no reembolsable** → posible registro como costo (`OffPayrollTransaction`). Diferido (§18).

**Criterios de aceptación:** dado una solicitud aprobada, cuando se registra el desembolso, entonces el estado pasa a `DESEMBOLSADA` con los datos del pago; intentar desembolsar una no aprobada → `422`.

**Prioridad:** Media. **Dependencias:** RF-006; (opcional) catálogo de formas de pago.

---

### RF-008 — Editar / dar de baja una solicitud (RR. HH.)

**Descripción:** RR. HH. edita campos de negocio (con concurrencia `If-Match`) y **da de baja** (baja lógica). Las correcciones quedan auditadas.

**Reglas de negocio:** mismas validaciones que RF-003; requiere `concurrencyToken` vigente (conflicto → `409`); **sin borrado físico** (RN-10).

**Criterios de aceptación:** token vigente + datos válidos → actualiza y retorna nuevo `ETag`; token desactualizado → `409`.

**Prioridad:** Media. **Dependencias:** RF-003.

---

### RF-009 — Cancelar / retirar la propia solicitud pendiente (autoservicio)

**Descripción:** El empleado puede **cancelar/retirar** una solicitud **propia** que aún esté **pendiente** (`SOLICITADA` o `EN_REVISION` / `PENDIENTE_DOCUMENTACION`), pasándola a `ANULADA`.

**Reglas de negocio:**
- Solo el **propio** empleado (autoservicio) o RR. HH.
- Solo desde estados **no resueltos**; una solicitud `APROBADA`/`RECHAZADA`/`DESEMBOLSADA` no se cancela por el empleado.
- Registra actor y fecha; baja lógica/estado, no borrado.

**Criterios de aceptación:** dado el dueño de una solicitud pendiente, cuando la cancela, entonces pasa a `ANULADA`; intentar cancelar una ya resuelta → `422`.

**Prioridad:** Media (Opcional — ver D-11). **Dependencias:** RF-003/RF-005.

---

### RF-010 — Auditoría de operaciones

**Descripción:** Toda alta/validación/desembolso/edición/baja y operación de adjuntos registra auditoría (actor, fecha, before/after), reutilizando `IAuditService` / `PersonnelFileEmployeeAudits`.

**Prioridad:** Alta. **Dependencias:** servicio de auditoría.

---

### RF-011 — Diseño preparado para el flujo de aprobación futuro

**Descripción:** El modelo debe permitir **incorporar un flujo de aprobación** (multinivel, por umbrales, con notificaciones y delegación) **sin migración disruptiva**.

**Reglas de negocio / criterios de diseño:**
- **Estado** como **catálogo configurable** (RF-002), de modo que agregar estados intermedios (p. ej. `PENDIENTE_APROBACION_NIVEL_2`) no requiera cambios de dominio.
- **Validación como acción** (RF-006) con actor/fecha/notas, no como edición de un campo: mañana esa acción se convierte en el **paso terminal** de un flujo.
- **Reservar** la posibilidad de una entidad hija de **historial de aprobación** (`EconomicAidApprovalStep`: nivel, aprobador, decisión, fecha, comentario) — **no se construye en Fase 1**, pero la entidad raíz no debe impedirlo.
- Campos de resolución (`ResolvedByUserId`, `ResolutionDateUtc`, `ResolutionNotes`, `ApprovedAmount`) ya presentes desde Fase 1 (los consume el futuro flujo).

**Criterios de aceptación:** revisión de diseño confirma que añadir el flujo de aprobación (Fase 2) **no** obliga a recrear la tabla raíz ni a romper contratos existentes.

**Prioridad:** Alta (es un requisito explícito del negocio). **Dependencias:** RF-002, RF-006.

---

### RF-012 — Búsqueda, paginación y exportación

**Descripción:** Consulta paginada con filtros (RF-005) y **exportación** (Excel/CSV) del listado filtrado, con **totalización por moneda** (solicitado y aprobado), sin conversión FX.

**Prioridad:** Media/Baja. **Dependencias:** RF-005; `ReportExportResources`.

---

### RF-013 — Localización bilingüe

**Descripción:** Etiquetas, estados y mensajes de error en **es/en** (`.resx`), con paridad verificada por pruebas.

**Prioridad:** Media. **Dependencias:** convenciones de localización del proyecto.

---

## 7. Requerimientos no funcionales

- **Seguridad / Privacidad:** permisos dedicados; multi-tenant (`TenantId`); **el motivo de la emergencia es dato sensible** → acceso solo a RR. HH. y al propio empleado; control de acceso a la descarga de adjuntos; **sin auto-validación**.
- **Rendimiento:** listados paginados; índices por `PersonnelFileId`, `RequestStatusCode`, `EconomicAidTypeCode`, `RequestDateUtc`, `CurrencyCode`.
- **Disponibilidad / Escalabilidad:** según el API existente; volumen moderado por empleado; adjuntos en Blob.
- **Usabilidad:** selector de tipo; estados claros; autocompletar moneda por defecto; mensajes de validación claros; vista de "mis solicitudes" para el empleado.
- **Auditoría:** trazabilidad completa before/after en cada operación; baja lógica; auditoría de adjuntos.
- **Compatibilidad:** ISO‑4217; fechas UTC normalizadas; **enums como strings**; `api/v1`; código de error en `extensions.code`.
- **Mantenibilidad:** "slice vertical" del módulo + **módulo de reglas puro** testeable (patrón `*.Rules.cs`).
- **Escalabilidad funcional (forward-compatibility):** estado configurable + validación como acción + reserva de historial de aprobación (RF-011).
- **Concurrencia:** `concurrencyToken` por ítem + `If-Match` (faltante → `400`; desactualizado → `409`).
- **Accesibilidad / i18n:** etiquetas y errores bilingües.

---

## 8. Historias de usuario

### HU-001 — Solicitar ayuda económica
Como **empleado**, quiero **solicitar ayuda económica por una emergencia** adjuntando respaldo, para **recibir apoyo de la empresa** de forma ágil y formal.
- Dado que estoy autenticado sobre mi expediente, cuando ingreso tipo, descripción, monto y moneda válidos y adjunto el respaldo, entonces la solicitud queda registrada en estado `SOLICITADA`.

### HU-002 — Validar una solicitud
Como **agente de RR. HH.**, quiero **revisar y validar** la solicitud (aprobar/rechazar/requerir documentación) registrando el **monto aprobado** y observaciones, para **gestionar la asistencia** con trazabilidad.
- Dado una solicitud `SOLICITADA`, cuando la apruebo con monto válido, entonces pasa a `APROBADA` y queda registrado quién y cuándo la aprobó.
- Dado que soy el **solicitante**, cuando intento validar mi propia solicitud, entonces el sistema lo **impide** (`403`).

### HU-003 — Consultar el estado de mi solicitud
Como **empleado**, quiero **ver el estado de mis solicitudes**, para **saber si fue aprobada, rechazada o requiere documentación**.
- Dado que tengo solicitudes, cuando consulto, entonces veo **solo las mías** con su estado actual.

### HU-004 — Registrar el desembolso
Como **agente de RR. HH.**, quiero **registrar el desembolso** de una solicitud aprobada, para **dejar constancia del pago** realizado.
- Dado una solicitud `APROBADA`, cuando registro fecha y monto desembolsado, entonces pasa a `DESEMBOLSADA`.

### HU-005 — Adjuntar y respaldar
Como **empleado / RR. HH.**, quiero **adjuntar documentos de respaldo**, para **sustentar** la emergencia y la decisión.
- Dado una solicitud, cuando subo un documento válido, entonces queda asociado, listable y descargable.

### HU-006 — Cancelar mi solicitud (opcional)
Como **empleado**, quiero **retirar mi solicitud mientras esté pendiente**, para **cancelarla** si la emergencia se resolvió por otra vía.
- Dado una solicitud `SOLICITADA`, cuando la cancelo, entonces pasa a `ANULADA`.

### HU-007 — Configurar catálogos
Como **administrador de catálogos**, quiero **mantener los tipos de ayuda y los estados**, para **estandarizar** el proceso por país.
- Dado permiso de gestión, cuando creo `EMERGENCIA_MEDICA / "Emergencia médica"`, entonces queda activo y disponible.

---

## 9. Reglas de negocio

- **RN-01.** La solicitud está **asociada a un empleado** existente (por id de expediente), no a texto libre.
- **RN-02.** Es **independiente de la nómina**: no afecta cálculos ni pagos; el desembolso es informativo en Fase 1.
- **RN-03.** El **tipo de ayuda** debe existir y estar **activo** en el catálogo al crear/editar (validado por key).
- **RN-04.** El **estado** debe ser un código válido y activo del catálogo de estados; el estado inicial es `SOLICITADA` (asignado por el sistema).
- **RN-05.** El **monto solicitado** es obligatorio y **> 0**. El **monto aprobado**, al aprobar, es **> 0** y puede ser **parcial** (menor al solicitado); un aprobado de 0 no es válido (usar `RECHAZADA`) (D-05).
- **RN-06.** La **moneda** es obligatoria, **ISO‑4217**, validada contra catálogo; default = moneda de la empresa.
- **RN-07.** La **fecha de solicitud** es obligatoria y **no futura**; la **fecha de resolución** (si existe) es **≥** la de solicitud; la **fecha de desembolso** (si existe) es **≥** la de resolución.
- **RN-08.** La **descripción/justificación** (motivo de la emergencia) es obligatoria y se trata como **dato sensible** (acceso restringido).
- **RN-09.** El **`ResponseTimeDays`** se **deriva** de (resolución − solicitud) y se recalcula en cada resolución.
- **RN-10.** No hay **borrado físico**; bajas lógicas y cambios de estado.
- **RN-11.** Toda escritura (incluidos adjuntos y validación) queda **auditada** y protegida por **concurrencia optimista**.
- **RN-12.** La **totalización por moneda** (solicitado/aprobado) se calcula por empleado, con subtotales por `CurrencyCode`, **sin conversión**.
- **RN-13.** Las operaciones requieren expediente en estado **empleado completado y activo**.
- **RN-14.** Aislamiento **multi-tenant** en toda operación.
- **RN-15.** **Autoservicio:** el empleado puede **crear, consultar y (opcional) cancelar SUS PROPIAS** solicitudes (resolución por `LinkedUserPublicId` = usuario actual). RR. HH. con permiso de gestión opera sobre cualquiera.
- **RN-16.** **Sin auto-validación:** el solicitante **no puede validar** (aprobar/rechazar) su propia solicitud (análogo a `allowSelfApproval=false`).
- **RN-17.** Desactivar un **tipo/estado** no afecta solicitudes históricas (se conserva snapshot de la descripción del tipo).
- **RN-18.** (Diseño futuro) El modelo no debe impedir agregar **estados intermedios** ni un **historial de aprobación** (RF-011).
- **RN-19.** **Elegibilidad por antigüedad (D-08):** además de expediente completado y activo, el empleado debe cumplir una **antigüedad mínima configurable** (parámetro a nivel de empresa, en meses), validada con su **antigüedad calculada**; si el parámetro es 0 o no está configurado, no hay restricción.

---

## 10. Flujos principales

### Flujo A — Configurar catálogos
1. Administrador → catálogos → "Tipos de ayuda económica" / "Estados de solicitud" → Nuevo / Editar.
2. Ingresa **Code** + **Descripción**. 3. Sistema valida unicidad/longitud por país. 4. Guarda (activo).

### Flujo B — El empleado solicita ayuda (autoservicio)
1. Empleado → su expediente / portal → "Ayuda económica" → Nueva solicitud.
2. Elige **tipo**, escribe **descripción/justificación**, ingresa **monto** y **moneda**, fecha.
3. (Opcional pero recomendado) **adjunta respaldo**.
4. Sistema valida (autoservicio = su propio expediente; tipo activo; monto > 0; moneda ISO; fecha no futura; expediente completado).
5. **Persiste** con estado `SOLICITADA`, registra **auditoría**, retorna `201 Created` con `ETag`.

### Flujo C — RR. HH. valida
1. RR. HH. abre la bandeja de solicitudes → filtra (estado/tipo/fecha) → abre una.
2. Cambia a `EN_REVISION` (opcional), o resuelve: **APROBADA** (con `ApprovedAmount` + notas) / **RECHAZADA** (con notas) / **PENDIENTE_DOCUMENTACION**.
3. Sistema valida (RR. HH., no auto-validación, estado válido, montos/fechas coherentes), deriva `ResponseTimeDays`, persiste, audita, retorna `200` con nuevo `ETag`.

### Flujo D — Desembolso (informativo)
1. Sobre una solicitud `APROBADA`, RR. HH. registra **fecha** y **monto desembolsado** (forma de pago opcional).
2. Estado → `DESEMBOLSADA`; auditoría.

### Flujo E — Consulta / seguimiento
1. El empleado ve "mis solicitudes" con su estado; RR. HH. ve todas, filtra, exporta (con subtotales por moneda).

### Flujo F — Cancelación por el empleado (opcional)
1. Sobre una solicitud propia pendiente, el empleado la **retira** → `ANULADA`; auditoría.

---

## 11. Flujos alternativos y excepciones

- **E-01 (tipo inválido/inactivo):** `422` `ECONOMIC_AID_TYPE_CODE_INVALID`.
- **E-02 (estado inválido/inactivo):** `422` `ECONOMIC_AID_STATUS_CODE_INVALID`.
- **E-03 (moneda inválida / sin default):** `422` `ECONOMIC_AID_CURRENCY_INVALID` / `ECONOMIC_AID_CURRENCY_REQUIRED`.
- **E-04 (monto solicitado ≤ 0):** `400/422`.
- **E-05 (monto aprobado ≤ 0 al aprobar):** `422` `ECONOMIC_AID_APPROVED_AMOUNT_INVALID`.
- **E-06 (fecha de solicitud futura):** `400/422`.
- **E-07 (fecha de resolución < solicitud / desembolso < resolución):** `422` `ECONOMIC_AID_DATE_INCOHERENT`.
- **E-08 (auto-validación: el solicitante intenta aprobar/rechazar lo suyo):** `403` `ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN`.
- **E-09 (autoservicio sobre expediente ajeno sin permiso):** `403` `Forbidden`.
- **E-10 (desembolsar una no aprobada / resolver una anulada):** `422` `ECONOMIC_AID_STATE_RULE_VIOLATION`.
- **E-11 (expediente no completado/inactivo):** `409/422` `StateRuleViolation`.
- **E-12 (conflicto de concurrencia / If-Match faltante):** `409` / `400`.
- **E-13 (solicitud/adjunto inexistente):** `404` `ItemNotFound`.
- **E-14 (sin permiso / no autenticado):** `403` / `401`.
- **E-15 (Code de catálogo duplicado por país):** `409/422` de unicidad.
- **E-16 (adjunto inválido — tipo/tamaño):** `400/413/422` según política de archivos.
- **E-17 (antigüedad mínima no cumplida):** `422` `ECONOMIC_AID_ELIGIBILITY_NOT_MET`.

---

## 12. Datos requeridos

### Entidad: TipoDeAyudaEconómica (catálogo) — `EconomicAidTypeCatalogItem`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| CountryCatalogItemId / CountryCode | FK / texto | Sí | País válido | Alcance por país |
| Code | Texto (≤80) | **Sí (lo ingresa el usuario)** | Único por país; mayúsculas | Código identificador |
| Name (**Descripción**) | Texto (≤200) | Sí | No vacío | Descripción del tipo |
| IsRefundable | Booleano | No (default `false`) | — | **Diseño futuro (D-13)**: subsidio vs anticipo/préstamo |
| IsActive | Booleano | Sí | — | Baja lógica |
| SortOrder | Entero ≥0 | No | — | Orden de despliegue |
| ConcurrencyToken | GUID | Sí (sistema) | — | Concurrencia |
| CreatedUtc / ModifiedUtc | Fecha | Sí/No | — | Auditoría |

**Seed inicial SV (tipos de ayuda/emergencia):**
`EMERGENCIA_MEDICA`/"Emergencia médica" · `GASTOS_FUNEBRES`/"Gastos fúnebres / fallecimiento de familiar" · `DESASTRE_NATURAL`/"Desastre natural" · `INCENDIO_VIVIENDA`/"Incendio o daño en vivienda" · `CALAMIDAD_DOMESTICA`/"Calamidad doméstica" · `ACCIDENTE`/"Accidente" · `OTRA`/"Otra emergencia".

### Entidad: EstadoDeSolicitud (catálogo) — `EconomicAidStatusCatalogItem`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| (mismos campos de catálogo *country-scoped* que arriba) | | | | |

**Seed inicial SV (estados):**
`SOLICITADA`/"Solicitada" (10) · `EN_REVISION`/"En revisión" (20) · `PENDIENTE_DOCUMENTACION`/"Pendiente de documentación" (30) · `APROBADA`/"Aprobada" (40) · `RECHAZADA`/"Rechazada" (50) · `DESEMBOLSADA`/"Desembolsada" (60) · `ANULADA`/"Anulada" (70).

### Entidad: SolicitudDeAyudaEconómica — `PersonnelFileEconomicAidRequest`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| PersonnelFileId | FK (long) | Sí | Expediente existente, **completado y activo** | Empleado solicitante |
| EconomicAidTypeCode | Texto (≤80) | Sí | Existe y **activo** en catálogo | Tipo de ayuda/emergencia |
| TypeNameSnapshot | Texto (≤200) | No (recomendado) | — | Snapshot de la descripción del tipo |
| RequestStatusCode | Texto (≤80) | Sí | Válido/activo; inicia `SOLICITADA` | Estado del ciclo de vida |
| Description (**justificación**) | Texto (≤2000) | Sí | No vacío | Motivo de la emergencia (**dato sensible**) |
| RequestedAmount | Decimal(18,2) | Sí | **> 0** | Monto solicitado |
| CurrencyCode | Texto (3) | Sí | ISO‑4217 (catálogo) | Moneda |
| RequestDateUtc | Fecha (UTC) | Sí | No futura; normalizada | Fecha de la solicitud |
| ApprovedAmount | Decimal(18,2)? | No | **≥ 0** si existe | Monto aprobado por RR. HH. |
| ResolvedByUserId | GUID? | No | — | **Quién validó** (agente de RR. HH.) |
| ResolutionDateUtc | Fecha (UTC)? | No | **≥** RequestDateUtc | Fecha de la validación |
| ResolutionNotes | Texto (≤2000)? | No | — | Observaciones / motivo de rechazo |
| ResponseTimeDays | Entero? | No (**derivado**) | — | (Resolución − solicitud) en días |
| DisbursedAmount | Decimal(18,2)? | No | **≥ 0** si existe | Monto desembolsado (informativo) |
| DisbursementDateUtc | Fecha (UTC)? | No | **≥** ResolutionDateUtc | Fecha del desembolso |
| PaymentMethodCode | Texto? | No | Catálogo de formas de pago (opcional) | Forma de pago del desembolso |
| IsActive | Booleano | Sí | — | Baja lógica |
| ConcurrencyToken | GUID | Sí (sistema) | — | Concurrencia |
| CreatedAtUtc / ModifiedAtUtc | Fecha | Sí/No | — | Auditoría |

### Entidad: DocumentoDeRespaldo — `EconomicAidRequestDocument` (adjunto)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| EconomicAidRequestId | FK | Sí | Solicitud existente | Solicitud a la que pertenece |
| StoredFileId / ruta Blob | ref | Sí | Política de archivos | Archivo en Azure Blob (`FilePurpose.EconomicAidRequestDocument`) |
| DocumentTypeCode | Texto | No | Catálogo (opcional) | Clasificación opcional |
| FileName / ContentType / SizeBytes | varios | Sí | Tipo/tamaño permitidos | Metadatos del archivo |
| IsActive / ConcurrencyToken | Bool / GUID | Sí | — | Baja lógica / concurrencia |
| CreatedAtUtc | Fecha | Sí | — | Auditoría |

### Entidad (DIFERIDA, Fase 2): PasoDeAprobación — `EconomicAidApprovalStep`

> **No se construye en Fase 1.** Se documenta para que la entidad raíz **no cierre la puerta**: nivel, aprobador (`ApproverUserId`), decisión, fecha, comentario, orden. El flujo de aprobación multinivel se apoyará en esta tabla hija + en los campos de resolución ya presentes.

### Parámetro de configuración — Antigüedad mínima (D-08)

| Parámetro | Tipo | Ubicación | Descripción |
|---|---|---|---|
| Antigüedad mínima para solicitar | Entero (meses) | **Nivel de empresa** (`CompanyPreference`) | Antigüedad mínima requerida para que un empleado pueda solicitar ayuda. **0 o sin configurar ⇒ sin restricción.** Se valida contra la **antigüedad calculada** del empleado (RN-19). |

---

## 13. Integraciones necesarias

**Servicios reutilizables accedidos por KEY (requisito explícito — reutilizar lo existente):**

- **Validación de catálogos por código:** `IPersonnelFileRepository.CatalogCodeIsActiveAsync(companyId, category, code)` con categorías `ECONOMIC_AID_TYPE_CATALOG` y `ECONOMIC_AID_STATUS_CATALOG` (mismo patrón que `ACTION_TYPE_CATALOG`, `MEDICAL_CLAIM_STATUS_CATALOG`, etc.).
- **Snapshot de nombre:** `IPersonnelFileRepository.GetCatalogItemNameAsync(companyId, category, code)` para persistir la descripción del tipo.
- **Moneda por defecto de la empresa:** `ICompanyPreferenceRepository` (default de `CurrencyCode`) + catálogo `CurrencyCatalogItem` (ISO‑4217).
- **Autoservicio (resolución del empleado):** patrón `LoadForCreateOwnOrManage…` (`PersonnelFile.LinkedUserPublicId` == `ICurrentUserService.UserId`), idéntico al de `MedicalClaims`.
- **Elegibilidad por antigüedad (D-08):** **antigüedad calculada** del empleado (perfil de empleado / antigüedad computada del expediente) + **parámetro de antigüedad mínima** en `CompanyPreference` (reutiliza el repositorio de preferencias de empresa).
- **Catálogos del módulo:** `GeneralCatalogItem` + `GeneralCatalogKeyMap` + seed `HasData` (wire-keys `economic-aid-types`, `economic-aid-statuses`).
- **Almacenamiento de archivos (Azure Blob)** + nuevo **`FilePurpose.EconomicAidRequestDocument`** (hoy el enum tiene `ProfileImage, PersonnelDocument, ReportExport, CompanyLogo, Attachment, MedicalClaimDocument, OffPayrollTransactionDocument`) + `DocumentTypeCatalogItem` (clasificación opcional). Registrar el nuevo `FilePurpose` en `appsettings` (gotcha conocido).
- **Servicio de auditoría:** `IAuditService` / `PersonnelFileEmployeeAudits` (before/after).
- **Concurrencia:** `ConditionalRequestResultFilter` (`If-Match`/`ETag`).
- **Autenticación/Autorización (IAM):** permisos dedicados (`AuthorizationPolicySet` a nivel de clase → **controlador dedicado**).
- **(Opcional) Catálogo de formas de pago** (ya implementado) para la forma de pago del desembolso.
- **Servicio de exportación** (`ReportExportResources`) para RF-012.

**Integraciones FUTURAS (fuera de alcance):**
- **Nómina / `OffPayrollTransaction`:** desembolso real; ayuda **reembolsable** → deducción en nómina; ayuda **no reembolsable** → costo registrado como `OffPayrollTransaction`.
- **Motor de aprobaciones / notificaciones / delegación** (`AuthorizationSubstitution`) para el flujo de Fase 2.

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Empleado (autoservicio)** | Crear, consultar y (opcional) cancelar **sus propias** solicitudes; adjuntar respaldo a las propias | Solo su expediente (`LinkedUserPublicId`); **no valida**; no ve solicitudes ajenas |
| **Gestor / Agente RR. HH.** | **Validar** (aprobar/rechazar/requerir-doc), registrar desembolso, crear en nombre, editar, dar de baja, consultar y exportar (`PersonnelFiles.ManageEconomicAidRequests`) | Solo su tenant; expediente completado; **sin auto-validación** |
| **Supervisor / Gerente (consulta)** | Consulta/exportación (lectura) (`PersonnelFiles.ViewEconomicAidRequests`) | Sin escritura |
| **Administrador de catálogos** | Crear/editar/activar/desactivar tipos y estados | Solo su tenant; estados son estructurales |
| **Aprobador (FUTURO)** | `PersonnelFiles.ApproveEconomicAidRequests` (Fase 2) | Por nivel/umbral; con delegación |
| **Sistema** | Validación, autoservicio, auditoría, concurrencia, almacenamiento | — |

> **Nota técnica:** seguir el patrón `MedicalClaims`: `AuthorizationPolicySet` a nivel de **clase** (superset *authn-only*) sobre un **controlador dedicado** (`EconomicAidRequestsController`), y el **chequeo fino** (Manage/View/Admin **o** autoservicio) en los handlers. En Fase 1 la **validación** vive dentro de `Manage`; en Fase 2 se separa un permiso **`Approve`** dedicado (D-03).

---

## 15. Criterios de aceptación generales

- **CA-01.** Catálogos de **tipos** y **estados** administrables, *country-scoped* y **semillados para SV**.
- **CA-02.** El empleado puede **solicitar** ayuda (autoservicio) con todas las validaciones; estado inicial `SOLICITADA`.
- **CA-03.** RR. HH. puede **validar** (aprobar/rechazar/requerir-doc) con **monto aprobado**, **observaciones**, **actor** y **fecha**; **el solicitante no puede auto-validar**.
- **CA-04.** Adjuntar/listar/descargar/eliminar **documentos de respaldo**.
- **CA-05.** Visibilidad segmentada: empleado ve **solo lo suyo**; RR. HH. ve **todo**.
- **CA-06.** Registro de **desembolso** informativo sobre solicitudes aprobadas.
- **CA-07.** **Auditoría + concurrencia** en todas las escrituras; **baja lógica**.
- **CA-08.** Las solicitudes **no** afectan la nómina ni sus reportes (desembolso informativo).
- **CA-09.** El **motivo** se trata como **dato sensible**; permisos dedicados; aislamiento multi-tenant.
- **CA-10.** **Diseño verificado como forward-compatible** con el flujo de aprobación (estado configurable + validación como acción + reserva de historial).
- **CA-11.** Mensajes/etiquetas **bilingües**; pruebas (unitarias de reglas + integración) en verde.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R-01 (privacidad / dato sensible):** el motivo de la emergencia (salud, fallecimiento) es sensible. Mitigación: acceso solo RR. HH. + autoservicio propio, permiso dedicado, control de descarga de adjuntos, auditoría.
- **R-02 (expectativa de aprobación):** el negocio espera, a futuro, un flujo de aprobación. Mitigación: diseñar forward-compatible (RF-011) y comunicar que Fase 1 es validación de un paso.
- **R-03 (confusión con módulos "primos"):** riesgo de mezclar con `PayrollTransaction` / `OffPayrollTransaction` / `MedicalClaim` / conceptos `ANTICIPO/PRESTAMO_*`. Mitigación: nomenclatura `EconomicAid…`, este documento, revisión de diseño.
- **R-04 (alcance del dinero / reembolsabilidad):** si se cuelan anticipos/préstamos, aparece repago/saldos/intereses. Mitigación: Fase 1 **no reembolsable**; reembolsables diferidos con `IsRefundable` en el tipo (D-13).
- **R-05 (desembolso sin pago real):** el desembolso es informativo; riesgo de interpretarlo como pago ejecutado. Mitigación: etiquetar claramente como registro informativo; integración a nómina/tesorería diferida.
- **R-06 (taxabilidad / cumplimiento):** ciertas ayudas pueden tener implicaciones fiscales. Mitigación: fuera de alcance; levantar con legal/nómina (§17).
- **R-07 (abuso / equidad):** sin topes ni elegibilidad, riesgo de uso desigual. Mitigación: auditoría + validación humana; topes/elegibilidad configurables a futuro (D-07/D-08).

### Supuestos
- **S-01.** No existe motor de nómina; el desembolso es informativo.
- **S-02.** Se reutilizan convenciones del módulo (catálogos por key, autoservicio, CQRS, auditoría, adjuntos, concurrencia, moneda por defecto).
- **S-03.** El empleado con autoservicio tiene `LinkedUserPublicId` (usuario vinculado).
- **S-04.** Catálogo de monedas y almacenamiento Blob disponibles.
- **S-05.** Una solicitud corresponde a un empleado (sin prorrateo colectivo).

### Dependencias
- **DP-01.** Convenciones de catálogos *country-scoped* + seed `HasData`.
- **DP-02.** Servicios de auditoría, exportación, autorización y **archivos (Blob)**.
- **DP-03.** Servicio de **autoservicio** (`LinkedUserPublicId`) y **preferencia de empresa** (moneda).
- **DP-04.** Permisos IAM (`View/ManageEconomicAidRequests`; futuro `Approve`).

---

## 17. Decisiones ratificadas (cierre de preguntas abiertas)

> **Ratificadas el 2026-06-27.** El documento ya incorpora estas decisiones. El único cambio de alcance respecto a la propuesta inicial es **D-08** (antigüedad mínima configurable).

| ID | Pregunta | **Decisión del negocio (ratificada)** | Implicación |
|---|---|---|---|
| **D-01/D-13** | Modalidades cubiertas (subsidio vs anticipo/préstamo) | **Solo subsidio NO reembolsable** (emergencia) | Anticipos/préstamos **diferidos** (requieren repago); `IsRefundable` queda **parametrizado** en el tipo para el futuro. |
| **D-02** | ¿Autoservicio del empleado? | **Sí** (patrón `MedicalClaims`) | El empleado crea/consulta lo suyo; RR. HH. puede crear en su nombre. |
| **D-03** | Permiso de validación | **Dentro de `Manage` en Fase 1** | `Approve` dedicado llega con el flujo (Fase 2); **siempre sin auto-validación**. |
| **D-04** | ¿Estado como catálogo o enum? | **Catálogo** *country-scoped* | Configurable y forward-compatible; seed SV. |
| **D-05** | Monto aprobado | **Parcial permitido; 0 no** | Al aprobar, `ApprovedAmount` **> 0**; no otorgar nada ⇒ `RECHAZADA`. |
| **D-06** | Adjunto al solicitar | **Opcional; RR. HH. puede requerirlo** | Estado `PENDIENTE_DOCUMENTACION` para solicitar respaldo; nuevo `FilePurpose`; clasificación opcional. |
| **D-07** | Topes / presupuesto | **Sin topes** | Discrecional; control por validación humana y auditoría. |
| **D-08** | Elegibilidad | **Activo + completado + antigüedad mínima configurable** | Parámetro a nivel de empresa (`CompanyPreference`, en meses); **0/sin configurar = sin restricción**; valida la antigüedad calculada (RN-19). |
| **D-09** | Desembolso/pago | **Informativo** | Estado `DESEMBOLSADA` + fecha/monto/forma de pago; integración nómina/tesorería **diferida**. |
| **D-10** | Confidencialidad del motivo | **Dato sensible** | Solo RR. HH. + autoservicio propio; permiso dedicado. |
| **D-11** | Cancelación por el empleado | **Sí, la propia y solo si está pendiente** | → `ANULADA` desde estados no resueltos. |
| **D-12** | Motivo de rechazo | **Texto libre** (`ResolutionNotes`) | Catálogo de motivos diferido/opcional. |
| **D-14** | Seed SV de tipos | **Las 7 categorías propuestas** (§12) | Catálogo administrable: se pueden agregar más luego (p. ej. `EDUCATIVA`, `VIVIENDA`). |
| **D-15** | Taxabilidad / tratamiento fiscal | **Fuera de alcance** | Levantar con legal/nómina para fases futuras. |
| **D-16** | Notificaciones por cambio de estado | **Fuera de alcance** Fase 1 | Sin motor de notificaciones acoplado. |

### Ajuste de alcance derivado de la ratificación

- **D-08 (antigüedad mínima):** única decisión que **amplió** el alcance respecto a la propuesta inicial. Se incorpora un **parámetro configurable a nivel de empresa** (antigüedad mínima en meses) y una **regla de elegibilidad** (RN-19) que valida la **antigüedad calculada** del empleado al solicitar. Si el parámetro es **0 o no está configurado, no hay restricción** (comportamiento equivalente al baseline). Reutiliza el cálculo de antigüedad ya existente en el perfil del empleado y el repositorio de preferencias de empresa.

---

## 18. Recomendaciones del Analista de Negocio

1. **Construir como módulo nuevo (net-new)**, sin reutilizar `PayrollTransaction` / `OffPayrollTransaction` / `MedicalClaim`. Combinar tres plantillas: **comportamiento** (`MedicalClaims`: autoservicio + estado + resolución + adjuntos), **datos financieros** (`OffPayrollTransaction`: monto + moneda + tipo-catálogo + snapshots), **estado de aprobación** (`SalaryTabulatorChangeRequest`: acción de validación con actor/fecha/comentario y **sin auto-aprobación**).
2. **Nomenclatura explícita:** entidad `PersonnelFileEconomicAidRequest`, catálogos `EconomicAidTypeCatalogItem` / `EconomicAidStatusCatalogItem`, rutas `/personnel-files/{id}/economic-aid-requests`, wire-keys `economic-aid-types` / `economic-aid-statuses`, `FilePurpose.EconomicAidRequestDocument`.
3. **Validar todo por key contra catálogo** (`CatalogCodeIsActiveAsync`) y **snapshotear** la descripción del tipo (`GetCatalogItemNameAsync`) — reutilizando los servicios existentes.
4. **Diseñar forward-compatible para el flujo de aprobación (OB-06/RF-011):** estado **configurable** (catálogo), validación como **acción** (`POST …/resolution`) con actor/fecha/notas/monto, y **reservar** la entidad hija `EconomicAidApprovalStep` (no construirla). Así, Fase 2 no requiere migración disruptiva.
5. **Tratar el motivo como dato sensible:** permiso dedicado, visibilidad segmentada (autoservicio ve lo propio), control de descarga de adjuntos.
6. **Mantener el desembolso informativo** en Fase 1; documentar el puente futuro: reembolsable → deducción de nómina; no reembolsable → `OffPayrollTransaction` (costo).
7. **Catálogos parametrizados con seed SV vía `HasData`** (no `DevSeedService`, que no rellena tenants existentes y produce `404` en ambientes desplegados — lección de incidentes previos). Usar IDs negativos en un rango libre y `PublicId` deterministas.
8. **Secuencia de PRs propuesta:**
   - **PR-1.** Catálogos (`EconomicAidTypeCatalogItem` + `EconomicAidStatusCatalogItem`) + wire-keys + **seed SV** (`HasData`) + permisos de catálogo.
   - **PR-2.** Entidad `PersonnelFileEconomicAidRequest` + migración + configuración EF + índices.
   - **PR-3.** CQRS de **solicitud y consulta** + **módulo de reglas puro** (`*.Rules.cs`) + errores bilingües + **validación por key** + **autoservicio** (`LinkedUserPublicId`) + moneda por defecto.
   - **PR-4.** **Controlador dedicado** `EconomicAidRequestsController` + rutas + `If-Match` + permisos `View/ManageEconomicAidRequests`.
   - **PR-5.** **Validación por RR. HH.** (acción `…/resolution`: aprobar/rechazar/requerir-doc con actor/fecha/notas/monto, **sin auto-validación**) + **desembolso** informativo.
   - **PR-6.** **Adjuntos** (`FilePurpose.EconomicAidRequestDocument`, subir/listar/descargar/eliminar) reutilizando la infraestructura de archivos.
   - **PR-7.** (Opcional) **Cancelación por el empleado** + **búsqueda/paginación/exportación** con subtotales por moneda.
   - **PR-8.** Pruebas de integración + **paridad de localización** (es/en) + guía de integración frontend.
9. **MVP mínimo (si se requiere recortar):** PR-1…PR-5 (solicitar + validar + adjuntar). Búsqueda/exportación, cancelación y desembolso pueden ir después.
10. **Fase 2 (futuro):** flujo de aprobación multinivel (umbrales por monto, ruteo, **notificaciones**, **delegación** vía `AuthorizationSubstitution`, **historial de aprobación**), ayudas **reembolsables** con repago en nómina, **topes/elegibilidad** configurables, **taxabilidad**, dashboards.
11. **Decisiones §17 ratificadas (2026-06-27):** listo para plan técnico. Incorporar en PR-3 la **regla de elegibilidad por antigüedad mínima** (parámetro de empresa en `CompanyPreference`, D-08) y la **aprobación parcial con monto > 0** (D-05).

---

## Anexo — Evidencia de la validación (trazabilidad a código)

| Hallazgo | Evidencia |
|---|---|
| No existe feature de "ayuda/asistencia económica" | Búsqueda nula de `EconomicAid`/`FinancialAssistance`/`AyudaEconomica`/`HardshipAid` y de una **solicitud del empleado de dinero** en `src/` y `docs/` |
| `ANTICIPO`/`PRESTAMO_*` son **conceptos de nómina**, no solicitudes | `DevSeedService` (conceptos de compensación/deducción), no una entidad de solicitud |
| Plantilla de **comportamiento** (autoservicio + estado + resolución + adjuntos) | `PersonnelFileMedicalClaim` (`src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs:994-1244`); `…/Compensation/MedicalClaims*.cs`; gate de autoservicio `LoadForCreateOwnOrManageMedicalClaimAsync` (`…/Common/PersonnelFileEmployeeHandlerBases.cs`) |
| Plantilla de **datos financieros** (monto/moneda/tipo-catálogo/snapshots/adjuntos) | `PersonnelFileOffPayrollTransaction` (`PersonnelFileEmployee.cs:1384-1533`); `OffPayrollTransactionTypeCatalogItem` (`GeneralCatalogItems.cs:254-280`); migración `20260624191922` |
| Plantilla de **estado de aprobación** (única máquina de estados real) | `SalaryTabulatorChangeRequest` (`src/CLARIHR.Domain/SalaryTabulator/…`): `Draft→Submitted→Approved/Rejected/Canceled`, `Approve(decidedBy, decidedAt, comment, allowSelfApproval)` |
| **No existe motor de aprobaciones genérico**; el estado de `MedicalClaim` es catálogo *country-scoped* validado por key | `MEDICAL_CLAIM_STATUS_CATALOG` (`GlobalCatalogSeedData.cs:502-512`) |
| Validación por key reutilizable | `IPersonnelFileRepository.CatalogCodeIsActiveAsync` / `GetCatalogItemNameAsync` (`…/Abstractions/PersonnelFiles/IPersonnelFileRepository.cs`) |
| Seed por país vía `HasData` (no `DevSeedService`) | `GlobalCatalogSeedData.cs` (helper `CreateGeneralCatalogSeed`, IDs negativos, `PublicId` deterministas) |
| `FilePurpose` actual (sin valor para esta feature → agregar uno) | `src/CLARIHR.Domain/Files/FileEnums.cs` (`…, MedicalClaimDocument, OffPayrollTransactionDocument`) |
| `AuthorizationPolicySet` es **class-only** → controlador dedicado | `MedicalClaimsController` / `OffPayrollTransactionsController` (`AuthorizationPolicySet` a nivel de clase; chequeo fino en handlers) |

---

> **Cierre:** El requerimiento **no estaba implementado** (net-new); los módulos "primos" (`PayrollTransaction`, `OffPayrollTransaction`, `MedicalClaim`, conceptos `ANTICIPO/PRESTAMO`) son **conceptos distintos** y **no se reutilizan** (la ayuda económica es un **flujo entrante: el empleado pide, la empresa decide y paga, RR. HH. valida**). Con las **decisiones D-01…D-16 ratificadas (2026-06-27)**, el requerimiento de negocio queda **cerrado**: **módulo nuevo** que reutiliza las **convenciones y servicios por key** existentes, con **catálogos parametrizables y seed SV (7 tipos)**, **autoservicio** del empleado, **validación por RR. HH. de un paso** (parcial > 0, **sin topes**, con **antigüedad mínima configurable**), **subsidio no reembolsable**, desembolso **informativo**, motivo **sensible**, y un **diseño explícitamente preparado** para el **flujo de aprobación futuro** (sin construirlo). Listo para el **plan técnico** y la secuencia de 8 PRs.
