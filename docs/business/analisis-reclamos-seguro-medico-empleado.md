# Análisis de Negocio — Reclamos de Seguro Médico (expediente del empleado)

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo, Cumplimiento/Privacidad |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Seguros y Beneficiarios (`PersonnelFileInsurance`, `PersonnelFileInsuranceBeneficiary`) · Reclamos médicos (`PersonnelFileMedicalClaim`) · Documentos/Archivos (`StoredFile`/`IFileStorageProvider`/`PersonnelFileDocument`) · Catálogos generales (`GeneralCatalogs`) · Preferencias de compañía (`CompanyPreference`) · Identidad/Permisos (`IdentityAccess`/Provisioning) |
| **Estado** | **Definido / Cerrado (Fase 1 — registro documental).** Decisiones ratificadas **D-01…D-13** (respuestas del negocio del 2026-06-22). Listo para diseño técnico de Fase 1. |
| **Versión** | v2 (incorpora decisiones del negocio P-01…P-13 → D-01…D-13) |
| **Fecha** | 2026-06-22 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |
| **Naturaleza del módulo** | **Solo de registro** (informativo/consulta). **No** es un motor de gestión/aprobación/pago de reclamos (D-01). |

---

## Contexto del cambio

En el **expediente del empleado** se requiere una sección para **consultar los reclamos de seguro médico** realizados por el empleado, **propios o de sus familiares beneficiarios**. La información a mostrar es: **nombre del seguro médico, número de cuenta, tipo de reclamo, diagnóstico, monto del reclamo, moneda, monto pagado, tiempo de respuesta y observaciones**. El módulo es **solo de registro**.

El objetivo declarado fue **doble**: (1) **validar** que lo ya implementado esté **bien alineado** con el requerimiento y con las buenas prácticas HRIS, y (2) **analizar y agregar** la información/reglas necesarias.

> **Hallazgo clave (confirmado en código).** Esto **no es un desarrollo desde cero**. CLARIHR **ya tiene implementada** la funcionalidad como entidad **`PersonnelFileMedicalClaim`**, con **CRUD completo** (Domain + Application/CQRS + API REST + auditoría + concurrencia + soft-delete + JSON Patch). De los **9 campos** del requerimiento, **8 están cubiertos** (uno —"nombre del seguro"— se almacena como **referencia** y no se **resuelve** a nombre en la lectura). El trabajo de Fase 1 es de **endurecimiento y alineación**: agregar la **dimensión paciente/beneficiario** (núcleo del requisito — D-02), hacer el **seguro obligatorio** y validado (D-03), **catalogar** tipo de reclamo (D-04) y **moneda ISO por país** (D-05), **validar** montos/fechas, **capturar fecha de resolución** y derivar el tiempo de respuesta (D-07), agregar **estado informativo** (D-10), **adjuntos** reutilizando el subsistema de documentos (D-11), habilitar **autoservicio del empleado** (D-09) y proteger el **diagnóstico** con un **permiso dedicado** que devuelve **403** (D-08).

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) | Decisión que aplica |
|---|---|---|---|
| 1 | **Entidad** | `PersonnelFileMedicalClaim` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:957`). CRUD completo Add/Update/Patch/Delete/Get/GetList (`Application/Features/PersonnelFiles/Compensation/MedicalClaims.cs` + `.Handlers.cs`). | Base correcta; se endurece. |
| 2 | **"Nombre del seguro"** | Se guarda **`InsurancePublicId`** (`Guid?`, ref. a `PersonnelFileInsurance`, **sin FK**, nullable, **sin validar** — `PersonnelFileEmployee.cs:999`). El response devuelve `InsuranceId` (Guid), **no resuelve el nombre** (`MedicalClaims.cs:20-22`). | Seguro **obligatorio** + validado + nombre resuelto (D-03, RF-002). |
| 3 | **Paciente / beneficiario** | **No existe** campo de paciente (**titular vs. beneficiario**) ni referencia a beneficiario. Las pólizas **sí** tienen beneficiarios (`PersonnelFileInsuranceBeneficiary` — `FullName`, `DocumentNumber`, `BirthDate`, `KinshipCode`, `PersonnelFileEmployee.cs:891`). | **Agregar** `ClaimantType` + `BeneficiaryPublicId` (D-02, RF-001). |
| 4 | **Tipo de reclamo** | `ClaimTypeCode`: **texto libre** `NotEmpty().MaximumLength(80)` (`MedicalClaims.cs:94-100`); **sin catálogo**. | Catálogo SV `medical-claim-types` (D-04, RF-003). |
| 5 | **Moneda** | `CurrencyCode`: **texto libre** `max 40` (`…Configuration.cs:403`); **sin** ISO ni default. | ISO 3 + default por país (D-05, RF-004). |
| 6 | **Montos** | `ClaimAmount`/`PaidAmount`: `numeric(18,2)` nullable (`…Configuration.cs:402,404`); **sin validación**. | No-negativos; pagado>reclamado **permitido** (reembolso, D-06, RF-005). |
| 7 | **Tiempo de respuesta** | `ResponseTimeDays`: `int?` **manual** (`PersonnelFileEmployee.cs:1013`); **sin** fecha de resolución. | Capturar fecha resolución/pago + **derivar** (D-07, RF-006). |
| 8 | **Estado del reclamo** | **No existe** estado. | Catálogo `medical-claim-status` (estándar HRIS) (D-10, RF-007). |
| 9 | **Fecha del reclamo** | `ClaimDateUtc` requerida por tipo, normalizada (`PersonnelFileEmployee.cs:989`); **pero** el validador **no** la valida (podría quedar `0001-01-01`/futura). | Validar requerida + no futura (RF-009). |
| 10 | **Permisos** | Controller `PersonnelFileCompensationController` con `[Authorize]` + `[AuthorizationPolicySet(Read, Manage)]` **genérico** (`…Controller.cs:25-27`); handlers usan `LoadForManageAsync` (**Manage**) y `LoadCompletedEmployeeForReadAsync` (**Read**). **No** usan `ViewCompensation` (que sí protege salario — `PersonnelFileCompensationConceptsController.cs:20`). | **Permiso dedicado** + **403** (D-08, RF-008). |
| 11 | **Dato sensible (salud)** | `Diagnosis` (categoría especial) **sin permiso dedicado**; auditado igual que el resto (`PersonnelFileEmployeeAudits.LogUpdateAsync`). | Permiso dedicado; auditar **solo cambios** (D-08, D-12, RF-008). |
| 12 | **Autoservicio** | Existe patrón **self-service de lectura** (D-16) para compensación: empleado lee **su propio** expediente (`PersonnelFileEmployeeHandlerBases.cs:245`; `IPersonnelFileAuthorizationService.cs:23`). | Extender a **ver + registrar** sus reclamos (D-09, RF-008). |
| 13 | **Adjuntos** | Existe subsistema de documentos **reutilizable**: `IFileStorageProvider` (Azure Blob), `StoredFile`, `FilePurpose`, flujo *upload-session → complete*, SAS URLs, limpieza en background, y `PersonnelFileDocument` (typed + catálogo). | **Reutilizar** para adjuntos del reclamo (D-11, RF-012). |
| 14 | **Integración aseguradora** | `SourceSystem`/`SourceReference`/`SourceSyncedUtc` existen como punto de extensión; **no** hay integración. | **No considerar** por ahora (D-13). |
| 15 | **Infra transversal** | `ConcurrencyToken` + `If-Match`/`ETag`; **solo expedientes completados** (`IsCompletedEmployee`, `MedicalClaims.Handlers.cs:46`); soft-delete `IsActive`; multi-tenant. | Se conserva. |

---

## Decisiones del negocio (ratificadas — 2026-06-22)

| # | Pregunta | Decisión |
|---|---|---|
| **D-01** | ¿Solo registro? | **Sí, solo registro.** **No** tramita, **no** aprueba, **no** paga reclamos. |
| **D-02** | ¿Distinguir titular vs. beneficiario y referenciarlo? | **Sí.** El reclamo indica el **paciente** (`TITULAR`/`BENEFICIARIO`) y, si es beneficiario, **referencia** al beneficiario de la póliza. |
| **D-03** | ¿Seguro obligatorio? | **Obligatorio.** Todo reclamo se asocia a un **seguro/póliza** válido del expediente. |
| **D-04** | ¿Catálogo de tipos de reclamo? | **Sí**, catálogo **de El Salvador** (`medical-claim-types`, país-scoped). |
| **D-05** | ¿Moneda? | **ISO 4217 (3 letras) según el país** (default desde la preferencia de compañía); editable a otra moneda ISO. |
| **D-06** | ¿Pagado puede superar al reclamado? | **Sí (modelo de reembolso).** No se **bloquea**; se permite (con aviso suave). Montos **no negativos**. |
| **D-07** | ¿Fecha de resolución/pago + derivar tiempo de respuesta? | **Sí.** Se captura `ResolutionDateUtc` y el **tiempo de respuesta se deriva** (`resolución − reclamo`). |
| **D-08** | Sin permiso de salud, ¿403 o enmascarar? | **403.** Acceso al reclamo **denegado** (no enmascaramiento). |
| **D-09** | ¿Autoservicio del empleado? | **Sí: empleado (autoservicio) y RRHH.** El empleado puede **ver y registrar** sus reclamos (propios y de sus beneficiarios); RRHH gestiona todo. |
| **D-10** | ¿Estado del reclamo? | **Sí**, catálogo con los **estados estándar de la industria HRIS** (`medical-claim-status`). Informativo (sin workflow). |
| **D-11** | ¿Adjuntos? | **Sí.** Agregar adjuntos **reutilizando los servicios de documentos ya desarrollados** (`IFileStorageProvider`/`StoredFile`/`FilePurpose`). |
| **D-12** | ¿Auditoría de lectura del diagnóstico? | **No**, **solo cambios** (escrituras). Se mantiene el diff de auditoría existente. |
| **D-13** | ¿Integración con aseguradora? | **Aún no está claro → no considerar por ahora.** Los campos `Source*` quedan como punto de extensión inerte. |

---

## Brechas verificadas y su resolución (GAP → Decisión)

| # | Brecha (as-is, verificada) | Resolución (to-be) |
|---|---|---|
| **G-01** | Sin campo de **paciente/beneficiario**; incumple "propios o por familiares". | `ClaimantType` + `BeneficiaryPublicId` validado contra la póliza (D-02, RF-001). |
| **G-02** | `InsurancePublicId` **sin FK ni validación**; read **no resuelve** el nombre. | Seguro **obligatorio**, validado y **nombre resuelto** en el read (D-03, RF-002). |
| **G-03** | `ClaimTypeCode` texto libre. | Catálogo SV `medical-claim-types` (D-04, RF-003). |
| **G-04** | `CurrencyCode` texto libre (≤40). | **ISO 4217 (3)**; default por país desde `CompanyPreference` (D-05, RF-004). |
| **G-05** | Montos sin validar. | No-negativos; **pagado > reclamado permitido** (reembolso) con aviso (D-06, RF-005). |
| **G-06** | `ResponseTimeDays` manual; sin fecha de resolución. | Capturar `ResolutionDateUtc` y **derivar** el tiempo (D-07, RF-006). |
| **G-07** | Sin estado del reclamo. | Catálogo `medical-claim-status` estándar HRIS (D-10, RF-007). |
| **G-08** | Diagnóstico (salud) bajo **Read genérico**; sin permiso dedicado. | **Permiso dedicado** + **403**; **self-service** del titular (D-08, D-09, RF-008). |
| **G-09** | `ClaimDateUtc` sin validar. | Requerida + **no futura** (RF-009). |
| **G-10** | Sin **módulo de reglas puro**. | Crear `MedicalClaims.Rules.cs` unit-testeable (RNF). |
| **G-11** | Lectura sin filtros/agregados. | **Opcional** Fase 3 (RF-010). |
| **G-12** | Sin **adjuntos**. | **En alcance**: reutilizar subsistema de documentos (D-11, RF-012). |
| **G-13** | Integración aseguradora no implementada. | **No considerar** ahora; `Source*` inerte (D-13, RF-011). |

---

## 1. Resumen del producto o requerimiento

Sección del expediente para **consultar y registrar los reclamos de seguro médico** del empleado y de sus **familiares beneficiarios**: qué seguro, número de cuenta, tipo de reclamo, **diagnóstico**, montos (reclamado y pagado), moneda, **tiempo de respuesta** y observaciones, con **adjuntos** de soporte y un **estado** informativo. Es un **registro documental/informativo** (D-01): **no** procesa, **no** aprueba, **no** paga reclamos.

La funcionalidad **ya existe** (`PersonnelFileMedicalClaim`); este alcance la **valida y endurece** conforme a D-01…D-13: dimensión paciente/beneficiario, seguro obligatorio con nombre resuelto, catálogos de tipo y moneda ISO, validaciones de montos/fechas, fecha de resolución y tiempo derivado, estado estándar HRIS, adjuntos reutilizando el subsistema de documentos, autoservicio del empleado y protección del diagnóstico con permiso dedicado (403).

---

## 2. Objetivos del negocio

- **O-1.** **Trazabilidad de beneficios médicos:** registro consultable de los reclamos del empleado **y de sus beneficiarios**, con quién, cuánto, cuándo y soporte documental.
- **O-2.** **Cumplir el requisito explícito** de distinguir reclamos **propios vs. de familiares beneficiarios** (D-02).
- **O-3.** **Integridad y reportabilidad:** seguro **obligatorio** y resuelto a nombre; tipo y moneda **estandarizados**; montos y fechas **coherentes**; **estado** estándar.
- **O-4.** **Protección de datos sensibles de salud:** el **diagnóstico** y sus adjuntos accesibles **solo** a RRHH autorizado y al **propio empleado** (autoservicio); resto **403** (D-08).
- **O-5.** **Autoservicio:** que el empleado **vea y registre** sus reclamos sin depender de RRHH (D-09).
- **O-6.** **Escalabilidad:** dejar la entidad lista para una eventual integración con aseguradoras (D-13) sin reprocesar el modelo.

---

## 3. Alcance funcional (Fase 1)

- **F1.** Endurecimiento del **registro de reclamos** existente (CRUD vía API REST).
- **F2.** **Dimensión paciente/beneficiario** (`ClaimantType` + `BeneficiaryPublicId`) — **núcleo** (RF-001).
- **F3.** **Seguro obligatorio**, validado y con **nombre/póliza resueltos** en la lectura (RF-002).
- **F4.** **Catálogos**: `medical-claim-types` (SV) y **moneda ISO 4217** con default por país (RF-003/004).
- **F5.** **Validaciones** monetarias (no-negativos; pagado>reclamado permitido) y de fechas (RF-005/009).
- **F6.** **Fecha de resolución/pago** + **tiempo de respuesta derivado** (RF-006).
- **F7.** **Estado** informativo estándar HRIS (RF-007).
- **F8.** **Adjuntos** del reclamo **reutilizando** el subsistema de documentos (RF-012).
- **F9.** **Permiso dedicado** de salud + **403** + **autoservicio** del empleado (RF-008).
- **F10.** **Módulo de reglas puro** + tests (RNF, G-10).

---

## 4. Fuera de alcance

- **FA-1.** **Gestión/tramitación de reclamos** (envío a la aseguradora, aprobación, pago, conciliación contable) (D-01).
- **FA-2.** **Workflow de autorización** con transiciones gobernadas / SLA / escalamiento. El **estado** es **descriptivo** (D-10).
- **FA-3.** **Integración** con aseguradoras (sync automático) — **no considerar** ahora (D-13).
- **FA-4.** **Gestión de pólizas y beneficiarios** (ya existe en `PersonnelFileInsurance`/`…Beneficiary`; aquí solo se **referencia**).
- **FA-5.** **Cálculos de cobertura/deducible/copago** y reportería financiera avanzada.
- **FA-6.** **Notificaciones** sobre el estado del reclamo.
- **FA-7.** **Filtros/agregados** avanzados de consulta — **opcional** Fase 3 (RF-010).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Empleado titular (autoservicio)** | **Ve y registra** sus propios reclamos y los de **sus beneficiarios**, incluyendo diagnóstico y adjuntos (es **su** dato de salud) (D-09). |
| **Analista / Gestor de RRHH (Beneficios)** | Gestiona (CRUD) reclamos de **cualquier** empleado; **único** con escritura sobre terceros. Maneja datos de salud (permiso dedicado). |
| **Auditor / Cumplimiento** | Consulta cambios auditados (diff) de reclamos. **Sin** auditoría de lectura (D-12). |
| **Familiar beneficiario** | **Paciente** de un reclamo no-propio; **no** es usuario del sistema (vive como beneficiario de la póliza). |
| **Sistema — aseguradora** | **No** aplica en esta fase (D-13). |

---

## 6. Requerimientos funcionales

### RF-001 — Identificar al paciente del reclamo (titular vs. beneficiario)
**Descripción:** Indicar de **quién** es el reclamo: del **titular** o de un **familiar beneficiario**, y en este último caso **cuál** (D-02).
**Reglas de negocio:**
- `ClaimantType` (catálogo `TITULAR`/`BENEFICIARIO`), **obligatorio**.
- Si `BENEFICIARIO`: `BeneficiaryPublicId` **referencia** a un `PersonnelFileInsuranceBeneficiary` **del seguro indicado** (RF-002) y **del mismo expediente**; se conserva **snapshot** `PatientName`/`KinshipCode`.
- Si `TITULAR`: sin referencia a beneficiario; el paciente es el empleado.
**Criterios de aceptación:**
- Dado `BENEFICIARIO` sin `BeneficiaryPublicId`, entonces **422** `CLAIM_BENEFICIARY_REQUIRED`.
- Dado un beneficiario que **no** pertenece al seguro/expediente, entonces **422** `CLAIM_BENEFICIARY_NOT_OWNED`.
- Dado `TITULAR`, cuando se consulta, entonces el paciente se muestra como el **empleado**.
**Prioridad:** Alta · **Dependencias:** `PersonnelFileInsurance`/`PersonnelFileInsuranceBeneficiary`; catálogo `claimant-types`.

### RF-002 — Seguro obligatorio, validado y resuelto
**Descripción:** El **seguro es obligatorio** (D-03); validar la referencia y **resolver nombre/póliza** en la lectura (hoy solo devuelve un Guid).
**Reglas de negocio:**
- `InsurancePublicId` **obligatorio**; **debe** existir, ser del **mismo tenant** y **pertenecer al expediente** (G-02).
- El response **proyecta** `InsuranceName` (resuelto desde `InsuranceCode`/catálogo) y `PolicyNumber`; se conserva **snapshot** del nombre para integridad histórica del registro.
- `AccountNumber` puede **sugerirse** desde la póliza (editable).
- **Integridad:** al eliminar un seguro con reclamos asociados, **bloquear** o conservar el snapshot (no orfanar el registro).
**Criterios de aceptación:**
- Dado un reclamo sin seguro, entonces **422** `CLAIM_INSURANCE_REQUIRED`.
- Dada una `InsurancePublicId` inexistente/de otro expediente, entonces **422** `CLAIM_INSURANCE_NOT_FOUND`/`…NOT_OWNED`.
- Dado un reclamo con seguro válido, cuando se consulta, entonces el response incluye **nombre del seguro** y **póliza**.
**Prioridad:** Alta · **Dependencias:** `IPersonnelFileEmployeeRepository`, `GeneralCatalogs`.

### RF-003 — Catalogar el tipo de reclamo (SV)
**Descripción:** Sustituir `ClaimTypeCode` (texto libre) por **catálogo** `medical-claim-types` país-scoped, sembrado para **El Salvador** (D-04).
**Reglas de negocio:**
- `ClaimTypeCode` **debe** existir y estar **activo** en el catálogo del tenant/país.
- **Seed propuesto (SV, a confirmar el wording con el plan):** `AMBULATORIO`, `HOSPITALARIO`, `EMERGENCIA`, `FARMACIA`, `LABORATORIO`, `DENTAL`, `OFTALMOLOGICO`, `MATERNIDAD`, `OTRO`.
**Criterios de aceptación:**
- Dado un código fuera de catálogo, entonces **422** `CLAIM_TYPE_CODE_INVALID`.
- Dado un código válido, entonces se acepta y persiste normalizado.
**Prioridad:** Alta · **Dependencias:** `GeneralCatalogs` (`CountryScopedCatalogItem`).

### RF-004 — Moneda ISO 4217 por país
**Descripción:** `CurrencyCode` como **ISO 4217 (3 letras)** con **default por país** (D-05).
**Reglas de negocio:**
- `CurrencyCode` de **3 caracteres** (convención de la casa — `CompanyPreferenceAdministration` valida `length == 3`).
- **Default** = moneda de la **preferencia de compañía** (`CompanyPreference.CurrencyCode`, que refleja el país); **editable** a otra moneda ISO (p. ej. tratamiento en el extranjero).
- **Obligatoria** cuando exista `ClaimAmount` o `PaidAmount`.
**Criterios de aceptación:**
- Dado un código de moneda con longitud ≠ 3, entonces **422** `CLAIM_CURRENCY_INVALID`.
- Dado un monto sin moneda, entonces **422** `CLAIM_CURRENCY_REQUIRED`.
- Dado un reclamo nuevo, cuando se abre el formulario, entonces la moneda **se sugiere** según el país/compañía.
**Prioridad:** Alta · **Dependencias:** `CompanyPreference`.

### RF-005 — Validaciones monetarias (modelo de reembolso)
**Descripción:** Consistencia de `ClaimAmount` y `PaidAmount` bajo modelo de **reembolso** (D-06).
**Reglas de negocio:**
- Montos `≥ 0` (no negativos) — **regla dura**.
- `PaidAmount` **puede superar** a `ClaimAmount` (reembolsos/ajustes); **no se bloquea** — a lo sumo **aviso suave** (warning informativo), **no** error.
- Si hay monto, hay moneda (RF-004).
**Criterios de aceptación:**
- Dado un monto negativo, entonces **422** `CLAIM_AMOUNT_NEGATIVE`.
- Dado `PaidAmount > ClaimAmount`, entonces **se acepta** (opcional: **warning** no bloqueante en la respuesta/UI).
**Prioridad:** Media · **Dependencias:** módulo de reglas (G-10).

### RF-006 — Fecha de resolución y tiempo de respuesta derivado
**Descripción:** Capturar `ResolutionDateUtc` (fecha de resolución/pago) y **derivar** `ResponseTimeDays` (D-07).
**Reglas de negocio:**
- `ResolutionDateUtc`, si se informa, **≥ `ClaimDateUtc`**.
- `ResponseTimeDays` se **deriva** = `ResolutionDateUtc − ClaimDateUtc` (en días); si no hay resolución, queda **nulo/pendiente** (o capturable manualmente como respaldo).
- Si se captura manualmente, **≥ 0**.
**Criterios de aceptación:**
- Dada `ResolutionDateUtc < ClaimDateUtc`, entonces **422** `CLAIM_RESOLUTION_BEFORE_CLAIM`.
- Dadas ambas fechas, cuando se consulta, entonces el **tiempo de respuesta** es consistente y **no editable** manualmente (derivado).
**Prioridad:** Media · **Dependencias:** RF-009.

### RF-007 — Estado del reclamo (estándar HRIS, informativo)
**Descripción:** Capturar un **estado** informativo del reclamo (atributo, **sin** workflow — D-10).
**Reglas de negocio:**
- Catálogo `medical-claim-status` con los **estados estándar de la industria**: `PRESENTADO`, `EN_REVISION`, `PENDIENTE_DOCUMENTACION`, `APROBADO`, `RECHAZADO`, `PAGADO`, `PAGO_PARCIAL`, `ANULADO`.
- Es **descriptivo**: **no** gobierna transiciones ni dispara acciones (módulo es solo registro). Se permite cualquier valor del catálogo en cualquier momento.
**Criterios de aceptación:**
- Dado un estado fuera de catálogo, entonces **422** `CLAIM_STATUS_INVALID`.
- Dado un estado válido, entonces se persiste y se muestra en la consulta.
**Prioridad:** Media · **Dependencias:** `GeneralCatalogs`.

### RF-008 — Permiso dedicado, 403 y autoservicio del empleado
**Descripción:** Proteger reclamos médicos con **permiso dedicado** y devolver **403** a quien no lo tenga (D-08), salvo el **propio empleado** vía **autoservicio** (D-09).
**Reglas de negocio:**
- Lectura por terceros: `PersonnelFiles.ViewMedicalClaims`; escritura por terceros: `PersonnelFiles.ManageMedicalClaims` (patrón de `ViewCompensation`/`AuthorizeRehire`).
- **Sin** el permiso y **sin** ser el titular → **403** (no enmascaramiento — D-08).
- **Autoservicio (D-09):** el **empleado titular** puede **ver y registrar** (crear) reclamos de **su propio** expediente (propios y de sus beneficiarios), reutilizando el patrón self-service existente (`PersonnelFileEmployeeHandlerBases.cs:245`). *(Edición/eliminación de terceros: solo RRHH; del propio registro: ver nota de diseño.)*
- Auditoría: **solo cambios** (escrituras), con el diff existente (D-12); **no** se auditan lecturas.
**Criterios de aceptación:**
- Dado un usuario sin `ViewMedicalClaims` que **no** es el titular, cuando consulta, entonces **403 FORBIDDEN**.
- Dado el **empleado titular**, cuando consulta o **registra** un reclamo propio/de su beneficiario, entonces **200/201** (self-service).
- Dada cualquier escritura, cuando ocurre, entonces queda **auditada** (diff); las lecturas **no** generan auditoría.
**Prioridad:** Alta · **Dependencias:** Provisioning/`IdentityAccess` (alta + seed de permisos); `IAuditService`; servicio de autorización del expediente.

### RF-009 — Validación de la fecha del reclamo
**Descripción:** Asegurar coherencia de `ClaimDateUtc`.
**Reglas de negocio:**
- **Requerida** y **no futura**.
- *(Opción)* dentro de la vigencia del seguro (`StartDateUtc`/`EndDateUtc`) si la póliza la define.
**Criterios de aceptación:**
- Dado `ClaimDateUtc` ausente/`0001-01-01`/futura, entonces **422** `CLAIM_DATE_INVALID`.
**Prioridad:** Media · **Dependencias:** RF-002.

### RF-010 — (Opcional, Fase 3) Consulta enriquecida
**Descripción:** Filtros y agregados de **solo lectura**.
**Reglas de negocio:** Filtros por **rango de fechas**, **tipo**, **beneficiario/titular**, **estado**; agregados de **total reclamado/pagado** por año y paciente.
**Criterios de aceptación:** Dado un beneficiario y un rango, cuando filtro, entonces obtengo sus reclamos y el **total pagado**.
**Prioridad:** Baja · **Dependencias:** RF-001.

### RF-011 — (No considerar ahora) Integración con aseguradora
**Descripción:** Los campos `SourceSystem`/`SourceReference`/`SourceSyncedUtc` **permanecen** como punto de extensión **inerte** (D-13); **no** se construye conector.
**Reglas de negocio:** Sin integración en esta fase; no se exponen ni se pueblan automáticamente.
**Criterios de aceptación:** N/A en esta fase.
**Prioridad:** Diferida.

### RF-012 — Adjuntos del reclamo (reutilizando el subsistema de documentos)
**Descripción:** Permitir **adjuntar documentos de soporte** al reclamo (formulario, factura, receta, EOB, informe), **reutilizando** el subsistema ya desarrollado (D-11).
**Reglas de negocio:**
- Reutilizar `IFileStorageProvider` (Azure Blob) + `StoredFile` + flujo **`upload-session` → `complete`** + **SAS** de descarga + limpieza en background (todo ya existe).
- Nuevo `FilePurpose` (p. ej. `MedicalClaimDocument`) con reglas (tipos permitidos PDF/JPG/PNG; tamaño ~10 MB, como `PersonnelDocument`).
- Nueva colección hija `MedicalClaimDocument` (espejo de `PersonnelFileDocument`: `FilePublicId`, `FileName`, `ContentType`, `SizeBytes`, `DocumentType` por catálogo, `Observations`, `IsActive`, `ConcurrencyToken`), **vinculada al reclamo**.
- **Tipos de documento (catálogo):** `FORMULARIO_RECLAMO`, `FACTURA`, `RECETA`, `EOB` (explicación de beneficios), `INFORME_MEDICO`, `OTRO`.
- Los adjuntos son **dato sensible de salud**: **misma** protección que el reclamo (RF-008: permiso dedicado o titular; 403 en caso contrario).
**Criterios de aceptación:**
- Dado un archivo permitido, cuando se adjunta al reclamo, entonces se sube vía sesión y queda listado con su metadato.
- Dado un tipo/ tamaño no permitido, entonces **413/422** (reglas de `FilePurpose`).
- Dado un usuario sin acceso al reclamo, cuando intenta descargar un adjunto, entonces **403**.
**Prioridad:** Media · **Dependencias:** `IFileStorageProvider`, `StoredFile`, `FilePurpose`, controlador de documentos (patrón `PersonnelFileDocumentsController`).

---

## 7. Requerimientos no funcionales

- **Privacidad / datos de salud.** `Diagnosis` y **adjuntos** son **categoría especial** (salud). Acceso por **permiso dedicado** o **titular**; resto **403** (RF-008/012). Minimización: exponerlos **solo** donde sea necesario. Auditoría de **cambios** (no lecturas, D-12).
- **Seguridad / Multi-tenant.** Toda operación filtrada por **tenant**; referencias (seguro, beneficiario, archivo) **sin fuga cross-tenant**.
- **Integridad / Consistencia.** Reglas de paciente, seguro, montos, moneda, fechas y estado en un **módulo de reglas puro** `MedicalClaims.Rules.cs`, **unit-testeable** (patrón `EmploymentAssignments.Rules.cs`) — G-10.
- **Concurrencia.** `ConcurrencyToken` + `If-Match`/`ETag` (ya implementado; aplica también a adjuntos).
- **Rendimiento.** Índice `(tenant, expediente, fecha, tipo)` existente; subida de adjuntos **directa a Blob** vía SAS (no pasa por la API).
- **Usabilidad.** Selector de **seguro** y de **beneficiario** (no GUID); **nombre del seguro** legible; moneda sugerida por país; tiempo de respuesta **derivado**; estado visible.
- **Compatibilidad / API.** Conservar los 6 endpoints del reclamo; añadir sub-recurso de **adjuntos**. Cambios de validación retro-compatibles salvo datos inválidos. El seguro pasa de opcional a **obligatorio** (breaking para datos sin seguro → migración).
- **Localización.** Mensajes/errores **bilingües (ES/EN)** con código estable (test de paridad — convención de la casa).
- **Mantenibilidad.** Reglas con tests unitarios y de **paridad de localización**.

---

## 8. Historias de usuario

### HU-001 — (Autoservicio) Registrar mi reclamo o el de mi beneficiario
Como **empleado**, quiero **registrar y consultar mis reclamos y los de mis beneficiarios**, para **dar seguimiento a mis beneficios sin depender de RRHH** (D-09).
- Dado mi expediente, cuando registro un reclamo (propio o de un beneficiario de mi póliza), entonces se guarda y lo veo (RF-008).
- Dado un reclamo de otro empleado, cuando intento verlo, entonces **403** (RF-008).

### HU-002 — Registrar un reclamo de un familiar beneficiario
Como **RRHH (Beneficios)**, quiero **indicar que un reclamo es de un beneficiario y de cuál**, para **diferenciarlo del titular** (RF-001).
- Dado un seguro con beneficiarios, cuando elijo `BENEFICIARIO` y selecciono uno, entonces se guarda vinculado.
- Dado `BENEFICIARIO` sin seleccionar, entonces **bloquea**.

### HU-003 — Ver el nombre del seguro, no un identificador
Como **usuario**, quiero **ver el nombre del seguro y la póliza** en la lista de reclamos, para **entender el contexto** (RF-002).
- Dado un reclamo con seguro válido, cuando consulto, entonces veo **nombre** y **póliza**.

### HU-004 — Estandarizar tipo, moneda y estado
Como **RRHH**, quiero **elegir tipo, moneda y estado de listas**, para **datos consistentes y reportables** (RF-003/004/007).
- Dado el catálogo, cuando elijo "HOSPITALARIO"/"USD"/"PAGADO", entonces se acepta; valores fuera de catálogo **bloquean**.

### HU-005 — Adjuntar soporte del reclamo
Como **RRHH/empleado**, quiero **adjuntar factura/receta/EOB al reclamo**, para **respaldarlo documentalmente** (RF-012).
- Dado un PDF/imagen permitido, cuando lo adjunto, entonces se sube y queda listado.
- Dado un usuario sin acceso al reclamo, cuando intenta descargar, entonces **403**.

### HU-006 — Tiempo de respuesta confiable
Como **RRHH**, quiero que **el tiempo de respuesta se calcule desde las fechas**, para **evitar errores manuales** (RF-006).
- Dadas fecha de reclamo y de resolución, cuando consulto, entonces el tiempo de respuesta es **derivado** y consistente.

### HU-007 — Cuidar los datos de salud
Como **Cumplimiento**, quiero que **el diagnóstico y los adjuntos solo los vea quien corresponde** y que **los cambios queden auditados**, para **proteger datos sensibles** (RF-008).
- Dado un usuario sin permiso ni titularidad, entonces **403**; dada una escritura, queda **auditada** (diff).

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** (`IsCompletedEmployee`) pueden tener reclamos (ya implementado — `MedicalClaims.Handlers.cs:46`).
- **RN-02.** El reclamo pertenece a un expediente (titular); el **paciente** es el **titular** o un **beneficiario** de su seguro (D-02, RF-001).
- **RN-03.** Si `ClaimantType=BENEFICIARIO`, el beneficiario **debe** pertenecer al **seguro** y **expediente** del reclamo (RF-001/002).
- **RN-04.** El **seguro es obligatorio**, válido, del **mismo tenant** y **del expediente**; su **nombre/póliza** se resuelven y se **snapshotean** (D-03, RF-002).
- **RN-05.** `ClaimTypeCode` ∈ catálogo `medical-claim-types` (SV) activo (D-04, RF-003).
- **RN-06.** `CurrencyCode` ISO 4217 (3); **default por país** desde `CompanyPreference`; obligatoria si hay monto (D-05, RF-004).
- **RN-07.** Montos **no negativos**; `PaidAmount` **puede superar** a `ClaimAmount` (reembolso) — **no se bloquea** (D-06, RF-005).
- **RN-08.** `ResolutionDateUtc ≥ ClaimDateUtc`; `ResponseTimeDays` **derivado** (no editable si hay fechas) (D-07, RF-006).
- **RN-09.** `ClaimDateUtc` **requerida** y **no futura** (RF-009).
- **RN-10.** `ClaimStatusCode` ∈ catálogo `medical-claim-status` (estándar HRIS); **descriptivo**, sin transiciones gobernadas (D-10, RF-007).
- **RN-11.** El **diagnóstico** y los **adjuntos** son **dato de salud**: acceso por **permiso dedicado** o **titular**; resto **403** (D-08, RF-008/012).
- **RN-12.** **Autoservicio:** el **titular** puede **ver y registrar** reclamos de su propio expediente; **terceros** requieren `ViewMedicalClaims`/`ManageMedicalClaims` (D-09, RF-008).
- **RN-13.** El módulo es **solo registro**: **no** aprueba, **no** paga, **no** tramita (D-01, FA-1/2).
- **RN-14.** Toda escritura exige **If-Match** y queda **auditada (diff)**; **no** se auditan lecturas (D-12).
- **RN-15.** Adjuntos reutilizan el subsistema de documentos (`FilePurpose=MedicalClaimDocument`); tipos/tamaño regidos por sus reglas (D-11, RF-012).

---

## 10. Flujos principales

**Flujo A: Registrar un reclamo (RRHH o autoservicio)**
1. RRHH (con `ManageMedicalClaims`) **o** el **empleado titular** (autoservicio) abre el expediente (debe estar **completado**).
2. Entra a **"Reclamos de Seguro Médico"** → **Agregar**.
3. Selecciona el **seguro** (**obligatorio**); el sistema lo **valida** y muestra **nombre/póliza** (RF-002).
4. Indica el **paciente**: `TITULAR` o `BENEFICIARIO`; si beneficiario, **lo elige** de la póliza (RF-001).
5. Selecciona **tipo** (catálogo, RF-003); ingresa **diagnóstico**, **montos** + **moneda** (sugerida por país, RF-004/005), **fecha del reclamo** (RF-009) y, si aplica, **fecha de resolución** (RF-006) y **estado** (RF-007); **observaciones**.
6. *(Opcional)* **Adjunta** factura/receta/EOB (RF-012).
7. El sistema **valida** y **persiste**; **audita** (diff) y devuelve `ETag`. El **tiempo de respuesta** se muestra **derivado**.

**Flujo B: Consultar reclamos**
1. RRHH (con `ViewMedicalClaims`) o el **titular** (autoservicio) abre la lista; ve **seguro, paciente, tipo, montos, estado, tiempo de respuesta, adjuntos**.
2. Un usuario **sin** permiso y **no** titular → **403** (D-08).

**Flujo C: Adjuntar/descargar documento**
1. Crea **upload-session** → sube a Blob por SAS → **complete** → vincula al reclamo (RF-012).
2. Para descargar, obtiene **read-url** (SAS), sujeto al control de acceso del reclamo.

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Titular **no completado**. | **Bloqueo** `STATE_RULE_VIOLATION` (ya implementado — `MedicalClaims.Handlers.cs:46`). |
| **E2** | **Sin seguro** (obligatorio). | **Bloqueo** `422 CLAIM_INSURANCE_REQUIRED` (RF-002). |
| **E3** | Seguro inexistente / de otro expediente. | **Bloqueo** `422 CLAIM_INSURANCE_NOT_FOUND` / `…NOT_OWNED` (RF-002). |
| **E4** | `BENEFICIARIO` sin beneficiario / beneficiario ajeno al seguro. | **Bloqueo** `422 CLAIM_BENEFICIARY_REQUIRED` / `…NOT_OWNED` (RF-001). |
| **E5** | `ClaimTypeCode` / `ClaimStatusCode` fuera de catálogo. | **Bloqueo** `422 CLAIM_TYPE_CODE_INVALID` / `CLAIM_STATUS_INVALID` (RF-003/007). |
| **E6** | Moneda longitud ≠ 3 / monto sin moneda. | **Bloqueo** `422 CLAIM_CURRENCY_INVALID` / `CLAIM_CURRENCY_REQUIRED` (RF-004). |
| **E7** | Monto **negativo**. | **Bloqueo** `422 CLAIM_AMOUNT_NEGATIVE` (RF-005). |
| **E8** | `PaidAmount > ClaimAmount`. | **Se acepta** (reembolso); opcional **warning** no bloqueante (D-06, RF-005). |
| **E9** | `ResolutionDateUtc < ClaimDateUtc`. | **Bloqueo** `422 CLAIM_RESOLUTION_BEFORE_CLAIM` (RF-006). |
| **E10** | `ClaimDateUtc` ausente/`0001-01-01`/futura. | **Bloqueo** `422 CLAIM_DATE_INVALID` (RF-009). |
| **E11** | Usuario **sin** permiso y **no** titular consulta/escribe. | **403 FORBIDDEN** (RF-008). |
| **E12** | **Empleado titular** registra/consulta lo suyo. | **200/201** (self-service, RF-008). |
| **E13** | Adjunto con tipo/tamaño no permitido. | **413/422** según reglas de `FilePurpose` (RF-012). |
| **E14** | `If-Match`/`ConcurrencyToken` no coincide. | **409 CONFLICT** (ya implementado — `MedicalClaims.Handlers.cs:128,213,318`). |
| **E15** | Eliminar un seguro con reclamos asociados. | **Bloqueo** o conservación de snapshot (RN-04, RF-002). |

---

## 12. Datos requeridos

### Entidad: `PersonnelFileMedicalClaim` *(ya existe — `Domain/PersonnelFiles/PersonnelFileEmployee.cs:957`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad |
| `personnelFileId` | long (FK) | Sí | del tenant | ✅ existe | **Titular** (dueño del reclamo) |
| `insurancePublicId` | GUID | **Sí** | existe + tenant + del expediente; resolver **nombre/póliza** | 🔧 **ahora obligatorio** (D-03, RF-002) | Seguro asociado |
| `insuranceNameSnapshot` | Texto | No | — | 🆕 opcional (RF-002) | *Snapshot* del nombre del seguro |
| `claimantType` | Texto → **catálogo** | **Sí** | `TITULAR`/`BENEFICIARIO` | 🆕 **nuevo** (D-02, RF-001) | **Paciente: titular o beneficiario** |
| `beneficiaryPublicId` | GUID (ref) | Condicional | requerido si `BENEFICIARIO`; del seguro/expediente | 🆕 **nuevo** (RF-001) | Beneficiario paciente |
| `patientNameSnapshot` / `kinshipCodeSnapshot` | Texto | No | — | 🆕 opcional (RF-001) | *Snapshot* del paciente/parentesco |
| `accountNumber` | Texto (≤120) | No | sugerible desde póliza | ✅ existe | Número de cuenta |
| `claimTypeCode` | Texto → **catálogo** | Sí | catálogo `medical-claim-types` (SV) | 🔧 a catálogo (D-04, RF-003) | Tipo de reclamo |
| `diagnosis` | Texto (≤1000) | No | **dato de salud**; permiso/titular (403) | 🔒 proteger (D-08, RF-008) | Diagnóstico |
| `claimAmount` | Decimal(18,2) | No | `≥ 0`; moneda si hay monto | 🔧 validar (RF-005) | Monto del reclamo |
| `currencyCode` | Texto **ISO(3)** | Condicional | 3 chars; default por país; obligatoria si hay monto | 🔧 a ISO + default (D-05, RF-004) | Moneda |
| `paidAmount` | Decimal(18,2) | No | `≥ 0`; **puede superar** a `claimAmount` | 🔧 validar (D-06, RF-005) | Monto pagado/reembolsado |
| `claimDateUtc` | Fecha | Sí | requerida; **no futura** | 🔧 validar (RF-009) | Fecha del reclamo |
| `resolutionDateUtc` | Fecha | No | `≥ claimDateUtc` | 🆕 **nuevo** (D-07, RF-006) | Fecha de resolución/pago |
| `responseTimeDays` | Número | No (derivado) | `≥ 0`; **derivado** de fechas | 🔧 derivar (D-07, RF-006) | Tiempo de respuesta |
| `claimStatusCode` | Texto → **catálogo** | No | catálogo `medical-claim-status` | 🆕 **nuevo** (D-10, RF-007) | Estado informativo |
| `notes` | Texto (≤2000) | No | — | ✅ existe | Observaciones |
| `sourceSystem`/`sourceReference`/`sourceSyncedUtc` | Texto/Texto/Fecha | No | inerte (D-13) | ✅ existe (sin uso) | Integración (no considerar) |
| `isActive` | Booleano | Sí | soft-delete (PATCH) | ✅ existe | Estado activo |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

### Entidad: `MedicalClaimDocument` *(nueva — RF-012; espejo de `PersonnelFileDocument`)*

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `publicId` | GUID | Sí | único | Identidad |
| `medicalClaimId` | long (FK) | Sí | del tenant | Reclamo dueño |
| `filePublicId` | GUID | Sí | ref. a `StoredFile` activo | Archivo en Blob |
| `documentTypeCode` | Texto → **catálogo** | Sí | `FORMULARIO_RECLAMO`/`FACTURA`/`RECETA`/`EOB`/`INFORME_MEDICO`/`OTRO` | Tipo de documento |
| `fileName` | Texto (≤260) | Sí | — | Nombre original |
| `contentType` | Texto (≤200) | Sí | tipos de `FilePurpose=MedicalClaimDocument` | MIME |
| `sizeBytes` | Número | Sí | ≤ máx del purpose | Tamaño |
| `observations` | Texto (≤2000) | No | — | Notas |
| `isActive` | Booleano | Sí | soft-delete | Estado |
| `concurrencyToken` | GUID | Sí | If-Match | Concurrencia |

### Catálogos nuevos

| Catálogo | Valores (seed) | Ref. |
|---|---|---|
| `claimant-types` | `TITULAR`, `BENEFICIARIO` | D-02, RF-001 |
| `medical-claim-types` (SV) | `AMBULATORIO`, `HOSPITALARIO`, `EMERGENCIA`, `FARMACIA`, `LABORATORIO`, `DENTAL`, `OFTALMOLOGICO`, `MATERNIDAD`, `OTRO` | D-04, RF-003 |
| `medical-claim-status` | `PRESENTADO`, `EN_REVISION`, `PENDIENTE_DOCUMENTACION`, `APROBADO`, `RECHAZADO`, `PAGADO`, `PAGO_PARCIAL`, `ANULADO` | D-10, RF-007 |
| `medical-claim-document-types` | `FORMULARIO_RECLAMO`, `FACTURA`, `RECETA`, `EOB`, `INFORME_MEDICO`, `OTRO` | D-11, RF-012 |

> **Entidades reutilizadas (ya existen):** `PersonnelFileInsurance` (`PersonnelFileEmployee.cs:774`), `PersonnelFileInsuranceBeneficiary` (`:891`), `StoredFile`/`IFileStorageProvider`/`FilePurpose`, `CompanyPreference.CurrencyCode`. El reclamo **referencia**, no duplica.

---

## 13. Integraciones necesarias

- **Catálogos generales (`GeneralCatalogs`).** Nuevos `claimant-types`, `medical-claim-types` (SV), `medical-claim-status`, `medical-claim-document-types`. Interno.
- **Seguros/Beneficiarios (`PersonnelFileInsurance`/`…Beneficiary`).** Validar seguro (obligatorio), **resolver nombre** y **listar beneficiarios** (RF-001/002).
- **Subsistema de documentos (`IFileStorageProvider`/`StoredFile`/`FilePurpose`).** **Reutilizar** para adjuntos: `upload-session`/`complete`, SAS, limpieza en background; nuevo `FilePurpose=MedicalClaimDocument` (RF-012, D-11).
- **Preferencias de compañía (`CompanyPreference`).** Default de **moneda por país** (RF-004, D-05).
- **Identidad/Provisioning (`IdentityAccess`).** Alta y seed de permisos `PersonnelFiles.ViewMedicalClaims` / `ManageMedicalClaims` (RF-008).
- **Autorización del expediente (`IPersonnelFileAuthorizationService`).** **Autoservicio** del titular (RF-008, D-09) reusando el patrón self-service (D-16).
- **Auditoría (`IAuditService`).** Diff de **cambios** (D-12). **No** lecturas.
- **Aseguradora:** **no** aplica (D-13).

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Empleado titular (autoservicio)** | **Ver y registrar** reclamos de **su propio** expediente (propios y de beneficiarios), con diagnóstico y adjuntos. | Solo **su** expediente; **no** ve los de terceros (403). *(Edición/baja de lo propio: nota de diseño.)* |
| **RRHH / Beneficios (gestor)** | CRUD y lectura de reclamos de **cualquier** empleado, incl. diagnóstico/adjuntos (**`ManageMedicalClaims`** + `ViewMedicalClaims`). | Solo expedientes **completados**; solo su **tenant**. |
| **Auditor / Cumplimiento** | Leer reclamos + **bitácora de cambios** (`ViewMedicalClaims`). | Sin escritura; auditoría **solo de cambios** (D-12). |
| **Otros usuarios (Read genérico, no titular)** | — | **403** sobre reclamos médicos (D-08). |
| **Sistema (aseguradora)** | — | **No** aplica (D-13). |

> **Permisos nuevos (RF-008):** `PersonnelFiles.ViewMedicalClaims` (lectura) y `PersonnelFiles.ManageMedicalClaims` (escritura por terceros), patrón de `PersonnelFiles.ViewCompensation` (`ProvisioningConstants.cs:71`). **Hoy** los reclamos usan el `Read`/`Manage` **genérico** (`PersonnelFileCompensationController.cs:25-27`), por lo que el diagnóstico queda **subprotegido**.

---

## 15. Criterios de aceptación generales

- ✅ Se puede **registrar y consultar** si el reclamo es del **titular** o de un **beneficiario** identificado (RF-001).
- ✅ El **seguro es obligatorio**, validado, y la consulta muestra su **nombre** y **póliza** (RF-002).
- ✅ **Tipo** (SV), **moneda ISO por país** y **estado** provienen de **catálogo** (RF-003/004/007).
- ✅ Montos **no negativos**; **pagado > reclamado permitido** (reembolso); **fecha** válida y **tiempo de respuesta derivado** (RF-005/006/009).
- ✅ El **diagnóstico y adjuntos** solo son visibles con **permiso dedicado** o por el **titular**; resto **403** (RF-008/012).
- ✅ El **empleado** puede **ver y registrar** lo suyo (autoservicio) (RF-008).
- ✅ **Adjuntos** reutilizan el subsistema de documentos existente (RF-012).
- ✅ Escritura **concurrencia-segura (If-Match)** y **auditada (diff)**; lecturas **no** auditadas (RF-008).
- ✅ Reglas en **módulo puro** con **tests unitarios** (G-10); mensajes **bilingües (ES/EN)**.
- ✅ El módulo permanece **solo de registro** (D-01).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1. Datos de salud subprotegidos (G-08).** Hoy el diagnóstico es accesible con `Read` genérico. Mitigación: RF-008 (permiso dedicado + 403 + self-service). **Alta.**
- **R2. Seguro ahora obligatorio (D-03).** Reclamos existentes sin seguro **romperían** la validación → **migración**/saneamiento previo. Definir comportamiento al **eliminar un seguro** con reclamos (RN-04).
- **R3. Datos sucios existentes.** Tipos/moneda en texto libre; referencias inválidas → limpieza/migración (tipos→catálogo, moneda→ISO(3), poblar `claimantType=TITULAR`).
- **R4. Calidad del beneficiario.** Si las pólizas no tienen beneficiarios cargados, solo se podría registrar `TITULAR` hasta poblarlos (S2).
- **R5. Autoservicio con escritura (D-09).** El patrón self-service existente es **de lectura**; extenderlo a **crear** requiere diseño cuidadoso del *scope* (el empleado solo su expediente) y de edición/baja de lo propio.
- **R6. Adjuntos sensibles.** Las facturas/EOB pueden contener datos de salud; aplicar el **mismo control** que al reclamo (RF-012).

### Supuestos
- **S1.** Verificar datos productivos/QA; sin datos → **drop & recreate**/normalización; con datos → **migración** previa (como en otros módulos del expediente).
- **S2.** Las pólizas y **beneficiarios** ya se gestionan en el expediente; el reclamo los **referencia**.
- **S3.** País de referencia **SV**; catálogos sembrados para SV; moneda por `CompanyPreference` (usual `USD`).
- **S4.** El módulo es **solo registro** (D-01).
- **S5.** El subsistema de documentos (Azure Blob + SAS + cleanup) está **operativo** y es reutilizable (D-11).
- **S6.** El patrón **self-service** (D-16) es reutilizable y extensible a escritura para el titular (D-09).

### Dependencias
- **D1.** `GeneralCatalogs` (4 catálogos nuevos).
- **D2.** `PersonnelFileInsurance` / `PersonnelFileInsuranceBeneficiary` + repositorio del expediente.
- **D3.** `IFileStorageProvider` / `StoredFile` / `FilePurpose` (adjuntos).
- **D4.** `CompanyPreference` (moneda por país).
- **D5.** `IdentityAccess`/Provisioning (permisos `ViewMedicalClaims` / `ManageMedicalClaims`).
- **D6.** `IPersonnelFileAuthorizationService` (autoservicio) + `IAuditService` (diff de cambios).

---

## 17. Decisiones resueltas (cierre de preguntas abiertas)

| Pregunta | Decisión | Ref. |
|---|---|---|
| P-01 ¿Solo registro? | **Sí, solo registro.** | D-01 |
| P-02 ¿Titular vs. beneficiario + referencia? | **Sí.** | D-02, RF-001 |
| P-03 ¿Seguro obligatorio? | **Obligatorio.** | D-03, RF-002 |
| P-04 ¿Catálogo de tipos? | **Sí, catálogo SV.** | D-04, RF-003 |
| P-05 ¿Moneda? | **ISO 4217 (3) por país** (default `CompanyPreference`). | D-05, RF-004 |
| P-06 ¿Pagado > reclamado? | **Sí (reembolso)**; no se bloquea. | D-06, RF-005 |
| P-07 ¿Fecha de resolución + derivar tiempo? | **Sí.** | D-07, RF-006 |
| P-08 ¿403 o enmascarar? | **403.** | D-08, RF-008 |
| P-09 ¿Autoservicio? | **Empleado (ver/registrar) y RRHH.** | D-09, RF-008 |
| P-10 ¿Estado? | **Sí, estándar HRIS** (8 estados). | D-10, RF-007 |
| P-11 ¿Adjuntos? | **Sí, reutilizando el subsistema de documentos.** | D-11, RF-012 |
| P-12 ¿Auditoría de lectura? | **No, solo cambios.** | D-12, RF-008 |
| P-13 ¿Integración aseguradora? | **No considerar ahora.** | D-13, RF-011 |

> **Pendientes menores de diseño (no bloqueantes):** (a) si el **empleado** puede **editar/eliminar** sus propios reclamos o solo crearlos/verlos (default propuesto: ver + crear; editar/baja por RRHH); (b) comportamiento exacto al **eliminar un seguro** con reclamos (bloquear vs. snapshot); (c) wording final del **seed SV** de tipos de reclamo con el plan del cliente; (d) nombres exactos de permisos (`ViewMedicalClaims`/`ManageMedicalClaims`, sujeto a convención).

---

## 18. Recomendaciones del Analista de Negocio

1. **Reposicionar como "validación + endurecimiento", no construcción nueva.** La entidad, CRUD, API, auditoría, concurrencia y soft-delete **ya existen**. El esfuerzo es de **campos nuevos** (paciente, fecha de resolución, estado, adjuntos), **catálogos**, **validaciones**, **resolución de nombre**, **permiso dedicado** y **autoservicio**.

2. **MVP Fase 1 (núcleo del requisito + privacidad):**
   - **RF-001** (paciente: titular/beneficiario) y **RF-002** (seguro obligatorio + nombre resuelto).
   - **RF-008** (permiso dedicado + **403** + **autoservicio** del titular) — privacidad y D-09.
   - **RF-005** (montos, reembolso) y **RF-009** (fecha válida) — bajo costo.
   - Extraer **`MedicalClaims.Rules.cs`** (módulo de reglas puro, con tests — G-10).

3. **Fase 2 (estandarización y trazabilidad):** **RF-003/004** (catálogos tipo SV y moneda ISO por país), **RF-006** (fecha de resolución → tiempo derivado), **RF-007** (estado estándar HRIS) y **RF-012** (**adjuntos** reutilizando el subsistema de documentos).

4. **Fase 3 (opcional):** **RF-010** (filtros/agregados de consulta). **RF-011** (integración aseguradora) **no se considera** ahora (D-13), pero los campos `Source*` quedan listos.

5. **Datos y migración (S1, R2):** verificar datos existentes. Sin datos → **drop & recreate**/normalización; con datos → **migrar** antes de activar validaciones duras (especialmente el **seguro obligatorio** y `claimantType=TITULAR` por defecto). Definir el comportamiento al **eliminar un seguro** con reclamos.

6. **Privacidad por diseño (R1, R6):** tratar **diagnóstico y adjuntos** como categoría especial: **permiso dedicado**, **403** para no autorizados, **self-service** del titular, y **auditoría de cambios** (D-12). Revisar con **Cumplimiento** conforme a la normativa de protección de datos aplicable en SV.

7. **Autoservicio con cuidado (R5):** reutilizar el patrón self-service (D-16) para **lectura** y extenderlo a **creación** del titular sobre su propio expediente; acotar el *scope* y resolver el pendiente (a) de edición/baja.

8. **Reutilizar el subsistema de documentos (D-11):** **no** construir almacenamiento nuevo; usar `IFileStorageProvider`/`StoredFile`/`FilePurpose` (nuevo `MedicalClaimDocument`) y el flujo `upload-session`/`complete` + SAS + limpieza, espejando `PersonnelFileDocument`.

9. **Naming/semántica:** exponer el módulo como **"Reclamos de Seguro Médico"** con su **propio permiso**, aunque el código resida bajo `Compensation`; evita confundir el gobierno de accesos entre **salario** y **salud**.

---

> **Naturaleza del documento.** Análisis de **validación + brechas (GAP)** sobre funcionalidad **ya implementada** (`PersonnelFileMedicalClaim`). El "estado as-is" está **verificado contra el código** (referencias `archivo:línea`). Las **decisiones del negocio están ratificadas** (D-01…D-13, 2026-06-22); el documento queda **cerrado para Fase 1** y listo para el **plan técnico**. Quedan **pendientes menores de diseño** (no bloqueantes) listados en la sección 17.
