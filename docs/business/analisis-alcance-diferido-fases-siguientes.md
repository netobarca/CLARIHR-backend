# Análisis — TODO el alcance diferido a fases siguientes (F2/F3), por necesidad y con los flujos afectados

| | |
|---|---|
| **Estado** | ANÁLISIS — corte 2026-07-16 |
| **Qué es** | El inventario COMPLETO de lo que quedó **fuera de esta fase por decisión de negocio ratificada**, organizado **por necesidad transversal** (no por módulo): qué es cada necesidad, por qué se difirió, **qué flujos/módulos la esperan**, qué costura ya existe en el código, y qué implicaría construirla. Cierra con los diferidos específicos por módulo, lo **descartado** (que NO debe esperarse), lo diseñado **sin ratificar**, y una propuesta de agrupación en olas F2. |
| **Método** | Cruce de los 29 análisis de negocio + planes técnicos + backlog + verificación directa en código (las costuras citadas con `archivo:línea` fueron verificadas hoy). |
| **Documentos hermanos** | `docs/pendientes-proyecto-16072026.md` (pendientes de despliegue/negocio de ESTA fase) · `docs/business/analisis-notificaciones-correo-flujos.md` (profundización de la necesidad N1 — correos, flujo por flujo) |

---

## 0. Resumen ejecutivo

El backlog de esta fase quedó 15/15 completo; lo diferido NO son cabos sueltos sino **13 necesidades transversales** que el negocio decidió posponer de forma consistente en todos los módulos, más un conjunto de diferidos puntuales por módulo. Patrón dominante: **esta fase construyó el REGISTRO y la DECISIÓN de cada proceso dentro del sistema; quedó para después todo lo que es "en línea", automático o hacia afuera** — avisar (correo), delegar/escalar (multinivel), que el empleado se autogestione (portal), que el sistema actúe solo (scheduler), marcar asistencia (biometría), y hablar con terceros (banco/ERP/IAM).

Las 13 necesidades, en orden de cuántos flujos las esperan:

| # | Necesidad | Flujos que la esperan | Costura en código |
|---|---|---|---|
| N1 | Correo / notificaciones | ~85 endpoints + identidad | ✅ contratos + 6 correos cableados |
| N2 | Aprobación multinivel / delegación | 12+ módulos con decisión | ✅ parcial (anti-self, estados aditivos) |
| N3 | Autoservicio del empleado | 8 superficies | ✅ gates self-or-view + 1 piloto ya construido |
| N4 | Scheduler / tareas programadas | 6+ automatismos | ⚠️ solo Worker de exports |
| N5 | Módulo de Ausencias/Permisos | 3 módulos lo referencian | ✅ chasis + seeds reservados |
| N6 | Asistencia/marcación y jornada real | 4 flujos | ✅ maestro de jornadas ya construido |
| N7 | Integraciones externas (banco/ERP/IAM) | 4 frentes | ✅ conciliación exportable como puente |
| N8 | Documental avanzado (firma, plantillas, PDF gráficos) | 5 entregables | ✅ seam DocumentModel |
| N9 | Puesto↔nivel piramidal | 2 tableros | ❌ |
| N10 | Adjuntos faltantes | 5 módulos | ✅ subsistema de documentos completo |
| N11 | Admin de catálogos + auditoría visible | 4 frentes | ✅ auditoría backend existe |
| N12 | Multi-país / multi-moneda | plataforma | ✅ modelo por país ya parametrizado |
| N13 | Importadores masivos | adopción | ❌ (retroactivo manual construido) |

---

## 1. N1 — Correo y notificaciones (la más transversal)

**Análisis completo aparte**: `analisis-notificaciones-correo-flujos.md`. Síntesis: no existe proveedor de correo ni notificación in-app; hay 2 contratos con 6 correos ya cableados contra stubs de log (el caso crítico: **afiliación/credenciales al publicar expediente** — token de 72 h que muere en el log); ~85 endpoints de transición donde ni el empleado ni el autorizador reciben aviso. Prioridad propuesta P0 = identidad (cero cambios en flujos, solo proveedor + implementación de los 2 contratos).

## 2. N2 — Motor de aprobación multinivel, umbrales y delegación efectiva

**Qué se construyó en F1**: toda decisión es de **un solo paso** con permiso `Authorize*` dedicado (que Admin NO cubre) y anti-self (simple, doble o triple según el módulo); la devolución-con-motivo existe donde se ratificó (planilla). **Qué quedó fuera**: rutas de más de un aprobador, umbrales por monto, escalamiento, y la **delegación real de autoridad** durante ausencias.

Flujos que lo esperan (todas decisiones ratificadas):

| Módulo | Decisión que lo difirió |
|---|---|
| Ayuda económica | flujo multinivel + permiso `Approve` dedicado (D-03); la entidad hija de pasos se **reservó solo en el análisis — verificado: no existe en código** |
| Vacaciones/incapacidades | D-01: el propio levantamiento difirió el flujo en línea jefatura→RRHH; los permisos `AuthorizeVacations`/`AuthorizeIncapacities` quedaron diseñados para F2 |
| Tiempo compensatorio | `AuthorizeCompensatoryTime` referenciado, no implementado (F2); flujo de autorización en línea P-06 |
| Otras transacciones (REQ-003) | P-01 multinivel configurable; los planes dejan los **estados "aditivos F2"** previstos |
| Ingresos cíclicos/eventuales | P-12 rutas/umbrales |
| Liquidación | flujo revisor→aprobador + estado `PAGADA` (D-15); hoy emisión directa |
| Retiro definitivo | FA-2 un solo nivel |
| Transacciones fuera de nómina | D-09 workflow de aprobación del gasto — **`StatusCode` reservado en el modelo** esperándolo |
| Compensación de plazas | D-16 workflow de cambios salariales |
| Multiplaza | P-09 aprobaciones de asignación |
| Catálogos | flujos de aprobación sobre cambios de catálogo (revalidación §4) |
| **Delegación** | Sustitución de autorizaciones RF-010: F1 es **documental** (slot-ref + snapshot construidos); la delegación EFECTIVA depende además del vínculo con ausencia real (D-10 → N5) |

**Qué implicaría**: un motor de pasos/rutas reutilizable (el repo ya converge en el patrón resolution-controller + policy-set — el motor se inserta detrás de esos mismos endpoints), estados aditivos ya previstos en los planes, y N1 para que cada paso avise al siguiente actor. Sin N1, el multinivel sería operativamente ciego.

## 3. N3 — Autoservicio del empleado (portal)

**Lo que F1 YA le da al empleado** (verificado): crear sus **reclamos de seguro** (gate self-service-create), solicitar **constancias**, ver su **historial de planilla** (REQ-015 P-03, gate self-or-view, estados fijos), y — único flujo de SOLICITUD en línea construido — registrar sus **horas extras** si la empresa enciende `OvertimeSelfServiceEnabled` (`CompanyPreference.cs:125`, default off, piloto recomendado).

**Lo diferido**:

| Superficie | Decisión |
|---|---|
| Solicitud de vacaciones en línea | REQ-001 D-01 (F1: RRHH registra) |
| Solicitud/consulta de tiempo compensatorio | P-06 |
| Renuncia autoservicio | Retiro D-03 |
| Consultar su liquidación/finiquito | Liquidación D-20 |
| Boleta de planilla PDF | REQ-012/015 P-12 |
| Consulta self de endeudamiento | REQ-010 F2 (ratificado) |
| Registrar sus competencias/evidencias | variante futura (pregunta abierta menor) |
| Autoservicio de equipo/acceso | FA-6 — dentro del Nivel B sin ratificar |

**Qué implicaría**: el patrón de gate **self-or-view ya existe y está probado** (molde medical-claims D-13, reutilizado en payroll — `PersonnelFilePolicies.cs`/`PayrollRunsReporting.cs`); cada superficie nueva es ese gate + su endpoint de lectura/creación. Las de SOLICITUD requieren N2 (a quién le llega) y N1 (cómo se entera).

## 4. N4 — Scheduler / tareas programadas del negocio

**Hoy no existe** un programador de tareas de negocio: `CLARIHR.Worker` solo procesa colas de exportación. Automatismos diferidos que lo esperan:

- **Programación de la generación de planilla a fecha/hora** — REQ-012 P-14, la F2 más explícita; diseño reservado en el plan §7-№6 (cola de jobs molde `ReportExportJob`).
- **Suspensión automática de empleado con reversión programada** — REQ-003 P-05/D-09 (job que revierte el estado al vencer la sanción).
- **Cambio automático de `EmploymentStatusCode`** a INCAPACIDAD/LICENCIA y su reversión — REQ-001 P-18.
- **Alertas de SLA de constancias** (`ResponseTimeDays`) — D-14.
- **Vencimientos**: tokens de invitación (72 h), vigencia de sustituciones, incapacidades por vencer, recordatorios de bandeja.
- **Tableros**: programación/correo de reportes — REQ-004 F2.

**Qué implicaría**: un job scheduler (Hangfire/Quartz o colas + cron del host) sobre el Worker existente; cada automatismo es pequeño una vez que existe el chasis. Los recordatorios además necesitan N1.

## 5. N5 — Módulo de Ausencias/Permisos generales (la pieza faltante nombrada por 3 módulos)

No existe el módulo de **permisos/licencias del empleado** (distinto de vacaciones/incapacidades/TNT). Lo esperan, con costuras ya dejadas a propósito:

- REQ-003: "módulo de permisos/licencias generales" (§4) — y la consulta de **Disponibilidad de tiempo** se construyó como **chasis extensible**: fuente nueva = 1 método + 1 categoría, contrato intacto (promesa cumplida literalmente al integrar REQ-011).
- Sustitución de autorizaciones D-10: el **vínculo con la ausencia real** (hoy la sustitución es por fechas declaradas) — prerequisito de la delegación efectiva (N2).
- REQ-011: el maestro TNT lleva el flag **`AppliesToPermission` inerte** (`NotWorkedTimeType.cs:64`) y el seed **`PERMISO = -9479` está reservado con comentario explícito de no reutilizarlo** (`GlobalCatalogSeedData.cs:950,964`) — ambos esperando este módulo.
- El autoservicio de permisos conectado a TNT figura en el mapa F2+ de REQ-008…011.

**Qué implicaría**: es un módulo nuevo (maestro de tipos + solicitud + decisión + efecto en TNT/planilla vía el arrastre ya construido), pero **aterriza en costuras preparadas**: ActionType reservado, flag del maestro, chasis de disponibilidad, y el motor de planilla ya consume TNT por horas (golden 14).

## 6. N6 — Asistencia, marcación y jornada real

- **Marcación/biometría**: cero código; la costura documentada es el campo `origin` (MANUAL hoy, MARCACION futuro) en los insumos de tiempo.
- **Jornadas**: el maestro `WorkSchedule` con plantilla legal 44 h **ya quedó construido** (REQ-012 PR-3) — lo diferido es explotarlo: motor de jornadas real en vacaciones (`usaJornada` reservado, REQ-001 F2), **conciliación planificado-vs-trabajado** de HE (P-02), sugerencia de tipo festivo/día de descanso desde asuetos (REQ-007 F2).
- **Portal de jefaturas / modelo de equipos** (quién aprueba a quién por estructura): F2 de REQ-007; también alimenta N2.

## 7. N7 — Integraciones externas

| Frente | Estado / decisión |
|---|---|
| **Banco (dispersión)** | F1 entrega la **conciliación bancaria exportable** (construida en REQ-012 PR-7); el **archivo propietario por banco** para dispersión automática = F2 |
| **ERP contable** | contabilización de la planilla = F2 (mapa de fases de REQ-012) |
| **IAM/AD/SSO — provisión real de accesos** | equipo-acceso G-09/RF-110: F1 es registro documental; la ejecución real de altas/bajas de accesos pertenece al Nivel B (sin ratificar — §11) |
| **AFP / bancos / registros académicos** | revalidación §4: integraciones asumidas fuera |
| **Aseguradoras / sistema externo de evaluación / ledger de nómina externa** | **DESCARTADOS** (D-13, FA-1, P-16) — ver §10, no esperarlos |

## 8. N8 — Documental avanzado (firma, plantillas, gráficos)

- **Firma electrónica + folio/QR verificable** en constancias — FA-02/R-04, candidato F2 (hoy PDF informativo con firma manual).
- **Plantillas/firmantes por unidad o por tipo** (constancias FA-09/S-05) y **plantilla institucional firmable** de la boleta de liquidación (F2).
- **Desglose salarial** en la constancia — P-04.
- **Impresión/PDF de los GRÁFICOS de los tableros** — REQ-004 P-01/D-12: diferido porque no hay librería de charting server-side y QuestPDF solo acepta raster (`.Image()`); el plan F2 contempla "PDF servidor con bloque raster".
- Exports menores diferidos: export del historial de planilla por empleado (aditivo), export por indicador del tablero RRHH (D-11), export xlsx de fuera-de-nómina.
- **Costura lista**: el seam `DocumentModel` (motor PDF intercambiable) ya sirve boletas de liquidación, constancias y boletas de planilla.

## 9. N9…N13 — el resto de necesidades transversales

- **N9 Puesto↔nivel piramidal**: no existe la relación canónica puesto↔nivel; bloquea el filtro por nivel en el tablero RRHH (D-07/RF-013) y en el de acciones (D-11/G-06). Requiere decisión de modelo en el dominio de puestos.
- **N10 Adjuntos faltantes**: retiro (carta renuncia/despido — D-19, F2 confirmada), entrevista de retiro (D-09, sin FilePurpose), HE (P-10), ingresos cíclicos/eventuales (P-07), competencias (evidencias/diplomas + verificación). El subsistema de documentos está completo — cada uno cuesta un purpose + contenedor + endpoint (patrón repetido 5 veces ya).
- **N11 Admin de catálogos + auditoría visible**: CRUD administrativo para catálogos seed-only (retiro RF-015 — "requiere infraestructura de escritura de catálogos que hoy no existe" —, rangos de tableros, catálogos enriquecidos de revalidación con su roadmap F2 educación estructurada / F3 formato-regex / F4 maestro AFP ampliado); y la **vista de historial/diff para el FE** (la auditoría backend EXISTE y registra todo; falta la superficie de consulta — anotado en la guía FE de seguros como "siguiente paso").
- **N12 Multi-país / multi-moneda**: siembra y reglas solo SV (los catálogos ya están parametrizados por país — la costura existe); mono-moneda USD por diseño (FA transversal). Activar otro país = decisión de negocio + siembra + reglas legales propias (revalidación D-13/BT-03).
- **N13 Importadores masivos**: descartados en F1 en todos los módulos; la adopción se hace por registro retroactivo normal (construido). Si el onboarding real lo exige, es un frente nuevo.

---

## 10. Diferidos ESPECÍFICOS por módulo (no caen en una necesidad transversal)

| Módulo | Ítem diferido | Ref |
|---|---|---|
| Planilla (REQ-012/014) | Retroactividad sobre periodos CERRADOS (correcciones van al periodo siguiente — política, no gap) · líneas manuales ad-hoc en la corrida · «apagado por categoría» y bandeja unificada de rezagos (ambos condicionados a que el uso real lo pida) | §4 análisis / P-02, P-04 REQ-014 |
| Ayuda económica | Ayudas **reembolsables** (anticipo/préstamo con plan de pagos — FA-03; hoy `IsRefundable` parametrizado y el plan de cuotas de REQ-008 podría absorberlo) · taxabilidad (D-15) · elegibilidad compleja (FA-05) · catálogo de motivos de rechazo (D-12) | analisis-ayuda-economica |
| Liquidación | Conversión **escenario→liquidación real** (D-05) · estado `PAGADA` (D-15 → N2) · señal automática de cierre para recontratación (`EMITIDA` reemplaza la confirmación manual — D-18) · beneficiario/heredero en fallecimiento · catálogo de salario mínimo por sector/vigencia | analisis-liquidacion |
| Endeudamiento (F2 ratificada) | Ingresos adicionales en la base de cálculo · descuentos eventuales dentro de la validación · re-validación retroactiva (P-13) | plan-tecnico-endeudamiento:77-79 |
| Entrevista de retiro | F2: **tabulación/analítica de rotación** (RF-014) + corrección/anulación de respuestas (RF-020) · F3: lógica condicional (skip-logic) | plan-tecnico-entrevista-retiro:237-242 |
| Recontratación | Pre-carga editable enriquecida (RF-005) · integración con acumulados/vacaciones al recontratar (RF-007 — decía "cuando exista nómina": **ya existe**, candidato barato) | frontend-integration:277-278 |
| Multiplaza | % dedicación/FTE (P-07, fuera por decisión) · consolidación F2 (retirar campos legacy de la raíz del perfil — RF-006/009/010) · organigrama de posiciones · reportes multi-plaza | analisis-multiples-plazas:100-132 |
| Compensación de plazas | Amortización de préstamos EXTERNOS con saldo/cuotas (**hoy en gran parte absorbida por REQ-008** — segmentos + interés; validar el resto) · aguinaldo como cálculo dedicado (la 13.ª corrida ya es legítima vía `TotalPeriods`) | analisis-plazas-ingresos-egresos §4 |
| Vacaciones | Plan de vacaciones por **jefaturas** (F1: solo RRHH) · comprobante PDF de solicitud | analisis-vacaciones:558,54 |
| Reclamos de seguro | Consulta enriquecida con agregados (RF-010, F3) | plan-tecnico-reclamos:490 |
| Seguros | Catálogos configurables por tenant (D-02: hoy alcance país) | analisis-seguros §4 |
| Competencias | **Análisis de brecha** persona↔perfil de puesto (D-07, requiere `JobProfileRequirement` del roadmap) | analisis-competencias-curriculares:57-72 |
| Tableros | Snapshot dimensional del asiento · comparativas multi-periodo · montos bajo `ViewCompensation` (D-15) · **indicadores cuyas fuentes YA se construyeron esta fase** (ausentismo vía TNT, horas extra, costos de planilla) — hoy son activaciones baratas, igual que bajas/rotación se activó con REQ-004 | analisis-tablero-acciones:169-176 |
| Tiempo compensatorio | (Todo lo suyo cayó en N1/N2/N3; la caducidad FIFO fue DESCARTADA — §11) | |

## 11. DESCARTADO deliberadamente (decisión en firme — NO esperarlo en fases siguientes)

*Distinto de "diferido": esto se decidió NO hacer, y volver a abrirlo exige nueva decisión de negocio.*

- **Fusión con el ledger de nómina externa** (`PersonnelFilePayrollTransaction` queda intacto con su consulta propia — P-16/P-02; además su `PayrollPeriodCode` es texto libre, no fusionable).
- **Reapertura de planillas CERRADAS** (política: corrección al periodo siguiente).
- Campo **«aplicación»** del maestro de nómina (eliminado en ratificación: «no lo tenemos claro — removámoslo»).
- **Recontratación masiva/por lote** (D-07: "no prevista en fases futuras") · continuidad automática de antigüedad (D-03) · portabilidad cross-tenant (D-02).
- **Integración con aseguradoras** (D-13) y **sincronización con sistema externo de evaluación** de competencias (FA-1) — CLARIHR es la fuente.
- **Apostilla/legalización** de constancias (FA-04).
- **Caducidad FIFO del saldo de tiempo compensatorio** (D-04/P-04 — ledger simple consciente; solo se revisita si el negocio la introduce).
- **Obligatoriedad de la entrevista de retiro** (D-05) y generalización del motor de formularios (D-01) · lectura de respuestas individuales por jefaturas (FA-7 — solo RRHH).
- **BI genérico / forecasting / ML** en tableros; multi-país simultáneo en una vista.

## 12. Diseñado pero SIN ratificar (gate de negocio — puede entrar a F2 si se decide)

1. **Checklist de requisitos mínimos para publicar expediente** — plan técnico en estado "Diseño", PR-2…PR-6 sin construir; bloquea la ratificación §5 (qué requisito bloquea vs advierte: salario base, DUI, contrato, cuenta bancaria).
2. **Equipo/acceso Nivel B** — el único análisis completamente sin ratificar (P-01…P-09): devolución con condición, identificación del activo, acta/responsiva, integración con egreso, provisión IAM real (N7), permiso dedicado, autoservicio.
3. **Multi-país operativo** (N12) — decisión D-13/BT-03 explícitamente diferible.

## 13. Propuesta de agrupación en olas F2 (para ratificar con el negocio)

| Ola | Contenido | Dependencias |
|---|---|---|
| **F2-A Comunicación** | Proveedor de correo + los 6 correos de identidad (P0 del doc de correos) + avisos de decisión al empleado + digest de bandejas (+ in-app opcional) | ninguna — desbloquea a las demás |
| **F2-B Flujos en línea** | Solicitudes autoservicio (vacaciones, TC, renuncia) + motor multinivel base + delegación efectiva | F2-A (avisos) · parcial N5 |
| **F2-C Tiempo y automatismos** | Módulo de Ausencias/Permisos (costuras listas) + scheduler + suspensión/estado automático + SLA/vencimientos + programación de planilla (P-14) | scheduler es el chasis común |
| **F2-D Documental** | Firma/QR de constancias + plantillas institucionales + adjuntos faltantes (5 módulos) + PDF de gráficos | ninguna dura |
| **F2-E Integraciones** | Archivo bancario de dispersión + ERP + IAM real (con Nivel B ratificado) | contrapartes externas reales |
| **Activaciones baratas** (cualquier momento) | Indicadores de tablero con fuentes ya construidas (ausentismo/HE/costos) · RF-007 recontratación↔liquidación (D-18) · absorber amortización externa en REQ-008 · consulta self de endeudamiento | ninguna |

---

## Trazabilidad

Cada ítem conserva su identificador de decisión original (D-xx/P-xx/FA-xx/RF-xx/G-xx) tal como quedó ratificado en su análisis de `docs/business/`. Costuras verificadas en código hoy: `NotWorkedTimeType.AppliesToPermission` (`src/CLARIHR.Domain/Leave/NotWorkedTimeType.cs:64`) · seed `PERMISO=-9479` reservado (`src/CLARIHR.Infrastructure/Persistence/GlobalCatalogSeedData.cs:950-964`) · `CompanyPreference.OvertimeSelfServiceEnabled` (`src/CLARIHR.Domain/Preferences/CompanyPreference.cs:125`) · gates self-or-view (`PersonnelFilePolicies`/`PayrollRunsReporting`) · canal `SuggestedItems` de liquidación · stubs de correo (ver doc hermano) · ausencia verificada de: motor de notificaciones, scheduler de negocio, entidad de pasos de aprobación (`EconomicAidApprovalStep` no existe en `src/`).
