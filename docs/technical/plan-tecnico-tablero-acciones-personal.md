# Plan Técnico — Tablero de gráficos e indicadores de acciones de personal (REQ-004)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación (Fase 1) |
| **Audiencia** | Equipo de desarrollo backend, QA, tech lead, frontend (contratos) |
| **Documento de negocio** | [`analisis-tablero-acciones-personal.md`](../business/analisis-tablero-acciones-personal.md) — **RATIFICADO 2026-07-05** (D-01…D-18 por unanimidad; P-01…P-14 respondidas; cierres: P-01 impresión/PDF **frontend** · P-02 catálogo país + **limpieza destructiva** sin backfill · P-03 indicadores D-04 + mapa de gráficos A.5) |
| **Módulos** | Reportería/Analítica (`PersonnelFiles/Reporting` — se extiende el tablero PR #52) · Journal de acciones (`PersonnelFilePersonnelAction`, solo lectura + 1 índice) · Asignaciones/plazas (`payrollTypeCode`: validación + limpieza) · Catálogos generales (1 catálogo nuevo) · Retiros/Entrevistas/Liquidaciones (solo lectura) |
| **Estado** | Propuesto — listo para implementar |
| **Fecha** | 2026-07-05 |
| **País de referencia (seed)** | El Salvador (SV) |
| **Naturaleza** | **Capa analítica read-only aditiva** sobre el tablero existente. Únicas escrituras: migración (catálogo + limpieza + índice) y la validación estricta de `payrollTypeCode` en plazas. **Cero superficie PDF backend** (P-01: la impresión/exportación PDF es del frontend). |

---

## 0. Aclaraciones pre-desarrollo (ratificación ya cerrada)

1. **PDF/impresión = frontend (P-01).** No se construye ningún endpoint/renderer/bloque de PDF. El backend garantiza agregados completos y la **guía FE** especifica la vista de impresión (encabezado: empresa + filtros + fecha de generación — RN-13) y el **mapa de gráficos A.5** como especificación de la vista. El reporte servidor (A.4-b) queda como referencia F2: no dejar ganchos de código.
2. **Limpieza destructiva de `payroll_type_code` (P-02), sin backfill.** En la migración M1, dos pasos: (a) normalizar (`UPPER(TRIM(...))`) los valores que coinciden con códigos del catálogo A.2; (b) **poner en NULL todo lo demás**. Sin flag de compatibilidad ni ruta legacy: el validador estricto rige desde el despliegue. Comunicar en el checklist §9 (con query de conteo previa como respaldo informativo).
3. **`payrollTypeCode` ≠ `contractTypeCode`.** Coexisten en `PersonnelFileEmploymentAssignment`: `contractTypeCode` = naturaleza del contrato (INDEFINIDO/PLAZO_FIJO…, catálogo existente con `IsTemporary`); `payrollTypeCode` = **modalidad de pago contractual** (MENSUAL/QUINCENAL/… — catálogo nuevo `payroll-types`). No unificar ni derivar uno del otro. **Coordinación REQ-001**: sus incapacidades referencian *este* catálogo; el requerimiento que se construya primero lo siembra con los IDs de este plan (§4) y el otro lo reutiliza.
4. **Fuente canónica de movimientos = perfil, nunca el journal (D-03/RN-03/RN-09).** Bajas = `PersonnelFileEmployeeProfile.RetirementDate` + `RetirementCategoryCode`/`RetirementReasonCode`; altas = `HireDate`; una **reversión** limpia `RetirementDate` → la baja sale de series/ratios (los asientos `BAJA`/`REVERSION_BAJA` permanecen solo en los indicadores documentales). El asiento `CONTRATACION` no lo escribe nadie y `BAJA` solo existe desde PR #55 — por eso el journal no sirve para movimientos.
5. **Índice nuevo del journal.** Los índices actuales anteponen `PersonnelFileId` (`PersonnelFileEmployeeConfiguration.cs:208`); la consulta corporativa filtra por `(TenantId, ActionDateUtc)`. M1 agrega `ix_personnel_file_personnel_actions_tenant_action_date` sobre `(tenant_id, action_date_utc)` con INCLUDE (`action_type_code`, `action_status_code`, `is_system_generated`, `personnel_file_id`). Validar con EXPLAIN en datos realistas antes de cerrar PR-3.
6. **Contrato 100 % aditivo del tablero.** No se toca la forma de `overview`/`hires`/`span-of-control`/`metadata`. Extensiones permitidas: campos nuevos en respuestas (p. ej. `byPayrollType` en overview, campos nuevos en metadata) y **parámetros opcionales** nuevos en `DashboardDimensionFilter` (`payrollTypeCode`, `costCenterId`). `month` existe **solo** en los endpoints nuevos (flujo); `dashboard/hires` no cambia (la vista de movimientos calcula sus propias altas con el mismo criterio).
7. **Población documental por defecto (D-05/RN-04):** estado `APLICADA`; parámetro `includeAllStatuses=true` para el universo completo; el desglose `byStatus` SIEMPRE se calcula sobre el universo completo (independiente del default).
8. **Sin montos bajo `ViewReports` (D-15/RN-16):** ni `Amount` ni agregados monetarios en respuestas, bandeja ni exports. Añadir un **test de contrato** que serialice las respuestas nuevas y falle si aparece `amount`/`currency` (el asiento de liquidación lleva el neto — es el dato a proteger).
9. **Dimensiones por asignación activa primaria actual (D-07):** las acciones se unen por `PersonnelFileId` al *row bundle* dimensional existente (diccionario `PersonnelFileId → EmployeeDimensionRow`). Aproximación documentada en guía/leyendas; sin snapshot en F1.
10. **Rotación y cobertura (D-08/RN-10/RN-15):** rotación = bajas ÷ headcount promedio ((activos al inicio + activos al fin)/2, aproximados con `HireDate`/`RetirementDate` — misma semántica R-02 del tablero) × 100; promedio 0 → `ratePercent = null` ("N/D"). Cobertura = bajas del periodo con `ExitInterviewSubmission` **completada** ÷ bajas del periodo — **verificar al implementar el valor exacto del enum de estado** de la submission (PR-4).
11. **Bandeja = familia Reporting.** `POST …/query` **sin** `[AuthorizationPolicySet]` (precedente documentado en `PersonnelFileReportingController.cs:22-26` y `SettlementsReportingController.cs:16-21` para que la convención no asigne política Manage a una lectura); gate `EnsureCanViewReportsAsync` en el handler; `StatusCounts` como liquidaciones.
12. **Governance/localización:** los endpoints viven en la familia Reporting (excluida del convention-test por regex). El validador nuevo de plaza **sin `.WithMessage` custom** salvo con clave resx (trampa conocida del test de paridad de localización — gotcha del tablero F1). Labels de desgloses provienen de catálogos/estructura (nunca strings codificados).
13. **openapi:** regenerar sin drift al cierre (PR-5) y **verificar el estado real del contrato publicado** — la exploración detectó que `docs/technical/api/openapi.yaml` no declara hoy las rutas de la familia dashboard/reportería (hallazgo pre-existente; si el archivo publicado está desactualizado, documentarlo o regenerarlo completo según decida el equipo en PR-5).
14. **Seeds:** bloque tentativo `-9520…-9525` (§4). **Verificar IDs libres contra `GlobalCatalogSeedData` al abrir PR-1** (reservas vigentes: REQ-001 `-9485…-9489`/`-9850…-9862`, REQ-002 `-9865…-9871`, REQ-003 `-9875…-9879`; rangos del tablero `-9500…-9514`; trampa `-9490…-9496` = `ACTION_STATUS_CATALOG`).

---

## 1. Objetivo y enfoque

Extender el tablero existente (familia Reporting, PR #52) con dos secciones nuevas — **acciones documentales** (primera consulta corporativa del journal) y **movimientos** (bajas/rotación/altas-neto/cobertura/liquidaciones, activando los diferidos del tablero RRHH) —, tres filtros nuevos (**mes**, **tipo de planilla**, **centro de costo**), la **bandeja corporativa de asientos** (drill) y las **exportaciones tabulares**. La impresión/exportación PDF es del frontend (P-01).

**Patrón base:** replicar exactamente la arquitectura del tablero F1 — proyección/row-bundle en `PersonnelFileDashboardRepository`, reglas puras (`PersonnelFileDashboardRules`), queries CQRS read-only con gate `EnsureCanViewReportsAsync` en el handler, controlador `PersonnelFileReportingController` sin `[AuthorizationPolicySet]`, catálogos país `HasData`.

---

## 2. Línea base verificada en el código (qué se reutiliza / qué se toca)

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Journal | `PersonnelFilePersonnelAction` (`PersonnelFileEmployee.cs:571-643`); tabla `personnel_file_personnel_actions`; config+índices `PersonnelFileEmployeeConfiguration.cs:208` (índices anteponen `PersonnelFileId`) | Fact table de la sección documental; **índice nuevo** (aclaración №5); sin cambios de entidad |
| 2 | Catálogos del journal | `ACTION_TYPE_CATALOG` `-9470…-9484` / `ACTION_STATUS_CATALOG` `-9490…-9496` (`GlobalCatalogSeedData.cs:734/753`); keys `action-types`/`action-statuses` | Ejes por tipo/estado; leyendas; sin seeds nuevos aquí |
| 3 | Escritores automáticos | `ExecuteRetirementRequest.cs:179` (BAJA) · `RevertRetirementRequest.cs:204` (REVERSION_BAJA) · `RehireEmployee.cs:250` (RECONTRATACION) · `Settlements.Handlers.cs:995` (LIQUIDACION, `Amount`=neto) · manual `Employment/PersonnelActions.cs` | Indicador de origen (`IsSystemGenerated`); el `Amount` de liquidación motiva el test sin-montos |
| 4 | Consulta actual del journal | Solo por expediente: `SearchPersonnelActionsAsync`/`ExportPersonnelActionsAsync` (`PersonnelFileEmployeeRepository.cs:678+`); export async resource `PERSONNEL_FILE_PERSONNEL_ACTIONS` (`DependencyInjection.cs:189`) | Referencia de columnas/filtros para la bandeja corporativa nueva |
| 5 | Tablero existente | `PersonnelFileReportingController.cs` — `dashboard/{overview,hires,span-of-control,metadata}`; queries/DTOs en `Features/PersonnelFiles/Reporting/PersonnelFileDashboard*.cs`; gate `EnsureCanViewReportsAsync` (`IPersonnelFileAuthorizationService.cs:168`) | Se extiende; contratos existentes intactos (aclaración №6) |
| 6 | Capa dimensional | `PersonnelFileDashboardRepository` (DI `DependencyInjection.cs:174`): row bundle por asignación activa primaria con OrgUnit/FunctionalArea/WorkCenter/JobProfile/PositionCategory; `EmployeeDimensionRow` incluye `HireDate`/`RetirementDate` | Se amplía con `PayrollTypeCode`, `CostCenterPublicId/Name` y `RetirementCategoryCode`/`RetirementReasonCode` |
| 7 | `payrollTypeCode` | Campo string ≤ 80 en la asignación; validación solo de longitud (`EmploymentAssignments.cs`); FE ya lo captura (`employment-information-frontend-integration.md`, ej. `"MENSUAL"`); **no** proyectado ni filtrable | Catálogo + validate-by-code + limpieza (aclaraciones №2/№3) |
| 8 | Centro de costo | `CostCenter` (`Domain/CostCenters/CostCenter.cs`) + `CostCenterPublicId` en la asignación | Filtro/desglose opcional de bajo costo (P-12) — solo proyección + filtro |
| 9 | Retiro | Perfil estampado (`RetirementDate`/`RetirementCategoryCode`/`RetirementReasonCode`); catálogos de categoría/motivo; reversión restaura el perfil | Fuente de bajas/rotación (aclaración №4) |
| 10 | Entrevistas de retiro | `ExitInterviewSubmission` (`ExitInterviewSubmission.cs`): `PersonnelFileId`, `Status`, `SubmittedUtc` | Cobertura (aclaración №10) |
| 11 | Liquidaciones | `PersonnelFileSettlement.StatusCode`; bandeja con `StatusCounts` (`SettlementsBandeja.cs`) | Conteos por estado; molde de la bandeja |
| 12 | Export tabular | `ReportExportFileWriter` (csv/json/xlsx OpenXML manual) + `ReportExportDeliveryService` (límite síncrono→413, auditoría `ReportExported`) + `report-export-jobs` (whitelist `ReportExportResources.cs`, authorizer por resource) | Exports de datasets y bandeja; 1 resource key nuevo |
| 13 | Catálogo por key | `GeneralCatalogKeyMap` (+ guardrail tests) · `GeneralCatalogsController` GET por key · patrón subclase de `GeneralCatalogItem` (p. ej. `ActionTypeCatalogItem`, `GeneralCatalogItems.cs:881`) | Molde de `PayrollTypeCatalogItem` + key `payroll-types` |
| 14 | Governance/localización | Familia Reporting excluida del convention-test (regex); paridad de localización escanea `.WithMessage` | Aclaraciones №11/№12 |

---

## 3. Arquitectura de la solución

### 3.1 Catálogo de tipos de planilla (D-10/RF-003) — M1

- **Entidad**: `PayrollTypeCatalogItem : GeneralCatalogItem` (espejo de `ActionTypeCatalogItem`), DbSet `PayrollTypeCatalogItems`, catalog-type **`PAYROLL_TYPE_CATALOG`**, wire key **`payroll-types`** (alta en `GeneralCatalogKeyMap` + guardrail), categoría de validación nueva (p. ej. `PersonnelCurriculumCatalogCategories.PayrollType = "PayrollType"`).
- **Seed SV (`HasData`, editable)**: los 6 valores A.2 — `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO` — IDs tentativos `-9520…-9525` (aclaración №14).
- **Validación en plaza**: en el validador/flujo de `EmploymentAssignments` (POST/PUT/PATCH), `payrollTypeCode` opcional pero, si viaja, debe resolver a un item **activo** del catálogo país → 422 `PAYROLL_TYPE_INVALID` bilingüe (mismo mecanismo validate-by-code que usan los demás códigos de la asignación; si `contractTypeCode` hoy solo valida longitud, **no** retrofitear otros códigos en este REQ — alcance solo `payrollTypeCode`).
- **Limpieza destructiva (migración, aclaración №2)**:

```sql
-- Paso 1: normalizar coincidencias exactas (case/espacios)
UPDATE personnel_file_employment_assignments
SET payroll_type_code = UPPER(TRIM(payroll_type_code))
WHERE payroll_type_code IS NOT NULL
  AND UPPER(TRIM(payroll_type_code)) IN ('MENSUAL','QUINCENAL','SEMANAL','POR_DIA','POR_OBRA','OTRO');

-- Paso 2: eliminar todo lo no conforme (sin datos legacy)
UPDATE personnel_file_employment_assignments
SET payroll_type_code = NULL
WHERE payroll_type_code IS NOT NULL
  AND payroll_type_code NOT IN ('MENSUAL','QUINCENAL','SEMANAL','POR_DIA','POR_OBRA','OTRO');
```

### 3.2 Extensión de la capa dimensional (RF-002/RF-003/P-12)

- `EmployeeDimensionRow` += `PayrollTypeCode` (string?), `CostCenterPublicId` (Guid?), `CostCenterName` (resuelto en memoria como las demás dimensiones), `RetirementCategoryCode`/`RetirementReasonCode` (string?, del perfil — para los desgloses de bajas).
- `DashboardDimensionFilter` += `PayrollTypeCode` (string?, código), `CostCenterId` (Guid?), `Month` (int?, 1-12 — **consumido solo por las queries de flujo**; las snapshot lo ignoran y la metadata lo declara).
- `overview` gana el desglose **`byPayrollType`** (aditivo, mismo shape `DashboardBreakdownResponse`; bucket `UNASSIGNED` = "Sin dato"). Los filtros nuevos aplican a **todos** los endpoints del tablero (overview/hires/span incluidos) por vivir en el filtro común.

### 3.3 Consulta corporativa del journal (RF-001) — repositorio

Ampliar `IPersonnelFileDashboardRepository`/`PersonnelFileDashboardRepository`:

```csharp
// Filas mínimas del journal del tenant en el rango (AsNoTracking + Select)
Task<IReadOnlyList<ActionFactRow>> GetPersonnelActionFactsAsync(
    long tenantId, int year, int? month, bool includeAllStatuses, CancellationToken ct);
// ActionFactRow: ActionTypeCode, ActionStatusCode, ActionDateUtc, IsSystemGenerated, PersonnelFileId
```

- Filtro base: `TenantId == tenantId && ActionDateUtc` dentro del año (y mes si viaja). `includeAllStatuses=false` → `ActionStatusCode == APLICADA`; el desglose `byStatus` usa una pasada sobre el universo completo (dos listas o una con flag — decisión de implementación, ver §6 test 3).
- Dimensiones: diccionario `PersonnelFileId → EmployeeDimensionRow` del row bundle existente (una sola consulta adicional reutilizada); acciones de expedientes sin fila dimensional → "Sin asignar".
- Movimientos: el row bundle ya trae `HireDate`/`RetirementDate` (+ categoría/motivo tras §3.2) → bajas/altas/neto/rotación se calculan **sin ir al journal**. Cobertura: query puntual a `ExitInterviewSubmissions` por los `PersonnelFileId` de las bajas del periodo con estado completado. Liquidaciones: `GROUP BY StatusCode` del rango.

### 3.4 Reglas puras (RF-005…RF-014)

Nuevo `PersonnelActionsDashboardRules` (+ extensión de `PersonnelFileDashboardRules` para movimientos), sin EF, unit-testable:

- `BuildActionsSeries(rows, year, month?)` → 12 meses rellenos con ceros + total.
- `BuildBreakdown(rows, selector, labels)` → `[{key,label,count}]` desc (reutilizar el helper existente si aplica).
- `BuildSeparations(dimensionRows, year, month?)` → serie + `byCategory`/`byReason`.
- `ComputeRotation(separations, headcountStart, headcountEnd)` → `ratePercent` (null si promedio 0).
- `ComputeNet(hiresByMonth, separationsByMonth)`.
- `ComputeExitInterviewCoverage(separationFileIds, completedFileIds)`.

### 3.5 Endpoints (extensión de `PersonnelFileReportingController` — familia Reporting)

Ruta base existente `api/v1/companies/{companyId:guid}/personnel-files/...`; todos con gate `EnsureCanViewReportsAsync` + `[EnableRateLimiting(Search|Export)]`:

| Endpoint | Respuesta (resumen) | RFs |
|---|---|---|
| `GET .../dashboard/personnel-actions?year=&month=&includeAllStatuses=&<filtros>` | `{ year, month, series{byMonth[12],total}, byType[], byStatus[], byOrigin[{manual,system}], byDimension{orgUnits[],functionalAreas[],workCenters[],jobProfiles[],positionCategories[],payrollTypes[]} }` | RF-005…RF-009 |
| `GET .../dashboard/movements?year=&month=&<filtros>` | `{ year, month, hires{byMonth,total}, separations{byMonth,total,byCategory[],byReason[]}, net{byMonth,total}, rotation{annual{separations,averageHeadcount,ratePercent}, byMonth[]}, exitInterviewCoverage{separations,completed,coveragePercent}, settlements{byStatus[]} }` | RF-010…RF-014 |
| `GET .../dashboard/metadata` (extendida, aditiva) | += `{ sections:[{key,active,acceptsMonth}], filters:[{key,enabled}], rotationFormula, payrollTypes[], actionTypes[], actionStatuses[] }` | RF-004/RF-018 |
| `POST .../personnel-actions/query` (bandeja) | Paginada + `StatusCounts`; filtros: tipo/estado/origen/rango de fechas/empleado/unidad; fila **sin `Amount`** | RF-017 |
| `GET .../personnel-actions/export?format=` · `GET .../dashboard/export?dataset=&format=` | Bandeja (límite síncrono → 413 + job async) · datasets agregados (siempre síncrono) | RF-016 |

`sections` en metadata declara las fuentes conectables (RF-018): F1 activa `PERSONNEL_ACTIONS`/`MOVEMENTS`; `INCAPACIDADES`/`VACACIONES` (REQ-001), `RECONOCIMIENTOS`/`AMONESTACIONES` (REQ-003) y `TIEMPO_COMPENSATORIO` (REQ-002) nacen `active:false` — cada módulo las activa al conectarse (mismo espíritu que `activeSources[]` de REQ-003 §3.11: fuentes = métodos del repositorio, contrato estable).

### 3.6 Exportaciones (RF-016)

- **Datasets agregados**: `dashboard/export?dataset=personnel-actions|movements&format=xlsx|csv|json` → filas en español vía `ReportExportFileWriter` + `ReportExportDeliveryService` (agregados pequeños: siempre síncrono).
- **Bandeja**: export propio con límite síncrono (413) + **resource key asíncrono nuevo** `COMPANY_PERSONNEL_ACTIONS` (whitelist `ReportExportResources` + handler espejo de `PersonnelFilePersonnelActionsExportHandler` a nivel tenant + authorizer → gate `ViewReports`).
- Ninguna columna monetaria en ningún export (aclaración №8).

### 3.7 Permisos y seguridad

- **Sin permisos nuevos**: todo bajo `EnsureCanViewReportsAsync` (`ViewReports` ∨ `Read` ∨ `Admin`) — D-16.
- Bandeja y exports en el mismo gate; el authorizer de export-jobs mapea el resource nuevo al mismo gate.
- Test de contrato sin-montos (aclaración №8).

### 3.8 Localización y auditoría

- Mensajes nuevos (`PAYROLL_TYPE_INVALID`, `DASHBOARD_MONTH_REQUIRES_YEAR`, `DASHBOARD_EXPORT_DATASET_INVALID`) con claves resx ES/EN (paridad de localización).
- Labels de desgloses = nombres de catálogo/estructura (sin literales); auditoría de exports = `ReportExported` existente.

---

## 4. Migraciones y seeds

**M1 — `AddPayrollTypeCatalogAndActionsDashboard` (PR-1):**
1. Tabla/discriminador del catálogo (`PayrollTypeCatalogItem`) + `HasData` SV: `MENSUAL=-9520`, `QUINCENAL=-9521`, `SEMANAL=-9522`, `POR_DIA=-9523`, `POR_OBRA=-9524`, `OTRO=-9525` (**verificar libres al abrir PR-1**; país SV = `CountryCatalogDefinition(-7068)`).
2. SQL de limpieza destructiva de `payroll_type_code` (§3.1 — normaliza y anula; idempotente).
3. Índice `ix_personnel_file_personnel_actions_tenant_action_date` (§0.5).

Recordatorios: `DOTNET_ROLL_FORWARD=Major` para `dotnet ef`; migración sin drift contra el modelo; actualizar guardrail tests de `GeneralCatalogKeyMap`.

---

## 5. Mapa de errores (resumen)

| Caso | Código | HTTP |
|---|---|---|
| `month` sin `year` (o fuera de 1-12) | `DASHBOARD_MONTH_REQUIRES_YEAR` / validación estándar | 400 |
| `payrollTypeCode` inexistente/inactivo al escribir plaza | `PAYROLL_TYPE_INVALID` | 422 |
| `dataset` de export desconocido | `DASHBOARD_EXPORT_DATASET_INVALID` | 400 |
| Sin `ViewReports`/`Read`/`Admin` | gate estándar | 403 |
| Export de bandeja sobre el límite síncrono | patrón existente | 413 |
| Filtro con PublicId inexistente | población vacía (comportamiento actual del tablero — no es error) | 200 |

---

## 6. Plan de pruebas

**Unitarias (reglas puras — casos dorados A.3):**
1. Serie 12 meses con ceros; default `APLICADA` excluye `ANULADA`; `includeAllStatuses` las incluye (A.3-1).
2. `byStatus` siempre sobre universo completo aunque el default filtre (A.3-1).
3. Breakdown por tipo/origen ordenado desc; códigos no catalogados agrupan por literal.
4. Bajas por mes/categoría/motivo; baja revertida (RetirementDate null) no cuenta (A.3-2/3).
5. Rotación: 2 bajas / promedio 100 → 2.0 %; promedio 0 → null (A.3-4).
6. Neto altas−bajas (A.3-5).
7. Cobertura 3/4 → 75 %; denominador excluye revertidas (A.3-9).
8. `month` restringe series; snapshot ignora month (semántica del filtro).
9. Dimensión aproximada: acción mapea a la unidad ACTUAL del empleado; sin asignación → "Sin asignar" (A.3-6).

**Integración:**
1. Agregados de `personnel-actions` y `movements` cuadran contra fixtures deterministas (retiro ejecutado/revertido, liquidación emitida, asientos manuales).
2. Filtros combinados (unidad + planilla + mes) consistentes entre secciones; filtros nuevos también sobre `overview` (byPayrollType).
3. **Migración de limpieza**: sembrar plazas con `"MENSUAL"`, `" mensual "`, `"Mensual Ordinaria"` → tras M1 quedan `MENSUAL`, `MENSUAL`, `NULL`; ninguna fila fuera del catálogo (criterio RF-003, A.3-8).
4. Validación de plaza: código inválido → 422 bilingüe; válido → persiste.
5. Bandeja: paginación + `StatusCounts` + drill cuadra con el desglose por tipo (A.3 drill); sin campo `amount` (test de contrato, A.3-11).
6. Exports: dataset + bandeja (413 sobre límite; job async con resource nuevo; auditoría `ReportExported`) (A.3-10).
7. Gates 403 en sección/bandeja/export para usuario sin permisos (A.3-10).
8. **No-regresión**: contratos existentes de `overview`/`hires`/`span-of-control`/`metadata` intactos (suite existente en verde).
9. Metadata: secciones futuras `active:false`; `acceptsMonth` correcto por sección.

**Transversales:** paridad de localización (mensajes nuevos), governance (Reporting sin `[AuthorizationPolicySet]`), migración sin drift, openapi regenerado.

---

## 7. Orden de implementación (PRs sugeridos)

- **PR-1 — Catálogo de planilla + limpieza + índice (M1):** entidad + config + seed `-9520…-9525` + `GeneralCatalogKeyMap` (`payroll-types`) + categoría de validación + validador de plaza (422) + migración (HasData + limpieza SQL + índice journal) + tests de validación/limpieza + openapi temprano — §3.1/§4.
- **PR-2 — Capa dimensional y filtros extendidos:** `EmployeeDimensionRow` (+payrollType/costCenter/categoría-motivo de retiro) + `DashboardDimensionFilter` (+month/payrollTypeCode/costCenterId) + `overview.byPayrollType` (aditivo) + metadata extendida (sections/filters/rotationFormula/catálogos) + tests — §3.2/§3.5.
- **PR-3 — Sección acciones documentales:** `GetPersonnelActionFactsAsync` + `PersonnelActionsDashboardRules` + endpoint `dashboard/personnel-actions` + **golden A.3 en verde (gate de la sección)** + EXPLAIN del índice — §3.3/§3.4/§3.5.
- **PR-4 — Sección movimientos:** bajas/rotación/altas-neto/cobertura/liquidaciones (reglas + endpoint `dashboard/movements`) + fixtures retiro/reversión/liquidación + test sin-montos — §3.4/§3.5.
- **PR-5 — Bandeja + exports + cierre:** `personnel-actions/query` + exports (dataset + bandeja + resource async `COMPANY_PERSONNEL_ACTIONS`) + suite E2E + `openapi.yaml` final (verificación del contrato publicado — aclaración №13) + **guía FE** `guia-integracion-frontend-tablero-acciones-personal.md` (contratos, semántica flujo/snapshot, fuentes activas, **vista de impresión + mapa de gráficos A.5**, nota de aproximación dimensional) — §3.5/§3.6.

Cada PR con suite completa en verde (unit + integración + governance + localización) antes del siguiente.

---

## 8. Riesgos y consideraciones técnicas

- **Volumen del journal**: la agregación es on-demand; el índice nuevo debe validarse con EXPLAIN (PR-3). Si un tenant creciera a millones de asientos, materializar (F2) — no anticipar.
- **Limpieza destructiva**: es irreversible por diseño (P-02). El checklist §9 exige registrar el conteo previo (`SELECT payroll_type_code, COUNT(*) … GROUP BY 1`) en la bitácora del despliegue como constancia de lo eliminado.
- **Enum de estado de `ExitInterviewSubmission`**: verificar el valor "completada" real al implementar la cobertura (aclaración №10) — no asumir.
- **Contrato de `hires` intocable**: la vista de movimientos calcula sus altas internamente; resistir la tentación de "extender" `hires` con bajas (rompería consumidores).
- **Aproximación dimensional (D-07)**: riesgo de lectura, no técnico — la guía FE y las leyendas lo declaran; el drill a la bandeja permite verificar.
- **openapi publicado**: baseline posiblemente desactualizado (no declara la familia dashboard) — resolver en PR-5 sin bloquear los PRs previos.
- **Buckets**: mantener `UNASSIGNED` ("Sin asignar"/"Sin dato") coherente con el tablero existente para que el FE reutilice el render.

---

## 9. Checklist de implementación / despliegue

- [ ] IDs de seed `-9520…-9525` verificados libres contra `GlobalCatalogSeedData` (PR-1)
- [ ] M1 aplicada: catálogo sembrado + **limpieza de `payroll_type_code` ejecutada** (registrar el conteo previo por valor en la bitácora del despliegue) + índice del journal creado
- [ ] Validación estricta de `payrollTypeCode` activa (422 bilingüe) — sin rutas de compatibilidad
- [ ] Suites unit + integración + governance + localización en verde (incluye no-regresión del tablero existente y test sin-montos)
- [ ] `openapi.yaml` regenerado sin drift + verificación del estado del contrato publicado (aclaración №13)
- [ ] Guía FE publicada con: contratos de las 2 secciones + bandeja/exports, semántica flujo/snapshot del filtro mes, fuentes activas por sección, **vista de impresión + exportación PDF del navegador (P-01)** y **mapa de gráficos A.5**, nota de aproximación dimensional y de "Sin dato" inicial en tipo de planilla
- [ ] Comunicación a la empresa: las plazas quedan sin clasificar en tipo de planilla tras la limpieza (estado esperado); se clasifican por edición natural
- [ ] Sin storage nuevo, sin configuración nueva, sin infraestructura PDF (P-01)
