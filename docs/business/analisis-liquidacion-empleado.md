# Análisis de Negocio — Liquidación de Personal (Nueva liquidación · Escenario de liquidación)

| | |
|---|---|
| **Tipo de documento** | Análisis de requerimiento (validación + GAP) y especificación funcional |
| **Módulo** | Expediente de Personal → Retiros → **Liquidación** (`PersonnelFiles`) |
| **Fecha** | 2026-07-04 (v1.0 propuesta) · **2026-07-04 (v1.1 ratificada)** |
| **Autor** | Analista de Negocio (CLARIHR), asistido por auditoría de código de 3 agentes |
| **Estado** | **Ratificado (v1.1) — 2026-07-04.** Decisiones **D-01…D-20 ratificadas por el negocio** (lo no modificado quedó según la recomendación del analista). Ajustes sobre la propuesta v1.0: **D-06 endurecida** (solicitante **solo RRHH**), **D-08 ampliada** (el cálculo cubre el "todo" legal: + bono, comisión, horas extras pendientes, descuento externo —última cuota—; salida en 5 secciones: ingresos, descuentos, **pagos patronales**, **reserva/provisión** y **resumen**; conceptos legalmente no cumplidos → **valor 0**), **D-09 precisada** (el "salario máximo" lo calcula el sistema desde el salario del empleado topado), **D-10 CAMBIADA** (**liquidación POR PLAZA**, no consolidada), **D-13 re-especificada** (pagos patronales = ISSS/AFP/**INCAF** —ex-INSAFORP—; **reserva = provisión contable** del dinero de la liquidación en el **centro de costo**), **D-19 ampliada** (la **boleta PDF pasa a Fase 1**; mandato de **reutilización** de abstracciones), **RF-011 re-anclada** (el salario mínimo vive **en la ficha del empleado**), **RN-10 endurecida** (el sistema **controla el exceso gravable**) y **Renta = tabla oficial vigente 2026**. Preguntas abiertas 1–16 resueltas (§17) y preguntas derivadas **P-01…P-03 resueltas (2026-07-04)**: antigüedad por plaza desde el `StartDate` de la asignación (P-01), parámetros contables finos según la recomendación del analista (P-02) y conceptos sin plaza → plaza principal (P-03). Plan técnico: [`docs/technical/plan-tecnico-liquidacion-empleado.md`](../technical/plan-tecnico-liquidacion-empleado.md). |
| **Naturaleza del módulo** | **Desarrollo NET-NEW puro** (cero código de liquidación en `src/`), pero **es la pieza que dos módulos ya declararon esperar**: la reversión de retiro difirió "revertir la liquidación" a este módulo (D-14 de retiro definitivo) y la recontratación usa una **confirmación manual** de cierre del periodo anterior "hasta que exista un módulo de nómina o de baja de personal" (D-06/D-17 de recontratación, `RehireEligibilityRules.cs:41`). Todos los insumos del cálculo **ya existen**: retiro ejecutado con snapshot, salario base por plaza, tasas ISSS/AFP con topes, tramos de Renta y antigüedad. |
| **Documentos hermanos** | `analisis-retiro-definitivo-empleado.md` (D-01…D-19 ratificadas — fuente del retirado), `analisis-plazas-ingresos-egresos.md` (D-01…D-20 — configuración de compensación; su D-03 difirió el *cálculo* a un módulo futuro), `analisis-recontratacion-empleados.md` (señal de cierre), `analisis-consulta-solicitudes-constancia.md` (plantilla bandeja + export Excel) |

---

## Contexto del cambio (requerimiento original)

> Esta opción deber realizar el cálculo de liquidación de un empleado que se ha retirado, por cualquier motivo.
>
> **Nueva liquidación:** La información más importante que se deberá ingresar para generar la liquidación es la siguiente: Empleado, categoría de motivo de la liquidación (catálogo), Motivo (catálogo), solicitante de la liquidación, fecha de solicitud, observación, detalle de los pagos a incluir en la liquidación: salario, vacación proporcional, aguinaldo proporcional, indemnización, renuncia voluntaria, salario mínimo, valor de salario máximo.
>
> El programa deberá realizar el cálculo de ingresos, descuentos y las reservas con base a información ingresada, además deberá permitir realizar modificaciones en la información ingresada, eliminar la información que no aplica.
>
> La información podrá ser exportada a Excel.
>
> **Escenario de liquidación:** esta opción debe permitir crear un escenario de liquidación de un empleado, para visualizar los cálculos de los ingresos, descuentos y reservas con base a la fecha de retiro estimada, sin aplicar ningún cálculo a la planilla. El ingreso de información será similar a la opción de nueva liquidación y también deberá permitir realizar modificaciones en la información ingresada o eliminar la información que no aplica, además exportar información a Excel.

---

## 0. Veredicto ejecutivo (resultado de la validación)

**El requerimiento NO está implementado. Es desarrollo nuevo (net-new).** Búsqueda exhaustiva en `src/` (2026-07-04): **cero** resultados para `liquidacion`, `Settlement`, `finiquito`, `severance` en código; las únicas menciones viven en documentación previa que lo **difiere explícitamente a este módulo**.

Tres hallazgos gobiernan el diseño:

1. **El sistema ya tiene casi todos los insumos del cálculo.** El retiro definitivo (PR #55, mergeado) deja una solicitud `EJECUTADA` con fecha efectiva, categoría/motivo (catálogos con `SeparationType`), solicitante y —clave— los **registros de cierre** (`RetirementRequestClosedRecord`: qué plazas y contratos cerró la baja). La compensación por plaza aporta el **salario base negociado** (`SALARIO_BASE` por asignación), las **tasas y topes de ISSS/AFP** (3%/7.5% tope $1,000; 7.25%/8.75% tope $7,045.06 — sembrados SV) y los **tramos de Renta** por periodo de pago. El perfil aporta `HireDate` para la antigüedad. **Lo que falta es el motor que los combine** — exactamente lo que la D-03 de plazas ingresos/egresos difirió.
2. **Lo que NO existe y el requerimiento asume:** un registro de **salario mínimo** (solo existe `MinContributionBase` = 365.00 como IBC mínimo de ISSS/AFP), **saldos de vacaciones** (el enricher devuelve `null` — módulo futuro) y cualquier noción de **escenario/simulación**. El requerimiento pide "salario mínimo" y "valor de salario máximo" como **entradas**: se interpretan como los **parámetros de topes legales** (indemnización topada a 4× el salario mínimo — Art. 58 CT; prestación por renuncia topada a 2× — LRPERV), interpretación **confirmada en la ratificación** (§17.2: el "salario máximo" lo calcula el sistema desde el salario del empleado topado; el mínimo vive en la **ficha del empleado**).
3. **La planilla es externa.** No hay motor de nómina interno; `PersonnelFilePayrollTransaction` es una bitácora **inmutable sincronizada desde fuera**. Por tanto, ni la liquidación real ni el escenario "aplican a la planilla": el módulo **calcula y documenta**; el pago material y su contabilización ocurren fuera del sistema (gestión de expectativas — R-04). El texto del requerimiento ("sin aplicar ningún cálculo a la planilla") es naturalmente compatible con esta realidad.

**Consecuencia de diseño:** una sola entidad de liquidación con **dos modos** (real y escenario) sobre **un mismo motor de cálculo parametrizable** (pure rules module), anclando la liquidación real a la solicitud de retiro `EJECUTADA` y reutilizando catálogos de motivo, bandeja y exportación existentes. *(v1.1: la ratificación fijó la granularidad en **una liquidación por plaza** — cada asignación cerrada por el retiro se liquida por separado, con su salario, sus conceptos y su centro de costo.)*

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Estado | Evidencia |
|---|---|---|
| **Retiro definitivo** — solicitud con ciclo `SOLICITADA → AUTORIZADA → EJECUTADA → REVERTIDA` (+`RECHAZADA`/`ANULADA`), snapshots de solicitante/categoría/motivo, ejecución orquestada y reversión con ventana de 30 días | ✅ Mergeado (PR #55). La `EJECUTADA` es la **fuente canónica del retirado**: fecha, motivo y **`ClosedRecords`** (plazas/contratos cerrados por la baja, con `PreviousEndDate`) — insumo exacto para valorizar. Puerta única: no hay bajas fuera del módulo (datos legados eliminados, D-08 de retiro). | `PersonnelFileEmployee.cs:2401-2732` (entidad + `RetirementRequestClosedRecord`); estados `:2377`; endpoints `RetirementRequestsController.cs:31-157`, reversión `RetirementRequestReversalController.cs:27`; ventana `RetirementRequest.Rules.cs:95` |
| **Catálogos jerárquicos de motivo** (`RetirementCategory` → `RetirementReason`, país-scoped, categoría con `SeparationType` Voluntaria/Involuntaria/Otra) | ✅ Sembrados SV: **8 categorías** (`VOLUNTARIA`, `JUBILACION`, `INVOLUNTARIA`, `ABANDONO`, `NO_SUPERA_PERIODO_PRUEBA`, `FIN_CONTRATO`, `MUTUO_ACUERDO`, `FALLECIMIENTO`; ids -9200…-9207) / **23 motivos** (-9220…-9242). Son exactamente la "categoría de motivo de la liquidación (catálogo), Motivo (catálogo)" del requerimiento. | `RetirementCatalogItems.cs:20,58`; seeds `20260625033455_AddRetirementReasonCatalogs.cs:96-130`; `GET /api/v1/reference-catalogs/retirement-categories` y `retirement-reasons?parentCode=` |
| **Salario real por plaza** (3 niveles: banda del tabulador → presupuestado de la plaza → **negociado**) | ✅ El negociado es la fuente de verdad: concepto `SALARIO_BASE` (`IsBaseSalary`) por asignación (`AssignedPositionPublicId`), único activo por plaza y validado contra la banda. Multi-plaza soportado (`IsPrimary` + secundarias). | `PersonnelFileCompensation.cs:12` (instancia), `CompensationConceptTypeCatalogItem.cs:74`; regla `CompensationConcepts.Rules.cs:73`; asignaciones `PersonnelFileEmployee.cs:118-299` (`Close`/`Reopen`) |
| **Tasas y topes ISSS/AFP** (empleado + patronal + tope IBC + IBC mínimo) | ✅ Sembrados SV en el catálogo de tipos y copiados a cada instancia: **ISSS** 3.00 / 7.50, tope 1,000.00, IBC mín 365.00 (id -9727); **AFP** 7.25 / 8.75, tope 7,045.06 (id -9728). Editables por país/instancia. | `GlobalCatalogSeedData.cs:933-934`; campos `DefaultEmployeeRate`/`DefaultEmployerRate`/`ContributionCap`/`MinContributionBase` (`CompensationConceptTypeCatalogItem.cs`) |
| **Tramos de Renta** (retención por periodo de pago: límites, cuota fija, % sobre exceso) | ✅ Entidad y administración listas (`GET/PUT api/v1/income-tax-brackets`), **per-tenant**. ⚠️ Solo hay **seed de desarrollo** (4 tramos MENSUAL 2024); producción exige configurarlos (R-06). | `IncomeTaxWithholdingBracket.cs:11`; `DevSeedService.cs:262`; `IncomeTaxBracketsController.cs:24,42` |
| **Antigüedad y periodos** | ✅ `HireDate` en el perfil (ancla de antigüedad; la recontratación lo **sobrescribe** al abrir nuevo periodo); línea de tiempo de periodos **derivada** de `ContractHistory` (sin entidad propia). | `PersonnelFileEmployee.cs:41`; `PersonnelFileDashboard.Rules.cs:36-50`; `EmploymentPeriodsTimeline.cs` |
| **Bandeja + exportación** (query con filtros, contadores, export xlsx/csv/json con auditoría y límite 413) | ✅ Infraestructura propia (SIN librería externa de Excel: OOXML manual sobre `ZipArchive`), reutilizada por ~10 módulos, incluida la bandeja de retiros. | `ReportExportFileWriter.cs:78`; `ReportExportDeliveryService.cs:49-110`; patrón `CertificateRequestsReportingController.cs:30,66` |
| **PDF (QuestPDF Community)** | ✅ Motor de documentos configurable (QuestPDF/Gotenberg) usado por constancias y perfiles de puesto — base para una futura *boleta de liquidación* firmable (Fase 2). | `DependencyInjection.cs:205`; `QuestPdfDocumentRenderer.cs:14` |
| **Permisos y gates** | ✅ Pares `View*`/`Manage*` por feature (códigos string aprovisionados por tenant), gates por handler fail-closed (`IPersonnelFileAuthorizationService`), precedente de datos salariales: `PersonnelFiles.ViewCompensation`. | `PersonnelFileCommon.cs:98,205-226`; `ProvisioningConstants.cs:71,87-90`; `PersonnelFileAuthorizationService.cs:27` |
| **Preferencias de empresa** (fila tipada por tenant, columnas + setter dedicado) | ✅ Patrón para defaults del módulo (p. ej. salario mínimo mensual por defecto). | `CompanyPreference.cs:5,34,74` |
| **Bitácora de planilla externa** (`PersonnelFilePayrollTransaction`) | ✅ Existe y **NO se toca**: ledger inmutable sincronizado desde el sistema de nómina externo (POST de sync + PATCH solo-isActive). Trampa conocida: no confundir con este módulo. | `PersonnelFileEmployee.cs:630-692`; `PersonnelFileCompensationController.cs:370-536` |

### Lo que NO existe (verificado exhaustivamente)

| Concepto | Resultado de la búsqueda |
|---|---|
| **Liquidación / finiquito / settlement / severance** (entidad, endpoint, migración, cálculo) | ❌ Cero resultados en `src/`. Solo menciones en docs que lo difieren a este módulo (`analisis-retiro-definitivo-empleado.md` D-14/G-09; `analisis-recontratacion-empleados.md:613`). |
| **Motor de cálculo de nómina/prestaciones** | ❌ No existe. La D-03 de plazas ingresos/egresos lo declaró explícitamente futuro; este módulo construye el **primer** motor de cálculo del sistema (acotado a liquidaciones). |
| **Salario mínimo** (registro legal por vigencia/sector) | ❌ No existe entidad ni preferencia. Lo más cercano: `MinContributionBase` = 365.00 en los tipos ISSS/AFP, cuyo comentario dice "SV: the current minimum wage" (`CompensationConceptTypeCatalogItem.cs:83`). |
| **Saldos/acumulados de vacaciones o aguinaldo** | ❌ `VacationDaysAvailable` se devuelve **null** por diseño ("owned by the future vacations module", `EmployeeProfiles.cs:23-24`, repo `:2226`). El aguinaldo existe solo como tipo de concepto de ingreso recurrente (-9725), sin devengo. |
| **Escenarios / simulaciones** de cualquier tipo | ❌ No existe el concepto en ningún módulo. |
| **Exportación individual formateada** (documento por registro con encabezado + detalle + totales) | ❌ La infraestructura exporta **listados planos** (filas homogéneas por reflexión del DTO); el export individual de una liquidación es una extensión nueva (G-08). |

---

## Brechas identificadas (GAP → resolución propuesta)

| # | Brecha verificada | Resolución propuesta |
|---|---|---|
| **G-01** | No existe entidad ni ciclo de **liquidación**. | Nueva entidad `PersonnelFileSettlement` (encabezado) + `PersonnelFileSettlementLine` (detalle), ciclo `BORRADOR → EMITIDA → ANULADA` (D-01, D-15). |
| **G-02** | No existe **motor de cálculo** (ingresos/descuentos/reservas). | Motor determinista server-side como **módulo de reglas puro** con fórmulas SV parametrizadas y snapshot de parámetros (D-08…D-13; RF-008…RF-011). |
| **G-03** | No hay registro de **salario mínimo** ni de topes legales. | Parámetros **por liquidación** (digitados con defaults sugeridos) + topes calculados con override; default inicial desde preferencia de empresa (D-09). |
| **G-04** | No hay **saldos de vacaciones** (goce no registrado). | Días de vacación **editables** con default proporcional al último aniversario; el módulo futuro de vacaciones refinará el default (D-08, R-02). |
| **G-05** | "**Reservas**" sin contraparte en el sistema y con significado ambiguo. | **Resuelto en ratificación (D-13):** dos bloques — **pagos patronales** (ISSS 7.5%, AFP 8.75%, **INCAF** 1%) y **reserva = provisión contable** del dinero de la liquidación en el **centro de costo** de la plaza. |
| **G-06** | La **reversión de retiro** ignora liquidaciones (integración declarada futura en D-14 de retiro). | Cierre del ciclo: reversión **anula** borradores automáticamente y se **bloquea** ante una `EMITIDA` vigente (D-17; RF-017). |
| **G-07** | No existe **escenario/simulación**. | Mismo modelo y motor con `Kind = ESCENARIO`: sin efectos, sin ciclo, siempre editable, export marcado SIMULACIÓN (D-02, D-05). |
| **G-08** | Export existente = **listados planos**; falta export individual con encabezado + detalle + totales. | Export individual de liquidación (xlsx/csv/json) **+ boleta PDF estándar en Fase 1** (ratificado), construidos sobre las abstracciones existentes (`ReportExportDeliveryService`, pipeline `IDocumentModelRenderer`/QuestPDF) y, donde falte pieza, como **abstracción reutilizable** para futuros módulos (D-19). |
| **G-09** | Tramos de **Renta** solo con seed de desarrollo; en producción pueden no estar configurados. | Línea de Renta **sugerida** cuando hay tramos vigentes, en 0 con advertencia cuando no, y **siempre con override** (D-12, R-06). |
| **G-10** | La **recontratación** exige confirmación manual de cierre del periodo anterior. | Sinergia Fase 2: liquidación `EMITIDA` del periodo como señal automática de cierre, reemplazando la confirmación manual (D-18). |

---

## Decisiones ratificadas por el negocio (2026-07-04) — D-01…D-20

| # | Pregunta | Resolución ratificada (2026-07-04) |
|---|---|---|
| **D-01** | ¿Naturaleza y ubicación del módulo? | Net-new dentro de `PersonnelFiles`: `PersonnelFileSettlement` + `PersonnelFileSettlementLine`. **No** se toca `PersonnelFilePayrollTransaction` (ledger externo inmutable) ni `PersonnelFileOffPayrollTransaction` (otro concepto). |
| **D-02** | ¿Liquidación y escenario son entidades distintas? | **Una entidad, dos modos** (`Kind = LIQUIDACION | ESCENARIO`): mismo formulario, mismo motor, mismo export. El escenario **jamás** produce efectos (sin journal, sin candados de unicidad, sin ciclo de estados). |
| **D-03** | ¿Qué habilita una **liquidación real**? | Referencia obligatoria a una solicitud de retiro **`EJECUTADA`** del empleado. De ella se **heredan en solo lectura**: fecha de retiro, categoría/motivo (con snapshots) y las plazas/contratos cerrados (`ClosedRecords`) a valorizar. Cubre "por cualquier motivo" (los 8/23 códigos). No hay retirados fuera del módulo (puerta única + limpieza de legados ya ratificadas en retiro). |
| **D-04** | ¿Catálogo propio de "categoría/motivo de liquidación"? | **No: se reutilizan** `RetirementCategory`/`RetirementReason` existentes. Crear un catálogo paralelo duplicaría datos y rompería la trazabilidad retiro→liquidación. En escenario los códigos son seleccionables (hipótesis); en real, heredados. |
| **D-05** | ¿Elegibilidad y datos del **escenario**? | Empleado `Employee`+`Completed` **activo** (sin retiro vigente); **fecha de retiro estimada** editable (≥ `HireDate`; típicamente futura); categoría/motivo hipotéticos del catálogo. Escenarios **ilimitados** por empleado, editables siempre, borrado lógico permitido. |
| **D-06** | ¿Qué es el "**solicitante de la liquidación**"? | **Endurecida en la ratificación: el solicitante SOLO puede ser RRHH.** Por defecto, el propio gestor que registra (usuario con `ManageSettlements`), referenciado a su expediente + snapshot de nombre; si se selecciona a otra persona, debe pertenecer al área funcional de RRHH (validación con `HrFunctionalAreaCode` de la preferencia de empresa cuando esté configurada). Nunca el empleado sujeto ni su jefatura. `RequestedByUserId` se audita siempre. |
| **D-07** | ¿Cómo se modelan los **conceptos** del detalle? | Nuevo catálogo tipado país-scoped `settlement-concepts` con **clase** (`INGRESO`/`DESCUENTO`/`PAGO_PATRONAL`), **matriz de afectación** (afecta ISSS / AFP / Renta — para ingresos), **regla de exención** (RN-009.4) y marca de **calculado por el motor** vs **manual**. **Seed SV ratificado (17):** ingresos `SALARIO`, `VACACION_PROPORCIONAL`, `AGUINALDO_PROPORCIONAL`, `INDEMNIZACION`, `RENUNCIA_VOLUNTARIA`, `BONO_PENDIENTE`, `COMISION_PENDIENTE`, `HORAS_EXTRAS_PENDIENTES`, `OTRO_INGRESO`; descuentos `ISSS`, `AFP`, `RENTA`, `DESCUENTO_EXTERNO`, `OTRO_DESCUENTO`; pagos patronales `ISSS_PATRONAL`, `AFP_PATRONAL`, `INCAF` (D-08 ampliada; la clase `RESERVA` pasa a llamarse `PAGO_PATRONAL` — la reserva/provisión es un bloque calculado, no líneas de concepto, D-13). |
| **D-08** | ¿Cómo funciona el **motor**? | **Ratificada y AMPLIADA:** cálculo **server-side determinista** (módulo de reglas puro, patrón `RetirementRequest.Rules`): al crear, el sistema **sugiere** las líneas aplicables según el `SeparationType`/motivo (involuntaria → indemnización; voluntaria → prestación por renuncia — nunca ambas por defecto) y **recalcula todo en cada guardado**. El negocio ratificó que el cálculo debe cubrir **"el todo" según la ley de El Salvador**: vacación, aguinaldo, compensación económica por renuncia voluntaria o despido sin justa causa, **salario pendiente, bono pendiente, comisión pendiente, horas extras pendientes, otros ingresos adicionales**; descuentos de ley **ISSS, AFP, Renta** y **descuento externo (la última cuota a generar para pago)**; más la sección de **pagos patronales** y una sección de **resumen** del cálculo (RF-008…RF-010). **Conceptos legalmente no cumplidos → valor 0** (p. ej. renuncia con < 2 años de servicio queda registrada con monto 0 y el motivo visible — ya no mera advertencia). |
| **D-09** | ¿De dónde salen **salario mínimo** y "**valor de salario máximo**"? | **Ratificada (confirmados los topes legales) y precisada:** el **salario mínimo** vive en la **ficha del empleado** (nuevo campo — RF-011) y de ahí lo toma el cálculo (snapshot + override auditado en la liquidación). El **"valor de salario máximo" lo calcula el sistema a partir del salario del empleado**: salario aplicable por concepto = `min(salario de la plaza, tope legal)` — indemnización: `4 ×` mínimo (Art. 58 CT), renuncia voluntaria: `2 ×` mínimo (LRPERV) — multiplicadores parametrizados, montos resultantes visibles con override. Todos los parámetros quedan en **snapshot inmutable** dentro de la liquidación (cambian año a año). |
| **D-10** | ¿Cuál es la **base salarial** con múltiples plazas? | **CAMBIADA en la ratificación: LIQUIDACIÓN POR PLAZA.** Cada liquidación valoriza **una** asignación: su `SALARIO_BASE`, sus conceptos de ingreso/deducción propios y su **centro de costo** (destino de la reserva D-13). En la real, la plaza se elige entre las **cerradas por ese retiro** (`ClosedRecords`); en el escenario, entre las **activas**. Un empleado multi-plaza genera una liquidación por cada plaza (unicidad D-16 ajustada). Los conceptos a nivel empleado (sin plaza asignada) se sugieren en la liquidación de la **plaza principal** (P-03). Sin `SALARIO_BASE` configurado en la plaza → 422 explicativo (no se inventan bases). |
| **D-11** | ¿Cómo se calcula la **antigüedad**? | Del **periodo laboral vigente**: `HireDate` → fecha de retiro (la recontratación ya sobrescribe `HireDate` al abrir nuevo periodo, y el periodo anterior se liquidó en su momento). Años completos + fracción en días/365 para prestaciones proporcionales. Periodos anteriores NO se acumulan (§17.9). |
| **D-12** | ¿Cómo se calculan los **descuentos**? | **ISSS/AFP**: tasas y topes **efectivos del empleado** (instancias de conceptos de compensación; fallback a defaults del catálogo de tipos) aplicados solo a la porción afecta según la matriz D-07. **Renta**: sugerida con la **tabla oficial vigente 2026** del periodo `MENSUAL` sobre la base gravable (incluido el **exceso gravable** que el sistema controla — RN-10), **siempre con override**; sin tramos configurados → línea en 0 + advertencia (checklist de despliegue: cargar la tabla 2026). **Descuento externo**: la **última cuota a generar para pago** de cada deducción externa activa del empleado (`DeductionClass.Externo` — préstamos, embargos, con contraparte), sugerida con su valor y editable/excluible. Otros descuentos manuales (`OTRO_DESCUENTO`) digitables. |
| **D-13** | ¿Qué son las "**reservas**"? | **Re-especificada en la ratificación — dos bloques distintos:** (a) **Pagos patronales** = `ISSS_PATRONAL` (7.5%), `AFP_PATRONAL` (8.75%) e **`INCAF`** (**ex-INSAFORP** — renombrado por ley; 1%) sobre las bases afectas — costo del empleador que no altera el neto; la **Renta retenida** figura en esta sección como **remisión informativa** (retención del empleado a enterar al fisco, no costo adicional — interpretación P-02). (b) **Reserva = provisión contable**: "reservar el dinero de la liquidación dentro del **centro de costo**" — monto total a provisionar = `Total Ingresos + Total Pagos Patronales`, cargado al centro de costo de la plaza liquidada (fórmula a validar con los casos dorados — P-02). |
| **D-14** | ¿Qué **ediciones** permite el detalle? | Por línea: **excluir/eliminar** (regenerable desde el catálogo), **ajustar** insumos (días, base) o **fijar monto manual** (override) con **nota obligatoria**; se conservan visibles el monto calculado Y el monto final (transparencia). Líneas manuales adicionales permitidas. Totales **siempre** recalculados server-side — el cliente nunca manda totales. |
| **D-15** | ¿**Ciclo de estados** de la liquidación real? | `BORRADOR → EMITIDA → ANULADA` (anulable también desde borrador; motivo obligatorio al anular una emitida). Solo `BORRADOR` es editable; `EMITIDA` es **inmutable** (corregir = anular + nueva). Catálogo país-scoped `settlement-statuses` (patrón híbrido canónico+catálogo). Al **emitir** se journalea la acción de personal `LIQUIDACION` (nuevo seed en `ActionType`). El escenario no tiene ciclo. **Sin flujo de aprobación multinivel en Fase 1** (patrón de la casa; §17.10). |
| **D-16** | ¿**Unicidad**? | Ajustada a D-10: a lo sumo **una liquidación real no-ANULADA por (solicitud de retiro × plaza)** (índice único filtrado, patrón retiro). Escenarios ilimitados. |
| **D-17** | ¿Qué pasa al **revertir un retiro** con liquidación? | **Ratificada** (cierre del punto D-14 de retiro), ajustada a por-plaza: la reversión **anula automáticamente todas** las liquidaciones en `BORRADOR` de ese retiro (motivo automático "reversión de retiro"); si **alguna** `EMITIDA` vigente existe, la reversión se **bloquea** (422) hasta anularla manualmente — el dinero eventualmente pagado se gestiona administrativamente fuera. |
| **D-18** | ¿Sinergia con **recontratación**? | **Fase 2**: cuando el periodo anterior tenga liquidación `EMITIDA`, el rehire deriva automáticamente la señal de cierre en lugar de la confirmación manual (`PriorPeriodClosureConfirmed`) — reemplazo ya previsto por D-17 de recontratación. Fase 1: sin cambios al rehire. |
| **D-19** | ¿**Exportación**? | **Ratificada AMPLIADA:** Fase 1 incluye **(a)** export **individual** de la liquidación/escenario (encabezado + detalle por sección + resumen; xlsx/csv/json — layout estándar, sin plantilla institucional), **(b)** export de **bandeja** con filtros y **(c)** la **boleta PDF pasa a Fase 1** (layout estándar; la plantilla con firmas queda Fase 2). El export de escenario va **marcado "SIMULACIÓN"**. **Mandato de reutilización (ratificado):** usar los servicios/abstracciones existentes (`ReportExportDeliveryService` para tabulares; pipeline `IDocumentModelRenderer`/QuestPDF para el PDF); si falta una pieza (export de documento individual), se construye como **abstracción reutilizable** para futuros desarrollos similares, no ad-hoc. |
| **D-20** | ¿**Permisos** y acceso? | Par dedicado: `PersonnelFiles.ViewSettlements` (bandeja/detalle/export) y `PersonnelFiles.ManageSettlements` (crear/editar/emitir/anular; incluye escenarios). Gates por handler (patrón fail-closed). Ver una liquidación expone salarios: el permiso dedicado ES la puerta de ese dato (rol RRHH debe otorgarse junto a `ViewCompensation` por coherencia). **Anti-auto-gestión**: el empleado sujeto no crea ni emite su propia liquidación (patrón anti-auto-aprobación). Sin autoservicio del empleado en Fase 1 (§17.13). |

---

## 1. Resumen del producto o requerimiento

Se construirá el módulo de **Liquidación de Personal**, que calcula la liquidación (finiquito) de un empleado retirado **por cualquier motivo** y permite simularla anticipadamente:

- **Nueva liquidación (por plaza):** a partir del **retiro ejecutado** de un empleado (fecha, categoría y motivo ya registrados por el módulo de retiro), RRHH elige la **plaza a liquidar** (de las cerradas por la baja) y registra solicitante (siempre RRHH), fecha de solicitud y observación; el salario mínimo se toma de la **ficha del empleado** y de él derivan los topes legales. El sistema **genera y calcula** el detalle en cinco secciones: **ingresos** (salario pendiente, vacación proporcional, aguinaldo proporcional, indemnización o prestación por renuncia voluntaria, bono/comisión/horas extras pendientes, otros), **descuentos** (ISSS, AFP, Renta con control de exceso gravable, descuento externo —última cuota— y manuales), **pagos patronales** (ISSS 7.5%, AFP 8.75%, INCAF 1%), **reserva** (provisión contable en el centro de costo de la plaza) y **resumen**. Permite **modificar, excluir u override** de cualquier línea con recálculo inmediato, emite la liquidación como documento inmutable y la **exporta a Excel y boleta PDF**.
- **Escenario de liquidación:** la misma captura y el mismo motor sobre una **plaza activa** de un empleado activo con **fecha de retiro estimada**, para visualizar los cálculos **sin ningún efecto** en el sistema ni en la planilla (que además es externa a CLARIHR); editable, eliminable y exportable (marcado SIMULACIÓN).

**Problema que resuelve.** Hoy la liquidación se calcula fuera del sistema (hojas de cálculo del contador), sin trazabilidad, sin vínculo con el retiro registrado, sin parámetros auditables y sin posibilidad de simular el costo de una salida antes de decidirla. Además, dos módulos ya construidos dependen de esta pieza: la reversión de retiro declaró la liquidación como integración pendiente (D-14 de retiro) y la recontratación usa una confirmación manual de cierre "hasta que exista" este módulo.

**Objetivo principal.** Que toda salida de personal tenga su cálculo de liquidación **dentro** del sistema: derivado de datos reales (salario por plaza, antigüedad, tasas y topes vigentes), transparente (monto calculado vs ajustado, parámetros en snapshot), auditable, exportable y simulable.

## 2. Objetivos del negocio

- **O-1. Exactitud y consistencia del cálculo:** una sola fuente de fórmulas parametrizadas (vacación 15+30%, aguinaldo 15/19/21 días, indemnización topada a 4× mínimo, renuncia voluntaria topada a 2×) alimentada por los datos reales del expediente — se eliminan las hojas de cálculo paralelas y sus discrepancias.
- **O-2. Trazabilidad y auditoría:** cada liquidación queda anclada al retiro que la origina, con solicitante, parámetros legales en snapshot, monto calculado vs ajustado por línea y journal de emisión — defendible ante una inspección laboral o auditoría.
- **O-3. Planificación financiera:** el escenario permite conocer el costo de una salida **antes** de ejecutarla (presupuesto de reestructuraciones, negociación de mutuos acuerdos) sin tocar datos reales.
- **O-4. Cierre del ciclo de baja:** completa la cadena retiro → entrevista → **liquidación** → (reversión/recontratación), resolviendo las integraciones que retiro (D-14) y recontratación (D-17) dejaron declaradas.
- **O-5. Control del gasto patronal y contable:** los pagos patronales (ISSS/AFP/INCAF) hacen visible el costo del empleador y la **reserva (provisión contable)** deja el dinero de la liquidación apartado en el **centro de costo** de la plaza — no solo el neto del empleado.

## 3. Alcance funcional

### Fase 1 — MVP

- **F1.** **Nueva liquidación por plaza** anclada al retiro `EJECUTADA`: elección de la plaza (de las cerradas por la baja) + encabezado (solicitante RRHH + snapshot, fecha de solicitud, observación; fecha/categoría/motivo heredados en solo lectura) — RF-001.
- **F2.** **Detalle de conceptos** generado por el motor según el motivo (`SeparationType`) — incluidos bono/comisión/horas extras pendientes y descuento externo (última cuota) sugeridos desde la configuración de compensación de la plaza — con inclusión/exclusión, líneas manuales y eliminación de lo que no aplica — RF-002.
- **F3.** **Motor de cálculo** en 5 secciones: ingresos (fórmulas SV parametrizadas + manuales), descuentos (ISSS/AFP con tasas y topes efectivos; Renta tabla 2026 con control de **exceso gravable**; descuento externo; manuales), pagos patronales (ISSS/AFP/INCAF), reserva (provisión contable por centro de costo) y resumen — RF-008…RF-010.
- **F4.** **Parámetros legales por liquidación** (salario mínimo tomado de la **ficha del empleado**, topes calculados desde el salario del empleado con override, días de aguinaldo por antigüedad, recargo de vacación, divisores 30/365, límites de exención) con **snapshot inmutable** — RF-011.
- **F5.** **Edición y recálculo**: modificar encabezado/parámetros/líneas, override con nota, recálculo server-side de totales (ingresos, descuentos, neto, reservas) — RF-003.
- **F6.** **Emisión** (BORRADOR → EMITIDA, inmutable, journal `LIQUIDACION`) y **anulación** con motivo — RF-004, RF-005.
- **F7.** **Bandeja de liquidaciones** de la empresa (filtros por tipo/estado/motivo/fechas/empleado, contadores, detalle) — RF-006.
- **F8.** **Exportación**: Excel/csv/json individual (encabezado + detalle por sección + resumen), **boleta PDF estándar** (ratificado Fase 1) y export de bandeja; escenarios marcados SIMULACIÓN; todo sobre abstracciones reutilizables — RF-007, RF-014.
- **F9.** **Escenario de liquidación**: creación sobre una plaza activa de un empleado activo con fecha estimada y motivo hipotético, edición/eliminación libres, cero efectos — RF-012, RF-013.
- **F10.** **Transversales**: catálogos nuevos (`settlement-concepts`, `settlement-statuses`) con seed SV — RF-015; permisos dedicados — RF-016; integración con la reversión de retiro — RF-017; auditoría — RF-018.

### Fase 2 — Evoluciones (ratificadas como futuras)

- **Plantilla institucional/firmable** de la boleta PDF y del Excel (la boleta estándar ya sale en Fase 1 — D-19).
- **Señal de cierre para recontratación**: liquidación `EMITIDA` reemplaza la confirmación manual del rehire (D-18).
- **Convertir escenario → liquidación real** al ejecutarse el retiro (§17.14).
- **Flujo de aprobación** (revisor/aprobador) — ratificado: "emisión directa; las aprobaciones a futuro" (§17.10).
- **Autoservicio**: el empleado consulta su liquidación emitida (§17.13).
- **Módulo de vacaciones** (fecha de pago y fecha de goce): alimentará "el último registro" como fuente del cálculo vacacional (§17.4); catálogo de salario mínimo por sector/vigencia que alimente el campo de la ficha.
- **Beneficiario/heredero receptor** en bajas por fallecimiento (§17.15).
- **Notificaciones** (correo/in-app) — hoy solo existen stubs de log.

## 4. Fuera de alcance

- **FA-1. Pago y contabilización:** el módulo **calcula y documenta**; no ejecuta pagos, no genera partidas contables ni escribe en la planilla (que es un sistema externo — su bitácora `PersonnelFilePayrollTransaction` solo se alimenta por sincronización externa y **no se toca**).
- **FA-2. Motor de nómina general:** las fórmulas cubren la **liquidación**; no se calcula nómina recurrente, horas extra, comisiones ni planillas mensuales (D-03 de plazas ingresos/egresos sigue vigente para ese futuro módulo).
- **FA-3. Saldos de vacaciones/incapacidades:** sin registro de goce no hay saldo exacto; los días proporcionales son editables (G-04) y el módulo futuro de vacaciones refinará el default.
- **FA-4. Cambios al módulo de retiro:** se consume tal cual; el único toque es el gancho de reversión (RF-017), ya previsto por su D-14.
- **FA-5. Flujo de aprobación multinivel, notificaciones y autoservicio:** Fase 2 (patrón consistente de la casa).
- **FA-6. Multi-moneda:** la liquidación usa la moneda de la empresa (USD en SV); conversiones fuera de alcance.
- **FA-7. Recálculo fiscal anual exacto (devolución/complemento de Renta):** la línea de Renta es una **retención sugerida** con override del contador; la conciliación anual con la DGII ocurre fuera.
- **FA-8. Liquidación consolidada del empleado:** la ratificación fijó la granularidad **por plaza** (D-10); no existe una "liquidación total" que fusione plazas — la vista del empleado completo es la suma de sus liquidaciones por plaza en la bandeja/resumen.

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Gestor de RRHH (liquidaciones)** | Crea la liquidación desde el retiro ejecutado, ajusta el detalle (incluir/excluir/override), gestiona parámetros, **emite**, anula, exporta; crea y gestiona escenarios. |
| **Consulta RRHH / Gerencia** | Ve bandeja y detalle, exporta (solo lectura). |
| **Solicitante** | **Solo RRHH** (D-06 endurecida): por defecto el gestor que registra; si se elige a otro, debe ser del área funcional de RRHH. Referenciado con snapshot; nunca el empleado sujeto ni su jefatura. |
| **Empleado retirado** | Sujeto del cálculo. Sin acceso en Fase 1 (autoservicio §17.13). No puede gestionar su propia liquidación (anti-auto-gestión, D-20). |
| **Contador / Finanzas** | Consumidor del export; valida la Renta y ejecuta el pago **fuera** del sistema; con permiso de vista si se le otorga. |
| **Sistema** | Genera líneas sugeridas por motivo, calcula y recalcula server-side, aplica topes, mantiene snapshots, journalea `LIQUIDACION`, audita, y coordina con la reversión de retiro (anular/bloquear). |

## 6. Requerimientos funcionales

### Grupo A — Liquidación (ciclo de vida)

### RF-001 — Crear nueva liquidación (desde el retiro ejecutado, por plaza)

**Descripción:**
Crear una liquidación real para un empleado **retirado**, referenciando su solicitud de retiro `EJECUTADA` **y la plaza a liquidar** (elegida entre las asignaciones cerradas por esa baja — `ClosedRecords`; D-10). Se heredan en solo lectura: fecha de retiro, categoría y motivo (con snapshots de nombre). Se capturan: **solicitante** (solo RRHH — D-06; default el gestor que registra), **fecha de solicitud** y **observación**; el **salario mínimo** se copia de la ficha del empleado (RF-011). Al crear, el motor genera el detalle sugerido (RF-002) y calcula (RF-008…RF-010). Nace en `BORRADOR`.

**Reglas de negocio:**
- RN-001.1 El empleado debe tener una solicitud de retiro `EJECUTADA` vigente (no `REVERTIDA`); la liquidación referencia esa solicitud y una plaza cerrada por ella.
- RN-001.2 A lo sumo **una liquidación real no-ANULADA por (solicitud de retiro × plaza)** (D-16).
- RN-001.3 `FechaSolicitud ≤ hoy`. Observación opcional ≤ 2000 caracteres.
- RN-001.4 Solicitante: **miembro de RRHH** (default: usuario que registra; validación por área funcional de RRHH cuando esté configurada); snapshot de nombre; `RequestedByUserId` auditado (D-06).
- RN-001.5 Debe existir base salarial: `SALARIO_BASE` configurado en **la plaza elegida** (D-10); si no, 422 explicativo.
- RN-001.6 Anti-auto-gestión: el usuario vinculado al empleado sujeto no puede crear su propia liquidación (D-20).
- RN-001.7 Debe existir salario mínimo en la ficha del empleado o digitarse como override al crear (RF-011); sin ambos → 422 accionable.

**Criterios de aceptación:**
- Liquidación creada en `BORRADOR` con la plaza identificada (nombre/centro de costo en snapshot), detalle sugerido y totales calculados; retorna token de concurrencia.
- 422 bilingüe si: sin retiro ejecutado, retiro revertido, plaza no pertenece al retiro, (retiro × plaza) ya liquidado vigente, sin salario base en la plaza, sin salario mínimo, fechas inválidas.

**Prioridad:** Alta
**Dependencias:** Módulo de retiro (existente); RF-008…RF-011 (motor); RF-015 (catálogos); RF-016 (permisos).

### RF-002 — Detalle de conceptos: generación sugerida y gestión de líneas

**Descripción:**
Al crear (o regenerar), el sistema propone las líneas aplicables según el motivo: ingresos base (`SALARIO`, `VACACION_PROPORCIONAL`, `AGUINALDO_PROPORCIONAL`) siempre; `INDEMNIZACION` cuando la categoría es involuntaria; `RENUNCIA_VOLUNTARIA` cuando es voluntaria (nunca ambas por defecto — D-08); **`BONO_PENDIENTE`/`COMISION_PENDIENTE`** cuando la plaza tiene conceptos de ingreso de ese tipo activos (monto sugerido desde la configuración, editable); descuentos (`ISSS`, `AFP`, `RENTA`) sobre lo afecto; **`DESCUENTO_EXTERNO`** por cada deducción externa activa del empleado en la plaza (`DeductionClass.Externo` — última cuota sugerida con su contraparte); pagos patronales (`ISSS_PATRONAL`, `AFP_PATRONAL`, `INCAF`). El usuario puede **excluir/eliminar** líneas ("eliminar la información que no aplica"), **re-añadirlas** desde el catálogo y agregar líneas **manuales** (`HORAS_EXTRAS_PENDIENTES`, `OTRO_INGRESO`, `OTRO_DESCUENTO` con descripción y monto). Los conceptos a nivel empleado (sin plaza) se sugieren solo en la liquidación de la **plaza principal** (P-03).

**Reglas de negocio:**
- RN-002.1 Toda línea pertenece a un concepto activo del catálogo `settlement-concepts` del país; su clase (ingreso/descuento/reserva) viene del catálogo.
- RN-002.2 Excluir una línea la retira del cálculo (y de los totales) sin perder su definición en el catálogo; puede re-generarse.
- RN-002.3 Incluir `INDEMNIZACION` y `RENUNCIA_VOLUNTARIA` a la vez exige confirmación explícita (advertencia no bloqueante — casos atípicos negociados).
- RN-002.4 Las líneas manuales exigen descripción (≤ 300) y monto ≥ 0.

**Criterios de aceptación:**
- Con motivo involuntario se sugiere indemnización y no prestación por renuncia (y viceversa); las líneas excluidas no suman; una línea manual altera los totales al instante.

**Prioridad:** Alta
**Dependencias:** RF-001/RF-012; RF-015 (catálogo de conceptos).

### RF-003 — Edición y recálculo

**Descripción:**
Modificar, **solo en `BORRADOR`** (o en escenarios, siempre): encabezado (solicitante, fecha solicitud, observación), parámetros legales (RF-011), e insumos por línea (días, base, o **monto manual/override con nota obligatoria** — D-14). Cada guardado recalcula **todas** las líneas no-override y los totales server-side.

**Reglas de negocio:**
- RN-003.1 Solo `BORRADOR` es editable en liquidaciones reales; `EMITIDA` retorna 422 (corregir = anular + crear nueva).
- RN-003.2 El cliente **nunca** envía totales; toda cifra derivada se calcula en el servidor (anti-manipulación).
- RN-003.3 El override conserva visible el monto calculado original y exige nota (auditoría D-14); quitar el override restaura el cálculo.
- RN-003.4 Concurrencia optimista `If-Match` (400 sin header / 409 obsoleto — convención de la casa).

**Criterios de aceptación:**
- Cambiar el salario mínimo recalcula topes e indemnización/renuncia; un override con nota fija el monto final y sobrevive recálculos; sin nota → 422.

**Prioridad:** Alta
**Dependencias:** RF-008…RF-011.

### RF-004 — Emitir la liquidación

**Descripción:**
Transición `BORRADOR → EMITIDA`: congela el documento (inmutable), registra emisor y fecha, y journalea la acción de personal `LIQUIDACION` (tipo nuevo sembrado) con estado `APLICADA`.

**Reglas de negocio:**
- RN-004.1 Solo desde `BORRADOR`; requiere al menos **una línea de ingreso incluida** y neto ≥ 0 (neto negativo exige confirmación explícita con advertencia — caso deudas > haberes).
- RN-004.2 Anti-auto-gestión: el sujeto no emite su propia liquidación (403 dedicado).
- RN-004.3 La emisión no ejecuta ningún pago ni escribe en la planilla externa (FA-1); es un acto documental.

**Criterios de aceptación:**
- Emitida queda inmutable (PUT/PATCH de negocio → 422); el journal del empleado muestra `LIQUIDACION`; la bandeja refleja el estado.

**Prioridad:** Alta
**Dependencias:** RF-001…RF-003; seed `ActionType` (RF-015).

### RF-005 — Anular la liquidación

**Descripción:**
Transición a `ANULADA` desde `BORRADOR` (motivo opcional) o desde `EMITIDA` (motivo **obligatorio**). Terminal; conserva toda la historia. Tras anular, puede crearse una nueva liquidación para el mismo retiro (RN-001.2 libera el candado).

**Criterios de aceptación:**
- Anular emitida sin motivo → 422; anulada no acepta más transiciones; nueva liquidación creable después.

**Prioridad:** Alta
**Dependencias:** RF-001, RF-004.

### RF-006 — Bandeja de liquidaciones (consulta y detalle)

**Descripción:**
Consulta a nivel empresa con filtros (tipo real/escenario, estado, categoría/motivo, empleado, rangos de fecha de solicitud/retiro, texto), paginación, contadores por estado y detalle completo (encabezado + parámetros snapshot + líneas con calculado/ajustado + totales + línea de tiempo de emisión/anulación). Patrón bandeja de constancias/retiros (`POST …/query`).

**Reglas de negocio:**
- RN-006.1 Acceso con `ViewSettlements` (gate por handler, patrón reporting sin `AuthorizationPolicySet`); rate-limit de búsqueda estándar.
- RN-006.2 El detalle muestra snapshots aunque el expediente cambie después.

**Criterios de aceptación:**
- Filtros y contadores correctos; escenarios distinguibles de reales en el listado.

**Prioridad:** Alta
**Dependencias:** RF-001, RF-012; infraestructura de reporting existente.

### RF-007 — Exportación (Excel + boleta PDF)

**Descripción:**
Tres salidas: (a) **individual tabular** — la liquidación o escenario completo (encabezado, parámetros, detalle por sección, resumen) en xlsx/csv/json, layout estándar sin plantilla; (b) **boleta PDF estándar** de la liquidación individual (**Fase 1 — ratificado**), rendereada con el pipeline de documentos existente (`IDocumentModelRenderer`/QuestPDF, motor conmutable); (c) **bandeja** — el listado filtrado (una fila por liquidación-plaza con totales). Tabulares vía `ReportExportDeliveryService` (auditoría, límite síncrono 413); nombres `settlements.xlsx` / `settlement-{empleado}-{plaza}.{ext}`.

**Reglas de negocio:**
- RN-007.1 El export individual (tabular y PDF) de un **escenario** incluye la marca visible `SIMULACIÓN — SIN EFECTOS` (D-19, R-10).
- RN-007.2 El export respeta permisos de vista y se audita (patrón `ReportExported`).
- RN-007.3 **Mandato de reutilización (D-19):** se usan los servicios/abstracciones existentes; la pieza faltante (export de documento individual encabezado+detalle+totales) se diseña como **abstracción reutilizable** para futuros módulos, no como código ad-hoc del feature.

**Criterios de aceptación:**
- El xlsx y el PDF individuales reproducen exactamente los montos en pantalla (calculado, ajustado, final, secciones y resumen); el de bandeja respeta filtros.

**Prioridad:** Alta
**Dependencias:** RF-006; pipeline de documentos QuestPDF existente; extensión reutilizable del writer (plan técnico).

### Grupo B — Motor de cálculo

> **Nota normativa:** las fórmulas siguientes son los **defaults de El Salvador** propuestos (Código de Trabajo Arts. 58, 177, 187, 196-202; D.L. 592 LRPERV). **Todos los factores son parámetros** (RF-011) con snapshot por liquidación; el negocio debe validarlos con casos reales antes del build (§17, §18.1). Convención de bases: salario diario = mensual ÷ **30**; proporcionalidades sobre año de **365** días; redondeo a 2 decimales por línea.

### RF-008 — Cálculo de ingresos

**Descripción:**
Con `SalarioMensualBase` = `SALARIO_BASE` de **la plaza liquidada** (D-10) y `SalarioDiario = SalarioMensualBase / 30`:

| Concepto | Fórmula default (SV) | Insumos editables |
|---|---|---|
| `SALARIO` (pendiente) | `DíasPendientes × SalarioDiario` | **DíasPendientes** — lo digita el usuario (la planilla externa sabe qué se pagó); default sugerido: días del 1 del mes de retiro a la fecha de retiro. |
| `VACACION_PROPORCIONAL` | `SalarioDiario × 15 × (1 + 30%) × (DíasDesdeAniversario / 365)` | **DíasDesdeAniversario** (default: desde el último aniversario a la fecha de retiro) — editable. **Ratificado §17.4:** la fuente definitiva será "el último registro" (fecha de pago y fecha de goce) del **futuro módulo de vacaciones**; punto de integración declarado (G-04). |
| `AGUINALDO_PROPORCIONAL` | `SalarioDiario × DíasAguinaldo × (DíasPeriodo / 365)` | **DíasAguinaldo** por antigüedad: `15` (≥1 y <3 años), `19` (≥3 y <10), `21` (≥10) — calculado con override. **DíasPeriodo**: del 12-dic anterior a la fecha de retiro. |
| `INDEMNIZACION` | `SalarioMensualTopado₄ × AñosServicio + SalarioMensualTopado₄ × (DíasFracción / 365)` con `SalarioMensualTopado₄ = min(SalarioMensualBase, 4 × SalarioMínimoMensual)` | Multiplicador (4) y tope resultante visibles y con override (RF-011). |
| `RENUNCIA_VOLUNTARIA` | `(SalarioMensualTopado₂ / 30) × 15 × AñosServicio (+ fracción proporcional)` con `SalarioMensualTopado₂ = min(SalarioMensualBase, 2 × SalarioMínimoMensual)` | Multiplicador (2) y días (15). **Ratificado §17.6:** si no cumple el requisito legal (2 años de servicio), la línea se registra con **valor 0** y el motivo visible (no advertencia suelta, no bloqueo). |
| `BONO_PENDIENTE` / `COMISION_PENDIENTE` | Monto sugerido desde los conceptos de ingreso activos de la plaza (`BONO`/`COMISION`; valor si es fijo) | Monto editable; manual si la plaza no tiene el concepto configurado (D-08 ampliada). |
| `HORAS_EXTRAS_PENDIENTES` | Monto manual (opcional: `Horas × SalarioDiario/8 × Factor recargo`) | Horas/factor o monto directo — la planilla externa es quien conoce el pendiente (D-08 ampliada). |
| `OTRO_INGRESO` | Monto manual | Descripción + monto. |

**Reglas de negocio:**
- RN-008.1 `AñosServicio`/fracción según D-11 (periodo vigente `HireDate → FechaRetiro`).
- RN-008.2 La sugerencia inicial respeta el `SeparationType` (D-08); la exclusión mutua indemnización/renuncia es advertencia (RN-002.3).
- RN-008.3 Cada línea persiste su **base de cálculo** (días, factor, base, tope aplicado) para trazabilidad total del número.
- RN-008.4 **Principio de valor 0 (ratificado §17.6):** el concepto cuyo requisito legal no se cumple **se registra con monto 0** y el motivo legible (p. ej. "no cumple 2 años de servicio continuo"), en lugar de omitirse o bloquear el registro; un override con nota puede fijar otro valor (decisión del negocio, auditada).

**Criterios de aceptación:**
- Casos dorados del negocio (§18.1) reproducidos al centavo; cambiar un insumo (días, mínimo) recalcula la línea y los totales.

**Prioridad:** Alta
**Dependencias:** Compensación por plaza y perfil (existentes); RF-011.

### RF-009 — Cálculo de descuentos

**Descripción:**
Sobre la **porción afecta** de los ingresos incluidos (matriz de afectación del catálogo D-07):

| Concepto | Fórmula default | Fuente de tasas |
|---|---|---|
| `ISSS` | `3.00% × min(BaseAfectaISSS, Tope 1,000.00)` | Instancias ISSS del empleado; fallback: defaults del tipo (-9727). |
| `AFP` | `7.25% × min(BaseAfectaAFP, Tope 7,045.06)` | Instancias AFP del empleado; fallback: defaults del tipo (-9728). |
| `RENTA` | **Tabla oficial vigente 2026** (`MENSUAL`) sobre `BaseGravable`: `CuotaFija + Tasa% × (Base − ExcesoDe)` | `IncomeTaxWithholdingBracket` del tenant (cargar tabla 2026 — checklist de despliegue); **sugerida, override esperado** (D-12). |
| `DESCUENTO_EXTERNO` | **Última cuota a generar para pago** de cada deducción externa activa (préstamos, embargos) | Sugerida desde `DeductionClass.Externo` de la plaza/empleado con su valor y contraparte; editable/excluible (D-08 ampliada). |
| `OTRO_DESCUENTO` | Monto manual (anticipos, cuotas internas) | Usuario. |

**Matriz de afectación (ratificada §17.3 — "dentro de límites legales", exceso controlado por el sistema):** `SALARIO`, `VACACION_PROPORCIONAL`, `BONO_PENDIENTE`, `COMISION_PENDIENTE` y `HORAS_EXTRAS_PENDIENTES` afectos a ISSS/AFP/Renta; `AGUINALDO_PROPORCIONAL` no cotiza y exento de Renta **hasta su límite legal**; `INDEMNIZACION` y `RENUNCIA_VOLUNTARIA` no cotizan y exentas **hasta su monto legal** (Art. 4 LISR).

**Control del exceso gravable (ratificado §17.3 — lo controla el sistema, no el override):** cada concepto lleva su **regla de exención parametrizada**: aguinaldo exento hasta `LímiteExenciónAguinaldo` (default `2 × SalarioMínimoMensual` — confirmar valor con el contador, P-02); indemnización y renuncia voluntaria exentas hasta su **monto legal calculado** (todo excedente — típicamente por override al alza — es gravable). El motor calcula la **porción exenta y el excedente por línea** y suma automáticamente los excedentes a la `BaseGravable` de Renta, con el desglose visible.

**Reglas de negocio:**
- RN-009.1 Los topes ISSS/AFP se aplican por mes equivalente (una liquidación normalmente cubre fracción de mes); casos multi-periodo se resuelven con override (documentado).
- RN-009.2 Sin tramos de Renta configurados → línea `RENTA` en 0 con **advertencia visible** (no bloquea; G-09).
- RN-009.3 Ningún descuento supera la suma de ingresos afectos correspondientes sin confirmación (advertencia de neto negativo — RN-004.1).
- RN-009.4 El exceso gravable se calcula por el sistema línea a línea (regla de exención del catálogo + parámetros del snapshot); el usuario ve exento vs gravado y solo puede corregir vía override auditado.

**Criterios de aceptación:**
- Con base afecta $1,200: ISSS = $30.00 (tope); AFP = $87.00; Renta según tramo con desglose visible (cuota fija + % sobre exceso).

**Prioridad:** Alta
**Dependencias:** Config de compensación y tramos de Renta (existentes); RF-015 (matriz en catálogo).

### RF-010 — Pagos patronales, reserva (provisión contable) y resumen

**Descripción (re-especificada por la ratificación de D-13):**
Tres bloques de salida adicionales a ingresos y descuentos:
1. **Pagos patronales** — costo del empleador sobre las bases afectas: `ISSS_PATRONAL` (7.50% con tope), `AFP_PATRONAL` (8.75% con tope) e **`INCAF`** (ex-INSAFORP; 1%, base y tope default = los de ISSS — confirmar P-02). La sección muestra además, como **remisión informativa**, las retenciones del empleado a enterar (ISSS 3%, AFP 7.25%, Renta) — no son costo adicional ni suman al total patronal.
2. **Reserva (provisión contable)** — "reservar el dinero de la liquidación dentro del centro de costo": `Provisión = Total Ingresos + Total Pagos Patronales`, asignada al **centro de costo de la plaza liquidada** (snapshot de código/nombre). Fórmula a validar con los casos dorados (P-02).
3. **Resumen** — total ingresos, total descuentos, **neto a pagar**, total pagos patronales y **provisión total** con su centro de costo; es la sección final de la pantalla, del Excel y de la boleta PDF (D-08 ampliada).

**Reglas de negocio:**
- RN-010.1 Las líneas patronales son excluibles y con override como cualquier línea (D-14); la provisión y el resumen son **calculados, no editables** (se derivan de las líneas vigentes).
- RN-010.2 Nada de esta sección altera el `NetoAPagar` del empleado.
- RN-010.3 Si la plaza no tiene centro de costo asignado, la provisión se muestra sin destino con advertencia (dato de asignación opcional hoy).

**Criterios de aceptación:**
- `TotalIngresos − TotalDescuentos = NetoAPagar`; `Provisión = TotalIngresos + TotalPagosPatronales`; el resumen cuadra con las secciones en pantalla y en ambos exports.

**Prioridad:** Alta
**Dependencias:** RF-009 (bases afectas); centro de costo de la asignación (existente).

### RF-011 — Parámetros legales y topes (salario mínimo en la ficha + snapshot)

**Descripción (re-anclada por la ratificación §17.16):**
El **salario mínimo mensual aplicable vive en la ficha del empleado** (nuevo campo del perfil de empleo, editable junto a la demás información laboral; refleja el sector del empleado) — "de ahí se toma el salario con el que se calculará". Al crear la liquidación/escenario, el valor se copia como **snapshot** con **override auditado** (cubre fichas de retirados —bloqueadas para edición— o valores desactualizados). Del mínimo derivan los topes: el sistema calcula el **salario aplicable por concepto** = `min(salario de la plaza, tope legal)` (D-09) — indemnización `4×`, renuncia `2×` — y lo muestra como el "valor de salario máximo" con override. Completa el snapshot: días de vacación (15) y recargo (30%), días de aguinaldo por tramo (15/19/21), prestación por renuncia (15 días/año, servicio mínimo 2 años), **límites de exención** (aguinaldo `2×` mínimo — P-02), divisores (mes 30, año 365).

**Reglas de negocio:**
- RN-011.1 `SalarioMínimoMensual > 0`; multiplicadores > 0; el snapshot no se recalcula al cambiar la ficha o defaults posteriores (inmutabilidad histórica).
- RN-011.2 Nuevo campo en `PersonnelFileEmployeeProfile` (perfil de empleo); sin valor en la ficha, la creación exige digitarlo como override (RN-001.7) — 422 accionable si falta en ambos.
- RN-011.3 El escenario usa el mismo origen (ficha → snapshot → override).

**Criterios de aceptación:**
- Dos liquidaciones emitidas en años distintos conservan cada una sus parámetros; el detalle muestra qué tope y qué exención se aplicó a qué línea; cambiar la ficha después no altera liquidaciones existentes.

**Prioridad:** Alta
**Dependencias:** Perfil de empleo (campo nuevo — migración); módulo de retiro (la ficha del retirado es de solo lectura → el override cubre el caso).

### Grupo C — Escenario de liquidación

### RF-012 — Crear escenario (simulación)

**Descripción:**
Crear un registro `Kind = ESCENARIO` para un empleado **activo** (`Employee`+`Completed`, sin retiro vigente), eligiendo **una plaza activa** (D-10): **fecha de retiro estimada** (editable, ≥ `HireDate`), **categoría/motivo hipotéticos** (catálogo, coherencia jerárquica), solicitante (RRHH), observación y parámetros (RF-011, salario mínimo desde la ficha). El motor calcula igual que en la real sobre esa plaza. **Cero efectos**: sin journal, sin candados, sin estados.

**Reglas de negocio:**
- RN-012.1 Escenarios ilimitados por empleado; visibles en la bandeja con marca inequívoca.
- RN-012.2 Motivo debe pertenecer a la categoría (validación jerárquica existente).
- RN-012.3 Un escenario nunca aparece como liquidación del retiro ni bloquea la creación de la real.

**Criterios de aceptación:**
- Crear escenario para empleado retirado → 422 (la vía correcta es la liquidación real); los cálculos coinciden con los de una real ante los mismos insumos.

**Prioridad:** Alta
**Dependencias:** RF-008…RF-011; catálogos de motivo (existentes).

### RF-013 — Editar y eliminar escenario

**Descripción:**
Edición sin restricciones de estado (fecha estimada, motivo, parámetros, líneas — con recálculo) y **eliminación** (borrado lógico `IsActive=false`, convención de la casa).

**Criterios de aceptación:**
- Cambiar la fecha estimada recalcula antigüedad, proporcionales y aguinaldo; el eliminado desaparece de la bandeja (sin traza en journal — nunca la tuvo).

**Prioridad:** Alta
**Dependencias:** RF-012.

### RF-014 — Exportar escenario

**Descripción:**
Mismo export individual que RF-007 con marca `SIMULACIÓN — SIN EFECTOS` en el contenido y sufijo en el nombre del archivo.

**Prioridad:** Media
**Dependencias:** RF-007, RF-012.

### Grupo D — Transversales

### RF-015 — Catálogos nuevos y seeds SV

**Descripción:**
(a) `settlement-concepts` (tipado país-scoped, patrón TPH `GeneralCatalogItem`): código, nombre, clase (`INGRESO`/`DESCUENTO`/`PAGO_PATRONAL`), flags de afectación (ISSS/AFP/Renta), **regla de exención** (ninguna / hasta límite × mínimo / hasta monto legal — RN-009.4), `IsSystemCalculated`, orden — **seed SV con los 17 conceptos ratificados de D-07**. (b) `settlement-statuses` (país-scoped): `BORRADOR`, `EMITIDA`, `ANULADA` (patrón híbrido canónico+catálogo). (c) `ActionType` +1 seed: `LIQUIDACION`. Bloques nuevos de IDs negativos en la banda `-9xxx` según convención; claves wire en `GeneralCatalogKeyMap` (test de biyección).

**Prioridad:** Alta
**Dependencias:** Convenciones de catálogo existentes.

### RF-016 — Permisos dedicados y política de acceso

**Descripción:**
`PersonnelFiles.ViewSettlements` y `PersonnelFiles.ManageSettlements` (constantes + aprovisionamiento por tenant + políticas MVC + gates por handler `EnsureCanViewSettlementsAsync`/`EnsureCanManageSettlementsAsync` fail-closed). `ManageAdministration` como fallback universal (convención). Anti-auto-gestión del sujeto en crear/emitir/anular (403 dedicado).

**Prioridad:** Alta
**Dependencias:** Infraestructura IAM existente.

### RF-017 — Integración con la reversión de retiro

**Descripción:**
Gancho en la reversión (cierra la D-14 de retiro; ratificado §17.11): (a) si la solicitud a revertir tiene liquidaciones reales en `BORRADOR` (una por plaza) → se **anulan todas automáticamente** con motivo "Reversión de retiro" y se auditan; (b) si **alguna** tiene `EMITIDA` vigente → la reversión retorna **422 bloqueada** con código dedicado, indicando anularla primero (decisión consciente del operador). Los escenarios no se ven afectados.

**Criterios de aceptación:**
- Revertir con borradores → retiro revertido y todas las liquidaciones `ANULADAS` en la misma transacción; revertir con alguna emitida → 422 y ningún cambio.

**Prioridad:** Alta
**Dependencias:** Módulo de retiro (gancho puntual); RF-005.

### RF-018 — Auditoría y trazabilidad

**Descripción:**
Patrón de auditoría de expediente para crear/editar/emitir/anular/exportar; overrides auditados con nota, autor y fecha; journal `LIQUIDACION` al emitir; el detalle conserva monto calculado, ajustado y final por línea con su base de cálculo.

**Prioridad:** Alta
**Dependencias:** Infraestructura de auditoría existente.

## 7. Requerimientos no funcionales

- **Seguridad:** multi-tenant estricto (`TenantEntity` + query filter global); RBAC con el par dedicado (RF-016); datos salariales sensibles — coherencia con `ViewCompensation` en la asignación de roles; anti-auto-gestión; **todos los montos derivados se calculan server-side** (el cliente jamás envía totales ni montos calculados — solo insumos y overrides auditados).
- **Precisión numérica:** `decimal` en toda la cadena (nunca flotantes); montos `numeric(18,2)`, tasas `numeric(11,8)` (convención existente); redondeo **half-up a 2 decimales por línea**, totales = suma de líneas redondeadas (S-06 — confirmar con el contador).
- **Auditoría:** toda transición y override con usuario/fecha/nota; export auditado (`ReportExported`); snapshot de parámetros inmutable.
- **Concurrencia:** optimista `If-Match`/`ETag` en toda mutación (400 sin header / 409 obsoleto); emisión y el gancho de reversión transaccionales.
- **Rendimiento:** cálculo síncrono < 1 s (aritmética simple sobre datos ya cargados); bandeja paginada con rate-limit de búsqueda; export con límite síncrono (413 al exceder) y vía de jobs asíncronos existente si un tenant lo requiere.
- **Usabilidad (API):** errores 422/403 con `extensions.code` **bilingüe EN/ES** (recursos `.resx` — el test de paridad los exige); enums como strings; `Guid XxxId` → `xxxPublicId` en el wire; `AllowedActions` en DTOs de detalle (`ISupportsAllowedActions` — el test de cobertura lo exige).
- **Compatibilidad:** `openapi.yaml` regenerado sin drift; sin cambios de contrato a módulos existentes salvo el gancho de reversión (respuesta de error nueva, no breaking).
- **Mantenibilidad:** el motor completo como **módulo de reglas puro** (`Settlements.Rules` — patrón `CertificateRequest.Rules`/`RetirementRequest.Rules`) testeable sin infraestructura, con **suite de casos dorados** del negocio como tests unitarios; parámetros sembrados editables (sin re-deploy por cambio de salario mínimo).
- **Disponibilidad/escalabilidad:** sin componentes nuevos de infraestructura (sin scheduler, colas ni servicios externos).

## 8. Historias de usuario

### HU-001 — Crear la liquidación de un empleado retirado (por plaza)
Como **gestor de RRHH**, quiero **crear la liquidación de una plaza de un empleado a partir de su retiro ejecutado, con solicitante, fecha de solicitud, observación y el salario mínimo tomado de su ficha**, para **obtener al instante el cálculo completo: ingresos, descuentos, pagos patronales, reserva y resumen**.
**Criterios de aceptación:**
- Dado un empleado con retiro `EJECUTADA`, cuando elijo una de las plazas cerradas por la baja y creo la liquidación, entonces nace en `BORRADOR` con fecha/categoría/motivo heredados del retiro y el detalle sugerido calculado (incluidos bono/comisión/descuento externo si la plaza los tiene configurados).
- Dado un empleado activo o con retiro revertido, cuando intento crear, entonces recibo 422 explicativo.
- Dado un (retiro × plaza) que ya tiene liquidación vigente, cuando intento crear otra, entonces recibo 422 de duplicidad.

### HU-002 — Ajustar el detalle del cálculo
Como **gestor de RRHH**, quiero **excluir conceptos que no aplican, ajustar días/bases, agregar líneas manuales y fijar montos con nota**, para **que la liquidación refleje la realidad del caso sin perder la trazabilidad del cálculo original**.
**Criterios de aceptación:**
- Dado un borrador, cuando excluyo la indemnización, entonces los totales se recalculan sin ella y puedo re-generarla después.
- Cuando fijo un monto manual sin nota, entonces recibo un error de validación; con nota, el detalle muestra monto calculado y monto final.

### HU-003 — Emitir la liquidación
Como **gestor de RRHH**, quiero **emitir la liquidación cuando el cálculo está validado**, para **congelarla como documento definitivo y dejar constancia en el expediente**.
**Criterios de aceptación:**
- Dado un borrador con ingresos incluidos, cuando emito, entonces pasa a `EMITIDA` inmutable y el journal del empleado registra `LIQUIDACION`.
- Dado que soy el empleado sujeto, cuando intento emitir mi propia liquidación, entonces recibo 403.

### HU-004 — Anular una liquidación
Como **gestor de RRHH**, quiero **anular una liquidación (con motivo si estaba emitida)**, para **corregir errores creando una nueva versión limpia**.
**Criterios de aceptación:**
- Dada una emitida, cuando anulo con motivo, entonces queda `ANULADA` (terminal) y puedo crear una nueva liquidación del mismo retiro.

### HU-005 — Consultar la bandeja de liquidaciones
Como **usuario de consulta de RRHH**, quiero **listar liquidaciones y escenarios con filtros y contadores, y abrir su detalle completo**, para **dar seguimiento y auditar los cálculos**.
**Criterios de aceptación:**
- Cuando filtro por estado/tipo/motivo/fechas, entonces veo solo lo coincidente, con escenarios marcados como simulación.
- Cuando abro el detalle, entonces veo parámetros aplicados, base de cálculo de cada línea, montos calculado/ajustado/final y totales.

### HU-006 — Exportar a Excel y boleta PDF
Como **gestor de RRHH**, quiero **exportar la liquidación individual a Excel y como boleta PDF, y el listado filtrado a Excel**, para **entregarla al contador/finanzas y reportar a gerencia**.
**Criterios de aceptación:**
- Cuando exporto una liquidación (xlsx o PDF), entonces el archivo contiene encabezado, parámetros, detalle por sección y el resumen idénticos a la pantalla.
- Cuando exporto la bandeja, entonces descargo el conjunto filtrado (xlsx/csv/json) con auditoría del export.

### HU-007 — Simular una liquidación (escenario)
Como **gestor de RRHH**, quiero **crear un escenario con fecha de retiro estimada y motivo hipotético para un empleado activo**, para **conocer el costo de una eventual salida sin afectar la planilla ni ningún dato real**.
**Criterios de aceptación:**
- Dado un empleado activo, cuando elijo una plaza activa y creo el escenario con fecha estimada futura, entonces veo ingresos/descuentos/pagos patronales/reserva calculados como si esa plaza se liquidara ese día.
- Entonces ningún dato del empleado, journal o planilla cambia; el escenario aparece solo en la bandeja como SIMULACIÓN.

### HU-008 — Ajustar o eliminar un escenario
Como **gestor de RRHH**, quiero **modificar la fecha estimada, el motivo o el detalle del escenario, o eliminarlo**, para **iterar hipótesis (¿y si se va en diciembre?, ¿y si es mutuo acuerdo?) y descartar las que no sirven**.
**Criterios de aceptación:**
- Cuando cambio la fecha estimada, entonces antigüedad, proporcionales y topes se recalculan.
- Cuando elimino el escenario, entonces desaparece del listado sin dejar efectos.

### HU-009 — Exportar un escenario
Como **gestor de RRHH**, quiero **exportar el escenario a Excel marcado como simulación**, para **compartirlo con gerencia en la toma de decisiones sin que se confunda con una liquidación real**.
**Criterios de aceptación:**
- El archivo incluye la marca `SIMULACIÓN — SIN EFECTOS` y la fecha estimada usada.

### HU-010 — Coherencia ante una reversión de retiro
Como **usuario con permiso de reversión de retiros**, quiero **que al revertir una baja la liquidación en borrador se anule sola y una emitida me bloquee con explicación**, para **que nunca quede una liquidación viva de un retiro que "no ocurrió"**.
**Criterios de aceptación:**
- Dado un retiro con liquidación en borrador, cuando revierto, entonces la liquidación queda `ANULADA` (motivo automático) en la misma operación.
- Dado un retiro con liquidación emitida, cuando intento revertir, entonces recibo 422 indicando anularla primero.

## 9. Reglas de negocio (consolidadas)

1. **RN-01.** La liquidación real existe solo sobre una solicitud de retiro `EJECUTADA` vigente; hereda de ella fecha, categoría y motivo (solo lectura) y valoriza **una plaza** cerrada por esa baja (D-03, D-10).
2. **RN-02.** A lo sumo una liquidación real no-`ANULADA` por **(solicitud de retiro × plaza)**; escenarios ilimitados (D-16).
3. **RN-03.** El escenario es de empleados activos, sobre **una plaza activa**, con fecha estimada ≥ `HireDate`, y **jamás** produce efectos (sin journal, sin candados, sin estados) (D-02, D-05).
4. **RN-04.** Máquina de estados (real): `BORRADOR → EMITIDA → ANULADA`; solo `BORRADOR` edita; `EMITIDA` es inmutable; `ANULADA` es terminal; anular una emitida exige motivo (D-15).
5. **RN-05.** El motor sugiere líneas por `SeparationType` (involuntaria → indemnización; voluntaria → prestación por renuncia) y por configuración de la plaza (bono/comisión/descuento externo); coexistencia indemnización+renuncia solo con confirmación explícita; **concepto legalmente no cumplido → valor 0 con motivo visible** (D-08).
6. **RN-06.** Base salarial = `SALARIO_BASE` de la plaza liquidada (cerrada por el retiro / activa en escenario); sin base configurada no hay liquidación; el salario mínimo se toma de la **ficha del empleado** con snapshot + override (D-09, D-10, RF-011).
7. **RN-07.** Antigüedad = periodo vigente `HireDate → FechaRetiro`; años completos + fracción días/365; periodos anteriores no se acumulan (D-11).
8. **RN-08.** Salario diario = mensual/30; proporcionalidades sobre 365; redondeo half-up a 2 decimales por línea; totales = suma de líneas (parámetros RF-011).
9. **RN-09.** Topes legales: indemnización sobre salario ≤ 4× mínimo; prestación por renuncia sobre salario ≤ 2× mínimo; ISSS/AFP hasta su tope de IBC; topes visibles y con override auditado (D-09, D-12).
10. **RN-10.** Afectación por concepto según la matriz del catálogo (salario, vacación, bono, comisión y horas extras cotizan y tributan; aguinaldo, indemnización y renuncia exentos **hasta sus límites legales**) y **el sistema controla el exceso gravable** por línea, sumándolo a la base de Renta (ratificado §17.3; RN-009.4).
11. **RN-11.** La Renta es una retención **sugerida** por la **tabla oficial vigente 2026**, con override esperado; sin tramos → 0 + advertencia (D-12).
12. **RN-12.** Los **pagos patronales** (ISSS 7.5%, AFP 8.75%, INCAF 1%) se calculan e informan sin alterar el neto; la **reserva (provisión contable)** = ingresos + pagos patronales, asignada al centro de costo de la plaza; el **resumen** cierra el cálculo (D-13, RF-010).
13. **RN-13.** Todo monto derivado se calcula server-side; los overrides exigen nota y conservan visible el monto calculado (D-14).
14. **RN-14.** Emisión requiere ≥1 ingreso incluido; neto negativo solo con confirmación explícita; el sujeto no gestiona su propia liquidación (RF-004, D-20).
15. **RN-15.** Reversión de retiro: anula automáticamente **todos** los `BORRADOR` del retiro; se bloquea si **alguna** `EMITIDA` vigente (D-17).
16. **RN-16.** La emisión journalea `LIQUIDACION`; nada de este módulo escribe en la planilla externa ni en su bitácora (FA-1).
17. **RN-17.** Los parámetros legales viven en snapshot por registro; cambios de defaults no reescriben historia (D-09).
18. **RN-18.** Toda mutación exige `If-Match` y refresca el token; toda transición/override se audita (convención de la casa).

## 10. Flujos principales

### Flujo 1 — Nueva liquidación (fin a fin)
1. El gestor de RRHH abre "Liquidaciones → Nueva", elige al empleado retirado (el sistema localiza su retiro `EJECUTADA`) y **la plaza a liquidar** (de las cerradas por la baja).
2. El sistema precarga fecha de retiro, categoría y motivo (solo lectura), propone como solicitante al propio gestor (RRHH) y toma el **salario mínimo de la ficha del empleado** junto a los demás parámetros default (multiplicadores, días, exenciones).
3. El gestor completa fecha de solicitud y observación, ajusta parámetros si procede (override auditado), y guarda.
4. El sistema crea el `BORRADOR`, genera el detalle sugerido por motivo y configuración de la plaza (bono/comisión/descuento externo) y calcula las **cinco secciones**: ingresos, descuentos (con exceso gravable controlado), pagos patronales, reserva por centro de costo y resumen.
5. El gestor depura: excluye lo que no aplica, ajusta días (p. ej. vacaciones ya gozadas), agrega líneas manuales, fija overrides con nota; cada guardado recalcula.
6. Validado el cálculo (idealmente contra el contador), el gestor **emite**: la liquidación queda inmutable y se journalea `LIQUIDACION`.
7. El gestor **exporta el Excel y la boleta PDF** y los entrega a finanzas; el pago se ejecuta fuera del sistema. Repite el flujo por cada plaza restante del retiro.

### Flujo 2 — Escenario de liquidación (simulación)
1. El gestor abre "Liquidaciones → Escenario", elige un empleado activo y **una de sus plazas activas**.
2. Ingresa la **fecha de retiro estimada**, categoría/motivo hipotéticos, solicitante y parámetros (mínimo desde la ficha).
3. El sistema calcula las cinco secciones como si esa plaza se liquidara en la fecha estimada.
4. El gestor itera: cambia fecha o motivo, ajusta líneas, compara resultados; exporta a Excel (marcado SIMULACIÓN) para gerencia.
5. Si la salida se decide: se tramita el retiro en su módulo y, ejecutada la baja, se crea la **liquidación real** (Fase 2: conversión directa del escenario).
6. Si no, el escenario se conserva como referencia o se elimina.

### Flujo 3 — Reversión de retiro con liquidación
1. El usuario con permiso de reversión revierte un retiro ejecutado (módulo de retiro).
2. El sistema detecta la liquidación del retiro: si está en `BORRADOR`, la anula automáticamente (motivo "Reversión de retiro") dentro de la misma operación; si está `EMITIDA`, bloquea la reversión (422) y explica que debe anularse primero.
3. Tras la reversión exitosa, el empleado queda restaurado y sin liquidación vigente; la anulada conserva su historia.

## 11. Flujos alternativos y excepciones

| # | Escenario | Comportamiento esperado |
|---|---|---|
| E-01 | Crear liquidación para empleado sin retiro ejecutado (activo, o retiro solo `SOLICITADA`/`AUTORIZADA`) | 422 — la liquidación real exige la baja ejecutada; para hipótesis está el escenario. |
| E-02 | Crear liquidación sobre retiro `REVERTIDA` | 422 — el retiro "no ocurrió". |
| E-03 | Segunda liquidación del mismo (retiro × plaza) con una vigente | 422 de duplicidad (anular primero). |
| E-04 | Crear escenario para empleado retirado | 422 — corresponde liquidación real. |
| E-05 | Plaza elegida sin `SALARIO_BASE` configurado | 422 explicativo señalando la configuración de compensación faltante. |
| E-06 | Tenant sin tramos de Renta vigentes (tabla 2026 no cargada) | Línea `RENTA` = 0 con advertencia visible; no bloquea (override manual disponible); checklist de despliegue. |
| E-07 | Renuncia voluntaria con antigüedad < 2 años | Línea registrada con **valor 0** y motivo legal visible (ratificado §17.6); override con nota posible. |
| E-08 | Incluir indemnización Y prestación por renuncia | Advertencia + confirmación explícita (caso atípico negociado). |
| E-09 | Salario mínimo ≤ 0, multiplicador ≤ 0, días negativos | 400/422 de validación de parámetros. |
| E-10 | Override sin nota | 422 (nota obligatoria — D-14). |
| E-11 | Editar una `EMITIDA` | 422 — inmutable; el flujo correcto es anular y crear nueva. |
| E-12 | Emitir sin líneas de ingreso incluidas | 422 (RN-14). |
| E-13 | Emitir con neto negativo | 422 salvo confirmación explícita (flag) — advertencia de deudas > haberes. |
| E-14 | El empleado sujeto intenta crear/emitir/anular su propia liquidación | 403 anti-auto-gestión (código dedicado). |
| E-15 | Revertir retiro con **alguna** liquidación `EMITIDA` vigente | 422 bloqueada con código dedicado (anular primero) — RF-017. |
| E-16 | Fecha estimada de escenario < `HireDate` | 422 de coherencia de fechas. |
| E-16b | Ficha del empleado sin salario mínimo y sin override al crear | 422 accionable (RN-001.7): registrar el valor en la ficha o digitarlo en la liquidación. |
| E-16c | Plaza elegida que no pertenece al retiro / no activa (escenario) | 422 de coherencia de plaza (D-10). |
| E-16d | Plaza sin centro de costo asignado | La provisión se muestra **sin destino** con advertencia (no bloquea) — RN-010.3. |
| E-16e | Solicitante que no pertenece a RRHH | 422 de validación del solicitante (D-06 endurecida). |
| E-17 | Mutación sin `If-Match` / token obsoleto | 400 / 409 (convención de la casa). |
| E-18 | Usuario sin permiso (vista o gestión) | 403; export respeta los mismos permisos. |
| E-19 | Export que excede el límite síncrono | 413 (vía asíncrona de jobs disponible como alternativa). |
| E-20 | Motivo de escenario no pertenece a la categoría | 422 de coherencia jerárquica (validación existente). |

## 12. Datos requeridos

### Entidad: Liquidación (`PersonnelFileSettlement` — nueva)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Auto | Único | Identificador público (wire: `publicId`) |
| Kind | Enum string (`LIQUIDACION`/`ESCENARIO`) | Sí | Inmutable tras crear | Modo del registro (D-02) |
| Empleado (PersonnelFileId) | Referencia expediente | Sí | Employee+Completed; real: retirado / escenario: activo | Sujeto de la liquidación |
| RetirementRequestPublicId | GUID | Real: Sí / Escenario: — | Solicitud `EJECUTADA` vigente del empleado | Ancla al retiro (D-03) |
| AssignedPositionPublicId | GUID | Sí | Real: plaza cerrada por ese retiro (`ClosedRecords`); escenario: plaza activa; única con liquidación real viva por (retiro × plaza) (D-10, D-16) | **Plaza liquidada** |
| PositionNameSnapshot / CostCenterPublicId / CostCenterNameSnapshot | Texto / GUID / Texto | Auto | Centro de costo de la asignación (destino de la provisión — RN-010.3) | Identificación de la plaza y su centro de costo |
| FechaRetiro | Fecha | Sí | Real: heredada (solo lectura); escenario: editable ≥ HireDate | Fecha efectiva o estimada |
| RetirementCategoryCode / NameSnapshot | Código catálogo + texto | Sí / Auto | Real: heredados; escenario: activos y coherentes (jerarquía) | Categoría del motivo (catálogo existente, D-04) |
| RetirementReasonCode / NameSnapshot | Código catálogo + texto | Sí / Auto | Ídem; pertenece a la categoría | Motivo (catálogo existente) |
| Solicitante (RequesterFilePublicId) | Referencia expediente | Sí | **Solo RRHH** (default: gestor que registra; validación por área funcional RRHH) — nunca el sujeto | Quien pide la liquidación (D-06 endurecida) |
| RequesterNameSnapshot | Texto (≤300) | Auto | — | Nombre del solicitante al registrar |
| FechaSolicitud | Fecha | Sí | ≤ hoy | Fecha de la petición |
| Observacion | Texto (≤2000) | No | — | Observación del encabezado |
| StatusCode | Código | Auto (real) | `BORRADOR`/`EMITIDA`/`ANULADA` (RF-015); escenario: sin ciclo | Estado del ciclo (D-15) |
| **Parámetros (snapshot):** SalarioMinimoMensual (desde la **ficha**, override auditado) · MultTopeIndemnizacion (4) · MultTopeRenuncia (2) · DiasVacacion (15) · RecargoVacacionPct (30) · DiasAguinaldo (15/19/21, override) · DiasPrestacionRenuncia (15) · ServicioMinimoRenunciaAnios (2) · LimiteExencionAguinaldo (2× mínimo — P-02) · DivisorMes (30) · DivisorAnio (365) | Decimal/entero | Sí (defaults) | > 0; snapshot inmutable | Parámetros legales aplicados (D-09, RF-011) |
| **Derivados (snapshot):** SalarioMensualBase (de la plaza) · SalarioMaxIndemnizacion · SalarioMaxRenuncia (= min(salario, tope) — D-09) · AntiguedadAnios/Dias | Decimal | Auto | Calculados; topes con override auditado | Bases del cálculo visibles (D-10, D-11) |
| **Totales (resumen):** TotalIngresos · TotalDescuentos · NetoAPagar · TotalPagosPatronales · ProvisionTotal | Decimal (18,2) | Auto | Server-side; nunca del cliente; `Provision = Ingresos + PagosPatronales` | Resultado del motor en 5 secciones (RN-13, RF-010) |
| CurrencyCode | Código ISO | Auto | Moneda de la empresa | USD en SV (FA-6) |
| EmitidaPorUserId / FechaEmisionUtc | GUID / Fecha | Auto | — | Emisión (RF-004) |
| AnuladaPorUserId / FechaAnulacionUtc / MotivoAnulacion | GUID/Fecha/Texto (≤2000) | Auto / motivo Sí desde EMITIDA | — | Anulación (RF-005) |
| RequestedByUserId | GUID | Auto | — | Usuario que registró (auditoría) |
| IsActive | Booleano | Auto | — | Borrado lógico (escenarios) |
| ConcurrencyToken | GUID | Auto | If-Match | Concurrencia optimista |

### Entidad: Línea de liquidación (`PersonnelFileSettlementLine` — nueva)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId / SettlementId | GUID / FK | Auto | Cascada con la liquidación | Identidad y pertenencia |
| ConceptClass | Enum string (`INGRESO`/`DESCUENTO`/`PAGO_PATRONAL`) | Auto | Del catálogo | Clase de la línea (la provisión/resumen son bloques calculados, no líneas — D-13) |
| ConceptCode / ConceptNameSnapshot | Código catálogo + texto | Sí / Auto | Activo en `settlement-concepts` del país | Concepto (D-07) |
| Descripcion | Texto (≤300) | Manuales: Sí | — | Detalle de líneas `OTRO_*` |
| BaseCalculo | Decimal | Calculadas: Auto | ≥ 0 | Base usada (p. ej. salario afecto topado) |
| DiasOFactor | Decimal | Según concepto | ≥ 0; editable (D-14) | Días/factor del cálculo (p. ej. días pendientes, días desde aniversario) |
| MontoCalculado | Decimal (18,2) | Auto | Motor server-side | Resultado de la fórmula (0 en manuales) |
| MontoOverride / NotaOverride | Decimal / Texto (≤500) | No / Sí si hay override | Override ⇒ nota obligatoria (RN-13) | Ajuste manual auditado |
| MontoFinal | Decimal (18,2) | Auto | `Override ?? Calculado` | Monto que suma a totales |
| Incluida | Booleano | Auto (default true) | Excluida no suma (RN-002.2) | Inclusión en el cálculo |
| DetalleBase | Texto (≤500) | Auto | — | Trazabilidad legible ("15 × 1.30 × 143/365 × $12.17…") |
| SortOrder / ConcurrencyToken | Entero / GUID | Auto | — | Orden y concurrencia |

### Catálogo: Conceptos de liquidación (`settlement-concepts` — nuevo, país-scoped, tipado)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| CountryCode/Code/Name/SortOrder/IsActive + NormalizedCode/Name | Convención catálogos | Sí | Único por país (código normalizado) | Base TPH `GeneralCatalogItem` |
| ConceptClass | Enum string | Sí | INGRESO/DESCUENTO/PAGO_PATRONAL | Clase |
| AffectsIsss / AffectsAfp / AffectsRenta | Booleanos | Sí (ingresos) | Matriz de afectación (RN-10) | Qué descuentos/aportes genera |
| ExemptionRule | Enum string | Sí | `NINGUNA` / `HASTA_LIMITE_X_MINIMO` / `HASTA_MONTO_LEGAL` | Regla de exceso gravable controlada por el sistema (RN-009.4) |
| IsSystemCalculated | Booleano | Sí | Motor vs manual | `HORAS_EXTRAS_PENDIENTES`/`OTRO_*` = manual |
| **Seed SV (17 — ratificado)** | — | — | — | Ingresos: `SALARIO`, `VACACION_PROPORCIONAL`, `AGUINALDO_PROPORCIONAL`, `INDEMNIZACION`, `RENUNCIA_VOLUNTARIA`, `BONO_PENDIENTE`, `COMISION_PENDIENTE`, `HORAS_EXTRAS_PENDIENTES`, `OTRO_INGRESO` · Descuentos: `ISSS`, `AFP`, `RENTA`, `DESCUENTO_EXTERNO`, `OTRO_DESCUENTO` · Pagos patronales: `ISSS_PATRONAL`, `AFP_PATRONAL`, `INCAF` (ex-INSAFORP) |

### Catálogo: Estados de liquidación (`settlement-statuses` — nuevo, país-scoped)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| CountryCode / Code / Name / SortOrder / IsActive | Convención catálogos | Sí | Código único por país | Seed SV: `BORRADOR`, `EMITIDA`, `ANULADA` (D-15) |

### Entidades existentes afectadas / consultadas

| Entidad | Uso | Cambio |
|---|---|---|
| `PersonnelFileRetirementRequest` (+`ClosedRecords`) | Lectura: ancla, fechas, motivo, plazas cerradas | Gancho de reversión (RF-017) — anula/bloquea liquidación |
| `PersonnelFileCompensationConcept` / `CompensationConceptTypeCatalogItem` | Lectura: `SALARIO_BASE`, ingresos `BONO`/`COMISION`, deducciones externas (`DeductionClass.Externo`) de la plaza; tasas/topes ISSS-AFP | Ninguno |
| `IncomeTaxWithholdingBracket` | Lectura: tramos Renta vigentes (**tabla 2026** — checklist de despliegue) | Ninguno |
| `PersonnelFileEmployeeProfile` | Lectura: `HireDate`; **+1 campo nuevo: salario mínimo mensual aplicable** (RF-011, editable con la información de empleo) | +1 columna + migración |
| `PersonnelFileEmploymentAssignment` / `ContractHistory` | Lectura: plaza, centro de costo, periodos | Ninguno |
| Catálogo `ActionType` | Journal al emitir | +1 seed `LIQUIDACION` (RF-015) |
| `PersonnelFilePayrollTransaction` | — | **Intocable** (FA-1) |

## 13. Integraciones necesarias

**Externas:** ninguna. Explícitamente: **no** hay integración con el sistema de planilla externo — ni la liquidación real ni el escenario escriben en la planilla (el requerimiento del escenario lo exige; en la real, el pago ocurre fuera y, si acaso, regresa por la sincronización externa existente a la bitácora — sin participación de este módulo).

**Internas (puntos de contacto):**
- **Módulo de retiro definitivo** — fuente del retirado (solicitud `EJECUTADA`: fecha, motivo, `ClosedRecords`); gancho de reversión (RF-017) que cierra su D-14.
- **Compensación por plaza** — lectura del `SALARIO_BASE` por asignación, de los conceptos de ingreso `BONO`/`COMISION` y de las **deducciones externas** (`DeductionClass.Externo`, con contraparte) para las sugerencias de líneas; tasas/topes ISSS-AFP efectivos (instancias → defaults del tipo).
- **Tramos de Renta** — lectura de la tabla vigente del tenant (`MENSUAL`, **tabla oficial 2026** — checklist de despliegue).
- **Perfil y periodos** — `HireDate`, **salario mínimo aplicable (campo nuevo en la ficha — RF-011)**, línea de tiempo derivada de contratos (antigüedad D-11); centro de costo de la asignación (destino de la provisión).
- **Catálogos de motivo de retiro** — reutilización directa (D-04), incluida la validación jerárquica.
- **Recontratación** — Fase 2: la liquidación `EMITIDA` como señal automática de cierre del periodo anterior (D-18; hoy `PriorPeriodClosureConfirmed` manual).
- **Acciones de personal** — journal `LIQUIDACION` al emitir.
- **Reporting/exportación** — bandeja `POST …/query` + export tabular vía `ReportExportDeliveryService` (patrón constancias/retiros) y **boleta PDF** vía el pipeline de documentos existente (`IDocumentModelRenderer`/QuestPDF, motor conmutable); la pieza nueva (export de documento individual) se construye **reutilizable** (D-19).
- **Auditoría e IAM** — patrones existentes (auditoría de expediente; aprovisionamiento de permisos por tenant).
- **(Futuro)** módulo de vacaciones (saldo exacto de días), boleta PDF (QuestPDF), contabilidad/nómina.

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Gestor de RRHH (liquidaciones) | `ViewSettlements` + `ManageSettlements`: crear/editar/emitir/anular liquidaciones y escenarios, exportar | Anti-auto-gestión si es el sujeto; requiere datos de compensación configurados; ve salarios (otorgar junto a `ViewCompensation` por coherencia de rol) |
| Consulta RRHH / Gerencia / Finanzas | `ViewSettlements`: bandeja, detalle, export | Solo lectura |
| Administrador (`ManageAdministration`) | Fallback universal (convención de la casa) | — |
| Revertidor de retiros | (Permisos de retiro existentes) | La reversión interactúa con liquidaciones vía RF-017; no requiere permisos de liquidación para el efecto automático |
| Empleado | Sin acceso en Fase 1 | Autoservicio de consulta = Fase 2 (§17.13); nunca gestiona la propia (D-20) |
| Sistema | Motor de cálculo, snapshots, journal, gancho de reversión | Todos los montos derivados server-side |

## 15. Criterios de aceptación generales

1. Compilación sin errores; **suites unitaria e integración verdes** (hoy ~450 tests de integración y ~1,740 unitarios), incluidas las pruebas de cobertura de `AllowedActions` y de paridad de localización EN/ES.
2. **Casos dorados del negocio reproducidos al centavo:** al menos 5 liquidaciones reales históricas (una por familia de motivo: renuncia, despido, mutuo acuerdo, fin de contrato, jubilación), **incluyendo un caso multi-plaza, uno con exceso gravable y uno de renuncia < 2 años (valor 0)**, firmadas por RRHH/contador y codificadas como tests unitarios del motor — criterio de salida **bloqueante** (§18.1).
3. Migraciones aplicadas sin drift; seeds SV verificados (**17 conceptos**, 3 estados, tipo de acción `LIQUIDACION`); campo de salario mínimo en la ficha migrado.
4. Permisos `ViewSettlements`/`ManageSettlements` aprovisionados y visibles en el catálogo de permisos; gates verificados (anti-auto-gestión y solicitante-solo-RRHH incluidos).
5. Flujo completo demostrable fin a fin: retirar empleado multi-plaza → crear liquidación **por cada plaza** → ajustar (excluir línea, override con nota) → emitir → exportar **Excel y boleta PDF** → anular → nueva; y escenario → iterar fecha/motivo → exportar → eliminar.
6. Integración de reversión probada en ambos sentidos: borrador se anula automáticamente; emitida bloquea la reversión (422).
7. Ningún dato de planilla externa tocado (bitácora intacta); el escenario demuestra **cero efectos** colaterales (perfil, journal, estados idénticos antes/después).
8. Errores bilingües (EN/ES) para todos los códigos nuevos; `openapi.yaml` regenerado sin drift; guía de integración frontend publicada (`docs/technical/guia-integracion-frontend-liquidacion.md`).
9. Los módulos de retiro, recontratación y compensación siguen funcionando sin regresión.

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **R-01 — Exactitud legal:** las fórmulas y topes cambian por reforma o ajuste anual del salario mínimo. Mitigación: todo parametrizado (RF-011) con snapshot por registro; los defaults se corrigen sin re-deploy ni reescritura de historia.
- **R-02 — Vacaciones sin registro de goce:** el proporcional por aniversario puede sub/sobre-estimar si hay vacaciones gozadas o años acumulados no gozados. Mitigación: días editables + advertencia; **ratificado §17.4**: la fuente definitiva será "el último registro" (fecha de pago/goce) del módulo futuro de vacaciones — punto de integración declarado (G-04).
- **R-03 — Renta en liquidación ≠ tabla mensual:** la retención correcta puede requerir recálculo del contador. Mitigación: línea sugerida + override esperado (D-12) + desglose visible del tramo aplicado.
- **R-04 — Expectativa de "aplicar a planilla":** el negocio podría esperar que la liquidación pague o contabilice. Mitigación: gestión de expectativas explícita (FA-1, §0.3); el export es el puente con finanzas.
- **R-05 — Datos incompletos de compensación:** empleados sin `SALARIO_BASE` por plaza bloquean la liquidación. Mitigación: 422 temprano y accionable (E-05); reporte de configuración faltante como mejora.
- **R-06 — Tramos de Renta solo con seed de desarrollo:** tenants productivos sin tramos → Renta en 0. Mitigación: advertencia visible + checklist de despliegue (configurar `PUT api/v1/income-tax-brackets`).
- **R-07 — Redondeos vs práctica del contador:** diferencias de centavos por orden de redondeo. Mitigación: regla única documentada (RN-08) validada en los casos dorados (§18.1).
- **R-08 — Fuentes de "salario mínimo":** el campo de la **ficha del empleado** (fuente ratificada — RF-011) puede divergir del `MinContributionBase` de ISSS/AFP (IBC mínimo) o quedar desactualizado tras un ajuste legal. Mitigación: son conceptos distintos y así se documentan; snapshot + override auditado por liquidación; checklist anual de actualización de fichas.
- **R-12 — Antigüedad en la liquidación por plaza — RESUELTO (P-01 ratificada):** la antigüedad se calcula **desde el `StartDate` de la asignación liquidada**; queda cubierto con un caso dorado multi-plaza que valide tramos de aguinaldo/años en plazas secundarias.
- **R-13 — Fórmula de la provisión — RESUELTO (P-02 ratificada):** `Provisión = Total Ingresos + Total Pagos Patronales`; los casos dorados verifican los **valores** finos (exención de aguinaldo 2× mínimo, INCAF 1% base/tope ISSS, renta patronal como remisión informativa).
- **R-09 — Abuso del override:** montos manuales podrían desvirtuar el cálculo. Mitigación: nota obligatoria, auditoría, y visibilidad permanente de calculado vs final (D-14).
- **R-10 — Confusión escenario/real:** una simulación tomada por liquidación oficial. Mitigación: marca `SIMULACIÓN` en bandeja, detalle y export (RN-007.1); entidades separadas por `Kind` con candados solo en la real.
- **R-11 — Bajas por fallecimiento:** el "solicitante" y el receptor del pago no son el empleado. El cálculo no cambia, pero el destinatario (herederos/beneficiarios) es tema administrativo externo en Fase 1 (§17.15).

### Supuestos

- **S-01** El Salvador es el primer país (moneda USD, preferencia de empresa); el diseño multi-país queda cubierto por catálogos país-scoped y parámetros por registro.
- **S-02** Convenciones de cálculo SV: mes de 30 días, año de 365, salario diario = mensual/30 (parametrizados).
- **S-03** El pago material, su contabilización y cualquier retención definitiva ante la DGII ocurren **fuera** del sistema (FA-1, FA-7).
- **S-04** "Salario mínimo" y "valor de salario máximo" del requerimiento = parámetros de topes legales (D-09) — **confirmado en la ratificación (§17.2)**: el máximo lo calcula el sistema desde el salario del empleado topado.
- **S-05** Todo retirado proviene del módulo de retiro (puerta única + limpieza de legados ya ratificadas allí): no hay que liquidar bajas "legadas" fuera del modelo.
- **S-06** Redondeo half-up a 2 decimales por línea; totales = suma de líneas redondeadas — a validar con el contador (R-07).
- **S-07** RRHH conoce los días de salario pendientes (la planilla externa es quien sabe qué se pagó): el sistema los sugiere pero el usuario manda (RF-008).
- **S-08** La antigüedad legal para prestaciones es la del periodo vigente (rehire reinicia; el periodo previo se liquidó en su momento — coherente con D-13 de recontratación) (§17.9).

### Dependencias

- **Módulo de retiro definitivo** (existente, mergeado) — ancla de la liquidación real y gancho de reversión.
- **Compensación por plaza** (existente) — salario base, tasas y topes; **calidad de datos**: `SALARIO_BASE` configurado por plaza.
- **Tramos de Renta** (existente) — configuración por tenant en producción (R-06).
- **Catálogos de motivo de retiro** (existentes) — reutilización directa.
- **Infraestructura de reporting/export, auditoría, IAM y catálogos** (existentes).
- **Ratificación de D-01…D-20 y P-01…P-03 — ✅ completada (2026-07-04).** Único pendiente bloqueante para el **build**: los **casos dorados** del contador (verifican valores de P-02).
- **(Futuras)** módulo de vacaciones (saldos), notificaciones, boleta PDF, contabilidad.

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-04)

Las decisiones **D-01…D-20 fueron ratificadas por el negocio el 2026-07-04** (lo no modificado quedó según la recomendación del analista — tabla de decisiones al inicio). Respuestas a las dieciséis preguntas de la v1.0:

1. **(D-13) Reservas** — **Ambas cosas, redefinidas:** los **aportes patronales** son ISSS, AFP e **INCAF** (⚠️ INSAFORP **pasó a llamarse INCAF**), con la Renta retenida presente en esa sección como remisión (P-02); y la **"reserva" es la provisión contable**: "reservar el dinero de la liquidación dentro del centro de costo". RF-010 quedó re-especificado con ambos bloques + resumen.
2. **(D-09) Salario mínimo / valor de salario máximo** — **Confirmado: son parámetros de topes legales.** El "salario máximo" **es tomado del salario del empleado**: el sistema calcula `min(salario de la plaza, tope legal)` por concepto; el salario mínimo se lee de la **ficha del empleado** (§17.16).
3. **(RN-10) Matriz de afectación** — **Confirmada** ("dentro de límites legales") y **endurecida: el exceso gravable lo controla el sistema** (no queda al override del contador) — reglas de exención parametrizadas por concepto, excedente sumado automáticamente a la base de Renta (RN-009.4).
4. **(RF-008) Vacación** — **"Según el último registro"**: la fuente definitiva será el **futuro módulo de vacaciones** (registrará fecha de pago y fecha de goce). Fase 1: proporcional desde el último aniversario con días editables; punto de integración declarado.
5. **(RF-008) Aguinaldo** — **Confirmado el periodo del 12 de diciembre al 11 de diciembre** (tramos 15/19/21 sin objeción; quedan parametrizados).
6. **(RF-008) Renuncia voluntaria** — **Ni advertir ni bloquear: registrar con valor 0.** "Se debe poder registrar la liquidación y lo que no le corresponda quedará con valor cero por no cumplir la ley" — principio generalizado a todo concepto legalmente no cumplido (RN-008.4).
7. **(D-12) Renta** — **La tabla por ley al 2026**: retención sugerida con la tabla oficial vigente 2026 (cargarla por tenant — checklist de despliegue), override disponible.
8. **(D-10) Multi-plaza** — **LIQUIDACIÓN POR PLAZA** (cambia la propuesta consolidada): cada plaza cerrada por el retiro se liquida por separado, con su salario, sus conceptos y su centro de costo (D-10/D-16 ajustadas; deriva P-01).
9. **(D-11) Antigüedad con recontratación** — **Aceptada la propuesta:** solo el periodo vigente; "el anterior se liquidó al salir".
10. **(D-15) Aprobación / estado `PAGADA`** — **Emisión directa; las aprobaciones a futuro** (Fase 2).
11. **(D-17) Reversión con `EMITIDA`** — **Aceptada la propuesta:** bloquear hasta anulación manual.
12. **(D-19) Formato de exportación** — **El PDF se requiere en Fase 1**; el Excel sin plantilla institucional (layout estándar). D-19 ampliada + mandato de reutilización de servicios/abstracciones.
13. **(D-20) Autoservicio del empleado** — **Fase 2.**
14. **(D-05) Conversión escenario → real / comparación** — **Fase 2.**
15. **(R-11) Fallecimiento (beneficiario receptor)** — **Fuera de Fase 1.**
16. **(RF-011) Salario mínimo** — **"Debe estar en la ficha del empleado; de ahí se toma el salario con el que se calculará"**: nuevo campo en el perfil de empleo (refleja el sector del empleado), snapshot + override en la liquidación. El catálogo por sector/vigencia queda como evolución que alimente ese campo.

### Preguntas derivadas de la ratificación — P-01…P-03 **RESUELTAS (2026-07-04)**

- **P-01 — Antigüedad de la liquidación por plaza** — **Ratificado: "desde el StartDate"** de la asignación liquidada (tramos de aguinaldo y años de indemnización/renuncia por plaza; la principal normalmente coincide con `HireDate`). D-11 queda acotada así para el contexto por-plaza.
- **P-02 — Parámetros contables finos** — **Ratificada la recomendación del analista:** (a) exención de Renta del aguinaldo hasta `2 × salario mínimo mensual`; (b) **INCAF** 1% sobre la misma base afecta de ISSS con su tope; (c) la **Renta** en la sección patronal es **remisión informativa** (no costo adicional); (d) **provisión = Total Ingresos + Total Pagos Patronales**. Los **valores** (no las fórmulas) se verifican con los casos dorados del contador.
- **P-03 — Conceptos a nivel empleado (sin plaza)** — **Ratificada la recomendación:** se sugieren en la liquidación de la **plaza principal** (sin prorrateo).

## 18. Recomendaciones del Analista de Negocio

1. **Ratificación completada (2026-07-04) — el pendiente bloqueante son los "casos dorados":** pedir al negocio 3-5 liquidaciones reales históricas calculadas por el contador (una por familia de motivo), incluyendo **un caso multi-plaza (valida P-01), uno con exceso gravable (valida P-02) y uno de renuncia < 2 años (valor 0)**. Son a la vez la validación de las fórmulas, los tests unitarios del motor y el criterio de aceptación №2. **Sin casos dorados no debería arrancar el build** — el riesgo del módulo no es técnico, es de exactitud legal.
2. **Construir en dos olas dentro de Fase 1, motor primero:**
   - **Ola 1 — Motor + Escenario:** catálogos y seeds (RF-015), campo de salario mínimo en la ficha + parámetros (RF-011), motor completo en 5 secciones (RF-008…RF-010) y el escenario (RF-012…RF-014) con sus exports (Excel + boleta PDF — necesarios para el ciclo de validación con el contador). El escenario ejercita el 100% de la matemática **sin ningún efecto colateral**: permite validar fórmulas con el negocio en ambiente real antes de tocar el flujo oficial. Es el des-riesgo más barato posible.
   - **Ola 2 — Liquidación real:** ancla al retiro por plaza (RF-001), ciclo y emisión (RF-004/005), bandeja + export (RF-006/007), permisos (RF-016), gancho de reversión (RF-017) y auditoría (RF-018).
3. **Reutilizar agresivamente lo probado:** catálogos de motivo del retiro (D-04 — no duplicar), plantilla de entidad/ciclo/bandeja/export de constancias-retiros, patrón de snapshot de solicitante (D-02 de retiro), pipeline de documentos QuestPDF para la boleta, y el patrón de módulo de reglas puro para el motor (`Settlements.Rules`) — 100% testeable sin infraestructura. Es además mandato ratificado de D-19: lo que falte se construye **reutilizable**.
4. **Tratar los parámetros legales como datos, no como código:** salario mínimo en la **ficha del empleado** (ratificado §17.16) + snapshot por liquidación + defaults editables; el catálogo por sector/vigencia que alimente las fichas es una evolución natural, no un prerrequisito. Así el ajuste anual del salario mínimo es una edición de datos, no un release.
5. **No tocar la planilla externa y decirlo temprano:** la bitácora `PersonnelFilePayrollTransaction` es de solo-sincronización (trampa documentada). Alinear con el cliente desde la ratificación que este módulo **calcula y documenta**; el pago vive fuera (R-04).
6. **Cerrar los dos ciclos declarados:** el gancho de reversión (RF-017) salda la D-14 de retiro, y la señal de cierre para recontratación (D-18, Fase 2) salda la D-17 de rehire. Documentarlo en la ratificación da coherencia de roadmap: retiro → entrevista → liquidación → reversión/recontratación quedan conectados.
7. **Checklist de despliegue con datos:** tramos de Renta **tabla oficial 2026** por tenant (R-06), `SALARIO_BASE` por plaza en empleados próximos a liquidar (R-05) y **salario mínimo cargado en las fichas** de los empleados (R-08). El módulo es tan bueno como la configuración que lo alimenta.
8. **Siguiente paso:** con D-01…D-20 **ratificadas (2026-07-04)**, elaborar `docs/technical/plan-tecnico-liquidacion-empleado.md` con el desglose en PRs (patrón de los planes hermanos: catálogos + campo de ficha → dominio + motor con tests dorados → endpoints escenario → endpoints liquidación → bandeja/exports (Excel + boleta PDF reutilizable) → gancho de reversión → guía frontend), incorporando los endurecimientos de la ratificación (por-plaza, solicitante RRHH, valor 0, exceso gravable, INCAF, ficha) y cerrando P-01…P-03 con el contador en el arranque.
