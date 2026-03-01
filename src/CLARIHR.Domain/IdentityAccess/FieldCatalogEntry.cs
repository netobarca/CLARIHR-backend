using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class FieldCatalogEntry : AuditableEntity
{
    private FieldCatalogEntry()
    {
    }

    private FieldCatalogEntry(
        string fieldKey,
        string resourceKey,
        string propertyName,
        string displayName,
        bool isConfigurable,
        bool isSensitive,
        string dataType)
    {
        FieldKey = IdentityNormalization.Clean(fieldKey, nameof(fieldKey));
        NormalizedFieldKey = IdentityNormalization.Normalize(fieldKey);
        ResourceKey = IdentityNormalization.Clean(resourceKey, nameof(resourceKey));
        NormalizedResourceKey = IdentityNormalization.Normalize(resourceKey);
        PropertyName = IdentityNormalization.Clean(propertyName, nameof(propertyName));
        NormalizedPropertyName = IdentityNormalization.Normalize(propertyName);
        DisplayName = IdentityNormalization.Clean(displayName, nameof(displayName));
        IsConfigurable = isConfigurable;
        IsSensitive = isSensitive;
        DataType = IdentityNormalization.Clean(dataType, nameof(dataType));
    }

    public string FieldKey { get; private set; } = string.Empty;

    public string NormalizedFieldKey { get; private set; } = string.Empty;

    public string ResourceKey { get; private set; } = string.Empty;

    public string NormalizedResourceKey { get; private set; } = string.Empty;

    public string PropertyName { get; private set; } = string.Empty;

    public string NormalizedPropertyName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsConfigurable { get; private set; }

    public bool IsSensitive { get; private set; }

    public string DataType { get; private set; } = string.Empty;

    public static FieldCatalogEntry Create(
        string fieldKey,
        string resourceKey,
        string propertyName,
        string displayName,
        bool isConfigurable,
        bool isSensitive,
        string dataType) =>
        new(fieldKey, resourceKey, propertyName, displayName, isConfigurable, isSensitive, dataType);
}
