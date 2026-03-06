using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobCatalogItem : TenantEntity
{
    private JobCatalogItem()
    {
    }

    private JobCatalogItem(
        Guid publicId,
        JobCatalogCategory category,
        string code,
        string name,
        bool isSystem)
    {
        PublicId = publicId;
        Category = category;
        SetCode(code);
        SetName(name);
        IsSystem = isSystem;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public JobCatalogCategory Category { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static JobCatalogItem Create(
        JobCatalogCategory category,
        string code,
        string name,
        bool isSystem = false) =>
        new(Guid.NewGuid(), category, code, name, isSystem);

    public void Update(string code, string name)
    {
        SetCode(code);
        SetName(name);
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
        Code = JobProfileNormalization.Clean(code, nameof(code));
        NormalizedCode = JobProfileNormalization.NormalizeCode(code);
    }

    private void SetName(string name)
    {
        Name = JobProfileNormalization.Clean(name, nameof(name));
        NormalizedName = JobProfileNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
