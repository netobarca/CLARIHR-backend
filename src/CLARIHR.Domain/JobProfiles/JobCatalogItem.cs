using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobCatalogItem : TenantEntity
{
    // H-11 — domain-owned length invariants, mirroring the sibling catalogs of the section. `MaxNameLength`
    // was 120 while the position-description family used 150; harmonised UPWARD, which can never reject a
    // value that fit before.
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 500;

    private JobCatalogItem()
    {
    }

    private JobCatalogItem(
        Guid publicId,
        JobCatalogCategory category,
        string code,
        string name,
        string? description,
        int sortOrder,
        bool isSystem)
    {
        PublicId = publicId;
        Category = category;
        SetCode(code);
        SetName(name);
        SetDescription(description);
        SortOrder = sortOrder;
        IsSystem = isSystem;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public JobCatalogCategory Category { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>
    /// H-11 — the catalog had no description at all, so a competency dictionary had nowhere to record what a
    /// competency means. Every sibling catalog of the section already carried one.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// H-11 — the catalog had no ordering field, so a picker over its 12 competencies could only be
    /// alphabetical: `LIDERAZGO` could not be shown first on a directive profile. Deliberately NOT unique,
    /// matching the position-description family; only the occupational pyramid enforces uniqueness, because
    /// there the order IS a strict ranking.
    /// </summary>
    public int SortOrder { get; private set; }

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static JobCatalogItem Create(
        JobCatalogCategory category,
        string code,
        string name,
        string? description = null,
        int sortOrder = 0,
        bool isSystem = false) =>
        new(Guid.NewGuid(), category, code, name, description, sortOrder, isSystem);

    public void Update(string code, string name, string? description, int sortOrder)
    {
        SetCode(code);
        SetName(name);
        SetDescription(description);
        SortOrder = sortOrder;
        RefreshConcurrencyToken();
    }

    /// <summary>
    /// H-11 — bulk reorder. Rewrites only the ordering field so a drag-and-drop save is one transaction
    /// instead of N patches. Kept separate from <see cref="Update"/> because it must not touch code or name.
    /// </summary>
    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
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
        Code = JobProfileNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = JobProfileNormalization.Clean(name, nameof(name));
        NormalizedName = JobProfileNormalization.NormalizeName(name);

        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }
    }

    private void SetDescription(string? description)
    {
        Description = JobProfileNormalization.CleanOptional(description);

        if (Description is { Length: > MaxDescriptionLength })
        {
            throw new ArgumentException(
                $"Description must be {MaxDescriptionLength} characters or fewer.", nameof(description));
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
