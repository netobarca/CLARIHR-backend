# Análisis de Negocio — Competencias del Puesto (consulta en el expediente del empleado)

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo, Gerencia |
| **Módulos afectados** | Expedientes de Personal · Talento (`PersonnelFilePositionCompetencyResult`) · Marco de Competencias (`CompetencyFramework`: `OccupationalPyramidLevel`, `CompetencyConduct`/`CompetencyConductBehavior`, `JobProfileCompetencyExpectation`) · Perfiles de Puesto (`JobProfiles`) · Plazas (`PositionSlots`) · Asignaciones (`EmploymentAssignments`) · Catálogos (`JobCatalogItem`) · Identidad/Permisos (`IdentityAccess`/Provisioning) · Auditoría |
| **Estado** | **Definido / Cerrado (Fase 1).** Decisiones **ratificadas D-01…D-12** (respuestas del negocio del **2026-06-23**). La **consulta de los 6 datos ya está implementada** (`PersonnelFilePositionCompetencyResult`); el trabajo de Fase 1 es **conectar y endurecer**: la **fuente de la verdad es CLARIHR** (no sincronización externa), la pantalla **deriva las competencias esperadas desde la matriz del perfil según el nivel jerárquico**, el resultado se **vincula al marco** (competencia, tipo y conductas estructuradas dejan de ser texto libre), la **escala es configurable por empresa**, la **brecha se calcula**, la **fecha es obligatoria/no futura**, se soporta **historial de notas**, se crea **permiso dedicado de lectura** y **autoservicio** del empleado. **No hay datos productivos → drop & recreate (sin migración).** **Listo para diseño técnico.** |
| **Versión** | v2 (incorpora decisiones del negocio P-01…P-11 → D-01…D-12) |
| **Fecha** | 2026-06-23 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |

---

## Contexto del cambio

En el **expediente del empleado** existe la opción **"Competencias del puesto"**, donde se **consultan** las competencias **de gestión, organizacionales y técnicas** requeridas en el puesto que ocupa el empleado. El requerimiento indica que **las competencias dependerán del nivel jerárquico del puesto asignado** y que la **información mostrada** es: **la competencia, las conductas deseadas, la nota esperada, las notas alcanzadas, la brecha identificada en la evaluación y la fecha de evaluación**.

> **Hallazgo clave (verificado en código).** La **consulta de los 6 datos ya está implementada** (`PersonnelFilePositionCompetencyResult` — `Domain/PersonnelFiles/PersonnelFileEmployee.cs:1468`), pero el análisis reveló que CLARIHR tiene **dos subsistemas distintos y no conectados**: (1) el **Marco de Competencias** (definición catalogada: `OccupationalPyramidLevel` = nivel jerárquico, catálogos de competencia/tipo/conducta vía `JobCatalogItem`, `CompetencyConduct` = conducta deseada, y la **matriz por puesto** `JobProfileCompetencyExpectation` que liga **perfil + nivel jerárquico + competencia**), y (2) el **resultado por empleado** (una **instantánea de texto libre** con **cero llaves foráneas** al marco; la competencia se identifica por `CompetencyCode` de texto libre). Por eso la premisa "*las competencias dependerán del nivel jerárquico del puesto asignado*" **no se aplicaba en tiempo de ejecución**.
>
> **Resolución ratificada (2026-06-23).** El negocio decidió que **la fuente de la verdad es CLARIHR** (D-01) y que la pantalla **debe derivar** las competencias esperadas **desde la matriz del perfil según el nivel jerárquico** (D-02). En consecuencia, el resultado por empleado se **vincula al marco** (la competencia, su **tipo** y sus **conductas deseadas** se toman del marco, no como texto libre — D-03, D-10, D-12), la **escala** de calificación es **configurable por empresa** (D-04), la **brecha se calcula** `esperada − alcanzada` (D-05), la **fecha de evaluación es obligatoria y no futura** (D-06), se soporta **historial de varias notas** por competencia (D-07), se crea un **permiso dedicado de lectura** (D-08) y **autoservicio** del empleado (D-09). **No hay datos productivos**: se aplica **drop & recreate** (D-11). Los campos `Source*` (`SourceSystem`/`SourceReference`/`SourceSyncedUtc`) dejan de ser el mecanismo de entrada (origen externo) y quedan como **metadatos opcionales de procedencia**.

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) | Resolución (to-be) |
|---|---|---|---|
| 1 | **Entidad de resultado (la pantalla)** | `PersonnelFilePositionCompetencyResult` (`PersonnelFileEmployee.cs:1468`). Campos: `CompetencyCode` (:1502), `DesiredBehaviors` (:1504), `ExpectedScore` (:1506), `AchievedScore` (:1508), `GapScore` (:1510), `EvaluationDateUtc` (:1512), `SourceSystem/Reference/SyncedUtc` (:1514-1518), `ConcurrencyToken`. | **Cobertura de campos = 100 %** ✅; se **reestructura** para vincular al marco e historial. |
| 2 | **Cobertura de los 6 datos mostrados** | competencia → `CompetencyCode`; conductas → `DesiredBehaviors`; nota esperada → `ExpectedScore`; nota alcanzada → `AchievedScore`; brecha → `GapScore`; fecha → `EvaluationDateUtc`. Todos expuestos por los GET. | **Alineado** ✅ — se conserva la visualización. |
| 3 | **Competencia (identificación)** | `CompetencyCode`: **texto libre**, `NotEmpty().MaximumLength(80)` (`Talent/PositionCompetencyResults.cs:89`). Sin FK ni validación. | **Vincular al marco** (RF-011, D-12). |
| 4 | **Tipo de competencia (gestión/organizacional/técnica)** | **No existe** en el resultado. El marco tiene la categoría `CompetencyType` (`JobProfileEnums.cs:20`) **sin seed**. | **Agregar tipo + seed SV** (RF-003, D-03). |
| 5 | **Conductas deseadas** | `DesiredBehaviors`: **texto libre** (`varchar(2000)`). En el marco están **estructuradas** (`CompetencyConduct` + `CompetencyConductBehavior`). | **Vincular a conductas estructuradas** (RF-004, D-10). |
| 6 | **Nota esperada / alcanzada** | `ExpectedScore`/`AchievedScore`: `numeric(18,2)` **opcionales**, **sin escala**. En el marco lo esperado es un **nivel de conducta catalogado** + `ExpectedEvidence`. | **Escala configurable**; **esperada desde la matriz** (RF-002, RF-005, D-02, D-04). |
| 7 | **Brecha** | `GapScore`: `numeric(18,2)` **almacenada tal cual**; **no se calcula**. | **Calcular** `esperada − alcanzada` (RF-006, D-05). |
| 8 | **Fecha de evaluación** | `EvaluationDateUtc`: **`DateTime?` opcional** (:1512). (Contraste: `PerformanceEvaluation.EvaluationDateUtc` **obligatoria** :1409.) | **Obligatoria y no futura** (RF-007, D-06). |
| 9 | **Historial (notas plural)** | Una `AchievedScore` por fila; índice `(TenantId, PersonnelFileId, CompetencyCode)` **no único**; lista ordena por `CompetencyCode`, no por fecha. | **Historial por ciclo** (varias notas + fecha) (RF-008, D-07). |
| 10 | **Dependencia del nivel jerárquico** | `JobProfile` **no** referencia `OccupationalPyramidLevel` (verificado). El nivel vive solo en cada fila de `JobProfileCompetencyExpectation` (`OccupationalPyramidLevelId:66`, junto a `JobProfileId:64`). Vínculo empleado→plaza = **`Guid?` suelto** (`PositionSlotPublicId`, sin FK). | **Construir el puente** empleado→plaza→perfil→nivel→matriz; **atar el perfil a un nivel** (RF-002, D-02). |
| 11 | **Derivación empleado→esperadas** | **No existe** consulta que una empleado → asignación → plaza → perfil → nivel → expectativas (grep sin coincidencias en `Features/PersonnelFiles`). | **Crear la consulta derivada** (RF-002, D-02). |
| 12 | **Marco (definición)** | `OccupationalPyramidLevel` (`OccupationalPyramidLevel.cs`, `LevelOrder:43`), `CompetencyConduct` (+ `CompetencyConductBehavior`), `JobProfileCompetencyExpectation` (`JobProfileId:64`, `OccupationalPyramidLevelId:66`, competencia/tipo/nivel `:68/:70/:72`, `ExpectedEvidence:74`). Editor matriz vía `JobProfileCompetencyMatrixController`. | **Base sólida ✅** — se reutiliza como fuente de lo esperado. |
| 13 | **Catálogos competencia/tipo/nivel/conducta** | `JobCatalogItem` por `JobCatalogCategory`: `Competency=3`, `CompetencyType=10`, `BehaviorLevel=11`, `Behavior=12` (`JobProfileEnums.cs:14,20,21,22`), **por tenant**, **sin seed**. | **Sembrar SV** (tipos + base de competencias/escala) (RF-003, RF-005, D-03). |
| 14 | **Capa de aplicación (resultado)** | CRUD completo (Add/Update/Patch/Delete/Get/List) en `Talent/PositionCompetencyResults.cs` + `.Handlers.cs`; JSON Patch RFC 6902. **No existe** `*.Rules.cs`. | **Módulo de reglas puro** + reestructura CRUD (RNF). |
| 15 | **Persistencia (resultado)** | Tabla `personnel_file_position_competency_results` (`PersonnelFileEmployeeConfiguration.cs:482`); `competency_code` 80 (:489); `expected/achieved/gap_score numeric(18,2)` (:491-493); `evaluation_date_utc` (:494). Única FK → `personnel_file` (:506). Sin check constraints ni FK al marco. | **FK al marco + constraints** (RF-002/006/007); **drop & recreate** (D-11). |
| 16 | **API (resultado)** | 6 endpoints REST bajo `/api/v1/personnel-files/{publicId}/position-competency-results[/{id}]` (`PersonnelFileTalentController.cs`). | Se conservan; se amplían (tipo, escala, historial, esperadas). |
| 17 | **Permisos (resultado)** | `[AuthorizationPolicySet(Read, Manage)]` **genérico**. Existen dedicados `ViewCompensation:98`, `ManageSubstitutions:106`, `ViewInsurance:113` (`PersonnelFileCommon.cs`). | **Permiso dedicado de lectura** (RF-009, D-08). |
| 18 | **Autoservicio** | **No** hay (lecturas de talento usan `LoadForReadAsync` sin rama `isSelf`). Compensación **sí** lo tiene (`PersonnelFileCommon.cs:94-96`). | **Autoservicio del empleado** (RF-010, D-09). |
| 19 | **Permisos (marco)** | Dedicados `CompetencyFramework.Read`/`.Admin` + módulo comercial `COMPETENCY_FRAMEWORK` (gate por `CompetencyFrameworkAuthorizationService`). | Alineado ✅. |
| 20 | **Estado/concurrencia/auditoría** | Solo expedientes **completados** (`IsCompletedEmployee`); `ConcurrencyToken` + If-Match; auditoría por operación. | Se conserva ✅ (auditoría ideal con diff). |
| 21 | **Pruebas** | ~11 unitarias (`CompetencyFrameworkDomainTests`, `PersonnelFilePositionCompetencyResultPatchTests`, etc.). **Sin** pruebas de integración. | Reglas + **integración** del flujo derivado (RNF). |
| 22 | **Migraciones** | Tablas en base consolidada `20260409021844_InitialCurrentState`; luego `20260527033140_AddConcurrencyTokenToPersonnelFileTalentEntities`. | Migración de esquema para FK/constraints/escala/historial. |

---

## Decisiones del negocio (ratificadas — 2026-06-23)

| # | Pregunta | Decisión |
|---|---|---|
| **D-01** | Fuente de la verdad | **CLARIHR.** Los resultados se **capturan en CLARIHR**, no se sincronizan desde un sistema externo. Los campos `Source*` quedan como **metadatos opcionales de procedencia** (no son el mecanismo de entrada). |
| **D-02** | Dependencia del nivel | **Derivar desde la matriz del perfil.** La pantalla obtiene las competencias **esperadas** recorriendo **empleado → plaza/puesto asignado → perfil → nivel jerárquico → `JobProfileCompetencyExpectation`**, y las combina con las notas alcanzadas del empleado. |
| **D-03** | Tipo de competencia | **Confirmado**: tres categorías **gestión / organizacional / técnica** como catálogo (`CompetencyType`). **Seed inicial con datos de El Salvador (SV).** |
| **D-04** | Escala de calificación | **Configurable por la empresa.** Cada empresa define su escala (p. ej. **1–5, 0–100, 0–10, A–F**, etc.). La escala es **ordenada/comparable** para poder calcular la brecha; esperada y alcanzada usan la **misma** escala. |
| **D-05** | Brecha | **Se calcula** por el sistema: **`brecha = nota esperada − nota alcanzada`** (sobre el valor numérico/ordinal de la escala). |
| **D-06** | Fecha de evaluación | **Obligatoria**; **no se permiten fechas futuras**. |
| **D-07** | Historial | **Se requiere historial**: **varias notas alcanzadas** por competencia a lo largo del tiempo (cada una con su fecha), conservando la **vigente/última**. |
| **D-08** | Permiso | **Se crea** un **permiso dedicado de lectura** de competencias (datos sensibles de evaluación), patrón `ViewCompensation`/`ViewInsurance` (Admin como superset). |
| **D-09** | Autoservicio (ESS) | **Sí.** El empleado puede ver **sus propias** competencias y brechas, **como en compensación** (rama `isSelf` en los handlers de lectura). |
| **D-10** | Conductas deseadas | **Vinculadas** a las **conductas estructuradas** del marco (`CompetencyConduct`/`CompetencyConductBehavior`), no texto libre. |
| **D-11** | Datos existentes | **No migrar.** Se **borra/normaliza** lo necesario (drop & recreate); no hay datos productivos a preservar. |
| **D-12** | Identificación de la competencia *(consecuencia de D-01/D-02)* | El resultado **se vincula al marco**: `CompetencyCode` de **texto libre se reemplaza** por una **referencia** a la competencia/expectativa del marco. |

---

## Brechas verificadas y su resolución (GAP → Resolución)

> **Leyenda:** 🔴 desalineación con el requerimiento escrito · 🟡 endurecimiento HRIS · 🟢 enriquecimiento ratificado.

| # | Sev. | Brecha (as-is) | Resolución (to-be) |
|---|---|---|---|
| **G-01** | 🔴 | La premisa "las competencias dependen del **nivel jerárquico del puesto asignado**" **no se aplica**. | **Construir el puente** empleado→plaza→perfil→nivel→matriz y **atar el perfil a un nivel** (D-02, RF-002). |
| **G-02** | 🔴 | El resultado **no** distingue **gestión/organizacional/técnica**. | **Tipo de competencia** desde catálogo + **seed SV** (D-03, RF-003). |
| **G-03** | 🔴 | Competencia y conductas como **texto libre**, desconectadas del marco. | **Vincular** competencia y **conductas estructuradas** al marco (D-10, D-12, RF-004, RF-011). |
| **G-04** | 🟡 | `ExpectedScore`/`AchievedScore` **sin escala**. | **Escala configurable** por empresa, ordenada/comparable (D-04, RF-005). |
| **G-05** | 🟡 | `GapScore` **almacenada**, no calculada. | **Calcular** `esperada − alcanzada` (D-05, RF-006). |
| **G-06** | 🟡 | `EvaluationDateUtc` **opcional**. | **Obligatoria y no futura** (D-06, RF-007). |
| **G-07** | 🟢 | Una sola nota; sin historial/serie. | **Historial por ciclo** (varias notas + fecha) (D-07, RF-008). |
| **G-08** | 🟡 | Lectura por `PersonnelFiles.Read` genérico. | **Permiso dedicado** de lectura (D-08, RF-009). |
| **G-09** | 🟢 | Sin **autoservicio** del empleado. | **ESS**: el empleado ve sus competencias/brechas (D-09, RF-010). |
| **G-10** | 🟡 | **Sin** módulo de reglas, **sin** validación de escala/fecha/brecha, **sin** pruebas de integración. | **`PositionCompetencyResults.Rules.cs`** + tests (unit + integración) + paridad ES/EN (RNF). |
| **G-11** | 🟢 | Catálogos `Competency`/`CompetencyType`/`BehaviorLevel`/`Behavior` **sin seed**. | **Sembrar SV** (tipos, escala/niveles y base de competencias) (D-03, RF-003/005). |

---

## 1. Resumen del producto o requerimiento

Opción **"Competencias del puesto"** dentro del expediente del empleado para **consultar** las competencias **de gestión, organizacionales y técnicas** requeridas en su puesto, **según el nivel jerárquico** de la plaza asignada, mostrando por cada competencia: **conductas deseadas, nota esperada, notas alcanzadas, brecha y fecha de evaluación**.

La **visualización ya existe** (`PersonnelFilePositionCompetencyResult` cubre los 6 datos). Esta **Fase 1** la **conecta y endurece** según las decisiones ratificadas: **CLARIHR es la fuente** (D-01); la pantalla **deriva lo esperado de la matriz del perfil por nivel jerárquico** (D-02) y lo **combina** con las notas alcanzadas; el resultado se **vincula al marco** (competencia, **tipo** y **conductas estructuradas** — D-03/D-10/D-12); la **escala es configurable** (D-04); la **brecha se calcula** (D-05); la **fecha es obligatoria/no futura** (D-06); hay **historial de notas** (D-07); se crea **permiso dedicado** (D-08) y **autoservicio** (D-09); y se aplica **drop & recreate** sin migración (D-11).

---

## 2. Objetivos del negocio

- **O-1. Consulta confiable de competencias del puesto** (esperada vs. alcanzada y brecha), por empleado.
- **O-2. Coherencia con el puesto y el nivel jerárquico:** lo mostrado corresponde al **nivel** de la plaza asignada, derivado de la **matriz del perfil** (premisa del requerimiento).
- **O-3. Clasificación estándar:** distinguir **gestión, organizacionales y técnicas**.
- **O-4. Integridad de la evaluación:** **escala** definida, **brecha calculada**, **fecha** registrada y válida.
- **O-5. Seguimiento en el tiempo:** **historial** de notas para medir evolución y cierre de brechas.
- **O-6. Transparencia y control:** **autoservicio** del empleado y **permiso dedicado**; operaciones **auditadas** y **concurrencia-seguras**.

---

## 3. Alcance funcional (Fase 1 — todo lo ratificado D-01…D-12)

- **F1.** **Validación** de la consulta existente de competencias por empleado (CRUD/lectura) — **ya implementado**.
- **F2.** **Puente puesto→nivel→competencias esperadas**: consulta derivada desde la **matriz del perfil** según el **nivel jerárquico** de la plaza asignada (D-02).
- **F3.** **Vínculo al marco**: competencia y **tipo** (gestión/organizacional/técnica) desde catálogo; **conductas deseadas** estructuradas (D-03, D-10, D-12) + **seed SV**.
- **F4.** **Escala de calificación configurable** por empresa (ordenada/comparable) (D-04).
- **F5.** **Brecha calculada** `esperada − alcanzada` (D-05).
- **F6.** **Fecha** obligatoria y **no futura** (D-06).
- **F7.** **Historial** de notas por ciclo (varias notas con su fecha; última vigente) (D-07).
- **F8.** **Permiso dedicado de lectura** de competencias (D-08).
- **F9.** **Autoservicio** del empleado para sus propias competencias/brechas (D-09).
- **F10.** **Módulo de reglas puro** + pruebas (unit + integración) + **paridad de localización ES/EN** (RNF).
- **F11.** **Drop & recreate** del esquema afectado (sin migración de datos) (D-11).

---

## 4. Fuera de alcance

- **FA-1.** **Sincronización desde un sistema externo de evaluación** (D-01: la fuente es CLARIHR). Los campos `Source*` quedan como metadatos opcionales; no se construye integración externa.
- **FA-2.** **Motor de evaluación de desempeño** (flujos, evaluadores, ponderaciones, ciclos formales). La evaluación general por evento ya existe como entidad **separada** (`PersonnelFilePerformanceEvaluation`).
- **FA-3.** **Edición de la matriz de competencias por puesto** como feature nueva: el **editor de la matriz ya existe** (`JobProfileCompetencyMatrixController`); aquí se **consume** para derivar lo esperado (se ajusta solo lo necesario para atar perfil↔nivel y la escala).
- **FA-4.** **Planes de desarrollo / capacitación** derivados de la brecha (consumo posterior).
- **FA-5.** **Acoplamiento con planilla / compensación** (la competencia no afecta ingresos/egresos).
- **FA-6.** **Catálogos configurables por país**: el marco es **por tenant**; el seed SV es la base inicial, no un cambio de alcance a país.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Analista / Gestor de RRHH** | Registra/edita las notas de competencias del empleado y consulta resultados (CLARIHR es la fuente — D-01). |
| **Jefe / Supervisor del puesto** | Consulta competencias y brechas de su equipo (según permisos). |
| **Empleado titular** | Sujeto de la evaluación; **consume por autoservicio** sus propias competencias/brechas (D-09). |
| **Administrador del Marco de Competencias** | Define niveles jerárquicos, catálogos (competencia/tipo/escala/conducta), conductas y la **matriz por puesto**; ata el perfil a su nivel. |
| **Auditor / Consulta** | Lee competencias e historial bajo permiso dedicado (D-08). |

---

## 6. Requerimientos funcionales

### RF-001 — Consultar las competencias del puesto del empleado *(ya implementado — validación)*
**Descripción:** Mostrar, por expediente **completado**, las competencias del puesto con **competencia, conductas deseadas, nota esperada, nota alcanzada, brecha y fecha**.
**Reglas de negocio:** Solo expedientes **completados** (`IsCompletedEmployee`); lectura **auditada** y **concurrencia-segura**.
**Criterios de aceptación:** Dado un expediente completado, cuando consulto, entonces obtengo la lista con los 6 datos *(ya cubierto)*; expediente no completado → **422** `STATE_RULE_VIOLATION` *(ya cubierto)*.
**Prioridad:** Alta · **Dependencias:** ninguna (existe).

### RF-002 — Derivar competencias esperadas desde el nivel jerárquico del puesto *(🔴 G-01 — D-02)*
**Descripción:** La consulta **deriva** las competencias **esperadas** recorriendo **empleado → asignación vigente → plaza (`PositionSlot`) → perfil (`JobProfile`) → nivel jerárquico (`OccupationalPyramidLevel`) → `JobProfileCompetencyExpectation`**, y las **combina** con las notas alcanzadas del empleado.
**Reglas de negocio:**
- Para derivar **por nivel**, el **perfil debe estar atado a un nivel jerárquico** (hoy `JobProfile` **no** tiene esa referencia → se agrega), de modo que las expectativas aplicables sean las del **nivel del perfil asignado**.
- El vínculo empleado→plaza (`PositionSlotPublicId`) se usa para resolver el perfil; si hay **multi-plaza**, se considera la **asignación vigente/principal**.
**Criterios de aceptación:**
- Dado un empleado con plaza asignada, cuando consulto, entonces veo las competencias **esperadas** de su **nivel jerárquico**, combinadas con sus notas alcanzadas.
- Dado un empleado sin plaza/nivel resoluble, entonces estado claro "sin competencias esperadas" (sin error 500).
**Prioridad:** Alta · **Dependencias:** `EmploymentAssignments` + `JobProfiles` (atar a nivel) + `CompetencyFramework`.

### RF-003 — Competencia y tipo desde catálogo (gestión/organizacional/técnica) + seed SV *(🔴 G-02, G-11 — D-03)*
**Descripción:** Identificar la competencia por **catálogo** (`JobCatalogItem`/`Competency`) e incorporar el **tipo** (`CompetencyType`: gestión/organizacional/técnica) al resultado; **sembrar** el catálogo con **datos de El Salvador**.
**Reglas de negocio:** competencia y tipo deben existir y estar **activos** en el tenant. **Seed SV:** `GESTION`, `ORGANIZACIONAL`, `TECNICA` (y base inicial de competencias por tipo).
**Criterios de aceptación:** competencia fuera de catálogo → **422** `COMPETENCY_CODE_INVALID`; tipo fuera de catálogo → **422** `COMPETENCY_TYPE_INVALID`.
**Prioridad:** Alta · **Dependencias:** `JobCatalogItem` + seed.

### RF-004 — Conductas deseadas vinculadas a las conductas estructuradas del marco *(🔴 G-03 — D-10)*
**Descripción:** Las **conductas deseadas** se **toman del marco** (`CompetencyConduct`/`CompetencyConductBehavior`) según la competencia/nivel, en lugar del texto libre `DesiredBehaviors`.
**Reglas de negocio:** las conductas mostradas pertenecen a la competencia y al nivel correspondiente de la matriz.
**Criterios de aceptación:** Dado un resultado, cuando lo consulto, entonces veo las **conductas estructuradas** del marco (no texto libre).
**Prioridad:** Alta · **Dependencias:** RF-002, RF-011.

### RF-005 — Escala de calificación configurable por empresa *(🟡 G-04 — D-04)*
**Descripción:** Soportar una **escala configurable por empresa** (p. ej. 1–5, 0–100, 0–10, A–F), **ordenada/comparable**; `ExpectedScore` y `AchievedScore` se expresan en la **misma** escala.
**Reglas de negocio:**
- La escala define su rango/valores ordenados; las notas deben ser **válidas dentro de la escala**.
- Para escalas no numéricas (A–F) cada nivel tiene un **valor ordinal** que habilita el cálculo de brecha.
**Criterios de aceptación:** nota fuera de escala → **422** `COMPETENCY_SCORE_OUT_OF_RANGE`.
**Prioridad:** Alta · **Dependencias:** configuración de escala (ver §16, punto de diseño); módulo de reglas.

### RF-006 — Brecha calculada *(🔴 G-05 — D-05)*
**Descripción:** El sistema **calcula** `brecha = nota esperada − nota alcanzada` (sobre el valor numérico/ordinal de la escala).
**Reglas de negocio:** la brecha **no** se captura manualmente; se deriva de esperada/alcanzada.
**Criterios de aceptación:** Dadas esperada y alcanzada, cuando guardo, entonces `brecha = esperada − alcanzada` y se muestra coherente.
**Prioridad:** Alta · **Dependencias:** RF-005; módulo de reglas.

### RF-007 — Fecha de evaluación obligatoria y no futura *(🟡 G-06 — D-06)*
**Descripción:** Exigir `EvaluationDateUtc` y validar que **no sea futura**.
**Criterios de aceptación:** fecha ausente o futura → **422** `COMPETENCY_EVALUATION_DATE_INVALID`.
**Prioridad:** Alta · **Dependencias:** módulo de reglas.

### RF-008 — Historial de notas por ciclo *(🟢 G-07 — D-07)*
**Descripción:** Soportar **varias notas alcanzadas** por competencia a lo largo del tiempo (cada una con su fecha), exponiendo la **serie** y la **última vigente**.
**Reglas de negocio:** cada evaluación es una entrada con su nota y fecha; la **vigente** es la más reciente; la lista por competencia se ordena por **fecha**.
**Criterios de aceptación:** Dado un empleado con varias evaluaciones de una competencia, entonces veo la **línea de tiempo** (nota + fecha + brecha) y la **vigente**.
**Prioridad:** Media · **Dependencias:** modelo de historial; RF-006.

### RF-009 — Permiso dedicado de lectura *(🟡 G-08 — D-08)*
**Descripción:** Permiso dedicado (p. ej. `PersonnelFiles.ViewCompetency`) para leer competencias; escritura por `Manage`; patrón `ViewCompensation`/`ViewInsurance` (Admin superset).
**Criterios de aceptación:** usuario sin el permiso (y no titular) → **403 FORBIDDEN** al consultar.
**Prioridad:** Alta · **Dependencias:** Provisioning/`IdentityAccess` (alta + seed + política).

### RF-010 — Autoservicio del empleado *(🟢 G-09 — D-09)*
**Descripción:** El **empleado titular** consulta **sus propias** competencias/brechas (rama `isSelf` en los handlers de lectura, como en compensación).
**Criterios de aceptación:** Dado el titular, cuando consulta su propio expediente, entonces ve sus competencias sin requerir permiso de RRHH; **no** puede ver las de otros.
**Prioridad:** Media · **Dependencias:** handlers de lectura; RF-009.

### RF-011 — Vincular el resultado al marco (reemplazar `CompetencyCode` texto libre) *(🔴 G-03 — D-12)*
**Descripción:** Sustituir `CompetencyCode` de texto libre por una **referencia** a la competencia/expectativa del marco, de modo que la evaluación se registre **contra una competencia esperada** del puesto/nivel.
**Reglas de negocio:** una nota alcanzada se asocia a una competencia **existente** del marco aplicable al puesto/nivel del empleado.
**Criterios de aceptación:** intentar registrar una nota para una competencia no vinculable → **422** `COMPETENCY_NOT_IN_PROFILE` (o equivalente).
**Prioridad:** Alta · **Dependencias:** RF-002, RF-003.

---

## 7. Requerimientos no funcionales

- **Seguridad / Multi-tenant.** Operaciones por **tenant** (ya implementado). **Permiso dedicado** (RF-009) y **autoservicio** controlado por titularidad (RF-010).
- **Integridad / Consistencia.** Escala (RF-005), brecha (RF-006), fecha (RF-007), catálogos y vínculo al marco (RF-003/004/011) en un **módulo de reglas puro** `PositionCompetencyResults.Rules.cs`, **unit-testeable**.
- **Concurrencia.** `ConcurrencyToken` + If-Match/ETag (ya implementado).
- **Auditoría.** Operaciones auditadas; idealmente con **diff** (antes/después).
- **Rendimiento.** La derivación empleado→perfil→nivel→matriz (RF-002) debe evitar **N+1**; aprovechar índices por tenant.
- **Usabilidad.** Agrupar por **tipo** (gestión/organizacional/técnica); mostrar **esperada vs. alcanzada** y **brecha** con semáforo; **escala** consistente; **historial** navegable.
- **Compatibilidad / API.** Mantener los 6 endpoints; ampliar contratos (tipo, escala, esperadas derivadas, historial). El paso a catálogo/FK es *breaking* solo para datos inválidos (no hay datos productivos — D-11).
- **Localización.** Mensajes/errores **bilingües (ES/EN)** con **código estable**; test de **paridad de localización**.
- **Mantenibilidad / Pruebas.** Extender `PersonnelFilePositionCompetencyResultPatchTests` y **agregar pruebas de integración** del flujo derivado (hoy inexistentes).

---

## 8. Historias de usuario

### HU-001 — Consultar competencias del puesto
Como **Analista de RRHH**, quiero **ver las competencias del puesto del empleado con su evaluación**, para **identificar fortalezas y brechas**.
- Dado un expediente completado, cuando abro "Competencias del puesto", entonces veo competencia, conductas deseadas, nota esperada, nota alcanzada, brecha y fecha.

### HU-002 — Ver competencias según el nivel jerárquico (derivadas de la matriz)
Como **Jefe del puesto**, quiero que **las competencias mostradas correspondan al nivel jerárquico de la plaza** (desde la matriz del perfil), para **evaluar contra lo realmente esperado**.
- Dado un empleado con plaza asignada, cuando consulto, entonces las **esperadas** son las de su **nivel** (RF-002).

### HU-003 — Clasificar por tipo de competencia
Como **Analista de RRHH**, quiero **distinguir gestión, organizacionales y técnicas**, para **analizar por categoría** (RF-003).

### HU-004 — Datos de evaluación coherentes (escala, brecha, fecha)
Como **Analista de RRHH**, quiero que **escala, brecha y fecha sean válidas**, para **confiar en la información**.
- Dada esperada y alcanzada, entonces la **brecha se calcula** (RF-006); nota fuera de escala o fecha futura/ausente **bloquea** (RF-005/007).

### HU-005 — Seguimiento en el tiempo
Como **Jefe / RRHH**, quiero **ver el historial de notas** por competencia, para **medir la evolución y el cierre de brechas** (RF-008).

### HU-006 — Autoservicio del empleado
Como **Empleado**, quiero **ver mis propias competencias y brechas**, para **conocer mi desarrollo**.
- Dado el titular, entonces ve **sus** competencias por autoservicio; **no** las de otros (RF-010).

### HU-007 — Acceso controlado
Como **Auditor / RRHH**, quiero **acceso restringido a los datos de competencias**, para **proteger información sensible**.
- Usuario sin permiso (y no titular) → **403** (RF-009).

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** consultan/gestionan competencias (ya implementado).
- **RN-02.** Las **esperadas** corresponden al **nivel jerárquico** de la plaza/perfil asignado, **derivadas de la matriz** (D-02, RF-002).
- **RN-03.** **Competencia**, **tipo** (gestión/organizacional/técnica) y **conductas** provienen del **marco/catálogo** activo (D-03/D-10/D-12, RF-003/004/011).
- **RN-04.** `ExpectedScore`/`AchievedScore` válidos dentro de la **escala configurable** de la empresa (D-04, RF-005).
- **RN-05.** **`brecha = esperada − alcanzada`**, calculada por el sistema (D-05, RF-006).
- **RN-06.** `EvaluationDateUtc` **obligatoria** y **no futura** (D-06, RF-007).
- **RN-07.** Se conserva **historial** de notas por competencia; la **vigente** es la más reciente (D-07, RF-008).
- **RN-08.** **Lectura** con permiso dedicado o por **titularidad** (autoservicio); **escritura** con `Manage` (D-08/D-09, RF-009/010).
- **RN-09.** Toda escritura exige **If-Match** y queda **auditada**; multitenancy aplicada (ya implementado).
- **RN-10.** La competencia **no** afecta ingresos/egresos (sin acoplar a planilla).
- **RN-11.** **No** hay migración de datos: se aplica **drop & recreate** (D-11).

---

## 10. Flujos principales

**Flujo: Consultar competencias del puesto (RRHH/Jefe/Empleado)**
1. El usuario (con permiso o titular) abre "Competencias del puesto" del expediente (**completado**).
2. El sistema **resuelve la plaza/perfil asignado** y su **nivel jerárquico** (RF-002).
3. Deriva de la **matriz** las competencias **esperadas** del nivel (competencia, **tipo**, **conductas estructuradas** y **nivel/valor esperado**).
4. Las **combina** con las **notas alcanzadas** del empleado y **calcula la brecha** (RF-006).
5. Muestra, **agrupado por tipo**, cada competencia con conductas, **esperada**, **alcanzada (vigente)**, **brecha**, **fecha** e **historial** (RF-008).

**Flujo: Registrar/actualizar una nota de competencia (RRHH — CLARIHR es la fuente)**
1. Sobre una competencia **esperada** del puesto (de la matriz), RRHH ingresa la **nota alcanzada** (en escala — RF-005) y la **fecha** (RF-007).
2. El sistema **calcula la brecha** (RF-006), crea una **entrada de historial** (RF-008), persiste, **audita** y devuelve `ETag`.

**Flujo: Autoservicio (Empleado)**
1. El empleado titular consulta su propio expediente → ve sus competencias/brechas e historial (RF-010), sin acceso a terceros.

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Expediente **no completado**. | **Bloqueo** `422 STATE_RULE_VIOLATION` (ya implementado). |
| **E2** | Empleado **sin plaza/nivel** resoluble. | "Sin competencias esperadas" (estado claro, sin 500) (RF-002). |
| **E3** | Perfil **no atado a un nivel** jerárquico. | Señalar configuración pendiente; no derivar esperadas hasta atar el nivel (RF-002). |
| **E4** | **Competencia/tipo** fuera de catálogo. | **Bloqueo** `422 COMPETENCY_CODE_INVALID` / `COMPETENCY_TYPE_INVALID` (RF-003). |
| **E5** | Nota para competencia **no aplicable** al puesto/nivel. | **Bloqueo** `422 COMPETENCY_NOT_IN_PROFILE` (RF-011). |
| **E6** | **Nota** fuera de escala. | **Bloqueo** `422 COMPETENCY_SCORE_OUT_OF_RANGE` (RF-005). |
| **E7** | **Fecha** ausente o **futura**. | **Bloqueo** `422 COMPETENCY_EVALUATION_DATE_INVALID` (RF-007). |
| **E8** | `If-Match`/`ConcurrencyToken` no coincide. | **409 CONFLICT** (ya implementado). |
| **E9** | Usuario sin permiso y **no** titular. | **403 FORBIDDEN** (RF-009/010). |

---

## 12. Datos requeridos

### Entidad: `PersonnelFilePositionCompetencyResult` *(existe — `PersonnelFileEmployee.cs:1468`; se reestructura)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `personnelFileId` | long (FK) | Sí | del tenant; cascada | ✅ existe | Empleado dueño |
| `competencyRef` | FK → **marco** | Sí | competencia/expectativa del marco aplicable | 🔧 vincular (RF-011, D-12) | **Competencia** (reemplaza `competencyCode` texto libre) |
| `competencyTypeCode` | Texto → **catálogo** | Sí | `CompetencyType` (gestión/org/técnica) | 🆕 nuevo (RF-003) | **Tipo de competencia** |
| `desiredBehaviorsRef` | Vínculo → **conductas** | Sí | `CompetencyConduct`/`Behavior` del marco | 🔧 vincular (RF-004, D-10) | **Conductas deseadas** (estructuradas) |
| `expectedScore` | Escala (config.) | Sí | dentro de la **escala**; **desde la matriz** | 🔧 derivar/validar (RF-002/005) | **Nota esperada** |
| `achievedScore` | Escala (config.) | Sí* | dentro de la **escala** | 🔧 validar (RF-005) | **Nota alcanzada** (*por evaluación/historial) |
| `gapScore` | Escala (calc.) | Auto | **= esperada − alcanzada** | 🔧 calcular (RF-006) | **Brecha** (no editable) |
| `evaluationDateUtc` | Fecha | Sí | **no futura** | 🔧 obligatoria (RF-007) | **Fecha de evaluación** |
| `sourceSystem/Reference/SyncedUtc` | Texto/Fecha | No | — | ✅ existe (opcional) | Procedencia (metadato; D-01) |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

> **Historial (RF-008):** las **notas alcanzadas** se modelan como **varias entradas** (nota + fecha + brecha) por competencia; la **vigente** es la más reciente. *(Modelado exacto — colección de evaluaciones por competencia vs. filas múltiples — se define en el plan técnico.)*

### Marco de Competencias *(existe — fuente de lo esperado)*

| Entidad | Archivo | Rol |
|---|---|---|
| `OccupationalPyramidLevel` | `OccupationalPyramidLevel.cs` (`LevelOrder:43`) | **Nivel jerárquico** (por tenant). |
| `JobProfile` | `Domain/JobProfiles/JobProfile.cs` | **Perfil del puesto.** ⚠️ **Sin** FK a nivel hoy → **se ata a un `OccupationalPyramidLevel`** (RF-002). |
| `JobProfileCompetencyExpectation` | `JobProfileCompetencyExpectation.cs` (`JobProfileId:64`, `OccupationalPyramidLevelId:66`, competencia/tipo/nivel `:68/:70/:72`, `ExpectedEvidence:74`) | **Matriz por puesto/nivel** (lo esperado). ⚠️ Hoy lo esperado es un **nivel cualitativo**; se alinea con la **escala** (ver §16). |
| `CompetencyConduct` (+ `CompetencyConductBehavior`) | `CompetencyConduct.cs` | **Conductas deseadas** estructuradas. |

### Catálogos *(vía `JobCatalogItem` — `JobProfileEnums.cs`)*

| Categoría (enum) | Valor | Alcance | Seed SV |
|---|---|---|---|
| `CompetencyType` | 10 | Tenant | `GESTION`, `ORGANIZACIONAL`, `TECNICA` |
| `Competency` | 3 | Tenant | base inicial SV por tipo |
| `BehaviorLevel` | 11 | Tenant | niveles de la **escala** (ordenados/valor) |
| `Behavior` | 12 | Tenant | conductas base |

---

## 13. Integraciones necesarias

- **Marco de Competencias (`CompetencyFramework`).** Fuente de lo **esperado** (RF-002) y de **conductas** (RF-004); base de competencia/tipo/escala.
- **Perfiles de Puesto (`JobProfiles`) + Plazas (`PositionSlots`) + Asignaciones (`EmploymentAssignments`).** Resolver **empleado → plaza → perfil → nivel** (RF-002); **atar el perfil a un nivel**.
- **Catálogos `JobCatalogItem`.** Competencia/tipo/escala/conducta + **seed SV** (RF-003/005).
- **Identidad/Provisioning (`IdentityAccess`).** Alta + seed del **permiso dedicado** (RF-009) y rama de **autoservicio** (RF-010).
- **Auditoría (`IAuditService`).** Operaciones auditadas (ideal con diff).

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **RRHH / Administrador de Expedientes** | Crear/editar/eliminar notas (**`PersonnelFiles.Manage`**) y leer (**permiso dedicado**). | Solo expedientes **completados**; solo su **tenant**. |
| **Jefe / Supervisor** | Leer competencias/brechas e historial (**permiso dedicado**). | Según alcance organizacional. |
| **Empleado titular** | Leer **sus propias** competencias/brechas (autoservicio). | Solo su expediente (D-09). |
| **Auditor / Consulta** | Leer competencias e historial (**permiso dedicado**). | Sin escritura. |
| **Administrador del Marco** | Definir niveles, catálogos, escala, conductas, matriz; **atar perfil↔nivel** (**`CompetencyFramework.Admin`**). | Módulo `COMPETENCY_FRAMEWORK` habilitado. |

> **Permiso nuevo (D-08):** lectura dedicada (p. ej. `PersonnelFiles.ViewCompetency`), patrón `ViewCompensation`/`ViewInsurance` (Admin superset). La **escritura** se mantiene en `PersonnelFiles.Manage`.

---

## 15. Criterios de aceptación generales

- ✅ Los **6 datos** se consultan por empleado (**ya cumplido**).
- ◻️ Las esperadas **se derivan de la matriz** según el **nivel jerárquico** del puesto asignado (RF-002).
- ◻️ Competencia, **tipo** y **conductas** vinculadas al **marco/catálogo** + **seed SV** (RF-003/004/011).
- ◻️ Notas dentro de la **escala configurable**; **brecha calculada**; **fecha obligatoria/no futura** (RF-005/006/007).
- ◻️ **Historial** de notas por competencia (RF-008).
- ◻️ **Lectura** por permiso dedicado o **titularidad** (autoservicio); escritura por `Manage` (RF-009/010).
- ◻️ Reglas en **módulo puro** con **tests (unit + integración)** y **paridad ES/EN** (RNF).
- ◻️ **Drop & recreate** aplicado; sin migración de datos (D-11).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1. Reconciliación "esperado del marco" ↔ "escala".** Hoy el marco expresa lo esperado como **nivel cualitativo** (`BehaviorLevelCatalogItemId`) + `ExpectedEvidence`, **sin número**; la escala ratificada puede ser **numérica (0–100)** o **discreta (A–F)**. *(Punto de diseño:)* definir la **escala como niveles ordenados con valor** (reutilizando/extendiendo `BehaviorLevel`) o una **escala numérica** por empresa, y **fijar el valor esperado por competencia** en esa escala dentro de la matriz. Resuelve RF-005/006.
- **R2. Perfil sin nivel jerárquico.** `JobProfile` **no** referencia `OccupationalPyramidLevel` (verificado). Atarlos es **prerrequisito** de la derivación por nivel (RF-002). Mitigación: agregar la FK y validar en la matriz que el nivel de cada expectativa coincida con el del perfil.
- **R3. Vínculo empleado→plaza suelto.** `PositionSlotPublicId` es un **`Guid?` sin FK** usado en validación de escritura; la consulta derivada debe resolverlo de forma robusta (asignación vigente; multi-plaza).
- **R4. Reestructura del resultado (texto→FK).** Cambiar `CompetencyCode`/`DesiredBehaviors` a referencias del marco impacta DTOs/JSON-Patch/repositorio; mitigado por **drop & recreate** (sin datos — D-11) y extensión de tests.
- **R5. Cobertura de pruebas.** No hay pruebas de integración; el flujo derivado debe cubrirse end-to-end.

### Supuestos
- **S1.** **CLARIHR es la fuente** (D-01); no se construye integración externa (los `Source*` son metadatos opcionales).
- **S2.** El **marco de competencias** y su **matriz por puesto** son la definición correcta de "competencias por nivel jerárquico".
- **S3.** **No hay datos productivos** (D-11): **drop & recreate**; sí se requiere **migración de esquema** EF Core.
- **S4.** País de referencia **SV**; seeds y mensajes ES/EN.
- **S5.** La competencia **no** afecta planilla.

### Dependencias
- **D1.** `CompetencyFramework` (niveles, catálogos, escala, conductas, matriz).
- **D2.** `JobProfiles` (atar a nivel) + `PositionSlots` + `EmploymentAssignments` (resolución del puesto asignado).
- **D3.** `JobCatalogItem` + **seed SV**.
- **D4.** `IdentityAccess`/Provisioning (permiso dedicado + autoservicio).
- **D5.** `IAuditService`.

---

## 17. Decisiones resueltas (cierre de preguntas abiertas)

| Pregunta | Decisión | Ref. |
|---|---|---|
| P-01 ¿Fuente de la verdad? | **CLARIHR** (no sincronización externa). | D-01 |
| P-02 ¿Dependencia del nivel? | **Derivar desde la matriz del perfil** por nivel jerárquico. | D-02, RF-002 |
| P-03 ¿Tipo de competencia? | **Sí**, gestión/organizacional/técnica como catálogo; **seed SV**. | D-03, RF-003 |
| P-04 ¿Escala? | **Configurable por empresa** (1–5, 0–100, 0–10, A–F…), ordenada/comparable. | D-04, RF-005 |
| P-05 ¿Brecha? | **Calculada** `esperada − alcanzada`. | D-05, RF-006 |
| P-06 ¿Fecha? | **Obligatoria**; **no futura**. | D-06, RF-007 |
| P-07 ¿Historial? | **Sí**, varias notas por competencia (con fecha); última vigente. | D-07, RF-008 |
| P-08 ¿Permiso dedicado? | **Sí**, lectura dedicada. | D-08, RF-009 |
| P-09 ¿Autoservicio? | **Sí**, el empleado ve sus propias competencias/brechas (como compensación). | D-09, RF-010 |
| P-10 ¿Conductas deseadas? | **Vinculadas** a las conductas estructuradas del marco. | D-10, RF-004 |
| P-11 ¿Datos existentes? | **No migrar**; borrar/normalizar lo necesario (drop & recreate). | D-11 |

> **Pendiente menor de diseño (no bloqueante, para el plan técnico):** (a) modelado exacto de la **escala** (niveles ordenados con valor vs. rango numérico) y dónde se fija el **valor esperado** por competencia en la matriz (R1); (b) FK **perfil↔nivel** y validación de coherencia con la matriz (R2); (c) modelo de **historial** (colección de evaluaciones vs. filas múltiples); (d) nombre final del permiso (`PersonnelFiles.ViewCompetency`, sujeto a convención de Provisioning); (e) contenido del **seed SV**.

---

## 18. Recomendaciones del Analista de Negocio

1. **Reposicionar como "conexión + endurecimiento", no construcción nueva.** La **visualización ya está** y el **marco/matriz también**; el esfuerzo es **unir ambos lados** (derivar lo esperado del puesto/nivel y registrar lo alcanzado contra esa definición) y **dar integridad** (escala/brecha/fecha/historial/permiso/autoservicio).

2. **Resolver primero los dos prerrequisitos estructurales (R1, R2).** (a) **Atar `JobProfile` a un `OccupationalPyramidLevel`** y validar que la matriz sea coherente con ese nivel; (b) **definir la escala** (niveles ordenados con valor) y **fijar el valor esperado** por competencia en la matriz. Sin esto, RF-002/005/006 no son realizables.

3. **Construir el puente como consulta derivada (RF-002).** Recorrer empleado → asignación vigente → plaza → perfil → nivel → `JobProfileCompetencyExpectation`, combinando con las notas alcanzadas y **calculando la brecha**. Cubrir con **pruebas de integración**.

4. **Vincular el resultado al marco (RF-011/004/003) + seed SV.** Reemplazar `CompetencyCode`/`DesiredBehaviors` de texto libre por referencias; sembrar `CompetencyType` (gestión/organizacional/técnica) y la base SV. Aprovechar **drop & recreate** (D-11).

5. **Integridad y reglas (RF-005/006/007/008).** Escala válida, **brecha calculada**, **fecha obligatoria/no futura** e **historial**, en un **módulo de reglas puro** `PositionCompetencyResults.Rules.cs` con tests y paridad ES/EN.

6. **Acceso (RF-009/010).** Alta del **permiso dedicado** en Provisioning + política; **autoservicio** del titular replicando el patrón de compensación (rama `isSelf`).

7. **Orden de construcción sugerido (todo en Fase 1):** (1) escala + perfil↔nivel + matriz con valor esperado → (2) vínculo resultado↔marco + seed SV → (3) consulta derivada + cálculo de brecha → (4) historial → (5) permiso + autoservicio → (6) reglas/tests/localización. Confirmar los pendientes menores de diseño del §17 antes de cerrar el plan técnico.

---

> **Naturaleza del documento.** Análisis de **validación + brechas (GAP)** sobre funcionalidad **parcialmente implementada**, con **estado as-is verificado contra el código** (referencias `archivo:línea`) y **decisiones del negocio ratificadas** (D-01…D-12, 2026-06-23). La **consulta de los 6 datos ya estaba alineada**; el trabajo de Fase 1 es **conectar** el resultado por empleado con el **marco de competencias por puesto/nivel** (derivación desde la matriz, vínculo de competencia/tipo/conductas), **dar integridad** (escala configurable, brecha calculada, fecha válida, historial) y **controlar el acceso** (permiso dedicado + autoservicio), bajo **drop & recreate** sin migración. El documento queda **cerrado para Fase 1 y listo para diseño técnico**.
