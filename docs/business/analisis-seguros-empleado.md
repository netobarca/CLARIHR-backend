# Análisis de Negocio — Seguros del Empleado y Beneficiarios

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Compensación/Beneficios (`PersonnelFileInsurance`, `PersonnelFileInsuranceBeneficiary`) · Catálogos de referencia país (`PersonnelReferenceCatalog`) · Catálogo de monedas ISO-4217 · Identidad/Permisos (`IdentityAccess`/Provisioning) · Auditoría (`IAuditService`) |
| **Estado** | **Definido / Cerrado (Fase 1).** Decisiones ratificadas **D-01…D-15** (respuestas del negocio del 2026-06-22). Funcionalidad **ya implementada** (`PersonnelFileInsurance` + `PersonnelFileInsuranceBeneficiary`); el trabajo es **endurecimiento y alineación**: catalogar nombre de seguro y rango (alcance **país**), tipo de documento y asignación de beneficiarios, moneda ISO-4217, anti-duplicado, permiso dedicado e historial/diff. **Listo para diseño técnico.** |
| **Versión** | v2 (incorpora decisiones del negocio P-01…P-11) |
| **Fecha** | 2026-06-22 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |

---

## Contexto del cambio

En el **expediente del empleado** existe una sección de **Seguros** para registrar los seguros a los que el empleado está **afiliado** (como **beneficio que brinda la empresa**) y sus **beneficiarios**. Para cada seguro se captura: **nombre de seguro, cuota empleado, cuota patronal, rango, póliza, valor asegurado, activo (sí/no)**; y por cada **beneficiario**: **nombre, documento de identidad, fecha de nacimiento, parentesco**. El **nombre del seguro, los rangos y el parentesco** se eligen de **catálogos**. El empleado **puede tener varios** seguros y esto **no afecta ningún ingreso o egreso** del empleado. El objetivo declarado fue **doble**: (1) **validar** que el desarrollo **ya implementado** esté **bien alineado** y (2) **analizar y agregar** lo necesario para un HRIS robusto.

> **Hallazgo clave (verificado en código).** Esto **no es un desarrollo desde cero**. CLARIHR **ya tiene implementada** la funcionalidad como dos entidades — `PersonnelFileInsurance` y `PersonnelFileInsuranceBeneficiary` — con **CRUD completo** (Domain + Application/CQRS + API REST + JSON Patch + auditoría + concurrencia + multitenancy). **Todos los campos del requerimiento existen**, se permiten **múltiples seguros**, y las cuotas/valor asegurado son **decimales informativos** que **no** se conectan a planilla (respeta "no afecta ingreso/egreso"). La línea base es **sólida**; el trabajo de Fase 1 es de **endurecimiento y alineación** según las decisiones ratificadas: **catalogar nombre de seguro y rango** (única desalineación real con el texto del requerimiento; el parentesco **ya** está catalogado), con **alcance país** (D-01: universal, como el parentesco) y **rango jerárquico** bajo el seguro; **validar** orden de fechas y montos; **enriquecer** los beneficiarios (tipo de documento + % de asignación + principal/contingente); **catalogar la moneda** (ISO-4217); **bloquear duplicados**; aplicar un **permiso dedicado** de lectura; y exponer **historial/diff**.

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) | Decisión que aplica |
|---|---|---|---|
| 1 | **Entidad seguro** | `PersonnelFileInsurance` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:774`). Campos: `InsuranceCode`, `EmployeeContribution?`, `EmployerContribution?`, `RangeCode?`, `PolicyNumber?`, `InsuredAmount?`, `CurrencyCode?`, `IsActive`, `StartDateUtc?`, `EndDateUtc?`, `Beneficiaries`, `ConcurrencyToken`. | Base correcta y **completa**. |
| 2 | **Entidad beneficiario** | `PersonnelFileInsuranceBeneficiary` (`…PersonnelFileEmployee.cs:891`). Campos: `FullName`, `DocumentNumber?`, `BirthDate?`, `KinshipCode`, `IsActive`, `ConcurrencyToken`. | Se **enriquece** (D-09, D-10). |
| 3 | **Cobertura de campos** | **100 %** de los campos del requerimiento presentes (7 de seguro + activo; 4 de beneficiario). | **Alineado** — sin acción. |
| 4 | **Nombre de seguro** | `InsuranceCode`: solo `NotEmpty().MaximumLength(80)` (`Compensation/Insurances.cs:94`); **sin** catálogo. | Catalogar, alcance **país** (D-02, RF-002). |
| 5 | **Rango** | `RangeCode?`: **texto libre opcional**, **sin** validación (`…Handlers.cs:55`). | Catalogar **país**, **opcional**, **hijo del seguro** (D-03, RF-003). |
| 6 | **Parentesco** | `KinshipCode`: **catalogado** y validado vía `ValidateKinshipCodeAsync` en Add/Update/Patch (`InsuranceBeneficiaries.Handlers.cs:51,116,231`); categoría `Kinship` (`PersonnelReferenceCatalog.cs:115`). | **Alineado** ✅ — sin acción. |
| 7 | **Múltiples seguros** | Colección 1:N por expediente; **sin** restricción de "único activo". | **Alineado** ✅ (D-05). |
| 8 | **No afecta ingreso/egreso** | `EmployeeContribution`/`EmployerContribution`/`InsuredAmount` `numeric(18,2)` **informativos**; **no** se enlazan a nómina/`CompensationConcept`. | **Alineado** ✅ — proteger (D-01, RF-007). |
| 9 | **Vigencia** | `StartDateUtc?`/`EndDateUtc?` **opcionales**; **no** se valida `Start ≤ End`. | Validar orden; **conservar** fechas (D-06, RF-005). |
| 10 | **Montos** | `numeric(18,2)`; **sin** validación de no-negatividad. | Cuotas/valor asegurado **≥ 0** (D-07, RF-006). |
| 11 | **Moneda** | `CurrencyCode?` **texto libre** (max 40); sin catálogo. | **Catalogar ISO-4217** (D-12, RF-011). |
| 12 | **Documento beneficiario** | `DocumentNumber?` **sin tipo**; existe catálogo `IdentificationType` (DUI/NIT/Pasaporte/Carné — `PersonnelReferenceCatalog.cs:29`). | Agregar **tipo** reutilizando `IdentificationType` (D-10, RF-008). |
| 13 | **Asignación beneficiario** | **No** existe `%` ni principal/contingente. | Agregar **% + tipo**, principales suman 100 % (D-09, RF-009). |
| 14 | **Unicidad** | **Sin** control de duplicados (póliza/beneficiario). | **Bloquear** duplicados (D-13, RF-012). |
| 15 | **Capa de aplicación** | CQRS completo + JSON Patch RFC 6902 endurecido. **No existe** `Insurances.Rules.cs`. | Crear **módulo de reglas puro** (RNF). |
| 16 | **Persistencia** | Tablas `personnel_file_insurances` e `personnel_file_insurance_beneficiaries` (`…/PersonnelFileEmployeeConfiguration.cs:316`). FK+cascada; UQ `public_id`; IX por tenant. **Sin** check constraints. | Base correcta; constraints nuevos (RF-005/006/012). |
| 17 | **API** | 12 endpoints REST bajo `/api/v1/personnel-files/{publicId}/insurances[/{id}][/beneficiaries[/{id}]]` (`…CompensationController.cs:570-923`). | Se conservan. |
| 18 | **Permisos** | Controlador con `[AuthorizationPolicySet(Read, Manage)]` **genérico** (`…CompensationController.cs:27`); **no** usa `ViewCompensation`. | **Permiso dedicado de lectura** `ViewInsurance` (D-11, RF-010). |
| 19 | **Concurrencia/Auditoría** | `ConcurrencyToken` + `If-Match`/`ETag`; auditoría por operación (`PersonnelFileEmployeeAudits.LogUpdateAsync`). | Auditoría con **diff** + **historial visible** (D-15, RF-013). |
| 20 | **Estado completado** | Solo expedientes **completados** (`IsCompletedEmployee`) gestionan seguros (`Insurances.Handlers.cs:46`). | Se conserva (RN-01). |

---

## Decisiones del negocio (ratificadas — 2026-06-22)

| # | Pregunta | Decisión |
|---|---|---|
| **D-01** | Naturaleza (¿afecta nómina?) | **Beneficio documental.** El seguro **no** afecta ingresos/egresos; las cuotas son **informativas** (no se deducen en planilla). **Se protege** como invariante (P-07). |
| **D-02** | Catálogo de **nombre de seguro** — alcance | **Universal por país** (referencia, como el parentesco): nuevo catálogo país-scoped en `PersonnelReferenceCatalog` (P-01). **No** configurable por tenant. |
| **D-03** | Catálogo de **rango** | **Universal por país**, **opcional** (P-02), y **jerárquico**: el rango **cuelga del nombre de seguro** vía `ParentId` (P-03). |
| **D-04** | Parentesco | **Ya catalogado** (categoría `Kinship`). **Sin cambios.** |
| **D-05** | Múltiples seguros | **Permitidos** (sin "único activo"). **Ya es así.** |
| **D-06** | Vigencia | `StartDateUtc ≤ EndDateUtc` **validado**; fechas **opcionales** y **se conservan** (P-08). |
| **D-07** | Montos | Cuota empleado, cuota patronal y valor asegurado **≥ 0**. |
| **D-08** | Aseguradora/proveedor estructurado | **Fuera de alcance.** Con catálogo **país** el "nombre de seguro" es el **tipo/producto** universal; el detalle concreto queda en **póliza** (texto). No se modela proveedor como entidad. |
| **D-09** | Beneficiarios — asignación | **En alcance** (P-04): **% de asignación** (0–100) y **tipo** (principal/contingente). Los **principales activos suman 100 %** por seguro. |
| **D-10** | Beneficiarios — documento | **En alcance** (P-05): **tipo de documento** reutilizando el catálogo `IdentificationType` (DUI/NIT/Pasaporte/Carné). |
| **D-11** | Permisos | **Permiso dedicado de lectura** `PersonnelFiles.ViewInsurance` por sensibilidad (PII + montos); escritura por `Manage`; **sin** autoservicio (P-06). |
| **D-12** | Moneda | **Catalogada ISO-4217** (P-08). |
| **D-13** | Unicidad/duplicado | **Evitar duplicado**: misma **póliza** repetida por empleado **se bloquea**; mismo **beneficiario** (tipo+número de documento) repetido en un seguro **se bloquea**. Se siguen permitiendo **varios** seguros (P-09). |
| **D-14** | Datos existentes | **No hay datos productivos.** Se aplica **drop & recreate** / normalización directa; **sin** migración (P-10). |
| **D-15** | Auditoría | **Historial/diff visible** (antes/después), alineado con el patrón de sustituciones (P-11). |

---

## Brechas verificadas y su resolución (GAP → Resolución)

> **Leyenda:** 🔴 brecha de alineación con el requerimiento escrito · 🟡 endurecimiento HRIS · 🟢 enriquecimiento ratificado (P-04…P-11).

| # | Sev. | Brecha (as-is) | Resolución (to-be) |
|---|---|---|---|
| **G-01** | 🔴 | `InsuranceCode` texto libre; el requerimiento exige catálogo. | Catalogar **nombre de seguro**, alcance **país** (D-02, RF-002). |
| **G-02** | 🔴 | `RangeCode` texto libre; el requerimiento exige catálogo. | Catalogar **rango**, alcance **país**, opcional, **hijo del seguro** (D-03, RF-003). |
| **G-03** | 🟡 | Sin validación `StartDateUtc ≤ EndDateUtc`. | Validar orden de fechas (D-06, RF-005). |
| **G-04** | 🟡 | Montos sin validación de no-negatividad. | Cuotas/valor asegurado **≥ 0** (D-07, RF-006). |
| **G-05** | 🟡 | Sin módulo de reglas puro (`Insurances.Rules.cs`). | Crear módulo **unit-testeable** (RNF). |
| **G-06** | 🟢 | Beneficiario: `DocumentNumber` **sin tipo**. | `DocumentTypeCode` → catálogo `IdentificationType` (D-10, RF-008). |
| **G-07** | 🟢 | Sin **% de asignación** ni **principal/contingente**. | Modelar asignación; principales suman 100 % (D-09, RF-009). |
| **G-08** | 🟡 | `CurrencyCode` texto libre. | **Catalogar ISO-4217** (D-12, RF-011). |
| **G-09** | 🟡 | Seguros usa `Read/Manage` genérico (PII + montos). | **Permiso dedicado de lectura** `ViewInsurance` (D-11, RF-010). |
| **G-10** | 🟢 | Sin control de **duplicados** (póliza/beneficiario). | **Bloquear** duplicado de póliza por empleado y de beneficiario por seguro (D-13, RF-012). |
| **G-11** | 🟢 | Sin **diff/historial visible** de cambios. | Auditoría con diff + historial visible (D-15, RF-013). |

---

## 1. Resumen del producto o requerimiento

Sección del expediente para **registrar los seguros a los que el empleado está afiliado** como **beneficio de la empresa**, junto con sus **beneficiarios**. Cada seguro captura **nombre, cuota empleado, cuota patronal, rango, póliza, valor asegurado y estado activo** (más **moneda** y **vigencia**); cada beneficiario captura **nombre, documento (con tipo), fecha de nacimiento, parentesco, % de asignación y tipo principal/contingente**. **Nombre de seguro, rango y parentesco** provienen de **catálogos país**; la **moneda** del catálogo **ISO-4217**. El empleado puede tener **varios** seguros, y el registro **no afecta sus ingresos ni egresos** (cuotas **informativas**).

La funcionalidad **ya existe** (`PersonnelFileInsurance` + `PersonnelFileInsuranceBeneficiary`) con CRUD completo, auditoría y concurrencia. Esta Fase 1 la **valida y endurece** conforme a D-01…D-15: catálogos país (nombre de seguro + rango jerárquico), tipo de documento y asignación de beneficiarios, moneda ISO-4217, validación de fechas/montos, anti-duplicado, permiso dedicado de lectura, módulo de reglas y auditoría con historial visible.

---

## 2. Objetivos del negocio

- **O-1. Registro confiable de beneficios de seguro:** constancia trazable de afiliaciones y beneficiarios por empleado.
- **O-2. Integridad de datos:** estandarizar **nombre de seguro, rango, parentesco, tipo de documento y moneda** vía **catálogo** (consistencia y reportabilidad).
- **O-3. Separación clara de la nómina:** el seguro es **informativo** y **no** altera ingresos/egresos (sin acoplar a planilla).
- **O-4. Flexibilidad controlada:** **múltiples** seguros por empleado y múltiples beneficiarios por seguro, **sin duplicados** indebidos.
- **O-5. Cobertura completa del beneficiario:** distribución del beneficio (**% + principal/contingente**) e identificación tipificada.
- **O-6. Trazabilidad y control:** operaciones **auditadas con diff**, **concurrencia-seguras** y bajo **permiso dedicado** por la sensibilidad de los datos.

---

## 3. Alcance funcional (Fase 1)

- **F1.** **Validación** del registro de seguros y beneficiarios existente (CRUD vía API REST) — **ya implementado**.
- **F2.** **Catálogo país** de **nombre de seguro** y de **rango** (rango **jerárquico** bajo el seguro, opcional) (D-02, D-03).
- **F3.** **Parentesco** desde catálogo `Kinship` — **ya implementado** (se reafirma).
- **F4.** **Tipo de documento** del beneficiario (catálogo `IdentificationType`) (D-10).
- **F5.** **Asignación** del beneficiario: **% (0–100)** + **principal/contingente**; principales **suman 100 %** (D-09).
- **F6.** **Moneda** desde catálogo **ISO-4217** (D-12).
- **F7.** **Reglas de vigencia** (orden de fechas) y **montos no negativos** (D-06, D-07).
- **F8.** **Anti-duplicado**: póliza por empleado y beneficiario por seguro (D-13).
- **F9.** **Invariante de independencia de nómina** (cuotas informativas; sin enlace a `CompensationConcept`) (D-01).
- **F10.** **Permiso dedicado de lectura** `PersonnelFiles.ViewInsurance`; escritura por `Manage`; sin autoservicio (D-11).
- **F11.** **Módulo de reglas puro** + tests + paridad de localización ES/EN (RNF).
- **F12.** **Auditoría con diff** e **historial visible** (D-15).

---

## 4. Fuera de alcance

- **FA-1.** **Siniestros / reclamos médicos** (`PersonnelFileMedicalClaim`) — feature **separada existente**.
- **FA-2.** **Cálculo o deducción en planilla** de cuotas (D-01). Integración con **Ingresos/Egresos** (`CompensationConcept`) **fuera de alcance**.
- **FA-3.** **Beneficios adicionales** genéricos (`additional-benefits`) — feature **separada**.
- **FA-4.** **Aseguradora/proveedor como entidad estructurada** (D-08): con catálogo país, el "nombre de seguro" es el **tipo/producto**; el detalle concreto va en **póliza**.
- **FA-5.** **Catálogos configurables por tenant** para seguros/rangos (D-02: alcance **país**).
- **FA-6.** **Notificaciones** y **autoservicio** del empleado (D-11).
- **FA-7.** **Integración con la aseguradora** (sincronización externa de pólizas/coberturas).
- **FA-8.** **Documentos adjuntos** (póliza/carné escaneados) — se canaliza por el módulo de documentos del expediente.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Analista / Gestor de RRHH** | **Único** que crea/edita/activa/elimina seguros y beneficiarios (escritura por `Manage`) y consulta historial. |
| **Consulta / Auditor** | Lee seguros, beneficiarios e **historial/diff** (lectura por `PersonnelFiles.ViewInsurance`). |
| **Empleado titular** | Sujeto de los seguros. **Sin** autoservicio (D-11). |
| **Beneficiarios** | Personas designadas (cónyuge, hijos, etc.). **No** son usuarios; son **datos** del seguro. |
| **Administrador de Catálogos** | Mantiene los catálogos país (nombre de seguro, rango) y de identificación/moneda. |
| **Sistema — módulo de Ingresos/Egresos** | **No** consume el seguro (independencia de nómina — D-01). Límite documentado. |

---

## 6. Requerimientos funcionales

### RF-001 — Registrar la afiliación de seguro del empleado *(ya implementado — validación)*
**Descripción:** Registrar, por expediente **completado**, uno o varios seguros con nombre, cuota empleado, cuota patronal, rango, póliza, valor asegurado, estado activo, **moneda** y **fechas de vigencia**.
**Reglas de negocio:** Solo expedientes **completados** (`Insurances.Handlers.cs:46`); **múltiples** seguros permitidos (D-05); operación **auditada** y **concurrencia-segura**.
**Criterios de aceptación:**
- Dado un expediente completado, cuando registro un seguro válido, entonces se guarda y queda auditado.
- Dado un expediente **no completado**, entonces **422** `STATE_RULE_VIOLATION` (ya cubierto).
**Prioridad:** Alta · **Dependencias:** ninguna (existe).

### RF-002 — Nombre de seguro desde catálogo país *(🔴 G-01)*
**Descripción:** Sustituir `InsuranceCode` de texto libre por **referencia a catálogo país** (nueva categoría `InsuranceType` en `PersonnelReferenceCatalog`), validado como **activo**.
**Reglas de negocio:**
- `InsuranceCode` **debe** existir y estar **activo** en el catálogo país (SV).
- **Seed SV (propuesto, a confirmar por el negocio):** `SEGURO_VIDA`, `SEGURO_MEDICO_HOSPITALARIO`, `SEGURO_GASTOS_MEDICOS`, `SEGURO_DENTAL`, `SEGURO_VISION`, `SEGURO_ACCIDENTES`, `OTRO`.
**Criterios de aceptación:**
- Dado un código fuera de catálogo, entonces **422** `INSURANCE_CODE_INVALID`.
- Dado un código válido, entonces se acepta y persiste normalizado.
**Prioridad:** Alta · **Dependencias:** `PersonnelReferenceCatalog` + validador análogo a `ValidateKinshipCodeAsync` (`Catalogs/PersonnelReferenceCatalogs.cs:189`).

### RF-003 — Rango desde catálogo país, opcional y jerárquico *(🔴 G-02)*
**Descripción:** Sustituir `RangeCode` por **referencia a catálogo país** (nueva categoría `InsuranceRange`), **opcional** (D-03), donde cada rango **cuelga del nombre de seguro** mediante `ParentId` (mismo patrón Departamento→Municipio).
**Reglas de negocio:**
- Si se informa, `RangeCode` **debe** pertenecer al catálogo de rangos **activo** y ser **hijo del `InsuranceCode` seleccionado**.
- Si el seguro no tiene rangos definidos, el campo queda **vacío** (válido).
**Criterios de aceptación:**
- Dado un rango fuera de catálogo, entonces **422** `INSURANCE_RANGE_INVALID`.
- Dado un rango que **no** pertenece al seguro elegido, entonces **422** `INSURANCE_RANGE_NOT_OWNED`.
- Dado rango vacío, entonces se acepta (opcional).
**Prioridad:** Alta · **Dependencias:** RF-002.

### RF-004 — Registrar beneficiarios con parentesco catalogado *(ya implementado — validación)*
**Descripción:** Por seguro, registrar 0..N beneficiarios; el **parentesco** proviene del catálogo `Kinship` (ya validado en Add/Update/Patch).
**Reglas de negocio:** `KinshipCode` **debe** existir en `Kinship` (ya implementado); borrar un seguro **elimina en cascada** sus beneficiarios.
**Criterios de aceptación:**
- Dado un `kinshipCode` fuera de catálogo, entonces **422** `KINSHIP_CODE_INVALID` (ya cubierto).
**Prioridad:** Alta · **Dependencias:** ninguna (existe).

### RF-005 — Reglas de vigencia y coherencia *(🟡 G-03)*
**Descripción:** Validar el **orden de fechas**; las fechas **se conservan** y siguen **opcionales** (D-06).
**Reglas de negocio:** Si ambas se informan, `StartDateUtc ≤ EndDateUtc`. *(Opcional)* señalar inconsistencia si `IsActive = true` con `EndDateUtc` pasada.
**Criterios de aceptación:** Dado `EndDateUtc < StartDateUtc`, entonces **422** `INSURANCE_DATE_RANGE_INVALID`.
**Prioridad:** Media · **Dependencias:** módulo de reglas (RNF).

### RF-006 — Montos no negativos *(🟡 G-04)*
**Descripción:** Validar que **cuota empleado, cuota patronal y valor asegurado** sean **≥ 0**.
**Criterios de aceptación:** Dado un monto negativo, entonces **422** `INSURANCE_AMOUNT_INVALID`.
**Prioridad:** Media · **Dependencias:** módulo de reglas (RNF).

### RF-007 — Independencia de la nómina (invariante protegida) *(D-01)*
**Descripción:** Garantizar que el seguro **no** afecta ingresos/egresos: las cuotas son **informativas** y **no** generan transacciones de planilla ni se enlazan a `CompensationConcept`.
**Reglas de negocio:** El módulo **no** publica eventos/registros hacia Ingresos/Egresos/planilla. Un descuento real futuro se modelaría en **Ingresos/Egresos**, no aquí.
**Criterios de aceptación:** Dado el alta/edición de un seguro, cuando se consultan ingresos/egresos del empleado, entonces **no** aparece ninguna línea derivada del seguro.
**Prioridad:** Alta (gobernanza) · **Dependencias:** módulo de Ingresos/Egresos (solo para fijar el límite).

### RF-008 — Tipo de documento del beneficiario *(🟢 G-06)*
**Descripción:** Agregar `DocumentTypeCode` al beneficiario, reutilizando el catálogo `IdentificationType` (DUI, NIT, Pasaporte, Carné de residente — `PersonnelReferenceCatalog.cs:29`).
**Reglas de negocio:** Si se informa documento, **debe** indicarse su **tipo** del catálogo; si no hay documento, el tipo es opcional.
**Criterios de aceptación:** Dado un tipo fuera de catálogo, entonces **422** `DOCUMENT_TYPE_INVALID`.
**Prioridad:** Media · **Dependencias:** catálogo `IdentificationType` (existe).

### RF-009 — Asignación de beneficiarios (% y principal/contingente) *(🟢 G-07)*
**Descripción:** Modelar `AllocationPercentage` (0–100) y `BeneficiaryType` (**principal**/**contingente**) por beneficiario.
**Reglas de negocio:**
- `AllocationPercentage` ∈ [0, 100].
- La **suma** de `AllocationPercentage` de los beneficiarios **principales activos** = **100 %** por seguro.
- Los **contingentes** no computan en el 100 % de principales (regla separada o sin tope, a aplicar igual lógica si el negocio lo pide).
**Criterios de aceptación:**
- Dado un total de principales ≠ 100 %, entonces **422** `BENEFICIARY_ALLOCATION_INVALID`.
- Dado `%` fuera de [0,100], entonces **422** `BENEFICIARY_ALLOCATION_RANGE`.
**Prioridad:** Media · **Dependencias:** módulo de reglas (RNF).

### RF-010 — Permiso dedicado de lectura *(🟡 G-09)*
**Descripción:** Proteger la **lectura** de seguros/beneficiarios con un **permiso dedicado** `PersonnelFiles.ViewInsurance` (siguiendo el patrón de `ViewCompensation`: superset Admin); la **escritura** sigue con `PersonnelFiles.Manage`. **Sin** autoservicio.
**Reglas de negocio:** Lectura requiere `ViewInsurance` (o Admin/superadmin); escritura requiere `Manage`.
**Criterios de aceptación:**
- Dado un usuario sin `ViewInsurance`, cuando consulta seguros, entonces **403 FORBIDDEN**.
- Dado un usuario sin `Manage`, cuando intenta crear/editar, entonces **403 FORBIDDEN**.
**Prioridad:** Alta · **Dependencias:** Provisioning/`IdentityAccess` (alta + seed del permiso; política en `Program.cs`).

### RF-011 — Moneda desde catálogo ISO-4217 *(🟡 G-08)*
**Descripción:** Validar `CurrencyCode` contra un catálogo **ISO-4217** (p. ej. `USD`); normalizar a mayúsculas.
**Reglas de negocio:** Si se informa, `CurrencyCode` **debe** ser un código ISO-4217 válido/activo.
**Criterios de aceptación:** Dado un código no ISO-4217, entonces **422** `CURRENCY_CODE_INVALID`.
**Prioridad:** Media · **Dependencias:** catálogo de monedas (reutilizar existente o crear system-scoped).

### RF-012 — Anti-duplicado *(🟢 G-10)*
**Descripción:** **Bloquear** duplicados: (a) misma **póliza** (`PolicyNumber`) repetida en el mismo empleado; (b) mismo **beneficiario** (tipo + número de documento) repetido en el mismo seguro. Se siguen permitiendo **varios** seguros.
**Reglas de negocio:**
- No dos seguros del mismo empleado con idéntico `PolicyNumber` (cuando se informa).
- No dos beneficiarios **activos** con idéntico (`DocumentTypeCode`, `DocumentNumber`) en un mismo seguro.
**Criterios de aceptación:**
- Dada una póliza ya registrada para el empleado, entonces **422** `INSURANCE_POLICY_DUPLICATE`.
- Dado un beneficiario con documento ya presente en el seguro, entonces **422** `BENEFICIARY_DUPLICATE`.
**Prioridad:** Media · **Dependencias:** módulo de reglas; índices de apoyo.

### RF-013 — Auditoría con diff e historial visible *(🟢 G-11)*
**Descripción:** Registrar cada alta/cambio/baja con **valores antes/después (diff)** y **exponer un historial visible** (RRHH/auditor). La auditoría base ya existe; se completa el *diff* y la **vista** de historial.
**Reglas de negocio:** Cada operación de escritura genera una entrada con **estado anterior y nuevo**; el historial es **consultable** desde la UI por usuarios autorizados (`ViewInsurance`).
**Criterios de aceptación:** Dada una edición, cuando se consulta el historial, entonces se ve **qué cambió** (campo, valor anterior, valor nuevo, quién y cuándo).
**Prioridad:** Media · **Dependencias:** `IAuditService`; endpoint/vista de historial.

---

## 7. Requerimientos no funcionales

- **Seguridad / Multi-tenant.** Operaciones filtradas por **tenant** (ya implementado). **Permiso dedicado de lectura** por PII de beneficiarios + montos (RF-010).
- **Integridad / Consistencia.** Catálogos (RF-002/003/008/011), fechas (RF-005), montos (RF-006), asignación (RF-009) y unicidad (RF-012) en un **módulo de reglas puro** `Insurances.Rules.cs`, **unit-testeable** (patrón `EmploymentAssignments.Rules.cs`).
- **Concurrencia.** `ConcurrencyToken` + `If-Match`/`ETag` (ya implementado).
- **Auditoría.** Diff antes/después + historial visible (D-15, RF-013).
- **Rendimiento.** Índices existentes por tenant cubren listados; agregar apoyo para anti-duplicado (póliza por empleado; documento por seguro).
- **Usabilidad.** Selección por **catálogo** (combos dependientes seguro→rango); validación de `%` con suma 100 % en cliente; montos validados.
- **Compatibilidad / API.** Mantener los 12 endpoints; ampliar contratos (tipo de documento, %/tipo de beneficiario). Cambio a catálogo es *breaking* solo para datos inválidos.
- **Localización.** Mensajes/errores **bilingües (ES/EN)** con **código estable**; test de **paridad de localización**.
- **Mantenibilidad.** Extender los tests de patch existentes (`PersonnelFileInsurancePatchTests`, `PersonnelFileInsuranceBeneficiaryPatchTests`) a las nuevas reglas.

---

## 8. Historias de usuario

### HU-001 — Afiliar a un empleado a un seguro
Como **Analista de RRHH**, quiero **registrar los seguros del empleado con sus datos**, para **dejar constancia de los beneficios que la empresa le brinda**.
- Dado un expediente completado, cuando registro un seguro con **nombre de catálogo**, póliza y valor asegurado, entonces se guarda y se audita.
- Dado un expediente no completado, entonces **bloquea** (`STATE_RULE_VIOLATION`).

### HU-002 — Estandarizar nombre de seguro y rango (dependiente)
Como **Analista de RRHH**, quiero **elegir el nombre del seguro y, si aplica, su rango asociado**, para **mantener datos consistentes y coherentes**.
- Dado un nombre de seguro, cuando abro rango, entonces solo veo **rangos de ese seguro** (RF-003).
- Dado un nombre o rango fuera de catálogo, entonces **bloquea** (RF-002/003).

### HU-003 — Registrar beneficiarios con distribución
Como **Analista de RRHH**, quiero **agregar beneficiarios con parentesco, tipo de documento, % y principal/contingente**, para **reflejar quién recibe el beneficio y en qué proporción**.
- Dado un seguro, cuando agrego beneficiarios principales, entonces sus **%** deben **sumar 100 %** (RF-009).
- Dado un documento, cuando no indico **tipo**, entonces **bloquea** (RF-008).

### HU-004 — Varios seguros sin afectar la nómina y sin duplicar
Como **Analista de RRHH**, quiero **registrar varios seguros como beneficio, sin duplicar pólizas ni impactar la planilla**, para **separar beneficios de la compensación**.
- Dado un empleado con varios seguros, cuando consulto sus ingresos/egresos, entonces **no** aparece ninguna línea de seguros (RF-007).
- Dada una póliza ya registrada, cuando la repito, entonces **bloquea** (RF-012).

### HU-005 — Datos válidos de vigencia, montos y moneda
Como **Analista de RRHH**, quiero que **el sistema valide fechas, montos y moneda**, para **evitar registros incoherentes**.
- Dado `EndDate < StartDate`, un monto negativo o una moneda no ISO, entonces **bloquea** (RF-005/006/011).

### HU-006 — Auditar y controlar acceso
Como **Auditor / RRHH**, quiero **ver el historial de cambios y que el acceso esté restringido**, para **cumplimiento y protección de PII**.
- Dada una edición, cuando abro el historial, entonces veo **valor anterior y nuevo**, autor y fecha (RF-013).
- Dado un usuario sin `ViewInsurance`, cuando consulta, entonces **403** (RF-010).

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** gestionan seguros (ya implementado).
- **RN-02.** El empleado puede tener **varios** seguros; **sin** "único activo" (D-05).
- **RN-03.** **Nombre de seguro** y **rango** **deben** provenir de **catálogo país** activo; el **rango** es **hijo** del seguro (D-02/D-03, RF-002/003).
- **RN-04.** **Parentesco** **debe** pertenecer al catálogo `Kinship` activo (ya implementado).
- **RN-05.** **Tipo de documento** del beneficiario **debe** pertenecer a `IdentificationType` cuando hay documento (D-10, RF-008).
- **RN-06.** Beneficiarios **principales activos** **suman 100 %**; `%` ∈ [0,100] (D-09, RF-009).
- **RN-07.** `StartDateUtc ≤ EndDateUtc`; fechas **opcionales** (D-06, RF-005).
- **RN-08.** Cuotas y valor asegurado **≥ 0** (D-07, RF-006).
- **RN-09.** `CurrencyCode` **debe** ser **ISO-4217** (D-12, RF-011).
- **RN-10.** El seguro **no** afecta ingresos/egresos: cuotas **informativas**, sin enlace a planilla/`CompensationConcept` (D-01, RF-007).
- **RN-11.** **Anti-duplicado**: póliza por empleado y beneficiario (tipo+documento) por seguro (D-13, RF-012).
- **RN-12.** Borrar un seguro **elimina en cascada** sus beneficiarios (ya implementado).
- **RN-13.** **Lectura** con `PersonnelFiles.ViewInsurance`; **escritura** con `Manage`; **sin** autoservicio (D-11).
- **RN-14.** Toda escritura exige **If-Match** y queda **auditada con diff**; historial **visible** (D-15, RF-013).

---

## 10. Flujos principales

**Flujo: Registrar un seguro (RRHH)**
1. RRHH (con `Manage`) abre el expediente del empleado (**completado**).
2. Entra a **"Seguros" → Agregar**.
3. Selecciona el **nombre del seguro** del **catálogo país** (RF-002); el sistema carga los **rangos hijos** y permite elegir uno (opcional) (RF-003).
4. Ingresa **cuota empleado/patronal, póliza, valor asegurado** (montos ≥ 0 — RF-006), **moneda** (ISO-4217 — RF-011) y **estado activo**; opcionalmente **fechas** (orden — RF-005).
5. El sistema valida **anti-duplicado** de póliza (RF-012). Guarda → persiste, **audita con diff**, devuelve `ETag`.

**Flujo: Agregar beneficiarios**
1. Sobre un seguro, **Agregar beneficiario**.
2. Ingresa **nombre, documento + tipo** (RF-008), **fecha de nacimiento**, **parentesco** (`Kinship`), **% de asignación** y **principal/contingente** (RF-009).
3. El sistema valida **anti-duplicado** (RF-012) y, al guardar el conjunto, que los **principales sumen 100 %** (RF-009). Persiste y **audita**.

**Flujo: Consultar e historial**
1. Usuario con `ViewInsurance` consulta seguros/beneficiarios y abre el **historial/diff** (RF-013).

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Expediente **no completado**. | **Bloqueo** `422 STATE_RULE_VIOLATION` (ya implementado). |
| **E2** | **Nombre de seguro** fuera de catálogo. | **Bloqueo** `422 INSURANCE_CODE_INVALID` (RF-002). |
| **E3** | **Rango** fuera de catálogo o no perteneciente al seguro. | **Bloqueo** `422 INSURANCE_RANGE_INVALID` / `INSURANCE_RANGE_NOT_OWNED` (RF-003). |
| **E4** | **Parentesco** fuera de catálogo. | **Bloqueo** `422 KINSHIP_CODE_INVALID` (ya implementado). |
| **E5** | **Tipo de documento** fuera de catálogo. | **Bloqueo** `422 DOCUMENT_TYPE_INVALID` (RF-008). |
| **E6** | Principales **no suman 100 %** / `%` fuera de rango. | **Bloqueo** `422 BENEFICIARY_ALLOCATION_INVALID` / `…_RANGE` (RF-009). |
| **E7** | `EndDate < StartDate`. | **Bloqueo** `422 INSURANCE_DATE_RANGE_INVALID` (RF-005). |
| **E8** | Monto **negativo**. | **Bloqueo** `422 INSURANCE_AMOUNT_INVALID` (RF-006). |
| **E9** | **Moneda** no ISO-4217. | **Bloqueo** `422 CURRENCY_CODE_INVALID` (RF-011). |
| **E10** | **Póliza** duplicada por empleado / **beneficiario** duplicado por seguro. | **Bloqueo** `422 INSURANCE_POLICY_DUPLICATE` / `BENEFICIARY_DUPLICATE` (RF-012). |
| **E11** | `If-Match`/`ConcurrencyToken` no coincide. | **409 CONFLICT** (ya implementado). |
| **E12** | Borrar un seguro con beneficiarios. | Borra **en cascada** (ya implementado). |
| **E13** | Usuario sin `ViewInsurance` (lectura) o sin `Manage` (escritura). | **403 FORBIDDEN** (RF-010). |

---

## 12. Datos requeridos

### Entidad: `PersonnelFileInsurance` *(ya existe — `Domain/PersonnelFiles/PersonnelFileEmployee.cs:774`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `personnelFileId` | long (FK) | Sí | del tenant; cascada | ✅ existe | Empleado dueño |
| `insuranceCode` | Texto → **catálogo país** | Sí | catálogo `InsuranceType` activo | 🔧 catalogar (RF-002) | **Nombre de seguro** |
| `rangeCode` | Texto → **catálogo país** | No | catálogo `InsuranceRange`, **hijo del seguro** | 🔧 catalogar (RF-003) | **Rango** (jerárquico) |
| `employeeContribution` | Decimal(18,2) | No | **≥ 0** | 🔧 validar (RF-006) | Cuota empleado (informativa) |
| `employerContribution` | Decimal(18,2) | No | **≥ 0** | 🔧 validar (RF-006) | Cuota patronal (informativa) |
| `policyNumber` | Texto (120) | No | anti-duplicado por empleado | 🔧 unicidad (RF-012) | Póliza |
| `insuredAmount` | Decimal(18,2) | No | **≥ 0** | 🔧 validar (RF-006) | Valor asegurado |
| `currencyCode` | Texto (40) → **catálogo** | No | **ISO-4217** | 🔧 catalogar (RF-011) | Moneda |
| `isActive` | Booleano | Sí | — | ✅ existe | Activo (sí/no) |
| `startDateUtc` | Fecha | No | `≤ endDate` | ✅ existe | Inicio de vigencia |
| `endDateUtc` | Fecha | No | `≥ startDate` | ✅ existe | Fin de vigencia |
| `beneficiaries` | Colección | No | 0..N | ✅ existe | Beneficiarios |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

### Entidad: `PersonnelFileInsuranceBeneficiary` *(ya existe — `…PersonnelFileEmployee.cs:891`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `insuranceId` | long (FK) | Sí | del seguro; cascada | ✅ existe | Seguro dueño |
| `fullName` | Texto (200) | Sí | no vacío | ✅ existe | Nombre |
| `documentTypeCode` | Texto → **catálogo** | No* | catálogo `IdentificationType` | 🆕 nuevo (RF-008) | Tipo de documento (*req. si hay documento) |
| `documentNumber` | Texto (80) | No | anti-duplicado por seguro | ✅ existe + unicidad (RF-012) | Número de documento |
| `birthDate` | Fecha | No | — | ✅ existe | Fecha de nacimiento |
| `kinshipCode` | Texto → **catálogo** | Sí | catálogo `Kinship` | ✅ existe | **Parentesco** |
| `allocationPercentage` | Decimal | No | [0,100]; principales=100% | 🆕 nuevo (RF-009) | % de asignación |
| `beneficiaryType` | Enum | No | principal/contingente | 🆕 nuevo (RF-009) | Tipo de beneficiario |
| `isActive` | Booleano | Sí | — | ✅ existe | Activo |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

### Catálogos país *(nuevos — `PersonnelReferenceCatalog`, patrón `Kinship`)*

| Categoría | Alcance | Jerarquía | Seed SV (propuesto, a confirmar) |
|---|---|---|---|
| `InsuranceType` | País (SV) | raíz | `SEGURO_VIDA`, `SEGURO_MEDICO_HOSPITALARIO`, `SEGURO_GASTOS_MEDICOS`, `SEGURO_DENTAL`, `SEGURO_VISION`, `SEGURO_ACCIDENTES`, `OTRO` |
| `InsuranceRange` | País (SV) | **hijo de `InsuranceType`** (`ParentId`) | p. ej. para vida: `BASICO`, `INTERMEDIO`, `PREMIUM` (definir por seguro) |
| `IdentificationType` | País (SV) | raíz | **ya existe** (`DUI`, `NIT`, `PASSPORT`, `RESIDENT_CARD`) |

> **Moneda (RF-011):** catálogo **ISO-4217** (reutilizar existente o crear *system-scoped*); SV opera típicamente en `USD`.

---

## 13. Integraciones necesarias

- **Catálogos país (`PersonnelReferenceCatalog`).** Nuevas categorías `InsuranceType` y `InsuranceRange` (jerárquica); `Kinship` e `IdentificationType` **ya existen**.
- **Catálogo de monedas (ISO-4217).** Para `CurrencyCode` (RF-011).
- **Repositorio de expedientes (`IPersonnelFileEmployeeRepository`).** Persistencia de seguros/beneficiarios (ya implementado); apoyo anti-duplicado (RF-012).
- **Identidad/Provisioning (`IdentityAccess`).** Alta + seed del permiso `PersonnelFiles.ViewInsurance` y política en `Program.cs` (RF-010).
- **Auditoría (`IAuditService`).** Diff + historial visible (RF-013).
- **Módulo de Ingresos/Egresos (`CompensationConcept`).** **Solo** para fijar el **límite**: el seguro **no** se integra a planilla (RF-007).

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **RRHH / Administrador de Expedientes** | Crear/editar/activar/eliminar (**`PersonnelFiles.Manage`**) y leer (**`PersonnelFiles.ViewInsurance`**). | Solo expedientes **completados**; solo su **tenant**. |
| **Consulta / Auditor** | Leer seguros, beneficiarios e **historial/diff** (**`PersonnelFiles.ViewInsurance`**). | Sin escritura. |
| **Administrador de Catálogos** | Mantener catálogos país (nombre de seguro, rango) e identificación/moneda. | Según gobierno de catálogos. |
| **Empleado titular** | — | **Sin** autoservicio (D-11). |

> **Permiso nuevo (D-11):** `PersonnelFiles.ViewInsurance`, siguiendo el patrón de `PersonnelFiles.ViewCompensation` (Admin/superadmin como **superset**). La **escritura** se mantiene en `PersonnelFiles.Manage`. Hoy el controlador usa `Read/Manage` genérico (`…CompensationController.cs:27`) → se separa la lectura de seguros a su permiso dedicado.

---

## 15. Criterios de aceptación generales

- ✅ Todos los campos del requerimiento presentes (seguro y beneficiario) (**ya cumplido**).
- ✅ **Nombre de seguro** y **rango** se eligen de **catálogo país**; el rango es **hijo** del seguro (RF-002/003).
- ✅ **Parentesco** y **tipo de documento** del beneficiario desde catálogo (RF-004/008).
- ✅ Beneficiarios con **% + principal/contingente**; principales **suman 100 %** (RF-009).
- ✅ **Moneda** validada **ISO-4217** (RF-011).
- ✅ Fechas coherentes (`Start ≤ End`) y montos **≥ 0** (RF-005/006).
- ✅ **Anti-duplicado** de póliza por empleado y beneficiario por seguro (RF-012).
- ✅ El seguro **no** genera ingresos/egresos ni líneas de planilla (RF-007 — protegido).
- ✅ **Lectura** con `ViewInsurance`; **escritura** con `Manage` (RF-010).
- ✅ Operaciones **concurrencia-seguras (If-Match)** y **auditadas con diff/historial visible** (RF-013).
- ✅ Reglas en **módulo puro** con **tests** y **paridad de localización ES/EN**.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1. Completitud del catálogo país.** Al ser **universal por país** (D-02), el seed de `InsuranceType`/`InsuranceRange` debe cubrir los productos comunes; mitigación: incluir `OTRO` y mantener el catálogo **extensible**. *(Trade-off aceptado: el detalle por aseguradora no se estructura — D-08; queda en `policyNumber`.)*
- **R2. Jerarquía seguro→rango.** Requiere cargar rangos **dependientes** del seguro en UI y validar pertenencia (`ParentId`); definir bien el seed por seguro.
- **R3. Regla del 100 %.** La validación de suma de principales debe contemplar altas/bajas/edición y beneficiarios **inactivos**; ubicarla en el **módulo de reglas** con tests.
- **R4. Ampliación de contratos API.** Nuevos campos (tipo de documento, %, tipo de beneficiario) cambian DTOs/JSON Patch; extender validadores y tests de patch existentes.

### Supuestos
- **S1.** **No hay datos productivos** (D-14): se aplica **drop & recreate**/normalización directa; **sin** migración. Se eliminará/normalizará lo necesario de seguros/beneficiarios existentes en entornos no productivos.
- **S2.** Infraestructura de catálogos país (`PersonnelReferenceCatalog`) reutilizable; soporta jerarquía (`ParentId`, como Departamento→Municipio).
- **S3.** País de referencia **SV**; seeds en ES (con `name` ES/EN). SV opera en `USD`.
- **S4.** Las cuotas **no** se descuentan en planilla en esta fase (D-01).

### Dependencias
- **D1.** `PersonnelReferenceCatalog` (nuevas categorías `InsuranceType`, `InsuranceRange`).
- **D2.** Catálogo **ISO-4217** (RF-011).
- **D3.** `IdentityAccess`/Provisioning (permiso `ViewInsurance`).
- **D4.** `IAuditService` + vista de historial.
- **D5.** `IPersonnelFileEmployeeRepository` (existe).

---

## 17. Decisiones resueltas (cierre de preguntas abiertas)

| Pregunta | Decisión | Ref. |
|---|---|---|
| P-01 ¿Alcance del catálogo de seguro/rango? | **Universal por país** (no por tenant). | D-02/D-03, RF-002/003 |
| P-02 ¿Rango obligatorio? | **Opcional.** | D-03, RF-003 |
| P-03 ¿Rango depende del seguro (jerarquía)? | **Sí** (`ParentId`, rango hijo del seguro). | D-03, RF-003 |
| P-04 ¿% asignación + principal/contingente? | **Sí, en alcance.** Principales suman 100 %. | D-09, RF-009 |
| P-05 ¿Tipo de documento del beneficiario? | **Sí**, reutilizar `IdentificationType`. | D-10, RF-008 |
| P-06 ¿Permisos? ¿Autoservicio? | **Permiso dedicado de lectura** `ViewInsurance`; escritura `Manage`; **sin** autoservicio. | D-11, RF-010 |
| P-07 ¿Cuotas informativas (sin planilla)? | **Sí**; límite con Ingresos/Egresos documentado. | D-01, RF-007 |
| P-08 ¿Moneda ISO? ¿Fechas? | **Catalogar ISO-4217**; **conservar** fechas. | D-12, RF-011 |
| P-09 ¿Unicidad/duplicado? | **Evitar duplicado** (póliza por empleado; beneficiario por seguro). | D-13, RF-012 |
| P-10 ¿Datos productivos/migración? | **No hay datos** → **drop & recreate**, sin migración. | D-14 |
| P-11 ¿Historial/diff visible? | **Sí.** | D-15, RF-013 |

> **Pendiente menor de diseño (no bloqueante):** seed final de `InsuranceType`/`InsuranceRange` (lista por el negocio); nombre exacto del permiso (`PersonnelFiles.ViewInsurance`, sujeto a convención de Provisioning); tratamiento de la suma 100 % para **contingentes** (si aplican su propio 100 % o no computan).

---

## 18. Recomendaciones del Analista de Negocio

1. **Reposicionar como "validación + endurecimiento", no construcción nueva.** Entidades, CRUD, beneficiarios, parentesco catalogado, auditoría, concurrencia y multitenancy **ya existen**. El esfuerzo es **catálogos + reglas + permiso + historial**.

2. **Fase 1 (todo lo ratificado D-01…D-15):** RF-002 (nombre de seguro, catálogo país), RF-003 (rango país, opcional, jerárquico), RF-005 (fechas), RF-006 (montos), RF-007 (independencia de nómina), RF-008 (tipo de documento), RF-009 (asignación %/tipo, suma 100 %), RF-010 (permiso `ViewInsurance`), RF-011 (moneda ISO-4217), RF-012 (anti-duplicado), RF-013 (auditoría con diff/historial). Extraer un **módulo de reglas puro** `Insurances.Rules.cs` y **extender** los tests de patch existentes.

3. **Catálogos primero.** Sembrar `InsuranceType` y `InsuranceRange` (jerárquico) en `PersonnelReferenceCatalog` y el catálogo **ISO-4217**; reutilizar `ValidateKinshipCodeAsync` como plantilla para los nuevos validadores (nombre de seguro, rango con verificación de **pertenencia al padre**, tipo de documento, moneda).

4. **Datos: drop & recreate (D-14).** Al **no** haber datos productivos, normalizar directamente las tablas `personnel_file_insurances` / `personnel_file_insurance_beneficiaries` (catálogos + nuevos campos `documentTypeCode`, `allocationPercentage`, `beneficiaryType`) sin migración de datos; sí se requiere **migración de esquema** EF Core.

5. **Permiso e historial.** Dar de alta `PersonnelFiles.ViewInsurance` en Provisioning + política en `Program.cs`, y separar la **lectura** de seguros del `Read` genérico. Completar el **diff** en auditoría y exponer la **vista de historial** (alinear con el patrón de sustituciones).

6. **Proteger la independencia de nómina (RF-007).** Dejar explícito (código + doc) que las cuotas son informativas y que el módulo **no** se acopla a Ingresos/Egresos; un descuento real futuro se modela en ese otro módulo.

7. **Confirmar con el negocio el seed** de `InsuranceType`/`InsuranceRange` y la regla de **contingentes** (no bloqueante) antes de cerrar el diseño técnico.

---

> **Naturaleza del documento.** Análisis de **validación + brechas (GAP)** sobre funcionalidad **ya implementada** (`PersonnelFileInsurance` + `PersonnelFileInsuranceBeneficiary`). El **estado as-is está verificado contra el código** (referencias `archivo:línea`). Las **decisiones del negocio están ratificadas** (D-01…D-15, 2026-06-22); el documento queda **cerrado para Fase 1** y listo para diseño técnico. La única desalineación con el requerimiento escrito (nombre de seguro y rango como texto libre) se resuelve catalogándolos con **alcance país**; el resto es endurecimiento e enriquecimiento ya confirmados.
