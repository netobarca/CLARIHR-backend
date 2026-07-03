# Análisis de Negocio — Revalidación de Catálogos del Expediente de Empleados

> **Tipo de documento:** Análisis de requerimiento (revalidación + GAP) y especificación funcional.
> **Módulo:** Expediente de Personal (`PersonnelFiles`) + Catálogos globales (`GeneralCatalogs`, `PersonnelReferenceCatalog`, `EducationCatalogs`, `Compensation`).
> **Fecha:** 2026-06-30 · **Versión:** v2.1 (decisiones D-01…D-16 **ratificadas** + riesgos de plan RT-01…RT-06 **resueltos**; **seed inicial definido**).
> **Autor:** Analista de Negocio (CLARIHR).
> **Estado:** Requerimiento **cerrado a nivel de negocio**; listo para plan técnico. **Seed inicial obligatorio por `HasData`** (ver §20).

---

## 0. Veredicto ejecutivo (resultado de la revalidación)

El requerimiento pide **revalidar 13 catálogos** del expediente de empleados y, **para los que no existan o estén incompletos, agregarlos al plan** para cerrar el entregable.

**Resultado de la auditoría de código (`src/`):** de los 13 catálogos, **solo 2 están completos**, **6 existen pero están incompletos** (les faltan campos o relaciones exigidas por el negocio) y **5 no existen** (desarrollo nuevo). Además, la revalidación descubrió **1 catálogo derivado** que hay que crear (**Nivel educativo**, requerido por "Tipos de estudios") y **4 brechas transversales** (mantenimiento por API, motor de validación por formato, alcance de país y siembra en servidor).

### 0.1. Matriz de revalidación (el corazón del entregable)

| # | Catálogo solicitado | Estado | Entidad / evidencia en código | Patrón | Sembrado | Brecha principal a cerrar |
|---|---|---|---|---|---|---|
| 1 | **Títulos personales** | ❌ **NO EXISTE** | ninguna (la persona solo tiene `FirstName`/`LastName`, `PersonnelFile.cs:73-75`) | — | no | Catálogo nuevo + campo/FK de tratamiento en la persona |
| 2 | **Tipos de direcciones** | ❌ **NO EXISTE** | dirección sin tipo (`PersonnelFileAddress`, `PersonnelFile.cs:1023`; solo `IsCurrent`) | — | no | Catálogo nuevo + campo tipo en la dirección |
| 3 | **Tipos de documentos** (identidad) | 🟡 **PARCIAL** | `IdentificationTypeCatalogItem` (`PersonnelReferenceCatalogItem.cs:24`) | Reference (país) | SV | **Columna de formato/máscara + validación del número** contra ese formato |
| 4 | **Parentesco** | ✅ **COMPLETO** (gap menor) | `KinshipCatalogItem` (`PersonnelReferenceCatalogItem.cs:108`) | Reference (país) | SV | Contactos de emergencia usan `Relationship` texto libre (`PersonnelFile.cs:1100`) |
| 5 | **Hobbies** | ❌ **NO EXISTE** | solo texto libre (`PersonnelFileHobby.HobbyName`, `PersonnelFile.cs:1395`) | — | no | Catálogo nuevo + migrar texto libre a FK |
| 6 | **Asociaciones** | ❌ **NO EXISTE** | solo texto libre (`PersonnelFileAssociation.AssociationName`, `PersonnelFile.cs:1548`) | — | no | Catálogo nuevo + migrar texto libre a FK |
| 7 | **AFP** | ❌ **NO EXISTE** (como catálogo maestro) | solo una fila de *tasas* genérica "AFP" en `CompensationConceptTypeCatalogItem` (`GlobalCatalogSeedData.cs:774`) | — | tasas SV | **Entidad AFP maestra completa** (identidad + contacto + parámetros de cálculo) + afiliación del empleado |
| 8 | **Tipos de estudios** | 🟡 **PARCIAL** | `EducationStudyTypeCatalogItem` (`EducationCatalogItems.cs:40`) | System (global) | global | **Abreviatura** + **catálogo "Nivel educativo" (nuevo) + FK** |
| 9 | **Carreras** | 🟡 **PARCIAL** | `EducationCareerCatalogItem` (`EducationCatalogItems.cs:55`) | System (global) | global | **Abreviatura, Incremento, Reconocida, FK a Tipo de estudio, País** |
| 10 | **Beneficios adicionales** | 🟡 **PARCIAL** | registro por empleado sí (`PersonnelFileAdditionalBenefit`, `PersonnelFileEmployee.cs:353`); **catálogo NO** (`BenefitTypeCode` texto libre) | — | no | **Catálogo de tipos de beneficio** (descripción + país) + migrar texto libre |
| 11 | **Tipos de contratos** | 🟡 **PARCIAL** | `ContractTypeCatalogItem` (`GeneralCatalogItems.cs:764`) | General (país) | SV | **Abreviatura** + booleano **Temporal** |
| 12 | **Rubros salariales** | 🟡 **PARCIAL** | `CompensationConceptTypeCatalogItem` (`CompensationConceptTypeCatalogItem.cs:13`); legado `SalaryItem` **eliminado** (mig `20260622000049`) | General enriquecido (país) | SV | "**Es salario base**" hoy es un **código mágico** `SALARIO_BASE`, no un booleano |
| 13 | **Formas de pago** | ✅ **COMPLETO** (gap menor de dato) | `PaymentMethodCatalogItem` (`GeneralCatalogItems.cs:535`) | General (país) | SV | Falta sembrar el valor "**boleta de pago**" |
| 14 | **Nivel educativo** *(derivado de #8)* | ❌ **NO EXISTE** | ninguna | — | no | Catálogo nuevo (referenciado por Tipos de estudios) |

**Leyenda:** ✅ Completo · 🟡 Parcial (existe, requiere ampliación) · ❌ No existe (net-new).
**Rutas de API:** catálogos "General" se leen en `GET api/v1/general-catalogs/{key}`; catálogos "Reference" en `GET api/v1/reference-catalogs/{key}`.

### 0.2. Resumen cuantitativo

- **✅ Completos (2):** Parentesco, Formas de pago (ambos con brecha menor).
- **🟡 Parciales — existen, requieren ampliar (6):** Tipos de documentos, Tipos de estudios, Carreras, Beneficios adicionales, Tipos de contratos, Rubros salariales.
- **❌ Nuevos — no existen (5 + 1 derivado):** Títulos personales, Tipos de direcciones, Hobbies, Asociaciones, AFP, **Nivel educativo**.
- **Total con trabajo pendiente:** **12 de 14** catálogos.

### 0.3. Cuatro brechas transversales (aplican a varios catálogos)

- **BT-01 — Sin API de mantenimiento para catálogos "General".** Los catálogos de la familia `GeneralCatalogItem` (contratos, formas de pago, rubros y cualquiera nuevo) hoy son **solo lectura + solo semilla**: se leen en `GeneralCatalogsController` (`[Authorize]`, sin RBAC) y **no tienen endpoints de escritura** para el tenant. Solo las familias `PersonnelReferenceCatalog` (vía backoffice `SystemCatalogsController`, política `PlatformOperator`) y `EducationCatalog` (backoffice `EducationCatalogsController`) tienen CRUD, y **es de operador de plataforma, no autoservicio de RR. HH.** Varios requerimientos dicen "*se utilizará para dar mantenimiento*" → **hay que decidir** si RR. HH. requiere CRUD propio (D-15).
- **BT-02 — La siembra solo llega al servidor por `HasData`.** Los catálogos sembrados solo en `DevSeedService` quedan **vacíos en producción** (no se hace backfill sobre tenants ya provisionados). Todo catálogo nuevo debe sembrarse vía `GlobalCatalogSeedData` (`HasData` → `InsertData` en migración → todos los ambientes).
- **BT-03 — Alcance de país.** Hoy todo está sembrado **solo para El Salvador (SV)**. El concepto "*aplica para todos los países / aplica para este país*" (pedido en Carreras y Beneficios) **no existe** en el modelo: `CountryScopedCatalogItem` exige país obligatorio y `SystemScopedCatalogItem` no tiene país. No hay bandera "aplica a todos" (D-13).
- **BT-04 — No existe motor de validación por formato/máscara.** Ningún catálogo define hoy un formato/máscara/expresión regular para validar un valor ingresado por el usuario. La validación por formato del número de documento (catálogo #3) es **desarrollo nuevo** (columna + regla); el primitivo reutilizable es `System.Text.RegularExpressions.Regex`.

> **Nota de método:** todos los hallazgos de "no existe" fueron confirmados con búsqueda exhaustiva de clases/columnas y verificados manualmente (sin coincidencias para `salutation/honorific/tratamiento`, `AddressType`, entidad `AFP/PensionFund`, ni columna de `format/mask` en el catálogo de identificaciones). Las decisiones abiertas están en §17.

---

## 1. Resumen del producto o requerimiento

Se requiere **revalidar y completar el conjunto de catálogos maestros** que alimentan el **Expediente de Personal** de CLARIHR. Un **catálogo** es una lista administrable de valores estandarizados (código + nombre + atributos) que se usa como **fuente de opciones** en los formularios del expediente (datos personales, familiares, educación, contratación, compensación) y que **garantiza consistencia de datos, validación en el servidor y explotación analítica** (agrupar/filtrar por valores controlados en lugar de texto libre).

El objetivo del entregable es doble:

1. **Verificar** cuáles de los 13 catálogos solicitados **ya existen** y **con qué campos**, comparándolos contra la especificación de negocio.
2. **Cerrar las brechas**: crear los catálogos faltantes, ampliar los incompletos (agregar campos y relaciones), y estandarizar los que hoy se capturan como **texto libre** (hobbies, asociaciones, tipo de beneficio, parentesco en contacto de emergencia).

**Problema que resuelve:** hoy conviven tres situaciones que degradan la calidad del dato y limitan el análisis: (a) datos capturados como **texto libre** sin catálogo (hobbies, asociaciones, beneficios); (b) catálogos que existen pero **carecen de atributos** exigidos por RR. HH. (abreviatura, temporal, incremento, reconocida, formato de documento, parámetros de AFP); y (c) catálogos que **no existen** (títulos personales, tipos de dirección, AFP maestra, nivel educativo). Cerrar estas brechas permite formularios consistentes, validación server-side, reportes confiables y preparación para nómina.

---

## 2. Objetivos del negocio

- **OB-01.** Disponer de **catálogos maestros completos y consistentes** para todos los campos de opción del expediente, eliminando el texto libre donde el negocio exige un valor controlado.
- **OB-02.** **Estandarizar y validar** en el servidor los valores ingresados (por código de catálogo activo), evitando inconsistencias y datos "sucios".
- **OB-03.** **Cerrar el entregable de expediente**: que ningún formulario dependa de un catálogo inexistente o incompleto.
- **OB-04.** Habilitar **explotación analítica** confiable (agrupar por tipo de contrato, parentesco, carrera, nivel educativo, forma de pago, etc.).
- **OB-05.** Preparar la base para **nómina y prestaciones** (AFP con parámetros de cálculo, rubros salariales, formas de pago).
- **OB-06.** **Reutilizar las convenciones de catálogo existentes** (country-scoped, `HasData`, wire key, concurrencia, auditoría) para minimizar costo y riesgo, y **decidir el modelo de mantenimiento** (semilla vs. CRUD de RR. HH.).
- **OB-07.** Mantener **trazabilidad histórica**: al migrar campos de texto libre a catálogo, no perder los datos ya capturados.

---

## 3. Alcance funcional

Incluye, por catálogo (detalle en §6):

- **AF-01.** Crear los **catálogos inexistentes**: Títulos personales, Tipos de direcciones, Hobbies, Asociaciones, AFP (maestro) y Nivel educativo.
- **AF-02.** Ampliar los **catálogos parciales** con los campos exigidos: formato/máscara en Tipos de documentos; Abreviatura + Nivel educativo en Tipos de estudios; Abreviatura + Incremento + Reconocida + FK a Tipo de estudio + País en Carreras; catálogo de tipos en Beneficios adicionales; Abreviatura + Temporal en Tipos de contratos; explicitar "es salario base" en Rubros salariales.
- **AF-03.** **Migrar a catálogo** los campos hoy en texto libre: `HobbyName`, `AssociationName`, `BenefitTypeCode`, y `Relationship` del contacto de emergencia (a Parentesco).
- **AF-04.** **Validación server-side** por código de catálogo activo en todos los puntos de consumo (crear/editar), con error de negocio (422) cuando el código no exista o esté inactivo.
- **AF-05.** **Sembrar** los valores iniciales para El Salvador (SV) vía `HasData`, incluido el valor faltante "boleta de pago" en Formas de pago.
- **AF-06.** **Validación del número de documento** por formato definido en el catálogo de Tipos de documentos.
- **AF-07.** **Lectura por API** de cada catálogo (whitelist en `GeneralCatalogKeyMap`) para alimentar los formularios del frontend.
- **AF-08.** **(Sujeto a D-15)** Endpoints de **mantenimiento (CRUD)** para los catálogos que el negocio requiera administrar en tiempo de ejecución.

---

## 4. Fuera de alcance

- **FA-01.** El **cálculo de nómina** (planilla, retenciones, topes, ISR por tramos): los catálogos de AFP/rubros/formas de pago se **configuran** aquí, pero el **cómputo** pertenece al futuro módulo de Nómina.
- **FA-02.** **Migración masiva de datos productivos** más allá del backfill técnico de los campos que pasan de texto libre a catálogo (no hay limpieza/curación manual de datos existentes en este alcance salvo lo indicado).
- **FA-03.** **Multi-país operativo**: se siembra SV; habilitar otros países (y el concepto "aplica a todos") es una decisión de diseño (D-13) que puede diferirse.
- **FA-04.** **Rediseño del motor de compensación**: "Rubros salariales" se revalida contra el modelo `CompensationConcept` existente; no se rediseña dicho módulo.
- **FA-05.** **Flujos de aprobación / workflow** sobre catálogos (alta/baja con autorización): fuera de alcance salvo indicación.
- **FA-06.** **Integraciones externas** con entidades AFP, bancos o registros académicos (sincronización automática): fuera de alcance.
- **FA-07.** **Internacionalización de los valores** de catálogo (traducción de nombres a varios idiomas) más allá de ES.

---

## 5. Actores o usuarios involucrados

| Actor | Rol / interacción |
|---|---|
| **Administrador de RR. HH. (tenant)** | Consume los catálogos en los formularios del expediente; **potencialmente** los administra (CRUD) si se aprueba D-15. |
| **Analista / Capturista de expediente** | Registra datos del empleado seleccionando valores de catálogo. |
| **Empleado (autoservicio)** | En pantallas de autoservicio, lee catálogos para completar sus propios datos (según permisos por módulo). |
| **Operador de plataforma (Backoffice)** | Hoy es quien administra (CRUD) los catálogos `Reference` y `Education` vía backoffice (`PlatformOperator`); mantiene la data maestra global. |
| **Sistema (validación server-side)** | Valida los códigos contra el catálogo activo y el número de documento contra su formato; siembra valores por `HasData`. |
| **Módulo de Nómina (futuro, consumidor)** | Consumirá AFP, rubros salariales y formas de pago para el cálculo (fuera de alcance de construcción aquí). |

---

## 6. Requerimientos funcionales

> Cada RF incluye un bloque **Estado actual / Brecha** con la evidencia de código, para trazar la revalidación. Prioridad orientativa; el orden de ejecución se propone en §18.

### RF-001 — Catálogo de Títulos personales

**Descripción:** Crear un catálogo administrable de **tratamientos/títulos** con los que la persona prefiere ser tratada (Ing., Sr., Sra., Srta., Lic., Dr., Dra., etc.) y asociarlo como campo opcional de la persona en el expediente.

**Estado actual / Brecha:** ❌ **No existe.** La persona solo tiene `FirstName`/`LastName` (`PersonnelFile.cs:73-75`); no hay campo de tratamiento ni catálogo. **Net-new completo.**

**Reglas de negocio:** valor único por país (`Code`); activo/inactivo; ordenable (`SortOrder`); el campo en la persona es **opcional**; al asignarlo debe existir y estar activo.

**Criterios de aceptación:** (a) existe el catálogo con lectura por API; (b) el expediente permite seleccionar/limpiar el título; (c) al guardar un código inválido/inactivo → error 422; (d) valores SV sembrados vía `HasData`.

**Prioridad:** Media. **Dependencias:** patrón de catálogo país (`PersonnelReferenceCatalog`); campo nuevo en la persona.

---

### RF-002 — Catálogo de Tipos de direcciones

**Descripción:** Crear un catálogo de **tipos de dirección** (casa/habitación, trabajo, facturación, temporal, etc.) usado como lista de opciones en el formulario de direcciones del expediente, y agregar el campo tipo a la dirección.

**Estado actual / Brecha:** ❌ **No existe.** El registro `PersonnelFileAddress` (`PersonnelFile.cs:1023`) tiene `AddressLine/Country/Department/Municipality/PostalCode/IsCurrent`, pero **ningún discriminador de tipo**. **Net-new** (catálogo + campo en la dirección).

**Reglas de negocio:** código único por país; activo/inactivo; el campo tipo en la dirección puede ser obligatorio u opcional (D-10); validación server-side del código.

**Criterios de aceptación:** (a) catálogo con lectura por API; (b) el formulario de dirección expone el selector de tipo; (c) código inválido/inactivo → 422; (d) semilla SV.

**Prioridad:** Media. **Dependencias:** decisión de obligatoriedad del tipo (D-10).

---

### RF-003 — Tipos de documentos (identidad) con **validación por formato**

**Descripción:** Ampliar el catálogo de **tipos de documento personal** (DUI, NIT, pasaporte, carné de residente, etc.) para que **cada tipo defina el formato/máscara** con el que debe ingresarse el número, y **validar** el número capturado contra ese formato.

**Estado actual / Brecha:** 🟡 **Parcial.** El catálogo existe (`IdentificationTypeCatalogItem`, `PersonnelReferenceCatalogItem.cs:24`), sembrado SV (DUI/NIT/PASSPORT/RESIDENT_CARD, mig `20260412191520`) y validado por existencia. **Falta:** (1) columna de **formato/máscara** (regex) en el ítem del catálogo; (2) **regla que valide el número** contra ese formato. Hoy el número (`PersonnelFileIdentification.IdentificationNumber`) solo valida `IsValidCode` + `MaxLength(80)` (`Identifications.cs:167-171`). **No existe ningún mecanismo de máscara reutilizable** (BT-04).

**Reglas de negocio:** el formato es **opcional por tipo** (si está vacío, no se valida el patrón); si está definido, el número debe cumplirlo (regex ancla completa); mensajes de error claros con el formato esperado; el formato se administra junto al tipo.

**Criterios de aceptación:** (a) el catálogo acepta y persiste el formato por tipo; (b) al capturar un número que no cumple el formato del tipo seleccionado → 422 con mensaje bilingüe; (c) tipos sin formato definido conservan la validación genérica; (d) DUI/NIT SV traen su formato sembrado (p. ej. DUI `########-#`).

**Prioridad:** **Alta** (es el punto crítico explícito del requerimiento). **Dependencias:** BT-04 (motor de formato nuevo); definición de los patrones por tipo (D-06).

---

### RF-004 — Parentesco (completar consumo en contacto de emergencia)

**Descripción:** El catálogo de **parentesco** ya existe y se usa en familiares y beneficiarios; se debe **cerrar la brecha** de que el **contacto de emergencia** lo use en lugar de texto libre.

**Estado actual / Brecha:** ✅ **Completo con gap menor.** `KinshipCatalogItem` (`PersonnelReferenceCatalogItem.cs:108`), sembrado SV (mig `20260414181103`: CONYUGE/PAREJA/PADRE/MADRE/HIJO_A/…), validado en familiares (`FamilyMembers.Handlers.cs`) y beneficiarios (`InsuranceBeneficiaries.Handlers.cs`). **Gap:** `PersonnelFileEmergencyContact.Relationship` es **texto libre** (`PersonnelFile.cs:1100`).

**Reglas de negocio:** el parentesco del contacto de emergencia debe referenciar un código activo del catálogo; migrar valores existentes (mapear/normalizar) sin perder datos.

**Criterios de aceptación:** (a) el contacto de emergencia usa selector de parentesco; (b) código inválido/inactivo → 422; (c) datos históricos preservados/migrados.

**Prioridad:** Baja. **Dependencias:** estrategia de migración del texto libre existente.

---

### RF-005 — Catálogo de Hobbies

**Descripción:** Crear un catálogo de **aficiones/pasatiempos** y migrar la captura de hobbies del empleado de texto libre a selección de catálogo.

**Estado actual / Brecha:** ❌ **No existe.** Solo `PersonnelFileHobby.HobbyName` texto libre (`PersonnelFile.cs:1395`). **Net-new** (catálogo + FK + migración de texto libre).

**Reglas de negocio:** código único; activo/inactivo; el registro del empleado referencia el código; ¿permitir "Otro"/texto libre complementario? (D-11).

**Criterios de aceptación:** (a) catálogo con lectura por API; (b) el expediente selecciona hobbies del catálogo; (c) código inválido → 422; (d) semilla SV; (e) datos previos migrados.

**Prioridad:** Baja. **Dependencias:** D-11 (¿país-scoped o global?, ¿permitir "Otro"?).

---

### RF-006 — Catálogo de Asociaciones

**Descripción:** Crear un catálogo de **asociaciones/entidades** a las que puede pertenecer un empleado (sindicatos, colegios profesionales, clubes) y migrar la captura de texto libre.

**Estado actual / Brecha:** ❌ **No existe.** Solo `PersonnelFileAssociation.AssociationName` texto libre (con `Role/JoinedDate/LeftDate/Payment`) (`PersonnelFile.cs:1548`). **Net-new** (catálogo + FK; se conservan los atributos por-empleado role/fechas/pago).

**Reglas de negocio:** código único; activo/inactivo; el registro por empleado mantiene rol, fechas y pago, pero la **entidad** pasa a ser catálogo; ¿permitir "Otro"? (D-11).

**Criterios de aceptación:** (a) catálogo con lectura por API; (b) el expediente selecciona la asociación del catálogo y conserva rol/fechas/pago; (c) código inválido → 422; (d) semilla SV; (e) migración de datos previos.

**Prioridad:** Baja. **Dependencias:** D-11.

---

### RF-007 — Catálogo maestro de **AFP** (con parámetros de cálculo)

**Descripción:** Crear el **catálogo maestro de AFP** (Administradoras de Fondos de Pensiones) con datos de identidad, contacto y **parámetros de cálculo**, y permitir asociar a cada empleado su AFP.

**Estado actual / Brecha:** ❌ **No existe como catálogo maestro.** Solo existe **una fila de *tasas* genérica** con código `"AFP"` dentro de `CompensationConceptTypeCatalogItem` (`GlobalCatalogSeedData.cs:774`; tasas empleado 7.25% / patrono 8.75%), aplicada por plaza vía `PersonnelFileCompensationConcept`. **No hay** entidad por-administradora ni los campos de identidad/contacto ni todos los parámetros. **Net-new (el ítem más grande).**

Campos requeridos (spec): **Nombre, Dirección, Teléfono, Fax, Nombre de contacto, Abreviatura, País**; **parámetros:** Cuota de empleado, Cuota de patrono, **Cuota patronal para empleado pensionado**, **Valor mínimo para cotización**, **Valor máximo para cotización**.

**Reglas de negocio:** AFP única por país (Código/Abreviatura); activo/inactivo; parámetros numéricos con precisión de porcentaje/monto (convención del proyecto: % `numeric(11,8)`, monto `numeric(18,2)`); el empleado referencia **una** AFP; **reconciliación** con las tasas hoy en `CompensationConceptType` (D-07: ¿la nómina usará las tasas de la AFP o del concepto genérico?).

**Criterios de aceptación:** (a) CRUD/maestro de AFP con todos los campos; (b) el expediente/afiliación del empleado permite seleccionar su AFP; (c) validación de AFP activa → 422 si inválida; (d) semilla SV de las AFP reales (Confía, Crecer…) con sus parámetros; (e) definido y documentado qué fuente de tasas usa la nómina.

**Prioridad:** **Alta** (por complejidad y valor), pero **ejecutable como mini-proyecto aparte** (Fase 4). **Dependencias:** D-07 (reconciliación de tasas con compensación); definición de dónde se afilia el empleado a la AFP.

---

### RF-008 — Tipos de estudios (agregar **Abreviatura** y **Nivel educativo**)

**Descripción:** Ampliar el catálogo de **tipos de estudios** con **Abreviatura** y con **Nivel educativo** (seleccionable de un **catálogo nuevo**, ver RF-014).

**Estado actual / Brecha:** 🟡 **Parcial.** `EducationStudyTypeCatalogItem` (`EducationCatalogItems.cs:40`) existe (global/`SystemScoped`, sembrado). **Falta:** columna **Abreviatura** y **FK a "Nivel educativo"** (catálogo que **no existe** hoy → RF-014).

**Reglas de negocio:** descripción (=`Name`), abreviatura, nivel educativo (FK obligatoria u opcional, D-12); código único.

**Criterios de aceptación:** (a) el ítem persiste abreviatura y nivel educativo; (b) lectura por API incluye ambos; (c) nivel educativo inválido → error; (d) semilla SV coherente.

**Prioridad:** Media. **Dependencias:** **RF-014** (catálogo Nivel educativo).

---

### RF-009 — Carreras (agregar **Abreviatura, Incremento, Reconocida, FK a Tipo de estudio, País**)

**Descripción:** Ampliar el catálogo de **carreras/grados académicos** con abreviatura, incremento, reconocida (sí/no), **relación con Tipo de estudio** y **país** (aplica a todos / a este país).

**Estado actual / Brecha:** 🟡 **Parcial (la brecha más grande de los "parciales").** `EducationCareerCatalogItem` (`EducationCatalogItems.cs:55`) es un catálogo plano código/nombre (global/`SystemScoped`). **Faltan TODOS** los atributos de la spec: **Abreviatura, Incremento (numérico), Reconocida (booleano), FK a Tipo de estudio, País.** Hoy `PersonnelFileEducation` guarda Tipo de estudio y Carrera como **dos FKs independientes** — la relación carrera→tipo-de-estudio **no existe**. Además, pasar a país choca con BT-03 (el modelo actual no tiene "aplica a todos").

**Reglas de negocio:** carrera pertenece a un tipo de estudio; incremento numérico ≥ 0; reconocida booleano; alcance de país según D-13; código único (por país si se vuelve country-scoped).

**Criterios de aceptación:** (a) el ítem persiste los 5 atributos; (b) el selector de carrera puede filtrarse por tipo de estudio; (c) lectura por API incluye todo; (d) semilla SV; (e) decisión de país aplicada.

**Prioridad:** Media. **Dependencias:** RF-008; **D-13** (modelo de país); reestructura de FKs de educación.

---

### RF-010 — Beneficios adicionales (crear **catálogo de tipos**)

**Descripción:** Crear el **catálogo de tipos de beneficio adicional** (descripción + país que aplica) y migrar el campo de tipo del registro por-empleado de texto libre a catálogo.

**Estado actual / Brecha:** 🟡 **Parcial.** El registro por empleado `PersonnelFileAdditionalBenefit` (`PersonnelFileEmployee.cs:353`) existe con CRUD, pero su tipo es **texto libre** (`BenefitTypeCode`, validado solo `NotEmpty`+`MaxLength(80)`). **No existe** catálogo de tipos con descripción + país. (El `JobCatalogItem`/`BenefitType` de perfiles de puesto es tenant-scoped, sin descripción ni país → **no reutilizable**.)

**Reglas de negocio:** código único por país; descripción; país que aplica (D-13); validación server-side; migrar valores de texto libre existentes.

**Criterios de aceptación:** (a) catálogo con lectura por API; (b) el registro de beneficio del empleado usa el código de catálogo; (c) inválido → 422; (d) semilla SV; (e) datos previos migrados.

**Prioridad:** Media. **Dependencias:** D-13 (país).

---

### RF-011 — Tipos de contratos (agregar **Abreviatura** y **Temporal**)

**Descripción:** Ampliar el catálogo de **tipos de contrato** con **Abreviatura** y bandera **Temporal** (sí/no).

**Estado actual / Brecha:** 🟡 **Parcial.** `ContractTypeCatalogItem` (`GeneralCatalogItems.cs:764`) existe, sembrado SV (8 valores, mig `20260627212537`) y validado en historial de contratos. **Faltan:** columna **Abreviatura** y **booleano Temporal**. (Existe un *valor* con código `TEMPORAL`, pero no una bandera por fila.)

**Reglas de negocio:** descripción (=`Name`), abreviatura, temporal booleano; código único por país.

**Criterios de aceptación:** (a) el ítem persiste abreviatura y temporal; (b) lectura por API incluye ambos; (c) semilla SV marca correctamente cuáles son temporales; (d) el consumo en contratación puede leer la bandera Temporal.

**Prioridad:** Media. **Dependencias:** patrón de catálogo enriquecido (como `BankCatalogItem`).

---

### RF-012 — Rubros salariales (explicitar "**Es salario base**")

**Descripción:** Revalidar el catálogo de **rubros salariales** contra el modelo actual y **explicitar** el atributo "es salario base".

**Estado actual / Brecha:** 🟡 **Parcial (funcionalmente cubierto, estructuralmente divergente).** El legado `SalaryItem` fue **eliminado** (mig `20260622000049`) y unificado en `CompensationConceptTypeCatalogItem` (`CompensationConceptTypeCatalogItem.cs:13`), un catálogo **enriquecido** (naturaleza ingreso/egreso, tasas, base de cálculo, etc.). La descripción está cubierta (`Name`). **"Es salario base" NO es un booleano**: se identifica por el **código mágico** `SALARIO_BASE` (`CompensationConcepts.Rules.cs:15`). El modelo actual **excede** la spec simple.

**Reglas de negocio:** decidir (D-14) entre: (a) **aceptar** el enfoque por código y documentarlo, o (b) **agregar** un booleano explícito `IsBaseSalary` por claridad/consistencia con la spec.

**Criterios de aceptación:** (a) queda documentado que rubros = `CompensationConceptType`; (b) si D-14 = booleano, el catálogo expone `IsBaseSalary` y la regla se apoya en él; (c) no se rompe el módulo de compensación.

**Prioridad:** Baja (mayormente validación/documentación). **Dependencias:** módulo de Compensación; D-14.

---

### RF-013 — Formas de pago (sembrar "**boleta de pago**")

**Descripción:** Revalidar el catálogo de **formas de pago**; agregar el valor faltante "boleta de pago".

**Estado actual / Brecha:** ✅ **Completo con gap de dato.** `PaymentMethodCatalogItem` (`GeneralCatalogItems.cs:535`) existe, sembrado SV (TRANSFERENCIA/CHEQUE/EFECTIVO), validado en la plaza (`EmploymentAssignments.Handlers.cs`). **Falta** sembrar **BOLETA (boleta de pago)** y cualquier otro valor deseado.

**Reglas de negocio:** código único por país; activo/inactivo.

**Criterios de aceptación:** (a) el catálogo incluye "boleta de pago"; (b) seleccionable en la configuración de pago de la plaza.

**Prioridad:** Baja (quick win). **Dependencias:** ninguna.

---

### RF-014 — **(Nuevo, derivado)** Catálogo de Nivel educativo

**Descripción:** Crear el catálogo de **Nivel educativo** requerido por "Tipos de estudios" (RF-008).

**Estado actual / Brecha:** ❌ **No existe.** Es prerequisito de RF-008.

**Reglas de negocio:** código único; activo/inactivo; ordenable (representa progresión educativa).

**Criterios de aceptación:** (a) catálogo con lectura por API; (b) referenciable desde Tipos de estudios; (c) semilla SV (p. ej. Básico, Medio, Técnico, Superior/Universitario, Posgrado).

**Prioridad:** Media. **Dependencias:** ninguna (bloquea RF-008).

---

### RF-015 — **(Transversal, sujeto a D-15)** Mantenimiento (CRUD) de catálogos

**Descripción:** Proveer administración en tiempo de ejecución para los catálogos que el negocio requiera mantener sin migración (varias specs dicen "*dar mantenimiento*").

**Estado actual / Brecha:** BT-01. La familia `GeneralCatalogItem` es **solo lectura + semilla**; solo `Reference`/`Education` tienen CRUD y es de **operador de plataforma** (backoffice), no autoservicio de RR. HH.

**Reglas de negocio:** definir **quién** administra (operador de plataforma vs. admin de RR. HH.), **qué** catálogos son administrables y las políticas (Read/Manage). `AuthorizationPolicySet` es a nivel de clase → catálogos con política propia requieren **controlador dedicado**.

**Criterios de aceptación:** (a) los catálogos marcados como administrables exponen CRUD con RBAC; (b) alta/edición/activar/inactivar; (c) auditoría de cambios.

**Prioridad:** Media (depende de la respuesta del negocio). **Dependencias:** **D-15**.

---

## 7. Requerimientos no funcionales

- **RNF-01 · Seguridad / RBAC.** Lectura de catálogos autenticada; escritura (si aplica RF-015) tras política Manage. Códigos siempre validados server-side (no confiar en el cliente).
- **RNF-02 · Rendimiento.** Catálogos pequeños y cacheables; lectura por `countryCode`; índices `(country, code)` únicos y `(country, isActive, sortOrder)` de navegación.
- **RNF-03 · Disponibilidad / Siembra.** La data mínima requerida por el servidor debe existir vía `HasData` (BT-02) para no romper formularios en producción.
- **RNF-04 · Escalabilidad multi-país.** El modelo debe permitir sembrar por país; la decisión "aplica a todos" (BT-03/D-13) no debe bloquear SV.
- **RNF-05 · Usabilidad.** Selectores con nombre legible y orden lógico (`SortOrder`); mensajes de validación claros (incl. el formato esperado del documento).
- **RNF-06 · Auditoría.** Alta/cambio de valores de catálogo y de datos del expediente con trazabilidad (usuario, fecha, antes/después) siguiendo el patrón existente.
- **RNF-07 · Compatibilidad / Contrato.** Convención de serialización: `XxxId` (Guid) → `xxxPublicId`; enums como strings; catálogos referenciados por `Code`; concurrencia optimista con `If-Match`. No romper el contrato de openapi.
- **RNF-08 · Mantenibilidad.** Reutilizar los patrones de catálogo (entidad + config + seed + wire key + migración); guardrails de bijección (`GeneralCatalogKeyMapGuardrailsTests`) al agregar claves.
- **RNF-09 · Integridad de datos.** Migración de texto libre → catálogo con backfill que preserve el dato; unicidad por código; FKs con guardas.
- **RNF-10 · Accesibilidad / i18n.** Errores bilingües (EN/ES) como el resto del backend; nombres de catálogo aptos para UI accesible.

---

## 8. Historias de usuario

### HU-001 — Título personal en el expediente
Como **capturista de expediente**, quiero **seleccionar el título/tratamiento** de la persona de un catálogo, para **registrar cómo prefiere ser tratada** de forma consistente.
**Criterios:** Dado el catálogo sembrado, cuando abro el formulario personal, entonces veo el selector; cuando elijo "Ing." y guardo, entonces se persiste; cuando envío un código inexistente, entonces 422.

### HU-002 — Tipo de dirección
Como **capturista**, quiero **clasificar cada dirección por tipo**, para **distinguir casa/trabajo/facturación**.
**Criterios:** Dado el catálogo, cuando agrego una dirección, entonces selecciono su tipo; código inválido → 422.

### HU-003 — Validación del número de documento por formato
Como **RR. HH.**, quiero que **el número de documento se valide según el formato del tipo** (p. ej. DUI `########-#`), para **evitar capturas erróneas**.
**Criterios:** Dado un tipo con formato, cuando ingreso un número que no cumple, entonces 422 con el formato esperado; tipo sin formato → validación genérica.

### HU-004 — Parentesco en contacto de emergencia
Como **capturista**, quiero **elegir el parentesco del contacto de emergencia de catálogo**, para **estandarizar** (hoy es texto libre).
**Criterios:** Dado el catálogo Kinship, cuando registro el contacto, entonces selecciono el parentesco; datos previos migrados.

### HU-005 — Hobbies/Asociaciones de catálogo
Como **empleado (autoservicio)**, quiero **seleccionar mis hobbies y asociaciones de una lista**, para **capturarlos de forma consistente**.
**Criterios:** Dado el catálogo, cuando edito mis intereses, entonces elijo de la lista; (opcional) "Otro" si D-11 lo permite.

### HU-006 — AFP del empleado con parámetros
Como **RR. HH.**, quiero **registrar la AFP del empleado y su información/parámetros**, para **soportar la afiliación y (a futuro) el cálculo de nómina**.
**Criterios:** Dado el catálogo maestro de AFP, cuando asigno la AFP al empleado, entonces se persiste; AFP inactiva → 422; los parámetros están disponibles para nómina.

### HU-007 — Carrera ligada a tipo de estudio
Como **RR. HH.**, quiero que **la carrera esté relacionada con su tipo de estudio** y con abreviatura/incremento/reconocida, para **completar la educación del empleado con datos ricos**.
**Criterios:** Dado el catálogo ampliado, cuando selecciono una carrera, entonces puedo filtrar por tipo de estudio y ver sus atributos.

### HU-008 — Tipo de contrato temporal
Como **RR. HH.**, quiero **saber si un tipo de contrato es temporal**, para **aplicar reglas de contratación**.
**Criterios:** Dado el catálogo con bandera Temporal, cuando consulto un tipo, entonces obtengo su abreviatura y si es temporal.

### HU-009 — Forma de pago "boleta de pago"
Como **RR. HH.**, quiero que **"boleta de pago" esté disponible** como forma de pago, para **configurarla en la plaza**.
**Criterios:** Dado el catálogo sembrado con el valor, cuando configuro el pago, entonces puedo elegir "boleta de pago".

### HU-010 — (Sujeto a D-15) Mantener catálogos
Como **administrador** (de plataforma o RR. HH., según D-15), quiero **crear/editar/activar/inactivar valores de catálogo**, para **mantenerlos sin depender de una migración**.
**Criterios:** Dado el permiso Manage, cuando creo/edito un valor, entonces se persiste con auditoría; sin permiso → 403.

---

## 9. Reglas de negocio

- **RN-01.** Todo valor de catálogo tiene **Código** (único por su ámbito: país o global), **Nombre**, **Activo/Inactivo** y **Orden**.
- **RN-02.** Las entidades del expediente referencian el catálogo por **Código**; el servidor valida que exista y esté **activo** (código inválido/inactivo → **422**).
- **RN-03.** La **siembra mínima requerida** por el servidor va por **`HasData`** (todos los ambientes), nunca solo por `DevSeed`.
- **RN-04.** Catálogos **country-scoped**: unicidad `(país, código)`; catálogos **globales**: unicidad por código normalizado. **No existe** bandera "aplica a todos" (BT-03).
- **RN-05.** Catálogos **jerárquicos** se modelan con **FK a otra entidad de catálogo** (p. ej. Municipio→Departamento, Carrera→Tipo de estudio, Tipo de estudio→Nivel educativo), no con `ParentId` autorreferencial.
- **RN-06.** Al **migrar texto libre → catálogo** (hobbies, asociaciones, tipo de beneficio, parentesco de contacto de emergencia), se **preservan** los datos existentes (backfill/mapeo).
- **RN-07.** El **número de documento** se valida contra el **formato del tipo** cuando este esté definido; si no hay formato, aplica la validación genérica.
- **RN-08.** Los **parámetros de AFP** son numéricos con la precisión del proyecto (% `numeric(11,8)`, monto `numeric(18,2)`); mínimo ≤ máximo de cotización.
- **RN-09.** "**Es salario base**" se determina hoy por el código `SALARIO_BASE`; si D-14 lo aprueba, se explicita como booleano.
- **RN-10.** Los **códigos** se normalizan (trim + mayúsculas); los nombres tienen longitud máxima (código ≤ 80, nombre ≤ 200).
- **RN-11.** **Concurrencia optimista** en escritura (token `If-Match`); enums en el contrato como **strings**; `XxxId` (Guid) se serializa como `xxxPublicId`.
- **RN-12.** El **mantenimiento (CRUD)** de un catálogo, si aplica, respeta las políticas Read/Manage; un catálogo con política propia vive en **controlador dedicado** (`AuthorizationPolicySet` es a nivel de clase).

---

## 10. Flujos principales

**Flujo A — Consumo de catálogo en el expediente (lectura):**
1. El usuario abre un formulario del expediente (p. ej. datos personales, educación, contrato).
2. El frontend solicita el catálogo por su **wire key** (`GET api/v1/general-catalogs/{key}` o `.../reference-catalogs/{key}`), con `countryCode`.
3. El sistema devuelve los ítems **activos** ordenados por `SortOrder`.
4. El usuario selecciona un valor; el frontend envía el **código**.
5. Al guardar, el servidor **valida** el código contra el catálogo activo.
6. Si es válido, persiste; si no, responde **422**.

**Flujo B — Alta/edición de un valor de catálogo (mantenimiento, si D-15):**
1. El administrador (plataforma o RR. HH.) abre la administración del catálogo.
2. Crea/edita un valor (código, nombre, atributos, activo).
3. El sistema valida unicidad y reglas (formato, FKs, parámetros).
4. Persiste con **auditoría** y token de concurrencia.
5. El valor queda disponible para los formularios.

**Flujo C — Validación del número de documento por formato (RF-003):**
1. El usuario selecciona el **tipo de documento**.
2. El sistema recupera el **formato** definido para ese tipo.
3. El usuario ingresa el número.
4. El sistema valida el número contra el formato (si existe).
5. Cumple → persiste; no cumple → **422** con el formato esperado.

**Flujo D — Migración de texto libre a catálogo (RF-004/005/006/010):**
1. Se crea el catálogo y se siembra.
2. Migración de datos: se **mapea/normaliza** el texto libre existente a códigos (los no mapeables se marcan/registran).
3. El campo pasa a referenciar el catálogo; nuevas capturas usan selector.

---

## 11. Flujos alternativos y excepciones

- **E-01 · Código inexistente o inactivo** → 422 (`ErrorCatalog.Validation` / error específico del módulo) con la clave del campo.
- **E-02 · Falta el catálogo en el servidor** (sembrado solo por DevSeed) → lista vacía en producción (BT-02); se previene sembrando por `HasData`.
- **E-03 · Número de documento no cumple formato** → 422 con mensaje bilingüe indicando el patrón.
- **E-04 · Formato del tipo no definido** → se omite la validación de patrón (queda la genérica).
- **E-05 · País no soportado** → no hay ítems para ese país (BT-03); definir fallback/decisión (D-13).
- **E-06 · Concurrencia** → escritura sin `If-Match` → 400; token obsoleto → 409.
- **E-07 · Migración con valor no mapeable** (texto libre exótico) → registrar y decidir mapeo a "Otro" o corrección manual (D-11).
- **E-08 · AFP: parámetros incoherentes** (mínimo > máximo, tasas fuera de rango) → 422.
- **E-09 · Borrado/inactivación de un valor en uso** → impedir borrado físico o inactivar preservando referencias históricas (snapshot de nombre donde aplique).
- **E-10 · Reconciliación de tasas AFP** (D-07): si la nómina toma las tasas del concepto genérico y no de la AFP, documentar y evitar doble fuente de verdad.

---

## 12. Datos requeridos

> Campos base heredados por catálogos country-scoped: `Code`, `NormalizedCode`, `Name`, `NormalizedName`, `IsActive`, `SortOrder`, `CountryCode`, `CountryCatalogItemId`, `ConcurrencyToken`. Los globales omiten los de país.

### Entidad: PersonalTitleCatalogItem (RF-001) — nuevo

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Code | Texto | Sí | único por país, ≤80, normalizado | Código (ING, SR, SRA…) |
| Name | Texto | Sí | ≤200 | Nombre visible (Ingeniero, Señor…) |
| IsActive | Booleano | Sí | — | Activo/inactivo |
| SortOrder | Número | Sí | — | Orden en UI |
| *(persona)* PersonalTitleCode | Texto | No | código activo | Título asignado a la persona |

### Entidad: AddressTypeCatalogItem (RF-002) — nuevo
Campos base + `AddressTypeCode` en `PersonnelFileAddress` (obligatorio/opcional según D-10). Semilla SV sugerida: CASA, TRABAJO, FACTURACION, TEMPORAL.

### Entidad: IdentificationTypeCatalogItem (RF-003) — **ampliar**

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| *(existente)* Code/Name/IsActive/… | — | — | — | Ya existe |
| **NumberFormat** | Texto (regex/máscara) | No | patrón válido | Formato del número por tipo (nuevo) |
| *(consumo)* IdentificationNumber | Texto | Sí | cumple `NumberFormat` si está definido | Número del documento (validación nueva) |

### Entidad: HobbyCatalogItem (RF-005) / AssociationCatalogItem (RF-006) — nuevos
Campos base. El registro por-empleado referencia el código; Asociaciones conserva `Role/JoinedDate/LeftDate/Payment`.

### Entidad: AfpCatalogItem (RF-007) — nuevo (enriquecido)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Code / Abbreviation | Texto | Sí | único por país | Código / **Abreviatura** |
| Name | Texto | Sí | ≤200 | Nombre de la AFP |
| Address | Texto | No | — | Dirección |
| Phone | Texto | No | formato teléfono | Teléfono |
| Fax | Texto | No | — | Fax |
| ContactName | Texto | No | — | Nombre de contacto |
| CountryCode | Texto | Sí | ISO país | País |
| EmployeeRate | Número (%) | Sí | 0–100, `numeric(11,8)` | Cuota de empleado |
| EmployerRate | Número (%) | Sí | 0–100 | Cuota de patrono |
| PensionedEmployerRate | Número (%) | Sí | 0–100 | Cuota patronal para pensionado |
| MinContributionBase | Monto | Sí | ≥0, `numeric(18,2)`, ≤ máx | Valor mínimo de cotización |
| MaxContributionBase | Monto | Sí | ≥ mín | Valor máximo de cotización |
| IsActive / SortOrder | — | Sí | — | Estado / orden |
| *(empleado)* AfpCode | Texto | No | AFP activa | AFP del empleado |

### Entidad: EducationLevelCatalogItem (RF-014) — nuevo
Campos base (global o país según D-13). Semilla SV sugerida: BASICO, MEDIO, TECNICO, SUPERIOR, POSGRADO.

### Entidad: EducationStudyTypeCatalogItem (RF-008) — **ampliar**
+ `Abbreviation` (texto) · + `EducationLevelId` (FK a RF-014).

### Entidad: EducationCareerCatalogItem (RF-009) — **ampliar**
+ `Abbreviation` (texto) · + `Increment` (número ≥0) · + `IsRecognized` (booleano) · + `StudyTypeId` (FK a RF-008) · + país (según D-13).

### Entidad: AdditionalBenefitTypeCatalogItem (RF-010) — nuevo
Campos base (país) + `Description`. `PersonnelFileAdditionalBenefit.BenefitTypeCode` pasa de texto libre a código validado.

### Entidad: ContractTypeCatalogItem (RF-011) — **ampliar**
+ `Abbreviation` (texto) · + `IsTemporary` (booleano).

### Entidad: PaymentMethodCatalogItem (RF-013) — **sembrar**
Sin cambios de esquema; agregar valor **BOLETA** (boleta de pago).

### Entidad: CompensationConceptTypeCatalogItem (RF-012) — **decisión**
Opcional (D-14): + `IsBaseSalary` (booleano) para explicitar lo que hoy resuelve el código `SALARIO_BASE`.

---

## 13. Integraciones necesarias

- **INT-01 · API de lectura de catálogos (interna):** `GeneralCatalogsController` (`general-catalogs` / `reference-catalogs`) + whitelist en `GeneralCatalogKeyMap`. Cada catálogo nuevo agrega su **wire key** + constante de categoría (guardrail de bijección).
- **INT-02 · Backoffice (operador de plataforma):** `SystemCatalogsController` / `EducationCatalogsController` para CRUD de las familias que lo soportan; base para RF-015.
- **INT-03 · Módulo de Compensación / Nómina (futuro):** consumirá AFP, rubros (`CompensationConceptType`) y formas de pago. Reconciliar tasas AFP (D-07).
- **INT-04 · Persistencia / Migraciones:** `GlobalCatalogSeedData` (`HasData`) + migraciones EF (`DOTNET_ROLL_FORWARD=Major`; límite de 63 caracteres en nombres de índice).
- **INT-05 · Sin integraciones externas** (AFP, bancos, registros académicos) en este alcance (FA-06).

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Empleado (autoservicio)** | Leer catálogos; capturar sus datos con valores de catálogo (según módulo) | Solo su propio expediente; sin administrar catálogos |
| **Capturista / Analista RR. HH.** | Leer catálogos; registrar/editar datos del expediente | Sin CRUD de catálogos (salvo D-15) |
| **Administrador RR. HH. (tenant)** | Leer catálogos; **(D-15)** administrar los catálogos habilitados a nivel tenant | Solo su tenant; catálogos globales no editables |
| **Operador de plataforma (Backoffice)** | CRUD de catálogos `Reference`/`Education` (y nuevos) | Política `PlatformOperator`; alcance global |
| **Sistema** | Validar códigos y formato; sembrar por `HasData` | — |

> Nota: la lectura de catálogos hoy es **authn-only** (sin RBAC fino); la escritura requiere política Manage/`PlatformOperator`. Catálogos con política propia → **controlador dedicado**.

---

## 15. Criterios de aceptación generales

- **CA-01.** Los 14 catálogos quedan **revalidados**: cada uno existe con los campos exigidos por su spec, o está documentado por qué diverge (rubros).
- **CA-02.** Los catálogos nuevos y los campos nuevos **se leen por API** y **se validan server-side** en todos los puntos de consumo (422 ante inválido/inactivo).
- **CA-03.** Toda la **semilla SV** requerida por el servidor está por **`HasData`** (no queda catálogo vacío en producción).
- **CA-04.** El **número de documento** se valida por formato del tipo (cuando aplica).
- **CA-05.** Los campos migrados de **texto libre a catálogo** conservan los datos existentes.
- **CA-06.** La **AFP** dispone de todos los campos e-parámetros y el empleado puede asociarse a una; la fuente de tasas para nómina queda definida (D-07).
- **CA-07.** No hay **regresiones** en compilación ni en las suites de pruebas (unitarias + integración) ni **drift** de migraciones; guardrails de catálogo (bijección) verdes.
- **CA-08.** El contrato de API (openapi) se mantiene consistente (convención `xxxPublicId`, enums como strings, `If-Match`).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R-01.** **AFP subestimada:** es una entidad rica con parámetros de cálculo y una **reconciliación no trivial** con las tasas ya existentes en compensación; tratarla como "un catálogo más" puede duplicar la fuente de verdad de tasas (D-07).
- **R-02.** **Carreras/país (BT-03):** el modelo actual no soporta "aplica a todos los países"; forzarlo puede requerir cambiar la clase base o introducir una bandera nueva, con impacto en índices y semilla.
- **R-03.** **Migración de texto libre:** valores heredados inconsistentes (hobbies/asociaciones/beneficios) pueden no mapear limpiamente al catálogo.
- **R-04.** **Expectativa de "mantenimiento":** varias specs asumen CRUD que **hoy no existe** para la familia `General`; si el negocio espera autoservicio, es trabajo adicional (RF-015/D-15).
- **R-05.** **Formato de documento:** definir patrones incorrectos (o demasiado estrictos) puede bloquear capturas válidas; requiere validación con datos reales SV.
- **R-06.** **Colisión de siembra dev↔prod:** mover un catálogo de DevSeed a HasData puede colisionar en DBs de desarrollo persistentes (requiere limpieza `id>0`, lección ya documentada en el proyecto).
- **R-07.** **Alcance amplio:** 14 catálogos con niveles de esfuerzo muy dispares; sin fasear, el entregable se vuelve monolítico y arriesgado.

### Supuestos
- **S-01.** El país objetivo inicial es **El Salvador (SV)**; otros países se difieren.
- **S-02.** El **cálculo de nómina** es un módulo futuro; aquí solo se **configura**.
- **S-03.** Se **reutilizan** las convenciones de catálogo existentes (entidad+config+seed+wire key+migración).
- **S-04.** No hay integraciones externas en este alcance.
- **S-05.** El modelo `CompensationConcept` es la **respuesta oficial** a "rubros salariales" (no se reintroduce `SalaryItem`).
- **S-06.** Los datos productivos existentes en campos de texto libre son **acotados** y migrables.

### Dependencias
- **D(dep)-01.** Patrón de catálogo país (`PersonnelReferenceCatalog` / `GeneralCatalog`) y global (`Education`/`System`).
- **D(dep)-02.** `GlobalCatalogSeedData` + pipeline de migraciones (`HasData`/`InsertData`).
- **D(dep)-03.** Módulo de Compensación (rubros) y futuro de Nómina (AFP).
- **D(dep)-04.** Convenciones de contrato/serialización y de permisos (`AuthorizationPolicySet` a nivel de clase).
- **D(dep)-05.** Motor de validación por regex (nuevo) para el formato de documento.

---

## 17. Preguntas abiertas para el cliente o stakeholders

> **ESTADO: RATIFICADAS 2026-06-30.** Todas las decisiones fueron respondidas — ver la tabla de **§19 (Decisiones ratificadas)** y las listas de valores en **§20 (Seed inicial)**. Se conservan aquí como referencia del planteamiento original.

- **D-01.** ¿Los catálogos nuevos deben ser **country-scoped (por país)** o **globales**? (Convención del proyecto favorece país; Hobbies/Asociaciones podrían ser globales.)
- **D-02.** **Títulos personales:** ¿lista sugerida (Ing., Lic., Dr., Dra., Sr., Sra., Srta., Prof., …)? ¿El campo en la persona es opcional?
- **D-03.** **Tipos de dirección:** ¿catálogo de valores esperado (Casa, Trabajo, Facturación, Temporal, Otra)? ¿el tipo es obligatorio en cada dirección?
- **D-04.** **Documentos — formato:** ¿qué patrones exactos por tipo? (DUI `########-#`, NIT `####-######-###-#`, Pasaporte alfanumérico, etc.) ¿La validación es bloqueante (422) o solo advertencia?
- **D-05.** **AFP:** ¿lista de AFP a sembrar (Confía, Crecer, …)? ¿Los **parámetros** (cuotas, topes) se cargan por AFP o son iguales por ley? ¿Dónde se afilia el empleado a la AFP (expediente/compensación)?
- **D-06.** **AFP — tasas (crítico):** ¿la **nómina** usará las tasas de la **AFP** o las del **concepto genérico** `AFP` de compensación? (Evitar doble fuente de verdad — R-01.)
- **D-07.** **Carreras/Beneficios — país:** ¿se requiere realmente "aplica a todos los países" o basta país obligatorio? (BT-03 impacta el diseño base.)
- **D-08.** **Nivel educativo:** ¿valores esperados (Básico, Medio, Técnico, Superior/Universitario, Posgrado)? ¿La FK en Tipos de estudios es obligatoria?
- **D-09.** **Carreras — "Incremento":** ¿qué representa (porcentaje/monto de incremento salarial por grado)? ¿unidad y uso?
- **D-10.** **Contratos:** ¿lista de tipos y **cuáles son temporales**? ¿La abreviatura tiene un estándar?
- **D-11.** **Migración de texto libre:** para Hobbies/Asociaciones/Beneficios/Contacto de emergencia, ¿mapear a catálogo, permitir "Otro" o requerir curación manual?
- **D-12.** **Rubros salariales:** ¿se acepta el modelo actual (`CompensationConceptType` + código `SALARIO_BASE`) o se exige un **booleano** `IsBaseSalary` explícito?
- **D-13.** **Formas de pago:** además de "boleta de pago", ¿otros valores a sembrar?
- **D-14.** **Mantenimiento (CRUD):** ¿RR. HH. del tenant debe **administrar** catálogos en tiempo real, o basta con que el **operador de plataforma** los mantenga (backoffice)? ¿Qué catálogos son administrables por tenant?
- **D-15.** **Alcance/fases:** ¿se aprueba el faseo propuesto en §18 (con AFP como mini-proyecto separado y MVP = quick wins + catálogos simples)?
- **D-16.** **Datos históricos:** ¿existen datos productivos en los campos de texto libre que deban preservarse/migrarse? ¿volumen aproximado?

---

## 18. Recomendaciones del Analista de Negocio

### 18.1. Recomendación general
El entregable es **heterogéneo**: mezcla quick wins (un valor de semilla), ampliaciones de columnas (abreviatura/temporal), migraciones de texto libre y **un mini-proyecto** (AFP). Recomiendo **NO tratarlo como un bloque único**, sino **fasearlo por esfuerzo/valor** y **ratificar D-01…D-16 antes** de construir, porque varias decisiones (país, tasas de AFP, mantenimiento) cambian el diseño base.

### 18.2. MVP sugerido (cierre rápido del grueso del entregable)
**MVP = Fase 0 + Fase 1.** Cierra **9 de 14** catálogos con bajo riesgo, dejando AFP y educación estructurada para fases dedicadas.

### 18.3. Faseo propuesto

- **Fase 0 — Quick wins (días).**
  - RF-013: sembrar "boleta de pago" (Formas de pago).
  - RF-012: **decisión** sobre rubros (documentar o agregar booleano) — D-12.
  - RF-004: enlazar contacto de emergencia al catálogo Parentesco (+ migración de texto libre).
  - *Cierra/valida:* Parentesco, Formas de pago, Rubros.

- **Fase 1 — Catálogos simples nuevos + ampliaciones de columna.**
  - RF-001 Títulos personales, RF-002 Tipos de direcciones (catálogo + campo).
  - RF-005 Hobbies, RF-006 Asociaciones (catálogo + migración de texto libre).
  - RF-010 Beneficios adicionales (catálogo de tipos + migración).
  - RF-011 Tipos de contratos (+Abreviatura +Temporal).
  - RF-008 (parte): Abreviatura en Tipos de estudios.
  - *Cierra:* 5 nuevos + 2 ampliaciones simples.

- **Fase 2 — Educación estructurada.**
  - RF-014 Nivel educativo (nuevo) → RF-008 (FK) → RF-009 Carreras (+Abreviatura +Incremento +Reconocida +FK a Tipo de estudio +País).
  - Requiere **D-08/D-09/D-07** (país). Reestructura de FKs de educación.

- **Fase 3 — Documentos con formato.**
  - RF-003: columna de formato + **motor de validación** por regex + patrones SV (D-04). Es la pieza con más valor de calidad de dato y con desarrollo nuevo (BT-04).

- **Fase 4 — AFP maestro (mini-proyecto).**
  - RF-007: entidad rica + afiliación del empleado + **reconciliación de tasas** con compensación (D-05/D-06). Mayor esfuerzo y decisiones; se ejecuta aparte.

- **Fase transversal — Mantenimiento (si D-14 lo aprueba).**
  - RF-015: CRUD por API para los catálogos que el negocio requiera administrar (controladores dedicados por política).

### 18.4. Recomendaciones de diseño
- **Reutilizar el patrón canónico** de catálogo (entidad `: GeneralCatalogItem`/`PersonnelReferenceCatalogItemBase` + config + `GlobalCatalogSeedData` + wire key + migración) para minimizar riesgo; los catálogos enriquecidos (AFP, contratos con temporal) siguen el patrón `BankCatalogItem`/`CompensationConceptType` (columnas extra).
- **Sembrar siempre por `HasData`** (BT-02) e incluir el prepend de limpieza `id>0` si el catálogo pasa de DevSeed a HasData (evita colisiones en DBs de desarrollo).
- **Definir país temprano (D-07/BT-03):** si se requiere "aplica a todos", diseñar la bandera antes de construir Carreras/Beneficios.
- **AFP: una sola fuente de verdad de tasas** (D-06). Si la ley SV fija tasas iguales para todas las AFP, considerar parámetros a nivel país y no por AFP, dejando en la AFP solo identidad/contacto.
- **Migraciones de texto libre con backfill** y política clara de "no mapeables" (D-11).
- **Validación de formato de documento** con regex ancladas y mensajes que muestren el patrón esperado; validar los patrones con datos reales antes de hacerlos bloqueantes.

---

## 19. Decisiones ratificadas (D-01…D-16) — 2026-06-30

| # | Tema | Ratificación |
|---|---|---|
| **D-01** | Ámbito de catálogos nuevos | **Country-scoped SV** (patrón `GeneralCatalog`/`PersonnelReference`), **excepto Nivel educativo** que es **global** (coherente con la familia `Education`). |
| **D-02** | Títulos personales | Campo **opcional** en la persona (`PersonalTitleCode`). Valores → §20.1. |
| **D-03** | Tipos de dirección | Campo tipo **opcional** en la dirección. Valores → §20.2. |
| **D-04** | Formato de documento | **Bloqueante (422)** con **regex ancladas sembradas por tipo**; tipos sin patrón → validación genérica. Patrones → §20.3. |
| **D-05** | AFP — afiliación | El empleado referencia **una AFP** vía `AfpCode` a **nivel empleado** (informativo/afiliación). Lista → §20.7. |
| **D-06** | AFP — tasas/parámetros | **A nivel país** (concepto de compensación); el catálogo AFP guarda **solo identidad/contacto/abreviatura**. **Una sola fuente de verdad.** Parámetros país → §20.7. |
| **D-07** | País (Carreras/Beneficios) | **País obligatorio, solo SV**; **sin** bandera "aplica a todos". **Carreras pasa a country-scoped SV** (conversión desde `SystemScoped`). |
| **D-08** | Nivel educativo | Catálogo **global nuevo**; FK en Tipos de estudios **poblada en el seed** (nullable + backfill para datos previos). Valores → §20.14. |
| **D-09** | Carreras — "Incremento" | **% de incremento salarial por grado** (RATIFICADO RT-03): `decimal` (0–100), lo consumirá Nómina para ponderar/ajustar salario; se siembra **0**. |
| **D-10** | Tipos de contrato | Se agregan **Abreviatura** + **Temporal (bool)**; marcado en §20.11. |
| **D-11** | Migración de texto libre | **Mapeo best-effort** (normalizado) a códigos; no mapeables → **OTRO** preservando el registro. Sin curación manual en esta fase. |
| **D-12** | Rubros salariales | Se **acepta** `CompensationConceptType` como respuesta **y** se **agrega booleano `IsBaseSalary`** (elimina el acoplamiento al código mágico `SALARIO_BASE`). |
| **D-13** | Formas de pago | Agregar **BOLETA** (boleta de pago) al seed. §20.13. |
| **D-14** | Mantenimiento (CRUD) | **Backoffice (operador de plataforma)** por ahora; autoservicio de RR. HH. **diferido**. Todo catálogo se entrega **con su seed inicial**. |
| **D-15** | Faseo | **Aprobado** F0…F4 + transversal; **MVP = F0 + F1** (cierra 9/14). |
| **D-16** | Datos históricos | **DROP & RECREATE autorizado** (RT-02/RT-06): no se preservan datos; se dropea y recrea la estructura de los catálogos reestructurados; **sin backfill**. |

---

## 20. Seed inicial por catálogo (obligatorio) — vía `HasData` → todos los ambientes

> **Principio ratificado (requisito duro):** **ningún catálogo se entrega vacío.** Toda la semilla va por `GlobalCatalogSeedData` (`HasData` → `InsertData` en migración → **todos los ambientes**, incluido producción; **nunca** solo `DevSeed`). País **SV** salvo donde se indique **(global)**. Códigos normalizados (mayúsculas, sin acentos). Los **valores legales de AFP** son **defaults editables**, a confirmar con Nómina/RR. HH. Todos los catálogos incluyen `OTRO/OTRA` como comodín y `SortOrder` incremental.

### 20.1. Títulos personales — `PersonalTitleCatalogItem` (SV, nuevo)
`ING` Ingeniero/a · `LIC` Licenciado/a · `ARQ` Arquitecto/a · `DR` Doctor · `DRA` Doctora · `MSC` Máster · `TEC` Técnico/a · `PROF` Profesor/a · `SR` Señor · `SRA` Señora · `SRTA` Señorita · `OTRO` Otro.

### 20.2. Tipos de direcciones — `AddressTypeCatalogItem` (SV, nuevo)
`CASA` Casa / Habitación · `TRABAJO` Trabajo · `FACTURACION` Facturación · `TEMPORAL` Temporal · `OTRA` Otra.

### 20.3. Tipos de documentos — formato (ampliación de `IdentificationTypeCatalogItem`, SV)
| Código | Nombre | `NumberFormat` (regex anclada) | Ejemplo |
|---|---|---|---|
| `DUI` | Documento Único de Identidad | `^\d{8}-\d$` | `01234567-8` |
| `NIT` | Número de Identificación Tributaria | `^\d{4}-\d{6}-\d{3}-\d$` | `0614-123456-101-2` |
| `PASSPORT` | Pasaporte | `^[A-Z0-9]{6,12}$` | `A1234567` |
| `RESIDENT_CARD` | Carné de Residente | `^[A-Za-z0-9-]{5,20}$` | (variable) |

> Patrones = **defaults editables**; anclados (`^…$`). Tipos sin patrón conservan la validación genérica. Migración añade la columna `number_format` (nullable) al catálogo existente.

### 20.4. Parentesco — `KinshipCatalogItem` (SV, **ya sembrado**, sin nuevo seed)
Acción: **enlazar** `PersonnelFileEmergencyContact.Relationship` al catálogo (hoy texto libre) + migrar valores previos (D-11). Valores existentes: `CONYUGE, PAREJA, PADRE, MADRE, HIJO_A, HERMANO_A, ABUELO_A, NIETO_A, TIO_A, OTRO`.

### 20.5. Hobbies — `HobbyCatalogItem` (SV, nuevo)
`DEPORTE` Deportes · `LECTURA` Lectura · `MUSICA` Música · `CINE` Cine y series · `VIAJES` Viajes · `COCINA` Cocina · `ARTE` Arte y pintura · `TECNOLOGIA` Tecnología · `FOTOGRAFIA` Fotografía · `JARDINERIA` Jardinería · `VOLUNTARIADO` Voluntariado · `OTRO` Otro.

### 20.6. Asociaciones — `AssociationCatalogItem` (SV, nuevo)
`SINDICATO` Sindicato · `COLEGIO_PROF` Colegio profesional · `CAMARA` Cámara empresarial/gremial · `ONG` ONG / Fundación · `CLUB` Club social o deportivo · `RELIGIOSA` Asociación religiosa · `COOPERATIVA` Cooperativa · `OTRA` Otra.

### 20.7. AFP — identidad + parámetros país (SV)
**(a) Catálogo maestro `AfpCatalogItem`** (identidad/contacto; contacto nullable, a completar):

| Código | Nombre | Abreviatura |
|---|---|---|
| `CONFIA` | AFP Confía | CONFIA |
| `CRECER` | AFP Crecer | CRECER |
| `OTRA` | Otra AFP | OTRA |

**(b) Parámetros de cálculo a nivel país** (D-06 — concepto de compensación `AFP`; **defaults editables, a confirmar**):

| Parámetro | Valor de ley SV (default editable) |
|---|---|
| Cuota de empleado | **7.25 %** (ya sembrado) |
| Cuota de patrono | **8.75 %** (LISP 2022; el +1% no se traslada al trabajador) |
| Cuota patronal para pensionado | **8.75 %** (igual; el pensionado que sigue trabajando cotiza igual, con devolución anual de la porción CIAP) |
| Valor mínimo de cotización (IBC mín.) | **= salario mínimo vigente** (~$365/mes según sector) — columna nueva |
| Valor máximo de cotización (IBC máx.) | **$7,045.06/mes** (parámetro 2026, actualizable por la SSF) — mapea al `ContributionCap` |

> **Fuente (RT-04):** Ley Integral del Sistema de Pensiones (2022, vigente ene-2023); total del sistema = **16 %** del IBC. `DefaultPensionedEmployerRate` y `MinContributionBase` son **columnas nuevas**; el `ContributionCap` existente = IBC máx. El empleado se afilia vía `AfpCode` a **nivel persona** (D-05/RT-05).

### 20.8. Tipos de estudios — `EducationStudyTypeCatalogItem` (SV; re-seed + Abreviatura + FK Nivel)
| Código | Nombre | Abrev. | Nivel educativo (FK → §20.14) |
|---|---|---|---|
| `BASICA` | Educación Básica | BAS | `BASICO` |
| `BACHILLERATO` | Bachillerato | BACH | `MEDIO` |
| `TECNICO` | Técnico / Tecnólogo | TEC | `TECNICO` |
| `UNIVERSITARIA` | Universitaria | UNIV | `SUPERIOR` |
| `POSGRADO` | Posgrado | POSG | `POSGRADO` |

> Reemplaza las 3 filas placeholder actuales (`BACHELOR/MASTER/TECHNICAL`) — requiere limpieza `id>0` y cuidado con las FK de educación (RESTRICT).

### 20.9. Carreras — `EducationCareerCatalogItem` (SV, country-scoped; + Abrev./Incremento/Reconocida/FK Tipo estudio)
| Código | Nombre | Abrev. | Reconocida | Increment | Tipo de estudio (FK → §20.8) |
|---|---|---|---|---|---|
| `ING_INDUSTRIAL` | Ingeniería Industrial | II | Sí | 0 | `UNIVERSITARIA` |
| `ING_SISTEMAS` | Ingeniería en Sistemas/Computación | IS | Sí | 0 | `UNIVERSITARIA` |
| `LIC_ADMIN` | Lic. Administración de Empresas | LAE | Sí | 0 | `UNIVERSITARIA` |
| `LIC_CONTADURIA` | Lic. Contaduría Pública | LCP | Sí | 0 | `UNIVERSITARIA` |
| `LIC_PSICOLOGIA` | Lic. Psicología | LP | Sí | 0 | `UNIVERSITARIA` |
| `LIC_DERECHO` | Lic. Ciencias Jurídicas | LCJ | Sí | 0 | `UNIVERSITARIA` |
| `TEC_COMPUTACION` | Técnico en Computación | TC | Sí | 0 | `TECNICO` |
| `MBA` | Maestría en Administración (MBA) | MBA | Sí | 0 | `POSGRADO` |
| `OTRA` | Otra carrera | OTRA | No | 0 | `UNIVERSITARIA` |

> **Incremento** = **% de incremento salarial por grado** (RT-03), `decimal` 0–100, sembrado en 0 (editable; lo consumirá Nómina). Catálogo **country-scoped SV** creado por **drop & recreate** (RT-02).

### 20.10. Beneficios adicionales — `AdditionalBenefitTypeCatalogItem` (SV, nuevo)
`SEGURO_VIDA` Seguro de vida · `SEGURO_MEDICO` Seguro médico privado · `BONO_ALIMENTACION` Bono de alimentación · `VALE_DESPENSA` Vale de despensa · `AYUDA_TRANSPORTE` Ayuda de transporte · `GIMNASIO` Gimnasio · `BECA_CAPACITACION` Beca / capacitación · `PLAN_TELEFONO` Plan de teléfono · `VEHICULO` Vehículo / combustible · `OTRO` Otro.

### 20.11. Tipos de contratos — `ContractTypeCatalogItem` (SV; + Abreviatura + Temporal)
| Código | Nombre | Abrev. | Temporal |
|---|---|---|---|
| `INDEFINIDO` | Contrato indefinido | INDEF | No |
| `PLAZO_FIJO` | Contrato a plazo fijo | PF | **Sí** |
| `POR_OBRA` | Contrato por obra o servicio | OBRA | **Sí** |
| `EVENTUAL` | Contrato eventual | EVEN | **Sí** |
| `APRENDIZAJE` | Contrato de aprendizaje | APREN | **Sí** |
| `SERVICIOS_PROFESIONALES` | Servicios profesionales | SP | No |
| `TEMPORAL` | Contrato temporal | TEMP | **Sí** |
| `OTRO` | Otro | OTRO | No |

### 20.12. Rubros salariales — `CompensationConceptTypeCatalogItem` (SV, **ya sembrado**; marcar `IsBaseSalary`)
Solo se **marca** el nuevo booleano: `SALARIO_BASE` → **`IsBaseSalary = Sí`**; el resto (`HORAS_EXTRA, COMISION, BONO, VIATICOS, AGUINALDO, OTRO_INGRESO, ISSS, AFP, RENTA`…) → **No**. Sin filas nuevas.

### 20.13. Formas de pago — `PaymentMethodCatalogItem` (SV, **ya sembrado**; agregar valor)
Existentes: `TRANSFERENCIA, CHEQUE, EFECTIVO`. **Nuevo:** `BOLETA` Boleta de pago.

### 20.14. Nivel educativo — `EducationLevelCatalogItem` (**global**, nuevo)
| Código | Nombre | Orden |
|---|---|---|
| `BASICO` | Básico | 1 |
| `MEDIO` | Medio | 2 |
| `TECNICO` | Técnico | 3 |
| `SUPERIOR` | Superior / Universitario | 4 |
| `POSGRADO` | Posgrado | 5 |

---

> **Próximos pasos:** (1) **decisiones ratificadas** (§19) y **seed inicial definido** (§20) ✅; (2) elaborar el **plan técnico** (`docs/technical/plan-tecnico-revalidacion-catalogos.md`) con el desglose de PRs por fase, incorporando el seed de §20 como entregable de cada catálogo; (3) construir **MVP (Fase 0 + Fase 1)**. Este documento queda como base de negocio cerrada para PO, PM, UX, QA y desarrollo.
