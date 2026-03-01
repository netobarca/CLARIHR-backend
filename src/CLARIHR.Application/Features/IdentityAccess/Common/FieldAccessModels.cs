namespace CLARIHR.Application.Features.IdentityAccess.Common;

public sealed record FieldCatalogDefinition(
    string FieldKey,
    string ResourceKey,
    string PropertyName,
    string DisplayName,
    string DataType,
    bool IsConfigurable,
    bool IsSensitive);

public sealed record FieldAccessRule(
    string FieldKey,
    string ResourceKey,
    string PropertyName,
    string DisplayName,
    string DataType,
    bool IsConfigurable,
    bool IsSensitive,
    bool IsVisible,
    bool IsEditable,
    bool IsRequired,
    bool IsMasked,
    bool CanCreate,
    bool CanUpdate)
{
    public bool CanWrite(RbacPermissionAction action) =>
        action switch
        {
            RbacPermissionAction.Create => IsEditable && CanCreate,
            RbacPermissionAction.Update => IsEditable && CanUpdate,
            _ => false
        };
}

public sealed class FieldAccessProfile
{
    private readonly IReadOnlyDictionary<string, FieldAccessRule> _rulesByFieldKey;

    public FieldAccessProfile(string resourceKey, IReadOnlyCollection<FieldAccessRule> rules)
    {
        ResourceKey = resourceKey;
        _rulesByFieldKey = rules.ToDictionary(static rule => rule.FieldKey, StringComparer.OrdinalIgnoreCase);
        Rules = rules;
    }

    public string ResourceKey { get; }

    public IReadOnlyCollection<FieldAccessRule> Rules { get; }

    public FieldAccessRule GetRule(string fieldKey) => _rulesByFieldKey[fieldKey];

    public bool CanView(string fieldKey) =>
        _rulesByFieldKey.TryGetValue(fieldKey, out var rule) && rule.IsVisible;

    public bool CanWrite(string fieldKey, RbacPermissionAction action) =>
        _rulesByFieldKey.TryGetValue(fieldKey, out var rule) && rule.CanWrite(action);
}
