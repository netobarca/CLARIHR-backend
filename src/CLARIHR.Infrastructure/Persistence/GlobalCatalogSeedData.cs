using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Common;

namespace CLARIHR.Infrastructure.Persistence;

internal static class GlobalCatalogSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 03, 18, 0, 0, 0, DateTimeKind.Utc);
    public const long FreeCommercialPlanInternalId = -3000L;
    public const long FreeCommercialPlanVersionInternalId = -3001L;
    public static readonly Guid FreeCommercialPlanPublicId = Guid.Parse("00000000-0000-0000-0000-000000000901");
    public static readonly Guid FreeCommercialPlanConcurrencyToken = Guid.Parse("00000000-0000-0000-0000-000000000902");

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
                Name = "Free",
                NormalizedName = "FREE",
                Description = "Canonical free commercial plan used during provisioning.",
                BaseMonthlyFee = 0m,
                PricePerActiveEmployee = 0m,
                Status = CommercialPlanStatus.Active,
                IsSystemPlan = true,
                ConcurrencyToken = FreeCommercialPlanConcurrencyToken,
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
            }
        ];

    public static IEnumerable<object> GetPlanEntitlements() =>
        CommercialModuleCatalog.DefaultFreeModuleKeys.Select(static (moduleKey, index) => new
        {
            Id = -1000L - index,
            PublicId = CreateSeedPublicId("PLAN_ENTITLEMENT", moduleKey),
            CommercialPlanId = FreeCommercialPlanInternalId,
            PlanCode = ProvisioningConstants.FreePlanCode.ToUpperInvariant(),
            ModuleKey = moduleKey.ToUpperInvariant(),
            IsEnabled = true,
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        });

    public static IEnumerable<object> GetRbacResources() =>
        PermissionMatrixCatalog.Screens.Select(static (screen, index) => new
        {
            Id = -4000L - index,
            PublicId = CreateSeedPublicId("RBAC_RESOURCE", screen.ResourceKey),
            ResourceKey = screen.ResourceKey,
            NormalizedResourceKey = screen.ResourceKey.ToUpperInvariant(),
            DisplayName = screen.DisplayName,
            IsActive = true,
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        });

    public static IEnumerable<object> GetFieldCatalogEntries() =>
        FieldCatalogRegistry.Definitions.Select(static (definition, index) => new
        {
            Id = -2000L - index,
            PublicId = CreateSeedPublicId("FIELD_CATALOG", definition.FieldKey),
            FieldKey = definition.FieldKey.Trim(),
            NormalizedFieldKey = definition.FieldKey.Trim().ToUpperInvariant(),
            ResourceKey = definition.ResourceKey.Trim(),
            NormalizedResourceKey = definition.ResourceKey.Trim().ToUpperInvariant(),
            PropertyName = definition.PropertyName.Trim(),
            NormalizedPropertyName = definition.PropertyName.Trim().ToUpperInvariant(),
            DisplayName = definition.DisplayName.Trim(),
            IsConfigurable = definition.IsConfigurable,
            IsSensitive = definition.IsSensitive,
            DataType = definition.DataType.Trim(),
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        });
}
