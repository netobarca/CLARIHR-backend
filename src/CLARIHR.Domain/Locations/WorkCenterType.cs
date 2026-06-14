using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Locations;

public sealed class WorkCenterType : TenantEntity
{
    private WorkCenterType()
    {
    }

    private WorkCenterType(
        Guid publicId,
        string code,
        string name,
        string? description,
        bool requiresAddress,
        bool requiresGeo,
        bool allowsBiometric)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = LocationNormalization.CleanOptional(description);
        RequiresAddress = requiresAddress;
        RequiresGeo = requiresGeo;
        AllowsBiometric = allowsBiometric;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool RequiresAddress { get; private set; }

    public bool RequiresGeo { get; private set; }

    public bool AllowsBiometric { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static WorkCenterType Create(
        string code,
        string name,
        string? description,
        bool requiresAddress,
        bool requiresGeo,
        bool allowsBiometric) =>
        new(Guid.NewGuid(), code, name, description, requiresAddress, requiresGeo, allowsBiometric);

    public void Update(
        string code,
        string name,
        string? description,
        bool requiresAddress,
        bool requiresGeo,
        bool allowsBiometric)
    {
        SetCode(code);
        SetName(name);
        Description = LocationNormalization.CleanOptional(description);
        RequiresAddress = requiresAddress;
        RequiresGeo = requiresGeo;
        AllowsBiometric = allowsBiometric;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
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
