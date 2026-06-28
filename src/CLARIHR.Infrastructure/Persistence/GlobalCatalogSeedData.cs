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

    // ── Simple country-scoped general catalogs ──────────────────────────────────────────────────────────
    // These catalogs back required `code` fields on personnel-file sections. They were previously seeded only
    // by DevSeedService (dev environment, hard-coded dev tenant), so they came up EMPTY in every staging/prod
    // tenant and hard-blocked data entry (frontend incident "Catálogos de personnel-files vacíos", 2026-06-27).
    // Seeding them here via HasData lands them in EVERY environment through the migration pipeline (MigrateAsync)
    // and backfills already-provisioned databases — same proven pattern as assignment-types / employment-statuses.
    // Values mirror the dev seed verbatim. SV only for this phase; their DevSeedService blocks were removed to
    // avoid colliding with these HasData rows on the (country, normalized_code) unique index.

    public static IEnumerable<object> GetCurrencyCatalogItems() =>
    [
        CreateGeneralCatalogSeed("CURRENCY_CATALOG", -9370L, "SV", "USD", "Dolar estadounidense", 10),
    ];

    public static IEnumerable<object> GetPaymentMethodCatalogItems() =>
    [
        CreateGeneralCatalogSeed("PAYMENT_METHOD_CATALOG", -9320L, "SV", "TRANSFERENCIA", "Transferencia bancaria", 10),
        CreateGeneralCatalogSeed("PAYMENT_METHOD_CATALOG", -9321L, "SV", "CHEQUE", "Cheque", 20),
        CreateGeneralCatalogSeed("PAYMENT_METHOD_CATALOG", -9322L, "SV", "EFECTIVO", "Efectivo", 30),
    ];

    public static IEnumerable<object> GetSubstitutionTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("SUBSTITUTION_TYPE_CATALOG", -9330L, "SV", "VACACIONES", "Vacaciones", 10),
        CreateGeneralCatalogSeed("SUBSTITUTION_TYPE_CATALOG", -9331L, "SV", "INCAPACIDAD", "Incapacidad", 20),
        CreateGeneralCatalogSeed("SUBSTITUTION_TYPE_CATALOG", -9332L, "SV", "PERMISO", "Permiso", 30),
        CreateGeneralCatalogSeed("SUBSTITUTION_TYPE_CATALOG", -9333L, "SV", "MISION_OFICIAL", "Misión oficial", 40),
        CreateGeneralCatalogSeed("SUBSTITUTION_TYPE_CATALOG", -9334L, "SV", "LICENCIA", "Licencia", 50),
        CreateGeneralCatalogSeed("SUBSTITUTION_TYPE_CATALOG", -9335L, "SV", "OTRO", "Otro", 60),
    ];

    public static IEnumerable<object> GetAssetAccessTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9300L, "SV", "EQUIPO_COMPUTO", "Equipo de cómputo", 10),
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9301L, "SV", "TELEFONO_MOVIL", "Teléfono móvil", 20),
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9302L, "SV", "UNIFORME", "Uniforme", 30),
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9303L, "SV", "LICENCIA_SOFTWARE", "Licencia de software", 40),
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9304L, "SV", "ACCESO_SISTEMA", "Acceso a sistema", 50),
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9305L, "SV", "MOBILIARIO", "Mobiliario", 60),
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9306L, "SV", "HERRAMIENTA", "Herramienta", 70),
        CreateGeneralCatalogSeed("ASSET_ACCESS_TYPE_CATALOG", -9307L, "SV", "OTRO", "Otro", 80),
    ];

    public static IEnumerable<object> GetDeliveryStatusCatalogItems() =>
    [
        CreateGeneralCatalogSeed("DELIVERY_STATUS_CATALOG", -9310L, "SV", "PENDIENTE", "Pendiente", 10),
        CreateGeneralCatalogSeed("DELIVERY_STATUS_CATALOG", -9311L, "SV", "ENTREGADO", "Entregado", 20),
        CreateGeneralCatalogSeed("DELIVERY_STATUS_CATALOG", -9312L, "SV", "EN_USO", "En uso", 30),
        CreateGeneralCatalogSeed("DELIVERY_STATUS_CATALOG", -9313L, "SV", "DEVUELTO", "Devuelto", 40),
        CreateGeneralCatalogSeed("DELIVERY_STATUS_CATALOG", -9314L, "SV", "EXTRAVIADO", "Extraviado", 50),
        CreateGeneralCatalogSeed("DELIVERY_STATUS_CATALOG", -9315L, "SV", "DANADO", "Dañado", 60),
        CreateGeneralCatalogSeed("DELIVERY_STATUS_CATALOG", -9316L, "SV", "NO_APLICA", "No aplica", 70),
    ];

    public static IEnumerable<object> GetMedicalClaimTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9340L, "SV", "AMBULATORIO", "Ambulatorio", 10),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9341L, "SV", "HOSPITALARIO", "Hospitalario", 20),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9342L, "SV", "EMERGENCIA", "Emergencia", 30),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9343L, "SV", "FARMACIA", "Farmacia", 40),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9344L, "SV", "LABORATORIO", "Laboratorio", 50),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9345L, "SV", "DENTAL", "Dental", 60),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9346L, "SV", "OFTALMOLOGICO", "Oftalmológico", 70),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9347L, "SV", "MATERNIDAD", "Maternidad", 80),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_TYPE_CATALOG", -9348L, "SV", "OTRO", "Otro", 90),
    ];

    public static IEnumerable<object> GetMedicalClaimStatusCatalogItems() =>
    [
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9350L, "SV", "PRESENTADO", "Presentado", 10),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9351L, "SV", "EN_REVISION", "En revisión", 20),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9352L, "SV", "PENDIENTE_DOCUMENTACION", "Pendiente de documentación", 30),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9353L, "SV", "APROBADO", "Aprobado", 40),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9354L, "SV", "RECHAZADO", "Rechazado", 50),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9355L, "SV", "PAGADO", "Pagado", 60),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9356L, "SV", "PAGO_PARCIAL", "Pago parcial", 70),
        CreateGeneralCatalogSeed("MEDICAL_CLAIM_STATUS_CATALOG", -9357L, "SV", "ANULADO", "Anulado", 80),
    ];

    public static IEnumerable<object> GetOffPayrollTransactionTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("OFF_PAYROLL_TRANSACTION_TYPE_CATALOG", -9360L, "SV", "HERRAMIENTAS", "Herramientas de trabajo", 10),
        CreateGeneralCatalogSeed("OFF_PAYROLL_TRANSACTION_TYPE_CATALOG", -9361L, "SV", "EPP", "Equipo de protección personal", 20),
        CreateGeneralCatalogSeed("OFF_PAYROLL_TRANSACTION_TYPE_CATALOG", -9362L, "SV", "UNIFORMES", "Uniformes", 30),
        CreateGeneralCatalogSeed("OFF_PAYROLL_TRANSACTION_TYPE_CATALOG", -9363L, "SV", "PROMOCIONALES", "Artículos promocionales", 40),
        CreateGeneralCatalogSeed("OFF_PAYROLL_TRANSACTION_TYPE_CATALOG", -9364L, "SV", "RECONOCIMIENTOS", "Reconocimientos", 50),
        CreateGeneralCatalogSeed("OFF_PAYROLL_TRANSACTION_TYPE_CATALOG", -9365L, "SV", "REGALOS", "Regalos", 60),
    ];

    public static IEnumerable<object> GetEconomicAidTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9520L, "SV", "EMERGENCIA_MEDICA", "Emergencia médica", 10),
        CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9521L, "SV", "GASTOS_FUNEBRES", "Gastos fúnebres / fallecimiento de familiar", 20),
        CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9522L, "SV", "DESASTRE_NATURAL", "Desastre natural", 30),
        CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9523L, "SV", "INCENDIO_VIVIENDA", "Incendio o daño en vivienda", 40),
        CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9524L, "SV", "CALAMIDAD_DOMESTICA", "Calamidad doméstica", 50),
        CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9525L, "SV", "ACCIDENTE", "Accidente", 60),
        CreateGeneralCatalogSeed("ECONOMIC_AID_TYPE_CATALOG", -9526L, "SV", "OTRA", "Otra emergencia", 70),
    ];

    public static IEnumerable<object> GetEconomicAidStatusCatalogItems() =>
    [
        CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9540L, "SV", "SOLICITADA", "Solicitada", 10),
        CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9541L, "SV", "EN_REVISION", "En revisión", 20),
        CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9542L, "SV", "PENDIENTE_DOCUMENTACION", "Pendiente de documentación", 30),
        CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9543L, "SV", "APROBADA", "Aprobada", 40),
        CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9544L, "SV", "RECHAZADA", "Rechazada", 50),
        CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9545L, "SV", "DESEMBOLSADA", "Desembolsada", 60),
        CreateGeneralCatalogSeed("ECONOMIC_AID_STATUS_CATALOG", -9546L, "SV", "ANULADA", "Anulada", 70),
    ];

    // Certificate ("constancia") catalogs — D-18/D-19. Type codes drive the PDF layout / salary printing.
    public static IEnumerable<object> GetCertificateTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9560L, "SV", "CONSTANCIA_SALARIO", "Constancia de salario", 10),
        CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9561L, "SV", "CONSTANCIA_LABORAL", "Constancia de trabajo (laboral)", 20),
        CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9562L, "SV", "CONSTANCIA_EMBAJADA", "Constancia para embajada", 30),
        CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9563L, "SV", "CONSTANCIA_TIEMPO_LABORADO", "Constancia de tiempo laborado", 40),
        CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9564L, "SV", "CONSTANCIA_NO_DESCUENTO", "Constancia de no descuento", 50),
        CreateGeneralCatalogSeed("CERTIFICATE_TYPE_CATALOG", -9565L, "SV", "CARTA_RECOMENDACION", "Carta de recomendación laboral", 60),
    ];

    public static IEnumerable<object> GetCertificateRequestStatusCatalogItems() =>
    [
        CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9570L, "SV", "SOLICITADA", "Solicitada", 10),
        CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9571L, "SV", "EN_PROCESO", "En proceso", 20),
        CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9572L, "SV", "EMITIDA", "Emitida", 30),
        CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9573L, "SV", "ENTREGADA", "Entregada", 40),
        CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9574L, "SV", "RECHAZADA", "Rechazada", 50),
        CreateGeneralCatalogSeed("CERTIFICATE_REQUEST_STATUS_CATALOG", -9575L, "SV", "ANULADA", "Anulada", 60),
    ];

    public static IEnumerable<object> GetCertificateDeliveryMethodCatalogItems() =>
    [
        CreateGeneralCatalogSeed("CERTIFICATE_DELIVERY_METHOD_CATALOG", -9580L, "SV", "PRESENCIAL", "Entrega presencial", 10),
        CreateGeneralCatalogSeed("CERTIFICATE_DELIVERY_METHOD_CATALOG", -9581L, "SV", "CORREO_ELECTRONICO", "Correo electrónico", 20),
        CreateGeneralCatalogSeed("CERTIFICATE_DELIVERY_METHOD_CATALOG", -9582L, "SV", "PORTAL", "Descarga desde el portal", 30),
    ];

    public static IEnumerable<object> GetCertificatePurposeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9590L, "SV", "TRAMITE_BANCARIO", "Trámite bancario", 10),
        CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9591L, "SV", "CREDITO", "Solicitud de crédito", 20),
        CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9592L, "SV", "VISA_EMBAJADA", "Visa / trámite ante embajada", 30),
        CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9593L, "SV", "TRAMITE_MIGRATORIO", "Trámite migratorio", 40),
        CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9594L, "SV", "USO_PERSONAL", "Uso personal", 50),
        CreateGeneralCatalogSeed("CERTIFICATE_PURPOSE_CATALOG", -9595L, "SV", "OTRO", "Otro", 60),
    ];

    public static IEnumerable<object> GetLanguageCatalogItems() =>
    [
        CreateGeneralCatalogSeed("LANGUAGE_CATALOG", -9410L, "SV", "ENGLISH", "Ingles", 10),
        CreateGeneralCatalogSeed("LANGUAGE_CATALOG", -9411L, "SV", "SPANISH", "Espanol", 20),
    ];

    public static IEnumerable<object> GetLanguageLevelCatalogItems() =>
    [
        CreateGeneralCatalogSeed("LANGUAGE_LEVEL_CATALOG", -9420L, "SV", "ADVANCED", "Avanzado", 10),
        CreateGeneralCatalogSeed("LANGUAGE_LEVEL_CATALOG", -9421L, "SV", "INTERMEDIATE", "Intermedio", 20),
        CreateGeneralCatalogSeed("LANGUAGE_LEVEL_CATALOG", -9422L, "SV", "BASIC", "Basico", 30),
    ];

    public static IEnumerable<object> GetTrainingTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("TRAINING_TYPE_CATALOG", -9430L, "SV", "COURSE", "Curso", 10),
        CreateGeneralCatalogSeed("TRAINING_TYPE_CATALOG", -9431L, "SV", "WORKSHOP", "Taller", 20),
        CreateGeneralCatalogSeed("TRAINING_TYPE_CATALOG", -9432L, "SV", "CERTIFICATION", "Certificacion", 30),
    ];

    public static IEnumerable<object> GetDurationUnitCatalogItems() =>
    [
        CreateGeneralCatalogSeed("DURATION_UNIT_CATALOG", -9440L, "SV", "HOUR", "Hora", 10),
        CreateGeneralCatalogSeed("DURATION_UNIT_CATALOG", -9441L, "SV", "DAY", "Dia", 20),
    ];

    public static IEnumerable<object> GetReferenceTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("REFERENCE_TYPE_CATALOG", -9450L, "SV", "PERSONAL", "Personal", 10),
        CreateGeneralCatalogSeed("REFERENCE_TYPE_CATALOG", -9451L, "SV", "PROFESSIONAL", "Profesional", 20),
    ];

    // ── NEW catalogs (frontend incidents contract-history / personnel-actions, 2026-06-27) ───────────────
    // contract-types backs the manual contract-history `contractTypeCode`; action-types / action-statuses back
    // the personnel-actions journal `actionTypeCode` / `actionStatusCode` (previously free text → no catalog).

    public static IEnumerable<object> GetContractTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9460L, "SV", "INDEFINIDO", "Contrato por tiempo indefinido", 10),
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9461L, "SV", "PLAZO_FIJO", "Contrato a plazo fijo", 20),
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9462L, "SV", "POR_OBRA", "Contrato por obra o labor", 30),
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9463L, "SV", "EVENTUAL", "Contrato eventual", 40),
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9464L, "SV", "APRENDIZAJE", "Contrato de aprendizaje", 50),
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9465L, "SV", "SERVICIOS_PROFESIONALES", "Servicios profesionales", 60),
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9466L, "SV", "TEMPORAL", "Contrato temporal", 70),
        CreateGeneralCatalogSeed("CONTRACT_TYPE_CATALOG", -9467L, "SV", "OTRO", "Otro", 80),
    ];

    public static IEnumerable<object> GetActionTypeCatalogItems() =>
    [
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9470L, "SV", "NOMBRAMIENTO", "Nombramiento", 10),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9471L, "SV", "CONTRATACION", "Contratación", 20),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9472L, "SV", "RECONTRATACION", "Recontratación", 30),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9473L, "SV", "ASCENSO", "Ascenso", 40),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9474L, "SV", "TRASLADO", "Traslado", 50),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9475L, "SV", "CAMBIO_PUESTO", "Cambio de puesto", 60),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9476L, "SV", "AUMENTO_SALARIAL", "Aumento salarial", 70),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9477L, "SV", "AMONESTACION", "Amonestación", 80),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9478L, "SV", "SUSPENSION", "Suspensión", 90),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9479L, "SV", "PERMISO", "Permiso", 100),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9480L, "SV", "REINTEGRO", "Reintegro", 110),
        CreateGeneralCatalogSeed("ACTION_TYPE_CATALOG", -9481L, "SV", "OTRO", "Otro", 120),
    ];

    public static IEnumerable<object> GetActionStatusCatalogItems() =>
    [
        CreateGeneralCatalogSeed("ACTION_STATUS_CATALOG", -9490L, "SV", "BORRADOR", "Borrador", 10),
        CreateGeneralCatalogSeed("ACTION_STATUS_CATALOG", -9491L, "SV", "PENDIENTE", "Pendiente", 20),
        CreateGeneralCatalogSeed("ACTION_STATUS_CATALOG", -9492L, "SV", "EN_TRAMITE", "En trámite", 30),
        CreateGeneralCatalogSeed("ACTION_STATUS_CATALOG", -9493L, "SV", "APROBADA", "Aprobada", 40),
        CreateGeneralCatalogSeed("ACTION_STATUS_CATALOG", -9494L, "SV", "RECHAZADA", "Rechazada", 50),
        CreateGeneralCatalogSeed("ACTION_STATUS_CATALOG", -9495L, "SV", "APLICADA", "Aplicada", 60),
        CreateGeneralCatalogSeed("ACTION_STATUS_CATALOG", -9496L, "SV", "ANULADA", "Anulada", 70),
    ];

    // HR analytics dashboard — parametrizable AGE ranges (D-10). Bounds in whole years, inclusive; null upper = open.
    public static IEnumerable<object> GetAgeRangeCatalogItems() =>
    [
        CreateAgeRangeSeed(-9500L, "SV", "EDAD_18_25", "18 a 25 años", 10, 18, 25),
        CreateAgeRangeSeed(-9501L, "SV", "EDAD_26_35", "26 a 35 años", 20, 26, 35),
        CreateAgeRangeSeed(-9502L, "SV", "EDAD_36_45", "36 a 45 años", 30, 36, 45),
        CreateAgeRangeSeed(-9503L, "SV", "EDAD_46_55", "46 a 55 años", 40, 46, 55),
        CreateAgeRangeSeed(-9504L, "SV", "EDAD_56_MAS", "56 años o más", 50, 56, null),
    ];

    // HR analytics dashboard — parametrizable SENIORITY (antigüedad) ranges (D-10). Bounds in whole months, inclusive.
    public static IEnumerable<object> GetSeniorityRangeCatalogItems() =>
    [
        CreateSeniorityRangeSeed(-9510L, "SV", "ANT_0_1", "Menos de 1 año", 10, 0, 11),
        CreateSeniorityRangeSeed(-9511L, "SV", "ANT_1_3", "1 a 3 años", 20, 12, 35),
        CreateSeniorityRangeSeed(-9512L, "SV", "ANT_3_5", "3 a 5 años", 30, 36, 59),
        CreateSeniorityRangeSeed(-9513L, "SV", "ANT_5_10", "5 a 10 años", 40, 60, 119),
        CreateSeniorityRangeSeed(-9514L, "SV", "ANT_10_MAS", "10 años o más", 50, 120, null),
    ];

    private static object CreateAgeRangeSeed(
        long id, string countryCode, string code, string name, int sortOrder, int lowerBoundYears, int? upperBoundYears) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("AGE_RANGE_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            IsActive = true,
            SortOrder = sortOrder,
            LowerBoundYears = lowerBoundYears,
            UpperBoundYears = upperBoundYears,
            ConcurrencyToken = CreateSeedPublicId("AGE_RANGE_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    private static object CreateSeniorityRangeSeed(
        long id, string countryCode, string code, string name, int sortOrder, int lowerBoundMonths, int? upperBoundMonths) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId("SENIORITY_RANGE_CATALOG", $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            IsActive = true,
            SortOrder = sortOrder,
            LowerBoundMonths = lowerBoundMonths,
            UpperBoundMonths = upperBoundMonths,
            ConcurrencyToken = CreateSeedPublicId("SENIORITY_RANGE_CATALOG_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    // Shared factory for the simple country-scoped general-catalog HasData rows above. Deterministic PublicId /
    // ConcurrencyToken (via CreateSeedPublicId) keep the migration stable across runs; `seedPrefix` is unique
    // per catalog so PublicIds never collide across catalogs.
    private static object CreateGeneralCatalogSeed(
        string seedPrefix,
        long id,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new
        {
            Id = id,
            PublicId = CreateSeedPublicId(seedPrefix, $"{countryCode}:{code}"),
            CountryCatalogItemId = ResolveCountryId(countryCode),
            CountryCode = countryCode,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            IsActive = true,
            SortOrder = sortOrder,
            ConcurrencyToken = CreateSeedPublicId($"{seedPrefix}_CONCURRENCY", $"{countryCode}:{code}"),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        };

    private static long ResolveCountryId(string countryCode) =>
        CLARIHR.Domain.Locations.CountryCatalog.Items
            .Single(item => string.Equals(item.Code, countryCode, StringComparison.OrdinalIgnoreCase))
            .Id;

}
