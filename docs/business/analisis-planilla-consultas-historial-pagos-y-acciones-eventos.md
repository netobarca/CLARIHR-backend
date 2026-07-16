# Análisis de negocio — Planilla: consulta de historial de pagos por empleado y consulta de acciones/eventos aplicados en la planilla generada

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra el análisis/plan **RATIFICADOS** de REQ-012 + verificación de código con 3 exploraciones) |
| **Módulo** | Planilla — la **cara de consulta POR EMPLEADO** del motor: (1) historial de pagos del empleado a través de las planillas donde se le incluyeron pagos, con filtros/orden; (2) acciones de personal y eventos de planilla **ya aplicados** a un empleado en el periodo abierto con la planilla en estado `GENERADA` |
| **Fecha** | 2026-07-15 |
| **Autor** | Equipo CLARIHR — análisis asistido, validado contra `analisis-planilla-generacion.md` (RATIFICADO 2026-07-14) + `plan-tecnico-planilla-generacion.md` + análisis hermanos REQ-013/REQ-014 (ratificados también el 2026-07-15) + código (HEAD `597aa75`, rama `feature/planilla-descuentos`) |
| **Estado** | **RATIFICADO 2026-07-15 — P-01…P-04 respondidas (§17): P-01/P-02/P-04 aceptadas TAL CUAL · P-03 AJUSTADA («ambos: corporativo y el empleado pueden ver»)** → el **autoservicio de LECTURA del propio historial ENTRA a F1** como segunda superficie del delta (RF-005, molde self-or-view existente). **Delta propagado al plan técnico de REQ-012 el mismo día** (§1.4 índice · §3.7 consultas · §4 gate · §6 PR-7); cero condiciones de negocio pendientes |
| **Naturaleza del requerimiento** | **SUBCONJUNTO de REQ-012 + 1 delta quirúrgico de CONSULTA.** Los datos que ambas consultas muestran ya están completos en el modelo M4 ratificado (corrida + líneas con trazabilidad a fuente). Lo único que el plan de REQ-012 no trae es el **eje empleado**: sus consultas son corrida-céntricas (bandeja por nómina/periodo/estado/año + drill dentro de UNA corrida). El delta = **una consulta empleado-céntrica a través de corridas — superficie corporativa + autoservicio de lectura del propio empleado (P-03 ajustada) — + un índice** — cero entidades, cero migraciones nuevas (el índice viaja con M4), cero seeds, cero permisos nuevos (el autoservicio usa el gate self-or-view existente). Se entrega DENTRO de REQ-012 (PR-4 índice · PR-6 drill · PR-7 consultas · PR-8 guía FE) |
| **Documentos hermanos** | [`analisis-planilla-generacion.md`](analisis-planilla-generacion.md) (REQ-012 — el motor; su M4/RN-03 son el sustrato) · [`analisis-planilla-estado-revision-autorizacion.md`](analisis-planilla-estado-revision-autorizacion.md) (REQ-013 — la cara de consulta **corporativa**; este documento es la cara **por empleado**) · [`analisis-planilla-transacciones-no-aplicadas-y-seleccion.md`](analisis-planilla-transacciones-no-aplicadas-y-seleccion.md) (REQ-014 — aplicar/ignorar y rezagos: sus decisiones se VEN en estas consultas) · [`plan-tecnico-planilla-generacion.md`](../technical/plan-tecnico-planilla-generacion.md) (§1.4 modelo · §3.6 drill · §3.7 bandeja/reportes) |
| **Alcance geográfico** | El Salvador (SV) — mismo corte de toda la cadena |

---

## Contexto del cambio (requerimiento original)

> **Consulta de historial de pagos** — Esta consulta mostrará el historial de pagos de un empleado, en las diferentes planillas donde se ha incluido pagos. La interfaz deberá contar con filtros para ordenar la información según se requiera.
>
> **Consulta de acciones y eventos de planilla** — Esta consulta deberá permitir consultar las acciones de personal y eventos de planilla que ya se aplicaron para un empleado específico en el periodo de planillas abierto y que la planilla se encuentre en estado generada. Entre los eventos podrían ser: horas extras, servicios realizados, ingresos eventuales, etc. Entre las acciones incluidas podrían estar: amonestaciones, solicitudes de vacación, incapacidades, etc.

**Linaje del pedido.** El mapa de la visión (REQ-005 A.1, cerrado por REQ-012 A.1) ya declaraba «Historial de pago → ✅ REQ-005 + **líneas de corrida**»: los DATOS estaban resueltos, la CONSULTA no. REQ-013 (hoy) especificó la cara de consulta **corporativa** del motor (bandeja de corridas, revisión, exports); este levantamiento especifica la cara **por empleado** — el tercer levantamiento del día sobre el mismo motor, y como los dos anteriores, casi todo aterriza en PRs ya planificados.

---

## 0. Veredicto ejecutivo

1. **Los datos de ambas consultas ya están completos en el modelo ratificado — lo que falta es UNA consulta.** Cada línea de corrida (M4, plan §1.4) persiste empleado (`PersonnelFileId` + snapshots nombre/código), plaza, concepto, clase explícita, montos (calculado/override/final), `IsIncluded`, advertencias y la **trazabilidad a fuente** (`SourceModule` + `SourceReferencePublicId` — RN-03 de REQ-012); la cabecera persiste nómina, periodo, fecha de pago, estado y actores. El «historial de pagos» y las «acciones/eventos aplicados» son **dos lecturas del mismo modelo**, sin una pieza nueva de escritura.
2. **El delta quirúrgico: el plan de REQ-012 solo consulta por CORRIDA, no por EMPLEADO.** La bandeja (`payroll-runs/query`, plan §3.7) filtra por nómina/periodo/estado/año, y el drill (`GET …/{id}/employees/{fileId}`, §3.6) vive DENTRO de una corrida. Recorrer «las diferentes planillas donde se ha incluido pagos» de un empleado obligaría al FE a iterar todas las corridas. Propuesta (RF-001): **una consulta empleado-céntrica a través de corridas** — una fila por corrida donde el empleado tiene líneas, con sus totales (ingresos/descuentos/neto), filtros y orden — más **un índice de líneas por empleado** que viaja con la migración M4. Cero entidades, cero seeds, cero permisos nuevos; aterriza en PR-7 (la consulta) y PR-4 (el índice). **Ratificación 2026-07-15 (P-03 ajustada): el delta gana una segunda superficie — el empleado consulta SU propio historial y sus líneas** (gate self-or-view molde medical-claims/D-13, estados fijos `CERRADA`/`AUTORIZADA`), también en PR-7.
3. **La consulta 2 es SUBCONJUNTO PURO — y la sirve el mismo endpoint.** «Acciones y eventos que ya se aplicaron… planilla en estado generada» = las **líneas de la corrida `GENERADA`** del periodo abierto (estado `GENERADO`, extensión M2), trazadas a su registro fuente. El punto de entrada por empleado es RF-001 con filtro `estado=GENERADA`; el detalle es el drill ya planificado (§3.6, PR-6). Además la vista muestra las **decisiones de REQ-014** (líneas excluidas = ignoradas/pospuestas, `IsIncluded=false`) y los **rezagos arrastrados** con advertencia (si su P-03 se ratifica) — es la pantalla donde el analista VE lo que decidió.
4. **Mapa de los ejemplos del cliente — uno no genera línea.** Horas extras ✓ (`OVERTIME`), ingresos eventuales ✓ (`ONE_TIME_INCOME`), **servicios realizados** ✓ = tipo POR_OBRA: «se paga por obra un valor fijo» registrado como ingresos (golden 12 firmado; verificado: no existe módulo propio de servicios — catálogo `-9894`), amonestaciones ✓ (`DISCIPLINARY`, las APLICADA con descuento), incapacidades ✓ (`INCAPACITY`, descuento snapshot + patronal informativo). **«Solicitudes de vacación» ✗: la P-11 de REQ-012 ratificó que el goce NO altera el pago — las vacaciones no generan línea de planilla en F1** (verificado: el módulo maneja días contra fondo, sin montos). → **P-04 ratificada tal cual (2026-07-15)**: la consulta muestra lo aplicado CON efecto en la planilla; el contexto no monetario del periodo lo dan las consultas existentes (módulos fuente, disponibilidad de tiempos, tablero de acciones REQ-004).
5. **Semántica de estados — la misma máquina ratificada, dos usos.** «Historial de pagos» = corridas **`CERRADA`** (pago ejecutado) y **`AUTORIZADA`** (en proceso de pago), con el estado visible — `GENERADA` es borrador (cifras editables) y entra solo con filtro explícito (es exactamente el uso de la consulta 2); `ANULADA` queda fuera por defecto (histórico consultable con filtro). → **P-01 ratificada tal cual (2026-07-15)**.
6. **Lo que NO se fusiona: el ledger de la nómina externa.** `PersonnelFilePayrollTransaction` es sincronización de nómina EXTERNA con periodo en **texto libre** (verificado — ni siquiera es FK al maestro de periodos), y la P-16 de REQ-012 lo ratificó «intacto y sin uso» en F1. El historial de pagos nace **del motor** (sin producción a la fecha, no hay históricos externos que fusionar). → **P-02 ratificada tal cual (2026-07-15)**.
7. **Registro como REQ-015 sin PRs propios — y RATIFICADO el mismo día.** Entrega: PR-4 (índice con M4) · PR-6 (drill) · PR-7 (consulta corporativa + autoservicio) · PR-8 (guía FE). **P-01…P-04 respondidas 2026-07-15** (única ajustada: P-03 — el autoservicio del propio empleado entra a F1) y **delta propagado al plan técnico**; cero condiciones de negocio pendientes — resta la verificación de correspondencia al cerrar PR-6/PR-7/PR-8. **La «Próxima acción» del programa sigue siendo REQ-012 PR-1, sin cambios.**

### Trazabilidad frase a frase del levantamiento

| Frase del requerimiento | Cobertura (REQ-012 ratificado + plan técnico / delta) |
|---|---|
| «historial de pagos de un empleado» | Líneas M4 por `PersonnelFileId` a través de corridas (plan §1.4) — datos completos; **la consulta trans-corridas es el delta** (RF-001, PR-7 + índice PR-4) **+ autoservicio del propio empleado (RF-005 — P-03 ajustada en ratificación)** |
| «en las diferentes planillas donde se ha incluido pagos» | Una fila por corrida con líneas del empleado: nómina, periodo, fecha de pago, estado, totales del empleado (Σ de sus líneas `IsIncluded` por clase); estados por defecto `CERRADA`+`AUTORIZADA` (P-01) |
| «filtros para ordenar la información según se requiera» | Filtros año/nómina/tipo de nómina/estado/rango de fechas + `SortBy`/`SortDirection` + paginación — molde de las bandejas de la casa (verificado: `POST …/query` con groupBy de 8 dimensiones en REQ-006, `StatusCounts` span-todos) |
| «acciones de personal y eventos de planilla que **ya se aplicaron**» | Líneas de la corrida con `SourceModule`+`SourceReferencePublicId` (RN-03 de REQ-012); «aplicado» = línea persistida de la corrida vigente (los pools quedan APLICADA-origen `MOTOR` en la misma tx — D-09); las decisiones aplicar/ignorar (`IsIncluded`) y los rezagos de REQ-014 se ven aquí |
| «para un empleado específico» | Drill por empleado `GET …/payroll-runs/{id}/employees/{fileId}` (plan §3.6, PR-6) + entrada empleado-céntrica por RF-001 |
| «en el periodo de planillas abierto» | Periodo con estado `GENERADO` (extensión M2, catálogo `payroll-period-statuses`); el periodo cierra junto con su corrida (RF-015 de REQ-012) |
| «la planilla se encuentre en estado generada» | Corrida `StatusCode=GENERADA` (catálogo `payroll-run-statuses` `-9970`); filtro de estado del mismo endpoint RF-001 |
| «horas extras, servicios realizados, ingresos eventuales, etc.» | `OVERTIME` (valoradas por el motor: Σ horas×factor × valor-hora) · POR_OBRA vía ingresos (golden 12) · `ONE_TIME_INCOME` · (etc.: `RECURRING_INCOME`) |
| «amonestaciones, solicitudes de vacación, incapacidades, etc.» | `DISCIPLINARY` (APLICADA con descuento — snapshot REQ-003) · **vacaciones SIN línea en F1 (P-11 ratificada) → P-04** · `INCAPACITY` (descuento/subsidio snapshot REQ-001) · (etc.: `NOT_WORKED_TIME`, descuentos) |

### Mapa operativo — ejemplos del cliente → fuente → línea de corrida

| Ejemplo (levantamiento) | Módulo fuente (estado) | Cómo aparece en la consulta | Estado |
|---|---|---|---|
| Horas extras | REQ-007 `PersonnelFileOvertimeRecord` (🟢) | Línea Ingreso `SourceModule=OVERTIME`, valorada por el motor (REQ-012 §2.2 [3]) | ✅ plan PR-4/PR-5 |
| Servicios realizados | **Sin módulo propio** — POR_OBRA paga «valor fijo por obra» como ingresos (golden 12; catálogo `-9894`) | Líneas `RECURRING_INCOME`/`ONE_TIME_INCOME` de la plaza POR_OBRA | ✅ ratificado (caso 12) |
| Ingresos eventuales | REQ-006 `PersonnelFileOneTimeIncome` (🟢) | Línea Ingreso `ONE_TIME_INCOME` | ✅ plan PR-5 |
| Ingresos cíclicos («etc.») | REQ-005 `PersonnelFileRecurringIncome` (🟢) | Línea Ingreso `RECURRING_INCOME` (cuota) | ✅ plan PR-5 |
| Amonestaciones | REQ-003 `PersonnelFileDisciplinaryAction` (🟢) | Línea Descuento `DISCIPLINARY` — solo APLICADA con `HasPayrollDeduction` (RN de REQ-014); rezagadas via arrastre (REQ-014 P-03) | ✅ plan PR-5 |
| **Solicitudes de vacación** | REQ-001 `PersonnelFileVacationRequest` (🟢) | **SIN línea en F1** — P-11: el goce no altera el pago (días contra fondo, sin montos; verificado) | ⚠️ **P-04** |
| Incapacidades | REQ-001 `PersonnelFileIncapacity` (🟢) | Líneas `INCAPACITY`: descuento del empleado + aporte patronal informativo (snapshot por tramos) | ✅ plan PR-5 |
| Tiempos no trabajados («etc.») | REQ-011 `PersonnelFileNotWorkedTime` (🟢) | Línea Descuento `NOT_WORKED_TIME` (incluye permisos POR HORAS — golden 14) | ✅ plan PR-5 |
| Descuentos cíclicos/eventuales («etc.») | REQ-008/009 (🟢) | Líneas `RECURRING_DEDUCTION`/`ONE_TIME_DEDUCTION` | ✅ plan PR-5 |
| (base del cálculo) | motor | `SALARIO`, `LEY_ISSS/AFP/RENTA`, `PATRONAL_*` — parte del drill; no son «acción/evento» pero completan el pago | ✅ plan PR-4 |

---

## 1. Resumen del producto o requerimiento

Exponer al usuario la **cara por empleado** del motor de planillas (REQ-012), con dos consultas de solo lectura: (1) el **historial de pagos** de un empleado — una fila por cada planilla donde se le incluyeron pagos, con nómina, periodo, fecha de pago, estado y sus totales, filtrable y ordenable, con drill al detalle de líneas; y (2) las **acciones de personal y eventos de planilla ya aplicados** a un empleado específico en el periodo abierto cuya planilla está `GENERADA` — la vista de verificación por empleado previa a la autorización, con cada elemento trazado a su registro fuente.

**Problema que resuelve:** sin la primera consulta, responder «¿qué le han pagado a este empleado y cuándo?» exigiría recorrer planilla por planilla; sin la segunda, el analista no puede verificar de un vistazo qué eventos y acciones del periodo entraron al cálculo de UN empleado antes de autorizar. **Nota estructural:** los datos de ambas ya fueron levantados, ratificados y planificados en REQ-012; este documento confirma la cobertura, detecta **un** hueco quirúrgico (la consulta empleado-céntrica trans-corridas) y precisa cuatro puntos (§17).

## 2. Objetivos del negocio

1. **Transparencia histórica por empleado**: reconstruir en segundos qué se pagó, cuándo, por qué planilla y compuesto de qué — para atención de consultas del empleado, auditoría y constancias internas.
2. **Verificación por empleado antes de autorizar**: confirmar que las acciones/eventos del periodo (horas extras, eventuales, amonestaciones, incapacidades, tiempos no trabajados) entraron — o se pospusieron — en la planilla generada, con su rastro.
3. **Cero recálculo en la consulta**: todo lo mostrado es lo persistido por el motor (la consulta no calcula, no muta y no reinterpreta).
4. **Una sola fuente de verdad**: el historial ES las líneas de corrida — lo mismo que imprime la boleta y exporta la impresión de planilla (cuadre garantizado por las suites de REQ-012).

## 3. Alcance funcional

Dos consultas, ambas dentro de REQ-012 F1 (Olas B y C), con un delta de consulta:

- **A. Historial de pagos por empleado** — consulta trans-corridas (fila por planilla con totales del empleado), filtros (año, nómina, tipo, estado, rango de fechas) y orden; drill al detalle de líneas de esa planilla (RF-001, RF-002).
- **B. Acciones y eventos aplicados en el periodo abierto** — la misma consulta con filtro `estado=GENERADA` como punto de entrada + drill por empleado con clasificación por módulo fuente (`SourceModule`), decisiones de inclusión/exclusión visibles y navegación al registro origen (RF-003, RF-004).
- **C. Autoservicio del propio empleado (P-03 ajustada en ratificación)** — el empleado con usuario vinculado consulta SU historial de pagos y el detalle de sus líneas, limitado a corridas `CERRADA`/`AUTORIZADA` (RF-005).

**Solo lectura.** Los ajustes (override/incluir-excluir/recálculo/regeneración) y las decisiones (autorizar/devolver/cerrar) son de REQ-012/REQ-013/REQ-014 y NO se duplican aquí; estas consultas los **muestran** ya tomados.

## 4. Fuera de alcance

- **Autoservicio del empleado sobre BORRADORES o decisiones**: el empleado ve su historial (`CERRADA`/`AUTORIZADA` — RF-005, P-03 ajustada) pero NUNCA corridas `GENERADA`/`ANULADA` ni la consulta de revisión del periodo abierto (herramienta corporativa de REQ-013/REQ-014). La **descarga de su boleta PDF** tampoco entra: sigue en F2 (P-12 de REQ-012 — boleta vía RRHH); el autoservicio de F1 es la CONSULTA.
- **Fusión con el ledger de nómina externa** (`PersonnelFilePayrollTransaction`): P-16 de REQ-012 lo dejó intacto y sin uso; su consulta propia ya existe — **P-02**.
- **Exports del historial por empleado**: el levantamiento pide filtros, no exports; si se pidieran, el molde xlsx/csv ya está conectado a la corrida (REQ-013 RF-003) y agregarlos es aditivo.
- **Acciones sin efecto monetario dentro de estas consultas** (p. ej. una solicitud de vacación aprobada): las consultas muestran lo **aplicado a la planilla**; el contexto no monetario vive en las consultas ya construidas de cada módulo, la disponibilidad de tiempos y el tablero REQ-004 — **P-04** confirma.
- **Boleta PDF** (ya es REQ-012 RF-016), **exports de corrida** (REQ-013), **selección aplicar/ignorar** (REQ-014), reapertura de planillas, notificaciones.
- Lo ya excluido por REQ-012: programación (P-14), correo, asistencia/marcación, multi-nivel, ERP.

## 5. Actores o usuarios involucrados

| Actor | Rol en este requerimiento |
|---|---|
| **Analista de planilla** (`ManagePayrollRuns`) | Usa la consulta 2 en su flujo de revisión por empleado; consulta historiales para atención de casos |
| **Autorizador de planilla** (`AuthorizePayrollRuns`) | Verifica por empleado qué se aplicó antes de autorizar/devolver |
| **Gerencia RRHH / consulta** (`ViewPayrollRuns`) | Consulta historial y detalle en solo lectura (atención al empleado, auditoría) |
| **Contador (externo)** | Contrasta el historial de un empleado contra boletas/exports en el piloto |
| **Empleado** (usuario vinculado al expediente) | **Consulta SU propio historial de pagos y el detalle de sus líneas** en corridas `CERRADA`/`AUTORIZADA` (P-03 ajustada — RF-005); su boleta PDF sigue vía RRHH (F2) |

## 6. Requerimientos funcionales

> Numeración local RF-001…RF-004; cada uno declara su **cobertura**. Solo RF-001 tiene desarrollo propio (una consulta + un índice); el resto es correspondencia.

### RF-001 - Historial de pagos por empleado (la consulta trans-corridas) — **DELTA**

**Descripción:** Consulta paginada que, para un empleado, devuelve **una fila por corrida de planilla donde tiene líneas**: nómina (código/nombre/tipo), periodo (label + rango + fecha de pago), estado de la corrida, moneda, y los **totales del empleado en esa corrida** (Σ ingresos, Σ descuentos, neto — solo líneas `IsIncluded`; el costo patronal informativo no altera el neto). Filtros: año, nómina, tipo de nómina, estado(s), rango de fechas (periodo o pago). Orden: por fecha de pago/periodo (default desc) con `SortBy`/`SortDirection` del molde de bandejas.

**Reglas de negocio:**
- Estados por defecto: `CERRADA` + `AUTORIZADA` (con el estado visible); `GENERADA` y `ANULADA` solo con filtro explícito (P-01) — `GENERADA` es la puerta de la consulta 2 (RF-003).
- Una fila = la corrida **vigente** de esa nómina+periodo (las `ANULADA` no duplican filas por defecto).
- Los totales del empleado se derivan de SUS líneas persistidas (`FinalAmount` de las incluidas, por clase) — cero recálculo del motor.
- Solo lectura bajo `ViewPayrollRuns` con gate fail-closed; multi-tenant.
- Empleado sin corridas → lista vacía (200), no error.

**Criterios de aceptación:** filtros combinables; paginación estándar; Σ de la fila ≡ Σ de las líneas del drill (RF-002) ≡ boleta del empleado; fila `AUTORIZADA` y `CERRADA` distinguibles; orden estable.

**Prioridad:** Alta.
**Dependencias:** REQ-012 PR-4/PR-5 (corridas y líneas existentes).
**Cobertura:** datos = M4 (plan §1.4, ratificado). **Consulta nueva** en el reporting controller de corridas (plan §3.7) + **índice de líneas por empleado** `(tenant, personnel_file_id, payroll_run_id)` que viaja con la migración M4 → **PR-7 (consulta) + PR-4 (índice)**, propagación al plan al ratificar (§18-3).

### RF-002 - Detalle de un pago (drill de líneas del empleado en una corrida)

**Descripción:** Desde una fila del historial, abrir el detalle de las líneas del empleado en esa corrida: concepto (código/nombre), clase (`Ingreso`/`Descuento`/`PagoPatronal`), unidades, base, monto calculado, override (con nota/actor), monto final, incluida sí/no, módulo fuente, referencia al registro origen y advertencias — más la plaza (`assignedPositionPublicId`) de cada línea (multi-plaza: líneas por plaza, consulta consolidada por empleado — P-15 de REQ-012).

**Reglas de negocio:** las de REQ-013 RF-002 (es el mismo drill); en corridas `CERRADA`/`AUTORIZADA` las cifras son definitivas/congeladas; en `GENERADA` son provisionales (el FE lo marca por el estado).

**Criterios de aceptación:** cada línea expone `sourceModule` + `sourceReferencePublicId`; totales del drill ≡ fila del historial; advertencias visibles por línea.

**Prioridad:** Alta.
**Dependencias:** RF-001.
**Cobertura:** REQ-012 RF-010/RF-019 · plan §3.6 (`GET …/payroll-runs/{id}/employees/{fileId}`) · **PR-6** — subconjunto puro (cero desarrollo).

### RF-003 - Consulta de acciones y eventos aplicados (periodo abierto, planilla `GENERADA`)

**Descripción:** Para un empleado específico, listar las **acciones de personal y eventos de planilla ya aplicados** en la(s) corrida(s) `GENERADA` de periodo(s) abierto(s): horas extras valoradas, ingresos eventuales/cíclicos (incluye «servicios» POR_OBRA), amonestaciones con descuento, incapacidades, tiempos no trabajados y descuentos — es decir, las líneas del empleado con su `SourceModule`, montos, decisión de inclusión y advertencias (incluida la marca de rezago arrastrado de REQ-014 si su P-03 se ratifica).

**Reglas de negocio:**
- «Periodo abierto» = periodo en estado `GENERADO`; «planilla generada» = corrida `GENERADA` (la vigente de esa nómina+periodo). Un empleado con plazas en varias nóminas puede tener más de una corrida `GENERADA` simultánea — la consulta las lista todas (una fila por corrida, drill por cada una).
- «Aplicado» = línea persistida de la corrida (el pool fuente quedó APLICADA-`MOTOR` en la misma transacción — D-09); una línea excluida (`IsIncluded=false`) se muestra **marcada** como ignorada/pospuesta (REQ-014), no se oculta.
- La agrupación «eventos» (ingresos: HE, eventuales, cíclicos) vs «acciones» (amonestaciones, incapacidades, tiempos no trabajados) es **presentación del FE sobre `SourceModule`** (lista estable del plan §1.4); las líneas de base (`SALARIO`, `LEY_*`, `PATRONAL_*`) completan el drill pero no son «acción/evento».
- Acciones sin efecto monetario en la planilla (p. ej. solicitudes de vacación — P-11) no aparecen: ver P-04.
- Solo lectura; cualquier ajuste se hace con los mecanismos de REQ-012/REQ-014 (solo en `GENERADA`).

**Criterios de aceptación:** dado un empleado con HE autorizada, un eventual, una amonestación aplicada y una incapacidad en el periodo, la consulta lista las 4 con su módulo fuente y montos tras generar; una línea excluida aparece marcada; sin corrida `GENERADA` → vacío con respuesta clara (no error).

**Prioridad:** Alta.
**Dependencias:** RF-001 (punto de entrada con `estado=GENERADA`) + RF-002 (detalle).
**Cobertura:** REQ-012 RF-010/RF-019 + M4 `SourceModule`/`SourceReferencePublicId`/`IsIncluded` · plan §3.6 · **PR-6/PR-7** — subconjunto puro (el endpoint es el mismo de RF-001).

### RF-004 - Trazabilidad navegable a la fuente

**Descripción:** Cada acción/evento (línea) navega a su registro origen vía `sourceReferencePublicId`: la jornada de HE (fecha, horas, factor), el eventual (referencia, fórmula), la cuota (número, plan), la amonestación (causa, tipo), la incapacidad (rango, tramos), el tiempo no trabajado (fechas/horas, tipo) — usando los endpoints por-registro **ya construidos y certificados** de cada módulo.

**Reglas de negocio:** la referencia es de solo lectura (RN-03 de REQ-012); el acceso al detalle fuente respeta los permisos `View*` de cada módulo; línea sin referencia (SALARIO/ley/patronal) no navega.

**Criterios de aceptación:** desde una línea `OVERTIME` se llega a la jornada con sus horas y factor; desde `DISCIPLINARY` a la amonestación; los permisos por módulo se respetan (403 si el consultor no tiene el `View*` correspondiente).

**Prioridad:** Media (la referencia ya viaja en RF-002/RF-003; esto fija la navegación).
**Dependencias:** RF-002/RF-003; módulos fuente (existentes).
**Cobertura:** REQ-012 RN-03 + endpoints por-registro existentes (verificados §A.2) · **PR-8** documenta el mapa `SourceModule`→endpoint en la guía FE.

### RF-005 - Autoservicio: el empleado consulta su propio historial — **DELTA (P-03 AJUSTADA en ratificación)**

**Descripción:** El empleado cuyo expediente está vinculado a su usuario consulta **su propio** historial de pagos (una fila por corrida, mismos campos que RF-001) y el detalle de **sus** líneas por corrida — el contenido de su boleta, en pantalla.

**Reglas de negocio:**
- **Gate self-or-view** (molde medical-claims / lectura D-13, verificado §A.2): pasa quien tiene `ViewPayrollRuns` O es el usuario vinculado al expediente consultado (`LinkedUserPublicId`) — **cero permisos nuevos**.
- **Estados FIJOS `CERRADA` + `AUTORIZADA`** (sin parámetro de estado): el empleado nunca ve borradores (`GENERADA`), anuladas ni la consulta de revisión del periodo abierto (RF-003 es corporativa).
- Solo el propio expediente: intentar otro → 403/404; expediente sin usuario vinculado → sin acceso self-service (RRHH consulta por él con RF-001).
- La descarga de la boleta PDF NO se habilita por esta vía (F2 — P-12 de REQ-012).

**Criterios de aceptación:** el empleado vinculado lista sus pagos `CERRADA`/`AUTORIZADA` y abre sus líneas; un tercero sin `ViewPayrollRuns` → 403; el propio empleado pidiendo otro expediente → 403/404; ninguna corrida `GENERADA`/`ANULADA` aparece por ningún filtro.

**Prioridad:** Alta (ratificada por el negocio: «ambos… pueden ver»).
**Dependencias:** RF-001 (misma consulta interna); vínculo usuario↔expediente (existe).
**Cobertura:** **DELTA** — segunda superficie del endpoint de RF-001 bajo `personnel-files/{publicId}` con el gate existente · propagado al plan §3.7 · **PR-7**.

## 7. Requerimientos no funcionales

| Categoría | Requisito |
|---|---|
| Seguridad | Gate fail-closed `ViewPayrollRuns` en las consultas corporativas; **autoservicio con gate self-or-view** (`ViewPayrollRuns` O expediente vinculado al usuario — molde medical-claims/D-13) con **estados fijos `CERRADA`/`AUTORIZADA`** y solo el propio expediente; drill a fuente respeta los `View*` por módulo; multi-tenant por `TenantId`; sin montos en logs; sin mutaciones (cero If-Match) |
| Rendimiento | Historial pagina por corrida y agrega SOLO las líneas del empleado (índice `(tenant, personnel_file_id, payroll_run_id)` — viaja con M4); sin agregación corporativa al vuelo; drill ya paginado por diseño (§3.6) |
| Exactitud | Fila del historial ≡ Σ líneas incluidas del drill ≡ boleta ≡ export de la corrida (suites de cuadre de REQ-012); estados siempre visibles para distinguir provisional (`GENERADA`) de definitivo (`CERRADA`) |
| Usabilidad | Estados como catálogo país (`payroll-run-statuses`) para render; `SourceModule` con lista estable documentada; errores/respuestas vacías claras; español en toda etiqueta de export/documento |
| Auditoría | Las consultas no generan asientos (D-14); lo consultado ya trae actor/fecha de cada decisión (generó/ajustó/autorizó/cerró) |
| Compatibilidad | Convenciones del repo: `api/v1`, `POST …/query` para búsqueda con filtros, `PagedResponse`, enums string, `XxxId`→`xxxPublicId`, openapi.yaml a mano + guardrails |
| Mantenibilidad | **Una consulta + un índice nuevos**; todo lo demás es el modelo/los endpoints ya planificados de REQ-012 y los endpoints certificados de los módulos fuente |

## 8. Historias de usuario

### HU-001 - Responder «¿qué me pagaron?»
Como **analista de RRHH**, quiero **ver el historial de pagos de un empleado con filtros por año y nómina**, para **responder sus consultas y sustentar constancias sin recorrer planilla por planilla**.
**Criterios:** Dado un empleado con pagos en 6 quincenas, cuando filtro por año, entonces veo 6 filas con periodo, fecha de pago, estado y sus totales, ordenadas por fecha de pago descendente.

### HU-002 - Verificar a un empleado antes de autorizar
Como **autorizador de planilla**, quiero **consultar qué acciones y eventos se aplicaron a un empleado en la planilla generada del periodo abierto**, para **confirmar su cálculo antes de autorizar**.
**Criterios:** Dada una corrida `GENERADA` con HE, un eventual y una amonestación del empleado, cuando abro la consulta, entonces veo los 3 elementos con módulo fuente, monto y decisión de inclusión.

### HU-003 - Explicar una línea
Como **analista de planilla**, quiero **navegar de una línea del pago al registro que la originó**, para **explicar de dónde salió un monto (jornada, cuota, amonestación, incapacidad)**.
**Criterios:** Dada una línea `OVERTIME`, cuando navego por su referencia, entonces llego a la jornada con fecha, horas y factor.

### HU-004 - Ver una decisión de posponer
Como **analista de planilla**, quiero **ver marcadas las transacciones ignoradas en la planilla generada**, para **saber qué quedó pospuesto para otro periodo** (REQ-014).
**Criterios:** Dada una línea excluida, cuando consulto las acciones/eventos del empleado, entonces la veo marcada como no incluida (y sé que su transacción volvió a pendiente).

## 9. Reglas de negocio (consolidadas)

- **RN-01** Ambas consultas son de **solo lectura**: no mutan corridas, líneas ni registros fuente; no generan asientos.
- **RN-02** Historial por defecto = corridas `CERRADA` + `AUTORIZADA` con estado visible; `GENERADA`/`ANULADA` solo con filtro explícito (P-01).
- **RN-03** Fila del historial = corrida **vigente** por nómina+periodo (índice único parcial de REQ-012); las anuladas no duplican historia.
- **RN-04** Cifras = las persistidas: `FinalAmount = override ?? calculado`; solo líneas `IsIncluded` suman al neto; el costo patronal es informativo (clase `PagoPatronal`).
- **RN-05** Toda línea traza a su fuente (`SourceModule` + `SourceReferencePublicId` — RN-03 de REQ-012); la lista de módulos fuente es la del plan §1.4, estable para el FE.
- **RN-06** «Aplicado» = línea de la corrida; una línea excluida se muestra marcada (ignorada = pospuesta, REQ-014) — nunca se oculta en la consulta 2.
- **RN-07** Acciones sin efecto monetario en la planilla no aparecen en estas consultas (vacaciones — P-11 de REQ-012; amonestaciones sin descuento; ver P-04): su consulta vive en los módulos fuente/tablero REQ-004.
- **RN-08** Multi-plaza: líneas por plaza, consulta consolidada por empleado con la plaza visible por línea (P-15 de REQ-012). Multi-nómina: una fila por corrida.
- **RN-09** Permisos: lectura corporativa bajo `ViewPayrollRuns` fail-closed; el drill al registro fuente exige el `View*` del módulo correspondiente.
- **RN-10** Autoservicio (P-03 ajustada): el empleado solo ve SU expediente (gate self-or-view por usuario vinculado) y solo corridas `CERRADA`/`AUTORIZADA` — sin ningún filtro que exponga `GENERADA`/`ANULADA`; la consulta del periodo abierto (RF-003) y la boleta PDF siguen fuera de su superficie.

## 10. Flujos principales

### Flujo 1 — Historial de pagos (atención de consulta)
1. El analista abre la ficha del empleado → historial de pagos. 2. Filtra por año/nómina. 3. El sistema lista una fila por planilla con totales y estado. 4. El analista abre una fila. 5. El sistema muestra el drill de líneas (RF-002) — coincide con la boleta entregada.

### Flujo 2 — Verificación por empleado antes de autorizar
1. Periodo abierto con corrida `GENERADA`. 2. El autorizador consulta las acciones/eventos aplicados del empleado (RF-003). 3. Verifica cada elemento contra su fuente (RF-004). 4. Detecta algo indebido → lo resuelve con los mecanismos de REQ-012/REQ-014 (excluir/override/recalcular — fuera de esta consulta). 5. Autoriza (REQ-013).

### Flujo 3 — Explicación de una línea
1. Desde el historial (o la consulta 2), el analista abre una línea. 2. Navega por `sourceReferencePublicId` al registro origen. 3. El módulo fuente muestra el detalle completo (fechas, horas, causa, plan de cuotas o tramos).

### Flujo 4 — El empleado consulta su historial (P-03 ajustada)
1. El empleado (usuario vinculado a su expediente) abre «Mi historial de pagos». 2. El sistema lista sus corridas `CERRADA`/`AUTORIZADA` con totales. 3. Abre una fila y ve sus líneas — el contenido de su boleta, en pantalla. 4. Cualquier duda la gestiona con RRHH (que dispone del contexto completo vía RF-001…RF-004).

## 11. Flujos alternativos y excepciones

| Escenario | Comportamiento |
|---|---|
| Empleado sin corridas (o sin líneas en el filtro) | 200 con lista vacía — no es error |
| Periodo abierto sin corrida `GENERADA` (aún no se genera) | Consulta 2 vacía; el FE lo comunica («aún no hay planilla generada del periodo») |
| Empleado inexistente / de otra empresa | 404 / aislamiento por tenant |
| Usuario sin `ViewPayrollRuns` | 403 (gate fail-closed) |
| Drill a fuente sin el `View*` del módulo | 403 del módulo fuente (la línea sigue visible con su monto) |
| Corrida `ANULADA` en el rango | Excluida por defecto; visible solo con filtro explícito (histórico/auditoría) |
| Corrida `GENERADA` consultada como «pago» | Solo con filtro explícito; el FE marca cifras provisionales (P-01) |
| Empleado en 2 nóminas con periodos abiertos | Dos filas `GENERADA`; el drill se abre por corrida |
| Línea excluida (`IsIncluded=false`) | Visible y marcada en la consulta 2; no suma al neto (RN-04/RN-06) |
| Registro fuente anulado DESPUÉS de cerrar la corrida | La línea histórica no cambia (snapshot); la discrepancia se gestiona en el periodo siguiente (RN-12 de REQ-012) |
| Empleado sin usuario vinculado al expediente | Sin acceso self-service (el gate exige el vínculo); RRHH consulta por él (RF-001) |
| Empleado intenta consultar otro expediente | 403/404 (gate self-or-view — RN-10) |
| Empleado intenta ver corridas `GENERADA`/`ANULADA` | Imposible por contrato: la superficie self-service no tiene parámetro de estado (estados fijos) |

## 12. Datos requeridos

**Este requerimiento no modela datos nuevos — cero entidades, cero seeds; un índice adicional sobre una tabla YA planificada (M4).** Se listan los campos que las consultas **exponen** (la superficie de autoservicio RF-005 expone estas MISMAS vistas, limitadas a `CERRADA`/`AUTORIZADA` y al propio expediente):

### Vista: fila del historial de pagos (derivada de `PayrollRun` + líneas del empleado)

| Campo | Tipo | Descripción |
|---|---|---|
| payrollRunPublicId · statusCode | Guid / catálogo `payroll-run-statuses` | La corrida y su estado (`CERRADA`/`AUTORIZADA` por defecto — P-01) |
| payrollDefinitionCode/Name · payrollTypeCode | Texto | Nómina (snapshot) y su tipo |
| periodLabel · periodStartDate/EndDate · paymentDate | Texto/fechas | Periodo (snapshot) y fecha de pago |
| employeeIncomeTotal · employeeDeductionTotal · employeeNetTotal | Decimal | Σ de las líneas `IsIncluded` del empleado por clase (el patronal no altera el neto) |
| currencyCode · hasWarnings · lineCount | Texto/bool/int | Moneda snapshot; si alguna línea del empleado trae advertencias; # líneas |

### Vista: drill de líneas (la misma de REQ-013 §12 — `PayrollRunLine`)

Concepto/clase/unidades/base/calculado/override(nota-actor)/final/incluida/`sourceModule`/`sourceReferencePublicId`/advertencias/moneda + `assignedPositionPublicId` (plaza). Sin campos nuevos.

### Índice nuevo (nota técnica — viaja con M4)

`payroll_run_lines (tenant_id, personnel_file_id, payroll_run_id)` — habilita el eje empleado sin escaneo por corrida; se suma a los dos índices ya planificados (§1.4). Propagar al plan al ratificar (§18-3).

## 13. Integraciones necesarias

| Integración | Estado |
|---|---|
| Motor/corrida de REQ-012 (M4: corridas + líneas trazadas) | En plan (PR-4/PR-5) — **dependencia dura: sin motor no hay nada que consultar** |
| Drill por corrida (`GET …/{id}/employees/{fileId}`) | En plan (PR-6) — se reusa tal cual |
| Endpoints por-registro de los módulos fuente (HE, eventuales, cíclicos, amonestaciones, incapacidades, TNT, descuentos) | **Existen** (🟢 certificados; verificados §A.2) — destino de la navegación RF-004 |
| Exportador xlsx/csv | Existe — **no requerido en F1** para el historial (supuesto §16); la corrida ya exporta por REQ-013 |
| Ledger externo `PersonnelFilePayrollTransaction` | **No se integra** (P-16 de REQ-012; P-02 local) |

## 14. Roles y permisos

**Cero permisos nuevos** — se usan los 5 de REQ-012 (PR-1):

| Rol/Permiso | Permisos | Restricciones |
|---|---|---|
| `PersonnelFiles.ViewPayrollRuns` | Historial de pagos + consulta de acciones/eventos + drill (lectura) | No ajusta ni decide; el drill a fuente exige además el `View*` del módulo |
| `PersonnelFiles.ManagePayrollRuns` | Lo anterior (su flujo de revisión usa la consulta 2) | Los ajustes son de REQ-012, no de estas consultas |
| `PersonnelFiles.AuthorizePayrollRuns` | Consulta como insumo de su decisión | Sin Admin implícito; anti-self (REQ-012) |
| Empleado (usuario vinculado) | **Su propio historial + detalle de sus líneas** (RF-005) — gate self-or-view, sin permiso nuevo | Solo corridas `CERRADA`/`AUTORIZADA`; solo SU expediente; sin consulta del periodo abierto ni boleta PDF (F2) |

## 15. Criterios de aceptación generales

1. Historial por empleado: una fila por corrida con nómina/periodo/fecha de pago/estado/totales del empleado; filtros combinables (año, nómina, tipo, estado, rango) y orden; paginación estándar.
2. Σ de cada fila ≡ Σ líneas incluidas del drill ≡ boleta del empleado ≡ export de la corrida (cuadre por suite).
3. Estados por defecto del historial = `CERRADA`+`AUTORIZADA`; `GENERADA`/`ANULADA` solo con filtro explícito; el estado siempre viaja en la respuesta.
4. Consulta 2: con corrida `GENERADA` del periodo abierto, lista TODAS las líneas del empleado con `sourceModule`, montos, `isIncluded` y advertencias (incluidas las marcas de rezago de REQ-014 si aplica); sin corrida generada → vacío claro.
5. Toda línea traza a su fuente; la navegación funciona para los módulos con efecto (HE, ingresos, descuentos, amonestaciones, incapacidades, TNT) respetando permisos.
6. Cero escrituras: ninguna de las dos consultas genera asientos, mutaciones ni bloqueos.
7. Autoservicio (RF-005): el empleado vinculado lista su historial y abre sus líneas (`CERRADA`/`AUTORIZADA`); nunca ve borradores, anuladas ni expedientes ajenos (probado con 403/404); sin permisos nuevos.
8. openapi.yaml sin drift; respuestas/errores bilingües donde aplique; guía FE (REQ-012 PR-8) documenta las tres superficies (historial corporativo, consulta del periodo abierto, autoservicio) y el mapa `SourceModule`→endpoint fuente.

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **Expectativa sobre «solicitudes de vacación»**: el cliente las lista como ejemplo, pero la P-11 ratificada implica que NO generan línea (goce no altera el pago). Si el negocio espera verlas en esta pantalla, la respuesta es composición FE de consultas existentes (cero backend) — P-04 lo resuelve en ratificación, no en código.
- **Confusión con el ledger externo**: hay una consulta que «parece» historial de pagos (`payroll-transactions`) pero es sincronización externa con periodo en texto libre; comunicar que el historial nuevo nace del motor (P-02) evita reportes cruzados.
- **Autoservicio (resuelto en ratificación — P-03 ajustada)**: el empleado ve su historial en F1. El riesgo residual es de EXPECTATIVA sobre lo que NO incluye (borradores del periodo abierto, boleta PDF descargable — F2): comunicarlo en la guía FE y en la adopción.
- **Riesgo técnico: bajo.** Una consulta de lectura + un índice; el riesgo del programa sigue viviendo en el motor (Ola B), no aquí.

### Supuestos
- Los totales por empleado-corrida se derivan al consultar (GROUP BY sobre el índice nuevo; páginas acotadas) — no se persisten columnas nuevas.
- La consulta refleja lo aplicado **por la corrida**; aplicaciones manuales de borde (P-09 de REQ-012) siguen visibles en las consultas de su módulo, no aquí.
- «Filtros para ordenar» no exige exports en F1; si se piden, el molde xlsx/csv ya está conectado a la corrida (costo marginal).
- La agrupación «eventos» vs «acciones» es presentación del FE sobre `SourceModule` (lista estable del plan §1.4).
- Sin producción a la fecha (2026-07-15): sin datos históricos que migrar; la estabilidad del contrato FE sí aplica.

### Dependencias
- **REQ-012 PR-4/PR-5** (corridas + líneas + índice) — sustrato; **PR-6** (drill) y **PR-7** (la consulta nueva) — entrega; **PR-8** — guía FE.
- Ratificación: ✅ **completada 2026-07-15** (P-01/P-02/P-04 tal cual · P-03 ajustada — autoservicio del propio empleado a F1). **Nada bloquea ningún PR.**
- Propagación del delta al plan técnico de REQ-012: ✅ **hecha 2026-07-15** (§1.4 índice · §3.7 consultas corporativa+autoservicio · §4 gate · §6 PR-7).

## 17. Preguntas al negocio — **P-01…P-04 RESPONDIDAS (ratificación 2026-07-15)**

P-01, P-02 y P-04 aceptadas con la recomendación tal cual; **P-03 ajustada** (en negrita).

| # | Ámbito | Pregunta | Respuesta del negocio (2026-07-15) |
|---|---|---|---|
| **P-01** | Estados del historial | ¿Qué cuenta como «pago» en el historial: solo planillas `CERRADA` (pago ejecutado y periodo cerrado), o también `AUTORIZADA` (congelada, en proceso de pago)? ¿Las `ANULADA` deben poder consultarse? | **Aceptada la recomendación tal cual** → por defecto **`CERRADA` + `AUTORIZADA` con el estado visible**; `GENERADA` solo con filtro explícito (es borrador — y es la puerta de la consulta 2); `ANULADA` solo con filtro (auditoría). Un solo endpoint con filtro de estados |
| **P-02** | Alcance del historial | ¿El historial es SOLO de las planillas del motor interno, o debe incluir pagos históricos de la nómina externa (`payroll-transactions`, sincronización con periodo en texto libre)? | **Aceptada la recomendación tal cual** → **solo motor interno**; el ledger externo queda intacto y con su consulta propia (coherente con P-16 de REQ-012) |
| **P-03** | Autoservicio | ¿El «historial de pagos» lo consulta solo RRHH (corporativo) en F1, o el empleado debe ver el suyo (portal)? | **AJUSTADA: «Ambos: corporativo y el empleado pueden ver»** → el **autoservicio de LECTURA del propio historial ENTRA a F1** (RF-005): gate self-or-view existente (`LinkedUserPublicId`, molde medical-claims/D-13), **estados FIJOS `CERRADA`/`AUTORIZADA`**, solo el propio expediente y sin permisos nuevos. La consulta del periodo abierto (RF-003) sigue corporativa y la **boleta PDF sigue en F2** (P-12 de REQ-012) |
| **P-04** | Acciones sin efecto monetario | El ejemplo «solicitudes de vacación»: la P-11 ratificada dice que el goce NO altera el pago (sin línea de planilla en F1). ¿Se confirma que esta consulta muestra las acciones **con efecto en la planilla** (amonestaciones con descuento, incapacidades, tiempos no trabajados), y que el contexto no monetario del periodo (vacaciones aprobadas, amonestaciones documentales) se consulta en sus módulos/tablero como hoy? | **Aceptada la recomendación** → la consulta muestra lo aplicado CON efecto en la planilla; las vacaciones (sin línea — P-11) no aparecen; el contexto no monetario sigue en sus módulos/tablero (si se quisiera en pantalla: composición FE de lecturas existentes, documentable en la guía — cero backend) |

## 18. Recomendaciones del Analista de Negocio

1. **No abrir desarrollo propio.** Registrar como **REQ-015** con entrega vía REQ-012 (PR-4 índice · PR-6 drill · PR-7 consulta · PR-8 guía FE) y usar este documento como checklist de correspondencia — mismo protocolo que REQ-013/REQ-014.
2. ~~Ratificar P-01…P-04~~ **HECHO (2026-07-15)**: P-01/P-02/P-04 tal cual · P-03 ajustada (autoservicio del propio empleado a F1). La «Próxima acción» del programa (**REQ-012 PR-1**) sigue sin cambios.
3. ~~Propagar el delta al plan técnico~~ **HECHO (2026-07-15)** en 4 puntos quirúrgicos: §1.4 (+índice `payroll_run_lines (tenant, personnel_file_id, payroll_run_id)` — viaja con M4/PR-4), §3.7 (+consulta corporativa `payroll-runs/employee-history/query` **+ superficie de autoservicio** `personnel-files/{publicId}/payroll-history` con gate self-or-view y estados fijos), §4 (nota del gate — cero permisos nuevos) y §6 (PR-7). Cero errores/códigos nuevos (respuestas vacías no son error).
4. **Un endpoint, dos consultas (+ una superficie propia)**: comunicar al FE que el historial (estados `CERRADA`/`AUTORIZADA`) y la consulta del periodo abierto (`GENERADA`) son la MISMA consulta corporativa con filtros distintos + el drill común — una sola integración; el autoservicio (RF-005) es la misma vista bajo `personnel-files/{publicId}` con gate propio y estados fijos.
5. **Publicar en la guía FE el mapa `SourceModule` → módulo fuente → endpoint de detalle** (la tabla del §0 de este documento): es lo que convierte «confirmar que se aplicó» en un clic, y fija la agrupación eventos/acciones como presentación.
6. **Usar la consulta 2 como paso estándar del piloto** (checklist de REQ-012): verificación por empleado de la primera corrida en paralelo contra la nómina externa — complementa los exports corporativos de REQ-013 con la vista fina por persona.
7. **No fusionar el ledger externo** (P-02 ratificada). Para el autoservicio (P-03 ajustada): mantenerlo de **LECTURA con estados fijos y gate self-or-view** — la superficie del empleado no crece hacia borradores, decisiones ni descarga de boleta (F2) sin un nuevo levantamiento.

---

## Anexo A — Correspondencia, verificaciones y linaje

### A.1 Mapa de entrega (RF locales → programa)

| RF local | Cobertura | Plan técnico | PR |
|---|---|---|---|
| RF-001 Historial por empleado | **DELTA de consulta** (datos = M4 ratificado) | §3.7 (+endpoint) · §1.4 (+índice) al propagar | **PR-7** (+PR-4 índice) |
| RF-002 Drill del pago | REQ-012 RF-010/RF-019 | §3.6 `GET …/{id}/employees/{fileId}` | PR-6 |
| RF-003 Acciones/eventos aplicados | REQ-012 RF-010/RF-019 + M4 (`SourceModule`/`IsIncluded`) + REQ-014 (decisiones/rezagos visibles) | §3.6 + RF-001 con `estado=GENERADA` | PR-6/PR-7 |
| RF-004 Navegación a la fuente | RN-03 de REQ-012 + endpoints por-registro existentes | guía FE (mapa fuente→endpoint) | PR-8 |
| RF-005 Autoservicio del propio historial | **DELTA (P-03 ajustada 2026-07-15)** — gate self-or-view existente (medical-claims/D-13) | §3.7 (+`personnel-files/{publicId}/payroll-history`) | **PR-7** |

### A.2 Verificaciones de código de este análisis (2026-07-15, HEAD `597aa75`, rama `feature/planilla-descuentos`)

- **La corrida NO existe aún** (0 hits de `PayrollRun`/`PayrollLine` en Domain/Application) — todo el sustrato de estas consultas es el modelo M4 del plan de REQ-012; coincide con la verificación de REQ-013/REQ-014 de hoy.
- **No hay consulta empleado-céntrica planificada**: plan §3.7 = `payroll-runs/query` con filtros nómina/periodo/estado/año; §3.6 = drill dentro de UNA corrida — confirma el delta de RF-001.
- **Vacaciones sin montos**: `PersonnelFileVacationRequest`/`PersonnelFileVacationPeriod` (`src/CLARIHR.Domain/PersonnelFiles/PersonnelFileVacations.cs`) modelan DÍAS contra fondo por empleado (asignaciones FIFO/devoluciones LIFO), sin monto ni FK de corrida; su rastro es el journal (`GOCE_VACACIONES -9488`/`DEVOLUCION_VACACIONES -9489`) — sustenta P-11/P-04.
- **Amonestaciones con sustrato completo**: `PersonnelFileDisciplinaryAction` (`PersonnelFilePersonnelTransactions.cs`) con bloque opcional de deducción (`HasPayrollDeduction`/`DeductionAmount`/concepto snapshot congelado al aplicar) + suspensión; hoy su efecto es documental (asientos `AMONESTACION -9477`/`SUSPENSION -9478`, sin periodo ni cobro) — el motor la toma como línea `DISCIPLINARY` (golden 9) y REQ-014 P-03 cubre las rezagadas.
- **Incapacidades listas para el motor**: `PersonnelFileIncapacity` con `PayrollPeriodDefinitionId` + snapshot de montos por tramos (`SubsidyAmount`/`DiscountAmount`/`EmployerAmount` + `TrancheDetailJson`) — la línea `INCAPACITY` solo lo toma (P-10).
- **TNT sin destino ni consumo** (`PersonnelFileNotWorkedTime`: solo fechas/horas + `DiscountAmount`) — confirma que la marca de consumo del empleado nace en las líneas M4 (REQ-014).
- **POR_OBRA sin módulo propio**: `-9894` es código del catálogo `payroll-types`; no existe entidad «servicios» — el pago por obra son ingresos (golden 12), por eso «servicios realizados» aparece como líneas de ingreso.
- **Ledger externo no fusionable**: `PersonnelFilePayrollTransaction` (`PersonnelFileEmployee.cs:673-763`) es inmutable de facto, con `PayrollPeriodCode` **texto libre** (sin FK al maestro de periodos) y `SourceSystem`/`SourceReference` — espejo de nómina externa con consulta propia (`GET …/payroll-transactions`); sustenta P-02.
- **Permisos**: hoy NO existe ningún `ViewPayroll*` (el ledger externo usa `PersonnelFiles.Read/Manage`); los 5 permisos de corrida los crea REQ-012 PR-1 — estas consultas no agregan ninguno.
- **Molde de autoservicio para F2 (P-03)**: gate por `LinkedUserPublicId` (`PersonnelFileEmployeeHandlerBases.cs:228` — medical claims, «el único write self-service») + lecturas self-service D-13 donde el empleado ve solo sus registros aplicados.
- **Molde de filtros/orden**: bandejas `POST …/query` con `SortBy`/`SortDirection`/`StatusCounts`; groupBy multidimensional verificado en REQ-006 (`OneTimeIncomesReportingController` — 8 dimensiones); endpoints por-registro de los 7 módulos fuente disponibles para RF-004 (p. ej. `GET personnel-files/{id}/overtime-records/{id}`, `…/disciplinary-actions/{id}`, `…/incapacities/{id}`, `…/not-worked-times/{id}`, cuotas/aplicaciones de ingresos y descuentos).

### A.3 Linaje de la capacidad en el programa

| Hito | Qué estableció |
|---|---|
| REQ-005 (2026-07-05) | «Historial de pago» POR REGISTRO (RF-009/D-09: cuotas aplicadas + proyección + saldos) — la primera aparición del término |
| REQ-012 A.1 (2026-07-13, ratificado 07-14) | Cerró el mapa de la visión: «Historial de pago → ✅ REQ-005 + **líneas de corrida**» — los datos del historial POR EMPLEADO quedaron resueltos en M4; la consulta quedó implícita |
| REQ-013 (2026-07-15) | La cara de consulta **corporativa** del motor (bandeja de corridas, revisión, exports Excel/CSV, autorización/cierre) |
| REQ-014 (2026-07-15) | Aplicar/ignorar + rezagos: las decisiones (`IsIncluded`) y advertencias de arrastre que ESTAS consultas muestran por empleado |
| **Este REQ (2026-07-15)** | La cara de consulta **por empleado**: historial trans-corridas (el delta) + verificación del periodo abierto — completa las tres vistas del motor: corporativa (REQ-013), por transacción (REQ-014) y por empleado (REQ-015) |
