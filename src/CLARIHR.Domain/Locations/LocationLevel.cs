using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Locations;

public sealed class LocationLevel : TenantEntity
{
    private LocationLevel()
    {
    }

    private LocationLevel(
        Guid publicId,
        int levelOrder,
        string displayName,
        bool isActive,
        bool isRequired,
        bool allowsWorkCenters)
    {
        if (levelOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(levelOrder), "Level order must be greater than zero.");
        }

        if (isRequired && !isActive)
        {
            throw new InvalidOperationException("Required levels must be active.");
        }

        PublicId = publicId;
        LevelOrder = levelOrder;
        DisplayName = LocationNormalization.Clean(displayName, nameof(displayName));
        IsActive = isActive;
        IsRequired = isRequired;
        AllowsWorkCenters = allowsWorkCenters;
        ConcurrencyToken = Guid.NewGuid();
    }

    public int LevelOrder { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool IsRequired { get; private set; }

    public bool AllowsWorkCenters { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static LocationLevel Create(
        int levelOrder,
        string displayName,
        bool isActive,
        bool isRequired,
        bool allowsWorkCenters) =>
        new(Guid.NewGuid(), levelOrder, displayName, isActive, isRequired, allowsWorkCenters);

    public void Update(string displayName, bool isActive, bool isRequired, bool allowsWorkCenters)
    {
        if (isRequired && !isActive)
        {
            throw new InvalidOperationException("Required levels must be active.");
        }

        DisplayName = LocationNormalization.Clean(displayName, nameof(displayName));
        IsActive = isActive;
        IsRequired = isRequired;
        AllowsWorkCenters = allowsWorkCenters;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        if (IsRequired)
        {
            throw new InvalidOperationException("Required levels cannot be inactivated.");
        }

        IsActive = false;
        RefreshConcurrencyToken();
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
