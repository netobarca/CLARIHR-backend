# Análisis de Negocio — Reclamos de Seguro Médico (expediente del empleado)

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo, Cumplimiento/Privacidad |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Seguros y Beneficiarios (`PersonnelFileInsurance`, `PersonnelFileInsuranceBeneficiary`) · Reclamos médicos (`PersonnelFileMedicalClaim`) · Catálogos generales (`GeneralCatalogs`) · Identidad/Permisos (`IdentityAccess`/Provisioning) |
| **Estado** | **Borrador para validación del negocio.** El desarrollo **ya existe** (`PersonnelFileMedicalClaim`, CRUD completo). Este documento **valida la alineación** y **levanta brechas**; las decisiones P-01…P-13 quedan **abiertas** para ratificación del negocio. **No** se inventan decisiones. |
| **Versión** | v1 (validación inicial contra código) |
| **Fecha** | 2026-06-22 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |
| **Naturaleza del módulo** | **Solo de registro** (informativo/consulta). **No** es un motor de gestión/aprobación/pago de reclamos. |

---

## Contexto del cambio

En el **expediente del empleado** se requiere una sección para **consultar los reclamos de seguro médico** realizados por el empleado, **propios o de sus familiares beneficiarios**. La información a mostrar es: **nombre del seguro médico, número de cuenta, tipo de reclamo, diagnóstico, monto del reclamo, moneda, monto pagado, tiempo de respuesta y observaciones**. El módulo es **solo de registro**.

El objetivo declarado es **doble**: (1) **validar** que lo ya implementado esté **bien alineado** con el requerimiento y con las buenas prácticas HRIS, y (2) **analizar y agregar** la información/reglas necesarias; **si no hace falta nada más, cerrar**.

> **Hallazgo clave (confirmado en código).** Esto **no es un desarrollo desde cero**. CLARIHR **ya tiene implementada** la funcionalidad como entidad **`PersonnelFileMedicalClaim`**, con **CRUD completo** (Domain + Application/CQRS + API REST + auditoría + concurrencia + soft-delete + JSON Patch). De los **9 campos** del requerimiento, **8 están cubiertos** (uno —"nombre del seguro"— se almacena como **referencia** y no se **resuelve** a nombre en la lectura). **Falta 1 capacidad explícita del requerimiento**: distinguir si el reclamo es **del titular o de un beneficiario** ("propios o por familiares beneficiarios"). Por tanto **no procede "cerrar sin cambios"**: existe al menos **una brecha funcional** atada directamente al texto del requisito, más **una brecha de privacidad relevante** (el **diagnóstico** —dato de salud— está protegido por permiso **genérico**, más débil que el salario).

### Veredicto de alineación (resumen ejecutivo)

| Dimensión | Veredicto |
|---|---|
| **Cobertura de campos del requisito** | **8 / 9** presentes; **1** ("propios/beneficiario") **ausente**; "nombre del seguro" presente como **referencia sin resolver**. |
| **Calidad del modelo (HRIS)** | **Aceptable como registro**, pero con **texto libre** donde debería haber **catálogo** (tipo de reclamo, moneda) y **sin validaciones monetarias**. |
| **Privacidad / dato sensible** | ⚠️ **Brecha**: el **diagnóstico** (categoría especial de datos de salud) se protege solo con `PersonnelFiles.Read` genérico, **no** con un permiso dedicado (a diferencia del salario, que sí usa `ViewCompensation`). |
| **Infra técnica (CRUD/audit/concurrencia/multi-tenant)** | ✅ **Sólida y consistente** con el resto del expediente. |
| **¿Cerrar sin cambios?** | ❌ **No.** Se recomienda una **Fase 1 acotada** (campo de paciente/beneficiario + catálogos + permiso dedicado de salud + validaciones). El resto puede diferirse. |

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) |
|---|---|---|
| 1 | **Entidad** | `PersonnelFileMedicalClaim` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:957`). CRUD completo Add/Update/Patch/Delete/Get/GetList (`Application/Features/PersonnelFiles/Compensation/MedicalClaims.cs` + `MedicalClaims.Handlers.cs`). |
| 2 | **Campos** | `InsurancePublicId?`, `AccountNumber?`, `ClaimTypeCode` (req), `Diagnosis?`, `ClaimAmount?`, `CurrencyCode?`, `PaidAmount?`, `ResponseTimeDays?`, `Notes?`, `ClaimDateUtc` (req), `SourceSystem?`/`SourceReference?`/`SourceSyncedUtc?` (integración), `IsActive`, `ConcurrencyToken` (`PersonnelFileEmployee.cs:995-1027`). |
| 3 | **"Nombre del seguro"** | Se guarda **`InsurancePublicId`** (`Guid?`, referencia a `PersonnelFileInsurance`, **sin FK**, loose coupling, nullable, **sin validar** — `PersonnelFileEmployee.cs:999`; EF sin FK en `…Configuration.cs:398`). El response devuelve `InsuranceId` (Guid), **no resuelve el nombre** (`MedicalClaims.cs:20-22`). El seguro se identifica por `InsuranceCode` (**código de catálogo**, no nombre libre — `PersonnelFileEmployee.cs:812`). |
| 4 | **Paciente / beneficiario** | **No existe** campo que indique de quién es el reclamo (**titular vs. beneficiario**) ni referencia a un beneficiario. Las pólizas **sí** tienen beneficiarios: `PersonnelFileInsuranceBeneficiary` (`FullName`, `DocumentNumber`, `BirthDate`, `KinshipCode` — `PersonnelFileEmployee.cs:891-955`), pero el **reclamo no se vincula** a ellos. |
| 5 | **Tipo de reclamo** | `ClaimTypeCode`: **texto libre** validado solo `NotEmpty().MaximumLength(80)` (`MedicalClaims.cs:94-100`); **sin catálogo**. |
| 6 | **Moneda** | `CurrencyCode`: **texto libre** `max 40` (`…Configuration.cs:403`); **sin catálogo** ISO 4217; longitud sobredimensionada. |
| 7 | **Montos** | `ClaimAmount`, `PaidAmount`: `numeric(18,2)` nullable (`…Configuration.cs:402,404`); **sin validación** (ni no-negativos, ni `PaidAmount ≤ ClaimAmount`, ni moneda obligatoria si hay monto). |
| 8 | **Tiempo de respuesta** | `ResponseTimeDays`: `int?` **manual** (`PersonnelFileEmployee.cs:1013`); **sin validar** (`≥0`); **no** se deriva de fechas (no hay fecha de resolución/pago). |
| 9 | **Estado del reclamo** | **No existe** estado (PRESENTADO/EN_TRÁMITE/PAGADO/PARCIAL/RECHAZADO). Coherente con "solo registro", pero un registro HRIS típico lo captura. |
| 10 | **Fecha del reclamo** | `ClaimDateUtc` **requerida por tipo** (no-nullable) y normalizada a fecha (`PersonnelFileEmployee.cs:989`); **pero** el validador **no** la valida (podría quedar `0001-01-01` o futura). |
| 11 | **Permisos** | Controller `PersonnelFileCompensationController` con `[Authorize]` + `[AuthorizationPolicySet(Read, Manage)]` **genérico** (`…Controller.cs:25-27`); los endpoints de *medical-claims* **no** lo sobreescriben. Handlers: escritura `LoadForManageAsync` (**Manage genérico**, `MedicalClaims.Handlers.cs:34`); lectura `LoadCompletedEmployeeForReadAsync` (**Read genérico**, `:362`). **No** usan el gate `ViewCompensation` que sí protege salario/conceptos (`PersonnelFileCompensationConceptsController.cs:20`; `PersonnelFileEmployeeHandlerBases.cs:200,228`). |
| 12 | **Dato sensible (salud)** | `Diagnosis` (categoría especial de datos de salud) **sin permiso dedicado**, **sin enmascaramiento**; auditado igual que el resto (`PersonnelFileEmployeeAudits.LogUpdateAsync`, `MedicalClaims.Handlers.cs:77,162,267,335`). |
| 13 | **Infra transversal** | `ConcurrencyToken` + `If-Match`/`ETag`; auditoría por operación; **solo expedientes completados** (`IsCompletedEmployee`, `MedicalClaims.Handlers.cs:46`); **soft-delete** `IsActive` (PUT preserva, PATCH muta — `:133`); multi-tenant (`TenantId`). |
| 14 | **Semántica/ubicación** | Vive bajo **"Compensation"** (carpeta `Features/PersonnelFiles/Compensation`, `PersonnelFileCompensationController`, namespace) aunque un **reclamo médico no es compensación**; es dato de **beneficios/salud**. |
| 15 | **Integración** | `SourceSystem`/`SourceReference`/`SourceSyncedUtc` **preparan** la sincronización desde sistemas de aseguradora, pero **no hay** integración implementada (captura **manual**). |
| 16 | **Lectura/consulta** | `GET` lista **todos** los reclamos del expediente (`IReadOnlyCollection`, **sin** filtros/paginación/agregados — `MedicalClaims.Handlers.cs:350-375`). |
| 17 | **Índices** | Único `uq_…__public_id`; compuesto `ix_…__tenant_file_date_type` (`tenant_id, personnel_file_id, claim_date_utc, claim_type_code` — `…Configuration.cs:421-423`); FK **solo** a `personnel_file` (`:419`). |

---

## Matriz de validación: requerimiento ↔ implementación

| Campo del requerimiento | Campo implementado | Tipo | Oblig. | Estado | Observación |
|---|---|---|---|---|---|
| **Nombre del seguro médico** | `InsurancePublicId` (ref. a `PersonnelFileInsurance`, **sin FK**) | `Guid?` | No | ⚠️ **Parcial** | Guarda **referencia**, no nombre; el read devuelve `InsuranceId` (Guid) y **no resuelve el nombre**; referencia **no validada**. |
| **Número de cuenta** | `AccountNumber` | `string?` (≤120) | No | ✅ | Texto libre; podría **derivarse** del seguro/póliza. |
| **Tipo de reclamo** | `ClaimTypeCode` | `string` (≤80) | **Sí** | ⚠️ | **Texto libre**, sin catálogo. |
| **Diagnóstico** | `Diagnosis` | `string?` (≤1000) | No | ✅ ⚠️ sensible | **Dato de salud** (categoría especial). |
| **Monto del reclamo** | `ClaimAmount` | `decimal(18,2)?` | No | ⚠️ | Sin validación (no-negativo). |
| **Moneda** | `CurrencyCode` | `string?` (≤40) | No | ⚠️ | **Texto libre**, sin catálogo ISO 4217; longitud sobredimensionada. |
| **Monto pagado** | `PaidAmount` | `decimal(18,2)?` | No | ⚠️ | Sin validación (`≤ ClaimAmount`, no-negativo). |
| **Tiempo de respuesta** | `ResponseTimeDays` | `int?` | No | ⚠️ | **Manual**; sin validar (`≥0`); no derivado de fechas. |
| **Observaciones** | `Notes` | `string?` (≤2000) | No | ✅ | |
| **Propio / familiar beneficiario** | — | — | — | ❌ **Ausente** | **No existe** campo de paciente/beneficiario. **Brecha funcional principal.** |
| *(extra)* Fecha del reclamo | `ClaimDateUtc` | `DateTime` | Sí | ✅➕ | Añadido correcto (no estaba en el requisito); validar rango. |
| *(extra)* Origen/integración | `SourceSystem`/`SourceReference`/`SourceSyncedUtc` | `string?`/`string?`/`DateTime?` | No | ✅➕ | Punto de extensión para sync con aseguradora. |
| *(extra)* Estado/concurrencia | `IsActive`, `ConcurrencyToken` | `bool`/`Guid` | Sí | ✅➕ | Soft-delete + concurrencia optimista. |

> **Lectura del veredicto:** la implementación **cubre el grueso** del requisito con buena infraestructura, pero **(a)** no resuelve el **nombre** del seguro en la lectura, **(b)** carece del **vínculo paciente/beneficiario** exigido explícitamente, y **(c)** trata datos de **salud** con protección de **lectura genérica**. Estas tres son el corazón del trabajo de Fase 1.

---

## Brechas verificadas y su resolución propuesta (GAP → To-be)

| # | Brecha (as-is, verificada) | Resolución propuesta (to-be) | Severidad |
|---|---|---|---|
| **G-01** | **No hay** forma de indicar si el reclamo es del **titular** o de un **beneficiario** (familiar). Incumple "propios o por familiares beneficiarios". | Agregar `ClaimantType` (catálogo `TITULAR`/`BENEFICIARIO`) + `BeneficiaryPublicId?` (ref. a `PersonnelFileInsuranceBeneficiary`) y/o **snapshot** `PatientName?` + `KinshipCode?` (RF-001). | **Alta** |
| **G-02** | `InsurancePublicId` **sin FK ni validación**; el read **no resuelve** el nombre del seguro (solo Guid). | Validar la referencia (existe/tenant/**pertenece al expediente**); **proyectar** `InsuranceName`/`PolicyNumber` en el response (RF-002). | **Alta** |
| **G-03** | `ClaimTypeCode` **texto libre**. | Catálogo `medical-claim-types` (país-scoped) (RF-003). | Media |
| **G-04** | `CurrencyCode` **texto libre** (≤40), sin ISO. | Catálogo **ISO 4217** + longitud a 3; moneda **obligatoria si hay monto** (RF-004). | Media |
| **G-05** | Montos sin validar. | `≥ 0`; `PaidAmount ≤ ClaimAmount`; moneda requerida cuando hay importe (RF-005). | Media |
| **G-06** | `ResponseTimeDays` manual, sin validar; sin fecha de resolución. | Validar `≥ 0`; **opción**: capturar `ResolutionDateUtc?`/`PaymentDateUtc?` y **derivar** el tiempo (RF-006). | Baja |
| **G-07** | **Sin estado** del reclamo. | **Opcional**: catálogo `medical-claim-status` (atributo, **no** workflow) (RF-007). | Baja |
| **G-08** | **Diagnóstico (salud)** detrás de **Read/Manage genérico**, más débil que el salario; sin enmascaramiento. | **Permiso dedicado** `PersonnelFiles.ViewMedicalClaims` / `ManageMedicalClaims`; **enmascarar** diagnóstico sin permiso; auditoría reforzada (RF-008). | **Alta** |
| **G-09** | `ClaimDateUtc` sin validar (puede quedar `0001-01-01`/futura). | Validar requerida y **no futura**; coherente con la vigencia del seguro (RF-009). | Baja |
| **G-10** | Sin **módulo de reglas puro** (patrón de la casa). | Crear `MedicalClaims.Rules.cs` unit-testeable (RNF). | Media |
| **G-11** | Lectura sin **filtros/agregados**. | **Opcional**: filtros (fecha/tipo/beneficiario) y totales por año/beneficiario (RF-010). | Baja |
| **G-12** | Sin **adjuntos** (formulario, factura, EOB). | **Pregunta abierta** P-11 (posible fuera de alcance "solo registro"). | Baja |
| **G-13** | Integración aseguradora **no** implementada (solo campos `Source*`). | **Diferido**: dejar los campos como punto de extensión (RF-011). | Baja |

---

## 1. Resumen del producto o requerimiento

Sección del expediente para **consultar y registrar los reclamos de seguro médico** del empleado y de sus **familiares beneficiarios**: qué seguro, número de cuenta, tipo de reclamo, **diagnóstico**, montos (reclamado y pagado), moneda, **tiempo de respuesta** de la aseguradora y observaciones. Es un **registro documental/informativo** (**no** procesa, **no** aprueba, **no** paga reclamos).

La funcionalidad **ya existe** (`PersonnelFileMedicalClaim`); este alcance la **valida y endurece**: añade la **dimensión paciente/beneficiario** (núcleo del requisito), **resuelve el nombre** del seguro en la lectura, **cataloga** tipo de reclamo y moneda, **valida** montos/fechas, y **protege el diagnóstico** con un permiso dedicado de datos de salud.

---

## 2. Objetivos del negocio

- **O-1.** **Trazabilidad de beneficios médicos:** dejar constancia consultable de los reclamos del empleado **y de sus beneficiarios**, con quién, cuánto y cuándo.
- **O-2.** **Cumplir el requisito explícito** de distinguir reclamos **propios vs. de familiares beneficiarios** (hoy no es posible).
- **O-3.** **Integridad y reportabilidad:** tipo de reclamo y moneda **estandarizados** (catálogo), montos **coherentes**, seguro **resuelto** a nombre legible.
- **O-4.** **Protección de datos sensibles de salud:** el **diagnóstico** accesible **solo** a roles autorizados, con auditoría reforzada (cumplimiento/privacidad).
- **O-5.** **Escalabilidad:** dejar la entidad lista como **fuente de verdad** para una eventual **integración con aseguradoras** (campos `Source*` ya presentes) sin reprocesar el modelo.

---

## 3. Alcance funcional (Fase 1 propuesta)

- **F1.** Endurecimiento del **registro de reclamos** existente (CRUD vía API REST).
- **F2.** **Dimensión paciente/beneficiario** (`ClaimantType` + `BeneficiaryPublicId?` + snapshot) — **núcleo** (RF-001).
- **F3.** **Validación y resolución del seguro** (referencia válida + nombre/póliza en la lectura) (RF-002).
- **F4.** **Catálogos**: `medical-claim-types` y **moneda ISO 4217** (RF-003/004).
- **F5.** **Validaciones** monetarias y de fechas (RF-005/006/009).
- **F6.** **Permiso dedicado** de salud + **enmascaramiento** del diagnóstico (RF-008).
- **F7.** **Módulo de reglas puro** + tests (RNF, G-10).
- **F8.** *(Opcional)* **Estado** del reclamo y **filtros/agregados** de lectura (RF-007/010).

---

## 4. Fuera de alcance

- **FA-1.** **Gestión/tramitación de reclamos** (envío a la aseguradora, aprobación, pago, conciliación contable). El módulo es **solo registro**.
- **FA-2.** **Workflow de autorización** del reclamo (estados con transiciones gobernadas, SLA, escalamiento).
- **FA-3.** **Integración en línea** con aseguradoras (sync automático) — **diferida** (RF-011; campos `Source*` listos).
- **FA-4.** **Autoservicio del empleado** para crear/editar sus reclamos (a confirmar — P-09).
- **FA-5.** **Gestión de pólizas y beneficiarios** (ya existe en `PersonnelFileInsurance`/`…Beneficiary`; aquí solo se **referencia**).
- **FA-6.** **Cálculos de cobertura/deducible/copago** y reportería financiera avanzada.
- **FA-7.** **Notificaciones** al empleado/RRHH sobre el estado del reclamo.
- **FA-8.** **Adjuntos documentales** salvo decisión explícita (P-11).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Analista / Gestor de RRHH (Beneficios)** | Registra/edita/consulta reclamos; **único** con escritura. Maneja datos de salud. |
| **Responsable de Beneficios / Bienestar** | Consulta reclamos y montos para seguimiento del beneficio (puede no requerir ver el diagnóstico — P-08). |
| **Empleado titular** | Sujeto de los reclamos propios. **Sin** autoservicio en esta fase (a confirmar — P-09). |
| **Familiar beneficiario** | Paciente de un reclamo no-propio; **no** es usuario del sistema (su dato vive como beneficiario de la póliza). |
| **Auditor / Cumplimiento / Privacidad** | Consulta la bitácora de accesos/cambios a datos de salud (RF-008). |
| **Sistema — aseguradora (diferido)** | Fuente eventual de reclamos vía `Source*` (RF-011). |

---

## 6. Requerimientos funcionales

### RF-001 — Identificar al paciente del reclamo (titular vs. beneficiario)
**Descripción:** Permitir indicar de **quién** es el reclamo: del **titular** o de un **familiar beneficiario**, y en este último caso **cuál**. Cubre el texto "propios o por familiares beneficiarios".
**Reglas de negocio:**
- Nuevo `ClaimantType` (catálogo `TITULAR` / `BENEFICIARIO`), **obligatorio**.
- Si `BENEFICIARIO`: `BeneficiaryPublicId` **referencia** a un `PersonnelFileInsuranceBeneficiary` **del seguro indicado** y **del mismo expediente**; se conserva **snapshot** `PatientName`/`KinshipCode` para historial.
- Si `TITULAR`: sin referencia a beneficiario.
- Coherencia: el beneficiario referenciado **debe** pertenecer al `InsurancePublicId` del reclamo (RF-002).
**Criterios de aceptación:**
- Dado `ClaimantType=BENEFICIARIO` sin `BeneficiaryPublicId`, cuando se guarda, entonces **422** `CLAIM_BENEFICIARY_REQUIRED`.
- Dado un `BeneficiaryPublicId` que no pertenece al seguro/expediente, entonces **422** `CLAIM_BENEFICIARY_NOT_OWNED`.
- Dado `ClaimantType=TITULAR`, cuando se consulta, entonces el paciente se muestra como el **empleado**.
**Prioridad:** Alta · **Dependencias:** `PersonnelFileInsurance`/`PersonnelFileInsuranceBeneficiary`; catálogo `claimant-types`.

### RF-002 — Validar y resolver el seguro médico
**Descripción:** Validar la referencia `InsurancePublicId` y **resolver el nombre/póliza** del seguro en la lectura (hoy el response solo devuelve un Guid).
**Reglas de negocio:**
- `InsurancePublicId`, si se informa, **debe** existir, ser del **mismo tenant** y **pertenecer al expediente** (hoy no se valida — G-02).
- El response **proyecta** `InsuranceName` (resuelto desde `InsuranceCode`/catálogo) y `PolicyNumber`.
- *(Pregunta P-03)* definir si el seguro es **obligatorio** o puede registrarse un reclamo sin póliza vinculada.
**Criterios de aceptación:**
- Dada una `InsurancePublicId` inexistente/de otro expediente, cuando se guarda, entonces **422** `CLAIM_INSURANCE_NOT_FOUND`/`CLAIM_INSURANCE_NOT_OWNED`.
- Dado un reclamo con seguro válido, cuando se consulta, entonces el response incluye **nombre del seguro** y **número de póliza**.
**Prioridad:** Alta · **Dependencias:** `IPersonnelFileEmployeeRepository`, `GeneralCatalogs`.

### RF-003 — Catalogar el tipo de reclamo
**Descripción:** Sustituir `ClaimTypeCode` de **texto libre** por **catálogo** `medical-claim-types` (país-scoped), validado como activo.
**Reglas de negocio:**
- `ClaimTypeCode` **debe** existir y estar **activo** en el catálogo del tenant/país.
- **Seed propuesto (SV, a confirmar — P-04):** `AMBULATORIO`, `HOSPITALARIO`, `EMERGENCIA`, `FARMACIA`, `LABORATORIO`, `DENTAL`, `OFTALMOLOGICO`, `MATERNIDAD`, `OTRO`.
**Criterios de aceptación:**
- Dado un código fuera de catálogo, entonces **422** `CLAIM_TYPE_CODE_INVALID`.
- Dado un código válido, entonces se acepta y persiste normalizado.
**Prioridad:** Media · **Dependencias:** `GeneralCatalogs` (`CountryScopedCatalogItem`).

### RF-004 — Catalogar la moneda (ISO 4217)
**Descripción:** Validar `CurrencyCode` contra **catálogo ISO 4217** (reusar el de compensación) y reducir la longitud (hoy ≤40).
**Reglas de negocio:**
- `CurrencyCode` **debe** ser ISO 4217 válido (3 letras).
- **Obligatoria** cuando exista `ClaimAmount` o `PaidAmount` (RF-005).
- *(Pregunta P-05)* default de moneda (p. ej. `USD` para SV) o tomarla del seguro.
**Criterios de aceptación:**
- Dado un código de moneda inválido, entonces **422** `CLAIM_CURRENCY_INVALID`.
- Dado un monto sin moneda, entonces **422** `CLAIM_CURRENCY_REQUIRED`.
**Prioridad:** Media · **Dependencias:** catálogo de monedas existente.

### RF-005 — Validaciones monetarias
**Descripción:** Reglas de consistencia para `ClaimAmount` y `PaidAmount`.
**Reglas de negocio:**
- Montos `≥ 0` (no negativos).
- `PaidAmount ≤ ClaimAmount` cuando ambos existan *(salvo política contraria — P-06)*.
- Si hay monto, hay moneda (RF-004).
**Criterios de aceptación:**
- Dado `PaidAmount > ClaimAmount`, entonces **422** `CLAIM_PAID_EXCEEDS_AMOUNT`.
- Dado un monto negativo, entonces **422** `CLAIM_AMOUNT_NEGATIVE`.
**Prioridad:** Media · **Dependencias:** módulo de reglas (G-10).

### RF-006 — Tiempo de respuesta validado / derivable
**Descripción:** Validar `ResponseTimeDays` y **ofrecer** derivarlo de fechas.
**Reglas de negocio:**
- `ResponseTimeDays ≥ 0` si se informa.
- *(Opción, P-07)* capturar `ResolutionDateUtc?`/`PaymentDateUtc?` y **derivar** `ResponseTimeDays = fecha_resolución − ClaimDateUtc`.
**Criterios de aceptación:**
- Dado `ResponseTimeDays < 0`, entonces **422** `CLAIM_RESPONSE_TIME_NEGATIVE`.
- Dada una fecha de resolución y la fecha del reclamo, cuando se consulta, entonces el tiempo de respuesta es **consistente** con ambas.
**Prioridad:** Baja · **Dependencias:** RF-009.

### RF-007 — (Opcional) Estado del reclamo
**Descripción:** Capturar un **estado** informativo del reclamo (atributo, **sin** workflow).
**Reglas de negocio:**
- Catálogo `medical-claim-status`: p. ej. `PRESENTADO`, `EN_TRAMITE`, `APROBADO`, `PAGADO`, `PARCIAL`, `RECHAZADO` (P-10).
- Es **descriptivo**; **no** gobierna transiciones ni dispara acciones (módulo es solo registro).
**Criterios de aceptación:**
- Dado un estado fuera de catálogo, entonces **422** `CLAIM_STATUS_INVALID`.
**Prioridad:** Baja · **Dependencias:** decisión P-10.

### RF-008 — Permiso dedicado y protección del diagnóstico (dato de salud)
**Descripción:** Proteger reclamos médicos con **permiso dedicado** (separado del Read/Manage genérico y del de compensación) y **enmascarar** el diagnóstico para quien no lo tenga.
**Reglas de negocio:**
- Lectura: `PersonnelFiles.ViewMedicalClaims`; escritura: `PersonnelFiles.ManageMedicalClaims` (patrón de `ViewCompensation`/`AuthorizeRehire`).
- Sin `ViewMedicalClaims` → **403** (o, según P-08, lectura **sin** `Diagnosis`, enmascarado).
- Auditar **accesos de lectura** a `Diagnosis`, no solo escrituras (cumplimiento).
**Criterios de aceptación:**
- Dado un usuario con `Read` genérico pero **sin** `ViewMedicalClaims`, cuando consulta reclamos, entonces **403** (o diagnóstico **oculto** — P-08).
- Dada una lectura de un reclamo con diagnóstico, cuando ocurre, entonces queda **registrada** en auditoría.
**Prioridad:** Alta · **Dependencias:** Provisioning/`IdentityAccess` (alta + seed del permiso); `IAuditService`.

### RF-009 — Validación de la fecha del reclamo
**Descripción:** Asegurar coherencia de `ClaimDateUtc`.
**Reglas de negocio:**
- **Requerida** y **no futura**.
- *(Opción)* dentro de la vigencia del seguro (`StartDateUtc`/`EndDateUtc`) si hay póliza vinculada.
**Criterios de aceptación:**
- Dado `ClaimDateUtc` ausente/`0001-01-01`/futura, entonces **422** `CLAIM_DATE_INVALID`.
**Prioridad:** Baja · **Dependencias:** RF-002.

### RF-010 — (Opcional) Consulta enriquecida
**Descripción:** Filtros y agregados de **solo lectura** sobre los reclamos del expediente.
**Reglas de negocio:**
- Filtros por **rango de fechas**, **tipo**, **beneficiario/titular**, **estado**.
- Agregados: **total reclamado/pagado** por año y por paciente (titular/beneficiario).
**Criterios de aceptación:**
- Dado un rango y un beneficiario, cuando consulto, entonces obtengo solo sus reclamos y el **total pagado**.
**Prioridad:** Baja · **Dependencias:** RF-001.

### RF-011 — (Diferido) Punto de extensión para integración con aseguradora
**Descripción:** Mantener `SourceSystem`/`SourceReference`/`SourceSyncedUtc` como **contrato** para una futura sincronización automática de reclamos.
**Reglas de negocio:** Un reclamo **sincronizado** (con `SourceReference`) **no** se duplica en altas manuales; *upsert* por `(SourceSystem, SourceReference)`.
**Criterios de aceptación:**
- Dado un reclamo con `SourceReference` ya existente, cuando llega de nuevo, entonces se **actualiza** (no se duplica).
**Prioridad:** Diferida · **Dependencias:** futuro conector de aseguradora.

---

## 7. Requerimientos no funcionales

- **Privacidad / datos de salud.** `Diagnosis` es **categoría especial** (salud). Acceso por **permiso dedicado**, **enmascaramiento** y **auditoría de lectura** (RF-008). Minimización: exponer el diagnóstico **solo** donde sea necesario.
- **Seguridad / Multi-tenant.** Toda operación filtrada por **tenant**; las referencias (seguro, beneficiario) **sin fuga cross-tenant** (RF-001/002).
- **Integridad / Consistencia.** Reglas de paciente, montos, moneda y fechas en un **módulo de reglas puro** `MedicalClaims.Rules.cs`, **unit-testeable** (patrón de `EmploymentAssignments.Rules.cs`) — G-10.
- **Concurrencia.** `ConcurrencyToken` + `If-Match`/`ETag` (ya implementado).
- **Auditoría.** Diff de escrituras (ya existe) **+** registro de **accesos de lectura** al diagnóstico (RF-008).
- **Rendimiento.** Índice `(tenant, expediente, fecha, tipo)` existente; añadir filtros indexados si se implementa RF-010.
- **Usabilidad.** Selector de **beneficiario** del seguro (no GUID); **nombre del seguro** legible (no Guid); montos con moneda visible.
- **Compatibilidad / API.** Conservar los 6 endpoints; los cambios de validación son retro-compatibles salvo para **datos inválidos**; añadir campos al response es aditivo.
- **Localización.** Mensajes/errores **bilingües (ES/EN)** con código estable (test de paridad — convención de la casa).
- **Mantenibilidad.** Reglas con tests unitarios y de **paridad de localización**.

---

## 8. Historias de usuario

### HU-001 — Registrar un reclamo de un familiar beneficiario
Como **Analista de RRHH (Beneficios)**, quiero **indicar que un reclamo es de un beneficiario y de cuál**, para **diferenciarlo de los reclamos del titular**.
- Dado un seguro con beneficiarios, cuando elijo `BENEFICIARIO` y selecciono uno, entonces se guarda vinculado y auditado (RF-001).
- Dado `BENEFICIARIO` sin seleccionar beneficiario, entonces **bloquea** (RF-001).

### HU-002 — Ver el nombre del seguro, no un identificador
Como **RRHH/consulta**, quiero **ver el nombre del seguro y la póliza** en la lista de reclamos, para **entender el contexto sin buscar el Guid** (RF-002).
- Dado un reclamo con seguro válido, cuando consulto, entonces veo **nombre** y **número de póliza**.

### HU-003 — Estandarizar tipo de reclamo y moneda
Como **RRHH**, quiero **elegir el tipo de reclamo y la moneda de una lista**, para **mantener datos consistentes y reportables** (RF-003/004).
- Dado el catálogo, cuando elijo "HOSPITALARIO"/"USD", entonces se acepta; texto fuera de catálogo **bloquea**.

### HU-004 — Cuidar los datos de salud
Como **Oficial de Cumplimiento/Privacidad**, quiero que **el diagnóstico solo lo vean roles autorizados** y que **los accesos queden registrados**, para **cumplir la protección de datos sensibles** (RF-008).
- Dado un usuario sin `ViewMedicalClaims`, cuando consulta, entonces **no** ve el diagnóstico (oculto o 403).
- Dada una lectura del diagnóstico, cuando ocurre, entonces queda **auditada**.

### HU-005 — Montos coherentes
Como **RRHH**, quiero que **el monto pagado no supere el reclamado** y **no haya negativos**, para **evitar registros erróneos** (RF-005).
- Dado `PaidAmount > ClaimAmount`, entonces **bloquea**.

### HU-006 — Consultar reclamos por persona
Como **RRHH/Beneficios**, quiero **filtrar los reclamos por titular o beneficiario y ver el total pagado**, para **dar seguimiento al uso del beneficio** (RF-010).
- Dado un beneficiario, cuando filtro, entonces obtengo sus reclamos y el **total pagado**.

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** (`IsCompletedEmployee`) pueden tener reclamos (ya implementado — `MedicalClaims.Handlers.cs:46`).
- **RN-02.** El reclamo pertenece **siempre** a un expediente (titular); el **paciente** puede ser el titular o un **beneficiario** de su seguro (RF-001).
- **RN-03.** Si `ClaimantType=BENEFICIARIO`, el beneficiario **debe** existir y pertenecer al **seguro** y **expediente** del reclamo (RF-001/002).
- **RN-04.** `InsurancePublicId`, si se informa, **debe** existir, ser del **mismo tenant** y **pertenecer al expediente** (RF-002).
- **RN-05.** `ClaimTypeCode` **debe** pertenecer al catálogo `medical-claim-types` activo (RF-003).
- **RN-06.** `CurrencyCode` **debe** ser ISO 4217 y es **obligatorio si hay monto** (RF-004/005).
- **RN-07.** Montos **no negativos**; `PaidAmount ≤ ClaimAmount` (salvo P-06) (RF-005).
- **RN-08.** `ResponseTimeDays ≥ 0`; si hay fechas de resolución/pago, debe ser **consistente** (RF-006).
- **RN-09.** `ClaimDateUtc` **requerida** y **no futura** (RF-009).
- **RN-10.** El **diagnóstico** es **dato de salud**: acceso por **permiso dedicado**, enmascaramiento y **auditoría de lectura** (RF-008).
- **RN-11.** Gestión **solo RRHH/Beneficios** con `ManageMedicalClaims`; **autoservicio del empleado** a confirmar (P-09).
- **RN-12.** El módulo es **solo registro**: **no** aprueba, **no** paga, **no** tramita reclamos (FA-1/2).
- **RN-13.** Toda escritura exige **If-Match** y queda **auditada**; soft-delete vía `IsActive` (ya implementado).

---

## 10. Flujos principales

**Flujo: Registrar un reclamo (RRHH/Beneficios)**
1. RRHH (con `ManageMedicalClaims`) abre el expediente del **titular** (debe estar **completado**).
2. Entra a **"Reclamos de Seguro Médico"** → **Agregar**.
3. Selecciona el **seguro** del empleado → el sistema lo **valida** y muestra su **nombre/póliza** (RF-002).
4. Indica el **paciente**: `TITULAR` o `BENEFICIARIO`; si beneficiario, **lo elige** de la póliza (RF-001).
5. Selecciona **tipo** (catálogo, RF-003), ingresa **diagnóstico**, **montos** + **moneda** (catálogo, RF-004/005), **tiempo de respuesta** y **observaciones**; **fecha del reclamo** (RF-009).
6. El sistema **valida** montos/moneda/fechas/paciente y **persiste**; **audita** y devuelve `ETag`.

**Flujo: Consultar reclamos (RRHH/Beneficios/Auditoría)**
1. Abre la lista de reclamos del expediente (RF-010); ve **seguro, paciente, tipo, montos, estado**.
2. Si tiene `ViewMedicalClaims`, ve el **diagnóstico**; si no, queda **oculto** (RF-008). El acceso al diagnóstico se **audita**.

**Flujo: (Diferido) Sincronización con aseguradora**
1. El conector trae reclamos con `SourceReference`; el sistema hace **upsert** por `(SourceSystem, SourceReference)` (RF-011).

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Titular **no completado**. | **Bloqueo** `STATE_RULE_VIOLATION` (ya implementado — `MedicalClaims.Handlers.cs:46`). |
| **E2** | `ClaimantType=BENEFICIARIO` sin beneficiario. | **Bloqueo** `422 CLAIM_BENEFICIARY_REQUIRED` (RF-001). |
| **E3** | Beneficiario que **no** pertenece al seguro/expediente. | **Bloqueo** `422 CLAIM_BENEFICIARY_NOT_OWNED` (RF-001). |
| **E4** | `InsurancePublicId` inexistente / de otro expediente. | **Bloqueo** `422 CLAIM_INSURANCE_NOT_FOUND` / `…NOT_OWNED` (RF-002). |
| **E5** | `ClaimTypeCode` fuera de catálogo. | **Bloqueo** `422 CLAIM_TYPE_CODE_INVALID` (RF-003). |
| **E6** | Moneda inválida / monto sin moneda. | **Bloqueo** `422 CLAIM_CURRENCY_INVALID` / `CLAIM_CURRENCY_REQUIRED` (RF-004). |
| **E7** | `PaidAmount > ClaimAmount` o monto negativo. | **Bloqueo** `422 CLAIM_PAID_EXCEEDS_AMOUNT` / `CLAIM_AMOUNT_NEGATIVE` (RF-005). |
| **E8** | `ResponseTimeDays < 0`. | **Bloqueo** `422 CLAIM_RESPONSE_TIME_NEGATIVE` (RF-006). |
| **E9** | `ClaimDateUtc` ausente/`0001-01-01`/futura. | **Bloqueo** `422 CLAIM_DATE_INVALID` (RF-009). |
| **E10** | Usuario **sin** `ViewMedicalClaims` consulta. | **403** o diagnóstico **enmascarado** (según P-08, RF-008). |
| **E11** | Usuario **sin** `ManageMedicalClaims` intenta escribir. | **403 FORBIDDEN** (RF-008). |
| **E12** | `If-Match`/`ConcurrencyToken` no coincide. | **409 CONFLICT** (ya implementado — `MedicalClaims.Handlers.cs:128,213,318`). |
| **E13** | Reclamo sincronizado con `SourceReference` duplicado. | **Upsert** (no duplica) — diferido (RF-011). |

---

## 12. Datos requeridos

### Entidad: `PersonnelFileMedicalClaim` *(ya existe — `Domain/PersonnelFiles/PersonnelFileEmployee.cs:957`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `personnelFileId` | long (FK) | Sí | del tenant | ✅ existe | **Titular** (dueño del reclamo) |
| `insurancePublicId` | GUID | No | **existe + tenant + del expediente**; resolver **nombre/póliza** | 🔧 endurecer (RF-002) | Seguro asociado |
| `claimantType` | Texto → **catálogo** | **Sí** | `TITULAR`/`BENEFICIARIO` | 🆕 **nuevo** (RF-001) | **Paciente: titular o beneficiario** |
| `beneficiaryPublicId` | GUID (ref) | Condicional | requerido si `BENEFICIARIO`; del seguro/expediente | 🆕 **nuevo** (RF-001) | Beneficiario paciente |
| `patientNameSnapshot` | Texto | No | — | 🆕 opcional (RF-001) | *Snapshot* del nombre del paciente |
| `kinshipCodeSnapshot` | Texto | No | — | 🆕 opcional (RF-001) | *Snapshot* del parentesco |
| `accountNumber` | Texto (≤120) | No | — | ✅ existe | Número de cuenta |
| `claimTypeCode` | Texto → **catálogo** | Sí | catálogo `medical-claim-types` | 🔧 a catálogo (RF-003) | Tipo de reclamo |
| `diagnosis` | Texto (≤1000) | No | **dato de salud**; permiso/enmascaramiento | 🔒 proteger (RF-008) | Diagnóstico |
| `claimAmount` | Decimal(18,2) | No | `≥ 0`; moneda si hay monto | 🔧 validar (RF-005) | Monto del reclamo |
| `currencyCode` | Texto → **catálogo ISO** | Condicional | ISO 4217; obligatoria si hay monto | 🔧 a catálogo (RF-004) | Moneda |
| `paidAmount` | Decimal(18,2) | No | `≥ 0`; `≤ claimAmount` | 🔧 validar (RF-005) | Monto pagado |
| `responseTimeDays` | Número | No | `≥ 0`; derivable de fechas | 🔧 validar (RF-006) | Tiempo de respuesta |
| `resolutionDateUtc` | Fecha | No | `≥ claimDate` | 🆕 opcional (RF-006) | Fecha de resolución/pago |
| `claimStatusCode` | Texto → **catálogo** | No | catálogo `medical-claim-status` | 🆕 opcional (RF-007) | Estado informativo |
| `notes` | Texto (≤2000) | No | — | ✅ existe | Observaciones |
| `claimDateUtc` | Fecha | Sí | requerida; **no futura** | 🔧 validar (RF-009) | Fecha del reclamo |
| `sourceSystem` / `sourceReference` / `sourceSyncedUtc` | Texto/Texto/Fecha | No | upsert por `(system, reference)` | ✅ existe (diferido) | Integración aseguradora |
| `isActive` | Booleano | Sí | soft-delete (PATCH) | ✅ existe | Estado activo |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

### Entidad: `MedicalClaimTypeCatalogItem` *(nueva — RF-003)*

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `publicId` | GUID | Sí | único | Identidad |
| `countryCode` | Texto | Sí | catálogo de países | País (patrón `CountryScopedCatalogItem`) |
| `code` | Texto | Sí | único por país | AMBULATORIO, HOSPITALARIO, EMERGENCIA, FARMACIA, … |
| `name` | Texto | Sí | — | Nombre visible (ES/EN) |
| `isActive` | Booleano | Sí | — | Estado |
| `sortOrder` | Número | No | — | Orden de despliegue |

> **Catálogos adicionales (según decisión):** `claimant-types` (TITULAR/BENEFICIARIO — RF-001), `medical-claim-status` (RF-007) y reuso del **catálogo de monedas ISO 4217** (RF-004).

> **Entidades reutilizadas (ya existen):** `PersonnelFileInsurance` (`PersonnelFileEmployee.cs:774`) y `PersonnelFileInsuranceBeneficiary` (`:891` — `FullName`, `DocumentNumber`, `BirthDate`, `KinshipCode`). El reclamo **referencia**, no duplica.

---

## 13. Integraciones necesarias

- **Catálogos generales (`GeneralCatalogs`).** Nuevos `medical-claim-types`, `claimant-types`, `medical-claim-status` (país-scoped) y reuso de **monedas ISO 4217**. Interno.
- **Seguros/Beneficiarios (`PersonnelFileInsurance`/`…Beneficiary`).** Para validar el seguro, **resolver su nombre** y **listar beneficiarios** (RF-001/002).
- **Identidad/Provisioning (`IdentityAccess`).** Alta y seed de permisos `PersonnelFiles.ViewMedicalClaims` / `ManageMedicalClaims` (RF-008).
- **Auditoría (`IAuditService`).** Diff de escrituras (existe) + **registro de accesos de lectura** al diagnóstico (RF-008).
- **Aseguradora (diferido).** Conector que alimente `Source*` (RF-011). **No** en esta fase.
- **Notificaciones:** **no aplica** en esta fase.

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **RRHH / Beneficios (gestor)** | Crear/editar/activar/eliminar y leer reclamos, **incluido diagnóstico** (**`ManageMedicalClaims`** + `ViewMedicalClaims`). | Solo expedientes **completados**; solo su **tenant**. |
| **Consulta / Beneficios (sin salud)** | Leer reclamos **sin diagnóstico** (solo `PersonnelFiles.Read`). | Diagnóstico **enmascarado** (P-08). |
| **Auditor / Cumplimiento** | Leer reclamos + **bitácora de accesos** al diagnóstico (`ViewMedicalClaims` o rol de auditoría). | Sin escritura. |
| **Empleado titular** | — | **Sin** autoservicio en esta fase (P-09). |
| **Sistema (aseguradora, diferido)** | Upsert de reclamos sincronizados (RF-011). | Solo el contrato `Source*`. |

> **Permisos nuevos (RF-008):** `PersonnelFiles.ViewMedicalClaims` (lectura del diagnóstico) y `PersonnelFiles.ManageMedicalClaims` (escritura), siguiendo el patrón de `PersonnelFiles.ViewCompensation` (`ProvisioningConstants.cs:71`). **Hoy** los reclamos usan el `Read`/`Manage` **genérico** (`PersonnelFileCompensationController.cs:25-27`), por lo que el diagnóstico queda **subprotegido**.

---

## 15. Criterios de aceptación generales

- ✅ Se puede registrar y consultar si el reclamo es del **titular** o de un **beneficiario** identificado (RF-001).
- ✅ La consulta muestra el **nombre del seguro** y la **póliza**, no un Guid (RF-002).
- ✅ **Tipo de reclamo** y **moneda** provienen de **catálogo** (RF-003/004).
- ✅ Los **montos** son coherentes (no negativos, pagado ≤ reclamado) y la **fecha** es válida (RF-005/009).
- ✅ El **diagnóstico** solo es visible con **permiso dedicado**; su acceso queda **auditado** (RF-008).
- ✅ La escritura exige `ManageMedicalClaims`; es **concurrencia-segura (If-Match)** y **auditada**.
- ✅ Las reglas viven en un **módulo puro** con **tests unitarios** (G-10).
- ✅ Mensajes/errores **bilingües (ES/EN)** con código estable.
- ✅ El módulo permanece **solo de registro** (sin tramitación/aprobación/pago).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1. Datos de salud subprotegidos (G-08).** Hoy el diagnóstico es accesible con `Read` genérico. Riesgo de **cumplimiento/privacidad**. Mitigación: RF-008 (permiso dedicado + enmascaramiento + auditoría de lectura). **Prioridad alta.**
- **R2. Requisito incumplido (G-01).** Sin campo de paciente/beneficiario, el sistema **no** satisface "propios o por familiares beneficiarios". Mitigación: RF-001.
- **R3. Datos sucios existentes.** Tipos/moneda en texto libre, referencias de seguro inválidas; el endurecimiento (RF-002/003/004) es *breaking* para esos datos → limpieza/migración.
- **R4. Calidad del beneficiario.** Si las pólizas no tienen beneficiarios cargados, RF-001 solo permitiría `TITULAR` hasta poblarlos (S2).
- **R5. Ubicación semántica.** Estar bajo "Compensation" puede confundir gobierno de permisos; el permiso dedicado (RF-008) lo mitiga sin mover el código.

### Supuestos
- **S1.** Verificar si hay datos productivos/QA de reclamos; sin datos → **drop & recreate**/normalización directa; con datos → **migración** previa (como en otros módulos del expediente).
- **S2.** Las pólizas (`PersonnelFileInsurance`) y sus **beneficiarios** ya se gestionan en el expediente; RF-001 los **referencia**.
- **S3.** País de referencia **SV**; catálogos sembrados para SV; moneda usual **USD**.
- **S4.** El módulo es **solo registro** (confirmado por el requisito): sin workflow ni pagos.
- **S5.** Existe catálogo de **monedas ISO 4217** reutilizable (usado por compensación).

### Dependencias
- **D1.** `GeneralCatalogs` (`medical-claim-types`, `claimant-types`, `medical-claim-status`, monedas).
- **D2.** `PersonnelFileInsurance` / `PersonnelFileInsuranceBeneficiary` + repositorio del expediente.
- **D3.** `IdentityAccess`/Provisioning (permisos `ViewMedicalClaims` / `ManageMedicalClaims`).
- **D4.** `IAuditService` (auditoría de lectura del diagnóstico).
- **D5.** (Diferido) Conector de **aseguradora** (RF-011).

---

## 17. Preguntas abiertas para el cliente o stakeholders

> El requisito pide **validar** y, **si hace falta, agregar**. Las siguientes decisiones determinan el alcance de la Fase 1. **No** se asumen como resueltas.

| # | Pregunta | Por qué importa |
|---|---|---|
| **P-01** | ¿Confirmamos que es **solo registro** (sin tramitar/aprobar/pagar)? | Fija el fuera-de-alcance (FA-1/2). |
| **P-02** | ¿El reclamo debe distinguir **titular vs. beneficiario** y **referenciar** al beneficiario? *(El texto del requisito sugiere que sí.)* | Habilita RF-001 (brecha principal G-01). |
| **P-03** | ¿El **seguro** es **obligatorio** en el reclamo o puede registrarse sin póliza? | Define RF-002 y la validación referencial. |
| **P-04** | ¿Catálogo de **tipos de reclamo**? ¿Qué valores (SV)? | RF-003 (seed). |
| **P-05** | ¿**Moneda** por defecto o tomada del seguro? ¿Multimoneda? | RF-004. |
| **P-06** | ¿Puede el **monto pagado superar** el reclamado (reembolsos/ajustes)? | RF-005 (regla `PaidAmount ≤ ClaimAmount`). |
| **P-07** | ¿Capturar **fecha de resolución/pago** y **derivar** el tiempo de respuesta? | RF-006. |
| **P-08** | Para quien **no** tenga permiso de salud, ¿**403** o **enmascarar** el diagnóstico? | RF-008 (modo de protección). |
| **P-09** | ¿**Autoservicio** del empleado (ver/registrar sus reclamos) o **solo RRHH**? | Roles/permisos (RF-008, RN-11). |
| **P-10** | ¿Se requiere **estado** del reclamo (informativo)? ¿Qué valores? | RF-007. |
| **P-11** | ¿Se requieren **adjuntos** (formulario, factura, EOB)? | G-12 (posible fase posterior). |
| **P-12** | ¿Se exige **auditoría de accesos de lectura** al diagnóstico (no solo cambios)? | RF-008 (cumplimiento). |
| **P-13** | ¿Habrá **integración con aseguradora** a futuro? ¿Qué sistema(s)? | RF-011 (`Source*`). |

---

## 18. Recomendaciones del Analista de Negocio

1. **No cerrar "sin cambios".** El desarrollo está **bien encaminado** (8/9 campos + infra sólida), pero **(a)** falta el **paciente/beneficiario** exigido por el requisito y **(b)** el **diagnóstico** (dato de salud) está **subprotegido**. Ambos son **imprescindibles** para considerar el requisito cumplido.

2. **Reposicionar como "validación + endurecimiento", no construcción nueva.** La entidad, CRUD, API, auditoría, concurrencia y soft-delete **ya existen**. El esfuerzo es de **un campo nuevo (paciente)**, **catálogos**, **validaciones**, **resolución de nombre** y **un permiso dedicado**.

3. **MVP Fase 1 (recomendado) — lo imprescindible:**
   - **RF-001** (paciente: titular/beneficiario) — *núcleo del requisito*.
   - **RF-002** (validar y **resolver el nombre** del seguro).
   - **RF-008** (permiso dedicado de salud + **enmascaramiento** del diagnóstico + auditoría de lectura).
   - **RF-005** (validaciones monetarias) y **RF-009** (fecha válida) — bajo costo, alto valor.
   - Extraer **`MedicalClaims.Rules.cs`** (módulo de reglas puro, con tests — G-10).

4. **Fase 2 (deseable):** **RF-003/004** (catálogos de tipo y moneda ISO) y **RF-006** (fecha de resolución → tiempo derivado). Requieren **migración** de datos en texto libre.

5. **Fase 3 (opcional/diferida):** **RF-007** (estado informativo), **RF-010** (filtros/agregados) y **RF-011** (integración con aseguradora, ya preparada con `Source*`).

6. **Datos y migración (S1):** verificar datos existentes. Sin datos → **drop & recreate**/normalización directa; con datos → **migrar** (tipos→catálogo, moneda→ISO, depurar referencias de seguro, poblar `claimantType=TITULAR` por defecto) antes de activar validaciones duras.

7. **Privacidad por diseño (R1):** tratar el **diagnóstico** como dato de categoría especial desde ya: permiso dedicado, **minimización** (no exponerlo donde no se necesite), **auditoría de lectura** y revisión con **Cumplimiento** (alineado a la normativa de protección de datos aplicable en SV y buenas prácticas de PHI).

8. **Naming/semántica (R5):** considerar exponer el módulo como **"Reclamos de Seguro Médico"** con su **propio permiso**, aunque el código resida bajo `Compensation`; evita confundir el gobierno de accesos entre **salario** y **salud**.

---

> **Naturaleza del documento.** Análisis de **validación + brechas (GAP)** sobre funcionalidad **ya implementada** (`PersonnelFileMedicalClaim`). El "estado as-is" está **verificado contra el código** (referencias `archivo:línea`). Las **decisiones del negocio (P-01…P-13) están abiertas**; al ratificarse, este documento pasa a "cerrado para Fase 1" y habilita el **plan técnico**. **No** se inventaron decisiones: lo no confirmado quedó como **pregunta abierta** o **supuesto**.
