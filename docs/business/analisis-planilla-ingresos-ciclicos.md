# Análisis de negocio — Módulo de planilla: ingresos cíclicos (registro, autorización e historial de pago)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | Planilla — rebanada 1: **ingresos cíclicos** (alta con plan de cuotas + flujo de autorización + aplicación por periodo + historial de pago), con el **mapa de la visión completa del módulo de planilla** (periodos, generación, endeudamiento, transacciones pendientes) documentado y faseado (Anexo A.1) |
| **Fecha** | 2026-07-05 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código |
| **Estado** | **RATIFICADO por el negocio (2026-07-05)** — decisiones **D-01…D-18 ratificadas** (D-03/D-12 ajustadas por las respuestas) y **P-01…P-15 respondidas** (§17). Cierres clave: **P-01** el motor de planilla **se realizará aparte** (F1 sin motor confirmada) · **P-02** se requieren **ambos catálogos**: «tipo ingreso» = catálogo de tipos de ingreso (Salario, Aguinaldo, horas extras, vacaciones, bonificaciones… — reutiliza conceptos país) y «tipo» = **catálogo nuevo de tipos de ingresos cíclicos** (prestaciones permanentes independientes del salario: ayuda para alimentación, gastos de representación, combustible…) · **P-15** centro de costo **obligatorio y relacionado a la plaza** del empleado |
| **Naturaleza del módulo** | Semi-greenfield **sobre una frontera estratégica**: la **configuración** de ingresos/egresos ya existe (`PersonnelFileCompensationConcept` + catálogos); lo pedido añade la capa **transaccional** (registro cíclico con cuotas, autorización, historial) — construible hoy — y una **visión de motor de planilla interno que NO existe** y que contradice el corte «la nómina se procesa fuera de CLARIHR» ratificado repetidamente en módulos previos |
| **Documentos hermanos** | [`analisis-plazas-ingresos-egresos.md`](analisis-plazas-ingresos-egresos.md) (la configuración de conceptos que este módulo consume; su D-08 declara el cálculo como «módulo futuro de nómina») · [`analisis-transacciones-fuera-nomina-empleado.md`](analisis-transacciones-fuera-nomina-empleado.md) (precedente de movimiento monetario editable con periodo Año/Mes y corrección por contra-asiento) · [`analisis-vacaciones-incapacidades-empleado.md`](analisis-vacaciones-incapacidades-empleado.md) (REQ-001 — especifica el **maestro de periodos de planilla por empresa**) · [`analisis-tablero-acciones-personal.md`](analisis-tablero-acciones-personal.md) (REQ-004 — especifica el **catálogo país de tipos de planilla**) |
| **Documentos relacionados** | `analisis-ayuda-economica-empleado.md` (molde del flujo de una decisión + anti-autoaprobación) · `analisis-liquidacion-empleado.md` (motor de liquidación y su mecanismo de líneas sugeridas — gancho de la «acción de liquidación») · `plan-tecnico-tiempo-compensatorio.md` (precedente de integración quirúrgica con liquidación) · [`plan-tecnico-planilla-ingresos-ciclicos.md`](../technical/plan-tecnico-planilla-ingresos-ciclicos.md) (plan de implementación de este módulo) |

---

## Contexto del cambio (requerimiento original)

El levantamiento describe un **módulo de planilla** que debe permitir: registrar todo tipo de **ingresos y descuentos** de los empleados, **consultas de nivel de endeudamiento**, **creación de periodos de planilla** y **generar la planilla**. Deberá permitir consultar **transacciones que no se aplicaron en planilla**, así como las acciones que se quieren aplicar en el cálculo del **periodo de planilla activo** y las que se quieren **enviar a otro periodo**.

Las dos opciones **detalladas** del levantamiento son:

1. **Nuevo ingreso cíclico** — aplicar los ingresos cíclicos **adicionales** que estará recibiendo un empleado específico. Información principal: **estado del ingreso, fecha, referencia, empleado, tipo (catálogo), tipo ingreso (catálogo), centro de costo, observaciones**. Información de las **cuotas**: **fecha de inicio, moneda (catálogo), tipo de planilla, frecuencia de cuotas (catálogo), monto y número de cuotas indefinido (sí/no), valor de cuota, acción de liquidación (catálogo)**. Se deberá considerar el **flujo de autorización** del ingreso cíclico.
2. **Consultar el historial de pago de un ingreso cíclico** — consultar el **histórico de cuotas calculadas en planillas de pago**.

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **El levantamiento pisa una frontera estratégica del producto.** La visión del módulo (periodos de planilla + **generar la planilla** + pool de transacciones pendientes aplicables al «periodo activo») es un **motor de nómina interno**, y ese motor **no existe ni fue nunca alcance**: el corte «la nómina se procesa fuera de CLARIHR; CLARIHR configura, registra y exporta» está ratificado en cadena — D-08 de plazas ingresos/egresos («el cálculo es módulo futuro de nómina»), RN-14 de REQ-003 («no se escriben ledgers de planilla»), G-06 de REQ-002, y comentarios de dominio en `PersonnelFileCompensation.cs:8-10`. **Las dos opciones detalladas (ingreso cíclico + historial de pago) sí son construibles hoy** sin el motor; el motor es un programa aparte que exige su propio levantamiento. → **P-01 (decisión estructural)** con recomendación: F1 = ingresos cíclicos end-to-end **sin motor** (aplicación de cuotas manual por periodo + exportación insumo); el motor de planilla = levantamiento futuro (mapa Anexo A.1).
2. **La configuración de ingresos recurrentes ya existe; lo pedido es la capa transaccional que le falta.** `PersonnelFileCompensationConcept` (mergeado) ya modela ingresos/egresos por empleado y/o plaza con naturaleza, monto fijo o %, base de cálculo, **moneda**, **periodicidad** (catálogo `pay-periods`: `MENSUAL`/`QUINCENAL`/`SEMANAL`/`UNICA`) y vigencia — pero **como definición estructural, sin estados, sin autorización, sin cuotas contadas, sin historial de aplicación** (verificado: cero entidades de installments/amortización/ingreso cíclico). El nuevo módulo NO debe enriquecer ese concepto (lo consumen la sugerencia ISSS/AFP, las reglas de salario y el motor de liquidación) sino crear la **entidad transaccional del expediente** con plan de cuotas, reutilizando el catálogo país de conceptos (`compensation-concept-types`, `Nature=Ingreso`) como «tipo ingreso» (D-02/D-03).
3. **«Creación de periodos de planilla» ya está especificada en REQ-001** — no se duplica: `PayrollPeriodDefinition` (maestro por empresa: tipo de periodo validado contra `PAY_PERIOD_CATALOG`, año, número, rango de fechas, sin plantilla — cada empresa carga su calendario; `plan-tecnico-vacaciones-incapacidades.md` §3.1) cubre el **calendario** de periodos. Lo que no existe es el **periodo activo con corrida de cálculo** (motor). Propuesta: la cuota aplicada referencia opcionalmente `PayrollPeriodDefinition` (mismo par `PayrollTypeCode?` + `PayrollPeriodId?` que REQ-001 usa en incapacidades), con degradación a etiqueta si este módulo se adelantara a REQ-001 (D-10/D-17).
4. **«Tipo de planilla» hoy es texto libre y su catálogo ya está especificado en REQ-004 — con una colisión de seeds detectada en esta validación.** `PayrollTypeCode` vive en la plaza como string libre (max 80, `PersonnelFileEmployee.cs:187`); REQ-004 especifica `PAYROLL_TYPE_CATALOG` (`MENSUAL`/`QUINCENAL`/`SEMANAL`/`POR_DIA`/`POR_OBRA`/`OTRO`) con IDs tentativos `-9520…-9525` — **verificado: ese bloque YA está ocupado en código por `ECONOMIC_AID_TYPE_CATALOG` (`GlobalCatalogSeedData.cs:539-544`, `-9520…-9526`)**. El catálogo debe **re-ubicarse** (propuesta: `-9890…-9895`, Anexo A.2); sigue habiendo **una sola definición** (la de REQ-004) y la siembra el REQ que se construya primero. Este módulo lo consume en las cuotas.
5. **El «flujo de autorización» pedido se resuelve con el molde probado, no con un motor nuevo**: flujo de **una decisión** con estado híbrido (constantes canónicas + catálogo país editable), PATCH de decisión, **anti-autoaprobación doble** (ni el empleado sujeto ni quien registró) y permiso `Authorize*` con `RequireAssertion` que **excluye Admin** (molde ayuda económica + `AuthorizeRetirement`, mismo corte que REQ-003 D-03/D-04). Multi-nivel configurable = F2 si se ratifica (P-12).
6. **El «historial de cuotas calculadas en planillas» no puede nacer del cálculo (no hay motor): nace de la aplicación.** Propuesta F1: el plan de cuotas **proyectado** es derivado (fecha inicio + frecuencia + valor + número/indefinido); la cuota se **persiste al aplicarse** (unitaria o en lote por periodo, por RRHH/planilla, con lock anti-carrera), con snapshot de monto/moneda/periodo y origen `MANUAL`; el historial de pago es la serie de cuotas aplicadas + las proyectadas pendientes. Cuando llegue el motor (F2), este aplica las cuotas con origen `MOTOR` **sobre el mismo modelo** — el contrato del historial no cambia (D-07/D-08/D-09). La exportación de **insumo por periodo** (cuotas pendientes del periodo, por tipo de planilla) es el puente con la planilla externa mientras tanto.
7. **«Consultar transacciones que no se aplicaron en planilla» y «aplicar al periodo activo / enviar a otro periodo» tienen aproximación F1 honesta**: bandeja de **cuotas pendientes/vencidas no aplicadas** con filtros por periodo/tipo de planilla, y aplicación **en lote por periodo con exclusión** (excluir una cuota del lote = «enviarla a otro periodo»; queda pendiente y se aplica después). El concepto pleno de «periodo activo» pertenece al motor (F2) — se documenta la degradación (D-08, RF-011).
8. **«Acción de liquidación» tiene gancho real ya construido**: el motor de liquidación (mergeado, PR #56) arma **líneas sugeridas** desde las instancias de compensación de la plaza (`SettlementRepository.GetCalculationContextAsync` → `BONO_PENDIENTE`/`COMISION_PENDIENTE`/`DESCUENTO_EXTERNO`) y REQ-002 ya planificó la línea condicional de sistema (`HORAS_EXTRAS_PENDIENTES`). Propuesta: catálogo país de acciones (`PAGAR_SALDO` / `CANCELAR`); al liquidar, los ingresos cíclicos vigentes con `PAGAR_SALDO` sugieren su saldo pendiente como línea (editable, snapshot); con `CANCELAR` no sugieren nada y el registro se finaliza (D-11, RF-013).
9. **«Nivel de endeudamiento» y los descuentos cíclicos NO están detallados en este levantamiento y no existen en el código** (verificado: sin `Loan`/préstamos/saldos/amortización; el egreso `Externo` del concepto es descuento recurrente **sin saldo**). El endeudamiento requiere el lado egresos con saldo/cuotas — es la **siguiente rebanada natural** del módulo (mismo modelo con `Nature=EGRESO` + umbral por empresa). Propuesta: diseñar la entidad **preparada para naturaleza** (precedente D-01 del concepto unificado) y entregar en F1 solo ingresos, como pide el levantamiento (D-02, P-10).
10. **Nada de este módulo toca el ledger inmutable**: `PersonnelFilePayrollTransaction` sigue siendo la bitácora de sincronización externa (POST unitario, PATCH solo `isActive`, trampa documentada) y el **destino del cálculo futuro**; correlacionar cuotas contra ese ledger queda fuera de F1 (P-09).

### Trazabilidad campo a campo del levantamiento

| Campo pedido | Cobertura propuesta |
|---|---|
| Estado del ingreso | Estado del **flujo** (no digitable): `EN_REVISION → VIGENTE / RECHAZADO` + `SUSPENDIDO` (P-03 ratificada) + `FINALIZADO` (automático) + `ANULADO` — híbrido canónico+catálogo (D-04) |
| Fecha | Fecha de registro/otorgamiento (≤ hoy, RN-09) |
| Referencia | Texto libre (acuerdo, acta, contrato que origina el ingreso) |
| Empleado | Expediente (`personnel-files/{publicId}/…`), con **plaza obligatoria** (default: principal; D-12/P-15) |
| Tipo (catálogo) | **Catálogo nuevo de tipos de ingresos cíclicos** (P-02 ratificada): prestaciones permanentes independientes del salario — ayuda para alimentación, gastos de representación, combustible… (plantilla A.2) |
| Tipo ingreso (catálogo) | **Reutiliza** el catálogo país `compensation-concept-types` con `Nature=Ingreso` + snapshot del nombre (D-03) — cubre Salario, Aguinaldo, horas extras, vacaciones, bonificaciones… (P-02) |
| Centro de costo | **Reutiliza** la entidad `CostCenter`; **obligatorio y derivado de la plaza del empleado** (P-15 ratificada; D-12) |
| Observaciones | Notas |
| Cuotas: fecha de inicio | Fecha de la primera cuota (retroactiva permitida — P-13) |
| Cuotas: moneda (catálogo) | **Reutiliza** `CurrencyCatalogItem` (multi-moneda sin FX, D-13) |
| Cuotas: tipo de planilla | **Reutiliza** el catálogo `PAYROLL_TYPE_CATALOG` especificado por REQ-004 (con re-ubicación de seeds — hallazgo №4) |
| Cuotas: frecuencia (catálogo) | **Reutiliza** `PAY_PERIOD_CATALOG` (`MENSUAL`/`QUINCENAL`/`SEMANAL`/`UNICA`) (D-13) |
| Cuotas: monto y nº de cuotas indefinido (sí/no) | Flag `isIndefinite`; si finito: valor de cuota + (número de cuotas **o** monto total) con coherencia y ajuste en la última (D-07, P-04) |
| Cuotas: valor de cuota | Monto de cada cuota (> 0) |
| Cuotas: acción de liquidación (catálogo) | Catálogo país nuevo `PAGAR_SALDO`/`CANCELAR` + gancho al motor de liquidación (D-11) |
| Flujo de autorización | Flujo de una decisión + anti-autoaprobación doble + `Authorize*` sin Admin (D-04/D-05) |
| Historial de pago | Cuotas **aplicadas** persistidas (snapshot, origen, periodo) + proyección de pendientes + saldos derivados (D-09, RF-009) |

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Dónde | Uso en este módulo |
|---|---|---|
| Configuración unificada de ingresos/egresos: `PersonnelFileCompensationConcept` (naturaleza, fijo/%, base de cálculo, `EmployerRate`, tope, moneda, `PayPeriodCode`, vigencia, ámbito empleado/plaza, `IsSystemSuggested`) | `PersonnelFileCompensation.cs`; `Compensation/CompensationConcepts.*` | **Frontera documentada** (D-03): sigue siendo la definición estructural (salario, ley); el ingreso cíclico es la transacción con cuotas. No se modifica |
| Catálogo país de conceptos: `CompensationConceptTypeCatalogItem` (`Nature` Ingreso/Egreso, `IsStatutory`, defaults de cálculo/tasas, `IsBaseSalary`) + GET `api/v1/compensation-concept-types?nature=` | `Compensation/CompensationConceptTypeCatalogItem.cs`; `CompensationConceptTypesController.cs` | **«Tipo ingreso»** del formulario: conceptos activos `Nature=Ingreso`, con snapshot del nombre al registrar (D-03) |
| Catálogo de monedas `CurrencyCatalogItem` (multi-moneda sin conversión) y de frecuencias `PayPeriodCatalogItem` (`MENSUAL`/`QUINCENAL`/`SEMANAL`/`UNICA`, seeds `GlobalCatalogSeedData.cs:985`) | `GeneralCatalogItems.cs:714,742` | «Moneda» y «frecuencia de cuotas» del bloque de cuotas — **cero catálogos nuevos** para ambos (D-13) |
| Centros de costo: entidad dedicada por empresa `CostCenter` (código, nombre, tipo, **cuentas contables de planilla**: gasto/patronal/provisión) usada por la plaza (`CostCenterPublicId`) y snapshoteada en liquidación | `CostCenters/CostCenter.cs`; `CostCentersController.cs` | «Centro de costo» del ingreso cíclico: FK + snapshot, default el de la plaza (D-12) |
| Plaza con `PayrollTypeCode` (texto libre, max 80, nullable) | `PersonnelFileEmployee.cs:187`; config `:72` | El «tipo de planilla» de las cuotas usa el **catálogo** (REQ-004) — y hereda como default el de la plaza si está clasificada |
| Maestro de **periodos de planilla por empresa** `PayrollPeriodDefinition` (tipo, año, número, rango; único por tenant/tipo/año/número; sin solape) — **especificado en REQ-001, aún no construido** | `plan-tecnico-vacaciones-incapacidades.md` §3.1 | Referencia opcional de imputación de la cuota aplicada (`PayrollPeriodId?` + snapshot), mismo par que la incapacidad de REQ-001 (D-10) |
| Catálogo país de **tipos de planilla** `PAYROLL_TYPE_CATALOG` (6 valores A.2) — **especificado en REQ-004, aún no construido; seeds tentativos con COLISIÓN** (ver hallazgo №4) | `plan-tecnico-tablero-acciones-personal.md` §3.1/§4 | «Tipo de planilla» de las cuotas: misma única definición; la siembra el REQ que se construya primero (D-16) |
| Flujo de una decisión con estado híbrido + anti-autoaprobación (`SelfApprovalForbidden` 403 cuando `LinkedUserPublicId == decidedByUserId`) + PATCH de resolución con If-Match | `PersonnelFileEmployee.cs:1678-1880` (ayuda económica); `EconomicAidRequests.Handlers.cs:371-384` | Molde exacto del «flujo de autorización del ingreso cíclico» (D-04/D-05) |
| Permisos `Authorize*` que **excluyen Admin** vía `RequireAssertion` (molde `AuthorizeRetirement`, comentario explícito `Program.cs:550-554`) + receta estándar `View*`/`Manage*` | `ProvisioningConstants.cs:33-93`; `Program.cs:557-575` | `AuthorizeRecurringIncomes` con el mismo corte (D-05/D-06) |
| Patrón estado canónico + catálogo país editable (dominio valida transiciones con constantes; handler valida existencia/actividad vía `CatalogCodeIsActiveAsync`) | `EconomicAidRequestStatuses`; `GlobalCatalogSeedData.cs:548+` | Estados del ingreso cíclico (D-04); el plan técnico decide catálogo general vs TPH según lo construido al arrancar |
| Movimiento monetario editable de referencia: `PersonnelFileOffPayrollTransaction` (monto no-cero, periodo `Year`/`Month`, **corrección por contra-asiento** `CorrectsTransactionPublicId`, totales por moneda, adjuntos) | `PersonnelFileEmployee.cs:1442`; `OffPayrollTransactionsController.cs` | Precedente de estilo del registro monetario y de la anulación/corrección de cuotas (P-08) |
| Ledger inmutable de sincronización externa `PersonnelFilePayrollTransaction` (`PayrollPeriodCode` texto libre, `SourceSystem/SourceReference/SourceSyncedUtc`; PATCH solo `isActive`) | `PersonnelFileEmployee.cs:645`; `PersonnelFileCompensationController.cs:368+` | **No se toca** (RN-14): sigue siendo la bitácora externa y el destino del cálculo futuro |
| Motor de liquidación (mergeado PR #56): 11 pasos puros, líneas con `UnitsOrDays`/`UnitsOverridden`/`OverrideAmount`/`FinalAmount`, **líneas sugeridas** desde compensación de la plaza (`SuggestedPlazaItem` → `BONO_PENDIENTE`/`COMISION_PENDIENTE`/`DESCUENTO_EXTERNO`) y plan REQ-002 de línea condicional de sistema | `SettlementCalculation.Rules.cs:70-74,300-336,657`; `SettlementRepository.cs:126-152` | Gancho de la **«acción de liquidación»**: saldo pendiente con `PAGAR_SALDO` → línea sugerida en la liquidación (D-11, RF-013) |
| Bandeja + exportación tabular (query paginada `StatusCounts`, export xlsx/csv/json con rate limiting, filas en español) | `SettlementsBandeja.cs`; `ReportExportFileWriter.cs` | Bandeja de ingresos cíclicos + bandeja de cuotas pendientes + insumo de planilla (RF-010/011/012) |
| Gates de autogestión (`LinkedUserPublicId`, ramas `LoadForCreateOwnOrManage*`) y preferencia por empresa (columna nullable en `CompanyPreference` + setter + PATCH admin) | `PersonnelFileEmployeeHandlerBases.cs`; `CompanyPreference.cs:34,74-83` | Lectura self si se ratifica (P-11); umbral de endeudamiento futuro como preferencia (P-10) |
| Convenciones API: `api/v1`, If-Match (faltante→400, obsoleto→409) + ETag rotativo, enums string, errores bilingües `extensions.code`, sub-recursos con `parentConcurrencyToken` | `ProblemDetailsFactory.cs:42-61`; `FromIfMatchAttribute.cs` | Aplican a todos los endpoints nuevos (RNF) |

### Lo que NO existe (verificado exhaustivamente en `src/`, tests y contrato)

- Ningún **motor/periodo de planilla**: sin `PayrollPeriod`, `PayrollRun`, `PayrollCalculation`, `PayrollEngine`, workers ni corridas periódicas. El único motor de cálculo es la **liquidación de salida** (finiquito), no una planilla periódica.
- Ninguna entidad de **ingreso cíclico / cuotas / amortización / installments** (sin `RecurringIncome`, `InstallmentPlan`, `AmortizationSchedule`, `CyclicIncome`).
- Ningún modelo de **préstamos / saldos / nivel de endeudamiento** (el egreso `Externo` del concepto es «descuento recurrente sin saldo/amortización» — D-09 de plazas ingresos/egresos, explícito).
- Ningún **catálogo de tipos de planilla** en código (`payroll_type_code` es texto libre en la plaza; el catálogo está solo especificado en REQ-004, con seeds tentativos colisionados — hallazgo №4).
- Ningún maestro de **periodos de planilla** en código (especificado en REQ-001, no construido).
- Ninguna **sincronización masiva** del ledger externo (`PersonnelFilePayrollTransaction` solo admite POST unitario) ni correlación cuota↔transacción externa.
- Ningún **motor de aprobaciones genérico/multi-nivel** (hallazgo repetido desde el análisis de sustituciones); el máximo existente es el flujo de una decisión.
- **REQ-001…REQ-004 no están construidos** (planes escritos): los periodos de planilla y el catálogo de tipos de planilla que este módulo referencia llegan con ellos — o los siembra este módulo si se adelanta (D-16/D-17).
- **Espacio de seeds**: piso real del catálogo general **-9846**; reservas de planes: REQ-001 `-9850…-9862` y `-9485…-9489`, REQ-002 `-9865…-9871`, REQ-003 `-9875…-9879`; trampa vigente `-9490…-9496` (`ACTION_STATUS_CATALOG`); **colisión detectada**: REQ-004 tentativo `-9520…-9525` vs `ECONOMIC_AID_TYPE_CATALOG` (`-9520…-9526`, en código). **Siguiente bloque libre: `-9880` en adelante** (Anexo A.2).

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha detectada | Propuesta de resolución |
|---|---|---|
| G-01 | La visión pide **generar la planilla** y no existe motor de nómina (frontera ratificada en cadena: la nómina es externa) | **Fasear como programa** (Anexo A.1): F1 entrega las dos opciones detalladas sin motor (registro + autorización + cuotas + aplicación manual + insumo exportable); el motor (periodo activo, pool, corrida de cálculo) exige su propio levantamiento y ratificación estratégica (P-01) |
| G-02 | «Ingresos cíclicos» sin entidad: la configuración existente (`CompensationConcept`) no tiene estados, autorización, cuotas ni historial | Nueva entidad transaccional del expediente (`PersonnelFileRecurringIncome`) con plan de cuotas y flujo de una decisión; **frontera documentada** con la configuración (D-02/D-03): el concepto define compensación estructural; el ingreso cíclico registra el adicional finito/indefinido con trazabilidad |
| G-03 | «Historial de cuotas **calculadas** en planillas» sin nada que calcule | La cuota se **persiste al aplicarse** (manual F1, motor F2 sobre el mismo modelo, campo `origen`); el historial = aplicadas + proyectadas; exportación de insumo por periodo como puente con la planilla externa (D-08/D-09) |
| G-04 | «Tipo de planilla» es texto libre; el catálogo especificado (REQ-004) tiene seeds colisionados | Una sola definición del catálogo (la de REQ-004) **re-ubicada a bloque libre** (propuesta `-9890…-9895`); la siembra el REQ que arranque primero; este módulo valida las cuotas contra él (D-16) |
| G-05 | «Periodo de planilla» de la cuota sin maestro construido (REQ-001 lo especifica) | Referencia **opcional** `PayrollPeriodId?` + snapshot de etiqueta (par idéntico al de incapacidades REQ-001); si este módulo se adelanta, la imputación degrada a etiqueta libre + tipo de planilla (D-10/D-17) |
| G-06 | «Flujo de autorización» sin motor genérico | Flujo de **una decisión**: `EN_REVISION → VIGENTE / RECHAZADO` (+`ANULADO`; `SUSPENDIDO` P-03; `FINALIZADO` automático), anti-autoaprobación **doble**, `Authorize*` excluye Admin — molde ayuda económica/REQ-003 (D-04/D-05) |
| G-07 | «Acción de liquidación» sin catálogo ni semántica definida | Catálogo país nuevo (`PAGAR_SALDO`/`CANCELAR`) **canónico** (el gancho de liquidación consume los códigos): saldo pendiente sugerido como línea de liquidación o cierre sin pago (D-11, RF-013, P-06) |
| G-08 | «Transacciones no aplicadas» y «enviar a otro periodo» presuponen el pool del motor | Aproximación F1 honesta: bandeja de **cuotas pendientes/vencidas** + aplicación **en lote por periodo con exclusión** (excluir = posponer a otro periodo); semántica plena de «periodo activo» = F2 (RF-011) |
| G-09 | «Nivel de endeudamiento» sin descuentos con saldo (no existen préstamos/amortización) | Fuera de F1; **siguiente rebanada**: descuentos cíclicos espejo (`Nature=EGRESO`) + saldo + umbral por empresa (preferencia) → la consulta de endeudamiento se construye sobre eso (P-10, Anexo A.1) |
| G-10 | Doble vía potencial: un bono recurrente podría registrarse como concepto de compensación o como ingreso cíclico | Regla editorial + validación blanda: el concepto es **estructural/permanente** (salario, ley, beneficios fijos de la plaza); el ingreso cíclico es **adicional, finito o revocable, con autorización y cuotas**; guía FE/UI lo comunica; sin bloqueo duro en F1 (RN-16) |

---

## Decisiones — D-01…D-18 (**RATIFICADAS por el negocio, 2026-07-05**)

> ✅ **Ratificadas junto con las respuestas P-01…P-15** (§17). Ajustes respecto del borrador: **D-03** incorpora el catálogo de **tipos de ingresos cíclicos** con la semántica definida por el negocio (P-02) y **D-12** endurece el centro de costo a **obligatorio y ligado a la plaza** (P-15). El resto se ratificó tal como se propuso — en particular **P-01: el motor de planilla se realizará aparte**.

| # | Tema | Decisión propuesta |
|---|---|---|
| D-01 | Fases | **F1 (este análisis)**: ingresos cíclicos end-to-end sin motor — catálogos/estados/permisos + alta con plan de cuotas + flujo de una decisión + aplicación de cuotas manual (unitaria/lote por periodo) + historial de pago + bandejas/exports (incluido insumo de planilla externa) + gancho de acción de liquidación. **F2+ (programa planilla, Anexo A.1)**: descuentos cíclicos + endeudamiento; periodo activo + pool de pendientes + **motor de generación**; correlación con ledger externo; multi-nivel de autorización |
| D-02 | Modelado | **Entidad transaccional del expediente** `PersonnelFileRecurringIncome` + cuotas hijas `…Installment` — NO se enriquece `PersonnelFileCompensationConcept` (lo consumen sugerencia ISSS/AFP, reglas de salario y liquidación) ni se tocan ledgers. Modelo **preparado para naturaleza** (los descuentos cíclicos de la siguiente rebanada reutilizan la maquinaria de cuotas; el plan técnico decide tabla unificada con `Nature` vs espejo — precedente D-15 de REQ-003) |
| D-03 | «Tipo ingreso» y «tipo» (P-02 ratificada) | **Dos catálogos**: (a) «**Tipo ingreso**» = referencia al **catálogo país existente** `compensation-concept-types` con `Nature=Ingreso` (activo) + **snapshot** del código y nombre al registrar — es el catálogo general de tipos de ingreso del negocio (Salario, Aguinaldo, horas extras, vacaciones, bonificaciones…; cero catálogos monetarios nuevos, precedente G-02 de REQ-003; verificar en PR-1 que los ejemplos del negocio existan como filas sembradas/creables). (b) «**Tipo**» = catálogo país editable **nuevo** `RECURRING_INCOME_TYPE_CATALOG` de **tipos de ingresos cíclicos**: los ingresos **permanentes** aplicables en las planillas, independientes del salario, por prestaciones — plantilla A.2: `AYUDA_ALIMENTACION`, `GASTOS_REPRESENTACION`, `COMBUSTIBLE`, `OTRO` (editable). La frontera con la configuración estructural queda documentada (G-10/RN-16) |
| D-04 | Flujo y estados | **Una decisión**: crear → `EN_REVISION` (editable); decidir → `VIGENTE` (autorizado, empieza a devengar cuotas) o `RECHAZADO` (motivo obligatorio); `ANULADO` desde `EN_REVISION` (retiro del trámite) o desde `VIGENTE` (revocación con motivo; las cuotas ya aplicadas quedan intactas); `FINALIZADO` **automático** al completarse el plan finito (o por cierre manual de un indefinido); `SUSPENDIDO`/reanudación como estado de pausa (**P-03 ratificada** — ciclo de 6 estados). Patrón híbrido (constantes canónicas + catálogo país editable) |
| D-05 | Anti-autoaprobación | **Doble** (precedente D-04 de REQ-003): quien decide no puede ser (a) el empleado sujeto del expediente (`LinkedUserPublicId`) ni (b) quien registró (`RegisteredByUserId`) → 403 `SELF_APPROVAL_FORBIDDEN` |
| D-06 | Permisos | 3 codes: `PersonnelFiles.ViewRecurringIncomes` / `ManageRecurringIncomes` / `AuthorizeRecurringIncomes`. `View`/`Manage` con receta estándar (fallback `Admin`/`ManageAdministration`); **`Authorize*` con `RequireAssertion` que excluye Admin** (molde `AuthorizeRetirement`). Alternativa de colapso con `ViewCompensation` a decidir en ratificación (P-14) |
| D-07 | Plan de cuotas | Campos: fecha de inicio, moneda, tipo de planilla, frecuencia, **`isIndefinite`**, **valor de cuota (> 0, obligatorio)** y — solo si finito — **número de cuotas o monto total** (el tercero se deriva; coherencia validada; la **última cuota ajusta** el remanente de redondeo/división). Indefinido: solo valor + frecuencia, corre hasta cierre/anulación/retiro. El **plan proyectado es derivado** (no se persisten filas futuras): fechas teóricas = inicio + frecuencia (P-04/P-05) |
| D-08 | Aplicación de cuotas (F1) | **Manual por RRHH/planilla** con `ManageRecurringIncomes`: unitaria o **en lote por periodo** (filtro tipo de planilla + periodo/rango; exclusión por cuota = posponer). Bajo **lock anti-carrera** (precedente `pg_advisory_xact_lock` de REQ-002) y con re-verificación de estado `VIGENTE`. El motor (F2) aplicará con origen `MOTOR` sobre el mismo modelo, sin cambio de contrato |
| D-09 | Historial de pago | La cuota aplicada se **persiste** con: número de secuencia, fecha de aplicación, monto (última ajusta), moneda, tipo de planilla, referencia de periodo (`PayrollPeriodId?` + etiqueta snapshot), origen (`MANUAL`/`MOTOR`), quién aplicó. **Inmutable** salvo anulación con motivo (estado `ANULADA`, sin borrado físico; el saldo la reintegra) (P-08). El historial del ingreso = cuotas aplicadas + proyección de pendientes + derivados (cuotas aplicadas, monto aplicado, saldo restante si finito) |
| D-10 | Periodo de imputación | Par `PayrollTypeCode` (catálogo REQ-004) + `PayrollPeriodId?` (FK al maestro REQ-001) + etiqueta snapshot — espejo del par de incapacidades REQ-001. Si el maestro no existe al construir (adelanto), degrada a etiqueta libre documentada (D-17) |
| D-11 | Acción de liquidación | Catálogo país nuevo **canónico** `RECURRING_INCOME_SETTLEMENT_ACTION_CATALOG`: `PAGAR_SALDO` (al liquidar al empleado, el saldo pendiente del ingreso se **sugiere como línea** de la liquidación — mecanismo `SuggestedPlazaItem`/spec condicional ya existente, editable y con snapshot) y `CANCELAR` (no sugiere; el registro pasa a `FINALIZADO` al emitirse la liquidación). Para indefinidos, `PAGAR_SALDO` no aplica (sin saldo definible) → **`CANCELAR` forzado: 422 si se intenta `PAGAR_SALDO`** (P-06 ratificada); las cuotas devengadas no aplicadas se cubren aplicándolas antes de emitir la liquidación |
| D-12 | Centro de costo y plaza (P-15 ratificada) | **Plaza asociada obligatoria** (default: la plaza **principal** del empleado) y **centro de costo obligatorio, derivado de la plaza** (`CostCenterPublicId` + snapshot de nombre — «relacionado a la plaza que tiene el empleado»): si la plaza del empleado no tiene centro de costo configurado, el alta se bloquea con 422 accionable (completar la plaza primero). La sugerencia de liquidación se ancla a la **plaza principal** (precedente REQ-002) |
| D-13 | Moneda y frecuencia | **Reutilización pura**: `CurrencyCatalogItem` (multi-moneda sin FX — los totales/bandejas agrupan por moneda, precedente off-payroll `/totals`) y `PayPeriodCatalogItem` (`MENSUAL`/`QUINCENAL`/`SEMANAL`/`UNICA`) para la frecuencia. Coherencia frecuencia ↔ tipo de planilla: validación blanda F1 (P-05) |
| D-14 | Autogestión | F1 **sin autoservicio** (P-11 ratificada — dato de compensación pactado por la empresa); la lectura self de `VIGENTE`/`FINALIZADO` queda como evolución aditiva si el negocio la pide (gate `isSelf` existente). Sin escritura self en ningún caso |
| D-15 | Retiro y liquidación | Perfil `RETIRADO`: sin altas nuevas; pendientes `EN_REVISION` solo `RECHAZADO`/`ANULADO` (precedente D-16 de REQ-003). Al **liquidar**: los `VIGENTE` se resuelven por su acción de liquidación (D-11); la **reversión de retiro** (que anula liquidaciones borrador) no toca los ingresos — si la liquidación emitida se anula manualmente, el `FINALIZADO` por `CANCELAR` se reabre a `VIGENTE` en la misma operación (simetría del gancho de reversión existente; detalle en plan técnico) |
| D-16 | Catálogos y seeds | Nuevos: `RECURRING_INCOME_STATUS_CATALOG` (6 estados, `-9880…-9885`), `RECURRING_INCOME_SETTLEMENT_ACTION_CATALOG` (2, `-9886`/`-9887`) y `RECURRING_INCOME_TYPE_CATALOG` (**confirmado por P-02**: 4 valores plantilla, `-9896…-9899`); `-9888`/`-9889` quedan de holgura del bloque (candidato: concepto de liquidación `INGRESO_CICLICO_PENDIENTE` si el plan técnico lo adopta). **`PAYROLL_TYPE_CATALOG` se re-ubica a `-9890…-9895`** (colisión verificada de los tentativos de REQ-004 con `ECONOMIC_AID_TYPE_CATALOG` `-9520…-9526`) — una sola definición, la siembra el primero que construya (coordinar backlog). Todos `HasData` idempotentes, verificación de IDs libres al abrir el PR |
| D-17 | Secuenciación | Registrar como **REQ-005** al final del backlog: aprovecha REQ-001 (maestro de periodos) y la especificación REQ-004 (tipos de planilla); la integración con liquidación no depende de nada pendiente (módulo mergeado). **Adelantable** si el negocio lo prioriza: degradando la FK de periodo a etiqueta (G-05) y sembrando él mismo el catálogo de tipos de planilla |
| D-18 | Asientos de expediente | **Sin asientos** en el journal de acciones de personal (`PersonnelFilePersonnelAction`): el ingreso cíclico es dato de **compensación** (como conceptos, off-payroll y ayudas económicas, que no asientan), no un hecho del expediente laboral. Si el negocio quiere huella en el journal, se evalúa en ratificación (aditivo) |

---

## 1. Resumen del producto o requerimiento

Se construirá la **primera rebanada del módulo de planilla** de CLARIHR: la gestión de los **ingresos cíclicos adicionales** de los empleados — montos que un empleado recibirá en cuotas recurrentes (bonos por metas, comisiones pactadas, incentivos temporales, ayudas pactadas en cuotas, etc.) — con **flujo de autorización**, **plan de cuotas** (finito o indefinido), **aplicación por periodo de planilla**, **historial de pago** consultable y **exportación como insumo** para la planilla (externa mientras no exista el motor interno).

**Qué se construye (F1).**

1. **Nuevo ingreso cíclico**: registro sobre el expediente con estado, fecha, referencia, tipo, **tipo de ingreso** (catálogo país de conceptos), centro de costo y observaciones; bloque de **cuotas** con fecha de inicio, moneda, tipo de planilla, frecuencia, monto/número de cuotas o indefinido, valor de cuota y **acción de liquidación**; sujeto a **autorización** (una decisión, anti-autoaprobación doble) antes de entrar en vigencia.
2. **Aplicación de cuotas por periodo**: unitaria o en lote (con exclusión = posponer a otro periodo), que va construyendo el historial real de pago; bandeja de **cuotas pendientes/vencidas no aplicadas** («transacciones que no se aplicaron en planilla»).
3. **Historial de pago del ingreso cíclico**: cuotas aplicadas (cuándo, cuánto, en qué periodo/planilla, quién) + proyección de pendientes + saldos.
4. **Bandejas y exportaciones**: por empresa con filtros y `StatusCounts`; **insumo de planilla por periodo** (cuotas a pagar) para el sistema de nómina externo.
5. **Integración con liquidación**: la «acción de liquidación» del ingreso decide si el saldo pendiente se sugiere como línea del finiquito o se cancela.

**Qué NO se construye en F1 (visión mapeada, Anexo A.1).** El **motor de generación de planilla** (periodos activos, pool de transacciones, corrida de cálculo con ISSS/AFP/Renta), los **descuentos cíclicos** y la **consulta de nivel de endeudamiento**: son las siguientes rebanadas del programa y exigen su propio levantamiento — en particular el motor, que cambia la frontera estratégica «la nómina es externa» ratificada en todos los módulos monetarios previos (P-01).

**Problema que resuelve.** Hoy los ingresos adicionales recurrentes solo pueden modelarse como configuración estructural (sin autorización, sin cuotas contadas, sin constancia de qué se pagó en qué planilla) o registrarse fuera del sistema: no hay trazabilidad de cuántas cuotas van, cuánto falta, quién autorizó el ingreso, ni un insumo exacto por periodo para nómina; y al liquidar a un empleado, los compromisos de pago pendientes no tienen tratamiento definido.

---

## 2. Objetivos del negocio

1. **Control y debido proceso sobre el gasto salarial adicional**: ningún ingreso cíclico entra en vigencia sin autorización de un tercero facultado (anti-autoaprobación doble), con referencia documental y auditoría completa.
2. **Trazabilidad cuota a cuota**: saber en todo momento cuántas cuotas se han pagado, en qué planillas, cuánto resta y cuándo termina cada compromiso — hoy inexistente.
3. **Insumo exacto para la nómina**: exportación por periodo (tipo de planilla + periodo) de las cuotas a aplicar, eliminando transcripciones manuales hacia el sistema de planilla externo — y dejando el modelo listo para que el futuro motor interno aplique las mismas cuotas automáticamente.
4. **Cierre limpio al liquidar**: la «acción de liquidación» resuelve los compromisos vigentes al retiro del empleado (pagar el saldo en el finiquito o cancelarlo), integrada al motor de liquidación ya construido.
5. **Imputación contable y de costos**: cada ingreso lleva centro de costo (con sus cuentas contables ya modeladas) para alimentar la contabilidad de planilla.
6. **Base del programa de planilla**: el modelo de cuotas + periodos + aplicación es el cimiento reutilizable de las rebanadas siguientes (descuentos cíclicos, endeudamiento, motor de generación) sin re-trabajo.

---

## 3. Alcance funcional

### Fase 1 — MVP (este análisis)

- **Configuración**: catálogo de estados del ingreso cíclico; catálogo de acciones de liquidación; catálogo de **tipos de ingresos cíclicos** (P-02 ratificada — prestaciones permanentes, plantilla A.2); adopción/siembra del catálogo país de **tipos de planilla** (especificación REQ-004, seeds re-ubicados); 3 permisos RBAC (`View`/`Manage`/`Authorize`).
- **Ingresos cíclicos**: crear/editar (`EN_REVISION`) con bloque de cuotas; decidir (autorizar → `VIGENTE` / rechazar con motivo); anular/revocar; suspensión/reanudación (P-03 ratificada); finalización automática del plan finito; cierre manual del indefinido.
- **Cuotas**: plan proyectado derivado (consulta); aplicación unitaria y **en lote por periodo** con exclusión (posponer); anulación de cuota aplicada con motivo; ajuste automático de la última cuota.
- **Historial de pago**: por ingreso (aplicadas + proyectadas + saldos) — la opción 2 del levantamiento.
- **Bandejas + exportaciones**: bandeja de ingresos por empresa (filtros estado/tipo/empleado/fechas, `StatusCounts`); bandeja de **cuotas pendientes/vencidas**; exports xlsx/csv/json; **export de insumo de planilla por periodo** (tipo de planilla + periodo/rango → cuotas a pagar con empleado, concepto, centro de costo, monto, moneda).
- **Integración con liquidación**: sugerencia del saldo (`PAGAR_SALDO`) como línea del finiquito / cierre por `CANCELAR`; coherencia con la reversión existente.

### Fase 2+ — Programa de planilla (contrato preparado, fuera de este MVP — Anexo A.1)

- **Descuentos cíclicos** (espejo con `Nature=EGRESO`, saldo y amortización) y **consulta de nivel de endeudamiento** (Σ cuotas de egresos activos vs salario, umbral por empresa).
- **Motor de generación de planilla**: periodo activo, pool de transacciones pendientes con «aplicar en este periodo / enviar a otro», corrida de cálculo (salario + conceptos + cíclicos + ISSS/AFP/Renta ya configurados), emisión y posteo al ledger.
- Correlación del historial con el ledger externo (`PersonnelFilePayrollTransaction`) si la nómina sigue externa (P-09).
- Autorización **multi-nivel** configurable (P-12); notificaciones; autoservicio ampliado.

---

## 4. Fuera de alcance

- **Generación/cálculo de planilla** (periodos activos, corrida, neto, posteo): programa aparte con levantamiento propio (P-01, Anexo A.1). F1 **no calcula** — registra, aplica cuotas y exporta.
- **Descuentos cíclicos** y **nivel de endeudamiento** (siguiente rebanada; el modelo queda preparado — D-02, P-10).
- **Creación de periodos de planilla** como funcionalidad de este módulo: ya especificada en **REQ-001** (`PayrollPeriodDefinition`, maestro por empresa); aquí solo se **consume** (D-10).
- **Escritura de ledgers**: ni `PersonnelFilePayrollTransaction` (sincronización externa inmutable) ni conceptos de compensación automáticos (RN-14).
- **Motor de aprobaciones multi-nivel** (F2 si se ratifica P-12); F1 entrega el flujo de una decisión.
- **Conversión de monedas / FX** (multi-moneda sin conversión, como todo el sistema; agrupación por moneda).
- **Notificaciones** (al autorizador o al empleado) — F2.
- **Importador masivo** de compromisos históricos (la adopción usa registro retroactivo por el flujo normal — P-13).
- **Adjuntos** (P-07 ratificada: F1 sin adjuntos): el campo `referencia` documenta el origen; el stack espejo de adjuntos queda como evolución barata si el negocio lo pide después (purpose + contenedor, pendiente de despliegue estándar).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador de empresa** | Mantiene catálogos editables, configura y asigna permisos |
| **Gestor de RRHH / Analista de planilla** (con `ManageRecurringIncomes`) | Registra y edita ingresos cíclicos, **aplica cuotas** (unitaria/lote por periodo), pospone, anula cuotas con motivo, exporta el insumo |
| **Autorizador** (jefatura/dirección con `AuthorizeRecurringIncomes`) | Decide (autoriza o rechaza) los ingresos `EN_REVISION`; revoca vigentes con motivo; sujeto a anti-autoaprobación doble; **Admin no decide** salvo grant explícito |
| **Consulta de RRHH / Auditor** (con `ViewRecurringIncomes`) | Solo lectura de fichas, historiales, bandejas y exportaciones |
| **Finanzas / Contabilidad** | Consume el insumo por periodo (cuotas con centro de costo y cuentas contables) y los exports |
| **Sistema de planilla externa** | Recibe el insumo del periodo y paga las cuotas (mientras no exista el motor interno) |
| **Motor de liquidación (interno)** | Consume la acción de liquidación: sugiere el saldo como línea del finiquito o dispara el cierre |
| **Empleado (autogestión)** | Sin acceso en F1 (P-11 ratificada); la lectura del propio historial queda como evolución aditiva |

---

## 6. Requerimientos funcionales

> Agrupados en 4 grupos (A: configuración · B: ingreso cíclico y su flujo · C: cuotas e historial · D: consultas, exports e integración). Prioridades: Alta = imprescindible F1; Media = F1 deseable.

### Grupo A — Configuración y catálogos

### RF-001 - Estados, catálogos y permisos del módulo

**Descripción:** Sembrar el catálogo de estados del ingreso cíclico (`EN_REVISION`, `VIGENTE`, `RECHAZADO`, `SUSPENDIDO`, `FINALIZADO`, `ANULADO` — P-03 ratificada), el catálogo de acciones de liquidación (`PAGAR_SALDO`, `CANCELAR`) y el catálogo de **tipos de ingresos cíclicos** (P-02 ratificada; plantilla editable A.2: `AYUDA_ALIMENTACION`, `GASTOS_REPRESENTACION`, `COMBUSTIBLE`, `OTRO`); declarar los 3 permisos (D-06) con la receta completa. Verificar además que el catálogo de conceptos (`Nature=Ingreso`) cubre los ejemplos del negocio (Salario, Aguinaldo, horas extras, vacaciones, bonificaciones) y completar la semilla si falta alguno.

**Reglas de negocio:**
- Patrón híbrido (constantes canónicas en dominio + catálogo país editable para i18n/UI; validación `CatalogCodeIsActiveAsync` en escrituras).
- IDs de semilla en bloque nuevo **`-9880…-9889`** (Anexo A.2), verificados contra `GlobalCatalogSeedData` al abrir el PR (respetar reservas REQ-001/002/003; trampa `-9490…-9496`).
- `AuthorizeRecurringIncomes` con `RequireAssertion` que excluye Admin; `View`/`Manage` con fallback estándar; gates fail-closed + governance tests.

**Criterios de aceptación:**
- Migración `HasData` idempotente; usuario sin permiso → 403 en cada endpoint; Admin sin `Authorize*` no puede decidir.

**Prioridad:** Alta
**Dependencias:** Verificación de IDs libres; D-04/D-05/D-06/D-16.

### RF-002 - Catálogo país de tipos de planilla (definición compartida con REQ-004)

**Descripción:** Adoptar la especificación única de REQ-004 (`PAYROLL_TYPE_CATALOG`: `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO`) — la siembra el REQ que se construya primero — **con seeds re-ubicados** a `-9890…-9895` por la colisión verificada con `ECONOMIC_AID_TYPE_CATALOG`.

**Reglas de negocio:**
- Una sola definición del catálogo en todo el sistema (sin duplicados por módulo); si REQ-004 ya lo sembró al arrancar este módulo, aquí solo se consume.
- Las cuotas validan `payrollTypeCode` contra el catálogo activo → 422 si inválido.
- La migración de limpieza destructiva de `payroll_type_code` en plazas es **de REQ-004** (no se repite aquí); este módulo no depende de ella.

**Criterios de aceptación:**
- Cuota con tipo de planilla inexistente/inactivo → 422 bilingüe; catálogo consultable por el FE (`payroll-types`).

**Prioridad:** Alta
**Dependencias:** Coordinación con REQ-004 (D-16); actualización del backlog con la re-ubicación.

### Grupo B — Ingreso cíclico y su flujo

### RF-003 - Crear y editar ingreso cíclico

**Descripción:** Alta por RRHH sobre el expediente: fecha, referencia, **tipo** (catálogo de tipos de ingresos cíclicos, P-02), **tipo de ingreso** (concepto país `Nature=Ingreso`, con snapshot), **plaza** (obligatoria, default la principal) con su **centro de costo derivado** (P-15), observaciones, y bloque de cuotas (fecha de inicio, moneda, tipo de planilla, frecuencia, `isIndefinite`, valor de cuota, número de cuotas o monto total si finito, acción de liquidación). Nace `EN_REVISION`; editable mientras siga en revisión.

**Reglas de negocio:**
- Solo `ManageRecurringIncomes`; If-Match en ediciones; perfil `RETIRADO` → 422 (RN-10).
- Tipo de ingreso: concepto país activo `Nature=Ingreso` → snapshot código+nombre (RN-04); **tipo** (ingreso cíclico) activo del catálogo → obligatorio.
- Plaza obligatoria (default principal) y centro de costo **derivado de la plaza** con snapshot (RN-12); plaza sin centro de costo configurado → 422 accionable.
- Cuotas: valor > 0; si finito, coherencia valor×número≈total con ajuste declarado en la última (RN-05); si indefinido, sin número/total y acción de liquidación compatible (D-11/P-06); frecuencia y moneda de catálogos activos; fecha de inicio retroactiva permitida (P-13).
- El estado nunca se digita: lo fija el flujo (RN-01).
- Solo `EN_REVISION` es editable; estados posteriores → 422/409 (RN-02).

**Criterios de aceptación:**
- POST → 201 `EN_REVISION` con `publicId` y ETag; el plan proyectado es consultable de inmediato; finito con número y total incoherentes → 422 con detalle; edición tras autorizar → 422.

**Prioridad:** Alta
**Dependencias:** RF-001, RF-002.

### RF-004 - Decidir ingreso cíclico (autorizar / rechazar)

**Descripción:** PATCH de decisión sobre un ingreso `EN_REVISION`: **autorizar** (queda `VIGENTE` y sus cuotas comienzan a ser aplicables) o **rechazar** (motivo obligatorio).

**Reglas de negocio:**
- Solo `AuthorizeRecurringIncomes` (excluye Admin); **anti-autoaprobación doble** (RN-03): 403 si quien decide es el empleado sujeto o quien registró.
- Re-verificación de estado dentro de la transacción (dos decisiones concurrentes → la segunda 409/422).
- Sobre perfil `RETIRADO` solo se admite rechazar/anular (D-15).

**Criterios de aceptación:**
- Decisión por el registrador → 403; autorizar → `VIGENTE` y cuotas aplicables; rechazar sin motivo → 422.

**Prioridad:** Alta
**Dependencias:** RF-003.

### RF-005 - Ciclo de vida post-autorización (anular, revocar, suspender, finalizar)

**Descripción:** Anulación con motivo desde `EN_REVISION` (retiro del trámite); **revocación** con motivo desde `VIGENTE` (el compromiso se cancela; las cuotas ya aplicadas quedan intactas y trazables); **suspensión/reanudación** (pausa temporal de aplicabilidad — P-03 ratificada); **finalización automática** al aplicarse la última cuota del plan finito; **cierre manual** de un indefinido (con motivo).

**Reglas de negocio:**
- Motivos obligatorios en anulación/revocación/cierre; baja lógica, nunca borrado (RN-08).
- `SUSPENDIDO` bloquea la aplicación de cuotas (unitaria y lote) sin alterar el plan; reanudar restituye la aplicabilidad (las cuotas no aplicadas durante la pausa quedan pendientes/posponibles).
- `FINALIZADO` es terminal junto con `RECHAZADO`/`ANULADO`; un finito queda `FINALIZADO` en la misma transacción que aplica su última cuota (RN-06).

**Criterios de aceptación:**
- Revocar un vigente con 3 cuotas aplicadas → registro `ANULADO`, cuotas aplicadas intactas, pendientes desaparecen de bandejas e insumos; aplicar la última cuota → `FINALIZADO` automático.

**Prioridad:** Alta
**Dependencias:** RF-004.

### Grupo C — Cuotas e historial

### RF-006 - Plan de cuotas proyectado (consulta derivada)

**Descripción:** GET del plan del ingreso: cuotas aplicadas (persistidas) + **proyección** de pendientes (fechas teóricas = fecha de inicio + frecuencia; para finitos, hasta completar número/total con ajuste final; para indefinidos, ventana rodante) + derivados (cuotas aplicadas, monto aplicado, saldo restante si finito, próxima cuota).

**Reglas de negocio:**
- La proyección es **calculada, nunca persistida** (D-07): cambia sola si se anula una cuota aplicada o cambia el estado.
- La proyección de un `SUSPENDIDO`/`ANULADO` se marca como no aplicable (RN-06).

**Criterios de aceptación:**
- Finito de 6 cuotas con 2 aplicadas → 4 proyectadas con fechas teóricas correctas y saldo exacto; total no divisible entre cuotas → última proyectada con el ajuste.

**Prioridad:** Alta
**Dependencias:** RF-003.

### RF-007 - Aplicar cuotas (unitaria y en lote por periodo)

**Descripción:** (a) Aplicación **unitaria**: registrar el pago de la siguiente cuota de un ingreso `VIGENTE` con fecha, periodo de imputación (tipo de planilla + `PayrollPeriodId?`/etiqueta) y nota; (b) aplicación **en lote por periodo**: para un tipo de planilla + periodo/rango, el sistema presenta las cuotas que corresponden (por frecuencia y fecha teórica) y RRHH confirma el lote **pudiendo excluir cuotas** (posponer = «enviar a otro periodo»).

**Reglas de negocio:**
- Solo `ManageRecurringIncomes`; solo ingresos `VIGENTE` (suspendidos/anulados/finalizados → excluidos); **lock anti-carrera** + re-verificación en transacción: no se puede aplicar dos veces la misma cuota (secuencia por ingreso) ni superar el plan finito (RN-06/RN-07).
- Monto de la cuota = valor del plan (última = ajuste automático); editable solo si se ratifica (P-04); moneda/tipo de planilla heredados con snapshot.
- La aplicación de la última cuota finita finaliza el ingreso en la misma transacción (RF-005).
- Origen `MANUAL` en F1 (el motor F2 usará `MOTOR` sobre el mismo endpoint interno) (D-08).

**Criterios de aceptación:**
- Lote del periodo con 10 cuotas y 2 excluidas → 8 aplicadas, 2 siguen pendientes para otro periodo; doble submit concurrente del lote → sin duplicados (test de carrera); aplicar cuota 7/6 → 422.

**Prioridad:** Alta
**Dependencias:** RF-004, RF-006.

### RF-008 - Anular cuota aplicada

**Descripción:** Anulación con motivo de una cuota aplicada por error (estado `ANULADA`, sin borrado físico); el saldo y la proyección la reintegran.

**Reglas de negocio:**
- Motivo obligatorio; quién/cuándo trazables; una cuota anulada no cuenta para el plan ni para los totales (RN-08).
- Si la anulación reabre un plan finito ya `FINALIZADO`, el ingreso vuelve a `VIGENTE` en la misma transacción (simetría de RF-005).
- Alternativa por contra-asiento (estilo off-payroll) descartada salvo ratificación en contra (P-08).

**Criterios de aceptación:**
- Anular la cuota 6/6 → ingreso vuelve a `VIGENTE` con saldo de 1 cuota; el historial muestra la cuota `ANULADA` con motivo.

**Prioridad:** Alta
**Dependencias:** RF-007.

### RF-009 - Historial de pago del ingreso cíclico (opción 2 del levantamiento)

**Descripción:** Consulta por ingreso: serie completa de cuotas **aplicadas** (número, fecha, monto, moneda, tipo de planilla, periodo, origen, quién aplicó, estado) + proyección de pendientes + derivados (aplicado/restante). Paginada y exportable.

**Reglas de negocio:**
- Lectura con `View…`/`Manage…` (self según P-11); cuotas anuladas visibles y marcadas (RN-08).
- Cuando exista el motor (F2) o la correlación con el ledger externo (P-09), el mismo contrato muestra el origen sin cambios de forma.

**Criterios de aceptación:**
- Ingreso con 4 aplicadas (1 anulada) → historial muestra 4 filas con estados correctos, totales excluyen la anulada; export del historial disponible.

**Prioridad:** Alta
**Dependencias:** RF-007.

### Grupo D — Consultas, exportaciones e integración

### RF-010 - Consulta en ficha + bandeja de empresa

**Descripción:** (a) Listado paginado por expediente (filtros: estado, tipo, tipo de ingreso, rango de fechas); (b) bandeja por empresa (filtros: estado, tipo de planilla, empleado, rango; `StatusCounts`; totales por moneda).

**Reglas de negocio:**
- `View…`/`Manage…` para RRHH; sin acceso de terceros; totales agrupados por moneda (sin FX) (RN-13).

**Criterios de aceptación:**
- `POST /companies/{companyId}/recurring-incomes/query` pagina y cuenta por estado; filtros combinables.

**Prioridad:** Alta
**Dependencias:** RF-003.

### RF-011 - Cuotas pendientes / no aplicadas («transacciones que no se aplicaron en planilla»)

**Descripción:** Bandeja por empresa de cuotas **pendientes** (proyectadas cuya fecha teórica cae en el rango consultado) y **vencidas** (fecha teórica pasada sin aplicar), con filtros por tipo de planilla, periodo, empleado y estado del ingreso; es la aproximación F1 de la consulta de transacciones no aplicadas del levantamiento.

**Reglas de negocio:**
- Solo cuotas de ingresos `VIGENTE` (suspendidos se muestran marcados, no aplicables) (RN-06).
- Desde la bandeja se puede lanzar la aplicación en lote (RF-007) — el flujo «aplicar al periodo actual / enviar a otro» se materializa aquí.
- La semántica plena de «periodo activo» llega con el motor (F2); la degradación queda documentada en guía FE (RN-15).

**Criterios de aceptación:**
- Cuota con fecha teórica de hace 2 quincenas sin aplicar → aparece como vencida; tras aplicarla en lote desaparece.

**Prioridad:** Alta
**Dependencias:** RF-006, RF-007.

### RF-012 - Exportaciones (bandejas + insumo de planilla por periodo)

**Descripción:** (a) Export xlsx/csv/json de las bandejas (ingresos y cuotas); (b) **export de insumo de planilla**: para un tipo de planilla + periodo/rango, las cuotas a pagar (empleado, plaza, concepto snapshot, centro de costo con cuentas contables, monto, moneda, número de cuota, referencia) — el puente operativo con la nómina externa.

**Reglas de negocio:**
- `View…` para exportar; rate limiting y límite síncrono existentes; filas en español (patrón liquidación).
- El insumo incluye solo cuotas de ingresos `VIGENTE` pendientes del periodo; excluye suspendidos/anulados (RN-06/RN-08).

**Criterios de aceptación:**
- El export de un periodo cuadra contra la bandeja de pendientes del mismo filtro en tests de integración.

**Prioridad:** Alta
**Dependencias:** RF-011.

### RF-013 - Integración con liquidación (acción de liquidación)

**Descripción:** Al preparar la liquidación de un empleado (motor mergeado PR #56), los ingresos cíclicos `VIGENTE` con acción `PAGAR_SALDO` **sugieren su saldo pendiente** como línea de ingreso de la liquidación (mecanismo de líneas sugeridas existente; editable, con snapshot y traza); con `CANCELAR`, no sugieren nada y el registro pasa a `FINALIZADO` al emitirse la liquidación. Coherencia con la reversión: anular la liquidación reabre los finalizados por esta vía.

**Reglas de negocio:**
- Solo plaza principal (anti doble pago, precedente REQ-002); saldo = plan finito restante (los indefinidos no tienen saldo → P-06).
- La línea sugerida es editable/excluible por el analista (precedente `BONO_PENDIENTE`); guard anti-duplicado si ya existe línea manual equivalente.
- Nada de esto escribe ledgers ni conceptos (RN-14).

**Criterios de aceptación:**
- Liquidar con un ingreso finito de saldo $300 y acción `PAGAR_SALDO` → línea sugerida de $300 visible/editable; con `CANCELAR` → sin línea y el ingreso finaliza al emitir; anular la liquidación emitida → el ingreso vuelve a `VIGENTE`.

**Prioridad:** Media (F1 deseable; recortable a segunda entrega sin bloquear el resto)
**Dependencias:** RF-004; módulo de liquidación (mergeado).

---

## 7. Requerimientos no funcionales

- **Seguridad**: dato de compensación **sensible**: lecturas con `ViewRecurringIncomes` (o colapso con `ViewCompensation` si se ratifica P-14); `Authorize*` excluye Admin (separación de funciones); gates fail-closed por handler además de la política de controlador; anti-autoaprobación doble; 403 sin enmascaramiento.
- **Auditoría**: `CreatedUtc`/`ModifiedUtc`, quién registró/decidió/aplicó/anuló y cuándo, motivos obligatorios, snapshots (concepto, centro de costo, moneda, periodo), baja lógica universal, secuencia de cuotas inmutable.
- **Concurrencia/API**: convenciones del repo — `api/v1`, If-Match (faltante → 400, obsoleto → 409), ETag rotativo, `publicId`, enums string, errores bilingües `extensions.code`, sub-recursos con `parentConcurrencyToken`. La aplicación de cuotas (unitaria y lote) corre bajo **lock anti-carrera** + re-verificación transaccional (test de doble submit obligatorio).
- **Rendimiento**: bandejas y planes paginados con índices por `(tenant, empresa, estado, fechas)` y `(tenant, ingreso, número de cuota)`; la proyección de cuotas es cálculo puro en memoria por ingreso (sin generación masiva de filas); exports con límite síncrono y rate limiting existentes.
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId`; sin jobs en F1 (la finalización es transaccional al aplicar; sin vencimientos programados).
- **Usabilidad**: errores accionables (incoherencia valor×número×total con los tres valores en el detalle, cuota duplicada con el número en conflicto, tipo de planilla inválido); proyección con fechas teóricas visibles; bandeja de vencidas como lista de trabajo.
- **Mantenibilidad**: reglas del plan de cuotas y del ciclo de vida en **módulo puro** (`RecurringIncomeRules`) con suite unitaria (casos dorados Anexo A.3) y **paridad de localización**; OpenAPI sin drift; guía FE.
- **Compatibilidad**: cambios 100 % aditivos (entidades, catálogos y permisos nuevos; catálogos existentes solo se consumen; contrato de liquidación intacto — la sugerencia es una línea más).
- **Accesibilidad**: (frontend) formulario de cuotas con derivación visible (número↔total↔valor), bandejas navegables/exportables; se documenta en la guía FE.

---

## 8. Historias de usuario

### HU-001 - Registrar y autorizar un ingreso cíclico
Como **gestor de RRHH**, quiero **registrar un ingreso cíclico adicional con su plan de cuotas**, para **que un autorizador lo apruebe y quede vigente con trazabilidad completa**.

**Criterios de aceptación:**
- Dado un concepto de ingreso activo, cuando registro el ingreso con 6 cuotas de $50, entonces queda `EN_REVISION` con plan proyectado de 6 fechas y no es aplicable aún.
- Dado un ingreso `EN_REVISION`, cuando el autorizador (tercero) lo autoriza, entonces queda `VIGENTE` y sus cuotas aparecen en la bandeja de pendientes.
- Dado que yo registré el ingreso, cuando intento decidirlo, entonces recibo 403.

### HU-002 - Aplicar las cuotas del periodo
Como **analista de planilla**, quiero **aplicar en lote las cuotas que corresponden a la quincena**, para **que el historial refleje lo pagado y el insumo cuadre con la nómina**.

**Criterios de aceptación:**
- Dado el filtro planilla `QUINCENAL` + periodo 2026-Q13, cuando confirmo el lote, entonces cada cuota queda aplicada con número, monto, periodo y origen `MANUAL`.
- Dado un doble clic/submit concurrente, entonces no se generan cuotas duplicadas.

### HU-003 - Posponer una cuota a otro periodo
Como **analista de planilla**, quiero **excluir una cuota del lote del periodo**, para **enviarla al siguiente periodo sin perderla**.

**Criterios de aceptación:**
- Dada una cuota excluida del lote, cuando consulto la bandeja de pendientes del siguiente periodo, entonces la cuota aparece disponible (y como vencida si su fecha teórica pasó).

### HU-004 - Consultar el historial de pago
Como **consulta de RRHH**, quiero **ver el historial de cuotas de un ingreso cíclico**, para **saber cuánto se ha pagado, en qué planillas y cuánto falta**.

**Criterios de aceptación:**
- Dado un ingreso con 4 cuotas aplicadas y 2 pendientes, cuando consulto su historial, entonces veo las 4 aplicadas (fecha, monto, periodo, quién) y las 2 proyectadas con fechas teóricas y el saldo.

### HU-005 - Resolver el compromiso al liquidar
Como **analista de liquidaciones**, quiero **que el saldo de los ingresos cíclicos con acción `PAGAR_SALDO` se sugiera en el finiquito**, para **cerrar los compromisos del empleado sin cálculos manuales**.

**Criterios de aceptación:**
- Dado un ingreso finito con saldo de $300 y acción `PAGAR_SALDO`, cuando preparo la liquidación, entonces aparece la línea sugerida de $300 editable.
- Dado un ingreso con `CANCELAR`, cuando emito la liquidación, entonces el ingreso queda `FINALIZADO` sin línea.

### HU-006 - Corregir una cuota mal aplicada
Como **gestor de RRHH**, quiero **anular con motivo una cuota aplicada por error**, para **que el saldo y el historial queden correctos y auditables**.

**Criterios de aceptación:**
- Dada la anulación con motivo, entonces la cuota queda `ANULADA` (visible en historial), el saldo la reintegra y, si el ingreso estaba `FINALIZADO`, vuelve a `VIGENTE`.

### HU-007 - Exportar el insumo del periodo
Como **finanzas/analista de planilla**, quiero **exportar las cuotas a pagar del periodo con concepto, centro de costo y montos**, para **aplicarlas en la nómina externa sin transcripciones**.

**Criterios de aceptación:**
- Dado un tipo de planilla y periodo, cuando exporto el insumo, entonces obtengo solo cuotas de ingresos vigentes pendientes del periodo, con empleado, concepto snapshot, centro de costo, monto y moneda.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | El **estado nunca se digita**: lo fija el flujo (`EN_REVISION` al crear; `VIGENTE`/`RECHAZADO` al decidir; `FINALIZADO` automático al completar el plan o por la acción `CANCELAR` en liquidación; `ANULADO` por retiro del trámite o revocación; `SUSPENDIDO` como pausa — P-03 ratificada) |
| RN-02 | Ciclo de vida: solo `EN_REVISION` es editable; transiciones fuera del ciclo → 422/409; `RECHAZADO`/`ANULADO`/`FINALIZADO` son terminales (la anulación de cuota puede reabrir `FINALIZADO`→`VIGENTE`, RF-008) |
| RN-03 | **Anti-autoaprobación doble**: quien decide no puede ser el empleado sujeto (`LinkedUserPublicId`) ni quien registró (`RegisteredByUserId`) → 403 |
| RN-04 | «Tipo ingreso» = concepto país activo `Nature=Ingreso`, con **snapshot** (código+nombre) al registrar; cambios posteriores del catálogo no recalculan históricos |
| RN-05 | Plan finito coherente: valor de cuota > 0; número de cuotas **o** monto total (el tercero se deriva); si viajan los tres, deben cuadrar; la **última cuota ajusta** el remanente y así se proyecta y aplica |
| RN-06 | Solo ingresos `VIGENTE` devengan/aplican cuotas; `SUSPENDIDO` bloquea aplicación sin alterar el plan; las cuotas siguen la **secuencia** (no se salta ni duplica número; no se supera el plan finito) |
| RN-07 | La aplicación (unitaria/lote) corre bajo **lock anti-carrera** con re-verificación de estado y secuencia en la transacción; la última cuota finita finaliza el ingreso en la misma transacción |
| RN-08 | Nada se borra físicamente: cuota mal aplicada → `ANULADA` con motivo (reintegra saldo y proyección); revocación del ingreso no toca cuotas ya aplicadas; anuladas visibles y excluidas de totales/insumos |
| RN-09 | Fecha de registro ≤ hoy; fecha de **inicio de cuotas** puede ser pasada (regularización, P-13) o futura (compromiso programado) |
| RN-10 | Perfil `RETIRADO`: sin altas; pendientes solo rechazables/anulables; los `VIGENTE` se resuelven vía liquidación según su **acción de liquidación** (D-15) |
| RN-11 | Acción de liquidación: `PAGAR_SALDO` → línea sugerida (editable, snapshot) en la liquidación de la **plaza principal**; `CANCELAR` → sin línea y cierre al emitir; anular la liquidación revierte el cierre (simetría) |
| RN-12 | Centro de costo **obligatorio y relacionado a la plaza del empleado** (P-15): la plaza asociada es obligatoria (default: principal) y el centro de costo se deriva de ella (FK a `CostCenter` activo + snapshot); plaza sin centro de costo configurado → 422 accionable |
| RN-13 | Multi-moneda **sin conversión**: totales y bandejas agrupan por moneda (precedente off-payroll) |
| RN-14 | **No se escriben ledgers ni configuración**: ni `PersonnelFilePayrollTransaction` (sincronización externa inmutable) ni `PersonnelFileCompensationConcept` automáticos; el efecto monetario viaja por aplicación de cuotas + exportación de insumo |
| RN-15 | «Transacciones no aplicadas» y «enviar a otro periodo» = bandeja de pendientes/vencidas + exclusión en lote (F1); la semántica de «periodo activo» pertenece al motor (F2) y la degradación se documenta |
| RN-16 | Frontera con la configuración: el **concepto de compensación** define lo estructural/permanente (salario, ley, beneficios fijos); el **ingreso cíclico** registra lo adicional finito/revocable con autorización y cuotas — comunicado en UI/guía FE; sin bloqueo duro en F1 |
| RN-17 | El periodo de imputación de la cuota usa el par `PayrollTypeCode` (catálogo) + `PayrollPeriodId?`/etiqueta snapshot (maestro REQ-001 cuando exista); nunca texto sin snapshot |
| RN-18 | Los catálogos consumidos (conceptos, monedas, frecuencias, tipos de planilla, acciones) deben estar activos al usarse → 422; los históricos conservan snapshot |

---

## 10. Flujos principales

### Flujo 1 — Ingreso cíclico end-to-end
1. RRHH abre el expediente y elige «Nuevo ingreso cíclico».
2. Ingresa fecha, referencia, tipo (ingreso cíclico), tipo de ingreso (concepto), plaza (default principal — su centro de costo se deriva automáticamente), observaciones, y el bloque de cuotas (inicio, moneda, tipo de planilla, frecuencia, valor, número/total o indefinido, acción de liquidación).
3. El sistema valida catálogos y coherencia del plan; guarda `EN_REVISION` (201 + ETag) y expone el plan proyectado.
4. El autorizador revisa la bandeja de pendientes y **autoriza** (o rechaza con motivo); el sistema verifica la anti-autoaprobación doble.
5. Al autorizar: el ingreso queda `VIGENTE` y sus cuotas aparecen en la bandeja de pendientes del periodo correspondiente.

### Flujo 2 — Aplicación de cuotas del periodo (lote)
1. El analista de planilla abre la bandeja de cuotas pendientes y filtra tipo de planilla `QUINCENAL` + periodo.
2. El sistema lista las cuotas cuya fecha teórica cae en el periodo (más las vencidas anteriores).
3. El analista excluye las que se posponen y confirma el lote.
4. El sistema aplica cada cuota (secuencia, monto con ajuste final, snapshot de periodo, origen `MANUAL`) bajo lock, finalizando los planes que se completan.
5. El analista exporta el **insumo del periodo** y lo entrega a la nómina externa.

### Flujo 3 — Historial de pago (opción 2 del levantamiento)
1. El usuario abre el ingreso cíclico y su pestaña de historial.
2. El sistema muestra las cuotas aplicadas (número, fecha, monto, periodo, origen, quién) + proyectadas + saldos.
3. Exporta si lo necesita.

### Flujo 4 — Rechazo / revocación
1. El autorizador encuentra improcedente el ingreso `EN_REVISION` → rechaza con motivo (sin efecto monetario).
2. O bien: un `VIGENTE` pactado se cancela (revocación con motivo): las cuotas aplicadas quedan; las pendientes desaparecen de bandejas e insumos.

### Flujo 5 — Liquidación del empleado con compromisos vigentes
1. El analista prepara la liquidación (módulo existente).
2. El sistema sugiere una línea por cada ingreso cíclico `VIGENTE` con `PAGAR_SALDO` (saldo pendiente, editable) — plaza principal.
3. Al **emitir** la liquidación, los ingresos con `CANCELAR` quedan `FINALIZADO`; los pagados quedan saldados y `FINALIZADO`.
4. Si la liquidación se anula, los cierres se revierten (los ingresos vuelven a `VIGENTE`).

### Flujo 6 — Corrección de una cuota
1. RRHH detecta una cuota aplicada por error (o aplicada al periodo equivocado).
2. La anula con motivo → `ANULADA`, saldo reintegrado; si corresponde, la re-aplica al periodo correcto.

### Flujo 7 — Adopción (compromisos preexistentes)
1. RRHH registra retroactivamente los ingresos cíclicos pactados antes del sistema (fecha de inicio pasada) y los pasa por autorización.
2. Aplica en lote las cuotas ya pagadas históricamente (con sus periodos reales) para que el saldo actual sea exacto (P-13).

---

## 11. Flujos alternativos y excepciones

- **Decisión por el sujeto o el registrador** → 403 `SELF_APPROVAL_FORBIDDEN`.
- **Plan incoherente** (valor×número≠total fuera del ajuste permitido) → 422 con los tres valores en el detalle.
- **Indefinido con número/total** o **finito sin ninguno de los dos** → 422.
- **Acción `PAGAR_SALDO` sobre indefinido** → 422 (`CANCELAR` forzado — P-06 ratificada).
- **Plaza del empleado sin centro de costo configurado** → 422 accionable (completar la plaza antes de registrar el ingreso — P-15).
- **Aplicar cuota de un ingreso no `VIGENTE`** (en revisión, suspendido, anulado, finalizado) → 422.
- **Cuota duplicada / fuera de secuencia / plan excedido** → 422 con el número en conflicto; **doble submit concurrente** → el segundo 409/422 (lock + re-verificación).
- **Anular cuota ya anulada** → 422; **anulación que reabre un finalizado** → transición automática a `VIGENTE` (no error).
- **Tipo de ingreso de naturaleza egreso o inactivo** → 422; **moneda/frecuencia/tipo de planilla/centro de costo inactivos** → 422.
- **Editar/decidir un registro no `EN_REVISION`** → 422/409; **rechazo/anulación/revocación/cierre sin motivo** → 422.
- **Perfil `RETIRADO`**: alta → 422; decidir → solo rechazo/anulación.
- **If-Match ausente** → 400; **obsoleto** → 409.
- **Usuario sin permiso** → 403 (gestión, decisión, bandejas, exports); **Admin sin `Authorize*`** → 403 al decidir.
- **Bandeja/insumo sin filtro de periodo o rango** → 400 (rango obligatorio en insumo).
- **Maestro de periodos (REQ-001) aún no construido** → la imputación degrada a etiqueta (modo degradado documentado, D-10/D-17).

---

## 12. Datos requeridos

> Convenciones del repo aplican a todas las entidades: `long Id` interno + `Guid publicId` externo, `TenantId`, `CreatedUtc`/`ModifiedUtc`, `isActive` (baja lógica), `concurrencyToken` rotativo (If-Match), factoría `Create(...)` + mutadores custodiados. Se listan solo los campos de negocio.

### Entidad: Ingreso cíclico (`PersonnelFileRecurringIncome` — sub-registro del expediente; modelo preparado para naturaleza, D-02)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| registrationDate | Fecha | Sí | ≤ hoy (RN-09) | «Fecha» del levantamiento (registro/otorgamiento) |
| reference | Texto (200) | No | — | «Referencia» (acuerdo, acta, contrato de origen) |
| recurringIncomeTypeCode | Código catálogo | Sí | `RECURRING_INCOME_TYPE_CATALOG` activo (P-02) | «Tipo» — tipo de ingreso cíclico (prestación permanente) |
| conceptTypeCode / conceptNameSnapshot | Código / Texto | Sí | Concepto país activo `Nature=Ingreso` (RN-04) | «Tipo ingreso» + snapshot |
| costCenterPublicId / costCenterNameSnapshot | Guid / Texto | Sí | **Derivado de la plaza** (RN-12/P-15); plaza sin centro de costo → 422 | «Centro de costo» |
| assignedPositionPublicId | Guid | Sí | Plaza del empleado (default: principal) | Plaza asociada (fuente del centro de costo; ancla de la sugerencia de liquidación) |
| observations | Texto (1000) | No | — | «Observaciones» |
| statusCode | Código catálogo | Sí (flujo) | `EN_REVISION`/`VIGENTE`/`RECHAZADO`/`SUSPENDIDO`/`FINALIZADO`/`ANULADO` (híbrido; RN-01) | «Estado del ingreso» — lo fija el flujo |
| installmentStartDate | Fecha | Sí | Pasada o futura (RN-09) | Cuotas: fecha de inicio (primera cuota teórica) |
| currencyCode | Código catálogo | Sí | Moneda activa (RN-18) | Cuotas: moneda |
| payrollTypeCode | Código catálogo | Sí | `PAYROLL_TYPE_CATALOG` activo (RF-002) | Cuotas: tipo de planilla |
| installmentFrequencyCode | Código catálogo | Sí | `PAY_PERIOD_CATALOG` activo | Cuotas: frecuencia |
| isIndefinite | Booleano | Sí | — | «Monto y número de cuotas indefinido (sí/no)» |
| installmentValue | Decimal (18,2) | Sí | > 0 | «Valor de cuota» |
| installmentCount | Entero | Si finito* | ≥ 1; coherencia RN-05 | Número de cuotas (derivable del total) |
| totalAmount | Decimal (18,2) | Si finito* | > 0; coherencia RN-05 | Monto total (derivable del número) |
| settlementActionCode | Código catálogo | Sí | `PAGAR_SALDO`/`CANCELAR` (compatibilidad P-06) | «Acción de liquidación» |
| registeredByUserId | Guid | Sí | Usuario autenticado | Quién registró (anti-autoaprobación) |
| decidedByUserId / decidedUtc / decisionNote | Guid / Fecha / Texto (500) | Al decidir | Anti-autoaprobación doble (RN-03); motivo al rechazar | Auditoría de la decisión |
| suspendedUtc / suspensionNote | Fecha / Texto (500) | Al suspender (P-03) | Motivo obligatorio | Pausa temporal |
| closedUtc / closureReason / closedByUserId | Fecha / Texto (500) / Guid | Al cerrar/revocar/anular | Motivo obligatorio (RN-08) | Cierre manual, revocación o anulación |
| *(derivados, no persistidos)* appliedInstallments / appliedAmount / remainingAmount / nextInstallmentDate | Entero / Decimal / Decimal / Fecha | — | Calculados de las cuotas aplicadas + plan | Saldos y próxima cuota (RF-006) |

\* Si finito: al menos uno de `installmentCount`/`totalAmount` (el otro se deriva); si viajan ambos, deben cuadrar con `installmentValue` (última cuota ajusta).

### Entidad: Cuota aplicada (`PersonnelFileRecurringIncomeInstallment` — hija; se persiste al aplicar, D-09)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| recurringIncomeId | FK | Sí | — | Ingreso cíclico padre |
| installmentNumber | Entero | Sí | Secuencial único por ingreso (RN-06) | Número de cuota |
| appliedDate | Fecha | Sí | — | Fecha de aplicación/pago |
| theoreticalDueDate | Fecha | Sí | Derivada del plan al aplicar | Fecha teórica de la cuota (trazabilidad de atrasos) |
| amount / currencyCode | Decimal (18,2) / Código | Sí | Valor del plan (última = ajuste RN-05); snapshot | Monto pagado y moneda |
| payrollTypeCode | Código | Sí | Snapshot del ingreso | Tipo de planilla de imputación |
| payrollPeriodId / payrollPeriodLabel | FK? / Texto | No / Sí al aplicar | Maestro REQ-001 cuando exista (RN-17) | Periodo de planilla imputado + etiqueta snapshot |
| originCode | Código | Sí | `MANUAL` (F1) / `MOTOR` (F2) | Origen de la aplicación (D-08) |
| statusCode | Código | Sí | `APLICADA`/`ANULADA` | Estado de la cuota |
| appliedByUserId | Guid | Sí | — | Quién aplicó |
| annulmentReason / annulledByUserId / annulledUtc | Texto (500) / Guid / Fecha | Al anular | Motivo obligatorio (RN-08) | Auditoría de anulación |
| notes | Texto (500) | No | — | Nota de la aplicación |

### Catálogos nuevos (semilla `HasData`, IDs tentativos — Anexo A.2)

- `RECURRING_INCOME_STATUS_CATALOG` (wire `recurring-income-statuses`): `EN_REVISION`, `VIGENTE`, `RECHAZADO`, `SUSPENDIDO`, `FINALIZADO`, `ANULADO` → `-9880…-9885`.
- `RECURRING_INCOME_SETTLEMENT_ACTION_CATALOG` (wire `recurring-income-settlement-actions`): `PAGAR_SALDO`, `CANCELAR` → `-9886`/`-9887`.
- `RECURRING_INCOME_TYPE_CATALOG` (wire `recurring-income-types`, **P-02 ratificada** — tipos de ingresos cíclicos, prestaciones permanentes independientes del salario; editable): `AYUDA_ALIMENTACION`, `GASTOS_REPRESENTACION`, `COMBUSTIBLE`, `OTRO` → `-9896…-9899`.
- `PAYROLL_TYPE_CATALOG` (definición de REQ-004, **re-ubicada**): `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO` → `-9890…-9895`.
- Holgura del bloque: `-9888`/`-9889` (candidato: concepto de liquidación `INGRESO_CICLICO_PENDIENTE` si el plan técnico lo adopta).

### Catálogos/entidades existentes reutilizados (solo lectura)

`compensation-concept-types` (`Nature=Ingreso`) · `currencies` · `pay-periods` · `cost-centers` (entidad por empresa) · `payroll-period-definitions` (maestro REQ-001, cuando exista) · motor de liquidación (líneas sugeridas).

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Planilla externa** | Saliente (archivos) | Export de **insumo por periodo**: cuotas a pagar (empleado, plaza, concepto snapshot, centro de costo + cuentas contables, monto, moneda, nº de cuota, referencia). No se escribe `PersonnelFilePayrollTransaction` (RN-14) |
| **Catálogo de conceptos de compensación** | Interna (lectura) | «Tipo ingreso» = conceptos país `Nature=Ingreso` (`api/v1/compensation-concept-types`) con snapshot |
| **Centros de costo** | Interna (lectura) | FK + snapshot; default el de la plaza; aporta cuentas contables al insumo |
| **Catálogo de tipos de planilla (REQ-004)** | Interna (definición compartida) | Una sola definición; la siembra el REQ que construya primero; **seeds re-ubicados** por colisión (D-16) |
| **Maestro de periodos de planilla (REQ-001)** | Interna (futura, lectura) | `PayrollPeriodId?` de la cuota aplicada; degradación a etiqueta si este módulo se adelanta (D-10) |
| **Motor de liquidación** | Interna (bidireccional acotada) | `PAGAR_SALDO` → línea sugerida (mecanismo existente `SuggestedPlazaItem`/spec condicional); `CANCELAR` → cierre al emitir; reversión simétrica (RF-013) |
| **Ledger de sincronización externa** | Ninguna en F1 | `PersonnelFilePayrollTransaction` intacto; correlación cuota↔transacción externa = F2 si se ratifica (P-09) |
| **Futuro motor de planilla (F2)** | Interna (futura) | Aplicará cuotas con origen `MOTOR` sobre el mismo modelo/endpoint interno — contrato del historial estable (D-08) |
| **Correo / notificaciones** | Fase 2 | Aviso al autorizador (pendientes) y a RRHH (vencidas) |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de empresa | Catálogos editables; asignación de permisos | **No decide** ingresos salvo grant explícito `Authorize*` (la política excluye el fallback Admin en la decisión) |
| Gestor de RRHH / Analista de planilla | `ManageRecurringIncomes` (+ lecturas implícitas) | No puede decidir lo que él mismo registró (RN-03); ediciones solo `EN_REVISION`; aplica/anula cuotas con auditoría |
| Autorizador | `AuthorizeRecurringIncomes` | Anti-autoaprobación doble; motivos obligatorios; revocaciones trazables |
| Consulta / Auditor | `ViewRecurringIncomes` | Solo lectura de fichas, historiales, bandejas y exports |
| Finanzas / Planilla externa | `ViewRecurringIncomes` (export insumo) | Solo lectura/exportación |
| Empleado (autogestión) | Sin acceso en F1 (P-11 ratificada) | Lectura self de `VIGENTE`/`FINALIZADO` = evolución aditiva si el negocio la pide |

---

## 15. Criterios de aceptación generales

1. **Ratificación previa**: ✅ cumplida (2026-07-05) — D-01…D-18 ratificadas (D-03/D-12 ajustadas) y P-01…P-15 respondidas (§17); el plan técnico se deriva de este documento ratificado.
2. Reglas del plan de cuotas y ciclo de vida como **módulo puro** con suite unitaria (casos dorados Anexo A.3) y test de paridad de localización.
3. Suite de integración completa (CRUD + flujo con anti-autoaprobación doble, aplicación unitaria/lote con **test de carrera**, exclusión/posposición, ajuste de última cuota, anulación con reapertura, gates de permisos incluida la exclusión de Admin en `Authorize*`, bandeja de vencidas, insumo cuadrado contra pendientes, gancho de liquidación con reversión) **en verde junto con la suite existente**.
4. Migraciones `HasData` idempotentes con IDs verificados contra `GlobalCatalogSeedData` (bloque `-9880…-9895`; sin tocar reservas REQ-001/002/003; **colisión REQ-004 resuelta y coordinada en backlog**).
5. `openapi.yaml` regenerado **sin drift**; convenciones API respetadas (If-Match, `publicId`, enums string, errores bilingües, sub-recursos con `parentConcurrencyToken`).
6. La proyección de cuotas es derivada (cero filas futuras persistidas) y cuadra con las aplicadas en todos los estados.
7. Bandejas paginadas con `StatusCounts` y totales por moneda; exportaciones xlsx/csv/json; **insumo de planilla por periodo** cuadrando contra la bandeja de pendientes del mismo filtro.
8. Integración con liquidación retrocompatible (contrato del motor intacto; línea sugerida editable; guard anti-duplicado; reversión simétrica verificada).
9. El ledger externo y la configuración de conceptos quedan **intactos** (test de no-escritura).
10. Guía de integración frontend publicada (`guia-integracion-frontend-planilla-ingresos-ciclicos.md`) con contratos, estados, flujo de aplicación por periodo, modo degradado (sin maestro de periodos) y fuentes del historial.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Expectativa de motor de planilla**: el levantamiento nombra «generar la planilla»; si el negocio espera cálculo interno en esta entrega, F1 decepcionará. Mitigación: **ratificar P-01 primero**, comunicar el mapa del programa (Anexo A.1) y entregar el insumo por periodo como puente operativo.
- **Doble vía de registro con la configuración** (`CompensationConcept` vs ingreso cíclico): un bono recurrente podría registrarse en cualquiera de las dos. Mitigación: frontera editorial documentada (RN-16), capacitación y guía FE; validación blanda evaluable en F2.
- **Carga operativa de la aplicación manual**: si nadie aplica las cuotas, el historial y el insumo divergen de la realidad. Mitigación: bandeja de **vencidas** como lista de trabajo, aplicación en lote de un clic, export por periodo; el motor (F2) elimina la carga.
- **Colisión de seeds con REQ-004**: si ambos módulos siembran `PAYROLL_TYPE_CATALOG` con IDs distintos o duplicados, la migración choca. Mitigación: definición única re-ubicada (`-9890…-9895`), coordinación explícita en backlog (D-16) y verificación de IDs al abrir cada PR.
- **Multi-moneda sin FX**: totales por moneda pueden confundir si una empresa mezcla monedas en cuotas. Mitigación: agrupación por moneda en bandejas/exports (precedente off-payroll) y moneda default USD en UI.
- **Sensibilidad del dato de compensación**: fuga por permisos laxos. Mitigación: permisos dedicados, `Authorize*` sin Admin, sin autoservicio en F1 (P-11 controla la apertura).
- **Decisiones tardías** — **atenuado por la ratificación (2026-07-05)**: P-02 (dos catálogos con semántica definida), P-04 (captura del plan), P-06 (acciones) y P-15 (centro de costo) quedaron cerradas antes del plan técnico; el riesgo residual es solo evolutivo (F2).

### Supuestos

- La nómina se procesa **externamente** durante toda la F1; el insumo exportado es el mecanismo de entrega (P-01).
- Tenant mono-país (SV) como el resto del sistema; moneda predominante USD.
- Los ingresos cíclicos son pactados/otorgados por la empresa (no solicitados por el empleado): el flujo nace en RRHH, no en autoservicio.
- El catálogo de conceptos país ya contiene (o el administrador crea) los conceptos de ingreso necesarios (`Nature=Ingreso`).
- Quien aplica cuotas (RRHH/planilla) trabaja por periodo con el calendario de la empresa (maestro REQ-001 cuando exista).
- La opción «Nuevo descuento cíclico» y el «nivel de endeudamiento» entran **inmediatamente después de los ingresos cíclicos** como levantamiento propio (P-10 ratificada; el modelo queda preparado — D-02).

### Dependencias

- **Ratificación del negocio**: ✅ completada (2026-07-05, §17).
- **REQ-004**: definición del `PAYROLL_TYPE_CATALOG` (compartida; re-ubicación de seeds acordada) — no bloquea: la siembra el primero que construya.
- **REQ-001**: maestro `PayrollPeriodDefinition` para la FK de imputación — no bloquea: degradación a etiqueta documentada (D-10/D-17).
- **Liquidación**: mergeada (PR #56) — el gancho RF-013 no espera nada.
- Internas: verificación de IDs de seed libres al abrir el primer PR (bloque `-9880…-9895`); convenciones de catálogos/permisos vigentes.

---

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-05)

| # | Pregunta (síntesis) | Respuesta del negocio → efecto en el diseño |
|---|---|---|
| **P-01** | **(Estructural)** ¿La generación interna de planilla es un programa aparte, y esta F1 entrega los ingresos cíclicos sin motor? | **«El motor se realizará aparte»** → D-01 confirmada: F1 = las dos opciones detalladas sin motor (aplicación manual + insumo exportable); el motor de generación se levanta como REQ propio (Anexo A.1) |
| P-02 | ¿Qué distingue «tipo» de «tipo ingreso» y qué valores tendría cada catálogo? | **«Se requieren ambos catálogos»** con estas definiciones: (a) **Tipos de ingresos** = catálogo para registrar **todos los tipos de ingresos** que puede recibir el empleado — Salario, Aguinaldo, horas extras, vacaciones, bonificaciones, etc. → campo «tipo ingreso», cubierto por el catálogo país de conceptos `Nature=Ingreso` (D-03a; verificar filas en PR-1); (b) **Tipos de ingresos cíclicos** = catálogo de los **ingresos permanentes aplicables en las diferentes planillas, independientes del salario, por prestaciones** — ayuda para alimentación, gastos de representación, combustible, etc. → campo «tipo», catálogo nuevo `RECURRING_INCOME_TYPE_CATALOG` con plantilla A.2 (D-03b) |
| P-03 | ¿Estado `SUSPENDIDO` en F1? | **Sí incluirlo** → ciclo de 6 estados; `SUSPENDIDO` bloquea la aplicación sin alterar el plan (D-04/RN-06) |
| P-04 | ¿Captura del plan finito y edición del monto al aplicar? | **Valor + (número o total), derivar el tercero, última cuota ajusta; monto NO editable al aplicar** (anulación + re-registro cubre correcciones) → RN-05/RF-007 confirmadas |
| P-05 | ¿Relación frecuencia de cuotas ↔ tipo de planilla? | **Validación blanda en F1** (sin matriz de compatibilidad): la cuota mensual sobre planilla quincenal se aplica en el periodo que el analista elija; matriz estricta evaluable en F2 → D-13 confirmada |
| P-06 | ¿Bastan `PAGAR_SALDO`/`CANCELAR`? ¿Indefinidos? | **Sí bastan; indefinido → `CANCELAR` forzado (422 si se intenta `PAGAR_SALDO`)**; las devengadas no aplicadas se aplican antes de emitir la liquidación → D-11 confirmada |
| P-07 | ¿Adjuntos en F1? | **Sin adjuntos en F1** (la `referencia` documenta el origen); stack espejo como evolución si se pide → sin storage nuevo en el despliegue |
| P-08 | ¿Anulación con motivo o contra-asiento? | **Anulación con motivo** (más simple, saldo exacto, historial visible); contra-asiento descartado para cuotas → RN-08/RF-008 confirmadas |
| P-09 | ¿Correlación con el ledger externo? | **No en F1**; evaluable en F2 junto con el motor → RN-14 confirmada (ledger intacto) |
| P-10 | ¿Cuándo entran descuentos cíclicos + endeudamiento? | **«Entra después de los ingresos cíclicos»** → siguiente levantamiento inmediato tras este módulo; el modelo F1 queda preparado (D-02); umbral de endeudamiento como preferencia por empresa cuando llegue |
| P-11 | ¿Autoservicio del empleado en F1? | **Sin autoservicio en F1** (dato de compensación pactado por la empresa); lectura self de `VIGENTE`/`FINALIZADO` = evolución aditiva → D-14 confirmada |
| P-12 | ¿Una decisión o multi-nivel? | **Una decisión con anti-autoaprobación doble**; multi-nivel = F2 aditivo → D-04/D-05 confirmadas |
| P-13 | ¿Retroactividad (registro y cuotas de periodos pasados)? | **Sí a ambos** (Flujo 7): mismo flujo de autorización, sin modo especial de importación → RN-09 confirmada |
| P-14 | ¿Permisos dedicados o colapsar con `ViewCompensation`? | **Dedicados** (`View`/`Manage`/`AuthorizeRecurringIncomes`) → D-06 confirmada |
| P-15 | ¿Centro de costo obligatorio u opcional? | **«Obligatorio y debe estar relacionado a la plaza que tiene el empleado»** → D-12/RN-12 ajustadas: plaza obligatoria (default principal), centro de costo derivado de la plaza con snapshot; plaza sin centro de costo → 422 accionable |

---

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar P-01 antes que todo**: es la decisión que define si esto es una feature (F1 sin motor, ~6 RFs de núcleo + integraciones) o un programa plurianual (motor de nómina). Todo lo demás de este documento está diseñado para valer en ambos escenarios (el modelo de cuotas es el mismo que el motor consumirá).
2. **No construir el motor por accidente**: mantener el corte «registrar + aplicar + exportar» de F1 exactamente como los módulos monetarios previos (off-payroll, descuentos de REQ-003, liquidación como única excepción calculadora). El motor merece su propio levantamiento con casos dorados del contador (precedente liquidación).
3. **Diseñar la entidad preparada para naturaleza** (D-02): los descuentos cíclicos y el endeudamiento (visión explícita del levantamiento) reutilizan la maquinaria de cuotas tal cual; decidir tabla unificada vs espejo en el plan técnico, no en el modelo de negocio.
4. **Una sola definición del catálogo de tipos de planilla** y resolver ya la colisión de seeds con REQ-004 (`-9520…-9525` ocupados por ayuda económica): re-ubicar a `-9890…-9895`, actualizar el backlog de REQ-004 y verificar IDs al abrir cualquier PR — es el tipo de choque que rompe migraciones en cadena.
5. **La bandeja de vencidas es el corazón operativo de F1**: sin motor, la disciplina de aplicación depende de que RRHH tenga una lista de trabajo clara (pendientes/vencidas por periodo + lote de un clic + export). Invertir ahí el esfuerzo de UX.
6. **No tocar el ledger ni la configuración** (RN-14/RN-16): `PersonnelFilePayrollTransaction` sigue siendo la bitácora externa (trampa documentada) y `CompensationConcept` la definición estructural; comunicar al cliente la frontera entre «configurar compensación» y «registrar un ingreso cíclico».
7. **Cuotas persistidas solo al aplicarse, proyección derivada** (D-07/D-09): evita filas futuras obsoletas, hace exacto el saldo y deja el mismo contrato para cuando el motor aplique automáticamente (origen `MOTOR`).
8. **Secuenciar como REQ-005 al final del backlog** (D-17): hereda el maestro de periodos (REQ-001) y el catálogo de tipos de planilla (REQ-004) ya sembrados. Si el negocio necesita adelantarlo, es viable con las dos degradaciones documentadas (etiqueta de periodo + sembrar el catálogo aquí).
9. **MVP recortable si urge**: RF-001…RF-009 (catálogos + flujo + cuotas + historial) entregan las dos opciones del levantamiento; bandejas/exports (RF-010…012) y el gancho de liquidación (RF-013) pueden ser segunda entrega — aunque el insumo por periodo (RF-012) es lo que evita transcripciones y se recomienda mantener en F1.
10. **F2 ya perfilado** (Anexo A.1): descuentos cíclicos + endeudamiento (umbral por empresa), motor de generación (periodo activo, pool «aplicar/enviar», corrida con ISSS/AFP/Renta ya configurados, posteo), correlación con ledger, multi-nivel y notificaciones.

---

## Anexo A — Referencias y propuestas

### A.1 Mapa de la visión del módulo de planilla → cobertura y fases

| Capacidad de la visión (levantamiento) | Estado hoy | Cobertura |
|---|---|---|
| Registrar todo tipo de **ingresos** de los empleados | Configuración estructural existe (`CompensationConcept` `Nature=Ingreso`) | **F1 de este REQ**: ingresos cíclicos transaccionales con cuotas y autorización |
| Registrar todo tipo de **descuentos** | Configuración existe (`Nature=Egreso`, incl. ley con tasas por plaza); sin saldo/cuotas | **Rebanada siguiente**: descuentos cíclicos espejo (préstamos, embargos, etc.) sobre la misma maquinaria (D-02, P-10) |
| Consultas de **nivel de endeudamiento** | No existe (sin préstamos/saldos) | **Rebanada siguiente**: Σ cuotas de egresos activos vs salario + umbral por empresa (preferencia) — depende de descuentos cíclicos |
| **Creación de periodos de planilla** | Especificado en REQ-001 (`PayrollPeriodDefinition`, maestro por empresa, sin plantilla) | **REQ-001** — este módulo solo lo consume (imputación de cuotas) |
| **Generar la planilla** (cálculo del periodo) | No existe motor; insumos configurados sí (salario 3 niveles, ISSS/AFP por plaza, Renta por frecuencia) | **Programa futuro (levantamiento propio)**: periodo activo + pool + corrida + emisión + posteo al ledger (P-01) |
| Consultar **transacciones no aplicadas** en planilla | No existe | **F1 aproximación**: bandeja de cuotas pendientes/vencidas (RF-011); pool pleno = motor |
| **Aplicar al periodo activo / enviar a otro periodo** | No existe | **F1 aproximación**: aplicación en lote por periodo con exclusión (RF-007); semántica de «periodo activo» = motor |
| Historial de pago de un ingreso cíclico | No existe | **F1 de este REQ** (RF-009) |

### A.2 Seeds tentativos y hallazgo de colisión (verificar IDs libres contra `GlobalCatalogSeedData` al abrir el primer PR)

- **Ocupación verificada hoy (en código)**: piso general **-9846** (conceptos de liquidación `-9830…-9846`); banda -95xx ocupada por tablero (`-9500…-9514`), **ayuda económica (`-9520…-9526` tipos, `-9540…-9546` estados)** y otros reference (`-9560…-9595` con huecos); ActionTypes `-9470…-9484`; **trampa vigente** `-9490…-9496` (`ACTION_STATUS_CATALOG`).
- **Reservas de planes (no en código)**: REQ-001 `-9850…-9862` + `-9485…-9489` · REQ-002 `-9865…-9871` · REQ-003 `-9875…-9879`.
- **⚠️ Colisión detectada (2026-07-05)**: los tentativos de REQ-004 para `PAYROLL_TYPE_CATALOG` (`-9520…-9525`) **chocan con `ECONOMIC_AID_TYPE_CATALOG`** (`GlobalCatalogSeedData.cs:539-544`). El propio plan de REQ-004 pedía verificar al abrir PR-1 — la verificación se hizo aquí: **re-ubicar**.
- **Propuesta de este módulo (bloque contiguo libre `-9880…-9895`)**:
  - `RECURRING_INCOME_STATUS_CATALOG`: `EN_REVISION=-9880`, `VIGENTE=-9881`, `RECHAZADO=-9882`, `SUSPENDIDO=-9883`, `FINALIZADO=-9884`, `ANULADO=-9885`.
  - `RECURRING_INCOME_SETTLEMENT_ACTION_CATALOG`: `PAGAR_SALDO=-9886`, `CANCELAR=-9887`.
  - `PAYROLL_TYPE_CATALOG` (definición REQ-004 re-ubicada): `MENSUAL=-9890`, `QUINCENAL=-9891`, `SEMANAL=-9892`, `POR_DIA=-9893`, `POR_OBRA=-9894`, `OTRO=-9895`.
  - `RECURRING_INCOME_TYPE_CATALOG` (**P-02 ratificada** — plantilla editable): `AYUDA_ALIMENTACION=-9896`, `GASTOS_REPRESENTACION=-9897`, `COMBUSTIBLE=-9898`, `OTRO=-9899`.
  - Holgura: `-9888`/`-9889` (candidato: concepto de liquidación `INGRESO_CICLICO_PENDIENTE`, decisión del plan técnico).
- Sin ActionTypes nuevos (D-18: sin asientos de journal). Los catálogos de moneda/frecuencia/conceptos no se tocan (solo verificación de filas `Nature=Ingreso` para los ejemplos del negocio — RF-001).

### A.3 Casos dorados sugeridos para la validación del negocio

1. **Ingreso finito e2e**: 6 cuotas de $50 (total $300) quincenal → autorizar por tercero → aplicar 6 lotes → `FINALIZADO` automático; historial con 6 aplicadas y saldo $0.
2. **Ajuste de última cuota**: total $100 en 3 cuotas de $33.33 → última = $33.34 (proyección y aplicación coinciden).
3. **Anti-autoaprobación doble**: el registrador intenta decidir → 403; el empleado sujeto (con permiso de autorizar) intenta decidir → 403.
4. **Posposición**: lote quincenal con 1 cuota excluida → queda vencida en la bandeja del siguiente periodo y se aplica ahí con su fecha teórica original trazada.
5. **Carrera**: doble submit concurrente del mismo lote → cero duplicados; segunda respuesta 409/422.
6. **Anulación con reapertura**: anular la cuota 6/6 de un `FINALIZADO` → vuelve a `VIGENTE` con saldo de 1 cuota; re-aplicar → vuelve a `FINALIZADO`.
7. **Indefinido**: solo valor + frecuencia; sin saldo; acción `PAGAR_SALDO` → 422 (P-06); cierre manual con motivo → `FINALIZADO`.
8. **Liquidación**: empleado con ingreso finito saldo $300 `PAGAR_SALDO` → línea sugerida $300 en el finiquito (editable); con `CANCELAR` → sin línea y cierre al emitir; anular la liquidación → reapertura a `VIGENTE`.
9. **Retirado**: alta sobre `RETIRADO` → 422; pendiente `EN_REVISION` de un retirado → solo rechazo/anulación.
10. **Insumo**: export del periodo cuadra exactamente contra la bandeja de pendientes del mismo filtro; excluye suspendidos y anulados.

### A.4 La frontera «nómina externa» (contexto de P-01 — citas verificadas)

- `PersonnelFileCompensation.cs:8-10` (comentario de dominio): la nómina se procesa fuera de CLARIHR.
- `analisis-plazas-ingresos-egresos.md` D-08: «el cálculo (resolver %, Renta por tramos, topes, generar movimientos, neto) es un módulo futuro de nómina, fuera de alcance».
- `analisis-otras-transacciones-personal.md` RN-14 (ratificado): «no se escriben ledgers de planilla; el efecto monetario viaja solo por exportación de insumo».
- `analisis-tiempo-compensatorio-empleado.md` G-06 (ratificado): mismo corte para el valor de horas.
- Única excepción calculadora: el **motor de liquidación** (finiquito, PR #56) — construido con levantamiento propio, casos dorados del contador y checklist pre-despliegue. Ese es el precedente de esfuerzo/proceso si el negocio decide construir el motor de planilla (P-01).
