# Análisis de negocio — Reportes legales de planilla: F-14, Planilla Única y Planilla Patronal

| | |
|---|---|
| **Tipo** | Análisis de negocio — alcance nuevo (candidato **REQ-016**) |
| **Módulo** | Planilla / Reportes legales y de cumplimiento |
| **Fecha** | 2026-07-16 |
| **Autor** | Analista de Negocio (asistido por IA) |
| **Estado** | **RATIFICADO por el negocio (2026-07-16/17)** — P-01…P-13 respondidas (§17), **sin preguntas de negocio pendientes**. 3 ajustes ampliaron el alcance original: P-02 (los 3 reportes deben **replicar la plantilla oficial exacta**), P-03 (la falta del perfil legal **patronal** bloquea generar nómina de la **empresa** — impacta REQ-012 ya certificado) y P-11 (la falta de NUP ISSS/cuenta AFP bloquea la nómina de **ese empleado** — mismo criterio, un nivel más abajo). **P-12 confirmó que el bloqueo excluye solo la línea de ese empleado** (el resto de la corrida procede con normalidad). **P-13 confirmó que NO habrá periodo de transición — el negocio acepta explícitamente interrumpir la operación** de empresas cuyos empleados no tengan NUP ISSS/cuenta AFP capturados antes del despliegue. Único pendiente real: **recibir del negocio el archivo de plantilla oficial de F-14/Planilla Única** (P-02) — es un insumo externo, no una decisión. |
| **Naturaleza del requerimiento** | Alcance **genuinamente nuevo** — no deriva de REQ-012…015 (verificado por búsqueda en los 29 análisis de `docs/business/` y en `docs/backlog-requerimientos.md`: F-14, Planilla Única y Planilla Patronal **nunca fueron mencionados** antes en este proyecto). **Reutiliza** el motor de cálculo y el modelo de datos de REQ-012 sin modificarlos — es una capa de **agregación + exportación**, no de cálculo. |
| **Documentos hermanos** | [`analisis-planilla-generacion.md`](analisis-planilla-generacion.md) (motor y modelo de datos fuente — REQ-012) · [`analisis-planilla-estado-revision-autorizacion.md`](analisis-planilla-estado-revision-autorizacion.md) (mecanismo de exportación ya certificado — REQ-013) · [`analisis-alcance-diferido-fases-siguientes.md`](analisis-alcance-diferido-fases-siguientes.md) (necesidad N7 — por qué la presentación electrónica ante DGII/ISSS queda fuera) · [`../backlog-requerimientos.md`](../backlog-requerimientos.md) |
| **Alcance geográfico** | El Salvador (ISSS/AFP/DGII son normativa local; ver D-01) |

> **Glosario rápido** (para lectores no contables): **DGII** = Dirección General de Impuestos Internos, del Ministerio de Hacienda. **F-14** = declaración mensual de pago a cuenta y retención de impuesto sobre la renta que el patrono presenta ante la DGII. **ISSS** = Instituto Salvadoreño del Seguro Social. **AFP** = Administradora de Fondos de Pensiones (la entidad privada que administra la pensión del empleado). **NUP** = Número Único Previsional (identifica al afiliado ante el ISSS). **NIT/DUI** = documentos de identificación tributaria y personal del país. El código interno `INCAF` referido más abajo es el nombre que el motor de planilla ya usa para una carga patronal adicional — este análisis no asume a qué obligación real corresponde exactamente; es una pregunta abierta menor si se necesita (ver §17).

---

## Contexto del cambio (requerimiento original)

> «Se requiere que con la Nómina se pueda crear el reporte F-14, El Reporte de Planilla única y un reporte de Planilla Patronal. Los reportes se podrán descargar en formato Excel. y se podrán visualizar por ciclos de nóminas pagados.»

**Tres reportes nuevos**: F-14, Planilla Única y Planilla Patronal, generados desde el módulo de Nómina ya construido (REQ-012…015).
**Un formato de salida**: Excel, exclusivamente — no se menciona PDF, impresión ni presentación electrónica.
**Un filtro de navegación**: por ciclo(s) de nómina ya **pagados** — es decir, corridas cerradas, no borradores en curso ni corridas pendientes de autorizar.

El pedido es deliberadamente breve (una oración por reporte). Este análisis interpreta cada reporte con base en la práctica de cumplimiento de nómina en El Salvador y dejará como **pregunta abierta explícita** (§17) todo detalle de formato exacto que no pueda confirmarse contra el propio texto del pedido, siguiendo la instrucción de no inventar información crítica.

---

## 0. Veredicto ejecutivo

1. **No hay cálculo nuevo que construir.** El motor de planilla (REQ-012) ya emite, línea a línea y por `ConceptCode`, todo lo que estos tres reportes necesitan como insumo: `RENTA`, `ISSS`, `AFP`, `ISSS_PATRONAL`, `AFP_PATRONAL`, `INCAF` (`PayrollCalculation.Rules.cs:19-29`). Este requerimiento es una capa de **agregación + exportación** sobre datos que ya existen, no una extensión del motor.
2. **Gap bloqueante #1 — identidad legal patronal — ALCANCE AMPLIADO en la ratificación.** `Company.cs` no tiene razón social, NIT patronal, número de registro patronal ante el ISSS, dirección fiscal ni actividad económica — **ningún** campo de estos existe hoy en el dominio (G-01). ~~Sin esto, el encabezado de los tres reportes no puede completarse con datos reales~~ **el negocio ratificó (P-03, 2026-07-16) algo más fuerte: sin este perfil, no se debe poder generar NINGUNA nómina de esa empresa** — el gate no es solo de estos 3 reportes, es de la generación de planilla misma (`PayrollRun.Create()`, REQ-012, ya certificado). Es el cambio de mayor impacto de esta ratificación: toca un módulo ya cerrado y desplegado-pendiente.
3. **Gap bloqueante #2 — identidad previsional del empleado — ALCANCE AMPLIADO en la segunda ratificación.** No existe número de afiliado ISSS (NUP) en ningún lugar del dominio, ni número de cuenta AFP (solo el código de la AFP a la que pertenece el empleado, `PersonnelFile.AfpCode`); NIT/DUI sí existen pero viven en una colección genérica de identificaciones sin garantía de que estén capturados (G-02/G-03/G-04). ~~Esto bloquea el detalle por empleado de Planilla Única y, parcialmente, de F-14~~ **el negocio ratificó (P-11, 2026-07-16) el mismo criterio que ya aplicó a nivel empresa en P-03: sin NUP ISSS/cuenta AFP, la nómina de ESE empleado no procede** — no es solo una advertencia en el reporte, es un gate en la generación misma, ahora a nivel de cada línea de empleado dentro del motor de REQ-012 (más profundo que el gate de RF-006, que solo corre una vez por corrida a nivel de empresa).
4. ~~Riesgo técnico de formato, a validar~~ **CONFIRMADO en la ratificación (P-02, 2026-07-16): los 3 reportes deben replicar la plantilla oficial exacta.** El exportador de la casa (`ReportExportFileWriter`) escribe una **sola hoja plana**, con columnas derivadas por reflexión sobre las propiedades del DTO de fila — no soporta encabezados fijos, celdas combinadas ni un layout posicional de varias secciones (G-05). Con el requisito de plantilla exacta ya ratificado, **el mecanismo actual NO alcanza tal cual**: se necesita una capacidad nueva de exportación (plantilla/posicional) antes de poder construir RF-001/002/003 con su layout final — y el sistema **todavía no tiene en su poder los archivos de plantilla oficial** de ninguno de los 3 reportes (nueva dependencia crítica, ver §16 y §18).
5. **Tensión de periodicidad sin resolver.** F-14 y muy probablemente Planilla Única son obligaciones **mensuales** ante Hacienda/ISSS; el motor de planilla corre por **ciclo** (mensual, quincenal o semanal, según cómo cada empresa configuró su nómina). Un mes con nómina quincenal contiene 2 corridas; uno con nómina semanal puede contener 4 o 5. El pedido dice literalmente «por ciclos... pagados», lo que podría no calzar con la periodicidad legal real de estos dos reportes (P-01). Buena noticia: la costura para consolidar por mes calendario ya existe en el modelo (`PayrollPeriodDefinition.Month`, campo contable 1-12 ya sembrado por REQ-012).
6. **Alcance genuinamente nuevo, verificado.** Se confirmó por búsqueda textual sobre los 29 análisis de `docs/business/` y sobre el backlog completo que F-14, Planilla Única y Planilla Patronal **nunca fueron mencionados** en este proyecto — a diferencia de REQ-013/014/015, que eran subconjuntos ya diseñados dentro de REQ-012, aquí no hay nada que "activar barato".
7. **El más barato de construir hoy, con los datos que YA existen, es Planilla Patronal.** Reutiliza directamente `TotalEmployerCost` de la cabecera de la corrida y las líneas `PagoPatronal` ya calculadas, sin depender de NUP ISSS ni de cuenta AFP. Es el candidato natural a piloto/MVP (§18) — y la ratificación (P-05) confirmó que su propósito es **control interno para validar lo que se paga al gobierno**, es decir, es el reporte que el propio negocio usará para revisar F-14/Planilla Única antes o después de presentarlos.
8. **RATIFICADO por el negocio 2026-07-16** — P-01…P-10 respondidas (tabla completa en §17). 8 de las 10 confirman la recomendación de este análisis tal cual (P-01, P-04, P-06, P-07, P-08, P-09, P-10, y P-05 como aclaración de propósito); **2 la superan en alcance** (P-02: plantilla oficial exacta, no detalle tabular; P-03: bloquea generación de nómina, no solo el reporte). Quedó **P-11** — un cabo suelto menor que este mismo análisis identificó al propagar P-03/P-04 (§17) — pendiente de una respuesta puntual antes de cerrar el plan técnico de RF-007.
9. **P-11 RESPONDIDA (2026-07-16): «debe bloquear la nómina, sin eso no procede».** El mismo criterio de P-03 (empresa) baja a nivel de empleado: sin NUP ISSS/cuenta AFP, ese empleado no debe procesarse en la corrida. Es el gate de mayor profundidad técnica de todo este análisis — corre dentro del cálculo por-empleado del motor de REQ-012, no solo en la creación de la corrida como el de RF-006.
10. **P-12 y P-13 RESPONDIDAS (2026-07-17): «bloquear solo la línea/inclusión de ese empleado»** (P-12 — el resto de la corrida procede con normalidad, confirma la recomendación) **y «sin periodo de transición, se acepta interrumpir la operación»** (P-13 — rechaza la recomendación de este análisis; el negocio decide asumir el riesgo de que empresas con empleados sin NUP ISSS/cuenta AFP no puedan generar nómina de esos empleados desde el día del despliegue). Esto convierte la captura de estos datos en un **prerequisito de despliegue obligatorio** (§16, §18), no un detalle de implementación con margen.

### Trazabilidad frase a frase del levantamiento

| Frase del requerimiento | Cobertura |
|---|---|
| «crear el reporte F-14» | RF-001 |
| «El Reporte de Planilla única» | RF-002 |
| «un reporte de Planilla Patronal» | RF-003 |
| «Los reportes se podrán descargar en formato Excel» | RF-004 |
| «se podrán visualizar por ciclos de nóminas pagados» | RF-005 (+ D-03) |
| *(no mencionado, pero necesario para que lo anterior produzca datos reales)* | RF-006, RF-007 — requerimientos de habilitación |

---

## 1. Resumen del producto o requerimiento

El motor de planilla (REQ-012…015) ya calcula, corrida a corrida, todas las cifras que exige el cumplimiento legal salvadoreño: retención de renta por empleado, cotización ISSS (empleado y patronal), cotización AFP (empleado y patronal) y cargas patronales adicionales. Hoy esas cifras solo pueden consultarse como líneas sueltas de una corrida individual (la bandeja y la impresión de planilla de REQ-013) — no existe una vista que las agregue con el formato y la periodicidad que exige cada obligación externa.

Este requerimiento pide construir esa capa de reporte: tres vistas agregadas y descargables en Excel —

- **F-14**: soporte de retención de impuesto sobre la renta, de periodicidad mensual, para la declaración ante la DGII/Ministerio de Hacienda.
- **Planilla Única**: cotizaciones ISSS + AFP por empleado, de periodicidad previsiblemente mensual, para el ISSS.
- **Planilla Patronal**: costo patronal total por empleado (salario + cargas patronales), de uso gerencial y de control interno.

filtrables por los ciclos de nómina que ya fueron **pagados** (corridas en estado `CERRADA`).

El problema que resuelve: hoy, si estos reportes no existen ya fuera del sistema, el contador los arma a mano (probablemente en una hoja de cálculo aparte), con el riesgo de que las cifras no cuadren centavo a centavo con lo que la planilla realmente calculó y pagó, y con trabajo repetido cada mes.

---

## 2. Objetivos del negocio

1. **Cumplimiento tributario y previsional**: producir, directamente desde la nómina ya calculada, los insumos que el patrono necesita para sus obligaciones ante Hacienda (DGII) e ISSS/AFP.
2. **Eliminar el armado manual** de estos reportes fuera del sistema, reduciendo el tiempo de cierre mensual del contador y el riesgo de error de transcripción.
3. **Garantizar trazabilidad**: toda cifra reportada debe poder rastrearse a la línea de la corrida de planilla que la originó — mecanismo que ya existe (`SourceModule`/`ConceptCode`) y que estos reportes solo tienen que reutilizar, no reinventar.
4. **Dejar la puerta abierta, sin comprometerse hoy**, a una futura integración de presentación electrónica real ante DGII/ISSS (ver `analisis-alcance-diferido-fases-siguientes.md` §7, necesidad N7) — este requerimiento entrega el insumo, no la integración.
5. **Reforzar la propuesta de valor de CLARIHR** como sistema integral de nómina para el mercado salvadoreño: competir con el Excel manual también en el terreno del cumplimiento legal, no solo en el cálculo del pago.

---

## 3. Alcance funcional

**A. Reporte F-14** — soporte de retención de renta del mes: por empleado, salario/base gravable y renta retenida, consolidando todas las corridas cerradas del mes calendario correspondiente (RF-001).

**B. Reporte Planilla Única** — cotizaciones ISSS y AFP del mes: por empleado, salario cotizable, ISSS (empleado/patronal) y AFP (empleado/patronal, con su AFP de afiliación), consolidando igual que F-14 (RF-002).

**C. Reporte Planilla Patronal** — costo patronal por ciclo de nómina: por empleado, salario base y cargas patronales (ISSS patronal, AFP patronal y demás conceptos `PagoPatronal`), con el total patronal ya cuadrado contra la cabecera de la corrida (RF-003).

**D. Exportación a Excel** — mecanismo de descarga para los tres reportes; para Planilla Patronal reutiliza el exportador ya certificado del sistema, para F-14 y Planilla Única requiere extenderlo con soporte de plantilla oficial exacta (RF-004, ratificado P-02).

**E. Visualización y selección por ciclo(s) pagado(s)** — filtro que solo ofrece corridas `CERRADA` como elegibles: individual para Planilla Patronal, consolidado por mes calendario para F-14/Planilla Única (RF-005, ratificado P-01).

**F. Datos habilitantes** — perfil legal de la empresa como patrono (RF-006) e identificación previsional completa del empleado (RF-007). No fueron pedidos explícitamente; tras la ratificación, el perfil legal patronal ya no solo habilita A/B/C: es **prerequisito para generar nómina** (P-03) — su alcance dejó de ser exclusivo de este requerimiento.

---

## 4. Fuera de alcance

- **Presentación o envío electrónico automático** ante la DGII o el ISSS — el sistema entrega el archivo Excel; la presentación ante la autoridad sigue siendo un acto manual del contador, fuera del sistema (coherente con la necesidad N7, ya diferida a nivel de proyecto — ver `analisis-alcance-diferido-fases-siguientes.md` §7).
- **Cálculo nuevo** de ISSS, AFP o Renta — el motor de REQ-012 ya lo hace; este requerimiento es una capa de reporte/agregación, no de cálculo, y no debe tocar `PayrollCalculationRules`.
- **Otros formularios de Hacienda no ligados a planilla** (IVA, retenciones por servicios/honorarios a terceros, pago a cuenta del impuesto de renta societario). El F-14 real de una empresa mezcla estas fuentes con la de sueldos; este sistema solo puede alimentar la porción de **retención sobre salarios**, no el formulario completo.
- **Firma electrónica, folio o plantilla institucional** del reporte (mismo diferido que aplica a constancias — necesidad N8).
- **Conciliación de lo declarado/pagado ante DGII/ISSS** contra lo reportado por el sistema — es auditoría externa, no una función de este módulo.
- **Multi-país / multi-moneda** — el alcance es El Salvador, USD (necesidad N12, ya diferida a nivel de proyecto; D-01).
- **Edición manual de las cifras** del reporte una vez generado — es un derivado de solo lectura de la(s) corrida(s) de origen.
- **Formato PDF o impreso** de estos tres reportes — el pedido dice explícitamente Excel; PDF queda fuera salvo que se ratifique lo contrario (P-07).
- **Recalcular retroactivamente** un reporte de un mes ya cerrado ante un cambio posterior de tasas o tramos — el reporte siempre refleja lo que estaba vigente cuando la corrida se calculó (D-04).

---

## 5. Actores o usuarios involucrados

- **Contador / analista de nómina** — genera y descarga los tres reportes para cumplir obligaciones mensuales; es el actor principal de este requerimiento.
- **Autorizador de planilla** — puede consultar los reportes de las corridas que autorizó, con el mismo permiso de lectura ya vigente.
- **Administrador de la empresa** — captura y mantiene el perfil legal patronal (RF-006), prerequisito de los tres reportes.
- **RRHH / gestor de expediente** — completa la identificación previsional del empleado (NUP ISSS, cuenta AFP) en el expediente (RF-007).
- **Sistema (motor de planilla)** — fuente de todas las cifras; no cambia su comportamiento por este requerimiento.
- **DGII / ISSS / AFP** (terceros, fuera del sistema) — destinatarios finales de la información que estos reportes producen como insumo; no son actores del sistema.

---

## 6. Requerimientos funcionales

> Numeración local a este análisis (RF-001…RF-007). No hay "Cobertura" hacia un REQ padre porque este es alcance nuevo, no un subconjunto — a diferencia de REQ-013/014/015 sobre REQ-012.

### RF-001 - Reporte F-14 (soporte de retención de renta mensual)

**Descripción:**
El sistema debe generar, para una empresa y un periodo mensual seleccionados, un reporte con una fila por empleado que muestra su identificación tributaria, su base gravable del mes y el impuesto sobre la renta que se le retuvo, consolidando todas las corridas de planilla **cerradas** cuyo periodo cae en ese mes calendario.

**Reglas de negocio:**
- Solo se consideran corridas en estado `CERRADA` (RN-01).
- **Consolidado por mes calendario** (`PayrollPeriodDefinition.Month`/`Year`), agregando todas las corridas cerradas del mes sin importar su frecuencia de origen — **ratificado tal cual (P-01, P-09)**.
- El monto de renta retenida es la suma de `FinalAmount` de las líneas con `ConceptCode = RENTA` del empleado en las corridas incluidas (RN-02) — nunca se recalcula.
- **El layout debe replicar la plantilla oficial del F-14 celda por celda — ratificado (P-02)**; no basta un detalle tabular genérico. Bloqueado en la práctica hasta que el negocio entregue el archivo/plantilla oficial exacta (ver RF-004, §16, §18).
- Un empleado sin NIT/DUI capturado aparece con advertencia visible, sin bloquear el resto del reporte (RN-06).
- El reporte requiere que el perfil legal patronal (RF-006) exista — y desde la ratificación (P-03), su ausencia bloquea antes: no se genera nómina de esa empresa sin él, así que en la práctica esta situación no debería llegar a ocurrir a nivel de reporte.

**Criterios de aceptación:**
Dado un mes con una o más corridas `CERRADA`, cuando el contador genera el F-14 de ese mes, entonces el reporte muestra una fila por cada empleado con línea `RENTA` en esas corridas, con el total de renta retenida cuadrando exactamente contra la suma de esas líneas, **en el layout de la plantilla oficial de la DGII**; dado un empleado sin NIT capturado, entonces su fila lleva una advertencia visible en vez de bloquear el reporte.

**Prioridad:** Alta.

**Dependencias:** RF-005 (selección de ciclo/mes), RF-006 (perfil legal patronal — ahora también gate de generación de nómina), RF-007 (NIT/DUI garantizado), RF-004 (exportador con soporte de plantilla oficial — **nueva dependencia dura tras P-02**). P-01…P-04 **ratificadas**; queda pendiente solo la entrega del archivo de plantilla oficial del F-14 por parte del negocio.

---

### RF-002 - Reporte Planilla Única (cotizaciones ISSS + AFP)

**Descripción:**
El sistema debe generar, para una empresa y un periodo mensual seleccionados, un reporte con una fila por empleado que muestra su identificación previsional, su salario cotizable y las cotizaciones ISSS y AFP (empleado y patronal), consolidando todas las corridas **cerradas** del mes calendario correspondiente.

**Reglas de negocio:**
- Mismas reglas de estado y de no-recálculo que RF-001 (RN-01, RN-02).
- **Consolidado por mes calendario**, igual que F-14 — **ratificado tal cual (P-01, P-09)**.
- Los montos ISSS se toman de las líneas `ConceptCode = ISSS` (empleado) e `ISSS_PATRONAL` (patronal); los de AFP, de `AFP` (empleado) y `AFP_PATRONAL` (patronal).
- Cada empleado se reporta con la AFP a la que pertenece (`PersonnelFile.AfpCode`, ya existente).
- **El layout debe replicar la plantilla oficial de la Planilla Única celda por celda — ratificado (P-02)**; misma dependencia dura de RF-004 y de recibir el archivo oficial que RF-001.
- **NUP ISSS y cuenta AFP son OBLIGATORIOS — ratificado (P-04)**, no un dato "recomendado". **Ratificado también (P-11): un empleado sin alguno de los dos NO se procesa** — su nómina individual no procede, mismo criterio que el perfil legal patronal a nivel empresa (P-03). **El bloqueo excluye solo a ese empleado — ratificado (P-12)** — el resto de la corrida se genera con normalidad. **Sin periodo de transición — ratificado (P-13)**: el negocio acepta que, si un empleado no tiene el dato capturado antes del despliegue, su nómina simplemente no se genera desde el primer ciclo — ver la campaña de captura obligatoria antes de desplegar (§16, §18, Pendientes de despliegue).

**Criterios de aceptación:**
Dado un mes con corridas `CERRADA`, cuando el contador genera la Planilla Única de ese mes, entonces el reporte muestra el detalle ISSS y AFP (empleado y patronal) por empleado, cuadrando contra las líneas de origen, **en el layout de la plantilla oficial del ISSS**; dado un empleado sin NUP ISSS o cuenta AFP, entonces **su nómina no procede** (P-11) — no aparece pagado en la corrida hasta que el dato se complete.

**Prioridad:** Alta.

**Dependencias:** RF-005, RF-006 (gate de generación de nómina a nivel empresa), RF-007 (NUP ISSS + cuenta AFP, gate de generación a nivel **empleado** — G-02/G-03), RF-004 (exportador con soporte de plantilla oficial). P-01, P-02, P-04, P-11, P-12, P-13 **todas ratificadas** — sin preguntas de negocio pendientes en este RF.

---

### RF-003 - Reporte Planilla Patronal (costo patronal por empleado)

**Descripción:**
El sistema debe generar, para una o más corridas de planilla **cerradas** seleccionadas, un reporte con una fila por empleado que muestra su salario base y el costo patronal total (ISSS patronal, AFP patronal y demás cargas `PagoPatronal`), cuadrando contra el `TotalEmployerCost` ya persistido en la cabecera de cada corrida. **Su propósito, ratificado en P-05, es de control interno: validar lo que la empresa realmente paga contra lo que luego declara al gobierno (F-14/Planilla Única)** — no es un formulario oficial con nombre numerado como F-14, es el respaldo propio del negocio.

**Reglas de negocio:**
- Mismas reglas de estado y de no-recálculo que RF-001 (RN-01, RN-02).
- Este reporte se genera **por corrida individual — ratificado (P-01)**, a diferencia de F-14 y Planilla Única que consolidan por mes; coherente con su propósito de control (revisar cada corrida a medida que se cierra, antes de que el mes se consolide y se declare).
- Es el único de los tres reportes que **no depende** de datos que hoy no existen (NUP ISSS, cuenta AFP) — solo depende del perfil legal patronal para su encabezado.
- **Layout**: al ser un reporte de control interno (no un formulario externo numerado), no está sujeto al mismo requisito de "plantilla oficial exacta" ratificado para F-14/Planilla Única en P-02 — puede construirse con el exportador tabular ya existente sin esperar un archivo de plantilla externo, salvo que el negocio tenga su propio formato interno estándar a replicar (a confirmar si existe).

**Criterios de aceptación:**
Dado una corrida `CERRADA`, cuando el analista genera la Planilla Patronal de esa corrida, entonces el reporte muestra el costo patronal por empleado y el total del reporte coincide con `TotalEmployerCost` de la cabecera de la corrida.

**Prioridad:** Alta — **candidato a MVP** (§18): es el único de los tres sin dependencia dura de un archivo de plantilla externo todavía no entregado.

**Dependencias:** RF-005, RF-006 (ahora también gate de generación de nómina). P-01 y P-05 **ratificadas**.

---

### RF-004 - Descarga en formato Excel, replicando la plantilla oficial de F-14 y Planilla Única

**Descripción:**
Los tres reportes deben poder descargarse como archivo `.xlsx`. **Tras la ratificación (P-02: «se deben replicar las planillas oficiales»), este requerimiento cambia de naturaleza**: para F-14 y Planilla Única no basta un detalle tabular genérico — el archivo debe reproducir el formulario oficial (DGII/ISSS) con su layout exacto. Planilla Patronal, al ser de control interno (P-05), puede seguir usando el exportador tabular ya existente salvo que el negocio tenga su propio formato interno a replicar.

**Reglas de negocio:**
- **El exportador actual (`ReportExportFileWriter`) NO alcanza para F-14/Planilla Única** — es un escritor de una sola hoja plana con columnas por reflexión, sin soporte de encabezados fijos, celdas combinadas ni layout multi-sección (G-05, confirmado). Se necesita extender el exportador con un modo de plantilla/posicional, o introducir un mecanismo capaz de layout fijo — decisión técnica a tomar en el plan técnico.
- **Bloqueante de dato, no solo de código**: el sistema **todavía no tiene en su poder** el archivo de plantilla oficial exacta de ninguno de los 3 reportes. Sin ese insumo del negocio, no se puede construir el layout final ni estimar el esfuerzo real con confianza (ver §16, §18).
- Planilla Patronal puede seguir reutilizando `ReportExportFileWriter`/`ReportExportDeliveryService` tal cual, salvo que P-05 implique también un formato interno específico a confirmar.
- Aplica el mismo límite de filas síncronas ya vigente (`NormalizedMaxSynchronousExportRows`) donde el mecanismo lo permita.

**Criterios de aceptación:**
Dado el archivo de plantilla oficial del F-14 (o de la Planilla Única) entregado por el negocio, cuando el sistema genera el reporte correspondiente, entonces el `.xlsx` resultante reproduce esa plantilla con los datos del periodo, celda por celda; dado la Planilla Patronal, cuando se genera, entonces se descarga en `.xlsx` con el exportador tabular ya existente.

**Prioridad:** Alta — **bloquea RF-001/RF-002 hasta resolverse** (dato + decisión técnica).

**Dependencias:** Recepción del archivo de plantilla oficial de F-14 y de Planilla Única (**acción del negocio, no del equipo técnico** — ver §18). Decisión técnica sobre cómo extender el exportador (plan técnico).

---

### RF-005 - Selección y visualización por ciclo(s) de nómina pagados

**Descripción:**
El usuario debe poder elegir, para cada reporte, el ciclo o los ciclos de planilla ya **pagados** (corridas `CERRADA`) que quiere reportar — un mes calendario para F-14/Planilla Única, una corrida (o rango) para Planilla Patronal.

**Reglas de negocio:**
- Solo se listan como elegibles las corridas en estado `CERRADA` — nunca `GENERADA`, `AUTORIZADA` ni `ANULADA` (RN-01, D-03) — **ratificado tal cual (P-06: «solo CERRADAS»)**.
- Para F-14 y Planilla Única, la selección es por mes calendario contable (`PayrollPeriodDefinition.Month`/`Year`, ya sembrado por REQ-012), consolidando automáticamente todas las corridas cerradas de ese mes sin importar si la nómina de origen es mensual, quincenal o semanal (RN-03) — **ratificado tal cual (P-01, P-09)**.
- Si el mes/ciclo seleccionado no tiene ninguna corrida `CERRADA`, el reporte se genera vacío con un aviso, no como error.

**Criterios de aceptación:**
Dado que el usuario abre el selector de periodo, cuando lista los ciclos disponibles, entonces solo aparecen corridas/meses con al menos una corrida `CERRADA`; dado un mes sin corridas cerradas, cuando lo selecciona, entonces el reporte se genera vacío con aviso explícito.

**Prioridad:** Alta.

**Dependencias:** Ninguna nueva — el estado `CERRADA` y `PayrollPeriodDefinition` ya existen. **P-01 y P-06 ratificadas, sin cambios pendientes.**

---

### RF-006 - Perfil legal de la empresa como patrono *(requerimiento de habilitación — ALCANCE AMPLIADO en la ratificación)*

**Descripción:**
El sistema debe permitir capturar y mantener, una vez por empresa, los datos de identidad legal del patrono que los tres reportes necesitan en su encabezado: razón social, NIT patronal, número de registro patronal ante el ISSS, dirección fiscal y actividad económica.

**Por qué se incluye sin haber sido pedido explícitamente:** se verificó en código que `Company.cs` no tiene ninguno de estos campos hoy (G-01) — sin este requerimiento, RF-001/002/003 no pueden producir un encabezado real, solo uno en blanco.

**Reglas de negocio:**
- Es un registro **único por empresa** (tenant).
- Solo el administrador de la empresa puede editarlo.
- **Ratificado con un matiz importante que amplía el alcance original (P-03): no se pueden generar nóminas (corridas de planilla) de una empresa que no tenga su perfil legal patronal completo.** El gate no es solo de estos 3 reportes — es de la generación de planilla misma. Esto significa que `PayrollRun.Create()` (REQ-012, **módulo ya certificado y con el backlog cerrado 15/15**) necesita un guard nuevo que hoy no existe.
- Si falta al momento de generar cualquiera de los tres reportes, el sistema igual bloquea con un aviso explícito (RN-07) — aunque en la práctica esta situación no debería poder ocurrir una vez el gate de generación esté activo, porque ya no existirían corridas `CERRADA` de una empresa sin perfil legal.

**Criterios de aceptación:**
Dado que una empresa no tiene perfil legal patronal capturado, cuando alguien intenta **generar una corrida de planilla** de esa empresa, entonces el sistema bloquea con un mensaje claro señalando qué falta y dónde capturarlo (nuevo criterio, RF-006 ampliado); dado que el perfil existe, entonces la generación de planilla procede con normalidad y los tres reportes completan su encabezado con esos datos.

**Prioridad:** Alta — bloqueante para RF-001/002/003 **y ahora también para la generación de planilla de REQ-012**.

**Dependencias:** Ninguna de datos — es dato nuevo, aislado. **Dependencia técnica nueva y de mayor riesgo**: tocar el punto de entrada de generación de planilla de un módulo ya cerrado exige regresión cuidadosa (ver §16). P-03 **ratificada, con el matiz ya incorporado arriba**.

---

### RF-007 - Identificación tributaria y previsional completa del empleado *(requerimiento de habilitación)*

**Descripción:**
El sistema debe garantizar, o al menos exponer con claridad cuándo falta, la identificación que F-14 y Planilla Única necesitan por empleado: NIT/DUI (ya existen como colección genérica, RF-007a) y, como dato nuevo, el número de afiliado ISSS (NUP) y el número de cuenta AFP (RF-007b).

**Por qué se incluye sin haber sido pedido explícitamente:** se verificó en código que no existe NUP ISSS en ningún lugar del dominio, ni número de cuenta AFP (solo el código de la AFP) — G-02/G-03. Sin este dato, Planilla Única no puede identificar al empleado ante el ISSS/AFP en su fila.

**Reglas de negocio:**
- NIT/DUI: se valida su existencia contra la colección `PersonnelFileIdentification` ya construida (tipos `DUI`/`NIT` del catálogo existente) — no se duplica el dato, solo se garantiza su lectura para el reporte. Su ausencia sigue siendo solo advertencia (F-14, RN-06) — P-11 no tocó este dato, solo NUP ISSS/cuenta AFP.
- NUP ISSS y cuenta AFP: dato nuevo. Recomendación de este análisis (mantenida): el NUP ISSS se modela igual que DUI/NIT, como una entrada más de `PersonnelFileIdentification` con un tipo de catálogo nuevo; la cuenta AFP se modela como un campo hermano de `PersonnelFile.AfpCode` (ya existente).
- **NUP ISSS y cuenta AFP son OBLIGATORIOS — ratificado tal cual (P-04: «Si son obligatorios»)**.
- **Ratificado (P-11, 2026-07-16): «debe bloquear la nómina, sin eso no procede».** Mismo criterio que P-03 (perfil legal patronal), un nivel más abajo: sin NUP ISSS o sin cuenta AFP, la nómina de **ese empleado específico** no procede — no es solo una advertencia en el reporte, es un gate en la generación misma. **Es el cambio de mayor profundidad técnica del análisis**: a diferencia del gate de RF-006 (una sola verificación por corrida, a nivel de empresa, en `PayrollRun.Create()`), este gate corre **una vez por empleado dentro del cálculo del motor** (REQ-012, ya certificado) — cada línea que el motor genera para un empleado necesita el chequeo.
- **Mecánica ratificada (P-12, 2026-07-17): «bloquear solo la línea/inclusión de ese empleado».** El resto de la corrida —los demás empleados de la misma empresa— se genera con normalidad; solo el empleado sin el dato queda excluido de ese ciclo.
- **Sin periodo de transición — ratificado (P-13, 2026-07-17): «se acepta interrumpir la operación»**. El negocio decidió explícitamente no construir un modo de advertencia-sin-bloqueo transitorio: el gate se activa duro desde el despliegue. Esto traslada el riesgo del código al **calendario de despliegue** — la captura de NUP ISSS/cuenta AFP de la plantilla existente pasa a ser un **prerequisito operativo antes de desplegar**, no una mejora posterior (ver §16, §18 y Pendientes de despliegue en el backlog).

**Criterios de aceptación:**
Dado un empleado sin NUP ISSS o sin cuenta AFP capturados, cuando se intenta generar su nómina, entonces el sistema no la procesa (P-11) y lo señala con una advertencia bloqueante en su expediente y en la corrida; dado un empleado con ambos datos completos, entonces su nómina se genera con normalidad y su fila en la Planilla Única los muestra correctamente.

**Prioridad:** Alta — bloqueante para el detalle por empleado de RF-002, parcial para RF-001, **y ahora también para la generación de nómina del propio empleado en REQ-012**.

**Dependencias:** Ninguna estructural de datos — extiende patrones ya existentes (`PersonnelFileIdentification`, `PersonnelFile.AfpCode`). **Dependencia técnica nueva y de mayor riesgo que RF-006**: el gate corre dentro del cálculo por-empleado del motor ya certificado, no solo en su punto de entrada — exige la regresión más cuidadosa de todo este requerimiento (ver §16, §18). **P-04, P-11, P-12 y P-13 ratificadas — sin preguntas de negocio pendientes en este RF.** Dependencia operativa nueva: campaña de captura de datos previa al despliegue (ver Pendientes de despliegue).

---

## 7. Requerimientos no funcionales

| Categoría | Requisito |
|---|---|
| **Seguridad** | Estos reportes concentran NIT/DUI/NUP ISSS/cuenta AFP de **toda** la plantilla en un solo archivo descargable — mayor sensibilidad que el detalle por corrida ya existente. Requiere permiso de lectura dedicado o, como mínimo, el permiso ya vigente `ViewPayrollRuns` explícitamente reconfirmado como suficiente (P-10). |
| **Rendimiento** | Reutiliza el límite de filas síncronas ya vigente (`NormalizedMaxSynchronousExportRows`); una empresa con varios cientos de empleados y 12 meses de historial debe poder generar cualquiera de los tres reportes en segundos. |
| **Disponibilidad** | Sin requisito nuevo — hereda la disponibilidad ya vigente del módulo de planilla. |
| **Escalabilidad** | La consolidación mensual debe soportar agregar N corridas (2 quincenas, 4-5 semanas) sin degradar linealmente; a evaluar en plan técnico si conviene precalcular totales mensuales o agregarlos on-demand en cada generación. |
| **Usabilidad** | Nombres de columna en español, formato de moneda y fecha consistente con las bandejas ya existentes de REQ-013; el contador no debería tener que adivinar a qué corresponde cada columna frente al formulario oficial que ya conoce. |
| **Auditoría** | Cada generación/descarga debe quedar registrada (usuario, fecha, empresa, filtro/periodo usado) — mismo patrón ya vigente para las bandejas de REQ-013. |
| **Exactitud** | Cero tolerancia a discrepancia: toda cifra del reporte debe cuadrar centavo a centavo contra las líneas persistidas de la(s) corrida(s) de origen (RN-02) — no hay redondeo ni recálculo adicional en la capa de reporte. |
| **Compatibilidad** | El archivo `.xlsx` debe abrir sin advertencias en Excel y en Google Sheets, mismo estándar ya validado del exportador de la casa. |
| **Mantenibilidad** | Las tasas ISSS/AFP y los tramos de Renta ya están versionados en tablas oficiales (`IncomeTaxWithholdingBracket`, esquemas de contribución); un cambio normativo futuro no debería tocar el código de estos tres reportes, solo los datos de catálogo que el motor ya consume. |
| **Accesibilidad** | No aplica un requisito distinto al ya vigente para exportación de archivos; si se agrega una pantalla de configuración del perfil legal patronal (RF-006), debe seguir los mismos estándares de accesibilidad del resto del sistema. |

---

## 8. Historias de usuario

### HU-001 - Generar el F-14 del mes

Como **contador**,
quiero **generar el reporte F-14 de un mes calendario**,
para **contar con el soporte de retención de renta que necesito declarar ante la DGII sin armarlo a mano**.

**Criterios de aceptación:**
- Dado un mes con corridas `CERRADA`, cuando genero el reporte, entonces obtengo una fila por empleado con su renta retenida cuadrada contra las líneas de origen.
- Dado un empleado sin NIT capturado, cuando genero el reporte, entonces su fila muestra una advertencia en vez de detener todo el reporte.

### HU-002 - Generar la Planilla Única del mes

Como **contador**,
quiero **generar el reporte de Planilla Única de un mes calendario**,
para **contar con el detalle de cotizaciones ISSS y AFP que necesito para cumplir con el ISSS**.

**Criterios de aceptación:**
- Dado un mes con corridas `CERRADA`, cuando genero el reporte, entonces obtengo ISSS y AFP (empleado y patronal) por empleado, con su AFP de afiliación identificada.
- Dado un empleado sin NUP ISSS o cuenta AFP, cuando genero el reporte, entonces su fila muestra advertencia visible.

### HU-003 - Generar la Planilla Patronal de una corrida

Como **analista de nómina**,
quiero **generar el reporte de Planilla Patronal de una corrida ya pagada**,
para **tener el detalle del costo patronal por empleado, para control interno o una inspección**.

**Criterios de aceptación:**
- Dado una corrida `CERRADA`, cuando genero el reporte, entonces el total coincide con el costo patronal ya persistido en la cabecera de esa corrida.

### HU-004 - Descargar cualquiera de los tres reportes en Excel

Como **contador o analista de nómina**,
quiero **descargar el reporte que generé como archivo Excel**,
para **adjuntarlo a mi trámite o guardarlo como respaldo**.

**Criterios de aceptación:**
- Dado un reporte generado, cuando lo descargo, entonces recibo un `.xlsx` válido con las columnas correspondientes.

### HU-005 - Elegir el ciclo o mes a reportar

Como **contador**,
quiero **elegir de una lista solo los ciclos de nómina que ya están pagados**,
para **no arriesgarme a reportar cifras de una corrida todavía en revisión o sin autorizar**.

**Criterios de aceptación:**
- Dado el selector de periodo, cuando lo abro, entonces solo veo ciclos/meses con al menos una corrida `CERRADA`.

### HU-006 - Configurar el perfil legal de la empresa

Como **administrador de la empresa**,
quiero **capturar los datos legales de mi empresa como patrono una sola vez**,
para **que los tres reportes puedan completar su encabezado automáticamente**.

**Criterios de aceptación:**
- Dado que no he capturado el perfil legal, cuando alguien intenta generar cualquiera de los tres reportes, entonces el sistema bloquea con un aviso claro de qué falta.
- Dado que ya lo capturé, cuando se genera cualquiera de los tres reportes, entonces el encabezado se completa con esos datos.

### HU-007 - Completar la identificación previsional del empleado

Como **gestor de expediente (RRHH)**,
quiero **registrar el NUP ISSS y la cuenta AFP de cada empleado en su expediente**,
para **que la Planilla Única no muestre advertencias evitables por falta de este dato**.

**Criterios de aceptación:**
- Dado un expediente sin NUP ISSS o cuenta AFP, cuando lo edito y los capturo, entonces la próxima Planilla Única que incluya a ese empleado ya no muestra advertencia por ese motivo.

---

## 9. Reglas de negocio (consolidadas)

> Todas ratificadas por el negocio el 2026-07-16 (§17), salvo donde se indica lo contrario.

- **RN-01** Los tres reportes solo incluyen corridas de planilla en estado `CERRADA` — nunca `GENERADA`, `AUTORIZADA` ni `ANULADA` (D-03, P-06).
- **RN-02** Las cifras reportadas son siempre las **persistidas** en la corrida (`FinalAmount` por línea, totales de cabecera) — jamás se recalculan al generar el reporte (D-04, P-08), igual que ya rige para las bandejas de REQ-013.
- **RN-03** F-14 y Planilla Única agrupan por **empresa + mes calendario contable** (`PayrollPeriodDefinition.Month`/`Year`), consolidando todas las corridas `CERRADA` de ese mes sin importar su frecuencia de origen (P-01, P-09).
- **RN-04** Planilla Patronal se genera por **corrida individual** (P-01) — no tiene la misma restricción legal mensual que las otras dos; es un reporte de control interno (P-05), no una declaración externa.
- **RN-05** Todo empleado incluido en un reporte debe poder rastrearse a su(s) línea(s) de origen por `ConceptCode`/`SourceModule` — trazabilidad ya vigente desde REQ-012, reutilizada aquí sin cambios.
- **RN-06** Un empleado sin NIT/DUI capturado aparece en el reporte F-14 con una **advertencia visible por fila** — esto NO bloquea (P-11 solo aplica a NUP ISSS/cuenta AFP, ver RN-10).
- **RN-07** El perfil legal patronal es un registro **único por empresa**, editable solo por administración, y es prerequisito para generar cualquiera de los tres reportes con encabezado completo. **Reforzada por P-03**: además, es prerequisito para **generar cualquier nómina de esa empresa** — el gate vive en la generación de planilla (REQ-012), no solo en estos reportes.
- **RN-08** Los tres reportes se descargan en formato Excel (`.xlsx`). Para Planilla Patronal, reutilizando el mecanismo de exportación ya certificado (REQ-013), sujeto al límite de filas síncronas ya vigente. **Para F-14 y Planilla Única, el archivo debe replicar la plantilla oficial exacta (P-02)** — el mecanismo actual no alcanza tal cual (G-05); ver RF-004.
- **RN-09** *(nueva)* El permiso de lectura de los tres reportes es **dedicado**, distinto del `ViewPayrollRuns` ya existente — ratificado (P-10); nombre de trabajo `ViewComplianceReports` (a confirmar en plan técnico).
- **RN-10** *(nueva, P-11/P-12/P-13)* Un empleado sin NUP ISSS o sin cuenta AFP capturados **no se procesa en la generación de nómina** — ratificado (P-11: «debe bloquear la nómina, sin eso no procede»), mismo criterio que RN-07 (perfil legal patronal) pero a nivel de empleado individual, corriendo dentro del cálculo por-empleado del motor. **El bloqueo excluye solo a ese empleado, el resto de la corrida procede con normalidad (P-12).** **Sin periodo de transición: el gate se activa duro desde el despliegue, sin modo advertencia-primero (P-13)** — la captura de estos datos para la plantilla existente debe completarse antes de desplegar esta funcionalidad.

---

## 10. Flujos principales

### Flujo 1 — Generar el reporte F-14 del mes
1. El contador entra a Nómina → Reportes legales. 2. Selecciona la empresa y el mes calendario a reportar. 3. El sistema identifica todas las corridas `CERRADA` cuyo periodo cae en ese mes (`PayrollPeriodDefinition.Month`/`Year`). 4. El sistema agrega, por empleado, la base gravable y la renta retenida (suma de líneas `ConceptCode = RENTA`) de esas corridas. 5. El sistema valida que el perfil legal patronal y el NIT de cada empleado existan; si falta alguno, marca advertencia sin bloquear el resto. 6. El contador descarga el archivo `.xlsx`.

### Flujo 2 — Generar el reporte Planilla Única del mes
1. El contador entra a Nómina → Reportes legales. 2. Selecciona la empresa y el mes calendario a reportar. 3. El sistema identifica todas las corridas `CERRADA` de ese mes, igual que en el Flujo 1. 4. El sistema agrega, por empleado, ISSS y AFP (empleado y patronal), junto con la AFP de afiliación. 5. El sistema valida NUP ISSS y cuenta AFP por empleado, marcando advertencia si faltan. 6. El contador descarga el archivo `.xlsx`.

### Flujo 3 — Generar el reporte Planilla Patronal de una corrida
1. El analista de nómina entra a una corrida `CERRADA` específica (o selecciona un rango, si se ratifica). 2. El sistema agrega, por empleado, el salario base y las líneas `PagoPatronal` (ISSS patronal, AFP patronal y demás cargas). 3. El sistema muestra el costo patronal total, coincidente con `TotalEmployerCost` de la cabecera. 4. El analista descarga el archivo `.xlsx`.

### Flujo 4 — Configurar el perfil legal patronal *(habilitante, una sola vez por empresa)*
1. El administrador de la empresa entra a Configuración → Datos legales. 2. Captura razón social, NIT patronal, número de registro patronal ISSS, dirección fiscal y actividad económica. 3. El sistema guarda el registro único de la empresa. 4. Desde ese momento, los tres reportes pueden completar su encabezado.

---

## 11. Flujos alternativos y excepciones

| Escenario | Comportamiento |
|---|---|
| El mes/ciclo seleccionado no tiene ninguna corrida `CERRADA` | El reporte se genera vacío con aviso «sin corridas cerradas en el periodo seleccionado» — no es un error. |
| Falta el perfil legal patronal | El sistema **bloquea** la generación con un aviso explícito y enlace a la configuración — no genera un encabezado incompleto en silencio. |
| Un empleado no tiene NIT/DUI, NUP ISSS o cuenta AFP capturados | Aparece en el reporte con advertencia visible por fila; el reporte se genera igual (RN-06). |
| Se seleccionan corridas de más de una empresa | No aplica — el reporte siempre es de una sola empresa (multi-tenant, sin excepción). |
| El volumen de filas excede el límite síncrono de exportación | Mismo comportamiento ya vigente en las demás bandejas del sistema (entrega asíncrona vía `ReportExportDeliveryService`). |
| Se corrige o anula una corrida **después** de haber descargado el reporte de su mes | El archivo ya descargado es una fotografía del momento; no se re-emite automáticamente — el contador debe volver a generarlo si necesita la versión actualizada. |
| El mes seleccionado mezcla corridas de distinta frecuencia (una nómina mensual y otra quincenal en la misma empresa) | Se consolidan todas en un solo reporte del mes (RN-03) — sujeto a P-09. |
| El usuario no tiene el permiso de lectura de planilla | 403, igual que el resto de bandejas y consultas de planilla ya existentes. |

---

## 12. Datos requeridos

### Entidad: Perfil legal patronal *(NUEVA — RF-006)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Razón social | Texto | Sí | No vacío | Nombre legal completo de la empresa ante Hacienda |
| NIT patronal | Texto | Sí | Formato NIT salvadoreño (14 dígitos con guiones) | Número de identificación tributaria de la empresa como patrono |
| Número de registro patronal ISSS | Texto | Sí | Numérico, longitud según ISSS *(confirmar formato exacto — P-03)* | Identifica a la empresa como patrono ante el ISSS |
| Dirección fiscal | Texto | Sí | No vacío | Dirección registrada ante Hacienda |
| Actividad económica | Catálogo o texto | Recomendado | FK a catálogo nuevo si se decide tipificarlo *(hoy no existe — G-01)* | Código/descripción de actividad económica de la empresa |
| Representante legal | Referencia | No | FK a `LegalRepresentative` (ya existente) | Vínculo opcional al representante legal ya modelado en el sistema |

### Entidad: Identificación previsional del empleado *(EXTENSIÓN de `PersonnelFileIdentification` y `PersonnelFile` — RF-007)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| NIT | Texto | Sí, para F-14 | Ya validado por el catálogo de tipos de identificación existente | Identificación tributaria del empleado (ya existe como dato, se garantiza su lectura) |
| DUI | Texto | Respaldo si no hay NIT | Ídem | Documento de identidad del empleado (ya existe) |
| NUP ISSS | Texto | Sí, para Planilla Única | Numérico *(confirmar formato — P-04)* | Número Único Previsional del empleado ante el ISSS — **dato nuevo, no existe hoy (G-02)** |
| Cuenta AFP | Texto | Sí, para Planilla Única | Numérico *(confirmar formato por AFP — P-04)* | Número de cuenta del empleado en su AFP — **dato nuevo, no existe hoy (G-03)** |
| Código AFP | Catálogo | Ya existente | FK a `AfpCatalogItem` | Identifica a qué AFP pertenece el empleado (`PersonnelFile.AfpCode`, ya construido) |

### Entidad: Línea del reporte F-14 *(proyección — no es tabla nueva, se arma en consulta)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Empleado | Texto | Sí | — | Nombre completo |
| Código de empleado | Texto | Sí | — | Identificador interno |
| NIT | Texto | Sí (con advertencia si falta) | — | Identificación tributaria |
| Salario/base gravable del mes | Moneda | Sí | Σ `BaseAmount` de líneas `RENTA` del mes | Base sobre la que se calculó la retención |
| Renta retenida del mes | Moneda | Sí | Σ `FinalAmount` de líneas `ConceptCode=RENTA` del mes | Impuesto retenido a reportar |
| Advertencias | Texto | No | — | P. ej. «sin NIT registrado» |

### Entidad: Línea del reporte Planilla Única *(proyección)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Empleado / Código de empleado | Texto | Sí | — | Identificación interna |
| NUP ISSS | Texto | Sí (con advertencia si falta) | — | Identificación previsional ISSS |
| Salario cotizable del mes | Moneda | Sí | Σ `BaseAmount` de líneas ISSS/AFP del mes | Base de cotización |
| ISSS empleado / ISSS patronal | Moneda | Sí | Σ `FinalAmount` de `ISSS` / `ISSS_PATRONAL` | Cotización de salud |
| Código y nombre de AFP | Catálogo | Sí | `PersonnelFile.AfpCode` → `AfpCatalogItem` | AFP de afiliación |
| Cuenta AFP | Texto | Sí (con advertencia si falta) | — | Número de cuenta del empleado en su AFP |
| AFP empleado / AFP patronal | Moneda | Sí | Σ `FinalAmount` de `AFP` / `AFP_PATRONAL` | Cotización de pensión |
| Advertencias | Texto | No | — | P. ej. «sin cuenta AFP registrada» |

### Entidad: Línea del reporte Planilla Patronal *(proyección)*

| Campo | Tipo de dato | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| Empleado / Código de empleado | Texto | Sí | — | Identificación interna |
| Centro de costo | Texto | No | Ya disponible en `PayrollRunLine.CostCenterName` | Para reportes gerenciales por área |
| Salario base | Moneda | Sí | `BaseAmount` de la línea `SALARIO` | Sueldo del periodo |
| ISSS patronal / AFP patronal | Moneda | Sí | Σ `FinalAmount` de `ISSS_PATRONAL` / `AFP_PATRONAL` | Cargas patronales de ley |
| Otras cargas patronales | Moneda | No | Σ `FinalAmount` de otras líneas `LineClass=PagoPatronal` (p. ej. `INCAF`) | Cargas patronales adicionales ya modeladas en el motor |
| Costo patronal total | Moneda | Sí | Debe coincidir con `PayrollRun.TotalEmployerCost` de la cabecera | Verificación de cuadre |

---

## 13. Integraciones necesarias

| Integración | Estado |
|---|---|
| Presentación electrónica ante la DGII (Hacienda) | **Fuera de alcance** — el reporte es un archivo descargable, no hay envío automático (necesidad N7, ya diferida a nivel de proyecto) |
| Presentación o pago ante el ISSS (Planilla Única) | **Fuera de alcance**, misma razón |
| Catálogo de actividad económica | No existe hoy en el sistema; si se decide validarlo contra un catálogo formal (en vez de texto libre), es un catálogo nuevo a sembrar (parte de RF-006) |
| Integraciones de terceros nuevas | **Ninguna** — los tres reportes se generan 100% con datos que ya existen o se capturan manualmente dentro de CLARIHR |

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Contador / analista de nómina | Generar y descargar los tres reportes (permiso dedicado `ViewComplianceReports`, nombre de trabajo — **ratificado P-10**) | Solo corridas `CERRADA` de la(s) empresa(s) a las que tiene acceso |
| Administrador de la empresa | Configurar/editar el perfil legal patronal (RF-006) | No genera los reportes por este permiso por sí solo, salvo que también tenga el permiso de lectura de planilla |
| RRHH / gestor de expediente | Completar NUP ISSS / cuenta AFP en el expediente del empleado | Mismos gates de edición de expediente ya vigentes — sin permiso nuevo |
| Autorizador de planilla | Ver los reportes de las corridas que autorizó | Mismo permiso de lectura ya vigente, sin uno adicional |
| Empleado (autoservicio) | **Sin acceso** | Estos reportes son agregados patronales/corporativos de toda la plantilla, no información individual — explícitamente fuera de alcance para autoservicio |

---

## 15. Criterios de aceptación generales

1. Los tres reportes se generan exclusivamente a partir de corridas `CERRADA`.
2. Toda cifra de los reportes cuadra centavo a centavo con las líneas persistidas de la(s) corrida(s) de origen — cero recálculo en la capa de reporte.
3. Los tres reportes se descargan en `.xlsx` mediante el mecanismo de exportación ya certificado del sistema.
4. Un empleado con datos previsionales o tributarios incompletos aparece con advertencia visible, sin bloquear el reporte completo.
5. El perfil legal patronal existe y se valida antes de completar el encabezado de cualquiera de los tres reportes; si falta, el sistema bloquea con aviso explícito en vez de generar un encabezado en blanco.
6. El acceso a los tres reportes respeta el permiso de lectura de planilla vigente, o el permiso dedicado que se ratifique en P-10.
7. Las preguntas P-01…P-10 quedan respondidas por el negocio/contador antes de pasar a plan técnico.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos

- **[CONFIRMADO, ya no hipotético] El exportador actual no soporta layout de celda fija/combinada, y el formato legal SÍ debe reproducirse exacto (P-02)** — hay desarrollo no previsto en el mecanismo de exportación (G-05) que debe dimensionarse en plan técnico.
- **El sistema no tiene todavía el archivo de plantilla oficial de F-14 ni de Planilla Única** — sin ese insumo del negocio, el plan técnico de RF-001/002/004 no puede estimarse con confianza (nuevo riesgo #1, ver §18).
- **Tocar la generación de planilla de REQ-012** (nuevo gate de perfil legal patronal, P-03) es un cambio a un módulo **ya certificado y con el backlog cerrado 15/15** — riesgo de regresión si no se prueba con la misma suite E2E/dorada que ya lo certificó.
- **[RATIFICADO — riesgo de adopción ACEPTADO explícitamente por el negocio, P-13] El 100% de los empleados existentes carece HOY de NUP ISSS/cuenta AFP, porque el campo todavía no existe en el sistema. El negocio confirmó que NO habrá periodo de transición** — «se acepta interrumpir la operación». Esto significa que, si una empresa despliega esta funcionalidad sin haber completado antes la captura de NUP ISSS/cuenta AFP de su plantilla, **esos empleados sencillamente no tendrán nómina generada desde el primer ciclo**. Ya no es un riesgo abierto — es una consecuencia aceptada que debe convertirse en un **prerequisito de despliegue obligatorio** (campaña de captura de datos antes de activar, ver §18 recomendación 10 y Pendientes de despliegue del backlog).
- Mecánica del bloqueo confirmada (P-12): excluye solo la línea del empleado faltante, no la corrida completa — el riesgo del punto anterior queda acotado a los empleados individuales sin dato, no a toda la empresa.
- Cambio normativo entre este análisis y la construcción — ya ocurrió dentro de este mismo proyecto con los tramos de Renta (REQ-012 sembró tres tablas oficiales por decreto DGII durante su propia construcción); no se puede descartar que ISSS/AFP cambien su mecanismo de "Planilla Única" en el mismo periodo.

### Supuestos

- El motor de cálculo (ISSS/AFP/Renta) ya es correcto y no requiere cambios — este es un requerimiento de capa de reporte, no de cálculo.
- Moneda única USD, alcance geográfico único El Salvador (D-01).
- El "ciclo de nómina pagado" del pedido corresponde al estado `CERRADA` ya modelado (D-03) — no hay un estado de "pago ejecutado" separado en el sistema hoy.
- Los tres reportes son para consumo del contador/analista, no para el empleado (no hay autoservicio en este alcance).

### Dependencias

- REQ-012 (motor y modelo de datos de planilla) — ya construido, no se modifica.
- Necesidad N7 del análisis de alcance diferido (integraciones externas) — ya diferida; este requerimiento no la reabre.
- Confirmación del contador/negocio sobre los formatos exactos esperados de cada reporte (§17) antes de pasar a plan técnico.

---

## 17. Preguntas al negocio — P-01…P-13 RESPONDIDAS (ratificación 2026-07-16/17) — sin preguntas de negocio pendientes

**8 de las 13 recomendaciones originales fueron aceptadas tal cual** (P-01, P-04, P-06, P-07, P-08, P-09, P-10, P-12) o como aclaración de propósito sin cambiar el diseño (P-05). **3 fueron ratificadas con un alcance mayor al recomendado por este análisis** (P-02: plantilla oficial exacta, no detalle tabular; P-03: bloquea la generación de nómina de la empresa, no solo estos reportes; P-11: el mismo bloqueo baja a nivel de empleado). **P-13 rechazó explícitamente la recomendación** de este análisis (periodo de transición) — el negocio prefiere activar el gate duro desde el despliegue y acepta la interrupción operativa que eso implica. Todas las ampliaciones ya están propagadas al resto del documento (§0, §6, §9, §16, §18). **No quedan preguntas de negocio abiertas** — el único pendiente real es recibir del negocio el archivo de plantilla oficial de F-14/Planilla Única (P-02), que es un insumo externo, no una decisión.

| # | Ámbito | Pregunta | Recomendación de este análisis | Respuesta del negocio (2026-07-16) |
|---|---|---|---|---|
| **P-01** | Periodicidad | ¿F-14 y Planilla Única se generan por **corrida individual** o se **consolidan por mes calendario**? | Consolidar F-14 y Planilla Única por mes calendario; Planilla Patronal por corrida individual. | ✔ **«Consolidar F-14 y planilla única por mes, la patronal por corrida individual»** — aceptada tal cual. |
| **P-02** | Formato de salida | ¿Reproducir la plantilla oficial celda por celda, o basta un detalle tabular? | Partir de un detalle tabular y validar en un piloto si además se requiere plantilla oficial. | ⚠️ **«Se deben replicar las planillas oficiales»** — **más exigente que la recomendación**: no hay etapa intermedia tabular, el layout final ES la plantilla oficial. Confirma G-05 como bloqueante real, no hipotético; nueva dependencia dura de recibir el archivo oficial (§18). |
| **P-03** | Perfil legal patronal | ¿Quién lo captura y con qué formato/validación exactos? | Nuevo maestro "Perfil legal patronal", registro por empresa, editable solo por administrador. | ⚠️ **«Aceptada la propuesta con un matiz importante: no se pueden generar nóminas sin esa información»** — **amplía el alcance**: el gate no es solo de estos 3 reportes, es de la generación de planilla misma (RF-006, impacta REQ-012 ya certificado). |
| **P-04** | Datos previsionales del empleado | ¿NUP ISSS y cuenta AFP son obligatorios? | Sí en la práctica; advierten pero no bloquean el reporte completo. | ✔ **«Si son obligatorios»** — aceptada; el alcance del bloqueo quedó resuelto por P-11 (fila siguiente). |
| **P-05** | Identidad de "Planilla Patronal" | ¿Formulario oficial específico o listado gerencial general? | Listado gerencial de costo patronal — no se identificó un formulario oficial numerado. | ✔ **«Es un control interno para validar lo que se paga al gobierno»** — confirma la lectura de este análisis y precisa su propósito: es el respaldo interno para verificar F-14/Planilla Única. |
| **P-06** | Estado de corrida elegible | ¿Solo `CERRADA`, o también `AUTORIZADA`? | Solo `CERRADA`. | ✔ **«Solo CERRADAS»** — aceptada tal cual. |
| **P-07** | Formato adicional | ¿Además de Excel, se necesita PDF? | Solo Excel por ahora. | ✔ **«Solo Excel»** — aceptada tal cual. |
| **P-08** | Retroactividad de tasas/tramos | ¿El reporte usa las tasas vigentes al cálculo original, o las actuales? | Siempre las persistidas en la corrida original. | ✔ **«Siempre las persistidas en la corrida original»** — aceptada tal cual. |
| **P-09** | Consolidación multi-frecuencia | ¿El reporte mensual consolida grupos de distinta frecuencia de nómina? | Sí, agrupa por empresa + mes sin importar la frecuencia de origen. | ✔ **«Consolida ambos grupos»** — aceptada tal cual. |
| **P-10** | Permiso de acceso | ¿Reutilizar `ViewPayrollRuns` o crear uno dedicado? | Permiso dedicado. | ✔ **«Permiso dedicado»** — aceptada tal cual; nombre de trabajo `ViewComplianceReports` (a confirmar en plan técnico). |
| **P-11** *(derivada de propagar P-03/P-04)* | Bloqueo a nivel de empleado | P-03 ratificó que la falta del perfil legal **patronal** bloquea generar nómina de la **empresa**. P-04 confirmó que NUP ISSS/cuenta AFP son obligatorios, pero sin repetir ese mismo verbo de bloqueo. ¿Un empleado sin NUP ISSS/cuenta AFP debe bloquear la generación de **su propia** línea de nómina (mismo criterio que P-03), o solo genera advertencia visible en el reporte y en su expediente (RN-06 tal como quedó redactada)? | Advertencia visible y prominente, sin bloquear — por consistencia con `WarningCodesJson` y porque bloquear por empleado individual tiene mayor impacto operativo día a día. | ⚠️ **«Debe bloquear la nómina, sin eso no procede»** — **más exigente que la recomendación**, mismo sentido que P-03: el bloqueo de empresa baja a nivel de empleado (RN-10, nueva). |
| **P-12** *(derivada de P-11)* | Mecánica exacta del bloqueo | El bloqueo ratificado en P-11, ¿excluye solo la línea/inclusión de **ese empleado específico** (el resto de la corrida se genera con normalidad), o bloquea **la corrida completa** hasta que todos los empleados tengan NUP ISSS/cuenta AFP? | Bloquear solo la línea/inclusión de ese empleado — evita que un dato administrativo faltante de una persona retrase el pago de toda la plantilla; el empleado queda excluido con advertencia bloqueante hasta completar su dato. | ✔ **«Bloquear solo la línea/inclusión de ese empleado»** — aceptada tal cual (2026-07-17). |
| **P-13** *(derivada de P-11)* | Periodo de transición | NUP ISSS y cuenta AFP son campos que **no existen hoy** — el 100% de los empleados actuales los tiene vacíos. Si el bloqueo (P-11) se activa el mismo día que se despliega esta funcionalidad, **¿podría impedir pagar a la plantilla completa de cada empresa en el primer ciclo?** ¿Se necesita un periodo de transición (advertencia sin bloqueo por N ciclos, con fecha de corte anunciada) antes de activar el bloqueo real? | Sí, se necesita transición: activar primero en modo advertencia, anunciar una fecha de corte, y solo bloquear a partir de esa fecha — evita interrumpir la operación del cliente el día del despliegue. | ⚠️ **«Sin periodo de transición se acepta interrumpir la operación»** — **rechaza la recomendación** (2026-07-17): el negocio elige el gate duro desde el despliegue. La captura de datos de la plantilla existente pasa a ser un prerequisito operativo de despliegue, no una función del sistema. |

---

## 18. Recomendaciones del Analista de Negocio

> Actualizadas tras la ratificación (2026-07-16); las recomendaciones 1, 2, 5 y 6 originales se mantienen — se agregan 3 nuevas (7, 8, 9) que responden directamente a los 2 ajustes de alcance de la ratificación (P-02, P-03).

1. **Empezar por Planilla Patronal (RF-003)** como piloto/MVP: sigue siendo el candidato más claro tras la ratificación — no depende de NUP ISSS/cuenta AFP, no está sujeto al requisito de plantilla oficial exacta (P-02 aplica a F-14/Planilla Única, no a este), y reutiliza directamente `TotalEmployerCost` y las líneas `PagoPatronal` ya calculadas.
2. **Resolver RF-006 (perfil legal patronal) primero, sin importar el orden de los reportes** — sigue siendo un bloqueante transversal, y tras P-03 es más urgente todavía: bloquea la generación de nómina, no solo los reportes.
3. ~~Hacer un spike corto con el contador antes de estimar RF-001/RF-002, sobre P-02 y P-01~~ **YA RESUELTO por la ratificación** — P-01 y P-02 quedaron respondidos; el spike que sigue pendiente es distinto (ver recomendación 7).
4. **Construir Planilla Única (RF-002) al final**, se mantiene: depende del dato nuevo más grande (NUP ISSS + cuenta AFP, ahora confirmados obligatorios por P-04) y es la más expuesta al riesgo normativo.
5. **No mezclar este requerimiento con una integración real de presentación electrónica** — se mantiene sin cambios (necesidad N7, ya diferida).
6. **Dejar una advertencia visible en pantalla, no solo en el Excel** — se mantiene, y gana importancia tras P-04 (NUP ISSS/cuenta AFP ahora obligatorios).
7. **[Nueva, crítica] Solicitar al negocio, como insumo previo al plan técnico, el archivo/plantilla oficial exacta de F-14 y de Planilla Única (y el formato interno de Planilla Patronal, si existe uno estándar más allá del listado tabular).** Sin estos archivos no se puede dimensionar con confianza el desarrollo del exportador (RF-004) ni el layout final de RF-001/RF-002 — es la dependencia externa más determinante del esfuerzo real, y no es algo que el equipo técnico pueda resolver por sí solo.
8. **[Nueva] Tratar el nuevo guard de generación de planilla (RF-006, sobre `PayrollRun.Create()` de REQ-012) como su propia unidad de trabajo con regresión explícita contra la suite dorada/E2E que ya certificó ese módulo** — es la parte de mayor riesgo técnico de toda la ratificación, porque toca código ya cerrado y con el backlog marcado 15/15 completo.
9. ~~Ratificar P-11 antes de escribir el plan técnico de RF-007~~ **YA RESUELTO (2026-07-16): P-11 confirmó que el bloqueo SÍ se replica a nivel de empleado individual** — «debe bloquear la nómina, sin eso no procede».
10. ~~No activar el bloqueo de P-11 en producción sin antes resolver P-13~~ **YA RESUELTO (2026-07-17), en sentido contrario al recomendado: el negocio eligió NO tener periodo de transición** — «sin periodo de transición se acepta interrumpir la operación» (P-13), y confirmó que el bloqueo excluye solo la línea de ese empleado, no toda la corrida (P-12). **La recomendación cambia de forma, no de fondo**: dado que el sistema no absorberá el riesgo con un modo de transición, la captura de NUP ISSS/cuenta AFP de la plantilla existente debe tratarse como un **prerequisito de despliegue obligatorio** — un checklist explícito de "Pendientes de despliegue" en el backlog (campaña de captura por RRHH/administrador, empresa por empresa, antes de activar esta funcionalidad), para que ningún cliente descubra el corte el día que deja de poder pagarle a alguien.

---

## Anexo A — Correspondencia y referencias

### A.1 Mapa de entrega (RF → dato/código fuente)

| RF | Dato o código fuente | Estado |
|---|---|---|
| RF-001 F-14 | Líneas `ConceptCode=RENTA`; tabla `IncomeTaxWithholdingBracket` | Cálculo ya existe — falta agregación mensual (ratificada) + NIT patronal (RF-006) + **plantilla oficial exacta aún no recibida (P-02, bloqueante)** |
| RF-002 Planilla Única | Líneas `ISSS`/`AFP`/`ISSS_PATRONAL`/`AFP_PATRONAL`; `PersonnelFile.AfpCode` | Cálculo ya existe — faltan NUP ISSS + cuenta AFP obligatorios (RF-007, P-04) + agregación mensual (ratificada) + **plantilla oficial exacta aún no recibida (P-02, bloqueante)** |
| RF-003 Planilla Patronal | `PayrollRun.TotalEmployerCost`; líneas `LineClass=PagoPatronal` | Existe completo — sin gaps de datos ni dependencia de plantilla externa (P-05: control interno) |
| RF-004 Exportación Excel | `ReportExportFileWriter` / `ReportExportDeliveryService` | **Confirmado insuficiente para F-14/Planilla Única (P-02)** — necesita modo de plantilla/posicional nuevo; suficiente tal cual para Planilla Patronal |
| RF-005 Selección por ciclo pagado | `PayrollRun.StatusCode=Cerrada`; `PayrollPeriodDefinition.Month`/`Year` | Existe — P-01/P-06 ratificadas sin cambios |
| RF-006 Perfil legal patronal | — | **Nuevo** — no existe nada hoy en `Company.cs` (G-01); **ahora también gate de `PayrollRun.Create()` (P-03)** |
| RF-007 Identificación previsional del empleado | `PersonnelFileIdentification`; `PersonnelFile.AfpCode` | Parcial — faltan NUP ISSS y cuenta AFP, **confirmados obligatorios y bloqueantes de nómina, solo por línea de empleado, sin transición (G-02/G-03, P-04, P-11, P-12, P-13 — todas ratificadas)**; requiere campaña de captura previa al despliegue |

### A.2 Máquina de estados relevante (ya existente, sin cambios)

```
GENERADA --Authorize()--> AUTORIZADA --Close()--> CERRADA   ← única elegible para los 3 reportes (RN-01)
   ^                          |
   +---------Return()---------+   (regresa a GENERADA con motivo del autorizador)

GENERADA/AUTORIZADA --Annul()--> ANULADA   ← excluida de los 3 reportes
```

### A.3 Verificaciones de código de este análisis (2026-07-16)

- `src/CLARIHR.Domain/Payroll/PayrollRun.cs` — cabecera de corrida: `StatusCode` (línea 94, default `Generada`), `TotalIncome/TotalDeductions/TotalEmployerCost/TotalNet` (líneas 118-126), `PaymentDate` (líneas 84-90). `PayrollRunLine` (línea 332) con `ConceptCode`, `LineClass` (líneas 408-419, CHECK sin default), `FinalAmount => OverrideAmount ?? CalculatedAmount` (línea 442).
- `src/CLARIHR.Domain/Payroll/PayrollStatuses.cs:12-33` — `PayrollRunStatuses.Generada/Autorizada/Cerrada/Anulada`; `PayrollPeriodStatuses:42-50`.
- `src/CLARIHR.Application/Features/Payroll/PayrollCalculation.Rules.cs:19-29` — `PayrollEngineConceptCodes`: `Salario`, `HorasExtra`, `Isss`, `Afp`, `Renta`, `IsssPatronal`, `AfpPatronal`, `Incaf`. `PayrollCalculatedLine` (líneas 152-165). `PayrollContributionScheme` (líneas 62-65) — una sola tasa empleado/patronal por esquema, **sin campo de comisión AFP separado** (G-06). Cálculo de Renta: `rentaBase = rentaIncomeBase - isssAmount - afpAmount` (línea 381) contra `PayrollTaxBracket`.
- `src/CLARIHR.Domain/Compensation/IncomeTaxWithholdingBracket.cs` — tabla oficial de tramos de Renta por frecuencia, ya sembrada por decreto DGII (REQ-012) — es efectivamente la base legal que F-14 necesita reportar.
- `src/CLARIHR.Domain/Leave/PayrollPeriodDefinition.cs` — `Year`, `Number`, `Label`, `StartDate/EndDate`, `PayPeriodTypeCode`, `Month` (campo contable 1-12, ya sembrado), `StatusCode`.
- `src/CLARIHR.Api/Controllers/PayrollRunsReportingController.cs` — endpoints existentes: `POST .../payroll-runs/query`, `GET .../payroll-runs/export` (línea 68), `GET .../payroll-runs/{id}/lines/export` (línea 111), `GET .../payroll-runs/{id}/bank-reconciliation/export` (línea 157), `POST .../payroll-runs/employee-history/query`. Ninguno produce hoy un reporte agregado mensual ni con layout de formulario legal.
- `src/CLARIHR.Application/Features/Payroll/PayrollRunsReporting.cs:59-124` — filas de exportación ya existentes (`CorridaPlanillaExportRow`, `ImpresionPlanillaExportRow`, `ConciliacionBancariaExportRow`) — ninguna incluye DUI, NIT, NUP ISSS ni cuenta AFP (verificado leyendo las proyecciones `Select(...)` en `src/CLARIHR.Infrastructure/Payroll/PayrollRunRepository.cs:229,267,369`).
- `src/CLARIHR.Application/Features/Reports/ReportExportFileWriter.cs:78-159` — escritor OOXML manual (un solo `ZipArchive`, una sola hoja, celdas `inlineStr`, columnas por reflexión sobre las propiedades públicas del DTO de fila) — sin soporte de encabezados fijos ni celdas combinadas (G-05). Formatos: csv/xlsx/json/pdf.
- `src/CLARIHR.Api/Common/ReportExportDeliveryService.cs` — entrega con límite de filas síncronas y auditoría, usado por todas las bandejas existentes.
- `src/CLARIHR.Domain/PersonnelFiles/PersonnelFile.cs:963-1044` — `PersonnelFileIdentification` (colección genérica: `IdentificationType`, `IdentificationNumber`, `NormalizedIdentificationNumber`, `IssuedDate`, `ExpiryDate`, `Issuer`, `IsPrimary`); tipos catalogados en `PersonnelReferenceCatalog.cs:33-39` (`DUI`, `NIT`, `PASSPORT`, `RESIDENT_CARD`) — **sin tipo `NUP_ISSS` hoy** (G-02). `PersonnelFile.AfpCode` (línea 98) — identifica la AFP, **sin campo de número de cuenta** (G-03).
- `src/CLARIHR.Domain/Afps/AfpCatalogItem.cs` — maestro de AFP por país (`Code, Name, Abbreviation, Address, Phone, Fax, ContactName`) — catálogo, sin campo por-empleado.
- `src/CLARIHR.Domain/Companies/Company.cs` — campos completos: `Name, Slug, CountryCode, CountryCatalogItemId, Status, CreatedByUserPublicId, CompanyTypeCatalogItemId, IsBillable, BillableSinceUtc`. **Confirmado ausente** (grep repo-wide sin resultados): `RazonSocial`, `NitPatronal`, `NrcIsss`, dirección, actividad económica (G-01). `src/CLARIHR.Domain/LegalRepresentatives/LegalRepresentative.cs` modela una **persona** (representante legal), no la identidad tributaria de la empresa misma.
- `src/CLARIHR.Application/Features/PersonnelFiles/Common/PersonnelFilePolicies.cs:419,427,434` — `ViewPayrollRuns`, `ManagePayrollRuns`, `AuthorizePayrollRuns`. Gate real por handler: `EnsureCanViewPayrollRunsAsync` (`IPersonnelFileAuthorizationService.cs:517`); `PayrollRunsReportingController` deliberadamente sin `[AuthorizationPolicySet]` de clase — cada handler valida individualmente ("los datos de planilla exponen salarios; la lectura corporativa es solo de RRHH").
- Búsqueda cruzada (`grep -i` sobre los 29 archivos de `docs/business/*.md` + `docs/backlog-requerimientos.md`): **cero menciones** de "F-14", "Planilla Única", "Planilla Patronal" en cualquier análisis o bitácora previa de este proyecto. "DGII" aparece solo 2 veces con sentido sustantivo, ambas fuera de este alcance: como fuente de los tramos oficiales de Renta ya sembrados (REQ-012) y como la entidad ante la que la conciliación fiscal anual ocurre **fuera del sistema** (`analisis-liquidacion-empleado.md`).
