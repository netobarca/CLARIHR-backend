namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class FieldPermissionEvaluator
{
    public static FieldAccessProfile BuildProfile(
        string resourceKey,
        IReadOnlyCollection<FieldCatalogDefinition> catalog,
        IReadOnlyDictionary<string, FieldPermissionOverrideState> overrides,
        RbacPermissionState screenState)
    {
        var rules = catalog
            .Select(definition => BuildRule(definition, overrides, screenState))
            .ToArray();

        return new FieldAccessProfile(resourceKey, rules);
    }

    public static FieldAccessProfile Merge(
        string resourceKey,
        IReadOnlyCollection<FieldCatalogDefinition> catalog,
        IEnumerable<FieldAccessProfile> profiles)
    {
        var profileList = profiles.ToArray();
        if (profileList.Length == 0)
        {
            return BuildProfile(
                resourceKey,
                catalog,
                new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase),
                new RbacPermissionState(false, false, false, false, false));
        }

        var rules = catalog
            .Select(definition =>
            {
                var contributions = profileList
                    .Select(profile => profile.GetRule(definition.FieldKey))
                    .Where(static rule => rule.IsVisible || rule.IsEditable || rule.IsMasked || rule.CanCreate || rule.CanUpdate)
                    .ToArray();

                if (contributions.Length == 0)
                {
                    return BuildRule(
                        definition,
                        new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase),
                        new RbacPermissionState(false, false, false, false, false));
                }

                var isVisible = contributions.Any(static rule => rule.IsVisible);
                var isEditable = contributions.Any(static rule => rule.IsEditable);
                var hasVisibleUnmasked = contributions.Any(rule => rule.IsVisible && !rule.IsMasked);
                var hasVisibleMasked = contributions.Any(rule => rule.IsVisible && rule.IsMasked);

                return new FieldAccessRule(
                    definition.FieldKey,
                    definition.ResourceKey,
                    definition.PropertyName,
                    definition.DisplayName,
                    definition.DataType,
                    definition.IsConfigurable,
                    definition.IsSensitive,
                    IsVisible: isVisible,
                    IsEditable: isEditable,
                    IsRequired: contributions.Any(static rule => rule.IsRequired),
                    IsMasked: isVisible && !hasVisibleUnmasked && hasVisibleMasked,
                    CanCreate: contributions.Any(static rule => rule.CanCreate),
                    CanUpdate: contributions.Any(static rule => rule.CanUpdate));
            })
            .ToArray();

        return new FieldAccessProfile(resourceKey, rules);
    }

    public static FieldPermissionOverrideState NormalizeOverride(
        bool isVisible,
        bool isEditable,
        bool isRequired,
        bool isMasked) =>
        new(
            IsVisible: isVisible,
            IsEditable: isVisible && isEditable,
            IsRequired: isVisible && isRequired,
            IsMasked: isVisible && isMasked);

    private static FieldAccessRule BuildRule(
        FieldCatalogDefinition definition,
        IReadOnlyDictionary<string, FieldPermissionOverrideState> overrides,
        RbacPermissionState screenState)
    {
        if (!definition.IsConfigurable)
        {
            return new FieldAccessRule(
                definition.FieldKey,
                definition.ResourceKey,
                definition.PropertyName,
                definition.DisplayName,
                definition.DataType,
                definition.IsConfigurable,
                definition.IsSensitive,
                IsVisible: screenState.HasAccess,
                IsEditable: false,
                IsRequired: false,
                IsMasked: false,
                CanCreate: false,
                CanUpdate: false);
        }

        var configured = overrides.TryGetValue(definition.FieldKey, out var overrideState)
            ? overrideState
            : NormalizeOverride(isVisible: true, isEditable: true, isRequired: false, isMasked: false);

        var isVisible = screenState.CanRead && configured.IsVisible;
        var isEditable = isVisible && configured.IsEditable && (screenState.CanCreate || screenState.CanUpdate);

        return new FieldAccessRule(
            definition.FieldKey,
            definition.ResourceKey,
            definition.PropertyName,
            definition.DisplayName,
            definition.DataType,
            definition.IsConfigurable,
            definition.IsSensitive,
            IsVisible: isVisible,
            IsEditable: isEditable,
            IsRequired: isVisible && configured.IsRequired,
            IsMasked: isVisible && configured.IsMasked,
            CanCreate: screenState.CanCreate && configured.IsEditable,
            CanUpdate: screenState.CanUpdate && configured.IsEditable);
    }
}

public sealed record FieldPermissionOverrideState(
    bool IsVisible,
    bool IsEditable,
    bool IsRequired,
    bool IsMasked);
