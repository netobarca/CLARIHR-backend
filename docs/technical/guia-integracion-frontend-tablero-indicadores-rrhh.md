# Guía de Integración Frontend — Tablero de Indicadores de RRHH (HR Analytics Dashboard)

| | |
|---|---|
| **Audiencia** | Equipo Frontend (web) |
| **Backend** | CLARIHR API · prefijo `api/v1` |
| **Documentos fuente** | [`analisis-tablero-indicadores-rrhh.md`](../business/analisis-tablero-indicadores-rrhh.md) (negocio, D-01…D-14) · [`plan-tecnico-tablero-indicadores-rrhh.md`](./plan-tecnico-tablero-indicadores-rrhh.md) (técnico) |
| **Estado backend** | **Implementado (Fase 1)** — branch `feature/tablero-indicadores-rrhh-fase1`. Read-only. |
| **País de referencia (seed)** | El Salvador (SV) |
| **Fecha** | 2026-06-28 |

---

## 1. Qué se implementó (flujos)

Un **tablero de indicadores de RRHH de solo lectura** sobre el padrón de personal. El backend expone **4 endpoints de consulta** + la **parametrización** por empresa. Todos comparten un mismo conjunto de **filtros transversales**.

Indicadores disponibles (Fase 1):

- **Composición / headcount por categorías** (tipo de registro, estado de empleo, tipo de contrato, tipo de puesto, área funcional, unidad, centro de trabajo).
- **Distribución por edad** (rangos parametrizables).
- **Distribución por antigüedad** (rangos parametrizables).
- **Distribución por estado civil**.
- **Altas** (serie mensual por año).
- **Colaboradores por jefe** (tramo de control).
- **Personal de RRHH por cada 100 empleados**.
- **Expedientes actualizados vs. desactualizados**.
- **Plazas ocupadas vs. vacantes**.

> **Diferidos (NO disponibles todavía — ver §10):** **bajas**, **índice de rotación** (dependen del módulo *Baja de Personal*, aún no construido) y el **filtro por nivel de pirámide** (depende de una relación puesto↔nivel pendiente). El frontend **no** debe ofrecer estos controles aún.

Todos los datos son **agregados** (conteos/ratios). El tablero **no muta** datos (salvo la pantalla de parametrización, §8).

---

## 2. Autenticación y permisos

- Todas las llamadas requieren el **JWT** habitual (header `Authorization: Bearer …`) y van **scopeadas por compañía** vía `{companyId}` en la ruta.
- Permiso de lectura del tablero: **`PersonnelFiles.ViewReports`** (dedicado) **o** `PersonnelFiles.Read` **o** `PersonnelFiles.Admin`. Si el usuario no tiene ninguno → **403**.
- La **parametrización** (§8) usa el flujo estándar de *Company Preferences* (permiso de administración de la compañía).

---

## 3. Convenciones (recordatorio)

- Prefijo **`api/v1`** (no `/v1`).
- **Enums como strings** (p. ej. `recordType` = `"Employee"`).
- Errores con `extensions.code` (formato ProblemDetails).
- `If-Match` para la edición de preferencias (ver §8): faltante → 400, desactualizado → 409.
- Fechas en **UTC** (ISO-8601).

---

## 4. Filtros transversales

Los 4 endpoints aceptan, vía **query string**, el mismo conjunto de filtros (todos **opcionales**, combinables con AND). Los valores de dimensión son **PublicIds (GUID)** de las entidades correspondientes.

| Parámetro | Tipo | Aplica a | Descripción |
|---|---|---|---|
| `year` | int | overview, hires, span | Año de referencia. En `overview`/`span` es **snapshot al cierre del año** (aprox., ver §9-R-02); en `hires` es el año de las altas. |
| `functionalAreaId` | GUID | todos | PublicId del **área funcional** (de la unidad de la asignación activa). |
| `orgUnitId` | GUID | todos | PublicId de la **unidad**. |
| `positionCategoryId` | GUID | todos | PublicId del **tipo de puesto** (PositionCategory). |
| `jobProfileId` | GUID | todos | PublicId del **puesto** (JobProfile). |
| `workCenterId` | GUID | todos | PublicId del **centro de trabajo**. |
| `includeInactive` | bool | overview, span | Por defecto `false` (solo activos — D-03). `true` incluye inactivos en la composición. |

> Las dimensiones (área/unidad/tipo/puesto/centro) se resuelven por la **asignación activa primaria** del empleado. Empleados sin asignación caen en el bucket **"Sin asignar"** (`key = "UNASSIGNED"`).

---

## 5. Catálogos para poblar los filtros

El frontend obtiene las **opciones de los selectores** reutilizando los endpoints de listado ya existentes (cada uno devuelve `publicId` + nombre, que es lo que se pasa como filtro):

| Filtro | Fuente de opciones (endpoint existente) |
|---|---|
| Unidad | `GET api/v1/companies/{companyId}/organization-units` |
| Centro de trabajo | `GET api/v1/companies/{companyId}/work-centers` |
| Puesto | `GET api/v1/companies/{companyId}/job-profiles` |
| Tipo de puesto | `GET api/v1/companies/{companyId}/position-categories` |
| Área funcional | Catálogo de áreas funcionales (org-structure catalogs) |
| **Rangos de edad / antigüedad** | **`…/dashboard/metadata`** (ver §6.1) — solo informativo/legendas; el bucketizado lo hace el backend |

---

## 6. Endpoints

Ruta base: `api/v1/companies/{companyId}/personnel-files/dashboard`.

### 6.1 `GET …/dashboard/metadata`

Devuelve los **rangos parametrizables** (con sus cotas, para etiquetas/leyendas) y la **configuración** resuelta de la empresa. Llamar **una vez** al cargar el tablero.

**Respuesta `200`:**

```json
{
  "ageRanges": [
    { "code": "EDAD_18_25", "label": "18 a 25 años", "lowerBound": 18, "upperBound": 25 },
    { "code": "EDAD_26_35", "label": "26 a 35 años", "lowerBound": 26, "upperBound": 35 },
    { "code": "EDAD_36_45", "label": "36 a 45 años", "lowerBound": 36, "upperBound": 45 },
    { "code": "EDAD_46_55", "label": "46 a 55 años", "lowerBound": 46, "upperBound": 55 },
    { "code": "EDAD_56_MAS", "label": "56 años o más", "lowerBound": 56, "upperBound": null }
  ],
  "seniorityRanges": [
    { "code": "ANT_0_1", "label": "Menos de 1 año", "lowerBound": 0, "upperBound": 11 },
    { "code": "ANT_1_3", "label": "1 a 3 años", "lowerBound": 12, "upperBound": 35 },
    { "code": "ANT_3_5", "label": "3 a 5 años", "lowerBound": 36, "upperBound": 59 },
    { "code": "ANT_5_10", "label": "5 a 10 años", "lowerBound": 60, "upperBound": 119 },
    { "code": "ANT_10_MAS", "label": "10 años o más", "lowerBound": 120, "upperBound": null }
  ],
  "fileUpToDateThresholdMonths": 12,
  "hrFunctionalAreaCode": null
}
```

- `upperBound: null` = rango abierto (sin límite superior).
- `hrFunctionalAreaCode: null` ⇒ el indicador de RRHH/100 está **sin configurar** (ver §6.2 y §8).

### 6.2 `GET …/dashboard/overview`

El **payload principal** del tablero: headcount + todos los desgloses + indicadores de estructura/calidad. Acepta todos los filtros de §4.

**Respuesta `200` (abreviada):**

```json
{
  "headcount": { "total": 128, "active": 120, "inactive": 8 },
  "byRecordType":       [ { "key": "Employee", "label": "Employee", "count": 128 } ],
  "byEmploymentStatus": [ { "key": "ACTIVO", "label": "ACTIVO", "count": 120 }, { "key": "SUSPENDIDO", "label": "SUSPENDIDO", "count": 8 } ],
  "byContractType":     [ { "key": "INDEFINIDO", "label": "INDEFINIDO", "count": 100 }, { "key": "PLAZO_FIJO", "label": "PLAZO_FIJO", "count": 28 } ],
  "byPositionCategory": [ { "key": "<guid>", "label": "Operativo", "count": 70 }, { "key": "UNASSIGNED", "label": "Sin asignar", "count": 5 } ],
  "byFunctionalArea":   [ { "key": "<guid>", "label": "Recursos Humanos", "count": 6 } ],
  "byOrgUnit":          [ { "key": "<guid>", "label": "Operaciones", "count": 80 } ],
  "byWorkCenter":       [ { "key": "<guid>", "label": "Planta Central", "count": 90 } ],
  "byAgeRange":         [ { "key": "EDAD_18_25", "label": "18 a 25 años", "count": 20 }, { "key": "UNASSIGNED", "label": "Sin dato", "count": 0 } ],
  "bySeniorityRange":   [ { "key": "ANT_0_1", "label": "Menos de 1 año", "count": 15 } ],
  "byMaritalStatus":    [ { "key": "SOLTERO_A", "label": "SOLTERO_A", "count": 60 } ],
  "fileFreshness":   { "upToDate": 110, "outdated": 18, "thresholdMonths": 12 },
  "hrRatio":         { "hrHeadcount": 6, "totalHeadcount": 128, "ratioPer100": 4.69, "configured": true },
  "positionOccupancy": { "maxPositions": 140, "occupied": 120, "vacant": 20 }
}
```

Notas de render:
- Cada `by*` es una lista de `{ key, label, count }` ordenada por `count` desc. `key` = PublicId (para dimensiones) o código de catálogo/enum; **`label`** es lo que se muestra.
- `byEmploymentStatus`/`byContractType`/`byMaritalStatus` devuelven el **código** como label (p. ej. `SOLTERO_A`); el FE puede mapearlo a su display name vía el catálogo correspondiente (`marital-statuses`, `employment-statuses`, `contract-types`).
- Buckets **"Sin asignar"** / **"Sin dato"** (`key = "UNASSIGNED"`) son explícitos: muéstralos como una categoría más (no los ocultes).
- `byAgeRange`/`bySeniorityRange` siguen el orden de `metadata`; el bucket final `Sin dato` aparece solo si hay empleados sin fecha.
- **`hrRatio.configured = false`** ⇒ render "No configurado" (no muestres un 0 como si fuera real); se activa configurando el área de RRHH (§8).
- `ratioPer100` puede ser `null` si `totalHeadcount = 0`.

### 6.3 `GET …/dashboard/hires`

**Altas** (D-02) por mes para un año. Bajas/rotación **no** se incluyen (diferidas — §10).

Query: `year` (opcional, default año actual) + filtros de dimensión (§4; `includeInactive` no aplica — se cuentan todas las altas del año).

**Respuesta `200`:**

```json
{
  "year": 2026,
  "byMonth": [
    { "month": 1, "count": 4 }, { "month": 2, "count": 2 }, { "month": 3, "count": 0 },
    { "month": 4, "count": 5 }, { "month": 5, "count": 1 }, { "month": 6, "count": 3 },
    { "month": 7, "count": 0 }, { "month": 8, "count": 0 }, { "month": 9, "count": 0 },
    { "month": 10, "count": 0 }, { "month": 11, "count": 0 }, { "month": 12, "count": 0 }
  ],
  "total": 15
}
```

`byMonth` siempre trae los **12 meses** (rellena ceros). Ideal para un gráfico de barras/línea por mes.

### 6.4 `GET …/dashboard/span-of-control`

**Colaboradores por jefe** (D-05). El jefe es el ocupante de la plaza de la que depende directamente la plaza del colaborador. Acepta filtros de §4 + `includeInactive`.

**Respuesta `200`:**

```json
{
  "managers": [
    { "managerEmployeeId": "<guid>", "managerName": "María López", "positionTitle": "Jefe de Operaciones", "directReports": 12 },
    { "managerEmployeeId": "<guid>", "managerName": "Juan Pérez", "positionTitle": "Supervisor", "directReports": 5 }
  ],
  "withoutManagerCount": 8,
  "totalEmployees": 120
}
```

- `managers` ordenado por `directReports` desc.
- `withoutManagerCount` = empleados (de la población filtrada) sin un jefe resoluble (plaza superior vacante o sin dependencia). Útil como barra "Sin jefe".
- `managerEmployeeId` es el PublicId del empleado-jefe (puedes enlazar a su expediente).

---

## 7. Mapa indicador de negocio → endpoint

| Indicador (negocio) | Endpoint · campo |
|---|---|
| Cantidad de empleados por categorías | `overview` · `headcount` + `byRecordType`/`byEmploymentStatus`/`byContractType`/`byPositionCategory`/`byFunctionalArea`/`byOrgUnit`/`byWorkCenter` |
| Altas | `hires` · `byMonth` / `total` |
| Edad | `overview` · `byAgeRange` (+ leyendas de `metadata.ageRanges`) |
| Antigüedad | `overview` · `bySeniorityRange` (+ `metadata.seniorityRanges`) |
| Estado civil | `overview` · `byMaritalStatus` |
| Colaboradores por jefe | `span-of-control` · `managers` |
| Personal de RRHH por 100 | `overview` · `hrRatio` |
| Expedientes actualizados/desactualizados | `overview` · `fileFreshness` |
| Plazas ocupadas/vacantes | `overview` · `positionOccupancy` |
| **Bajas / Rotación / Nivel de pirámide** | **No disponibles (diferidos — §10)** |

---

## 8. Parametrización (pantalla de configuración del tablero)

Dos parámetros por empresa viven en **Company Preferences**:

- **`hrFunctionalAreaCode`**: el **código de área funcional** que identifica RRHH (habilita `hrRatio.configured`). Debe ser un `code` válido del catálogo de áreas funcionales (p. ej. `RRHH`).
- **`fileUpToDateThresholdMonths`**: ventana (meses) para "expediente actualizado" (default 12 si null).

### Leer la configuración
`GET api/v1/companies/{companyId}/preferences` →
```json
{
  "id": "<guid>",
  "currencyCode": "USD",
  "timeZone": "America/El_Salvador",
  "hrFunctionalAreaCode": "RRHH",
  "fileUpToDateThresholdMonths": 12,
  "concurrencyToken": "<guid>",
  "createdAtUtc": "…",
  "modifiedAtUtc": "…"
}
```

### Guardar la configuración
`PUT api/v1/companies/{companyId}/preferences` con header **`If-Match: <concurrencyToken>`** y body:
```json
{
  "currencyCode": "USD",
  "timeZone": "America/El_Salvador",
  "hrFunctionalAreaCode": "RRHH",
  "fileUpToDateThresholdMonths": 12
}
```
- `hrFunctionalAreaCode` y `fileUpToDateThresholdMonths` son **opcionales** (puedes mandar `null` para limpiar / volver al default).
- `fileUpToDateThresholdMonths` debe ser **> 0** si se envía (si no → 400).
- Devuelve el registro actualizado + nuevo `concurrencyToken` (también en el header `ETag`).
- (El `PATCH` de preferences sigue cubriendo solo `currencyCode`/`timeZone`; para estos dos campos usa `PUT`.)

> Tras cambiar `hrFunctionalAreaCode` o el umbral, **refresca** `metadata` y `overview` para reflejar el nuevo cálculo.

---

## 9. Notas y gotchas

- **R-02 — "year" en indicadores snapshot es aproximado.** No existen *snapshots* históricos de plantilla; con `year`, el backend aproxima "activo al 31-Dic de ese año" desde `HireDate`/`RetirementDate`. Para el estado **actual**, no envíes `year`. Comunica esta aproximación si muestras años pasados.
- **R-03 — recontratación sobrescribe la fecha de alta.** Un empleado recontratado aparece como **alta del año de su recontratación** (pierde la fecha original). Aplica a `hires` y a la antigüedad.
- **Población por defecto = solo activos.** Usa `includeInactive=true` solo si el usuario lo pide explícitamente.
- **"Sin asignar"/"Sin dato"** son categorías legítimas (no errores). Si un empleado no tiene asignación activa, sus dimensiones de puesto/unidad/centro/área son "Sin asignar".
- **`hrRatio.configured=false`** y **`ratioPer100=null`**: estados válidos, no son errores; renderiza un placeholder ("No configurado" / "N/D").
- **Consistencia de filtros**: aplica el **mismo** objeto de filtros a `overview`, `hires` y `span-of-control` para que todos los gráficos cuenten lo mismo.
- **Rendimiento**: son agregaciones; cachea `metadata` por sesión y *debounce* los cambios de filtro.

---

## 10. Fuera de alcance (Fase 1) — NO ofrecer en la UI todavía

| No disponible | Motivo | Cuándo |
|---|---|---|
| **Bajas** (serie) | Depende del módulo **Baja de Personal** (aún no construido) | Fase 2 |
| **Índice de rotación** | Necesita las bajas (módulo Baja de Personal) | Fase 2 |
| **Filtro por nivel de pirámide** | Falta la relación canónica puesto↔nivel (módulo puestos/competencias) | Fase 2 |
| **Exportación por indicador** | Diferida (se reutilizará `report-export-jobs`) | Fase 2 |

El tablero ya deja "ganchos" para estos (el filtro de nivel y las series de bajas/rotación se añadirán sin romper los contratos actuales).

---

## 11. Checklist de integración

- [ ] Cargar `metadata` al abrir el tablero (rangos + config).
- [ ] Selectores de filtro poblados desde los endpoints de §5; pasar **PublicId**.
- [ ] Mismo objeto de filtros para `overview` / `hires` / `span-of-control`.
- [ ] Render de buckets "Sin asignar"/"Sin dato" como categoría.
- [ ] Manejo de `hrRatio.configured=false` y `ratioPer100=null`.
- [ ] Toggle `includeInactive` (default off).
- [ ] `hires`: gráfico de 12 meses; ocultar/avisar sobre bajas (no disponibles).
- [ ] Pantalla de parametrización (RRHH-area + umbral) con `If-Match`.
- [ ] **No** mostrar bajas, rotación ni filtro de nivel de pirámide (Fase 2).
- [ ] Estados 401/403 (sin permiso `ViewReports`/`Read`).
