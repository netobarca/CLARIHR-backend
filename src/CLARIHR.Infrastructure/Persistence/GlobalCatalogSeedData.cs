using CLARIHR.Application.Features.Provisioning.Common;
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

}
