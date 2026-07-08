# Análisis de negocio — Vacaciones e Incapacidades (empleado)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | Vacaciones e incapacidades: incapacidades (+ catálogos), lactancia, fondo de vacaciones, plan anual y solicitudes/devolución |
| **Fecha** | 2026-07-04 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código |
| **Estado** | **RATIFICADO por el negocio (2026-07-04)** — respuestas P-01…P-18 incorporadas; decisiones finales D-01…D-27; parámetros legales verificados en Anexo A |
| **Naturaleza del módulo** | Greenfield (4 submódulos) con costuras ya declaradas en el código; incluye un entregable transversal nuevo (calendario de asuetos) |
| **Documentos hermanos** | [`docs/technical/plan-tecnico-vacaciones-incapacidades.md`](../technical/plan-tecnico-vacaciones-incapacidades.md) (**escrito 2026-07-04**) · `docs/technical/guia-integracion-frontend-vacaciones-incapacidades.md` (pendiente — se entrega en PR-10) |
| **Documentos relacionados** | `analisis-liquidacion-empleado.md` (G-04/§17.4: goce de vacaciones) · `analisis-sustitucion-autorizaciones-empleado.md` (cobertura durante ausencias) · `analisis-reclamos-seguro-medico-empleado.md` (plantilla autogestión + adjuntos) · `analisis-ayuda-economica-empleado.md` (plantilla estado híbrido + anti-autoaprobación) |

---

## Contexto del cambio (requerimiento original)

El levantamiento solicita un módulo de **"Vacaciones e incapacidades"** que permita el ingreso de vacaciones e incapacidades de los empleados de la institución, incluyendo permisos por lactancia y la construcción de un calendario anual de vacaciones. Se compone de cuatro bloques:

1. **Incapacidades** — registro de incapacidades del empleado "para realizar los respectivos cálculos de prestaciones o descuentos en la planilla correspondiente, con su respectivo asiento en el expediente". Campos principales de la nueva incapacidad: empleado que solicita, empleado incapacitado, riesgo (catálogo), clínica médica (catálogo), tipo, planilla, periodo de planilla (catálogo), fecha inicio, fecha final, días incapacitado. Los días de descuento o subsidio "se deberán configurar a partir de los riesgos de incapacidad (según el ISSS)". Debe permitir **extender una incapacidad en caso de existir prórroga**. El **flujo de autorizaciones en línea queda explícitamente para la siguiente fase**.
2. **Catálogos** — clínicas médicas (descripción, especialidad; del ISSS, públicas o privadas), riesgos de incapacidad (séptimo, sábado, asueto, usa jornada, incapacidad indefinida, prórroga, utiliza fondo, con/sin subsidio, más parámetros del riesgo) y tipos de incapacidad (descripción, tipo de descuento, tipo de ingreso, aplica a accidente de trabajo).
3. **Administración de lactancia** — periodos de lactancia posteriores al postnatal (empleado que solicita, empleado destino, nombre de incapacidad —catálogo—, fecha inicial, fecha final) con **horarios de lactancia** (fecha inicial, fecha final, cantidad de permisos, tiempo en minutos).
4. **Fondo y plan anual de vacaciones** — generación de periodos de vacaciones por empleado (año inicial, genera días de goce, usar fecha de aniversario, usar parametrización, días a otorgar); consulta del detalle del fondo (gozados, solicitados, pendientes) con autogestión según Reglamento Interno (periodos de 21, 7, 11 o 10 días); plan anual multi-empleado con parametrización de inicio/fin en asueto/séptimo; solicitud de vacaciones con verificación de fondo; y **devolución de vacaciones** (anular una solicitud autorizada reintegrando los días al periodo de origen).

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **El módulo es greenfield**: no existe ningún código de vacaciones, incapacidades, lactancia ni ausencias en el backend. Verificado exhaustivamente (entidades, controladores, endpoints de `openapi.yaml`, migraciones).
2. **Pero el código ya declaró las costuras para este módulo**: el perfil del empleado expone `vacationDaysAvailable` y `disabilityDaysAvailable` (hoy siempre `null`, comentados como "propiedad del futuro módulo de vacaciones/incapacidades"), y el análisis de liquidación ratificó que "la fuente definitiva del goce será el último registro del futuro módulo de vacaciones" (G-04/§17.4 de `analisis-liquidacion-empleado.md`). Este módulo debe **poblar esas costuras**, no inventar campos paralelos.
3. **Tres brechas estructurales** que el levantamiento asume y el sistema NO tiene: (a) **no existe motor de planilla** — los "descuentos en planilla" no pueden aplicarse automáticamente; el módulo calculará y expondrá el **desglose de días y montos referenciales** (subsidio/descuento/patrono, con base salarial snapshot) como insumo exportable para la planilla externa; (b) **no existe calendario de asuetos** — imprescindible para los flags séptimo/sábado/asueto del riesgo y para las validaciones del plan anual; se crea como entregable transversal; (c) **no existe modelo de jornada** — solo `WorkdayCode` texto libre en la plaza; la Fase 1 usará convenciones de calendario parametrizables.
4. **El flujo de autorizaciones en línea queda para Fase 2 por decisión del propio levantamiento.** La Fase 1 opera con aprobación directa de RRHH usando el patrón de **estado híbrido + transiciones custodiadas + anti-autoaprobación** ya probado en ayuda económica, de modo que el flujo multi-nivel futuro no rompa el contrato.
5. Las **ambigüedades del levantamiento quedaron resueltas en la ratificación del 2026-07-04**: fondo **por empleado** (la segunda plaza suele ser contrato por servicios), el módulo **sí calcula montos referenciales** además de días, devolución **parcial en Fase 1**, **auto-registro de incapacidades** en autogestión (con constancia obligatoria por defecto y confirmación de RRHH), riesgos y tipos de incapacidad **editables por cada empresa** (plantilla SV precargada), nuevo **maestro de periodos de planilla** con fechas de corte por quincena, día de descanso semanal **por empleado**, y fracciones del Reglamento Interno **descartadas** (vacación de ley + días adicionales como beneficio de empresa). Decisiones finales D-01…D-27; parámetros legales verificados en el **Anexo A**.

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Dónde | Uso en este módulo |
|---|---|---|
| Saldos en perfil: `VacationDaysAvailable` / `DisabilityDaysAvailable` (hoy `null`) | `Features/PersonnelFiles/Employment/EmployeeProfiles.cs:39-40`; hardcodeados a `null` en `PersonnelFileEmployeeRepository.cs:2213-2227`; expuestos en `openapi.yaml` | El módulo los **pobla** (RF-018) |
| Liquidación: `VACACION_PROPORCIONAL` (15 días, 30% prima, divisor 365, default `DaysSinceAnniversary`, unidades editables `UnitsOrFactorUsed` + `IsOverridden`) | `Settlements/SettlementCalculation.Rules.cs`; `PersonnelFileSettlement.cs` | Consumidor futuro del fondo: los **días pendientes** del fondo refinan la sugerencia (RF-019) |
| `PAY_PERIOD_CATALOG` (`MENSUAL` -9740, `QUINCENAL` -9741, `SEMANAL` -9742) | `GlobalCatalogSeedData.cs` | "Periodo de planilla" como **tipo** (la instancia se imputa con año+mes, patrón fuera-de-nómina) |
| Acciones de personal: `ACTION_TYPE_CATALOG` / `ACTION_STATUS_CATALOG` + entidad `PersonnelFilePersonnelAction` (p. ej. `BAJA` -9482, `REVERSION_BAJA` -9483) | `GlobalCatalogSeedData.cs:748-749`; `PersonnelFileEmployee.cs` | **Asiento en expediente** automático con códigos nuevos (RF-012) |
| Autogestión: `PersonnelFile.LinkedUserPublicId` + gates `LoadForCreateOwnOrManage…Async` / lectura `View… OR isSelf` | `Common/PersonnelFileEmployeeHandlerBases.cs` | Solicitud de vacaciones y consulta del fondo en autogestión (RF-017, RF-022) |
| Estado híbrido + anti-autoaprobación (`EconomicAidRequestStatuses`, `Resolve/Disburse/Cancel` custodiados, `SelfApprovalForbidden` 403) | `PersonnelFileEmployee.cs:1678-1880`; `EconomicAidRequests.Handlers.cs:371-380` | Ciclo de vida de solicitudes de vacaciones e incapacidades (RF-023) |
| Stack de adjuntos: `FilePurpose` + upload-session SAS + `PATCH complete` + gate `Storage:Purposes` + read-url | `Files/*`; plantilla `MedicalClaimDocuments.Handlers.cs` | Constancias ISSS de incapacidad (RF-011) |
| Bandeja + exportación tabular (query paginada con `StatusCounts`, export xlsx/csv/json con rate limiting, filas con propiedades en español) | `SettlementsBandeja.cs`, `ReportExportFileWriter.cs`, `SettlementsReportingController.cs` | Bandejas de incapacidades y solicitudes (RF-013, RF-025) |
| `CompanyPreference` tipada (columnas + setters, p. ej. `MinimumSeniorityMonthsForEconomicAid`) | `Domain/Preferences/CompanyPreference.cs` | Parametrización de vacaciones (RF-005) |
| Plazas: `PersonnelFileEmploymentAssignment` (`StartDate`/`EndDate`, `IsPrimary`, `WorkdayCode` texto libre, `PayrollTypeCode`) y perfil (`HireDate`, `EmploymentStatusCode` con `LICENCIA`/`INCAPACIDAD` ya sembrados, `MinimumMonthlyWage`) | `PersonnelFileEmployee.cs` | Ancla de antigüedad por plaza (aniversario) y estado del empleado |
| Catálogo de motivos de sustitución (`VACACIONES`, `INCAPACIDAD`, `PERMISO`, `LICENCIA`…) | `GlobalCatalogSeedData.cs:471-476` | Correlación funcional (quién cubre durante la ausencia); sin acople técnico en Fase 1 |
| `DocumentModel` + QuestPDF (boleta PDF formato-agnóstica) | `Abstractions/Reports/Documents/DocumentModel.cs`, `QuestPdfDocumentRenderer.cs` | Opcional Fase 2 (comprobante de solicitud) |

### Costuras ya declaradas esperando este módulo

- Comentario en `EmployeeProfiles.cs:22-24`: *"vacation/disability balances are read-only here and owned by the future vacations/incapacities module (null until that module computes them)"*.
- `analisis-liquidacion-empleado.md` (G-04, R-02, §17.4, RF-008): el default `DaysSinceAnniversary` de la vacación proporcional es provisional; *"la fuente definitiva será 'el último registro' (fecha de pago y fecha de goce) del futuro módulo de vacaciones"*.
- Histórico: existió una columna `vacation_configuration_json` en el perfil y fue **eliminada deliberadamente** en la migración `20260621191139_SimplifyEmployeeProfileAndAssignmentContract` al adelgazar el perfil. Esa configuración renace aquí como preferencias de empresa + periodos del fondo (no como JSON en el perfil).

### Lo que NO existe (verificado exhaustivamente)

- Ningún módulo/entidad/endpoint de **vacaciones, incapacidades, lactancia, permisos o ausencias**.
- Ningún **calendario de asuetos/feriados** (la única fecha codificada del sistema es la ventana de aguinaldo 12-dic→11-dic en `SettlementCalculation.Rules.cs:614`).
- Ningún modelo de **jornada/horarios/turnos**: `WorkdayCode` es texto libre (máx. 80) en la plaza, sin catálogo; el `Shift` existente es jornada de **estudios** del currículo, no laboral.
- Ningún **motor de planilla**: `PersonnelFilePayrollTransaction` es un ledger **inmutable de sincronización externa** (solo `Create` + `SetActive`); los conceptos de compensación y los tramos de Renta declaran textualmente que el cálculo lo hará "el futuro módulo de planilla".
- Ninguna **instancia de periodo de planilla** (solo el tipo mensual/quincenal/semanal del catálogo).
- Ningún **motor genérico de flujos de aprobación**: cada módulo resuelve su ciclo de vida con métodos de dominio custodiados + gates en handlers.
- Ningún concepto **"riesgo"** en el código → el catálogo de riesgos de incapacidad no colisiona con nada existente.

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha detectada | Propuesta de resolución |
|---|---|---|
| G-01 | El levantamiento asume "cálculos de prestaciones o descuentos **en la planilla**", pero no existe motor de planilla | El módulo calcula y persiste el **desglose de días y montos referenciales** (subsidiados / con descuento / a cargo del patrono, con base salarial snapshot) y lo expone vía consulta + exportación para la planilla externa; la aplicación final del dinero es externa y no se escriben transacciones de planilla (D-02, D-21 ratificada: montos SÍ) |
| G-02 | Los flags séptimo/sábado/asueto del riesgo y las validaciones del plan anual requieren un **calendario de asuetos** que no existe | Nueva entidad transversal **Asueto institucional** por empresa (fechas concretas por año, CRUD admin) (D-03, RF-004) |
| G-03 | "Usa jornada" y "séptimo" requieren modelo de horarios; solo existe `WorkdayCode` texto libre | Fase 1 sin motor de jornadas: conteo sobre calendario con el **día de descanso semanal de cada empleado** (`restDayOfWeek` en la plaza, default empresa domingo — D-26, P-10); el flag `usaJornada` se almacena para el futuro (D-04) |
| G-04 | "Planilla" y "Periodo de planilla (catálogo)" no tienen representación: no hay instancia de planilla | **Nuevo maestro por empresa de periodos de planilla** (instancias con tipo, año, número, etiqueta y fechas de corte — p. ej. primera quincena cortando el día 10…15 según defina la empresa); la incapacidad referencia la instancia + `payrollTypeCode` (D-23, P-06) |
| G-05 | Semántica ambigua en el levantamiento: "utiliza fondo", "genera días de goce", "es prórroga", "tipo de descuento/ingreso" | **Resuelta**: "utiliza fondo" = provisión financiera institucional para proyectar el pago (P-01, D-25); "genera días de goce"=no → periodo de control/pago monetario que leen RRHH y Finanzas sin ejecutar acciones (P-02); tipo de descuento/ingreso = **texto informativo** por ahora (P-07) |
| G-06 | Multi-plaza: el sistema ratificó antigüedad **por plaza** (liquidación P-01), pero el levantamiento habla de vacaciones "del empleado" | **Ratificado: fondo POR EMPLEADO** (P-03: la segunda plaza generalmente es contrato por servicios, no laboral); ancla de aniversario = `StartDate` de la **plaza principal** (P-04, D-05/D-06) |
| G-07 | No existe motor de aprobaciones y el levantamiento difiere el flujo en línea a la siguiente fase | Fase 1: aprobación directa RRHH con estados híbridos + anti-autoaprobación; Fase 2: flujo multi-nivel sin romper contrato (D-01, D-16) |
| G-08 | Los saldos del perfil (`vacationDaysAvailable`/`disabilityDaysAvailable`) están en `null` esperando dueño | Este módulo los calcula y los expone; `disabilityDaysAvailable` = días restantes del tope patronal anual (ley/política + beneficio) — ratificado (D-20/D-27, P-15) |
| G-09 | La liquidación sugiere días de vacación con `DaysSinceAnniversary`, sin datos reales de goce | Nuevo query interno: **días pendientes del fondo por plaza** como fuente de la sugerencia (RF-019) |
| G-10 | El levantamiento modela la lactancia como "nombre de incapacidad (catálogo)" | Entidad propia **Periodo de lactancia** (con detalle de horarios) referenciando un tipo sembrado `LACTANCIA`; no es una incapacidad con descuento (D-12) |
| G-11 | Ningún módulo gestiona **solapes** entre ausencias (vacación×vacación, incapacidad×incapacidad, vacación×incapacidad) | Reglas de solape nuevas del módulo (RN-14…RN-16); ratificado P-12: **devolución (parcial) manual previa**, sin interrupción automática en Fase 1 |
| G-12 | Fracciones del Reglamento Interno (21, 7, 11 o 10 días) sin regla verificable | **Descartada** (P-05): aplican las vacaciones de ley (15 días + 30 %, Art. 177 CT) y la empresa puede otorgar **días adicionales como beneficio** (D-24); defaults legales de fechas según Art. 178 CT (no iniciar en asueto ni descanso) |

---

## Decisiones ratificadas por el negocio (2026-07-04) — D-01…D-27

> ✅ **TODAS RATIFICADAS** el 2026-07-04 con las respuestas P-01…P-18 (§17). Las marcadas **(ajustada)** difieren de la propuesta original del borrador; D-23…D-27 nacen de las respuestas.

| # | Tema | Decisión ratificada |
|---|---|---|
| D-01 | Fases | Fase 1 sin flujo de autorizaciones en línea (lo difiere el propio levantamiento): RRHH registra/aprueba directamente; estados híbridos preparados para el flujo multi-nivel de Fase 2 |
| D-02 | Planilla | El módulo produce **días y montos referenciales** (desglose subsidio/descuento/patrono con base salarial snapshot + exportaciones); la aplicación monetaria final la hace la planilla externa y se reconciliará con el futuro módulo de planilla. No se escribe `PersonnelFilePayrollTransaction` desde este módulo (es el ledger de sincronización del sistema externo) |
| D-03 | Asuetos | Nueva entidad **Asueto institucional** por empresa: fechas concretas por año (maneja feriados móviles), CRUD de administrador, ámbito NACIONAL/LOCAL/INSTITUCIONAL |
| D-04 | Jornada/séptimo **(ajustada)** | Sin motor de horarios en Fase 1. El séptimo/descanso **depende del descanso semanal de cada empleado** (P-10): campo `restDayOfWeek` en la plaza (D-26), con default de empresa (domingo) cuando no esté definido; sábado contable según flag del riesgo. `usaJornada` se persiste pero no activa lógica en Fase 1 |
| D-05 | Granularidad del fondo **(ajustada)** | Periodos de vacaciones **POR EMPLEADO** (P-03): el empleado tiene vacaciones por una plaza laboral; la segunda plaza generalmente es contrato por servicios. Unicidad `(expediente, año)`; el fondo no tiene dimensión de plaza |
| D-06 | Aniversario | Con "utilizar fecha de aniversario" = sí, el periodo anual corre de aniversario a aniversario del `StartDate` de la **plaza principal** (P-04: "aniversario startdate"); si no, año calendario (1-ene a 31-dic) |
| D-07 | Riesgos de incapacidad **(ajustada)** | **Maestro por empresa, editable** (P-09), con flags (séptimo, sábado, asueto, usa jornada, indefinida, aplica prórroga, utiliza fondo, con subsidio) + **parámetros por rangos de días** (desde–hasta, % de subsidio/descuento, pagador: ISSS / EMPRESA / SIN_PAGO). Plantilla SV precargada al aprovisionar la empresa (Anexo A.2) |
| D-08 | Tipos de incapacidad **(ajustada)** | **Maestro por empresa, editable** (coherente con P-09 y el levantamiento: "según lo establezca la institución"): descripción + **tipo de descuento y tipo de ingreso como texto informativo** por ahora (P-07) + flag "aplica a accidente de trabajo". Plantilla SV precargada (incluye `LACTANCIA`) |
| D-09 | Clínicas médicas **(ajustada)** | **Maestro por empresa** (CRUD del administrador): **solo la descripción es obligatoria** (P-08, sin código); especialidad y sector (ISSS/PÚBLICA/PRIVADA) opcionales. **Sin semilla — el maestro inicia vacío** (confirmado 2026-07-04: no existe catálogo de clínicas), por lo que la **clínica es opcional en la incapacidad** |
| D-10 | Incapacidad | Registro **por empleado** (persona incapacitada, con referencia opcional a plaza para imputación) con estado híbrido `REGISTRADA → ANULADA`. **Prórroga** = registro nuevo enlazado (`extendsIncapacityPublicId`), permitido solo si el riesgo aplica prórroga, con continuidad de fechas; el conteo de días para los parámetros del riesgo **continúa a través de la cadena** |
| D-11 | Incapacidad indefinida | `endDate` nula permitida solo si el riesgo lo permite; cierre posterior vía PATCH dedicado que fija fecha final y recalcula |
| D-12 | Lactancia | Entidad propia (no incapacidad): periodo con fechas + **horarios** (rangos, cantidad de permisos/día, minutos por permiso). Registral, sin descuento en planilla; genera asiento |
| D-13 | Consumo del fondo | La solicitud aprobada consume el fondo mediante **asignaciones explícitas a periodos** (FIFO del periodo más antiguo con saldo, editable por RRHH); la devolución reintegra exactamente a los periodos de origen |
| D-14 | Devolución **(ajustada)** | Fase 1 incluye devolución **total Y parcial** (P-13). Devoluciones como registros hijos de la solicitud (días, fecha, motivo); reintegro automático a los periodos de origen (reversa LIFO de las asignaciones, editable). Total → `DEVUELTA`; parcial → `DEVUELTA_PARCIAL` (días devueltos acumulados ≤ consumidos) |
| D-15 | Plan anual | Programación **indicativa** multi-empleado (cabecera + detalle multi-tramo) que NO consume fondo; valida disponibilidad y fechas como **advertencia**; la solicitud puede referenciar la línea del plan |
| D-16 | Estados híbridos **(ajustada)** | `VACATION_REQUEST_STATUS_CATALOG`: `SOLICITADA, APROBADA, RECHAZADA, ANULADA, DEVUELTA_PARCIAL, DEVUELTA`; `INCAPACITY_STATUS_CATALOG`: `EN_REVISION` (solo origen autoservicio), `REGISTRADA, ANULADA`. Constantes canónicas + transiciones custodiadas en dominio + validación por catálogo en handlers (patrón ayuda económica) |
| D-17 | Permisos | `PersonnelFiles.ViewIncapacities` / `ManageIncapacities` (incluye lactancia) y `PersonnelFiles.ViewVacations` / `ManageVacations` (incluye fondo y plan). Fase 2: `AuthorizeVacations` / `AuthorizeIncapacities` con `RequireAssertion` que excluye `Admin` (patrón `AuthorizeRetirement`) |
| D-18 | Autogestión Fase 1 **(ajustada)** | Empleado: crear su **solicitud de vacaciones**, **auto-registrar su incapacidad** (P-11, con constancia adjunta obligatoria por defecto; entra `EN_REVISION` hasta confirmación de RRHH) y ver su fondo, saldos y registros propios. La **lactancia** la registra solo RRHH |
| D-19 | Asiento en expediente | Automático vía `PersonnelFilePersonnelAction` con códigos nuevos de `ACTION_TYPE_CATALOG`: `INCAPACIDAD`, `PRORROGA_INCAPACIDAD`, `LACTANCIA`, `GOCE_VACACIONES`, `DEVOLUCION_VACACIONES` (IDs de semilla ≤ -9850) |
| D-20 | Saldos del perfil **(ajustada)** | `vacationDaysAvailable` = suma de días pendientes de los periodos activos con goce; `disabilityDaysAvailable` = **días restantes del tope patronal anual** (ley/política + beneficio adicional, D-27) del año en curso (P-15). El detalle completo (acumulado del año, tope, beneficio, restante) se expone en una consulta de saldos de incapacidad. Calculados en la proyección del perfil, sin columnas nuevas |
| D-21 | Montos **(ajustada — invertida)** | **SÍ se calculan en Fase 1** (P-14): el motor persiste la base salarial mensual/diaria (snapshot) y los montos por tramo (subsidio ISSS $, descuento $, patrono $) como **referenciales**; el fondo expone el valor proyectado de vacaciones (días pendientes × salario diario × 1.30). La aplicación final sigue siendo de la planilla externa y se complementará cuando exista el módulo de planilla |
| D-22 | Adjuntos **(ajustada)** | Nuevo `FilePurpose.IncapacityDocument` (constancia ISSS y similares) con entrada `Storage:Purposes` en appsettings BASE y contenedor pre-aprovisionado; constancia **obligatoria por defecto**, parametrizable por empresa (P-17) |
| D-23 | Periodos de planilla **(nueva)** | Maestro por empresa de **instancias** de periodo de planilla (P-06): tipo (`PAY_PERIOD_CATALOG`), año, número, etiqueta y **fechas de corte** (p. ej. la primera quincena puede terminar el día 10, 11, 12, 13, 14 o 15 según defina la empresa). **Confirmado 2026-07-04: el calendario no es estático, cada empresa carga el suyo — sin plantilla global.** La incapacidad referencia la instancia; CRUD del administrador |
| D-24 | Vacación de ley + beneficio **(nueva)** | Sin fracciones de Reglamento Interno (P-05). El periodo otorga **días de ley** (default 15, Art. 177 CT) + **días adicionales de beneficio** opcionales por empresa; ambos consumibles por solicitudes. Defaults legales de fechas: no iniciar en asueto ni en día de descanso (Art. 178 CT), parametrizable |
| D-25 | Provisión financiera **(nueva)** | "Utiliza fondo" = el registro alimenta la **provisión financiera institucional** (P-01): consultas y exportaciones exponen el valor proyectado (vacaciones: días pendientes × salario diario × 1.30; incapacidades con `utilizaFondo`: montos del desglose) para **Finanzas/Contabilidad, que solo lee, no ejecuta acciones** (P-02) |
| D-26 | Descanso semanal por empleado **(nueva)** | Campo `restDayOfWeek` (día de descanso semanal) en la **plaza** (P-10), con default de empresa (domingo) cuando no esté definido. Lo usan el motor de incapacidades (séptimo) y las validaciones de vacaciones (no iniciar en descanso) |
| D-27 | Tope patronal de incapacidad **(nueva)** | Preferencias `employerCoveredIncapacityDaysPerYear` (default **9**, política del cliente) y `additionalIncapacityBenefitDaysPerYear` (default 0, beneficio interno P-15). La ley fija 3 días por evento a cargo del patrono al 75 % **sin tope anual** (Anexo A.1); el tope anual de 9 es política interna parametrizable. El motor consume el tope con los días de pagador EMPRESA; agotado el tope, esos días pasan a descuento (SIN_PAGO) |

---

## 1. Resumen del producto o requerimiento

Se construirá el módulo de **Vacaciones e Incapacidades** de CLARIHR para instituciones salvadoreñas: la gestión integral de las ausencias remuneradas/subsidiadas del empleado dentro del expediente de personal.

**Qué se construye.** Cuatro capacidades sobre el expediente del empleado:

1. **Incapacidades**: registro de incapacidades médicas (enfermedad común, accidente de trabajo, maternidad, etc.) con catálogos de clínicas, riesgos y tipos **editables por la empresa**; un **motor de cálculo de días y montos referenciales** que, a partir de los parámetros del riesgo (según normativa ISSS), el calendario de asuetos, el día de descanso del empleado y su base salarial, determina cuántos días (y qué montos) son subsidiados, cuántos generan descuento y cuántos asume el patrono (con tope anual parametrizable); soporte de **prórrogas** encadenadas e **incapacidades indefinidas**; **auto-registro del empleado** en autogestión con confirmación de RRHH; adjuntos (constancia ISSS, obligatoria por defecto) y asiento automático en el expediente.
2. **Lactancia**: registro del periodo de lactancia posterior al postnatal con sus **horarios diarios** (cantidad de permisos y minutos por permiso).
3. **Fondo de vacaciones**: generación (individual y masiva) de los **periodos anuales de vacaciones por empleado** con días de ley y días adicionales de beneficio, consulta del detalle (otorgados, gozados, solicitados, devueltos, pendientes) y **valor proyectado para la provisión financiera** — el fondo es la fuente única que consumen las solicitudes.
4. **Plan anual y solicitudes**: plan anual indicativo multi-empleado y multi-tramo (el "calendario anual de vacaciones"), **solicitud de vacaciones** (autogestión del empleado o registro de RRHH) con verificación de días disponibles y validaciones de fechas contra asuetos/días de descanso, aprobación/rechazo por RRHH (Fase 1) y **devolución total o parcial** (reintegro de días al fondo desde una solicitud aprobada).

**Problema que resuelve.** Hoy CLARIHR no tiene ninguna gestión de ausencias: los saldos de vacaciones e incapacidades del perfil están vacíos, la liquidación estima el goce por aniversario sin datos reales, y el control de vacaciones/incapacidades se lleva fuera del sistema (papel/Excel), sin trazabilidad en el expediente ni insumos confiables para la planilla.

**Objetivo principal.** Que toda ausencia del empleado quede registrada, calculada y trazada en el expediente, que el fondo de vacaciones sea la fuente única de verdad de los días disponibles, y que la planilla externa reciba insumos exactos (días de subsidio/descuento) en lugar de cálculos manuales.

---

## 2. Objetivos del negocio

1. **Control normativo de incapacidades**: aplicar de forma consistente los parámetros del ISSS (días subsidiados, porcentajes, quién paga cada tramo) eliminando el cálculo manual y sus errores en planilla.
2. **Fuente única de verdad del derecho a vacaciones**: fondo por periodo con saldo auditable (otorgado − gozado + devuelto), consumido exclusivamente por solicitudes aprobadas, con **valor proyectado para la provisión financiera** que consultan Finanzas/Contabilidad (solo lectura, P-01/P-02).
3. **Autogestión**: que el empleado consulte su saldo y solicite vacaciones en línea, reduciendo carga operativa de RRHH; base para el flujo de autorizaciones de Fase 2.
4. **Planificación**: plan anual de vacaciones por unidad/empresa para anticipar coberturas (se correlaciona con el módulo de sustitución de autorizaciones ya existente).
5. **Expediente completo**: cada incapacidad, lactancia, goce y devolución deja asiento automático en las acciones de personal del expediente.
6. **Insumos exactos para planilla y liquidación**: exportaciones tabulares con **días y montos referenciales** para la planilla externa, y días pendientes reales para la vacación proporcional de la liquidación (sustituyendo el estimado por aniversario).
7. **Cumplimiento y auditoría**: trazabilidad de quién solicitó, quién decidió y cuándo, con anti-autoaprobación y datos de salud protegidos por permisos.

---

## 3. Alcance funcional

### Fase 1 — MVP (este análisis)

- **Catálogos y configuración**: clínicas médicas (maestro por empresa, solo descripción obligatoria), riesgos de incapacidad con parámetros por rangos (maestro por empresa, plantilla SV), tipos de incapacidad (maestro por empresa, plantilla SV), **maestro de periodos de planilla** (instancias con fechas de corte), calendario institucional de asuetos, día de descanso semanal por plaza, parametrización de vacaciones y topes de incapacidad en preferencias de empresa, catálogos de estados, permisos RBAC.
- **Incapacidades**: registro por RRHH **o auto-registro del empleado** (entra en revisión hasta confirmación de RRHH) con solicitante/incapacitado, riesgo, clínica, tipo, referencia de planilla (tipo + instancia de periodo), fechas y días; motor de cálculo del desglose de **días y montos referenciales** (subsidio/descuento/patrono) según riesgo + asuetos + descanso del empleado + base salarial, con tope patronal anual; prórrogas encadenadas; incapacidad indefinida con cierre posterior; anulación; adjuntos (constancia obligatoria por defecto); asiento en expediente; bandeja de empresa + exportación; consulta en ficha, autogestión y **saldos de incapacidad** (acumulado / tope / restante).
- **Lactancia**: registro del periodo con horarios diarios; asiento; consulta.
- **Fondo de vacaciones**: generación individual y **masiva** de periodos anuales **por empleado** (aniversario de la plaza principal o año calendario; días de ley por parametrización o manuales + días de beneficio adicional); consulta del detalle por empleado (RRHH y autogestión) con **valor proyectado de provisión financiera**; saldos publicados en el perfil (`vacationDaysAvailable`/`disabilityDaysAvailable`); query interno de días pendientes para la liquidación.
- **Plan anual**: cabecera + detalle multi-empleado y multi-tramo; validaciones parametrizables de inicio/fin contra asuetos y día de descanso; edición de fechas y días.
- **Solicitudes**: creación por autogestión o RRHH con verificación de fondo; aprobación/rechazo/anulación por RRHH con anti-autoaprobación; **devolución total y parcial** con reintegro automático a los periodos de origen; bandeja de empresa + exportación; datos para vista de calendario anual.

### Fase 2 — Evoluciones (fuera de este MVP, contrato preparado)

- **Flujo de autorizaciones en línea** multi-nivel para vacaciones e incapacidades (jefatura → RRHH), con permisos `Authorize*` y notificaciones — diferido por el propio levantamiento.
- Motor de **jornadas/horarios** reales (activando `usaJornada` del riesgo).
- Boleta PDF de solicitud/incapacidad vía `DocumentModel`.
- Integración nativa con el futuro módulo de planilla (aplicación automática de descuentos/subsidios).
- Planificación por jefaturas (plan anual creado por el jefe de unidad sobre su equipo).

---

## 4. Fuera de alcance

- **Aplicación monetaria en planilla**: el módulo calcula días y **montos referenciales** (D-21 ratificada), pero el pago/descuento efectivo lo ejecuta la planilla externa (y a futuro el módulo de planilla, que reconciliará contra estos snapshots). La **compensación en dinero de vacaciones no gozadas al terminar la relación laboral ya la cubre el módulo de liquidación** (`VACACION_PROPORCIONAL`; compensarlas en dinero durante la relación está prohibido por el CT — Anexo A.1).
- **Motor de planilla** y aplicación automática de descuentos; escritura de `PersonnelFilePayrollTransaction` (ledger reservado a la sincronización del sistema externo).
- **Flujo de autorizaciones en línea** (Fase 2, por decisión del levantamiento).
- **Gestión de horarios/turnos/marcaciones** y control de asistencia.
- Integración electrónica con el **ISSS** (no existe API pública; la constancia se adjunta escaneada).
- Provisión contable del pasivo vacacional (fondo monetario).
- Notificaciones por correo y recordatorios (Fase 2).
- Permisos/licencias generales no contemplados en el levantamiento (permiso personal, duelo, matrimonio…) — el diseño de catálogos los admite a futuro, pero no se siembran ni se implementan pantallas.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador de empresa** | Configura catálogos propios (clínicas, asuetos), preferencias de vacaciones y asigna permisos |
| **Gestor de RRHH** (con `Manage*`) | Registra incapacidades, prórrogas y lactancia; genera el fondo; construye el plan anual; decide solicitudes; ejecuta devoluciones; exporta |
| **Consulta de RRHH / Auditor** (con `View*`) | Consulta bandejas, fichas, fondo y exportaciones sin poder de escritura |
| **Empleado (autogestión)** | Consulta su fondo, saldos y registros propios; crea su solicitud de vacaciones; **auto-registra su incapacidad** con constancia (queda en revisión de RRHH) |
| **Jefatura / Autorizador** | Fase 2: aprueba solicitudes de su equipo en el flujo en línea |
| **Finanzas / Contabilidad** | Consulta y exporta (solo lectura) las provisiones financieras: valor proyectado del fondo e incapacidades con "utiliza fondo"; **no ejecuta acciones** (P-02) |
| **Sistema de planilla externa** | Consume las exportaciones (días y **montos referenciales** de subsidio/descuento, goces del periodo) y aplica los montos |
| **Módulo de liquidación (interno)** | Consume los días pendientes del fondo como sugerencia de la vacación proporcional |

---

## 6. Requerimientos funcionales

> Agrupados en 5 grupos (A: configuración y catálogos · B: incapacidades · C: lactancia · D: fondo de vacaciones · E: plan anual y solicitudes). Prioridades: Alta = imprescindible Fase 1; Media = Fase 1 deseable; Baja = puede diferirse.

### Grupo A — Configuración y catálogos

### RF-001 - Catálogo de clínicas médicas (maestro por empresa)

**Descripción:** CRUD de clínicas médicas propias de la institución (ISSS, públicas o privadas) con descripción, especialidad y sector, administrado por la empresa.

**Reglas de negocio:**
- **Solo la descripción es obligatoria** (P-08, sin código), única por empresa (comparación normalizada); especialidad opcional.
- Sector opcional contra catálogo `ISSS / PUBLICA / PRIVADA`.
- Baja lógica (`isActive`); una clínica inactiva no es seleccionable en nuevas incapacidades pero se conserva en las históricas.

**Criterios de aceptación:**
- CRUD completo con concurrencia If-Match y auditoría.
- Una incapacidad nueva con clínica inactiva o inexistente → 422 con código de error bilingüe.

**Prioridad:** Alta
**Dependencias:** Ninguna (patrón de maestros por empresa existente).

### RF-002 - Catálogo de riesgos de incapacidad con parámetros de subsidio/descuento

**Descripción:** Maestro **por empresa** (editable, P-09) de riesgos de incapacidad. Cada riesgo define flags de conteo (séptimo, sábado, asueto, usa jornada), capacidades (incapacidad indefinida, aplica prórroga, utiliza fondo —provisión financiera, D-25—, con/sin subsidio) y una tabla de **parámetros por rangos de días** (día desde–hasta, porcentaje, pagador ISSS/EMPRESA/SIN_PAGO) que gobierna el cálculo.

**Reglas de negocio:**
- **Plantilla SV precargada por empresa** al aprovisionarla (`ENFERMEDAD_COMUN`, `ACCIDENTE_COMUN`, `ACCIDENTE_TRABAJO`, `ENFERMEDAD_PROFESIONAL`, `MATERNIDAD`) con los parámetros verificados del **Anexo A.2**, editable por el administrador (P-09); validación final del contador recomendada.
- Rangos de parámetros: sin solapes, contiguos desde el día 1; `dayTo` nulo = "en adelante"; porcentaje 0–100.
- Un riesgo sin subsidio no admite parámetros con pagador ISSS.
- Validación por código (`validate-by-code`) en todo registro de incapacidad.

**Criterios de aceptación:**
- CRUD por empresa (administrador) devuelve riesgos con sus parámetros; las ediciones no recalculan incapacidades históricas (snapshot).
- El motor de cálculo consume exclusivamente estos parámetros (sin números mágicos en código).

**Prioridad:** Alta
**Dependencias:** Patrón de maestros por empresa (existe); Anexo A.2 (plantilla).

### RF-003 - Catálogo de tipos de incapacidad

**Descripción:** Maestro **por empresa** (editable) de tipos de incapacidad según la institución/legislación: descripción, **tipo de descuento y tipo de ingreso como texto informativo** (P-07), y flag "aplica a accidente de trabajo".

**Reglas de negocio:**
- Plantilla SV mínima precargada por empresa, incluyendo el tipo `LACTANCIA` usado por el submódulo de lactancia (D-12).
- Tipo de descuento/ingreso: texto libre informativo que viaja en las exportaciones a planilla (sin mapeo a conceptos por ahora, P-07).

**Criterios de aceptación:**
- CRUD por empresa operativo; validación por referencia activa al registrar incapacidades.

**Prioridad:** Alta
**Dependencias:** Ninguna (patrón de maestros por empresa).

### RF-004 - Calendario institucional de asuetos

**Descripción:** CRUD por empresa de los asuetos (fechas concretas por año) con descripción y ámbito (NACIONAL/LOCAL/INSTITUCIONAL). Entregable transversal: lo consumen el motor de incapacidades, las validaciones del plan/solicitudes y, a futuro, otros módulos (tablero RRHH, planilla).

**Reglas de negocio:**
- Fecha única por empresa; fechas concretas por año (los feriados móviles —Semana Santa— se cargan cada año).
- Carga inicial asistida: plantilla de asuetos nacionales SV del Art. 190 CT (Anexo A.3) para el año en curso, editable (incluye los locales: 3 y 5 de agosto en San Salvador o el día principal de la fiesta patronal local).
- Baja lógica; los cálculos históricos no se recalculan al modificar el calendario (el desglose queda persistido en cada incapacidad).

**Criterios de aceptación:**
- Dado un asueto registrado dentro del rango de una incapacidad cuyo riesgo no cuenta asuetos, el motor lo excluye del conteo.
- Dada la preferencia "no iniciar vacaciones en asueto", una solicitud que inicia en asueto → 422.

**Prioridad:** Alta
**Dependencias:** Ninguna técnica; contenido inicial a confirmar.

### RF-005 - Parametrización de vacaciones (preferencias de empresa)

**Descripción:** Nuevas preferencias tipadas en `CompanyPreference`: días anuales de ley por defecto (15), días adicionales de beneficio de vacaciones por defecto, flags de validación de fechas (permitir iniciar/finalizar en asueto, permitir iniciar en día de descanso — **defaults legales del Art. 178 CT: NO iniciar en asueto ni descanso**), uso de aniversario por defecto, día de descanso por defecto de la empresa (domingo), **tope patronal anual de incapacidad** (default 9, D-27) + días de beneficio adicional de incapacidad, y obligatoriedad de constancia (default sí, D-22).

**Reglas de negocio:**
- Columnas anulables con defaults legales SV (null = 15 días de ley; no iniciar vacaciones en asueto ni descanso; constancia obligatoria; tope patronal 9).
- Editables solo por administrador vía la administración de preferencias existente.

**Criterios de aceptación:**
- La generación masiva usa días de ley + beneficio por defecto cuando "usar parametrización" = sí.
- Las validaciones de solicitud y el tope patronal del motor reaccionan a cada preferencia de forma comprobable por test de integración.

**Prioridad:** Alta
**Dependencias:** D-04/D-26, D-06, D-24, D-27.

### RF-006 - Estados, tipos de acción y permisos del módulo

**Descripción:** Sembrar los catálogos de estado (`INCAPACITY_STATUS_CATALOG`: `EN_REVISION` / `REGISTRADA` / `ANULADA`; `VACATION_REQUEST_STATUS_CATALOG`: `SOLICITADA` / `APROBADA` / `RECHAZADA` / `ANULADA` / `DEVUELTA_PARCIAL` / `DEVUELTA`), los nuevos tipos de acción de personal (`INCAPACIDAD`, `PRORROGA_INCAPACIDAD`, `LACTANCIA`, `GOCE_VACACIONES`, `DEVOLUCION_VACACIONES`) y declarar los permisos `PersonnelFiles.ViewIncapacities/ManageIncapacities/ViewVacations/ManageVacations`.

**Reglas de negocio:**
- Patrón híbrido: transiciones custodiadas por constantes canónicas; el catálogo aporta i18n/visualización.
- IDs de semilla en bloque nuevo ≤ -9850 (piso actual verificado: -9846).
- Permisos registrados en `PersonnelFilePolicies` + `ProvisioningConstants.CompanyAdminPermissions` + políticas en `Program.cs` + gates en handlers.

**Criterios de aceptación:**
- Migración `HasData` idempotente; catálogos visibles por sus wire-keys kebab-case.
- Usuario sin permiso recibe 403 en cada endpoint de gestión; empleado autogestionado accede solo a lo propio.

**Prioridad:** Alta
**Dependencias:** D-16, D-17, D-19.

### RF-026 - Maestro de periodos de planilla (instancias) *(agregado en ratificación, D-23)*

**Descripción:** CRUD por empresa de **instancias** de periodo de planilla: tipo de periodo (`PAY_PERIOD_CATALOG`), año, número correlativo, etiqueta y **fechas de corte** (inicio/fin) — p. ej. una primera quincena que termina el día 10, 11, 12, 13, 14 o 15 según la práctica de la empresa (P-06).

**Reglas de negocio:**
- Unicidad `(empresa, tipo, año, número)`; rangos de fechas sin solape por tipo dentro del año.
- Baja lógica solo si ninguna incapacidad activa la referencia.
- La incapacidad referencia la instancia y valida que esté activa.

**Criterios de aceptación:**
- CRUD con If-Match; incapacidad con periodo inexistente/inactivo → 422.
- La exportación de incapacidades incluye etiqueta y fechas del periodo referenciado.

**Prioridad:** Alta
**Dependencias:** `PAY_PERIOD_CATALOG` (existe).

### RF-027 - Día de descanso semanal por plaza *(agregado en ratificación, D-26)*

**Descripción:** Nuevo campo `restDayOfWeek` (día de descanso semanal) en la plaza, editable con el contrato; cuando no está definido aplica el default de empresa (domingo).

**Reglas de negocio:**
- El motor de incapacidades usa el descanso del empleado (plaza principal) para el conteo del séptimo (P-10).
- Las validaciones de vacaciones lo usan para "no iniciar en día de descanso" (Art. 178 CT).
- Cambiarlo afecta solo cálculos futuros (los históricos conservan su snapshot).

**Criterios de aceptación:**
- Empleado con descanso configurado en miércoles: el motor excluye miércoles (no domingos) cuando el riesgo no cuenta séptimo; una solicitud que inicia miércoles → 422 con el default legal.

**Prioridad:** Alta
**Dependencias:** Contrato de plaza existente (PUT employment-information).

### Grupo B — Incapacidades

### RF-007 - Registrar nueva incapacidad

**Descripción:** Alta de una incapacidad sobre el expediente del empleado incapacitado — por RRHH o **auto-registro del propio empleado** (P-11): solicitante (referencia a expediente + snapshot de nombre), riesgo, clínica, tipo, referencia de planilla (tipo de planilla + **instancia de periodo de planilla**, RF-026), plaza opcional, fecha inicio, fecha final (o indefinida) y días.

**Reglas de negocio:**
- Gate de creación: `ManageIncapacities` **OR `isSelf`** (D-18 ajustada, patrón `LoadForCreateOwnOrManage…`); el solicitante se captura como dato (patrón liquidación: `requesterFilePublicId` + snapshot + `requestedByUserId`) y se persiste el origen `RRHH` / `AUTOSERVICIO`.
- Riesgo y tipo validados por referencia activa; **clínica opcional** (validada solo si se envía — el maestro inicia vacío); empleado con perfil `RETIRADO` → 422 (perfil bloqueado).
- `endDate` nula solo si el riesgo permite indefinida (D-11); si hay `endDate`, `startDate ≤ endDate`.
- Sin solape con otra incapacidad activa del mismo empleado (la prórroga es el mecanismo de extensión, no un registro solapado).
- Días naturales = fin − inicio + 1; **días computables** los calcula el motor (RF-008) y son ajustables manualmente quedando `isOverridden` + nota (patrón liquidación).
- Estado inicial: `REGISTRADA` (RRHH) o `EN_REVISION` (autoservicio, hasta confirmación de RRHH — RF-010); con constancia obligatoria (default sí) el alta exige al menos un adjunto válido. Genera asiento (RF-012).

**Criterios de aceptación:**
- POST devuelve 201 con `publicId`, ETag y desglose calculado; catálogo inválido → 422 con `extensions.code`.
- Solape con incapacidad activa → 422 con código específico.

**Prioridad:** Alta
**Dependencias:** RF-001…RF-004, RF-006, RF-008.

### RF-008 - Motor de cálculo de días de subsidio/descuento

**Descripción:** Módulo de reglas **puro y determinista** que, dado el rango de fechas, los flags del riesgo, el calendario de asuetos, el **día de descanso del empleado**, los parámetros por rangos, la **base salarial** (mensual/diaria snapshot) y el **tope patronal anual** disponible, produce el desglose: días computables, días subsidiados (ISSS), días con descuento y días a cargo del patrono, **con el porcentaje y el monto referencial por tramo** (D-21).

**Reglas de negocio:**
- Conteo día a día del rango: se excluyen séptimos (día de descanso del empleado, D-26), sábados y asuetos solo cuando el flag correspondiente del riesgo indica que NO se consideran; con flag "sí" se cuentan.
- Los rangos de parámetros se aplican sobre el **día acumulado de la cadena** (incapacidad original + prórrogas): una prórroga que arranca en el día 31 de la cadena toma el tramo correspondiente al día 31, no reinicia en 1.
- El desglose se **persiste** en el registro (snapshot auditable); cambios posteriores de catálogo no recalculan históricos.
- **Montos referenciales** (D-21 ratificada): salario diario = base mensual / 30 (convención SV); monto por tramo = días × salario diario × %; los días de pagador EMPRESA consumen el tope patronal anual (D-27) y, agotado el tope, pasan a SIN_PAGO (descuento). Base salarial y montos se persisten como snapshot.

**Criterios de aceptación:**
- Suite de **casos dorados** (Anexo A.4, a validar por el contador) en tests unitarios del módulo puro — incluyendo montos y consumo del tope patronal — con paridad de localización de errores.
- Mismo input → mismo output (sin dependencia de reloj/aleatoriedad).

**Prioridad:** Alta
**Dependencias:** RF-002, RF-004, RF-027, D-27; base salarial de compensación de la plaza principal.

### RF-009 - Prórroga de incapacidad

**Descripción:** Extender una incapacidad existente creando un registro enlazado (`extendsIncapacityPublicId`) que continúa el conteo de la cadena.

**Reglas de negocio:**
- Solo si el riesgo de la incapacidad origen aplica prórroga; el origen debe estar `REGISTRADA` y con fecha final definida.
- Continuidad: la prórroga inicia el día siguiente al fin del origen (tolerancia a ratificar si aplica).
- La cadena comparte riesgo (mismo código) salvo decisión en contrario; el desglose de la prórroga usa el día acumulado (RF-008).
- Genera asiento `PRORROGA_INCAPACIDAD`.

**Criterios de aceptación:**
- Prórroga sobre riesgo sin flag → 422; sobre incapacidad anulada → 422.
- El detalle de la incapacidad origen lista sus prórrogas encadenadas.

**Prioridad:** Alta
**Dependencias:** RF-007, RF-008.

### RF-010 - Confirmación, cierre de indefinida y anulación de incapacidad

**Descripción:** PATCH dedicados: (a) **confirmación** de una incapacidad auto-registrada (`EN_REVISION` → `REGISTRADA`, solo RRHH); (b) **cierre** de una incapacidad indefinida fijando fecha final y recalculando el desglose; (c) **anulación** (`ANULADA`) con motivo, que revierte su efecto en saldos y deja el registro trazable.

**Reglas de negocio:**
- Confirmar: solo `ManageIncapacities` (el empleado no confirma la propia); una incapacidad `EN_REVISION` no se exporta como insumo de planilla ni consume el tope patronal hasta confirmarse.
- Cerrar solo si `endDate` es nula; fecha de cierre ≥ fecha inicio.
- Anular no borra: baja lógica del efecto con asiento de reversa en expediente (nota en la acción original o acción de anulación — definir en plan técnico).
- No se permite editar fechas de una incapacidad con prórrogas encadenadas sin anular primero las prórrogas (integridad de cadena).

**Criterios de aceptación:**
- Ambos PATCH exigen If-Match; transición inválida → 409/422 según patrón del repo.

**Prioridad:** Alta
**Dependencias:** RF-007, RF-009.

### RF-011 - Adjuntos de incapacidad (constancia ISSS)

**Descripción:** Adjuntar documentos a la incapacidad (constancia ISSS, certificados médicos) reutilizando el stack de archivos: upload-session SAS → complete con `purpose` → asociación con validación de propósito → read-url temporal.

**Reglas de negocio:**
- Nuevo `FilePurpose.IncapacityDocument` con entrada `Storage:Purposes` en appsettings **base** (gotcha conocido: si falta la configuración → 422) y contenedor pre-aprovisionado.
- Documento con snapshot de nombre/tipo/tamaño, clasificación opcional por catálogo, baja lógica.
- Obligatoriedad de al menos una constancia: parametrizable por empresa, **default obligatoria** (P-17); aplica también al auto-registro del empleado.

**Criterios de aceptación:**
- Adjuntar archivo con propósito distinto → 422 `InvalidPurpose`; read-url expira según configuración.

**Prioridad:** Alta
**Dependencias:** RF-007; configuración de storage.

### RF-012 - Asiento en expediente y estado del empleado

**Descripción:** Toda incapacidad registrada (y su prórroga, lactancia, goce y devolución de vacaciones) genera automáticamente una acción de personal en el expediente con su tipo, fechas de vigencia y estado.

**Reglas de negocio:**
- Códigos D-19 sobre `ACTION_TYPE_CATALOG`; la acción se crea en la misma transacción que el registro fuente.
- El **estado del empleado** (`EmploymentStatusCode` `INCAPACIDAD`/`LICENCIA`) **no** cambia automáticamente en Fase 1 (ratificado en P-18); queda como evolución de Fase 2 con reglas de reversión.

**Criterios de aceptación:**
- Registrar incapacidad → aparece acción `INCAPACIDAD` en el expediente con las fechas del registro; anular → trazabilidad de la anulación.

**Prioridad:** Alta
**Dependencias:** RF-006, RF-007.

### RF-013 - Bandeja de incapacidades + exportación

**Descripción:** Bandeja de empresa (query paginado con filtros: rango de fechas, riesgo, tipo, estado, empleado, plaza) con `StatusCounts`, y exportación tabular xlsx/csv/json con columnas en español (patrón liquidación/constancias), pensada como **insumo para la planilla externa**.

**Reglas de negocio:**
- Solo `ViewIncapacities`/`ManageIncapacities`; export con rate limiting y límite síncrono existente.
- La exportación incluye el desglose (computables, subsidiados, descuento, patrono, % y **montos por tramo**, base salarial) e imputación (tipo de planilla + instancia de periodo con sus fechas); las `EN_REVISION` se excluyen del insumo a planilla (filtro por estado).

**Criterios de aceptación:**
- `POST /companies/{companyId}/incapacities/query` pagina y cuenta por estado; `GET …/export` respeta formatos y filtros.

**Prioridad:** Alta
**Dependencias:** RF-007, RF-008.

### RF-014 - Consulta de incapacidades en ficha y autogestión

**Descripción:** Listado/detalle de incapacidades dentro de la ficha del empleado (RRHH) y consulta de **las propias** por el empleado autogestionado.

**Reglas de negocio:**
- Lectura: `ViewIncapacities` OR `isSelf` (dato de salud: 403 sin enmascaramiento para terceros, patrón reclamos médicos).

**Criterios de aceptación:**
- Empleado vinculado ve sus registros y adjuntos; otro empleado sin permiso → 403.

**Prioridad:** Alta
**Dependencias:** RF-007.

### Grupo C — Lactancia

### RF-015 - Registrar periodo de lactancia con horarios

**Descripción:** Alta del periodo de lactancia (solicitante, empleada destino, tipo —catálogo, código `LACTANCIA`—, fecha inicial, fecha final) y de sus **horarios**: uno o varios rangos con cantidad de permisos por día y minutos por permiso.

**Reglas de negocio:**
- Solo RRHH en Fase 1 (D-18); la empleada consulta lo propio.
- Fechas del horario contenidas en el periodo; sin solape entre horarios del mismo periodo; permisos/día ≥ 1; minutos ≥ 1.
- Registral: no genera descuento ni consume fondo; genera asiento `LACTANCIA`.
- Duración por defecto sugerida por preferencia (P-16: p. ej. 6 meses post-postnatal, a confirmar con legal).
- Estados `REGISTRADA → ANULADA` (mismo catálogo de incapacidades o propio — plan técnico).

**Criterios de aceptación:**
- Alta con horarios válidos → 201 + asiento; horario fuera del periodo o solapado → 422.
- Edición de horarios con If-Match; anulación trazable.

**Prioridad:** Alta
**Dependencias:** RF-003 (tipo `LACTANCIA`), RF-006, RF-012.

### Grupo D — Fondo de vacaciones

### RF-016 - Generar periodos de vacaciones (individual y masivo)

**Descripción:** Creación de periodos anuales del fondo **por empleado**: individual y **masiva** (todos los activos o filtrados), con: año inicial del periodo, genera días de goce (sí/no), utilizar fecha de aniversario (sí/no), usar parametrización (sí/no), días de ley a otorgar y días adicionales de beneficio (D-24).

**Reglas de negocio:**
- Granularidad **por empleado** (D-05 ratificada): unicidad `(expediente, año)`; la generación masiva **omite** los ya existentes y reporta resumen (creados/omitidos/errores) — idempotente.
- Vigencia del periodo: aniversario→aniversario del `StartDate` de la **plaza principal** (D-06) o año calendario.
- Elegibilidad: empleado activo con al menos 1 año de servicio continuo al inicio del periodo (Art. 177 CT, Anexo A.1; parametrizable).
- Días de ley > 0 (default 15) + beneficio ≥ 0; con "usar parametrización" toma los defaults de empresa (RF-005).
- "Genera días de goce" = no → periodo **informativo/monetario** que no habilita solicitudes (semántica P-02).
- Baja lógica de un periodo solo sin consumos.

**Criterios de aceptación:**
- Masivo sobre 2 corridas seguidas → segunda corrida 0 creados (idempotencia).
- Periodo duplicado manual → 409/422 según convención.

**Prioridad:** Alta
**Dependencias:** RF-005; D-05/D-06/D-24.

### RF-017 - Consulta del detalle del fondo (RRHH y autogestión)

**Descripción:** Detalle por empleado: lista de periodos con días otorgados (ley + beneficio), gozados, solicitados en curso, devueltos y **pendientes**, el total disponible y el **valor proyectado para la provisión financiera** (días pendientes × salario diario × 1.30, D-25). Disponible para RRHH en la ficha, para el empleado en autogestión y exportable para Finanzas/Contabilidad (solo lectura).

**Reglas de negocio:**
- Saldo por periodo = otorgados − consumidos por solicitudes `APROBADA` (no devueltas); `SOLICITADA` se muestra como "en trámite" sin reservar (D-13, ratificable).
- Autogestión: `isSelf` únicamente sobre el propio expediente.

**Criterios de aceptación:**
- Los números cuadran contra las asignaciones de consumo en todos los tests de integración de solicitudes/devoluciones.

**Prioridad:** Alta
**Dependencias:** RF-016, RF-022.

### RF-018 - Saldos publicados en el perfil del empleado

**Descripción:** Poblar `vacationDaysAvailable` (suma de pendientes de periodos activos que generan goce) y `disabilityDaysAvailable` (**días restantes del tope patronal anual de incapacidad**: tope + beneficio − días patrono consumidos en el año, D-27/P-15) en la respuesta del perfil, hoy `null`; más una **consulta de saldos de incapacidad** con el detalle (acumulado del año, tope de ley/política, beneficio adicional, restante).

**Reglas de negocio:**
- Cálculo en la proyección (sin columnas nuevas en el perfil); mantiene el contrato OpenAPI existente (los campos ya están declarados).
- Solo las incapacidades `REGISTRADA` consumen tope; las `EN_REVISION` y `ANULADA` no.

**Criterios de aceptación:**
- Perfil de empleado con fondo/tope configurado muestra los saldos reales; sin datos → `null` (documentado en guía FE); la consulta de saldos cuadra contra el motor.

**Prioridad:** Alta
**Dependencias:** RF-016, RF-017, RF-008, D-27.

### RF-019 - Integración con liquidación (días pendientes como sugerencia)

**Descripción:** Query interno que entrega los **días de vacación pendientes del empleado** (fondo por empleado, D-05) para que la liquidación lo use como default de `VACACION_PROPORCIONAL` en lugar de `DaysSinceAnniversary`, cumpliendo la costura declarada en `analisis-liquidacion-empleado.md` (G-04/§17.4).

**Reglas de negocio:**
- La liquidación conserva la editabilidad (`UnitsOrFactorUsed` + `IsOverridden`); el fondo solo mejora la sugerencia.
- Si el empleado no tiene fondo, la liquidación cae al default actual (retrocompatible).

**Criterios de aceptación:**
- Test de integración: liquidación de empleado con fondo sugiere los pendientes del fondo; sin fondo, conserva el comportamiento actual.

**Prioridad:** Media
**Dependencias:** RF-016/RF-017; módulo de liquidación (mergeado).

### Grupo E — Plan anual, solicitudes y devolución

### RF-020 - Plan anual de vacaciones (cabecera + detalle multi-empleado)

**Descripción:** Crear el plan anual: cabecera (solicitante, año, fecha de solicitud) y detalle de empleados con fecha inicio/fin (y días derivados), permitiendo **varios tramos por empleado**. Es la base del "calendario anual de vacaciones".

**Reglas de negocio:**
- Programación **indicativa** (D-15): no consume fondo; valida disponibilidad y reglas de fechas como **advertencias** (respuesta con warnings por línea).
- Edición de fecha inicial/final y días por línea; líneas por empleado ilimitadas mientras no se solapen entre sí.
- Estados de plan: `VIGENTE → ANULADO` (simple, Fase 1).
- Gestión solo RRHH (`ManageVacations`) en Fase 1; plan por jefaturas = Fase 2.

**Criterios de aceptación:**
- Alta de plan con 3 empleados y tramos múltiples → 201; advertencias listadas por línea cuando exceden disponibilidad o caen en asueto.

**Prioridad:** Media
**Dependencias:** RF-004, RF-005, RF-016.

### RF-021 - Validaciones de fechas contra asuetos y descansos

**Descripción:** Reglas parametrizables aplicadas a solicitudes (bloqueo) y plan (advertencia): iniciar/finalizar en asueto, iniciar en séptimo/día de descanso.

**Reglas de negocio:**
- Flags de empresa (RF-005): `allowVacationStartOnHoliday`, `allowVacationEndOnHoliday`, `allowVacationStartOnRestDay` — **defaults legales del Art. 178 CT: no iniciar en asueto ni en día de descanso; finalizar en asueto permitido**.
- El día de descanso es el `restDayOfWeek` del empleado (D-26); default de empresa (domingo) cuando la plaza no lo define.

**Criterios de aceptación:**
- Con el default legal (o flag restrictivo), solicitud que inicia en asueto o en el día de descanso del empleado → 422 con código específico; en plan, la misma condición → warning, no error.

**Prioridad:** Alta
**Dependencias:** RF-004, RF-005.

### RF-022 - Solicitud de vacaciones (autogestión y RRHH) con verificación de fondo

**Descripción:** Crear solicitud con fecha inicio, fecha fin, días solicitados y (opcional) referencia a línea del plan; canal autogestión (empleado sobre sí mismo) o RRHH. Verifica existencia de días disponibles no gozados en el fondo.

**Reglas de negocio:**
- Gate de creación: `ManageVacations` OR `isSelf` (patrón `LoadForCreateOwnOrManage…`).
- Días solicitados ≤ disponibles del fondo (por plaza según D-05) al momento de crear **y** de aprobar.
- Sin restricción de fracciones (P-05 descartó el esquema 21/7/11/10): los días solicitados solo están limitados por el disponible del fondo (ley + beneficio).
- Validaciones RF-021; sin solape con otra solicitud `SOLICITADA/APROBADA` ni con incapacidad activa del empleado.
- Estado inicial `SOLICITADA`; el empleado puede **anular** su propia solicitud mientras esté `SOLICITADA`.

**Criterios de aceptación:**
- Autogestión: empleado vinculado crea su solicitud → 201; sobre otro expediente → 403.
- Fondo insuficiente → 422 con código específico y detalle del saldo.

**Prioridad:** Alta
**Dependencias:** RF-016, RF-017, RF-021.

### RF-023 - Decisión de solicitudes (aprobar / rechazar / anular) — Fase 1 manual

**Descripción:** PATCH de decisión por RRHH: aprobar (consume fondo con asignaciones a periodos, FIFO editable), rechazar (con motivo) o anular; auditoría de quién y cuándo decidió.

**Reglas de negocio:**
- Solo `ManageVacations`; **anti-autoaprobación**: el decisor no puede ser el empleado del expediente (`LinkedUserPublicId == decidedByUserId` → 403, patrón ayuda económica).
- Transiciones custodiadas: `SOLICITADA → APROBADA | RECHAZADA | ANULADA`; re-verificación de saldo y solapes al aprobar.
- Al aprobar: se crean las asignaciones de consumo (suma = días solicitados) y el asiento `GOCE_VACACIONES` con las fechas.

**Criterios de aceptación:**
- Aprobación descuenta del periodo correcto (FIFO) y actualiza la consulta del fondo; decisión sobre estado no pendiente → 409/422.
- Autoaprobación → 403 `SelfApprovalForbidden` (código propio del módulo).

**Prioridad:** Alta
**Dependencias:** RF-022; D-13, D-16.

### RF-024 - Devolución de vacaciones

**Descripción:** Devolver días de una solicitud **aprobada** reintegrándolos automáticamente a los periodos exactos de donde se consumieron: **total** (todos los días → `DEVUELTA`) o **parcial** (una parte, p. ej. goce interrumpido por incapacidad o llamado al trabajo → `DEVUELTA_PARCIAL`), con motivo y auditoría (D-14, P-13).

**Reglas de negocio:**
- Solo `ManageVacations`; las devoluciones son **registros hijos** de la solicitud (días, fecha, motivo); acumulado devuelto ≤ días consumidos.
- Reintegro = reversa de las asignaciones de consumo originales (LIFO por defecto, editable), trazable; genera asiento `DEVOLUCION_VACACIONES` por cada devolución.
- `DEVUELTA` (total) es terminal; `DEVUELTA_PARCIAL` admite devoluciones adicionales hasta agotar los días consumidos (entonces pasa a `DEVUELTA`).

**Criterios de aceptación:**
- Devolución total: el saldo del fondo vuelve exactamente al valor previo a la aprobación; parcial: el saldo recupera exactamente los días devueltos (tests de integración con múltiples periodos de origen y devoluciones encadenadas).

**Prioridad:** Alta
**Dependencias:** RF-023.

### RF-025 - Calendario anual, bandeja de solicitudes y exportación

**Descripción:** (a) Query de **calendario**: goces aprobados + plan del año por empresa/unidad para la vista de calendario anual del frontend; (b) bandeja de empresa de solicitudes (filtros: estado, rango, empleado, plaza) con `StatusCounts`; (c) exportación xlsx/csv/json de solicitudes y de goces del periodo (insumo planilla).

**Reglas de negocio:**
- `ViewVacations` para bandeja/export/calendario; export con rate limiting.
- El calendario devuelve datos (no imágenes): rangos por empleado con estado y origen (plan vs solicitud).

**Criterios de aceptación:**
- Bandeja pagina y cuenta por estado; export respeta filtros; calendario devuelve el año solicitado con goces y plan superpuestos.

**Prioridad:** Media (bandeja/export Alta; calendario Media)
**Dependencias:** RF-020, RF-022, RF-023.

---

## 7. Requerimientos no funcionales

- **Seguridad**: datos de incapacidad y lactancia = **datos de salud sensibles**: lectura solo con `ViewIncapacities` o `isSelf` (403 sin enmascaramiento, patrón reclamos médicos); anti-autoaprobación en decisiones; adjuntos con URLs SAS temporales; gates por handler además de la política de controlador.
- **Auditoría**: `CreatedUtc`/`ModifiedUtc`, `requestedByUserId`/`decidedByUserId` con fechas, baja lógica universal (`isActive`, nunca borrado físico), asientos automáticos en expediente, desglose de cálculo persistido como snapshot inmutable.
- **Concurrencia/API**: convenciones del repo — `api/v1`, If-Match obligatorio en escrituras (faltante → 400, obsoleto → 409), token de concurrencia rotativo por registro, DELETE → `parentConcurrencyToken`, `publicId` (Guid) como identificador externo, enums como strings, errores bilingües en `extensions.code`.
- **Rendimiento**: motor de cálculo puro O(días del rango); bandejas paginadas con índices por `(tenant, expediente, estado, fechas)`; generación masiva procesada por lotes con resumen (volúmenes esperados: cientos de empleados por corrida); exportaciones bajo el límite síncrono existente con desvío a asíncrono.
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId` en todas las entidades; sin trabajos nocturnos obligatorios (los saldos son derivados, no materializados).
- **Usabilidad**: advertencias (plan) diferenciadas de errores (solicitud); mensajes accionables con el saldo/fecha que causó el rechazo; catálogos con `sortOrder` para orden estable en UI.
- **Mantenibilidad**: reglas en módulo puro (`*.Rules.cs`) con tests unitarios y **paridad de localización**; sin números legales codificados (todo en catálogos/parámetros/preferencias); OpenAPI regenerado sin drift.
- **Compatibilidad**: los campos `vacationDaysAvailable`/`disabilityDaysAvailable` ya publicados en el contrato se pueblan sin breaking change; la liquidación mantiene retrocompatibilidad cuando no hay fondo.
- **Accesibilidad**: (frontend) vista de calendario navegable por teclado y con texto alternativo de estados; fuera del alcance backend, se documenta en la guía FE.

---

## 8. Historias de usuario

### HU-001 - Registrar incapacidad con cálculo automático
Como **gestor de RRHH**, quiero **registrar una incapacidad con su riesgo, clínica, tipo y fechas**, para **que el sistema calcule los días de subsidio y descuento según el ISSS y quede asentada en el expediente**.

**Criterios de aceptación:**
- Dado un empleado activo y catálogos válidos, cuando registro la incapacidad, entonces recibo el desglose (computables/subsidiados/descuento/patrono) y se crea la acción `INCAPACIDAD` en el expediente.
- Dado un riesgo que no cuenta asuetos, cuando el rango incluye un asueto del calendario, entonces ese día no se computa.

### HU-002 - Prorrogar una incapacidad
Como **gestor de RRHH**, quiero **extender una incapacidad existente**, para **que los tramos del ISSS continúen contando sobre los días acumulados**.

**Criterios de aceptación:**
- Dado un riesgo que aplica prórroga y una incapacidad con fecha final, cuando registro la prórroga iniciando el día siguiente, entonces se crea enlazada y su desglose usa el día acumulado de la cadena.
- Dado un riesgo sin prórroga, cuando intento prorrogar, entonces recibo 422.

### HU-003 - Adjuntar constancia ISSS
Como **gestor de RRHH**, quiero **adjuntar la constancia de incapacidad escaneada**, para **respaldar documentalmente el registro**.

**Criterios de aceptación:**
- Dado un archivo subido con propósito `IncapacityDocument`, cuando lo asocio a la incapacidad, entonces queda visible con snapshot de nombre/tamaño y read-url temporal.
- Dado un archivo con otro propósito, cuando intento asociarlo, entonces recibo 422.

### HU-004 - Registrar lactancia con horarios
Como **gestor de RRHH**, quiero **registrar el periodo de lactancia de una empleada con sus horarios diarios**, para **formalizar los permisos (cantidad y minutos) durante la jornada**.

**Criterios de aceptación:**
- Dado un periodo válido, cuando agrego horarios con permisos/día y minutos, entonces se guardan sin solaparse y se asienta `LACTANCIA` en el expediente.

### HU-005 - Generar el fondo anual masivamente
Como **gestor de RRHH**, quiero **generar los periodos de vacaciones del año para todos los empleados activos**, para **construir el fondo sin cargarlo empleado por empleado**.

**Criterios de aceptación:**
- Dada una corrida masiva, cuando se ejecuta, entonces se crean solo los periodos faltantes y recibo un resumen (creados/omitidos/errores).
- Dada una segunda corrida idéntica, cuando se ejecuta, entonces no crea duplicados.

### HU-006 - Consultar mi fondo de vacaciones
Como **empleado autogestionado**, quiero **ver mis periodos con días otorgados, gozados, en trámite y pendientes**, para **saber cuántos días puedo solicitar**.

**Criterios de aceptación:**
- Dado mi usuario vinculado a mi expediente, cuando consulto mi fondo, entonces veo mis periodos y totales; si consulto otro expediente, recibo 403.

### HU-007 - Solicitar vacaciones en línea
Como **empleado autogestionado**, quiero **solicitar vacaciones indicando fechas y días**, para **que RRHH las apruebe sin trámites en papel**.

**Criterios de aceptación:**
- Dado saldo suficiente y fechas válidas (asuetos y día de descanso según los defaults legales/configuración), cuando creo la solicitud, entonces queda `SOLICITADA` y puedo anularla mientras no se decida.
- Dado saldo insuficiente, cuando intento crearla, entonces recibo 422 con mi saldo actual.

### HU-008 - Decidir solicitudes sin autoaprobación
Como **gestor de RRHH**, quiero **aprobar o rechazar solicitudes pendientes**, para **controlar el goce contra el fondo**, sin poder decidir sobre mi propia solicitud.

**Criterios de aceptación:**
- Dada una solicitud `SOLICITADA` con saldo vigente, cuando apruebo, entonces se consumen días del periodo más antiguo (editable) y se asienta `GOCE_VACACIONES`.
- Dado que la solicitud es de mi propio expediente, cuando intento decidirla, entonces recibo 403.

### HU-009 - Devolver vacaciones aprobadas
Como **gestor de RRHH**, quiero **anular una solicitud aprobada reintegrando los días**, para **reflejar goces que no se ejecutaron**.

**Criterios de aceptación:**
- Dada una solicitud `APROBADA`, cuando ejecuto la devolución total con motivo, entonces pasa a `DEVUELTA`, el fondo recupera exactamente los días en sus periodos de origen y se asienta `DEVOLUCION_VACACIONES`.
- Dada una devolución **parcial** (p. ej. goce interrumpido), cuando indico los días devueltos, entonces pasa a `DEVUELTA_PARCIAL` y el fondo recupera exactamente esos días.

### HU-010 - Construir el plan anual
Como **gestor de RRHH**, quiero **programar el plan anual con varios empleados y tramos**, para **anticipar coberturas y visualizar el calendario del año**.

**Criterios de aceptación:**
- Dado un plan con líneas por empleado, cuando alguna línea excede la disponibilidad o cae en asueto restringido, entonces recibo advertencias por línea sin bloquear el plan.

### HU-011 - Mantener el calendario de asuetos
Como **administrador de empresa**, quiero **cargar los asuetos del año**, para **que los cálculos de incapacidad y las validaciones de vacaciones los tomen en cuenta**.

**Criterios de aceptación:**
- Dado un asueto nuevo, cuando registro una incapacidad que lo incluye con riesgo que no cuenta asuetos, entonces el día queda excluido del cómputo.

### HU-012 - Exportar insumos para la planilla
Como **analista de planilla (sistema externo)**, quiero **exportar incapacidades con su desglose y los goces del periodo**, para **aplicar descuentos y subsidios en la planilla correspondiente**.

**Criterios de aceptación:**
- Dado un filtro por instancia de periodo de planilla, cuando exporto, entonces obtengo xlsx/csv/json con días y **montos por tramo**, porcentajes, pagador, base salarial e identificación del empleado/plaza (solo incapacidades `REGISTRADA`).

### HU-013 - Auto-registrar mi incapacidad
Como **empleado autogestionado**, quiero **registrar mi incapacidad adjuntando la constancia**, para **que quede en revisión de RRHH sin tener que presentarme**.

**Criterios de aceptación:**

- Dada mi constancia subida con propósito válido, cuando registro mi incapacidad, entonces queda `EN_REVISION` y RRHH la ve en la bandeja para confirmarla.
- Dado que intento registrarla sin constancia (obligatoria por defecto), entonces recibo 422.
- Dado que intento confirmar mi propia incapacidad, entonces recibo 403.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | Los días **y montos referenciales** de subsidio/descuento de una incapacidad se derivan **exclusivamente** de los parámetros del riesgo (rangos día-desde/hasta, %, pagador) y de la base salarial snapshot — sin números legales codificados |
| RN-02 | Flags del riesgo gobiernan el conteo: séptimo (= día de descanso semanal del empleado, D-26), sábado y asueto se excluyen del cómputo solo cuando el riesgo indica que NO se consideran |
| RN-03 | El conteo de tramos continúa a través de la **cadena de prórrogas** (el día 1 de la prórroga es el día N+1 de la cadena) |
| RN-04 | Prórroga solo si el riesgo la permite, sobre incapacidad `REGISTRADA` con fecha final, iniciando el día siguiente |
| RN-05 | `endDate` nula (indefinida) solo si el riesgo lo permite; el cierre posterior fija la fecha y recalcula |
| RN-06 | El desglose calculado se persiste como snapshot: cambios de catálogo/calendario no alteran históricos |
| RN-07 | Días computables ajustables manualmente solo con marca `isOverridden` + nota |
| RN-08 | Todo evento (incapacidad, prórroga, lactancia, goce, devolución) genera asiento automático en acciones de personal |
| RN-09 | El fondo se consume únicamente por solicitudes **aprobadas**, mediante asignaciones explícitas a periodos (FIFO por defecto, editable) |
| RN-10 | Disponible por periodo = otorgados − consumidos (aprobadas no devueltas); la verificación ocurre al crear **y** al aprobar |
| RN-11 | La devolución (total o parcial, D-14) reintegra exactamente a los periodos de origen; el acumulado devuelto nunca excede lo consumido |
| RN-12 | Sin fracciones de Reglamento Interno (P-05): el límite de una solicitud es el disponible del fondo (días de ley + beneficio adicional de empresa, D-24) |
| RN-13 | Validaciones de asueto/descanso: bloqueo en solicitudes, advertencia en plan (parametrizable por empresa) |
| RN-14 | Sin solape entre incapacidades activas del mismo empleado (la extensión es la prórroga) |
| RN-15 | Sin solape entre solicitudes `SOLICITADA/APROBADA` del mismo empleado |
| RN-16 | Sin solape entre una solicitud de vacaciones y una incapacidad activa; el caso "incapacidad interrumpe vacación aprobada" requiere devolución previa (automatización P-12) |
| RN-17 | Anti-autoaprobación: quien decide no puede ser el empleado del expediente decidido (403) |
| RN-18 | Perfil `RETIRADO` bloqueado: no admite nuevos registros del módulo |
| RN-19 | Periodo único por `(expediente, año)` (fondo por empleado, D-05); generación masiva idempotente |
| RN-20 | Un periodo que no "genera días de goce" no habilita solicitudes (semántica exacta P-02) |
| RN-21 | El módulo calcula y entrega días, porcentajes y **montos referenciales** vía exportación, pero no escribe transacciones de planilla (el ledger `PersonnelFilePayrollTransaction` pertenece al sistema externo); la aplicación monetaria final es de la planilla |
| RN-22 | Catálogo/clínica inactivos no son seleccionables en registros nuevos; los históricos conservan su referencia y snapshot |
| RN-23 | Solo las incapacidades `REGISTRADA` son insumo de planilla y consumen tope patronal; `EN_REVISION` (auto-registro) requiere confirmación de RRHH y `ANULADA` no cuenta |
| RN-24 | Los días de pagador EMPRESA consumen el tope patronal anual (default 9 + beneficio adicional, D-27); agotado el tope pasan a SIN_PAGO (descuento) |
| RN-25 | La base salarial usada por el motor se persiste como snapshot en cada incapacidad; cambios salariales posteriores no recalculan históricos |
| RN-26 | El valor de provisión financiera (fondo: pendientes × salario diario × 1.30; incapacidades con `utilizaFondo`: montos del desglose) es informativo para Finanzas/Contabilidad, que solo lee (P-02, D-25) |
| RN-27 | Defaults legales de fechas de vacaciones (Art. 178 CT): no iniciar en asueto ni en el día de descanso del empleado; parametrizable por empresa |

---

## 10. Flujos principales

### Flujo 1 — Registro de incapacidad con cálculo
1. RRHH abre la ficha del empleado incapacitado (o el empleado, en autogestión, abre "Mi incapacidad") y elige "Nueva incapacidad".
2. Ingresa solicitante, riesgo, clínica, tipo, referencia de planilla (tipo + instancia de periodo, RF-026), plaza (opcional) y fechas.
3. El sistema valida catálogos activos, solapes y coherencia de fechas (indefinida solo si el riesgo lo permite).
4. El motor calcula días computables y el desglose por tramos con **montos referenciales** (subsidio/descuento/patrono) usando el calendario de asuetos, el descanso del empleado, la base salarial y el tope patronal disponible.
5. RRHH revisa (puede ajustar computables con nota → `isOverridden`) y guarda.
6. El sistema persiste el registro + snapshot del desglose (días y montos), crea el asiento `INCAPACIDAD` y devuelve 201 con ETag; en autoservicio queda `EN_REVISION` hasta la confirmación de RRHH.
7. Se adjunta la constancia ISSS (obligatoria por defecto: upload-session → complete → asociar).

### Flujo 2 — Prórroga
1. RRHH abre la incapacidad origen y elige "Prorrogar".
2. El sistema verifica flag de prórroga del riesgo y propone fecha inicio = fin origen + 1.
3. RRHH ingresa la nueva fecha final; el motor calcula el desglose con el día acumulado de la cadena.
4. Se guarda enlazada (`extendsIncapacityPublicId`) y se asienta `PRORROGA_INCAPACIDAD`.

### Flujo 3 — Generación masiva del fondo
1. RRHH elige "Generar periodos" con año, flags (aniversario/parametrización/genera goce) y días de ley/beneficio (si manual).
2. El sistema recorre los empleados activos elegibles, omite periodos existentes y crea los faltantes.
3. Devuelve resumen: creados, omitidos, con error (motivo por fila).

### Flujo 4 — Solicitud y aprobación de vacaciones
1. El empleado (autogestión) o RRHH crea la solicitud con fechas y días (opcionalmente desde una línea del plan).
2. El sistema valida saldo disponible, asuetos y día de descanso del empleado (defaults legales del Art. 178 CT) y solapes → `SOLICITADA`.
3. RRHH la ve en la bandeja y decide: aprobar (re-verifica saldo/solapes; consume FIFO editable; asienta `GOCE_VACACIONES`), rechazar o anular (con motivo).
4. El empleado consulta el estado y su fondo actualizado; el perfil refleja el nuevo `vacationDaysAvailable`.

### Flujo 5 — Devolución (total o parcial)
1. RRHH abre la solicitud `APROBADA` (o `DEVUELTA_PARCIAL`) y elige "Devolución" indicando los días a devolver y el motivo.
2. El sistema revierte las asignaciones de consumo (LIFO, editable) hacia sus periodos de origen, pasa a `DEVUELTA` (total) o `DEVUELTA_PARCIAL` y asienta `DEVOLUCION_VACACIONES`.

### Flujo 6 — Plan anual
1. RRHH crea la cabecera (solicitante, año, fecha) y agrega líneas: empleado, fecha inicio, fecha fin (múltiples tramos).
2. El sistema calcula días por línea y devuelve advertencias (disponibilidad, asuetos) sin bloquear.
3. El plan alimenta la vista de calendario anual; las solicitudes pueden referenciar sus líneas.

### Flujo 7 — Lactancia
1. RRHH registra el periodo (solicitante, empleada, tipo `LACTANCIA`, fechas).
2. Agrega horarios: rango, cantidad de permisos/día, minutos por permiso (sin solapes).
3. El sistema guarda, asienta `LACTANCIA` y lo muestra en la ficha y en la autogestión de la empleada.

---

## 11. Flujos alternativos y excepciones

- **Fondo insuficiente** al crear o aprobar solicitud → 422 con saldo actual y días faltantes.
- **Cambio de saldo entre creación y aprobación** (otra solicitud aprobada primero) → la aprobación re-verifica y falla con 422 explicativo.
- **Solape** (incapacidad×incapacidad, solicitud×solicitud, solicitud×incapacidad) → 422 con el registro en conflicto.
- **Incapacidad sobrevenida durante vacaciones aprobadas** → RRHH ejecuta una **devolución parcial** por los días no gozados y luego registra la incapacidad (secuencia manual ratificada en P-12; sin automatización en Fase 1).
- **Prórroga inválida**: riesgo sin flag, origen anulado o indefinido, fecha no contigua → 422 específico por causa.
- **Cierre de indefinida** con fecha < inicio → 422; incapacidad con prórrogas no admite edición de fechas sin anular la cadena → 422.
- **Anulación de incapacidad** con constancia adjunta → permitida; documentos quedan trazables en el registro anulado.
- **Autoaprobación** (decisor = empleado del expediente) → 403.
- **Empleado RETIRADO** → 422 en todo registro nuevo del módulo (perfil bloqueado).
- **Catálogo/clínica inactivos o inexistentes** → 422 por código; catálogo país sin `countryCode` válido → 400.
- **Devolución parcial que excede lo consumido** → 422 con el máximo devolvible.
- **Confirmación de incapacidad** por usuario sin `ManageIncapacities` (o intento del propio empleado) → 403.
- **Auto-registro de incapacidad sin constancia** cuando es obligatoria (default) → 422.
- **Instancia de periodo de planilla inexistente o inactiva** → 422.
- **Inicio en asueto o en día de descanso del empleado** (default legal Art. 178 CT) o fin en asueto con flag restrictivo → 422 (solicitud) / warning (plan).
- **Generación masiva parcialmente fallida** → corrida no transaccional por fila: filas válidas se crean, errores se reportan por fila (idempotencia permite re-correr).
- **Adjunto con propósito incorrecto, tamaño o tipo no permitido** → 422 según `Storage:Purposes`; configuración faltante → 422 (gotcha documentado).
- **If-Match ausente** → 400; **obsoleto** → 409 (reintento con ETag fresco).
- **Baja de periodo con consumos** → 422 (debe devolverse primero).
- **Devolución de solicitud no aprobada** → 409/422 (transición inválida).
- **Plan anulado** → sus líneas dejan de aparecer en el calendario; solicitudes ya creadas que lo referencian no se afectan.

---

## 12. Datos requeridos

> Convenciones del repo aplican a todas las entidades: `long Id` interno + `Guid publicId` externo, `TenantId`, `CreatedUtc`/`ModifiedUtc`, `isActive` (baja lógica), `concurrencyToken` rotativo (If-Match), factoría `Create(...)` + mutadores custodiados. Se listan solo los campos de negocio.

### Entidad: Clínica médica (`MedicalClinic` — maestro por empresa)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| descripcion | Texto (200) | Sí | No vacía; única normalizada por empresa | Nombre de la clínica |
| especialidad | Texto (150) | No | — | Especialidad médica principal |
| sectorCode | Código catálogo | No | `ISSS`/`PUBLICA`/`PRIVADA` | Sector de la clínica |

### Entidad: Riesgo de incapacidad (`IncapacityRisk` — maestro por empresa, plantilla SV precargada)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Único por empresa (código normalizado) | Identificador y descripción del riesgo |
| cuentaSeptimo | Booleano | Sí | — | Si el séptimo día se computa |
| cuentaSabado | Booleano | Sí | — | Si el sábado se computa |
| cuentaAsueto | Booleano | Sí | — | Si los asuetos se computan |
| usaJornada | Booleano | Sí | — | Reservado (Fase 2, motor de jornadas) |
| permiteIndefinida | Booleano | Sí | — | Admite incapacidad sin fecha final |
| aplicaProrroga | Booleano | Sí | — | Admite prórrogas encadenadas |
| utilizaFondo | Booleano | Sí | — | Alimenta la provisión financiera (P-01, D-25) |
| conSubsidio | Booleano | Sí | Sin subsidio → sin tramos ISSS | Si el riesgo genera subsidio |

### Entidad: Parámetro de riesgo (`IncapacityRiskParameter` — hija del riesgo)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| diaDesde | Entero | Sí | ≥ 1; rangos contiguos sin solape desde 1 | Día inicial del tramo (acumulado de cadena) |
| diaHasta | Entero | No | ≥ diaDesde; nulo = en adelante | Día final del tramo |
| porcentaje | Decimal | Sí | 0–100 | % de subsidio o descuento del tramo |
| pagadorCode | Código | Sí | `ISSS`/`EMPRESA`/`SIN_PAGO` | Quién asume el tramo |
| orden | Entero | Sí | — | Orden de presentación |

### Entidad: Tipo de incapacidad (`IncapacityType` — maestro por empresa, plantilla SV precargada)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Único por empresa | Identificador y descripción del tipo |
| tipoDescuento | Texto (150) | No | Texto informativo (P-07) | Tipo de descuento (viaja en exportaciones) |
| tipoIngreso | Texto (150) | No | Texto informativo (P-07) | Tipo de ingreso (viaja en exportaciones) |
| aplicaAccidenteTrabajo | Booleano | Sí | — | Si aplica a accidente de trabajo |

### Entidad: Asueto institucional (`CompanyHoliday` — por empresa)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| fecha | Fecha | Sí | Única por empresa | Fecha concreta del asueto (por año) |
| descripcion | Texto (200) | Sí | No vacía | Nombre del asueto |
| ambitoCode | Código | No | `NACIONAL`/`LOCAL`/`INSTITUCIONAL` | Ámbito del asueto |

### Entidad: Periodo de planilla (`PayrollPeriodDefinition` — maestro por empresa, D-23)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| payPeriodTypeCode | Código catálogo | Sí | `PAY_PERIOD_CATALOG` | Tipo (mensual/quincenal/semanal) |
| anio / numero | Entero | Sí | Único por (empresa, tipo, año, número) | Identificación del periodo |
| etiqueta | Texto (80) | Sí | — | Nombre visible (p. ej. "Quincena 13-2026") |
| fechaInicio / fechaFin | Fecha | Sí | inicio ≤ fin; sin solape por tipo dentro del año | Fechas de corte (P-06: la 1.ª quincena puede terminar el 10…15) |

### Campo nuevo en la plaza (`PersonnelFileEmploymentAssignment`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| restDayOfWeek | Entero (día semana) | No | 0–6; null → default de empresa (domingo) | Día de descanso semanal del empleado (D-26, P-10) |

### Entidad: Incapacidad (`PersonnelFileIncapacity` — sub-registro del expediente)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| requesterFilePublicId + requesterNameSnapshot | Guid + Texto | No | Expediente existente del tenant | Empleado que solicita (patrón liquidación) |
| requestedByUserId | Guid | Sí | Usuario autenticado | Quién digitó el registro |
| riskPublicId | Guid | Sí | Riesgo activo de la empresa | Riesgo de incapacidad |
| clinicPublicId | Guid | No | Clínica activa de la empresa, si se envía | Clínica médica (opcional: el maestro inicia vacío) |
| incapacityTypePublicId | Guid | Sí | Tipo activo de la empresa | Tipo de incapacidad |
| assignedPositionPublicId | Guid | No | Plaza del empleado | Plaza para imputación (opcional) |
| payrollTypeCode | Código catálogo | No | Mismo catálogo de tipo de planilla que usa la plaza | "Planilla" del levantamiento |
| payrollPeriodPublicId | Guid | No | Instancia activa del maestro de periodos (RF-026) | Periodo de planilla (instancia con fechas de corte) |
| origenCode | Código | Sí | `RRHH`/`AUTOSERVICIO` | Canal de registro (D-18) |
| startDate | Fecha | Sí | ≤ endDate si existe | Fecha de inicio |
| endDate | Fecha | No | Nula solo si riesgo permite indefinida | Fecha final |
| diasNaturales | Entero | Derivado | fin − inicio + 1 | Días calendario |
| diasComputables | Decimal | Sí | Motor; editable con `isOverridden` + nota | Días que cuentan según riesgo/asuetos |
| diasSubsidiados / diasDescuento / diasPatrono | Decimal | Derivado (snapshot) | Suma coherente con computables | Desglose de días persistido del motor |
| salarioBaseMensual / salarioDiario | Decimal | Sí (snapshot) | > 0; diario = mensual/30 | Base salarial usada por el motor (D-21) |
| montoSubsidio / montoDescuento / montoPatrono | Decimal | Derivado (snapshot) | días × salario diario × % por tramo | Desglose monetario referencial (D-21) |
| statusCode | Código catálogo | Sí | `EN_REVISION`/`REGISTRADA`/`ANULADA` (híbrido) | Estado |
| extendsIncapacityPublicId | Guid | No | Cadena válida (RN-04) | Incapacidad origen (prórroga) |
| observaciones | Texto (1000) | No | — | Notas |

### Entidad: Documento de incapacidad (`PersonnelFileIncapacityDocument`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| filePublicId | Guid | Sí | `StoredFile` activo, mismo tenant, `Purpose = IncapacityDocument` | Referencia al archivo subido |
| fileName / contentType / sizeBytes | Texto/Texto/Entero | Sí | Snapshot al asociar | Metadatos del archivo |
| documentTypeCatalogItemId | Referencia catálogo | No | — | Clasificación opcional |
| observaciones | Texto (500) | No | — | Notas |

### Entidad: Periodo de lactancia (`PersonnelFileLactationPeriod`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| requesterFilePublicId + snapshot / requestedByUserId | Guid/Texto/Guid | No/Sí | — | Solicitante y digitador |
| incapacityTypePublicId | Guid | Sí | Tipo `LACTANCIA` u otro habilitado (maestro de empresa) | "Nombre de incapacidad" del levantamiento |
| startDate / endDate | Fecha | Sí | inicio ≤ fin | Vigencia del periodo |
| statusCode | Código catálogo | Sí | `REGISTRADA`/`ANULADA` | Estado |

### Entidad: Horario de lactancia (`LactationSchedule` — hija del periodo)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| startDate / endDate | Fecha | Sí | Dentro del periodo; sin solape entre horarios | Vigencia del horario |
| cantidadPermisos | Entero | Sí | ≥ 1 | Permisos por día |
| minutosPorPermiso | Entero | Sí | ≥ 1 | Duración de cada permiso |

### Entidad: Periodo de vacaciones (`PersonnelFileVacationPeriod` — fondo)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| periodoAnio | Entero | Sí | Único por (expediente, año) — fondo por empleado (D-05) | Año inicial del periodo |
| fechaInicio / fechaFin | Fecha | Derivado | Aniversario de la plaza principal (D-06) o año calendario | Vigencia del periodo |
| diasLeyOtorgados | Decimal | Sí | > 0 (default 15, Art. 177 CT) | Días de ley a otorgar |
| diasBeneficioOtorgados | Decimal | No | ≥ 0 (default 0) | Días adicionales de beneficio de empresa (D-24) |
| generaDiasGoce | Booleano | Sí | Semántica P-02 | Si habilita solicitudes de goce |
| usaAniversario | Booleano | Sí | — | Base de la vigencia |
| usaParametrizacion | Booleano | Sí | — | Días desde preferencia vs manual |
| origenCode | Código | Sí | `MANUAL`/`GENERACION_MASIVA` | Cómo se creó |
| gozados / enTramite / devueltos / pendientes | Decimal | Derivados | Desde asignaciones de consumo | Saldos calculados (no columnas) |

### Entidad: Plan anual de vacaciones (`VacationPlan` — nivel empresa)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| planAnio | Entero | Sí | — | Año del plan |
| fechaSolicitud | Fecha | Sí | — | Fecha de elaboración |
| requesterFilePublicId + snapshot | Guid + Texto | Sí | Expediente del tenant | Empleado solicitante (jefe/RRHH) |
| statusCode | Código | Sí | `VIGENTE`/`ANULADO` | Estado del plan |

### Entidad: Línea de plan anual (`VacationPlanLine` — hija del plan)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| personnelFilePublicId | Guid | Sí | Expediente activo | Empleado programado |
| fechaInicio / fechaFin | Fecha | Sí | inicio ≤ fin; sin solape entre líneas del mismo empleado | Tramo programado |
| dias | Decimal | Sí | > 0 (derivable de fechas) | Días del tramo |
| advertencias | — | Derivado | Disponibilidad/asuetos (no persistidas o snapshot ligero) | Warnings de validación |

### Entidad: Solicitud de vacaciones (`PersonnelFileVacationRequest`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| requestedByUserId | Guid | Sí | Usuario autenticado | Quién creó (empleado o RRHH) |
| fechaInicio / fechaFin | Fecha | Sí | RF-021; sin solapes RN-15/16 | Rango a gozar |
| diasSolicitados | Decimal | Sí | > 0; ≤ disponibles del fondo (ley + beneficio) | Días a consumir |
| statusCode | Código catálogo | Sí | `SOLICITADA`/`APROBADA`/`RECHAZADA`/`ANULADA`/`DEVUELTA_PARCIAL`/`DEVUELTA` | Estado híbrido |
| planLinePublicId | Guid | No | Línea de plan vigente | Referencia al plan |
| decidedByUserId / decisionDateUtc / decisionNotes | Guid/Fecha/Texto | Al decidir | Anti-autoaprobación RN-17 | Auditoría de decisión |
| diasDevueltos | Decimal | Derivado | Suma de devoluciones hijas ≤ consumidos | Total devuelto (D-14) |

### Entidad: Consumo de solicitud (`VacationRequestAllocation` — hija de la solicitud)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| vacationPeriodPublicId | Guid | Sí | Periodo con saldo del mismo expediente | Periodo de origen |
| dias | Decimal | Sí | > 0; suma de asignaciones = diasSolicitados | Días tomados de ese periodo |

### Entidad: Devolución de vacaciones (`VacationReturn` — hija de la solicitud, D-14)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| dias | Decimal | Sí | > 0; acumulado ≤ días consumidos | Días devueltos en este evento |
| fechaDevolucion / motivo | Fecha/Texto | Sí | Motivo no vacío | Auditoría de la devolución |
| decididoPorUserId | Guid | Sí | Usuario con `ManageVacations` | Quién ejecutó la devolución |

### Preferencias de empresa (columnas nuevas en `CompanyPreference`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| annualVacationDaysDefault | Decimal | No | > 0; null = 15 (Art. 177 CT) | Días de ley anuales por parametrización |
| additionalVacationBenefitDaysDefault | Decimal | No | ≥ 0; null = 0 | Días adicionales de beneficio por defecto (D-24) |
| allowVacationStartOnHoliday / allowVacationEndOnHoliday / allowVacationStartOnRestDay | Booleano | No | null = defaults legales Art. 178 CT (no iniciar en asueto/descanso; finalizar en asueto permitido) | Flags de validación de fechas |
| defaultUseAnniversary | Booleano | No | null = sí | Base por defecto de los periodos |
| companyRestDayOfWeek | Entero (día semana) | No | null = domingo | Descanso por defecto cuando la plaza no lo define (D-26) |
| employerCoveredIncapacityDaysPerYear | Decimal | No | ≥ 0; null = 9 (política del cliente, D-27) | Tope patronal anual de días de incapacidad |
| additionalIncapacityBenefitDaysPerYear | Decimal | No | ≥ 0; null = 0 | Beneficio adicional interno de incapacidad (P-15) |
| incapacityRequiresDocument | Booleano | No | null = **sí** (P-17) | Constancia obligatoria al registrar incapacidad |

### Catálogos generales nuevos (código + nombre, semilla `HasData`, IDs ≤ -9850)

- `INCAPACITY_STATUS_CATALOG`: `EN_REVISION`, `REGISTRADA`, `ANULADA`.
- `VACATION_REQUEST_STATUS_CATALOG`: `SOLICITADA`, `APROBADA`, `RECHAZADA`, `ANULADA`, `DEVUELTA_PARCIAL`, `DEVUELTA`.
- Nuevos códigos en `ACTION_TYPE_CATALOG`: `INCAPACIDAD`, `PRORROGA_INCAPACIDAD`, `LACTANCIA`, `GOCE_VACACIONES`, `DEVOLUCION_VACACIONES`.
- Sector de clínica (`CLINIC_SECTOR_CATALOG`): `ISSS`, `PUBLICA`, `PRIVADA` (o enum simple — plan técnico).

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Planilla externa** | Saliente (archivos) | Exportaciones xlsx/csv/json de incapacidades (desglose de días y **montos referenciales** por tramo, base salarial, instancia de periodo con fechas de corte) y goces de vacaciones. **No** se escribe `PersonnelFilePayrollTransaction` desde este módulo: ese ledger inmutable pertenece a la sincronización del sistema externo (trampa documentada) |
| **Azure Blob Storage (SAS)** | Existente | Adjuntos de incapacidad vía upload-session + `Storage:Purposes` (`IncapacityDocument`) + contenedor pre-aprovisionado |
| **Módulo de liquidación** | Interna | Query de días pendientes por plaza como sugerencia de `VACACION_PROPORCIONAL` (RF-019) |
| **Perfil del empleado** | Interna | Población de `vacationDaysAvailable` / `disabilityDaysAvailable` en la proyección del perfil (RF-018) |
| **Finanzas / Contabilidad** | Interna (solo lectura) | Consulta/exportación del valor de provisión financiera del fondo y de incapacidades con `utilizaFondo` (D-25); sin acciones de escritura (P-02) |
| **ISSS** | Manual | Sin API pública: la constancia se adjunta escaneada; los parámetros de subsidio viven en el maestro de riesgos de la empresa |
| **Sustitución de autorizaciones** | Correlación funcional | El motivo `VACACIONES`/`INCAPACIDAD` del módulo existente cubre la delegación durante la ausencia; sin acople técnico en Fase 1 |
| **Correo / notificaciones** | Fase 2 | Avisos de solicitud/decisión dentro del flujo de autorizaciones en línea |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de empresa | Administración de clínicas, asuetos y preferencias de vacaciones; asignación de permisos | No decide solicitudes por sí solo (necesita `ManageVacations`); anti-autoaprobación aplica igualmente |
| Gestor de RRHH | `PersonnelFiles.ManageIncapacities` (incapacidades, prórrogas, lactancia, adjuntos) y/o `PersonnelFiles.ManageVacations` (fondo, plan, solicitudes, devoluciones) + lecturas implícitas | No puede decidir solicitudes de su propio expediente (403); datos de salud solo con permiso |
| Consulta / Auditor | `PersonnelFiles.ViewIncapacities`, `PersonnelFiles.ViewVacations` | Solo lectura: bandejas, fichas, exportaciones |
| Empleado (autogestión) | Sin permisos RBAC: gate `isSelf` por `LinkedUserPublicId` | Solo su expediente: crear/anular su solicitud `SOLICITADA`, **auto-registrar su incapacidad** (queda `EN_REVISION`), consultar su fondo, saldos y registros; no ve datos de terceros ni confirma su propia incapacidad |
| Autorizador (Fase 2) | `PersonnelFiles.AuthorizeVacations` / `AuthorizeIncapacities` (RequireAssertion que excluye `Admin`, patrón `AuthorizeRetirement`) | Separación de funciones: autoriza sin poder de gestión general |
| Finanzas / Contabilidad | Credencial con `View*` (provisiones y exportaciones) | Solo lectura; no ejecuta acciones (P-02) |
| Sistema planilla externa | Credencial con `View*` para exportaciones | Solo lectura/export |

---

## 15. Criterios de aceptación generales

1. Motor de cálculo de incapacidades como **módulo puro** con suite unitaria de **casos dorados** (Anexo A.4, ✅ **confirmados por el negocio 2026-07-04**) cubriendo días, montos referenciales y consumo del tope patronal, con test de paridad de localización (patrón liquidación).
2. Suite de integración completa del módulo (CRUD, gates de autogestión, transiciones, solapes, devolución, generación masiva idempotente) **en verde junto con la suite existente** (línea base actual: 472/472).
3. Migraciones `HasData` idempotentes para catálogos y estados (IDs ≤ -9850, verificación `InsertData` en la migración); catálogos consultables por sus wire-keys.
4. `openapi.yaml` regenerado **sin drift**; convenciones API respetadas (If-Match, `publicId`, enums string, errores `extensions.code` bilingües, DELETE → `parentConcurrencyToken`).
5. Saldos del perfil poblados sin breaking change del contrato existente.
6. Bandejas paginadas con `StatusCounts` y exportaciones xlsx/csv/json con rate limiting operativas para incapacidades y solicitudes.
7. Asientos automáticos verificados en expediente para los 5 tipos de acción nuevos.
8. Adjuntos operando de punta a punta con `Storage:Purposes` configurado en appsettings base y contenedor aprovisionado (verificado en despliegue).
9. Anti-autoaprobación y protección de datos de salud cubiertos por tests de integración (403 esperados).
10. Guía de integración frontend publicada (`guia-integracion-frontend-vacaciones-incapacidades.md`) con contratos, estados y flujos.
11. ✅ Decisiones D-01…D-27 ratificadas y preguntas P-01…P-18 respondidas (2026-07-04) — **cumplido**; el plan técnico puede iniciar.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Expectativa de "descuento en planilla" automático**: sin motor de planilla, el cliente podría esperar aplicación monetaria automática; mitigación: comunicar D-02/D-21 y entregar exportaciones exactas.
- **Parámetros legales mal sembrados**: subsidios ISSS mal configurados producirían planillas erróneas; mitigación: casos dorados del contador antes del despliegue (criterio 1) y parámetros 100 % editables en catálogo.
- ~~Discrepancia de fuentes en el % de riesgo profesional~~ → **resuelto**: el negocio confirmó la tabla A.2 (100 % desde el día 1); el parámetro sigue siendo editable por empresa si el ISSS emite otro criterio.
- **Calendario de asuetos desactualizado**: feriados móviles no cargados distorsionan cómputos; mitigación: advertencia operativa anual + utilitario de carga.
- **Montos referenciales vs planilla real**: el módulo calcula con la convención salario/30; si la planilla externa usa otra base (días reales del periodo), habrá diferencias — documentar la convención en las exportaciones y reconciliar cuando exista el módulo de planilla.
- **Volumen de generación masiva** en instituciones grandes; mitigación: procesamiento por lotes idempotente y re-ejecutable.
- **Solapes con módulos futuros** (permisos generales, control de asistencia): mitigación: catálogos y entidades extensibles, sin hardcodear "vacación/incapacidad" fuera de sus códigos.

### Supuestos

- Legislación SV **verificada el 2026-07-04 (Anexo A.1, con fuentes)** como default parametrizable: 15 días + 30 % tras 1 año continuo (Art. 177 CT); no iniciar vacaciones en asueto/descanso (Art. 178 CT); patrono paga días 1–3 de enfermedad común al 75 % (Arts. 50/307 CT + Art. 100 Ley del Seguro Social, criterio MTPS) e ISSS subsidia 75 % desde el día 4 hasta 52 semanas; riesgo profesional subsidiado desde el día 1 (% a confirmar); maternidad 16 semanas al 100 % (12 semanas cotizadas); lactancia 1 h/día por 6 meses (Ley "Amor Convertido en Alimento"). **Ninguno se codifica: todo vive en maestros/preferencias editables por empresa.**
- El tope patronal de **9 días/año es política del cliente**, no un tope legal (la ley fija 3 días por evento sin tope anual); se implementa como preferencia parametrizable (D-27).
- La planilla se procesa en un sistema externo que consume archivos (no hay API de planilla).
- Tenant mono-país (SV) como en el resto de catálogos país del sistema.
- El flujo de autorizaciones en línea es Fase 2 (decisión del propio levantamiento).
- Los empleados autogestionados tienen usuario vinculado (`LinkedUserPublicId`); quienes no, operan vía RRHH.

### Dependencias

- ✅ Ratificación completada (2026-07-04): D-01…D-27 y P-01…P-18 — el plan técnico puede iniciar.
- ✅ Contador/negocio: plantilla de riesgos (A.2), asuetos (A.3) y casos dorados (A.4) **confirmados (2026-07-04)**.
- Cliente en operación: cada empresa carga **su propio calendario de periodos de planilla** (no es estático — sin plantilla global) y crea sus clínicas sobre la marcha (el maestro inicia vacío).
- Infraestructura: entrada `Storage:Purposes:IncapacityDocument` + contenedor de blobs aprovisionado antes del despliegue.
- Internas: módulo de liquidación (mergeado) para RF-019; convenciones de catálogos/seeds vigentes.

---

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-04)

| # | Pregunta (síntesis) | Respuesta del negocio → efecto en el diseño |
|---|---|---|
| P-01 | ¿A qué refiere "utiliza fondo" en el riesgo? | Fondo institucional / de vacaciones para **proyectar el pago**: el efecto operativo es tener la **provisión financiera** para pagar vacaciones, incapacidades, etc. → D-25 (valor proyectado en consultas/exports) |
| P-02 | "Genera días de goce"=no, ¿quién lo usa? | Sí: control y pago monetario; lo usan RRHH y Finanzas/Contabilidad, pero **Finanzas solo toma los datos, no ejecuta acciones** → actor de solo lectura |
| P-03 | ¿Fondo por plaza o por empleado? | **Por empleado**: los días de vacaciones vienen de una plaza laboral; la segunda plaza generalmente es contrato por servicios → D-05 ajustada, unicidad (expediente, año) |
| P-04 | ¿Aniversario de plaza o de institución? | **Aniversario `StartDate`** (plaza principal) → D-06 |
| P-05 | ¿Qué significan las fracciones 21/7/11/10? | **Ignorarlas**: aplican las vacaciones de ley; la empresa puede dar **más días como beneficio adicional** → D-24 (días ley + beneficio; sin lista de fracciones) |
| P-06 | ¿Planilla como tipo+año/mes o instancias catalogadas? | **Se deben catalogar las fechas**: la primera quincena puede cortar el día 10, 11, 12, 13, 14 o 15 → D-23 (maestro de instancias de periodo con fechas de corte) |
| P-07 | ¿Tipo de descuento/ingreso mapea a conceptos? | **De momento texto informativo** → campos de texto en el tipo de incapacidad |
| P-08 | ¿Clínicas con código además de descripción? | **No, solo descripción** → maestro por empresa con descripción obligatoria única |
| P-09 | ¿Riesgos semilla fija o editables? | **Editables por cada empresa** → maestro por empresa con plantilla SV precargada (Anexo A.2) |
| P-10 | ¿Séptimo domingo fijo o por empleado? | **Depende del descanso semanal de cada empleado** → D-26 (`restDayOfWeek` en la plaza, default empresa) |
| P-11 | ¿Auto-registro de incapacidad? | **Sí se puede auto-registrar** → gate `isSelf`, estado `EN_REVISION` + confirmación RRHH, constancia obligatoria |
| P-12 | ¿Interrupción automática de vacaciones por incapacidad? | **Devolución manual previa** → secuencia manual documentada (devolución parcial + registro) |
| P-13 | ¿Devolución parcial en Fase 1? | **Necesaria en Fase 1** → D-14 ajustada (devoluciones hijas, `DEVUELTA_PARCIAL`) |
| P-14 | ¿Calcular montos además de días? | **Sí, aun sin planilla**; se complementará cuando exista el módulo de planilla → D-21 invertida (montos referenciales snapshot) |
| P-15 | ¿Qué muestra `disabilityDaysAvailable`? | Acumulado del año, tope legal restante, etc.; la ley fija los días y **la empresa puede dar más como beneficio interno** → D-20/D-27 + consulta de saldos de incapacidad |
| P-16 | ¿Quién provee los defaults legales / casos dorados? | El cliente fija **9 días/año cubiertos por la empresa**; el resto se verificó contra la ley (Anexo A.1: Arts. 177/178/190/307 CT, ISSS 75 % día 4, riesgo profesional día 1, maternidad 16 semanas, lactancia 1 h × 6 meses) |
| P-17 | ¿Constancia obligatoria? | **Parametrizable por empresa, default SÍ** → D-22 |
| P-18 | ¿Cambio automático del estado del empleado? | **No en Fase 1** → RF-012 (asiento sí, estado no) |

---

## 18. Recomendaciones del Analista de Negocio

1. ✅ **Ratificación completada (2026-07-04)**: las 18 respuestas están incorporadas (fondo por empleado, montos referenciales, devolución parcial, auto-registro con revisión, maestros por empresa, periodos de planilla con fechas de corte). El plan técnico puede iniciar sobre D-01…D-27 sin preguntas abiertas.
2. **Entregar el calendario de asuetos como primer PR transversal**: desbloquea el motor de incapacidades y las validaciones de vacaciones, y lo reutilizarán el tablero RRHH (ausentismo) y el futuro módulo de planilla.
3. **Secuenciar la construcción en PRs** (patrón liquidación): PR-1 catálogos + asuetos + permisos + seeds → PR-2 incapacidades + motor + adjuntos → PR-3 lactancia → PR-4 fondo (generación + consulta + perfil) → PR-5 solicitudes + decisión + devolución → PR-6 plan anual + calendario → PR-7 bandejas + exportaciones → PR-8 integración liquidación + guía FE + suite E2E.
4. **Aislar el cálculo monetario dentro del módulo puro** (D-21 ratificada: montos SÍ): documentar la convención (salario diario = mensual/30, redondeo a 2 decimales por tramo) en el propio motor y en las exportaciones, de modo que el futuro módulo de planilla pueda reemplazar la fuente sin tocar los snapshots históricos.
5. ✅ **Plantilla legal confirmada por el negocio (2026-07-04)**: A.2 (incluido riesgo profesional 100 % día 1), A.3 y A.4 ratificados tal cual — los casos dorados se codifican como tests bloqueantes con estos números.
6. **No construir motor de jornadas ahora**: el flag `usaJornada` queda persistido y documentado; un motor de horarios prematuro sin requerimiento de asistencia sería especulativo.
7. **Reutilizar patrones probados sin variaciones**: estado híbrido + anti-autoaprobación (ayuda económica), gates de autogestión (reclamos médicos), adjuntos con propósito, bandeja/export (liquidación). La novedad real del módulo es el **motor de conteo** y el **fondo con asignaciones** — concentrar allí el esfuerzo de pruebas.
8. **Cuidar los nombres frente a liquidación**: `VacationDays`/`VacationPremiumPercent`/`VACACION_PROPORCIONAL` ya pertenecen a los parámetros de liquidación; el fondo debe usar vocabulario propio (otorgados/gozados/pendientes) para no confundir dominios.
9. **Poblar los saldos del perfil temprano** (RF-018): es la señal visible de valor inmediato para el cliente y cierra una costura pública del contrato.
10. **MVP recomendado si se necesita recortar**: Grupo A + Grupo B (incapacidades completas) primero — es lo que alimenta planilla y tiene urgencia normativa; fondo/plan/solicitudes como segunda entrega. La lactancia es pequeña y puede acompañar cualquiera de las dos.

---

## Anexo A — Parámetros legales de referencia (El Salvador), verificados el 2026-07-04

> Verificación documental vía fuentes públicas, por delegación del negocio (P-16: "lo demás verifícalo tú a través de lo que la ley estipula"). Son los **defaults de la plantilla por empresa** — todo es editable (D-07).
>
> ✅ **CONFIRMADO por el negocio (2026-07-04): A.2 (tabla de riesgos), A.3 (asuetos) y A.4 (casos dorados) quedaron ratificados tal cual.** Además: el calendario de quincenas **no es estático — cada empresa define el suyo** (refuerza D-23, sin plantilla global de periodos), y **no existe catálogo de clínicas de momento** (el maestro inicia vacío, sin semilla; la clínica pasa a ser opcional en la incapacidad).

### A.1 Reglas verificadas

| Concepto | Regla verificada | Base legal / fuente |
|---|---|---|
| Vacación anual | 15 días remunerados con salario ordinario **+ 30 %**, tras 1 año de trabajo continuo | Art. 177 CT ([CSJ](https://www.csj.gob.sv/wp-content/uploads/2021/06/10-Co%CC%81digo-de-Trabajo-de-El-Salvador-Vacacio%CC%81n-anual-remunerada.pdf), [ILO](https://webapps.ilo.org/public/spanish/region/ampro/mdtsanjose/papers/cod_elsa.htm)) |
| Inicio del goce | Asuetos y descansos dentro del periodo **no lo prolongan**; las vacaciones **no pueden iniciarse** en asueto ni en día de descanso semanal | Art. 178 CT ([tusalario.org](https://tusalario.org/elsalvador/derechos-laborales/vacaciones/vacaciones-anuales-y-trabajo-en-dias-festivos)) |
| Compensación / fraccionamiento | Prohibido compensarlas en dinero o especie y acumularlas; fraccionamiento solo excepcional con acuerdo (texto exacto de los Arts. 186–189 a validar con legal) | ([finiquitojusto](https://finiquitojusto.com/derechos-laborales/guia-completa-de-vacaciones-anuales-en-el-salvador-2026/)) |
| Asuetos nacionales | 1-ene; jueves, viernes y sábado de Semana Santa; 1-may; 10-may; 17-jun; 6-ago; 15-sep; 2-nov; 25-dic; **+ 3 y 5 de agosto en San Salvador**; resto del país: día principal de la fiesta patronal local | Art. 190 CT ([CSJ](https://www.csj.gob.sv/wp-content/uploads/2021/06/11-Co%CC%81digo-de-Trabajo-de-El-Salvador-Di%CC%81as-de-asueto.pdf)) |
| Enfermedad / accidente común — patrono | El patrono paga los **días 1–3** (75 % del salario básico); criterio del MTPS (oct-2023) con base en Arts. 50 y 307 CT y Art. 100 de la Ley del Seguro Social | ([BDS Asesores](https://publicaciones.bdsasesores.com/blog/elsalvador-bds_alertalaboral-pago-de-los-tres-primeros-dias-de-incapacidad), [Sulen Ayala](https://www.sulenayala.com/post/es-obligaci%C3%B3n-del-empleador-pagar-los-primeros-3-d%C3%ADas-de-incapacidad-m%C3%A9dica-de-un-trabajador), [LatinAlliance](https://latinalliance.co/en/2023/10/25/comunicado-del-ministerio-de-trabajo-sobre-el-pago-de-incapacidades-el-salvador/)) |
| Enfermedad / accidente común — ISSS | Subsidio **75 % del salario medio de base desde el día 4**, hasta **52 semanas por la misma enfermedad** | Reglamento de Aplicación del Régimen del Seguro Social, Art. 24 ([ISSS — preguntas frecuentes](https://www.isss.gob.sv/preguntas-frecuentes/), [Worki360](https://www.worki360.com/blog/planilla/cumplimiento/el-salvador/incapacidad-isss-el-salvador), [tusalario.org](https://tusalario.org/elsalvador/derechos-laborales/prestaciones-por-enfermedad/prestaciones-por-enfermedad)) |
| Riesgo profesional (accidente de trabajo / enfermedad profesional) | Subsidio ISSS **desde el día 1 al 100 %** — ✅ default **confirmado por el negocio** (A.2 ratificada; fuentes secundarias discrepantes quedan documentadas); gestiona la Comisión Técnica de Riesgos Profesionales | ([Worki360](https://www.worki360.com/blog/planilla/cumplimiento/el-salvador/incapacidad-isss-el-salvador)) |
| Maternidad | Licencia de **16 semanas** (10 obligatorias después del parto); subsidio ISSS **100 %** del salario medio de base (sujeto al tope vigente del ISSS); requisito **12 semanas cotizadas** antes del parto; si no está asegurada, el patrono paga el 75 % | Art. 309 CT reformado ([La Prensa Gráfica](https://www.laprensagrafica.com/elsalvador/Si-quedaste-con-dudas-sobre-licencia-de-maternidad-en-El-Salvador-Lee-esto.-20171019-0064.html), [WageIndicator](https://wageindicator.org/es-sv/trabajo-en-el-salvador/leyes-laborales/maternidad-y-trabajo/)) |
| Lactancia | Interrupción de **1 hora diaria** (divisible en 2 pausas de 30 min a solicitud) — Art. 312 CT; Ley "Amor Convertido en Alimento" (2022): derecho por **6 meses desde el nacimiento**, distribuible por acuerdo, **+1 hora adicional si la jornada excede 8 h** | ([Asamblea Legislativa](https://www.asamblea.gob.sv/node/12423), [Ley](https://crecerjuntos.gob.sv/dist/documents/Ley-Amor-convertido-en-Alimento.pdf), [Reglamento](https://asp.salud.gob.sv/regulacion/pdf/docexternos/19%20Reglamento%20de%20la%20Ley%20Amor%20Convertido%20en%20Alimento%20para%20el%20Fomento%20Proteccion%20y%20Apoyo%20a%20la%20Lactancia%20Materna.pdf)) |
| Tope patronal anual de incapacidad | **No existe tope anual en la ley** (la regla legal es 3 días por evento a cargo del patrono); los "9 días/año que cubre la empresa" indicados por el cliente se implementan como **política parametrizable** (D-27, default 9) | Ratificación del cliente (P-16) |

### A.2 Plantilla de riesgos SV (semilla por empresa, editable — D-07)

| Riesgo | Flags sugeridos | Parámetros (día desde–hasta → % / pagador) |
|---|---|---|
| `ENFERMEDAD_COMUN` | con subsidio; aplica prórroga; indefinida no; séptimo/sábado/asueto: se computan | 1–3 → 75 % **EMPRESA** (consume tope D-27) · 4–∞ → 75 % **ISSS** (máx. 52 semanas) |
| `ACCIDENTE_COMUN` | igual a `ENFERMEDAD_COMUN` | igual a `ENFERMEDAD_COMUN` |
| `ACCIDENTE_TRABAJO` | con subsidio; aplica prórroga; aplica a accidente de trabajo | 1–∞ → **100 % ISSS** (✅ confirmado; no consume tope patronal) |
| `ENFERMEDAD_PROFESIONAL` | igual a `ACCIDENTE_TRABAJO` | igual a `ACCIDENTE_TRABAJO` |
| `MATERNIDAD` | con subsidio; prórroga no; indefinida no | 1–112 → **100 % ISSS** (16 semanas) |

### A.3 Plantilla de asuetos (Art. 190 CT) — cargar por año con fechas concretas

1 de enero · jueves, viernes y sábado de Semana Santa (móviles) · 1 de mayo · 10 de mayo · 17 de junio · 6 de agosto · 15 de septiembre · 2 de noviembre · 25 de diciembre · (San Salvador: 3 y 5 de agosto; otras localidades: día principal de la fiesta patronal).

### A.4 Casos dorados sugeridos para la validación del contador

1. **Enfermedad común 5 días** (mié→dom), descanso del empleado = domingo, riesgo no cuenta séptimo: computables 4; días 1–3 EMPRESA 75 %, día 4 ISSS 75 %; con salario $600 → diario $20 → patrono $45, ISSS $15.
2. **Enfermedad común 2 días**: todo a cargo del patrono; consume 2 del tope anual (quedan 7 de 9).
3. **Cadena con prórroga**: original 3 días + prórroga 4 días → la prórroga arranca en el día 4 de la cadena (tramo ISSS), no reinicia en 1.
4. **Tope patronal agotado**: tercer evento del año de 3 días con tope 9 ya consumido → esos días pasan a SIN_PAGO (descuento al empleado).
5. **Accidente de trabajo 10 días**: ISSS 100 % desde el día 1; no consume tope patronal.
6. **Maternidad 112 días**: ISSS 100 %; sin descuento; sin prórroga.
7. **Incapacidad que cruza un asueto** con riesgo que no cuenta asuetos → el día queda excluido del cómputo y del monto.
8. **Vacaciones**: solicitud que inicia en asueto → 422 (default legal Art. 178); periodo con 15 días de ley + 5 de beneficio → disponible 20; devolución parcial de 4 días reintegra exactamente a los periodos de origen y deja la solicitud en `DEVUELTA_PARCIAL`.





