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
        Assert.Contains(AllowedActionReasonCodes.NonEditableState, result.Reasons);
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
        Assert.Contains(AllowedActionReasonCodes.SoftDeleteEnforced, result.Reasons);
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
        Assert.Contains(AllowedActionReasonCodes.HasDependencies, result.Reasons);
    }

    [Fact]
    public void ResourceActionPolicyService_WhenPublishableStateAndAllowed_ShouldAllowPublish()
    {
        var service = new ResourceActionPolicyService();

        var result = service.Evaluate(new ResourceActionContext(
            "JOB_PROFILES",
            State: "Draft",
            IsActive: true,
            SupportsPublish: true,
            PublishAllowed: true,
            PublishableStates: ["DRAFT"]));

        Assert.True(result.CanPublish);
    }

    [Fact]
    public void ResourceActionPolicyService_WhenStateNotPublishable_ShouldBlockPublishAsRestricted()
    {
        var service = new ResourceActionPolicyService();

        var result = service.Evaluate(new ResourceActionContext(
            "JOB_PROFILES",
            State: "Published",
            IsActive: true,
            SupportsPublish: true,
            PublishAllowed: true,
            PublishableStates: ["DRAFT"]));

        Assert.False(result.CanPublish);
        Assert.Contains(AllowedActionReasonCodes.ActionRestricted, result.Reasons);
    }

    [Fact]
    public void ResourceActionPolicyService_WhenPublishNotAllowed_ShouldBlockPublishAsNotAuthorized()
    {
        var service = new ResourceActionPolicyService();

        var result = service.Evaluate(new ResourceActionContext(
            "JOB_PROFILES",
            State: "Draft",
            IsActive: true,
            SupportsPublish: true,
            PublishAllowed: false,
            PublishableStates: ["DRAFT"]));

        Assert.False(result.CanPublish);
        Assert.Contains(AllowedActionReasonCodes.NotAuthorized, result.Reasons);
    }
}
