# Análisis de negocio — Tablero de gráficos e indicadores de acciones de personal

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | Acciones de personal — Tablero de gráficos e indicadores (dashboard del módulo), con criterios de búsqueda por año, mes, tipo de planilla, área funcional, unidad, tipo de puesto, nivel piramidal, puesto y centro de trabajo, e impresión/exportación a PDF |
| **Fecha** | 2026-07-05 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código |
| **Estado** | **RATIFICADO por el negocio (2026-07-05)** — decisiones **D-01…D-18 ratificadas y aprobadas por unanimidad** y respuestas **P-01…P-14 incorporadas** (§17). Dos cierres que ajustan el detalle: **P-01** → impresión y exportación PDF **desde el navegador/frontend** (el reporte PDF servidor queda como referencia F2, A.4-b); **P-02** → tipo de planilla = modalidad de pago contractual del empleado, catálogo **por país** con los valores A.2, **sin backfill**: migración de **limpieza destructiva** de los valores libres (sin datos ni código legacy) |
| **Naturaleza del módulo** | **Extensión read-only del tablero ya construido** (`personnel-files/dashboard/*`, PR #52) + **primera consulta corporativa del journal de acciones de personal** (`PersonnelFilePersonnelAction`, hoy solo consultable por expediente). No crea flujos transaccionales; el único cambio de escritura posible es la formalización del catálogo de tipo de planilla (D-10, condicionada a P-02) |
| **Documentos hermanos** | [`analisis-tablero-indicadores-rrhh.md`](analisis-tablero-indicadores-rrhh.md) — **CONSTRUIDO y mergeado (PR #52)**; este requerimiento lo extiende y **activa sus indicadores diferidos** (bajas/rotación) · [`analisis-retiro-definitivo-empleado.md`](analisis-retiro-definitivo-empleado.md) — **CONSTRUIDO (PR #55)**; es el "módulo Baja de Personal" que aquel tablero esperaba |
| **Documentos relacionados** | [`analisis-vacaciones-incapacidades-empleado.md`](analisis-vacaciones-incapacidades-empleado.md) (REQ-001 — fuente futura: incapacidades/vacaciones; usa `payrollTypeCode` → coordinación P-02) · [`analisis-tiempo-compensatorio-empleado.md`](analisis-tiempo-compensatorio-empleado.md) (REQ-002 — fuente futura) · [`analisis-otras-transacciones-personal.md`](analisis-otras-transacciones-personal.md) (REQ-003 — fuente futura: reconocimientos/amonestaciones; patrón de fuentes conectables) · `analisis-entrevista-retiro-empleado.md` (módulo existente — indicador de cobertura) · `analisis-liquidacion-empleado.md` (módulo existente — asiento `LIQUIDACION` y conteos) |

---

## Contexto del cambio (requerimiento original)

El levantamiento describe el módulo de **acciones de personal**: registrar las acciones que se aplican a los empleados de la institución (incapacidades, reconocimientos, cambios de centro de costo, amonestaciones, cambios de tipo de contrato, vacaciones, entre otros), realizar contrataciones, registrar movimientos de personal y documentar las entrevistas de retiro. Entre sus principales opciones pide:

> **Tablero de Gráficos e indicadores**: se requiere un tablero (dashboard) con gráficos e indicadores **estándares del módulo de acciones de personal**. Debe proporcionar criterios de búsqueda como: **año, mes, tipo de planilla, área funcional, unidad, tipo de puesto, nivel piramidal, puesto y centro de trabajo**. Las gráficas e indicadores deben permitir la **impresión** y **exportarla a formato PDF**.

**Delimitación del pedido.** De las opciones del módulo mencionadas en la introducción, la mayoría **ya existe o ya está en el backlog**: contrataciones (alta/finalización del expediente + recontratación), retiros/bajas (módulo de retiro definitivo, PR #55), entrevistas de retiro (módulo dedicado, PR #51), movimientos documentales (journal manual de acciones), liquidaciones (PR #56); incapacidades/vacaciones llegan con **REQ-001** y reconocimientos/amonestaciones con **REQ-003**. Lo que este levantamiento pide construir —y lo que este análisis cubre— es el **tablero** del módulo: sus indicadores, sus criterios de búsqueda y su impresión/exportación a PDF.

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **Ya existe un tablero en producción y este requerimiento es su extensión natural — no construir un segundo stack.** El tablero de indicadores de RRHH (Fase 1) está **mergeado en master (PR #52)**: familia `GET api/v1/companies/{companyId}/personnel-files/dashboard/{overview,hires,span-of-control,metadata}` (`PersonnelFileReportingController.cs`), permiso dedicado `PersonnelFiles.ViewReports` (`IPersonnelFileAuthorizationService.cs:168`), capa dimensional por asignación activa primaria (`PersonnelFileDashboardRepository.cs`) y catálogos de rangos parametrizables. **6 de los 9 criterios de búsqueda pedidos ya están construidos** (año, área funcional, unidad, tipo de puesto, puesto, centro de trabajo — `DashboardDimensionFilter`); faltan **mes** (trivial), **tipo de planilla** (ver punto 4) y **nivel piramidal** (ver punto 5).
2. **El journal unificado de acciones existe, pero no tiene consulta a nivel empresa — esa consulta es el núcleo nuevo.** `PersonnelFilePersonnelAction` (`PersonnelFileEmployee.cs:571-643`, tabla `personnel_file_personnel_actions`) ya unifica el vocabulario (15 tipos sembrados `-9470…-9484`: NOMBRAMIENTO…OTRO, `BAJA`, `REVERSION_BAJA`, `LIQUIDACION`; 7 estados `-9490…-9496`) y **ya recibe asientos automáticos** (`IsSystemGenerated`) desde retiro (`ExecuteRetirementRequest.cs:179`), reversión (`RevertRetirementRequest.cs:204`), recontratación (`RehireEmployee.cs:250`) y liquidación (`Settlements.Handlers.cs:995`), además del asiento manual (`Employment/PersonnelActions.cs`). Sin embargo, **toda su superficie de consulta es por expediente** (`SearchPersonnelActionsAsync`, `PersonnelFileEmployeeRepository.cs:678`); no existe query/bandeja/agregación tenant-wide. El tablero exige construirla.
3. **El módulo que bloqueaba bajas y rotación YA existe → este requerimiento las activa.** El tablero RRHH difirió **bajas** e **índice de rotación** (D-02/D-14 ratificadas) porque el "módulo Baja de Personal" no existía. Desde el PR #55 existe (solicitudes de retiro, `RetirementDate` + categoría/motivo en perfil, ventana de reversión). Las bajas y la rotación se construyen aquí sobre la **fuente canónica del perfil** (`RetirementDate`/`RetirementCategoryCode`/`RetirementReasonCode`) — **no** sobre el journal: `CONTRATACION` nunca se escribe automáticamente (las altas se derivan de `HireDate`, precedente ratificado) y el asiento `BAJA` solo existe desde el PR #55 en adelante (histórico incompleto).
4. **"Tipo de planilla" ya tiene campo, pero es texto libre sin catálogo ni proyección.** La plaza (`PersonnelFileEmploymentAssignment`) lleva `PayrollTypeCode` (string ≤ 80, validación solo de longitud en `EmploymentAssignments.cs`; el FE ya lo captura, p. ej. `"MENSUAL"`), pero **no existe `PAYROLL_TYPE_CATALOG`**, el tablero no lo proyecta y no es filtrable. La brecha es formalizar el catálogo, validar por código, proyectar y filtrar — con **normalización/backfill** del dato libre existente (D-10, P-02 crítica). Coordinación obligada con **REQ-001**, cuyo análisis asume "el mismo catálogo de tipo de planilla que usa la plaza" para las incapacidades.
5. **"Nivel piramidal" sigue estructuralmente bloqueado — se mantiene el diferimiento ya ratificado.** `OccupationalPyramidLevel` existe, pero no hay relación canónica 1-a-1 con el puesto (vive solo en la matriz de competencias `JobProfileCompetencyExpectation.OccupationalPyramidLevelId`; un puesto puede tener expectativas en varios niveles). Es la misma situación que ratificó el diferimiento D-07 del tablero RRHH; este tablero hereda el gancho (D-11).
6. **Impresión/PDF de gráficos: nada existe y es la decisión crítica del requerimiento.** La exportación por indicador quedó **explícitamente diferida a Fase 2** en el tablero construido (guía FE §10). El stack PDF del repo es sólido pero **documental**: AST `DocumentModel` (párrafos, tablas, key-value, listas — **sin bloque de imagen ni de gráfico**) con **motor dual conmutable** por configuración `Reporting:Pdf:Engine` (QuestPDF 2024.12.3 Community por defecto; Gotenberg HTML→PDF como alternativa), y **no hay ninguna librería de charting/SVG** en la solución; ambos renderers lanzan `NotSupportedException` ante bloques desconocidos (`QuestPdfDocumentRenderer.cs:148`, `DocumentModelHtmlSerializer.cs:111`). Opciones viables en Anexo A.4; propuesta en D-12; ratificación en **P-01**.
7. **Las dimensiones organizativas de una acción son una aproximación reconocida.** El asiento del journal referencia al **expediente**, no a una plaza/unidad: agrupar acciones por unidad/puesto/centro/planilla solo puede resolverse por la **asignación activa primaria actual** del empleado (mismo mecanismo del tablero existente). Para acciones pasadas de empleados que rotaron internamente, la dimensión reflejará su posición actual — límite estructural documentado (D-07, P-07); el snapshot dimensional en el asiento sería F2 y solo hacia adelante.
8. **Sensibilidad: conteos sí, montos no.** El asiento de liquidación guarda `Amount` = **neto pagado**. Bajo `ViewReports` no deben viajar montos (ni agregados ni columnas de bandeja) en F1; el análisis monetario requeriría `ViewCompensation` y se difiere (D-15). Los indicadores de módulos aún no construidos (incapacidades/vacaciones — REQ-001; reconocimientos/amonestaciones — REQ-003; tiempo compensatorio — REQ-002) se diseñan como **fuentes conectables** con contrato estable: se activan al llegar cada módulo, sin re-trabajo (D-17), igual que la consulta de tiempos de REQ-003.

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Dónde | Uso en este módulo |
|---|---|---|
| **Tablero RRHH Fase 1 (construido, PR #52)**: 4 endpoints `dashboard/{overview,hires,span-of-control,metadata}` en la familia Reporting (read-only, gate en handler, sin `[AuthorizationPolicySet]`), filtros `DashboardDimensionFilter` (año + 5 dimensiones por PublicId), buckets "Sin asignar"/"Sin dato" | `PersonnelFileReportingController.cs`; `Features/PersonnelFiles/Reporting/PersonnelFileDashboard*.cs` | Se **extiende** con las secciones de acciones/movimientos; mismos filtros, convenciones y contrato |
| Permiso dedicado del tablero: `PersonnelFiles.ViewReports` (satisfecho por `Read`/`Admin`), gate `EnsureCanViewReportsAsync` | `IPersonnelFileAuthorizationService.cs:168`; `PersonnelFileAuthorizationService.cs` | Mismo permiso para todas las secciones nuevas, bandeja y exports (D-16) |
| Capa dimensional por **asignación activa primaria**: proyección `EmployeeDimensionRow` (unidad, área funcional, centro, puesto/slot, tipo de puesto, contrato) con resolución en memoria | `PersonnelFileDashboardRepository.cs` (DI `DependencyInjection.cs:174`); `IPersonnelFileDashboardRepository.cs` | Resuelve las dimensiones organizativas de los indicadores de acciones (D-07) |
| **Journal de acciones de personal**: `PersonnelFilePersonnelAction` (`ActionTypeCode`, `ActionStatusCode`, `ActionDateUtc`, `EffectiveFromUtc/ToUtc`, `Description`, `Reference`, `Amount`+`CurrencyCode`, `IsSystemGenerated`) con índices `(TenantId, PersonnelFileId, ActionDateUtc)` y `(TenantId, PersonnelFileId, ActionTypeCode, ActionStatusCode)` | `PersonnelFileEmployee.cs:571-643`; config `PersonnelFileEmployeeConfiguration.cs:208`; tabla `personnel_file_personnel_actions` | **Fuente de los indicadores documentales** (serie, tipo, estado, origen) |
| Catálogos país del journal: `ACTION_TYPE_CATALOG` (`-9470…-9484`: NOMBRAMIENTO, CONTRATACION, RECONTRATACION, ASCENSO, TRASLADO, CAMBIO_PUESTO, AUMENTO_SALARIAL, AMONESTACION, SUSPENSION, PERMISO, REINTEGRO, OTRO, **BAJA**, **REVERSION_BAJA**, **LIQUIDACION**) y `ACTION_STATUS_CATALOG` (`-9490…-9496`: BORRADOR, PENDIENTE, EN_TRAMITE, APROBADA, RECHAZADA, APLICADA, ANULADA), expuestos por keys `action-types`/`action-statuses` y validados por código en el asiento manual | `GlobalCatalogSeedData.cs:734/753`; `GeneralCatalogItems.cs:881/914` | Ejes "por tipo" y "por estado"; etiquetas de leyendas |
| **Asientos automáticos ya escritos** (`IsSystemGenerated`, estado `APLICADA`): `BAJA` al ejecutar retiro, `REVERSION_BAJA` al revertirlo, `RECONTRATACION` al recontratar, `LIQUIDACION` (con neto) al emitir liquidación; asiento **manual** para el resto de tipos | `ExecuteRetirementRequest.cs:179`; `RevertRetirementRequest.cs:204`; `RehireEmployee.cs:250`; `Settlements.Handlers.cs:995`; `Employment/PersonnelActions.cs` | El indicador de "origen" (manual vs automático) y la trazabilidad del drill |
| Consulta/exportación del journal **por expediente** (search paginado + export sync/async con resource `PERSONNEL_FILE_PERSONNEL_ACTIONS`) | `PersonnelFileEmployeeRepository.cs:678`; `PersonnelFilePersonnelActionsExportHandler` (`DependencyInjection.cs:189`) | Referencia de forma/columnas para la **nueva** consulta tenant-wide (RF-001/RF-017) |
| **Módulo de retiro definitivo (PR #55)**: solicitudes con estados, ejecución que estampa el perfil (`RetirementDate`, `RetirementCategoryCode`, `RetirementReasonCode`, estado `RETIRADO`), reversión con ventana de 30 días | `PersonnelFileEmployee.cs` (perfil y zona de retiro); `Features/PersonnelFiles/Retirements/*` | **Fuente canónica de bajas** por mes/categoría/motivo y de la rotación (RF-010/RF-011) |
| Indicador de **altas** existente (serie mensual por año desde `HireDate`; bajas devueltas como diferidas) | `PersonnelFileDashboardIndicators.cs` (`GetDashboardHiresQuery`) | Se integra con bajas y neto (RF-012) |
| **Entrevistas de retiro**: formularios + `ExitInterviewSubmission` (`PersonnelFileId`, `RetirementReasonCode`, `TotalScore`, `SubmittedUtc`, `Status`) | `ExitInterview.cs`; `ExitInterviewSubmission.cs` | Indicador de **cobertura de entrevistas** sobre las bajas del periodo (RF-013) |
| **Liquidaciones**: `PersonnelFileSettlement` con `StatusCode` y bandeja con `StatusCounts` | `Settlements*`; `SettlementsBandeja.cs` | Conteos por estado/mes (RF-014, solo conteos — sin montos) |
| Historial de contratos `PersonnelFileContractHistory` (tipo, fechas, plaza) + línea de tiempo de periodos de empleo derivada | `personnel_file_contract_histories`; `Rehire/EmploymentPeriodsTimeline.cs` | Fuente derivable opcional para "cambios de tipo de contrato" (mención en A.1) |
| Campo `PayrollTypeCode` en la plaza (texto libre ≤ 80, ya capturado por el FE) y `CostCenterPublicId` también en la plaza + entidad `CostCenter` | `PersonnelFileEmployee.cs` (asignación); `Domain/CostCenters/CostCenter.cs` | Base del filtro **tipo de planilla** (D-10) y del filtro opcional **centro de costo** (P-12) |
| **Stack PDF documental con motor dual**: AST `DocumentModel` (Paragraph/MutedText/LabeledParagraph/KeyValue/Table/BulletList) → `IDocumentModelRenderer`; QuestPDF 2024.12.3 **Community** por defecto o **Gotenberg** (HTML→PDF Chromium) por configuración `Reporting:Pdf:Engine`; precedentes: boleta de liquidación (`GET …/settlements/{id}/document?format=pdf`), constancias, PDF de puesto (sync y **asíncrono** vía `JobProfilePdfExportHandler`) | `Abstractions/Reports/Documents/DocumentModel.cs`; `Infrastructure/Reports/Documents/*`; `DocumentPdfRenderingRegistration.cs:53`; `SettlementsController.cs:42-63` | Camino del **PDF servidor** del tablero si se ratifica (D-12/P-01, Anexo A.4) |
| **Exportación tabular** hecha en casa: `ReportExportFileWriter` (csv/json/**xlsx** OpenXML manual), `ReportExportDeliveryService` (límite síncrono → 413, auditoría `ReportExported`) y subsistema **asíncrono** `report-export-jobs` (11 handlers, whitelist `ReportExportResources`) | `Features/Reports/ReportExportFileWriter.cs`; `Api.Common/ReportExportDeliveryService.cs`; `ReportExportJobsController.cs` | Exportación de datasets del tablero y de la bandeja (RF-016) |
| Bandeja de empresa (patrón): `POST …/query` paginado con `StatusCounts` + `GET …/export`, filas en español | `SettlementsBandeja.cs`; `*ReportingController.cs` de liquidaciones/constancias/retiros | Molde de la **bandeja de asientos** destino del drill (RF-017) |

### Lo que NO existe (verificado exhaustivamente)

- Ninguna **consulta, bandeja ni agregación del journal a nivel empresa** — toda la superficie actual es por expediente (`api/v1/personnel-files/{publicId}/personnel-actions*`).
- Ningún **indicador sobre acciones de personal** (el tablero construido agrega el padrón de expedientes, no el journal).
- **Bajas e índice de rotación**: siguen sin construirse (diferidos del tablero RRHH); su módulo bloqueante ya existe (PR #55), pero nadie los ha activado. `hires` no devuelve bajas.
- **Filtro por mes**: no existe (`DashboardDimensionFilter` solo tiene `Year`); el mes existe únicamente como agrupación de salida en `hires`.
- **Catálogo de tipo de planilla**: no existe (`PAYROLL_TYPE_CATALOG` no está sembrado); el campo de la plaza es texto libre y el tablero no lo proyecta ni filtra.
- **Relación canónica puesto ↔ nivel piramidal**: sigue pendiente (sin FK en `JobProfile`; el nivel vive solo en la matriz de competencias).
- **Impresión/exportación PDF del tablero**: nada — diferida explícitamente a Fase 2 en el tablero construido (guía FE §10); el AST `DocumentModel` **no tiene bloque de imagen ni de gráfico** y **no hay librería de charting/SVG** en ningún csproj (solo QuestPDF + PdfPig de tests).
- **Registro de cambios de centro de costo**: el cambio es una sobrescritura in-place del campo de la plaza (sin log de eventos); el vehículo documental hoy es el asiento manual (`TRASLADO`/`OTRO`).
- **Asiento automático de `CONTRATACION`**: el código existe en el catálogo pero ningún flujo lo escribe (las altas viven en `HireDate`).
- **REQ-001/REQ-002/REQ-003 no están construidos**: incapacidades, vacaciones, tiempo compensatorio, reconocimientos y amonestaciones no tienen fuente aún → sus indicadores solo pueden diseñarse como conectables.

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha detectada | Propuesta de resolución |
|---|---|---|
| G-01 | Tablero pedido sobre acciones sin consulta tenant-wide del journal | **Nueva query agregada a nivel empresa** sobre `personnel_file_personnel_actions` (serie/tipo/estado/origen/dimensión), en la familia Reporting existente (RF-001, RF-005…RF-009) |
| G-02 | El cliente pide indicadores "estándares" sin enumerarlos | Set estándar propuesto de 11 indicadores (D-04, Anexo A.1) a **ratificar** con el cliente (P-03) |
| G-03 | Bajas/rotación diferidas en el tablero RRHH por falta del módulo de baja — ese módulo ya existe (PR #55) | **Activarlas en este requerimiento** desde la fuente canónica del perfil (`RetirementDate` + categoría/motivo), una sola implementación (RF-010/RF-011, P-09) |
| G-04 | Filtro **mes** inexistente | Agregarlo a los filtros transversales con semántica de **flujo** (acciones/altas/bajas/rotación); los indicadores snapshot lo ignoran documentadamente (D-09) |
| G-05 | **Tipo de planilla**: campo libre sin catálogo, sin proyección y sin filtro; REQ-001 asume ese catálogo | Formalizar `PAYROLL_TYPE_CATALOG` (seed SV editable) + validación por código en la plaza + proyección/filtro en el tablero + **normalización/backfill** del dato libre (D-10; crítica P-02; coordinación REQ-001) |
| G-06 | **Nivel piramidal** sin relación canónica puesto↔nivel (mismo hallazgo ratificado D-07 del tablero RRHH) | Mantener **diferido** con gancho en el contrato; se habilita cuando el módulo de puestos/competencias materialice la relación (D-11) |
| G-07 | **Impresión/PDF de gráficos**: sin bloque de imagen/gráfico en `DocumentModel`, sin librería de charting; export por indicador diferido F2 | Estrategia de dos vías a ratificar (P-01, Anexo A.4): impresión = navegador (vista de impresión FE); PDF = FE (mínimo) **o** PDF servidor con `DocumentModel` + nuevo bloque de imagen raster alimentado por el FE, sobre el motor dual y `report-export-jobs` (D-12/D-13) |
| G-08 | El asiento no referencia plaza/unidad → dimensiones organizativas de acciones no exactas | Resolver por **asignación activa primaria actual** (capa dimensional existente), aproximación documentada en UI/contrato; snapshot dimensional en el asiento = F2 y solo hacia adelante (D-07, P-07) |
| G-09 | Doble narrativa posible de altas/bajas (journal parcial vs perfil): `CONTRATACION` nunca se asienta, `BAJA` solo desde PR #55 | Regla de **fuente canónica por indicador** (D-03, Anexo A.1): movimientos desde el perfil/módulos; el journal solo alimenta los indicadores documentales |
| G-10 | Journal histórico incompleto y de alimentación parcialmente manual (adopción variable; tipos antiguos podrían ser texto pre-validación) | Comunicar que el tablero documental "muestra lo registrado"; plan de adopción/registro retroactivo y normalización de códigos históricos en la ratificación (P-13) |

---

## Decisiones — D-01…D-18 (**RATIFICADAS por el negocio, 2026-07-05**)

> ✅ **Todas ratificadas y aprobadas por unanimidad** junto con las respuestas P-01…P-14 (§17). Dos decisiones cerraron con ajuste de detalle respecto del borrador: **D-12** fija la vía **frontend** para impresión/PDF (P-01) y **D-10** reemplaza el backfill por una **migración de limpieza destructiva** de los valores libres de `payrollTypeCode` (P-02). El resto quedó tal como se propuso.

| # | Tema | Decisión propuesta |
|---|---|---|
| D-01 | Fases | **F1**: extensión del tablero con las secciones de **acciones documentales** (serie/tipo/estado/origen/dimensión) y **movimientos** (altas+bajas+neto, rotación, cobertura de entrevistas, liquidaciones por estado) + filtros **mes** y **tipo de planilla** (condicionada a P-02) + **impresión/PDF** (según P-01) + exportación tabular + bandeja de asientos (drill). **Activaciones** (no son fase): indicadores de incapacidades/vacaciones (REQ-001), reconocimientos/amonestaciones (REQ-003) y tiempo compensatorio (REQ-002) se conectan al llegar cada módulo. **F2**: snapshot dimensional del asiento, análisis monetario con `ViewCompensation`, nivel piramidal, programación/envío por correo de reportes, comparativas multi-periodo |
| D-02 | Un solo tablero | **Extender la familia existente** `api/v1/companies/{companyId}/personnel-files/dashboard/*` (permiso, filtros, convenciones Reporting y metadata compartidos) con endpoints nuevos de sección (nombres finales en el plan técnico; p. ej. `dashboard/personnel-actions`, `dashboard/separations`). **No** se crea un stack/controlador paralelo ni un segundo permiso |
| D-03 | Fuentes canónicas | Cada indicador declara su fuente (Anexo A.1): **journal** → indicadores documentales; **perfil** (`HireDate` / `RetirementDate`+categoría/motivo) → altas/bajas/rotación; **módulos ricos** (retiro, entrevistas, liquidaciones) → sus KPIs. Regla dura: **el journal nunca alimenta altas/bajas/rotación** (asientos `CONTRATACION` inexistentes y `BAJA` solo desde PR #55) |
| D-04 | Set estándar F1 (a ratificar P-03) | 11 indicadores: (1) serie mensual de acciones del año, (2) acciones por tipo, (3) acciones por estado, (4) origen manual vs automático, (5) acciones por dimensión organizativa (unidad/área/centro/puesto/tipo de puesto), (6) altas por mes, (7) **bajas por mes + desglose por categoría y motivo**, (8) neto altas−bajas, (9) **índice de rotación**, (10) cobertura de entrevistas de retiro, (11) liquidaciones por estado (conteos) |
| D-05 | Población de asientos por defecto | Los indicadores documentales cuentan por defecto **asientos efectivos** (estado `APLICADA`; `ANULADA` excluida), con parámetro para incluir todos los estados; el desglose "por estado" (indicador 3) siempre muestra el universo completo (P-04) |
| D-06 | Fecha de serie | Las series usan **`ActionDateUtc`** (fecha de la acción); las vigencias `EffectiveFromUtc/ToUtc` son informativas del asiento, no eje temporal (P-05) |
| D-07 | Dimensiones de las acciones | Se resuelven por la **asignación activa primaria actual** del empleado (capa dimensional existente); empleados sin asignación → bucket "Sin asignar". **Aproximación documentada** en contrato y UI (una acción de enero de un empleado trasladado en junio se agrupa en su unidad actual). Exactitud histórica (snapshot de dimensiones en el asiento) = F2 y solo hacia adelante (P-07) |
| D-08 | Rotación | `Rotación (%) = (bajas del periodo ÷ headcount promedio) × 100`, con headcount promedio = (activos al inicio + activos al fin) / 2, vista mensual y anual; desglose adicional por categoría de retiro (aproximación a voluntaria/involuntaria) como vista secundaria; headcount promedio 0 → "N/D" (P-06) |
| D-09 | Filtro mes | Nuevo parámetro `month` (1-12, requiere `year`) aplicable a los indicadores de **flujo** (acciones, altas, bajas, neto, rotación mensual); los indicadores **snapshot** (composición del tablero RRHH) lo ignoran y así se documenta en el contrato/metadata |
| D-10 | Tipo de planilla | **RATIFICADA con ajuste (P-02).** Semántica confirmada: **modalidad de pago contractual del empleado** («el tipo de contrato del empleado» en palabras del negocio — la frecuencia/modalidad con la que se paga la plaza; **distinto de `contractTypeCode`** INDEFINIDO/PLAZO_FIJO…, que coexiste en la plaza). Se formaliza **`PAYROLL_TYPE_CATALOG` por país** (wire key `payroll-types`, seed SV con los valores A.2 confirmados) + **validación por código** al escribir `payrollTypeCode` en la plaza + proyección en la fila dimensional + filtro + desglose en composición. **Sin backfill**: la migración de adopción hace **limpieza destructiva** de los valores libres existentes — normaliza a código las coincidencias exactas con el catálogo y **elimina (NULL) el resto**, sin dejar datos ni rutas de código legacy (validación estricta desde el día 1). Las plazas quedan sin clasificar hasta su edición natural → bucket "Sin dato" |
| D-11 | Nivel piramidal | Sigue **DIFERIDO** (se mantiene la decisión D-07 ratificada del tablero RRHH): sin relación canónica puesto↔nivel no hay filtro fiable; el contrato deja el gancho y se habilita cuando el módulo de puestos/competencias la materialice |
| D-12 | Impresión y PDF | **RATIFICADA — vía (a) frontend (P-01)**: la **impresión** y la **exportación a PDF** se resuelven en el navegador/frontend sobre una **vista de impresión** del tablero (con encabezado de empresa, filtros aplicados y fecha de generación — RN-13). **El backend no compone PDF del tablero en F1**: entrega los agregados completos y la guía FE especifica la vista de impresión y el mapa de gráficos (A.5). El reporte PDF servidor (bloque de imagen raster sobre el motor dual) queda **solo como referencia F2** (A.4-b) por si el negocio exige a futuro documento archivable/asíncrono/enviado por correo |
| D-13 | Exportación tabular | Cada dataset del tablero (y la bandeja) exportable a **xlsx/csv/json** con la infraestructura existente (`ReportExportFileWriter` + límite síncrono 413 + `report-export-jobs` con resource keys nuevos + auditoría `ReportExported`). Cubre para estas secciones el diferido "exportación por indicador" (D-11 del tablero RRHH) |
| D-14 | Bandeja de asientos (drill) | Nueva consulta paginada **a nivel empresa** del journal (filtros: tipo, estado, origen, rango de fechas, empleado, unidad; `StatusCounts`) + export — destino del drill de los gráficos documentales y primera vista corporativa del journal. Payload **sin montos** (D-15); prioridad recortable si el MVP aprieta (P-08) |
| D-15 | Sensibilidad y montos | F1 **sin montos** bajo `ViewReports`: ni agregados monetarios ni columnas `Amount` en bandeja/exports (el asiento de liquidación lleva el neto pagado). Análisis monetario = F2 condicionado a `ViewCompensation` (P-11) |
| D-16 | Permisos | **Reutilizar `PersonnelFiles.ViewReports`** (∨ `Read` ∨ `Admin`) para todas las secciones, la bandeja y los exports — un solo gate para todo el tablero. La administración del catálogo de planilla usa la vía estándar de catálogos/administración |
| D-17 | Fuentes conectables | Las secciones de módulos futuros (REQ-001/002/003) nacen como **contrato estable por sección** + metadata de **fuentes activas** (patrón de la consulta de tiempos de REQ-003): al mergear cada módulo se conecta su indicador sin romper contrato; la UI muestra qué fuentes están activas (P-14) |
| D-18 | Secuenciación | Registrar como **REQ-004**. **No depende de REQ-001…REQ-003** (consume solo módulos existentes, es read-only y chico comparado con aquellos): puede mantener el orden del backlog **o adelantarse** como quick-win de visibilidad gerencial si el negocio lo prioriza — decisión de prioridad, no técnica. La única coordinación es el catálogo de tipo de planilla con REQ-001 (P-02) |

---

## 1. Resumen del producto o requerimiento

Se construirá el **Tablero de gráficos e indicadores del módulo de acciones de personal** de CLARIHR: una vista gráfica, filtrable e imprimible/exportable a PDF de las acciones que se aplican a los empleados — asientos documentales (amonestaciones, traslados, permisos, ascensos, etc.), movimientos estructurales (contrataciones/altas, retiros/bajas, recontrataciones, liquidaciones) y, a medida que sus módulos lleguen, incapacidades, vacaciones, reconocimientos y amonestaciones estructuradas.

**Qué se construye.** Cuatro capacidades:

1. **Indicadores documentales del journal** (primera consulta corporativa del ledger de acciones): serie mensual del año, distribución por tipo y por estado, origen manual vs automático y cruce por dimensiones organizativas.
2. **Indicadores de movimientos**: altas y **bajas** por mes (con desglose por categoría y motivo de retiro), neto de plantilla, **índice de rotación**, cobertura de entrevistas de retiro y liquidaciones por estado — activando los indicadores que el tablero RRHH dejó diferidos, hoy desbloqueados por el módulo de retiro.
3. **Criterios de búsqueda**: los 6 filtros ya construidos (año, área funcional, unidad, tipo de puesto, puesto, centro de trabajo) + **mes** (nuevo) + **tipo de planilla** (formalizando el catálogo sobre el campo existente de la plaza); **nivel piramidal** queda referenciado con gancho (bloqueado por la relación puesto↔nivel, igual que en el tablero RRHH).
4. **Salidas**: impresión (vista de impresión), **exportación a PDF** (estrategia a ratificar: frontend y/o reporte PDF servidor con la infraestructura documental existente), exportación tabular de datasets y una bandeja de asientos a nivel empresa como destino del drill.

**Problema que resuelve.** Hoy las acciones de personal solo pueden verse expediente por expediente: no existe ninguna vista corporativa —ni cuantitativa ni gráfica— de cuántas acciones ocurren, de qué tipo, dónde ni cuándo; las bajas y la rotación no se miden en ningún lugar del sistema pese a que su módulo ya existe; y nada del tablero puede imprimirse ni exportarse a PDF.

**Objetivo principal.** Que RRHH y la dirección vean, filtren, impriman y exporten los indicadores estándar del módulo de acciones de personal en un solo tablero, con fuentes de datos confiables y trazables hasta el asiento/módulo de origen.

---

## 2. Objetivos del negocio

1. **Visibilidad gerencial del movimiento de personal**: una sola vista de altas, bajas, rotación y acciones aplicadas, filtrable por las dimensiones organizativas de la institución.
2. **Gestión de la rotación**: medir por fin el índice de rotación y el desglose de motivos de baja (se complementa con la cobertura de entrevistas de retiro para el análisis de causas).
3. **Control operativo del módulo de acciones**: saber cuántas acciones se registran, de qué tipo, en qué estado y por qué vía (manual vs automática) — insumo de supervisión y de adopción del módulo.
4. **Soporte a decisiones por segmento**: cruzar los indicadores por unidad, área funcional, centro de trabajo, puesto, tipo de puesto y tipo de planilla para focalizar intervenciones.
5. **Reportabilidad institucional**: impresión y exportación a PDF de las gráficas e indicadores para comités, juntas y auditorías; exportación tabular para análisis posterior.
6. **Reutilización y costo mínimo**: extender el tablero, el permiso, la capa dimensional, los exportadores y el stack PDF existentes — el desarrollo nuevo se concentra en la consulta corporativa del journal y las agregaciones.
7. **Extensibilidad sin re-trabajo**: contrato preparado para conectar los indicadores de incapacidades/vacaciones (REQ-001), reconocimientos/amonestaciones (REQ-003) y tiempo compensatorio (REQ-002) cuando existan.

---

## 3. Alcance funcional

### Fase 1 — MVP (este análisis)

- **Consulta corporativa del journal** (nueva): agregaciones tenant-wide sobre `personnel_file_personnel_actions` con filtros transversales.
- **Sección "Acciones documentales"**: serie mensual + total del periodo; por tipo; por estado; por origen (manual/automática); por dimensión organizativa.
- **Sección "Movimientos"**: altas por mes (integra el endpoint existente); **bajas por mes + por categoría + por motivo**; neto altas−bajas; **índice de rotación** (mensual/anual); cobertura de entrevistas de retiro; liquidaciones por estado (conteos).
- **Filtros**: año, **mes** (nuevo), área funcional, unidad, tipo de puesto, puesto, centro de trabajo (existentes), **tipo de planilla** (nuevo: catálogo + validación estricta + migración de limpieza — P-02) y **centro de costo** (opcional de bajo costo — P-12). Nivel piramidal: gancho documentado, sin filtro (D-11).
- **Salidas**: impresión y **exportación PDF desde el frontend** (vista de impresión — P-01/D-12, especificada con el mapa de gráficos A.5); exportación tabular xlsx/csv/json por dataset; **bandeja de asientos a nivel empresa** con export (drill).
- **Metadata del tablero**: fuentes activas por sección, configuración resuelta y catálogos necesarios para leyendas.
- **Parametrización**: catálogo `PAYROLL_TYPE_CATALOG` administrable (país, valores A.2).

### Activaciones (no son fase — se conectan al llegar cada módulo)

- **REQ-001**: indicadores de incapacidades (por riesgo/tipo/mes) y vacaciones (solicitudes/días) — la sección aparece cuando el módulo exista.
- **REQ-003**: reconocimientos y amonestaciones estructuradas (por tipo/causa/estado) — ídem.
- **REQ-002**: acreditaciones/goces de tiempo compensatorio — ídem.
- **Nivel piramidal**: filtro habilitado cuando exista la relación canónica puesto↔nivel.

### Fase 2 — Evoluciones (contrato preparado, fuera de este MVP)

- **Snapshot dimensional del asiento** (exactitud histórica de unidad/puesto/planilla al momento de la acción, solo hacia adelante).
- **Análisis monetario** (montos de liquidaciones/asientos) condicionado a `ViewCompensation`.
- **Reporte PDF generado por el servidor** (A.4-b: bloque de imagen raster sobre el motor documental dual) y su programación/envío por correo; comparativas multi-periodo y metas; tableros configurables por usuario.
- Cambio estructurado de centro de costo / movimientos con flujo (hoy solo asiento documental — fuera de este requerimiento).

---

## 4. Fuera de alcance

- **Construir los módulos fuente que faltan** (incapacidades, vacaciones, reconocimientos, amonestaciones estructuradas, tiempo compensatorio, permisos generales): el tablero **solo consume módulos existentes** — principio ya confirmado por el negocio en el tablero RRHH (2026-06-27); lo futuro se conecta al llegar (D-17).
- **La relación puesto↔nivel piramidal** (pertenece al módulo de puestos/competencias) y por tanto el filtro por nivel en F1.
- **Motor de nómina o cálculo de planilla**: "tipo de planilla" es una clasificación de la plaza para filtrar/agrupar; no se calcula nada.
- **Montos y análisis monetario** bajo `ViewReports` (D-15); nada de `ViewCompensation` en F1.
- **Registro de eventos de cambio de centro de costo** o flujos de movimiento estructurados (traslados con autorización): siguen siendo asiento documental manual; un módulo de movimientos sería un requerimiento aparte.
- **Motor de BI genérico**, constructor de reportes ad-hoc, predicciones/ML.
- **Series históricas reconstruidas** más allá de lo que las fuentes permiten (sin snapshots retroactivos de plantilla ni de dimensiones; misma limitación R-02 documentada del tablero RRHH).
- **Notificaciones** y programación de envíos (F2).
- Escritura/edición de datos desde el tablero (excepto la administración del catálogo de planilla, que usa la vía estándar).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Analista / Gestor de RRHH** | Consumidor principal: consulta el tablero, aplica filtros, hace drill a la bandeja, exporta e imprime |
| **Gerencia / Dirección de RRHH** | Lectura ejecutiva: rotación, bajas por motivo, neto de plantilla, cobertura de entrevistas |
| **Dirección / Gerencia general** | Lectura de indicadores agregados; receptores del PDF institucional |
| **Auditor / Contraloría** | Verifica indicadores contra la bandeja/asientos de origen (drill + export) |
| **Administrador de catálogos (RRHH/TI)** | Administra el catálogo de tipos de planilla y coordina el backfill de plazas |
| **Administrador de seguridad (IAM)** | Asigna `ViewReports` (mismo permiso del tablero existente) |
| **Sistema (backend)** | Calcula agregaciones, resuelve dimensiones por asignación activa, compone exports/PDF |
| **Frontend (web)** | Renderiza los gráficos, la vista de impresión y (según P-01) genera el PDF o aporta las imágenes al reporte servidor |

---

## 6. Requerimientos funcionales

> Agrupados en 5 grupos (A: núcleo de consulta y filtros · B: indicadores documentales · C: indicadores de movimientos · D: salidas · E: extensibilidad). Prioridades: Alta = imprescindible F1; Media = F1 deseable/recortable.

### Grupo A — Núcleo de consulta y filtros

### RF-001 - Consulta agregada corporativa del journal de acciones

**Descripción:** Nueva capacidad de consulta a nivel empresa sobre `personnel_file_personnel_actions` (hoy solo consultable por expediente): agregaciones por periodo, tipo, estado, origen y dimensión organizativa, con los filtros transversales del tablero.

**Reglas de negocio:**
- Tenant-scoped; join al expediente para excluir registros de otros tenants y resolver dimensiones (RN-06).
- Por defecto cuenta asientos efectivos (`APLICADA`), excluyendo `ANULADA` (RN-04); parámetro explícito para incluir todos los estados.
- Serie temporal por `ActionDateUtc` (RN-05).

**Criterios de aceptación:**
- Los agregados cuadran contra los asientos de fixtures deterministas (casos dorados A.3); un asiento `ANULADA` no cuenta por defecto y sí aparece en el desglose por estado.

**Prioridad:** Alta
**Dependencias:** Journal existente; capa dimensional existente.

### RF-002 - Filtros transversales heredados + mes

**Descripción:** Las secciones nuevas aceptan los filtros ya construidos (año, área funcional, unidad, tipo de puesto, puesto, centro de trabajo — mismos parámetros y semántica del tablero) más el **mes** (1-12, requiere año).

**Reglas de negocio:**
- Combinables con AND; valores por PublicId como hoy (RN-07).
- `month` aplica solo a indicadores de **flujo**; los snapshot lo ignoran y el contrato lo documenta (D-09).
- Nivel piramidal NO se ofrece (gancho D-11).

**Criterios de aceptación:**
- Mismo objeto de filtros produce subconjuntos consistentes entre secciones; `month` sin `year` → 400; `month=2` restringe la serie al mes 2.

**Prioridad:** Alta
**Dependencias:** RF-001; filtros existentes del tablero.

### RF-003 - Catálogo y filtro de tipo de planilla (ratificado — P-02)

**Descripción:** Formalizar el "tipo de planilla" (modalidad de pago contractual del empleado): catálogo país-scoped `PAYROLL_TYPE_CATALOG` (wire `payroll-types`, seed SV con los valores A.2 confirmados), validación por código al escribir `payrollTypeCode` en la plaza (campo existente), **migración de limpieza destructiva** de los valores libres actuales, proyección en la fila dimensional, filtro `payrollTypeCode` en el tablero y desglose en la composición.

**Reglas de negocio:**
- La migración de adopción **normaliza a código** los valores existentes que coinciden exactamente con el catálogo y **elimina (NULL) el resto** — sin backfill, sin datos ni código legacy, validación estricta desde el día 1 (RN-11, P-02).
- Un código inválido en alta/edición de plaza → 422 bilingüe (patrón validate-by-code).
- Las plazas que quedaron sin clasificar tras la limpieza se clasifican por edición natural; mientras tanto → "Sin dato".
- Coordinación con REQ-001 (sus incapacidades referencian el mismo catálogo).

**Criterios de aceptación:**
- Plaza con código inexistente/inactivo → 422; filtro por `MENSUAL` restringe todos los indicadores; tras la migración no queda ningún `payrollTypeCode` fuera del catálogo (verificable por query); plazas sin clasificar aparecen como "Sin dato" sin romper totales.

**Prioridad:** Alta
**Dependencias:** Verificación de IDs de seed (Anexo A.2).

### RF-004 - Metadata del tablero (fuentes activas + configuración)

**Descripción:** Extender/complementar `dashboard/metadata` con: fuentes activas por sección (p. ej. `INCAPACIDADES: inactiva — módulo no disponible`), catálogos necesarios para leyendas (tipos/estados de acción, tipos de planilla) y qué filtros están habilitados (planilla sí/no, nivel piramidal no).

**Reglas de negocio:**
- La metadata es la fuente de verdad del FE para mostrar/ocultar secciones y filtros (RN-14); contrato aditivo.

**Criterios de aceptación:**
- Con REQ-001 sin construir, la sección incapacidades se reporta inactiva; al conectarse la fuente, pasa a activa sin cambio de contrato.

**Prioridad:** Alta
**Dependencias:** RF-001…RF-003.

### Grupo B — Indicadores documentales (fuente: journal)

### RF-005 - Serie mensual de acciones

**Descripción:** Total de acciones del periodo y serie de 12 meses (rellena ceros) para el año filtrado, respetando los filtros transversales.

**Reglas de negocio:** Eje temporal `ActionDateUtc` (RN-05); población por defecto RN-04.

**Criterios de aceptación:** El año sin datos devuelve 12 meses en cero; el filtro por unidad restringe la serie.

**Prioridad:** Alta
**Dependencias:** RF-001.

### RF-006 - Acciones por tipo

**Descripción:** Distribución de acciones por `ActionTypeCode` (etiquetas del catálogo `action-types`), ordenada por conteo descendente.

**Reglas de negocio:** Los tipos sin ocurrencias no aparecen; códigos históricos no catalogados (si existieran) se agrupan bajo su código literal (RN-03/G-10).

**Criterios de aceptación:** Con 3 `TRASLADO` y 1 `AMONESTACION` aplicados en el rango, el desglose los refleja exactamente.

**Prioridad:** Alta
**Dependencias:** RF-001.

### RF-007 - Acciones por estado

**Descripción:** Distribución por `ActionStatusCode` sobre el universo completo de asientos del rango (incluye `BORRADOR`…`ANULADA`), independiente del default de población.

**Criterios de aceptación:** Un asiento `ANULADA` cuenta aquí y no en RF-005/RF-006 por defecto.

**Prioridad:** Alta
**Dependencias:** RF-001.

### RF-008 - Origen manual vs automático

**Descripción:** Conteo de asientos por `IsSystemGenerated` (automáticos de módulos vs registros manuales de RRHH) — indicador de adopción/operación del módulo.

**Criterios de aceptación:** Emitir una liquidación suma 1 automático; un asiento manual suma 1 manual.

**Prioridad:** Media
**Dependencias:** RF-001.

### RF-009 - Acciones por dimensión organizativa

**Descripción:** Distribución de acciones por unidad, área funcional, centro de trabajo, puesto y tipo de puesto (y tipo de planilla si RF-003 entra), resolviendo la dimensión por la asignación activa primaria actual del empleado.

**Reglas de negocio:** Aproximación D-07 documentada; "Sin asignar" explícito (RN-06).

**Criterios de aceptación:** Las acciones de un empleado sin asignación activa aparecen en "Sin asignar"; el total del desglose cuadra con RF-005.

**Prioridad:** Alta
**Dependencias:** RF-001; capa dimensional.

### Grupo C — Indicadores de movimientos (fuentes canónicas del perfil/módulos)

### RF-010 - Bajas por mes, categoría y motivo (activa el diferido del tablero RRHH)

**Descripción:** Serie mensual de bajas del año (empleados con `RetirementDate` en el periodo) + desgloses por `RetirementCategoryCode` y `RetirementReasonCode` (catálogos del módulo de retiro), con filtros transversales.

**Reglas de negocio:**
- Fuente canónica = perfil estampado por el módulo de retiro; **nunca** el journal (D-03/RN-03).
- Una baja **revertida** desaparece de la serie (el perfil se restaura); el asiento `REVERSION_BAJA` queda como traza documental (RN-09).
- Desglose por dimensión organizativa con la aproximación D-07.

**Criterios de aceptación:** Ejecutar un retiro en marzo suma 1 baja a marzo con su categoría/motivo; revertirlo la resta; el filtro por centro de trabajo restringe la serie.

**Prioridad:** Alta
**Dependencias:** Módulo de retiro existente (PR #55).

### RF-011 - Índice de rotación

**Descripción:** Rotación del periodo (mensual y anual) según la fórmula D-08, sobre la población filtrada.

**Reglas de negocio:** Headcount promedio = (activos al inicio + activos al fin) / 2 (aproximado desde `HireDate`/`RetirementDate`, misma técnica R-02 del tablero); headcount 0 → "N/D" (RN-10); fórmula visible en la metadata/leyenda.

**Criterios de aceptación:** Con 2 bajas y headcount promedio 100 → 2.0 %; sin población → N/D sin error.

**Prioridad:** Alta
**Dependencias:** RF-010.

### RF-012 - Altas y neto de plantilla

**Descripción:** Integrar las altas existentes (`dashboard/hires`) con las bajas nuevas: vista combinada altas vs bajas por mes + **neto** (altas − bajas) del periodo.

**Reglas de negocio:** Altas = `HireDate` (regla existente; la recontratación reinicia la fecha — limitación R-03 documentada); mismo objeto de filtros que RF-010.

**Criterios de aceptación:** Mes con 3 altas y 1 baja → neto +2; los totales cuadran con `hires` y RF-010 por separado.

**Prioridad:** Alta
**Dependencias:** `hires` existente; RF-010.

### RF-013 - Cobertura de entrevistas de retiro

**Descripción:** Porcentaje de bajas del periodo con entrevista de retiro **completada** (`ExitInterviewSubmission` del expediente), como KPI + conteos absolutos.

**Reglas de negocio:** Numerador = bajas del periodo con envío completado; denominador = bajas del periodo (RN-15); sin exponer contenido/score de las entrevistas en F1 (P-10).

**Criterios de aceptación:** 4 bajas, 3 con entrevista completada → 75 %; una baja revertida sale del denominador.

**Prioridad:** Media
**Dependencias:** RF-010; módulo de entrevistas existente.

### RF-014 - Liquidaciones por estado (conteos)

**Descripción:** Conteo de liquidaciones del periodo por estado (borrador/emitida/anulada), reutilizando la semántica de la bandeja de liquidaciones. **Sin montos** (D-15).

**Criterios de aceptación:** Emitir una liquidación en el rango suma 1 a "emitida"; ningún campo monetario viaja en la respuesta.

**Prioridad:** Media
**Dependencias:** Módulo de liquidaciones existente (PR #56).

### Grupo D — Salidas

### RF-015 - Impresión y exportación a PDF del tablero (ratificado: vía frontend — P-01)

**Descripción:** La **impresión** y la **exportación a PDF** se resuelven en el **frontend**: vista de impresión del tablero (encabezado con empresa, filtros aplicados y fecha de generación) imprimible por el navegador y exportable a PDF desde la misma vista. El backend **no compone PDF** en F1; su responsabilidad es que los agregados de todas las secciones estén completos y consistentes para esa vista, y que la **guía FE** especifique la vista de impresión y el **mapa de gráficos** (Anexo A.5).

**Reglas de negocio:**
- La vista impresa/exportada refleja exactamente los filtros y el momento de generación (RN-13).
- El mapa de gráficos A.5 (tipo de gráfico, orden y prioridad por indicador) es la especificación de la vista.
- El reporte PDF servidor queda como referencia F2 (A.4-b); ningún endpoint de PDF del tablero se construye en F1.

**Criterios de aceptación:**
- La vista de impresión muestra todas las secciones visibles con los filtros y la fecha en el encabezado; el PDF generado por el FE es fiel a la vista; la guía FE documenta la vista, el mapa A.5 y el mecanismo de exportación.

**Prioridad:** Alta
**Dependencias:** RF-005…RF-014 (datos completos); guía FE.

### RF-016 - Exportación tabular de datasets

**Descripción:** Exportación xlsx/csv/json de cada dataset del tablero (serie, desgloses, bajas, rotación, cobertura, liquidaciones) y de la bandeja (RF-017), con límite síncrono y jobs asíncronos.

**Reglas de negocio:** Filas en español (patrón liquidación); rate limiting y auditoría existentes (RN-12); resource keys nuevos whitelisteados para el job asíncrono; sin montos (D-15).

**Criterios de aceptación:** Export de la serie con filtros = mismos números que el endpoint JSON; export sobre el límite síncrono → 413 y disponible por job.

**Prioridad:** Alta
**Dependencias:** RF-005…RF-014; infraestructura de export existente.

### RF-017 - Bandeja corporativa de asientos (drill)

**Descripción:** `POST …/query` paginado del journal a nivel empresa: filtros por tipo, estado, origen, rango de fechas, empleado y unidad; `StatusCounts`; export propio. Es el destino del drill desde los gráficos documentales.

**Reglas de negocio:** Gate `ViewReports`; columnas: empleado, tipo, estado, fecha de acción, vigencias, referencia, origen — **sin `Amount`** (D-15); ordenamiento por fecha descendente por defecto.

**Criterios de aceptación:** Filtro tipo=`TRASLADO` + rango devuelve exactamente los asientos contados por RF-006; la columna de monto no existe en payload ni export.

**Prioridad:** Media (recortable — P-08)
**Dependencias:** RF-001.

### Grupo E — Extensibilidad

### RF-018 - Fuentes conectables para módulos futuros

**Descripción:** Contrato estable por sección + metadata de fuentes activas (RF-004): al llegar REQ-001 (incapacidades/vacaciones), REQ-003 (reconocimientos/amonestaciones) y REQ-002 (tiempo compensatorio), sus indicadores se conectan de forma aditiva; la UI muestra las fuentes activas por versión.

**Reglas de negocio:** La forma de la fila/sección no cambia al conectar una fuente (RN-14); la degradación (fuente ausente) es explícita, nunca silenciosa.

**Criterios de aceptación:** Documentación de fuentes activas por versión publicada en la guía FE; conectar una fuente futura no requiere cambios en el FE más allá de mostrar la sección.

**Prioridad:** Alta (es diseño de contrato, no código adicional)
**Dependencias:** RF-004.

---

## 7. Requerimientos no funcionales

- **Seguridad**: todo el tablero, bandeja y exports gateados por `ViewReports` (∨ `Read` ∨ `Admin`) con verificación en handler (fail-closed, familia Reporting sin `[AuthorizationPolicySet]` — convención vigente); **sin montos** bajo este permiso (D-15); payloads agregados sin datos personales sensibles (la bandeja expone lo mismo que ya expone la consulta por expediente, menos el monto); 403 sin enmascaramiento.
- **Auditoría**: exportaciones auditadas (`ReportExported`, patrón existente); el PDF servidor (si entra) registra quién/cuándo/con qué filtros; el tablero no muta datos.
- **Rendimiento**: agregaciones acotadas por tenant y rango; evaluar índice adicional del journal para consulta corporativa (hoy los índices anteponen `PersonnelFileId`; probable `(TenantId, ActionDateUtc)` — lo fija el plan técnico); proyecciones mínimas `AsNoTracking` + bucketización en memoria (patrón del tablero); rate limiting de search/export existente.
- **Concurrencia/API**: convenciones del repo — `api/v1`, GET agregados idempotentes, enums/códigos como strings, errores bilingües `extensions.code`, PublicIds en filtros; contrato **aditivo** (no se toca la forma de `overview`/`hires`/`span-of-control`/`metadata` existentes salvo extensiones opcionales).
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId`; sin jobs nocturnos (agregación on-demand); exports grandes por el subsistema asíncrono existente.
- **Usabilidad**: buckets "Sin asignar"/"Sin dato" explícitos; leyendas desde catálogos (labels ES); fórmula de rotación visible; estados "N/D"/"no configurado" en lugar de ceros engañosos; la UI declara la aproximación dimensional (D-07) y las fuentes activas.
- **Mantenibilidad**: reglas de agregación en módulo puro (patrón `PersonnelFileDashboardRules`) con tests unitarios y paridad de localización; openapi actualizado sin drift (verificar además el estado real del contrato publicado — hallazgo: las rutas de dashboard/reportería no están declaradas hoy); guía FE dedicada.
- **Compatibilidad**: 100 % aditivo sobre el tablero y el journal; la validación de `payrollTypeCode` no invalida datos históricos (agrupa en "Sin dato").
- **Accesibilidad**: (frontend) la vista de impresión y los gráficos con etiquetas/valores textuales exportables; se documenta en la guía FE.

---

## 8. Historias de usuario

### HU-001 - Consultar el tablero de acciones
Como **analista de RRHH**, quiero **ver la serie mensual y la distribución por tipo/estado de las acciones de personal, filtrada por año, mes y unidad**, para **supervisar la operación del módulo y detectar patrones**.

**Criterios de aceptación:**
- Dado un año con asientos aplicados, cuando abro el tablero, entonces veo la serie de 12 meses y los desgloses por tipo y estado coherentes entre sí.
- Dado un filtro por unidad, cuando lo aplico, entonces todas las secciones se recalculan sobre esa población.

### HU-002 - Analizar bajas y rotación
Como **gerente de RRHH**, quiero **ver las bajas por mes con su categoría y motivo, junto al índice de rotación del periodo**, para **anticipar y corregir las causas de salida**.

**Criterios de aceptación:**
- Dado un retiro ejecutado en el periodo, cuando consulto, entonces la baja aparece en su mes con categoría y motivo, y la rotación refleja la fórmula ratificada.
- Dado un retiro revertido, entonces la baja desaparece de la serie y del denominador de cobertura.

### HU-003 - Imprimir y exportar a PDF
Como **director**, quiero **imprimir el tablero y exportarlo a PDF con los filtros aplicados**, para **presentarlo en comité y archivarlo**.

**Criterios de aceptación:**
- Dada la vista con filtros aplicados, cuando imprimo, entonces la vista de impresión muestra todas las secciones con sus gráficas.
- Cuando exporto a PDF, entonces obtengo un documento con encabezado (empresa, filtros, fecha de generación) y los indicadores/gráficas de la vista.

### HU-004 - Exportar un dataset a Excel
Como **analista de RRHH**, quiero **exportar a xlsx el desglose de acciones por tipo del periodo**, para **trabajarlo en mis propias hojas de cálculo**.

**Criterios de aceptación:**
- Dado el desglose visible, cuando exporto, entonces el archivo contiene los mismos valores que la pantalla, en español y sin columnas de montos.

### HU-005 - Drill a los asientos
Como **auditor**, quiero **abrir desde un gráfico la lista de asientos que lo componen**, para **verificar los indicadores contra su origen**.

**Criterios de aceptación:**
- Dado el conteo de `TRASLADO` del trimestre, cuando hago drill, entonces la bandeja lista exactamente esos asientos con empleado, fechas, estado y origen.

### HU-006 - Configurar tipos de planilla
Como **administrador de catálogos**, quiero **definir los tipos de planilla y clasificar las plazas**, para **habilitar el filtro y el desglose por tipo de planilla**.

**Criterios de aceptación:**
- Dado el catálogo sembrado y editable, cuando clasifico las plazas, entonces el filtro devuelve la población correcta y las plazas sin clasificar aparecen como "Sin dato".
- Dado un código inválido en una plaza, entonces recibo 422 bilingüe.

### HU-007 - Monitorear cobertura de entrevistas
Como **dirección de RRHH**, quiero **ver qué porcentaje de las bajas del periodo tuvo entrevista de retiro completada**, para **asegurar que el proceso de salida se cumple**.

**Criterios de aceptación:**
- Dadas 4 bajas y 3 entrevistas completadas en el periodo, entonces el KPI muestra 75 % con sus absolutos.

### HU-008 - Fuentes que se activan solas
Como **analista de RRHH**, quiero **que los indicadores de incapacidades/vacaciones aparezcan en el tablero cuando el módulo exista**, para **no esperar un re-desarrollo del tablero**.

**Criterios de aceptación:**
- Dado que REQ-001 aún no existe, entonces la metadata reporta la sección inactiva y la UI no la ofrece.
- Dado que REQ-001 se libera y conecta, entonces la sección aparece sin cambios de contrato.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | El tablero es **read-only**: ningún endpoint de sección/bandeja/export muta datos; la única administración asociada es el catálogo de tipos de planilla por la vía estándar |
| RN-02 | Acceso por `PersonnelFiles.ViewReports` (∨ `Read` ∨ `Admin`) verificado **en el handler** de cada query (fail-closed), incluyendo bandeja y exports |
| RN-03 | **Fuente canónica por indicador** (Anexo A.1): el journal alimenta solo los indicadores documentales; altas = `HireDate`; bajas = `RetirementDate` + categoría/motivo del perfil; el journal **nunca** alimenta altas/bajas/rotación |
| RN-04 | Población documental por defecto = asientos **`APLICADA`** (excluye `ANULADA` y estados intermedios); parámetro explícito para el universo completo; el desglose por estado siempre muestra todos |
| RN-05 | Eje temporal de las series documentales = `ActionDateUtc`; las vigencias del asiento no definen el periodo |
| RN-06 | Dimensiones organizativas resueltas por la **asignación activa primaria actual**; empleados sin asignación → "Sin asignar"; valores no clasificados → "Sin dato"; ninguna fila se descarta silenciosamente |
| RN-07 | Filtros combinables AND, valores por PublicId/código, mismos parámetros que el tablero existente + `month` (flujo); `month` requiere `year` |
| RN-08 | `month` no aplica a indicadores snapshot; el contrato/metadata declara qué secciones lo aceptan |
| RN-09 | Una baja **revertida** sale de las series y ratios (fuente = estado actual del perfil); los asientos `BAJA`/`REVERSION_BAJA` permanecen como traza documental en los indicadores del journal |
| RN-10 | Rotación con headcount promedio 0 → "N/D" (nunca división por cero ni 0 % engañoso); la fórmula ratificada se muestra en la leyenda/metadata |
| RN-11 | `payrollTypeCode`: **validación estricta por catálogo** en toda escritura; la migración de adopción normaliza las coincidencias exactas y **elimina** los valores libres no conformes (sin backfill, sin datos ni código legacy — P-02); plazas sin clasificar → "Sin dato" |
| RN-12 | Exportaciones (tabulares y PDF) con rate limiting, límite síncrono (413 → job asíncrono) y auditoría `ReportExported` |
| RN-13 | El PDF/impresión refleja **exactamente** los filtros y el momento de generación (fecha/hora y filtros impresos en el encabezado) |
| RN-14 | Contrato **aditivo y estable por sección**: conectar fuentes futuras no cambia la forma de las filas; la metadata declara fuentes activas y filtros habilitados; degradación explícita, nunca silenciosa |
| RN-15 | Cobertura de entrevistas = bajas del periodo con `ExitInterviewSubmission` completada ÷ bajas del periodo; sin exponer contenido ni score en F1 |
| RN-16 | Sin montos bajo `ViewReports` (ni agregados, ni columnas, ni exports); el análisis monetario es F2 con `ViewCompensation` |

---

## 10. Flujos principales

### Flujo 1 — Consulta del tablero de acciones
1. El analista abre el tablero (sección acciones de personal) con su permiso `ViewReports`.
2. El FE carga la metadata (fuentes activas, catálogos de leyendas, filtros habilitados).
3. El analista aplica filtros (año, mes, unidad, tipo de planilla…).
4. El sistema devuelve los agregados de cada sección (serie, tipos, estados, origen, dimensiones).
5. El FE grafica; los buckets "Sin asignar"/"Sin dato" se muestran como categorías.

### Flujo 2 — Análisis de movimientos
1. La gerencia consulta la sección de movimientos del año.
2. El sistema deriva altas (`HireDate`), bajas (`RetirementDate` + categoría/motivo), neto y rotación sobre la población filtrada.
3. La gerencia revisa el desglose de motivos de baja y la cobertura de entrevistas para el mismo periodo.

### Flujo 3 — Impresión / exportación a PDF (vía frontend — P-01)
1. El usuario pulsa "Imprimir" → el FE abre la vista de impresión (encabezado: empresa, filtros, fecha de generación) y delega al navegador.
2. El usuario pulsa "Exportar PDF" → el FE genera el PDF de esa misma vista (fiel a lo que se ve).
3. El backend no interviene: los datos ya viajaron en las consultas de las secciones.

### Flujo 4 — Exportación tabular
1. El usuario exporta un dataset (p. ej. bajas por motivo) en xlsx.
2. El sistema aplica los mismos filtros, respeta el límite síncrono (o deriva a job) y entrega el archivo en español, sin montos.

### Flujo 5 — Drill de auditoría
1. Desde el gráfico "acciones por tipo", el auditor abre el detalle.
2. El FE llama la bandeja corporativa con los mismos filtros + tipo seleccionado.
3. La bandeja lista los asientos (empleado, fechas, estado, origen) con export propio.

### Flujo 6 — Parametrización del tipo de planilla
1. El administrador revisa el catálogo sembrado (editable) y ajusta los tipos a su institución.
2. RRHH clasifica/backfillea las plazas existentes (hoy texto libre).
3. El filtro y el desglose por planilla se habilitan; lo no clasificado aparece como "Sin dato".

### Flujo 7 — Activación de una fuente futura
1. Se libera REQ-001 (incapacidades/vacaciones) y su PR conecta la fuente al contrato del tablero.
2. La metadata pasa la sección a activa; la UI la muestra; el contrato no cambia.

---

## 11. Flujos alternativos y excepciones

- **Usuario sin permiso** (`ViewReports`/`Read`/`Admin`) → 403 en secciones, bandeja y exports.
- **Periodo sin datos** → series con 12 meses en cero y desgloses vacíos (nunca error).
- **`month` sin `year`** → 400; `month` sobre sección snapshot → ignorado y documentado (metadata).
- **Filtro de planilla sin catálogo activo / sin ratificar P-02** → filtro no ofrecido (metadata lo declara deshabilitado).
- **Plazas con `payrollTypeCode` libre no reconocido** → bucket "Sin dato" (no rompe totales ni filtros).
- **Empleado sin asignación activa** → dimensiones "Sin asignar" (categoría legítima).
- **Headcount promedio 0** → rotación "N/D".
- **Baja revertida** → sale de series/ratios; su traza documental permanece en el journal.
- **Export sobre el límite síncrono** → 413 con indicación de usar el job asíncrono; **job fallido** → estado consultable y reintento (subsistema existente).
- **Códigos históricos de asiento no catalogados** (previos a la validación) → se agrupan por su código literal en los desgloses (visibles, no ocultados) — insumo del plan de normalización (P-13).
- **Dos consultas concurrentes con filtros distintos** → sin estado compartido (read-only, sin caché con fuga entre tenants).

---

## 12. Datos requeridos

> El tablero es read-only: **no crea entidades transaccionales**. Los datos nuevos son un catálogo (condicionado a P-02) y datasets derivados. Convenciones del repo aplican (Ids internos + `publicId`, `TenantId`, auditoría, catálogos con baja lógica).

### Entidad: Tipo de planilla (`PayrollTypeCatalogItem` — catálogo país-scoped, seed SV editable; condicionado a P-02)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Código único por país (normalizado) | Identificador y descripción del tipo de planilla (p. ej. `MENSUAL`) |
| sortOrder | Entero | Sí | — | Orden de presentación |
| isActive | Booleano | Sí | Baja lógica | Inactivo no seleccionable en plazas nuevas |

**Cambio asociado (aditivo):** `PersonnelFileEmploymentAssignment.payrollTypeCode` pasa de texto libre a **código validado** contra este catálogo en escrituras nuevas (el campo y su longitud no cambian).

### Datos consumidos del journal (`PersonnelFilePersonnelAction` — existente, sin cambios)

| Campo | Uso en el tablero |
|---|---|
| ActionTypeCode / ActionStatusCode | Ejes "por tipo" y "por estado" (leyendas de catálogo) |
| ActionDateUtc | Eje temporal de series (RN-05) |
| EffectiveFromUtc / EffectiveToUtc | Solo columnas informativas de la bandeja |
| IsSystemGenerated | Indicador de origen (RF-008) y columna de bandeja |
| PersonnelFileId | Join a expediente → dimensiones (RN-06) y drill |
| Description / Reference | Solo columnas de bandeja |
| Amount / CurrencyCode | **NO viaja** en F1 (RN-16) |

### Datasets derivados (no persistidos — contratos de respuesta)

| Dataset | Forma (resumen) |
|---|---|
| Serie de acciones | `{ year, month?, byMonth[12]{month,count}, total }` |
| Desgloses documentales | `[{ key, label, count }]` por tipo / estado / origen / dimensión (mismo shape `DashboardBreakdownResponse` del tablero) |
| Bajas | `{ year, byMonth[12], total, byCategory[], byReason[] }` |
| Altas + neto | `{ year, hiresByMonth[12], separationsByMonth[12], netByMonth[12], totals }` |
| Rotación | `{ period, separations, averageHeadcount, ratePercent | null }` |
| Cobertura de entrevistas | `{ separations, interviewsCompleted, coveragePercent | null }` |
| Liquidaciones | `[{ statusCode, label, count }]` del periodo |
| Fila de bandeja | `{ employeePublicId, employeeName, actionTypeCode, actionStatusCode, actionDateUtc, effectiveFromUtc?, effectiveToUtc?, reference?, isSystemGenerated }` (sin monto) |
| Metadata | `{ sections[{key, active, acceptsMonth}], filters[{key, enabled}], catalogs (action-types, action-statuses, payroll-types), rotationFormula }` |

### Reporte PDF (solo referencia F2 — P-01 ratificó la vía frontend)

Si en el futuro el negocio exige un documento servidor: composición `DocumentModel` (título + encabezado con empresa/filtros/fecha + secciones `TableBlock`/`KeyValueBlock` + **nuevo bloque de imagen raster** con las gráficas aportadas por el FE), entregado por el motor dual y/o `report-export-jobs`. **No se construye en F1.**

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Tablero RRHH existente** | Interna | Se extiende su familia de endpoints, filtros, permiso y metadata; sin romper contratos (`overview`/`hires`/`span-of-control`/`metadata` intactos) |
| **Journal de acciones** | Interna (lectura) | Primera consulta corporativa del ledger; los módulos que ya escriben asientos (retiro, reversión, recontratación, liquidación) no cambian |
| **Módulos de retiro / entrevistas / liquidaciones** | Interna (lectura) | Fuentes canónicas de bajas/rotación, cobertura y conteos |
| **Motor PDF documental** | Interna (**F2, referencia**) | No se usa en F1 (P-01 ratificó PDF frontend); si F2 activa el reporte servidor: `DocumentModel` + motor dual + nuevo bloque de imagen raster (A.4-b) |
| **Subsistema de exportación** | Interna | `ReportExportFileWriter` (xlsx/csv/json) + `ReportExportDeliveryService` (límites/auditoría) + `report-export-jobs` (resource keys nuevos) |
| **Catálogos generales** | Interna (lectura) | `action-types`, `action-statuses`, catálogos de retiro (categorías/motivos), `payroll-types` (nuevo) |
| **Frontend (web)** | Contrato | Render de gráficos (mapa A.5), **vista de impresión y generación del PDF** (P-01); guía FE dedicada |
| **REQ-001 / REQ-002 / REQ-003** | Interna (futura) | Conectan sus indicadores como fuentes del contrato estable (D-17); coordinación del catálogo `payroll-types` con REQ-001 (P-02) |
| **Correo / programación de reportes** | Fase 2 | Envío/agenda del reporte PDF |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Analista / Gestor de RRHH | `PersonnelFiles.ViewReports` (o `Read`) | Solo lectura; sin montos (RN-16) |
| Gerencia / Dirección | `ViewReports` | Ídem |
| Auditor | `ViewReports` | Drill y exports; sin montos |
| Administrador de catálogos | Administración estándar de catálogos | Gestiona `payroll-types`; no requiere `ViewReports` para administrar |
| Administrador (empresa) | `Admin` (satisface el gate de lectura) | La edición de plazas (backfill de planilla) usa los permisos de expediente existentes |
| Empleado (autogestión) | — | **Sin acceso** al tablero corporativo (no hay vista self en F1) |
| Sistema de planilla externa | — | Sin integración directa (los exports tabulares son para análisis, no insumo de nómina) |

---

## 15. Criterios de aceptación generales

1. **Ratificación previa**: ✅ **cumplida (2026-07-05)** — D-01…D-18 aprobadas por unanimidad y P-01…P-14 respondidas (§17).
2. Reglas de agregación como **módulo puro** (patrón `PersonnelFileDashboardRules`) con suite unitaria (casos dorados A.3) y paridad de localización.
3. Suite de integración completa (agregados contra fixtures deterministas; filtros combinados incluido mes y planilla; gates 403; bandeja + exports con límites y auditoría; **migración de limpieza de `payrollTypeCode` verificada** — coincidencias normalizadas, no conformes eliminados; buckets "Sin asignar"/"Sin dato"; baja revertida) **en verde junto con la suite existente**.
4. **Cero regresión del tablero existente**: contratos de `overview`/`hires`/`span-of-control`/`metadata` intactos (extensiones solo aditivas).
5. Los indicadores **cuadran contra sus fuentes canónicas** (A.1) en tests de integración (p. ej. bajas del tablero = retiros ejecutados no revertidos del periodo).
6. Catálogo `payroll-types` (si entra) con migración `HasData` idempotente e IDs verificados contra `GlobalCatalogSeedData` (bloque tentativo A.2); validación por código con errores 422 bilingües.
7. openapi actualizado **sin drift** para los endpoints nuevos (y verificación del estado del contrato publicado para la familia dashboard).
8. Exportaciones con rate limiting, límite síncrono (413) y auditoría `ReportExported`; resource keys asíncronos whitelisteados.
9. Sin montos en ninguna respuesta/export bajo `ViewReports` (verificado por test).
10. Guía de integración frontend publicada (`guia-integracion-frontend-tablero-acciones-personal.md`) con contratos, filtros, semántica flujo/snapshot, fuentes activas y la vía de impresión/PDF ratificada.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Calidad/cobertura del journal (el mayor para los indicadores documentales)**: la alimentación manual depende de la adopción; el histórico previo a los asientos automáticos es parcial y pudo capturar códigos libres (pre-validación). El tablero documental "muestra lo registrado" — comunicarlo, y acompañar con plan de registro retroactivo/normalización (P-13). Los indicadores de **movimientos** no sufren esto (fuentes canónicas de perfil/módulos).
- **Lectura errónea por la aproximación dimensional** (D-07): acciones antiguas se agrupan por la posición actual del empleado; en instituciones con mucha rotación interna puede distorsionar desgloses históricos. Mitigación: nota visible en UI/leyendas + P-07; snapshot = F2.
- **Doble narrativa altas/bajas**: un asiento manual `CONTRATACION`/`BAJA` no altera los indicadores de movimientos (fuente = perfil), pero un usuario podría esperar lo contrario. Mitigación: regla D-03 publicada en la guía y leyendas de fuente por indicador.
- ~~PDF con gráficas~~ **Resuelto por ratificación (P-01)**: la vía frontend elimina el riesgo técnico backend del PDF; el riesgo residual (fidelidad de la vista de impresión) es del FE y se especifica en la guía (A.5).
- **Tipo de planilla sin clasificar tras la limpieza**: la migración elimina los valores libres no conformes (P-02) y no hay backfill, así que el filtro nace con "Sin dato" dominante hasta que RRHH clasifique las plazas por edición natural. Mitigación: comunicarlo en la guía/UI ("Sin dato" es estado esperado inicial) y a la empresa en el despliegue; los valores que ya coinciden con el catálogo (p. ej. `MENSUAL`) se conservan normalizados.
- **Rendimiento de la consulta corporativa del journal**: los índices actuales anteponen `PersonnelFileId`; volúmenes grandes podrían requerir índice nuevo (se decide en el plan técnico con EXPLAIN sobre datos realistas).
- **Contrato publicado desactualizado**: la exploración detectó que el openapi publicado no declara las rutas de la familia dashboard/reportería; riesgo de fricción FE. Mitigación: regenerar/actualizar como parte del cierre (criterio 7).

### Supuestos

- El **frontend renderiza los gráficos** (el backend entrega agregados JSON); la impresión es capacidad del navegador sobre una vista de impresión.
- La población y semántica del tablero RRHH existente (activos por defecto, año aproximado R-02, recontratación reinicia `HireDate` R-03) **se heredan sin cambios**.
- Tenant mono-país (SV); catálogos país como el resto del sistema.
- Volumen del journal moderado (decenas de miles de asientos por tenant, no millones) — agregación on-demand suficiente en F1.
- La nómina es externa: "tipo de planilla" es clasificación organizativa, no configuración de cálculo.
- `ViewReports` ya está desplegado y asignado (tablero existente en uso).

### Dependencias

- **Ratificación del negocio**: ✅ resuelta (2026-07-05) — P-01…P-14 respondidas; el plan técnico queda desbloqueado.
- **Ninguna dependencia dura de REQ-001…REQ-003** (D-18): el tablero consume módulos existentes; puede adelantarse en el backlog si el negocio lo decide.
- **Coordinación con REQ-001**: el catálogo de tipo de planilla queda **especificado aquí** (A.2, país, seeds tentativos A.2); lo siembra el requerimiento que se construya primero y el otro lo reutiliza — nunca se define dos veces.
- Verificación de IDs de seed libres al abrir el primer PR (bloque tentativo A.2; reservas vigentes: REQ-001 `-9485…-9489` y `-9850…-9862`, REQ-002 `-9865…-9871`, REQ-003 `≤ -9875`, rangos del tablero `-9500…-9514`).
- ~~PDF servidor~~ descartado en F1 (P-01): sin dependencias de motor/infraestructura PDF para este requerimiento.

---

## 17. Preguntas al cliente o stakeholders — P-01…P-14 (**RESPONDIDAS — ratificación 2026-07-05**)

> ✅ El negocio respondió las 14 preguntas. P-04…P-14 aceptaron **textualmente** la propuesta del análisis (la columna derecha es, por tanto, la respuesta ratificada). P-01, P-02 y P-03 se registran abajo con la respuesta literal del negocio.

| # | Pregunta | Respuesta del negocio (2026-07-05) |
|---|---|---|
| P-01 | **Impresión/PDF (crítica)**: ¿basta con que la **vista del tablero se imprima y exporte a PDF desde el navegador/frontend**, o se requiere un **documento PDF institucional generado por el servidor**? | **«Basta con que la vista del tablero se imprima y exporte a PDF desde el navegador/frontend»** → D-12 ratificada por la **vía (a) FE-only**; cero trabajo backend de PDF en F1; el reporte servidor queda como referencia F2 (A.4-b) |
| P-02 | **Tipo de planilla (crítica)**: ¿qué significa exactamente? ¿Los valores A.2 son correctos? ¿Catálogo país o maestro por empresa? ¿Quién clasifica/backfillea las plazas existentes? | **«Es el tipo de contrato del empleado»** (la modalidad de pago contractual de la plaza — distinta del `contractTypeCode` existente); **«A.2 es correcto»**; **catálogo por país**; **«no debe haber backfill: eliminar lo que se necesite eliminar sin dejar datos o código legacy»** → D-10 ratificada con **migración de limpieza destructiva** (coincidencias exactas se normalizan a código; el resto se elimina/NULL; validación estricta sin rutas de compatibilidad) |
| P-03 | **Set de indicadores (crítica)**: ¿los 11 indicadores propuestos (D-04) cubren los "estándares" esperados? ¿Qué gráficas concretas espera ver el cliente (tipos de gráfico, orden, prioridad)? | **«Los 11 indicadores propuestos (D-04) cubren los estándares esperados»**; las gráficas concretas que el cliente espera (tipo de gráfico, orden y prioridad por indicador) quedan definidas en el **mapa de gráficos del Anexo A.5**, que la guía FE adopta como especificación de la vista |
| P-04 | ¿Los indicadores documentales deben contar por defecto solo asientos **aplicados** (`APLICADA`) o todo el universo? ¿Las `ANULADA` se ven solo en el desglose por estado? | Default = efectivos (`APLICADA`), universo completo bajo parámetro; `ANULADA` visible solo en "por estado" (D-05) |
| P-05 | ¿La serie temporal usa la **fecha de la acción** (`ActionDateUtc`) o la **vigencia** (`EffectiveFromUtc`)? | Fecha de la acción (D-06); vigencias informativas |
| P-06 | **Rotación**: ¿aceptan la fórmula propuesta (bajas ÷ headcount promedio [(inicio+fin)/2] × 100)? ¿Debe separarse voluntaria/involuntaria y con qué mapeo de categorías de retiro? | Fórmula D-08 con desglose por categoría como vista secundaria; el mapeo voluntaria/involuntaria se define con el catálogo de categorías |
| P-07 | ¿Aceptan que las dimensiones organizativas de acciones **pasadas** se resuelvan por la asignación activa **actual** (aproximación documentada)? ¿O exigen exactitud histórica (→ snapshot dimensional en el asiento, F2 y solo hacia adelante)? | Aceptar la aproximación en F1 con nota en UI (D-07); snapshot F2 si el negocio lo exige |
| P-08 | ¿La **bandeja corporativa de asientos** (drill) entra en F1? ¿Qué columnas mínimas? | Sí, con columnas no monetarias (D-14); recortable a segunda entrega si el MVP aprieta |
| P-09 | Las **bajas/rotación** activadas, ¿se muestran también dentro de la vista del tablero RRHH existente (junto a las altas) o solo en la sección de acciones? (una sola implementación; decisión de UI/contrato) | Una sola implementación expuesta en la sección de movimientos; la UI puede referenciarla desde ambas vistas |
| P-10 | **Cobertura de entrevistas**: ¿entra en F1? ¿Debe incluir algún KPI del contenido (p. ej. score promedio) o solo cobertura? | Cobertura sí (RF-013); contenido/score fuera de F1 (sensibilidad y foco) |
| P-11 | ¿Se requieren **montos agregados** en alguna vista F1 (p. ej. total liquidado del periodo)? | No en F1 (D-15/RN-16); F2 con `ViewCompensation` |
| P-12 | ¿Agregar **centro de costo** como filtro/desglose adicional? (el dato ya existe en la plaza; costo marginal; no estaba en la lista del cliente) | Sí, como filtro opcional de bajo costo — confirmar utilidad con el cliente |
| P-13 | **Adopción/calidad del journal**: ¿habrá registro retroactivo de acciones históricas relevantes? ¿Se requiere normalizar códigos libres capturados antes de la validación por catálogo? | Campaña de adopción + normalización puntual antes del go-live del tablero documental; los indicadores de movimientos no dependen de esto |
| P-14 | **Fuentes futuras**: ¿confirman el orden de conexión (REQ-001 incapacidades/vacaciones → REQ-003 reconocimientos/amonestaciones → REQ-002 compensatorio)? ¿La UI debe mostrar "fuentes activas" por sección (propuesto sí)? | Conexión al mergear cada módulo (aditiva, D-17); metadata + UI declaran fuentes activas |

---

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar primero P-01 (PDF), P-02 (tipo de planilla) y P-03 (set de indicadores)**: fijan el alcance del backend (bloque de imagen en los renderers sí/no; catálogo+backfill sí/no; lista de secciones). El resto ajusta defaults.
2. **No construir un segundo tablero**: extender la familia, el permiso y los filtros existentes (D-02). Un stack paralelo duplicaría gates, metadata y mantenimiento sin valor.
3. **Activar bajas y rotación aquí y una sola vez** (G-03): el módulo que las bloqueaba ya existe; son los indicadores de mayor valor gerencial del levantamiento y cierran el diferido del tablero RRHH con una sola implementación.
4. **Publicar la regla de fuente canónica** (D-03/A.1) en la guía FE y en las leyendas: es la vacuna contra la doble narrativa journal-vs-perfil y contra conclusiones erradas de auditoría.
5. **Tratar el tablero documental como espejo de la operación, no como verdad histórica**: su valor crece con la adopción del journal (P-13). Lanzarlo junto con una campaña de registro (y la normalización de códigos libres) — de lo contrario mostrará poco y se culpará al tablero.
6. **Empezar el PDF por el piso frontend** y dejar el reporte servidor como decisión de negocio informada (A.4): satisface el requerimiento literal con riesgo casi nulo; la vía institucional queda especificada y estimable si el cliente la exige.
7. **Definir el catálogo de tipo de planilla UNA vez y con REQ-001 en la mesa** (P-02): su análisis ya asume ese catálogo para incapacidades; duplicarlo o divergir sería deuda inmediata. El backfill necesita dueño y fecha.
8. **Aprovechar la independencia del requerimiento** (D-18): es read-only, chico comparado con REQ-001…003 y de alto impacto visible. Si el negocio necesita un quick-win gerencial, puede adelantarse sin tocar la secuencia técnica de los demás.
9. **MVP si hay que recortar**: grupos A + B + C con exportación tabular (RF-016) y la impresión/PDF por la vía FE; la bandeja (RF-017) y los KPIs Media (RF-008/RF-013/RF-014) pueden ser segunda entrega. No recortar bajas/rotación: son el corazón del valor.
10. **F2 ya perfilado**: snapshot dimensional del asiento, análisis monetario con `ViewCompensation`, nivel piramidal (cuando exista la relación), programación/correo del reporte, comparativas multi-periodo, y conexión de las fuentes REQ-001/002/003 conforme lleguen.

---

## Anexo A — Referencias y propuestas

### A.1 Mapa indicador → fuente canónica (regla D-03)

| Indicador | Fuente canónica | Nota |
|---|---|---|
| Serie/tipo/estado/origen/dimensión de acciones | Journal `PersonnelFilePersonnelAction` | Default `APLICADA`; eje `ActionDateUtc` |
| Altas por mes | `PersonnelFileEmployeeProfile.HireDate` | Regla existente del tablero (`hires`); recontratación reinicia fecha (R-03) |
| Bajas por mes/categoría/motivo | `RetirementDate` + `RetirementCategoryCode`/`RetirementReasonCode` (perfil, estampado por el módulo de retiro) | **Nunca** el journal (asiento `BAJA` solo existe desde PR #55); baja revertida sale de la serie |
| Neto de plantilla | Altas − bajas (fuentes anteriores) | — |
| Índice de rotación | Bajas + headcount promedio derivado (`HireDate`/`RetirementDate`) | Aproximación R-02 heredada |
| Cobertura de entrevistas | `ExitInterviewSubmission` (completadas) ÷ bajas del periodo | Sin contenido/score en F1 |
| Liquidaciones por estado | `PersonnelFileSettlement.StatusCode` | Solo conteos (sin montos) |
| (Opcional, mención) Cambios de tipo de contrato | `PersonnelFileContractHistory` | Fuente derivable si el negocio lo pide en la ratificación |
| Incapacidades / vacaciones | REQ-001 (futuro) | Fuente conectable — inactiva hasta mergear |
| Reconocimientos / amonestaciones estructuradas | REQ-003 (futuro) | Fuente conectable |
| Tiempo compensatorio | REQ-002 (futuro) | Fuente conectable |

### A.2 Catálogo de tipos de planilla (**RATIFICADO — P-02**: «A.2 es correcto», catálogo por país; editable)

Wire key `payroll-types`, país-scoped (SV), seed `HasData`:

| Código | Descripción |
|---|---|
| `MENSUAL` | Planilla mensual |
| `QUINCENAL` | Planilla quincenal |
| `SEMANAL` | Planilla semanal |
| `POR_DIA` | Planilla por día / jornales |
| `POR_OBRA` | Planilla por obra o servicio |
| `OTRO` | Otro |

**Seeds tentativos**: bloque contiguo **`-9520…-9525`** (zona de catálogos del tablero: rangos de edad `-9500…-9504` y antigüedad `-9510…-9514` ya ocupados). **Verificación obligatoria contra `GlobalCatalogSeedData` al abrir el PR** — reservas vigentes que no deben tocarse: `-9485…-9489` y `-9850…-9862` (REQ-001), `-9865…-9871` (REQ-002), `≤ -9875` (REQ-003), `-9490…-9496` (`ACTION_STATUS_CATALOG`).

### A.3 Casos dorados sugeridos para la validación del negocio

1. **Serie documental**: 3 asientos `APLICADA` (feb, feb, may) + 1 `ANULADA` (feb) → serie {feb: 2, may: 1}; desglose por estado muestra la anulada; con "incluir todos" la serie da {feb: 3, may: 1}.
2. **Fuente canónica de bajas**: retiro ejecutado el 15-mar (categoría RENUNCIA) → bajas{mar: 1} y el journal suma 1 `BAJA` en documentales; un asiento manual `BAJA` adicional NO altera bajas{}.
3. **Reversión**: revertir ese retiro → bajas{mar: 0}; los asientos `BAJA` y `REVERSION_BAJA` siguen contando como acciones documentales.
4. **Rotación**: headcount promedio 100 y 2 bajas del periodo → 2.0 %; empresa sin empleados → "N/D".
5. **Neto**: 3 altas y 1 baja en abril → neto +2; totales cuadran con `hires` y bajas por separado.
6. **Dimensión aproximada**: empleado con acción en enero trasladado de unidad en junio → la acción aparece en su unidad ACTUAL (comportamiento documentado D-07).
7. **Mes**: `year=2026&month=2` restringe series al mes 2; `month` sin `year` → 400.
8. **Tipo de planilla**: plaza clasificada `MENSUAL` cuenta en el filtro; plaza con texto libre viejo → "Sin dato"; código inválido al editar plaza → 422.
9. **Cobertura**: 4 bajas, 3 entrevistas completadas → 75 %.
10. **Permisos**: usuario sin `ViewReports`/`Read`/`Admin` → 403 en sección, bandeja y export; export audita `ReportExported`.
11. **Sin montos**: ninguna respuesta/export del tablero o bandeja contiene `amount` (test de contrato).
12. **PDF** (según vía ratificada): el documento refleja filtros y fecha de generación; con imagen corrupta (vía servidor) → 422 sin PDF parcial.

### A.4 Opciones de impresión/exportación PDF (insumo de P-01 — **RESUELTO: el negocio ratificó la opción (a)**; (b) queda como referencia F2)

| Opción | Qué es | Costo/riesgo | Cuándo elegirla |
|---|---|---|---|
| **(a) FE-only** (piso propuesto) | Vista de impresión + PDF generado por el navegador/librería FE | Backend: cero. Riesgo casi nulo. Fidelidad = lo que se ve | Si el PDF es para imprimir/compartir informalmente |
| **(b) PDF servidor con imágenes del FE** | El FE envía las gráficas como PNG; el backend compone el "Reporte del tablero" con `DocumentModel` + **nuevo bloque de imagen raster** (enseñado a `QuestPdfDocumentRenderer` y `DocumentModelHtmlSerializer`) y lo entrega síncrono o por `report-export-jobs` | Backend medio (bloque nuevo en 2 renderers + endpoint/job + contrato de subida); coordinación FE | Si se exige documento institucional uniforme, archivable, asíncrono o (F2) enviado por correo |
| **(c) PDF servidor con gráficas nativas** | El backend dibuja las gráficas (SVG/HTML vía Gotenberg, o librería de rasterización nueva para QuestPDF) | Alto: dependencia de motor específico o librería nueva + código de charting propio; hoy no existe nada de esto en la solución | Solo si (b) resulta inviable para el FE y aun así se exige servidor |

> Nota verificada: el AST documental no tiene bloque de imagen/gráfico y ambos renderers lanzan `NotSupportedException` ante bloques desconocidos; QuestPDF `.Image()` es raster-only (precedente: logo de constancias). La opción (b) funciona idéntica con ambos motores; la (c) ataría el reporte a Gotenberg o a una librería nueva.

### A.5 Mapa de gráficos del tablero (especificación de la vista — P-03; la guía FE lo adopta)

> Orden = disposición sugerida de arriba hacia abajo (lo gerencial primero); la vista de impresión (RF-015) respeta este mismo orden. Todos los gráficos muestran valores absolutos accesibles (etiqueta/tooltip) y los buckets "Sin asignar"/"Sin dato" como categorías.

| Orden | Indicador (D-04) | Gráfico | Prioridad |
|---|---|---|---|
| 1 | Fila de KPIs del periodo: total de acciones · altas · bajas · neto · **rotación %** · **cobertura de entrevistas %** | Tarjetas de indicador (stat tiles) con valor y absolutos | Alta |
| 2 | (6)+(7)+(8) Altas vs bajas por mes + neto | Barras agrupadas mensuales (altas/bajas) + línea de neto superpuesta | Alta |
| 3 | (9) Índice de rotación mensual | Línea mensual (con la fórmula visible en la leyenda — RN-10) | Alta |
| 4 | (7) Bajas por categoría / por motivo | Dona (categoría) + barras horizontales top-N (motivo) | Alta |
| 5 | (1) Serie mensual de acciones | Barras mensuales (12 meses, ceros incluidos) | Alta |
| 6 | (2) Acciones por tipo | Barras horizontales ordenadas desc (etiquetas del catálogo) | Alta |
| 7 | (5) Acciones por dimensión organizativa | Barras horizontales con selector de dimensión (unidad/área/centro/puesto/tipo de puesto/planilla) | Alta |
| 8 | (3) Acciones por estado | Dona o barras apiladas (universo completo — RN-04) | Media |
| 9 | (4) Origen manual vs automático | Dona de 2 segmentos | Media |
| 10 | (11) Liquidaciones por estado | Tarjeta con mini-dona (conteos, sin montos — RN-16) | Media |
| 11 | (10) Cobertura de entrevistas — detalle | Gauge/porcentaje con absolutos (complementa el KPI de la fila 1) | Media |
