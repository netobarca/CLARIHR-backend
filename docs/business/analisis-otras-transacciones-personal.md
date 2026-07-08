# Análisis de negocio — Otras transacciones de personal (reconocimientos, amonestaciones y disponibilidad de tiempos)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | Otras transacciones de personal: reconocimientos, amonestaciones (con suspensión y descuento), 3 catálogos maestros y consulta de disponibilidad de tiempos |
| **Fecha** | 2026-07-05 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código |
| **Estado** | **RATIFICADO por el negocio (2026-07-05)** — decisiones **D-01…D-18 ratificadas sin ajustes** (todas las propuestas aceptadas) y respuestas **P-01…P-14 incorporadas** (§17). Nota de P-14: el negocio declara que **no hay multas** en su régimen → sin advertencia legal configurable en F1; la capacidad de descuento se mantiene (P-02/P-03) para los marcos que la permitan |
| **Naturaleza del módulo** | Semi-greenfield **con costura documental**: el catálogo de acciones de personal ya siembra `AMONESTACION`/`SUSPENSION` y hoy pueden registrarse como asiento documental genérico; el módulo estructurado (catálogos propios, flujo custodiado, descuento, suspensión con fechas, consulta) no existe |
| **Documentos hermanos** | [`analisis-vacaciones-incapacidades-empleado.md`](analisis-vacaciones-incapacidades-empleado.md) (REQ-001 — aporta el patrón de maestros por empresa con plantilla y 3 de las 5 fuentes de la consulta) · [`analisis-tiempo-compensatorio-empleado.md`](analisis-tiempo-compensatorio-empleado.md) (REQ-002 — cuarta familia de ausencias) · [`plan-tecnico-otras-transacciones-personal.md`](../technical/plan-tecnico-otras-transacciones-personal.md) (plan de implementación de este módulo) |
| **Documentos relacionados** | `analisis-ayuda-economica-empleado.md` (patrón de flujo de una decisión + anti-autoaprobación) · `analisis-sustitucion-autorizaciones-empleado.md` (hallazgo: no existe motor de aprobaciones genérico) · `analisis-retiro-definitivo-empleado.md` (asientos automáticos + permiso `Authorize*` que excluye Admin) |

---

## Contexto del cambio (requerimiento original)

El levantamiento solicita un módulo de **"Otras transacciones de personal"** con las opciones necesarias para ingresar reconocimientos y amonestaciones, junto con sus catálogos. Seis opciones principales:

1. **Reconocimientos** — ingreso de los reconocimientos que los empleados reciben por su buen trabajo, mayor producción, entre otros. **Deberá utilizar un flujo de autorizaciones para su aplicación en el expediente.**
2. **Tipos de reconocimiento** — catálogo para clasificar los reconocimientos con base en las políticas de la institución; se usa en el formulario de Reconocimientos.
3. **Amonestaciones** — ingreso de las amonestaciones que se aplican a los empleados por faltas cometidas. **Podrán tener o no descuento en planilla.** Deberá utilizar un flujo de autorizaciones para su aplicación en el expediente.
4. **Tipos de amonestación** — catálogo para clasificar las amonestaciones; **podrán configurarse para que apliquen suspensión de labores sin goce de sueldo**; se usa en el formulario de Amonestaciones.
5. **Causas de amonestaciones** — catálogo para clasificar las amonestaciones y **asociarlas a un tipo de descuento en caso de que aplique**; se usa en el formulario de Amonestaciones.
6. **Consulta de disponibilidad de tiempos de empleados** — consulta de las acciones registradas **con ausencia** de los empleados en un periodo establecido: **permisos, incapacidades, vacaciones, suspensiones y finalización de contratos temporales**, todos en un rango de tiempo.

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **No existe módulo estructurado, pero sí una costura documental ya sembrada.** Verificado exhaustivamente: cero entidades/endpoints/DTOs de reconocimientos o amonestaciones (las únicas coincidencias son ejemplos en comentarios del módulo de transacciones fuera de nómina, `GeneralCatalogItems.cs:283`). Sin embargo, `ACTION_TYPE_CATALOG` ya siembra `AMONESTACION=-9477`, `SUSPENSION=-9478`, `PERMISO=-9479` y `REINTEGRO=-9480` (`GlobalCatalogSeedData.cs:736-750`), y el endpoint manual de acciones de personal (`PersonnelActions.cs:210`) permite hoy registrar una amonestación como **asiento documental genérico** (con vigencias `EffectiveFromUtc/ToUtc` e incluso `Amount`). Lo que falta —y es el módulo— es la capa estructurada: catálogos institucionales propios, flujo de autorización custodiado, suspensión con fechas verificables, descuento asociado a causa, adjuntos y consultas. Nota: `RECONOCIMIENTO` **no** existe en el catálogo de acciones (se siembra nuevo).
2. **El "flujo de autorizaciones" no existe como motor genérico** (mismo hallazgo del análisis de sustituciones): el precedente probado es el **flujo de una decisión** con estado híbrido + PATCH de resolución + anti-autoaprobación 403 (`EconomicAidRequests.Handlers.cs:371-380`) y permisos `Authorize*` con `RequireAssertion` que **excluye Admin** (patrón `AuthorizeRetirement`). Se propone exactamente eso: `EN_REVISION → APLICADA / RECHAZADA` (+`ANULADA`), con anti-autoaprobación **doble** (ni el empleado sujeto ni quien registró pueden decidir). Autorización multi-nivel configurable = Fase 2 (P-01).
3. **"Descuento en planilla" = registro + exportación, nunca cálculo**: la nómina se procesa fuera de CLARIHR (comentarios de dominio `PersonnelFileCompensation.cs:8-10`) y **no existe un catálogo de "tipos de descuento"** — los descuentos del sistema son conceptos del catálogo país `CompensationConceptTypeCatalogItem` con `Nature = Egreso` (`CompensationConceptTypeCatalogItem.cs:13`, enum `CompensationNature`). Propuesta: la **causa** referencia opcionalmente un concepto de egreso de ese catálogo (cero catálogos monetarios nuevos); la amonestación aplicada con descuento registra monto + concepto (snapshot) y se **exporta como insumo** para la planilla externa. No se escribe `PersonnelFilePayrollTransaction` (ledger inmutable de sincronización externa — trampa documentada) ni se generan `PersonnelFileCompensationConcept` automáticos en F1.
4. **La suspensión ya tiene vocabulario en el sistema, pero ningún productor**: el estado de empleo `SUSPENDIDO` está sembrado (`GlobalCatalogSeedData.cs:178`, `-9101`) y el ActionType `SUSPENSION` existe, pero nada los produce automáticamente. Propuesta F1: la amonestación cuyo **tipo** habilita suspensión lleva **rango de fechas sin goce**; genera asiento `SUSPENSION` con vigencias; **no cambia el estado del empleado automáticamente** (precedente P-18 de REQ-001; el cambio manual sigue disponible) — automatismo con reversión al vencimiento sería un job nuevo, F2 si se ratifica (P-05).
5. **La consulta de disponibilidad de tiempos es un agregador de 5 familias del que hoy solo existen 2 fuentes**: (a) **suspensiones** — nacen con este módulo; (b) **finalización de contratos temporales** — 100 % derivable ya: la plaza guarda `ContractTypeCode` y sus `StartDate/EndDate` son la vigencia del contrato (`PersonnelFileEmployee.cs:182-205`), y `CONTRACT_TYPE_CATALOG` trae el flag **`IsTemporary`** (`PLAZO_FIJO`, `POR_OBRA`, `EVENTUAL`, `APRENDIZAJE`, `TEMPORAL` → true; `GlobalCatalogSeedData.cs:693-702`). **Vacaciones e incapacidades llegan con REQ-001** (aún no construido) y los **"permisos" no tienen módulo**: REQ-001 los dejó explícitamente fuera (`analisis-vacaciones-incapacidades-empleado.md:188`); lo más cercano serán la lactancia (REQ-001) y las ausencias por tiempo compensatorio (REQ-002). Propuesta: diseñar la consulta **por fuentes conectables** con contrato estable desde F1 y degradación documentada (P-09), y secuenciarla al final (D-18).
6. **Marco legal SV heterogéneo → todo paramétrico (Anexo A.1, verificado 2026-07-05)**: en el sector **privado** el CT prohíbe las multas/descuentos como sanción y la suspensión disciplinaria vía reglamento interno es de **hasta 1 día por falta** (más de 1 y hasta 30 días requiere autorización de la Dirección General de Inspección de Trabajo — Arts. 302-306 CT); en el sector **público** la Ley de Servicio Civil (Art. 41) sí contempla la **multa deducible del sueldo** y suspensiones sin goce de **hasta 5 días por mes y 15 por año** impuestas por las jefaturas. Consecuencia de diseño: **nada se codifica como regla legal** — tipos, causas, procedencia del descuento y topes viven en maestros por empresa; el sistema registra, advierte y exporta; la procedencia legal es responsabilidad del empleador (riesgo documentado, P-14).
7. **Cuidado con la nomenclatura**: ya existe el módulo "Transacciones **fuera de nómina**" (`PersonnelFileOffPayrollTransaction`, registro monetario directo sin flujo). Este levantamiento — "**Otras transacciones de personal**" — es otra cosa (reconocimientos/amonestaciones/consulta). Los nombres de UI y permisos deben diferenciarlos sin ambigüedad.

---

## Estado actual verificado en el código (línea base "as-is")

### Lo que YA existe y este módulo reutiliza

| Pieza | Dónde | Uso en este módulo |
|---|---|---|
| Asientos de acciones de personal: `PersonnelFilePersonnelAction` (`ActionTypeCode`, `ActionStatusCode`, `ActionDateUtc`, `EffectiveFromUtc/ToUtc`, `Description`, `Reference`, `Amount`, `IsSystemGenerated`) + alta manual + escrituras automáticas con estado `APLICADA` desde retiro/reversión/recontratación/liquidación | `PersonnelFileEmployee.cs:571-631`; manual `Employment/PersonnelActions.cs:210`; automáticas p. ej. `ExecuteRetirementRequest.cs:54-55` | Asiento automático (`IsSystemGenerated`) al **aplicar** un reconocimiento o amonestación, en la misma transacción (RF-006/RF-009) |
| `ACTION_TYPE_CATALOG` con `AMONESTACION=-9477`, `SUSPENSION=-9478`, `PERMISO=-9479`, `REINTEGRO=-9480` (…hasta `LIQUIDACION=-9484`; REQ-001 reserva `-9485…-9489`) | `GlobalCatalogSeedData.cs:736-750` | Se **reutilizan** `-9477`/`-9478` para los asientos; solo se siembra `RECONOCIMIENTO` (nuevo) |
| `ACTION_STATUS_CATALOG` `-9490…-9496` (`BORRADOR`…`APROBADA`/`RECHAZADA`/`APLICADA`/`ANULADA`) | `GlobalCatalogSeedData.cs:755-761` | Estados del **asiento** (se escriben `APLICADA`/`ANULADA`); el registro del módulo lleva catálogo TPH propio (D-15) |
| Flujo de una decisión con estado híbrido + anti-autoaprobación (`SelfApprovalForbidden` 403 cuando `LinkedUserPublicId == currentUser.UserId`) y PATCH de resolución/cancelación | `PersonnelFileEmployee.cs:1678-1880` (ayuda económica); `EconomicAidRequests.Handlers.cs:331-380`; `EconomicAidRequestsController.cs:151-216` | Molde exacto del flujo de autorización pedido (D-03/D-04) |
| Receta de permisos RBAC (declaración + provisioning + gates fail-closed con fallback `Admin`/`ManageAdministration`) y permisos `Authorize*` que **excluyen** Admin (patrón `AuthorizeRetirement`; ejemplo `AuthorizeRehire`) | `ProvisioningConstants.cs:30-93`; `PersonnelFileCommon.cs:82`; `PersonnelFileAuthorizationService.cs:362` | 7 codes nuevos (D-05) |
| Stack de adjuntos compartido: `StoredFile` + enum `FilePurpose` + espejos por módulo (SAS de subida directa + confirm + read-URL) | `StoredFile.cs:5`; `FileEnums.cs:12`; `MedicalClaimDocuments*.cs`, `EconomicAidRequestDocuments*.cs` | Actas/descargos de amonestación y diplomas de reconocimiento (D-12); 2 purposes nuevos |
| Catálogo país de conceptos de compensación con `Nature` (`Ingreso`/`Egreso`), `DeductionClass` (`Ley`/`Interno`/`Externo`) — los "tipos de descuento" del sistema son sus filas de egreso | `CompensationConceptTypeCatalogItem.cs:13-86`; `CompensationEnums.cs:4-22`; GET `api/v1/compensation-concept-types` | Referencia del "tipo de descuento" de la **causa** (D-06/D-10) — sin catálogo monetario nuevo |
| Plaza con `ContractTypeCode` opcional y `StartDate/EndDate` como **vigencia del contrato** (comentario explícito) + `CONTRACT_TYPE_CATALOG` con flag `IsTemporary` (`-9460…-9467`) | `PersonnelFileEmployee.cs:182-205`; `GlobalCatalogSeedData.cs:693-702`; `GeneralCatalogItems.cs:833-853` | Fuente **derivable hoy** de "finalización de contratos temporales" en la consulta (RF-013) |
| Estado de empleo `SUSPENDIDO` sembrado (sin productor automático) | `GlobalCatalogSeedData.cs:178` (`-9101`) | Coherencia con la suspensión (D-09): el estado existe para gestión manual |
| Maestros por empresa con seeder de defaults cableado al provisioning (evoluciona a `LeaveTemplateSeeder` + `load-template` idempotente en REQ-001 §3.1) | `OrgStructureCatalogSeedService.cs:10-55`; `CompanyProvisioningService.cs:151-153` | Los 3 catálogos del módulo son maestros por empresa con plantilla (D-06) |
| Bandeja + exportación tabular (query paginada `StatusCounts`, export xlsx/csv/json con rate limiting, filas en español) | `SettlementsBandeja.cs`; `ReportExportFileWriter.cs`; `SettlementsReportingController.cs` | Bandejas por familia + exportación insumo de planilla + export de la consulta de tiempos (RF-012/RF-013) |
| Gates de autogestión (lecturas `View… OR isSelf` vía `LinkedUserPublicId`) | `Common/PersonnelFileEmployeeHandlerBases.cs` | Visibilidad del propio historial aplicado (D-13, P-06) |
| Registro monetario puntual de referencia (estilo): `PersonnelFileOffPayrollTransaction` (monto, periodo `Year/Month`, corrección referenciada) y ledger `PersonnelFilePayrollTransaction` (inmutable, sync externa) | `PersonnelFileEmployee.cs:1442`, `:645` | **Solo referencia de estilo**: ninguno se escribe desde este módulo (RN-14) |
| Catálogo de motivos de sustitución (`PERMISO`, `LICENCIA`, `VACACIONES`…) | `GlobalCatalogSeedData.cs:471-476` | Correlación funcional (cobertura durante suspensiones); sin acople técnico |

### Lo que NO existe (verificado exhaustivamente en `src/`, tests y contrato)

- Ningún módulo/entidad/endpoint/DTO de **reconocimientos, amonestaciones, sanciones o suspensiones estructuradas** (las coincidencias de "reconocimientos" son ejemplos en comentarios del módulo fuera de nómina; `ManualSuspension` es de suscripciones de empresa).
- Ningún **catálogo de tipos de reconocimiento, tipos de amonestación ni causas** (ni país ni empresa).
- Ningún **motor de aprobaciones genérico/multi-nivel** (hallazgo ya documentado en el análisis de sustituciones); el máximo existente es el flujo de una decisión de ayuda económica.
- Ningún **catálogo de "tipos de descuento" dedicado**: los egresos son filas del catálogo de conceptos (`Nature=Egreso`).
- Ningún **motor de planilla**: los efectos monetarios (descuento, días sin goce) solo pueden registrarse y **exportarse** como insumo.
- Ningún módulo de **permisos/licencias generales** (REQ-001 los excluyó explícitamente) → hueco de la fuente "permisos" de la consulta (G-04, P-09).
- Ninguna **consulta agregadora de ausencias** por rango de fechas.
- **REQ-001 y REQ-002 no están construidos**: 3 de las 5 familias de la consulta dependen de su llegada.
- **Espacio de seeds**: piso verificado `-9846`; REQ-001 reserva `-9850…-9862` y `-9485…-9489`; REQ-002 propone `-9865…-9871`; **trampa vigente**: `-9490…-9496` ocupados por `ACTION_STATUS_CATALOG`. Bloque propuesto para este módulo: **≤ -9875** (Anexo A.3).

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha detectada | Propuesta de resolución |
|---|---|---|
| G-01 | "Flujo de autorizaciones" requerido sin motor genérico en el sistema | **Flujo de una decisión** por registro: `EN_REVISION → APLICADA / RECHAZADA` (+`ANULADA`), decisión con permiso `Authorize*` (excluye Admin) y **anti-autoaprobación doble** (sujeto y registrador). Multi-nivel configurable = F2 (P-01) |
| G-02 | "Descuento en planilla" sin nómina interna ni catálogo de descuentos | La **causa** referencia opcionalmente un **concepto de egreso** del catálogo país existente; la amonestación registra flag + monto + concepto (snapshot al aplicar) y todo se **exporta como insumo**; no se escriben ledgers de planilla (RN-14) |
| G-03 | Suspensión sin modelo (solo ActionType documental y estado `SUSPENDIDO` sin productor) | Bloque de suspensión **en la amonestación** cuando su tipo lo habilita: rango de fechas sin goce + días derivados; asiento `SUSPENSION` con vigencias; **sin cambio automático** del estado del empleado (P-05) |
| G-04 | La consulta pide "permisos" y no existe (ni existirá con REQ-001) un módulo de permisos generales | Consulta diseñada **por fuentes conectables** con contrato estable: F1 = suspensiones + fin de contratos temporales; REQ-001 conecta vacaciones/incapacidades/lactancia; REQ-002 conecta ausencias compensatorias; un futuro módulo de permisos generales se conecta sin romper contrato (P-09) |
| G-05 | `RECONOCIMIENTO` no existe en `ACTION_TYPE_CATALOG` (la amonestación y suspensión sí) | Sembrar `RECONOCIMIENTO` (nuevo, bloque del módulo); **reutilizar** `AMONESTACION=-9477` y `SUSPENSION=-9478` |
| G-06 | Catálogos "con base a las políticas de la institución" | 3 **maestros por empresa** editables con plantilla precargada + `load-template` idempotente (patrón `LeaveTemplateSeeder` de REQ-001, Anexo A.2) |
| G-07 | El asiento manual documental (`AMONESTACION`/`SUSPENSION`) seguirá disponible y podría duplicar información | Coexistencia declarada: los asientos del módulo son `IsSystemGenerated`; el asiento manual queda para actos documentales/históricos; la consulta de tiempos lee **de las entidades**, nunca de asientos (sin doble conteo) (RN-16) |
| G-08 | Marco legal heterogéneo (privado: multas prohibidas, suspensión reglamentaria ≤ 1 día; público: multa y suspensión hasta 5 días/mes) | **Nada codificado como ley**: procedencia del descuento y topes de suspensión son política por empresa; advertencia configurable no bloqueante como máximo (P-04/P-14) |
| G-09 | Historial pre-sistema (amonestaciones/reconocimientos en papel) al adoptar el módulo | Registro retroactivo permitido (fecha del hecho pasada) **pasando el mismo flujo**; si el volumen de migración lo exige, alta directa `APLICADA` documentada se evalúa en la ratificación (P-11) |

---

## Decisiones — D-01…D-18 (**RATIFICADAS por el negocio, 2026-07-05**)

> ✅ **Todas ratificadas sin ajustes** junto con las respuestas P-01…P-14 (§17). D-01 se ratificó con el alcance F1 completo tal como se propuso (incluida la consulta de disponibilidad con las fuentes existentes). Siguen los precedentes ratificados de módulos anteriores donde aplican.

| # | Tema | Decisión propuesta |
|---|---|---|
| D-01 | Fases | **F1**: 3 maestros por empresa + reconocimientos y amonestaciones end-to-end (flujo de una decisión, suspensión, descuento documental, adjuntos, asientos) + bandejas/exports + consulta de disponibilidad con las fuentes existentes. **F2**: autorización multi-nivel (si se ratifica), notificaciones, reincidencia/prescripción, cambio automático de estado por suspensión, valorización/integración adicional con planilla, conexión de fuentes futuras de la consulta |
| D-02 | Modelado | **Entidades propias del expediente**: `PersonnelFileRecognition` y `PersonnelFileDisciplinaryAction` (precedente de todos los módulos estructurados) — NO se enriquece el asiento genérico; el asiento es la **consecuencia** (automático, `IsSystemGenerated`) de aplicar el registro |
| D-03 | Flujo de autorización | **Una decisión**: crear → `EN_REVISION`; decidir → `APLICADA` (genera asiento en la misma transacción) o `RECHAZADA` (motivo obligatorio); `ANULADA` desde `EN_REVISION` (retiro del trámite) o desde `APLICADA` (revocación con motivo que **anula el asiento**). Estados híbridos (constantes canónicas + catálogo TPH) preparados para agregar pasos F2 de forma aditiva |
| D-04 | Anti-autoaprobación | **Doble**: quien decide no puede ser (a) el empleado sujeto del expediente (`LinkedUserPublicId`, precedente ayuda económica) ni (b) quien registró la transacción (`RegisteredByUserId`) → 403 `SELF_APPROVAL_FORBIDDEN` |
| D-05 | Permisos | 7 codes: `PersonnelFiles.ViewRecognitions`/`ManageRecognitions`/`AuthorizeRecognitions`, `ViewDisciplinaryActions`/`ManageDisciplinaryActions`/`AuthorizeDisciplinaryActions` y `ViewTimeAvailability` (consulta). `View`/`Manage` con receta estándar (fallback `Admin`/`ManageAdministration`); **`Authorize*` con `RequireAssertion` que excluye Admin** (patrón `AuthorizeRetirement`). El negocio puede colapsar granularidad en la ratificación (P-01) |
| D-06 | Catálogos | 3 **maestros por empresa** editables con plantilla + `load-template` idempotente: **Tipo de reconocimiento** (código, descripción, orden, baja lógica) · **Tipo de amonestación** (+ flag `appliesSuspension`) · **Causa de amonestación** (+ referencia **opcional** a concepto de egreso del catálogo país `compensation-concept-types`) (Anexo A.2, P-12) |
| D-07 | Reconocimiento | Registro sobre el expediente: tipo (maestro), **fecha del hecho** (≤ hoy), **detalle/motivo obligatorio**, monto + moneda **opcionales e informativos** (premio económico; sin acople a planilla en F1 — P-08), plaza opcional, adjuntos (diploma/memo), flujo D-03, asiento `RECONOCIMIENTO` al aplicar |
| D-08 | Amonestación | Registro sobre el expediente: tipo (maestro), **causa** (maestro), **fecha de la falta** (≤ hoy), **relato de los hechos obligatorio**, descuento: flag + monto > 0 + concepto de egreso (default de la causa, snapshot al aplicar), suspensión (solo si el tipo la habilita): fechas desde/hasta (futuras permitidas) + días derivados, plaza opcional, adjuntos (acta/descargo), flujo D-03, asientos al aplicar: `AMONESTACION` siempre + `SUSPENSION` (con vigencias) cuando aplica |
| D-09 | Suspensión y estado | La suspensión aplicada **no** cambia `EmploymentStatusCode` automáticamente (precedente P-18 de REQ-001); el estado `SUSPENDIDO` (`-9101`) queda para gestión manual de RRHH; automatismo inicio/fin (job) = F2 si se ratifica (P-05). La suspensión alimenta la consulta de tiempos y el export de insumo de planilla (días sin goce) |
| D-10 | Montos y planilla | Sin motor de nómina: el descuento y los días sin goce son **registro + exportación** (insumo para la planilla externa). No se escribe `PersonnelFilePayrollTransaction` ni se crean `PersonnelFileCompensationConcept` automáticos en F1 (la generación automática del egreso configurado puede evaluarse en F2 — P-02/P-03) |
| D-11 | Asientos | Automáticos `IsSystemGenerated` con estado `APLICADA` en la misma transacción de la decisión; la revocación (`ANULADA` desde `APLICADA`) pone el asiento en `ANULADA` en la misma transacción. El asiento **manual** sigue disponible para actos documentales; la consulta de tiempos lee de entidades (RN-16) |
| D-12 | Adjuntos | **En F1 para ambas familias** (el respaldo documental es central en amonestaciones —acta, descargo— y útil en reconocimientos —diploma): 2 purposes nuevos (`RecognitionDocument`, `DisciplinaryActionDocument`) sobre el stack espejo existente; pendiente de despliegue conocido (purpose en appsettings base + contenedor). El negocio puede diferir reconocimientos a F2 (P-07) |
| D-13 | Autogestión | F1 **solo lectura** `isSelf`: el empleado ve sus reconocimientos y amonestaciones **en estado `APLICADA`** (transparencia del expediente; los `EN_REVISION`/`RECHAZADA` no se le muestran); sin escritura self; notificaciones = F2 (P-06) |
| D-14 | Consulta de disponibilidad | Endpoint agregador **por empresa**, solo lectura, rango de fechas obligatorio, filtros por empleado/categoría/fuente; fila = empleado + plaza + **categoría** (`VACACION`/`INCAPACIDAD`/`PERMISO`/`SUSPENSION`/`FIN_CONTRATO_TEMPORAL`) + fechas + días + estado + referencia; **payload mínimo no sensible** (sin diagnósticos, montos ni relatos); permiso dedicado `ViewTimeAvailability`; export xlsx/csv/json. Fuentes F1: suspensiones aplicadas + plazas con contrato `IsTemporary` y `EndDate` en rango; el resto se conecta al llegar cada módulo (P-09/P-10) |
| D-15 | Estados | Catálogo TPH nuevo `PERSONNEL_TRANSACTION_STATUS_CATALOG` (wire `personnel-transaction-statuses`): `EN_REVISION`, `APLICADA`, `RECHAZADA`, `ANULADA` — compartido por ambas entidades (el plan técnico decide 1 TPH compartido vs 2 espejo). Patrón híbrido (constantes canónicas + validación por catálogo). No se reutiliza `ACTION_STATUS_CATALOG` (pertenece al asiento) |
| D-16 | Retiro | Perfil `RETIRADO` bloqueado para **crear** y para **aplicar** (la decisión sobre un retirado solo admite `RECHAZADA`/`ANULADA`); los históricos aplicados quedan visibles. El retiro del empleado **no se bloquea** por registros `EN_REVISION` (quedan decidibles solo a rechazo/anulación) |
| D-17 | Reincidencia | F1 **sin** cómputo de reincidencia ni prescripción/caducidad de amonestaciones: el historial filtrable por tipo/causa/fechas la hace consultable manualmente; contador automático, escala progresiva sugerida o exclusión por antigüedad = F2 (P-13) |
| D-18 | Secuenciación | Registrar como **REQ-003** al final del backlog. Los submódulos de reconocimientos/amonestaciones dependen de REQ-001 solo por el **patrón de plantilla** (PR-1) — podrían adelantarse extrayendo el seeder si el negocio lo prioriza; la **consulta** rinde valor completo únicamente tras REQ-001 (y suma REQ-002). Recomendación: construir después de REQ-001 y REQ-002, con la consulta al cierre |

---

## 1. Resumen del producto o requerimiento

Se construirá el módulo de **Otras transacciones de personal** de CLARIHR: la administración, dentro del expediente del empleado, de los **reconocimientos** (méritos) y las **amonestaciones** (faltas, con posible suspensión sin goce de sueldo y posible descuento en planilla), ambos sujetos a un **flujo de autorización** antes de aplicarse al expediente, más una **consulta transversal de disponibilidad de tiempos** que muestra quién estará ausente y por qué en un rango de fechas.

**Qué se construye.** Seis capacidades:

1. **Reconocimientos**: registro del mérito (tipo, fecha, motivo, monto informativo opcional, adjuntos) que un autorizador aprueba o rechaza; al aprobarse queda **aplicado al expediente** con asiento automático.
2. **Amonestaciones**: registro de la falta (tipo, causa, hechos, adjuntos), con **suspensión de labores sin goce** cuando el tipo la habilita (rango de fechas) y **descuento en planilla** opcional (monto + concepto de egreso asociado a la causa); mismo flujo de autorización y asiento automático (más asiento de suspensión).
3. **Tipos de reconocimiento**, **tipos de amonestación** (con flag de suspensión) y **causas de amonestación** (con concepto de descuento opcional): tres **maestros por empresa** editables con plantilla inicial.
4. **Consulta de disponibilidad de tiempos**: vista agregada por empresa de las ausencias/indisponibilidades en un rango — permisos, incapacidades, vacaciones, suspensiones y fines de contrato temporal — con exportación.

**Problema que resuelve.** Hoy los méritos y faltas se llevan fuera del sistema o como asientos documentales sueltos: no hay catálogos institucionales, ni autorización trazable, ni constancia estructurada de suspensiones y descuentos para la planilla externa; y no existe una vista única de "quién no estará disponible" para planificar cobertura.

**Objetivo principal.** Que todo reconocimiento y amonestación pase por autorización, quede aplicado y trazado en el expediente con sus efectos (suspensión, descuento) exportables como insumo exacto de planilla, y que RRHH y las jefaturas puedan consultar la disponibilidad de tiempos del personal en cualquier periodo.

---

## 2. Objetivos del negocio

1. **Debido proceso y trazabilidad disciplinaria**: ninguna amonestación o reconocimiento llega al expediente sin autorización de un tercero facultado (anti-autoaprobación), con motivo, evidencia adjunta y auditoría completa — defensa documental del empleador y del empleado.
2. **Cumplimiento de la política institucional**: tipos, causas, procedencia del descuento y uso de suspensión son **parametrización por empresa** (reglamento interno / normativa pública aplicable), nunca constantes del sistema.
3. **Insumo exacto para la planilla externa**: exportación de descuentos aprobados (monto + concepto) y días de suspensión sin goce del periodo — la planilla externa aplica; CLARIHR documenta.
4. **Expediente completo y consistente**: méritos y faltas como acciones de personal junto a contrataciones, retiros y liquidaciones, reutilizando el vocabulario ya sembrado (`AMONESTACION`, `SUSPENSION`).
5. **Planificación de cobertura**: una sola consulta de disponibilidad de tiempos (ausencias + fines de contrato temporal) para anticipar huecos de personal en un rango de fechas.
6. **Gestión del talento**: historial de reconocimientos consultable (insumo para evaluaciones, ascensos y retención) y historial disciplinario filtrable (insumo para decisiones de reincidencia — manual en F1).
7. **Transparencia con el empleado**: autogestión de solo lectura del propio historial aplicado (propuesto, P-06).

---

## 3. Alcance funcional

### Fase 1 — MVP (este análisis)

- **Catálogos y configuración**: 3 maestros por empresa (tipos de reconocimiento, tipos de amonestación con `appliesSuspension`, causas con concepto de egreso opcional) con plantilla editable + `load-template`; catálogo TPH de estados; ActionType `RECONOCIMIENTO`; 7 permisos RBAC; 2 purposes de storage.
- **Reconocimientos**: crear/editar (`EN_REVISION`), decidir (aplicar/rechazar con anti-autoaprobación doble), anular/revocar, adjuntos, asiento automático, consulta en ficha + autogestión lectura.
- **Amonestaciones**: ídem + bloque de suspensión (fechas sin goce, días derivados) + bloque de descuento (flag, monto, concepto snapshot de la causa) + asiento `SUSPENSION` adicional.
- **Bandejas + exportaciones**: query paginada por familia a nivel empresa (`StatusCounts`, filtros por estado/tipo/causa/fechas/empleado) y exportaciones xlsx/csv/json, incluida la **exportación insumo de planilla** (descuentos y suspensiones del rango).
- **Consulta de disponibilidad de tiempos**: agregador por empresa (rango obligatorio) sobre las fuentes existentes — suspensiones aplicadas y fines de contrato temporal (derivado de plazas `IsTemporary` + `EndDate`) — con contrato preparado para conectar vacaciones/incapacidades/lactancia (REQ-001), ausencias compensatorias (REQ-002) y permisos generales (futuro); export.

### Fase 2 — Evoluciones (contrato preparado, fuera de este MVP)

- Autorización **multi-nivel** (jefatura → RRHH/dirección) y bandeja del autorizador con notificaciones (P-01).
- **Reincidencia**: contador por causa/periodo, escala progresiva sugerida, prescripción/exclusión de amonestaciones antiguas (P-13).
- Cambio **automático** del estado del empleado a `SUSPENDIDO` con reversión al vencimiento (P-05).
- Generación automática del **concepto de egreso** configurado o integración adicional con planilla (P-02/P-03); reconocimientos con incentivo económico hacia planilla (P-08).
- Conexión de nuevas fuentes a la consulta (permisos generales cuando exista su módulo) y vista calendario.
- `REINTEGRO` post-suspensión como flujo asistido (hoy el ActionType existe para uso manual).

---

## 4. Fuera de alcance

- **Cálculo o aplicación real en planilla** del descuento y de los días sin goce (la nómina es externa; CLARIHR registra y exporta — RN-14). No se escriben `PersonnelFilePayrollTransaction` ni conceptos automáticos.
- **Motor de aprobaciones genérico/multi-nivel configurable** (F2 si se ratifica P-01); F1 entrega el flujo de una decisión.
- **Procedimiento completo de debido proceso** (citaciones, audiencias de descargo, plazos legales): el módulo da soporte documental (hechos, adjuntos, decisión), no workflow procesal.
- **Módulo de permisos/licencias generales**: la consulta de tiempos solo **consume** fuentes; no crea la fuente "permisos" (P-09).
- **Reincidencia automática y prescripción** de amonestaciones (F2, P-13).
- **Cambio automático de estado del empleado** por suspensión y su reversión programada (F2, P-05).
- **Notificaciones** por correo (decisiones, vencimientos de suspensión).
- Validaciones **legales bloqueantes** (topes de días, procedencia de multas): solo parametrización y, si se ratifica, advertencia no bloqueante (P-04/P-14).
- Importador masivo de historial (la adopción usa registro retroactivo por el flujo normal — G-09/P-11).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador de empresa** | Mantiene los 3 maestros (y su plantilla), configura y asigna permisos |
| **Gestor de RRHH** (con `Manage…`) | Registra y edita reconocimientos/amonestaciones, gestiona adjuntos, anula en revisión, consulta y exporta |
| **Autorizador** (jefatura/dirección con `Authorize…`) | Decide (aplica o rechaza) las transacciones `EN_REVISION`; revoca aplicadas con motivo; sujeto a anti-autoaprobación doble |
| **Consulta de RRHH / Auditor** (con `View…`) | Solo lectura de fichas, bandejas y exportaciones |
| **Jefaturas / Planificadores** (con `ViewTimeAvailability`) | Consultan la disponibilidad de tiempos del personal para planificar cobertura |
| **Empleado (autogestión)** | Lectura de su propio historial **aplicado** (F1); en F2 recibiría notificaciones |
| **Finanzas / Analista de planilla** | Exporta el insumo del periodo: descuentos aprobados y días de suspensión sin goce |
| **Sistema de planilla externa** | Consume las exportaciones; aplica descuentos y días sin goce |

---

## 6. Requerimientos funcionales

> Agrupados en 5 grupos (A: configuración y catálogos · B: reconocimientos · C: amonestaciones · D: transversales · E: consulta de tiempos). Prioridades: Alta = imprescindible F1; Media = F1 deseable.

### Grupo A — Configuración y catálogos

### RF-001 - Maestro de tipos de reconocimiento (por empresa)

**Descripción:** CRUD por empresa de los tipos de reconocimiento: código, descripción, orden y baja lógica. Plantilla mínima precargada al aprovisionar (Anexo A.2), editable, con `load-template` idempotente para tenants existentes.

**Reglas de negocio:**
- Código único por empresa (comparación normalizada); descripción obligatoria.
- Baja lógica: tipo inactivo no seleccionable en registros nuevos; los históricos conservan referencia.
- La plantilla nunca pisa ediciones (idempotente por código).

**Criterios de aceptación:**
- CRUD completo con If-Match, `[ResourceActions]`/AllowedActions y auditoría; segunda corrida de `load-template` = 0 cambios; registro nuevo con tipo inactivo/inexistente → 422 bilingüe.

**Prioridad:** Alta
**Dependencias:** Patrón de maestros por empresa con plantilla (REQ-001 PR-1).

### RF-002 - Maestro de tipos de amonestación (por empresa, con suspensión)

**Descripción:** CRUD por empresa de los tipos de amonestación: código, descripción, **flag `appliesSuspension`** (habilita el bloque de suspensión sin goce en el formulario), orden y baja lógica. Plantilla editable (`VERBAL`, `ESCRITA`, `SUSPENSION_SIN_GOCE`…).

**Reglas de negocio:**
- Igual base que RF-001.
- `appliesSuspension` gobierna el formulario (RN-05): sin el flag, el registro no admite fechas de suspensión; con el flag, las exige.
- Cambiar el flag del tipo **no** altera amonestaciones existentes (el registro manda una vez creado).

**Criterios de aceptación:**
- Amonestación con fechas de suspensión sobre un tipo sin flag → 422; tipo con flag exige fechas → 422 si faltan.

**Prioridad:** Alta
**Dependencias:** RF-001 (misma familia de maestros).

### RF-003 - Maestro de causas de amonestación (por empresa, con concepto de descuento)

**Descripción:** CRUD por empresa de las causas: código, descripción, **referencia opcional a un concepto de egreso** del catálogo país (`compensation-concept-types`, `Nature=Egreso`) como "tipo de descuento" asociado, orden y baja lógica. Plantilla editable.

**Reglas de negocio:**
- Igual base que RF-001.
- El concepto referenciado debe existir, estar activo y ser de naturaleza egreso → 422 en caso contrario.
- La causa **sugiere** el concepto al formulario de amonestación; el registro toma **snapshot** (código + nombre) al aplicarse (RN-06); cambiar la causa después no recalcula históricos.

**Criterios de aceptación:**
- Causa con concepto de ingreso → 422; amonestación aplicada conserva el snapshot aunque la causa cambie.

**Prioridad:** Alta
**Dependencias:** Catálogo de conceptos existente (solo lectura).

### RF-004 - Estados, tipo de acción y permisos del módulo

**Descripción:** Sembrar `PERSONNEL_TRANSACTION_STATUS_CATALOG` (`EN_REVISION`, `APLICADA`, `RECHAZADA`, `ANULADA`), el ActionType `RECONOCIMIENTO`, y declarar los 7 permisos (D-05) con la receta completa; registrar los 2 purposes de storage (D-12).

**Reglas de negocio:**
- Patrón híbrido (constantes canónicas + catálogo para i18n/UI).
- IDs de semilla en bloque nuevo **≤ -9875** (Anexo A.3), verificados contra `GlobalCatalogSeedData` al abrir el PR (no tocar `-9865…-9871` propuestos por REQ-002 ni `-9490…-9496` de `ACTION_STATUS_CATALOG`); **reutilizar** `AMONESTACION=-9477` y `SUSPENSION=-9478`.
- `Authorize*` con `RequireAssertion` que excluye Admin; `View`/`Manage` con fallback estándar; gates fail-closed + governance tests.

**Criterios de aceptación:**
- Migración `HasData` idempotente; usuario sin permiso → 403 en cada endpoint; Admin sin `Authorize*` no puede decidir; empleado autogestionado accede solo a lo propio aplicado.

**Prioridad:** Alta
**Dependencias:** D-05/D-11/D-15; verificación de IDs libres.

### Grupo B — Reconocimientos

### RF-005 - Crear y editar reconocimiento (con adjuntos)

**Descripción:** Alta por RRHH sobre el expediente: tipo (maestro), fecha del hecho, detalle/motivo, monto + moneda opcionales (informativos), plaza opcional y adjuntos (diploma, memo). Nace `EN_REVISION`; editable mientras siga en revisión.

**Reglas de negocio:**
- Solo `ManageRecognitions`; If-Match en ediciones; perfil `RETIRADO` → 422 (RN-09).
- Tipo activo del maestro de la empresa (RN-04); fecha del hecho ≤ hoy (RN-10); detalle obligatorio; monto, si viaja, > 0 con moneda.
- Adjuntos con el stack espejo (purpose `RecognitionDocument`), gateados por los permisos del módulo.
- Solo registros `EN_REVISION` son editables; `APLICADA`/`RECHAZADA`/`ANULADA` → 422/409 (RN-01).

**Criterios de aceptación:**
- POST → 201 `EN_REVISION` con `publicId` y ETag; edición tras aplicar → 422; adjunto sube por SAS y confirma.

**Prioridad:** Alta
**Dependencias:** RF-001, RF-004.

### RF-006 - Decidir reconocimiento (aplicar / rechazar)

**Descripción:** PATCH de decisión sobre un reconocimiento `EN_REVISION`: **aplicar** (queda `APLICADA` y genera el asiento `RECONOCIMIENTO` en la misma transacción) o **rechazar** (motivo obligatorio).

**Reglas de negocio:**
- Solo `AuthorizeRecognitions` (excluye Admin); **anti-autoaprobación doble** (RN-02): 403 si quien decide es el empleado sujeto o quien registró.
- Aplicación → asiento `IsSystemGenerated` tipo `RECONOCIMIENTO`, estado `APLICADA`, fecha de acción = fecha del hecho (RN-03).
- Sobre perfil `RETIRADO` solo se admite rechazar (D-16).

**Criterios de aceptación:**
- Decisión por el registrador → 403; aplicar → asiento visible en el expediente; rechazar sin motivo → 422.

**Prioridad:** Alta
**Dependencias:** RF-005.

### RF-007 - Anular y revocar reconocimiento

**Descripción:** Anulación con motivo desde `EN_REVISION` (retiro del trámite, por `Manage`) y **revocación** con motivo desde `APLICADA` (por `Authorize`), que anula el asiento en la misma transacción.

**Reglas de negocio:**
- Motivo obligatorio; baja lógica, nunca borrado físico (RN-11); revocación → asiento a `ANULADA` (RN-07).

**Criterios de aceptación:**
- Revocar → registro `ANULADA` + asiento `ANULADA`, ambos trazables con quién/cuándo/motivo.

**Prioridad:** Alta
**Dependencias:** RF-006.

### Grupo C — Amonestaciones

### RF-008 - Crear y editar amonestación (suspensión + descuento + adjuntos)

**Descripción:** Alta por RRHH: tipo (maestro), causa (maestro), fecha de la falta, relato de los hechos, **bloque de suspensión** (si el tipo la habilita): fecha inicio/fin sin goce + días derivados; **bloque de descuento**: flag + monto + concepto de egreso (default de la causa); plaza opcional; adjuntos (acta, descargo). Nace `EN_REVISION`.

**Reglas de negocio:**
- Solo `ManageDisciplinaryActions`; perfil `RETIRADO` → 422; solo `EN_REVISION` editable.
- Tipo y causa activos del maestro de la empresa (RN-04); fecha de la falta ≤ hoy (RN-10); hechos obligatorios.
- Suspensión: exclusiva de tipos con `appliesSuspension` (RN-05); `startDate ≤ endDate`; fechas futuras permitidas (cumplimiento programado); días = calendario del rango (P-04); **sin solape** con otra suspensión vigente del mismo empleado (RN-18).
- Descuento: si flag → monto > 0 obligatorio; concepto de egreso opcional (default de la causa, editable entre conceptos de egreso activos) (RN-06).
- Adjuntos purpose `DisciplinaryActionDocument`.

**Criterios de aceptación:**
- Suspensión sobre tipo sin flag → 422; solape de suspensiones → 422 con el registro en conflicto; descuento sin monto → 422.

**Prioridad:** Alta
**Dependencias:** RF-002, RF-003, RF-004.

### RF-009 - Decidir amonestación (aplicar / rechazar)

**Descripción:** PATCH de decisión sobre una amonestación `EN_REVISION`: aplicar (asienta `AMONESTACION` y, si lleva suspensión, **también** `SUSPENSION` con las vigencias del rango, misma transacción, snapshot del concepto de descuento) o rechazar con motivo.

**Reglas de negocio:**
- Solo `AuthorizeDisciplinaryActions` (excluye Admin); anti-autoaprobación doble (RN-02).
- Al aplicar: asiento `AMONESTACION` (fecha de la falta) + asiento `SUSPENSION` con `EffectiveFromUtc/ToUtc` cuando aplica (RN-03); snapshot `deductionConceptTypeCode` + nombre (RN-06); el estado del empleado **no cambia** (RN-13).
- Desde la aplicación, la suspensión es fuente de la consulta de tiempos y del export de insumo (RN-15).

**Criterios de aceptación:**
- Aplicar con suspensión → 2 asientos con vigencias correctas; aplicar sin suspensión → 1 asiento; decisión del propio sujeto → 403.

**Prioridad:** Alta
**Dependencias:** RF-008.

### RF-010 - Anular y revocar amonestación

**Descripción:** Anulación desde `EN_REVISION` y revocación desde `APLICADA` (p. ej. apelación resuelta a favor) con motivo; la revocación anula **todos** los asientos generados (amonestación y suspensión) en la misma transacción.

**Reglas de negocio:**
- Motivo obligatorio; baja lógica; revocación → asientos a `ANULADA` (RN-07); el registro revocado sale de la consulta de tiempos y de los exports de insumo (RN-15).

**Criterios de aceptación:**
- Revocar amonestación con suspensión → registro y 2 asientos `ANULADA`; la suspensión desaparece de la consulta de tiempos.

**Prioridad:** Alta
**Dependencias:** RF-009.

### Grupo D — Transversales

### RF-011 - Consulta en ficha + autogestión de lectura

**Descripción:** Listado paginado por expediente de reconocimientos y de amonestaciones (filtros por estado, tipo, causa, rango de fechas) para RRHH (`View…`/`Manage…`); el empleado (`isSelf`) ve **solo los propios en `APLICADA`**.

**Reglas de negocio:**
- Lectura RRHH: todos los estados; lectura self: solo `APLICADA` del propio expediente (D-13); tercero sin permiso → 403.
- Datos sensibles (relato de hechos, montos) visibles solo con permiso de la familia correspondiente.

**Criterios de aceptación:**
- Empleado vinculado ve sus aplicadas y no ve las `EN_REVISION`; otro expediente → 403.

**Prioridad:** Alta
**Dependencias:** RF-005, RF-008.

### RF-012 - Bandejas de empresa + exportaciones (incluye insumo de planilla)

**Descripción:** (a) Query paginada por familia a nivel empresa (filtros: estado, tipo, causa, empleado, rango; `StatusCounts`); (b) export xlsx/csv/json de cada bandeja; (c) **export de insumo de planilla**: descuentos de amonestaciones aplicadas (empleado, causa, concepto snapshot, monto) y suspensiones sin goce (empleado, fechas, días) del rango solicitado.

**Reglas de negocio:**
- `View…` por familia para bandeja/export; export con rate limiting y límite síncrono existente; filas en español (patrón liquidación).
- El insumo excluye revocadas/anuladas; incluye solo `APLICADA` (RN-15).

**Criterios de aceptación:**
- `POST /companies/{companyId}/…/query` pagina y cuenta por estado; el export de insumo de un rango cuadra contra las amonestaciones aplicadas del rango en tests de integración.

**Prioridad:** Alta
**Dependencias:** RF-006, RF-009.

### Grupo E — Consulta de disponibilidad de tiempos

### RF-013 - Consulta de disponibilidad de tiempos de empleados

**Descripción:** Query agregadora por empresa con **rango de fechas obligatorio**: devuelve las indisponibilidades que **intersectan** el rango, unificadas como filas `{empleado, plaza, categoría, fechaInicio, fechaFin, días, estado, fuente, referencia}` con categorías `SUSPENSION` y `FIN_CONTRATO_TEMPORAL` en F1, y `VACACION`/`INCAPACIDAD`/`PERMISO` al conectarse REQ-001/REQ-002/futuro módulo de permisos. Filtros por empleado, categoría y unidad organizativa; paginada; export xlsx/csv/json.

**Reglas de negocio:**
- Permiso dedicado `ViewTimeAvailability` (D-14); **payload mínimo no sensible**: categoría + fechas + estado, sin diagnósticos, montos ni relatos (P-10).
- Fuentes F1: suspensiones de amonestaciones `APLICADA` (fechas del bloque de suspensión); plazas activas con `ContractTypeCode` cuyo catálogo tenga `IsTemporary = true` y `EndDate` dentro del rango (categoría `FIN_CONTRATO_TEMPORAL`, con fecha inicio = fecha fin = `EndDate`).
- Intersección de rangos (no contención): una suspensión que empieza antes y termina dentro del rango aparece (RN-15).
- Cada fuente se conecta con **contrato estable** (la fila no cambia de forma): documentación explícita de fuentes activas por versión (degradación de G-04).

**Criterios de aceptación:**
- Rango ausente → 400; suspensión que intersecta parcialmente aparece con sus fechas reales; plaza `PLAZO_FIJO` con `EndDate` en el rango aparece; plaza `INDEFINIDO` no; revocar la amonestación la saca de la consulta.

**Prioridad:** Alta (fuentes F1) / se completa con REQ-001/REQ-002
**Dependencias:** RF-009 (suspensiones); catálogo de contratos existente; REQ-001/REQ-002 para las demás fuentes.

---

## 7. Requerimientos no funcionales

- **Seguridad**: la amonestación es dato laboral **sensible**: lecturas con `ViewDisciplinaryActions` o `isSelf` (solo aplicadas); la consulta de tiempos expone payload mínimo con permiso propio; `Authorize*` excluye Admin (separación de funciones); gates fail-closed por handler además de la política de controlador; 403 sin enmascaramiento para terceros.
- **Auditoría**: `CreatedUtc`/`ModifiedUtc`, quién registró/decidió/anuló y cuándo, motivos obligatorios en rechazo/anulación/revocación, snapshots (concepto de descuento), baja lógica universal, asientos automáticos trazables (`IsSystemGenerated`).
- **Concurrencia/API**: convenciones del repo — `api/v1`, If-Match (faltante → 400, obsoleto → 409), token rotativo, DELETE → `parentConcurrencyToken`, Guid `publicId`, enums como strings, errores bilingües en `extensions.code`. La decisión re-verifica el estado dentro de la transacción (dos autorizadores concurrentes: el segundo recibe 409/422).
- **Rendimiento**: bandejas y consulta de tiempos paginadas con índices por `(tenant, empresa, estado, fechas)`; la consulta agrega por unión de queries acotadas por rango (sin full scans); exports con límite síncrono y rate limiting existentes.
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId` en todas las entidades; sin jobs nocturnos en F1.
- **Usabilidad**: errores accionables (tipo sin suspensión, solape con registro en conflicto, concepto no-egreso); catálogos con `sortOrder`; categorías de la consulta como códigos estables para el FE.
- **Mantenibilidad**: reglas en módulo puro (`PersonnelTransactionRules` o equivalente por familia) con tests unitarios y **paridad de localización**; sin números legales codificados; OpenAPI sin drift; guía FE.
- **Compatibilidad**: cambios 100 % aditivos (entidades, catálogos y permisos nuevos; ActionTypes existentes reutilizados sin tocar su semántica).
- **Accesibilidad**: (frontend) formularios con validación bilingüe y consulta de tiempos navegable/exportable; se documenta en la guía FE.

---

## 8. Historias de usuario

### HU-001 - Registrar un reconocimiento
Como **gestor de RRHH**, quiero **registrar un reconocimiento con su tipo, motivo y evidencia**, para **que un autorizador lo apruebe y quede aplicado en el expediente del empleado**.

**Criterios de aceptación:**
- Dado un tipo activo, cuando registro el reconocimiento, entonces queda `EN_REVISION` y no aparece aún como asiento del expediente.
- Dado un reconocimiento `EN_REVISION`, cuando el autorizador lo aplica, entonces se asienta `RECONOCIMIENTO` en el expediente en la misma operación.

### HU-002 - Autorizar una amonestación con suspensión
Como **autorizador**, quiero **revisar y aplicar una amonestación cuyo tipo conlleva suspensión sin goce**, para **que la falta, sus fechas de suspensión y su efecto queden formalizados**.

**Criterios de aceptación:**
- Dada una amonestación `EN_REVISION` con suspensión del 10 al 12, cuando la aplico, entonces quedan los asientos `AMONESTACION` y `SUSPENSION` (10→12) y la suspensión aparece en la consulta de tiempos.
- Dado que yo registré esa amonestación, cuando intento decidirla, entonces recibo 403.

### HU-003 - Amonestación con descuento en planilla
Como **gestor de RRHH**, quiero **asociar el descuento de una amonestación a la causa y su concepto de egreso**, para **que Finanzas reciba el insumo exacto (concepto + monto) del periodo**.

**Criterios de aceptación:**
- Dada una causa con concepto `DESCUENTO_INTERNO`, cuando registro la amonestación con descuento de $25, entonces el formulario precarga el concepto y, al aplicarse, el export de insumo del rango la incluye con concepto y monto.

### HU-004 - Rechazar y revocar
Como **autorizador**, quiero **rechazar una transacción improcedente o revocar una aplicada por apelación**, para **que el expediente refleje solo lo que corresponde, con motivo trazable**.

**Criterios de aceptación:**
- Dado un rechazo sin motivo, entonces recibo 422; con motivo, el registro queda `RECHAZADA` sin asiento.
- Dada una amonestación aplicada con suspensión, cuando la revoco con motivo, entonces registro y asientos quedan `ANULADA` y la suspensión sale de la consulta de tiempos.

### HU-005 - Consultar mi historial
Como **empleado autogestionado**, quiero **ver mis reconocimientos y amonestaciones aplicados**, para **conocer mi expediente sin depender de RRHH**.

**Criterios de aceptación:**
- Dado mi usuario vinculado, cuando consulto mi historial, entonces veo solo los registros `APLICADA` de mi expediente; los `EN_REVISION` no aparecen; otro expediente → 403.

### HU-006 - Planificar cobertura con la consulta de tiempos
Como **jefatura con permiso de disponibilidad**, quiero **consultar quién estará ausente o termina contrato en un rango**, para **planificar la cobertura del área**.

**Criterios de aceptación:**
- Dado un rango quincenal, cuando consulto, entonces veo suspensiones que intersectan el rango y contratos temporales con fecha fin dentro de él, cada uno con su categoría, fechas y días; el detalle sensible no viaja.

### HU-007 - Mantener los catálogos institucionales
Como **administrador de empresa**, quiero **definir tipos de reconocimiento, tipos de amonestación (con o sin suspensión) y causas (con o sin descuento)**, para **que los formularios reflejen la política de mi institución**.

**Criterios de aceptación:**
- Dada la plantilla precargada, cuando la edito y recargo `load-template`, entonces no se duplica ni pisa mis ediciones.
- Dado un tipo sin `appliesSuspension`, cuando intento registrar suspensión, entonces recibo 422.

### HU-008 - Exportar el insumo de planilla
Como **analista de planilla**, quiero **exportar los descuentos y suspensiones sin goce aprobados del periodo**, para **aplicarlos en la nómina externa sin transcripciones manuales**.

**Criterios de aceptación:**
- Dado un rango de periodo, cuando exporto el insumo, entonces obtengo empleado, causa, concepto (snapshot), monto y días de suspensión, excluyendo revocadas.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | Ciclo de vida: `EN_REVISION → APLICADA / RECHAZADA`; `ANULADA` desde `EN_REVISION` (retiro) o desde `APLICADA` (revocación). Solo `EN_REVISION` es editable; las transiciones fuera del ciclo → 422/409 |
| RN-02 | **Anti-autoaprobación doble**: quien decide no puede ser el empleado sujeto (`LinkedUserPublicId`) ni quien registró (`RegisteredByUserId`) → 403 |
| RN-03 | Aplicar genera el/los asientos de personal en la **misma transacción** (`RECONOCIMIENTO`; `AMONESTACION` + `SUSPENSION` con vigencias cuando aplica), `IsSystemGenerated`, estado `APLICADA` |
| RN-04 | Tipo y causa deben ser activos y del maestro de la **empresa** del expediente; inactivos no seleccionables en registros nuevos; históricos conservan referencia y snapshot |
| RN-05 | La suspensión es exclusiva de tipos con `appliesSuspension`; exige `startDate ≤ endDate` (futuras permitidas); días derivados del rango (calendario, P-04) |
| RN-06 | Descuento: flag → monto > 0 obligatorio; el concepto de egreso default viene de la causa y se **snapshotea** (código + nombre) al aplicar; cambios posteriores del catálogo/causa no recalculan históricos |
| RN-07 | Rechazo, anulación y revocación exigen **motivo**; la revocación anula además todos los asientos generados, en la misma transacción |
| RN-08 | Baja lógica universal: nada se borra físicamente; anuladas/rechazadas quedan trazables con quién/cuándo/motivo |
| RN-09 | Perfil `RETIRADO`: sin altas nuevas y sin aplicar pendientes (solo rechazar/anular) — precedente RN-18 de retiro |
| RN-10 | Fecha del hecho / de la falta ≤ hoy (no se reconoce ni amonesta a futuro); las fechas de **suspensión** sí pueden ser futuras (cumplimiento programado) |
| RN-11 | Adjuntos por el stack espejo con purposes propios; herencia de permisos del módulo padre |
| RN-12 | Maestros por empresa: código único normalizado; plantilla idempotente que nunca pisa ediciones |
| RN-13 | Ni la amonestación ni la suspensión cambian `EmploymentStatusCode` automáticamente (precedente P-18 de REQ-001); `SUSPENDIDO` queda para gestión manual |
| RN-14 | **No se escriben ledgers de planilla** (`PersonnelFilePayrollTransaction` es sincronización externa; sin conceptos automáticos en F1): el efecto monetario viaja solo por exportación de insumo |
| RN-15 | Consulta de tiempos y exports de insumo consideran **solo registros `APLICADA` vigentes**; la intersección con el rango es por solape de fechas (no contención); revocar excluye |
| RN-16 | El asiento manual documental coexiste; los asientos del módulo se distinguen por `IsSystemGenerated`; la consulta de tiempos lee de **entidades**, nunca de asientos (sin doble conteo) |
| RN-17 | El monto del reconocimiento es informativo (no genera efecto de planilla en F1) |
| RN-18 | Sin solape de suspensiones vigentes del mismo empleado; al llegar REQ-001/REQ-002, la validación cruzada contra vacaciones/incapacidades/ausencias compensatorias se especifica condicionada a su presencia (mismo corte que REQ-002 RN-05) |

---

## 10. Flujos principales

### Flujo 1 — Reconocimiento end-to-end
1. RRHH abre la ficha del empleado y elige "Nuevo reconocimiento".
2. Ingresa tipo, fecha del hecho, motivo, monto informativo opcional y adjunta el diploma/memo.
3. El sistema valida catálogo y fechas; guarda `EN_REVISION` (201 + ETag).
4. El autorizador revisa la bandeja de pendientes y **aplica** (o rechaza con motivo); el sistema verifica anti-autoaprobación.
5. Al aplicar: asiento `RECONOCIMIENTO` en el expediente (misma transacción); el empleado lo ve en autogestión.

### Flujo 2 — Amonestación con suspensión y descuento
1. RRHH elige "Nueva amonestación": tipo `SUSPENSION_SIN_GOCE` (habilita el bloque de suspensión), causa `INASISTENCIA_INJUSTIFICADA` (precarga el concepto de egreso).
2. Ingresa fecha de la falta, relato de los hechos, fechas de suspensión (p. ej. 3 días hábiles próximos), flag de descuento + monto; adjunta el acta.
3. El sistema valida tipo/causa/fechas/solapes y guarda `EN_REVISION`.
4. El autorizador aplica: asientos `AMONESTACION` + `SUSPENSION` (vigencias del rango) y snapshot del concepto.
5. La suspensión aparece en la consulta de disponibilidad; el export de insumo del periodo incluye descuento y días sin goce.

### Flujo 3 — Rechazo
1. El autorizador encuentra improcedente la transacción `EN_REVISION`.
2. Rechaza con motivo; el registro queda `RECHAZADA`, sin asiento, trazable; RRHH puede registrar una nueva si corresponde.

### Flujo 4 — Revocación (apelación)
1. El empleado apela por la vía institucional y gana; el autorizador abre la amonestación `APLICADA`.
2. Revoca con motivo; registro y asientos quedan `ANULADA`; la suspensión sale de la consulta y del insumo de planilla.

### Flujo 5 — Consulta de disponibilidad de tiempos
1. La jefatura/planificador abre la consulta y define el rango (p. ej. la próxima quincena) y filtros.
2. El sistema une las fuentes disponibles (F1: suspensiones aplicadas + contratos temporales con fin en el rango; +vacaciones/incapacidades/lactancia con REQ-001; +ausencias compensatorias con REQ-002) por intersección de fechas.
3. Muestra filas por empleado/categoría con días; exporta a xlsx si se requiere.

### Flujo 6 — Mantenimiento de catálogos
1. El administrador revisa las plantillas precargadas de los 3 maestros y las adapta a su reglamento interno (tipos con/sin suspensión; causas con/sin concepto de descuento).
2. Para tenants existentes ejecuta `load-template` (idempotente).

### Flujo 7 — Adopción (historial en papel)
1. RRHH registra retroactivamente los antecedentes relevantes (fecha del hecho pasada) y los pasa por el flujo normal de autorización.
2. El expediente queda completo y auditable desde la adopción (si el volumen exige alta directa, ver P-11).

---

## 11. Flujos alternativos y excepciones

- **Decisión por el sujeto o el registrador** → 403 `SELF_APPROVAL_FORBIDDEN` (ambas ramas).
- **Suspensión sobre tipo sin `appliesSuspension`** → 422; **tipo con flag sin fechas** → 422.
- **Solape de suspensión** con otra vigente del empleado → 422 con el registro en conflicto.
- **Descuento sin monto** (o monto ≤ 0) → 422; **concepto no-egreso o inactivo** en causa/registro → 422.
- **Tipo/causa inactivo o de otra empresa** → 422 por código.
- **Fecha del hecho futura** → 422 (las fechas de suspensión futuras sí se permiten).
- **Editar/decidir un registro no `EN_REVISION`** → 422/409 (transición inválida); **dos decisiones concurrentes** → la segunda 409/422.
- **Rechazo/anulación/revocación sin motivo** → 422.
- **Perfil `RETIRADO`**: alta → 422; aplicar → 422 (solo rechazar/anular permitidos).
- **If-Match ausente** → 400; **obsoleto** → 409.
- **Usuario sin permiso** → 403 (gestión, decisión, bandejas, consulta de tiempos); **empleado consultando otro expediente** → 403; **empleado consultando sus `EN_REVISION`** → no aparecen (filtro de visibilidad self).
- **Consulta de tiempos sin rango** → 400; **fuentes ausentes** (REQ-001/REQ-002 no construidos): la consulta responde con las fuentes activas y lo documenta (modo degradado de G-04).
- **Adjunto sin confirmar subida** → el documento no queda activo (patrón SAS existente).

---

## 12. Datos requeridos

> Convenciones del repo aplican a todas las entidades: `long Id` interno + `Guid publicId` externo, `TenantId`, `CreatedUtc`/`ModifiedUtc`, `isActive` (baja lógica), `concurrencyToken` rotativo (If-Match), factoría `Create(...)` + mutadores custodiados. Se listan solo los campos de negocio.

### Entidad: Tipo de reconocimiento (`RecognitionType` — maestro por empresa, plantilla editable)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Código único por empresa (normalizado) | Identificador y descripción del tipo |
| sortOrder | Entero | Sí | — | Orden de presentación |

### Entidad: Tipo de amonestación (`DisciplinaryActionType` — maestro por empresa)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Código único por empresa (normalizado) | Identificador y descripción |
| appliesSuspension | Booleano | Sí (default false) | — | Habilita/exige el bloque de suspensión sin goce (RN-05) |
| sortOrder | Entero | Sí | — | Orden de presentación |

### Entidad: Causa de amonestación (`DisciplinaryActionCause` — maestro por empresa)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Texto | Sí | Código único por empresa (normalizado) | Identificador y descripción |
| deductionConceptTypeCode | Código catálogo | No | Concepto país activo con `Nature=Egreso` | "Tipo de descuento" asociado (RF-003) |
| sortOrder | Entero | Sí | — | Orden de presentación |

### Entidad: Reconocimiento (`PersonnelFileRecognition` — sub-registro del expediente)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| recognitionTypePublicId | Guid | Sí | Tipo activo de la empresa | Tipo de reconocimiento |
| eventDate | Fecha | Sí | ≤ hoy (RN-10) | Fecha del hecho/otorgamiento |
| detail | Texto (1000) | Sí | No vacío | Motivo/detalle del mérito |
| amount / currencyCode | Decimal (12,2) / Código | No | > 0 si viaja; moneda requerida con monto | Premio económico **informativo** (RN-17, P-08) |
| assignedPositionPublicId | Guid | No | Plaza del empleado | Plaza asociada (opcional) |
| statusCode | Código catálogo | Sí | `EN_REVISION`/`APLICADA`/`RECHAZADA`/`ANULADA` (híbrido) | Estado del flujo |
| registeredByUserId | Guid | Sí | Usuario autenticado | Quién registró |
| decidedByUserId / decidedUtc / decisionNote | Guid / Fecha / Texto (500) | Al decidir | Anti-autoaprobación (RN-02); motivo obligatorio al rechazar | Auditoría de la decisión |
| annulmentReason / annulledByUserId / annulledUtc | Texto (500) / Guid / Fecha | Al anular/revocar | Motivo obligatorio | Auditoría de anulación/revocación |
| personnelActionPublicId | Guid | Al aplicar | — | Asiento `RECONOCIMIENTO` generado (RN-03) |
| notes | Texto (500) | No | — | Observaciones |

### Entidad: Amonestación (`PersonnelFileDisciplinaryAction` — sub-registro del expediente)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| disciplinaryActionTypePublicId | Guid | Sí | Tipo activo de la empresa | Tipo de amonestación |
| disciplinaryActionCausePublicId | Guid | Sí | Causa activa de la empresa | Causa de la falta |
| incidentDate | Fecha | Sí | ≤ hoy (RN-10) | Fecha de la falta |
| factsDetail | Texto (2000) | Sí | No vacío | Relato de los hechos |
| hasPayrollDeduction | Booleano | Sí (default false) | — | Si conlleva descuento en planilla |
| deductionAmount / currencyCode | Decimal (12,2) / Código | Condicional | > 0 y moneda si `hasPayrollDeduction` (RN-06) | Monto del descuento (insumo, no cálculo) |
| deductionConceptTypeCode / deductionConceptNameSnapshot | Código / Texto | No (snapshot al aplicar) | Concepto país `Nature=Egreso` activo | "Tipo de descuento" (default de la causa, RN-06) |
| suspensionStartDate / suspensionEndDate | Fecha | Condicional | Solo tipos `appliesSuspension`; inicio ≤ fin; sin solape (RN-05/RN-18) | Rango de suspensión sin goce (futuro permitido) |
| suspensionDays | Entero | Derivado | Días calendario del rango (P-04) | Días de suspensión |
| assignedPositionPublicId | Guid | No | Plaza del empleado | Plaza asociada (opcional) |
| statusCode | Código catálogo | Sí | `EN_REVISION`/`APLICADA`/`RECHAZADA`/`ANULADA` (híbrido) | Estado del flujo |
| registeredByUserId | Guid | Sí | Usuario autenticado | Quién registró |
| decidedByUserId / decidedUtc / decisionNote | Guid / Fecha / Texto (500) | Al decidir | Anti-autoaprobación (RN-02); motivo al rechazar | Auditoría de la decisión |
| annulmentReason / annulledByUserId / annulledUtc | Texto (500) / Guid / Fecha | Al anular/revocar | Motivo obligatorio | Auditoría de anulación/revocación |
| personnelActionPublicId / suspensionActionPublicId | Guid | Al aplicar | — | Asientos `AMONESTACION` y `SUSPENSION` generados (RN-03) |
| notes | Texto (500) | No | — | Observaciones |

### Entidades: Documentos (`RecognitionDocument` / `DisciplinaryActionDocument` — stack espejo)

Espejo del patrón existente (`MedicalClaimDocument`): metadatos + `StoredFile` compartido, purposes `RecognitionDocument` / `DisciplinaryActionDocument`, subida por SAS + confirm, read-URL server-side; permisos heredados del módulo padre.

### Consulta derivada: Disponibilidad de tiempos (`TimeAvailabilityRow` — no persistida)

| Campo | Tipo de dato | Descripción |
|---|---|---|
| personnelFilePublicId / employeeName | Guid / Texto | Empleado |
| assignedPositionPublicId / positionName | Guid / Texto | Plaza (cuando la fuente la aporta) |
| categoryCode | Código | `SUSPENSION` · `FIN_CONTRATO_TEMPORAL` (F1) · `VACACION` · `INCAPACIDAD` · `PERMISO` (al conectarse fuentes) |
| startDate / endDate / days | Fecha / Fecha / Entero | Rango que intersecta la consulta y días |
| statusCode / sourceModule / referencePublicId | Código / Código / Guid | Estado del registro fuente, módulo de origen y referencia navegable |

### Catálogos generales nuevos (semilla `HasData`, IDs tentativos ≤ -9875 — Anexo A.3)

- `PERSONNEL_TRANSACTION_STATUS_CATALOG` (wire `personnel-transaction-statuses`): `EN_REVISION`, `APLICADA`, `RECHAZADA`, `ANULADA`.
- Nuevo código en `ACTION_TYPE_CATALOG`: `RECONOCIMIENTO` (los asientos de amonestación/suspensión **reutilizan** `-9477`/`-9478`).

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Planilla externa** | Saliente (archivos) | Export de insumo del periodo: descuentos aprobados (empleado, causa, **concepto snapshot**, monto) y suspensiones sin goce (fechas, días). No se escribe `PersonnelFilePayrollTransaction` ni se generan conceptos automáticos (RN-14) |
| **Catálogo de conceptos de compensación** | Interna (lectura) | La causa referencia conceptos país `Nature=Egreso` (`api/v1/compensation-concept-types`) como "tipo de descuento" |
| **Expediente (acciones de personal)** | Interna | Asientos automáticos `RECONOCIMIENTO`/`AMONESTACION`/`SUSPENSION` con estados `APLICADA`/`ANULADA` (RN-03/RN-07) |
| **REQ-001 (vacaciones/incapacidades/lactancia)** | Interna (futura) | Fuentes `VACACION`/`INCAPACIDAD`/(`PERMISO` lactancia) de la consulta de tiempos; validación cruzada de solapes (RN-18) |
| **REQ-002 (tiempo compensatorio)** | Interna (futura) | Fuente `PERMISO` (ausencias compensatorias) de la consulta de tiempos |
| **Plazas / contratos** | Interna (lectura) | Fuente `FIN_CONTRATO_TEMPORAL`: plazas con contrato `IsTemporary` y `EndDate` en rango (sin cambios en la entidad) |
| **Storage (Azure Blob)** | Interna | 2 purposes nuevos (`RecognitionDocument`, `DisciplinaryActionDocument`) + contenedores; pendiente de despliegue estándar |
| **Sustitución de autorizaciones** | Correlación funcional | Cobertura del puesto durante suspensiones; sin acople técnico |
| **Correo / notificaciones** | Fase 2 | Aviso al autorizador (pendientes) y al empleado (aplicadas) |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de empresa | Maestros (3) + `load-template`; asignación de permisos | **No decide** transacciones salvo que tenga `Authorize*` (la política excluye el fallback Admin en la decisión) |
| Gestor de RRHH | `ManageRecognitions` y/o `ManageDisciplinaryActions` (+ lecturas implícitas, adjuntos) | No puede decidir lo que él mismo registró (RN-02); ediciones solo `EN_REVISION` |
| Autorizador | `AuthorizeRecognitions` / `AuthorizeDisciplinaryActions` | Anti-autoaprobación doble; motivos obligatorios; revocaciones trazables |
| Consulta / Auditor | `ViewRecognitions` / `ViewDisciplinaryActions` | Solo lectura de fichas, bandejas y exports |
| Jefatura / Planificador | `ViewTimeAvailability` | Solo la consulta agregada (payload mínimo); sin acceso al detalle disciplinario |
| Empleado (autogestión) | Sin permisos RBAC: gate `isSelf` | Solo su expediente y solo registros `APLICADA` (lectura) |
| Finanzas / Planilla externa | `ViewDisciplinaryActions` (export insumo) | Solo lectura/exportación |

---

## 15. Criterios de aceptación generales

1. **Ratificación previa**: ✅ cumplida (2026-07-05) — D-01…D-18 aprobadas sin ajustes y P-01…P-14 respondidas (§17); el plan técnico se deriva de este documento ratificado.
2. Reglas de flujo/validación como **módulo puro** con suite unitaria (casos dorados Anexo A.4) y test de paridad de localización.
3. Suite de integración completa (CRUD + flujo con anti-autoaprobación doble en ambas familias, asientos y su anulación, suspensiones con solapes, descuentos con snapshot, gates de permisos incluida la exclusión de Admin en `Authorize*`, autogestión de lectura, consulta de tiempos con ambas fuentes F1 y modo degradado) **en verde junto con la suite existente**.
4. Migraciones `HasData` idempotentes con IDs verificados contra `GlobalCatalogSeedData` (bloque ≤ -9875; sin tocar reservas de REQ-001/REQ-002; reutilizando `-9477`/`-9478`).
5. `openapi.yaml` regenerado **sin drift**; convenciones API respetadas (If-Match, `publicId`, enums string, errores bilingües, DELETE → `parentConcurrencyToken`).
6. Adjuntos end-to-end (SAS + confirm + read-URL) con los 2 purposes configurados en appsettings **base**.
7. Bandejas paginadas con `StatusCounts`, exportaciones xlsx/csv/json y **export de insumo de planilla** cuadrando contra los registros aplicados del rango.
8. Plantillas de los 3 maestros idempotentes (2.ª corrida = 0 cambios) y editables sin perder históricos (snapshots).
9. Consulta de tiempos con contrato estable por fila, fuentes documentadas por versión y payload mínimo verificado (sin campos sensibles).
10. Guía de integración frontend publicada (`guia-integracion-frontend-otras-transacciones-personal.md`) con contratos, estados, flujos y fuentes activas de la consulta.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Legal**: en el sector privado SV las multas/descuentos disciplinarios están prohibidos y la suspensión reglamentaria tiene topes estrictos (Anexo A.1); un cliente podría configurar causas con descuento improcedente. Mitigación: nada codificado como ley, documentación explícita y responsabilidad del empleador comunicada. **Atenuado por la ratificación de P-14**: el negocio declara que no hay multas en su régimen — la plantilla de causas nace sin conceptos de descuento y no se construye advertencia en F1; el riesgo residual queda en empresas que activen descuentos por su cuenta.
- **Doble vía de registro**: el asiento manual documental (`AMONESTACION`/`SUSPENSION`) coexiste con el módulo estructurado. Mitigación: asientos del módulo `IsSystemGenerated`, capacitación ("el módulo es la vía"), consulta de tiempos leyendo solo de entidades (RN-16).
- **Expectativa de consulta completa**: hasta REQ-001/REQ-002 la consulta solo tendrá 2 fuentes. Mitigación: fuentes activas documentadas por versión (guía FE + UI), secuenciación D-18.
- **Sensibilidad del dato disciplinario**: fuga por la consulta agregada o por permisos laxos. Mitigación: payload mínimo (P-10), permiso dedicado, `Authorize*` excluye Admin, self solo aplicadas.
- **Expectativa de descuento automático en nómina**: no hay motor de planilla. Mitigación: comunicar D-10/RN-14 y entregar el export de insumo exacto.
- **Decisiones tardías sobre el flujo** (P-01 multi-nivel): cambiar de una decisión a multi-nivel después de construir es caro. Mitigación: estados híbridos aditivos + ratificar P-01 antes del plan técnico.

### Supuestos

- El cliente cuenta con **reglamento interno / normativa** que tipifica faltas y sanciones (privado: reglamento aprobado por el Ministerio; público: LSC/normativa propia); los maestros reflejan esa política y su legalidad es responsabilidad del empleador.
- La planilla se procesa **externamente** y consume archivos; el monto del descuento lo digita RRHH (el sistema no calcula fórmulas en F1 — P-02).
- Tenant mono-país (SV) como el resto del sistema.
- Los empleados autogestionados tienen usuario vinculado (`LinkedUserPublicId`); quienes no, operan vía RRHH.
- "Permisos" de la consulta se cubrirán por las familias de ausencia existentes/planificadas hasta que exista un módulo de permisos generales (P-09).
- Los reconocimientos con premio económico son informativos en F1 (P-08).

### Dependencias

- **Ratificación del negocio**: ✅ completada (2026-07-05, §17).
- **REQ-001**: patrón de maestros por empresa con plantilla (`LeaveTemplateSeeder`, PR-1) + 3 fuentes de la consulta; **REQ-002**: cuarta fuente. Secuenciación D-18.
- **Storage**: 2 purposes en appsettings base + contenedores aprovisionados (pendiente de despliegue estándar).
- Internas: verificación de IDs de seed libres al abrir el primer PR (bloque ≤ -9875); convenciones de catálogos/permisos vigentes.

---

## 17. Preguntas abiertas para el cliente o stakeholders — resueltas (2026-07-05)

| # | Pregunta (síntesis) | Respuesta del negocio → efecto en el diseño |
|---|---|---|
| P-01 | ¿Flujo de un solo nivel o multi-nivel? ¿Mismos autorizadores por familia? | **Un nivel con anti-autoaprobación doble y permisos `Authorize*` separados por familia; multi-nivel = F2 aditivo** → D-03/D-04/D-05 confirmadas tal cual |
| P-02 | ¿Monto del descuento: manual o fórmula de la causa? ¿Moneda? | **Monto manual > 0 digitado por RRHH (sin fórmulas F1); USD default** → RN-06 confirmada; sin motor de fórmulas |
| P-03 | ¿"Tipo de descuento" = catálogo de conceptos existente o catálogo propio? | **Referenciar el catálogo país existente (`Nature=Egreso`) — cero catálogos monetarios nuevos** → D-06/RF-003 confirmadas |
| P-04 | ¿Suspensión: rango o días? ¿Calendario o hábiles? ¿Tope? | **Rango de fechas + días calendario derivados; sin tope en F1** (advertencia configurable solo si se ratifica a futuro); hábiles requeriría REQ-001 → D-08/RN-05 confirmadas |
| P-05 | ¿Cambio automático a `SUSPENDIDO` con reversión? | **No en F1** (precedente P-18 de REQ-001): registro + asiento; cambio manual disponible; automatismo con job = F2 → D-09 confirmada |
| P-06 | ¿El empleado ve sus registros aplicados? ¿Notificaciones? | **Lectura self de aplicadas en ambas familias** (transparencia del expediente); notificaciones F2 → D-13 confirmada |
| P-07 | ¿Adjuntos F1 en ambas familias? | **Ambas** (stack espejo barato); si se recorta, priorizar amonestaciones → D-12 confirmada (2 purposes) |
| P-08 | ¿Incentivo económico del reconocimiento a planilla? | **Informativo exportable en F1; integración a planilla como ingreso = F2** → D-07/RN-17 confirmadas |
| P-09 | Fuente "permisos" inexistente: ¿fuentes conectables o módulo de permisos primero? | **Fuentes conectables con contrato estable y degradación documentada; el módulo de permisos generales se evalúa como requerimiento aparte** → D-14/RF-013 confirmadas |
| P-10 | ¿Payload de la consulta: mínimo o detallado? ¿Permiso dedicado? | **Payload mínimo (categoría + fechas + días) con permiso dedicado `ViewTimeAvailability`** — la vista es de planificación, no de expediente → D-14 confirmada |
| P-11 | ¿Adopción del historial: flujo normal o alta directa? | **Flujo normal retroactivo (auditable)**; alta directa solo si el volumen lo exige (se re-evaluaría entonces) → G-09 cerrada sin modo especial |
| P-12 | ¿Se siembran las plantillas A.2? | **Sí, sembrar las plantillas A.2 (editables); las causas nacen sin concepto de descuento** (lo asocia cada empresa) → D-06 confirmada con semilla |
| P-13 | ¿Reincidencia/prescripción automática? | **F2**; en F1 el historial filtrable por tipo/causa/fechas soporta el análisis manual → D-17 confirmada |
| P-14 | Régimen legal del cliente y advertencia configurable | **«No hay multas»**: el régimen del cliente no contempla multas/descuentos-sanción → **sin advertencia legal configurable en F1** (no hay política que vigilar) y las causas de la plantilla quedan **sin concepto de descuento** (coherente con P-12). La **capacidad** de descuento del módulo se mantiene tal como la pide el levantamiento (P-02/P-03 ratificadas) para empresas cuyo marco sí lo permita (p. ej. LSC pública); su uso es responsabilidad del empleador (RN-14, Anexo A.1) |

---

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar primero P-01 (flujo), P-04 (suspensión) y P-09/P-10 (consulta)**: son las decisiones que fijan la arquitectura (una decisión vs multi-nivel; semántica de días; contrato del agregador). El resto ajusta campos y defaults.
2. **No construir un motor de aprobaciones genérico**: el flujo de una decisión con anti-autoaprobación doble cubre el levantamiento y ya está probado en el sistema (ayuda económica); un motor configurable multi-nivel es un proyecto en sí mismo y quedaría especulativo.
3. **Reutilizar el vocabulario ya sembrado**: asientos con `AMONESTACION`/`SUSPENSION` existentes y solo un ActionType nuevo (`RECONOCIMIENTO`); el asiento manual queda como vía documental y el módulo como vía formal — comunicar la diferencia al cliente.
4. **Mantener el dinero fuera del módulo**: el "descuento en planilla" es un registro con concepto (catálogo existente) + monto exportable; no escribir ledgers ni calcular fórmulas. Es el mismo corte ratificado en REQ-002 (G-06) y evita construir nómina por accidente.
5. **Tratar la amonestación como dato sensible de primera clase**: permisos por familia, `Authorize*` sin fallback Admin, payload mínimo en la consulta agregada y visibilidad self limitada a aplicadas. La reputación del producto en clientes institucionales depende de esto.
6. **Diseñar la consulta de tiempos como contrato, no como feature terminada**: fila estable + categorías por código + fuentes conectables; así REQ-001/REQ-002 la completan sin re-trabajo y el FE no cambia. Publicar en la UI qué fuentes están activas.
7. **Secuenciar después de REQ-001 y REQ-002** (D-18): se hereda el patrón de plantillas y la consulta nace con 5 fuentes en lugar de 2. Si el negocio necesita adelantar la parte disciplinaria, es viable extrayendo el patrón de plantilla — decisión de prioridad, no técnica.
8. **MVP si se necesita recortar**: RF-001…RF-010 (catálogos + ambas familias con flujo y asientos) entregan el valor central del levantamiento; bandejas/exports (RF-012) y consulta de tiempos (RF-013) pueden ser segunda entrega — la consulta gana valor real tras REQ-001.
9. **Validación legal temprana con el cliente** (P-14): antes de sembrar plantillas con causas de descuento, confirmar el régimen aplicable (CT vs LSC) para no institucionalizar una práctica improcedente desde el catálogo.
10. **F2 ya perfilado**: multi-nivel con bandeja del autorizador y notificaciones, reincidencia/prescripción, automatismo de estado `SUSPENDIDO`, integración del incentivo económico y de la generación del concepto de egreso, y conexión de nuevas fuentes a la consulta.

---

## Anexo A — Referencias y propuestas

### A.1 Marco legal y normativo de referencia (El Salvador) — verificado 2026-07-05, a validar con el negocio/legal

> **Nada de esto se codifica como regla**: la tipificación de faltas, sanciones y descuentos es política de cada institución dentro de su marco legal; el módulo lo refleja con maestros por empresa y, a lo sumo, advertencias configurables (P-04/P-14).

| Concepto | Referencia verificada | Implicación para el módulo |
|---|---|---|
| Sanciones disciplinarias (sector privado) | Deben estar tipificadas en el **Reglamento Interno de Trabajo** aprobado por el Ministerio de Trabajo (Arts. 302-306 CT). **Prohibido descontar del salario en concepto de multas**; la suspensión disciplinaria establecida en el reglamento es de **hasta 1 día por falta**, y suspensiones mayores (hasta 30 días) requieren **autorización previa de la Dirección General de Inspección de Trabajo** y audiencia del trabajador ([CSJ — Boletín Reglamento Interno de Trabajo](https://www.csj.gob.sv/wp-content/uploads/2021/07/Boleti%CC%81n-de-Educacio%CC%81n-Judicial-Popular-42-2021-del-18.06.2021-Reglamento-interno-de-trabajo.pdf), [ILO — Código de Trabajo](https://webapps.ilo.org/public/spanish/region/ampro/mdtsanjose/papers/cod_elsa.htm)) | El "descuento en planilla" de una amonestación puede ser **improcedente en el sector privado**; se registra solo si la política del cliente lo sustenta (P-14). Topes de suspensión = parametrización/advertencia, nunca constante |
| Sanciones disciplinarias (sector público) | Ley de Servicio Civil, **Art. 41**: amonestación oral privada, amonestación escrita, **multa** (se deduce del sueldo), **suspensión sin goce de sueldo** — las jefaturas pueden imponer, en casos justificados, hasta **5 días por mes calendario** y **máximo 15 días por año**; sanciones mayores con procedimiento ante la Comisión de Servicio Civil ([Asamblea Legislativa — LSC](https://www.asamblea.gob.sv/sites/default/files/documents/decretos/01180EB7-C617-4DD0-8CF2-A6E8EF0B3845.pdf), [Guía de debido proceso en el sector público](http://www.aecid.sv/wp-content/uploads/2014/01/Guia2_Sobre_Debido_Proceso_en_Casos_Terminaciones_Laborales_en_el_Sector_Publico.pdf)) | Confirma que **descuento y suspensión sí existen legalmente** en el régimen público — el módulo debe soportarlos; los topes varían por régimen → maestros y preferencias por empresa |
| Debido proceso | En ambos regímenes la sanción exige respaldo (hechos, audiencia/descargo del trabajador) | Sustenta: relato de hechos obligatorio, adjuntos (acta/descargo) en F1 (D-12) y flujo de autorización con separación de funciones (D-03/D-04) |
| Contratos temporales | El fin del plazo del contrato (plazo fijo, obra, eventual) es una **terminación sin responsabilidad** que debe preverse operativamente | Sustenta la categoría `FIN_CONTRATO_TEMPORAL` de la consulta (RF-013), derivada del flag `IsTemporary` ya existente |

### A.2 Plantillas propuestas de los 3 maestros (borrador — a ratificar en P-12; todo editable)

**Tipos de reconocimiento**

| Código | Descripción |
|---|---|
| `FELICITACION_ESCRITA` | Felicitación escrita |
| `DESEMPENO_SOBRESALIENTE` | Desempeño sobresaliente |
| `PRODUCTIVIDAD` | Logro de metas / mayor producción |
| `ANTIGUEDAD` | Reconocimiento por años de servicio |
| `OTRO` | Otro |

**Tipos de amonestación**

| Código | Descripción | appliesSuspension |
|---|---|---|
| `VERBAL` | Amonestación verbal (con constancia escrita) | No |
| `ESCRITA` | Amonestación escrita | No |
| `SUSPENSION_SIN_GOCE` | Suspensión de labores sin goce de sueldo | **Sí** |
| `OTRO` | Otra | No |

**Causas de amonestación** (todas nacen **sin** concepto de descuento; cada empresa lo asocia si su régimen lo permite)

| Código | Descripción |
|---|---|
| `INASISTENCIA_INJUSTIFICADA` | Inasistencia injustificada |
| `LLEGADAS_TARDIAS` | Llegadas tardías reiteradas |
| `INCUMPLIMIENTO_FUNCIONES` | Incumplimiento de funciones o instrucciones |
| `CONDUCTA_INDEBIDA` | Conducta indebida / faltas al reglamento |
| `DANO_BIENES` | Daño o pérdida de bienes de la institución |
| `OTRO` | Otra |

### A.3 Seeds tentativos (verificar IDs libres contra `GlobalCatalogSeedData` al abrir el primer PR)

- Ocupación verificada hoy: piso general **-9846**; REQ-001 reserva **-9850…-9862** (TPH) y **-9485…-9489** (ActionTypes); REQ-002 propone **-9865…-9871**; `ACTION_STATUS_CATALOG` ocupa **-9490…-9496** (trampa vigente: los ActionTypes no continúan secuencia).
- **Reutilización** (sin seed nuevo): `AMONESTACION=-9477`, `SUSPENSION=-9478` (asientos); `SUSPENDIDO=-9101` (estado de empleo, solo uso manual); conceptos de egreso del catálogo país existente.
- Propuesta (bloque del módulo, contiguo): `PERSONNEL_TRANSACTION_STATUS_CATALOG` → `EN_REVISION=-9875`, `APLICADA=-9876`, `RECHAZADA=-9877`, `ANULADA=-9878` · `ACTION_TYPE_CATALOG` → `RECONOCIMIENTO=-9879`.
- Los 3 maestros por empresa **no llevan seed global** (plantilla por seeder + `load-template`, patrón REQ-001).

### A.4 Casos dorados sugeridos para la validación del negocio

1. **Reconocimiento e2e**: crear (`EN_REVISION`, sin asiento) → aplicar por un tercero → asiento `RECONOCIMIENTO` visible; el empleado lo ve en autogestión.
2. **Anti-autoaprobación doble**: el registrador intenta decidir → 403; el propio empleado sujeto (con permiso de autorizar) intenta decidir → 403.
3. **Amonestación con suspensión y descuento**: tipo `SUSPENSION_SIN_GOCE` + causa con concepto → aplicar → asientos `AMONESTACION` + `SUSPENSION` (10→12), snapshot del concepto, y aparece en consulta de tiempos y en export de insumo.
4. **Suspensión inválida**: fechas sobre tipo `ESCRITA` → 422; solape con suspensión vigente → 422 con conflicto.
5. **Revocación**: amonestación aplicada con suspensión revocada con motivo → registro + 2 asientos `ANULADA`; desaparece de consulta e insumo.
6. **Consulta de tiempos — intersección**: rango 01-15 con suspensión 28(mes previo)→03 → aparece con sus fechas reales; plaza `PLAZO_FIJO` con `EndDate` el 10 → aparece como `FIN_CONTRATO_TEMPORAL`; plaza `INDEFINIDO` → no aparece.
7. **RETIRADO**: alta sobre retirado → 422; pendiente de un retirado solo puede rechazarse/anularse.
8. **Catálogos**: `load-template` 2.ª corrida = 0 cambios; causa con concepto de **ingreso** → 422; tipo inactivo en alta nueva → 422 (históricos intactos).
