# Guía de integración frontend — Liquidación de Personal (Nueva liquidación · Escenario)

| | |
|---|---|
| **Módulo** | Expediente de Personal → Retiros → **Liquidación** (`PersonnelFiles`) |
| **Documentos hermanos** | `docs/business/analisis-liquidacion-empleado.md` (D-01…D-20 + P-01…P-03 ratificadas) · `docs/technical/plan-tecnico-liquidacion-empleado.md` · `guia-integracion-frontend-retiro-definitivo.md` (el retiro es el ancla de la liquidación real) |
| **Permisos** | `PersonnelFiles.ViewSettlements` (lecturas/exports) · `PersonnelFiles.ManageSettlements` (crear/editar/emitir/anular; incluye escenarios). `PersonnelFiles.Admin` y el super-admin son supersets. **Sin autoservicio del empleado en Fase 1.** |
| **Convenciones** | Prefijo `api/v1` · errores con `extensions.code` bilingüe · mutaciones con `If-Match` (falta → 400, obsoleto → 409; el token nuevo regresa en `ETag` y en `concurrencyToken`) · enums como strings · `Guid Id` serializa como `publicId` |

---

## 1. Modelo mental (2 minutos)

- **Una liquidación es POR PLAZA** (D-10): un empleado con 2 plazas retirado genera hasta 2 liquidaciones, cada una con su salario, sus conceptos y su centro de costo. La "vista del empleado" es la lista.
- **Dos modos, una entidad** (`kind`):
  - `Liquidacion` — real, anclada al **retiro EJECUTADO** del empleado; hereda fecha/categoría/motivo **en solo lectura** y solo acepta plazas que esa baja cerró. Ciclo `BORRADOR → EMITIDA → ANULADA` (solo `BORRADOR` es editable; `EMITIDA` es inmutable — corregir = anular + crear de nuevo; `ANULADA` libera el slot retiro×plaza).
  - `Escenario` — simulación sobre una **plaza activa** de un empleado activo con **fecha estimada** y motivo hipotético. `statusCode: null`, siempre editable, se elimina con DELETE (soft). **Cero efectos** — márquenlo SIEMPRE como "SIMULACIÓN — SIN EFECTOS" (R-10); sugerimos que el filtro default de la bandeja sea `kind=Liquidacion`.
- **El servidor calcula TODO**: el cliente nunca manda montos calculados ni totales — solo insumos (días), overrides auditados (monto + nota obligatoria), inclusiones/exclusiones y líneas manuales. **Cada guardado recalcula** y devuelve el documento completo.
- **El detalle son líneas con `conceptClass`**: agrúpenlas en 3 secciones — `Ingreso`, `Descuento`, `PagoPatronal` — más los bloques calculados **Reserva/Provisión** (`provisionTotal` + centro de costo) y **Resumen** (5 totales).
- **El salario mínimo vive en la ficha** del empleado (`minimumMonthlyWage` del `PUT/GET …/employment-information`). La liquidación lo copia como snapshot; si la ficha (bloqueada en retirados) no lo tiene, se envía `minimumMonthlyWage` al crear (si falta en ambos → 422 `SETTLEMENT_MINIMUM_WAGE_MISSING`).
- **El solicitante solo puede ser RRHH** (D-06): omitan `requesterFilePublicId` y el backend usa el expediente del gestor autenticado. Un expediente distinto solo pasa si pertenece al área funcional de RRHH configurada en las preferencias.

## 2. Catálogos

| Necesidad | Endpoint |
|---|---|
| Conceptos de liquidación (clase, matriz ISSS/AFP/Renta, regla de exención, motor-vs-manual, tasa patronal) — para pickers de líneas manuales/re-añadir | `GET api/v1/settlement-concepts?countryCode=SV&conceptClass=Ingreso\|Descuento\|PagoPatronal` |
| Estados de liquidación (badges) | `GET api/v1/general-catalogs/settlement-statuses?countryCode=SV` → `BORRADOR`, `EMITIDA`, `ANULADA` (validación); el escenario no tiene estado |
| Categoría / motivo (para el ESCENARIO; la real los hereda del retiro) | `GET api/v1/reference-catalogs/retirement-categories?countryCode=SV` y `retirement-reasons?countryCode=SV&parentCode={categoria}` |

Seed SV de conceptos (17): ingresos `SALARIO`, `VACACION_PROPORCIONAL`, `AGUINALDO_PROPORCIONAL`, `INDEMNIZACION`, `RENUNCIA_VOLUNTARIA`, `BONO_PENDIENTE`, `COMISION_PENDIENTE`, `HORAS_EXTRAS_PENDIENTES`, `OTRO_INGRESO` · descuentos `ISSS`, `AFP`, `RENTA`, `DESCUENTO_EXTERNO`, `OTRO_DESCUENTO` · patronales `ISSS_PATRONAL`, `AFP_PATRONAL`, `INCAF`.

## 3. Endpoints

Base por expediente: `api/v1/personnel-files/{publicId}/settlements`

| Verbo y ruta | Uso | Notas |
|---|---|---|
| `GET …` | Lista del expediente (reales + escenarios, con líneas) | — |
| `GET …/{settlementPublicId}` | Detalle completo | `ETag` |
| `POST …` | **Crear liquidación real** | Body: `{ assignedPositionPublicId, requestDate, requesterFilePublicId?, notes?, minimumMonthlyWage? }`. El backend localiza el retiro EJECUTADO; la plaza debe ser una de las cerradas por él. 200 con el cálculo completo. |
| `POST …/scenarios` | **Crear escenario** | Body: `{ assignedPositionPublicId, estimatedRetirementDate, retirementCategoryCode, retirementReasonCode, requestDate, requesterFilePublicId?, notes?, minimumMonthlyWage? }` (plaza ACTIVA). |
| `PUT …/{id}` | Encabezado + parámetros (+ supuestos del escenario) | `If-Match`. Body: `{ requestDate, parameters: { minimumMonthlyWage, … }, requesterFilePublicId?, notes?, estimatedRetirementDate?, retirementCategoryCode?, retirementReasonCode? }`. Los 3 últimos SOLO en escenarios. En `parameters`, todo lo omitido cae a los defaults ratificados (4×, 2×, 15+30%, 15 días/año, 2 años, exención 2×, 30/365; `aguinaldoDays: 0` = tramo automático 15/19/21). |
| `PUT …/{id}/lines/{linePublicId}` | Ajustar UNA línea | `If-Match` = token de la LIQUIDACIÓN. Body (todo opcional): `{ isIncluded?, unitsOrDays?, clearUnitsOverride?, overrideAmount?, overrideReason?, clearOverride?, description?, manualAmount? }`. `overrideAmount` exige `overrideReason` (422 `SETTLEMENT_OVERRIDE_NOTE_REQUIRED`). Días fijados (`unitsOverridden: true`) sobreviven recálculos; `clearUnitsOverride` los libera. |
| `POST …/{id}/lines` | Línea manual | `{ conceptCode, description, amount }` — solo conceptos manuales del catálogo. |
| `DELETE …/{id}/lines/{linePublicId}` | Quitar línea | Re-creable vía regenerate. |
| `POST …/{id}/lines/regenerate` | **Regenerar** | Re-lee la configuración (salario, bonos, externas, tasas, tramos) y reconstruye las sugeridas. **Descarta manuales/exclusiones/overrides** — pidan confirmación en UI. |
| `PATCH …/{id}/issuance` | **Emitir** | `{ confirmNegativeNet }`. Congela el documento y journalea `LIQUIDACION`. Neto < 0 sin flag → 422 `SETTLEMENT_NET_NEGATIVE_CONFIRMATION_REQUIRED`. |
| `PATCH …/{id}/annulment` | **Anular** | `{ reason? }` — obligatorio desde `EMITIDA` (422 `SETTLEMENT_ANNUL_REASON_REQUIRED`). Libera el slot retiro×plaza. |
| `DELETE …/{id}` | Eliminar ESCENARIO (soft) | `If-Match` = token del escenario. 204. Una real → 422 (se anula, no se borra). |
| `GET …/{id}/document?format=pdf\|xlsx\|csv\|json` | **Boleta / export individual** | `pdf` = boleta estándar; tabulares = filas seccionadas (`ENCABEZADO`/secciones/`RESUMEN`). Escenarios siempre marcados `SIMULACIÓN — SIN EFECTOS`. |

Bandeja empresa: `POST api/v1/companies/{companyId}/settlements/query` (filtros: `kind`, `statusCode`, `categoryCode`, `reasonCode`, `employeeId`, `requestFromUtc/ToUtc`, `retirementFromUtc/ToUtc`, `search`, paginación) → `{ items, pageNumber, pageSize, totalCount, statusCounts }` (escenarios cuentan bajo `ESCENARIO`). Export: `GET api/v1/companies/{companyId}/settlements/export?format=xlsx&…mismos filtros…&q=` (413 al exceder el límite síncrono).

## 4. La respuesta (shape que pinta la pantalla)

Campos clave de `GET/POST/PUT` (además de los evidentes):

- **Identidad/ancla**: `publicId`, `kind`, `statusCode` (null en escenario), `retirementRequestPublicId` (null en escenario), `assignedPositionPublicId`, `positionName`, `plazaStartDate`, `costCenterPublicId/costCenterName` (destino de la provisión; null ⇒ warning `SETTLEMENT_WARNING_NO_COST_CENTER`).
- **Parámetros snapshot** (editables vía PUT): `minimumMonthlyWage`, `indemnityCapMultiplier`, `resignationCapMultiplier`, `vacationDays`, `vacationPremiumPercent`, `aguinaldoDays` (0 = automático), `resignationBenefitDays`, `resignationMinimumServiceYears`, `aguinaldoExemptionMultiplier`, `monthDivisorDays`, `yearDivisorDays`.
- **Derivados** (solo lectura): `monthlyBaseSalary`, `seniorityYears/seniorityDays` (¡de la PLAZA, desde su `startDate` — P-01!), `cappedMonthlySalaryIndemnity/Resignation` (el "valor de salario máximo": `min(salario, N×mínimo)`).
- **Totales (Resumen)**: `totalIncomes`, `totalDeductions`, `netPay`, `totalEmployerCharges`, `provisionTotal` (= ingresos + patronales), `currencyCode`.
- **`lines[]`**: `publicId`, `conceptClass`, `conceptCode`, `conceptName`, `isSystemCalculated`, `calculationBase`, `unitsOrDays`, `unitsOverridden`, `calculatedAmount`, `exemptAmount`, `taxableExcessAmount` (exceso que fue a Renta — muéstrenlo en el tooltip), `overrideAmount/overrideReason`, `finalAmount`, `isIncluded`, `isZeroByLaw` + `zeroReasonCode` (p. ej. renuncia < 2 años → `SERVICIO_MINIMO_NO_CUMPLIDO`: pinten la línea en 0 con el motivo, NO la oculten), `calculationDetail` (traza legible de la fórmula), `counterpartyName` (descuento externo), `sortOrder`.
- **`warnings[]`**: `{ code, conceptCode? }` — no bloqueantes, para banners: `SETTLEMENT_WARNING_RENTA_BRACKETS_MISSING` (tenant sin tabla de Renta — la línea va en 0), `…_ZERO_BY_LAW`, `…_NET_NEGATIVE`, `…_BOTH_COMPENSATIONS`, `…_NO_COST_CENTER`.
- Emisión/anulación: `issuedAtUtc/issuedByUserPublicId`, `annulledAtUtc/annulmentReason`.

## 5. Flujos de pantalla sugeridos

**Nueva liquidación**: al elegir al empleado retirado, listar sus plazas liquidables = las del retiro ejecutado (la creación valida contra `ClosedRecords`; un 422 `SETTLEMENT_POSITION_NOT_IN_RETIREMENT` significa plaza equivocada). Crear → render de 5 secciones → depurar (excluir/ajustar días/override con nota/manuales; cada guardado devuelve el recálculo) → **Emitir** (confirmación; badge EMITIDA congela la edición) → botones "Boleta PDF" y "Excel" → si hay error: **Anular** (motivo) y crear de nuevo.

**Escenario**: mismo formulario con fecha estimada + categoría/motivo elegibles sobre plazas ACTIVAS; iterar fecha/motivo (el PUT recalcula) y exportar para gerencia. Recordatorio visual permanente de SIMULACIÓN.

**Reversión de retiro** (pantalla de retiros): si la reversión devuelve 422 `RETIREMENT_REVERSAL_BLOCKED_BY_SETTLEMENT`, guíen al usuario a anular la liquidación EMITIDA primero. Los BORRADOR se anulan solos con motivo "Reversión de retiro".

**Ficha del empleado**: agreguen el campo **`minimumMonthlyWage`** al formulario de información de empleo (`PUT …/employment-information`, opcional > 0) — es la fuente del cálculo (§17.16 ratificada).

## 6. Códigos de error del módulo (422 salvo indicación)

| Código | Cuándo |
|---|---|
| `SETTLEMENT_RETIREMENT_NOT_EXECUTED` / `SETTLEMENT_RETIREMENT_REVERTED` | Crear real sin baja ejecutada / sobre baja revertida |
| `SETTLEMENT_POSITION_NOT_IN_RETIREMENT` / `SETTLEMENT_POSITION_INVALID` | Plaza fuera del retiro / plaza inválida-inactiva (escenario) |
| `SETTLEMENT_ALREADY_EXISTS_FOR_POSITION` | Duplicado por (retiro × plaza) — anular primero |
| `SETTLEMENT_SCENARIO_EMPLOYEE_RETIRED` | Escenario sobre retirado (corresponde la real) |
| `SETTLEMENT_BASE_SALARY_MISSING` | La plaza no tiene `SALARIO_BASE` activo |
| `SETTLEMENT_MINIMUM_WAGE_MISSING` | Sin mínimo en ficha ni override |
| `SETTLEMENT_REQUESTER_NOT_HR` | Solicitante fuera de RRHH (D-06) |
| `SETTLEMENT_SELF_ACTION_FORBIDDEN` (**403**) | El empleado sujeto gestiona su propia liquidación |
| `SETTLEMENT_STATE_RULE_VIOLATION` | Editar EMITIDA, borrar una real, emitir no-borrador… |
| `SETTLEMENT_DATE_INCOHERENT` / `SETTLEMENT_PARAMETERS_INVALID` (400) | Fechas/parámetros inválidos |
| `SETTLEMENT_OVERRIDE_NOTE_REQUIRED` | Override sin nota |
| `SETTLEMENT_CONCEPT_INVALID` | Línea manual con concepto no manual/inactivo |
| `SETTLEMENT_ISSUE_REQUIRES_INCOME` / `SETTLEMENT_NET_NEGATIVE_CONFIRMATION_REQUIRED` | Guards de emisión |
| `SETTLEMENT_ANNUL_REASON_REQUIRED` | Anular EMITIDA sin motivo |
| `SETTLEMENT_NOT_FOUND` / `SETTLEMENT_LINE_NOT_FOUND` (404) | Referencias inexistentes |
| `RETIREMENT_REVERSAL_BLOCKED_BY_SETTLEMENT` | En la pantalla de reversión de retiros |

Transversales: 400 sin `If-Match` · 409 token obsoleto · 403 sin permiso · 413 export excedido · `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` (400).

## 7. Checklist de despliegue (el módulo es tan bueno como sus datos)

1. **Tabla de Renta 2026** por empresa: `PUT api/v1/income-tax-brackets` (periodo `MENSUAL`) — sin tramos, la línea RENTA va en 0 con warning.
2. **Salario mínimo en las fichas** de los empleados (nuevo campo de employment-information).
3. **`SALARIO_BASE` por plaza** en la configuración de compensación de los empleados próximos a liquidar.

> **Nota de alcance (FA-1):** la liquidación **calcula y documenta**; no paga ni escribe en la planilla externa. La emisión es un acto documental con journal `LIQUIDACION`.
