# Análisis de Negocio — Plazas Asignadas: Ingresos y Egresos configurables

| | |
|---|---|
| **Tipo de documento** | Documentación de requerimientos / Análisis de Negocio |
| **Audiencia** | Product Owner, Project Manager, UX/UI, QA, Equipo de Desarrollo |
| **Módulos afectados** | Expedientes de Personal (`PersonnelFiles`) · Plazas/Asignaciones (`PersonnelFileEmploymentAssignment`) · Compensación (`PersonnelFiles/Compensation`) · Catálogos generales (`GeneralCatalogs`) · Tabulador salarial (`SalaryTabulator`) · Plazas (`PositionSlots`) · (futuro) Nómina/Planilla |
| **Estado** | **Definido / Cerrado** — decisiones ratificadas (D-01…D-20) y preguntas resueltas (R-1…R-6); listo para diseño técnico |
| **Versión** | v3 (incorpora respuestas y refinamientos del negocio del 2026-06-21) |
| **Fecha** | 2026-06-21 |
| **País de referencia** | El Salvador (SV) |
| **Idioma de mensajes/errores** | Bilingüe (ES / EN) |

---

## Contexto del cambio

En el **perfil de personal**, la sección hoy llamada **"Asignaciones"** pasa a llamarse **"Plazas Asignadas"** (incluyendo **rutas y labels técnicos**) y se convierte en el **contenedor laboral-económico** del empleado. Un empleado tiene **al menos una plaza**: **una siempre principal** y las demás **secundarias** (ya existe). Cada plaza concentra **Contrato, Fechas, Plaza, Tipo de contrato y Salario**, y gestiona los **Ingresos** y **Egresos**.

El núcleo es un **modelo unificado y configurable** (`PersonnelFileCompensationConcept`): a nivel **empleado** y/o **plaza** se registran **1 o más ingresos** y **1 o más egresos**, cada uno como **monto fijo** o **porcentaje** (con su **base de cálculo**), con **vigencia**, **moneda** y **periodicidad** (mensual/quincenal/semanal/única). Los **egresos** comparten la estructura de los ingresos y se **clasifican por instancia** en **de ley** (ISSS, AFP, Renta), **internos** (p. ej., daño de equipo) o **externos** (p. ej., préstamo bancario).

**Objetivo explícito:** crear esta estructura **escalable en el tiempo**, lista para alimentar una futura **nómina/planilla** (que es la que **calcula**; aquí solo se **configura**).

> **Hallazgo clave (confirmado).** Esto **no es un desarrollo desde cero**: CLARIHR ya tiene un módulo de Compensación parcialmente construido. El trabajo real es **reorientarlo y completarlo**: (1) **ligar la compensación a la plaza**, (2) **añadir el modo porcentaje** (hoy solo monto fijo), (3) **introducir egresos recurrentes de primera clase** con clasificación ley/interno/externo, (4) **normalizar catálogos** (hoy texto libre), (5) **configurar la tabla de Renta** y los **parámetros de ISSS/AFP** (empleado + carga patronal). El **cálculo** de planilla queda fuera.

### Estado actual verificado en el código (línea base)

| # | Tema | Hallazgo (verificado) | Implicación |
|---|---|---|---|
| 1 | **Plaza = asignación** | `PersonnelFileEmploymentAssignment` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:99`). Contrato/fechas/tipo/jornada/payroll + multi-plaza (principal, cupo, sin solapes). | La plaza ya es contenedor de contrato. **NO tiene Salario** ni vínculo a ingresos/egresos. |
| 2 | **Ingresos** | `PersonnelFileSalaryItem` (`…:335`, CRUD `/salary-items`): `IncomeTypeCode/SalaryRubricCode/CurrencyCode/PayPeriodCode/Amount` (`numeric(18,2)`, `amount>=0`), vigencia, `IsActive`. | Ligado al **expediente, no a la plaza**; **solo monto fijo**; **income-only**; códigos **texto libre**. **Se reemplaza** por el modelo unificado (D-11: sin datos en producción → drop & recreate). |
| 3 | **Bitácora** | `PersonnelFilePayrollTransaction` (`…:730`): `IsDebit`, `SourceSystem/SourceSyncedUtc`, PATCH solo `isActive` ("registro de auditoría inmutable"). | Capa de **movimientos/planilla** (externa), no configuración. **No se modifica**; será destino del cálculo futuro. |
| 4 | **Salario por puesto** | `JobProfileCompensation` → `SalaryTabulatorLine` con `BaseAmount`, `MinAmount`, `MaxAmount`, vigencia. `PositionSlot` (`Domain/PositionSlots/PositionSlot.cs:5`) **no tiene** campo de salario. | El **rango del perfil** ($500–$1000) ya existe. El **valor configurado de la plaza** ($600) **no existe** → nuevo en `PositionSlot` (D-02). |
| 5 | **Dinero** | Sin `Money` VO; `decimal` + `CurrencyCode(40)` por fila; `numeric(18,2)`. Catálogo `currencies`. | Mantener `numeric(18,2)` para montos; **porcentaje con 8 decimales** (D-15). |
| 6 | **Catálogos** | `GeneralCatalogKeyMap` tiene `currencies`, `assignment-types`, etc. **No** existen `income-types`, `deduction-types`, `pay-periods`, `calculation-bases`. `CountryScopedCatalogItem` es **pobre en atributos**. | Crear catálogos; el de **tipos** debe ser **enriquecido** (D-07). |
| 7 | **Concurrencia/errores** | `ConcurrencyToken`+`If-Match`/`ETag`; `Error(code,msg,type)` con **resx EN+ES obligatorio**. | Todo lo nuevo: bilingüe + If-Match. |

---

## Decisiones del negocio (ratificadas)

| # | Tema | Decisión |
|---|---|---|
| **D-01** | Modelo de datos | **Modelo unificado** `PersonnelFileCompensationConcept` con `Nature` = **INGRESO/EGRESO**. Ingresos y egresos comparten estructura y reglas. |
| **D-02** | Salario (3 niveles) | **(1) Perfil de puesto** = **rango** `Min..Max` (vía `SalaryTabulator`); **(2) Plaza** = **valor configurado** almacenado en **`PositionSlot.configuredBaseSalary`** (p. ej. $600) — **nuevo** (R-2); **(3) Empleado** = **salario negociado real** (p. ej. $700), como **concepto INGRESO `SALARIO_BASE` por plaza** (fuente de verdad). El negociado **debe estar dentro del rango salarial de la plaza** (heredado del perfil/tabulador) y **se BLOQUEA si lo supera** (R-3) — p. ej. $700 ✓ dentro de $500–$1000; $1200 ✗. |
| **D-03** | Cardinalidad / ámbito | El empleado tiene **≥1 plaza** y **≥1 ingreso** y **≥1 egreso** (mínimo **1**). El `SALARIO_BASE` garantiza el ingreso; los egresos de ley sugeridos (D-20) garantizan el egreso. Ámbito por `assignedPositionPublicId` **nullable** (null = nivel empleado; con valor = nivel plaza). |
| **D-04** | Fijo vs porcentaje | `CalculationType` = **FIXED / PERCENTAGE**. |
| **D-05** | Base de cálculo del % | Catálogo `calculation-bases`. El **bono porcentual es sobre `SALARIO_BASE`** (base por defecto). |
| **D-06** | Clasificación de egresos | **LEY / INTERNO / EXTERNO**, **editable por instancia**. El tipo propone un default; el usuario puede cambiarlo. |
| **D-07** | Catálogos | Catálogo **enriquecido** `CompensationConceptTypeCatalogItem` (`nature`, `isStatutory`, `defaultDeductionClass`, `defaultCalculationType`, `defaultCalculationBaseCode`, **`defaultEmployeeRate`, `defaultEmployerRate`, `contributionCap`**) + catálogos `pay-periods`, `calculation-bases`. Seed SV. |
| **D-08** | Frontera con nómina | **Solo configuración.** El **cálculo** (resolver %, Renta por tramos, topes, generar movimientos, neto) es **módulo futuro de nómina**, fuera de alcance. **Sin** vista de estimación más allá de la informativa. |
| **D-09** | Egresos externos / período | Externos = **descuento recurrente** (sin saldo/amortización en MVP). La configuración permite aplicar el descuento a **quincena o mensualidad** (periodicidad). |
| **D-10** | Rename | "Asignaciones" → **"Plazas Asignadas"** incluyendo **rutas y labels técnicos** (**breaking change** aceptado; el FE ajusta). `employment-assignments` → **`assigned-positions`**. |
| **D-11** | Migración | **No hay datos en producción** → **eliminar y recrear** (`SalaryItem` se reemplaza por el modelo unificado; sin preservación de datos). |
| **D-12** | Multi-moneda | Conceptos en **monedas distintas** permitidos; **sin** conversión/tasas. |
| **D-13** | Descuentos de ley (ISSS/AFP) | Se guardan **% empleado** **y** **% carga patronal** **y** **topes**. Se configuran **por plaza** (D-18). |
| **D-14** | Renta/ISR | Se **configura** una **tabla de tramos por período**: se cargan **las tres** (semanal, quincenal y mensual) (R-6). El **cálculo no es aquí** (es nómina). Valores **editables en cualquier momento** (D-19). |
| **D-15** | Precisión | **Porcentaje con 8 decimales** (`numeric(11,8)`, rango 0–100). Montos `numeric(18,2)`. |
| **D-16** | Visibilidad / aprobación | El **empleado ve su propia compensación**; además, **roles configurables** mediante un **permiso nuevo `PersonnelFiles.ViewCompensation`** (se crea — R-5). **Sin** workflow de aprobación (diferido). |
| **D-17** | Ingresos recurrentes | Aguinaldo, horas extra, viáticos, comisiones, bonos se **configuran como recurrentes aquí**. |
| **D-18** | Multi-plaza + ley | Los **egresos de ley** se configuran **por plaza** (no consolidados a nivel empleado). |
| **D-19** | Valores legales | Los % de ISSS/AFP, topes y la tabla de Renta son **parámetros 100% editables en cualquier momento**; los seeds son solo **defaults** (no requieren firma legal para salir; **no es crítico** definir los valores exactos ahora) (R-1). |
| **D-20** | Egresos de ley sugeridos | Al **crear una plaza**, el sistema **sugiere automáticamente** los egresos de ley **ISSS y AFP** (con los defaults del catálogo), **editables/eliminables**; **no** bloquea el guardado (R-4). |

---

## 1. Resumen del producto o requerimiento

Se transforma la sección **"Asignaciones"** en **"Plazas Asignadas"** (incluyendo **rutas/labels técnicos** — D-10), consolidando cada **plaza** como contenedor de contrato, fechas, tipo de contrato, **salario** e **Ingresos/Egresos**.

El núcleo es un **modelo unificado y configurable** (`PersonnelFileCompensationConcept`): a nivel **empleado** y/o **plaza** se registran **≥1 ingreso** y **≥1 egreso**, cada uno **fijo o porcentaje** (con **base de cálculo**), **moneda**, **periodicidad** (mensual/quincenal/semanal/única) y **vigencia**. Los **egresos** se **clasifican por instancia** en **ley/interno/externo**. El **salario** se modela en **tres niveles** (rango del perfil → valor configurado en la plaza → **negociado del empleado**, que **bloquea** si supera el rango). Se **configuran** además la **tabla de Renta** (por período) y los **parámetros de ISSS/AFP** (empleado + carga patronal + topes), **sugeridos automáticamente** al crear la plaza.

**Problema que resuelve:** hoy la compensación está **incompleta y desalineada**: el salario/ingresos **no cuelgan de la plaza**, **no existe el modo porcentaje**, **no hay egresos recurrentes** ni su clasificación, los **códigos no están catalogados**, y **no hay configuración de Renta ni de aportes de ley**. Se entrega una estructura **única, configurable y escalable**, base de la futura nómina.

## 2. Objetivos del negocio

1. **Consolidar la plaza** como contenedor de contrato + **salario** (3 niveles) + ingresos/egresos, con **múltiples plazas**.
2. **Configurar la compensación sin desarrollo**: ingresos/egresos por empleado/plaza, **fijos o %**, con catálogos administrables.
3. **Modelar los egresos** salvadoreños: **ley** (ISSS/AFP/Renta), **internos** y **externos**, clasificables por instancia.
4. **Parametrizar la ley**: % de ISSS/AFP (empleado **y** patronal) + topes (editables), y **tabla de Renta** por tramos/período.
5. **Escalabilidad**: estructura que admita nuevos tipos/reglas y un motor de nómina futuro **sin rediseño**.
6. **Reutilizar lo existente** (multi-plaza, tabulador, catálogos, bitácora, concurrencia).
7. **Visibilidad controlada**: el empleado ve su compensación; roles configurables también (permiso nuevo).

## 3. Alcance funcional

- **F1. Renombrar y reencuadrar** "Asignaciones" → **"Plazas Asignadas"** (rutas/labels incluidas — D-10).
- **F2. Salario en 3 niveles**: rango del perfil (tabulador), valor configurado en la plaza (`PositionSlot.configuredBaseSalary`), **salario negociado** del empleado (concepto `SALARIO_BASE` por plaza), con validación **bloqueante** contra el rango.
- **F3. Ingresos configurables** (≥1) por empleado/plaza: tipo, **fijo o %**, base, moneda, periodicidad, vigencia. Incluye aguinaldo/horas extra/viáticos/comisiones/bonos (D-17).
- **F4. Egresos configurables** (≥1) por empleado/plaza: misma mecánica + **clasificación ley/interno/externo editable** (D-06).
- **F5. Catálogos administrables** (country-scoped, enriquecidos): tipos de ingreso/egreso, periodicidades, bases; seed SV.
- **F6. Parámetros de ley**: ISSS/AFP con **% empleado + % patronal + tope**; **por plaza** (D-13, D-18); **sugeridos** al crear la plaza (D-20).
- **F7. Configuración de Renta**: **tabla de tramos por período** (las tres — D-14), consumible por nómina.
- **F8. Ámbito empleado vs plaza** coherente con multi-plaza.
- **F9. Vista consolidada** de compensación (informativa).
- **F10. Validaciones/integridad** (montos, % a 8 decimales, base obligatoria si %, moneda, unicidad de salario base por plaza, **bloqueo de rango**, vigencias).
- **F11. Vigencia/activación e historial**.
- **F12. Visibilidad por rol (`ViewCompensation`) + autoservicio del empleado** (D-16).
- **F13. Mensajería bilingüe + concurrencia `If-Match`**.

## 4. Fuera de alcance (esta fase)

- **Motor de cálculo de planilla / nómina** (D-08): resolver %, aplicar la tabla de Renta y topes, prorrateos, generar `PayrollTransaction`, calcular **neto a pagar**, comprobantes/boletas.
- **Cálculo del ISR por tramos**: aquí solo se **configura** la tabla; el cálculo es de nómina (D-14).
- **Aplicación de topes/redondeos legales de ISSS/AFP**: se **guardan como parámetros**, no se aplican.
- **Amortización de préstamos externos** (saldo/cuotas/intereses): solo descuento recurrente (D-09).
- **Generación de pagos / dispersión bancaria** y conciliación.
- **Conversión de moneda** (multi-moneda sin tasas — D-12).
- **Workflow de aprobación** de cambios de compensación (D-16, diferido).
- **Aguinaldo/indemnización/vacaciones como cálculo** (se configuran ingresos, no se calculan prestaciones).
- **Importación masiva** de configuraciones.

## 5. Actores o usuarios involucrados

| Actor | Rol |
|---|---|
| **Analista / Gestor de RRHH** | Configura plazas, salario, ingresos y egresos (`PersonnelFiles.Manage`). |
| **Administrador de Compensación / Nómina** | Mantiene **catálogos**, **parámetros de ley** (ISSS/AFP) y **tabla de Renta**. Permiso de administración. |
| **Administrador de Plazas** | Define `PositionSlot` (cupo, estado, rol) y el **valor configurado de salario** de la plaza (D-02 nivel 2). |
| **Empleado (autoservicio)** | **Consulta su propia compensación** (solo lectura — D-16). |
| **Roles con visibilidad de compensación** | Roles configurables con el permiso **`PersonnelFiles.ViewCompensation`** (nuevo) que leen la compensación de otros. |
| **Auditor** | Consulta historial de conceptos y cambios. |
| **Sistema CLARIHR** | Valida invariantes; preserva historial; concurrencia; aislamiento por tenant; **sugiere** ISSS/AFP al crear plaza. |
| **(Futuro) Módulo de Nómina** | **Consume** la configuración para calcular y generar movimientos. |

## 6. Requerimientos funcionales

### RF-001 — "Plazas Asignadas" (rename con rutas/labels)
**Descripción:** Renombrar la sección y la **API**: `employment-assignments` → `assigned-positions`; etiquetas técnicas y de UI a "Plazas Asignadas". Mantener el comportamiento multi-plaza.
**Reglas:** **Breaking change aceptado** (D-10); el FE ajusta. Sin preservar nombres viejos.
**Criterios:** Dado el perfil, la sección se titula "Plazas Asignadas" y la API responde en `/assigned-positions`. La plaza principal y secundarias mantienen sus reglas.
**Prioridad:** Alta · **Dependencias:** multi-plaza.

---

### RF-002 — Salario en tres niveles (validación bloqueante)
**Descripción:** Modelar el salario en **(1)** rango del **perfil** (`SalaryTabulatorLine.Min/Max`), **(2)** **valor configurado en la plaza** (`PositionSlot.configuredBaseSalary`), **(3)** **salario negociado del empleado** = concepto INGRESO `SALARIO_BASE` por plaza (fuente de verdad).
**Reglas:**
- El **negociado** **debe estar dentro del rango** `[Min, Max]` salarial de la plaza (heredado del perfil/tabulador); si lo **supera**, el sistema **BLOQUEA** (no permite guardar) (R-3).
- A lo sumo **un** `SALARIO_BASE` **activo** por plaza/vigencia (unicidad).
- El salario base es `FIXED` (sin porcentaje).
- El **valor configurado de la plaza** (nivel 2, `configuredBaseSalary`) es referencia/presupuesto; no es obligatorio para registrar el negociado.
**Criterios:**
- Dado perfil $500–$1000 y plaza configurada $600, cuando registro el negociado en **$700**, entonces se acepta (dentro del rango) como `SALARIO_BASE` activo de esa plaza.
- Dado un negociado de **$1200** (fuera de rango), entonces el sistema **BLOQUEA** con `422 COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE`.
- Dado un segundo `SALARIO_BASE` activo solapado en la plaza, entonces **rechaza** (unicidad).
**Prioridad:** Alta · **Dependencias:** `SalaryTabulator`/`JobProfileCompensation`; `PositionSlots` (nivel 2).

---

### RF-003 — Ingresos configurables (≥1) por empleado y/o plaza
**Descripción:** Registrar **uno o más** ingresos (salario, bono, comisión, **aguinaldo, horas extra, viáticos** — D-17) a nivel empleado o plaza, **fijos o %**.
**Reglas:**
- `Nature=INGRESO`; `ConceptTypeCode` de catálogo; `assignedPositionPublicId` nullable define el ámbito (D-03).
- `FIXED` → `Value` monto `>=0`. `PERCENTAGE` → `Value` % (0–100, **8 decimales**) **y** `CalculationBaseCode` (por defecto `SALARIO_BASE` — D-05).
- `CurrencyCode` (catálogo `currencies`), `PayPeriodCode` (catálogo `pay-periods`), vigencia.
**Criterios:**
- Bono **$10 mensual** → `FIXED`, sin base.
- Comisión **1% mensual** sobre `SALARIO_BASE` → `PERCENTAGE`, `Value=1`, base `SALARIO_BASE`.
- % sin base → rechazo.
**Prioridad:** Alta · **Dependencias:** RF-006, RF-007.

---

### RF-004 — Egresos configurables (≥1) por empleado y/o plaza
**Descripción:** Igual a RF-003 con `Nature=EGRESO`, más **clasificación** (RF-005). Aplicables a **quincena o mensualidad** (D-09).
**Reglas:**
- Mismas reglas de fijo/%/base/moneda/periodicidad/vigencia.
- El monto **no** se guarda negativo; el signo lo da `Nature=EGRESO` (coherente con `IsDebit`). Check `value>=0`.
**Criterios:**
- ISSS **3%** sobre la base de cotización, **por plaza** → `PERCENTAGE`, clasificación `LEY`.
- Daño de equipo **$50** → `FIXED`, `INTERNO`.
- Cuota préstamo bancario **$75 quincenal** → `FIXED`, `EXTERNO`, periodicidad quincenal.
**Prioridad:** Alta · **Dependencias:** RF-005, RF-006, RF-007.

---

### RF-005 — Clasificación de egresos (editable por instancia)
**Descripción:** Cada egreso se clasifica en **LEY / INTERNO / EXTERNO**; el tipo propone un default pero es **editable por instancia** (D-06).
**Reglas:** `LEY` = ISSS/AFP/Renta; `INTERNO` = daño/anticipo/préstamo interno; `EXTERNO` = préstamo bancario/embargo/cuota alimenticia. Un mismo tipo puede usarse como interno o externo según el caso.
**Criterios:** Dado un tipo con default `EXTERNO`, cuando lo registro como `INTERNO`, entonces se respeta lo elegido.
**Prioridad:** Alta · **Dependencias:** RF-007.

---

### RF-006 — Base de cálculo para conceptos porcentuales
**Descripción:** Si `PERCENTAGE`, indicar la **base** (catálogo `calculation-bases`); por defecto `SALARIO_BASE` (D-05).
**Reglas:** `CalculationBaseCode` obligatorio si `%`; vacío si `FIXED`. La **resolución numérica** es de nómina (D-08). `%` en 0–100, escala 8 (D-15).
**Criterios:** % sin base → rechazo; `FIXED` con base → ignorado/rechazado consistentemente.
**Prioridad:** Alta · **Dependencias:** RF-007.

---

### RF-007 — Catálogos administrables (country-scoped, enriquecidos)
**Descripción:** Crear/registrar catálogos: **tipos de ingreso**, **tipos de egreso** (enriquecidos), **periodicidades**, **bases**; seed SV.
**Reglas:**
- Patrón existente (registro en `GeneralCatalogKeyMap`, validación `CatalogCodeIsActiveAsync`, seed `DevSeedService`, lectura `GET /api/v1/general-catalogs/{key}`).
- Catálogo de **tipos** enriquecido (D-07): `nature`, `isStatutory`, `defaultDeductionClass`, `defaultCalculationType`, `defaultCalculationBaseCode`, `defaultEmployeeRate`, `defaultEmployerRate`, `contributionCap`.
- Seed SV **propuesto** (editable — D-19): **Ingresos** `SALARIO_BASE, HORAS_EXTRA, COMISION, BONO, VIATICOS, AGUINALDO, OTRO_INGRESO`. **Egresos ley** `ISSS, AFP, RENTA`. **Internos** `DANO_EQUIPO, ANTICIPO, PRESTAMO_INTERNO`. **Externos** `PRESTAMO_BANCARIO, EMBARGO, CUOTA_ALIMENTICIA, OTRO_EXTERNO`. **Periodicidades** `MENSUAL, QUINCENAL, SEMANAL, UNICA`. **Bases** `SALARIO_BASE, SALARIO_BRUTO, IBC, RUBRO_ESPECIFICO`.
**Criterios:** `deduction-types?countryCode=SV` devuelve ISSS/AFP/Renta con sus atributos; tipo inexistente/inactivo → `…_CODE_INVALID`.
**Prioridad:** Alta · **Dependencias:** patrón de catálogos.

---

### RF-008 — Parámetros de ley (ISSS/AFP) + sugerencia automática
**Descripción:** Guardar, para los egresos de ley, **% del empleado**, **% de la carga patronal** y **topes** (D-13), **por plaza** (D-18); y **sugerirlos automáticamente** al crear la plaza (D-20).
**Reglas:**
- ISSS/AFP llevan `employeeRate`, `employerRate` y `contributionCap` (base máxima cotizable). Defaults en el catálogo de tipos; la **instancia** puede sobrescribir.
- El aporte patronal **no** se descuenta del empleado pero **se guarda** (costo empresa, insumo de nómina/reportes).
- Al **crear una plaza**, el sistema **propone automáticamente** los egresos **ISSS y AFP** con los defaults (editables/eliminables); **no bloquea** el guardado (D-20). Con esto se satisface el mínimo "≥1 egreso" (D-03).
- **Valores SV por defecto (editables en cualquier momento — D-19):** ISSS empleado **3%** / patronal **7.5%**, tope salarial **$1,000/mes**; AFP empleado **7.25%** / patronal **8.75%**, con su IBC máximo.
**Criterios:**
- Dado ISSS, cuando lo configuro, entonces guarda % empleado, % patronal y el tope.
- Dado que **creo una plaza**, entonces el sistema **propone** ISSS y AFP automáticamente (editables/quitables) (D-20).
- Dado un empleado con 2 plazas, entonces ISSS/AFP se configuran **por plaza** (D-18).
**Prioridad:** Alta · **Dependencias:** RF-004, RF-007.

---

### RF-009 — Configuración de la tabla de Renta (ISR) por tramos y período
**Descripción:** Configurar tablas **progresivas de Renta por período**: se cargan **las tres** (semanal, quincenal y mensual) (R-6). **No** se calcula aquí (D-14); es insumo de nómina.
**Reglas:**
- Cada **tramo**: `período`, `orden`, `desde`, `hasta` (nullable en el último), `cuotaFija`, `porcentaje` (sobre el exceso), `sobreExcesoDe`, vigencia.
- Editable por el Administrador de Nómina; **valores editables en cualquier momento** (D-19); versionable por vigencia.
- **Seed tabla mensual SV (verificar publicación vigente; editable — D-19):**

| Tramo | Desde | Hasta | Cuota fija | % sobre exceso | Sobre exceso de |
|---|---|---|---|---|---|
| I | $0.01 | $472.00 | $0.00 | 0% (exento) | — |
| II | $472.01 | $895.24 | $17.67 | 10% | $472.00 |
| III | $895.25 | $2,038.10 | $60.00 | 20% | $895.24 |
| IV | $2,038.11 | en adelante | $288.57 | 30% | $2,038.10 |

> Se cargan **las tres** tablas (semanal/quincenal/mensual), cada una con sus **4 tramos** (misma estructura). Por **D-19** los valores son **editables en cualquier momento**; los de quincenal y semanal se cargan con los montos oficiales por período (orientativamente, quincenal ≈ mensual ÷ 2 y semanal ≈ mensual ÷ 4.33). El MVP **incluye las tres**.

**Criterios:** Dado cualquiera de los períodos (semanal/quincenal/mensual), cuando consulto la tabla de Renta, entonces obtengo sus 4 tramos vigentes con cuota fija y %.
**Prioridad:** Alta · **Dependencias:** RF-007.

---

### RF-010 — Ámbito empleado vs plaza con múltiples plazas
**Descripción:** Conceptos a **nivel empleado** (aplican al empleado) y a **nivel plaza** (aplican a la plaza). Los **egresos de ley** son **por plaza** (D-18).
**Reglas:** Concepto nivel plaza se cierra al cerrar la plaza; concepto nivel empleado persiste mientras el empleado esté activo. Salario e ISSS/AFP → por plaza.
**Criterios:** 2 plazas → cada una con su salario e ISSS/AFP; un préstamo personal puede registrarse a nivel empleado.
**Prioridad:** Alta · **Dependencias:** RF-003/RF-004; multi-plaza.

---

### RF-011 — Vigencia, activación e historial
**Descripción:** Vigencia (`StartDate/EndDate`) y `IsActive` por concepto; cambios **no sobrescriben** historial.
**Reglas:** `EndDate>=StartDate`; baja lógica; cierre en cascada con la plaza.
**Criterios:** desactivar conserva histórico; cambiar monto deja el anterior con su vigencia.
**Prioridad:** Media · **Dependencias:** RF-003/RF-004.

---

### RF-012 — Vista consolidada (informativa)
**Descripción:** Resumen por plaza y total: ingresos, egresos por clasificación, **neto estimado informativo** (sin cálculo de planilla — D-08).
**Reglas:** `FIXED` se suma; `PERCENTAGE` se muestra como "% de {base}" (no se resuelve).
**Criterios:** vista por empleado con ingresos/egresos y neto estimado.
**Prioridad:** Media · **Dependencias:** RF-003/RF-004/RF-006.

---

### RF-013 — Validaciones e integridad
**Descripción:** Consistencia de los datos.
**Reglas:** `value>=0`; `%` 0–100 con 8 decimales; base obligatoria si `%`; moneda/periodicidad/tipo de catálogo activo; **unicidad** del salario base activo por plaza; **bloqueo de salario fuera de rango** (RF-002); vigencias coherentes; concurrencia `If-Match`.
**Criterios:** datos inválidos → error específico (400/422) localizado; `If-Match` viejo → `409`.
**Prioridad:** Alta · **Dependencias:** RF-003…RF-009.

---

### RF-014 — Visibilidad de compensación (autoservicio + roles)
**Descripción:** El **empleado ve su propia** compensación; **roles configurables** con el permiso **`PersonnelFiles.ViewCompensation`** (nuevo) pueden ver la de otros (D-16, R-5). **Sin** workflow de aprobación.
**Reglas:** Lectura sensible protegida por **`PersonnelFiles.ViewCompensation`**; el empleado siempre ve la suya. Escritura con `PersonnelFiles.Manage`.
**Criterios:** El empleado (autoservicio) ve su compensación; un usuario sin el permiso no ve la de terceros.
**Prioridad:** Media · **Dependencias:** RBAC existente (nuevo permiso).

---

### RF-015 — Egresos externos: descuento recurrente (MVP)
**Descripción:** Externos como **descuento recurrente** (fijo o %), con periodicidad (quincena/mensual). **Sin** saldo/amortización (D-09).
**Reglas:** Campos opcionales `counterpartyName`/`externalReference` para contexto; `totalAmount/installmentCount/remainingBalance` **diferidos**.
**Criterios:** Préstamo $75 quincenal → descuento recurrente activo.
**Prioridad:** Media · **Dependencias:** RF-004.

---

### RF-016 — Mensajería bilingüe y concurrencia
**Descripción:** Errores **ES/EN** y mutaciones con **`If-Match`**.
**Reglas:** Cada `Error("CODE",…)`/mensaje en `BackendMessages.resx` **y** `.es.resx`. Códigos propuestos: `COMPENSATION_CONCEPT_TYPE_CODE_INVALID`, `COMPENSATION_CONCEPT_CALCULATION_BASE_REQUIRED`, `COMPENSATION_CONCEPT_PERCENTAGE_OUT_OF_RANGE`, `COMPENSATION_CONCEPT_CURRENCY_INVALID`, `COMPENSATION_CONCEPT_PAY_PERIOD_INVALID`, `COMPENSATION_BASE_SALARY_ALREADY_ACTIVE`, `COMPENSATION_CONCEPT_ASSIGNED_POSITION_NOT_FOUND`, `COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE` (bloqueo).
**Criterios:** `Accept-Language: es` → `detail` en español; `code` estable.
**Prioridad:** Alta · **Dependencias:** localización existente.

## 7. Requerimientos no funcionales

- **Seguridad:** escritura `PersonnelFiles.Manage`; **lectura de compensación** con **`PersonnelFiles.ViewCompensation`** (nuevo) + autoservicio del empleado (D-16); administración de catálogos/parámetros de ley con permiso. Aislamiento **por tenant**.
- **Rendimiento:** listados por expediente/plaza con índices `(TenantId, PersonnelFileId, …)`; consolidado sin N+1.
- **Atomicidad:** alta/cambio transaccional; cierre de plaza arrastra conceptos atómicamente; la **sugerencia de ISSS/AFP** al crear plaza se hace en la misma operación.
- **Escalabilidad:** modelo unificado + catálogos enriquecidos + tabla de Renta versionable → admite nuevos tipos/reglas y la nómina futura **sin rediseño**.
- **Precisión financiera:** montos `numeric(18,2)`; **porcentajes `numeric(11,8)`** (D-15).
- **Usabilidad:** distinción fijo/%, base, clasificación ley/interno/externo; defaults del catálogo; **bloqueo claro** de salario fuera de rango; ISSS/AFP precargados.
- **Auditoría:** historial por vigencia/baja lógica; cambios de salario rastreables.
- **Compatibilidad:** reutiliza concurrencia `If-Match`, CQRS, catálogos, bitácora.
- **Mantenibilidad:** reglas en **módulo puro** testeable (estilo `EmploymentAssignmentRules`).
- **Internacionalización:** ES/EN; catálogos country-scoped.

## 8. Historias de usuario

### HU-001 — Salario negociado dentro del rango (bloqueante)
Como **Analista de RRHH**, quiero **registrar el salario negociado del empleado en su plaza**, sin poder superar el rango del puesto, para **respetar la banda salarial**.
- Dado perfil $500–$1000 y plaza $600, cuando registro $700, entonces se acepta como `SALARIO_BASE`.
- Dado $1200, entonces el sistema **bloquea** (fuera de rango).

### HU-002 — Ingreso fijo o porcentual
Como **Analista de RRHH**, quiero **agregar bonos/comisiones fijos o en %**, para **reflejar la compensación**.
- $10 → `FIXED`. 1% → `PERCENTAGE` sobre `SALARIO_BASE`.

### HU-003 — Descuentos de ley sugeridos con carga patronal
Como **Administrador de Nómina**, quiero **que ISSS/AFP se sugieran al crear la plaza con % empleado y patronal y sus topes, por plaza**, para **tener listo el insumo legal sin recapturar**.
- Al crear la plaza, ISSS y AFP aparecen propuestos (editables/quitables).
- ISSS → 3% empleado / 7.5% patronal / tope $1,000. AFP → 7.25% / 8.75% (editables).

### HU-004 — Configurar la tabla de Renta
Como **Administrador de Nómina**, quiero **mantener las tablas de Renta por tramos y período (semanal/quincenal/mensual)**, para **que la nómina calcule la retención**.

### HU-005 — Descuentos internos y externos
Como **Analista de RRHH**, quiero **registrar descuentos internos (daño) y externos (préstamo), a quincena o mes**, para **descontarlos de forma trazable**.
- Daño $50 → `INTERNO`. Préstamo $75 quincenal → `EXTERNO`.

### HU-006 — Administrar catálogos
Como **Administrador de Compensación**, quiero **administrar tipos/periodicidades/bases y los valores de ley**, para **escalar sin desarrollo** (valores editables en cualquier momento).

### HU-007 — Ver mi compensación
Como **Empleado**, quiero **ver mi compensación**, para **conocer mis ingresos y descuentos**.
- El empleado ve la suya; roles con `ViewCompensation` ven la de otros.

## 9. Reglas de negocio

- **RN-01.** "Asignaciones" → **"Plazas Asignadas"** (rutas/labels incluidas; breaking aceptado — D-10).
- **RN-02.** El empleado tiene **≥1 plaza** (una principal + 0..N secundarias).
- **RN-03.** El empleado/plaza tiene **≥1 ingreso** y **≥1 egreso** (mínimo 1 — D-03). El `SALARIO_BASE` garantiza el ingreso; al **crear la plaza** se **sugieren ISSS/AFP** automáticamente (D-20, editables/eliminables, no bloqueante) para el egreso.
- **RN-04.** Concepto = `FIXED` (monto) o `PERCENTAGE` (%, 0–100, **8 decimales**); el % **exige base** (default `SALARIO_BASE`).
- **RN-05.** **Salario en 3 niveles** (perfil rango → plaza configurada en `PositionSlot` → **negociado** del empleado). El negociado (`SALARIO_BASE`, único activo por plaza) **debe estar dentro del rango y se BLOQUEA si lo supera** (D-02, R-3).
- **RN-06.** Egresos **clasificados por instancia** en **LEY/INTERNO/EXTERNO** (D-06).
- **RN-07.** ISSS/AFP guardan **% empleado + % patronal + tope**, **por plaza** (D-13, D-18). El patronal se guarda aunque no se descuente al empleado. Valores **editables** (D-19).
- **RN-08.** Renta se **configura** como **tabla de tramos por período** (las tres); **no se calcula aquí** (D-14).
- **RN-09.** Montos `>=0` (`numeric(18,2)`); moneda por concepto; **multi-moneda sin conversión** (D-12).
- **RN-10.** Tipos/periodicidades/bases de **catálogo administrable** (D-07).
- **RN-11.** Mutaciones **concurrencia-segura** (`If-Match`) y **por tenant**.
- **RN-12.** Cambios **no sobrescriben** historial (vigencias/baja lógica).
- **RN-13.** El **cálculo de planilla** es de la **nómina futura** (D-08); esto solo **configura**.
- **RN-14.** Conceptos nivel plaza se cierran con la plaza; nivel empleado persisten.
- **RN-15.** Egresos externos = **descuento recurrente** (sin saldo/amortización — D-09); aplicables a quincena o mes.
- **RN-16.** **Sin migración de datos** (drop & recreate — D-11). **Sin** workflow de aprobación (D-16).
- **RN-17.** Visibilidad: el **empleado ve la suya**; **roles con `ViewCompensation`** ven la de otros (D-16).
- **RN-18.** Los **valores legales** (ISSS/AFP/Renta) son **parámetros editables en cualquier momento**; los seeds son solo defaults (D-19).

## 10. Flujos principales

**Flujo A — Configurar la compensación de una plaza:**
1. RRHH abre "Plazas Asignadas" del empleado.
2. Selecciona o **crea** una plaza → el sistema **sugiere automáticamente ISSS y AFP** (editables/quitables) (D-20).
3. Registra el **salario negociado** (`SALARIO_BASE`); si **supera el rango** de la plaza, el sistema **bloquea** (RF-002).
4. Agrega **ingresos** (bono $10, comisión 1% sobre salario base; aguinaldo/horas extra/viáticos) (RF-003).
5. Ajusta/añade **egresos**: ISSS/AFP (ley, % con patronal y tope, por plaza), internos (daño), externos (préstamo, quincenal/mensual) (RF-004/RF-008).
6. El sistema **valida** (tipo, fijo/%, base, moneda, periodicidad, unicidad de salario, rango, 8 decimales) (RF-013).
7. Guarda con `ConcurrencyToken` y vigencia.
8. RRHH consulta la **vista consolidada** (RF-012).

**Flujo B — Administrar catálogos y parámetros de ley:**
1. El Administrador mantiene tipos/periodicidades/bases (RF-007), los **parámetros de ISSS/AFP** (RF-008) y las **tablas de Renta** (RF-009) — todos **editables en cualquier momento** (D-19).

**Flujo C (futuro, fuera de alcance) — Nómina:** toma la configuración, resuelve %, aplica Renta/topes, genera `PayrollTransaction` (egresos `IsDebit=true`), calcula neto.

## 11. Flujos alternativos y excepciones

| ID | Escenario | Resultado |
|---|---|---|
| **E1** | Salario negociado **fuera del rango** de la plaza (perfil/tabulador). | **Bloqueo** `422 COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE` (no se permite guardar) (R-3). |
| **E2** | `PERCENTAGE` sin base. | `422 …_CALCULATION_BASE_REQUIRED`. |
| **E3** | Tipo inexistente/inactivo. | `422 …_TYPE_CODE_INVALID`. |
| **E4** | % fuera de 0–100 o con más de 8 decimales. | `422 …_PERCENTAGE_OUT_OF_RANGE`. |
| **E5** | Moneda/periodicidad fuera de catálogo. | `422 …_CURRENCY_INVALID` / `…_PAY_PERIOD_INVALID`. |
| **E6** | Segundo `SALARIO_BASE` activo solapado en la plaza. | `422 COMPENSATION_BASE_SALARY_ALREADY_ACTIVE`. |
| **E7** | Concepto nivel plaza con plaza inexistente/cerrada. | `404/422 …_ASSIGNED_POSITION_NOT_FOUND`. |
| **E8** | Monto negativo. | `400/422` (check `value>=0`). |
| **E9** | `If-Match` viejo. | `409 CONCURRENCY_CONFLICT`. |
| **E10** | Cierre de plaza con conceptos activos. | Conceptos de la plaza se **cierran** atómicamente. |
| **E11** | Vigencia incoherente. | `400/422` (check de fechas). |
| **E12** | Empleado/rol sin `ViewCompensation` intenta ver compensación ajena. | `403`; el empleado solo ve la suya (D-16). |
| **E13** | Quito los ISSS/AFP sugeridos. | Permitido (son **editables/eliminables**, no bloqueantes — D-20). |

## 12. Datos requeridos

### Entidad: `PersonnelFileCompensationConcept` *(nueva — unifica ingresos y egresos)*

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| publicId | GUID | Sí | único | Identidad |
| personnelFileId | long (FK) | Sí | del tenant | Empleado dueño |
| assignedPositionPublicId | GUID | No | plaza activa si viene | **null = nivel empleado**; con valor = **nivel plaza** (D-03) |
| nature | enum | Sí | `INGRESO`/`EGRESO` | Naturaleza |
| conceptTypeCode | Texto(80) | Sí | catálogo activo por `nature` | Tipo (SALARIO_BASE, BONO, ISSS, …) |
| deductionClass | enum | Cond. | si `EGRESO`: `LEY/INTERNO/EXTERNO` (editable — D-06) | Clasificación |
| calculationType | enum | Sí | `FIXED`/`PERCENTAGE` | Modo |
| value | decimal | Sí | `>=0`; si `%` → `numeric(11,8)`, 0–100 | Monto o porcentaje |
| calculationBaseCode | Texto(40) | Cond. | requerido si `%`; catálogo (default `SALARIO_BASE`) | Base del % |
| employerRate | decimal | No | `numeric(11,8)` | Carga patronal (ISSS/AFP — D-13) |
| contributionCap | decimal | No | `numeric(18,2)` | Tope/base máxima cotizable |
| currencyCode | Texto(40) | Sí | catálogo `currencies` | Moneda (multi-moneda — D-12) |
| payPeriodCode | Texto(40) | Sí | catálogo `pay-periods` (MENSUAL/QUINCENAL/SEMANAL/UNICA) | Periodicidad (D-09) |
| counterpartyName / externalReference | Texto | No | — | Contexto de egreso externo (D-09) |
| startDate / endDate | Fecha | Sí/No | `endDate>=startDate` | Vigencia |
| isActive | Booleano | Sí | — | Estado |
| isSystemSuggested | Booleano | No | — | Marca de concepto **sugerido** (ISSS/AFP al crear plaza — D-20) |
| notes | Texto(2000) | No | — | Observaciones |
| concurrencyToken | GUID | Sí | If-Match | Concurrencia |

Tabla `personnel_file_compensation_concepts`, checks `value>=0` y fechas; índices `(tenant_id, personnel_file_id, nature, is_active)` y `(tenant_id, assigned_position_public_id)`.

### Entidad: `CompensationConceptTypeCatalogItem` *(nueva — catálogo enriquecido)*

| Campo | Tipo | Descripción |
|---|---|---|
| countryCode / code / name | Texto | País / código / nombre |
| nature | enum | INGRESO / EGRESO |
| isStatutory | Booleano | ¿de ley? (ISSS/AFP/RENTA) |
| defaultDeductionClass | enum | LEY/INTERNO/EXTERNO por defecto (editable en instancia) |
| defaultCalculationType / defaultCalculationBaseCode | enum/Texto | Defaults de cálculo |
| defaultEmployeeRate / defaultEmployerRate | decimal(11,8) | % empleado / % patronal por defecto (D-13, editables — D-19) |
| contributionCap | decimal(18,2) | Tope por defecto |
| isActive / sortOrder | Booleano/int | Estado/orden |

### Entidad: `IncomeTaxWithholdingBracket` *(nueva — tabla de Renta configurable, D-14)*

| Campo | Tipo | Descripción |
|---|---|---|
| countryCode | Texto | País (SV) |
| payPeriodCode | Texto | Período (MENSUAL/QUINCENAL/SEMANAL) — se cargan **las tres** (R-6) |
| bracketOrder | int | Orden del tramo |
| lowerBound / upperBound | decimal(18,2) | Desde / Hasta (upper nullable en el último) |
| fixedFee | decimal(18,2) | Cuota fija |
| ratePercent | decimal(11,8) | % sobre el exceso |
| excessOver | decimal(18,2) | Sobre el exceso de |
| effectiveFromUtc / effectiveToUtc | Fecha | Vigencia (versionable; valores editables — D-19) |

Seed mensual SV: ver tabla en **RF-009**. Quincenal y semanal con la misma estructura (montos oficiales por período, editables).

### Entidad: `PositionSlot` *(existente — afectada, D-02 nivel 2)*
Agregar `configuredBaseSalary` (`numeric(18,2)`) + `currencyCode` como **valor configurado** de la plaza (**decidido — R-2**). El **rango** salarial que valida el negociado proviene del perfil/tabulador (`SalaryTabulatorLine.Min/Max`) y es **bloqueante** (R-3).

### Entidad: `PersonnelFileSalaryItem` *(existente — ELIMINAR, D-11)*
Se **reemplaza** por `PersonnelFileCompensationConcept` (INGRESO). **Sin migración de datos** (no hay en producción): la migración **dropa** `personnel_file_salary_items` y crea la nueva tabla.

### Entidad: `PersonnelFilePayrollTransaction` *(existente — consumidor futuro)*
Permanece como **bitácora**; destino del cálculo de nómina. No se modifica.

## 13. Integraciones necesarias

- **Catálogos (`GeneralCatalogs`):** `income-types`/`deduction-types` (enriquecidos), `pay-periods`, `calculation-bases`; reutiliza `currencies`. Registro en `GeneralCatalogKeyMap`, validación, seed, lectura.
- **Plazas (`PositionSlots`):** **nuevo** `configuredBaseSalary` (nivel 2 — D-02); rango bloqueante; cierre en cascada de conceptos de plaza; **sugerencia de ISSS/AFP** al crear plaza (D-20).
- **Tabulador (`SalaryTabulator`/`JobProfileCompensation`):** rango del perfil para **bloquear** el salario negociado fuera de banda.
- **Bitácora (`PersonnelFilePayrollTransaction`):** destino del cálculo futuro.
- **Identity/permisos:** `PersonnelFiles.Read/Manage` + **`PersonnelFiles.ViewCompensation`** (nuevo) + administración de catálogos/parámetros.
- **Localización:** nuevos códigos ES/EN.
- **(Futuro) Nómina:** consumidor de la configuración (conceptos, parámetros de ley, tablas de Renta).
- **Sin integraciones externas** (ISSS/AFP/bancos en línea) en este alcance.

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| **Analista / Gestor de RRHH** | Configurar plazas, salario, ingresos y egresos (`PersonnelFiles.Manage`); leer compensación. | Por tenant; sin aprobación (D-16). |
| **Administrador de Compensación / Nómina** | Mantener catálogos, **parámetros ISSS/AFP** y **tablas de Renta** (editables — D-19). | Permiso de administración. |
| **Administrador de Plazas** | Plazas + **valor configurado de salario** (`PositionSlots.Manage`). | No configura compensación del empleado. |
| **Empleado (autoservicio)** | **Ver su propia** compensación (lectura). | Solo la suya (D-16). |
| **Roles con visibilidad** | **Leer** compensación de otros (`PersonnelFiles.ViewCompensation` — **nuevo**, R-5). | Solo lectura. |
| **Auditor** | Historial de conceptos/cambios. | Solo lectura. |
| **Sistema** | Validar invariantes, sugerir ISSS/AFP, preservar historial, concurrencia, tenant. | Atómico. |

> **Se crea** el permiso de **lectura de compensación** (`PersonnelFiles.ViewCompensation`) para roles configurables, además del autoservicio del empleado (D-16, R-5). Escritura sigue en `PersonnelFiles.Manage`.

## 15. Criterios de aceptación generales

- Sección y API renombradas a **"Plazas Asignadas" / `assigned-positions`** (D-10).
- Salario en **3 niveles** con validación **bloqueante** contra el rango de la plaza (RF-002; RN-05).
- **≥1 ingreso** y **≥1 egreso** por empleado/plaza, **fijo o %** (8 decimales) con base (RF-003/RF-004/RF-006; RN-03/RN-04).
- Egresos **clasificados por instancia** ley/interno/externo (RF-005; RN-06).
- **ISSS/AFP** con **% empleado + patronal + tope**, **por plaza** y **sugeridos** al crear la plaza (RF-008; RN-07; D-20).
- **Tablas de Renta** configurables por tramos/período (las tres), sin cálculo (RF-009; RN-08, D-14).
- Catálogos y **valores legales editables en cualquier momento** (RF-007; RN-18, D-19).
- Validaciones, **concurrencia `If-Match`**, historial (RF-011/RF-013; RN-11/RN-12).
- **Visibilidad**: empleado ve la suya; roles con `ViewCompensation` ven otras (RF-014; RN-17).
- **Sin migración** (drop & recreate) ni workflow de aprobación (RN-16).
- Mensajes **bilingües** (RF-016).
- Estructura **escalable** lista para nómina (D-08).

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **Reemplazo de `SalaryItem`:** drop & recreate elimina la entidad actual; cualquier consumidor interno/endpoint `/salary-items` debe redirigirse al modelo unificado. **Mitigación:** localizar lecturas/endpoints y migrarlos a `/compensation-concepts`.
- **Rename breaking (`assigned-positions`):** rompe el contrato del FE. **Mitigación:** guía de integración Frontend + coordinación de release (D-10 lo acepta).
- **Tres niveles de salario:** confusión entre valor de plaza y negociado. **Mitigación:** UX clara; el negociado (`SALARIO_BASE`) es la **fuente de verdad**; el de plaza es referencia; **bloqueo** de rango explícito.
- **Multi-plaza + ley por plaza (D-18):** los topes (p. ej. ISSS) se aplican por persona en la ley; configurarlos por plaza puede sobre/sub-descontar al consolidar. **Mitigación:** es tema de **cálculo (nómina)**; aquí solo se configura por plaza según lo decidido.
- **Precisión del %:** `numeric(11,8)` debe ser consistente en API/serialización. **Mitigación:** contrato y validación 0–100 con 8 decimales.

### Supuestos
- Construye sobre **multi-plaza** y el **módulo de Compensación** existentes.
- El **cálculo/ejecución de planilla** es **módulo futuro** (D-08).
- **No hay datos de compensación en producción** (D-11).
- Los **valores legales** (ISSS/AFP/Renta) son **parámetros editables en cualquier momento** (D-19); **no** bloquean la salida ni requieren firma legal previa (R-1).
- Multi-moneda **sin** conversión (D-12).
- País inicial **El Salvador**; catálogos y tablas country-scoped.

### Dependencias
- Patrón de **catálogos** country-scoped (registro/validación/seed/lectura).
- **Plazas/multi-plaza** y **PositionSlots** (nuevo `configuredBaseSalary` + sugerencia ISSS/AFP).
- **Tabulador salarial** (rango del perfil, bloqueante).
- **Localización** ES/EN y **concurrencia** `If-Match`.
- **Permiso nuevo** `PersonnelFiles.ViewCompensation` (D-16).
- **(Futuro) Nómina** (consumidor).

## 17. Preguntas abiertas

> **No quedan preguntas abiertas.** Las decisiones de diseño se **ratificaron** (D-01…D-20) y las **6 confirmaciones operativas** restantes se resolvieron (R-1…R-6):

| # | Punto | Resolución | Decisión |
|---|---|---|---|
| R-1 | Valores legales (ISSS/AFP/Renta) | **Parámetros 100% editables en cualquier momento**; los seeds son solo defaults. No es crítico definirlos ahora ni requieren firma legal para salir. | D-19 |
| R-2 | Salario de plaza (nivel 2) | Se **almacena `configuredBaseSalary` en `PositionSlot`** (no se deriva). | D-02 |
| R-3 | Validación de rango | **Bloquea**: el salario negociado **no puede superar el rango** salarial de la plaza. | D-02 |
| R-4 | Mínimo de egresos | Al crear la plaza se **sugieren automáticamente ISSS/AFP** (editables/eliminables, no bloqueante). | D-20 |
| R-5 | Permiso de lectura | Se **crea** `PersonnelFiles.ViewCompensation` (nuevo). | D-16 |
| R-6 | Tabla de Renta por período | Se cargan **las tres**: semanal, quincenal y mensual. | D-14 |

## 18. Recomendaciones del Analista de Negocio

1. **Modelo unificado `CompensationConcept`** (D-01) como única superficie de ingresos/egresos; reglas en **módulo puro testeable** (estilo `EmploymentAssignmentRules`), bilingüe desde el día uno.
2. **Salario como `SALARIO_BASE` por plaza** (nivel 3 = fuente de verdad), con **bloqueo** contra el rango del perfil; el **valor configurado de la plaza** (`PositionSlot.configuredBaseSalary`, nivel 2) como referencia.
3. **Sugerir ISSS/AFP al crear la plaza** (D-20) con `isSystemSuggested=true`, editables/eliminables: resuelve el mínimo "≥1 egreso" sin bloquear el guardado.
4. **Catálogos enriquecidos + parámetros de ley editables**: tipos con `nature/isStatutory/defaultDeductionClass/defaultEmployeeRate/defaultEmployerRate/contributionCap`; **tablas de Renta versionables** (las tres). Todo **editable en cualquier momento** (D-19) — materializa la escalabilidad pedida.
5. **Drop & recreate de `SalaryItem`** (D-11): migración EF que elimina `personnel_file_salary_items` y crea `personnel_file_compensation_concepts`; redirigir lecturas internas y el endpoint `/salary-items` al nuevo `/compensation-concepts`.
6. **Rename con guía de Frontend** (D-10): `employment-assignments` → `assigned-positions`, nuevo `/compensation-concepts`, y documento de integración (como en multi-plaza/recontratación).
7. **Separar configuración de cálculo** (D-08): los parámetros (ISSS/AFP/Renta) son **insumo** del motor de nómina futuro; esta fase **no calcula**.
8. **Mantener los valores legales como parámetros editables** (D-19): los seeds son defaults ajustables en cualquier momento; sin dependencia de firma legal para la salida.
9. **Producir el documento técnico de implementación** (modelo `CompensationConcept`, catálogos enriquecidos + tablas de Renta + migraciones, sugerencia de ISSS/AFP, `configuredBaseSalary` + bloqueo de rango, endpoints `/compensation-concepts` y rename `/assigned-positions`, permiso `ViewCompensation`, pruebas de parity de localización y E2E) y la **guía de integración Frontend**.

---

> **Nota de trazabilidad.** Análisis construido **verificando el código**: la plaza `PersonnelFileEmploymentAssignment` (`PersonnelFileEmployee.cs:99`, sin salario); ingresos `PersonnelFileSalaryItem` (`…:335`, fijos, ligados al expediente — **a reemplazar**); bitácora `PersonnelFilePayrollTransaction` (`…:730`, externa/inmutable); `PositionSlot` (`PositionSlots/PositionSlot.cs:5`, **sin** salario → se le agrega `configuredBaseSalary`); `JobProfileCompensation`→`SalaryTabulatorLine` (rango `Min/Max/Base`); `GeneralCatalogKeyMap` (sin catálogos de compensación); convención `numeric(18,2)`+`CurrencyCode`. Decisiones **ratificadas** (D-01…D-20) y preguntas **resueltas** (R-1…R-6). Los **valores legales** de ISSS/AFP/Renta se incluyen como **defaults editables** (D-19): pueden modificarse en cualquier momento; no se asumen como definitivos.
