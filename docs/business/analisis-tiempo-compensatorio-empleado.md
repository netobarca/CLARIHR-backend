# Análisis de negocio — Gestión de tiempo compensatorio (empleado)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | Tiempo compensatorio: acreditaciones, ausencias por tiempo compensatorio (débitos), fondo/estado de cuenta y catálogo de tipos |
| **Fecha** | 2026-07-05 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código |
| **Estado** | **RATIFICADO por el negocio (2026-07-05)** — respuestas P-01…P-14 incorporadas; decisiones finales **D-01…D-21** (D-05/D-06/D-12/D-17 **ajustadas** en la ratificación; D-19…D-21 **nuevas**). Queda **una pregunta de segunda ronda no bloqueante**: P-15 (tarifa de valoración del saldo al liquidar — default parametrizable propuesto) |
| **Naturaleza del módulo** | Greenfield **total** (sin costuras declaradas en el código, a diferencia de vacaciones); tercer miembro de la familia de ausencias diseñada en REQ-001 |
| **Documentos hermanos** | [`analisis-vacaciones-incapacidades-empleado.md`](analisis-vacaciones-incapacidades-empleado.md) (REQ-001, ratificado — aporta la infraestructura transversal que este módulo reutiliza) · [`plan-tecnico-vacaciones-incapacidades.md`](../technical/plan-tecnico-vacaciones-incapacidades.md) · `plan-tecnico-tiempo-compensatorio.md` (**pendiente — puede iniciar con esta ratificación**) |
| **Documentos relacionados** | `analisis-liquidacion-empleado.md` (integración F1: saldo → línea `HORAS_EXTRAS_PENDIENTES` — D-19) · `analisis-sustitucion-autorizaciones-empleado.md` (cobertura durante ausencias) · `analisis-ayuda-economica-empleado.md` (patrón estado híbrido) |

---

## Contexto del cambio (requerimiento original)

El levantamiento solicita un módulo de **"Gestión de tiempo compensatorio"** que proporcione las opciones necesarias para gestionar el tiempo compensatorio que se le debe dar a los empleados **cuando laboran fuera de su jornada y tengan derecho a recibirlo**. Cuatro opciones principales:

1. **Crear acreditación de tiempo compensatorio** — registrar el tiempo compensatorio que debe otorgarse a los empleados que laboran fuera de su jornada con derecho a compensación.
2. **Ausencias por tiempo compensatorio** — registrar las ausencias del empleado por utilizar su tiempo compensatorio, **verificando que tenga fondo**.
3. **Fondo de tiempo compensatorio** — **estado de cuenta** de todas las acreditaciones y ausencias con el **saldo disponible**; saldo positivo = tiempo a favor del empleado.
4. **Tipos de tiempo compensatorio** — catálogo de tipos **para acreditar o debitar** tiempo, usado en los formularios de acreditación **y** de ausencias.

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **El módulo es greenfield total** (verificado exhaustivamente en `src/`, tests y contrato): cero entidades/endpoints/migraciones de tiempo compensatorio, banco de horas u overtime; 0 hits de `compensator|comp-time|overtime` en `docs/technical/api/openapi.yaml`. Lo único que existe sobre "horas extra" son **dos conceptos aislados sin fondo ni saldo**: el concepto de ingreso `HORAS_EXTRA` del catálogo de compensación (`GlobalCatalogSeedData.cs:937`) y la línea manual `HORAS_EXTRAS_PENDIENTES` de la liquidación (`GlobalCatalogSeedData.cs:969`, `SettlementCalculation.Rules.cs:657`). La ratificación convirtió esa segunda pieza en **integración de Fase 1**: el saldo pendiente se agrega automáticamente al cálculo de la liquidación (D-19). A diferencia de vacaciones, **no existe ninguna costura declarada para este módulo**: el perfil solo declara `vacationDaysAvailable`/`disabilityDaysAvailable` (`openapi.yaml:49713-49717`); exponer el saldo de tiempo compensatorio será un campo **aditivo nuevo** (no-breaking).
2. **El sistema no puede saber quién laboró "fuera de su jornada"**: no existe modelo de jornada (la plaza solo tiene `WorkdayCode` texto libre; el `Shift` existente es jornada de *estudios* del currículo) ni control de asistencia/marcaciones. Por tanto la **acreditación es un registro declarativo** de RRHH — fecha trabajada, horas, detalle, quién autorizó y, ratificado en P-11, **documento adjunto de la autorización de jefatura (PDF) obligatorio** — no un cálculo automático. Construir un motor de jornadas para este módulo sería especulativo (misma conclusión G-03/D-04 de REQ-001).
3. **Es el tercer miembro de la familia de ausencias** diseñada en REQ-001 (vacaciones e incapacidades, ratificado y planificado, aún no construido) y debe montarse **sobre su infraestructura**: calendario de asuetos, `restDayOfWeek` por plaza, estados híbridos, asientos de expediente, bandejas + exportaciones. **Ratificado: secuenciarlo después de REQ-001** (D-18).
4. **Arquitectura confirmada — ledger simple**: la ratificación descartó la caducidad en F1 (P-04) y el saldo negativo (P-07), así que el fondo es un **ledger corrido por empleado** (saldo = Σ horas acreditadas − Σ horas debitadas de registros vigentes), derivado, sin periodos anuales ni asignaciones FIFO ni devoluciones — estructuralmente más simple que el fondo de vacaciones.
5. **Unidad confirmada — horas** (P-01/P-02): horas con 2 decimales como unidad canónica; días completos se convierten con la preferencia horas estándar/día (default 8, coherente con el despacho público 8:00-16:00).
6. **La ratificación amplió la Fase 1 en tres piezas** respecto del borrador: (a) **adjunto obligatorio** de autorización de jefatura en la acreditación (P-11 → D-20: nuevo `FilePurpose` + configuración de storage + contenedor); (b) **integración con liquidación** — el saldo pendiente entra al cálculo automático como línea `HORAS_EXTRAS_PENDIENTES` sugerida y editable (P-09/G-07 → D-19: "las horas, por ser compensatorias, se deben pagar"); (c) el **maestro de tipos inicia vacío** — sin plantilla ni seeder; el Anexo A.2 pasa a ser catálogo de referencia para el administrador (P-12 → D-05 ajustada). Además se declara la **costura hacia el futuro módulo de horas extras** (P-13 → D-21: referencia opcional en la acreditación).
7. **Marco legal SV (Anexo A.1)**: en el sector privado el CT ordena **pagar** las horas extra con 100 % de recargo (Arts. 169-170) y el "día de descanso compensatorio remunerado" del Art. 175 es **adicional** al recargo del 50 % por trabajar el descanso — no un sustituto del pago. El esquema "hora extra → hora de tiempo compensatorio" es figura del **sector público** (Disposiciones Generales de Presupuestos, Arts. 84/113) y de **políticas institucionales internas**, con factores y topes que varían por institución. Consecuencia de diseño: **nada se codifica como regla legal** — tipos, factores y topes viven en maestros/preferencias editables por empresa, y se documenta el riesgo de **doble beneficio** (pagar la hora extra en planilla Y acreditarla como tiempo).

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Dónde | Uso en este módulo |
|---|---|---|
| Estado híbrido + anti-autoacción (constantes canónicas + catálogo + transiciones custodiadas + `SelfApprovalForbidden` 403) | `PersonnelFileEmployee.cs:1678-1880` (ayuda económica); `EconomicAidRequests.Handlers.cs:371-380` | Ciclo `REGISTRADA → ANULADA` de acreditaciones y ausencias (F1) y base para el flujo de solicitudes de F2 |
| Gates de autogestión (`LoadForCreateOwnOrManage…`, lecturas `View… OR isSelf` vía `LinkedUserPublicId`) | `Common/PersonnelFileEmployeeHandlerBases.cs` | Consulta del estado de cuenta propio por el empleado (F1 solo lectura) |
| Acciones de personal (`PersonnelFilePersonnelAction.Create` + `ACTION_TYPE_CATALOG` hasta `LIQUIDACION=-9484`) | `PersonnelFileEmployee.cs:573-584`; `GlobalCatalogSeedData.cs:736-750` | Asiento automático de acreditaciones y goces (RF-010) con 2 códigos nuevos |
| `CompanyPreference` tipada (columnas + mutadores custodiados) | `Domain/Preferences/CompanyPreference.cs` | Horas estándar por día, tope opcional de saldo, obligatoriedad del adjunto y tarifa de liquidación (RF-002) |
| Maestros por empresa governed (`TenantEntity` + familia `[ResourceActions]`; seeders `*SeedService.InitializeDefaultsAsync` cableados en provisioning cuando aplica) | `OrgStructureCatalogSeedService.cs:10-55`; `CompanyProvisioningService.cs:151-153` | Catálogo de **tipos de tiempo compensatorio** (RF-001) sigue la familia governed — **sin seeder: inicia vacío** (P-12), como las clínicas de REQ-001 |
| Stack de adjuntos con propósito (`FilePurpose` + upload-session SAS + `PATCH complete` + gate `Storage:Purposes` + read-url) | `Files/*`; plantilla `MedicalClaimDocuments.Handlers.cs:126-132,188` | **Documento de autorización de jefatura** de la acreditación (RF-012, D-20 — obligatorio P-11) |
| Estilo de ledger crédito/débito (append-only con `IsDebit`; corrección referenciada `CorrectsTransactionPublicId`) | `PersonnelFilePayrollTransaction` (`PersonnelFileEmployee.cs:645` — **solo estilo: NO escribirlo**, es la sincronización externa); `PersonnelFileOffPayrollTransaction` (`:1442`) | Referencia estilística del estado de cuenta; el **saldo almacenado no tiene precedente** en el sistema (todo saldo se computa on-read) — coherente con el saldo derivado de D-03 |
| Conceptos de horas extra existentes (frontera e integración del módulo) | `HORAS_EXTRA` ingreso de compensación (`GlobalCatalogSeedData.cs:937`); `HORAS_EXTRAS_PENDIENTES` línea manual de liquidación (`GlobalCatalogSeedData.cs:969`; `SettlementCalculation.Rules.cs:657`) | `HORAS_EXTRA` delimita RN-14 (lo pagado por planilla no se acredita); `HORAS_EXTRAS_PENDIENTES` es el **receptor de la integración F1** (D-19: el saldo se sugiere automáticamente al liquidar) |
| Liquidación (`PersonnelFileSettlement`, motor puro, líneas editables `UnitsOrFactorUsed` + `IsOverridden`, gancho de reversión) | Módulo **mergeado** (PR #56) | **Consumidor en F1** (RF-013): query interna del saldo → línea automática sugerida y editable |
| Calendario de asuetos + `restDayOfWeek` por plaza (default empresa domingo) | **Llegan con REQ-001** (PR-1/PR-2; aún no construidos) | Sugerencia de horas a debitar en ausencias de días completos (excluir descanso/asuetos) y coherencia de solapes |
| Maestro de periodos de planilla (instancias con fechas de corte) | **Llega con REQ-001** (D-23, `PayrollPeriodDefinition`) | Imputación opcional de la ausencia a un periodo (insumo de planilla, P-14) |
| Plazas: `PersonnelFileEmploymentAssignment` (`IsPrimary`, `WorkdayCode` texto libre, `PayrollTypeCode`) | `PersonnelFileEmployee.cs` | Referencia opcional de plaza en la acreditación (dónde se trabajó el tiempo extra) |
| Perfil: patrón de saldos calculados en la proyección (sin columnas) | `EmployeeProfiles.cs:39-40`; `PersonnelFileEmployeeRepository.cs:2213-2227` | Mismo patrón para el **nuevo campo aditivo** `compensatoryTimeHoursAvailable` (RF-009) |
| Bandeja + exportación tabular (query paginada `StatusCounts`, export xlsx/csv/json con rate limiting, filas en español) | `SettlementsBandeja.cs`, `ReportExportFileWriter.cs`, `SettlementsReportingController.cs` | Bandeja de movimientos y export de saldos (RF-011) |
| Catálogo de motivos de sustitución (`PERMISO`, `VACACIONES`…) | `GlobalCatalogSeedData.cs:471-476` | Correlación funcional (quién cubre durante la ausencia); sin acople técnico |

### Lo que NO existe (verificado exhaustivamente en `src/`, tests y contrato)

- Ningún módulo/entidad/endpoint de **tiempo compensatorio, banco de horas u overtime** (0 hits de `compensator|CompTime|comp_time|TimeOff|overtime` en `src/`; "horas extra" solo como los dos conceptos aislados citados arriba, sin fondo ni acreditación).
- Ninguna **costura declarada** para este módulo: el perfil solo declara los saldos de vacaciones/incapacidades (propiedad de REQ-001, comentario en `EmployeeProfiles.cs:22-24`); no hay campo, comentario ni TODO de tiempo compensatorio.
- Ningún modelo de **jornada/horarios/turnos/marcaciones**: la jornada es solo `WorkdayCode` — string libre opcional ≤ 80 en la plaza (`PersonnelFileEmployee.cs:185`; validación de longitud en `EmploymentAssignments.cs:108`), sin horas/día, horas/semana ni días de descanso; el `Shift` existente es turno **académico** del currículo. Imposible validar automáticamente "fuera de su jornada" (G-01).
- Ningún **saldo almacenado** en el sistema (todo saldo se computa on-read): el estado de cuenta con saldo corrido es diseño nuevo, sin precedente materializado — coherente con el saldo derivado ratificado (D-03).
- Ningún **módulo de horas extras** (P-13): la relación acreditación ↔ registro de hora extra queda como **costura declarada** (D-21) esperando ese módulo futuro.

---

## Brechas identificadas (GAP → resolución ratificada)

| # | Brecha detectada | Resolución (ratificada 2026-07-05) |
|---|---|---|
| G-01 | "Laboran fuera de su jornada" presupone conocer la jornada, pero no hay modelo de jornada ni asistencia/marcaciones | **Acreditación declarativa**: RRHH registra fecha trabajada, horas, detalle del trabajo y quién lo autorizó, **con el documento de autorización de jefatura adjunto (PDF, obligatorio — P-11)**; la veracidad es responsabilidad del registrador/autorizador (RN-13). Sin motor de jornadas en F1 (mismo corte D-04 de REQ-001) |
| G-02 | "Que tengan derecho a recibir tiempo compensatorio" sin modelo de elegibilidad | F1: **criterio de RRHH** al registrar (sin gate duro); si el negocio requiere control formal → flag de elegibilidad por plaza en F2 (P-05) |
| G-03 | Unidad de medida sin definir (¿horas o días?) | **Horas** (decimal, 2 posiciones) como unidad canónica (P-01); días completos se convierten con la preferencia **horas estándar por día** (default 8, P-02). Cubre ausencias parciales y días completos (D-02) |
| G-04 | Un solo catálogo de tipos "para acreditar o debitar" usado en ambos formularios | Maestro por empresa con **operación** por tipo (`ACREDITA`/`DEBITA`/`AMBAS`) + **factor de acreditación** (default 1.00: 1 hora trabajada = 1 hora acreditada; editable por empresa — P-03) (D-05). **Inicia vacío, sin plantilla** (P-12); el Anexo A.2 queda como referencia para el administrador |
| G-05 | "Verificar que tenga fondo" sin semántica definida (momento, concurrencia, negativos) | Invariantes de saldo: débito ≤ saldo **dentro de la transacción** de escritura (sin carreras); saldo **nunca negativo** (P-07); anular una acreditación no puede dejar el saldo descubierto (D-08/D-09) |
| G-06 | Sin motor de planilla: la ausencia compensatoria es **pagada** (sin descuento) y las horas acreditadas **no se pagan** | Exportaciones tabulares como insumo para la planilla externa (ausencias justificadas del periodo + horas acreditadas); regla operativa de **no doble beneficio** documentada (RN-14) |
| G-07 | Destino del saldo al terminar la relación laboral | **Resuelta (P-09): "las horas, por ser compensatorias, se deben pagar."** El retiro no se bloquea, y en **F1 la liquidación agrega automáticamente el saldo pendiente al cálculo** como línea `HORAS_EXTRAS_PENDIENTES` (`SettlementCalculation.Rules.cs:657`) sugerida y editable (D-19, RF-013). En F2 se podrá refinar la política de pago o caducidad del saldo al retiro |
| G-08 | Caducidad del tiempo acreditado no definida | **Resuelta (P-04): sin caducidad en F1** → ledger simple derivado (el negocio no indicó ninguna política de caducidad vigente). Si en el futuro se introduce, el consumo pasaría a FIFO por tramos (evolución consciente, no F1) |
| G-09 | Solapes contra vacaciones/incapacidades: REQ-001 diseñado pero no construido | Secuenciar este módulo **después** de REQ-001 (D-18); las validaciones cruzadas de solape se especifican condicionadas a su presencia (RN-05) |
| G-10 | Empresas que adoptan el módulo ya arrastran saldos históricos (Excel/papel) | **Carga inicial** vía acreditaciones con un tipo creado por la propia empresa (p. ej. `SALDO_INICIAL`, sugerido en el Anexo A.2 — el maestro inicia vacío): fecha, horas, detalle "saldo migrado al DD/MM/AAAA" y el documento de respaldo del control anterior como adjunto. Sin mecanismo especial de importación en F1 |

---

## Decisiones ratificadas por el negocio (2026-07-05) — D-01…D-21

> ✅ **TODAS RATIFICADAS** el 2026-07-05 con las respuestas P-01…P-14 (§17). Las marcadas **(ajustada)** difieren del borrador original; D-19…D-21 nacen de las respuestas. Única pendiente menor: P-15 (tarifa de valoración al liquidar) — **segunda ronda, no bloqueante** (default parametrizable definido en D-19).

| # | Tema | Decisión ratificada |
|---|---|---|
| D-01 | Fases | **F1**: registro directo por RRHH (acreditaciones y ausencias) + consulta del estado de cuenta propio en autogestión (solo lectura). **F2**: solicitud en línea del empleado (goce y/o acreditación) con flujo de autorización de jefatura, notificaciones. Estados híbridos preparados para agregar `SOLICITADA/APROBADA/RECHAZADA` sin romper contrato (P-06) |
| D-02 | Unidad de medida | **Horas** con 2 decimales como unidad canónica del fondo (P-01). Ausencias de días completos se registran en horas usando la preferencia `horas estándar por día` (default **8**, P-02 — jornada diurna / despacho público 8:00-16:00) |
| D-03 | Granularidad del fondo | **Por empleado** (precedente P-03/D-05 de REQ-001). Saldo = Σ horas acreditadas − Σ horas debitadas de registros **vigentes** (`REGISTRADA` y activos), **derivado** (nunca materializado). La acreditación referencia la **plaza** opcionalmente (dónde se trabajó), sin partir el fondo |
| D-04 | Caducidad | **Sin caducidad en F1** (P-04 ratificada; el negocio no tiene política de caducidad vigente): ledger corrido simple. Si a futuro se introduce caducidad, el consumo pasa a **tramos FIFO** con fecha de expiración — evolución consciente documentada, no se construye especulativamente |
| D-05 | Catálogo de tipos **(ajustada)** | Maestro por empresa, editable: código, descripción, **operación** (`ACREDITA`/`DEBITA`/`AMBAS`), **factor de acreditación** (decimal, default 1.00), orden, baja lógica. **Inicia VACÍO — sin plantilla ni seeder ni `load-template`** (P-12): cada empresa crea sus tipos antes de operar (paso de adopción); el **Anexo A.2 es solo catálogo de referencia** para el administrador (P-03: factores sugeridos en 1.00 hasta definición de cada empresa) |
| D-06 | Acreditación **(ajustada)** | Registro declarativo por RRHH sobre el expediente: tipo (operación `ACREDITA`/`AMBAS`), **fecha trabajada** (un día por registro, no futura), hora inicio/fin opcionales (informativas), **horas trabajadas** (> 0), **factor aplicado** (snapshot del tipo, editable con nota → `isOverridden`, patrón liquidación), **horas acreditadas** = trabajadas × factor (redondeo 2 dec), **detalle del trabajo** (obligatorio), **autorizado por** (texto obligatorio + referencia opcional a expediente), **documento de autorización de jefatura adjunto obligatorio (PDF — P-11, D-20)**, plaza opcional y **referencia opcional al registro de hora extra de origen** (costura al módulo futuro — P-13, D-21). Estado `REGISTRADA → ANULADA` |
| D-07 | Ausencia | Registro por RRHH: tipo (operación `DEBITA`/`AMBAS`), **fecha inicio/fin** (rango; puede ser futura — goce programado), **horas a debitar** explícitas (> 0), con **sugerencia** = días del rango × horas estándar (excluyendo día de descanso del empleado y asuetos cuando REQ-001 esté presente), motivo, imputación **opcional** a instancia de periodo de planilla (P-14). Estado `REGISTRADA → ANULADA` |
| D-08 | Verificación de fondo | Horas a debitar ≤ **saldo disponible**, verificado al validar **y re-verificado dentro de la transacción** de escritura (evita carreras entre débitos concurrentes). Insuficiente → 422 con saldo actual y faltante. **Saldo negativo no permitido** (P-07 ratificada: nunca negativo; sin "adelanto de tiempo") |
| D-09 | Anulaciones | Con motivo obligatorio, baja lógica del efecto (nunca borrado físico), trazables. **Anular una acreditación** exige que el saldo resultante sea ≥ 0 (los débitos ya registrados no pueden quedar descubiertos — P-07) → 422 si viola. **Anular una ausencia** restaura el saldo automáticamente (es derivado). Ambas re-verificadas dentro de la transacción |
| D-10 | Estado de cuenta | Query cronológica de **movimientos** del empleado (acreditaciones + y ausencias −, con tipo, fechas, detalle y **saldo corrido**) + totales (acreditado, debitado, **saldo disponible**). Visible para RRHH (`View`/`Manage`) en la ficha y para el empleado (`isSelf`) en autogestión; exportable |
| D-11 | Saldo en perfil | Nuevo campo **aditivo** `compensatoryTimeHoursAvailable` (nullable) en la respuesta del perfil (P-08 ratificada), calculado en la proyección (patrón RF-018 de REQ-001, sin columnas nuevas). `null` = sin movimientos / módulo sin datos (documentar en guía FE) |
| D-12 | Montos **(ajustada)** | El **fondo se administra solo en horas** (sin montos ni provisión financiera durante la relación laboral). **Única valoración monetaria de F1**: la sugerencia automática al liquidar (D-19), calculada por el **motor de liquidación** (no por este módulo) a partir del saldo en horas — el fondo permanece limpio de dinero |
| D-13 | Permisos | `PersonnelFiles.ViewCompensatoryTime` / `PersonnelFiles.ManageCompensatoryTime` (receta de 8 archivos, fallback `Admin`/`ManageAdministration`); lecturas `View… OR isSelf`; escrituras manage-only en F1. F2: `AuthorizeCompensatoryTime` (RequireAssertion que excluye Admin, patrón `AuthorizeRetirement`) |
| D-14 | Asientos en expediente | Automáticos en la misma transacción: `ACREDITACION_TIEMPO_COMPENSATORIO` y `GOCE_TIEMPO_COMPENSATORIO` (espejo de `GOCE_VACACIONES`) sobre `ACTION_TYPE_CATALOG`; anulaciones trazables en el registro (sin ActionType propio, mismo corte que REQ-001). IDs de semilla en bloque nuevo ≤ -9865 (Anexo A.3 — **no** continuar la secuencia -9485…-9489: el rango -9490…-9496 ya está ocupado por `ACTION_STATUS_CATALOG`) |
| D-15 | Estados | Un catálogo TPH `COMPENSATORY_TIME_STATUS_CATALOG` (wire `compensatory-time-statuses`): `REGISTRADA`, `ANULADA` (F1). Patrón híbrido (constantes canónicas + validación por catálogo). F2 agrega estados de solicitud de forma aditiva. La **operación** del tipo (`ACREDITA`/`DEBITA`/`AMBAS`) puede ser TPH `compensatory-time-operations` o constantes — lo define el plan técnico (mismo corte que `clinic-sectors`) |
| D-16 | Elegibilidad | F1 **sin gate duro** (P-05): RRHH decide a quién acredita ("que tengan derecho" = política institucional que el sistema no puede derivar sin modelo de contrato/jornada). Si el negocio requiere control formal, F2 agrega flag por plaza (p. ej. `eligibleForCompensatoryTime`) validado al acreditar |
| D-17 | Retiro y saldo **(ajustada)** | Perfil `RETIRADO` **bloqueado** para registros nuevos (precedente RN-18). El retiro de un empleado con saldo positivo **no se bloquea** y el saldo queda visible en bandeja/export. **Ratificado (P-09/G-07): el saldo se paga** — en F1 la liquidación lo incluye **automáticamente** en su cálculo (D-19, RF-013); en F2 se podrá refinar la política de pago o caducidad del saldo al retiro |
| D-18 | Secuenciación | Construir **después de REQ-001** (al menos su Ola 1 + PR-2): reutiliza asuetos y `restDayOfWeek` (sugerencia de débito), periodos de planilla (imputación opcional) y las reglas de solape cruzado. Si REQ-001 se pospusiera, el módulo puede operar **degradado** (sugerencia = días calendario × horas estándar; sin solapes cruzados). La integración con liquidación (D-19) **no** depende de REQ-001 (el módulo de liquidación ya está mergeado) |
| D-19 | Integración con liquidación **(nueva — P-09/G-07)** | Al generar/regenerar una liquidación, el motor consulta el **saldo de horas pendiente** del empleado (query interna de este módulo) y, si es > 0, **agrega automáticamente la línea `HORAS_EXTRAS_PENDIENTES`** con: unidades = horas del saldo, monto sugerido = horas × **valor hora ordinaria** (= salario base mensual / 30 / horas estándar por día, redondeo 2 dec, misma convención salario/30 ratificada) × **tarifa parametrizable** (preferencia `compensatoryTimeSettlementRateFactor`, default **1.00** — P-15 segunda ronda). La línea conserva la editabilidad estándar del liquidador (`UnitsOrFactorUsed` + `IsOverridden`); sin saldo o sin datos → sin línea automática (retrocompatible con la línea manual actual). El snapshot del saldo usado queda persistido en la liquidación |
| D-20 | Adjunto de autorización **(nueva — P-11)** | Nuevo `FilePurpose` para el documento de autorización de jefatura (nombre final en plan técnico, p. ej. `CompensatoryTimeAuthorization`): entrada `Storage:Purposes` en appsettings **BASE** (content-type **PDF**; gotcha conocido: config faltante → 422) + **contenedor de blobs pre-aprovisionado**. **Obligatorio al crear la acreditación** (el POST exige `filePublicId` validado por propósito; se asocia en la misma transacción del alta — espejo de la constancia de incapacidad D-22 de REQ-001); parametrizable por empresa con default **sí** (`compensatoryTimeCreditRequiresDocument`). La ausencia NO requiere adjunto |
| D-21 | Costura al módulo de horas extras **(nueva — P-13)** | La acreditación lleva un campo opcional `overtimeRecordPublicId` (Guid nullable, **sin FK dura ni validación en F1** — el módulo de horas extras no existe aún) que **queda listo** para que, cuando ese módulo se desarrolle, la acreditación se vincule al registro de hora extra que la originó (y el módulo futuro pueda validar/poblar la relación y prevenir doble beneficio de forma sistemática). Documentado como costura en código y contrato |

---

## 1. Resumen del producto o requerimiento

Se construirá el módulo de **Gestión de tiempo compensatorio** de CLARIHR: la administración, dentro del expediente de personal, del **banco de tiempo** que la institución otorga a los empleados que laboran fuera de su jornada con derecho a compensación en tiempo (no en dinero).

**Qué se construye.** Cinco capacidades sobre el expediente del empleado:

1. **Acreditaciones**: registro declarativo del tiempo compensatorio otorgado — fecha trabajada, horas, tipo (catálogo), factor de acreditación, detalle del trabajo, quién lo autorizó y el **documento de autorización de jefatura adjunto (PDF, obligatorio)** — con asiento automático en el expediente y costura al futuro módulo de horas extras.
2. **Ausencias por tiempo compensatorio**: registro del goce — rango de fechas y horas a debitar — **verificando que el empleado tenga fondo suficiente** al momento de registrar (sin saldos negativos), con asiento automático.
3. **Fondo (estado de cuenta)**: consulta cronológica de todas las acreditaciones y ausencias con **saldo corrido y saldo disponible**, para RRHH (ficha del empleado) y para el propio empleado (autogestión); saldo publicado en el perfil y exportable.
4. **Catálogo de tipos**: maestro por empresa de los tipos de tiempo compensatorio (con operación acreditar/debitar y factor), usado en ambos formularios; **inicia vacío** — cada empresa define su política.
5. **Integración con liquidación**: al liquidar a un empleado, el **saldo pendiente de horas se agrega automáticamente** al cálculo como línea `HORAS_EXTRAS_PENDIENTES` sugerida y editable — las horas compensatorias no gozadas **se pagan**.

**Problema que resuelve.** Hoy el control del tiempo compensatorio se lleva fuera del sistema (papel/Excel): no hay trazabilidad de cuánto tiempo extra se debe a cada empleado, las ausencias "a cuenta de tiempo" no se validan contra ningún saldo, el saldo pendiente no llega a la liquidación, y ni el expediente ni la planilla externa reciben constancia de estos movimientos.

**Objetivo principal.** Que todo tiempo compensatorio otorgado y gozado quede registrado, respaldado documentalmente, validado contra un **fondo único por empleado** y trazado en el expediente; que la planilla externa reciba el insumo exacto de ausencias justificadas del periodo; y que al retiro el saldo pendiente entre automáticamente al cálculo de la liquidación.

---

## 2. Objetivos del negocio

1. **Fuente única de verdad del tiempo compensatorio**: saldo auditable por empleado (acreditado − gozado), consumido solo mediante ausencias verificadas contra fondo — elimina los controles paralelos en Excel y las ausencias sin respaldo.
2. **Cumplimiento de la política institucional**: soportar los esquemas de compensación en tiempo del sector público salvadoreño (DGP) y de reglamentos internos, con tipos, factores y topes **parametrizables por empresa** (nada codificado como ley).
3. **Trazabilidad y control documental**: cada acreditación registra qué se trabajó, cuándo, **quién lo autorizó y con qué documento** (autorización de jefatura adjunta obligatoria); cada goce queda asentado en el expediente; anulaciones con motivo y auditoría.
4. **Autogestión**: el empleado consulta su propio estado de cuenta y saldo sin cargar a RRHH; base para la solicitud en línea de F2.
5. **Insumo exacto para la planilla externa**: exportación de ausencias compensatorias del periodo (ausencias **justificadas y pagadas**, que no deben descontarse) y de horas acreditadas (que **no** deben pagarse como extra — no doble beneficio).
6. **Expediente completo**: acreditaciones y goces como acciones de personal, consistente con el resto de módulos (incapacidades, vacaciones, retiro).
7. **Pago garantizado del saldo al retiro**: "las horas, por ser compensatorias, se deben pagar" (G-07 ratificada) — el saldo pendiente entra automáticamente al cálculo de la liquidación (línea editable), sin depender de que el liquidador lo recuerde.

---

## 3. Alcance funcional

### Fase 1 — MVP (ratificado 2026-07-05)

- **Catálogo y configuración**: maestro por empresa de tipos de tiempo compensatorio (operación + factor; **inicia vacío**, sin plantilla — la empresa crea sus tipos como paso de adopción), preferencias de empresa (horas estándar por día, tope opcional de saldo, obligatoriedad del adjunto, tarifa de liquidación), catálogo de estados, permisos RBAC, 2 tipos de acción de personal.
- **Acreditaciones**: crear (RRHH) con **documento de autorización de jefatura adjunto obligatorio (PDF)**, editar y anular con invariantes de saldo; snapshot del factor; detalle y autorizado-por obligatorios; plaza opcional; **costura `overtimeRecordPublicId`** al futuro módulo de horas extras; asiento automático.
- **Ausencias**: crear (RRHH) con verificación transaccional de fondo y sugerencia de horas; editar y anular (restaura saldo); imputación opcional a periodo de planilla; asiento automático.
- **Fondo**: estado de cuenta por empleado con saldo corrido (RRHH + autogestión `isSelf`); saldo publicado en el perfil (campo aditivo).
- **Integración con liquidación**: query interna del saldo + línea automática `HORAS_EXTRAS_PENDIENTES` sugerida (unidades = horas; monto = horas × valor hora ordinaria × tarifa parametrizable, default 1.00), editable por el liquidador; retrocompatible sin saldo.
- **Bandeja + exportaciones**: query paginada de movimientos a nivel empresa (filtros por empleado, tipo, operación, estado, rango de fechas) y exportaciones xlsx/csv/json de movimientos y de **saldos por empleado**.
- **Carga inicial**: adopción vía acreditaciones con tipo propio de la empresa (p. ej. `SALDO_INICIAL`, sugerido en A.2), con el documento de respaldo del control anterior como adjunto.

### Fase 2 — Evoluciones (contrato preparado, fuera de este MVP)

- **Solicitud en línea del empleado** (goce y/o acreditación) con flujo de autorización de jefatura (`AuthorizeCompensatoryTime`), estados `SOLICITADA/APROBADA/RECHAZADA` aditivos y notificaciones (P-06).
- **Política refinada de pago o caducidad del saldo al retiro** (P-09 F2) y, si el negocio la introduce, **caducidad** del tiempo acreditado con consumo FIFO por tramos (P-04).
- **Elegibilidad formal** por plaza (P-05).
- **Vínculo activo con el módulo de horas extras** cuando se desarrolle (D-21: validación/poblado de `overtimeRecordPublicId`, prevención sistemática del doble beneficio).
- Integración con un futuro módulo de asistencia/marcaciones (validación automática de "fuera de jornada").

---

## 4. Fuera de alcance

- **Pago de horas extra y recargos en planilla** (Arts. 169-170/175 CT): este módulo administra **tiempo**; no calcula recargos del 100 %/50 % ni escribe transacciones de planilla (`PersonnelFilePayrollTransaction` pertenece a la sincronización externa). La decisión operativa "esta hora se paga vs se compensa" es del empleador; el módulo registra solo lo que se compensa. (La **única** valoración monetaria de F1 es la sugerencia al liquidar — D-19 — y la ejecuta el motor de liquidación.)
- **Control de asistencia, marcaciones, jornadas y turnos** (no existe y no se construye aquí — G-01).
- **Módulo de horas extras** (P-13): no existe aún; este módulo solo deja la costura declarada (`overtimeRecordPublicId`).
- **Flujo de autorizaciones en línea** y solicitud del empleado (F2, P-06).
- **Caducidad automática** del tiempo (descartada en F1 — P-04; entraría como evolución FIFO consciente).
- **Provisión financiera / valoración del saldo durante la relación laboral** (el fondo vive en horas; solo se valora al liquidar).
- Notificaciones por correo y recordatorios.
- Importador masivo de saldos históricos (la carga inicial usa acreditaciones del tipo de adopción, una a una o vía API).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador de empresa** | Crea el catálogo de tipos (inicia vacío — paso de adopción), configura preferencias (horas/día, tope, obligatoriedad del adjunto, tarifa de liquidación) y asigna permisos |
| **Gestor de RRHH** (con `ManageCompensatoryTime`) | Registra, edita y anula acreditaciones (con el documento de autorización) y ausencias; consulta estados de cuenta; exporta |
| **Consulta de RRHH / Auditor** (con `ViewCompensatoryTime`) | Consulta bandeja, fichas, estados de cuenta y exportaciones, sin escritura |
| **Empleado (autogestión)** | Consulta su propio estado de cuenta y saldo (F1 solo lectura); en F2 solicitará goces en línea |
| **Jefatura / Autorizador** | F1: autoriza el trabajo extra **mediante el documento que se adjunta** a la acreditación (se captura además como dato "autorizado por"); F2: autoriza solicitudes en línea |
| **Liquidador (módulo de liquidación)** | Recibe automáticamente la línea `HORAS_EXTRAS_PENDIENTES` sugerida con el saldo del empleado; puede ajustarla o removerla (editabilidad estándar) |
| **Finanzas / Contabilidad** | Consulta/exporta saldos por empleado (visibilidad del pasivo de tiempo); solo lectura |
| **Sistema de planilla externa** | Consume las exportaciones (ausencias justificadas del periodo; horas acreditadas no pagables como extra) |

---

## 6. Requerimientos funcionales

> Agrupados en 5 grupos (A: configuración y catálogo · B: acreditaciones · C: ausencias · D: fondo · E: transversales). RF-012 y RF-013 fueron **agregados en la ratificación** (P-11 y P-09). Prioridades: Alta = imprescindible F1; Media = F1 deseable.

### Grupo A — Configuración y catálogo

### RF-001 - Catálogo de tipos de tiempo compensatorio (maestro por empresa, inicia vacío)

**Descripción:** CRUD por empresa de los tipos de tiempo compensatorio: código, descripción, **operación** (`ACREDITA` / `DEBITA` / `AMBAS`), **factor de acreditación** (default 1.00) y baja lógica. **El maestro inicia vacío** (P-12): no hay plantilla, seeder ni `load-template`; la empresa crea sus tipos como paso de adopción (el Anexo A.2 sirve de referencia para el administrador).

**Reglas de negocio:**
- Código único por empresa (comparación normalizada); descripción obligatoria.
- Operación obligatoria: gobierna en qué formulario es seleccionable (RN-04).
- Factor > 0 (relevante solo si la operación acredita); default 1.00; su cambio **no** recalcula acreditaciones históricas (snapshot, RN-02/RN-09).
- Baja lógica: tipo inactivo no seleccionable en registros nuevos; los históricos conservan referencia y snapshot.
- Sin tipos creados, los formularios de acreditación/ausencia no pueden operar (el tipo es obligatorio) — documentar el paso de adopción en la guía FE.

**Criterios de aceptación:**
- CRUD completo con If-Match, `[ResourceActions]`/AllowedActions y auditoría.
- Registro nuevo con tipo inactivo/inexistente o de operación incompatible → 422 con código bilingüe.
- Tenant recién aprovisionado: maestro vacío (sin semilla).

**Prioridad:** Alta
**Dependencias:** Patrón de maestros por empresa governed (existe).

### RF-002 - Parametrización de tiempo compensatorio (preferencias de empresa)

**Descripción:** Nuevas preferencias tipadas en `CompanyPreference`: **horas estándar por día** para equivalencias (default 8), **tope máximo de saldo acumulable en horas** (P-10: null = indefinido/sin tope; si no, el valor que defina cada empresa), **obligatoriedad del documento de autorización** al acreditar (default sí — P-11) y **tarifa de valoración al liquidar** (default 1.00 — D-19/P-15).

**Reglas de negocio:**
- Columnas anulables con defaults (null → 8 horas/día; sin tope; adjunto obligatorio; tarifa 1.00).
- Editables solo por administrador vía la administración de preferencias existente.
- El tope, si está configurado, bloquea acreditaciones que dejarían el saldo por encima (RN-11).

**Criterios de aceptación:**
- La sugerencia de horas de ausencia usa las horas/día configuradas; acreditación que excede el tope → 422; acreditación sin adjunto con la preferencia activa → 422; la línea de liquidación usa la tarifa configurada — todo comprobable por tests de integración.

**Prioridad:** Alta
**Dependencias:** `CompanyPreference` (existe).

### RF-003 - Estados, tipos de acción y permisos del módulo

**Descripción:** Sembrar `COMPENSATORY_TIME_STATUS_CATALOG` (`REGISTRADA`, `ANULADA`), la representación de la operación (`ACREDITA`/`DEBITA`/`AMBAS` — TPH o constantes, plan técnico), los 2 tipos de acción de personal (`ACREDITACION_TIEMPO_COMPENSATORIO`, `GOCE_TIEMPO_COMPENSATORIO`) y declarar los permisos `PersonnelFiles.ViewCompensatoryTime` / `ManageCompensatoryTime`.

**Reglas de negocio:**
- Patrón híbrido (constantes canónicas + catálogo para i18n/UI).
- IDs de semilla en bloque nuevo **≤ -9865** (Anexo A.3), verificados contra `GlobalCatalogSeedData` al abrir el PR (no continuar -9485…-9489: -9490…-9496 ocupados por `ACTION_STATUS_CATALOG`).
- Permisos con receta completa (codes + provisioning + policies + gates fail-closed + governance tests), fallback `Admin`/`ManageAdministration`.

**Criterios de aceptación:**
- Migración `HasData` idempotente; catálogos visibles por wire-keys kebab-case; usuario sin permiso → 403 en cada endpoint de gestión; empleado autogestionado accede solo a lo propio.

**Prioridad:** Alta
**Dependencias:** D-13/D-14/D-15; verificación de IDs libres.

### Grupo B — Acreditaciones

### RF-004 - Crear acreditación de tiempo compensatorio

**Descripción:** Alta por RRHH sobre el expediente: tipo (que acredite), fecha trabajada, hora inicio/fin opcionales, horas trabajadas, factor aplicado (snapshot del tipo, ajustable con nota), horas acreditadas derivadas, detalle del trabajo, autorizado-por (texto + referencia opcional a expediente), **documento de autorización de jefatura adjunto (obligatorio — RF-012)**, plaza opcional y **referencia opcional al registro de hora extra de origen** (`overtimeRecordPublicId`, costura D-21).

**Reglas de negocio:**
- Solo `ManageCompensatoryTime` (sin rama self en F1 — D-01).
- Tipo activo con operación `ACREDITA`/`AMBAS` (RN-04); fecha trabajada no futura (RN-15); horas trabajadas > 0; detalle y autorizado-por obligatorios (RN-13).
- **Adjunto obligatorio** (RN-17): con la preferencia activa (default sí), el POST exige `filePublicId` con el propósito correcto; el documento se asocia en la misma transacción del alta (D-20).
- Horas acreditadas = trabajadas × factor, redondeo 2 decimales; ajuste manual solo con `isOverridden` + nota (RN-02).
- Si hay tope de saldo configurado: saldo resultante ≤ tope → si no, 422 (RN-11).
- `overtimeRecordPublicId`: opcional, **sin validación en F1** (el módulo de horas extras no existe — RN-19); se persiste y viaja en el contrato.
- Perfil `RETIRADO` → 422 (RN-10). Genera asiento `ACREDITACION_TIEMPO_COMPENSATORIO` (RF-010).
- Hora inicio/fin, si viajan ambas, deben ser coherentes (fin > inicio) — informativas, no recalculan horas.

**Criterios de aceptación:**
- POST → 201 con `publicId`, ETag y horas acreditadas calculadas; tipo con operación `DEBITA` → 422; sin adjunto (preferencia activa) → 422; saldo del estado de cuenta refleja el movimiento de inmediato.

**Prioridad:** Alta
**Dependencias:** RF-001, RF-002, RF-003, RF-012.

### RF-005 - Editar y anular acreditación (invariante de saldo)

**Descripción:** Edición de una acreditación `REGISTRADA` (fechas, horas, detalle — recalcula horas acreditadas) y **anulación** con motivo. Ninguna operación puede dejar el fondo descubierto.

**Reglas de negocio:**
- Solo `ManageCompensatoryTime`; If-Match obligatorio.
- Editar o anular re-verifica **dentro de la transacción**: saldo resultante ≥ 0 (los débitos ya registrados no pueden quedar sin respaldo) → 422 `…BALANCE_WOULD_GO_NEGATIVE` (RN-06).
- Anulación: baja lógica del efecto con motivo obligatorio, quién y cuándo; el registro queda trazable en el estado de cuenta como anulado (excluido del saldo) (RN-07). Los adjuntos permanecen trazables en el registro anulado.

**Criterios de aceptación:**
- Con saldo 4 h producto de una acreditación de 12 h y débitos por 8 h: anular la acreditación → 422; anularla tras anular la ausencia → 200 y saldo 0.

**Prioridad:** Alta
**Dependencias:** RF-004, RF-006.

### RF-012 - Documento de autorización de la acreditación *(agregado en ratificación, P-11/D-20)*

**Descripción:** Adjuntar a la acreditación el **documento de autorización de jefatura (PDF)** reutilizando el stack de archivos: upload-session SAS → complete con `purpose` → asociación con validación de propósito → read-url temporal. Obligatorio al crear (parametrizable, default sí).

**Reglas de negocio:**
- Nuevo `FilePurpose` (nombre final en plan técnico, p. ej. `CompensatoryTimeAuthorization`) con entrada `Storage:Purposes` en appsettings **BASE** (content-type PDF; gotcha conocido: configuración faltante → 422) y **contenedor pre-aprovisionado**.
- Documento con snapshot de nombre/tipo/tamaño, baja lógica; asociación validada por propósito y tenant.
- Obligatoriedad parametrizable por empresa, **default obligatorio** (P-11); permite adjuntos adicionales posteriores al alta.
- Solo la **acreditación** requiere adjunto; la ausencia no.

**Criterios de aceptación:**
- Adjuntar archivo con propósito distinto → 422 `InvalidPurpose`; read-url expira según configuración; alta sin adjunto con preferencia activa → 422.

**Prioridad:** Alta
**Dependencias:** RF-004; configuración de storage + contenedor (pendiente de despliegue).

### Grupo C — Ausencias

### RF-006 - Registrar ausencia por tiempo compensatorio con verificación de fondo

**Descripción:** Alta por RRHH: tipo (que debite), fecha inicio/fin, horas a debitar (con **sugerencia** calculada), motivo, imputación opcional a instancia de periodo de planilla. Verifica fondo suficiente.

**Reglas de negocio:**
- Solo `ManageCompensatoryTime` (F1); tipo activo con operación `DEBITA`/`AMBAS`.
- `startDate ≤ endDate`; fechas futuras permitidas (goce programado).
- Horas a debitar > 0 y ≤ **saldo disponible**, verificado al crear y **re-verificado dentro de la transacción** (RN-03; carrera entre débitos concurrentes → el segundo recibe 422).
- Sugerencia de horas = días del rango × horas estándar (RF-002), excluyendo el día de descanso del empleado (`restDayOfWeek`) y asuetos cuando REQ-001 esté disponible — editable por el registrador (la cifra final la decide RRHH).
- Sin solape con otra ausencia compensatoria vigente del mismo empleado; sin solape con incapacidades activas ni solicitudes/goces de vacaciones cuando REQ-001 esté presente (RN-05).
- Periodo de planilla: opcional (P-14); si viaja, debe existir y estar activo (imputación, no contención — mismo criterio que incapacidades).
- Perfil `RETIRADO` → 422. Genera asiento `GOCE_TIEMPO_COMPENSATORIO`.

**Criterios de aceptación:**
- POST con saldo suficiente → 201 y saldo decrementado; con saldo insuficiente → 422 con `saldoDisponible` y `horasFaltantes` en el detalle del error; solape → 422 con el registro en conflicto.

**Prioridad:** Alta
**Dependencias:** RF-002, RF-003, RF-004; REQ-001 (sugerencia y solapes cruzados — degradable).

### RF-007 - Editar y anular ausencia

**Descripción:** Edición de una ausencia `REGISTRADA` (fechas, horas, motivo — re-verifica fondo) y anulación con motivo (restaura el saldo automáticamente al ser derivado).

**Reglas de negocio:**
- Solo `ManageCompensatoryTime`; If-Match; edición re-verifica saldo y solapes dentro de la transacción.
- Anulación con motivo obligatorio; el movimiento queda trazable como anulado en el estado de cuenta (RN-07).

**Criterios de aceptación:**
- Anular una ausencia de 8 h → el saldo recupera exactamente 8 h; editar horas de 8→16 con saldo 10 → 422.

**Prioridad:** Alta
**Dependencias:** RF-006.

### Grupo D — Fondo (estado de cuenta)

### RF-008 - Estado de cuenta del fondo por empleado (RRHH y autogestión)

**Descripción:** Consulta cronológica de los movimientos del empleado — cada acreditación (+ horas acreditadas) y cada ausencia (− horas debitadas), con tipo, fechas, detalle, estado y **saldo corrido** — más totales: total acreditado, total debitado, **saldo disponible**. Incluye opcionalmente los movimientos anulados (marcados, excluidos del saldo).

**Reglas de negocio:**
- Lectura: `ViewCompensatoryTime` OR `isSelf` (el empleado solo su propio expediente).
- Saldo = Σ acreditadas − Σ debitadas de registros vigentes (RN-01); el saldo corrido se calcula sobre el orden cronológico (fecha del movimiento, desempate por creación).
- Paginado; filtros por rango de fechas, tipo y estado.

**Criterios de aceptación:**
- Los totales cuadran contra los movimientos en todos los tests de integración; empleado vinculado ve lo suyo, otro expediente → 403.

**Prioridad:** Alta
**Dependencias:** RF-004, RF-006.

### RF-009 - Saldo publicado en el perfil del empleado

**Descripción:** Nuevo campo **aditivo** `compensatoryTimeHoursAvailable` (nullable) en la respuesta del perfil (P-08 ratificada), calculado en la proyección (sin columnas nuevas), junto a los saldos de vacaciones/incapacidades de REQ-001.

**Reglas de negocio:**
- `null` cuando el empleado no tiene movimientos (documentado en guía FE); nunca negativo (invariantes del fondo).
- Cambio de contrato **aditivo** (no-breaking); openapi regenerado.

**Criterios de aceptación:**
- Perfil de empleado con movimientos muestra el saldo real y cuadra con el estado de cuenta (mismo módulo de reglas — cuadre por construcción).

**Prioridad:** Media
**Dependencias:** RF-008.

### Grupo E — Transversales

### RF-010 - Asiento automático en expediente

**Descripción:** Toda acreditación y toda ausencia registrada genera una acción de personal (`ACREDITACION_TIEMPO_COMPENSATORIO` / `GOCE_TIEMPO_COMPENSATORIO`) con sus fechas de vigencia, en la misma transacción.

**Reglas de negocio:**
- Códigos nuevos sobre `ACTION_TYPE_CATALOG` (D-14); anulaciones trazables en el registro fuente (sin ActionType propio, mismo corte que REQ-001).
- El **estado del empleado** (`EmploymentStatusCode`) **no** cambia automáticamente (precedente P-18 de REQ-001).

**Criterios de aceptación:**
- Registrar acreditación/ausencia → aparece la acción correspondiente en el expediente con las fechas del registro.

**Prioridad:** Alta
**Dependencias:** RF-003, RF-004, RF-006.

### RF-011 - Bandeja de empresa + exportaciones

**Descripción:** (a) Query paginada de **movimientos** a nivel empresa (filtros: empleado, tipo, operación, estado, rango de fechas; `StatusCounts`); (b) exportación xlsx/csv/json de movimientos (insumo para planilla externa: ausencias justificadas del periodo, horas acreditadas no pagables) y (c) exportación de **saldos por empleado** (visibilidad del pasivo de tiempo para RRHH/Finanzas).

**Reglas de negocio:**
- `ViewCompensatoryTime` para query/export; export con rate limiting y límite síncrono existente; filas con propiedades en español (patrón liquidación).
- La exportación de movimientos incluye el periodo de planilla imputado (etiqueta + fechas) cuando existe; los anulados se excluyen por defecto (filtro documentado).

**Criterios de aceptación:**
- `POST /companies/{companyId}/compensatory-time/query` pagina y cuenta por estado; `GET …/export` y `GET …/balances/export` respetan filtros y formatos.

**Prioridad:** Alta (bandeja/movimientos) / Media (saldos)
**Dependencias:** RF-004, RF-006, RF-008.

### RF-013 - Integración con liquidación: saldo pendiente en el cálculo automático *(agregado en ratificación, P-09/D-19)*

**Descripción:** Al generar (o regenerar) la liquidación de un empleado, el motor consulta el **saldo de horas de tiempo compensatorio pendiente** y, si es > 0, agrega automáticamente la línea **`HORAS_EXTRAS_PENDIENTES`** al cálculo: unidades = horas del saldo; monto sugerido = horas × valor hora ordinaria (salario base mensual / 30 / horas estándar por día) × tarifa parametrizable (default 1.00 — P-15).

**Reglas de negocio:**
- Query interna de este módulo (`GetPendingCompensatoryTimeHoursAsync` o equivalente): saldo vigente al momento del cálculo; la valoración monetaria la ejecuta el **motor de liquidación** (D-12: el fondo permanece en horas).
- La línea conserva la editabilidad estándar del liquidador (`UnitsOrFactorUsed` + `IsOverridden` + descripción); puede ajustarse o removerse antes de emitir.
- Sin saldo o sin datos del módulo → **sin línea automática** (retrocompatible: la línea manual `HORAS_EXTRAS_PENDIENTES` sigue disponible como hasta hoy).
- El saldo usado y su valoración quedan **persistidos como snapshot** en la liquidación (auditable); la emisión de la liquidación no debita el fondo (el expediente queda `RETIRADO` y bloqueado — el saldo histórico permanece trazable).
- Redondeo: convención de liquidación (2 decimales, AwayFromZero), un solo helper.

**Criterios de aceptación:**
- Liquidación de empleado con saldo 12 h y salario $600 (8 h/día, tarifa 1.00) → línea automática de $30.00 editable; sin saldo → sin línea y suite de liquidación existente intacta (test de retrocompatibilidad en ambos sentidos).
- El override del liquidador sobrevive a la regeneración (mismo comportamiento que las demás líneas).

**Prioridad:** Alta
**Dependencias:** RF-004/RF-006/RF-008 (saldo); módulo de liquidación (mergeado, PR #56); RF-002 (tarifa).

---

## 7. Requerimientos no funcionales

- **Seguridad**: dato laboral propio (no dato de salud): lectura con `ViewCompensatoryTime` o `isSelf` (403 sin enmascaramiento para terceros); escrituras manage-only en F1; gates fail-closed por handler además de la política de controlador; adjuntos con URLs SAS temporales y validación de propósito.
- **Auditoría**: `CreatedUtc`/`ModifiedUtc`, quién registró/anuló y cuándo, motivo obligatorio en anulaciones, snapshot del factor aplicado y del saldo valorado en liquidación, baja lógica universal (nunca borrado físico), asientos automáticos en expediente.
- **Concurrencia/API**: convenciones del repo — `api/v1`, If-Match obligatorio (faltante → 400, obsoleto → 409), token rotativo, DELETE → `parentConcurrencyToken`, Guid `publicId`, enums como strings, errores bilingües en `extensions.code`. **La verificación de saldo se re-ejecuta dentro de la transacción de escritura** (débitos concurrentes no pueden sobre-girar el fondo).
- **Rendimiento**: saldo derivado con índices por `(tenant, expediente, estado)`; estado de cuenta paginado; sin trabajos nocturnos (el saldo nunca se materializa); la query de saldo para liquidación es una agregación simple (sin impacto medible en la generación).
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId` en todas las entidades.
- **Usabilidad**: errores accionables (saldo disponible y horas faltantes en el 422 de fondo insuficiente); sugerencia de horas editable; catálogo con `sortOrder`; línea de liquidación sugerida pero siempre editable.
- **Mantenibilidad**: reglas en módulo puro (`CompensatoryTimeRules`) con tests unitarios y **paridad de localización**; sin números de política codificados (factor/horas-día/tope/tarifa en catálogo y preferencias); OpenAPI sin drift.
- **Compatibilidad**: el campo del perfil es aditivo; la integración con liquidación es retrocompatible (sin saldo → comportamiento actual); el módulo no rompe contratos existentes.
- **Accesibilidad**: (frontend) estado de cuenta navegable y con totales textuales; se documenta en la guía FE.

---

## 8. Historias de usuario

### HU-001 - Acreditar tiempo compensatorio con autorización documentada
Como **gestor de RRHH**, quiero **registrar el tiempo compensatorio autorizado a un empleado que laboró fuera de su jornada, adjuntando la autorización de jefatura**, para **que quede en su fondo con constancia de qué trabajó, quién lo autorizó y con qué documento**.

**Criterios de aceptación:**
- Dado un tipo que acredita con factor 1.00 y el PDF de autorización subido con propósito válido, cuando registro 3 horas trabajadas, entonces se acreditan 3.00 horas, el documento queda asociado y se asienta `ACREDITACION_TIEMPO_COMPENSATORIO` en el expediente.
- Dado que intento registrar sin adjunto (preferencia obligatoria por defecto), entonces recibo 422.
- Dado un tipo con factor 2.00 (p. ej. asueto), cuando registro 4 horas, entonces se acreditan 8.00 horas con el factor en snapshot.

### HU-002 - Registrar ausencia con verificación de fondo
Como **gestor de RRHH**, quiero **registrar una ausencia por tiempo compensatorio**, para **que el goce quede descontado del fondo solo si el empleado tiene saldo**.

**Criterios de aceptación:**
- Dado saldo 12 h, cuando registro una ausencia de 8 h, entonces queda `REGISTRADA`, el saldo pasa a 4 h y se asienta `GOCE_TIEMPO_COMPENSATORIO`.
- Dado saldo 4 h, cuando intento registrar 8 h, entonces recibo 422 con mi saldo disponible y las horas faltantes.

### HU-003 - Consultar mi estado de cuenta
Como **empleado autogestionado**, quiero **ver mis acreditaciones, mis ausencias y mi saldo disponible**, para **saber cuánto tiempo compensatorio tengo a mi favor**.

**Criterios de aceptación:**
- Dado mi usuario vinculado, cuando consulto mi fondo, entonces veo los movimientos con saldo corrido y el saldo final; si consulto otro expediente, recibo 403.

### HU-004 - Consultar el fondo en la ficha
Como **gestor de RRHH**, quiero **ver el estado de cuenta del empleado en su ficha**, para **decidir si procede otorgar una ausencia o revisar su historial**.

**Criterios de aceptación:**
- Dado un empleado con movimientos, cuando abro su fondo, entonces los totales (acreditado, debitado, disponible) cuadran con los movimientos listados.

### HU-005 - Anular una acreditación sin descubrir el fondo
Como **gestor de RRHH**, quiero **anular una acreditación registrada por error**, para **corregir el fondo sin dejar ausencias sin respaldo**.

**Criterios de aceptación:**
- Dada una acreditación cuyo retiro dejaría el saldo negativo, cuando intento anularla, entonces recibo 422.
- Dada una acreditación anulable, cuando la anulo con motivo, entonces el saldo se reduce y el movimiento queda trazable como anulado (con sus adjuntos).

### HU-006 - Anular una ausencia
Como **gestor de RRHH**, quiero **anular una ausencia que no se ejecutó**, para **que el empleado recupere sus horas**.

**Criterios de aceptación:**
- Dada una ausencia `REGISTRADA` de 8 h, cuando la anulo con motivo, entonces el saldo recupera exactamente 8 h.

### HU-007 - Crear el catálogo de tipos de mi empresa
Como **administrador de empresa**, quiero **definir los tipos de tiempo compensatorio con su operación y factor**, para **que los formularios de acreditación y ausencia usen mi política institucional**.

**Criterios de aceptación:**
- Dado un tenant recién aprovisionado, cuando consulto el maestro, entonces está vacío y debo crear los tipos antes de registrar movimientos.
- Dado un tipo con operación `ACREDITA`, cuando intento usarlo en una ausencia, entonces recibo 422.

### HU-008 - Exportar movimientos y saldos
Como **analista de planilla / Finanzas**, quiero **exportar las ausencias compensatorias del periodo y los saldos por empleado**, para **no descontar ausencias justificadas y conocer el pasivo de tiempo acumulado**.

**Criterios de aceptación:**
- Dado un filtro por rango/periodo, cuando exporto movimientos, entonces obtengo xlsx/csv/json con empleado, tipo, fechas, horas y periodo imputado (excluyendo anulados por defecto).

### HU-009 - Liquidar con el saldo incluido automáticamente
Como **liquidador**, quiero **que la liquidación incluya automáticamente el saldo pendiente de tiempo compensatorio como línea de horas extras pendientes**, para **pagar las horas compensatorias no gozadas sin depender de un cálculo manual**.

**Criterios de aceptación:**
- Dado un empleado con saldo 12 h y salario $600 (8 h/día, tarifa 1.00), cuando genero su liquidación, entonces aparece la línea `HORAS_EXTRAS_PENDIENTES` con 12 unidades y $30.00 sugeridos, editable.
- Dado un empleado sin saldo, cuando genero su liquidación, entonces no aparece línea automática y el comportamiento actual se conserva.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | Saldo del fondo = Σ horas acreditadas − Σ horas debitadas de registros **vigentes** (`REGISTRADA`, activos); es **derivado** (nunca se materializa) y **por empleado** (D-03) |
| RN-02 | Horas acreditadas = horas trabajadas × **factor del tipo** (snapshot al registrar); ajuste manual solo con `isOverridden` + nota; cambios posteriores del catálogo no recalculan históricos |
| RN-03 | Toda ausencia exige horas ≤ saldo disponible, verificado al validar **y re-verificado dentro de la transacción** de escritura (sin sobregiro por carrera) |
| RN-04 | La operación del tipo gobierna su uso: `ACREDITA` solo en acreditaciones, `DEBITA` solo en ausencias, `AMBAS` en ambos formularios |
| RN-05 | Sin solape entre ausencias compensatorias vigentes del mismo empleado; sin solape con incapacidades activas ni goces/solicitudes vivas de vacaciones cuando REQ-001 esté presente |
| RN-06 | Anular o editar una **acreditación** no puede dejar el saldo negativo (los débitos registrados no quedan descubiertos) → 422 |
| RN-07 | Las anulaciones no borran: baja lógica del efecto con motivo obligatorio, quién y cuándo; el movimiento anulado sigue visible (marcado) en el estado de cuenta |
| RN-08 | Toda acreditación y toda ausencia genera asiento automático en las acciones de personal, en la misma transacción |
| RN-09 | Tipos inactivos no son seleccionables en registros nuevos; los históricos conservan referencia y snapshot |
| RN-10 | Perfil `RETIRADO` bloqueado: no admite registros nuevos del módulo (precedente RN-18 de REQ-001) |
| RN-11 | Si la empresa configura tope de saldo (P-10: null = indefinido), una acreditación que lo excedería → 422 con el máximo acreditable |
| RN-12 | El fondo se administra en **horas** (D-02); la única valoración monetaria de F1 es la sugerencia en la liquidación (RN-18/D-19), ejecutada por el motor de liquidación |
| RN-13 | La acreditación es **declarativa**: detalle del trabajo, "autorizado por" y **documento de autorización adjunto** obligatorios; la veracidad es responsabilidad del registrador/autorizador (no hay marcaciones) |
| RN-14 | **No doble beneficio**: las horas acreditadas como tiempo compensatorio no deben además pagarse como hora extra en planilla (el concepto de ingreso `HORAS_EXTRA` del catálogo de compensación es la vía del pago — frontera del módulo); el módulo exporta lo acreditado para que la planilla externa lo excluya (control operativo del empleador; control sistemático llegará con el módulo de horas extras vía D-21) |
| RN-15 | Fecha trabajada de la acreditación ≤ hoy (no se acredita trabajo futuro); las ausencias sí pueden ser futuras (goce programado) |
| RN-16 | El estado del empleado no cambia automáticamente por una ausencia compensatoria (precedente P-18 de REQ-001) |
| RN-17 | **Adjunto de autorización obligatorio** al acreditar (P-11): PDF con propósito dedicado, asociado en la misma transacción del alta; parametrizable por empresa (default obligatorio) |
| RN-18 | **El saldo al retiro se paga** (G-07/P-09): la liquidación agrega automáticamente la línea `HORAS_EXTRAS_PENDIENTES` con el saldo (unidades = horas; monto = horas × valor hora ordinaria × tarifa parametrizable, default 1.00), editable por el liquidador; el snapshot queda persistido en la liquidación; sin saldo → sin línea (retrocompatible) |
| RN-19 | **Costura al módulo de horas extras** (P-13): `overtimeRecordPublicId` opcional en la acreditación, sin FK dura ni validación en F1; el módulo futuro la validará/poblará |

---

## 10. Flujos principales

### Flujo 1 — Acreditar tiempo compensatorio
1. RRHH abre la ficha del empleado y elige "Nueva acreditación".
2. Sube el **documento de autorización de jefatura (PDF)** vía upload-session (propósito dedicado).
3. Ingresa tipo (catálogo, operación que acredita), fecha trabajada, horas trabajadas, hora inicio/fin (opcional), detalle del trabajo y quién lo autorizó (+ plaza opcional; + referencia a hora extra si existiera).
4. El sistema valida catálogo/operación, fecha no futura, tope de saldo (si configurado) y el adjunto; calcula horas acreditadas = horas × factor (ajustable con nota).
5. Guarda con snapshot del factor, asocia el documento en la misma transacción, crea el asiento `ACREDITACION_TIEMPO_COMPENSATORIO` y devuelve 201 con ETag.
6. El estado de cuenta y el saldo del perfil reflejan el movimiento.

### Flujo 2 — Registrar ausencia por tiempo compensatorio
1. RRHH elige "Nueva ausencia por tiempo compensatorio" en la ficha.
2. Ingresa tipo (que debita), fechas inicio/fin y motivo; el sistema **sugiere** las horas (días × horas estándar, excluyendo descanso/asuetos si REQ-001 está presente); RRHH confirma o ajusta las horas; imputa opcionalmente el periodo de planilla.
3. El sistema valida solapes y **verifica el fondo** (horas ≤ saldo) re-chequeando dentro de la transacción.
4. Guarda, crea el asiento `GOCE_TIEMPO_COMPENSATORIO` y devuelve 201.
5. El saldo disminuye; la exportación del periodo la incluirá como ausencia justificada.

### Flujo 3 — Consultar el estado de cuenta (autogestión)
1. El empleado entra a "Mi tiempo compensatorio".
2. El sistema lista sus movimientos en orden cronológico con saldo corrido y muestra los totales y el saldo disponible.
3. El empleado planifica su goce (y en F2 lo solicitará en línea).

### Flujo 4 — Anular una acreditación
1. RRHH abre la acreditación y elige "Anular" con motivo.
2. El sistema verifica que el saldo resultante no quede negativo (RN-06).
3. Anula (baja lógica del efecto), deja trazabilidad (incluidos adjuntos) y el saldo se recalcula.

### Flujo 5 — Anular una ausencia
1. RRHH abre la ausencia y elige "Anular" con motivo (p. ej. el goce no se ejecutó).
2. El sistema anula y el saldo recupera las horas automáticamente (derivado).

### Flujo 6 — Adopción: crear el catálogo y la configuración
1. El administrador crea los tipos de su empresa (el maestro **inicia vacío** — puede apoyarse en el catálogo de referencia del Anexo A.2), con operación y factor propios.
2. Configura horas estándar por día, tope de saldo (opcional), obligatoriedad del adjunto y tarifa de liquidación.
3. Hasta que existan tipos, los formularios de acreditación/ausencia no pueden operar (tipo obligatorio).

### Flujo 7 — Carga inicial de saldos históricos
1. RRHH registra una acreditación con el tipo de adopción de la empresa (p. ej. `SALDO_INICIAL`) por empleado: horas = saldo migrado; detalle = "saldo al DD/MM/AAAA según control anterior"; adjunto = acta/planilla del control anterior (PDF).
2. El estado de cuenta arranca con ese movimiento como primer crédito.

### Flujo 8 — Liquidación con saldo pendiente
1. El liquidador genera la liquidación del empleado retirado (flujo existente del módulo de liquidación).
2. El motor consulta el saldo de horas del fondo; con saldo > 0 agrega la línea `HORAS_EXTRAS_PENDIENTES`: unidades = horas, monto = horas × valor hora ordinaria × tarifa (default 1.00).
3. El liquidador revisa, ajusta o remueve la línea (editabilidad estándar) y emite; el snapshot del saldo valorado queda persistido.

---

## 11. Flujos alternativos y excepciones

- **Fondo insuficiente** al crear/editar ausencia → 422 con saldo disponible y horas faltantes.
- **Carrera de débitos concurrentes** (dos ausencias contra el mismo saldo) → la segunda transacción re-verifica y falla con 422.
- **Anulación/edición de acreditación que descubriría débitos** → 422 `BALANCE_WOULD_GO_NEGATIVE` con el máximo ajustable.
- **Alta de acreditación sin documento de autorización** (preferencia activa, default) → 422.
- **Adjunto con propósito incorrecto, tamaño o tipo no permitido** → 422 según `Storage:Purposes`; **configuración de storage faltante** → 422 (gotcha documentado).
- **Tipo con operación incompatible** (acreditar con `DEBITA` o debitar con `ACREDITA`) → 422.
- **Tipo/periodo de planilla inactivo o inexistente** → 422 por código.
- **Maestro de tipos vacío** (empresa sin adopción completada): los formularios no pueden enviarse (tipo obligatorio) — documentado en guía FE como paso previo.
- **Solape** de ausencia compensatoria con otra vigente (o con incapacidad/vacación activa, si REQ-001 presente) → 422 con el registro en conflicto.
- **Tope de saldo excedido** al acreditar → 422 con el máximo acreditable.
- **Fecha trabajada futura** en acreditación → 422.
- **Ajuste de horas acreditadas sin nota** → 422 (RN-02).
- **Empleado `RETIRADO`** → 422 en todo registro nuevo.
- **Anulación sin motivo** → 422.
- **If-Match ausente** → 400; **obsoleto** → 409.
- **Usuario sin permiso** → 403 en gestión; **empleado consultando otro expediente** → 403.
- **Edición de un registro `ANULADA`** → 422/409 (transición inválida).
- **Liquidación sin saldo de tiempo compensatorio** → sin línea automática (no es error; comportamiento actual intacto).
- **REQ-001 ausente** (modo degradado): la sugerencia usa días calendario × horas estándar y no valida solapes cruzados — documentado en guía FE.

---

## 12. Datos requeridos

> Convenciones del repo aplican a todas las entidades: `long Id` interno + `Guid publicId` externo, `TenantId`, `CreatedUtc`/`ModifiedUtc`, `isActive` (baja lógica), `concurrencyToken` rotativo (If-Match), factoría `Create(...)` + mutadores custodiados. Se listan solo los campos de negocio.

### Entidad: Tipo de tiempo compensatorio (`CompensatoryTimeType` — maestro por empresa, **sin semilla: inicia vacío**)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Código único por empresa (normalizado) | Identificador y descripción del tipo |
| operationCode | Código | Sí | `ACREDITA`/`DEBITA`/`AMBAS` | En qué formulario es utilizable (RN-04) |
| creditFactor | Decimal (5,2) | Sí | > 0; default 1.00 | Factor de acreditación (horas acreditadas por hora trabajada) |
| sortOrder | Entero | Sí | — | Orden de presentación |

### Entidad: Acreditación de tiempo compensatorio (`PersonnelFileCompensatoryTimeCredit` — sub-registro del expediente)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| compensatoryTimeTypePublicId | Guid | Sí | Tipo activo, operación `ACREDITA`/`AMBAS` | Tipo de tiempo compensatorio |
| workDate | Fecha | Sí | ≤ hoy (RN-15) | Fecha en que se laboró fuera de jornada |
| startTime / endTime | Hora | No | Coherentes si viajan ambas (informativas) | Rango horario del trabajo |
| hoursWorked | Decimal (5,2) | Sí | > 0 | Horas laboradas fuera de jornada |
| factorApplied | Decimal (5,2) | Sí (snapshot) | > 0; default del tipo | Factor usado (snapshot, RN-02) |
| hoursCredited | Decimal (6,2) | Derivado | = hoursWorked × factorApplied (2 dec); editable con `isOverridden` + nota | Horas que entran al fondo |
| isOverridden / overrideNote | Booleano / Texto (300) | Condicional | Nota obligatoria si hay ajuste | Marca de ajuste manual |
| workDetail | Texto (500) | Sí | No vacío (RN-13) | Qué trabajo se realizó |
| authorizedByText | Texto (200) | Sí | No vacío (RN-13) | Quién autorizó el trabajo extra |
| authorizerFilePublicId | Guid | No | Expediente del tenant, si viaja | Referencia opcional al autorizador |
| assignedPositionPublicId | Guid | No | Plaza del empleado | Plaza donde se laboró (opcional) |
| overtimeRecordPublicId | Guid | No | **Sin validación en F1** (RN-19, D-21) | Costura: registro de hora extra de origen (módulo futuro) |
| registeredByUserId | Guid | Sí | Usuario autenticado | Quién digitó |
| statusCode | Código catálogo | Sí | `REGISTRADA`/`ANULADA` (híbrido) | Estado |
| annulmentReason / annulledByUserId / annulledUtc | Texto (300) / Guid / Fecha | Al anular | Motivo obligatorio | Auditoría de anulación |
| notes | Texto (500) | No | — | Observaciones |

### Entidad: Documento de acreditación (`PersonnelFileCompensatoryTimeCreditDocument` — hija de la acreditación, D-20)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| filePublicId | Guid | Sí | `StoredFile` activo, mismo tenant, propósito dedicado (PDF) | Referencia al archivo subido (autorización de jefatura) |
| fileName / contentType / sizeBytes | Texto/Texto/Entero | Sí | Snapshot al asociar | Metadatos del archivo |
| observaciones | Texto (500) | No | — | Notas |

### Entidad: Ausencia por tiempo compensatorio (`PersonnelFileCompensatoryTimeAbsence` — sub-registro del expediente)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| compensatoryTimeTypePublicId | Guid | Sí | Tipo activo, operación `DEBITA`/`AMBAS` | Tipo de tiempo compensatorio |
| startDate / endDate | Fecha | Sí | inicio ≤ fin; futuras permitidas | Rango de la ausencia |
| hoursDebited | Decimal (6,2) | Sí | > 0; ≤ saldo disponible (RN-03) | Horas debitadas del fondo (sugeridas, editables) |
| reason | Texto (500) | Sí | No vacío | Motivo/detalle del goce |
| payrollPeriodPublicId | Guid | No | Instancia activa del maestro de REQ-001, si viaja | Imputación a periodo de planilla (P-14) |
| registeredByUserId | Guid | Sí | Usuario autenticado | Quién digitó |
| statusCode | Código catálogo | Sí | `REGISTRADA`/`ANULADA` (híbrido) | Estado |
| annulmentReason / annulledByUserId / annulledUtc | Texto (300) / Guid / Fecha | Al anular | Motivo obligatorio | Auditoría de anulación |
| notes | Texto (500) | No | — | Observaciones |

### Consulta derivada: Estado de cuenta (`CompensatoryTimeStatement` — no persistida)

| Campo | Tipo de dato | Descripción |
|---|---|---|
| movimientos[] | Lista | Unión cronológica de acreditaciones (+) y ausencias (−): fecha, tipo, operación, detalle, horas ±, estado, saldo corrido |
| totalAcreditado / totalDebitado | Decimal | Totales de registros vigentes |
| saldoDisponible | Decimal | totalAcreditado − totalDebitado (≥ 0 por invariantes) |

### Preferencias de empresa (columnas nuevas en `CompanyPreference`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| compensatoryTimeStandardDailyHours | Decimal (4,2) | No | > 0; null = 8 | Horas estándar por día para equivalencias (D-02/P-02) |
| compensatoryTimeMaxBalanceHours | Decimal (6,2) | No | > 0; null = **indefinido/sin tope** (P-10) | Tope de saldo acumulable (RN-11) |
| compensatoryTimeCreditRequiresDocument | Booleano | No | null = **sí** (P-11) | Adjunto de autorización obligatorio al acreditar (D-20) |
| compensatoryTimeSettlementRateFactor | Decimal (5,2) | No | > 0; null = 1.00 (P-15 — segunda ronda) | Tarifa aplicada al valorar el saldo en la liquidación (D-19) |

### Snapshot en liquidación (campos del módulo de liquidación — RF-013)

| Campo | Tipo de dato | Descripción |
|---|---|---|
| compensatoryTimeHoursUsed | Decimal | Saldo de horas tomado del fondo al calcular (snapshot auditable) |
| (línea `HORAS_EXTRAS_PENDIENTES`) | Línea de liquidación | Unidades/monto sugeridos, con la editabilidad estándar (`UnitsOrFactorUsed` + `IsOverridden`) |

### Catálogos generales nuevos (semilla `HasData`, IDs tentativos ≤ -9865 — Anexo A.3)

- `COMPENSATORY_TIME_STATUS_CATALOG` (wire `compensatory-time-statuses`): `REGISTRADA`, `ANULADA`.
- Operación del tipo: `ACREDITA` / `DEBITA` / `AMBAS` (TPH `compensatory-time-operations` o constantes — plan técnico).
- Nuevos códigos en `ACTION_TYPE_CATALOG`: `ACREDITACION_TIEMPO_COMPENSATORIO`, `GOCE_TIEMPO_COMPENSATORIO`.
- **Sin semillas de tipos de tiempo compensatorio** (P-12: maestro por empresa vacío).

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Módulo de liquidación** | **Interna — F1 (RF-013/D-19)** | Query interna del saldo de horas → línea automática `HORAS_EXTRAS_PENDIENTES` (`SettlementCalculation.Rules.cs:657`) con unidades = horas y monto = horas × valor hora ordinaria × tarifa (default 1.00); editable; snapshot persistido; retrocompatible sin saldo. F2: refinamiento de la política de pago/caducidad al retiro |
| **Azure Blob Storage (SAS)** | Existente — F1 (RF-012/D-20) | Documento de autorización de jefatura vía upload-session + `Storage:Purposes` (propósito nuevo, PDF) + contenedor pre-aprovisionado (pendiente de despliegue) |
| **Planilla externa** | Saliente (archivos) | Export de movimientos: ausencias compensatorias del periodo (**justificadas y pagadas — no descontar**) y horas acreditadas (**no pagar como extra** — RN-14). No se escribe `PersonnelFilePayrollTransaction` (ledger de sincronización externa) |
| **REQ-001 (asuetos, `restDayOfWeek`, periodos de planilla)** | Interna | Sugerencia de horas a debitar (excluir descanso/asuetos), imputación opcional de la ausencia al periodo (P-14), solapes cruzados con incapacidades/vacaciones (D-18) |
| **Módulo de horas extras (futuro)** | Costura declarada (D-21) | `overtimeRecordPublicId` opcional en la acreditación, sin validación en F1; el módulo futuro validará/poblará el vínculo y sistematizará el no-doble-beneficio |
| **Perfil del empleado** | Interna | Nuevo campo aditivo `compensatoryTimeHoursAvailable` en la proyección (RF-009) |
| **Expediente (acciones de personal)** | Interna | Asientos automáticos (RF-010) |
| **Sustitución de autorizaciones** | Correlación funcional | El motivo `PERMISO` del módulo existente cubre la delegación durante la ausencia; sin acople técnico |
| **Correo / notificaciones** | Fase 2 | Avisos de solicitud/decisión del flujo en línea |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de empresa | Catálogo de tipos (creación inicial — maestro vacío), preferencias; asignación de permisos | Las escrituras de registros requieren `ManageCompensatoryTime` (fallback Admin aplica según receta estándar) |
| Gestor de RRHH | `PersonnelFiles.ManageCompensatoryTime` (acreditar con adjunto, registrar/editar/anular ausencias) + lecturas implícitas | Auditoría plena de sus escrituras; anulaciones siempre con motivo |
| Consulta / Auditor | `PersonnelFiles.ViewCompensatoryTime` | Solo lectura: bandeja, fichas, estados de cuenta, exportaciones |
| Empleado (autogestión) | Sin permisos RBAC: gate `isSelf` por `LinkedUserPublicId` | Solo su expediente y **solo lectura** en F1 (estado de cuenta y saldo) |
| Jefatura / Autorizador (F2) | `PersonnelFiles.AuthorizeCompensatoryTime` (RequireAssertion que excluye Admin) | F1: autoriza vía documento adjunto (fuera del sistema); F2: decide solicitudes en línea; anti-autoaprobación |
| Liquidador | Permisos del módulo de liquidación (existentes) | Recibe la línea automática; puede ajustarla/removerla (editabilidad estándar de liquidación) |
| Finanzas / Planilla externa | Credencial con `ViewCompensatoryTime` | Solo lectura/exportación |

---

## 15. Criterios de aceptación generales

1. ✅ **Ratificación completada (2026-07-05)**: D-01…D-21 aprobadas y P-01…P-14 respondidas — el plan técnico puede iniciar. Única pendiente menor: P-15 (tarifa de liquidación), cubierta por el default parametrizable 1.00.
2. Reglas del fondo como **módulo puro** (`CompensatoryTimeRules`: saldo, factor, sugerencia, invariantes, valoración para liquidación) con suite unitaria de casos dorados (Anexo A.4, ajustados a la ratificación) y test de paridad de localización.
3. Suite de integración completa (CRUD, gates, invariantes de saldo con carrera de débitos, solapes, anulaciones, adjunto obligatorio, autogestión de lectura, línea automática de liquidación con retrocompatibilidad) **en verde junto con la suite existente** (línea base al arrancar: la vigente tras REQ-001).
4. Migraciones `HasData` idempotentes con IDs verificados contra `GlobalCatalogSeedData` (bloque ≤ -9865; **no** -9490…-9496, ocupados por `ACTION_STATUS_CATALOG`); **sin semillas de tipos** (maestro vacío).
5. `openapi.yaml` regenerado **sin drift**; convenciones API respetadas (If-Match, `publicId`, enums string, errores `extensions.code` bilingües, DELETE → `parentConcurrencyToken`).
6. Campo del perfil publicado como cambio aditivo, cuadrando por construcción con el estado de cuenta.
7. Bandeja paginada con `StatusCounts` y exportaciones xlsx/csv/json (movimientos y saldos) con rate limiting.
8. Asientos automáticos verificados para los 2 tipos de acción nuevos.
9. Adjuntos operando de punta a punta con `Storage:Purposes` configurado en appsettings **base** y contenedor aprovisionado (verificado en despliegue — gotcha conocido).
10. Integración con liquidación verificada en ambos sentidos: con saldo → línea sugerida correcta (editable, snapshot persistido); sin saldo → suite existente de liquidación intacta.
11. Guía de integración frontend publicada (`guia-integracion-frontend-tiempo-compensatorio.md`) con contratos, estados, flujos y el **paso de adopción** (crear tipos + preferencias antes de operar).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Doble beneficio** (pagar la hora extra en planilla Y acreditarla como tiempo): el sistema no controla la planilla externa; mitigación: export de horas acreditadas + regla operativa documentada (RN-14) + capacitación al cliente; el control sistemático llegará con el módulo de horas extras (costura D-21).
- **Acreditaciones infladas** sin control de asistencia: el registro es declarativo; mitigación: detalle + "autorizado por" + **documento de autorización adjunto obligatorio** (P-11), auditoría, bandeja/export para revisión, y tope opcional de saldo (RN-11).
- **Tarifa de liquidación sin confirmar** (P-15): el default 1.00 (valor hora ordinaria) podría no reflejar la política del cliente (¿recargo?); mitigación: preferencia parametrizable + línea siempre editable por el liquidador + confirmación en segunda ronda antes del despliegue. Nota: si el factor de acreditación del tipo ya incorporó el recargo en tiempo (p. ej. 2.00 en asueto), la tarifa ordinaria evita duplicar el recargo en dinero.
- **Introducir caducidad con saldos vivos** (P-04 descartada en F1): si a futuro se ratifica caducidad, migrar el ledger simple a FIFO con datos en producción tiene costo; mitigación: decisión documentada como evolución consciente (D-04), revisar antes de acumular saldos masivos.
- **REQ-001 pospuesto**: la sugerencia y los solapes cruzados degradan; mitigación: modo degradado documentado (D-18) y secuenciación en el backlog (la integración con liquidación NO depende de REQ-001).
- **Saldos acumulados sin política de tope** que se vuelven pasivo significativo (y pagadero al retiro — RN-18); mitigación: export de saldos para Finanzas + tope opcional por empresa (P-10) + visibilidad del pasivo.
- **Expectativa de descuento/pago automático en planilla mensual**: no hay motor de planilla; mitigación: comunicar D-12/G-06 y entregar exportaciones exactas (el único pago automático es el de la liquidación al retiro).
- **Adopción incompleta** (maestro de tipos vacío): sin tipos creados el módulo no opera; mitigación: paso de adopción documentado en guía FE + catálogo de referencia A.2.

### Supuestos

- El cliente es una **institución con política interna de tiempo compensatorio** (sector público bajo DGP o política privada de reglamento interno); los factores, topes y elegibilidad son **política de cada empresa**, no ley general — nada se codifica (Anexo A.1, verificado 2026-07-05 con fuentes).
- La jefatura emite la autorización del trabajo extra como documento (PDF) fuera del sistema; RRHH la adjunta al acreditar (P-11). El flujo de autorización EN el sistema es F2.
- Tenant mono-país (SV) como el resto del sistema; la planilla se procesa externamente y consume archivos.
- Los empleados autogestionados tienen usuario vinculado (`LinkedUserPublicId`); quienes no, operan vía RRHH.
- La valoración del saldo al liquidar usa la convención salarial ya ratificada del sistema (salario/30; horas estándar por día de este módulo).

### Dependencias

- ✅ **Ratificación completada (2026-07-05)**: D-01…D-21 y P-01…P-14 — el plan técnico puede iniciar. P-15 (tarifa) se confirma en segunda ronda; no bloquea (default parametrizable).
- **REQ-001 (vacaciones e incapacidades)**: asuetos, `restDayOfWeek`, periodos de planilla y reglas de solape — este módulo se secuencia después (D-18).
- **Módulo de liquidación**: mergeado (PR #56) — la integración RF-013 puede construirse sin esperas.
- **Infraestructura**: entrada `Storage:Purposes` del nuevo propósito en appsettings **base** + **contenedor de blobs aprovisionado** antes del despliegue (D-20 — mismo gotcha que REQ-001).
- **Adopción por empresa**: crear los tipos de tiempo compensatorio (maestro vacío) y configurar preferencias antes de operar.
- Internas: convenciones de catálogos/seeds vigentes; verificación de IDs libres al abrir el primer PR.

---

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-05)

| # | Pregunta (síntesis) | Respuesta del negocio → efecto en el diseño |
|---|---|---|
| P-01 | ¿Unidad de medida del fondo: horas o días? | **Horas (decimal)** → D-02: unidad canónica en horas, 2 decimales |
| P-02 | ¿Cuántas horas equivalen a un día? | **Preferencia por empresa, default 8** (jornada diurna / despacho público 8:00-16:00) → `compensatoryTimeStandardDailyHours` |
| P-03 | ¿Qué factores de acreditación aplican? | **Factor por tipo, default 1.00, editable por empresa**; la referencia A.2 arranca todo en 1.00 hasta definición → D-05 |
| P-04 | ¿El tiempo acreditado caduca? | **Sin caducidad en F1** (no hay política de caducidad vigente) → ledger simple derivado confirmado (D-04); caducidad futura = evolución FIFO consciente |
| P-05 | ¿Quiénes "tienen derecho"? ¿flag o criterio? | **F1: criterio de RRHH (sin gate)**; flag por plaza en F2 si se requiere control formal → D-16 |
| P-06 | ¿El empleado solicita en línea en F1? | **F1 solo consulta; solicitud + autorización de jefatura en F2** (espejo del flujo de vacaciones) → D-01 |
| P-07 | ¿Se permite saldo negativo? | **No, nunca negativo; anular una acreditación no puede dejar el saldo descubierto** → D-08/D-09 (invariantes RN-03/RN-06) |
| P-08 | ¿El saldo aparece en el perfil? | **Sí**, campo aditivo `compensatoryTimeHoursAvailable` → RF-009/D-11 |
| P-09 | ¿Qué pasa con el saldo al retiro? | **F1: el retiro no se bloquea, el saldo queda visible en exportaciones Y la liquidación agrega el saldo pendiente en su cálculo automático** ("las horas, por ser compensatorias, se deben pagar" — G-07). **F2: se podrá definir la política de pago o caducidad del saldo al retiro** → D-17 ajustada + D-19 + RF-013 (línea `HORAS_EXTRAS_PENDIENTES` sugerida y editable) |
| P-10 | ¿Tope de acumulación de saldo? | **Definido por cada empresa: null = indefinido; si no, el valor que defina** → `compensatoryTimeMaxBalanceHours` (RN-11) |
| P-11 | ¿Evidencia adjunta de la autorización? | **Sí, requerida en F1: documento de autorización de jefatura (PDF) para acreditar** → D-20 + RF-012 (FilePurpose nuevo, Storage:Purposes base, contenedor, obligatorio default parametrizable) |
| P-12 | ¿Plantilla de tipos: se siembra o inicia vacía? | **Inicia vacío** → D-05 ajustada: sin plantilla ni seeder ni load-template; A.2 pasa a catálogo de referencia; crear tipos = paso de adopción |
| P-13 | ¿Registrar el origen del trabajo extra? | **Se puede relacionar la hora extra a la que se acredita el tiempo compensatorio; el módulo de horas extras no está desarrollado aún pero queda listo para cuando se desarrolle** → D-21: costura `overtimeRecordPublicId` opcional sin validación en F1 (+ hora inicio/fin informativas se conservan de D-06) |
| P-14 | ¿La ausencia se imputa a periodo de planilla? | **Sí, referencia opcional** (imputación, no contención — mismo corte que incapacidades) → D-07 |

### Pregunta de segunda ronda (no bloqueante)

| # | Pregunta | Estado |
|---|---|---|
| P-15 | **¿A qué tarifa se valora la hora del saldo al liquidar?** (D-19 propone: valor hora ordinaria = salario/30/horas-día × factor parametrizable, **default 1.00**; la línea es editable por el liquidador). Considerar: si el factor de acreditación del tipo ya incorporó el recargo en tiempo, la tarifa ordinaria evita duplicarlo en dinero | **Abierta — con default definido**; confirmar antes del despliegue (no bloquea plan técnico ni desarrollo) |

---

## 18. Recomendaciones del Analista de Negocio

1. ✅ **Ratificación completada (2026-07-05)**: las 14 respuestas están incorporadas (horas, sin caducidad → ledger simple, adjunto obligatorio, maestro vacío, integración con liquidación, costura a horas extras). **El plan técnico puede iniciar sobre D-01…D-21**; solo P-15 (tarifa) queda para confirmar con default definido.
2. **Mantener la secuencia tras REQ-001** para el desarrollo (asuetos/`restDayOfWeek`/periodos de planilla y solapes coherentes), pero tener presente que la **integración con liquidación (RF-013) no depende de REQ-001** — el módulo de liquidación ya está mergeado.
3. **Confirmar P-15 con el contador/negocio antes del despliegue** (no del desarrollo): la tarifa vive en una preferencia y la línea es editable, así que el riesgo es bajo; documentar la convención (hora ordinaria = salario/30/horas-día) en la exportación y la guía FE.
4. **Concentrar el esfuerzo de pruebas en las invariantes del saldo** (RN-03/RN-06) y sus **carreras** (débitos concurrentes; anulación de crédito vs débito simultáneo), y en la **retrocompatibilidad de la liquidación** — el resto del módulo es composición de patrones probados (maestro governed, estado híbrido, adjuntos con propósito, bandeja/export, asientos).
5. **No construir motor de jornadas ni asistencia**: la acreditación declarativa con autorización documentada es el corte ratificado de F1; un futuro módulo de marcaciones podrá pre-llenar acreditaciones sin romper este contrato.
6. **Planificar el paso de adopción con el cliente**: como el maestro inicia vacío (P-12), la puesta en marcha requiere (a) crear los tipos (usar A.2 como referencia, incluido un tipo de saldo inicial), (b) configurar las 4 preferencias, (c) cargar los saldos históricos con su documento de respaldo — dejarlo como checklist de despliegue.
7. **Acordar con el cliente la regla operativa de no doble beneficio** (qué horas se pagan y cuáles se compensan) y entregarle la exportación de horas acreditadas como control; el control sistemático llegará al desarrollarse el módulo de horas extras (la costura D-21 ya queda lista).
8. **Cuidar el vocabulario frente a los módulos vecinos**: "acreditación/débito/saldo" pertenecen a este fondo; no reutilizar "goce/devolución" de vacaciones más allá del ActionType `GOCE_TIEMPO_COMPENSATORIO`, ni tocar el ledger `PersonnelFilePayrollTransaction`.
9. **MVP si se necesita recortar**: RF-001…RF-008 + RF-012 (catálogo + acreditar con adjunto + ausentar + estado de cuenta) entregan el valor del levantamiento; perfil (RF-009), bandeja/exportaciones (RF-011) e integración con liquidación (RF-013) pueden ser la segunda entrega — aunque RF-013 es compromiso ratificado de F1 y conviene no diferirlo.
10. **Plan de F2 ya perfilado**: solicitud del empleado con autorización en línea (espejo de solicitudes de vacaciones + anti-autoaprobación), política refinada de pago/caducidad al retiro, elegibilidad por plaza, vínculo activo con el módulo de horas extras.

---

## Anexo A — Referencias y propuestas

### A.1 Marco legal y normativo de referencia (El Salvador) — verificado 2026-07-05, a validar con el negocio/legal

> **Nada de esto se codifica como regla**: en SV el tiempo compensatorio es esencialmente **política institucional**; el módulo lo refleja con tipos, factores y topes parametrizables por empresa.

| Concepto | Referencia verificada | Implicación para el módulo |
|---|---|---|
| Horas extraordinarias (sector privado) | Se **pagan** con recargo del 100 % del salario básico por hora (Arts. 169-170 CT); el pago es la regla general — no existe en el CT un régimen general de "compensación en tiempo" en su lugar ([tusalario.org](https://tusalario.org/elsalvador/derechos-laborales/compensacion), [finiquitojusto](https://finiquitojusto.com/derechos-laborales/horas-extras-el-salvador/)) | El esquema "hora extra → hora compensatoria" en privados es política interna; riesgo de no doble beneficio (RN-14) |
| Trabajo en día de descanso semanal | Salario del día + recargo mínimo del 50 % por las horas trabajadas **y además** un **día de descanso compensatorio remunerado** en la misma semana o la siguiente (Art. 175 CT; el compensatorio se computa como trabajo efectivo) ([CSJ — Descanso semanal](https://www.csj.gob.sv/wp-content/uploads/2021/06/9-Co%CC%81digo-de-Trabajo-de-El-Salvador-Descanso-semanal.pdf), [ILO — Código de Trabajo](https://webapps.ilo.org/public/spanish/region/ampro/mdtsanjose/papers/cod_elsa.htm)) | Único "compensatorio" del CT privado: es **adicional** al recargo, no sustituto del pago; puede modelarse como tipo `TRABAJO_DIA_DESCANSO` |
| Jornada del sector público | Despacho ordinario L-V 8:00-16:00 (Art. 84 DGP); trabajos en exceso de la jornada → Art. 113 DGP ([jurisprudencia.gob.sv](https://www.jurisprudencia.gob.sv/DocumentosBoveda/E/1/2010-2019/2014/09/AAC18.HTML), [Justia — DGP](https://el-salvador.justia.com/nacionales/disposiciones/disposiciones-generales-de-presupuestos/gdoc/)) | Sustenta el default de 8 horas/día (P-02, ratificado) |
| Tiempo compensatorio en el sector público | Práctica institucional: horas extra reconocidas como tiempo compensatorio (licencia con goce), con equivalencias y topes que **varían por institución** según su reglamento interno (p. ej. [Reglamento de horas extraordinarias de la Corte de Cuentas](https://www.cortedecuentas.gob.sv/index.php/es/marco-normativo/norma-administrativa?download=4646:reglamento-para-la-remuneracion-del-trabajo-en-horas-extraordinarias-de-la-corte-de-cuentas-de-la-republica-segun-decreto-no-14)) | Confirma: factores (P-03), topes (P-10) y elegibilidad (P-05) son parametrización por empresa, nunca constantes |
| Trabajo en día de asueto | Salario extraordinario con recargo (Art. 192 CT) — referencia para el tipo `TRABAJO_ASUETO` | El factor > 1.00 para asueto es decisión de política de cada empresa (P-03) |

### A.2 Catálogo de referencia de tipos (para el administrador — **NO se siembra**: el maestro inicia vacío, P-12)

> Guía sugerida al crear los tipos de la empresa durante la adopción; los factores arrancan en 1.00 hasta que cada empresa defina su política (P-03).

| Código sugerido | Descripción | Operación | Factor | Nota |
|---|---|---|---|---|
| `TRABAJO_FUERA_JORNADA` | Trabajo fuera de jornada en día hábil | ACREDITA | 1.00 | Caso general |
| `TRABAJO_DIA_DESCANSO` | Trabajo en día de descanso semanal | ACREDITA | 1.00 | Factor editable según política (Art. 175 CT como referencia) |
| `TRABAJO_ASUETO` | Trabajo en día de asueto | ACREDITA | 1.00 | Factor editable según política |
| `SALDO_INICIAL` | Saldo inicial (adopción del módulo) | ACREDITA | 1.00 | Carga del control anterior (G-10); adjunto = acta/planilla previa |
| `GOCE_TIEMPO_COMPENSATORIO` | Goce de tiempo compensatorio | DEBITA | — | Débito estándar |
| `AJUSTE` | Ajuste administrativo | AMBAS | 1.00 | Correcciones documentadas (uso restringido) |

### A.3 Seeds tentativos (verificar IDs libres contra `GlobalCatalogSeedData` al abrir el primer PR)

- Piso general verificado (2026-07-05): **-9846**; REQ-001 reserva **-9850…-9862** (TPH) y **-9485…-9489** (ActionTypes).
- **Trampa verificada**: `ACTION_STATUS_CATALOG` ocupa **-9490…-9496** → los ActionTypes de este módulo NO continúan la secuencia.
- Propuesta (bloque del módulo, contiguo): `COMPENSATORY_TIME_STATUS_CATALOG` `REGISTRADA=-9865`, `ANULADA=-9866` · operaciones (si TPH) `ACREDITA=-9867`, `DEBITA=-9868`, `AMBAS=-9869` · `ACTION_TYPE_CATALOG` `ACREDITACION_TIEMPO_COMPENSATORIO=-9870`, `GOCE_TIEMPO_COMPENSATORIO=-9871`.
- **Sin seeds de tipos** (P-12) → no hay seeder de plantilla ni `load-template` en este módulo.

### A.4 Casos dorados sugeridos para la validación del negocio (ajustados a la ratificación)

1. **Acreditación simple**: tipo factor 1.00, 3 h trabajadas, con PDF de autorización adjunto → saldo 3.00; ausencia de 8 h → 422 (saldo 3.00, faltan 5.00).
2. **Adjunto obligatorio**: alta de acreditación sin documento (preferencia default) → 422; con documento de propósito incorrecto → 422.
3. **Acumulación y goce**: acreditaciones de 8 h y 4 h → saldo 12.00; ausencia de 1 día (sugerencia 8 h) → saldo 4.00.
4. **Factor**: tipo asueto con factor 2.00, 4 h trabajadas → 8.00 h acreditadas (factor en snapshot; cambiar el catálogo después no altera el registro).
5. **Anti-descubierto**: con saldo 4.00 (crédito 12 − débito 8), anular la acreditación de 12 → 422; anular primero la ausencia → saldo 12; anular la acreditación → saldo 0.
6. **Carrera**: dos ausencias simultáneas de 8 h contra saldo 10 → una queda `REGISTRADA`, la otra 422.
7. **Tope** (P-10): tope configurado 40 h y saldo 38 → acreditar 6 h → 422 con máximo acreditable 2.00; sin tope (null) → sin límite.
8. **Sugerencia con calendario** (REQ-001 presente): ausencia lun-mié con asueto el martes y descanso domingo → sugerencia 2 días × 8 = 16 h (editable).
9. **Adopción**: la empresa crea sus tipos (maestro vacío) y registra acreditación `SALDO_INICIAL` de 20 h con el acta anterior adjunta → primer movimiento del estado de cuenta; el saldo del perfil muestra 20.00.
10. **Liquidación con saldo** (D-19): retiro con saldo 12 h, salario $600, 8 h/día, tarifa 1.00 → valor hora = 600/30/8 = $2.50 → línea `HORAS_EXTRAS_PENDIENTES` sugerida de 12 unidades × $2.50 = $30.00, editable; sin saldo → sin línea y suite de liquidación intacta.
