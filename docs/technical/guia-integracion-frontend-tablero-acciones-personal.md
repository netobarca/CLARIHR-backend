# Guía de integración Frontend — Tablero de gráficos e indicadores de acciones de personal (REQ-004)

| | |
|---|---|
| **Audiencia** | Equipo Frontend |
| **Fecha** | 2026-07-09 |
| **Rama backend** | `feature/vacaciones-incapacidades` (REQ-004 PR-1…PR-5 completos) |
| **Documentos** | `docs/business/analisis-tablero-acciones-personal.md` (D-01…D-18, Anexo A.5) · `docs/technical/plan-tecnico-tablero-acciones-personal.md` |
| **Alcance** | **Extensión aditiva del tablero RRHH ya publicado** (`personnel-files/dashboard/*`) con: `overview.byPayrollType` · sección **acciones documentales** (`dashboard/personnel-actions`) · sección **movimientos** (`dashboard/movements`) · **bandeja corporativa de asientos** (`personnel-actions/query`) + **exports** (síncrono + job asíncrono `COMPANY_PERSONNEL_ACTIONS`) · **metadata extendida** (`sections[]`/`filters[]`/`rotationFormula`) · **3 filtros nuevos** (`month`, `payrollTypeCode`, `costCenterPublicId`) · **impresión/PDF = frontend** (P-01) con **mapa de gráficos A.5** |

Este REQ **extiende** el tablero existente; **no cambia** la forma de `overview`/`hires`/`span-of-control`/`metadata` (contrato 100% aditivo). Convenciones de la casa:

- Prefijo `api/v1`; ruta base del tablero `…/companies/{companyId}/personnel-files/dashboard/…`; la **bandeja y su export** cuelgan de `…/companies/{companyId}/personnel-actions/…` (recurso corporativo, como `settlements`).
- Error de negocio en `problemDetails.extensions.code` (mensaje en `problemDetails.detail`, mostrar tal cual).
- Enums/códigos como **strings**; todo `Guid XxxId` se serializa/deserializa como `xxxPublicId` (aplica a los query params `Guid` y a los campos `Guid` del body).
- Toda la reportería es **solo lectura** bajo un único permiso; **cero escrituras** desde estas pantallas.

> **⚠️ SIN MONTOS (D-15/RN-16, aclaración №8).** Ninguna respuesta de este módulo (secciones, bandeja o export) expone `amount`/`currency`. El asiento de liquidación lleva el neto en el back, pero el tablero **nunca** lo devuelve. Hay un test de contrato que falla si aparece un campo monetario: no dependas de montos aquí.

---

## 1. Permisos (RBAC)

Todo el módulo se sirve bajo **un solo gate** — el mismo del tablero base:

| Acción | Permiso (cualquiera) |
|---|---|
| Ver cualquier sección, la bandeja y los exports | `PersonnelFiles.ViewReports` **∨** `PersonnelFiles.Read` **∨** `PersonnelFiles.Admin` (∨ IAM super-admin) |

- No hay permisos nuevos (D-16). Sin ninguno de esos → `403` en toda la familia (secciones, bandeja, export síncrono y creación del job asíncrono).
- No hay autoservicio: es una vista gerencial de empresa.
- La autorización se aplica **por handler** (los controladores de reportería no llevan `[AuthorizationPolicySet]`); para el FE es transparente: si no tienes el permiso, recibes `403`.

---

## 2. Filtro común (los 3 filtros nuevos)

Todos los endpoints del tablero comparten un filtro dimensional. REQ-004 añadió **3 parámetros opcionales**:

| Parámetro (wire) | Tipo | Aplica a | Semántica |
|---|---|---|---|
| `payrollTypeCode` | string (código de `payroll-types`) | **todos** los endpoints | Modalidad de pago contractual del empleado (MENSUAL/QUINCENAL/…). Case-insensitive. |
| `costCenterPublicId` | uuid | **todos** los endpoints | Centro de costo de la plaza activa. |
| `month` | int 1-12 | **solo** las secciones de flujo (`personnel-actions`, `movements`, `hires`) y la bandeja | Restringe la serie/consulta a ese mes del año. **Requiere `year` explícito** → si no, `400 DASHBOARD_MONTH_REQUIRES_YEAR`. |

Los filtros heredados siguen igual: `year`, `functionalAreaPublicId`, `orgUnitPublicId`, `positionCategoryPublicId`, `jobProfilePublicId`, `workCenterPublicId`, `includeInactive` (donde aplique).

> **Nota de aproximación dimensional (D-07).** Los filtros/desgloses dimensionales atribuyen cada acción/movimiento a la **unidad organizativa ACTUAL** del empleado (su asignación activa primaria de hoy), **no** a la unidad que tenía cuando ocurrió el hecho. No hay snapshot histórico dimensional en F1. Comunícalo en la leyenda de los gráficos con desglose organizativo (el drill a la bandeja permite verificar fila por fila). Una acción cuyo expediente no tiene fila dimensional cae en el bucket **`UNASSIGNED` / "Sin asignar"** — salvo que haya un filtro dimensional activo, en cuyo caso se excluye.

> **Semántica flujo vs. snapshot.** `month` **solo** tiene efecto en los endpoints de **flujo** (series por fecha de evento): `personnel-actions`, `movements`, `hires` y la bandeja. Los endpoints **snapshot** (`overview`, `span-of-control`) **aceptan** `month` pero lo **ignoran** (miden estado actual, no una ventana). La metadata declara qué secciones aceptan `month` (`sections[].acceptsMonth`).

---

## 3. `overview` — desglose nuevo `byPayrollType` (aditivo)

`GET …/dashboard/overview?<filtros>` sigue devolviendo exactamente lo de antes **más** un campo nuevo:

```jsonc
{
  // …todo lo existente (headcount, byRecordType, byOrgUnit, ageRanges, …) intacto…
  "byPayrollType": [
    { "key": "MENSUAL",    "label": "Mensual",   "count": 42 },
    { "key": "QUINCENAL",  "label": "Quincenal", "count": 18 },
    { "key": "UNASSIGNED", "label": "Sin dato",  "count": 7  }   // plazas sin clasificar
  ]
}
```

- `label` viene del catálogo `payroll-types` (nunca hardcodear; obtén los ítems por `general-catalogs/payroll-types?countryCode=SV` si necesitas la lista completa para un selector).
- El bucket **`UNASSIGNED` = "Sin dato"** agrupa las plazas cuyo `payrollTypeCode` quedó sin clasificar (ver §11 — tras la migración destructiva, las plazas no conformes quedan en NULL hasta que RRHH las reclasifique).

---

## 4. Sección **acciones documentales** — `dashboard/personnel-actions`

Primera consulta corporativa del **historial de acciones de personal** (journal). Serie mensual + desgloses.

`GET …/dashboard/personnel-actions?year=&month=&includeAllStatuses=&<filtros>`

```jsonc
{
  "year": 2026,
  "month": null,                       // o el mes filtrado
  "includeAllStatuses": false,
  "series": { "byMonth": [ { "month": 1, "count": 0 }, … 12 buckets … ], "total": 37 },
  "byType":    [ { "key": "BAJA", "label": "Baja / retiro definitivo", "count": 12 }, … ],  // desc
  "byStatus":  [ { "key": "APLICADA", "label": "Aplicada", "count": 34 }, { "key": "ANULADA", … } ],
  "byOrigin":  [ { "key": "MANUAL", "label": "Manual", "count": 20 }, { "key": "SYSTEM", "label": "Automático", "count": 17 } ],
  "byDimension": {
    "orgUnits": [ … ], "functionalAreas": [ … ], "workCenters": [ … ],
    "jobProfiles": [ … ], "positionCategories": [ … ], "payrollTypes": [ … ]
  }
}
```

Reglas de lectura:
- **Población por defecto = `APLICADA`** (D-05/RN-04). `includeAllStatuses=true` amplía los *items* (serie, byType, byOrigin, byDimension) a **todos** los estados.
- **`byStatus` SIEMPRE cubre el universo completo** de estados (independiente del default): úsalo para el gráfico "por estado" aunque el resto muestre solo `APLICADA`.
- `byOrigin` **siempre** emite los 2 segmentos (`MANUAL`/`SYSTEM`), aun con conteo 0 (dona determinista de 2 gajos).
- `series.byMonth` siempre trae 12 buckets (ceros incluidos). Con `month`, solo ese mes tiene conteo.
- `month` sin `year` → `400 DASHBOARD_MONTH_REQUIRES_YEAR`. `year` omitido → año actual.

---

## 5. Sección **movimientos** — `dashboard/movements`

Bajas / altas / neto / rotación / cobertura de entrevistas / liquidaciones por estado. **Fuente canónica = el PERFIL del empleado, nunca el journal** (D-03): una baja revertida (que limpia `RetirementDate`) sale de las series y ratios.

`GET …/dashboard/movements?year=&month=&<filtros>`

```jsonc
{
  "year": 2026,
  "month": null,
  "hires":       { "byMonth": [ …12… ], "total": 8 },
  "separations": {
    "series":     { "byMonth": [ …12… ], "total": 5 },
    "byCategory": [ { "key": "VOLUNTARIA", "label": "Renuncia voluntaria", "count": 3 }, … ],
    "byReason":   [ { "key": "MEJOR_OFERTA_SALARIAL", "label": "Mejor oferta salarial", "count": 2 }, … ]
  },
  "net":         { "byMonth": [ …12… ], "total": 3 },              // altas − bajas; puede ser negativo
  "rotation":    { "separations": 5, "averageHeadcount": 100.0, "ratePercent": 5.0 },   // ratePercent null si avg 0 ("N/D")
  "exitInterviewCoverage": { "separations": 5, "completed": 4, "coveragePercent": 80.0 }, // coveragePercent null si 0 bajas
  "settlementsByStatus": [ { "key": "EMITIDA", "label": "Emitida", "count": 3 }, { "key": "BORRADOR", … } ]  // CONTEOS, sin montos
}
```

Reglas de lectura:
- **Rotación** = bajas ÷ headcount promedio × 100. Si el promedio es 0 → `ratePercent: null` (muestra "N/D"). La fórmula descriptiva viene en `metadata.rotationFormula` (muéstrala en la leyenda — RN-10).
- **Cobertura de entrevistas** = bajas del periodo con entrevista de retiro **completada** ÷ bajas. Con 0 bajas → `coveragePercent: null`.
- **Liquidaciones por estado** = liquidaciones **reales** (los escenarios/simulaciones se excluyen) con retiro en el periodo, agrupadas por estado. **Conteos, sin montos.**
- `dashboard/hires` **no cambió**; la sección movimientos recalcula sus altas con el mismo criterio (no lo consumas dos veces si muestras ambos).
- `month` sin `year` → `400 DASHBOARD_MONTH_REQUIRES_YEAR`.

---

## 6. **Bandeja corporativa de asientos** (drill) — `personnel-actions/query`

Detalle fila-por-fila del journal a nivel empresa (el "drill" desde los gráficos de la sección documental). Recurso corporativo (cuelga de `personnel-actions`, no de `personnel-files`).

`POST /api/v1/companies/{companyId}/personnel-actions/query`

**Request** (`QueryCompanyPersonnelActionsRequest` — todos los campos opcionales):

```jsonc
{
  "actionTypeCode": "BAJA",        // filtra por tipo de acción (código de action-types)
  "actionStatusCode": "APLICADA",  // filtra los items por estado
  "isSystemGenerated": true,       // origen: false = manual, true = automático
  "year": 2026, "month": 5,        // ventana por año(+mes)…
  "fromUtc": null, "toUtc": null,  // …o por rango absoluto (date-time UTC); si ambos van, gana el rango
  "employeePublicId": null,        // filtrar por un empleado
  "functionalAreaPublicId": null, "orgUnitPublicId": null, "positionCategoryPublicId": null,
  "jobProfilePublicId": null, "workCenterPublicId": null,
  "payrollTypeCode": null, "costCenterPublicId": null,
  "pageNumber": 1, "pageSize": 25
}
```

**Response** (`PersonnelActionBandejaResponse`):

```jsonc
{
  "items": [
    {
      "personnelActionPublicId": "…",
      "personnelFilePublicId": "…",
      "employeeFullName": "Ana Mensual",
      "employeeCode": "EMP-001",
      "actionTypeCode": "BAJA",   "actionTypeName": "Baja / retiro definitivo",
      "actionStatusCode": "APLICADA", "actionStatusName": "Aplicada",
      "originCode": "SYSTEM",     "isSystemGenerated": true,   // MANUAL | SYSTEM
      "actionDateUtc": "2026-05-20T00:00:00Z",
      "effectiveFromUtc": null, "effectiveToUtc": null,
      "description": null, "reference": null
      // ⚠️ NO hay amount/currency — la bandeja no expone montos (aclaración №8)
    }
  ],
  "pageNumber": 1, "pageSize": 25, "totalCount": 37,
  "statusCounts": { "APLICADA": 34, "ANULADA": 3 }
}
```

Reglas de lectura:
- **Ventana temporal**: se resuelve en el back — `fromUtc`/`toUtc` (rango inclusivo) si vienen; si no, `year`(+`month`); si nada, **el año actual**. `month` sin `year`/rango → `400 DASHBOARD_MONTH_REQUIRES_YEAR`.
- **`statusCounts` abarcan TODOS los estados** del conjunto filtrado (ignoran `actionStatusCode`): úsalos para las pestañas/contadores por estado aunque el usuario tenga un estado seleccionado.
- Orden: por `actionDateUtc` descendente. Paginación estándar (`pageNumber`/`pageSize`, `pageSize` 1–100).
- **Ni el usuario que generó la acción ni ningún monto se exponen** — la fila es documental (empleado, código, tipo, estado, origen, fecha, vigencias, descripción/referencia).
- El drill **cuadra** con `byType`/`byOrigin` de la sección documental cuando aplicas los mismos filtros (mismo scope dimensional D-07).

---

## 7. Exportaciones

### 7.1 Export síncrono (descarga inmediata) — `personnel-actions/export`

`GET /api/v1/companies/{companyId}/personnel-actions/export?format=xlsx|csv|json&<mismos filtros que la bandeja como query params>`

- Mismos filtros que la bandeja (aquí van como **query params**; los `Guid` como `…PublicId`, `isSystemGenerated`, `year`/`month`/`fromUtc`/`toUtc`, `actionTypeCode`/`actionStatusCode`, dimensiones).
- Formatos: `xlsx` (default), `csv`, `json`. Columnas en **español**: `Empleado, CodigoEmpleado, Tipo, Estado, Origen, FechaAsiento, VigenciaDesde, VigenciaHasta, Descripcion, Referencia`. **Sin columna de monto.**
- Cap síncrono: si el resultado excede el límite configurado → **`413 Payload Too Large`**. Ante `413`, ofrece el job asíncrono (§7.2).

### 7.2 Export asíncrono (volúmenes grandes) — job `COMPANY_PERSONNEL_ACTIONS`

Reutiliza la infraestructura genérica de `report-export-jobs`:

1. `POST /api/v1/companies/{companyId}/report-export-jobs` con
   ```jsonc
   { "resourceKey": "COMPANY_PERSONNEL_ACTIONS", "format": "xlsx",
     "parameters": { "year": 2026, "month": 5, "actionTypeCode": "BAJA", "isSystemGenerated": true,
                     "orgUnitPublicId": "…", "payrollTypeCode": "MENSUAL" } }
   ```
   → `202 Accepted` con el job (`status: "Queued"`). Requiere `ViewReports` (mismo gate).
2. Sondea `GET /api/v1/report-export-jobs/{jobId}` hasta `status: "Succeeded"`.
3. Descarga con `GET /api/v1/report-export-jobs/{jobId}/download` (mismo permiso por recurso).

Los `parameters` aceptan las mismas claves del filtro de la bandeja (`year`/`month`/`fromUtc`/`toUtc`/`actionTypeCode`/`actionStatusCode`/`isSystemGenerated`/`employeePublicId`/dimensiones/`payrollTypeCode`/`costCenterPublicId`). El archivo generado es idéntico al síncrono (mismas filas ES, sin montos).

> **Cuándo usar cada uno:** por defecto el export síncrono; si devuelve `413`, cae al job asíncrono. El resultado tabular es el mismo.

---

## 8. Metadata extendida — `dashboard/metadata`

`GET …/dashboard/metadata` sigue devolviendo lo de antes **más** 3 bloques aditivos:

```jsonc
{
  // …ageRanges, seniorityRanges, fileUpToDateThresholdMonths, hrFunctionalAreaCode… (intacto)
  "sections": [
    { "key": "PERSONNEL_ACTIONS", "active": true,  "acceptsMonth": true },
    { "key": "MOVEMENTS",         "active": true,  "acceptsMonth": true },
    { "key": "INCAPACIDADES",     "active": false, "acceptsMonth": true },  // REQ-001 al conectarse
    { "key": "VACACIONES",        "active": false, "acceptsMonth": true },  // REQ-001
    { "key": "RECONOCIMIENTOS",   "active": false, "acceptsMonth": true },  // REQ-003
    { "key": "AMONESTACIONES",    "active": false, "acceptsMonth": true },  // REQ-003
    { "key": "TIEMPO_COMPENSATORIO", "active": false, "acceptsMonth": true } // REQ-002
  ],
  "filters": [
    { "key": "year", "enabled": true }, { "key": "functionalAreaPublicId", "enabled": true }, …
    { "key": "payrollTypeCode", "enabled": true }, { "key": "costCenterPublicId", "enabled": true },
    { "key": "month", "enabled": true }
  ],
  "rotationFormula": "…texto descriptivo de bajas ÷ headcount promedio × 100…"
}
```

- **`sections[]` = fuentes conectables** (RF-018; espejo del `activeSources[]` de otros módulos). Renderiza **solo** las secciones `active: true`; las `active: false` son módulos futuros que se irán encendiendo (mismo contrato). `acceptsMonth` te dice si esa sección honra el filtro `month`.
- **`filters[]`** enumera los filtros comunes disponibles (las `key` son los **nombres wire** — usa `payrollTypeCode`, `costCenterPublicId`, `month`, `…PublicId`).
- **`rotationFormula`** es el texto a mostrar en la leyenda del gráfico de rotación (RN-10). No lo hardcodees.

---

## 9. Impresión y exportación **PDF = FRONTEND** (P-01 / D-12)

El backend **no** compone PDF del tablero. La **impresión** y la **exportación a PDF** se resuelven en el navegador sobre una **vista de impresión** que el FE construye. Responsabilidad del FE:

- **Encabezado de la vista de impresión (RN-13):** nombre de la **empresa** + **filtros aplicados** (año/mes, unidad, tipo de planilla, centro de costo, …) + **fecha y hora de generación**.
- La vista de impresión muestra **todas las secciones visibles** con los filtros vigentes, en el **mismo orden** del mapa A.5 (§10).
- El PDF se genera desde esa vista (print-to-PDF del navegador); debe ser **fiel** a lo mostrado en pantalla.
- No hay endpoint PDF ni job PDF para el tablero: no lo busques en el back (el export tabular xlsx/csv/json de la bandeja es lo único servido).

---

## 10. Mapa de gráficos (Anexo A.5) — especificación de la vista

Orden = disposición sugerida de arriba hacia abajo (lo gerencial primero); la **vista de impresión respeta este mismo orden**. Todos los gráficos muestran valores absolutos accesibles (etiqueta/tooltip) y los buckets **"Sin asignar"/"Sin dato"** como categorías.

| Orden | Indicador | Gráfico | Prioridad | Fuente (endpoint → campo) |
|---|---|---|---|---|
| 1 | Fila de KPIs del periodo: total de acciones · altas · bajas · neto · **rotación %** · **cobertura entrevistas %** | Tarjetas de indicador (stat tiles) con valor + absolutos | Alta | `personnel-actions.series.total` · `movements.hires.total` · `movements.separations.series.total` · `movements.net.total` · `movements.rotation.ratePercent` · `movements.exitInterviewCoverage.coveragePercent` |
| 2 | Altas vs bajas por mes + neto | Barras agrupadas mensuales (altas/bajas) + línea de neto superpuesta | Alta | `movements.hires.byMonth` + `movements.separations.series.byMonth` + `movements.net.byMonth` |
| 3 | Índice de rotación mensual | Línea mensual (con la fórmula en la leyenda — RN-10) | Alta | `movements.rotation` (+ `metadata.rotationFormula` en la leyenda) |
| 4 | Bajas por categoría / por motivo | Dona (categoría) + barras horizontales top-N (motivo) | Alta | `movements.separations.byCategory` / `.byReason` |
| 5 | Serie mensual de acciones | Barras mensuales (12 meses, ceros incluidos) | Alta | `personnel-actions.series.byMonth` |
| 6 | Acciones por tipo | Barras horizontales ordenadas desc (etiquetas del catálogo) | Alta | `personnel-actions.byType` |
| 7 | Acciones por dimensión organizativa | Barras horizontales con **selector de dimensión** (unidad/área/centro/puesto/tipo de puesto/planilla) | Alta | `personnel-actions.byDimension.{orgUnits,functionalAreas,workCenters,jobProfiles,positionCategories,payrollTypes}` |
| 8 | Acciones por estado | Dona o barras apiladas (universo completo — RN-04) | Media | `personnel-actions.byStatus` |
| 9 | Origen manual vs automático | Dona de 2 segmentos | Media | `personnel-actions.byOrigin` |
| 10 | Liquidaciones por estado | Tarjeta con mini-dona (conteos, sin montos — RN-16) | Media | `movements.settlementsByStatus` |
| 11 | Cobertura de entrevistas — detalle | Gauge/porcentaje con absolutos (complementa el KPI de la fila 1) | Media | `movements.exitInterviewCoverage` |

Notas de render:
- El **selector de dimensión** de la fila 7 alterna entre las 6 listas de `byDimension`; incluye "planilla" (`payrollTypes`).
- El bucket **`UNASSIGNED`** ("Sin asignar" en dimensiones documentales / "Sin dato" en `byPayrollType`) es una categoría legítima — muéstrala, no la ocultes.
- El **drill** (fila de KPIs → gráficos → bandeja `personnel-actions/query`) permite bajar al detalle fila-por-fila; encadena los filtros vigentes al abrir la bandeja.

---

## 11. Notas de despliegue / operación (contexto para el FE)

- **Migración destructiva de `payroll_type_code` (P-02, ya aplicada en PR-1).** Al desplegar, las plazas cuyo tipo de planilla no coincidía **exactamente** con un código del catálogo `payroll-types` quedaron en **NULL** (sin backfill). Efecto en la UI: muchas plazas caen en el bucket **"Sin dato"** de `byPayrollType` hasta que RRHH las reclasifique editando la plaza. Es el estado esperado; comunícalo con un aviso suave si el bucket "Sin dato" es grande.
- **`payrollTypeCode` se valida al escribir la plaza:** un código inexistente/inactivo → `422 PAYROLL_TYPE_INVALID` (esto ocurre en el módulo de plazas, no en el tablero; el tablero solo filtra/agrupa por él).
- **Sin storage/config/infra nuevos** para el tablero (P-01 = frontend). El export asíncrono usa el mismo `report-export-jobs` (que sí requiere el storage de exports ya provisionado, común a todos los exports async).
- **Contrato aditivo:** si consumes hoy `overview`/`hires`/`span-of-control`/`metadata`, siguen igual; solo aparecen campos nuevos. Ignora con tolerancia los campos que aún no uses.

---

## 12. Errores del módulo

| Código | HTTP | Cuándo |
|---|---|---|
| `DASHBOARD_MONTH_REQUIRES_YEAR` | 400 | `month` sin `year` (ni rango `fromUtc`/`toUtc`) en una sección de flujo o en la bandeja |
| (validación estándar) | 400 | `month` fuera de 1-12, `pageSize` fuera de 1-100, `toUtc` < `fromUtc` |
| (gate estándar) | 403 | Sin `ViewReports`/`Read`/`Admin` en cualquier endpoint (sección, bandeja, export, job) |
| `REPORT_EXPORT_...` / `413` | 413 | Export síncrono por encima del límite → usar el job asíncrono `COMPANY_PERSONNEL_ACTIONS` |

Los mensajes bilingües viajan en `problemDetails.detail` (mostrar tal cual); el código de negocio en `problemDetails.extensions.code`.
