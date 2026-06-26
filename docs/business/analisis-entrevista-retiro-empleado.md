# Análisis de Negocio — Formulario de Entrevista de Retiro (Exit Interview)

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo, Cumplimiento/Privacidad, Gerencia de RRHH |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Perfil de empleado / baja (`PersonnelFileEmployeeProfile`) · Catálogos generales (`GeneralCatalogs`) · Identidad/Permisos (`IdentityAccess`/Provisioning) · **NUEVO** módulo **exclusivo** de entrevista de retiro (`ExitInterviewForm` + `ExitInterviewSubmission`) |
| **Estado** | **Definido / Cerrado (Fase 1).** Decisiones **RATIFICADAS D-01…D-14** + residuales técnicas **RQ-01…RQ-06** (respuestas del negocio del 2026-06-24). Funcionalidad **NO implementada** (desarrollo nuevo) — **lista para diseño técnico** (`plan-tecnico-entrevista-retiro.md`). |
| **Versión** | v2 (incorpora las respuestas del negocio Q-01…Q-14 → D-01…D-14) |
| **Fecha** | 2026-06-24 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |
| **Naturaleza del módulo** | **Módulo EXCLUSIVO de entrevista de retiro** (no motor genérico — D-01): constructor de formularios + **captación de respuestas** (autoservicio del empleado **y** captura por RRHH — D-04) + **base para tabulación** de causas de rotación (Fase 2 — D-10). Llenado **opcional** (D-05). |

---

## Contexto del cambio

Se requiere un **formulario para la entrevista de retiro (exit interview)**. El objetivo de negocio es **conocer los motivos por los que un empleado se retira** de la institución para **corregir las falencias que provocan rotación de personal innecesaria**. El formulario debe poder **crearse de forma dinámica** (constructor de campos y grupos) y **asociarse a un motivo de retiro**.

La especificación del cliente desglosa tres capacidades:

1. **Crear el formulario** — nombre del formulario, descripción del uso, y luego **agregar campos y grupos**.
2. **Configurar cada campo** — tipo de control (lista desplegable, casilla de selección, etc.), nombre del campo, **llenado anónimo (sí/no)**, **título** de lo que el usuario debe ingresar, **descripción**, **peso** del campo y si es **obligatorio**.
3. **Utilizar el formulario** — el formulario creado debe quedar **disponible para que lo llenen los empleados que se retiran** de la institución.

El objetivo declarado del requerimiento es **doble**: (1) **validar** si lo ya implementado está bien alineado **o** si el requerimiento **no existe**, y (2) **analizar y agregar** la información necesaria para este tipo de proceso HRIS. Si no fuera necesario hacer nada, se cerraría. Adicionalmente, se exige: **dejar los catálogos parametrizados**, **reutilizar los servicios accedidos por keys** ya existentes y hacer el **seed inicial de catálogos por país, comenzando con El Salvador (SV)**.

> ### Hallazgo clave (confirmado en código)
> **Esto SÍ es un desarrollo desde cero.** Tras una búsqueda exhaustiva en `Domain`, `Application/Features`, `Api/Controllers`, configuraciones EF y migraciones, **no existe** ninguna entidad, handler, controlador ni catálogo de **entrevista de retiro** ni de **formularios dinámicos** (se buscaron, entre otros: `ExitInterview`, `FormDefinition`, `FormTemplate`, `FormField`, `FormGroup`, `DynamicForm`, `Questionnaire`/`Cuestionario`, `Survey`/`Encuesta`, `ControlType`, `Anonymous`/`Anónimo`, `Weight`/`Peso`). El **único** rastro es `PersonnelFileCustomFieldDefinition`, que aparece **solo en migraciones antiguas** (≥ `20260409…`) pero **fue removido del código fuente** (no está en `Domain` ni registrado en el `DbContext`): es un artefacto **abandonado, no reutilizable**.
>
> **Conclusión de la validación:** **no hay nada que validar contra lo existente**; el requerimiento debe construirse íntegro. La sección **3 (Alcance)** y **18 (Recomendaciones)** proponen un **MVP por fases** porque un módulo de entrevista de retiro con constructor de formularios es un subsistema considerable.

> ### Segundo hallazgo clave — el "motivo de retiro" hoy es **texto libre, sin catálogo**
> El requisito central es **"asociar el formulario a un motivo de retiro"**, pero **el motivo de retiro no está catalogado**. En `PersonnelFileEmployeeProfile` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:49-55`) la baja se guarda como **texto libre**: `RetirementCategoryCode` (`varchar(80)`, nullable, **sin validar**), `RetirementReasonCode` (`varchar(80)`, nullable, **sin validar**), `RetirementNotes` (`varchar(2000)`) y `RetirementDate`. No hay catálogo de motivos ni de categorías de retiro. **Por lo tanto, el primer entregable de este requerimiento es el propio catálogo de "motivo de retiro"** (y su categoría), parametrizado y con **seed SV**, sobre el cual se anclará el formulario.

> ### Tercer hallazgo — "Finalizar expediente" **no es** "dar de baja"
> El comando `FinalizePersonnelFileCommand` (`Application/Features/PersonnelFiles/FinalizePersonnelFile.cs:30`) **completa el onboarding** (Draft → empleado activo, crea cuenta de usuario), **no** la terminación. La **baja** se registra **editando el perfil** (PUT `employment-information` con `RetirementDate` + códigos de retiro); **no existe** un flujo/comando formal de *offboarding*. Esto define **dónde se engancha** la entrevista de retiro (alrededor del registro de la baja, no en `finalize`).

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado en código) | Implicación |
|---|---|---|---|
| 1 | **Formulario de entrevista de retiro** | **No existe** (0 coincidencias). | Construir desde cero. |
| 2 | **Constructor de formularios dinámicos** | **No existe** infraestructura genérica de formularios. `PersonnelFileCustomFieldDefinition` solo vive en migraciones viejas, removido del código. | Construir desde cero; no reutilizable. |
| 3 | **Motivo de retiro** | `RetirementReasonCode` / `RetirementCategoryCode` = **texto libre** `varchar(80)`, nullable, **sin catálogo ni validación** (`PersonnelFileEmployee.cs:49-51`). | **Crear catálogo(s)** de motivo/categoría de retiro (RF-001). |
| 4 | **Notas de baja** | `RetirementNotes` `varchar(2000)` texto libre (`:53`). | Conserva; complementa, no sustituye, a la entrevista. |
| 5 | **Registro de la baja** | Vía PUT `…/employment-information` (`Employment/EmployeeProfiles.cs`); el rehire **limpia** estos campos (`Rehire/RehireEmployee.cs`). | La entrevista se ancla al perfil/baja del **periodo** actual. |
| 6 | **Catálogo país-scoped (patrón)** | `GeneralCatalogItem : CountryScopedCatalogItem` (`Domain/GeneralCatalogs/GeneralCatalogItems.cs`); índice único `(CountryCatalogItemId, NormalizedCode)`. SV = `CountryCatalogDefinition(-7068L,"SV",…)` (`Domain/Locations/CountryCatalogData.cs:75`). | Reutilizar el patrón para los catálogos nuevos. |
| 7 | **Servicios por key (patrón)** | `GeneralCatalogKeyMap` (wire key → categoría) e `IPositionCatalogLookup.GetActiveCatalogReferenceByCodeAsync(tenant,type,code,…)` (validar-por-código → código canónico). | Reutilizar para exponer y **validar** catálogos por key. |
| 8 | **Seed por país (patrón)** | `HasData` vía `GlobalCatalogSeedData` (todas las envs, backfill en prod) vs `DevSeedService` (solo dev). Regla: catálogo que debe existir en prod → **HasData**. | Seed SV vía **HasData** (un catálogo de motivos de retiro que daría 404 si falta). |
| 9 | **Permisos (patrón)** | `AuthorizationPolicySet` es **solo a nivel de clase** → **controlador dedicado** por política; split View/Manage; gate **self-service** (`LoadForCreateOwnOrManage…` compara `LinkedUserPublicId` con el usuario actual). | Controlador(es) dedicado(s) + permisos nuevos + autoservicio del empleado. |
| 10 | **Reglas (patrón)** | Módulo puro `*.Rules.cs` (funciones puras + `Error` con código bilingüe). | Crear `ExitInterview.Rules.cs`. |
| 11 | **Localización (patrón)** | `BackendMessages.resx` / `.es.resx`; test de **paridad EN/ES** obliga a tener ambas. | Errores `EXIT_INTERVIEW_*` bilingües. |
| 12 | **Adjuntos (patrón)** | `FilePurpose` (`Domain/Files/FileEnums.cs`) + entidad `*Document` + flujo *upload-session → complete*. | Opcional: `FilePurpose.ExitInterviewDocument`. |
| 13 | **Tabulación / analítica de rotación** | **No existe** ninguna reportería de causas de rotación. | Objetivo de negocio real; **Fase 2** (RF-014). |

---

## Decisiones del negocio (ratificadas — 2026-06-24)

> Respuestas del negocio a las preguntas **Q-01…Q-14** (sección 17). **Reemplazan** las propuestas P-xx del análisis v1. Las preguntas **técnicas residuales/derivadas** quedan listadas en la sección 17.

| # | Tema | Decisión ratificada |
|---|---|---|
| **D-01** | ¿Motor genérico o módulo exclusivo? | **Módulo EXCLUSIVO de entrevista de retiro.** Sin discriminador `FormType` ni motor reutilizable; entidades específicas (`ExitInterview*`). |
| **D-02** | Estructura del motivo de retiro | **Jerárquico, alineado a HRIS:** clasificación **`SeparationType`** (`VOLUNTARIA`/`INVOLUNTARIA`/`OTRA`) como **atributo de la Categoría**; **Categoría** (`RetirementCategoryCode`) → **Motivo** (`RetirementReasonCode`, FK a categoría). 2 niveles **almacenados** + clasificación para *roll-up*. Texto libre → **validado por código**. |
| **D-03** | Asociación formulario ↔ motivo | **Un formulario se asocia a UN (1) motivo.** **Un solo formulario activo por motivo** (resolución determinista). **Sin** formulario por defecto, **sin** asociación por categoría, **sin** multi-motivo. |
| **D-04** | ¿Quién llena / quién lee? | **Llenado por AMBOS:** autoservicio del empleado **y** captura por RRHH. **Lectura de respuestas: RRHH** (ver D-14). |
| **D-05** | ¿Obligatoria antes de la baja? | **Opcional / informativa.** El empleado puede hacerla o no; **nunca bloquea** la baja ni la finalización. |
| **D-06** | Semántica de "llenado anónimo" | **A nivel de TODA la submission** (no por campo). El **formulario** se marca **anónimo (sí/no)**; si es anónimo, la submission se almacena **disociada** de la identidad (sin FK al expediente), conservando dimensiones **de-identificadas** (motivo/categoría/tipo/área/periodo) para analítica. **Se elimina** el atributo anónimo por campo. |
| **D-07** | Semántica de "peso" / score | **Ponderado**; el modelo de puntaje lo define el analista (ver **RF-012 → "Modelo de puntaje"**): opciones/escala normalizadas a **0–100**, peso de campo ≥ 0, **índice 0–100** = promedio ponderado. |
| **D-08** | Tipos de control del MVP | **Confirmados** los 9 (no falta ninguno): texto corto/largo, número, fecha, lista, opción única, selección múltiple, casilla Sí/No, escala. |
| **D-09** | Adjuntos | **No** se requieren. **Fuera de alcance** (sin `FilePurpose.ExitInterviewDocument`; RF-021 retirado de Fase 1). |
| **D-10** | Tabulación / reportes | **Por motivo, categoría, área, periodo y score**, con **exportación**. **Fase 2** (RF-014). |
| **D-11** | Motivos legados (texto libre) | **Eliminar** (son datos de prueba). **Sin backfill/normalización**; limpiar columnas (drop&recreate). |
| **D-12** | Submissions tras un rehire | **Se archivan** (no se borran, no quedan activas). |
| **D-13** | Seed SV | **Aprobada** la propuesta de la sección 18 (refinada con `SeparationType` — D-02). |
| **D-14** | ¿Quién lee las respuestas? | **Solo RRHH.** **No** jefatura del área. |

---

## Brechas verificadas y su resolución (GAP → Resolución)

| # | Brecha (as-is, verificada) | Resolución (to-be) |
|---|---|---|
| **G-01** | No existe formulario de entrevista de retiro. | Construir el módulo completo (RF-003…RF-013). |
| **G-02** | No existe constructor de formularios ni infra reutilizable. | Nuevo **módulo exclusivo** `ExitInterviewForm` (no genérico — D-01). |
| **G-03** | Motivo de retiro = texto libre sin catálogo (anclaje inexistente). | Catálogos `RetirementCategory` + `RetirementReason` país-scoped, validados por código, seed SV (RF-001). |
| **G-04** | Tipo de control no catalogado. | Catálogo de sistema `form-control-types` (RF-002). |
| **G-05** | No hay forma de presentar el formulario al empleado que se retira. | Endpoint de "formulario disponible por motivo" + autoservicio (RF-010/RF-011). |
| **G-06** | No hay almacenamiento de respuestas ni tabulación. | `ExitInterviewSubmission` + `ExitInterviewAnswer` con score derivado (RF-012); analítica Fase 2 (RF-014). |
| **G-07** | No hay versionado/snapshot de definiciones. | Publicación + versión + snapshot en submission (RF-008, RF-012). |
| **G-08** | No hay permisos ni autoservicio para este flujo. | Permisos dedicados + gate self-service (RF-013). |
| **G-09** | No hay reglas puras ni mensajes bilingües. | `ExitInterview.Rules.cs` + claves `EXIT_INTERVIEW_*` EN/ES (RNF). |
| **G-10** | Privacidad del "llenado anónimo" no contemplada. | Anonimato **a nivel de submission/formulario** (no por campo): submission **disociada** del expediente (D-06). |

---

## 1. Resumen del producto o requerimiento

Un **módulo exclusivo de entrevista de retiro** (D-01) que permite a RRHH **diseñar formularios** (nombre, descripción, **anónimo Sí/No a nivel de formulario**, **grupos** y **campos** con tipo de control, título, descripción, **peso** y **obligatoriedad**), **asociarlos a un (1) motivo de retiro**, **publicarlos** y ponerlos **disponibles para que los empleados que se retiran los llenen** (autoservicio, **opcional**) o RRHH los capture en una entrevista. El fin último es **tabular las causas de rotación** para **corregir falencias** que generan rotación innecesaria.

La funcionalidad **no existe** en el sistema (validación confirmada en código). Es un **desarrollo nuevo**. Como prerrequisito, debe crearse el **catálogo de motivo de retiro** (hoy texto libre) y un **catálogo de tipos de control**, ambos **parametrizados**, **accedidos por key** (reutilizando `GeneralCatalogKeyMap` + lookup validar-por-código) y con **seed inicial para El Salvador**.

---

## 2. Objetivos del negocio

- **O-1. Reducir la rotación innecesaria:** capturar de forma estructurada **por qué** se va la gente para **corregir las causas raíz**.
- **O-2. Estandarizar el "motivo de retiro":** pasar de texto libre a **catálogo** parametrizable y reportable (categoría + motivo).
- **O-3. Flexibilidad sin desarrollo:** que RRHH **construya y ajuste** sus formularios **sin** intervención de TI (constructor de formularios).
- **O-4. Tabulación/analítica:** habilitar el **conteo y ponderación** (peso) de las razones para priorizar acciones (Fase 2).
- **O-5. Autoservicio + franqueza:** que el **empleado que se retira** complete el formulario por sí mismo; un formulario puede marcarse **anónimo** (su submission **no se atribuye** al empleado — D-06) para fomentar respuestas honestas.
- **O-6. Cumplimiento y trazabilidad:** auditoría de cambios, concurrencia, multi-tenant y tratamiento adecuado de datos personales/anónimos.
- **O-7. Foco y simplicidad:** entregar un **módulo exclusivo** de entrevista de retiro (D-01), **sin** sobre-construir un motor genérico; cada pieza (catálogos, formulario, respuestas) resuelve **solo** este proceso.

---

## 3. Alcance funcional

### Fase 1 — MVP (constructor + captación)
- **F1.** Catálogos **parametrizados** y con **seed SV**: `RetirementCategory` (con clasificación `SeparationType` — D-02) → `RetirementReason` (motivo) y `form-control-types` (tipos de control) — RF-001, RF-002.
- **F2.** **Crear/editar formulario** (nombre, descripción del uso, **anónimo Sí/No a nivel de formulario** — D-06) — RF-003.
- **F3.** **Grupos** (secciones) ordenables — RF-004.
- **F4.** **Campos** con sus atributos (tipo de control, nombre, título, descripción, **peso**, **obligatorio**, orden) — *el anonimato ya **no** es por campo* (D-06) — RF-005.
- **F5.** **Opciones** para campos de selección (lista/radio/múltiple), con etiqueta y **puntaje** — RF-006.
- **F6.** **Validación de definición** (coherencia tipo de control ↔ opciones/rangos) vía módulo de reglas — RF-007.
- **F7.** **Publicar / versionar / archivar** formulario; bloqueo tras publicación — RF-008.
- **F8.** **Asociación a UN (1) motivo de retiro** con **único activo por motivo** (D-03) — RF-009.
- **F9.** **Consultar el formulario aplicable** al motivo (para llenarlo) — RF-010.
- **F10.** **Llenar el formulario** (autoservicio del empleado **y** captura por RRHH — D-04), respuestas con **snapshot**, **anonimato a nivel de submission** (D-06) y **score ponderado** derivado (D-07) — RF-011, RF-012.
- **F11.** **Permisos** dedicados + **autoservicio** + **lectura de respuestas solo RRHH** (D-14) + controlador(es) dedicado(s) — RF-013.
- **F12.** **Archivar** las submissions del periodo previo cuando hay **rehire** (D-12) — RF-012 (RN-012.6).
- **F13.** **Reglas puras** + **mensajes bilingües** + **auditoría/concurrencia/soft-delete/multi-tenant** (RNF).

### Fase 2 — Tabulación / analítica
- Reportes de causas de rotación por **motivo, categoría, área, periodo** y **score ponderado** (peso de campos/opciones), con **exportación** (D-10) — RF-014.

### Fase 3 — Evoluciones (no comprometidas)
- Posibles mejoras: **lógica condicional** (skip logic) y **notificaciones**. **Descartado por decisión:** la **generalización del motor** a otros formularios (D-01) y la **obligatoriedad** de la entrevista antes de la baja (D-05, es opcional).

---

## 4. Fuera de alcance

- **FA-1.** **Motor de formularios genérico/reutilizable** para otros dominios (clima, onboarding, evaluación) — **descartado** (D-01): el módulo es **exclusivo** de entrevista de retiro. Incluye lógica condicional/saltos y cálculos/fórmulas.
- **FA-2.** **Dashboards/BI** y analítica avanzada de rotación (predicción, cohortes) — la **tabulación** es Fase 2; lo avanzado queda fuera.
- **FA-3.** **Workflow de aprobación** de la entrevista (revisión/firmas/estados gobernados).
- **FA-4.** **Notificaciones** automáticas (correo/WhatsApp) al empleado o a RRHH.
- **FA-5.** **Obligatoriedad** de la entrevista para finalizar la baja — **descartada** (D-05): la entrevista es **opcional**.
- **FA-6.** **Anonimato por campo** — **descartado** (D-06): el anonimato es a nivel de **toda la submission/formulario**.
- **FA-7.** **Lectura de respuestas individuales por jefatura/área** — fuera (D-14): **solo RRHH**. Gerencia consume **tabulación agregada** (Fase 2).
- **FA-8.** **Adjuntos** (carta de renuncia u otros) — fuera (D-09).
- **FA-9.** **Backfill/normalización** de los motivos de retiro legados — no aplica (D-11): se **eliminan** (datos de prueba).
- **FA-10.** **Internacionalización del contenido** del formulario (multilenguaje por campo) — se crea en el idioma del tenant.
- **FA-11.** **Integración** con sistemas externos de encuestas (SurveyMonkey, Typeform, etc.) y **firma electrónica** de la entrevista.

---

## 5. Actores o usuarios involucrados

| Actor | Rol |
|---|---|
| **Administrador/RRHH (diseñador de formularios)** | Crea, edita, publica, versiona y archiva formularios; gestiona catálogos de motivos de retiro; asocia formularios a motivos. |
| **RRHH / Entrevistador** | Realiza la entrevista de salida y **captura** respuestas en nombre del empleado (entrevista presencial); es el **único rol que puede leer** las respuestas (D-04, D-14). |
| **Empleado que se retira (autoservicio)** | Llena su propia entrevista de retiro (opcional — D-05); si el formulario es **anónimo**, su submission **no se le atribuye** (D-06). Puede ver/retomar su **borrador** antes de enviar. |
| **Gerencia / Analista de RRHH** | Consume la **tabulación agregada** de causas de rotación (Fase 2); **no** accede a respuestas individuales (D-14). |
| **Auditor / Cumplimiento** | Revisa trazabilidad de cambios y el tratamiento de datos anónimos/personales. |
| **Sistema (CLARIHR)** | Valida definiciones y respuestas, deriva scores, versiona, controla concurrencia, multi-tenant y permisos; expone catálogos por key. |

---

## 6. Requerimientos funcionales

> **Convención de prioridad:** `Alta` = núcleo del MVP · `Media` = importante · `Baja` = mejora/posterior.
> **Convención de IDs:** `RF-00X` requerimiento · `RN-00X.y` regla de negocio del requerimiento · `EXIT_INTERVIEW_*` / `RETIREMENT_REASON_*` códigos de error bilingües.
> Los **endpoints son referenciales** (orientan al diseño técnico; no son contrato cerrado). Todas las escrituras son **multi-tenant**, **auditadas**, con **concurrencia optimista** (`If-Match`/`ConcurrencyToken`) y **soft-delete** salvo indicación contraria.

---

### Grupo A — Catálogos y parametrización

### RF-001 — Catálogo jerárquico de "motivo de retiro" (categoría → motivo)
**Descripción:** Crear dos catálogos **país-scoped** relacionados, con la estructura **alineada a HRIS** (D-02): `RetirementCategoryCatalogItem` (agrupación de motivos) que lleva una **clasificación `SeparationType`** (`VOLUNTARIA` / `INVOLUNTARIA` / `OTRA`), y `RetirementReasonCatalogItem` (motivo específico que **cuelga** de una categoría). Así se obtienen **tres niveles de roll-up** para reportes (tipo → categoría → motivo) usando solo los **dos códigos** que ya existen en el perfil. Reemplazar el **texto libre** `RetirementCategoryCode`/`RetirementReasonCode` (`PersonnelFileEmployee.cs:49-51`) por **códigos validados** contra estos catálogos al registrar la baja. Es el **anclaje** del formulario (RF-009) y el prerrequisito de todo el módulo.

**API (referencial):**
- `GET /api/v1/general-catalogs/retirement-categories?country=SV` (devuelve `separationType` por categoría)
- `GET /api/v1/general-catalogs/retirement-reasons?categoryCode=RENUNCIA_VOLUNTARIA&country=SV`
- Validación en el `PUT …/employment-information` existente (registro de baja).

**Reglas de negocio:**
- **RN-001.1** País-scoped (`CountryScopedCatalogItem`); SV es el primer país sembrado (RF-001-seed, D-13).
- **RN-001.2** Índice único: categoría `(país, código)`; motivo `(país, categoría, código)` — el mismo código de motivo puede repetirse bajo **distintas** categorías (patrón análogo a *insurance type → range*).
- **RN-001.3** Un motivo pertenece a **exactamente una** categoría (FK obligatoria y **activa**).
- **RN-001.4** Cada **categoría** declara un `SeparationType` ∈ {`VOLUNTARIA`, `INVOLUNTARIA`, `OTRA`} (clasificación fija para *roll-up* de reportes — D-02).
- **RN-001.5** Códigos normalizados (trim + UPPER); `Name` obligatorio; `IsActive`, `SortOrder`.
- **RN-001.6** El lookup **por código** retorna el código **canónico**; un código inexistente o inactivo se **rechaza**.
- **RN-001.7** No se puede **inactivar** una categoría que tenga motivos activos (primero reasignar/inactivar sus motivos) — integridad.
- **RN-001.8** Un código **en uso** (por bajas o submissions) **no se borra**: se **inactiva** (RF-015). **No** hay migración de datos legados: los motivos en texto libre **se eliminan** (D-11, son datos de prueba).

**Criterios de aceptación (Gherkin):**
- *Lectura:* **Dado** un país SV con categorías sembradas, **cuando** consulto `retirement-categories`, **entonces** recibo solo las activas (con su `separationType`), ordenadas por `SortOrder`.
- *Jerarquía:* **Dado** el motivo `MEJOR_OFERTA_SALARIAL` bajo `RENUNCIA_VOLUNTARIA`, **cuando** filtro motivos por esa categoría **entonces** aparece; **cuando** filtro por otra categoría **entonces** no aparece.
- *Roll-up por tipo:* **Dado** varias categorías con `separationType=VOLUNTARIA`, **cuando** la tabulación (RF-014) agrupa por tipo, **entonces** todas suman bajo `VOLUNTARIA`.
- *Validación de baja:* **Dado** un registro de baja con `RetirementReasonCode` inexistente/inactivo, **cuando** guardo, **entonces** error `RETIREMENT_REASON_INVALID` (422) y no persiste.
- *Canonicalización:* **Dado** un código con distinto casing/espacios (`" mejor_oferta_salarial "`), **cuando** lo envío, **entonces** se resuelve al canónico `MEJOR_OFERTA_SALARIAL`.
- *Código repetible entre categorías:* **Dado** el código `OTRO` bajo dos categorías distintas, **cuando** los creo, **entonces** ambos se permiten.

**Prioridad:** Alta · **Fase 1 (PR-1)** · **Dependencias:** `GeneralCatalogs`, `GeneralCatalogKeyMap`, lookup validar-por-código, `GlobalCatalogSeedData`.

### RF-002 — Catálogo de tipos de control de campo (sistema), accedido por key
**Descripción:** Catálogo **cerrado y gobernado por el sistema** de los tipos de control disponibles para un campo. Cada tipo declara metadatos que el **backend** usa para validar y el **frontend** para renderizar. Expuesto por key `form-control-types`; **no editable por el tenant**. Conjunto inicial: `TEXTO_CORTO`, `TEXTO_LARGO`, `NUMERO`, `FECHA`, `LISTA_DESPLEGABLE`, `OPCION_UNICA` (radio), `SELECCION_MULTIPLE`, `CASILLA` (Sí/No booleano), `ESCALA` (Likert 1..n).

**Reglas de negocio:**
- **RN-002.1** Conjunto **cerrado**: alta de nuevos tipos solo por *release* (migración/seed), nunca por API de tenant.
- **RN-002.2** Cada tipo declara: `ValueKind` (texto/número/fecha/booleano/opciones), `SupportsOptions`, `SupportsRange`, `SupportsMultiple`.
- **RN-002.3** El **código** es neutro/universal; las **etiquetas** son localizables (ES/EN).
- **RN-002.4** Lectura por key con metadatos; **cacheable**.

**Criterios de aceptación (Gherkin):**
- *Capacidades:* **Dado** el catálogo sembrado, **cuando** consulto `form-control-types`, **entonces** obtengo los tipos con sus banderas (p.ej. `LISTA_DESPLEGABLE` → `SupportsOptions=true`; `NUMERO` → `SupportsRange=true, SupportsOptions=false`).
- *No editable:* **Dado** un tenant, **cuando** intenta crear/editar un tipo de control, **entonces** no está disponible (403/404).

**Prioridad:** Alta · **Fase 1 (PR-2)** · **Dependencias:** `GeneralCatalogs`.

### RF-015 — Administración (CRUD) de catálogos de motivo de retiro
**Descripción:** Permitir a RRHH **crear/editar/activar/inactivar/ordenar** categorías y motivos de retiro (sobre el seed SV), por país, respetando integridad.

**API (referencial):** `POST/PUT/PATCH /api/v1/general-catalogs/retirement-categories[/{id}]` y `…/retirement-reasons[/{id}]`.

**Reglas de negocio:**
- **RN-015.1** Requiere `ManageExitInterviewForms` (o permiso de catálogos del tenant).
- **RN-015.2** No inactivar una categoría con motivos activos (RN-001.6); no borrar un código en uso (RN-001.7) → inactivar.
- **RN-015.3** Cambios **auditados** (before/after).

**Criterios de aceptación (Gherkin):**
- **Dado** `ManageExitInterviewForms`, **cuando** creo un motivo bajo una categoría activa, **entonces** queda disponible para asociación y baja.
- **Dado** una categoría con motivos activos, **cuando** intento inactivarla, **entonces** error `RETIREMENT_CATEGORY_HAS_ACTIVE_REASONS`.
- **Dado** un motivo usado por bajas/submissions, **cuando** intento borrarlo, **entonces** se ofrece inactivar (no se borra).

**Prioridad:** Media · **Fase 1/2** · **Dependencias:** RF-001, RF-013.

---

### Grupo B — Constructor del formulario

### RF-003 — Crear / editar la cabecera del formulario
**Descripción:** Crear un formulario de entrevista de retiro (raíz del agregado) con **nombre**, **descripción del uso** y **bandera de anonimato (`IsAnonymous` Sí/No)** a **nivel de formulario** (D-06), en estado `Draft`; editar nombre/descripción/anonimato mientras esté en `Draft`. Como el módulo es **exclusivo** (D-01), **no** hay discriminador `FormType`.

**API (referencial):** `POST /api/v1/exit-interview-forms` · `PUT /api/v1/exit-interview-forms/{publicId}` (If-Match) · `GET …/{publicId}`.

**Reglas de negocio:**
- **RN-003.1** `Name` obligatorio (3..200), **único por tenant** (`NormalizedName`).
- **RN-003.2** `Description` opcional (≤ 1000).
- **RN-003.3** `IsAnonymous` (Sí/No) se define a **nivel de formulario** (D-06); si es `Sí`, **todas** sus submissions se almacenan **disociadas** del expediente (RF-012). Solo editable en `Draft` (al publicar queda fijo para esa versión).
- **RN-003.4** Estado inicial `Draft`. Solo editable **estructuralmente** en `Draft` (ver RF-008 para `Published`).
- **RN-003.5** Concurrencia (`ConcurrencyToken`/If-Match); soft-delete (`IsActive`).
- **RN-003.6** Requiere `ManageExitInterviewForms`.

**Criterios de aceptación (Gherkin):**
- **Dado** `ManageExitInterviewForms`, **cuando** creo un formulario con nombre único, **entonces** queda en `Draft` v1 y recibo `PublicId` + `ConcurrencyToken`.
- *Anonimato del formulario:* **Dado** un formulario marcado `IsAnonymous=Sí`, **cuando** lo publico y se responde, **entonces** las submissions **no** quedan vinculadas al empleado (RF-012).
- *Duplicado:* **Dado** un nombre ya usado (normalizado) en el tenant, **cuando** creo, **entonces** error `EXIT_INTERVIEW_FORM_NAME_DUPLICATE` (409).
- *Concurrencia:* **Dado** un `If-Match` desactualizado, **cuando** edito, **entonces** 409.
- *Permiso:* **Dado** un usuario sin permiso, **cuando** crea, **entonces** 403.

**Prioridad:** Alta · **Fase 1 (PR-3)** · **Dependencias:** RF-001.

### RF-004 — Gestión y ordenamiento de grupos (secciones)
**Descripción:** Agregar/editar/**reordenar**/eliminar grupos (secciones) del formulario; cada grupo tiene nombre/título, descripción y orden. Organizan visualmente los campos.

**API (referencial):** `POST/PUT/DELETE …/forms/{id}/groups[/{groupId}]` · `PUT …/forms/{id}/groups/reorder`.

**Reglas de negocio:**
- **RN-004.1** Grupo pertenece a un formulario; `Title` obligatorio (≤ 200); `Description` ≤ 1000.
- **RN-004.2** `DisplayOrder` ≥ 0; el sistema normaliza el orden al guardar.
- **RN-004.3** Un campo pertenece a **0..1** grupo; los campos sin grupo se renderizan en el nivel raíz.
- **RN-004.4** Eliminar un grupo **no borra** sus campos: quedan **sin grupo** (RF-004).
- **RN-004.5** Solo en `Draft` (o nueva versión).

**Criterios de aceptación (Gherkin):**
- **Dado** un formulario en `Draft`, **cuando** agrego 3 grupos y los reordeno, **entonces** se listan en el nuevo orden.
- *Eliminación segura:* **Dado** un grupo con campos, **cuando** lo elimino, **entonces** sus campos quedan **sin grupo** y siguen existiendo.
- *Bloqueo por estado:* **Dado** un formulario `Published`, **cuando** intento editar grupos, **entonces** se exige crear **nueva versión** (RF-008).

**Prioridad:** Media · **Fase 1 (PR-3)** · **Dependencias:** RF-003.

### RF-005 — Gestión de campos (atributos del requisito)
**Descripción:** Agregar/editar/**reordenar**/eliminar campos. Cada campo captura los atributos pedidos por el negocio: **tipo de control** (RF-002), **nombre del campo** (clave técnica), **título**, **descripción**, **peso**, **obligatorio (sí/no)**, **orden**, **grupo** (opcional) y **configuración por tipo** (min/máx, longitud, n de la escala). **Nota (D-06):** el **"llenado anónimo" ya NO es un atributo de campo** — el anonimato se define a **nivel de formulario** (RF-003).

**API (referencial):** `POST/PUT/DELETE …/forms/{id}/fields[/{fieldId}]` · `PUT …/forms/{id}/fields/reorder`.

**Reglas de negocio:**
- **RN-005.1** `ControlTypeCode` válido (catálogo RF-002).
- **RN-005.2** `FieldKey` obligatorio, **único** en el formulario (normalizado), patrón `[A-Za-z0-9_]`, **estable** (es la clave de la respuesta).
- **RN-005.3** `Title` obligatorio (≤ 300); `Description` ≤ 1000.
- **RN-005.4** `Weight` decimal **≥ 0** (recomendado 1–10; default `1`). Solo aplica a campos **puntuables** (selección/escala) para el score (RF-012).
- **RN-005.5** `IsRequired` booleano. *(El anonimato es del formulario, no del campo — D-06.)*
- **RN-005.6** Config por tipo: `NUMERO`/`ESCALA` admiten `Min`/`Max` con `Min ≤ Max`; `TEXTO_*` admite `MaxLength`; los de **selección exigen opciones** (RF-006).
- **RN-005.7** Coherencia: un campo de **selección** no lleva rango; uno **numérico** no lleva opciones (validado en RF-007).
- **RN-005.8** `DisplayOrder` ≥ 0; solo en `Draft`/nueva versión.

**Criterios de aceptación (Gherkin):**
- *Persistencia completa:* **Dado** un campo nuevo, **cuando** lo guardo con sus atributos, **entonces** se persisten tipo, clave, título, descripción, peso, obligatorio, orden y grupo.
- *Selección sin opciones:* **Dado** `LISTA_DESPLEGABLE` sin opciones, **cuando** intento publicar, **entonces** error `EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED`.
- *Peso inválido:* **Dado** `Weight = -1`, **cuando** guardo, **entonces** error `EXIT_INTERVIEW_FIELD_WEIGHT_INVALID`.
- *Clave duplicada:* **Dado** dos campos con el mismo `FieldKey`, **cuando** guardo el segundo, **entonces** error `EXIT_INTERVIEW_FIELD_KEY_DUPLICATE`.
- *Rango inválido:* **Dado** `NUMERO` con `Min=10, Max=5`, **cuando** guardo, **entonces** error `EXIT_INTERVIEW_FIELD_RANGE_INVALID`.

**Prioridad:** Alta · **Fase 1 (PR-3)** · **Dependencias:** RF-002, RF-004.

### RF-006 — Opciones de campos de selección (con puntaje)
**Descripción:** Para `LISTA_DESPLEGABLE`, `OPCION_UNICA` y `SELECCION_MULTIPLE`, gestionar las **opciones**: código/valor, **etiqueta** visible, **orden** y **puntaje** opcional (alimenta la tabulación ponderada — D-07, ver RF-012).

**Reglas de negocio:**
- **RN-006.1** Solo aplica a tipos con `SupportsOptions=true`.
- **RN-006.2** `OptionCode` **único** dentro del campo; `Label` obligatorio.
- **RN-006.3** **≥ 1** opción (recomendado ≥ 2) para poder **publicar**.
- **RN-006.4** `Score` decimal opcional (default null/0); `DisplayOrder` ≥ 0.

**Criterios de aceptación (Gherkin):**
- *Duplicado:* **Dado** un campo de selección, **cuando** agrego opciones con código repetido, **entonces** error `EXIT_INTERVIEW_OPTION_CODE_DUPLICATE`.
- *Tipo no soportado:* **Dado** un campo `NUMERO`, **cuando** agrego opciones, **entonces** error (no soportado).
- *Puntaje:* **Dado** opciones con `Score`, **cuando** se responde, **entonces** alimentan el score derivado (RF-012).

**Prioridad:** Media · **Fase 1 (PR-3)** · **Dependencias:** RF-005.

### RF-007 — Validación de coherencia de la definición (módulo de reglas puro)
**Descripción:** Validación **integral** de la definición (en cada guardado y antes de publicar): coherencia tipo↔config↔opciones, unicidad de claves, pesos/puntajes válidos, **al menos un campo**, grupos referenciados existentes, nombre único. Implementada como módulo **puro** `ExitInterview.Rules.cs` (sin acceso a BD), unit-testeable.

**Reglas de negocio:**
- **RN-007.1** Consolida y evalúa, sin BD, las reglas de RF-003…RF-006.
- **RN-007.2** Devuelve la **lista completa de incidencias** (no solo la primera), al estilo del *preview* de finalización (`FinalizePersonnelFile.cs`), para que el frontend muestre todo lo que falta.
- **RN-007.3** Cada incidencia es un `Error` con código bilingüe `EXIT_INTERVIEW_*`.

**Criterios de aceptación (Gherkin):**
- *Inelegible:* **Dado** un formulario con varias incoherencias, **cuando** valido/publico, **entonces** recibo la **lista** de errores específicos.
- *Elegible:* **Dado** un formulario válido (≥ 1 campo, claves únicas, opciones donde corresponde), **cuando** valido, **entonces** "elegible para publicar".
- *Cobertura:* cada regla tiene **prueba unitaria** y **paridad EN/ES** (RNF).

**Prioridad:** Alta · **Fase 1 (PR-3)** · **Dependencias:** RF-002, RF-005, RF-006.

### RF-016 — Previsualización del formulario (preview)
**Descripción:** Render **de solo lectura** de la definición (como la vería el empleado) **sin** crear submission, para validar antes de publicar.

**API (referencial):** `GET …/forms/{id}/preview`.

**Reglas de negocio:** **RN-016.1** Disponible en `Draft` y `Published`; **RN-016.2** No persiste nada; **RN-016.3** Refleja grupos/campos/opciones en orden y la config por tipo.

**Criterios de aceptación:** **Dado** un formulario en `Draft`, **cuando** solicito el preview, **entonces** obtengo la estructura renderizable y **no** se crea ninguna submission.

**Prioridad:** Baja/Media · **Fase 1** · **Dependencias:** RF-005, RF-006.

### RF-017 — Duplicar / clonar un formulario (plantilla)
**Descripción:** Crear un nuevo formulario `Draft` a partir de uno existente, copiando grupos/campos/opciones, con **nombre nuevo**; útil para reutilizar formularios como plantilla.

**API (referencial):** `POST …/forms/{id}/clone` (body: nuevo nombre).

**Reglas de negocio:** **RN-017.1** El clon nace en `Draft` v1, **sin** asociaciones a motivos ni submissions; **RN-017.2** Nombre **único** por tenant; **RN-017.3** Estructura **idéntica** e **independiente** del original.

**Criterios de aceptación:** **Dado** un formulario `Published`, **cuando** lo clono con un nombre nuevo, **entonces** obtengo un `Draft` con la misma estructura y sin asociaciones; editarlo **no** afecta al original.

**Prioridad:** Baja · **Fase 1/2** · **Dependencias:** RF-003…RF-006.

---

### Grupo C — Publicación, versionado y asociación

### RF-008 — Publicar / versionar / archivar
**Descripción:** **Publicar** (`Draft → Published`, **bloquea** la estructura); **editar** un `Published` crea una **nueva versión** `Draft` (clon); **archivar** retira de uso sin borrar; consultar el **histórico** de versiones.

**API (referencial):** `POST …/forms/{id}/publish` · `POST …/forms/{id}/new-version` · `POST …/forms/{id}/archive` · `GET …/forms/{id}/versions`.

**Reglas de negocio:**
- **RN-008.1** Publicar exige pasar **RF-007**.
- **RN-008.2** `Published` es **inmutable** estructuralmente; "editar" = crear versión **N+1** en `Draft`.
- **RN-008.3** Una sola versión `Published` **vigente** por formulario lógico; las anteriores quedan como **histórico**.
- **RN-008.4** `Archived` no se asigna a motivos ni se presenta a empleados.
- **RN-008.5** **No se borra** un formulario/versión con submissions → se **archiva** (RN-13).

**Criterios de aceptación (Gherkin):**
- *Publicación:* **Dado** un `Draft` válido, **cuando** publico, **entonces** pasa a `Published` v1 y su estructura queda **bloqueada**.
- *Versionado:* **Dado** un `Published`, **cuando** edito un campo, **entonces** se crea **v2 `Draft`** y v1 permanece intacta y consultable.
- *Borrado seguro:* **Dado** un formulario con submissions, **cuando** intento borrarlo, **entonces** error que **sugiere archivar**.

**Prioridad:** Media · **Fase 1 (PR-4)** · **Dependencias:** RF-007.

### RF-009 — Asociación a UN motivo de retiro (único activo por motivo)
**Descripción:** Asociar un formulario **`Published`** a **exactamente un (1) motivo** de retiro (D-03); garantizar **a lo sumo un formulario ACTIVO por motivo** para una **resolución determinista** en RF-010. **No** hay asociación por categoría, **ni** multi-motivo, **ni** formulario por defecto.

**API (referencial):** `PUT …/forms/{id}/reason` (un solo `reasonCode`).

**Reglas de negocio:**
- **RN-009.1** Solo formularios `Published` pueden asociarse.
- **RN-009.2** Un formulario referencia **un único** `RetirementReasonCode` válido (RF-001).
- **RN-009.3** **Single-active por motivo:** activar un formulario para un motivo **desactiva** el formulario que estuviera activo para ese mismo motivo (D-03).
- **RN-009.4** **Resolución determinista:** un motivo tiene **0 ó 1** formulario activo. Si tiene 0, **no hay entrevista** para ese motivo (consistente con que es **opcional** — D-05).
- **RN-009.5** Cambios de asociación **auditados**.

**Criterios de aceptación (Gherkin):**
- *1 motivo:* **Dado** un formulario `Published`, **cuando** lo asocio, **entonces** queda ligado a **un** motivo (no a varios ni a una categoría).
- *Single-active:* **Dado** un motivo con el formulario A activo, **cuando** activo el formulario B para el mismo motivo, **entonces** A se desactiva y B queda activo.
- *Sin formulario:* **Dado** un motivo sin formulario activo, **cuando** se resuelve el aplicable, **entonces** "sin formulario" (no error).
- *Estado inválido:* **Dado** un formulario `Draft`, **cuando** intento asociarlo, **entonces** error.

**Prioridad:** Alta · **Fase 1 (PR-4)** · **Dependencias:** RF-001, RF-008.

---

### Grupo D — Uso del formulario (llenado y respuestas)

### RF-010 — Resolver / consultar el formulario aplicable (para llenar)
**Descripción:** Dado un expediente/empleado y/o un **motivo de retiro**, devolver la **definición completa renderizable** del formulario aplicable (grupos, campos en orden, opciones, config, **versión** a la que se responderá).

**API (referencial):** `GET /api/v1/personnel-files/{id}/exit-interview/form` (resuelve por el motivo de la baja) · `GET …/exit-interview-forms/resolve?reasonCode=…`.

**Reglas de negocio:**
- **RN-010.1** Resolución por **RF-009**: el (único) formulario activo del motivo de la baja.
- **RN-010.2** Solo `Published`/activo; si el motivo **no tiene** formulario → respuesta clara "sin formulario" (no excepción; la entrevista es **opcional** — D-05).
- **RN-010.3** Devuelve la **versión exacta** (para el snapshot de RF-012).
- **RN-010.4** Una sola lectura, **sin N+1**.

**Criterios de aceptación (Gherkin):**
- *Con form:* **Dado** un empleado cuyo motivo tiene un formulario activo, **cuando** consulto el aplicable, **entonces** recibo la definición + la versión.
- *Sin form:* **Dado** un motivo sin formulario activo, **cuando** consulto, **entonces** indicador "sin formulario" (no 500).
- *Autoservicio:* **Dado** el empleado, **cuando** consulta **el suyo**, **entonces** se permite; **sobre otro** → 403 (RF-013).

**Prioridad:** Alta · **Fase 1 (PR-5)** · **Dependencias:** RF-009.

### RF-011 — Llenar el formulario (autoservicio + RRHH) con guardado parcial
**Descripción:** Crear/editar la **submission** del empleado que se retira; **guardar parcial** (`Draft`) y **reanudar**; **enviar** (`Submitted`). Soporta **autoservicio** (empleado) y **captura por RRHH** (entrevista presencial).

**API (referencial):** `POST …/personnel-files/{id}/exit-interview/submission` · `PUT …/submission/{sid}` (If-Match) · `POST …/submission/{sid}/submit`.

**Reglas de negocio:**
- **RN-011.1** **Autoservicio:** el empleado solo sobre **su propio** expediente (`LinkedUserPublicId == caller`); RRHH (`ManageExitInterviews`) sobre cualquiera (D-04).
- **RN-011.2** **Guardado parcial** permitido **sin** validar obligatorios; el **envío** valida los obligatorios.
- **RN-011.3** La submission referencia la **versión** resuelta en RF-010 (snapshot).
- **RN-011.4** **Una** submission "activa" por empleado+baja; reintentos editan el `Draft`.
- **RN-011.5** **Validación por tipo** del valor: número en rango, fecha válida, opción(es) dentro de las definidas, longitud de texto.
- **RN-011.6** **Anonimato a nivel de submission** (D-06): si el **formulario** es anónimo (RF-003), la submission se persiste **disociada** del expediente (RF-012); si no, queda **vinculada** al empleado.

**Criterios de aceptación (Gherkin):**
- *Parcial/reanudar:* **Dado** el empleado, **cuando** guardo parcial sin completar todo, **entonces** se persiste `Draft` y puedo **reanudar** después.
- *Obligatorios al enviar:* **Dado** un campo obligatorio vacío, **cuando** envío, **entonces** error que **lista** los campos faltantes.
- *Valor inválido:* **Dado** un número fuera de rango o una opción inexistente, **cuando** guardo, **entonces** error de validación de respuesta.
- *Acceso indebido:* **Dado** un tercero no autorizado, **cuando** intenta llenar el de otro, **entonces** 403.
- *Captura RRHH:* **Dado** RRHH con `ManageExitInterviews`, **cuando** captura la submission de un empleado, **entonces** se permite (y, si el formulario **no** es anónimo, se registra **quién** capturó).

**Prioridad:** Alta · **Fase 1 (PR-5)** · **Dependencias:** RF-010, RF-013.

### RF-012 — Persistencia de respuestas: snapshot + anonimato + score derivado
**Descripción:** Persistir la **submission** y cada **respuesta** (`ExitInterviewAnswer`) con el valor según el tipo, el **snapshot** de la definición (FieldKey, título, peso, versión) y un **score ponderado derivado** (índice 0–100); aplicar el **anonimato a nivel de submission** (D-06) y el **archivado en rehire** (D-12).

**Reglas de negocio:**
- **RN-012.1** **Snapshot** de la definición en la submission/respuestas: independiente del estado vivo del formulario.
- **RN-012.2** **Anonimato a nivel de submission** (D-06): si el formulario es **anónimo**, la submission **no** guarda FK ni referencia al expediente/empleado; conserva solo **dimensiones de-identificadas** para analítica (`RetirementReasonCode`, `RetirementCategoryCode`, `SeparationType`, **plaza** (`PositionSlot`) a la fecha de baja — RQ-02, **periodo** = año-mes de la baja). Si **no** es anónimo, la submission **se vincula** al expediente.
- **RN-012.3** `TotalScore` **derivado** (no editable) — ver **"Modelo de puntaje"** abajo (D-07).
- **RN-012.4** **Inmutable** tras `Submitted`, salvo **corrección por RRHH** (RF-020) con auditoría.
- **RN-012.5** Valor según control: `ValueText`/`ValueNumber`/`ValueDate`/`ValueBool`/`SelectedOptionCodes`.
- **RN-012.6** **Rehire (D-12):** al recontratar al empleado (`Rehire/RehireEmployee.cs` limpia la baja), las submissions del **periodo previo** pasan a estado **`Archived`** (no se borran, no quedan activas).

**Modelo de puntaje (definido por el analista — D-07):**
- **Puntaje de opción (`Score`):** se recomienda escala **0–100** (p.ej. *Muy satisfecho*=100 … *Muy insatisfecho*=0). Lo define RRHH por opción (RF-006).
- **Escala Likert (1..n):** se normaliza a 0–100 → `(valor − 1) / (n − 1) × 100`.
- **Peso de campo (`Weight`):** ≥ 0 (recomendado 1–10); pondera la importancia del campo.
- **Campos puntuables:** solo **selección** (con `Score`) y **escala**. Texto/fecha/número libre **no** puntúan (no entran en el índice).
- **Índice de la submission (0–100):** promedio ponderado = `Σ(Weight_i × scoreNorm_i) / Σ(Weight_i)` sobre los campos puntuables **respondidos**. Si `Σ(Weight)=0` o no hay campos puntuables → índice `null`.
- **Interpretación:** índice **0–100** de la experiencia de salida (mayor = más favorable); permite **rankear motivos/áreas por score promedio** en la tabulación (RF-014, D-10).

**Criterios de aceptación (Gherkin):**
- *Snapshot:* **Dado** que el formulario **cambia** tras el envío, **cuando** consulto la submission, **entonces** refleja la definición **original**.
- *Score determinista:* **Dado** un campo escala (peso 2) respondido en 4/5 y un campo selección (peso 1) con opción de `Score=100`, **cuando** se envía, **entonces** índice = `(2×75 + 1×100)/3 = 83.3`.
- *Submission anónima:* **Dado** un formulario anónimo, **cuando** el empleado envía, **entonces** la submission **no** tiene FK al expediente, pero **sí** conserva motivo/categoría/tipo/área/periodo para tabular.
- *Rehire archiva:* **Dado** una submission de un periodo, **cuando** el empleado es recontratado, **entonces** la submission queda **`Archived`** y no figura como activa.

**Prioridad:** Alta · **Fase 1 (PR-5)** · **Dependencias:** RF-011, `Rehire/RehireEmployee.cs`.

### RF-019 — Consultar respuestas (submissions): listado y detalle
**Descripción:** Listar y ver el **detalle** de submissions (por formulario, empleado, motivo, periodo); el detalle muestra las respuestas con su **snapshot**; **respeta el anonimato**.

**API (referencial):** `GET …/exit-interviews?formId=&reasonCode=&from=&to=` · `GET …/exit-interviews/{sid}`.

**Reglas de negocio:**
- **RN-019.1** Lectura de respuestas: **solo RRHH** (`ViewExitInterviews`) — **D-14**; **no** jefatura/área. El **empleado** puede ver/retomar **su propio borrador** antes de enviar, pero **no** consulta respuestas enviadas.
- **RN-019.2** Las submissions de un **formulario anónimo** se muestran **sin atribución** al empleado (no re-identificables).
- **RN-019.3** Todas las respuestas (incluido texto libre) son **sensibles**; el acceso queda restringido a **RRHH** (D-14).

**Criterios de aceptación (Gherkin):**
- **Dado** `ViewExitInterviews` (RRHH), **cuando** listo por periodo/motivo, **entonces** obtengo las submissions con estado/motivo/fecha.
- **Dado** una submission anónima, **cuando** la consulto, **entonces** veo los valores **sin** vincularlos a la identidad.
- **Dado** una jefatura/área (sin `ViewExitInterviews`), **cuando** intenta consultar respuestas, **entonces** 403 (D-14).

**Prioridad:** Media · **Fase 1** · **Dependencias:** RF-012, RF-013.

### RF-020 — Corrección / anulación de una submission por RRHH
**Descripción:** RRHH (`ManageExitInterviews`) puede **corregir** o **anular** (soft) una submission ya enviada, con auditoría del antes/después; el **empleado no edita** tras enviar.

**Reglas de negocio:** **RN-020.1** Solo `ManageExitInterviews`; **RN-020.2** Recalcula `TotalScore`; **RN-020.3** **Audita** before/after; **RN-020.4** No expone submissions anónimas de forma re-identificable.

**Criterios de aceptación:** **Dado** una submission `Submitted`, **cuando** RRHH corrige un valor, **entonces** se audita el cambio y se **recalcula** el score; **cuando** la anula, **entonces** cambia de estado y deja de contar en la tabulación.

**Prioridad:** Baja · **Fase 2** · **Dependencias:** RF-012.

### ~~RF-021 — Adjuntos de la entrevista~~ — **DESCARTADO (D-09)**
**Estado:** **Fuera de alcance.** El negocio confirmó que **no se requieren adjuntos** (Q-09). No se crea `FilePurpose.ExitInterviewDocument` ni entidad de documentos para este módulo. Se documenta aquí solo para trazabilidad de la decisión; podría reconsiderarse en una fase futura si el negocio lo solicita.

---

### Grupo E — Búsqueda, seguridad y analítica

### RF-018 — Listar / buscar formularios
**Descripción:** Listado **paginado** con filtros (estado, motivo asociado, texto en nombre), orden y **conteo de submissions**.

**Reglas de negocio:** **RN-018.1** Tenant-scoped; solo no-borrados; requiere `View`; **RN-018.2** Muestra versión/estado y nº de submissions.

**Criterios de aceptación:** **Dado** varios formularios, **cuando** filtro por estado `Published` y texto, **entonces** obtengo la página filtrada y ordenada con su conteo de submissions.

**Prioridad:** Media · **Fase 1** · **Dependencias:** RF-003.

### RF-013 — Permisos, autoservicio y controlador dedicado
**Descripción:** Crear permisos `ManageExitInterviewForms` (construir formularios + catálogos de motivo), `ViewExitInterviews` / `ManageExitInterviews` (respuestas) y habilitar **autoservicio** del empleado. Exponer en **controlador(es) dedicado(s)** porque `AuthorizationPolicySet` es **solo a nivel de clase**.

**Reglas de negocio:**
- **RN-013.1** Construir/publicar/asociar/CRUD de catálogos → `ManageExitInterviewForms`.
- **RN-013.2** Leer definiciones para gestión → `View` (o `Manage` de forms).
- **RN-013.3** **Llenar** → `ManageExitInterviews` **o** dueño del expediente (self-service) — D-04.
- **RN-013.4** **Leer respuestas** → `ViewExitInterviews` (**solo RRHH** — D-14; **no** jefatura/área).
- **RN-013.5** Accesos indebidos → **403**; cross-tenant → 403/404.
- **RN-013.6** Alta de permisos en RBAC/Provisioning + **pruebas de gobernanza**.

**Criterios de aceptación (Gherkin):**
- *Sin permiso:* **Dado** un usuario sin permisos, **cuando** accede a cualquier endpoint, **entonces** 403.
- *Split por verbo:* **Dado** la política de clase, **cuando** hace `GET` vs `POST`, **entonces** se evalúan `View` vs `Manage` respectivamente.
- *Self-service:* **Dado** el empleado sin `Manage`, **cuando** llena **el suyo**, **entonces** se permite vía self-service.
- *Gobernanza:* la suite reconoce `ManageExitInterviewForms`/`ViewExitInterviews`/`ManageExitInterviews`.

**Prioridad:** Alta · **Fase 1 (PR-6)** · **Dependencias:** `IdentityAccess`/Provisioning, `PersonnelFilePolicies`.

### RF-014 — Tabulación de causas de rotación (Fase 2)
**Descripción:** Reportes que **tabulan** las razones de retiro: **conteos** por motivo/categoría/periodo/área/plaza, **distribución**, **score ponderado** promedio y **tendencia**; con **exportación** (CSV/Excel). Es el **valor de negocio** central (corregir falencias que causan rotación).

**Reglas de negocio:**
- **RN-014.1** Solo datos `Submitted`.
- **RN-014.2** **Respeta anonimato:** agrega **sin re-identificar**; umbral mínimo de agregación **k ≥ 5** (RQ-04) para no exponer submissions anónimas con muestras muy pequeñas.
- **RN-014.3** Filtros por rango de fechas, **plaza** (área — RQ-02), categoría y motivo.

**Criterios de aceptación (Gherkin):**
- **Dado** un periodo, **cuando** consulto el reporte, **entonces** obtengo el **ranking** de motivos y su **peso/score**, y la tendencia.
- **Dado** un grupo anónimo con muestra menor al umbral, **cuando** consulto, **entonces** se **suprime/agrega** para no re-identificar.
- **Dado** un reporte, **cuando** exporto, **entonces** obtengo CSV/Excel coherente con la vista.

**Prioridad:** Media · **Fase 2** · **Dependencias:** RF-012.

---

### Mapa de requerimientos por fase

| Fase | Requerimientos | Objetivo |
|---|---|---|
| **Fase 1 — MVP (constructor + captación)** | RF-001, RF-002, RF-003, RF-004, RF-005, RF-006, RF-007, RF-008, RF-009, RF-010, RF-011, RF-012, RF-013, RF-016, RF-018, RF-019 (lectura RRHH) (+ RF-015, RF-017 según capacidad) | Catálogos + construir/publicar/asociar + llenar/guardar respuestas + lectura RRHH + permisos. |
| **Fase 2 — Tabulación/analítica** | RF-014, RF-020 | Tabular causas de rotación + corrección/anulación de respuestas. |
| **Fase 3 — Evoluciones (no comprometidas)** | lógica condicional, notificaciones | Automatizaciones. **Descartado:** generalización del motor (D-01), obligatoriedad antes de la baja (D-05), adjuntos (D-09). |

---

## 7. Requerimientos no funcionales

- **Seguridad / Privacidad:** **Anonimato a nivel de formulario/submission** (D-06) — si el formulario es anónimo, **toda** la submission se **disocia** del empleado (sin FK al expediente; no re-identificable vía la submission ni la auditoría), conservando solo dimensiones de-identificadas para analítica; tratamiento conforme a la **normativa de protección de datos personales** aplicable en El Salvador. **Lectura de respuestas: solo RRHH** (D-14). Permisos dedicados; 403 ante accesos indebidos; autoservicio limitado al propio expediente.
- **Auditoría:** Auditar **cambios** en definiciones (crear/editar/publicar/versionar/archivar) y en respuestas (sin re-identificar submissions anónimas); patrón de `IAuditService` con before/after.
- **Rendimiento:** La resolución del formulario aplicable y el guardado de respuestas deben responder en tiempos interactivos; la definición se entrega en una sola lectura (sin N+1).
- **Disponibilidad / Escalabilidad:** Multi-tenant; los formularios y respuestas escalan por compañía; catálogos cacheables.
- **Usabilidad:** El constructor debe permitir reordenar grupos/campos y previsualizar; mensajes de error claros y **bilingües (ES/EN)** con paridad garantizada por test.
- **Compatibilidad / Mantenibilidad:** CQRS + FluentValidation + módulo de reglas puro; convención de catálogos país-scoped + lookup por key; migraciones con timestamp (`DOTNET_ROLL_FORWARD=Major` para `dotnet ef 9.0.x`).
- **Concurrencia:** `ConcurrencyToken` + `If-Match`/ETag en formularios, campos y respuestas.
- **Accesibilidad:** Los tipos de control y sus etiquetas deben permitir render accesible (labels, descripciones, requerido) en el frontend.

---

## 8. Historias de usuario

> Cada historia incluye criterios en formato **Gherkin** (Dado/Cuando/Entonces) con el **camino feliz** y al menos un **escenario alternativo o de excepción**. Trazabilidad al RF correspondiente entre paréntesis.

### Épica A — Diseño del formulario (RRHH / diseñador)

#### HU-001 — Crear un formulario de entrevista de retiro (RF-003)
Como **RRHH (diseñador)**, quiero **crear un formulario con nombre, descripción del uso y la marca de anonimato (Sí/No) a nivel de formulario**, para **tener la base sobre la cual construir la entrevista de salida y decidir si será anónima** (D-06).
**Criterios:**
- *Feliz:* **Dado** que tengo `ManageExitInterviewForms`, **cuando** creo un formulario con un nombre único, **entonces** queda en estado `Draft` v1 y obtengo su identificador y token de concurrencia.
- *Anónimo:* **Dado** que marco el formulario como **anónimo**, **cuando** lo publico, **entonces** todas sus submissions se guardarán **sin** atribución al empleado (D-06).
- *Duplicado:* **Dado** un nombre ya usado en mi compañía (ignorando mayúsculas/espacios), **cuando** intento crearlo, **entonces** recibo `EXIT_INTERVIEW_FORM_NAME_DUPLICATE`.
- *Sin permiso:* **Dado** un usuario sin `ManageExitInterviewForms`, **cuando** intenta crear, **entonces** recibe 403.

#### HU-002 — Organizar el formulario en grupos y reordenarlos (RF-004)
Como **RRHH (diseñador)**, quiero **crear grupos (secciones) y ordenarlos**, para **estructurar visualmente la entrevista** (p.ej. "Motivos de salida", "Clima laboral", "Sugerencias").
**Criterios:**
- *Feliz:* **Dado** un formulario en `Draft`, **cuando** agrego varios grupos y los reordeno, **entonces** se listan en el orden definido.
- *Eliminación segura:* **Dado** un grupo con campos, **cuando** lo elimino, **entonces** sus campos **no** se borran: quedan sin grupo.
- *Bloqueo por estado:* **Dado** un formulario `Published`, **cuando** intento editar grupos, **entonces** se me exige crear una nueva versión.

#### HU-003 — Configurar un campo con sus atributos (RF-005)
Como **RRHH (diseñador)**, quiero **definir por campo el tipo de control, nombre, título, descripción, peso y obligatoriedad**, para **controlar exactamente qué y cómo se pregunta**. *(El anonimato es del formulario, no del campo — D-06, ver HU-001.)*
**Criterios:**
- *Feliz:* **Dado** un grupo, **cuando** agrego un campo con su tipo de control y atributos, **entonces** se persisten todos (tipo, clave, título, descripción, peso, obligatorio, orden, grupo).
- *Peso inválido:* **Dado** un campo, **cuando** asigno un peso negativo, **entonces** recibo `EXIT_INTERVIEW_FIELD_WEIGHT_INVALID`.
- *Clave duplicada:* **Dado** un campo existente, **cuando** creo otro con el mismo nombre/clave, **entonces** recibo `EXIT_INTERVIEW_FIELD_KEY_DUPLICATE`.
- *Rango inválido:* **Dado** un campo numérico, **cuando** defino mínimo mayor que máximo, **entonces** recibo error de rango.

#### HU-004 — Definir opciones con puntaje para campos de selección (RF-006)
Como **RRHH (diseñador)**, quiero **definir las opciones (y su puntaje) de listas/selección**, para **estandarizar respuestas y habilitar la tabulación ponderada**.
**Criterios:**
- *Feliz:* **Dado** un campo de lista desplegable, **cuando** agrego opciones con etiqueta, orden y puntaje, **entonces** se guardan y se devuelven en orden.
- *Selección sin opciones:* **Dado** un campo de selección sin opciones, **cuando** intento publicar, **entonces** recibo `EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED`.
- *Tipo no soportado:* **Dado** un campo numérico, **cuando** intento agregar opciones, **entonces** recibo un error (no soportado).

#### HU-005 — Previsualizar el formulario antes de publicar (RF-016)
Como **RRHH (diseñador)**, quiero **previsualizar el formulario tal como lo verá el empleado**, para **validar la experiencia antes de publicarlo**.
**Criterios:**
- *Feliz:* **Dado** un formulario en `Draft`, **cuando** solicito el preview, **entonces** veo grupos, campos y opciones en orden.
- *Sin efectos:* **Dado** que previsualizo, **cuando** salgo, **entonces** **no** se creó ninguna submission ni se alteró el formulario.

#### HU-006 — Validar y publicar el formulario (RF-007, RF-008)
Como **RRHH (diseñador)**, quiero **publicar el formulario solo si es coherente**, para **evitar formularios mal construidos en producción**.
**Criterios:**
- *Inelegible:* **Dado** un formulario con incoherencias, **cuando** intento publicar, **entonces** recibo la **lista** de errores a corregir.
- *Feliz:* **Dado** un formulario válido (≥ 1 campo, claves únicas, opciones donde corresponde), **cuando** publico, **entonces** pasa a `Published` y su estructura queda bloqueada.

#### HU-007 — Versionar un formulario publicado (RF-008)
Como **RRHH (diseñador)**, quiero **modificar un formulario ya publicado creando una nueva versión**, para **mejorarlo sin afectar las respuestas históricas**.
**Criterios:**
- *Feliz:* **Dado** un formulario `Published` v1, **cuando** edito un campo, **entonces** se crea v2 en `Draft` y v1 permanece intacta y consultable.
- *Integridad histórica:* **Dado** submissions contra v1, **cuando** publico v2, **entonces** las submissions de v1 siguen reflejando su definición original.

#### HU-008 — Asociar el formulario a un (1) motivo de retiro (RF-009)
Como **RRHH (diseñador)**, quiero **asociar el formulario a un único motivo de retiro (con un solo formulario activo por motivo)**, para **que se presente el formulario correcto según la causa de salida** (D-03).
**Criterios:**
- *1 motivo:* **Dado** un formulario `Published`, **cuando** lo asocio, **entonces** queda ligado a **un** motivo (no a varios ni a una categoría).
- *Single-active:* **Dado** un motivo con el formulario A activo, **cuando** activo el B para el mismo motivo, **entonces** A se desactiva y B queda activo.
- *Estado inválido:* **Dado** un formulario en `Draft`, **cuando** intento asociarlo, **entonces** recibo un error.

#### HU-009 — Administrar el catálogo de motivos de retiro (RF-001, RF-015)
Como **RRHH (administrador)**, quiero **crear/editar/activar/inactivar categorías y motivos de retiro por país**, para **mantener el catálogo alineado a la realidad de la institución**.
**Criterios:**
- *Feliz:* **Dado** el seed SV, **cuando** agrego un motivo bajo una categoría activa, **entonces** queda disponible para asociación y para registrar bajas.
- *Integridad:* **Dado** una categoría con motivos activos, **cuando** intento inactivarla, **entonces** recibo `RETIREMENT_CATEGORY_HAS_ACTIVE_REASONS`.
- *No borrado en uso:* **Dado** un motivo usado por bajas/submissions, **cuando** intento borrarlo, **entonces** se me ofrece inactivarlo.

#### HU-010 — Duplicar un formulario como plantilla (RF-017)
Como **RRHH (diseñador)**, quiero **clonar un formulario existente**, para **reutilizar su estructura sin partir de cero**.
**Criterios:**
- *Feliz:* **Dado** un formulario `Published`, **cuando** lo clono con un nombre nuevo, **entonces** obtengo un `Draft` idéntico en estructura, **sin** asociaciones ni submissions.
- *Independencia:* **Dado** el clon, **cuando** lo edito, **entonces** el original no cambia.

#### HU-011 — Listar y buscar formularios (RF-018)
Como **RRHH**, quiero **listar y filtrar los formularios (por estado, motivo, texto)**, para **encontrar y gestionar el que necesito**.
**Criterios:**
- *Feliz:* **Dado** varios formularios, **cuando** filtro por estado `Published` y un texto, **entonces** obtengo la página filtrada con su versión y conteo de submissions.

### Épica B — Llenado de la entrevista (empleado que se retira)

#### HU-012 — Abrir el formulario aplicable a mi salida (RF-010)
Como **empleado que se retira**, quiero **acceder al formulario que corresponde a mi motivo de salida**, para **completar mi entrevista de retiro**.
**Criterios:**
- *Feliz:* **Dado** que mi baja tiene un motivo con formulario asociado, **cuando** abro mi entrevista, **entonces** recibo el formulario aplicable listo para llenar.
- *Sin formulario:* **Dado** un motivo sin formulario activo, **cuando** abro mi entrevista, **entonces** se me informa que **no hay formulario** (sin error técnico; es opcional).
- *Aislamiento:* **Dado** otro empleado, **cuando** intento abrir su entrevista, **entonces** recibo 403.

#### HU-013 — Guardar parcial y reanudar (RF-011)
Como **empleado que se retira**, quiero **guardar mi avance y continuar después**, para **no perder lo escrito si no termino de una sola vez**.
**Criterios:**
- *Feliz:* **Dado** que lleno parte del formulario, **cuando** guardo, **entonces** se conserva como borrador y puedo reanudar.
- *Sin validar obligatorios:* **Dado** un guardado parcial, **cuando** guardo sin completar obligatorios, **entonces** **se permite** (la validación de obligatorios ocurre al enviar).

#### HU-014 — Responder en un formulario anónimo (RF-011, RF-012, D-06)
Como **empleado que se retira**, quiero **que, cuando el formulario sea anónimo, mi entrevista completa no quede atribuida a mí**, para **expresarme con honestidad sin temor a represalias**.
**Criterios:**
- *Disociación de la submission:* **Dado** un formulario **anónimo**, **cuando** lo completo y envío, **entonces** mi submission se almacena **sin** FK ni vínculo a mi identidad (solo dimensiones de-identificadas para analítica).
- *No re-identificable:* **Dado** una submission anónima, **cuando** RRHH/auditoría la consulta, **entonces** **no** puede atribuírmela.
- *No anónimo:* **Dado** un formulario **no** anónimo, **cuando** envío, **entonces** mi submission **sí** queda vinculada a mi expediente (visible para RRHH).

#### HU-015 — Enviar mi entrevista de retiro (RF-011)
Como **empleado que se retira**, quiero **enviar mi entrevista completa**, para **dejar registrados mis motivos de salida**.
**Criterios:**
- *Feliz:* **Dado** que completé los campos obligatorios, **cuando** envío, **entonces** mi submission queda `Submitted` con su snapshot y score.
- *Faltan obligatorios:* **Dado** un campo obligatorio vacío, **cuando** envío, **entonces** recibo la **lista** de campos faltantes y no se envía.
- *Valor inválido:* **Dado** un valor fuera de rango o una opción inexistente, **cuando** envío, **entonces** recibo un error de validación del valor.

### Épica C — Operación y análisis (RRHH / Gerencia)

#### HU-016 — Capturar la entrevista en nombre del empleado (RF-011)
Como **entrevistador de RRHH**, quiero **capturar las respuestas durante una entrevista presencial**, para **registrar salidas cuando no hay autoservicio**.
**Criterios:**
- *Feliz:* **Dado** `ManageExitInterviews`, **cuando** capturo y envío la submission de un empleado, **entonces** se guarda con su snapshot, score y el registro de quién capturó.

#### HU-017 — Consultar las respuestas (solo RRHH) respetando el anonimato (RF-019)
Como **RRHH**, quiero **consultar las submissions y su detalle**, para **analizar casos puntuales de salida**. *(Solo RRHH; jefatura/área no accede — D-14.)*
**Criterios:**
- *Feliz:* **Dado** `ViewExitInterviews` (RRHH), **cuando** listo por periodo/motivo, **entonces** obtengo las submissions con estado, motivo y fecha.
- *Anonimato:* **Dado** una submission de un formulario **anónimo**, **cuando** veo el detalle, **entonces** los valores aparecen **sin** atribución al empleado.
- *Sin permiso:* **Dado** una jefatura/área sin `ViewExitInterviews`, **cuando** consulta, **entonces** recibe 403 (D-14).

#### HU-018 — Corregir o anular una submission (RF-020)
Como **RRHH (gestor)**, quiero **corregir o anular una submission enviada con auditoría**, para **subsanar errores de captura** sin alterar el histórico de forma opaca.
**Criterios:**
- *Corrección:* **Dado** una submission `Submitted`, **cuando** corrijo un valor, **entonces** se audita el antes/después y se **recalcula** el score.
- *Anulación:* **Dado** una submission, **cuando** la anulo, **entonces** cambia de estado y **deja de contar** en la tabulación.

#### HU-019 — Tabular las causas de rotación (RF-014, Fase 2)
Como **gerente/analista de RRHH**, quiero **ver las causas de retiro tabuladas y ponderadas por periodo/área/motivo**, para **priorizar acciones que reduzcan la rotación**.
**Criterios:**
- *Feliz:* **Dado** un periodo, **cuando** consulto el reporte, **entonces** obtengo el ranking de motivos, su peso/score y la tendencia.
- *Privacidad:* **Dado** un grupo anónimo con muestra muy pequeña, **cuando** consulto, **entonces** el dato se **agrega/suprime** para no re-identificar.
- *Exportación:* **Dado** un reporte, **cuando** exporto, **entonces** obtengo CSV/Excel coherente con la vista.

### Épica D — Transversales (seguridad / integridad)

#### HU-020 — Bloquear accesos indebidos (RF-013)
Como **responsable de seguridad/Cumplimiento**, quiero **que cada acción exija el permiso correcto y que el empleado solo acceda a lo suyo**, para **proteger datos sensibles de salida**.
**Criterios:**
- *Permisos:* **Dado** un usuario sin el permiso requerido, **cuando** invoca cualquier endpoint, **entonces** recibe 403.
- *Aislamiento de tenant:* **Dado** un recurso de otra compañía, **cuando** lo solicito, **entonces** recibo 403/404.

#### HU-021 — Mantener históricos íntegros ante cambios del formulario (RF-008, RF-012)
Como **sistema/Cumplimiento**, quiero **que las respuestas conserven la definición con la que se respondieron**, para **garantizar trazabilidad aunque el formulario evolucione**.
**Criterios:**
- *Snapshot:* **Dado** que se publica una nueva versión, **cuando** consulto una submission antigua, **entonces** refleja la definición y los pesos **originales**.
- *Concurrencia:* **Dado** dos ediciones simultáneas del mismo recurso, **cuando** la segunda usa un token desactualizado, **entonces** recibe 409.

---

## 9. Reglas de negocio

- **RN-01.** El **motivo de retiro** debe existir como **catálogo** jerárquico (Categoría con `SeparationType` → Motivo), país-scoped, validado por código. (RF-001, D-02)
- **RN-02.** El **tipo de control** proviene de un **catálogo de sistema** cerrado; determina valor, opciones y rango admisibles. (RF-002, RF-007)
- **RN-03.** El **nombre del formulario** es único por tenant (normalizado); el **anonimato** es una propiedad **del formulario** (Sí/No). (RF-003, D-06)
- **RN-04.** Cada **campo** lleva: tipo de control, nombre/clave (único en el formulario), título, descripción, peso (≥0), obligatorio, orden. *(El anonimato NO es de campo — D-06.)* (RF-005)
- **RN-05.** Los campos de **selección** requieren **al menos una opción**. (RF-006)
- **RN-06.** Un formulario solo se **publica** si pasa la validación de coherencia. (RF-007/008)
- **RN-07.** Tras publicar, la estructura **se bloquea**; los cambios crean **nueva versión**; las respuestas referencian la versión y guardan **snapshot**. (RF-008, RF-012)
- **RN-08.** Un formulario se asocia a **un (1) motivo**; **a lo sumo un formulario activo por motivo** (sin formulario por defecto). (RF-009, D-03)
- **RN-09.** El **empleado** solo puede llenar la entrevista de **su propio** expediente (autoservicio); RRHH (Manage), cualquiera. La **lectura de respuestas es solo RRHH**. (RF-011/013, D-04/D-14)
- **RN-10.** Los campos **obligatorios** deben responderse para **enviar**. (RF-011)
- **RN-11.** Si el **formulario es anónimo**, **toda la submission** se almacena **disociada** del expediente (sin FK; solo dimensiones de-identificadas). (RF-012, D-06)
- **RN-12.** El **score** de la submission es un **índice 0–100 derivado** (peso × puntaje normalizado); no se captura manualmente. (RF-012, D-07)
- **RN-13.** No se **borra** un formulario con respuestas; se **archiva**. (RF-008)
- **RN-14.** Tras un **rehire**, las submissions del periodo previo se **archivan**. (RF-012, D-12)
- **RN-15.** No hay **migración** de motivos legados (texto libre): se **eliminan** (datos de prueba). (D-11)
- **RN-16.** Toda escritura es **auditada**, con **concurrencia optimista** y **multi-tenant**. (RNF)
- **RN-17.** Mensajes/errores **bilingües (ES/EN)** con paridad garantizada por test. (RNF)

---

## 10. Flujos principales

### Flujo A — Construcción y publicación del formulario (RRHH)
1. RRHH crea el formulario (nombre, descripción, **anónimo Sí/No**) en estado `Draft`.
2. Agrega **grupos** (secciones) y los ordena.
3. Agrega **campos** (tipo de control, título, descripción, peso, obligatorio, orden) y, si aplica, **opciones** con puntaje.
4. El sistema **valida la coherencia** (RF-007) en cada cambio.
5. RRHH **publica** el formulario (queda bloqueado, versión N).
6. RRHH **asocia** el formulario a **un (1) motivo de retiro** (single-active por motivo).

### Flujo B — Llenado de la entrevista (empleado en autoservicio — opcional)
1. Se determina el **motivo de retiro** del empleado (catálogo).
2. El sistema **resuelve el formulario aplicable** (RF-010) y lo entrega para render. *(Si el motivo no tiene formulario, no hay entrevista — es opcional.)*
3. El empleado **completa** los campos; puede **guardar parcial** y reanudar.
4. El empleado **envía**; el sistema valida obligatorios.
5. El sistema persiste la **submission** + **respuestas** con **snapshot** y **score** (índice 0–100) derivado; si el formulario es **anónimo**, la submission se guarda **disociada** del expediente.
6. Se registra auditoría (respetando el anonimato).

### Flujo C — Captura por RRHH (entrevista presencial)
1. RRHH abre el expediente del empleado que se retira.
2. Resuelve el formulario aplicable y **captura** las respuestas en su nombre (Manage).
3. Envía y se persiste igual que en el Flujo B.

### Flujo D — Tabulación (Fase 2, RRHH/Gerencia)
1. Se consulta el reporte por **motivo, categoría, área, periodo y score** (D-10).
2. El sistema agrega conteos y **score ponderado** sin re-identificar submissions anónimas.
3. Se **exporta** el resultado (CSV/Excel).

---

## 11. Flujos alternativos y excepciones

- **E-01.** Motivo de retiro **inválido/inactivo** al registrar la baja → error bilingüe (RF-001).
- **E-02.** Publicar un formulario que **no pasa** la validación (campo de selección sin opciones, peso negativo, clave duplicada) → bloqueado con error específico (RF-007/008).
- **E-03.** Activar un **segundo** formulario para un motivo que ya tiene uno activo → **desactiva el anterior** (single-active, D-03).
- **E-04.** Empleado intenta llenar la entrevista de **otro** expediente → **403** (RF-013).
- **E-05.** Falta un **campo obligatorio** al enviar → bloqueado con la lista de faltantes (RF-011).
- **E-06.** **Conflicto de concurrencia** al editar formulario/campo/respuesta → 409 vía `If-Match` (RNF).
- **E-07.** Intento de **editar la estructura** de un formulario `Published` → se exige crear **nueva versión** (RF-008).
- **E-08.** Intento de **borrar** un formulario con respuestas → se ofrece **archivar** (RN-13).
- **E-09.** **Rehire** del empleado: la baja anterior se **limpia** (perfil) y las submissions del periodo previo se **archivan** (D-12, RN-14).
- **E-10.** **Motivo sin formulario asociado:** al abrir la entrevista no hay formulario → se informa "sin formulario" (la entrevista es **opcional**, D-05).
- **E-11.** **Jefatura/área** intenta leer respuestas → **403** (solo RRHH, D-14).

---

## 12. Datos requeridos

> Notas: catálogos país-scoped heredan de `CountryScopedCatalogItem` (Id, PublicId, CountryCatalogItemId, CountryCode, Code, NormalizedCode, Name, NormalizedName, IsActive, SortOrder, ConcurrencyToken, CreatedUtc, ModifiedUtc). Las entidades del formulario son **tenant-scoped** (`TenantEntity`).

### Entidad: RetirementCategoryCatalogItem (catálogo, país-scoped) — NUEVO
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Code | Texto(80) | Sí | Único por país; upper | Código de la categoría de retiro (p.ej. `RENUNCIA_VOLUNTARIA`). |
| Name | Texto(200) | Sí | — | Nombre visible (p.ej. "Renuncia voluntaria"). |
| SeparationType | Enum/Texto | Sí | `VOLUNTARIA`/`INVOLUNTARIA`/`OTRA` | **Clasificación** para *roll-up* de reportes (D-02). |
| (base país-scoped) | — | — | — | CountryCode, IsActive, SortOrder, etc. |

### Entidad: RetirementReasonCatalogItem (catálogo, país-scoped, jerárquico) — NUEVO
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| RetirementCategoryCatalogItemId | FK | Sí | Categoría existente y activa | Categoría padre. |
| Code | Texto(80) | Sí | Único `(país, categoría, código)` | Código del motivo (p.ej. `MEJOR_OFERTA`). |
| Name | Texto(200) | Sí | — | Nombre visible del motivo. |

### Entidad: FormControlTypeCatalogItem (catálogo de sistema) — NUEVO
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Code | Texto(40) | Sí | Único; del set cerrado | Tipo de control (`LISTA_DESPLEGABLE`, `CASILLA`, …). |
| Name | Texto(120) | Sí | — | Nombre visible/localizable. |
| ValueKind | Enum/Texto | Sí | texto/número/fecha/booleano/opciones | Tipo de valor que captura. |
| SupportsOptions | Booleano | Sí | — | Si admite opciones (lista/radio/múltiple). |
| SupportsRange | Booleano | Sí | — | Si admite min/máx (número/escala). |

### Entidad: ExitInterviewForm (tenant-scoped) — NUEVO
> Módulo **exclusivo** (D-01): **no** hay discriminador `FormType`. La asociación al motivo es **1:1** (D-03), por eso vive en el propio formulario (sin tabla de unión).

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | Guid | Sí | — | Referencia externa. |
| Name | Texto(200) | Sí | Único por tenant (normalizado) | Nombre del formulario. |
| Description | Texto(1000) | No | — | Descripción del uso. |
| IsAnonymous | Booleano | Sí | Fijo al publicar | **Anónimo Sí/No a nivel de formulario** (D-06). |
| RetirementReasonCode | Texto(80) | No | Catálogo válido (RF-001); **un (1)** motivo | Motivo asociado (D-03). |
| IsActiveForReason | Booleano | Sí | **Único activo por motivo** | Formulario vigente para ese motivo (D-03). |
| Status | Enum | Sí | Draft/Published/Archived | Estado del ciclo de vida. |
| Version | Entero | Sí | ≥ 1 | Versión publicada. |
| IsActive | Booleano | Sí | — | Soft-delete. |
| ConcurrencyToken | Guid | Sí | — | Concurrencia optimista. |

### Entidad: ExitInterviewFormGroup (tenant-scoped) — NUEVO
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| FormId | FK | Sí | — | Formulario. |
| Name / Title | Texto(200) | Sí | — | Nombre/título de la sección. |
| Description | Texto(1000) | No | — | Descripción del grupo. |
| DisplayOrder | Entero | Sí | ≥ 0 | Orden. |

### Entidad: ExitInterviewFormField (tenant-scoped) — NUEVO
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| FormId | FK | Sí | — | Formulario. |
| GroupId | FK | No | — | Grupo (0..1). |
| ControlTypeCode | Texto(40) | Sí | Catálogo RF-002 | **Tipo de control**. |
| FieldKey / Name | Texto(100) | Sí | Único en el formulario | **Nombre del campo** (clave técnica). |
| Title | Texto(300) | Sí | — | **Título** visible. |
| Description | Texto(1000) | No | — | **Descripción** del campo. |
| Weight | Decimal | Sí | ≥ 0 (default 1) | **Peso** del campo (solo campos puntuables). |
| IsRequired | Booleano | Sí | — | **Obligatorio** (sí/no). |
| DisplayOrder | Entero | Sí | ≥ 0 | Orden. |
| MinValue / MaxValue / MaxLength | Núm/Núm/Int | No | Según tipo | Configuración por tipo de control. |

### Entidad: ExitInterviewFormFieldOption (tenant-scoped) — NUEVO
| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| FieldId | FK | Sí | Campo de selección | Campo dueño. |
| OptionCode / Value | Texto(80) | Sí | Único en el campo | Valor de la opción. |
| Label | Texto(300) | Sí | — | Etiqueta visible. |
| Score | Decimal | No | 0–100 recomendado | Puntaje para tabulación (D-07). |
| DisplayOrder | Entero | Sí | ≥ 0 | Orden. |

### Entidad: ExitInterviewSubmission (tenant-scoped) — NUEVO
> **Anonimato (D-06):** si `IsAnonymous=Sí`, `PersonnelFileId` y `SubmittedByUserId` quedan **null** (disociados); se conservan las **dimensiones de-identificadas** (motivo/categoría/tipo/**plaza**/periodo) para la tabulación.

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | Guid | Sí | — | Referencia externa. |
| FormId / FormVersion | FK / Int | Sí | — | Formulario y versión respondidos (snapshot). |
| IsAnonymous | Booleano | Sí | Snapshot del flag del formulario | Si la submission está disociada (D-06). |
| PersonnelFileId | FK | **No** | **Null si anónimo**; si no, empleado del tenant | Empleado que se retira (solo si **no** es anónimo). |
| SubmittedByUserId | Guid | No | **Null si anónimo** | Quién capturó (empleado o RRHH), si no es anónimo. |
| RetirementReasonCode | Texto(80) | Sí | Snapshot del motivo | Motivo de retiro (dimensión analítica). |
| RetirementCategoryCode / SeparationType | Texto | Sí | Snapshot | Categoría y clasificación (dimensiones, D-02). |
| PositionSlotPublicId / PlazaSnapshot | Guid/Texto | No | **Plaza a la fecha de baja** (RQ-02) | **Área = plaza** para tabulación (D-10) sin re-identificar. |
| Period | Texto(7) | Sí | `YYYY-MM` de la baja | **Periodo** para tabulación (D-10). |
| Status | Enum | Sí | Draft/Submitted/**Archived** | Estado (Archived en rehire — D-12). |
| SubmittedUtc | Fecha | No | No futura | Fecha de envío. |
| TotalScore | Decimal | No | Derivado **0–100** | Índice ponderado (RF-012, D-07). |
| ConcurrencyToken | Guid | Sí | — | Concurrencia. |

### Entidad: ExitInterviewAnswer (tenant-scoped) — NUEVO
> El anonimato es de la **submission** (no de la respuesta): la respuesta **siempre** pertenece a su submission; es la submission la que está (o no) disociada del empleado.

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| SubmissionId | FK | Sí | — | Submission dueña. |
| FieldKeySnapshot / TitleSnapshot | Texto | Sí | — | Snapshot de la definición. |
| ControlTypeCode | Texto(40) | Sí | — | Tipo de control respondido. |
| ValueText / ValueNumber / ValueDate / ValueBool | Varios | No | Según tipo | Valor según el tipo de control. |
| SelectedOptionCodes | Texto/JSON | No | Para selección | Opciones elegidas. |
| WeightSnapshot / NormalizedScore | Decimal | No | Derivado | Peso y puntaje normalizado (0–100) de la respuesta. |

### Entidad (existente, a evolucionar): PersonnelFileEmployeeProfile
| Campo | Estado | Acción |
|---|---|---|
| RetirementCategoryCode / RetirementReasonCode | Texto libre `varchar(80)` (`PersonnelFileEmployee.cs:49-51`) | **Validar por código** contra catálogos RF-001. |

---

## 13. Integraciones necesarias

- **Catálogos por key (interno):** reutilizar `GeneralCatalogKeyMap` (wire key → categoría) y el lookup **validar-por-código** (`IPositionCatalogLookup.GetActiveCatalogReferenceByCodeAsync` o equivalente para `GeneralCatalogs`) para `retirement-categories`, `retirement-reasons` y `form-control-types`.
- **Expediente de personal (interno):** la submission **no anónima** referencia `PersonnelFile`/`PersonnelFileEmployeeProfile`; siempre toma del periodo el **motivo/categoría/área** (snapshot para tabulación).
- **Rehire (interno):** enganchar en `Rehire/RehireEmployee.cs` (que limpia la baja) el **archivado** de las submissions del periodo previo (D-12).
- **Identidad/Permisos (interno):** alta de permisos `ManageExitInterviewForms`, `ViewExitInterviews`, `ManageExitInterviews` en el subsistema RBAC/Provisioning.
- **Archivos:** **no aplica** — sin adjuntos (D-09).
- **Externas:** **ninguna** en el alcance (sin correo/WhatsApp/encuestas externas — FA-4/FA-11).

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **RRHH / Admin (diseñador)** | `ManageExitInterviewForms` (crear/editar/publicar/versionar/archivar formularios y catálogos), `ViewExitInterviews`, `ManageExitInterviews` | Solo en su tenant; no borra formularios con respuestas (archiva). |
| **RRHH / Entrevistador** | `ManageExitInterviews` (capturar respuestas de cualquier empleado) | No construye formularios sin `ManageExitInterviewForms`. |
| **Empleado que se retira** | **Autoservicio**: leer el formulario aplicable y crear/enviar su **propia** submission (opcional — D-05) | Solo su propio expediente; si el formulario es anónimo su submission no se atribuye; **no** lee respuestas enviadas; 403 sobre otros. |
| **Gerencia / Analista RRHH** | **Tabulación agregada** (Fase 2) | **No** accede a respuestas individuales (D-14); sin re-identificar anónimos. |
| **Jefatura / área** | — | **Sin acceso** a respuestas (D-14): solo RRHH. |
| **Auditor** | Lectura de auditoría | Sin acceso a contenido de submission anónima re-identificable. |

---

## 15. Criterios de aceptación generales

- **CA-1.** Catálogos `retirement-categories`, `retirement-reasons` y `form-control-types` **parametrizados**, accesibles **por key** y con **seed SV** disponible en todas las envs (`HasData`).
- **CA-2.** El **motivo de retiro** del perfil se **valida por código** (no más texto libre).
- **CA-3.** RRHH puede **crear, configurar (con los atributos del requisito), validar, publicar, versionar y asociar a UN motivo** un formulario; el **anonimato** se define a nivel de formulario.
- **CA-4.** Existe **un** formulario aplicable determinista por motivo (**single-active**, sin fallback); si el motivo no tiene formulario, no hay entrevista (opcional).
- **CA-5.** El empleado puede **llenar y enviar** su entrevista (autoservicio); RRHH puede **capturar** en su nombre; accesos indebidos → **403**.
- **CA-6.** Las respuestas guardan **snapshot** + **índice 0–100 derivado**; si el formulario es **anónimo**, la submission queda **disociada** del expediente.
- **CA-7.** **Lectura de respuestas: solo RRHH** (D-14); jefatura/área → 403.
- **CA-8.** **Sin datos legados** de motivo en texto libre (eliminados — D-11); el motivo del perfil se **valida por código**.
- **CA-9.** Tras un **rehire**, las submissions del periodo previo quedan **archivadas** (D-12).
- **CA-10.** Reglas en módulo **puro** unit-testeado; errores **bilingües** con **paridad EN/ES** verificada por test.
- **CA-11.** Auditoría de cambios, **concurrencia optimista**, **soft-delete** y **multi-tenant** operativos.
- **CA-12.** La **suite** de pruebas (unitarias + gobernanza de permisos + localización) pasa en verde.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R-01. Alcance/complejidad:** aun siendo **exclusivo** (D-01), un constructor de formularios es un subsistema considerable. **Mitigación:** MVP por fases; alcance acotado (sin motor genérico, sin adjuntos).
- **R-02. Privacidad del anonimato:** un anonimato mal implementado (re-identificable) incumple la normativa de datos. **Mitigación (D-06):** disociación **a nivel de submission** (sin FK al expediente) + revisión de Cumplimiento; lectura solo RRHH (D-14).
- **R-03. Versionado/snapshot:** editar formularios con respuestas existentes puede corromper históricos. **Mitigación:** bloqueo al publicar + snapshot por submission.
- **R-04. ~~Migración del motivo de retiro~~ — RESUELTO (D-11):** no hay datos a migrar; los motivos legados en texto libre **se eliminan** (son prueba). Riesgo neutralizado.
- **R-05. ~~Ambigüedad de "peso"~~ — RESUELTO (D-07):** es **scoring ponderado**; el modelo (índice 0–100) quedó definido en RF-012.
- **R-06. Expectativa de tabulación:** el valor real (analítica de rotación) es **Fase 2**; gestionar expectativas con el negocio.
- **R-07. Anonimato vs. dimensión "área" (= plaza, RQ-02):** reportar por plaza sobre submissions anónimas puede re-identificar en muestras pequeñas. **Mitigación:** umbral de agregación **k ≥ 5** (RQ-04) en RF-014.

### Supuestos
- **S-01.** No hay datos de producción que impidan rehacer estructuras (alinear con la práctica de *drop&recreate* usada en módulos previos).
- **S-02.** Los tipos de control son un conjunto **cerrado** gobernado por el sistema (no editable por el tenant).
- **S-03.** El idioma del contenido del formulario es el del tenant (sin multilenguaje por campo — FA-7).
- **S-04.** El empleado que se retira tiene (o tendrá) acceso de autoservicio (cuenta vinculada `LinkedUserPublicId`).
- **S-05.** SV es el primer país; el diseño país-scoped permite agregar otros sin recodificar.

### Dependencias
- **D-1.** `GeneralCatalogs` + `GeneralCatalogKeyMap` + lookup validar-por-código (servicios por key).
- **D-2.** `GlobalCatalogSeedData`/`HasData` para el seed SV.
- **D-3.** RBAC/Provisioning para los permisos nuevos.
- **D-4.** `PersonnelFileEmployeeProfile` (anclaje del motivo de retiro y del expediente).
- **D-5.** `Rehire/RehireEmployee.cs` (engancha el archivado de submissions — D-12).

---

## 17. Preguntas resueltas y residuales

### Preguntas resueltas por el negocio (Q-01…Q-14 → D-xx, 2026-06-24)

| # | Pregunta | Respuesta del negocio | → Decisión |
|---|---|---|---|
| **Q-01** | ¿Motor genérico o módulo exclusivo? | Módulo **exclusivo** de entrevista de retiro. | D-01 |
| **Q-02** | ¿Motivo jerárquico? | **Sí**, jerárquico (analista propone estructura HRIS: `SeparationType` → categoría → motivo). | D-02 |
| **Q-03** | ¿Asociación a uno o varios motivos? | A **un** motivo. | D-03 |
| **Q-04** | ¿Quién llena? | **Ambos** (autoservicio + RRHH); RRHH puede ver. | D-04 |
| **Q-05** | ¿Obligatoria antes de la baja? | **Opcional** (el empleado puede hacerla o no). | D-05 |
| **Q-06** | ¿Anónimo por campo o por submission? | **Toda la submission**. | D-06 |
| **Q-07** | ¿"Peso" es ponderado? ¿Puntaje? | **Sí, ponderado**; el puntaje lo define el analista. | D-07 |
| **Q-08** | ¿Tipos de control completos? | **Sí, no falta ninguno**. | D-08 |
| **Q-09** | ¿Adjuntos en Fase 1? | **No** se requieren. | D-09 |
| **Q-10** | ¿Qué reportes? | Por **motivo, categoría, área, periodo, score** + exportación. | D-10 |
| **Q-11** | ¿Motivos legados? | **Eliminar** (son prueba); sin backfill. | D-11 |
| **Q-12** | ¿Submissions tras rehire? | **Se archivan**. | D-12 |
| **Q-13** | ¿Seed SV? | **Aprobada** la propuesta de la sección 18. | D-13 |
| **Q-14** | ¿Quién lee respuestas? | **Solo RRHH**. | D-14 |

### Preguntas residuales/derivadas (técnicas) — **TODAS RESUELTAS (2026-06-24)**

| # | Pregunta técnica | Resolución ratificada |
|---|---|---|
| **RQ-01** | Mapeo `SeparationType` por categoría del seed | **Ratificado** el mapeo de la sección 18 (VOL/INVOL/OTRA por categoría). |
| **RQ-02** | Fuente del snapshot de "área" para tabulación | **Plaza (`PositionSlot`) a la fecha de baja.** (No unidad organizativa.) |
| **RQ-03** | Interpretación del índice 0–100 | **Ratificado:** mayor = experiencia más favorable; RRHH define puntajes 0–100 por opción. |
| **RQ-04** | Umbral *k* de agregación para anónimos | **k ≥ 5** (propuesta aceptada). |
| **RQ-05** | ¿Empleado re-lee su submission enviada? | **No**; solo el **borrador antes de enviar** (consistente con D-14). |
| **RQ-06** | ¿Una submission por empleado+baja o varias? | **Una activa**; los reintentos **editan el borrador**. |

---

## 18. Recomendaciones del Analista de Negocio

1. **Cerrar primero el prerrequisito de catálogo.** El corazón del requisito ("asociar a un motivo de retiro") **no tiene sobre qué anclarse** hoy. Recomiendo **PR-1 = catálogos de motivo de retiro** (categoría + motivo, país-scoped, validados por código, **seed SV**) y migrar el perfil a validación por código. Es la pieza de menor riesgo y mayor valor inmediato, y deja el "motivo de retiro" **reportable** aun antes del constructor.

2. **Módulo exclusivo, no un motor genérico (D-01).** Modelar entidades `ExitInterview*` **sin** discriminador `FormType` y **sin** lógica condicional/multilenguaje/cálculos. Mantener el alcance acotado evita la sobre-ingeniería (R-01) y entrega valor antes.

3. **MVP en fases (ratificado):**
   - **Fase 1 (MVP):** PR-1 catálogos jerárquicos (con `SeparationType`) + seed SV → PR-2 tipos de control → PR-3 constructor (form con bandera **anónimo**/grupos/campos/opciones + reglas) → PR-4 publicar/versionar/asociar a **1 motivo** → PR-5 llenar (autoservicio+RRHH) con snapshot + **índice 0–100** + anonimato a nivel submission + **archivado en rehire** → PR-6 permisos + **lectura RRHH** + controlador dedicado + localización + RF-019.
   - **Fase 2:** tabulación de rotación (motivo/categoría/área/periodo/score) + exportación + corrección/anulación (RF-020) — el **valor de negocio** real.
   - **Fase 3 (no comprometida):** lógica condicional, notificaciones. **Descartado:** generalización (D-01), obligatoriedad antes de la baja (D-05), adjuntos (D-09).

4. **Ambigüedades ya resueltas (no reabrir):** el **anonimato** es a nivel de **submission/formulario** (D-06) y el **"peso"** es **scoring ponderado** con índice **0–100** (D-07, modelo en RF-012). Los detalles técnicos menores **(RQ-01…RQ-06) también quedaron resueltos** (sección 17): área = **plaza** a la fecha de baja, *k* ≥ 5, índice mayor = mejor, sin re-lectura de la submission enviada, una submission activa.

5. **Reutilizar al máximo los patrones del repositorio:** catálogos país-scoped (`CountryScopedCatalogItem`), servicios por key (`GeneralCatalogKeyMap` + validar-por-código), seed `HasData`/`GlobalCatalogSeedData`, controlador dedicado por `AuthorizationPolicySet` (clase), gate self-service, módulo `*.Rules.cs`, y paridad de localización EN/ES. Esto mantiene consistencia y baja el costo.

6. **Versionado y snapshot desde el día 1.** Aun en el MVP, publicar→bloquear→versionar y snapshotear las respuestas; reabrir esto luego es caro y arriesgado (R-03).

7. **Seed SV (aprobado — D-13; refinado con `SeparationType` — D-02):**

   **Categorías (con su clasificación `SeparationType`):**

   | Categoría (`Code`) | `SeparationType` |
   |---|---|
   | `VOLUNTARIA` | VOLUNTARIA |
   | `JUBILACION` | VOLUNTARIA |
   | `INVOLUNTARIA` | INVOLUNTARIA |
   | `ABANDONO` | INVOLUNTARIA |
   | `NO_SUPERA_PERIODO_PRUEBA` | INVOLUNTARIA |
   | `FIN_CONTRATO` | OTRA |
   | `MUTUO_ACUERDO` | OTRA |
   | `FALLECIMIENTO` | OTRA |

   - **Motivos (ejemplos por categoría):** *VOLUNTARIA* → `MEJOR_OFERTA_SALARIAL`, `CRECIMIENTO_PROFESIONAL`, `AMBIENTE_LABORAL`, `RELACION_JEFATURA`, `MOTIVOS_PERSONALES`, `SALUD`, `ESTUDIOS`, `REUBICACION_GEOGRAFICA`, `DISTANCIA_TRANSPORTE`, `INSATISFACCION_FUNCIONES`; *INVOLUNTARIA* → `BAJO_DESEMPENO`, `REESTRUCTURACION`, `FALTA_DISCIPLINARIA`, `AUSENTISMO`, `INCUMPLIMIENTO_POLITICAS`, `RECORTE_PRESUPUESTARIO`; *OTRA* → `FIN_CONTRATO_TEMPORAL`, `FIN_OBRA_PROYECTO`, `JUBILACION_EDAD`, `MUTUO_ACUERDO`.
   - **Tipos de control:** `TEXTO_CORTO`, `TEXTO_LARGO`, `NUMERO`, `FECHA`, `LISTA_DESPLEGABLE`, `OPCION_UNICA`, `SELECCION_MULTIPLE`, `CASILLA`, `ESCALA`.

8. **Privacidad por diseño.** Tratar el anonimato (D-06) como **disociación real a nivel de submission** (no solo "ocultar en UI"): sin FK al expediente, solo dimensiones de-identificadas; lectura **solo RRHH** (D-14); revisado por Cumplimiento, dada la normativa de protección de datos personales aplicable en El Salvador.

---

## Consideraciones finales

Este análisis **valida que el requerimiento NO está implementado** (ni la entrevista de retiro ni un constructor de formularios): se requiere desarrollo. Con las respuestas del negocio, las decisiones **D-01…D-14** quedan **ratificadas** (2026-06-24) y el análisis pasa a estado **Definido / Cerrado (Fase 1)**. El alcance es un **módulo exclusivo** de entrevista de retiro (D-01): catálogos jerárquicos de motivo (con `SeparationType`) + tipos de control, **parametrizados**, **accedidos por key** (reutilizando los servicios existentes) y con **seed SV**; constructor de formularios con **anonimato a nivel de formulario** (D-06) y **asociación a un (1) motivo** (D-03); captación por **autoservicio + RRHH** (D-04), **opcional** (D-05), con **score ponderado 0–100** (D-07); **lectura solo RRHH** (D-14); **archivado en rehire** (D-12); **sin adjuntos** (D-09) y **sin** datos legados (D-11). Los detalles técnicos (RQ-01…RQ-06) también quedaron **resueltos** (área = plaza, *k* ≥ 5, índice mayor = mejor). **Siguiente paso:** redactar el **plan técnico** (`docs/technical/plan-tecnico-entrevista-retiro.md`) y la **guía de integración frontend**, siguiendo el patrón de los módulos previos.
