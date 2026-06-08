using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Infrastructure.Policies;

namespace CLARIHR.Application.UnitTests;

public sealed class ResourceActionPolicyTests
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
}
