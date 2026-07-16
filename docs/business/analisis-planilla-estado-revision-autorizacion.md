# Análisis de negocio — Planilla: estado de generación, revisión (exports Excel/CSV) y autorización (cierre de nómina)

| | |
|---|---|
| **Tipo** | Análisis de negocio (validación contra el análisis/plan **RATIFICADOS** de REQ-012 + verificación puntual de código) |
| **Módulo** | Planilla — la **cara de consulta y decisión** del motor: estado de generación (bandeja de corridas) · revisión del detalle con exports Excel/CSV · autorización que congela los cálculos y cierra la nómina |
| **Fecha** | 2026-07-15 |
| **Autor** | Equipo CLARIHR — análisis asistido, validado contra `analisis-planilla-generacion.md` (RATIFICADO 2026-07-14) + `plan-tecnico-planilla-generacion.md` (escrito 2026-07-14) + código (HEAD `597aa75`, rama `feature/planilla-descuentos`) |
| **Estado** | **RATIFICADO por el negocio (2026-07-15)** — P-01…P-03 respondidas (§17): **las 3 recomendaciones aceptadas TAL CUAL** («Mantener 2 pasos» · «Confirmar tal cual» · «Arrancar con la cabecera persistida»). **El modelo ratificado de REQ-012 queda SIN CAMBIOS y este REQ sin condiciones de negocio pendientes**; resta solo la verificación de correspondencia al cerrar PR-6/PR-7/PR-8 |
| **Naturaleza del requerimiento** | **SUBCONJUNTO de REQ-012.** Los tres sub-requerimientos son la especificación detallada de capacidades que el levantamiento del motor ya incluyó y el negocio ya ratificó (2026-07-14). **No agregan alcance nuevo**: aterrizan en los PR-6 y PR-7 del plan técnico existente (los datos que se consultan los producen PR-4/PR-5). Este documento sirve como **confirmación de cobertura + checklist de correspondencia**, no como levantamiento de construcción |
| **Documentos hermanos** | [`analisis-planilla-generacion.md`](analisis-planilla-generacion.md) (REQ-012 — el motor; RF-010/014/015/017/019/020 son la cobertura de este REQ) · [`plan-tecnico-planilla-generacion.md`](../technical/plan-tecnico-planilla-generacion.md) (§1.4 modelo de corrida · §3.6 revisión/decisión/cierre · §3.7 bandeja/reportes) |
| **Alcance geográfico** | El Salvador (SV) — mismo corte de toda la cadena |

---

## Contexto del cambio (requerimiento original)

> **Estado Generación de planillas** — Esta consulta deberá proporcionar elementos para visualizar el estado de la generación de planillas, que se han generado en diferentes periodos; para visualizarlas se podrán elegir de un listado con la información más importante de cada planilla.
>
> **Revisión de planillas** — Esta consulta deberá permitir revisar los datos de la planilla generada, para confirmar que los ingresos, descuentos y acciones se han aplicado correctamente. También se deberá tener opciones para exportar la información de las planillas a formatos de Microsoft Excel y formato Separado por Coma (CSV).
>
> **Autorización de Planillas** — Esta acción se deberá realizar sobre las planillas generadas, para cerrar la nómina. Luego de esta acción ya no se deberá permitir hacer cambios en los cálculos.

---

## 0. Veredicto ejecutivo

1. **Cobertura TOTAL por REQ-012 — cero desarrollo nuevo.** El levantamiento original del motor ya pedía «las **consultas** necesarias para revisar la información de las planillas» y el flujo a→f incluía revisión (d), autorización (e) y cierre; el negocio lo ratificó completo el 2026-07-14. Los tres sub-requerimientos de hoy son ese segmento, ahora detallado por el cliente: estado→RF-019, revisión→RF-010/RF-017/RF-019, exports→RF-020, autorización/cierre→RF-014/RF-015 (numeración de REQ-012).
2. **Excel y CSV ya existen literalmente en el molde de la casa.** `ReportExportFileWriter` emite `text/csv` y el OOXML de Excel (`ReportExportFileWriter.cs:29-30`), con rate-limit de export y 413 por tamaño vía `ReportExportDeliveryService` — es el mismo exportador que ya sirven las bandejas de REQ-004…011. El requerimiento no exige construir un exportador: exige **conectar** el que existe a la corrida (plan §3.7, PR-7).
3. **«La información más importante de cada planilla» ya está persistida en la cabecera de la corrida** (plan §1.4): snapshots de nómina y periodo (código/nombre/label/fechas/pago), estado, quién/cuándo generó/autorizó/cerró, contador de regeneraciones, `EmployeeCount` y los totales (`TotalIncome`/`TotalDeductions`/`TotalEmployerCost`/`TotalNet`), moneda y advertencias. El listado no calcula nada al vuelo.
4. **«Confirmar que ingresos, descuentos y acciones se aplicaron correctamente» tiene doble respaldo ya ratificado**: (a) trazabilidad línea→registro fuente (RN-03 de REQ-012: `SourceModule` + `SourceReferencePublicId` — cuota, eventual, jornada HE, tiempo no trabajado, amonestación, incapacidad, ley, patronal); (b) los **tests de cuadre** corrida ≡ insumo/export del mismo filtro (criterio de aceptación 2 de REQ-012) — la revisión humana verifica lo que la suite ya garantiza mecánicamente.
5. **Dos matices de redacción se elevaron como P-01/P-02 y el negocio los RATIFICÓ tal como se recomendó (2026-07-15):** (a) el texto funde «autorizar» y «cerrar la nómina» en una sola acción → **se confirman los DOS pasos** del modelo ratificado (`AUTORIZADA` solo-lectura → `CERRADA` terminal) con el envío (boletas/conciliación de la Ola C) entre ambos; (b) «ya no se deberá permitir hacer cambios en los cálculos» → **se confirma la devolución** `AUTORIZADA→GENERADA` con motivo (Flujo 5 de REQ-012) como ÚNICA reapertura pre-cierre. El efecto pedido queda cumplido: desde `AUTORIZADA` nadie edita cálculos; la única reapertura es una decisión explícita, auditada y con motivo; `CERRADA` no tiene vuelta.
6. **Registro como REQ-013 sin PRs propios**: se entrega con REQ-012 (PR-6 autorización/cierre · PR-7 bandeja/exports · PR-8 guía FE). Su checklist en el backlog es de **correspondencia** (verificar al cerrar esos PRs que los tres sub-requerimientos quedaron cubiertos), no de construcción. La «Próxima acción» del programa sigue siendo **PR-1 de REQ-012, sin bloqueos**.

### Trazabilidad frase a frase del levantamiento

| Frase del requerimiento | Cobertura (REQ-012 ratificado + plan técnico) |
|---|---|
| «visualizar el estado de la generación de planillas… en diferentes periodos» | Bandeja de corridas: `POST companies/{id}/payroll-runs/query` con filtros nómina/periodo/estado/año + `StatusCounts` span-todos (RF-019 · plan §3.7 · **PR-7**); estados como catálogo país `payroll-run-statuses` (`GENERADA/-9970 · AUTORIZADA/-9971 · CERRADA/-9972 · ANULADA/-9973`) |
| «elegir de un listado con la información más importante de cada planilla» | Cabecera persistida de `PayrollRun` (plan §1.4): nómina, periodo, estado, actores/fechas, totales, moneda, warnings → seleccionar = `GET …/payroll-runs/{id}` (detalle) |
| «revisar los datos de la planilla generada» | Detalle + drill por empleado: `GET …/{id}` y `GET …/{id}/employees/{fileId}` con líneas (concepto, clase, unidades, base, calculado, override, final, incluida, fuente) — RF-010/RF-019 · plan §3.6 · **PR-6/PR-7** |
| «confirmar que los ingresos, descuentos y acciones se han aplicado correctamente» | Toda línea traza a su registro fuente (RN-03: `SourceModule`/`SourceReferencePublicId`); «acciones» = amonestaciones aplicadas, tiempos no trabajados, incapacidades (P-10 de REQ-012); tests de cuadre corrida≡insumo por los 5 pools |
| «exportar… a Microsoft Excel y… CSV» | RF-020 + RF-017: `…/payroll-runs/export` (bandeja) y `…/{id}/lines/export` (impresión de planilla con totales por concepto/CC) en xlsx/csv/json — molde `ReportExportDeliveryService` + `ReportExportFileWriter` (`text/csv` y OOXML **verificados en código**) · **PR-7** |
| «Autorización… sobre las planillas generadas» | RF-014: `PATCH …/{id}/authorization` en controller dedicado; anti-self doble; permiso `AuthorizePayrollRuns` **sin Admin** (D-11) · **PR-6** |
| «para cerrar la nómina» | RF-015: `PATCH …/{id}/closure` — corrida `CERRADA` + periodo `CERRADO` en la misma transacción; **P-01 RATIFICADA (2026-07-15): dos pasos** — autorizar y cerrar son acciones separadas, con el envío entre ambas |
| «ya no se deberá permitir hacer cambios en los cálculos» | Sets de estado del plan §1.5: `Editable={GENERADA}` — desde `AUTORIZADA` toda mutación de líneas/recálculo/regeneración → 422 `PAYROLL_RUN_STATE_RULE_VIOLATION`; **P-02 RATIFICADA (2026-07-15): la devolución auditada con motivo es la única excepción pre-cierre** |

---

## 1. Resumen del producto o requerimiento

Exponer al usuario la **cara de consulta y decisión** del motor de planillas (REQ-012): (1) un **listado del estado de generación** de las planillas de todos los periodos, con la información clave de cada una y navegación al detalle; (2) la **revisión** de una planilla generada — verificar por empleado que cada ingreso, descuento y acción de personal se aplicó correctamente, con **exportación a Microsoft Excel (xlsx) y CSV**; y (3) la **autorización**, que congela los cálculos y conduce al **cierre** de la nómina del periodo.

**Problema que resuelve:** sin estas consultas, la corrida del motor sería una caja negra — no habría forma operativa de saber qué planillas existen y en qué estado están, de verificar el cálculo antes de pagar, ni de formalizar el punto de no retorno (autorización/cierre) que protege las cifras pagadas. **Nota estructural:** todo esto ya fue levantado, ratificado y planificado dentro de REQ-012; este documento confirma la cobertura y precisa tres puntos finos (§17).

## 2. Objetivos del negocio

1. **Visibilidad del ciclo de pago**: saber en todo momento qué planillas se generaron, en qué periodos, en qué estado están y quién actuó sobre ellas.
2. **Verificación antes de pagar**: confirmar contra las fuentes (pools, acciones, ley) que el cálculo es correcto — con la misma información exportable para verificación externa (contador, Tesorería, piloto en paralelo contra la nómina externa).
3. **Control formal del cierre**: separación de funciones (quien genera no autoriza), inmutabilidad de los cálculos desde la autorización y cierre terminal defendible ante auditoría.
4. **Cero fricción de herramienta**: Excel/CSV como formato puente con la operación actual (los mismos formatos de los insumos que hoy alimentan la nómina externa).

## 3. Alcance funcional

Tres bloques, todos dentro de REQ-012 F1 (Olas B y C):

- **A. Estado de generación** — bandeja corporativa de corridas con filtros (nómina, periodo, estado, año), conteos por estado sobre el conjunto completo, y selección hacia el detalle (RF-001, RF-002).
- **B. Revisión + exports** — detalle de cabecera y drill por empleado/línea con trazabilidad a la fuente; export del listado y del detalle de líneas (impresión de planilla) a **xlsx y csv** (+json del molde) (RF-002, RF-003).
- **C. Autorización y cierre** — autorizar (congela cálculos), devolver con motivo (única reapertura pre-cierre), cerrar (terminal; periodo `CERRADO`) (RF-004…RF-006).

**Los ajustes de revisión** (override por línea, incluir/excluir, recálculo selectivo, regeneración) son parte de REQ-012 (RF-010…RF-012) y quedan disponibles al revisor; este levantamiento solo exige la **lectura** de verificación — no agrega ni recorta nada ahí.

## 4. Fuera de alcance

Idéntico al F2 mapeado de REQ-012 — este levantamiento no adelanta nada de eso:

- Programación de la generación a fecha/hora (P-14 de REQ-012, diferida por el negocio).
- Envío de boletas por correo y notificaciones (sin proveedor real de email).
- Autorización multi-nivel y delegaciones (F1 = una decisión).
- Archivo bancario con formato propietario (F1 = reporte de conciliación).
- Módulo de asistencia/marcación; contabilización/ERP; multi-moneda con tipo de cambio.
- Reapertura de planillas `CERRADAS` (correcciones → periodo siguiente, RN-12 de REQ-012).
- Cambios al motor de cálculo, al motor de liquidación o al ledger externo `PersonnelFilePayrollTransaction`.

## 5. Actores o usuarios involucrados

| Actor | Rol en este requerimiento |
|---|---|
| **Analista de planilla** (`ManagePayrollRuns`) | Consulta el estado, revisa el detalle, exporta, cierra la nómina tras el envío |
| **Autorizador de planilla** (`AuthorizePayrollRuns`, sin Admin) | Autoriza o devuelve con motivo; rol distinto de quien generó (anti-self) |
| **Gerencia RRHH / consulta** (`ViewPayrollRuns`) | Visualiza bandeja, detalle y exports en solo lectura |
| **Tesorería / Finanzas** | Consume los exports y la conciliación para ejecutar/autorizar el pago |
| **Contador (externo)** | Verifica cifras vía exports Excel/CSV (piloto en paralelo del checklist de REQ-012) |

## 6. Requerimientos funcionales

> Numeración local RF-001…RF-006; cada uno declara su **cobertura** en REQ-012 (donde ya está ratificado y planificado). Prioridad Alta en todos: son la cara operativa del motor.

### RF-001 - Listado del estado de generación de planillas

**Descripción:** Bandeja corporativa paginada de todas las corridas de planilla generadas en los diferentes periodos, con filtros por nómina, periodo, estado y año, conteos por estado (`StatusCounts`) sobre el conjunto filtrado completo, y columnas clave: nómina (código/nombre), periodo (label + rango + fecha de pago), estado, generada por/el, regeneraciones, autorizada por/el, cerrada por/el, # empleados, total ingresos, total descuentos, costo patronal, neto, moneda y advertencias.

**Reglas de negocio:**
- `StatusCounts` abarca TODOS los estados del filtro, no solo la página (molde de bandejas de la casa).
- Las corridas `ANULADA` permanecen visibles como histórico (una sola ACTIVA por nómina+periodo — índice único parcial de REQ-012).
- Solo lectura bajo `ViewPayrollRuns` con gate fail-closed.
- Los totales del listado son los persistidos en la cabecera (no se recalculan al listar).

**Criterios de aceptación:** paginación estándar; conteos ≡ Σ de estados; seleccionar un elemento abre el detalle (RF-002); filtros combinables.

**Prioridad:** Alta.
**Dependencias:** corridas existentes (REQ-012 PR-4/PR-5).
**Cobertura:** REQ-012 RF-019 · plan §3.7 (`POST companies/{id}/payroll-runs/query`) · **PR-7**.

### RF-002 - Revisión del detalle de la planilla generada

**Descripción:** Detalle de una corrida: cabecera completa + historial de acciones (auditoría) + drill por empleado con sus líneas — concepto (código/nombre), clase (`Ingreso`/`Descuento`/`PagoPatronal`), unidades, base, monto calculado, override (si hubo, con nota y actor), monto final, incluida sí/no, **módulo fuente y referencia al registro origen**, advertencias por línea — para confirmar que los ingresos, descuentos y acciones del periodo se aplicaron correctamente.

**Reglas de negocio:**
- Toda línea traza a su fuente (RN-03 de REQ-012): cuotas cíclicas/eventuales de ingreso y descuento, jornadas de horas extras valoradas, tiempos no trabajados, amonestaciones aplicadas, incapacidades, ley (ISSS/AFP/Renta) y patronales.
- Los totales de cabecera ≡ Σ de líneas incluidas (verificado por suite).
- La revisión es lectura; los ajustes (override/excluir/recalcular/regenerar) son los ya ratificados en REQ-012 RF-010…RF-012 y solo proceden en estado `GENERADA`.

**Criterios de aceptación:** cada línea expone `sourceModule` + `sourceReferencePublicId` navegable; empleado con override lo muestra con nota/actor; advertencias visibles a nivel corrida, empleado y línea.

**Prioridad:** Alta.
**Dependencias:** RF-001.
**Cobertura:** REQ-012 RF-010 (lectura del detalle) + RF-019 (drill) · plan §3.6 (`GET …/{id}`, `GET …/{id}/employees/{fileId}`) · **PR-6/PR-7**.

### RF-003 - Export a Microsoft Excel (xlsx) y CSV

**Descripción:** Exportar (a) el **listado** de planillas (bandeja filtrada) y (b) el **detalle de líneas** de una planilla (la «impresión de planilla»: filas empleado×concepto + totales por concepto y centro de costo) en formato Excel (xlsx) y Separado por Coma (csv); json disponible por el molde.

**Reglas de negocio:**
- Molde de la casa: `ReportExportDeliveryService` + `ReportExportFileWriter` — `text/csv` y OOXML de Excel nativos (**verificado**: `ReportExportFileWriter.cs:29-30`); `[EnableRateLimiting(Export)]`; respuesta 413 si excede el tamaño máximo.
- Cabeceras de columnas en español (convención de todos los exports del repo).
- Nota FE heredada (hallazgo REQ-008): el export **json** serializa nombres en PascalCase.

**Criterios de aceptación:** ambos formatos descargan para bandeja y para líneas de una corrida; las cifras del export ≡ pantalla ≡ totales persistidos; rate-limit aplicado.

**Prioridad:** Alta.
**Dependencias:** RF-001/RF-002.
**Cobertura:** REQ-012 RF-020 + RF-017 · plan §3.7 (`…/export`, `…/{id}/lines/export`) · **PR-7**.

### RF-004 - Autorizar planilla

**Descripción:** Acción sobre una corrida `GENERADA` que la pasa a `AUTORIZADA`. Desde ese momento **los cálculos quedan congelados**: sin overrides, sin incluir/excluir líneas, sin recálculo selectivo, sin regeneración.

**Reglas de negocio:**
- **Anti-self doble**: quien generó/regeneró por última vez no puede autorizar (403 `PAYROLL_RUN_SELF_AUTHORIZATION_FORBIDDEN`).
- Permiso `AuthorizePayrollRuns` **sin Admin implícito** (la exclusión vive en la policy — lección REQ-007); controller dedicado (`AuthorizationPolicySet` es class-only).
- Concurrencia con If-Match; solo desde `GENERADA` (422 `PAYROLL_RUN_STATE_RULE_VIOLATION` en otro estado).
- Auditoría `PAYROLL_RUN_AUTHORIZED` con actor y timestamp persistidos en la cabecera.

**Criterios de aceptación:** el generador intenta autorizar → 403; un tercero con permiso → `AUTORIZADA`; cualquier mutación de líneas sobre `AUTORIZADA` → 422; Admin sin el grant explícito → 403.

**Prioridad:** Alta.
**Dependencias:** RF-002 (revisión previa).
**Cobertura:** REQ-012 RF-014 · D-11 · plan §3.6 (`PATCH …/{id}/authorization`) · **PR-6**.

### RF-005 - Cerrar la nómina

**Descripción:** Cierre de la corrida `AUTORIZADA` (tras el envío — boletas/conciliación de la Ola C): la corrida pasa a `CERRADA` (terminal), el **periodo** pasa a `CERRADO` en la misma transacción y los pools aplicados quedan firmes.

**Reglas de negocio:**
- Solo desde `AUTORIZADA`; `CERRADA` es terminal en F1 (correcciones posteriores → periodo siguiente, RN-12 de REQ-012).
- La anulación total de la corrida solo procede ANTES de cerrar (revierte pools y libera el periodo).
- Generar de nuevo sobre un periodo cerrado → 422.

**Criterios de aceptación:** cerrar → periodo `CERRADO`; intento de generación posterior → 422; intento de devolución sobre `CERRADA` → 422.

**Prioridad:** Alta.
**Dependencias:** RF-004. **P-01 RATIFICADA (2026-07-15): dos pasos** — el cierre es una acción separada, posterior al envío.
**Cobertura:** REQ-012 RF-015 · plan §3.6 (`PATCH …/{id}/closure`) · **PR-6**.

### RF-006 - Devolución controlada (única reapertura pre-cierre)

**Descripción:** El autorizador devuelve una corrida `AUTORIZADA` a `GENERADA` con **motivo obligatorio** — la única vía de «cambio» después de autorizar, explícita y auditada, pensada para errores detectados antes de pagar (Flujo 5 de REQ-012).

**Reglas de negocio:** motivo requerido (422 `PAYROLL_RUN_RETURN_REASON_REQUIRED`); mismo permiso `AuthorizePayrollRuns`; auditoría `PAYROLL_RUN_RETURNED`; `CERRADA` no admite devolución.

**Criterios de aceptación:** devolver con motivo reabre los ajustes (estado `GENERADA`); sin motivo → 422; el motivo queda visible en el detalle/historial.

**Prioridad:** Alta.
**Dependencias:** RF-004. **P-02 RATIFICADA (2026-07-15): confirmada tal cual** — única reapertura pre-cierre, explícita y auditada.
**Cobertura:** REQ-012 RF-014 (devolver) · plan §3.6 (`PATCH …/{id}/return`) · **PR-6**.

## 7. Requerimientos no funcionales

| Categoría | Requisito |
|---|---|
| Seguridad | Gates fail-closed por handler (`ViewPayrollRuns` para toda lectura); `AuthorizePayrollRuns` sin Admin implícito; anti-self en la decisión; multi-tenant por `TenantId`; If-Match en las tres mutaciones (autorizar/devolver/cerrar); sin montos en logs |
| Rendimiento | Listado sobre totales **persistidos** (cero agregación al vuelo); detalle paginado por empleado; exports con rate-limit `Export` y 413 por tamaño |
| Auditoría | `PAYROLL_RUN_AUTHORIZED/RETURNED/CLOSED` + actores/fechas en cabecera; historial de acciones consultable (D-14 de REQ-012: sin asientos de journal por empleado — por volumen) |
| Exactitud | Lo que se muestra/exporta ≡ lo persistido ≡ Σ líneas incluidas (tests de cuadre de REQ-012) |
| Usabilidad | Errores 422/403 bilingües EN/ES con códigos estables; estados como catálogo país (`payroll-run-statuses`) para render del FE; advertencias diferenciadas de errores |
| Compatibilidad | Convenciones del repo: `api/v1`, enums string, `XxxId`→`xxxPublicId`, If-Match (falta→400, stale→409), openapi.yaml a mano + guardrails |
| Mantenibilidad | **Cero piezas nuevas**: bandeja/exports/decisión son moldes ya certificados (bandejas REQ-004…011, exports `ReportExportDeliveryService`, decisión molde settlements/retirement) |

## 8. Historias de usuario

### HU-001 - Ver el estado de las planillas
Como **gerente de RRHH**, quiero **ver el listado de planillas de todos los periodos con su estado y totales**, para **saber qué está generado, qué falta autorizar y qué ya cerró**.
**Criterios:** Dado el filtro por año y nómina, cuando consulto la bandeja, entonces veo cada corrida con estado, actores, # empleados y totales, y los conteos por estado corresponden al filtro completo.

### HU-002 - Revisar una planilla antes de autorizar
Como **analista de planilla**, quiero **abrir el detalle de un empleado y ver de dónde sale cada línea**, para **confirmar que sus ingresos, descuentos y acciones del periodo se aplicaron correctamente**.
**Criterios:** Dada una corrida `GENERADA`, cuando abro el drill de un empleado, entonces cada línea muestra concepto, clase, montos y la referencia a su registro fuente (cuota, jornada HE, tiempo no trabajado, amonestación, incapacidad o ley).

### HU-003 - Exportar a Excel y CSV
Como **analista de planilla / Tesorería**, quiero **exportar el listado y el detalle de líneas a Excel y CSV**, para **verificar y compartir la información fuera del sistema** (incluido el piloto en paralelo contra la nómina externa).
**Criterios:** Dada una corrida, cuando exporto sus líneas en xlsx y csv, entonces ambos descargan con cabeceras en español y las cifras cuadran con la pantalla.

### HU-004 - Autorizar y congelar
Como **autorizador de planilla**, quiero **autorizar la corrida revisada**, para **cerrar la nómina con separación de funciones y que nadie altere los cálculos después**.
**Criterios:** Dado que yo generé la corrida, cuando intento autorizarla, entonces 403; dado un tercero con permiso, cuando autoriza, entonces `AUTORIZADA` y toda mutación de cálculo posterior responde 422.

### HU-005 - Devolver con motivo
Como **autorizador**, quiero **devolver una planilla autorizada indicando el motivo**, para **corregir un error detectado antes del pago sin anular todo el trabajo**.
**Criterios:** Dada una corrida `AUTORIZADA`, cuando la devuelvo con motivo, entonces vuelve a `GENERADA`, el motivo queda auditado y los ajustes se reabren.

### HU-006 - Cerrar la nómina del periodo
Como **analista de planilla**, quiero **cerrar la corrida autorizada tras emitir boletas y conciliación**, para **dejar el periodo `CERRADO` y las cifras firmes**.
**Criterios:** Dada una corrida `AUTORIZADA`, cuando la cierro, entonces la corrida queda `CERRADA`, el periodo `CERRADO`, y generar de nuevo ese periodo responde 422.

## 9. Reglas de negocio (consolidadas)

- **RN-01** Una corrida ACTIVA por nómina+periodo; el listado muestra la vigente y las anuladas como histórico (REQ-012 RN-01).
- **RN-02** Sets de estado (plan §1.5): `Editable={GENERADA}` · `Authorizable={GENERADA}` · `Returnable={AUTORIZADA}` · `Closable={AUTORIZADA}` · `Terminal={CERRADA, ANULADA}`.
- **RN-03** Desde `AUTORIZADA` **ningún cambio de cálculo es posible** (overrides, exclusiones, recálculo y regeneración → 422); la única reapertura pre-cierre es la devolución explícita con motivo del autorizador (P-02).
- **RN-04** `CERRADA` es terminal: sin devolución, sin anulación, sin regeneración; correcciones → periodo siguiente (REQ-012 RN-12).
- **RN-05** Anti-self doble en la autorización; Admin no autoriza sin el grant explícito (REQ-012 RN-11).
- **RN-06** Toda línea revisable traza a su registro fuente; toda decisión traza a su actor/fecha/motivo (REQ-012 RN-03 + D-14).
- **RN-07** Los totales mostrados/exportados son los persistidos y ≡ Σ de líneas incluidas; los exports cuadran con los insumos del mismo filtro (tests de cuadre).
- **RN-08** El cierre de la corrida cierra el **periodo** en la misma transacción; los pools aplicados con origen `MOTOR` quedan firmes (REQ-012 D-09).

## 10. Flujos principales

### Flujo 1 — Consulta de estado
1. El usuario entra a la bandeja de planillas. 2. Filtra por año/nómina/estado. 3. El sistema lista las corridas con su información clave y conteos por estado. 4. El usuario selecciona una planilla. 5. El sistema muestra el detalle (cabecera + historial + empleados).

### Flujo 2 — Revisión con verificación
1. Corrida `GENERADA`. 2. El analista abre el drill de un empleado. 3. Confirma cada línea contra su fuente (referencia navegable). 4. Exporta las líneas a Excel/CSV para verificación externa. 5. (Si hay ajustes, aplican los mecanismos de REQ-012: override/excluir/recalcular/regenerar.)

### Flujo 3 — Autorización y cierre
1. Corrida revisada en `GENERADA`. 2. Un tercero con `AuthorizePayrollRuns` autoriza → `AUTORIZADA` (cálculos congelados). 3. Se emiten boletas/impresión/conciliación (REQ-012 Ola C). 4. Se ejecuta el pago. 5. El analista cierra → corrida `CERRADA` + periodo `CERRADO`.

### Flujo 4 — Devolución
1. Corrida `AUTORIZADA`. 2. Tesorería detecta un error antes de pagar. 3. El autorizador devuelve con motivo → `GENERADA`. 4. Ajuste/recálculo. 5. Re-autorización por tercero.

## 11. Flujos alternativos y excepciones

| Escenario | Comportamiento |
|---|---|
| Autorizar quien generó/regeneró por última vez | 403 `PAYROLL_RUN_SELF_AUTHORIZATION_FORBIDDEN` |
| Autorizar/devolver/cerrar en estado indebido | 422 `PAYROLL_RUN_STATE_RULE_VIOLATION` |
| Editar línea / recalcular / regenerar sobre `AUTORIZADA` o `CERRADA` | 422 (solo `GENERADA` es editable) |
| Devolver sin motivo | 422 `PAYROLL_RUN_RETURN_REASON_REQUIRED` |
| Cerrar sin autorizar | 422 (solo `AUTORIZADA` cierra) |
| If-Match ausente / obsoleto en la mutación | 400 / 409 (convención del repo) |
| Usuario sin `ViewPayrollRuns` consulta bandeja/detalle/export | 403 (gate fail-closed) |
| Export que excede el tamaño máximo | 413 (molde `ReportExportDeliveryService`) |
| Corrida `ANULADA` seleccionada del listado | Detalle visible (histórico); sin acciones disponibles |
| Generar sobre periodo `CERRADO` | 422 (el cierre es del periodo, no solo de la corrida) |

## 12. Datos requeridos

**Este requerimiento no modela datos nuevos — cero entidades y cero migraciones adicionales.** Consume el modelo M4 de REQ-012 (plan §1.4: `payroll_runs` + `payroll_run_lines`) y sus catálogos de estado (§1.5). Se listan los campos que las consultas **exponen**:

### Vista: Listado (cabecera de `PayrollRun`)

| Campo | Tipo | Descripción |
|---|---|---|
| payrollDefinitionCode / Name · payrollTypeCode | Texto | Nómina (snapshot) y su tipo |
| periodLabel · periodStartDate/EndDate · paymentDate | Texto/Fechas | Periodo (snapshot) y fecha de pago |
| statusCode | Catálogo `payroll-run-statuses` | `GENERADA/AUTORIZADA/CERRADA/ANULADA` |
| generatedBy/Utc · regeneratedCount · authorizedBy/Utc · returnReason · closedBy/Utc · annulledBy/Utc/Reason | Guid?/fechas/texto | Trazabilidad de acciones |
| employeeCount · totalIncome · totalDeductions · totalEmployerCost · totalNet · currencyCode | Int/decimal/texto | «La información más importante» — persistida |
| warningsJson | jsonb | Advertencias de la corrida |

### Vista: Detalle de revisión (línea de `PayrollRunLine`)

| Campo | Tipo | Descripción |
|---|---|---|
| employeePublicId/Name/Code · assignedPositionPublicId · costCenterName | Guid/texto | Empleado × plaza |
| conceptCode/Name · lineClass | Texto / `Ingreso,Descuento,PagoPatronal` | Concepto y clase EXPLÍCITA |
| units · baseAmount · calculatedAmount · overrideAmount · overrideNote · adjustedBy · finalAmount (derivado) · isIncluded | Decimal/texto/bool | Cálculo + ajuste + inclusión |
| sourceModule · sourceReferencePublicId | Texto/Guid? | **La trazabilidad que permite «confirmar»**: SALARIO, RECURRING_INCOME, ONE_TIME_INCOME, OVERTIME, RECURRING_DEDUCTION, ONE_TIME_DEDUCTION, NOT_WORKED_TIME, INCAPACITY, DISCIPLINARY, LEY_*, PATRONAL_* |
| warningCodesJson · currencyCode | jsonb/texto | Advertencias por línea; moneda snapshot |

### Exports

Bandeja (`…/payroll-runs/export`) y líneas por corrida (`…/{id}/lines/export`, con totales por concepto/centro de costo) en **xlsx · csv · json**; cabeceras en español.

## 13. Integraciones necesarias

| Integración | Estado |
|---|---|
| Exportador xlsx/csv/json (`ReportExportDeliveryService` + `ReportExportFileWriter`) | **Existe** — se conecta (verificado en código) |
| Motor/corrida de REQ-012 (fuente de todo lo consultado) | En plan (PR-4/PR-5) — dependencia dura |
| Auditoría (`AuditEventTypes` de corrida) | Molde existente — eventos definidos en REQ-012 D-14 |
| Correo, banca (archivo), ERP | Fuera de alcance (F2 de REQ-012) |

## 14. Roles y permisos

| Rol/Permiso | Permisos | Restricciones |
|---|---|---|
| `PersonnelFiles.ViewPayrollRuns` | Bandeja, detalle, drill, exports (lectura) | No ajusta, no decide, no cierra |
| `PersonnelFiles.ManagePayrollRuns` | Lo anterior + cerrar la nómina (y los ajustes/generación de REQ-012) | **No autoriza ni devuelve** |
| `PersonnelFiles.AuthorizePayrollRuns` | Autorizar / devolver con motivo | **Sin Admin implícito** (exclusión en policy); anti-self doble |
| Empleado | — (sin autoservicio en F1; boleta vía RRHH) | Portal = F2 de REQ-012 |

## 15. Criterios de aceptación generales

1. Bandeja con filtros + `StatusCounts` span-todos + paginación estándar, y selección → detalle completo.
2. Cada línea del detalle traza a su registro fuente; totales de cabecera ≡ Σ líneas incluidas.
3. Exports **xlsx y csv** de bandeja y de líneas descargan, cuadran con pantalla y respetan rate-limit/413.
4. Inmutabilidad probada: toda mutación de cálculo sobre `AUTORIZADA`/`CERRADA` → 422; sobre `CERRADA` tampoco procede devolución ni anulación.
5. Anti-self probado (403) y Admin sin grant explícito no autoriza.
6. Cerrar → corrida `CERRADA` + periodo `CERRADO` en la misma transacción; generación posterior sobre ese periodo → 422.
7. Devolución con motivo reabre y queda auditada; sin motivo → 422.
8. openapi.yaml sin drift y guía FE publicada (con REQ-012 PR-8); errores bilingües con códigos estables.

## 16. Riesgos, supuestos y dependencias

### Riesgos
- ~~Expectativa de «un solo acto»~~ **RESUELTO en la ratificación (P-01, 2026-07-15)**: el negocio confirmó los **dos pasos** (autorizar → cerrar); el efecto «sin cambios» rige desde `AUTORIZADA`.
- ~~Expectativa de inmutabilidad absoluta~~ **RESUELTO en la ratificación (P-02, 2026-07-15)**: la **devolución con motivo** queda confirmada tal cual como única reapertura pre-cierre.
- **Riesgo técnico: bajo.** Todo es molde certificado (bandejas, exports, decisión anti-self); el riesgo real del programa vive en el motor (REQ-012 Ola B), no aquí.

### Supuestos
- Los exports pedidos aplican tanto al **listado** como al **detalle de líneas** (ambos ya en el plan §3.7; costo marginal cero).
- «Acciones» en «ingresos, descuentos y acciones» = acciones de personal con efecto en planilla (amonestaciones, tiempos no trabajados, incapacidades) — todas entran como líneas trazadas (P-10 de REQ-012).
- Sin producción a la fecha (2026-07-15): sin necesidades de migración de datos; la estabilidad del contrato FE sí aplica.
- La numeración/orden de columnas del listado la decide el FE sobre la cabecera expuesta (**P-03 ratificada: se arranca con la cabecera persistida**; cualquier columna extra sería aditiva).

### Dependencias
- **REQ-012 PR-4/PR-5** (existencia de corridas y líneas) — sin motor no hay nada que consultar; **PR-6/PR-7** son la entrega de este REQ; **PR-8** publica la guía FE.
- ~~Ratificación de P-01…P-03 antes de PR-6~~ **CUMPLIDA (2026-07-15)** — este REQ ya no tiene condiciones de negocio pendientes.
- Permisos asignados a roles en despliegue (`AuthorizePayrollRuns` a un rol autorizador distinto — Admin NO lo cubre; checklist §8 del plan de REQ-012).

## 17. Preguntas al negocio — **P-01…P-03 RESPONDIDAS (ratificación 2026-07-15)**

Las tres recomendaciones fueron **aceptadas tal cual** — el modelo ratificado de REQ-012 queda sin cambios y este REQ sin condiciones de negocio pendientes.

| # | Ámbito | Pregunta | Recomendación | **Respuesta del negocio (2026-07-15)** |
|---|---|---|---|---|
| **P-01** | Autorizar vs cerrar | El levantamiento dice que la autorización sirve «para cerrar la nómina». El modelo ratificado de REQ-012 tiene **dos pasos**: `AUTORIZADA` (cálculos congelados, solo lectura) y luego `CERRADA` (terminal), con el envío (boletas, impresión, conciliación para Tesorería) entre ambos. ¿Se mantienen los dos pasos, o «autorizar» debe dejar la planilla cerrada en el mismo acto? | **Mantener dos pasos** (ya ratificado): el efecto pedido — no más cambios — rige desde `AUTORIZADA`; el paso de cierre confirma que el pago se ejecutó y cierra el periodo | ✔ **«Mantener 2 pasos»** — aceptada tal cual |
| **P-02** | Inmutabilidad | «Luego de esta acción ya no se deberá permitir hacer cambios en los cálculos.» REQ-012 ratificó además la **devolución** `AUTORIZADA→GENERADA` con motivo obligatorio y auditada (Flujo 5: error detectado antes de pagar). ¿Se confirma que esa devolución explícita sigue siendo la única reapertura pre-cierre, y que `CERRADA` no tiene vuelta alguna? | **Confirmar tal cual** (ya ratificado): ningún cambio directo tras autorizar; la devolución es una decisión formal del autorizador, con motivo y rastro | ✔ **«Confirmar tal cual»** — la devolución con motivo es la única reapertura pre-cierre; `CERRADA` sin vuelta |
| **P-03** | Listado | «La información más importante de cada planilla»: la cabecera ya persiste nómina, periodo (con fechas y fecha de pago), estado, actores/fechas de cada acción, # empleados, totales (ingresos/descuentos/patronal/neto), moneda y advertencias. ¿Falta alguna columna imprescindible para la operación? | **Arrancar con la cabecera persistida** — cubre el pedido; cualquier columna extra es aditiva y el detalle/export ya expone el resto | ✔ **«Arrancar con la cabecera persistida»** — columnas extra = aditivas si la operación las pide |

## 18. Recomendaciones del Analista de Negocio

1. **No abrir desarrollo propio.** Registrar como REQ-013 con entrega vía REQ-012 (PR-6/PR-7/PR-8) y usar este documento como **checklist de correspondencia** al cerrar esos PRs (verificar que los tres sub-requerimientos y sus criterios §15 quedaron cubiertos).
2. ~~Ratificar P-01…P-03 antes de PR-6~~ **HECHO (2026-07-15, las 3 aceptadas tal cual)** — este REQ ya no tiene condiciones de negocio pendientes; la «Próxima acción» del programa (**PR-1 de REQ-012**) sigue sin bloqueos.
3. ~~Presentar al negocio la máquina de estados al ratificar P-01/P-02~~ **HECHO en la ratificación** — el matiz autorizar≠cerrar y la devolución quedaron confirmados sobre el diagrama (Anexo A.2).
4. **Usar los exports Excel/CSV como herramienta del piloto** en paralelo contra la nómina externa (checklist de despliegue de REQ-012): son el puente de verificación con la operación actual, además de un requisito del cliente.
5. **No prometer inmutabilidad absoluta en la comunicación** al usuario final: comunicar «desde la autorización nadie puede modificar cálculos; solo el autorizador puede devolver, con motivo y quedando registrado» — es más fuerte ante auditoría que un candado sin salida.

---

## Anexo A — Correspondencia y referencias

### A.1 Mapa de entrega (RF locales → REQ-012)

| RF local | REQ-012 | Plan técnico | PR |
|---|---|---|---|
| RF-001 Listado de estado | RF-019 | §3.7 `payroll-runs/query` + StatusCounts | PR-7 |
| RF-002 Revisión del detalle | RF-010 (lectura) + RF-019 (drill) | §3.6 detalle + empleados | PR-6/PR-7 |
| RF-003 Export Excel/CSV | RF-020 + RF-017 | §3.7 `/export` + `/{id}/lines/export` | PR-7 |
| RF-004 Autorizar | RF-014 | §3.6 controller dedicado + anti-self | PR-6 |
| RF-005 Cerrar | RF-015 | §3.6 `closure` (corrida+periodo) | PR-6 |
| RF-006 Devolver | RF-014 (devolver) | §3.6 `return` con motivo | PR-6 |

### A.2 Máquina de estados (referencia: REQ-012 Anexo A.4)

```
Corrida:   (generar) → GENERADA ⇄ (regenerar/recalcular/ajustar)
           GENERADA → AUTORIZADA (anti-self)   AUTORIZADA → GENERADA (devolver, con motivo — P-02)
           AUTORIZADA → CERRADA (terminal — P-01)   GENERADA|AUTORIZADA → ANULADA (pre-cierre)
Periodo:   GENERADO → CERRADO (al cerrar su corrida) · ANULADO (sin corrida)
```

### A.3 Verificaciones de código de este análisis (2026-07-15, HEAD `597aa75`)

- `src/CLARIHR.Application/Features/Reports/ReportExportFileWriter.cs:29-30` — el exportador de la casa emite `text/csv` (CSV) y `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (Excel xlsx): **los dos formatos pedidos existen nativamente**.
- `src/CLARIHR.Api/Common/ReportExportDeliveryService.cs:15` — servicio de entrega con límite de tamaño (413) usado por todas las bandejas existentes.
- Todo lo demás (corrida, líneas, estados, decisión, cierre) es **modelo por construir de REQ-012** — verificado que NO existe aún (análisis de REQ-012, sección «Lo que NO existe», vigente a la fecha).
