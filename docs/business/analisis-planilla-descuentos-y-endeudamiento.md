# Análisis de negocio — Módulo de planilla: descuentos cíclicos y eventuales, endeudamiento y tiempos no trabajados

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | Planilla — **el lado egresos** del programa: **descuentos cíclicos** (plan de cuotas con segmentos, interés compuesto y cuotas extraordinarias) · **descuentos eventuales** · **endeudamiento** (parámetros + validación + consulta/simulación) · **tiempos no trabajados** (maestro + registro con descuento calculado). Es la rebanada anticipada por REQ-005 (P-10 ratificada: «entra después de los ingresos cíclicos»; A.1; D-02 «modelo preparado») |
| **Fecha** | 2026-07-12 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código (HEAD `ca94d8c`, master con REQ-001…007 integrados) |
| **Estado** | **RATIFICADO por el negocio (2026-07-12)** — decisiones **D-01…D-22 ratificadas** (D-08 ajustada por P-03: tasa de interés con **default configurable por empresa**; D-16 anotada por P-13: **gap documentado** hasta REQ-010) y **P-01…P-21 respondidas** (§17); **P-22 ELIMINADA** (sin producción: frontend y data de prueba — **sin fallbacks, sin legacy, sin adopción**). La **división en 4 planes (REQ-008…REQ-011) quedó confirmada** (P-01), con REQ-011 adelantable si urge el ausentismo |
| **Naturaleza del módulo** | **Espejo del lado ingresos ya construido y certificado** (REQ-005 ingresos cíclicos · REQ-006 eventuales): ~70 % del diseño hereda decisión por decisión (numeración D espejo). Greenfield verificado en el lado descuentos transaccional; los 4 deltas duros son: **segmentos de cuotas**, **interés compuesto**, **cuotas extraordinarias** y **endeudamiento** (net-new total). «Tiempos no trabajados» es un módulo hermano de dominio distinto (ausencias) con todos sus insumos de calendario ya construidos |
| **Documentos hermanos** | [`analisis-planilla-ingresos-ciclicos.md`](analisis-planilla-ingresos-ciclicos.md) (molde del Plan 1; su A.1 mapea este levantamiento como «rebanada siguiente») · [`analisis-planilla-ingresos-eventuales.md`](analisis-planilla-ingresos-eventuales.md) (molde del Plan 2) · [`analisis-plazas-ingresos-egresos.md`](analisis-plazas-ingresos-egresos.md) (catálogo de conceptos `Nature=Egreso` que se reutiliza como «tipos de descuentos») · [`analisis-vacaciones-incapacidades-empleado.md`](analisis-vacaciones-incapacidades-empleado.md) (motor de días con exclusiones — precedente del cálculo de tiempos no trabajados) · [`analisis-otras-transacciones-personal.md`](analisis-otras-transacciones-personal.md) (suspensiones documentales + disponibilidad de tiempo) |
| **Documentos relacionados** | `analisis-liquidacion-empleado.md` (motor de finiquito: líneas de deducción sugeridas ya soportadas) · planes técnicos de REQ-005/006 (anclas de implementación del molde) |

---

## Contexto del cambio (requerimiento original)

El levantamiento pide el **lado descuentos** del módulo de planilla:

1. **Nuevo descuento cíclico** — aplicar los descuentos cíclicos de un empleado (préstamos bancarios, procuraduría, cooperativas…). Información principal: **estado, fecha que entra en vigencia, referencia, empleado, tipo de descuento cíclico (catálogo), institución financiera, observación**. Información de cuotas: **fecha de inicio de descuento, meses con excepción (catálogo), moneda (catálogo), tipo de planilla (catálogo), frecuencia de cuota (catálogo), frecuencia de aplicación (catálogo), monto y número de cuotas indefinidas (sí/no)**. Detalle de cuotas: **cuota inicial, cuota final, frecuencia, valor**; el sistema calcula **total cobrado y total no cobrado**. Además **deberá permitir la liquidación**.
2. **Cálculo de interés compuesto** — el sistema podrá calcular interés compuesto en las cuotas, detallando **valor de cuota, monto a capital y monto a interés** automáticamente.
3. **Cuotas extraordinarias** — agregar a una **referencia crediticia** existente: **nº de cuota, fecha, valor, planilla de aplicación, periodo de planilla**.
4. **Porcentaje de endeudamiento configurado** — validar el valor a descontar contra el **% máximo de endeudamiento** configurado; mostrar mensaje si se sobrepasa.
5. **Parámetros de aplicación de endeudamiento** — parámetros de **valor fijo** que definen el comportamiento del sistema según el alcance que el usuario defina.
6. **Descuentos eventuales** — descuentos ocasionales: **empleado, tipo de descuento (catálogo), fecha de aplicación, monto, valor fijo (sí/no) — si no, factor o porcentaje —, moneda (catálogo), solicitante, planilla en que se procesa, periodo de planilla**.
7. **Catálogos** — **Tipo de descuento cíclico** (deducciones no de ley: bancarios, procuraduría, cooperativas, asociaciones…) y **Tipos de descuentos** (impuestos, ahorro de pensiones, préstamos, cooperativas…), este último **asociado con el catálogo de familias de ingresos y descuentos**.
8. **Tiempos no trabajados** — **ingresar tiempo no trabajado** a un empleado con sus descuentos si aplican (si se adquiere el módulo de marcación, se tomará de los biométricos); **catálogo de tipos de tiempo no trabajado** con: nombre, aplica para permiso (sí/no), utiliza jornada (sí/no), incluye asueto (sí/no), incluye domingo (sí/no), incluye sábado (sí/no), descuento de séptimo (sí/no), % de descuento, tipo de descuento (catálogo), tipo de ingreso (catálogo).
9. **Consulta de nivel de endeudamiento** — consultar el endeudamiento actual de un empleado y **simular** (ingreso actual + deducción adicional → alerta si supera el nivel; **solo simulación, no afecta planilla**). **Parámetros de endeudamiento por tipo de descuento** con advertencia al superar el límite mayor — **el sistema debe permitir el endeudamiento si se confirma la acción**.

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **Es la rebanada que el propio producto dejó anticipada y preparada.** REQ-005 ratificó P-10 («los descuentos cíclicos y el endeudamiento entran inmediatamente después»), mapeó ambas capacidades en su Anexo A.1 como «rebanada siguiente» y diseñó su modelo «preparado para naturaleza» (D-02). El molde completo está **construido, certificado y en master**: entidad con plan de cuotas + flujo de una decisión + aplicación por periodo + bandejas/exports + gancho de liquidación con reapertura simétrica (`PersonnelFileRecurringIncome.cs`, `RecurringIncomes.Rules.cs`, 3 controllers, lock advisory `RINC`).
2. **Greenfield verificado en el lado descuentos transaccional.** Cero entidades/flujos de descuento cíclico o eventual de empleado (búsqueda exhaustiva: sin `*Deduction/Loan/Prestamo/Embargo/Garnish` transaccional; `procuraduría`/`endeudamiento`/`indebted` = 0 hits). Lo único existente es **configuración estructural sin cuotas ni saldo**: conceptos país `Nature=Egreso` (`-9727…-9736`, incl. `PRESTAMO_BANCARIO`, `EMBARGO`, `CUOTA_ALIMENTICIA`) e instancias `PersonnelFileCompensationConcept` («descuento recurrente **sin saldo**» — D-09 de plazas ingresos/egresos), más el descuento documental opcional de la amonestación (REQ-003, monto manual).
3. **El levantamiento se divide naturalmente en 4 planes** (petición explícita del negocio: «múltiples planes para realizarlo de manera más ordenada»): **REQ-008 descuentos cíclicos** (núcleo con los 3 deltas duros) → **REQ-009 descuentos eventuales** (espejo puntual, contiguo) → **REQ-010 endeudamiento** (parámetros + validación con confirmación + consulta/simulación; engancha a 008/009) → **REQ-011 tiempos no trabajados** (dominio hermano, independiente — puede adelantarse o ir en paralelo). Detalle en «División en planes» y Anexo A.4.
4. **Los deltas duros vs el molde de ingresos son exactamente 4 — y solo el endeudamiento es net-new total**: (a) **segmentos de cuotas** («cuota inicial, cuota final, valor»: el plan del ingreso cíclico es un valor plano único; aquí el plan es una **lista ordenada de tramos** — construible como tabla hija de definición); (b) **interés compuesto** (calculadora de amortización estilo sistema francés con desglose capital/interés — motor puro nuevo con **casos dorados del contador**, precedente de proceso: liquidación); (c) **cuotas extraordinarias** (abonos fuera de secuencia que reducen saldo, con efecto en plazo/cuota a ratificar); (d) **endeudamiento** (parámetros por empresa y por tipo + validación advertir-con-confirmación + consulta/simulación — sin ningún precedente en código).
5. **«Tipos de descuentos» ya existe; «familias» es la única pieza dudosa del bloque de catálogos.** El catálogo pedido de tipos de descuentos (impuestos, ahorro de pensiones, préstamos…) **es el catálogo país de conceptos existente** `compensation-concept-types` con `Nature=Egreso` — ya sembrado con `ISSS`(-9727), `AFP`(-9728, «ahorro de pensiones»), `RENTA`(-9729, «impuestos»), `PRESTAMO_BANCARIO`(-9733), `EMBARGO`(-9734), `CUOTA_ALIMENTICIA`(-9735)… y su clasificación `DeductionClass` (Ley/Interno/Externo) + `Nature` **ya actúa como familia implícita**. Un catálogo explícito de «familias de ingresos y descuentos» **no existe** (verificado: sin columna family/categoría en ninguno de los dos catálogos de conceptos) → P-09 decide si la clasificación existente basta (recomendado F1) o se añade columna aditiva + catálogo nuevo.
6. **«Tipo de descuento cíclico» es catálogo nuevo espejo** del `RECURRING_INCOME_TYPE_CATALOG` de REQ-005: plantilla editable `PRESTAMO_BANCARIO`, `PROCURADURIA`, `COOPERATIVA`, `ASOCIACION`, `OTRO` (los ejemplos literales del levantamiento).
7. **El gancho de liquidación (finiquito) para descuentos ya está soportado por el motor**: el canal de sugerencias produce **líneas de clase `Descuento`** hoy mismo (`DESCUENTO_EXTERNO` con contraparte, `SettlementRepository.cs:144-153`; `ResolveClass` mapea a `Descuento`; `ComputeDeductionLine:563` con rama manual/externa `:621`; neto = ingresos − descuentos `:288`). Propuesta espejo exacto de REQ-005 D-11: acción de liquidación **`DESCONTAR_SALDO`/`CANCELAR`** + concepto de liquidación nuevo **`DESCUENTO_CICLICO_PENDIENTE` con `IsSystemCalculated=false`** (línea manual editable, lección `-9888`/`-9905`; **no** `true` — eso es solo para líneas calculadas por el motor como `-9915`). El «deberá permitir la liquidación» tiene además una segunda lectura cubierta: **liquidar el crédito** (payoff anticipado) = cuota extraordinaria por el saldo → `FINALIZADO` (P-04).
8. **Moneda, frecuencias, tipo de planilla y periodos: reutilización pura, ya sin degradaciones.** `currencies` (mono-moneda de facto: `CompanyPreference.CurrencyCode` USD, motores en USD), `pay-periods` (`-9740…-9743` MENSUAL/QUINCENAL/SEMANAL/UNICA) para las **dos** frecuencias pedidas (cuota y aplicación), `payroll-types` **ya sembrado** (`-9890…-9895`) y `PayrollPeriodDefinition` **ya construido** (REQ-001, FK real usada por REQ-005/006/007) — a diferencia de cuando se analizó REQ-005, ya no hay modo degradado.
9. **Endeudamiento: la única base de ingreso disponible hoy es el salario base por plaza** (`MonthlyBaseSalary` = concepto `IsBaseSalary` de la plaza, `SettlementRepository.cs:89-126`); **no existe** «ingreso mensual total» multi-plaza ni nada de endeudamiento. Propuesta F1: base = **Σ salario base mensualizado de las plazas activas** (derivación nueva pero simple); carga = **Σ cuota mensualizada de descuentos cíclicos `VIGENTE` no estatutarios**; límites = parámetro global + **tabla por tipo** (molde `IncomeTaxWithholdingBracket`, no columnas en `CompanyPreference`); comportamiento = **advertir, nunca bloquear**: el registro con exceso exige **confirmación explícita auditada** (el levantamiento lo exige literalmente). La consulta/simulación es read-only y **no toca planilla**.
10. **Tiempos no trabajados: net-new con todos los insumos ya construidos.** No existe ausencia/permiso genérico (`PERMISO` es solo un código de catálogo `-9479` sin entidad), pero: el **motor de días con exclusiones** existe (incapacidades: scan día a día excluyendo descanso/sábado/asueto según flags del riesgo + descuento `SIN_PAGO = días × salario/30`, `IncapacityCalculation.Rules.cs:128-355`) y los flags del maestro pedido calcan los del `IncapacityRisk` (`CountsSeventhDay/Saturday/Holiday`, `UsesWorkSchedule`); los **asuetos** (`CompanyHoliday`), el **día de descanso por plaza** (`RestDayOfWeek` + fallback empresa) y las **horas-día** (`CompensatoryTimeStandardDailyHours`) existen; y el **chasis «Disponibilidad de tiempo»** (REQ-003) declara textualmente que conectar `PERMISO`/ausencias es aditivo. Las plazas **no** tienen jornada/horario real (`WorkdayCode` es código libre sin horas) → el modo «utiliza jornada» valora horas con la preferencia de empresa. **Marcación biométrica: no existe nada** (solo el flag `AllowsBiometric` de centros de trabajo) → fuera de alcance con costura documentada, tal como lo condiciona el propio levantamiento.
11. **La frontera «sin motor de planilla» se mantiene** (P-01 de REQ-005 ratificada: «el motor se realizará aparte»): todos los planes registran, aplican cuotas/descuentos manual o en lote por periodo y **exportan insumo**; nada escribe `PersonnelFilePayrollTransaction` ni conceptos automáticos. La única excepción calculadora sigue siendo el motor de liquidación (finiquito), al que solo se le **sugieren líneas**.
12. **Espacio de seeds verificado**: piso real en código **`-9915`** (`HORAS_EXTRAS_PENDIENTES_PAGO`); **`-9916…-9999` completamente libre** (verificación con sufijo `L` — la trampa de fragmentos de GUID en `Designer.cs`/snapshot está documentada y se evitó). Propuesta de bloques por plan en A.2: REQ-008 `-9920…-9939` · REQ-009 `-9940…-9949` · REQ-010 `-9950…-9959` · REQ-011 `-9960…-9969`.

### Trazabilidad campo a campo del levantamiento

**Nuevo descuento cíclico (Plan 1 / REQ-008)**

| Campo pedido | Cobertura propuesta |
|---|---|
| Estado | Estado del **flujo** (no digitable): `EN_REVISION → VIGENTE / RECHAZADO` + `SUSPENDIDO` + `FINALIZADO` + `ANULADO` — espejo exacto del ciclo de 6 estados de REQ-005 (D-04) |
| Fecha que entra en vigencia | `effectiveDate` — **puede ser futura** (delta vs REQ-005: aquí el levantamiento pide vigencia, no fecha de registro; el registro queda auditado aparte) (D-04/RN-09) |
| Referencia | Texto libre (200) — la **referencia crediticia** (nº de préstamo/orden); es el ancla de las cuotas extraordinarias (RF-008) |
| Empleado | Expediente (`personnel-files/{publicId}/…`) con **plaza obligatoria** (default: principal — ancla de liquidación e insumo). **Sin centro de costo** (delta razonado vs ingresos: el descuento no es gasto de la empresa — P-08) |
| Tipo de descuento cíclico (catálogo) | Catálogo país editable **nuevo** `RECURRING_DEDUCTION_TYPE_CATALOG` (espejo P-02 de REQ-005): plantilla `PRESTAMO_BANCARIO`, `PROCURADURIA`, `COOPERATIVA`, `ASOCIACION`, `OTRO` (D-03) |
| Institución financiera | Texto libre (200) — precedente `CounterpartyName` del concepto de compensación; maestro por empresa = F2 si se ratifica (P-07) |
| Observación | Notas |
| Cuotas: fecha de inicio de descuento | `installmentStartDate` (primera cuota teórica; retroactiva permitida — espejo P-13 de REQ-005) |
| Cuotas: meses con excepción (catálogo) | Multi-valor `exceptionMonths[]` (1..12): meses en que la cuota **no se aplica** y el plan **se corre** (P-05); sin catálogo BD nuevo recomendado (meses universales) |
| Cuotas: moneda (catálogo) | **Reutiliza** `currencies` (mono-moneda de facto USD, sin FX — D-14) |
| Cuotas: tipo de planilla (catálogo) | **Reutiliza** `payroll-types` (`-9890…-9895`, ya sembrado) |
| Cuotas: frecuencia de cuota (catálogo) | **Reutiliza** `pay-periods` (`MENSUAL`/`QUINCENAL`/`SEMANAL`/`UNICA`) — la frecuencia del **devengo** del plan |
| Cuotas: frecuencia de aplicación (catálogo) | **Reutiliza** `pay-periods` — la frecuencia con que se **descuenta en planilla** (puede dividir la cuota: mensual $100 → 2 quincenales $50) (P-06) |
| Monto y nº de cuotas indefinidas (sí/no) | Flag `isIndefinite` (espejo REQ-005 D-07): indefinido = solo valor + frecuencia, corre hasta cierre/liquidación |
| Detalle de cuotas: cuota inicial / cuota final / frecuencia / valor | **Segmentos del plan** (delta №1): tabla hija de tramos contiguos `fromInstallment–toInstallment → value`; el plan plano de REQ-005 es el caso particular de 1 segmento (D-07) |
| Total cobrado / total no cobrado | **Derivados, nunca persistidos** (espejo D-09 de REQ-005): cobrado = Σ cuotas hijas `APLICADA`; no cobrado = plan − cobrado (RF-006) |
| Permitir la liquidación | Doble cobertura: (a) **liquidación del crédito** = cuota extraordinaria por el saldo → `FINALIZADO` (P-04); (b) **acción al liquidar al empleado** (finiquito): `DESCONTAR_SALDO`/`CANCELAR` → línea de deducción sugerida (D-12, RF-013) |

**Interés compuesto y cuotas extraordinarias (Plan 1 / REQ-008)**

| Campo pedido | Cobertura propuesta |
|---|---|
| Cálculo de interés compuesto | Flag `usesCompoundInterest` + `principalAmount` + `interestRatePercent` + nº de cuotas → **calculadora de amortización** (sistema francés propuesto, P-03): cuota fija derivada + **desglose capital/interés por cuota** (motor puro nuevo, casos dorados del contador — A.3) (D-08) |
| Valor de cuota / monto a capital / monto a interés | Proyección derivada (tabla de amortización) + **snapshot `capitalAmount`/`interestAmount` en cada cuota aplicada** (D-08) |
| Cuota extraordinaria: nº de cuota, fecha, valor | Cuota hija con `kind=EXTRAORDINARIA` (fuera de la secuencia regular), abona a capital/saldo (D-09) |
| Cuota extraordinaria: planilla de aplicación, periodo de planilla | Mismo par `payrollTypeCode` + `payrollPeriodId?`/etiqueta del molde (RN-17 de REQ-005) |
| Efecto de la extraordinaria en el plan | **Reducir plazo** (default propuesto) vs recalcular cuota — P-04 decide; sin interés: acorta el remanente con ajuste de última cuota |

**Descuentos eventuales (Plan 2 / REQ-009)** — espejo 1:1 de REQ-006 (ingresos eventuales), cambiando la naturaleza:

| Campo pedido | Cobertura propuesta |
|---|---|
| Empleado | Expediente + plaza obligatoria (default principal) — espejo REQ-006 D-11 (centro de costo: P-08) |
| Tipo de descuento (catálogo) | **Reutiliza** conceptos país `Nature=Egreso` **no estatutarios** + snapshot (espejo D-03 de REQ-006; `IsStatutory=true` excluido — ISSS/AFP/Renta no se registran a mano) |
| Fecha que será aplicado | Fecha objetivo + par planilla/periodo (espejo D-08 de REQ-006) |
| Monto a descontar | `amount` (>0): directo si fijo; **derivado y validado por el servidor** si por factores (espejo D-07) |
| Valor fijo (sí/no) | Flag `isFixedValue`; si no: `PORCENTAJE_SOBRE_BASE` (% × base digitada) o `CANTIDAD_POR_VALOR` (cantidad × valor × factor) — los dos métodos ya construidos en REQ-006 |
| Factor o porcentaje | Componentes **persistidos** con coherencia validada (422 con desglose) — espejo `OneTimeIncomeRules.ComputeAmount`/`Round2` |
| Moneda (catálogo) | **Reutiliza** `currencies`; default `CompanyPreference.CurrencyCode` (USD) |
| Solicitante | **Trío** `RequesterFilePublicId` + `RequesterNameSnapshot` + `RequestedByUserId` y **anti-autoaprobación TRIPLE** (espejo D-05/D-12 de REQ-006) |
| Planilla en que se procesa / periodo de planilla | **Reutiliza** `payroll-types` + FK real a `payroll_period_definitions` + etiqueta snapshot; re-imputable mientras `AUTORIZADO` (espejo D-08) |

**Catálogos (Planes 1-2)**

| Catálogo pedido | Cobertura propuesta |
|---|---|
| Tipo de descuento cíclico | **Nuevo** `RECURRING_DEDUCTION_TYPE_CATALOG` (país, editable, plantilla con los 4 ejemplos del levantamiento + OTRO) (D-03) |
| Tipos de descuentos | **Ya existe**: `compensation-concept-types` con `Nature=Egreso` (`-9727…-9736`) — verificación de filas + siembra de faltantes (`COOPERATIVA`, `PROCURADURIA` como `Externo`) en PR-1 (D-03) |
| Familias de ingresos y descuentos | **Net-new** (verificado: sin columna de familia en ningún catálogo de conceptos). Propuesta F1: la clasificación existente `Nature` (Ingreso/Egreso) + `DeductionClass` (Ley/Interno/Externo) **actúa como familia**; catálogo explícito + columna aditiva solo si el negocio lo usa (P-09) |

**Tiempos no trabajados (Plan 4 / REQ-011)**

| Campo pedido | Cobertura propuesta |
|---|---|
| Nombre del tipo | Maestro **por empresa** `NotWorkedTimeType` (molde de maestros governed + plantilla, como riesgos de incapacidad) (D-18) |
| Aplica para permiso (sí/no) | Flag `appliesToLeavePermit` — clasifica el tipo como permiso; costura con el futuro módulo de solicitudes (P-17) |
| Utiliza jornada (sí/no) | Flag `usesWorkSchedule`: el registro se captura **en horas** y se valora con horas-día de la empresa (`CompensatoryTimeStandardDailyHours`, null→8) — las plazas no tienen jornada real (verificado) (D-18) |
| Incluye asueto / domingo / sábado (sí/no) | Flags de conteo espejo del `IncapacityRisk` (`CountsHoliday/Saturday` + descanso); el scan de días reutiliza el patrón del motor de incapacidades (D-18) |
| Descuento de séptimo (sí/no) | Flag `deductsSeventhDay`: si hay ausencia computable en la semana, se añade el día de descanso de esa semana al descuento (regla + golden del contador — P-18) |
| Porcentaje de descuento | `discountPercent` (0–100): 0 = con goce (sin descuento); 100 = sin goce pleno (D-18) |
| Tipo de descuento (catálogo) | Concepto país `Nature=Egreso` + snapshot (clasifica el descuento del insumo) |
| Tipo de ingreso (catálogo) | Concepto país `Nature=Ingreso` + snapshot, **opcional** (clasificación del pago con goce — semántica a confirmar, P-17) |
| Ingresar tiempo no trabajado | Registro sobre el expediente: tipo (snapshot de flags), plaza, rango de fechas (u horas si `usesWorkSchedule`), motivo; **cálculo automático**: días/horas computables × salario/30 (÷ horas-día) × % + séptimo (D-18/D-19) |
| Módulo de marcación (biométricos) | **Fuera de alcance** (no existe nada de marcación — verificado); costura documentada: el registro admite origen `MANUAL` hoy / `MARCACION` futuro (D-19) |

**Endeudamiento (Plan 3 / REQ-010)**

| Elemento pedido | Cobertura propuesta |
|---|---|
| % máximo de endeudamiento configurado | Parámetro **global por empresa** (`MaxIndebtednessPercent`) + **tabla por tipo de descuento cíclico** (molde `IncomeTaxWithholdingBracket`: fila por tipo, editable) (D-16) |
| Validación al ingresar el descuento | Al **crear y al autorizar** (P-14): % proyectado = (carga mensualizada + nueva cuota) / base de ingreso; si supera el límite aplicable → **422 con detalle**, salvo confirmación explícita (D-16) |
| «Debe permitir el endeudamiento si se confirma» | Flag de confirmación en el request (`acknowledgeIndebtednessExceeded`) → se registra **con huella auditada** (quién confirmó, % al momento, límite vigente) — advertir, **nunca bloquear** (D-16) |
| Parámetros de aplicación (valor fijo, alcance) | Parámetros de comportamiento: % global, límites por tipo, **alcance de la carga** (qué descuentos cuentan — P-12) y **base de ingreso** (P-11) |
| Consulta de nivel de endeudamiento | GET por empleado: base de ingreso, carga mensualizada desglosada por descuento, % actual, límites aplicables y semáforo (D-17) |
| Simulación | POST **sin persistencia**: ingreso digitado (default: el derivado) + deducción adicional → % simulado + alerta si supera — «solo simulación y no debe afectar la planilla» (literal) (D-17) |

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este levantamiento reutiliza

| Pieza | Dónde | Uso aquí |
|---|---|---|
| **Molde cíclico completo** `PersonnelFileRecurringIncome` + hija `…Installment`: header con tipo/concepto+snapshot/plaza, plan (inicio, moneda, tipo planilla, frecuencia, `isIndefinite`, valor, count/total, acción de liquidación), 6 estados, mutadores custodiados (`Approve/Reject/Suspend/Resume/Annul/CloseManually/FinalizeByPlanCompletion/FinalizeBySettlement/ReopenFromSettlement/ApplyInstallment/AnnulInstallment`), cuota hija = la aplicación por periodo (nº, fecha teórica, monto, FK `PayrollPeriodId?`+etiqueta, origen `MANUAL`/`MOTOR`, `APLICADA`/`ANULADA`), índice único filtrado por (ingreso, nº, activa) | `PersonnelFileRecurringIncome.cs:77-911`; config EF | **Espejo estructural del Plan 1** — el descuento cíclico calca todo y añade segmentos/interés/extraordinarias |
| **Reglas puras del plan** `RecurringIncomeRules`: `Round2`, `NormalizePlan` (deriva count/total), `InstallmentAmountFor` (última ajusta), `BuildProjection` (proyección derivada, nunca persistida), `RemainingAmount`, `CanTransition`, `ValidateSettlementAction` | `RecurringIncomes.Rules.cs:124-470` + suite golden | Base del módulo de reglas del descuento (se extiende con segmentos + amortización) |
| **Molde eventual completo** `PersonnelFileOneTimeIncome` (+ aplicaciones): 5 estados, `isFixedValue`, métodos `CANTIDAD_POR_VALOR`/`PORCENTAJE_SOBRE_BASE` con componentes persistidos y recálculo del servidor, solicitante trío, **anti-self triple**, re-imputación, aplicación única con índice parcial, FK periodo | `PersonnelFileOneTimeIncome.cs`; `OneTimeIncomes.Rules.cs`; REQ-006 | **Espejo estructural del Plan 2** |
| **Endpoints/controllers molde**: CRUD + ciclo (12 rutas), **controller de resolución dedicado** (`AuthorizationPolicySet` class-only), reporting company-wide sin policy-set (gate por handler): `apply-period`, `query`, `export`, `pending-installments/query|export`, `payroll-input/export` | `RecurringIncomesController.cs`, `RecurringIncomeResolutionController.cs`, `RecurringIncomesReportingController.cs` | Receta 1:1 de rutas y permisos para Planes 1-2 |
| **Catálogo país de conceptos** `compensation-concept-types`: `Nature` Ingreso/**Egreso**, `IsStatutory`, `DeductionClass` **Ley/Interno/Externo**, defaults de cálculo/tasas; **Egreso sembrado**: `ISSS`(-9727), `AFP`(-9728), `RENTA`(-9729), `DANO_EQUIPO`(-9730), `ANTICIPO`(-9731), `PRESTAMO_INTERNO`(-9732), `PRESTAMO_BANCARIO`(-9733), `EMBARGO`(-9734), `CUOTA_ALIMENTICIA`(-9735), `OTRO_EXTERNO`(-9736) | `CompensationConceptTypeCatalogItem.cs`; `GlobalCatalogSeedData.cs:1080-1089`; enums `CompensationEnums.cs` | **«Tipos de descuentos» del levantamiento — ya existe**; `Nature`+`DeductionClass` = familia implícita (P-09) |
| **Motor de liquidación — lado deducciones ya operativo**: `ComputeDeductionLine` (ISSS/AFP/RENTA calculadas + rama externa/manual), canal `SuggestedItems` que **ya produce líneas clase `Descuento`** (`DESCUENTO_EXTERNO` con contraparte, últimas cuotas de egresos Externo de la plaza), neto = ingresos − descuentos, conceptos de liquidación clase Descuento `-9839…-9843` | `SettlementCalculation.Rules.cs:563-635,288`; `SettlementRepository.cs:144-153` | Gancho del finiquito para Planes 1-2: saldo/pendientes como **línea de deducción sugerida** (D-12) |
| **Precedente exacto del cierre/reapertura simétrica al liquidar**: emitir → `FinalizeBySettlement` de los `VIGENTE` con acción; anular → `ReopenFromSettlement` (solo los cerrados por ESA liquidación) | `Settlements.Handlers.cs:1027-1031,1160-1164` | Se calca para descuentos (D-12/D-15) |
| Conceptos de liquidación de los hermanos: `INGRESO_CICLICO_PENDIENTE=-9888` e `INGRESO_EVENTUAL_PENDIENTE=-9905` — ambos **`IsSystemCalculated=false`** (línea manual sugerida editable); `-9915` es el contraejemplo `true` (línea calculada) | `GlobalCatalogSeedData.cs:1124,1128,1134` | Los conceptos espejo de descuentos van con **`false`** (D-12) |
| **Periodos de planilla construidos** `PayrollPeriodDefinition` (por empresa: tipo validado contra `pay-periods`, año, número, rango) + FK real ya usada por cuotas/eventuales/horas extras | `Domain/Leave/PayrollPeriodDefinition.cs`; `PayrollPeriodsController.cs` | «Periodo de planilla» de cuotas/eventuales/extraordinarias — sin modo degradado |
| Catálogos transversales: `payroll-types` (`-9890…-9895`), `pay-periods` (`-9740…-9743`), `currencies` (+ `CompanyPreference.CurrencyCode` USD; motores mono-USD) | `GlobalCatalogSeedData.cs:679-684,1139-1142` | Tipo de planilla, las 2 frecuencias y moneda — **cero catálogos nuevos** para estos campos |
| **Parámetros por empresa**: `CompanyPreference` (patrón nullable = default legal resuelto al consumir; setters ricos **por PUT**, PATCH scalar-only) y **parámetros por tipo**: `IncomeTaxWithholdingBracket` (tabla tenant por `PayPeriodCode`+orden, editable con vigencia) | `CompanyPreference.cs`; `IncomeTaxWithholdingBracket.cs` | Moldes del Plan 3: % global (preferencia) + límites por tipo (tabla) (D-16) |
| **Salario base por plaza**: concepto `IsBaseSalary` de la plaza → `MonthlyBaseSalary`; convención `DailySalary = salario/30` en 3 motores | `SettlementRepository.cs:89-126`; `IncapacityCalculation.Rules.cs:121`; `VacationFundDetail.cs:80` | Base de ingreso del endeudamiento (Σ plazas activas = derivación nueva) y valor-día de tiempos no trabajados |
| **Motor de días con exclusiones** (incapacidades): scan día a día excluyendo descanso/sábado/asueto según flags, segmentación por tramos, descuento `SIN_PAGO = días × salario/30`, warnings | `IncapacityCalculation.Rules.cs:128-355` | Precedente directo del cálculo de tiempos no trabajados (D-18) |
| Maestros de calendario y jornada: `CompanyHoliday` (asuetos por empresa, NACIONAL/LOCAL/INSTITUCIONAL), `RestDayOfWeek` en la plaza + fallback `CompanyRestDayOfWeek`→domingo, `CompensatoryTimeStandardDailyHours` (horas-día, null→8) | `CompanyHoliday.cs:22`; `PersonnelFileEmployee.cs:199`; `CompanyPreference.cs:99` | Flags incluye-asueto/domingo/sábado, séptimo y modo horas del Plan 4 |
| **Chasis «Disponibilidad de tiempo»** (REQ-003): vista unificada de indisponibilidad con 2 fuentes (`SUSPENSION`, `FIN_CONTRATO_TEMPORAL`) y comentario explícito: conectar `VACACION/INCAPACIDAD/PERMISO` = «a new repository source method + a new category — the wire contract does not change» | `TimeAvailability.cs:8-23` | El registro de tiempo no trabajado entra como **fuente aditiva** (D-19) |
| Maestros por empresa con plantilla (molde): riesgos/tipos de incapacidad (`LeaveTemplateSeeder`), maestros REQ-003/007 (`CostCenter` governed sin DELETE, template seeders idempotentes + `load-template`) | `LeaveTemplateSeeder.cs`; REQ-007 PR-1 | Molde del maestro de tipos de tiempo no trabajado (D-18) |
| Flujo de una decisión + anti-autoaprobación (doble en REQ-005; **triple** con solicitante en REQ-006/007, molde `RetirementAuthorizerGuards`) + `Authorize*` con `RequireAssertion` **sin Admin** | `PersonnelFilePolicies.cs:283-299`; REQ-006 PR-3 | Flujos de autorización de Planes 1-2 (D-05/D-06) |
| Locks anti-carrera por agregado (`pg_advisory_xact_lock`, class-id ASCII — `RINC` cíclicos) + re-verificación transaccional | `PersonnelFileEmployeeRepository.cs:3740-3749` | Aplicación de cuotas/lotes de descuentos (RN espejo) |
| Bandejas + exports (query paginada `StatusCounts`, xlsx/csv/json OpenXML a mano, rate limiting, filas en español, insumo por periodo cuadrado contra pendientes) | Reporting controllers REQ-005/006/007 | Bandejas, exports e insumos de los 4 planes |
| Convenciones API: `api/v1`, If-Match (faltante→400, obsoleto→409), ETag rotativo, enums string, errores bilingües `extensions.code`, user IDs en response `Guid?` | transversal | Aplican a todos los endpoints nuevos |

### Lo que NO existe (verificado exhaustivamente en `src/`)

- **Ninguna entidad transaccional de descuentos**: sin `RecurringDeduction`/`OneTimeDeduction`/`Loan`/`Prestamo`/amortización/saldos (búsqueda de clases y términos: 0 hits; `cooperativa` solo aparece como catálogo de asociaciones/tipo de empresa; `procuraduría` 0 hits).
- **Ningún interés/amortización**: cero cálculo financiero de cuotas (el único motor monetario es la liquidación; incapacidades/vacaciones usan salario/30).
- **Ningún endeudamiento**: sin parámetros, sin validación, sin consulta (`endeudamiento`/`indebted`/`debt ratio`: 0 hits); **no existe «ingreso mensual total» multi-plaza** (el salario es por plaza).
- **Ningún catálogo de «familias» de conceptos** (sin columna family/categoría en `compensation-concept-types` ni en `settlement-concepts`).
- **Ninguna ausencia/permiso genérico**: `PERMISO` es solo el código `-9479` de `ACTION_TYPE_CATALOG`; el «tiempo no trabajado sin goce» existe solo dentro del motor de incapacidades (`SIN_PAGO`) y la suspensión disciplinaria es documental (deducción **manual opcional**, no calculada).
- **Ninguna jornada real por plaza** (`WorkdayCode` es un código sin horas/días) ni **marcación/biometría** (solo `WorkCenterType.AllowsBiometric`, un flag).
- **Ningún motor de planilla** (frontera ratificada en cadena — se mantiene).
- **Seeds**: piso real `-9915`; libres `-9916…-9999` (y `-9889`, `-9906…-9909` como holguras internas de bloques ya asignados). Trampa documentada: los aparentes IDs en `Migrations/*.Designer.cs`/snapshot son fragmentos de GUID.

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha detectada | Propuesta de resolución |
|---|---|---|
| G-01 | «Descuento cíclico» sin entidad: la configuración existente (`CompensationConcept` Egreso) es estructural, **sin saldo, cuotas, estados ni autorización** | Nueva entidad transaccional espejo `PersonnelFileRecurringDeduction` + cuotas hijas, calcando el molde construido de REQ-005 (D-02); frontera editorial con la configuración documentada (RN-16 espejo) |
| G-02 | El plan de cuotas del molde es **plano** (un solo valor); el levantamiento pide **detalle por tramos** (cuota inicial→final con valor) | **Segmentos de plan** (tabla hija de definición, tramos contiguos 1..N); el plan plano es el caso de 1 segmento; proyección/última-cuota-ajusta se generalizan (D-07) |
| G-03 | **Interés compuesto** inexistente (cero cálculo financiero de amortización) | Motor puro nuevo (calculadora sistema francés propuesto): cuota fija + desglose capital/interés por cuota; **casos dorados del contador como gate** (precedente de proceso: liquidación) (D-08, P-03) |
| G-04 | **Cuotas extraordinarias** sin precedente (abonos fuera de secuencia) | Cuota hija `kind=EXTRAORDINARIA` con par planilla/periodo; abona a saldo/capital; efecto en el plan (reducir plazo vs recalcular cuota) a ratificar (D-09, P-04) |
| G-05 | «Meses con excepción» y «frecuencia de aplicación» sin precedente en el molde | `exceptionMonths[]` (1..12, la cuota se **corre**, no se pierde — P-05) y `applicationFrequencyCode` (pay-periods; división entera de la cuota si difiere de la frecuencia de cuota — P-06) |
| G-06 | «Tipos de descuentos» pedido como catálogo nuevo… | …**ya existe** (`compensation-concept-types` `Nature=Egreso`): solo verificación + siembra de faltantes (`COOPERATIVA`, `PROCURADURIA`) (D-03) |
| G-07 | «Familias de ingresos y descuentos» inexistente como catálogo | F1: la clasificación existente `Nature`+`DeductionClass` actúa como familia; catálogo explícito + columna aditiva **solo si el negocio lo usa** (P-09) |
| G-08 | **Endeudamiento** net-new total (sin parámetros, validación, consulta, ni ingreso total multi-plaza) | Plan 3 dedicado: preferencia global + tabla de límites por tipo (molde brackets) + validación advertir-con-confirmación-auditada + consulta/simulación read-only; base de ingreso = Σ salario base de plazas activas (P-11/P-12) (D-16/D-17) |
| G-09 | «Tiempo no trabajado» genérico inexistente (`PERMISO` es solo un código; la suspensión no calcula) | Plan 4: maestro por empresa con flags (molde `IncapacityRisk` + plantilla) + registro con **cálculo automático** (motor de días espejo incapacidades: exclusiones + % + séptimo + salario/30) (D-18) |
| G-10 | Doble vía potencial: un préstamo puede registrarse como concepto Externo de la plaza (config) o como descuento cíclico (transacción) | Regla editorial espejo RN-16 de REQ-005: la configuración es estructural/indefinida sin saldo; el descuento cíclico es el **crédito con saldo, cuotas y trazabilidad**; guía FE/UI lo comunica; sin bloqueo duro en F1. Sin datos productivos que migrar (P-22 eliminada — data de prueba) |
| G-11 | La jornada real por plaza no existe (para «utiliza jornada») | F1: horas valoradas con `CompensatoryTimeStandardDailyHours` (empresa, null→8); jornada por plaza = evolución futura documentada (D-18) |
| G-12 | Marcación biométrica inexistente (el levantamiento la condiciona: «en caso se adquiera el módulo») | Fuera de alcance; costura documentada: campo `origin` en el registro (`MANUAL` hoy / `MARCACION` futuro) (D-19) |

---

## División en planes (petición explícita del levantamiento)

> El requerimiento agrupa **cuatro dominios funcionales** con dependencias claras. Se propone ejecutarlos como **4 REQs del backlog** (orden = prioridad), cada uno con su plan técnico y PRs propios. Detalle de PRs sugeridos en el Anexo A.4.

| Plan | REQ propuesto | Alcance | Depende de | Tamaño estimado |
|---|---|---|---|---|
| **Plan 1** | **REQ-008 — Planilla: descuentos cíclicos** | Catálogos + entidad con **segmentos de cuotas** + **interés compuesto** (calculadora/amortización) + flujo de autorización + aplicación por periodo (meses de excepción, frecuencia de aplicación) + **cuotas extraordinarias** + historial/totales + bandejas/exports/insumo + integración liquidación (`DESCONTAR_SALDO`/`CANCELAR`) | Nada pendiente (molde REQ-005 en master) | El mayor: ~6 PRs (espejo REQ-005 + 2 PRs de deltas) |
| **Plan 2** | **REQ-009 — Planilla: descuentos eventuales** | Espejo de REQ-006 con `Nature=Egreso`: fijo/factores, solicitante trío, anti-self triple, aplicación/re-imputación, búsqueda + exports + insumo, integración liquidación | Recomendado **contiguo a REQ-008** (amortiza andamiaje); técnicamente independiente | ~5 PRs (espejo REQ-006) |
| **Plan 3** | **REQ-010 — Endeudamiento** | Parámetros (global + por tipo) + **validación con confirmación auditada** en REQ-008/009 + consulta de nivel + **simulación** | **REQ-008** (la carga nace de los cíclicos); engancha también a REQ-009 si está | ~3 PRs |
| **Plan 4** | **REQ-011 — Tiempos no trabajados** | Maestro por empresa (flags + plantilla) + registro con **cálculo de descuento** (motor de días) + bandeja/insumo/export + fuente de «Disponibilidad de tiempo» + costura marcación | Independiente (insumos REQ-001/002 ya en master); puede adelantarse o ir en paralelo | ~4 PRs |

**Orden confirmado (P-01 ratificada):** REQ-008 → REQ-009 → REQ-010 → REQ-011 (REQ-011 adelantable si urge el ausentismo). La validación de endeudamiento **se activa cuando llegue REQ-010**; **P-13 ratificada sin adelanto**: el gap queda **documentado como pendiente para esa iteración** (los descuentos registrados antes no se re-validan retroactivamente — se reflejan en la consulta; anotado en D-16 y en el backlog de REQ-010).

---

## Decisiones — D-01…D-22 (PROPUESTAS — pendientes de ratificación, §17)

> Numeración espejo de REQ-005/006 donde aplica (se marca «espejo»). Las decisiones heredadas de ratificaciones previas del negocio se proponen tal cual; las nuevas (segmentos, interés, extraordinarias, endeudamiento, tiempos) quedan sujetas a las P de §17.

| # | Tema | Decisión propuesta |
|---|---|---|
| D-01 | Fases y planes | Ejecutar como **4 REQs** (REQ-008…REQ-011, tabla anterior) manteniendo el corte **sin motor de planilla** (P-01 de REQ-005 ratificada: registrar + aplicar manual/lote + exportar insumo). F2+ del programa: motor de generación, correlación con ledger, multi-nivel, marcación biométrica, notificaciones |
| D-02 | Modelado (espejo REQ-005 D-02) | **Entidades gemelas del expediente** (no se enriquece `CompensationConcept` ni se retrofitea `Nature` en las tablas de ingresos construidas): `PersonnelFileRecurringDeduction` + segmentos + cuotas hijas; `PersonnelFileOneTimeDeduction` (+ aplicaciones). El plan técnico decide cuánta maquinaria comparte con los hermanos (reglas puras parametrizadas vs duplicadas) |
| D-03 | Catálogos de tipo | (a) «**Tipo de descuento cíclico**» = catálogo país editable **nuevo** `RECURRING_DEDUCTION_TYPE_CATALOG` (plantilla: `PRESTAMO_BANCARIO`, `PROCURADURIA`, `COOPERATIVA`, `ASOCIACION`, `OTRO`); (b) «**Tipos de descuentos**» = **reutiliza** `compensation-concept-types` `Nature=Egreso` activo + snapshot (verificar/sembrar `COOPERATIVA` y `PROCURADURIA` como `DeductionClass=Externo` en PR-1); estatutarios (`IsStatutory`) **excluidos** del registro manual de eventuales; (c) «familias» = clasificación existente `Nature`+`DeductionClass` en F1 (P-09) |
| D-04 | Flujo y estados | **Cíclicos**: espejo exacto REQ-005 — una decisión, 6 estados (`EN_REVISION→VIGENTE/RECHAZADO` + `SUSPENDIDO` + `FINALIZADO` + `ANULADO`), estado nunca digitable; **`effectiveDate` puede ser futura** (las cuotas no se aplican antes de la vigencia). **Eventuales**: espejo exacto REQ-006 — 5 estados (`EN_REVISION→AUTORIZADO/RECHAZADO→APLICADO` + `ANULADO`, `APLICADO` reversible). Autorización confirmable en P-02 |
| D-05 | Anti-autoaprobación | **Cíclicos: doble** (sujeto + registrador — espejo REQ-005 D-05, sin solicitante en el levantamiento); **eventuales: TRIPLE** (sujeto + registrador + **solicitante**, espejo REQ-006 D-05 — el levantamiento pide solicitante) → 403 `SELF_APPROVAL_FORBIDDEN` |
| D-06 | Permisos | Tríos dedicados por módulo (espejo P-14 de REQ-005): `PersonnelFiles.View/Manage/AuthorizeRecurringDeductions`, `…View/Manage/AuthorizeOneTimeDeductions`; Plan 3: `ViewIndebtedness` (consulta/simulación) + `ManageIndebtednessParameters`; Plan 4: `View/ManageNotWorkedTimes` (sin Authorize — P-16). Receta completa: `Authorize*` con `RequireAssertion` **sin Admin**; gates fail-closed; resolución en **controller dedicado**; governance tests |
| D-07 | Plan de cuotas con segmentos | Plan = fecha de inicio + moneda + tipo de planilla + **frecuencia de cuota** + **frecuencia de aplicación** + `exceptionMonths[]` + `isIndefinite` + **segmentos** (tramos contiguos `desde–hasta → valor`; finito: Σ segmentos define count/total; 1 segmento = caso plano). Indefinido: 1 segmento abierto (solo valor). Coherencia validada (tramos contiguos sin huecos/solapes desde 1); **total cobrado / no cobrado derivados** (nunca persistidos); última cuota ajusta el remanente de redondeo |
| D-08 | Interés compuesto | Flag `usesCompoundInterest`: si activo, se capturan `principalAmount` + `interestRatePercent` (**nominal anual ÷ periodos de la frecuencia de cuota — P-03 ratificada**; con **default configurable por empresa**: preferencia nullable nueva —el % depende de cada empresa—, precarga el formulario y la tasa del crédito sigue siendo por registro, editable) + nº de cuotas, y la **calculadora genera el plan** (cuota fija, sistema francés) con **tabla de amortización derivada** (por cuota: valor, capital, interés, saldo); RRHH puede **aceptarla o editarla** (el acreedor manda — la tabla del banco puede diferir; si se edita, el desglose se recalcula proporcional o se digita — P-03). Al **aplicar** cada cuota se snapshotea `capitalAmount`/`interestAmount`. Redondeo `Round2` half-up único; **golden del contador como gate** (A.3) |
| D-09 | Cuotas extraordinarias | Cuota hija `kind=EXTRAORDINARIA` sobre un descuento `VIGENTE`: nº propio (secuencia separada E1, E2…), fecha, valor (>0, ≤ saldo), par planilla/periodo, nota. **Abona a capital/saldo**; efecto en el plan: **reducir plazo** (default propuesto — las últimas cuotas desaparecen; con interés, la tabla se regenera desde el saldo) vs recalcular valor de cuota (P-04). **Liquidar el crédito** = extraordinaria por el saldo total → `FINALIZADO` automático |
| D-10 | Aplicación de cuotas (espejo REQ-005 D-08) | Manual F1 con `ManageRecurringDeductions`: unitaria o **lote por periodo con exclusión** (posponer), bajo **lock advisory** + re-verificación; solo `VIGENTE` con vigencia alcanzada; **meses de excepción se saltan** (el plan se corre — P-05); si frecuencia de aplicación ≠ frecuencia de cuota, la cuota se divide en partes iguales por aplicación (división entera Round2, última parte ajusta — P-06); origen `MANUAL`/`MOTOR` |
| D-11 | Historial y totales (espejo REQ-005 D-09) | Cuota aplicada persistida (nº, fecha, teórica, monto, desglose capital/interés si aplica, moneda, planilla, FK periodo + etiqueta, origen, quién) — inmutable salvo **anulación con motivo** (reintegra saldo; puede reabrir `FINALIZADO→VIGENTE`). Historial = aplicadas + extraordinarias + proyección + derivados (**total cobrado / total no cobrado**, saldo, próxima cuota) |
| D-12 | Acción de liquidación (espejo REQ-005 D-11) | Catálogo país nuevo `RECURRING_DEDUCTION_SETTLEMENT_ACTION_CATALOG`: **`DESCONTAR_SALDO`** (el saldo pendiente se sugiere como **línea de deducción** del finiquito — canal `SuggestedItems` clase `Descuento`, editable, concepto nuevo **`DESCUENTO_CICLICO_PENDIENTE` `IsSystemCalculated=false`**, `Affects*=false`) y **`CANCELAR`** (condonación: sin línea; `FINALIZADO` al emitir). Indefinidos → `CANCELAR` forzado (espejo P-06 REQ-005). Emitir/anular liquidación = cierre/reapertura simétrica (molde verificado). Eventuales: los `AUTORIZADO` no aplicados se sugieren con **`DESCUENTO_EVENTUAL_PENDIENTE`** (espejo D-15 REQ-006; al emitir pasan a `APLICADO` origen `LIQUIDACION`) |
| D-13 | Plaza sin centro de costo (delta razonado) | **Plaza obligatoria** (default: principal — ancla de liquidación e insumo) pero **sin centro de costo** en ambos módulos de descuentos: el descuento no es gasto de la empresa sino retención al empleado (los ingresos sí lo llevan por imputación contable). Confirmación en P-08 |
| D-14 | Moneda (espejo) | Reutilización pura `currencies` + default `CompanyPreference.CurrencyCode` (USD); mono-moneda de facto, sin FX; totales por moneda. El endeudamiento calcula en la moneda de la empresa |
| D-15 | Retiro y liquidación | Perfil `RETIRADO`: sin altas; pendientes `EN_REVISION` solo rechazables/anulables; `VIGENTE`/`AUTORIZADO` se resuelven al liquidar según D-12. Anular la liquidación emitida reabre los cerrados por ella (espejo verificado) |
| D-16 | Endeudamiento — parámetros y validación | **Parámetros**: `MaxIndebtednessPercent` global (preferencia por empresa, nullable = sin control) + **tabla por tipo de descuento cíclico** `IndebtednessLimit` (tenant, tipo → % límite; molde brackets; el por-tipo **prevalece** sobre el global para descuentos de ese tipo). **Validación** (al crear **y** al autorizar — P-14): % proyectado = (carga mensualizada actual + cuota mensualizada nueva) ÷ base de ingreso; si supera el límite aplicable → **422 `INDEBTEDNESS_LIMIT_EXCEEDED`** con desglose, **salvo** `acknowledgeIndebtednessExceeded=true` → procede y **persiste la huella** (quién confirmó, cuándo, % y límite al momento). **Nunca bloquea** en firme (literal del levantamiento). Base F1 = **Σ salario base mensual de plazas activas**; carga F1 = **Σ cuota mensualizada de descuentos cíclicos `VIGENTE` no estatutarios** (P-11/P-12 ratificadas: bruto, semanal ×4.33; suspendidos visibles pero excluidos). **Gap documentado (P-13 ratificada)**: los descuentos registrados antes de REQ-010 **no se re-validan retroactivamente** — solo se reflejan en la consulta; sin chequeo adelantado en REQ-008/009 |
| D-17 | Endeudamiento — consulta y simulación | **Consulta** (GET, `ViewIndebtedness`): base derivada, carga desglosada por descuento (tipo, institución, cuota mensualizada), % actual, límites aplicables, semáforo. **Simulación** (POST **sin persistencia**): ingreso digitado (default el derivado) + deducción adicional (y opcionalmente tipo) → % simulado + alerta si supera — «solo simulación, no afecta la planilla» (literal). Sin efecto en datos |
| D-18 | Tiempos no trabajados — maestro y cálculo | Maestro **por empresa** `NotWorkedTimeType` (governed sin DELETE + **plantilla** `load-template`: `AUSENCIA_SIN_GOCE` 100%, `AUSENCIA_CON_GOCE` 0%, `SUSPENSION_CON_DESCUENTO` 100%, `LLEGADA_TARDIA` 100% con jornada — editable): flags `appliesToLeavePermit`, `usesWorkSchedule`, `includesHoliday/Sunday/Saturday`, `deductsSeventhDay`, `discountPercent` 0–100, concepto egreso + concepto ingreso opcional (snapshots). **Registro** con **cálculo automático** (motor puro espejo incapacidades): días computables (scan con exclusiones según flags) — u **horas** si `usesWorkSchedule` (valoradas con horas-día de la empresa) — × `salario/30` × % + **séptimo** (si `deductsSeventhDay` y hay ausencia computable en la semana: +1 día de descanso de esa semana — P-18); desglose persistido como snapshot (molde `ApplyCalculation` de incapacidad) |
| D-19 | Tiempos no trabajados — ciclo, disponibilidad y costuras | Ciclo simple sin autorización F1 (molde incapacidad): `REGISTRADA → ANULADA` (motivo obligatorio) — P-16 puede añadir decisión; par planilla/periodo declarado + bandeja/insumo por periodo (el descuento calculado viaja como insumo, **no** genera automáticamente un descuento eventual — P-19); **fuente nueva de «Disponibilidad de tiempo»** (categoría `TIEMPO_NO_TRABAJADO`, contrato aditivo verificado); **asiento** en el journal (hecho laboral, espejo suspensión: amount null + rango — P-20 decide tipo `-9479 PERMISO` existente vs nuevo); origen `MANUAL` hoy / `MARCACION` futuro (G-12) |
| D-20 | Catálogos y seeds | Bloques nuevos (piso verificado `-9915`; todo `HasData` idempotente; verificación de IDs libres al abrir cada PR-1): **REQ-008** `-9920…-9939` (estados `-9920…-9925`, acciones `-9926/-9927`, concepto liquidación `-9928`, tipos cíclicos `-9930…-9934`, holgura resto) · **REQ-009** `-9940…-9949` (estados `-9940…-9944`, concepto `-9945`) · **REQ-010** `-9950…-9959` (holgura/familias si P-09 ratifica) · **REQ-011** `-9960…-9969` (estados `-9960/-9961`, ActionType `-9965` si P-20 elige nuevo). Detalle A.2 |
| D-21 | Secuenciación | REQ-008 → REQ-009 (contiguo) → REQ-010 → REQ-011 (adelantable/paralelo). Cada plan con análisis ratificado (este documento, §17) + plan técnico propio + PRs con suite verde antes de avanzar (protocolo del backlog) |
| D-22 | Asientos de expediente | **Descuentos (Planes 1-2): sin asientos** en el journal (dato de compensación — espejo D-18 de REQ-005/006). **Tiempos no trabajados (Plan 4): SÍ asienta** (hecho del expediente laboral con fechas, como la suspensión de REQ-003) — P-20 |

---

## 1. Resumen del producto o requerimiento

Se construirá el **lado egresos del módulo de planilla** de CLARIHR, en 4 planes:

1. **Descuentos cíclicos (REQ-008)**: registro sobre el expediente de los descuentos recurrentes no estatutarios del empleado (préstamos bancarios, órdenes de la procuraduría, cooperativas, asociaciones…) con **plan de cuotas por segmentos** (tramos cuota inicial–final con valor), **cálculo opcional de interés compuesto** (cuota fija con desglose capital/interés), **meses de excepción**, **frecuencias de cuota y de aplicación**, flujo de **autorización**, **aplicación por periodo** (unitaria/lote), **cuotas extraordinarias** (abonos que reducen el saldo, incluida la **liquidación anticipada del crédito**), historial con **total cobrado/no cobrado**, bandejas/exportaciones (incluido el **insumo de planilla por periodo**) y resolución al **liquidar al empleado** (descontar el saldo en el finiquito o cancelar).
2. **Descuentos eventuales (REQ-009)**: descuentos de una sola vez (o de periodos imprevistos) con **valor fijo o calculado por factor/porcentaje**, **solicitante** con anti-autoaprobación triple, imputación a **planilla + periodo**, aplicación/re-imputación, búsqueda corporativa y exportaciones — espejo exacto del módulo de ingresos eventuales ya construido.
3. **Endeudamiento (REQ-010)**: **parámetros** de % máximo (global por empresa + por tipo de descuento cíclico), **validación al registrar/autorizar** descuentos que **advierte** cuando se supera el límite y **permite continuar solo con confirmación explícita auditada**, y **consulta del nivel de endeudamiento** por empleado con **simulación** (ingreso + deducción hipotética → % proyectado y alerta) sin ningún efecto en planilla.
4. **Tiempos no trabajados (REQ-011)**: **catálogo por empresa de tipos de tiempo no trabajado** (con goce/sin goce, % de descuento, uso de jornada, inclusión de asueto/sábado/domingo, descuento de séptimo, conceptos de descuento/ingreso) y **registro por empleado con cálculo automático del descuento** (días u horas computables × salario/30 × % + séptimo), alimentando la bandeja/insumo de planilla y la vista de **disponibilidad de tiempo**; la integración con marcación biométrica queda como costura futura (el propio levantamiento la condiciona a adquirir ese módulo).

**Problema que resuelve.** Hoy los descuentos recurrentes solo pueden modelarse como configuración estructural **sin saldo ni cuotas** (no se sabe cuánto se ha retenido, cuánto falta, ni cuándo termina un préstamo), los descuentos puntuales y las ausencias se gestionan fuera del sistema, y no existe **ningún control de endeudamiento** al aceptar nuevas órdenes de descuento — con el riesgo legal/financiero de sobre-retener el salario. Este levantamiento cierra el ciclo completo del lado egresos con la misma disciplina ya probada en el lado ingresos.

---

## 2. Objetivos del negocio

1. **Trazabilidad total de cada crédito/orden de descuento**: referencia, institución, plan de cuotas, interés, abonos extraordinarios, saldo exacto y constancia de qué se retuvo en qué planilla — hoy inexistente.
2. **Debido proceso**: ningún descuento entra en vigencia sin autorización de un tercero facultado (anti-autoaprobación), con auditoría completa; las órdenes judiciales (procuraduría, cuota alimenticia) quedan documentadas con su referencia.
3. **Protección del salario y cumplimiento**: control configurable del **% de endeudamiento** (global y por tipo) con advertencia y confirmación consciente — sin bloquear la operación cuando el negocio decide proceder.
4. **Insumo exacto para la nómina externa**: exportación por periodo de las cuotas/descuentos a aplicar (con institución y referencia), eliminando transcripciones; el mismo modelo quedará listo para el futuro motor interno.
5. **Cierre limpio al liquidar**: el saldo de los créditos se descuenta del finiquito (o se condona) automáticamente como línea sugerida, con reversión simétrica si la liquidación se anula.
6. **Gestión de ausentismo con efecto económico**: registrar tiempos no trabajados con su descuento calculado (no digitado), alimentando disponibilidad de tiempo, planilla y — a futuro — la conciliación con marcación.
7. **Cerrar el mapa del programa de planilla**: con este levantamiento, de la visión original solo queda pendiente el motor de generación (levantamiento propio ya acordado).

---

## 3. Alcance funcional

### Plan 1 — REQ-008 Descuentos cíclicos
- **Configuración**: catálogo de estados; catálogo de acciones de liquidación (`DESCONTAR_SALDO`/`CANCELAR`); catálogo de **tipos de descuento cíclico** (plantilla editable); verificación/siembra de conceptos `Nature=Egreso` faltantes (`COOPERATIVA`, `PROCURADURIA`); concepto de liquidación `DESCUENTO_CICLICO_PENDIENTE`; 3 permisos RBAC.
- **Descuentos cíclicos**: crear/editar (`EN_REVISION`) con vigencia (futura permitida), institución financiera, plan por **segmentos**, meses de excepción, frecuencias de cuota/aplicación, indefinido; decidir (autorizar/rechazar); suspender/reanudar; revocar/anular; finalización automática; cierre manual.
- **Interés compuesto**: calculadora de amortización (cuota fija + desglose capital/interés + tabla derivada), plan editable, snapshot del desglose al aplicar.
- **Cuotas**: proyección derivada; aplicación unitaria y en lote por periodo con exclusión; anulación con motivo; **cuotas extraordinarias** (incluida liquidación anticipada del crédito).
- **Historial y totales**: cobrado/no cobrado, saldo, próxima cuota; por descuento y por empleado.
- **Bandejas/exports**: bandeja por empresa con `StatusCounts`; cuotas pendientes/vencidas; exports xlsx/csv/json; **insumo de planilla por periodo**.
- **Integración con liquidación**: línea de deducción sugerida por saldo / condonación; cierre y reapertura simétricos.

### Plan 2 — REQ-009 Descuentos eventuales
- Catálogo de estados propio; registro con valor fijo o **por factores** (componentes persistidos, recálculo del servidor); solicitante (trío) + **anti-self triple**; decidir/revocar; aplicar (unitaria/lote con exclusión), **re-imputar** («enviar a otro periodo»); reversión de aplicación; búsqueda corporativa con filtros + `StatusCounts` + totales por moneda; exports + insumo; integración con liquidación (`DESCUENTO_EVENTUAL_PENDIENTE`).

### Plan 3 — REQ-010 Endeudamiento
- **Parámetros**: % máximo global (preferencia por empresa) + límites **por tipo de descuento cíclico** (tabla editable).
- **Validación** en REQ-008/009: advertencia 422 con desglose al superar el límite aplicable; **override con confirmación auditada**.
- **Consulta**: nivel actual por empleado (base, carga desglosada, %, límites, semáforo).
- **Simulación**: escenario hipotético sin persistencia ni efecto en planilla.

### Plan 4 — REQ-011 Tiempos no trabajados
- **Maestro por empresa** de tipos (flags de conteo + % + conceptos) con plantilla y `load-template`.
- **Registro** por empleado con **cálculo automático** del descuento (días u horas, exclusiones, séptimo) y desglose persistido.
- Ciclo `REGISTRADA→ANULADA`; par planilla/periodo; bandeja/insumo/export.
- **Fuente nueva de «Disponibilidad de tiempo»** y **asiento** en el journal del expediente.
- Costura documentada con el futuro módulo de marcación (origen del registro).

### Fase 2+ (fuera de estos 4 planes — mapa A.1)
- Motor de generación de planilla (aplica cuotas con origen `MOTOR` sobre el mismo modelo); correlación con el ledger externo; autorización multi-nivel; notificaciones; maestro de instituciones financieras; jornada real por plaza; módulo de marcación biométrica; solicitudes de permiso del empleado (autoservicio) conectadas a tiempos no trabajados.

---

## 4. Fuera de alcance

- **Generación/cálculo de planilla** (motor): frontera ratificada en cadena — estos planes registran, aplican y exportan insumo; no calculan nómina ni escriben ledgers (`PersonnelFilePayrollTransaction` intacto).
- **Marcación biométrica**: no existe módulo de marcación; el registro de tiempos no trabajados es manual con costura documentada (literal del levantamiento: «en caso se adquiera el módulo…»).
- **Cobro/pago real a las instituciones** (remesas a bancos/procuraduría): CLARIHR registra y exporta el insumo; la dispersión es del proceso externo.
- **Conversión de monedas / FX** (mono-moneda de facto USD; totales por moneda).
- **Autoservicio del empleado** en F1 (espejo P-11 de REQ-005; consulta self de endeudamiento evaluable en F2 — P-15).
- **Motor de aprobaciones multi-nivel**; **notificaciones**; **importador masivo** (sin datos productivos que importar — P-22 eliminada; la retroactividad del flujo normal cubre cualquier regularización).
- **Maestro de instituciones financieras** (texto libre F1 — P-07).
- **Jornada/horario real por plaza** (las horas se valoran con la preferencia de empresa — G-11).
- **Retrofit de `Nature` en las tablas de ingresos construidas** (entidades gemelas — D-02).
- **Validación retroactiva de endeudamiento** sobre descuentos ya vigentes al llegar REQ-010 (se reflejan en la consulta, no se re-validan).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador de empresa** | Mantiene catálogos/maestros editables, parámetros de endeudamiento, asigna permisos |
| **Gestor de RRHH / Analista de planilla** (`Manage*`) | Registra descuentos cíclicos/eventuales y tiempos no trabajados, aplica cuotas/lotes, registra extraordinarias, pospone, anula con motivo, exporta insumos; **confirma** el override de endeudamiento |
| **Autorizador** (`Authorize*`) | Decide (autoriza/rechaza) descuentos `EN_REVISION`; revoca vigentes; sujeto a anti-autoaprobación (doble/triple); **Admin no decide** sin grant explícito |
| **Solicitante** (descuentos eventuales) | Expediente que pide el descuento (jefatura/área); participa en la anti-autoaprobación triple |
| **Consulta / Auditor** (`View*`) | Solo lectura de fichas, historiales, amortizaciones, bandejas, endeudamiento y exports |
| **Analista de endeudamiento / créditos** (`ViewIndebtedness`) | Consulta niveles y **simula** escenarios antes de aceptar órdenes de descuento |
| **Finanzas / Planilla externa** | Consume el insumo por periodo (cuotas y descuentos con institución/referencia) |
| **Instituciones externas** (banco, procuraduría, cooperativa) | Origen de las órdenes/referencias crediticias; **no** interactúan con el sistema (registro documental) |
| **Motor de liquidación (interno)** | Consume la acción de liquidación: sugiere el saldo como línea de deducción del finiquito o dispara la condonación |
| **Empleado** | Sin acceso en F1 (dato de compensación); sujeto de las reglas anti-autoaprobación |

---

## 6. Requerimientos funcionales

> Agrupados por plan. Prioridades: Alta = imprescindible del plan; Media = deseable/recortable a segunda entrega.

### Plan 1 (REQ-008) — Descuentos cíclicos

### RF-001 - Configuración, catálogos y permisos del módulo cíclico

**Descripción:** Sembrar el catálogo de estados (`EN_REVISION`, `VIGENTE`, `RECHAZADO`, `SUSPENDIDO`, `FINALIZADO`, `ANULADO`), el de acciones de liquidación (`DESCONTAR_SALDO`, `CANCELAR`) y el de **tipos de descuento cíclico** (plantilla editable: `PRESTAMO_BANCARIO`, `PROCURADURIA`, `COOPERATIVA`, `ASOCIACION`, `OTRO`); sembrar el concepto de liquidación `DESCUENTO_CICLICO_PENDIENTE` (clase Descuento, `IsSystemCalculated=false`); verificar/sembrar los conceptos país `Nature=Egreso` faltantes (`COOPERATIVA`, `PROCURADURIA` como `Externo`); declarar los 3 permisos con la receta completa (D-06).

**Reglas de negocio:**
- Patrón híbrido (constantes canónicas + catálogo país editable); IDs en bloque `-9920…-9939` verificados libres al abrir el PR (piso actual `-9915`; trampa GUID documentada).
- `AuthorizeRecurringDeductions` excluye Admin; gates fail-closed + governance tests.

**Criterios de aceptación:**
- Migración `HasData` idempotente; usuario sin permiso → 403; Admin sin `Authorize*` no decide.

**Prioridad:** Alta
**Dependencias:** Verificación de seeds; D-03/D-04/D-05/D-06/D-20.

### RF-002 - Crear y editar descuento cíclico (con plan por segmentos)

**Descripción:** Alta por RRHH sobre el expediente: **fecha de vigencia** (futura permitida), referencia crediticia, tipo de descuento cíclico (catálogo), **tipo de descuento** (concepto país `Nature=Egreso` no estatutario, con snapshot), institución financiera (texto), plaza (obligatoria, default principal), observaciones; bloque de cuotas: fecha de inicio, **meses con excepción**, moneda, tipo de planilla, **frecuencia de cuota**, **frecuencia de aplicación**, `isIndefinite`, y **segmentos** (cuota inicial–final → valor) o parámetros de interés (RF-003). Nace `EN_REVISION`; editable solo en revisión.

**Reglas de negocio:**
- Segmentos contiguos desde 1, sin huecos ni solapes, valores > 0; finito: el total y el nº de cuotas se derivan de los segmentos; indefinido: un solo segmento abierto (RN-05).
- Concepto egreso activo no estatutario (ISSS/AFP/Renta → 422); catálogos activos; If-Match; perfil `RETIRADO` → 422.
- El estado nunca se digita (RN-01); fecha de inicio de cuotas libre (retroactiva permitida — regularización).

**Criterios de aceptación:**
- POST → 201 `EN_REVISION` con plan proyectado inmediato; segmentos con hueco (1–6, 8–12) → 422 con detalle; concepto `ISSS` → 422.

**Prioridad:** Alta
**Dependencias:** RF-001.

### RF-003 - Cálculo de interés compuesto (calculadora de amortización)

**Descripción:** Para descuentos con `usesCompoundInterest`: capturar monto principal, tasa de interés (% — base de capitalización según P-03) y nº de cuotas; el sistema **calcula la cuota fija** (sistema francés) y expone la **tabla de amortización** (por cuota: valor, monto a capital, monto a interés, saldo). RRHH puede aceptar el plan calculado o **ajustarlo** a la tabla del acreedor; el desglose de cada cuota se snapshotea al aplicarla.

**Reglas de negocio:**
- Redondeo `Round2` half-up único; la última cuota ajusta el residuo (capital restante exacto).
- Interés y segmentos manuales son **excluyentes** en la captura (el interés genera el plan); editar el plan calculado re-deriva el desglose (P-03).
- **Casos dorados del contador como gate** de la suite (A.3) — precedente liquidación/incapacidades.

**Criterios de aceptación:**
- $1,000 al 12% nominal anual, 12 cuotas mensuales → cuota $88.85; cuota 1 = $10.00 interés + $78.85 capital; Σ capital = $1,000.00 exacto tras ajuste final.

**Prioridad:** Alta
**Dependencias:** RF-002.

### RF-004 - Decidir descuento cíclico (autorizar / rechazar)

**Descripción:** PATCH de decisión sobre `EN_REVISION` (controller de resolución dedicado): autorizar → `VIGENTE` (cuotas aplicables desde la vigencia) o rechazar (motivo obligatorio).

**Reglas de negocio:**
- Solo `AuthorizeRecurringDeductions` (excluye Admin); **anti-autoaprobación doble** (sujeto/registrador) → 403; re-verificación de estado en transacción.
- Si REQ-010 está construido: la decisión re-valida el endeudamiento (RF-021).

**Criterios de aceptación:**
- Decisión por el registrador → 403; rechazo sin motivo → 422; autorizar → `VIGENTE`.

**Prioridad:** Alta
**Dependencias:** RF-002.

### RF-005 - Ciclo post-autorización (suspender, revocar, finalizar, liquidar crédito)

**Descripción:** Suspensión/reanudación (pausa de aplicabilidad); anulación desde `EN_REVISION`; revocación desde `VIGENTE` (cuotas aplicadas intactas); **finalización automática** al saldarse el plan (última cuota o extraordinaria de payoff); cierre manual del indefinido; **liquidación anticipada del crédito** = cuota extraordinaria por el saldo total (RF-008).

**Reglas de negocio:**
- Motivos obligatorios; baja lógica siempre; `SUSPENDIDO` bloquea aplicación sin alterar plan; terminales: `RECHAZADO`/`ANULADO`/`FINALIZADO` (anulación de cuota puede reabrir `FINALIZADO→VIGENTE`).

**Criterios de aceptación:**
- Extraordinaria por el saldo total → `FINALIZADO` automático en la misma transacción; revocar con 3 cuotas aplicadas → `ANULADO`, historial intacto, pendientes fuera de bandejas/insumos.

**Prioridad:** Alta
**Dependencias:** RF-004.

### RF-006 - Plan proyectado, amortización y totales (cobrado / no cobrado)

**Descripción:** GET del plan del descuento: cuotas aplicadas + extraordinarias (persistidas) + **proyección** de pendientes (fechas teóricas por frecuencia de aplicación, saltando meses de excepción; desglose capital/interés si aplica) + derivados: **total cobrado, total no cobrado**, saldo (capital restante si interés), próxima cuota.

**Reglas de negocio:**
- Proyección **calculada, nunca persistida** (espejo D-07 REQ-005); recalcula sola ante anulaciones/extraordinarias/cambios de estado.
- Totales excluyen cuotas `ANULADA`.

**Criterios de aceptación:**
- Plan 12×$88.85 con 3 aplicadas y 1 extraordinaria de $200 → proyección regenerada desde el saldo (plazo reducido), cobrado = Σ 4 filas, no cobrado = plan restante exacto.

**Prioridad:** Alta
**Dependencias:** RF-002/RF-003.

### RF-007 - Aplicar cuotas (unitaria y en lote por periodo)

**Descripción:** (a) Unitaria: registrar la retención de la siguiente cuota de un `VIGENTE` con fecha, par planilla/periodo (FK real + etiqueta) y nota; (b) **lote por periodo**: tipo de planilla + periodo → cuotas que corresponden (por frecuencia de **aplicación**, vigencia alcanzada, mes no exceptuado), con **exclusión = posponer**.

**Reglas de negocio:**
- Solo `VIGENTE` con vigencia alcanzada; **lock advisory** + re-verificación (secuencia sin saltos/duplicados; no superar plan finito); meses de excepción se saltan y el plan se corre (P-05); si frecuencia de aplicación ≠ cuota, división en partes iguales con ajuste (P-06); última cuota finaliza en la misma transacción; origen `MANUAL` (F1).
- Snapshot al aplicar: monto (y capital/interés si aplica), moneda, planilla, periodo, teórica.

**Criterios de aceptación:**
- Lote quincenal con cuota mensual $100 → 2 aplicaciones de $50; mes exceptuado (diciembre) → sin cuota en ese periodo y plan corrido; doble submit concurrente → sin duplicados (test de carrera).

**Prioridad:** Alta
**Dependencias:** RF-004, RF-006.

### RF-008 - Cuotas extraordinarias (abonos y payoff)

**Descripción:** Registrar sobre un descuento `VIGENTE` una cuota **extraordinaria**: nº propio (E1, E2…), fecha, valor (> 0, ≤ saldo), par planilla/periodo de aplicación, nota. Reduce el saldo/capital; con interés, la amortización se **regenera desde el saldo** (default: reducir plazo — P-04). El payoff total finaliza el descuento (RF-005).

**Reglas de negocio:**
- Solo `VIGENTE` (o `SUSPENDIDO` si se ratifica — P-04); valor > saldo → 422; anulable con motivo (reabre saldo y proyección).
- Misma maquinaria de aplicación (lock, snapshot, origen); visible en el historial marcada como extraordinaria.

**Criterios de aceptación:**
- Extraordinaria de $200 sobre saldo $700 → saldo $500 y plazo reducido; extraordinaria de $500 restantes → `FINALIZADO`; anularla → reabre a `VIGENTE` con saldo $500.

**Prioridad:** Alta
**Dependencias:** RF-005/RF-006/RF-007.

### RF-009 - Anular cuota aplicada

**Descripción:** Anulación con motivo de una cuota (regular o extraordinaria) aplicada por error → `ANULADA` (sin borrado); saldo, totales y proyección la reintegran; puede reabrir un `FINALIZADO`.

**Reglas de negocio:** espejo exacto de REQ-005 RF-008 (motivo obligatorio, trazable, re-aplicable).

**Criterios de aceptación:**
- Anular la última cuota de un `FINALIZADO` → vuelve a `VIGENTE` con saldo de 1 cuota.

**Prioridad:** Alta
**Dependencias:** RF-007/RF-008.

### RF-010 - Historial de retención del descuento cíclico

**Descripción:** Consulta por descuento: serie de cuotas aplicadas/extraordinarias (nº, fecha, teórica, monto, capital/interés, planilla, periodo, origen, quién, estado) + proyección + derivados (cobrado/no cobrado/saldo). Paginada y exportable.

**Prioridad:** Alta
**Dependencias:** RF-007.

### RF-011 - Bandejas (descuentos y cuotas pendientes/vencidas)

**Descripción:** (a) Bandeja por empresa de descuentos (filtros: estado, tipo cíclico, concepto, institución, empleado, fechas; `StatusCounts`; totales por moneda); (b) bandeja de **cuotas pendientes/vencidas** por periodo/tipo de planilla (lista de trabajo del analista; desde ella se lanza el lote RF-007).

**Prioridad:** Alta
**Dependencias:** RF-006/RF-007.

### RF-012 - Exportaciones + insumo de planilla por periodo

**Descripción:** Exports xlsx/csv/json de las bandejas; **insumo por periodo** (tipo de planilla + periodo → cuotas a retener con empleado, plaza, concepto snapshot, tipo cíclico, institución, referencia, nº de cuota, monto, capital/interés, moneda) — puente con la nómina externa.

**Reglas de negocio:** rate limiting/límite síncrono existentes; filas en español; el insumo cuadra contra la bandeja de pendientes del mismo filtro; excluye suspendidos/anulados/no vigentes.

**Prioridad:** Alta
**Dependencias:** RF-011.

### RF-013 - Integración con liquidación del empleado (finiquito)

**Descripción:** Al preparar la liquidación: los descuentos cíclicos `VIGENTE` con acción **`DESCONTAR_SALDO`** sugieren su **saldo** como **línea de deducción** (concepto `DESCUENTO_CICLICO_PENDIENTE`, editable/excluible, con referencia/institución en la descripción); con **`CANCELAR`** no sugieren y quedan `FINALIZADO` al emitir (condonación). Anular la liquidación reabre los cerrados por ella.

**Reglas de negocio:**
- Solo plaza principal (espejo REQ-005); indefinidos → `CANCELAR` forzado (sin saldo definible) → 422 si se intenta `DESCONTAR_SALDO`; el neto del finiquito absorbe la deducción (motor existente: neto = ingresos − descuentos, warning si neto < 0).
- `IsSystemCalculated=false` (línea manual sugerida — lección `-9888`/`-9905`); nada escribe ledgers.

**Criterios de aceptación:**
- Liquidar con saldo $300 y `DESCONTAR_SALDO` → línea de deducción $300 editable; con `CANCELAR` → sin línea y `FINALIZADO` al emitir; anular la liquidación → reapertura a `VIGENTE`; neto negativo → warning del motor visible.

**Prioridad:** Media (recortable a segunda entrega del plan sin bloquear el resto)
**Dependencias:** RF-004; motor de liquidación (en master).

### Plan 2 (REQ-009) — Descuentos eventuales

### RF-014 - Configuración del módulo eventual

**Descripción:** Catálogo de estados (`EN_REVISION`, `AUTORIZADO`, `RECHAZADO`, `APLICADO`, `ANULADO`), concepto de liquidación `DESCUENTO_EVENTUAL_PENDIENTE` (`IsSystemCalculated=false`, clase Descuento) y 3 permisos (`View/Manage/AuthorizeOneTimeDeductions`) — espejo exacto de REQ-006 PR-1. Seeds en bloque `-9940…-9949`.

**Prioridad:** Alta
**Dependencias:** D-06/D-20 (independiente de REQ-008; comparte solo conceptos país).

### RF-015 - Crear descuento eventual (valor fijo o por factores)

**Descripción:** Registro sobre el expediente: empleado (plaza obligatoria, default principal), **tipo de descuento** (concepto país `Nature=Egreso` no estatutario + snapshot), fecha, **valor fijo (sí/no)** — si no: `PORCENTAJE_SOBRE_BASE` (% × base digitada) o `CANTIDAD_POR_VALOR` (cantidad × valor × factor) con componentes persistidos y **recálculo del servidor** (422 con desglose si no cuadra) —, monto, moneda, **solicitante** (trío), par **planilla + periodo** (FK real + etiqueta). Nace `EN_REVISION`.

**Reglas de negocio:** espejo exacto de REQ-006 RF-002/003 (amount opcional para computados — el servidor deriva; `IsStatutory` y `IsBaseSalary` → 422; `Round2` half-up).

**Criterios de aceptación:**
- 15% × base $200 → $30.00 derivado; mismatch enviado → 422 con desglose; concepto `AFP` → 422.

**Prioridad:** Alta
**Dependencias:** RF-014.

### RF-016 - Decidir, revocar y re-imputar

**Descripción:** Resolución en controller dedicado (autorizar/rechazar con motivo) con **anti-autoaprobación TRIPLE** (sujeto, registrador, **solicitante**); revocación de `AUTORIZADO`; **re-imputación de periodo** («enviar a otro periodo») mientras `AUTORIZADO`.

**Prioridad:** Alta
**Dependencias:** RF-015.

### RF-017 - Aplicar al periodo y revertir

**Descripción:** Constancia de procesamiento en planilla: aplicación unitaria o **lote por periodo con exclusión** (lock + índice de una-aplicación-activa); **reversión con motivo** (`APLICADO → AUTORIZADO`, historial trazado).

**Prioridad:** Alta
**Dependencias:** RF-016.

### RF-018 - Búsqueda corporativa, exports e insumo

**Descripción:** `POST /query` por empresa (filtros: estado, empleado, tipo, fechas, valor fijo, planilla, periodo, solicitante, moneda) + `StatusCounts` + totales por moneda; exports xlsx/csv/json; **insumo por periodo** cuadrado contra pendientes.

**Prioridad:** Alta
**Dependencias:** RF-015.

### RF-019 - Integración con liquidación

**Descripción:** Los `AUTORIZADO` no aplicados sugieren su monto como **línea de deducción** del finiquito (`DESCUENTO_EVENTUAL_PENDIENTE`, editable/excluible); al emitir pasan a `APLICADO` origen `LIQUIDACION`; anular la liquidación los reabre — espejo exacto de REQ-006 RF/D-15 con clase Descuento.

**Prioridad:** Media
**Dependencias:** RF-016; motor de liquidación.

### Plan 3 (REQ-010) — Endeudamiento

### RF-020 - Parámetros de endeudamiento (global y por tipo)

**Descripción:** (a) **`MaxIndebtednessPercent`** por empresa (preferencia nullable: null = sin control activo, patrón «default resuelto al consumir»; se administra por el **PUT** de preferencias — el PATCH es scalar-only); (b) tabla **`IndebtednessLimit`** por tipo de descuento cíclico (tenant, `recurringDeductionTypeCode` → `maxPercent`, editable, molde brackets de Renta). El límite por tipo **prevalece** sobre el global para los descuentos de ese tipo.

**Reglas de negocio:**
- % en (0, 100]; un límite por tipo (único por tenant+tipo); baja lógica; auditoría de cambios.
- Permiso `ManageIndebtednessParameters` (Admin fallback estándar).

**Criterios de aceptación:**
- Global 30% + `PRESTAMO_BANCARIO` 25% → un préstamo valida contra 25%; un descuento de cooperativa (sin fila) contra 30%; sin global ni fila → sin validación (registro libre).

**Prioridad:** Alta
**Dependencias:** REQ-008 construido (el catálogo de tipos).

### RF-021 - Validación de endeudamiento al registrar/autorizar descuentos

**Descripción:** Al **crear** y al **autorizar** un descuento cíclico (y opcionalmente eventual — P-12): el sistema calcula el **% proyectado** = (carga mensualizada actual + cuota mensualizada del nuevo) ÷ base de ingreso; si supera el límite aplicable responde **422 `INDEBTEDNESS_LIMIT_EXCEEDED`** con desglose (base, carga, nueva cuota, %, límite); reenviar con **`acknowledgeIndebtednessExceeded=true`** procede y **persiste la huella** del override (quién, cuándo, % y límite al momento). Nunca bloquea en firme (literal del levantamiento).

**Reglas de negocio:**
- Base F1 = Σ salario base mensual de plazas activas (P-11); carga F1 = Σ cuota mensualizada de cíclicos `VIGENTE` no estatutarios (P-12); mensualización: QUINCENAL×2, SEMANAL×4.33 (P-11 confirma factor), MENSUAL×1.
- Sin parámetros configurados → sin validación (aditivo/retrocompatible); el override queda visible en la ficha del descuento y en la consulta.

**Criterios de aceptación:**
- Salario $1,200, carga $340, límite 30%: nueva cuota $80 → 35% → 422 con desglose; con confirmación → registra con huella; empleado sin parámetros → sin advertencia.

**Prioridad:** Alta
**Dependencias:** RF-020; hooks en REQ-008 RF-002/RF-004 (y REQ-009 si P-12 lo incluye).

### RF-022 - Consulta de nivel de endeudamiento

**Descripción:** GET por empleado (`ViewIndebtedness`): base de ingreso derivada (desglosada por plaza), **carga mensualizada desglosada** por descuento vigente (tipo, institución, referencia, cuota, mensualizada), % actual, límites aplicables (global y por tipo), semáforo (dentro/excedido) y overrides históricos.

**Reglas de negocio:** solo lectura; sin efecto en datos; excluye descuentos suspendidos (marcados) — P-12 afina.

**Criterios de aceptación:**
- Empleado con 2 préstamos y 1 cooperativa → carga = Σ 3 mensualizadas, % = carga/base, cada fila con su límite aplicable.

**Prioridad:** Alta
**Dependencias:** RF-020; REQ-008.

### RF-023 - Simulación de endeudamiento

**Descripción:** POST de simulación (**sin persistencia**): parte del estado actual (o de un **ingreso digitado** — literal del levantamiento) + **deducción adicional** hipotética (monto de cuota + frecuencia + opcionalmente tipo) → % simulado, límite aplicable y **alerta** si lo supera. «Solo simulación y no debe afectar la planilla».

**Criterios de aceptación:**
- Simular cuota $150 sobre el escenario actual → % nuevo y bandera de exceso; nada queda persistido (test de no-escritura).

**Prioridad:** Alta
**Dependencias:** RF-022.

### Plan 4 (REQ-011) — Tiempos no trabajados

### RF-024 - Maestro de tipos de tiempo no trabajado (por empresa, con plantilla)

**Descripción:** CRUD governed (sin DELETE; activación/inactivación) del maestro por empresa: nombre, **aplica para permiso**, **utiliza jornada**, **incluye asueto**, **incluye domingo**, **incluye sábado**, **descuento de séptimo**, **% de descuento** (0–100), **tipo de descuento** (concepto `Nature=Egreso` + snapshot), **tipo de ingreso** (concepto `Nature=Ingreso` + snapshot, opcional) — los 10 campos literales del levantamiento. **Plantilla** (`load-template` + hook de provisioning, editable): `AUSENCIA_SIN_GOCE` (100%), `AUSENCIA_CON_GOCE` (0%), `SUSPENSION_CON_DESCUENTO` (100%), `LLEGADA_TARDIA` (100%, con jornada).

**Reglas de negocio:** molde de maestros REQ-007 (`CostCenter`): UQ por código normalizado filtrado por activo, probe + fallback anti-carrera → 422; conceptos activos al configurar; % en [0,100].

**Prioridad:** Alta
**Dependencias:** D-18; seeds `-9960…-9969`.

### RF-025 - Registrar tiempo no trabajado (con cálculo automático del descuento)

**Descripción:** Registro sobre el expediente: tipo (snapshot de flags y %), plaza (default principal), **rango de fechas** — u **horas** (fecha + h:m) si el tipo `usesWorkSchedule` —, motivo/observaciones, par planilla/periodo declarado. El sistema **calcula**: días computables (scan día a día excluyendo descanso semanal/sábado/domingo/asueto según flags) × `salario/30` × % + **séptimo** (si `deductsSeventhDay`: +1 día de descanso por semana afectada — P-18); en modo horas: horas × (`salario/30` ÷ horas-día empresa) × %. Desglose persistido como snapshot (días detalle, montos, warnings).

**Reglas de negocio:**
- Motor **puro** con casos dorados (espejo `IncapacityCalculationRules`); insumos snapshoteados: asuetos de la empresa, `RestDayOfWeek` de la plaza (fallback empresa→domingo), horas-día (null→8), salario base de la plaza.
- % = 0 (con goce) → registro sin descuento (igual alimenta disponibilidad); rango solapado con otro registro activo del mismo empleado → 422 (P-16 afina); origen `MANUAL` (costura `MARCACION` documentada).

**Criterios de aceptación:**
- Ausencia sin goce lunes–miércoles (3 días computables, tipo sin sábado/domingo/asueto) con séptimo → descuento = 4 × salario/30; con goce → monto $0; 6 horas con jornada 8h → 0.75 día × salario/30.

**Prioridad:** Alta
**Dependencias:** RF-024.

### RF-026 - Ciclo del registro (anulación) y asiento

**Descripción:** `REGISTRADA → ANULADA` con motivo (sin borrado); **asiento** en el journal del expediente al registrar (tipo P-20: `PERMISO -9479` existente o `TIEMPO_NO_TRABAJADO` nuevo; amount null, `effectiveFrom/To` = rango — espejo suspensión REQ-003); anular retira el efecto de bandejas/insumos y marca el asiento.

**Prioridad:** Alta
**Dependencias:** RF-025.

### RF-027 - Bandeja, insumo y exportaciones

**Descripción:** Bandeja por empresa (filtros: tipo, empleado, rango, estado, con/sin descuento) + export; **insumo por periodo** (tipo de planilla + periodo → registros con días/horas, % y monto calculado, concepto egreso snapshot) cuadrado contra la bandeja.

**Prioridad:** Alta
**Dependencias:** RF-025.

### RF-028 - Fuente de «Disponibilidad de tiempo»

**Descripción:** Los registros activos entran como **fuente aditiva** de la consulta de disponibilidad de REQ-003 (categoría nueva `TIEMPO_NO_TRABAJADO`, método de repositorio + categoría — contrato wire sin cambios, tal como su diseño lo anticipó).

**Criterios de aceptación:**
- Registro del 10–12 del mes → aparece en la consulta de disponibilidad del rango con su categoría; anulado → desaparece.

**Prioridad:** Media
**Dependencias:** RF-025; consulta de disponibilidad (en master).

---

## 7. Requerimientos no funcionales

- **Seguridad**: dato de compensación y **dato judicial sensible** (procuraduría, cuota alimenticia): permisos dedicados por módulo; `Authorize*` excluye Admin (separación de funciones); gates fail-closed por handler; anti-autoaprobación doble/triple; el override de endeudamiento exige permiso de gestión y queda auditado; 403 sin enmascaramiento; sin autoservicio en F1.
- **Auditoría**: quién registró/decidió/aplicó/anuló/confirmó-override y cuándo; motivos obligatorios; snapshots (concepto, tipo, institución, %, flags del maestro, desglose capital/interés, periodo); baja lógica universal; secuencia de cuotas inmutable.
- **Concurrencia/API**: convenciones del repo (`api/v1`, If-Match faltante→400 / obsoleto→409, ETag rotativo, enums string, errores bilingües `extensions.code`, user IDs `Guid?`); aplicación de cuotas/lotes y extraordinarias bajo **lock advisory** + re-verificación transaccional (tests de carrera obligatorios en ambos módulos de descuentos).
- **Rendimiento**: bandejas paginadas con índices por (tenant, empresa, estado, fechas) y (tenant, descuento, nº de cuota); proyección/amortización = cálculo puro en memoria por registro (cero filas futuras); consulta de endeudamiento resuelve base+carga en una pasada por empleado; exports con rate limiting y límite síncrono existentes.
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId`; sin jobs nuevos (finalizaciones transaccionales; sin vencimientos programados).
- **Usabilidad**: errores accionables con desglose (segmentos incoherentes, mismatch de factores, exceso de endeudamiento con los 5 números, cuota fuera de secuencia); tabla de amortización visible antes de guardar; bandeja de vencidas como lista de trabajo; la advertencia de endeudamiento explica el límite aplicado y cómo confirmar.
- **Mantenibilidad**: reglas en **módulos puros** (`RecurringDeductionRules` con amortización, `OneTimeDeductionRules`, `IndebtednessRules`, `NotWorkedTimeRules`) con suites doradas (A.3) y paridad de localización EN/ES; OpenAPI sin drift; guías FE por plan.
- **Compatibilidad**: cambios 100 % aditivos (entidades/catálogos/permisos nuevos; los módulos de ingresos, el motor de liquidación y los catálogos existentes solo se consumen; la validación de endeudamiento es opt-in por configuración).
- **Accesibilidad**: (frontend) formularios de segmentos y amortización con derivación visible; simulador con resultado inmediato; se documenta en las guías FE.

---

## 8. Historias de usuario

### HU-001 - Registrar y autorizar un préstamo con interés
Como **gestor de RRHH**, quiero **registrar un descuento cíclico por préstamo bancario con su tabla de amortización calculada**, para **que un autorizador lo apruebe y las retenciones queden planificadas con capital e interés exactos**.

**Criterios de aceptación:**
- Dado un préstamo de $1,000 al 12% en 12 cuotas, cuando lo registro con interés compuesto, entonces veo la cuota fija ($88.85) y la tabla capital/interés antes de guardar, y queda `EN_REVISION`.
- Dado que yo lo registré, cuando intento autorizarlo, entonces recibo 403.

### HU-002 - Retener las cuotas del periodo
Como **analista de planilla**, quiero **aplicar en lote las cuotas de descuento de la quincena (saltando los meses exceptuados)**, para **que el insumo hacia la nómina externa cuadre con lo realmente retenible**.

**Criterios de aceptación:**
- Dado el lote del periodo, cuando confirmo con 2 cuotas excluidas, entonces las excluidas quedan pendientes para otro periodo y el resto se aplica con snapshot (incl. capital/interés).
- Dado diciembre como mes de excepción del plan, entonces sus cuotas no aparecen en el lote y el plan se corre.

### HU-003 - Abonar y liquidar un crédito
Como **gestor de RRHH**, quiero **registrar cuotas extraordinarias (abonos) contra la referencia crediticia**, para **reducir el saldo, acortar el plazo o liquidar el crédito anticipadamente**.

**Criterios de aceptación:**
- Dado un abono de $200, entonces el saldo baja y la proyección se regenera (plazo reducido).
- Dado un abono por el saldo total, entonces el descuento queda `FINALIZADO` automáticamente.

### HU-004 - Controlar el endeudamiento al aceptar una orden
Como **gestor de RRHH**, quiero **que el sistema me advierta cuando un nuevo descuento supere el % de endeudamiento configurado y me deje continuar solo confirmando**, para **proteger el salario del empleado sin frenar órdenes obligatorias (procuraduría)**.

**Criterios de aceptación:**
- Dado límite 30% y % proyectado 35%, cuando registro, entonces recibo el detalle (base, carga, cuota nueva, %, límite) y no se guarda.
- Cuando reenvío con la confirmación explícita, entonces se guarda con la huella del override (quién/cuándo/%/límite).

### HU-005 - Consultar y simular el nivel de endeudamiento
Como **analista de créditos**, quiero **ver el endeudamiento actual de un empleado y simular una deducción adicional**, para **decidir antes de aceptar una nueva orden — sin afectar la planilla**.

**Criterios de aceptación:**
- Dada la consulta, entonces veo base, carga desglosada por descuento, % y límites; dada la simulación con cuota $150, entonces veo el % proyectado y la alerta, y nada queda persistido.

### HU-006 - Registrar un descuento eventual solicitado por un área
Como **gestor de RRHH**, quiero **registrar un descuento ocasional (fijo o por porcentaje) con su solicitante y su planilla/periodo**, para **que se autorice, se procese en el periodo correcto y quede trazado quién lo pidió**.

**Criterios de aceptación:**
- Dado un descuento del 10% sobre base $500, entonces el servidor deriva $50.00; dado que el solicitante intenta autorizar, entonces 403 (triple).

### HU-007 - Registrar una ausencia sin goce con descuento calculado
Como **gestor de RRHH**, quiero **registrar un tiempo no trabajado y que el sistema calcule el descuento según el tipo (exclusiones, %, séptimo)**, para **no digitar montos a mano ni equivocar el valor-día**.

**Criterios de aceptación:**
- Dada una ausencia sin goce de lunes a miércoles con séptimo, entonces el descuento = 4 × salario/30 con desglose visible.
- Dado un tipo con goce (0%), entonces el registro no genera descuento pero sí aparece en disponibilidad de tiempo.

### HU-008 - Resolver los créditos al liquidar al empleado
Como **analista de liquidaciones**, quiero **que el saldo de los descuentos con `DESCONTAR_SALDO` se sugiera como deducción del finiquito**, para **cerrar los créditos del empleado sin cálculos manuales (o condonarlos con `CANCELAR`)**.

**Criterios de aceptación:**
- Dado saldo $300 con `DESCONTAR_SALDO`, entonces la línea de deducción $300 aparece editable y el neto la absorbe; dado `CANCELAR`, entonces sin línea y `FINALIZADO` al emitir; dada la anulación de la liquidación, entonces reapertura a `VIGENTE`.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | El **estado nunca se digita** en ningún módulo: lo fija el flujo (cíclicos 6 estados espejo REQ-005; eventuales 5 espejo REQ-006; tiempos `REGISTRADA/ANULADA`) |
| RN-02 | Solo `EN_REVISION` es editable; terminales: `RECHAZADO`/`ANULADO`/`FINALIZADO` (la anulación de cuota puede reabrir `FINALIZADO→VIGENTE`) |
| RN-03 | **Anti-autoaprobación**: cíclicos doble (sujeto, registrador); eventuales **triple** (+ solicitante) → 403 |
| RN-04 | «Tipo de descuento» = concepto país activo `Nature=Egreso` **no estatutario** con snapshot; ISSS/AFP/Renta no se registran a mano (`CONCEPT_STATUTORY` → 422) |
| RN-05 | Plan por **segmentos**: tramos contiguos desde la cuota 1, sin huecos ni solapes, valores > 0; finito → count/total derivados de los segmentos; indefinido → un segmento abierto y sin `DESCONTAR_SALDO` (D-12); interés y segmentos manuales excluyentes en la captura |
| RN-06 | Interés compuesto: cuota fija derivada del principal/tasa/n (sistema francés — P-03); desglose capital/interés por cuota; `Round2` half-up único; última cuota ajusta el residuo; **golden del contador como gate** |
| RN-07 | Solo descuentos `VIGENTE` **con vigencia alcanzada** aplican cuotas; `SUSPENDIDO` bloquea sin alterar el plan; secuencia sin saltos/duplicados/exceso; **meses de excepción se saltan y el plan se corre** |
| RN-08 | Frecuencia de **aplicación** manda en la operación por periodo; si difiere de la frecuencia de cuota, la cuota se divide en partes iguales (ajuste en la última parte) — P-06 |
| RN-09 | Cuota **extraordinaria**: > 0 y ≤ saldo; abona a capital/saldo; regenera la proyección (default: reducir plazo — P-04); el payoff total finaliza; anulable con motivo (reintegra) |
| RN-10 | Aplicaciones bajo **lock advisory** + re-verificación transaccional; la última cuota finaliza en la misma transacción; test de carrera obligatorio |
| RN-11 | Nada se borra físicamente: cuota/registro mal aplicado → `ANULADA` con motivo; revocación no toca lo aplicado; anuladas visibles y fuera de totales/insumos |
| RN-12 | **Endeudamiento advierte, nunca bloquea**: exceso → 422 con desglose salvo confirmación explícita; el override se **persiste auditado**; sin parámetros configurados → sin validación |
| RN-13 | Base y carga del endeudamiento: base = Σ salario base mensual de plazas activas (P-11); carga = Σ cuota mensualizada de cíclicos `VIGENTE` no estatutarios (P-12); límite por tipo prevalece sobre el global |
| RN-14 | **No se escriben ledgers ni configuración**: ni `PersonnelFilePayrollTransaction` ni `CompensationConcept` automáticos; el efecto monetario viaja por aplicación + insumo; la liquidación solo recibe **líneas sugeridas** |
| RN-15 | Consulta y simulación de endeudamiento son **read-only** (test de no-escritura); la simulación admite ingreso digitado sin persistirlo |
| RN-16 | Frontera con la configuración: el concepto Externo de la plaza es estructural/indefinido **sin saldo**; el descuento cíclico es el **crédito con saldo y cuotas** — comunicado en UI/guía FE; sin bloqueo duro |
| RN-17 | Imputación: par `payrollTypeCode` (catálogo) + `payrollPeriodId?` (FK real) + etiqueta snapshot en toda cuota/aplicación/extraordinaria/registro |
| RN-18 | Catálogos/maestros consumidos deben estar activos al usarse → 422; los históricos conservan snapshot |
| RN-19 | Tiempos no trabajados: el descuento **se calcula, nunca se digita** — días/horas computables según flags del tipo (asueto/sábado/domingo/descanso) × salario/30 (÷ horas-día si jornada) × % + séptimo por semana afectada; desglose persistido; motor puro con golden |
| RN-20 | Perfil `RETIRADO`: sin altas en ningún módulo; pendientes solo rechazables/anulables; vigentes se resuelven al liquidar según su acción (D-12/D-15) |
| RN-21 | Tiempos no trabajados asientan en el journal (hecho laboral, amount null + rango); los descuentos cíclicos/eventuales **no** asientan (dato de compensación) — D-22 |
| RN-22 | Mono-moneda de facto: moneda de catálogo activa con default de la empresa (USD); totales por moneda; el endeudamiento calcula en la moneda de la empresa |

---

## 10. Flujos principales

### Flujo 1 — Descuento cíclico end-to-end (préstamo con interés)
1. RRHH abre el expediente → «Nuevo descuento cíclico».
2. Ingresa vigencia, referencia crediticia, tipo cíclico (`PRESTAMO_BANCARIO`), tipo de descuento (concepto), institución, plaza, observaciones.
3. Activa interés compuesto: principal $1,000, tasa 12%, 12 cuotas → el sistema muestra cuota $88.85 y la tabla de amortización; RRHH acepta (o ajusta a la tabla del banco).
4. Completa: fecha de inicio, meses de excepción, moneda, tipo de planilla, frecuencias.
5. (Si REQ-010 activo) El sistema valida el endeudamiento: dentro del límite → guarda `EN_REVISION`; excedido → muestra el desglose y RRHH confirma para continuar.
6. El autorizador (tercero) autoriza → `VIGENTE`; las cuotas entran a la bandeja de pendientes desde la vigencia.

### Flujo 2 — Retención del periodo (lote)
1. El analista abre la bandeja de cuotas pendientes, filtra planilla `QUINCENAL` + periodo.
2. El sistema lista las cuotas por frecuencia de aplicación (saltando meses exceptuados y descuentos suspendidos/no vigentes), más las vencidas.
3. El analista excluye las que se posponen y confirma; cada cuota se aplica bajo lock con snapshot (monto, capital/interés, periodo).
4. Exporta el **insumo del periodo** y lo entrega a la nómina externa.

### Flujo 3 — Cuota extraordinaria / liquidación del crédito
1. El empleado abona $200 (o la institución reporta el abono) → RRHH registra la extraordinaria (nº E1, fecha, valor, planilla/periodo).
2. El sistema reduce el saldo y regenera la proyección (plazo reducido).
3. Para liquidar el crédito: extraordinaria por el saldo total → `FINALIZADO` automático.

### Flujo 4 — Endeudamiento (consulta, simulación y override)
1. El analista consulta el nivel del empleado: base $1,200, carga $340 (28.3%), límites 30%/25% por tipo.
2. Simula una cuota adicional de $150 → 40.8% → alerta (nada se persiste).
3. Llega una orden de la procuraduría que excede el límite: registra el descuento, el sistema advierte, RRHH **confirma** (orden judicial) → se guarda con huella del override.

### Flujo 5 — Descuento eventual
1. Un área (solicitante) pide descontar un daño de equipo: RRHH registra con tipo `DANO_EQUIPO`, 10% sobre base $500 → $50 derivado, planilla/periodo destino.
2. El autorizador (ni sujeto, ni registrador, ni solicitante) autoriza → `AUTORIZADO`.
3. El analista lo aplica en el lote del periodo (o lo re-imputa a otro) → `APLICADO`; el insumo lo incluye.

### Flujo 6 — Tiempo no trabajado
1. RRHH registra la ausencia: tipo `AUSENCIA_SIN_GOCE`, plaza, lunes–miércoles, motivo.
2. El sistema calcula: 3 días computables + séptimo = 4 × salario/30 → desglose visible; guarda `REGISTRADA`, asienta en el journal y alimenta disponibilidad de tiempo.
3. El registro entra al insumo del periodo declarado; la nómina externa aplica el descuento.

### Flujo 7 — Liquidación del empleado con créditos vigentes
1. El analista prepara la liquidación (módulo existente).
2. El sistema sugiere una línea de **deducción** por cada cíclico `VIGENTE` con `DESCONTAR_SALDO` (saldo, editable) y por cada eventual `AUTORIZADO` no aplicado — plaza principal.
3. Al emitir: los `CANCELAR` quedan `FINALIZADO` (condonados); los descontados quedan saldados; los eventuales incluidos pasan a `APLICADO` origen `LIQUIDACION`.
4. Si la liquidación se anula, todo se reabre simétricamente.

---

## 11. Flujos alternativos y excepciones

- **Decisión por sujeto/registrador (cíclicos) o + solicitante (eventuales)** → 403 `SELF_APPROVAL_FORBIDDEN`; Admin sin `Authorize*` → 403.
- **Segmentos con huecos/solapes/valores ≤ 0** → 422 con el tramo en conflicto; **interés + segmentos manuales a la vez** → 422.
- **Concepto estatutario (`ISSS`/`AFP`/`RENTA`) o `IsBaseSalary` o inactivo** → 422; **catálogo/maestro inactivo** → 422.
- **`DESCONTAR_SALDO` sobre indefinido** → 422 (`CANCELAR` forzado).
- **Extraordinaria > saldo** → 422; **extraordinaria sobre no-`VIGENTE`** → 422.
- **Aplicar cuota de descuento no vigente / vigencia futura / mes exceptuado** → 422 (la bandeja ya los excluye; el guard re-verifica).
- **Cuota duplicada / fuera de secuencia / plan excedido** → 422; **doble submit concurrente** → segundo 409/422 (lock + re-verificación).
- **Exceso de endeudamiento sin confirmación** → 422 `INDEBTEDNESS_LIMIT_EXCEEDED` con desglose; **con confirmación** → 2xx + huella; **sin parámetros configurados** → sin validación.
- **Mismatch de factores del eventual** (monto ≠ derivado) → 422 con desglose; **eventual aplicado dos veces** → 422 (índice de aplicación activa).
- **Tiempo no trabajado solapado con otro registro activo** → 422; **tipo con jornada y registro por rango de días (o viceversa)** → 422; **horas > jornada del día** → 422.
- **Anular cuota ya anulada** → 422; **anulación que reabre `FINALIZADO`** → transición automática a `VIGENTE` (no error).
- **Perfil `RETIRADO`**: alta → 422; decidir → solo rechazo/anulación.
- **If-Match ausente** → 400; **obsoleto** → 409; **insumo sin tipo de planilla + periodo** → 400.
- **Neto del finiquito negativo por la deducción del saldo** → warning del motor (existente), línea editable por el analista.

---

## 12. Datos requeridos

> Convenciones del repo en todas las entidades: `long Id` + `Guid publicId`, `TenantId`, `CreatedUtc`/`ModifiedUtc`, `isActive`, `concurrencyToken`, factoría `Create(...)` + mutadores custodiados. Se listan solo campos de negocio. Nombres finales los fija cada plan técnico.

### Entidad: Descuento cíclico (`PersonnelFileRecurringDeduction` — Plan 1; espejo estructural de `PersonnelFileRecurringIncome`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| effectiveDate | Fecha | Sí | Puede ser futura | «Fecha que entra en vigencia» (las cuotas no aplican antes) |
| reference | Texto (200) | Sí | — | «Referencia» crediticia (ancla de extraordinarias) |
| recurringDeductionTypeCode | Código catálogo | Sí | `RECURRING_DEDUCTION_TYPE_CATALOG` activo | «Tipo de descuento cíclico» |
| conceptTypeCode / conceptNameSnapshot | Código / Texto | Sí | Concepto `Nature=Egreso` activo no estatutario (RN-04) | «Tipo de descuento» + snapshot |
| financialInstitution | Texto (200) | No* | — | «Institución financiera» (*obligatoria según tipo — P-07) |
| assignedPositionPublicId | Guid | Sí | Plaza del empleado (default principal) | Ancla de liquidación e insumo (sin centro de costo — D-13) |
| observations | Texto (1000) | No | — | «Observación» |
| statusCode | Código catálogo | Sí (flujo) | 6 estados espejo (RN-01) | «Estado» — lo fija el flujo |
| installmentStartDate | Fecha | Sí | Retroactiva permitida | Cuotas: fecha de inicio de descuento |
| exceptionMonths | Entero[] (1..12) | No | Valores 1–12 únicos | «Meses con excepción» (la cuota se corre — P-05) |
| currencyCode | Código catálogo | Sí | Moneda activa; default empresa | Cuotas: moneda |
| payrollTypeCode | Código catálogo | Sí | `payroll-types` activo | Cuotas: tipo de planilla |
| installmentFrequencyCode | Código catálogo | Sí | `pay-periods` activo | «Frecuencia de cuota» (devengo) |
| applicationFrequencyCode | Código catálogo | Sí | `pay-periods` activo; coherencia blanda con la de cuota (P-06) | «Frecuencia de aplicación» (retención) |
| isIndefinite | Booleano | Sí | Indefinido → sin `DESCONTAR_SALDO` | «Monto y nº de cuotas indefinidas (sí/no)» |
| usesCompoundInterest | Booleano | Sí | Excluyente con segmentos manuales | Activa la calculadora (RF-003) |
| principalAmount / interestRatePercent / plannedInstallments | Decimal / Decimal / Entero | Si interés | > 0; tasa nominal anual (P-03) | Parámetros de la amortización; la tasa se **precarga del default por empresa** (preferencia nullable — P-03 ratificada) y es editable por crédito |
| settlementActionCode | Código catálogo | Sí | `DESCONTAR_SALDO`/`CANCELAR` (compatibilidad D-12) | Acción al liquidar al empleado |
| registeredByUserId / decidedBy… / suspended… / closed… / closedBySettlementPublicId | (espejo) | — | Espejo exacto REQ-005 | Auditoría de flujo y cierre por liquidación |
| acknowledgedIndebtedness… (exceededPercent, limitPercent, byUserId, utc) | Decimal / Decimal / Guid / Fecha | Al confirmar override | Solo vía RF-021 | Huella del override de endeudamiento |
| *(derivados)* totalCollected / totalPending / remainingBalance / nextInstallment | — | — | Calculados (RN nunca persistidos) | «Total cobrado / total no cobrado», saldo, próxima |

### Entidad: Segmento del plan (`…DeductionPlanSegment` — hija de definición, Plan 1)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| fromInstallment / toInstallment | Entero / Entero? | Sí / Si finito | Contiguos desde 1, sin huecos/solapes; `to` null = abierto (indefinido) | «Cuota inicial / cuota final» |
| installmentValue | Decimal (18,2) | Sí | > 0 | «Valor» del tramo |

### Entidad: Cuota aplicada (`…DeductionInstallment` — hija, se persiste al aplicar; Plan 1)

Espejo exacto de la cuota de REQ-005 (nº, fecha, teórica, monto, moneda, planilla, `payrollPeriodId?` + etiqueta, origen `MANUAL`/`MOTOR`, estado `APLICADA`/`ANULADA`, quién, anulación) **más**:

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| kind | Código | Sí | `REGULAR`/`EXTRAORDINARIA` | Cuota del plan o abono (RF-008) |
| extraordinaryNumber | Entero? | Si extraordinaria | Secuencia propia E1, E2… | «Nº de cuota» de la extraordinaria |
| capitalAmount / interestAmount | Decimal? / Decimal? | Si interés | capital + interés = monto (Round2) | «Monto a capital / monto a interés» (snapshot) |

### Entidad: Descuento eventual (`PersonnelFileOneTimeDeduction` — Plan 2; espejo de `PersonnelFileOneTimeIncome`)

Campos espejo de REQ-006: fecha, concepto egreso + snapshot (RN-04), plaza, `isFixedValue`, método (`PORCENTAJE_SOBRE_BASE`/`CANTIDAD_POR_VALOR`) + componentes persistidos (`percentage`/`baseAmount`/`quantity`/`unitValue`/`multiplier`), `amount` derivado/validado, moneda, **solicitante trío** (`requesterFilePublicId`/`requesterNameSnapshot`/`requestedByUserId`), par planilla/periodo re-imputable, estados 5, aplicaciones hijas con índice de una-activa, `appliedBySettlementPublicId`.

### Entidad: Límite de endeudamiento por tipo (`IndebtednessLimit` — Plan 3; molde brackets)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| recurringDeductionTypeCode | Código catálogo | Sí | Tipo activo; único por tenant+tipo | Tipo de descuento cíclico al que aplica |
| maxPercent | Decimal (5,2) | Sí | (0, 100] | Límite del tipo (prevalece sobre el global) |

*(+ preferencia por empresa `MaxIndebtednessPercent decimal?` en `CompanyPreference` — null = sin control; se administra por PUT.)*

### Entidad: Tipo de tiempo no trabajado (`NotWorkedTimeType` — Plan 4; maestro por empresa con plantilla)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | UQ normalizado por tenant (activo) | «Nombre del tipo» |
| appliesToLeavePermit | Booleano | Sí | — | «Aplica para permiso» (costura P-17) |
| usesWorkSchedule | Booleano | Sí | — | «Utiliza jornada» (registro en horas) |
| includesHoliday / includesSunday / includesSaturday | Booleano ×3 | Sí | — | «Incluye asueto / domingo / sábado» (flags de conteo) |
| deductsSeventhDay | Booleano | Sí | — | «Descuento de séptimo» |
| discountPercent | Decimal (5,2) | Sí | [0, 100]; 0 = con goce | «Porcentaje de descuento» |
| deductionConceptTypeCode / …NameSnapshot | Código / Texto | Sí | Concepto `Egreso` activo | «Tipo de descuento» del insumo |
| incomeConceptTypeCode / …NameSnapshot | Código / Texto | No | Concepto `Ingreso` activo | «Tipo de ingreso» (semántica P-17) |

### Entidad: Registro de tiempo no trabajado (`PersonnelFileNotWorkedTime` — Plan 4)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| notWorkedTimeTypePublicId + snapshot de flags/% | Guid + varios | Sí | Tipo activo; flags congelados al registrar | Tipo aplicado |
| assignedPositionPublicId | Guid | Sí | Plaza (default principal) | Fuente de salario/descanso |
| startDate / endDate | Fecha / Fecha | Sí (modo días) | rango válido; sin solape activo | Periodo no trabajado |
| hours (h:m → decimal) | Decimal? | Sí (modo horas) | > 0; ≤ jornada del día | Duración si `usesWorkSchedule` |
| reason / observations | Texto | Sí / No | — | Motivo |
| payrollTypeCode / payrollPeriodId? / label | Código / FK? / Texto | Sí | RN-17 | Imputación declarada |
| *(cálculo persistido)* computableDays·u·hours / dailySalarySnapshot / seventhDayDeducted / discountAmount / detailJson | varios | Sí (al calcular) | Motor puro (RN-19) | Desglose del descuento (molde `ApplyCalculation`) |
| statusCode / originCode | Código / Código | Sí | `REGISTRADA`/`ANULADA` · `MANUAL` (`MARCACION` futuro) | Ciclo y costura biométrica |

### Catálogos nuevos (semilla `HasData` — detalle e IDs tentativos en A.2)

- `RECURRING_DEDUCTION_STATUS_CATALOG` (6) · `RECURRING_DEDUCTION_SETTLEMENT_ACTION_CATALOG` (2) · `RECURRING_DEDUCTION_TYPE_CATALOG` (5, plantilla editable) · concepto de liquidación `DESCUENTO_CICLICO_PENDIENTE` — Plan 1.
- `ONE_TIME_DEDUCTION_STATUS_CATALOG` (5) · concepto `DESCUENTO_EVENTUAL_PENDIENTE` — Plan 2.
- (Plan 3: sin catálogos salvo que P-09 ratifique «familias».)
- `NOT_WORKED_TIME_STATUS_CATALOG` (2) · ActionType del asiento (P-20) — Plan 4; el maestro de tipos es entidad por empresa (plantilla, sin seeds globales).

### Catálogos/entidades existentes reutilizados (solo lectura)

`compensation-concept-types` (`Nature=Egreso` — «tipos de descuentos»; `Nature=Ingreso` para el tipo de ingreso del Plan 4) · `currencies` · `pay-periods` (las 2 frecuencias) · `payroll-types` · `payroll-period-definitions` · `CompanyHoliday` · `RestDayOfWeek` (plaza→empresa→domingo) · `CompensatoryTimeStandardDailyHours` · salario base por plaza (`IsBaseSalary`) · motor de liquidación (líneas de deducción sugeridas) · chasis de disponibilidad de tiempo.

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Planilla externa** | Saliente (archivos) | Insumos por periodo de los 3 dominios: cuotas de cíclicos (con institución/referencia/capital-interés), eventuales y tiempos no trabajados. No se escribe `PersonnelFilePayrollTransaction` (RN-14) |
| **Catálogo de conceptos** | Interna (lectura) | «Tipos de descuentos» = `Nature=Egreso` no estatutario; verificación/siembra de `COOPERATIVA`/`PROCURADURIA` (D-03) |
| **Motor de liquidación** | Interna (bidireccional acotada) | Líneas de **deducción** sugeridas (`DESCUENTO_CICLICO_PENDIENTE`, `DESCUENTO_EVENTUAL_PENDIENTE`, ambas `IsSystemCalculated=false`); cierre al emitir / reapertura al anular (molde verificado) |
| **Periodos/tipos de planilla** | Interna (lectura) | FK real a `payroll_period_definitions` + `payroll-types` (ya sembrado) — sin modo degradado |
| **Endeudamiento ↔ descuentos** | Interna | Hooks de validación en crear/autorizar de REQ-008 (y REQ-009 según P-12); consulta lee los vigentes |
| **Disponibilidad de tiempo (REQ-003)** | Interna (aditiva) | Tiempos no trabajados = fuente/categoría nueva (contrato wire sin cambios — anticipado en su diseño) |
| **Journal del expediente** | Interna | Solo Plan 4 asienta (amount null + rango, espejo suspensión); Planes 1-2 no asientan (D-22) |
| **Módulo de marcación (futuro)** | Costura documentada | Campo `origin` del registro (`MANUAL`/`MARCACION`); el levantamiento lo condiciona a adquirir ese módulo |
| **Instituciones externas** | Ninguna (documental) | Banco/procuraduría/cooperativa viven como texto + referencia; maestro = F2 (P-07) |
| **Correo / notificaciones** | Fase 2 | Aviso a autorizadores/analistas (pendientes, vencidas, overrides) |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de empresa | Catálogos/maestros, parámetros de endeudamiento (`ManageIndebtednessParameters`), asignación de permisos | **No decide** descuentos salvo grant `Authorize*` explícito |
| Gestor de RRHH / Analista de planilla | `ManageRecurringDeductions`, `ManageOneTimeDeductions`, `ManageNotWorkedTimes` | No decide lo que registró (anti-self); ediciones solo `EN_REVISION`; el override de endeudamiento queda auditado a su nombre |
| Autorizador | `AuthorizeRecurringDeductions` / `AuthorizeOneTimeDeductions` | Anti-self doble/triple; motivos obligatorios; sin fallback Admin |
| Consulta / Auditor | `ViewRecurringDeductions` / `ViewOneTimeDeductions` / `ViewNotWorkedTimes` / `ViewIndebtedness` | Solo lectura y exports |
| Analista de créditos | `ViewIndebtedness` | Consulta y simulación; sin escritura |
| Finanzas / Planilla externa | `View*` (insumos) | Solo lectura/exportación |
| Empleado | Sin acceso en F1 | Autoservicio/consulta self = evolución (P-15) |

---

## 15. Criterios de aceptación generales

1. **Ratificación previa**: ✅ cumplida (2026-07-12) — D-01…D-22 ratificadas (D-08/D-16 anotadas), P-01…P-21 respondidas y P-22 eliminada (§17); la **división en 4 REQs confirmada** (P-01); los planes técnicos se derivan de este documento ratificado.
2. Reglas de cada dominio en **módulos puros** con suites doradas (amortización y séptimo con **casos del contador** — A.3) y paridad de localización EN/ES.
3. Suite de integración por plan **en verde junto con la existente**: flujos completos con anti-self, segmentos/interés/extraordinarias, lote con exclusión + **tests de carrera**, meses de excepción, endeudamiento (422 → override → huella; no-escritura de la simulación), cálculo de tiempos (exclusiones + séptimo), permisos incluida la exclusión de Admin, insumos cuadrados contra pendientes, ganchos de liquidación con reversión simétrica.
4. Migraciones `HasData` idempotentes; IDs verificados contra `GlobalCatalogSeedData` al abrir cada PR-1 (bloques A.2; trampa GUID); `has-pending-model-changes` vacío.
5. `openapi.yaml` sin drift; convenciones API respetadas; guía FE publicada **por plan**.
6. Proyecciones/amortizaciones derivadas (cero filas futuras persistidas) cuadrando con lo aplicado en todos los estados.
7. Los módulos de ingresos, el motor de liquidación (contrato), el ledger externo y la configuración de conceptos quedan **intactos** (tests de no-escritura/retrocompatibilidad).
8. La validación de endeudamiento es **opt-in por configuración** (sin parámetros → comportamiento actual) y **nunca bloquea** con confirmación.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Aritmética financiera sin bendición del contador**: una amortización que difiera en centavos de la tabla del banco genera disputa por cada cuota. Mitigación: golden cases del contador **como gate** (A.3), plan editable para adoptar la tabla del acreedor, desglose visible antes de guardar.
- **Expectativa de descuento automático en planilla**: sin motor, la retención real la hace la nómina externa con el insumo. Mitigación: comunicar la frontera (igual que REQ-005), bandeja de vencidas como lista de trabajo, lote de un clic.
- **Complejidad de frecuencias** (cuota vs aplicación + meses de excepción): riesgo de planes incomprensibles. Mitigación: P-05/P-06 ratificadas antes del plan técnico; validación blanda + proyección visible; casos dorados de calendario.
- **Doble vía con la configuración** (concepto Externo de la plaza vs descuento cíclico): mitigación RN-16 (frontera editorial + guía FE) y adopción manual de los Externo existentes (P-22).
- **Sensibilidad judicial** (procuraduría, cuota alimenticia): fuga = riesgo legal. Mitigación: permisos dedicados, sin autoservicio, auditoría completa.
- **Override de endeudamiento banalizado** (confirmar sin leer): mitigación: huella auditada visible en ficha/consulta + desglose en el error; el negocio puede endurecer a bloqueo en F2 si lo pide.
- **Colisión de seeds** entre planes que arranquen en paralelo: mitigación: bloques disjuntos reservados (A.2) + verificación al abrir cada PR-1 (protocolo ya probado en REQ-004/005).
- **Alcance del Plan 4 creciendo hacia «permisos/solicitudes»** (autoservicio del empleado): mitigación: F1 es registro de RRHH; la costura `appliesToLeavePermit` queda documentada sin construir flujo de solicitud.

### Supuestos

- La nómina se procesa **externamente** durante estos 4 planes; el insumo exportado es el mecanismo de entrega (P-01 de REQ-005 sigue vigente).
- Tenant mono-país (SV), moneda de empresa USD; los montos de descuentos se registran en esa moneda.
- Las órdenes de descuento llegan **a RRHH** (documentos de banco/procuraduría/cooperativa); el empleado no las registra.
- El salario base por plaza está configurado (concepto `IsBaseSalary`) para los empleados con descuentos — insumo del endeudamiento y de tiempos no trabajados (sin salario → 422 accionable en el cálculo, como en liquidación).
- Los 4 planes se ratifican sobre este único análisis; cada uno deriva su plan técnico al arrancar.

### Dependencias

- **Ratificación del negocio**: ✅ completada (2026-07-12, §17) — los 4 planes técnicos quedan desbloqueados.
- **REQ-008 → REQ-010**: la carga de endeudamiento nace de los cíclicos; REQ-009/011 son independientes entre sí y de REQ-010.
- **Módulos en master** (verificado): molde REQ-005/006, liquidación, periodos de planilla, asuetos, `RestDayOfWeek`, horas-día, disponibilidad de tiempo — ninguna dependencia pendiente de construcción.
- Internas: verificación de seeds al abrir cada PR-1; convenciones de catálogos/permisos vigentes; confirmación del contador (amortización y séptimo) antes del despliegue de REQ-008/011.

---

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-12)

> ✅ **Respondidas por el negocio (2026-07-12)**: **18 recomendaciones aceptadas tal cual** (P-01/02/04/05/06/07/08/10/11/12/14/15/16/17/18/19/20/21) + **P-09 con precisión** (la clasificación existente basta **y** los catálogos que no existan se generan — ya previsto en D-20/P-10) + **1 ajuste: P-03** (el % de interés **depende de cada empresa → default configurable** como preferencia; la tasa del crédito sigue por registro, editable) + **1 instrucción: P-13** (esperar a REQ-010 **documentando el pendiente** para la futura iteración) + **1 eliminada: P-22** (no aplica — **aún no hay producción**: frontend y data de prueba; **sin fallbacks ni migraciones legacy**; se retiró el flujo de adopción). Con esto **D-01…D-22 quedan RATIFICADAS** (D-08/D-16 anotadas).

| # | Plan | Pregunta | Recomendación → respuesta del negocio |
|---|---|---|---|
| **P-01** | Todos | **(Estructural)** ¿Se confirma la división en 4 REQs (008 cíclicos → 009 eventuales → 010 endeudamiento → 011 tiempos) manteniendo el corte sin motor de planilla? | **Sí** — el orden amortiza el molde y el endeudamiento necesita los cíclicos primero; REQ-011 puede adelantarse si urge el ausentismo |
| P-02 | 1-2 | ¿Los descuentos (cíclicos y eventuales) llevan **flujo de autorización** como los ingresos? El levantamiento no lo pide explícito para descuentos | **Sí, espejo** (una decisión, anti-self): son retenciones al salario — más sensibles que los ingresos; simetría operativa y de permisos |
| P-03 | 1 | **Interés compuesto**: ¿tasa nominal **anual** con capitalización por frecuencia de cuota (12%/12 mensual)? ¿O tasa efectiva/por periodo? ¿La calculadora es ayuda editable o fuente de verdad? | **Nominal anual ÷ periodos** (sistema francés estándar SV); la calculadora **genera el plan editable** (la tabla del acreedor manda si difiere); si se edita, capital/interés se re-derivan del saldo. **✔ Aceptada con ajuste: «el % de interés depende de cada empresa, debe ser configurable»** → preferencia por empresa como default del formulario (D-08 ajustada) |
| P-04 | 1 | **Cuota extraordinaria** con interés: ¿reduce plazo (cuota igual, menos cuotas) o recalcula cuota (mismo plazo, cuota menor)? ¿Se admite sobre `SUSPENDIDO`? | **Reducir plazo** (práctica bancaria dominante y aritmética más simple); no sobre suspendidos en F1. «Liquidar el crédito» = extraordinaria por el saldo |
| P-05 | 1 | **Meses con excepción**: ¿la cuota exceptuada se **corre** (el plan se alarga) o se **pierde**? ¿Se necesita catálogo BD de meses? | **Se corre** (el crédito se debe completo); sin catálogo BD — multi-select 1..12 con etiquetas en FE |
| P-06 | 1 | **Frecuencia de aplicación** vs frecuencia de cuota: ¿la cuota mensual retenida en planilla quincenal se **divide** ($100 → 2×$50)? ¿Qué combinaciones son válidas? | **Sí, división entera** (aplicación ≥ frecuencia de cuota: mensual→quincenal/semanal); combinaciones inversas → 422; validación blanda del par con la planilla de la plaza |
| P-07 | 1 | **Institución financiera**: ¿texto libre o maestro por empresa? ¿Obligatoria siempre o según tipo? | **Texto libre F1** (precedente `CounterpartyName`), obligatoria para tipos externos (préstamo/procuraduría/cooperativa); maestro = F2 si el negocio lo pide |
| P-08 | 1-2 | ¿Los descuentos llevan **centro de costo** como los ingresos? | **No** (D-13): el descuento es retención, no gasto de la empresa; la plaza basta como ancla. Confirmar |
| P-09 | 2 | **«Familias de ingresos y descuentos»**: ¿basta la clasificación existente (`Nature` Ingreso/Egreso + `DeductionClass` Ley/Interno/Externo) o se requiere catálogo explícito de familias asociado a los conceptos? | **F1: clasificación existente** (cero cambios a un catálogo consumido por ISSS/AFP/liquidación); catálogo explícito + columna aditiva solo si el negocio lo usa en reportes/agrupaciones |
| P-10 | 2 | «Tipos de descuentos»: ¿confirma reutilizar los conceptos país `Nature=Egreso` y sembrar los faltantes (`COOPERATIVA`, `PROCURADURIA` como Externo)? ¿Falta algún otro (p. ej. `ASOCIACION`)? | **Sí** — reutilización pura + siembra de faltantes en PR-1 (espejo D-03 de REQ-005/006) |
| P-11 | 3 | **Base de ingreso** del %: ¿Σ salario base mensual de plazas activas? ¿Bruto o neto? ¿Incluye ingresos cíclicos/eventuales vigentes? Factor de mensualización semanal (×4.33) | **Σ salario base bruto mensualizado de plazas activas** F1 (simple, verificable, siempre disponible); ingresos adicionales y neto = F2; semanal ×4.33 |
| P-12 | 3 | **Carga de deuda**: ¿solo cíclicos `VIGENTE` no estatutarios? ¿Suman los eventuales del periodo? ¿Los suspendidos? ¿ISSS/AFP/Renta? | **Solo cíclicos vigentes no estatutarios** F1 (cuota mensualizada); suspendidos visibles pero excluidos; eventuales y estatutarios fuera (los estatutarios no son endeudamiento voluntario) |
| P-13 | 3 | ¿La validación del límite se **adelanta** a REQ-008 (chequeo mínimo contra un % global) o espera a REQ-010 completo? | **Esperar a REQ-010**. **✔ Aceptada con instrucción: documentar lo que queda pendiente para la futura iteración** → gap anotado en D-16, en la sección «División en planes» y en el backlog de REQ-010 (lo registrado antes no se re-valida; solo aparece en la consulta) |
| P-14 | 3 | ¿La validación corre al **crear**, al **autorizar**, o ambas? | **Ambas** (la carga puede cambiar entre registro y decisión); la huella del override se estampa en cada punto que la exigió |
| P-15 | 3 | ¿Quién ve la consulta/simulación? ¿Permiso dedicado? ¿Consulta self del empleado? | **`ViewIndebtedness` dedicado** (dato agregado sensible); sin self en F1 (evaluable F2) |
| P-16 | 4 | **Registro de tiempo no trabajado**: el levantamiento no detalla campos ni flujo — ¿registro directo de RRHH (`REGISTRADA`) sin autorización, anulable? ¿O con decisión? | **Directo sin decisión** F1 (molde incapacidad — el hecho ya ocurrió); si el negocio exige aprobación, se añade el flujo de una decisión (aditivo) |
| P-17 | 4 | Semántica de **«aplica para permiso»** y **«tipo de ingreso»** del maestro: ¿clasificación para el futuro módulo de permisos y para el insumo con goce? | **Sí a ambas como clasificación** F1 (flags/snapshot sin lógica adicional); el módulo de solicitudes de permiso es levantamiento futuro |
| P-18 | 4 | **Séptimo**: ¿regla exacta — se descuenta el día de descanso de **cada semana** con ausencia computable (CT SV)? ¿Proporcional o día completo? | **+1 día completo de descanso por semana afectada** (regla simple defendible); **golden del contador** valida antes de construir |
| P-19 | 4 | ¿El registro de tiempo no trabajado **genera automáticamente un descuento eventual** o mantiene su propio insumo? | **Insumo propio** F1 (evita doble registro y doble estado); la generación automática hacia REQ-009 queda como costura F2 si se pide |
| P-20 | 4 | **Asiento** en el journal: ¿reutilizar el ActionType existente `PERMISO` (-9479, hoy sin uso) o crear `TIEMPO_NO_TRABAJADO` nuevo? | **Nuevo `TIEMPO_NO_TRABAJADO`** (el nombre del tipo viaja en el payload; `PERMISO` queda para el futuro módulo de solicitudes) |
| P-21 | 4 | **Marcación biométrica**: ¿se confirma fuera de alcance con la costura `origin` documentada? | **Sí** (no existe módulo de marcación; el levantamiento lo condiciona a adquirirlo) |
| P-22 | 1 | ~~**Adopción** de egresos `Externo` ya configurados~~ | **✖ ELIMINADA por el negocio**: «aún no tenemos producción; el frontend y la data son de prueba — **no fallbacks ni legacys**» → sin adopción/re-registro/migración; se retiró el flujo de adopción del análisis; la frontera editorial RN-16 basta hacia adelante |

---

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar P-01 (la división en 4 planes) antes que nada**: define el backlog (REQ-008…011), el orden y qué se le promete al cliente en cada entrega. Todo el documento está diseñado para que cada plan sea entregable por sí solo.
2. **Ejecutar REQ-008 y REQ-009 contiguos** (mismo patrón que REQ-005→006): comparten catálogos de conceptos, molde de flujo, bandejas gemelas y ganchos de liquidación — el segundo cuesta una fracción del primero.
3. **Los casos dorados del contador son el gate de REQ-008 y REQ-011** (amortización y séptimo): es la misma disciplina que hizo confiable la liquidación. No construir la calculadora sin la tabla bendecida (A.3).
4. **No inventar catálogos que ya existen**: «tipos de descuentos» = conceptos `Nature=Egreso`; frecuencias = `pay-periods`; planilla = `payroll-types`; moneda = `currencies`. Los únicos catálogos nuevos son los de estados/acciones/tipos-cíclicos (y familias **solo** si P-09 lo exige).
5. **Respetar la lección de `IsSystemCalculated`**: los dos conceptos de liquidación nuevos (`DESCUENTO_CICLICO_PENDIENTE`, `DESCUENTO_EVENTUAL_PENDIENTE`) van con **`false`** (línea manual sugerida, editable) — `true` es solo para líneas calculadas por el motor (caso `-9915`), que aquí no aplican.
6. **El endeudamiento debe nacer advertir-nunca-bloquear con huella**: el levantamiento lo pide literal («debe permitir… si se confirma la acción»); la auditoría del override es lo que protege a la empresa. Endurecer a bloqueo sería una preferencia F2, no un default.
7. **Mantener la frontera sin motor** (P-01 de REQ-005): bandeja de vencidas + lote + insumo son el corazón operativo; cuando llegue el motor, aplicará las mismas cuotas con origen `MOTOR` sin cambiar contratos.
8. **Plan 4 es la mejor válvula de paralelización**: no depende de los otros tres y ataca un dolor distinto (ausentismo); si hay dos frentes de trabajo, REQ-011 puede correr en paralelo a REQ-008.
9. **MVP recortable por plan**: en REQ-008, RF-001…RF-010 entregan el ciclo completo del crédito (los exports RF-011/012 y la liquidación RF-013 pueden ser segunda entrega); en REQ-010, la consulta (RF-022) puede salir antes que la simulación (RF-023).
10. **Comunicar las dos fronteras editoriales** en la guía FE y capacitación: configuración estructural vs descuento cíclico (RN-16) y registro documental de suspensión (REQ-003) vs tiempo no trabajado con cálculo (REQ-011) — son los dos puntos donde los usuarios pueden registrar en el lugar equivocado.

---

## Anexo A — Referencias y propuestas

### A.1 Mapa de la visión del módulo de planilla — actualización con este levantamiento

| Capacidad de la visión original | Estado tras REQ-005/006/007 | Cobertura de este levantamiento |
|---|---|---|
| Registrar todo tipo de **ingresos** | ✅ Construido (cíclicos + eventuales + jornadas de horas extras) | — |
| Registrar todo tipo de **descuentos** | Solo configuración estructural | **REQ-008 + REQ-009** (transaccional con saldo, cuotas, interés, extraordinarias) |
| Consultas de **nivel de endeudamiento** | No existe | **REQ-010** (parámetros + validación + consulta + simulación) |
| Creación de **periodos de planilla** | ✅ Construido (REQ-001) | Se consume (FK real) |
| **Tiempos no trabajados** | Solo incapacidades/vacaciones/suspensión documental | **REQ-011** (genérico con cálculo) |
| **Generar la planilla** (motor) | No existe (frontera ratificada) | **Sigue fuera** — levantamiento propio futuro; estos planes dejan el 100 % de los insumos transaccionales listos |
| Transacciones no aplicadas / enviar a otro periodo | Aproximación construida (bandejas + lote con exclusión) | Se extiende a descuentos y tiempos (mismo patrón) |

### A.2 Seeds tentativos por plan (piso verificado `-9915`; verificar IDs libres al abrir cada PR-1)

> Verificación 2026-07-12 (HEAD `ca94d8c`, sufijo `L` — la trampa de fragmentos de GUID en `Designer.cs`/snapshot no cuenta): ocupado hasta `-9915`; **libres `-9916…-9999`** (además de holguras internas `-9889`, `-9906…-9909` de bloques ya asignados a REQ-005/006 — no reutilizarlas).

- **REQ-008 (bloque `-9920…-9939`)**: `RECURRING_DEDUCTION_STATUS_CATALOG`: `EN_REVISION=-9920`, `VIGENTE=-9921`, `RECHAZADO=-9922`, `SUSPENDIDO=-9923`, `FINALIZADO=-9924`, `ANULADO=-9925` · `RECURRING_DEDUCTION_SETTLEMENT_ACTION_CATALOG`: `DESCONTAR_SALDO=-9926`, `CANCELAR=-9927` · concepto liquidación `DESCUENTO_CICLICO_PENDIENTE=-9928` (**clase Descuento, `IsSystemCalculated=false`, Affects*=false**) · `-9929` holgura · `RECURRING_DEDUCTION_TYPE_CATALOG`: `PRESTAMO_BANCARIO=-9930`, `PROCURADURIA=-9931`, `COOPERATIVA=-9932`, `ASOCIACION=-9933`, `OTRO=-9934` · `-9935…-9939` holgura. Además: conceptos país faltantes (`COOPERATIVA`, `PROCURADURIA` como `Nature=Egreso`/`Externo`) — continúan la serie de conceptos (`-9737`, `-9738` si libres; verificar).
- **REQ-009 (bloque `-9940…-9949`)**: `ONE_TIME_DEDUCTION_STATUS_CATALOG`: `EN_REVISION=-9940`, `AUTORIZADO=-9941`, `RECHAZADO=-9942`, `APLICADO=-9943`, `ANULADO=-9944` · concepto `DESCUENTO_EVENTUAL_PENDIENTE=-9945` (ídem `false`) · `-9946…-9949` holgura.
- **REQ-010 (bloque `-9950…-9959`)**: sin seeds propios salvo P-09 (familias: `-9950…-9954`); resto holgura. `MaxIndebtednessPercent` es columna de preferencia (sin seed); `IndebtednessLimit` es tabla por empresa (sin seeds globales).
- **REQ-011 (bloque `-9960…-9969`)**: `NOT_WORKED_TIME_STATUS_CATALOG`: `REGISTRADA=-9960`, `ANULADA=-9961` · ActionType `TIEMPO_NO_TRABAJADO=-9965` (si P-20 elige nuevo; si reutiliza, queda libre) · resto holgura. El maestro de tipos es entidad por empresa (plantilla `load-template`, sin seeds globales).

### A.3 Casos dorados sugeridos (validar con el contador antes de construir)

1. **Amortización francesa**: $1,000.00 al 12% nominal anual, 12 cuotas mensuales → i=1%, cuota = $88.85; cuota 1 = $10.00 interés + $78.85 capital; el saldo decrece hasta $0.00 exacto con ajuste en la cuota 12; Σ capital = $1,000.00.
2. **Plan por segmentos sin interés**: cuotas 1–6 × $50 + 7–12 × $75 → total $750; cobrado/no cobrado exactos tras 4 aplicaciones.
3. **Extraordinaria reduce plazo**: sobre el préstamo del caso 1 con 3 cuotas aplicadas, abono de $200 → tabla regenerada desde el saldo; el plazo cae (≈2.4 cuotas menos); Σ capital sigue siendo $1,000.00.
4. **Payoff**: extraordinaria por el saldo exacto → `FINALIZADO` automático; anularla → reapertura con saldo restituido.
5. **Meses de excepción**: plan mensual iniciado en octubre con diciembre exceptuado → la cuota 3 cae en enero (el plan se corre, no se pierde).
6. **División por frecuencia de aplicación**: cuota mensual $100 con aplicación quincenal → 2×$50.00; $33.33/$33.34 en montos impares (última parte ajusta).
7. **Endeudamiento**: salario $1,200 (2 plazas: $800+$400), carga $340 → 28.33%; nueva cuota $80 → 35.00% vs límite 30% → advertencia con desglose; override → huella.
8. **Simulación**: mismos números con ingreso digitado $1,500 → 28.00%; nada persiste.
9. **Tiempo no trabajado con séptimo**: ausencia sin goce lunes–miércoles (tipo excluye sábado/domingo/asueto, séptimo sí) → 3 días computables + 1 descanso = 4 × salario/30; salario $600 → $80.00.
10. **Tiempo por horas**: 6h con jornada 8h y 100% → 0.75 × $20.00 (salario $600/30) = $15.00.
11. **Con goce**: mismo rango con tipo 0% → monto $0.00, registro visible en disponibilidad.
12. **Liquidación**: saldo $300 `DESCONTAR_SALDO` → línea de deducción $300 en el finiquito (neto la absorbe; si neto < 0 → warning); `CANCELAR` → condonación al emitir; anular liquidación → reapertura.

### A.4 División en planes — PRs sugeridos (insumo del plan técnico de cada REQ)

**REQ-008 — Descuentos cíclicos (~6 PRs, molde REQ-005 + deltas)**
- PR-1 Configuración (M1): 3 catálogos + concepto `-9928` + conceptos país faltantes + 3 permisos/policies + governance.
- PR-2 Dominio + reglas (M2): entidad + segmentos + cuota hija; `RecurringDeductionRules` (segmentos, proyección con meses de excepción y frecuencia de aplicación) + **motor de amortización** con golden del contador (gate).
- PR-3 Flujo end-to-end: CRUD + resolución dedicada (anti-self doble) + suspensión/revocación/cierres.
- PR-4 Aplicación: unitaria + lote por periodo con exclusión + **extraordinarias/payoff** + anulación + tests de carrera.
- PR-5 Consultas: historial/amortización, bandejas (descuentos + pendientes/vencidas), exports + insumo.
- PR-6 Integración liquidación (canal sugerencias clase Descuento) + openapi + guía FE.

**REQ-009 — Descuentos eventuales (~5 PRs, molde REQ-006)**
- PR-1 Configuración (estados + concepto `-9945` + permisos) · PR-2 Dominio + reglas (fijo/factores, golden) · PR-3 Flujo (anti-self **triple**, re-imputación) · PR-4 Aplicación (lote + reversión + carrera) · PR-5 Búsqueda/exports/insumo + liquidación + guía FE.

**REQ-010 — Endeudamiento (~3 PRs)**
- PR-1 Parámetros (preferencia por PUT + tabla `IndebtednessLimit` + permisos) · PR-2 Motor `IndebtednessRules` (base, mensualización, límite aplicable — golden) + **hooks** en crear/autorizar de REQ-008(/009 según P-12) con override auditado · PR-3 Consulta + simulación (no-escritura) + exports + guía FE.

**REQ-011 — Tiempos no trabajados (~4 PRs)**
- PR-1 Maestro + plantilla + `load-template` + catálogo de estados + permisos · PR-2 Dominio + `NotWorkedTimeRules` (scan de días, séptimo, horas — golden del contador) · PR-3 Registro end-to-end (cálculo + asiento + anulación) · PR-4 Bandeja/insumo/exports + fuente de disponibilidad de tiempo + guía FE.
