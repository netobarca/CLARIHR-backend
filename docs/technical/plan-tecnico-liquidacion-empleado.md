# Plan Técnico de Implementación — Liquidación de Personal (Nueva liquidación · Escenario de liquidación)

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-liquidacion-empleado.md`](../business/analisis-liquidacion-empleado.md) (v1.1, decisiones **D-01…D-20 + P-01…P-03 ratificadas 2026-07-04**) |
| **Módulos** | `PersonnelFiles` (Settlements — net-new) · `PersonnelFiles/Retirements` (gancho de reversión) · `Compensation` (solo lectura) · `GeneralCatalogs` (país-scoped) · Provisioning (RBAC) · Reporting/Export + Documentos PDF · Localization · Auditoría |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-07-04 |
| **País de referencia** | El Salvador (SV, `CountryCatalogItemId = -7068L`) |
| **Endurecimientos de la ratificación** | **Liquidación POR PLAZA** (D-10) · boleta **PDF en Fase 1** + abstracciones **reutilizables** (D-19) · salario mínimo **en la ficha** (RF-011) · **exceso gravable controlado por el sistema** (RN-10) · conceptos no cumplidos → **valor 0** (RN-008.4) · solicitante **solo RRHH** (D-06) · pagos patronales ISSS/AFP/**INCAF** + **provisión por centro de costo** (D-13) · Renta **tabla 2026** |

---

## 0. Aclaraciones pre-desarrollo (recomendación del desarrollador senior; P-01…P-03 ya ratificadas)

1. **Antigüedad por plaza (P-01 ratificada):** el motor recibe `PlazaStartDate` (el `StartDate` de la asignación liquidada) y calcula años/fracción y tramos de aguinaldo **desde esa fecha**; `HireDate` viaja solo como dato informativo del encabezado. En la plaza principal ambos normalmente coinciden.
2. **Snapshot de insumos al crear + regeneración explícita:** salario base, tasas/topes ISSS-AFP, tramos de Renta, bonos/comisiones y cuotas externas se leen **una sola vez al crear** y quedan en el registro (determinismo y auditabilidad). Cambios posteriores de configuración NO recalculan solos; existe la acción explícita `POST …/lines/regenerate` que re-lee la configuración y reconstruye las líneas no-override.
3. **Redondeo:** `decimal` en toda la cadena; `Math.Round(x, 2, MidpointRounding.AwayFromZero)` **por línea**; totales = suma de líneas redondeadas (S-06 del análisis). Un único helper en el módulo de reglas — prohibido redondear en handlers.
4. **Advertencias ≠ errores:** las no-bloqueantes (renta sin tramos, neto negativo pendiente de confirmación, plaza sin centro de costo, valor-0 legal) viajan en el response como `warnings[]` (código + mensaje bilingüe), nunca como ProblemDetails.
5. **Emitir con neto negativo:** requiere `confirmNegativeNet=true` en el request; sin él → 422 `SETTLEMENT_NET_NEGATIVE_CONFIRMATION_REQUIRED`.
6. **Plaza principal para conceptos sin plaza (P-03 ratificada):** entre las asignaciones cerradas por el retiro, la que tenga `IsPrimary=true`; si ninguna lo es (edge), la de `StartDate` más antiguo. Mismo criterio en escenario sobre las activas.
7. **Escenario se elimina con `DELETE`** (soft: `IsActive=false`) usando la convención `parentConcurrencyToken`; la liquidación real **nunca** se borra — se anula (RF-005).
8. **`SettlementKind` es inmutable** tras crear (no existe "convertir" en Fase 1 — §17.14 lo dejó para F2 como operación de copia, no de mutación).
9. **Fechas:** `FechaSolicitud ≤ hoy` compara fecha-UTC vía `IDateTimeProvider.UtcNow.Date` (convención del módulo de retiro, aclaración №2 de aquel plan).
10. **La tabla de Renta 2026 no se siembra en producción** (es config por tenant, editable vía `PUT api/v1/income-tax-brackets`): va en el **checklist de despliegue**; el `DevSeedService` sí se actualiza a los valores 2026 para desarrollo.

---

## 1. Objetivo y enfoque

Construir el módulo de **liquidación de personal**: cálculo por plaza de la liquidación de un empleado retirado (anclada a la solicitud de retiro `EJECUTADA` y a una asignación cerrada por ella) y **escenario** de simulación sobre plazas activas, con un **motor determinista de 5 secciones** (ingresos, descuentos, pagos patronales, reserva/provisión, resumen), ciclo `BORRADOR → EMITIDA → ANULADA`, bandeja empresa, exports (xlsx/csv/json + **boleta PDF**) y gancho de coherencia con la reversión de retiro.

**Insight central del análisis de código.** No existe ni una línea de liquidación en `src/`, pero **todas las piezas de datos y todas las plantillas ya existen**. El módulo se arma combinando:

1. **Entidad + ciclo + snapshots** — `PersonnelFileRetirementRequest` (`Domain/PersonnelFiles/PersonnelFileEmployee.cs:2401-2732`): la plantilla más nueva y más cercana (estados canónicos + catálogo, snapshots de nombre, índice único filtrado, acciones `PATCH` con `[FromIfMatch]`).
2. **Motor como módulo de reglas puro** — patrón `RetirementRequest.Rules.cs` / `CertificateRequest.Rules.cs:59`: estáticos sin side-effects, testeables sin infraestructura. Aquí vive TODO el cálculo (la pieza de mayor riesgo del módulo es de exactitud, no de plomería).
3. **Bandeja + export tabular** — `CertificateRequestsReportingController.cs:30,66` + `RetirementRequestsReportingController.cs:31,102` + `ReportExportDeliveryService.cs:49-110` (resourceKey, 413, auditoría, rate-limits).
4. **Boleta PDF** — pipeline de documentos existente: `DocumentModel` (AST agnóstico) + `IDocumentModelRenderer` → `QuestPdfDocumentRenderer.cs:14` (License.Community en `DependencyInjection.cs:205`, motor conmutable QuestPDF/Gotenberg vía `Reporting:Pdf:Engine`). Precedentes: constancias (`CertificateQuestPdfRenderer` + `CertificatePrintDataProvider`) y perfiles de puesto (`JobProfilePdfExportHandler`).
5. **Catálogo tipado con tabla propia** — receta de `RetirementCatalogItems.cs` (deriva `PersonnelReferenceCatalogItemBase`, seed por migración) y de `CompensationConceptTypeCatalogItem.cs:13` (columnas tipadas de negocio).
6. **Insumos del cálculo (solo lectura)** — `SALARIO_BASE`/`BONO`/`COMISION` y deducciones `Externo` por plaza (`PersonnelFileCompensation.cs:12`, `AssignedPositionPublicId :65`, `DeductionClass :72`, `CounterpartyName :91`); tasas/topes ISSS `-9727` (3.00/7.50/1,000.00) y AFP `-9728` (7.25/8.75/7,045.06) (`GlobalCatalogSeedData.cs:933-934`); tramos `IncomeTaxWithholdingBracket.cs:11` (per-tenant, `PayPeriodCode=MENSUAL`); `HireDate`/perfil (`PersonnelFileEmployee.cs:41`); plazas cerradas por el retiro (`RetirementRequestClosedRecord`, `PersonnelFileEmployee.cs:2703-2732`, `EntityKind=ASSIGNMENT` + `PreviousEndDate`).

**Las tres piezas sin plantilla directa** (foco de riesgo, §8):
- **El motor de cálculo en sí** — primer motor de cálculo del sistema. Mitigación: 100% puro + casos dorados del contador como tests bloqueantes.
- **Export individual con secciones** (encabezado + detalle + resumen): la infraestructura actual exporta listados planos por reflexión (`ReportExportFileWriter.GetExportProperties<TRow>:155`). Se resuelve con filas seccionadas (xlsx) + `DocumentModel` (PDF), ambas como piezas **reutilizables** (D-19).
- **Campo nuevo en la ficha** (`MinimumMonthlyWage`): toca el contrato del `PUT …/employment-information` (aditivo, no breaking — a diferencia del retiro, aquí se AÑADE un campo).

**Decisiones ratificadas que gobiernan el diseño**: net-new sin tocar ledgers (D-01) · una entidad, dos modos (D-02) · ancla = retiro `EJECUTADA` + plaza de `ClosedRecords` (D-03/D-10) · catálogos de motivo reutilizados (D-04) · escenario sobre plaza activa, sin efectos (D-05) · solicitante solo RRHH (D-06) · catálogo tipado de conceptos con matriz de afectación + regla de exención (D-07) · motor sugiere por `SeparationType`, valor-0 legal, recálculo server-side (D-08) · mínimo en ficha + topes `min(salario, N×mínimo)` calculados (D-09/RF-011) · antigüedad por plaza desde `StartDate` (D-11/P-01) · ISSS/AFP efectivos + Renta 2026 con override + descuento externo última cuota (D-12) · pagos patronales ISSS/AFP/INCAF + provisión = ingresos+patronales al centro de costo (D-13/P-02) · overrides con nota, calculado visible (D-14) · `BORRADOR→EMITIDA→ANULADA` + acción `LIQUIDACION` (D-15) · unicidad (retiro × plaza) (D-16) · reversión anula borradores / bloquea con emitida (D-17) · sinergia rehire en F2 (D-18) · exports xlsx/csv/json + **PDF F1**, reutilizables (D-19) · permisos dedicados View/Manage + anti-auto-gestión (D-20).

---

## 2. Línea base verificada en el código (qué se reutiliza / qué se toca)

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Plantilla de entidad/ciclo | `PersonnelFileRetirementRequest` + `RetirementRequestStatuses` + `RetirementRequestClosedRecord` (`PersonnelFileEmployee.cs:2377-2732`); config con **índice único filtrado** `uq…tenant_file_open` | Espejo de entidad, guards, snapshots y del índice filtrado de unicidad (aquí: por retiro × plaza, solo `Kind=LIQUIDACION`, `status != ANULADA`, `is_active`). |
| 2 | Ancla del retirado | Solicitud `EJECUTADA` con `RetirementDate`, `RetirementCategoryCode/ReasonCode` + snapshots; `ClosedRecords` con `EntityKind` `ASSIGNMENT`/`CONTRACT` + `EntityPublicId` + `PreviousEndDate` | La creación real valida estado `EJECUTADA` y que la plaza elegida ∈ `ClosedRecords(ASSIGNMENT)`. |
| 3 | Plazas y salario | `PersonnelFileEmploymentAssignment` (`PersonnelFileEmployee.cs:118-299`): `PublicId`, `StartDate`, `IsPrimary`, `CostCenterPublicId`, `PositionSlotPublicId`; salario negociado = `PersonnelFileCompensationConcept` con `IsBaseSalary`/`SALARIO_BASE` por `AssignedPositionPublicId` (regla `CompensationConcepts.Rules.cs:18,73`) | `SettlementCalculationDataProvider` resuelve por plaza: salario base, `StartDate` (P-01), centro de costo (provisión), `IsPrimary` (P-03). |
| 4 | Tasas y topes | Tipo-catálogo `CompensationConceptTypeCatalogItem` (`DefaultEmployeeRate/DefaultEmployerRate/ContributionCap/MinContributionBase`); instancias del empleado con `EmployerRate`/`ContributionCap` override (`PersonnelFileCompensation.cs:83-85`); seeds ISSS `-9727` / AFP `-9728` (`GlobalCatalogSeedData.cs:933-934`) | Resolución: instancia del empleado en la plaza → fallback defaults del tipo. INCAF **no existe** como tipo: sus parámetros (1%, base/tope ISSS — P-02) viven en el catálogo de conceptos de liquidación (§3.1). |
| 5 | Renta | `IncomeTaxWithholdingBracket.cs:11` (per-tenant, por `PayPeriodCode`, vigencias); solo **DevSeed 2024** (`DevSeedService.cs:262`); admin `IncomeTaxBracketsController.cs:24,42` (PUT reemplaza el set) | El motor consume los tramos vigentes `MENSUAL`; sin tramos → línea 0 + warning (G-09). DevSeed se actualiza a tabla 2026; producción por checklist. |
| 6 | Ficha (perfil de empleo) | `PersonnelFileEmployeeProfile` (`PersonnelFileEmployee.cs:5-116`): `HireDate :41`, `Update` upsert vía `EmployeeProfiles.cs:77-90` + request `PersonnelFileRequests.cs:115-125`; **perfil retirado bloqueado** para el PUT (puerta única del retiro) | +1 columna `minimum_monthly_wage numeric(18,2) NULL` + parámetro en `Create/Update` + request/response + validador (> 0) + `Map`. Aditivo (no breaking). El bloqueo del PUT en retirados justifica el **override** del mínimo en la liquidación (RN-001.7). |
| 7 | Bandeja + export | `RetirementRequestsReportingController.cs` (`:31` query, `:69` interview-tray, `:102` export — gates por handler, sin `[AuthorizationPolicySet]`); `ReportExportDeliveryService.cs:49-110` (413, auditoría `ReportExported`, filename `{prefix}.{format}` `:84`); rate-limits `Search`/`Export` | Clonar para `settlements` (query + contadores por estado/kind + export). |
| 8 | PDF | `IDocumentModelRenderer`/`DocumentModel` + `QuestPdfDocumentRenderer.cs:14`; registro `DependencyInjection.cs:201-207`; precedente de "documento por registro": `CertificatePrintDataProvider` + `CertificateIssuanceService` | La boleta se construye como `DocumentModel` (reutilizable por diseño, motor conmutable) — NO se escribe QuestPDF directo en el feature. |
| 9 | Export xlsx individual | `ReportExportFileWriter.WriteXlsxAsync:78` (filas homogéneas por reflexión, `inlineStr :170`) | Filas seccionadas `SettlementExportRow` (columna `Seccion` + filas de encabezado/resumen) por el writer existente — sin librería nueva; el builder de filas es la pieza reutilizable (§3.9). |
| 10 | Receta de catálogo TPH | Subclase `GeneralCatalogItem` + const de categoría + wire-key en `GeneralCatalogKeyMap` + switch `CatalogCodeIsActiveAsync` + config índice + seed (receta №14 del plan de retiro; guardrail `GeneralCatalogKeyMapGuardrailsTests`) | Replicar 1× para `settlement-statuses` (3 códigos). |
| 11 | Receta de catálogo tipado con tabla propia | `RetirementCatalogItems.cs:20,58` (deriva `PersonnelReferenceCatalogItemBase`; unique `(country, normalized_code)`; seed por migración `20260625033455…:96-130`; lectura vía `reference-catalogs/…`) | `SettlementConceptCatalogItem` con columnas de negocio (clase, afectación, exención, `IsSystemCalculated`) — mismo esqueleto. |
| 12 | Permisos (receta 8 archivos) | Codes `PersonnelFileCommon.cs:205-226` (retiro); seed tuples `ProvisioningConstants.cs:87-90`; policies `PersonnelFilePolicies.cs:143-165` + `Program.cs:555-575`; gates default-fail-closed `IPersonnelFileAuthorizationService` → impl `PersonnelFileAuthorizationService.cs:27`; governance `AuthorizationPolicyConventionGovernanceTests` | 2 permisos nuevos (`ViewSettlements`/`ManageSettlements`), ambos con fallback `Admin`/`ManageAdministration` (a diferencia de Authorize/Revert de retiro, aquí no se excluye Admin — D-20). |
| 13 | Gancho de reversión | `RevertRetirementRequest.cs:103-107` (bloqueos en cadena antes de restaurar; transacción tipo rehire) | Insertar 2 pasos: guard `EMITIDA` (422 nuevo) + anulación bulk de borradores dentro de la misma transacción (D-17). El análisis de retiro ya declaró este punto (su D-14). |
| 14 | Acción de personal | Factory `PersonnelFilePersonnelAction.Create` (`PersonnelFileEmployee.cs:573-584`); tipos `BAJA=-9482`/`REVERSION_BAJA=-9483`; estado `APLICADA=-9495` | Journal `LIQUIDACION` al emitir; nuevo ActionType **`LIQUIDACION=-9484`** (SV). |
| 15 | IDs de seed libres | Retiro ocupó `-9810…-9815` (estados) y `-9482/-9483` (acciones); catálogos tipados de retiro `-9200…-9242` | **`settlement-statuses` = -9820…-9822** · **conceptos = -9830…-9846** (17, tabla propia) · **ActionType `LIQUIDACION` = -9484** · país SV `-7068L`. Verificar contra `GlobalCatalogSeedData` al abrir PR-1. |
| 16 | Wire / If-Match / auditoría | `PublicContractNaming` (Guid `XxxId`→`xxxPublicId`); `[FromIfMatch]`; auditoría doble-`SaveChanges` (`EconomicAidRequests.Handlers.cs:123-135`); DELETE con `parentConcurrencyToken` | Convenciones obligatorias en todos los handlers nuevos; controllers de liquidación **sin** `[ResourceActions]` (mismo corte que retiro). |
| 17 | Localización | `BackendMessages.resx`/`.es.resx`/`.es-SV.resx` + paridad `BackendMessageLocalizationTests` | ~20 códigos nuevos EN+ES **en el mismo PR que los introduce**; `validation.message.*` por cada `WithMessage`. |
| 18 | Multi-tenant | `TenantEntity` + query filter global (`ApplicationDbContext.cs:514-517`) + gates por `companyId` | Las 3 entidades nuevas son `TenantEntity`; el catálogo de conceptos es país-scoped (no tenant). |

---

## 3. Arquitectura de la solución

### 3.1 Catálogos + tipos de acción (D-07/D-15/RF-015)

**(a) `SettlementConceptCatalogItem`** — tabla propia `settlement_concept_catalog_items` (receta №11), deriva `PersonnelReferenceCatalogItemBase`. Columnas de negocio:

```csharp
public SettlementConceptClass ConceptClass { get; }   // Ingreso | Descuento | PagoPatronal (string en BD)
public bool AffectsIsss { get; }                       // matriz de afectación (solo relevante en ingresos)
public bool AffectsAfp { get; }
public bool AffectsRenta { get; }
public SettlementExemptionRule ExemptionRule { get; }  // Ninguna | HastaLimitePorMinimo | HastaMontoLegal (string)
public decimal? ExemptionMultiplier { get; }           // p. ej. 2.00 para aguinaldo (P-02)
public bool IsSystemCalculated { get; }                // motor vs manual
public decimal? DefaultRatePercent { get; }            // pagos patronales: ISSS_PATRONAL 7.50, AFP_PATRONAL 8.75, INCAF 1.00
```

Seed SV (**-9830…-9846**, migración M1) — los 17 conceptos ratificados: ingresos `SALARIO`, `VACACION_PROPORCIONAL`, `AGUINALDO_PROPORCIONAL` (exención `HastaLimitePorMinimo`, ×2), `INDEMNIZACION` (`HastaMontoLegal`), `RENUNCIA_VOLUNTARIA` (`HastaMontoLegal`), `BONO_PENDIENTE`, `COMISION_PENDIENTE`, `HORAS_EXTRAS_PENDIENTES` (manual), `OTRO_INGRESO` (manual); descuentos `ISSS`, `AFP`, `RENTA`, `DESCUENTO_EXTERNO`, `OTRO_DESCUENTO` (manual); pagos patronales `ISSS_PATRONAL` (7.50), `AFP_PATRONAL` (8.75), `INCAF` (1.00 — P-02, usa base/tope de ISSS). Lectura: `GET /api/v1/reference-catalogs/settlement-concepts?class=` (familia `reference-catalogs`, patrón retirement-categories con filtro).

**(b) `SettlementStatusCatalogItem`** — TPH `GeneralCatalogItem` (receta №10), key `settlement-statuses`, seed SV **-9820…-9822**: `BORRADOR`, `EMITIDA`, `ANULADA`. Los códigos canónicos viven en `SettlementStatuses` (dominio) — patrón híbrido D-15.

**(c) ActionType** — seed `LIQUIDACION = -9484` (SV) en `ACTION_TYPE_CATALOG`; se journalea con estado `APLICADA` (existente `-9495`) al emitir.

### 3.2 Permisos, políticas y gates (D-20)

- Codes: `PersonnelFiles.ViewSettlements` / `PersonnelFiles.ManageSettlements` (`PersonnelFilePermissionCodes`, patrón `:205-226`).
- Provisioning: 2 tuples en `ProvisioningConstants` (Module=PersonnelFiles, Screen=PersonnelFiles, Actions ViewSettlements/ManageSettlements).
- Policies MVC: 2 en `PersonnelFilePolicies` + wiring en `Program.cs` (View → autenticado + claim; Manage → `RequireAssertion` con `ManageSettlements | PersonnelFiles.Admin | ManageAdministration`).
- Gates: `EnsureCanViewSettlementsAsync` / `EnsureCanManageSettlementsAsync` en `IPersonnelFileAuthorizationService` (default interface **fail-closed**, patrón `:124`) + impl en `PersonnelFileAuthorizationService`.
- **Anti-auto-gestión** (D-20): en crear/emitir/anular, si `personnelFile.LinkedUserPublicId == usuario actual` → 403 `SETTLEMENT_SELF_ACTION_FORBIDDEN` (patrón anti-self de economic aid / retiro).
- **Solicitante solo RRHH** (D-06): validación en handler — el expediente solicitante debe (a) ser el del usuario que registra, o (b) pertenecer al área funcional RRHH cuando `CompanyPreference.HrFunctionalAreaCode` esté configurada (`CompanyPreference.cs:28`); si no hay preferencia configurada, solo (a). 422 `SETTLEMENT_REQUESTER_NOT_HR`.
- Governance: añadir las 2 policies al HashSet de `AuthorizationPolicyConventionGovernanceTests`.

### 3.3 Dominio — nuevo archivo `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileSettlement.cs`

#### 3.3.1 Constantes y enums

```csharp
public static class SettlementStatuses   // patrón RetirementRequestStatuses:2377
{ public const string Borrador = "BORRADOR"; Emitida = "EMITIDA"; Anulada = "ANULADA"; }
public enum SettlementKind { Liquidacion = 1, Escenario = 2 }            // string en BD
public enum SettlementConceptClass { Ingreso = 1, Descuento = 2, PagoPatronal = 3 }
public enum SettlementExemptionRule { Ninguna = 0, HastaLimitePorMinimo = 1, HastaMontoLegal = 2 }
```

#### 3.3.2 `PersonnelFileSettlement : TenantEntity` — encabezado

Campos según §12 del análisis: identidad (`PublicId`, `PersonnelFileId` + nav), modo (`Kind`), ancla (`RetirementRequestId?` FK long + `RetirementRequestPublicId?`), **plaza** (`AssignedPositionPublicId`, `PositionNameSnapshot`, `CostCenterPublicId?`, `CostCenterNameSnapshot?`), motivo heredado/hipotético (`RetirementDate`, `RetirementCategoryCode/NameSnapshot`, `RetirementReasonCode/NameSnapshot`), solicitante (`RequesterFilePublicId`, `RequesterNameSnapshot`, `RequestDate`, `RequestedByUserId`), `Notes`, ciclo (`StatusCode` — solo en `Liquidacion`), parámetros snapshot (los 11 de RF-011, incl. `MinimumMonthlyWage`, `AguinaldoExemptionMultiplier`), derivados snapshot (`MonthlyBaseSalary`, `PlazaStartDate`, `SeniorityYears/Days`, `CappedSalaryIndemnity/Resignation`), totales (`TotalIncomes/TotalDeductions/NetPay/TotalEmployerCharges/ProvisionTotal`, `CurrencyCode`), emisión/anulación (`IssuedByUserId/IssuedAtUtc`, `AnnulledByUserId/AnnulledAtUtc/AnnulmentReason`), `IsActive`, `ConcurrencyToken`, colección `Lines` (backing field).

Métodos con guards (patrón retiro): `Create(...)` (real vs escenario — factories separados que fijan `Kind`), `UpdateHeader(...)` + `UpdateParameters(...)` (solo `BORRADOR` o escenario), `ReplaceCalculation(lines, totals)` (única vía de escritura de líneas/totales — la invoca el handler tras correr el motor), `MarkIssued(userId, atUtc, confirmNegativeNet)` (guards: estado, ≥1 ingreso incluido, neto), `Annul(userId, atUtc, reason)` (motivo obligatorio desde `EMITIDA`), `SetActive(bool)` (solo escenario).

#### 3.3.3 `PersonnelFileSettlementLine : TenantEntity` — detalle

`PublicId`, `SettlementId` (FK cascade), `ConceptClass`, `ConceptCode` + `ConceptNameSnapshot`, `Description?` (manuales), `CalculationBase?`, `UnitsOrDays?`, `CalculatedAmount`, `ExemptAmount` + `TaxableExcessAmount` (RN-009.4 — trazabilidad del exceso), `OverrideAmount?` + `OverrideReason?` (juntos o ninguno), `FinalAmount` (persistido = `Override ?? Calculated`), `IsIncluded`, `IsZeroByLaw` + `ZeroReasonCode?` (RN-008.4 — valor-0 con motivo legible), `CalculationDetail?` (≤500, "15 × 1.30 × 143/365 × $12.17"), `CounterpartyName?` (descuento externo), `SortOrder`, `ConcurrencyToken`.

#### 3.3.4 Cambios en entidades existentes

- `PersonnelFileEmployeeProfile`: `decimal? MinimumMonthlyWage` + parámetro en `Create/Update` (`PersonnelFileEmployee.cs:61-96`) — **aditivo**; el PUT sigue bloqueado para retirados (puerta única intacta).
- Sin cambios en asignaciones, contratos, retiro (solo el handler de reversión — §3.8) ni ledgers (D-01).

### 3.4 Módulo de reglas puro — `Features/PersonnelFiles/Settlements/SettlementCalculation.Rules.cs`

**Todo el cálculo es estático y puro** (el corazón del módulo — patrón `RetirementRequest.Rules.cs`). API propuesta:

```csharp
internal static class SettlementCalculationRules
{
    // Entrada: TODO lo externo resuelto de antemano (sin I/O aquí)
    public sealed record CalculationInput(
        SettlementKind Kind, DateTime RetirementDate, DateTime PlazaStartDate,     // P-01
        decimal MonthlyBaseSalary, decimal MinimumMonthlyWage,
        SettlementParameters Parameters,                    // multiplicadores, días, divisores, exenciones
        RetirementSeparationType SeparationType,            // sugerencia de líneas (D-08)
        IReadOnlyList<ConceptConfig> Concepts,              // catálogo: clase, afectación, exención, tasas patronales
        IReadOnlyList<SuggestedItem> PlazaItems,            // bonos/comisiones (montos) y cuotas externas (contraparte)
        ContributionScheme Isss, ContributionScheme Afp,    // tasa empleado/patronal + tope (instancia→default)
        IReadOnlyList<TaxBracket> RentaBrackets,            // vigentes MENSUAL (tabla 2026); vacío ⇒ warning
        LineAdjustments Adjustments);                       // días editados, overrides, exclusiones, manuales

    public static CalculationResult Calculate(CalculationInput input);
    // CalculationResult: List<LineResult> (por sección) + Totals + Warnings + derivados (antigüedad, topes)
}
```

Orden interno (determinista): **[1]** antigüedad desde `PlazaStartDate` (años + fracción días/365) → **[2]** topes `min(salario, N×mínimo)` (D-09) → **[3]** líneas de ingreso sugeridas por `SeparationType` + `PlazaItems` + ajustes/manuales; requisito legal no cumplido ⇒ `IsZeroByLaw` (RN-008.4) → **[4]** bases afectas por matriz → **[5]** exenciones y **exceso gravable** por línea (`ExemptionRule`) → **[6]** ISSS/AFP empleado (`tasa × min(base, tope)`) → **[7]** Renta por tramos sobre base gravable + excesos (cuota fija + % sobre excedente); sin tramos ⇒ 0 + warning → **[8]** descuento externo (cuotas sugeridas) + manuales → **[9]** pagos patronales (ISSS/AFP/INCAF sobre bases afectas con tope) → **[10]** provisión = ingresos + patronales (P-02) → **[11]** totales (redondeo por línea, aclaración №3).

Las **fórmulas** son las del análisis RF-008/RF-009/RF-010 con los parámetros del snapshot. `SettlementErrors` (co-ubicado) define los códigos §5.

### 3.5 Aplicación — feature `Features/PersonnelFiles/Settlements/`

| Archivo | Contenido |
|---|---|
| `Settlements.cs` | DTOs (`SettlementResponse` con secciones + `warnings[]`), commands/queries CRUD, validadores |
| `Settlements.Handlers.cs` | Crear real (valida retiro `EJECUTADA` + plaza ∈ `ClosedRecords` + unicidad + solicitante-RRHH + anti-self + mínimo), crear escenario (empleado activo + plaza activa), leer, editar encabezado/parámetros, editar líneas (ajustes/override/incluir), añadir/quitar línea manual, `regenerate`, DELETE escenario. **Cada mutación**: cargar → `Calculate` → `ReplaceCalculation` → auditar |
| `Settlements.Actions.Handlers.cs` | `PATCH /issuance` (guards + journal `LIQUIDACION` + auditoría) y `PATCH /annulment` |
| `SettlementCalculation.Rules.cs` | §3.4 (motor puro) + `SettlementErrors` |
| `SettlementCalculationDataProvider.cs` | Único punto de I/O del cálculo: resuelve salario base de la plaza, `StartDate`, centro de costo, bonos/comisiones/externas (de la plaza + las employee-level si la plaza es la principal — P-03), esquemas ISSS/AFP (instancia→default), tramos Renta, mínimo de la ficha. Se invoca al **crear** y en **regenerate** (aclaración №2) |
| `SettlementsBandeja.cs` (+`.Handlers.cs`) | Query empresa con filtros (kind, estado, categoría/motivo, empleado, rangos de fechas, texto) + contadores + export rows |
| `SettlementDocuments.cs` (+`.Handlers.cs`) | Boleta PDF (`DocumentModel`) + export individual xlsx/csv/json (filas seccionadas) — §3.9 |

### 3.6 API — controllers y contratos

**`SettlementsController`** (`[AuthorizationPolicySet(ViewSettlements, ManageSettlements)]`, sin `[ResourceActions]`):

| Verbo y ruta (`api/v1/personnel-files/{publicId:guid}/settlements…`) | Acción |
|---|---|
| `GET …` / `GET …/{settlementPublicId:guid}` | Listar por expediente (ambos kinds) / detalle con secciones + warnings |
| `POST …` | Crear (request discriminado por `kind`: real → `retirementRequestPublicId`+`assignedPositionPublicId`; escenario → plaza activa + fecha estimada + categoría/motivo) |
| `PUT …/{id}` | Encabezado + parámetros (recalcula) |
| `PUT …/{id}/lines/{linePublicId:guid}` | Ajustar línea (días/base/override+nota/incluir) |
| `POST …/{id}/lines` / `DELETE …/{id}/lines/{lineId}` | Línea manual / quitar línea |
| `POST …/{id}/lines/regenerate` | Re-leer configuración y reconstruir líneas no-override (aclaración №2) |
| `PATCH …/{id}/issuance` / `PATCH …/{id}/annulment` | Emitir / anular |
| `DELETE …/{id}` | Solo escenario (soft, `parentConcurrencyToken`) |
| `GET …/{id}/document?format=pdf\|xlsx\|csv\|json` | Boleta PDF / export individual (§3.9) |

**`SettlementsReportingController`** (sin policy-set; gates por handler — convención bandejas): `POST api/v1/companies/{companyId:guid}/settlements/query` (+ contadores) y `GET …/settlements/export?format=` (resourceKey `"SETTLEMENTS"`, prefix `settlements`, rate-limits `Search`/`Export`).

Contratos en `Contracts/PersonnelFiles/SettlementContracts.cs` (`[Required]` en parámetro de record posicional — nunca `[property:…]`, gotcha conocido). Toda mutación con `[FromIfMatch]`.

### 3.7 Emisión (RF-004, D-15)

`PATCH …/issuance` — handler transaccional simple (sin orquestación multi-entidad): guards de dominio (`MarkIssued`) + anti-auto-gestión + journal `PersonnelFilePersonnelAction.Create(LIQUIDACION, APLICADA, sistema)` + auditoría (doble `SaveChanges` en transacción, patrón economic aid). No toca perfil, plazas, login ni planilla (FA-1): la emisión es un acto documental.

### 3.8 Gancho de reversión de retiro (RF-017, D-17) — toca `RevertRetirementRequest`

En el handler existente (`RevertRetirementRequest.cs`, tras los bloqueos actuales `:103-107` y **antes** de restaurar):

1. Cargar liquidaciones reales activas de la solicitud (`RetirementRequestId`, `Kind=Liquidacion`, `IsActive`).
2. Si **alguna** `EMITIDA` → rollback + 422 `RETIREMENT_REVERSAL_BLOCKED_BY_SETTLEMENT` (mensaje: anularla primero).
3. Anular (`Annul` con motivo del sistema "Reversión de retiro") **todas** las `BORRADOR` dentro de la misma transacción + auditoría por cada una.

Los escenarios no se tocan. Cero cambios de contrato en los endpoints de retiro (solo un código de error nuevo, documentado en su guía FE).

### 3.9 Exportación individual reutilizable (RF-007, D-19) — la pieza transversal nueva

**(a) Boleta PDF.** `SettlementDocumentModelBuilder` (Application) arma un `DocumentModel` (AST existente): encabezado (empresa/empleado/plaza/motivo/fechas/solicitante), bloque de parámetros aplicados, una tabla por sección (ingresos / descuentos / pagos patronales) con columnas `Concepto · Base · Días/Factor · Calculado · Ajustado · Final`, bloque de reserva (provisión + centro de costo) y resumen. Marca `SIMULACIÓN — SIN EFECTOS` cuando `Kind=Escenario` (RN-007.1). Render vía `IDocumentModelRenderer` (motor conmutable) — **cero QuestPDF directo en el feature**: cualquier módulo futuro con "documento por registro" reutiliza el mismo camino (mandato D-19).

**(b) Export tabular individual (xlsx/csv/json).** `SettlementExportRowComposer` genera filas seccionadas (`SettlementExportRow`: `Seccion, Concepto, Detalle, Base, DiasFactor, Calculado, Ajustado, Final, Notas`) con filas de encabezado (sección `ENCABEZADO`/`PARAMETROS`) y de cierre (`RESUMEN`), servidas por el `ReportExportFileWriter` existente. El composer vive en `Features/Reports` como utilidad genérica de "registro seccionado" si el tech lead prefiere extraerlo ya; como mínimo, queda documentado como patrón a extraer en el segundo consumidor.

**(c) Bandeja.** Export plano estándar (una fila por liquidación-plaza con totales y estado), patrón constancias/retiros.

### 3.10 Localización

~20 códigos nuevos EN+ES (§5) + `validation.message.*` de cada validador. Los textos de warnings (`SETTLEMENT_WARNING_*`) también van al resx (se muestran en UI/exports). Paridad garantizada por `BackendMessageLocalizationTests` en cada PR.

### 3.11 Auditoría

Patrón `PersonnelFileEmployeeAudits` (before/after, doble `SaveChanges`): crear, editar encabezado/parámetros, cada mutación de línea con override (motivo incluido en el payload de auditoría), regenerate, emitir, anular, export (vía `ReportExported` existente), anulación-por-reversión (actor = usuario que revierte, motivo sistema).

---

## 4. Migraciones y seeds

| # | Migración (PR) | Contenido |
|---|---|---|
| M1 (PR-1) | `AddSettlementCatalogsAndProfileMinimumWage` | `CreateTable settlement_concept_catalog_items` + seed 17 conceptos (**-9830…-9846**, SV, con clase/afectación/exención/tasas) + seed TPH `settlement-statuses` (**-9820…-9822**, SV) + `InsertData` ActionType `LIQUIDACION=-9484` (SV) + `AddColumn personnel_file_employee_profiles.minimum_monthly_wage numeric(18,2) NULL` |
| M2 (PR-3) | `AddPersonnelFileSettlements` | Tablas `personnel_file_settlements` + `personnel_file_settlement_lines`; índices: `uq…public_id` (ambas), lectura `(tenant, personnel_file_id, kind)` y `(tenant, kind, status_code, request_date)`, y **filtered-unique** `uq_personnel_file_settlements__tenant_retirement_position_active` sobre `(tenant_id, retirement_request_id, assigned_position_public_id)` where `kind='LIQUIDACION' and status_code <> 'ANULADA' and is_active` (D-16) |

DevSeed: actualizar `SeedIncomeTaxBrackets` a la **tabla 2026** (aclaración №10) + sembrar `MinimumMonthlyWage` en los perfiles demo. Generación/drift: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add … -p src/CLARIHR.Infrastructure -s src/CLARIHR.Api` · `has-pending-model-changes` vacío · guardrail `MigrationSeedingIntegrationTests`.

---

## 5. Mapa de errores (resumen)

| Código | HTTP | Dónde |
|---|---|---|
| `SETTLEMENT_RETIREMENT_NOT_EXECUTED` | 422 | Crear real sin retiro `EJECUTADA` vigente (E-01) |
| `SETTLEMENT_RETIREMENT_REVERTED` | 422 | Crear real sobre retiro revertido (E-02) |
| `SETTLEMENT_POSITION_NOT_IN_RETIREMENT` | 422 | Plaza ∉ `ClosedRecords(ASSIGNMENT)` del retiro (E-16c) |
| `SETTLEMENT_ALREADY_EXISTS_FOR_POSITION` | 422 | Duplicado (retiro × plaza) — D-16 (E-03) |
| `SETTLEMENT_BASE_SALARY_MISSING` | 422 | Plaza sin `SALARIO_BASE` (E-05) |
| `SETTLEMENT_MINIMUM_WAGE_MISSING` | 422 | Sin mínimo en ficha ni override (RN-001.7, E-16b) |
| `SETTLEMENT_REQUESTER_NOT_HR` | 422 | Solicitante fuera de RRHH (D-06, E-16e) |
| `SETTLEMENT_SELF_ACTION_FORBIDDEN` | **403** | Sujeto crea/emite/anula su propia liquidación (D-20, E-14) |
| `SETTLEMENT_STATE_RULE_VIOLATION` | 422 | Editar `EMITIDA`, anular `ANULADA`, emitir no-borrador, DELETE de real… (E-11) |
| `SETTLEMENT_SCENARIO_EMPLOYEE_RETIRED` | 422 | Escenario sobre retirado (E-04) |
| `SETTLEMENT_SCENARIO_POSITION_INVALID` | 422 | Escenario sobre plaza no activa (E-16c) |
| `SETTLEMENT_DATE_INCOHERENT` | 422 | `FechaSolicitud` futura; fecha estimada < `PlazaStartDate`/`HireDate` (E-16) |
| `SETTLEMENT_PARAMETERS_INVALID` | 400/422 | Mínimo ≤ 0, multiplicadores ≤ 0, días negativos (E-09) |
| `SETTLEMENT_OVERRIDE_NOTE_REQUIRED` | 422 | Override sin nota (D-14, E-10) |
| `SETTLEMENT_CONCEPT_INVALID` | 422 | Código inactivo/clase incoherente al añadir línea |
| `SETTLEMENT_ISSUE_REQUIRES_INCOME` | 422 | Emitir sin ingresos incluidos (RN-004.1, E-12) |
| `SETTLEMENT_NET_NEGATIVE_CONFIRMATION_REQUIRED` | 422 | Emitir con neto < 0 sin flag (aclaración №5, E-13) |
| `SETTLEMENT_ANNUL_REASON_REQUIRED` | 422 | Anular `EMITIDA` sin motivo (RF-005) |
| `RETIREMENT_REVERSAL_BLOCKED_BY_SETTLEMENT` | 422 | Reversión con `EMITIDA` vigente (D-17, E-15) |
| **Warnings (no bloqueantes, `warnings[]`)**: `SETTLEMENT_WARNING_RENTA_BRACKETS_MISSING` · `…_ZERO_BY_LAW` (renuncia < 2 años) · `…_NO_COST_CENTER` · `…_BOTH_COMPENSATIONS` (indemnización + renuncia) · `…_NET_NEGATIVE` | — | Response de cálculo (aclaración №4) |

Reusados: `PERSONNEL_FILE_EXPORT_FORMAT_INVALID` (400), `REPORT_EXPORT_TOO_LARGE` (413), 400/409 de `If-Match`, 403 de gates.

---

## 6. Plan de pruebas

**Unitarias (`tests/CLARIHR.Application.UnitTests/`):**
- **`SettlementCalculationRulesTests` — la suite crítica:**
  - **Casos dorados del contador** (bloqueantes, §18.1 del análisis): ≥5 liquidaciones reales — renuncia (con prestación 2×-tope), despido (indemnización 4×-tope), mutuo acuerdo, fin de contrato, jubilación — **incluyendo multi-plaza (P-01), exceso gravable (aguinaldo > 2× mínimo) y renuncia < 2 años (valor 0)**. Se codifican como `[Theory]` con los números firmados; hasta recibirlos, la estructura queda con casos sintéticos marcados `// TODO golden`.
  - Fórmulas por concepto (bordes: aniversario exacto, antigüedad 2.999 vs 3.0 años para tramos 15/19/21, retiro el 11-dic y el 12-dic, salario == tope, salario > tope).
  - Exenciones/exceso: aguinaldo bajo/sobre el límite; override al alza de indemnización → excedente gravable.
  - ISSS/AFP: base > tope (ISSS $30.00 exacto con base $1,200), instancia-override vs default.
  - Renta: cada tramo 2026 + sin tramos (warning) + base 0.
  - Redondeo: propiedad `Total == Σ líneas redondeadas` con montos de céntimos conflictivos.
  - Valor-0 legal y exclusiones/manuales/overrides (el override sobrevive al recálculo; quitar override restaura).
- Dominio: guards de `Create/UpdateHeader/ReplaceCalculation/MarkIssued/Annul/SetActive` (cada transición inválida lanza); línea con override sin nota lanza.
- Perfil: `MinimumMonthlyWage` en `Create/Update` (> 0 o null).
- Validadores; governance (2 policies); paridad de localización (~20 códigos + warnings).

**Integración (`tests/CLARIHR.Api.IntegrationTests/` — `ApiIntegrationTests.Settlements.cs`, espejo de `ApiIntegrationTests.Retirement.cs`):**
- **Round-trip real**: retirar (módulo de retiro) → crear liquidación de la plaza → verificar secciones/totales/warnings → ajustar línea (override+nota) → regenerate → emitir (journal `LIQUIDACION` presente) → export xlsx + **PDF (200, content-type `application/pdf`)** → anular con motivo → crear nueva (candado liberado).
- **Round-trip escenario**: crear sobre plaza activa → editar fecha estimada (recalcula) → export marcado SIMULACIÓN → DELETE soft → no aparece en bandeja.
- **Multi-plaza**: retiro con 2 plazas → 2 liquidaciones; duplicado por plaza → 422; conceptos employee-level solo en la principal (P-03).
- **Reversión**: con borradores → reversión OK y liquidaciones `ANULADA`; con emitida → 422 `RETIREMENT_REVERSAL_BLOCKED_BY_SETTLEMENT` y nada cambia.
- 403 (self-action; sin permisos), 422 del mapa §5 (uno por código), 409 `If-Match`, 413 export, bandeja (filtros + contadores + kinds).
- **PUT employment-information** acepta y persiste `minimumMonthlyWage` (y sigue rechazando perfiles retirados).
- Guardrails existentes verdes: `MigrationSeedingIntegrationTests`, `GeneralCatalogKeyMapGuardrailsTests`, `OpenApiContractGuardrailsIntegrationTests`, `AuthorizationPolicyConventionGovernanceTests`, `AllowedActionsCoverageIntegrationTests`.

---

## 7. Orden de implementación (PRs sugeridos)

**Ola 1 — motor + escenario + documentos (validación con el contador sin tocar el flujo oficial):**

1. **PR-1 — Catálogos + ActionType + campo de ficha (M1)** (§3.1, §3.3.4): entidad de conceptos + TPH de estados + seeds + `minimum_monthly_wage` + PUT/GET de employment-information + endpoints de lectura de catálogos + resx + guardrails de biyección/seeding.
2. **PR-2 — Permisos + gates** (§3.2): 2 codes + provisioning + policies + governance + 2 `EnsureCan…` + anti-self compartido.
3. **PR-3 — Dominio + EF + M2** (§3.3): entidades + guards + configs + índices (incl. filtered-unique) + batería unitaria de dominio.
4. **PR-4 — Motor de cálculo** (§3.4) + `SettlementCalculationDataProvider` (§3.5): reglas puras completas + suite `SettlementCalculationRulesTests` (casos sintéticos + esqueleto golden).
5. **PR-5 — Escenario end-to-end** (§3.5/§3.6): crear/leer/editar/regenerate/DELETE + líneas + warnings + validadores + resx + integración de escenario.
6. **PR-6 — Documentos y exports individuales** (§3.9): `SettlementDocumentModelBuilder` (PDF) + `SettlementExportRowComposer` (xlsx/csv/json) + endpoint `document` + marca SIMULACIÓN + tests (content-type/estructura). **Hito Ola 1: el contador valida escenarios reales exportados; los golden cases se firman y se codifican aquí.**

**Ola 2 — liquidación real + bandeja + reversión:**

7. **PR-7 — Liquidación real** (§3.5/§3.6/§3.7): crear desde retiro×plaza (todas las validaciones §5) + ciclo emitir/anular + journal `LIQUIDACION` + anti-self + solicitante-RRHH + integración real.
8. **PR-8 — Bandeja + export empresa** (§3.5/§3.6): reporting controller + query/contadores + export + rate-limits.
9. **PR-9 — Gancho de reversión** (§3.8): guard + anulación bulk en la transacción de reversión + código nuevo + tests de ambos sentidos + nota en la guía FE de retiro.
10. **PR-10 — E2E + guía frontend**: `ApiIntegrationTests.Settlements.cs` completo (round-trips + multi-plaza + reversión), verificación integral (suites verdes, drift vacío, seeds en BD real), `openapi.yaml` regenerado, `docs/technical/guia-integracion-frontend-liquidacion.md`.

> **Cada PR lleva sus claves resx y sus tests.** Los **casos dorados firmados** son el gate entre Ola 1 y Ola 2 (recomendación №1 del análisis): si el contador corrige fórmulas/valores, se ajusta el motor ANTES de construir el flujo oficial.

---

## 8. Riesgos y consideraciones técnicas

- **R-T1 — Exactitud del motor (el riesgo real del módulo).** Mitigación: motor 100% puro con casos dorados bloqueantes; hito de validación con el contador al cierre de Ola 1; `CalculationDetail` por línea hace auditable cada número; parámetros en snapshot (no hay "cambió la config y ahora no cuadra").
- **R-T2 — Topes ISSS/AFP en fracciones de mes.** El tope es mensual y la liquidación suele cubrir fracción + conceptos anuales. Decisión Fase 1 (RN-009.1): tope aplicado una vez sobre la base afecta total, override para casos multi-periodo — documentado en la guía FE y validado en golden cases.
- **R-T3 — Renta en liquidación ≠ retención mensual exacta.** Ya asumido por el negocio (D-12: sugerida + override). El response marca la línea como sugerida; el excedente gravable se documenta en `CalculationDetail`.
- **R-T4 — INCAF sin tipo de compensación existente.** Sus parámetros viven en el catálogo de conceptos de liquidación (`DefaultRatePercent`, base/tope de ISSS — P-02). Si el negocio luego crea INCAF como concepto de compensación, la resolución instancia→default se extiende sin romper nada.
- **R-T5 — Snapshot vs datos vivos.** Insumos leídos solo al crear/regenerate (aclaración №2) — evita que un cambio de salario posterior altere un borrador en revisión. El botón `regenerate` es la vía consciente; test dedicado (cambiar config → borrador intacto → regenerate → refleja).
- **R-T6 — Índice filtrado de unicidad con 3 columnas** (tenant, retiro, plaza): verificar en M2 el nombre ≤ 63 chars (convención Postgres) — patrón del índice de solicitud abierta de retiro.
- **R-T7 — `PUT employment-information` compartido.** Añadir `minimumMonthlyWage` toca un contrato usado por el frontend (aditivo). `openapi.yaml` se regenera en PR-1 (no en PR-10) para que FE lo integre temprano; nullable ⇒ sin breaking.
- **R-T8 — Estado híbrido canónico+catálogo** (igual R-T6 de retiro): el `code` sembrado es estructural; sin CRUD admin del catálogo de estados/conceptos en Fase 1 (los conceptos se gestionan por seed/migración).
- **R-T9 — Bandeja con dos kinds.** El listado mezcla reales y escenarios: `kind` SIEMPRE presente en filas/exports y filtro default sugerido `kind=LIQUIDACION` en la guía FE (R-10 del análisis).
- **R-T10 — Reloj y ventanas.** Toda comparación de fechas vía `IDateTimeProvider` + parámetro `asOf` en reglas puras (tests deterministas — patrón retiro).
- **R-T11 — `dotnet ef`** requiere `DOTNET_ROLL_FORWARD=Major` en este entorno.

---

## 9. Checklist de implementación

- [ ] **Catálogos:** `SettlementConceptCatalogItem` (tabla propia, 17 seeds `-9830…-9846` con clase/afectación/exención/tasas) + `SettlementStatusCatalogItem` TPH (`-9820…-9822`) + key map + switch + configs + ActionType `LIQUIDACION=-9484`.
- [ ] **Ficha:** columna `minimum_monthly_wage` + `Create/Update` + PUT/GET/validador + openapi regenerado (PR-1).
- [ ] **Permisos:** 2 codes + provisioning + 2 policies (con fallback Admin/ManageAdministration) + governance + 2 gates fail-closed.
- [ ] **Dominio:** `PersonnelFileSettlement` + `PersonnelFileSettlementLine` + `SettlementStatuses`/enums + guards (`MarkIssued` con neto/ingresos, `Annul` con motivo, `ReplaceCalculation` única vía de escritura) + `IsZeroByLaw`/`TaxableExcessAmount` en línea.
- [ ] **Motor:** `SettlementCalculationRules.Calculate` puro (11 pasos §3.4) + redondeo único + warnings + `SettlementErrors`.
- [ ] **Data provider:** salario/plaza/centro-de-costo/bonos/comisiones/externas (P-03 plaza principal) + ISSS/AFP instancia→default + tramos 2026 + mínimo de ficha.
- [ ] **Aplicación:** CRUD real/escenario + líneas + regenerate + emisión (journal) + anulación + validaciones §5 + anti-self + solicitante-RRHH.
- [ ] **Reversión:** guard `EMITIDA` + anulación bulk de borradores en la transacción (+ auditoría) + código nuevo.
- [ ] **API:** `SettlementsController` + `SettlementsReportingController` + contratos (`[FromIfMatch]`, DELETE `parentConcurrencyToken`, sin `[ResourceActions]`).
- [ ] **Documentos:** `SettlementDocumentModelBuilder` (PDF vía `IDocumentModelRenderer`) + `SettlementExportRowComposer` (xlsx/csv/json seccionado) + marca SIMULACIÓN + endpoint `document`.
- [ ] **Bandeja/export:** query + contadores (estado×kind) + export (`resourceKey "SETTLEMENTS"`, 413) + rate-limits.
- [ ] **Migraciones:** M1 + M2 (filtered-unique retiro×plaza) + DevSeed (Renta 2026, mínimos demo); `has-pending-model-changes` vacío.
- [ ] **Localización:** ~20 códigos + 5 warnings EN+ES por PR; `validation.message.*` por validador.
- [ ] **Auditoría:** todas las mutaciones + overrides con motivo + anulación-por-reversión.
- [ ] **Tests:** golden cases firmados (gate Ola 1→2) + fórmulas/bordes + dominio + integración round-trips (real, escenario, multi-plaza, reversión) + 403/422/409/413 + guardrails verdes.
- [ ] **Checklist de despliegue:** tabla Renta 2026 por tenant (`PUT api/v1/income-tax-brackets`) · salario mínimo cargado en fichas · `SALARIO_BASE` por plaza de próximos a liquidar.
- [ ] **Verificación final:** `dotnet build` (0 err) · `dotnet test` (unit + integración) · drift vacío · seeds verificados en BD real · guía FE publicada.

---

> **Trazabilidad decisión → componente.** D-01 → §3.3 (entidades nuevas; ledgers intactos) · D-02 → `SettlementKind` + factories (§3.3.2) · D-03/D-10 → validaciones de creación (§3.5) + ancla `RetirementRequestId`+`AssignedPositionPublicId` · D-04 → reuso de `ValidateRetirementCodesAsync` en escenario · D-05 → factory escenario + `SETTLEMENT_SCENARIO_*` · D-06 → validación solicitante-RRHH (§3.2) · D-07 → §3.1(a) catálogo tipado · D-08 → §3.4 pasos [3]-[5] + `IsZeroByLaw` · D-09/RF-011 → parámetros snapshot + topes paso [2] + `MinimumMonthlyWage` en ficha (§3.3.4) · P-01 → `PlazaStartDate` paso [1] · D-12 → pasos [6]-[8] + tabla 2026 (aclaración №10) · D-13/P-02 → pasos [9]-[10] + `DefaultRatePercent` INCAF · D-14 → `OverrideAmount/OverrideReason` + `SETTLEMENT_OVERRIDE_NOTE_REQUIRED` · D-15 → `SettlementStatuses` + `MarkIssued`/journal (§3.7) · D-16 → filtered-unique (M2) · D-17 → §3.8 · D-18 → sin componente (F2; la señal será "existe `EMITIDA` del periodo") · D-19 → §3.9 (DocumentModel + composer reutilizables) · D-20 → §3.2 · P-03 → data provider (plaza principal). Este plan implementa la Fase 1 completa (RF-001…RF-018) en 2 olas y 10 PRs, con **una única pieza de riesgo real** (la exactitud del motor), acotada por pureza + casos dorados bloqueantes entre olas.
