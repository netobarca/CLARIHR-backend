using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Locations;

public sealed class LocationHierarchyConfig : TenantEntity
{
    private LocationHierarchyConfig()
    {
    }

    private LocationHierarchyConfig(
        Guid publicId,
        bool isMultiLevel,
        string defaultGroupCode,
        string defaultGroupName)
    {
        PublicId = publicId;
        IsMultiLevel = isMultiLevel;
        DefaultGroupCode = LocationNormalization.NormalizeCode(defaultGroupCode);
        DefaultGroupName = LocationNormalization.Clean(defaultGroupName, nameof(defaultGroupName));
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public bool IsMultiLevel { get; private set; }

    public string DefaultGroupCode { get; private set; } = string.Empty;

    public string DefaultGroupName { get; private set; } = string.Empty;

    public Guid ConcurrencyToken { get; private set; }

    public static LocationHierarchyConfig Create(bool isMultiLevel, string defaultGroupCode, string defaultGroupName) =>
        new(Guid.NewGuid(), isMultiLevel, defaultGroupCode, defaultGroupName);

    public void Update(bool isMultiLevel, string? defaultGroupCode = null, string? defaultGroupName = null)
    {
        IsMultiLevel = isMultiLevel;
        if (defaultGroupCode is not null)
        {
            DefaultGroupCode = LocationNormalization.NormalizeCode(defaultGroupCode);
        }

        if (defaultGroupName is not null)
        {
            DefaultGroupName = LocationNormalization.Clean(defaultGroupName, nameof(defaultGroupName));
        }

        RefreshConcurrencyToken();
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
