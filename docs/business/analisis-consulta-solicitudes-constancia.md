# Análisis de Negocio — Solicitud y Consulta de Constancias (laboral / de salario / para embajada)

> **Tipo de documento:** Análisis de requerimiento (validación + GAP) y especificación funcional.
> **Módulo:** Expediente de Personal → Solicitudes / Autoservicio (`PersonnelFiles`).
> **Fecha:** 2026-06-28 · **Versión:** v2.0 (decisiones **D-01…D-20 RATIFICADAS 2026-06-28**).
> **Autor:** Analista de Negocio (CLARIHR).
> **Estado:** Requerimiento de negocio **cerrado**; listo para plan técnico.

---

## 0. Veredicto ejecutivo (resultado de la validación)

El requerimiento pedía: una opción para **consultar / revisar las solicitudes de constancia** —**constancia de salario**, **constancia de trabajo (laboral)** y **constancia para embajada**— que se han **ingresado en el módulo Solicitudes del portal**; mostrarlas en un **listado**, poder **ver el detalle** de cada una y **exportar el listado a Excel**. Además: validar el alineamiento con lo ya implementado, **parametrizar catálogos**, **reutilizar servicios accedidos por key** y **hacer el seed inicial por país (El Salvador)**.

**Resultado: el requerimiento NO está implementado. Es desarrollo nuevo (net-new).**

Búsqueda exhaustiva en `src/` y `docs/`: **no existe** ninguna entidad, catálogo, endpoint ni migración para "constancia", "certificate", "constancia de salario/trabajo/embajada", "employment letter", "carta laboral" ni "verification letter". El "**módulo Solicitudes del portal**" **no es un contenedor genérico** en el backend: hoy se materializa como **dos solicitudes de autoservicio independientes** ya construidas —`PersonnelFileMedicalClaim` (reclamos de seguro médico) y `PersonnelFileEconomicAidRequest` (ayuda económica, construida el **2026-06-28**)—. La **solicitud de constancia es un nuevo integrante de esa familia**, no una validación de algo existente.

> **Consecuencia directa:** la "consulta de solicitudes de constancia" **no puede validarse ni mostrar nada** porque el modelo que alimenta esa consulta (la solicitud de constancia) **no existe todavía**. La consulta es inseparable de su modelo de datos y del canal de ingreso que lo puebla.

### ⚠️ Hallazgo de alineación 1 — la "constancia" entrega un DOCUMENTO, no dinero

Existen módulos de "solicitud" con forma parecida que **NO deben confundirse**. La diferencia clave es **qué se entrega al final**:

| Solicitud (ya existe) | ¿Quién la crea? | ¿Qué resuelve RR. HH.? | Resultado entregado | ¿Reutilizar? |
|---|---|---|---|---|
| `PersonnelFileMedicalClaim` | Empleado (autoservicio) | Estado + monto pagado por la aseguradora | Reembolso (dinero) | **No** (plantilla de comportamiento) |
| `PersonnelFileEconomicAidRequest` | Empleado (autoservicio) | Aprobar/rechazar **monto** + desembolso | Ayuda económica (dinero) | **No** (plantilla estructural — la **más cercana**) |
| **Solicitud de constancia (net-new)** | **Empleado (autoservicio)** | **Emitir** o **rechazar** la constancia | **Un documento** (constancia/carta **generada en PDF**) | **Net-new** |

**Conclusión de alineación:** la constancia es un **flujo entrante** (el empleado **pide un documento**, RR. HH. lo **emite**) inexistente hoy. Se construye como **nuevo integrante de la familia de "Solicitudes"**, **clonando el esqueleto** de `EconomicAidRequest` (autoservicio + catálogo tipo/estado + acción de resolución + adjuntos + controlador dedicado + seed SV + validación de catálogo por key) **PERO SIN su maquinaria financiera**: sin **monto/moneda**, sin **desembolso** y **sin motor de aprobación** (ver Hallazgo 2). La "resolución" aquí es **generar/emitir y entregar el documento**.

### ⚠️ Hallazgo de alineación 2 — NO se requiere motor de aprobación (disciplina de alcance)

`EconomicAidRequest` dejó el modelo **preparado para un flujo de aprobación futuro** (porque otorgar dinero es una decisión sensible). **Emitir una constancia laboral es una tarea operativa de rutina, de bajo riesgo**, no una decisión que requiera aprobación multinivel. Por tanto **NO se replica** la forward-compatibility de aprobaciones: el ciclo de vida es **lineal y simple** (Solicitada → En proceso → Emitida → Entregada, con Rechazada/Anulada).

### ⚠️ Hallazgo de alineación 3 — la CONSULTA es una BANDEJA cross-empleado (no la lista por expediente)

El entregable que **da nombre** al requerimiento ("consulta de solicitudes... en forma de listado... exportar a Excel") es una **bandeja a nivel de empresa**: RR. HH. revisa **todas** las solicitudes de **todos** los empleados, las **filtra**, las **pagina** y las **exporta**. Esto **NO** es la lista por expediente que exponen `MedicalClaims`/`EconomicAid` (`GET /personnel-files/{id}/...`); es una **consulta tipo reporte** (patrón `PersonnelFileReporting` con *dynamic-query* + `ReportExportDeliveryService`), **company-scoped**. El **ingreso** (autoservicio) sigue siendo por expediente; la **consulta** es transversal.

### ⚠️ Hallazgo de alineación 4 — la GENERACIÓN del PDF es factible reutilizando lo existente (no hay motor de plantillas)

Tras la ratificación, la **generación automática del documento entra en Fase 1** (D-15). El sistema **ya tiene QuestPDF** (`2024.12.3`) y una **arquitectura de render reutilizable**: `DocumentModel` (AST) + `IDocumentModelRenderer` / `QuestPdfDocumentRenderer`, con el ejemplo `JobProfilePdfRenderer` + `IJobProfileDocumentMapper`. **Todas las fuentes de datos del merge existen** (ver §13): **salario** en `PersonnelFileCompensationConcept` del assignment activo, **cargo** en `JobProfile.Title` (o `PositionSlot.Title`), **antigüedad** computada (`EmployeeSeniority.Between`), **identidad** en `PersonnelFileIdentification`, **fecha de ingreso** en `PersonnelFileEmployeeProfile.HireDate`. **NO existe** ningún motor de campos de fusión editables (Scriban/Handlebars/Razor) → se descartó (D-15/P-11) en favor de **layout estructurado por tipo (definido en código)** con **encabezado/firmante/pie configurables por empresa** (D-17). **Implicación de seguridad:** imprimir el salario expone datos de compensación → las constancias que imprimen salario requieren además el permiso **`ViewCompensation`** (D-20).

### 0.1. ¿Es necesario hacer algo? — Sí (y el alcance se AMPLIÓ en la ratificación)

El requerimiento indicaba: *"si no es necesario hacer nada más, no incluir nada y dar por cerrado"*. El módulo **no existe** y resuelve un proceso real y muy común de RR. HH., por lo que **sí es necesario construirlo**. En la **ratificación (2026-06-28)** el negocio **amplió** el alcance respecto al MVP inicialmente propuesto:

- **(P-01)** Se construye el **slice vertical completo** (ingreso autoservicio + procesamiento/emisión + consulta/bandeja + export). **D-01.**
- **(P-02 + P-11)** La **generación automática del PDF** entra en **Fase 1**, mediante **layout en código por tipo** (no motor de merge-fields editable). **D-15/D-17.**
- **(P-07)** **Medio de entrega** y **propósito** se modelan como **catálogos country-scoped con seed SV** (no texto simple). **D-18.**
- **(P-03)** El **seed SV de tipos** se **amplía** más allá de los tres nombrados. **D-19.**

Se mantiene la **disciplina de alcance** en lo que NO aporta al objetivo: **sin** motor de aprobación, **sin** motor de plantillas editables, **sin** firma electrónica/folio-QR, **sin** notificaciones, **sin** cobro (ver §4).

---

## 1. Resumen del producto o requerimiento

Se incorpora al **Expediente de Personal / portal de autoservicio** la capacidad de que un **empleado solicite una constancia** (de salario, de trabajo/laboral, para embajada, de tiempo laborado, de no descuento, carta de recomendación) y que **RR. HH. revise, procese, GENERE y emita** esa constancia en **PDF**, manteniendo **trazabilidad** de quién la pidió, para qué, en qué estado está, quién la emitió y cuándo se entregó. El entregable que da nombre al requerimiento es la **consulta/bandeja**: un **listado** transversal de todas las solicitudes, con **detalle** por solicitud y **exportación a Excel**.

El módulo tiene **cinco piezas**:

1. **Solicitud de constancia** (operativo, autoservicio): el empleado registra el **tipo**, el **propósito** (catálogo) y el **destinatario** ("dirigida a…"), el **medio de entrega** (catálogo), opcionalmente **fecha requerida**, **idioma** y **número de copias**. RR. HH. también puede registrarla **en su nombre**.
2. **Procesamiento y emisión** (operativo, interno RR. HH.): RR. HH. cambia el **estado** (en proceso → emitida → entregada, o rechazada); al emitir, el sistema **genera el PDF** y lo deja como documento emitido; registra **actor y fecha**.
3. **Generación del documento** (nuevo): el sistema arma la constancia en **PDF** por **tipo** (layout en código, QuestPDF/`DocumentModel`), fusionando datos del expediente (nombre, documento, cargo, antigüedad, fecha de ingreso, **salario** cuando aplica) con la **configuración de la empresa** (membrete/logo, ciudad, firmante, pie/texto legal).
4. **Consulta / bandeja** (núcleo del requerimiento): **listado company-scoped** con **filtros** (tipo, estado, propósito, rango de fechas, empleado, unidad organizativa), **paginación**, **detalle** y **exportación a Excel/CSV**.
5. **Catálogos parametrizables** (configuración): **tipos de constancia**, **estados**, **medio de entrega** y **propósito**, *country-scoped* y con **seed inicial para El Salvador**.

**Problema que resuelve:** hoy no existe un canal estructurado, documentado y auditable para que el empleado solicite constancias ni para que RR. HH. las gestione y emita. Centralizarlo da **trazabilidad**, **tiempos de respuesta** medibles, **respaldo documental** del PDF emitido, **consistencia** del documento (layout estandarizado por tipo) y una **bandeja única** de trabajo para RR. HH.

---

## 2. Objetivos del negocio

- **OB-01.** Ofrecer al empleado un **canal de autoservicio** para solicitar constancias.
- **OB-02.** Dar a RR. HH. una **bandeja única** para **revisar, procesar y emitir** las constancias, con **listado, detalle y exportación a Excel**.
- **OB-03.** **Estandarizar** tipos, estados, medios de entrega y propósitos mediante **catálogos administrables**, *country-scoped* y con **seed SV**.
- **OB-04.** Garantizar **trazabilidad y auditoría** completas (solicitud, emisión, entrega, adjuntos) con concurrencia optimista.
- **OB-05.** Medir el **tiempo de respuesta** (solicitud → emisión) para gestionar SLA de RR. HH.
- **OB-06.** **Reutilizar las convenciones y servicios existentes** (catálogos validados por key, autoservicio, adjuntos, auditoría, **motor de exportación**, **render PDF QuestPDF/`DocumentModel`**) para minimizar costo y riesgo.
- **OB-07.** **Generar la constancia en PDF** de forma **consistente y estandarizada por tipo**, con **encabezado/firmante/pie configurables por empresa**.
- **OB-08.** Mantener **disciplina de alcance**: **no** construir motor de aprobación, **no** motor de plantillas editables, **no** firma electrónica en Fase 1.
- **OB-09.** **Proteger los datos de compensación**: las constancias que imprimen salario solo se generan con permiso **`ViewCompensation`**.

---

## 3. Alcance funcional

Incluye (Fase 1):

- **AF-01.** CRUD del **catálogo de tipos de constancia** (Code + Descripción), *country-scoped*, con **seed SV ampliado** (RF-001).
- **AF-02.** Lectura/administración del **catálogo de estados** de la solicitud, *country-scoped*, con **seed SV** (RF-002).
- **AF-03.** **Solicitar** constancia (autoservicio del empleado) o registrarla **en su nombre** (RR. HH.): tipo, propósito (catálogo), destinatario ("dirigida a"), medio de entrega (catálogo), fecha de solicitud, (opcional) fecha requerida, idioma, copias.
- **AF-04.** **Consulta / bandeja company-scoped** (núcleo): **listar / filtrar / paginar** todas las solicitudes; el empleado ve **solo las suyas**, RR. HH. ve **todas**.
- **AF-05.** **Ver el detalle** de una solicitud (datos + procesamiento + documento emitido).
- **AF-06.** **Exportar a Excel/CSV** el listado filtrado (requisito explícito).
- **AF-07.** **Procesar / emitir** por RR. HH.: cambiar estado, **generar el PDF** y registrar actor/fecha/observaciones.
- **AF-08.** **Documento emitido**: principalmente **generado por el sistema** (PDF); con **carga manual opcional** (override/escaneo firmado). Listar, descargar (URL temporal) y reemplazar.
- **AF-09.** **Marcar entregada** (`ENTREGADA`) con fecha y **medio de entrega**.
- **AF-10.** **Editar / dar de baja** (baja lógica) por RR. HH.; **cancelar/retirar** la propia solicitud aún pendiente por el empleado.
- **AF-11.** **Validaciones:** tipo/propósito/medio/estado activos del catálogo; fechas coherentes; expediente **completado y activo**; **disponibilidad de datos** para la generación (salario/cargo cuando aplica).
- **AF-12.** **Tiempo de respuesta derivado** (`ResponseTimeDays` = emisión − solicitud).
- **AF-13.** **Auditoría** de toda operación (incl. generación, emisión, exportación, adjuntos).
- **AF-14.** **Permisos dedicados** de consulta y gestión; **autoservicio** para crear/consultar/cancelar lo propio; **`ViewCompensation`** adicional para constancias con salario.
- **AF-15.** **Localización** bilingüe (es / en) de etiquetas y errores.
- **AF-16.** **Generación del documento** (RF-015): servicio de armado por **tipo** (layout en código, QuestPDF/`DocumentModel`), fusionando datos del expediente + configuración de empresa.
- **AF-17.** **Configuración de constancia por empresa** (RF-016): membrete/logo, ciudad de emisión, **firmante** (nombre/cargo), pie/texto legal; *tenant-scoped*.
- **AF-18.** CRUD del **catálogo de medio de entrega**, *country-scoped*, con **seed SV** (RF-017).
- **AF-19.** CRUD del **catálogo de propósito**, *country-scoped*, con **seed SV** (RF-018).

---

## 4. Fuera de alcance (esta fase)

- **FA-01.** **Motor de plantillas EDITABLES por el usuario** (campos de fusión `{{…}}` tipo Scriban/Handlebars, entidad de plantilla versionada, editor). **Descartado en la ratificación (D-15/P-11)** en favor de **layout en código por tipo**. El cuerpo de la constancia es **estructural**; lo configurable es encabezado/firmante/pie (RF-016).
- **FA-02.** **Firma electrónica / digital** y **folio/QR verificable en línea** de la constancia. El PDF de Fase 1 es **informativo / para firma manual**. Candidato fuerte de Fase 2 (R-4).
- **FA-03.** **Motor de aprobación** (ruteo, niveles, umbrales, delegación). No es necesario (Hallazgo 2).
- **FA-04.** **Apostilla / legalización / traducción jurada** de la constancia para embajada (trámite consular). Manual/externo.
- **FA-05.** **Cálculo de nómina**: el salario se **lee** de los conceptos de compensación existentes (`CompensationConcept`); no se calcula ni proyecta nada nuevo.
- **FA-06.** **Notificaciones** (correo/portal) al empleado al emitir/entregar. Deseable; no integrado en Fase 1.
- **FA-07.** **Cobro de la constancia** (algunas empresas cobran la de embajada). Sin componente de pago.
- **FA-08.** **Dashboards analíticos** avanzados más allá del listado, su exportación y conteos por estado/tipo.
- **FA-09.** **Plantillas/firmantes por unidad organizativa o por tipo** (más allá del firmante por defecto de empresa). Override por tipo se difiere.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en este módulo |
|---|---|
| **Empleado (autoservicio)** | **Crea** su solicitud, **consulta** el estado de **sus propias** solicitudes, **descarga** el PDF emitido y (opcional) **cancela/retira** una propia aún pendiente. **No** ve solicitudes de otros, no emite ni genera. |
| **Gestor / Agente de RR. HH.** | Usa la **bandeja**: revisa, **procesa, genera y emite** la constancia, **marca entregada**, edita, da de baja, **consulta y exporta a Excel**. Puede crear **en nombre** de un empleado. *Para constancias con salario requiere `ViewCompensation`.* |
| **Administrador / Configurador** | Mantiene los **catálogos** (tipos, estados, medio de entrega, propósito) *country-scoped* y la **configuración de constancia de la empresa** (membrete/firmante/pie). |
| **Supervisor / Gerente con permiso de consulta** | **Consulta** (solo lectura) la bandeja y exporta, si se le otorga el permiso de vista. |
| **Sistema CLARIHR** | Validaciones, autoservicio, **generación del PDF**, auditoría, concurrencia, almacenamiento de adjuntos, generación de la exportación. |
| **Almacenamiento de archivos (Azure Blob)** | Persistencia del **PDF emitido**, el **logo** de la empresa y el archivo de exportación. |
| **Sistema de notificaciones (FUTURO)** | Avisos de "constancia emitida/entregada". No integrado (FA-06). |

---

## 6. Requerimientos funcionales

### RF-001 — Catálogo de tipos de constancia

**Descripción:** El administrador crea/mantiene las **categorías de constancia** con **Code** (ingresado por el usuario) + **Descripción**.

**Reglas de negocio:**
- **Code** obligatorio, ≤80, **único por país/tenant**, normalizado en mayúsculas; **Descripción** obligatoria, ≤200.
- Catálogo **country-scoped** (`GeneralCatalogItem`); ítem nace activo; `SortOrder` configurable; **extensible**.
- **Códigos canónicos** (seed) determinan el **layout de generación** y si **imprime salario** (RN-17/RN-20); los tipos **agregados por el usuario** usan un **layout genérico** sin salario.

**Criterios de aceptación:** Code+Descripción válidos → disponible; Code duplicado por país → unicidad; existe **seed SV ampliado** (§12).

**Prioridad:** Alta. **Dependencias:** convenciones de catálogo (`GeneralCatalogItem`, seed `HasData`).

---

### RF-002 — Catálogo de estados de la solicitud

**Descripción:** Catálogo **country-scoped** con el ciclo lineal `SOLICITADA`, `EN_PROCESO`, `EMITIDA`, `ENTREGADA`, `RECHAZADA`, `ANULADA` (patrón **híbrido**: catálogo + códigos canónicos, como `EconomicAidStatusCatalogItem`).

**Reglas de negocio:** mismas reglas de catálogo *country-scoped*; estados **estructurales** (seed SV); la app valida existencia/actividad **por key**; estado inicial `SOLICITADA` lo asigna el sistema.

**Criterios de aceptación:** existe **seed SV**; estado inexistente/inactivo → `422`.

**Prioridad:** Alta. **Dependencias:** RF-001.

---

### RF-003 — Solicitar constancia (autoservicio)

**Descripción:** El **empleado** registra una solicitud para **sí mismo**; RR. HH. también **en su nombre**. Datos: **tipo** (catálogo), **propósito** (catálogo), **destinatario** ("dirigida a"), **medio de entrega** (catálogo), **fecha de solicitud**, y opcionalmente **fecha requerida**, **idioma**, **copias**.

**Reglas de negocio:**
- **Autoservicio:** el empleado solo crea sobre **su propio** expediente (`LinkedUserPublicId` = usuario actual); RR. HH. con permiso de gestión, sobre cualquiera (RN-12).
- **Tipo / propósito / medio de entrega:** obligatorios y **activos** en sus catálogos (validados por key, RN-03).
- **Destinatario ("dirigida a"):** texto libre, ≤500; **obligatorio para `CONSTANCIA_EMBAJADA`** (D-06).
- **Fecha de solicitud:** no futura (tolerancia +1 día). **Fecha requerida** (si existe): **≥** solicitud.
- **Estado inicial:** `SOLICITADA`. **Sin monto ni moneda** (la constancia no involucra dinero).
- Expediente **empleado completado y activo** (RN-09).

**Criterios de aceptación:** datos válidos → `201 Created` con `ETag`, estado `SOLICITADA`; crear sobre expediente ajeno sin permiso → `403`; tipo/propósito/medio inactivo → `422`; embajada sin destinatario → `422`.

**Prioridad:** Alta. **Dependencias:** RF-001/RF-002/RF-017/RF-018.

---

### RF-004 — Consultar / bandeja de solicitudes (núcleo del requerimiento)

**Descripción:** **Listado company-scoped** con **filtros**, **orden** y **paginación**. **Visibilidad segmentada:** el empleado ve **solo las suyas**; RR. HH. ve **todas**.

**Reglas de negocio:**
- Endpoint de **bandeja** a nivel de empresa (`GET /api/v1/companies/{companyId}/certificate-requests`), patrón `PersonnelFileReporting` (paginado + filtros).
- **Filtros:** **tipo**, **estado**, **propósito**, **rango de fechas**, **empleado**, **unidad organizativa**, **texto**.
- Autoservicio: si el solicitante es empleado, el filtro se fuerza a sus propias solicitudes (RN-12).
- Respuesta con **`PagedResponse`** y (opcional) **conteos por estado** para la bandeja (P-06 → sí, recomendado).
- Cada ítem expone: empleado, tipo, propósito, estado, destinatario, fecha de solicitud, fecha de emisión, fecha de entrega, responsable de emisión, indicador "documento adjunto".

**Criterios de aceptación:** RR. HH. con vista → todas paginadas/filtrables; empleado → **solo las suyas**.

**Prioridad:** Alta. **Dependencias:** RF-003.

---

### RF-005 — Ver el detalle de una solicitud

**Descripción:** Consultar por id con **todos** los datos: solicitud (tipo, propósito, destinatario, idioma, copias, fechas), procesamiento (estado, responsable, fecha de emisión, observaciones, fecha de entrega, medio) y **documento emitido** (metadatos + enlace de descarga).

**Reglas de negocio:** el empleado solo accede al detalle de **su propia** solicitud; RR. HH. a cualquiera; el `concurrencyToken` del detalle se usa en el `If-Match` de escrituras.

**Criterios de aceptación:** dueño o RR. HH. ven todo; tercero sin permiso → `403`; id inexistente → `404`.

**Prioridad:** Alta. **Dependencias:** RF-003/RF-004.

---

### RF-006 — Exportar el listado a Excel (requisito explícito)

**Descripción:** Exportar a **Excel (XLSX)** / **CSV** el **listado filtrado**, reutilizando `ReportExportDeliveryService` / `ReportExportFileWriter`. Descarga síncrona acotada; (opcional) export asíncrono por job para volúmenes grandes.

**Reglas de negocio:**
- Aplica **los mismos filtros** que la bandeja (RF-004).
- Columnas: Empleado, Documento de identidad, Unidad organizativa, Tipo, Propósito, Estado, Dirigida a, Medio de entrega, Fecha de solicitud, Fecha de emisión, Fecha de entrega, Responsable, Tiempo de respuesta (días).
- Auditoría de la exportación (filtros, quién, cuándo); solo **RR. HH./permiso de vista** exporta.

**Criterios de aceptación:** RR. HH. con filtro → descarga `.xlsx` con las filas filtradas; formato inválido → `400`.

**Prioridad:** **Alta**. **Dependencias:** RF-004; `ReportExportDeliveryService`.

---

### RF-007 — Procesar y emitir la constancia (RR. HH.)

**Descripción:** RR. HH. **procesa** (`EN_PROCESO`) y, al **emitir** (`EMITIDA`), dispara la **generación del PDF** (RF-015) y lo deja como documento emitido; registra **responsable**, **fecha de emisión** y **observaciones**. Modelado como **acción** dedicada.

**Reglas de negocio:**
- **Solo RR. HH.** (permiso de gestión); para tipos que **imprimen salario**, además **`ViewCompensation`** (RN-20/D-20).
- Estado destino válido/activo (por key).
- Al **emitir**: se **genera** el PDF (o se usa el cargado manualmente, RF-008); registra `IssuedByUserId`, `IssuedDateUtc`, `ResolutionNotes`.
- Al **rechazar** (`RECHAZADA`): `ResolutionNotes` recomendado (motivo).
- `IssuedDateUtc` **≥** `RequestDateUtc`. **Derivado:** `ResponseTimeDays` recalculado al emitir.
- Si **faltan datos** para la generación (p. ej. sin assignment activo / sin salario en una constancia de salario) → **bloquea** con error claro (E-17).
- Transiciones coherentes: no emitir una `ANULADA`; no re-emitir una `ENTREGADA` (recomendado; permitir **regenerar** mientras esté `EMITIDA`).

**Criterios de aceptación:** RR. HH. sobre `SOLICITADA`/`EN_PROCESO` → emite, se genera el PDF, pasa a `EMITIDA`, `200` + nuevo `ETag`; el propio empleado intentando emitir → `403`; faltan datos de salario → `422` (E-17).

**Prioridad:** Alta. **Dependencias:** RF-003; RF-015; permisos (`ManageCertificateRequests` + `ViewCompensation`).

---

### RF-008 — Documento emitido (generado + carga manual opcional)

**Descripción:** El **documento emitido** es, por defecto, el **PDF generado** por el sistema (RF-015). RR. HH. puede **cargar manualmente** un documento (escaneo firmado u override) y **reemplazar** el vigente. Listar, descargar (URL temporal/SAS) y eliminar.

**Reglas de negocio:**
- Normalmente **un documento vigente** por solicitud; al regenerar/reemplazar, el anterior queda inactivo (historial).
- `FilePurpose.CertificateRequestDocument`; almacenamiento en Blob; `IsSystemGenerated` distingue generado vs cargado.
- **Adjunta/genera RR. HH.**; el **empleado descarga** su propia constancia (no sube — RN-13).

**Criterios de aceptación:** al emitir, queda el PDF generado asociado y descargable; el dueño puede descargarlo; cargar un override lo reemplaza con auditoría.

**Prioridad:** Alta. **Dependencias:** archivos (Blob), `FilePurpose`, RF-015.

---

### RF-009 — Marcar entregada

**Descripción:** RR. HH. marca `ENTREGADA` registrando **fecha de entrega** y confirmando el **medio de entrega** (catálogo).

**Reglas de negocio:** solo desde `EMITIDA`; `DeliveredDateUtc` ≥ `IssuedDateUtc`.

**Criterios de aceptación:** `EMITIDA` → entregada con fecha; entregar una no emitida → `422`.

**Prioridad:** Media. **Dependencias:** RF-007; RF-017.

---

### RF-010 — Editar / dar de baja una solicitud (RR. HH.)

**Descripción:** RR. HH. edita campos de negocio (con `If-Match`) y **da de baja** (baja lógica), con auditoría.

**Reglas de negocio:** validaciones de RF-003; `concurrencyToken` vigente (conflicto → `409`); **sin borrado físico** (RN-08).

**Criterios de aceptación:** token vigente + datos válidos → actualiza + nuevo `ETag`; token viejo → `409`.

**Prioridad:** Media. **Dependencias:** RF-003.

---

### RF-011 — Cancelar / retirar la propia solicitud pendiente (autoservicio)

**Descripción:** El empleado **cancela** una solicitud **propia** aún **pendiente** (`SOLICITADA`/`EN_PROCESO`) → `ANULADA`.

**Reglas de negocio:** solo el **propio** empleado o RR. HH.; solo estados **no resueltos**; registra actor y fecha; baja por estado.

**Criterios de aceptación:** dueño de una pendiente → `ANULADA`; cancelar una emitida → `422`.

**Prioridad:** Media. **Dependencias:** RF-003/RF-004.

---

### RF-012 — Auditoría de operaciones

**Descripción:** Toda alta/generación/emisión/entrega/edición/baja/adjunto/exportación registra auditoría (actor, fecha, before/after), reutilizando `IAuditService` / `PersonnelFileEmployeeAudits`.

**Prioridad:** Alta. **Dependencias:** servicio de auditoría.

---

### RF-013 — Localización bilingüe

**Descripción:** Etiquetas, estados y errores en **es/en** (`.resx`), con paridad verificada por `BackendMessageLocalizationTests`. *(El cuerpo del PDF de embajada puede ofrecerse en inglés según `LanguageCode` — D-07/RN-19.)*

**Prioridad:** Media. **Dependencias:** convenciones de localización.

---

### RF-014 — Permisos dedicados

**Descripción:** `ViewCertificateRequests` (consulta/exportación) y `ManageCertificateRequests` (procesar/emitir/editar/baja), en **controlador(es) dedicado(s)** (porque `AuthorizationPolicySet` es a nivel de clase); el autoservicio se resuelve en los **gates de los handlers**. Las constancias que imprimen salario exigen además **`ViewCompensation`** (RN-20).

**Prioridad:** Alta. **Dependencias:** IAM / `AuthorizationPolicySet` / `ProvisioningConstants`.

---

### RF-015 — Generación del documento (PDF) por tipo

**Descripción:** Servicio que **arma la constancia en PDF** según el **tipo** (layout en código), reutilizando `DocumentModel` + `QuestPdfDocumentRenderer` (patrón `JobProfilePdfRenderer`). Fusiona **datos del expediente** con la **configuración de empresa** (RF-016).

**Reglas de negocio:**
- **Layout por código canónico** del tipo; tipos personalizados → **layout genérico** (datos básicos + sin salario).
- **Fuentes de datos (merge):** nombre (`PersonnelFile.FullName`), documento (`PersonnelFileIdentification` primaria: tipo + número), **cargo** (`JobProfile.Title` / `PositionSlot.Title` del **assignment activo+primario**), **fecha de ingreso** (`PersonnelFileEmployeeProfile.HireDate`), **antigüedad** (`EmployeeSeniority.Between(HireDate, hoy)`), **salario** (concepto de compensación `Ingreso`/`Fijo` del assignment activo) **solo si el tipo lo imprime**.
- **Encabezado/firmante/pie** desde `CompanyCertificateSettings` (RF-016): membrete/logo, ciudad, fecha de emisión, "A quien interesa/corresponda", firmante (nombre/cargo), pie/texto legal.
- **Idioma:** si `LanguageCode = en` y el tipo lo soporta (embajada), usar el layout en inglés (RN-19).
- **Disponibilidad de datos:** si falta un dato obligatorio del tipo (p. ej. salario/cargo) → **no genera** y devuelve error (E-17).
- El PDF se persiste como **documento emitido** (`IsSystemGenerated = true`) y queda descargable.

**Criterios de aceptación:** dado un empleado con datos completos, cuando RR. HH. emite, entonces se genera un PDF con membrete/firmante de la empresa y los datos correctos del tipo; un tipo de salario sin salario cargado → `422` (E-17); sin `ViewCompensation` para un tipo con salario → `403` (E-18).

**Prioridad:** Alta. **Dependencias:** QuestPDF/`IDocumentModelRenderer`; `CompensationConcept`/`EmploymentAssignment`/`JobProfile`/`EmployeeProfile`/`Identification`; RF-016; `ViewCompensation`.

---

### RF-016 — Configuración de constancia por empresa (membrete / firmante / pie)

**Descripción:** Entidad de **configuración tenant-scoped** con los elementos **configurables** del documento: **logo/membrete**, **ciudad de emisión** por defecto, **firmante** (nombre y cargo), **pie / texto legal** y (opcional) variantes por idioma. Administrable por un rol de configuración.

**Reglas de negocio:** una configuración por empresa (con valores por defecto si no se ha configurado); el **cuerpo por tipo es estructural** (no editable como texto libre — D-15); el logo se almacena en Blob (`FilePurpose` de logo/empresa existente).

**Criterios de aceptación:** configurada la empresa, cuando se genera una constancia, entonces el PDF muestra el membrete/firmante/pie configurados; sin configurar, usa valores por defecto razonables.

**Prioridad:** Alta. **Dependencias:** RF-015; almacenamiento de archivos (logo).

---

### RF-017 — Catálogo de medio de entrega

**Descripción:** Catálogo **country-scoped** (`GeneralCatalogItem`) de medios de entrega; **seed SV** (`PRESENCIAL`, `CORREO_ELECTRONICO`, `PORTAL`).

**Reglas de negocio:** mismas reglas de catálogo *country-scoped*; validado por key.

**Criterios de aceptación:** existe **seed SV**; medio inexistente/inactivo → `422`.

**Prioridad:** Media. **Dependencias:** convenciones de catálogo.

---

### RF-018 — Catálogo de propósito

**Descripción:** Catálogo **country-scoped** (`GeneralCatalogItem`) del **propósito** de la constancia; **seed SV** (`TRAMITE_BANCARIO`, `CREDITO`, `VISA_EMBAJADA`, `TRAMITE_MIGRATORIO`, `USO_PERSONAL`, `OTRO`).

**Reglas de negocio:** mismas reglas de catálogo *country-scoped*; validado por key. Complementa (no sustituye) el **destinatario** libre ("dirigida a").

**Criterios de aceptación:** existe **seed SV**; propósito inexistente/inactivo → `422`.

**Prioridad:** Media. **Dependencias:** convenciones de catálogo.

---

## 7. Requerimientos no funcionales

- **Seguridad / Privacidad:** permisos dedicados; multi-tenant (`TenantId`); el empleado ve **solo lo suyo**; descarga del PDF con URL temporal/SAS; **`ViewCompensation`** obligatorio para constancias con salario; el salario se **inyecta server-side** (nunca se acepta del cliente).
- **Rendimiento:** bandeja **paginada**; índices por `PersonnelFileId`, `RequestStatusCode`, `CertificateTypeCode`, `RequestDateUtc`; generación de PDF **offload** a thread-pool (como `QuestPdfDocumentRenderer`); exportación con límite síncrono / job asíncrono.
- **Disponibilidad / Escalabilidad:** según el API existente; PDFs, logos y exportaciones en Blob.
- **Usabilidad:** selectores de tipo/propósito/medio; estados claros; vista "mis solicitudes"; bandeja con filtros, conteos y exportación; mensajes de validación claros.
- **Auditoría:** trazabilidad before/after; baja lógica; auditoría de generación, adjuntos y exportaciones.
- **Compatibilidad:** fechas UTC; **enums como strings**; `api/v1`; error en `extensions.code`; `If-Match`/`ETag` (faltante → `400`; viejo → `409`).
- **Mantenibilidad:** "slice vertical" + **módulo de reglas puro** (`*.Rules.cs`); el mapper de generación por tipo aislado y testeable.
- **Accesibilidad / i18n:** etiquetas y errores bilingües; PDF de embajada disponible en inglés (RN-19).

---

## 8. Historias de usuario

### HU-001 — Solicitar una constancia
Como **empleado**, quiero **solicitar una constancia** indicando tipo, propósito y para quién es, para **obtener el documento** sin trámites presenciales.
- Dado que estoy autenticado sobre mi expediente, cuando elijo tipo/propósito/medio y el destinatario, entonces queda en estado `SOLICITADA`.

### HU-002 — Revisar la bandeja
Como **agente de RR. HH.**, quiero **ver el listado de todas las solicitudes** con filtros, para **gestionar** la emisión.
- Dado permiso de vista, cuando abro la bandeja, entonces veo todas paginadas y filtrables, con conteos por estado.

### HU-003 — Emitir y generar la constancia
Como **agente de RR. HH.**, quiero **emitir** la constancia y que el sistema **genere el PDF** con membrete y firmante, para **entregarla** con respaldo y formato consistente.
- Dado una solicitud `SOLICITADA` con datos completos, cuando emito, entonces se genera el PDF, pasa a `EMITIDA` y queda registrado quién y cuándo.
- Dado un tipo de salario sin salario cargado, cuando intento emitir, entonces el sistema me lo impide con un mensaje claro.

### HU-004 — Exportar a Excel
Como **agente de RR. HH.**, quiero **exportar el listado a Excel**, para **reportar** fuera del sistema.
- Dado un filtro aplicado, cuando exporto, entonces descargo un `.xlsx` con las filas filtradas.

### HU-005 — Consultar y descargar mi constancia
Como **empleado**, quiero **ver el estado** de mis solicitudes y **descargar** la constancia emitida, para **saber** cuándo está lista.
- Dado que tengo solicitudes, cuando consulto, entonces veo **solo las mías** y descargo la emitida.

### HU-006 — Configurar el formato de la empresa
Como **administrador**, quiero **configurar membrete, firmante y pie** de las constancias, para que **todas** salgan con la imagen institucional.
- Dado permiso de configuración, cuando guardo el firmante y el logo, entonces las constancias generadas los usan.

### HU-007 — Cancelar mi solicitud (opcional)
Como **empleado**, quiero **retirar mi solicitud mientras esté pendiente**, para **cancelarla** si ya no la necesito.
- Dado una `SOLICITADA`, cuando la cancelo, entonces pasa a `ANULADA`.

### HU-008 — Configurar catálogos
Como **administrador de catálogos**, quiero **mantener tipos, estados, medios y propósitos**, para **estandarizar** el proceso por país.
- Dado permiso de gestión, cuando creo `CONSTANCIA_SALARIO`, entonces queda activo y disponible.

---

## 9. Reglas de negocio

- **RN-01.** La solicitud está **asociada a un empleado** existente (por id de expediente).
- **RN-02.** Es **independiente de la nómina**: **no** tiene monto ni moneda; el salario impreso se **lee** de compensación (no se calcula).
- **RN-03.** **Tipo, propósito y medio de entrega** deben existir y estar **activos** en sus catálogos al crear/editar (validados por key).
- **RN-04.** El **estado** es un código válido/activo del catálogo; inicial `SOLICITADA`. Ciclo **lineal** (sin aprobación).
- **RN-05.** **Fechas:** solicitud no futura; requerida ≥ solicitud; emisión ≥ solicitud; entrega ≥ emisión.
- **RN-06.** El **destinatario ("dirigida a")** es texto libre; **obligatorio para `CONSTANCIA_EMBAJADA`** (D-06).
- **RN-07.** El **`ResponseTimeDays`** se **deriva** de (emisión − solicitud) y se recalcula al emitir.
- **RN-08.** No hay **borrado físico**; bajas lógicas y cambios de estado.
- **RN-09.** Las operaciones requieren expediente en estado **empleado completado y activo**.
- **RN-10.** Aislamiento **multi-tenant** en toda operación.
- **RN-11.** Toda escritura (incluidos generación, adjuntos, emisión y exportación) queda **auditada** y protegida por **concurrencia optimista**.
- **RN-12.** **Autoservicio:** el empleado **crea/consulta/cancela/descarga lo propio** (`LinkedUserPublicId` = usuario actual). RR. HH. con permiso opera sobre cualquiera.
- **RN-13.** **RR. HH. genera/emite**; el empleado **no** genera ni sube el documento (solo descarga el suyo).
- **RN-14.** Desactivar un **tipo/estado/medio/propósito** no afecta solicitudes históricas (snapshots de descripción).
- **RN-15.** El **PDF emitido** es el respaldo; su descarga está controlada por permiso/propiedad y se sirve con URL temporal.
- **RN-16.** El **layout** del PDF lo determina el **código canónico** del tipo; los tipos personalizados usan **layout genérico** sin salario.
- **RN-17.** La **constancia de salario** y la **de embajada** **imprimen salario**; las demás, **no** (D-20, confirmable por tipo).
- **RN-18.** El **salario** proviene del **concepto de compensación** (`Ingreso`/`Fijo`, activo) del **assignment activo+primario**; si no hay dato, **no se genera** (E-17).
- **RN-19.** La constancia para **embajada** puede generarse en **inglés** según `LanguageCode`; el resto en español (es).
- **RN-20.** **Acoplamiento de permisos:** emitir/generar una constancia que **imprime salario** requiere **`ManageCertificateRequests` + `ViewCompensation`**; el salario se **inyecta server-side**.

---

## 10. Flujos principales

### Flujo A — Configurar catálogos y formato de empresa
1. Administrador → catálogos (tipos / estados / medios / propósitos) → Nuevo / Editar (Code + Descripción, validación por país).
2. Administrador → "Configuración de constancias" → carga **logo**, **ciudad**, **firmante** (nombre/cargo) y **pie/texto legal**.

### Flujo B — El empleado solicita (autoservicio)
1. Empleado → portal → "Solicitudes" → "Solicitar constancia".
2. Elige **tipo**, **propósito**, escribe **dirigida a**, elige **medio de entrega**; opcional: fecha requerida, idioma, copias.
3. Sistema valida (autoservicio; catálogos activos; fechas; embajada con destinatario; expediente completado).
4. **Persiste** `SOLICITADA`, **audita**, retorna `201 Created` con `ETag`.

### Flujo C — RR. HH. procesa, genera y emite
1. RR. HH. abre la **bandeja** → filtra → abre el **detalle**.
2. (Opcional) `EN_PROCESO`; valida datos; **emite** → el sistema **genera el PDF** (membrete/firmante/datos) → `EMITIDA`, o **rechaza** con motivo.
3. Sistema valida (RR. HH.; `ViewCompensation` si imprime salario; disponibilidad de datos; fechas), deriva `ResponseTimeDays`, persiste, audita, `200` + nuevo `ETag`.
4. **Marca entregada** (`ENTREGADA`) con fecha y medio.

### Flujo D — Exportar
1. Sobre la bandeja con filtros, RR. HH. → "Exportar a Excel" → `.xlsx` (filas/columnas filtradas) + auditoría.

### Flujo E — Consulta/descarga del empleado
1. El empleado ve "mis solicitudes"; cuando está `EMITIDA`/`ENTREGADA`, **descarga** el PDF.

### Flujo F — Cancelación por el empleado (opcional)
1. Sobre una propia pendiente, el empleado la **retira** → `ANULADA`; auditoría.

---

## 11. Flujos alternativos y excepciones

- **E-01 (tipo inválido/inactivo):** `422` `CERTIFICATE_TYPE_CODE_INVALID`.
- **E-02 (estado inválido/inactivo):** `422` `CERTIFICATE_REQUEST_STATUS_CODE_INVALID`.
- **E-03 (destinatario faltante en embajada):** `422` `CERTIFICATE_ADDRESSEE_REQUIRED`.
- **E-04 (propósito/medio inválido o inactivo):** `422` `CERTIFICATE_PURPOSE_CODE_INVALID` / `CERTIFICATE_DELIVERY_METHOD_CODE_INVALID`.
- **E-05 (fecha de solicitud futura):** `400/422`.
- **E-06 (fechas incoherentes):** `422` `CERTIFICATE_DATE_INCOHERENT`.
- **E-07 (emitir/entregar sin permiso de gestión):** `403` `Forbidden`.
- **E-08 (transición inválida):** `422` `CERTIFICATE_STATE_RULE_VIOLATION`.
- **E-09 (autoservicio sobre expediente ajeno sin permiso):** `403`.
- **E-10 (expediente no completado/inactivo):** `409/422` `StateRuleViolation`.
- **E-11 (concurrencia / If-Match faltante):** `409` / `400`.
- **E-12 (solicitud/adjunto inexistente):** `404` `ItemNotFound`.
- **E-13 (sin permiso / no autenticado):** `403` / `401`.
- **E-14 (Code de catálogo duplicado por país):** `409/422`.
- **E-15 (adjunto/override inválido — tipo/tamaño):** `400/413/422`.
- **E-16 (formato de exportación no soportado):** `400` `REPORT_EXPORT_FORMAT_INVALID`.
- **E-17 (datos insuficientes para generar — sin assignment activo / sin salario en constancia de salario / sin cargo):** `422` `CERTIFICATE_GENERATION_DATA_UNAVAILABLE`.
- **E-18 (constancia con salario sin `ViewCompensation`):** `403` `CERTIFICATE_COMPENSATION_FORBIDDEN`.
- **E-19 (fallo de render del PDF):** `500/422` `CERTIFICATE_GENERATION_FAILED` (auditado).

---

## 12. Datos requeridos

### Entidad: TipoDeConstancia (catálogo) — `CertificateTypeCatalogItem`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| (campos estándar de catálogo *country-scoped* `GeneralCatalogItem`: PublicId, CountryCatalogItemId/CountryCode, Code ≤80 único por país, Name ≤200, IsActive, SortOrder, ConcurrencyToken) | | | | |

**Seed inicial SV (tipos — ampliado, D-19):**
`CONSTANCIA_SALARIO`/"Constancia de salario" (10, **imprime salario**) · `CONSTANCIA_LABORAL`/"Constancia de trabajo (laboral)" (20) · `CONSTANCIA_EMBAJADA`/"Constancia para embajada" (30, **imprime salario**) · `CONSTANCIA_TIEMPO_LABORADO`/"Constancia de tiempo laborado" (40) · `CONSTANCIA_NO_DESCUENTO`/"Constancia de no descuento" (50) · `CARTA_RECOMENDACION`/"Carta de recomendación laboral" (60).

### Entidad: EstadoDeSolicitud (catálogo) — `CertificateRequestStatusCatalogItem`
**Seed SV:** `SOLICITADA`/"Solicitada" (10) · `EN_PROCESO`/"En proceso" (20) · `EMITIDA`/"Emitida" (30) · `ENTREGADA`/"Entregada" (40) · `RECHAZADA`/"Rechazada" (50) · `ANULADA`/"Anulada" (60).

### Entidad: MedioDeEntrega (catálogo) — `CertificateDeliveryMethodCatalogItem`
**Seed SV:** `PRESENCIAL`/"Entrega presencial" (10) · `CORREO_ELECTRONICO`/"Correo electrónico" (20) · `PORTAL`/"Descarga desde el portal" (30).

### Entidad: Propósito (catálogo) — `CertificatePurposeCatalogItem`
**Seed SV:** `TRAMITE_BANCARIO`/"Trámite bancario" (10) · `CREDITO`/"Solicitud de crédito" (20) · `VISA_EMBAJADA`/"Visa / trámite ante embajada" (30) · `TRAMITE_MIGRATORIO`/"Trámite migratorio" (40) · `USO_PERSONAL`/"Uso personal" (50) · `OTRO`/"Otro" (60).

### Entidad: SolicitudDeConstancia — `PersonnelFileCertificateRequest`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| PersonnelFileId | FK (long) | Sí | Expediente **completado y activo** | Empleado solicitante |
| CertificateTypeCode | Texto (≤80) | Sí | Activo en catálogo | Tipo de constancia |
| TypeNameSnapshot | Texto (≤200) | No (recomendado) | — | Snapshot de la descripción del tipo |
| RequestStatusCode | Texto (≤80) | Sí | Válido/activo; inicia `SOLICITADA` | Estado del ciclo |
| PurposeCode | Texto (≤80) | Sí | Activo en catálogo | Propósito |
| AddressedTo (**Dirigida a**) | Texto (≤500) | Condicional | Obligatorio si `CONSTANCIA_EMBAJADA` | Destinatario |
| DeliveryMethodCode | Texto (≤80) | Sí | Activo en catálogo | Medio de entrega |
| LanguageCode | Texto (≤10) | No | es / en (D-07) | Idioma del documento |
| Copies | Entero | No | ≥1 (default 1) | Número de copias |
| RequestDateUtc | Fecha (UTC) | Sí | No futura | Fecha de solicitud |
| NeededByDateUtc | Fecha (UTC)? | No | ≥ RequestDateUtc | Fecha en que la necesita |
| RequestedByUserId | GUID | Sí (sistema) | — | Quién la solicitó |
| IssuedByUserId | GUID? | No | — | Quién la emitió (RR. HH.) |
| IssuedDateUtc | Fecha (UTC)? | No | ≥ RequestDateUtc | Fecha de emisión |
| DeliveredDateUtc | Fecha (UTC)? | No | ≥ IssuedDateUtc | Fecha de entrega |
| ResolutionNotes | Texto (≤2000)? | No | — | Observaciones / motivo de rechazo |
| ResponseTimeDays | Entero? | No (**derivado**) | — | (Emisión − solicitud) en días |
| IsActive | Booleano | Sí | — | Baja lógica |
| ConcurrencyToken | GUID | Sí (sistema) | — | Concurrencia |
| CreatedAtUtc / ModifiedAtUtc | Fecha | Sí/No | — | Auditoría |

### Entidad: DocumentoEmitido — `CertificateRequestDocument`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| CertificateRequestId | FK | Sí | Solicitud existente | Solicitud a la que pertenece |
| FilePublicId / ruta Blob | ref | Sí | Política de archivos | Archivo en Blob (`FilePurpose.CertificateRequestDocument`) |
| IsSystemGenerated | Booleano | Sí | — | Generado por el sistema vs cargado manual |
| FileName / ContentType / SizeBytes | varios | Sí | Tipo/tamaño permitidos | Metadatos |
| Observations | Texto? | No | — | Notas |
| IsActive / ConcurrencyToken | Bool / GUID | Sí | — | Baja lógica / concurrencia |
| CreatedAtUtc | Fecha | Sí | — | Auditoría |

> **Nota:** a diferencia de `EconomicAidRequestDocument` (evidencia **de entrada** que sube el empleado), aquí el documento es el **PDF de salida** que **genera RR. HH./el sistema** (RN-13/RN-15).

### Entidad: ConfiguraciónDeConstanciaDeEmpresa — `CompanyCertificateSettings` (tenant-scoped)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| LogoFilePublicId | GUID? | No | Archivo (Blob) | Membrete / logo |
| IssuingCity | Texto (≤120)? | No | — | Ciudad de emisión por defecto |
| SignatoryName | Texto (≤200)? | No | — | Nombre del firmante |
| SignatoryTitle | Texto (≤200)? | No | — | Cargo del firmante |
| FooterText | Texto (≤2000)? | No | — | Pie / texto legal |
| ConcurrencyToken | GUID | Sí (sistema) | — | Concurrencia |

---

## 13. Integraciones necesarias

**Servicios reutilizables accedidos por KEY / infraestructura existente (requisito explícito):**

- **Validación de catálogos por código:** `IPersonnelFileRepository.CatalogCodeIsActiveAsync(companyId, category, code)` con **cuatro** keys nuevas `CERTIFICATE_TYPE_CATALOG`, `CERTIFICATE_REQUEST_STATUS_CATALOG`, `CERTIFICATE_DELIVERY_METHOD_CATALOG`, `CERTIFICATE_PURPOSE_CATALOG` — **registrarlas en el switch** de `PersonnelFileRepository`.
- **Snapshot de nombre:** `IPersonnelFileRepository.GetCatalogItemNameAsync(...)` para `TypeNameSnapshot` (y los demás snapshots).
- **Autoservicio:** patrón `LoadForCreateOwnOrManage…` (`PersonnelFile.LinkedUserPublicId` == `ICurrentUserService.UserId`).
- **Catálogos del módulo:** `GeneralCatalogItem` + `GeneralCatalogKeyMap` + seed `HasData` (wire-keys `certificate-types`, `certificate-request-statuses`, `certificate-delivery-methods`, `certificate-purposes`).
- **Motor de exportación (RF-006):** `ReportExportFileWriter` (CSV/XLSX) + `ReportExportDeliveryService.CreateFileResultAsync<TRow>(…)`; opcional `ReportExportJob`.
- **Consulta paginada/filtrada (RF-004):** `PagedResponse<TItem>` + patrón *dynamic-query* de `PersonnelFileReporting` (company-scoped).
- **Generación de PDF (RF-015):** **QuestPDF `2024.12.3`** + `DocumentModel` (AST) + `IDocumentModelRenderer` / `QuestPdfDocumentRenderer`, espejando `JobProfilePdfRenderer` + un nuevo `ICertificateDocumentMapper` (datos → `DocumentModel`).
- **Fuentes de datos del merge:** `PersonnelFileCompensationConcept` (Value/CurrencyCode, `Ingreso`/`Fijo`, activo) del **assignment activo+primario** (`PersonnelFileEmploymentAssignment`); cargo vía `PositionSlot.Title` / `JobProfile.Title`; `PersonnelFileEmployeeProfile.HireDate` + `EmployeeSeniority.Between(...)`; `PersonnelFile.FullName`; `PersonnelFileIdentification` (primaria: tipo + número).
- **Permiso de compensación:** `PersonnelFilePolicies.ViewCompensation` / `IPersonnelFileAuthorizationService.EnsureCanViewCompensationAsync(...)` — **gate obligatorio** para constancias con salario (RN-20).
- **Almacenamiento de archivos (Azure Blob):** nuevo **`FilePurpose.CertificateRequestDocument`** (PDF emitido) — **registrar en `appsettings`** (gotcha conocido); logo de empresa con el `FilePurpose` de logo existente; `DocumentTypeCatalogItem` (clasificación opcional).
- **Auditoría:** `IAuditService` / `PersonnelFileEmployeeAudits` (before/after).
- **Concurrencia:** `ConditionalRequestResultFilter` (`If-Match`/`ETag`).
- **Autorización (IAM):** permisos dedicados `ViewCertificateRequests` / `ManageCertificateRequests` (`AuthorizationPolicySet` a nivel de clase → **controlador dedicado**) + registro en `ProvisioningConstants`.
- **Localización:** `BackendMessages.resx` / `.es.resx` (+ `.es-SV.resx`) + `BackendMessageLocalizationTests`.

**Integraciones FUTURAS (fuera de alcance — §4):**
- **Firma electrónica + folio/QR verificable**; **notificaciones** al empleado; **plantillas editables** (Scriban); **apostilla/legalización**.

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Empleado (autoservicio)** | Crear su solicitud; consultar/cancelar **las propias**; **descargar** su PDF emitido | No ve/gestiona solicitudes de otros; **no genera ni emite**; no exporta |
| **Gestor / Agente RR. HH.** | `ManageCertificateRequests` + `ViewCertificateRequests`: bandeja, detalle, **exportar**, procesar/**emitir/generar**, marcar entregada, editar, baja; crear en nombre de otro | Constancias con salario requieren además **`ViewCompensation`** (RN-20) |
| **Supervisor / Gerente (consulta)** | `ViewCertificateRequests`: bandeja, detalle, **exportar** (solo lectura) | No procesa ni emite |
| **Administrador / Configurador** | Gestión de catálogos (tipos/estados/medios/propósitos) + **configuración de constancia de empresa** (membrete/firmante/pie) | No opera solicitudes individuales |
| **Sistema CLARIHR** | Validaciones, autoservicio, **generación PDF**, auditoría, concurrencia, exportación | — |

---

## 15. Criterios de aceptación generales

- Existen los **4 catálogos** *country-scoped* (tipos / estados / medios / propósitos) con **seed SV** y validables por key.
- El **empleado** puede **solicitar** sobre su propio expediente y **consultar/descargar** solo lo suyo.
- RR. HH. dispone de una **bandeja company-scoped** (listado + filtros + conteos + detalle) y puede **exportar a Excel** el listado filtrado.
- RR. HH. puede **emitir** y el sistema **genera el PDF** con membrete/firmante de la empresa y los datos correctos por tipo; **marcar entregada**; `ResponseTimeDays` derivado.
- Las constancias que **imprimen salario** exigen **`ViewCompensation`** y el salario se **inyecta server-side**; sin datos suficientes → error claro.
- Toda operación **auditada**, con **concurrencia** (`If-Match`/`ETag`) y **multi-tenant**.
- Errores y etiquetas **bilingües** con **paridad** verificada por prueba.
- **NO** se construye motor de aprobación, motor de plantillas editables, firma electrónica/QR ni notificaciones.
- La solución **compila**, pasa **pruebas unitarias** (incl. localización y gobernanza) y la **migración** queda **sin drift**.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R-01 — Disponibilidad de datos para generar (alto):** la **constancia de salario/embajada** depende de que el empleado tenga **assignment activo** y **salario cargado** en compensación; si faltan, no se genera. *Mitigación:* validación previa + error claro (E-17); el negocio debe asegurar la carga de compensación.
- **R-02 — Exactitud/consistencia del documento legal:** un PDF con datos errados es un riesgo reputacional/legal. *Mitigación:* layout en código (consistente), datos server-side (no editables por el cliente), **revisión legal de los layouts semilla**, regeneración antes de firmar.
- **R-03 — Exposición de compensación:** generar constancias de salario expone datos sensibles. *Mitigación:* gate **`ViewCompensation`** (RN-20) + descarga con URL temporal + auditoría.
- **R-04 — Sin firma electrónica en Fase 1:** el PDF generado es **informativo / para firma manual**; puede no ser suficiente para algunos trámites. *Mitigación:* firma física/sello; firma electrónica + QR en Fase 2 (R-4).
- **R-05 — Confusión con la familia "Solicitudes":** riesgo de duplicar lógica ya resuelta por `EconomicAidRequest`. *Mitigación:* clonar el esqueleto, **omitir** lo financiero/aprobación.
- **R-06 — Alcance ampliado:** la generación + 2 catálogos extra + configuración de empresa aumentan el esfuerzo respecto al MVP. *Mitigación:* fasear **dentro** de Fase 1 (catálogos+modelo+ingreso+bandeja+export primero; generación+settings después) — §18 R-7.

### Supuestos
- **S-01.** El "módulo Solicitudes del portal" es la **agrupación de autoservicio** (frontend); cada tipo es su **propio agregado** en el backend.
- **S-02.** No existe data productiva de constancias (net-new) → sin migración de datos.
- **S-03.** El empleado autenticado está vinculado a su expediente por `LinkedUserPublicId`.
- **S-04.** El **salario** vive en `CompensationConcept` (Ingreso/Fijo) del assignment activo; el **cargo** en `JobProfile.Title`/`PositionSlot.Title`.
- **S-05.** El membrete/firmante por defecto es **a nivel de empresa**; override por tipo/unidad se difiere (FA-09).

### Dependencias
- **D-A.** Convenciones de catálogo *country-scoped* + seed `HasData` + migración.
- **D-B.** Motor de exportación (`ReportExportDeliveryService` / `ReportExportFileWriter`).
- **D-C.** Infraestructura de archivos (Blob) + nuevo `FilePurpose` (+ `appsettings`).
- **D-D.** **QuestPDF / `IDocumentModelRenderer`** y las **fuentes de datos de compensación/assignment/job-profile/identidad**.
- **D-E.** Permiso **`ViewCompensation`** y su servicio de autorización.
- **D-F.** IAM (permisos dedicados) + `ProvisioningConstants` + pruebas de gobernanza/localización.

---

## 17. Preguntas abiertas — RESUELTAS en la ratificación (2026-06-28)

| # | Pregunta | Resolución |
|---|---|---|
| **P-01** | ¿Slice completo o solo la consulta? | **Slice vertical completo** (ingreso + emisión + consulta + export). → **D-01.** |
| **P-02** | ¿Generar el documento o solo adjuntar? | **Generar en Fase 1**. → **D-15.** |
| **P-11** | ¿Cómo generar (no hay motor de plantillas)? | **Layout en código por tipo** + encabezado/firmante/pie configurables; **sin** motor de merge-fields editable. → **D-15/D-17.** |
| **P-07** | ¿Medio de entrega y propósito como catálogos? | **Sí, catálogos *country-scoped* con seed SV.** → **D-18.** |
| **P-03** | ¿Más tipos a semillar? | **Sí, seed SV ampliado** (6 tipos). → **D-19.** |
| **P-04** | Contenido de la constancia de salario | Salario **mensual fijo** del assignment activo (concepto `Ingreso`/`Fijo`) + cargo + antigüedad + fecha de ingreso. Bruto fijo; desglose detallado se difiere. *(Ratificado por recomendación.)* |
| **P-05** | Embajada: ¿inglés? ¿apostilla? | **Inglés disponible** por `LanguageCode` (RN-19); **apostilla/legalización fuera de alcance** (FA-04). |
| **P-06** | ¿Conteos en la bandeja? | **Sí**, conteos por estado además del listado. |
| **P-08** | ¿SLA? | Se expone **`ResponseTimeDays`** derivado; alertas de SLA se difieren. → **D-14.** |
| **P-09** | ¿Cobro de la constancia? | **No** en Fase 1 (FA-07). |
| **P-10** | ¿Quién descarga el PDF? | **Empleado dueño + RR. HH.**; jefatura solo si tiene permiso de vista; salario sujeto a `ViewCompensation`. |

**Preguntas residuales (no bloquean; ratificadas por defecto, ajustables):**
- **P-12.** ¿El conjunto de tipos que **imprimen salario** es {salario, embajada}? (Default RN-17; confirmable por tipo.)
- **P-13.** ¿El **firmante** es único por empresa o se requiere por tipo/unidad desde Fase 1? (Default: único por empresa — FA-09.)
- **P-14.** ¿La constancia de salario debe mostrar **bruto, neto o ambos**? (Default: bruto fijo; neto depende de deducciones/nómina.)

---

## 18. Recomendaciones del Analista de Negocio

- **R-1 (slice vertical, con la consulta como centro):** entidad `PersonnelFileCertificateRequest` + **4 catálogos** seed SV + **ingreso autoservicio** + **bandeja company-scoped (listado+filtros+detalle)** + **export Excel** + **generación PDF** + **configuración de empresa** + emisión/entrega + permisos + auditoría + localización. **Clonar el esqueleto de `EconomicAidRequest`** y el **render de `JobProfilePdfRenderer`**.
- **R-2 (qué NO construir — disciplina de alcance):** **sin** monto/moneda, **sin** motor de aprobación, **sin** motor de plantillas editables (layout en código), **sin** firma electrónica/QR, **sin** notificaciones, **sin** cobro.
- **R-3 (acoplamiento de compensación):** tratar `ViewCompensation` como **gate de primera clase** para constancias con salario; inyectar el salario **server-side**; validar disponibilidad antes de emitir (E-17).
- **R-4 (Fase 2):** **firma electrónica + folio/QR verificable**, **notificaciones** al emitir/entregar, y —si el negocio lo pide— **plantillas editables** (Scriban) y **override de firmante por tipo/unidad**.
- **R-5 (revisión legal):** los **layouts semilla** (sobre todo salario y embajada) deben pasar **revisión legal/RR. HH.** antes de producción.
- **R-6 (reutilización máxima):** catálogos por key, autoservicio, adjuntos, auditoría, concurrencia, **exportación** y **render PDF** ya existen; el costo incremental real es **el modelo + la bandeja + el seed + el mapper de generación + la config de empresa**.
- **R-7 (faseo DENTRO de Fase 1 para reducir riesgo):** **(a)** catálogos+seed → modelo/migración → ingreso autoservicio → **bandeja+export** (cierra el requerimiento nombrado); **(b)** generación PDF + configuración de empresa + emisión/entrega + adjunto. Permite entregar la **consulta** primero.
- **R-8 (alineación de producto):** integrar la constancia a la sección **"Solicitudes"** del portal junto a reclamos médicos y ayuda económica (vista unificada "Mis solicitudes").
- **R-9 (siguiente paso):** redactar el **plan técnico** (`docs/technical/plan-tecnico-consulta-solicitudes-constancia.md`) por PRs, espejando el de ayuda económica.

---

## 19. Decisiones (RATIFICADAS 2026-06-28)

- **D-01 — Alcance:** **slice vertical completo** (ingreso autoservicio + procesamiento/emisión + consulta/bandeja + export). *(P-01.)*
- **D-02 — Autoservicio:** el empleado **crea/consulta/cancela/descarga lo propio**; RR. HH. gestiona todo.
- **D-03 — Sin dinero:** la constancia **no** tiene monto/moneda/desembolso.
- **D-04 — Sin aprobación:** ciclo **lineal** (Solicitada→En proceso→Emitida→Entregada; Rechazada/Anulada).
- **D-05 — Documento generado + override:** el documento emitido es el **PDF generado** por el sistema; se permite **carga manual** opcional (override). *(Modificada vs v1 — ahora se genera.)*
- **D-06 — Destinatario:** "Dirigida a" **obligatorio para embajada**, opcional para el resto.
- **D-07 — Idioma:** `LanguageCode` (es/en); inglés disponible para embajada (RN-19).
- **D-08 — Bandeja company-scoped:** la consulta es un **listado a nivel de empresa** (patrón reporting) con **export Excel** vía `ReportExportDeliveryService`.
- **D-09 — (sustituida por D-18)** — *medio de entrega y propósito pasan a catálogos (ver D-18).*
- **D-10 — (ampliada por D-19)** — *seed de tipos ampliado (ver D-19).*
- **D-11 — Permisos dedicados:** `ViewCertificateRequests` / `ManageCertificateRequests` + **controlador(es) dedicado(s)**; autoservicio en handlers.
- **D-12 — Adjunto de salida:** `FilePurpose.CertificateRequestDocument`; descarga con **URL temporal**; el empleado **descarga**, no sube.
- **D-13 — Sin notificaciones en Fase 1** (FA-06).
- **D-14 — Tiempo de respuesta:** `ResponseTimeDays` **derivado** (emisión − solicitud).
- **D-15 — Generación en Fase 1 vía LAYOUT EN CÓDIGO por tipo** (QuestPDF/`DocumentModel`, patrón `JobProfilePdfRenderer`); **NO** motor de merge-fields editable. *(P-02/P-11.)*
- **D-16 — Fuente del salario:** concepto de compensación `Ingreso`/`Fijo` (activo) del **assignment activo+primario**; sin dato → **no se genera** (E-17).
- **D-17 — Configuración por empresa:** membrete/logo, ciudad, **firmante** (nombre/cargo) y pie/texto legal (**tenant-scoped**, `CompanyCertificateSettings`); el cuerpo por tipo es **estructural**.
- **D-18 — Medio de entrega y propósito = catálogos *country-scoped* con seed SV** (P-07); "dirigida a" sigue libre.
- **D-19 — Tipos ampliados en el seed SV** (P-03): salario, laboral, embajada, tiempo laborado, no descuento, carta de recomendación.
- **D-20 — Acoplamiento de permisos:** las constancias que **imprimen salario** (salario y embajada) requieren **`ManageCertificateRequests` + `ViewCompensation`**; salario **server-side**; **sin firma electrónica** en Fase 1 (PDF informativo/para firma manual).

---

> **Próximos pasos:** (1) §17/§19 **ratificados**; (2) redactar el plan técnico por PRs (catálogos+seed → entidad/migración → ingreso autoservicio → **bandeja+export** → generación PDF + configuración de empresa → emisión/entrega+adjuntos → permisos/localización/pruebas); (3) integrar a la sección "Solicitudes" del portal (frontend).
