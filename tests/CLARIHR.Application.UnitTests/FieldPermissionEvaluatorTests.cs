using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.IdentityAccess;

namespace CLARIHR.Application.UnitTests;

public sealed class FieldPermissionEvaluatorTests
{
    [Fact]
    public void BuildProfile_WhenFieldVisibilityIsDisabled_ShouldHideAndStopSerializingTheField()
    {
        var profile = FieldPermissionEvaluator.BuildProfile(
            CompanyUserFieldKeys.ResourceKey,
            FieldCatalogRegistry.GetResourceFields(CompanyUserFieldKeys.ResourceKey),
            new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase)
            {
                [CompanyUserFieldKeys.Email] = FieldPermissionEvaluator.NormalizeOverride(
                    isVisible: false,
                    isEditable: true,
                    isRequired: true,
                    isMasked: true)
            },
            new RbacPermissionState(true, true, true, true, true));

        var serializer = new FieldSerializationService();
        var emailRule = profile.GetRule(CompanyUserFieldKeys.Email);

        Assert.False(emailRule.IsVisible);
        Assert.False(emailRule.IsEditable);
        Assert.Null(serializer.SerializeString(emailRule, "ana@acme.test"));
    }

    [Fact]
    public void BuildProfile_WhenFieldIsReadOnly_ShouldBlockUpdateWrites()
    {
        var profile = FieldPermissionEvaluator.BuildProfile(
            CompanyUserFieldKeys.ResourceKey,
            FieldCatalogRegistry.GetResourceFields(CompanyUserFieldKeys.ResourceKey),
            new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase)
            {
                [CompanyUserFieldKeys.FirstName] = FieldPermissionEvaluator.NormalizeOverride(
                    isVisible: true,
                    isEditable: false,
                    isRequired: false,
                    isMasked: false)
            },
            new RbacPermissionState(true, true, true, true, false));

        var firstNameRule = profile.GetRule(CompanyUserFieldKeys.FirstName);

        Assert.True(firstNameRule.IsVisible);
        Assert.False(firstNameRule.IsEditable);
        Assert.False(firstNameRule.CanWrite(RbacPermissionAction.Update));
    }

    [Fact]
    public void BuildProfile_WhenScreenAccessIsMissing_ShouldHideAllFields()
    {
        var profile = FieldPermissionEvaluator.BuildProfile(
            CompanyUserFieldKeys.ResourceKey,
            FieldCatalogRegistry.GetResourceFields(CompanyUserFieldKeys.ResourceKey),
            new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase),
            new RbacPermissionState(false, false, false, false, false));

        Assert.All(profile.Rules, static rule =>
        {
            Assert.False(rule.IsVisible);
            Assert.False(rule.IsEditable);
        });
    }

    [Fact]
    public void NormalizeOverride_WhenVisibilityIsDisabled_ShouldNormalizeDependentFlags()
    {
        var normalized = FieldPermissionEvaluator.NormalizeOverride(
            isVisible: false,
            isEditable: true,
            isRequired: true,
            isMasked: true);

        Assert.False(normalized.IsVisible);
        Assert.False(normalized.IsEditable);
        Assert.False(normalized.IsRequired);
        Assert.False(normalized.IsMasked);
    }
}
