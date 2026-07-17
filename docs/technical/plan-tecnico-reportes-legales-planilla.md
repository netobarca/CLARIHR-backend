# Plan técnico — Reportes legales de planilla: F-14, Planilla Única y Planilla Patronal

| | |
|---|---|
| **Análisis** | [`analisis-reportes-legales-planilla.md`](../business/analisis-reportes-legales-planilla.md) — **RATIFICADO 2026-07-16/17** (P-01…P-13, sin preguntas de negocio pendientes) |
| **Fecha** | 2026-07-17 |
| **Rama** | `feature/reportes-legales-planilla` (crear desde `master`) |
| **Molde** | `CompanyPreference` (perfil único por empresa — RF-006) · `PayrollCalculationDataProvider.BuildPopulationAsync` (exclusión silenciosa ya existente — se le agrega motivo visible, RF-007) · `IDocumentModelRenderer` (seam de renderer intercambiable ya probado con QuestPDF/Gotenberg — molde del exportador nuevo, RF-004) |
| **Migraciones** | **M1** `AddCompanyLegalProfiles` (PR-1) · **M2** `AddAfpAccountNumberToPersonnelFile` + `SeedNupIsssIdentificationTypeForElSalvador` (PR-2) |
| **Seeds** | +1 fila en `identification_type_catalog_items` (`NUP_ISSS`, país `SV`) |
| **Estado** | Diseño — **RF-006/RF-007/RF-003 listos para construir sin bloqueos de negocio.** RF-001/RF-002/RF-004 tienen arquitectura fija en este plan, pero su **layout final celda-por-celda queda bloqueado por P-02** (el negocio todavía no entregó el archivo de plantilla oficial de F-14/Planilla Única) |
| **PRs** | 8 (ver §6) — el orden **no** es el mismo que el de valor de negocio (§18 del análisis recomendaba empezar por Planilla Patronal); este plan prioriza los 2 gates primero porque su fecha de activación real depende de una campaña de captura de datos que necesita tiempo de sobra (§0.11, §7) |
| **Depende de** | REQ-012 (motor — `PayrollRun`/`PayrollRunLine`/`PayrollCalculationRules`, **YA CERTIFICADO, no se toca su contrato público**) · REQ-013 (exportador `ReportExportFileWriter` — **tampoco se toca**; el renderer nuevo vive aparte) |
| **F2 (no construir en este plan)** | Presentación electrónica ante DGII/ISSS · PDF de estos 3 reportes · catálogo tipado de actividad económica (texto libre por ahora, D-06 de este plan) · modo de transición interno del gate (rechazado en P-13) |

---

## §0 Aclaraciones verificadas contra el código (léelas antes de cada PR)

1. **`PayrollRun.Create()` no recibe ninguna referencia a `Company`.** El constructor privado (`src/CLARIHR.Domain/Payroll/PayrollRun.cs:41-72`) solo valida longitudes de string; la factory estática (`:140-166`) no toma un perfil de empresa como parámetro. El `TenantId` se asigna **después**, vía `run.SetTenantId(command.CompanyId)`. Verificar el perfil legal patronal en el Gate A exige una **consulta nueva** — no hay dato de empresa ya cargado en el handler en ese punto (no es gratis, pero es una consulta de una sola fila por `TenantId`).
2. El handler real de generación es `GeneratePayrollRunCommandHandler.Handle` (`src/CLARIHR.Application/Features/Payroll/PayrollRuns.Handlers.cs:325-479`); la llamada a `PayrollRun.Create(...)` ocurre en las líneas **377-389**. El Gate A se inserta **antes** de esa llamada, después de la autorización (línea 342) y de `PayrollRunGenerationSupport.ResolveAsync` (línea 348).
3. El motor puro (`PayrollCalculationRules.Calculate`, `src/CLARIHR.Application/Features/Payroll/PayrollCalculation.Rules.cs:194-220`) **no tiene concepto de exclusión** — cada `PayrollEmployeeInput` que entra sale con su set completo de líneas (`foreach (var employee in input.Employees) CalculateEmployee(...)`, sin ningún filtro). La exclusión real ya vive **una capa arriba**, en `PayrollCalculationDataProvider.BuildPopulationAsync` (`src/CLARIHR.Infrastructure/Payroll/PayrollCalculationDataProvider.cs:100-149`), que hoy **ya excluye silenciosamente** por asignación inactiva, tipo de nómina distinto, expediente incompleto, retirado o ya liquidado en el periodo — el Gate B es un `.Where` más en ese mismo lugar; **el patrón ya existe**, solo falta el motivo visible (§2.2).
4. El sistema de warnings (`PayrollRunWarningResult(Code, PersonnelFilePublicId, Context)`) **ya tiene forma para adjuntar un aviso a un empleado específico** — hoy solo se usa para empleados que SÍ participan en la corrida (avisos de línea, ej. `NoBaseSalary`). Se reutiliza este mismo molde para los excluidos por el Gate B: un warning con el mismo `PersonnelFilePublicId` pero **sin ninguna línea asociada** — a verificar en PR-2 el punto exacto donde `PayrollRunWarningResult` se serializa a `PayrollRun.WarningsJson`, pero el molde `Code+PersonnelFilePublicId+Context` ya existe y no se inventa nada nuevo.
5. `PersonnelFile.AfpCode` (`PersonnelFile.cs:98`) identifica **la AFP**, no una cuenta. "Cuenta AFP" es un campo hermano **nuevo** (`AfpAccountNumber`) — no una reutilización de `AfpCode`.
6. **`PersonnelReferenceCatalog.cs` (la lista estática DUI/NIT/PASSPORT/RESIDENT_CARD) es código muerto — cero consumidores verificados.** La validación real de `IdentificationType` es contra la tabla `identification_type_catalog_items` vía `PersonnelReferenceCatalogValidation.ValidateIdentificationTypeCodeAsync`. Esto significa que agregar `NUP_ISSS` como tipo de identificación es **una fila de catálogo, cero cambios de handler** — `Identifications.Handlers.cs` ya acepta cualquier código válido de catálogo genéricamente.
7. **El exportador de la casa (`ReportExportFileWriter.WriteXlsxAsync`, `src/CLARIHR.Application/Features/Reports/ReportExportFileWriter.cs:78-153`) no es un motor de celdas** — las celdas no llevan referencia `r="A1"`, hay un solo estilo (`cellXf`) para todo el archivo, no existe `<mergeCells>`, y es estrictamente una fila por objeto en orden secuencial. **No se puede extender in-place para un layout de formulario oficial** — es un serializador lineal, no una grilla direccionable. Se confirmó que **ningún paquete de Excel existe hoy en la solución** (`Directory.Packages.props` + los 9 `.csproj`) y que **nada en el repo lee un `.xlsx` existente** (el único `ZipArchiveMode.Read` es un test que relee lo que el propio test acaba de escribir).
8. El patrón ya probado para "un renderer más" en este repo es `IDocumentModelRenderer.RenderAsync(DocumentModel, Stream, ct)` (`src/CLARIHR.Application/Abstractions/Reports/Documents/DocumentModel.cs:66-69`), con 2 implementaciones reales (`QuestPdfDocumentRenderer`, `GotenbergDocumentRenderer`) seleccionadas por config (`DocumentPdfRenderingRegistration.cs:29-77`). Es solo para PDF hoy y está desconectado de `ReportExportFileWriter` — es el **molde a copiar** para el renderer de plantilla nuevo, no un lugar para insertar código.
9. El molde de **"un registro único por empresa"** ya existe repetido (`CompanyPreference`, y por el mismo patrón de índice único sobre `TenantId`: `LocationHierarchyConfig`, `CompanyCertificateSettings`): `TenantEntity` con índice único sobre `TenantId`, **sin navegación 1:1 desde `Company`**, cargado por un repositorio dedicado (`GetByTenantIdAsync`). `CompanyLegalProfile` (RF-006) es el 4.º de este molde — no se inventa nada nuevo.
10. **REQ-014 (transacciones no aplicadas) es estructuralmente ajeno** a la re-inclusión de un empleado excluido por el Gate B: REQ-014 mueve líneas dentro de una corrida ya generada, nunca agrega o quita un empleado entero. Un empleado excluido por el Gate B se resuelve solo **cuando la siguiente generación/regeneración vuelve a evaluar la población** (`BuildPopulationAsync` se re-ejecuta con el dato ya completo) — no hace falta construir ningún mecanismo de "reintento", es consecuencia natural de cómo ya funciona la población hoy.
11. **La ratificación (P-13) rechazó un modo de transición DENTRO del sistema** (nada de advertencia-antes-de-bloquear como interruptor de producto) — pero eso es distinto de **cuándo se despliega cada pieza de código**. Este plan separa deliberadamente, en el tiempo de despliegue, "capturar el dato" (PR-1/PR-2, sin gate activo) de "exigir el dato" (activación del gate, después de la campaña de captura) — sin contradecir P-13: el sistema en sí nunca tendrá un interruptor de modo advertencia, pero el equipo sí controla cuándo mergea/activa cada gate.

---

## §1 Modelo de datos

### 1.1 `CompanyLegalProfile` (RF-006, M1) — nuevo

Molde exacto de `CompanyPreference` (`src/CLARIHR.Domain/Preferences/CompanyPreference.cs`): `TenantEntity`, ctor privado, factory `Create`, un solo mutador `Update` que rota `ConcurrencyToken`. Un registro por empresa, sin navegación 1:1 desde `Company` — se carga con un repositorio dedicado.

```csharp
public sealed class CompanyLegalProfile : TenantEntity
{
    public string LegalName { get; private set; } = string.Empty;              // razón social
    public string EmployerNitNumber { get; private set; } = string.Empty;      // NIT patronal
    public string IsssEmployerRegistrationNumber { get; private set; } = string.Empty; // NRC/registro patronal ISSS
    public string FiscalAddress { get; private set; } = string.Empty;
    public string? EconomicActivityDescription { get; private set; }           // texto libre — D-06, sin catálogo en esta fase
    public Guid? LegalRepresentativePublicId { get; private set; }             // FK opcional a LegalRepresentative ya existente
    public byte[] ConcurrencyToken { get; private set; } = [];

    public static CompanyLegalProfile Create(Guid tenantId, string legalName, string employerNitNumber,
        string isssEmployerRegistrationNumber, string fiscalAddress, string? economicActivityDescription,
        Guid? legalRepresentativePublicId) => new(...);

    public void Update(string legalName, string employerNitNumber, string isssEmployerRegistrationNumber,
        string fiscalAddress, string? economicActivityDescription, Guid? legalRepresentativePublicId)
    {
        // valida no-vacío en los 4 campos obligatorios, rota ConcurrencyToken
    }
}
```

EF: `CompanyLegalProfileConfiguration` — tabla `company_legal_profiles`, `HasIndex(x => x.TenantId).IsUnique()` (mismo nombre de índice que el molde: `uq_company_legal_profiles__tenant_id`), `HasOne<Company>().WithMany().HasForeignKey(x => x.TenantId).HasPrincipalKey(c => c.PublicId).OnDelete(Restrict)` — copiado literal de `CompanyPreferenceConfiguration.cs:118-131`. Repositorio: `ICompanyLegalProfileRepository.GetByTenantIdAsync(Guid tenantId, CancellationToken ct)` — copiado literal de `CompanyPreferenceRepository.cs:12-13`.

**Validación de formato del NIT**: reutilizar el regex de NIT salvadoreño ya usado en la validación de identificaciones de empleado (14 dígitos con guiones) — **a confirmar el nombre exacto de la constante/regex al escribir el PR**, no se verificó su ubicación exacta en esta investigación.

**D-06 (decisión de este plan, no del negocio)**: `EconomicActivityDescription` es texto libre, no un catálogo. Crear un catálogo de actividad económica (CIIU) es trabajo aparte no pedido por el análisis — si el negocio lo requiere, es F2.

### 1.2 `PersonnelFile.AfpAccountNumber` (RF-007a, M2) — columna nueva

Campo hermano de `AfpCode`, mismo patrón de normalización (trim+upper si aplica formato alfanumérico; a confirmar con el formato real de cuenta AFP en la implementación):

```csharp
public string? AfpAccountNumber { get; private set; }   // NUEVO — columna afp_account_number varchar(80), nullable
```

Se agrega al mismo `Create`/`UpdatePersonalInfo` donde vive `AfpCode` hoy (`src/CLARIHR.Application/Features/PersonnelFiles/Shell/PersonnelFileCore.Handlers.cs:169-182`) — **mismo endpoint existente, no se crea uno nuevo.** Migración `AddAfpAccountNumberToPersonnelFile` (M2a), mismo molde que `AddAfpCatalogAffiliationAndPensionParams.cs:16-20` (que agregó `AfpCode`).

### 1.3 Tipo de identificación `NUP_ISSS` (RF-007b, M2) — solo dato, cero cambio de esquema

Una fila nueva en `identification_type_catalog_items`, país `SV`, código `NUP_ISSS`. Migración de datos pura `SeedNupIsssIdentificationTypeForElSalvador` (M2b), mismo molde que el seed original de `DUI`/`NIT` (`20260415040945_UnifySystemCatalogsByCountry.cs:1769`). **Cero cambios de handler**: la captura ya pasa por los endpoints genéricos de identificaciones del expediente (`PersonnelFile.AddIdentification`/`UpdateIdentification`), que validan el tipo contra el catálogo sin importar qué código sea.

---

## §2 Los 2 gates de generación de nómina (RF-006/RF-007 — el corazón de este plan)

### 2.1 Gate A — perfil legal patronal bloquea la generación de la corrida (P-03)

Se inserta en `GeneratePayrollRunCommandHandler.Handle` (`PayrollRuns.Handlers.cs`), inmediatamente después de la autorización (línea ~342) y antes de `PayrollRun.Create()` (línea ~377):

```csharp
var legalProfile = await companyLegalProfileRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
if (legalProfile is null)
{
    throw new DomainRuleViolationException("PAYROLL_RUN_MISSING_LEGAL_PROFILE",
        "La empresa no tiene perfil legal patronal configurado; no se puede generar planilla.");
}
```
(nombre exacto de la excepción/mecanismo de error a confirmar contra el patrón de errores ya usado en este mismo handler — mismo tipo de respuesta 422 que otras reglas de generación).

**Efecto**: `POST .../payroll-runs` (generar corrida) responde 422 con el código `PAYROLL_RUN_MISSING_LEGAL_PROFILE` si la empresa no tiene perfil — bloquea la corrida **completa**, consistente con P-03 (bloqueo a nivel empresa).

### 2.2 Gate B — datos previsionales del empleado excluyen su línea, no la corrida (P-11/P-12)

Se inserta como un `.Where` adicional en `PayrollCalculationDataProvider.BuildPopulationAsync` (`Infrastructure/Payroll/PayrollCalculationDataProvider.cs:100-149`), en el mismo lugar donde hoy ya se excluyen asignaciones inactivas/expedientes incompletos/retirados/ya liquidados:

```csharp
where /* ...predicados existentes... */
      && file.AfpAccountNumber != null
      && file.Identifications.Any(id => id.IdentificationType == "NUP_ISSS")
```

**A diferencia de las exclusiones silenciosas que ya existen hoy, esta debe ser visible (P-11/P-12 ratificadas: "bloquea, pero excluye solo la línea de ese empleado")**: los empleados que caen en este filtro se recolectan aparte (no en `filtered`, sino en una lista paralela de excluidos) y se emite un `PayrollRunWarningResult` por cada uno con un código nuevo (`PAYROLL_EMPLOYEE_EXCLUDED_PREVISIONAL_DATA_MISSING`) y su `PersonnelFilePublicId` — **reutilizando el molde de warning ya existente (§0.4), sin tabla nueva**. Ese warning viaja en `PayrollRun.WarningsJson` de la cabecera (el empleado no tiene ninguna línea, así que no puede llevarlo en `WarningCodesJson` de una línea que no existe).

**Efecto**: el empleado sin NUP ISSS/cuenta AFP **no aparece en la corrida** (ni siquiera con `LineClass=Descuento` en cero) — el resto de la empresa se genera con normalidad (P-12). La corrida trae un aviso de cabecera "N empleados excluidos por datos previsionales incompletos" con el detalle por `PersonnelFilePublicId`, que la bandeja/impresión de planilla (REQ-013) ya sabe mostrar (los warnings de cabecera ya son visibles hoy — solo se agrega un código nuevo al vocabulario, no una superficie nueva de UI).

### 2.3 Secuenciación de despliegue de los 2 gates (no es lo mismo que secuenciar el código)

Ambos gates se **construyen** en PR-1/PR-2 (§6), pero **activarlos en producción antes de que las empresas hayan tenido tiempo de capturar el dato interrumpe pagos reales** (riesgo aceptado explícitamente en P-13, pero sin ventana de tiempo definida). Este plan recomienda — como práctica de despliegue, no como parte del producto — separar el **merge/activación** del **build**: los endpoints de captura (perfil legal patronal, `AfpAccountNumber`, `NUP_ISSS`) pueden desplegarse y usarse desde el día 1 del PR-1/PR-2 sin que el gate esté todavía activo (un feature flag de infraestructura/config, no un modo de producto), y el gate se activa cuando la campaña de captura (Pendiente de despliegue en el backlog) esté lista. Ver R-T1 en §7.

---

## §3 Arquitectura por vertical (RF-001…RF-004)

### 3.1 RF-003 — Planilla Patronal (el más barato, sin bloqueos — MVP recomendado)

Espejo casi exacto del patrón ya construido en REQ-013 para `GetRunLineExportRowsAsync`/`ImpresionPlanillaExportRow` (`src/CLARIHR.Application/Features/Payroll/PayrollRunsReporting.cs:88-103`, repositorio `PayrollRunRepository.cs:267`) — **mismo mecanismo de exportación (`ReportExportFileWriter`), sin gap de plantilla oficial (P-05: es control interno).**

- Nuevo método en `IPayrollRunRepository`: `GetEmployerCostReportRowsAsync(Guid tenantId, Guid payrollRunId, CancellationToken ct)` — filtra líneas por `LineClass == PagoPatronal` (más `SALARIO` para mostrar la base), agrupa por empleado, sumando `FinalAmount`.
- Nueva fila `PlanillaPatronalExportRow(Empleado, CodigoEmpleado, CentroCosto, SalarioBase, IsssPatronal, AfpPatronal, OtrasCargasPatronales, CostoPatronalTotal)` — el total debe cuadrar contra `PayrollRun.TotalEmployerCost` de la cabecera (criterio de aceptación del análisis, RF-003).
- Endpoint propuesto: `GET api/v1/companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/employer-cost-report/export` — mismo controlador (`PayrollRunsReportingController`), mismo gate por handler que el resto (`EnsureCanViewComplianceReportsAsync`, §4).

### 3.2 RF-004 — Exportador de plantilla oficial (arquitectura fija, sin layout final)

**Decisión de diseño (DP-01, este plan): NO se toca `ReportExportFileWriter`.** Se agrega un renderer nuevo y angosto, mirror del seam ya probado `IDocumentModelRenderer` (§0.8), que este plan llama `IComplianceReportTemplateRenderer`:

```csharp
public interface IComplianceReportTemplateRenderer
{
    Task RenderAsync(string templateResourceKey, IReadOnlyDictionary<string, object?> headerValues,
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> rows, Stream destination, CancellationToken ct);
}
```

Implementación concreta usando **`DocumentFormat.OpenXml`** (paquete NUEVO, agregado solo al proyecto que implementa el renderer — NO a `CLARIHR.Application` ni a `ReportExportFileWriter`): abre una plantilla `.xlsx` en blanco pre-cargada (ver DP-02), localiza celdas por nombre/rango con nombre definido (`DefinedName` de OOXML) en vez de coordenadas hardcodeadas, y escribe encabezado + filas repitiendo el bloque de fila tantas veces como haga falta.

**DP-02 (decisión de este plan, a validar cuando lleguen los archivos reales)**: la plantilla oficial **no varía por empresa** — es un formulario del gobierno, igual para todas las empresas salvadoreñas. Se propone guardarla como **recurso embebido en el ensamblado** (o archivo de configuración desplegado junto a la app), no como upload por tenant vía `Storage:Purposes` (ese mecanismo es para documentos *del* tenant, no plantillas *del sistema*). Esto es una hipótesis de diseño razonable dado que la plantilla es la misma para todos, pero **debe confirmarse cuando el negocio entregue el archivo real** — si resulta que cada empresa necesita una variante (poco probable para un formulario de Hacienda/ISSS, pero posible para un membrete), este diseño cambia a un `FilePurpose` nuevo.

**Lo que este PR entrega sin los archivos reales**: la interfaz, el mecanismo de "abrir plantilla + escribir por rango con nombre", y una plantilla de PLACEHOLDER (una hoja simple con 2-3 celdas nombradas) para probar el mecanismo end-to-end con un test. **Lo que NO entrega**: el mapeo celda-por-celda real de F-14/Planilla Única — eso es RF-001/RF-002 (PR-6/PR-7), bloqueado hasta que el negocio entregue los archivos oficiales (P-02).

### 3.3 RF-001 — F-14 (bloqueado por P-02 para el layout final; la consulta sí se puede construir ya)

**Consulta de consolidación mensual** — nuevo método en `IPayrollRunRepository`: `GetMonthlyIncomeTaxWithholdingRowsAsync(Guid tenantId, int year, int month, CancellationToken ct)`. Join `PayrollRun` (filtrado `StatusCode == Cerrada`) → `PayrollPeriodDefinition` (filtrado `Year == year && Month == month`, sin importar `PayPeriodTypeCode` — consolida mensual/quincenal/semanal juntos, P-09) → `PayrollRunLine` (filtrado `ConceptCode == RENTA`), agrupado por `PersonnelFilePublicId`, sumando `FinalAmount` y `BaseAmount` a través de **todas** las corridas del mes (puede ser más de una si la nómina es quincenal/semanal).

Fila propuesta: `F14ExportRow(Empleado, CodigoEmpleado, Nit, SalarioGravableMes, RentaRetenidaMes, Advertencias)` — `Advertencias` incluye "sin NIT registrado" cuando aplica (RN-06, solo advierte, no bloquea — distinto del Gate B).

Endpoint propuesto: `GET api/v1/companies/{companyId:guid}/compliance-reports/income-tax-withholding/export?year=&month=`.

**Esta consulta y este endpoint SÍ se pueden construir en este plan.** Lo que queda bloqueado es únicamente el paso final: pasar estas filas por `IComplianceReportTemplateRenderer` con el mapeo celda-por-celda del F-14 real, que no existe todavía (P-02).

### 3.4 RF-002 — Planilla Única (mismo patrón que RF-001, más exigente en datos)

Mismo mecanismo de consolidación mensual que RF-001, pero uniendo `ConceptCode IN (ISSS, ISSS_PATRONAL, AFP, AFP_PATRONAL)` y agregando `PersonnelFile.AfpCode`/`AfpAccountNumber` y la identificación `NUP_ISSS` a la fila. Nuevo método `GetMonthlySocialSecurityContributionRowsAsync(Guid tenantId, int year, int month, CancellationToken ct)`.

Fila propuesta: `PlanillaUnicaExportRow(Empleado, CodigoEmpleado, NupIsss, SalarioCotizableMes, IsssEmpleado, IsssPatronal, CodigoAfp, NombreAfp, CuentaAfp, AfpEmpleado, AfpPatronal, Advertencias)`.

Endpoint propuesto: `GET api/v1/companies/{companyId:guid}/compliance-reports/social-security-contributions/export?year=&month=`.

**Nota importante**: dado que el Gate B (§2.2) ya excluye de la corrida a cualquier empleado sin NUP ISSS/cuenta AFP, en teoría esta consulta **nunca debería encontrar una fila con esos campos vacíos** una vez el gate esté activo — la columna `Advertencias` de este reporte solo tendría contenido real durante la ventana de transición de despliegue (§2.3), antes de que el gate esté activo. Después de esa ventana, es una columna que debería quedar siempre vacía por construcción.

---

## §4 Permisos, policies y gates

Nuevo permiso `ViewComplianceReports` (P-10 ratificado: dedicado, no reutilizar `ViewPayrollRuns`) — **8 puntos de contacto, calcados 1:1 del molde `ViewPayrollRuns`** (verificado en el código):

1. `ProvisioningConstants.cs` — entrada en `CompanyAdminPermissions[]`: `new("PersonnelFiles.ViewComplianceReports", "Ver reportes legales de planilla", ...)`.
2. `PersonnelFileCommon.cs` (`PersonnelFilePermissionCodes`) — constante `ViewComplianceReports = "PersonnelFiles.ViewComplianceReports"`.
3. `PersonnelFilePolicies.cs` — constante de policy ASP.NET (mismo string).
4. `IPersonnelFileAuthorizationService.cs` — default fail-closed `EnsureCanViewComplianceReportsAsync`.
5. `PersonnelFileAuthorizationService.cs` — override concreto (mismo patrón que `EnsureCanViewPayrollRunsAsync`: `EnsureHasAnyClaimAsync(companyId, [ViewComplianceReports, Admin, ManageAdministration], Read, ct)`).
6. `Program.cs` — `options.AddPolicy(...)`.
7. Controladores de los 3 reportes — `[AuthorizationPolicySet(PersonnelFilePolicies.ViewComplianceReports)]` o gate por handler (a decidir en PR-4 si comparten controlador con `PayrollRunsReportingController` o si se crea uno nuevo — dado que RF-001/002 son consultas trans-corrida, distinto de las consultas por-corrida existentes, un controlador separado `ComplianceReportsController` es más consistente con la forma de las rutas propuestas en §3).
8. `OwnerPermissionCatalog.cs` + `CompanyProvisioningService.cs`/`DevSeedService.cs` — que el permiso llegue a empresas **nuevas**.

**R-T2 (riesgo técnico, §7)**: el punto 8 solo cubre empresas que se aprovisionan **después** de este cambio. Las empresas ya existentes no reciben el permiso nuevo automáticamente — se necesita una migración de datos que lo asigne al rol admin de cada tenant ya provisionado, o un paso manual documentado en Pendientes de despliegue. Este mismo patrón ya se resolvió para permisos anteriores del proyecto (`ViewPayrollRuns` mismo) — **revisar cómo se resolvió esa vez antes de escribir el PR-3**, en vez de inventar un mecanismo nuevo.

---

## §5 Errores y contrato

| Código | Dónde | HTTP | Naturaleza |
|---|---|---|---|
| `PAYROLL_RUN_MISSING_LEGAL_PROFILE` | Generar corrida (Gate A) | 422 | Bloquea la corrida completa |
| `PAYROLL_EMPLOYEE_EXCLUDED_PREVISIONAL_DATA_MISSING` | Warning de cabecera de la corrida (Gate B) | n/a (no es error HTTP — viaja en `WarningsJson` de una corrida generada con 200/201) | Bloquea solo la línea de ese empleado |
| *(estándar del proyecto)* | CRUD del perfil legal patronal | 400 (If-Match faltante) / 409 (stale) | Mismo contrato ya vigente en el resto de maestros de empresa |
| *(sin código nuevo)* | Mes/ciclo sin corridas `CERRADA` para F-14/Planilla Única/Planilla Patronal | 200, filas vacías + aviso | Igual al criterio ya definido en el análisis (RF-005) |
| `403` | Falta `ViewComplianceReports` | 403 | Igual al patrón ya vigente de los demás reportes de planilla |

Cada PR que toque un endpoint nuevo o un DTO nuevo regenera `openapi.yaml` y corre el guardrail de drift ya vigente (`OpenApiContractGuardrails`) — sin excepciones, igual que el resto del proyecto.

---

## §6 Orden de PRs (checklist para el backlog)

| PR | Contenido | Migración | Gate / bloqueo |
|---|---|---|---|
| **PR-1** | `CompanyLegalProfile` (M1) + CRUD (`GET/PUT companies/{id}/legal-profile`) + Gate A en `GeneratePayrollRunCommandHandler` **desplegado con el gate DESACTIVADO** (solo captura, ver §2.3) | M1 `AddCompanyLegalProfiles` | Ninguno de negocio — código listo para construir ya |
| **PR-2** | `PersonnelFile.AfpAccountNumber` + tipo `NUP_ISSS` (M2) + Gate B en `BuildPopulationAsync` + warning de exclusión visible, **desplegado con el gate DESACTIVADO** | M2 `AddAfpAccountNumberToPersonnelFile` + `SeedNupIsssIdentificationTypeForElSalvador` | Ninguno de negocio — código listo para construir ya |
| **PR-3** | Permiso `ViewComplianceReports` (8 puntos, §4) + backfill del permiso para empresas ya provisionadas (R-T2) | ninguna (o una migración de datos si el backfill lo requiere) | Ninguno — revisar cómo se resolvió el mismo problema para `ViewPayrollRuns` |
| **PR-4** | Reporte Planilla Patronal (RF-003) completo — consulta + endpoint + exportador `ReportExportFileWriter` ya existente | ninguna | Ninguno — **candidato a primer entregable visible al negocio** |
| **PR-5** | Renderer de plantilla (RF-004, §3.2) — interfaz `IComplianceReportTemplateRenderer` + implementación `DocumentFormat.OpenXml` + prueba con plantilla placeholder | ninguna (agrega el paquete NuGet nuevo) | Ninguno de negocio — **el mapeo real de celdas no se construye aquí** |
| **PR-6** | Reporte F-14 (RF-001) — consulta de consolidación mensual + endpoint completos; mapeo celda-por-celda **bloqueado hasta recibir la plantilla oficial** | ninguna | **Bloqueado por P-02 para el layout final** — la consulta puede mergearse, el mapeo no |
| **PR-7** | Reporte Planilla Única (RF-002) — ídem PR-6 | ninguna | **Bloqueado por P-02 para el layout final** |
| **PR-8** | **Activación de los gates A/B** (una vez la campaña de captura esté lista, ver Pendientes de despliegue) + openapi final + guía FE | ninguna | **Bloqueado por la campaña de captura de datos, no por código** |

**Nota de secuenciación**: PR-1…PR-5 no tienen ningún bloqueo — pueden construirse y mergearse en cualquier orden razonable desde hoy. PR-6/PR-7 pueden construir su mitad de consulta/endpoint ya, pero su mitad de exportación espera el insumo del negocio. PR-8 es exclusivamente un interruptor de despliegue, no código nuevo de fondo.

---

## §7 Decisiones de este plan y riesgos técnicos

- **DP-01**: no tocar `ReportExportFileWriter` — nuevo renderer aparte (`IComplianceReportTemplateRenderer`), mismo espíritu que `IDocumentModelRenderer`. Ver §3.2/§0.8.
- **DP-02**: la plantilla oficial se trata como recurso del sistema (no por-tenant) — a confirmar cuando lleguen los archivos reales; si una empresa necesita variantes, este diseño cambia a un `FilePurpose` nuevo.
- **DP-03**: los 3 reportes nuevos extienden `IPayrollRunRepository` con métodos nuevos (no se crea un repositorio separado) — consistente con cómo ese repositorio ya creció orgánicamente en REQ-012/013 (`GetRunExportRowsAsync`, `GetRunLineExportRowsAsync`, `GetBankReconciliationRowsAsync`).
- **DP-04**: separar, en el tiempo de despliegue, "capturar el dato" de "exigir el dato" (§2.3) — no es un modo de producto (P-13 lo rechazó), es una práctica de release del equipo.
- **R-T1 (riesgo técnico — el más grande de este plan)**: si PR-8 activa el Gate B antes de que la campaña de captura esté suficientemente avanzada, empresas completas podrían quedarse sin poder pagar a una parte relevante de su plantilla de un ciclo a otro. Mitigación: PR-8 no se mergea/activa sin una señal explícita de que la campaña de captura (Pendiente de despliegue en el backlog) está lista — esto debe ser una decisión de negocio en el momento, no una fecha fija de hoy.
- **R-T2**: el permiso `ViewComplianceReports` no llega automáticamente a empresas ya provisionadas (§4, punto 8) — revisar el mecanismo ya usado para permisos anteriores antes de decidir cómo resolverlo aquí, en vez de improvisar uno nuevo.
- **R-T3**: el regex/formato exacto de validación del NIT patronal (§1.1) y de la cuenta AFP (§1.2) no se verificaron con precisión en esta investigación — confirmar contra el catálogo de revalidación de identificaciones (o la fuente que el contador indique) antes de escribir PR-1/PR-2, no inventar un formato.
- **R-T4**: el punto exacto donde `PayrollRunWarningResult` se serializa a `PayrollRun.WarningsJson` (§0.4/§2.2) se infiere por el molde existente pero no se verificó línea por línea — confirmar al escribir PR-2.

---

## §8 Checklist de despliegue

- [ ] M1/M2 aplicadas en el entorno de destino.
- [ ] Seed `NUP_ISSS` en `identification_type_catalog_items` ejecutado (país `SV`).
- [ ] Permiso `ViewComplianceReports` asignado a los roles admin de las empresas **ya provisionadas** (R-T2) — no solo a las nuevas.
- [ ] **Campaña de captura de perfil legal patronal + NUP ISSS/cuenta AFP completada o en curso, comunicada a cada cliente, ANTES de activar PR-8.** (Ya registrado como Pendiente de despliegue crítico en `docs/backlog-requerimientos.md`, REQ-016 — este plan no lo repite, solo lo referencia).
- [ ] Archivos de plantilla oficial de F-14 y Planilla Única recibidos del negocio y cargados como el recurso que DP-02 asuma, antes de PR-6/PR-7.
- [ ] `openapi.yaml` sin drift tras cada PR.
