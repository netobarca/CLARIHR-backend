using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Locations;

public sealed class LocationGroup : TenantEntity
{
    private LocationGroup()
    {
    }

    private LocationGroup(
        Guid publicId,
        int levelOrder,
        string code,
        string name,
        long? parentId,
        string? description,
        bool isDefault)
    {
        if (levelOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(levelOrder), "Level order must be greater than zero.");
        }

        PublicId = publicId;
        LevelOrder = levelOrder;
        SetCode(code);
        SetName(name);
        ParentId = parentId;
        Description = LocationNormalization.CleanOptional(description);
        IsActive = true;
        IsDefault = isDefault;
        ConcurrencyToken = Guid.NewGuid();
    }

    public int LevelOrder { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public long? ParentId { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsDefault { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static LocationGroup Create(
        int levelOrder,
        string code,
        string name,
        long? parentId,
        string? description,
        bool isDefault = false) =>
        new(Guid.NewGuid(), levelOrder, code, name, parentId, description, isDefault);

    public void Update(string code, string name, string? description)
    {
        if (IsDefault &&
            (!Code.Equals(LocationNormalization.Clean(code, nameof(code)), StringComparison.Ordinal) ||
             !Name.Equals(LocationNormalization.Clean(name, nameof(name)), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The default group identity cannot be changed.");
        }

        SetCode(code);
        SetName(name);
        Description = LocationNormalization.CleanOptional(description);
        RefreshConcurrencyToken();
    }

    public void Move(long? parentId)
    {
        ParentId = parentId;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        if (IsDefault)
        {
            throw new InvalidOperationException("The default group cannot be inactivated.");
        }

        IsActive = false;
        RefreshConcurrencyToken();
    }

    private void SetCode(string code)
    {
        Code = LocationNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = LocationNormalization.Clean(name, nameof(name));
        NormalizedName = LocationNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
