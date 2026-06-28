# Análisis de Negocio — Tablero de Gráficos e Indicadores de RRHH (HR Analytics Dashboard)

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (**validación + brechas**) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo, Gerencia de RRHH, Dirección |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Reportería/Analítica (`Reports`, `PersonnelFileReporting`) · Asignaciones/Plazas (`EmploymentAssignment` / `PositionSlot`) · Estructura organizacional (`OrgUnits`, `WorkCenters`, `JobProfiles`, `PositionCategory`, `OccupationalPyramidLevel`) · Acciones de personal (`PersonnelAction`) · Catálogos (`GeneralCatalogs` / `ReferenceCatalogs`) · Identidad/Permisos (`IdentityAccess`) |
| **Estado** | **Borrador para validación.** Funcionalidad **parcialmente implementada** (existe una base analítica de expedientes); el tablero solicitado **NO está construido** en su mayoría. **Alcance de Fase 1 CERRADO** — decisiones ratificadas **D-01…D-14** (2026-06-27). · **Principio de alcance (confirmado 2026-06-27):** el tablero **solo consume módulos existentes**; los indicadores que dependen de módulos futuros (**Baja de Personal** → **bajas/rotación**) o de relaciones no listas (**nivel de pirámide**) **NO se construyen aquí**: se **referencian** y activan cuando esos módulos existan. |
| **Versión** | v1 |
| **Fecha** | 2026-06-27 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |
| **Naturaleza del módulo** | **Tablero de solo lectura (read-only)**: agregaciones, series temporales y razones (ratios) sobre el padrón de personal, con cruces por dimensiones organizacionales y de puesto. No muta datos. |

---

## Contexto del cambio

Se requiere un **tablero de gráficos e indicadores** que muestre, entre otros:

- **Cantidad de empleados por categorías**
- **Altas y bajas** de empleados
- **Edad** y **antigüedad**
- **Estado civil**
- **Índice de rotación**
- **Colaboradores por jefe**
- **Personal de RRHH por cada 100 empleados**
- **Expedientes actualizados vs. expedientes desactualizados**

El tablero debe permitir **criterios de búsqueda/filtro** por: **año**, **área funcional**, **unidad**, **tipo de puesto**, **nivel de pirámide**, **puesto** y **centro de trabajo**.

El objetivo declarado es **doble**: (1) **validar** si el desarrollo ya implementado está bien alineado **o** si el requerimiento **no existe**, y (2) **analizar y agregar** la información necesaria para este tipo de proceso HRIS. Si no fuera necesario hacer nada, se cerraría. Adicionalmente, se exige: **dejar los catálogos parametrizados**, **reutilizar los servicios accedidos por keys** ya existentes y hacer el **seed inicial de catálogos por país, comenzando con El Salvador (SV)**.

> ### Hallazgo clave (confirmado en código)
> **Existe una base analítica, pero el tablero solicitado está mayormente SIN construir.** El sistema ya expone reportería **read-only sobre el expediente de personal**:
> - `POST /api/v1/companies/{companyId}/personnel-files/dynamic-query` — filtros + agrupación + orden + texto libre + paginación (`PersonnelFileReportingController.cs:31`).
> - `GET /api/v1/companies/{companyId}/personnel-files/analytics/summary` — totales, activos/inactivos, y desglose **por tipo de registro, por rango de edad y por unidad organizacional** (`PersonnelFileReportingController.cs:162`).
> - `GET /api/v1/companies/{companyId}/personnel-files/export` (síncrono) + **job asíncrono** `report-export-jobs` (resource `PERSONNEL_FILES`) para exportaciones grandes.
>
> Esta base cubre **solo 2-3 de los 9 indicadores** pedidos y **1 de las 7 dimensiones de filtro** (unidad). El resto **no existe**. La causa raíz se documenta en el siguiente hallazgo.

> ### Segundo hallazgo clave — la analítica actual es **"expediente-céntrica"**, no une la asignación/puesto
> El motor de consulta (`PersonnelFileDynamicQuerySpec`, `PersonnelFilePatchAndQueryHelpers.cs:33`) opera **solo sobre columnas del propio expediente** (`PersonnelFile`). Sus campos **agrupables** son únicamente: `recordtype`, `maritalstatus`, `nationality`, `orgunitid`, `isactive`. **No une** la asignación activa (`PersonnelFileEmploymentAssignment`) ni, por lo tanto, **puesto, centro de trabajo, área funcional, tipo de puesto ni nivel de pirámide**. El resumen analítico (`GetAnalyticsSummaryAsync`, `PersonnelFileRepository.cs:~1755`) solo desglosa por **tipo de registro, rango de edad (6 buckets fijos) y unidad**. **Conclusión:** para soportar las dimensiones pedidas hay que construir una **capa de analítica de RRHH** que una `PersonnelFile → EmploymentAssignment(activa) → PositionSlot/JobProfile/OrgUnit/WorkCenter` y las series de altas/bajas/rotación.

> ### Tercer hallazgo — el **nivel de pirámide no es un atributo directo del puesto**
> `OccupationalPyramidLevel` (`Domain/CompetencyFramework/OccupationalPyramidLevel.cs`) **no** está enganchado por FK al `JobProfile`. El nivel vive en las **filas de la matriz de competencias** (`JobProfileCompetencyExpectation.OccupationalPyramidLevelId`) — ver memoria de *competencias del puesto* ("level lives on matrix rows; el FK JobProfile↔level fue descartado"). **No hay hoy un "nivel de pirámide canónico" único por puesto.** Filtrar el tablero por nivel de pirámide **requiere primero** decidir y materializar esa relación (1 nivel por puesto). **Riesgo y prerrequisito** — ver R-04 y Q-07.

> ### Cuarto hallazgo — las "bajas" NO son una acción de personal; se registran en el perfil
> Existe un **journal append-only de acciones de personal** (`PersonnelFilePersonnelAction`, catálogos `action-types`/`action-statuses` ya sembrados SV) con tipos: `NOMBRAMIENTO, CONTRATACION, RECONTRATACION, ASCENSO, TRASLADO, CAMBIO_PUESTO, AUMENTO_SALARIAL, AMONESTACION, SUSPENSION, PERMISO, REINTEGRO, OTRO`. **No incluye un tipo "BAJA/RETIRO".** La baja se registra en el **perfil** (`PersonnelFileEmployeeProfile.RetirementDate` + `RetirementCategoryCode`/`RetirementReasonCode`), y el alta tiene su ancla en `HireDate`. **Para "altas y bajas" y "rotación", la fuente confiable es `HireDate` (altas) y `RetirementDate` (bajas)** por periodo, NO el journal de acciones (que es manual, por-expediente y carece de tipo baja).

---

## Principio de alcance — el tablero solo consume módulos existentes

> **Confirmado con el negocio (2026-06-27):** si un **módulo no existe todavía**, **no se construye** como parte de este tablero (es **desarrollo futuro**). El tablero es un **consumidor read-only**: muestra indicadores sobre datos/módulos **ya implementados** y, para lo que aún no existe, **solo deja la referencia** y lo **activa cuando el módulo esté disponible**. Ejemplo guía dado por el negocio: la **Baja de Personal** será un **módulo futuro**; el expediente solo tendrá una **referencia** para ver la baja del empleado. El mismo criterio aplica a cualquier otro requerimiento aún no desarrollado.

Aplicado a los indicadores y dimensiones solicitados:

| Indicador / Dimensión | Depende de (módulo/dato) | ¿Existe hoy? | Tratamiento en el tablero |
|---|---|---|---|
| Headcount **por categorías** | Expedientes + Asignaciones/Plazas | ✅ Sí | **Construir ahora** |
| **Edad** | Expediente (`BirthDate`) | ✅ Sí | **Construir ahora** (rangos parametrizables) |
| **Antigüedad** | Expediente (`HireDate`) | ✅ Sí | **Construir ahora** (rangos parametrizables) |
| **Estado civil** | Expediente + catálogo `marital-statuses` | ✅ Sí | **Construir ahora** |
| **Colaboradores por jefe** | Estructura org / plazas (jefatura) | ✅ Sí | **Construir ahora** (definir jefatura) |
| **Personal de RRHH / 100** | Estructura org + parametrización (no es módulo nuevo) | ✅ Sí | **Construir ahora** (parametrizar marcador RRHH) |
| **Expedientes actualizados / desactualizados** | Expediente + regla configurable | ✅ Sí | **Construir ahora** (definir regla/umbral) |
| **Altas** | Alta/onboarding (`Finalize`/rehire → `HireDate`) | ✅ Sí | **Construir ahora** (opcional) |
| **Bajas** | **Módulo Baja de Personal** | ❌ **Futuro** (solo campos de referencia de retiro) | **Diferir — solo referencia** |
| **Índice de rotación** | **Módulo Baja de Personal** (necesita las bajas) | ❌ **Futuro** | **Diferir** |
| Filtro **nivel de pirámide** | Relación puesto↔nivel (módulo puestos/competencias) | ❌ No lista | **Diferir el filtro** |
| Filtros **año / área funcional / unidad / tipo de puesto / puesto / centro de trabajo** | Estructura org / plazas | ✅ Sí | **Construir ahora** |

**Consecuencia para el alcance:** la **Fase 1** del tablero cubre **solo** los indicadores marcados *"Construir ahora"*. **Bajas, índice de rotación y el filtro por nivel de pirámide quedan diferidos** y se incorporarán **por referencia** cuando existan sus respectivos módulos/relaciones. Esto **reduce alcance y riesgo** y respeta el orden de construcción del producto.

> ### Conclusión de la validación de módulos (verificada en código)
> **El único MÓDULO faltante que bloquea indicadores del tablero es "Baja de Personal"** (bloquea **bajas** e **índice de rotación**). Verificado: no existe ningún flujo/comando de baja/separación/offboarding; la baja solo son **campos de referencia** en el perfil (`PersonnelFileEmployeeProfile.RetirementDate`/`RetirementCategoryCode`/`RetirementReasonCode`) + catálogos de motivos; el catálogo `action-types` **no tiene tipo BAJA**.
>
> **El "nivel de pirámide" NO es un módulo faltante**, sino una **relación de modelo pendiente dentro del módulo de puestos/competencias** (que **sí existe**): `JobProfile` **no tiene FK de nivel** (solo `OrgUnitId`, `ReportsToJobProfileId`, `PositionCategoryId`); el nivel vive **únicamente** en la matriz `JobProfileCompetencyExpectation.OccupationalPyramidLevelId`, donde un puesto puede tener expectativas en **varios** niveles → **no hay un nivel canónico único por puesto**. Habilitar el filtro requiere esa decisión/relación, no un módulo nuevo.
>
> **Todo lo demás está soportado por módulos existentes** (cadenas de FK verificadas): puesto (`assignment.PositionSlotPublicId → PositionSlot.JobProfileId → JobProfile`), tipo de puesto (`JobProfile.PositionCategoryId → PositionCategory`), centro de trabajo (`assignment.WorkCenterPublicId` / `PositionSlot.WorkCenterId`), unidad (`assignment.OrgUnitPublicId → OrgUnit`), área funcional (`OrgUnit.FunctionalAreaCatalogItemId`), jefe (`PositionSlot.DirectDependencyPositionSlotId` + ocupante vía asignación / `OrgUnit.ManagerEmployeeId`), edad (`BirthDate`), antigüedad (`HireDate`), estado civil (`MaritalStatus` + catálogo), altas (`HireDate`/onboarding). Lo **"nuevo"** que hay que construir es la **capa analítica del tablero** + **parametrización** (catálogos de rangos de edad/antigüedad, marcador de RRHH, umbral de "expediente actualizado") — **ninguno de ellos es un módulo** en el sentido de un subsistema funcional faltante.

---

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado en código) | Implicación |
|---|---|---|---|
| 1 | **Resumen analítico** | `analytics/summary`: Total, Activos, Inactivos, **ByRecordType, ByAgeRange (6 buckets fijos: <18, 18-25, 26-35, 36-45, 46-55, 56+), ByOrgUnit** (`PersonnelFileReporting.cs:57`). | Base reutilizable, pero buckets **hardcodeados** y dimensiones limitadas. |
| 2 | **Consulta dinámica** | `dynamic-query` agrupa por `recordtype, maritalstatus, nationality, orgunitid, isactive` (máx. 3); filtra además por `age, birthdate, createdatutc, profession, nombres` (`PersonnelFilePatchAndQueryHelpers.cs:33`). | Cubre **estado civil** y **unidad**; **no** puesto/centro/área/tipo/nivel. |
| 3 | **Exportación** | Síncrona (`/export`, tope de filas → 413) + **asíncrona** `report-export-jobs` (resource `PERSONNEL_FILES`) (`ReportExportResources.cs`). | Reutilizable para "exportar el tablero". |
| 4 | **Edad** | `BirthDate` en el expediente; `Age` calculada en consulta (`CalculateAge`). Buckets fijos en `summary`. | Indicador **edad** disponible; faltan **rangos parametrizables**. |
| 5 | **Antigüedad** | `PersonnelFileEmployeeProfile.HireDate` (ancla de antigüedad; se sobrescribe en recontratación) (`PersonnelFileEmployee.cs:46`). **No** se expone agregación por antigüedad ni rangos. | **Construir** agregación + catálogo de rangos. |
| 6 | **Altas / Bajas** | Alta = `HireDate`; Baja = `RetirementDate` (+ categoría/motivo, `PersonnelFileEmployee.cs:49-55`). **No existe módulo de *Baja de Personal*** (solo campos de referencia); **no** hay serie temporal. | **Diferir bajas/rotación** al futuro módulo *Baja de Personal*; **altas** (dato existente) opcional. |
| 7 | **Estado civil** | Catálogo país-scoped (`marital-statuses`, key) — `MaritalStatus` en el expediente; agrupable. | **Disponible** (✓). |
| 8 | **Rotación** | **No existe** ningún cálculo de rotación (0 coincidencias `turnover`/`rotacion`). | **Diferir** al módulo *Baja de Personal* (la rotación necesita las bajas). |
| 9 | **Colaboradores por jefe** | Jefatura existe en datos: `OrgUnit.ManagerEmployeeId` (`OrgUnit.cs:72`), `PositionSlot.DirectDependencyPositionSlotId`/`FunctionalDependencyPositionSlotId`, `JobProfile.ReportsToJobProfileId`. **No** hay agregación "headcount por jefe". | **Construir**; **definir** cuál jefatura usar (Q-05). |
| 10 | **RRHH por 100 empleados** | **No existe** marcador de "área de RRHH". Existe `OrgUnit.FunctionalAreaCatalogItemId` y `OrgUnit.OrgUnitTypeCatalogItemId` (catálogos). | **Construir**; **parametrizar** qué área(s)/unidad(es) = RRHH (Q-06). |
| 11 | **Expedientes actualizados vs. desactualizados** | **No existe** concepto de "actualizado". Hay `ModifiedAtUtc`/`CreatedAtUtc` (auditoría) y `LifecycleStatus` (Draft/Completed). | **Definir** regla de "actualizado" + umbral parametrizable (Q-08); luego construir. |
| 12 | **Dimensiones de cruce** | Asignación activa lleva `PositionSlotPublicId, OrgUnitPublicId, WorkCenterPublicId, ContractTypeCode` (`PersonnelFileEmployee.cs:99-175`); puesto→`JobProfile.PositionCategoryId` (tipo de puesto), área funcional→`OrgUnit.FunctionalAreaCatalogItemId`. | **Construir** los `JOIN`s; el motor actual no los hace. |
| 13 | **Nivel de pirámide** | `OccupationalPyramidLevel` **sin FK directa** al puesto; vive en la matriz `JobProfileCompetencyExpectation`. | **Prerrequisito**: 1 nivel canónico por puesto (R-04, Q-07). |
| 14 | **Permisos** | Lectura de expedientes/reportería gateada por `PersonnelFiles.Read` (`EnsureCanReadAsync`); patrón de permisos `View*` dedicados por dato sensible. **No** hay permiso de "dashboard/reportes". | **Decidir**: reutilizar `Read` o crear `ViewReports`/`ViewDashboard` (Q-09, P-09). |
| 15 | **Catálogo por key (patrón)** | `GeneralCatalogKeyMap` (wire key → categoría) + `IPositionCatalogLookup.GetActiveCatalogReferenceByCodeAsync` (validar-por-código). Keys ya mapeados incluyen `action-types`, `action-statuses`, `marital-statuses`, etc. | **Reutilizar** para catálogos nuevos (rangos edad/antigüedad). |
| 16 | **Seed por país (patrón)** | `HasData` vía `GlobalCatalogSeedData` (todas las envs) vs `DevSeedService` (solo dev). SV = `CountryCatalogDefinition(-7068L,"SV",…)`. | **Seed SV vía HasData** para catálogos nuevos. |

---

## Decisiones propuestas (P-xx) y preguntas abiertas (Q-xx)

> Estas propuestas requieren **ratificación del negocio** antes del plan técnico. No están cerradas.

| # | Tema | Propuesta del Analista (a ratificar) | Pregunta abierta |
|---|---|---|---|
| **P-01** | Naturaleza del módulo | Tablero **read-only** dedicado de RRHH; endpoints de agregación que **reutilizan** la infra de reportería (auth `Read`, export jobs). | Q-01 |
| **P-02** | Fuente de altas/bajas/rotación | Derivar de `HireDate` (alta) y `RetirementDate` (baja) por periodo; **no** del journal de acciones. | Q-02 |
| **P-03** | Población base ("empleado") | `RecordType = Employee` con **asignación activa** (excluir candidatos y expedientes en Draft); incluir/excluir inactivos según el indicador. | Q-03 |
| **P-04** | "Por categorías" | Headcount desglosable por: **tipo de registro, estado de empleo, tipo de contrato, tipo de puesto (PositionCategory), área funcional, unidad**. | Q-04 |
| **P-05** | Definición de jefe | Usar **una** definición canónica (recomendado: jefatura por **dependencia directa de plaza** `DirectDependencyPositionSlotId`; alterno: `OrgUnit.ManagerEmployeeId`). | Q-05 |
| **P-06** | "Personal de RRHH" | Parametrizar mediante **marca de área funcional / unidad = RRHH** (código reservado `RRHH`), administrable; ratio = (headcount RRHH ÷ headcount total) × 100. | Q-06 |
| **P-07** | Nivel de pirámide | **Prerrequisito**: materializar **1 nivel de pirámide por puesto** (FK en `JobProfile` o derivación determinista desde la matriz). Sin esto, el filtro por nivel queda **fuera de la Fase 1**. | Q-07 |
| **P-08** | "Expediente actualizado" | Parametrizable: `LifecycleStatus = Completed` **y** `ModifiedAtUtc` dentro de un **umbral configurable** (p. ej. 12 meses). Umbral por compañía. | Q-08 |
| **P-09** | Permisos | Crear permiso de lectura **dedicado** `PersonnelFiles.ViewReports` (consistente con el patrón `View*`), que se satisface también con `Read`. | Q-09 |
| **P-10** | Catálogos a parametrizar + seed SV | Nuevos catálogos: **rangos de edad** y **rangos de antigüedad** (país-scoped, seed SV vía HasData, accedidos por key). HR-marker reutiliza `FunctionalArea`. | Q-10, Q-11 |
| **P-11** | Año | "Año" = año de referencia que ancla series (altas/bajas/rotación) y *snapshot* de headcount al cierre del año/periodo. | Q-12 |
| **P-12** | Tabulación/exportación | Reutilizar `report-export-jobs`; añadir resource keys de los nuevos datasets si se exporta cada gráfico. | Q-13 |

---

## Decisiones ratificadas — cierre de alcance Fase 1 (2026-06-27)

> Respuestas del negocio a las preguntas Q-xx (sección 17). **Cierran el alcance de la Fase 1** y **reemplazan** las propuestas P-xx correspondientes.

| # | Tema | Decisión ratificada |
|---|---|---|
| **D-01** | Naturaleza | Tablero **read-only**; se permite **drill-to-expediente** (abrir el expediente desde un gráfico). |
| **D-02** | Altas / Bajas | **Altas SÍ** en Fase 1 (serie desde `HireDate`, opcional). **Bajas y rotación DIFERIDAS** al módulo *Baja de Personal*. |
| **D-03** | Población base | **Solo empleados activos por defecto**, con **toggle** para incluir inactivos. |
| **D-04** | Categorías | Desglose por **tipo de registro, estado de empleo, tipo de contrato, tipo de puesto, área funcional, unidad**; dimensiones resueltas por la **asignación activa**. |
| **D-05** | Definición de jefe | **Dependencia directa de plaza** (`PositionSlot.DirectDependencyPositionSlotId`): el jefe = ocupante de la plaza superior (vía asignación activa). |
| **D-06** | Identificación de RRHH | **Marcar un área funcional = RRHH** (parametrizable por empresa); ratio = (headcount con esa área ÷ headcount total) × 100. |
| **D-07** | Nivel de pirámide | **Diferido** (fuera de Fase 1) hasta que exista la relación canónica puesto↔nivel. |
| **D-08** | Expediente actualizado | `LifecycleStatus = Completed` **Y** `ModifiedAtUtc` dentro de un **umbral configurable** (default **12 meses**), parametrizable por empresa. |
| **D-09** | Permiso | Permiso de lectura **dedicado** `PersonnelFiles.ViewReports` (satisfecho también por `Read`/`Admin`). |
| **D-10** | Rangos edad/antigüedad | **Catálogos país-scoped, seed SV, editables.** Edad: **18-25, 26-35, 36-45, 46-55, 56+** (+ `<18` si aplica). Antigüedad: **<1, 1-3, 3-5, 5-10, 10+ años**. |
| **D-11** | Exportación por indicador | **Fase 2** (no en el MVP). |
| **D-12** | Tablero acotado a jefatura | **No** en Fase 1 (sin data-scoping por jefatura). |
| **D-13** | Indicadores adicionales | **Añadir plazas vacantes vs. ocupadas** (`PositionSlot.MaxEmployees`/`OccupiedEmployees`). Nacionalidad/profesión/género como **desgloses opcionales**. |
| **D-14** | Bajas/rotación (módulo futuro) | Confirmado **diferidas**: se incorporan por referencia cuando exista *Baja de Personal*. |

**Alcance Fase 1 cerrado.** Indicadores: composición por categorías, edad (rangos), antigüedad (rangos), estado civil, **altas**, colaboradores por jefe, RRHH/100, expedientes actualizados/desactualizados, **plazas vacantes/ocupadas**. Filtros: año, área funcional, unidad, tipo de puesto, puesto, centro de trabajo. **Excluido de Fase 1:** bajas, rotación, nivel de pirámide.

---

## Brechas verificadas y su resolución (GAP → Resolución)

| # | Brecha (as-is, verificada) | Resolución (to-be) |
|---|---|---|
| **G-01** | La analítica actual no une la asignación/puesto → sin dimensiones puesto/centro/área/tipo/nivel. | Capa de analítica de RRHH con `JOIN` a la **asignación activa** y estructura (RF-002…RF-004). |
| **G-02** | No hay serie temporal de **altas y bajas**. | **Diferido**: las **bajas** dependen del futuro módulo **Baja de Personal**; se referencian cuando exista. Altas (dato existente) opcional (RF-006). |
| **G-03** | No existe **índice de rotación**. | **Diferido** al módulo **Baja de Personal** (la rotación necesita las bajas) (RF-007). |
| **G-04** | Rangos de **edad** hardcodeados; sin rangos de **antigüedad**. | Catálogos parametrizables de rangos edad/antigüedad + agregaciones (RF-005, RF-008). |
| **G-05** | No hay **colaboradores por jefe**. | Agregación headcount por jefe (RF-009), tras definir jefatura canónica. |
| **G-06** | No hay marcador de **RRHH** ni ratio por 100. | Parametrización HR-area + ratio (RF-010). |
| **G-07** | No hay concepto de **expediente actualizado**. | Regla + umbral configurable + indicador (RF-011). |
| **G-08** | Nivel de pirámide sin relación canónica con el puesto. | **Diferido**: la relación puesto↔nivel pertenece al módulo de puestos/competencias; el filtro por nivel se habilita cuando exista (RF-013). |
| **G-09** | No hay permiso dedicado de reportes/dashboard. | `ViewReports` (RF-012). |
| **G-10** | Filtros del tablero incompletos (solo unidad/edad/estado civil). | Conjunto de filtros unificado año/área/unidad/tipo/nivel/puesto/centro (RF-001). |

---

## 1. Resumen del producto o requerimiento

Un **tablero de indicadores de RRHH (HR analytics dashboard)**, de **solo lectura**, que presenta de forma **gráfica y agregada** la situación de la plantilla: **headcount por categorías**, **altas y bajas**, **distribución por edad y antigüedad**, **estado civil**, **índice de rotación**, **colaboradores por jefe**, **densidad de RRHH (por cada 100 empleados)** y **estado de actualización de expedientes**. Todos los indicadores deben poder **filtrarse** por **año, área funcional, unidad, tipo de puesto, nivel de pirámide, puesto y centro de trabajo**.

La **validación en código** confirma que existe una **base analítica parcial** sobre el expediente (`analytics/summary`, `dynamic-query`, `export`), pero **cubre solo una fracción** de lo solicitado y **no soporta** las dimensiones de puesto/estructura. Por lo tanto, **el grueso del tablero es desarrollo nuevo** que debe **reutilizar** la infraestructura de reportería existente y unir el expediente con la **asignación activa** y la estructura organizacional. Como parte del alcance se deben **parametrizar catálogos** (rangos de edad/antigüedad, marcador de RRHH, umbral de "expediente actualizado"), **accederlos por key** y hacer el **seed inicial para El Salvador (SV)**.

---

## 2. Objetivos del negocio

- **O-1. Visibilidad ejecutiva:** ofrecer a RRHH y dirección una vista única, gráfica y filtrable del estado de la plantilla.
- **O-2. Gestión de la rotación:** medir altas, bajas e **índice de rotación** para anticipar y corregir causas (se complementa con el módulo de *entrevista de retiro*).
- **O-3. Planeación de plantilla:** entender la composición por **edad, antigüedad, categoría, puesto, nivel y área** para sucesión, equidad y planeación.
- **O-4. Eficiencia organizacional:** medir **tramo de control** (colaboradores por jefe) y **densidad de RRHH** (por 100 empleados) como indicadores de estructura.
- **O-5. Calidad del dato:** monitorear **expedientes actualizados vs. desactualizados** para impulsar el mantenimiento del expediente.
- **O-6. Parametrización sin desarrollo:** que los **rangos** (edad/antigüedad), el **marcador de RRHH** y el **umbral de actualización** sean **configurables** por la empresa, con **seed inicial por país**.
- **O-7. Reutilización:** apoyarse en los servicios ya existentes (catálogos por key, reportería, export jobs, permisos) para minimizar costo y riesgo.

---

## 3. Alcance funcional

> **Regla de alcance (ver "Principio de alcance"):** el tablero **solo construye indicadores sobre módulos/datos que ya existen**. Lo que depende de módulos aún no desarrollados (**Baja de Personal** → bajas/rotación) o de relaciones no listas (**nivel de pirámide ↔ puesto**) **NO se construye ahora**; se incorpora **por referencia** cuando esos módulos existan.

**Incluido (Fase 1 — solo sobre módulos existentes):**

1. **Indicadores de composición (snapshot):**
   - Cantidad de empleados **por categorías** (tipo de registro, estado de empleo, tipo de contrato, tipo de puesto, área funcional, unidad).
   - Distribución por **edad** (rangos **parametrizables**).
   - Distribución por **antigüedad** (rangos **parametrizables**).
   - Distribución por **estado civil**.
2. **Indicadores de estructura:**
   - **Colaboradores por jefe** (tramo de control; jefe = **dependencia directa de plaza** — D-05).
   - **Personal de RRHH por cada 100 empleados** (marcador **área funcional = RRHH** — D-06).
   - **Plazas vacantes vs. ocupadas** (`PositionSlot.MaxEmployees`/`OccupiedEmployees` — D-13).
3. **Indicadores de calidad:**
   - **Expedientes actualizados vs. desactualizados** (regla + umbral configurable).
4. **(Opcional) Altas:** serie de **altas** por periodo desde el flujo de alta/onboarding existente (`HireDate`). *(Las **bajas** NO — ver "Diferido".)*
5. **Filtros transversales** sobre todos los indicadores: **año, área funcional, unidad, tipo de puesto, puesto, centro de trabajo** *(sin nivel de pirámide — ver "Diferido")*.
6. **Catálogos parametrizables + seed SV:** rangos de edad, rangos de antigüedad; marcador de área = RRHH; umbral de "expediente actualizado".
7. **Exportación** de los datasets del tablero (reutilizando `report-export-jobs`).
8. **Permiso de lectura dedicado** (`ViewReports`) y trazabilidad/auditoría de acceso.

**Diferido (depende de módulos/relaciones futuros — solo referencia, NO construir ahora):**

- **Bajas** e **índice de rotación** → dependen del módulo **Baja de Personal** (inexistente). El tablero los incorporará **cuando exista** ese módulo; mientras tanto, solo se **referencian** los campos de retiro del perfil. (RF-006, RF-007.)
- **Filtro por nivel de pirámide** → depende de la relación canónica **puesto↔nivel**, que pertenece al **módulo de puestos/competencias**. (RF-013.)

**Diferible (Fase 2 — mejoras, no bloqueadas por otros módulos):**

- Tableros configurables por el usuario (drag & drop), favoritos, *drill-down* avanzado.
- Comparativas multi-periodo, *targets*/metas y alertas.
- Indicadores derivados de nómina, ausentismo, costos o desempeño (dependen de otros módulos).

---

## 4. Fuera de alcance

- **Construir el módulo de "Baja de Personal"** (o cualquier flujo de baja/terminación/offboarding), su analítica, la **serie de bajas** o el **índice de rotación**: es **desarrollo futuro**. El tablero solo **referenciará** los campos de retiro existentes y activará esos indicadores cuando el módulo exista.
- **Construir la relación canónica nivel de pirámide ↔ puesto**: pertenece al módulo de puestos/competencias; el filtro por nivel se habilita cuando esa relación exista.
- **Escritura/edición** de datos desde el tablero (es 100% read-only).
- **Indicadores que dependen de módulos inexistentes o fuera de este requerimiento:** **bajas/rotación (módulo Baja de Personal, futuro)**, ausentismo, horas extra, costos de nómina, capacitación, desempeño/competencias agregadas, clima laboral.
- **Motor de BI genérico** / constructor de reportes arbitrario por el usuario final (más allá de los filtros definidos).
- **Predicciones / analítica avanzada** (forecasting de rotación, ML).
- **Series históricas reconstruidas retroactivamente** más allá de lo que `HireDate`/`RetirementDate`/auditoría permiten derivar (no hay *headcount snapshots* históricos almacenados — ver R-02).
- **Datos sensibles restringidos** (compensación, seguros, reclamos médicos, competencias individuales) salvo decisión expresa; cada uno tiene su propio permiso `View*`.
- **Multi-país simultáneo** en esta fase: se parametriza por país pero el **seed inicial es solo SV**.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el tablero |
|---|---|
| **Analista / Gestor de RRHH** | Consumidor principal: consulta indicadores, aplica filtros, exporta. |
| **Gerente / Director de RRHH** | Lectura ejecutiva; seguimiento de rotación, estructura y calidad del expediente. |
| **Administrador de catálogos (RRHH/TI)** | Parametriza rangos de edad/antigüedad, marcador de RRHH y umbral de actualización. |
| **Dirección / Gerencia general** | Lectura de indicadores agregados (según permiso). |
| **Administrador de seguridad (IAM)** | Asigna el permiso de lectura de reportes/dashboard. |
| **Jefatura de área** (potencial) | Lectura **acotada a su área/equipo** (solo si el negocio lo aprueba — Q-14). |
| **Sistema (backend)** | Calcula agregaciones, deriva altas/bajas/rotación, evalúa la regla de "expediente actualizado". |

---

## 6. Requerimientos funcionales

### RF-001 — Filtros transversales del tablero

**Descripción:** El sistema debe permitir filtrar **todos** los indicadores por: **año**, **área funcional**, **unidad (org unit)**, **tipo de puesto (PositionCategory)**, **puesto (JobProfile)**, **centro de trabajo** y **nivel de pirámide** (este último condicionado a RF-013/P-07). Los filtros se resuelven contra la **asignación activa** del empleado.

**Reglas de negocio:**
- Filtros combinables (AND). Valores provienen de catálogos/estructura existentes (selección por `PublicId`/código).
- "Año" ancla las series temporales y el *snapshot* de headcount al cierre del periodo (P-11).
- El alcance de datos respeta el *tenant* (compañía) y el permiso del usuario.

**Criterios de aceptación:**
- Dado un usuario con permiso de lectura, cuando aplica uno o varios filtros, entonces todos los gráficos reflejan el subconjunto filtrado de forma consistente.
- Dado un filtro por dimensión de puesto/centro/área, entonces la población se restringe vía la asignación activa correspondiente.

**Prioridad:** Alta
**Dependencias:** RF-002 (join de asignación), RF-013 (nivel de pirámide), catálogos/estructura existentes.

---

### RF-002 — Capa de analítica con join a la asignación activa

**Descripción:** El sistema debe resolver, por empleado, su **asignación activa** (`PersonnelFileEmploymentAssignment` con `IsActive = true`, primaria cuando aplique) y, a partir de ella, las dimensiones: **unidad**, **centro de trabajo**, **puesto (slot→JobProfile)**, **tipo de puesto**, **área funcional** y **tipo de contrato**.

**Reglas de negocio:**
- Si un empleado no tiene asignación activa, se clasifica como **"Sin asignar"** (no se descarta silenciosamente).
- La unidad/área del empleado se toma de la asignación; si la asignación no la trae, se hereda del slot/estructura (definir precedencia — Q-04).

**Criterios de aceptación:**
- Dado un empleado con asignación activa, cuando se consulta cualquier indicador dimensional, entonces se agrupa correctamente por la dimensión de su asignación.
- Dado un empleado sin asignación, entonces aparece en un bucket "Sin asignar" y no rompe los totales.

**Prioridad:** Alta
**Dependencias:** Modelo `EmploymentAssignment`, `PositionSlot`, `JobProfile`, `OrgUnit`, `WorkCenter`.

---

### RF-003 — Cantidad de empleados por categorías (composición)

> **Decisión ratificada (D-03/D-04):** población = **solo activos por defecto** + toggle para inactivos; categorías = tipo de registro, estado de empleo, tipo de contrato, tipo de puesto, área funcional, unidad (resueltas por la asignación activa).

**Descripción:** Mostrar headcount **total** y **desglosado** por categoría: tipo de registro, **estado de empleo**, **tipo de contrato**, **tipo de puesto**, **área funcional** y **unidad**.

**Reglas de negocio:**
- Población base: empleados activos (P-03); permitir alternar incluir/excluir inactivos.
- Cada desglose suma al total (los "Sin asignar"/"Sin dato" se muestran explícitamente).

**Criterios de aceptación:**
- Dado el tablero, cuando se selecciona una categoría, entonces se muestra el conteo por cada valor de esa categoría con su total.
- Dado un filtro activo, entonces los conteos se recalculan respetando el filtro.

**Prioridad:** Alta
**Dependencias:** RF-002.

---

### RF-004 — Reutilizar/extender el resumen analítico existente

**Descripción:** Extender (o suceder) `GET …/personnel-files/analytics/summary` para que, además de tipo de registro / edad / unidad, soporte las nuevas dimensiones y filtros, o exponer un nuevo endpoint de dashboard que las contemple.

**Reglas de negocio:**
- Mantener compatibilidad con los consumidores actuales del summary (no romper contrato existente).
- Reutilizar la autorización `EnsureCanReadAsync` (o el nuevo `ViewReports`).

**Criterios de aceptación:**
- Dado el endpoint de dashboard, cuando se consulta con filtros, entonces devuelve los buckets de cada indicador en una sola respuesta agregada (o por indicador, según diseño técnico).

**Prioridad:** Alta
**Dependencias:** RF-001, RF-002, infra de reportería.

---

### RF-005 — Distribución por edad con rangos parametrizables

**Descripción:** Mostrar la distribución de empleados por **rango de edad**, usando un **catálogo configurable** de rangos (no los 6 buckets hardcodeados actuales).

**Reglas de negocio:**
- Los rangos provienen de un catálogo país-scoped (seed SV), administrable.
- Edad calculada desde `BirthDate` a la fecha de referencia (año del filtro o fecha actual).

**Criterios de aceptación:**
- Dado un catálogo de rangos de edad configurado, cuando se consulta el indicador, entonces los empleados se agrupan según esos rangos.
- Dado un cambio en el catálogo, entonces el indicador refleja los nuevos rangos sin desplegar código.

**Prioridad:** Media
**Dependencias:** Catálogo de rangos de edad (RF-014), `BirthDate`.

---

### RF-006 — Altas y bajas por periodo

> **Estado: DIFERIDO (depende de módulo futuro).** Las **bajas** dependen del módulo **Baja de Personal**, que **aún no existe** (hoy solo hay campos de referencia de retiro en el perfil). Este RF **no se construye ahora**; el tablero lo incorporará **por referencia** cuando exista ese módulo. Las **altas** (dato existente vía `HireDate`/onboarding) **sí** pueden mostrarse antes si el negocio lo desea.

**Descripción:** Mostrar la **serie de altas y bajas** por periodo (año, con desglose mensual), derivadas de `HireDate` (altas) y `RetirementDate` (bajas).

**Reglas de negocio:**
- "Alta" = empleados cuyo `HireDate` cae en el periodo; "Baja" = empleados cuyo `RetirementDate` cae en el periodo.
- En recontratación, `HireDate` se sobrescribe (la antigüedad y el alta se reinician) — documentar el efecto en la serie (R-03).
- Las bajas pueden desglosarse por **categoría/motivo de retiro** (catálogos existentes).

**Criterios de aceptación:**
- Dado un año seleccionado, cuando se consulta el indicador, entonces se muestran altas y bajas por mes y el total del año.
- Dado un filtro dimensional, entonces altas/bajas se restringen a la población filtrada.

**Prioridad:** Alta
**Dependencias:** `HireDate`, `RetirementDate`, RF-002 (para filtrar por dimensión).

---

### RF-007 — Índice de rotación

> **Estado: DIFERIDO (depende de módulo futuro).** La rotación necesita las **bajas** del módulo **Baja de Personal** (inexistente). **No se construye ahora**; se habilita cuando exista ese módulo.

**Descripción:** Calcular el **índice de rotación** del periodo: `Rotación (%) = (Bajas del periodo ÷ Headcount promedio del periodo) × 100`, con la fórmula y el periodo **parametrizables**.

**Reglas de negocio:**
- Definir "headcount promedio" (p. ej. promedio de activos al inicio y fin del periodo) y si se separan bajas voluntarias/involuntarias (usando `RetirementCategoryCode`/`SeparationType`).
- La fórmula exacta debe **ratificarse** (Q-12) por ser sensible a interpretación.

**Criterios de aceptación:**
- Dado un periodo, cuando se calcula la rotación, entonces el resultado coincide con la fórmula ratificada y es consistente con altas/bajas (RF-006).
- Dado un filtro dimensional, entonces la rotación se calcula sobre la población filtrada.

**Prioridad:** Alta
**Dependencias:** RF-006, definición de headcount promedio.

---

### RF-008 — Distribución por antigüedad con rangos parametrizables

**Descripción:** Mostrar la distribución por **antigüedad** (tiempo desde `HireDate`) en **rangos configurables** (catálogo).

**Reglas de negocio:**
- Antigüedad = fecha de referencia − `HireDate` (afectada por recontratación, R-03).
- Rangos provienen de un catálogo país-scoped (seed SV).

**Criterios de aceptación:**
- Dado un catálogo de rangos de antigüedad, cuando se consulta, entonces los empleados se agrupan según esos rangos.

**Prioridad:** Media
**Dependencias:** Catálogo de rangos de antigüedad (RF-014), `HireDate`.

---

### RF-009 — Colaboradores por jefe (tramo de control)

> **Decisión ratificada (D-05):** jefe = **dependencia directa de plaza** (`PositionSlot.DirectDependencyPositionSlotId`); el jefe es el **ocupante** de la plaza superior, resuelto vía la asignación activa.

**Descripción:** Mostrar el **número de colaboradores por jefe**, usando la definición canónica de jefatura ratificada (D-05).

**Reglas de negocio:**
- Definición canónica única (recomendado: dependencia directa de plaza). Documentar exclusiones (jefes sin colaboradores, colaboradores sin jefe).
- Identificar al jefe como **empleado** (no solo como plaza vacante).

**Criterios de aceptación:**
- Dado un jefe, cuando se consulta el indicador, entonces se muestra el conteo de sus colaboradores directos.
- Dado un filtro dimensional, entonces el indicador se restringe a la población filtrada.

**Prioridad:** Media
**Dependencias:** Definición de jefatura (Q-05), `PositionSlot.DirectDependencyPositionSlotId` o `OrgUnit.ManagerEmployeeId`.

---

### RF-010 — Personal de RRHH por cada 100 empleados

> **Decisión ratificada (D-06):** "RRHH" se identifica **marcando un área funcional = RRHH** (`OrgUnit.FunctionalAreaCatalogItemId`), parametrizable por empresa.

**Descripción:** Calcular el ratio `(Headcount de RRHH ÷ Headcount total) × 100`, identificando "RRHH" mediante un **marcador parametrizable** (área funcional/unidad).

**Reglas de negocio:**
- "RRHH" se determina por una **marca configurable** (p. ej. `FunctionalArea = RRHH` o lista de unidades), administrable por la empresa.
- El ratio respeta los filtros dimensionales activos (acotando el "total").

**Criterios de aceptación:**
- Dada la parametrización de RRHH, cuando se consulta, entonces se muestra el ratio por cada 100 empleados.
- Dado que no hay parametrización, entonces el indicador informa "no configurado" (no calcula con supuestos ocultos).

**Prioridad:** Media
**Dependencias:** Parametrización HR-area (Q-06), `OrgUnit.FunctionalAreaCatalogItemId`.

---

### RF-011 — Expedientes actualizados vs. desactualizados

> **Decisión ratificada (D-08):** actualizado = `LifecycleStatus = Completed` **Y** `ModifiedAtUtc` dentro de un **umbral configurable** (default **12 meses**), parametrizable por empresa.

**Descripción:** Clasificar y graficar expedientes como **actualizados** o **desactualizados** según una **regla configurable**.

**Reglas de negocio:**
- Regla propuesta (P-08): `LifecycleStatus = Completed` **y** `ModifiedAtUtc ≥ (fecha referencia − umbral)`, con **umbral configurable** por compañía (p. ej. 12 meses).
- Permitir desglose por unidad/área para focalizar el mantenimiento.

**Criterios de aceptación:**
- Dado un umbral configurado, cuando se consulta, entonces cada expediente se clasifica correctamente como actualizado/desactualizado.
- Dado un cambio de umbral, entonces la clasificación se recalcula sin desplegar código.

**Prioridad:** Media
**Dependencias:** Definición de la regla (Q-08), `ModifiedAtUtc`, `LifecycleStatus`.

---

### RF-012 — Permiso de lectura del tablero

**Descripción:** Gatear el acceso al tablero con un permiso de lectura **dedicado** (`PersonnelFiles.ViewReports`), satisfecho también por `Read`/`Admin`.

**Reglas de negocio:**
- Patrón consistente con los permisos `View*` del módulo (controlador/política dedicados).
- El acceso debe auditarse.

**Criterios de aceptación:**
- Dado un usuario sin el permiso, cuando intenta abrir el tablero, entonces recibe 403.
- Dado un usuario con el permiso, entonces accede solo a datos de su tenant y según su alcance.

**Prioridad:** Alta
**Dependencias:** `IdentityAccess`, patrón `AuthorizationPolicySet`/controlador dedicado.

---

### RF-013 — (Prerrequisito) Nivel de pirámide canónico por puesto

> **Estado: DIFERIDO / fuera del tablero.** La relación canónica puesto↔nivel **pertenece al módulo de puestos/competencias**, no se construye dentro del tablero. Mientras no exista, el **filtro por nivel de pirámide no se ofrece**.

**Descripción:** Para habilitar el **filtro por nivel de pirámide**, establecer **un** nivel de pirámide por puesto (FK en `JobProfile` o derivación determinista desde la matriz `JobProfileCompetencyExpectation`).

**Reglas de negocio:**
- Hoy el nivel no es atributo directo del puesto; debe definirse cómo se asigna (manual vs. derivado) y resolver puestos sin nivel.
- Si no se prioriza, el **filtro por nivel queda en Fase 2** y se documenta su ausencia (no se filtra con supuestos).

**Criterios de aceptación:**
- Dado un puesto con nivel asignado, cuando se filtra por nivel, entonces la población se restringe correctamente.
- Dado un puesto sin nivel, entonces aparece como "Sin nivel" (no se descarta silenciosamente).

**Prioridad:** Media (Alta si el filtro por nivel es indispensable en Fase 1)
**Dependencias:** Módulo de competencias/pirámide, decisión Q-07.

---

### RF-014 — Catálogos parametrizables (rangos) + seed SV

**Descripción:** Crear catálogos **país-scoped** y **accedidos por key** para **rangos de edad** y **rangos de antigüedad**, con **seed inicial SV** vía HasData, y exponerlos por `GeneralCatalogKeyMap`.

**Reglas de negocio:**
- Cada rango: código, etiqueta, límites (min/max), orden, activo. Validación por código (`GetActiveCatalogReferenceByCodeAsync`).
- Seed SV con rangos por defecto (ver sección 12 / 18).

**Criterios de aceptación:**
- Dado el catálogo sembrado, cuando el frontend pide la lista por key, entonces recibe los rangos activos del país.
- Dado un rango inactivo, entonces no se ofrece para nuevas agrupaciones pero los datos históricos se conservan.

**Prioridad:** Media
**Dependencias:** `GeneralCatalogKeyMap`, `GlobalCatalogSeedData`, patrón de catálogos.

---

### RF-015 — Exportación del tablero

**Descripción:** Permitir exportar los datasets del tablero (síncrono para conjuntos chicos, **asíncrono** vía `report-export-jobs` para grandes).

**Reglas de negocio:**
- Reutilizar la infra de export jobs; registrar resource keys de los nuevos datasets si se exporta cada indicador.
- Respetar permiso y filtros activos.

**Criterios de aceptación:**
- Dado un indicador con filtros, cuando se exporta, entonces el archivo refleja exactamente lo mostrado.

**Prioridad:** Baja
**Dependencias:** `ReportExportJob`, `ReportExportResources`.

---

### RF-016 — Plazas vacantes vs. ocupadas

**Descripción:** Mostrar la **ocupación de plazas** (ocupadas vs. vacantes) por unidad, centro de trabajo y puesto, a partir de `PositionSlot.MaxEmployees` y `PositionSlot.OccupiedEmployees` (D-13).

**Reglas de negocio:**
- Vacantes = `MaxEmployees − OccupiedEmployees` por plaza (no negativo).
- Respeta los filtros transversales (unidad, centro, puesto, área, tipo de puesto).

**Criterios de aceptación:**
- Dado el indicador, cuando se consulta, entonces se muestran plazas ocupadas y vacantes con su total.
- Dado un filtro, entonces el conteo se restringe a la población filtrada.

**Prioridad:** Media
**Dependencias:** `PositionSlot` (`MaxEmployees`/`OccupiedEmployees`).

---

## 7. Requerimientos no funcionales

- **Seguridad:** multi-tenant estricto; gate por permiso de lectura dedicado; sin exponer datos sensibles (compensación/seguros/salud) salvo permiso específico; auditoría de acceso a reportes.
- **Rendimiento:** las agregaciones deben responder en tiempos interactivos sobre plantillas medianas; usar consultas agregadas en BD (evitar materializar en memoria); aplicar *rate limiting* (ya existe `PersonnelFileRateLimitPolicies.Search`/`Export`).
- **Disponibilidad:** read-only; degradación elegante si un indicador falla (los demás siguen).
- **Escalabilidad:** export pesado vía jobs asíncronos; paginación/topes en datasets grandes.
- **Usabilidad:** gráficos claros, filtros persistentes en la sesión, indicación explícita de buckets "Sin dato"/"Sin asignar".
- **Auditoría:** registrar consultas/exportaciones (patrón `AuditEntityTypes`).
- **Compatibilidad:** API `api/v1`, enums como strings, errores con `extensions.code`, bilingüe ES/EN (ver convenciones de integración).
- **Mantenibilidad:** reglas puras (`*.Rules.cs`) para fórmulas (rotación, "actualizado"); rangos y umbrales en catálogos/configuración, no en código.
- **Accesibilidad:** contraste y descripciones de gráficos; tablas alternativas a los charts (responsabilidad de frontend, pero el backend debe entregar datos tabulables).
- **Configurabilidad:** rangos, marcador de RRHH y umbral de actualización editables por la empresa.

---

## 8. Historias de usuario

### HU-001 — Composición de la plantilla
Como **analista de RRHH**, quiero **ver cuántos empleados hay por categoría (puesto, contrato, área, unidad)**, para **entender la composición de la plantilla**.
**Criterios:** Dado que tengo permiso, cuando abro el tablero y elijo una categoría, entonces veo el conteo por cada valor con su total; cuando aplico un filtro, entonces los conteos se recalculan.

### HU-002 — Altas y bajas
Como **gerente de RRHH**, quiero **ver las altas y bajas por mes en un año**, para **monitorear el movimiento de personal**.
**Criterios:** Dado un año, cuando consulto el indicador, entonces veo altas y bajas por mes; cuando filtro por área, entonces solo veo esa área.

### HU-003 — Índice de rotación
Como **director de RRHH**, quiero **ver el índice de rotación del periodo**, para **priorizar acciones de retención**.
**Criterios:** Dado un periodo, cuando consulto, entonces obtengo el % de rotación calculado con la fórmula ratificada; cuando filtro, entonces se recalcula sobre la población filtrada.

### HU-004 — Edad y antigüedad
Como **analista de RRHH**, quiero **ver la distribución por edad y antigüedad en rangos configurables**, para **planear sucesión y equidad**.
**Criterios:** Dado un catálogo de rangos, cuando consulto, entonces los empleados se agrupan según esos rangos; cuando cambio el catálogo, entonces el gráfico refleja los nuevos rangos.

### HU-005 — Estado civil
Como **analista de RRHH**, quiero **ver la distribución por estado civil**, para **conocer el perfil demográfico**.
**Criterios:** Dado el tablero, cuando consulto, entonces veo el conteo por estado civil (catálogo).

### HU-006 — Colaboradores por jefe
Como **gerente de RRHH**, quiero **ver cuántos colaboradores tiene cada jefe**, para **evaluar el tramo de control**.
**Criterios:** Dado un jefe, cuando consulto, entonces veo su número de colaboradores directos según la definición ratificada.

### HU-007 — Densidad de RRHH
Como **director**, quiero **ver el personal de RRHH por cada 100 empleados**, para **comparar la estructura con benchmarks**.
**Criterios:** Dada la parametrización de RRHH, cuando consulto, entonces veo el ratio; dado que no está parametrizado, entonces el indicador indica "no configurado".

### HU-008 — Calidad de expedientes
Como **analista de RRHH**, quiero **ver cuántos expedientes están actualizados vs. desactualizados**, para **planear su mantenimiento**.
**Criterios:** Dado un umbral configurado, cuando consulto, entonces veo el conteo actualizado/desactualizado y su desglose por unidad.

### HU-009 — Filtros transversales
Como **usuario del tablero**, quiero **filtrar por año, área, unidad, tipo de puesto, puesto y centro de trabajo**, para **enfocar el análisis**.
**Criterios:** Dado cualquier indicador, cuando aplico filtros, entonces todos los gráficos respetan el mismo filtro de forma consistente.

### HU-010 — Parametrización de catálogos
Como **administrador de catálogos**, quiero **configurar los rangos de edad/antigüedad, el marcador de RRHH y el umbral de actualización**, para **adaptar el tablero a mi empresa sin desarrollo**.
**Criterios:** Dado un cambio en la parametrización, cuando se guarda, entonces los indicadores afectados reflejan el cambio sin desplegar código.

---

## 9. Reglas de negocio

- **RN-01.** Población base "empleado" = `RecordType = Employee` con expediente no-Draft; el alcance de inactivos depende del indicador (P-03).
- **RN-02.** Las dimensiones de cruce (puesto, centro, área, tipo, unidad) se resuelven por la **asignación activa**; sin asignación → "Sin asignar".
- **RN-03.** Alta = `HireDate` en el periodo; Baja = `RetirementDate` en el periodo.
- **RN-04.** Recontratación **sobrescribe** `HireDate` (reinicia antigüedad y alta) — afecta series y antigüedad (R-03).
- **RN-05.** Rotación = (Bajas ÷ headcount promedio) × 100, fórmula/periodo ratificados (Q-12).
- **RN-06.** Rangos de edad/antigüedad provienen de **catálogos configurables** (no hardcode).
- **RN-07.** "RRHH" se identifica por **marcador parametrizable** (área/unidad); sin marcador → indicador "no configurado".
- **RN-08.** "Expediente actualizado" = `Completed` **y** `ModifiedAtUtc` dentro del umbral configurable.
- **RN-09.** "Jefe" se determina por **una** definición canónica ratificada (P-05).
- **RN-10.** Todo indicador respeta el **tenant** y los **filtros** activos; los buckets "Sin dato"/"Sin asignar" se muestran explícitamente (no se descartan).
- **RN-11.** El tablero es **read-only**; no muta datos.
- **RN-12.** Datos sensibles (compensación/seguros/salud/competencias individuales) **no** se exponen sin permiso `View*` específico.
- **RN-13.** El nivel de pirámide solo se ofrece como filtro si existe relación canónica puesto↔nivel (RF-013).

---

## 10. Flujos principales

1. El usuario (con permiso `ViewReports`/`Read`) abre el **tablero de RRHH**.
2. El sistema valida permiso y *tenant*, y carga **catálogos de filtros** (año, áreas, unidades, tipos de puesto, puestos, centros, niveles) por key.
3. El usuario **selecciona filtros** (p. ej. año = 2026, área = Operaciones).
4. El sistema ejecuta las **agregaciones** (composición, edad, antigüedad, estado civil, altas/bajas, rotación, jefes, RRHH/100, expedientes) sobre la población filtrada, uniendo la **asignación activa**.
5. El sistema devuelve los **datasets** y el frontend **renderiza los gráficos**.
6. El usuario puede **cambiar filtros** (vuelve al paso 4) o **exportar** un indicador (RF-015).
7. El sistema **audita** la consulta/exportación.

---

## 11. Flujos alternativos y excepciones

- **E-01. Sin permiso:** el sistema responde **403** y no devuelve datos.
- **E-02. Empleado sin asignación activa:** se clasifica en **"Sin asignar"** y se cuenta en los totales.
- **E-03. Dato faltante** (sin estado civil, sin nivel, sin fecha): bucket **"Sin dato"** explícito; no se descarta el registro.
- **E-04. Catálogo de rangos vacío/inactivo:** el indicador de edad/antigüedad informa "rangos no configurados" o cae a un default documentado (definir — Q-10).
- **E-05. RRHH no parametrizado:** el indicador RRHH/100 muestra "no configurado" (no asume áreas).
- **E-06. Nivel de pirámide no canónico (RF-013 no implementado):** el filtro por nivel **no se ofrece**; se documenta la ausencia.
- **E-07. Recontratación dentro del periodo:** el empleado puede aparecer como **alta** del periodo aunque tenga historia previa; se documenta (R-03).
- **E-08. Export grande:** si excede el tope síncrono → **413**; se ofrece el job asíncrono.
- **E-09. División por cero** (headcount promedio = 0): el índice de rotación/ratio devuelve "N/D" en vez de error.

---

## 12. Datos requeridos

> La mayoría de campos **ya existen**; se marcan los **nuevos** (catálogos/parametrización).

### Entidad: PersonnelFile / PersonnelFileEmployeeProfile (existente)
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| RecordType | Enum | Sí | Candidate/Employee | Filtra población base |
| LifecycleStatus | Enum | Sí | Draft/Completed | Insumo de "expediente actualizado" |
| BirthDate | Fecha | Sí | ≤ hoy | Edad |
| MaritalStatus | Texto (cód.) | No | Catálogo `marital-statuses` | Estado civil |
| HireDate | Fecha | Sí | — | Ancla de antigüedad / altas |
| RetirementDate | Fecha | No | ≥ HireDate | Bajas |
| RetirementCategoryCode / RetirementReasonCode | Texto (cód.) | No | Catálogos retiro | Motivo de baja (rotación vol/invol) |
| ModifiedAtUtc / CreatedAtUtc | Fecha | Sí | — | "Expediente actualizado" |
| OrgUnitPublicId | GUID | No | FK OrgUnit | Unidad |

### Entidad: PersonnelFileEmploymentAssignment (existente)
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| IsActive / IsPrimary | Booleano | Sí | — | Selección de asignación vigente |
| PositionSlotPublicId | GUID | No | FK PositionSlot | Puesto/plaza |
| OrgUnitPublicId | GUID | No | FK OrgUnit | Unidad |
| WorkCenterPublicId | GUID | No | FK WorkCenter | Centro de trabajo |
| ContractTypeCode | Texto (cód.) | No | Catálogo `contract-types` | Tipo de contrato |
| StartDate / EndDate | Fecha | Sí/No | — | Vigencia de la asignación |

### Entidad: JobProfile / PositionCategory / OrgUnit / WorkCenter (existentes)
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| JobProfile.PositionCategoryId | FK | No | — | Tipo de puesto |
| JobProfile.Title/Code | Texto | Sí | — | Puesto |
| OrgUnit.FunctionalAreaCatalogItemId | FK | No | Catálogo FunctionalArea | Área funcional |
| OrgUnit.ManagerEmployeeId | GUID | No | — | Jefe (def. alterna) |
| PositionSlot.DirectDependencyPositionSlotId | FK | No | — | Jefe (def. recomendada) |
| WorkCenter.Name/Code | Texto | Sí | — | Centro de trabajo |
| OccupationalPyramidLevel | FK/derivado | No | RF-013 | Nivel de pirámide (prerrequisito) |

### Entidad: AgeRangeCatalogItem (**NUEVA** — país-scoped, seed SV)
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Code | Texto | Sí | único por país | Código del rango |
| Name/Label | Texto | Sí | — | Etiqueta |
| MinAge / MaxAge | Número | Sí/No | min ≥ 0, min ≤ max | Límites del rango |
| SortOrder | Número | Sí | ≥ 0 | Orden de visualización |
| IsActive | Booleano | Sí | — | Disponible para agrupar |

### Entidad: SeniorityRangeCatalogItem (**NUEVA** — país-scoped, seed SV)
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Code | Texto | Sí | único por país | Código del rango |
| Name/Label | Texto | Sí | — | Etiqueta |
| MinMonths / MaxMonths (o años) | Número | Sí/No | min ≥ 0, min ≤ max | Límites de antigüedad |
| SortOrder | Número | Sí | ≥ 0 | Orden |
| IsActive | Booleano | Sí | — | Disponible |

### Parametrización (configuración por compañía — **NUEVA**)
| Parámetro | Tipo | Descripción |
|---|---|---|
| HrAreaMarker | Lista de cód. / flag | Qué área(s)/unidad(es) = RRHH para el ratio RRHH/100 |
| FileUpToDateThresholdMonths | Número | Umbral de "expediente actualizado" |
| TurnoverFormula / Period | Enum/param | Variante de fórmula y periodo de rotación |

---

## 13. Integraciones necesarias

- **Sin integraciones externas nuevas.** Todo es interno al backend CLARIHR.
- **Reutiliza:** servicios de catálogo por key (`GeneralCatalogsController`, `GeneralCatalogKeyMap`, `IPositionCatalogLookup`), reportería/export (`report-export-jobs`, `ReportExportResources`), autorización de expedientes (`IPersonnelFileAuthorizationService`), auditoría.
- **Frontend:** consume los endpoints de dashboard (agregaciones) y los catálogos de filtros; renderiza los gráficos. (Posible guía de integración `docs/technical/guia-integracion-frontend-tablero-indicadores-rrhh.md` en la fase de implementación.)
- **Sin** correo/WhatsApp/pasarela de pago/autenticación externa.

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Analista de RRHH** | Lectura del tablero (`ViewReports`/`Read`); aplicar filtros; exportar | Solo su tenant; sin datos sensibles sin `View*` específico |
| **Gerente/Director de RRHH** | Lectura del tablero; exportar | Solo su tenant |
| **Administrador de catálogos** | Configurar rangos edad/antigüedad, marcador RRHH, umbral de actualización | No modifica datos de empleados |
| **Administrador IAM** | Asigna el permiso `ViewReports` | No accede a datos sin el permiso |
| **Jefatura de área** (opcional) | Lectura acotada a su área/equipo | Solo si el negocio lo aprueba (Q-14); requiere *data scoping* |
| **Dirección general** | Lectura agregada | Según permiso |

---

## 15. Criterios de aceptación generales

- Todos los indicadores solicitados (composición, altas/bajas, edad, antigüedad, estado civil, rotación, colaboradores por jefe, RRHH/100, expedientes actualizados/desactualizados) **se calculan correctamente** y **respetan los filtros** transversales.
- Los filtros **año, área funcional, unidad, tipo de puesto, puesto, centro de trabajo** funcionan de forma **consistente** en todos los gráficos (nivel de pirámide condicionado a RF-013).
- Los **rangos de edad/antigüedad**, el **marcador de RRHH** y el **umbral de actualización** son **configurables** y vienen **sembrados para SV**.
- El tablero es **read-only**, **multi-tenant**, gateado por permiso, y **audita** consultas/exportaciones.
- Se **reutiliza** la infraestructura existente (catálogos por key, reportería, export jobs) y **no se rompe** el contrato del `analytics/summary` ni del `dynamic-query` actuales.
- Mensajes/errores **bilingües** (ES/EN); contrato `api/v1` con enums como strings y `extensions.code`.
- Buckets "Sin dato"/"Sin asignar" **explícitos**; sin descartes silenciosos.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R-01. Costo de los `JOIN`s analíticos:** unir expediente↔asignación↔estructura sobre toda la plantilla puede ser costoso; mitigar con consultas agregadas en BD y/o vistas materializadas/roll-ups.
- **R-02. Ausencia de *snapshots* históricos de headcount:** el sistema guarda **estado actual** (no fotos de plantilla por fecha). El "headcount al cierre del año" y series históricas deben **derivarse** de `HireDate`/`RetirementDate`, lo que es aproximado para periodos pasados.
- **R-03. Recontratación sobrescribe `HireDate`:** distorsiona antigüedad y series de altas históricas (un recontratado "pierde" su alta original). Debe documentarse e idealmente complementarse con el historial de periodos derivado.
- **R-04. Nivel de pirámide sin relación canónica:** filtrar por nivel exige trabajo de modelo previo (RF-013); riesgo de alcance si se asume disponible.
- **R-05. Definición ambigua de "jefe", "rotación" y "actualizado":** múltiples interpretaciones; sin ratificación, riesgo de re-trabajo (Q-05, Q-07/Q-12, Q-08).
- **R-06. Exposición de datos sensibles:** un tablero amplio podría filtrar info restringida si no se respeta el modelo de permisos `View*`.
- **R-07. "Área funcional" no sembrada por país:** `FunctionalAreaCatalogItem` es **company-scoped** (no country-seeded); cada empresa la define. El filtro por área depende de que la empresa la haya configurado.

### Supuestos
- **S-01.** La población objetivo son **empleados activos** salvo que el indicador requiera lo contrario.
- **S-02.** `HireDate`/`RetirementDate` son la fuente de altas/bajas (no el journal de acciones).
- **S-03.** Los catálogos de filtros (unidades, puestos, centros, áreas, tipos) ya están poblados por la empresa.
- **S-04.** El seed inicial es **solo SV**; otros países se parametrizan después.
- **S-05.** El tablero no requiere datos de nómina/ausentismo/desempeño en Fase 1.

### Dependencias
- **D-01.** Infraestructura de reportería (`PersonnelFileReporting`, `report-export-jobs`).
- **D-02.** Modelo de asignación/estructura (`EmploymentAssignment`, `PositionSlot`, `JobProfile`, `OrgUnit`, `WorkCenter`, `PositionCategory`).
- **D-03.** Catálogos por key + seed (`GeneralCatalogKeyMap`, `GlobalCatalogSeedData`).
- **D-04.** Permisos (`IdentityAccess`, `IPersonnelFileAuthorizationService`).
- **D-05.** (Para nivel de pirámide) módulo de competencias/pirámide (RF-013).
- **D-06.** (Para "RRHH/100" y "área funcional") parametrización organizacional de la empresa.

---

## 17. Preguntas abiertas para el cliente o stakeholders

- **Q-01.** ¿El tablero es estrictamente read-only o se espera alguna acción (p. ej. abrir el expediente desde un gráfico)?
- **Q-02.** ¿Confirmamos que **altas/bajas y rotación** se derivan de `HireDate`/`RetirementDate` (y no del journal de acciones de personal)?
- **Q-03.** Por defecto, ¿qué población muestra cada indicador: solo activos, o activos + inactivos? ¿Configurable por indicador?
- **Q-04.** "Cantidad de empleados **por categorías**": ¿qué categorías exactas (tipo de puesto, contrato, estado de empleo, área, unidad, otra)? ¿Precedencia unidad/área (asignación vs. estructura)?
- **Q-05.** **Definición de "jefe"** para "colaboradores por jefe": ¿dependencia directa de plaza, jefe de la unidad (`ManagerEmployeeId`) o reporte de perfil (`ReportsToJobProfileId`)? ¿Solo directos o toda la cadena?
- **Q-06.** **"Personal de RRHH"**: ¿cómo se identifica RRHH (un área funcional, una lista de unidades, un tipo de unidad)? ¿Quién lo mantiene?
- **Q-07.** **Nivel de pirámide**: ¿es indispensable como filtro en Fase 1? Si sí, ¿cómo se asigna **un** nivel por puesto (manual o derivado de la matriz)?
- **Q-08.** **"Expediente actualizado"**: ¿qué define exactamente "actualizado" (finalizado + modificado dentro de X meses, secciones obligatorias completas, revisión periódica)? ¿Umbral por defecto?
- **Q-09.** ¿Permiso de lectura **dedicado** (`ViewReports`) o se reutiliza `PersonnelFiles.Read`?
- **Q-10.** **Rangos de edad/antigüedad** por defecto para SV: ¿qué cortes desea el negocio? ¿Comportamiento si el catálogo está vacío?
- **Q-11.** Los catálogos de rangos, ¿son **país-scoped** (compartidos por país) o **company-scoped** (cada empresa los suyos)?
- **Q-12.** **Fórmula de rotación** y **periodo**: ¿definición de headcount promedio? ¿Se separan rotación voluntaria/involuntaria? ¿Anual/mensual?
- **Q-13.** ¿Se requiere **exportación** por indicador en Fase 1, o solo visualización?
- **Q-14.** ¿La **jefatura de área** debe poder ver el tablero **acotado a su equipo**? (Implica *data scoping* por jefatura.)
- **Q-15.** ¿Hay indicadores adicionales esperados (p. ej. género, nacionalidad, profesión, plazas vacantes vs. ocupadas) que deban incluirse?

---

## 18. Recomendaciones del Analista de Negocio

1. **No cerrar el requerimiento:** la validación confirma que **existe una base** (analytics summary, dynamic query, export) pero **cubre solo ~2-3 de 9 indicadores y 1 de 7 filtros**. El tablero solicitado es, en su mayoría, **desarrollo nuevo** que debe **reutilizar** la infraestructura existente. **Alcance (confirmado con el negocio):** el tablero es un **consumidor read-only de módulos existentes**; **no** se construyen aquí módulos aún no desarrollados. **Bajas** e **índice de rotación** dependen del futuro módulo **Baja de Personal** (hoy solo campos de referencia de retiro en el perfil) y el filtro **nivel de pirámide** depende de la relación puesto↔nivel (módulo de puestos/competencias): ambos se **incorporan por referencia** cuando existan, **no se anticipan**.

2. **Reutilizar agresivamente lo existente (clave del requerimiento):**
   - Autorización y *rate limiting* de reportería (`EnsureCanReadAsync`, políticas de `Search`/`Export`).
   - **Export jobs** (`report-export-jobs`, `ReportExportResources`) para la exportación.
   - **Catálogos por key** (`GeneralCatalogKeyMap`, `IPositionCatalogLookup`) para los catálogos nuevos.
   - **Seed por país** vía `GlobalCatalogSeedData` (HasData) — SV = `-7068`.
   - Evitar duplicar el `analytics/summary`: **extenderlo** o suceder con un endpoint de dashboard que incorpore el **join a la asignación activa** (la pieza que hoy falta).

3. **Construir primero la "capa dimensional" (RF-002):** el mayor *unlock* es unir `PersonnelFile → asignación activa → puesto/centro/área/tipo/unidad`. Con ese join, casi todos los indicadores y filtros quedan habilitados.

4. **Parametrizar lo que el negocio cambia (catálogos):** rangos de **edad** y **antigüedad** como **catálogos sembrados SV** (no hardcode como hoy); **marcador de RRHH** (reutilizar `FunctionalArea`); **umbral de "expediente actualizado"** (config por compañía). Esto cumple el mandato de "dejar catálogos parametrizados + seed SV".

5. **Fasear para reducir riesgo (MVP por fases):**
   - **Fase 1 (MVP, solo módulos existentes):** filtros año/área/unidad/tipo/puesto/centro; indicadores de composición, edad (rangos param.), antigüedad (rangos param.), estado civil, **colaboradores por jefe**, **RRHH/100**, expedientes actualizados; **(opcional) altas**; permiso dedicado; seed SV. **Sin** nivel de pirámide, **sin** bajas/rotación.
   - **Fase 2 (al existir sus módulos/relaciones):** **bajas** e **índice de rotación** (al existir el módulo **Baja de Personal**); **nivel de pirámide** (al existir la relación canónica puesto↔nivel); exportación por indicador, *drill-down* y comparativas multi-periodo.

6. **Ratificar las definiciones sensibles antes de construir:** "jefe", "rotación", "expediente actualizado" y "RRHH" tienen múltiples interpretaciones (Q-05…Q-08, Q-12). Cerrar estas decisiones evita re-trabajo.

7. **Gestionar las limitaciones de datos históricos (R-02/R-03):** comunicar que las series pasadas son **aproximaciones** derivadas de `HireDate`/`RetirementDate` (no hay snapshots históricos), y que la recontratación sobrescribe `HireDate`. Si el negocio exige histórico fiel, evaluar un **mecanismo de snapshots periódicos** (fuera del MVP).

8. **Permiso dedicado recomendado (`ViewReports`):** consistente con el patrón `View*` del módulo, da control fino sobre quién ve la analítica sin abrir todo el expediente.

9. **Próximo entregable sugerido:** una vez ratificadas las preguntas, redactar el **plan técnico** (`docs/technical/plan-tecnico-tablero-indicadores-rrhh.md`) con: endpoints de dashboard, modelo de catálogos de rangos, migración + seed SV (HasData), permiso `ViewReports`, reglas puras de rotación/actualización y la guía de integración frontend.

---

## Consideraciones finales

Este documento es una **validación + análisis de brechas**: confirma qué parte del tablero **ya existe** (base analítica del expediente) y qué parte **debe construirse** (la mayoría: dimensiones de puesto/estructura, altas/bajas, rotación, jefes, RRHH/100, calidad de expedientes y catálogos parametrizables). No se inventaron definiciones de negocio sensibles; las que requieren decisión quedan como **preguntas abiertas (Q-xx)** o **propuestas a ratificar (P-xx)**. Tras la ratificación, el material está listo para el **plan técnico** y el diseño UX/UI, QA y desarrollo.
