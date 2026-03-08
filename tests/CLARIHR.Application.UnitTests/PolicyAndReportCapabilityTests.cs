using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Infrastructure.Policies;
using CLARIHR.Infrastructure.Reports;

namespace CLARIHR.Application.UnitTests;

public sealed class PolicyAndReportCapabilityTests
{
    [Fact]
    public void ResourceActionPolicyService_WhenStateIsNonEditable_ShouldDisableEdit()
    {
        var service = new ResourceActionPolicyService();

        var result = service.Evaluate(new ResourceActionContext(
            OrgUnitPermissionCodes.ResourceKey,
            State: "Archived",
            IsActive: true,
            SupportsEdit: true,
            NonEditableStates: ["ARCHIVED"]));

        Assert.False(result.CanEdit);
        Assert.Contains(result.Reasons, reason => reason.Contains("cannot be edited", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResourceActionPolicyService_WhenSoftDeletePolicyApplies_ShouldDisableDelete()
    {
        var service = new ResourceActionPolicyService();

        var result = service.Evaluate(new ResourceActionContext(
            OrgUnitPermissionCodes.ResourceKey,
            State: null,
            IsActive: true,
            SupportsEdit: true,
            SupportsDelete: false));

        Assert.False(result.CanDelete);
        Assert.Contains(result.Reasons, reason => reason.Contains("soft delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResourceActionPolicyService_WhenDependenciesExist_ShouldDisableInactivate()
    {
        var service = new ResourceActionPolicyService();

        var result = service.Evaluate(new ResourceActionContext(
            OrgUnitPermissionCodes.ResourceKey,
            State: null,
            IsActive: true,
            HasDependencies: true,
            SupportsEdit: true,
            SupportsInactivate: true));

        Assert.False(result.CanInactivate);
        Assert.Contains(result.Reasons, reason => reason.Contains("dependencies", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReportCapabilityRegistry_ShouldReturnOrgUnitCapabilities()
    {
        var registry = new ReportCapabilityRegistry();

        var found = registry.TryGet(OrgUnitPermissionCodes.ResourceKey, out var definition);

        Assert.True(found);
        Assert.NotNull(definition);
        Assert.True(definition.Capabilities.SupportsExport);
        Assert.False(definition.Capabilities.SupportsPrint);
        Assert.Contains(definition.Capabilities.SupportedTableFormats, format => format.Equals("csv", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(definition.Capabilities.SupportedTableFormats, format => format.Equals("xlsx", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(definition.Capabilities.SupportedGraphFormats, format => format.Equals("graphml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(definition.Capabilities.SupportedGraphFormats, format => format.Equals("json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(definition.Capabilities.SupportedGraphFormats, format => format.Equals("dot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReportCapabilityRegistry_WhenUnknownResource_ShouldReturnFalse()
    {
        var registry = new ReportCapabilityRegistry();

        var found = registry.TryGet("UNKNOWN_RESOURCE", out _);

        Assert.False(found);
    }
}
