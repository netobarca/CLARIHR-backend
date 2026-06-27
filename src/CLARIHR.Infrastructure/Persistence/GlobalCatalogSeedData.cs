using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Banks;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Infrastructure.Persistence;

internal static class GlobalCatalogSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 03, 18, 0, 0, 0, DateTimeKind.Utc);
    public const long FreeCommercialPlanInternalId = -3000L;
    public const long FreeCommercialPlanVersionInternalId = -3001L;
    public const long MasterCommercialPlanInternalId = -3002L;
    public const long MasterCommercialPlanVersionInternalId = -3003L;
    public static readonly Guid FreeCommercialPlanPublicId = Guid.Parse("00000000-0000-0000-0000-000000000901");
    public static readonly Guid FreeCommercialPlanConcurrencyToken = Guid.Parse("00000000-0000-0000-0000-000000000902");
    public static readonly Guid MasterCommercialPlanPublicId = Guid.Parse("00000000-0000-0000-0000-000000000903");
    public static readonly Guid MasterCommercialPlanConcurrencyToken = Guid.Parse("00000000-0000-0000-0000-000000000904");

    public static Guid CreateSeedPublicId(string category, string key) =>
        Entity.CreateDeterministicPublicId($"{category}:{key}".ToUpperInvariant());

    public static IEnumerable<object> GetCommercialPlans() =>
        [
            new
            {
                Id = FreeCommercialPlanInternalId,
                PublicId = FreeCommercialPlanPublicId,
                Code = ProvisioningConstants.FreePlanCode,
                NormalizedCode = ProvisioningConstants.FreePlanCode.ToUpperInvariant(),
                Name = ProvisioningConstants.FreePlanName,
                NormalizedName = ProvisioningConstants.FreePlanName.ToUpperInvariant(),
                Description = ProvisioningConstants.FreePlanDescription,
                BaseMonthlyFee = 0m,
                PricePerActiveEmployee = 0m,
                Status = CommercialPlanStatus.Active,
                IsSystemPlan = true,
                ConcurrencyToken = FreeCommercialPlanConcurrencyToken,
                CreatedUtc = SeededAtUtc,
                ModifiedUtc = SeededAtUtc
            },
            new
            {
                Id = MasterCommercialPlanInternalId,
                PublicId = MasterCommercialPlanPublicId,
                Code = ProvisioningConstants.MasterPlanCode,
                NormalizedCode = ProvisioningConstants.MasterPlanCode.ToUpperInvariant(),
                Name = ProvisioningConstants.MasterPlanName,
                NormalizedName = ProvisioningConstants.MasterPlanName.ToUpperInvariant(),
                Description = ProvisioningConstants.MasterPlanDescription,
                BaseMonthlyFee = 0m,
                PricePerActiveEmployee = 0m,
                Status = CommercialPlanStatus.Active,
                IsSystemPlan = true,
                ConcurrencyToken = MasterCommercialPlanConcurrencyToken,
                CreatedUtc = SeededAtUtc,
                ModifiedUtc = SeededAtUtc
            }
        ];

    public static IEnumerable<object> GetCommercialPlanVersions() =>
        [
            new
            {
                Id = FreeCommercialPlanVersionInternalId,
                PublicId = CreateSeedPublicId("COMMERCIAL_PLAN_VERSION", "FREE:1"),
                CommercialPlanId = FreeCommercialPlanInternalId,
                VersionNumber = 1,
                CurrencyCode = "USD",
                BaseMonthlyFee = 0m,
                PricePerActiveEmployee = 0m,
                EffectiveFromUtc = SeededAtUtc,
                EffectiveToUtc = (DateTime?)null,
                CreatedUtc = SeededAtUtc,
                ModifiedUtc = SeededAtUtc
            },
            new
            {
                Id = MasterCommercialPlanVersionInternalId,
                PublicId = CreateSeedPublicId("COMMERCIAL_PLAN_VERSION", "MASTER:1"),
                CommercialPlanId = MasterCommercialPlanInternalId,
                VersionNumber = 1,
                CurrencyCode = "USD",
                BaseMonthlyFee = 0m,
                PricePerActiveEmployee = 0m,
                EffectiveFromUtc = SeededAtUtc,
                EffectiveToUtc = (DateTime?)null,
                CreatedUtc = SeededAtUtc,
                ModifiedUtc = SeededAtUtc
            }
        ];

    public static IEnumerable<object> GetPlanEntitlements() =>
        CreatePlanEntitlements(
            FreeCommercialPlanInternalId,
            ProvisioningConstants.FreePlanCode,
            CommercialModuleCatalog.DefaultFreeModuleKeys,
            startId: -1000L)
        .Concat(CreatePlanEntitlements(
            MasterCommercialPlanInternalId,
            ProvisioningConstants.MasterPlanCode,
            CommercialModuleCatalog.DefaultMasterModuleKeys,
            startId: -2000L));

    private static IEnumerable<object> CreatePlanEntitlements(
        long commercialPlanId,
        string planCode,
        IEnumerable<string> moduleKeys,
        long startId) =>
        moduleKeys.Select((moduleKey, index) => new
        {
            Id = startId - index,
            PublicId = CreateSeedPublicId("PLAN_ENTITLEMENT", $"{planCode}:{moduleKey}"),
            CommercialPlanId = commercialPlanId,
            PlanCode = planCode.ToUpperInvariant(),
            CapabilityCode = CommercialCapabilityCatalog.GetByModuleKey(moduleKey).Code,
            ModuleKey = moduleKey.ToUpperInvariant(),
            IsEnabled = true,
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        });

    public static IEnumerable<object> GetBankCatalogItems() =>
    [
        CreateBankCatalogSeed(-9000L, "SV", "BANCO_AGRICOLA", "Banco Agricola", "Agricola", alias: "Agricola"),
        CreateBankCatalogSeed(-9001L, "SV", "DAVIVIENDA", "Davivienda", "Davivienda", alias: "Davivienda"),
        CreateBankCatalogSeed(-9002L, "SV", "CUSCATLAN", "Cuscatlan", "Cuscatlan", alias: "Cuscatlan"),
        CreateBankCatalogSeed(-9003L, "SV", "BAC", "BAC Credomatic", "BAC Credomatic", alias: "BAC"),
        CreateBankCatalogSeed(-9010L, "US", "BANK_OF_AMERICA", "Bank of America", "Bank of America", alias: "BofA"),
        CreateBankCatalogSeed(-9011L, "US", "CITIBANK", "Citibank", "Citibank", alias: "Citi"),
        CreateBankCatalogSeed(-9012L, "US", "WELLS_FARGO", "Wells Fargo", "Wells Fargo", alias: "Wells"),
        CreateBankCatalogSeed(-9013L, "US", "CHASE", "Chase", "Chase", alias: "JPMorgan Chase")
    ];

    private static object CreateBankCatalogSeed(
        long id,
        string countryCode,
        string code,
        string name,
        string normalizedName,
        string? alias = null,
        string? swiftCode = null,
        string? routingCode = null) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("BANK_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = normalizedName.ToUpperInvariant(),
            Alias = alias,
            NormalizedAlias = alias?.ToUpperInvariant(),
            SwiftCode = swiftCode,
            NormalizedSwiftCode = swiftCode?.ToUpperInvariant(),
            RoutingCode = routingCode,
            NormalizedRoutingCode = routingCode?.ToUpperInvariant(),
            IsActive = true,
            SortOrder = Math.Abs((int)id) - 8900,
            ConcurrencyToken = CreateSeedPublicId("BANK_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    // Country-scoped employment-status catalog (general-catalogs key `employment-statuses`). Seeded here
    // — like the bank catalog above — so it lands in EVERY environment through the migration pipeline
    // (MigrateAsync) and backfills already-provisioned databases, instead of only fresh dev databases via
    // DevSeedService (whose idempotency guard short-circuits once the dev user exists, which is exactly why
    // the endpoint was returning 404). SV only for this phase; mirrors the codes consumed by the frontend.
    public static IEnumerable<object> GetEmploymentStatusCatalogItems() =>
    [
        CreateEmploymentStatusSeed(-9100L, "SV", "ACTIVO", "Activo", 10),
        CreateEmploymentStatusSeed(-9101L, "SV", "SUSPENDIDO", "Suspendido", 20),
        CreateEmploymentStatusSeed(-9102L, "SV", "LICENCIA", "Licencia", 30),
        CreateEmploymentStatusSeed(-9103L, "SV", "INCAPACIDAD", "Incapacidad", 40),
        CreateEmploymentStatusSeed(-9104L, "SV", "RETIRADO", "Retirado", 50)
    ];

    private static object CreateEmploymentStatusSeed(
        long id,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("EMPLOYMENT_STATUS_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            IsActive = true,
            SortOrder = sortOrder,
            ConcurrencyToken = CreateSeedPublicId("EMPLOYMENT_STATUS_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    // Country-scoped experience-metric units catalog (general-catalogs key `experience-metrics`), backing the
    // curricular-competency metric field (business decision D-04: AÑOS / MESES / DIAS / HORAS). Seeded here — like
    // the employment-status catalog above — so it lands in EVERY environment through the migration pipeline
    // (MigrateAsync) and backfills already-provisioned databases, not only fresh dev databases via DevSeedService.
    // Codes are ASCII (ANOS) so they pass code validation and normalize cleanly; names keep the Spanish accents
    // for display. SV only for this phase.
    public static IEnumerable<object> GetExperienceMetricCatalogItems() =>
    [
        CreateExperienceMetricSeed(-9120L, "SV", "ANOS", "Años", 10),
        CreateExperienceMetricSeed(-9121L, "SV", "MESES", "Meses", 20),
        CreateExperienceMetricSeed(-9122L, "SV", "DIAS", "Días", 30),
        CreateExperienceMetricSeed(-9123L, "SV", "HORAS", "Horas", 40)
    ];

    private static object CreateExperienceMetricSeed(
        long id,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("EXPERIENCE_METRIC_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            IsActive = true,
            SortOrder = sortOrder,
            ConcurrencyToken = CreateSeedPublicId("EXPERIENCE_METRIC_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    // Country-scoped assignment-type catalog (general-catalogs key `assignment-types`), backing the MANDATORY
    // `assignmentTypeCode` of employment assignments (POST/PUT/PATCH …/assigned-positions) and of the rehire flow.
    // Seeded here — like the employment-status catalog above — so it lands in EVERY environment through the
    // migration pipeline (MigrateAsync) and backfills already-provisioned databases, instead of only fresh dev
    // databases via DevSeedService (whose idempotency guard short-circuits once the dev user exists). Because the
    // field is required server-side, an empty catalog would hard-block creating a plaza; HasData guarantees the
    // frontend always has values to pick. SV only for this phase; mirrors the codes the frontend selects from.
    public static IEnumerable<object> GetAssignmentTypeCatalogItems() =>
    [
        CreateAssignmentTypeSeed(-9140L, "SV", "LEY_SALARIOS", "Ley de Salarios", 10),
        CreateAssignmentTypeSeed(-9141L, "SV", "CONTRATO", "Contrato", 20),
        CreateAssignmentTypeSeed(-9142L, "SV", "INDEFINIDO", "Tiempo indefinido", 30),
        CreateAssignmentTypeSeed(-9143L, "SV", "PLAZO_FIJO", "Plazo fijo", 40),
        CreateAssignmentTypeSeed(-9144L, "SV", "INTERINO", "Interinato", 50),
        CreateAssignmentTypeSeed(-9145L, "SV", "POR_OBRA", "Por obra o servicio", 60),
        CreateAssignmentTypeSeed(-9146L, "SV", "AD_HONOREM", "Ad honorem", 70),
        CreateAssignmentTypeSeed(-9147L, "SV", "SERVICIOS_PROFESIONALES", "Servicios profesionales", 80),
        CreateAssignmentTypeSeed(-9148L, "SV", "RECARGO_FUNCIONES", "Recargo de funciones", 90)
    ];

    private static object CreateAssignmentTypeSeed(
        long id,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("ASSIGNMENT_TYPE_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            IsActive = true,
            SortOrder = sortOrder,
            ConcurrencyToken = CreateSeedPublicId("ASSIGNMENT_TYPE_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    // Country-scoped retirement category catalog (reference-catalogs key `retirement-categories`). Seeded in
    // EVERY environment via the migration pipeline (D-13) so the baja flow always has active categories to
    // validate against. Each category carries an HRIS SeparationType for reporting roll-up (D-02). SV only.
    public static IEnumerable<object> GetRetirementCategoryCatalogItems() =>
    [
        CreateRetirementCategorySeed(-9200L, "SV", "VOLUNTARIA", "Renuncia voluntaria", 10, RetirementSeparationType.Voluntaria),
        CreateRetirementCategorySeed(-9201L, "SV", "JUBILACION", "Jubilación", 20, RetirementSeparationType.Voluntaria),
        CreateRetirementCategorySeed(-9202L, "SV", "INVOLUNTARIA", "Despido / involuntaria", 30, RetirementSeparationType.Involuntaria),
        CreateRetirementCategorySeed(-9203L, "SV", "ABANDONO", "Abandono de trabajo", 40, RetirementSeparationType.Involuntaria),
        CreateRetirementCategorySeed(-9204L, "SV", "NO_SUPERA_PERIODO_PRUEBA", "No supera período de prueba", 50, RetirementSeparationType.Involuntaria),
        CreateRetirementCategorySeed(-9205L, "SV", "FIN_CONTRATO", "Fin de contrato", 60, RetirementSeparationType.Otra),
        CreateRetirementCategorySeed(-9206L, "SV", "MUTUO_ACUERDO", "Mutuo acuerdo", 70, RetirementSeparationType.Otra),
        CreateRetirementCategorySeed(-9207L, "SV", "FALLECIMIENTO", "Fallecimiento", 80, RetirementSeparationType.Otra),
    ];

    // Country-scoped retirement reason catalog (reference-catalogs key `retirement-reasons`), each reason a
    // child of a category. Seeded in EVERY environment (D-13). SV only.
    public static IEnumerable<object> GetRetirementReasonCatalogItems() =>
    [
        // VOLUNTARIA (-9200)
        CreateRetirementReasonSeed(-9220L, "SV", "MEJOR_OFERTA_SALARIAL", "Mejor oferta salarial", 10, -9200L),
        CreateRetirementReasonSeed(-9221L, "SV", "CRECIMIENTO_PROFESIONAL", "Crecimiento profesional", 20, -9200L),
        CreateRetirementReasonSeed(-9222L, "SV", "AMBIENTE_LABORAL", "Ambiente laboral", 30, -9200L),
        CreateRetirementReasonSeed(-9223L, "SV", "RELACION_JEFATURA", "Relación con la jefatura", 40, -9200L),
        CreateRetirementReasonSeed(-9224L, "SV", "MOTIVOS_PERSONALES", "Motivos personales", 50, -9200L),
        CreateRetirementReasonSeed(-9225L, "SV", "SALUD", "Salud", 60, -9200L),
        CreateRetirementReasonSeed(-9226L, "SV", "ESTUDIOS", "Estudios", 70, -9200L),
        CreateRetirementReasonSeed(-9227L, "SV", "REUBICACION_GEOGRAFICA", "Reubicación geográfica", 80, -9200L),
        CreateRetirementReasonSeed(-9228L, "SV", "DISTANCIA_TRANSPORTE", "Distancia / transporte", 90, -9200L),
        CreateRetirementReasonSeed(-9229L, "SV", "INSATISFACCION_FUNCIONES", "Insatisfacción con las funciones", 100, -9200L),
        // JUBILACION (-9201)
        CreateRetirementReasonSeed(-9230L, "SV", "JUBILACION_EDAD", "Jubilación por edad", 10, -9201L),
        // INVOLUNTARIA (-9202)
        CreateRetirementReasonSeed(-9231L, "SV", "BAJO_DESEMPENO", "Bajo desempeño", 10, -9202L),
        CreateRetirementReasonSeed(-9232L, "SV", "REESTRUCTURACION", "Reestructuración", 20, -9202L),
        CreateRetirementReasonSeed(-9233L, "SV", "FALTA_DISCIPLINARIA", "Falta disciplinaria", 30, -9202L),
        CreateRetirementReasonSeed(-9234L, "SV", "AUSENTISMO", "Ausentismo", 40, -9202L),
        CreateRetirementReasonSeed(-9235L, "SV", "INCUMPLIMIENTO_POLITICAS", "Incumplimiento de políticas", 50, -9202L),
        CreateRetirementReasonSeed(-9236L, "SV", "RECORTE_PRESUPUESTARIO", "Recorte presupuestario", 60, -9202L),
        // ABANDONO (-9203)
        CreateRetirementReasonSeed(-9237L, "SV", "ABANDONO_TRABAJO", "Abandono de trabajo", 10, -9203L),
        // NO_SUPERA_PERIODO_PRUEBA (-9204)
        CreateRetirementReasonSeed(-9238L, "SV", "NO_SUPERA_PRUEBA", "No superó el período de prueba", 10, -9204L),
        // FIN_CONTRATO (-9205)
        CreateRetirementReasonSeed(-9239L, "SV", "FIN_CONTRATO_TEMPORAL", "Fin de contrato temporal", 10, -9205L),
        CreateRetirementReasonSeed(-9240L, "SV", "FIN_OBRA_PROYECTO", "Fin de obra o proyecto", 20, -9205L),
        // MUTUO_ACUERDO (-9206)
        CreateRetirementReasonSeed(-9241L, "SV", "MUTUO_ACUERDO", "Mutuo acuerdo", 10, -9206L),
        // FALLECIMIENTO (-9207)
        CreateRetirementReasonSeed(-9242L, "SV", "FALLECIMIENTO", "Fallecimiento", 10, -9207L),
    ];

    private static object CreateRetirementCategorySeed(
        long id,
        string countryCode,
        string code,
        string name,
        int sortOrder,
        RetirementSeparationType separationType) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("RETIREMENT_CATEGORY_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            SeparationType = separationType,
            IsActive = true,
            SortOrder = sortOrder,
            ConcurrencyToken = CreateSeedPublicId("RETIREMENT_CATEGORY_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    private static object CreateRetirementReasonSeed(
        long id,
        string countryCode,
        string code,
        string name,
        int sortOrder,
        long retirementCategoryCatalogItemId) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("RETIREMENT_REASON_CATALOG", $"{countryCode}:{retirementCategoryCatalogItemId}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            RetirementCategoryCatalogItemId = retirementCategoryCatalogItemId,
            IsActive = true,
            SortOrder = sortOrder,
            ConcurrencyToken = CreateSeedPublicId("RETIREMENT_REASON_CATALOG_CONCURRENCY", $"{countryCode}:{retirementCategoryCatalogItemId}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    // Closed system catalog of form control types for the exit-interview builder (general-catalogs key
    // `form-control-types`, D-08). Seeded in EVERY environment via the migration pipeline. Codes are universal;
    // seeded under SV to satisfy the per-country seed convention.
    public static IEnumerable<object> GetFormControlTypeCatalogItems() =>
    [
        CreateFormControlTypeSeed(-9260L, "SV", "TEXTO_CORTO", "Texto corto", 10, FormControlValueKind.Text, false, false, false),
        CreateFormControlTypeSeed(-9261L, "SV", "TEXTO_LARGO", "Texto largo", 20, FormControlValueKind.Text, false, false, false),
        CreateFormControlTypeSeed(-9262L, "SV", "NUMERO", "Número", 30, FormControlValueKind.Number, false, true, false),
        CreateFormControlTypeSeed(-9263L, "SV", "FECHA", "Fecha", 40, FormControlValueKind.Date, false, false, false),
        CreateFormControlTypeSeed(-9264L, "SV", "LISTA_DESPLEGABLE", "Lista desplegable", 50, FormControlValueKind.Options, true, false, false),
        CreateFormControlTypeSeed(-9265L, "SV", "OPCION_UNICA", "Opción única", 60, FormControlValueKind.Options, true, false, false),
        CreateFormControlTypeSeed(-9266L, "SV", "SELECCION_MULTIPLE", "Selección múltiple", 70, FormControlValueKind.Options, true, false, true),
        CreateFormControlTypeSeed(-9267L, "SV", "CASILLA", "Casilla (Sí/No)", 80, FormControlValueKind.Boolean, false, false, false),
        CreateFormControlTypeSeed(-9268L, "SV", "ESCALA", "Escala", 90, FormControlValueKind.Number, false, true, false),
    ];

    private static object CreateFormControlTypeSeed(
        long id,
        string countryCode,
        string code,
        string name,
        int sortOrder,
        FormControlValueKind valueKind,
        bool supportsOptions,
        bool supportsRange,
        bool supportsMultiple) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("FORM_CONTROL_TYPE_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ValueKind = valueKind,
            SupportsOptions = supportsOptions,
            SupportsRange = supportsRange,
            SupportsMultiple = supportsMultiple,
            IsActive = true,
            SortOrder = sortOrder,
            ConcurrencyToken = CreateSeedPublicId("FORM_CONTROL_TYPE_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    private static long ResolveCountryId(string countryCode) =>
        CLARIHR.Domain.Locations.CountryCatalog.Items
            .Single(item => string.Equals(item.Code, countryCode, StringComparison.OrdinalIgnoreCase))
            .Id;

}
