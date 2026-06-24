# Análisis de Negocio — Competencias Curriculares del Empleado

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo, Gerencia |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles` → área *Talent/Desarrollo*) · Estructura Organizativa (`PositionDescriptionCatalogs`, `JobProfiles`) · Catálogos · Identidad/Permisos (`IdentityAccess`) |
| **Estado** | **Definido / Cerrado (Fase 1 — endurecimiento).** Decisiones ratificadas **D-01…D-08** (negocio, 2026-06-23). El desarrollo **ya existe** y está alineado en datos/operaciones; esta fase **enlaza catálogos y agrega validaciones**. **Listo para diseño técnico.** |
| **Versión** | v2 (incorpora decisiones del negocio P-01…P-08) |
| **Fecha** | 2026-06-23 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |

---

## Contexto del cambio

En el **expediente del empleado** existe la opción **"Competencias curriculares"**, donde se registran las competencias del empleado **asociadas a los tipos de requisitos** (tomados del **catálogo del módulo de Estructura Organizativa**). Por cada requisito se captura: **tipo de requisito, nombre del requisito, dominio de la competencia, tiempo de experiencia y métrica**.

El objetivo declarado fue **doble**: (1) **validar** que el desarrollo **ya implementado** esté **bien alineado**, y (2) **analizar y agregar** la información/reglas necesarias para que el proceso sea robusto en un HRIS. Tras la **validación inicial (v1)** se confirmó que la funcionalidad **ya existe** y está bien construida; quedaba **una sola brecha de alineación** (el tipo de requisito no se validaba contra el catálogo) y un set de mejoras a decidir. **El negocio ratificó las decisiones (2026-06-23)** y, con ellas, esta versión **define el alcance de Fase 1 (endurecimiento)**.

> **Hallazgo clave (confirmado en código).** Esto **no es un desarrollo desde cero**. CLARIHR **ya tiene implementada** la funcionalidad como entidad `PersonnelFileCurricularCompetency`, con **CRUD completo** (Domain + Application/CQRS + API REST + auditoría + concurrencia + pruebas). Los **cinco campos** del requerimiento están presentes y bien tipados. El trabajo de Fase 1 es de **endurecimiento y alineación**: (a) **validar el tipo de requisito contra el catálogo** `RequirementType` de Estructura Organizativa, (b) **catalogar la métrica** (AÑOS/MESES/DIAS/HORAS), (c) **catalogar el dominio** (configurable por tenant, lista o escala), (d) **impedir duplicados** y (e) validar **tiempo de experiencia ≥ 0**.

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) | Severidad |
|---|---|---|---|
| 1 | **Entidad** | `PersonnelFileCurricularCompetency` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:1654`). Campos: `RequirementTypeCode`, `RequirementName`, `CompetencyDomain`, `ExperienceTimeValue`, `MetricCode`, `Notes?`, `SourceSystem?`, `SourceReference?`, `SourceSyncedUtc?`, `ConcurrencyToken`. Hereda multi-tenant (`TenantEntity`). | ✅ Alineado |
| 2 | **Cobertura de los 5 campos** | `RequirementTypeCode`=*Tipo de requisito*; `RequirementName`=*Nombre del requisito*; `CompetencyDomain`=*Dominio de la competencia*; `ExperienceTimeValue`=*Tiempo de experiencia* (numérico); `MetricCode`=*Métrica*. **Los cinco campos existen.** | ✅ Alineado |
| 3 | **Tipo de requisito ↔ catálogo** | `RequirementTypeCode` es **texto libre**: solo `NotEmpty().MaximumLength(80)` + `Clean()` (`CurricularCompetencies.cs:89`). **Sin FK ni validación** contra el catálogo. El handler `Add` **no** valida el código (`CurricularCompetencies.Handlers.cs:51`). | 🔴 **Brecha → D-01** |
| 4 | **Catálogo de Estructura Organizativa (existe)** | El catálogo **sí existe**: `PositionDescriptionCatalogType.RequirementType = 5` (`Domain/PositionDescriptionCatalogs/PositionDescriptionCatalogEnums.cs:9`), gestionado como `PositionDescriptionCatalogItem` (código, nombre, activo, orden) y expuesto vía `PositionDescriptionCatalogItemsController`. **No está enlazado** a la competencia curricular. | 🔴 (origen de G-01) |
| 5 | **Métrica** | `MetricCode` **texto libre opcional** (`HasMaxLength(80)`, sin catálogo). | 🟡 → D-04 |
| 6 | **Dominio de la competencia** | `CompetencyDomain` **texto libre obligatorio** (`HasMaxLength(120)`). Sin escala ni catálogo. | 🟡 → D-03 |
| 7 | **Tiempo de experiencia** | `ExperienceTimeValue` `decimal? numeric(18,2)` **opcional**, sin validación de rango (p. ej. ≥ 0). | 🟡 → D-06 |
| 8 | **Capa de aplicación** | CQRS completo Add/Update/Patch/Delete/Get/GetList (`CurricularCompetencies.cs` + `.Handlers.cs`). **No existe** `CurricularCompetencies.Rules.cs`. | 🟡 → RNF |
| 9 | **API REST** | 6 endpoints `/api/v{version}/personnel-files/{publicId}/curricular-competencies` (`Api/Controllers/PersonnelFileTalentController.cs:534-702`); contratos en `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs:583`. `GET` lista, `GET` por id, `POST`, `PUT`, `PATCH` (RFC 6902) y `DELETE`. | ✅ Alineado |
| 10 | **Permisos** | Clase con `[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]` (`PersonnelFileTalentController.cs:26`): lecturas → **Read**, escrituras → **Manage** (genérico). Sin permiso dedicado ni autoservicio. | ✅ → D-08 |
| 11 | **Reglas de estado** | Solo expedientes **completados** (`IsCompletedEmployee`, `CurricularCompetencies.Handlers.cs:46`). **Sin** control de duplicados. | ✅ / 🟡 → D-05 |
| 12 | **Concurrencia / Auditoría** | `ConcurrencyToken` + `If-Match`/`ETag`; auditoría por operación vía `PersonnelFileEmployeeAudits.LogUpdateAsync` (`Handlers.cs:73,153,252,320`). Borrado **físico** que devuelve el token del expediente padre. | ✅ Alineado |
| 13 | **Persistencia** | Tabla `personnel_file_curricular_competencies`; PK `id`; **única** FK → `personnel_files` (CASCADE); `uq public_id`; índice **no único** `(tenant_id, personnel_file_id, requirement_type_code)`. **No hay** FK a catálogo alguno (`PersonnelFileEmployeeConfiguration.cs:549`). | ✅ / 🔴 (sin FK a catálogo) |
| 14 | **Pruebas** | `tests/CLARIHR.Application.UnitTests/PersonnelFileCurricularCompetencyPatchTests.cs`. | ✅ Alineado |

> **Leyenda de severidad.** 🔴 brecha de **alineación** con la letra del requerimiento · 🟡 **endurecimiento** (calidad HRIS) · 🟢 **enriquecimiento** futuro · ✅ correcto/alineado.

---

## Decisiones del negocio (ratificadas — 2026-06-23)

| # | Pregunta | Decisión |
|---|---|---|
| **D-01** | ¿El tipo de requisito se valida contra el catálogo? (P-01) | **Sí, obligatorio.** El *tipo de requisito* **debe validarse** contra el catálogo de Estructura Organizativa. **No** se acepta texto libre. (RF-002) |
| **D-02** | ¿Cuál catálogo? (P-02) | **`PositionDescriptionCatalogType.RequirementType`** (catálogo **configurable** del módulo de Estructura Organizativa). **No** el enum fijo `JobRequirementType` de `JobProfileRequirement`. |
| **D-03** | Semántica de "dominio de la competencia" (P-03) | **Catálogo configurable por tenant.** Cada tenant define sus valores y puede usarlo como **lista plana (área temática)** o como **escala ordenada (nivel de pericia)** — el **orden** (`SortOrder`) soporta ambos. El campo se **valida** contra dicho catálogo. (RF-004) |
| **D-04** | ¿Métrica como catálogo? (P-04) | **Sí.** Catálogo de **unidades de métrica** con valores **AÑOS, MESES, DIAS, HORAS** (seed inicial). El campo se **valida** contra el catálogo. (RF-003) |
| **D-05** | ¿Impedir duplicados? (P-06) | **Sí.** No se permiten **duplicados** del **mismo tipo de requisito + nombre del requisito** dentro de un mismo expediente (comparación normalizada/insensible a mayúsculas). (RF-007) |
| **D-06** | ¿Tiempo de experiencia admite 0? (P-08) | **Sí, admite 0.** Validar **`ExperienceTimeValue ≥ 0`**. Sin tope máximo definido. (RF-007) |
| **D-07** | ¿Análisis de brecha vs. perfil de puesto? (P-05/P-07) | **Interesa a futuro** (roadmap). **Fuera de esta fase.** El cruce competencias-empleado ↔ `JobProfileRequirement` se abordará después. Evidencias/verificación: **diferido** (no solicitado). (FA-1) |
| **D-08** | Gestión y permisos (P-09) | **Se mantiene** el modelo actual: gestión **solo RRHH** con `PersonnelFiles.Read`/`Manage` (sin permiso dedicado, sin autoservicio del empleado). No fue objetado. |

---

## Brechas verificadas y su resolución (GAP → Decisión)

| # | Brecha (as-is) | Severidad | Resolución (to-be) |
|---|---|---|---|
| **G-01** | `RequirementTypeCode` **texto libre**, sin enlace al catálogo de Estructura Organizativa. | 🔴 Alineación | **Validación referencial** contra `PositionDescriptionCatalogItem (RequirementType)` por tenant + activo (D-01/D-02, RF-002). |
| **G-02** | `MetricCode` texto libre, sin catálogo. | 🟡 Endurecimiento | Catálogo de **unidades** AÑOS/MESES/DIAS/HORAS (D-04, RF-003). |
| **G-03** | `CompetencyDomain` texto libre, semántica ambigua. | 🟡 Endurecimiento | Catálogo **configurable por tenant** (lista o escala, vía orden) (D-03, RF-004). |
| **G-04** | Sin validación de rango en `ExperienceTimeValue`. | 🟡 Menor | Validar **≥ 0** (D-06, RN-06). |
| **G-05** | Sin control de **duplicados** (mismo tipo + nombre por expediente). | 🟡 Menor | **Bloqueo** de duplicado normalizado (D-05, RN-05). |
| **G-06** | Sin `CurricularCompetencies.Rules.cs` (reglas puras). | 🟡 Mantenibilidad | Crear módulo de reglas **puro** (testeable sin BD) que encapsule RF-002…RF-007 (RNF). |
| **G-07** | Sin cruce con el **perfil del puesto** (`JobProfileRequirement`). | 🟢 Futuro | **Fuera de alcance** de esta fase (D-07, FA-1). |
| **G-08** | Sin **evidencia/adjuntos** ni **estado de verificación**. | 🟢 Futuro | **Diferido** (D-07, FA-2). |

---

## 1. Resumen del producto o requerimiento

Opción del expediente del empleado para **registrar las competencias curriculares** del trabajador, cada una **asociada a un tipo de requisito** del **catálogo de Estructura Organizativa**. Por competencia se captura: **tipo de requisito, nombre del requisito, dominio de la competencia, tiempo de experiencia y métrica**.

La funcionalidad **ya existe** (`PersonnelFileCurricularCompetency`) con **CRUD completo, multi-tenant, concurrencia, auditoría y pruebas**. Esta Fase 1 la **endurece y alinea** conforme a D-01…D-08: **validación del tipo de requisito** contra el catálogo, **catálogo de métrica** (AÑOS/MESES/DIAS/HORAS), **catálogo configurable de dominio** (lista o escala por tenant), **bloqueo de duplicados** y **experiencia ≥ 0**. **Problema que resuelve:** mantener un **inventario estructurado, estandarizado y trazable** de las competencias/requisitos cumplidos por cada empleado — base para selección, desarrollo, evaluación y (futuro) análisis de brecha vs. el perfil del puesto.

---

## 2. Objetivos del negocio

- **O-1.** **Inventariar** de forma estructurada las competencias curriculares de cada empleado (tipo, nombre, dominio, experiencia, métrica).
- **O-2.** **Estandarizar** tipo de requisito, dominio y métrica mediante **catálogos** (consistencia y reportería; sin texto libre en esos tres campos).
- **O-3.** **Integridad y trazabilidad**: datos por tenant, validados contra catálogo, sin duplicados, auditables y con control de concurrencia.
- **O-4.** **Reutilización**: dejar la información lista como **fuente de verdad** para selección, desarrollo, evaluación y el **análisis de brecha** futuro vs. el perfil del puesto.
- **O-5.** **No sobre-construir**: ejecutar solo el endurecimiento ratificado; diferir lo no solicitado (brecha, evidencias).

---

## 3. Alcance funcional (Fase 1)

- **F1.** **Endurecimiento** del registro de competencias curriculares existente (CRUD vía API REST). *(Base ya implementada.)*
- **F2.** **Validación del tipo de requisito** contra el catálogo `RequirementType` de Estructura Organizativa (D-01/D-02, RF-002).
- **F3.** **Catálogo de métrica** (seed AÑOS/MESES/DIAS/HORAS) + validación del campo (D-04, RF-003).
- **F4.** **Catálogo de dominio de competencia** configurable por tenant (lista o escala, con orden) + validación del campo (D-03, RF-004).
- **F5.** **Bloqueo de duplicados** (tipo + nombre) y **validación `experiencia ≥ 0`** (D-05/D-06, RF-007).
- **F6.** **Módulo de reglas puro** `CurricularCompetencies.Rules.cs` + **errores bilingües** (ES/EN) con paridad en `BackendMessages.resx`/`.es.resx` (RNF).

---

## 4. Fuera de alcance

- **FA-1.** **Análisis de brecha** competencia-empleado vs. **requisitos del perfil de puesto** (`JobProfileRequirement`) — **interés futuro** (D-07).
- **FA-2.** **Evidencias/adjuntos** (diplomas, certificados) y **flujo de verificación/aprobación** de competencias (D-07, diferido).
- **FA-3.** **Vigencia/caducidad** de competencias (fechas de obtención/expiración) y recordatorios de recertificación.
- **FA-4.** **Autoservicio** del empleado para auto-registrar/editar sus competencias (gestión solo RRHH — D-08).
- **FA-5.** **Notificaciones**, **importación masiva** o **integración** con sistemas externos de formación/LMS.
- **FA-6.** **Puntuación/ponderación** de competencias o cálculo de "score de idoneidad".

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Analista / Gestor de RRHH** | Crea, edita, consulta y elimina las competencias curriculares del expediente (permiso `Manage`/`Read`). Único rol de gestión (D-08). |
| **Administrador de Estructura Organizativa** | Mantiene el **catálogo de tipos de requisito** (`RequirementType`) y el **catálogo de dominio** (configurable por tenant) que consume esta opción. |
| **Administrador de catálogos** | Mantiene el **catálogo de métrica** (AÑOS/MESES/DIAS/HORAS) y su extensión. |
| **Empleado titular** | Sujeto del registro. **Sin autoservicio** en esta fase (D-08). |
| **Auditor / Cumplimiento** | Consulta la **bitácora** de cambios (auditoría). |
| **Sistema (HRIS)** | Aplica validaciones de catálogo, anti-duplicado, concurrencia (`If-Match`/`ETag`) y auditoría automática. |

---

## 6. Requerimientos funcionales

### RF-001 — Gestionar competencias curriculares del expediente (CRUD) — ✅ **IMPLEMENTADO**

**Descripción:** Registrar, listar, consultar, actualizar (total y parcial) y eliminar competencias curriculares bajo un expediente de empleado.

**Reglas de negocio:** Solo expedientes **completados** (`IsCompletedEmployee`); cada escritura exige `If-Match` con el `concurrencyToken` vigente; auditoría por operación; aislamiento por tenant.

**Criterios de aceptación:**
- Dado un expediente completado, cuando se hace `POST …/curricular-competencies` con datos válidos, entonces se crea y se devuelve `201` con `Location` y `ETag`.
- `GET` (lista y por id), `PUT`, `PATCH` (RFC 6902) y `DELETE` operan con control de concurrencia y errores estándar.

**Prioridad:** Alta · **Dependencias:** Expediente completado. · **Estado:** ✅ Existe (`PersonnelFileTalentController.cs:534-702`).

---

### RF-002 — Tipo de requisito validado contra el catálogo de Estructura Organizativa — 🔧 **FASE 1 (D-01/D-02)**

**Descripción:** El *tipo de requisito* debe **referirse a un ítem activo** del catálogo `PositionDescriptionCatalogItem` con `CatalogType = RequirementType`, **rechazando** códigos inexistentes, inactivos o de otro tenant.

**Reglas de negocio:**
- `RequirementTypeCode` debe **existir** en el catálogo `RequirementType`, estar **activo** y pertenecer al **tenant** del expediente.
- Si no cumple → **`CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID`** (bilingüe, `422`).
- **(Recomendado)** Guardar **snapshot del nombre** del tipo al registrar, para preservar la etiqueta histórica si el catálogo cambia.

**Criterios de aceptación:**
- Dado un código **inexistente/inactivo/otro tenant**, en `POST`/`PUT`/`PATCH` → `422`.
- Dado un código **válido y activo** → la operación se acepta.

**Prioridad:** Alta · **Dependencias:** Catálogo `RequirementType`. · **Estado:** 🔧 Por implementar.

---

### RF-003 — Catálogo de métrica (unidad del tiempo de experiencia) — 🆕 **FASE 1 (D-04)**

**Descripción:** Convertir `MetricCode` en **referencia a catálogo** de unidades con seed **AÑOS, MESES, DIAS, HORAS** (extensible).

**Reglas de negocio:**
- Si se informa, debe existir en el catálogo de métrica (activo, tenant/país según convención). Si vacío → se acepta (campo opcional).
- Si inválido → **`CURRICULAR_COMPETENCY_METRIC_INVALID`** (bilingüe, `422`).
- **Coherencia:** si se informa `ExperienceTimeValue`, se **recomienda** exigir `MetricCode` (para que el número tenga unidad). *(Confirmar en diseño — ver §17.)*

**Criterios de aceptación:** `metricCode` fuera del catálogo → `422`; valor del seed (p. ej. `AÑOS`) → aceptado.

**Prioridad:** Alta · **Dependencias:** Nuevo catálogo de métrica. · **Estado:** 🆕 Por implementar.

---

### RF-004 — Catálogo de dominio de competencia (configurable por tenant) — 🆕 **FASE 1 (D-03)**

**Descripción:** Convertir `CompetencyDomain` en **referencia a un catálogo configurable por tenant**. Cada tenant define sus valores y puede tratarlos como **lista plana (área temática)** o como **escala ordenada (nivel de pericia)**; el **orden** (`SortOrder`) soporta ambas lecturas.

**Reglas de negocio:**
- El valor debe **existir** en el catálogo de dominio del tenant y estar **activo**.
- Si inválido → **`CURRICULAR_COMPETENCY_DOMAIN_INVALID`** (bilingüe, `422`).
- El catálogo expone **orden** para que la UI lo presente como escala cuando el tenant así lo use.

**Criterios de aceptación:** Valor fuera del catálogo del tenant → `422`; valor válido y activo → aceptado; el listado expone el valor y su **orden**.

**Prioridad:** Alta · **Dependencias:** Nuevo catálogo de dominio (tenant-scoped). · **Estado:** 🆕 Por implementar.

---

### RF-005 — Consulta/listado por expediente — ✅ **IMPLEMENTADO**

**Descripción:** Listar todas las competencias de un expediente y consultar una por id, cada una con su `concurrencyToken` (y, tras RF-002/003/004, las **etiquetas** de tipo/dominio/métrica para mostrar).

**Criterios de aceptación:** `GET …/curricular-competencies` devuelve la colección; `GET …/{id}` devuelve detalle o `404`.

**Prioridad:** Alta · **Estado:** ✅ Existe (se enriquecen las respuestas con nombres de catálogo).

---

### RF-006 — Concurrencia optimista y auditoría — ✅ **IMPLEMENTADO**

**Descripción:** Toda escritura usa `If-Match`/`ETag` y registra auditoría (diff por operación).

**Criterios de aceptación:** Token desactualizado → `409 Conflict`; cada operación genera entrada de auditoría.

**Prioridad:** Alta · **Estado:** ✅ Existe.

---

### RF-007 — Anti-duplicado y validación de experiencia — 🔧 **FASE 1 (D-05/D-06)**

**Descripción:** (a) **Impedir duplicados** del **mismo `RequirementTypeCode` + `RequirementName`** (normalizado, insensible a mayúsculas/espacios) dentro de un mismo expediente; (b) validar **`ExperienceTimeValue ≥ 0`** (admite 0).

**Reglas de negocio:**
- Alta/edición que genere duplicado → **`CURRICULAR_COMPETENCY_DUPLICATE`** (`409`/`422`, a definir en diseño).
- Experiencia negativa → **`CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE`** (`422`).

**Criterios de aceptación:** Alta duplicada → rechazo; `experienceTimeValue = -1` → `422`; `0` → aceptado.

**Prioridad:** Media · **Dependencias:** Índice único `(tenant, file, requirementTypeCode, normalizedRequirementName)`. · **Estado:** 🔧 Por implementar.

---

### RF-008 — Análisis de brecha vs. perfil de puesto — 🟢 **FUTURO (D-07, FA-1)**

**Descripción (futuro):** Cruzar las competencias del empleado con `JobProfileRequirement` de su puesto para identificar **requisitos cumplidos/faltantes** y su nivel.

**Prioridad:** N/A (roadmap) · **Dependencias:** `JobProfiles`. · **Estado:** ⬜ Diferido.

---

## 7. Requerimientos no funcionales

- **Seguridad.** Autenticación obligatoria; autorización `PersonnelFiles.Read`/`Manage`; aislamiento **multi-tenant**. *(Cumplido.)*
- **Integridad.** **Integridad referencial** a catálogos (tipo, dominio, métrica) validada en aplicación; **unicidad** anti-duplicado a nivel de BD (índice único). *(Fase 1.)*
- **Concurrencia.** Bloqueo optimista `ConcurrencyToken` + `If-Match`/`ETag`. *(Cumplido.)*
- **Auditoría.** Registro de **diff** por operación. *(Cumplido.)*
- **Rendimiento.** Índices por `(tenant, file)` para listados; índice único para anti-duplicado. *(Cumplido/ampliado.)*
- **Usabilidad.** Mensajes de error **bilingües** (ES/EN) con **paridad** (`BackendMessageLocalizationTests`). *(Fase 1 — nuevos códigos.)*
- **Compatibilidad.** API versionada (`/api/v{version}`), `PATCH` RFC 6902, límites de patch. *(Cumplido.)*
- **Mantenibilidad.** Reglas nuevas en módulo **puro** `CurricularCompetencies.Rules.cs` (testeable sin BD), siguiendo el patrón del repositorio (p. ej. `AssetAccess.Rules.cs`, `Insurances.Rules.cs`). *(Fase 1.)*
- **Escalabilidad.** Modelo listo como **fuente de verdad** para el análisis de brecha futuro.

---

## 8. Historias de usuario

### HU-001 — Registrar una competencia curricular
Como **gestor de RRHH**, quiero **registrar una competencia curricular** del empleado, para **mantener su inventario de requisitos/competencias actualizado**.
**Criterios de aceptación:**
- Dado un expediente completado, cuando ingreso tipo (de catálogo), nombre, dominio (de catálogo) y, opcionalmente, experiencia y métrica (de catálogo), entonces el sistema **guarda** y **lista** la competencia.
- Cuando omito un obligatorio (tipo, nombre o dominio) o uso un código fuera de catálogo, entonces el sistema **rechaza** con mensaje claro.

### HU-002 — Elegir tipo, dominio y métrica desde catálogos
Como **gestor de RRHH**, quiero **seleccionar tipo de requisito, dominio y métrica desde catálogos**, para **estandarizar** la información y evitar errores de tipeo.
**Criterios de aceptación:**
- Solo se aceptan **códigos activos** del catálogo correspondiente; cualquier otro → `422`.
- El **dominio** se muestra como lista o escala según el orden definido por el tenant.

### HU-003 — Evitar duplicados
Como **gestor de RRHH**, quiero que el sistema **impida registrar la misma competencia dos veces** (mismo tipo + nombre), para **mantener la calidad del inventario**.
**Criterios de aceptación:** Alta/edición que genere un duplicado normalizado → rechazo.

### HU-004 — Editar/eliminar con seguridad de concurrencia
Como **gestor de RRHH**, quiero **editar o eliminar** con control de concurrencia, para **no perder cambios** ante ediciones simultáneas.
**Criterios de aceptación:** `concurrencyToken` desactualizado → `409 Conflict`.

### HU-005 — Consultar el historial de cambios
Como **auditor**, quiero **ver la bitácora** de altas/ediciones/bajas, para **cumplimiento y trazabilidad**.
**Criterios de aceptación:** Cada escritura genera una entrada de auditoría con diff.

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo se gestionan competencias en expedientes **completados** (`IsCompletedEmployee`). *(Cumplida.)*
- **RN-02.** **Obligatorios:** tipo de requisito, nombre del requisito y dominio. **Opcionales:** tiempo de experiencia, métrica, notas, datos de origen. *(Cumplida.)*
- **RN-03.** **Tipo de requisito** debe provenir del catálogo `RequirementType` (activo, mismo tenant). *(🔧 Fase 1 — D-01.)*
- **RN-04.** **Dominio** debe provenir del catálogo de dominio del tenant (activo). **Métrica**, si se informa, del catálogo de unidades (AÑOS/MESES/DIAS/HORAS). *(🔧 Fase 1 — D-03/D-04.)*
- **RN-05.** **No** se permiten **duplicados** (mismo tipo + nombre, normalizado) por expediente. *(🔧 Fase 1 — D-05.)*
- **RN-06.** **Tiempo de experiencia ≥ 0** (admite 0). *(🔧 Fase 1 — D-06.)*
- **RN-07.** Toda escritura exige **`If-Match`** con el token vigente; desajuste → `409`. *(Cumplida.)*
- **RN-08.** Toda escritura genera **auditoría** con diff. *(Cumplida.)*
- **RN-09.** Gestión **solo RRHH** (sin autoservicio del empleado). *(Cumplida — D-08.)*

---

## 10. Flujos principales

**Flujo: Registrar competencia curricular**
1. El gestor de RRHH abre el expediente (completado) → sección **Competencias curriculares**.
2. Selecciona **tipo de requisito** (catálogo `RequirementType`), **dominio** (catálogo del tenant) e ingresa **nombre**; opcionalmente **tiempo de experiencia** y **métrica** (catálogo).
3. El sistema **valida**: obligatorios, **existencia/actividad en catálogo** (tipo, dominio, métrica), **anti-duplicado** y **experiencia ≥ 0**.
4. El sistema **guarda** y devuelve `201` con `Location` y `ETag`.
5. La competencia aparece en el **listado** (con etiquetas de catálogo); se registra **auditoría**.

**Flujo: Editar / Eliminar**
1. El gestor obtiene la competencia (con su `concurrencyToken`).
2. Envía `PUT`/`PATCH`/`DELETE` con `If-Match`.
3. El sistema verifica concurrencia, valida (catálogos, anti-duplicado, rango), persiste y **audita**; devuelve nuevo `ETag` (o el token del expediente padre en `DELETE`).

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Campo obligatorio vacío (tipo/nombre/dominio). | `422` con detalle de validación. |
| **E2** | `RequirementTypeCode` inexistente/inactivo/otro tenant. | `422` `CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID`. |
| **E3** | `CompetencyDomain` fuera del catálogo del tenant. | `422` `CURRICULAR_COMPETENCY_DOMAIN_INVALID`. |
| **E4** | `metricCode` fuera del catálogo. | `422` `CURRICULAR_COMPETENCY_METRIC_INVALID`. |
| **E5** | `experienceTimeValue` negativo. | `422` `CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE`. |
| **E6** | Alta/edición duplicada (mismo tipo + nombre). | `409`/`422` `CURRICULAR_COMPETENCY_DUPLICATE`. |
| **E7** | `If-Match` ausente o desactualizado. | `428 Precondition Required` / `409 Conflict`. |
| **E8** | Expediente **no** completado. | Rechazo por regla de estado (`StateRuleViolation`). |
| **E9** | Competencia inexistente en `GET/PUT/PATCH/DELETE`. | `404 Not Found`. |
| **E10** | Patch con operación/ruta no soportada o exceso de operaciones. | `422` (validación de patch endurecida). |

---

## 12. Datos requeridos

### Entidad: PersonnelFileCurricularCompetency (`personnel_file_curricular_competencies`)

| Campo | Tipo de dato | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `PublicId` | GUID | Sí (sistema) | Único | ✅ | Identificador público. |
| `PersonnelFileId` | Long (FK) | Sí (sistema) | FK → `personnel_files` (CASCADE) | ✅ | Expediente propietario. |
| `RequirementTypeCode` | Texto (≤ 80) | **Sí** | `NotEmpty` + **existir en catálogo `RequirementType`** (activo, tenant) | 🔧 | **Tipo de requisito** (catálogo Estructura Organizativa). |
| `RequirementName` | Texto (≤ 200) | **Sí** | `NotEmpty` + **anti-duplicado** con tipo | 🔧 | **Nombre del requisito**. |
| `CompetencyDomain` | Texto (≤ 120) | **Sí** | `NotEmpty` + **existir en catálogo de dominio** (tenant, activo) | 🔧 | **Dominio de la competencia** (lista o escala por tenant). |
| `ExperienceTimeValue` | Decimal (18,2) | No | **≥ 0** (admite 0) | 🔧 | **Tiempo de experiencia** (cantidad; unidad = métrica). |
| `MetricCode` | Texto (≤ 80) | No | **existir en catálogo de métrica** si se informa | 🔧 | **Métrica** (AÑOS/MESES/DIAS/HORAS). |
| `Notes` | Texto (≤ 2000) | No | — | ✅ | Observaciones libres. |
| `SourceSystem` / `SourceReference` / `SourceSyncedUtc` | Texto/Texto/Fecha UTC | No | — | ✅ | Provenance de integración/import. |
| `ConcurrencyToken` | GUID | Sí (sistema) | `If-Match`/`ETag` | ✅ | Control de concurrencia. |
| `CreatedUtc` / `ModifiedUtc` | Fecha/hora (UTC) | Sí (sistema) | — | ✅ | Auditoría temporal. |
| `TenantId` | GUID | Sí (sistema) | Aislamiento multi-tenant | ✅ | Tenant propietario. |

> **Índice anti-duplicado (nuevo):** único `(tenant_id, personnel_file_id, requirement_type_code, normalized_requirement_name)` — sustituye/complementa el índice no único actual.

### Catálogo (existente): PositionDescriptionCatalogItem · `CatalogType = RequirementType`
Catálogo **tenant-scoped** de Estructura Organizativa (`Code`, `Name`, `Description?`, `SortOrder`, `IsActive`). **Fuente** del tipo de requisito (RF-002).

### Catálogo (nuevo): Dominio de competencia — **configurable por tenant**
| Campo | Tipo | Obligatorio | Descripción |
|---|---|---|---|
| `Code` / `Name` | Texto | Sí | Valor del dominio (área o nivel). |
| `SortOrder` | Entero | Sí | **Orden** — habilita la lectura como **escala** (Básico→Experto). |
| `IsActive` | Booleano | Sí | Disponibilidad. |

> Cada tenant decide si lo usa como **lista plana** o **escala ordenada** (D-03). Modelado recomendado: nuevo `PositionDescriptionCatalogType` o catálogo tenant-scoped equivalente (decisión de diseño — §17).

### Catálogo (nuevo): Métrica (unidades) — seed
| Code | Name (ES) |
|---|---|
| `AÑOS` | Años |
| `MESES` | Meses |
| `DIAS` | Días |
| `HORAS` | Horas |

> Seed inicial (extensible). Decisión de diseño: **country-seeded** vs **tenant-configurable** (§17).

---

## 13. Integraciones necesarias

- **Estructura Organizativa (`PositionDescriptionCatalogs`).** Catálogo `RequirementType` (tipo) y catálogo de **dominio** (RF-002/RF-004).
- **Catálogo de métrica.** Unidades AÑOS/MESES/DIAS/HORAS (RF-003).
- **Identidad/Permisos (`IdentityAccess`).** Políticas `PersonnelFiles.Read`/`Manage` (ya integradas).
- **Auditoría.** `IAuditService` / `PersonnelFileEmployeeAudits` (ya integrado).
- **Localización.** `BackendMessages.resx`/`.es.resx` para los nuevos errores bilingües.
- **(Futuro) `JobProfiles`.** Análisis de brecha vs. perfil de puesto (FA-1).
- **Sin** integraciones externas (correo, WhatsApp, pasarelas, LMS) en esta fase.

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Gestor / Analista de RRHH** | `PersonnelFiles.Read` + `PersonnelFiles.Manage`: ver, crear, editar (PUT/PATCH), eliminar competencias. | Solo expedientes **completados** y del **mismo tenant**. |
| **Administrador de Estructura Organizativa** | Gestión de catálogos `RequirementType` y **dominio** (tenant). | No edita expedientes; provee catálogos. |
| **Administrador de catálogos** | Gestión del catálogo de **métrica**. | Alcance según convención (país/tenant). |
| **Empleado titular** | — | **Sin** autoservicio (D-08). |
| **Auditor / Cumplimiento** | Lectura de bitácora de auditoría. | Solo lectura. |

> **Nota.** No se requiere permiso **dedicado** (a diferencia de Sustitución de Autorizaciones): encaja en el `Manage` genérico del área *Talent* (D-08).

---

## 15. Criterios de aceptación generales

- ✅ La opción captura los **cinco campos** del requerimiento.
- ✅ CRUD completo con **multi-tenant, concurrencia (`If-Match`/`ETag`) y auditoría**.
- 🟨 **Tipo de requisito** validado contra el catálogo `RequirementType` (D-01/D-02).
- 🟨 **Dominio** validado contra catálogo configurable por tenant (lista/escala) (D-03).
- 🟨 **Métrica** validada contra catálogo AÑOS/MESES/DIAS/HORAS (D-04).
- 🟨 **Anti-duplicado** (tipo + nombre) y **experiencia ≥ 0** (D-05/D-06).
- 🟨 Errores nuevos **bilingües** con paridad ES/EN; reglas en módulo **puro** testeable.
- ⬜ *(Futuro)* Análisis de brecha vs. perfil de puesto; evidencias/verificación (D-07).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1.** **Migración de datos existentes**: si ya hay competencias con `RequirementTypeCode`/`MetricCode`/`CompetencyDomain` en texto libre que no existen en los catálogos, la validación estricta podría bloquearlas. Mitiga: **backfill/seed** de catálogos a partir de los valores presentes, o ventana de saneamiento previa.
- **R2.** **Definición incompleta de catálogos**: sin poblar `RequirementType`/dominio/métrica por tenant, no se podrá registrar. Mitiga: seed inicial (métrica) y datos mínimos por tenant.
- **R3.** **Doble naturaleza del dominio** (lista vs escala) puede confundir a la UI. Mitiga: exponer `SortOrder` y documentar el patrón.
- **R4.** **Sobre-ingeniería**: ceñirse a lo ratificado (no agregar vigencia/evidencias).

### Supuestos
- **S1.** "El catálogo del módulo de Estructura Organizativa" = `PositionDescriptionCatalogType.RequirementType` (D-02).
- **S2.** El registro lo gestiona **RRHH**, sin autoservicio (D-08).
- **S3.** No hay (esta fase) vigencia, evidencia, verificación ni cruce con perfil del puesto (D-07).
- **S4.** País de referencia **El Salvador (SV)**; los catálogos siguen el patrón country/tenant del repositorio.

### Dependencias
- **D1.** Catálogo `RequirementType` poblado por tenant (RF-002).
- **D2.** Nuevos catálogos de **dominio** (tenant) y **métrica** (seed) creados (RF-003/RF-004).
- **D3.** Paridad de errores bilingües (`BackendMessages.resx`/`.es.resx`).

---

## 17. Preguntas abiertas (de diseño técnico — menores)

> Las decisiones de negocio quedaron **resueltas** (ver §"Decisiones ratificadas"). Restan definiciones **técnicas** menores para el plan de diseño:

1. **Hogar de catálogos.** ¿El catálogo de **dominio** se modela como nuevo `PositionDescriptionCatalogType` (tenant) o catálogo propio? ¿La **métrica** es **country-seeded** (como `PersonnelReferenceCatalog`) o **tenant-configurable**? *(Recomendación: dominio = tenant-configurable; métrica = country-seeded + extensible.)*
2. **Coherencia experiencia↔métrica.** ¿Exigir `MetricCode` cuando se informa `ExperienceTimeValue` (y/o viceversa)? *(Recomendado: sí, para que el número tenga unidad.)*
3. **Semántica del anti-duplicado.** Confirmar comparación **normalizada/insensible a mayúsculas** y si aplica solo a registros activos (no hay `IsActive` hoy en la competencia).
4. **Snapshot del nombre del tipo/dominio.** ¿Persistir el `Name` del catálogo al registrar (histórico) además del `Code`?
5. **Borrado de ítem de catálogo en uso.** Política `Restrict` (no permitir borrar un tipo/dominio/métrica referenciado). *(Recomendado: Restrict.)*
6. **Tope de experiencia.** ¿Algún máximo razonable (p. ej. ≤ 99 años) para validación de cordura? *(Negocio: sin tope definido.)*

---

## 18. Recomendaciones del Analista de Negocio

1. **Proceder a diseño técnico de Fase 1** (RF-002, RF-003, RF-004, RF-007 + RNF de reglas/errores). El alcance está **acotado y de bajo riesgo** sobre una base ya sólida.
2. **Reusar patrones del repositorio**: validación de catálogos (estilo `AssetAccess`/`Insurances`), módulo de reglas **puro** `CurricularCompetencies.Rules.cs`, errores bilingües con paridad, migración con índice único anti-duplicado.
3. **Planificar el saneamiento/seed** antes de activar la validación estricta (R1): poblar `RequirementType`, dominio y métrica; backfill de datos existentes.
4. **Enriquecer las respuestas de lectura** con los **nombres** (no solo códigos) de tipo/dominio/métrica para la UI, y exponer `SortOrder` del dominio para el modo escala.
5. **Diferir explícitamente** el análisis de brecha vs. perfil de puesto y evidencias/verificación (FA-1/FA-2) al **roadmap**, dejando la entidad como fuente de verdad.
6. **Resolver las 6 preguntas de diseño (§17)** al inicio del plan técnico (son menores y no bloquean el negocio).

---

> **Nota de cierre.** Documento de **validación + definición de Fase 1**. La funcionalidad **ya está implementada** y, con las decisiones **D-01…D-08** (2026-06-23), el alcance de endurecimiento queda **definido**: enlazar **tipo de requisito** al catálogo de Estructura Organizativa, **catalogar dominio y métrica**, **bloquear duplicados** y validar **experiencia ≥ 0**. Lo no solicitado (brecha vs. perfil de puesto, evidencias/verificación) queda **diferido** al roadmap, en línea con la consigna de **no agregar lo innecesario**. **Listo para el plan técnico.**
