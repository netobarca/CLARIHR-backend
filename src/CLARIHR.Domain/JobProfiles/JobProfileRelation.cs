using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileRelation : TenantEntity
{
    private JobProfileRelation()
    {
    }

    private JobProfileRelation(
        JobRelationType relationType,
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string counterpart,
        string? notes,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        RelationType = relationType;
        CatalogItem = catalogItem;
        CatalogItemId = catalogItem?.Id ?? catalogItemId;
        Counterpart = JobProfileNormalization.Clean(counterpart, nameof(counterpart));
        Notes = JobProfileNormalization.CleanOptional(notes);
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long JobProfileId { get; private set; }

    public JobProfile JobProfile { get; private set; } = null!;

    public JobRelationType RelationType { get; private set; }

    public long? CatalogItemId { get; private set; }

    public JobCatalogItem? CatalogItem { get; private set; }

    public string Counterpart { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static JobProfileRelation Create(
        JobRelationType relationType,
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string counterpart,
        string? notes,
        int sortOrder) =>
        new(relationType, catalogItemId, catalogItem, counterpart, notes, sortOrder);

    public void Update(
        JobRelationType relationType,
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string counterpart,
        string? notes,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        RelationType = relationType;
        CatalogItem = catalogItem;
        CatalogItemId = catalogItem?.Id ?? catalogItemId;
        Counterpart = JobProfileNormalization.Clean(counterpart, nameof(counterpart));
        Notes = JobProfileNormalization.CleanOptional(notes);
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }
}
