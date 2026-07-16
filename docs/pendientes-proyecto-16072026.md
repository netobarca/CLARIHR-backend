# Pendientes para completar el proyecto — corte 2026-07-16

> **Propósito**: inventario único de TODO lo que falta para dar por completo el proyecto *hasta este punto* (según lo documentado y lo desarrollado), producto de una verificación integral ejecutada el 2026-07-16: backlog (`docs/backlog-requerimientos.md`), los 34 análisis de negocio (`docs/business/`), los ~70 documentos técnicos (`docs/technical/` + `docs/questions-incidents/`), la memoria de sesiones y el código/las suites reales.
>
> **Veredicto ejecutivo**: el **desarrollo del backlog está COMPLETO — 15/15 requerimientos en 🟢 — y verificado en fresco hoy** (§1). Lo pendiente NO es código de los requerimientos: son (A) integración de ramas y la decisión de push, (B) checklists de despliegue infra/BD, (C) confirmaciones de negocio/contador, (D) adopción de datos por empresa, (E) el piloto de planilla, (F) dos diseños sin ratificar que nunca entraron al backlog, y (G) el catálogo de funcionalidades **diferidas a F2/F3 por decisión ratificada** (que no son deuda, pero definen el alcance de la siguiente fase).

---

## 1. Verificación ejecutada hoy (evidencia en fresco, no de bitácora)

| Verificación | Resultado 2026-07-16 | Cuadra con lo documentado |
|---|---|---|
| Suite unitaria (`CLARIHR.Application.UnitTests`) | **2766/2766 en verde** (re-ejecutada) | ✅ idéntico al cierre de PR-8 |
| Suite integración `~PayrollRun` (pila Docker completa: Postgres + Gotenberg + Azurite; incluye boletas PDF y lote zip) | **11/11 en verde** (1m03s) | ✅ idéntico a la certificación |
| Contrato `docs/technical/api/openapi.yaml` | **557 paths / 948 schemas** | ✅ exacto |
| Backlog: checkboxes de **desarrollo** sin marcar | **0** (los únicos `[ ]` en los 15 REQ son de despliegue/operación/negocio) | ✅ |
| Working tree | limpio; rama `feature/planilla-generacion` en `1927106` | ✅ |
| Migraciones en código | 31 migraciones nuevas desde `20260708…AddLeaveConfigurationMasters` hasta `20260716130012_AddPayrollRuns` | ✅ M1–M4 de cada REQ presentes |

**Estado del backlog (15/15 🟢)**: REQ-001 vacaciones/incapacidades · REQ-002 tiempo compensatorio · REQ-003 otras transacciones · REQ-004 tablero de acciones · REQ-005 ingresos cíclicos · REQ-006 ingresos eventuales · REQ-007 horas extras · REQ-008 descuentos cíclicos · REQ-009 descuentos eventuales · REQ-010 endeudamiento · REQ-011 tiempos no trabajados · REQ-012 motor de generación de planilla (PR-1…PR-8) · REQ-013/014/015 entregados íntegros vía REQ-012 con correspondencias verificadas e2e.

---

## 2. Pendiente A — Integración de código y decisión de push 🔑

*Lo único que separa el código terminado de un repositorio publicable.*

| # | Acción | Detalle |
|---|---|---|
| A-1 | **Merge `feature/planilla-generacion` → `master` local** | La rama lleva **18 commits** sobre `master` (PR-1…PR-8 de REQ-012 + bitácoras). `master` es su base directa → fast-forward esperado. |
| A-2 | **Decisión de push a `origin/master`** | `master` local va **36 commits adelante** de `origin` (54 tras A-1). Registrada en el backlog como **decisión aparte del usuario** — no se ejecuta sin esa decisión. |
| A-3 | **Riesgo mientras no se haga A-2** | Todo REQ-001…REQ-015 (código + docs) vive **solo en esta máquina**. Es el punto único de falla del proyecto completo. |

---

## 3. Pendiente B — Despliegue: infraestructura y base de datos

*Checklists escritos y vigentes; ninguno ejecutado (a 2026-07-12 no existe producción — el "servidor" actual es el entorno compartido que usa el FE).*

### 3.1 Aprovisionamiento por entorno (una sola vez) — runbook `docs/technical/operations/production-deployment.md`
- [ ] PostgreSQL gestionado · Storage account · Gotenberg · App Service .NET 10 · Google OAuth (§1).
- [ ] **Checklist de seguridad pre-producción §6 — está íntegro sin marcar**: secretos solo en App Settings, `UseManagedIdentity=true`, red de PostgreSQL restringida, `Swagger:Enabled=false`, **rotación de credenciales §N1**, gitleaks en verde, `SigningKey` JWT único.

### 3.2 Migraciones de base de datos en el servidor
- [ ] Aplicar **todas las migraciones pendientes** (procedimiento: `docs/technical/operations/manual-migrations-and-azure-deploy.md`). Como mínimo las **31 de REQ-001…REQ-012** (2026-07-08 → 2026-07-16, `AddLeaveConfigurationMasters` … `AddPayrollRuns`); **verificar antes** que las 3 migraciones de seed de junio (`20260627212537`, `20260628234354`, `20260628235800`) ya estén — los incidentes del FE de junio fueron por su ausencia.
- [ ] **Las dos limpiezas destructivas viajan dentro de migraciones y son irreversibles por diseño**: `payroll_type_code` (REQ-004 M1 — **registrar el conteo por valor ANTES** en la bitácora del despliegue) y `workday_code` (REQ-012 M3). Comunicar que las plazas quedan "Sin dato" en tipo de planilla/jornada hasta reclasificarse por edición natural.
- [ ] **Verificación post-deploy de catálogos** con `docs/technical/verificacion-catalogos-servidor.md` — regla de oro: **`DevSeedService` NUNCA siembra en el servidor; solo `HasData`-en-migración llega**.
- [ ] Limpieza de datos puntual: formularios de entrevista huérfanos en el tenant `4252b16d-…` (causan 409 por nombre duplicado — `respuesta-backend-incidentes-frontend-27062026.md`).
- [ ] Migración de datos de **competencias curriculares** (plan R1): siembra de catálogos por tenant/país + backfill `normalized_requirement_name` **en la misma ventana** del deploy que activa la validación estricta.

### 3.3 Configuración `Storage:Purposes` (appsettings **base**) + contenedores blob (creación MANUAL por ops en cada entorno)

| Purpose (config) | Contenedor | Módulo | Si falta |
|---|---|---|---|
| `IncapacityDocument` | `clarihr-incapacity-documents` | REQ-001 | 422 en alta con constancia |
| `CompensatoryTimeDocument` | `clarihr-compensatory-time-documents` | REQ-002 | 422 (adjunto PDF es obligatorio en POST) |
| `RecognitionDocument` | `clarihr-recognition-documents` | REQ-003 | 422 |
| `DisciplinaryActionDocument` | `clarihr-disciplinary-action-documents` | REQ-003 | 422 |
| `CompanyLogo` (agregada 30/06, **pendiente de desplegar**) | `clarihr-company-logos` | logo empresa | 404 `ContainerNotFound` en el PUT |

⚠️ Los tests de integración NO detectan contenedores faltantes — es paso de ops puro.

### 3.4 Permisos por rol (asignación en el tenant; los `Authorize*` NO los cubre Admin — separación de funciones)

| REQ | Permisos a asignar |
|---|---|
| REQ-001 | los 6 permisos de leave |
| REQ-005 | `View/ManageRecurringIncomes` + **`AuthorizeRecurringIncomes`** (rol autorizador distinto) |
| REQ-006 | **`AuthorizeOneTimeIncomes`** |
| REQ-007 | **`AuthorizeOvertimeRecords`** |
| REQ-008 | `View/ManageRecurringDeductions` + **`AuthorizeRecurringDeductions`** |
| REQ-009 | `View/Manage/AuthorizeOneTimeDeductions` |
| REQ-010 | `ViewIndebtedness` / `ManageIndebtednessParameters` (estos SÍ los cubre Admin) |
| REQ-011 | `ViewNotWorkedTimes` / `ManageNotWorkedTimes` / `ManageNotWorkedTimeTypes` |
| REQ-012 | los 5 de planilla: `View/ManagePayrollRuns`, **`AuthorizePayrollRuns` a un rol NO-Admin**, `PayrollConfiguration.Read/Manage` |

---

## 4. Pendiente C — Confirmaciones de NEGOCIO / CONTADOR antes de desplegar

*El código está construido a la regla ratificada; si el contador discrepa "se corrige por dato o por cálculo, no por modelo". Ninguna bloquea el build — todas bloquean el corte productivo.*

| # | Confirmación | Módulo | Estado |
|---|---|---|---|
| C-1 | **Casos dorados del contador para LIQUIDACIÓN** — el motor solo tiene el caso sintético verificado a mano | Liquidación (pre-backlog) | ⚠️ **pendiente; es EL gate pre-deploy de liquidación** |
| C-2 | Factores de horas extra de la plantilla (HED 2.00 / HEN 2.50 / HEDF 4.00 / HENF 5.00 + posible tipo «día de descanso» Art. 175) — **antes** del `load-template` productivo | REQ-007 (P-03) | pendiente |
| C-3 | **Firma del contador sobre los dorados de amortización A.3** (francesa estándar; suite 32/32 verde; si discrepa se corrige el PLAN por dato) | REQ-008 | pendiente (ratificación de negocio, no bloqueo de código) |
| C-4 | Regla del SÉPTIMO (+1 día completo por semana afectada; lun→vie descuenta SEIS días) — fijada por 17 dorados | REQ-011 (P-18) | pendiente sign-off |
| C-5 | Tarifa de valoración del tiempo compensatorio al liquidar (default parametrizable 1.00) | REQ-002 (P-15) | pendiente |
| C-6 | Re-verificar **vigencia** de mínimo $408.80 · semana 44 h · tablas de Renta DGII al momento del despliegue (el sign-off golden 13/13 + golden 14 YA está obtenido 2026-07-14/15) | REQ-012 | solo re-verificación |

**Comunicaciones al negocio/operación que los checklists exigen**: la validación de endeudamiento es **opt-in por configuración** (sin `maxIndebtednessPercent`/límites NO corre — no es un bug) y lo previo a REQ-010 no se re-valida (P-13) · el lote por periodo de descuentos cíclicos es **ATÓMICO** y el ledger cuenta **COBROS, no cuotas** · operación manual F1 por periodo en ingresos/descuentos (bandeja → aplicar; el insumo exportado es el puente con la nómina externa mientras conviva) · los dos «no» de F1 de planilla (sin correo de boletas · sin programación a fecha/hora).

---

## 5. Pendiente D — Adopción por empresa (datos que cada tenant debe cargar)

*El pre-flight de la primera corrida de planilla lista los huecos automáticamente.*

- **REQ-012 (planilla)**: crear las **nóminas** (`payroll-definitions`) · generar el **calendario de periodos del año** (no hay plantilla — cada empresa define sus quincenas) · `load-template` de **jornadas 44 h** · verificar plazas: `PayrollTypeCode`, salario base ≥ **$408.80**, forma de pago/cuenta bancaria.
- **Tablas de Renta por tenant**: verificar que existan las 3 frecuencias (MENSUAL/QUINCENAL/SEMANAL). El seed de PR-4 es DevSeed (no llega al servidor) → cargarlas vía `PUT api/v1/income-tax-brackets` si faltan. Sin tabla: retención 0 + warning (nunca bloquea, pero paga mal).
- **REQ-001**: `load-template` de riesgos/tipos/asuetos del año · clínicas (maestro inicia vacío) · `restDayOfWeek` y preferencias.
- **REQ-002**: crear tipos de tiempo compensatorio (maestro **sin semilla por diseño**) · 4 preferencias · saldos iniciales con acta adjunta.
- **REQ-003**: `load-template` de los 3 maestros (causas sin concepto de descuento — cada empresa asocia si su marco lo permite).
- **REQ-005/006**: revisar catálogo de tipos · confirmar conceptos `Nature=Ingreso` · plazas con centro de costo · registrar retroactivos si se desea continuidad de reportes.
- **REQ-007**: `load-template` de los 2 maestros (tras C-2) · preferencias (`OvertimeSelfServiceEnabled` — piloto recomendado, default off · `OvertimeMaxDailyMinutes`).
- **REQ-008**: preferencia de tasa de interés default (vía **PUT** de preferencias) · revisar plantilla del catálogo de tipos.
- **REQ-010**: **configurar parámetros de endeudamiento** (sin ellos la validación no corre — comunicarlo).
- **REQ-011**: `load-template` del maestro TNT · **verificar asuetos y `RestDayOfWeek`** en las empresas piloto (sin ellos el cálculo degrada silenciosamente a domingo).
- **Catálogos por empresa que arrancan VACÍOS por diseño**: `competency-domains` y `requirement-types` (un admin debe POSTear items antes de crear competencias curriculares).
- **REQ-014 (adopción)**: usar el pre-flight de la primera corrida como **inventario de rezagos históricos** y excluir lo ya pagado por la nómina externa (advertir-nunca-bloquear).

---

## 6. Pendiente E — Piloto de planilla

- [ ] **Una nómina, un periodo, corrida en PARALELO contra la nómina externa** (cuadre vía insumos exportados) **antes del corte definitivo** al motor interno. Es el último gate operativo de REQ-012 y donde se re-validan C-1…C-6 con números reales.

---

## 7. Pendiente F — Diseñado pero NO ratificado (la única cola de "desarrollo" que existe)

*No pertenecen al backlog 15/15; son diseños en espera de decisión de negocio. Sin la ratificación no hay nada que construir.*

| # | Ítem | Estado | Bloqueado por |
|---|---|---|---|
| F-1 | **Checklist guiado de requisitos mínimos para publicar expediente** — `docs/technical/plan-tecnico-requisitos-minimos-publicacion-expediente.md` | Estado "Diseño"; solo el Bug 3 (deducción de plaza) se implementó; **PR-2…PR-6 sin construir** | Ratificación §5: qué requisito **bloquea** vs **solo advierte** (salario base, DUI, contrato vigente, cuenta bancaria) |
| F-2 | **Equipo/acceso — Nivel B** (ciclo de devolución con condición, serie/etiqueta/valor, acta/responsiva firmada, integración con egreso, permiso dedicado) | Nivel A (hardening) construido; **es el ÚNICO análisis del proyecto completamente sin ratificar** | P-01…P-09 de `analisis-equipo-acceso-empleado.md` |
| F-3 | `costCenterPublicId` en `PositionSlotResponse` (pedido del FE, jun-2026) | Sin construir (join nuevo a cost-centers) | Priorización |
| F-4 | Siembra de catálogos para **países distintos de SV** | Solo SV sembrado | Onboarding real de otro país |

---

## 8. Definido para FASES POSTERIORES (F2/F3) — decisión ratificada, NO es deuda

### 8.1 Transversales (aparecen en casi todos los módulos; son "el programa" de la F2)

1. **Motor de aprobación multinivel / delegación efectiva de autoridad** — F1 usa siempre flujo de una decisión + anti-self. El punto de extensión reservado es Sustitución de autorizaciones (RF-010) + su vínculo con un futuro módulo de **Ausencias**.
2. **Notificaciones correo/in-app** — no existe proveedor real de email (solo stubs `LoggingEmailService`). Bloquea: boletas por correo, avisos a autorizadores, recordatorios.
3. **Autoservicio del empleado ampliado** — solicitudes en línea (vacaciones/TC), consulta de su liquidación, renuncia autoservicio, **boleta PDF de planilla en autoservicio (P-12)**.
4. **Programación de la generación de planilla a fecha/hora** (REQ-012 P-14) — diseño reservado: cola de jobs molde `ReportExportJob`.
5. **Marcación/biometría y control de asistencia** — no existe; costura `origin` MANUAL/MARCACION documentada.
6. **Multi-moneda/FX** — sistema mono-moneda USD por diseño.
7. **Importadores masivos de históricos** — la adopción es por registro retroactivo normal.
8. **Relación puesto↔nivel piramidal** — bloquea el filtro por nivel en ambos tableros.
9. **Contabilización a ERP · archivo bancario propietario** (la conciliación bancaria exportable SÍ está en F1).

### 8.2 Por módulo (diferidos explícitos ratificados)

| Módulo | Diferido a F2/F3 |
|---|---|
| **Planilla (REQ-012…015)** | correo de boletas · programación (P-14) · boleta autoservicio (P-12) · retroactividad sobre periodos CERRADOS (correcciones van al siguiente) · líneas manuales ad-hoc en la corrida · fusión con el ledger externo `PersonnelFilePayrollTransaction` (P-16 — intocable) · exports del historial por empleado · «parámetros especiales»/apagado por categoría (solo con ejemplo real del negocio) |
| **Vacaciones/incapacidades (REQ-001)** | permisos `Authorize*` + flujo en línea multinivel · plan de vacaciones por jefaturas · cambio automático de `EmploymentStatusCode` (P-18) · motor de jornadas real (`usaJornada`) · comprobante PDF |
| **Tiempo compensatorio (REQ-002)** | caducidad del saldo (tramos FIFO — descartada F1) · `AuthorizeCompensatoryTime` · solicitud del empleado · provisión financiera |
| **Otras transacciones (REQ-003)** | multinivel configurable (P-01) · suspensión automática con reversión programada (P-05) · reincidencia/prescripción de amonestaciones (P-13) · módulo de permisos/licencias generales (`PERMISO=-9479` reservado — NO reutilizar) |
| **Tableros (REQ-004 / RRHH)** | impresión/PDF de gráficos (P-01/D-12, sin librería de charting) · filtro nivel piramidal · montos bajo `ViewCompensation` (D-15) · snapshot dimensional · comparativas multi-periodo · export por indicador |
| **Ingresos/descuentos (REQ-005…009)** | multinivel (P-12) · adjuntos (P-07) · notificaciones · maestro de instituciones financieras |
| **Endeudamiento (REQ-010)** | ingresos adicionales en la base · descuentos eventuales en el cálculo · consulta self del empleado · re-validación retroactiva |
| **Horas extras (REQ-007)** | sugerencia de tipo festivo/día de descanso desde asuetos · portal de jefaturas · conciliación planificado-vs-trabajado (P-02) · adjuntos (P-10) |
| **Liquidación** | plantilla institucional firmable · conversión escenario→liquidación (D-05) · flujo revisor/aprobador + estado `PAGADA` (D-15) · señal automática a recontratación (D-18) · beneficiario por fallecimiento · autoservicio (D-20) |
| **Retiro definitivo** | adjuntos (D-19) · notificaciones (D-17) · renuncia autoservicio (D-03) · CRUD admin de catálogos (seed-only) |
| **Entrevista de retiro** | F2: tabulación/analítica de rotación (RF-014) + corrección de respuestas (RF-020) · F3: lógica condicional + notificaciones · CRUD admin de catálogos (RF-015) |
| **Constancias** | firma electrónica + folio/QR verificable · plantillas/firmantes por unidad o tipo · alertas SLA (D-14) · desglose salarial (P-04) |
| **Revalidación de catálogos** | F2 educación estructurada · F3 documentos con formato/regex · F4 maestro AFP ampliado · admin dedicado de catálogos enriquecidos |
| **Competencias** | análisis de brecha vs perfil de puesto (D-07) · evidencias/adjuntos con verificación · registro por el propio empleado |
| **Reclamos seguro médico** | consulta enriquecida (RF-010, F3) · adjuntos ya en F2 · integración con aseguradora descartada (D-13) |
| **Seguros / Sustitución / Ayuda económica / Fuera de nómina** | catálogos por tenant · delegación real (D-10) · multinivel/umbrales + ayudas reembolsables (FA-03) + taxabilidad (D-15) · workflow de gasto (D-09) + export xlsx |
| **Multi-plaza / Plazas ingresos-egresos** | % dedicación/FTE (P-07) · consolidación Fase 2 (limpiar campos raíz) · amortización de préstamos externos · organigrama de posiciones |

---

## 9. Deuda técnica y documentación por alinear (menores)

- [ ] Guía FE de competencias curriculares dice **428** donde corresponde otro código — corrección anunciada en `aclaraciones.md` y aún no aplicada.
- [ ] Cabeceras desactualizadas en `docs/questions-incidents/` — varios incidentes marcados 🟡/🔴 "Abierto" que ya están resueltos en backend (assigned-positions, contract-history, deadlock-publicar, personnel-actions, exit-interview) — actualizarlas o archivarlas.
- [ ] Planes técnicos con notas superadas por la realidad (p. ej. ingresos cíclicos/eventuales aún dicen "`PayrollPeriodDefinition` no está construido" — REQ-012 PR-2 lo extendió). Son notas históricas; no requieren acción de código.
- [ ] Naming heredado `SubstitutePersonnelFilePublicId` vs `…Id` (sustitución) — mantener u homologar, decisión estética.
- [ ] ADR-0002: normalización de búsqueda asimétrica (falsos negativos) — observación abierta menor.

## 10. Notas de entorno de pruebas (NO son despliegue)

- `GRANT pg_signal_backend TO clarihr;` en el Postgres local elimina el flake `42501` del reset de BD.
- Los 2 tests de boleta PDF exigen el contenedor `gotenberg` arriba (hoy verificado arriba; los 11/11 de `~PayrollRun` lo incluyeron).
- Re-medir el baseline de PDFs/segundo del worker antes del primer pico de fin de mes (runbook de load-testing).

---

## Fuentes

`docs/backlog-requerimientos.md` (secciones "Pendientes de despliegue" y "Próxima acción" de los 15 REQ) · `docs/technical/plan-tecnico-planilla-generacion.md` §7/§8 · `docs/technical/operations/production-deployment.md` §1/§6 · `docs/technical/verificacion-catalogos-servidor.md` · `docs/technical/aclaraciones.md` · `docs/technical/respuesta-backend-*.md` (jun-2026) · `docs/technical/plan-tecnico-requisitos-minimos-publicacion-expediente.md` · los 29 `docs/business/analisis-*.md` (secciones §4 "fuera de alcance" y §17 ratificaciones) · verificación en vivo 2026-07-16 (suites + openapi + git).
