# Análisis de negocio — Módulo de planilla: ingresos eventuales (registro + búsqueda avanzada)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | Planilla — rebanada 2: **ingresos eventuales** (ingresos de una sola vez por empleado — horas extras, comisiones, bonos puntuales — con valor fijo o calculado por factores, imputación a planilla/periodo y **búsqueda avanzada** con agrupación y exportación a Excel) |
| **Fecha** | 2026-07-05 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código |
| **Estado** | **RATIFICADO por el negocio (2026-07-06)** — decisiones **D-01…D-18 ratificadas** y **P-01…P-13 respondidas** (§17): todas las recomendaciones del análisis **aceptadas**. Cierres clave: **P-01** flujo de **autorización de una decisión** confirmado (ciclo de 5 estados; alternativa sin autorización descartada) · **P-02** solicitante = **expediente de la empresa + snapshot** (trío) y **participa en la anti-autoaprobación** · **P-03/P-04** dos métodos de cálculo por factores, **sin tarifas legales ni lookup de salario** en F1 · **P-09** este **no** es el módulo de horas extras — «ese será otro módulo» (el registro de jornadas es levantamiento aparte; costura documentada) |
| **Naturaleza del módulo** | Greenfield transaccional **dentro de un programa ya faseado**: es el **hermano puntual** del REQ-005 (ingresos cíclicos, ratificado 2026-07-05) — mismo esqueleto (autorización, imputación planilla/periodo, aplicación manual F1, insumo exportable, gancho de liquidación) **sin plan de cuotas**; hereda la frontera estratégica «la nómina se procesa fuera de CLARIHR» (P-01 de REQ-005: «el motor se realizará aparte») |
| **Documentos hermanos** | [`analisis-planilla-ingresos-ciclicos.md`](analisis-planilla-ingresos-ciclicos.md) (REQ-005 — molde directo: D-03…D-16 de aquel documento se heredan casi una a una; su Anexo A.1 es el mapa del programa donde esta rebanada encaja) · [`analisis-plazas-ingresos-egresos.md`](analisis-plazas-ingresos-egresos.md) (la configuración de conceptos que este módulo consume) · [`analisis-transacciones-fuera-nomina-empleado.md`](analisis-transacciones-fuera-nomina-empleado.md) (frontera: aquello es gasto **fuera** de nómina; esto se procesa **en** una planilla) |
| **Documentos relacionados** | `analisis-ayuda-economica-empleado.md` (molde del flujo de una decisión + anti-autoaprobación) · `analisis-liquidacion-empleado.md` (motor de liquidación y líneas sugeridas — gancho de pendientes al liquidar) · `analisis-tiempo-compensatorio-empleado.md` (costura `overtimeRecordPublicId` hacia el futuro módulo de horas extras — P-09) · `analisis-tablero-acciones-personal.md` (REQ-004 — especificación del catálogo país de **tipos de planilla**) · `analisis-vacaciones-incapacidades-empleado.md` (REQ-001 — maestro de **periodos de planilla** por empresa) · [`plan-tecnico-planilla-ingresos-eventuales.md`](../technical/plan-tecnico-planilla-ingresos-eventuales.md) (plan de implementación de este módulo) |

---

## Contexto del cambio (requerimiento original)

> **Ingresos eventuales**: Esta opción debe permitir agregar ingresos eventuales de un empleado, tales como horas extras, comisiones, etc. La principal información que se ingresará es: **empleado** a quien se agregará el ingreso, **tipo de ingreso**, **fecha**, **valor fijo (sí/no)**, **valor a pagar**, **moneda (catálogo)**, **centro de costo**, **solicitante**, **planilla en que se procesa**, **periodo de planilla**. El formulario deberá permitir seleccionar si el monto a pagar **no es por valor fijo**, para lo cual se podrá realizar **por otros factores**.
>
> **Búsqueda avanzada de ingresos eventuales**: pantalla de búsqueda por criterios — **estado, empleado, tipo, fecha, valor fijo** — con **filtros para agrupar** la información; resultados **exportables a Excel**; la búsqueda deberá mostrar el **estado** del ingreso eventual y sus **detalles**.

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **Es la segunda rebanada natural del programa de planilla y hereda ~80 % de las decisiones ya ratificadas.** El REQ-005 (ingresos cíclicos, ratificado 2026-07-05) resolvió para su hermano recurrente exactamente los mismos problemas que este levantamiento plantea: tipo de ingreso (catálogo país de conceptos), moneda, centro de costo vía plaza (P-15), tipo de planilla (catálogo REQ-004), periodo (maestro REQ-001 con degradación), flujo de autorización de una decisión, aplicación manual por periodo con insumo exportable, y gancho de liquidación. Lo que cambia aquí: **no hay plan de cuotas** (una sola aplicación), aparecen el **«valor fijo sí/no» con cálculo por factores** y el **«solicitante»**, y la segunda opción es una **búsqueda avanzada corporativa con agrupación** en lugar de un historial por registro.
2. **El «tipo de ingreso» ya existe sembrado — incluidos los dos ejemplos del levantamiento.** El catálogo país `COMPENSATION_CONCEPT_TYPE_CATALOG` (`CompensationConceptTypeCatalogItem`, `Nature=Ingreso`) trae `HORAS_EXTRA` (`-9721`), `COMISION` (`-9722`), `BONO` (`-9723`), `VIATICOS` (`-9724`), `AGUINALDO` (`-9725`) y `OTRO_INGRESO` (`-9726`) (`GlobalCatalogSeedData.cs:936-952`). **Cero catálogos de tipos nuevos**: se reutiliza con snapshot, igual que el «tipo ingreso» del cíclico (D-03 de REQ-005, P-02a ratificada).
3. **No existe ninguna entidad de ingreso eventual** (verificado en `src/`, tests y contrato: sin `OneTimeIncome`/`EventualIncome`/`Overtime`/`Commission` transaccional; «EVENTUAL» solo existe como tipo de contrato). Los parientes: `PersonnelFileOffPayrollTransaction` (movimiento monetario editable — pero **fuera** de nómina, sin centro de costo ni solicitante), `PersonnelFileEconomicAidRequest` (molde de flujo con estados) y las líneas manuales de la liquidación (única captura puntual de «horas extras» hoy, y solo dentro de un finiquito).
4. **«Valor fijo sí/no… por otros factores» no tiene precedente transaccional y es la pieza nueva de fondo.** Propuesta: flag `isFixedValue`; cuando no es fijo, el formulario captura los **componentes del cálculo** — método `CANTIDAD_POR_VALOR` (cantidad × valor unitario × factor opcional, p. ej. 10 h × $2.50 × 1.5) o `PORCENTAJE_SOBRE_BASE` (p. ej. 3 % × $10,000 de ventas) — y el servidor **recalcula y valida la coherencia** con el valor a pagar (422 con desglose si no cuadra). **Sin tarifas legales automáticas ni lookup del salario** en F1: calcular el valor-hora o los recargos de ley pertenece al motor/módulo de horas extras futuro (frontera «sin motor» — P-04). El concepto país ya trae `DefaultCalculationType` (p. ej. `COMISION` = porcentaje sobre `SALARIO_BASE`), que el formulario usa como default del método.
5. **El «solicitante» tiene precedente exacto en el código**: liquidaciones y retiros modelan el trío `RequesterFilePublicId` (expediente del solicitante) + `RequesterNameSnapshot` + `RequestedByUserId` (auditoría de quién lo digitó) — `PersonnelFileSettlement.cs:114-123` y `PersonnelFileEmployee.cs:2456-2478` — y las bandejas/exports ya lo muestran como «Solicitante». Se propone el mismo trío (P-02 confirma semántica y si participa en la anti-autoaprobación).
6. **«Planilla en que se procesa» + «periodo de planilla» = el par ya especificado por REQ-004 y REQ-001** — la misma solución que las cuotas del cíclico (D-10 de REQ-005): `payrollTypeCode` contra el catálogo país `PAYROLL_TYPE_CATALOG` (definición única de REQ-004, **seeds re-ubicados `-9890…-9895`**; hoy `payroll_type_code` es texto libre en la plaza) + `PayrollPeriodId?` contra el maestro `PayrollPeriodDefinition` (REQ-001, no construido) con **etiqueta snapshot y degradación documentada** si este módulo se adelanta. Mientras el registro está pendiente, el periodo destino es **re-imputable** («enviar a otro periodo»).
7. **El levantamiento no menciona flujo de autorización — pero pide «estado» y «solicitante», y el hermano cíclico lo ratificó para dinero equivalente.** P-01 (estructural): se propone el **molde de una decisión** (`EN_REVISION → AUTORIZADO / RECHAZADO`, anti-autoaprobación, `Authorize*` que excluye Admin — mecánica idéntica a REQ-005 D-04/D-05), coherente con el objetivo ratificado «ningún ingreso adicional entra sin autorización de un tercero facultado». Alternativa sin autorización documentada (ciclo reducido `REGISTRADO → APLICADO`); la elige el negocio.
8. **La «búsqueda avanzada con agrupación» es la única pieza sin precedente completo.** Las bandejas corporativas existen y son el molde (POST `/query` con filtros + paginación + `StatusCounts` + GET `/export` — liquidaciones/constancias/retiros), pero la **agrupación por dimensión** («filtros para agrupar») es **net-new**: hoy el único rollup es el conteo por estado (`SettlementRepository.cs:355-363`). Propuesta acotada: parámetro `groupBy` con dimensiones fijas (estado, tipo, empleado, tipo de planilla, periodo, centro de costo, moneda, mes) que devuelve conteos y **sumas por moneda** por grupo (sin FX). La exportación a **Excel ya tiene infraestructura completa** (xlsx/csv/json OpenXML hecho a mano, cabeceras en español, tope síncrono, auditoría y rate limiting — `ReportExportFileWriter.cs` + `ReportExportDeliveryService`).
9. **La aplicación a planilla es el mismo puente F1 del hermano**: sin motor, alguien debe constatar que el ingreso se procesó — aplicación **manual** (unitaria o en **lote por periodo con exclusión** = posponer), bandeja de **pendientes/atrasados** («transacciones que no se aplicaron en planilla», visión del programa) e **insumo exportable por periodo** hacia la nómina externa. Cuando llegue el motor (F2), aplica sobre el mismo modelo con origen `MOTOR` sin cambio de contrato (D-08/D-09 de REQ-005, espejo).
10. **Seeds: el bloque `≤ -9900` está completamente libre** (verificado 2026-07-05 en doble pasada: piso real en código `-9846`; reservas de planes hasta `-9899` — REQ-001 `-9850…-9862`, REQ-002 `-9865…-9871`, REQ-003 `-9875…-9879`, REQ-005 `-9880…-9899`; nada en código ni en planes bajo `-9899`). Propuesta: estados `-9900…-9904`, concepto de liquidación `INGRESO_EVENTUAL_PENDIENTE` `-9905` (candidato), holgura `-9906…-9909` (Anexo A.2). **No** reutilizar `HORAS_EXTRAS_PENDIENTES=-9837`: REQ-002 lo reclama para el saldo de tiempo compensatorio.

### Trazabilidad campo a campo del levantamiento

| Campo pedido | Cobertura propuesta |
|---|---|
| Empleado | Expediente (`personnel-files/{publicId}/…`) con **plaza obligatoria** (default: la principal) — ancla del centro de costo y de la liquidación (D-11) |
| Tipo de ingreso | **Reutiliza** el catálogo país `compensation-concept-types` con `Nature=Ingreso` + snapshot (D-03) — `HORAS_EXTRA` y `COMISION` ya sembrados; `SALARIO_BASE` excluido (no es eventual) |
| Fecha | Fecha del hecho generador/otorgamiento (≤ hoy — RN-09; P-05) |
| Valor fijo (sí/no) | Flag `isFixedValue`; si **no** es fijo → cálculo **por factores** con componentes trazados y coherencia validada (D-07, P-03) |
| Valor a pagar | Monto (> 0): directo si fijo; **derivado de los componentes** si no (última palabra del servidor: recálculo + 422 si no cuadra) |
| Moneda (catálogo) | **Reutiliza** `CurrencyCatalogItem` (`currencies`; multi-moneda sin FX; default = moneda de la empresa `CompanyPreference.CurrencyCode`, USD) (D-13) |
| Centro de costo | **Reutiliza** `CostCenter`; **obligatorio y derivado de la plaza** con snapshot (espejo exacto de P-15 ratificada en REQ-005; D-11) |
| Solicitante | Trío `RequesterFilePublicId` + `RequesterNameSnapshot` + `RequestedByUserId` (precedente liquidación/retiro; D-12, P-02) |
| Planilla en que se procesa | **Reutiliza** el catálogo `PAYROLL_TYPE_CATALOG` (definición REQ-004, seeds re-ubicados `-9890…-9895`; la siembra el REQ que construya primero) (D-08) |
| Periodo de planilla | `PayrollPeriodId?` (maestro REQ-001) + **etiqueta snapshot**; re-imputable mientras esté pendiente («enviar a otro periodo»); degradación a etiqueta si este módulo se adelanta (D-08) |
| (Búsqueda) Estado / empleado / tipo / fecha / valor fijo | Filtros del `POST /query` corporativo + `StatusCounts` + totales por moneda (RF-008) |
| (Búsqueda) Filtros para **agrupar** | Parámetro `groupBy` con dimensiones fijas → conteos + sumas por moneda por grupo (RF-009 — pieza nueva) |
| (Búsqueda) Exportar a **Excel** | GET `/export?format=xlsx` (además csv/json de la casa) con filas en español + **insumo de planilla por periodo** (RF-011) |
| (Búsqueda) Estado + **detalles** | Fila con estado + GET de detalle (cálculo desglosado, decisión, aplicación, auditoría) (RF-008) |

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Dónde | Uso en este módulo |
|---|---|---|
| Catálogo país de conceptos: `CompensationConceptTypeCatalogItem` (`Nature` Ingreso/Egreso, `IsStatutory`, `DefaultCalculationType` Fixed/Percentage, `DefaultCalculationBaseCode`, `IsBaseSalary`) + GET `api/v1/compensation-concept-types?nature=`; seeds `-9720…-9736` con **`HORAS_EXTRA=-9721` y `COMISION=-9722`** | `Compensation/CompensationConceptTypeCatalogItem.cs`; `CompensationConceptTypesController.cs`; `GlobalCatalogSeedData.cs:936-952` | «Tipo de ingreso» del formulario: conceptos activos `Nature=Ingreso` + snapshot; `DefaultCalculationType` como default del método de cálculo (D-03/D-07) |
| Catálogo de monedas `CurrencyCatalogItem` (`currencies`, USD sembrado) + **default por empresa** `CompanyPreference.CurrencyCode` (USD) | `GeneralCatalogItems.cs:714`; `Preferences/CompanyPreference.cs` | «Moneda» del formulario — multi-moneda **sin FX**, totales agrupados por moneda (D-13) |
| Centros de costo: entidad por empresa `CostCenter` (código, nombre, tipo, **cuentas contables de planilla**: gasto/patronal/provisión); la plaza (`PersonnelFileEmploymentAssignment`) tiene `CostCenterPublicId` **nullable** | `CostCenters/CostCenter.cs`; `CostCentersController.cs`; `PersonnelFileEmployee.cs` (plaza) | «Centro de costo»: derivado de la plaza + snapshot; plaza sin centro de costo → 422 accionable (espejo P-15 de REQ-005; D-11) |
| Plaza con `PayrollTypeCode` **texto libre** (max 80, nullable) y `IsPrimary` (plaza principal) | `PersonnelFileEmployee.cs:187` | El «tipo de planilla» del ingreso usa el **catálogo** (REQ-004) y hereda como default el de la plaza si está clasificada (D-08) |
| Catálogo país de **tipos de planilla** `PAYROLL_TYPE_CATALOG` (`MENSUAL`/`QUINCENAL`/`SEMANAL`/`POR_DIA`/`POR_OBRA`/`OTRO`) — **especificado (REQ-004), NO construido; seeds re-ubicados `-9890…-9895`** | `plan-tecnico-tablero-acciones-personal.md` §3.1/§4; re-ubicación en `analisis-planilla-ingresos-ciclicos.md` A.2 | «Planilla en que se procesa»: misma definición única; la siembra el REQ que construya primero (D-08) |
| Maestro de **periodos de planilla por empresa** `PayrollPeriodDefinition` (tipo, año, número, rango) — **especificado (REQ-001), NO construido** | `plan-tecnico-vacaciones-incapacidades.md` §3.1 | «Periodo de planilla»: FK opcional + etiqueta snapshot; degradación a etiqueta si este módulo se adelanta (D-08) |
| Flujo de una decisión con estado híbrido (constantes canónicas + catálogo editable) + **anti-autoaprobación** en handler (403 cuando el decisor es el sujeto) + PATCH de resolución con If-Match | `PersonnelFileEmployee.cs:1678+` (ayuda económica); `EconomicAidRequests.Handlers.cs:371-376` | Molde del flujo de autorización propuesto (D-04/D-05, condicionado a P-01) |
| Permiso `Authorize*` que **excluye Admin** + **controller de resolución dedicado** (el atributo `AuthorizationPolicySet` es class-only) | `RetirementRequestResolutionController.cs`; `ProvisioningConstants.cs` | `AuthorizeOneTimeIncomes` con el mismo corte; decisión en controller dedicado (D-05/D-06) |
| «Solicitante» de primera clase: trío `RequesterFilePublicId` + `RequesterNameSnapshot` + `RequestedByUserId`, expuesto en bandejas/exports como «Solicitante» | `PersonnelFileSettlement.cs:114-123`; `PersonnelFileEmployee.cs:2456-2478` (retiro) | «Solicitante» del formulario: mismo trío (D-12, P-02) |
| Movimiento monetario editable de referencia: `PersonnelFileOffPayrollTransaction` (monto no-cero, periodo `Year`/`Month`, corrección por contra-asiento, **`/totals` por moneda**) — **sin** centro de costo ni solicitante | `PersonnelFileEmployee.cs:1442`; `OffPayrollTransactionsController.cs` | **Frontera** (RN-16/A.4): aquello es gasto fuera de nómina; el ingreso eventual se procesa **en** una planilla. Precedente de totales por moneda |
| Ledger inmutable de sincronización externa `PersonnelFilePayrollTransaction` (PATCH solo `isActive`; `PayrollPeriodCode` texto libre) | `PersonnelFileEmployee.cs:645`; `PersonnelFileCompensationController.cs:368+` | **No se toca** (RN-14): sigue siendo la bitácora externa y el destino del cálculo futuro |
| Motor de liquidación (mergeado PR #56): **líneas sugeridas** desde el contexto de cálculo + líneas manuales de ingreso (descripción obligatoria en `OTRO_*`/horas extras); catálogo `SETTLEMENT_CONCEPT_CATALOG` `-9830…-9846` (`HORAS_EXTRAS_PENDIENTES=-9837` — **reclamado por REQ-002**) | `PersonnelFileSettlement.cs:578-700`; `SettlementRepository.cs:126-152` | Gancho de pendientes al liquidar: línea sugerida con concepto **nuevo** `INGRESO_EVENTUAL_PENDIENTE` (D-15, RF-012) |
| Bandeja corporativa + exportación: `POST /companies/{id}/{recurso}/query` (filtros + paginación + **`StatusCounts`**) + `GET /export?format=` — molde liquidaciones/constancias/retiros; **rollup existente = solo conteo por estado** | `SettlementsReportingController.cs`; `SettlementsBandeja.cs`; `SettlementRepository.cs:355-363` | Búsqueda avanzada (RF-008); la **agrupación por dimensión es net-new** (RF-009) |
| Exportación de archivos: **OpenXML hecho a mano** (sin ClosedXML/EPPlus) — xlsx/csv/json, columnas por reflexión (propiedades en español = cabeceras), tope síncrono (413), auditoría `ReportExported`, rate limiting; pipeline async de jobs para volúmenes grandes | `Reports/ReportExportFileWriter.cs`; `Api/Common/ReportExportDeliveryService.cs`; `ReportExportJobsController.cs` | Export a **Excel** del levantamiento + insumo de planilla (RF-011) |
| Journal de acciones (`PersonnelFilePersonnelAction`, con `Amount?`/`CurrencyCode?`) | `PersonnelFileEmployee.cs:571-643` | **Sin asientos** (D-18, espejo de REQ-005): dato de compensación, no hecho del expediente laboral |
| Gates de autogestión (`LinkedUserPublicId`) y preferencias por empresa (columna nullable + setter + PATCH) | `PersonnelFileEmployeeHandlerBases.cs`; `CompanyPreference.cs` | Sin autoservicio F1 (D-14); umbral de doble autorización futuro como preferencia (P-12) |
| Convenciones API: `api/v1`, If-Match (faltante→400, obsoleto→409) + ETag rotativo, enums string, errores bilingües `extensions.code`, `publicId` | `ProblemDetailsFactory.cs`; `FromIfMatchAttribute.cs` | Aplican a todos los endpoints nuevos (RNF) |

### Lo que NO existe (verificado exhaustivamente en `src/`, tests y contrato)

- Ninguna entidad de **ingreso eventual / puntual** (sin `OneTimeIncome`, `EventualIncome`, `OvertimeRecord`, `CommissionRecord`; «EVENTUAL» es solo un tipo de contrato del catálogo `CONTRACT_TYPE`).
- Ningún **cálculo por factores** transaccional (cantidad × valor, % × base): el único cálculo del sistema es el motor de liquidación; los conceptos de compensación guardan configuración (`Fixed`/`Percentage`) pero nadie la ejecuta.
- Ninguna **agrupación por dimensión** en bandejas (solo `StatusCounts` por estado); ninguna búsqueda corporativa con `groupBy`.
- Ningún **catálogo de tipos de planilla** en código (`payroll_type_code` texto libre; catálogo especificado en REQ-004 con seeds re-ubicados) ni **maestro de periodos** (especificado en REQ-001).
- Ningún motor de planilla (frontera ratificada en cadena; P-01 de REQ-005: «el motor se realizará aparte»).
- **REQ-001…REQ-005 no están construidos** (planes escritos, sin commit): el catálogo de tipos de planilla y el maestro de periodos llegan con ellos — o los siembra este módulo si se adelanta (degradaciones D-08).
- **Espacio de seeds**: piso real en código **`-9846`**; reservas de planes hasta **`-9899`** (REQ-001 `-9850…-9862` y `-9485…-9489` · REQ-002 `-9865…-9871` · REQ-003 `-9875…-9879` · REQ-005 `-9880…-9899` incluido `payroll-types -9890…-9895`); trampa vigente `-9490…-9496` (`ACTION_STATUS_CATALOG`). **Bloque `≤ -9900` completamente libre** (verificado — Anexo A.2).

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha detectada | Propuesta de resolución |
|---|---|---|
| G-01 | «Ingresos eventuales» sin entidad: no hay dónde registrar un ingreso puntual con imputación a planilla | Nueva entidad transaccional del expediente (`PersonnelFileOneTimeIncome`) — hermana sin cuotas del ingreso cíclico (REQ-005); frontera documentada con la configuración estructural y con off-payroll (D-02, A.4) |
| G-02 | El levantamiento pide «estado» y «solicitante» pero no define ciclo de vida ni autorización | Propuesta: ciclo de **5 estados** con flujo de **una decisión** (molde ayuda económica/REQ-005: anti-autoaprobación, `Authorize*` sin Admin) — **P-01 estructural** decide; alternativa sin autorización documentada (D-04) |
| G-03 | «Valor no fijo… por otros factores» sin precedente: nada calcula montos transaccionales hoy | Flag + **dos métodos de cálculo** con componentes persistidos y coherencia validada en servidor; **sin tarifas legales ni lookup de salario** (frontera sin-motor); el concepto país aporta el default del método (D-07, P-03/P-04) |
| G-04 | «Planilla en que se procesa» es hoy texto libre en la plaza; el catálogo (REQ-004) no está construido | Misma definición única re-ubicada (`-9890…-9895`); la siembra el REQ que arranque primero; este módulo valida contra él → 422 si inválido (D-08) |
| G-05 | «Periodo de planilla» sin maestro construido (REQ-001 lo especifica) | Par `PayrollTypeCode` + `PayrollPeriodId?` + **etiqueta snapshot** (espejo de cuotas REQ-005/incapacidades REQ-001); degradación a etiqueta si este módulo se adelanta (D-08) |
| G-06 | «Solicitante» no existe en los módulos monetarios (off-payroll/ayuda económica no lo tienen como campo de negocio) | Trío `RequesterFilePublicId`+`RequesterNameSnapshot`+`RequestedByUserId` (precedente exacto liquidación/retiro); semántica y anti-autoaprobación → P-02 (D-12) |
| G-07 | «Filtros para agrupar la información»: el único rollup existente es el conteo por estado | Parámetro `groupBy` con **dimensiones fijas** (estado/tipo/empleado/tipo de planilla/periodo/centro de costo/moneda/mes) → conteos + sumas **por moneda**; pieza nueva pero acotada (EF `GroupBy`) (RF-009, P-08) |
| G-08 | «Transacciones que no se aplicaron en planilla» (visión del programa) sin aproximación para eventuales | Bandeja de **pendientes de aplicar** (+ marca de atrasado si el periodo destino ya pasó) + aplicación **en lote por periodo con exclusión** (= posponer) + **insumo exportable por periodo**; el motor pleno = F2 (D-09, RF-010) |
| G-09 | Cierre al liquidar sin definición: ¿qué pasa con un ingreso autorizado no aplicado si el empleado se retira? | Gancho al motor de liquidación: pendientes → **línea sugerida** `INGRESO_EVENTUAL_PENDIENTE` (editable, snapshot); al emitir → `APLICADO` origen `LIQUIDACION`; reversión simétrica (D-15, RF-012) |
| G-10 | Cuádruple vía monetaria potencial (concepto estructural / ingreso cíclico / ingreso eventual / off-payroll): riesgo de registrar lo mismo en el lugar equivocado | **Tabla de fronteras** (Anexo A.4) + regla editorial en UI/guía FE; sin bloqueo duro en F1 (RN-16) |

---

## Decisiones — D-01…D-18 (**RATIFICADAS por el negocio, 2026-07-06**)

> ✅ **Ratificadas junto con las respuestas P-01…P-13 (§17) — todas las recomendaciones del análisis aceptadas sin ajustes.** Numeración espejo de REQ-005 para facilitar la lectura comparada; las marcadas «(espejo REQ-005)» heredan una decisión que el negocio ya había ratificado para el hermano cíclico y que aquí quedó confirmada.

| # | Tema | Decisión propuesta |
|---|---|---|
| D-01 | Fases | **F1 (este análisis)**: las dos opciones del levantamiento sin motor — registro con valor fijo o por factores + flujo de una decisión (P-01) + re-imputación/aplicación manual (unitaria/lote por periodo) + búsqueda avanzada con agrupación + exports (incluido insumo de planilla) + gancho de liquidación. **F2+ (programa planilla, A.1 de REQ-005)**: el motor aplica automáticamente (origen `MOTOR`), notificaciones, multi-nivel por umbral, correlación con el ledger externo, registro de jornadas de horas extras (P-09) |
| D-02 | Modelado | **Entidad transaccional del expediente** `PersonnelFileOneTimeIncome` (nombre final lo fija el plan técnico) — NO se enriquece `PersonnelFileCompensationConcept` ni se tocan ledgers. **Hermana del ingreso cíclico** (REQ-005 `PersonnelFileRecurringIncome`): el plan técnico decide cuánta maquinaria comparten (base común vs entidades gemelas), sabiendo que esta **no tiene cuotas** — una sola aplicación |
| D-03 | Tipo de ingreso (espejo REQ-005) | Referencia al **catálogo país existente** `compensation-concept-types` con `Nature=Ingreso` (activo) + **snapshot** código+nombre al registrar. `HORAS_EXTRA`/`COMISION`/`BONO`/`VIATICOS`/`AGUINALDO`/`OTRO_INGRESO` ya sembrados. **`IsBaseSalary` excluido** (el salario base no es un ingreso eventual → 422). Sin catálogo nuevo de tipos (a diferencia del cíclico, aquí el levantamiento solo pide un campo de tipo) |
| D-04 | Flujo y estados (**P-01 ratificada**) | **Una decisión**: crear → `EN_REVISION` (editable); decidir → `AUTORIZADO` (pendiente de aplicar en planilla) o `RECHAZADO` (motivo obligatorio); `AUTORIZADO` → `APLICADO` al constatarse el pago en planilla (RF-006); `ANULADO` desde `EN_REVISION` (retiro del trámite) o desde `AUTORIZADO` (revocación con motivo). Ciclo de **5 estados**, patrón híbrido (constantes canónicas + catálogo editable). La alternativa sin autorización quedó **descartada** por la ratificación de P-01 |
| D-05 | Anti-autoaprobación | **Doble + solicitante**: quien decide no puede ser (a) el empleado sujeto del expediente (`LinkedUserPublicId`), (b) quien registró (`RequestedByUserId`), ni (c) **el solicitante** (vía `LinkedUserPublicId` del expediente solicitante) → 403 `SELF_APPROVAL_FORBIDDEN`. La extensión (c) es nueva respecto del precedente doble de REQ-003/REQ-005 — **confirmada por P-02** |
| D-06 | Permisos | 3 codes dedicados: `PersonnelFiles.ViewOneTimeIncomes` / `ManageOneTimeIncomes` / `AuthorizeOneTimeIncomes` (espejo de P-14 de REQ-005: «dedicados»). `View`/`Manage` con receta estándar (fallback `Admin`/`ManageAdministration`); **`Authorize*` con `RequireAssertion` que excluye Admin** (molde `AuthorizeRetirement`); decisión en **controller dedicado** (`AuthorizationPolicySet` es class-only); bandeja `POST /query` **sin** `AuthorizationPolicySet` con gate en handler (molde reporting controllers) |
| D-07 | Valor fijo / factores | Flag **`isFixedValue`**. Fijo → `amount` directo (> 0). No fijo → método `CANTIDAD_POR_VALOR` (`quantity` × `unitValue` × `multiplier` opcional default 1.00 — p. ej. horas × valor-hora × recargo) **o** `PORCENTAJE_SOBRE_BASE` (`percentage` × `baseAmount` — p. ej. comisión % × ventas). Componentes **persistidos** (trazabilidad del cálculo) + `amount` = resultado redondeado a 2 decimales; el servidor **recalcula y valida coherencia** (422 con desglose). El `DefaultCalculationType` del concepto país preselecciona el método en FE. **Sin lookup automático de salario ni tarifas legales** (P-04): los valores los digita RRHH; el cálculo normativo pertenece al motor/módulo de horas extras futuro |
| D-08 | Imputación de planilla y periodo (espejo REQ-005 D-10) | Par **`payrollTypeCode`** (catálogo REQ-004, obligatorio; default: el de la plaza si está clasificada) + **`payrollPeriodId?`** (FK al maestro REQ-001 cuando exista) + **`payrollPeriodLabel`** snapshot (obligatoria — «periodo de planilla» del levantamiento). **Re-imputable mientras `AUTORIZADO`** («enviar a otro periodo», con If-Match y auditoría); al aplicar se snapshotea el periodo **real**. Degradación a etiqueta libre documentada si este módulo se adelanta a REQ-001 |
| D-09 | Aplicación (F1) (espejo REQ-005 D-08) | **Manual por RRHH/planilla** con `ManageOneTimeIncomes`: unitaria o **en lote por periodo** (tipo de planilla + periodo → pendientes del periodo; **exclusión por registro = posponer**). Bajo **lock anti-carrera** + re-verificación de estado en transacción (un ingreso se aplica **una sola vez**). Origen `MANUAL` (F1) / `MOTOR` (F2) / `LIQUIDACION` (RF-012) sobre el mismo modelo |
| D-10 | Corrección de un aplicado | **Reversión de la aplicación con motivo**: `APLICADO → AUTORIZADO` (la aplicación anulada queda trazada — histórico visible), re-aplicable al periodo correcto o anulable. Sin borrado físico; **contra-asiento descartado** salvo ratificación en contra (espejo de P-08 de REQ-005) (P-06) |
| D-11 | Centro de costo y plaza (espejo REQ-005 D-12 / P-15 ratificada) | **Plaza asociada obligatoria** (default: la **principal**) y **centro de costo obligatorio, derivado de la plaza** (`CostCenterPublicId` + snapshot de nombre); plaza sin centro de costo configurado → 422 accionable. La sugerencia de liquidación se ancla a la plaza principal |
| D-12 | Solicitante (**P-02 ratificada**) | Trío del precedente liquidación/retiro: **`RequesterFilePublicId`** (expediente del solicitante — normalmente la jefatura que pide el pago; puede ser el propio empleado; no requiere ser usuario del sistema) + **`RequesterNameSnapshot`** + **`RequestedByUserId`** (auditoría de quién digitó). Obligatorio y **participa en la anti-autoaprobación** (D-05) |
| D-13 | Moneda (espejo REQ-005) | **Reutilización pura**: `CurrencyCatalogItem` activo (422 si inválido); default = `CompanyPreference.CurrencyCode` (USD). Multi-moneda **sin FX**: totales y agrupaciones **por moneda** (precedente off-payroll `/totals`) |
| D-14 | Autogestión (espejo REQ-005 P-11) | F1 **sin autoservicio** (dato de compensación otorgado por la empresa); lectura self como evolución aditiva si el negocio la pide (gate `isSelf` existente). Sin escritura self en ningún caso |
| D-15 | Retiro y liquidación | Perfil `RETIRADO`: sin altas nuevas; pendientes `EN_REVISION` solo `RECHAZADO`/`ANULADO`. Al **liquidar**: los `AUTORIZADO` no aplicados **sugieren su monto como línea** del finiquito (concepto nuevo `INGRESO_EVENTUAL_PENDIENTE`; editable/excluible, snapshot); al **emitir**, los sugeridos-e-incluidos pasan a `APLICADO` con origen `LIQUIDACION` + referencia a la liquidación; **anular la liquidación los reabre** a `AUTORIZADO` (simetría del gancho existente). **No** se reutiliza `HORAS_EXTRAS_PENDIENTES=-9837` (reclamado por REQ-002 para tiempo compensatorio) |
| D-16 | Catálogos y seeds | Nuevo: `ONE_TIME_INCOME_STATUS_CATALOG` (5 estados) → **`-9900…-9904`**; concepto de liquidación `INGRESO_EVENTUAL_PENDIENTE` → **`-9905`** (candidato, lo adopta el plan técnico); holgura `-9906…-9909`. `PAYROLL_TYPE_CATALOG` = definición compartida REQ-004 (`-9890…-9895`), la siembra el primero que construya. Todos `HasData` idempotentes; verificación de IDs libres al abrir el PR (bloque `≤ -9900` verificado libre — A.2). **Sin ActionTypes nuevos** (D-18) |
| D-17 | Secuenciación | Registrar como **REQ-006** al final del backlog. **Sinergia fuerte con REQ-005**: comparten catálogo de tipos de planilla, molde de flujo, bandejas gemelas y gancho de liquidación — se recomienda construirlos **contiguos** (REQ-005 → REQ-006) para amortizar el andamiaje. **Adelantable** con dos degradaciones (etiqueta de periodo + sembrar `payroll-types` aquí), igual que el hermano |
| D-18 | Asientos de expediente (espejo REQ-005) | **Sin asientos** en el journal (`PersonnelFilePersonnelAction`): dato de compensación, no hecho del expediente laboral (el journal admite `Amount`/`CurrencyCode`, pero el precedente ratificado es no asentar datos monetarios de compensación). Aditivo si el negocio lo pide |

---

## 1. Resumen del producto o requerimiento

Se construirá la **segunda rebanada del módulo de planilla** de CLARIHR: el registro y la consulta de los **ingresos eventuales** de los empleados — pagos **de una sola vez** como horas extras, comisiones, bonos puntuales o incentivos — con **valor fijo o calculado por factores**, imputados a una **planilla y periodo** concretos, sujetos a **autorización** (P-01 ratificada), aplicables al periodo (manual en F1, motor en F2) y consultables mediante una **búsqueda avanzada corporativa** con filtros, **agrupación** y **exportación a Excel**.

**Qué se construye (F1).**

1. **Nuevo ingreso eventual**: registro sobre el expediente con empleado (y su plaza), tipo de ingreso (catálogo país de conceptos — horas extras, comisiones…), fecha, **valor fijo (sí/no)** — si no es fijo, el monto se calcula **por factores** (cantidad × valor unitario × factor, o porcentaje × base) con el desglose trazado —, valor a pagar, moneda, centro de costo (derivado de la plaza), **solicitante**, y la **planilla + periodo** donde se procesará.
2. **Flujo de autorización** (P-01 ratificada): el ingreso nace `EN_REVISION` y un tercero facultado lo autoriza o rechaza; anti-autoaprobación; revocación con motivo.
3. **Aplicación al periodo**: constancia de que el ingreso se procesó en la planilla (unitaria o en lote por periodo con exclusión = posponer); bandeja de **pendientes/atrasados** e **insumo exportable por periodo** para la nómina externa; re-imputación («enviar a otro periodo») mientras esté pendiente.
4. **Búsqueda avanzada** por empresa: criterios estado / empleado / tipo / fecha / valor fijo (+ tipo de planilla, periodo, centro de costo, moneda, solicitante), **agrupación por dimensión** con conteos y sumas por moneda, `StatusCounts`, detalle completo por registro y **exportación a Excel** (xlsx/csv/json).
5. **Integración con liquidación**: los autorizados no aplicados se sugieren como línea del finiquito al liquidar al empleado (cierre limpio de compromisos).

**Qué NO se construye en F1.** El **motor de planilla** (cálculo de tarifas legales de horas extra, valor-hora desde el salario, corrida del periodo) — frontera ratificada en cadena («el motor se realizará aparte», P-01 de REQ-005); el **registro de jornadas de horas extras** (fechas/horarios trabajados con su propia autorización — costura documentada hacia el futuro módulo, P-09); notificaciones; autorización multi-nivel; correlación con el ledger externo.

**Problema que resuelve.** Hoy un ingreso puntual (horas extra de un mes, una comisión, un bono único) no tiene dónde registrarse: la configuración de compensación es estructural/recurrente, las transacciones fuera de nómina son gasto no-salarial, y el ledger de planilla es una bitácora inmutable de sincronización. No hay trazabilidad de quién solicitó y autorizó el pago, en qué planilla/periodo se procesó, ni un insumo por periodo para la nómina externa — ni una búsqueda corporativa que agrupe y exporte estos pagos.

---

## 2. Objetivos del negocio

1. **Control y debido proceso sobre el gasto salarial eventual**: cada pago puntual queda registrado con solicitante, autorización de un tercero facultado (anti-autoaprobación) y auditoría completa (P-01 ratificada — espejo del objetivo de los cíclicos).
2. **Trazabilidad del cálculo**: cuando el valor no es fijo, los factores (cantidad, valor unitario, recargo, porcentaje, base) quedan persistidos — se puede auditar cómo se llegó al monto de cada pago.
3. **Insumo exacto para la nómina**: exportación por planilla + periodo de los ingresos autorizados pendientes, eliminando transcripciones manuales hacia el sistema de planilla externo; el modelo queda listo para que el futuro motor aplique automáticamente.
4. **Visibilidad corporativa**: búsqueda avanzada con agrupación (por tipo, estado, empleado, periodo, centro de costo…) y exportación a Excel — la vista gerencial/operativa que el levantamiento pide.
5. **Imputación contable y de costos**: cada ingreso lleva plaza y centro de costo (con sus cuentas contables) para alimentar la contabilidad de planilla.
6. **Cierre limpio al liquidar**: los compromisos autorizados no pagados se resuelven en el finiquito (línea sugerida), sin pagos huérfanos al retiro del empleado.

---

## 3. Alcance funcional

### Fase 1 — MVP (este análisis)

- **Configuración**: catálogo de estados del ingreso eventual; adopción/siembra del catálogo país de **tipos de planilla** (definición REQ-004, seeds re-ubicados); 3 permisos RBAC (`View`/`Manage`/`Authorize` — P-01 ratificada); verificación de conceptos `Nature=Ingreso` (los ejemplos del levantamiento ya están sembrados).
- **Ingresos eventuales**: crear/editar (`EN_REVISION`) con valor fijo o por factores; decidir (autorizar → `AUTORIZADO` / rechazar con motivo); anular/revocar; **re-imputar el periodo destino** («enviar a otro periodo»).
- **Aplicación**: unitaria y **en lote por periodo con exclusión** (posponer); reversión de una aplicación con motivo; bandeja de **pendientes/atrasados**.
- **Búsqueda avanzada**: consulta corporativa con los criterios del levantamiento + extras de la casa; **agrupación por dimensión** (conteos + sumas por moneda); `StatusCounts`; detalle por registro; consulta por expediente.
- **Exportaciones**: búsqueda y bandejas a **xlsx**/csv/json; **insumo de planilla por periodo** (empleado, concepto, centro de costo con cuentas, monto, moneda, solicitante).
- **Integración con liquidación**: pendientes → línea sugerida `INGRESO_EVENTUAL_PENDIENTE`; cierre al emitir; reversión simétrica.

### Fase 2+ — Programa de planilla (contrato preparado, fuera de este MVP)

- **Motor de generación**: aplica los ingresos eventuales del periodo automáticamente (origen `MOTOR`), calcula tarifas legales (valor-hora, recargos CT) y posteos.
- **Registro de jornadas de horas extras** (módulo propio: fechas/horarios, autorización de las horas, costura `overtimeRecordPublicId` de REQ-002 — P-09).
- Autorización **multi-nivel** / umbral de monto (P-12); notificaciones; autoservicio de lectura; correlación con el ledger externo.

---

## 4. Fuera de alcance

- **Cálculo normativo de horas extras** (valor-hora desde el salario, recargos legales CT SV): F1 registra los factores que RRHH digita; el cálculo legal pertenece al motor/módulo de horas extras futuro (P-04).
- **Registro de jornadas** (qué días/horas se trabajaron): este módulo registra el **pago**; el registro operativo de horas es levantamiento aparte (P-09).
- **Motor de planilla** (periodo activo, corrida, neto, posteo): programa aparte ya faseado (P-01 de REQ-005).
- **Escritura de ledgers**: ni `PersonnelFilePayrollTransaction` (sincronización externa inmutable) ni conceptos de compensación automáticos (RN-14).
- **Autorización multi-nivel / umbrales de monto** (F2 — P-12 ratificada: F1 sin umbral); F1 entrega el flujo de una decisión.
- **Conversión de monedas / FX** (multi-moneda sin conversión; agrupación por moneda).
- **Notificaciones** (al autorizador o al solicitante) — F2.
- **Adjuntos** (P-07): la referencia documental y el solicitante cubren la trazabilidad F1; stack espejo si el negocio exige respaldo de jefatura (asimetría con REQ-002 documentada en la pregunta).
- **Autoservicio del empleado** (D-14; evolución aditiva).
- **Importador masivo** de históricos (adopción = registro retroactivo por el flujo normal, P-05).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador de empresa** | Mantiene catálogos editables, configura y asigna permisos |
| **Gestor de RRHH / Analista de planilla** (con `ManageOneTimeIncomes`) | Registra y edita ingresos eventuales, re-imputa periodos, **aplica** (unitaria/lote), revierte aplicaciones con motivo, exporta el insumo |
| **Solicitante** (jefatura/área que pide el pago) | Origina la solicitud fuera o dentro del sistema; queda **registrado en el campo solicitante** (trío con snapshot); no requiere ser usuario del sistema (P-02) |
| **Autorizador** (con `AuthorizeOneTimeIncomes`) | Decide (autoriza o rechaza) los ingresos `EN_REVISION`; revoca autorizados con motivo; sujeto a anti-autoaprobación; **Admin no decide** salvo grant explícito |
| **Consulta de RRHH / Auditor** (con `ViewOneTimeIncomes`) | Solo lectura de fichas, búsqueda avanzada, agrupaciones y exportaciones |
| **Finanzas / Contabilidad** | Consume el insumo por periodo (montos con centro de costo y cuentas contables) y los exports |
| **Sistema de planilla externa** | Recibe el insumo del periodo y paga los ingresos (mientras no exista el motor interno) |
| **Motor de liquidación (interno)** | Sugiere los pendientes como línea del finiquito y dispara el cierre al emitir (RF-012) |
| **Empleado (autogestión)** | Sin acceso en F1 (D-14); lectura del propio historial = evolución aditiva |

---

## 6. Requerimientos funcionales

> Agrupados en 4 grupos (A: configuración · B: registro y flujo · C: aplicación · D: búsqueda, exportaciones e integración). Prioridades: Alta = imprescindible F1; Media = F1 deseable.

### Grupo A — Configuración y catálogos

### RF-001 - Estados, catálogo y permisos del módulo

**Descripción:** Sembrar el catálogo de estados del ingreso eventual (`EN_REVISION`, `AUTORIZADO`, `RECHAZADO`, `APLICADO`, `ANULADO` — P-01 ratificada) y declarar los 3 permisos (D-06) con la receta completa. Verificar que el catálogo país de conceptos (`Nature=Ingreso`) cubre los tipos del negocio (`HORAS_EXTRA` y `COMISION` ya sembrados) y completar filas si faltara alguno.

**Reglas de negocio:**
- Patrón híbrido (constantes canónicas en dominio + catálogo editable para i18n/UI; validación `CatalogCodeIsActiveAsync` en escrituras).
- IDs de semilla en bloque nuevo **`-9900…-9904`** (Anexo A.2), verificados contra `GlobalCatalogSeedData` al abrir el PR (respetar reservas REQ-001/002/003/005; trampa `-9490…-9496`).
- `AuthorizeOneTimeIncomes` con `RequireAssertion` que excluye Admin; `View`/`Manage` con fallback estándar; gates fail-closed + governance tests; decisión en controller dedicado.

**Criterios de aceptación:**
- Migración `HasData` idempotente; usuario sin permiso → 403 en cada endpoint; Admin sin `Authorize*` no puede decidir.

**Prioridad:** Alta
**Dependencias:** Verificación de IDs libres; D-04/D-05/D-06/D-16.

### RF-002 - Catálogo país de tipos de planilla (definición compartida con REQ-004)

**Descripción:** Adoptar la especificación única de REQ-004 (`PAYROLL_TYPE_CATALOG`: `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO`) con **seeds re-ubicados `-9890…-9895`** — la siembra el REQ que se construya primero (REQ-001/004/005/006 la comparten).

**Reglas de negocio:**
- Una sola definición del catálogo en todo el sistema; si otro REQ ya lo sembró al arrancar este módulo, aquí solo se consume.
- El ingreso eventual valida `payrollTypeCode` contra el catálogo activo → 422 si inválido; default: el tipo de planilla de la plaza si está clasificada.
- La migración de limpieza destructiva de `payroll_type_code` en plazas es **de REQ-004** (no se repite aquí).

**Criterios de aceptación:**
- Ingreso con tipo de planilla inexistente/inactivo → 422 bilingüe; catálogo consultable por el FE (`payroll-types`).

**Prioridad:** Alta
**Dependencias:** Coordinación con REQ-004/REQ-005 (D-16 de ambos); backlog actualizado.

### Grupo B — Registro y flujo

### RF-003 - Crear y editar ingreso eventual

**Descripción:** Alta por RRHH sobre el expediente: fecha, **tipo de ingreso** (concepto país `Nature=Ingreso`, con snapshot), **plaza** (obligatoria, default la principal) con su **centro de costo derivado**, **solicitante** (trío con snapshot), **valor fijo (sí/no)** — si fijo, valor a pagar directo; si no, método de cálculo + componentes con el valor derivado —, **moneda**, **planilla en que se procesa** (catálogo) y **periodo de planilla** destino (FK/etiqueta), referencia y observaciones (opcionales). Nace `EN_REVISION`; editable mientras siga en revisión.

**Reglas de negocio:**
- Solo `ManageOneTimeIncomes`; If-Match en ediciones; perfil `RETIRADO` → 422 (RN-10).
- Tipo de ingreso: concepto país activo `Nature=Ingreso` → snapshot código+nombre (RN-04); `IsBaseSalary` → 422.
- Plaza obligatoria (default principal) y centro de costo **derivado de la plaza** con snapshot (RN-12); plaza sin centro de costo configurado → 422 accionable.
- Valor: fijo → `amount` > 0; no fijo → componentes válidos (> 0) y **coherencia recalculada por el servidor** (redondeo a 2 decimales; 422 con desglose si no cuadra) (RN-05).
- Solicitante obligatorio: expediente activo de la empresa + snapshot (RN-03/P-02).
- Tipo de planilla del catálogo activo; periodo destino: FK al maestro si existe (o etiqueta en modo degradado) (RN-17).
- El estado nunca se digita (RN-01); solo `EN_REVISION` es editable (RN-02).

**Criterios de aceptación:**
- POST → 201 `EN_REVISION` con `publicId` y ETag; no-fijo con 10 × $2.50 × 1.5 → `amount` $37.50 calculado y desglose consultable; componentes incoherentes con el monto → 422 con los valores en el detalle; edición tras autorizar → 422.

**Prioridad:** Alta
**Dependencias:** RF-001, RF-002.

### RF-004 - Decidir ingreso eventual (autorizar / rechazar)

**Descripción:** PATCH de decisión sobre un ingreso `EN_REVISION`: **autorizar** (queda `AUTORIZADO`, pendiente de aplicar y visible en el insumo de su periodo) o **rechazar** (motivo obligatorio).

**Reglas de negocio:**
- Solo `AuthorizeOneTimeIncomes` (excluye Admin); **anti-autoaprobación** (RN-03): 403 si quien decide es el empleado sujeto, quien registró **o el solicitante** (D-05).
- Re-verificación de estado dentro de la transacción (dos decisiones concurrentes → la segunda 409/422).
- Sobre perfil `RETIRADO` solo se admite rechazar/anular (D-15).

**Criterios de aceptación:**
- Decisión por el registrador o el solicitante → 403; autorizar → `AUTORIZADO` y aparece en la bandeja de pendientes de su periodo; rechazar sin motivo → 422.

**Prioridad:** Alta
**Dependencias:** RF-003.

### RF-005 - Ciclo post-decisión: re-imputar periodo, anular / revocar

**Descripción:** (a) **Re-imputación del periodo destino** de un `AUTORIZADO` («enviar a otro periodo»): cambiar tipo de planilla y/o periodo antes de aplicar, con If-Match y auditoría; (b) **anulación** con motivo desde `EN_REVISION` (retiro del trámite) o desde `AUTORIZADO` (revocación — el compromiso se cancela sin efecto monetario).

**Reglas de negocio:**
- Motivos obligatorios en anulación/revocación; baja lógica, nunca borrado (RN-08).
- `RECHAZADO`/`ANULADO` son terminales; `APLICADO` no se anula directamente — primero se revierte la aplicación (RF-007, D-10).
- La re-imputación no altera monto/cálculo (solo destino); un `ANULADO` desaparece de bandejas e insumos.

**Criterios de aceptación:**
- Re-imputar un autorizado de la quincena 13 a la 14 → sale del insumo de la 13 y entra al de la 14 (trazado); revocar un autorizado → `ANULADO` con motivo, fuera de insumos.

**Prioridad:** Alta
**Dependencias:** RF-004.

### Grupo C — Aplicación

### RF-006 - Aplicar a planilla (unitaria y en lote por periodo)

**Descripción:** (a) Aplicación **unitaria**: constatar que un ingreso `AUTORIZADO` se procesó, con fecha y periodo real de imputación (default: el destino declarado) y nota; (b) aplicación **en lote por periodo**: para un tipo de planilla + periodo, el sistema presenta los autorizados pendientes y RRHH confirma el lote **pudiendo excluir registros** (posponer = «enviar a otro periodo»).

**Reglas de negocio:**
- Solo `ManageOneTimeIncomes`; solo ingresos `AUTORIZADO`; **lock anti-carrera** + re-verificación en transacción: un ingreso se aplica **una sola vez** (RN-06/RN-07).
- Al aplicar: snapshot del periodo real (tipo + FK/etiqueta), fecha, quién aplicó y origen `MANUAL` (F1; `MOTOR` en F2 y `LIQUIDACION` vía RF-012 sobre el mismo modelo) (D-09).
- El monto aplicado es el del registro (no editable al aplicar; correcciones = reversión + re-aplicación o revocación).

**Criterios de aceptación:**
- Lote del periodo con 10 pendientes y 2 excluidos → 8 `APLICADO`, 2 siguen pendientes; doble submit concurrente del lote → sin dobles aplicaciones (test de carrera); aplicar un `EN_REVISION`/`ANULADO` → 422.

**Prioridad:** Alta
**Dependencias:** RF-004.

### RF-007 - Revertir una aplicación

**Descripción:** Reversión con motivo de una aplicación registrada por error (periodo equivocado, no se procesó realmente): el ingreso vuelve a `AUTORIZADO` y la aplicación anulada queda trazada y visible; después puede re-aplicarse al periodo correcto o revocarse.

**Reglas de negocio:**
- Motivo obligatorio; quién/cuándo trazables; la aplicación revertida no cuenta para totales ni insumos pero permanece visible en el detalle (RN-08).
- Sin borrado físico; contra-asiento descartado salvo ratificación en contra (P-06, espejo de P-08 de REQ-005).

**Criterios de aceptación:**
- Revertir la aplicación de la quincena 13 → ingreso `AUTORIZADO` de nuevo con la reversión trazada; re-aplicar en la 14 → `APLICADO` con el nuevo snapshot; el detalle muestra ambas aplicaciones (anulada + vigente).

**Prioridad:** Alta
**Dependencias:** RF-006.

### Grupo D — Búsqueda, exportaciones e integración

### RF-008 - Búsqueda avanzada por empresa + consulta en ficha (opción 2 del levantamiento)

**Descripción:** (a) **Búsqueda avanzada corporativa** (`POST /companies/{companyId}/one-time-incomes/query`): criterios del levantamiento — **estado(s), empleado, tipo (concepto), rango de fechas, valor fijo (sí/no)** — más tipo de planilla, periodo, centro de costo, moneda, solicitante y texto libre; paginada, con **`StatusCounts`** y **totales por moneda**; cada fila muestra el **estado** y el acceso al **detalle** completo (desglose del cálculo, decisión, aplicaciones, auditoría). (b) Listado paginado por expediente (filtros: estado, tipo, rango de fechas).

**Reglas de negocio:**
- `View…`/`Manage…` para RRHH (bandeja corporativa sin autoservicio); gate en handler (el `POST /query` es lectura — molde reporting controllers) (RNF seguridad).
- Filtros combinables; paginación con tope (precedente 1–100); totales agrupados por moneda sin FX (RN-13).

**Criterios de aceptación:**
- Query con estado=`AUTORIZADO` + valorFijo=false + rango de fechas → página correcta con `StatusCounts` del filtro completo; el detalle de un no-fijo muestra cantidad/valor unitario/factor y el monto derivado.

**Prioridad:** Alta
**Dependencias:** RF-003.

### RF-009 - Agrupación de resultados («filtros para agrupar»)

**Descripción:** Sobre la misma búsqueda, parámetro **`groupBy`** con dimensiones fijas — **estado · tipo de ingreso · empleado · tipo de planilla · periodo · centro de costo · moneda · mes (de la fecha)** — que devuelve por grupo: conteo y **sumas por moneda** (y el desglose fijo/no-fijo). Una dimensión por consulta (P-08 confirma las dimensiones).

**Reglas de negocio:**
- La agrupación respeta todos los filtros activos de la búsqueda; los montos se suman **por moneda dentro de cada grupo** (nunca cruzando monedas) (RN-13).
- Pieza **nueva** (el precedente solo agrupa por estado): implementación acotada con agregación en base de datos; sin agrupación multi-nivel en F1.

**Criterios de aceptación:**
- `groupBy=tipo` con filtro de un trimestre → una fila por concepto con conteo y suma USD; `groupBy=mes` → serie mensual; los totales de la agrupación cuadran contra la búsqueda plana del mismo filtro.

**Prioridad:** Alta (es la mitad de la segunda opción del levantamiento)
**Dependencias:** RF-008.

### RF-010 - Bandeja de pendientes de aplicar («no aplicados en planilla»)

**Descripción:** Bandeja corporativa de ingresos `AUTORIZADO` no aplicados, con filtros por tipo de planilla, periodo destino y empleado, y marca de **atrasado** (el periodo destino ya venció sin aplicarse); desde la bandeja se lanza la aplicación en lote (RF-006) o la re-imputación (RF-005).

**Reglas de negocio:**
- Solo `AUTORIZADO` (los demás estados no son aplicables); es la aproximación F1 de «transacciones que no se aplicaron en planilla» de la visión del programa (RN-15).
- «Atrasado» = periodo destino con fecha fin pasada (cuando exista el maestro REQ-001) o etiqueta de periodo anterior al actual (modo degradado documentado).

**Criterios de aceptación:**
- Autorizado imputado a una quincena ya cerrada y sin aplicar → aparece marcado atrasado; tras aplicarlo o re-imputarlo desaparece de la vista del periodo.

**Prioridad:** Alta
**Dependencias:** RF-006.

### RF-011 - Exportaciones (Excel de la búsqueda + insumo de planilla por periodo)

**Descripción:** (a) Export **xlsx**/csv/json de la búsqueda avanzada y de la bandeja de pendientes (mismos filtros vía query string, filas en español — el «exportar a Excel» del levantamiento); (b) **export de insumo de planilla**: para un tipo de planilla + periodo, los ingresos autorizados a pagar (empleado, plaza, concepto snapshot, centro de costo con cuentas contables, monto, moneda, valor fijo/desglose, solicitante, referencia) — el puente operativo con la nómina externa.

**Reglas de negocio:**
- `View…` para exportar; formato inválido → 400; tope síncrono → 413; auditoría `ReportExported`; rate limiting existentes (molde `ReportExportDeliveryService`).
- El insumo incluye solo `AUTORIZADO` pendientes del periodo; excluye anulados/aplicados (RN-08); debe cuadrar contra la bandeja de pendientes del mismo filtro.

**Criterios de aceptación:**
- `GET /export?format=xlsx` descarga el archivo con cabeceras en español; el insumo de un periodo cuadra contra la bandeja en tests de integración.

**Prioridad:** Alta
**Dependencias:** RF-008, RF-010.

### RF-012 - Integración con liquidación (pendientes al retiro)

**Descripción:** Al preparar la liquidación de un empleado (motor mergeado PR #56), los ingresos eventuales `AUTORIZADO` no aplicados **sugieren su monto** como línea de ingreso del finiquito (concepto nuevo `INGRESO_EVENTUAL_PENDIENTE`; editable/excluible, con snapshot y traza). Al **emitir** la liquidación, los sugeridos-e-incluidos pasan a `APLICADO` con origen `LIQUIDACION` y referencia a la liquidación; **anular la liquidación los reabre** a `AUTORIZADO` (simetría del gancho existente).

**Reglas de negocio:**
- Solo plaza principal (anti doble pago, precedente REQ-002/REQ-005); línea editable/excluible por el analista; guard anti-duplicado si ya existe línea manual equivalente.
- **No** se reutiliza `HORAS_EXTRAS_PENDIENTES=-9837` (REQ-002 lo reclama para tiempo compensatorio); nada de esto escribe ledgers ni conceptos (RN-14).
- Los excluidos de la sugerencia siguen pendientes y se resuelven manualmente (aplicar o revocar) antes o después de emitir.

**Criterios de aceptación:**
- Liquidar con un eventual autorizado de $150 → línea sugerida de $150 visible/editable; al emitir con la línea incluida → el ingreso queda `APLICADO` origen `LIQUIDACION`; anular la liquidación → vuelve a `AUTORIZADO`.

**Prioridad:** Media (F1 deseable; recortable a segunda entrega sin bloquear el resto)
**Dependencias:** RF-004; módulo de liquidación (mergeado).

---

## 7. Requerimientos no funcionales

- **Seguridad**: dato de compensación **sensible**: lecturas con `ViewOneTimeIncomes`; `Authorize*` excluye Admin (separación de funciones); gates fail-closed por handler además de la política de controlador; la bandeja `POST /query` **omite** `AuthorizationPolicySet` (class-only, mapea por verbo) y gatea en el handler — molde reporting controllers con sus governance tests; anti-autoaprobación extendida al solicitante; 403 sin enmascaramiento.
- **Auditoría**: `CreatedUtc`/`ModifiedUtc`, quién registró/decidió/aplicó/revirtió y cuándo, motivos obligatorios, snapshots (concepto, centro de costo, solicitante, moneda, periodo real), componentes del cálculo persistidos, baja lógica universal, exportaciones auditadas (`ReportExported`).
- **Concurrencia/API**: convenciones del repo — `api/v1`, If-Match (faltante → 400, obsoleto → 409), ETag rotativo, `publicId`, enums string, errores bilingües `extensions.code`. La aplicación (unitaria y lote) corre bajo **lock anti-carrera** + re-verificación transaccional (test de doble submit obligatorio).
- **Rendimiento**: búsqueda y bandejas paginadas con índices por `(tenant, empresa, estado, fechas)` y por periodo destino; la **agrupación se agrega en base de datos** (una consulta por dimensión, sin materializar la página completa); exports con tope síncrono y rate limiting existentes (pipeline async de jobs disponible si el volumen lo exige).
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId`; sin jobs en F1 (la marca de atrasado es derivada en consulta, no programada).
- **Usabilidad**: errores accionables (incoherencia de componentes con el desglose completo en el detalle, plaza sin centro de costo con instrucción de completarla, tipo de planilla inválido); desglose del cálculo visible en el detalle; bandeja de atrasados como lista de trabajo.
- **Mantenibilidad**: reglas del cálculo por factores y del ciclo de vida en **módulo puro** (`OneTimeIncomeRules`) con suite unitaria (casos dorados Anexo A.3) y **paridad de localización**; `openapi.yaml` mantenido a mano vía skill, sin drift; guía FE.
- **Compatibilidad**: cambios 100 % aditivos (entidad, catálogo y permisos nuevos; catálogos existentes solo se consumen; contrato de liquidación intacto — la sugerencia es una línea más).
- **Accesibilidad**: (frontend) formulario con derivación visible del cálculo (componentes ↔ valor a pagar), búsqueda con agrupación navegable/exportable; se documenta en la guía FE.

---

## 8. Historias de usuario

### HU-001 - Registrar y autorizar un ingreso eventual de valor fijo
Como **gestor de RRHH**, quiero **registrar una comisión puntual de $150 para un empleado con su solicitante y su planilla/periodo destino**, para **que un autorizador la apruebe y quede lista para pagarse con trazabilidad completa**.

**Criterios de aceptación:**
- Dado un concepto `COMISION` activo, cuando registro el ingreso con valor fijo $150, moneda USD y quincena destino, entonces queda `EN_REVISION` con solicitante y centro de costo (derivado de la plaza) trazados.
- Dado un ingreso `EN_REVISION`, cuando el autorizador (tercero) lo autoriza, entonces queda `AUTORIZADO` y aparece en la bandeja de pendientes de su periodo.
- Dado que yo registré el ingreso (o soy el solicitante), cuando intento decidirlo, entonces recibo 403.

### HU-002 - Registrar horas extras con valor calculado por factores
Como **analista de planilla**, quiero **registrar 10 horas extra a $2.50 la hora con factor 1.5 y que el sistema calcule $37.50**, para **que el monto quede justificado por sus componentes y no por un número suelto**.

**Criterios de aceptación:**
- Dado valor fijo = no y método cantidad×valor, cuando envío 10 × $2.50 × 1.5, entonces el valor a pagar es $37.50 y el desglose queda persistido y visible en el detalle.
- Dado un monto que no cuadra con los componentes, cuando intento guardar, entonces recibo 422 con el desglose esperado.

### HU-003 - Aplicar los ingresos del periodo
Como **analista de planilla**, quiero **aplicar en lote los ingresos autorizados de la quincena**, para **que el módulo refleje lo que la planilla externa pagó y el insumo cuadre**.

**Criterios de aceptación:**
- Dado el filtro planilla `QUINCENAL` + periodo, cuando confirmo el lote, entonces cada ingreso queda `APLICADO` con snapshot del periodo real y origen `MANUAL`.
- Dado un doble clic/submit concurrente, entonces ningún ingreso se aplica dos veces.

### HU-004 - Enviar un ingreso a otro periodo
Como **analista de planilla**, quiero **excluir un ingreso del lote o re-imputar su periodo destino**, para **pagarlo en el siguiente periodo sin perderlo ni re-registrarlo**.

**Criterios de aceptación:**
- Dado un ingreso excluido del lote, cuando consulto la bandeja del siguiente periodo (tras re-imputarlo), entonces aparece disponible ahí y ya no en el original.
- Dado un autorizado cuyo periodo destino venció sin aplicarse, entonces aparece marcado como atrasado.

### HU-005 - Buscar, agrupar y exportar
Como **consulta de RRHH**, quiero **buscar ingresos eventuales por estado, empleado, tipo, fecha y valor fijo, agrupar por tipo o por mes y exportar a Excel**, para **analizar el gasto eventual sin armar reportes a mano**.

**Criterios de aceptación:**
- Dado un filtro combinado, cuando consulto, entonces obtengo la página con `StatusCounts`, totales por moneda y el estado + detalle de cada registro.
- Dado `groupBy=tipo`, entonces obtengo conteos y sumas por moneda por concepto, cuadrando contra la búsqueda plana.
- Dado `format=xlsx`, entonces descargo el archivo con cabeceras en español.

### HU-006 - Corregir una aplicación equivocada
Como **gestor de RRHH**, quiero **revertir con motivo la aplicación hecha al periodo equivocado y re-aplicarla al correcto**, para **que el historial quede exacto y auditable**.

**Criterios de aceptación:**
- Dada la reversión con motivo, entonces el ingreso vuelve a `AUTORIZADO`, la aplicación anulada sigue visible en el detalle y puedo re-aplicar al periodo correcto.

### HU-007 - Resolver pendientes al liquidar
Como **analista de liquidaciones**, quiero **que los ingresos eventuales autorizados no pagados se sugieran como línea del finiquito**, para **cerrar los compromisos del empleado sin cálculos manuales**.

**Criterios de aceptación:**
- Dado un autorizado de $150 pendiente, cuando preparo la liquidación, entonces aparece la línea sugerida `INGRESO_EVENTUAL_PENDIENTE` de $150 editable.
- Dada la liquidación emitida con la línea incluida, entonces el ingreso queda `APLICADO` origen `LIQUIDACION`; si la liquidación se anula, vuelve a `AUTORIZADO`.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | El **estado nunca se digita**: lo fija el flujo (`EN_REVISION` al crear; `AUTORIZADO`/`RECHAZADO` al decidir; `APLICADO` al aplicar — manual, motor o liquidación; `ANULADO` por retiro del trámite o revocación) — P-01 ratificada |
| RN-02 | Ciclo de vida: solo `EN_REVISION` es editable (campos de negocio); en `AUTORIZADO` solo se re-imputa el periodo destino; `RECHAZADO`/`ANULADO` son terminales; `APLICADO` solo admite reversión de la aplicación (→ `AUTORIZADO`) |
| RN-03 | **Anti-autoaprobación extendida**: quien decide no puede ser el empleado sujeto (`LinkedUserPublicId`), quien registró (`RequestedByUserId`) ni el **solicitante** → 403 (D-05, P-02) |
| RN-04 | «Tipo de ingreso» = concepto país activo `Nature=Ingreso`, con **snapshot** (código+nombre) al registrar; `IsBaseSalary` → 422; cambios posteriores del catálogo no recalculan históricos |
| RN-05 | Valor a pagar > 0. Si **no es fijo**: componentes obligatorios según el método (`quantity`/`unitValue`[/`multiplier`] o `percentage`/`baseAmount`), todos > 0; el servidor **recalcula** (redondeo 2 decimales) y valida coherencia → 422 con desglose; los componentes se persisten (trazabilidad del cálculo) |
| RN-06 | Solo `AUTORIZADO` es aplicable; un ingreso se aplica **una sola vez** (la reversión lo devuelve a aplicable); aplicar en otro estado → 422 |
| RN-07 | La aplicación (unitaria/lote) corre bajo **lock anti-carrera** con re-verificación de estado en la transacción; registra snapshot del periodo real, fecha, quién y origen (`MANUAL`/`MOTOR`/`LIQUIDACION`) |
| RN-08 | Nada se borra físicamente: anulaciones y reversiones con motivo, visibles y excluidas de totales/insumos; las aplicaciones revertidas permanecen en el detalle |
| RN-09 | Fecha del ingreso (hecho generador) ≤ hoy (P-05); el periodo destino puede ser pasado (regularización) o futuro (pago programado) |
| RN-10 | Perfil `RETIRADO`: sin altas; pendientes solo rechazables/anulables; los `AUTORIZADO` se resuelven vía liquidación (RF-012) o manualmente antes del cierre |
| RN-11 | Gancho de liquidación: pendientes → línea sugerida `INGRESO_EVENTUAL_PENDIENTE` (editable, snapshot, plaza principal); al emitir → `APLICADO` origen `LIQUIDACION`; anular la liquidación revierte el cierre (simetría) |
| RN-12 | Centro de costo **obligatorio y derivado de la plaza** (default: principal) + snapshot; plaza sin centro de costo configurado → 422 accionable (espejo P-15 REQ-005) |
| RN-13 | Multi-moneda **sin conversión**: totales, `StatusCounts` monetarios y **agrupaciones** suman por moneda (nunca cruzando monedas) |
| RN-14 | **No se escriben ledgers ni configuración**: ni `PersonnelFilePayrollTransaction` ni `PersonnelFileCompensationConcept`; el efecto monetario viaja por aplicación + exportación de insumo |
| RN-15 | «Transacciones no aplicadas» = bandeja de pendientes/atrasados; «enviar a otro periodo» = re-imputación del destino o exclusión del lote; la semántica de «periodo activo» pertenece al motor (F2) |
| RN-16 | Fronteras documentadas (Anexo A.4): el **concepto de compensación** define lo estructural; el **ingreso cíclico** (REQ-005) lo recurrente con cuotas; el **ingreso eventual** lo puntual de una vez; la **transacción fuera de nómina** el gasto no-salarial; el **ledger** la sincronización externa — comunicado en UI/guía FE; sin bloqueo duro en F1 |
| RN-17 | El periodo usa el par `payrollTypeCode` (catálogo) + `PayrollPeriodId?`/etiqueta snapshot (maestro REQ-001 cuando exista); nunca texto sin snapshot |
| RN-18 | Los catálogos consumidos (conceptos, monedas, tipos de planilla, centros de costo vía plaza) deben estar activos al usarse → 422; los históricos conservan snapshot |

---

## 10. Flujos principales

### Flujo 1 — Ingreso eventual de valor fijo end-to-end
1. RRHH abre el expediente y elige «Nuevo ingreso eventual».
2. Ingresa tipo de ingreso (p. ej. `COMISION`), fecha, valor fijo = sí, valor a pagar, moneda, plaza (default principal — el centro de costo se deriva), solicitante, planilla y periodo destino.
3. El sistema valida catálogos y guarda `EN_REVISION` (201 + ETag).
4. El autorizador revisa y **autoriza** (o rechaza con motivo); el sistema verifica la anti-autoaprobación (sujeto/registrador/solicitante).
5. Al autorizar: el ingreso queda `AUTORIZADO` y aparece en la bandeja de pendientes y en el insumo de su periodo.

### Flujo 2 — Ingreso con valor por factores (horas extras)
1. RRHH selecciona tipo `HORAS_EXTRA` y valor fijo = **no**.
2. El formulario ofrece el método (default por el concepto): cantidad × valor unitario × factor — digita 10 × $2.50 × 1.5.
3. El sistema calcula $37.50, valida la coherencia y persiste los componentes con el monto.
4. Sigue el flujo de autorización normal; el detalle muestra siempre el desglose.

### Flujo 3 — Aplicación del periodo (lote) e insumo
1. El analista abre la bandeja de pendientes y filtra tipo de planilla + periodo.
2. El sistema lista los autorizados del periodo (más los atrasados de periodos vencidos).
3. El analista excluye los que se posponen y confirma el lote; cada ingreso queda `APLICADO` (snapshot, origen `MANUAL`) bajo lock.
4. Exporta el **insumo del periodo** y lo entrega a la nómina externa (o lo exportó antes de aplicar, según su operación).

### Flujo 4 — Enviar a otro periodo
1. Un autorizado no debe pagarse en su periodo destino.
2. El analista lo re-imputa (cambia periodo/tipo de planilla con If-Match) o simplemente lo excluye del lote.
3. El ingreso aparece en la bandeja e insumo del nuevo periodo; si su periodo venció sin aplicarse, se marca atrasado.

### Flujo 5 — Búsqueda avanzada con agrupación y Excel
1. El usuario abre «Búsqueda avanzada de ingresos eventuales».
2. Filtra por estado, empleado, tipo, rango de fechas y/o valor fijo (más planilla/periodo/centro de costo/moneda/solicitante).
3. El sistema devuelve la página con `StatusCounts`, totales por moneda y el estado de cada registro; el detalle muestra el desglose completo.
4. El usuario agrupa (`groupBy=tipo|mes|…`) para ver conteos y sumas por moneda por grupo.
5. Exporta a Excel (xlsx) el resultado del filtro.

### Flujo 6 — Corrección de una aplicación
1. RRHH detecta un ingreso aplicado al periodo equivocado (o que no se pagó realmente).
2. Revierte la aplicación con motivo → vuelve a `AUTORIZADO` (la aplicación anulada queda trazada).
3. Re-aplica al periodo correcto, o revoca el ingreso si no procede.

### Flujo 7 — Liquidación con pendientes
1. El analista prepara la liquidación (módulo existente).
2. El sistema sugiere una línea `INGRESO_EVENTUAL_PENDIENTE` por cada autorizado no aplicado (plaza principal), editable/excluible.
3. Al **emitir**, los incluidos quedan `APLICADO` origen `LIQUIDACION`; si la liquidación se anula, se reabren a `AUTORIZADO`.

### Flujo 8 — Adopción (históricos)
1. RRHH registra retroactivamente los ingresos eventuales pagados antes del sistema (fecha pasada, periodo real) y los pasa por autorización.
2. Los aplica con su periodo histórico para que la búsqueda y las agrupaciones reflejen la realidad (P-05).

---

## 11. Flujos alternativos y excepciones

- **Decisión por el sujeto, el registrador o el solicitante** → 403 `SELF_APPROVAL_FORBIDDEN` (RN-03).
- **Componentes incoherentes con el valor a pagar** (no-fijo) → 422 con el desglose esperado en el detalle (RN-05).
- **No-fijo sin componentes** o **fijo con componentes** → 422 (el método exige exactamente sus campos).
- **Concepto de naturaleza egreso, inactivo o `IsBaseSalary`** → 422 (RN-04).
- **Plaza del empleado sin centro de costo configurado** → 422 accionable (completar la plaza primero — RN-12).
- **Solicitante inexistente/inactivo** → 422; **moneda/tipo de planilla/periodo inválidos o inactivos** → 422 (RN-18).
- **Aplicar un ingreso no `AUTORIZADO`** (en revisión, rechazado, anulado, ya aplicado) → 422; **doble submit concurrente** → el segundo 409/422 (lock + re-verificación, RN-06/RN-07).
- **Re-imputar o editar un `APLICADO`** → 422 (primero revertir la aplicación).
- **Revertir una aplicación ya revertida** → 422; **rechazo/anulación/revocación/reversión sin motivo** → 422.
- **Editar/decidir un registro no `EN_REVISION`** → 422/409.
- **Perfil `RETIRADO`**: alta → 422; decidir → solo rechazo/anulación (D-15).
- **If-Match ausente** → 400; **obsoleto** → 409.
- **Usuario sin permiso** → 403 (gestión, decisión, búsqueda, exports); **Admin sin `Authorize*`** → 403 al decidir.
- **Insumo sin tipo de planilla + periodo** → 400 (filtro obligatorio del insumo).
- **`groupBy` con dimensión no soportada** → 400 con la lista de dimensiones válidas.
- **Maestro de periodos (REQ-001) aún no construido** → la imputación degrada a etiqueta (modo degradado documentado, D-08).

---

## 12. Datos requeridos

> Convenciones del repo aplican: `long Id` interno + `Guid publicId` externo, `TenantId`, `CreatedUtc`/`ModifiedUtc`, `isActive` (baja lógica), `concurrencyToken` rotativo (If-Match), factoría `Create(...)` + mutadores custodiados. Se listan solo los campos de negocio.

### Entidad: Ingreso eventual (`PersonnelFileOneTimeIncome` — sub-registro del expediente)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| incomeDate | Fecha | Sí | ≤ hoy (RN-09) | «Fecha» del levantamiento (hecho generador/otorgamiento) |
| conceptTypeCode / conceptNameSnapshot | Código / Texto | Sí | Concepto país activo `Nature=Ingreso`, no `IsBaseSalary` (RN-04) | «Tipo de ingreso» + snapshot |
| isFixedValue | Booleano | Sí | — | «Valor fijo (sí/no)» |
| calculationMethod | Enum string | Si no fijo | `CANTIDAD_POR_VALOR` \| `PORCENTAJE_SOBRE_BASE` (D-07) | Método del cálculo por factores |
| quantity / unitValue / multiplier | Decimales | Si método cantidad | > 0; `multiplier` opcional (default 1.00) | Cantidad (horas, unidades) × valor unitario × factor/recargo |
| percentage / baseAmount | Decimales | Si método porcentaje | > 0 | Porcentaje × monto base (p. ej. comisión sobre ventas) |
| amount | Decimal (18,2) | Sí | > 0; si no fijo: coherencia con componentes ± redondeo (RN-05) | «Valor a pagar» |
| currencyCode | Código catálogo | Sí | Moneda activa (RN-18); default preferencia de la empresa | «Moneda» |
| assignedPositionPublicId | Guid | Sí | Plaza del empleado (default: principal) | Plaza asociada (fuente del centro de costo; ancla de liquidación) |
| costCenterPublicId / costCenterNameSnapshot | Guid / Texto | Sí | **Derivado de la plaza** (RN-12); plaza sin centro → 422 | «Centro de costo» |
| requesterFilePublicId / requesterNameSnapshot | Guid / Texto | Sí | Expediente activo de la empresa (P-02) | «Solicitante» (trío con auditoría de registro) |
| payrollTypeCode | Código catálogo | Sí | `PAYROLL_TYPE_CATALOG` activo (RF-002); default el de la plaza | «Planilla en que se procesa» |
| payrollPeriodId / payrollPeriodLabel | FK? / Texto | No / Sí | Maestro REQ-001 cuando exista (RN-17); re-imputable en `AUTORIZADO` | «Periodo de planilla» destino + etiqueta snapshot |
| statusCode | Código catálogo | Sí (flujo) | `EN_REVISION`/`AUTORIZADO`/`RECHAZADO`/`APLICADO`/`ANULADO` (híbrido; RN-01; P-01 ratificada) | «Estado» — lo fija el flujo |
| reference | Texto (200) | No | — | Referencia documental (acuerdo, memo — propuesta más allá del levantamiento) |
| observations | Texto (1000) | No | — | Observaciones (propuesta más allá del levantamiento) |
| requestedByUserId | Guid | Sí | Usuario autenticado | Auditoría: quién digitó (anti-autoaprobación) |
| decidedByUserId / decidedUtc / decisionNote | Guid / Fecha / Texto (500) | Al decidir | Anti-autoaprobación (RN-03); motivo al rechazar | Auditoría de la decisión (P-01) |
| annulledByUserId / annulledUtc / annulmentReason | Guid / Fecha / Texto (500) | Al anular/revocar | Motivo obligatorio (RN-08) | Auditoría de anulación/revocación |
| *(aplicación vigente)* appliedDate / appliedPayrollTypeCode / appliedPayrollPeriodId / appliedPayrollPeriodLabel / appliedByUserId / applicationOrigin / applicationNote / settlementPublicId? | Fecha / Códigos / FK? / Texto / Guid / Enum / Texto / Guid? | Al aplicar | Snapshot del periodo **real**; origen `MANUAL`/`MOTOR`/`LIQUIDACION` (RN-07) | Constancia de la aplicación (referencia a la liquidación si vino de RF-012) |
| *(histórico)* aplicaciones revertidas | — | — | Visibles con motivo/quién/cuándo (RN-08) | El plan técnico decide representación (sub-registros — recomendado, espejo de cuotas REQ-005 — vs campos + bitácora) |

### Catálogos nuevos (semilla `HasData`, IDs tentativos — Anexo A.2)

- `ONE_TIME_INCOME_STATUS_CATALOG` (wire `one-time-income-statuses`): `EN_REVISION`, `AUTORIZADO`, `RECHAZADO`, `APLICADO`, `ANULADO` → **`-9900…-9904`**.
- Concepto de liquidación `INGRESO_EVENTUAL_PENDIENTE` → **`-9905`** (candidato; lo adopta el plan técnico en RF-012 — no reutilizar `HORAS_EXTRAS_PENDIENTES=-9837`, reclamado por REQ-002).
- Holgura del bloque: `-9906…-9909`.
- `PAYROLL_TYPE_CATALOG` (definición REQ-004, re-ubicada): `-9890…-9895` — la siembra el REQ que construya primero (aquí solo si este módulo se adelanta).

### Catálogos/entidades existentes reutilizados (solo lectura)

`compensation-concept-types` (`Nature=Ingreso`; `HORAS_EXTRA`/`COMISION`/`BONO`/`VIATICOS`/`AGUINALDO`/`OTRO_INGRESO` sembrados) · `currencies` (+ default `CompanyPreference.CurrencyCode`) · `cost-centers` (vía plaza) · `payroll-types` (definición REQ-004) · `payroll-period-definitions` (maestro REQ-001, cuando exista) · motor de liquidación (líneas sugeridas).

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Planilla externa** | Saliente (archivos) | Export de **insumo por periodo**: autorizados a pagar (empleado, plaza, concepto snapshot, centro de costo + cuentas contables, monto, moneda, desglose del cálculo, solicitante, referencia). No se escribe `PersonnelFilePayrollTransaction` (RN-14) |
| **Catálogo de conceptos de compensación** | Interna (lectura) | «Tipo de ingreso» = conceptos país `Nature=Ingreso` (`api/v1/compensation-concept-types`) con snapshot; `DefaultCalculationType` como default del método |
| **Centros de costo** | Interna (lectura) | Derivado de la plaza + snapshot; aporta cuentas contables al insumo |
| **Catálogo de tipos de planilla (REQ-004)** | Interna (definición compartida) | Una sola definición (`-9890…-9895`); la siembra el REQ que construya primero (D-16 de REQ-004/005/006 coordinados en backlog) |
| **Maestro de periodos de planilla (REQ-001)** | Interna (futura, lectura) | `PayrollPeriodId?` del destino y de la aplicación; degradación a etiqueta si este módulo se adelanta (D-08) |
| **Motor de liquidación** | Interna (bidireccional acotada) | Pendientes → línea sugerida `INGRESO_EVENTUAL_PENDIENTE` (mecanismo existente); cierre al emitir / reapertura al anular (RF-012) |
| **Módulo de horas extras (futuro)** | Ninguna en F1 (costura documentada) | REQ-002 dejó la costura `overtimeRecordPublicId` hacia un registro de jornadas; este módulo registra el **pago** — la conexión (jornada → pago) se define en aquel levantamiento (**P-09 ratificada: «será otro módulo»**) |
| **Ledger de sincronización externa** | Ninguna en F1 | `PersonnelFilePayrollTransaction` intacto; correlación = F2 junto con el motor |
| **Futuro motor de planilla (F2)** | Interna (futura) | Aplicará los eventuales del periodo con origen `MOTOR` sobre el mismo modelo — contrato estable (D-09) |
| **Correo / notificaciones** | Fase 2 | Aviso al autorizador (pendientes de decisión) y a RRHH (atrasados) |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de empresa | Catálogos editables; asignación de permisos | **No decide** ingresos salvo grant explícito `Authorize*` (la política excluye el fallback Admin en la decisión) |
| Gestor de RRHH / Analista de planilla | `ManageOneTimeIncomes` (+ lecturas implícitas) | No puede decidir lo que él mismo registró ni lo que solicitó (RN-03); ediciones solo `EN_REVISION`; re-imputación solo `AUTORIZADO`; aplica/revierte con auditoría |
| Autorizador | `AuthorizeOneTimeIncomes` | Anti-autoaprobación extendida (sujeto/registrador/solicitante); motivos obligatorios; revocaciones trazables |
| Consulta / Auditor | `ViewOneTimeIncomes` | Solo lectura de fichas, búsqueda avanzada, agrupaciones y exports |
| Finanzas / Planilla externa | `ViewOneTimeIncomes` (export insumo) | Solo lectura/exportación |
| Empleado (autogestión) | Sin acceso en F1 (D-14) | Lectura self = evolución aditiva si el negocio la pide |

---

## 15. Criterios de aceptación generales

1. **Ratificación previa**: ✅ cumplida (2026-07-06) — D-01…D-18 ratificadas y P-01…P-13 respondidas (§17) con todas las recomendaciones aceptadas; el plan técnico se deriva de este documento ratificado.
2. Reglas del cálculo por factores y del ciclo de vida como **módulo puro** con suite unitaria (casos dorados Anexo A.3) y test de paridad de localización.
3. Suite de integración completa (CRUD + flujo con anti-autoaprobación extendida, cálculo por factores con coherencia, re-imputación, aplicación unitaria/lote con **test de carrera**, exclusión/posposición, reversión de aplicación, gates de permisos incluida la exclusión de Admin en `Authorize*`, búsqueda avanzada con `StatusCounts` y agrupación cuadrando contra la búsqueda plana, insumo cuadrado contra pendientes, gancho de liquidación con reversión) **en verde junto con la suite existente**.
4. Migraciones `HasData` idempotentes con IDs verificados contra `GlobalCatalogSeedData` (bloque `-9900…-9909`; sin tocar reservas REQ-001/002/003/005; coordinación `payroll-types -9890…-9895` en backlog).
5. `openapi.yaml` actualizado **sin drift** (mantenido a mano vía skill); convenciones API respetadas (If-Match, `publicId`, enums string, errores bilingües).
6. Los componentes del cálculo quedan persistidos y el detalle siempre puede reconstruir el monto (auditoría del cálculo).
7. Búsqueda avanzada paginada con `StatusCounts`, totales por moneda y `groupBy` por dimensión; exportaciones xlsx/csv/json; **insumo de planilla por periodo** cuadrando contra la bandeja de pendientes del mismo filtro.
8. Integración con liquidación retrocompatible (contrato del motor intacto; línea sugerida editable; guard anti-duplicado; reversión simétrica verificada).
9. El ledger externo y la configuración de conceptos quedan **intactos** (test de no-escritura).
10. Guía de integración frontend publicada (`guia-integracion-frontend-planilla-ingresos-eventuales.md`) con contratos, estados, métodos de cálculo, flujo de aplicación por periodo, agrupación y modo degradado (sin maestro de periodos).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Expectativa de cálculo legal automático de horas extras** — **atenuado por la ratificación (P-03/P-04)**: el negocio confirmó que RRHH digita los valores en F1. Riesgo residual: usuarios finales que esperen automatización. Mitigación: mostrar el desglose persistido como valor de auditoría, comunicarlo en la guía FE/capacitación, y dejar el cálculo normativo perfilado para el motor/módulo de horas extras (F2, P-09).
- **Ambigüedad del flujo** — **resuelta por la ratificación (2026-07-06)**: P-01 confirmó la autorización de una decisión. Riesgo residual solo operativo (un paso más), mitigado por la bandeja de pendientes del autorizador.
- **Cuádruple vía de registro monetario** (concepto estructural / cíclico / eventual / off-payroll): riesgo de registrar el mismo pago en el lugar equivocado. Mitigación: tabla de fronteras (A.4), regla editorial en UI/guía FE y capacitación; validación blanda evaluable en F2.
- **Carga operativa de la aplicación manual**: si nadie aplica, la búsqueda y el insumo divergen de lo pagado. Mitigación: bandeja de **atrasados** como lista de trabajo, lote de un clic, export por periodo; el motor (F2) elimina la carga.
- **Coordinación de seeds compartidos**: `payroll-types` (`-9890…-9895`) lo puede sembrar REQ-001, REQ-004, REQ-005 o este módulo según el orden real de construcción. Mitigación: definición única documentada en los cuatro, verificación de IDs al abrir cada PR y coordinación en backlog (precedente de la colisión ya resuelta).
- **Multi-moneda sin FX** en agrupaciones: sumas por grupo podrían malinterpretarse si se mezclan monedas. Mitigación: toda suma va acompañada de su moneda (nunca un total mezclado), precedente off-payroll `/totals`.
- **Sensibilidad del dato de compensación**: fuga por permisos laxos. Mitigación: permisos dedicados, `Authorize*` sin Admin, sin autoservicio en F1, bandeja corporativa gateada en handler.

### Supuestos

- La nómina se procesa **externamente** durante toda la F1; el insumo exportado es el mecanismo de entrega (frontera P-01 de REQ-005, heredada).
- Tenant mono-país (SV); moneda predominante USD (default por empresa).
- Los ingresos eventuales los **registra RRHH** a partir de una solicitud del área (el solicitante) — no nacen en autoservicio (D-14).
- El catálogo de conceptos país ya contiene los tipos necesarios (`HORAS_EXTRA`, `COMISION`, `BONO`, `VIATICOS`, `AGUINALDO`, `OTRO_INGRESO` verificados como semilla).
- Quien aplica trabaja por periodo con el calendario de la empresa (maestro REQ-001 cuando exista).
- El registro de **jornadas** de horas extras (fechas/horarios con su autorización operativa) es un levantamiento aparte (P-09); este módulo registra el pago.

### Dependencias

- **Ratificación del negocio**: ✅ completada (2026-07-06, §17).
- **REQ-004/REQ-005**: definición compartida del `PAYROLL_TYPE_CATALOG` (seeds re-ubicados) — no bloquea: la siembra el primero que construya.
- **REQ-001**: maestro `PayrollPeriodDefinition` para las FK de periodo — no bloquea: degradación a etiqueta documentada (D-08).
- **REQ-005 (hermano)**: no bloquea, pero la **sinergia de construcción contigua** es fuerte (D-17): molde de flujo, bandejas, gancho de liquidación y catálogo compartido.
- **Liquidación**: mergeada (PR #56) — el gancho RF-012 no espera nada.
- Internas: verificación de IDs de seed libres al abrir el primer PR (bloque `-9900…-9909`); convenciones de catálogos/permisos vigentes.

---

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-06)

> **Todas las recomendaciones del análisis fueron aceptadas por el negocio.** P-09 con la precisión literal: «no es el módulo de horas extras; ese será otro módulo» (coincide con la recomendación).

| # | Pregunta (síntesis) | Respuesta del negocio → efecto en el diseño |
|---|---|---|
| **P-01** | **(Estructural)** ¿Autorización previa de un tercero antes de poder pagarse, o basta registrar? | **Aceptada la recomendación: autorización de una decisión** (`EN_REVISION → AUTORIZADO/RECHAZADO`, anti-autoaprobación, `Authorize*` sin Admin) → D-04/D-05/D-06 confirmadas; ciclo de **5 estados**; la alternativa sin autorización queda **descartada** |
| P-02 | ¿Quién es el solicitante, cómo se registra y participa en la anti-autoaprobación? | **Aceptada la recomendación**: expediente de la empresa + snapshot (trío del precedente liquidación/retiro), obligatorio, **participa en la anti-autoaprobación** (quien solicita no decide) → D-05/D-12 confirmadas |
| P-03 | ¿Bastan los dos métodos de cálculo (cantidad×valor[×factor] y %×base)? | **Aceptada la recomendación: bastan** → D-07 confirmada; esquemas complejos (tablas, tramos, fórmulas) pertenecen al motor (F2) |
| P-04 | ¿Calcular el valor-hora desde el salario y/o los recargos legales automáticamente? | **Aceptada la recomendación: NO en F1** — RRHH digita los valores con desglose auditado; el cálculo normativo exige el motor y su levantamiento propio (casos del contador, precedente liquidación) → frontera sin-motor confirmada |
| P-05 | ¿Fecha ≤ hoy? ¿Retroactividad y periodo destino futuro? | **Aceptada la recomendación**: fecha ≤ hoy; retroactividad sí (adopción por el flujo normal); periodo destino pasado (regularización) o futuro (programado) → RN-09 confirmada |
| P-06 | ¿Corrección de un aplicado por reversión con motivo o por contra-asiento? | **Aceptada la recomendación: reversión con motivo** (vuelve a `AUTORIZADO`, re-aplicable; historial visible); contra-asiento descartado → D-10/RF-007 confirmadas |
| P-07 | ¿Adjuntos en F1? | **Aceptada la recomendación: sin adjuntos en F1** (solicitante + referencia documentan); si el negocio exige respaldo después, se replica el patrón REQ-002 (purpose + contenedor + preferencia) como evolución aditiva — sin storage nuevo en el despliegue |
| P-08 | ¿Dimensiones de agrupación suficientes? ¿Multi-nivel? | **Aceptada la recomendación**: las 8 dimensiones fijas (estado/tipo/empleado/tipo de planilla/periodo/centro de costo/moneda/mes), una por consulta; multi-nivel = F2 → RF-009 confirmada |
| **P-09** | ¿Este módulo es «el módulo de horas extras» de la costura `overtimeRecordPublicId` de REQ-002? | **«No es el módulo de horas extras; ese será otro módulo»** → confirmado: este módulo registra el **pago**; el registro de jornadas (y su conexión con tiempo compensatorio y con este pago) será un módulo/levantamiento propio; la costura queda documentada sin construir |
| P-10 | ¿Autoservicio del empleado en F1? | **Aceptada la recomendación: sin autoservicio en F1** → D-14 confirmada; lectura self = evolución aditiva |
| P-11 | ¿Permisos dedicados o compartidos con los cíclicos? | **Aceptada la recomendación: dedicados** (`View`/`Manage`/`AuthorizeOneTimeIncomes`) → D-06 confirmada; si un mismo equipo opera ambos módulos, se le asignan ambas ternas |
| P-12 | ¿Umbral de monto con doble autorización? | **Aceptada la recomendación: F1 sin umbral** (multi-nivel = F2; si llega, preferencia por empresa) → sin cambios de diseño |
| P-13 | ¿Gancho de liquidación en F1 o segunda entrega? | **Aceptada la recomendación: F1** (prioridad Media, RF-012; recortable sin bloquear el resto) → D-15 confirmada |

---

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar P-01 antes que todo**: es la única decisión que cambia la forma del módulo (5 estados + permiso de autorización vs ciclo reducido). Todo lo demás del documento vale en ambos escenarios.
2. **Construir contiguo a REQ-005** (D-17): comparten el catálogo de tipos de planilla, el molde de flujo/decisión, las bandejas con insumo por periodo y el gancho de liquidación. Hacer REQ-005 → REQ-006 seguidos amortiza el andamiaje (y el plan técnico puede extraer una base común de «ingresos de planilla»); construirlos separados duplica esfuerzo.
3. **No calcular tarifas legales en F1** (P-04): la frontera «sin motor» está ratificada en cadena; el valor de esta fase es la **trazabilidad del cálculo digitado** (componentes persistidos + coherencia validada), no la automatización normativa. El motor y el módulo de jornadas de horas extras (P-09) son los levantamientos donde eso vive.
4. **El insumo por periodo es el corazón operativo** (como en el hermano): sin motor, la disciplina depende de que RRHH tenga la bandeja de pendientes/atrasados clara, el lote de un clic y el export que cuadra. Invertir ahí el esfuerzo de UX.
5. **Acotar la agrupación a dimensiones fijas** (RF-009/P-08): cubre el levantamiento sin construir un motor de reporting genérico; cada dimensión nueva después es aditiva y barata.
6. **Documentar las fronteras monetarias** (A.4, RN-16): con cuatro registros monetarios coexistiendo (estructural, cíclico, eventual, fuera de nómina), la guía FE y la capacitación deben dejar claro cuál usar cuándo — es el riesgo de adopción №1 del programa.
7. **Solicitante con el trío del precedente** (D-12): no inventar un modelo nuevo — `RequesterFilePublicId` + snapshot + auditoría ya existe, las bandejas ya lo muestran y habilita la anti-autoaprobación extendida sin costo.
8. **Seeds en el bloque `-9900…-9909`** (D-16): verificado libre hoy; re-verificar contra `GlobalCatalogSeedData` al abrir el PR (la colisión REQ-004/ayuda económica demostró que los tentativos envejecen) y registrar la reserva en el backlog para los REQ siguientes.
9. **MVP recortable si urge**: RF-001…RF-008 entregan las dos opciones del levantamiento (registro + búsqueda con detalle); la agrupación (RF-009), la bandeja de pendientes (RF-010) y el gancho de liquidación (RF-012) pueden ser segunda entrega — aunque el insumo por periodo (RF-011) se recomienda mantener en F1 porque es lo que elimina transcripciones.
10. **F2 ya perfilado**: motor aplica automáticamente (origen `MOTOR` sobre el mismo modelo), cálculo normativo de horas extras + registro de jornadas (costura REQ-002), multi-nivel por umbral (P-12), notificaciones, autoservicio de lectura y correlación con el ledger externo.

---

## Anexo A — Referencias y propuestas

### A.1 Posición en el programa de planilla (mapa heredado de REQ-005 A.1)

| Capacidad de la visión del programa | Cobertura |
|---|---|
| Registrar todo tipo de **ingresos** de los empleados | **REQ-005** (cíclicos con cuotas) + **este REQ-006** (eventuales de una vez) — juntos cubren la capa transaccional de ingresos |
| Registrar todo tipo de **descuentos** | Rebanada siguiente (descuentos cíclicos espejo — P-10 de REQ-005 ratificada: «entra después de los ingresos cíclicos») |
| Consultas de **nivel de endeudamiento** | Depende de descuentos cíclicos (rebanada siguiente) |
| **Creación de periodos de planilla** | REQ-001 (`PayrollPeriodDefinition`) — este módulo solo lo consume |
| **Generar la planilla** (cálculo del periodo) | Programa futuro con levantamiento propio (P-01 de REQ-005: «el motor se realizará aparte») |
| Consultar **transacciones no aplicadas** | REQ-005 (cuotas pendientes/vencidas) + **este REQ-006** (eventuales pendientes/atrasados) — aproximación F1; pool pleno = motor |
| **Aplicar al periodo activo / enviar a otro periodo** | Aplicación en lote con exclusión + **re-imputación del destino** (este módulo la agrega); semántica de «periodo activo» = motor |
| Búsqueda avanzada corporativa con agrupación y Excel | **Este REQ-006** (RF-008/009/011) |

### A.2 Seeds tentativos (verificar IDs libres contra `GlobalCatalogSeedData` al abrir el primer PR)

- **Ocupación verificada hoy (en código)**: piso general **`-9846`** (conceptos de liquidación `-9830…-9846`, con `HORAS_EXTRAS_PENDIENTES=-9837`); trampa vigente `-9490…-9496` (`ACTION_STATUS_CATALOG`); conceptos país `-9720…-9736` (`HORAS_EXTRA=-9721`, `COMISION=-9722`).
- **Reservas de planes (no en código)**: REQ-001 `-9850…-9862` + `-9485…-9489` · REQ-002 `-9865…-9871` · REQ-003 `-9875…-9879` · **REQ-005 `-9880…-9899`** (estados `-9880…-9885`, acciones `-9886/-9887`, concepto `-9888`, holgura `-9889`, **payroll-types `-9890…-9895`**, tipos cíclicos `-9896…-9899`).
- **Verificación de este análisis (2026-07-05, doble pasada)**: **nada en código ni en planes ocupa `-9900` o inferior** (grep de literales y de `InsertData` en migraciones sin coincidencias).
- **Propuesta de este módulo (bloque `-9900…-9909`)**:
  - `ONE_TIME_INCOME_STATUS_CATALOG`: `EN_REVISION=-9900`, `AUTORIZADO=-9901`, `RECHAZADO=-9902`, `APLICADO=-9903`, `ANULADO=-9904`.
  - Concepto de liquidación `INGRESO_EVENTUAL_PENDIENTE=-9905` (candidato — decisión del plan técnico en RF-012).
  - Holgura: `-9906…-9909`.
- `PAYROLL_TYPE_CATALOG` (`-9890…-9895`) es la definición compartida de REQ-004 — la siembra el primero que construya (coordinar en backlog).
- Sin ActionTypes nuevos (D-18: sin asientos de journal). Moneda/conceptos/centros de costo no se tocan.

### A.3 Casos dorados sugeridos para la validación del negocio

1. **Fijo e2e**: comisión $150 fija → autorizar por tercero → aplicar a la quincena → `APLICADO`; búsqueda la muestra con estado y detalle.
2. **Por factores (cantidad)**: 10 h × $2.50 × 1.5 → $37.50 calculado; componentes visibles; monto digitado que no cuadra → 422 con desglose.
3. **Por factores (porcentaje)**: 3 % × $10,000 → $300; el concepto `COMISION` preselecciona el método porcentaje.
4. **Anti-autoaprobación extendida**: el registrador, el empleado sujeto y el solicitante intentan decidir → 403 los tres.
5. **Enviar a otro periodo**: autorizado en quincena 13 re-imputado a la 14 → sale del insumo de la 13, entra al de la 14; excluirlo del lote de la 14 lo mantiene pendiente.
6. **Atrasado**: autorizado con periodo destino vencido sin aplicar → marcado atrasado en la bandeja.
7. **Carrera**: doble submit concurrente del mismo lote → cero dobles aplicaciones; segunda respuesta 409/422.
8. **Reversión**: aplicado a la quincena equivocada → revertir con motivo → `AUTORIZADO` con la aplicación anulada visible → re-aplicar al periodo correcto.
9. **Agrupación**: `groupBy=tipo` de un trimestre cuadra contra la búsqueda plana; sumas siempre por moneda.
10. **Liquidación**: autorizado de $150 pendiente → línea sugerida en el finiquito → al emitir queda `APLICADO` origen `LIQUIDACION` → anular la liquidación lo reabre a `AUTORIZADO`.
11. **Retirado**: alta sobre `RETIRADO` → 422; pendiente `EN_REVISION` de un retirado → solo rechazo/anulación.
12. **Insumo**: export del periodo cuadra exactamente contra la bandeja de pendientes del mismo filtro; excluye anulados y aplicados.

### A.4 Fronteras de los registros monetarios del sistema (guía editorial — RN-16)

| Registro | Qué es | Cuándo usarlo | Módulo |
|---|---|---|---|
| `PersonnelFileCompensationConcept` | **Configuración estructural** de compensación (salario, ley, beneficios fijos de la plaza; fijo o %) | Lo permanente que define la compensación del empleado/plaza | Plazas ingresos/egresos (mergeado) |
| `PersonnelFileRecurringIncome` (REQ-005) | Ingreso **adicional recurrente** con plan de cuotas, autorización e historial | Compromisos en cuotas (ayuda de alimentación pactada, bono en 6 cuotas…) | Planilla — ingresos cíclicos (plan escrito) |
| **`PersonnelFileOneTimeIncome` (este REQ-006)** | Ingreso **puntual de una sola vez** con valor fijo o por factores, imputado a una planilla/periodo | Horas extras del mes, una comisión, un bono único | Planilla — ingresos eventuales (este análisis) |
| `PersonnelFileOffPayrollTransaction` | Movimiento monetario **fuera de nómina** (gasto no-salarial: herramientas, EPP, regalos…) | Lo que la empresa gasta en el empleado sin pasar por planilla | Transacciones fuera de nómina (mergeado) |
| `PersonnelFilePayrollTransaction` | **Ledger inmutable** de sincronización con la nómina externa | Solo escritura por integración; nunca registro operativo | Compensación (mergeado — trampa documentada) |
| Línea de liquidación (`PersonnelFileSettlementLine`) | Componente del **finiquito** (calculado o manual) | Solo dentro de una liquidación | Liquidación (mergeado, PR #56) |
