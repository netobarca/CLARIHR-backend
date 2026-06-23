# Análisis de Negocio — Seguros del Empleado y Beneficiarios

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Compensación/Beneficios (`PersonnelFileInsurance`, `PersonnelFileInsuranceBeneficiary`) · Catálogos de referencia (`PersonnelReferenceCatalog`) · Catálogos configurables (`CountryScopedCatalogItem` / por tenant) · Identidad/Permisos (`IdentityAccess`/Provisioning) |
| **Estado** | **Validación / Análisis inicial — MAYORMENTE ALINEADO.** La implementación cubre el **100 % de los campos** del requerimiento y respeta la regla de **no afectar ingresos/egresos**. **Única brecha de alineación con el requerimiento escrito:** *nombre de seguro* y *rango* son **texto libre**, pero el requerimiento exige **catálogo** (parentesco **sí** cumple). Resto = **endurecimiento menor** + **enriquecimientos HRIS opcionales** a confirmar. **Pendiente de ratificación del negocio** (P-01…P-10). **No es una reconstrucción.** |
| **Versión** | v1 |
| **Fecha** | 2026-06-22 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |

---

## Contexto del cambio

En el **expediente del empleado** existe una sección de **Seguros** para registrar los seguros a los que el empleado está **afiliado** (como **beneficio que brinda la empresa**) y sus **beneficiarios**. Para cada seguro se captura: **nombre de seguro, cuota empleado, cuota patronal, rango, póliza, valor asegurado, activo (sí/no)**; y por cada **beneficiario**: **nombre, documento de identidad, fecha de nacimiento, parentesco**. El **nombre del seguro, los rangos y el parentesco** se eligen de **catálogos**. El empleado **puede tener varios** seguros y esto **no afecta ningún ingreso o egreso** del empleado. El objetivo declarado es **doble**: (1) **validar** que el desarrollo **ya implementado** esté **bien alineado** y (2) **analizar y agregar** lo necesario para un HRIS robusto — y, si **no hace falta nada más**, **no agregar** y **cerrar**.

> **Hallazgo clave (verificado en código).** Esto **no es un desarrollo desde cero**. CLARIHR **ya tiene implementada** la funcionalidad como dos entidades — `PersonnelFileInsurance` y `PersonnelFileInsuranceBeneficiary` — con **CRUD completo** (Domain + Application/CQRS + API REST + JSON Patch + auditoría + concurrencia + multitenancy). **Todos los campos del requerimiento existen**, se permiten **múltiples seguros**, y las cuotas/valor asegurado son **decimales informativos** que **no** se conectan a ningún cálculo de planilla (respeta "no afecta ingreso/egreso"). La implementación incluso **supera** el requerimiento con campos útiles para HRIS (**moneda**, **fechas de vigencia**). **La única desalineación real con el texto del requerimiento** es que **el nombre de seguro y el rango son texto libre**, mientras que el requerimiento exige que se **elijan de catálogo** — **el parentesco sí está catalogado**. El resto del trabajo posible es **endurecimiento menor** (orden de fechas, montos no negativos, módulo de reglas) y **enriquecimientos HRIS opcionales** que conviene **confirmar** antes de incluir, respetando la instrucción de **no sobre-construir**.

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) | Decisión / acción |
|---|---|---|---|
| 1 | **Entidad seguro** | `PersonnelFileInsurance` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:774`). Campos: `InsuranceCode`, `EmployeeContribution?`, `EmployerContribution?`, `RangeCode?`, `PolicyNumber?`, `InsuredAmount?`, `CurrencyCode?`, `IsActive`, `StartDateUtc?`, `EndDateUtc?`, `Beneficiaries`, `ConcurrencyToken`. | Base correcta y **completa**. |
| 2 | **Entidad beneficiario** | `PersonnelFileInsuranceBeneficiary` (`…PersonnelFileEmployee.cs:891`). Campos: `FullName`, `DocumentNumber?`, `BirthDate?`, `KinshipCode`, `IsActive`, `ConcurrencyToken`. | Base correcta y **completa**. |
| 3 | **Cobertura de campos** | **100 %** de los campos del requerimiento están presentes (7 de seguro + activo; 4 de beneficiario). | **Alineado** — sin acción. |
| 4 | **Nombre de seguro** | `InsuranceCode`: solo `NotEmpty().MaximumLength(80)` (`Compensation/Insurances.cs:94`); **sin** catálogo. | **Brecha:** catalogar (D-02, RF-002). |
| 5 | **Rango** | `RangeCode?`: **texto libre opcional**, **sin** validación (`Insurances.cs` / `…Handlers.cs:55`). | **Brecha:** catalogar (D-03, RF-003). |
| 6 | **Parentesco** | `KinshipCode`: **catalogado** y validado vía `PersonnelReferenceCatalogValidation.ValidateKinshipCodeAsync` en Add/Update/Patch (`InsuranceBeneficiaries.Handlers.cs:51,116,231`); categoría `Kinship` (`PersonnelReferenceCatalog.cs:115`). | **Alineado** ✅ — sin acción. |
| 7 | **Múltiples seguros** | Colección 1:N por expediente; **sin** restricción de "único activo". | **Alineado** ✅ (el requerimiento pide varios). |
| 8 | **No afecta ingreso/egreso** | `EmployeeContribution`/`EmployerContribution`/`InsuredAmount` son `numeric(18,2)` **informativos**; **no** se enlazan a nómina/`CompensationConcept`. | **Alineado** ✅ — proteger como invariante (RF-007). |
| 9 | **Vigencia** | `StartDateUtc?`/`EndDateUtc?` **opcionales**; **no** se valida `Start ≤ End` (ni dominio ni BD). | **Endurecer** (RF-005). |
| 10 | **Montos** | `numeric(18,2)`; **sin** validación de no-negatividad. | **Endurecer** (RF-006). |
| 11 | **Capa de aplicación** | CQRS completo Add/Update/Patch/Delete/Get/GetList para seguro **y** beneficiario; JSON Patch RFC 6902 endurecido. **No existe** `Insurances.Rules.cs`. | Crear **módulo de reglas puro** (RNF). |
| 12 | **Persistencia** | Tablas `personnel_file_insurances` e `personnel_file_insurance_beneficiaries` (`…/PersonnelFileEmployeeConfiguration.cs:316`). FK + cascada; UQ `public_id`; IX `(tenant_id, personnel_file_id, is_active, insurance_code)` y `(tenant_id, insurance_id, is_active)`. **Sin** check constraints. | Base correcta; constraints opcionales (RF-005/006). |
| 13 | **API** | 12 endpoints REST bajo `/api/v1/personnel-files/{publicId}/insurances[/{id}][/beneficiaries[/{id}]]` (`Api/Controllers/PersonnelFileCompensationController.cs:570-923`). | Se conservan. |
| 14 | **Permisos** | Controlador con `[AuthorizationPolicySet(Read, Manage)]` **genérico** (`…CompensationController.cs:27`). **No** usa `ViewCompensation` (que **sí** protege `CompensationConcepts` — `…CompensationConceptsController.cs:20`). | **Decisión** de gobierno/sensibilidad (RF-009, P-06). |
| 15 | **Concurrencia/Auditoría** | `ConcurrencyToken` + `If-Match`/`ETag`; auditoría por operación vía `PersonnelFileEmployeeAudits.LogUpdateAsync`. | Se conserva ✅. |
| 16 | **Estado completado** | Solo expedientes **completados** (`IsCompletedEmployee`) pueden gestionar seguros (`Insurances.Handlers.cs:46`). | Se conserva (RN-01). |
| 17 | **Funciones adyacentes** | En el mismo controlador existen **siniestros/reclamos médicos** (`PersonnelFileMedicalClaim`, `…:957`) y **beneficios adicionales** (`additional-benefits`) — **features separadas**. | **Fuera de alcance** (FA). |

---

## Decisiones propuestas (pendientes de ratificación — 2026-06-22)

> A diferencia de análisis previos ya ratificados, **estas decisiones son propuestas del analista** y **requieren confirmación del negocio** (ver §17). Se marcan **D-0x** para trazabilidad.

| # | Tema | Decisión propuesta |
|---|---|---|
| **D-01** | Naturaleza | **Beneficio documental.** El seguro **no** afecta ingresos/egresos del empleado; las cuotas son **informativas** (no se deducen en planilla). **Ya es así** en código — se **protege** como invariante. |
| **D-02** | Nombre de seguro | **Catalogar.** `InsuranceCode` pasa de texto libre a **referencia a catálogo** validado. *(Alcance del catálogo → P-01.)* |
| **D-03** | Rango | **Catalogar.** `RangeCode` pasa a **referencia a catálogo** validado. *(¿Obligatorio? → P-02.)* |
| **D-04** | Parentesco | **Ya catalogado** (categoría `Kinship`). **Sin cambios.** |
| **D-05** | Múltiples seguros | **Permitidos** (sin "único activo"). **Ya es así.** |
| **D-06** | Vigencia | `StartDateUtc ≤ EndDateUtc` **validado**; fechas **opcionales** (el requerimiento no las pide; son mejora existente). |
| **D-07** | Montos | Cuotas y valor asegurado **≥ 0** (no negativos). |
| **D-08** | Enriquecimiento del catálogo de seguro | *(Opcional)* el ítem de catálogo puede llevar **aseguradora/proveedor** y **tipo** (vida/salud/dental). → P-03. |
| **D-09** | Beneficiarios — asignación | *(Opcional)* **% de asignación** (suma 100 %) y **principal/contingente**. → P-04 (no incluir si el negocio no lo pide). |
| **D-10** | Beneficiarios — documento | *(Opcional)* **tipo de documento** del beneficiario reutilizando catálogo `IdentificationType` (DUI/NIT/Pasaporte/Carné). → P-05. |
| **D-11** | Permisos | **A definir:** mantener `Read/Manage` o migrar a `ViewCompensation`/permiso dedicado por sensibilidad (PII de beneficiarios + montos). → P-06. |
| **D-12** | Moneda | Conservar `CurrencyCode` (mejora existente); **opcionalmente** catalogar ISO-4217. → P-08. |

---

## Brechas verificadas y su resolución (GAP → Resolución)

> **Leyenda:** 🔴 **Brecha de alineación con el requerimiento escrito** (debe resolverse para estar "alineado"). 🟡 **Endurecimiento HRIS** (bajo costo, recomendado). 🟢 **Enriquecimiento opcional** (confirmar; **no incluir** si el negocio no lo pide).

| # | Sev. | Brecha (as-is) | Resolución (to-be) |
|---|---|---|---|
| **G-01** | 🔴 | `InsuranceCode` **texto libre**; el requerimiento exige catálogo. | Catalogar **nombre de seguro** (D-02, RF-002). |
| **G-02** | 🔴 | `RangeCode` **texto libre**; el requerimiento exige catálogo. | Catalogar **rango** (D-03, RF-003). |
| **G-03** | 🟡 | Sin validación `StartDateUtc ≤ EndDateUtc`. | Validar orden de fechas (D-06, RF-005). |
| **G-04** | 🟡 | Montos sin validación de no-negatividad. | Cuotas/valor asegurado **≥ 0** (D-07, RF-006). |
| **G-05** | 🟡 | Sin módulo de reglas puro (`Insurances.Rules.cs`). | Crear módulo de reglas **unit-testeable** (RNF). |
| **G-06** | 🟢 | Beneficiario: `DocumentNumber` **sin tipo** de documento. | *(Opcional)* `DocumentTypeCode` → catálogo `IdentificationType` (D-10, RF-008). |
| **G-07** | 🟢 | Sin **% de asignación** ni **principal/contingente** de beneficiarios. | *(Opcional)* modelar asignación (D-09, RF-009) **si** el negocio lo requiere. |
| **G-08** | 🟢 | El catálogo de seguro no estructura **aseguradora/proveedor** ni **tipo**. | *(Opcional)* enriquecer el ítem de catálogo (D-08, RF-002 ext.). |
| **G-09** | 🟡/❓ | Seguros usa `Read/Manage` genérico, no `ViewCompensation`. | **Decisión** de gobierno/sensibilidad (D-11, RF-010, P-06). |
| **G-10** | 🟢 | Sin validación de **duplicado** (misma póliza repetida / mismo beneficiario repetido). | *(Opcional)* unicidad blanda (RF-005, P-09). |

> **Lectura ejecutiva:** las **únicas** brechas **🔴 obligatorias** para cumplir el requerimiento escrito son **G-01** y **G-02** (catalogar nombre de seguro y rango). Todo lo demás es **opcional** o **endurecimiento menor**. Si el negocio **no** desea enriquecimientos y **no** hay datos productivos que migrar, el alcance se reduce a **2 catálogos + 2 validaciones** y la fase se **cierra** rápidamente.

---

## 1. Resumen del producto o requerimiento

Sección del expediente para **registrar los seguros a los que el empleado está afiliado** como **beneficio de la empresa**, junto con sus **beneficiarios**. Cada seguro captura **nombre, cuota empleado, cuota patronal, rango, póliza, valor asegurado y estado activo**; cada beneficiario captura **nombre, documento, fecha de nacimiento y parentesco**. **Nombre de seguro, rango y parentesco** provienen de **catálogos**. El empleado puede tener **varios** seguros, y el registro **no afecta sus ingresos ni egresos** (las cuotas son **informativas**).

La funcionalidad **ya existe** (`PersonnelFileInsurance` + `PersonnelFileInsuranceBeneficiary`) con CRUD completo, auditoría y concurrencia. Este análisis la **valida** (resultado: **mayormente alineada**) y propone un **endurecimiento focalizado**: **catalogar nombre de seguro y rango** (única desalineación con el requerimiento), validar **orden de fechas** y **montos no negativos**, extraer un **módulo de reglas** y **decidir** el gobierno de permisos; además presenta un **menú de enriquecimientos HRIS opcionales** para que el negocio elija **solo lo necesario**.

---

## 2. Objetivos del negocio

- **O-1. Registro confiable de beneficios de seguro:** dejar constancia trazable de a qué seguros está afiliado cada empleado y quiénes son sus beneficiarios.
- **O-2. Integridad de datos:** estandarizar **nombre de seguro, rango y parentesco** vía **catálogo** (consistencia y reportabilidad), evitando texto libre.
- **O-3. Separación clara de la nómina:** garantizar que el seguro es **informativo** y **no** altera ingresos/egresos del empleado (sin acoplar a planilla).
- **O-4. Flexibilidad:** permitir **múltiples seguros** por empleado y múltiples **beneficiarios** por seguro.
- **O-5. Trazabilidad y control:** operaciones **auditadas**, **concurrencia-seguras** y bajo el **permiso** adecuado.
- **O-6. No sobre-construir:** incorporar **solo** lo necesario para un HRIS robusto; los enriquecimientos avanzados se incluyen **únicamente** si el negocio los pide.

---

## 3. Alcance funcional

- **F1.** **Validación** del registro de seguros y beneficiarios existente (CRUD vía API REST) — **ya implementado**.
- **F2.** **Catálogo de nombre de seguro** y **catálogo de rango**, validados como activos (G-01, G-02).
- **F3.** **Parentesco** desde catálogo `Kinship` — **ya implementado** (se reafirma).
- **F4.** **Reglas de vigencia** (orden de fechas) y **montos no negativos** (G-03, G-04).
- **F5.** **Invariante de independencia de nómina**: las cuotas son informativas; **no** se enlazan a `CompensationConcept`/planilla (G — reafirmar, RF-007).
- **F6.** **Módulo de reglas puro** + tests + paridad de localización ES/EN (RNF).
- **F7.** **Decisión de gobierno de permisos** (mantener `Read/Manage` o migrar) (G-09).
- **F8.** *(Opcional, sujeto a §17)* Enriquecimientos: **proveedor/tipo** en catálogo de seguro, **% asignación + principal/contingente** de beneficiarios, **tipo de documento** del beneficiario.

---

## 4. Fuera de alcance

- **FA-1.** **Siniestros / reclamos médicos** (`PersonnelFileMedicalClaim`) — feature **separada existente**.
- **FA-2.** **Cálculo o deducción en planilla** de cuotas (el seguro **no** afecta ingreso/egreso — D-01). Integración con el módulo de **Ingresos/Egresos** (`CompensationConcept`) **fuera de alcance**.
- **FA-3.** **Beneficios adicionales** genéricos (`additional-benefits`) — feature **separada**.
- **FA-4.** **Gestión del catálogo de aseguradoras** como módulo administrativo completo (alta/baja de proveedores) si se decide enriquecer — se trataría como **dependencia** de catálogos, no como parte del expediente.
- **FA-5.** **Notificaciones** a empleado/beneficiarios/RRHH.
- **FA-6.** **Autoservicio** del empleado para gestionar sus seguros/beneficiarios (salvo decisión contraria — P-06).
- **FA-7.** **Integración con la aseguradora** (sincronización de pólizas/coberturas vía API externa).
- **FA-8.** **Documentos adjuntos** (póliza escaneada, carné) — si se requiere, se canaliza por el módulo de documentos del expediente.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Analista / Gestor de RRHH** | Crea/edita/activa/elimina seguros y beneficiarios y los consulta. Hoy con permiso `Manage`; lectura con `Read` (gobierno a confirmar — P-06). |
| **Empleado titular** | Sujeto de los seguros. **Sin** autoservicio en esta fase (salvo decisión contraria). |
| **Beneficiarios** | Personas designadas (cónyuge, hijos, etc.). **No** son usuarios del sistema; son **datos** del seguro. |
| **Auditor / Cumplimiento** | Consulta la bitácora de cambios (auditoría existente). |
| **Administrador de Catálogos** | Mantiene los catálogos de **nombre de seguro** y **rango** (y, si aplica, **aseguradoras/tipos**). |
| **Sistema — módulo de Ingresos/Egresos** | **No** consume el seguro (independencia de nómina — D-01). Se documenta el **límite** explícitamente. |

---

## 6. Requerimientos funcionales

### RF-001 — Registrar la afiliación de seguro del empleado *(ya implementado — validación)*
**Descripción:** Permitir registrar, por expediente **completado**, uno o varios seguros con: nombre, cuota empleado, cuota patronal, rango, póliza, valor asegurado, estado activo (y, como mejora existente, **moneda** y **fechas de vigencia**).
**Reglas de negocio:**
- Solo expedientes **completados** (`IsCompletedEmployee`) — ya implementado (`Insurances.Handlers.cs:46`).
- Se permiten **múltiples** seguros por empleado (D-05).
- Operación **auditada** y **concurrencia-segura** (`If-Match`) — ya implementado.
**Criterios de aceptación:**
- Dado un expediente completado, cuando registro un seguro con nombre válido, entonces se guarda y queda auditado.
- Dado un expediente **no completado**, entonces **422** `STATE_RULE_VIOLATION` (ya cubierto).
**Prioridad:** Alta · **Dependencias:** ninguna (existe).

### RF-002 — Nombre de seguro desde catálogo *(🔴 G-01)*
**Descripción:** Sustituir `InsuranceCode` de **texto libre** por una **referencia a catálogo** de nombres de seguro, validado como **activo**. *(Alcance del catálogo — país vs. por empresa — se decide en P-01.)*
**Reglas de negocio:**
- `InsuranceCode` **debe** existir y estar **activo** en el catálogo correspondiente al tenant/país.
- *(Opcional D-08)* el ítem de catálogo puede portar **aseguradora/proveedor** y **tipo** (vida/salud/dental).
**Criterios de aceptación:**
- Dado un código fuera de catálogo, entonces **422** `INSURANCE_CODE_INVALID`.
- Dado un código válido, entonces se acepta y persiste normalizado.
**Prioridad:** Alta · **Dependencias:** infraestructura de catálogos (P-01); validador análogo a `ValidateKinshipCodeAsync` (`Catalogs/PersonnelReferenceCatalogs.cs:189`).

### RF-003 — Rango desde catálogo *(🔴 G-02)*
**Descripción:** Sustituir `RangeCode` de **texto libre** por **referencia a catálogo** de rangos, validado como activo. *(¿Obligatorio? — P-02; hoy es opcional.)*
**Reglas de negocio:**
- Si se informa, `RangeCode` **debe** pertenecer al catálogo de rangos activo.
- El **rango** puede depender del **nombre de seguro** (jerarquía/`ParentId`) — a confirmar en P-01/P-03.
**Criterios de aceptación:**
- Dado un rango fuera de catálogo, entonces **422** `INSURANCE_RANGE_INVALID`.
- Dado un rango válido (o vacío, si es opcional), entonces se acepta.
**Prioridad:** Alta · **Dependencias:** RF-002 (si el rango cuelga del seguro).

### RF-004 — Registrar beneficiarios con parentesco catalogado *(ya implementado — validación)*
**Descripción:** Por cada seguro, registrar 0..N beneficiarios con **nombre, documento, fecha de nacimiento y parentesco**; el **parentesco** proviene del catálogo `Kinship` (ya validado).
**Reglas de negocio:**
- `KinshipCode` **debe** existir en el catálogo `Kinship` del tenant (ya implementado — Add/Update/Patch).
- Borrar un seguro **elimina en cascada** sus beneficiarios (ya implementado).
**Criterios de aceptación:**
- Dado un `kinshipCode` fuera de catálogo, entonces **422** `KINSHIP_CODE_INVALID` (ya cubierto).
- Dado un beneficiario válido, entonces se guarda asociado al seguro.
**Prioridad:** Alta · **Dependencias:** ninguna (existe).

### RF-005 — Reglas de vigencia y coherencia *(🟡 G-03)*
**Descripción:** Validar el **orden de fechas** de vigencia y la coherencia con el estado.
**Reglas de negocio:**
- Si ambas fechas se informan, `StartDateUtc ≤ EndDateUtc`.
- *(Opcional)* señalar inconsistencia si `IsActive = true` con `EndDateUtc` pasada.
- Las fechas **siguen siendo opcionales** (el requerimiento no las exige; son mejora existente).
**Criterios de aceptación:**
- Dado `EndDateUtc < StartDateUtc`, entonces **422** `INSURANCE_DATE_RANGE_INVALID`.
**Prioridad:** Media · **Dependencias:** módulo de reglas (RNF).

### RF-006 — Montos no negativos *(🟡 G-04)*
**Descripción:** Validar que **cuota empleado, cuota patronal y valor asegurado** sean **≥ 0**.
**Reglas de negocio:**
- `EmployeeContribution`, `EmployerContribution`, `InsuredAmount` **≥ 0** cuando se informan.
**Criterios de aceptación:**
- Dado un monto negativo, entonces **422** `INSURANCE_AMOUNT_INVALID`.
**Prioridad:** Media · **Dependencias:** módulo de reglas (RNF).

### RF-007 — Independencia de la nómina (invariante protegida) *(reafirmar)*
**Descripción:** Garantizar que el seguro **no** afecta ingresos/egresos: las cuotas son **informativas** y **no** generan transacciones de planilla ni se enlazan a `CompensationConcept`.
**Reglas de negocio:**
- El módulo de Seguros **no** publica eventos ni registros hacia Ingresos/Egresos/planilla.
- Cualquier necesidad futura de **descuento real** de cuota se modelaría en el módulo de **Ingresos/Egresos**, **no** aquí (límite explícito).
**Criterios de aceptación:**
- Dado el alta/edición de un seguro, cuando se consultan ingresos/egresos del empleado, entonces **no** aparece ninguna línea derivada del seguro.
**Prioridad:** Alta (de gobernanza) · **Dependencias:** módulo de Ingresos/Egresos (solo para fijar el límite).

### RF-008 — *(Opcional)* Tipo de documento del beneficiario *(🟢 G-06)*
**Descripción:** Añadir `DocumentTypeCode` al beneficiario, reutilizando el catálogo `IdentificationType` (DUI, NIT, Pasaporte, Carné de residente — `PersonnelReferenceCatalog.cs:29`).
**Reglas de negocio:** Si se informa documento, **debe** indicarse su **tipo** del catálogo.
**Criterios de aceptación:** Dado un tipo fuera de catálogo, entonces **422** `DOCUMENT_TYPE_INVALID`.
**Prioridad:** Baja (opcional) · **Dependencias:** P-05.

### RF-009 — *(Opcional)* Asignación de beneficiarios *(🟢 G-07)*
**Descripción:** Modelar `AllocationPercentage` (0–100) y `BeneficiaryType` (principal/contingente) por beneficiario, validando que los **principales** sumen **100 %** por seguro.
**Reglas de negocio:**
- Suma de `%` de beneficiarios **principales activos** = **100 %** por seguro (si se adopta).
**Criterios de aceptación:** Dado un total ≠ 100 %, entonces **422** `BENEFICIARY_ALLOCATION_INVALID`.
**Prioridad:** Baja (opcional, típico de seguros de **vida**) · **Dependencias:** P-04. **No incluir** si el negocio no lo pide.

### RF-010 — Gobierno de permisos / sensibilidad de datos *(🟡/❓ G-09)*
**Descripción:** Decidir si Seguros mantiene `Read/Manage` genérico o se protege con `ViewCompensation`/un permiso dedicado, dada la presencia de **PII de beneficiarios** (documento, fecha de nacimiento) y **montos** (valor asegurado).
**Reglas de negocio:**
- Si se considera **sensible**, la **lectura** podría exigir `ViewCompensation` (como `CompensationConcepts`) o un permiso propio; la **escritura**, `Manage`.
- Como el seguro **no** es ingreso/egreso, `ViewCompensation` (definido como "salario, ingresos y egresos") podría **no** ser el gate semánticamente correcto → evaluar **permiso dedicado**.
**Criterios de aceptación:** Definida la política, un usuario sin el permiso requerido recibe **403**.
**Prioridad:** Media · **Dependencias:** Provisioning/`IdentityAccess` (P-06).

---

## 7. Requerimientos no funcionales

- **Seguridad / Multi-tenant.** Toda operación filtrada por **tenant** (ya implementado). Revisar **sensibilidad** de PII de beneficiarios y montos (RF-010).
- **Integridad / Consistencia.** Catálogos (RF-002/003), fechas (RF-005) y montos (RF-006) en un **módulo de reglas puro** `Insurances.Rules.cs`, **unit-testeable** (patrón de la casa: `EmploymentAssignments.Rules.cs`).
- **Concurrencia.** `ConcurrencyToken` + `If-Match`/`ETag` (ya implementado).
- **Auditoría.** Operaciones registradas vía `IAuditService` (ya implementado); diff/historial visible **si** el negocio lo pide (alinear con sustituciones).
- **Rendimiento.** Índices existentes `(tenant_id, personnel_file_id, is_active, insurance_code)` y `(tenant_id, insurance_id, is_active)` cubren los listados.
- **Usabilidad.** Selección de **nombre de seguro**, **rango** y **parentesco** por **catálogo** (combos, no texto libre); montos con validación en cliente.
- **Compatibilidad / API.** Mantener los 12 endpoints; el cambio a catálogo es *breaking* **solo** para datos inválidos (aceptable; ver migración).
- **Localización.** Mensajes/errores **bilingües (ES/EN)** con **código estable**; test de **paridad de localización** (convención de la casa).
- **Mantenibilidad.** Tests de reglas y de patch (ya existen `PersonnelFileInsurancePatchTests` y `PersonnelFileInsuranceBeneficiaryPatchTests`); extenderlos a las nuevas validaciones.

---

## 8. Historias de usuario

### HU-001 — Afiliar a un empleado a un seguro
Como **Analista de RRHH**, quiero **registrar los seguros del empleado con sus datos**, para **dejar constancia de los beneficios que la empresa le brinda**.
- Dado un expediente completado, cuando registro un seguro con **nombre de catálogo**, póliza y valor asegurado, entonces se guarda y se audita.
- Dado un expediente no completado, entonces **bloquea** (`STATE_RULE_VIOLATION`).

### HU-002 — Estandarizar nombre de seguro y rango
Como **Analista de RRHH**, quiero **elegir el nombre del seguro y el rango de una lista**, para **mantener datos consistentes y reportables**.
- Dado el catálogo de seguros, cuando selecciono un nombre válido, entonces se acepta (RF-002).
- Dado un nombre o rango fuera de catálogo, entonces **bloquea** (RF-002/003).

### HU-003 — Registrar beneficiarios
Como **Analista de RRHH**, quiero **agregar los beneficiarios del seguro con su parentesco**, para **saber quién recibe el beneficio**.
- Dado un seguro, cuando agrego un beneficiario con **parentesco de catálogo**, entonces se guarda (RF-004).
- Dado un parentesco fuera de catálogo, entonces **bloquea** (ya cubierto).

### HU-004 — Mantener varios seguros sin afectar la nómina
Como **Analista de RRHH**, quiero **registrar varios seguros como beneficio sin que impacten la planilla**, para **separar beneficios de la compensación**.
- Dado un empleado con varios seguros, cuando consulto sus ingresos/egresos, entonces **no** aparece ninguna línea derivada de seguros (RF-007).

### HU-005 — Datos válidos de vigencia y montos
Como **Analista de RRHH**, quiero que **el sistema valide fechas y montos**, para **evitar registros incoherentes**.
- Dado `EndDate < StartDate` o un monto negativo, entonces **bloquea** (RF-005/006).

### HU-006 *(Opcional)* — Asignación de beneficiarios de seguro de vida
Como **Analista de RRHH**, quiero **indicar el porcentaje y si el beneficiario es principal o contingente**, para **reflejar la distribución del seguro de vida**.
- Dado un seguro de vida, cuando los principales no suman 100 %, entonces **bloquea** (RF-009). *(Solo si el negocio adopta P-04.)*

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** (`IsCompletedEmployee`) gestionan seguros (ya implementado).
- **RN-02.** El empleado puede tener **varios** seguros; **no** hay restricción de "único activo" (D-05).
- **RN-03.** **Nombre de seguro** y **rango** **deben** provenir de **catálogo** activo (D-02/D-03, RF-002/003).
- **RN-04.** **Parentesco** **debe** pertenecer al catálogo `Kinship` activo (ya implementado).
- **RN-05.** `StartDateUtc ≤ EndDateUtc` cuando ambas se informan; fechas **opcionales** (D-06, RF-005).
- **RN-06.** Cuotas y valor asegurado **≥ 0** (D-07, RF-006).
- **RN-07.** El seguro **no** afecta ingresos/egresos: las cuotas son **informativas**; **no** se enlazan a planilla/`CompensationConcept` (D-01, RF-007).
- **RN-08.** Borrar un seguro **elimina en cascada** sus beneficiarios (ya implementado).
- **RN-09.** Toda escritura exige **If-Match** y queda **auditada** (ya implementado).
- **RN-10.** *(Opcional)* Si se adopta asignación: los beneficiarios **principales** activos **suman 100 %** por seguro (RF-009).
- **RN-11.** *(Opcional)* Si se informa documento del beneficiario, debe indicarse su **tipo** (RF-008).

---

## 10. Flujos principales

**Flujo: Registrar un seguro (RRHH)**
1. RRHH (con permiso de escritura) abre el expediente del empleado (**completado**).
2. Entra a **"Seguros" → Agregar**.
3. Selecciona el **nombre del seguro** del **catálogo** (RF-002) y, si aplica, el **rango** del catálogo (RF-003).
4. Ingresa **cuota empleado, cuota patronal, póliza, valor asegurado** (validación de montos — RF-006) y **estado activo**; opcionalmente **moneda** y **fechas** (validación de orden — RF-005).
5. Guarda → persiste, **audita**, devuelve `ETag`.

**Flujo: Agregar beneficiarios**
1. Sobre un seguro existente, **Agregar beneficiario**.
2. Ingresa **nombre, documento, fecha de nacimiento** y selecciona **parentesco** del catálogo `Kinship` (RF-004).
3. *(Opcional)* indica **tipo de documento** (RF-008) y **% / principal-contingente** (RF-009).
4. Guarda → persiste y **audita**.

**Flujo: Consultar seguros y beneficiarios**
1. RRHH/Auditor consulta la lista de seguros del empleado y, por seguro, sus beneficiarios (lectura con el permiso definido — RF-010).

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Expediente **no completado**. | **Bloqueo** `422 STATE_RULE_VIOLATION` (ya implementado — `Insurances.Handlers.cs:46`). |
| **E2** | **Nombre de seguro** fuera de catálogo. | **Bloqueo** `422 INSURANCE_CODE_INVALID` (RF-002). |
| **E3** | **Rango** fuera de catálogo. | **Bloqueo** `422 INSURANCE_RANGE_INVALID` (RF-003). |
| **E4** | **Parentesco** fuera de catálogo. | **Bloqueo** `422 KINSHIP_CODE_INVALID` (ya implementado). |
| **E5** | `EndDate < StartDate`. | **Bloqueo** `422 INSURANCE_DATE_RANGE_INVALID` (RF-005). |
| **E6** | Monto **negativo**. | **Bloqueo** `422 INSURANCE_AMOUNT_INVALID` (RF-006). |
| **E7** | `If-Match`/`ConcurrencyToken` no coincide. | **409 CONFLICT** (ya implementado). |
| **E8** | Borrar un seguro con beneficiarios. | Borra **en cascada** los beneficiarios (ya implementado). |
| **E9** | Usuario sin el permiso definido intenta leer/escribir. | **403 FORBIDDEN** (según RF-010). |
| **E10** | *(Opcional)* Principales no suman 100 %. | **Bloqueo** `422 BENEFICIARY_ALLOCATION_INVALID` (RF-009, si se adopta). |
| **E11** | *(Opcional)* Documento sin tipo. | **Bloqueo** `422 DOCUMENT_TYPE_INVALID` (RF-008, si se adopta). |

---

## 12. Datos requeridos

### Entidad: `PersonnelFileInsurance` *(ya existe — `Domain/PersonnelFiles/PersonnelFileEmployee.cs:774`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `personnelFileId` | long (FK) | Sí | del tenant; cascada | ✅ existe | Empleado dueño |
| `insuranceCode` | Texto → **catálogo** | Sí | **catálogo activo** | 🔧 catalogar (RF-002) | **Nombre de seguro** |
| `employeeContribution` | Decimal(18,2) | No | **≥ 0** | 🔧 validar (RF-006) | Cuota empleado (informativa) |
| `employerContribution` | Decimal(18,2) | No | **≥ 0** | 🔧 validar (RF-006) | Cuota patronal (informativa) |
| `rangeCode` | Texto → **catálogo** | No* | **catálogo activo** | 🔧 catalogar (RF-003) | **Rango** (*¿obligatorio? P-02) |
| `policyNumber` | Texto (120) | No | — | ✅ existe | Póliza |
| `insuredAmount` | Decimal(18,2) | No | **≥ 0** | 🔧 validar (RF-006) | Valor asegurado |
| `currencyCode` | Texto (40) | No | *(opcional)* ISO-4217 | ✅ existe (mejora) | Moneda (P-08) |
| `isActive` | Booleano | Sí | — | ✅ existe | Activo (sí/no) |
| `startDateUtc` | Fecha | No | `≤ endDate` | ✅ existe (mejora) | Inicio de vigencia |
| `endDateUtc` | Fecha | No | `≥ startDate` | ✅ existe (mejora) | Fin de vigencia |
| `beneficiaries` | Colección | No | 0..N | ✅ existe | Beneficiarios |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

> *(Opcional D-08/P-03)* el catálogo de `insuranceCode` podría portar **aseguradora/proveedor** y **tipo** (vida/salud/dental) como datos estructurados.

### Entidad: `PersonnelFileInsuranceBeneficiary` *(ya existe — `…PersonnelFileEmployee.cs:891`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `insuranceId` | long (FK) | Sí | del seguro; cascada | ✅ existe | Seguro dueño |
| `fullName` | Texto (200) | Sí | no vacío | ✅ existe | Nombre |
| `documentNumber` | Texto (80) | No | — | ✅ existe | Documento de identidad |
| `documentTypeCode` | Texto → **catálogo** | No | catálogo `IdentificationType` | 🆕 opcional (RF-008) | Tipo de documento (P-05) |
| `birthDate` | Fecha | No | — | ✅ existe | Fecha de nacimiento |
| `kinshipCode` | Texto → **catálogo** | Sí | **catálogo `Kinship`** | ✅ existe | **Parentesco** |
| `allocationPercentage` | Decimal | No | 0–100; principales=100% | 🆕 opcional (RF-009) | % de asignación (P-04) |
| `beneficiaryType` | Enum | No | principal/contingente | 🆕 opcional (RF-009) | Tipo de beneficiario (P-04) |
| `isActive` | Booleano | Sí | — | ✅ existe | Activo |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

### Entidad: `InsuranceNameCatalogItem` / `InsuranceRangeCatalogItem` *(nuevas — RF-002/003)*

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `publicId` | GUID | Sí | único | Identidad |
| `scope` | País **o** Tenant | Sí | según P-01 | **Alcance del catálogo (decisión clave — P-01)** |
| `code` | Texto | Sí | único por alcance | Código estable |
| `name` | Texto | Sí | — | Nombre visible (ES/EN) |
| `parentCode` | Texto | No | rango cuelga del seguro (P-03) | Jerarquía opcional seguro→rango |
| `provider` | Texto | No | *(opcional D-08)* | Aseguradora/proveedor |
| `insuranceType` | Enum | No | vida/salud/dental/… | *(opcional D-08)* Tipo |
| `isActive` | Booleano | Sí | — | Estado |
| `sortOrder` | Número | No | — | Orden de despliegue |

---

## 13. Integraciones necesarias

- **Catálogos.** **Nuevos** catálogos de **nombre de seguro** y **rango** (alcance por definir — P-01). Catálogo `Kinship` **ya integrado** (parentesco). *(Opcional)* `IdentificationType` para tipo de documento del beneficiario.
- **Repositorio de expedientes (`IPersonnelFileEmployeeRepository`).** Persistencia de seguros/beneficiarios (ya implementado).
- **Identidad/Provisioning (`IdentityAccess`).** Política de permisos de Seguros (RF-010); alta de permiso dedicado **si** se decide.
- **Auditoría (`IAuditService`).** Registro de operaciones (ya implementado).
- **Módulo de Ingresos/Egresos (`CompensationConcept`).** **Solo** para fijar el **límite**: el seguro **no** se integra a planilla (RF-007). **Sin** acoplamiento.
- **Aseguradora externa / Notificaciones / Documentos.** **No aplica** en esta fase (FA).

---

## 14. Roles y permisos

| Rol | Permisos (as-is) | Restricciones |
|---|---|---|
| **RRHH / Administrador de Expedientes** | Crear/editar/activar/eliminar y leer seguros y beneficiarios (**`PersonnelFiles.Manage`** + `Read`). | Solo expedientes **completados**; solo su **tenant**. |
| **Consulta / Auditor** | Leer seguros y beneficiarios (**`PersonnelFiles.Read`**). | Sin escritura. |
| **Administrador de Catálogos** | Mantener catálogos de nombre de seguro/rango (y aseguradoras/tipos si aplica). | Según gobierno de catálogos. |
| **Empleado titular** | — | **Sin** autoservicio en esta fase (salvo P-06). |

> **Decisión pendiente (RF-010, P-06):** evaluar si la **sensibilidad** (PII de beneficiarios + montos) justifica migrar la **lectura** a `ViewCompensation` o a un **permiso dedicado** (p. ej. `PersonnelFiles.ViewInsurance`), en lugar del `Read` genérico actual. Hoy: `Read/Manage` (`…CompensationController.cs:27`).

---

## 15. Criterios de aceptación generales

- ✅ Todos los campos del requerimiento están presentes para seguro y beneficiario (**ya cumplido**).
- ✅ **Nombre de seguro** y **rango** se eligen de **catálogo** (RF-002/003) — *(brecha a cerrar)*.
- ✅ **Parentesco** se elige de **catálogo** (**ya cumplido**).
- ✅ Se permiten **múltiples** seguros por empleado (**ya cumplido**).
- ✅ El seguro **no** genera ingresos/egresos ni líneas de planilla (RF-007 — **ya cumplido**, se protege).
- ✅ Fechas coherentes (`Start ≤ End`) y montos **≥ 0** (RF-005/006).
- ✅ Operaciones **concurrencia-seguras (If-Match)** y **auditadas** (**ya cumplido**).
- ✅ Las reglas viven en un **módulo puro** con **tests unitarios** y **paridad de localización ES/EN**.
- ⚙️ Política de **permisos** de Seguros **definida y aplicada** (RF-010).
- ⚙️ *(Si se adoptan)* enriquecimientos opcionales (tipo de documento, asignación de beneficiarios, proveedor/tipo) **validados**.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1. Alcance del catálogo (P-01).** Si el negocio espera que cada **empresa** configure sus propios seguros/rangos (lo más probable, pues son productos negociados), un catálogo **país-scoped estático** (como `Kinship`) **no** encaja; se requiere un catálogo **configurable por tenant**. Elegir mal el alcance implica retrabajo. **Decisión bloqueante** para RF-002/003.
- **R2. Datos sucios existentes.** Registros con `insuranceCode`/`rangeCode` de texto libre; al catalogar, esos valores quedarían **inválidos** → requiere **limpieza/migración** antes de activar validaciones duras.
- **R3. Sobre-construcción.** El requerimiento pide **no agregar** lo innecesario; los enriquecimientos (RF-008/009 y proveedor/tipo) deben **confirmarse** y no asumirse.
- **R4. Semántica de `ViewCompensation`.** Reusar `ViewCompensation` para Seguros puede ser **incoherente** (el seguro no es salario/ingreso/egreso); un permiso dedicado sería más limpio (R-010).

### Supuestos
- **S1.** Existe infraestructura de catálogos reutilizable (referencia país `PersonnelReferenceCatalog`; configurables `CountryScopedCatalogItem`, `DocumentTypeCatalogItem`, etc.) para alojar los nuevos catálogos.
- **S2.** El sustituto del texto libre por catálogo es aceptable como cambio *breaking* para datos inválidos (patrón aplicado en otros módulos).
- **S3.** País de referencia **SV**; seeds en ES (con `name` ES/EN).
- **S4.** Las cuotas **no** se descuentan en planilla en esta fase (D-01); si en el futuro se requiere, se modela en Ingresos/Egresos.

### Dependencias
- **D1.** Infraestructura de **catálogos** (nombre de seguro, rango) — **decisión de alcance P-01**.
- **D2.** `IPersonnelFileEmployeeRepository` (existe).
- **D3.** `IdentityAccess`/Provisioning (permiso de Seguros — RF-010).
- **D4.** `IAuditService` (existe).
- **D5.** Catálogo `IdentificationType` (existe) — solo si se adopta RF-008.

---

## 17. Preguntas abiertas para el cliente o stakeholders

> **Pendiente de ratificación.** Junto a cada pregunta se incluye la **recomendación del analista**.

| # | Pregunta | Recomendación |
|---|---|---|
| **P-01** | **(Bloqueante)** ¿El catálogo de **nombre de seguro** y **rango** es de **alcance país** (referencia universal, como parentesco) o **configurable por empresa/tenant**? | **Configurable por tenant** (cada empresa negocia sus propios seguros/rangos con la aseguradora). |
| **P-02** | ¿El **rango** es **obligatorio**? Hoy es opcional. | **Mantener opcional** salvo exigencia explícita; si el seguro lo requiere, hacerlo condicional al tipo. |
| **P-03** | ¿El **rango** depende del **nombre de seguro** (jerarquía)? | **Sí**, modelar `parentCode` (rango cuelga del seguro) para coherencia. |
| **P-04** | ¿Se requieren **% de asignación** y **principal/contingente** de beneficiarios (típico de seguro de **vida**)? | **Confirmar.** Recomendado para vida; **no incluir** si el negocio no lo pide (evitar sobre-construir). |
| **P-05** | ¿El **documento** del beneficiario debe llevar **tipo** (DUI/Pasaporte/…)? | **Sí** (bajo costo): reutilizar catálogo `IdentificationType`. |
| **P-06** | ¿**Gobierno de permisos**? ¿Seguros sigue con `Read/Manage` o pasa a `ViewCompensation`/permiso dedicado por sensibilidad (PII + montos)? ¿Hay **autoservicio**? | **Permiso dedicado de lectura** (p. ej. `ViewInsurance`) por PII; **sin** autoservicio en esta fase. |
| **P-07** | ¿Las **cuotas** son **solo informativas** (sin deducción en planilla) y así permanece? | **Sí** (ya es así). Documentar el **límite** con Ingresos/Egresos (RF-007). |
| **P-08** | ¿Se **cataloga la moneda** (ISO-4217) o se deja texto? ¿Se conservan las **fechas de vigencia**? | **Conservar** fechas; **catalogar** moneda si se desea consistencia. |
| **P-09** | ¿Reglas de **unicidad/duplicado**? (mismo seguro+póliza repetido; mismo beneficiario repetido en un seguro) | **Evitar duplicado exacto** de póliza por empleado; permitir múltiples seguros. |
| **P-10** | ¿Existen **datos productivos/QA** de seguros que requieran **migración** al catalogar? | Si **no** hay datos → normalización directa; si **sí** → migración previa. |
| **P-11** | ¿Se requiere **historial/diff visible** de cambios (como en sustituciones) o basta la auditoría actual? | Auditoría actual suficiente; **diff visible** solo si lo piden. |

---

## 18. Recomendaciones del Analista de Negocio

1. **Reconocer el alto grado de alineación.** La entidad, CRUD, beneficiarios, catálogo de parentesco, auditoría, concurrencia y multitenancy **ya existen y están bien construidos**, e incluso **superan** el requerimiento (moneda, vigencia). **No es una reconstrucción.**

2. **Cerrar la única brecha de alineación real (🔴):** **catalogar nombre de seguro y rango** (RF-002/003). El parentesco ya cumple. Antes de implementar, **resolver P-01** (alcance país vs. tenant) — recomendado **tenant-configurable**, porque los seguros son productos propios de cada empresa.

3. **Endurecimiento mínimo de bajísimo costo (🟡):** validar **orden de fechas** (RF-005) y **montos no negativos** (RF-006), y **extraer** un **módulo de reglas puro** `Insurances.Rules.cs` con **tests** y **paridad de localización** (patrón de la casa). Reutilizar el patrón de `ValidateKinshipCodeAsync` para los validadores de catálogo.

4. **Proteger la independencia de nómina (RF-007).** Dejar **explícito** (código + doc) que las cuotas son **informativas** y que el módulo **no** se acopla a Ingresos/Egresos/planilla. Cualquier descuento real futuro se modela en el módulo de **Ingresos/Egresos**, no aquí.

5. **Decidir el gobierno de permisos (P-06).** Por la **PII de beneficiarios** y los **montos**, evaluar un **permiso dedicado de lectura** (p. ej. `PersonnelFiles.ViewInsurance`) en lugar del `Read` genérico; **evitar** reusar `ViewCompensation` por incoherencia semántica (el seguro no es ingreso/egreso).

6. **Tratar los enriquecimientos como un menú opcional (🟢), no como alcance por defecto.** Respetando la instrucción de **no sobre-construir**: presentar **proveedor/tipo** en el catálogo (D-08), **% asignación + principal/contingente** (RF-009) y **tipo de documento** (RF-008) como **decisiones del negocio**; **incluir solo lo que pidan**.

7. **MVP sugerido y posible cierre rápido.** Si el negocio **no** desea enriquecimientos y **no** hay datos a migrar, el alcance se reduce a **2 catálogos + 2 validaciones + módulo de reglas/tests**, y la fase puede **cerrarse** con bajo esfuerzo. Si tampoco se desea catalogar (aceptando texto libre), el desarrollo quedaría **alineado al uso real** y el requerimiento se **cierra sin desarrollo** — pero esto **contradice** el texto ("se elegirá de catálogos"), por lo que se recomienda **al menos** RF-002/003.

---

> **Naturaleza del documento.** Análisis de **validación + brechas (GAP)** sobre funcionalidad **ya implementada** (`PersonnelFileInsurance` + `PersonnelFileInsuranceBeneficiary`). El **estado as-is está verificado contra el código** (referencias `archivo:línea`). **Conclusión:** el desarrollo está **mayormente alineado**; la **única** desalineación con el requerimiento escrito es que **nombre de seguro y rango son texto libre** (deberían ser catálogo, como ya lo es el parentesco). El resto son **endurecimientos menores** y **enriquecimientos opcionales** sujetos a ratificación. **Las decisiones D-01…D-12 y las preguntas P-01…P-11 están pendientes de confirmación del negocio**; una vez resueltas, el documento queda listo para diseño técnico o para **cierre** si el negocio opta por no desarrollar.
