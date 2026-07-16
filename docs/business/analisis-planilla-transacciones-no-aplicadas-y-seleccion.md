# Análisis de negocio — Planilla: transacciones que no se aplicaron (aplicar en otro periodo) y transacciones que aplican en el cálculo

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra el análisis/plan **RATIFICADOS** de REQ-012 + verificación de código de los 7 módulos fuente) |
| **Módulo** | Planilla — la **cara transaccional** del motor: (1) transacciones que no se aplicaron en una planilla y deben aplicarse en otra planilla de otro periodo · (2) selección de las transacciones que se aplican o se ignoran en el cálculo |
| **Fecha** | 2026-07-15 |
| **Autor** | Equipo CLARIHR — análisis asistido, validado contra `analisis-planilla-generacion.md` (RATIFICADO 2026-07-14) + `plan-tecnico-planilla-generacion.md` (escrito 2026-07-14) + código (rama `feature/planilla-descuentos`, HEAD `597aa75`) |
| **Estado** | **RATIFICADO 2026-07-15 — P-01…P-04 aceptadas TAL CUAL** («aceptar todas las recomendaciones»): P-01 selección sobre la planilla `GENERADA` (el pre-flight es la vista previa) · P-02 ignorar POR TRANSACCIÓN (sin revivir «aplicación»/«parámetros especiales») · **P-03 ARRASTRE AUTOMÁTICO de TNT/amonestaciones con advertencia, sin límite de antigüedad en F1 (los pools NO se auto-mueven)** · P-04 pre-flight + bandejas existentes (sin bandeja unificada). **Delta PROPAGADO al plan técnico de REQ-012 (2026-07-15): §0.11 arrastre · §1.4 índice de consumo derivado (M4/PR-4) · §3.4 candidatos+pre-flight · §3.5 test de liberación · §5 `PAYROLL_WARNING_CARRYOVER_INPUT` · §6 PR-4/PR-5 · §8 adopción.** Cero condiciones de negocio pendientes; resta solo la verificación de correspondencia al cerrar REQ-012 PR-5/PR-6/PR-8 |
| **Naturaleza del requerimiento** | **SUBCONJUNTO de REQ-012 + 1 delta quirúrgico (RATIFICADO).** De los 7 requerimientos funcionales de este levantamiento, **6 ya están construidos-certificados (REQ-005…011) o ratificados-planificados (REQ-012)**. El único alcance genuinamente nuevo es el **arrastre de insumos sin destino declarado** (tiempos no trabajados y amonestaciones registrados tarde, cuyo periodo ya cerró): una regla de consulta + una advertencia, **cero entidades, cero migraciones, cero seeds** — ratificado (P-03) y propagado al plan: índice en M4 (PR-4), arrastre+pre-flight en PR-5 |
| **Documentos hermanos** | [`analisis-planilla-generacion.md`](analisis-planilla-generacion.md) (REQ-012 — el motor; sus RF-007/008/010/011/012 son la cobertura de la opción 2) · [`analisis-planilla-estado-revision-autorizacion.md`](analisis-planilla-estado-revision-autorizacion.md) (REQ-013 — precedente de levantamiento-subconjunto) · [`plan-tecnico-planilla-generacion.md`](../technical/plan-tecnico-planilla-generacion.md) (§3.4/§3.5 consumo de pools · §3.6 revisión/exclusión) · análisis de los módulos fuente: REQ-005/006/007 (ingresos y HE), REQ-008…011 (descuentos, endeudamiento y TNT), REQ-003 (amonestaciones) |
| **Alcance geográfico** | El Salvador (SV) — mismo corte de toda la cadena |

---

## Contexto del cambio (requerimiento original)

> **Transacciones que no se aplicaron en planilla** — Esta opción deberá permitir agregar transacciones que no se agregaron en la planilla, para que sean aplicadas en otra planilla de otro periodo. Entre las transacciones que se podrán agregar están: horas extras, otros ingresos, otros descuentos, tiempos no trabajados y amonestaciones.
>
> **Transacciones que aplican en planilla** — Esta opción deberá proporcionar los elementos necesarios para seleccionar las opciones que se aplicarán o ignorarán en el cálculo de planilla, para lo cual deberá seleccionarse de los listados y elegir una opción según se requiera.

**Linaje del pedido.** No es la primera vez que el programa ve estas dos frases: el levantamiento original de REQ-005 (2026-07-05) ya pedía «consultar **transacciones que no se aplicaron en planilla**, así como las acciones que se quieren aplicar en el cálculo del **periodo de planilla activo** y las que se quieren **enviar a otro periodo**», y el flujo a–f de REQ-012 (paso b) enumeró literalmente las mismas familias: «descuentos, ingresos, permisos, horas extras, bonos, **amonestaciones, tiempos no trabajados**, etc.». Este levantamiento es la **especificación detallada de esa capacidad**, igual que REQ-013 lo fue de la cara de consulta/decisión.

---

## 0. Veredicto ejecutivo

1. **La opción 2 («transacciones que aplican en planilla») es SUBCONJUNTO PURO de REQ-012 — cero desarrollo nuevo.** El modelo ratificado 2026-07-14 ya contiene los tres momentos de la selección: **antes** de generar, el pre-flight lista lo que entrará y advierte huecos (RF-007); **al** generar, se elige población completa o subconjunto `employeeIds[]` (RF-008); **después** de generar, cada línea de la corrida se puede **excluir/incluir** (`IsIncluded`) u **override** con nota (RF-010), con recálculo selectivo (RF-012) y regeneración (RF-011). Todo aterriza en PR-5/PR-6 del plan técnico existente.
2. **«Ignorar» y «enviar a otro periodo» son el MISMO mecanismo — la unificación ya está ratificada.** El plan §3.6 lo dice textual: «**excluir línea de pool revierte su aplicación**». Una transacción ignorada en la corrida vuelve a quedar **pendiente en su módulo fuente** y, por tanto, disponible para la siguiente planilla (o para re-imputarla a un periodo concreto). La opción 1 y la opción 2 del levantamiento son dos caras de la misma moneda, y el motor incluso la usa solo: el paso [7] del pipeline difiere cuotas voluntarias LIFO para cumplir el ingreso mínimo (`PAYROLL_WARNING_INSTALLMENT_DEFERRED` — «la cuota queda pendiente en su pool»).
3. **La opción 1 ya está construida y certificada para los 5 pools.** Verificado en código: cada pool lleva su maquinaria completa de «no aplicado → otro periodo»: bandejas de **pendientes/atrasados** tituladas literalmente «no aplicados en planilla» (REQ-006 RF-010, REQ-007 RF-010, REQ-008/009 RF-011), **re-imputación del periodo destino** («enviar a otro periodo»: REQ-006 RF-005, REQ-007 RF-008, REQ-009 RF-016), aplicación **en lote con exclusión = posponer** (REQ-005/006/007/008/009), y **registro tardío sin bloqueo** (ningún gate impide registrar con fechas pasadas; REQ-007 P-02 lo permite explícito). El motor de REQ-012 consume lo pendiente con origen `MOTOR` (D-09 — que incluye «cuotas **vencidas**»: el arrastre de cuotas cíclicas atrasadas ya está ratificado) y las bandejas marcan «atrasado» cuando el periodo destino venció.
4. **El ÚNICO hueco real: tiempos no trabajados y amonestaciones no tienen destino de planilla NI marca de consumo — un registro tardío se perdería.** Verificado en código: `PersonnelFileNotWorkedTime` expone `DiscountAmount` calculado pero **solo fechas** (`StartDate`/`EndDate`), sin `PayrollPeriodId` ni `PayrollTypeCode` ni hija de aplicación; `PersonnelFileDisciplinaryAction` tiene bloque monetario completo (`HasPayrollDeduction`/`DeductionAmount`/concepto de egreso snapshot) pero es **documental** (journal), sin cableado alguno a periodos. El plan de REQ-012 los lee «del rango» del periodo (§3.4): un TNT o amonestación con fechas de un periodo **cuya planilla ya cerró** no entraría en ninguna corrida futura — exactamente el caso que la opción 1 pide cubrir. **Propuesta (P-03 — RATIFICADA 2026-07-15): arrastre automático** — la corrida incluye también los registros anteriores al periodo **no consumidos por ninguna corrida activa** (consumo derivado de las líneas `SourceModule`+`SourceReferencePublicId` que el modelo M4 ya trae), con advertencia visible por línea y excluibles en revisión (= volver a posponer). Costo: una regla de consulta + una advertencia; **cero entidades, cero migraciones, cero seeds; cero toques a los módulos certificados REQ-011/REQ-003**.
5. **Los pools NO se arrastran solos — y eso es una virtud, no un hueco.** Un eventual o una jornada HE imputados a un periodo vencido **declararon un destino** por decisión del usuario; moverlos en silencio cambiaría esa decisión. La ruta ratificada/construida: la bandeja los marca **atrasados**, el pre-flight de REQ-012 advierte «pools con elementos vencidos de periodos anteriores» (RF-007), y el usuario **re-imputa** al periodo que corresponda (operación construida y certificada). La línea divisoria propuesta: **lo que tiene destino declarado se re-imputa a mano; lo que no tiene destino (TNT/amonestaciones) se arrastra solo** — con la excepción ya ratificada de las cuotas cíclicas vencidas (su «destino» es un plan de amortización, no una decisión por periodo).
6. **La opción 2 podría ser el difunto campo «aplicación» del maestro — no revivirlo sin ejemplos.** En la ratificación de REQ-012 el negocio **eliminó** «aplicación (lista desplegable)» (P-02: «no lo tenemos claro») e **ignoró** los «parámetros especiales» (P-03). Si con «seleccionar las opciones que se aplicarán o ignorarán» el cliente quisiera decir una **configuración por nómina** (p. ej. «esta planilla nunca toma horas extras»), estaríamos ante el significado original de aquel campo. La recomendación (P-02 de este documento) es mantener la selección **por transacción en la corrida** (ratificada) y solo considerar el apagado por categoría como parámetro aditivo de la generación en F2, si el negocio lo confirma con un caso real.
7. **Registro como REQ-014: sin PRs propios.** Las coberturas se entregan con REQ-012 (PR-5 generación/pools/pre-flight · PR-6 revisión/exclusión · PR-7 bandeja; guía FE con PR-8). El delta del arrastre (№4) quedó **ratificado (P-03, 2026-07-15) y PROPAGADO al plan técnico de REQ-012** el mismo día (§0.11 + §1.4 índice en M4 + §3.4 + §3.5 test de liberación + §5 warning + §6 PR-4/PR-5 + §8 adopción); se construye dentro de PR-4 (índice) y PR-5 (arrastre). La «Próxima acción» del programa **no cambia: REQ-012 PR-1, sin bloqueos**.

### Trazabilidad frase a frase del levantamiento

| Frase del requerimiento | Cobertura (construido / REQ-012 ratificado / delta) |
|---|---|
| «permitir **agregar** transacciones que no se agregaron en la planilla» | **Construido**: los 7 módulos fuente aceptan registro con fechas pasadas (ningún gate lo impide; explícito en REQ-007 P-02). Con REQ-012, la única restricción nueva es la ventana de ingreso de HE **del periodo destino** (RF-006) — y la ruta sancionada para una HE tardía es imputarla al siguiente periodo abierto, que es exactamente lo que pide esta opción |
| «para que sean **aplicadas en otra planilla de otro periodo**» — horas extras | **Construido + ratificado**: re-imputación del destino en `AUTORIZADA` (REQ-007 RF-008 «enviar a otro periodo») + bandeja de no aplicadas con marca de atrasada (RF-010) → la corrida del nuevo periodo la consume con origen `MOTOR` (REQ-012 D-09/PR-5) |
| — otros ingresos | **Construido + ratificado**: eventuales pendientes re-imputables (REQ-006 RF-005) con bandeja «no aplicados en planilla» (RF-010); cuotas cíclicas **vencidas** se arrastran solas (REQ-012 D-09 las consume; pre-flight advierte — RF-007) |
| — otros descuentos | **Construido + ratificado** (espejo): eventuales re-imputables (REQ-009 RF-016), bandejas de cuotas pendientes/vencidas (REQ-008 RF-011), cuotas vencidas consumidas por D-09; las diferidas por ingreso mínimo quedan pendientes para la siguiente corrida (REQ-012 §2.2 paso [7]) |
| — **tiempos no trabajados** | **DELTA (P-03)**: sin destino ni marca de consumo (`PersonnelFileNotWorkedTime` solo tiene fechas) → **arrastre automático** propuesto: la corrida incluye los REGISTRADO con `DiscountAmount>0` anteriores al periodo no consumidos por ninguna corrida activa, con advertencia y exclusión posible (RF-003 local) |
| — **amonestaciones** | **DELTA (P-03)**, mismo mecanismo: `PersonnelFileDisciplinaryAction` APLICADA con `HasPayrollDeduction` y `DeductionAmount` (concepto de egreso snapshot al aplicar — REQ-003 D-08) entra por rango hoy; el arrastre la recoge si su planilla ya cerró |
| «seleccionar las opciones que se **aplicarán o ignorarán** en el cálculo de planilla» | **REQ-012 ratificado**: `IsIncluded` por línea + override con nota (RF-010, plan §1.4/§3.6 — solo estado `GENERADA`); **excluir una línea de pool revierte su aplicación** (vuelve a pendiente = queda para otra planilla); recálculo selectivo (RF-012) y regeneración (RF-011) re-evalúan los insumos |
| «deberá **seleccionarse de los listados** y elegir una opción según se requiera» | **REQ-012 ratificado**: los listados = pre-flight con detalle paginado (RF-007), detalle de la corrida por empleado×línea con `SourceModule`/`SourceReferencePublicId` (RF-010/RF-019), bandeja de corridas (RF-019); cada línea expone su decisión (incluida/excluida/override) trazada |

### Cobertura por tipo de transacción (el mapa operativo de la opción 1)

| Tipo (levantamiento) | Módulo fuente (estado) | ¿Destino de planilla declarado? | «No se aplicó → otra planilla»: mecanismo | Estado |
|---|---|---|---|---|
| Horas extras | REQ-007 `PersonnelFileOvertimeRecord` (🟢 certificado) | Sí (`payrollTypeCode` + periodo re-imputable) | Bandeja atrasadas (RF-010) → **re-imputar** (RF-008) → corrida del nuevo periodo la consume (`MOTOR`) | ✅ construido + REQ-012 PR-5 |
| Otros ingresos — eventuales | REQ-006 `PersonnelFileOneTimeIncome` (🟢) | Sí (ídem) | Bandeja «no aplicados» (RF-010) → **re-imputar** (RF-005) → corrida | ✅ construido + PR-5 |
| Otros ingresos — cuotas cíclicas | REQ-005 `PersonnelFileRecurringIncome` (🟢) | Plan de cuotas (`TheoreticalDueDate`) | **Arrastre ya ratificado**: D-09 consume «cuotas vencidas»; pre-flight advierte (RF-007) | ✅ ratificado (PR-5) |
| Otros descuentos — eventuales | REQ-009 `PersonnelFileOneTimeDeduction` (🟢) | Sí | Espejo ingresos: re-imputar (RF-016) + bandeja → corrida | ✅ construido + PR-5 |
| Otros descuentos — cuotas cíclicas | REQ-008 `PersonnelFileRecurringDeduction` (🟢) | Plan de amortización | D-09 cuotas vencidas + **diferimiento LIFO del paso [7]** (la diferida queda pendiente para la siguiente) | ✅ ratificado (PR-5) |
| Tiempos no trabajados | REQ-011 `PersonnelFileNotWorkedTime` (🟢) | **No** (solo fechas) | **DELTA: arrastre automático con advertencia** (RF-003 local, P-03) | ✅ P-03 ratificada → PR-5 |
| Amonestaciones | REQ-003 `PersonnelFileDisciplinaryAction` (🟢) | **No** (documental + monto) | **DELTA: mismo arrastre** (RF-003 local, P-03) | ✅ P-03 ratificada → PR-5 |

---

## 1. Resumen del producto o requerimiento

Dar al analista de planilla el control operativo sobre **qué transacciones entran en cada planilla**: (1) que una transacción que **no se aplicó** en la planilla de su periodo —porque se registró tarde, porque se excluyó a propósito o porque la planilla ya había cerrado— pueda **aplicarse en otra planilla de otro periodo** sin perderse ni duplicarse (horas extras, otros ingresos, otros descuentos, tiempos no trabajados y amonestaciones); y (2) que al calcular una planilla se pueda **seleccionar, desde los listados, qué se aplica y qué se ignora**, transacción por transacción.

**Problema que resuelve:** en la operación real las transacciones no siempre llegan a tiempo — una hora extra se reporta después del corte, una amonestación se aplica cuando la quincena ya se pagó, un permiso sin goce se registra tarde. Sin estos mecanismos, el dinero se paga de más (descuentos perdidos) o de menos (ingresos perdidos), o se fuerza la reapertura de planillas cerradas. **Nota estructural:** casi todo esto ya fue levantado, ratificado y planificado (REQ-005…012); este documento confirma la cobertura, precisa cuatro puntos (§17) y detecta **un** hueco real y quirúrgico: el arrastre de los dos insumos sin destino declarado (TNT y amonestaciones).

## 2. Objetivos del negocio

1. **Cero transacciones perdidas**: todo lo registrado y autorizado que afecta el pago termina aplicado en alguna planilla — en la de su periodo o, si no llegó a tiempo, en una posterior, con rastro de por qué.
2. **Cero dobles pagos/dobles descuentos**: una transacción se consume en UNA corrida; ignorarla o revertirla la devuelve a pendiente de forma visible y auditada.
3. **Control del analista sobre el cálculo**: decidir qué entra y qué se pospone sin hackear los datos fuente (la exclusión es una decisión de la corrida, no una anulación del registro).
4. **Cierre defendible del periodo**: la política ratificada «correcciones posteriores al cierre = registro en el periodo siguiente» (REQ-012 RN-12) se vuelve operativa — este levantamiento ES esa política con mecanismo.
5. **Transparencia con el empleado**: la boleta del periodo donde una transacción rezagada aterriza la muestra como línea trazada a su registro fuente (fechas originales visibles).

## 3. Alcance funcional

Dos bloques, ambos dentro de REQ-012 F1 (Olas B y C), con un delta condicionado a P-03:

**Bloque A — Transacciones que no se aplicaron en planilla**
- Registro tardío en los módulos fuente (construido; sin gates de fecha, salvo la ventana HE del periodo destino de REQ-012).
- Re-imputación del destino («enviar a otro periodo») y bandejas de pendientes/atrasados de los 5 pools (construido).
- Consumo por la corrida de lo pendiente/vencido de los pools con origen `MOTOR` (REQ-012 D-09, PR-5).
- **Arrastre automático de TNT y amonestaciones rezagados** (delta — P-03): la corrida incluye los registros anteriores al periodo no consumidos por ninguna corrida activa, con advertencia; el pre-flight los lista.
- Incorporación a una planilla abierta: regenerar / recálculo selectivo toman los insumos nuevos (REQ-012 RF-011/RF-012, PR-6).

**Bloque B — Transacciones que aplican en planilla**
- Vista previa de lo que entrará (pre-flight — REQ-012 RF-007, PR-5).
- Selección de población al generar: todos o `employeeIds[]` (REQ-012 RF-008, PR-5).
- Aplicar/ignorar por transacción sobre la planilla `GENERADA`: `IsIncluded` por línea + override con nota; **excluir una línea de pool revierte su aplicación** (queda pendiente = disponible para otra planilla) (REQ-012 RF-010 + plan §3.6, PR-6).
- Recálculo selectivo y regeneración para re-evaluar insumos (REQ-012 RF-011/RF-012, PR-6).

## 4. Fuera de alcance

- **Reabrir planillas `CERRADAS`** para meter transacciones tardías: la política ratificada es la contraria (RN-12 — correcciones → periodo siguiente); este levantamiento la instrumenta.
- **Apagar categorías completas por configuración de la nómina** (el difunto campo «aplicación»/«parámetros especiales» — P-02/P-03 de REQ-012): no se revive sin ejemplos reales del negocio (ver P-02 local); F1 selecciona por transacción.
- **Agregar líneas manuales ad-hoc a una corrida** (montos sin registro fuente): toda línea nace de un registro trazable o de la ley; un monto nuevo se registra en su módulo (eventual) y se recalcula.
- **Incapacidades y vacaciones**: no están en la lista del levantamiento; las incapacidades ya llevan FK propio a periodo (REQ-001) y las vacaciones no generan línea de planilla en F1 (REQ-012 P-11). El arrastre no las toca.
- **Automatismos de notificación** (avisar «tienes rezagos») — F2 de la cadena (sin proveedor real de correo).
- Lo ya excluido por REQ-012: programación a fecha/hora (P-14), retroactividad compleja, multi-nivel, marcación.

## 5. Actores o usuarios involucrados

| Actor | Rol en este requerimiento |
|---|---|
| **Analista de planilla** (`ManagePayrollRuns` + `Manage*` de los módulos fuente) | Registra tarde, re-imputa destinos, corre pre-flight, genera, excluye/incluye líneas, recalcula |
| **Gestor de RRHH de cada módulo fuente** (`ManageOvertimeRecords`, `ManageNotWorkedTimes`, `ManageDisciplinaryActions`, etc.) | Registra/re-imputa las transacciones en su módulo (la materia prima de este REQ) |
| **Autorizador de planilla** (`AuthorizePayrollRuns`, sin Admin) | Autoriza la corrida con las decisiones de inclusión/exclusión ya tomadas; devuelve con motivo si algo debe reabrirse |
| **Gerencia RRHH / consulta** (`ViewPayrollRuns` + `View*`) | Ve bandejas de pendientes/atrasados, pre-flight y el detalle con advertencias de arrastre |
| **Empleado** | Recibe en su boleta la línea rezagada trazada a su registro (fechas originales); sin acciones propias en F1 |

## 6. Requerimientos funcionales

> Numeración local RF-001…RF-007; cada uno declara su **cobertura** (módulo construido o REQ-012 con su PR). Solo RF-003 es desarrollo nuevo (condicionado a P-03).

### RF-001 - Registro tardío de transacciones («agregar las que no se agregaron»)

**Descripción:** Los módulos fuente permiten registrar transacciones cuyo hecho ocurrió en el pasado (jornada HE de la semana anterior, permiso sin goce del mes pasado, amonestación por falta antigua), aunque la planilla de ese rango ya exista o haya cerrado.

**Reglas de negocio:**
- Ningún módulo bloquea por fecha pasada (verificado; REQ-007 P-02 lo permite explícito, REQ-003 G-09 contempla el registro retroactivo pasando el mismo flujo).
- Única restricción nueva (REQ-012 RF-006): si la nómina exige ventana de ingreso de HE, la jornada debe imputarse a un **periodo destino cuya ventana esté abierta** — la ruta para una HE tardía es imputarla al siguiente periodo abierto (que es exactamente la opción 1).
- Los flujos de autorización propios de cada módulo no se alteran (una transacción tardía pasa por la misma decisión que una puntual).

**Criterios de aceptación:** registrar un TNT con fechas del mes anterior → 200/201 y descuento calculado; registrar jornada HE imputada al periodo siguiente con ventana abierta → 201; imputada a periodo con ventana cerrada → 422 `OVERTIME_ENTRY_WINDOW_CLOSED`.

**Prioridad:** Alta.
**Dependencias:** módulos fuente (construidos).
**Cobertura:** REQ-003/005/006/007/008/009/011 (🟢 certificados) + REQ-012 RF-006 (**PR-2**, gate de ventana).

### RF-002 - Reorientar pendientes de los pools a otra planilla/otro periodo

**Descripción:** Toda transacción de pool autorizada y no aplicada puede **cambiar su destino** (tipo de planilla y/o periodo) para que la corrida de ese otro periodo la consuma; las bandejas de pendientes muestran las **atrasadas** (periodo destino vencido sin aplicarse) para detectarlas.

**Reglas de negocio:**
- Re-imputación solo mientras está pendiente (`AUTORIZADO/A`), con If-Match y auditoría; no altera montos/horas/factor — solo destino (reglas ya certificadas: REQ-006 RF-005 · REQ-007 RF-008 · REQ-009 RF-016).
- Cuotas cíclicas: sin re-imputación por cuota — su «destino» es el plan; las **vencidas** no aplicadas las consume la corrida directamente (REQ-012 D-09) y el pre-flight las advierte (RF-007).
- La corrida consume los candidatos pendientes del periodo con origen `MOTOR` y reversión simétrica (REQ-012 D-09/§3.5).
- Los pools con destino declarado **no se auto-mueven**: el sistema señala (bandeja «atrasado» + pre-flight) y el usuario decide (re-imputar o dejar) — ver P-03.

**Criterios de aceptación:** eventual autorizado imputado a la quincena pasada aparece «atrasado» en su bandeja; re-imputado a la quincena actual, la corrida lo incluye y su aplicación queda origen `MOTOR`; el pre-flight de la quincena actual advierte los vencidos no re-imputados.

**Prioridad:** Alta.
**Dependencias:** RF-001.
**Cobertura:** REQ-005/006/007/008/009 (🟢 — re-imputación y bandejas construidas) + REQ-012 D-09 · plan §3.4/§3.5 · **PR-5**.

### RF-003 - Arrastre de insumos sin destino: tiempos no trabajados y amonestaciones rezagados — **DELTA (P-03 ✅ RATIFICADA 2026-07-15)**

**Descripción:** La corrida de un periodo incluye, además de los TNT/amonestaciones **del rango** del periodo (ya ratificado), los registros **anteriores al periodo** que ninguna corrida activa consumió — para que un registro tardío de un periodo ya cerrado «sea aplicado en otra planilla de otro periodo» sin intervención manual ni cambios en los módulos fuente.

**Reglas de negocio:**
- Elegibles al arrastre: TNT `REGISTRADO` con `DiscountAmount > 0` (mismo filtro del insumo — fuera anulados y ausencias con goce) y amonestaciones `APLICADA` con `HasPayrollDeduction` y `DeductionAmount > 0`, de empleados de la población de la corrida, con fechas **anteriores al inicio del periodo**.
- **Consumo derivado, sin tocar los módulos fuente**: un registro está consumido si existe una línea con su `SourceModule` (`NOT_WORKED_TIME`/`DISCIPLINARY`) y `SourceReferencePublicId` en una corrida **no anulada** con `IsIncluded=true`. Excluir la línea o anular la corrida lo libera (vuelve a ser arrastrable) — simetría exacta con la reversión de pools.
- Cada línea arrastrada lleva advertencia con código estable (p. ej. `PAYROLL_WARNING_CARRYOVER_INPUT` — nombre final lo fija el plan) y conserva la traza a su registro (fechas originales visibles en el detalle/boleta).
- El **pre-flight** lista los rezagos que se arrastrarían (extensión natural de la advertencia «pools con elementos vencidos» de RF-007 de REQ-012).
- La línea arrastrada es excluible en revisión como cualquier otra (= posponer otra vez, RF-007 local).
- El monto arrastrado es el **snapshot del registro** (el motor no recalcula TNT ni amonestaciones — mismo principio ratificado del rango: el motor «solo lo toma», REQ-012 P-10 por analogía).
- El anulado en el módulo fuente nunca entra (ni por rango ni por arrastre).

**Criterios de aceptación:** TNT registrado hoy con fechas del periodo N−1 cerrado → la corrida del periodo N lo incluye con advertencia; excluir esa línea → el registro vuelve a arrastrarse en N+1; anular la corrida N → ídem; el mismo registro nunca aparece en dos corridas activas; suite de cuadre: Σ arrastrados + Σ del rango ≡ insumo del filtro equivalente.

**Prioridad:** Alta (es el único hueco que el levantamiento destapa).
**Dependencias:** ~~ratificación P-03~~ ✅ **RATIFICADA 2026-07-15**; modelo M4 (líneas con `SourceModule`/`SourceReferencePublicId`/`IsIncluded` — ya en el plan §1.4).
**Cobertura:** **NUEVO — ratificado y PROPAGADO al plan de REQ-012 (2026-07-15)**: §0.11 (regla) + §1.4 (índice de consumo derivado, viaja en M4 → **PR-4**) + §3.4 (candidatos + pre-flight) + §3.5 (test de liberación) + §5 (`PAYROLL_WARNING_CARRYOVER_INPUT`) + §8 (adopción); se construye dentro de **PR-5** (el índice en PR-4); **cero entidades/migraciones/seeds**.

### RF-004 - Incorporar transacciones a una planilla abierta

**Descripción:** Mientras la corrida está `GENERADA`, las transacciones recién registradas/autorizadas del periodo se incorporan re-ejecutando el cálculo: **recálculo selectivo** de los empleados afectados (conserva overrides) o **regeneración** completa (descarta ajustes, por diseño).

**Reglas de negocio:** las de REQ-012 RF-011/RF-012 (solo `GENERADA`; reversión/re-aplicación simétrica de pools; auditoría del subconjunto). Con el arrastre (RF-003), el recálculo también recoge rezagos liberados después de generar.

**Criterios de aceptación:** criterio literal ya ratificado en REQ-012 RF-011: «insumo nuevo registrado → regenerar lo incorpora»; recálculo selectivo de un empleado refleja su TNT nuevo y deja al resto byte-idéntico.

**Prioridad:** Alta.
**Dependencias:** RF-001.
**Cobertura:** REQ-012 RF-011/RF-012 · plan §3.6 · **PR-6**.

### RF-005 - Vista previa de lo que aplicará (pre-flight)

**Descripción:** Antes de generar, consultar qué entrará en el cálculo y qué está en riesgo: pools con elementos vencidos de periodos anteriores, rezagos de TNT/amonestaciones que se arrastrarían (RF-003), incapacidades `EN_REVISION` del rango, huecos duros (sin salario base, sin tramos Renta…).

**Reglas de negocio:** advertir, no bloquear (filosofía REQ-010, ya ratificada en REQ-012 RF-007); advertencias con códigos estables; detalle paginado.

**Criterios de aceptación:** los de REQ-012 RF-007 + los rezagos de RF-003 listados con conteo y detalle.

**Prioridad:** Alta.
**Dependencias:** RF-002/RF-003.
**Cobertura:** REQ-012 RF-007 · plan §3.4 (`POST …/payroll-runs/preflight`) · **PR-5** (la extensión de rezagos viaja con RF-003).

### RF-006 - Selección de población al generar

**Descripción:** Generar la planilla para todos los empleados de la población o un subconjunto (`employeeIds[]`) — la primera palanca de «aplicar o ignorar» (a nivel empleado).

**Reglas de negocio / criterios:** los de REQ-012 RF-008 (población D-18, una corrida activa por nómina+periodo, anti-carrera, consumo `MOTOR` en la misma tx).

**Prioridad:** Alta.
**Dependencias:** RF-005.
**Cobertura:** REQ-012 RF-008 · plan §3.4 · **PR-5**.

### RF-007 - Aplicar/ignorar por transacción en la planilla («elegir una opción según se requiera»)

**Descripción:** Sobre la planilla `GENERADA`, el revisor recorre los listados (por empleado, por concepto, con módulo fuente y referencia navegable) y decide por línea: **mantener** (aplicar), **excluir** (`IsIncluded=false` — ignorar) o **ajustar** (override con nota). El neto y los totales se recalculan.

**Reglas de negocio:**
- Solo en `GENERADA` (desde `AUTORIZADA` → 422; REQ-013 RN-03).
- **Excluir una línea de pool revierte su aplicación** (plan §3.6): la transacción vuelve a pendiente en su módulo → disponible para re-imputación o para la siguiente corrida. **Ignorar = posponer** — la unificación de las dos opciones del levantamiento.
- Excluir una línea arrastrada (RF-003) la libera para la siguiente corrida (consumo derivado).
- Las líneas de ley (ISSS/AFP/Renta) y salario no se excluyen como «transacciones»: su control es el override trazado (mismo corte de REQ-012 RF-010: «línea de sistema no editable en su fórmula, solo override trazado»).
- Toda decisión queda trazada (quién/cuándo/nota — `AdjustedByUserId`, auditoría `PAYROLL_RUN_ADJUSTED`).

**Criterios de aceptación:** excluir línea de pool → aplicación revertida (verificable en el historial del registro fuente) + totales re-derivados; incluirla de nuevo → re-aplicada; excluida al autorizar → no pagada y pendiente para la siguiente corrida; todo con If-Match y auditoría.

**Prioridad:** Alta.
**Dependencias:** RF-006.
**Cobertura:** REQ-012 RF-010 · plan §1.4 (`IsIncluded`) + §3.6 · **PR-6**.

## 7. Requerimientos no funcionales

| Categoría | Requisito |
|---|---|
| Seguridad | Los mismos gates de REQ-012 (fail-closed; `ManagePayrollRuns` para generar/ajustar; `AuthorizePayrollRuns` sin Admin para decidir) + los permisos propios de cada módulo fuente para registrar/re-imputar; multi-tenant por `TenantId`; If-Match en toda mutación |
| Integridad | **Una transacción, una corrida activa**: el consumo (aplicación `MOTOR` en pools; derivado por línea en TNT/amonestaciones) impide el doble pago; reversión simétrica probada (generar→excluir→regenerar→anular deja los módulos fuente byte-idénticos — extiende el test dorado de REQ-012 §3.5) |
| Rendimiento | El arrastre es una consulta acotada por población de la corrida + estado + no-consumido; requiere índice sobre las líneas por `(tenant, source_module, source_reference_public_id)` — se define en el plan (nota técnica №1); pre-flight paginado |
| Auditoría | Decisiones de inclusión/exclusión y re-imputaciones trazadas (actor/fecha/nota) — moldes existentes; advertencia de arrastre persistida en la línea (`WarningCodesJson`) |
| Exactitud | El monto de la línea rezagada = snapshot del registro fuente (cero recálculo); tests de cuadre corrida ≡ insumo extendidos al arrastre |
| Usabilidad | Advertencias con códigos estables distinguibles de errores; la línea arrastrada muestra las fechas originales del hecho (transparencia en revisión y boleta); errores 422/403 bilingües EN/ES |
| Mantenibilidad | **Cero entidades, cero migraciones, cero seeds nuevos**; cero toques a los módulos certificados (REQ-003/011); el delta vive en el data provider de REQ-012 (PR-5) |
| Compatibilidad | Convenciones del repo (`api/v1`, enums string, `XxxId`→`xxxPublicId`, If-Match, openapi a mano); contratos FE de los módulos fuente intactos |

## 8. Historias de usuario

### HU-001 - Pagar una hora extra que llegó tarde
Como **analista de planilla**, quiero **re-imputar al periodo actual una jornada de horas extras autorizada que quedó atrasada en el periodo anterior**, para **que se pague en la siguiente planilla sin registrar nada de nuevo**.
**Criterios:** Dada una HE `AUTORIZADA` imputada a un periodo vencido, cuando la re-imputo al periodo abierto y genero la planilla, entonces la corrida la incluye como línea `OVERTIME` con su referencia y su aplicación queda con origen `MOTOR`.

### HU-002 - No perder un permiso sin goce registrado tarde
Como **analista de planilla**, quiero **que un tiempo no trabajado con fechas de un periodo ya cerrado entre automáticamente en la siguiente planilla con una advertencia**, para **descontarlo aunque se haya registrado tarde, sin re-digitarlo ni reabrir nada**.
**Criterios:** Dado un TNT `REGISTRADO` con `DiscountAmount>0` y fechas del periodo cerrado N−1, cuando genero la planilla del periodo N, entonces aparece como línea de descuento con advertencia de arrastre y trazada al registro original; el pre-flight ya lo listaba.

### HU-003 - Ignorar una transacción en esta planilla sin perderla
Como **analista de planilla**, quiero **excluir del cálculo una transacción específica desde el listado de la planilla generada**, para **posponerla a otro periodo sin anular el registro**.
**Criterios:** Dada una línea de pool en una corrida `GENERADA`, cuando la excluyo, entonces su aplicación se revierte (el registro vuelve a pendiente), los totales se recalculan y en la siguiente corrida vuelve a aparecer.

### HU-004 - Ver qué entrará antes de calcular
Como **analista de planilla**, quiero **ver antes de generar la lista de transacciones que aplicarán y las advertencias (vencidos, rezagos, huecos)**, para **decidir con información qué re-imputo, qué dejo entrar y qué excluiré**.
**Criterios:** Dado un periodo con pendientes vencidos de pools y un TNT rezagado, cuando corro el pre-flight, entonces ambos aparecen con su código de advertencia, conteo y detalle paginado.

### HU-005 - Descontar una amonestación aplicada después del pago
Como **gestor de relaciones laborales / analista de planilla**, quiero **que una amonestación con descuento aplicada cuando su quincena ya estaba pagada se descuente en la siguiente planilla**, para **ejecutar la sanción sin procesos manuales**.
**Criterios:** Dada una amonestación `APLICADA` con `HasPayrollDeduction` y monto, aplicada tras el cierre de su periodo, cuando genero la siguiente planilla, entonces aparece como línea `DISCIPLINARY` con advertencia de arrastre; si la excluyo, reaparece en la subsiguiente.

## 9. Reglas de negocio (consolidadas)

- **RN-01** Toda transacción monetaria autorizada/registrada debe terminar aplicada en exactamente **una** corrida activa — o seguir visible como pendiente/rezago. Nada se pierde en silencio, nada se paga dos veces.
- **RN-02** «Ignorar» una transacción en una planilla **no la anula**: la devuelve a pendiente (pools: reversión de la aplicación `MOTOR`; TNT/amonestaciones: liberación del consumo derivado) y queda disponible para otra planilla.
- **RN-03** Lo que tiene **destino declarado** (eventuales, HE) se mueve por decisión del usuario (re-imputación construida); lo que **no tiene destino** (TNT, amonestaciones) se **arrastra automáticamente** con advertencia (P-03); las **cuotas cíclicas vencidas** se consumen directo (D-09 ratificada).
- **RN-04** El monto de una transacción rezagada es el **snapshot de su registro** — el motor no lo recalcula (coherente con REQ-012 P-10 y con el «monto del registro fuente» de todos los pools).
- **RN-05** La selección aplicar/ignorar se ejerce **sobre la planilla `GENERADA`** (línea a línea, trazada); desde `AUTORIZADA` no hay cambios (REQ-013 RN-03).
- **RN-06** El registro tardío pasa por el **mismo flujo** de su módulo (autorización incluida); no existe una vía «exprés» que salte controles por ser tardío.
- **RN-07** Las líneas de ley y salario no son «transacciones seleccionables»: su ajuste es el override trazado, nunca la exclusión.
- **RN-08** Anular una corrida o excluir una línea libera sus transacciones (pools por reversión, insumos por consumo derivado) — simetría total con REQ-012 §3.5.
- **RN-09** El pre-flight advierte, nunca bloquea (filosofía REQ-010): vencidos, rezagos y huecos son información para decidir, no candados.
- **RN-10** Correcciones tras el cierre = registro/aplicación en el periodo siguiente (REQ-012 RN-12 ratificada) — este REQ es su instrumentación.

## 10. Flujos principales

### Flujo 1 — Hora extra rezagada (pool con destino declarado)
1. La jornada HE del periodo N−1 se registra y autoriza cuando N−1 ya cerró. 2. La bandeja de no aplicadas la marca **atrasada**. 3. El analista la **re-imputa** al periodo N (o el pre-flight de N se la recuerda). 4. Genera la planilla de N: la corrida la consume (línea `OVERTIME`, aplicación origen `MOTOR`). 5. Revisión → autorización → cierre.

### Flujo 2 — TNT/amonestación rezagados (insumo sin destino — delta P-03)
1. El TNT con fechas de N−1 (cerrado) se registra tarde; su descuento se calcula solo (REQ-011). 2. El pre-flight del periodo N lo lista como rezago. 3. Generar N lo **arrastra**: línea de descuento con advertencia y traza al registro. 4. El revisor decide: mantener (se paga/descuenta en N) o excluir (pasa a N+1). 5. Cierre de N marca el consumo (derivado de la línea incluida).

### Flujo 3 — Ignorar en el cálculo (selección)
1. Planilla de N `GENERADA`. 2. El revisor recorre el listado por empleado/concepto. 3. Excluye una transacción («ignorar») → su aplicación se revierte, totales re-derivados. 4. Autoriza: lo excluido no se paga. 5. La transacción excluida entra en la corrida de N+1 (o se re-imputa antes).

### Flujo 4 — Transacción nueva con la planilla abierta
1. Planilla de N `GENERADA`. 2. Se registra/autoriza una transacción del periodo. 3. **Recálculo selectivo** del empleado (conserva overrides) o **regeneración**. 4. La transacción entra; el resto queda byte-idéntico (recálculo) o se reconstruye (regeneración).

## 11. Flujos alternativos y excepciones

| Escenario | Comportamiento |
|---|---|
| Registrar HE imputada a periodo con ventana de ingreso cerrada | 422 `OVERTIME_ENTRY_WINDOW_CLOSED` (REQ-012 RF-006) — imputarla al siguiente periodo abierto |
| Re-imputar una transacción ya aplicada | 422 del módulo fuente (solo pendientes se re-imputan — reglas certificadas) |
| Excluir/incluir línea sobre corrida `AUTORIZADA`/`CERRADA` | 422 `PAYROLL_RUN_STATE_RULE_VIOLATION` (solo `GENERADA`) |
| Transacción fuente anulada entre generar y revisar | El recálculo/regeneración la saca; la exclusión manual no es necesaria (el registro ANULADO nunca es candidato) |
| El mismo TNT candidato en dos corridas concurrentes | El consumo derivado + lock de generación de REQ-012 (§0.18) lo impiden; conflicto → 422 `PAYROLL_RUN_POOL_CONFLICT` (molde) |
| Corrida anulada con líneas arrastradas | Los rezagos quedan liberados y vuelven a arrastrarse en la siguiente (consumo derivado desaparece con la anulación) |
| TNT con goce (0%) o amonestación sin descuento | Nunca genera línea (filtros del insumo: `DiscountAmount>0` / `HasPayrollDeduction`) — son documentales para planilla |
| Rezago de un empleado fuera de la población de la corrida (otra nómina/plaza) | No se arrastra ahí; aparecerá en la corrida de SU nómina (población D-18) |
| Empleado liquidado con rezagos | Fuera de la corrida (REQ-012 P-11); sus pendientes los resolvió/resolverá el finiquito (acciones de liquidación ya certificadas) |
| Primera corrida tras la adopción con histórico de rezagos | El pre-flight los lista; el revisor excluye lo ya pagado por la nómina externa (advertir-nunca-bloquear); sin producción hoy, impacto nulo |

## 12. Datos requeridos

**Este requerimiento no modela datos nuevos — cero entidades, cero migraciones, cero seeds.** Todo lo que necesita ya existe (verificado en código) o ya está en el modelo M4 del plan de REQ-012. Se listan los campos que cada mecanismo **usa**:

### Re-imputación y consumo de pools (existente — verificado)

| Pieza | Campos (código) |
|---|---|
| Destino declarado del registro | `PayrollTypeCode` (siempre) + `PayrollPeriodPublicId?`/`PayrollPeriodLabel` (+FK `PayrollPeriodId?` al aplicar con override) — en los 5 pools |
| Aplicación/cuota (hija) | `AppliedDate`, `OriginCode` (`MANUAL`/`MOTOR`[/`LIQUIDACION`]), `StatusCode` (`APLICADA`/`ANULADA`), snapshots de periodo; cuotas además `Amount` (+`CapitalAmount`/`InterestAmount` en descuentos) |
| Marca de pendiente | Padre en `AUTORIZADO/A` sin aplicación activa (índice único filtrado) / cuota vencida sin fila `APLICADA` |

### Arrastre de insumos (delta P-03 — usa lo ya planificado en M4)

| Pieza | Campos |
|---|---|
| Candidato TNT | `PersonnelFileNotWorkedTime`: `StatusCode=REGISTRADO`, `DiscountAmount>0`, `StartDate/EndDate` (ancla), `AssignedPositionPublicId`, snapshots de concepto |
| Candidato amonestación | `PersonnelFileDisciplinaryAction`: estado `APLICADA`, `HasPayrollDeduction=true`, `DeductionAmount`, `DeductionConceptTypeCode/NameSnapshot` (congelados al aplicar), fecha ancla (falta/aplicación — la fija el plan) |
| Marca de consumo (derivada) | `PayrollRunLine`: `SourceModule` (`NOT_WORKED_TIME`/`DISCIPLINARY`) + `SourceReferencePublicId` + `IsIncluded=true`, de corrida con estado ≠ `ANULADA` — **ya en el plan §1.4**; requiere índice (nota técnica №1) |
| Advertencia | `WarningCodesJson` de la línea + advertencia agregada del pre-flight (código estable nuevo, p. ej. `PAYROLL_WARNING_CARRYOVER_INPUT` — resx EN/ES, sin catálogo) |

### Selección aplicar/ignorar (ya en el plan §1.4)

`PayrollRunLine.IsIncluded` (bool, default true) · `OverrideAmount?`/`OverrideNote?`/`AdjustedByUserId?` · `SourceModule`/`SourceReferencePublicId` (la referencia navegable del «listado»).

## 13. Integraciones necesarias

| Integración | Estado |
|---|---|
| Módulos fuente (REQ-003/005/006/007/008/009/011) — registro, autorización, re-imputación, bandejas | **Existen** (🟢 certificados); este REQ no los modifica |
| Motor/corrida de REQ-012 (pre-flight, generación, pools `MOTOR`, revisión, líneas) | En plan (PR-4…PR-6) — **vehículo de entrega** de este REQ |
| Exports/insumos por periodo de cada módulo | Existen — siguen como verificación de cuadre (REQ-012 criterio 2) |
| Correo/notificaciones de rezagos | Fuera de alcance (F2 de la cadena) |

## 14. Roles y permisos

**Cero permisos nuevos.** Composición de los existentes:

| Rol/Permiso | Permisos | Restricciones |
|---|---|---|
| `PersonnelFiles.ManagePayrollRuns` | Pre-flight, generar (población), excluir/incluir/override líneas, recálculo/regeneración | Solo en `GENERADA`; no autoriza (anti-self de REQ-012) |
| `Manage*` de cada módulo fuente (`ManageOvertimeRecords`, `ManageNotWorkedTimes`, `ManageDisciplinaryActions`, ingresos/descuentos) | Registro tardío, re-imputación de destino, aplicación/reversión manual de borde | Flujos y anti-self propios de cada módulo (certificados) |
| `PersonnelFiles.ViewPayrollRuns` + `View*` fuente | Bandejas de pendientes/atrasados, pre-flight (lectura), detalle con advertencias | Solo lectura |
| `PersonnelFiles.AuthorizePayrollRuns` | Autoriza/devuelve la corrida con las decisiones tomadas | Sin Admin implícito; anti-self doble |

## 15. Criterios de aceptación generales

1. Una transacción de pool no aplicada en su periodo llega a otra planilla por re-imputación (eventuales/HE) o arrastre de cuota vencida (cíclicos), y su aplicación final queda origen `MOTOR` con referencia.
2. Un TNT y una amonestación con descuento registrados tarde (periodo cerrado) entran en la siguiente corrida con advertencia visible, trazados a su registro (P-03 ratificada).
3. Excluir una línea en revisión revierte/libera su transacción y ésta reaparece en la siguiente corrida; incluirla de nuevo la re-aplica; nada se duplica (suite de reversión/cuadre extendida).
4. El pre-flight lista vencidos de pools y rezagos de insumos con códigos estables, conteos y detalle.
5. Ninguna mutación de selección procede fuera de `GENERADA`; todas trazan actor/fecha/nota.
6. Los módulos fuente conservan sus contratos intactos (suites REQ-003/005…011 verdes sin editar — mismo gate que REQ-012 PR-2).
7. openapi.yaml sin drift; advertencias/errores bilingües; guía FE (REQ-012 PR-8) documenta rezagos y selección.

## 16. Riesgos, supuestos y dependencias

### Riesgos

> **Nota (2026-07-15):** con P-01…P-04 ratificadas tal cual, los tres primeros riesgos quedan **resueltos por decisión** (se conservan como registro); vigente solo el recordatorio de adopción (pre-flight de la primera corrida).

- **Expectativa de pantalla propia**: el cliente podría imaginar una pantalla dedicada «transacciones no aplicadas» de los 5 tipos; la cobertura real son las bandejas por módulo + el pre-flight unificado por periodo. Si el negocio la exige como vista única, es agregación de lecturas existentes (P-04) — costo bajo, pero conviene no construirla por adelantado.
- **Arrastre percibido como «la planilla trae cosas viejas»**: mitigado con la advertencia por línea + pre-flight + exclusión a un clic; la alternativa (re-imputación manual en TNT/amonestaciones) exige desarrollo en dos módulos certificados y una operación manual permanente (P-03 lo decide).
- **Primera corrida tras adopción**: podría arrastrar históricos que la nómina externa ya pagó — mitigado por pre-flight + exclusión; sin producción hoy (2026-07-15), impacto nulo.
- **Riesgo técnico: bajo.** 6 de 7 RF son moldes/planes existentes; el delta es una consulta + advertencia dentro de un PR ya planificado.

### Supuestos
- «Otros ingresos» y «otros descuentos» abarcan **eventuales y cíclicos** (ambos cubiertos; el levantamiento no distingue).
- «Amonestaciones» = solo las `APLICADA` **con descuento** (`HasPayrollDeduction`); las documentales no generan línea (coherente con REQ-003 D-08). El descuento se consume **completo en una corrida** (sin cuotas; un monto grande que requiera fraccionarse se registraría como descuento cíclico — fuera de este REQ).
- «Seleccionarse de los listados» = los listados de la corrida (pre-flight + detalle por línea), no un maestro de configuración (P-01/P-02 lo confirman).
- Incapacidades y vacaciones fuera del alcance (no están en la lista del cliente; las incapacidades ya llevan FK de periodo propio).
- Sin producción a la fecha: sin migración de rezagos históricos; la estabilidad del contrato FE sí aplica.

### Dependencias
- **REQ-012 PR-4/PR-5** (motor + data provider + pools `MOTOR`): vehículo de RF-002/003/005/006; **PR-6**: RF-004/007; **PR-2**: gate de ventana HE (RF-001); **PR-8**: guía FE.
- ~~Ratificación P-01…P-04~~ ✅ **RATIFICADAS 2026-07-15 tal cual** — cero condiciones de negocio pendientes. **Nada bloquea ningún PR.**
- ✅ **Delta propagado al plan técnico de REQ-012 (2026-07-15)**: §0.11 + §1.4 (índice → M4/PR-4) + §3.4 + §3.5 + §5 + §6 (PR-4/PR-5) + §8 — sin PRs nuevos.

## 17. Preguntas al negocio — **P-01…P-04 RESPONDIDAS (ratificación 2026-07-15)**

> Respuesta del negocio: **«aceptar todas las recomendaciones de las preguntas»** — las 4 quedan ratificadas TAL CUAL con la columna «Recomendación» como decisión final. Efectos ejecutados el mismo día: P-03 → delta propagado al plan de REQ-012 (§0.11/§1.4/§3.4/§3.5/§5/§6/§8); P-01/P-02/P-04 → cero cambios (coinciden con lo ya ratificado/construido).

| # | Ámbito | Pregunta | Recomendación → **DECISIÓN (aceptada tal cual, 2026-07-15)** |
|---|---|---|---|
| **P-01** | Momento de la selección | «Seleccionar las opciones que se aplicarán o ignorarán» — el modelo ratificado decide **sobre la planilla generada** (pre-flight informa antes; la exclusión por línea, con reversión, decide después; el efecto es idéntico a decidir antes). ¿Se confirma ese momento, o el negocio necesita una pantalla de marcado **previa** a la generación? | **Confirmar el modelo ratificado** (REQ-012 RF-007/RF-010): un solo lugar de decisión, auditado, y el «ignorar» devuelve la transacción a pendiente. Una pantalla previa duplicaría la revisión con más código y más estados; el pre-flight ya es la vista previa |
| **P-02** | Granularidad del «ignorar» | ¿El «ignorar» es siempre **por transacción individual** (ratificado), o el negocio también necesita apagar **categorías completas** en una corrida o por nómina (p. ej. «esta planilla nunca procesa horas extras»)? Nota: esto último podría ser el significado del campo «aplicación» que la ratificación de REQ-012 **eliminó** por no estar claro (P-02) | **F1 = por transacción** (ya ratificado). El apagado por categoría solo se considera si el negocio lo confirma con un caso real — y entonces como parámetro aditivo de la generación (F2 barato), **no** reviviendo el campo del maestro sin ejemplos |
| **P-03** | **El delta — arrastre de rezagos** | Un TNT o amonestación con fechas de un periodo cuya planilla **ya cerró** hoy no entraría en ninguna corrida (se leen «del rango»). ¿Se ratifica el **arrastre automático** — la corrida incluye también los anteriores al periodo no consumidos por ninguna corrida activa, con advertencia visible y exclusión posible (= posponer)? ¿O se prefiere **re-imputación manual** (habilitar destino de planilla en TNT/amonestaciones: más desarrollo, toca dos módulos certificados y añade una operación manual permanente)? ¿Algún límite de antigüedad para el arrastre? | **Arrastre automático con advertencia** (RF-003): cero cambios en módulos certificados (consumo derivado de las líneas del plan §1.4), simetría con «excluir = posponer», y el pre-flight lo hace visible. Sin límite de antigüedad en F1 (sin producción; el pre-flight lista todo y la exclusión decide) — límite configurable = F2 si molesta. Los **pools no se auto-mueven** (destino declarado se respeta; re-imputación ya construida; cuotas vencidas ya ratificadas en D-09). **Ratificar antes de REQ-012 PR-5** |
| **P-04** | Vista unificada | ¿Basta el **pre-flight por nómina+periodo** (unificado, con vencidos y rezagos de todos los tipos) + las **bandejas por módulo** ya construidas («no aplicados en planilla», con marca de atrasado), o el negocio exige una **bandeja única** corporativa de transacciones no aplicadas de los 5 tipos? | **Pre-flight + bandejas existentes** (cero desarrollo): el pre-flight ES la vista unificada en el momento en que importa (antes de generar). Si tras usarlo el negocio insiste, la bandeja única es una consulta de agregación aditiva (F2/PR-7) |

## 18. Recomendaciones del Analista de Negocio

1. **No abrir desarrollo propio.** Registrar como **REQ-014** con entrega vía REQ-012 (PR-5/PR-6/PR-7; guía FE en PR-8) y usar este documento como checklist de correspondencia — mismo protocolo que REQ-013.
2. **Ratificar P-03 antes de PR-5** (es la única pregunta con código asociado); P-01/P-02 antes de PR-6 y P-04 antes de PR-7. **La «Próxima acción» del programa no cambia: REQ-012 PR-1.**
3. **Al ratificar P-03, propagar el delta al plan técnico de REQ-012** en tres puntos quirúrgicos: §3.4 (candidatos del data provider: + rezagos TNT/amonestaciones por consumo derivado), §5 (warning `PAYROLL_WARNING_CARRYOVER_INPUT` o nombre equivalente) y RF-007/pre-flight (listar rezagos). Añadir al PR-5 el índice de líneas por `(tenant, source_module, source_reference_public_id)` y el test de liberación (excluir/anular → re-arrastrable).
4. **Comunicar la unificación «ignorar = posponer»** al negocio y al FE con una línea: *excluir una transacción de la planilla no la borra — la deja lista para la siguiente*. Es el concepto que hace que las dos opciones del levantamiento sean un solo mecanismo.
5. **No revivir «aplicación»/«parámetros especiales» del maestro** por esta vía (P-02 local): la selección por transacción cubre el pedido; una configuración por nómina solo con ejemplos reales.
6. **Extender la suite dorada de reversión** (REQ-012 §3.5) con el ciclo del rezago: generar→arrastrar→excluir→regenerar→anular deja TNT/amonestaciones byte-idénticos y re-arrastrables — es la garantía mecánica de RN-01/RN-02.
7. **En la adopción**, usar el pre-flight de la primera corrida como inventario de rezagos históricos y excluir lo ya pagado por la nómina externa (advertir-nunca-bloquear); documentarlo en el checklist de despliegue de REQ-012 (piloto en paralelo).

---

## Anexo A — Correspondencia, verificaciones y linaje

### A.1 Mapa de entrega (RF locales → programa)

| RF local | Cobertura | Plan técnico | PR |
|---|---|---|---|
| RF-001 Registro tardío | REQ-003/005…011 (🟢) + REQ-012 RF-006 | §3.2 (gate ventana HE) | PR-2 |
| RF-002 Reorientar pools | REQ-006 RF-005 · REQ-007 RF-008 · REQ-009 RF-016 (🟢) + REQ-012 D-09 | §3.4/§3.5 | PR-5 |
| RF-003 **Arrastre TNT/amonestaciones** | **NUEVO — P-03** | §3.4 (+§5 warning, +pre-flight) al ratificar | **PR-5** |
| RF-004 Planilla abierta | REQ-012 RF-011/RF-012 | §3.6 | PR-6 |
| RF-005 Pre-flight | REQ-012 RF-007 | §3.4 | PR-5 |
| RF-006 Población al generar | REQ-012 RF-008 | §3.4 | PR-5 |
| RF-007 Aplicar/ignorar por línea | REQ-012 RF-010 | §1.4/§3.6 | PR-6 |

### A.2 Verificaciones de código de este análisis (2026-07-15, rama `feature/planilla-descuentos`)

- **Los 5 pools, maquinaria completa de destino/aplicación** (padre + hija): `PersonnelFileOneTimeIncome.cs:738` (aplicación con `AppliedDate`/`PayrollTypeCode`/`PayrollPeriodId?` FK/`OriginCode`/`StatusCode`), `PersonnelFileOneTimeDeduction.cs:696` (ídem), `PersonnelFileOvertimeRecord.cs:791` (ídem), `PersonnelFileRecurringIncome.cs:732` (cuota con `Amount`/`TheoreticalDueDate`), `PersonnelFileRecurringDeduction.cs:1156` (cuota con `Kind` REGULAR/EXTRAORDINARIA + split capital/interés).
- **Origen `MOTOR` reservado y sin escribir**: constantes en los 5 dominios (`PersonnelFileRecurringIncome.cs:39`, `PersonnelFileOneTimeIncome.cs:31`, `PersonnelFileRecurringDeduction.cs:43`, `PersonnelFileOneTimeDeduction.cs:30`, `PersonnelFileOvertimeRecord.cs:39`); todos los handlers actuales aplican con `MANUAL` — la costura espera al motor (confirma REQ-012 §0.10).
- **Estado «pendiente» de un pool = padre autorizado sin aplicación activa** (p. ej. `HasActiveApplication`, `PersonnelFileOneTimeIncome.cs:236` + índice único filtrado); la reversión (`AnnulApplication`) lo devuelve a `AUTORIZADO` — es la base de «ignorar = posponer».
- **TNT sin destino ni consumo**: `PersonnelFileNotWorkedTime.cs` — `DiscountAmount` calculado (`:138`), solo `StartDate`/`EndDate` (`:115-117`), **sin** `PayrollPeriodId`/`PayrollTypeCode`/hija; estados `REGISTRADO/ANULADO` (`-9960/-9961`).
- **Amonestación monetaria pero documental**: `PersonnelFilePersonnelTransactions.cs:273` (`PersonnelFileDisciplinaryAction`), bloque deducción/suspensión `:356-373` (`HasPayrollDeduction`, `DeductionAmount`, `DeductionConceptTypeCode/NameSnapshot` congelados en `Apply()` `:490-524`); al aplicar escribe journal (`DisciplinaryActions.Handlers.cs:395-419`) — **sin** FK de periodo ni aplicación.
- **Consumidores FK de `PayrollPeriodDefinition`**: los 5 pools + incapacidades — **TNT y amonestaciones NO figuran** (confirma el hueco del arrastre).
- **Resolución de periodo al aplicar** (molde del FK opcional): `OvertimeRecordApplications.Handlers.cs:154-165` (override explícito → FK real; sin override → snapshot degradado `:52`).

### A.3 Linaje de la capacidad en el programa

| Hito | Qué estableció |
|---|---|
| REQ-005 (levantamiento 2026-07-05) | La visión pidió «transacciones que no se aplicaron en planilla… aplicar al periodo activo… **enviar a otro periodo**»; G-08 respondió con la aproximación F1: bandeja de pendientes/vencidas + lote con exclusión (= posponer) |
| REQ-006/007/008/009 | Extendieron el patrón a eventuales, HE y descuentos: **re-imputación** del destino + bandejas «no aplicados en planilla» + marca de atrasado — todo construido y certificado |
| REQ-011 / REQ-003 | Construyeron TNT y amonestaciones como **insumos por rango** (sin destino) — suficiente para nómina externa, el hueco que este REQ cierra para el motor |
| REQ-012 (ratificado 2026-07-14) | El «pool pleno»: la corrida consume pendientes/vencidos con origen `MOTOR`, exclusión por línea con reversión, pre-flight, RN-12 (correcciones → periodo siguiente); su mapa A.1 marcó «Transacciones no aplicadas / periodo activo → ✅ pool pleno = la corrida» |
| **Este REQ (2026-07-15)** | El cliente detalla la capacidad; el análisis confirma cobertura total salvo **un** delta (arrastre de insumos sin destino — P-03) y unifica el vocabulario: **ignorar = posponer = enviar a otro periodo** |
