# Análisis de Negocio — Equipo o Acceso del Empleado (activos, licencias y accesos a sistemas)

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio (validación + brechas) |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo, Gerencia |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Empleo (`PersonnelFileAssetAccess`) · Catálogos generales (`GeneralCatalogs`) · (futuro) Egreso/Finalización · (futuro) Provisión TI / IAM |
| **Estado** | **Fase 1 cerrada** (el requerimiento ya estaba cubierto por `PersonnelFileAssetAccess`) **+ Nivel A IMPLEMENTADO (2026-06-22)**: catálogos `asset-access-types` y `delivery-statuses`, validación de coherencia de fechas (app + *check constraints* en BD), módulo de reglas puro y errores bilingües (ES/EN). **Nivel B** (devolución/condición, serie/etiqueta/valor, acta/responsiva, egreso, IAM, permiso dedicado) permanece **fuera de alcance** (P-03…P-09). Ver **§ Estado de implementación**. |
| **Versión** | v2 (Nivel A implementado) |
| **Fecha** | 2026-06-22 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |

---

## Contexto del cambio

En el **expediente del empleado** se requiere una sección de **"Equipo o acceso"** para registrar el **equipo asignado** (computadora, teléfono celular, uniformes) y las **licencias y accesos a sistemas** que la institución posee, junto con su **nivel de acceso**. Por cada registro debe capturarse: **equipo o acceso, fecha de alta, fecha de baja, observación, fecha de entrega y estado de entrega**.

El objetivo declarado es **doble**: (1) **validar** que esté **bien alineado con el desarrollo ya implementado** y (2) **analizar y agregar** la información necesaria para este tipo de procesos en un HRIS — **y si no es necesario agregar nada, no incluir nada y dar por cerrado**.

> **Hallazgo clave (verificado en código).** Esto **no es un desarrollo desde cero**. CLARIHR **ya tiene implementada** la sección como la entidad **`PersonnelFileAssetAccess`**, con **CRUD completo** (Domain + Application/CQRS + API REST + JSON Patch + concurrencia + auditoría + multi-tenant). **Los seis campos solicitados existen tal cual**, más dos campos adicionales útiles (**nivel de acceso** y **estado activo**). El requerimiento, **tal como está enunciado, está 100 % cubierto**. Lo que existe hoy es un **registro documental** de activos/accesos (qué se asignó, cuándo, su entrega y su baja). En consecuencia, **la Fase 1 puede darse por cerrada**. El único trabajo **recomendable** —y aun así **opcional**— es de **endurecimiento y consistencia con el resto de CLARIHR**: convertir códigos de **texto libre** a **catálogos** (tipo de equipo/acceso y estado de entrega) y **validar la coherencia de fechas** (alta ≤ baja). Todo lo demás (devolución con condición, identificación del activo, acta firmada, integración con egreso/IAM) son **mejoras de valor opcionales** que se dejan como **preguntas abiertas** al negocio para no sobre-construir.

### Estado actual verificado en el código (línea base "as-is")

| # | Tema | Hallazgo (verificado) |
|---|---|---|
| 1 | **Entidad** | `PersonnelFileAssetAccess` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:669`). Hija del agregado `PersonnelFile` (FK `personnel_file_id`, **cascade delete**). |
| 2 | **Campos** | `AssetTypeCode` (≤80), `AssetOrAccessName` (≤200), `AccessLevelCode?` (≤80), `StartDateUtc`, `EndDateUtc?`, `DeliveryDateUtc?`, `DeliveryStatusCode?` (≤80), `IsActive`, `Notes?` (≤2000), `ConcurrencyToken` (`PersonnelFileEmployee.cs:699-721`). |
| 3 | **Mapeo del requerimiento** | **6/6 campos cubiertos** (ver tabla de mapeo). Además se añaden **nivel de acceso** (`AccessLevelCode`) e **IsActive**. |
| 4 | **Normalización** | Strings con `Clean`/`CleanOptional`; fechas con `NormalizeDate` (`PersonnelFileEmployee.cs:688-696`). |
| 5 | **Validación de entrada** | `AssetAccessInputValidator` valida **solo** `AssetTypeCode` (NotEmpty ≤80) y `AssetOrAccessName` (NotEmpty ≤200) (`AssetAccess.cs:85-92`). **No** valida orden de fechas ni catálogos. |
| 6 | **Capa de aplicación** | CQRS completo: Add / Update (PUT) / Patch (JSON Patch RFC 6902) / Delete / GetList / GetById (`AssetAccess.cs:48-83`, `AssetAccess.Handlers.cs`). **No existe** `AssetAccess.Rules.cs` (módulo de reglas puro). |
| 7 | **Regla de estado** | Toda operación exige expediente **completado** (`IsCompletedEmployee`, `AssetAccess.Handlers.cs:46,113,193,293`); si no, `STATE_RULE_VIOLATION`. |
| 8 | **PUT vs PATCH** | `PUT` reemplaza campos de negocio y **preserva `IsActive`**; `IsActive` se muta **solo** vía `PATCH` (`AssetAccess.Handlers.cs:129`; controller `:1008`). |
| 9 | **API REST** | 6 endpoints `/api/v1/personnel-files/{publicId}/assets-accesses[/{assetAccessPublicId}]` (`Api/Controllers/PersonnelFileEmploymentController.cs:897-1069`); contratos en `Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs`. |
| 10 | **Permisos** | Escrituras vía `LoadForManageAsync` (**Manage** genérico); lecturas vía `LoadCompletedEmployeeForReadAsync` (**Read**). **No** hay permiso dedicado (`PersonnelFilePolicies.cs` solo expone `Read`, `Manage`, `ViewCompensation`). |
| 11 | **Concurrencia** | `ConcurrencyToken` + `If-Match`/`ETag` en todas las escrituras (`AssetAccess.Handlers.cs:124,204,304`). |
| 12 | **Auditoría** | Cada operación registra vía `PersonnelFileEmployeeAudits.LogUpdateAsync` (`AssetAccess.Handlers.cs:73,153,253,321`). Auditoría **por operación** (sin *diff* estructurado antes/después; igual que el resto de sub-recursos). |
| 13 | **Persistencia** | Tabla `personnel_file_assets_accesses`; índice `ix_…__tenant_file_start_active` sobre (`TenantId`, `PersonnelFileId`, `StartDateUtc`, `IsActive`); único por `PublicId` (`PersonnelFileEmployeeConfiguration.cs:280-313`). |
| 14 | **Pruebas** | Cobertura unitaria del aplicador de JSON Patch (`tests/CLARIHR.Application.UnitTests/PersonnelFileAssetAccessPatchTests.cs`). |

### Mapeo requerimiento → implementación (cobertura)

| Campo solicitado | Campo implementado | Tipo | Estado |
|---|---|---|---|
| **Equipo o acceso** | `AssetTypeCode` (clasificación) + `AssetOrAccessName` (nombre/descr.) | texto (≤80 / ≤200) | ✅ cubierto |
| **Fecha de alta** | `StartDateUtc` | fecha | ✅ cubierto |
| **Fecha de baja** | `EndDateUtc?` | fecha (opcional) | ✅ cubierto |
| **Observación** | `Notes?` | texto (≤2000) | ✅ cubierto |
| **Fecha de entrega** | `DeliveryDateUtc?` | fecha (opcional) | ✅ cubierto |
| **Estado de entrega** | `DeliveryStatusCode?` | texto (≤80) | ✅ cubierto |
| *Nivel de acceso* (mencionado en prosa) | `AccessLevelCode?` | texto (≤80) | ✅ cubierto (extra) |
| *(no solicitado)* Estado activo | `IsActive` | booleano | ✅ extra |

> **Conclusión del mapeo:** la implementación es un **superconjunto** de lo solicitado. **No falta ningún campo.**

---

## Brechas verificadas y su clasificación (GAP)

Las brechas se clasifican en **dos niveles** para respetar la instrucción del negocio ("si no es necesario, no agregar nada"):

> **Nivel A — Endurecimiento recomendado** (bajo costo, alinea con el patrón ya usado en el resto de CLARIHR; *no* cambia el alcance funcional).
> **Nivel B — Mejora de valor opcional / diferida** (amplía el alcance; se deja como **pregunta abierta** para que el negocio decida; por defecto **fuera de alcance**).

| # | Brecha (as-is) | Nivel | Resolución propuesta |
|---|---|---|---|
| **G-01** | Sin validación `StartDate ≤ EndDate` ni coherencia de `DeliveryDate`. El módulo de Sustituciones sí la tiene (`AuthorizationSubstitutions.cs:87`); este no. | **A** | Validar orden de fechas (RF-101). |
| **G-02** | `AssetTypeCode` es **texto libre**, pese a que el requerimiento enumera un conjunto cerrado (computadora, teléfono, uniforme, licencia, acceso a sistema). | **A** | Catálogo `asset-access-types` (RF-102). |
| **G-03** | `DeliveryStatusCode` es **texto libre**; "estado de entrega" es un enum natural (pendiente, entregado, devuelto…). | **A** | Catálogo `delivery-statuses` (RF-103). |
| **G-04** | `AccessLevelCode` es **texto libre**; el "nivel de acceso" varía por sistema. | **B** | Normalizar/catalogar opcional (RF-104) — por defecto se conserva texto libre. |
| **G-05** | `IsActive` es **manual**; un activo con `EndDate` pasada puede quedar `IsActive=true` (incoherencia). | **B** | Estado efectivo derivado de la vigencia (RF-105). |
| **G-06** | El modelo registra **entrega** (`DeliveryDate`/`DeliveryStatus`) pero **no la devolución** (fecha y **condición** del activo al regresar). Para activos físicos (laptop/uniforme), la devolución al egreso es un proceso HRIS distinto. | **B** | Ciclo de **devolución** + condición (RF-106) — **pregunta abierta P-03**. |
| **G-07** | Sin **identificación del activo**: número de serie / IMEI / etiqueta de inventario / cantidad / valor monetario (para descuento por pérdida). | **B** | Campos de identificación de activo (RF-107) — **pregunta abierta P-04**. |
| **G-08** | Sin **acta de entrega / responsiva firmada** ni adjunto documental (relevante legalmente para propiedad de la empresa). | **B** | Acuse/adjunto (RF-108) — **pregunta abierta P-05**. |
| **G-09** | Es **registro documental**: declarar un "acceso a sistema" **no provisiona/desprovisiona** acceso real (no hay integración IAM). | **B (diferido)** | Provisión real diferida a futuro módulo TI/IAM (RF-110). |
| **G-10** | Sin enlace al **egreso/finalización**: al dar de baja al empleado no se listan activos pendientes de devolver ni accesos por revocar. | **B (diferido)** | Integración con egreso (RF-109) — **pregunta abierta P-06**. |
| **G-11** | Escrituras con permiso **genérico** `Manage`; el registro de "accesos a sistemas y nivel de acceso" es información **sensible de seguridad**. | **B** | ¿Permiso dedicado? (RF-111) — **pregunta abierta P-07**. |

---

## Decisiones propuestas (PENDIENTES de ratificación del negocio)

> A diferencia de análisis previos ya ratificados, **aquí no hay decisiones cerradas todavía**. Se proponen como punto de partida; ver **§17 Preguntas abiertas**.

| # | Tema | Propuesta del analista |
|---|---|---|
| **DP-01** | ¿Se cierra el requerimiento con lo existente? | **Sí.** El requerimiento enunciado está cubierto; la Fase 1 (registro documental) se **da por cerrada**. |
| **DP-02** | ¿Endurecimiento Nivel A? | **Recomendado** (catálogos de tipo y de estado de entrega + validación de fechas), por **consistencia** con el resto de CLARIHR. Bajo esfuerzo. Si el negocio prefiere mantenerlo documental, **se omite y se cierra tal cual**. |
| **DP-03** | ¿Mejoras Nivel B? | **Fuera de alcance** por defecto. Se incorporan **solo** si el negocio responde afirmativamente a las preguntas abiertas P-03…P-07. |
| **DP-04** | Catálogo de tipo (seed SV) | `asset-access-types`: **EQUIPO_COMPUTO, TELEFONO_MOVIL, UNIFORME, LICENCIA_SOFTWARE, ACCESO_SISTEMA, MOBILIARIO, HERRAMIENTA, OTRO**. |
| **DP-05** | Catálogo de estado de entrega (seed SV) | `delivery-statuses`: **PENDIENTE, ENTREGADO, EN_USO, DEVUELTO, EXTRAVIADO, DAÑADO, NO_APLICA**. |

---

## Estado de implementación (Nivel A — 2026-06-22)

El negocio decidió **endurecer** (Nivel A). Implementado y verificado (build verde, **1852** pruebas unitarias en verde, incluidas las guardas de paridad de localización y de bijección de catálogos). El detalle técnico está en `docs/technical/plan-tecnico-equipo-acceso.md`. Resumen de cambios:

| RF | Qué se hizo | Dónde |
|---|---|---|
| **RF-102** | Catálogo `asset-access-types` (entidad `AssetAccessTypeCatalogItem`, config, `DbSet`, categoría, key-map, switches de lectura+validación, seed SV) | `Domain/GeneralCatalogs/GeneralCatalogItems.cs`, `Infrastructure/Persistence/Configurations/GeneralCatalogs/…`, `ApplicationDbContext.cs`, `Catalogs/PersonnelReferenceCatalogs.cs`, `Catalogs/GeneralCatalogKeyMap.cs`, `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs`, `Persistence/DevSeedService.cs` |
| **RF-103** | Catálogo `delivery-statuses` (entidad `DeliveryStatusCatalogItem` + mismas capas) | *(idem RF-102)* |
| **RF-101** | Coherencia de fechas: módulo de reglas puro `AssetAccessRules.ValidateDates` (alta ≤ baja; entrega ≥ alta) + *check constraints* en BD | `Employment/AssetAccess.Rules.cs`, `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs` |
| **RF-102/103/101** | `AssetAccessCommandSupport.ValidateAsync` (catálogos + fechas) invocado por los handlers Add/PUT/PATCH | `Employment/AssetAccess.Handlers.cs` |
| **Errores** | 4 códigos bilingües: `ASSET_ACCESS_TYPE_CODE_INVALID`, `ASSET_ACCESS_DELIVERY_STATUS_CODE_INVALID`, `ASSET_ACCESS_DATE_RANGE_INVALID`, `ASSET_ACCESS_DELIVERY_DATE_INVALID` (422) | `Localization/BackendMessages.resx` + `.es.resx` |
| **Migración** | 2 tablas de catálogo + 2 *check constraints* | `Migrations/20260622202650_AssetAccessCatalogsAndHardenAssetAccess.cs` |
| **Pruebas** | `AssetAccessRulesTests` (7 casos de coherencia de fechas) | `tests/CLARIHR.Application.UnitTests/AssetAccessRulesTests.cs` |

> **Endpoints de lectura para la UI:** `GET /api/v1/general-catalogs/asset-access-types?countryCode=SV` y `GET /api/v1/general-catalogs/delivery-statuses?countryCode=SV` (vía `GeneralCatalogsController`).
> **Nota de datos:** la validación de catálogo aplica en **escritura**; registros previos con tipo/estado de texto libre permanecen pero un nuevo guardado exige códigos de catálogo. `DevSeedService` no crea filas de equipo/acceso, por lo que las *check constraints* de fecha no chocan con datos sembrados.

---

## 1. Resumen del producto o requerimiento

Sección del expediente del empleado para **registrar el equipo y los accesos asignados** por la institución: activos físicos (computadora, teléfono, uniformes) y entitlements lógicos (licencias de software y accesos a sistemas con su **nivel de acceso**). Cada registro lleva **fecha de alta, fecha de baja, observación, fecha de entrega y estado de entrega**.

La funcionalidad **ya existe** (`PersonnelFileAssetAccess`) y **cubre íntegramente** el requerimiento. Este documento la **valida** contra el código y propone, **de forma opcional**, un **endurecimiento de consistencia** (catálogos + validación de fechas) y una lista de **mejoras de valor diferidas** que el negocio puede activar o descartar. Sin decisiones adicionales, **el requerimiento queda cerrado**.

---

## 2. Objetivos del negocio

- **O-1. Control de propiedad de la empresa.** Saber **qué activo/acceso** tiene cada empleado, desde cuándo (alta) y hasta cuándo (baja).
- **O-2. Trazabilidad de la entrega.** Registrar **cuándo** y en **qué estado** se entregó cada equipo (responsabilidad sobre el bien).
- **O-3. Visibilidad de accesos.** Documentar **a qué sistemas** y con **qué nivel** accede el empleado (insumo para revisiones de seguridad y para el egreso).
- **O-4. Integridad de datos.** Datos consistentes y comparables (catálogos en lugar de texto libre) y sin incoherencias de fechas.
- **O-5. Base para el egreso.** Dejar la información lista para que, al desvincular al empleado, se recuperen activos y se revoquen accesos (consumo futuro).

---

## 3. Alcance funcional

### Ya implementado (Fase 1 — **cerrado**)
- **F1.** Alta/edición/baja/consulta de equipos y accesos por expediente (CRUD vía API REST, 6 endpoints).
- **F2.** Captura de los 6 campos solicitados + nivel de acceso + estado activo.
- **F3.** Concurrencia optimista (`If-Match`/`ETag`), auditoría por operación, multi-tenant y *cascade delete* con el expediente.
- **F4.** Regla: solo expedientes **completados** pueden gestionar equipos/accesos.
- **F5.** Actualización parcial vía **JSON Patch** (RFC 6902).

### Propuesto — Nivel A (opcional, recomendado)
- **F6.** **Catálogo** `asset-access-types` para el tipo de equipo/acceso (RF-102).
- **F7.** **Catálogo** `delivery-statuses` para el estado de entrega (RF-103).
- **F8.** **Validación de coherencia de fechas** (alta ≤ baja; entrega coherente) (RF-101).

### Propuesto — Nivel B (opcional, sujeto a decisión)
- **F9.** Ciclo de **devolución** (fecha + condición) (RF-106).
- **F10.** **Identificación del activo** (serie/etiqueta/cantidad/valor) (RF-107).
- **F11.** **Acta de entrega / responsiva** y adjunto documental (RF-108).
- **F12.** Integración con **egreso** (pendientes de devolución / revocación) (RF-109).

---

## 4. Fuera de alcance

- **FA-1.** **Provisión/desprovisión real** de accesos a sistemas (integración **IAM/Active Directory/SSO**). El registro es **documental** (G-09, RF-110 diferido).
- **FA-2.** **Inventario/ITAM central** (catálogo maestro de activos con stock, depreciación, ubicación). Aquí el activo se describe **por empleado**.
- **FA-3.** **Flujo de aprobación** para asignación/retiro de equipos o accesos (no hay motor de workflow).
- **FA-4.** **Notificaciones** automáticas (a TI, al empleado, a seguridad).
- **FA-5.** **Firma electrónica** de la responsiva (más allá de adjuntar un documento, si se decide RF-108).
- **FA-6.** **Autoservicio** del empleado para registrar/ver sus propios equipos/accesos.
- **FA-7.** **Cálculo de descuentos** en planilla por pérdida/daño (solo se registraría el valor como dato, si se decide RF-107).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el proceso |
|---|---|
| **Analista / Gestor de RRHH** | Crea/edita/da de baja y consulta los equipos y accesos del expediente (permiso `Manage`). Actor principal. |
| **TI / Sistemas** *(potencial)* | Fuente de verdad de licencias y accesos a sistemas; hoy **no** es actor del sistema (registro lo hace RRHH). Posible consumidor/colaborador si se decide RF-110. |
| **Seguridad de la Información / Auditor** | Consulta qué accesos y niveles tiene el personal (lectura `Read`); revisa la bitácora. |
| **Empleado** | Sujeto de la asignación; **firma** la responsiva (fuera del sistema, salvo RF-108). **Sin** autoservicio. |
| **Responsable de Egreso** *(potencial)* | Al desvincular, recupera activos y revoca accesos (consumidor futuro, RF-109). |
| **Sistema — futuro IAM/Egreso** | (Diferido) Consumiría los registros de acceso para provisión/revocación real. |

---

## 6. Requerimientos funcionales

> **RF-000 (línea base — YA IMPLEMENTADO).** Gestión CRUD de equipos/accesos por expediente (Add/Update/Patch/Delete/GetList/GetById), con los 6 campos solicitados + nivel de acceso + estado activo, concurrencia `If-Match`, auditoría por operación, multi-tenant, *cascade delete* y regla de **expediente completado**. **Criterio:** se cumple y está en producción (`AssetAccess.cs`, `AssetAccess.Handlers.cs`, `PersonnelFileEmploymentController.cs:897-1069`). **Prioridad:** — (hecho).

### RF-101 — Validar coherencia de fechas *(Nivel A)*
**Descripción:** Validar que `StartDateUtc ≤ EndDateUtc` (cuando exista baja) y que `DeliveryDateUtc` sea coherente con el período (no anterior al alta).
**Reglas de negocio:** Una **fecha de baja** no puede ser anterior a la **fecha de alta**; la **fecha de entrega** no debería ser anterior al alta.
**Criterios de aceptación:**
- Dado `EndDateUtc < StartDateUtc`, cuando se guarda, entonces **422** `ASSET_ACCESS_DATE_RANGE_INVALID`.
- Dado `DeliveryDateUtc < StartDateUtc`, cuando se guarda, entonces **422** `ASSET_ACCESS_DELIVERY_DATE_INVALID` (o *warning* configurable).
**Prioridad:** Media · **Dependencias:** módulo de reglas (ver RNF).

### RF-102 — Catalogar el tipo de equipo/acceso *(Nivel A)*
**Descripción:** Sustituir `AssetTypeCode` de **texto libre** por el catálogo **`asset-access-types`** (país-scoped), validado como activo.
**Reglas de negocio:** El código **debe** existir y estar **activo**. **Seed SV (DP-04):** `EQUIPO_COMPUTO`, `TELEFONO_MOVIL`, `UNIFORME`, `LICENCIA_SOFTWARE`, `ACCESO_SISTEMA`, `MOBILIARIO`, `HERRAMIENTA`, `OTRO`.
**Criterios de aceptación:**
- Dado un código fuera de catálogo, entonces **422** `ASSET_TYPE_CODE_INVALID`.
- Dado un código válido, se acepta y persiste normalizado.
**Prioridad:** Media · **Dependencias:** `GeneralCatalogs` (patrón `CountryScopedCatalogItem`).

### RF-103 — Catalogar el estado de entrega *(Nivel A)*
**Descripción:** Sustituir `DeliveryStatusCode` (texto libre) por el catálogo **`delivery-statuses`**.
**Reglas de negocio:** **Seed SV (DP-05):** `PENDIENTE`, `ENTREGADO`, `EN_USO`, `DEVUELTO`, `EXTRAVIADO`, `DAÑADO`, `NO_APLICA`.
**Criterios de aceptación:**
- Dado un estado fuera de catálogo, entonces **422** `DELIVERY_STATUS_CODE_INVALID`.
- Para licencias/accesos donde "entrega" no aplica, se permite `NO_APLICA`.
**Prioridad:** Media · **Dependencias:** `GeneralCatalogs`.

### RF-104 — Nivel de acceso (normalización opcional) *(Nivel B)*
**Descripción:** Opcionalmente catalogar/normalizar `AccessLevelCode` (p. ej. `LECTURA`, `ESCRITURA`, `ADMINISTRADOR`), aceptando que el nivel **depende del sistema**.
**Reglas de negocio:** Por defecto **se conserva texto libre**; si se cataloga, sería por sistema/aplicación.
**Criterios de aceptación:** Si se implementa, un nivel fuera del catálogo aplicable produce **422**; si no, se acepta texto.
**Prioridad:** Baja · **Dependencias:** decisión P-08.

### RF-105 — Estado efectivo derivado de la vigencia *(Nivel B)*
**Descripción:** Exponer un estado **derivado** (`VIGENTE`/`DADO_DE_BAJA`) a partir de `EndDateUtc` vs. fecha actual, en lugar de depender solo del `IsActive` manual.
**Reglas de negocio:** `IsActive=true` con `EndDateUtc` pasada se marca como **inconsistencia** (o se impide).
**Criterios de aceptación:** Dado un registro con `EndDateUtc` pasada e `IsActive=true`, al consultarlo el estado efectivo es **DADO_DE_BAJA**.
**Prioridad:** Baja · **Dependencias:** RF-101.

### RF-106 — Ciclo de devolución (fecha + condición) *(Nivel B — pregunta abierta P-03)*
**Descripción:** Para activos físicos, registrar **fecha de devolución** y **condición** al devolver (`BUENO`, `REGULAR`, `DAÑADO`, `NO_DEVUELTO`), separadas de la **entrega**.
**Reglas de negocio:** Aplica a activos físicos; para licencias/accesos, "devolución" = revocación lógica.
**Criterios de aceptación:** Dado un activo con baja, se puede registrar su devolución y condición; el estado de entrega refleja `DEVUELTO`.
**Prioridad:** Baja · **Dependencias:** P-03.

### RF-107 — Identificación del activo *(Nivel B — pregunta abierta P-04)*
**Descripción:** Campos opcionales para activos físicos: **número de serie / IMEI**, **etiqueta de inventario**, **cantidad** (uniformes), **valor monetario**.
**Reglas de negocio:** Opcionales; el valor permite, a futuro, sustentar descuentos por pérdida (cálculo **fuera de alcance**).
**Criterios de aceptación:** Si se implementa, los campos se persisten y se muestran; sin ellos, el registro sigue siendo válido.
**Prioridad:** Baja · **Dependencias:** P-04.

### RF-108 — Acta de entrega / responsiva y adjunto *(Nivel B — pregunta abierta P-05)*
**Descripción:** Registrar el **acuse** de recepción del empleado y **adjuntar** el documento (responsiva escaneada).
**Reglas de negocio:** Refuerza la responsabilidad legal sobre la propiedad de la empresa.
**Criterios de aceptación:** Si se implementa, se puede adjuntar el documento y marcar el acuse; integra con el módulo documental si existe.
**Prioridad:** Baja · **Dependencias:** P-05; módulo de documentos/adjuntos.

### RF-109 — Integración con egreso/finalización *(Nivel B diferido — pregunta abierta P-06)*
**Descripción:** Al desvincular al empleado, **listar activos pendientes de devolución** y **accesos por revocar** como parte del *checklist* de egreso.
**Reglas de negocio:** No bloquea el egreso; lo **informa**. Reusa la baja (`EndDateUtc`) y el estado de entrega/devolución.
**Criterios de aceptación:** Dado un empleado en proceso de baja, el sistema muestra sus equipos/accesos activos no devueltos.
**Prioridad:** Diferido · **Dependencias:** módulo de Egreso/Finalización; P-06.

### RF-110 — (Diferido) Provisión/desprovisión real de accesos *(Nivel B diferido)*
**Descripción:** Cuando exista integración **IAM/SSO**, el registro de "acceso a sistema" podría **disparar** la provisión/revocación real.
**Reglas de negocio:** Hoy el registro es **documental** (no concede ni revoca acceso técnico).
**Criterios de aceptación:** (Futuro) Contrato de sincronización con el proveedor de identidad.
**Prioridad:** Diferido · **Dependencias:** futuro módulo TI/IAM.

### RF-111 — Permiso dedicado de gestión *(Nivel B — pregunta abierta P-07)*
**Descripción:** Evaluar un permiso **dedicado** (p. ej. `PersonnelFiles.ManageAssetsAccesses`) por la **sensibilidad de seguridad** del registro de accesos, separado del `Manage` genérico.
**Reglas de negocio:** Seguiría el patrón de `ViewCompensation`. Por defecto **se mantiene** `Manage` genérico salvo decisión contraria.
**Criterios de aceptación:** Si se implementa, sin el permiso → **403**.
**Prioridad:** Baja · **Dependencias:** Provisioning/`IdentityAccess`; P-07.

---

## 7. Requerimientos no funcionales

- **Seguridad / Multi-tenant.** Toda operación filtrada por **tenant** (ya implementado). El registro de accesos/niveles es **sensible**: considerar permiso dedicado (RF-111) y trazar lecturas si lo pide cumplimiento.
- **Integridad / Consistencia.** De adoptarse el Nivel A, alojar las reglas (fechas, catálogos) en un **módulo de reglas puro** `AssetAccess.Rules.cs` **unit-testeable** (patrón de `EmploymentAssignments.Rules.cs`), hoy inexistente.
- **Concurrencia.** `ConcurrencyToken` + `If-Match`/`ETag` (ya implementado).
- **Auditoría.** Por operación (ya implementado). Si cumplimiento lo exige, ampliar a *diff* antes/después e historial visible.
- **Rendimiento.** Consulta por expediente indexada (`ix_…__tenant_file_start_active`) — adecuada para listados por empleado.
- **Usabilidad.** Selección por catálogo (tipo/estado) en lugar de texto libre; señalización del estado efectivo (vigente/baja).
- **Compatibilidad / API.** Los 6 endpoints se conservan; el Nivel A es retro-compatible salvo el rechazo de datos inválidos (aceptable). Catalogar campos hoy libres es **breaking** solo para datos sucios → requiere migración/normalización.
- **Localización.** Mensajes/errores **bilingües (ES/EN)** con código estable; **test de paridad de localización** (convención de la casa).
- **Mantenibilidad.** Tests de reglas si se adopta el Nivel A.

---

## 8. Historias de usuario

### HU-001 — Registrar un equipo entregado al empleado *(ya soportado)*
Como **Analista de RRHH**, quiero **registrar un equipo asignado (p. ej. laptop) con su fecha de alta y de entrega**, para **dejar constancia de la propiedad bajo responsabilidad del empleado**.
- Dado un expediente **completado**, cuando registro tipo, nombre, fecha de alta, fecha de entrega y estado de entrega, entonces se guarda y queda auditado.
- Dado un expediente **no completado**, entonces **bloquea** (`STATE_RULE_VIOLATION`).

### HU-002 — Registrar un acceso a sistema con su nivel *(ya soportado)*
Como **Analista de RRHH**, quiero **registrar el acceso a un sistema y su nivel de acceso**, para **documentar los entitlements del empleado**.
- Dado un acceso lógico, cuando indico el sistema y el nivel, entonces se guarda (estado de entrega puede ser `NO_APLICA`).

### HU-003 — Dar de baja un equipo/acceso *(ya soportado)*
Como **Analista de RRHH**, quiero **registrar la fecha de baja de un equipo/acceso**, para **reflejar que el empleado ya no lo posee/usa**.
- Dado un registro vigente, cuando indico la fecha de baja, entonces deja de estar vigente.

### HU-004 — Estandarizar tipos y estados *(Nivel A)*
Como **Analista de RRHH**, quiero **elegir el tipo y el estado de entrega de una lista**, para **mantener datos consistentes y reportables**.
- Dado el catálogo, cuando selecciono "UNIFORME"/"ENTREGADO", entonces se acepta; un valor fuera de catálogo **se rechaza** (RF-102/103).

### HU-005 — Evitar fechas incoherentes *(Nivel A)*
Como **Analista de RRHH**, quiero que **el sistema rechace una baja anterior al alta**, para **evitar datos erróneos**.
- Dado `EndDate < StartDate`, cuando guardo, entonces **bloquea** (RF-101).

### HU-006 — Ver pendientes al egreso *(Nivel B / diferido)*
Como **Responsable de Egreso**, quiero **ver los equipos no devueltos y accesos activos del empleado que se va**, para **recuperarlos y revocarlos**.
- Dado un empleado en baja, cuando abro su egreso, entonces veo sus activos/accesos pendientes (RF-109).

---

## 9. Reglas de negocio (consolidadas)

- **RN-01.** Solo expedientes **completados** (`IsCompletedEmployee`) pueden gestionar equipos/accesos *(ya implementado)*.
- **RN-02.** `AssetTypeCode` y `AssetOrAccessName` son **obligatorios** *(ya implementado)*.
- **RN-03.** `IsActive` se muta **solo** vía `PATCH`; el `PUT` lo **preserva** *(ya implementado)*.
- **RN-04.** Toda escritura exige **If-Match** y queda **auditada** *(ya implementado)*.
- **RN-05.** Al **eliminar el expediente**, sus equipos/accesos se eliminan en **cascada** *(ya implementado)*.
- **RN-06.** *(Nivel A)* `StartDate ≤ EndDate`; `DeliveryDate` coherente con el período (RF-101).
- **RN-07.** *(Nivel A)* `AssetTypeCode` ∈ catálogo `asset-access-types` activo (RF-102).
- **RN-08.** *(Nivel A)* `DeliveryStatusCode` ∈ catálogo `delivery-statuses` activo; `NO_APLICA` para accesos/licencias (RF-103).
- **RN-09.** *(Nivel B)* El **estado efectivo** se deriva de la vigencia; `IsActive=true` con baja pasada es inconsistente (RF-105).
- **RN-10.** El registro es **documental**: declarar un acceso **no** concede acceso técnico real (RF-110 diferido).

---

## 10. Flujos principales

**Flujo: Registrar equipo/acceso (RRHH) — ya soportado**
1. RRHH abre el expediente del empleado (debe estar **completado**).
2. Entra a **"Equipo o acceso"** → **Agregar**.
3. Indica **tipo** (catálogo si Nivel A), **nombre/descripción** y, si aplica, **nivel de acceso**.
4. Ingresa **fecha de alta**, **fecha de entrega** y **estado de entrega**; agrega **observación** si corresponde.
5. (Nivel A) El sistema valida **catálogos** y **coherencia de fechas**.
6. Guarda → persiste, **audita**, devuelve `ETag`.

**Flujo: Dar de baja un equipo/acceso — ya soportado**
1. RRHH abre el registro → indica **fecha de baja** (PUT) y/o cambia `IsActive` (PATCH).
2. El sistema valida concurrencia (`If-Match`) y guarda.

**Flujo: Consultar equipos/accesos de un empleado — ya soportado**
1. RRHH/Seguridad consulta la lista del expediente (GET); cada ítem trae su `concurrencyToken`.

**Flujo: Egreso (diferido)**
1. Al iniciar la baja del empleado, el sistema **lista** equipos no devueltos y accesos vigentes para su recuperación/revocación (RF-109).

---

## 11. Flujos alternativos y excepciones

| # | Escenario | Resultado esperado |
|---|---|---|
| **E1** | Expediente **no completado**. | **Bloqueo** `STATE_RULE_VIOLATION` *(ya implementado — `AssetAccess.Handlers.cs:46`)*. |
| **E2** | `AssetTypeCode` o `AssetOrAccessName` vacío. | **422** validación *(ya implementado — `AssetAccess.cs:89-90`)*. |
| **E3** | `If-Match`/`ConcurrencyToken` no coincide. | **409 CONFLICT** *(ya implementado — `:124,204,304`)*. |
| **E4** | Registro inexistente (id incorrecto). | **404/422** `ITEM_NOT_FOUND` *(ya implementado — `:121,199,301`)*. |
| **E5** | `EndDate < StartDate`. | *(Nivel A)* **422** `ASSET_ACCESS_DATE_RANGE_INVALID` (RF-101). |
| **E6** | Tipo/estado fuera de catálogo. | *(Nivel A)* **422** `ASSET_TYPE_CODE_INVALID` / `DELIVERY_STATUS_CODE_INVALID` (RF-102/103). |
| **E7** | Acceso/licencia sin entrega física. | Estado de entrega `NO_APLICA` (RF-103). |
| **E8** | `IsActive=true` con baja pasada. | *(Nivel B)* Estado efectivo **DADO_DE_BAJA**; señalar inconsistencia (RF-105). |
| **E9** | JSON Patch a ruta/propiedad no soportada. | **422** validación de patch *(ya implementado — `AssetAccess.Handlers.cs:409,510`)*. |

---

## 12. Datos requeridos

### Entidad: `PersonnelFileAssetAccess` *(YA EXISTE — `Domain/PersonnelFiles/PersonnelFileEmployee.cs:669`)*

| Campo | Tipo | Obligatorio | Validaciones | Estado | Descripción |
|---|---|---|---|---|---|
| `publicId` | GUID | Sí | único | ✅ existe | Identidad pública |
| `personnelFileId` | long (FK) | Sí | del tenant; *cascade* | ✅ existe | Expediente dueño |
| `assetTypeCode` | Texto (≤80) → **catálogo** | Sí | NotEmpty; *(A)* catálogo `asset-access-types` | 🔧 catalogar (RF-102) | **Equipo o acceso** (clasificación) |
| `assetOrAccessName` | Texto (≤200) | Sí | NotEmpty | ✅ existe | Nombre/descripción del equipo/acceso |
| `accessLevelCode` | Texto (≤80) | No | *(B)* normalizar opcional | 🟡 opcional (RF-104) | **Nivel de acceso** |
| `startDateUtc` | Fecha | Sí | *(A)* `≤ endDate` | 🔧 validar (RF-101) | **Fecha de alta** |
| `endDateUtc` | Fecha | No | *(A)* `≥ startDate` | 🔧 validar (RF-101) | **Fecha de baja** |
| `deliveryDateUtc` | Fecha | No | *(A)* coherente con período | 🔧 validar (RF-101) | **Fecha de entrega** |
| `deliveryStatusCode` | Texto (≤80) → **catálogo** | No | *(A)* catálogo `delivery-statuses` | 🔧 catalogar (RF-103) | **Estado de entrega** |
| `isActive` | Booleano | Sí | *(B)* coherente con vigencia | 🟡 derivar (RF-105) | Estado activo (muta solo por PATCH) |
| `notes` | Texto (≤2000) | No | — | ✅ existe | **Observación** |
| `concurrencyToken` | GUID | Sí | If-Match | ✅ existe | Concurrencia |

### Entidad: `AssetAccessTypeCatalogItem` *(nueva — RF-102 / DP-04, si se adopta Nivel A)*

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `publicId` | GUID | Sí | único | Identidad |
| `countryCode` | Texto | Sí | catálogo de países | País (patrón `CountryScopedCatalogItem`) |
| `code` | Texto | Sí | único por país | EQUIPO_COMPUTO, TELEFONO_MOVIL, UNIFORME, LICENCIA_SOFTWARE, ACCESO_SISTEMA, MOBILIARIO, HERRAMIENTA, OTRO |
| `name` | Texto | Sí | — | Nombre visible (ES/EN) |
| `isActive` | Booleano | Sí | — | Estado |
| `sortOrder` | Número | No | — | Orden de despliegue |

### Entidad: `DeliveryStatusCatalogItem` *(nueva — RF-103 / DP-05, si se adopta Nivel A)*

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| `publicId` | GUID | Sí | único | Identidad |
| `countryCode` | Texto | Sí | catálogo de países | País |
| `code` | Texto | Sí | único por país | PENDIENTE, ENTREGADO, EN_USO, DEVUELTO, EXTRAVIADO, DAÑADO, NO_APLICA |
| `name` | Texto | Sí | — | Nombre visible (ES/EN) |
| `isActive` | Booleano | Sí | — | Estado |
| `sortOrder` | Número | No | — | Orden de despliegue |

> **Campos potenciales (Nivel B, solo si se aprueban):** `returnDateUtc`, `returnConditionCode` (RF-106); `serialNumber`, `assetTag`, `quantity`, `monetaryValue` (RF-107); `acknowledgedByEmployee`, `documentAttachmentId` (RF-108).

---

## 13. Integraciones necesarias

- **Catálogos generales (`GeneralCatalogs`).** Nuevos `asset-access-types` y `delivery-statuses` (país-scoped, seed SV) **si** se adopta el Nivel A. Interno.
- **Auditoría (`IAuditService`).** Ya integrada (por operación).
- **Identidad/Provisioning (`IdentityAccess`).** Solo si se decide el permiso dedicado (RF-111).
- **Módulo documental / adjuntos.** Solo si se decide el acta/responsiva (RF-108).
- **Futuro módulo de Egreso/Finalización.** Consumidor de pendientes (RF-109, diferido).
- **Futuro IAM/SSO/Directorio.** Provisión/revocación real (RF-110, **fuera de alcance**).
- **Notificaciones / Inventario ITAM:** **no aplican** en esta fase.

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **RRHH / Administrador de Expedientes** | Crear/editar/baja/eliminar y leer equipos/accesos (**`PersonnelFiles.Manage`** + `Read`). | Solo expedientes **completados**; solo su **tenant**. |
| **Consulta / Seguridad / Auditor** | Leer equipos/accesos (**`PersonnelFiles.Read`**). | Sin escritura. |
| **Empleado** | — | **Sin** autoservicio en esta fase. |
| **Sistema (Egreso/IAM, diferido)** | Consumir registros (lectura). | Solo lectura del contrato. |

> **Estado actual:** **no** existe permiso dedicado; las escrituras usan `Manage` genérico (`PersonnelFilePolicies.cs`). La conveniencia de un permiso específico `PersonnelFiles.ManageAssetsAccesses` queda como **pregunta abierta P-07** (RF-111), dada la sensibilidad de los registros de acceso.

---

## 15. Criterios de aceptación generales

- ✅ **El requerimiento enunciado está cubierto**: los 6 campos (equipo/acceso, fecha de alta, fecha de baja, observación, fecha de entrega, estado de entrega) se capturan, además de nivel de acceso (RF-000).
- ✅ CRUD completo, concurrencia `If-Match`, auditoría, multi-tenant y *cascade delete* operativos.
- ✅ Solo expedientes **completados** gestionan equipos/accesos.
- 🟨 *(Si se adopta Nivel A)* Tipo y estado de entrega provienen de **catálogo**; las **fechas** son coherentes (alta ≤ baja).
- 🟨 *(Si se adopta Nivel A)* Reglas en **módulo puro** con **tests unitarios** y **paridad de localización (ES/EN)**.
- ⬜ *(Nivel B)* Devolución/condición, identificación del activo, acta/adjunto e integración con egreso **solo** si el negocio los aprueba.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R1. Sobre-construcción.** Agregar ITAM/serie/valor/devolución sin necesidad real complica el módulo. **Mitigación:** mantenerlos como **Nivel B** opcional; por defecto, cerrar con lo existente.
- **R2. Datos sucios.** Si se catalogan campos hoy libres (tipo/estado), los registros existentes con valores arbitrarios romperían la validación. **Mitigación:** normalización/migración previa (mapear valores actuales a códigos).
- **R3. Falsa sensación de control de accesos.** "Acceso a sistema" es **documental**; no garantiza que el acceso técnico exista/esté revocado. **Mitigación:** comunicar la naturaleza documental (G-09) y planear RF-110 a futuro.
- **R4. Cumplimiento.** Registros de acceso/nivel son sensibles; sin permiso dedicado ni *diff* de auditoría podría haber observaciones de auditoría. **Mitigación:** evaluar RF-111 y ampliar auditoría si se requiere.

### Supuestos
- **S1.** El requerimiento se refiere a un **registro por empleado** (no a un inventario central/ITAM).
- **S2.** El registro es **documental** (no provisiona/revoca acceso técnico).
- **S3.** País de referencia **SV**; catálogos (si se crean) se siembran para SV.
- **S4.** No hay decisiones de negocio ratificadas aún para los niveles A/B (este documento las propone).

### Dependencias
- **D1.** `GeneralCatalogs` (si Nivel A).
- **D2.** `IdentityAccess`/Provisioning (si RF-111).
- **D3.** Módulo documental (si RF-108).
- **D4.** (Diferido) Módulos de **Egreso/Finalización** e **IAM** (RF-109/110).

---

## 17. Preguntas abiertas para el cliente o stakeholders

- **P-01.** ¿Se considera **cerrado** el requerimiento con la funcionalidad existente (registro documental con los 6 campos)? *(Propuesta: **Sí**.)*
- **P-02.** ¿Se desea el **endurecimiento Nivel A** (catálogos de tipo y estado de entrega + validación de fechas) por consistencia con el resto del sistema? *(Propuesta: **Sí**, bajo esfuerzo.)*
- **P-03.** ¿Se requiere registrar la **devolución** del activo (fecha + **condición** al devolver), distinta de la entrega? (RF-106)
- **P-04.** ¿Se requiere **identificar** el activo (número de serie/IMEI, etiqueta de inventario, **cantidad**, **valor monetario**)? (RF-107)
- **P-05.** ¿Se requiere **acta de entrega/responsiva** firmada y/o **adjuntar** el documento? (RF-108)
- **P-06.** ¿Debe integrarse con el **egreso** para listar pendientes de devolución y accesos por revocar? (RF-109)
- **P-07.** ¿Se desea un **permiso dedicado** para gestionar equipos/accesos, dada su sensibilidad? (RF-111)
- **P-08.** ¿El **nivel de acceso** debe catalogarse (y por sistema) o permanecer como texto libre? (RF-104)
- **P-09.** ¿Existe **integración futura con TI/IAM** prevista, para dimensionar RF-110?

---

## 18. Recomendaciones del Analista de Negocio

1. **Cerrar la Fase 1 (registro documental):** el requerimiento, **tal como fue enunciado, ya está implementado y alineado** (`PersonnelFileAssetAccess`, 6/6 campos + nivel de acceso). **No se requiere desarrollo para cumplirlo.** Esta es la respuesta directa a "si no es necesario, no agregar nada y dar por cerrado".

2. **Endurecimiento mínimo recomendado (Nivel A) — opcional, alto valor/bajo costo:** por **consistencia** con el resto de CLARIHR, convertir `AssetTypeCode` y `DeliveryStatusCode` a **catálogos** (`asset-access-types`, `delivery-statuses`) y agregar **validación de fechas** (alta ≤ baja). Esto mejora calidad de datos y reportabilidad sin ampliar el alcance. Si el negocio prefiere mantenerlo documental, **se omite y se cierra tal cual**.

3. **No sobre-construir (Nivel B) salvo necesidad explícita:** devolución/condición, serie/etiqueta/valor, acta/responsiva e integración con egreso/IAM son **valiosos en HRIS maduros**, pero **no fueron solicitados**. Dejarlos como **preguntas abiertas** (P-03…P-07) y activarlos **solo** por decisión del negocio, idealmente en una **Fase 2** separada.

4. **Si se adopta el Nivel A**, extraer un **módulo de reglas puro** `AssetAccess.Rules.cs` (patrón `EmploymentAssignments.Rules.cs`) con **tests unitarios** y **paridad de localización (ES/EN)**, y planear la **normalización/migración** de datos existentes hacia los nuevos catálogos.

5. **Comunicar la naturaleza documental** del registro de accesos (no provisiona ni revoca acceso técnico) para evitar la brecha de expectativa (G-09); reservar la provisión real (RF-110) para una integración futura con IAM.

---

> **Naturaleza del documento.** Análisis de **validación + brechas (GAP)** sobre funcionalidad **ya implementada** (`PersonnelFileAssetAccess`). El "estado as-is" está **verificado contra el código** (referencias `archivo:línea`). **Resultado:** el requerimiento **está cubierto** y la **Fase 1 se da por cerrada**; el **endurecimiento Nivel A** (catálogos + validación de fechas) se **recomienda como opcional** y las **mejoras Nivel B** quedan como **preguntas abiertas** sin decisión. **No hay decisiones de negocio ratificadas aún** (P-01…P-09 pendientes).
