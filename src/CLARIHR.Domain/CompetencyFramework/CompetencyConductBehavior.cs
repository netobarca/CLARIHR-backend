using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

public sealed class CompetencyConductBehavior : TenantEntity
{
    private CompetencyConductBehavior()
    {
    }

    private CompetencyConductBehavior(long behaviorCatalogItemId, string? notes, int sortOrder)
    {
        if (behaviorCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behaviorCatalogItemId), "Behavior catalog item id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        BehaviorCatalogItemId = behaviorCatalogItemId;
        Notes = CompetencyFrameworkNormalization.CleanOptional(notes);
        SortOrder = sortOrder;
    }

    public long CompetencyConductId { get; private set; }

    public CompetencyConduct CompetencyConduct { get; private set; } = null!;

    public long BehaviorCatalogItemId { get; private set; }

    public string? Notes { get; private set; }

    public int SortOrder { get; private set; }

    public static CompetencyConductBehavior Create(long behaviorCatalogItemId, string? notes, int sortOrder) =>
        new(behaviorCatalogItemId, notes, sortOrder);
}
