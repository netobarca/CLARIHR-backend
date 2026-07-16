# Guía Frontend — Motor de planillas (REQ-012 · REQ-013 · REQ-014 · REQ-015)

> **Fuente de verdad del contrato**: `docs/technical/api/openapi.yaml` (volcado del swagger real).
> Esta guía mapea las superficies, los flujos y las trampas; los esquemas exactos viven en el contrato.

## 0. Convenciones (aplican a TODO lo de abajo)

- Base `api/v1`. Identidades públicas: todo id en el wire es `publicId`/`xxxPublicId` (jamás ids internos).
- **Concurrencia**: toda mutación lleva `If-Match` con el `concurrencyToken` vigente (falta → `400`, viejo → `409 CONCURRENCY_CONFLICT`). El token viaja en el body y en el header `ETag` de los GET/PATCH.
- **Errores**: ProblemDetails con el código estable en el miembro RAÍZ `code` (p. ej. `PAYROLL_RUN_ALREADY_ACTIVE`). Mensajes bilingües EN/ES vía `Accept-Language`.
- Enums viajan como string. Fechas `yyyy-MM-dd` (DateOnly) o ISO-8601 UTC (DateTime).
- Montos: `decimal` con 2 decimales (AwayFromZero), moneda en `currencyCode` (hoy `USD`).

## 1. Configuración (Ola A)

| Superficie | Endpoints | Permiso |
|---|---|---|
| **Maestro Nómina** | `POST/GET/PUT companies/{companyId}/payroll-definitions` + `PATCH …/{id}/activation·inactivation` | `PayrollConfiguration.Read`/`.Manage` |
| **Calendario anual** | `POST companies/{companyId}/payroll-definitions/{id}/periods/generate?year=` (idempotente; `created`/`skipped`/`notDerivable`) | `LeaveConfiguration` (los periodos siguen ahí — contrato estable) |
| **Periodos** | `GET/POST/PUT companies/{companyId}/payroll-periods` — campos nuevos (nómina/corte/pago/mes/ventanas/estado) opcionales como grupo: si viaja `payrollDefinitionPublicId` se exige el grupo completo | ídem |
| **Jornadas** | `POST/GET/PUT companies/{companyId}/work-schedules` + `PATCH activation/inactivation` + **`POST companies/{companyId}/payroll-configuration/load-template`** (siembra la jornada legal 44 h `JORNADA_ORDINARIA`) | `PayrollConfiguration.*` |

Notas FE:
- `payroll-types` es el catálogo de "tipo de planilla" (QUINCENAL/MENSUAL/SEMANAL/POR_OBRA…).
- La plaza referencia jornada por `workdayCode`; si viaja debe existir ACTIVA (`422 WORK_SCHEDULE_INVALID`).
- Ventanas de HE del periodo: si el periodo cuelga de nómina y la ventana está cerrada, el registro de HE responde `422 OVERTIME_ENTRY_NOT_ALLOWED_FOR_PERIOD` / `OVERTIME_ENTRY_WINDOW_CLOSED`.

## 2. Generación (REQ-012 §3.4 — permisos `PersonnelFiles.ViewPayrollRuns` / `ManagePayrollRuns`)

```
POST companies/{companyId}/payroll-runs/preflight   ← vista previa SIN escribir
POST companies/{companyId}/payroll-runs             ← genera (201 + Location al GET)
body: { payrollDefinitionPublicId, payrollPeriodPublicId, employeeIds?: Guid[] }
```

- **Pre-flight** = misma derivación que generar: población, conteos por módulo, totales proyectados y `warnings[]`. Es la herramienta de adopción: el primer pre-flight lista los **rezagos históricos** de TNT/amonestaciones (`carryoverInputs` + warning `PAYROLL_WARNING_CARRYOVER_INPUT`) para excluir lo ya pagado por la nómina externa.
- `employeeIds` opcional restringe la población (REQ-014 P-01: la selección fina se ejerce sobre la corrida GENERADA).
- **Una corrida ACTIVA por nómina × periodo**: segundo intento → `409 PAYROLL_RUN_ALREADY_ACTIVE` (anular libera el slot). Insumo que cambió a medio vuelo → `409 PAYROLL_RUN_POOL_CONFLICT` (reintentar).
- Warnings estables: `PAYROLL_WARNING_RENTA_BRACKETS_MISSING` (retención 0 — cargar tabla), `_NO_BASE_SALARY`, `_BASE_UNDEFINED`, `_INSTALLMENT_DEFERRED` (mínimo garantizado difirió una voluntaria), `_CARRYOVER_INPUT` (rezago arrastrado — REQ-014).

## 3. Revisión y decisión (REQ-013 — la corrida `GENERADA` es la única editable)

| Acción | Endpoint | Notas |
|---|---|---|
| Detalle | `GET …/payroll-runs/{id}` | ETag = token para TODO lo siguiente |
| Drill por empleado | `GET …/payroll-runs/{id}/employees/{personnelFilePublicId}` | líneas con `sourceModule`+`sourceReferencePublicId` (trazabilidad), `isIncluded`, override, `warningCodes` |
| Ajustar línea | `PATCH …/{id}/lines/{linePublicId}` body `{ overrideAmount?, overrideNote?, isIncluded?, clearOverride? }` | nota OBLIGATORIA con override; ley/patronales y el toggle de la línea de HE → `422 PAYROLL_RUN_LINE_NOT_ADJUSTABLE` |
| Recalcular empleados | `PATCH …/{id}/recalculation` body `{ employeeIds[] }` | re-deriva SOLO esos empleados con insumos actuales; conserva overrides del mismo concepto+fuente |
| Regenerar todo | `PATCH …/{id}/regeneration` | descarta ajustes (avisar al usuario), `regeneratedCount++` |
| **Autorizar** | `PATCH …/{id}/authorization` | grant dedicado `AuthorizePayrollRuns` (Admin NO lo cubre); **anti-self**: quien generó recibe `403 PAYROLL_RUN_SELF_AUTHORIZATION_FORBIDDEN`; congela cálculos |
| **Devolver** | `PATCH …/{id}/return` body `{ reason }` | única reapertura pre-cierre (`AUTORIZADA→GENERADA`); motivo obligatorio → `422 PAYROLL_RUN_RETURN_REASON_REQUIRED` |
| **Cerrar** | `PATCH …/{id}/closure` | `AUTORIZADA→CERRADA` terminal; **cierra el PERIODO en la misma transacción** |
| **Anular** | `PATCH …/{id}/annulment` body `{ reason }` | pre-cierre; **revierte TODAS las aplicaciones MOTOR** (los registros fuente vuelven a candidatos) y libera el slot |

Semántica clave para la UI (REQ-014):
- **Excluir una línea de pool** (ingresos/descuentos recurrentes o eventuales) ANULA su aplicación `MOTOR`: el registro fuente vuelve a `AUTORIZADO` y reaparece como candidato de la siguiente corrida. Re-incluir re-aplica (referencia `sourceReferencePublicId` cambia: hijo→padre→hijo nuevo).
- **Excluir una línea de registro** (TNT/amonestación) la libera para el arrastre de la siguiente corrida. «Ignorar = posponer = enviar a otro periodo».
- La línea de **horas extra** es el agregado por plaza: su inclusión no se togglea (`422`); para sacarla, anular el registro de HE en su módulo y recalcular al empleado. El override de monto SÍ se permite.
- Estados: `GENERADA → AUTORIZADA → CERRADA` + `ANULADA` (pre-cierre). Tras `AUTORIZADA` todo ajuste responde `422 PAYROLL_RUN_STATE_RULE_VIOLATION`.

## 4. Bandeja, exports y conciliación (REQ-013 — gate `ViewPayrollRuns`)

- **Bandeja** `POST companies/{companyId}/payroll-runs/query` body `{ payrollDefinitionPublicId?, payrollPeriodPublicId?, statusCode?, year?, pageNumber?, pageSize? }` → `items` (cabecera PERSISTIDA — no recalcular nada en el FE) + **`statusCounts` que SIEMPRE abarcan todos los estados** (son los números de las pestañas: no derivarlos de `items`).
- **Exports** (query `?format=xlsx|csv|json`; auditados; límite síncrono → `413`):
  - Bandeja: `GET …/payroll-runs/export`
  - **Impresión de planilla**: `GET …/payroll-runs/{id}/lines/export` — filas `TipoFila=DETALLE` + `TOTAL_POR_CONCEPTO` + `TOTAL_POR_CENTRO_COSTO` (calculadas sobre las líneas INCLUIDAS). Los nombres de propiedad de las filas son los encabezados (PascalCase en json).
  - **Conciliación bancaria**: `GET …/payroll-runs/{id}/bank-reconciliation/export` — una fila por empleado: `FormaPago` (de la plaza primaria activa) → `Banco/TipoCuenta/NumeroCuenta` (cuenta designada de la plaza o la PRIMARIA del expediente) → `Neto`; **sin cuenta la fila viaja con `Advertencia = PAYROLL_WARNING_NO_BANK_ACCOUNT`** (advertir, nunca bloquear).

## 5. Historial por empleado (REQ-015)

**Corporativo** (`ViewPayrollRuns`): `POST companies/{companyId}/payroll-runs/employee-history/query`
body `{ personnelFilePublicId, year?, payrollDefinitionPublicId?, payrollTypeCode?, statusCodes?, from?, to?, pageNumber?, pageSize? }`.

- Una fila por corrida donde el empleado tiene líneas INCLUIDAS, con **SUS** Σ ingresos/descuentos/neto (no los totales de la corrida), más nueva primero.
- **«Un endpoint, dos consultas»**: default `statusCodes = [CERRADA, AUTORIZADA]` = historial de pagos; con `["GENERADA"]` el MISMO endpoint es la **consulta de acciones/eventos aplicados en el periodo abierto**. `ANULADA` solo con filtro explícito.
- El drill de una fila = `GET …/payroll-runs/{id}/employees/{fileId}` (§3). El mapa `sourceModule` → módulo fuente: `OVERTIME`→horas extra · `ONE_TIME_INCOME`/`RECURRING_INCOME`→ingresos · `ONE_TIME_DEDUCTION`/`RECURRING_DEDUCTION`→descuentos · `NOT_WORKED_TIME`→TNT · `DISCIPLINARY`→amonestaciones · `INCAPACITY`→incapacidades · `SALARIO`/`LEY_*`/`PATRONAL_*`→motor.
- **Nota P-04**: las solicitudes de vacación NO generan línea (el goce no altera el pago); su contexto vive en el módulo de vacaciones/tablero. «Servicios realizados» = tipo POR_OBRA vía ingresos.

**Autoservicio del propio empleado** (sin permisos nuevos — pasa el usuario VINCULADO al expediente o quien tenga `ViewPayrollRuns`):

```
GET personnel-files/{publicId}/payroll-history?year=&pageNumber=&pageSize=
GET personnel-files/{publicId}/payroll-history/{payrollRunPublicId}
```

- Estados **FIJOS** `CERRADA`/`AUTORIZADA` (sin parámetro): una corrida `GENERADA`/`ANULADA` **no existe** en esta superficie (lista vacía / drill `404`).
- Expediente ajeno → `403`; inexistente → `404`. Sin boleta PDF por esta vía (F2).

## 6. Boletas (REQ-012 PR-8 — gate `ViewPayrollRuns`)

```
GET companies/{companyId}/payroll-runs/{id}/employees/{fileId}/slip   → PDF individual
GET companies/{companyId}/payroll-runs/{id}/slips                    → zip de PDFs (uno por empleado)
```

- **Solo corridas `AUTORIZADA`/`CERRADA`** — mientras `GENERADA` (o anulada) responden `422 PAYROLL_RUN_STATE_RULE_VIOLATION` (las cifras no son finales).
- La boleta lleva SOLO las líneas de ese empleado: cabecera (empleado/nómina/periodo/fecha de pago) + Ingresos/Descuentos/Patronales (informativos) + resumen con NETO. Nombre de archivo `boleta-{run8}-{file8}.pdf`; el lote `boletas-{run8}.zip` con entradas `boleta-{códigoEmpleado}.pdf`.

## 7. Los dos «no» de F1

1. **Sin correo**: las boletas se descargan/imprimen desde la UI; el envío por email llega en F2.
2. **Sin programación a fecha/hora** (P-14): la generación es manual (pre-flight → generar); el diseño del scheduler quedó reservado para F2.

## 8. Flujo recomendado de pantalla (resumen)

1. Configurar una vez: nómina → calendario del año → jornadas (o `load-template`) → verificar plazas (tipo de planilla, salario base, mínimo $408.80, forma de pago/cuenta).
2. Cada periodo: **pre-flight** (revisar huecos/rezagos) → **generar** → revisar bandeja/drill → ajustar (overrides con nota / excluir-incluir) o recalcular/regenerar → **autorizar** (otro usuario con el grant) → conciliación bancaria + impresión → **cerrar** (cierra el periodo) → boletas.
3. Correcciones: `AUTORIZADA` se devuelve con motivo; cualquier cosa pre-cierre se anula con motivo (los insumos vuelven a estar disponibles) y se regenera.
