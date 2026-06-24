using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Banks;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;

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

    private static long ResolveCountryId(string countryCode) =>
        CLARIHR.Domain.Locations.CountryCatalog.Items
            .Single(item => string.Equals(item.Code, countryCode, StringComparison.OrdinalIgnoreCase))
            .Id;

}
