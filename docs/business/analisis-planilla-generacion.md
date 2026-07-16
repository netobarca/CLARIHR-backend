# Análisis de negocio — Módulo de planilla: motor de generación (nóminas, periodos, jornadas y corrida de cálculo)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + revalidación de impactos + propuesta) |
| **Módulo** | Planilla — **el motor**: maestro de **tipos de planilla (nóminas)** · **periodos de planilla extendidos** (corte, pago, ventanas de ingreso, estado) · **jornadas laborales** (net-new) · **generación de planilla** (corrida de cálculo con revisión, regeneración, recálculo selectivo, autorización, boletas, conciliación bancaria y programación) |
| **Fecha** | 2026-07-13 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código (HEAD `597aa75`, rama `feature/planilla-descuentos`) |
| **Estado** | **RATIFICADO por el negocio (2026-07-14)** — D-01…D-18 ratificadas y P-01…P-18 respondidas (§17). **Ajustes de la ratificación**: **P-02** el campo «aplicación» del maestro **se ELIMINA** («no lo tenemos claro — removámoslo») · **P-03** «parámetros especiales» **se IGNORAN en F1** (sin ejemplos del negocio) · **P-07** el motor debe soportar **TODOS los tipos** (mensual, quincenal, semanal, por día, por obra); la aritmética exacta por tipo y las tablas de Renta por frecuencia se fijan con los **golden A.3 ampliados — gate del contador antes del PR del motor** · **P-14** la **programación a fecha/hora se DIFIERE a F2** (D-13 diferida; la Ola C queda boletas + impresión + conciliación) |
| **Naturaleza del módulo** | **El pivote de la frontera estratégica.** Es el levantamiento **anunciado y esperado** por toda la cadena: P-01 de REQ-005 ratificó «el motor se realizará aparte»; el mapa A.1 de ese análisis lo declaró «programa futuro con levantamiento propio»; y el análisis de REQ-008…011 cerró con «de la visión original **solo queda pendiente el motor de generación**». Los 11 REQ del backlog construyeron los **insumos**; este REQ construye el **consumidor**: la nómina deja de ser externa y CLARIHR pasa a **calcular** |
| **Documentos hermanos** | [`analisis-planilla-ingresos-ciclicos.md`](analisis-planilla-ingresos-ciclicos.md) (mapa A.1 de la visión + frontera A.4) · [`analisis-planilla-descuentos-y-endeudamiento.md`](analisis-planilla-descuentos-y-endeudamiento.md) (los 4 planes espejo + F2+ que este REQ recoge) · [`analisis-plazas-ingresos-egresos.md`](analisis-plazas-ingresos-egresos.md) (la configuración legal que el motor consume; su D-08 declaró este módulo) · [`analisis-vacaciones-incapacidades-empleado.md`](analisis-vacaciones-incapacidades-empleado.md) (maestro de periodos + montos de incapacidad) · [`analisis-horas-extras-empleado.md`](analisis-horas-extras-empleado.md) (jornadas HE que el motor valora por primera vez) · [`analisis-liquidacion-empleado.md`](analisis-liquidacion-empleado.md) (el precedente de motor certificado — proceso y esfuerzo) |
| **Alcance geográfico** | El Salvador (SV) — mismo corte de toda la cadena |

---

## Contexto del cambio (requerimiento original)

El módulo deberá tener las opciones para crear **jornadas laborales**, **tipos de nóminas con sus períodos**, así como las **consultas** necesarias para revisar la información de las planillas. Para la generación y su procesamiento se define el flujo:

> **a)** Debe existir: periodos de planilla (la generación puede ser automática, el estado debe ser «generado»), tipos de planilla, jornadas laborales · **b)** Ingreso de acciones que implican cálculo: descuentos, ingresos, permisos, horas extras, bonos, amonestaciones, tiempos no trabajados, etc. · **c)** Generación de planilla · **d)** Revisión: modificaciones/ajustes y **generar nuevamente** · **e)** Autorización · **f)** Envío: generación/envío/impresión de **boletas de pago**, impresión de planilla y **conciliación bancaria** para autorización.

Los cuatro sub-requerimientos detallados:

1. **Período de Planillas** — código, nómina para la que se crea el periodo, frecuencia, fecha inicial, fecha final, fecha de corte, fecha de pago, mes y año, permite horas extras (sí/no), periodo para ingresar horas extras, permite asistencia (sí/no), periodo para ingresar asistencia, estado de la planilla.
2. **Tipos de Planillas** — código, descripción, tipo (catálogo), aplicación (lista desplegable), periodo, total periodos, garantizar ingreso mínimo después de descuentos (sí/no), moneda, rango especial para fecha final de ingreso de horas extras (sí/no) + rango (lista) + días de desplazamiento, rango especial para asistencia (sí/no) + rango (lista), parámetros especiales.
3. **Jornada** — código, descripción, horario, fecha de la asistencia tomada de hora de salida o de entrada, total de horas; detalle por día: hora inicio, hora fin, hora de comida.
4. **Generación de planilla** — formulación para todos los empleados o específicos; validación de insumos previos; selección de la planilla + opción de generar (cálculos + registro de la acción); **programación a fecha/hora**; **recálculo de uno o varios empleados** sobre una planilla ya generada.

El levantamiento pide además **revalidar dónde impactan estos requerimientos y las fases que quedaron pendientes en espera de esta solicitud** → §R (Revalidación de impactos).

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **Este ES el levantamiento que todo el programa esperaba — y la costura está preparada.** La frontera «la nómina se procesa fuera de CLARIHR» se ratificó en cadena (D-08 de plazas, RN-14 de REQ-003, G-06 de REQ-002, P-01 de REQ-005) **siempre con la salvedad de que el motor vendría con levantamiento propio**. La prueba de que se esperaba: **los 5 módulos de pools** (ingresos cíclicos/eventuales, descuentos cíclicos/eventuales, horas extras) tienen el **origen de aplicación `MOTOR` sembrado en el dominio** desde su construcción (`PersonnelFileRecurringIncome.cs:42`, `PersonnelFileRecurringDeduction.cs:46`, `PersonnelFileOneTimeIncome.cs:34`, `PersonnelFileOneTimeDeduction.cs:33`, `PersonnelFileOvertimeRecord.cs:42`) — hoy nadie lo usa; es la costura reservada para este motor. Construir aquí NO es refactor: es **encajar el consumidor en interfaces ya diseñadas para él**.
2. **Los cuatro sub-requerimientos tienen coberturas muy distintas.** (a) **Periodos**: PARCIAL — `PayrollPeriodDefinition` (REQ-001) existe con 6 consumidores por FK real, pero cuelga de la **frecuencia** (`pay-periods`), no de una **nómina**, y le faltan 8 campos (código, nómina, corte, pago, mes, ventanas HE/asistencia, estado de flujo) → se **EXTIENDE**, no se duplica (D-03). (b) **Tipos de planilla**: PARCIAL-mínimo — `payroll-types` (`-9890…-9895`) es un catálogo plano code/name; el levantamiento pide un **maestro por empresa** con ~12 campos → entidad nueva «Nómina» cuyo campo «tipo» ES el catálogo existente (D-02). (c) **Jornada**: NET-NEW total — verificado dos veces (G-11 de REQ-011 y este análisis): no hay entidad de jornada; la plaza lleva `WorkdayCode` **texto libre** (`PersonnelFileEmployee.cs:187`) — la costura natural (D-06). (d) **Generación**: NET-NEW — el motor; el único calculador existente (liquidación) NO es reutilizable (№5).
3. **El único motor existente es el de liquidación y NO debe extenderse a planilla** (`SettlementCalculationRules`, certificado con golden del contador): es per-retiro/per-plaza, one-shot, con reglas de finiquito; su base salarial es **CRUDA** (№4) y su `ResolveClass` con default→`Ingreso` (`SettlementCalculation.Rules.cs:398-406`) es trampa conocida. Lo que SÍ se hereda de él: el **proceso** (motor puro determinista + golden cases del contador como gate + checklist pre-despliegue), el **molde de revisión** (líneas con `OverrideAmount` que sobreviven el recálculo; la regeneración limpia por diseño), sus **fuentes de configuración legal** (№6) y la **boleta por `DocumentModel`** (№9).
4. **Hallazgo estructural que este motor DEBE cerrar: la mensualización no existe.** `SettlementRepository.GetCalculationContextAsync:121-126` toma el `Value` del concepto `IsBaseSalary` **crudo, sin mirar `PayPeriodCode`** — documentado explícitamente en `PersonnelFileEmployeeRepository.cs:3875-3877` («feeds the CERTIFIED settlement engine… this REQ derives its own»). REQ-010, tiempos no trabajados e incapacidades derivaron cada uno su propia base. El motor de planilla debe **proratizar por frecuencia del periodo** (mensual/quincenal/semanal) con regla ratificada por el contador (P-07) — y NO tocar la del finiquito.
5. **Los insumos del paso b) del flujo YA están construidos y certificados — todos.** Descuentos (REQ-008/009), ingresos (REQ-005/006), horas extras (REQ-007 — jornadas × factor, hoy SIN montos: el motor las **valora por primera vez**), amonestaciones con concepto de egreso (REQ-003), tiempos no trabajados con descuento calculado (REQ-011), incapacidades con montos por tramos (REQ-001). Cada uno con estados, autorización, aplicación por periodo e insumo exportable. La generación es, en esencia, **la corrida que junta lo que ya está registrado** (§R detalla módulo por módulo).
6. **La configuración legal del cálculo ya existe completa**: tasas ISSS/AFP con tope por catálogo país + override por instancia (`SettlementRepository.ResolveSchemeAsync:739-755`), maestro AFP con parámetros de pensión, **tramos de Renta** (`IncomeTaxWithholdingBracket`, hoy solo `PayPeriodCode='MENSUAL'` — la derivación quincenal/semanal es pregunta del contador, P-07), salario base por concepto `IsBaseSalary`, tabulador 3 niveles (`SalaryTabulatorLine`), **salario mínimo en la ficha** (`PersonnelFileEmployeeProfile.MinimumMonthlyWage` — insumo directo del «ingreso mínimo garantizado» del maestro), asuetos y día de descanso con cadena de fallback.
7. **«Estado debe ser generado» + «la generación puede ser automática» tienen molde exacto**: la generación **masiva idempotente** de periodos ya se construyó para el fondo de vacaciones (`POST /companies/{id}/vacation-periods/generate`, REQ-001 PR-7) y `RecurringDeductionFrequencies.PeriodsPerYear` (12/24/52/1) ya deriva cuántos periodos por año corresponden a una frecuencia. Generar el calendario anual de una nómina es ensamblaje (D-04).
8. **La programación a fecha/hora es net-new PERO con chasis**: no hay cron/Quartz/Hangfire; sí hay el precedente de **cola de jobs en BD + worker de polling** (`ReportExportJobBackgroundService` con claim/retry/expire ya resueltos). «Programar la generación» = fila de job con `scheduledForUtc` que el worker toma al vencer (precisión de minutos) (D-13). **Ratificación: DIFERIDA a F2 (P-14).**
9. **El paso f) «envío» tiene 2 de 3 piezas**: boleta PDF por empleado = molde completo (`SettlementDocumentMapper` → `DocumentModel` → QuestPDF/Gotenberg configurable); conciliación bancaria = las **cuentas bancarias** (`PersonnelFileBankAccount` con banco/tipo/número/primaria) y la **forma de pago por plaza** (`PaymentMethodCode`, catálogo con `BOLETA` sembrado) ya existen — el reporte agrupa neto por forma de pago/banco/cuenta. Lo que NO existe: **correo saliente real** (solo stubs `LoggingEmailService`) → envío por email = F2 (P-12).
10. **Qué NO toca este módulo**: el motor de liquidación (certificado — coexistencia definida en RN, no modificación), el ledger externo `PersonnelFilePayrollTransaction` (cuasi-inmutable, la liquidación es explícita: «FA-1: no payroll write», `Settlements.Handlers.cs:1009`; su futuro se decide en P-16), y los catálogos país compartidos (payroll-types, pay-periods, currencies se **consumen**).
11. **La asistencia sigue sin existir y este levantamiento NO la construye** — pero sus campos la anticipan: «permite asistencia / periodo para ingresar asistencia» se construyen como **configuración y gate** (ventanas del periodo), dejando la costura para el futuro módulo de marcación (G-12 de REQ-011: «biometría se desarrollará a futuro»). Igual que REQ-011 dejó la costura `origin MANUAL/MARCACION`.
12. **Riesgo mayor y su mitigación heredada**: el motor convierte a CLARIHR en responsable de cifras legales de pago recurrente. El precedente es exacto — el motor de liquidación exigió **casos dorados del contador antes de construir** y sign-off en el checklist de despliegue (mismo trato que la amortización de REQ-008 y el séptimo de REQ-011: «si el contador discrepa se corrige el cálculo, no el modelo»). Este análisis propone el Anexo A.3 como el paquete de golden cases a validar ANTES del plan técnico (D-08).

### Trazabilidad campo a campo del levantamiento

#### 1) Período de Planillas (extensión de `PayrollPeriodDefinition` — D-03)

| Campo pedido | ¿Existe hoy? | Cobertura propuesta |
|---|---|---|
| Código | NO (hay `Number` int + `Label`) | Campo nuevo `Code` (único por nómina+año) |
| Nómina para la que se crea | **NO** (hoy cuelga de la frecuencia `pay-periods`) | FK nueva al maestro «Nómina» (D-02); coherencia con el par `payrollTypeCode` de los pools |
| Frecuencia | SÍ — `PayPeriodTypeCode` validado contra `pay-periods` (`PayrollPeriods.Handlers.cs:465-471`) | Se conserva; default heredado de la nómina |
| Fecha inicial / final | SÍ — `StartDate`/`EndDate` + CHECK + no-solape | Se conservan |
| Fecha de corte | NO | Campo nuevo `CutoffDate` (≤ `EndDate`) |
| Fecha de pago | NO | Campo nuevo `PaymentDate` |
| Mes y año | PARCIAL — `Year` sí; mes no | Campo nuevo `Month` (1-12, mes contable de imputación — P-04) |
| Permite horas extras (sí/no) | NO | Flag nuevo + gate de registro de jornadas HE (D-05) |
| Periodo para ingresar horas extras | NO (REQ-007 imputa a periodo pero **sin ventana de ingreso**) | Ventana nueva `OvertimeEntryStart/End` — materializada desde la regla del maestro (rango + desplazamiento), editable (P-18) |
| Permite asistencia + periodo para ingresarla | NO (no existe asistencia) | Flags/ventana como **configuración anticipada** (№11); el módulo de marcación sigue futuro |
| Estado de la planilla | NO (solo `IsActive`) | Estado de flujo nuevo del periodo: `GENERADO → CERRADO` (+`ANULADO`); el detalle fino vive en la corrida (D-04/D-07) |

#### 2) Tipos de Planillas (maestro nuevo «Nómina» — D-02)

| Campo pedido | Cobertura propuesta |
|---|---|
| Código / descripción | Maestro por empresa (`Code` único normalizado + `Name`), molde governed `CostCenter` (sin DELETE físico) |
| Tipo (catálogo) | **Reutiliza `payroll-types`** (`-9890…-9895` MENSUAL/QUINCENAL/SEMANAL/POR_DIA/POR_OBRA/OTRO) — el catálogo que ya referencian plaza y los 5 pools |
| Aplicación (lista desplegable) | **ELIMINADO en ratificación (P-02: «no lo tenemos claro — removámoslo»)** — no se construye |
| Periodo | Frecuencia default de sus periodos — catálogo `pay-periods` |
| Total periodos | Int (12/24/52…) validado contra `PeriodsPerYear` de la frecuencia; alimenta la generación automática del calendario (D-04) |
| Garantizar ingreso mínimo tras descuentos (sí/no) | Flag + regla del motor sobre `MinimumMonthlyWage` de la ficha (P-08 define la regla exacta con el contador) |
| Moneda | Catálogo `currencies` (hoy solo USD `-9370`; default `CompanyPreference.CurrencyCode`) |
| Rango especial fecha final ingreso HE (sí/no) + rango (lista) + días de desplazamiento | Regla de ventana en el maestro (fuente del rango + desplazamiento en días) que **materializa** la ventana concreta en cada periodo (P-18 fija la semántica de la lista) |
| Rango especial asistencia (sí/no) + rango (lista) | Ídem, como configuración anticipada (№11) |
| Parámetros especiales | **IGNORADO en F1 (P-03: «no se tiene claro — ignorar»)** — se levanta en F2 si aparecen ejemplos reales |

#### 3) Jornada (maestro nuevo — D-06)

| Campo pedido | Cobertura propuesta |
|---|---|
| Código / descripción | Maestro por empresa `WorkSchedule` (molde governed sin DELETE) |
| Horario | Etiqueta legible (p.ej. «L-V 08:00–17:00») — derivable del detalle |
| Fecha de asistencia tomada de hora de salida o de entrada | Enum `ENTRADA`/`SALIDA` (ancla para turnos que cruzan medianoche) |
| Total de horas | Horas semanales (derivadas del detalle; editable como referencia) |
| Detalle por día: hora inicio, hora fin, hora de comida | Hija `WorkScheduleDay` (día 0-6, inicio, fin, comida inicio/fin o minutos, horas netas derivadas) |
| Ordinaria / extraordinaria | Flag de clase de jornada («que se pueden aplicar en un período») |
| — (costura) | La plaza YA lleva `WorkdayCode` texto libre → pasa a validarse contra este maestro, con limpieza destructiva de datos de prueba (P-17; precedente: migración destructiva de `payroll_type_code` en REQ-004) |

#### 4) Generación de planilla (corrida — D-07…D-10)

| Capacidad pedida | Cobertura propuesta |
|---|---|
| Formulación para todos o empleados específicos | `POST …/payroll-runs` con población = plazas activas de la nómina (P-06) + subset opcional `employeeIds` |
| Validar insumos previos (periodos, ingresos/descuentos, movimientos, incapacidades, cambios de jornada, incrementos, HE, bonos…) | **Pre-flight** de advertencias (RF-008) + el motor consume lo REGISTRADO/AUTORIZADO/VIGENTE de cada módulo (§R) — no puede «validar que no falte nada» (eso es operativo), pero sí reportar huecos duros (sin salario base, sin tipo de planilla, sin tramos Renta…) |
| Seleccionar la planilla de un listado + opción generar | Bandeja de corridas + acción generar (una corrida activa por nómina+periodo — índice único) |
| Realizar cálculos y registrar la acción | Motor puro `PayrollCalculationRules` (D-08) + líneas persistidas + auditoría (D-14) |
| Programarse a fecha/hora | **DIFERIDO A F2 (P-14 ratificada)** — diseño reservado: cola de jobs con `scheduledForUtc` (molde `ReportExportJob`, D-13) |
| Recalcular uno o varios empleados de una generada | RF-013 — recálculo selectivo que respeta overrides (molde liquidación) |
| Revisión, ajustes y regenerar | RF-011/RF-012 — overrides por línea + regeneración que limpia y reconstruye |
| Autorización | RF-015 — una decisión, anti-self doble, `Authorize*` sin Admin (D-11) |
| Boletas + impresión + conciliación bancaria | RF-017…RF-019 (D-12) |

---

## R. Revalidación de impactos — dónde pega este levantamiento y qué fases estaban en espera

Pedido explícito del levantamiento. Dos vistas: impacto módulo a módulo, y el registro de fases diferidas que este REQ recoge (o sigue difiriendo).

### R.1 Impacto módulo por módulo (los 11 REQ + 4 módulos pre-backlog)

| Módulo | Qué dejó preparado para este motor | Impacto de este REQ |
|---|---|---|
| **Plazas ingresos/egresos** (pre-backlog) | TODA la configuración legal: conceptos país con `IsBaseSalary`/tasas ISSS-AFP/tope, Renta por tramos, salario 3 niveles, `PayrollTypeCode` en la plaza | Su **D-08 se cumple** («el cálculo es módulo futuro de nómina» — es este). El motor consume; NO se cambia el modelo de configuración |
| **Liquidación** (PR #56) | El precedente de motor certificado + boleta `DocumentModel` + molde de revisión (override/regeneración) | **NO SE TOCA.** Coexistencia por reglas: empleado con finiquito EMITIDO en el periodo queda fuera de la corrida (el finiquito ya pagó sus días — P-11); los pools cerrados con origen `LIQUIDACION` no entran al motor |
| **REQ-001 vacaciones/incapacidades** | `PayrollPeriodDefinition` (se extiende); incapacidades con montos snapshot por tramos (`SubsidyAmount`/`DiscountAmount`/`EmployerAmount` + `TrancheDetailJson`) y bandeja default `REGISTRADA` = insumo | Periodos ganan 8 campos (D-03) — **los 6 consumidores FK quedan intactos**; el motor lee incapacidades REGISTRADA del periodo como líneas (P-10). Vacaciones siguen sin línea de planilla (goce no altera el pago mensual; proporcional paga en finiquito) — confirmar en P-11 |
| **REQ-002 tiempo compensatorio** | Ledger de horas; G-06 (valor de horas fuera) | El ledger **sigue sin insumo de planilla** (solo liquidación, concepto `-9837`). Sin cambios F1 (P-11) |
| **REQ-003 otras transacciones** | Amonestaciones con **concepto de egreso snapshot al aplicar** + insumo (`InsumoPlanillaExportRow`); RN-14 «no se escriben ledgers de planilla» | El motor toma amonestaciones aplicadas del periodo como línea de descuento. **RN-14 queda SUPERADA para la corrida**: el motor escribe **sus propias líneas** (`PayrollRunLine`), nunca el ledger externo — la prohibición de escribir `PersonnelFilePayrollTransaction` se mantiene (P-16) |
| **REQ-004 tablero acciones** | Catálogo `payroll-types` sembrado + limpieza destructiva de `payroll_type_code` + dashboard `byPayrollType` | El catálogo pasa a ser el campo «tipo» del maestro Nómina. Dashboard y filtros intactos (el código no cambia de forma) |
| **REQ-005 ingresos cíclicos** | **El anuncio**: P-01 «el motor se realizará aparte» + mapa A.1; cuotas con origen `MOTOR` reservado; D-09 «cuando llegue el motor aplica las cuotas… sobre el mismo modelo — el contrato del historial no cambia»; insumo por periodo | **Promesa cobrada tal cual**: la corrida aplica cuotas vencidas del periodo con origen `MOTOR` (D-09/P-09); la aplicación manual y el insumo siguen existiendo (operación de borde + verificación de cuadre) |
| **REQ-006 ingresos eventuales** | Aplicación única por ingreso con origen `MOTOR` reservado + insumo | Ídem: el motor imputa los AUTORIZADOS del periodo (D-09) |
| **REQ-007 horas extras** | Jornadas × factor **SIN montos** («el valor lo pone la nómina externa»); par tipo+periodo FK; aplicación origen `MOTOR`; insumo horas×factor | **Doble impacto**: (1) el motor VALORA por primera vez (Σ horas×factor × valor-hora del periodo — la fórmula ratificada en su RF-014 aplicada ahora a planilla); (2) los campos «permite HE / ventana de ingreso» del periodo agregan el **gate de registro** que REQ-007 no tenía (D-05) — hoy solo hay sanity-cap de 366 días |
| **REQ-008 descuentos cíclicos** | Plan por segmentos O interés compuesto + extraordinarias + `OutstandingBalance()`; cuotas origen `MOTOR`; insumo | La corrida aplica la cuota del periodo (el monto lo deriva el propio módulo — segmentos o amortización); el motor NO recalcula amortización (golden del contador de REQ-008 sigue vigente) |
| **REQ-009 descuentos eventuales** | Aplicación + insumo; origen `MOTOR` | Ídem D-09 |
| **REQ-010 endeudamiento** | Advertir-nunca-bloquear al crear/autorizar; **hallazgo: mensualización no existe** | El motor **cierra el hueco estructural**: proratiza por `PayPeriodCode` (P-07). El chequeo de endeudamiento NO se re-ejecuta en la corrida (ya se hizo al autorizar cada descuento); el mínimo garantizado del maestro es una regla distinta y nueva (P-08) |
| **REQ-011 tiempos no trabajados** | Descuento **calculado** (REGISTRADO, monto>0) + insumo con exclusiones; séptimo resuelto; G-11 «sin jornada real» + G-12 «sin marcación» | El motor toma los REGISTRADOS del periodo como líneas de descuento. **G-11 se cierra** (nace el maestro de jornadas; `LLEGADA_TARDIA` con `UsesWorkSchedule` podrá refinarse contra horario real = F2 de ese módulo). G-12 sigue abierto (asistencia = módulo futuro; aquí solo configuración) |
| **Formas de pago / cuentas bancarias** (pre-backlog) | `PaymentMethodCode` por plaza (BOLETA sembrado) + `PersonnelFileBankAccount` (banco/tipo/número/primaria) | Insumo directo de la **conciliación bancaria** (RF-019) |
| **Transacciones fuera de nómina** (pre-backlog) | Movimiento monetario NO-planilla | Sin impacto (fuera de nómina por definición); NO entra al motor |

### R.2 Registro de fases diferidas «en espera de esta solicitud»

| Fase diferida (dónde quedó anotada) | ¿La recoge este REQ? |
|---|---|
| **Motor de generación de planilla** (REQ-005 P-01/A.1 · REQ-008 F2+ «solo queda pendiente el motor» · plazas D-08) | ✅ **SÍ — es el núcleo de este levantamiento** |
| **Pool pleno de transacciones pendientes + semántica de «periodo activo»** (REQ-005 A.1: «pool pleno = motor») | ✅ SÍ — la corrida del periodo ES el pool aplicándose; las bandejas de pendientes existentes pasan de «lista de trabajo manual» a «lo que la corrida va a tomar» |
| **Aplicar al periodo activo / enviar a otro periodo** (REQ-005 A.1) | ✅ SÍ — generar aplica; excluir/re-imputar (mecanismos ya construidos) = enviar a otro periodo |
| **Jornada real por plaza** (REQ-008 F2+ · REQ-011 G-11) | ✅ SÍ — sub-requerimiento 3 |
| **Valoración monetaria de horas extras en operación regular** (REQ-007: «SIN montos; el valor lo pone la nómina externa») | ✅ SÍ — el motor valora |
| **Mensualización/proratización de la base salarial** (hallazgo REQ-010) | ✅ SÍ — regla nueva del motor (P-07) |
| **Correlación con el ledger externo `PersonnelFilePayrollTransaction`** (REQ-005 P-09/A.1 · REQ-008 F2+) | ⚠️ PARCIAL — con motor interno la pregunta cambia: ¿se congela/retira el ledger externo? → **P-16** (recomendación: F1 no postea ni correlaciona; decidir destino del módulo) |
| **Autorización multi-nivel** (REQ-005 P-12 · REQ-008 F2+) | ❌ NO — sigue F2 (D-11: una decisión en F1) |
| **Notificaciones** (REQ-008 F2+) | ❌ NO — sigue F2 (sin proveedor real de correo — №9) |
| **Módulo de marcación biométrica / asistencia** (REQ-011 G-12/P-21) | ❌ NO — este REQ solo construye la **configuración** (ventanas del periodo); la marcación exige su propio levantamiento |
| **Solicitudes de permiso del empleado** (REQ-011 F2) | ❌ NO — ajeno a este levantamiento |

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza o extiende

| Pieza | Dónde | Estado |
|---|---|---|
| Maestro de periodos | `PayrollPeriodDefinition` (`Domain/Leave/PayrollPeriodDefinition.cs`): frecuencia (`pay-periods`) + año + número + label + rango + activo; governed bajo `LeaveConfiguration.Read/Manage`; unicidad (tipo, año, número) + no-solape | **Se extiende** (D-03) |
| Consumidores del periodo | FK real `payroll_period_id` + label snapshot: cuotas cíclicas (ingresos `PersonnelFileRecurringIncome.cs:819`, descuentos `:1271`), eventuales (ingresos `:804`, descuentos `:762`), horas extras (`:202/:857`); incapacidades con navegación + filtro activo (`PersonnelFileIncapacityRepository.cs:41-44`) | Intactos |
| Catálogo tipos de planilla | `PayrollTypeCatalogItem` `-9890…-9895` (`GlobalCatalogSeedData.cs:677-685`), wire `payroll-types`, code/name/sort/active; validación de activo `PersonnelFileRepository.cs:1664`; en plaza + 5 pools + dashboard | Pasa a ser el «tipo» del maestro Nómina |
| Catálogo frecuencias | `pay-periods` `-9740…-9743` (MENSUAL/QUINCENAL/SEMANAL/UNICA) + `PeriodsPerYear` 12/24/52/1 (`PersonnelFileRecurringDeduction.cs:71-100`) | Se consume |
| Moneda | `currencies` (solo USD `-9370`) + `CompanyPreference.CurrencyCode` + snapshots de 3 chars por módulo | Se consume |
| `WorkdayCode` en plaza | Texto libre max 80 sin FK (`PersonnelFileEmployee.cs:187`, col `workday_code`) | Costura de jornadas (D-06) |
| Horas/día estándar | `CompanyPreference.CompensatoryTimeStandardDailyHours` (null→8; `CompanyPreference.cs:99`) — única «horas por día» del sistema | La jornada la refina (cadena jornada→preferencia→8) |
| Día de descanso | Cadena plaza referida→principal→preferencia→domingo (`LeaveCalculationDataProvider.cs:154-159`) | Se consume |
| Asuetos | `CompanyHoliday` por tenant+fecha con scope | Se consume |
| Config legal | ISSS/AFP: catálogo país + override instancia (`SettlementRepository.cs:156-167`, `ResolveSchemeAsync:739-755`) · Renta: `IncomeTaxWithholdingBracket` (MENSUAL, vigencias) · mínimo: `PersonnelFileEmployeeProfile.MinimumMonthlyWage` · salario: concepto `IsBaseSalary` + tabulador `SalaryTabulatorLine` | Se consume (proratización nueva — P-07) |
| Pools con origen `MOTOR` | Los 5 módulos (§0.1) + apply-period + `payroll-input/export` cada uno | La corrida los aplica (D-09) |
| Insumos sin pool | Incapacidades (montos snapshot, REGISTRADA) · tiempos no trabajados (REGISTRADO, monto>0) · amonestaciones aplicadas con egreso | La corrida los lee (P-10) |
| Boleta PDF | Seam `DocumentModel` → QuestPDF (default, License Community) / Gotenberg (`DocumentPdfRenderingRegistration.cs:27-77`); molde `SettlementDocumentMapper` | Molde de la boleta de pago |
| Jobs en background | `ReportExportJobBackgroundService` (cola BD + polling, claim/retry/expire) + 2 más; **sin cron** | Molde de la programación (D-13) |
| Correo | Solo stubs (`LoggingEmailService`, `LoggingAuthEmailService`) | Envío por email = F2 |
| Ledger externo | `PersonnelFilePayrollTransaction` (`PersonnelFileEmployee.cs:673-763`): cuasi-inmutable (PATCH solo `isActive`), marca `SourceSystem/SourceReference/SourceSyncedUtc`; nadie más lo escribe | NO se toca en F1 (P-16) |

### Lo que NO existe (verificado exhaustivamente)

- **Maestro de nóminas/tipos de planilla** con los campos del levantamiento (solo el catálogo plano).
- **Campos de corte/pago/mes/ventanas/estado** en el periodo (verificado: `CutoffDate` solo existe como parámetro runtime de bandejas, no como campo del maestro).
- **Entidad de jornada** (0 hits `WorkSchedule/Shift/Jornada` en dominio; comentario explícito `NotWorkedTimeType.cs:67`).
- **Asistencia/marcación** (0 entidades, 0 DbSets).
- **Motor de planilla**: cero entidades de corrida/líneas; la liquidación no escribe planilla («FA-1: no payroll write»).
- **Proratización de base salarial** por frecuencia (hallazgo №4).
- **Tramos de Renta no-mensuales** (solo `MENSUAL`).
- **Scheduler a fecha/hora** y **correo saliente real**.
- **Ventana de ingreso de horas extras** (REQ-007 registra sin gate de ventana).

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha | Propuesta |
|---|---|---|
| G-01 | La visión pide **generar la planilla** y no existe motor (frontera ratificada en cadena, siempre con la salvedad del levantamiento propio) | **Este levantamiento ES esa salvedad**: motor puro nuevo `PayrollCalculationRules` + agregado `PayrollRun` (D-07/D-08); proceso heredado del motor de liquidación (golden del contador como gate) |
| G-02 | Los periodos cuelgan de la **frecuencia**, no de una **nómina**; faltan corte/pago/mes/ventanas/estado | Extender `PayrollPeriodDefinition` (D-03) — sin romper a los 6 consumidores FK |
| G-03 | «Tipo de planilla» es catálogo plano; el levantamiento pide un maestro rico | Maestro «Nómina» por empresa (D-02); el catálogo existente es su campo «tipo» |
| G-04 | Sin jornada real (plaza con `WorkdayCode` libre); `LLEGADA_TARDIA` de REQ-011 usa horas estándar | Maestro `WorkSchedule` + detalle por día (D-06); plaza valida contra el maestro (P-17); refinamiento de REQ-011 = F2 de ese módulo |
| G-05 | La base salarial no se mensualiza en ninguna fuente compartida (№4) | Regla de proratización del motor por `PayPeriodCode` del periodo, ratificada por el contador (P-07); la del finiquito NO se toca |
| G-06 | Renta solo tiene tramos `MENSUAL` | P-07 (contador): derivación aritmética (tabla mensual ÷ factor) o tablas propias por frecuencia (el modelo `IncomeTaxWithholdingBracket` ya soporta `PayPeriodCode` distinto — sería sembrar/cargar más filas) |
| G-07 | Horas extras sin valoración monetaria en operación regular | El motor valora: Σ(horas×factor) × valor-hora del periodo (fórmula RF-014 de REQ-007 con la base proratizada del periodo) |
| G-08 | No hay gate de ventana para ingreso de HE (ni de asistencia) | Flags+ventanas en el periodo (D-05), materializadas desde la regla del maestro (P-18); el gate se aplica al CREAR jornadas HE |
| G-09 | «Ingreso mínimo garantizado tras descuentos» no existe como regla | Regla nueva del motor sobre `MinimumMonthlyWage` proratizado: qué descuentos se difieren y en qué orden = P-08 (contador); las cuotas diferidas quedan pendientes en su pool (no se pierden) |
| G-10 | Programación a fecha/hora inexistente | Cola de jobs con `scheduledForUtc` (molde `ReportExportJob`) — D-13/P-14 |
| G-11 | Boleta/conciliación: sin reporte de planilla ni agrupación bancaria | Boleta por `DocumentModel` (molde liquidación) + reporte de planilla + conciliación por forma de pago/banco/cuenta (RF-017…019); formato de archivo bancario = P-13 |
| G-12 | Correo saliente = stubs | Envío por email de boletas = F2 (P-12); F1 = descarga/impresión en lote |
| G-13 | Doble beneficio potencial motor↔liquidación (empleado liquidado en el periodo; pools cerrados por finiquito) | Reglas de coexistencia (RN-13/RN-14): finiquito EMITIDO en el periodo → fuera de la corrida (P-11); pools con origen `LIQUIDACION` → nunca los toma el motor |

---

## Decisiones de diseño — D-01…D-18 (**RATIFICADAS por el negocio, 2026-07-14**; ajustadas por la ratificación: D-01 y D-13 por P-14 · D-02 por P-02/P-03 — ver §17)

| # | Tema | Decisión propuesta |
|---|---|---|
| D-01 | Fases | **(RATIFICADA con ajuste P-14)** **F1, en 3 olas**: Ola A configuración (maestro Nómina + periodos extendidos + generación automática de calendario + jornadas) · Ola B motor (corrida, revisión/regeneración/recalc selectivo, autorización, consumo de pools origen `MOTOR`) · Ola C envío (boletas, impresión, conciliación bancaria). **F2**: **programación a fecha/hora (P-14)**, envío por correo, asistencia/marcación, multi-nivel, notificaciones, correlación/retiro del ledger externo, refinamiento `LLEGADA_TARDIA` con jornada real |
| D-02 | Maestro Nómina | **(RATIFICADA con ajustes P-02/P-03)** Entidad nueva por empresa (`PayrollDefinition`), molde governed `CostCenter` (sin DELETE; activate/inactivate; UQ `(tenant, normalized_code)`); campo «tipo» = catálogo país `payroll-types` existente; «total periodos» validado contra `PeriodsPerYear`; flag ingreso mínimo; moneda; reglas de ventana HE/asistencia. **SIN campo «aplicación» (P-02 eliminada) y SIN hijas de «parámetros especiales» (P-03 ignorada en F1)** |
| D-03 | Periodos | **EXTENDER `PayrollPeriodDefinition`** (no crear entidad paralela): + `Code`, + FK a Nómina, + `CutoffDate`, + `PaymentDate`, + `Month`, + flags/ventanas HE-asistencia, + estado de flujo. Los 6 consumidores FK y el controller quedan; el contrato FE crece aditivamente (campos nuevos opcionales al inicio; obligatoriedad se define en el plan). Sin producción → limpieza/backfill destructivo permitido |
| D-04 | Calendario automático | `POST …/payroll-definitions/{id}/periods/generate?year=` — generación **masiva idempotente** del año (molde `vacation-periods/generate`): deriva rangos por frecuencia + total periodos, estado inicial **`GENERADO`**, resumen creados/omitidos. Estados del periodo: `GENERADO → CERRADO` (al cerrar su corrida) + `ANULADO`; el flujo fino vive en la corrida (D-07) |
| D-05 | Ventanas de ingreso | «Permite HE» + ventana del periodo = **gate al CREAR jornadas HE** (REQ-007): fecha de registro dentro de la ventana del periodo destino, si la nómina lo exige. Asistencia: mismos campos como **configuración anticipada** (sin módulo de marcación aún). Fuera de ventana → 422 con código propio |
| D-06 | Jornadas | Maestro `WorkSchedule` + hija `WorkScheduleDay` (por día 0-6: inicio, fin, comida, horas netas); ancla de fecha `ENTRADA/SALIDA`; clase ordinaria/extraordinaria; plantilla opcional (L-V 8h). La plaza mantiene `WorkdayCode` pero **validado contra el maestro** (P-17), con limpieza destructiva de valores libres de prueba |
| D-07 | La corrida | Agregado `PayrollRun` (una **activa** por nómina+periodo — índice único parcial) + `PayrollRunLine` (empleado × plaza × concepto: clase Ingreso/Descuento/PagoPatronal, unidades, base, monto calculado, `OverrideAmount`, incluida, origen, referencia al registro fuente). Estados: `GENERADA → AUTORIZADA → CERRADA` + `ANULADA` + devolución `AUTORIZADA→GENERADA` con motivo. Regenerar (solo GENERADA) limpia y reconstruye; el **recálculo** conserva overrides (molde liquidación `BuildSpecsFromExisting`) |
| D-08 | El motor | Módulo puro nuevo `PayrollCalculationRules` (molde `IncapacityCalculationRules`/`SettlementCalculationRules`): determinista, sin reloj/BD, **golden del contador como gate del PR** (Anexo A.3). Pasos: base proratizada por frecuencia (P-07) → ingresos (salario del periodo + cuotas/eventuales/HE valoradas) → descuentos (ley ISSS/AFP/Renta del periodo + cuotas/eventuales/tiempos no trabajados/amonestaciones/incapacidad) → patronales → mínimo garantizado (P-08) → neto. **`SettlementCalculationRules` NO se toca** |
| D-09 | Consumo de pools | Generar la corrida **aplica** los elementos elegibles del periodo con origen **`MOTOR`** (la costura sembrada en los 5 módulos): cuotas vencidas, eventuales autorizados, jornadas HE autorizadas-transcurridas no compensadas. Regenerar/anular la corrida **revierte simétricamente** (los mutadores de anulación/reapertura ya existen). La aplicación manual y los insumos/exports quedan como operación de borde + **verificación de cuadre** (test dorado: corrida ≡ insumo del mismo filtro) |
| D-10 | Ley en la corrida | ISSS/AFP: esquema país + override por instancia (misma resolución que liquidación, base topada por periodo); Renta: tramos por frecuencia (P-07) con warning si faltan (molde `SETTLEMENT_WARNING_RENTA_BRACKETS_MISSING`); patronales ISSS/AFP/INCAF; redondeo `Round2` una sola vez por línea (lección REQ-011) |
| D-11 | Autorización | Una decisión sobre la corrida: `PATCH …/authorization` con **anti-self doble** (quien generó no autoriza; el autorizador no puede ser empleado incluido con conflicto — definir en plan) + permiso **`AuthorizePayrollRuns` sin Admin** (la exclusión vive en la POLICY — hallazgo REQ-007). Multi-nivel = F2 |
| D-12 | Envío | Boleta por empleado desde las líneas (mapper → `DocumentModel` → seam QuestPDF/Gotenberg — molde `SettlementDocumentMapper`), individual y en lote; **impresión de planilla** (reporte completo por corrida con totales); **conciliación bancaria** = reporte agrupado por forma de pago → banco → cuenta con totales para autorización de Tesorería (formato de archivo bancario = P-13). Correo = F2 (P-12) |
| D-13 | Programación | **DIFERIDA A F2 (ratificación P-14: «aún no — en una siguiente fase»).** Diseño reservado para F2: fila de programación (nómina+periodo+`scheduledForUtc`+solicitante) procesada por un `BackgroundService` de polling (molde exacto `ReportExportJobBackgroundService`: claim/retry/expire); precisión de minutos; corre como el actor que la programó |
| D-14 | Auditoría | **Sin asientos de journal por empleado** (volumen — mismo corte que REQ-007); auditoría por `AuditEventTypes` de corrida (generada/regenerada/recalculada/autorizada/devuelta/cerrada/anulada/programada) + historial embebido en la corrida. El «registro de la acción» pedido por el levantamiento se cumple con esto |
| D-15 | Ledger externo | `PersonnelFilePayrollTransaction` **no se escribe ni se lee** en F1; su destino (congelar/retirar/correlacionar) = P-16. Sin producción, la limpieza es barata — pero es decisión de negocio, no técnica |
| D-16 | Permisos | Familia nueva de configuración **`PayrollConfiguration.Read/Manage`** (maestro Nómina + jornadas; molde `LeaveConfiguration`/`OvertimeConfiguration`) — los **periodos se quedan** bajo `LeaveConfiguration` (controller existente; mover familias rompe FE). Corridas: `ViewPayrollRuns` / `ManagePayrollRuns` / `AuthorizePayrollRuns` (gates fail-closed + policies; molde settlements + `AuthorizeRetirement`) |
| D-17 | Estados y catálogos | Estados híbridos canónico + catálogo país (receta 8 toques, tablas propias — NO TPH): estados de corrida y de periodo. Seeds tentativos bloque **`-9970…-9989`** (Anexo A.2; libre global verificado a -9970+; trampas: `-9490…-9496` y fragmentos GUID en `Designer.cs`) |
| D-18 | Población y multi-plaza | Población de la corrida = empleados con **plaza activa cuyo `PayrollTypeCode` == tipo de la nómina** durante el periodo (P-06 confirma o cambia a FK explícita plaza→nómina) + subset opcional. Líneas **por plaza** (coherente con pools y liquidación); boleta **consolidada por empleado** (P-15). Perfil `RETIRADO` antes del inicio del periodo → fuera; retiro dentro del periodo → P-11 |

---

## 1. Resumen del producto o requerimiento

Construir el **núcleo del módulo de planillas** de CLARIHR: (1) el **maestro de nóminas** (tipos de planilla con moneda, garantía de ingreso mínimo y reglas de ventanas), (2) el **calendario de periodos** por nómina (con corte, pago, ventanas de ingreso de horas extras/asistencia y estado, generable automáticamente), (3) el **maestro de jornadas laborales** (horario por día), y (4) el **motor de generación**: una corrida por nómina+periodo que calcula el pago de todos los empleados (o un subconjunto) tomando el salario proratizado, los pools transaccionales ya construidos (ingresos/descuentos cíclicos y eventuales, horas extras, tiempos no trabajados, incapacidades, amonestaciones) y la ley (ISSS/AFP/Renta/patronales), con **revisión y ajustes, regeneración, recálculo selectivo, autorización, boletas de pago, impresión y conciliación bancaria**. La **programación** de la generación a fecha/hora quedó **diferida a F2** en la ratificación (P-14).

**Problema que resuelve:** hoy CLARIHR registra y autoriza todo lo que afecta el pago, pero el cálculo lo hace una nómina externa alimentada por exports («insumos»). Eso duplica trabajo, introduce transcripciones y deja el neto fuera del sistema. Con el motor, el ciclo completo (configurar → registrar → generar → revisar → autorizar → pagar) vive en CLARIHR.

## 2. Objetivos del negocio

1. **Cerrar el programa de planilla**: entregar la única capacidad pendiente del mapa de la visión (motor de generación) sobre los 11 módulos ya certificados.
2. **Eliminar la dependencia de la nómina externa** y las transcripciones de insumos (los exports pasan de «puente» a «verificación»).
3. **Un solo lugar para el ciclo de pago** con trazabilidad completa: quién registró cada concepto, quién generó, quién ajustó, quién autorizó.
4. **Exactitud legal certificable** (ISSS/AFP/Renta/mínimos) con casos dorados del contador — mismo estándar que el finiquito.
5. **Boletas y conciliación bancaria** como salida operativa inmediata para Tesorería.
6. **Configuración institucional formal**: nóminas, calendarios de periodos y jornadas dejan de ser convenciones implícitas (texto libre) y pasan a maestros gobernados.

## 3. Alcance funcional

### Fase 1 — este análisis (3 olas)

**Ola A — Configuración**
- Maestro **Nómina** (tipos de planilla) por empresa con su catálogo país «tipo», moneda, total de periodos, garantía de ingreso mínimo, reglas de ventanas HE/asistencia y parámetros especiales.
- **Periodos extendidos**: código, nómina, corte, pago, mes, ventanas, estado; generación automática del calendario anual (masiva idempotente); CRUD governed existente conservado.
- Maestro **Jornadas** + detalle por día; vínculo con la plaza (`WorkdayCode` validado).

**Ola B — Motor**
- Corrida por nómina+periodo (todos o subconjunto de empleados) con pre-flight de advertencias.
- Cálculo: base proratizada, pools con origen `MOTOR`, incapacidades/tiempos no trabajados/amonestaciones, ley, patronales, mínimo garantizado, neto.
- Revisión (detalle por empleado/línea, override, incluir/excluir), **regeneración**, **recálculo selectivo** de uno o varios empleados.
- **Autorización** (una decisión, anti-self, sin Admin) y devolución a revisión; cierre.
- Bandeja de corridas + consultas + auditoría.

**Ola C — Envío**
- **Boletas de pago** PDF (individual y lote) desde las líneas.
- **Impresión de planilla** (reporte completo con totales por concepto/centro de costo).
- **Conciliación bancaria** (agrupación por forma de pago/banco/cuenta con totales, para autorización).
- Exports xlsx/csv/json de todo lo anterior.

### Fase 2+ (fuera de este MVP, mapeadas)
- **Programación de la generación a fecha/hora** (P-14 ratificada; diseño reservado en D-13 — cola de jobs molde `ReportExportJob`).
- Envío de boletas por **correo** (exige proveedor real de email), notificaciones.
- **Módulo de asistencia/marcación** (las ventanas del periodo ya quedan listas).
- Autorización **multi-nivel** configurable.
- Correlación/retiro del ledger externo (según P-16).
- Refinamiento de `LLEGADA_TARDIA` (REQ-011) contra jornada real; conciliación planificado-vs-trabajado de HE futuras (F2 de REQ-007).

## 4. Fuera de alcance

- Marcación/asistencia biométrica (solo configuración anticipada).
- **Programación de la generación a fecha/hora** (P-14 — diferida a F2 por el negocio; diseño documentado en D-13).
- Envío de boletas por correo electrónico y notificaciones push (sin proveedor real).
- Autorización multi-nivel y delegaciones.
- Archivos bancarios con formato propietario de cada banco (F1 entrega el reporte de conciliación; formato de archivo = P-13).
- Contabilización (posteo a un libro contable/ERP).
- Retroactividad compleja (recálculo de periodos CERRADOS): F1 permite regenerar/ajustar solo ANTES de cerrar; correcciones posteriores = registro en el periodo siguiente (política a confirmar en ratificación).
- Cambios al motor de **liquidación** y al ledger externo `PersonnelFilePayrollTransaction`.
- Multi-moneda con tipo de cambio (catálogo hoy: solo USD).

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador RRHH / Configurador** | Crea nóminas, calendarios de periodos y jornadas (permisos de configuración) |
| **Analista de planilla** | Registra insumos (módulos existentes), corre el pre-flight, genera, revisa/ajusta, regenera, recalcula, programa |
| **Autorizador de planilla** | Rol distinto del generador; autoriza o devuelve la corrida (`Authorize*` sin Admin) |
| **Tesorería / Finanzas** | Consume conciliación bancaria e impresión de planilla; ejecuta el pago |
| **Empleado** | Recibe su boleta de pago (F1: descarga/impresión por RRHH; portal/correo = F2) |
| **Contador (externo al sistema)** | Valida los casos dorados del motor antes de construir y en el checklist de despliegue |
| **Sistema (worker)** | Ejecuta corridas programadas a su vencimiento |

## 6. Requerimientos funcionales

### Grupo A — Configuración

#### RF-001 - Maestro de nóminas (tipos de planilla)
**Descripción:** CRUD governed por empresa de la entidad Nómina con: código, descripción, tipo (catálogo `payroll-types`), aplicación (P-02), frecuencia (`pay-periods`), total de periodos, garantizar ingreso mínimo (sí/no), moneda, reglas de ventana HE (rango + desplazamiento en días) y asistencia, parámetros especiales (clave-valor).
**Reglas de negocio:** código único normalizado por empresa; tipo/frecuencia/moneda activos en catálogo; total periodos coherente con la frecuencia (`PeriodsPerYear`); sin DELETE físico (inactivación con guard de uso: nómina con periodos/corridas activas no se inactiva).
**Criterios de aceptación:** alta/edición/consulta/activación-inactivación con If-Match; 422 bilingüe por catálogo inválido o incoherencia; guard de uso probado.
**Prioridad:** Alta. **Dependencias:** catálogos `payroll-types`/`pay-periods`/`currencies` (existen).

#### RF-002 - Periodos de planilla extendidos
**Descripción:** El maestro de periodos gana: código, nómina (FK), fecha de corte, fecha de pago, mes, permite HE + ventana, permite asistencia + ventana, estado (`GENERADO/CERRADO/ANULADO`). El CRUD governed existente se conserva.
**Reglas de negocio:** corte dentro del rango; pago ≥ inicio; mes/año coherentes con el rango (P-04); unicidad y no-solape ahora **por nómina**; ventanas materializadas desde la regla de la nómina, editables por periodo; los 6 consumidores FK existentes no cambian de contrato.
**Criterios de aceptación:** periodos existentes siguen funcionando (suites de REQ-005…009 y 011 verdes sin editar); campos nuevos en respuesta; validaciones 422.
**Prioridad:** Alta. **Dependencias:** RF-001.

#### RF-003 - Generación automática del calendario de periodos
**Descripción:** Acción masiva idempotente que genera los periodos del año de una nómina (frecuencia + total periodos → rangos), con estado inicial `GENERADO` y resumen creados/omitidos/errores.
**Reglas de negocio:** idempotente (re-corrida no duplica — molde `vacation-periods/generate`); fechas de corte/pago derivadas por regla configurable (offset en días, editable después); no pisa periodos editados.
**Criterios de aceptación:** 2.ª corrida = 0 creados; año bisiesto correcto; quincenas 24/año exactas.
**Prioridad:** Alta. **Dependencias:** RF-001/RF-002.

#### RF-004 - Maestro de jornadas laborales
**Descripción:** CRUD governed por empresa de jornadas (código, descripción, horario-etiqueta, ancla de fecha de asistencia ENTRADA/SALIDA, total de horas, clase ordinaria/extraordinaria) con detalle por día (inicio, fin, comida, horas netas).
**Reglas de negocio:** al menos un día configurado; fin > inicio (con soporte de cruce de medianoche vía ancla); comida contenida en el turno; total de horas = Σ derivada (editable como referencia); sin DELETE físico (guard de uso por plazas vinculadas).
**Criterios de aceptación:** round-trip con detalle por día; validaciones de contención/solape 422; jornada usada no se inactiva.
**Prioridad:** Alta. **Dependencias:** ninguna dura.

#### RF-005 - Vínculo plaza→jornada
**Descripción:** `WorkdayCode` de la plaza pasa de texto libre a código validado contra el maestro de jornadas activo.
**Reglas de negocio:** validar-por-código con snapshot (patrón de la casa); limpieza destructiva de valores libres existentes (datos de prueba — P-17); campo opcional (plaza sin jornada = horas estándar de la preferencia).
**Criterios de aceptación:** alta/edición de plaza con jornada inválida → 422; migración de limpieza ejecutada; openapi actualizado.
**Prioridad:** Media. **Dependencias:** RF-004.

### Grupo B — Generación y revisión

#### RF-006 - Gate de ventanas de ingreso (horas extras)
**Descripción:** Si la nómina/periodo lo exige, el registro de jornadas HE imputadas a un periodo debe caer dentro de su ventana de ingreso.
**Reglas de negocio:** fuera de ventana → 422 código propio; periodo sin ventana o nómina sin regla → sin gate (comportamiento actual); asistencia queda como configuración (sin gate funcional hasta el módulo de marcación).
**Criterios de aceptación:** crear HE dentro/fuera de ventana probado; REQ-007 sin regresión cuando no hay ventana.
**Prioridad:** Media. **Dependencias:** RF-002; REQ-007 (construido).

#### RF-007 - Pre-flight de generación
**Descripción:** Consulta que reporta, ANTES de generar: empleados sin salario base, plazas sin tipo de planilla/centro de costo, tramos Renta faltantes para la frecuencia, mínimo salarial sin configurar, pools con elementos vencidos de periodos anteriores, incapacidades EN_REVISION del rango, corrida previa sin cerrar.
**Reglas de negocio:** advertir, no bloquear (filosofía REQ-010) — salvo los huecos duros que el motor no puede absorber (definidos en plan).
**Criterios de aceptación:** reporte con conteos y detalle paginado; cada advertencia con código estable para el FE.
**Prioridad:** Alta. **Dependencias:** insumos existentes.

#### RF-008 - Generar planilla
**Descripción:** Crear la corrida de una nómina+periodo para todos los empleados de la población o un subconjunto (`employeeIds`), ejecutando el motor y persistiendo líneas por empleado×plaza×concepto; registra la acción (auditoría).
**Reglas de negocio:** una corrida ACTIVA por nómina+periodo (índice único parcial; regenerar reemplaza, no duplica); población según D-18; anti-carrera con lock advisory (molde `pg_advisory_xact_lock`); consumo de pools origen `MOTOR` en la misma transacción (D-09); advertencias por línea/empleado persistidas (molde warnings liquidación).
**Criterios de aceptación:** corrida GENERADA con líneas y totales; pools del periodo quedan APLICADA-MOTOR; doble submit concurrente → una sola corrida; golden A.3 en verde.
**Prioridad:** Alta. **Dependencias:** RF-001…003; motor D-08.

#### RF-009 - Cálculo del periodo (el motor)
**Descripción:** Módulo puro que calcula por empleado×plaza: salario del periodo (base proratizada P-07), ingresos (cuotas cíclicas vencidas, eventuales autorizados, HE = Σ horas×factor × valor-hora), descuentos (ISSS/AFP/Renta del periodo, cuotas de descuento, eventuales, tiempos no trabajados, amonestaciones aplicadas, descuento de incapacidad), patronales (ISSS/AFP/INCAF), garantía de ingreso mínimo (P-08) y neto.
**Reglas de negocio:** determinista, sin I/O; `Round2` una vez por línea; clase de línea EXPLÍCITA por concepto (lección `ResolveClass`: nunca default→Ingreso); tramo Renta faltante → warning + 0 (molde liquidación); base topada para ISSS/AFP según esquema.
**Criterios de aceptación:** **suite dorada del contador (Anexo A.3) como gate**; cuadre contra los insumos/exports del mismo filtro (test de cuadre por módulo).
**Prioridad:** Alta. **Dependencias:** P-07/P-08 ratificadas ANTES de construir.

#### RF-010 - Revisión y ajustes
**Descripción:** Detalle de la corrida por empleado (líneas, bases, unidades, warnings) con ajustes del revisor: override de monto por línea (con nota), incluir/excluir línea, notas por empleado.
**Reglas de negocio:** solo en GENERADA; overrides sobreviven recálculo (molde `OverrideAmount`/`FinalAmount` liquidación); línea de sistema no editable en su fórmula, solo override trazado.
**Criterios de aceptación:** ajustar → recalcular → override intacto; excluir línea recalcula neto; todo trazado (quién/cuándo/nota).
**Prioridad:** Alta. **Dependencias:** RF-008.

#### RF-011 - Regenerar planilla
**Descripción:** Volver a formular la corrida completa (paso d del flujo): limpia líneas, revierte aplicaciones `MOTOR` de pools y re-ejecuta el motor sobre el estado actual de los insumos.
**Reglas de negocio:** solo GENERADA; **la regeneración descarta ajustes** (por diseño — molde liquidación; se confirma en criterios); reversión simétrica de pools probada.
**Criterios de aceptación:** insumo nuevo registrado → regenerar lo incorpora; cuotas reaplicadas sin duplicar (idempotencia por periodo); anti-carrera.
**Prioridad:** Alta. **Dependencias:** RF-008.

#### RF-012 - Recálculo selectivo
**Descripción:** Recalcular los valores de **uno o varios empleados** de una corrida GENERADA sin tocar al resto (pedido literal del levantamiento).
**Reglas de negocio:** re-resuelve insumos solo de esos empleados (aplicando/revirtiendo sus pools); conserva overrides propios; audita el subconjunto.
**Criterios de aceptación:** empleado con insumo nuevo → recálculo selectivo lo refleja; los demás quedan byte-idénticos; totales de la corrida re-derivados.
**Prioridad:** Alta. **Dependencias:** RF-008/RF-010.

#### RF-013 - Programar generación — **DIFERIDO A F2 (ratificación P-14)**
**Descripción:** Agendar la generación de una corrida a fecha/hora. **El negocio lo difirió a una fase siguiente**; el diseño queda documentado para levantarlo sin re-análisis (D-13: cola en BD + worker de polling molde `ReportExportJob` con claim/retry/expire, precisión de minutos, cancelable, corre como el solicitante).
**Prioridad:** F2. **Dependencias:** RF-008.

### Grupo C — Autorización y cierre

#### RF-014 - Autorizar / devolver
**Descripción:** Decisión sobre la corrida: autorizar (pasa a AUTORIZADA, solo lectura) o devolver a revisión con motivo (AUTORIZADA→GENERADA).
**Reglas de negocio:** anti-self doble (quien generó/regeneró por última vez no autoriza); permiso `AuthorizePayrollRuns` **sin Admin** (exclusión en la policy); devolución exige motivo; concurrencia If-Match.
**Criterios de aceptación:** generador intenta autorizar → 403; tercero autoriza; devolución reabre ajustes; todo auditado.
**Prioridad:** Alta. **Dependencias:** RF-008.

#### RF-015 - Cerrar planilla
**Descripción:** Cierre de la corrida AUTORIZADA (post-envío): las líneas quedan definitivas, el periodo pasa a `CERRADO`, los pools aplicados quedan firmes.
**Reglas de negocio:** solo AUTORIZADA; el cierre es terminal en F1 (correcciones posteriores → periodo siguiente); anulación total solo antes de cerrar (revierte pools).
**Criterios de aceptación:** cerrar → periodo CERRADO; generar de nuevo sobre ese periodo → 422; anular antes de cerrar revierte pools.
**Prioridad:** Alta. **Dependencias:** RF-014.

### Grupo D — Envío (paso f)

#### RF-016 - Boletas de pago
**Descripción:** Boleta PDF por empleado desde las líneas de la corrida (ingresos, descuentos, neto, datos de pago), individual y en lote (zip o PDF concatenado — definir en plan).
**Reglas de negocio:** solo corridas AUTORIZADA/CERRADA; mapper → `DocumentModel` → seam QuestPDF/Gotenberg (molde boleta liquidación); contenido mínimo P-12.
**Criterios de aceptación:** boleta individual y lote 200 con PDF válido; datos cuadran con las líneas.
**Prioridad:** Alta. **Dependencias:** RF-014.

#### RF-017 - Impresión de planilla
**Descripción:** Reporte completo de la corrida (todas las filas empleado×concepto + totales por concepto, por centro de costo y generales) exportable (xlsx/pdf).
**Prioridad:** Alta. **Dependencias:** RF-008. **Criterios:** totales cuadran con Σ líneas; rate-limit Export.

#### RF-018 - Conciliación bancaria
**Descripción:** Reporte de la corrida agrupado por forma de pago → banco → cuenta (neto por empleado + totales por grupo) «para autorización» de Tesorería.
**Reglas de negocio:** cuenta = la primaria del empleado (`IsPrimary`); empleado sin cuenta y forma BANCO → advertencia en el reporte; formato de archivo bancario = P-13 (F1: reporte).
**Criterios de aceptación:** Σ grupos == neto total de la corrida; advertencias listadas.
**Prioridad:** Alta. **Dependencias:** RF-014; cuentas bancarias (existen).

### Grupo E — Consultas

#### RF-019 - Bandeja y detalle de planillas
**Descripción:** Bandeja corporativa de corridas (filtros nómina/periodo/estado/año + `StatusCounts`) y detalle con drill por empleado; consulta del historial de acciones de la corrida.
**Prioridad:** Alta. **Dependencias:** RF-008. **Criterios:** paginación estándar, StatusCounts span-todos (molde de la casa).

#### RF-020 - Exports
**Descripción:** Exports xlsx/csv/json de bandeja, detalle por empleado, líneas y conciliación (rate-limit Export, 413 por tamaño — molde `ReportExportDeliveryService`).
**Prioridad:** Media. **Dependencias:** RF-019.

## 7. Requerimientos no funcionales

| Categoría | Requisito |
|---|---|
| Seguridad | RBAC con gates fail-closed por handler; `Authorize*` sin Admin; multi-tenant por `TenantId` en todas las tablas; If-Match/ETag en mutaciones; sin montos en logs |
| Rendimiento | Corrida de 1,000 empleados × ~15 líneas en < 60 s (lote transaccional, locks ordenados por Id anti-deadlock); detalle paginado; índices por (tenant, corrida, empleado) |
| Consistencia | Generar/regenerar/recalcular = transaccional total (o todo o nada); anti-carrera con `pg_advisory_xact_lock` (molde REQ-005); idempotencia de aplicación de pools por periodo |
| Auditoría | AuditEvents por acción de corrida + actor + timestamp; líneas con referencia al registro fuente (cuota/jornada/registro) |
| Exactitud | Motor puro determinista; `Round2` único por línea; golden del contador como gate y en checklist de despliegue |
| Disponibilidad | La generación no bloquea lecturas (la programación y su worker quedan en F2 — P-14) |
| Usabilidad | Errores 422 bilingües EN/ES con códigos estables (`extensions.code`); warnings diferenciados de errores |
| Compatibilidad | Contrato FE aditivo en periodos; openapi.yaml a mano + guardrails; enums como strings; `XxxId`→`xxxPublicId` |
| Mantenibilidad | Motor en `.Rules.cs` puro con suite dorada; sin tocar motor de liquidación; moldes de la casa (governed masters, bandejas, exports) |

## 8. Historias de usuario

### HU-001 - Configurar la nómina y su calendario
Como **administrador RRHH**, quiero **crear la nómina quincenal con su moneda y reglas, y generar automáticamente sus 24 periodos del año**, para **no digitar el calendario a mano**.
**Criterios:** Dado un maestro de nómina QUINCENAL con total 24, cuando ejecuto la generación del año, entonces se crean 24 periodos `GENERADO` con rangos correctos y la re-corrida no duplica.

### HU-002 - Registrar jornadas
Como **administrador RRHH**, quiero **definir las jornadas laborales con horario por día y hora de comida**, para **que las plazas referencien un horario real y no un texto libre**.
**Criterios:** Dado el maestro, cuando asigno la jornada a una plaza con un código inexistente, entonces recibo 422; con código válido queda vinculada.

### HU-003 - Generar la planilla
Como **analista de planilla**, quiero **generar la planilla de la quincena para todos los empleados**, para **obtener el cálculo completo (salario, cuotas, horas extras, ley) sin transcribir insumos**.
**Criterios:** Dado el periodo con insumos registrados, cuando genero, entonces obtengo una corrida GENERADA con líneas por empleado, las cuotas del periodo quedan aplicadas con origen MOTOR y las advertencias quedan listadas.

### HU-004 - Revisar y ajustar
Como **analista de planilla**, quiero **revisar el detalle de un empleado, sobrescribir una línea con nota y recalcular**, para **corregir sin perder el ajuste**.
**Criterios:** Dado un override, cuando recalculo el empleado, entonces el override sobrevive; cuando REGENERO la corrida completa, entonces los ajustes se descartan y se me advierte antes.

### HU-005 - Recalcular a un empleado
Como **analista de planilla**, quiero **recalcular solo a los 3 empleados con insumos tardíos**, para **no re-formular a los 500 restantes**.
**Criterios:** Dado un insumo nuevo de un empleado, cuando recalculo selectivamente, entonces solo sus líneas cambian y los totales se re-derivan.

### HU-006 - Autorizar
Como **autorizador de planilla**, quiero **autorizar la corrida revisada (o devolverla con motivo)**, para **habilitar el pago con separación de funciones**.
**Criterios:** Dado que yo generé la corrida, cuando intento autorizarla, entonces 403; dado un tercero con el permiso, cuando autoriza, entonces AUTORIZADA y solo lectura.

### HU-007 - Emitir boletas y conciliar
Como **analista de Tesorería**, quiero **las boletas en lote y la conciliación por banco/cuenta**, para **ejecutar y autorizar el pago**.
**Criterios:** Dada una corrida AUTORIZADA, cuando pido la conciliación, entonces Σ grupos == neto total; cuando pido el lote de boletas, entonces cada PDF cuadra con las líneas.

### HU-008 - Programar la corrida — **F2 (P-14)**
Como **analista de planilla**, quiero **programar la generación para el día de corte a las 6:00 pm**, para **que la planilla esté lista al llegar**. *(Diferida por el negocio a la siguiente fase.)*

## 9. Reglas de negocio (consolidadas)

- **RN-01** Una corrida ACTIVA por nómina+periodo; regenerar reemplaza en el mismo agregado (histórico por auditoría, no por duplicado).
- **RN-02** Población = plazas activas con `PayrollTypeCode` de la nómina durante el periodo (P-06); perfil `RETIRADO` antes del inicio → fuera; finiquito EMITIDO dentro del periodo → fuera de la corrida (el finiquito pagó — P-11).
- **RN-03** Toda línea nace de una fuente trazada: motor (fórmula), pool (referencia al registro origen) o ajuste manual (override con nota y actor).
- **RN-04** Consumo de pools: solo elementos del periodo elegibles (cuotas vencidas al corte, eventuales AUTORIZADOS, HE AUTORIZADAS transcurridas no compensadas, tiempos no trabajados REGISTRADOS con monto>0, amonestaciones aplicadas, incapacidades REGISTRADA) — cada exclusión documentada por módulo (§R.1).
- **RN-05** Elementos con origen `LIQUIDACION` o cerrados por finiquito NUNCA entran al motor (anti doble beneficio — costuras ya construidas).
- **RN-06** La regeneración revierte las aplicaciones `MOTOR` de esa corrida y re-aplica sobre el estado vigente; la anulación (pre-cierre) revierte y libera el periodo.
- **RN-07** Base del periodo = salario base mensualizado/proratizado por la frecuencia con la regla ratificada (P-07); **nunca** reutilizar la base cruda del finiquito.
- **RN-08** ISSS/AFP con tope por esquema; Renta por tramos de la frecuencia (faltan tramos → warning y 0, nunca inventar); patronales calculados siempre (informativos para costo).
- **RN-09** Ingreso mínimo garantizado (si la nómina lo exige): el neto no baja del mínimo proratizado; los descuentos diferibles se posponen según la regla P-08 y **quedan pendientes en su pool** (no se pierden).
- **RN-10** Ajustes (override/exclusión) solo en GENERADA; sobreviven recálculo, mueren con regeneración (advertido).
- **RN-11** Anti-self doble en autorización; Admin no autoriza sin el grant explícito.
- **RN-12** CERRADA es terminal; correcciones posteriores van al periodo siguiente (F1).
- **RN-13** El motor no escribe `PersonnelFilePayrollTransaction` ni conceptos de compensación (test de no-escritura — molde de la casa).
- **RN-14** Ventana de HE: si la nómina define ventana y el periodo la materializa, crear jornadas HE fuera de ventana → 422 (asistencia: solo configuración por ahora).
- **RN-15** Todo monto redondeado una sola vez (`Round2` AwayFromZero) al nivel de línea; los totales son Σ de líneas redondeadas.

## 10. Flujos principales

### Flujo 1 — Configuración anual
1. RRHH crea la Nómina (tipo, frecuencia, total periodos, moneda, reglas). 2. Genera el calendario del año (periodos `GENERADO`). 3. Ajusta cortes/pagos donde aplique. 4. Crea jornadas y las asigna a plazas.

### Flujo 2 — Ciclo del periodo (a→f del levantamiento)
1. Los módulos existentes registran insumos (paso b — ya construido). 2. El analista corre el **pre-flight** y resuelve advertencias. 3. **Genera** (RF-008): motor + pools origen MOTOR + warnings. 4. **Revisa** el detalle, ajusta líneas (override/excluir), **recalcula selectivo** o **regenera** si entraron insumos tardíos. 5. **Autoriza** un tercero (RF-014). 6. **Envía**: boletas en lote + impresión de planilla + conciliación bancaria (RF-016…018). 7. **Cierra** (RF-015): periodo `CERRADO`, pools firmes.

### Flujo 3 — Recálculo selectivo
1. Corrida GENERADA. 2. Ingresa un insumo tardío de 2 empleados. 3. Recalcular seleccionando esos empleados. 4. Solo sus líneas cambian; totales re-derivados; overrides propios intactos.

### Flujo 4 — Corrida programada **(F2 — P-14)**
1. El analista programa nómina+periodo a fecha/hora. 2. El worker toma el job al vencer. 3. Genera y audita como el solicitante. 4. Falla → job en error con motivo y reintento acotado.

### Flujo 5 — Devolución
1. Corrida AUTORIZADA. 2. Tesorería detecta un error antes de pagar. 3. El autorizador **devuelve** con motivo → GENERADA. 4. Ajuste → recálculo → re-autorización.

## 11. Flujos alternativos y excepciones

| Escenario | Comportamiento |
|---|---|
| Generar sin periodos del año | 422 — el pre-flight lo anticipa |
| Generar con corrida activa existente | 422 (regenerar es la vía; una activa por nómina+periodo) |
| Doble submit concurrente de generar/regenerar | Lock advisory → segunda espera/recibe 409-422 coherente; cero corridas duplicadas |
| Empleado sin salario base o sin plaza del tipo | Advertencia; empleado excluido o con línea 0 según regla del plan (pre-flight lo lista) |
| Tramos Renta faltantes para la frecuencia | Warning por corrida + Renta 0 (nunca inventar) — molde liquidación |
| Cuota del pool con estado cambiado entre pre-scan y aplicación | Rollback total + 422 con detalle (molde `apply-period` REQ-005) |
| Autorizar quien generó | 403 anti-self |
| Regenerar tras ajustes | Advertencia explícita: los ajustes se descartan |
| Cerrada y se detecta error | Corrección en el periodo siguiente (F1); anulación ya no procede |
| (F2) Job programado vence con corrida ya generada manualmente | Job termina en no-op auditado |
| Empleado con 2 plazas de nóminas distintas | Entra en cada corrida por la plaza correspondiente; boleta por nómina (P-15 confirma consolidación) |
| Incapacidad EN_REVISION al generar | No entra (solo REGISTRADA); pre-flight la lista como pendiente de confirmación |

## 12. Datos requeridos

### Entidad: Nómina (`PayrollDefinition` — maestro por empresa, D-02)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / normalizedCode | Texto ≤80 | Sí | Único por empresa (normalizado) | Código de la nómina |
| name | Texto ≤200 | Sí | — | Descripción |
| payrollTypeCode | Texto ≤80 | Sí | Activo en `payroll-types` | «Tipo (catálogo)» |
| payPeriodCode | Texto ≤80 | Sí | Activo en `pay-periods` | Frecuencia («periodo») |
| totalPeriods | Entero | Sí | Coherente con `PeriodsPerYear` | Total de periodos del año |
| guaranteesMinimumIncome | Booleano | Sí (default no) | — | Garantizar ingreso mínimo tras descuentos |
| currencyCode | Texto 3 | Sí | Activo en `currencies` | Moneda |
| overtimeWindowEnabled / overtimeWindowRangeCode / overtimeWindowOffsetDays | Bool / Texto / Entero | No | P-18 | Regla de ventana de ingreso de HE |
| attendanceWindowEnabled / attendanceWindowRangeCode | Bool / Texto | No | P-18 | Regla de ventana de asistencia (config anticipada) |
| isActive / concurrencyToken | Bool / Guid | — | Guard de uso al inactivar | Governed |

### Entidad: Periodo de planilla (`PayrollPeriodDefinition` — EXTENSIÓN, D-03)

| Campo NUEVO | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code | Texto ≤80 | Sí (nuevos) | Único por nómina+año | Código del periodo |
| payrollDefinitionId | FK | Sí (nuevos) | Nómina activa de la empresa | «Nómina para la que se crea» |
| cutoffDate | Fecha | Sí (nuevos) | inicio ≤ corte ≤ fin | Fecha de corte |
| paymentDate | Fecha | Sí (nuevos) | ≥ inicio | Fecha de pago |
| month | Entero 1-12 | Sí (nuevos) | Coherencia P-04 | Mes de imputación |
| allowsOvertimeEntry + overtimeEntryStart/End | Bool + fechas | No | Ventana coherente | Permite HE + su ventana |
| allowsAttendance + attendanceEntryStart/End | Bool + fechas | No | Ventana coherente | Config anticipada |
| statusCode | Texto | Sí | `GENERADO/CERRADO/ANULADO` (catálogo país) | «Estado de la planilla» del periodo |
| *(existentes)* | — | — | — | `PayPeriodTypeCode`, `Year`, `Number`, `Label`, `StartDate`, `EndDate`, `IsActive` se conservan |

### Entidad: Jornada (`WorkSchedule` + `WorkScheduleDay`, D-06)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Único normalizado por empresa | Código y descripción |
| scheduleLabel | Texto ≤200 | No | — | «Horario» legible |
| attendanceDateAnchor | Enum `ENTRADA/SALIDA` | Sí | — | Ancla de fecha de asistencia |
| totalWeeklyHours | Decimal | Derivado/editable | > 0 | Total de horas |
| scheduleClass | Enum `ORDINARIA/EXTRAORDINARIA` | Sí | — | Clase de jornada |
| **Día**: dayOfWeek / startTime / endTime / mealStart / mealEnd / netHours | Entero 0-6 / horas | Sí por fila | fin>inicio (con cruce); comida contenida | Detalle por día |

### Entidad: Corrida (`PayrollRun` + `PayrollRunLine`, D-07)

| Campo (cabecera) | Tipo | Descripción |
|---|---|---|
| payrollDefinitionId + payrollPeriodId | FK | Nómina + periodo (única activa) |
| statusCode | Catálogo país | `GENERADA/AUTORIZADA/CERRADA/ANULADA` |
| generatedByUserId / generatedUtc / regeneratedCount | Guid/fecha/int | Trazabilidad de formulación |
| authorizedByUserId / authorizedUtc / returnReason | Guid?/fecha?/texto | Decisión |
| closedByUserId / closedUtc | Guid?/fecha? | Cierre |
| employeeCount / totalIncome / totalDeductions / totalEmployerCost / totalNet | Int/decimal | Totales derivados persistidos |
| warningsJson | jsonb | Advertencias de corrida |

| Campo (línea) | Tipo | Descripción |
|---|---|---|
| personnelFileId + assignedPositionPublicId | FK/Guid | Empleado × plaza |
| conceptCode / conceptName / lineClass | Texto / `Ingreso,Descuento,PagoPatronal` | Concepto snapshot + clase EXPLÍCITA |
| units / baseAmount / calculatedAmount / overrideAmount / finalAmount | Decimal | Cálculo + ajuste (final = override ?? calculated) |
| isIncluded / overrideNote / adjustedByUserId | Bool/texto/Guid? | Revisión |
| sourceModule / sourceReferencePublicId | Texto/Guid? | Registro fuente (cuota, jornada, incapacidad…) |
| currencyCode | Texto 3 | Snapshot |

### Entidad: Programación (`PayrollRunSchedule`) — **F2 (P-14 ratificada)**
No se construye en F1. Diseño reservado (molde ReportExportJob, D-13): `payrollDefinitionId`, `payrollPeriodId`, `scheduledForUtc`, `requestedByUserId`, `statusCode (PENDIENTE/EJECUTADO/CANCELADO/ERROR)`, `attempts`, `lastError`.

### Catálogos nuevos (semilla `HasData`, IDs tentativos — Anexo A.2)
- Estados de corrida (`payroll-run-statuses`): `GENERADA/-9970`, `AUTORIZADA/-9971`, `CERRADA/-9972`, `ANULADA/-9973`.
- Estados de periodo (`payroll-period-statuses`): `GENERADO/-9975`, `CERRADO/-9976`, `ANULADO/-9977`.
- (P-02 resuelta por ELIMINACIÓN — no hay catálogo de «aplicación».)

### Reutilizados (solo lectura)
`payroll-types` (-9890…-9895) · `pay-periods` (-9740…-9743) · `currencies` (-9370) · conceptos país `compensation-concept-types` (con `IsBaseSalary`, tasas, `Nature`) · `IncomeTaxWithholdingBracket` · `MinimumMonthlyWage` del perfil · `CompanyHoliday` · cadena `RestDayOfWeek` · `CostCenter` · formas de pago + cuentas bancarias.

## 13. Integraciones necesarias

| Integración | Estado |
|---|---|
| Pools transaccionales (5 módulos) vía aplicación origen `MOTOR` | Costura sembrada — se consume (D-09) |
| Incapacidades / tiempos no trabajados / amonestaciones (lectura de registros del periodo) | Construidos — se leen (P-10) |
| Motor de liquidación | **Solo coexistencia** (RN-05, P-11); cero cambios |
| PDF (`DocumentModel` → QuestPDF/Gotenberg) | Existe — se reutiliza |
| Correo electrónico | Solo stubs — envío de boletas = F2 |
| Banca (archivo de pago) | No existe — F1 entrega reporte de conciliación (P-13) |
| Contabilidad / ERP | Fuera de alcance |
| `PersonnelFilePayrollTransaction` (nómina externa) | No se toca en F1; destino = P-16 |

## 14. Roles y permisos

| Rol/Permiso | Permisos | Restricciones |
|---|---|---|
| `PayrollConfiguration.Read/Manage` (nuevo) | Ver/gestionar maestro Nómina + jornadas | Estricta (molde config; sin autoservicio) |
| `LeaveConfiguration.Read/Manage` (existente) | Periodos (controller actual conservado) | Sin cambio de familia (contrato FE estable) |
| `ViewPayrollRuns` (nuevo) | Bandeja, detalle, exports, boletas (lectura) | Sin ver ajustar |
| `ManagePayrollRuns` (nuevo) | Pre-flight, generar, ajustar, regenerar, recalcular, programar, cerrar, boletas/conciliación | No autoriza |
| `AuthorizePayrollRuns` (nuevo) | Autorizar / devolver | **Sin Admin implícito** (exclusión en policy); anti-self doble |
| Empleado | (F2: ver su boleta en portal) | F1 sin autoservicio |

## 15. Criterios de aceptación generales

1. Suite dorada del contador (Anexo A.3) verde como **gate del motor** + sign-off en checklist de despliegue.
2. Test de cuadre por módulo: total de la corrida por concepto ≡ insumo/export del mismo filtro (los exports pasan a ser verificación).
3. Reversión simétrica probada: generar→regenerar→anular deja los 5 pools byte-idénticos al estado previo.
4. Test de no-escritura: la corrida no toca `PersonnelFilePayrollTransaction` ni `PersonnelFileCompensationConcept`.
5. Anti-carrera: dobles submits de generar/regenerar/recalcular sin duplicados ni 500.
6. Suites existentes de los 11 REQ **verdes sin editar** (retrocompatibilidad de contratos).
7. openapi.yaml sin drift + guía FE publicada + errores bilingües con códigos estables.
8. Corrida de volumen (≥500 empleados sintéticos) dentro del presupuesto de tiempo.

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **Responsabilidad legal del cálculo recurrente** (el mayor): mitigación = golden del contador ANTES de construir + warnings nunca-inventar + sign-off en despliegue (precedente liquidación/REQ-008/REQ-011).
- **Proratización y Renta por frecuencia sin definición oficial** (P-07): bloquea el motor; se ratifica primero.
- ~~Semánticas ambiguas del levantamiento~~ **RESUELTO en la ratificación**: «aplicación» eliminada (P-02), «parámetros especiales» ignorados en F1 (P-03), ventanas modeladas como regla fuente+offset con fechas editables (P-18).
- **Doble beneficio con liquidación** en retiros de medio periodo: reglas RN-02/RN-05 + P-11.
- **Volumen de líneas** (500 empleados × 24 periodos × 15 líneas ≈ 180k filas/año): índices + paginación + totales persistidos.
- **Expectativa de correo** en «envío de boletas»: comunicar F1 = descarga/lote; correo = F2.

### Supuestos
- SV/USD únicamente (cadena completa); datos actuales son de prueba (limpiezas destructivas permitidas; estabilidad de contrato FE se respeta).
- Los insumos certificados (REQ-001…011) se mergean a master antes de arrancar este REQ.
- El contador está disponible para ratificar P-07/P-08 y los golden A.3.
- La operación por periodo existente (aplicación manual + insumos) sigue disponible durante la transición.

### Dependencias
- Merge de `feature/vacaciones-incapacidades` y `feature/planilla-descuentos` a master (los insumos del motor).
- Sign-offs de negocio pendientes previos: amortización REQ-008 y séptimo REQ-011 (el motor consume esos montos).
- Ratificación de P-01…P-18 (este documento) antes del plan técnico.
- Catálogos compartidos ya sembrados (`payroll-types`, `pay-periods`).

## 17. Preguntas al negocio — **P-01…P-18 RESPONDIDAS (ratificación 2026-07-14)**

Las respuestas que coinciden con la columna final son recomendaciones **aceptadas tal cual** («las que no se tocaron se ratifican»); las 4 con ajuste están marcadas en negrita (P-02, P-03, P-07, P-14).

| # | Ámbito | Pregunta | Respuesta del negocio (2026-07-14) |
|---|---|---|---|
| **P-01** | Estructural | ¿Se ratifica el **pivote de frontera** (CLARIHR calcula la nómina) y el faseo F1 en 3 olas + F2 (correo, asistencia, multi-nivel, notificaciones)? | Sí — es el levantamiento anunciado; F2 tal cual §3 |
| **P-02** | Maestro | ¿Qué significa **«aplicación (lista desplegable)»** del tipo de planilla? ¿Cuáles son sus valores? | **«No lo tenemos claro — removámoslo»** → el campo se **ELIMINA** del maestro (D-02 ajustada) |
| **P-03** | Maestro | ¿Qué **«parámetros especiales»** usan hoy las planillas? (ejemplos reales) | **«No se tiene claro — ignorar»** → fuera de F1; se levanta en F2 si aparecen ejemplos (D-02 ajustada) |
| **P-04** | Periodos | ¿«Mes y año en que se aplica el periodo» = mes contable de imputación (puede diferir del mes calendario del rango)? | Sí — campo explícito `month` + validación blanda |
| **P-05** | Periodos | ¿«La generación puede ser automática, el estado debe ser generado» = generación masiva del calendario anual con estado inicial `GENERADO`? | Sí (D-04, molde vacaciones) |
| **P-06** | Población | ¿Qué ata empleado↔nómina: el `PayrollTypeCode` de la plaza (as-is, D-18) o una **asignación explícita de nómina** en la plaza (FK al maestro)? | F1 = PayrollTypeCode (cero migración de plazas); FK explícita si el negocio necesita 2 nóminas del mismo tipo |
| **P-07** | **Contador** | Regla de **proratización** (quincenal = mensual/2 exacto ¿o por días?; semanal; POR_DIA/POR_OBRA) y **Renta no mensual** (¿tabla mensual ÷ factor o tablas propias por frecuencia?) | **«Debe permitir todos los tipos: mensual, quincenal, por día, por obra»** (+ semanal del catálogo) → **alcance TOTAL ratificado**; la aritmética exacta por tipo y las tablas de Renta por frecuencia se fijan con los **golden A.3 ampliados (casos 11-13) — gate del contador ANTES del PR del motor**; el modelo de brackets ya soporta otros `PayPeriodCode` (cargar tablas oficiales) |
| **P-08** | **Contador** | Regla exacta del **ingreso mínimo garantizado tras descuentos**: ¿qué descuentos son diferibles, en qué orden, y la ley (ISSS/AFP/Renta) nunca se difiere? | Ley intocable; diferir cuotas voluntarias LIFO por monto; cuota diferida queda pendiente en su pool |
| **P-09** | Pools | ¿La corrida **aplica** los pools con origen `MOTOR` (recomendado — la costura sembrada) con reversión al regenerar/anular? | Sí — D-09; la aplicación manual queda como borde |
| **P-10** | Incapacidades | ¿El descuento del empleado (y el subsidio patronal informativo) de las incapacidades REGISTRADA entra como línea automática? | Sí — snapshot ya calculado por REQ-001; el motor solo lo toma |
| **P-11** | Coexistencia | ¿Retirados/liquidados dentro del periodo quedan **fuera** de la corrida (el finiquito pagó sus días)? ¿Vacaciones y tiempo compensatorio siguen SIN línea de planilla en F1? | Sí y sí (goce no altera el pago; CT paga en finiquito) |
| **P-12** | Boletas | Contenido mínimo/formato de la boleta y ¿el envío por correo queda para F2 (no hay proveedor real)? | Molde boleta liquidación; correo F2 |
| **P-13** | Tesorería | ¿La conciliación bancaria F1 es el reporte agrupado (banco/cuenta/totales) o se requiere **archivo bancario** con formato específico? ¿De qué banco(s)? | F1 reporte; archivo = F2 con spec del banco |
| **P-14** | Programación | ¿La **programación a fecha/hora** es imprescindible en F1 (cola de jobs, precisión de minutos) o puede ser F2? | **«Aún no realizar la programación — la realizaremos en una siguiente fase»** → **DIFERIDA a F2** (D-01/D-13 ajustadas; diseño reservado) |
| **P-15** | Multi-plaza | Empleado con varias plazas: ¿líneas por plaza con **boleta consolidada por empleado** (por nómina)? | Sí — coherente con pools y liquidación |
| **P-16** | Ledger externo | Con motor interno, ¿`PersonnelFilePayrollTransaction` (sincronización de nómina externa) se congela, se retira o se mantiene para históricos/correlación? | F1: intacto y sin uso; decidir retiro en F2 (sin producción, retirar es barato) |
| **P-17** | Jornadas | ¿Se valida `WorkdayCode` de la plaza contra el maestro con **limpieza destructiva** de los valores libres actuales (datos de prueba)? | Sí — precedente REQ-004 (`payroll_type_code`) |
| **P-18** | Ventanas | Semántica exacta de **«rango (lista desplegable)» + «días de desplazamiento»** para HE/asistencia (¿ventana relativa al periodo actual/anterior desplazada N días?) — ejemplos reales | Modelar como regla (fuente + offset) que materializa fechas editables por periodo |

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar P-01 y las 4 bloqueantes (P-06/P-07/P-08 + P-02) antes de cualquier plan técnico** — P-07/P-08 son del contador y definen el motor.
2. **Golden cases primero** (Anexo A.3): mismo proceso que convirtió la liquidación, la amortización (REQ-008) y el séptimo (REQ-011) en cálculos defendibles — «si el contador discrepa, se corrige el cálculo, no el modelo».
3. **Construir en olas con gates**: Ola A es ensamblaje de moldes (bajo riesgo, valor inmediato de configuración); la Ola B carga TODO el riesgo — su PR del motor puro con suite dorada es el gate del REQ; Ola C reusa reporting/PDF.
4. **No tocar el motor de liquidación ni su base cruda** — coexistencia por reglas, no por refactor. El costo de mover cifras de finiquitos certificados es mayor que duplicar 30 líneas de proratización propia.
5. **Mantener los insumos/exports vivos** como verificación de cuadre (test dorado corrida≡insumo) y como plan B operativo durante la adopción.
6. **Piloto con una nómina** (la quincenal) un periodo completo en paralelo contra la nómina externa antes del corte definitivo — la comparación es gratis gracias a los insumos.
7. **Comunicar los dos «no» de F1** para gestionar expectativas: boletas se descargan (no se envían por correo) y la asistencia es configuración (no marcación).
8. Registrar este levantamiento como **REQ-012** en el backlog (protocolo: ningún desarrollo sin análisis ratificado) — hecho en esta misma fecha.

---

## Anexo A — Referencias y propuestas

### A.1 Cierre del mapa de la visión (origen: REQ-005 Anexo A.1)

| Capacidad de la visión | Cobertura tras este REQ |
|---|---|
| Registrar ingresos / descuentos | ✅ REQ-005/006/008/009 (certificados) |
| Nivel de endeudamiento | ✅ REQ-010 |
| Creación de periodos de planilla | ✅ REQ-001 + **extendidos aquí** |
| **Generar la planilla** | ✅ **ESTE REQ** (era la única fila pendiente) |
| Transacciones no aplicadas / periodo activo | ✅ Pool pleno = la corrida (este REQ) |
| Historial de pago | ✅ REQ-005 + líneas de corrida |

### A.2 Seeds tentativos (verificar IDs libres contra `GlobalCatalogSeedData` al abrir PR-1)

- **Ocupación verificada 2026-07-13**: en código hasta `-9945` (`DESCUENTO_EVENTUAL_PENDIENTE`); reservas de planes hasta `-9969` (REQ-008…011; el bloque `-9950…-9959` quedó LIBRE — REQ-010 no consumió). **Libre global: `-9970+`**.
- **Propuesta (bloque `-9970…-9989`)**: estados de corrida `-9970…-9973` · estados de periodo `-9975…-9977` · holgura `-9978…-9989` (candidatos: catálogo «aplicación» P-02, tipos de ventana P-18).
- Trampas vigentes: `-9490…-9496` (`ACTION_STATUS_CATALOG`) y los falsos IDs por fragmentos de GUID en `Migrations/*.Designer.cs`.
- Sin ActionTypes nuevos (D-14: sin asientos por empleado).

### A.3 Casos dorados — **✅ VALIDADOS 13/13 por el negocio/contador (2026-07-14). El GATE de la Ola B está CUMPLIDO.**

Los 13 resultados firmados se codifican literalmente como la suite dorada del PR del motor (plan §2.3).

| # | Caso | Respuesta del negocio (2026-07-14) |
|---|---|---|
| 1 | **Quincena base**: salario mensual $600, nómina QUINCENAL → base, ISSS/AFP topados, Renta quincenal, neto | ✔ «El caso es correcto» → **quincenal = mensual/2** confirmado |
| 2 | **HE valoradas**: 2h30m ×2.00 + 1h30m ×2.50 = 8.75 h-factor; con $600 (hora $2.50) → **$21.88** | ✔ «Cálculo correcto» |
| 3 | **Cuota con interés compuesto** (REQ-008) | ✔ «Entra tal cual, el motor no re-amortiza» |
| 4 | **Séptimo**: lunes-viernes sin goce | ✔ «Descuenta 6 días, no 5» |
| 5 | **Incapacidad con tramos** (snapshot REQ-001) | ✔ «Descuento al empleado más aporte patronal» |
| 6 | **Mínimo garantizado** (P-08: ley intocable; diferir voluntarias LIFO) | ✔ **«Ingreso mínimo $408.80 según ley»** → referencia mensual **$408.80** (cargada en `MinimumMonthlyWage` de la ficha); proratizado: quincena **$204.40** · semana **$95.39** (408.80×7/30) |
| 7 | **Multi-plaza** | ✔ «Línea por plaza, 1 boleta» |
| 8 | **Retiro a media quincena** | ✔ «Cero doble pago, queda por fuera» (el finiquito paga) |
| 9 | **Amonestación con concepto de egreso** | ✔ «Puede ser aplicada» → línea de descuento con snapshot |
| 10 | **Cuadre global** | ✔ «La corrida suma igual que los insumos actuales» (test por los 5 pools) |
| 11 | **Nómina POR_DIA** | ✔ **«En El Salvador son 44 horas semanales por ley; realiza el cálculo tú»** → aritmética FIJADA (abajo) |
| 12 | **Nómina POR_OBRA** | ✔ **«Se paga por obra un valor fijo»** → sin salario base; el pago del periodo = Σ valores fijos de las obras **registradas como ingresos** (eventuales/cíclicos) — sin módulo de captura de unidades (levantamiento aparte si se necesita) |
| 13 | **Renta por frecuencia** | ✔ «Debemos tener toda la tabla» → **AJUSTADO 2026-07-15: TRES tablas — semanal, quincenal y mensual** (la diaria se descartó); MENSUAL ya sembrada, SEMANAL/QUINCENAL del decreto DGII las siembra el PR del motor; la frecuencia del periodo elige la tabla; sin tabla → warning + retención 0, nunca derivación inventada |

**Aritmética fijada por el caso 11 (delegada por el contador, anclada en la semana legal de 44 h):**

- **Semana legal = 44 h**: jornada ordinaria diurna 8 h/día — L-V (40 h) + sábado 4 h. La **plantilla de jornada** del maestro se siembra con 44 h (no 40).
- **Tarifa diaria = salario mensual ÷ 30** (mes comercial — la convención YA certificada en liquidación, incapacidades y tiempos no trabajados). Con $600 → **$20.00/día**.
- **Hora ordinaria = diaria ÷ 8** → **$2.50/h** (lo que excede la jornada va por horas extras con factor — REQ-007).
- **Base por frecuencia (todo deriva de la diaria, SIEMPRE en días COMERCIALES — ajuste 2026-07-15)**: MENSUAL = mensual (30 días) · QUINCENAL = 15 × diaria = mensual/2 ✔ (coherente con el caso 1) · SEMANAL = 7 × diaria = mensual×7/30 (con $600 → $140.00; el descanso semanal es remunerado) · **POR_DIA = diaria × días COMERCIALES del periodo** (misma base; su diferencia es la GRANULARIDAD día/hora del pago y los descuentos, no el conteo) · POR_OBRA = 0 + ingresos (caso 12).

**Ajustes post-firma del negocio (2026-07-15) — tres precisiones registradas ANTES de construir:**

1. **La quincena SIEMPRE son 15 días**: «si el mes es de 16 días se pagan los 15, porque el cálculo es 30/2 — no más no menos». La base de TODOS los tipos usa **días comerciales** (mes 30 · quincena 15 · semana 7). Esto **corrige** la regla previa del caso 11 para POR_DIA (que proponía días calendario reales: el ejemplo de la quincena de 16 días × diaria queda SIN efecto).
2. **Granularidad POR HORA del pago de días trabajados — GOLDEN 14 (ejemplo literal del negocio)**: contrato $1,000/mes → diaria $33.33 (1000/30, mes de 30 «no más no menos») · hora $4.17 (diaria/8). Un **permiso personal de 4 horas** → la quincena no paga 15 días sino **14.5**: base quincenal **$500.00** (15 días comerciales) − descuento **$16.67** (4 h = 0.50 días × diaria) = **$483.33**. **Verificado contra el código — el escenario YA está construido y certificado**: el registro de tiempos no trabajados (REQ-011) captura HORAS (`PersonnelFileNotWorkedTime.Hours`) y su motor convierte `HoursToDays = Round2(horas ÷ horas-día estándar, default 8)` (`NotWorkedTime.Rules.cs:184-186`) → días × diaria × % del tipo; el motor de planilla toma ese `DiscountAmount` como línea automática (caso 4 / P-10). Las **incapacidades** son POR DÍAS por naturaleza (constancia médica en días) y su descuento viene calculado en el snapshot de REQ-001 — también entra como línea (caso 5). Lo único que sigue siendo futuro es el módulo de **solicitudes** de permiso (autoservicio del empleado — F2 de REQ-011); el REGISTRO del descuento por horas existe hoy vía RRHH, y la empresa puede crear el tipo «PERMISO_PERSONAL» en su maestro (por empresa, editable, modo horas).
3. **Renta: TRES tablas — semanal, quincenal y mensual** (la diaria se descarta → desaparece la bandera de despliegue sobre su fuente oficial). El PR del motor siembra SEMANAL y QUINCENAL (MENSUAL ya existe).

### A.4 Máquina de estados propuesta

```
Corrida:   (generar) → GENERADA ⇄ (regenerar/recalcular/ajustar)
           GENERADA → AUTORIZADA (anti-self)   AUTORIZADA → GENERADA (devolver, con motivo)
           AUTORIZADA → CERRADA (terminal)     GENERADA|AUTORIZADA → ANULADA (pre-cierre; revierte pools)
Periodo:   GENERADO → CERRADO (al cerrar su corrida) · ANULADO (sin corrida)
Pools:     elegible → APLICADA(origen MOTOR) al generar · reversión simétrica al regenerar/anular · firme al CERRAR
```

### A.5 La frontera «nómina externa» — citas de su levantamiento y de su cierre

- `PersonnelFileCompensation.cs:8-10` (comentario de dominio): la nómina se procesa fuera de CLARIHR — **queda obsoleto con este REQ (actualizar el comentario en el plan)**.
- `analisis-plazas-ingresos-egresos.md` D-08 · `analisis-otras-transacciones-personal.md` RN-14 · `analisis-tiempo-compensatorio-empleado.md` G-06 · `analisis-planilla-ingresos-ciclicos.md` P-01/A.1/A.4 · `analisis-planilla-descuentos-y-endeudamiento.md` («solo queda pendiente el motor de generación»).
- Precedente de proceso para motores: liquidación (PR #56) — levantamiento propio + golden del contador + checklist pre-despliegue. **Este documento sigue ese camino.**
