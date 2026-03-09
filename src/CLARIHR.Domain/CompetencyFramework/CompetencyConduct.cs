using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

public sealed class CompetencyConduct : TenantEntity
{
    private readonly List<CompetencyConductBehavior> _behaviors = [];

    private CompetencyConduct()
    {
    }

    private CompetencyConduct(
        Guid publicId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string description,
        int sortOrder)
    {
        if (competencyCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyCatalogItemId), "Competency catalog item id must be greater than zero.");
        }

        if (competencyTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyTypeCatalogItemId), "Competency type catalog item id must be greater than zero.");
        }

        if (behaviorLevelCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behaviorLevelCatalogItemId), "Behavior level catalog item id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        PublicId = publicId;
        CompetencyCatalogItemId = competencyCatalogItemId;
        CompetencyTypeCatalogItemId = competencyTypeCatalogItemId;
        BehaviorLevelCatalogItemId = behaviorLevelCatalogItemId;
        Description = CompetencyFrameworkNormalization.Clean(description, nameof(description));
        NormalizedDescription = CompetencyFrameworkNormalization.NormalizeName(description);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public long CompetencyCatalogItemId { get; private set; }

    public long CompetencyTypeCatalogItemId { get; private set; }

    public long BehaviorLevelCatalogItemId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string NormalizedDescription { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<CompetencyConductBehavior> Behaviors => _behaviors;

    public static CompetencyConduct Create(
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string description,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            behaviorLevelCatalogItemId,
            description,
            sortOrder);

    public void Update(
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string description,
        int sortOrder)
    {
        if (competencyCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyCatalogItemId), "Competency catalog item id must be greater than zero.");
        }

        if (competencyTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyTypeCatalogItemId), "Competency type catalog item id must be greater than zero.");
        }

        if (behaviorLevelCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behaviorLevelCatalogItemId), "Behavior level catalog item id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        CompetencyCatalogItemId = competencyCatalogItemId;
        CompetencyTypeCatalogItemId = competencyTypeCatalogItemId;
        BehaviorLevelCatalogItemId = behaviorLevelCatalogItemId;
        Description = CompetencyFrameworkNormalization.Clean(description, nameof(description));
        NormalizedDescription = CompetencyFrameworkNormalization.NormalizeName(description);
        SortOrder = sortOrder;
        RefreshConcurrencyToken();
    }

    public void ReplaceBehaviors(IEnumerable<CompetencyConductBehavior> items)
    {
        _behaviors.Clear();
        _behaviors.AddRange(items);
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

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
