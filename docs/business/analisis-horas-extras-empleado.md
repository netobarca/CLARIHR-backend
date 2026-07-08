# Análisis de negocio — Módulo de horas extras del empleado (registro de jornadas, autorización y consulta de solicitudes)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra código + GAP + propuesta) |
| **Módulo** | **Horas extras** — registro de las **jornadas extraordinarias trabajadas** por empleado (tipo de hora extra con factor, fecha, duración en horas y minutos, solicitante, justificación, planilla y periodo destino) con **flujo de autorización**, dos **catálogos maestros** (tipos de hora extra y tipos de justificación) y **consulta corporativa de solicitudes** con exportación a Excel |
| **Fecha** | 2026-07-06 |
| **Autor** | Equipo CLARIHR — análisis asistido y validado contra el código |
| **Estado** | **RATIFICADO por el negocio (2026-07-06)** — decisiones **D-01…D-21 ratificadas** y **P-01…P-14 respondidas** (§17): 12 recomendaciones aceptadas tal cual + **2 ajustes de alcance** — **P-02** admite también **jornadas futuras organizadas previamente por cada área** (D-08 ajustada) · **P-08** la **liquidación calcula las horas extras pendientes de pago** (línea `HORAS_EXTRAS_PENDIENTES_PAGO=-9915`, **nueva D-21**/RF-014) |
| **Naturaleza del módulo** | Greenfield transaccional **dos veces anunciado por ratificaciones previas**: es el «módulo de horas extras» al que REQ-002 dejó la costura `overtimeRecordPublicId` (D-21: «queda listo para cuando se desarrolle») y del que REQ-006 dijo literalmente **«no es el módulo de horas extras; ese será otro módulo»** (P-09, 2026-07-06). Registra la **jornada** (horas trabajadas × factor), **no el dinero**: hereda la frontera ratificada en cadena «sin motor, sin tarifas legales automáticas, sin lookup de salario» (P-01 de REQ-005 · P-03/P-04 de REQ-006) |
| **Documentos hermanos** | [`analisis-planilla-ingresos-eventuales.md`](analisis-planilla-ingresos-eventuales.md) (REQ-006 — registra el **pago**; su P-09 remite a este módulo el registro de jornadas) · [`analisis-tiempo-compensatorio-empleado.md`](analisis-tiempo-compensatorio-empleado.md) (REQ-002 — registra la **compensación en tiempo**; su D-21 es la costura `overtimeRecordPublicId` que este módulo validará) · [`analisis-planilla-ingresos-ciclicos.md`](analisis-planilla-ingresos-ciclicos.md) (REQ-005 — molde del flujo de una decisión y de la aplicación por periodo) |
| **Documentos relacionados** | `analisis-vacaciones-incapacidades-empleado.md` (REQ-001 — maestro de **periodos de planilla** D-23, asuetos D-03, `restDayOfWeek` D-26, patrón de plantillas `load-template`) · `analisis-tablero-acciones-personal.md` (REQ-004 — especificación del catálogo país de **tipos de planilla**, seeds re-ubicados `-9890…-9895`) · `analisis-ayuda-economica-empleado.md` y `analisis-reclamos-seguro-medico-empleado.md` (precedentes de **autoservicio** del empleado) · `analisis-liquidacion-empleado.md` (motor de liquidación — destino del gancho RF-014 de pendientes de pago; el concepto `-9837` sigue reclamado por REQ-002) |

---

## Contexto del cambio (requerimiento original)

> **Nuevas horas extra**: En esta opción se agregará las horas extras trabajadas para un empleado, utilizando los catálogos de **tipos de horas extras** y **periodos de planilla**. La principal información que se ingresará es: **Empleado**, **tipo de hora extra (catálogo)**, **factor**, **fecha**, **duración en horas y minutos**, **empleado que solicita**, **motivo**, **nómina**, **periodo de nómina**. Deberá utilizar un **flujo de autorización**.
>
> **Catálogos necesarios para horas extras:**
> - **Tipos de hora extra**: catálogo para registrar los tipos de horas extras que se autorizan en la institución, especificando las condiciones que cada tipo de hora tendrá como **efecto en el pago de planilla**. Entre los tipos de horas extra estarán: **Hora Extra Diurna, Hora extra nocturna, Hora extra diurna festiva, hora extra nocturna festiva**.
> - **Tipos de justificación de horas extras**: catálogo para registrar las **razones que justifican** las horas extras de los empleados.
>
> **Consulta de solicitudes de horas extras**: esta opción permitirá realizar consulta de las solicitudes de horas extras **que han ingresado desde el portal**. El resultado será un **listado detallando información** de la consulta. El formulario deberá permitir **exportar a Excel**.

---

## 0. Veredicto ejecutivo (resultado de la validación)

1. **Es el módulo dos veces anunciado por la cadena de ratificaciones.** REQ-002 (tiempo compensatorio, ratificado 2026-07-05) dejó declarada la costura `overtimeRecordPublicId` en la acreditación — «sin FK dura ni validación en F1; el módulo futuro la validará/poblará y prevendrá el doble beneficio» (D-21/RN-19 de aquel análisis). REQ-006 (ingresos eventuales, ratificado 2026-07-06) cerró su P-09 con la precisión literal **«no es el módulo de horas extras; ese será otro módulo»**: aquel registra el **pago**, este registra la **jornada**. Este levantamiento es ese módulo — la tercera pieza del triángulo jornada → pago (REQ-006) / compensación en tiempo (REQ-002), con las costuras ya reservadas desde ambos lados (Anexo A.5).
2. **Greenfield total, re-verificado hoy sobre el mismo HEAD (`62b341b`, merge PR #56) contra el que se validaron REQ-002/005/006.** No existe ninguna entidad/endpoint/tabla de registro de horas extras (`OvertimeRecord`/`HoraExtra`/`ExtraHour`: 0 hits transaccionales en `src/`). Lo único que existe son **dos conceptos aislados**: el concepto país de compensación `HORAS_EXTRA` (`-9721`, `GlobalCatalogSeedData.cs:936-952` — «tipo de ingreso» que REQ-006 consumirá para pagar) y el concepto de liquidación `HORAS_EXTRAS_PENDIENTES` (`-9837` — **reclamado por REQ-002** para el saldo compensatorio al retiro; este módulo NO lo toca).
3. **El flujo de autorización viene pedido explícitamente** («Deberá utilizar un flujo de autorización») — a diferencia de REQ-005/REQ-006 donde fue propuesta del análisis. Se aplica el molde ratificado tres veces (ayuda económica → REQ-005 → REQ-006): **una decisión** (`EN_REVISION → AUTORIZADA / RECHAZADA` + `APLICADA` + `ANULADA`), anti-autoaprobación y permiso `Authorize*` cuya policy **no incluye el fallback Admin**. La anti-autoaprobación **triple** (sujeto + registrador + solicitante) tiene ya **precedente exacto en código**: los guards del retiro validan al sujeto **y al solicitante** (`RetirementAuthorizerGuards.CheckAsync`, `RetirementRequestResolution.cs:64-87`), además de la extensión ratificada en REQ-006 D-05.
4. **Precisión verificada hoy sobre el patrón `Authorize*` sin Admin** (corrige la redacción de análisis/planes previos): el permiso **SÍ figura** en el array `CompanyAdminPermissions` de provisioning (`ProvisioningConstants.cs:89`, con la descripción «No implicado por la administración de expedientes»); la separación de deberes vive en la **policy** — `AuthorizeRetirement` en `Program.cs:564-569` acepta solo `AuthorizeRetirement` + `ManageAdministration` y **omite `PersonnelFiles.Admin`** (contrastar con `ManageRetirements`, `Program.cs:557-563`, que sí lo incluye; diseño comentado en `Program.cs:552-554`). El permiso de este módulo replica exactamente ese corte.
5. **«Que han ingresado desde el portal» es la novedad estructural del levantamiento** — y tiene **precedente triple construido**: reclamos de seguro médico, ayuda económica y constancias ya permiten que el empleado **cree sobre su propio expediente**. El mecanismo verificado: la policy de escritura se mantiene **authn-only** (comentario de diseño en `PersonnelFilePolicies.cs:47-53`) y el gate fino vive en el handler comparando `PersonnelFile.LinkedUserPublicId` con el usuario autenticado (helpers `LoadForCreateOwnOrManage*` en `PersonnelFileEmployeeHandlerBases.cs:227/316/678`; bloque `isSelf` `:254-256`). **No existe** portal de jefaturas (registrar por «mi equipo» requeriría un modelo de equipos/jerarquía que el sistema no tiene) → **P-01 estructural**: alcance del canal en F1 (recomendación: RRHH + autoservicio del propio empleado, con preferencia de empresa y origen trazado; jefaturas = F2).
6. **Los cuatro tipos del levantamiento son el estándar salvadoreño y sus factores son derivables del Código de Trabajo** (referencia: HE diurna 2.00 · HE nocturna 2.50 · HE diurna festiva 4.00 · HE nocturna festiva 5.00 — derivación transparente en Anexo A.2 sobre Arts. 168/169/175/192 CT). Propuesta: **maestro por empresa CON plantilla** (a diferencia del maestro de REQ-002, que el negocio quiso vacío, aquí el levantamiento **nombra los tipos**) y factor **editable por empresa y por registro con snapshot** — sin tarifas hardcodeadas (frontera P-03/P-04 de REQ-006 heredada); los valores de la plantilla se confirman con el contador (**P-03**, espejo del precedente «golden cases del contador» de liquidación).
7. **El módulo NO registra dinero.** Sin monto, moneda ni centro de costo persistidos: la unidad es **horas × factor**. El valor monetario lo pone la planilla externa (a partir del **insumo por periodo**: empleado, plaza, tipo, factor, horas decimales) o — si la empresa registra el pago en CLARIHR — un **ingreso eventual** `HORAS_EXTRA` de REQ-006 (frontera editorial documentada, Anexo A.5). Calcular el valor-hora desde el salario pertenece al motor futuro (frontera «sin motor» ratificada en cadena). **Única excepción ratificada (P-08)**: al retiro, la **liquidación** — que ya es un motor de cálculo con contexto salarial (PR #56) — calcula y sugiere el valor de las horas pendientes de pago (D-21/RF-014, concepto propio `-9915`).
8. **«Nómina» + «periodo de nómina» = el par ya especificado por REQ-004 y REQ-001**, cuarta reutilización de la misma solución (cuotas REQ-005, eventuales REQ-006, incapacidades REQ-001). Verificado hoy: sigue sin construir — `PayrollTypeCode` es texto libre en la plaza (`PersonnelFileEmployee.cs:187`; columna varchar(80) sin FK, `PersonnelFileEmployeeConfiguration.cs:72`), no existe `PayrollPeriodDefinition` (0 hits) ni `PAYROLL_TYPE_CATALOG` en seeds. Solución espejo: `payrollTypeCode` contra el catálogo país (definición única REQ-004, seeds `-9890…-9895`, la siembra el REQ que construya primero) + `PayrollPeriodId?` + **etiqueta snapshot** con **degradación documentada** si este módulo se adelanta a REQ-001. El propio levantamiento asume el catálogo de periodos como existente («utilizando los catálogos de … periodos de planilla») — existirá vía REQ-001.
9. **La consulta con Excel tiene molde completo, sin piezas net-new de reporting**: bandeja corporativa `POST /companies/{id}/{recurso}/query` con filtros + paginación + `StatusCounts` (`SettlementsReportingController.cs:30`; rollup `SettlementRepository.cs:355-363`) y `GET /export` xlsx/csv/json con rate limiting y auditoría (`SettlementsReportingController.cs:71`; `ReportExportDeliveryService.cs:49`; OpenXML a mano `ReportExportFileWriter.cs:78`). Los totales aquí son **en horas** (por tipo y globales), no monetarios; no se necesita el `groupBy` dimensional de REQ-006 (aditivo futuro si el negocio lo pide — P-11/P-14 no lo cubren porque el levantamiento no lo pide).
10. **Ciclo con `APLICADA` por periodo (P-07)**: el levantamiento no pide la constancia de aplicación, pero la familia la ratificó dos veces para dinero equivalente (D-08/D-09 de REQ-005, D-09 de REQ-006) y es lo que hace útil el insumo («qué horas autorizadas aún no se han pagado» — bandeja de pendientes/atrasados). Propuesta: 5 estados espejo con aplicación manual unitaria/lote por periodo con exclusión, bajo lock anti-carrera. Recortable si el negocio prefiere cerrar en `AUTORIZADA`.
11. **Seeds: bloque nuevo `-9910…-9919`** — REQ-006 verificó en doble pasada (mismo HEAD) que **nada en código ni en planes ocupa `-9900` o inferior** y reservó `-9900…-9909`; el piso real en código sigue siendo `-9846`. Este módulo toma el siguiente bloque: estados `-9910…-9914`, concepto de liquidación `-9915` (P-08) y holgura `-9916…-9919`. Los dos maestros son **por empresa** (sin IDs globales de seed; plantilla vía seeder, patrón `load-template` de REQ-001). Sin ActionTypes nuevos (D-19: sin asientos de journal — el volumen diario de horas extras inundaría el journal que REQ-004 grafica).

### Trazabilidad campo a campo del levantamiento

| Campo pedido | Cobertura propuesta |
|---|---|
| Empleado | Expediente (`personnel-files/{publicId}/…`) + **plaza asociada** (obligatoria, default la principal — ancla del contexto para la nómina externa) (D-12) |
| Tipo de hora extra (catálogo) | **Maestro por empresa nuevo** `overtime-types` con plantilla (los 4 tipos del levantamiento + factores de referencia CT) + **snapshot** código/nombre/factor al registrar (D-03) |
| Factor | Default del **tipo** (snapshot), **editable por registro con nota** cuando difiere — espejo exacto del factor de la acreditación de REQ-002 (D-09, P-06) |
| Fecha | Fecha trabajada **pasada** (retroactiva) **o futura** — jornada **organizada previamente por cada área** (P-02 ratificada); aplicar exige fecha ya transcurrida (RN-09) |
| Duración en horas y minutos | `durationHours` + `durationMinutes` (0–59) → **horas decimales derivadas** (2 decimales) para totales e insumo (D-08) |
| Empleado que solicita | **Trío solicitante** con precedente exacto: `RequesterFilePublicId` + `RequesterNameSnapshot` + `RequestedByUserId` (`PersonnelFileSettlement.cs:115-123`); obligatorio y **participa en la anti-autoaprobación** (D-11) |
| Motivo | **Tipo de justificación** (maestro por empresa nuevo `overtime-justification-types`, con plantilla) + observaciones libres opcionales (D-04) |
| Nómina | `payrollTypeCode` contra el **catálogo país** `PAYROLL_TYPE_CATALOG` (definición REQ-004, seeds `-9890…-9895`; default: el de la plaza si está clasificada) (D-13) |
| Periodo de nómina | `PayrollPeriodId?` (maestro por empresa de REQ-001, D-23) + **etiqueta snapshot**; re-imputable mientras esté pendiente («enviar a otro periodo»); **degradación a etiqueta** si este módulo se adelanta (D-13) |
| Flujo de autorización | **Una decisión** (`EN_REVISION → AUTORIZADA/RECHAZADA`) + anti-autoaprobación **triple** + `AuthorizeOvertimeRecords` cuya policy omite Admin + revocación con motivo (D-05/D-06/D-07) |
| (Retiro) pendientes de pago | **Línea calculada por la liquidación** (P-08 ratificada): Σ(horas × factor) × valor-hora del contexto salarial, concepto propio `-9915` (D-21/RF-014) |
| (Consulta) solicitudes desde el portal | **Canal de autoservicio** del propio empleado (precedente triple en código) con preferencia de empresa y **origen trazado** (`RRHH`/`PORTAL`) — P-01 define el alcance; la consulta filtra por origen (D-10, P-11) |
| (Consulta) listado detallado | Bandeja corporativa `POST /query` con filtros + `StatusCounts` + **totales en horas** + detalle completo por solicitud (D-15) |
| (Consulta) exportar a Excel | `GET /export?format=xlsx` (además csv/json de la casa) + **insumo de planilla por periodo** (horas × factor por empleado/tipo — el puente con la nómina externa) (D-15) |

---

## Estado actual verificado en el código (línea base "as-is")

> Verificación fresca del 2026-07-06 sobre `master` = `62b341b` (merge PR #56) — el mismo HEAD contra el que se validaron REQ-002/005/006; el árbol de trabajo solo contiene documentos sin commitear.

### Lo que YA existe y este módulo reutiliza

| Pieza | Dónde | Uso en este módulo |
|---|---|---|
| Concepto país `HORAS_EXTRA` (`-9721`, `Nature=Ingreso`) del catálogo de conceptos de compensación | `GlobalCatalogSeedData.cs:936-952` | **No se consume aquí** — es el «tipo de ingreso» con el que REQ-006 registrará el **pago**; frontera y costura documentadas (A.5). Evita confundir: el «tipo de hora extra» de este módulo es un maestro nuevo por empresa |
| Concepto de liquidación `HORAS_EXTRAS_PENDIENTES` (`-9837`, `IsSystemCalculated=false` → solo-manual hoy; afecta ISSS/AFP/Renta) | `GlobalCatalogSeedData.cs:969`; `SettlementCalculation.Rules.cs:657` | **No se toca** — REQ-002 lo reclama para el saldo de tiempo compensatorio al liquidar (su D-19). Este módulo no genera líneas de finiquito (D-16) |
| Costura declarada desde REQ-002: `overtimeRecordPublicId` opcional en la acreditación compensatoria (Guid nullable, sin FK ni validación en F1) | `analisis-tiempo-compensatorio-empleado.md` D-21/RN-19 (plan técnico PR-2) | Cuando ambos módulos convivan, **este módulo valida/pobla el vínculo** y sostiene el anti-doble-beneficio (D-17, RF-013, P-14) |
| Flujo de una decisión con estado híbrido + anti-autoaprobación en handler | `PersonnelFileEmployee.cs:1678+` (ayuda económica); `EconomicAidRequests.Handlers.cs:371-376` | Molde del ciclo `EN_REVISION → AUTORIZADA/RECHAZADA` (D-05) |
| **Guards de decisión que ya validan sujeto Y solicitante**: `RetirementAuthorizerGuards.CheckAsync` — sujeto (`LinkedUserPublicId == actingUserId`) y solicitante (lookup del expediente solicitante → `RequesterCannotAuthorize`) | `RetirementRequestResolution.cs:64-87`; invocado en `:144-145` y `:228-229` | Molde **exacto** de la anti-autoaprobación triple (D-06): sujeto + registrador + solicitante |
| Permiso `Authorize*` cuya **policy omite el fallback Admin** (el permiso sí vive en `CompanyAdminPermissions` para provisioning; la exclusión la hace la policy) + **controller de resolución dedicado** (el atributo `AuthorizationPolicySet` es class-only) | `ProvisioningConstants.cs:89`; `Program.cs:552-569`; `RetirementRequestResolutionController.cs:27/:30/:58` | `AuthorizeOvertimeRecords` con el mismo corte; decisión en controller dedicado (D-07) |
| «Solicitante» de primera clase: trío `RequesterFilePublicId` + `RequesterNameSnapshot` + `RequestedByUserId` (+ `RequestDate`, `Notes`), expuesto en bandejas/exports | `PersonnelFileSettlement.cs:115-123`; retiro (`PersonnelFileEmployee.cs:2456-2478`) | «Empleado que solicita» del levantamiento: mismo trío (D-11) |
| **Autoservicio del empleado (precedente triple)**: creación sobre el propio expediente en reclamos médicos, ayuda económica y constancias — policy de escritura **authn-only** (a propósito) + gate `isSelf` en handler comparando `LinkedUserPublicId` con el usuario autenticado | `PersonnelFilePolicies.cs:47-53/:95/:110`; `PersonnelFileEmployeeHandlerBases.cs:227/:316/:678` (helpers `LoadForCreateOwnOrManage*`), `:254-256` (bloque `isSelf`); `MedicalClaimsController.cs:71`; `EconomicAidRequestsController.cs:71` | Canal «portal» del levantamiento (D-10, P-01): registro de horas extras del **propio** empleado, con origen trazado |
| Bandeja corporativa + exportación: `POST /companies/{id}/{recurso}/query` (filtros + paginación + `StatusCounts`) + `GET /export?format=` con rate limiting (`Search`/`Export`) y auditoría | `SettlementsReportingController.cs:30/:71`; `SettlementRepository.cs:355-363` (rollup por estado); `PersonnelFileRateLimitPolicies.cs:19/:27`; `ReportExportDeliveryService.cs:15/:49`; `ReportExportFileWriter.cs:37/:78` (xlsx OpenXML a mano) | «Consulta de solicitudes» + «exportar a Excel» (D-15, RF-011/RF-012) — molde completo |
| Plaza con `PayrollTypeCode` **texto libre** (varchar 80, nullable, sin FK) e `IsPrimary` (plaza principal) | `PersonnelFileEmployee.cs:187`; `PersonnelFileEmployeeConfiguration.cs:72`; `EmploymentAssignments.cs:109` | «Nómina» del registro usa el **catálogo** (REQ-004) y hereda como default el de la plaza si está clasificada (D-13) |
| Catálogo país de **tipos de planilla** `PAYROLL_TYPE_CATALOG` — **especificado (REQ-004), NO construido** (verificado: 0 hits en seeds); seeds re-ubicados `-9890…-9895` | `plan-tecnico-tablero-acciones-personal.md` §3.1/§4; re-ubicación en `analisis-planilla-ingresos-ciclicos.md` A.2 | «Nómina en que se procesa»: definición única compartida; la siembra el REQ que construya primero (D-13) |
| Maestro de **periodos de planilla por empresa** `PayrollPeriodDefinition` (tipo, año, número, etiqueta, fechas de corte) — **especificado (REQ-001 D-23), NO construido** (verificado: 0 hits) | `analisis-vacaciones-incapacidades-empleado.md` D-23/RF-026 | «Periodo de nómina»: FK opcional + etiqueta snapshot; **degradación a etiqueta** si este módulo se adelanta (D-13) |
| **Maestro por empresa construido** (precedente del patrón: `TenantEntity` + código normalizado + `IsActive` + CRUD bajo `companies/{companyId}/…`): `CompetencyRatingScale`. Precisión verificada hoy: los catálogos de la entrevista de retiro son **por país** (`CountryScopedCatalogItem`), NO por empresa — el molde correcto es el rating scale | `CompetencyFramework/CompetencyRatingScale.cs:12`; `CompetencyRatingScalesController.cs:30/:44` | Molde de entidad/controller de los 2 maestros nuevos (D-03/D-04) |
| Maestros por empresa + **plantilla + `load-template`** (patrón de adopción especificado en REQ-001 PR-1, `LeaveTemplateSeeder`; REQ-003 lo reutiliza) | `plan-tecnico-vacaciones-incapacidades.md` §3.1; `plan-tecnico-otras-transacciones-personal.md` PR-1 | Los 2 maestros de este módulo (tipos + justificaciones) siguen el mismo patrón (D-03/D-04); si este módulo arranca primero, trae su propio seeder |
| Asuetos institucionales por empresa (REQ-001 D-03) y día de descanso semanal por plaza `restDayOfWeek` (REQ-001 D-26) — **especificados, NO construidos** | `analisis-vacaciones-incapacidades-empleado.md` G-02/D-03/D-26 | **F2**: sugerir/validar tipos festivos cuando la fecha trabajada caiga en asueto y señalar trabajo en día de descanso (Art. 175 CT — genera además día compensatorio: costura con REQ-002) |
| Preferencias por empresa — hoy solo 5 columnas de dominio: `CurrencyCode` (default USD), `TimeZone`, `HrFunctionalAreaCode`, `FileUpToDateThresholdMonths`, `MinimumSeniorityMonthsForEconomicAid` (patrón: columna nullable + setter + PATCH) | `Preferences/CompanyPreference.cs:5` (columnas `:22-:34`, setter `:74`) | 2 preferencias nuevas: habilitación del canal portal + tope diario opcional (D-10/D-08, P-01/P-05) |
| Gates de autogestión (`LinkedUserPublicId`) y convenciones API del repo (`api/v1`, If-Match faltante→400 / obsoleto→409, ETag rotativo, enums string, errores bilingües `extensions.code`, `publicId`) | `PersonnelFileEmployeeHandlerBases.cs`; `ProblemDetailsFactory.cs`; `FromIfMatchAttribute.cs` | Aplican a todos los endpoints nuevos (RNF) |

### Lo que NO existe (verificado en `src/`, tests y contrato)

- Ninguna entidad/endpoint/tabla de **registro de horas extras** (`OvertimeRecord`, `HoraExtra`, `ExtraHour`: 0 hits transaccionales; «horas extra» aparece solo en los dos conceptos aislados citados y en descripciones de líneas manuales de liquidación).
- Ningún **catálogo de tipos de hora extra** ni de **justificaciones** (ni país ni por empresa).
- Ningún **flujo de autorización de jornadas** (los flujos existentes deciden dinero/retiros/solicitudes de otros dominios).
- Ningún **modelo de equipos/jerarquía de jefaturas** (no hay forma de que un jefe registre «por su equipo» — el autoservicio existente es solo sobre el propio expediente) → P-01.
- Ningún catálogo de **tipos de planilla** ni maestro de **periodos** en código (especificados en REQ-004/REQ-001; degradaciones documentadas).
- Ningún campo de **duración horas+minutos** en el dominio (verificado: `DurationMinutes`/`DurationHours` 0 hits; los `TimeSpan` existentes son infraestructura no persistida — rate limits, lockouts, TTLs; los módulos existentes cuentan **días**; REQ-002 especifica horas pero no está construido) — pieza nueva de bajo riesgo (D-08).
- **REQ-001…REQ-006 no están construidos** (planes escritos, sin commit): plantillas/`load-template`, periodos, asuetos, `restDayOfWeek`, tipos de planilla y los módulos hermanos llegan con ellos.
- **Espacio de seeds**: piso real en código **`-9846`** (`GlobalCatalogSeedData.cs:980` — **toda la banda `-9847…-9999` libre en código**, verificación propia de hoy); reservas de planes hasta **`-9909`** (REQ-001 `-9850…-9862` + `-9485…-9489` · REQ-002 `-9865…-9871` · REQ-003 `-9875…-9879` · REQ-005 `-9880…-9899` incl. payroll-types `-9890…-9895` · REQ-006 `-9900…-9909`); trampa vigente `-9490…-9496` (`ACTION_STATUS_CATALOG`); ojo: los aparentes `-9914`/`-9897` en `Migrations/*.Designer.cs` son **fragmentos de GUID**, no seeds. **Bloque `-9910…-9919` libre** (Anexo A.3).

---

## Brechas identificadas (GAP → propuesta)

| # | Brecha detectada | Propuesta de resolución |
|---|---|---|
| G-01 | «Horas extras trabajadas» sin entidad: no hay dónde registrar la jornada extraordinaria con su autorización | Nueva entidad transaccional del expediente `PersonnelFileOvertimeRecord` (nombre alineado a la costura `overtimeRecordPublicId` de REQ-002; el plan técnico fija el final) — registra **horas × factor**, sin dinero (D-02) |
| G-02 | «Tipos de hora extra» sin catálogo; el levantamiento exige especificar «condiciones… efecto en el pago» | **Maestro por empresa** con plantilla (los 4 tipos nombrados + factores de referencia CT, Anexo A.2) — código, nombre, **factor default**, descripción del efecto; snapshot al registrar (D-03, P-03/P-04) |
| G-03 | «Tipos de justificación» sin catálogo | **Maestro por empresa** con plantilla (6 razones sugeridas, Anexo A.2); el «motivo» del registro referencia el maestro + observaciones (D-04, P-09) |
| G-04 | «Flujo de autorización» pedido sin ciclo definido | Ciclo de **5 estados** con una decisión (molde ratificado 3 veces) + anti-autoaprobación **triple** con precedente exacto en código + revocación con motivo (D-05/D-06) |
| G-05 | «Solicitudes que han ingresado desde el portal» sin canal definido — no existe portal de jefaturas ni flag de autoservicio para este dominio | Canal de **autoservicio del propio empleado** (precedente triple: policy authn-only + `isSelf` en handler) habilitado por **preferencia de empresa** + **origen trazado** (`RRHH`/`PORTAL`); jefaturas por equipo = F2 (**P-01 estructural**) (D-10) |
| G-06 | «Nómina» y «periodo de nómina» sin catálogo/maestro (texto libre en plaza; nada de periodos) | Par `payrollTypeCode` (catálogo REQ-004) + `PayrollPeriodId?`/etiqueta snapshot (maestro REQ-001) con **degradación documentada** — cuarta reutilización de la solución (D-13) |
| G-07 | «Duración en horas y minutos» sin precedente de campo en el dominio | `durationHours` + `durationMinutes` (0–59) con **horas decimales derivadas** (2 decimales) como cifra operativa de totales/insumo; hora inicio/fin opcionales informativas (espejo del D-06 de REQ-002) (D-08) |
| G-08 | Riesgo de **doble beneficio** (misma jornada pagada Y compensada con tiempo) y de **doble registro** (jornada aquí + monto en REQ-006) sin control | Costuras y fronteras documentadas (D-17, Anexo A.5): la acreditación REQ-002 referencia la hora extra (validación al converger — RF-013); el pago vive en REQ-006/nómina externa; capacitación + guía FE; validación dura = F2 |
| G-09 | «Consulta de solicitudes… exportar a Excel» sin bandeja propia | Bandeja corporativa `POST /query` + `StatusCounts` + **totales en horas** + `GET /export` (molde completo verificado) + **insumo de planilla por periodo** (D-15, RF-011/RF-012) |

---

## Decisiones — D-01…D-21 (**RATIFICADAS por el negocio, 2026-07-06**)

> ✅ **Ratificadas junto con las respuestas P-01…P-14 (§17)** — 12 recomendaciones aceptadas sin cambios; **D-08 ajustada** (P-02: también jornadas futuras organizadas por cada área) y **D-16 ajustada + D-21 nueva** (P-08: la liquidación calcula las pendientes de pago). Las marcadas «(espejo …)» heredan decisiones que el negocio ya había ratificado para los módulos hermanos.

| # | Tema | Decisión propuesta |
|---|---|---|
| D-01 | Fases | **F1 (este análisis)**: las tres opciones del levantamiento — registro de jornadas con flujo de autorización + 2 maestros por empresa con plantilla + consulta corporativa con Excel — más aplicación por periodo (P-07), insumo exportable, bandeja de pendientes/atrasados, **registro anticipado de jornadas organizadas** (fecha futura — P-02) e **integración con liquidación** (línea calculada de pendientes de pago — P-08, D-21). **F2+**: vínculo activo con REQ-002 (validar/poblar `overtimeRecordPublicId`, anti-doble-beneficio duro), generación del pago (ingreso eventual REQ-006 o motor), cálculo normativo de tarifas (motor), portal de jefaturas (requiere modelo de equipos), **conciliación planificado-vs-trabajado** de las jornadas futuras, sugerencias por asuetos/día de descanso (REQ-001), notificaciones |
| D-02 | Modelado | **Entidad transaccional del expediente** `PersonnelFileOvertimeRecord` (nombre final lo fija el plan técnico, alineado a la costura de REQ-002). **Sin monto, moneda ni centro de costo persistidos**: la unidad de negocio es **horas × factor**; el contexto monetario (salario, centro de costo) se **deriva al exportar el insumo** desde la plaza. No toca ledgers ni conceptos de compensación |
| D-03 | Maestro de tipos de hora extra | **Maestro por empresa** (código, nombre, **factor default** > 0, descripción de condiciones/efecto en pago, orden, baja lógica) **con plantilla**: los 4 tipos del levantamiento con factores de referencia CT (A.2 — HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00), editables por cada empresa; patrón plantilla + `load-template` (REQ-001; seeder propio si este módulo se adelanta). Al registrar: **snapshot** código + nombre + factor (P-03/P-04) |
| D-04 | Maestro de justificaciones | **Maestro por empresa** (código, nombre, descripción, orden, baja lógica) **con plantilla** (6 razones sugeridas — A.2). El «motivo» del registro = referencia al maestro (obligatoria, con snapshot) + **observaciones libres opcionales** (P-09) |
| D-05 | Flujo y estados | **Una decisión** (pedida por el levantamiento): crear → `EN_REVISION` (editable); decidir → `AUTORIZADA` (pendiente de aplicarse en planilla) o `RECHAZADA` (motivo obligatorio); `AUTORIZADA → APLICADA` al constatarse el procesamiento en planilla (P-07); `ANULADA` desde `EN_REVISION` (retiro del trámite) o desde `AUTORIZADA` (revocación con motivo). **5 estados**, patrón híbrido (constantes canónicas + catálogo editable) |
| D-06 | Anti-autoaprobación | **Triple** (espejo D-05 de REQ-006, ratificada; precedente exacto en guards de retiro): quien decide no puede ser (a) el **empleado sujeto** (`LinkedUserPublicId`), (b) **quien registró** (`RequestedByUserId`), ni (c) el **solicitante** (vía `LinkedUserPublicId` del expediente solicitante) → 403 `SELF_APPROVAL_FORBIDDEN`. En el canal portal (a)=(b)=(c): un tercero facultado siempre decide |
| D-07 | Permisos | 3 codes dedicados: `PersonnelFiles.ViewOvertimeRecords` / `ManageOvertimeRecords` / `AuthorizeOvertimeRecords`. `View`/`Manage` con receta estándar (fallback `Admin`/`ManageAdministration`); **`Authorize*` con policy que omite `PersonnelFiles.Admin`** (molde exacto `Program.cs:564-569`; el permiso sí se aprovisiona en `CompanyAdminPermissions` como grant asignable — precisión №4 del veredicto). Decisión en **controller dedicado** (`AuthorizationPolicySet` es class-only); la escritura de creación queda **authn-only en la policy** con gates en handler (habilita el canal portal — precedente `PersonnelFilePolicies.cs:47-53`) |
| D-08 | Fecha y duración (**ajustada — P-02**) | Fecha trabajada **pasada** (retroactiva — adopción/históricos) **o futura**: el negocio ratificó registrar «horas futuras que previamente fueron **organizadas por cada área**» (jornada organizada; sin tope normativo — el plan técnico puede fijar un límite de sanidad). **Salvaguardas derivadas**: la **aplicación** exige fecha ya transcurrida (RN-09) y al retiro las futuras no trabajadas se **anulan** (D-16/D-21); la **conciliación** planificado-vs-trabajado es F2. Duración en **horas y minutos** (`durationHours` ≥ 0, `durationMinutes` 0–59, total > 0) → **horas decimales derivadas** a 2 decimales (2 h 30 m = 2.50) como cifra de totales/insumo; **hora inicio/fin opcionales informativas** (espejo D-06 de REQ-002). **Tope diario opcional por preferencia** (`OvertimeMaxDailyMinutes`, null = sin tope; si se configura, exceder → 422) (P-05). Varios registros del mismo día/empleado permitidos (p. ej. diurna + nocturna) — sin control de solape horario en F1 (no se capturan rangos obligatorios) |
| D-09 | Factor | Default del **tipo** (snapshot al registrar); **editable por registro** con **nota obligatoria** cuando difiere del factor vigente del tipo (espejo del factor «ajustable con nota» de la acreditación REQ-002); factor > 0. El factor es **insumo** para la planilla externa — este módulo no lo convierte en dinero (P-06) |
| D-10 | Canal del portal (**P-01 estructural**) | **Dual**: (a) **RRHH** (`ManageOvertimeRecords`) registra sobre cualquier expediente; (b) **autoservicio**: el empleado registra **sus propias** horas trabajadas (policy authn-only + `isSelf` en handler — precedente triple), habilitado por preferencia de empresa **`OvertimeSelfServiceEnabled`** (default **deshabilitado** hasta adopción). **Origen trazado** en cada solicitud (`RRHH` / `PORTAL`). En autoservicio el solicitante = el propio expediente y el registro nace `EN_REVISION` igual (la autorización SIEMPRE la da un tercero facultado). **Jefatura por su equipo = F2** (requiere modelo de equipos inexistente) |
| D-11 | Solicitante («empleado que solicita») | Trío del precedente liquidación/retiro: **`RequesterFilePublicId`** (expediente del empleado que solicita — normalmente la jefatura; puede ser el propio empleado; no requiere ser usuario del sistema) + **`RequesterNameSnapshot`** + **`RequestedByUserId`** (auditoría de quién digitó). Obligatorio y **participa en la anti-autoaprobación** (D-06) |
| D-12 | Plaza asociada | **Obligatoria** (default: la **principal**): ancla el contexto que la nómina externa necesita (salario/jornada de referencia y centro de costo se **derivan al exportar**, sin snapshot monetario en el registro). Empleado multi-plaza: la jornada extraordinaria se imputa a la plaza donde se trabajó |
| D-13 | Imputación de planilla y periodo (espejo D-08 de REQ-006 / D-10 de REQ-005) | Par **`payrollTypeCode`** (catálogo país REQ-004, obligatorio; default: el de la plaza si está clasificada) + **`payrollPeriodId?`** (FK al maestro REQ-001 cuando exista) + **`payrollPeriodLabel`** snapshot obligatoria. **Re-imputable mientras `AUTORIZADA`** («enviar a otro periodo», If-Match + auditoría); al aplicar se snapshotea el periodo **real**. Degradación a etiqueta libre documentada si este módulo se adelanta a REQ-001 |
| D-14 | Aplicación (F1) (espejo D-09 de REQ-006) | **Constancia manual** de que las horas se procesaron en la planilla: unitaria o **en lote por periodo con exclusión** (= posponer), solo sobre `AUTORIZADA`, bajo **lock anti-carrera** + re-verificación en transacción (cada solicitud se aplica **una sola vez**); origen `MANUAL` (F1) / `MOTOR` (F2). **Reversión con motivo** (`APLICADA → AUTORIZADA`, aplicación anulada trazada y visible) (P-07) |
| D-15 | Consulta y exportaciones | (a) **Bandeja corporativa** `POST /companies/{companyId}/overtime-records/query`: filtros estado(s) / empleado / tipo / justificación / rango de fecha trabajada / tipo de planilla / periodo / solicitante / **origen** (`RRHH`/`PORTAL`) / plaza; paginada con `StatusCounts` y **totales en horas** (globales y por tipo); detalle completo por solicitud. (b) Listado por expediente + **lectura self** del propio historial cuando el canal portal esté activo (P-12). (c) **Exports**: xlsx/csv/json de la consulta y de la bandeja de pendientes + **insumo de planilla por periodo** (empleado, plaza, tipo snapshot, factor, horas decimales, fecha, justificación, solicitante) — el puente operativo con la nómina externa |
| D-16 | Retiro (**ajustada — P-08**) | Perfil `RETIRADO`: sin altas nuevas; `EN_REVISION` solo `RECHAZADA`/`ANULADA`; las `AUTORIZADA` pendientes de pago **se resuelven vía la línea calculada de la liquidación (D-21)** — la resolución manual queda solo para lo excluido de la sugerencia; las de **fecha futura** (organizadas y no trabajadas) se **anulan** al cierre. El saldo **compensatorio** al retiro sigue siendo de REQ-002 (`HORAS_EXTRAS_PENDIENTES=-9837`, su D-19) |
| D-17 | Costuras con los hermanos | (a) **REQ-002 (compensación en tiempo)**: la acreditación compensatoria lleva `overtimeRecordPublicId` opcional (su D-21) — cuando ambos módulos convivan, **este módulo valida el vínculo** (registro existente, mismo empleado, estado `AUTORIZADA`) y la solicitud exhibe la insignia «**compensada con tiempo**»; una hora extra compensada queda **excluida del insumo de pago** (anti-doble-beneficio blando — RF-013, prioridad Media, condicionada a que REQ-002 esté mergeado; P-14). (b) **REQ-006 (pago)**: si la empresa registra el pago en CLARIHR, usa un **ingreso eventual** tipo `HORAS_EXTRA` — frontera editorial (jornada aquí, dinero allá; A.5); referencia cruzada activa = F2 |
| D-18 | Catálogos y seeds | Nuevo catálogo de **estados** (`OVERTIME_RECORD_STATUS_CATALOG`, wire `overtime-record-statuses`): 5 estados → **`-9910…-9914`**; concepto de liquidación `HORAS_EXTRAS_PENDIENTES_PAGO` → **`-9915`** (D-21); holgura `-9916…-9919` (bloque nuevo tras la reserva `-9900…-9909` de REQ-006). Los **2 maestros por empresa** no llevan seeds globales (plantilla vía seeder/`load-template`). `PAYROLL_TYPE_CATALOG` = definición compartida REQ-004 (`-9890…-9895`), la siembra el primero que construya. Todos `HasData` idempotentes; verificación de IDs libres al abrir el PR |
| D-19 | Asientos de expediente | **Sin asientos** en el journal (`PersonnelFilePersonnelAction`): espejo de REQ-005/REQ-006 (dato operativo de compensación, no hecho del expediente laboral) **más un argumento propio de volumen** — las horas extras pueden ser diarias por empleado e inundarían el journal que el tablero REQ-004 grafica. Sin ActionTypes nuevos |
| D-20 | Secuenciación | Registrar como **REQ-007** al final del backlog. Orden recomendado: **después de REQ-001** (plantillas/`load-template`, maestro de periodos, asuetos/`restDayOfWeek` para el F2) y **cerca de REQ-002** (activar la costura D-17a con RF-013 y compartir la preferencia de horas-día estándar de D-21). **Adelantable** con tres degradaciones documentadas: etiqueta de periodo (D-13), sembrar `payroll-types` aquí (D-18) y seeder de plantillas propio (D-03/D-04). No depende de REQ-005/REQ-006 |
| D-21 | Integración con liquidación (**nueva — P-08**) | «La liquidación debe **calcular** las horas extras pendientes de pago»: al liquidar una plaza, las solicitudes `AUTORIZADA` de esa plaza no aplicadas, **no compensadas** (RN-16) y con **fecha ya trabajada** sugieren una **línea calculada por el motor** — monto = Σ(horas decimales × factor) × **valor-hora del contexto salarial** (salario/30 ÷ horas-día estándar; preferencia de REQ-002 cuando exista, default 8) — con concepto **nuevo** `HORAS_EXTRAS_PENDIENTES_PAGO` (**`-9915`** candidato). **NO** se reutiliza `-9837` (REQ-002, saldo compensatorio): ambas líneas **coexisten sin doble conteo** gracias a RN-16. Línea editable/excluible con snapshot y traza de los registros incluidos; al **emitir** → `APLICADA` origen `LIQUIDACION` + referencia a la liquidación; **anular la liquidación reabre** a `AUTORIZADA` (simetría de la familia). Las futuras no trabajadas al retiro → se anulan, no se pagan. Mecánica: línea de sistema condicional espejo de la integración de REQ-002 (nombres reales `UnitsOrDays`/`UnitsOverridden`/`OverrideAmount`) |

---

## 1. Resumen del producto o requerimiento

Se construirá el **módulo de horas extras** de CLARIHR: el registro, con **flujo de autorización**, de las **jornadas extraordinarias trabajadas** por los empleados — qué día, cuántas horas y minutos, de qué tipo (diurna, nocturna, festiva…), con qué **factor** de recargo, **quién lo solicita** y **por qué razón** —, imputadas a la **planilla y periodo** donde se pagarán, junto con los dos **catálogos maestros** que lo alimentan (tipos de hora extra con su efecto en el pago, y tipos de justificación) y una **consulta corporativa de solicitudes** con exportación a **Excel** e **insumo por periodo** para la nómina.

**Qué se construye (F1).**

1. **Nueva hora extra**: registro sobre el expediente con empleado (y su plaza), **tipo de hora extra** (maestro por empresa, con snapshot), **factor** (default del tipo, editable con nota), **fecha trabajada** — pasada o **futura** (jornada organizada previamente por el área, P-02) —, **duración en horas y minutos** (con hora inicio/fin opcionales), **empleado que solicita** (trío con snapshot), **motivo** (tipo de justificación + observaciones) y **nómina + periodo** destino. Nace `EN_REVISION`.
2. **Flujo de autorización** (pedido por el levantamiento): un tercero facultado **autoriza o rechaza**; anti-autoaprobación triple (sujeto/registrador/solicitante); revocación con motivo; el permiso de decidir no lo hereda el Admin.
3. **Canal del portal** (P-01): además del registro por RRHH, el **empleado puede registrar sus propias horas** desde el portal (autoservicio con precedente triple en el sistema), si la empresa habilita la preferencia; cada solicitud queda trazada con su **origen** (`RRHH`/`PORTAL`).
4. **Catálogos**: maestro por empresa de **tipos de hora extra** (plantilla con los 4 tipos del levantamiento y factores de referencia del Código de Trabajo, editables) y maestro de **tipos de justificación** (plantilla sugerida); adopción del catálogo país de **tipos de planilla** (definición REQ-004) y del maestro de **periodos** (REQ-001, con degradación).
5. **Aplicación por periodo**: constancia de que las horas autorizadas se procesaron en la planilla (unitaria o en lote con exclusión = posponer), bandeja de **pendientes/atrasados**, re-imputación («enviar a otro periodo») y reversión con motivo.
6. **Consulta de solicitudes**: bandeja corporativa con filtros (incluido el **origen portal**), `StatusCounts`, **totales en horas**, detalle completo, **exportación a Excel** e **insumo de planilla por periodo** (horas × factor por empleado/tipo — lo que la nómina externa necesita para pagar).
7. **Integración con liquidación** (P-08): al retiro, las horas autorizadas pendientes de pago se sugieren como **línea calculada** del finiquito — Σ(horas × factor) × valor-hora del contexto salarial — con cierre al emitir y reapertura al anular (RF-014).

**Qué NO se construye en F1.** El **valor monetario** de las horas en la operación regular (sin monto, sin lookup de salario, sin tarifas legales automáticas — el cálculo pertenece a la planilla externa hoy y al motor futuro; **única excepción ratificada**: la línea calculada al liquidar, P-08); el **pago** (vive en REQ-006 o en la nómina externa — frontera documentada); la **compensación en tiempo** (vive en REQ-002; aquí solo la validación del vínculo cuando ambos convivan — RF-013); el **portal de jefaturas** (requiere modelo de equipos); la **conciliación** planificado-vs-trabajado de las jornadas futuras; notificaciones.

**Problema que resuelve.** Hoy las horas extras trabajadas no tienen dónde registrarse ni quién las autorice formalmente: el sistema solo conoce «horas extra» como un concepto de pago (catálogo) y como una línea manual del finiquito. No hay trazabilidad de qué jornadas se trabajaron, quién las solicitó y autorizó, con qué justificación, ni un insumo confiable por periodo para pagarlas — ni forma de conectarlas después con su pago (REQ-006) o su compensación en tiempo (REQ-002) sin riesgo de doble beneficio.

---

## 2. Objetivos del negocio

1. **Control y debido proceso sobre la jornada extraordinaria**: ninguna hora extra queda registrada como válida sin la **autorización de un tercero facultado** (anti-autoaprobación triple; el Admin no decide sin grant explícito) — es el pedido explícito del levantamiento.
2. **Trazabilidad completa de la jornada**: qué día, cuántas horas y minutos, de qué tipo y con qué factor, quién la solicitó, quién la registró, por qué razón (justificación del catálogo) y en qué planilla/periodo se pagará.
3. **Insumo exacto para la nómina**: exportación por planilla + periodo de las horas autorizadas (horas decimales × factor por empleado y tipo), eliminando el control paralelo en Excel y las transcripciones manuales; el modelo queda listo para el motor futuro.
4. **Canal de entrada desde el portal**: las solicitudes pueden originarse en el empleado (autoservicio) y RRHH las consulta de forma centralizada — la «consulta de solicitudes que han ingresado desde el portal» del levantamiento.
5. **Estandarización institucional**: los tipos de hora extra (con su efecto en el pago) y las razones válidas de justificación son **catálogos gobernados por la empresa**, no texto libre.
6. **Base para el ecosistema de horas extras**: costuras listas hacia la compensación en tiempo (REQ-002) y el pago (REQ-006), con el anti-doble-beneficio como objetivo explícito del diseño (una jornada no debe pagarse Y compensarse).
7. **Cierre limpio al liquidar** (P-08): las jornadas autorizadas y no pagadas se resuelven en el finiquito con una **línea calculada por el motor** — sin horas huérfanas al retiro ni cálculos manuales del analista.

---

## 3. Alcance funcional

### Fase 1 — MVP (este análisis)

- **Configuración**: catálogo de estados; maestro por empresa de **tipos de hora extra** (plantilla A.2, factores editables); maestro por empresa de **tipos de justificación** (plantilla A.2); adopción/siembra del catálogo país de **tipos de planilla** (definición REQ-004); 3 permisos RBAC (`View`/`Manage`/`Authorize`); 2 preferencias de empresa (canal portal + tope diario opcional).
- **Registro de horas extras**: crear/editar (`EN_REVISION`) por RRHH sobre cualquier expediente y por el **empleado sobre el propio** (canal portal, si la preferencia lo habilita), con origen trazado y **fecha pasada o futura** (jornada organizada por el área — P-02); decidir (autorizar / rechazar con motivo); anular/revocar; **re-imputar el periodo destino** («enviar a otro periodo»).
- **Aplicación**: unitaria y **en lote por periodo con exclusión** (posponer); reversión con motivo; bandeja de **pendientes/atrasados**.
- **Consulta**: bandeja corporativa con los filtros del levantamiento + extras de la casa (incluido origen `RRHH`/`PORTAL`), `StatusCounts`, **totales en horas** (globales y por tipo), detalle por solicitud; listado por expediente; lectura self del propio historial (canal portal).
- **Exportaciones**: consulta y bandejas a **xlsx**/csv/json; **insumo de planilla por periodo** (horas × factor por empleado/tipo, con el contexto de la plaza).
- **Integración con liquidación** (P-08 — RF-014): pendientes de pago → **línea calculada** `HORAS_EXTRAS_PENDIENTES_PAGO` (Σ horas × factor × valor-hora; editable/excluible); cierre al emitir (`APLICADA` origen `LIQUIDACION`); reapertura simétrica al anular.
- **Costura con REQ-002** (solo si aquel ya está mergeado al construir este — RF-013, prioridad Media): validación del vínculo `overtimeRecordPublicId` desde las acreditaciones + insignia «compensada con tiempo» + exclusión del insumo de pago.

### Fase 2+ — contrato preparado, fuera de este MVP

- **Vínculo activo pleno** con REQ-002 (poblar/validar en ambos sentidos, anti-doble-beneficio duro) y con REQ-006 (generar el ingreso eventual `HORAS_EXTRA` desde horas autorizadas, o referencia cruzada del pago).
- **Motor de planilla**: cálculo normativo (valor-hora desde el salario, recargos legales), aplicación automática con origen `MOTOR`.
- **Portal de jefaturas** (registrar/solicitar por «mi equipo» — requiere modelo de equipos/jerarquía).
- **Conciliación planificado-vs-trabajado** de las jornadas futuras (confirmar lo efectivamente trabajado contra lo organizado por el área — el registro anticipado en sí ya es F1, P-02).
- **Sugerencias por calendario** (REQ-001): proponer tipo festivo si la fecha cae en asueto institucional; señalar trabajo en día de descanso (`restDayOfWeek`, Art. 175 CT — con su día compensatorio, costura REQ-002).
- Notificaciones (al autorizador, al solicitante); agrupación dimensional de la consulta (`groupBy` espejo REQ-006) si el negocio la pide.

---

## 4. Fuera de alcance

- **Valor monetario de las horas** (monto, moneda, valor-hora, recargos calculados): el módulo entrega **horas × factor**; el dinero lo pone la planilla externa o el ingreso eventual (REQ-006). Sin lookup de salario (frontera P-03/P-04 de REQ-006 heredada).
- **El pago en sí** (registro del monto pagado): módulo de ingresos eventuales (REQ-006) o nómina externa — frontera editorial A.5.
- **La compensación en tiempo** (acreditar horas al fondo compensatorio): módulo REQ-002; aquí solo la validación del vínculo cuando ambos convivan (RF-013).
- **Motor de planilla** (corrida del periodo, posteo): programa aparte ya faseado (P-01 de REQ-005).
- **Control de asistencia/marcaciones** (relojes, biométricos, turnos): este módulo registra jornadas extraordinarias declaradas y autorizadas, no marca tiempos.
- **Portal de jefaturas / modelo de equipos** (F2 — P-01).
- **Conciliación planificado-vs-trabajado** de las jornadas futuras (F2 — P-02; el registro con fecha futura sí es F1).
- **Escritura de ledgers** (`PersonnelFilePayrollTransaction`) o de conceptos de compensación (la línea calculada de D-21 viaja por el canal del motor de liquidación, no por escritura directa de este módulo).
- **Adjuntos** (P-10): la autorización es EN el sistema (el flujo es el respaldo); si el negocio exige documento, patrón espejo de REQ-002 como evolución aditiva.
- **Notificaciones**; **importador masivo** de históricos (adopción = registro retroactivo por el flujo normal, P-13).

---

## 5. Actores o usuarios involucrados

| Actor | Rol en el módulo |
|---|---|
| **Administrador de empresa** | Mantiene los 2 maestros (tipos + justificaciones), configura las preferencias (canal portal, tope diario) y asigna permisos |
| **Gestor de RRHH** (con `ManageOvertimeRecords`) | Registra y edita solicitudes sobre cualquier expediente, re-imputa periodos, **aplica** (unitaria/lote), revierte aplicaciones con motivo, exporta el insumo |
| **Empleado (portal)** | Registra **sus propias** horas trabajadas desde el portal (si la empresa habilitó el canal — P-01) y consulta el estado de sus solicitudes |
| **Solicitante** («empleado que solicita» — normalmente la jefatura) | Origina la solicitud; queda **registrado en el trío solicitante** (con snapshot); no requiere ser usuario del sistema; **no puede autorizar** lo que solicitó |
| **Autorizador** (con `AuthorizeOvertimeRecords`) | Decide (autoriza/rechaza) las solicitudes `EN_REVISION`; revoca autorizadas con motivo; sujeto a anti-autoaprobación triple; **Admin no decide** sin el grant explícito |
| **Consulta de RRHH / Auditor** (con `ViewOvertimeRecords`) | Solo lectura: consulta corporativa, detalle, exportaciones |
| **Analista de planilla / Finanzas** | Consume el **insumo por periodo** (horas × factor) y aplica/constata el procesamiento |
| **Sistema de planilla externa** | Recibe el insumo del periodo y calcula/paga el valor monetario (mientras no exista el motor interno) |
| **Módulo de tiempo compensatorio (REQ-002)** | Referencia las horas extras autorizadas al acreditar tiempo (`overtimeRecordPublicId`) — validación al converger (RF-013) |
| **Motor de liquidación (interno)** | Calcula y sugiere la línea de horas pendientes de pago al liquidar (Σ horas × factor × valor-hora) y dispara el cierre al emitir / la reapertura al anular (RF-014) |

---

## 6. Requerimientos funcionales

> Agrupados en 4 grupos (A: configuración · B: registro y flujo · C: aplicación por periodo · D: consulta, exportaciones, costuras e integración con liquidación). Prioridades: Alta = imprescindible F1; Media = F1 deseable/recortable.

### Grupo A — Configuración y catálogos

### RF-001 - Estados, permisos y preferencias del módulo

**Descripción:** Sembrar el catálogo de estados (`EN_REVISION`, `AUTORIZADA`, `RECHAZADA`, `APLICADA`, `ANULADA` — D-05) y declarar los 3 permisos (D-07) con la receta completa; agregar las 2 preferencias de empresa (`OvertimeSelfServiceEnabled` default off, `OvertimeMaxDailyMinutes` nullable).

**Reglas de negocio:**
- Patrón híbrido (constantes canónicas + catálogo editable; validación de código activo en escrituras).
- IDs de semilla en bloque nuevo **`-9910…-9914`** (Anexo A.3), verificados contra `GlobalCatalogSeedData` al abrir el PR (respetar reservas REQ-001…REQ-006; trampa `-9490…-9496`).
- `AuthorizeOvertimeRecords` con policy que **omite Admin** (molde `Program.cs:564-569`); la policy de creación queda authn-only con gates en handler (canal portal); gates fail-closed + governance tests; decisión en controller dedicado.

**Criterios de aceptación:**
- Migración `HasData` idempotente; usuario sin permiso → 403 en cada endpoint; **Admin sin `Authorize*` no puede decidir**; preferencia portal off → autoservicio 403/422.

**Prioridad:** Alta
**Dependencias:** Verificación de IDs libres; D-05/D-06/D-07/D-18.

### RF-002 - Maestro por empresa de tipos de hora extra (opción 2a del levantamiento)

**Descripción:** CRUD del maestro `overtime-types` por empresa: código, nombre, **factor default** (> 0, hasta 2 decimales), descripción de las condiciones/efecto en el pago, orden, baja lógica; **plantilla** con los 4 tipos del levantamiento y factores de referencia CT (A.2) cargable vía `load-template` (patrón REQ-001; seeder propio si este módulo se adelanta).

**Reglas de negocio:**
- Código único normalizado por empresa; edición del factor NO recalcula registros históricos (snapshot al registrar — RN-04/RN-05).
- Baja lógica: un tipo inactivo no admite registros nuevos; los históricos conservan su snapshot.
- Los factores de la plantilla son **referencia editable** (sin tarifas hardcodeadas); confirmación del contador antes del despliegue (P-03).

**Criterios de aceptación:**
- `load-template` crea los 4 tipos con sus factores de referencia; crear registro con tipo inactivo → 422; cambiar el factor del tipo no altera solicitudes existentes.

**Prioridad:** Alta
**Dependencias:** RF-001; patrón de plantillas (REQ-001 PR-1 o seeder propio).

### RF-003 - Maestro por empresa de tipos de justificación (opción 2b del levantamiento)

**Descripción:** CRUD del maestro `overtime-justification-types` por empresa: código, nombre, descripción, orden, baja lógica; **plantilla** con las razones sugeridas (A.2).

**Reglas de negocio:**
- Código único normalizado por empresa; baja lógica con snapshot en los registros históricos.
- El «motivo» de toda solicitud referencia un tipo **activo** del maestro (+ observaciones libres opcionales — D-04).

**Criterios de aceptación:**
- `load-template` crea las razones sugeridas; solicitud con justificación inexistente/inactiva → 422.

**Prioridad:** Alta
**Dependencias:** RF-001.

### RF-004 - Catálogo país de tipos de planilla (definición compartida con REQ-004)

**Descripción:** Adoptar la especificación única de REQ-004 (`PAYROLL_TYPE_CATALOG`: `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO`) con seeds re-ubicados **`-9890…-9895`** — la siembra el REQ que se construya primero (REQ-001/004/005/006/007 la comparten).

**Reglas de negocio:**
- Una sola definición en todo el sistema; si otro REQ ya lo sembró, aquí solo se consume.
- La solicitud valida `payrollTypeCode` contra el catálogo activo → 422 si inválido; default: el tipo de planilla de la plaza si está clasificada.
- La migración de limpieza destructiva de `payroll_type_code` en plazas es de REQ-004 (no se repite aquí).

**Criterios de aceptación:**
- Solicitud con tipo de planilla inexistente/inactivo → 422 bilingüe; catálogo consultable por el FE.

**Prioridad:** Alta
**Dependencias:** Coordinación en backlog (D-16/D-18 de REQ-004/005/006 + D-18 de este).

### Grupo B — Registro y flujo

### RF-005 - Crear y editar solicitud de horas extras (opción 1 del levantamiento)

**Descripción:** Alta por RRHH sobre el expediente: **fecha trabajada**, **tipo de hora extra** (maestro, con snapshot de código/nombre/factor), **factor aplicado** (default el del tipo; editable con nota — D-09), **duración en horas y minutos** (hora inicio/fin opcionales), **empleado que solicita** (trío con snapshot), **motivo** (tipo de justificación + observaciones), **plaza** (obligatoria, default la principal), **nómina** (catálogo) y **periodo** destino (FK/etiqueta). Nace `EN_REVISION`; editable mientras siga en revisión.

**Reglas de negocio:**
- Solo `ManageOvertimeRecords` (el canal portal es RF-006); If-Match en ediciones; perfil `RETIRADO` → 422 (RN-12).
- Fecha pasada **o futura** (jornada organizada previamente por el área — RN-07/P-02); duración: horas ≥ 0, minutos 0–59, total > 0 → horas decimales derivadas a 2 decimales (RN-06); tope diario si la preferencia está configurada (RN-08).
- Tipo y justificación activos del maestro → snapshot (RN-04); factor > 0, nota obligatoria si difiere del factor vigente del tipo (RN-05).
- Solicitante obligatorio: expediente activo de la empresa + snapshot (RN-03).
- Tipo de planilla del catálogo activo; periodo destino FK al maestro si existe (o etiqueta en modo degradado) (RN-15).
- El estado nunca se digita (RN-01); solo `EN_REVISION` es editable (RN-02); origen = `RRHH` (RN-17).

**Criterios de aceptación:**
- POST → 201 `EN_REVISION` con `publicId` y ETag; 2 h 30 m → `durationDecimalHours` 2.50; minutos = 65 → 422; factor distinto al del tipo sin nota → 422; edición tras autorizar → 422.

**Prioridad:** Alta
**Dependencias:** RF-001…RF-004.

### RF-006 - Canal del portal: registro de horas propias (autoservicio)

**Descripción:** El empleado autenticado con expediente vinculado registra **sus propias** horas trabajadas (mismos campos de RF-005) cuando la empresa habilitó `OvertimeSelfServiceEnabled`; la solicitud nace `EN_REVISION` con **origen `PORTAL`**, solicitante = su propio expediente, y el empleado puede editarla/retirarla mientras siga en revisión; lectura del propio historial (RF-011c).

**Reglas de negocio:**
- Gate en handler: `PersonnelFile.LinkedUserPublicId == usuario autenticado` (molde `LoadForCreateOwnOrManage*`, `PersonnelFileEmployeeHandlerBases.cs:227+`); policy authn-only (D-07/D-10).
- Preferencia off → 403/422 accionable; el canal NO permite registrar sobre expedientes ajenos (jefaturas = F2).
- Mismas validaciones de negocio de RF-005; la decisión SIEMPRE la toma un tercero facultado (RN-03).

**Criterios de aceptación:**
- Con preferencia on: el empleado crea su solicitud (origen `PORTAL`, solicitante él mismo) y la ve en su historial; con preferencia off → rechazo accionable; intento sobre expediente ajeno → 403.

**Prioridad:** Alta (condicionada a **P-01** — si el negocio elige «solo RRHH», este RF pasa a F2 y la consulta pierde el filtro de origen)
**Dependencias:** RF-005; preferencia de empresa (RF-001).

### RF-007 - Decidir solicitud (autorizar / rechazar)

**Descripción:** PATCH de decisión sobre una solicitud `EN_REVISION`: **autorizar** (queda `AUTORIZADA`, pendiente de aplicarse y visible en el insumo de su periodo) o **rechazar** (motivo obligatorio) — controller de resolución dedicado.

**Reglas de negocio:**
- Solo `AuthorizeOvertimeRecords` (policy omite Admin); **anti-autoaprobación triple** (RN-03): 403 si quien decide es el empleado sujeto, quien registró **o el solicitante** (molde `RetirementAuthorizerGuards`).
- Re-verificación de estado dentro de la transacción (dos decisiones concurrentes → la segunda 409/422).
- Sobre perfil `RETIRADO` solo se admite rechazar/anular (D-16).

**Criterios de aceptación:**
- Decisión por el registrador, el sujeto o el solicitante → 403; autorizar → `AUTORIZADA` y aparece en la bandeja de pendientes de su periodo; rechazar sin motivo → 422; Admin sin grant → 403.

**Prioridad:** Alta
**Dependencias:** RF-005/RF-006.

### RF-008 - Ciclo post-decisión: re-imputar periodo, anular / revocar

**Descripción:** (a) **Re-imputación del periodo destino** de una `AUTORIZADA` («enviar a otro periodo»): cambiar tipo de planilla y/o periodo antes de aplicar, con If-Match y auditoría; (b) **anulación** con motivo desde `EN_REVISION` (retiro del trámite — en el canal portal la ejerce también el propio empleado) o desde `AUTORIZADA` (revocación — la jornada deja de ser pagadera).

**Reglas de negocio:**
- Motivos obligatorios en anulación/revocación; baja lógica, nunca borrado (RN-10).
- `RECHAZADA`/`ANULADA` son terminales; `APLICADA` no se anula directamente — primero se revierte la aplicación (RF-009, D-14).
- La re-imputación no altera horas/factor (solo destino); una `ANULADA` desaparece de bandejas e insumos.

**Criterios de aceptación:**
- Re-imputar una autorizada de la quincena 13 a la 14 → sale del insumo de la 13 y entra al de la 14 (trazado); revocar una autorizada → `ANULADA` con motivo, fuera de insumos.

**Prioridad:** Alta
**Dependencias:** RF-007.

### Grupo C — Aplicación por periodo

### RF-009 - Aplicar a planilla (unitaria y en lote por periodo) y revertir

**Descripción:** (a) Aplicación **unitaria**: constatar que una solicitud `AUTORIZADA` se procesó en la planilla, con fecha y periodo real (default: el destino declarado) y nota; (b) aplicación **en lote por periodo**: para un tipo de planilla + periodo, el sistema presenta las autorizadas pendientes y RRHH confirma el lote **pudiendo excluir registros** (posponer); (c) **reversión** con motivo de una aplicación errónea (la solicitud vuelve a `AUTORIZADA`; la aplicación anulada queda trazada y visible).

**Reglas de negocio:**
- Solo `ManageOvertimeRecords`; solo solicitudes `AUTORIZADA` **con fecha trabajada ya transcurrida** (no se constata el pago de una jornada futura aún no trabajada — RN-09, salvaguarda de P-02); **lock anti-carrera** + re-verificación en transacción: cada solicitud se aplica **una sola vez** (RN-09).
- Al aplicar: snapshot del periodo real (tipo + FK/etiqueta), fecha, quién y origen `MANUAL` (F1; `MOTOR` en F2; `LIQUIDACION` vía RF-014) (D-14).
- Las horas/factor aplicados son los del registro (no editables al aplicar; correcciones = reversión + re-aplicación o revocación).

**Criterios de aceptación:**
- Lote del periodo con 10 pendientes y 2 excluidas → 8 `APLICADA`, 2 siguen pendientes; doble submit concurrente → sin dobles aplicaciones (test de carrera); aplicar una `EN_REVISION`/`ANULADA` → 422; **aplicar una jornada con fecha futura → 422**; reversión con motivo → vuelve a `AUTORIZADA` con la aplicación anulada visible.

**Prioridad:** Alta (condicionada a **P-07**; si el negocio cierra el ciclo en `AUTORIZADA`, este RF y RF-010 pasan a F2 y el insumo lista autorizadas sin marca de aplicación)
**Dependencias:** RF-007.

### RF-010 - Bandeja de pendientes de aplicar («horas no aplicadas en planilla»)

**Descripción:** Bandeja corporativa de solicitudes `AUTORIZADA` no aplicadas, con filtros por tipo de planilla, periodo destino y empleado, y marca de **atrasada** (el periodo destino ya venció sin aplicarse); desde la bandeja se lanza la aplicación en lote (RF-009) o la re-imputación (RF-008).

**Reglas de negocio:**
- Solo `AUTORIZADA`; «atrasada» = periodo destino con fecha fin pasada (maestro REQ-001) o etiqueta de periodo anterior al actual (modo degradado documentado).
- Es la aproximación F1 de «qué horas autorizadas aún no se han pagado» (RN-13).

**Criterios de aceptación:**
- Autorizada imputada a una quincena vencida y sin aplicar → marcada atrasada; tras aplicarla o re-imputarla desaparece de la vista del periodo.

**Prioridad:** Alta (condicionada a P-07, junto con RF-009)
**Dependencias:** RF-009.

### Grupo D — Consulta, exportaciones y costuras

### RF-011 - Consulta de solicitudes de horas extras (opción 3 del levantamiento)

**Descripción:** (a) **Consulta corporativa** (`POST /companies/{companyId}/overtime-records/query`): filtros estado(s), empleado, tipo de hora extra, justificación, rango de fecha trabajada, tipo de planilla, periodo, solicitante, **origen** (`RRHH`/`PORTAL` — «las que han ingresado desde el portal» = filtro origen `PORTAL`), plaza; paginada, con **`StatusCounts`** y **totales en horas** (globales y por tipo); cada fila muestra el estado y el acceso al **detalle** completo (duración, factor y su nota, solicitante, justificación, decisión, aplicaciones, auditoría). (b) Listado paginado por expediente (filtros: estado, tipo, rango de fechas). (c) **Lectura self** del propio historial cuando el canal portal esté activo (P-12).

**Reglas de negocio:**
- `View…`/`Manage…` para la consulta corporativa (gate en handler — molde reporting controllers); la lectura self solo expone el propio expediente (RN-17).
- Filtros combinables; paginación con tope (precedente 1–100); los totales son **en horas decimales** (nunca dinero) (RN-14).

**Criterios de aceptación:**
- Query con estado=`AUTORIZADA` + origen=`PORTAL` + rango de fechas → página correcta con `StatusCounts` del filtro completo y total de horas; el detalle muestra duración h:m, decimal, factor y nota.

**Prioridad:** Alta
**Dependencias:** RF-005.

### RF-012 - Exportaciones (Excel de la consulta + insumo de planilla por periodo)

**Descripción:** (a) Export **xlsx**/csv/json de la consulta corporativa y de la bandeja de pendientes (mismos filtros vía query string, filas en español — el «exportar a Excel» del levantamiento); (b) **export de insumo de planilla**: para un tipo de planilla + periodo, las horas autorizadas a pagar — empleado, plaza, tipo snapshot, **factor**, **horas decimales**, fecha trabajada, justificación, solicitante, origen — el puente operativo con la nómina externa.

**Reglas de negocio:**
- `View…` para exportar; formato inválido → 400; tope síncrono → 413; auditoría `ReportExported`; rate limiting existentes (molde `ReportExportDeliveryService`).
- El insumo incluye solo `AUTORIZADA` pendientes del periodo (excluye anuladas/aplicadas y las **compensadas con tiempo** — RF-013); debe cuadrar contra la bandeja de pendientes del mismo filtro (RN-13).

**Criterios de aceptación:**
- `GET /export?format=xlsx` descarga el archivo con cabeceras en español; el insumo de un periodo cuadra contra la bandeja en tests de integración; 2 h 30 m viaja como 2.50.

**Prioridad:** Alta
**Dependencias:** RF-011 (y RF-010 para el insumo de pendientes).

### RF-013 - Costura con tiempo compensatorio (validación del vínculo y anti-doble-beneficio)

**Descripción:** Cuando REQ-002 esté mergeado al construir este módulo (o como PR de integración posterior): (a) la acreditación compensatoria que traiga `overtimeRecordPublicId` **valida** que el registro exista, pertenezca al mismo empleado y esté `AUTORIZADA` (hoy aquel campo viaja sin validación — su RN-19); (b) la solicitud de hora extra vinculada exhibe la insignia «**compensada con tiempo**» (con referencia a la acreditación); (c) una hora extra compensada queda **excluida del insumo de pago** y marcada en bandejas (anti-doble-beneficio blando F1).

**Reglas de negocio:**
- La validación vive en el módulo de acreditaciones (REQ-002) consultando este; la insignia/exclusión viven aquí; ninguna de las dos escribe en el otro módulo (lectura cruzada).
- Vincular una hora extra ya `APLICADA` (pagada) → 422 en la acreditación (doble beneficio); el sentido inverso (pagar una compensada) se bloquea vía exclusión del insumo + advertencia en aplicación unitaria.
- Sin FK dura entre módulos en F1 (publicId + validación de aplicación) — el plan técnico decide la mecánica.

**Criterios de aceptación:**
- Acreditación con `overtimeRecordPublicId` inexistente/ajeno/no-autorizado → 422; hora extra compensada → fuera del insumo, insignia visible; intento de aplicarla unitariamente → advertencia/422 según ratificación.

**Prioridad:** Media (condicionada a la convivencia con REQ-002 — **P-14**; recortable a F2 sin bloquear el resto)
**Dependencias:** RF-007; REQ-002 mergeado.

### RF-014 - Integración con liquidación: cálculo de las horas pendientes de pago (P-08)

**Descripción:** Al preparar la liquidación de una plaza (motor mergeado PR #56), las solicitudes `AUTORIZADA` de esa plaza no aplicadas, **no compensadas con tiempo** (RF-013) y con **fecha trabajada ya transcurrida** sugieren una **línea de ingreso calculada por el motor**: monto = **Σ(horas decimales × factor) × valor-hora del contexto salarial** (salario diario ÷ horas estándar por día), con el concepto nuevo `HORAS_EXTRAS_PENDIENTES_PAGO` — editable/excluible, con snapshot y traza de los registros incluidos. Al **emitir** la liquidación con la línea incluida, esas solicitudes pasan a `APLICADA` con origen `LIQUIDACION` + referencia; **anular la liquidación las reabre** a `AUTORIZADA` (simetría de la familia). Las pendientes con **fecha futura** (jornadas organizadas que ya no se trabajarán) no se sugieren: se **anulan** en el cierre (D-16).

**Reglas de negocio:**
- Concepto **propio** `-9915` (candidato): **NO** reutilizar `HORAS_EXTRAS_PENDIENTES=-9837` — reclamado por REQ-002 para el saldo compensatorio; ambas líneas pueden coexistir en un finiquito **sin doble conteo** (las jornadas compensadas están excluidas de esta línea — RN-16).
- El **valor-hora** sale del contexto salarial del motor: salario/30 ÷ horas-día estándar (preferencia de REQ-002 cuando exista; default 8 — el plan técnico fija la fuente); línea editable con la mecánica real del motor (`UnitsOrDays`/`UnitsOverridden`/`OverrideAmount`); implementación = **línea de sistema condicional** (espejo de la integración de REQ-002) con cierre/reapertura espejo de REQ-005/006.
- Guard anti-duplicado frente a una línea manual equivalente; nada escribe ledgers ni conceptos (RN-20/RN-21); contrato del motor 100 % retrocompatible.

**Criterios de aceptación:**
- Plaza con 2 pendientes (2.50 h × 2.00 y 1.50 h × 2.50), salario diario $10.00 y 8 h/día → línea sugerida (5.00 + 3.75) × $1.25 = **$10.94**, editable; al emitir → ambas `APLICADA` origen `LIQUIDACION`; anular la liquidación → ambas reabren a `AUTORIZADA`; una jornada compensada (RF-013) no entra al cálculo; una futura no trabajada se anula y no se paga.

**Prioridad:** Alta (énfasis explícito de la ratificación — P-08)
**Dependencias:** RF-007; módulo de liquidación (mergeado); coordinación de la preferencia horas-día con REQ-002 (D-21).

---

## 7. Requerimientos no funcionales

- **Seguridad**: dato laboral sensible: lecturas con `ViewOvertimeRecords`; la policy de `Authorize*` **omite Admin** (separación de funciones — molde verificado `Program.cs:564-569`); gates fail-closed por handler además de la política de controlador; la creación es authn-only en policy con gate fino en handler (canal portal — precedente `PersonnelFilePolicies.cs:47-53`); anti-autoaprobación **triple**; el autoservicio solo alcanza el propio expediente (`LinkedUserPublicId`); bandeja `POST /query` sin `AuthorizationPolicySet` con gate en handler (molde reporting); 403 sin enmascaramiento.
- **Auditoría**: `CreatedUtc`/`ModifiedUtc`, quién registró/decidió/aplicó/revirtió y cuándo, motivos obligatorios, snapshots (tipo con factor, justificación, solicitante, periodo real), **origen del canal** (`RRHH`/`PORTAL`), nota obligatoria al editar el factor, baja lógica universal, exportaciones auditadas (`ReportExported`).
- **Concurrencia/API**: convenciones del repo — `api/v1`, If-Match (faltante → 400, obsoleto → 409), ETag rotativo, `publicId`, enums string, errores bilingües `extensions.code`. La aplicación (unitaria y lote) corre bajo **lock anti-carrera** + re-verificación transaccional (test de doble submit obligatorio).
- **Rendimiento**: consulta y bandejas paginadas con índices por `(tenant, empresa, estado, fecha trabajada)` y por periodo destino; totales en horas agregados en base de datos; exports con tope síncrono y rate limiting existentes (pipeline async de jobs disponible si el volumen lo exige). El volumen esperado es el mayor de la familia (registros potencialmente diarios por empleado): dimensionar índices y paginación desde el diseño.
- **Disponibilidad/Escalabilidad**: multi-tenant por `TenantId`; sin jobs en F1 (la marca de atrasada es derivada en consulta).
- **Usabilidad**: errores accionables (minutos fuera de rango, factor editado sin nota, tipo/justificación inactivos, preferencia de portal deshabilitada con instrucción, tope diario excedido con el límite visible); duración siempre visible en h:m Y en decimal; bandeja de atrasadas como lista de trabajo; el detalle reconstruye la jornada completa.
- **Mantenibilidad**: reglas del ciclo de vida, duración/factor y elegibilidad en **módulo puro** (`OvertimeRecordRules`) con suite unitaria (casos dorados Anexo A.4) y **paridad de localización**; `openapi.yaml` mantenido a mano vía skill, sin drift; guía FE.
- **Compatibilidad**: cambios 100 % aditivos (entidad, maestros, catálogo de estados, permisos y preferencias nuevos; catálogos existentes solo se consumen; contrato de REQ-002 intacto — la validación RF-013 es interna).
- **Accesibilidad**: (frontend) formulario con duración en h:m y equivalente decimal visible, factor con su origen (tipo vs editado), consulta filtrable y exportable; se documenta en la guía FE.

---

## 8. Historias de usuario

### HU-001 - Registrar y autorizar una jornada extraordinaria
Como **gestor de RRHH**, quiero **registrar 2 horas 30 minutos de hora extra diurna trabajadas por un empleado el martes, con su solicitante y su planilla/periodo destino**, para **que un autorizador las apruebe y queden listas para pagarse con trazabilidad completa**.

**Criterios de aceptación:**
- Dado un tipo `HED` activo (factor 2.00), cuando registro la jornada con 2 h 30 m, entonces queda `EN_REVISION` con factor 2.00 snapshoteado, 2.50 horas decimales, solicitante y justificación trazados.
- Dado una solicitud `EN_REVISION`, cuando el autorizador (tercero) la autoriza, entonces queda `AUTORIZADA` y aparece en la bandeja de pendientes de su periodo.
- Dado que yo registré la solicitud (o soy el solicitante o el sujeto), cuando intento decidirla, entonces recibo 403.

### HU-002 - Solicitar mis horas desde el portal
Como **empleado**, quiero **registrar desde el portal las horas extras que trabajé ayer**, para **que mi solicitud entre al flujo de autorización sin depender de que RRHH la digite**.

**Criterios de aceptación:**
- Dado que mi empresa habilitó el canal portal y mi usuario está vinculado a mi expediente, cuando registro mis horas, entonces la solicitud nace `EN_REVISION` con origen `PORTAL` y yo como solicitante.
- Dado el canal deshabilitado, cuando intento registrar, entonces recibo un rechazo accionable.
- Dado que soy el solicitante, cuando consulto mi historial, entonces veo el estado de cada una de mis solicitudes.

### HU-003 - Decidir con separación de funciones
Como **autorizador**, quiero **aprobar o rechazar las solicitudes pendientes sin que nadie pueda autorizarse a sí mismo**, para **que ninguna hora extra se pague sin el visto bueno de un tercero facultado**.

**Criterios de aceptación:**
- Dado el permiso `AuthorizeOvertimeRecords`, cuando autorizo una solicitud `EN_REVISION`, entonces queda `AUTORIZADA` con mi decisión trazada.
- Dado que soy Admin sin el grant de autorizar, cuando intento decidir, entonces recibo 403.
- Dado un rechazo, cuando no escribo motivo, entonces recibo 422.

### HU-004 - Aplicar las horas del periodo
Como **analista de planilla**, quiero **aplicar en lote las horas autorizadas de la quincena, excluyendo las que se posponen**, para **que el módulo refleje lo que la planilla externa pagó y el insumo cuadre**.

**Criterios de aceptación:**
- Dado el filtro planilla `QUINCENAL` + periodo, cuando confirmo el lote con 2 exclusiones, entonces las incluidas quedan `APLICADA` (snapshot del periodo real, origen `MANUAL`) y las excluidas siguen pendientes.
- Dado un doble submit concurrente, entonces ninguna solicitud se aplica dos veces.

### HU-005 - Consultar y exportar las solicitudes del portal
Como **consulta de RRHH**, quiero **filtrar las solicitudes que ingresaron desde el portal por estado y rango de fechas, y exportarlas a Excel**, para **darles seguimiento sin armar reportes a mano**.

**Criterios de aceptación:**
- Dado el filtro origen=`PORTAL` + estado(s) + rango, cuando consulto, entonces obtengo la página con `StatusCounts`, totales en horas y el detalle de cada solicitud.
- Dado `format=xlsx`, entonces descargo el archivo con cabeceras en español.

### HU-006 - Entregar el insumo del periodo a la nómina
Como **analista de planilla**, quiero **exportar por periodo las horas autorizadas con su tipo, factor y horas decimales por empleado**, para **que la nómina externa calcule y pague sin transcripciones**.

**Criterios de aceptación:**
- Dado un periodo con horas autorizadas pendientes, cuando exporto el insumo, entonces obtengo empleado/plaza/tipo/factor/horas decimales/fecha/solicitante y el archivo cuadra contra la bandeja de pendientes.
- Dada una jornada de 2 h 30 m, entonces el insumo la lista como 2.50.

### HU-007 - Corregir una aplicación equivocada
Como **gestor de RRHH**, quiero **revertir con motivo la aplicación hecha al periodo equivocado y re-aplicarla al correcto**, para **que el historial quede exacto y auditable**.

**Criterios de aceptación:**
- Dada la reversión con motivo, entonces la solicitud vuelve a `AUTORIZADA`, la aplicación anulada sigue visible y puedo re-aplicar al periodo correcto o re-imputar el destino.

### HU-008 - Registrar una jornada organizada por el área (fecha futura)
Como **gestor de RRHH**, quiero **registrar las horas extras del sábado próximo que el área ya organizó**, para **que lleguen autorizadas al día del trabajo y se paguen en su periodo sin trámite de última hora**.

**Criterios de aceptación:**
- Dada una fecha futura, cuando registro la jornada organizada, entonces entra al flujo normal (`EN_REVISION` → autorización) imputada a su periodo.
- Dada una jornada futura `AUTORIZADA`, cuando intento aplicarla antes de la fecha, entonces recibo 422; transcurrida la fecha, se aplica normal.
- Dada una jornada futura que ya no se trabajará, cuando la revoco con motivo, entonces queda `ANULADA` y fuera de insumos.

### HU-009 - Cerrar las horas pendientes al liquidar (P-08)
Como **analista de liquidaciones**, quiero **que las horas extras autorizadas y no pagadas se calculen y sugieran como línea del finiquito**, para **cerrar los compromisos del empleado sin cálculos manuales**.

**Criterios de aceptación:**
- Dadas 8.75 horas-factor pendientes y un valor-hora de $1.25, cuando preparo la liquidación, entonces aparece la línea sugerida `HORAS_EXTRAS_PENDIENTES_PAGO` de $10.94, editable/excluible.
- Dada la liquidación emitida con la línea incluida, entonces las solicitudes quedan `APLICADA` origen `LIQUIDACION`; si la liquidación se anula, vuelven a `AUTORIZADA`.
- Dada una jornada compensada con tiempo (RF-013), entonces no entra al cálculo (sin doble beneficio).

### HU-010 - No pagar lo ya compensado (costura REQ-002)
Como **gestor de RRHH**, quiero **que una hora extra acreditada como tiempo compensatorio quede marcada y fuera del insumo de pago**, para **que la misma jornada no se pague y se compense a la vez**.

**Criterios de aceptación:**
- Dada una acreditación compensatoria vinculada a mi solicitud `AUTORIZADA`, entonces la solicitud exhibe la insignia «compensada con tiempo» y no aparece en el insumo del periodo.
- Dada una solicitud ya `APLICADA` (pagada), cuando la acreditación intenta vincularla, entonces el módulo de REQ-002 recibe 422.

---

## 9. Reglas de negocio (consolidadas)

| # | Regla |
|---|---|
| RN-01 | El **estado nunca se digita**: lo fija el flujo (`EN_REVISION` al crear; `AUTORIZADA`/`RECHAZADA` al decidir; `APLICADA` al aplicar; `ANULADA` por retiro del trámite o revocación) |
| RN-02 | Ciclo de vida: solo `EN_REVISION` es editable (campos de negocio); en `AUTORIZADA` solo se re-imputa el periodo destino; `RECHAZADA`/`ANULADA` son terminales; `APLICADA` solo admite reversión de la aplicación (→ `AUTORIZADA`) |
| RN-03 | **Anti-autoaprobación triple**: quien decide no puede ser el empleado sujeto (`LinkedUserPublicId`), quien registró (`RequestedByUserId`) ni el **solicitante** (vía su expediente) → 403 (D-06; molde `RetirementAuthorizerGuards`) |
| RN-04 | «Tipo de hora extra» y «justificación» = ítems **activos** de los maestros por empresa, con **snapshot** (código+nombre; el tipo además su **factor**) al registrar; cambios posteriores del maestro no recalculan históricos |
| RN-05 | **Factor** > 0: default el del tipo (snapshot); editable por registro con **nota obligatoria** cuando difiere del factor vigente del tipo (D-09) |
| RN-06 | **Duración**: horas ≥ 0, minutos 0–59, total > 0; la cifra operativa es la **derivada decimal a 2 decimales** (2 h 30 m = 2.50) — fuente de verdad = horas+minutos digitados; hora inicio/fin opcionales informativas |
| RN-07 | Fecha trabajada **pasada** (retroactividad/adopción) **o futura** (jornada **organizada previamente por cada área** — P-02 ratificada; sin tope normativo, límite de sanidad a criterio del plan técnico); el **periodo destino** puede ser el corriente o futuro y pasado solo como regularización |
| RN-08 | **Tope diario opcional** (`OvertimeMaxDailyMinutes`): si la empresa lo configura, la suma de duraciones del empleado en la fecha no puede excederlo → 422 con el límite; sin preferencia = sin tope (P-05) |
| RN-09 | Solo `AUTORIZADA` es aplicable y **solo con fecha trabajada ya transcurrida** (no se constata el pago de una jornada aún no trabajada — salvaguarda de P-02); cada solicitud se aplica **una sola vez** (la reversión la devuelve a aplicable); la aplicación corre bajo **lock anti-carrera** con re-verificación y registra snapshot del periodo real, fecha, quién y origen (`MANUAL`/`MOTOR`/`LIQUIDACION`) |
| RN-10 | Nada se borra físicamente: anulaciones, rechazos y reversiones con motivo, visibles y excluidos de totales/insumos |
| RN-11 | **Sin dinero en la operación regular**: el módulo no persiste montos, monedas ni centros de costo; no consulta el salario; el valor monetario pertenece a la planilla externa / REQ-006 / motor futuro (frontera ratificada en cadena). **Única excepción (P-08)**: la línea calculada al liquidar (RN-21), donde el **motor de liquidación** — no este módulo — posee el contexto salarial |
| RN-12 | Perfil `RETIRADO`: sin altas; pendientes `EN_REVISION` solo rechazables/anulables; las `AUTORIZADA` pendientes de pago se resuelven vía la **línea calculada del finiquito** (RN-21) — o manualmente si se excluyen de la sugerencia; las de **fecha futura** (no trabajadas) se **anulan** al cierre (D-16/D-21) |
| RN-13 | El **insumo por periodo** lista solo `AUTORIZADA` pendientes (excluye anuladas, aplicadas y compensadas-con-tiempo) y debe cuadrar contra la bandeja de pendientes del mismo filtro |
| RN-14 | Totales de consulta/exports **en horas decimales** (globales y por tipo) — nunca montos |
| RN-15 | El periodo usa el par `payrollTypeCode` (catálogo país REQ-004) + `PayrollPeriodId?`/etiqueta snapshot (maestro REQ-001 cuando exista); nunca texto sin snapshot; re-imputación solo en `AUTORIZADA` |
| RN-16 | **Anti-doble-beneficio** (costura D-17/RF-013): una hora extra **compensada con tiempo** (REQ-002) queda fuera del insumo de pago e identificada; una ya `APLICADA` (pagada) no puede vincularse a una acreditación compensatoria |
| RN-17 | **Canal portal**: habilitado solo por preferencia de empresa; el empleado solo registra/edita/retira/consulta **sus propias** solicitudes (`LinkedUserPublicId`); el origen (`RRHH`/`PORTAL`) se persiste y es filtro de la consulta |
| RN-18 | Varios registros del mismo día/empleado permitidos (p. ej. diurna + nocturna); sin control de solape horario en F1 (no hay rangos obligatorios); el tope RN-08 es el control cuantitativo |
| RN-19 | Los maestros/catálogos consumidos (tipos, justificaciones, tipos de planilla, periodos) deben estar **activos** al usarse → 422; los históricos conservan snapshot |
| RN-20 | **Sin asientos** de journal ni escrituras de ledgers/conceptos (D-19); la única superficie sobre liquidación es el canal de la línea sugerida (RN-21) — test de no-escritura para todo lo demás |
| RN-21 | **Gancho de liquidación** (P-08, D-21): pendientes de pago (no aplicadas, no compensadas, fecha transcurrida) de la plaza que se liquida → línea sugerida **calculada** = Σ(horas × factor) × valor-hora (salario/30 ÷ horas-día estándar), concepto propio `HORAS_EXTRAS_PENDIENTES_PAGO` (nunca `-9837`); editable/excluible con guard anti-duplicado; al emitir → `APLICADA` origen `LIQUIDACION`; anular la liquidación reabre (simetría) |

---

## 10. Flujos principales

### Flujo 1 — Registro por RRHH y autorización end-to-end
1. RRHH abre el expediente y elige «Nueva hora extra».
2. Ingresa fecha trabajada, tipo (el factor default se carga del maestro), duración h:m, solicitante, justificación, plaza (default principal), nómina y periodo destino.
3. El sistema valida maestros/catálogos, deriva las horas decimales y guarda `EN_REVISION` (201 + ETag) con origen `RRHH`.
4. El autorizador revisa y **autoriza** (o rechaza con motivo); el sistema verifica la anti-autoaprobación triple.
5. Al autorizar: la solicitud queda `AUTORIZADA` y aparece en la bandeja de pendientes y en el insumo de su periodo.

### Flujo 2 — Solicitud desde el portal
1. El empleado (usuario vinculado a su expediente, empresa con el canal habilitado) abre «Mis horas extras» y registra la jornada trabajada.
2. La solicitud nace `EN_REVISION` con origen `PORTAL` y él mismo como solicitante.
3. RRHH/el autorizador la ve en la consulta (filtro origen=`PORTAL`) y la decide; el empleado sigue el estado en su historial.

### Flujo 3 — Aplicación del periodo (lote) e insumo
1. El analista abre la bandeja de pendientes y filtra tipo de planilla + periodo.
2. El sistema lista las autorizadas del periodo (más las atrasadas de periodos vencidos).
3. El analista **exporta el insumo** (horas × factor por empleado/tipo) y lo entrega a la nómina externa.
4. Constatado el pago, confirma el lote **excluyendo** las que se posponen; cada solicitud queda `APLICADA` (snapshot, origen `MANUAL`) bajo lock.

### Flujo 4 — Enviar a otro periodo
1. Una autorizada no debe pagarse en su periodo destino.
2. El analista la re-imputa (cambia periodo/tipo de planilla con If-Match) o simplemente la excluye del lote.
3. La solicitud aparece en la bandeja e insumo del nuevo periodo; si su periodo venció sin aplicarse, se marca atrasada.

### Flujo 5 — Consulta y exportación a Excel
1. El usuario abre «Consulta de solicitudes de horas extras».
2. Filtra por estado, empleado, tipo, justificación, rango de fechas, origen (`PORTAL` para las del levantamiento), planilla/periodo.
3. El sistema devuelve la página con `StatusCounts`, totales en horas y el estado de cada solicitud; el detalle muestra la jornada completa.
4. Exporta a Excel (xlsx) el resultado del filtro.

### Flujo 6 — Corrección de una aplicación
1. RRHH detecta una solicitud aplicada al periodo equivocado (o que no se pagó realmente).
2. Revierte la aplicación con motivo → vuelve a `AUTORIZADA` (la aplicación anulada queda trazada).
3. Re-aplica al periodo correcto, re-imputa el destino, o revoca la solicitud si no procede.

### Flujo 7 — Compensación en lugar de pago (costura REQ-002, cuando conviva)
1. El empleado y la empresa acuerdan compensar la jornada con tiempo en vez de pagarla.
2. RRHH crea la **acreditación compensatoria** (módulo REQ-002) referenciando la hora extra autorizada (`overtimeRecordPublicId`).
3. Este módulo valida el vínculo (existe, mismo empleado, `AUTORIZADA`), marca la solicitud «compensada con tiempo» y la excluye del insumo de pago.

### Flujo 8 — Adopción (históricos)
1. RRHH registra retroactivamente las jornadas extraordinarias previas al sistema (fecha pasada, periodo real) y las pasa por autorización (P-13).
2. Las aplica con su periodo histórico para que la consulta y los totales reflejen la realidad.

### Flujo 9 — Jornada organizada por el área (fecha futura — P-02)
1. El área organiza horas extras para una fecha próxima (p. ej. inventario del sábado) y las solicita.
2. RRHH (o el empleado por el portal) registra la jornada con la **fecha futura**; sigue el flujo normal de autorización e imputación a su periodo.
3. La solicitud `AUTORIZADA` no puede **aplicarse** hasta que la fecha transcurra (RN-09).
4. Trabajada la jornada, entra al lote del periodo; si no se trabajó, se revoca con motivo (o se anula al liquidar — D-16).

### Flujo 10 — Liquidación con pendientes de pago (P-08)
1. El analista prepara la liquidación de la plaza (módulo existente, PR #56).
2. El motor calcula la línea `HORAS_EXTRAS_PENDIENTES_PAGO` con las autorizadas no aplicadas ni compensadas de fecha transcurrida — Σ(horas × factor) × valor-hora — editable/excluible; las futuras no trabajadas se anulan.
3. Al **emitir**, las incluidas quedan `APLICADA` origen `LIQUIDACION` con referencia; si la liquidación se **anula**, se reabren a `AUTORIZADA`.

---

## 11. Flujos alternativos y excepciones

- **Decisión por el sujeto, el registrador o el solicitante** → 403 `SELF_APPROVAL_FORBIDDEN` (RN-03).
- **Admin sin `AuthorizeOvertimeRecords`** intenta decidir → 403 (la policy omite Admin).
- **Minutos fuera de rango (≥ 60), duración total 0 o negativa** → 422 (RN-06).
- **Factor editado distinto al del tipo sin nota** → 422; **factor ≤ 0** → 422 (RN-05).
- **Tope diario excedido** (preferencia configurada) → 422 con el límite y las horas ya registradas del día (RN-08).
- **Tipo de hora extra o justificación inexistente/inactiva** → 422 (RN-04/RN-19).
- **Aplicar una jornada con fecha trabajada futura** → 422 (RN-09 — primero debe transcurrir; el registro y la autorización anticipados sí son válidos, P-02).
- **Canal portal deshabilitado** (preferencia off) → 403/422 accionable; **registro portal sobre expediente ajeno** → 403 (RN-17).
- **Tipo de planilla/periodo inválido o inactivo** → 422; **maestro de periodos aún no construido** → modo degradado a etiqueta (D-13).
- **Aplicar una solicitud no `AUTORIZADA`** → 422; **doble submit concurrente** del lote → la segunda 409/422 (lock + re-verificación, RN-09).
- **Re-imputar o editar una `APLICADA`** → 422 (primero revertir la aplicación).
- **Rechazo/anulación/revocación/reversión sin motivo** → 422 (RN-10).
- **Editar/decidir un registro no `EN_REVISION`** → 422/409; **If-Match ausente** → 400; **obsoleto** → 409.
- **Perfil `RETIRADO`**: alta → 422; decidir → solo rechazo/anulación (D-16).
- **Acreditación compensatoria con `overtimeRecordPublicId` inválido** (inexistente/ajeno/no-autorizada/ya pagada) → 422 en REQ-002 (RF-013, RN-16).
- **Liquidación**: jornada compensada no entra a la línea calculada; línea manual equivalente ya digitada → guard anti-duplicado (RN-21); anular la liquidación reabre las cerradas por ella.
- **Usuario sin permiso** → 403 (gestión, decisión, consulta, exports); **insumo sin tipo de planilla + periodo** → 400 (filtro obligatorio).

---

## 12. Datos requeridos

> Convenciones del repo aplican: `long Id` interno + `Guid publicId` externo, `TenantId`, `CreatedUtc`/`ModifiedUtc`, `isActive` (baja lógica), `concurrencyToken` rotativo (If-Match), factoría `Create(...)` + mutadores custodiados. Se listan solo los campos de negocio.

### Entidad: Solicitud de horas extras (`PersonnelFileOvertimeRecord` — sub-registro del expediente)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| workDate | Fecha | Sí | Pasada **o futura** (RN-07 — P-02) | «Fecha» del levantamiento: día trabajado, o jornada futura organizada por el área |
| overtimeTypePublicId / overtimeTypeCodeSnapshot / overtimeTypeNameSnapshot | Guid / Código / Texto | Sí | Tipo **activo** del maestro de la empresa (RN-04) | «Tipo de hora extra (catálogo)» + snapshot |
| typeFactorSnapshot | Decimal (5,2) | Sí | Factor vigente del tipo al registrar | Referencia del factor del catálogo (auditoría del default) |
| factorApplied | Decimal (5,2) | Sí | > 0; nota obligatoria si ≠ `typeFactorSnapshot` (RN-05) | «Factor» del levantamiento — el que viaja al insumo |
| factorOverrideNote | Texto (300) | Condicional | Obligatoria cuando el factor difiere del tipo | Justificación del ajuste del factor |
| durationHours / durationMinutes | Entero / Entero | Sí | ≥ 0 / 0–59; total > 0 (RN-06) | «Duración en horas y minutos» |
| durationDecimalHours | Decimal (6,2) | Derivado | = horas + minutos/60, redondeado a 2 decimales | Cifra operativa de totales e insumo (2 h 30 m = 2.50) |
| startTime / endTime | Hora / Hora | No | Informativas (espejo D-06 de REQ-002) | Rango horario opcional de la jornada |
| justificationTypePublicId / justificationCodeSnapshot / justificationNameSnapshot | Guid / Código / Texto | Sí | Tipo **activo** del maestro (RN-04) | «Motivo» del levantamiento (razón del catálogo) |
| observations | Texto (1000) | No | — | Detalle libre complementario del motivo |
| requesterFilePublicId / requesterNameSnapshot | Guid / Texto | Sí | Expediente activo de la empresa (D-11) | «Empleado que solicita» (trío con auditoría de registro) |
| requestedByUserId | Guid | Sí | Usuario autenticado | Auditoría: quién digitó (anti-autoaprobación) |
| originChannel | Enum string | Sí (flujo) | `RRHH` \| `PORTAL` (RN-17) | Canal de entrada — filtro de la consulta del levantamiento |
| assignedPositionPublicId | Guid | Sí | Plaza del empleado (default: principal) (D-12) | Plaza donde se trabajó la jornada (contexto para la nómina) |
| payrollTypeCode | Código catálogo | Sí | `PAYROLL_TYPE_CATALOG` activo (RF-004); default el de la plaza | «Nómina» en que se pagará |
| payrollPeriodId / payrollPeriodLabel | FK? / Texto | No / Sí | Maestro REQ-001 cuando exista (RN-15); re-imputable en `AUTORIZADA` | «Periodo de nómina» destino + etiqueta snapshot |
| statusCode | Código catálogo | Sí (flujo) | `EN_REVISION`/`AUTORIZADA`/`RECHAZADA`/`APLICADA`/`ANULADA` (híbrido; RN-01) | Estado — lo fija el flujo |
| decidedByUserId / decidedUtc / decisionNote | Guid / Fecha / Texto (500) | Al decidir | Anti-autoaprobación triple (RN-03); motivo al rechazar | Auditoría de la decisión |
| annulledByUserId / annulledUtc / annulmentReason | Guid / Fecha / Texto (500) | Al anular/revocar | Motivo obligatorio (RN-10) | Auditoría de anulación/revocación |
| *(aplicación vigente)* appliedDate / appliedPayrollTypeCode / appliedPayrollPeriodId / appliedPayrollPeriodLabel / appliedByUserId / applicationOrigin / applicationNote / settlementPublicId? | Fecha / Códigos / FK? / Texto / Guid / Enum / Texto / Guid? | Al aplicar | Snapshot del periodo **real**; origen `MANUAL`/`MOTOR`/`LIQUIDACION` (RN-09); fecha trabajada ya transcurrida | Constancia del procesamiento en planilla (referencia a la liquidación si vino de RF-014) |
| *(histórico)* aplicaciones revertidas | — | — | Visibles con motivo/quién/cuándo (RN-10) | El plan técnico decide representación (sub-registros — recomendado, espejo REQ-005/006 — vs campos + bitácora) |
| *(costura)* compensatedByCreditPublicId? | Guid | No | Poblado por RF-013 al validar el vínculo desde REQ-002 | Insignia «compensada con tiempo» + exclusión del insumo (RN-16) |

### Entidad: Tipo de hora extra (`OvertimeType` — maestro por empresa, D-03)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Código / Texto (120) | Sí | Único normalizado por empresa | Identidad del tipo (p. ej. `HED` / «Hora extra diurna») |
| defaultFactor | Decimal (5,2) | Sí | > 0 | Factor de recargo default (referencia CT en plantilla — A.2) |
| payrollEffectDescription | Texto (500) | No | — | «Condiciones… efecto en el pago de planilla» del levantamiento (texto informativo para el analista) |
| sortOrder / isActive | Entero / Booleano | Sí | — | Orden en UI y baja lógica |

### Entidad: Tipo de justificación (`OvertimeJustificationType` — maestro por empresa, D-04)

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| code / name | Código / Texto (120) | Sí | Único normalizado por empresa | Identidad de la razón (p. ej. `PRODUCCION_URGENTE`) |
| description | Texto (500) | No | — | Detalle de la razón |
| sortOrder / isActive | Entero / Booleano | Sí | — | Orden en UI y baja lógica |

### Catálogos nuevos y preferencias (semilla `HasData` / columnas — Anexo A.3)

- `OVERTIME_RECORD_STATUS_CATALOG` (wire `overtime-record-statuses`): `EN_REVISION`, `AUTORIZADA`, `RECHAZADA`, `APLICADA`, `ANULADA` → **`-9910…-9914`**.
- Concepto de liquidación `HORAS_EXTRAS_PENDIENTES_PAGO` → **`-9915`** (candidato — D-21/RF-014; **no** reutilizar `HORAS_EXTRAS_PENDIENTES=-9837`, reclamado por REQ-002). Holgura del bloque: `-9916…-9919`.
- Maestros `overtime-types` y `overtime-justification-types`: **por empresa, sin seeds globales** (plantilla vía seeder/`load-template` — A.2).
- `PAYROLL_TYPE_CATALOG` (definición REQ-004, re-ubicada): `-9890…-9895` — la siembra el REQ que construya primero.
- Preferencias de empresa: `OvertimeSelfServiceEnabled` (bool, default false — D-10) y `OvertimeMaxDailyMinutes` (int?, null = sin tope — RN-08).

### Catálogos/entidades existentes reutilizados (solo lectura)

Expediente y plazas (`IsPrimary`, contexto del insumo) · `payroll-types` (definición REQ-004) · `payroll-period-definitions` (maestro REQ-001, cuando exista) · trío solicitante (patrón) · infraestructura de exports (OpenXML).

---

## 13. Integraciones necesarias

| Integración | Tipo | Detalle |
|---|---|---|
| **Planilla externa** | Saliente (archivos) | Export de **insumo por periodo**: horas autorizadas a pagar (empleado, plaza, tipo snapshot, **factor**, **horas decimales**, fecha, justificación, solicitante, origen). El valor monetario lo calcula la nómina externa. No se escribe `PersonnelFilePayrollTransaction` (RN-20) |
| **Maestros propios** | Interna (lectura/escritura) | Tipos de hora extra y justificaciones por empresa con plantilla (`load-template`) — patrón REQ-001 |
| **Catálogo de tipos de planilla (REQ-004)** | Interna (definición compartida) | Una sola definición (`-9890…-9895`); la siembra el REQ que construya primero (coordinación en backlog) |
| **Maestro de periodos de planilla (REQ-001)** | Interna (futura, lectura) | `PayrollPeriodId?` del destino y de la aplicación; **degradación a etiqueta** si este módulo se adelanta (D-13) |
| **Módulo de tiempo compensatorio (REQ-002)** | Interna (bidireccional acotada — RF-013) | La acreditación referencia `overtimeRecordPublicId` (su D-21); al converger: validación del vínculo + insignia «compensada» + exclusión del insumo (anti-doble-beneficio). Sin FK dura en F1 |
| **Módulo de ingresos eventuales (REQ-006)** | Ninguna en F1 (frontera editorial) | El **pago** en CLARIHR se registra allá (concepto `HORAS_EXTRA=-9721`); generación automática / referencia cruzada del pago = F2 (A.5) |
| **Motor de liquidación** | Interna (bidireccional acotada — RF-014, P-08) | Pendientes de pago → **línea calculada** `HORAS_EXTRAS_PENDIENTES_PAGO=-9915` (Σ horas × factor × valor-hora del contexto salarial; editable/excluible; guard anti-duplicado); al emitir → `APLICADA` origen `LIQUIDACION`; anulación simétrica. El saldo **compensatorio** sigue en REQ-002 (`-9837`) — coexisten sin doble conteo (RN-16) |
| **Asuetos y descanso semanal (REQ-001)** | Fase 2 | Sugerir tipo festivo si la fecha cae en asueto; señalar trabajo en `restDayOfWeek` (Art. 175 CT — día compensatorio, costura REQ-002) |
| **Futuro motor de planilla (F2)** | Interna (futura) | Calculará el valor monetario y aplicará con origen `MOTOR` sobre el mismo modelo — contrato estable (D-14) |
| **Correo / notificaciones** | Fase 2 | Aviso al autorizador (pendientes) y al solicitante/empleado (decisión) |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de empresa | Maestros (tipos/justificaciones), preferencias, asignación de permisos | **No decide** solicitudes salvo grant explícito `AuthorizeOvertimeRecords` (la policy omite el fallback Admin) |
| Gestor de RRHH | `ManageOvertimeRecords` (+ lecturas implícitas) | No puede decidir lo que él mismo registró ni lo que solicitó (RN-03); ediciones solo `EN_REVISION`; re-imputación solo `AUTORIZADA`; aplica/revierte con auditoría |
| Autorizador | `AuthorizeOvertimeRecords` | Anti-autoaprobación triple; motivos obligatorios; revocaciones trazables |
| Consulta / Auditor | `ViewOvertimeRecords` | Solo lectura: consulta corporativa, detalle, exports |
| Empleado (portal) | Sin permiso RBAC — canal por preferencia + `LinkedUserPublicId` | Solo **su propio** expediente: registrar/editar/retirar `EN_REVISION` y consultar su historial; no decide nunca |
| Analista de planilla / Finanzas | `ViewOvertimeRecords` (+ `Manage…` si aplica lotes) | Insumo y aplicación por periodo con auditoría |

---

## 15. Criterios de aceptación generales

1. **Ratificación previa**: ✅ cumplida (2026-07-06) — D-01…D-21 ratificadas y P-01…P-14 respondidas (§17); el plan técnico se deriva de este documento ratificado (la confirmación de los factores con el contador queda como paso del checklist de despliegue — P-03).
2. Reglas del ciclo de vida, duración/factor y anti-autoaprobación como **módulo puro** con suite unitaria (casos dorados Anexo A.4) y test de paridad de localización.
3. Suite de integración completa (CRUD + flujo con anti-autoaprobación **triple**, canal portal con gate de preferencia e `isSelf`, derivación h:m→decimal, tope diario, re-imputación, aplicación unitaria/lote con **test de carrera**, exclusión/posposición, reversión, gates de permisos incluida la exclusión de Admin en `Authorize*`, consulta con `StatusCounts` y totales en horas cuadrando contra la búsqueda plana, insumo cuadrado contra pendientes, costura RF-013 si aplica) **en verde junto con la suite existente**.
4. Migraciones `HasData` idempotentes con IDs verificados contra `GlobalCatalogSeedData` (bloque `-9910…-9919`; sin tocar reservas REQ-001…REQ-006; coordinación `payroll-types -9890…-9895` en backlog).
5. `openapi.yaml` actualizado **sin drift** (mantenido a mano vía skill); convenciones API respetadas (If-Match, `publicId`, enums string, errores bilingües).
6. La duración se persiste como horas+minutos y toda cifra derivada (decimal, totales, insumo) se reconstruye exactamente (2 h 30 m = 2.50 en todos los caminos).
7. Consulta corporativa paginada con `StatusCounts`, totales en horas y filtro de **origen**; exportaciones xlsx/csv/json; **insumo de planilla por periodo** cuadrando contra la bandeja de pendientes del mismo filtro.
8. Maestros gobernados por empresa con plantilla cargable (`load-template` o seeder propio) y snapshots verificados en históricos.
9. **Sin escrituras prohibidas** (test de no-escritura): ledgers, conceptos de compensación, journal de acciones y liquidación intactos.
10. Guía de integración frontend publicada (`guia-integracion-frontend-horas-extras.md`) con contratos, estados, canal portal (preferencia + gates), duración h:m/decimal, factor con nota, jornadas futuras (P-02), flujo de aplicación por periodo, insumo y modo degradado (sin maestro de periodos).
11. **Integración con liquidación retrocompatible** (RF-014): contrato del motor intacto; línea calculada editable con guard anti-duplicado; cierre al emitir / reapertura al anular verificados; **coexistencia sin doble conteo** con la línea compensatoria `-9837` de REQ-002 (jornadas compensadas excluidas del cálculo).

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **Expectativa de cálculo monetario automático**: usuarios que esperen ver «cuánto se pagará». Mitigación: comunicar la frontera (el módulo entrega horas × factor; el dinero lo pone la nómina externa/REQ-006/motor F2) en UI, guía FE y capacitación; el insumo por periodo es el entregable monetizable.
- **Doble beneficio / doble registro** (misma jornada pagada y compensada; jornada aquí + monto en REQ-006 sin referencia): mitigación con la costura RF-013 (insignia + exclusión del insumo), la tabla de fronteras A.5 en la guía FE y capacitación; validación dura = F2.
- **Factores legales mal configurados** (la plantilla es referencia, no asesoría legal): mitigación con **P-03** (confirmación del contador antes del despliegue — espejo del precedente de golden cases de liquidación) y factores editables por empresa.
- **Canal portal sin gobierno**: solicitudes masivas o mal justificadas si se habilita sin política interna. Mitigación: preferencia **default off**, tope diario opcional, justificación obligatoria del catálogo y el flujo de autorización como filtro; piloto recomendado.
- **Carga operativa de la aplicación manual**: si nadie aplica, la consulta y el insumo divergen de lo pagado. Mitigación: bandeja de **atrasadas** como lista de trabajo, lote de un clic, export por periodo; el motor (F2) elimina la carga (P-07 ratificada: la constancia por periodo quedó en F1).
- **Jornadas futuras que no se concretan** (P-02): horas organizadas y autorizadas que nunca se trabajan podrían quedar como pendientes fantasma. Mitigación: no son aplicables hasta que la fecha transcurra (RN-09), la bandeja las muestra, se revocan con motivo y al liquidar se anulan automáticamente (D-16/D-21); la conciliación fina es F2.
- **Volumen** (el mayor de la familia — registros potencialmente diarios por empleado): mitigación con índices por fecha/estado/periodo, paginación con tope y totales agregados en BD; sin asientos de journal (D-19) para no inundar el tablero REQ-004.
- **Coordinación de seeds compartidos**: `payroll-types` (`-9890…-9895`) lo puede sembrar REQ-001/004/005/006 o este módulo según el orden real. Mitigación: definición única documentada en los cinco, verificación de IDs al abrir cada PR (precedente de la colisión ya resuelta) y coordinación en backlog.

### Supuestos

- La nómina se procesa **externamente** durante toda la F1; el insumo exportado es el mecanismo de entrega (frontera P-01 de REQ-005, heredada en cadena).
- Tenant mono-país (SV); los factores de la plantilla son referencia del CT salvadoreño, editables por empresa.
- Las horas se registran ya trabajadas (retroactivas) **o previamente organizadas por cada área** (fecha futura — P-02 ratificada); la conciliación fina planificado-vs-trabajado es F2.
- «Empleado que solicita» = un expediente de la empresa (normalmente la jefatura; el propio empleado en el canal portal); no requiere cuenta de usuario.
- No existe (ni se requiere en F1) un modelo de equipos/jerarquía: el portal es del **propio** empleado (P-01).
- El registro de jornadas NO sustituye un control de asistencia: es declarativo y autorizado.
- REQ-001 y REQ-002 se construirán antes que este módulo si se respeta el orden del backlog — pero ninguno lo bloquea (degradaciones D-20).

### Dependencias

- **Ratificación del negocio**: ✅ completada (2026-07-06, §17).
- **REQ-004/REQ-005/REQ-006**: definición compartida del `PAYROLL_TYPE_CATALOG` (seeds re-ubicados `-9890…-9895`) — no bloquea: la siembra el primero que construya.
- **REQ-001**: maestro `PayrollPeriodDefinition` para la FK de periodo + patrón de plantillas/`load-template` + (F2) asuetos/`restDayOfWeek` — no bloquea: degradación a etiqueta + seeder propio documentados (D-20).
- **REQ-002**: costura `overtimeRecordPublicId` (RF-013) — no bloquea: la validación se activa solo si aquel está mergeado; si no, queda como PR de integración posterior.
- **Liquidación**: mergeada (PR #56) — el gancho RF-014 no espera nada; coordinar con REQ-002 la **preferencia de horas-día estándar** del valor-hora (si aquel no está construido, default 8 propio — lo fija el plan técnico).
- Internas: verificación de IDs de seed libres al abrir el primer PR (bloque `-9910…-9919`); convenciones de catálogos/permisos vigentes.

---

## 17. Preguntas abiertas para el cliente o stakeholders — **resueltas (2026-07-06)**

> **Ratificación del 2026-07-06**: el negocio **aceptó la recomendación tal cual está redactada** en P-01 y P-03…P-07 y P-09…P-14 (12 de 14). Dos respuestas **ajustan el alcance** y sus filas registran la respuesta literal y el efecto en el diseño: **P-02** (también jornadas futuras organizadas por cada área → D-08 ajustada) y **P-08** (la liquidación **calcula** las pendientes de pago → D-16 ajustada + **D-21 nueva** + RF-014).

| # | Pregunta | Contexto y recomendación |
|---|---|---|
| **P-01** | **(Estructural)** «Solicitudes que han ingresado desde el portal»: ¿quién registra las horas extras? (a) RRHH **y** el propio empleado desde el portal; (b) solo RRHH (el «portal» es la app corporativa); (c) las jefaturas por su equipo | **Recomendación: (a)** — el autoservicio del propio empleado tiene precedente triple construido (reclamos médicos, ayuda económica, constancias), se habilita por preferencia de empresa (default off) y deja el **origen trazado** que la consulta del levantamiento pide filtrar. **(c) es F2**: no existe modelo de equipos/jerarquía y construirlo es un levantamiento propio. Si se elige (b), RF-006 pasa a F2 y la consulta pierde el filtro de origen |
| **P-02** | ¿El registro es **posterior** a trabajar las horas (fecha ≤ hoy) o se requiere **pre-autorización** de jornadas futuras? | **Respuesta (ajusta la recomendación)**: «debe permitir crear horas extras **pasadas** y crear horas **futuras** que previamente fueron **organizadas por cada área**» → **D-08 ajustada**: fecha pasada (retroactiva) **o futura** (jornada organizada) desde F1, por ambos canales. **Salvaguardas derivadas**: la **aplicación** exige fecha ya transcurrida (RN-09), al retiro las futuras no trabajadas se **anulan** (D-16/D-21), y la **conciliación** planificado-vs-trabajado queda en F2 |
| **P-03** | **Factores de la plantilla**: ¿confirma el contador los valores de referencia — HED **2.00**, HEN **2.50**, HEDF **4.00**, HENF **5.00** (derivación CT en A.2)? | Espejo del precedente «golden cases del contador» de liquidación: los factores son **editables por empresa** y la plantilla es solo referencia, pero desplegarla con valores validados evita errores de pago desde el día uno. Confirmar también si se agrega el tipo «trabajo en día de descanso» (Art. 175: +50 % **+ día compensatorio** — toca la costura REQ-002) |
| P-04 | ¿El maestro de tipos nace **con plantilla** (los 4 del levantamiento) o **vacío** (como el maestro de REQ-002)? | **Recomendación: con plantilla** — aquí el levantamiento nombra los tipos («entre los tipos estarán…»), a diferencia de REQ-002 donde el negocio prefirió arrancar vacío. `load-template` opcional por empresa mantiene la libertad |
| P-05 | ¿**Tope de horas** por día (u otra ventana)? ¿Bloquea o solo advierte? | **Recomendación: preferencia opcional por empresa** (`OvertimeMaxDailyMinutes`, sin tope por default) que **bloquea** (422) al excederse — el CT trata la jornada extraordinaria como excepcional y algunos reglamentos internos fijan máximos; sin hardcodear límites legales |
| P-06 | ¿El **factor es editable por registro** (default del tipo + nota obligatoria al diferir — espejo REQ-002) o siempre el del tipo? | **Recomendación: editable con nota** — cubre pactos puntuales y correcciones sin tocar el maestro; el snapshot del factor del tipo preserva la auditoría de la desviación |
| **P-07** | ¿El ciclo incluye **`APLICADA`** (constancia de procesada en planilla, con bandeja de pendientes/atrasadas e insumo por periodo — espejo REQ-005/006) o termina en `AUTORIZADA`? | **Recomendación: con `APLICADA`** — es lo que responde «¿qué horas autorizadas aún no se han pagado?» y mantiene el patrón operativo que el negocio ya ratificó dos veces para dinero equivalente. Si se recorta: RF-009/RF-010 pasan a F2 y el insumo lista autorizadas sin marca |
| **P-08** | Al **retiro** del empleado con horas `AUTORIZADA` sin aplicar: ¿basta la resolución manual en el finiquito o se requiere gancho automático a la liquidación? | **Respuesta (acepta con énfasis)**: «la **liquidación debe calcular** las horas extras pendientes de pago» → **D-16 ajustada + D-21 nueva + RF-014 (prioridad Alta)**: línea **calculada por el motor** — Σ(horas × factor) × valor-hora del contexto salarial (salario/30 ÷ horas-día estándar) — con concepto propio `HORAS_EXTRAS_PENDIENTES_PAGO=-9915` (**no** se toca `-9837`, saldo compensatorio de REQ-002; coexisten sin doble conteo vía RN-16), editable/excluible; al emitir → `APLICADA` origen `LIQUIDACION`; anular la liquidación reabre. La resolución manual queda solo para lo excluido de la sugerencia |
| P-09 | ¿La **plantilla de justificaciones** (A.2: producción urgente, cierre contable/inventario, emergencia operativa, cobertura de turno, proyecto especial, requerimiento del cliente) cubre las razones del negocio? | Ajustar/ampliar en la ratificación; el maestro es editable por empresa en todo momento |
| P-10 | ¿**Adjuntos** de respaldo en F1 (p. ej. memo de la jefatura escaneado)? | **Recomendación: sin adjuntos** — la autorización aquí es EN el sistema (el flujo es el respaldo), a diferencia de la acreditación REQ-002 (declarativa ex-post, por eso su PDF obligatorio). Si el negocio exige documento, se replica el patrón (purpose + contenedor + preferencia) como evolución aditiva |
| P-11 | La **consulta corporativa**: ¿muestra todas las solicitudes con **filtro de origen** (recomendado) o exclusivamente las del portal (lectura literal del levantamiento)? | **Recomendación: todas + filtro** — la vista literal del levantamiento es el filtro origen=`PORTAL` guardado como default en el FE; restringir el endpoint a un solo origen recorta valor sin ahorrar esfuerzo |
| P-12 | ¿**Lectura self** del propio historial cuando el canal portal esté activo? | **Recomendación: sí** — el solicitante debe poder seguir el estado de su solicitud (es la mitad del valor del portal); es lectura del propio expediente con gate existente |
| P-13 | ¿**Históricos**: se registran retroactivamente por el flujo normal (fecha pasada + autorización + aplicación con su periodo real)? | **Recomendación: sí** (espejo P-05 de REQ-006); sin importador masivo en F1 |
| P-14 | La **costura con REQ-002** (validar `overtimeRecordPublicId`, insignia «compensada», exclusión del insumo — RF-013): ¿entra en F1 si aquel módulo ya está mergeado al construir este, o se difiere completa a F2? | **Recomendación: F1 condicionada** (prioridad Media, recortable) — es barata (lectura cruzada + un campo), cierra el anti-doble-beneficio que ambos análisis prometieron y evita re-abrir los dos módulos después |

---

## 18. Recomendaciones del Analista de Negocio

1. **Ratificar P-01 antes que todo**: es la única decisión que cambia la forma del módulo (canal portal sí/no y para quién). La recomendación (RRHH + autoservicio propio con preferencia off por default) usa solo precedentes construidos; el portal de jefaturas es un levantamiento aparte (modelo de equipos) que no debe colarse en F1.
2. **Confirmar los factores con el contador antes de desplegar la plantilla (P-03)** — mismo tratamiento que los golden cases de liquidación: los valores 2.00/2.50/4.00/5.00 son la práctica salvadoreña derivada del CT, pero el módulo no debe «opinar» de ley: plantilla editable + validación del contador.
3. **Resistir el scope creep monetario**: el valor de F1 es el **control de la jornada** (quién, cuánto, por qué, autorizado por quién) y el **insumo limpio por periodo** — no calcular pagos. Cada vez que aparezca «¿y cuánto se le pagará?», la respuesta es la frontera ratificada en cadena (motor aparte; pago en REQ-006).
4. **Construir después de REQ-001 y cerca de REQ-002 (D-20)**: REQ-001 aporta el maestro de periodos, el patrón de plantillas y (para F2) asuetos/día de descanso; REQ-002 permite activar la costura anti-doble-beneficio (RF-013) sin re-abrir módulos. Si el negocio necesita adelantarlo, las tres degradaciones están documentadas y son las mismas que la familia ya aceptó.
5. **El insumo por periodo es el corazón operativo** (como en REQ-005/006): sin motor, la disciplina depende de la bandeja de pendientes/atrasadas, el lote de un clic y el export que cuadra. Invertir ahí el esfuerzo de UX.
6. **Mantener `APLICADA` (P-07)**: sin la constancia de aplicación, el módulo no puede responder «qué autorizado no se ha pagado» — la pregunta que motiva la visión «transacciones no aplicadas» del programa de planilla. El costo es un clic por periodo (lote).
7. **Piloto del canal portal**: habilitar la preferencia primero en un área (la justificación del catálogo + el tope diario + la autorización filtran el ruido); medir volumen antes de abrirlo a toda la empresa.
8. **Documentar el triángulo jornada/pago/compensación (A.5)** en la guía FE y la capacitación: con REQ-002 y REQ-006 en el mapa, la pregunta «¿dónde registro esto?» tiene tres respuestas correctas según la intención — es el riesgo de adopción №1 del ecosistema de horas extras.
9. **MVP recortable si urge**: RF-001…RF-008 + RF-011/RF-012 entregan las tres opciones del levantamiento (registro con autorización + catálogos + consulta con Excel); la costura REQ-002 (RF-013) y la lectura self pueden ser segunda entrega. **No recortables tras la ratificación**: la aplicación por periodo (RF-009/010 — P-07 aceptada) y la **integración con liquidación (RF-014 — P-08 con énfasis explícito)**. El insumo por periodo (parte de RF-012) se mantiene en F1: es lo que elimina el Excel paralelo.
10. **F2 ya perfilado**: conciliación planificado-vs-trabajado de las jornadas futuras (el registro anticipado ya es F1 — P-02), portal de jefaturas (modelo de equipos), generación del pago (REQ-006/motor), cálculo normativo, sugerencias por asuetos/descanso (REQ-001), anti-doble-beneficio duro, notificaciones y `groupBy` dimensional de la consulta.

---

## Anexo A — Referencias y propuestas

### A.1 Posición en el ecosistema de horas extras (el triángulo jornada → pago / compensación)

| Pieza | Módulo | Qué registra | Estado |
|---|---|---|---|
| **Jornada extraordinaria** (fecha, tipo, factor, horas, solicitante, justificación, autorización) | **Este módulo (REQ-007)** | El **hecho** trabajado y su autorización — sin dinero | Este análisis |
| Pago del monto | REQ-006 — ingresos eventuales (concepto `HORAS_EXTRA=-9721`) o nómina externa vía insumo | El **dinero** pagado en una planilla/periodo | Plan escrito, sin construir |
| Compensación en tiempo | REQ-002 — acreditación al fondo compensatorio (costura `overtimeRecordPublicId`, su D-21) | Las **horas acreditadas** para descanso futuro | Plan escrito, sin construir |
| Saldo compensatorio al retiro | REQ-002 → liquidación (línea `HORAS_EXTRAS_PENDIENTES=-9837`) | El pago del saldo no gozado en el finiquito | Plan escrito (REQ-002 D-19) |
| **Jornadas pendientes de pago al retiro** | **Este módulo → liquidación** (línea calculada `HORAS_EXTRAS_PENDIENTES_PAGO=-9915`, D-21/RF-014) | El pago en el finiquito de las horas autorizadas no aplicadas ni compensadas (Σ horas × factor × valor-hora) | Este análisis (P-08 ratificada) |
| Línea manual del finiquito | Liquidación (mergeado PR #56) | Horas extras pactadas al cierre, digitadas por el analista | Construido |

**Regla editorial**: la jornada se registra UNA vez (aquí); su beneficio va por UNA vía (pago **o** compensación — RN-16); el insumo/las costuras conectan las piezas sin duplicar el hecho.

### A.2 Plantillas de los maestros (referencia de adopción — factores a confirmar con el contador, P-03)

**Tipos de hora extra** (los 4 del levantamiento; derivación transparente sobre el CT SV — *referencia práctica, no asesoría legal*):

| Código | Nombre | Factor ref. | Derivación (referencia CT) |
|---|---|---|---|
| `HED` | Hora extra diurna | **2.00** | Hora ordinaria + recargo 100 % (Art. 169) |
| `HEN` | Hora extra nocturna | **2.50** | Base nocturna (+25 % mínimo, Art. 168) × recargo 100 % → 1.25 × 2 |
| `HEDF` | Hora extra diurna festiva | **4.00** | Salario extraordinario de asueto (+100 %, Art. 192) × recargo 100 % → 2 × 2 |
| `HENF` | Hora extra nocturna festiva | **5.00** | Compuesto asueto × nocturna × extra → 2 × 1.25 × 2 |

> Nota: el trabajo en **día de descanso semanal** (Art. 175: +50 % **más día de descanso compensatorio**) no está entre los 4 tipos del levantamiento; la empresa puede crearlo como tipo adicional (p. ej. `HE_DESCANSO`) — su «día compensatorio» conecta con REQ-002 (P-03 lo consulta).

**Tipos de justificación** (sugerencia inicial — P-09):

| Código | Nombre |
|---|---|
| `PRODUCCION_URGENTE` | Producción / entrega urgente |
| `CIERRE_CONTABLE` | Cierre contable o de inventario |
| `EMERGENCIA_OPERATIVA` | Emergencia operativa / fuerza mayor |
| `COBERTURA_TURNO` | Cobertura de turno / ausencia de personal |
| `PROYECTO_ESPECIAL` | Proyecto especial |
| `REQUERIMIENTO_CLIENTE` | Requerimiento del cliente |

### A.3 Seeds tentativos (verificar IDs libres contra `GlobalCatalogSeedData` al abrir el primer PR)

- **Ocupación verificada (2026-07-06, HEAD `62b341b`)**: piso general en código **`-9846`** (conceptos de liquidación `-9830…-9846`, cierre en `GlobalCatalogSeedData.cs:980`); **banda `-9847…-9999` completamente libre en código** (grep de literales `-9###L` en seed canónico + `HasData` de migraciones); ActionTypes ocupados `-9470…-9484` (`GlobalCatalogSeedData.cs:736-750`); trampa vigente `-9490…-9496` (`ACTION_STATUS_CATALOG`); conceptos país `-9720…-9736` (`HORAS_EXTRA=-9721` en `:937`). **Trampa de verificación**: los aparentes `-9914`/`-9897`/`-9938` en `Migrations/*.Designer.cs` son **fragmentos de GUID**, no IDs de seed — filtrar por sufijo `L` al re-verificar.
- **Reservas de planes (no en código)**: REQ-001 `-9850…-9862` + `-9485…-9489` · REQ-002 `-9865…-9871` · REQ-003 `-9875…-9879` · REQ-005 `-9880…-9899` (incl. **payroll-types `-9890…-9895`**) · REQ-006 `-9900…-9909`. La verificación en doble pasada de REQ-006 (mismo HEAD) confirmó **nada en código ni en planes bajo `-9899`**; nadie reserva `-9910` o inferior.
- **Propuesta de este módulo (bloque `-9910…-9919`)**:
  - `OVERTIME_RECORD_STATUS_CATALOG`: `EN_REVISION=-9910`, `AUTORIZADA=-9911`, `RECHAZADA=-9912`, `APLICADA=-9913`, `ANULADA=-9914`.
  - Concepto de liquidación `HORAS_EXTRAS_PENDIENTES_PAGO=-9915` (candidato — D-21/RF-014; **no** reutilizar `-9837` de REQ-002).
  - Holgura: `-9916…-9919`.
- Maestros `overtime-types` / `overtime-justification-types`: **por empresa, sin seeds globales** (plantilla vía seeder/`load-template`).
- `PAYROLL_TYPE_CATALOG` (`-9890…-9895`) es la definición compartida de REQ-004 — la siembra el primero que construya (coordinar en backlog).
- **Sin ActionTypes nuevos** (D-19: sin asientos de journal). Próximo bloque libre global tras este módulo: **`-9920`**.

### A.4 Casos dorados sugeridos para la validación del negocio

1. **E2e diurna**: 2 h 30 m tipo `HED` (factor 2.00) → `EN_REVISION` con 2.50 decimales → autorizada por tercero → aplicada a la quincena → `APLICADA`; la consulta la muestra con estado y detalle.
2. **Portal**: empleado con canal habilitado registra sus horas (origen `PORTAL`, solicitante él mismo) → RRHH la ve con el filtro de origen; con preferencia off → rechazo accionable.
3. **Anti-autoaprobación triple**: el registrador, el empleado sujeto y el solicitante intentan decidir → 403 los tres; Admin sin grant → 403.
4. **Factor editado**: tipo con factor 2.00, registro con 2.25 y nota → snapshot de ambos; sin nota → 422.
5. **Duración inválida**: 1 h 65 m → 422; 0 h 0 m → 422; 0 h 45 m → 0.75 decimales OK.
6. **Tope diario**: preferencia 240 min; segundo registro del día que suma 250 → 422 con el límite.
7. **Dos jornadas el mismo día**: `HED` 2 h + `HEN` 1 h 30 m del mismo empleado/fecha → ambas válidas (RN-18).
8. **Enviar a otro periodo**: autorizada de la quincena 13 re-imputada a la 14 → sale del insumo de la 13 y entra al de la 14; excluirla del lote de la 14 la mantiene pendiente.
9. **Carrera**: doble submit concurrente del mismo lote → cero dobles aplicaciones; segunda respuesta 409/422.
10. **Reversión**: aplicada a la quincena equivocada → revertir con motivo → `AUTORIZADA` con la aplicación anulada visible → re-aplicar al periodo correcto.
11. **Insumo**: export del periodo cuadra exactamente contra la bandeja de pendientes; 2 h 30 m viaja como 2.50; excluye anuladas/aplicadas/compensadas.
12. **Retirado**: alta sobre `RETIRADO` → 422; pendiente `EN_REVISION` de un retirado → solo rechazo/anulación; autorizada sin aplicar → entra a la línea calculada del finiquito (caso 16).
13. **Costura REQ-002** (si convive): acreditación vinculada a una autorizada → insignia «compensada» + fuera del insumo; vincular una `APLICADA` → 422; acreditación con publicId ajeno → 422.
14. **Consulta cuadra**: `StatusCounts` y totales en horas del filtro coinciden con la suma de la búsqueda plana; export xlsx con cabeceras en español.
15. **Jornada organizada (futura — P-02)**: registro con fecha del próximo sábado → válido, autorizado e imputado a su periodo; intentar aplicarla antes de la fecha → 422; transcurrida la fecha, se aplica normal; si no se trabajó, se revoca.
16. **Liquidación (P-08)**: plaza con pendientes 2.50 h × 2.00 y 1.50 h × 2.50, salario diario $10.00 y 8 h/día → línea sugerida (5.00 + 3.75) × $1.25 = **$10.94** editable; al emitir → ambas `APLICADA` origen `LIQUIDACION`; anular la liquidación las reabre; una compensada (caso 13) no entra al cálculo; una futura no trabajada se anula.

### A.5 Fronteras del ecosistema de horas extras (guía editorial — RN-16)

| Registro | Qué es | Cuándo usarlo | Módulo |
|---|---|---|---|
| **Solicitud de hora extra (este módulo)** | La **jornada trabajada** (fecha, tipo, factor, h:m) con su autorización | Siempre que se trabaje tiempo extraordinario y deba controlarse/pagarse/compensarse | REQ-007 (este análisis) |
| Ingreso eventual `HORAS_EXTRA` | El **pago puntual** del monto en una planilla/periodo | Cuando la empresa registra en CLARIHR el dinero de horas extras (mientras no haya motor) | REQ-006 (plan escrito) |
| Acreditación compensatoria | Las **horas acreditadas** al fondo para descanso futuro (con factor propio) | Cuando la jornada se compensa con tiempo en lugar de pagarse (referencia `overtimeRecordPublicId`) | REQ-002 (plan escrito) |
| Línea **calculada** `HORAS_EXTRAS_PENDIENTES_PAGO` (`-9915`) | Sugerencia automática del **finiquito** con las jornadas pendientes de pago de este módulo | Nunca se digita: la calcula el motor al liquidar (editable/excluible — D-21) | Este módulo → liquidación (P-08) |
| Línea manual de liquidación «horas extras» | Componente digitado del **finiquito** | Solo al liquidar, para casos **fuera** de la sugerencia calculada (guard anti-duplicado — RN-21) | Liquidación (mergeado) |
| Concepto país `HORAS_EXTRA` (`-9721`) | **Tipo de ingreso** del catálogo de compensación | Nunca se registra ahí una jornada: es el clasificador que REQ-006 consume | Catálogos (mergeado) |
| Ledger `PersonnelFilePayrollTransaction` | Bitácora **inmutable** de sincronización con nómina externa | Solo integración; nunca registro operativo (trampa documentada) | Compensación (mergeado) |
