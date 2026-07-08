# Guía de integración Frontend — Gestión de tiempo compensatorio

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-08 |
| **Rama backend** | `feature/vacaciones-incapacidades` (REQ-002 PR-1…PR-6 completos) |
| **Documentos** | `docs/business/analisis-tiempo-compensatorio-empleado.md` (D-01…D-21) · `docs/technical/plan-tecnico-tiempo-compensatorio.md` |
| **Alcance** | Maestro de tipos por empresa · acreditaciones (con documento de autorización obligatorio) · ausencias con verificación transaccional del fondo · estado de cuenta con saldo corrido · saldo en el perfil · bandeja/exports · integración con la liquidación (línea automática `HORAS_EXTRAS_PENDIENTES`) |

El módulo registra un **fondo de horas por empleado**: se **acreditan** horas trabajadas fuera de jornada (horas × factor del tipo) y se **debitan** con ausencias (goces). El fondo es un agregado derivado (Σ acreditado − Σ debitado de los movimientos `REGISTRADA`); el saldo nunca es negativo (re-verificación transaccional bajo lock). Al retiro, el saldo pendiente se paga automáticamente en la liquidación. Convenciones de la casa:

- Prefijo `api/v1`.
- Error de negocio en `problemDetails.extensions.code` (mensajes bilingües EN/ES/es-SV en `problemDetails.detail` — mostrar tal cual).
- Concurrencia optimista con `If-Match` en **todo write** (sin header → `400`; token obsoleto → `409`; el token nuevo viaja en `ETag` y en `concurrencyToken` del body). El `DELETE` de documento lleva el token del padre en `parentConcurrencyToken`.
- Enums/códigos como **strings**; todo `Guid XxxId` se serializa como `xxxPublicId`.
- `workDate` de acreditación y `startDate`/`endDate` de ausencia viajan como `date` (`"2026-07-15"`, sin hora); `startTime`/`endTime` de acreditación son `time` (`"08:00:00"`, informativas); las fechas de auditoría son `date-time` UTC.

> **Montos referenciales (convención salario/30).** El valor hora usado en la liquidación es **salario mensual ÷ 30 (salario diario) ÷ horas-día estándar**. Es un cálculo **referencial** para insumo de la liquidación; la nómina real reconcilia. En operación regular (acreditaciones/ausencias) **no hay montos**: el fondo se lleva **solo en horas**.

---

## 1. Permisos (RBAC)

| Permiso | Habilita | Notas |
|---|---|---|
| `PersonnelFiles.LeaveConfiguration.Read` / `.Admin` | Ver / administrar el **maestro de tipos** (reutiliza el permiso de configuración de ausencias de REQ-001) | `Admin` los implica. |
| `PersonnelFiles.ViewCompensatoryTime` | Leer acreditaciones, ausencias, estado de cuenta, bandeja/exports | `Admin` lo implica. Lectura de un expediente ajeno: **View OR es el propio empleado** (403 sin enmascarar). |
| `PersonnelFiles.ManageCompensatoryTime` | Crear/editar/anular acreditaciones y ausencias, documentos | `Admin` lo implica. |

**Autoservicio (F1):** el empleado logueado puede **leer lo propio** (estado de cuenta, sus acreditaciones y ausencias) sin permiso de gestión; **no** hay creación por autoservicio en F1 (D-01 — todas las escrituras son solo RRHH `ManageCompensatoryTime`). Un intento de leer otro expediente sin `ViewCompensatoryTime` → `403`.

---

## 2. Catálogos y máquina de estados

Catálogos país-scoped (SV sembrado) vía `GET /api/v1/general-catalogs/{key}?countryCode=SV` — los **códigos son estructurales** (el nombre es i18n/editable):

| Wire key | Códigos |
|---|---|
| `compensatory-time-statuses` | `REGISTRADA`, `ANULADA` |
| `compensatory-time-operations` | `ACREDITA`, `DEBITA`, `AMBAS` |

```
Acreditación / Ausencia:  REGISTRADA ──annulment──► ANULADA (terminal)
   Solo REGISTRADA cuenta para el saldo del fondo y para el insumo de planilla / liquidación.
```

`operationCode` del tipo restringe su uso: `ACREDITA` solo en acreditaciones, `DEBITA` solo en ausencias, `AMBAS` en las dos (`COMPENSATORY_TIME_TYPE_OPERATION_MISMATCH` si se usa al revés).

---

## 3. Maestro de tipos por empresa (pantalla "Tipos de tiempo compensatorio")

Maestro **governed** (familia `[ResourceActions]`): cada respuesta incluye `allowedActions[]` (usar para habilitar botones), `concurrencyToken` e `isActive`. **Inicia vacío** (D-05 — sin semilla ni plantilla): el administrador crea los tipos como **primer paso de adopción**. Permiso `LeaveConfiguration.Read` (GET) / `LeaveConfiguration.Admin` (write). If-Match en todo write.

| Operación | Endpoint |
|---|---|
| Listar | `GET /api/v1/companies/{companyId}/compensatory-time-types` — filtros `isActive`, `operationCode`, `q` (código/nombre); `includeAllowedActions=true` para flags por ítem |
| Crear | `POST /api/v1/companies/{companyId}/compensatory-time-types` → `201` + `Location` + `ETag` |
| Obtener / editar | `GET/PUT /api/v1/compensatory-time-types/{publicId}` |
| Activar / inactivar | `PATCH …/{publicId}/activate` · `…/inactivate` (If-Match) |

Body: `{ code, name, operationCode, creditFactor, sortOrder? }`. `operationCode` ∈ `ACREDITA`/`DEBITA`/`AMBAS`; `creditFactor` > 0 (default 1.00). Código activo duplicado → `409 COMPENSATORY_TIME_TYPE_CODE_CONFLICT`. Inactivar un tipo referenciado por un registro activo → `422 COMPENSATORY_TIME_TYPE_IN_USE`. Editar el `creditFactor` **no** recalcula históricos (el factor es snapshot en cada acreditación).

> Ejemplos de tipos (guía documental, no semilla): `TRABAJO_FUERA_JORNADA` (ACREDITA 1.00), `TRABAJO_ASUETO` (ACREDITA 2.00), `GOCE_TIEMPO_COMPENSATORIO` (DEBITA).

---

## 4. Preferencias de la empresa (4 campos)

`GET /api/v1/companies/{companyId}/preferences` y `PUT …/preferences` (reemplazo; el `PATCH` sigue scalar-only para moneda/zona — los policies del módulo van por el **PUT**). Campos nuevos (todos anulables; `null` = default):

| Campo (wire) | Default (null) | Efecto |
|---|---|---|
| `compensatoryTimeStandardDailyHours` | `8` | Horas-día estándar: divisor del valor-hora en la liquidación y multiplicador de la sugerencia de ausencia. |
| `compensatoryTimeMaxBalanceHours` | sin tope | Tope de saldo acreditable (P-10). Superarlo → `422 COMPENSATORY_TIME_MAX_BALANCE_EXCEEDED`. |
| `compensatoryTimeCreditRequiresDocument` | `true` | Exige el documento de autorización en el alta de acreditación (D-20). |
| `compensatoryTimeSettlementRateFactor` | `1.00` | Tarifa del pago al retiro (P-15). Confirmar con el negocio antes del despliegue. |

---

## 5. Acreditaciones (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/compensatory-time-credits`. **Solo RRHH** (`ManageCompensatoryTime` para write; lecturas View OR propio).

### 5.1 Registrar — `POST …/compensatory-time-credits` → `201`

```json
{
  "compensatoryTimeTypePublicId": "…",        // tipo ACREDITA o AMBAS
  "workDate": "2026-03-04",                    // ≤ hoy (RN-15)
  "startTime": null,                           // informativas; si viajan ambas, inicio < fin
  "endTime": null,
  "hoursWorked": 8,                            // > 0
  "hoursCreditedOverride": null,               // ajuste manual del calculado (opcional)
  "overrideNote": null,                        // OBLIGATORIA si hoursCreditedOverride viaja
  "workDetail": "Soporte fuera de jornada",    // requerido
  "authorizedByText": "Jefatura de TI",        // requerido
  "assignedPositionPublicId": null,            // opcional (plaza informativa)
  "overtimeRecordPublicId": null,              // costura al futuro módulo de horas extras (D-21; sin FK)
  "notes": null,
  "authorizationFilePublicId": "…",            // DOCUMENTO DE AUTORIZACIÓN — file ya subido, purpose=CompensatoryTimeDocument
  "documentTypeCatalogItemPublicId": null,
  "documentObservations": null
}
```

- **Documento de autorización (patrón nuevo)** — con `compensatoryTimeCreditRequiresDocument` (default sí) el `authorizationFilePublicId` es **obligatorio en el POST**: subir primero el PDF (flujo de archivos con `purpose=CompensatoryTimeDocument`, solo PDF), luego enviar su `publicId` aquí; se asocia como documento hijo en la misma transacción. Sin adjunto → `422 COMPENSATORY_TIME_DOCUMENT_REQUIRED`; adjunto con propósito ajeno → `422 COMPENSATORY_TIME_DOCUMENT_PURPOSE_INVALID`. Con la preferencia en `false` → `201` sin adjunto.
- Respuesta: `factorApplied` (snapshot del tipo), `hoursCredited` (= `Round2(hoursWorked × factor)`, o el override), `isOverridden`, `compensatoryTimeTypeCode`, `statusCode`, `authorizerFilePublicId`, `concurrencyToken`. Al crear journalea `ACREDITACION_TIEMPO_COMPENSATORIO`.

### 5.2 Ciclo

| Operación | Endpoint (+ `If-Match`) | Notas |
|---|---|---|
| Listar / detalle | `GET …/compensatory-time-credits` · `GET …/{creditPublicId}` | |
| Editar | `PUT …/{creditPublicId}` | Solo `REGISTRADA`. Reduce saldo → re-verifica bajo lock. |
| Anular | `PATCH …/{creditPublicId}/annulment` — `{ "reason": "…" }` (obligatorio) | Solo `REGISTRADA`. Anular una acreditación que dejaría el saldo negativo (débitos ya la consumían) → `422 COMPENSATORY_TIME_BALANCE_WOULD_GO_NEGATIVE`. |
| Documentos | `GET/POST …/{creditPublicId}/documents` · `DELETE …/documents/{documentPublicId}` (`parentConcurrencyToken`) · `GET …/documents/{documentPublicId}/read-url` | Sub-recurso espejo de reclamos médicos; `read-url` da URL temporal de descarga. |

---

## 6. Ausencias (sub-recurso del expediente)

Base: `api/v1/personnel-files/{personnelFilePublicId}/compensatory-time-absences`. **Solo RRHH** (write); lecturas View OR propio. **No** llevan adjuntos.

### 6.1 Sugerencia de horas — `GET …/absence-hours-suggestion?start=YYYY-MM-DD&end=YYYY-MM-DD`

Devuelve `{ suggestedHours, workingDays, holidaysExcluded }` = días del rango excluyendo el **descanso semanal de la plaza** (plaza principal → preferencia `companyRestDayOfWeek`, degradado si no hay) y los **asuetos** (maestro de REQ-001) × horas-día estándar. Usarla para pre-llenar `hoursDebited`.

### 6.2 Registrar — `POST …/compensatory-time-absences` → `201`

```json
{
  "compensatoryTimeTypePublicId": "…",        // tipo DEBITA o AMBAS
  "startDate": "2026-04-06",                   // inicio ≤ fin; futuras permitidas
  "endDate": "2026-04-10",
  "hoursDebited": 32,                          // > 0
  "reason": "Goce solicitado por el empleado", // requerido
  "payrollPeriodPublicId": null,               // opcional (imputación al periodo de planilla de REQ-001; sin contención)
  "notes": null
}
```

- **Verificación transaccional del fondo (RN-03):** débito > saldo → `422 COMPENSATORY_TIME_BALANCE_INSUFFICIENT`. El invariante saldo ≥ 0 se protege con un lock por (tenant, expediente); dos débitos concurrentes contra un saldo que solo cubre uno → exactamente un `201` y un `422`.
- Solape con otra ausencia vigente → `422 COMPENSATORY_TIME_ABSENCE_OVERLAP`; con incapacidad activa → `…_INCAPACITY_OVERLAP`; con solicitud/goce de vacaciones vivo → `…_VACATION_OVERLAP`. Periodo de planilla inexistente/inactivo → `…_PAYROLL_PERIOD_INVALID`.
- Al crear journalea `GOCE_TIEMPO_COMPENSATORIO`. **Anular** (`PATCH …/{absencePublicId}/annulment`, `{ reason }`) restaura las horas al fondo.

---

## 7. Estado de cuenta (sub-recurso del expediente)

`GET /api/v1/personnel-files/{personnelFilePublicId}/compensatory-time-statement` — **View OR propio**. Filtros: `fromDate`, `toDate`, `compensatoryTimeTypePublicId`, `statusCode`, `includeAnnulled` (default `false`), `pageNumber`, `pageSize`.

Respuesta: `{ items: [{ publicId, date, kind (CREDIT/ABSENCE), signedHours (+/−), statusCode, isAnnulled, runningBalance }], totalCredited, totalDebited, availableBalance, … }`. El **saldo corrido** se calcula sobre el set filtrado completo (una página intermedia arrastra el acumulado previo — R-T9); los totales cubren el set filtrado. Sin filtros, `availableBalance` == el saldo del fondo == `compensatoryTimeHoursAvailable` del perfil.

---

## 8. Bandeja corporativa y exportaciones (nivel empresa)

| Pantalla | Endpoint |
|---|---|
| Movimientos (query) | `POST /api/v1/companies/{companyId}/compensatory-time-movements/query` — `{ employeeId?, compensatoryTimeTypePublicId?, operationCode? (ACREDITACION/AUSENCIA), fromDate?, toDate?, statusCode?, includeAnnulled?, pageNumber?, pageSize? }`. Devuelve `items[]` (crédito → `+`, ausencia → `−signedHours`; `payrollPeriodLabel` si imputado), `totalCount` y `statusCounts` (cubren **todos** los estados). Por defecto los items excluyen `ANULADA` (pedir `statusCode=ANULADA` o `includeAnnulled=true` para verlos). |
| Export de movimientos | `GET …/compensatory-time-movements/export?format=xlsx\|csv\|json` — filas ES (Empleado, CodigoEmpleado, Operacion, Tipo, FechaInicio/Fin, HorasTrabajadas, Factor, Horas±, Detalle, AutorizadoPor, Estado, PeriodoPlanilla, FechaRegistro); anulados excluidos por defecto. |
| Export de saldos | `GET …/compensatory-time-balances/export?format=…` — una fila por empleado con movimiento vigente (TotalAcreditado, TotalDebitado, SaldoDisponible, UltimoMovimiento). |

Exports **síncronos**, rate-limited y con tope (`413 REPORT_EXPORT_TOO_LARGE` → sugerir filtrar); formato inválido → `400 PERSONNEL_FILE_EXPORT_FORMAT_INVALID`. La bandeja/exports es insumo RRHH (company-scoped, sin autoservicio).

---

## 9. Saldo en el perfil e integración con la liquidación

- `GET /api/v1/personnel-files/{id}/employment-information` **puebla** `compensatoryTimeHoursAvailable` (decimal) = saldo del fondo (Σ acreditado − Σ debitado vigentes). **`null`** si el empleado aún no tiene movimientos (mostrar "—", no 0).
- **Liquidación (RF-013/D-19).** Al generar la liquidación de un empleado con retiro **y saldo positivo en su plaza principal**, el motor agrega automáticamente la línea **`HORAS_EXTRAS_PENDIENTES`**:
  - `unitsOrDays` = horas pendientes del fondo · `calculationBase` = valor hora (`salario diario ÷ horas-día estándar`) · `calculatedAmount` = `horas × valor-hora × tarifa` (ej. golden: saldo 12 h, salario diario 20, 8 h/día, tarifa 1.00 → base 2.50, monto **$30.00**). Afecta ISSS/AFP/Renta automáticamente.
  - El liquidador puede **editar las horas** de la línea (`PUT …/lines/{lineId}` con `unitsOrDays`) o **sobrescribir el monto** (override auditado); ambos **sobreviven** los recálculos normales. Una **regeneración** (`POST …/lines/regenerate`) descarta los ajustes y vuelve a leer el fondo (12 h → $30.00).
  - **Sin saldo / plaza no principal / módulo sin datos → no hay línea** (retrocompatible; la liquidación existente no cambia). En un retiro **multi-plaza** la línea aparece **solo** en la liquidación de la plaza principal (el fondo es por empleado).
  - El concepto `HORAS_EXTRAS_PENDIENTES` es ahora **de sistema**: agregarlo como **línea manual** se rechaza con `422 SETTLEMENT_CONCEPT_INVALID` (evita doble pago auto + manual). El FE de liquidación no requiere cambios de contrato (es lógica interna del motor).

---

## 10. Tabla de errores del módulo

| `extensions.code` | HTTP | Cuándo |
|---|---|---|
| `COMPENSATORY_TIME_TYPE_INVALID` | 422 | Tipo inexistente/inactivo (crédito, ausencia). |
| `COMPENSATORY_TIME_TYPE_OPERATION_MISMATCH` | 422 | Tipo `DEBITA` en crédito / `ACREDITA` en ausencia (RN-04). |
| `COMPENSATORY_TIME_TYPE_IN_USE` | 422 | Inactivar/editar código de tipo referenciado por un registro activo. |
| `COMPENSATORY_TIME_TYPE_CODE_CONFLICT` | 409 | Código de tipo duplicado (crear/activar). |
| `COMPENSATORY_TIME_TYPE_NOT_FOUND` | 404 | Tipo inexistente. |
| `COMPENSATORY_TIME_WORK_DATE_IN_FUTURE` | 422 | `workDate` > hoy (RN-15). |
| `COMPENSATORY_TIME_TIME_RANGE_INVALID` | 422 | `startTime`/`endTime` incoherentes. |
| `COMPENSATORY_TIME_DOCUMENT_REQUIRED` | 422 | Alta de crédito sin `authorizationFilePublicId` con la preferencia activa (D-20). |
| `COMPENSATORY_TIME_DOCUMENT_PURPOSE_INVALID` | 422 | El archivo no fue subido con `purpose=CompensatoryTimeDocument`. |
| `COMPENSATORY_TIME_OVERRIDE_NOTE_REQUIRED` | 422 | Ajuste de `hoursCreditedOverride` sin nota (RN-02). |
| `COMPENSATORY_TIME_MAX_BALANCE_EXCEEDED` | 422 | Superaría el tope de saldo (P-10). |
| `COMPENSATORY_TIME_BALANCE_INSUFFICIENT` | 422 | Débito > saldo (RN-03). |
| `COMPENSATORY_TIME_BALANCE_WOULD_GO_NEGATIVE` | 422 | Editar/anular crédito descubre débitos (RN-06). |
| `COMPENSATORY_TIME_ABSENCE_OVERLAP` / `…_INCAPACITY_OVERLAP` / `…_VACATION_OVERLAP` | 422 | Solape con ausencia vigente / incapacidad activa / solicitud-goce de vacaciones (RN-05). |
| `COMPENSATORY_TIME_PAYROLL_PERIOD_INVALID` | 422 | Periodo de planilla inexistente/inactivo (P-14). |
| `COMPENSATORY_TIME_ANNULMENT_REASON_REQUIRED` | 422 | Anulación sin motivo (RN-07). |
| `COMPENSATORY_TIME_STATE_RULE_VIOLATION` | 422/409 | Editar/anular un registro `ANULADA`. |
| `SETTLEMENT_CONCEPT_INVALID` | 422 | Agregar `HORAS_EXTRAS_PENDIENTES` (u otro concepto de sistema) como línea manual en la liquidación. |
| `EMPLOYEE_PROFILE_RETIRED_LOCKED` | 422 | Cualquier escritura del módulo sobre un perfil `RETIRADO` (aclaración №9). |
| `REPORT_EXPORT_TOO_LARGE` / `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` | 413 / 400 | Export. |
| `CONCURRENCY_CONFLICT` / (sin If-Match) | 409 / 400 | Convenciones de concurrencia. |

---

## 11. Flujo recomendado + pasos de adopción por empresa

**Adopción (una vez por empresa, roles `LeaveConfiguration.Admin` / `Admin`):**
1. Crear los **tipos de tiempo compensatorio** (el maestro inicia vacío — sin tipos el módulo no opera → `COMPENSATORY_TIME_TYPE_INVALID`).
2. Configurar las **4 preferencias** (§4) — confirmar la **tarifa de liquidación** (P-15) con el negocio.
3. (Opcional) cargar saldos iniciales del fondo con acta: acreditaciones históricas por empleado.

**Pantallas:**
1. **Tipos de tiempo compensatorio**: tabla governed (`allowedActions[]` gobierna botones) — crear/editar/activar/inactivar.
2. **Acreditaciones** (per-file): alta con **documento de autorización obligatorio** (subir PDF → enviar `authorizationFilePublicId`), factor snapshot, ajuste manual con nota; anulación con motivo.
3. **Ausencias** (per-file): sugerencia de horas → alta con verificación de fondo; anulación restaura horas.
4. **Estado de cuenta** (per-file + autoservicio de solo lectura): tabla cronológica con saldo corrido + totales.
5. **Bandeja corporativa**: chips por `statusCounts`, filtros por operación/tipo/estado/fecha, export de movimientos y de saldos.
6. **Liquidación**: la línea `HORAS_EXTRAS_PENDIENTES` aparece sola (editable + regenerable); mostrar el detalle del cálculo (horas × valor-hora × tarifa).

---

## 12. Notas de despliegue

- Migraciones incluidas (aplicar en orden): **M1** `AddCompensatoryTimeConfiguration` (maestro + catálogos TPH + 2 ActionTypes + 4 preferencias), **M2** `AddPersonnelFileCompensatoryTime` (créditos + documentos + ausencias), **M3** `AddCompensatoryTimeSettlementConcept` (voltea `HORAS_EXTRAS_PENDIENTES` a `is_system_calculated=true` — habilita la línea automática de la liquidación y cierra su vía manual).
- `Storage:Purposes:CompensatoryTimeDocument` debe estar en **appsettings base** (solo PDF, tamaño) y el contenedor de blobs `clarihr-compensatory-time-documents` **pre-aprovisionado** (config faltante → `422` en el alta con adjunto).
- Los 2 permisos (`ViewCompensatoryTime`/`ManageCompensatoryTime`) se agregan al catálogo de aprovisionamiento; **asignarlos a los roles**.
- **Confirmar P-15** (tarifa de liquidación, default 1.00) con el negocio antes del despliegue; ajustar `compensatoryTimeSettlementRateFactor` por empresa si difiere.
- El maestro de tipos **inicia vacío** en producción (sin semilla): documentar el paso de adopción. Todos los mensajes llegan localizados (EN/ES/es-SV) en `problemDetails.detail`.
