using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Provisioning.Common;

namespace CLARIHR.Infrastructure.Persistence;

internal static class GlobalCatalogSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 03, 18, 0, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<object> GetPlanEntitlements() =>
        ProvisioningConstants.FreePlanEnabledModules.Select(static (moduleKey, index) => new
        {
            Id = -1000L - index,
            PlanCode = ProvisioningConstants.FreePlanCode.ToUpperInvariant(),
            ModuleKey = moduleKey.ToUpperInvariant(),
            IsEnabled = true,
            CreatedUtc = SeededAtUtc,
            ModifiedUtc = SeededAtUtc
        });

    public static IEnumerable<object> GetRbacResources() =>
        PermissionMatrixCatalog.Screens.Select(static screen => new
        {
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
