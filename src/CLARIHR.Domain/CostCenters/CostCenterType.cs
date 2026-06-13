using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CostCenters;

public sealed class CostCenterType : TenantEntity
{
    private CostCenterType()
    {
    }

    private CostCenterType(
        Guid publicId,
        string code,
        string name,
        string? description)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = CostCenterNormalization.CleanOptional(description);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CostCenterType Create(string code, string name, string? description) =>
        new(Guid.NewGuid(), code, name, description);

    public void Update(string code, string name, string? description)
    {
        SetCode(code);
        SetName(name);
        Description = CostCenterNormalization.CleanOptional(description);
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
        Code = CostCenterNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = CostCenterNormalization.Clean(name, nameof(name));
        NormalizedName = CostCenterNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
